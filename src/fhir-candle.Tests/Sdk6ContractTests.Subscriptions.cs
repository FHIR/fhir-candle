// <copyright file="Sdk6ContractTests.Subscriptions.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

extern alias candleR4;
extern alias candleR4B;
extern alias candleR5;

using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using fhir.candle.Tests.Extensions;
using fhir.candle.Tests.Models;
using FhirCandle.Models;
using FhirCandle.Storage;
using FhirCandle.Utils;
using Xunit.Abstractions;

namespace fhir.candle.Tests;

public partial class Sdk6ContractTests
{
    private readonly ITestOutputHelper _subscriptionOutput;

    /// <summary>Captures owned-child timing, TRX identity, and per-row subscription evidence.</summary>
    public Sdk6ContractTests(ITestOutputHelper output)
    {
        _subscriptionOutput = output;
    }

    private const string SubscriptionEvidence =
        "R4TestSubscriptions/R4BTestSubscriptions/R5TestSubscriptions ParseTopic/ParseSubscription/ParseHandshake/" +
        "TestSubEncounterNoFilters; FhirStoreTestsR5.SubscriptionTriggeredCreate/SubscriptionTriggeredUpdateFhirpath/" +
        "SubscriptionTriggeredUpdateQuery. Synthetic local-only copies, independent JSON/XML literals.";
    private const string SubscriptionTopicUrl = "http://localhost/SubscriptionTopic/sdk6-encounter";
    private const string SubscriptionId = "local-subscription";
    private const string SubscriptionEventsEmptyEnvelopeId = "events-empty-envelope";
    private const string SubscriptionEndpoint = "http://localhost/fhir/local-receiver/$subscription-hook";
    private const string BackportExtensionRoot = "http://hl7.org/fhir/uv/subscriptions-backport/StructureDefinition/";
    private const string TopicExtensionRoot = "http://hl7.org/fhir/5.0/StructureDefinition/extension-SubscriptionTopic.";

    /// <summary>Each positive/negative interaction and criterion family runs independently on both boundaries.</summary>
    public static IEnumerable<object[]> SubscriptionFilterCases
    {
        get
        {
            foreach (object[] row in OperationRows("subscription-filter", ["fhirpath", "query", "interaction"]))
            {
                string[] probes = (string)row[4] == "interaction"
                    ? ["create-match", "create-filter-miss", "update-match", "update-filter-miss", "delete-match", "delete-filter-miss"]
                    : ["create-match", "create-filter-miss", "create-criteria-miss",
                       "update-match", "update-filter-miss", "update-criteria-miss", "update-already-terminal",
                       "delete-match", "delete-filter-miss", "delete-criteria-miss"];
                foreach (string probe in probes)
                {
                    yield return [$"{row[0]} / {probe}", row[1], row[2], row[3], row[4], probe];
                }
            }
        }
    }

    /// <summary>Handshake and heartbeat use fresh subscriptions; event rows bind two actual generated events.</summary>
    public static IEnumerable<object[]> SubscriptionNotificationCases
    {
        get
        {
            foreach (object[] row in OperationRows("subscription-notification", ["empty", "id-only", "full-resource"]))
            {
                foreach (string notificationType in new[] { "handshake", "heartbeat", "event-notification" })
                {
                    yield return [$"{row[0]} / {notificationType}", row[1], row[2], row[3], row[4], notificationType];
                }
            }
        }
    }

    /// <summary>Pure independent controls ensure status assertions reject changed identity and event relationships.</summary>
    public static IEnumerable<object[]> SubscriptionOracleCases
    {
        get
        {
            foreach (FhirReleases.FhirSequenceCodes version in _versions)
            {
                foreach (CompatibilityFormat format in Enum.GetValues<CompatibilityFormat>())
                {
                    foreach (string defect in new[] { "none", "subscription-tenant", "focus-tenant", "additional-context", "event-number", "event-total" })
                    {
                        yield return [$"notification-oracle / {version} / {format} / {defect}", version, format, defect];
                    }
                }
            }
        }
    }

    /// <summary>Ordinary replay covers each content level and both complete request forms.</summary>
    public static IEnumerable<object[]> SubscriptionEventsReplayCases =>
        SubscriptionEventsMethodRows("events-replay", ["empty", "id-only", "full-resource"]);

    /// <summary>Every literal empty selection must retain only its native status entry.</summary>
    public static IEnumerable<object[]> SubscriptionEventsEmptyCases =>
        SubscriptionEventsMethodRows("events-empty", ["empty", "cleared", "no-match", "gap", "reversed"]);

    /// <summary>Missing or malformed bounds use GET; the body-precedence row always supplies all parameters.</summary>
    public static IEnumerable<object[]> SubscriptionEventsDefaultCases =>
        OperationRows("events-defaults",
            ["omitted-both", "omitted-since", "omitted-until", "malformed-both", "malformed-since", "malformed-until",
             "alias-camel", "alias-lowercase", "alias-dashed", "default-content", "body-precedence", "unknown"]);

    /// <summary>Optional POST members resolve independently across every version, format, and transport.</summary>
    public static IEnumerable<object[]> SubscriptionEventsOptionalBodyCases =>
        OperationRows("events-optional-body",
            ["no-query-none", "no-query-since", "no-query-until", "no-query-content", "no-query-bounds",
             "no-query-since-content", "no-query-until-content", "no-query-complete",
             "query-none", "query-since", "query-until", "query-content", "query-bounds",
             "query-since-content", "query-until-content", "query-complete",
             "unrelated-defaults", "unrelated-query", "mixed-default-since", "mixed-default-until", "mixed-default-content",
             "valueless-since", "valueless-until", "valueless-bounds", "valueless-content", "literal-empty-content",
             "fresh-history", "unmatched-partial-range", "partial-negative-since", "partial-negative-until"]);

    /// <summary>Only R4/R4B string bounds carry nonnumeric literals through the wire parser.</summary>
    public static IEnumerable<object[]> SubscriptionEventsUnparseableBodyCases =>
        OperationRows("events-unparseable-body", ["unparseable-since", "unparseable-until", "unparseable-bounds"])
            .Where(row => (FhirReleases.FhirSequenceCodes)row[1] is
                FhirReleases.FhirSequenceCodes.R4 or FhirReleases.FhirSequenceCodes.R4B);

    /// <summary>Element-empty inputs and id-only envelopes exercise the same request boundary.</summary>
    public static IEnumerable<object[]> SubscriptionEventsEmptyParametersEnvelopeCases =>
        OperationRows("events-empty-envelope", ["empty-envelope-boundary"]);

    /// <summary>Ordinary negative bounds, including reversed combinations, run without extreme endpoints.</summary>
    public static IEnumerable<object[]> SubscriptionEventsNegativeCases =>
        SubscriptionEventsMethodRows("events-negative",
            ["negative-since", "negative-until", "both-negative", "negative-reversed", "negative-upper-reversed"]);

    /// <summary>Both legacy fallback modes are checked independently at each conversion entry point.</summary>
    public static IEnumerable<object[]> SubscriptionEventsLegacyCases =>
        OperationRows("events-legacy", ["query-event", "event-notification"]);

    private static IEnumerable<object[]> SubscriptionEventsExtremeCases =>
        SubscriptionEventsMethodRows("events-extreme",
            ["empty-wide", "two-wide", "huge-no-match", "absent-max", "minimum-since", "minimum-until",
             "requested-upper", "retained-above-high-water", "retained-max-singleton", "retained-max-pair"]);

    private static IEnumerable<object[]> SubscriptionEventsConcurrentCases =>
        OperationRows("events-concurrent",
            ["register-default", "register-max-get", "register-max-post", "clear",
             "serialize-register", "bundle-register", "reversed", "no-match"]);

    /// <summary>Potentially unbounded interval regressions run only in this fact's owned deadline-bounded child.</summary>
    [Fact]
    public async Task SubscriptionEventsExtremeBoundsAreIsolated()
    {
        if (IsSubscriptionEventsChild(nameof(SubscriptionEventsExtremeBoundsAreIsolated)))
        {
            int executed = 0;
            foreach (object[] row in SubscriptionEventsExtremeCases)
            {
                await RunSubscriptionEventsExtremeCase(row);
                executed++;
            }
            Assert.Equal(240, executed);
            _subscriptionOutput.WriteLine($"H2 extreme inventory executed={executed}; all 240 expected rows passed.");
            return;
        }

        await RunSubscriptionEventsChild(nameof(SubscriptionEventsExtremeBoundsAreIsolated));
    }

    /// <summary>Controlled history overlap and independent wrapper ownership run in a separate owned child.</summary>
    [Fact]
    public async Task SubscriptionEventsConcurrentSelectionIsIsolated()
    {
        if (IsSubscriptionEventsChild(nameof(SubscriptionEventsConcurrentSelectionIsIsolated)))
        {
            int executed = 0;
            foreach (object[] row in SubscriptionEventsConcurrentCases)
            {
                if ((string)row[4] is "reversed" or "no-match")
                {
                    await RunSubscriptionEventsEmptyOverlap(row);
                }
                else
                {
                    await RunSubscriptionEventsConversionOverlap(row);
                }
                executed++;
            }
            Assert.Equal(96, executed);
            _subscriptionOutput.WriteLine($"H2 concurrency inventory executed={executed}; 72 gated conversions and 24 four-pair rendezvous rows passed.");
            return;
        }

        await RunSubscriptionEventsChild(nameof(SubscriptionEventsConcurrentSelectionIsIsolated));
    }

    /// <summary>Retained replay is inclusive and ascending regardless of history insertion order.</summary>
    [Theory]
    [MemberData(nameof(SubscriptionEventsReplayCases))]
    public async Task SubscriptionEventsReplayPreservesInclusiveBoundsAndContent(
        string label,
        FhirReleases.FhirSequenceCodes version,
        CompatibilityTransport transport,
        CompatibilityFormat format,
        string contentLevel,
        string method)
    {
        CompatibilityTenant tenant = Tenant(version, $"events-{VersionName(version)}");
        CompatibilityScenario scenario = new(label, SubscriptionEvidence, [tenant], [MetadataStep(tenant, format)]);
        await using CompatibilityReplay replay = new(scenario, transport);
        await replay.RunAsync();
        using SubscriptionCapture capture = new(replay.GetOwnedStore(tenant.Name));
        SubscriptionGraph graph = await SeedSubscriptionGraph(replay, tenant, format, "fhirpath");
        ParsedSubscription subscription = AssertParsedSubscription(capture.Store, "fhirpath", version);
        NotificationEventContract[] events = await GenerateSubscriptionReplayEvents(replay, graph, capture, 2);
        ReverseSubscriptionHistory(subscription);

        await AssertSubscriptionEventsReplay(replay, graph, "inclusive", method, 1, 2, contentLevel, events, 2);
        await AssertSubscriptionEventsReplay(replay, graph, "singleton", method, 2, 2, contentLevel, [events[1]], 2);
        await AssertSubscriptionEventsReplay(replay, graph, "requested-upper", method, 0, 1, contentLevel, [events[0]], 2);

        // A retained key above the reserved counter must not escape the captured high-water bound.
        subscription.RegisterEvent(subscription.GeneratedEvents[2] with { EventNumber = 3 });
        Assert.Equal(2, subscription.CurrentEventCount);
        Assert.Equal(3, subscription.GeneratedEvents.Count);
        await AssertSubscriptionEventsReplay(replay, graph, "captured-upper", method, 1, 4, contentLevel, events, 2);
        Assert.Equal(2, capture.Sent.Count);
        Assert.Empty(subscription.NotificationErrors);
    }

