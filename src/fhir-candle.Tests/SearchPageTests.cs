// <copyright file="SearchPageTests.cs" company="Microsoft Corporation">
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
using static FhirCandle.Storage.Common;

namespace fhir.candle.Tests;

public class SearchPageTests
{
    public static IEnumerable<object[]> Versions => LifecycleTestStore.Versions;

    private static JsonObject Page(IFhirStore store, string path, Action<string> observe, AuthorizationInfo? auth = null,
        Action<int>? observePeak = null)
    {
        FhirRequestContext ctx = new(store, "GET", path)
        {
            Authorization = auth, DestinationFormat = "application/fhir+json",
        };
        switch (store)
        {
            case candleR4::FhirCandle.Storage.VersionedFhirStore r4:
                r4.PageObservers.Add(ctx, new(observe, observePeak));
                break;
            case candleR4B::FhirCandle.Storage.VersionedFhirStore r4b:
                r4b.PageObservers.Add(ctx, new(observe, observePeak));
                break;
            case candleR5::FhirCandle.Storage.VersionedFhirStore r5:
                r5.PageObservers.Add(ctx, new(observe, observePeak));
                break;
            default:
                throw new ArgumentException(nameof(store));
        }
        FhirResponseContext response;
        bool success = ctx.Interaction switch
        {
            StoreInteractionCodes.TypeSearch => store.TypeSearch(ctx, out response),
            StoreInteractionCodes.CompartmentSearch => store.CompartmentSearch(ctx, out response),
            StoreInteractionCodes.CompartmentTypeSearch => store.CompartmentTypeSearch(ctx, out response),
            StoreInteractionCodes.SystemSearch => store.SystemSearch(ctx, out response),
            _ => throw new ArgumentException(path),
        };
        Assert.True(success, response.SerializedOutcome);
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
             "status":"active","code":"Patient","search":true,"resource":[{"code":"Observation","param":["subject"]},
             {"code":"Encounter","param":["subject"]}]}
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
    public void UnsortedWindowBoundsRetainedKeys(FhirReleases.FhirSequenceCodes version)
    {
        using LifecycleTestStore fixture = new(version);
        SeedObservations(fixture); // Five visible; o5 belongs to another patient.
        AuthorizationInfo auth = Grant();
        int seededVisible = 5;
        foreach (int visible in new[] { 5, 100 })
        {
            while (seededVisible < visible)
            {
                JsonObject row = JsonNode.Parse("""
                    {"resourceType":"Observation","status":"final","code":{"text":"sample"},"subject":{"reference":"Patient/a"}}
                    """)!.AsObject();
                row["id"] = $"extra-{++seededVisible}";
                fixture.Put(row);
            }

            foreach ((string controls, int rows, int peak, bool next) in new[]
            {
                ("_count=1&_offset=2&_maxresults=4", 1, 1, true),
                ("_count=1&_offset=2&_maxresults=3", 1, 1, false),
                ("_summary=count&_maxresults=2", 0, 0, false),
                ("_count=0&_total=none", 0, 0, false),
                ($"_count=1&_offset={visible + 1}", 0, 0, false),
                // Calibration: the sorted path retains both the ordered candidates and the selected window.
                ("_count=1&_sort=_id", 1, visible + 1, true),
            })
            {
                List<int> peaks = [];
                List<string> events = [];
                JsonObject page = Page(fixture.Store, "Observation?" + controls, events.Add, auth, peaks.Add);
                Assert.Equal(visible, (int?)page["total"]);
                Assert.Equal(peak, Assert.Single(peaks));
                string[] matches = Ids(page);
                Assert.Equal(rows, matches.Length);
                Assert.DoesNotContain("o5", matches);
                Assert.Equal(rows, events.Count(e => e == "detached"));
                Assert.Equal(next, Next(page) is not null);
            }
        }
    }

    [Theory]
    [MemberData(nameof(Versions))]
    public void OverlappingSourcesCountEachKeyOnce(FhirReleases.FhirSequenceCodes version)
    {
        using LifecycleTestStore fixture = new(version);
        fixture.Put(JsonNode.Parse("""
            {"resourceType":"CompartmentDefinition","id":"membership","url":"urn:test:membership","name":"Membership",
             "status":"active","code":"Patient","search":true,"resource":[{"code":"Observation","param":["subject","performer"]}]}
            """)!.AsObject());
        fixture.Put(new() { ["resourceType"] = "Patient", ["id"] = "a" });
        foreach ((string id, string subject, string performer) in new[]
        {
            ("a", "a", "a"), ("subject", "a", "b"), ("performer", "b", "a"), ("neither", "b", "b"),
        })
        {
            JsonObject row = JsonNode.Parse("""
                {"resourceType":"Observation","status":"final","code":{"text":"sample"},"subject":{},"performer":[{}]}
                """)!.AsObject();
            row["id"] = id;
            row["subject"]!["reference"] = "Patient/" + subject;
            row["performer"]![0]!["reference"] = "Patient/" + performer;
            fixture.Put(row);
        }
        JsonObject system = Page(fixture.Store, "?_type=Patient,Observation&_type=Observation,Patient", _ => { });
        Assert.Equal(5, (int?)system["total"]);
        AssertPageEntries(fixture.Store, system, ("Patient/a", "match", "a"), ("Observation/a", "match", "a"),
            ("Observation/subject", "match", "subject"), ("Observation/performer", "match", "performer"),
            ("Observation/neither", "match", "neither"));
        foreach (string path in new[] { "Patient/a/Observation", "Patient/a/*" })
        {
            JsonObject compartment = Page(fixture.Store, path, _ => { });
            Assert.Equal(3, (int?)compartment["total"]);
            Assert.Equal(new[] { "a", "performer", "subject" }, Ids(compartment).Order());
        }
    }

