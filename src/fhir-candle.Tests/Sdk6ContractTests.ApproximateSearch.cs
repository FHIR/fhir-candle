// <copyright file="Sdk6ContractTests.ApproximateSearch.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using fhir.candle.Tests.Extensions;
using fhir.candle.Tests.Models;
using FhirCandle.Utils;

namespace fhir.candle.Tests;

public partial class Sdk6ContractTests
{
    private static readonly string[] _approximateSearchGroups =
    [
        "isolated-zero", "positive-window", "negative-window", "zero-window",
        "lexical-prefix-isolation", "presence-units", "range-safety",
    ];

    /// <summary>Native decimal nodes have seven independently executable graphs in every boundary tuple.</summary>
    public static IEnumerable<object[]> ApproximateSearchDecimalCases => ApproximateSearchRows("decimal-number");

    /// <summary>Quantity uses the same numeric oracles, with independent unit eligibility controls.</summary>
    public static IEnumerable<object[]> ApproximateSearchQuantityCases => ApproximateSearchRows("quantity");

    /// <summary>The original integer and date approximation policies retain the complete twelve-row matrix.</summary>
    public static IEnumerable<object[]> ApproximateSearchPolicyCases =>
        SearchCases.Select(row => new object[] { $"retained-integer-date / {row[0]}", row[1], row[2], row[3] });

    /// <summary>Scalar extraction must reach a native FHIR decimal, including integer-spelled resource tokens.</summary>
    [Theory]
    [MemberData(nameof(ApproximateSearchDecimalCases))]
    public async Task ApproximateSearchDecimalNumberReturnsExpectedIds(
        string label, FhirReleases.FhirSequenceCodes version, CompatibilityTransport transport,
        CompatibilityFormat format, string group)
    {
        Assert.NotEmpty(label);
        SearchGraph graph = ApproximateSearchGraph(Tenant(version, $"m7-number-{group}-{VersionName(version)}"), format, group);
        await using CompatibilityReplay replay = new(SearchScenario($"m7-number-{group}", graph), transport);
        await replay.RunAsync();
        foreach (SearchProbe probe in ApproximateSearchProbes(group, quantity: false))
        {
            await AssertSearchProbe(replay, graph, probe);
        }
    }

    /// <summary>Approximation retains missing-scalar and unit/system eligibility before comparing magnitudes.</summary>
    [Theory]
    [MemberData(nameof(ApproximateSearchQuantityCases))]
    public async Task ApproximateSearchQuantityReturnsExpectedIds(
        string label, FhirReleases.FhirSequenceCodes version, CompatibilityTransport transport,
        CompatibilityFormat format, string group)
    {
        Assert.NotEmpty(label);
        SearchGraph graph = ApproximateSearchGraph(Tenant(version, $"m7-quantity-{group}-{VersionName(version)}"), format, group);
        await using CompatibilityReplay replay = new(SearchScenario($"m7-quantity-{group}", graph), transport);
        await replay.RunAsync();
        foreach (SearchProbe probe in ApproximateSearchProbes(group, quantity: true))
        {
            await AssertSearchProbe(replay, graph, probe);
        }
    }

    /// <summary>No decimal repair may replace integer ±1 or precision-aware date approximation.</summary>
    [Theory]
    [MemberData(nameof(ApproximateSearchPolicyCases))]
    public async Task ApproximateSearchUnchangedIntegerAndDatePolicies(
        string label, FhirReleases.FhirSequenceCodes version, CompatibilityTransport transport, CompatibilityFormat format)
    {
        Assert.NotEmpty(label);
        SearchGraph graph = LoadSearchGraph(Tenant(version, $"m7-policies-{VersionName(version)}"), format);
        await using CompatibilityReplay replay = new(SearchScenario("m7-retained-policies", graph), transport);
        await replay.RunAsync();
        SearchProbe[] probes =
        [
            new("integer-ap", "Patient?multiplebirth=ap3", ["Patient/alpha", "Patient/beta"]),
            new("date-ap-expanded", "Patient?birthdate=ap1981", ["Patient/alpha", "Patient/gamma"]),
            new("date-ap-negative", "Patient?birthdate=ap2030", []),
        ];
        foreach (SearchProbe probe in probes)
        {
            await AssertSearchProbe(replay, graph, probe);
        }
    }

