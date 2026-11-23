// <copyright file="SearchPageTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

extern alias candleR4;
extern alias candleR4B;
extern alias candleR5;

using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using FhirCandle.Models;
using FhirCandle.Storage;
using FhirCandle.Utils;
using static FhirCandle.Storage.Common;

namespace fhir.candle.Tests;

public class SearchPageTests
{
    public static IEnumerable<object[]> Versions => LifecycleTestStore.Versions;

    private static JsonObject Page(IFhirStore store, string path, Action<string> observe, AuthorizationInfo? auth = null)
    {
        FhirRequestContext ctx = new(store, "GET", path)
        {
            Authorization = auth, DestinationFormat = "application/fhir+json",
        };
        ConditionalWeakTable<FhirRequestContext, Action<string>> observers = store switch
        {
            candleR4::FhirCandle.Storage.VersionedFhirStore r4 => r4.PageObservers,
            candleR4B::FhirCandle.Storage.VersionedFhirStore r4b => r4b.PageObservers,
            candleR5::FhirCandle.Storage.VersionedFhirStore r5 => r5.PageObservers,
            _ => throw new ArgumentException(nameof(store)),
        };
        observers.Add(ctx, observe);
        Assert.True(store.TypeSearch(ctx, out FhirResponseContext response), response.SerializedOutcome);
        return JsonNode.Parse(response.SerializedResource)!.AsObject();
    }

    private static string[] Ids(JsonObject bundle, string mode = "match") =>
        (bundle["entry"]?.AsArray() ?? []).Where(e => (string?)e!["search"]!["mode"] == mode)
            .Select(e => (string)e!["resource"]!["id"]!).ToArray();

    private static string? Next(JsonObject bundle) =>
        (string?)bundle["link"]?.AsArray().SingleOrDefault(l => (string?)l!["relation"] == "next")?["url"];

    private static AuthorizationInfo Grant() => new()
    {
        Tenant = "lifecycle", RemoteIpAddress = "local", RequestParameters = new(),
        PatientScopes = ["Patient.s", "Observation.s"], LaunchPatient = "Patient/a",
        UserScopes = ["Practitioner.s"],
    };

    private static void SeedObservations(LifecycleTestStore fixture)
    {
        fixture.Put(JsonNode.Parse("""
            {"resourceType":"CompartmentDefinition","id":"membership","url":"urn:test:membership","name":"Membership",
             "status":"active","code":"Patient","search":true,"resource":[{"code":"Observation","param":["subject"]}]}
            """)!.AsObject());
        fixture.Put(new() { ["resourceType"] = "Patient", ["id"] = "a" });
        fixture.Put(new() { ["resourceType"] = "Practitioner", ["id"] = "doctor" });
        for (int i = 0; i < 6; i++)
        {
            JsonObject row = JsonNode.Parse("""
                {"resourceType":"Observation","status":"final","code":{"text":"sample"},
                 "hasMember":[{"reference":"Observation/o1"}],"subject":{"reference":"Patient/a"}}
                """)!.AsObject();
            row["id"] = $"o{i}";
            if (i != 0)
            {
                row["valueQuantity"] = new JsonObject { ["value"] = i % 2 == 0 ? 2 : 10 };
                row["effectiveDateTime"] = i % 2 == 0 ? "2020-01-01" : "2021-01-01";
            }
            if (i == 5)
            {
                row["subject"]!["reference"] = "Patient/b";
            }
            fixture.Put(row);
        }
    }

