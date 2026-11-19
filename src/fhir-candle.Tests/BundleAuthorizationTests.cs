// <copyright file="BundleAuthorizationTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

extern alias candleR4;
extern alias candleR4B;
extern alias candleR5;

using System.Net;
using System.Text.Json.Nodes;
using FhirCandle.Models;
using FhirCandle.Storage;
using FhirCandle.Utils;
using Microsoft.Extensions.Primitives;

namespace fhir.candle.Tests;

/// <summary>Fresh-store bundle compatibility and authorization regressions across the three model assemblies.</summary>
public class BundleAuthorizationTests
{
    private const string JsonFormat = "application/fhir+json";
    private const string PatientFullUrl = "urn:uuid:00000000-0000-0000-0000-000000000001";
    private const string SecondPatientFullUrl = "urn:uuid:00000000-0000-0000-0000-000000000002";

    /// <summary>Each case owns and disposes its store; no shared fixture state is changed.</summary>
    public static IEnumerable<object[]> Versions =>
    [
        [FhirReleases.FhirSequenceCodes.R4],
        [FhirReleases.FhirSequenceCodes.R4B],
        [FhirReleases.FhirSequenceCodes.R5],
    ];

    /// <summary>Prepared IDs survive forced create, original-ID reads, URNs, and nested live-state reference resolution.</summary>
    [Theory]
    [MemberData(nameof(Versions))]
    public void AuthorizedTransactionIdentityAndReferencesRemainStable(FhirReleases.FhirSequenceCodes version)
    {
        using IFhirStore store = CreateStore(version);
        SeedPatient(store, "remove");
        AuthorizationInfo authorization = Authorize(store, "Patient.c", "Patient.r", "Patient.u", "Patient.d", "Observation.c", "Observation.r");
        JsonObject nested = Bundle("transaction",
            Entry("nested-observation", "POST", "Observation", Observation("nested-client", "Patient?name=Stable")));
        JsonObject request = Bundle("transaction",
            Entry("read", "GET", "Patient/client"),
            Entry("urn-observation", "POST", "Observation", Observation("observation-client", PatientFullUrl)),
            Entry("update", "PUT", "Patient/upsert", Patient("upsert")),
            Entry("patient", "POST", "Patient", Patient("client", "Stable"), fullUrl: PatientFullUrl),
            Entry("delete", "DELETE", "Patient/remove"),
            Entry("head", "HEAD", "Patient/client"),
            Entry("nested", "POST", "/", nested));

        JsonObject response = SendBundle(store, request, authorization);
        AssertOrder(response, "delete", "urn-observation", PatientFullUrl, "nested", "update", "read", "head");
        JsonObject created = ResponseEntry(response, 2);
        string patientId = ResourceId(created);
        Assert.True(Guid.TryParse(patientId, out _));
        Assert.NotEqual("client", patientId);
        AssertSuccessResource(created, 201, patientId, "1");
        AssertSuccessResource(ResponseEntry(response, 5), 200, patientId, "1");
        AssertSuccessResource(ResponseEntry(response, 6), 200, patientId, "1");
        Assert.Equal($"Patient/{patientId}", Stored(store, "Observation", ResourceId(ResponseEntry(response, 1)))["subject"]!["reference"]!.GetValue<string>());
        JsonObject nestedResponse = Assert.IsType<JsonObject>(ResponseEntry(response, 3)["resource"]);
        JsonObject nestedCreated = ResponseEntry(nestedResponse, 0);
        AssertSuccessResource(nestedCreated, 201, ResourceId(nestedCreated), "1");
        Assert.Equal($"Patient/{patientId}", Stored(store, "Observation", ResourceId(nestedCreated))["subject"]!["reference"]!.GetValue<string>());
        Assert.Equal(new[] { patientId, "upsert" }.Order(), store["Patient"].Keys.Order());
        Assert.Equal("Stable", Stored(store, "Patient", patientId)["name"]![0]!["family"]!.GetValue<string>());
        Assert.Equal(2, store["Observation"].Count);
    }

