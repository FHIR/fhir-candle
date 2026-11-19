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
            Assert.Equal($"Patient/{firstId}?_tag=first", ToJson(store, model)["entry"]![0]!["request"]!["url"]!.GetValue<string>());
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

    /// <summary>Denied mutations preserve complete privileged state through both bundle entry points.</summary>
    [Theory]
    [MemberData(nameof(Versions))]
    public void EntryPermissionsAreEnforced(FhirReleases.FhirSequenceCodes version)
    {
        foreach (string kind in new[] { "batch", "transaction" })
        {
            foreach (string method in new[] { "DELETE", "POST", "PUT" })
            {
                foreach (bool objectInput in new[] { false, true })
                {
                    using IFhirStore store = CreateStore(version);
                    SeedPatient(store, "seed", "Original");
                    MutationWatch watch = new(store);
                    string[] before = Fingerprint(store);
                    JsonObject entry = Entry("denied", method, method == "POST" ? "Patient" : "Patient/seed",
                        method == "DELETE" ? null : Patient(method == "POST" ? "client" : "seed", "Changed"));
                    JsonObject request = Bundle(kind, entry);
                    AuthorizationInfo authorization = Authorize(store, "Patient.r");
                    JsonObject response = objectInput
                        ? SendObjectBundle(store, ToModel(version, request), authorization)
                        : SendBundle(store, request, authorization);
                    AssertOwnDenial(ResponseEntry(response, 0), method,
                        method == "POST" ? "Patient" : "Patient/seed");
                    AssertUnchanged(store, before, watch);
                }
            }
        }

        // Approvals/refusals belong to a request, never to the reused payload or store.
        using IFhirStore reusedStore = CreateStore(version);
        object reused = ToModel(version, Bundle("transaction", Entry("reuse", "POST", "Patient", Patient("client"))));
        AssertOwnDenial(ResponseEntry(SendObjectBundle(reusedStore, reused, Authorize(reusedStore, "Patient.r")), 0), "POST", "Patient");
        JsonObject permitted = ResponseEntry(SendObjectBundle(reusedStore, reused, Authorize(reusedStore, "Patient.c")), 0);
        AssertSuccessResource(permitted, 201, ResourceId(permitted), "1");
        Assert.Single(reusedStore["Patient"]);
    }

    /// <summary>Authorization follows the parsed method, interaction, and URL type, never the body or outer bundle.</summary>
    [Theory]
    [MemberData(nameof(Versions))]
    public void EntryPermissionsUseResourceAndInteraction(FhirReleases.FhirSequenceCodes version)
    {
        (string Scope, string Method, string Url)[] denied =
        [
            ("Patient.r", "GET", "Observation/seed"),
            ("Patient.r", "HEAD", "Observation/seed"),
            ("Patient.s", "GET", "Observation?status=final"),
            ("Patient.c", "GET", "Patient/seed"),
            ("Patient.c", "HEAD", "Patient/seed"),
            ("Patient.u", "GET", "Patient/seed"),
            ("Patient.u", "HEAD", "Patient/seed"),
            ("Patient.c", "GET", "Patient?name=Seed"),
            ("Patient.u", "GET", "Patient?name=Seed"),
            ("Patient.r", "GET", "Patient?name=Seed"),
            ("Patient.c", "GET", "Patient/$not-installed"),
            ("Patient.u", "GET", "Patient/seed/$not-installed"),
            ("Patient.r", "POST", "Patient/$not-installed"),
            ("Patient.c", "POST", "Patient/seed/$not-installed"),
            ("Patient.r", "POST", "$not-installed"),
            // Conditional update is deliberately not mapped by the restricted policy.
            ("Patient.u", "PUT", "Patient?_id=seed"),
            ("Patient.c", "POST", "Observation"),
        ];
        foreach (string kind in new[] { "batch", "transaction" })
        {
            using IFhirStore store = CreateStore(version);
            SeedPatient(store, "seed");
            SeedResource(store, "Observation", "seed", Observation("seed", "Patient/seed"));
            string[] before = Fingerprint(store);
            MutationWatch watch = new(store);
            foreach ((string scope, string method, string url) in denied)
            {
                JsonObject response = SendBundle(store, Bundle(kind,
                    Entry("denied", method, url, method is "POST" or "PUT" ? Patient("seed") : null)),
                    Authorize(store, scope));
                AssertOwnDenial(ResponseEntry(response, 0), method, url);
                AssertUnchanged(store, before, watch);
            }

            foreach ((string scope, string method, string url) in new[]
            {
                ("Patient.r", "GET", "Patient/seed"),
                ("Patient.r", "HEAD", "Patient/seed"),
                ("Patient.s", "GET", "Patient?name=Seed"),
                ("Patient.r", "GET", "Patient/$not-installed"),
                ("Patient.u", "POST", "Patient/seed/$not-installed"),
            })
            {
                JsonObject response = SendBundle(store, Bundle(kind, Entry("allowed", method, url)), Authorize(store, scope));
                Assert.NotEqual("401 Unauthorized", ResponseEntry(response, 0)["response"]!["status"]!.GetValue<string>());
                AssertUnchanged(store, before, watch);
            }
        }
    }

    /// <summary>Every denial is discovered before any group runs, irrespective of source order or malformed siblings.</summary>
    [Theory]
    [MemberData(nameof(Versions))]
    public void TransactionDenialPreventsEveryMutation(FhirReleases.FhirSequenceCodes version)
    {
        (string Allowed, string Denied, string Scope)[] cases =
        [
            ("DELETE", "POST", "Patient.d"),
            ("DELETE", "PUT", "Patient.d"),
            ("POST", "GET", "Patient.c"),
            ("POST", "HEAD", "Patient.c"),
            ("PUT", "GET", "Patient.u"),
            ("PUT", "HEAD", "Patient.u"),
        ];
        foreach ((string allowed, string denied, string scope) in cases)
        {
            foreach (bool reverse in new[] { false, true })
            {
                using IFhirStore store = CreateStore(version);
                SeedPatient(store, "seed", "Original");
                SeedResource(store, "Observation", "observation", Observation("observation", "Patient/seed"));
                string[] before = Fingerprint(store);
                MutationWatch watch = new(store);
                JsonObject permitted = Entry("permitted", allowed, allowed == "POST" ? "Patient" : "Patient/seed",
                    allowed == "DELETE" ? null : Patient(allowed == "POST" ? "client" : "seed", "MustNotPersist"));
                JsonObject refused = Entry("denied", denied, denied == "POST" ? "Patient" : "Patient/seed",
                    denied is "POST" or "PUT" ? Patient(null, "MustNotPersist") : null);
                JsonObject[] pair = reverse ? [refused, permitted] : [permitted, refused];
                JsonObject response = SendBundle(store, Bundle("transaction",
                    Entry("unparsed", "GET", "NotAResource/id"), pair[0], new() { ["fullUrl"] = "missing" }, pair[1]),
                    Authorize(store, scope));
                Assert.Equal(4, Entries(response).Count);
                JsonObject own = Entries(response).OfType<JsonObject>().Single(e => e["fullUrl"]!.GetValue<string>() == "denied");
                AssertOwnDenial(own, denied, denied == "POST" ? "Patient" : "Patient/seed");
                JsonObject collateral = Entries(response).OfType<JsonObject>().Single(e => e["fullUrl"]!.GetValue<string>() == "permitted");
                AssertCollateralDenial(collateral);
                Assert.Single(Issues(collateral));
                JsonObject malformed = Entries(response).OfType<JsonObject>().Single(e => e["fullUrl"]!.GetValue<string>() == "unparsed");
                AssertStatus(malformed, 500);
                Assert.Equal("not-supported", Issues(malformed)[0]!["code"]!.GetValue<string>());
                AssertStatus(ResponseEntry(response, Entries(response).Count - 1), 400);
                Assert.Equal("missing", ResponseEntry(response, Entries(response).Count - 1)["fullUrl"]!.GetValue<string>());
                AssertUnchanged(store, before, watch);
            }
        }

        // A permitted read is collateral too: no successful resources or read metadata are fabricated.
        using IFhirStore readStore = CreateStore(version);
        SeedPatient(readStore, "seed");
        MutationWatch readWatch = new(readStore);
        string[] readBefore = Fingerprint(readStore);
        JsonObject readResponse = SendBundle(readStore, Bundle("transaction",
            Entry("read", "GET", "Patient/seed"), Entry("denied", "DELETE", "Patient/seed")), Authorize(readStore, "Patient.r"));
        AssertCollateralDenial(ResponseEntry(readResponse, 1));
        AssertUnchanged(readStore, readBefore, readWatch);
    }

    /// <summary>Descendant denials refuse the enclosing transaction, but not independent outer batch units.</summary>
    [Theory]
    [MemberData(nameof(Versions))]
    public void NestedDenialBlocksEnclosingTransaction(FhirReleases.FhirSequenceCodes version)
    {
        foreach (string nestedKind in new[] { "batch", "transaction" })
        {
            foreach (string deniedMethod in new[] { "GET", "PUT" })
            {
                using IFhirStore store = CreateStore(version);
                SeedPatient(store, "seed");
                MutationWatch watch = new(store);
                string[] before = Fingerprint(store);
                JsonObject nested = Bundle(nestedKind,
                    Entry("nested-create", "POST", "Patient", Patient("child")),
                    Entry("denied", deniedMethod, "Patient/seed", deniedMethod == "PUT" ? Patient("seed", "Changed") : null));
                JsonObject response = SendBundle(store, Bundle("transaction",
                    Entry("parent-create", "POST", "Patient", Patient("parent")),
                    Entry("wrapper", "POST", "/", nested)), Authorize(store, "Patient.c"));
                AssertOrder(response, "parent-create", "wrapper");
                AssertCollateralDenial(ResponseEntry(response, 0));
                JsonObject wrapper = ResponseEntry(response, 1);
                AssertCollateralDenial(wrapper);
                Assert.Equal(2, Issues(wrapper).Count);
                Assert.Equal("forbidden", Issues(wrapper)[1]!["code"]!.GetValue<string>());
                Assert.Equal($"Denied descendant Bundle.entry[1].entry[1]: Unauthorized request: {deniedMethod} Patient/seed, parsed interaction: " +
                    (deniedMethod == "GET" ? "InstanceRead" : "InstanceUpdate"),
                    Issues(wrapper)[1]!["diagnostics"]!.GetValue<string>());
                AssertUnchanged(store, before, watch);
            }
        }

        using IFhirStore independent = CreateStore(version);
        SeedPatient(independent, "seed", "Original");
        MutationWatch independentWatch = new(independent);
        string original = Stored(independent, "Patient", "seed").ToJsonString();
        JsonObject batch = SendBundle(independent, Bundle("batch",
            Entry("before", "POST", "Patient", Patient(null, "Before")),
            Entry("wrapper", "POST", "/", Bundle("transaction",
                Entry("nested-create", "POST", "Patient", Patient("child")),
                Entry("denied", "GET", "Patient/seed"))),
            Entry("after", "POST", "Patient", Patient(null, "After"))), Authorize(independent, "Patient.c"));
        AssertOrder(batch, "before", "wrapper", "after");
        AssertStatus(ResponseEntry(batch, 0), 201);
        AssertStatus(ResponseEntry(batch, 1), 200);
        JsonObject rejectedChild = Assert.IsType<JsonObject>(ResponseEntry(batch, 1)["resource"]);
        AssertCollateralDenial(ResponseEntry(rejectedChild, 0));
        AssertOwnDenial(ResponseEntry(rejectedChild, 1), "GET", "Patient/seed");
        AssertStatus(ResponseEntry(batch, 2), 201);
        Assert.Equal(3, independent["Patient"].Count);
        Assert.Equal(original, Stored(independent, "Patient", "seed").ToJsonString());
        Assert.Equal(new[]
        {
            $"create:Patient/{ResourceId(ResponseEntry(batch, 0))}",
            $"create:Patient/{ResourceId(ResponseEntry(batch, 2))}",
        }, independentWatch.Events);

        // Walk beyond one executable level, retaining the original ordinal path.
        using IFhirStore deep = CreateStore(version);
        MutationWatch deepWatch = new(deep);
        string[] deepBefore = Fingerprint(deep);
        JsonObject deepResponse = SendBundle(deep, Bundle("transaction", Entry("outer", "POST", "/", Bundle("batch",
            Entry("middle", "POST", "/", Bundle("transaction", Entry("denied", "HEAD", "Observation/absent")))))),
            Authorize(deep, "Patient.r"));
        Assert.Contains("Bundle.entry[0].entry[0].entry[0]", Issues(ResponseEntry(deepResponse, 0))[1]!["diagnostics"]!.GetValue<string>());
        AssertUnchanged(deep, deepBefore, deepWatch);

        // A direct denial must not stop structural scanning before a later descendant denial.
        JsonObject multiple = SendBundle(deep, Bundle("transaction",
            Entry("own", "POST", "Observation", Observation("client", "Patient/absent")),
            Entry("wrapper", "POST", "/", Bundle("batch",
                Entry("unparsed", "GET", "NotAResource/id"),
                Entry("first-denied", "GET", "Observation/absent"),
                Entry("second-denied", "HEAD", "Observation/absent")))),
            Authorize(deep, "Patient.c"));
        AssertOwnDenial(ResponseEntry(multiple, 0), "POST", "Observation");
        JsonObject multipleWrapper = ResponseEntry(multiple, 1);
        AssertCollateralDenial(multipleWrapper);
        Assert.Equal(3, Issues(multipleWrapper).Count);
        Assert.Contains("Bundle.entry[1].entry[1]: Unauthorized request: GET Observation/absent",
            Issues(multipleWrapper)[1]!["diagnostics"]!.GetValue<string>());
        Assert.Contains("Bundle.entry[1].entry[2]: Unauthorized request: HEAD Observation/absent",
            Issues(multipleWrapper)[2]!["diagnostics"]!.GetValue<string>());
        AssertUnchanged(deep, deepBefore, deepWatch);

        // A batch create changes the shared ID during interaction execution, not
        // transaction preparation. The later URL must still be checked as a type search.
        using IFhirStore aliased = CreateStore(version);
        object shared = ToModel(version, Patient("Patient", "Aliased"));
        object aliasedRequest = ToModel(version, Bundle("transaction",
            Entry("earlier-batch", "POST", "/", Bundle("batch",
                Entry("batch-create", "POST", "Patient", Patient("placeholder")))),
            Entry("later-transaction", "POST", "/", Bundle("transaction",
                Entry("transaction-create", "POST", "Patient", Patient("placeholder")),
                Entry("denied-search", "GET", "Patient?name=Aliased")))));
        SetEntryResource(EntryResource(aliasedRequest, 0), 0, shared);
        SetEntryResource(EntryResource(aliasedRequest, 1), 0, shared);
        string aliasedInputBefore = ToJson(aliased, aliasedRequest).ToJsonString();
        string[] aliasedBefore = Fingerprint(aliased);
        MutationWatch aliasedWatch = new(aliased);
        JsonObject aliasedResponse = SendObjectBundle(aliased, aliasedRequest, Authorize(aliased, "Patient.c"));
        AssertUnchanged(aliased, aliasedBefore, aliasedWatch);
        Assert.Equal(aliasedInputBefore, ToJson(aliased, aliasedRequest).ToJsonString());
        AssertCollateralDenial(ResponseEntry(aliasedResponse, 0));
        AssertCollateralDenial(ResponseEntry(aliasedResponse, 1));
        Assert.Single(Issues(ResponseEntry(aliasedResponse, 0)));
        Assert.Equal(2, Issues(ResponseEntry(aliasedResponse, 1)).Count);
        Assert.Equal("Denied descendant Bundle.entry[1].entry[1]: Unauthorized request: GET Patient?name=Aliased, parsed interaction: TypeSearch",
            Issues(ResponseEntry(aliasedResponse, 1))[1]!["diagnostics"]!.GetValue<string>());
    }

    /// <summary>Interaction-time re-ID cannot change admission; shared occurrences retain their live execution effects.</summary>
    [Theory]
    [MemberData(nameof(Versions))]
    public void InteractionTimeAliasedIdsPreserveAdmission(FhirReleases.FhirSequenceCodes version)
    {
        using (IFhirStore store = CreateStore(version))
        {
            object shared = ToModel(version, Patient("Patient", "Aliased"));
            Hl7.Fhir.Model.Bundle request = AliasedNestedRequest(version, shared,
                Bundle("batch", Entry("batch-create", "POST", "Patient", Patient("placeholder"))),
                Bundle("transaction",
                    Entry("transaction-create", "POST", "Patient", Patient("placeholder")),
                    // Materialize one match: the two keys intentionally share the same body ID.
                    Entry("search", "GET", "Patient?name=Aliased&_count=1")));
            MutationWatch watch = new(store);
            JsonObject response = SendObjectBundle(store, request, Authorize(store, "Patient.c", "Patient.s"));
            JsonObject first = ResponseEntry(NestedResponse(response, 0), 0);
            JsonObject later = NestedResponse(response, 1);
            JsonObject second = ResponseEntry(later, 0);
            string firstId = LocationId(first);
            string secondId = LocationId(second);
            Assert.NotEqual(firstId, secondId);
            Assert.True(Guid.TryParse(firstId, out _));
            Assert.True(Guid.TryParse(secondId, out _));
            AssertSuccessResource(first, 201, secondId, "1", firstId);
            AssertSuccessResource(second, 201, secondId, "1");
            Assert.Equal(new[] { firstId, secondId }.Order(), store["Patient"].Keys.Order());
            Assert.Same(shared, store["Patient"][firstId]);
            Assert.Same(shared, store["Patient"][secondId]);
            Assert.Equal(secondId, ToJson(store, shared)["id"]!.GetValue<string>());
            Assert.Equal(new[] { $"create:Patient/{firstId}", $"create:Patient/{secondId}" }, watch.Events);
            AssertStatus(ResponseEntry(later, 1), 200);
            Assert.Equal(2, ResponseEntry(later, 1)["resource"]!["total"]!.GetValue<int>());
            JsonObject search = Assert.IsType<JsonObject>(ResponseEntry(later, 1)["resource"]);
            Assert.Single(Entries(search));
            Assert.Equal(secondId, ResourceId(ResponseEntry(search, 0)));
            Assert.Equal("Patient", ModelBundle(EntryResource(request, 0)).Entry[0].Request!.Url);
            Assert.Equal("Patient", ModelBundle(EntryResource(request, 1)).Entry[0].Request!.Url);
            Assert.Equal("Patient?name=Aliased&_count=1", ModelBundle(EntryResource(request, 1)).Entry[1].Request!.Url);
        }

        foreach (string candidate in new[] { "zero", "one", "multiple", "earlier-write", "strict", "mismatch", "search-error" })
        {
            foreach (bool header in new[] { false, true })
            {
                using IFhirStore store = CreateStore(version, strict: candidate == "strict");
                if (candidate is "one" or "multiple")
                {
                    SeedPatient(store, "match-one", "Match");
                }
                if (candidate == "multiple")
                {
                    SeedPatient(store, "match-two", "Match");
                }
                string query = candidate == "search-error" ? "not-a-search-parameter=value" : "name=Match";
                JsonObject earlier = Entry("candidate", "POST", candidate == "mismatch" ? "Observation" : "Patient", Patient("placeholder"));
                if (candidate is not ("strict" or "mismatch"))
                {
                    SetConditional(earlier, query, header);
                }
                JsonObject batch = candidate == "earlier-write"
                    ? Bundle("batch", Entry("earlier-match", "POST", "Patient", Patient(null, "Match")), earlier)
                    : Bundle("batch", earlier);
                object shared = ToModel(version, Patient("Patient", "Aliased"));
                Hl7.Fhir.Model.Bundle request = AliasedNestedRequest(version, shared, batch,
                    Bundle("transaction",
                        Entry("create", "POST", "Patient", Patient("placeholder")),
                        Entry("denied-read", "GET", "Patient/Patient")));
                string input = ToJson(store, request).ToJsonString();
                string[] before = Fingerprint(store);
                MutationWatch watch = new(store);
                JsonObject response = SendObjectBundle(store, request, Authorize(store, "Patient.c", "Patient.s", "Observation.c"),
                    headers: candidate == "search-error" ? StrictSearchHeaders() : null);
                AssertCollateralDenial(ResponseEntry(response, 0));
                Assert.Single(Issues(ResponseEntry(response, 0)));
                AssertCollateralDenial(ResponseEntry(response, 1));
                Assert.Equal("Denied descendant Bundle.entry[1].entry[1]: Unauthorized request: GET Patient/Patient, parsed interaction: InstanceRead",
                    Issues(ResponseEntry(response, 1))[1]!["diagnostics"]!.GetValue<string>());
                Assert.Equal(input, ToJson(store, request).ToJsonString());
                AssertUnchanged(store, before, watch);
            }
        }

        // Every ordinal is visited, even when Bundle, entry, request and resource objects repeat.
        using (IFhirStore store = CreateStore(version))
        {
            Hl7.Fhir.Model.Bundle child = ModelBundle(ToModel(version, Bundle("batch",
                Entry("denied", "GET", "Observation/Patient", Patient("Patient")),
                Entry("also-denied", "GET", "Observation/Patient", Patient("Patient")))));
            child.Entry[1].Request = child.Entry[0].Request;
            child.Entry[1].Resource = child.Entry[0].Resource;
            child.Entry.Add(child.Entry[0]);
            Hl7.Fhir.Model.Bundle request = ModelBundle(ToModel(version, Bundle("transaction",
                Entry("wrapper", "POST", "/", Bundle("batch")))));
            request.Entry[0].Resource = child;
            request.Entry.Add(request.Entry[0]);
            string input = ToJson(store, request).ToJsonString();
            string[] before = Fingerprint(store);
            MutationWatch watch = new(store);
            JsonObject response = SendObjectBundle(store, request, Authorize(store, "Patient.c", "Patient.s"));
            Assert.Equal(2, Entries(response).Count);
            for (int wrapper = 0; wrapper < 2; wrapper++)
            {
                JsonObject refused = ResponseEntry(response, wrapper);
                AssertCollateralDenial(refused);
                Assert.Equal(4, Issues(refused).Count);
                for (int descendant = 0; descendant < 3; descendant++)
                {
                    Assert.Equal($"Denied descendant Bundle.entry[{wrapper}].entry[{descendant}]: Unauthorized request: GET Observation/Patient, parsed interaction: InstanceRead",
                        Issues(refused)[descendant + 1]!["diagnostics"]!.GetValue<string>());
                }
            }
            Assert.Equal(input, ToJson(store, request).ToJsonString());
            AssertUnchanged(store, before, watch);
        }

        // Reusing an approved child in independent batch positions must see its prior URL/ID writes.
        using (IFhirStore store = CreateStore(version))
        {
            Hl7.Fhir.Model.Bundle child = ModelBundle(ToModel(version, Bundle("transaction",
                Entry("create", "POST", "Patient", Patient("client", "Repeated")),
                Entry("read", "GET", "Patient/client"),
                Entry("same-request", "GET", "Patient/client"))));
            child.Entry[2].Request = child.Entry[1].Request;
            Hl7.Fhir.Model.Bundle request = ModelBundle(ToModel(version, Bundle("batch",
                Entry("wrapper", "POST", "/", Bundle("transaction")),
                Entry("observer", "GET", "Patient/client"))));
            request.Entry[0].Resource = child;
            request.Entry.Insert(1, request.Entry[0]);
            request.Entry[2].Request = child.Entry[1].Request;
            object shared = Assert.IsAssignableFrom<Hl7.Fhir.Model.Resource>(child.Entry[0].Resource);
            MutationWatch watch = new(store);
            JsonObject response = SendObjectBundle(store, request, Authorize(store, "Patient.c", "Patient.r"));
            string firstId = LocationId(ResponseEntry(NestedResponse(response, 0), 0));
            string secondId = LocationId(ResponseEntry(NestedResponse(response, 1), 0));
            Assert.NotEqual(firstId, secondId);
            Assert.Equal($"Patient/{secondId}", child.Entry[1].Request!.Url);
            Assert.Same(child.Entry[1].Request, child.Entry[2].Request);
            Assert.Same(child.Entry[1].Request, request.Entry[2].Request);
            // Read responses also retain the live body, but their addressed Locations stay original.
            AssertSuccessResource(ResponseEntry(NestedResponse(response, 0), 1), 200, secondId, "1", firstId);
            AssertSuccessResource(ResponseEntry(NestedResponse(response, 0), 2), 200, secondId, "1", firstId);
            AssertSuccessResource(ResponseEntry(NestedResponse(response, 1), 1), 200, secondId, "1");
            AssertSuccessResource(ResponseEntry(response, 2), 200, secondId, "1");
            Assert.Equal(new[] { firstId, secondId }.Order(), store["Patient"].Keys.Order());
            Assert.Same(shared, store["Patient"][firstId]);
            Assert.Same(shared, store["Patient"][secondId]);
            Assert.Equal(secondId, ToJson(store, shared)["id"]!.GetValue<string>());
            Assert.Equal(new[] { $"create:Patient/{firstId}", $"create:Patient/{secondId}" }, watch.Events);
        }
    }

    /// <summary>Conditional outcomes bind later DELETE aliases from real current IDs, not inferred POST assignments.</summary>
    [Theory]
    [MemberData(nameof(Versions))]
    public void ConditionalAliasOutcomesPreserveLiveDeleteTargets(FhirReleases.FhirSequenceCodes version)
    {
        foreach (string outcome in new[] { "match", "no-match", "multiple", "earlier-write", "search-error" })
        {
            foreach (bool header in new[] { false, true })
            {
                using IFhirStore store = CreateStore(version);
                SeedPatient(store, "client", "SeededTarget");
                if (outcome == "multiple")
                {
                    SeedPatient(store, "second-match", "SeededTarget");
                }
                string seeded = Stored(store, "Patient", "client").ToJsonString();
                string query = outcome switch
                {
                    "no-match" => "name=Absent",
                    "earlier-write" => "name=Introduced",
                    "search-error" => "not-a-search-parameter=value",
                    _ => "name=SeededTarget",
                };
                JsonObject conditional = Entry("conditional", "POST", "Patient", Patient("placeholder"));
                SetConditional(conditional, query, header);
                string submitted = conditional["request"]!["url"]!.GetValue<string>();
                JsonObject batch = outcome == "earlier-write"
                    ? Bundle("batch", Entry("introduce-match", "POST", "Patient", Patient(null, "Introduced")), conditional)
                    : Bundle("batch", conditional);
                object shared = ToModel(version, Patient("client", "SharedInput"));
                Hl7.Fhir.Model.Bundle request = AliasedNestedRequest(version, shared, batch,
                    Bundle("transaction",
                        Entry("child-post", "POST", "Patient", Patient("placeholder")),
                        Entry("child-delete", "DELETE", "Patient/client")));
                MutationWatch watch = new(store);
                JsonObject response = SendObjectBundle(store, request, Authorize(store, "Patient.c", "Patient.s", "Patient.d"),
                    headers: outcome == "search-error" ? StrictSearchHeaders() : null);
                JsonObject earlier = NestedResponse(response, 0);
                JsonObject conditionalResponse = ResponseEntry(earlier, Entries(earlier).Count - 1);
                JsonObject later = NestedResponse(response, 1);
                AssertOrder(later, "child-delete", "child-post");
                JsonObject deleted = ResponseEntry(later, 0);
                JsonObject created = ResponseEntry(later, 1);
                string childId = LocationId(created);
                AssertSuccessResource(created, 201, childId, "1");
                Assert.True(Guid.TryParse(childId, out _));
                Assert.Same(shared, store["Patient"][childId]);
                Assert.Equal(childId, ToJson(store, shared)["id"]!.GetValue<string>());
                Assert.Equal(submitted, ModelBundle(EntryResource(request, 0)).Entry[^1].Request!.Url);
                Assert.Equal("Patient", ModelBundle(EntryResource(request, 1)).Entry[0].Request!.Url);
                List<string> expectedKeys = [childId];
                List<string> expectedEvents = [];
                if (outcome == "earlier-write")
                {
                    JsonObject introduced = ResponseEntry(earlier, 0);
                    string introducedId = LocationId(introduced);
                    AssertSuccessResource(introduced, 201, introducedId, "1");
                    AssertSuccessResource(conditionalResponse, 200, introducedId, "1");
                    Assert.Equal("Introduced", Stored(store, "Patient", introducedId)["name"]![0]!["family"]!.GetValue<string>());
                    expectedKeys.Add(introducedId);
                    expectedEvents.Add($"create:Patient/{introducedId}");
                }
                else if (outcome == "no-match")
                {
                    string batchId = LocationId(conditionalResponse);
                    Assert.NotEqual(batchId, childId);
                    AssertSuccessResource(conditionalResponse, 201, childId, "1", batchId);
                    Assert.Same(shared, store["Patient"][batchId]);
                    expectedKeys.Add(batchId);
                    expectedEvents.Add($"create:Patient/{batchId}");
                }
                else if (outcome == "match")
                {
                    AssertSuccessResource(conditionalResponse, 200, "client", "1");
                }
                else
                {
                    AssertStatus(conditionalResponse, 412);
                    Assert.Null(conditionalResponse["resource"]);
                    Assert.Null(conditionalResponse["response"]!["location"]);
                    Assert.Contains(outcome == "multiple" ? "too many matches" : "not-a-search-parameter",
                        Issues(conditionalResponse)[0]!["diagnostics"]!.GetValue<string>());
                }
                if (outcome == "no-match")
                {
                    AssertStatus(deleted, 200);
                    Assert.Equal("client", ResourceId(deleted));
                    Assert.Equal(seeded, deleted["resource"]!.ToJsonString());
                    Assert.DoesNotContain("client", store["Patient"].Keys);
                    expectedEvents.Add("delete:Patient/client");
                    Assert.Equal("Patient/client", ModelBundle(EntryResource(request, 1)).Entry[1].Request!.Url);
                }
                else
                {
                    AssertStatus(deleted, 404);
                    Assert.Null(deleted["resource"]);
                    Assert.Equal(seeded, Stored(store, "Patient", "client").ToJsonString());
                    Assert.Equal($"Patient/{childId}", ModelBundle(EntryResource(request, 1)).Entry[1].Request!.Url);
                    expectedKeys.Add("client");
                }
                if (outcome == "multiple")
                {
                    expectedKeys.Add("second-match");
                }
                expectedEvents.Add($"create:Patient/{childId}");
                Assert.Equal(expectedKeys.Order(), store["Patient"].Keys.Order());
                Assert.Equal(expectedEvents, watch.Events);
                Assert.Empty(store["Observation"]);
            }
        }
    }

    /// <summary>Failures before assignment preserve the seeded DELETE target and earlier authorized partial effects.</summary>
    [Theory]
    [MemberData(nameof(Versions))]
    public void PreAssignmentFailuresPreserveLiveDeleteTargets(FhirReleases.FhirSequenceCodes version)
    {
        foreach (string failure in new[] { "strict", "mismatch", "query-error", "header-error" })
        {
            using IFhirStore store = CreateStore(version, strict: failure == "strict");
            SeedPatient(store, "client", "SeededTarget");
            string seeded = Stored(store, "Patient", "client").ToJsonString();
            object shared = ToModel(version, Patient("client", "SharedInput"));
            JsonObject candidate = Entry("failure", "POST", failure == "mismatch" ? "Observation" : "Patient", Patient("placeholder"));
            if (failure.EndsWith("-error", StringComparison.Ordinal))
            {
                SetConditional(candidate, "not-a-search-parameter=value", failure == "header-error");
            }
            Hl7.Fhir.Model.Bundle request = AliasedNestedRequest(version, shared,
                Bundle("batch", Entry("earlier-success", "POST", "Patient", Patient(null, "EarlierSuccess")), candidate),
                Bundle("transaction",
                    Entry("forced-create", "POST", "Patient", Patient("placeholder")),
                    Entry("delete", "DELETE", "Patient/client")));
            MutationWatch watch = new(store);
            JsonObject response = SendObjectBundle(store, request, Authorize(store, "*.*"),
                headers: failure.EndsWith("-error", StringComparison.Ordinal) ? StrictSearchHeaders() : null);
            JsonObject earlier = NestedResponse(response, 0);
            string earlierId = LocationId(ResponseEntry(earlier, 0));
            AssertSuccessResource(ResponseEntry(earlier, 0), 201, earlierId, "1");
            JsonObject failed = ResponseEntry(earlier, 1);
            AssertStatus(failed, failure == "strict" ? 400 : failure == "mismatch" ? 422 : 412);
            Assert.Null(failed["resource"]);
            Assert.Null(failed["response"]!["location"]);
            Assert.Contains(failure == "strict" ? "Resource.id" : failure == "mismatch" ? "does not match request" : "not-a-search-parameter",
                Issues(failed)[0]!["diagnostics"]!.GetValue<string>());
            JsonObject later = NestedResponse(response, 1);
            AssertOrder(later, "delete", "forced-create");
            AssertStatus(ResponseEntry(later, 0), 404);
            JsonObject created = ResponseEntry(later, 1);
            string childId = LocationId(created);
            AssertSuccessResource(created, 201, childId, "1");
            Assert.Equal($"Patient/{childId}", ModelBundle(EntryResource(request, 1)).Entry[1].Request!.Url);
            Assert.Equal(seeded, Stored(store, "Patient", "client").ToJsonString());
            Assert.Equal(new[] { "client", earlierId, childId }.Order(), store["Patient"].Keys.Order());
            Assert.Equal("EarlierSuccess", Stored(store, "Patient", earlierId)["name"]![0]!["family"]!.GetValue<string>());
            Assert.Same(shared, store["Patient"][childId]);
            Assert.Equal(childId, ToJson(store, shared)["id"]!.GetValue<string>());
            Assert.Equal(new[] { $"create:Patient/{earlierId}", $"create:Patient/{childId}" }, watch.Events);
            Assert.Empty(store["Observation"]);
        }
    }

    /// <summary>Literal URL text and actual targets qualify the invariant against the current parser and policy.</summary>
    public static IEnumerable<object[]> TransactionUrlCases
    {
        get
        {
            (string Method, string? Url, string OriginalId, bool Remaps, Common.StoreInteractionCodes? Interaction, int Status)[] cases =
            [
                ("GET", "Patient/Patient", "Patient", true, Common.StoreInteractionCodes.InstanceRead, 200),
                ("GET", "Observation/client", "client", true, Common.StoreInteractionCodes.InstanceRead, 404),
                ("GET", "Patient/absent", "client", false, Common.StoreInteractionCodes.InstanceRead, 404),
                ("HEAD", "Patient/client", "client", true, Common.StoreInteractionCodes.InstanceRead, 200),
                ("GET", "/Patient/client?_tag=client&&_pretty=true&text=client%2Fclient?client", "client", true, Common.StoreInteractionCodes.InstanceRead, 200),
                ("GET", "{base}/Patient/Patient?_tag=Patient&_format=json", "Patient", true, Common.StoreInteractionCodes.InstanceRead, 200),
                ("GET", "{base}/Patient/fhir?text=fhir%2Ffhir&_pretty=true", "fhir", true, Common.StoreInteractionCodes.InstanceRead, 200),
                ("GET", "//Patient//client?_tag=client", "client", true, Common.StoreInteractionCodes.InstanceRead, 200),
                ("PUT", "Patient/Patient?_format=json&_pretty=true", "Patient", true, Common.StoreInteractionCodes.InstanceUpdate, 200),
                ("PUT", "/Patient/client?_pretty=true", "client", true, Common.StoreInteractionCodes.InstanceUpdate, 200),
                ("PUT", "{base}/Patient/client?name=Created&_format=json", "client", true, Common.StoreInteractionCodes.InstanceUpdateConditional, 200),
                ("PUT", "Patient/client?_id=client", "client", true, Common.StoreInteractionCodes.InstanceUpdateConditional, 412),
                ("PATCH", "Patient/Patient?_format=json&_pretty=true", "Patient", true, Common.StoreInteractionCodes.InstancePatch, 501),
                ("PATCH", "Patient/client?name=Created&_pretty=true", "client", true, Common.StoreInteractionCodes.InstancePatchConditional, 501),
                ("DELETE", "Patient/client?_tag=client", "client", true, Common.StoreInteractionCodes.InstanceDelete, 404),
                ("DELETE", "Patient/client/", "client", false, Common.StoreInteractionCodes.InstanceDelete, 200),
                ("DELETE", "{base}/Patient/client/?_pretty=true", "client", false, Common.StoreInteractionCodes.InstanceDelete, 200),
                ("GET", "Patient/client/", "client", false, Common.StoreInteractionCodes.InstanceRead, 200),
                ("POST", "Patient", "Patient", false, Common.StoreInteractionCodes.TypeCreate, 201),
                ("GET", "Patient?name=Created", "Patient", false, Common.StoreInteractionCodes.TypeSearch, 200),
                ("POST", "Patient?name=Absent", "Patient", false, Common.StoreInteractionCodes.TypeCreateConditional, 201),
                ("PUT", "Patient?name=Created", "Patient", false, Common.StoreInteractionCodes.InstanceUpdateConditional, 200),
                ("GET", "metadata", "metadata", false, Common.StoreInteractionCodes.SystemCapabilities, 200),
                ("GET", "Patient/client/_history", "_history", false, Common.StoreInteractionCodes.InstanceReadHistory, 501),
                ("GET", "Patient/client/_history/1", "1", false, Common.StoreInteractionCodes.InstanceReadVersion, 501),
                ("DELETE", "Patient/client/_history", "_history", false, Common.StoreInteractionCodes.InstanceDeleteHistory, 501),
                ("GET", "Patient/client/Observation", "Observation", false, Common.StoreInteractionCodes.CompartmentTypeSearch, 501),
                ("GET", "Patient/client/*", "*", false, Common.StoreInteractionCodes.CompartmentSearch, 501),
                ("GET", "Patient/$not-installed", "$not-installed", false, Common.StoreInteractionCodes.TypeOperation, 404),
                ("POST", "Patient/client/$not-installed", "$not-installed", false, Common.StoreInteractionCodes.InstanceOperation, 404),
                ("POST", "Patient/_search", "_search", false, Common.StoreInteractionCodes.TypeSearch, 200),
                ("GET", "urn:uuid:00000000-0000-0000-0000-000000000001", "urn:uuid:00000000-0000-0000-0000-000000000001", false, null, 500),
                ("GET", "Unknown/Patient", "Patient", false, null, 500),
                ("GET", "Patient/client/unknown", "unknown", false, null, 500),
                ("GET", "http://elsewhere.test/Patient/client", "client", false, null, 500),
                ("GET", null, "client", false, Common.StoreInteractionCodes.SystemSearch, 403),
            ];
            foreach (object[] version in Versions)
            {
                foreach ((string method, string? url, string id, bool remaps, Common.StoreInteractionCodes? interaction, int status) in cases)
                {
                    foreach (bool optionalNull in new[] { false, true })
                    {
                        yield return [version[0], method, url!, id, remaps, interaction!, status, optionalNull];
                    }
                }
            }
        }
    }

    /// <summary>Only the final ordinary instance-ID range changes; all other shapes keep their literal handling.</summary>
    [Theory]
    [MemberData(nameof(TransactionUrlCases))]
    public void TransactionUrlRemappingPreservesAuthorizationAndLiteralBoundaries(
        FhirReleases.FhirSequenceCodes version,
        string method,
        string? submitted,
        string originalId,
        bool remaps,
        Common.StoreInteractionCodes? interaction,
        int status,
        bool optionalNull)
    {
        using IFhirStore store = CreateStore(version);
        SeedPatient(store, "client", "SeededTarget");
        SeedResource(store, "Observation", "client", Observation("client", "Patient/client"));
        string seeded = Stored(store, "Patient", "client").ToJsonString();
        string seededObservation = Stored(store, "Observation", "client").ToJsonString();
        AuthorizationInfo? authorization = optionalNull ? null : Authorize(store, "*.*");
        string? url = submitted?.Replace("{base}", store.Config.BaseUrl, StringComparison.Ordinal);
        JsonObject target = Entry("target", method, "Patient/client",
            method is "PUT" or "POST" ? Patient(null, "Updated") : null);
        JsonObject json = Bundle("transaction",
            Entry("create", "POST", "Patient", Patient("placeholder", "Created")),
            target,
            Entry("reference", "POST", "Observation", Observation("observation", PatientFullUrl)),
            Entry("urn-source", "GET", "NotAResource/noop"));
        json["entry"]![0]!["fullUrl"] = PatientFullUrl;
        Hl7.Fhir.Model.Bundle request = ModelBundle(ToModel(version, json));
        request.Entry[0].Resource!.Id = originalId;
        request.Entry[1].Request!.Url = url;
        FhirRequestContext before = Context(store, method, url ?? string.Empty, authorization);
        Assert.Equal(interaction, before.Interaction);

        // A scope-discriminating refusal also proves no URL/body preparation happened.
        AuthorizationInfo restricted = Authorize(store, "Patient.c", "Observation.c");
        FhirRequestContext restrictedTarget = Context(store, method, url ?? string.Empty, restricted);
        if ((restrictedTarget.Interaction is not null) && !restrictedTarget.IsAuthorized())
        {
            string input = ToJson(store, request).ToJsonString();
            string[] fingerprint = Fingerprint(store);
            MutationWatch deniedWatch = new(store);
            JsonObject denied = SendObjectBundle(store, request, restricted);
            JsonObject own = Entries(denied).OfType<JsonObject>().Single(e => e["fullUrl"]!.GetValue<string>() == "target");
            AssertOwnDenial(own, method, url ?? string.Empty);
            Assert.Equal(input, ToJson(store, request).ToJsonString());
            AssertUnchanged(store, fingerprint, deniedWatch);
        }

        MutationWatch watch = new(store);
        JsonObject response = SendObjectBundle(store, request, authorization);
        JsonObject created = Entries(response).OfType<JsonObject>().Single(e => e["fullUrl"]!.GetValue<string>() == PatientFullUrl);
        string allocatedId = LocationId(created);
        Assert.True(Guid.TryParse(allocatedId, out _));
        Assert.NotEqual(originalId, allocatedId);
        string? expectedUrl = url;
        if (remaps)
        {
            int queryStart = url!.IndexOf('?');
            int pathEnd = queryStart < 0 ? url.Length : queryStart;
            expectedUrl = url[..(pathEnd - originalId.Length)] + allocatedId + url[pathEnd..];
        }
        Assert.Equal(expectedUrl, request.Entry[1].Request!.Url);
        FhirRequestContext after = Context(store, method, request.Entry[1].Request!.Url ?? string.Empty, authorization);
        Assert.Equal((before.Interaction, before.HttpMethod, before.ResourceType), (after.Interaction, after.HttpMethod, after.ResourceType));
        Assert.Equal(before.UrlQuery, after.UrlQuery);
        JsonObject executed = Entries(response).OfType<JsonObject>().Single(e => e["fullUrl"]!.GetValue<string>() == "target");
        AssertStatus(executed, status);
        JsonObject reference = Entries(response).OfType<JsonObject>().Single(e => e["fullUrl"]!.GetValue<string>() == "reference");
        Assert.Equal($"Patient/{allocatedId}", Stored(store, "Observation", ResourceId(reference))["subject"]!["reference"]!.GetValue<string>());
        Assert.Equal("Patient", request.Entry[0].Request!.Url);
        Assert.Equal("NotAResource/noop", request.Entry[3].Request!.Url);
        Assert.Equal(seededObservation, Stored(store, "Observation", "client").ToJsonString());
        if ((method is "GET" or "HEAD") && (interaction == Common.StoreInteractionCodes.InstanceRead) && (status == 200))
        {
            Assert.Equal(remaps ? allocatedId : "client", ResourceId(executed));
            Assert.Equal(remaps ? "Created" : "SeededTarget", executed["resource"]!["name"]![0]!["family"]!.GetValue<string>());
        }
        if ((method == "PUT") && (status == 200))
        {
            Assert.Equal(allocatedId, ResourceId(executed));
            Assert.Equal("Updated", Stored(store, "Patient", allocatedId)["name"]![0]!["family"]!.GetValue<string>());
            Assert.Contains($"update:Patient/{allocatedId}", watch.Events);
        }
        if ((method == "DELETE") && (status == 200))
        {
            Assert.Equal("client", ResourceId(executed));
            Assert.Equal(seeded, executed["resource"]!.ToJsonString());
            Assert.DoesNotContain("client", store["Patient"].Keys);
            Assert.Equal(new[] { "delete:Patient/client", $"create:Patient/{allocatedId}", $"create:Observation/{ResourceId(reference)}" }, watch.Events);
        }
        else
        {
            Assert.Equal(seeded, Stored(store, "Patient", "client").ToJsonString());
        }
    }

    /// <summary>Batch units retain input order, duplicate identities, live-state reads, and conditional visibility.</summary>
    [Theory]
    [MemberData(nameof(Versions))]
    public void BatchKeepsIndependentOutcomes(FhirReleases.FhirSequenceCodes version)
    {
        using IFhirStore store = CreateStore(version);
        MutationWatch watch = new(store);
        JsonObject first = Entry("first", "PUT", "Patient/created", Patient("created", "Allowed"));
        first.Remove("fullUrl");
        JsonObject conditional = Entry("duplicate", "POST", "Patient", Patient(null, "MustNotCreate"));
        conditional["request"]!["ifNoneExist"] = "_id=created";
        JsonObject response = SendBundle(store, Bundle("batch",
            first,
            Entry("duplicate", "POST", "Observation", Observation("denied", "Patient/created")),
            Entry("duplicate", "GET", "Patient/created"),
            conditional,
            Entry("denied-read", "GET", "Observation/absent"),
            Entry("nested-batch", "POST", "/", Bundle("batch",
                Entry("nested-denied", "HEAD", "Observation/absent"),
                Entry("nested-allowed", "GET", "Patient/created")))),
            Authorize(store, "Patient.c", "Patient.u", "Patient.r", "Patient.s"));
        Assert.Equal(new[] { 201, 401, 200, 200, 401, 200 }, Statuses(response));
        Assert.Null(ResponseEntry(response, 0)["fullUrl"]);
        Assert.Equal("duplicate", ResponseEntry(response, 1)["fullUrl"]!.GetValue<string>());
        Assert.Equal("duplicate", ResponseEntry(response, 2)["fullUrl"]!.GetValue<string>());
        Assert.Equal("duplicate", ResponseEntry(response, 3)["fullUrl"]!.GetValue<string>());
        AssertOwnDenial(ResponseEntry(response, 1), "POST", "Observation");
        AssertOwnDenial(ResponseEntry(response, 4), "GET", "Observation/absent");
        AssertSuccessResource(ResponseEntry(response, 0), 201, "created", "1");
        AssertSuccessResource(ResponseEntry(response, 2), 200, "created", "1");
        AssertSuccessResource(ResponseEntry(response, 3), 200, "created", "1");
        JsonObject nested = Assert.IsType<JsonObject>(ResponseEntry(response, 5)["resource"]);
        AssertOwnDenial(ResponseEntry(nested, 0), "HEAD", "Observation/absent");
        AssertSuccessResource(ResponseEntry(nested, 1), 200, "created", "1");
        Assert.Single(store["Patient"]);
        Assert.Empty(store["Observation"]);
        Assert.Equal("Allowed", Stored(store, "Patient", "created")["name"]![0]!["family"]!.GetValue<string>());
        Assert.Equal(new[] { "update:Patient/created" }, watch.Events);
    }

    /// <summary>Rejected object input must not even prepare the actual live dictionary resource or its request URLs.</summary>
    [Theory]
    [MemberData(nameof(Versions))]
    public void RejectedTransactionDoesNotMutateAliasedInput(FhirReleases.FhirSequenceCodes version)
    {
        using IFhirStore store = CreateStore(version);
        JsonObject patient = Patient("client", "LiveOriginal");
        patient["generalPractitioner"] = new JsonArray(new JsonObject { ["reference"] = "client" });
        SeedResource(store, "Patient", "client", patient);
        object live = store["Patient"]["client"];
        object request = ToModel(version, Bundle("transaction",
            Entry("post", "POST", "Patient", Patient("placeholder"), fullUrl: PatientFullUrl),
            Entry("read", "GET", "Patient/client"),
            Entry("denied", "GET", "Observation/absent")));
        SetEntryResource(request, 0, live);
        Assert.Same(live, EntryResource(request, 0));
        string inputBefore = ToJson(store, request).ToJsonString();
        string[] before = Fingerprint(store);
        MutationWatch watch = new(store);
        JsonObject response = SendObjectBundle(store, request, Authorize(store, "Patient.c", "Patient.r"));
        AssertCollateralDenial(ResponseEntry(response, 0));
        AssertCollateralDenial(ResponseEntry(response, 1));
        AssertOwnDenial(ResponseEntry(response, 2), "GET", "Observation/absent");
        Assert.Equal(inputBefore, ToJson(store, request).ToJsonString());
        Assert.Same(live, store["Patient"]["client"]);
        Assert.Equal("client", Stored(store, "Patient", "client")["generalPractitioner"]![0]!["reference"]!.GetValue<string>());
        AssertUnchanged(store, before, watch);
    }

    /// <summary>Reservation/admission never moves descendant reference searches ahead of earlier parent writes.</summary>
    [Theory]
    [MemberData(nameof(Versions))]
    public void NestedAuthorizedTransactionResolvesReferencesAtExecution(FhirReleases.FhirSequenceCodes version)
    {
        using IFhirStore store = CreateStore(version);
        MutationWatch watch = new(store);
        JsonObject response = SendBundle(store, Bundle("transaction",
            Entry("child", "POST", "/", Bundle("transaction",
                Entry("observation", "POST", "Observation", Observation("child", "Patient?name=WrittenFirst")))),
            Entry("parent", "PUT", "Patient/parent", Patient("parent", "WrittenFirst"))),
            Authorize(store, "Patient.u", "Observation.c"));
        // The child POST cannot see the parent PUT in a later method group.
        JsonObject earlyChild = Assert.IsType<JsonObject>(ResponseEntry(response, 0)["resource"]);
        AssertStatus(ResponseEntry(earlyChild, 0), 201);
        Assert.Equal("Patient?name=WrittenFirst", Stored(store, "Observation", ResourceId(ResponseEntry(earlyChild, 0)))["subject"]!["reference"]!.GetValue<string>());
        Assert.Equal(new[] { $"create:Observation/{ResourceId(ResponseEntry(earlyChild, 0))}", "update:Patient/parent" }, watch.Events);

        using IFhirStore ordered = CreateStore(version);
        MutationWatch orderedWatch = new(ordered);
        JsonObject positive = SendBundle(ordered, Bundle("transaction",
            Entry("read", "GET", "Patient/client"),
            Entry("parent", "POST", "Patient", Patient("client", "WrittenFirst")),
            Entry("child", "POST", "/", Bundle("transaction",
                Entry("observation", "POST", "Observation", Observation("child", "Patient?name=WrittenFirst"))))),
            Authorize(ordered, "Patient.c", "Patient.r", "Observation.c"));
        AssertOrder(positive, "parent", "child", "read");
        string parentId = ResourceId(ResponseEntry(positive, 0));
        JsonObject child = Assert.IsType<JsonObject>(ResponseEntry(positive, 1)["resource"]);
        string childId = ResourceId(ResponseEntry(child, 0));
        AssertSuccessResource(ResponseEntry(positive, 0), 201, parentId, "1");
        AssertSuccessResource(ResponseEntry(child, 0), 201, childId, "1");
        AssertSuccessResource(ResponseEntry(positive, 2), 200, parentId, "1");
        Assert.Equal($"Patient/{parentId}", Stored(ordered, "Observation", childId)["subject"]!["reference"]!.GetValue<string>());
        Assert.Equal(new[] { $"create:Patient/{parentId}", $"create:Observation/{childId}" }, orderedWatch.Events);
        Assert.Single(ordered["Patient"]);
        Assert.Single(ordered["Observation"]);
    }

    /// <summary>Only executable POST-to-base bundles are descendants; stored and operation payloads are ordinary bodies.</summary>
    [Theory]
    [MemberData(nameof(Versions))]
    public void StoredBundlePayloadIsNotRecursivelyAdmitted(FhirReleases.FhirSequenceCodes version)
    {
        foreach (string kind in new[] { "batch", "transaction" })
        {
            using IFhirStore store = CreateStore(version);
            MutationWatch watch = new(store);
            JsonObject stored = SendBundle(store, Bundle(kind, Entry("stored", "POST", "Bundle", Bundle("transaction",
                Entry("payload", "DELETE", "Observation/absent")))), Authorize(store, "Bundle.c"));
            JsonObject created = ResponseEntry(stored, 0);
            AssertSuccessResource(created, 201, ResourceId(created), "1");
            Assert.Single(store["Bundle"]);
            Assert.Equal("DELETE", Stored(store, "Bundle", ResourceId(created))["entry"]![0]!["request"]!["method"]!.GetValue<string>());
            Assert.Equal(new[] { $"create:Bundle/{ResourceId(created)}" }, watch.Events);
            string[] before = Fingerprint(store);
            int priorEvents = watch.Events.Count;
            JsonObject executable = SendBundle(store, Bundle(kind, Entry("execute", "POST", "/", Bundle("transaction",
                Entry("payload", "DELETE", "Observation/absent")))), Authorize(store, "Bundle.c"));
            JsonObject denial = kind == "transaction"
                ? ResponseEntry(executable, 0)
                : ResponseEntry(Assert.IsType<JsonObject>(ResponseEntry(executable, 0)["resource"]), 0);
            if (kind == "transaction")
            {
                AssertCollateralDenial(denial);
                Assert.Contains("Denied descendant", Issues(denial)[1]!["diagnostics"]!.GetValue<string>());
            }
            else
            {
                AssertOwnDenial(denial, "DELETE", "Observation/absent");
            }
            Assert.Equal(before, Fingerprint(store));
            Assert.Equal(priorEvents, watch.Events.Count);

            // A parsed operation is authorized by its method/type, not by entries in its Bundle payload.
            JsonObject operation = SendBundle(store, Bundle(kind, Entry("operation", "POST", "Patient/$not-installed",
                Bundle("transaction", Entry("payload", "DELETE", "Observation/absent")))), Authorize(store, "Patient.u"));
            Assert.NotEqual("401 Unauthorized", ResponseEntry(operation, 0)["response"]!["status"]!.GetValue<string>());
            Assert.Equal(before, Fingerprint(store));
            Assert.Equal(priorEvents, watch.Events.Count);
        }
    }

    /// <summary>Null authorization is policy-driven, including the explicit required-but-not-optional configuration.</summary>
    [Theory]
    [MemberData(nameof(Versions))]
    public void NullAuthorizationRespectsTenantPolicy(FhirReleases.FhirSequenceCodes version)
    {
        foreach ((bool required, bool allowed) in new[] { (false, false), (false, true), (true, false) })
        {
            foreach (string kind in new[] { "batch", "transaction" })
            {
                using IFhirStore store = CreateStore(version, required, allowed);
                SeedPatient(store, "seed");
                MutationWatch watch = new(store);
                string[] before = Fingerprint(store);
                JsonObject response = SendBundle(store, Bundle(kind,
                    Entry("update", "PUT", "Patient/seed", Patient("seed", "NullAllowed"))), null);
                if (required)
                {
                    AssertOwnDenial(ResponseEntry(response, 0), "PUT", "Patient/seed");
                    AssertUnchanged(store, before, watch);
                }
                else
                {
                    AssertSuccessResource(ResponseEntry(response, 0), 200, "seed", "2");
                    Assert.Equal("NullAllowed", Stored(store, "Patient", "seed")["name"]![0]!["family"]!.GetValue<string>());
                    Assert.Equal(new[] { "update:Patient/seed" }, watch.Events);
                }

                before = Fingerprint(store);
                int priorEvents = watch.Events.Count;
                JsonObject restricted = SendBundle(store, Bundle(kind,
                    Entry("denied", "DELETE", "Patient/seed")), Authorize(store, "Patient.r"));
                AssertOwnDenial(ResponseEntry(restricted, 0), "DELETE", "Patient/seed");
                Assert.Equal(before, Fingerprint(store));
                Assert.Equal(priorEvents, watch.Events.Count);
            }
        }
    }

    /// <summary>Real LoadDirectory initialization bypasses recursively, but the exemption ends before runtime dispatch.</summary>
    [Theory]
    [MemberData(nameof(Versions))]
    public void InitialLoadingRetainsBundleBypass(FhirReleases.FhirSequenceCodes version)
    {
        foreach (string kind in new[] { "batch", "transaction" })
        {
            DirectoryInfo root = new(AppContext.BaseDirectory);
            while (!File.Exists(Path.Combine(root.FullName, "fhir-candle.sln")))
            {
                root = root.Parent ?? throw new InvalidOperationException("Repository root not found.");
            }
            string path = Path.Combine(root.FullName, "scratch", "1119-02", $"bundle-loading-{Guid.NewGuid():N}");
            Directory.CreateDirectory(path);
            try
            {
                JsonObject request = Bundle(kind,
                    Entry("parent", "PUT", "Patient/loaded-parent", Patient("loaded-parent", "LoadedParent")),
                    Entry("nested", "POST", "/", Bundle(kind == "batch" ? "transaction" : "batch",
                        Entry("child", "PUT", "Patient/loaded-child", Patient("loaded-child", "LoadedChild")))),
                    Entry("literal-boundary", "POST", "/", Bundle("transaction",
                        Entry("alias", "POST", "Patient", Patient("Patient", "LoadedAliased")),
                        Entry("search", "GET", "Patient?name=LoadedAliased"))));
                File.WriteAllText(Path.Combine(path, "bundle.json"), request.ToJsonString());
                using IFhirStore store = CreateStore(version, smartRequired: true, smartAllowed: false, new DirectoryInfo(path));
                string aliasId = Assert.Single(store["Patient"].Keys, id => id is not ("loaded-child" or "loaded-parent"));
                Assert.True(Guid.TryParse(aliasId, out _));
                Assert.Equal(3, store["Patient"].Count);
                Assert.Equal("LoadedParent", Stored(store, "Patient", "loaded-parent")["name"]![0]!["family"]!.GetValue<string>());
                Assert.Equal("LoadedChild", Stored(store, "Patient", "loaded-child")["name"]![0]!["family"]!.GetValue<string>());
                Assert.Equal("LoadedAliased", Stored(store, "Patient", aliasId)["name"]![0]!["family"]!.GetValue<string>());
                string[] before = Fingerprint(store);
                MutationWatch watch = new(store);
                JsonObject response = SendBundle(store, request, null);
                Assert.All(Entries(response).OfType<JsonObject>(), e => AssertStatus(e, 401));
                AssertUnchanged(store, before, watch);
                JsonObject restricted = SendBundle(store, request, Authorize(store, "Patient.r"));
                if (kind == "transaction")
                {
                    AssertCollateralDenial(ResponseEntry(restricted, 0)); // POST wrapper is grouped first.
                    AssertCollateralDenial(ResponseEntry(restricted, 1));
                    AssertOwnDenial(ResponseEntry(restricted, 2), "PUT", "Patient/loaded-parent");
                }
                else
                {
                    AssertOwnDenial(ResponseEntry(restricted, 0), "PUT", "Patient/loaded-parent");
                    JsonObject childResponse = Assert.IsType<JsonObject>(ResponseEntry(restricted, 1)["resource"]);
                    AssertOwnDenial(ResponseEntry(childResponse, 0), "PUT", "Patient/loaded-child");
                    JsonObject literalResponse = NestedResponse(restricted, 2);
                    AssertOwnDenial(ResponseEntry(literalResponse, 0), "POST", "Patient");
                    AssertOwnDenial(ResponseEntry(literalResponse, 1), "GET", "Patient?name=LoadedAliased");
                }
                AssertUnchanged(store, before, watch);
            }
            finally
            {
                Directory.Delete(path, recursive: true);
            }
        }
    }

    /// <summary>Authorization admission is not rollback: a later authorized missing read retains earlier creates.</summary>
    [Theory]
    [MemberData(nameof(Versions))]
    public void AuthorizedExecutionFailureDoesNotRollBack(FhirReleases.FhirSequenceCodes version)
    {
        using IFhirStore store = CreateStore(version);
        MutationWatch watch = new(store);
        JsonObject response = SendBundle(store, Bundle("transaction",
            Entry("patient", "POST", "Patient", Patient("client"), fullUrl: PatientFullUrl),
            Entry("observation", "POST", "Observation", Observation("observation", PatientFullUrl)),
            Entry("missing", "GET", "Patient/absent")),
            Authorize(store, "Patient.c", "Patient.r", "Observation.c"));
        string patientId = ResourceId(ResponseEntry(response, 0));
        string observationId = ResourceId(ResponseEntry(response, 1));
        AssertSuccessResource(ResponseEntry(response, 0), 201, patientId, "1");
        AssertSuccessResource(ResponseEntry(response, 1), 201, observationId, "1");
        AssertStatus(ResponseEntry(response, 2), 404);
        Assert.Single(store["Patient"]);
        Assert.Single(store["Observation"]);
        Assert.Equal($"Patient/{patientId}", Stored(store, "Observation", observationId)["subject"]!["reference"]!.GetValue<string>());
        Assert.Equal(ResponseEntry(response, 0)["resource"]!.ToJsonString(), Stored(store, "Patient", patientId).ToJsonString());
        Assert.Equal(ResponseEntry(response, 1)["resource"]!.ToJsonString(), Stored(store, "Observation", observationId).ToJsonString());
        Assert.Equal(new[] { $"create:Patient/{patientId}", $"create:Observation/{observationId}" }, watch.Events);
    }

    private static Hl7.Fhir.Model.Bundle ModelBundle(object bundle) => Assert.IsType<Hl7.Fhir.Model.Bundle>(bundle);

    private static JsonObject NestedResponse(JsonObject response, int index) =>
        Assert.IsType<JsonObject>(ResponseEntry(response, index)["resource"]);

    private static string LocationId(JsonObject entry) => entry["response"]!["location"]!.GetValue<string>().Split('/')[^1];

    private static Dictionary<string, StringValues> StrictSearchHeaders() => new() { ["Prefer"] = "handling=strict" };

    private static void SetConditional(JsonObject entry, string query, bool header)
    {
        entry["request"]![header ? "ifNoneExist" : "url"] = header ? query : $"Patient?{query}";
    }

    private static Hl7.Fhir.Model.Bundle AliasedNestedRequest(
        FhirReleases.FhirSequenceCodes version,
        object shared,
        JsonObject earlierBatch,
        JsonObject laterTransaction)
    {
        Hl7.Fhir.Model.Bundle request = ModelBundle(ToModel(version, Bundle("transaction",
            Entry("earlier-batch", "POST", "/", earlierBatch),
            Entry("later-transaction", "POST", "/", laterTransaction))));
        Hl7.Fhir.Model.Bundle batch = ModelBundle(EntryResource(request, 0));
        SetEntryResource(batch, batch.Entry.Count - 1, shared);
        SetEntryResource(EntryResource(request, 1), 0, shared);
        return request;
    }

    private sealed class MutationWatch
    {
        public List<string> Events { get; } = [];

        public MutationWatch(IFhirStore store)
        {
            store.OnInstanceCreated += (_, e) => Events.Add($"create:{e.ResourceType}/{e.ResourceId}");
            store.OnInstanceUpdated += (_, e) => Events.Add($"update:{e.ResourceType}/{e.ResourceId}");
            store.OnInstanceDeleted += (_, e) => Events.Add($"delete:{e.ResourceType}/{e.ResourceId}");
        }
    }

    private static string[] Fingerprint(IFhirStore store) => store.OrderBy(s => s.Key, StringComparer.Ordinal)
        .SelectMany(s => new[] { $"{s.Key}:count={s.Value.Count}" }.Concat(s.Value.OrderBy(r => r.Key, StringComparer.Ordinal)
            .Select(r => $"{s.Key}/{r.Key}:{ToJson(store, r.Value).ToJsonString()}"))).ToArray();

    private static void AssertUnchanged(IFhirStore store, string[] before, MutationWatch watch)
    {
        Assert.Equal(before, Fingerprint(store));
        Assert.Empty(watch.Events);
    }

    private static JsonArray Issues(JsonObject entry) => entry["response"]!["outcome"]!["issue"]!.AsArray();

    private static void AssertRefusal(JsonObject entry)
    {
        AssertStatus(entry, 401);
        Assert.Null(entry["resource"]);
        Assert.Null(entry["response"]!["etag"]);
        Assert.Null(entry["response"]!["location"]);
        Assert.Null(entry["response"]!["lastModified"]);
        Assert.All(Issues(entry), issue => Assert.Equal("forbidden", issue!["code"]!.GetValue<string>()));
    }

    private static void AssertOwnDenial(JsonObject entry, string method, string url)
    {
        AssertRefusal(entry);
        Assert.Single(Issues(entry));
        Assert.StartsWith($"Unauthorized request: {method} {url}, parsed interaction: ", Issues(entry)[0]!["diagnostics"]!.GetValue<string>());
        Assert.DoesNotContain("not executed", Issues(entry)[0]!["diagnostics"]!.GetValue<string>());
    }

    private static void AssertCollateralDenial(JsonObject entry)
    {
        AssertRefusal(entry);
        Assert.Equal("Entry not executed because transaction authorization failed", Issues(entry)[0]!["diagnostics"]!.GetValue<string>());
    }

    private static void SeedResource(IFhirStore store, string type, string id, JsonObject resource)
    {
        FhirRequestContext ctx = Context(store, "PUT", $"{type}/{id}", Authorize(store, "*.*")) with
        {
            SourceContent = resource.ToJsonString(),
            SourceFormat = JsonFormat,
        };
        Assert.True(store.InstanceUpdate(ctx, out FhirResponseContext response), response.SerializedOutcome);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static IFhirStore CreateStore(
        FhirReleases.FhirSequenceCodes version,
        bool smartRequired = false,
        bool smartAllowed = true,
        DirectoryInfo? loadDirectory = null,
        bool strict = false)
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
            SmartRequired = smartRequired,
            SmartAllowed = smartAllowed,
            LoadDirectory = loadDirectory,
            SupportNotChanged = true,
            Strict = strict,
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
        if (store.Config.Strict)
        {
            FhirRequestContext seed = Context(store, "POST", "Patient", Authorize(store, "*.*")) with
            {
                SourceContent = Patient(id, family).ToJsonString(),
                SourceFormat = JsonFormat,
            };
            Assert.True(store.InstanceCreate(seed, out FhirResponseContext created, forceAllowExistingId: true), created.SerializedOutcome);
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            return;
        }

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

    private static JsonObject SendObjectBundle(
        IFhirStore store,
        object bundle,
        AuthorizationInfo? authorization,
        Dictionary<string, StringValues>? headers = null)
    {
        FhirRequestContext ctx = Context(store, "POST", string.Empty, authorization) with
        {
            SourceObject = bundle,
            RequestHeaders = headers ?? [],
        };
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
