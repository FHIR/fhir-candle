// <copyright file="Sdk6ContractTests.Subscriptions.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using fhir.candle.Tests.Extensions;
using fhir.candle.Tests.Models;
using FhirCandle.Models;
using FhirCandle.Storage;
using FhirCandle.Utils;

namespace fhir.candle.Tests;

public partial class Sdk6ContractTests
{
    private const string SubscriptionEvidence =
        "R4TestSubscriptions/R4BTestSubscriptions/R5TestSubscriptions ParseTopic/ParseSubscription/ParseHandshake/" +
        "TestSubEncounterNoFilters; FhirStoreTestsR5.SubscriptionTriggeredCreate/SubscriptionTriggeredUpdateFhirpath/" +
        "SubscriptionTriggeredUpdateQuery. Synthetic local-only copies, independent JSON/XML literals.";
    private const string SubscriptionTopicUrl = "http://localhost/SubscriptionTopic/sdk6-encounter";
    private const string SubscriptionId = "local-subscription";
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
        KeyValuePair<string, List<ParsedSubscriptionStatus>> received = Assert.Single(receiverCapture.Store.ReceivedNotifications);
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
