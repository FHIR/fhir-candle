// <copyright file="SmartVisibilityTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

extern alias candleR4;
extern alias candleR4B;
extern alias candleR5;

using FhirCandle.Models;
using FhirCandle.Storage;
using FhirCandle.Utils;
using Hl7.Fhir.Model;
using System.Net;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using static FhirCandle.Storage.Common;

namespace fhir.candle.Tests;

public class SmartVisibilityTests
{
    public static IEnumerable<object[]> Versions => FhirStoreTests.TestConfigurations;

    private static FhirRequestContext Request(IFhirStore store, string path, AuthorizationInfo? auth) => new()
    {
        Store = store,
        TenantName = store.Config.ControllerName,
        HttpMethod = "GET",
        Url = $"{store.Config.BaseUrl}/{path}",
        Authorization = auth,
        DestinationFormat = "application/fhir+json",
    };

    private static IFhirStore CreateStore(FhirReleases.FhirSequenceCodes version)
    {
        IFhirStore store = version switch
        {
            FhirReleases.FhirSequenceCodes.R4 => new candleR4::FhirCandle.Storage.VersionedFhirStore(),
            FhirReleases.FhirSequenceCodes.R4B => new candleR4B::FhirCandle.Storage.VersionedFhirStore(),
            _ => new candleR5::FhirCandle.Storage.VersionedFhirStore(),
        };
        store.Init(new()
        {
            FhirVersion = version, ControllerName = "visibility", BaseUrl = "http://localhost/fhir/visibility",
            AllowExistingId = true, AllowCreateAsUpdate = true,
        });
        void Put(string path, string body)
        {
            FhirRequestContext ctx = Request(store, path, null) with
            {
                HttpMethod = "PUT", SourceFormat = "application/fhir+json", SourceContent = body,
            };
            Assert.True(store.InstanceUpdate(ctx, out FhirResponseContext response), response.SerializedOutcome);
        }

        Put("CompartmentDefinition/patient", """
            {"resourceType":"CompartmentDefinition","id":"patient","url":"urn:visibility:patient","name":"PatientMembership",
             "status":"active","code":"Patient","search":true,"resource":[
             {"code":"Observation","param":["unknown-param","subject","performer"]},
             {"code":"Practitioner","param":["unknown-param"]}]}
            """);
        foreach (string id in new[] { "alpha", "beta" })
        {
            Put($"Patient/{id}", $$"""{"resourceType":"Patient","id":"{{id}}"}""");
        }
        Put("Practitioner/doctor", """{"resourceType":"Practitioner","id":"doctor"}""");
        foreach ((string id, string subject, string performer) in new[]
        {
            ("one", "alpha", "Practitioner/doctor"), ("two", "beta", "Patient/alpha"), ("other", "beta", "Practitioner/doctor"),
        })
        {
            Put($"Observation/{id}", $$"""
                {"resourceType":"Observation","id":"{{id}}","status":"final","code":{"text":"test"},
                 "subject":{"reference":"Patient/{{subject}}"},"performer":[{"reference":"{{performer}}"}]}
                """);
        }
        return store;
    }

    private static AuthorizationInfo Grant(params string[] patientScopes) => new()
    {
        Tenant = "visibility", RemoteIpAddress = "local", RequestParameters = new(),
        LaunchPatient = "Patient/alpha", PatientScopes = [.. patientScopes],
    };

    private static FhirResponseContext Search(IFhirStore store, string path, AuthorizationInfo? auth)
    {
        FhirRequestContext ctx = Request(store, path, auth);
        FhirResponseContext response;
        bool success = ctx.Interaction switch
        {
            StoreInteractionCodes.CompartmentSearch => store.CompartmentSearch(ctx, out response),
            StoreInteractionCodes.CompartmentTypeSearch => store.CompartmentTypeSearch(ctx, out response),
            _ => store.PerformInteraction(ctx, out response, true),
        };
        Assert.True(success, response.SerializedOutcome);
        return response;
    }

    private static void AssertEntries(FhirResponseContext response, int total, params string[] entries)
    {
        JsonNode bundle = JsonNode.Parse(response.SerializedResource)!;
        Assert.Equal(total, (int?)bundle["total"]);
        Assert.Equal(entries.Order(), (bundle["entry"]?.AsArray() ?? []).Select(entry =>
            $"{entry!["search"]!["mode"]}:{entry["resource"]!["resourceType"]}/{entry["resource"]!["id"]}").Order());
    }

    private static void AssertRead(IFhirStore store, string path, AuthorizationInfo? auth, bool visible)
    {
        FhirRequestContext ctx = Request(store, path, auth);
        if (!visible)
        {
            ctx = ctx with { IfMatch = "unmatched" };
        }
        Assert.Equal(visible, store.InstanceRead(ctx, out FhirResponseContext response));
        if (visible)
        {
            JsonNode resource = JsonNode.Parse(response.SerializedResource)!;
            Assert.Equal(path, $"{resource["resourceType"]}/{resource["id"]}");
            return;
        }
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Null(response.Resource);
        Assert.Empty(response.SerializedResource);
        Assert.Empty(response.ETag);
        Assert.Empty(response.LastModified);
        Assert.Empty(response.Location);
        Assert.DoesNotContain(path, response.SerializedOutcome);
    }

