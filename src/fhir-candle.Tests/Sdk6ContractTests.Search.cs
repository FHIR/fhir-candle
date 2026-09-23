// <copyright file="Sdk6ContractTests.Search.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

extern alias candleR4;
extern alias candleR4B;
extern alias candleR5;

using System.Globalization;
using System.Net;
using System.Text.Json;
using fhir.candle.Tests.Extensions;
using fhir.candle.Tests.Models;
using FhirCandle.Models;
using FhirCandle.Storage;
using FhirCandle.Utils;

namespace fhir.candle.Tests;

public partial class Sdk6ContractTests
{
    private const string SearchEvidence =
        "R4Tests.cs/R4BTests.cs/R5Tests.cs: PatientSearch, ObservationSearch, PatientCompartmentSearch, " +
        "PatientSortOrder, ObservationSortOrder; R5TestsEncounter; FhirStoreTestsR4/R4B/R5 SearchParameter " +
        "create/capability tests; CompartmentTests.cs: AuthCompartmentTests; StrictSearchHandlingTests.cs; " +
        "docs/user/strict-mode.md; FhirSortComparer.Compare missing-value branches; " +
        "ParsedSearchParameter._allResourceParameters; StoreTerminologyService.VsContains; " +
        "data/sdk6-compatibility/README.md";

    private static readonly string[] _searchFamilies =
        ["string", "token", "number", "quantity", "date", "reference", "uri", "composite"];
    private static readonly string[] _searchReferenceKinds =
        ["forward-chain", "nested-chain", "reverse-chain", "include", "revinclude", "membership"];
    private static readonly string[] _searchSortKinds =
        ["birthdate", "birthdate-secondary", "date", "date-secondary", "status", "status-secondary", "_lastUpdated"];
    private static readonly string[] _searchStateKinds = ["search-parameter", "compartment"];
    private static readonly string[] _searchPatientIds = ["Patient/alpha", "Patient/beta", "Patient/gamma", "Patient/missing"];
    private static readonly string[] _searchObservationIds = ["Observation/early", "Observation/late", "Observation/missing", "Observation/twin"];

    /// <summary>Each search case declares its version, transport, and independently authored format.</summary>
    public static IEnumerable<object[]> SearchCases =>
        ReplayCases.Select(row => new object[] { $"search / {row[1]} / {row[2]} / {row[3]}", row[1], row[2], row[3] });

    /// <summary>A failure in one native value family cannot hide the other families.</summary>
    public static IEnumerable<object[]> SearchFamilyCases => SearchRows(_searchFamilies);

    /// <summary>Resolution, inclusion, and terminology use separately isolated sibling-tenant rows.</summary>
    public static IEnumerable<object[]> SearchReferenceCases => SearchRows(_searchReferenceKinds);

    /// <summary>Both directions and each primary/secondary-key contract run independently.</summary>
    public static IEnumerable<object[]> SearchSortCases
    {
        get
        {
            foreach (object[] row in SearchRows(_searchSortKinds))
            {
                foreach (bool descending in new[] { false, true })
                {
                    yield return [$"{row[0]} / {(descending ? "descending" : "ascending")}", row[1], row[2], row[3], row[4], descending];
                }
            }
        }
    }

    /// <summary>Definition lifecycles have independent rows, rather than sharing a mutable store fixture.</summary>
    public static IEnumerable<object[]> SearchStateCases => SearchRows(_searchStateKinds);

    /// <summary>Each reverse-chain predicate is observed independently before repairing the existing branch.</summary>
    public static IEnumerable<object[]> SearchReverseChainCases => SearchRows(
        ["zero", "one", "one-target-many", "multiple-targets", "distinct-names", "distinct-names-reversed",
         "same-key", "same-key-reversed", "independent-related", "independent-related-reversed",
         "nested-filters", "nested-filters-reversed"]);

    /// <summary>All four existing POST routes have independent repeated, encoded, merged, and empty controls.</summary>
    public static IEnumerable<object[]> SearchFormCases
    {
        get
        {
            foreach (object[] row in SearchRows(["type", "system", "compartment", "compartment-type"]))
            {
                foreach (string kind in new[] { "repeated", "encoding", "merge", "empty" })
                {
                    yield return [$"{row[0]} / {kind}", row[1], row[2], row[3], row[4], kind];
                }
            }
        }
    }

    /// <summary>Primitive and complex presence guards do not stop behind another value family's failure.</summary>
    public static IEnumerable<object[]> SearchPresenceCases => SearchRows(
        ["human-name", "quantity", "reference", "primitive-extension", "primitive-false", "primitive-zero", "multi-value"]);

    /// <summary>Forwarded roots are an HTTP contract, not a synthesized direct response.</summary>
    public static IEnumerable<object[]> SearchInclusionCases
    {
        get
        {
            foreach (object[] row in SearchRows(["include", "revinclude"]))
            {
                yield return [$"{row[0]} / configured-root", row[1], row[2], row[3], row[4], false];
                if ((CompatibilityTransport)row[2] == CompatibilityTransport.Http)
                {
                    yield return [$"{row[0]} / forwarded-root", row[1], row[2], row[3], row[4], true];
                }
            }
        }
    }

    /// <summary>Reference spellings and each synthesized-reference consumer have separately observable rows.</summary>
    public static IEnumerable<object[]> SearchLocalReferenceCases => SearchRows(
        ["relative", "absolute", "foreign-authority", "foreign-tenant", "prefix-lookalike", "prefix-descendant",
         "urn", "oid", "typed", "canonical", "reverse-chain", "revinclude", "compartment", "compartment-type"]);

    /// <summary>Only the direct generic dispatcher has this explicitly unsupported boundary.</summary>
    public static IEnumerable<object[]> SearchGenericCompartmentCases =>
        SearchCases.Where(row => (CompatibilityTransport)row[2] == CompatibilityTransport.Direct);

    /// <summary>Both tenant defaults are tested with and without explicit request overrides.</summary>
    public static IEnumerable<object[]> SearchHandlingCases
    {
        get
        {
            foreach (object[] row in SearchCases)
            {
                foreach (bool strict in new[] { false, true })
                {
                    yield return [$"{row[0]} / {(strict ? "strict" : "default")}", row[1], row[2], row[3], strict];
                }
            }
        }
    }

    /// <summary>Positive, negative, and absent-value searches assert identities, modes, URLs, and complete content.</summary>
    [Theory]
    [MemberData(nameof(SearchFamilyCases))]
    public async Task SearchFamiliesReturnExpectedIds(
        string label,
        FhirReleases.FhirSequenceCodes version,
        CompatibilityTransport transport,
        CompatibilityFormat format,
        string family)
    {
        Assert.NotEmpty(label);
        CompatibilityTenant tenant = Tenant(version, $"families-{VersionName(version)}");
        SearchGraph graph = LoadSearchGraph(tenant, format);
        await using CompatibilityReplay replay = new(SearchScenario($"families-{family}", graph), transport);
        await replay.RunAsync();

        SearchProbe[] probes = SearchFamilyProbes(tenant, family);
        Assert.NotEmpty(probes);
        foreach (SearchProbe probe in probes)
        {
            await AssertSearchProbe(replay, graph, probe);
        }
    }

    /// <summary>Overlapping logical IDs must resolve to this tenant's resources and this tenant's ValueSet contents.</summary>
    [Theory]
    [MemberData(nameof(SearchReferenceCases))]
    public async Task ReferenceChainsAndIncludesStayTenantLocal(
        string label,
        FhirReleases.FhirSequenceCodes version,
        CompatibilityTransport transport,
        CompatibilityFormat format,
        string kind)
    {
        Assert.NotEmpty(label);
        SearchGraph oak = LoadSearchGraph(Tenant(version, $"search-oak-{VersionName(version)}"), format);
        SearchGraph elm = LoadSearchGraph(Tenant(version, $"search-elm-{VersionName(version)}"), format, sibling: true);
        await using CompatibilityReplay replay = new(SearchScenario($"references-{kind}", oak, elm), transport);
        await replay.RunAsync();

        // Revisit oak after elm has used the same expressions, logical IDs, and canonical ValueSet URL.
        foreach ((SearchGraph graph, bool sibling, string pass) in new[]
        {
            (oak, false, "oak-first"), (elm, true, "elm"), (oak, false, "oak-again"),
        })
        {
            foreach (SearchProbe probe in SearchReferenceProbes(graph.Tenant, kind, sibling))
            {
                await AssertSearchProbe(replay, graph, probe with { Id = pass + "-" + probe.Id });
            }
        }
    }

    /// <summary>Membership is checked before order; insertion order and equal primary values cannot satisfy this oracle.</summary>
    [Theory]
    [MemberData(nameof(SearchSortCases))]
    public async Task SortReturnsExplicitIdentityOrder(
        string label,
        FhirReleases.FhirSequenceCodes version,
        CompatibilityTransport transport,
        CompatibilityFormat format,
        string kind,
        bool descending)
    {
        Assert.NotEmpty(label);
        SearchGraph graph = LoadSearchGraph(Tenant(version, $"sort-{VersionName(version)}"), format);
        await using CompatibilityReplay replay = new(SearchScenario($"sort-{kind}-{descending}", graph), transport);
        await replay.RunAsync();

        if (kind == "_lastUpdated")
        {
            DateTimeOffset previous = DateTimeOffset.MinValue;
            // Expected order comes from these ordinary updates, not sorting the returned timestamps.
            foreach (string identity in new[] { "Observation/twin", "Observation/late", "Observation/missing", "Observation/early" })
            {
                await Task.Delay(TimeSpan.FromMilliseconds(25));
                GeneratedResourceExpectation updated = await UpdateSearchResource(replay, graph, identity);
                DateTimeOffset instant = replay.Bindings.Resources[updated.Name].Instant;
                Assert.True(instant > previous, "The controlled update sequence must establish strictly increasing timestamps.");
                previous = instant;
            }
        }

        SearchProbe probe = SearchSortProbe(kind, descending);
        await AssertSearchProbe(replay, graph, probe, ordered: true);
    }

    /// <summary>Create/update/delete definitions through the ordinary interaction boundary and observe fresh dependent state.</summary>
    [Theory]
    [MemberData(nameof(SearchStateCases))]
    public async Task DynamicSearchParameterAndCompartmentStateIsFresh(
        string label,
        FhirReleases.FhirSequenceCodes version,
        CompatibilityTransport transport,
        CompatibilityFormat format,
        string kind)
    {
        Assert.NotEmpty(label);
        SearchGraph oak = LoadSearchGraph(Tenant(version, $"state-oak-{VersionName(version)}"), format);
        SearchGraph elm = LoadSearchGraph(Tenant(version, $"state-elm-{VersionName(version)}"), format, sibling: true);
        await using CompatibilityReplay replay = new(SearchScenario($"state-{kind}", oak, elm), transport);
        await replay.RunAsync();

        if (kind == "search-parameter")
        {
            await AssertSearchParameterLifecycle(replay, oak, elm);
        }
        else
        {
            Assert.Equal("compartment", kind);
            await AssertSearchCompartmentLifecycle(replay, oak, elm);
        }
    }