    /// <summary>All conditional fields, forwarded URLs, and shared search headers retain their existing effects.</summary>
    [Theory]
    [MemberData(nameof(Versions))]
    public void AuthorizedConditionalAndHeaderSemanticsRemainStable(FhirReleases.FhirSequenceCodes version)
    {
        foreach (string kind in new[] { "batch", "transaction" })
        {
            foreach (bool optionalNull in new[] { false, true })
            {
                using IFhirStore store = CreateStore(version);
                SeedPatient(store, "conditional", "Original");
                AuthorizationInfo? authorization = optionalNull ? null : Authorize(store, "*.*");
                FhirRequestContext.ForwardedInfo forwarded = new()
                {
                    Protocol = "https",
                    Host = "proxy.example.test",
                    Prefix = "/forwarded/fhir",
                };
                AssertChildContextFidelity(store, authorization, forwarded);
                JsonObject byQuery = SendBundle(store, Bundle(kind,
                    Entry("query", "POST", "Patient?_id=conditional", Patient("ignored", "Changed"))), authorization, forwarded: forwarded);
                AssertSuccessResource(ResponseEntry(byQuery, 0), 200, "conditional", "1");
                Assert.Equal("https://proxy.example.test/forwarded/fhir/Patient/conditional", ResponseEntry(byQuery, 0)["response"]!["location"]!.GetValue<string>());

                JsonObject ifNoneExist = Entry("if-none-exist", "POST", "Patient", Patient(null, "Changed"));
                ifNoneExist["request"]!["ifNoneExist"] = "_id=conditional";
                AssertSuccessResource(ResponseEntry(SendBundle(store, Bundle(kind, ifNoneExist), authorization), 0), 200, "conditional", "1");
                Assert.Single(store["Patient"]);
                Assert.Equal("Original", Stored(store, "Patient", "conditional")["name"]![0]!["family"]!.GetValue<string>());

                JsonObject matchingPut = Entry("match", "PUT", "Patient?_id=conditional", Patient("conditional", "Updated"));
                matchingPut["request"]!["ifMatch"] = "W/\"1\"";
                JsonObject matched = ResponseEntry(SendBundle(store, Bundle(kind, matchingPut), authorization), 0);
                AssertSuccessResource(matched, 200, "conditional", "2");
                JsonObject noMatch = ResponseEntry(SendBundle(store, Bundle(kind,
                    Entry("no-match", "PUT", "Patient?_id=new-conditional", Patient("new-conditional"))), authorization), 0);
                AssertSuccessResource(noMatch, 201, "new-conditional", "1");

                JsonObject stale = Entry("stale", "PUT", "Patient/conditional", Patient("conditional", "MustNotPersist"));
                stale["request"]!["ifMatch"] = "W/\"1\"";
                // Failed updates retain the executor's existing 500 envelope.
                AssertStatus(ResponseEntry(SendBundle(store, Bundle(kind, stale), authorization), 0), 500);
                Assert.Equal("Updated", Stored(store, "Patient", "conditional")["name"]![0]!["family"]!.GetValue<string>());
                Assert.Equal("2", Stored(store, "Patient", "conditional")["meta"]!["versionId"]!.GetValue<string>());

                JsonObject cache = Entry("cache", "GET", "Patient/conditional");
                cache["request"]!["ifNoneMatch"] = "W/\"2\"";
                JsonObject cached = ResponseEntry(SendBundle(store, Bundle(kind, cache), authorization), 0);
                AssertStatus(cached, 304);
                Assert.Null(cached["resource"]);
                JsonObject exists = Entry("exists", "GET", "Patient/conditional");
                exists["request"]!["ifNoneMatch"] = "*";
                AssertStatus(ResponseEntry(SendBundle(store, Bundle(kind, exists), authorization), 0), 412);

                JsonObject modified = Entry("modified", "GET", "Patient/conditional");
                modified["request"]!["ifModifiedSince"] = "2099-01-01T00:00:00+00:00";
                // The legacy child converts this to FHIR dateTime, not an HTTP date.
                // Its comparison with the HTTP last-modified string currently returns the resource.
                AssertSuccessResource(ResponseEntry(SendBundle(store, Bundle(kind, modified), authorization), 0), 200, "conditional", "2");

                Dictionary<string, StringValues> headers = new() { ["Prefer"] = "handling=strict" };
                JsonObject strict = SendBundle(store, Bundle(kind,
                    Entry("strict", "GET", "Patient?not-a-search-parameter=value")), authorization, headers: headers);
                AssertStatus(ResponseEntry(strict, 0), 400);
                JsonObject lenient = SendBundle(store, Bundle(kind,
                    Entry("lenient", "GET", "Patient?not-a-search-parameter=value")), authorization);
                AssertStatus(ResponseEntry(lenient, 0), 200);
                Assert.Equal(2, store["Patient"].Count);
            }
        }
    }