    /// <summary>Pin every family/group/version/format/transport row and the independently declared graph identities.</summary>
    [Fact]
    public void ApproximateSearchTheoryDataIsNonEmptyAndLabeled()
    {
        Assert.Equal(84, ApproximateSearchDecimalCases.Count());
        Assert.Equal(84, ApproximateSearchQuantityCases.Count());
        Assert.Equal(12, ApproximateSearchPolicyCases.Count());
        object[][] rows = ApproximateSearchDecimalCases.Concat(ApproximateSearchQuantityCases)
            .Concat(ApproximateSearchPolicyCases).ToArray();
        Assert.Equal(180, rows.Length);
        Assert.Equal(180, rows.Select(row => (string)row[0]).Distinct(StringComparer.Ordinal).Count());
        Assert.All(rows, row =>
        {
            Assert.Contains(row[1].ToString()!, (string)row[0], StringComparison.Ordinal);
            Assert.Contains(row[2].ToString()!, (string)row[0], StringComparison.Ordinal);
            Assert.Contains(row[3].ToString()!, (string)row[0], StringComparison.Ordinal);
            Assert.Contains(row.Length == 5 ? (string)row[4] : "retained-integer-date", (string)row[0], StringComparison.Ordinal);
        });
        foreach (object[] tuple in ReplayCases)
        {
            foreach ((string family, IEnumerable<object[]> cases) in new[]
            {
                ("decimal-number", ApproximateSearchDecimalCases), ("quantity", ApproximateSearchQuantityCases),
            })
            {
                foreach (string group in _approximateSearchGroups)
                {
                    object[] row = Assert.Single(cases, row =>
                        row[1].Equals(tuple[1]) && row[2].Equals(tuple[2]) && row[3].Equals(tuple[3]) && (string)row[4] == group);
                    Assert.Contains(family, (string)row[0], StringComparison.Ordinal);
                }
            }
            Assert.Single(ApproximateSearchPolicyCases, row =>
                row[1].Equals(tuple[1]) && row[2].Equals(tuple[2]) && row[3].Equals(tuple[3]));
        }
        foreach (FhirReleases.FhirSequenceCodes version in _versions)
        {
            foreach (CompatibilityFormat format in Enum.GetValues<CompatibilityFormat>())
            {
                foreach (string group in _approximateSearchGroups)
                {
                    SearchGraph graph = ApproximateSearchGraph(Tenant(version, $"m7-inventory-{VersionName(version)}"), format, group);
                    string[] candidates = group switch
                    {
                        "isolated-zero" => ["Observation/zero"],
                        "positive-window" => ["Observation/p899", "Observation/p900", "Observation/p901", "Observation/p905",
                            "Observation/p1000", "Observation/p1099", "Observation/p1100", "Observation/p1101", "Observation/p1110",
                            "Observation/zero", "Observation/opposite"],
                        "negative-window" => ["Observation/n899", "Observation/n900", "Observation/n901", "Observation/n905",
                            "Observation/n1000", "Observation/n1099", "Observation/n1100", "Observation/n1101", "Observation/n1110",
                            "Observation/zero", "Observation/opposite"],
                        "zero-window" => ["Observation/minus-one", "Observation/minus-tenth", "Observation/zero",
                            "Observation/plus-tenth", "Observation/plus-one"],
                        "lexical-prefix-isolation" => ["Observation/negative-hundred", "Observation/negative-one",
                            "Observation/zero", "Observation/one", "Observation/hundred", "Observation/two-hundred"],
                        "presence-units" => ["Observation/zero", "Observation/hundred", "Observation/absent", "Observation/extension-only"],
                        "range-safety" => ["Observation/p78", "Observation/p79", "Observation/n78", "Observation/n79", "Observation/zero"],
                        _ => throw new ArgumentOutOfRangeException(nameof(group)),
                    };
                    Assert.Equal("SearchParameter/m7-decimal", graph.Resources.Keys.First());
                    Assert.Equal(candidates.Append("SearchParameter/m7-decimal").Order(StringComparer.Ordinal),
                        graph.Resources.Keys.Order(StringComparer.Ordinal));
                    Assert.Equal(graph.Resources.Keys.Order(StringComparer.Ordinal), graph.Generated.Keys.Order(StringComparer.Ordinal));
                    Assert.All(graph.Resources, resource => Assert.Equal(resource.Key, SearchIdentity(resource.Value, format)));
                    Assert.NotEmpty(ApproximateSearchProbes(group, quantity: false));
                    Assert.NotEmpty(ApproximateSearchProbes(group, quantity: true));
                }
                SearchGraph retained = LoadSearchGraph(Tenant(version, $"m7-policy-inventory-{VersionName(version)}"), format);
                string[] retainedIds =
                [
                    "Organization/shared", "Organization/other", "Patient/beta", "Patient/gamma", "Patient/alpha", "Patient/missing",
                    "Observation/late", "Observation/twin", "Observation/early", "Observation/missing", "Encounter/visit",
                    "Encounter/other-visit", "SearchParameter/multiplebirth", "ValueSet/local-membership",
                ];
                Assert.Equal(retainedIds.Order(StringComparer.Ordinal), retained.Resources.Keys.Order(StringComparer.Ordinal));
            }
        }
    }