    /// <summary>Reverse chains are existential, while separate occurrences retain their own complete filter and remain ANDed.</summary>
    [Theory]
    [MemberData(nameof(SearchReverseChainCases))]
    public async Task ReverseChainUsesExistentialMatchesAndDistinctFilters(
        string label,
        FhirReleases.FhirSequenceCodes version,
        CompatibilityTransport transport,
        CompatibilityFormat format,
        string kind)
    {
        Assert.NotEmpty(label);
        SearchGraph oak = ReverseChainControlGraph(Tenant(version, $"has-oak-{VersionName(version)}"), format, false);
        SearchGraph elm = ReverseChainControlGraph(Tenant(version, $"has-elm-{VersionName(version)}"), format, true);
        await using CompatibilityReplay replay = new(SearchScenario($"reverse-chain-control-{kind}", oak, elm), transport);
        await replay.RunAsync();

        // All references here are relative: M1's baseline does not depend on the P4 comparison repair.
        foreach ((SearchGraph graph, string target, string pass) in new[]
        {
            (oak, "Patient/alpha", "oak-first"), (elm, "Patient/beta", "elm"), (oak, "Patient/alpha", "oak-again"),
        })
        {
            const string has = "_has:Observation:subject:";
            SearchProbe probe = kind switch
            {
                "zero" => new(kind, $"Patient?{has}_id=not-present", []),
                "one" => new(kind, $"Patient?{has}_id=early", [target]),
                "one-target-many" => new(kind, $"Patient?{has}_id=early,twin", [target]),
                "multiple-targets" => new(kind, $"Patient?{has}status=final", ["Patient/alpha", "Patient/beta"]),
                "distinct-names" => new(kind, $"Patient?{has}status=final&{has}code=red", [target]),
                "distinct-names-reversed" => new(kind, $"Patient?{has}code=red&{has}status=final", [target]),
                "same-key" => new(kind, $"Patient?{has}_id=early&{has}_id=late", []),
                "same-key-reversed" => new(kind, $"Patient?{has}_id=late&{has}_id=early", []),
                "independent-related" => new(kind, $"Patient?{has}_id=early&{has}_id=twin", [target]),
                "independent-related-reversed" => new(kind, $"Patient?{has}_id=twin&{has}_id=early", [target]),
                "nested-filters" => new(kind, $"Patient?{has}subject.family=Alpha&{has}subject.family=Beta", []),
                "nested-filters-reversed" => new(kind, $"Patient?{has}subject.family=Beta&{has}subject.family=Alpha", []),
                _ => throw new ArgumentOutOfRangeException(nameof(kind)),
            };
            await AssertSearchProbe(replay, graph, probe with { Id = pass + "-" + probe.Id });
        }
    }