    /// <summary>Malformed responses, omitted methods, and supported transaction groups retain their exact positions.</summary>
    [Theory]
    [MemberData(nameof(Versions))]
    public void BundleResponseOrderingAndMalformedHandlingRemainStable(FhirReleases.FhirSequenceCodes version)
    {
        foreach (string kind in new[] { "batch", "transaction" })
        {
            using IFhirStore store = CreateStore(version);
            SeedPatient(store, "remove");
            JsonObject missing = new() { ["fullUrl"] = "missing" };
            JsonObject request = Bundle(kind,
                Entry("read", "GET", "Patient/upsert"),
                missing,
                Entry("null-method", "GET", "Patient"),
                Entry("post", "POST", "Patient", Patient()),
                Entry("unparsed", "GET", "NotAResource/id"),
                Entry("put", "PUT", "Patient/upsert", Patient("upsert")),
                Entry("unsupported", "GET", "Patient/upsert/_history"),
                Entry("delete", "DELETE", "Patient/remove"),
                Entry("patch", "PATCH", "Patient/upsert"),
                Entry("head", "HEAD", "Patient/upsert"));
            object model = ToModel(version, request);
            Assert.IsType<Hl7.Fhir.Model.Bundle.RequestComponent>(Assert.IsType<Hl7.Fhir.Model.Bundle>(model).Entry[2].Request).Method = null;
            JsonObject response = SendObjectBundle(store, model, Authorize(store, "*.*"));
            if (kind == "transaction")
            {
                AssertOrder(response, "delete", "post", "put", "patch", "read", "unparsed", "unsupported", "head", "missing");
                Assert.Equal(new[] { 200, 201, 201, 501, 200, 500, 501, 200, 400 }, Statuses(response));
            }
            else
            {
                AssertOrder(response, "read", "missing", "null-method", "post", "unparsed", "put", "unsupported", "delete", "patch", "head");
                Assert.Equal(new[] { 404, 400, 500, 201, 500, 201, 501, 200, 501, 200 }, Statuses(response));
            }

            JsonObject missingResponse = Entries(response).OfType<JsonObject>().Single(e => e["fullUrl"]!.GetValue<string>() == "missing");
            JsonNode missingIssue = missingResponse["response"]!["outcome"]!["issue"]![0]!;
            Assert.Equal("required", missingIssue["code"]!.GetValue<string>());
            Assert.Equal("Entry is missing a request", missingIssue["diagnostics"]!.GetValue<string>());
            JsonObject unparsed = Entries(response).OfType<JsonObject>().Single(e => e["fullUrl"]!.GetValue<string>() == "unparsed");
            Assert.Equal("not-supported", unparsed["response"]!["outcome"]!["issue"]![0]!["code"]!.GetValue<string>());
            Assert.Equal("Request could not be parsed to known interaction: GET NotAResource/id",
                unparsed["response"]!["outcome"]!["issue"]![0]!["diagnostics"]!.GetValue<string>());

            FhirRequestContext unsupported = Context(store, "POST", string.Empty, Authorize(store, "*.*")) with
            {
                SourceContent = Bundle("collection").ToJsonString(),
                SourceFormat = JsonFormat,
            };
            Assert.False(store.ProcessBundle(unsupported, out FhirResponseContext unsupportedResponse));
            Assert.Equal(HttpStatusCode.UnprocessableEntity, unsupportedResponse.StatusCode);
            Assert.Null(unsupportedResponse.Resource);
            JsonObject outcome = ToJson(store, unsupportedResponse.Outcome!);
            Assert.Equal("not-supported", outcome["issue"]![0]!["code"]!.GetValue<string>());
            Assert.Equal(2, store["Patient"].Count);

            // An unknown literal is retained by deserialization, but its typed Method getter
            // throws in the legacy executor. Do not silently add support or a new response.
            object unknownMethod = ToModel(version, Bundle(kind, Entry("options", "OPTIONS", "Patient")));
            Assert.Throws<Hl7.Fhir.Validation.CodedValidationException>(() =>
                SendObjectBundle(store, unknownMethod, Authorize(store, "*.*")));
        }
    }