    private static IEnumerable<object[]> ApproximateSearchRows(string family) =>
        SearchRows(_approximateSearchGroups).Select(row =>
            new object[] { $"{family} / {row[0]}", row[1], row[2], row[3], row[4] });

    private static SearchGraph ApproximateSearchGraph(CompatibilityTenant tenant, CompatibilityFormat format, string group)
    {
        SearchGraph graph = new(tenant, format, new(StringComparer.Ordinal), new(StringComparer.Ordinal));
        string root = SearchCanonicalRoot(tenant.Version);
        string processing = tenant.Version == FhirReleases.FhirSequenceCodes.R5 ? ",\"processingMode\":\"normal\"" : string.Empty;
        string xmlProcessing = tenant.Version == FhirReleases.FhirSequenceCodes.R5 ? """<processingMode value="normal"/>""" : string.Empty;
        AddSearchControl(graph,
            $$$"""
            {"resourceType":"SearchParameter","id":"m7-decimal","url":"{{{root}}}/SearchParameter/m7-decimal",
             "name":"M7Decimal","status":"active","description":"M7 native decimal approximation",
             "code":"m7-decimal","base":["Observation"],"type":"number",
             "expression":"Observation.value.ofType(Quantity).value"{{{processing}}}}
            """,
            $"""
            <SearchParameter xmlns="http://hl7.org/fhir"><id value="m7-decimal"/><url value="{root}/SearchParameter/m7-decimal"/>
            <name value="M7Decimal"/><status value="active"/><description value="M7 native decimal approximation"/>
            <code value="m7-decimal"/><base value="Observation"/><type value="number"/>
            <expression value="Observation.value.ofType(Quantity).value"/>{xmlProcessing}</SearchParameter>
            """);

        // JSON tokens and XML attribute values are authored separately, never converted through an SDK model.
        (string Id, string Json, string Xml)[] values = group switch
        {
            "isolated-zero" => [("zero", "0.0", "0.0")],
            "positive-window" =>
            [
                ("p899", "89.9", "89.9"), ("p900", "90", "90"), ("p901", "90.1", "90.1"), ("p905", "90.5", "90.5"),
                ("p1000", "100", "100"), ("p1099", "109.9", "109.9"), ("p1100", "110", "110"),
                ("p1101", "110.1", "110.1"), ("p1110", "111", "111"), ("zero", "0", "0"), ("opposite", "-100", "-100"),
            ],
            "negative-window" =>
            [
                ("n899", "-89.9", "-89.9"), ("n900", "-90", "-90"), ("n901", "-90.1", "-90.1"), ("n905", "-90.5", "-90.5"),
                ("n1000", "-100", "-100"), ("n1099", "-109.9", "-109.9"), ("n1100", "-110", "-110"),
                ("n1101", "-110.1", "-110.1"), ("n1110", "-111", "-111"), ("zero", "0", "0"), ("opposite", "100", "100"),
            ],
            "zero-window" => [("minus-one", "-1", "-1"), ("minus-tenth", "-0.1", "-0.1"), ("zero", "0", "0"),
                ("plus-tenth", "0.1", "0.1"), ("plus-one", "1", "1")],
            "lexical-prefix-isolation" => [("negative-hundred", "-100", "-100"), ("negative-one", "-1", "-1"),
                ("zero", "0", "0"), ("one", "1", "1"), ("hundred", "100", "100"), ("two-hundred", "200", "200")],
            "presence-units" => [("zero", "0", "0"), ("hundred", "100", "100")],
            "range-safety" =>
            [
                ("p78", "78000000000000000000000000000", "78000000000000000000000000000"),
                ("p79", "79000000000000000000000000000", "79000000000000000000000000000"),
                ("n78", "-78000000000000000000000000000", "-78000000000000000000000000000"),
                ("n79", "-79000000000000000000000000000", "-79000000000000000000000000000"), ("zero", "0", "0"),
            ],
            _ => throw new ArgumentOutOfRangeException(nameof(group)),
        };
        foreach ((string id, string json, string xml) in values)
        {
            AddSearchControl(graph,
                $$$"""
                {"resourceType":"Observation","id":"{{{id}}}","status":"final","code":{"text":"M7 synthetic measurement"},
                 "valueQuantity":{"value":{{{json}}},"unit":"mg","system":"http://unitsofmeasure.org","code":"mg"}}
                """,
                $"""
                <Observation xmlns="http://hl7.org/fhir"><id value="{id}"/><status value="final"/><code><text value="M7 synthetic measurement"/></code>
                <valueQuantity><value value="{xml}"/><unit value="mg"/><system value="http://unitsofmeasure.org"/><code value="mg"/></valueQuantity></Observation>
                """);
        }
        if (group == "presence-units")
        {
            AddSearchControl(graph,
                """{"resourceType":"Observation","id":"absent","status":"final","code":{"text":"M7 absent value"}}""",
                """<Observation xmlns="http://hl7.org/fhir"><id value="absent"/><status value="final"/><code><text value="M7 absent value"/></code></Observation>""");
            AddSearchControl(graph,
                """
                {"resourceType":"Observation","id":"extension-only","status":"final","code":{"text":"M7 extension-only value"},
                 "valueQuantity":{"_value":{"extension":[{"url":"https://example.test/m7/no-scalar","valueString":"no scalar"}]},
                 "unit":"mg","system":"http://unitsofmeasure.org","code":"mg"}}
                """,
                """
                <Observation xmlns="http://hl7.org/fhir"><id value="extension-only"/><status value="final"/><code><text value="M7 extension-only value"/></code>
                <valueQuantity><value><extension url="https://example.test/m7/no-scalar"><valueString value="no scalar"/></extension></value>
                <unit value="mg"/><system value="http://unitsofmeasure.org"/><code value="mg"/></valueQuantity></Observation>
                """);
        }
        return graph;
    }