    /// <summary>Empty, cleared, sparse, and reversed history selections cannot invoke legacy fallback.</summary>
    [Theory]
    [MemberData(nameof(SubscriptionEventsEmptyCases))]
    public async Task SubscriptionEventsEmptyRangesStayStatusOnly(
        string label,
        FhirReleases.FhirSequenceCodes version,
        CompatibilityTransport transport,
        CompatibilityFormat format,
        string kind,
        string method)
    {
        CompatibilityTenant tenant = Tenant(version, $"events-empty-{VersionName(version)}");
        CompatibilityScenario scenario = new(label, SubscriptionEvidence, [tenant], [MetadataStep(tenant, format)]);
        await using CompatibilityReplay replay = new(scenario, transport);
        await replay.RunAsync();
        using SubscriptionCapture capture = new(replay.GetOwnedStore(tenant.Name));
        SubscriptionGraph graph = await SeedSubscriptionGraph(replay, tenant, format, "fhirpath");
        ParsedSubscription subscription = AssertParsedSubscription(capture.Store, "fhirpath", version);
        int total = kind == "empty" ? 0 : kind == "gap" ? 3 : 2;
        await GenerateSubscriptionReplayEvents(replay, graph, capture, total);
        long since = 0;
        long until = 4;
        switch (kind)
        {
            case "empty":
                Assert.Empty(subscription.GeneratedEvents);
                break;
            case "cleared":
                subscription.ClearEvents();
                Assert.Empty(subscription.GeneratedEvents);
                break;
            case "no-match":
                since = 3;
                break;
            case "gap":
                lock (subscription.GeneratedEvents)
                {
                    Assert.True(subscription.GeneratedEvents.Remove(2));
                }
                Assert.Equal(new long[] { 1, 3 }, subscription.GeneratedEvents.Keys);
                since = 2;
                until = 2;
                break;
            case "reversed":
                since = 2;
                until = 1;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind));
        }
        Assert.Equal(total, subscription.CurrentEventCount);
        await AssertSubscriptionEventsReplay(replay, graph, kind, method, since, until, "full-resource", [], total);
        Assert.Equal(total, capture.Sent.Count);
        Assert.Empty(subscription.NotificationErrors);
    }

    /// <summary>GET fallback, all six bound aliases, content defaults, and complete POST precedence stay compatible.</summary>
    [Theory]
    [MemberData(nameof(SubscriptionEventsDefaultCases))]
    public async Task SubscriptionEventsDefaultsAliasesAndBodyPrecedenceRemainStable(
        string label,
        FhirReleases.FhirSequenceCodes version,
        CompatibilityTransport transport,
        CompatibilityFormat format,
        string kind)
    {
        CompatibilityTenant tenant = Tenant(version, $"events-default-{VersionName(version)}");
        CompatibilityScenario scenario = new(label, SubscriptionEvidence, [tenant], [MetadataStep(tenant, format)]);
        await using CompatibilityReplay replay = new(scenario, transport);
        await replay.RunAsync();
        using SubscriptionCapture capture = new(replay.GetOwnedStore(tenant.Name));
        SubscriptionGraph graph = await SeedSubscriptionGraph(replay, tenant, format, "fhirpath");
        ParsedSubscription subscription = AssertParsedSubscription(capture.Store, "fhirpath", version);
        NotificationEventContract[] events = await GenerateSubscriptionReplayEvents(replay, graph, capture, 2);
        string query = kind switch
        {
            "omitted-both" => "content=id-only",
            "omitted-since" => "eventsUntilNumber=1&content=id-only",
            "omitted-until" => "eventsSinceNumber=2&content=id-only",
            "malformed-both" => "eventsSinceNumber=not-a-number&eventsUntilNumber=not-a-number&content=id-only",
            "malformed-since" => "eventsSinceNumber=not-a-number&eventsUntilNumber=1&content=id-only",
            "malformed-until" => "eventsSinceNumber=2&eventsUntilNumber=not-a-number&content=id-only",
            "alias-camel" => "eventsSinceNumber=2&eventsUntilNumber=2&content=id-only",
            "alias-lowercase" => "eventssincenumber=2&eventsuntilnumber=2&content=id-only",
            "alias-dashed" => "events-since-number=2&events-until-number=2&content=id-only",
            "default-content" => "eventsSinceNumber=2&eventsUntilNumber=2",
            "body-precedence" => "eventsSinceNumber=1&eventsUntilNumber=1&content=empty",
            "unknown" => "eventsSinceNumber=1&eventsUntilNumber=2&content=full-resource",
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        string id = kind == "unknown" ? "unknown-subscription" : SubscriptionId;
        CompatibilityResponse response = await replay.SendAsync(new(tenant.Name,
            kind == "body-precedence" ? "POST" : "GET", $"Subscription/{id}/$events?{query}")
        {
            Format = format,
            Content = kind == "body-precedence" ? SubscriptionEventsParameters(graph, 2, 2, "id-only") : null,
        });
        if (kind == "unknown")
        {
            AssertExchangeFailureResponse(replay.Bindings, tenant, format, kind, response, HttpStatusCode.NotFound,
                "not-found", "Instance Subscription/unknown-subscription does not exist on this server.", checkMediaType: true);
        }
        else
        {
            NotificationEventContract[] expected = kind switch
            {
                "omitted-both" or "malformed-both" => events,
                "omitted-since" or "malformed-since" => [events[0]],
                _ => [events[1]],
            };
            AssertSubscriptionEventsResponse(replay, graph, kind, response,
                kind == "default-content" ? "full-resource" : "id-only", expected, 2);
        }
        Assert.Equal(2, subscription.CurrentEventCount);
        Assert.Equal(2, capture.Sent.Count);
        Assert.Empty(subscription.NotificationErrors);
    }

    /// <summary>Body presence overrides only its own query value, including name-only fallback inputs.</summary>
    [Theory]
    [MemberData(nameof(SubscriptionEventsOptionalBodyCases))]
    public async Task SubscriptionEventsOptionalBodyMembersResolveIndependently(
        string label,
        FhirReleases.FhirSequenceCodes version,
        CompatibilityTransport transport,
        CompatibilityFormat format,
        string kind)
    {
        const string ConflictingQuery = "eventsSinceNumber=2&eventsUntilNumber=2&content=empty";
        const string IdOnlyQuery = "eventsSinceNumber=2&eventsUntilNumber=2&content=id-only";
        const string NegativeQuery = "eventsSinceNumber=1&eventsUntilNumber=3&content=full-resource";
        CompatibilityTenant tenant = Tenant(version, $"events-optional-{VersionName(version)}");
        CompatibilityScenario scenario = new(label, SubscriptionEvidence, [tenant], [MetadataStep(tenant, format)]);
        await using CompatibilityReplay replay = new(scenario, transport);
        await replay.RunAsync();
        using SubscriptionCapture capture = new(replay.GetOwnedStore(tenant.Name));
        SubscriptionGraph graph = await SeedSubscriptionGraph(replay, tenant, format, "fhirpath");
        ParsedSubscription subscription = AssertParsedSubscription(capture.Store, "fhirpath", version);
        Assert.Equal("full-resource", subscription.ContentLevel);
        int total = kind == "fresh-history" ? 0 : 3;
        NotificationEventContract[] events = await GenerateSubscriptionReplayEvents(replay, graph, capture, total);
        (string Query, (string Name, string? Value)[] Members, long[] ExpectedNumbers, string ContentLevel) testCase = kind switch
        {
            "no-query-none" => (string.Empty, [], [1, 2, 3], "full-resource"),
            "no-query-since" => (string.Empty, [("eventsSinceNumber", "2")], [2, 3], "full-resource"),
            "no-query-until" => (string.Empty, [("eventsUntilNumber", "2")], [1, 2], "full-resource"),
            "no-query-content" => (string.Empty, [("content", "id-only")], [1, 2, 3], "id-only"),
            "no-query-bounds" => (string.Empty, [("eventsSinceNumber", "2"), ("eventsUntilNumber", "2")], [2], "full-resource"),
            "no-query-since-content" => (string.Empty, [("eventsSinceNumber", "2"), ("content", "id-only")], [2, 3], "id-only"),
            "no-query-until-content" => (string.Empty, [("eventsUntilNumber", "2"), ("content", "id-only")], [1, 2], "id-only"),
            "no-query-complete" => (string.Empty,
                [("eventsSinceNumber", "2"), ("eventsUntilNumber", "2"), ("content", "id-only")], [2], "id-only"),
            "query-none" => (ConflictingQuery, [], [2], "empty"),
            "query-since" => (ConflictingQuery, [("eventsSinceNumber", "1")], [1, 2], "empty"),
            "query-until" => (ConflictingQuery, [("eventsUntilNumber", "3")], [2, 3], "empty"),
            "query-content" => (ConflictingQuery, [("content", "id-only")], [2], "id-only"),
            "query-bounds" => (ConflictingQuery, [("eventsSinceNumber", "1"), ("eventsUntilNumber", "3")], [1, 2, 3], "empty"),
            "query-since-content" => (ConflictingQuery, [("eventsSinceNumber", "1"), ("content", "id-only")], [1, 2], "id-only"),
            "query-until-content" => (ConflictingQuery, [("eventsUntilNumber", "3"), ("content", "id-only")], [2, 3], "id-only"),
            "query-complete" => (ConflictingQuery,
                [("eventsSinceNumber", "1"), ("eventsUntilNumber", "3"), ("content", "id-only")], [1, 2, 3], "id-only"),
            "unrelated-defaults" => (string.Empty, [("unrelated", "ignored")], [1, 2, 3], "full-resource"),
            "unrelated-query" => (ConflictingQuery, [("unrelated", "ignored")], [2], "empty"),
            "mixed-default-since" => ("eventsuntilnumber=2", [("content", "id-only")], [1, 2], "id-only"),
            "mixed-default-until" => ("events-since-number=2", [("content", "id-only")], [2, 3], "id-only"),
            "mixed-default-content" => ("eventsSinceNumber=2", [("eventsUntilNumber", "2")], [2], "full-resource"),
            "valueless-since" => (IdOnlyQuery, [("eventsSinceNumber", null)], [1, 2], "id-only"),
            "valueless-until" => (IdOnlyQuery, [("eventsUntilNumber", null)], [2, 3], "id-only"),
            "valueless-bounds" => (IdOnlyQuery, [("eventsSinceNumber", null), ("eventsUntilNumber", null)], [1, 2, 3], "id-only"),
            "valueless-content" => (ConflictingQuery, [("content", null)], [2], "full-resource"),
            "literal-empty-content" => ("eventsSinceNumber=2&eventsUntilNumber=2&content=full-resource",
                [("content", "empty")], [2], "empty"),
            "fresh-history" => (string.Empty, [("content", "id-only")], [], "id-only"),
            "unmatched-partial-range" => (string.Empty, [("eventsSinceNumber", "4")], [], "full-resource"),
            "partial-negative-since" => (NegativeQuery, [("eventsSinceNumber", "-1")], [], "full-resource"),
            "partial-negative-until" => (NegativeQuery, [("eventsUntilNumber", "-1")], [], "full-resource"),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        string path = $"Subscription/{SubscriptionId}/$events";
        if (!string.IsNullOrEmpty(testCase.Query))
        {
            path += "?" + testCase.Query;
        }
        string content = SubscriptionEventsPartialParameters(graph, testCase.Members);
        if (testCase.Members.Length == 0)
        {
            AssertSubscriptionEventsEmptyEnvelope(format, content, DeserializeSubscriptionEventsEnvelope(label, version, format, content));
        }
        CompatibilityResponse response = await replay.SendAsync(new(tenant.Name, "POST", path)
        {
            Format = format,
            Content = content,
        });
        string context = CaptureSubscriptionEventsExchange(label, path, content, response);
        if (kind is "partial-negative-since" or "partial-negative-until")
        {
            AssertOperationOutcome(replay, tenant, format, kind, response, HttpStatusCode.BadRequest,
                success: false, resourceIsOutcome: true, [new("error", "exception", "Event number bounds must be nonnegative.")]);
        }
        else
        {
            AssertSubscriptionEventsPostSucceeded(response, context);
            NotificationEventContract[] expected = testCase.ExpectedNumbers.Select(number => events[checked((int)number - 1)]).ToArray();
            AssertSubscriptionEventsResponse(replay, graph, kind, response, testCase.ContentLevel, expected, total);
        }
        Assert.Equal(total, subscription.CurrentEventCount);
        Assert.Equal(total, subscription.GeneratedEvents.Count);
        Assert.Equal(events.Select(subscriptionEvent => subscriptionEvent.Number), subscription.GeneratedEvents.Keys.OrderBy(number => number));
        Assert.Equal(total, capture.Sent.Count);
        Assert.Empty(subscription.NotificationErrors);
    }

    /// <summary>Present nonnumeric string bounds discard query values before normal lower/upper fallback.</summary>
    [Theory]
    [MemberData(nameof(SubscriptionEventsUnparseableBodyCases))]
    public async Task SubscriptionEventsUnparseableBodyBoundsKeepFallbackSemantics(
        string label,
        FhirReleases.FhirSequenceCodes version,
        CompatibilityTransport transport,
        CompatibilityFormat format,
        string kind)
    {
        Assert.True(version is FhirReleases.FhirSequenceCodes.R4 or FhirReleases.FhirSequenceCodes.R4B);
        CompatibilityTenant tenant = Tenant(version, $"events-unparseable-{VersionName(version)}");
        CompatibilityScenario scenario = new(label, SubscriptionEvidence, [tenant], [MetadataStep(tenant, format)]);
        await using CompatibilityReplay replay = new(scenario, transport);
        await replay.RunAsync();
        using SubscriptionCapture capture = new(replay.GetOwnedStore(tenant.Name));
        SubscriptionGraph graph = await SeedSubscriptionGraph(replay, tenant, format, "fhirpath");
        ParsedSubscription subscription = AssertParsedSubscription(capture.Store, "fhirpath", version);
        Assert.Equal("full-resource", subscription.ContentLevel);
        NotificationEventContract[] events = await GenerateSubscriptionReplayEvents(replay, graph, capture, 3);
        ((string Name, string? Value)[] Members, long[] ExpectedNumbers) testCase = kind switch
        {
            "unparseable-since" => ([("eventsSinceNumber", "not-a-number")], [1, 2]),
            "unparseable-until" => ([("eventsUntilNumber", "not-a-number")], [2, 3]),
            "unparseable-bounds" => ([("eventsSinceNumber", "not-a-number"), ("eventsUntilNumber", "not-a-number")], [1, 2, 3]),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        string path = $"Subscription/{SubscriptionId}/$events?eventsSinceNumber=2&eventsUntilNumber=2&content=id-only";
        string content = SubscriptionEventsPartialParameters(graph, testCase.Members);
        CompatibilityResponse response = await replay.SendAsync(new(tenant.Name, "POST", path)
        {
            Format = format,
            Content = content,
        });
        string context = CaptureSubscriptionEventsExchange(label, path, content, response);
        AssertSubscriptionEventsPostSucceeded(response, context);
        NotificationEventContract[] expected = testCase.ExpectedNumbers.Select(number => events[checked((int)number - 1)]).ToArray();
        AssertSubscriptionEventsResponse(replay, graph, kind, response, "id-only", expected, 3);
        Assert.Equal(3, subscription.CurrentEventCount);
        Assert.Equal(3, subscription.GeneratedEvents.Count);
        Assert.Equal(new long[] { 1, 2, 3 }, subscription.GeneratedEvents.Keys.OrderBy(number => number));
        Assert.Equal(3, capture.Sent.Count);
        Assert.Empty(subscription.NotificationErrors);
    }

    /// <summary>Zero operation parameters are valid in an id-only envelope, not in the raw element-empty forms.</summary>
    [Theory]
    [MemberData(nameof(SubscriptionEventsEmptyParametersEnvelopeCases))]
    public async Task SubscriptionEventsEmptyParametersEnvelopePreservesBoundaryPolicy(
        string label,
        FhirReleases.FhirSequenceCodes version,
        CompatibilityTransport transport,
        CompatibilityFormat format,
        string kind)
    {
        CompatibilityTenant tenant = Tenant(version, $"events-envelope-{VersionName(version)}");
        CompatibilityScenario scenario = new(label, SubscriptionEvidence, [tenant], [MetadataStep(tenant, format)]);
        await using CompatibilityReplay replay = new(scenario, transport);
        await replay.RunAsync();
        using SubscriptionCapture capture = new(replay.GetOwnedStore(tenant.Name));
        SubscriptionGraph graph = await SeedSubscriptionGraph(replay, tenant, format, "fhirpath");
        ParsedSubscription subscription = AssertParsedSubscription(capture.Store, "fhirpath", version);
        Assert.Equal("full-resource", subscription.ContentLevel);
        NotificationEventContract[] events = await GenerateSubscriptionReplayEvents(replay, graph, capture, 3);

        string raw = format == CompatibilityFormat.Json
            ? """{"resourceType":"Parameters"}"""
            : """<Parameters xmlns="http://hl7.org/fhir" />""";
        string candidate = WithWireId(raw, format, SubscriptionEventsEmptyEnvelopeId);
        (HttpStatusCode Status, Hl7.Fhir.Model.Resource? Resource, string Error) rawEnvelope =
            DeserializeSubscriptionEventsEnvelope($"{label} / raw", version, format, raw);
        (HttpStatusCode Status, Hl7.Fhir.Model.Resource? Resource, string Error) candidateEnvelope =
            DeserializeSubscriptionEventsEnvelope($"{label} / candidate", version, format, candidate);

        string path = $"Subscription/{SubscriptionId}/$events";
        string queryPath = path + "?eventsSinceNumber=2&eventsUntilNumber=2&content=empty";
        (CompatibilityResponse Response, string Context) rawDefault = await SendBoundaryPost("raw-default", path, raw);
        (CompatibilityResponse Response, string Context) rawQuery = await SendBoundaryPost("raw-query", queryPath, raw);
        (CompatibilityResponse Response, string Context) candidateDefault = await SendBoundaryPost("candidate-default", path, candidate);
        (CompatibilityResponse Response, string Context) candidateQuery = await SendBoundaryPost("candidate-query", queryPath, candidate);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, rawEnvelope.Status);
        Assert.Null(rawEnvelope.Resource);
        Assert.NotEmpty(rawEnvelope.Error);
        AssertSubscriptionEventsEmptyEnvelope(format, candidate, candidateEnvelope);

        string diagnostics = "Operation $events does not consume non-FHIR content.";
        if (!string.IsNullOrEmpty(rawEnvelope.Error))
        {
            diagnostics += "\n\nError:\n" + rawEnvelope.Error;
        }
        Assert.True(rawDefault.Response.Status == HttpStatusCode.UnsupportedMediaType, rawDefault.Context);
        AssertExchangeFailureResponse(replay.Bindings, tenant, format, kind + "-raw-default", rawDefault.Response,
            HttpStatusCode.UnsupportedMediaType, "invalid", diagnostics, checkMediaType: true);
        Assert.True(rawQuery.Response.Status == HttpStatusCode.UnsupportedMediaType, rawQuery.Context);
        AssertExchangeFailureResponse(replay.Bindings, tenant, format, kind + "-raw-query", rawQuery.Response,
            HttpStatusCode.UnsupportedMediaType, "invalid", diagnostics, checkMediaType: true);
        AssertSubscriptionEventsPostSucceeded(candidateDefault.Response, candidateDefault.Context);
        AssertSubscriptionEventsResponse(replay, graph, kind + "-candidate-default", candidateDefault.Response, "full-resource", events, 3);
        AssertSubscriptionEventsPostSucceeded(candidateQuery.Response, candidateQuery.Context);
        AssertSubscriptionEventsResponse(replay, graph, kind + "-candidate-query", candidateQuery.Response, "empty", [events[1]], 3);

        async Task<(CompatibilityResponse Response, string Context)> SendBoundaryPost(string name, string requestPath, string content)
        {
            CompatibilityResponse response = await replay.SendAsync(new(tenant.Name, "POST", requestPath)
            {
                Format = format,
                Content = content,
            });
            string context = CaptureSubscriptionEventsExchange($"{label} / {name}", requestPath, content, response);
            Assert.Equal(3, subscription.CurrentEventCount);
            Assert.Equal(3, subscription.GeneratedEvents.Count);
            Assert.Equal(new long[] { 1, 2, 3 }, subscription.GeneratedEvents.Keys.OrderBy(number => number));
            Assert.Equal(3, capture.Sent.Count);
            Assert.Empty(subscription.NotificationErrors);
            return (response, context);
        }
    }

    /// <summary>Parsed negatives take precedence over empty or reversed interval handling.</summary>
    [Theory]
    [MemberData(nameof(SubscriptionEventsNegativeCases))]
    public async Task SubscriptionEventsNegativeBoundsReturnBadRequest(
        string label,
        FhirReleases.FhirSequenceCodes version,
        CompatibilityTransport transport,
        CompatibilityFormat format,
        string kind,
        string method)
    {
        CompatibilityTenant tenant = Tenant(version, $"events-negative-{VersionName(version)}");
        CompatibilityScenario scenario = new(label, SubscriptionEvidence, [tenant], [MetadataStep(tenant, format)]);
        await using CompatibilityReplay replay = new(scenario, transport);
        await replay.RunAsync();
        using SubscriptionCapture capture = new(replay.GetOwnedStore(tenant.Name));
        SubscriptionGraph graph = await SeedSubscriptionGraph(replay, tenant, format, "fhirpath");
        ParsedSubscription subscription = AssertParsedSubscription(capture.Store, "fhirpath", version);
        await GenerateSubscriptionReplayEvents(replay, graph, capture, 2);
        (long since, long until) = kind switch
        {
            "negative-since" => (-1, 2),
            "negative-until" => (0, -1),
            "both-negative" => (-2, -1),
            "negative-reversed" => (-1, -2),
            "negative-upper-reversed" => (2, -1),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        CompatibilityResponse response = await replay.SendAsync(SubscriptionEventsRequest(graph, method, since, until, "full-resource"));
        AssertOperationOutcome(replay, tenant, format, kind, response, HttpStatusCode.BadRequest,
            success: false, resourceIsOutcome: true, [new("error", "exception", "Event number bounds must be nonnegative.")]);
        Assert.Equal(2, subscription.CurrentEventCount);
        Assert.Equal(2, subscription.GeneratedEvents.Count);
        Assert.Equal(2, capture.Sent.Count);
        Assert.Empty(subscription.NotificationErrors);
    }

    /// <summary>Concrete bundle dispatch and legacy serialization establish fallback behavior independently of $events.</summary>
    [Theory]
    [MemberData(nameof(SubscriptionEventsLegacyCases))]
    public async Task SubscriptionEventsLegacyEmptySelectionFallbackRemainsAvailable(
        string label,
        FhirReleases.FhirSequenceCodes version,
        CompatibilityTransport transport,
        CompatibilityFormat format,
        string notificationType)
    {
        CompatibilityTenant tenant = Tenant(version, $"events-legacy-{VersionName(version)}");
        CompatibilityScenario scenario = new(label, SubscriptionEvidence, [tenant], [MetadataStep(tenant, format)]);
        await using CompatibilityReplay replay = new(scenario, transport);
        await replay.RunAsync();
        using SubscriptionCapture capture = new(replay.GetOwnedStore(tenant.Name));
        SubscriptionGraph graph = await SeedSubscriptionGraph(replay, tenant, format, "fhirpath");
        ParsedSubscription subscription = AssertParsedSubscription(capture.Store, "fhirpath", version);
        NotificationEventContract[] events = await GenerateSubscriptionReplayEvents(replay, graph, capture, 2);
        ReverseSubscriptionHistory(subscription);
        // Legacy non-query fallback means the last retained key, not the greatest number.
        NotificationEventContract[] expected = notificationType == "query-event" ? [events[1], events[0]] : [events[0]];
        string serialized = capture.Store.SerializeSubscriptionEvents(
            SubscriptionId, Array.Empty<long>(), notificationType, false,
            WireContractAssertions.MediaType(format), "full-resource");
        AssertSubscriptionBundle(replay, graph, "legacy-serialization", serialized, notificationType, "full-resource", expected, 2);

        // Neither the operation's lock nor the serialization entry point may stand in for this wrapper.
        Hl7.Fhir.Model.Bundle legacy = Assert.IsType<Hl7.Fhir.Model.Bundle>(
            ConcreteSubscriptionEventsBundle(capture.Store, notificationType));
        AssertSubscriptionBundle(replay, graph, "legacy-concrete", SerializeSubscriptionEventsBundle(version, format, legacy),
            notificationType, "full-resource", expected, 2);
        Hl7.Fhir.Model.Bundle empty = Assert.IsType<Hl7.Fhir.Model.Bundle>(
            ConcreteSubscriptionEventsBundle(capture.Store, notificationType, allowEmptySelectionFallback: false));
        AssertSubscriptionBundle(replay, graph, "literal-empty", SerializeSubscriptionEventsBundle(version, format, empty),
            notificationType, "full-resource", [], 2);
        Assert.Null(ConcreteSubscriptionEventsBundle(capture.Store, notificationType, subscriptionId: "unknown-subscription"));
        Assert.Equal(2, subscription.CurrentEventCount);
        Assert.Equal(2, capture.Sent.Count);
        Assert.Empty(subscription.NotificationErrors);
    }

    /// <summary>Pin ordinary and isolated H2 inventories independently of child dispatch and runner discovery.</summary>
    [Fact]
    public void SubscriptionEventsTheoryDataIsNonEmptyAndBounded()
    {
        Assert.Equal(72, SubscriptionEventsReplayCases.Count());
        Assert.Equal(120, SubscriptionEventsEmptyCases.Count());
        Assert.Equal(144, SubscriptionEventsDefaultCases.Count());
        Assert.Equal(120, SubscriptionEventsNegativeCases.Count());
        Assert.Equal(24, SubscriptionEventsLegacyCases.Count());
        AssertSubscriptionEventsMatrix(SubscriptionEventsReplayCases, ["empty", "id-only", "full-resource"], methods: true);
        AssertSubscriptionEventsMatrix(SubscriptionEventsEmptyCases, ["empty", "cleared", "no-match", "gap", "reversed"], methods: true);
        AssertSubscriptionEventsMatrix(SubscriptionEventsDefaultCases,
            ["omitted-both", "omitted-since", "omitted-until", "malformed-both", "malformed-since", "malformed-until",
             "alias-camel", "alias-lowercase", "alias-dashed", "default-content", "body-precedence", "unknown"], methods: false);
        AssertSubscriptionEventsMatrix(SubscriptionEventsNegativeCases,
            ["negative-since", "negative-until", "both-negative", "negative-reversed", "negative-upper-reversed"], methods: true);
        AssertSubscriptionEventsMatrix(SubscriptionEventsLegacyCases, ["query-event", "event-notification"], methods: false);
        Assert.Equal(360, SubscriptionEventsOptionalBodyCases.Count());
        Assert.Equal(24, SubscriptionEventsUnparseableBodyCases.Count());
        Assert.Equal(384, SubscriptionEventsOptionalBodyCases.Count() + SubscriptionEventsUnparseableBodyCases.Count());
        Assert.Equal(12, SubscriptionEventsEmptyParametersEnvelopeCases.Count());
        Assert.Equal(396, SubscriptionEventsOptionalBodyCases.Count() + SubscriptionEventsUnparseableBodyCases.Count() +
            SubscriptionEventsEmptyParametersEnvelopeCases.Count());
        AssertSubscriptionEventsMatrix(SubscriptionEventsEmptyParametersEnvelopeCases, ["empty-envelope-boundary"], methods: false);
        AssertSubscriptionEventsMatrix(SubscriptionEventsOptionalBodyCases,
            ["no-query-none", "no-query-since", "no-query-until", "no-query-content", "no-query-bounds",
             "no-query-since-content", "no-query-until-content", "no-query-complete",
             "query-none", "query-since", "query-until", "query-content", "query-bounds",
             "query-since-content", "query-until-content", "query-complete",
             "unrelated-defaults", "unrelated-query", "mixed-default-since", "mixed-default-until", "mixed-default-content",
             "valueless-since", "valueless-until", "valueless-bounds", "valueless-content", "literal-empty-content",
             "fresh-history", "unmatched-partial-range", "partial-negative-since", "partial-negative-until"], methods: false);
        object[][] unparseableRows = SubscriptionEventsUnparseableBodyCases.ToArray();
        string[] unparseableKinds = ["unparseable-since", "unparseable-until", "unparseable-bounds"];
        Assert.Equal(unparseableRows.Length, unparseableRows.Select(row => (string)row[0]).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(new[] { FhirReleases.FhirSequenceCodes.R4, FhirReleases.FhirSequenceCodes.R4B },
            unparseableRows.Select(row => (FhirReleases.FhirSequenceCodes)row[1]).Distinct().OrderBy(version => version));
        Assert.Equal(new[] { CompatibilityTransport.Http, CompatibilityTransport.Direct },
            unparseableRows.Select(row => (CompatibilityTransport)row[2]).Distinct().OrderBy(transport => transport));
        Assert.Equal(new[] { CompatibilityFormat.Json, CompatibilityFormat.Xml },
            unparseableRows.Select(row => (CompatibilityFormat)row[3]).Distinct().OrderBy(format => format));
        Assert.Equal(unparseableKinds.OrderBy(kind => kind, StringComparer.Ordinal),
            unparseableRows.Select(row => (string)row[4]).Distinct().OrderBy(kind => kind, StringComparer.Ordinal));
        foreach (FhirReleases.FhirSequenceCodes version in new[] { FhirReleases.FhirSequenceCodes.R4, FhirReleases.FhirSequenceCodes.R4B })
        {
            foreach (CompatibilityTransport transport in new[] { CompatibilityTransport.Http, CompatibilityTransport.Direct })
            {
                foreach (CompatibilityFormat format in new[] { CompatibilityFormat.Json, CompatibilityFormat.Xml })
                {
                    foreach (string kind in unparseableKinds)
                    {
                        Assert.Single(unparseableRows, row => (FhirReleases.FhirSequenceCodes)row[1] == version &&
                            (CompatibilityTransport)row[2] == transport && (CompatibilityFormat)row[3] == format &&
                            (string)row[4] == kind);
                    }
                }
            }
        }
        Assert.Equal(240, SubscriptionEventsExtremeCases.Count());
        Assert.Equal(96, SubscriptionEventsConcurrentCases.Count());
        AssertSubscriptionEventsMatrix(SubscriptionEventsExtremeCases,
            ["empty-wide", "two-wide", "huge-no-match", "absent-max", "minimum-since", "minimum-until",
             "requested-upper", "retained-above-high-water", "retained-max-singleton", "retained-max-pair"], methods: true);
        AssertSubscriptionEventsMatrix(SubscriptionEventsConcurrentCases,
            ["register-default", "register-max-get", "register-max-post", "clear",
             "serialize-register", "bundle-register", "reversed", "no-match"], methods: false);
    }

    /// <summary>Real resource writes drive native topic conversion, filters, and current/previous FHIRPath contexts.</summary>
    [Theory]
    [MemberData(nameof(SubscriptionFilterCases))]
    public async Task SubscriptionConversionAndFiltersPreserveEvents(
        string label,
        FhirReleases.FhirSequenceCodes version,
        CompatibilityTransport transport,
        CompatibilityFormat format,
        string criteria,
        string probe)
    {
        CompatibilityTenant oak = Tenant(version, $"sub-oak-{VersionName(version)}");
        CompatibilityTenant elm = Tenant(version, $"sub-elm-{VersionName(version)}");
        CompatibilityScenario scenario = new(label, SubscriptionEvidence, [oak, elm], [MetadataStep(oak, format)]);
        await using CompatibilityReplay replay = new(scenario, transport);
        await replay.RunAsync();
        using SubscriptionCapture oakCapture = new(replay.GetOwnedStore(oak.Name));
        using SubscriptionCapture elmCapture = new(replay.GetOwnedStore(elm.Name));

        bool create = probe.StartsWith("create-", StringComparison.Ordinal);
        bool delete = probe.StartsWith("delete-", StringComparison.Ordinal);
        bool filterMiss = probe.EndsWith("filter-miss", StringComparison.Ordinal);
        bool criteriaMiss = probe.EndsWith("criteria-miss", StringComparison.Ordinal);
        bool alreadyTerminal = probe == "update-already-terminal";
        string? prior = create ? null : SubscriptionEncounter(version, format,
            complete: (delete && !criteriaMiss) || alreadyTerminal, otherPatient: filterMiss);
        SubscriptionGraph oakGraph = await SeedSubscriptionGraph(replay, oak, format, criteria, prior);
        SubscriptionGraph elmGraph = await SeedSubscriptionGraph(replay, elm, format, criteria, prior);
        ParsedSubscription subscription = AssertParsedSubscription(oakCapture.Store, criteria, version);
        ParsedSubscription sibling = AssertParsedSubscription(elmCapture.Store, criteria, version);
        Assert.Empty(oakCapture.Sent);
        Assert.Empty(elmCapture.Sent);

        string identity;
        if (delete)
        {
            identity = "Encounter/encounter-under-test";
            await DeleteSubscriptionResource(replay, oakGraph, identity);
        }
        else
        {
            string current = SubscriptionEncounter(version, format, complete: !criteriaMiss, otherPatient: filterMiss);
            if (!create)
            {
                current = ReplaceFixtureValue(current, "\"en\"", "\"en-US\"");
            }
            identity = await WriteSubscriptionResource(replay, oakGraph, current, post: create);
        }
        bool triggers = !filterMiss && !criteriaMiss && !alreadyTerminal;
        int expectedCount = triggers ? 1 : 0;
        Assert.True(oakCapture.Sent.Count == expectedCount,
            $"{label}: expected {expectedCount} generated event(s), actual {oakCapture.Sent.Count}; errors: {string.Join(" | ", subscription.NotificationErrors)}");
        Assert.Equal(expectedCount, subscription.CurrentEventCount);
        Assert.Equal(expectedCount, subscription.GeneratedEvents.Count);
        Assert.Empty(subscription.NotificationErrors);
        if (triggers)
        {
            AssertCapturedSubscriptionEvent(oakCapture, oakGraph, 1, identity, replay.Bindings.StartedAt);
        }
        Assert.Empty(elmCapture.Sent);
        Assert.Equal(0, sibling.CurrentEventCount);
        Assert.Empty(sibling.GeneratedEvents);
        Assert.Empty(sibling.NotificationErrors);
        Assert.Empty(oakCapture.Store.ReceivedNotifications);
        Assert.Empty(elmCapture.Store.ReceivedNotifications);

        // The same-version sibling must still execute its own subscription, without incrementing oak's count.
        string siblingEncounter = WithWireId(OperationWithoutId(SubscriptionEncounter(version, format, complete: true), format), format, "sibling-trigger");
        string siblingIdentity = await WriteSubscriptionResource(replay, elmGraph, siblingEncounter);
        AssertCapturedSubscriptionEvent(elmCapture, elmGraph, 1, siblingIdentity, replay.Bindings.StartedAt);
        Assert.Single(elmCapture.Sent);
        Assert.Equal(expectedCount, oakCapture.Sent.Count);
        Assert.Equal(expectedCount, subscription.CurrentEventCount);
        Assert.Empty(sibling.NotificationErrors);
        await AssertSubscriptionRead(replay, oakGraph, "Subscription/" + SubscriptionId);
        await AssertSubscriptionRead(replay, elmGraph, "Subscription/" + SubscriptionId);
    }

    /// <summary>Generation and inbound state are tested here; no notification transport service is started.</summary>
    [Theory]
    [MemberData(nameof(SubscriptionNotificationCases))]
    public async Task SubscriptionNotificationContentPreservesReferences(
        string label,
        FhirReleases.FhirSequenceCodes version,
        CompatibilityTransport transport,
        CompatibilityFormat format,
        string contentLevel,
        string notificationType)
    {
        CompatibilityTenant sender = Tenant(version, $"notify-sender-{VersionName(version)}");
        CompatibilityTenant receiver = Tenant(version, $"notify-receiver-{VersionName(version)}");
        CompatibilityScenario scenario = new(label, SubscriptionEvidence, [sender, receiver], [MetadataStep(sender, format)]);
        await using CompatibilityReplay replay = new(scenario, transport);
        await replay.RunAsync();
        using SubscriptionCapture senderCapture = new(replay.GetOwnedStore(sender.Name));
        using SubscriptionCapture receiverCapture = new(replay.GetOwnedStore(receiver.Name));
        SubscriptionGraph graph = await SeedSubscriptionGraph(replay, sender, format, "fhirpath");
        ParsedSubscription subscription = AssertParsedSubscription(senderCapture.Store, "fhirpath", version);
        List<NotificationEventContract> events = [];
        if (notificationType == "event-notification")
        {
            for (int number = 1; number <= 2; number++)
            {
                string encounter = WithWireId(OperationWithoutId(SubscriptionEncounter(version, format, complete: true), format), format, $"event-{number}");
                string identity = await WriteSubscriptionResource(replay, graph, encounter);
                SubscriptionEvent actual = AssertCapturedSubscriptionEvent(senderCapture, graph, number, identity, replay.Bindings.StartedAt);
                events.Add(new(number, identity, actual.Timestamp));
            }
        }
        Assert.Equal(events.Count, senderCapture.Sent.Count);
        Assert.Equal(events.Count, subscription.CurrentEventCount);
        Assert.Empty(subscription.NotificationErrors);
        Assert.Empty(receiverCapture.Sent);
        Assert.Empty(receiverCapture.Store.CurrentSubscriptions);
        string serialized = senderCapture.Store.SerializeSubscriptionEvents(
            SubscriptionId, events.Select(e => e.Number), notificationType, pretty: false,
            contentType: WireContractAssertions.MediaType(format), contentLevel: contentLevel);
        AssertSubscriptionBundle(replay, graph, "generated", serialized, notificationType, contentLevel, events, events.Count);
        Assert.Empty(senderCapture.Store.SerializeSubscriptionEvents(
            "unknown-subscription", [1], notificationType, false, WireContractAssertions.MediaType(format), contentLevel));
        await AssertSubscriptionStatusOperations(replay, graph, events.Count);
        if (events.Count == 2)
        {
            await AssertSubscriptionEventsOperations(replay, graph, contentLevel, events[1]);
        }
        await AssertSyntheticInboundNotification(replay, graph, receiver, senderCapture, receiverCapture);
        Assert.Equal(events.Count, senderCapture.Sent.Count);
        Assert.Empty(receiverCapture.Sent);
    }

    /// <summary>Wrong tenant URLs and event numbers cannot become accepted by binding observed values.</summary>
    [Theory]
    [MemberData(nameof(SubscriptionOracleCases))]
    public void SubscriptionNotificationOracleRejectsChangedReferences(
        string label,
        FhirReleases.FhirSequenceCodes version,
        CompatibilityFormat format,
        string defect)
    {
        Assert.NotEmpty(label);
        CompatibilityTenant tenant = Tenant(version, $"oracle-{VersionName(version)}");
        DateTimeOffset timestamp = DateTimeOffset.Parse("2025-01-02T03:04:05Z", CultureInfo.InvariantCulture);
        NotificationEventContract expected = new(11, "Encounter/inbound-focus", timestamp);
        string status = SyntheticSubscriptionStatus(tenant, format, "11111111-1111-4111-8111-111111111111");
        status = defect switch
        {
            "none" => status,
            "subscription-tenant" => status.Replace(tenant.BaseUrl + "/Subscription/", "http://localhost/fhir/foreign/Subscription/", StringComparison.Ordinal),
            "focus-tenant" => status.Replace(tenant.BaseUrl + "/Encounter/", "http://localhost/fhir/foreign/Encounter/", StringComparison.Ordinal),
            "additional-context" => status.Replace("/Patient/sub-patient", "/Patient/other-patient", StringComparison.Ordinal),
            "event-number" => ReplaceNotificationCount(status, version, format, eventNumber: true),
            "event-total" => ReplaceNotificationCount(status, version, format, eventNumber: false),
            _ => throw new ArgumentOutOfRangeException(nameof(defect)),
        };
        Exception? failure = Record.Exception(() => AssertSubscriptionStatus(
            tenant, format, status, "event-notification", "id-only", [expected], 11));
        if (defect == "none")
        {
            Assert.Null(failure);
        }
        else
        {
            Assert.NotNull(failure);
        }
    }

    /// <summary>Pin authored theory counts and fixture differences independently of runner discovery.</summary>
    [Fact]
    public void SubscriptionTheoryDataIsNonEmptyAndVersionCorrect()
    {
        Assert.Equal(312, SubscriptionFilterCases.Count());
        Assert.Equal(108, SubscriptionNotificationCases.Count());
        Assert.Equal(36, SubscriptionOracleCases.Count());
        foreach (IEnumerable<object[]> matrix in new[] { SubscriptionFilterCases, SubscriptionNotificationCases, SubscriptionOracleCases })
        {
            object[][] rows = matrix.ToArray();
            Assert.NotEmpty(rows);
            Assert.Equal(rows.Length, rows.Select(row => (string)row[0]).Distinct(StringComparer.Ordinal).Count());
        }
        foreach (FhirReleases.FhirSequenceCodes version in _versions)
        {
            foreach (CompatibilityFormat format in Enum.GetValues<CompatibilityFormat>())
            {
                string topic = SubscriptionFixture(version, format, "topic-fhirpath");
                Assert.Equal(version == FhirReleases.FhirSequenceCodes.R4 ? "Basic" : "SubscriptionTopic", ExchangeResourceType(topic, format));
                string complete = SubscriptionFixture(version, format, "complete");
                Assert.Equal(version == FhirReleases.FhirSequenceCodes.R5 ? "completed" : "finished", ExchangePrimitive(complete, format, "status"));
                string subscription = SubscriptionFixture(version, format, "subscription");
                Assert.Contains(SubscriptionEndpoint, subscription, StringComparison.Ordinal);
                Assert.DoesNotContain("subscriptions.argo.run", subscription, StringComparison.Ordinal);
            }
        }
    }

    private sealed record SubscriptionGraph(CompatibilityTenant Tenant, CompatibilityFormat Format)
    {
        internal Dictionary<string, string> Content { get; } = new(StringComparer.Ordinal);
        internal Dictionary<string, GeneratedResourceExpectation> Bindings { get; } = new(StringComparer.Ordinal);
    }

    private sealed record NotificationEventContract(long Number, string FocusIdentity, DateTimeOffset Timestamp);

    private static DirectoryInfo SubscriptionEventsProjectDirectory()
    {
        DirectoryInfo? directory = new(Path.GetDirectoryName(typeof(Sdk6ContractTests).Assembly.Location)!);
        Assert.Equal("Release", directory.Parent!.Name);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "fhir-candle.Tests.csproj")))
        {
            directory = directory.Parent;
        }
        Assert.NotNull(directory);
        Assert.True(File.Exists(Path.Combine(directory.Parent!.Parent!.FullName, "AGENTS.md")));
        return directory;
    }

    private static string SubscriptionEventsChildMarker(string fact) => "FHIR_CANDLE_H2_CHILD_" + fact;

    private static bool IsSubscriptionEventsChild(string fact)
    {
        string? marker = Environment.GetEnvironmentVariable(SubscriptionEventsChildMarker(fact));
        if (marker is null)
        {
            return false;
        }

        Assert.Equal("fhir.candle.Tests.Sdk6ContractTests." + fact, marker);
        string root = Path.Combine(SubscriptionEventsProjectDirectory().Parent!.Parent!.FullName, "scratch") + Path.DirectorySeparatorChar;
        foreach (string key in new[] { "TEMP", "TMP", "TMPDIR" })
        {
            string path = Path.GetFullPath(Environment.GetEnvironmentVariable(key)!);
            Assert.StartsWith(root, path, StringComparison.OrdinalIgnoreCase);
            Assert.True(Directory.Exists(path));
        }
        Assert.StartsWith(root, Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase);
        return true;
    }

    private async Task RunSubscriptionEventsChild(string fact)
    {
        DirectoryInfo projectDirectory = SubscriptionEventsProjectDirectory();
        string root = projectDirectory.Parent!.Parent!.FullName;
        string ownedDirectory = Path.Combine(root, "scratch", "1119-03", "evidence", "h2-child-" + Guid.NewGuid().ToString("N"));
        string results = Path.Combine(ownedDirectory, "results");
        string temporary = Path.Combine(ownedDirectory, "tmp");
        Directory.CreateDirectory(results);
        Directory.CreateDirectory(temporary);
        string tfm = "net" + Environment.Version.Major + ".0";
        string identity = "fhir.candle.Tests.Sdk6ContractTests." + fact;
        string marker = SubscriptionEventsChildMarker(fact);
        string? parentTemp = Environment.GetEnvironmentVariable("TEMP");
        string? parentTmp = Environment.GetEnvironmentVariable("TMP");
        string? parentTmpDir = Environment.GetEnvironmentVariable("TMPDIR");
        Dictionary<string, string?> parentConnections = Environment.GetEnvironmentVariables().Keys.Cast<string>()
            .Where(key => key.StartsWith("VSTEST_", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(key => key, Environment.GetEnvironmentVariable, StringComparer.OrdinalIgnoreCase);
        ProcessStartInfo start = new("dotnet")
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (string argument in new[] { "test", Path.Combine(projectDirectory.FullName, "fhir-candle.Tests.csproj"),
            "--configuration", "Release", "--framework", tfm, "--no-build", "--no-restore", "--verbosity", "normal",
            "--filter", "FullyQualifiedName=" + identity, "--logger", "trx;LogFileName=child.trx", "--results-directory", results })
        {
            start.ArgumentList.Add(argument);
        }
        string[] connections = start.Environment.Keys.Where(key => key.StartsWith("VSTEST_", StringComparison.OrdinalIgnoreCase)).ToArray();
        foreach (string key in connections)
        {
            start.Environment.Remove(key);
        }
        start.Environment[marker] = identity;
        start.Environment["TEMP"] = temporary;
        start.Environment["TMP"] = temporary;
        start.Environment["TMPDIR"] = temporary;

        using Process process = new() { StartInfo = start };
        Stopwatch elapsed = Stopwatch.StartNew();
        bool started = false;
        bool outputRecorded = false;
        bool trxRecorded = false;
        Task<string>? stdout = null;
        Task<string>? stderr = null;
        int ownedPid = 0;
        try
        {
            Assert.True(process.Start());
            started = true;
            ownedPid = process.Id;
            stdout = process.StandardOutput.ReadToEndAsync();
            stderr = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(150));
            string output = await stdout.WaitAsync(TimeSpan.FromSeconds(10));
            string errors = await stderr.WaitAsync(TimeSpan.FromSeconds(10));
            _subscriptionOutput.WriteLine($"H2 parent fact={fact}, TFM={tfm}, child PID={ownedPid}, elapsed={elapsed.Elapsed.TotalSeconds:F3}s, exit={process.ExitCode}, removed child VSTEST keys={connections.Length}\n{output}\n{errors}");
            outputRecorded = true;
            // Preserve useful row/failure output in the parent's result before deleting the owned TRX.
            string trxPath = Path.Combine(results, "child.trx");
            Assert.True(File.Exists(trxPath), "Owned child did not produce its one-test TRX.");
            XDocument trx = XDocument.Load(trxPath);
            XNamespace ns = trx.Root!.Name.Namespace;
            foreach (XElement resultOutput in trx.Descendants(ns + "UnitTestResult"))
            {
                _subscriptionOutput.WriteLine(resultOutput.ToString());
            }
            trxRecorded = true;
            Assert.True(process.ExitCode == 0, output + Environment.NewLine + errors);
            XElement counters = Assert.Single(trx.Descendants(ns + "Counters"));
            Assert.Equal("1", counters.Attribute("total")!.Value);
            Assert.Equal("1", counters.Attribute("executed")!.Value);
            Assert.Equal("1", counters.Attribute("passed")!.Value);
            Assert.Equal("0", counters.Attribute("failed")!.Value);
            XElement result = Assert.Single(trx.Descendants(ns + "UnitTestResult"));
            Assert.Equal(identity, result.Attribute("testName")!.Value);
            Assert.Equal("Passed", result.Attribute("outcome")!.Value);
            XElement definition = Assert.Single(trx.Descendants(ns + "TestMethod"));
            Assert.Equal("fhir.candle.Tests.Sdk6ContractTests", definition.Attribute("className")!.Value);
            Assert.Equal(fact, definition.Attribute("name")!.Value);
            _subscriptionOutput.WriteLine($"H2 child TRX verified: {tfm}/{fact}, PID={ownedPid}, elapsed={elapsed.Elapsed.TotalSeconds:F3}s, exactly 1 intended executed / 1 passed / 0 failed.");
        }
        finally
        {
            try
            {
                if (started)
                {
                    if (!process.HasExited)
                    {
                        _subscriptionOutput.WriteLine($"H2 owned child cleanup: PID={ownedPid}, TFM={tfm}, elapsed={elapsed.Elapsed.TotalSeconds:F3}s; killing/reaping owned tree only.");
                        Assert.Equal(ownedPid, process.Id);
                        process.Kill(entireProcessTree: true);
                    }
                    await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
                    if (stdout is not null && stderr is not null)
                    {
                        await Task.WhenAll(stdout, stderr).WaitAsync(TimeSpan.FromSeconds(10));
                        if (!outputRecorded)
                        {
                            _subscriptionOutput.WriteLine($"H2 child failure evidence: fact={fact}, PID={ownedPid}, TFM={tfm}, elapsed={elapsed.Elapsed.TotalSeconds:F3}s\n{await stdout}\n{await stderr}");
                        }
                    }
                    string trxPath = Path.Combine(results, "child.trx");
                    if (!trxRecorded && File.Exists(trxPath))
                    {
                        _subscriptionOutput.WriteLine(File.ReadAllText(trxPath));
                    }
                }
            }
            finally
            {
                Directory.Delete(ownedDirectory, recursive: true);
                Assert.Null(Environment.GetEnvironmentVariable(marker));
                Assert.Equal(parentTemp, Environment.GetEnvironmentVariable("TEMP"));
                Assert.Equal(parentTmp, Environment.GetEnvironmentVariable("TMP"));
                Assert.Equal(parentTmpDir, Environment.GetEnvironmentVariable("TMPDIR"));
                foreach (KeyValuePair<string, string?> connection in parentConnections)
                {
                    Assert.Equal(connection.Value, Environment.GetEnvironmentVariable(connection.Key));
                }
            }
        }
    }

    private async Task RunSubscriptionEventsExtremeCase(object[] row)
    {
        string label = (string)row[0];
        FhirReleases.FhirSequenceCodes version = (FhirReleases.FhirSequenceCodes)row[1];
        CompatibilityTransport transport = (CompatibilityTransport)row[2];
        CompatibilityFormat format = (CompatibilityFormat)row[3];
        string kind = (string)row[4];
        string method = (string)row[5];
        CompatibilityTenant tenant = Tenant(version, $"extreme-{VersionName(version)}");
        CompatibilityScenario scenario = new(label, SubscriptionEvidence, [tenant], [MetadataStep(tenant, format)]);
        await using CompatibilityReplay replay = new(scenario, transport);
        await replay.RunAsync();
        using SubscriptionCapture capture = new(replay.GetOwnedStore(tenant.Name));
        SubscriptionGraph graph = await SeedSubscriptionGraph(replay, tenant, format, "fhirpath");
        ParsedSubscription subscription = AssertParsedSubscription(capture.Store, "fhirpath", version);
        int generated = kind == "empty-wide" ? 0 : kind.StartsWith("retained-max-", StringComparison.Ordinal) ? 1 : 2;
        NotificationEventContract[] events = await GenerateSubscriptionReplayEvents(replay, graph, capture, generated);
        long since = 0;
        long until = long.MaxValue;
        long total = generated;
        NotificationEventContract[] expected = events;
        bool negative = false;
        switch (kind)
        {
            case "empty-wide":
            case "two-wide":
                break;
            case "huge-no-match":
                since = 4000000000000000000;
                expected = [];
                break;
            case "absent-max":
                since = long.MaxValue;
                expected = [];
                break;
            case "minimum-since":
                since = long.MinValue;
                negative = true;
                break;
            case "minimum-until":
                until = long.MinValue;
                negative = true;
                break;
            case "requested-upper":
                until = 1;
                expected = [events[0]];
                break;
            case "retained-above-high-water":
                ReverseSubscriptionHistory(subscription);
                subscription.RegisterEvent(subscription.GeneratedEvents[2] with { EventNumber = 3 });
                Assert.Equal(2, subscription.CurrentEventCount);
                break;
            case "retained-max-singleton":
            case "retained-max-pair":
                FieldInfo? counter = typeof(ParsedSubscription).GetField("_currentEventCount", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.True(counter is not null, "H2 retained maximum proof requires the existing ParsedSubscription._currentEventCount field.");
                lock (subscription.GeneratedEvents)
                {
                    SubscriptionEvent original = subscription.GeneratedEvents[1];
                    counter.SetValue(subscription, long.MaxValue);
                    subscription.RegisterEvent(original with { EventNumber = long.MaxValue });
                }
                // This fresh fixture is never incremented after seeding the maximum.
                since = kind == "retained-max-singleton" ? long.MaxValue : long.MaxValue - 1;
                total = long.MaxValue;
                expected = [events[0] with { Number = long.MaxValue }];
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(row));
        }

        CompatibilityResponse response = await replay.SendAsync(SubscriptionEventsRequest(graph, method, since, until, "full-resource"));
        if (negative)
        {
            AssertOperationOutcome(replay, tenant, format, kind, response, HttpStatusCode.BadRequest,
                success: false, resourceIsOutcome: true, [new("error", "exception", "Event number bounds must be nonnegative.")]);
        }
        else
        {
            AssertSubscriptionEventsResponse(replay, graph, kind, response, "full-resource", expected, total);
        }
        Assert.Equal(total, subscription.CurrentEventCount);
        Assert.InRange(subscription.GeneratedEvents.Count, 0, 3);
        Assert.Equal(generated, capture.Sent.Count);
        Assert.Empty(subscription.NotificationErrors);
        _subscriptionOutput.WriteLine($"H2 extreme {label}: bounds=[{since.ToString(CultureInfo.InvariantCulture)},{until.ToString(CultureInfo.InvariantCulture)}], status={(negative ? 400 : 200)}, selected=[{string.Join(",", negative ? Array.Empty<long>() : expected.Select(e => e.Number))}], total={total.ToString(CultureInfo.InvariantCulture)}, retained={subscription.GeneratedEvents.Count}.");
    }

    private sealed class SubscriptionContextGate : IEnumerable<object>, IDisposable
    {
        private readonly object[] _context;
        private readonly object _monitor;
        private int _entered;

        internal SubscriptionContextGate(object[] context, object monitor)
        {
            Assert.InRange(context.Length, 1, 3);
            _context = context;
            _monitor = monitor;
        }

        internal ManualResetEventSlim Reached { get; } = new();
        internal ManualResetEventSlim Release { get; } = new();

        public IEnumerator<object> GetEnumerator()
        {
            if (Interlocked.Exchange(ref _entered, 1) == 0)
            {
                // This executes on the real converter thread, not the coordinating test thread.
                Assert.True(Monitor.IsEntered(_monitor), "The conversion entry point must own the fixed history monitor.");
                Reached.Set();
                Assert.True(Release.Wait(TimeSpan.FromSeconds(10)), "Timed out releasing real AdditionalContext conversion.");
            }
            foreach (object resource in _context)
            {
                yield return resource;
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public void Dispose()
        {
            Release.Set();
            Reached.Dispose();
            Release.Dispose();
        }
    }

    private async Task RunSubscriptionEventsConversionOverlap(object[] row)
    {
        string label = (string)row[0];
        FhirReleases.FhirSequenceCodes version = (FhirReleases.FhirSequenceCodes)row[1];
        CompatibilityTransport transport = (CompatibilityTransport)row[2];
        CompatibilityFormat format = (CompatibilityFormat)row[3];
        string kind = (string)row[4];
        CompatibilityTenant tenant = Tenant(version, $"overlap-{VersionName(version)}");
        CompatibilityScenario scenario = new(label, SubscriptionEvidence, [tenant], [MetadataStep(tenant, format)]);
        await using CompatibilityReplay replay = new(scenario, transport);
        await replay.RunAsync();
        using SubscriptionCapture capture = new(replay.GetOwnedStore(tenant.Name));
        SubscriptionGraph graph = await SeedSubscriptionGraph(replay, tenant, format, "fhirpath");
        ParsedSubscription subscription = AssertParsedSubscription(capture.Store, "fhirpath", version);
        NotificationEventContract[] events = await GenerateSubscriptionReplayEvents(replay, graph, capture, 2);
        SubscriptionEvent original = subscription.GeneratedEvents[1];
        SubscriptionEvent producerTemplate = subscription.GeneratedEvents[2];
        object[] context = Assert.IsAssignableFrom<IEnumerable<object>>(original.AdditionalContext).ToArray();
        using SubscriptionContextGate gate = new(context, subscription.GeneratedEvents);
        subscription.RegisterEvent(original with { AdditionalContext = gate });
        using ManualResetEventSlim attempted = new();
        using CancellationTokenSource stop = new();
        bool clear = kind == "clear";
        string method = kind == "register-max-post" ? "POST" : "GET";
        CompatibilityRequest request = kind is "register-max-get" or "register-max-post"
            ? SubscriptionEventsRequest(graph, method, 0, long.MaxValue, "full-resource")
            : new(tenant.Name, "GET", $"Subscription/{SubscriptionId}/$events?content=full-resource") { Format = format };
        CompatibilityResponse? response = null;
        int ordering = 0;
        int attemptOrder = 0;
        int releaseOrder = 0;
        int completionOrder = 0;
        long reserved = 0;
        Task? mutation = null;
        Task<string> conversion = Task.Run(async () =>
        {
            // These wrapper calls are deliberately outside $events and any test-held monitor.
            if (kind == "serialize-register")
            {
                return capture.Store.SerializeSubscriptionEvents(SubscriptionId, Array.Empty<long>(), "query-event",
                    false, WireContractAssertions.MediaType(format), "full-resource");
            }
            if (kind == "bundle-register")
            {
                Hl7.Fhir.Model.Bundle bundle = Assert.IsType<Hl7.Fhir.Model.Bundle>(
                    ConcreteSubscriptionEventsBundle(capture.Store, "query-event"));
                return SerializeSubscriptionEventsBundle(version, format, bundle);
            }
            response = await replay.SendAsync(request);
            return ExchangeResource(response);
        });
        try
        {
            Assert.True(gate.Reached.Wait(TimeSpan.FromSeconds(10)), label + ": conversion did not reach real context.");
            Assert.Equal(2, subscription.CurrentEventCount);
            mutation = Task.Run(() =>
            {
                try
                {
                    stop.Token.ThrowIfCancellationRequested();
                    if (!clear)
                    {
                        reserved = subscription.IncrementEventCount();
                        Assert.Equal(3, reserved);
                    }
                    bool acquired = Monitor.TryEnter(subscription.GeneratedEvents, TimeSpan.Zero);
                    if (acquired)
                    {
                        Monitor.Exit(subscription.GeneratedEvents);
                    }
                    Assert.False(acquired, label + ": mutation worker must contend with real conversion.");
                    attemptOrder = Interlocked.Increment(ref ordering);
                    attempted.Set();
                    if (clear)
                    {
                        subscription.ClearEvents();
                    }
                    else
                    {
                        // Register directly: SubscriptionCapture.Sent is intentionally not a concurrent list.
                        subscription.RegisterEvent(producerTemplate with { EventNumber = reserved });
                    }
                    Interlocked.Exchange(ref completionOrder, Interlocked.Increment(ref ordering));
                }
                finally
                {
                    attempted.Set();
                }
            });
            Assert.True(attempted.Wait(TimeSpan.FromSeconds(10)), label + ": mutation was not attempted.");
            Assert.True(Volatile.Read(ref attemptOrder) > 0, label + ": contended acquisition failed.");
            Assert.Equal(0, Volatile.Read(ref completionOrder));
            Assert.False(mutation.IsCompleted);
            Assert.Equal(clear ? 2 : 3, subscription.CurrentEventCount);
            releaseOrder = Interlocked.Increment(ref ordering);
            gate.Release.Set();
            await Task.WhenAll(conversion, mutation).WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(attemptOrder < releaseOrder && releaseOrder < completionOrder,
                $"{label}: attempt={attemptOrder}, release={releaseOrder}, completion={completionOrder}.");
            // All native status builders run before the context gate, hence before the producer reserves 3.
            // The initial total is 2 even though CurrentEventCount may already be 3 during conversion.
            if (response is not null)
            {
                AssertSubscriptionEventsResponse(replay, graph, kind + "-initial", response, "full-resource", events, 2);
            }
            else
            {
                AssertSubscriptionBundle(replay, graph, kind + "-initial", await conversion,
                    "query-event", "full-resource", events, 2);
            }
            NotificationEventContract[] subsequent = clear ? []
                : [events[0], events[1], events[1] with { Number = 3 }];
            await AssertSubscriptionEventsReplay(replay, graph, kind + "-after", "GET", 0, long.MaxValue,
                "full-resource", subsequent, clear ? 2 : 3);
            Assert.Equal(clear ? 0 : 3, subscription.GeneratedEvents.Count);
            Assert.Equal(clear ? 2 : 3, subscription.CurrentEventCount);
            Assert.Equal(2, capture.Sent.Count);
            Assert.Empty(subscription.NotificationErrors);
            _subscriptionOutput.WriteLine($"H2 conversion {label}: method={method}, monitor-owned=True, contended=True, initial-selected=[1,2], initial-total=2, reserved={reserved}, attempt/release/completion={attemptOrder}/{releaseOrder}/{completionOrder}, after-selected=[{string.Join(",", subsequent.Select(e => e.Number))}], after-total={subscription.CurrentEventCount}.");
        }
        finally
        {
            stop.Cancel();
            gate.Release.Set();
            attempted.Set();
            // Release before observing every worker, including when a gate/assertion failed.
            await Task.WhenAll(mutation is null ? [conversion] : new Task[] { conversion, mutation })
                .WaitAsync(TimeSpan.FromSeconds(10));
        }
    }

    private async Task RunSubscriptionEventsEmptyOverlap(object[] row)
    {
        const int Iterations = 4;
        string label = (string)row[0];
        FhirReleases.FhirSequenceCodes version = (FhirReleases.FhirSequenceCodes)row[1];
        CompatibilityTransport transport = (CompatibilityTransport)row[2];
        CompatibilityFormat format = (CompatibilityFormat)row[3];
        string kind = (string)row[4];
        CompatibilityTenant tenant = Tenant(version, $"empty-overlap-{VersionName(version)}");
        CompatibilityScenario scenario = new(label, SubscriptionEvidence, [tenant], [MetadataStep(tenant, format)]);
        await using CompatibilityReplay replay = new(scenario, transport);
        await replay.RunAsync();
        using SubscriptionCapture capture = new(replay.GetOwnedStore(tenant.Name));
        SubscriptionGraph graph = await SeedSubscriptionGraph(replay, tenant, format, "fhirpath");
        ParsedSubscription subscription = AssertParsedSubscription(capture.Store, "fhirpath", version);
        await GenerateSubscriptionReplayEvents(replay, graph, capture, 1);
        SubscriptionEvent template = subscription.GeneratedEvents[1];
        long since = kind == "reversed" ? 2 : 100;
        long until = kind == "reversed" ? 1 : long.MaxValue;
        long[] starts = new long[Iterations];
        long[] ends = new long[Iterations];
        long[] registrations = new long[Iterations];
        int completedRegistrations = 0;
        int completedReplays = 0;
        using CancellationTokenSource stop = new();
        for (int iteration = 0; iteration < Iterations; iteration++)
        {
            int index = iteration;
            using ManualResetEventSlim producerReady = new();
            using ManualResetEventSlim replayReady = new();
            using ManualResetEventSlim start = new();
            using ManualResetEventSlim replayStarted = new();
            string method = index % 2 == 0 ? "GET" : "POST";
            Task producer = Task.Run(() =>
            {
                stop.Token.ThrowIfCancellationRequested();
                long number = subscription.IncrementEventCount();
                Assert.Equal(index + 2, number);
                producerReady.Set();
                Assert.True(start.Wait(TimeSpan.FromSeconds(10), stop.Token));
                // Make completed registration (not just reservation) strictly later than replay start.
                Assert.True(replayStarted.Wait(TimeSpan.FromSeconds(10), stop.Token));
                subscription.RegisterEvent(template with { EventNumber = number });
                registrations[index] = Stopwatch.GetTimestamp();
                Interlocked.Increment(ref completedRegistrations);
            });
            Task<CompatibilityResponse> consumer = Task.Run(async () =>
            {
                replayReady.Set();
                Assert.True(start.Wait(TimeSpan.FromSeconds(10), stop.Token));
                starts[index] = Stopwatch.GetTimestamp();
                replayStarted.Set();
                CompatibilityResponse result = await replay.SendAsync(SubscriptionEventsRequest(graph, method, since, until, "full-resource"));
                ends[index] = Stopwatch.GetTimestamp();
                Interlocked.Increment(ref completedReplays);
                return result;
            });
            try
            {
                Assert.True(producerReady.Wait(TimeSpan.FromSeconds(10)), label + ": producer readiness.");
                Assert.True(replayReady.Wait(TimeSpan.FromSeconds(10)), label + ": replay readiness.");
                // Both workers exist and are ready before either is released or awaited.
                start.Set();
                await Task.WhenAll(producer, consumer).WaitAsync(TimeSpan.FromSeconds(10));
                AssertSubscriptionEventsResponse(replay, graph, kind + "-" + index, await consumer,
                    "full-resource", [], index + 2);
                Assert.Equal(index + 1, completedRegistrations);
                Assert.Equal(index + 1, completedReplays);
                Assert.True(starts[index] < registrations[index]);
                Assert.True(starts[index] <= ends[index]);
                Assert.InRange(subscription.GeneratedEvents.Count, 1, 3);
                // Keep retained history tiny without changing the reserved counter or notification capture.
                subscription.ClearEvents();
                subscription.RegisterEvent(template with { EventNumber = index + 2 });
                _subscriptionOutput.WriteLine($"H2 empty {label}: pair={index}, method={method}, selected=[], total={index + 2}, replay-start={starts[index]}, registration-completed={registrations[index]}, replay-completed={ends[index]}.");
            }
            finally
            {
                // No next pair starts until this pair is joined, even on a failed readiness/assertion.
                start.Set();
                replayStarted.Set();
                try
                {
                    await Task.WhenAll(producer, consumer).WaitAsync(TimeSpan.FromSeconds(10));
                }
                catch
                {
                    stop.Cancel();
                    throw;
                }
            }
        }
        Assert.Equal(Iterations, completedRegistrations);
        Assert.Equal(Iterations, completedReplays);
        int inside = registrations.Count(timestamp => starts[0] < timestamp && timestamp < ends[^1]);
        Assert.InRange(inside, 1, Iterations);
        Assert.Equal(5, subscription.CurrentEventCount);
        Assert.Single(capture.Sent);
        Assert.Empty(subscription.NotificationErrors);
        _subscriptionOutput.WriteLine($"H2 empty window {label}: joined-pairs={Iterations}, completed-registrations={completedRegistrations}, strictly-inside-replay-window={inside}, GET=2, complete-POST=2, final-total=5.");
    }

    private static IEnumerable<object[]> SubscriptionEventsMethodRows(string group, string[] kinds)
    {
        foreach (object[] row in OperationRows(group, kinds))
        {
            foreach (string method in new[] { "GET", "POST" })
            {
                yield return [$"{row[0]} / {method}", row[1], row[2], row[3], row[4], method];
            }
        }
    }

    private static void AssertSubscriptionEventsMatrix(IEnumerable<object[]> matrix, string[] kinds, bool methods)
    {
        object[][] rows = matrix.ToArray();
        Assert.NotEmpty(rows);
        Assert.Equal(rows.Length, rows.Select(row => (string)row[0]).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(new[] { FhirReleases.FhirSequenceCodes.R4, FhirReleases.FhirSequenceCodes.R4B, FhirReleases.FhirSequenceCodes.R5 },
            rows.Select(row => (FhirReleases.FhirSequenceCodes)row[1]).Distinct().OrderBy(version => version));
        Assert.Equal(new[] { CompatibilityTransport.Http, CompatibilityTransport.Direct },
            rows.Select(row => (CompatibilityTransport)row[2]).Distinct().OrderBy(transport => transport));
        Assert.Equal(new[] { CompatibilityFormat.Json, CompatibilityFormat.Xml },
            rows.Select(row => (CompatibilityFormat)row[3]).Distinct().OrderBy(format => format));
        Assert.Equal(kinds.OrderBy(kind => kind, StringComparer.Ordinal),
            rows.Select(row => (string)row[4]).Distinct().OrderBy(kind => kind, StringComparer.Ordinal));
        foreach (FhirReleases.FhirSequenceCodes version in new[]
            { FhirReleases.FhirSequenceCodes.R4, FhirReleases.FhirSequenceCodes.R4B, FhirReleases.FhirSequenceCodes.R5 })
        {
            foreach (CompatibilityTransport transport in new[] { CompatibilityTransport.Http, CompatibilityTransport.Direct })
            {
                foreach (CompatibilityFormat format in new[] { CompatibilityFormat.Json, CompatibilityFormat.Xml })
                {
                    foreach (string kind in kinds)
                    {
                        object[][] selected = rows.Where(row => (FhirReleases.FhirSequenceCodes)row[1] == version &&
                            (CompatibilityTransport)row[2] == transport && (CompatibilityFormat)row[3] == format &&
                            (string)row[4] == kind).ToArray();
                        Assert.Equal(methods ? 2 : 1, selected.Length);
                        if (methods)
                        {
                            Assert.Equal(new[] { "GET", "POST" }, selected.Select(row => (string)row[5]).OrderBy(method => method, StringComparer.Ordinal));
                        }
                    }
                }
            }
        }
    }

    private static async Task<NotificationEventContract[]> GenerateSubscriptionReplayEvents(
        CompatibilityReplay replay, SubscriptionGraph graph, SubscriptionCapture capture, int count)
    {
        Assert.InRange(count, 0, 3);
        List<NotificationEventContract> events = [];
        for (int number = 1; number <= count; number++)
        {
            string encounter = WithWireId(OperationWithoutId(SubscriptionEncounter(graph.Tenant.Version, graph.Format, complete: true),
                graph.Format), graph.Format, "replay-event-" + number.ToString(CultureInfo.InvariantCulture));
            string identity = await WriteSubscriptionResource(replay, graph, encounter);
            SubscriptionEvent actual = AssertCapturedSubscriptionEvent(capture, graph, number, identity, replay.Bindings.StartedAt);
            events.Add(new(number, identity, actual.Timestamp));
        }
        Assert.Equal(count, capture.Sent.Count);
        ParsedSubscription subscription = Assert.Single(capture.Store.CurrentSubscriptions);
        Assert.Equal(count, subscription.CurrentEventCount);
        Assert.Equal(count, subscription.GeneratedEvents.Count);
        Assert.Empty(subscription.NotificationErrors);
        return events.ToArray();
    }

    private static void ReverseSubscriptionHistory(ParsedSubscription subscription)
    {
        lock (subscription.GeneratedEvents)
        {
            SubscriptionEvent first = subscription.GeneratedEvents[1];
            SubscriptionEvent second = subscription.GeneratedEvents[2];
            subscription.ClearEvents();
            subscription.RegisterEvent(second);
            subscription.RegisterEvent(first);
            Assert.Equal(new long[] { 2, 1 }, subscription.GeneratedEvents.Keys);
        }
    }

    private static string SubscriptionEventsParameters(SubscriptionGraph graph, long since, long until, string contentLevel)
    {
        string integerChoice = graph.Tenant.Version == FhirReleases.FhirSequenceCodes.R5 ? "valueInteger64" : "valueString";
        string lower = since.ToString(CultureInfo.InvariantCulture);
        string upper = until.ToString(CultureInfo.InvariantCulture);
        return graph.Format == CompatibilityFormat.Json
            ? $$$"""{"resourceType":"Parameters","parameter":[{"name":"eventsSinceNumber","{{{integerChoice}}}":"{{{lower}}}"},{"name":"eventsUntilNumber","{{{integerChoice}}}":"{{{upper}}}"},{"name":"content","valueCode":"{{{contentLevel}}}"}]}"""
            : $"""<Parameters xmlns="http://hl7.org/fhir"><parameter><name value="eventsSinceNumber"/><{integerChoice} value="{lower}"/></parameter><parameter><name value="eventsUntilNumber"/><{integerChoice} value="{upper}"/></parameter><parameter><name value="content"/><valueCode value="{contentLevel}"/></parameter></Parameters>""";
    }

    private static string SubscriptionEventsPartialParameters(
        SubscriptionGraph graph, IReadOnlyList<(string Name, string? Value)> members)
    {
        string integerChoice = graph.Tenant.Version == FhirReleases.FhirSequenceCodes.R5 ? "valueInteger64" : "valueString";
        if (graph.Format == CompatibilityFormat.Json)
        {
            JsonObject resource = new() { ["resourceType"] = "Parameters" };
            JsonArray parameters = new();
            foreach ((string name, string? value) in members)
            {
                JsonObject parameter = new() { ["name"] = name };
                if (value is not null)
                {
                    string valueChoice = name switch
                    {
                        "eventsSinceNumber" or "eventsUntilNumber" => integerChoice,
                        "content" => "valueCode",
                        "unrelated" => "valueString",
                        _ => throw new ArgumentOutOfRangeException(nameof(members)),
                    };
                    parameter[valueChoice] = value;
                }
                parameters.Add(parameter);
            }
            if (parameters.Count == 0)
            {
                return WithWireId(resource.ToJsonString(), graph.Format, SubscriptionEventsEmptyEnvelopeId);
            }
            resource["parameter"] = parameters;
            return resource.ToJsonString();
        }

        XElement root = new(_exchangeFhir + "Parameters");
        foreach ((string name, string? value) in members)
        {
            XElement parameter = new(_exchangeFhir + "parameter",
                new XElement(_exchangeFhir + "name", new XAttribute("value", name)));
            if (value is not null)
            {
                string valueChoice = name switch
                {
                    "eventsSinceNumber" or "eventsUntilNumber" => integerChoice,
                    "content" => "valueCode",
                    "unrelated" => "valueString",
                    _ => throw new ArgumentOutOfRangeException(nameof(members)),
                };
                parameter.Add(new XElement(_exchangeFhir + valueChoice, new XAttribute("value", value)));
            }
            root.Add(parameter);
        }
        string xml = root.ToString(SaveOptions.DisableFormatting);
        return members.Count == 0 ? WithWireId(xml, graph.Format, SubscriptionEventsEmptyEnvelopeId) : xml;
    }

    private (HttpStatusCode Status, Hl7.Fhir.Model.Resource? Resource, string Error) DeserializeSubscriptionEventsEnvelope(
        string label, FhirReleases.FhirSequenceCodes version, CompatibilityFormat format, string content)
    {
        string mediaType = WireContractAssertions.MediaType(format);
        Hl7.Fhir.Model.Resource? resource;
        string exMessage;
        HttpStatusCode status = version switch
        {
            FhirReleases.FhirSequenceCodes.R4 => candleR4::FhirCandle.Serialization.SerializationUtils.TryDeserializeFhir(
                content, mediaType, out resource, out exMessage, lenient: false),
            FhirReleases.FhirSequenceCodes.R4B => candleR4B::FhirCandle.Serialization.SerializationUtils.TryDeserializeFhir(
                content, mediaType, out resource, out exMessage, lenient: false),
            FhirReleases.FhirSequenceCodes.R5 => candleR5::FhirCandle.Serialization.SerializationUtils.TryDeserializeFhir(
                content, mediaType, out resource, out exMessage, lenient: false),
            _ => throw new ArgumentOutOfRangeException(nameof(version)),
        };
        _subscriptionOutput.WriteLine($"{label} / {version} / {format} wrapper\nRequest: {content}\n" +
            $"Status: {(int)status} {status}\nResource type: {resource?.GetType().FullName ?? "(null)"}\nError: {exMessage}");
        return (status, resource, exMessage);
    }

    private static void AssertSubscriptionEventsEmptyEnvelope(
        CompatibilityFormat format, string content, (HttpStatusCode Status, Hl7.Fhir.Model.Resource? Resource, string Error) envelope)
    {
        if (format == CompatibilityFormat.Json)
        {
            AssertExchangeMembers(content, format, ["resourceType", "id"]);
            Assert.Equal("Parameters", ExchangeResourceType(content, format));
            Assert.Equal(SubscriptionEventsEmptyEnvelopeId, ExchangePrimitive(content, format, "id"));
        }
        else
        {
            XElement expected = new(_exchangeFhir + "Parameters",
                new XAttribute("xmlns", _exchangeFhir.NamespaceName),
                new XElement(_exchangeFhir + "id", new XAttribute("value", SubscriptionEventsEmptyEnvelopeId)));
            Assert.True(XNode.DeepEquals(expected, ExchangeXml(content)), $"Expected a Parameters root with only an id: {content}");
        }
        Assert.Equal(HttpStatusCode.OK, envelope.Status);
        Assert.Empty(envelope.Error);
        Hl7.Fhir.Model.Parameters parameters = Assert.IsType<Hl7.Fhir.Model.Parameters>(envelope.Resource);
        Assert.Equal(SubscriptionEventsEmptyEnvelopeId, parameters.Id);
        Assert.Empty(parameters.Parameter);
    }

    private string CaptureSubscriptionEventsExchange(string label, string path, string content, CompatibilityResponse response)
    {
        string body = response.Transport == CompatibilityTransport.Http
            ? $"HTTP body: {response.HttpBody ?? "(null)"}"
            : $"Direct success: {response.Direct?.Success.ToString() ?? "(null)"}\n" +
              $"Direct resource: {response.Direct?.SerializedResource ?? "(null)"}\n" +
              $"Direct outcome: {response.Direct?.SerializedOutcome ?? "(null)"}";
        string context = $"{label}: POST {path}\nRequest: {content}\nStatus: {(int)response.Status} {response.Status}\n{body}";
        _subscriptionOutput.WriteLine(context);
        return context;
    }

    private static void AssertSubscriptionEventsPostSucceeded(CompatibilityResponse response, string context)
    {
        Assert.True(response.Status == HttpStatusCode.OK, context);
        if (response.Transport == CompatibilityTransport.Direct)
        {
            Assert.True(response.Direct?.Success == true, context);
        }
    }

    private static CompatibilityRequest SubscriptionEventsRequest(
        SubscriptionGraph graph, string method, long since, long until, string contentLevel)
    {
        string path = $"Subscription/{SubscriptionId}/$events";
        if (method == "GET")
        {
            path += $"?eventsSinceNumber={since.ToString(CultureInfo.InvariantCulture)}&eventsUntilNumber={until.ToString(CultureInfo.InvariantCulture)}&content={contentLevel}";
        }
        return new(graph.Tenant.Name, method, path)
        {
            Format = graph.Format,
            Content = method == "POST" ? SubscriptionEventsParameters(graph, since, until, contentLevel) : null,
        };
    }

    private static async Task AssertSubscriptionEventsReplay(
        CompatibilityReplay replay, SubscriptionGraph graph, string name, string method, long since, long until,
        string contentLevel, IReadOnlyList<NotificationEventContract> events, long total)
    {
        CompatibilityResponse response = await replay.SendAsync(SubscriptionEventsRequest(graph, method, since, until, contentLevel));
        AssertSubscriptionEventsResponse(replay, graph, name, response, contentLevel, events, total);
    }

    private static void AssertSubscriptionEventsResponse(
        CompatibilityReplay replay, SubscriptionGraph graph, string name, CompatibilityResponse response,
        string contentLevel, IReadOnlyList<NotificationEventContract> events, long total)
    {
        string bundle = ExchangeResource(response);
        AssertExchangeContainer(replay, graph.Tenant, graph.Format, name, response, "Bundle",
            ExchangePrimitive(bundle, graph.Format, "id"),
            new OutcomeBodyExpectation(name + "-outcome", [new("information", "success", $"Events for subscription {SubscriptionId}.")]));
        AssertSubscriptionBundle(replay, graph, name, bundle, "query-event", contentLevel, events, total);
    }

    private static Hl7.Fhir.Model.Bundle? ConcreteSubscriptionEventsBundle(
        IFhirStore store, string notificationType, bool? allowEmptySelectionFallback = null, string subscriptionId = SubscriptionId) =>
        store switch
        {
            candleR4::FhirCandle.Storage.VersionedFhirStore r4 => allowEmptySelectionFallback.HasValue
                ? r4.BundleForSubscriptionEvents(subscriptionId, Array.Empty<long>(), notificationType, "full-resource",
                    allowEmptySelectionFallback: allowEmptySelectionFallback.Value)
                : r4.BundleForSubscriptionEvents(subscriptionId, Array.Empty<long>(), notificationType, "full-resource"),
            candleR4B::FhirCandle.Storage.VersionedFhirStore r4b => allowEmptySelectionFallback.HasValue
                ? r4b.BundleForSubscriptionEvents(subscriptionId, Array.Empty<long>(), notificationType, "full-resource",
                    allowEmptySelectionFallback: allowEmptySelectionFallback.Value)
                : r4b.BundleForSubscriptionEvents(subscriptionId, Array.Empty<long>(), notificationType, "full-resource"),
            candleR5::FhirCandle.Storage.VersionedFhirStore r5 => allowEmptySelectionFallback.HasValue
                ? r5.BundleForSubscriptionEvents(subscriptionId, Array.Empty<long>(), notificationType, "full-resource",
                    allowEmptySelectionFallback: allowEmptySelectionFallback.Value)
                : r5.BundleForSubscriptionEvents(subscriptionId, Array.Empty<long>(), notificationType, "full-resource"),
            _ => throw new ArgumentOutOfRangeException(nameof(store)),
        };

    private static string SerializeSubscriptionEventsBundle(
        FhirReleases.FhirSequenceCodes version, CompatibilityFormat format, Hl7.Fhir.Model.Bundle bundle) =>
        version switch
        {
            FhirReleases.FhirSequenceCodes.R4 => candleR4::FhirCandle.Serialization.SerializationUtils.SerializeFhir(
                bundle, WireContractAssertions.MediaType(format), false),
            FhirReleases.FhirSequenceCodes.R4B => candleR4B::FhirCandle.Serialization.SerializationUtils.SerializeFhir(
                bundle, WireContractAssertions.MediaType(format), false),
            FhirReleases.FhirSequenceCodes.R5 => candleR5::FhirCandle.Serialization.SerializationUtils.SerializeFhir(
                bundle, WireContractAssertions.MediaType(format), false),
            _ => throw new ArgumentOutOfRangeException(nameof(version)),
        };

    private sealed class SubscriptionCapture : IDisposable
    {
        internal SubscriptionCapture(IFhirStore store)
        {
            Store = store;
            store.OnSubscriptionSendEvent += OnSent;
            store.OnReceivedSubscriptionEvent += OnReceived;
        }

        internal IFhirStore Store { get; }
        internal List<(object? Sender, SubscriptionSendEventArgs Args)> Sent { get; } = [];
        internal List<(object? Sender, ReceivedSubscriptionEventArgs Args)> Received { get; } = [];

        private void OnSent(object? sender, SubscriptionSendEventArgs args) => Sent.Add((sender, args));
        private void OnReceived(object? sender, ReceivedSubscriptionEventArgs args) => Received.Add((sender, args));

        public void Dispose()
        {
            Store.OnSubscriptionSendEvent -= OnSent;
            Store.OnReceivedSubscriptionEvent -= OnReceived;
        }
    }

    private static async Task<SubscriptionGraph> SeedSubscriptionGraph(
        CompatibilityReplay replay, CompatibilityTenant tenant, CompatibilityFormat format, string criteria, string? priorEncounter = null)
    {
        SubscriptionGraph graph = new(tenant, format);
        await WriteSubscriptionResource(replay, graph, SubscriptionFixture(tenant.Version, format, "patient"));
        await WriteSubscriptionResource(replay, graph, SubscriptionFixture(tenant.Version, format, "other-patient"));
        if (priorEncounter is not null)
        {
            await WriteSubscriptionResource(replay, graph, priorEncounter);
        }
        string topic = SubscriptionFixture(tenant.Version, format, "topic-" + criteria);
        string topicIdentity = await WriteSubscriptionResource(replay, graph, topic);
        await AssertSubscriptionRead(replay, graph, topicIdentity);
        string subscriptionIdentity = await WriteSubscriptionResource(replay, graph, SubscriptionFixture(tenant.Version, format, "subscription"));
        await AssertSubscriptionRead(replay, graph, subscriptionIdentity);
        return graph;
    }

    private static async Task<string> WriteSubscriptionResource(
        CompatibilityReplay replay, SubscriptionGraph graph, string content, bool post = false)
    {
        string resourceType = ExchangeResourceType(content, graph.Format);
        string id = ExchangePrimitive(content, graph.Format, "id");
        string identity = resourceType + "/" + id;
        graph.Bindings.TryGetValue(identity, out GeneratedResourceExpectation? previous);
        string version = previous is null ? "1" : (int.Parse(previous.ExpectedVersion!, CultureInfo.InvariantCulture) + 1).ToString(CultureInfo.InvariantCulture);
        string name = graph.Tenant.Name + "/" + identity + "/v" + version;
        if (post)
        {
            Assert.Null(previous);
            content = OperationWithoutId(content, graph.Format);
        }
        GeneratedResourceExpectation binding = new(name, resourceType, version, post ? null : id, PreviousBinding: previous?.Name);
        await replay.StepAsync(new(name,
            new(graph.Tenant.Name, post ? "POST" : "PUT", post ? resourceType : identity) { Format = graph.Format, Content = content },
            ResourceResponse(name, content, binding, previous is null ? HttpStatusCode.Created : HttpStatusCode.OK)));
        identity = resourceType + "/" + replay.Bindings.Resources[binding.Name].Id;
        graph.Content[identity] = content;
        graph.Bindings[identity] = binding;
        return identity;
    }

    private static async Task AssertSubscriptionRead(CompatibilityReplay replay, SubscriptionGraph graph, string identity)
    {
        GeneratedResourceExpectation binding = graph.Bindings[identity];
        await replay.StepAsync(BoundRead(graph.Tenant.Name + "/read/" + identity + "/" + replay.Responses.Count,
            graph.Tenant, graph.Format, graph.Content[identity], binding));
    }

    private static async Task DeleteSubscriptionResource(CompatibilityReplay replay, SubscriptionGraph graph, string identity)
    {
        string name = graph.Tenant.Name + "/delete/" + identity;
        await replay.StepAsync(new(name, new(graph.Tenant.Name, "DELETE", identity) { Format = graph.Format },
            ResourceResponse(name, graph.Content[identity], graph.Bindings[identity] with { Mode = CompatibilityBindingMode.Reuse },
                HttpStatusCode.OK, resourceHeaders: false)));
        CompatibilityResponse missing = await replay.SendAsync(new(graph.Tenant.Name, "GET", identity) { Format = graph.Format });
        AssertExchangeFailureResponse(replay.Bindings, graph.Tenant, graph.Format, name + "-missing", missing, HttpStatusCode.NotFound,
            "exception", $"Resource: {identity} not found", checkMediaType: true);
    }

    private static ParsedSubscription AssertParsedSubscription(IFhirStore store, string criteria, FhirReleases.FhirSequenceCodes version)
    {
        ParsedSubscriptionTopic topic = Assert.Single(store.CurrentTopics);
        Assert.Equal("sdk6-encounter", topic.Id);
        Assert.Equal(SubscriptionTopicUrl, topic.Url);
        Assert.Equal(criteria == "query" ? 2 : 1, topic.ResourceTriggers["Encounter"].Count);
        Assert.Equal("patient", Assert.Single(topic.AllowedFilters["Encounter"]).Name);
        Assert.Equal(new[] { "_include=Encounter:patient" }, Assert.Single(topic.NotificationShapes["Encounter"]).Includes);
        Assert.Empty(topic.EventTriggers);
        string complete = version == FhirReleases.FhirSequenceCodes.R5 ? "completed" : "finished";
        ParsedSubscriptionTopic.ResourceTrigger trigger = topic.ResourceTriggers["Encounter"][0];
        Assert.True(trigger.OnCreate);
        Assert.True(trigger.OnUpdate);
        if (criteria == "fhirpath")
        {
            Assert.True(trigger.OnDelete);
            Assert.Equal(SubscriptionFhirPath(complete), trigger.FhirPathCriteria);
            Assert.Empty(trigger.QueryCurrent);
        }
        else if (criteria == "query")
        {
            Assert.Empty(trigger.FhirPathCriteria);
            Assert.Equal("status:not=" + complete, trigger.QueryPrevious);
            Assert.Equal("status=" + complete, trigger.QueryCurrent);
            Assert.True(trigger.CreateAutoPass);
            Assert.True(trigger.RequireBothQueries);
            Assert.True(topic.ResourceTriggers["Encounter"][1].DeleteAutoPass);
        }
        ParsedSubscription subscription = Assert.Single(store.CurrentSubscriptions);
        Assert.Equal(SubscriptionId, subscription.Id);
        Assert.Equal(SubscriptionTopicUrl, subscription.TopicUrl);
        Assert.Equal("active", subscription.CurrentStatus);
        Assert.Equal("rest-hook", subscription.ChannelCode);
        Assert.Equal(version == FhirReleases.FhirSequenceCodes.R5
            ? "http://terminology.hl7.org/CodeSystem/subscription-channel-type" : string.Empty, subscription.ChannelSystem);
        Assert.Equal(SubscriptionEndpoint, subscription.Endpoint);
        Assert.True(new Uri(subscription.Endpoint).IsLoopback);
        Assert.Equal("application/fhir+json", subscription.ContentType);
        Assert.Equal("full-resource", subscription.ContentLevel);
        Assert.Equal(120, subscription.HeartbeatSeconds);
        Assert.Equal(15, subscription.TimeoutSeconds);
        Assert.Equal(5, subscription.MaxEventsPerNotification);
        Assert.Equal(new[] { "phase5" }, subscription.Parameters["X-Synthetic"]);
        Assert.Equal(new ParsedSubscription.SubscriptionFilter("Encounter", "patient", string.Empty, string.Empty, "Patient/sub-patient"),
            Assert.Single(subscription.Filters["Encounter"]));
        Assert.Empty(subscription.NotificationErrors);
        return subscription;
    }

    private static SubscriptionEvent AssertCapturedSubscriptionEvent(
        SubscriptionCapture capture, SubscriptionGraph graph, long number, string identity, DateTimeOffset started)
    {
        (object? sender, SubscriptionSendEventArgs args) = capture.Sent[checked((int)number - 1)];
        Assert.Same(capture.Store, sender);
        Assert.Equal(graph.Tenant.Name, args.Tenant.ControllerName);
        Assert.Equal(graph.Tenant.BaseUrl, args.Tenant.BaseUrl);
        Assert.Equal(ParsedSubscription.NotificationTypeCodes.EventNotification, args.NotificationType);
        Assert.Same(Assert.Single(capture.Store.CurrentSubscriptions), args.Subscription);
        SubscriptionEvent subscriptionEvent = Assert.Single(args.NotificationEvents);
        Assert.Same(args.Subscription.GeneratedEvents[number], subscriptionEvent);
        Assert.Equal(number, subscriptionEvent.EventNumber);
        Assert.Equal(SubscriptionId, subscriptionEvent.SubscriptionId);
        Assert.Equal(SubscriptionTopicUrl, subscriptionEvent.TopicUrl);
        Assert.Equal("active", subscriptionEvent.StatusAtGeneration);
        Assert.InRange(subscriptionEvent.Timestamp, started, DateTimeOffset.UtcNow.AddSeconds(5));
        Assert.True(capture.Store.TryGetResourceInfo(subscriptionEvent.Focus, out string type, out string id));
        Assert.Equal(identity, type + "/" + id);
        object context = Assert.Single(subscriptionEvent.AdditionalContext!);
        Assert.True(capture.Store.TryGetResourceInfo(context, out type, out id));
        Assert.Equal("Patient/sub-patient", type + "/" + id);
        return subscriptionEvent;
    }

    private static string SubscriptionEncounter(
        FhirReleases.FhirSequenceCodes version, CompatibilityFormat format, bool complete, bool otherPatient = false)
    {
        string encounter = SubscriptionFixture(version, format, complete ? "complete" : "planned");
        return otherPatient ? encounter.Replace("Patient/sub-patient", "Patient/other-patient", StringComparison.Ordinal) : encounter;
    }

    private static string SubscriptionFhirPath(string complete) =>
        $"((%previous.empty() or (%previous.status != '{complete}')) and (%current.status = '{complete}')) or (%current.empty() and (%previous.status = '{complete}'))";

    private static string SubscriptionFixture(FhirReleases.FhirSequenceCodes version, CompatibilityFormat format, string kind)
    {
        if (format == CompatibilityFormat.Json)
        {
            using JsonDocument fixture = JsonDocument.Parse(CompatibilityReplay.LoadFixture(version, "subscription-resources.json"));
            return fixture.RootElement.GetProperty(kind).GetRawText();
        }
        if (kind.StartsWith("topic-", StringComparison.Ordinal))
        {
            return SubscriptionTopicXml(version, kind["topic-".Length..]);
        }
        if (kind == "subscription")
        {
            if (version == FhirReleases.FhirSequenceCodes.R5)
            {
                return $"""
                    <Subscription xmlns="http://hl7.org/fhir"><id value="local-subscription"/><status value="active"/>
                    <topic value="{SubscriptionTopicUrl}"/><reason value="Synthetic local phase 5 subscription"/>
                    <filterBy><resourceType value="Encounter"/><filterParameter value="patient"/><value value="Patient/sub-patient"/></filterBy>
                    <channelType><system value="http://terminology.hl7.org/CodeSystem/subscription-channel-type"/><code value="rest-hook"/></channelType>
                    <endpoint value="{SubscriptionEndpoint}"/><parameter><name value="X-Synthetic"/><value value="phase5"/></parameter>
                    <heartbeatPeriod value="120"/><timeout value="15"/><contentType value="application/fhir+json"/><content value="full-resource"/><maxCount value="5"/></Subscription>
                    """;
            }
            return $"""
                <Subscription xmlns="http://hl7.org/fhir"><id value="local-subscription"/><status value="active"/>
                <reason value="Synthetic local phase 5 subscription"/><criteria value="{SubscriptionTopicUrl}">
                <extension url="{BackportExtensionRoot}backport-filter-criteria"><valueString value="Encounter?patient=Patient/sub-patient"/></extension></criteria>
                <channel><extension url="{BackportExtensionRoot}backport-heartbeat-period"><valueInteger value="120"/></extension>
                <extension url="{BackportExtensionRoot}backport-timeout"><valueInteger value="15"/></extension>
                <extension url="{BackportExtensionRoot}backport-max-count"><valueInteger value="5"/></extension>
                <type value="rest-hook"/><endpoint value="{SubscriptionEndpoint}"/><payload value="application/fhir+json">
                <extension url="{BackportExtensionRoot}backport-payload-content"><valueCode value="full-resource"/></extension></payload>
                <header value="X-Synthetic: phase5"/></channel></Subscription>
                """;
        }
        if (kind == "patient")
        {
            return """<Patient xmlns="http://hl7.org/fhir"><id value="sub-patient"/><active value="true"/><name><family value="Synthetic Subscription"/><given value="Local"/></name></Patient>""";
        }
        if (kind == "other-patient")
        {
            return """<Patient xmlns="http://hl7.org/fhir"><id value="other-patient"/><active value="true"/><name><family value="Filter Negative"/></name></Patient>""";
        }
        string status = kind == "planned" ? "planned" : version == FhirReleases.FhirSequenceCodes.R5 ? "completed" : "finished";
        string encounterClass = version == FhirReleases.FhirSequenceCodes.R5
            ? """<class><coding><system value="http://terminology.hl7.org/CodeSystem/v3-ActCode"/><code value="AMB"/></coding></class>"""
            : """<class><system value="http://terminology.hl7.org/CodeSystem/v3-ActCode"/><code value="AMB"/></class>""";
        return $"""<Encounter xmlns="http://hl7.org/fhir"><id value="encounter-under-test"/><language value="en"/><status value="{status}"/>{encounterClass}<subject><reference value="Patient/sub-patient"/></subject></Encounter>""";
    }

    private static string SubscriptionTopicXml(FhirReleases.FhirSequenceCodes version, string criteria)
    {
        string complete = version == FhirReleases.FhirSequenceCodes.R5 ? "completed" : "finished";
        string fhirPath = SubscriptionFhirPath(complete);
        if (version == FhirReleases.FhirSequenceCodes.R4)
        {
            string trigger = criteria == "query"
                ? $"""
                    <extension url="{TopicExtensionRoot}resourceTrigger"><extension url="resource"><valueUri value="Encounter"/></extension>
                    <extension url="supportedInteraction"><valueCode value="create"/></extension><extension url="supportedInteraction"><valueCode value="update"/></extension>
                    <extension url="queryCriteria"><extension url="previous"><valueString value="status:not=finished"/></extension>
                    <extension url="resultForCreate"><valueCode value="test-passes"/></extension><extension url="current"><valueString value="status=finished"/></extension>
                    <extension url="resultForDelete"><valueCode value="test-fails"/></extension><extension url="requireBoth"><valueBoolean value="true"/></extension></extension></extension>
                    <extension url="{TopicExtensionRoot}resourceTrigger"><extension url="resource"><valueUri value="Encounter"/></extension>
                    <extension url="supportedInteraction"><valueCode value="delete"/></extension><extension url="queryCriteria">
                    <extension url="previous"><valueString value="status=finished"/></extension><extension url="resultForCreate"><valueCode value="test-fails"/></extension>
                    <extension url="resultForDelete"><valueCode value="test-passes"/></extension><extension url="requireBoth"><valueBoolean value="true"/></extension></extension></extension>
                    """
                : $"""
                    <extension url="{TopicExtensionRoot}resourceTrigger"><extension url="resource"><valueUri value="Encounter"/></extension>
                    <extension url="supportedInteraction"><valueCode value="create"/></extension><extension url="supportedInteraction"><valueCode value="update"/></extension>
                    <extension url="supportedInteraction"><valueCode value="delete"/></extension>
                    {(criteria == "fhirpath" ? $"""<extension url="fhirPathCriteria"><valueString value="{fhirPath}"/></extension>""" : string.Empty)}</extension>
                    """;
            return $"""
                <Basic xmlns="http://hl7.org/fhir"><id value="sdk6-encounter"/>
                <extension url="{TopicExtensionRoot}url"><valueUri value="{SubscriptionTopicUrl}"/></extension>{trigger}
                <extension url="{TopicExtensionRoot}canFilterBy"><extension url="resource"><valueUri value="Encounter"/></extension>
                <extension url="filterParameter"><valueString value="patient"/></extension></extension>
                <extension url="{TopicExtensionRoot}notificationShape"><extension url="resource"><valueUri value="Encounter"/></extension>
                <extension url="include"><valueString value="Encounter:patient"/></extension></extension>
                <modifierExtension url="{TopicExtensionRoot}status"><valueCode value="active"/></modifierExtension>
                <code><coding><system value="http://hl7.org/fhir/fhir-types"/><code value="SubscriptionTopic"/></coding></code></Basic>
                """;
        }
        string triggers = criteria == "query"
            ? $"""
                <resourceTrigger><resource value="Encounter"/><supportedInteraction value="create"/><supportedInteraction value="update"/>
                <queryCriteria><previous value="status:not={complete}"/><resultForCreate value="test-passes"/><current value="status={complete}"/>
                <resultForDelete value="test-fails"/><requireBoth value="true"/></queryCriteria></resourceTrigger>
                <resourceTrigger><resource value="Encounter"/><supportedInteraction value="delete"/><queryCriteria><previous value="status={complete}"/>
                <resultForCreate value="test-fails"/><resultForDelete value="test-passes"/><requireBoth value="true"/></queryCriteria></resourceTrigger>
                """
            : $"""
                <resourceTrigger><resource value="Encounter"/><supportedInteraction value="create"/><supportedInteraction value="update"/><supportedInteraction value="delete"/>
                {(criteria == "fhirpath" ? $"""<fhirPathCriteria value="{fhirPath}"/>""" : string.Empty)}</resourceTrigger>
                """;
        return $"""
            <SubscriptionTopic xmlns="http://hl7.org/fhir"><id value="sdk6-encounter"/><url value="{SubscriptionTopicUrl}"/><status value="active"/>
            {triggers}<canFilterBy><resource value="Encounter"/><filterParameter value="patient"/></canFilterBy>
            <notificationShape><resource value="Encounter"/><include value="Encounter:patient"/></notificationShape></SubscriptionTopic>
            """;
    }

    private static void AssertSubscriptionBundle(
        CompatibilityReplay replay,
        SubscriptionGraph graph,
        string name,
        string bundle,
        string notificationType,
        string contentLevel,
        IReadOnlyList<NotificationEventContract> events,
        long total)
    {
        CompatibilityFormat format = graph.Format;
        Assert.Equal("Bundle", ExchangeResourceType(bundle, format));
        AssertExchangeMembers(bundle, format, ["resourceType", "id", "type", "timestamp", "entry"]);
        Assert.Equal(graph.Tenant.Version == FhirReleases.FhirSequenceCodes.R5 ? "subscription-notification" : "history",
            ExchangePrimitive(bundle, format, "type"));
        string id = ExchangePrimitive(bundle, format, "id");
        Assert.True(Guid.TryParse(id, out _));
        replay.Bindings.Values.Add(name + "-bundle-id", new(name + "-bundle-id", id));
        string timestamp = ExchangePrimitive(bundle, format, "timestamp");
        DateTimeOffset instant = DateTimeOffset.Parse(timestamp, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        Assert.InRange(instant, replay.Bindings.StartedAt, DateTimeOffset.UtcNow.AddSeconds(5));
        replay.Bindings.Values.Add(name + "-bundle-time", new(name + "-bundle-time", timestamp));
        string[] entries = ExchangeRepeated(bundle, format, "entry");
        Assert.NotEmpty(entries);
        string status = ExchangeNestedResource(entries[0], format, "resource");
        AssertSubscriptionStatus(graph.Tenant, format, status, notificationType, contentLevel, events, total);
        string statusId = ExchangePrimitive(status, format, "id");
        Assert.True(Guid.TryParse(statusId, out _));
        Assert.NotEqual(id, statusId);
        replay.Bindings.Values.Add(name + "-status-id", new(name + "-status-id", statusId));
        Assert.Equal("urn:uuid:" + statusId, ExchangePrimitive(entries[0], format, "fullUrl"));

        List<string> expectedResources = [];
        if (contentLevel == "full-resource")
        {
            foreach (NotificationEventContract subscriptionEvent in events)
            {
                if (!expectedResources.Contains(subscriptionEvent.FocusIdentity, StringComparer.Ordinal))
                {
                    expectedResources.Add(subscriptionEvent.FocusIdentity);
                }
                if (!expectedResources.Contains("Patient/sub-patient", StringComparer.Ordinal))
                {
                    expectedResources.Add("Patient/sub-patient");
                }
            }
        }
        Assert.Equal(1 + expectedResources.Count, entries.Length);
        for (int index = 0; index < expectedResources.Count; index++)
        {
            string identity = expectedResources[index];
            string entry = entries[index + 1];
            AssertExchangeMembers(entry, format, ["fullUrl", "resource"]);
            Assert.Equal(graph.Tenant.BaseUrl + "/" + identity, ExchangePrimitive(entry, format, "fullUrl"));
            string resource = ExchangeNestedResource(entry, format, "resource");
            Assert.Equal(identity, SearchIdentity(resource, format));
            WireContractAssertions.Body(new ResourceBodyExpectation(graph.Content[identity],
                graph.Bindings[identity] with { Mode = CompatibilityBindingMode.Reuse }),
                resource, format, graph.Tenant.BaseUrl, replay.Bindings);
        }
        Assert.Equal(entries.Length, entries.Select(entry => ExchangePrimitive(entry, format, "fullUrl")).Distinct(StringComparer.Ordinal).Count());
    }

    private static void AssertSubscriptionStatus(
        CompatibilityTenant tenant,
        CompatibilityFormat format,
        string status,
        string notificationType,
        string contentLevel,
        IReadOnlyList<NotificationEventContract> events,
        long total)
    {
        bool backport = tenant.Version == FhirReleases.FhirSequenceCodes.R4;
        Assert.Equal(backport ? "Parameters" : "SubscriptionStatus", ExchangeResourceType(status, format));
        string subscriptionReference;
        string topic;
        string state;
        string type;
        string count;
        string[] notificationEvents;
        if (backport)
        {
            AssertExchangeMembers(status, format, ["resourceType", "id", "parameter"]);
            string[] parameters = ExchangeRepeated(status, format, "parameter");
            Assert.Equal(new[] { "subscription", "topic", "status", "type", "events-since-subscription-start" }
                .Concat(Enumerable.Repeat("notification-event", events.Count)),
                parameters.Select(parameter => ExchangePrimitive(parameter, format, "name")));
            subscriptionReference = ExchangePrimitive(ExchangeChild(parameters[0], format, "valueReference"), format, "reference");
            topic = ExchangePrimitive(parameters[1], format, "valueCanonical");
            state = ExchangePrimitive(parameters[2], format, "valueCode");
            type = ExchangePrimitive(parameters[3], format, "valueCode");
            count = ExchangePrimitive(parameters[4], format, "valueString");
            notificationEvents = parameters.Skip(5).ToArray();
        }
        else
        {
            List<string> members = ["resourceType", "id", "status", "type", "eventsSinceSubscriptionStart", "subscription", "topic"];
            if (events.Count != 0)
            {
                members.Add("notificationEvent");
            }
            AssertExchangeMembers(status, format, members.ToArray());
            subscriptionReference = ExchangePrimitive(ExchangeChild(status, format, "subscription"), format, "reference");
            topic = ExchangePrimitive(status, format, "topic");
            state = ExchangePrimitive(status, format, "status");
            type = ExchangePrimitive(status, format, "type");
            // R4B's string and R5's integer64 are both JSON strings, not lossy floating point numbers.
            count = ExchangePrimitive(status, format, "eventsSinceSubscriptionStart");
            notificationEvents = ExchangeHasMember(status, format, "notificationEvent")
                ? ExchangeRepeated(status, format, "notificationEvent") : [];
        }
        Assert.Equal(tenant.BaseUrl + "/Subscription/" + SubscriptionId, subscriptionReference);
        Assert.Equal(SubscriptionTopicUrl, topic);
        Assert.Equal("active", state);
        Assert.Equal(notificationType, type);
        Assert.Equal(total.ToString(CultureInfo.InvariantCulture), count);
        Assert.Equal(events.Count, notificationEvents.Length);
        for (int index = 0; index < events.Count; index++)
        {
            NotificationEventContract expected = events[index];
            string notificationEvent = notificationEvents[index];
            string eventNumber;
            string timestamp;
            string? focus;
            string[] context;
            if (backport)
            {
                string[] parts = ExchangeRepeated(notificationEvent, format, "part");
                Assert.Equal(contentLevel == "empty"
                    ? new[] { "event-number", "timestamp" }
                    : new[] { "event-number", "timestamp", "focus", "additional-context" },
                    parts.Select(part => ExchangePrimitive(part, format, "name")));
                eventNumber = ExchangePrimitive(parts[0], format, "valueString");
                timestamp = ExchangePrimitive(parts[1], format, "valueInstant");
                focus = contentLevel == "empty" ? null
                    : ExchangePrimitive(ExchangeChild(parts[2], format, "valueReference"), format, "reference");
                context = parts.Skip(3).Select(part =>
                    ExchangePrimitive(ExchangeChild(part, format, "valueReference"), format, "reference")).ToArray();
            }
            else
            {
                AssertExchangeMembers(notificationEvent, format, contentLevel == "empty"
                    ? ["eventNumber", "timestamp"] : ["eventNumber", "timestamp", "focus", "additionalContext"]);
                eventNumber = ExchangePrimitive(notificationEvent, format, "eventNumber");
                timestamp = ExchangePrimitive(notificationEvent, format, "timestamp");
                focus = contentLevel == "empty" ? null : ExchangePrimitive(ExchangeChild(notificationEvent, format, "focus"), format, "reference");
                context = contentLevel == "empty" ? [] : ExchangeRepeated(notificationEvent, format, "additionalContext")
                    .Select(reference => ExchangePrimitive(reference, format, "reference")).ToArray();
            }
            Assert.Equal(expected.Number.ToString(CultureInfo.InvariantCulture), eventNumber);
            Assert.Equal(expected.Timestamp, DateTimeOffset.Parse(timestamp, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));
            Assert.Equal(contentLevel == "empty" ? null : tenant.BaseUrl + "/" + expected.FocusIdentity, focus);
            string[] expectedContext = contentLevel == "empty" ? [] : [tenant.BaseUrl + "/Patient/sub-patient"];
            Assert.Equal(expectedContext, context);
        }
    }

    private static async Task AssertSubscriptionStatusOperations(CompatibilityReplay replay, SubscriptionGraph graph, long total)
    {
        string parameters = graph.Format == CompatibilityFormat.Json
            ? $$$"""{"resourceType":"Parameters","parameter":[{"name":"id","valueString":"{{{SubscriptionId}}}"},{"name":"status","valueCode":"active"}]}"""
            : $"""<Parameters xmlns="http://hl7.org/fhir"><parameter><name value="id"/><valueString value="{SubscriptionId}"/></parameter><parameter><name value="status"/><valueCode value="active"/></parameter></Parameters>""";
        foreach (string method in new[] { "GET", "POST" })
        {
            foreach (string path in new[] { "Subscription/$status", $"Subscription/{SubscriptionId}/$status" })
            {
                string name = "status-" + method + "-" + path;
                CompatibilityResponse response = await replay.SendAsync(new(graph.Tenant.Name, method, path)
                {
                    Format = graph.Format,
                    Content = method == "POST" ? parameters : null,
                });
                AssertExchangeContainer(replay, graph.Tenant, graph.Format, name, response, "Bundle", string.Empty);
                string bundle = ExchangeResource(response);
                Assert.Equal("searchset", ExchangePrimitive(bundle, graph.Format, "type"));
                string entry = Assert.Single(ExchangeRepeated(bundle, graph.Format, "entry"));
                string status = ExchangeNestedResource(entry, graph.Format, "resource");
                AssertSubscriptionStatus(graph.Tenant, graph.Format, status, "query-status", "empty", [], total);
                Assert.Equal("urn:uuid:" + ExchangePrimitive(status, graph.Format, "id"), ExchangePrimitive(entry, graph.Format, "fullUrl"));
                Assert.Equal("match", ExchangePrimitive(ExchangeChild(entry, graph.Format, "search"), graph.Format, "mode"));
            }
        }
        CompatibilityResponse off = await replay.SendAsync(new(graph.Tenant.Name, "GET", "Subscription/$status?status=off") { Format = graph.Format });
        AssertExchangeContainer(replay, graph.Tenant, graph.Format, "status-negative-filter", off, "Bundle", string.Empty);
        Assert.False(ExchangeHasMember(ExchangeResource(off), graph.Format, "entry"));
    }

    private static async Task AssertSubscriptionEventsOperations(
        CompatibilityReplay replay, SubscriptionGraph graph, string contentLevel, NotificationEventContract second)
    {
        string integerChoice = graph.Tenant.Version == FhirReleases.FhirSequenceCodes.R5 ? "valueInteger64" : "valueString";
        string parameters = graph.Format == CompatibilityFormat.Json
            ? $$$"""{"resourceType":"Parameters","parameter":[{"name":"eventsSinceNumber","{{{integerChoice}}}":"2"},{"name":"eventsUntilNumber","{{{integerChoice}}}":"2"},{"name":"content","valueCode":"{{{contentLevel}}}"}]}"""
            : $"""<Parameters xmlns="http://hl7.org/fhir"><parameter><name value="eventsSinceNumber"/><{integerChoice} value="2"/></parameter><parameter><name value="eventsUntilNumber"/><{integerChoice} value="2"/></parameter><parameter><name value="content"/><valueCode value="{contentLevel}"/></parameter></Parameters>""";
        foreach (string method in new[] { "GET", "POST" })
        {
            string path = $"Subscription/{SubscriptionId}/$events";
            if (method == "GET")
            {
                path += $"?events-since-number=2&events-until-number=2&content={contentLevel}";
            }
            CompatibilityResponse response = await replay.SendAsync(new(graph.Tenant.Name, method, path)
            {
                Format = graph.Format,
                Content = method == "POST" ? parameters : null,
            });
            string bundle = ExchangeResource(response);
            AssertExchangeContainer(replay, graph.Tenant, graph.Format, "events-" + method, response, "Bundle",
                ExchangePrimitive(bundle, graph.Format, "id"));
            AssertSubscriptionBundle(replay, graph, "events-" + method, bundle, "query-event", contentLevel, [second], 2);
        }
    }

    private static async Task AssertSyntheticInboundNotification(
        CompatibilityReplay replay,
        SubscriptionGraph graph,
        CompatibilityTenant receiver,
        SubscriptionCapture senderCapture,
        SubscriptionCapture receiverCapture)
    {
        const string bundleId = "inbound-synthetic";
        const string statusId = "22222222-2222-4222-8222-222222222222";
        string status = SyntheticSubscriptionStatus(graph.Tenant, graph.Format, statusId);
        string bundleType = graph.Tenant.Version == FhirReleases.FhirSequenceCodes.R5 ? "subscription-notification" : "history";
        string content = graph.Format == CompatibilityFormat.Json
            ? $$$"""{"resourceType":"Bundle","id":"{{{bundleId}}}","type":"{{{bundleType}}}","timestamp":"2025-01-02T03:04:05Z","entry":[{"fullUrl":"urn:uuid:{{{statusId}}}","resource":{{{status}}}}]}"""
            : $"""<Bundle xmlns="http://hl7.org/fhir"><id value="{bundleId}"/><type value="{bundleType}"/><timestamp value="2025-01-02T03:04:05Z"/><entry><fullUrl value="urn:uuid:{statusId}"/><resource>{status}</resource></entry></Bundle>""";
        CompatibilityResponse response = await replay.SendAsync(new(receiver.Name, "POST", "$subscription-hook")
        {
            Format = graph.Format,
            Content = content,
            Headers = new Dictionary<string, string> { ["Prefer"] = "return=OperationOutcome" },
        });
        WireContractAssertions.Status(HttpStatusCode.OK, response.Status);
        Assert.Equal(WireContractAssertions.MediaType(graph.Format), response.MediaType);
        WireContractAssertions.Body(new OutcomeBodyExpectation("received-outcome",
            [new("information", "success", "Subscription Notification Received")]),
            OperationOutcomeBody(response), graph.Format, receiver.BaseUrl, replay.Bindings);
        GeneratedResourceExpectation bundleBinding = new("received-bundle", "Bundle", "1", bundleId);
        CompatibilityResponse read = await replay.SendAsync(new(receiver.Name, "GET", "Bundle/" + bundleId) { Format = graph.Format });
        WireContractAssertions.Response(ResourceResponse("read-received", content, bundleBinding, HttpStatusCode.OK),
            read, graph.Format, receiver.BaseUrl, replay.Bindings);
        if (response.Direct is CompatibilityDirectResponse direct)
        {
            Assert.True(direct.Success);
            Assert.Equal("Bundle", direct.ResourceType);
            Assert.Equal(bundleId, direct.Id);
            Assert.Empty(direct.ETag);
            Assert.Empty(direct.Location);
            Assert.Empty(direct.LastModified);
            // DoSystemOperation preserves the body resource separately from the operation's outcome.
            WireContractAssertions.Content(ExchangeResource(read), direct.SerializedResource, graph.Format);
        }
        Assert.Empty(senderCapture.Received);
        Assert.Empty(senderCapture.Store.ReceivedNotifications);
        (object? eventSender, ReceivedSubscriptionEventArgs args) = Assert.Single(receiverCapture.Received);
        Assert.Same(receiverCapture.Store, eventSender);
        Assert.Equal(receiver.Name, args.Tenant.ControllerName);
        Assert.Equal(bundleId, args.BundleId);
        KeyValuePair<string, IReadOnlyList<ParsedSubscriptionStatus>> received = Assert.Single(receiverCapture.Store.ReceivedNotifications);
        Assert.Equal(graph.Tenant.BaseUrl + "/Subscription/" + SubscriptionId, received.Key);
        ParsedSubscriptionStatus parsed = Assert.Single(received.Value);
        Assert.Same(parsed, args.Status);
        Assert.Equal(bundleId, parsed.BundleId);
        Assert.Equal(received.Key, parsed.SubscriptionReference);
        Assert.Equal(SubscriptionTopicUrl, parsed.SubscriptionTopicCanonical);
        Assert.InRange(parsed.ProcessedDateTime, replay.Bindings.StartedAt, DateTimeOffset.UtcNow.AddSeconds(5));
        Assert.Equal("active", parsed.Status);
        Assert.Equal(ParsedSubscription.NotificationTypeCodes.EventNotification, parsed.NotificationType);
        Assert.Equal(11, parsed.EventsSinceSubscriptionStart);
        Assert.Empty(parsed.Errors);
        ParsedSubscriptionStatus.ParsedNotificationEvent notificationEvent = Assert.Single(parsed.NotificationEvents);
        Assert.Equal(11, notificationEvent.EventNumber);
        Assert.Equal(DateTimeOffset.Parse("2025-01-02T03:04:05Z", CultureInfo.InvariantCulture), notificationEvent.Timestamp);
        Assert.Equal(graph.Tenant.BaseUrl + "/Encounter/inbound-focus", notificationEvent.FocusReference);
        Assert.Equal(new[] { graph.Tenant.BaseUrl + "/Patient/sub-patient" }, notificationEvent.AdditionalContextReferences);
    }

    private static string SyntheticSubscriptionStatus(CompatibilityTenant tenant, CompatibilityFormat format, string id)
    {
        // These inbound literals are authored independently of outbound converter output.
        string subscription = tenant.BaseUrl + "/Subscription/" + SubscriptionId;
        string focus = tenant.BaseUrl + "/Encounter/inbound-focus";
        string context = tenant.BaseUrl + "/Patient/sub-patient";
        if (tenant.Version == FhirReleases.FhirSequenceCodes.R4)
        {
            return format == CompatibilityFormat.Json
                ? $$$"""
                    {"resourceType":"Parameters","id":"{{{id}}}","parameter":[
                    {"name":"subscription","valueReference":{"reference":"{{{subscription}}}"}},
                    {"name":"topic","valueCanonical":"{{{SubscriptionTopicUrl}}}"},
                    {"name":"status","valueCode":"active"},{"name":"type","valueCode":"event-notification"},
                    {"name":"events-since-subscription-start","valueString":"11"},
                    {"name":"notification-event","part":[{"name":"event-number","valueString":"11"},
                    {"name":"timestamp","valueInstant":"2025-01-02T03:04:05Z"},
                    {"name":"focus","valueReference":{"reference":"{{{focus}}}"}},
                    {"name":"additional-context","valueReference":{"reference":"{{{context}}}"}}]}]}
                    """
                : $"""
                    <Parameters xmlns="http://hl7.org/fhir"><id value="{id}"/>
                    <parameter><name value="subscription"/><valueReference><reference value="{subscription}"/></valueReference></parameter>
                    <parameter><name value="topic"/><valueCanonical value="{SubscriptionTopicUrl}"/></parameter>
                    <parameter><name value="status"/><valueCode value="active"/></parameter>
                    <parameter><name value="type"/><valueCode value="event-notification"/></parameter>
                    <parameter><name value="events-since-subscription-start"/><valueString value="11"/></parameter>
                    <parameter><name value="notification-event"/><part><name value="event-number"/><valueString value="11"/></part>
                    <part><name value="timestamp"/><valueInstant value="2025-01-02T03:04:05Z"/></part>
                    <part><name value="focus"/><valueReference><reference value="{focus}"/></valueReference></part>
                    <part><name value="additional-context"/><valueReference><reference value="{context}"/></valueReference></part></parameter></Parameters>
                    """;
        }
        return format == CompatibilityFormat.Json
            ? $$$"""
                {"resourceType":"SubscriptionStatus","id":"{{{id}}}","status":"active","type":"event-notification",
                "eventsSinceSubscriptionStart":"11","notificationEvent":[{"eventNumber":"11","timestamp":"2025-01-02T03:04:05Z",
                "focus":{"reference":"{{{focus}}}"},"additionalContext":[{"reference":"{{{context}}}"}]}],
                "subscription":{"reference":"{{{subscription}}}"},"topic":"{{{SubscriptionTopicUrl}}}"}
                """
            : $"""
                <SubscriptionStatus xmlns="http://hl7.org/fhir"><id value="{id}"/><status value="active"/><type value="event-notification"/>
                <eventsSinceSubscriptionStart value="11"/><notificationEvent><eventNumber value="11"/><timestamp value="2025-01-02T03:04:05Z"/>
                <focus><reference value="{focus}"/></focus><additionalContext><reference value="{context}"/></additionalContext></notificationEvent>
                <subscription><reference value="{subscription}"/></subscription><topic value="{SubscriptionTopicUrl}"/></SubscriptionStatus>
                """;
    }

    private static string ReplaceNotificationCount(string status, FhirReleases.FhirSequenceCodes version, CompatibilityFormat format, bool eventNumber)
    {
        if (format == CompatibilityFormat.Json)
        {
            JsonObject root = JsonNode.Parse(status)!.AsObject();
            if (version == FhirReleases.FhirSequenceCodes.R4)
            {
                JsonObject parameter = root["parameter"]!.AsArray().Select(node => node!.AsObject())
                    .Single(parameter => parameter["name"]!.GetValue<string>() == (eventNumber ? "notification-event" : "events-since-subscription-start"));
                if (eventNumber)
                {
                    parameter = parameter["part"]!.AsArray().Select(node => node!.AsObject())
                        .Single(part => part["name"]!.GetValue<string>() == "event-number");
                }
                parameter["valueString"] = "12";
            }
            else if (eventNumber)
            {
                root["notificationEvent"]![0]!["eventNumber"] = "12";
            }
            else
            {
                root["eventsSinceSubscriptionStart"] = "12";
            }
            return root.ToJsonString();
        }
        XElement resource = ExchangeXml(status);
        XElement target;
        if (version == FhirReleases.FhirSequenceCodes.R4)
        {
            XElement parameter = resource.Descendants().Single(element =>
                (element.Name.LocalName is "part" or "parameter") &&
                (string?)element.Element(_exchangeFhir + "name")?.Attribute("value") ==
                (eventNumber ? "event-number" : "events-since-subscription-start"));
            target = parameter.Element(_exchangeFhir + "valueString")!;
        }
        else
        {
            target = Assert.Single(resource.Descendants(_exchangeFhir + (eventNumber ? "eventNumber" : "eventsSinceSubscriptionStart")));
        }
        target.SetAttributeValue("value", "12");
        return resource.ToString(SaveOptions.DisableFormatting);
    }
}