    /// <summary>Target/state oracles pin first selection, null full URLs, literal rewriting, and body-independent parsing.</summary>
    [Theory]
    [MemberData(nameof(Versions))]
    public void ProjectionMatchesExecutedRequests(FhirReleases.FhirSequenceCodes version)
    {
        foreach (string identityCase in new[] { "duplicate-full-url", "missing-full-url", "repeated-original-id" })
        {
            using IFhirStore store = CreateStore(version);
            bool repeatedOriginal = identityCase == "repeated-original-id";
            string? firstUrl = identityCase == "missing-full-url" ? null : PatientFullUrl;
            string? secondUrl = repeatedOriginal ? SecondPatientFullUrl : firstUrl;
            JsonObject first = Entry("first", "POST", "Patient", Patient("first", "First"));
            JsonObject second = Entry("second", "POST", "Patient", Patient(repeatedOriginal ? "first" : "second", "Second"));
            first["fullUrl"] = firstUrl;
            second["fullUrl"] = secondUrl;
            if (firstUrl is null)
            {
                first.Remove("fullUrl");
                second.Remove("fullUrl");
            }
            JsonObject request = Bundle("transaction",
                Entry("read-first", "GET", "Patient/first?_tag=first"),
                first,
                second,
                Entry("reference", "POST", "Observation", Observation("observation", "first")),
                Entry("read-second", "GET", repeatedOriginal ? "Patient?name=Second" : "Patient/second"));
            object model = ToModel(version, request);
            JsonObject response = SendObjectBundle(store, model, Authorize(store, "Patient.c", "Patient.r", "Patient.s", "Observation.c"));
            JsonObject createdFirst = ResponseEntry(response, 0);
            string firstId = ResourceId(createdFirst);
            AssertSuccessResource(createdFirst, 201, firstId, "1");
            AssertSuccessResource(ResponseEntry(response, 3), 200, firstId, "1");
            Assert.Equal($"Patient/{firstId}", Stored(store, "Observation", ResourceId(ResponseEntry(response, 2)))["subject"]!["reference"]!.GetValue<string>());
            Assert.Equal($"Patient/{firstId}?_tag={firstId}", ToJson(store, model)["entry"]![0]!["request"]!["url"]!.GetValue<string>());
            Assert.Equal("First", Stored(store, "Patient", firstId)["name"]![0]!["family"]!.GetValue<string>());

            if (repeatedOriginal)
            {
                string secondId = ResourceId(ResponseEntry(response, 1));
                AssertSuccessResource(ResponseEntry(response, 1), 201, secondId, "1");
                Assert.NotEqual(firstId, secondId);
                Assert.Equal(2, store["Patient"].Count);
                JsonObject searched = Assert.IsType<JsonObject>(ResponseEntry(response, 4)["resource"]);
                Assert.Equal(secondId, ResourceId(ResponseEntry(searched, 0)));
                Assert.Equal("Patient?name=Second", ToJson(store, model)["entry"]![4]!["request"]!["url"]!.GetValue<string>());
            }
            else
            {
                AssertStatus(ResponseEntry(response, 1), 409);
                AssertStatus(ResponseEntry(response, 4), 404);
                Assert.Single(store["Patient"]);
                Assert.Equal(firstId, ToJson(store, model)["entry"]![2]!["resource"]!["id"]!.GetValue<string>());
                Assert.DoesNotContain("second", store["Patient"].Keys);
            }
        }

        using IFhirStore mismatchStore = CreateStore(version);
        JsonObject mismatch = SendBundle(mismatchStore, Bundle("transaction",
            Entry("mismatch", "POST", "Observation", Patient("not-an-observation"))), Authorize(mismatchStore, "Observation.c"));
        AssertStatus(ResponseEntry(mismatch, 0), 422);
        Assert.Empty(mismatchStore["Patient"]);
        Assert.Empty(mismatchStore["Observation"]);
    }

