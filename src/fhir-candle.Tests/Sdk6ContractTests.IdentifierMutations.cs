// <copyright file="Sdk6ContractTests.IdentifierMutations.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using System.Globalization;
using System.Net;
using fhir.candle.Tests.Extensions;
using fhir.candle.Tests.Models;
using FhirCandle.Models;
using FhirCandle.Storage;
using FhirCandle.Utils;

namespace fhir.candle.Tests;

public partial class Sdk6ContractTests
{
    private const string IdentifierMutationSystem = "https://example.test/m4";
    private const string IdentifierMutationEvidence =
        "Original-review M4; ResourceStore SDK IIdentifiable list/scalar create contract; " +
        "Sdk6ContractTests.Search AssertSearchProbe; ConditionalInteractionsPreserveStateForExplicitCreate; " +
        "UpdateRejectionUnexpectedExceptionRetainsBoundaryBehavior. Independently authored synthetic JSON/XML.";
    private static readonly string[] _identifierMutationKinds =
        ["replace", "put-create", "delete-reuse", "non-owner-update", "non-owner-delete", "remove"];
    private static readonly string[] _identifierMutationShapes = ["list", "scalar"];

    /// <summary>Every lifecycle/shape/version/boundary/format combination owns a fresh replay.</summary>
    public static IEnumerable<object[]> IdentifierMutationLifecycleCases
    {
        get
        {
            foreach (object[] row in ReplayCases)
            {
                foreach (string shape in _identifierMutationShapes)
                {
                    foreach (string kind in _identifierMutationKinds)
                    {
                        yield return [$"identifier-mutation / {row[1]} / {row[2]} / {row[3]} / {shape} / {kind}",
                            row[1], row[2], row[3], shape, kind];
                    }
                }
            }
        }
    }

    /// <summary>Synchronous publication is exercised directly in both independently authored formats.</summary>
    public static IEnumerable<object[]> IdentifierMutationReentrantCases =>
        ReplayCases.Where(row => (CompatibilityTransport)row[2] == CompatibilityTransport.Direct)
            .Select(row => new object[] { $"identifier-mutation-reentrant / {row[1]} / {row[2]} / {row[3]}",
                row[1], row[2], row[3] });