    /// <summary>Global offsets count visible primaries, not includes, hidden rows, or per-type windows.</summary>
    [Theory]
    [MemberData(nameof(Versions))]
    public void CrossTypePagesMatchUnpagedPrimaries(FhirReleases.FhirSequenceCodes version)
    {
        using LifecycleTestStore fixture = new(version);
        SeedObservations(fixture);
        AuthorizationInfo auth = Grant();
        auth.UserScopes.Add("Encounter.s");
        foreach ((string id, string patient) in new[] { ("o0a", "a"), ("o2a", "b") })
        {
            JsonObject encounter = fixture.Fixture("planned");
            encounter["id"] = id;
            encounter["subject"]!["reference"] = "Patient/" + patient;
            fixture.Put(encounter);
        }

        static string[] Keys(JsonObject bundle, string mode) =>
            (bundle["entry"]?.AsArray() ?? []).Where(e => (string?)e!["search"]!["mode"] == mode)
                .Select(e => $"{e!["resource"]!["resourceType"]}/{e["resource"]!["id"]}").ToArray();

        string[] observations = ["Observation/o0", "Observation/o1", "Observation/o2", "Observation/o3", "Observation/o4"];
        (string Route, string[] Expected)[] cases =
        [
            ("Observation?", observations),
            ("Patient/a/Observation?", observations),
            ("Patient/a/*?", [observations[0], "Encounter/o0a", .. observations.Skip(1)]),
            ("?_type=Observation,Encounter&", [observations[0], "Encounter/o0a", observations[1],
                observations[2], "Encounter/o2a", .. observations.Skip(3)]),
        ];
        foreach ((string route, string[] expected) in cases)
        {
            string path = route + "_sort=_id";
            JsonObject unpaged = Page(fixture.Store, path, _ => { }, auth);
            Assert.Equal(expected, Keys(unpaged, "match"));
            path += "&_include=Observation:has-member";
            foreach ((string controls, int limit) in new[]
            {
                ("", expected.Length), ("&_maxresults=3", 3), ("&_total=accurate", expected.Length),
                ("&_total=estimate", expected.Length), ("&_total=none&_maxresults=3", 3),
            })
            {
                string? link = path + "&_count=1" + controls;
                foreach (string primary in expected.Take(limit))
                {
                    Assert.NotNull(link);
                    JsonObject page = Page(fixture.Store, link, _ => { }, auth);
                    Assert.Equal(controls.Contains("_total=none") ? (int?)null : expected.Length, (int?)page["total"]);
                    Assert.Equal(new[] { primary }, Keys(page, "match"));
                    string[] includes = primary.StartsWith("Observation/") && primary != "Observation/o1"
                        ? ["Observation/o1"] : [];
                    Assert.Equal(includes, Keys(page, "include"));
                    link = Next(page);
                }
                Assert.Null(link);
            }
            foreach (string controls in new[] { "_offset=99", "_count=0&_total=none", "_summary=count&_maxresults=1" })
            {
                JsonObject empty = Page(fixture.Store, path + "&" + controls, _ => { }, auth);
                Assert.Equal(expected.Length, (int?)empty["total"]);
                Assert.Empty(empty["entry"]?.AsArray() ?? []);
                Assert.Null(Next(empty));
            }
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
    public void AliasedBodyIdsFetchEachSelectedStoreKey(FhirReleases.FhirSequenceCodes version)
    {
        using LifecycleTestStore fixture = new(version);
        SeedObservations(fixture);
        // Distinct values make a repeated fetch of the same body observable, even across pages.
        foreach (string key in new[] { "o1", "o2", "o5" })
        {
            ((Hl7.Fhir.Model.Resource)fixture.Store["Observation"][key]).Id = "o1";
        }
        AuthorizationInfo auth = Grant();
        const string Path = "Observation?_id=o1&_sort=value-quantity";
        static void CheckEntry(JsonNode entry, string key, decimal value)
        {
            Assert.EndsWith("/Observation/" + key, (string?)entry["fullUrl"]);
            Assert.Equal("o1", (string?)entry["resource"]!["id"]);
            Assert.Equal(value, (decimal?)entry["resource"]!["valueQuantity"]!["value"]);
        }
        JsonObject all = Page(fixture.Store, Path, _ => { }, auth);
        Assert.Equal(2, (int?)all["total"]);
        Assert.Collection(all["entry"]!.AsArray(), e => CheckEntry(e!, "o2", 2), e => CheckEntry(e!, "o1", 10));

        string? link = Path + "&_count=1&_include=Observation:subject";
        foreach ((string key, int value) in new[] { ("o2", 2), ("o1", 10) })
        {
            Assert.NotNull(link);
            List<string> events = [];
            JsonObject page = Page(fixture.Store, link, events.Add, auth);
            Assert.Equal(2, (int?)page["total"]);
            CheckEntry(Assert.Single(page["entry"]!.AsArray(), e => (string?)e!["search"]!["mode"] == "match")!, key, value);
            Assert.Equal(new[] { "a" }, Ids(page, "include"));
            Assert.Equal(new[] { "selected", "detached", "expanded" }, events);
            link = Next(page);
        }
        Assert.Null(link);
    }

    [Theory]
    [MemberData(nameof(Versions))]
    public void ForwardIncludesUseResolvedStoreKeys(FhirReleases.FhirSequenceCodes version)
    {
        using LifecycleTestStore fixture = new(version);
        SeedObservations(fixture);
        fixture.Put(JsonNode.Parse("""
            {"resourceType":"Practitioner","id":"logical","identifier":[{"system":"urn:test:staff","value":"two"}]}
            """)!.AsObject());
        fixture.Put(JsonNode.Parse("""
            {"resourceType":"Observation","id":"o0","status":"final","code":{"text":"sample"},
             "subject":{"reference":"Patient/a"},"hasMember":[{"reference":"Observation/o1"},{"reference":"Observation/o5"}],
             "performer":[{"reference":"Practitioner/doctor"},{"reference":"Practitioner/doctor"},
                          {"type":"Practitioner","identifier":{"system":"urn:test:staff","value":"two"}}]}
            """)!.AsObject());
        foreach (string key in new[] { "doctor", "logical" })
        {
            ((Hl7.Fhir.Model.Resource)fixture.Store["Practitioner"][key]).Id = "shared";
        }
        foreach (string key in new[] { "o0", "o1" })
        {
            ((Hl7.Fhir.Model.Resource)fixture.Store["Observation"][key]).Id = "focus";
        }

        JsonObject page = Page(fixture.Store, "Observation?_id=focus&_sort=value-quantity" +
            "&_include=Observation:performer&_include=Observation:has-member", _ => { }, Grant());
        Assert.Equal(2, (int?)page["total"]);
        AssertPageEntries(fixture.Store, page,
            ("Observation/o0", "match", "focus"), ("Observation/o1", "match", "focus"),
            ("Practitioner/doctor", "include", "shared"), ("Practitioner/logical", "include", "shared"));
    }

    [Theory]
    [MemberData(nameof(Versions))]
    public void ReverseIncludesUseBothStoreKeys(FhirReleases.FhirSequenceCodes version)
    {
        using LifecycleTestStore fixture = new(version);
        SeedObservations(fixture);
        foreach (string key in new[] { "o0", "o1" })
        {
            ((Hl7.Fhir.Model.Resource)fixture.Store["Observation"][key]).Id = "focus";
        }
        ((Hl7.Fhir.Model.Resource)fixture.Store["Observation"]["o2"]).Id = "referrer";
        List<string> events = [];
        JsonObject page = Page(fixture.Store, "Observation?_id=focus&_sort=value-quantity&_revinclude=Observation:has-member", boundary =>
        {
            events.Add(boundary);
            if (boundary == "selected")
            {
                // The first key fails its recheck. Only o1 may drive reverse expansion.
                fixture.Put(JsonNode.Parse("""
                    {"resourceType":"Observation","id":"o0","status":"final","code":{"text":"replacement"},
                     "subject":{"reference":"Patient/b"}}
                    """)!.AsObject());
            }
        }, Grant());
        Assert.Equal(2, (int?)page["total"]);
        Assert.Equal(new[] { "selected", "detached", "detached", "expanded" }, events);
        AssertPageEntries(fixture.Store, page, ("Observation/o1", "match", "focus"),
            ("Observation/o2", "include", "referrer"), ("Observation/o3", "include", "o3"),
            ("Observation/o4", "include", "o4"));
    }

    private static void AssertPageEntries(IFhirStore store, JsonObject page, params (string Path, string Mode, string Id)[] expected)
    {
        Assert.Equal(expected.Select(e => ($"{store.Config.BaseUrl}/{e.Path}", e.Mode, e.Id)).Order(),
            page["entry"]!.AsArray().Select(e => ((string)e!["fullUrl"]!, (string)e["search"]!["mode"]!,
                (string)e["resource"]!["id"]!)).Order());
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