    /// <summary>Parent and earlier-sibling preparation must not freeze a later child's original-ID binding.</summary>
    [Theory]
    [MemberData(nameof(Versions))]
    public void AuthorizedAliasedNestedTransactionKeepsExecutionTimeOriginalIds(FhirReleases.FhirSequenceCodes version)
    {
        foreach (bool parentPostsShared in new[] { true, false })
        {
            using IFhirStore store = CreateStore(version);
            SeedPatient(store, "client", "SeededTarget");
            object seeded = store["Patient"]["client"];
            object shared = ToModel(version, Patient("client", "SharedInput"));
            JsonObject child = Bundle("transaction",
                Entry("child-post", "POST", "Patient", Patient("placeholder")),
                Entry("child-delete", "DELETE", "Patient/client"));
            JsonObject first = parentPostsShared
                ? Entry("parent-post", "POST", "Patient", Patient("placeholder"))
                : Entry("first-child", "POST", "/", Bundle("transaction",
                    Entry("first-child-post", "POST", "Patient", Patient("placeholder"))));
            object model = ToModel(version, Bundle("transaction", first, Entry("later-child", "POST", "/", child)));
            if (parentPostsShared)
            {
                SetEntryResource(model, 0, shared);
            }
            else
            {
                SetEntryResource(EntryResource(model, 0), 0, shared);
            }

            SetEntryResource(EntryResource(model, 1), 0, shared);
            List<string> events = [];
            store.OnInstanceCreated += (_, e) => events.Add($"create:{e.ResourceType}/{e.ResourceId}");
            store.OnInstanceDeleted += (_, e) => events.Add($"delete:{e.ResourceType}/{e.ResourceId}");
            JsonObject response = SendObjectBundle(store, model, Authorize(store, "Patient.c", "Patient.d"));
            JsonObject firstCreated = parentPostsShared
                ? ResponseEntry(response, 0)
                : ResponseEntry(Assert.IsType<JsonObject>(ResponseEntry(response, 0)["resource"]), 0);
            JsonObject laterResponse = Assert.IsType<JsonObject>(ResponseEntry(response, 1)["resource"]);
            AssertOrder(laterResponse, "child-delete", "child-post");
            JsonObject deleted = ResponseEntry(laterResponse, 0);
            JsonObject laterCreated = ResponseEntry(laterResponse, 1);
            AssertStatus(deleted, 200);
            Assert.Equal("client", ResourceId(deleted));
            Assert.Equal("SeededTarget", deleted["resource"]!["name"]![0]!["family"]!.GetValue<string>());
            Assert.Equal("client", ToJson(store, seeded)["id"]!.GetValue<string>());
            string firstId = firstCreated["response"]!["location"]!.GetValue<string>().Split('/')[^1];
            string laterId = ResourceId(laterCreated);
            Assert.True(Guid.TryParse(firstId, out _));
            Assert.True(Guid.TryParse(laterId, out _));
            Assert.NotEqual(firstId, laterId);
            AssertSuccessResource(firstCreated, 201, laterId, "1", locationId: firstId);
            AssertSuccessResource(laterCreated, 201, laterId, "1");
            Assert.Equal(new[] { firstId, laterId }.Order(), store["Patient"].Keys.Order());
            Assert.Same(shared, store["Patient"][firstId]);
            Assert.Same(shared, store["Patient"][laterId]);
            Assert.Equal(laterId, Stored(store, "Patient", firstId)["id"]!.GetValue<string>());
            Assert.Equal(laterId, Stored(store, "Patient", laterId)["id"]!.GetValue<string>());
            Assert.Equal(new[] { $"create:Patient/{firstId}", "delete:Patient/client", $"create:Patient/{laterId}" }, events);
            Assert.DoesNotContain("client", store["Patient"].Keys);
        }
    }

    private static IFhirStore CreateStore(FhirReleases.FhirSequenceCodes version)
    {
        IFhirStore store = version switch
        {
            FhirReleases.FhirSequenceCodes.R4 => new candleR4::FhirCandle.Storage.VersionedFhirStore(),
            FhirReleases.FhirSequenceCodes.R4B => new candleR4B::FhirCandle.Storage.VersionedFhirStore(),
            FhirReleases.FhirSequenceCodes.R5 => new candleR5::FhirCandle.Storage.VersionedFhirStore(),
            _ => throw new ArgumentOutOfRangeException(nameof(version)),
        };
        store.Init(new()
        {
            FhirVersion = version,
            ControllerName = $"bundle-{version.ToString().ToLowerInvariant()}",
            BaseUrl = $"http://localhost/fhir/bundle-{version.ToString().ToLowerInvariant()}",
            AllowExistingId = false,
            AllowCreateAsUpdate = true,
            SmartAllowed = true,
            SupportNotChanged = true,
        });
        return store;
    }