    [Theory]
    [MemberData(nameof(Versions))]
    public void ReturnedResourcesRespectScopeAndPatient(FhirReleases.FhirSequenceCodes version)
    {
        using IFhirStore store = CreateStore(version);
        AuthorizationInfo grant = Grant("Patient.r", "Observation.r", "*.s");
        AssertRead(store, "Patient/alpha", grant, true);
        AssertRead(store, "Patient/beta", grant, false);
        AssertRead(store, "Observation/one", grant, true);
        AssertRead(store, "Observation/two", grant, true);
        AssertRead(store, "Observation/other", grant, false);
        Assert.False(store.InstanceRead(Request(store, "Observation/absent", grant), out FhirResponseContext missing));
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Contains("Observation/absent not found", missing.SerializedOutcome);
        AssertRead(store, "Observation/one", Grant("Observation.s"), false);
        AssertRead(store, "Practitioner/doctor", Grant("*.r"), false);
        AssertEntries(Search(store, "Observation", Grant("Observation.r")), 0);

        const string One = "match:Observation/one", Two = "match:Observation/two", Alpha = "match:Patient/alpha";
        foreach (string path in new[] { "Observation", "Patient/alpha/*", "Patient/alpha/Observation" })
        {
            AssertEntries(Search(store, path, grant), 2, One, Two);
            AssertEntries(Search(store, path + "?_summary=count", grant), 2);
        }
        AssertEntries(Search(store, "?_type=Observation,Patient,Practitioner", grant), 3, One, Two, Alpha);
        AssertEntries(Search(store, "?_type=Observation,Patient&_include=Observation:subject", grant), 3, One, Two, Alpha);
        AssertEntries(Search(store, "?_type=Observation,Patient,Practitioner&_summary=count", grant), 3);
        AssertEntries(Search(store, "Patient/alpha/*", Grant("Patient.s")), 0);
        AssertEntries(Search(store, "Patient/beta/Observation", Grant("Observation.s")), 1, Two);
        const string Forward = "Observation?_include=Observation:subject&_include=Observation:performer";
        AssertEntries(Search(store, Forward, grant), 2, One, Two, "include:Patient/alpha");
        grant.UserScopes.Add("Practitioner.s");
        AssertEntries(Search(store, Forward, grant), 2, One, Two, "include:Patient/alpha", "include:Practitioner/doctor");
        grant.UserScopes.Clear();
        AssertEntries(Search(store, Forward, Grant("Observation.s", "Patient.r")), 2, One, Two);
        const string Reverse = "Patient?_revinclude=Observation:subject";
        AssertEntries(Search(store, Reverse, grant), 1, Alpha, "include:Observation/one");
        AssertEntries(Search(store, Reverse, Grant("Patient.s", "Observation.r")), 1, Alpha);

        grant.LaunchPatient = string.Empty;
        AssertRead(store, "Patient/alpha", grant, false);
        AssertEntries(Search(store, "Observation", grant), 0);
        grant.UserScopes.Add("Observation.r");
        grant.UserScopes.Add("Observation.s");
        AssertRead(store, "Observation/other", grant, true);
        AssertEntries(Search(store, "Observation", grant), 3, One, Two, "match:Observation/other");
        AssertRead(store, "Patient/beta", null, true);
        AssertEntries(Search(store, "Patient", null), 2, Alpha, "match:Patient/beta");
    }

