// <copyright file="Sdk6ContractTests.ValueSetMembership.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

extern alias candleR4;
extern alias candleR4B;
extern alias candleR5;

using System.Globalization;
using System.Net;
using fhir.candle.Tests.Extensions;
using fhir.candle.Tests.Models;
using FhirCandle.Models;
using FhirCandle.Storage;
using FhirCandle.Utils;
using Hl7.Fhir.Model;
using Hl7.Fhir.Specification.Terminology;
using Task = System.Threading.Tasks.Task;

namespace fhir.candle.Tests;

public partial class Sdk6ContractTests
{
    private const string ValueSetMembershipU = "https://example.org/ValueSet/m5";
    private const string ValueSetMembershipV = "https://example.org/ValueSet/m5-renamed";
    private const string ValueSetMembershipSystem = "https://example.org/m5/codes";
    private const string ValueSetMembershipTimestamp = "2026-01-02T03:04:05Z";
    private const string ValueSetMembershipEvidence =
        "Original-review M5; scratch/1119-08/bugreport.md synthetic two-coded-Observation oracle; " +
        "StoreTerminologyService exact lookup/extraction/inline registration; " +
        "ResourceStore successful mutation and synchronous publication boundaries. Authored raw JSON, not SDK snapshots.";
    private static readonly string[] _valueSetMembershipRepresentations = ["compose", "expansion"];
    private static readonly string[] _valueSetMembershipKinds =
        ["replace-rename-delete", "ordinary-delete", "remove-url", "empty-membership"];

    /// <summary>HTTP/JSON lifecycles cross every version, explicit representation, and mutation kind.</summary>
    public static IEnumerable<object[]> ValueSetMembershipLifecycleCases
    {
        get
        {
            foreach (object[] row in ReplayCases.Where(row =>
                (CompatibilityTransport)row[2] == CompatibilityTransport.Http &&
                (CompatibilityFormat)row[3] == CompatibilityFormat.Json))
            {
                foreach (string representation in _valueSetMembershipRepresentations)
                {
                    foreach (string kind in _valueSetMembershipKinds)
                    {
                        yield return [$"value-set-membership / {row[1]} / {row[2]} / {row[3]} / {representation} / {kind}",
                            row[1], row[2], row[3], representation, kind];
                    }
                }
            }
        }
    }

    /// <summary>Synchronous publication has one direct/JSON row per version.</summary>
    public static IEnumerable<object[]> ValueSetMembershipPublicationCases =>
        ReplayCases.Where(row => (CompatibilityTransport)row[2] == CompatibilityTransport.Direct &&
            (CompatibilityFormat)row[3] == CompatibilityFormat.Json)
        .Select(row => new object[] { $"value-set-membership-publication / {row[1]} / {row[2]} / {row[3]}",
            row[1], row[2], row[3] });

    /// <summary>Each SDK-facing row exercises both representations on independently owned instances.</summary>
    public static IEnumerable<object[]> ValueSetMembershipInlineCases =>
        _versions.Select(version => new object[] { $"value-set-membership-inline / {version} / Direct / SDK", version });