    /// <summary>Logical includes follow committed identifiers, without evicting another live owner.</summary>
    [Theory]
    [MemberData(nameof(IdentifierMutationLifecycleCases))]
    public async Task IdentifierMutationLifecyclePreservesLogicalIncludes(
        string label, FhirReleases.FhirSequenceCodes version, CompatibilityTransport transport,
        CompatibilityFormat format, string shape, string kind)
    {
        Assert.NotEmpty(label);
        SearchGraph graph = IdentifierMutationGraph(Tenant(version, "m4-primary"), format);
        SearchGraph sibling = IdentifierMutationGraph(Tenant(version, "m4-sibling"), format);
        CompatibilityScenario scenario = new(label, IdentifierMutationEvidence, [graph.Tenant, sibling.Tenant],
            [MetadataStep(graph.Tenant, format), MetadataStep(sibling.Tenant, format)]);
        Assert.Empty(scenario.Seeds);
        await using CompatibilityReplay replay = new(scenario, transport);
        await replay.RunAsync();

        string[] originalKeys = shape == "list" && kind == "replace" ? ["A", "R"] : ["A"];
        string target = await IdentifierMutationPost(replay, graph, "target",
            IdentifierMutationTarget(format, shape, null, originalKeys));
        string id = graph.Generated[target].ExpectedId!;
        string a = await IdentifierMutationPost(replay, graph, "ref-a", IdentifierMutationReferrer(format, shape, "A"));
        string twin = await IdentifierMutationPost(replay, graph, "ref-a-twin", IdentifierMutationReferrer(format, shape, "A", marker: "twin"));
        string b = await IdentifierMutationPost(replay, graph, "ref-b", IdentifierMutationReferrer(format, shape, "B"));
        await IdentifierMutationInclude(replay, graph, shape, "initial-post-ownership", [a, twin], target);
        _subscriptionOutput.WriteLine($"M4 initial POST/read/include established: {label}");

        // Only this sibling ingest uses the direct explicit-ID override; the primary was POST-created.
        string siblingContent = IdentifierMutationTarget(format, shape, id, ["A"], language: "de");
        GeneratedResourceExpectation siblingVersion = new("sibling-target-v1", graph.Generated[target].ResourceType, "1", id);
        IFhirStore siblingStore = replay.GetOwnedStore(sibling.Tenant.Name);
        FhirRequestContext ingest = new(siblingStore, "POST", siblingVersion.ResourceType)
        {
            SourceContent = siblingContent, SourceFormat = WireContractAssertions.MediaType(format),
            DestinationFormat = WireContractAssertions.MediaType(format),
        };
        bool ingested = siblingStore.InstanceCreate(ingest, out FhirResponseContext siblingResponse, forceAllowExistingId: true);
        AssertDirectExchangeResponse(ResourceResponse("sibling-ingest", siblingContent, siblingVersion, HttpStatusCode.Created),
            ingested, siblingResponse, format, sibling.Tenant, replay.Bindings);
        sibling.Resources.Add(target, siblingContent);
        sibling.Generated.Add(target, siblingVersion);
        string siblingRef = await IdentifierMutationPost(replay, sibling, "sibling-ref", IdentifierMutationReferrer(format, shape, "A"));
        await IdentifierMutationRead(replay, sibling, target, "sibling-initial");
        await IdentifierMutationInclude(replay, sibling, shape, "sibling-initial", [siblingRef], target);

        string literal = await IdentifierMutationPost(replay, graph, "ref-literal",
            IdentifierMutationReferrer(format, shape, "A", literal: target));
        string untyped = await IdentifierMutationPost(replay, graph, "ref-untyped",
            IdentifierMutationReferrer(format, shape, "A", omitType: true));
        await IdentifierMutationInclude(replay, graph, shape, "literal-initial", [literal], target);
        await IdentifierMutationInclude(replay, graph, shape, "untyped-initial", [untyped], target);
        await IdentifierMutationSearchControls(replay, graph, shape, target, [a, twin, untyped], "A", "initial");

        switch (kind)
        {
            case "replace":
                await IdentifierMutationUpdate(replay, graph, target,
                    IdentifierMutationTarget(format, shape, id, originalKeys, language: "fr"));
                await IdentifierMutationInclude(replay, graph, shape, "unchanged-key", [a, twin], target);
                string replacement = IdentifierMutationTarget(format, shape, id,
                    shape == "list" ? ["B", "R"] : ["B"], language: "en-US");
                await AssertExchangeFailure(replay, graph.Tenant, format, "stale-change",
                    new CompatibilityRequest(graph.Tenant.Name, "PUT", target)
                    {
                        Format = format, Content = replacement, IfMatchBinding = "target-v1",
                    }, HttpStatusCode.PreconditionFailed, "exception",
                    format == CompatibilityFormat.Xml
                        ? "Conditional update query returned a match with version: 2, If-Match:"
                        : "Conditional update query returned a match with version: 2, If-Match: ");
                await IdentifierMutationRead(replay, graph, target, "after-stale");
                await IdentifierMutationInclude(replay, graph, shape, "after-stale", [a], target);
                await IdentifierMutationUpdate(replay, graph, target, replacement);
                await IdentifierMutationInclude(replay, graph, shape, "departed-a", [a]);
                await IdentifierMutationInclude(replay, graph, shape, "current-b", [b], target);
                await IdentifierMutationInclude(replay, graph, shape, "literal-after-replace", [literal], target);
                await IdentifierMutationInclude(replay, graph, shape, "untyped-after-replace", [untyped]);
                await IdentifierMutationSearchControls(replay, graph, shape, target, [a, twin, untyped], "A", "replaced", targetMatches: false);
                await IdentifierMutationSearchControls(replay, graph, shape, target, [b], "B", "current");
                if (shape == "list")
                {
                    string retained = await IdentifierMutationPost(replay, graph, "ref-retained", IdentifierMutationReferrer(format, shape, "R"));
                    await IdentifierMutationInclude(replay, graph, shape, "retained-r", [retained], target);
                }
                await IdentifierMutationCollision(replay, graph, shape);
                break;

            case "put-create":
                string putTarget = graph.Generated[target].ResourceType + "/m4-put-create";
                await IdentifierMutationMissing(replay, graph, putTarget, "before-put");
                string putContent = IdentifierMutationTarget(format, shape, "m4-put-create", ["B"]);
                GeneratedResourceExpectation putVersion = new("put-v1", graph.Generated[target].ResourceType, "1", "m4-put-create");
                await replay.StepAsync(new("put-create", new(graph.Tenant.Name, "PUT", putTarget)
                {
                    Format = format, Content = putContent,
                }, ResourceResponse("put-create", putContent, putVersion, HttpStatusCode.Created)));
                graph.Resources.Add(putTarget, putContent);
                graph.Generated.Add(putTarget, putVersion);
                await IdentifierMutationRead(replay, graph, putTarget, "after-put");
                await IdentifierMutationInclude(replay, graph, shape, "put-created-b", [b], putTarget);
                await IdentifierMutationInclude(replay, graph, shape, "post-owner-after-put", [a], target);
                break;

            case "delete-reuse":
                await IdentifierMutationDelete(replay, graph, target);
                await IdentifierMutationInclude(replay, graph, shape, "deleted-a", [a, twin]);
                await IdentifierMutationInclude(replay, graph, shape, "literal-deleted", [literal]);
                string reused = await IdentifierMutationPost(replay, graph, "reuse",
                    IdentifierMutationTarget(format, shape, null, ["A"], language: "es"));
                Assert.NotEqual(target, reused);
                await IdentifierMutationInclude(replay, graph, shape, "reused-a", [a, twin], reused);
                await IdentifierMutationInclude(replay, graph, shape, "old-literal-after-reuse", [literal]);
                break;

            case "non-owner-update":
            case "non-owner-delete":
                string duplicate = await IdentifierMutationPost(replay, graph, "duplicate",
                    IdentifierMutationTarget(format, shape, null, ["A"], language: "it"));
                await IdentifierMutationInclude(replay, graph, shape, "first-owner-before-duplicate-mutation", [a], target);
                if (kind == "non-owner-update")
                {
                    await IdentifierMutationUpdate(replay, graph, duplicate,
                        IdentifierMutationTarget(format, shape, graph.Generated[duplicate].ExpectedId, ["B"], language: "pt"));
                    await IdentifierMutationInclude(replay, graph, shape, "non-owner-new-b", [b], duplicate);
                }
                else
                {
                    await IdentifierMutationDelete(replay, graph, duplicate);
                }
                await IdentifierMutationInclude(replay, graph, shape, "owner-survives", [a, twin], target);
                break;

            case "remove":
                string emptyRequest = IdentifierMutationTarget(format, shape, id, [], explicitEmpty: true);
                // FHIR JSON forbids empty arrays. Retain the explicit [] negative fixture, then
                // clear via omission; do not normalize [] into an absent response collection.
                string emptyResponse = IdentifierMutationTarget(format, shape, id, []);
                if (shape == "list" && format == CompatibilityFormat.Json)
                {
                    CompatibilityResponse rejected = await replay.SendAsync(new(graph.Tenant.Name, "PUT", target)
                    {
                        Format = format, Content = emptyRequest, IfMatchBinding = graph.Generated[target].Name,
                    });
                    AssertExchangeFailureResponse(replay.Bindings, graph.Tenant, format, "empty-array", rejected,
                        HttpStatusCode.UnprocessableEntity, "structure", null, checkMediaType: false);
                    string outcome = rejected.HttpBody ?? rejected.Direct!.SerializedOutcome;
                    string issue = Assert.Single(ExchangeRepeated(outcome, format, "issue"));
                    string diagnostic = ExchangePrimitive(issue, format, "diagnostics");
                    Assert.StartsWith("Failed to deserialize resource, format: ", diagnostic, StringComparison.Ordinal);
                    _subscriptionOutput.WriteLine("M4 invalid empty-array fixture: " + diagnostic);
                    await IdentifierMutationRead(replay, graph, target, "after-rejected-empty-array");
                    await IdentifierMutationInclude(replay, graph, shape, "after-rejected-empty-array", [a], target);
                }
                await IdentifierMutationUpdate(replay, graph, target, emptyResponse);
                await IdentifierMutationInclude(replay, graph, shape, "cleared-a", [a]);
                await IdentifierMutationInclude(replay, graph, shape, "literal-cleared", [literal], target);
                await IdentifierMutationUpdate(replay, graph, target, IdentifierMutationTarget(format, shape, id, ["A"]));
                await IdentifierMutationInclude(replay, graph, shape, "reintroduced-a", [a], target);
                await IdentifierMutationUpdate(replay, graph, target, emptyResponse);
                await IdentifierMutationInclude(replay, graph, shape, "omitted-a", [a]);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(kind));
        }