    private static SearchProbe ApproximateSearchProbe(string id, string value, string[] matches, bool quantity)
    {
        string parameter = quantity ? "value-quantity" : "m7-decimal";
        string operand = quantity ? string.Join(',', value.Split(',').Select(part => part + "|http://unitsofmeasure.org|mg")) : value;
        return new(id, $"Observation?{parameter}={operand}", matches);
    }

    private static SearchProbe[] ApproximateSearchProbes(string group, bool quantity)
    {
        SearchProbe Probe(string value, string[] matches) => ApproximateSearchProbe(group + "-" + value, value, matches, quantity);
        switch (group)
        {
            case "isolated-zero":
                return [Probe("ap0.0", ["Observation/zero"]), Probe("ap1.0", []), Probe("ap-1.0", []),
                    Probe("ap0", ["Observation/zero"]), Probe("ap1", []), Probe("ap-1", [])];
            case "positive-window":
                return
                [
                    Probe("ap100.0", ["Observation/p900", "Observation/p901", "Observation/p905", "Observation/p1000", "Observation/p1099", "Observation/p1100"]),
                    Probe("ap100", ["Observation/p900", "Observation/p901", "Observation/p905", "Observation/p1000", "Observation/p1099", "Observation/p1100"]),
                ];
            case "negative-window":
                return
                [
                    Probe("ap-100.0", ["Observation/n900", "Observation/n901", "Observation/n905", "Observation/n1000", "Observation/n1099", "Observation/n1100"]),
                    Probe("ap-100", ["Observation/n900", "Observation/n901", "Observation/n905", "Observation/n1000", "Observation/n1099", "Observation/n1100"]),
                ];
            case "zero-window":
                return [Probe("ap0.0", ["Observation/zero"]), Probe("ap0", ["Observation/zero"])];
            case "range-safety":
                return [Probe("ap78000000000000000000000000000.0", ["Observation/p78", "Observation/p79"]),
                    Probe("ap-78000000000000000000000000000.0", ["Observation/n78", "Observation/n79"]), Probe("ap0.0", ["Observation/zero"])];
            case "presence-units":
                List<SearchProbe> presence =
                [
                    Probe("ap0.0", ["Observation/zero"]), Probe("ap100.0", ["Observation/hundred"]),
                    Probe("ap0", ["Observation/zero"]), Probe("ap100", ["Observation/hundred"]),
                ];
                if (quantity)
                {
                    foreach ((string request, string[] matches) in new (string, string[])[]
                    {
                        ("ap0.0", ["Observation/zero"]), ("ap100.0", ["Observation/hundred"]),
                        ("ap0", ["Observation/zero"]), ("ap100", ["Observation/hundred"]),
                    })
                    {
                        foreach (string suffix in new[] { string.Empty, "||mg", "|http://unitsofmeasure.org|g", "|https://example.test/not-ucum|mg" })
                        {
                            presence.Add(new("presence-" + request + suffix, "Observation?value-quantity=" + request + suffix,
                                suffix is "" or "||mg" ? matches : []));
                        }
                    }
                }
                return presence.ToArray();
            case "lexical-prefix-isolation":
                List<SearchProbe> lexical = [];
                foreach ((string prefix, string[] matches) in new (string, string[])[]
                {
                    ("", ["Observation/hundred"]), ("eq", ["Observation/hundred"]),
                    ("ne", ["Observation/negative-hundred", "Observation/negative-one", "Observation/zero", "Observation/one", "Observation/two-hundred"]),
                    ("gt", ["Observation/two-hundred"]), ("sa", ["Observation/two-hundred"]),
                    ("ge", ["Observation/hundred", "Observation/two-hundred"]),
                    ("lt", ["Observation/negative-hundred", "Observation/negative-one", "Observation/zero", "Observation/one"]),
                    ("eb", ["Observation/negative-hundred", "Observation/negative-one", "Observation/zero", "Observation/one"]),
                    ("le", ["Observation/negative-hundred", "Observation/negative-one", "Observation/zero", "Observation/one", "Observation/hundred"]),
                })
                {
                    lexical.Add(Probe(prefix + "100.0", matches));
                    lexical.Add(Probe(prefix + "100", quantity ? matches : []));
                }
                lexical.Add(Probe("ap0,eq100", quantity ? ["Observation/zero", "Observation/hundred"] : ["Observation/zero"]));
                lexical.Add(Probe("eq100,ap0", quantity ? ["Observation/zero", "Observation/hundred"] : ["Observation/zero"]));
                if (!quantity)
                {
                    // RequestedValueLiteral preserves the whole self-link value, not just its eligible alternatives.
                    lexical.Add(Probe("apbad,ap0", ["Observation/zero"]));
                    lexical.Add(Probe("apbad,ap100", ["Observation/hundred"]));
                    lexical.Add(Probe("ap0.0,eq100", ["Observation/zero", "Observation/hundred"]));
                    lexical.Add(Probe("eq100,ap0.0", ["Observation/zero", "Observation/hundred"]));
                }
                return lexical.ToArray();
            default:
                throw new ArgumentOutOfRangeException(nameof(group));
        }
    }
}