    /// <summary>Stored definitions and both public membership modifiers remain fresh and tenant-local.</summary>
    [Theory]
    [MemberData(nameof(ValueSetMembershipLifecycleCases))]
    public async Task ValueSetMembershipLifecycleIsFresh(
        string label, FhirReleases.FhirSequenceCodes version, CompatibilityTransport transport,
        CompatibilityFormat format, string representation, string kind)
    {
        Assert.NotEmpty(label);
        Assert.Equal(CompatibilityTransport.Http, transport);
        Assert.Equal(CompatibilityFormat.Json, format);
        SearchGraph primary = ValueSetMembershipGraph(Tenant(version, "m5-primary"));
        SearchGraph sibling = ValueSetMembershipGraph(Tenant(version, "m5-sibling"));
        CompatibilityScenario scenario = new(label, ValueSetMembershipEvidence, [primary.Tenant, sibling.Tenant],
            [MetadataStep(primary.Tenant, format), MetadataStep(sibling.Tenant, format)]);
        Assert.Empty(scenario.Seeds);
        Assert.Empty(primary.Resources);
        Assert.Empty(primary.Generated);
        Assert.Empty(sibling.Resources);
        Assert.Empty(sibling.Generated);
        await using CompatibilityReplay replay = new(scenario, transport);
        await replay.RunAsync();
        ValueSetMembershipResources a = await ValueSetMembershipPostResources(replay, primary, representation);
        ValueSetMembershipResources b = await ValueSetMembershipPostResources(replay, sibling, representation);
        string id = primary.Generated[a.ValueSet].ExpectedId!;
        GeneratedResourceExpectation original = primary.Generated[a.ValueSet];
        await ValueSetMembershipBoundary(replay, primary, a, sibling, b, "baseline", "codeA");

        switch (kind)
        {
            case "replace-rename-delete":
                await ValueSetMembershipUpdate(replay, primary, a.ValueSet,
                    ValueSetMembershipContent(representation, id, ValueSetMembershipU, "codeA"));
                await ValueSetMembershipBoundary(replay, primary, a, sibling, b, "unchanged-put", "codeA");
                string replacement = ValueSetMembershipContent(representation, id, ValueSetMembershipU, "codeB");
                await AssertExchangeFailure(replay, primary.Tenant, format, "stale-membership-change",
                    new(primary.Tenant.Name, "PUT", a.ValueSet)
                    {
                        Format = format, Content = replacement, IfMatchBinding = original.Name,
                    }, HttpStatusCode.PreconditionFailed, "exception",
                    "Conditional update query returned a match with version: 2, If-Match: ");
                await ValueSetMembershipBoundary(replay, primary, a, sibling, b, "stale-rejected", "codeA");
                await ValueSetMembershipUpdate(replay, primary, a.ValueSet, replacement);
                await ValueSetMembershipBoundary(replay, primary, a, sibling, b, "replaced", "codeB");
                await ValueSetMembershipUpdate(replay, primary, a.ValueSet,
                    ValueSetMembershipContent(representation, id, ValueSetMembershipV, "codeB"));
                await ValueSetMembershipBoundary(replay, primary, a, sibling, b, "renamed", null, "codeB");
                await ValueSetMembershipDelete(replay, primary, a.ValueSet, "renamed-delete");
                await ValueSetMembershipBoundary(replay, primary, a, sibling, b, "renamed-deleted", null);
                break;

            case "ordinary-delete":
                await ValueSetMembershipDelete(replay, primary, a.ValueSet, "ordinary-delete");
                await ValueSetMembershipBoundary(replay, primary, a, sibling, b, "ordinary-deleted", null);
                string upsert = ValueSetMembershipContent(representation, id, ValueSetMembershipU, "codeB");
                GeneratedResourceExpectation recreated = new($"{primary.Tenant.Name}-upsert-v1", "ValueSet", "1", id);
                await WriteSearchDefinition(replay, primary, "missing-id-put", a.ValueSet, upsert, recreated, HttpStatusCode.Created);
                primary.Resources.Add(a.ValueSet, upsert);
                primary.Generated.Add(a.ValueSet, recreated);
                await ValueSetMembershipBoundary(replay, primary, a, sibling, b, "upserted", "codeB");
                await ValueSetMembershipDelete(replay, primary, a.ValueSet, "upsert-delete");
                await ValueSetMembershipBoundary(replay, primary, a, sibling, b, "upsert-deleted", null);
                break;

            case "remove-url":
                await ValueSetMembershipUpdate(replay, primary, a.ValueSet,
                    ValueSetMembershipContent(representation, id, null, "codeA"));
                await ValueSetMembershipBoundary(replay, primary, a, sibling, b, "url-removed", null);
                await ValueSetMembershipUpdate(replay, primary, a.ValueSet,
                    ValueSetMembershipContent(representation, id, ValueSetMembershipU, "codeB"));
                await ValueSetMembershipBoundary(replay, primary, a, sibling, b, "url-restored", "codeB");
                await ValueSetMembershipUpdate(replay, primary, a.ValueSet,
                    ValueSetMembershipContent(representation, id, null, "codeB"));
                await ValueSetMembershipBoundary(replay, primary, a, sibling, b, "url-removed-again", null);
                await ValueSetMembershipDelete(replay, primary, a.ValueSet, "url-less-delete");
                await ValueSetMembershipBoundary(replay, primary, a, sibling, b, "url-less-deleted", null);
                break;

            case "empty-membership":
                await ValueSetMembershipUpdate(replay, primary, a.ValueSet,
                    ValueSetMembershipContent(representation, id, ValueSetMembershipU, null));
                await ValueSetMembershipBoundary(replay, primary, a, sibling, b, "empty-replacement", null);
                await ValueSetMembershipUpdate(replay, primary, a.ValueSet,
                    ValueSetMembershipContent(representation, id, ValueSetMembershipU, "codeB"));
                await ValueSetMembershipBoundary(replay, primary, a, sibling, b, "nonempty-restored", "codeB");
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(kind));
        }
    }

    /// <summary>Callbacks see committed membership; nested and throwing updates cannot undo its publication.</summary>
    [Theory]
    [MemberData(nameof(ValueSetMembershipPublicationCases))]
    public async Task ValueSetMembershipCommitsBeforePublication(
        string label, FhirReleases.FhirSequenceCodes version, CompatibilityTransport transport, CompatibilityFormat format)
    {
        Assert.NotEmpty(label);
        Assert.Equal(CompatibilityTransport.Direct, transport);
        Assert.Equal(CompatibilityFormat.Json, format);
        SearchGraph graph = ValueSetMembershipGraph(Tenant(version, "m5-publication"));
        await using CompatibilityReplay replay = new(new(label, ValueSetMembershipEvidence, [graph.Tenant],
            [MetadataStep(graph.Tenant, format)]), transport);
        await replay.RunAsync();
        string a = await ValueSetMembershipPost(replay, graph, "code-a", ValueSetMembershipObservation("codeA"));
        string b = await ValueSetMembershipPost(replay, graph, "code-b", ValueSetMembershipObservation("codeB"));
        IFhirStore store = replay.GetOwnedStore(graph.Tenant.Name);
        string initial = ValueSetMembershipContent("compose", null, ValueSetMembershipU, "codeA");
        string id = string.Empty;
        string identity = string.Empty;
        GeneratedResourceExpectation v1 = new("publication-v1", "ValueSet", "1");
        int creates = 0;
        void Created(object? sender, StoreInstanceEventArgs args)
        {
            if (args.ResourceType != "ValueSet")
            {
                return;
            }
            creates++;
            id = args.ResourceId;
            Assert.True(Guid.TryParseExact(id, "D", out _), "The create event must carry the server-assigned ID.");
            identity = "ValueSet/" + id;
            v1 = v1 with { ExpectedId = id };
            string content = WithWireId(initial, format, id);
            ValueSetMembershipSynchronousRead(replay, graph, store, identity, content, v1, "create-callback-read");
            graph.Resources.Add(identity, content);
            graph.Generated.Add(identity, v1);
            ValueSetMembershipSynchronousPairs(replay, graph, store, new(a, b, identity), "create-callback", "codeA");
        }
        store.OnInstanceCreated += Created;
        try
        {
            bool created = store.InstanceCreate(new(store, "POST", "ValueSet")
            {
                SourceContent = initial, SourceFormat = WireContractAssertions.MediaType(format),
                DestinationFormat = WireContractAssertions.MediaType(format),
            }, out FhirResponseContext response);
            AssertDirectExchangeResponse(ResourceResponse("publication-create", initial,
                v1 with { Mode = CompatibilityBindingMode.Reuse }, HttpStatusCode.Created),
                created, response, format, graph.Tenant, replay.Bindings);
        }
        finally
        {
            store.OnInstanceCreated -= Created;
        }
        Assert.Equal(1, creates);
        ValueSetMembershipResources resources = new(a, b, identity);
        await ValueSetMembershipRead(replay, graph, identity, "created-read");
        await ValueSetMembershipPairs(replay, graph, resources, "created", "codeA");

        string contentB = ValueSetMembershipContent("compose", id, ValueSetMembershipU, "codeB");
        string renamed = ValueSetMembershipContent("compose", id, ValueSetMembershipV, "codeB");
        GeneratedResourceExpectation v2 = new("publication-v2", "ValueSet", "2", id, PreviousBinding: v1.Name);
        GeneratedResourceExpectation v3 = new("publication-v3", "ValueSet", "3", id, PreviousBinding: v2.Name);
        bool entered = false;
        int updates = 0;
        int nestedWrites = 0;
        void Updated(object? sender, StoreInstanceEventArgs args)
        {
            if (args.ResourceType != "ValueSet" || args.ResourceId != id)
            {
                return;
            }
            updates++;
            if (entered)
            {
                return;
            }
            entered = true;
            ValueSetMembershipSynchronousRead(replay, graph, store, identity, contentB, v2, "update-callback-v2");
            graph.Resources[identity] = contentB;
            graph.Generated[identity] = v2;
            ValueSetMembershipSynchronousPairs(replay, graph, store, resources, "update-callback", "codeB");
            nestedWrites++;
            bool nested = store.InstanceUpdate(UpdateRequest(store, format, identity, renamed, "W/\"2\""),
                out FhirResponseContext response);
            AssertDirectExchangeResponse(ResourceResponse("nested-v3", renamed, v3, HttpStatusCode.OK),
                nested, response, format, graph.Tenant, replay.Bindings);
            graph.Resources[identity] = renamed;
            graph.Generated[identity] = v3;
            ValueSetMembershipSynchronousPairs(replay, graph, store, resources, "nested-return", null, "codeB");
        }
        store.OnInstanceUpdated += Updated;
        try
        {
            bool outer = store.InstanceUpdate(UpdateRequest(store, format, identity, contentB, "W/\"1\""),
                out FhirResponseContext response);
            AssertDirectExchangeResponse(ResourceResponse("outer-v2", contentB,
                v2 with { Mode = CompatibilityBindingMode.Reuse }, HttpStatusCode.OK),
                outer, response, format, graph.Tenant, replay.Bindings);
        }
        finally
        {
            store.OnInstanceUpdated -= Updated;
        }
        Assert.True(entered);
        Assert.Equal(2, updates);
        Assert.Equal(1, nestedWrites);
        await ValueSetMembershipRead(replay, graph, identity, "current-v3");
        await ValueSetMembershipPairs(replay, graph, resources, "outer-return", null, "codeB");

        InvalidOperationException expected = new("M5 synthetic post-assignment update callback");
        int throwingUpdates = 0;
        void ThrowAfterAssignment(object? sender, StoreInstanceEventArgs args)
        {
            if (args.ResourceType == "ValueSet" && args.ResourceId == id)
            {
                throwingUpdates++;
                throw expected;
            }
        }
        string changedV = ValueSetMembershipContent("compose", id, ValueSetMembershipV, "codeA");
        store.OnInstanceUpdated += ThrowAfterAssignment;
        try
        {
            InvalidOperationException actual = Assert.Throws<InvalidOperationException>(() =>
                store.InstanceUpdate(UpdateRequest(store, format, identity, changedV, "W/\"3\""), out _));
            Assert.Same(expected, actual);
        }
        finally
        {
            store.OnInstanceUpdated -= ThrowAfterAssignment;
        }
        Assert.Equal(1, throwingUpdates);
        GeneratedResourceExpectation v4 = new("publication-v4", "ValueSet", "4", id, PreviousBinding: v3.Name);
        // The exception has no successful response. Only this ordinary read binds committed version 4.
        await replay.StepAsync(new("read-committed-v4", new(graph.Tenant.Name, "GET", identity) { Format = format },
            ResourceResponse("read-committed-v4", changedV, v4, HttpStatusCode.OK)));
        graph.Resources[identity] = changedV;
        graph.Generated[identity] = v4;
        await ValueSetMembershipRead(replay, graph, identity, "current-v4");
        await ValueSetMembershipPairs(replay, graph, resources, "throwing-return", null, "codeA");

        int deletes = 0;
        void Deleted(object? sender, StoreInstanceEventArgs args)
        {
            if (args.ResourceType == "ValueSet" && args.ResourceId == id)
            {
                deletes++;
                ValueSetMembershipSynchronousPairs(replay, graph, store, resources, "delete-callback", null);
            }
        }
        store.OnInstanceDeleted += Deleted;
        try
        {
            await ValueSetMembershipDelete(replay, graph, identity, "publication-delete");
        }
        finally
        {
            store.OnInstanceDeleted -= Deleted;
        }
        Assert.Equal(1, deletes);
        await ValueSetMembershipPairs(replay, graph, resources, "deleted", null);
        await ValueSetMembershipRead(replay, graph, a, "publication-observation-a-unchanged");
        await ValueSetMembershipRead(replay, graph, b, "publication-observation-b-unchanged");
    }

    /// <summary>Inline registration, extraction, stored-create compatibility, and null/empty SDK URLs retain their contracts.</summary>
    [Theory]
    [MemberData(nameof(ValueSetMembershipInlineCases))]
    public async Task ValueSetMembershipInlineRegistrationKeepsFirstDefinition(
        string label, FhirReleases.FhirSequenceCodes version)
    {
        Assert.Contains("Direct / SDK", label, StringComparison.Ordinal);
        foreach (string representation in _valueSetMembershipRepresentations)
        {
            ICodeValidationTerminologyService service = ValueSetMembershipNewTerminology(version);
            await ValueSetMembershipValidate(service, ValueSetMembershipSdkDefinition(representation, ValueSetMembershipU, "codeA"), "codeA", true);
            ValueSetMembershipLookupControls(service);
            await ValueSetMembershipValidate(service, ValueSetMembershipSdkDefinition(representation, ValueSetMembershipU, "codeB"), "codeB", false);
            await ValueSetMembershipValidate(service, ValueSetMembershipSdkDefinition(representation, ValueSetMembershipU, "codeB"), "codeA", true);
            ValueSetMembershipLookupControls(service);

            ICodeValidationTerminologyService nullUrl = ValueSetMembershipNewTerminology(version);
            Parameters rejected = await ValueSetMembershipValidate(nullUrl,
                ValueSetMembershipSdkDefinition(representation, null, "codeA"), "codeA", false);
            Assert.Equal("No value set specified", rejected.GetSingleValue<FhirString>("message")?.Value);
            Assert.False(ValueSetMembershipContains(nullUrl, null, ValueSetMembershipSystem, "codeA"));
            Assert.False(ValueSetMembershipContains(nullUrl, string.Empty, ValueSetMembershipSystem, "codeA"));
            Assert.False(ValueSetMembershipContains(nullUrl, ValueSetMembershipU, ValueSetMembershipSystem, "codeA"));

            ICodeValidationTerminologyService emptyUrl = ValueSetMembershipNewTerminology(version);
            await ValueSetMembershipValidate(emptyUrl, ValueSetMembershipSdkDefinition(representation, string.Empty, "codeA"), "codeA", true);
            await ValueSetMembershipValidate(emptyUrl, ValueSetMembershipSdkDefinition(representation, string.Empty, "codeB"), "codeB", false);
            Assert.True(ValueSetMembershipContains(emptyUrl, string.Empty, ValueSetMembershipSystem, "codeA"));
            Assert.True(ValueSetMembershipContains(emptyUrl, string.Empty, null, "codeA"));
            Assert.True(ValueSetMembershipContains(emptyUrl, string.Empty, string.Empty, "codeA"));
            Assert.False(ValueSetMembershipContains(emptyUrl, string.Empty, ValueSetMembershipSystem, "codeB"));
            Assert.False(ValueSetMembershipContains(emptyUrl, null, ValueSetMembershipSystem, "codeA"));

            SearchGraph graph = ValueSetMembershipGraph(Tenant(version, "m5-inline-" + representation));
            await using (CompatibilityReplay replay = new(new(label + "-" + representation, ValueSetMembershipEvidence,
                [graph.Tenant], [MetadataStep(graph.Tenant, graph.Format)]), CompatibilityTransport.Direct))
            {
                await replay.RunAsync();
                ICodeValidationTerminologyService owned = ValueSetMembershipOwnedTerminology(replay.GetOwnedStore(graph.Tenant.Name));
                await ValueSetMembershipValidate(owned,
                    ValueSetMembershipSdkDefinition(representation, ValueSetMembershipU, "codeA"), "codeA", true);
                string identity = await ValueSetMembershipPost(replay, graph, "sole-stored-definition",
                    ValueSetMembershipContent(representation, null, ValueSetMembershipU, "codeB"));
                // The raw readback contains codeB, but create must preserve the prior inline snapshot.
                await ValueSetMembershipRead(replay, graph, identity, "inline-versus-created-read");
                Assert.True(ValueSetMembershipContains(owned, ValueSetMembershipU, ValueSetMembershipSystem, "codeA"));
                Assert.False(ValueSetMembershipContains(owned, ValueSetMembershipU, ValueSetMembershipSystem, "codeB"));
                await ValueSetMembershipUpdate(replay, graph, identity,
                    ValueSetMembershipContent(representation, graph.Generated[identity].ExpectedId, ValueSetMembershipU, "codeB"));
                Assert.False(ValueSetMembershipContains(owned, ValueSetMembershipU, ValueSetMembershipSystem, "codeA"));
                Assert.True(ValueSetMembershipContains(owned, ValueSetMembershipU, ValueSetMembershipSystem, "codeB"));
            }

            await ValueSetMembershipStoredEmptyUrl(label, version, representation);
        }
        await ValueSetMembershipExtractionControls(version);
    }

    /// <summary>Require the exact matrix, boundaries, formats, and unique dimension-bearing labels.</summary>
    [Fact]
    public void ValueSetMembershipTheoryDataIsNonEmptyAndLabeled()
    {
        object[][] lifecycle = ValueSetMembershipLifecycleCases.ToArray();
        object[][] publication = ValueSetMembershipPublicationCases.ToArray();
        object[][] inline = ValueSetMembershipInlineCases.ToArray();
        Assert.Equal(24, lifecycle.Length);
        Assert.Equal(3, publication.Length);
        Assert.Equal(3, inline.Length);
        object[][] all = lifecycle.Concat(publication).Concat(inline).ToArray();
        Assert.Equal(all.Length, all.Select(row => (string)row[0]).Distinct(StringComparer.Ordinal).Count());
        Assert.All(all, row => Assert.Contains(row[1].ToString()!, (string)row[0], StringComparison.Ordinal));
        Assert.All(lifecycle, row =>
        {
            Assert.Equal(6, row.Length);
            Assert.Equal(CompatibilityTransport.Http, row[2]);
            Assert.Equal(CompatibilityFormat.Json, row[3]);
            Assert.Contains((string)row[4], _valueSetMembershipRepresentations);
            Assert.Contains((string)row[5], _valueSetMembershipKinds);
            Assert.Contains((string)row[4], (string)row[0], StringComparison.Ordinal);
            Assert.Contains((string)row[5], (string)row[0], StringComparison.Ordinal);
        });
        Assert.All(publication, row =>
        {
            Assert.Equal(4, row.Length);
            Assert.Equal(CompatibilityTransport.Direct, row[2]);
            Assert.Equal(CompatibilityFormat.Json, row[3]);
        });
        Assert.All(lifecycle.Concat(publication), row =>
        {
            Assert.Contains(row[2].ToString()!, (string)row[0], StringComparison.Ordinal);
            Assert.Contains(row[3].ToString()!, (string)row[0], StringComparison.Ordinal);
        });
        Assert.All(inline, row =>
        {
            Assert.Equal(2, row.Length);
            Assert.Contains("Direct / SDK", (string)row[0], StringComparison.Ordinal);
        });
        foreach (FhirReleases.FhirSequenceCodes version in _versions)
        {
            foreach (string representation in _valueSetMembershipRepresentations)
            {
                foreach (string kind in _valueSetMembershipKinds)
                {
                    Assert.Single(lifecycle, row => row[1].Equals(version) &&
                        (string)row[4] == representation && (string)row[5] == kind);
                }
            }
            Assert.Single(publication, row => row[1].Equals(version));
            Assert.Single(inline, row => row[1].Equals(version));
        }
        _subscriptionOutput.WriteLine("M5 inventory: 24 HTTP/JSON lifecycle + three direct/JSON publication + three direct-SDK + one fact.");
    }

    private sealed record ValueSetMembershipResources(string CodeA, string CodeB, string ValueSet);

    private static SearchGraph ValueSetMembershipGraph(CompatibilityTenant tenant) =>
        new(tenant, CompatibilityFormat.Json, new(StringComparer.Ordinal), new(StringComparer.Ordinal));

    private static string ValueSetMembershipObservation(string code) =>
        $$$"""{"resourceType":"Observation","status":"final","code":{"coding":[{"system":"{{{ValueSetMembershipSystem}}}","code":"{{{code}}}"}]}}""";

    private static string ValueSetMembershipContent(string representation, string? id, string? url, string? code)
    {
        string identity = id is null ? string.Empty : $"""
            "id":"{id}",
            """;
        string canonical = url is null ? string.Empty : $"""
            "url":"{url}",
            """;
        string contents = representation switch
        {
            "compose" when code is not null =>
                $$$""","compose":{"include":[{"system":"{{{ValueSetMembershipSystem}}}","concept":[{"code":"{{{code}}}"}]}]}""",
            "compose" => string.Empty,
            "expansion" when code is not null =>
                $$$""","expansion":{"timestamp":"{{{ValueSetMembershipTimestamp}}}","contains":[{"system":"{{{ValueSetMembershipSystem}}}","code":"{{{code}}}"}]}""",
            "expansion" => $$$""","expansion":{"timestamp":"{{{ValueSetMembershipTimestamp}}}","total":0}""",
            _ => throw new ArgumentOutOfRangeException(nameof(representation)),
        };
        return $$$"""{"resourceType":"ValueSet",{{{identity}}}{{{canonical}}}"status":"active"{{{contents}}}}""";
    }

    private static async Task<ValueSetMembershipResources> ValueSetMembershipPostResources(
        CompatibilityReplay replay, SearchGraph graph, string representation)
    {
        string a = await ValueSetMembershipPost(replay, graph, "code-a", ValueSetMembershipObservation("codeA"));
        string b = await ValueSetMembershipPost(replay, graph, "code-b", ValueSetMembershipObservation("codeB"));
        string vs = await ValueSetMembershipPost(replay, graph, "definition",
            ValueSetMembershipContent(representation, null, ValueSetMembershipU, "codeA"));
        return new(a, b, vs);
    }

    private static async Task<string> ValueSetMembershipPost(
        CompatibilityReplay replay, SearchGraph graph, string name, string content)
    {
        string type = ExchangeResourceType(content, graph.Format);
        string step = graph.Tenant.Name + "-" + name;
        GeneratedResourceExpectation generated = new(step + "-v1", type, "1");
        await replay.StepAsync(new(step, new(graph.Tenant.Name, "POST", type) { Format = graph.Format, Content = content },
            ResourceResponse(step, content, generated, HttpStatusCode.Created)));
        string id = replay.Bindings.Resources[generated.Name].Id;
        string identity = type + "/" + id;
        graph.Resources.Add(identity, WithWireId(content, graph.Format, id));
        graph.Generated.Add(identity, generated with { ExpectedId = id });
        await ValueSetMembershipRead(replay, graph, identity, step + "-read");
        return identity;
    }

    private static Task<CompatibilityResponse> ValueSetMembershipRead(
        CompatibilityReplay replay, SearchGraph graph, string identity, string checkpoint) =>
        replay.StepAsync(BoundRead(checkpoint, graph.Tenant, graph.Format, graph.Resources[identity], graph.Generated[identity]));

    private static async Task ValueSetMembershipUpdate(
        CompatibilityReplay replay, SearchGraph graph, string identity, string content)
    {
        GeneratedResourceExpectation previous = graph.Generated[identity];
        string version = (int.Parse(previous.ExpectedVersion!, CultureInfo.InvariantCulture) + 1).ToString(CultureInfo.InvariantCulture);
        GeneratedResourceExpectation updated = new(previous.Name + "-next", "ValueSet", version,
            previous.ExpectedId, PreviousBinding: previous.Name);
        await WriteSearchDefinition(replay, graph, updated.Name, identity, content, updated, HttpStatusCode.OK, previous.Name);
        graph.Resources[identity] = content;
        graph.Generated[identity] = updated;
    }

    private static async Task ValueSetMembershipDelete(
        CompatibilityReplay replay, SearchGraph graph, string identity, string checkpoint)
    {
        await DeleteSearchDefinition(replay, graph, checkpoint, identity, graph.Resources[identity], graph.Generated[identity]);
        Assert.True(graph.Resources.Remove(identity));
        Assert.True(graph.Generated.Remove(identity));
    }

    private static (string[] In, string[] NotIn) ValueSetMembershipExpected(
        ValueSetMembershipResources resources, string? code) => code switch
    {
        "codeA" => ([resources.CodeA], [resources.CodeB]),
        "codeB" => ([resources.CodeB], [resources.CodeA]),
        null => ([], [resources.CodeA, resources.CodeB]),
        _ => throw new ArgumentOutOfRangeException(nameof(code)),
    };

    private static IEnumerable<SearchProbe> ValueSetMembershipProbes(
        ValueSetMembershipResources resources, string checkpoint, string? u, string? v)
    {
        foreach ((string canonical, string name, string? code) in new[]
        {
            (ValueSetMembershipU, "u", u), (ValueSetMembershipV, "v", v),
        })
        {
            (string[] positive, string[] negative) = ValueSetMembershipExpected(resources, code);
            yield return new(checkpoint + "-" + name + "-in", "Observation?code:in=" + canonical, positive);
            yield return new(checkpoint + "-" + name + "-not-in", "Observation?code:not-in=" + canonical, negative);
        }
    }

    private static async Task ValueSetMembershipPairs(
        CompatibilityReplay replay, SearchGraph graph, ValueSetMembershipResources resources,
        string checkpoint, string? u, string? v = null)
    {
        foreach (SearchProbe probe in ValueSetMembershipProbes(resources, checkpoint, u, v))
        {
            await AssertSearchRequest(replay, graph, probe, SearchRequest(graph, probe.Path, form: false, headers: null));
        }
    }

    private static async Task ValueSetMembershipBoundary(
        CompatibilityReplay replay, SearchGraph primary, ValueSetMembershipResources a,
        SearchGraph sibling, ValueSetMembershipResources b, string checkpoint, string? u, string? v = null)
    {
        await ValueSetMembershipPairs(replay, primary, a, checkpoint + "-a-first", u, v);
        await ValueSetMembershipPairs(replay, sibling, b, checkpoint + "-b", "codeA");
        await ValueSetMembershipPairs(replay, primary, a, checkpoint + "-a-again", u, v);
        foreach (SearchGraph graph in new[] { primary, sibling })
        {
            foreach (string identity in graph.Resources.Keys)
            {
                await ValueSetMembershipRead(replay, graph, identity, checkpoint + "-" + graph.Tenant.Name + "-" + identity);
            }
        }
    }

    private static void ValueSetMembershipSynchronousRead(
        CompatibilityReplay replay, SearchGraph graph, IFhirStore store, string identity, string content,
        GeneratedResourceExpectation generated, string checkpoint)
    {
        bool success = store.InstanceRead(new(store, "GET", identity)
        {
            DestinationFormat = WireContractAssertions.MediaType(graph.Format),
        }, out FhirResponseContext response);
        AssertDirectExchangeResponse(ResourceResponse(checkpoint, content, generated, HttpStatusCode.OK),
            success, response, graph.Format, graph.Tenant, replay.Bindings);
    }

    private static void ValueSetMembershipSynchronousPairs(
        CompatibilityReplay replay, SearchGraph graph, IFhirStore store, ValueSetMembershipResources resources,
        string checkpoint, string? u, string? v = null)
    {
        foreach (SearchProbe probe in ValueSetMembershipProbes(resources, checkpoint, u, v))
        {
            CompatibilityRequest request = SearchRequest(graph, probe.Path, form: false, headers: null);
            bool success = store.TypeSearch(new(store, "GET", request.Path)
            {
                DestinationFormat = WireContractAssertions.MediaType(graph.Format),
            }, out FhirResponseContext response);
            CompatibilityResponse actual = new(CompatibilityTransport.Direct, response.StatusCode!.Value, response.MimeType)
            {
                Direct = new(success, response.SerializedResource, response.SerializedOutcome, response.ResourceType,
                    response.Id, response.ETag, response.LastModified, response.Location),
            };
            AssertExchangeContainer(replay, graph.Tenant, graph.Format, probe.Id, actual, "Bundle", string.Empty,
                new(probe.Id + "-outcome", [new("information", "success", "Type search successful")]));
            string body = ExchangeResource(actual);
            Assert.Equal("Bundle", ExchangeResourceType(body, graph.Format));
            Assert.Equal("searchset", ExchangePrimitive(body, graph.Format, "type"));
            Assert.Equal(probe.Matches.Length, SearchTotal(body, graph.Format));
            string link = Assert.Single(ExchangeRepeated(body, graph.Format, "link"));
            AssertExchangeMembers(link, graph.Format, ["relation", "url"]);
            Assert.Equal("self", ExchangePrimitive(link, graph.Format, "relation"));
            Assert.Equal(graph.Tenant.BaseUrl + "/" + probe.Path,
                Uri.UnescapeDataString(ExchangePrimitive(link, graph.Format, "url")));
            string[] rawEntries = ExchangeHasMember(body, graph.Format, "entry")
                ? ExchangeRepeated(body, graph.Format, "entry") : [];
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
            AssertSearchIdentities(probe, entries, ordered: false, probe.Id);
            foreach (SearchEntry entry in entries)
            {
                Assert.Equal(graph.Tenant.BaseUrl + "/" + entry.Identity, entry.FullUrl);
                Assert.True(graph.Resources.ContainsKey(entry.Identity), "Undeclared match: " + entry.Identity);
                WireContractAssertions.Body(new ResourceBodyExpectation(graph.Resources[entry.Identity],
                    graph.Generated[entry.Identity] with { Mode = CompatibilityBindingMode.Reuse }),
                    entry.Content, graph.Format, graph.Tenant.BaseUrl, replay.Bindings);
            }
            AssertExchangeMembers(body, graph.Format, entries.Count == 0
                ? ["resourceType", "type", "total", "link"] : ["resourceType", "type", "total", "link", "entry"]);
        }
    }

    private static ICodeValidationTerminologyService ValueSetMembershipNewTerminology(FhirReleases.FhirSequenceCodes version) =>
        version switch
        {
            FhirReleases.FhirSequenceCodes.R4 => new candleR4::FhirCandle.Storage.StoreTerminologyService(),
            FhirReleases.FhirSequenceCodes.R4B => new candleR4B::FhirCandle.Storage.StoreTerminologyService(),
            FhirReleases.FhirSequenceCodes.R5 => new candleR5::FhirCandle.Storage.StoreTerminologyService(),
            _ => throw new ArgumentOutOfRangeException(nameof(version)),
        };

    private static ICodeValidationTerminologyService ValueSetMembershipOwnedTerminology(IFhirStore store) => store switch
    {
        candleR4::FhirCandle.Storage.VersionedFhirStore r4 => r4.Terminology,
        candleR4B::FhirCandle.Storage.VersionedFhirStore r4b => r4b.Terminology,
        candleR5::FhirCandle.Storage.VersionedFhirStore r5 => r5.Terminology,
        _ => throw new ArgumentException("Expected an owned R4/R4B/R5 store.", nameof(store)),
    };

    private static bool ValueSetMembershipContains(
        ICodeValidationTerminologyService service, string? url, string? system, string? code) => service switch
    {
        candleR4::FhirCandle.Storage.StoreTerminologyService r4 => r4.VsContains(url, system, code),
        candleR4B::FhirCandle.Storage.StoreTerminologyService r4b => r4b.VsContains(url, system, code),
        candleR5::FhirCandle.Storage.StoreTerminologyService r5 => r5.VsContains(url, system, code),
        _ => throw new ArgumentException("Expected a concrete local terminology service.", nameof(service)),
    };

    private static async Task<Parameters> ValueSetMembershipValidate(
        ICodeValidationTerminologyService service, ValueSet definition, string code, bool expected)
    {
        Parameters request = new()
        {
            Parameter =
            [
                new() { Name = "valueSet", Resource = definition },
                new() { Name = "system", Value = new FhirUri(ValueSetMembershipSystem) },
                new() { Name = "code", Value = new Code(code) },
            ],
        };
        Parameters result = await service.ValueSetValidateCode(request, id: null, useGet: false);
        Assert.Equal(expected, result.GetSingleValue<FhirBoolean>("result")?.Value);
        return result;
    }

    private static void ValueSetMembershipLookupControls(ICodeValidationTerminologyService service)
    {
        (string? Url, string? System, string? Code, bool Expected)[] controls =
        [
            (ValueSetMembershipU, ValueSetMembershipSystem, "codeA", true),
            (ValueSetMembershipU, null, "codeA", true),
            (ValueSetMembershipU, string.Empty, "codeA", true),
            (ValueSetMembershipU, ValueSetMembershipSystem, "codeB", false),
            (ValueSetMembershipU, null, "codeB", false),
            (ValueSetMembershipU, string.Empty, "codeB", false),
            (ValueSetMembershipU, "https://example.org/m5/wrong-system", "codeA", false),
            (ValueSetMembershipV, ValueSetMembershipSystem, "codeA", false),
            ("https://example.org/ValueSet/M5", ValueSetMembershipSystem, "codeA", false),
            (ValueSetMembershipU + "|1", ValueSetMembershipSystem, "codeA", false),
            (ValueSetMembershipU, ValueSetMembershipSystem, "CodeA", false),
            (ValueSetMembershipU, ValueSetMembershipSystem, null, false),
            (null, ValueSetMembershipSystem, "codeA", false),
            (string.Empty, ValueSetMembershipSystem, "codeA", false),
        ];
        foreach ((string? url, string? system, string? code, bool expected) in controls)
        {
            Assert.Equal(expected, ValueSetMembershipContains(service, url, system, code));
        }
    }

    private static ValueSet.ComposeComponent ValueSetMembershipSdkCompose(string code, string? system = ValueSetMembershipSystem) => new()
    {
        Include = [new() { System = system, Concept = [new() { Code = code }] }],
    };

    private static ValueSet.ExpansionComponent ValueSetMembershipSdkExpansion(string code, string? system = ValueSetMembershipSystem) => new()
    {
        TimestampElement = new FhirDateTime(ValueSetMembershipTimestamp),
        Contains = [new() { System = system, Code = code }],
    };

    private static ValueSet ValueSetMembershipSdkDefinition(string representation, string? url, string code, string? id = null) =>
        new()
        {
            Id = id, Url = url, Status = PublicationStatus.Active,
            Compose = representation == "compose" ? ValueSetMembershipSdkCompose(code) : null,
            Expansion = representation == "expansion" ? ValueSetMembershipSdkExpansion(code) : null,
        };

    private static async Task ValueSetMembershipExtractionControls(FhirReleases.FhirSequenceCodes version)
    {
        ICodeValidationTerminologyService precedence = ValueSetMembershipNewTerminology(version);
        await ValueSetMembershipValidate(precedence, new ValueSet
        {
            Url = ValueSetMembershipU, Status = PublicationStatus.Active,
            Compose = ValueSetMembershipSdkCompose("codeB"), Expansion = ValueSetMembershipSdkExpansion("codeA"),
        }, "codeA", true);
        Assert.True(ValueSetMembershipContains(precedence, ValueSetMembershipU, ValueSetMembershipSystem, "codeA"));
        Assert.False(ValueSetMembershipContains(precedence, ValueSetMembershipU, ValueSetMembershipSystem, "codeB"));

        ICodeValidationTerminologyService fallback = ValueSetMembershipNewTerminology(version);
        await ValueSetMembershipValidate(fallback, new ValueSet
        {
            Url = ValueSetMembershipU, Status = PublicationStatus.Active,
            Compose = ValueSetMembershipSdkCompose("codeB"),
            Expansion = new() { TimestampElement = new FhirDateTime(ValueSetMembershipTimestamp), Total = 0 },
        }, "codeB", true);
        Assert.False(ValueSetMembershipContains(fallback, ValueSetMembershipU, ValueSetMembershipSystem, "codeA"));
        Assert.True(ValueSetMembershipContains(fallback, ValueSetMembershipU, ValueSetMembershipSystem, "codeB"));

        ICodeValidationTerminologyService recursion = ValueSetMembershipNewTerminology(version);
        await ValueSetMembershipValidate(recursion, new ValueSet
        {
            Url = ValueSetMembershipU, Status = PublicationStatus.Active,
            Expansion = new()
            {
                TimestampElement = new FhirDateTime(ValueSetMembershipTimestamp),
                Contains = [new()
                {
                    System = ValueSetMembershipSystem, Code = "codeA",
                    Contains = [new() { System = ValueSetMembershipSystem, Code = "codeB" }],
                }],
            },
        }, "codeA", true);
        Assert.True(ValueSetMembershipContains(recursion, ValueSetMembershipU, ValueSetMembershipSystem, "codeA"));
        Assert.True(ValueSetMembershipContains(recursion, ValueSetMembershipU, ValueSetMembershipSystem, "codeB"));
        Assert.True(ValueSetMembershipContains(recursion, ValueSetMembershipU, null, "codeB"));

        ICodeValidationTerminologyService skippedSubtree = ValueSetMembershipNewTerminology(version);
        await ValueSetMembershipValidate(skippedSubtree, new ValueSet
        {
            Url = ValueSetMembershipU, Status = PublicationStatus.Active,
            Compose = ValueSetMembershipSdkCompose("codeA"),
            Expansion = new()
            {
                TimestampElement = new FhirDateTime(ValueSetMembershipTimestamp),
                Contains = [new()
                {
                    System = ValueSetMembershipSystem,
                    Contains = [new() { System = ValueSetMembershipSystem, Code = "codeB" }],
                }],
            },
        }, "codeB", false);
        Assert.False(ValueSetMembershipContains(skippedSubtree, ValueSetMembershipU, ValueSetMembershipSystem, "codeA"));
        Assert.False(ValueSetMembershipContains(skippedSubtree, ValueSetMembershipU, ValueSetMembershipSystem, "codeB"));
        Assert.False(ValueSetMembershipContains(skippedSubtree, ValueSetMembershipU, null, "codeB"));

        foreach (string representation in _valueSetMembershipRepresentations)
        {
            foreach (string? system in new string?[] { null, string.Empty })
            {
                ICodeValidationTerminologyService bare = ValueSetMembershipNewTerminology(version);
                await ValueSetMembershipValidate(bare, new ValueSet
                {
                    Url = ValueSetMembershipU, Status = PublicationStatus.Active,
                    Compose = representation == "compose" ? ValueSetMembershipSdkCompose("codeA", system) : null,
                    Expansion = representation == "expansion" ? ValueSetMembershipSdkExpansion("codeA", system) : null,
                }, "codeA", false);
                Assert.True(ValueSetMembershipContains(bare, ValueSetMembershipU, null, "codeA"));
                Assert.True(ValueSetMembershipContains(bare, ValueSetMembershipU, string.Empty, "codeA"));
                Assert.False(ValueSetMembershipContains(bare, ValueSetMembershipU, ValueSetMembershipSystem, "codeA"));
                Assert.False(ValueSetMembershipContains(bare, ValueSetMembershipU, null, "codeB"));
            }
        }
    }

    private static async Task ValueSetMembershipStoredEmptyUrl(
        string label, FhirReleases.FhirSequenceCodes version, string representation)
    {
        CompatibilityTenant tenant = Tenant(version, "m5-empty-url-" + representation);
        await using CompatibilityReplay replay = new(new(label + "-empty-url-" + representation, ValueSetMembershipEvidence,
            [tenant], [MetadataStep(tenant, CompatibilityFormat.Json)]), CompatibilityTransport.Direct);
        await replay.RunAsync();
        IFhirStore store = replay.GetOwnedStore(tenant.Name);
        ICodeValidationTerminologyService terminology = ValueSetMembershipOwnedTerminology(store);
        ValueSet created = ValueSetMembershipSdkWrite(store, "POST", "ValueSet",
            ValueSetMembershipSdkDefinition(representation, string.Empty, "codeA"), HttpStatusCode.Created, string.Empty, "1");
        string id = Assert.IsType<string>(created.Id);
        Assert.True(Guid.TryParseExact(id, "D", out _));
        string identity = "ValueSet/" + id;
        ValueSetMembershipSdkRead(store, identity, string.Empty, "1");
        ValueSetMembershipEmptyKey(terminology, a: true, b: false);

        ValueSetMembershipSdkWrite(store, "PUT", identity,
            ValueSetMembershipSdkDefinition(representation, string.Empty, "codeB", id), HttpStatusCode.OK, string.Empty, "2");
        ValueSetMembershipSdkRead(store, identity, string.Empty, "2");
        ValueSetMembershipEmptyKey(terminology, a: false, b: true);

        ValueSetMembershipSdkWrite(store, "PUT", identity,
            ValueSetMembershipSdkDefinition(representation, null, "codeB", id), HttpStatusCode.OK, null, "3");
        ValueSetMembershipSdkRead(store, identity, null, "3");
        ValueSetMembershipEmptyKey(terminology, a: false, b: false);

        ValueSetMembershipSdkWrite(store, "PUT", identity,
            ValueSetMembershipSdkDefinition(representation, string.Empty, "codeA", id), HttpStatusCode.OK, string.Empty, "4");
        ValueSetMembershipSdkRead(store, identity, string.Empty, "4");
        ValueSetMembershipEmptyKey(terminology, a: true, b: false);

        bool deleted = store.PerformInteraction(new(store, "DELETE", identity), out FhirResponseContext response, serializeReturn: false);
        Assert.True(deleted);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        ValueSetMembershipEmptyKey(terminology, a: false, b: false);
        bool found = store.PerformInteraction(new(store, "GET", identity), out FhirResponseContext missing, serializeReturn: false);
        Assert.False(found);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Null(missing.Resource);
    }

    private static ValueSet ValueSetMembershipSdkWrite(
        IFhirStore store, string method, string path, ValueSet input, HttpStatusCode status, string? url, string version)
    {
        // Every input is new: neither a returned SDK object nor the prior submitted object is mutated.
        bool success = store.PerformInteraction(new(store, method, path) { SourceObject = input },
            out FhirResponseContext response, serializeReturn: false);
        Assert.True(success);
        Assert.Equal(status, response.StatusCode);
        Assert.Empty(response.SerializedResource);
        ValueSet stored = Assert.IsType<ValueSet>(response.Resource);
        Meta meta = Assert.IsType<Meta>(stored.Meta);
        Assert.Equal(url, stored.Url);
        Assert.Equal(version, meta.VersionId);
        Assert.NotNull(meta.LastUpdated);
        Assert.Equal("ValueSet", response.ResourceType);
        Assert.Equal(stored.Id, response.Id);
        Assert.Equal($"W/\"{version}\"", response.ETag);
        Assert.Equal(store.Config.BaseUrl + "/ValueSet/" + stored.Id, response.Location);
        return stored;
    }

    private static void ValueSetMembershipSdkRead(IFhirStore store, string identity, string? url, string version)
    {
        bool success = store.PerformInteraction(new(store, "GET", identity), out FhirResponseContext response, serializeReturn: false);
        Assert.True(success);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        ValueSet stored = Assert.IsType<ValueSet>(response.Resource);
        Meta meta = Assert.IsType<Meta>(stored.Meta);
        Assert.Equal(identity, "ValueSet/" + stored.Id);
        Assert.Equal(url, stored.Url);
        Assert.Equal(version, meta.VersionId);
        Assert.NotNull(meta.LastUpdated);
        Assert.Equal($"W/\"{version}\"", response.ETag);
    }

    private static void ValueSetMembershipEmptyKey(ICodeValidationTerminologyService service, bool a, bool b)
    {
        Assert.Equal(a, ValueSetMembershipContains(service, string.Empty, ValueSetMembershipSystem, "codeA"));
        Assert.Equal(b, ValueSetMembershipContains(service, string.Empty, ValueSetMembershipSystem, "codeB"));
        Assert.Equal(a, ValueSetMembershipContains(service, string.Empty, null, "codeA"));
        Assert.Equal(b, ValueSetMembershipContains(service, string.Empty, string.Empty, "codeB"));
        Assert.False(ValueSetMembershipContains(service, null, ValueSetMembershipSystem, "codeA"));
        Assert.False(ValueSetMembershipContains(service, null, ValueSetMembershipSystem, "codeB"));
        Assert.False(ValueSetMembershipContains(service, ValueSetMembershipU, ValueSetMembershipSystem, "codeA"));
        Assert.False(ValueSetMembershipContains(service, ValueSetMembershipU, ValueSetMembershipSystem, "codeB"));
    }
}