    [Theory]
    [MemberData(nameof(Versions))]
    public void TypePageConstructsOnlySelectedMatches(FhirReleases.FhirSequenceCodes version)
    {
        using LifecycleTestStore fixture = new(version);
        SeedObservations(fixture);
        AuthorizationInfo auth = Grant();
        List<string> events = [];
        foreach (string sort in new[] { "", "value-quantity,_id", "-value-quantity,-_id", "date,_id", "-date,-_id" })
        {
            string path = "Observation?status=final&status=final&_include=Observation:subject&_include=Observation:has-member" +
                (sort == "" ? "" : "&_sort=" + sort);
            string[] oracle = Ids(Page(fixture.Store, path, _ => { }, auth));
            Assert.Equal(5, oracle.Length);
            if (sort != "")
            {
                Assert.Equal(sort.StartsWith('-') ? new[] { "o3", "o1", "o4", "o2", "o0" } :
                    new[] { "o0", "o2", "o4", "o1", "o3" }, oracle);
            }
            string? link = path + "&_count=2";
            for (int offset = 0; offset < 5; offset += 2)
            {
                events.Clear();
                JsonObject page = Page(fixture.Store, link!, events.Add, auth);
                string[] matches = Ids(page);
                Assert.Equal(5, (int?)page["total"]);
                Assert.Equal(oracle.Skip(offset).Take(2), matches);
                Assert.Equal(matches.Length, events.Count(e => e == "detached"));
                Assert.Equal(matches.Length, events.Count(e => e == "expanded"));
                Assert.Equal(matches.Contains("o1") ? new[] { "a" } : new[] { "a", "o1" }, Ids(page, "include").Order());
                link = Next(page);
                if (offset < 4)
                {
                    Assert.NotNull(link);
                    System.Collections.Specialized.NameValueCollection query = System.Web.HttpUtility.ParseQueryString(new Uri(link).Query);
                    Assert.Equal(new[] { "final", "final" }, query.GetValues("status"));
                    Assert.Equal(new[] { (offset + 2).ToString() }, query.GetValues("_offset"));
                    link = "Observation" + new Uri(link).Query;
                }
            }
            Assert.Null(link);
        }
        foreach (string count in new[] { "_count=0", "_summary=count" })
        {
            events.Clear();
            JsonObject page = Page(fixture.Store, $"Observation?{count}&_include=Observation:subject&_offset=99&_maxresults=1&_total=none", events.Add, auth);
            Assert.Equal(5, (int?)page["total"]);
            Assert.Equal(new[] { "selected" }, events);
            Assert.Empty(Ids(page));
            Assert.Null(Next(page));
        }
        foreach (string invalid in new[] { "-1", "2147483648", "", "1.0", "1&_offset=2" })
        {
            Assert.Equal(HttpStatusCode.BadRequest, fixture.Interact("GET", "Observation?_offset=" + invalid).Response.StatusCode);
        }
        foreach (string total in new[] { "none", "estimate", "accurate" })
        {
            JsonObject capped = Page(fixture.Store, $"Observation?_total={total}&_count=2&_offset=2&_maxresults=3", events.Add, auth);
            Assert.Equal(total == "none" ? (int?)null : 5, (int?)capped["total"]);
            Assert.Single(Ids(capped));
            Assert.Null(Next(capped));
        }

        fixture.Seed(subscription: false);
        for (int i = 0; i < 5; i++)
        {
            JsonObject subscription = fixture.SubscriptionJson(i == 0 ? "off" : "active");
            subscription["id"] = $"s{i}";
            fixture.Put(subscription);
        }
        events.Clear();
        JsonObject subscriptions = Page(fixture.Store, "Subscription?status=active&_sort=_id&_count=2&_offset=1", events.Add);
        Assert.Equal(new[] { "s2", "s3" }, Ids(subscriptions));
        Assert.Equal(4, (int?)subscriptions["total"]);
        Assert.Equal(2, events.Count(e => e == "detached"));
        events.Clear();
        Assert.Equal(4, (int?)Page(fixture.Store, "Subscription?status=active&_count=0", events.Add)["total"]);
        Assert.Equal(new[] { "selected" }, events);
    }

    [Theory]
    [MemberData(nameof(Versions))]
    public void RecheckPrecedesBundleAndExpansion(FhirReleases.FhirSequenceCodes version)
    {
        using LifecycleTestStore fixture = new(version);
        SeedObservations(fixture);
        List<string> events = [];
        JsonObject page = Page(fixture.Store, "Observation?_id=o0&_include=Observation:performer", boundary =>
        {
            events.Add(boundary);
            if (boundary == "selected")
            {
                fixture.Put(JsonNode.Parse("""
                    {"resourceType":"Observation","id":"o0","code":{"text":"replacement"},"status":"final",
                     "subject":{"reference":"Patient/b"},"performer":[{"reference":"Practitioner/doctor"}]}
                    """)!.AsObject());
            }
        }, Grant());
        Assert.Empty(page["entry"]?.AsArray() ?? []);
        Assert.Equal(new[] { "selected", "detached" }, events);
    }