    private static AuthorizationInfo Authorize(IFhirStore store, params string[] scopes) => new()
    {
        Tenant = store.Config.ControllerName,
        RemoteIpAddress = "127.0.0.1",
        RequestParameters = new() { Scope = string.Join(' ', scopes.Select(s => $"user/{s}")) },
        UserScopes = new(scopes),
    };

    private static FhirRequestContext Context(IFhirStore store, string method, string url, AuthorizationInfo? authorization) => new()
    {
        TenantName = store.Config.ControllerName,
        Store = store,
        Authorization = authorization,
        HttpMethod = method,
        Url = url,
    };

    private static void SeedPatient(IFhirStore store, string id, string family = "Seed")
    {
        FhirRequestContext ctx = Context(store, "PUT", $"Patient/{id}", Authorize(store, "*.*")) with
        {
            SourceContent = Patient(id, family).ToJsonString(),
            SourceFormat = JsonFormat,
        };
        Assert.True(store.InstanceUpdate(ctx, out FhirResponseContext response), response.SerializedOutcome);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static void AssertChildContextFidelity(
        IFhirStore store,
        AuthorizationInfo? authorization,
        FhirRequestContext.ForwardedInfo forwarded)
    {
        JsonObject entry = Entry("context", "GET", "Patient/original", Observation("body", "Patient/conditional"));
        entry["request"]!["ifMatch"] = "W/\"2\"";
        entry["request"]!["ifNoneMatch"] = "W/\"1\"";
        entry["request"]!["ifModifiedSince"] = "2099-01-01T00:00:00+00:00";
        entry["request"]!["ifNoneExist"] = "_id=conditional";
        Hl7.Fhir.Model.Bundle bundle = Assert.IsType<Hl7.Fhir.Model.Bundle>(ToModel(store.Config.FhirVersion, Bundle("batch", entry)));
        FhirRequestContext parent = Context(store, "POST", string.Empty, authorization) with
        {
            RequestHeaders = new() { ["Prefer"] = "handling=strict" },
            Forwarded = forwarded,
            SourceObject = bundle,
            SourceContent = "<outer-only/>",
            SourceFormat = "application/fhir+xml",
            DestinationFormat = "application/fhir+xml",
            SerializePretty = true,
            SerializeSummaryFlag = "count",
        };
        System.Reflection.MethodInfo factory = Assert.IsAssignableFrom<System.Reflection.MethodInfo>(
            store.GetType().GetMethod("CreateBundleEntryContext", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static));
        FhirRequestContext child = Assert.IsType<FhirRequestContext>(factory.Invoke(null, [parent, bundle.Entry[0], "Patient/projected"]));
        Assert.Equal(parent.TenantName, child.TenantName);
        Assert.Same(store, child.Store);
        Assert.Same(authorization, child.Authorization);
        Assert.Same(parent.RequestHeaders, child.RequestHeaders);
        Assert.Same(forwarded, child.Forwarded);
        Assert.Equal("GET", child.HttpMethod);
        Assert.Equal("Patient/projected", child.Url);
        Assert.Equal(Common.StoreInteractionCodes.InstanceRead, child.Interaction);
        Assert.Equal("Patient", child.ResourceType);
        Assert.Equal("projected", child.Id);
        Assert.Same(bundle.Entry[0].Resource, child.SourceObject);
        Assert.Equal("W/\"2\"", child.IfMatch);
        Assert.Equal("W/\"1\"", child.IfNoneMatch);
        Assert.StartsWith("2099-01-01T00:00:00", child.IfModifiedSince);
        Assert.Equal(new DateTimeOffset(2099, 1, 1, 0, 0, 0, TimeSpan.Zero), DateTimeOffset.Parse(child.IfModifiedSince));
        Assert.Equal("_id=conditional", child.IfNoneExist);
        Assert.Equal(string.Empty, child.SourceContent);
        Assert.Equal(string.Empty, child.SourceFormat);
        Assert.Equal(JsonFormat, child.DestinationFormat);
        Assert.False(child.SerializePretty);
        Assert.Equal(string.Empty, child.SerializeSummaryFlag);
    }

    private static JsonObject SendBundle(
        IFhirStore store,
        JsonObject bundle,
        AuthorizationInfo? authorization,
        FhirRequestContext.ForwardedInfo? forwarded = null,
        Dictionary<string, StringValues>? headers = null)
    {
        FhirRequestContext ctx = Context(store, "POST", string.Empty, authorization) with
        {
            SourceContent = bundle.ToJsonString(),
            SourceFormat = JsonFormat,
            Forwarded = forwarded,
            RequestHeaders = headers ?? [],
        };
        Assert.True(store.ProcessBundle(ctx, out FhirResponseContext response), response.SerializedOutcome);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal($"{bundle["type"]!.GetValue<string>()}-response", ToJson(store, response.Resource!)["type"]!.GetValue<string>());
        Assert.Equal(JsonFormat, response.MimeType);
        Assert.False(string.IsNullOrEmpty(response.SerializedResource));
        return JsonNode.Parse(response.SerializedResource)!.AsObject();
    }

    private static JsonObject SendObjectBundle(IFhirStore store, object bundle, AuthorizationInfo authorization)
    {
        FhirRequestContext ctx = Context(store, "POST", string.Empty, authorization) with { SourceObject = bundle };
        Assert.True(store.PerformInteraction(ctx, out FhirResponseContext response, serializeReturn: false));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return ToJson(store, response.Resource!);
    }

    private static JsonObject Patient(string? id = null, string family = "Patient")
    {
        JsonObject patient = new()
        {
            ["resourceType"] = "Patient",
            ["active"] = true,
            ["name"] = new JsonArray(new JsonObject { ["family"] = family }),
        };
        if (id is not null)
        {
            patient["id"] = id;
        }

        return patient;
    }

    private static JsonObject Observation(string id, string reference) => new()
    {
        ["resourceType"] = "Observation",
        ["id"] = id,
        ["status"] = "final",
        ["code"] = new JsonObject { ["text"] = "Bundle compatibility" },
        ["subject"] = new JsonObject { ["reference"] = reference },
    };

    private static JsonObject Entry(string label, string? method, string url, JsonObject? resource = null, string? fullUrl = null)
    {
        JsonObject request = new() { ["url"] = url };
        if (method is not null)
        {
            request["method"] = method;
        }

        JsonObject entry = new() { ["fullUrl"] = fullUrl ?? label, ["request"] = request };
        if (resource is not null)
        {
            entry["resource"] = resource;
        }

        return entry;
    }

    private static JsonObject Bundle(string type, params JsonObject[] entries)
    {
        JsonObject bundle = new()
        {
            ["resourceType"] = "Bundle",
            ["type"] = type,
            // The legacy transaction lookup reads a Bundle's single Identifier when POSTed.
            ["identifier"] = new JsonObject { ["system"] = "urn:bundle-compatibility", ["value"] = "bundle" },
        };
        if (entries.Length != 0)
        {
            bundle["entry"] = new JsonArray(entries.Cast<JsonNode?>().ToArray());
        }

        return bundle;
    }

    private static JsonArray Entries(JsonObject bundle) => bundle["entry"]!.AsArray();

    private static JsonObject ResponseEntry(JsonObject bundle, int index) => Entries(bundle)[index]!.AsObject();

    private static string ResourceId(JsonObject entry) => entry["resource"]!["id"]!.GetValue<string>();

    private static int[] Statuses(JsonObject bundle) => Entries(bundle)
        .Select(e => int.Parse(e!["response"]!["status"]!.GetValue<string>().Split(' ')[0])).ToArray();

    private static void AssertStatus(JsonObject entry, int expected)
    {
        string name = ((HttpStatusCode)expected).ToString();
        Assert.Equal($"{expected} {name}", entry["response"]!["status"]!.GetValue<string>());
    }

    private static void AssertOrder(JsonObject response, params string[] fullUrls) =>
        Assert.Equal(fullUrls, Entries(response).Select(e => e!["fullUrl"]!.GetValue<string>()));

    private static void AssertSuccessResource(JsonObject entry, int status, string id, string version, string? locationId = null)
    {
        AssertStatus(entry, status);
        Assert.Equal(id, ResourceId(entry));
        Assert.Equal(version, entry["resource"]!["meta"]!["versionId"]!.GetValue<string>());
        Assert.Equal($"W/\"{version}\"", entry["response"]!["etag"]!.GetValue<string>());
        if (locationId is null)
        {
            Assert.Equal(entry["resource"]!["meta"]!["lastUpdated"]!.GetValue<string>(), entry["response"]!["lastModified"]!.GetValue<string>());
        }
        else
        {
            // Aliased resources retain the later create's metadata, but the earlier
            // response component retains the earlier create's timestamp and location.
            Assert.True(DateTimeOffset.Parse(entry["response"]!["lastModified"]!.GetValue<string>()) <=
                DateTimeOffset.Parse(entry["resource"]!["meta"]!["lastUpdated"]!.GetValue<string>()));
        }
        Assert.EndsWith($"/{entry["resource"]!["resourceType"]!.GetValue<string>()}/{locationId ?? id}", entry["response"]!["location"]!.GetValue<string>());
    }

    private static JsonObject Stored(IFhirStore store, string type, string id) => ToJson(store, store[type][id]);

    private static object ToModel(FhirReleases.FhirSequenceCodes version, JsonObject json)
    {
        string content = json.ToJsonString();
        switch (version)
        {
            case FhirReleases.FhirSequenceCodes.R4:
                Assert.Equal(HttpStatusCode.OK, candleR4::FhirCandle.Serialization.SerializationUtils.TryDeserializeFhir(
                    content, JsonFormat, out Hl7.Fhir.Model.Resource? r4, out _));
                return Assert.IsAssignableFrom<Hl7.Fhir.Model.Resource>(r4);
            case FhirReleases.FhirSequenceCodes.R4B:
                Assert.Equal(HttpStatusCode.OK, candleR4B::FhirCandle.Serialization.SerializationUtils.TryDeserializeFhir(
                    content, JsonFormat, out Hl7.Fhir.Model.Resource? r4b, out _));
                return Assert.IsAssignableFrom<Hl7.Fhir.Model.Resource>(r4b);
            case FhirReleases.FhirSequenceCodes.R5:
                Assert.Equal(HttpStatusCode.OK, candleR5::FhirCandle.Serialization.SerializationUtils.TryDeserializeFhir(
                    content, JsonFormat, out Hl7.Fhir.Model.Resource? r5, out _));
                return Assert.IsAssignableFrom<Hl7.Fhir.Model.Resource>(r5);
            default:
                throw new ArgumentOutOfRangeException(nameof(version));
        }
    }

    private static JsonObject ToJson(IFhirStore store, object resource)
    {
        Hl7.Fhir.Model.Resource model = Assert.IsAssignableFrom<Hl7.Fhir.Model.Resource>(resource);
        string json = store.Config.FhirVersion switch
        {
            FhirReleases.FhirSequenceCodes.R4 => candleR4::FhirCandle.Serialization.SerializationUtils.SerializeFhir(model, JsonFormat, false),
            FhirReleases.FhirSequenceCodes.R4B => candleR4B::FhirCandle.Serialization.SerializationUtils.SerializeFhir(model, JsonFormat, false),
            FhirReleases.FhirSequenceCodes.R5 => candleR5::FhirCandle.Serialization.SerializationUtils.SerializeFhir(model, JsonFormat, false),
            _ => throw new ArgumentOutOfRangeException(nameof(store)),
        };
        return JsonNode.Parse(json)!.AsObject();
    }

    private static object EntryResource(object bundle, int index) =>
        Assert.IsAssignableFrom<Hl7.Fhir.Model.Resource>(Assert.IsType<Hl7.Fhir.Model.Bundle>(bundle).Entry[index].Resource);

    private static void SetEntryResource(object bundle, int index, object resource)
    {
        Assert.IsType<Hl7.Fhir.Model.Bundle>(bundle).Entry[index].Resource = Assert.IsAssignableFrom<Hl7.Fhir.Model.Resource>(resource);
    }
}