    // The hook interfaces live in the three aliased assemblies; all use the same response contract.
    public class ReplacementHook : DispatchProxy
    {
        internal HookRequestStateCodes State;
        internal FhirResponseContext Replacement = new();
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name == "get_HookRequestStates")
            {
                return new HashSet<HookRequestStateCodes> { State };
            }
            Assert.Equal("DoInteractionHook", method.Name);
            args![^1] = Replacement;
            return true;
        }
    }

    private static ReplacementHook InstallHook<THook>(IFhirStore store) where THook : class
    {
        THook hook = DispatchProxy.Create<THook, ReplacementHook>();
        FieldInfo field = store.GetType().GetField("_hooksByInteractionByResource", BindingFlags.NonPublic | BindingFlags.Instance)!;
        Dictionary<string, Dictionary<StoreInteractionCodes, THook[]>> hooks =
            (Dictionary<string, Dictionary<StoreInteractionCodes, THook[]>>)field.GetValue(store)!;
        hooks["Observation"] = new()
        {
            [StoreInteractionCodes.InstanceRead] = [hook],
            [StoreInteractionCodes.TypeSearch] = [hook],
        };
        return (ReplacementHook)(object)hook;
    }

    [Theory]
    [MemberData(nameof(Versions))]
    public void HookReplacementsRespectVisibility(FhirReleases.FhirSequenceCodes version)
    {
        using IFhirStore store = CreateStore(version);
        AuthorizationInfo grant = Grant("Observation.r", "Observation.s", "Patient.s");
        Bundle replacement = Assert.IsType<Bundle>(Search(store, "Observation?_include=Observation:subject", null).Resource);
        replacement.Total = 999;
        replacement.Link = [new() { Relation = "next", Url = "urn:unverified" }];
        ReplacementHook hook = version switch
        {
            FhirReleases.FhirSequenceCodes.R4 => InstallHook<candleR4::FhirCandle.Interactions.IFhirInteractionHook>(store),
            FhirReleases.FhirSequenceCodes.R4B => InstallHook<candleR4B::FhirCandle.Interactions.IFhirInteractionHook>(store),
            _ => InstallHook<candleR5::FhirCandle.Interactions.IFhirInteractionHook>(store),
        };
        foreach ((HookRequestStateCodes state, HttpStatusCode? status) in new[]
        {
            (HookRequestStateCodes.Pre, (HttpStatusCode?)HttpStatusCode.OK),
            (HookRequestStateCodes.Post, HttpStatusCode.OK),
            (HookRequestStateCodes.Post, null),
        })
        {
            hook.State = state;
            hook.Replacement = new()
            {
                StatusCode = status, Resource = store["Observation"]["other"],
                Location = "urn:unverified", ETag = "unverified", SerializedResource = "unverified", SerializedOutcome = "unverified",
            };
            AssertRead(store, "Observation/one", grant, false);
            hook.Replacement = hook.Replacement with { Resource = store["Observation"]["two"] };
            Assert.True(store.InstanceRead(Request(store, "Observation/one", grant), out FhirResponseContext read));
            JsonNode body = JsonNode.Parse(read.SerializedResource)!;
            Assert.Equal("two", (string?)body["id"]);
            Assert.Equal($"{store.Config.BaseUrl}/Observation/two", read.Location);
            Assert.Equal($"W/\"{body["meta"]!["versionId"]}\"", read.ETag);
            Assert.DoesNotContain("unverified", read.SerializedOutcome);
            hook.Replacement = hook.Replacement with { Resource = replacement };
            FhirResponseContext search = Search(store, "Observation", grant);
            AssertEntries(search, 2, "match:Observation/one", "match:Observation/two", "include:Patient/alpha");
            Assert.DoesNotContain("unverified", search.SerializedResource + search.SerializedOutcome);
            AssertEntries(Search(store, "Observation?_summary=count", grant), 2);
            Assert.True(store.TypeSearch(Request(store, "Observation", grant) with
            {
                DestinationFormat = "application/fhir+xml",
            }, out FhirResponseContext xmlResponse));
            XElement xml = XElement.Parse(xmlResponse.SerializedResource);
            XNamespace fhir = xml.Name.Namespace;
            Assert.Equal("2", (string?)xml.Element(fhir + "total")?.Attribute("value"));
            Assert.Null(xml.Element(fhir + "link"));
            Assert.Equal(new[] { "alpha", "one", "two" }, xml.Elements(fhir + "entry")
                .Select(e => (string?)e.Element(fhir + "resource")!.Elements().Single().Element(fhir + "id")!.Attribute("value")).Order());
        }
        hook.State = HookRequestStateCodes.Pre;
        hook.Replacement = hook.Replacement with { StatusCode = HttpStatusCode.OK };
        Assert.True(store.PerformInteraction(Request(store, "Observation", grant), out FhirResponseContext objects, false));
        Assert.Empty(objects.SerializedResource);
        Assert.Empty(objects.SerializedOutcome);
        Assert.Empty(objects.Location);
        Assert.Empty(objects.ETag);
        Assert.Equal(2, Assert.IsType<Bundle>(objects.Resource).Total);
        AuthorizationInfo administrator = new()
        {
            Key = Guid.Empty.ToString(), Tenant = "visibility", RemoteIpAddress = "local",
            RequestParameters = new(), UserScopes = ["*.*"],
        };
        foreach (AuthorizationInfo? trusted in new[] { null, administrator })
        {
            FhirResponseContext trustedResult = Search(store, "Observation", trusted);
            Assert.Same(replacement, trustedResult.Resource);
            Assert.Equal(999, (int?)JsonNode.Parse(trustedResult.SerializedResource)!["total"]);
        }
        hook.Replacement = new() { StatusCode = HttpStatusCode.OK, SerializedResource = "unverified" };
        AssertRead(store, "Observation/one", grant, false);
        Assert.False(store.TypeSearch(Request(store, "Observation", grant), out FhirResponseContext denied));
        Assert.Empty(denied.SerializedResource);
        hook.Replacement = new()
        {
            StatusCode = HttpStatusCode.BadRequest,
            Outcome = new OperationOutcome
            {
                Issue = [new() { Severity = OperationOutcome.IssueSeverity.Error, Code = OperationOutcome.IssueType.Invalid,
                    Diagnostics = "unverified" }],
            },
        };
        Assert.False(store.TypeSearch(Request(store, "Observation", grant), out FhirResponseContext diagnostic));
        Assert.Equal(HttpStatusCode.BadRequest, diagnostic.StatusCode);
        Assert.Equal("invalid", (string?)JsonNode.Parse(diagnostic.SerializedOutcome)!["issue"]![0]!["code"]);
        Assert.DoesNotContain("unverified", diagnostic.SerializedOutcome);
    }
}