    [Theory]
    [MemberData(nameof(Versions))]
    public void ConditionalWritesIgnorePresentation(FhirReleases.FhirSequenceCodes version)
    {
        foreach (StoreInteractionCodes route in new[]
        {
            StoreInteractionCodes.TypeCreateConditional, StoreInteractionCodes.InstanceUpdateConditional,
            StoreInteractionCodes.TypeDeleteConditional, StoreInteractionCodes.SystemDeleteConditional,
        })
        {
            using LifecycleTestStore fixture = new(version);
            bool post = route == StoreInteractionCodes.TypeCreateConditional;
            bool put = route == StoreInteractionCodes.InstanceUpdateConditional;
            bool system = route == StoreInteractionCodes.SystemDeleteConditional;
            bool delete = !post && !put;
            foreach (string id in new[] { "a", "b" })
            {
                fixture.Put(new() { ["resourceType"] = "Patient", ["id"] = id, ["active"] = true });
            }

            FhirResponseContext Write(string criteria, string controls, HttpStatusCode expected)
            {
                string query = string.Join('&', new[] { system && criteria != "" ? "_type=Patient" : "", criteria, controls }
                    .Where(s => s != ""));
                FhirRequestContext ctx = new(fixture.Store, post ? "POST" : put ? "PUT" : "DELETE", system ? "" : "Patient")
                {
                    Interaction = route, UrlQuery = query, IfNoneExist = post ? query : "",
                    SourceFormat = "application/fhir+json", DestinationFormat = "application/fhir+json",
                    SourceContent = delete ? "" : """{"resourceType":"Patient","active":false}""",
                };
                FhirResponseContext response;
                bool success = post ? fixture.Store.InstanceCreate(ctx, out response)
                    : put ? fixture.Store.InstanceUpdate(ctx, out response)
                    : system ? fixture.Store.SystemDelete(ctx, out response)
                    : fixture.Store.TypeDelete(ctx, out response);
                Assert.Equal(expected, response.StatusCode);
                Assert.Equal((int)expected < 400, success);
                return response;
            }

            Dictionary<string, string> State() =>
                (fixture.Get("Patient")["entry"]?.AsArray() ?? []).Select(entry => entry!["resource"]!)
                    .ToDictionary(r => (string)r["id"]!, r => r.ToJsonString());

            Dictionary<string, string> expected = State();
            void AssertState() => Assert.Equal(expected.OrderBy(p => p.Key), State().OrderBy(p => p.Key));
            const string EmptyWindow = "_offset=9&_count=0&_maxresults=0&_total=none&_summary=count&_include=Patient:organization";

            Write("active=true", "_count=1&_total=none&_maxresults=1", HttpStatusCode.PreconditionFailed);
            AssertState();
            FhirResponseContext onlyControls = Write("", "_offset=1", post ? HttpStatusCode.Created :
                system ? HttpStatusCode.InternalServerError : HttpStatusCode.BadRequest);
            if (post)
            {
                expected.Add(onlyControls.Id, fixture.Get("Patient/" + onlyControls.Id).ToJsonString());
            }
            AssertState();
            FhirResponseContext missing = Write("_id=absent", EmptyWindow, delete ? HttpStatusCode.NotFound : HttpStatusCode.Created);
            if (!delete)
            {
                JsonObject added = fixture.Get("Patient/" + missing.Id);
                Assert.False((bool)added["active"]!);
                expected.Add(missing.Id, added.ToJsonString());
            }
            AssertState();
            FhirResponseContext sole = Write("_id=a", EmptyWindow, HttpStatusCode.OK);
            if (delete)
            {
                expected.Remove("a");
            }
            else
            {
                Assert.Equal("a", sole.Id);
                if (put)
                {
                    JsonObject changed = fixture.Get("Patient/a");
                    Assert.False((bool)changed["active"]!);
                    Assert.Equal("2", (string?)changed["meta"]!["versionId"]);
                    expected["a"] = changed.ToJsonString();
                }
            }
            AssertState();
        }
    }
}