        await IdentifierMutationRead(replay, sibling, target, "sibling-after-primary");
        await IdentifierMutationInclude(replay, sibling, shape, "sibling-after-primary", [siblingRef], target);
        await IdentifierMutationEmptyComponents(replay, graph, shape);
    }

    /// <summary>Callbacks see the committed index, nested writes win, and a throwing publication cannot skip maintenance.</summary>
    [Theory]
    [MemberData(nameof(IdentifierMutationReentrantCases))]
    public async Task IdentifierMutationReentrantWriteCommitsLatestKeysBeforePublication(
        string label, FhirReleases.FhirSequenceCodes version, CompatibilityTransport transport, CompatibilityFormat format)
    {
        Assert.NotEmpty(label);
        Assert.Equal(CompatibilityTransport.Direct, transport);
        SearchGraph graph = IdentifierMutationGraph(Tenant(version, "m4-reentrant"), format);
        await using CompatibilityReplay replay = new(new(label, IdentifierMutationEvidence, [graph.Tenant],
            [MetadataStep(graph.Tenant, format)]), transport);
        await replay.RunAsync();
        string target = await IdentifierMutationPost(replay, graph, "reentrant-target",
            IdentifierMutationTarget(format, "list", null, ["A"]));
        Dictionary<string, string> refs = new(StringComparer.Ordinal);
        foreach (string key in new[] { "A", "B", "C", "D" })
        {
            refs.Add(key, await IdentifierMutationPost(replay, graph, "reentrant-ref-" + key,
                IdentifierMutationReferrer(format, "list", key)));
        }
        await IdentifierMutationInclude(replay, graph, "list", "reentrant-initial", [refs["A"]], target);
        _subscriptionOutput.WriteLine($"M4 initial POST/read/include established: {label}");
        IFhirStore store = replay.GetOwnedStore(graph.Tenant.Name);
        string id = graph.Generated[target].ExpectedId!;
        string contentB = IdentifierMutationTarget(format, "list", id, ["B"], language: "fr");
        string contentC = IdentifierMutationTarget(format, "list", id, ["C"], language: "de");
        GeneratedResourceExpectation versionA = graph.Generated[target];
        GeneratedResourceExpectation versionB = new("reentrant-v2", "Patient", "2", id, PreviousBinding: versionA.Name);
        GeneratedResourceExpectation versionC = new("reentrant-v3", "Patient", "3", id, PreviousBinding: versionB.Name);
        bool entered = false;
        int notifications = 0;
        int nestedWrites = 0;
        void Reenter(object? sender, StoreInstanceEventArgs args)
        {
            if (args.ResourceType != "Patient" || args.ResourceId != id)
            {
                return;
            }
            notifications++;
            if (entered)
            {
                return;
            }
            entered = true;
            // Capture version 2 from an ordinary synchronous read, not the unfinished outer response.
            FhirRequestContext read = new(store, "GET", target) { DestinationFormat = WireContractAssertions.MediaType(format) };
            bool readSuccess = store.InstanceRead(read, out FhirResponseContext current);
            AssertDirectExchangeResponse(ResourceResponse("callback-read-b", contentB, versionB, HttpStatusCode.OK),
                readSuccess, current, format, graph.Tenant, replay.Bindings);
            graph.Resources[target] = contentB;
            graph.Generated[target] = versionB;
            IdentifierMutationSynchronousInclude(replay, graph, store, "callback-departed-a", refs["A"]);
            IdentifierMutationSynchronousInclude(replay, graph, store, "callback-current-b", refs["B"], target);
            nestedWrites++;
            bool nested = store.InstanceUpdate(UpdateRequest(store, format, target, contentC, "W/\"2\""), out FhirResponseContext response);
            AssertDirectExchangeResponse(ResourceResponse("nested-c", contentC, versionC, HttpStatusCode.OK),
                nested, response, format, graph.Tenant, replay.Bindings);
            graph.Resources[target] = contentC;
            graph.Generated[target] = versionC;
        }
        store.OnInstanceUpdated += Reenter;
        try
        {
            bool outer = store.InstanceUpdate(UpdateRequest(store, format, target, contentB, "W/\"1\""), out FhirResponseContext response);
            AssertDirectExchangeResponse(ResourceResponse("outer-b", contentB,
                versionB with { Mode = CompatibilityBindingMode.Reuse }, HttpStatusCode.OK),
                outer, response, format, graph.Tenant, replay.Bindings);
        }
        finally
        {
            store.OnInstanceUpdated -= Reenter;
        }
        Assert.True(entered);
        Assert.Equal(2, notifications);
        Assert.Equal(1, nestedWrites);
        await IdentifierMutationRead(replay, graph, target, "latest-c");
        foreach (string key in refs.Keys)
        {
            await IdentifierMutationInclude(replay, graph, "list", "after-reentrant-" + key, [refs[key]],
                key == "C" ? [target] : []);
        }

        InvalidOperationException expected = new("M4 synthetic post-assignment update callback");
        int throwingNotifications = 0;
        void ThrowAfterAssignment(object? sender, StoreInstanceEventArgs args)
        {
            if (args.ResourceType == "Patient" && args.ResourceId == id)
            {
                throwingNotifications++;
                throw expected;
            }
        }
        string contentD = IdentifierMutationTarget(format, "list", id, ["D"], language: "es");
        store.OnInstanceUpdated += ThrowAfterAssignment;
        try
        {
            InvalidOperationException actual = Assert.Throws<InvalidOperationException>(() =>
                store.InstanceUpdate(UpdateRequest(store, format, target, contentD, "W/\"3\""), out _));
            Assert.Same(expected, actual);
        }
        finally
        {
            store.OnInstanceUpdated -= ThrowAfterAssignment;
        }
        Assert.Equal(1, throwingNotifications);
        GeneratedResourceExpectation versionD = new("reentrant-v4", "Patient", "4", id, PreviousBinding: versionC.Name);
        // No update response exists after the exception. Bind committed metadata from this ordinary read.
        await replay.StepAsync(new("read-committed-d", new(graph.Tenant.Name, "GET", target) { Format = format },
            ResourceResponse("read-committed-d", contentD, versionD, HttpStatusCode.OK)));
        graph.Resources[target] = contentD;
        graph.Generated[target] = versionD;
        await IdentifierMutationRead(replay, graph, target, "latest-d");
        foreach (string key in refs.Keys)
        {
            await IdentifierMutationInclude(replay, graph, "list", "after-exception-" + key, [refs[key]],
                key == "D" ? [target] : []);
        }
    }

    /// <summary>Require the complete Cartesian inventory, not merely aggregate dimension membership.</summary>
    [Fact]
    public void IdentifierMutationTheoryDataIsNonEmptyAndLabeled()
    {
        object[][] lifecycle = IdentifierMutationLifecycleCases.ToArray();
        object[][] reentrant = IdentifierMutationReentrantCases.ToArray();
        Assert.Equal(144, lifecycle.Length);
        Assert.Equal(6, reentrant.Length);
        foreach (object[][] rows in new[] { lifecycle, reentrant })
        {
            Assert.Equal(rows.Length, rows.Select(row => (string)row[0]).Distinct(StringComparer.Ordinal).Count());
            Assert.All(rows, row =>
            {
                Assert.Contains(row[1].ToString()!, (string)row[0], StringComparison.Ordinal);
                Assert.Contains(row[2].ToString()!, (string)row[0], StringComparison.Ordinal);
                Assert.Contains(row[3].ToString()!, (string)row[0], StringComparison.Ordinal);
            });
        }
        foreach (object[] foundation in ReplayCases)
        {
            foreach (string shape in _identifierMutationShapes)
            {
                foreach (string kind in _identifierMutationKinds)
                {
                    object[] row = Assert.Single(lifecycle, row => row[1].Equals(foundation[1]) &&
                        row[2].Equals(foundation[2]) && row[3].Equals(foundation[3]) &&
                        (string)row[4] == shape && (string)row[5] == kind);
                    Assert.Contains(shape, (string)row[0], StringComparison.Ordinal);
                    Assert.Contains(kind, (string)row[0], StringComparison.Ordinal);
                }
            }
            if ((CompatibilityTransport)foundation[2] == CompatibilityTransport.Direct)
            {
                Assert.Single(reentrant, row => row[1].Equals(foundation[1]) &&
                    row[2].Equals(foundation[2]) && row[3].Equals(foundation[3]));
            }
        }
        Assert.All(reentrant, row => Assert.Equal(CompatibilityTransport.Direct, row[2]));
        _subscriptionOutput.WriteLine("M4 inventory: 144 lifecycle + six direct reentrant + one fact.");
    }

    private static SearchGraph IdentifierMutationGraph(CompatibilityTenant tenant, CompatibilityFormat format) =>
        new(tenant, format, new(StringComparer.Ordinal), new(StringComparer.Ordinal));

    private static async Task<string> IdentifierMutationPost(
        CompatibilityReplay replay, SearchGraph graph, string name, string content)
    {
        string type = ExchangeResourceType(content, graph.Format);
        GeneratedResourceExpectation generated = new(name + "-v1", type, "1");
        await replay.StepAsync(new(name, new(graph.Tenant.Name, "POST", type) { Format = graph.Format, Content = content },
            ResourceResponse(name, content, generated, HttpStatusCode.Created)));
        string id = replay.Bindings.Resources[generated.Name].Id;
        string identity = type + "/" + id;
        graph.Resources.Add(identity, WithWireId(content, graph.Format, id));
        graph.Generated.Add(identity, generated with { ExpectedId = id });
        await IdentifierMutationRead(replay, graph, identity, "post-read-" + name);
        return identity;
    }

    private static async Task IdentifierMutationUpdate(
        CompatibilityReplay replay, SearchGraph graph, string identity, string expectedContent, string? requestContent = null)
    {
        GeneratedResourceExpectation previous = graph.Generated[identity];
        int version = int.Parse(previous.ExpectedVersion!, CultureInfo.InvariantCulture) + 1;
        GeneratedResourceExpectation updated = new(previous.Name + "-next", previous.ResourceType,
            version.ToString(CultureInfo.InvariantCulture), previous.ExpectedId, PreviousBinding: previous.Name);
        await replay.StepAsync(new(updated.Name, new(graph.Tenant.Name, "PUT", identity)
        {
            Format = graph.Format, Content = requestContent ?? expectedContent, IfMatchBinding = previous.Name,
        }, ResourceResponse(updated.Name, expectedContent, updated, HttpStatusCode.OK)));
        graph.Resources[identity] = expectedContent;
        graph.Generated[identity] = updated;
        await IdentifierMutationRead(replay, graph, identity, "read-" + updated.Name);
    }

    private static Task<CompatibilityResponse> IdentifierMutationRead(
        CompatibilityReplay replay, SearchGraph graph, string identity, string checkpoint) =>
        replay.StepAsync(BoundRead(checkpoint, graph.Tenant, graph.Format, graph.Resources[identity], graph.Generated[identity]));

    private static Task IdentifierMutationMissing(CompatibilityReplay replay, SearchGraph graph, string identity, string checkpoint) =>
        AssertExchangeFailure(replay, graph.Tenant, graph.Format, checkpoint,
            new(graph.Tenant.Name, "GET", identity) { Format = graph.Format },
            HttpStatusCode.NotFound, "exception", $"Resource: {identity} not found");

    private static async Task IdentifierMutationDelete(CompatibilityReplay replay, SearchGraph graph, string identity)
    {
        await DeleteExchangeResource(replay, graph.Tenant, graph.Format, graph.Resources[identity], graph.Generated[identity]);
        graph.Resources.Remove(identity);
        graph.Generated.Remove(identity);
    }

    private static SearchProbe IdentifierMutationProbe(string shape, string checkpoint, string[] refs, string[] includes)
    {
        string type = shape == "list" ? "Observation" : "Provenance";
        string edge = shape == "list" ? "subject" : "target";
        return new(checkpoint, $"{type}?_id={string.Join(',', refs.Select(identity => identity.Split('/')[1]))}&_include={type}:{edge}", refs)
        {
            Includes = includes,
        };
    }

    private static Task IdentifierMutationInclude(
        CompatibilityReplay replay, SearchGraph graph, string shape, string checkpoint, string[] refs, params string[] includes) =>
        AssertSearchProbe(replay, graph, IdentifierMutationProbe(shape, checkpoint, refs, includes));

    private static async Task IdentifierMutationSearchControls(
        CompatibilityReplay replay, SearchGraph graph, string shape, string target, string[] refs, string key,
        string checkpoint, bool targetMatches = true)
    {
        string type = shape == "list" ? "Observation" : "Provenance";
        string edge = shape == "list" ? "subject" : "target";
        await AssertSearchProbe(replay, graph, new(checkpoint + "-ordinary-identifier",
            $"{target.Split('/')[0]}?identifier={IdentifierMutationSystem}|{key}", targetMatches ? [target] : []));
        await AssertSearchProbe(replay, graph, new(checkpoint + "-reference-identifier",
            $"{type}?{edge}:identifier={IdentifierMutationSystem}|{key}", refs));
    }

    private static async Task IdentifierMutationCollision(CompatibilityReplay replay, SearchGraph graph, string shape)
    {
        // Isolated keys avoid borrowing the primary sequence's established B owner.
        string owner = await IdentifierMutationPost(replay, graph, "collision-owner",
            IdentifierMutationTarget(graph.Format, shape, null, ["collision-B"]));
        string other = await IdentifierMutationPost(replay, graph, "collision-other",
            IdentifierMutationTarget(graph.Format, shape, null, ["collision-A"], language: "de"));
        string a = await IdentifierMutationPost(replay, graph, "collision-ref-a",
            IdentifierMutationReferrer(graph.Format, shape, "collision-A"));
        string b = await IdentifierMutationPost(replay, graph, "collision-ref-b",
            IdentifierMutationReferrer(graph.Format, shape, "collision-B"));
        await IdentifierMutationInclude(replay, graph, shape, "collision-established-a", [a], other);
        await IdentifierMutationInclude(replay, graph, shape, "collision-established-b", [b], owner);
        await IdentifierMutationUpdate(replay, graph, other,
            IdentifierMutationTarget(graph.Format, shape, graph.Generated[other].ExpectedId, ["collision-B"], language: "fr"));
        await IdentifierMutationInclude(replay, graph, shape, "collision-departed-a", [a]);
        await IdentifierMutationInclude(replay, graph, shape, "destination-owner-retained", [b], owner);
        await IdentifierMutationUpdate(replay, graph, other,
            IdentifierMutationTarget(graph.Format, shape, graph.Generated[other].ExpectedId, ["collision-B"], language: "it"));
        await IdentifierMutationInclude(replay, graph, shape, "retained-non-owner-key", [b], owner);
        await IdentifierMutationDelete(replay, graph, owner);
        await IdentifierMutationInclude(replay, graph, shape, "no-duplicate-promotion", [b]);
        await IdentifierMutationUpdate(replay, graph, other,
            IdentifierMutationTarget(graph.Format, shape, graph.Generated[other].ExpectedId, ["collision-B"], language: "es"));
        await IdentifierMutationInclude(replay, graph, shape, "explicit-write-claims-unowned", [b], other);
    }

    private static async Task IdentifierMutationEmptyComponents(CompatibilityReplay replay, SearchGraph graph, string shape)
    {
        foreach ((string? system, string? value, string name) in new (string?, string?, string)[]
        {
            (null, "empty-system", "empty-system"), (IdentifierMutationSystem + "/empty-value", null, "empty-value"),
        })
        {
            string target = await IdentifierMutationPost(replay, graph, name,
                IdentifierMutationTarget(graph.Format, shape, null, ["unused"], system: system, value: value, customKey: true));
            string refer = await IdentifierMutationPost(replay, graph, name + "-ref",
                IdentifierMutationReferrer(graph.Format, shape, "unused", system: system, value: value, customKey: true));
            await IdentifierMutationInclude(replay, graph, shape, name + "-initial", [refer], target);
            await IdentifierMutationUpdate(replay, graph, target,
                IdentifierMutationTarget(graph.Format, shape, graph.Generated[target].ExpectedId, []));
            await IdentifierMutationInclude(replay, graph, shape, name + "-removed", [refer]);
        }
    }

    private static void IdentifierMutationSynchronousInclude(
        CompatibilityReplay replay, SearchGraph graph, IFhirStore store, string checkpoint, string refer, params string[] includes)
    {
        SearchProbe probe = IdentifierMutationProbe("list", checkpoint, [refer], includes);
        foreach (bool form in new[] { false, true })
        {
            CompatibilityRequest request = SearchRequest(graph, probe.Path, form, null);
            FhirRequestContext context = new(store, request.Method, request.Path)
            {
                SourceContent = request.Content ?? string.Empty,
                SourceFormat = request.SourceMediaType ?? WireContractAssertions.MediaType(graph.Format),
                DestinationFormat = WireContractAssertions.MediaType(graph.Format),
            };
            bool success = store.TypeSearch(context, out FhirResponseContext response);
            CompatibilityResponse boundary = new(CompatibilityTransport.Direct, response.StatusCode!.Value, response.MimeType)
            {
                Direct = new(success, response.SerializedResource, response.SerializedOutcome,
                    response.ResourceType, response.Id, response.ETag, response.LastModified, response.Location),
            };
            string name = checkpoint + "-" + request.Method;
            AssertExchangeContainer(replay, graph.Tenant, graph.Format, name, boundary, "Bundle", string.Empty,
                new OutcomeBodyExpectation(name + "-outcome", [new("information", "success", "Type search successful")]));
            string body = response.SerializedResource;
            Assert.Equal("searchset", ExchangePrimitive(body, graph.Format, "type"));
            Assert.Equal(1, SearchTotal(body, graph.Format));
            string link = Assert.Single(ExchangeRepeated(body, graph.Format, "link"));
            Assert.Equal("self", ExchangePrimitive(link, graph.Format, "relation"));
            Assert.Equal(graph.Tenant.BaseUrl + "/" + probe.Path,
                Uri.UnescapeDataString(ExchangePrimitive(link, graph.Format, "url")));
            List<SearchEntry> entries = [];
            foreach (string entry in ExchangeRepeated(body, graph.Format, "entry"))
            {
                AssertExchangeMembers(entry, graph.Format, ["fullUrl", "resource", "search"]);
                string resource = ExchangeNestedResource(entry, graph.Format, "resource");
                string search = ExchangeChild(entry, graph.Format, "search");
                AssertExchangeMembers(search, graph.Format, ["mode"]);
                string identity = SearchIdentity(resource, graph.Format);
                string fullUrl = ExchangePrimitive(entry, graph.Format, "fullUrl");
                Assert.Equal(graph.Tenant.BaseUrl + "/" + identity, fullUrl);
                entries.Add(new(identity, ExchangePrimitive(search, graph.Format, "mode"), fullUrl, resource));
                WireContractAssertions.Body(new ResourceBodyExpectation(graph.Resources[identity],
                    graph.Generated[identity] with { Mode = CompatibilityBindingMode.Reuse }),
                    resource, graph.Format, graph.Tenant.BaseUrl, replay.Bindings);
            }
            try
            {
                AssertSearchIdentities(probe, entries, false, checkpoint);
            }
            catch (Xunit.Sdk.XunitException exception)
            {
                throw new InvalidOperationException($"{graph.Tenant.Version} / {graph.Format} / Direct / probe {checkpoint} / " +
                    $"expected matches [{refer}], includes [{string.Join(", ", includes)}], " +
                    $"actual [{string.Join(", ", entries.Select(entry => entry.Identity))}]: {exception.Message}", exception);
            }
            AssertExchangeMembers(body, graph.Format, ["resourceType", "type", "total", "link", "entry"]);
        }
    }

    private static string IdentifierMutationTarget(
        CompatibilityFormat format, string shape, string? id, string[] keys, string language = "en",
        bool explicitEmpty = false, string? system = IdentifierMutationSystem, string? value = null, bool customKey = false)
    {
        string type = shape == "list" ? "Patient" : "Bundle";
        if (format == CompatibilityFormat.Json)
        {
            string identity = id is null ? string.Empty : $"\"id\":\"{id}\",";
            string[] identifiers = keys.Select(key => IdentifierMutationJsonIdentifier(system, customKey ? value : key)).ToArray();
            string identifier = shape == "list"
                ? (keys.Length > 0 || explicitEmpty ? ",\"identifier\":[" + string.Join(',', identifiers) + "]" : string.Empty)
                : (keys.Length > 0 ? ",\"identifier\":" + identifiers[0] : string.Empty);
            string tail = shape == "list" ? ",\"active\":true" : ",\"type\":\"collection\"";
            return $$$"""{"resourceType":"{{{type}}}",{{{identity}}}"language":"{{{language}}}"{{{identifier}}}{{{tail}}}}""";
        }
        string xmlId = id is null ? string.Empty : $"""<id value="{id}"/>""";
        string xmlIdentifiers = string.Concat(keys.Select(key =>
            "<identifier>" + IdentifierMutationXmlIdentifier(system, customKey ? value : key) + "</identifier>"));
        string xmlTail = shape == "list" ? """<active value="true"/>""" : """<type value="collection"/>""";
        return $"""<{type} xmlns="http://hl7.org/fhir">{xmlId}<language value="{language}"/>{xmlIdentifiers}{xmlTail}</{type}>""";
    }

    private static string IdentifierMutationReferrer(
        CompatibilityFormat format, string shape, string key, string marker = "logical",
        string? literal = null, bool omitType = false, string? system = IdentifierMutationSystem,
        string? value = null, bool customKey = false)
    {
        string targetType = shape == "list" ? "Patient" : "Bundle";
        if (format == CompatibilityFormat.Json)
        {
            string reference = literal is not null ? $$$"""{"reference":"{{{literal}}}"}""" :
                "{" + (omitType ? string.Empty : $"\"type\":\"{targetType}\",") +
                "\"identifier\":" + IdentifierMutationJsonIdentifier(system, customKey ? value : key) + "}";
            return shape == "list"
                ? $$$"""{"resourceType":"Observation","status":"final","code":{"text":"M4 {{{marker}}}"},"subject":{{{reference}}}}"""
                : $$$"""{"resourceType":"Provenance","target":[{{{reference}}}],"recorded":"2025-01-02T03:04:05Z","activity":{"text":"M4 {{{marker}}}"},"agent":[{"who":{"reference":"Practitioner/m4-author","display":"Synthetic author"}}]}""";
        }
        string xmlReference = literal is not null ? $"""<reference value="{literal}"/>""" :
            (omitType ? string.Empty : $"""<type value="{targetType}"/>""") +
            "<identifier>" + IdentifierMutationXmlIdentifier(system, customKey ? value : key) + "</identifier>";
        return shape == "list"
            ? $"""<Observation xmlns="http://hl7.org/fhir"><status value="final"/><code><text value="M4 {marker}"/></code><subject>{xmlReference}</subject></Observation>"""
            : $"""<Provenance xmlns="http://hl7.org/fhir"><target>{xmlReference}</target><recorded value="2025-01-02T03:04:05Z"/><activity><text value="M4 {marker}"/></activity><agent><who><reference value="Practitioner/m4-author"/><display value="Synthetic author"/></who></agent></Provenance>""";
    }

    private static string IdentifierMutationJsonIdentifier(string? system, string? value) =>
        "{" + string.Join(',', new[]
        {
            system is null ? null : $"\"system\":\"{system}\"",
            value is null ? null : $"\"value\":\"{value}\"",
        }.Where(component => component is not null)) + "}";

    private static string IdentifierMutationXmlIdentifier(string? system, string? value) =>
        (system is null ? string.Empty : $"""<system value="{system}"/>""") +
        (value is null ? string.Empty : $"""<value value="{value}"/>""");
}