    /// <summary>Parsed form values retain occurrence boundaries and encoding, including a query appended after the body.</summary>
    [Theory]
    [MemberData(nameof(SearchFormCases))]
    public async Task PostSearchPreservesRepeatedParametersAndEncoding(
        string label,
        FhirReleases.FhirSequenceCodes version,
        CompatibilityTransport transport,
        CompatibilityFormat format,
        string route,
        string kind)
    {
        Assert.NotEmpty(label);
        SearchGraph graph = FormSearchControlGraph(Tenant(version, $"form-{VersionName(version)}"), format);
        await using CompatibilityReplay replay = new(SearchScenario($"form-control-{route}-{kind}", graph), transport);
        await replay.RunAsync();
        string path = route switch
        {
            "type" => "Observation",
            "system" => string.Empty,
            "compartment" => "Patient/alpha/*",
            "compartment-type" => "Patient/alpha/Observation",
            _ => throw new ArgumentOutOfRangeException(nameof(route)),
        };
        string[] both = ["Observation/early", "Observation/twin"];
        const string tag = "https://example.test/forms|A&B+C%=Québec";
        switch (kind)
        {
            case "repeated":
                await Check("single", [("_id", "early")], [], ["Observation/early"], "_id=early");
                await Check("comma-or", [("_id", "early,twin")], [], both, "_id=early,twin");
                await Check("repeated-and", [("_id", "early"), ("_id", "twin")], [], [], "_id=early&_id=twin");
                break;
            case "encoding":
                await Check("value-delimiters-unicode", [("_tag", tag)], [], ["Observation/early"], "_tag=" + tag);
                await Check("encoded-modifier", [("_tag:not", tag)], [], ["Observation/twin"], "_tag:not=" + tag);
                await Check("encoded-key-no-injection", [("ignored&_id", "early"), ("_id", "twin")], [],
                    ["Observation/twin"], "_id=twin");
                await Check("encoded-key-delimiters-unicode", [("ignored&+%=Québec", "unused"), ("_id", "early")], [],
                    ["Observation/early"], "_id=early");
                break;
            case "merge":
                await Check("body-before-query", [("_id", "early")], [("_id", "twin")], [], "_id=early&_id=twin");
                await Check("body-before-query-reversed", [("_id", "twin")], [("_id", "early")], [], "_id=twin&_id=early");
                await Check("body-or-query-and", [("_id", "early,twin")], [("_id", "twin")],
                    ["Observation/twin"], "_id=early,twin&_id=twin");
                await Check("encoded-body-with-query", [("_tag", tag)], [("_id", "early")],
                    ["Observation/early"], "_tag=" + tag + "&_id=early");
                break;
            case "empty":
                await Check("empty-form", [], [], both, string.Empty);
                await Check("empty-value", [("_id", string.Empty)], [], both, string.Empty);
                await Check("empty-form-query-only", [], [("_id", "twin")], ["Observation/twin"], "_id=twin");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind));
        }

        async Task Check(
            string id,
            (string Key, string Value)[] bodyPairs,
            (string Key, string Value)[] queryPairs,
            string[] matches,
            string applied)
        {
            if (route == "system")
            {
                // Keep a truly empty form empty; system searches always declare their supported type.
                if (bodyPairs.Length == 0)
                {
                    queryPairs = [("_type", "Observation"), .. queryPairs];
                }
                else
                {
                    bodyPairs = [("_type", "Observation"), .. bodyPairs];
                }

                // Preserve the existing system self-link projection (one literal per parameter name).
                // Membership, unlike that projection, must apply every repeated occurrence.
                string[] appliedParts = applied.Length == 0 ? [] : applied.Split('&');
                if ((kind is "repeated" or "merge") && applied.StartsWith("_id=", StringComparison.Ordinal))
                {
                    applied = appliedParts[0];
                }
                applied = "_type=Observation" + (applied.Length == 0 ? string.Empty : "&" + applied);
            }
            string appliedPath = path + (applied.Length == 0 ? string.Empty : "?" + applied);
            SearchProbe probe = new($"{route}-{kind}-{id}", path, matches) { AppliedPath = appliedPath };
            string combined = await EncodeSearchPairs([.. bodyPairs, .. queryPairs]);
            await AssertSearchRequest(replay, graph, probe, new CompatibilityRequest(graph.Tenant.Name, "GET",
                path + (combined.Length == 0 ? string.Empty : "?" + combined)) { Format = format });

            string postPath = path.Length == 0 ? "_search"
                : (path.EndsWith("/*", StringComparison.Ordinal) ? path[..^2] : path) + "/_search";
            string body = await EncodeSearchPairs(bodyPairs);
            string query = await EncodeSearchPairs(queryPairs);
            if (transport == CompatibilityTransport.Direct && bodyPairs.Length != 0)
            {
                // Direct search consumes the complete SourceContent; URL/body merging is controller-only.
                body = combined;
                query = string.Empty;
            }
            await AssertSearchRequest(replay, graph, probe, new CompatibilityRequest(graph.Tenant.Name, "POST",
                postPath + (query.Length == 0 ? string.Empty : "?" + query))
            {
                Format = format, SourceMediaType = "application/x-www-form-urlencoded", Content = body,
            });
        }
    }

    /// <summary>Complex nodes, false/zero scalars, and a populated member are present; extension-only primitives are absent.</summary>
    [Theory]
    [MemberData(nameof(SearchPresenceCases))]
    public async Task MissingSearchDistinguishesComplexValuesAndPrimitiveExtensions(
        string label,
        FhirReleases.FhirSequenceCodes version,
        CompatibilityTransport transport,
        CompatibilityFormat format,
        string kind)
    {
        Assert.NotEmpty(label);
        SearchGraph graph = PresenceSearchControlGraph(Tenant(version, $"presence-{VersionName(version)}"), format);
        await using CompatibilityReplay replay = new(SearchScenario($"presence-control-{kind}", graph), transport);
        await replay.RunAsync();
        string[] absentPatients = ["Patient/empty", "Patient/extensions", "Patient/mixed"];
        (string path, string[] present, string[] absent) = kind switch
        {
            "human-name" => ("Patient?_id=populated,mixed,empty&name", new[] { "Patient/populated", "Patient/mixed" }, new[] { "Patient/empty" }),
            "quantity" => ("Observation?value-quantity", new[] { "Observation/populated" }, new[] { "Observation/empty" }),
            "reference" => ("Observation?subject", new[] { "Observation/populated" }, new[] { "Observation/empty" }),
            "primitive-extension" => ("Patient?birthdate", new[] { "Patient/populated" }, absentPatients),
            "primitive-false" => ("Patient?active", new[] { "Patient/populated" }, absentPatients),
            "primitive-zero" => ("Patient?multiplebirth", new[] { "Patient/populated" }, absentPatients),
            "multi-value" => ("Patient?compat-given", new[] { "Patient/populated", "Patient/mixed" },
                new[] { "Patient/empty", "Patient/extensions" }),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        await AssertSearchProbe(replay, graph, new(kind + "-missing", path + ":missing=true", absent));
        await AssertSearchProbe(replay, graph, new(kind + "-present", path + ":missing=false", present));
    }

    /// <summary>Both inclusion loops preserve identities, modes, deduplication, totals, full content, and the supplied root.</summary>
    [Theory]
    [MemberData(nameof(SearchInclusionCases))]
    public async Task IncludedFullUrlsIdentifyReturnedResources(
        string label,
        FhirReleases.FhirSequenceCodes version,
        CompatibilityTransport transport,
        CompatibilityFormat format,
        string kind,
        bool forwarded)
    {
        Assert.NotEmpty(label);
        SearchGraph graph = LoadSearchGraph(Tenant(version, $"included-{VersionName(version)}"), format);
        await using CompatibilityReplay replay = new(SearchScenario($"inclusion-control-{kind}-{forwarded}", graph), transport);
        await replay.RunAsync();
        string? root = forwarded ? $"https://forwarded.example.test/proxy/fhir/{graph.Tenant.Name}" : null;
        IReadOnlyDictionary<string, string>? headers = forwarded ? new Dictionary<string, string>
        {
            ["X-Forwarded-Proto"] = "https",
            ["X-Forwarded-Host"] = "forwarded.example.test",
            ["X-Forwarded-Prefix"] = "/proxy/fhir/" + graph.Tenant.Name,
        } : null;
        foreach (SearchProbe probe in SearchReferenceProbes(graph.Tenant, kind, false))
        {
            await AssertSearchProbe(replay, graph, probe, headers: headers, responseRoot: root);
        }
    }

    /// <summary>Only this store's base is equivalent to a typed relative reference; foreign references remain exact identities.</summary>
    [Theory]
    [MemberData(nameof(SearchLocalReferenceCases))]
    public async Task LocalReferenceEquivalenceRejectsForeignBases(
        string label,
        FhirReleases.FhirSequenceCodes version,
        CompatibilityTransport transport,
        CompatibilityFormat format,
        string kind)
    {
        Assert.NotEmpty(label);
        CompatibilityTenant oakTenant = Tenant(version, $"refs-oak-{VersionName(version)}");
        CompatibilityTenant elmTenant = Tenant(version, $"refs-elm-{VersionName(version)}");
        SearchGraph oak = LocalReferenceControlGraph(oakTenant, elmTenant, format);
        SearchGraph elm = LocalReferenceControlGraph(elmTenant, oakTenant, format);
        await using CompatibilityReplay replay = new(SearchScenario($"local-reference-control-{kind}", oak, elm), transport);
        await replay.RunAsync();
        foreach ((SearchGraph graph, CompatibilityTenant sibling, string pass) in new[]
        {
            (oak, elmTenant, "oak-first"), (elm, oakTenant, "elm"), (oak, elmTenant, "oak-again"),
        })
        {
            string[] local = ["Observation/relative", "Observation/absolute"];
            SearchProbe[] probes = kind switch
            {
                "relative" => [new(kind, "Observation?subject=Patient/alpha", local)],
                "absolute" => [new(kind, $"Observation?subject={graph.Tenant.BaseUrl}/Patient/alpha", local)],
                "typed" =>
                [
                    new("typed-local", "Observation?subject:Patient=Patient/alpha", local),
                    new("typed-other", "Observation?subject:Group=Group/alpha", ["Observation/typed-other"]),
                    new("typed-negative", "Observation?subject:Group=Patient/alpha", []),
                ],
                "urn" => [new(kind, "Observation?subject=urn:uuid:11111111-2222-4333-8444-555555555555", ["Observation/urn"])],
                "oid" => [new(kind, "Observation?subject=urn:oid:1.2.3.4.5", ["Observation/oid"])],
                "canonical" =>
                [
                    new("canonical-version", "Observation?_profile=https://example.test/profiles/local|1.0", ["Observation/relative"]),
                    new("canonical-negative", "Observation?_profile=https://example.test/profiles/local|2.0", []),
                ],
                "revinclude" => [new(kind, "Patient?_id=alpha&_revinclude=Observation:subject", ["Patient/alpha"]) { Includes = local }],
                "compartment" => [new(kind, "Patient/alpha/*", local)],
                "compartment-type" => [new(kind, "Patient/alpha/Observation", local)],
                "reverse-chain" =>
                [
                    new("reverse-relative", "Patient?_has:Observation:subject:_id=relative", ["Patient/alpha"]),
                    new("reverse-absolute", "Patient?_has:Observation:subject:_id=absolute", ["Patient/alpha"]),
                    new("reverse-foreign-authority", "Patient?_has:Observation:subject:_id=foreign-authority", []),
                    new("reverse-foreign-tenant", "Patient?_has:Observation:subject:_id=foreign-tenant", []),
                    new("reverse-prefix-lookalike", "Patient?_has:Observation:subject:_id=prefix-lookalike", []),
                    new("reverse-prefix-descendant", "Patient?_has:Observation:subject:_id=prefix-descendant", []),
                ],
                _ =>
                [
                    new(kind + "-exact", "Observation?subject=" + ForeignSearchReference(graph.Tenant, sibling, kind),
                        ["Observation/" + kind]),
                    new(kind + "-relative-negative", $"Observation?_id={kind}&subject=Patient/alpha", []),
                    new(kind + "-absolute-negative", $"Observation?_id={kind}&subject={graph.Tenant.BaseUrl}/Patient/alpha", []),
                ],
            };
            foreach (SearchProbe probe in probes)
            {
                await AssertSearchProbe(replay, graph, probe with { Id = pass + "-" + probe.Id });
            }
        }
    }

    /// <summary>Replay selects the supported dedicated methods before dispatch and preserves the complete existing lifecycle.</summary>
    [Theory]
    [MemberData(nameof(SearchCases))]
    public async Task CompartmentReplayUsesExistingMethods(
        string label,
        FhirReleases.FhirSequenceCodes version,
        CompatibilityTransport transport,
        CompatibilityFormat format)
    {
        Assert.NotEmpty(label);
        SearchGraph oak = LoadSearchGraph(Tenant(version, $"boundary-oak-{VersionName(version)}"), format);
        SearchGraph elm = LoadSearchGraph(Tenant(version, $"boundary-elm-{VersionName(version)}"), format, sibling: true);
        await using CompatibilityReplay replay = new(SearchScenario("dedicated-compartment-boundary", oak, elm), transport);
        await replay.RunAsync();
        await AssertSearchCompartmentLifecycle(replay, oak, elm);
    }

    /// <summary>The adapter correction must not silently create a production generic-dispatch capability.</summary>
    [Theory]
    [MemberData(nameof(SearchGenericCompartmentCases))]
    public void GenericCompartmentDispatchRemainsUnsupported(
        string label,
        FhirReleases.FhirSequenceCodes version,
        CompatibilityTransport transport,
        CompatibilityFormat format)
    {
        Assert.NotEmpty(label);
        Assert.Equal(CompatibilityTransport.Direct, transport);
        CompatibilityTenant tenant = Tenant(version, $"generic-compartment-{VersionName(version)}");
        using IFhirStore store = version switch
        {
            FhirReleases.FhirSequenceCodes.R4 => new candleR4::FhirCandle.Storage.VersionedFhirStore(),
            FhirReleases.FhirSequenceCodes.R4B => new candleR4B::FhirCandle.Storage.VersionedFhirStore(),
            FhirReleases.FhirSequenceCodes.R5 => new candleR5::FhirCandle.Storage.VersionedFhirStore(),
            _ => throw new ArgumentOutOfRangeException(nameof(version)),
        };
        store.Init(new TenantConfiguration
        {
            FhirVersion = version, ControllerName = tenant.Name, BaseUrl = tenant.BaseUrl,
            AllowExistingId = true, AllowCreateAsUpdate = true, LoadDirectory = null,
        });
        foreach ((string method, string path, Common.StoreInteractionCodes interaction) in new[]
        {
            ("GET", "Patient/alpha/*", Common.StoreInteractionCodes.CompartmentSearch),
            ("POST", "Patient/alpha/_search", Common.StoreInteractionCodes.CompartmentSearch),
            ("GET", "Patient/alpha/Observation", Common.StoreInteractionCodes.CompartmentTypeSearch),
            ("POST", "Patient/alpha/Observation/_search", Common.StoreInteractionCodes.CompartmentTypeSearch),
        })
        {
            FhirRequestContext context = new()
            {
                TenantName = tenant.Name, Store = store, HttpMethod = method, Url = tenant.BaseUrl + "/" + path,
                Authorization = null, SourceFormat = "application/x-www-form-urlencoded",
                SourceContent = method == "POST" ? "_id=early" : string.Empty,
                DestinationFormat = WireContractAssertions.MediaType(format),
            };
            string diagnostic = $"{version} / {format} / {transport} / tenant {tenant.Name} / generic-unsupported / {method} {context.Url}";
            Assert.Equal(interaction, context.Interaction);
            bool success = store.PerformInteraction(context, out FhirResponseContext response, serializeReturn: true);
            Assert.False(success, diagnostic + ": expected unsuccessful generic dispatch");
            Assert.True(response.StatusCode == HttpStatusCode.NotImplemented,
                $"{diagnostic}: expected 501, actual {response.StatusCode}");
            Assert.Null(response.Resource);
            Assert.NotNull(response.Outcome);
            Assert.Empty(response.SerializedResource);
            Assert.Empty(response.SerializedOutcome);
        }
    }

    /// <summary>Strict failures retain all issue semantics; lenient requests still return the independently expected Patient.</summary>
    [Theory]
    [MemberData(nameof(SearchHandlingCases))]
    public async Task StrictAndPreferHandlingPreserveOutcomes(
        string label,
        FhirReleases.FhirSequenceCodes version,
        CompatibilityTransport transport,
        CompatibilityFormat format,
        bool strict)
    {
        Assert.NotEmpty(label);
        CompatibilityTenant tenant = Tenant(version, $"handling-{VersionName(version)}") with { Strict = strict };
        CompatibilityScenario scenario = new($"handling-{strict}", SearchEvidence, [tenant], [MetadataStep(tenant, format)]);
        await using CompatibilityReplay replay = new(scenario, transport);
        await replay.RunAsync();

        SearchGraph graph = new(tenant, format, new(StringComparer.Ordinal), new(StringComparer.Ordinal));
        foreach ((string family, string birthDate) in new[] { ("Alpha", "1980-02-03"), ("Beta", "1990-04-05") })
        {
            string content = format == CompatibilityFormat.Json
                ? $$$"""{"resourceType":"Patient","name":[{"family":"{{{family}}}"}],"birthDate":"{{{birthDate}}}"}"""
                : $"""<Patient xmlns="http://hl7.org/fhir"><name><family value="{family}"/></name><birthDate value="{birthDate}"/></Patient>""";
            GeneratedResourceExpectation generated = new($"handling-{family}", "Patient", "1");
            await replay.StepAsync(new CompatibilityStep($"create-{family}",
                new CompatibilityRequest(tenant.Name, "POST", "Patient") { Format = format, Content = content },
                ResourceResponse($"create-{family}", content, generated, HttpStatusCode.Created)));
            string identity = "Patient/" + replay.Bindings.Resources[generated.Name].Id;
            graph.Resources.Add(identity, content);
            graph.Generated.Add(identity, generated);
        }

        string alpha = "Patient/" + replay.Bindings.Resources["handling-Alpha"].Id;
        string spec = $"http://hl7.org/fhir/{version}/search.html#errors";
        SearchFailureProbe[] failures =
        [
            new("unknown", "Patient?name=Alpha&bogus=1", "Patient?name=Alpha",
                [new("error", "not-supported", $"Unknown search parameter 'bogus' for this resource type. (see {spec})")]),
            new("multiple-unknown", "Patient?name=Alpha&bogus=1&bogus2=2", "Patient?name=Alpha",
                [
                    new("error", "not-supported", $"Unknown search parameter 'bogus' for this resource type. (see {spec})"),
                    new("error", "not-supported", $"Unknown search parameter 'bogus2' for this resource type. (see {spec})"),
                ]),
            new("malformed", "Patient?name=Alpha&birthdate=not-a-date", "Patient?name=Alpha",
                [new("error", "invalid", $"Search parameter 'birthdate' was rejected as malformed (modifier, chain, or value parse failure). (see {spec})")]),
            new("partially-malformed", "Patient?birthdate=1980-02-03,not-a-date", "Patient?birthdate=1980-02-03,not-a-date",
                [new("error", "invalid", $"Search parameter 'birthdate' value 'not-a-date' was rejected as malformed. (see {spec})")]),
        ];

        foreach ((string name, string? prefer, bool rejects) in new (string, string?, bool)[]
        {
            ("default", null, strict),
            ("explicit-lenient", "handling=lenient", false),
            ("explicit-strict", "handling=strict", true),
            ("last-lenient", "handling=strict, handling=lenient", false),
            ("last-strict", "handling=lenient, handling=strict", true),
        })
        {
            IReadOnlyDictionary<string, string> headers = prefer is null
                ? new Dictionary<string, string>()
                : new Dictionary<string, string> { ["Prefer"] = prefer };
            await AssertSearchProbe(replay, graph, new(name + "-valid", "Patient?name=Alpha", [alpha]), headers: headers);
            foreach (SearchFailureProbe failure in failures)
            {
                string stepId = name + "-" + failure.Id;
                if (rejects)
                {
                    await AssertSearchFailure(replay, graph, stepId, failure.Path, HttpStatusCode.BadRequest, failure.Issues, headers);
                }
                else
                {
                    await AssertSearchProbe(replay, graph,
                        new SearchProbe(stepId, failure.Path, [alpha]) { AppliedPath = failure.AppliedPath }, headers: headers);
                }
            }
        }

        // Failed and lenient searches are read-only, including the bound generated metadata.
        await AssertSearchProbe(replay, graph,
            new("unchanged-patients", "Patient", [alpha, "Patient/" + replay.Bindings.Resources["handling-Beta"].Id]));
    }

    /// <summary>Corpus/row inventory cannot silently lose a version, boundary, format, or behavior group.</summary>
    [Fact]
    public void SearchTheoryDataIsNonEmptyAndLabeled()
    {
        Assert.Equal(12, SearchCases.Count());
        Assert.Equal(96, SearchFamilyCases.Count());
        Assert.Equal(72, SearchReferenceCases.Count());
        Assert.Equal(168, SearchSortCases.Count());
        Assert.Equal(24, SearchStateCases.Count());
        Assert.Equal(24, SearchHandlingCases.Count());
        object[][] rows = SearchFamilyCases.Concat(SearchReferenceCases).Concat(SearchSortCases)
            .Concat(SearchStateCases).Concat(SearchHandlingCases).ToArray();
        Assert.Equal(rows.Length, rows.Select(row => (string)row[0]).Distinct(StringComparer.Ordinal).Count());
        Assert.All(rows, row =>
        {
            Assert.Contains(row[1].ToString()!, (string)row[0], StringComparison.Ordinal);
            Assert.Contains(row[2].ToString()!, (string)row[0], StringComparison.Ordinal);
            Assert.Contains(row[3].ToString()!, (string)row[0], StringComparison.Ordinal);
        });

        foreach (FhirReleases.FhirSequenceCodes version in _versions)
        {
            foreach (CompatibilityFormat format in Enum.GetValues<CompatibilityFormat>())
            {
                foreach (bool sibling in new[] { false, true })
                {
                    SearchGraph graph = LoadSearchGraph(Tenant(version, $"inventory-{VersionName(version)}"), format, sibling);
                    Assert.Equal(14, graph.Resources.Count);
                    foreach (KeyValuePair<string, string> resource in graph.Resources)
                    {
                        Assert.Equal(resource.Key, SearchIdentity(resource.Value, format));
                    }
                }
            }
        }
    }

    /// <summary>The recovery guards are additions, not replacements for the original 384 replay rows and inventory fact.</summary>
    [Fact]
    public void SearchRecoveryTheoryDataIsNonEmptyAndLabeled()
    {
        Assert.Equal(144, SearchReverseChainCases.Count());
        Assert.Equal(192, SearchFormCases.Count());
        Assert.Equal(84, SearchPresenceCases.Count());
        Assert.Equal(36, SearchInclusionCases.Count());
        Assert.Equal(168, SearchLocalReferenceCases.Count());
        Assert.Equal(12, SearchCases.Count());
        Assert.Equal(6, SearchGenericCompartmentCases.Count());
        foreach (IEnumerable<object[]> cases in new[]
        {
            SearchReverseChainCases, SearchFormCases, SearchPresenceCases, SearchInclusionCases,
            SearchLocalReferenceCases, SearchCases, SearchGenericCompartmentCases,
        })
        {
            object[][] rows = cases.ToArray();
            Assert.NotEmpty(rows);
            Assert.Equal(rows.Length, rows.Select(row => (string)row[0]).Distinct(StringComparer.Ordinal).Count());
            Assert.All(rows, row =>
            {
                Assert.Contains(row[1].ToString()!, (string)row[0], StringComparison.Ordinal);
                Assert.Contains(row[2].ToString()!, (string)row[0], StringComparison.Ordinal);
                Assert.Contains(row[3].ToString()!, (string)row[0], StringComparison.Ordinal);
            });
        }
    }

    private sealed record SearchProbe(string Id, string Path, string[] Matches)
    {
        /// <summary>Gets the independently expected included identities, separate from matches.</summary>
        public string[] Includes { get; init; } = [];

        /// <summary>Gets the applied self path when lenient handling omits a whole parameter.</summary>
        public string? AppliedPath { get; init; }
    }

    private sealed record SearchFailureProbe(string Id, string Path, string AppliedPath, OutcomeIssueExpectation[] Issues);

    private sealed record SearchGraph(
        CompatibilityTenant Tenant,
        CompatibilityFormat Format,
        Dictionary<string, string> Resources,
        Dictionary<string, GeneratedResourceExpectation> Generated);

    private sealed record SearchEntry(string Identity, string Mode, string FullUrl, string Content);

    private sealed record SearchCapabilityParameter(string Name, string Definition, string Type, string Documentation);

    private static IEnumerable<object[]> SearchRows(string[] kinds)
    {
        foreach (object[] row in SearchCases)
        {
            foreach (string kind in kinds)
            {
                yield return [$"{row[0]} / {kind}", row[1], row[2], row[3], kind];
            }
        }
    }

    private static CompatibilityScenario SearchScenario(string id, params SearchGraph[] graphs) =>
        new(id, SearchEvidence, graphs.Select(graph => graph.Tenant).ToArray(),
            graphs.Select(graph => MetadataStep(graph.Tenant, graph.Format)).ToArray())
        {
            Seeds = graphs.SelectMany(graph => graph.Resources.Select(resource =>
            {
                GeneratedResourceExpectation generated = graph.Generated[resource.Key];
                string name = $"seed-{generated.Name}";
                return new CompatibilitySeed(name, graph.Tenant.Name, generated.ResourceType, generated.ExpectedId!, graph.Format,
                    resource.Value, ResourceResponse(name, resource.Value, generated, HttpStatusCode.Created));
            })).ToArray(),
        };

    private static void AddSearchControl(SearchGraph graph, string json, string xml)
    {
        string content = graph.Format == CompatibilityFormat.Json ? json : xml;
        string identity = SearchIdentity(content, graph.Format);
        string[] parts = identity.Split('/');
        graph.Resources.Add(identity, content);
        graph.Generated.Add(identity, new($"{graph.Tenant.Name}/{identity}/v1", parts[0], "1", parts[1]));
    }

    private static async Task<string> EncodeSearchPairs(IEnumerable<(string Key, string Value)> pairs)
    {
        using FormUrlEncodedContent content = new(pairs.Select(pair => new KeyValuePair<string, string>(pair.Key, pair.Value)));
        return await content.ReadAsStringAsync();
    }

    private static SearchGraph FormSearchControlGraph(CompatibilityTenant tenant, CompatibilityFormat format)
    {
        SearchGraph graph = new(tenant, format, new(StringComparer.Ordinal), new(StringComparer.Ordinal));
        AddSearchControl(graph,
            """{"resourceType":"Patient","id":"alpha"}""",
            """<Patient xmlns="http://hl7.org/fhir"><id value="alpha"/></Patient>""");
        AddSearchControl(graph,
            """
            {"resourceType":"Observation","id":"early","meta":{"tag":[{"system":"https://example.test/forms","code":"A&B+C%=Québec"}]},
             "status":"final","code":{"text":"Encoded form control"},"subject":{"reference":"Patient/alpha"}}
            """,
            """
            <Observation xmlns="http://hl7.org/fhir"><id value="early"/><meta><tag><system value="https://example.test/forms"/><code value="A&amp;B+C%=Québec"/></tag></meta>
            <status value="final"/><code><text value="Encoded form control"/></code><subject><reference value="Patient/alpha"/></subject></Observation>
            """);
        AddSearchControl(graph,
            """
            {"resourceType":"Observation","id":"twin","meta":{"tag":[{"system":"https://example.test/forms","code":"plain"}]},
             "status":"final","code":{"text":"Ordinary form control"},"subject":{"reference":"Patient/alpha"}}
            """,
            """
            <Observation xmlns="http://hl7.org/fhir"><id value="twin"/><meta><tag><system value="https://example.test/forms"/><code value="plain"/></tag></meta>
            <status value="final"/><code><text value="Ordinary form control"/></code><subject><reference value="Patient/alpha"/></subject></Observation>
            """);
        return graph;
    }

    private static SearchGraph PresenceSearchControlGraph(CompatibilityTenant tenant, CompatibilityFormat format)
    {
        SearchGraph graph = new(tenant, format, new(StringComparer.Ordinal), new(StringComparer.Ordinal));
        AddSearchControl(graph,
            """{"resourceType":"Patient","id":"empty"}""",
            """<Patient xmlns="http://hl7.org/fhir"><id value="empty"/></Patient>""");
        AddSearchControl(graph,
            """
            {"resourceType":"Patient","id":"extensions",
             "_active":{"extension":[{"url":"https://example.test/presence","valueString":"no scalar"}]},
             "_birthDate":{"extension":[{"url":"https://example.test/presence","valueString":"no scalar"}]},
             "_multipleBirthInteger":{"extension":[{"url":"https://example.test/presence","valueString":"no scalar"}]}}
            """,
            """
            <Patient xmlns="http://hl7.org/fhir"><id value="extensions"/>
            <active><extension url="https://example.test/presence"><valueString value="no scalar"/></extension></active>
            <birthDate><extension url="https://example.test/presence"><valueString value="no scalar"/></extension></birthDate>
            <multipleBirthInteger><extension url="https://example.test/presence"><valueString value="no scalar"/></extension></multipleBirthInteger></Patient>
            """);
        AddSearchControl(graph,
            """
            {"resourceType":"Patient","id":"populated","active":false,"name":[{"family":"Present","given":["Bea"]}],
             "birthDate":"1980-02-03","multipleBirthInteger":0}
            """,
            """
            <Patient xmlns="http://hl7.org/fhir"><id value="populated"/><active value="false"/><name><family value="Present"/><given value="Bea"/></name>
            <birthDate value="1980-02-03"/><multipleBirthInteger value="0"/></Patient>
            """);
        AddSearchControl(graph,
            """
            {"resourceType":"Patient","id":"mixed","name":[{"given":[null,"Ada"],
             "_given":[{"extension":[{"url":"https://example.test/presence","valueString":"no scalar"}]},null]}]}
            """,
            """
            <Patient xmlns="http://hl7.org/fhir"><id value="mixed"/><name>
            <given><extension url="https://example.test/presence"><valueString value="no scalar"/></extension></given>
            <given value="Ada"/></name></Patient>
            """);
        AddSearchControl(graph,
            """{"resourceType":"Observation","id":"empty","status":"final","code":{"text":"No value or reference"}}""",
            """<Observation xmlns="http://hl7.org/fhir"><id value="empty"/><status value="final"/><code><text value="No value or reference"/></code></Observation>""");
        AddSearchControl(graph,
            """
            {"resourceType":"Observation","id":"populated","status":"final","code":{"text":"Present value and reference"},
             "subject":{"reference":"Patient/populated"},"valueQuantity":{"value":0,"unit":"mg"}}
            """,
            """
            <Observation xmlns="http://hl7.org/fhir"><id value="populated"/><status value="final"/><code><text value="Present value and reference"/></code>
            <subject><reference value="Patient/populated"/></subject><valueQuantity><value value="0"/><unit value="mg"/></valueQuantity></Observation>
            """);
        string root = SearchCanonicalRoot(tenant.Version);
        string processing = tenant.Version == FhirReleases.FhirSequenceCodes.R5 ? ",\"processingMode\":\"normal\"" : string.Empty;
        string xmlProcessing = tenant.Version == FhirReleases.FhirSequenceCodes.R5 ? """<processingMode value="normal"/>""" : string.Empty;
        AddSearchControl(graph,
            $$$"""
            {"resourceType":"SearchParameter","id":"multiplebirth","url":"{{{root}}}/SearchParameter/multiplebirth",
             "name":"PresenceMultipleBirth","status":"active","description":"Presence of an integer, including zero",
             "code":"multiplebirth","base":["Patient"],"type":"number","expression":"(Patient.multipleBirth as integer)"{{{processing}}}}
            """,
            $"""
            <SearchParameter xmlns="http://hl7.org/fhir"><id value="multiplebirth"/><url value="{root}/SearchParameter/multiplebirth"/>
            <name value="PresenceMultipleBirth"/><status value="active"/><description value="Presence of an integer, including zero"/>
            <code value="multiplebirth"/><base value="Patient"/><type value="number"/><expression value="(Patient.multipleBirth as integer)"/>{xmlProcessing}</SearchParameter>
            """);
        AddSearchControl(graph,
            $$$"""
            {"resourceType":"SearchParameter","id":"compat-given","url":"{{{root}}}/SearchParameter/compat-given",
             "name":"PresenceGiven","status":"active","description":"Presence across all primitive given-name members",
             "code":"compat-given","base":["Patient"],"type":"string","expression":"Patient.name.given"{{{processing}}}}
            """,
            $"""
            <SearchParameter xmlns="http://hl7.org/fhir"><id value="compat-given"/><url value="{root}/SearchParameter/compat-given"/>
            <name value="PresenceGiven"/><status value="active"/><description value="Presence across all primitive given-name members"/>
            <code value="compat-given"/><base value="Patient"/><type value="string"/><expression value="Patient.name.given"/>{xmlProcessing}</SearchParameter>
            """);
        return graph;
    }

    private static string ForeignSearchReference(CompatibilityTenant tenant, CompatibilityTenant sibling, string kind) => kind switch
    {
        "foreign-authority" => $"https://foreign.example.test/fhir/{tenant.Name}/Patient/alpha",
        "foreign-tenant" => sibling.BaseUrl + "/Patient/alpha",
        "prefix-lookalike" => tenant.BaseUrl + "-other/Patient/alpha",
        "prefix-descendant" => tenant.BaseUrl + "/other/Patient/alpha",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static SearchGraph LocalReferenceControlGraph(
        CompatibilityTenant tenant, CompatibilityTenant sibling, CompatibilityFormat format)
    {
        SearchGraph graph = new(tenant, format, new(StringComparer.Ordinal), new(StringComparer.Ordinal));
        AddSearchControl(graph,
            """{"resourceType":"Patient","id":"alpha"}""",
            """<Patient xmlns="http://hl7.org/fhir"><id value="alpha"/></Patient>""");
        foreach ((string id, string reference) in new[]
        {
            ("relative", "Patient/alpha"),
            ("absolute", tenant.BaseUrl + "/Patient/alpha"),
            ("foreign-authority", ForeignSearchReference(tenant, sibling, "foreign-authority")),
            ("foreign-tenant", ForeignSearchReference(tenant, sibling, "foreign-tenant")),
            ("prefix-lookalike", ForeignSearchReference(tenant, sibling, "prefix-lookalike")),
            ("prefix-descendant", ForeignSearchReference(tenant, sibling, "prefix-descendant")),
            ("urn", "urn:uuid:11111111-2222-4333-8444-555555555555"),
            ("oid", "urn:oid:1.2.3.4.5"),
            ("typed-other", "Group/alpha"),
        })
        {
            string meta = id == "relative" ? ""","meta":{"profile":["https://example.test/profiles/local|1.0"]}""" : string.Empty;
            string xmlMeta = id == "relative" ? """<meta><profile value="https://example.test/profiles/local|1.0"/></meta>""" : string.Empty;
            AddSearchControl(graph,
                $$$"""
                {"resourceType":"Observation","id":"{{{id}}}"{{{meta}}},"status":"final","code":{"text":"Reference boundary control"},
                 "subject":{"reference":"{{{reference}}}"}}
                """,
                $"""
                <Observation xmlns="http://hl7.org/fhir"><id value="{id}"/>{xmlMeta}<status value="final"/>
                <code><text value="Reference boundary control"/></code><subject><reference value="{reference}"/></subject></Observation>
                """);
        }
        return graph;
    }

    private static SearchGraph ReverseChainControlGraph(CompatibilityTenant tenant, CompatibilityFormat format, bool sibling)
    {
        SearchGraph graph = new(tenant, format, new(StringComparer.Ordinal), new(StringComparer.Ordinal));
        AddSearchControl(graph,
            """{"resourceType":"Patient","id":"alpha","name":[{"family":"Alpha"}]}""",
            """<Patient xmlns="http://hl7.org/fhir"><id value="alpha"/><name><family value="Alpha"/></name></Patient>""");
        AddSearchControl(graph,
            """{"resourceType":"Patient","id":"beta","name":[{"family":"Beta"}]}""",
            """<Patient xmlns="http://hl7.org/fhir"><id value="beta"/><name><family value="Beta"/></name></Patient>""");
        AddSearchControl(graph,
            """{"resourceType":"Patient","id":"missing"}""",
            """<Patient xmlns="http://hl7.org/fhir"><id value="missing"/></Patient>""");
        string target = sibling ? "beta" : "alpha";
        string other = sibling ? "alpha" : "beta";
        AddSearchControl(graph,
            $$$"""{"resourceType":"Observation","id":"early","status":"final","code":{"coding":[{"code":"red"}]},"subject":{"reference":"Patient/{{{target}}}"}}""",
            $"""<Observation xmlns="http://hl7.org/fhir"><id value="early"/><status value="final"/><code><coding><code value="red"/></coding></code><subject><reference value="Patient/{target}"/></subject></Observation>""");
        AddSearchControl(graph,
            $$$"""{"resourceType":"Observation","id":"twin","status":"preliminary","code":{"coding":[{"code":"blue"}]},"subject":{"reference":"Patient/{{{target}}}"}}""",
            $"""<Observation xmlns="http://hl7.org/fhir"><id value="twin"/><status value="preliminary"/><code><coding><code value="blue"/></coding></code><subject><reference value="Patient/{target}"/></subject></Observation>""");
        AddSearchControl(graph,
            $$$"""{"resourceType":"Observation","id":"late","status":"final","code":{"coding":[{"code":"blue"}]},"subject":{"reference":"Patient/{{{other}}}"}}""",
            $"""<Observation xmlns="http://hl7.org/fhir"><id value="late"/><status value="final"/><code><coding><code value="blue"/></coding></code><subject><reference value="Patient/{other}"/></subject></Observation>""");
        return graph;
    }

    private static SearchGraph LoadSearchGraph(CompatibilityTenant tenant, CompatibilityFormat format, bool sibling = false)
    {
        using JsonDocument document = JsonDocument.Parse(CompatibilityReplay.LoadFixture(tenant.Version, "search-resources.json"));
        Assert.Equal(ExpectedFhirVersion(tenant.Version), document.RootElement.GetProperty("fhirVersion").GetString());
        Dictionary<string, string> inputs = document.RootElement.GetProperty("resources").EnumerateArray()
            .ToDictionary(resource => SearchIdentity(resource.GetRawText(), CompatibilityFormat.Json), resource => resource.GetRawText(), StringComparer.Ordinal);
        Assert.Equal(14, inputs.Count);
        if (sibling)
        {
            JsonElement[] overrides = document.RootElement.GetProperty("siblingOverrides").EnumerateArray().ToArray();
            Assert.Equal(7, overrides.Length);
            Assert.Equal(overrides.Length, overrides.Select(resource => SearchIdentity(resource.GetRawText(), CompatibilityFormat.Json)).Distinct().Count());
            foreach (JsonElement resource in overrides)
            {
                string identity = SearchIdentity(resource.GetRawText(), CompatibilityFormat.Json);
                Assert.True(inputs.ContainsKey(identity), $"A sibling override must replace a declared resource: {identity}");
                inputs[identity] = resource.GetRawText();
            }
        }

        SearchGraph graph = new(tenant, format, new(StringComparer.Ordinal), new(StringComparer.Ordinal));
        foreach (KeyValuePair<string, string> input in inputs)
        {
            string[] identity = input.Key.Split('/');
            string content = format == CompatibilityFormat.Json
                ? input.Value.Replace("{{tenant-base}}", tenant.BaseUrl, StringComparison.Ordinal)
                : SearchGraphXml(tenant, input.Key, sibling);
            graph.Resources.Add(input.Key, content);
            graph.Generated.Add(input.Key, new GeneratedResourceExpectation($"{tenant.Name}/{input.Key}/v1", identity[0], "1", identity[1]));
        }
        return graph;
    }

    private static SearchProbe[] SearchFamilyProbes(CompatibilityTenant tenant, string family)
    {
        string root = SearchCanonicalRoot(tenant.Version);
        return family switch
        {
            "string" =>
            [
                new("name-prefix", "Patient?name=ad", ["Patient/alpha"]),
                new("normalized-family", "Patient?family=munoz", ["Patient/alpha", "Patient/gamma"]),
                new("family-contains", "Patient?family:contains=UNO", ["Patient/alpha", "Patient/gamma"]),
                new("exact-family", "Patient?family:exact=Muñoz", ["Patient/alpha", "Patient/gamma"]),
                new("exact-negative", "Patient?family:exact=Munoz", []),
                new("string-negative", "Patient?name=NotPresent", []),
                new("string-or", "Patient?name=NotPresent,Ada", ["Patient/alpha"]),
                new("string-and", "Patient?name=Ada&name=Zoe", []),
                new("address", "Patient?address=quebec", ["Patient/alpha"]),
                new("string-missing", "Patient?name:missing=true", ["Patient/missing"]),
                new("string-present", "Patient?name:missing=false", ["Patient/alpha", "Patient/beta", "Patient/gamma"]),
            ],
            "token" =>
            [
                new("enum-code", "Patient?gender=female", ["Patient/alpha", "Patient/gamma"]),
                new("enum-negative", "Patient?gender=other", []),
                new("enum-not-absent", "Patient?gender:not=male", ["Patient/alpha", "Patient/gamma", "Patient/missing"]),
                new("token-missing", "Patient?gender:missing=true", ["Patient/missing"]),
                new("token-present", "Patient?gender:missing=false", ["Patient/alpha", "Patient/beta", "Patient/gamma"]),
                new("identifier", $"Patient?identifier={root}/identifiers|A", ["Patient/alpha"]),
                new("wrong-identifier-system", "Patient?identifier=https://example.test/wrong|A", []),
                new("boolean-true", "Patient?active=true", ["Patient/alpha", "Patient/gamma"]),
                new("boolean-false", "Patient?active=false", ["Patient/beta"]),
                new("observation-code", $"Observation?code={root}/codes|weight", ["Observation/early", "Observation/late"]),
                new("observation-status", "Observation?status=amended", ["Observation/early"]),
                new("encounter-class", "Encounter?class=http://terminology.hl7.org/CodeSystem/v3-ActCode|AMB", ["Encounter/visit"]),
            ],
            "number" =>
            [
                new("integer-equal", "Patient?multiplebirth=2", ["Patient/alpha"]),
                new("integer-prefix", "Patient?multiplebirth=ge3", ["Patient/beta"]),
                new("integer-negative", "Patient?multiplebirth=lt2", []),
                new("integer-missing", "Patient?multiplebirth:missing=true", ["Patient/gamma", "Patient/missing"]),
                new("integer-present", "Patient?multiplebirth:missing=false", ["Patient/alpha", "Patient/beta"]),
            ],
            "quantity" =>
            [
                new("quantity-equal", "Observation?value-quantity=10|http://unitsofmeasure.org|mg", ["Observation/early"]),
                new("quantity-prefix", "Observation?value-quantity=ge15||mg", ["Observation/twin"]),
                new("quantity-negative", "Observation?value-quantity=gt30", []),
                new("quantity-wrong-unit", "Observation?value-quantity=10|http://unitsofmeasure.org|g", []),
                new("quantity-missing", "Observation?value-quantity:missing=true", ["Observation/missing"]),
                new("quantity-present", "Observation?value-quantity:missing=false", ["Observation/early", "Observation/late", "Observation/twin"]),
            ],
            "date" =>
            [
                new("date-precision", "Patient?birthdate=1980-02", ["Patient/alpha", "Patient/gamma"]),
                new("date-prefix", "Patient?birthdate=ge1990", ["Patient/beta"]),
                new("date-negative", "Patient?birthdate=gt2030", []),
                new("date-missing", "Patient?birthdate:missing=true", ["Patient/missing"]),
                new("date-present", "Patient?birthdate:missing=false", ["Patient/alpha", "Patient/beta", "Patient/gamma"]),
                new("datetime-year", "Observation?date=2020", ["Observation/early", "Observation/twin"]),
                new("datetime-range", "Observation?date=ge2020&date=le2020", ["Observation/early", "Observation/twin"]),
                new("datetime-prefix", "Observation?date=ge2021", ["Observation/late"]),
                new("datetime-missing", "Observation?date:missing=true", ["Observation/missing"]),
                new("period", "Encounter?date=2021-06", ["Encounter/visit"]),
                new("period-negative", "Encounter?date=2030", []),
                new("period-missing", "Encounter?date:missing=true", ["Encounter/other-visit"]),
            ],
            "reference" =>
            [
                new("relative-reference", "Observation?subject=Patient/alpha", ["Observation/early", "Observation/twin"]),
                new("bare-reference", "Observation?subject=alpha", ["Observation/early", "Observation/twin"]),
                new("typed-reference", "Observation?subject:Patient=Patient/alpha", ["Observation/early", "Observation/twin"]),
                new("wrong-reference-type", "Observation?subject:Device=Patient/alpha", []),
                new("reference-negative", "Observation?subject=Patient/not-present", []),
                new("absolute-reference", $"Observation?subject={tenant.BaseUrl}/Patient/beta", ["Observation/late"]),
                new("reference-missing", "Observation?subject:missing=true", ["Observation/missing"]),
                new("reference-present", "Observation?subject:missing=false", ["Observation/early", "Observation/late", "Observation/twin"]),
                new("canonical-reference", $"Observation?_profile={root}/profile/vitals", ["Observation/early"]),
                new("reference-identifier", $"Encounter?subject:identifier={root}/identifiers|A", ["Encounter/visit"]),
                new("reference-identifier-negative", $"Encounter?subject:identifier={root}/identifiers|not-present", []),
            ],
            "uri" =>
            [
                new("uri-equal", $"Observation?_source={root}/source/a", ["Observation/early"]),
                new("uri-negative", $"Observation?_source={root}/source/not-present", []),
                new("uri-missing", "Observation?_source:missing=true", ["Observation/missing", "Observation/twin"]),
                new("uri-present", "Observation?_source:missing=false", ["Observation/early", "Observation/late"]),
            ],
            "composite" =>
            [
                new("composite-equal", $"Observation?code-value-quantity={root}/codes|weight$10|http://unitsofmeasure.org|mg", ["Observation/early"]),
                new("composite-second", $"Observation?code-value-quantity={root}/codes|weight$30|http://unitsofmeasure.org|g", ["Observation/late"]),
                new("composite-no-cross-resource-match", $"Observation?code-value-quantity={root}/codes|pulse$10|http://unitsofmeasure.org|mg", []),
                new("composite-negative", $"Observation?code-value-quantity={root}/codes|weight$20|http://unitsofmeasure.org|mg", []),
                new("composite-or-component", $"Observation?code-value-quantity={root}/codes|weight,{root}/codes|pulse$20|http://unitsofmeasure.org|mg", ["Observation/twin"]),
                new("composite-absent-components", $"Observation?_id=missing&code-value-quantity={root}/codes|weight$10|http://unitsofmeasure.org|mg", []),
            ],
            _ => throw new ArgumentOutOfRangeException(nameof(family)),
        };
    }

    private static SearchProbe[] SearchReferenceProbes(CompatibilityTenant tenant, string kind, bool sibling)
    {
        string localTarget = sibling ? "beta" : "alpha";
        string otherTarget = sibling ? "alpha" : "beta";
        string root = SearchCanonicalRoot(tenant.Version);
        return kind switch
        {
            "forward-chain" =>
            [
                new("chain-id", "Observation?subject._id=alpha", sibling ? ["Observation/late"] : ["Observation/early", "Observation/twin"]),
                new("typed-chain", "Observation?subject:Patient.family=munoz", ["Observation/early", "Observation/twin"]),
                new("chain-other-tenant-name", "Observation?subject.family=elm", sibling ? ["Observation/late"] : []),
                new("chain-negative", "Observation?subject.name=Nobody", []),
                new("encounter-chain", "Encounter?subject:Patient._id=alpha", ["Encounter/visit"]),
            ],
            "nested-chain" =>
            [
                new("chain-oak-organization", "Observation?subject.organization.name=Oak", sibling ? [] : ["Observation/early", "Observation/twin"]),
                new("chain-elm-organization", "Observation?subject.organization.name=Elm", sibling ? ["Observation/late"] : []),
                new("chain-organization-negative", "Patient?organization.name=NotPresent", []),
            ],
            "reverse-chain" =>
            [
                new("reverse-patient", "Patient?_has:Observation:patient:_id=early", ["Patient/" + localTarget]),
                new("reverse-subject", "Patient?_has:Observation:subject:_id=late", ["Patient/" + otherTarget]),
                new("reverse-absent-reference", "Patient?_has:Observation:patient:_id=missing", []),
                new("reverse-no-target", "Patient?_has:Observation:patient:_id=not-present", []),
            ],
            "include" =>
            [
                new("include-deduplicates-target", $"Observation?subject=Patient/{localTarget}&_include=Observation:patient",
                    ["Observation/early", "Observation/twin"]) { Includes = ["Patient/" + localTarget] },
                new("include-absolute-target", "Observation?_id=late&_include=Observation:subject",
                    ["Observation/late"]) { Includes = ["Patient/" + otherTarget] },
                new("include-absent-target", "Observation?_id=missing&_include=Observation:patient", ["Observation/missing"]),
                new("include-empty-match", "Observation?_id=not-present&_include=Observation:patient", []),
            ],
            "revinclude" =>
            [
                new("revinclude", "Patient?_id=alpha&_revinclude=Observation:patient", ["Patient/alpha"])
                {
                    Includes = sibling ? ["Observation/late"] : ["Observation/early", "Observation/twin"],
                },
                new("revinclude-two-matches", "Patient?_id=alpha,beta&_revinclude=Observation:patient", ["Patient/alpha", "Patient/beta"])
                {
                    Includes = ["Observation/early", "Observation/late", "Observation/twin"],
                },
                new("revinclude-encounter", "Patient?_id=alpha&_revinclude=Encounter:patient", ["Patient/alpha"])
                {
                    Includes = ["Encounter/visit"],
                },
                new("revinclude-absent", "Patient?_id=missing&_revinclude=Observation:patient", ["Patient/missing"]),
            ],
            "membership" =>
            [
                new("local-valueset", $"Observation?code:in={root}/ValueSet/local-membership",
                    sibling ? ["Observation/twin"] : ["Observation/early", "Observation/late"]),
                new("unknown-valueset", $"Observation?code:in={root}/ValueSet/not-present", []),
                new("absent-coding-not-member", $"Observation?_id=missing&code:in={root}/ValueSet/local-membership", []),
            ],
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
    }

    private static SearchProbe SearchSortProbe(string kind, bool descending) => (kind, descending) switch
    {
        ("birthdate", false) => new("birthdate-ascending", "Patient?_id=alpha,beta,missing&_sort=birthdate",
            ["Patient/missing", "Patient/alpha", "Patient/beta"]),
        ("birthdate", true) => new("birthdate-descending", "Patient?_id=alpha,beta,missing&_sort=-birthdate",
            ["Patient/beta", "Patient/alpha", "Patient/missing"]),
        ("birthdate-secondary", false) => new("birthdate-secondary-ascending", "Patient?_sort=birthdate,-_id",
            ["Patient/missing", "Patient/gamma", "Patient/alpha", "Patient/beta"]),
        ("birthdate-secondary", true) => new("birthdate-secondary-descending", "Patient?_sort=-birthdate,_id",
            ["Patient/beta", "Patient/alpha", "Patient/gamma", "Patient/missing"]),
        ("date", false) => new("date-ascending", "Observation?_id=early,late,missing&_sort=date",
            ["Observation/missing", "Observation/early", "Observation/late"]),
        ("date", true) => new("date-descending", "Observation?_id=early,late,missing&_sort=-date",
            ["Observation/late", "Observation/early", "Observation/missing"]),
        ("date-secondary", false) => new("date-secondary-ascending", "Observation?_sort=date,-_id",
            ["Observation/missing", "Observation/twin", "Observation/early", "Observation/late"]),
        ("date-secondary", true) => new("date-secondary-descending", "Observation?_sort=-date,_id",
            ["Observation/late", "Observation/early", "Observation/twin", "Observation/missing"]),
        ("status", false) => new("status-ascending", "Observation?_id=early,late,twin&_sort=status",
            ["Observation/early", "Observation/late", "Observation/twin"]),
        ("status", true) => new("status-descending", "Observation?_id=early,late,twin&_sort=-status",
            ["Observation/twin", "Observation/late", "Observation/early"]),
        ("status-secondary", false) => new("status-secondary-ascending", "Observation?_sort=status,date,-_id",
            ["Observation/early", "Observation/missing", "Observation/late", "Observation/twin"]),
        ("status-secondary", true) => new("status-secondary-descending", "Observation?_sort=-status,-date,_id",
            ["Observation/twin", "Observation/late", "Observation/missing", "Observation/early"]),
        ("_lastUpdated", false) => new("last-updated-ascending", "Observation?_sort=_lastUpdated,_id",
            ["Observation/twin", "Observation/late", "Observation/missing", "Observation/early"]),
        ("_lastUpdated", true) => new("last-updated-descending", "Observation?_sort=-_lastUpdated,-_id",
            ["Observation/early", "Observation/missing", "Observation/late", "Observation/twin"]),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static async Task AssertSearchProbe(
        CompatibilityReplay replay,
        SearchGraph graph,
        SearchProbe probe,
        bool ordered = false,
        IReadOnlyDictionary<string, string>? headers = null,
        string? responseRoot = null)
    {
        foreach (bool form in new[] { false, true })
        {
            CompatibilityRequest request = SearchRequest(graph, probe.Path, form, headers);
            await AssertSearchRequest(replay, graph, probe, request, ordered, responseRoot);
        }
    }

    private static string SearchDiagnostic(
        CompatibilityReplay replay, SearchGraph graph, string probe, CompatibilityRequest request) =>
        $"{graph.Tenant.Version} / {graph.Format} / {replay.Transport} / tenant {graph.Tenant.Name} / probe {probe} / " +
        $"{request.Method} {graph.Tenant.BaseUrl}/{request.Path} / " +
        (request.SourceMediaType == "application/x-www-form-urlencoded" ? $"form [{request.Content}]" : "GET/query");

    private static async Task AssertSearchRequest(
        CompatibilityReplay replay,
        SearchGraph graph,
        SearchProbe probe,
        CompatibilityRequest request,
        bool ordered = false,
        string? responseRoot = null)
    {
        string context = SearchDiagnostic(replay, graph, probe.Id, request);
        string name = $"{graph.Tenant.Name}-{probe.Id}-{request.Method}";
        try
        {
            CompatibilityResponse response = await replay.SendAsync(request);
            string path = probe.Path.Split('?')[0];
            string diagnostic = path.Length == 0 ? "System search successful" : path.EndsWith("/*", StringComparison.Ordinal)
                ? "Compartment search successful"
                : path.Contains('/') ? "Compartment type search successful" : "Type search successful";
            AssertExchangeContainer(replay, graph.Tenant, graph.Format, name, response, "Bundle", string.Empty,
                new OutcomeBodyExpectation(name + "-outcome", [new("information", "success", diagnostic)]));
            string body = ExchangeResource(response);
            string[] rawEntries = ExchangeHasMember(body, graph.Format, "entry")
                ? ExchangeRepeated(body, graph.Format, "entry")
                : [];
            string[] actualIdentities = rawEntries.Select(entry =>
                SearchIdentity(ExchangeNestedResource(entry, graph.Format, "resource"), graph.Format)).ToArray();
            context += $" / expected matches [{string.Join(", ", probe.Matches)}], includes [{string.Join(", ", probe.Includes)}]" +
                $" / actual entries [{string.Join(", ", actualIdentities)}]";
            Assert.Equal("Bundle", ExchangeResourceType(body, graph.Format));
            Assert.Equal("searchset", ExchangePrimitive(body, graph.Format, "type"));
            Assert.Equal(probe.Matches.Length, SearchTotal(body, graph.Format));
            string link = Assert.Single(ExchangeRepeated(body, graph.Format, "link"));
            Assert.Equal("self", ExchangePrimitive(link, graph.Format, "relation"));
            string appliedPath = probe.AppliedPath ?? probe.Path;
            string root = responseRoot ?? graph.Tenant.BaseUrl;
            Assert.Equal(root + (appliedPath.StartsWith('?') ? string.Empty : "/") + appliedPath,
                Uri.UnescapeDataString(ExchangePrimitive(link, graph.Format, "url")));

            List<SearchEntry> entries = [];
            foreach (string entry in rawEntries)
            {
                AssertExchangeMembers(entry, graph.Format, ["fullUrl", "resource", "search"]);
                string resource = ExchangeNestedResource(entry, graph.Format, "resource");
                string search = ExchangeChild(entry, graph.Format, "search");
                AssertExchangeMembers(search, graph.Format, ["mode"]);
                entries.Add(new(SearchIdentity(resource, graph.Format), ExchangePrimitive(search, graph.Format, "mode"),
                    ExchangePrimitive(entry, graph.Format, "fullUrl"), resource));
            }

            AssertSearchIdentities(probe, entries, ordered, context);
            foreach (SearchEntry entry in entries)
            {
                Assert.Equal(root + "/" + entry.Identity, entry.FullUrl);
                Assert.True(graph.Resources.TryGetValue(entry.Identity, out string? expected), $"{name}: undeclared resource {entry.Identity}");
                GeneratedResourceExpectation generated = graph.Generated[entry.Identity] with { Mode = CompatibilityBindingMode.Reuse };
                WireContractAssertions.Body(new ResourceBodyExpectation(expected!, generated),
                    entry.Content, graph.Format, graph.Tenant.BaseUrl, replay.Bindings);
            }

            AssertExchangeMembers(body, graph.Format, entries.Count == 0
                ? ["resourceType", "type", "total", "link"]
                : ["resourceType", "type", "total", "link", "entry"]);
        }
        catch (Exception exception) when (exception is Xunit.Sdk.XunitException or InvalidOperationException or JsonException or System.Xml.XmlException)
        {
            throw new InvalidOperationException($"{context}: {exception.Message}", exception);
        }
    }

    private static void AssertSearchIdentities(SearchProbe probe, IReadOnlyList<SearchEntry> entries, bool ordered, string context)
    {
        string[] identities = entries.Select(entry => entry.Identity).ToArray();
        Assert.True(identities.Length == identities.Distinct(StringComparer.Ordinal).Count(),
            $"{context}: duplicate entries: [{string.Join(", ", identities)}]");
        Assert.All(entries, entry => Assert.Contains(entry.Mode, new[] { "match", "include" }));
        string[] matches = entries.Where(entry => entry.Mode == "match").Select(entry => entry.Identity).ToArray();
        string[] includes = entries.Where(entry => entry.Mode == "include").Select(entry => entry.Identity).ToArray();
        Assert.Equal(probe.Matches.Length + probe.Includes.Length, entries.Count);
        Assert.Equal(probe.Matches.Order(StringComparer.Ordinal), matches.Order(StringComparer.Ordinal));
        Assert.Equal(probe.Includes.Order(StringComparer.Ordinal), includes.Order(StringComparer.Ordinal));
        if (ordered)
        {
            Assert.True(probe.Matches.SequenceEqual(matches, StringComparer.Ordinal),
                $"{context}: expected order [{string.Join(", ", probe.Matches)}], actual [{string.Join(", ", matches)}]");
        }
    }

    private static CompatibilityRequest SearchRequest(
        SearchGraph graph,
        string path,
        bool form,
        IReadOnlyDictionary<string, string>? headers)
    {
        string[] parts = path.Split('?', 2);
        string query = parts.Length == 1 ? string.Empty : string.Join('&', parts[1].Split('&').Select(pair =>
        {
            string[] kv = pair.Split('=', 2);
            return Uri.EscapeDataString(kv[0]) + "=" + Uri.EscapeDataString(kv[1]);
        }));
        string target = form
            ? (parts[0].EndsWith("/*", StringComparison.Ordinal) ? parts[0][..^2] : parts[0]) + "/_search"
            : parts[0] + (query.Length == 0 ? string.Empty : "?" + query);
        return new CompatibilityRequest(graph.Tenant.Name, form ? "POST" : "GET", target)
        {
            Format = graph.Format,
            Content = form ? query : null,
            SourceMediaType = form ? "application/x-www-form-urlencoded" : null,
            Headers = headers ?? new Dictionary<string, string>(),
        };
    }

    private static int SearchTotal(string body, CompatibilityFormat format)
    {
        if (format == CompatibilityFormat.Json)
        {
            using JsonDocument document = JsonDocument.Parse(body);
            return document.RootElement.GetProperty("total").GetInt32();
        }
        return int.Parse(ExchangePrimitive(body, format, "total"), CultureInfo.InvariantCulture);
    }

    private static string SearchIdentity(string resource, CompatibilityFormat format) =>
        ExchangeResourceType(resource, format) + "/" + ExchangePrimitive(resource, format, "id");

    private static string SearchCanonicalRoot(FhirReleases.FhirSequenceCodes version) =>
        $"https://example.test/search/{VersionName(version)}";

    private static async Task<GeneratedResourceExpectation> UpdateSearchResource(
        CompatibilityReplay replay,
        SearchGraph graph,
        string identity)
    {
        GeneratedResourceExpectation previous = graph.Generated[identity];
        GeneratedResourceExpectation updated = new($"{graph.Tenant.Name}/{identity}/v2",
            previous.ResourceType, "2", previous.ExpectedId, PreviousBinding: previous.Name);
        await replay.StepAsync(new CompatibilityStep(updated.Name,
            new CompatibilityRequest(graph.Tenant.Name, "PUT", identity)
            {
                Format = graph.Format, Content = graph.Resources[identity], IfMatchBinding = previous.Name,
            }, ResourceResponse(updated.Name, graph.Resources[identity], updated, HttpStatusCode.OK)));
        graph.Generated[identity] = updated;
        return updated;
    }

    private static async Task AssertSearchFailure(
        CompatibilityReplay replay,
        SearchGraph graph,
        string id,
        string path,
        HttpStatusCode status,
        OutcomeIssueExpectation[] issues,
        IReadOnlyDictionary<string, string>? headers = null)
    {
        foreach (bool form in new[] { false, true })
        {
            string name = $"{graph.Tenant.Name}-{id}-{(form ? "form" : "get")}";
            CompatibilityRequest request = SearchRequest(graph, path, form, headers);
            try
            {
                CompatibilityResponse response = await replay.SendAsync(request);
                OutcomeBodyExpectation outcome = new(name + "-outcome", issues);
                WireContractAssertions.Response(
                    new CompatibilityResponseExpectation(status, false, outcome, new EmptyBodyExpectation(), outcome)
                    {
                        HttpHeaders = NoExchangeHeaders(),
                        DirectHeaders = NoExchangeHeaders(),
                    }, response, graph.Format, graph.Tenant.BaseUrl, replay.Bindings);
                if (response.Direct is CompatibilityDirectResponse direct)
                {
                    Assert.Empty(direct.ResourceType);
                    Assert.Empty(direct.Id);
                }
            }
            catch (Exception exception) when (exception is Xunit.Sdk.XunitException or InvalidOperationException or JsonException or System.Xml.XmlException)
            {
                throw new InvalidOperationException($"{SearchDiagnostic(replay, graph, id, request)}: {exception.Message}", exception);
            }
        }
    }

    private static async Task AssertSearchParameterLifecycle(CompatibilityReplay replay, SearchGraph oak, SearchGraph elm)
    {
        const string code = "compat-family";
        SearchCapabilityParameter[] before = await ReadSearchCapabilities(replay, oak, "before");
        SearchCapabilityParameter[] siblingBefore = await ReadSearchCapabilities(replay, elm, "before");
        Assert.DoesNotContain(before, parameter => parameter.Name == code);
        Assert.DoesNotContain(siblingBefore, parameter => parameter.Name == code);
        await AssertSearchProbe(replay, oak, new("before-unknown-lenient", $"Patient?{code}=munoz", _searchPatientIds) { AppliedPath = "Patient" });
        await AssertUnknownDynamicSearch(replay, oak, "before", code);

        string initial = DynamicSearchParameterContent(oak.Tenant.Version, oak.Format, "Patient.name.family", "Search family before update");
        GeneratedResourceExpectation v1 = new("dynamic-search-v1", "SearchParameter", "1", "compat-family");
        await WriteSearchDefinition(replay, oak, "create-search-parameter", "SearchParameter/compat-family", initial, v1, HttpStatusCode.Created);
        await AssertSearchProbe(replay, oak, new("created-family", $"Patient?{code}=munoz", ["Patient/alpha", "Patient/gamma"]));
        await AssertSearchProbe(replay, oak, new("created-given-negative", $"Patient?{code}=Ada", []));
        SearchCapabilityParameter[] created = await ReadSearchCapabilities(replay, oak, "created");
        Assert.Equal(before.Length + 1, created.Length);
        Assert.Equal(new SearchCapabilityParameter(code, SearchCanonicalRoot(oak.Tenant.Version) + "/SearchParameter/compat-family",
            "string", "Search family before update"), Assert.Single(created, parameter => parameter.Name == code));
        await AssertUnknownDynamicSearch(replay, elm, "after-oak-create", code);
        Assert.Equal(siblingBefore.Length, (await ReadSearchCapabilities(replay, elm, "after-oak-create")).Length);

        string changed = DynamicSearchParameterContent(oak.Tenant.Version, oak.Format, "Patient.name.given", "Search given after update");
        GeneratedResourceExpectation v2 = new("dynamic-search-v2", "SearchParameter", "2", "compat-family", PreviousBinding: v1.Name);
        await WriteSearchDefinition(replay, oak, "update-search-parameter", "SearchParameter/compat-family", changed, v2, HttpStatusCode.OK, v1.Name);
        await AssertSearchProbe(replay, oak, new("updated-old-expression-negative", $"Patient?{code}=munoz", []));
        await AssertSearchProbe(replay, oak, new("updated-given", $"Patient?{code}=Ada", ["Patient/alpha"]));
        await AssertSearchProbe(replay, oak, new("updated-other-given", $"Patient?{code}=Zoe", ["Patient/gamma"]));
        SearchCapabilityParameter[] updated = await ReadSearchCapabilities(replay, oak, "updated");
        Assert.Equal(before.Length + 1, updated.Length);
        Assert.Equal(new SearchCapabilityParameter(code, SearchCanonicalRoot(oak.Tenant.Version) + "/SearchParameter/compat-family",
            "string", "Search given after update"), Assert.Single(updated, parameter => parameter.Name == code));

        await DeleteSearchDefinition(replay, oak, "delete-search-parameter", "SearchParameter/compat-family", changed, v2);
        SearchCapabilityParameter[] deleted = await ReadSearchCapabilities(replay, oak, "deleted");
        Assert.Equal(before.Length, deleted.Length);
        Assert.DoesNotContain(deleted, parameter => parameter.Name == code);
        await AssertSearchProbe(replay, oak, new("deleted-unknown-lenient", $"Patient?{code}=Ada", _searchPatientIds) { AppliedPath = "Patient" });
        await AssertUnknownDynamicSearch(replay, oak, "deleted", code);
        await AssertUnknownDynamicSearch(replay, elm, "after-oak-delete", code);
        SearchCapabilityParameter[] siblingAfter = await ReadSearchCapabilities(replay, elm, "after-oak-delete");
        Assert.Equal(siblingBefore.Length, siblingAfter.Length);
        Assert.DoesNotContain(siblingAfter, parameter => parameter.Name == code);

        // Reusing the code after deletion must not resurrect its last compiled expression.
        GeneratedResourceExpectation recreated = new("dynamic-search-recreated", "SearchParameter", "1", "compat-family");
        await WriteSearchDefinition(replay, oak, "recreate-search-parameter", "SearchParameter/compat-family", initial, recreated, HttpStatusCode.Created);
        await AssertSearchProbe(replay, oak, new("recreated-family", $"Patient?{code}=munoz", ["Patient/alpha", "Patient/gamma"]));
        await AssertSearchProbe(replay, oak, new("recreated-given-negative", $"Patient?{code}=Ada", []));
        await AssertSearchProbe(replay, oak, new("patients-unchanged", "Patient", _searchPatientIds));
    }

    private static async Task AssertUnknownDynamicSearch(CompatibilityReplay replay, SearchGraph graph, string id, string code) =>
        await AssertSearchFailure(replay, graph, id + "-unknown", $"Patient?{code}=munoz", HttpStatusCode.BadRequest,
            [new("error", "not-supported",
                $"Unknown search parameter '{code}' for this resource type. (see http://hl7.org/fhir/{graph.Tenant.Version}/search.html#errors)")],
            new Dictionary<string, string> { ["Prefer"] = "handling=strict" });

    private static async Task<SearchCapabilityParameter[]> ReadSearchCapabilities(CompatibilityReplay replay, SearchGraph graph, string phase)
    {
        string name = $"{graph.Tenant.Name}-capabilities-{phase}";
        MetadataBodyExpectation body = new(ExpectedFhirVersion(graph.Tenant.Version),
            new GeneratedResourceExpectation(name, "CapabilityStatement", null, "metadata"), name + "-date");
        CompatibilityResponse response = await replay.StepAsync(new CompatibilityStep(name,
            new CompatibilityRequest(graph.Tenant.Name, "GET", "metadata") { Format = graph.Format },
            new CompatibilityResponseExpectation(HttpStatusCode.OK, true, body, body, SuccessOutcome(name))
            {
                HttpResourceHeadersBinding = name,
                DirectResourceBinding = name,
            }));
        string resource = ExchangeResource(response);
        string rest = Assert.Single(ExchangeRepeated(resource, graph.Format, "rest"),
            candidate => ExchangePrimitive(candidate, graph.Format, "mode") == "server");
        string patient = Assert.Single(ExchangeRepeated(rest, graph.Format, "resource"),
            candidate => ExchangePrimitive(candidate, graph.Format, "type") == "Patient");
        SearchCapabilityParameter[] parameters = ExchangeRepeated(patient, graph.Format, "searchParam").Select(parameter =>
            new SearchCapabilityParameter(
                ExchangePrimitive(parameter, graph.Format, "name"),
                SearchOptionalPrimitive(parameter, graph.Format, "definition"),
                ExchangePrimitive(parameter, graph.Format, "type"),
                SearchOptionalPrimitive(parameter, graph.Format, "documentation"))).ToArray();
        Assert.Equal(parameters.Length, parameters.Select(parameter => parameter.Name).Distinct(StringComparer.Ordinal).Count());
        return parameters;
    }

    private static string SearchOptionalPrimitive(string content, CompatibilityFormat format, string name) =>
        ExchangeHasMember(content, format, name) ? ExchangePrimitive(content, format, name) : string.Empty;

    private static async Task AssertSearchCompartmentLifecycle(CompatibilityReplay replay, SearchGraph oak, SearchGraph elm)
    {
        await AssertSearchProbe(replay, oak, new("core-compartment", "Patient/alpha/*",
            ["Observation/early", "Observation/twin", "Encounter/visit"]));
        await AssertSearchProbe(replay, oak, new("core-compartment-type", "Patient/alpha/Observation",
            ["Observation/early", "Observation/twin"]));
        await AssertSearchProbe(replay, oak, new("core-compartment-filter", "Patient/alpha/Observation?_id=early", ["Observation/early"]));
        await AssertSearchProbe(replay, oak, new("core-compartment-negative", "Patient/alpha/Observation?_id=late", []));
        await AssertSearchProbe(replay, oak, new("core-absent-patient", "Patient/not-present/*", []));
        await AssertSearchProbe(replay, oak, new("core-absent-patient-type", "Patient/not-present/Observation", []));

        string observationsOnly = SearchCompartmentContent(oak.Tenant.Version, oak.Format, "Observation", "subject");
        GeneratedResourceExpectation v1 = new("dynamic-compartment-v1", "CompartmentDefinition", "1", "compat-patient");
        await WriteSearchDefinition(replay, oak, "create-compartment", "CompartmentDefinition/compat-patient", observationsOnly, v1, HttpStatusCode.Created);
        await AssertSearchProbe(replay, oak, new("custom-compartment", "Patient/alpha/*", ["Observation/early", "Observation/twin"]));
        await AssertSearchProbe(replay, oak, new("custom-compartment-type", "Patient/alpha/Observation", ["Observation/early", "Observation/twin"]));
        await AssertSearchFailure(replay, oak, "excluded-encounter", "Patient/alpha/Encounter", HttpStatusCode.NotFound,
            [new("error", "not-supported", "Resource type: Encounter is not supported in compartment Patient")]);

        string encountersOnly = SearchCompartmentContent(oak.Tenant.Version, oak.Format, "Encounter", "patient");
        GeneratedResourceExpectation v2 = new("dynamic-compartment-v2", "CompartmentDefinition", "2", "compat-patient", PreviousBinding: v1.Name);
        await WriteSearchDefinition(replay, oak, "update-compartment", "CompartmentDefinition/compat-patient", encountersOnly, v2, HttpStatusCode.OK, v1.Name);
        await AssertSearchProbe(replay, oak, new("updated-compartment", "Patient/alpha/*", ["Encounter/visit"]));
        await AssertSearchProbe(replay, oak, new("updated-compartment-type", "Patient/alpha/Encounter", ["Encounter/visit"]));
        await AssertSearchFailure(replay, oak, "excluded-observation", "Patient/alpha/Observation", HttpStatusCode.NotFound,
            [new("error", "not-supported", "Resource type: Observation is not supported in compartment Patient")]);
        await AssertSearchProbe(replay, elm, new("sibling-compartment-after-update", "Patient/alpha/*", ["Observation/late", "Encounter/visit"]));
        await AssertSearchProbe(replay, elm, new("sibling-type-after-update", "Patient/alpha/Observation", ["Observation/late"]));

        await DeleteSearchDefinition(replay, oak, "delete-compartment", "CompartmentDefinition/compat-patient", encountersOnly, v2);
        foreach (string path in new[] { "Patient/alpha/*", "Patient/alpha/Observation" })
        {
            await AssertSearchFailure(replay, oak, "deleted-" + path, path, HttpStatusCode.NotFound,
                [new("error", "not-supported", "Compartment type: Patient is not supported")]);
        }
        await AssertSearchProbe(replay, elm, new("sibling-compartment-after-delete", "Patient/alpha/*", ["Observation/late", "Encounter/visit"]));
        await AssertSearchProbe(replay, oak, new("ordinary-search-after-compartment-delete", "Observation?subject=Patient/alpha",
            ["Observation/early", "Observation/twin"]));
        await AssertSearchProbe(replay, oak, new("observations-unchanged", "Observation", _searchObservationIds));
    }

    private static async Task WriteSearchDefinition(
        CompatibilityReplay replay,
        SearchGraph graph,
        string name,
        string path,
        string content,
        GeneratedResourceExpectation generated,
        HttpStatusCode status,
        string? previous = null)
    {
        await replay.StepAsync(new CompatibilityStep(name,
            new CompatibilityRequest(graph.Tenant.Name, "PUT", path)
            {
                Format = graph.Format, Content = content, IfMatchBinding = previous,
            }, ResourceResponse(name, content, generated, status)));
        await replay.StepAsync(BoundRead(name + "-read", graph.Tenant, graph.Format, content, generated));
    }

    private static async Task DeleteSearchDefinition(
        CompatibilityReplay replay,
        SearchGraph graph,
        string name,
        string path,
        string content,
        GeneratedResourceExpectation generated)
    {
        await replay.StepAsync(new CompatibilityStep(name,
            new CompatibilityRequest(graph.Tenant.Name, "DELETE", path) { Format = graph.Format },
            ResourceResponse(name, content, generated with { Mode = CompatibilityBindingMode.Reuse }, HttpStatusCode.OK, resourceHeaders: false)));
        await AssertExchangeFailure(replay, graph.Tenant, graph.Format, name + "-read",
            new CompatibilityRequest(graph.Tenant.Name, "GET", path) { Format = graph.Format },
            HttpStatusCode.NotFound, "exception", $"Resource: {path} not found");
    }

    private static string DynamicSearchParameterContent(
        FhirReleases.FhirSequenceCodes version,
        CompatibilityFormat format,
        string expression,
        string description)
    {
        string url = SearchCanonicalRoot(version) + "/SearchParameter/compat-family";
        if (format == CompatibilityFormat.Json)
        {
            string processingMode = version == FhirReleases.FhirSequenceCodes.R5 ? ",\"processingMode\":\"normal\"" : string.Empty;
            return $$$"""
                {"resourceType":"SearchParameter","id":"compat-family","url":"{{{url}}}","name":"CompatibilityFamily",
                 "status":"active","description":"{{{description}}}","code":"compat-family","base":["Patient"],"type":"string",
                 "expression":"{{{expression}}}"{{{processingMode}}}}
                """;
        }
        string xmlProcessing = version == FhirReleases.FhirSequenceCodes.R5 ? """<processingMode value="normal"/>""" : string.Empty;
        return $"""
            <SearchParameter xmlns="http://hl7.org/fhir"><id value="compat-family"/><url value="{url}"/><name value="CompatibilityFamily"/>
            <status value="active"/><description value="{description}"/><code value="compat-family"/><base value="Patient"/>
            <type value="string"/><expression value="{expression}"/>{xmlProcessing}</SearchParameter>
            """;
    }

    private static string SearchCompartmentContent(
        FhirReleases.FhirSequenceCodes version,
        CompatibilityFormat format,
        string resourceType,
        string parameter)
    {
        string url = SearchCanonicalRoot(version) + "/CompartmentDefinition/compat-patient";
        return format == CompatibilityFormat.Json
            ? $$$"""
                {"resourceType":"CompartmentDefinition","id":"compat-patient","url":"{{{url}}}","name":"CompatibilityPatient",
                 "status":"active","code":"Patient","search":true,"resource":[{"code":"{{{resourceType}}}","param":["{{{parameter}}}"]}]}
                """
            : $"""
                <CompartmentDefinition xmlns="http://hl7.org/fhir"><id value="compat-patient"/><url value="{url}"/><name value="CompatibilityPatient"/>
                <status value="active"/><code value="Patient"/><search value="true"/><resource><code value="{resourceType}"/>
                <param value="{parameter}"/></resource></CompartmentDefinition>
                """;
    }

    private static string SearchGraphXml(CompatibilityTenant tenant, string identity, bool sibling)
    {
        // These are separately authored XML inputs/expectations, not a conversion of JSON or an SDK response.
        string root = SearchCanonicalRoot(tenant.Version);
        string language = sibling ? "de" : "en";
        string localTarget = sibling ? "beta" : "alpha";
        string otherTarget = sibling ? "alpha" : "beta";
        bool r5 = tenant.Version == FhirReleases.FhirSequenceCodes.R5;
        string amb = r5
            ? """<class><coding><system value="http://terminology.hl7.org/CodeSystem/v3-ActCode"/><code value="AMB"/></coding></class>"""
            : """<class><system value="http://terminology.hl7.org/CodeSystem/v3-ActCode"/><code value="AMB"/></class>""";
        string imp = r5
            ? """<class><coding><system value="http://terminology.hl7.org/CodeSystem/v3-ActCode"/><code value="IMP"/></coding></class>"""
            : """<class><system value="http://terminology.hl7.org/CodeSystem/v3-ActCode"/><code value="IMP"/></class>""";
        string period = r5 ? "actualPeriod" : "period";
        string inner = identity switch
        {
            "Organization/shared" => $"""<id value="shared"/><name value="{(sibling ? "Elm Clinic" : "Oak Clinic")}"/>""",
            "Organization/other" => """<id value="other"/><name value="Cedar Clinic"/>""",
            "Patient/beta" => $"""
                <id value="beta"/><language value="{language}"/><identifier><system value="{root}/identifiers"/><value value="B"/></identifier>
                <active value="false"/><name><family value="{(sibling ? "Muñoz" : "Baker")}"/><given value="Bea"/></name>
                <gender value="male"/><birthDate value="1990-04-05"/><multipleBirthInteger value="4"/>
                <managingOrganization><reference value="Organization/other"/></managingOrganization>
                """,
            "Patient/gamma" => $"""
                <id value="gamma"/><language value="en"/><identifier><system value="{root}/identifiers"/><value value="G"/></identifier>
                <active value="true"/><name><family value="Muñoz"/><given value="Zoe"/></name><gender value="female"/><birthDate value="1980-02-03"/>
                """,
            "Patient/alpha" => $"""
                <id value="alpha"/><language value="{language}"/><identifier><system value="{root}/identifiers"/><value value="A"/></identifier>
                <active value="true"/><name><family value="{(sibling ? "Elm" : "Muñoz")}"/><given value="Ada"/></name>
                <gender value="female"/><birthDate value="1980-02-03"/><address><city value="Québec"/></address><multipleBirthInteger value="2"/>
                <managingOrganization><reference value="Organization/shared"/></managingOrganization>
                """,
            "Patient/missing" => """<id value="missing"/><language value="en"/>""",
            "Observation/late" => $"""
                <id value="late"/><meta><source value="{root}/source/b"/><profile value="{root}/profile/lab"/></meta><status value="final"/>
                <code><coding><system value="{root}/codes"/><code value="weight"/></coding></code>
                <subject><reference value="{tenant.BaseUrl}/Patient/{otherTarget}"/></subject><effectiveDateTime value="2022-06-07T08:09:10Z"/>
                <valueQuantity><value value="30"/><unit value="g"/><system value="http://unitsofmeasure.org"/><code value="g"/></valueQuantity>
                """,
            "Observation/twin" => $"""
                <id value="twin"/><status value="preliminary"/><code><coding><system value="{root}/codes"/><code value="pulse"/></coding></code>
                <subject><reference value="Patient/{localTarget}"/></subject><effectiveDateTime value="2020-01-02T03:04:05Z"/>
                <valueQuantity><value value="20"/><unit value="mg"/><system value="http://unitsofmeasure.org"/><code value="mg"/></valueQuantity>
                """,
            "Observation/early" => $"""
                <id value="early"/><meta><source value="{root}/source/a"/><profile value="{root}/profile/vitals"/></meta><status value="amended"/>
                <code><coding><system value="{root}/codes"/><code value="weight"/></coding></code>
                <subject><reference value="Patient/{localTarget}"/></subject><effectiveDateTime value="2020-01-02T03:04:05Z"/>
                <valueQuantity><value value="10"/><unit value="mg"/><system value="http://unitsofmeasure.org"/><code value="mg"/></valueQuantity>
                """,
            "Observation/missing" => """<id value="missing"/><status value="final"/><code><text value="No coded measurement or subject"/></code>""",
            "Encounter/visit" => $"""
                <id value="visit"/><status value="{(r5 ? "completed" : "finished")}"/>{amb}
                <subject><reference value="Patient/alpha"/><identifier><system value="{root}/identifiers"/><value value="A"/></identifier></subject>
                <{period}><start value="2021-06-01T09:00:00Z"/><end value="2021-06-01T10:00:00Z"/></{period}>
                """,
            "Encounter/other-visit" => $"""<id value="other-visit"/><status value="planned"/>{imp}<subject><reference value="Patient/beta"/></subject>""",
            "SearchParameter/multiplebirth" => $"""
                <id value="multiplebirth"/><url value="{root}/SearchParameter/multiplebirth"/><name value="CompatibilityMultipleBirth"/>
                <status value="active"/><description value="Search the authored integer multiple birth value"/><code value="multiplebirth"/>
                <base value="Patient"/><type value="number"/><expression value="(Patient.multipleBirth as integer)"/>
                {(r5 ? """<processingMode value="normal"/>""" : string.Empty)}
                """,
            "ValueSet/local-membership" => $"""
                <id value="local-membership"/><url value="{root}/ValueSet/local-membership"/><name value="CompatibilityLocalMembership"/><status value="active"/>
                <compose><include><system value="{root}/codes"/><concept><code value="{(sibling ? "pulse" : "weight")}"/></concept></include></compose>
                """,
            _ => throw new ArgumentOutOfRangeException(nameof(identity)),
        };
        string resourceType = identity.Split('/')[0];
        return $"""<{resourceType} xmlns="http://hl7.org/fhir">{inner}</{resourceType}>""";
    }
}
