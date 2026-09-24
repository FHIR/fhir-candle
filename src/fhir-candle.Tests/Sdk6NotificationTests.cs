// <copyright file="Sdk6NotificationTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using fhir.candle.Services;
using fhir.candle.Tests.Controllers;
using fhir.candle.Tests.Extensions;
using fhir.candle.Tests.Models;
using FhirCandle.Configuration;
using FhirCandle.Models;
using FhirCandle.Storage;
using FhirCandle.Utils;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;

namespace fhir.candle.Tests;

/// <summary>Real store events drive the existing notification service and an independently inspected loopback receiver.</summary>
[Collection(CompatibilityHostCollection.Name)]
public sealed class Sdk6NotificationTests
{
    private const string SubscriptionId = "local-subscription";
    private const string TopicUrl = "http://localhost/SubscriptionTopic/sdk6-encounter";
    private const string BackportRoot = "http://hl7.org/fhir/uv/subscriptions-backport/StructureDefinition/";
    private static readonly XNamespace _fhir = "http://hl7.org/fhir";
    private static readonly FhirReleases.FhirSequenceCodes[] _versions =
        [FhirReleases.FhirSequenceCodes.R4, FhirReleases.FhirSequenceCodes.R4B, FhirReleases.FhirSequenceCodes.R5];
    private readonly ITestOutputHelper _output;

    /// <summary>Creates a notification test with version and captured-transport diagnostics.</summary>
    /// <param name="output">The current xUnit test output.</param>
    public Sdk6NotificationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>Every supported version emits both wire formats at every content level.</summary>
    public static IEnumerable<object[]> DeliveryCases
    {
        get
        {
            foreach (FhirReleases.FhirSequenceCodes version in _versions)
            {
                foreach (CompatibilityFormat format in new[] { CompatibilityFormat.Json, CompatibilityFormat.Xml })
                {
                    foreach (string content in new[] { "empty", "id-only", "full-resource" })
                    {
                        yield return [$"rest-hook / {version} / POST / {format} / {content}", version, format, content];
                    }
                }
            }
        }
    }

    /// <summary>Graceful drain, canceled stop, and concurrent disposal each start with two held, real outbound requests.</summary>
    public static IEnumerable<object[]> ShutdownCases
    {
        get
        {
            foreach (FhirReleases.FhirSequenceCodes version in _versions)
            {
                foreach (CompatibilityFormat format in new[] { CompatibilityFormat.Json, CompatibilityFormat.Xml })
                {
                    foreach (string mode in new[] { "drain", "cancel", "dispose" })
                    {
                        yield return [$"notification-stop / {version} / POST / {format} / {mode}", version, format, mode];
                    }
                }
            }
        }
    }

    /// <summary>Protects the nonempty version/format/content/lifecycle matrix.</summary>
    [Fact]
    public void NotificationIntegrationInventoryIsNonempty()
    {
        Assert.Equal(18, DeliveryCases.Count());
        Assert.Equal(18, ShutdownCases.Count());
        Assert.Equal(18, DeliveryCases.Select(row => row[0]).Distinct().Count());
        Assert.Equal(18, ShutdownCases.Select(row => row[0]).Distinct().Count());
        Assert.All(DeliveryCases.Concat(ShutdownCases), row => Assert.False(string.IsNullOrEmpty(Assert.IsType<string>(row[0]))));
        foreach (FhirReleases.FhirSequenceCodes version in _versions)
        {
            Assert.Equal(6, DeliveryCases.Count(row => (FhirReleases.FhirSequenceCodes)row[1] == version));
            Assert.Equal(6, ShutdownCases.Count(row => (FhirReleases.FhirSequenceCodes)row[1] == version));
        }
        _output.WriteLine("Notification inventory: R4 Parameters; R4B SubscriptionStatus/history; R5 SubscriptionStatus/subscription-notification; JSON/XML; empty/id-only/full-resource; drain/cancel/dispose.");
    }

    /// <summary>Payload content, version-specific status shape, event relationships, and same-version sibling isolation cross real TCP.</summary>
    [Theory]
    [MemberData(nameof(DeliveryCases))]
    public async Task RestHookNotificationDeliversVersionCorrectWireBody(
        string label, FhirReleases.FhirSequenceCodes version, CompatibilityFormat format, string contentLevel)
    {
        await using LocalHttpFixture receiver = await LocalHttpFixture.StartAsync();
        await using NotificationHost host = new(version);
        _output.WriteLine($"{label}; owned temporary root: {host.Files.Root}");
        SubscriptionGraph sender = SeedGraph(host, host.Primary, receiver.Address, format, contentLevel);
        SubscriptionGraph sibling = SeedGraph(host, host.Sibling, receiver.Address, format, contentLevel);
        LocalHttpFixture.ExpectedRequest delivery = receiver.Expect("POST", "/notify/" + host.Primary,
            LocalHttpFixture.RawResponse.Empty(HttpStatusCode.Accepted));
        await host.StartAsync();

        // A real but filtered-out store event precedes the matching event; neither it nor the sibling can pass as the payload.
        WriteEncounter(host, sender, "filtered-event", otherPatient: true);
        Assert.Equal(0, sender.Subscription.CurrentEventCount);
        WriteEncounter(host, sender, "delivered-event");
        Assert.Equal(1, sender.Subscription.CurrentEventCount);
        Assert.Equal(0, sibling.Subscription.CurrentEventCount);
        LocalHttpFixture.CapturedRequest request = await delivery.WaitForRequestAsync();
        Assert.False(await delivery.WaitForCompletionAsync());

        await host.Service.StopAsync(CancellationToken.None).WaitAsync(LocalHttpFixture.Deadline);
        AssertNotification(host, sender, request, format, contentLevel, "delivered-event");
        Assert.Equal(new long[] { 1 }, sender.Subscription.SerializedEvents.Keys);
        WireContractAssertions.Content(request.Text, sender.Subscription.SerializedEvents[1], format);
        Assert.Empty(sender.Subscription.NotificationErrors);
        Assert.Empty(sibling.Subscription.GeneratedEvents);
        Assert.Empty(sibling.Subscription.SerializedEvents);
        Assert.Empty(sibling.Subscription.NotificationErrors);
        Assert.Empty(sender.Store.ReceivedNotifications);
        Assert.Empty(sibling.Store.ReceivedNotifications);
        Assert.DoesNotContain(host.Log.Entries, entry => entry.Level >= LogLevel.Warning);
        Assert.Single(receiver.Requests);
        receiver.AssertComplete();
    }

    /// <summary>Stop must join all in-flight work; cancellation is observed, and disposal cannot abandon timers or sends.</summary>
    [Theory]
    [MemberData(nameof(ShutdownCases))]
    public async Task NotificationManagerStopsDeliveryBeforeFixtureTeardown(
        string label, FhirReleases.FhirSequenceCodes version, CompatibilityFormat format, string mode)
    {
        await using LocalHttpFixture receiver = await LocalHttpFixture.StartAsync();
        await using NotificationHost host = new(version);
        _output.WriteLine($"{label}; owned temporary root: {host.Files.Root}");
        SubscriptionGraph sender = SeedGraph(host, host.Primary, receiver.Address, format, "full-resource");
        SubscriptionGraph sibling = SeedGraph(host, host.Sibling, receiver.Address, format, "full-resource");
        TaskCompletionSource releaseFirst = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource releaseSecond = new(TaskCreationOptions.RunContinuationsAsynchronously);
        LocalHttpFixture.ExpectedRequest first = HeldDelivery(receiver, host.Primary, releaseFirst);
        LocalHttpFixture.ExpectedRequest second = HeldDelivery(receiver, host.Sibling, releaseSecond);
        using CancellationTokenSource stopCancellation = new();
        await host.StartAsync();

        try
        {
            WriteEncounter(host, sender, "active-first");
            LocalHttpFixture.CapturedRequest firstRequest = await first.WaitForRequestAsync();
            WriteEncounter(host, sibling, "active-second");
            LocalHttpFixture.CapturedRequest secondRequest = await second.WaitForRequestAsync();
            AssertNotification(host, sender, firstRequest, format, "full-resource", "active-first");
            AssertNotification(host, sibling, secondRequest, format, "full-resource", "active-second");
            Assert.Empty(sender.Subscription.SerializedEvents);
            Assert.Empty(sibling.Subscription.SerializedEvents);

            Task firstStop = host.Service.StopAsync(stopCancellation.Token);
            Task repeatedStop = host.Service.StopAsync(CancellationToken.None);
            Assert.False(firstStop.IsCompleted, "Stop returned while a real HTTP send was still held by the receiver.");
            Assert.False(repeatedStop.IsCompleted, "A concurrent Stop call must join the same unfinished shutdown.");

            // This is an actual new event after shutdown began, not a manually invoked private callback.
            WriteEncounter(host, sender, "after-stop");
            Assert.Equal(2, sender.Subscription.CurrentEventCount);

            if (mode == "drain")
            {
                releaseFirst.TrySetResult();
                Assert.False(await first.WaitForCompletionAsync());
                Assert.False(firstStop.IsCompleted, "Releasing one delivery must not forget the other in-flight send.");
                Assert.False(repeatedStop.IsCompleted);
                releaseSecond.TrySetResult();
                Assert.False(await second.WaitForCompletionAsync());
                await Task.WhenAll(firstStop, repeatedStop).WaitAsync(LocalHttpFixture.Deadline);
                Assert.Equal(new long[] { 1 }, sender.Subscription.SerializedEvents.Keys);
                Assert.Equal(new long[] { 1 }, sibling.Subscription.SerializedEvents.Keys);
                Assert.DoesNotContain(host.Log.Entries, entry => entry.Message.Contains("canceled during shutdown", StringComparison.Ordinal));
            }
            else if (mode == "cancel")
            {
                stopCancellation.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => firstStop.WaitAsync(LocalHttpFixture.Deadline));
                // A canceled wait is not a successful drain. Join the same work before destroying any fixture.
                await repeatedStop.WaitAsync(LocalHttpFixture.Deadline);
                Assert.True(await first.WaitForCompletionAsync());
                Assert.True(await second.WaitForCompletionAsync());
                Assert.False(releaseFirst.Task.IsCompleted);
                Assert.False(releaseSecond.Task.IsCompleted);
                AssertCanceledDeliveries(host, sender, sibling);
            }
            else
            {
                Assert.Equal("dispose", mode);
                Task firstDispose = Task.Run(host.Service.Dispose);
                Task repeatedDispose = Task.Run(host.Service.Dispose);
                await Task.WhenAll(firstDispose, repeatedDispose, firstStop, repeatedStop).WaitAsync(LocalHttpFixture.Deadline);
                Assert.True(await first.WaitForCompletionAsync());
                Assert.True(await second.WaitForCompletionAsync());
                Assert.False(releaseFirst.Task.IsCompleted);
                Assert.False(releaseSecond.Task.IsCompleted);
                AssertCanceledDeliveries(host, sender, sibling);
            }

            await host.Service.StopAsync(CancellationToken.None).WaitAsync(LocalHttpFixture.Deadline);
            host.Service.Dispose();
            host.Service.Dispose();
            receiver.AssertComplete();
            await receiver.DisposeAsync();

            // Keep the stores alive briefly in program order, but close the receiver first. Detached event handlers
            // and the completed timer/send drain, not a sleep interval, establish the teardown barrier.
            WriteEncounter(host, sender, "after-receiver-disposal");
            Assert.Equal(3, sender.Subscription.CurrentEventCount);
            await host.Service.StopAsync(CancellationToken.None).WaitAsync(LocalHttpFixture.Deadline);
            host.Service.Dispose();
            Assert.False(sender.Subscription.SerializedEvents.ContainsKey(2));
            Assert.False(sender.Subscription.SerializedEvents.ContainsKey(3));
            Assert.Equal(2, receiver.Requests.Count);
            Assert.Empty(sender.Subscription.NotificationErrors);
            Assert.Empty(sibling.Subscription.NotificationErrors);
            Assert.DoesNotContain(host.Log.Entries, entry => entry.Level >= LogLevel.Warning);
        }
        finally
        {
            releaseFirst.TrySetResult();
            releaseSecond.TrySetResult();
            stopCancellation.Cancel();
        }
    }

    private static LocalHttpFixture.ExpectedRequest HeldDelivery(LocalHttpFixture receiver, string tenant, TaskCompletionSource release) =>
        receiver.Expect("POST", "/notify/" + tenant,
            async (_, cancellationToken) =>
            {
                await release.Task.WaitAsync(cancellationToken);
                return LocalHttpFixture.RawResponse.Empty();
            }, allowClientCancellation: true);

    private static void AssertCanceledDeliveries(NotificationHost host, SubscriptionGraph sender, SubscriptionGraph sibling)
    {
        Assert.Empty(sender.Subscription.SerializedEvents);
        Assert.Empty(sibling.Subscription.SerializedEvents);
        Assert.Equal(2, host.Log.Entries.Count(entry => entry.Message.Contains("canceled during shutdown", StringComparison.Ordinal)));
    }

    private static SubscriptionGraph SeedGraph(
        NotificationHost host, string tenant, Uri receiver, CompatibilityFormat format, string contentLevel)
    {
        IFhirStore store = host.Stores[tenant];
        FhirReleases.FhirSequenceCodes version = store.Config.FhirVersion;
        SubscriptionGraph graph = new(store);
        using JsonDocument fixtures = JsonDocument.Parse(CompatibilityReplay.LoadFixture(version, "subscription-resources.json"));
        WriteResource(host, graph, fixtures.RootElement.GetProperty("patient").GetRawText());
        WriteResource(host, graph, fixtures.RootElement.GetProperty("other-patient").GetRawText());
        WriteResource(host, graph, fixtures.RootElement.GetProperty("topic-fhirpath").GetRawText());
        JsonObject subscription = JsonNode.Parse(fixtures.RootElement.GetProperty("subscription").GetRawText())!.AsObject();
        string endpoint = new Uri(receiver, "notify/" + tenant).AbsoluteUri;
        if (version == FhirReleases.FhirSequenceCodes.R5)
        {
            subscription["endpoint"] = endpoint;
            subscription["contentType"] = WireContractAssertions.MediaType(format);
            subscription["content"] = contentLevel;
            subscription["heartbeatPeriod"] = 0;
            subscription["parameter"] = new JsonArray(
                new JsonObject { ["name"] = "X-Synthetic", ["value"] = "phase6" },
                new JsonObject { ["name"] = "X-Sdk6-Tenant", ["value"] = tenant });
        }
        else
        {
            JsonObject channel = subscription["channel"]!.AsObject();
            channel["endpoint"] = endpoint;
            channel["payload"] = WireContractAssertions.MediaType(format);
            channel["_payload"]!["extension"]![0]!["valueCode"] = contentLevel;
            foreach (JsonNode? extension in channel["extension"]!.AsArray())
            {
                JsonObject channelExtension = Assert.IsType<JsonObject>(extension);
                if (channelExtension["url"]!.GetValue<string>() == BackportRoot + "backport-heartbeat-period")
                {
                    channelExtension["valueInteger"] = 0;
                }
            }
            channel["header"] = new JsonArray("X-Synthetic: phase6", "X-Sdk6-Tenant: " + tenant);
        }
        WriteResource(host, graph, subscription.ToJsonString());
        graph.Subscription = Assert.Single(store.CurrentSubscriptions);
        Assert.Equal("active", graph.Subscription.CurrentStatus);
        Assert.Equal("rest-hook", graph.Subscription.ChannelCode);
        Assert.Equal(endpoint, graph.Subscription.Endpoint);
        Assert.True(new Uri(graph.Subscription.Endpoint).IsLoopback);
        Assert.Equal(contentLevel, graph.Subscription.ContentLevel);
        Assert.Equal(WireContractAssertions.MediaType(format), graph.Subscription.ContentType);
        Assert.Equal(0, graph.Subscription.HeartbeatSeconds);
        Assert.Equal(TopicUrl, Assert.Single(store.CurrentTopics).Url);
        Assert.Empty(graph.Subscription.GeneratedEvents);
        Assert.Empty(graph.Subscription.SerializedEvents);
        return graph;
    }

    private static void WriteEncounter(NotificationHost host, SubscriptionGraph graph, string id, bool otherPatient = false)
    {
        using JsonDocument fixtures = JsonDocument.Parse(CompatibilityReplay.LoadFixture(graph.Store.Config.FhirVersion, "subscription-resources.json"));
        string encounter = LocalPackageFixture.WithId(fixtures.RootElement.GetProperty("complete").GetRawText(), id);
        if (otherPatient)
        {
            encounter = encounter.Replace("Patient/sub-patient", "Patient/other-patient", StringComparison.Ordinal);
        }
        WriteResource(host, graph, encounter);
    }

    private static void WriteResource(NotificationHost host, SubscriptionGraph graph, string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        string type = document.RootElement.GetProperty("resourceType").GetString()!;
        string id = document.RootElement.GetProperty("id").GetString()!;
        string identity = type + "/" + id;
        FhirRequestContext request = new(graph.Store, "PUT", identity)
        {
            SourceContent = json,
            SourceFormat = "application/fhir+json",
            DestinationFormat = "application/fhir+json",
        };
        Assert.True(graph.Store.PerformInteraction(request, out FhirResponseContext response, serializeReturn: true));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        GeneratedResourceExpectation binding = new(graph.Store.Config.ControllerName + "/" + identity, type, "1", id);
        WireContractAssertions.Body(new ResourceBodyExpectation(json, binding), response.SerializedResource, CompatibilityFormat.Json,
            graph.Store.Config.BaseUrl, host.Bindings);
        graph.Json.Add(identity, json);
        graph.Bindings.Add(identity, binding with { Mode = CompatibilityBindingMode.Reuse });
    }

    private void AssertNotification(
        NotificationHost host, SubscriptionGraph graph, LocalHttpFixture.CapturedRequest request,
        CompatibilityFormat format, string contentLevel, string encounterId)
    {
        Assert.Equal("POST", request.Method);
        Assert.Equal("/notify/" + graph.Store.Config.ControllerName, request.Path);
        Assert.Empty(request.Query);
        Assert.Equal("phase6", request.Header("X-Synthetic"));
        Assert.Equal(graph.Store.Config.ControllerName, request.Header("X-Sdk6-Tenant"));
        MediaTypeHeaderValue mediaType = MediaTypeHeaderValue.Parse(request.Header("Content-Type"));
        Assert.Equal(WireContractAssertions.MediaType(format), mediaType.MediaType);
        Assert.Equal("utf-8", mediaType.CharSet);
        SubscriptionEvent generated = graph.Subscription.GeneratedEvents[1];
        Assert.Equal(1, generated.EventNumber);
        Assert.Equal(SubscriptionId, generated.SubscriptionId);
        Assert.Equal(TopicUrl, generated.TopicUrl);
        Assert.True(graph.Store.TryGetResourceInfo(generated.Focus, out string focusType, out string focusId));
        Assert.Equal("Encounter/" + encounterId, focusType + "/" + focusId);
        object context = Assert.Single(generated.AdditionalContext!);
        Assert.True(graph.Store.TryGetResourceInfo(context, out string contextType, out string contextId));
        Assert.Equal("Patient/sub-patient", contextType + "/" + contextId);

        int count = contentLevel == "full-resource" ? 3 : 1;
        if (format == CompatibilityFormat.Json)
        {
            using JsonDocument document = JsonDocument.Parse(request.Text);
            JsonElement bundle = document.RootElement;
            Assert.Equal(new[] { "entry", "id", "resourceType", "timestamp", "type" },
                bundle.EnumerateObject().Select(property => property.Name).OrderBy(name => name, StringComparer.Ordinal));
            Assert.Equal("Bundle", bundle.GetProperty("resourceType").GetString());
            AssertEnvelope(host, graph, bundle.GetProperty("id").GetString()!, bundle.GetProperty("timestamp").GetString()!,
                bundle.GetProperty("type").GetString()!);
            JsonElement[] entries = bundle.GetProperty("entry").EnumerateArray().ToArray();
            Assert.Equal(count, entries.Length);
            JsonElement status = entries[0].GetProperty("resource");
            string statusId = status.GetProperty("id").GetString()!;
            Assert.True(Guid.TryParse(statusId, out _));
            Assert.NotEqual(bundle.GetProperty("id").GetString(), statusId);
            Assert.Equal("urn:uuid:" + statusId, entries[0].GetProperty("fullUrl").GetString());
            string timestamp = graph.Store.Config.FhirVersion == FhirReleases.FhirSequenceCodes.R4
                ? status.GetProperty("parameter")[5].GetProperty("part")[1].GetProperty("valueInstant").GetString()!
                : status.GetProperty("notificationEvent")[0].GetProperty("timestamp").GetString()!;
            Assert.Equal(generated.Timestamp, DateTimeOffset.Parse(timestamp, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));
            WireContractAssertions.Content(StatusJson(graph, contentLevel, encounterId, statusId, timestamp), status.GetRawText(), format);
            if (contentLevel == "full-resource")
            {
                AssertJsonResource(entries[1], "Encounter/" + encounterId);
                AssertJsonResource(entries[2], "Patient/sub-patient");
            }

            void AssertJsonResource(JsonElement entry, string identity)
            {
                Assert.Equal(new[] { "fullUrl", "resource" }, entry.EnumerateObject().Select(property => property.Name).OrderBy(name => name, StringComparer.Ordinal));
                Assert.Equal(graph.Store.Config.BaseUrl + "/" + identity, entry.GetProperty("fullUrl").GetString());
                WireContractAssertions.Body(new ResourceBodyExpectation(graph.Json[identity], graph.Bindings[identity]),
                    entry.GetProperty("resource").GetRawText(), format, graph.Store.Config.BaseUrl, host.Bindings);
            }
        }
        else
        {
            XElement bundle = XElement.Parse(request.Text, LoadOptions.PreserveWhitespace);
            Assert.Equal(_fhir + "Bundle", bundle.Name);
            Assert.Equal(new[] { "id", "type", "timestamp" }.Concat(Enumerable.Repeat("entry", count)), bundle.Elements().Select(element => element.Name.LocalName));
            AssertEnvelope(host, graph, Primitive(bundle, "id"), Primitive(bundle, "timestamp"), Primitive(bundle, "type"));
            XElement[] entries = bundle.Elements(_fhir + "entry").ToArray();
            Assert.Equal(count, entries.Length);
            XElement status = Assert.Single(entries[0].Element(_fhir + "resource")!.Elements());
            string statusId = Primitive(status, "id");
            Assert.True(Guid.TryParse(statusId, out _));
            Assert.NotEqual(Primitive(bundle, "id"), statusId);
            Assert.Equal("urn:uuid:" + statusId, Primitive(entries[0], "fullUrl"));
            string timestamp = graph.Store.Config.FhirVersion == FhirReleases.FhirSequenceCodes.R4
                ? Primitive(status.Elements(_fhir + "parameter").ElementAt(5).Elements(_fhir + "part").ElementAt(1), "valueInstant")
                : Primitive(status.Element(_fhir + "notificationEvent")!, "timestamp");
            Assert.Equal(generated.Timestamp, DateTimeOffset.Parse(timestamp, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));
            WireContractAssertions.Content(StatusXml(graph, contentLevel, encounterId, statusId, timestamp), status.ToString(SaveOptions.DisableFormatting), format);
            if (contentLevel == "full-resource")
            {
                AssertXmlResource(entries[1], "Encounter/" + encounterId, EncounterXml(graph.Store.Config.FhirVersion, encounterId));
                AssertXmlResource(entries[2], "Patient/sub-patient",
                    """<Patient xmlns="http://hl7.org/fhir"><id value="sub-patient"/><active value="true"/><name><family value="Synthetic Subscription"/><given value="Local"/></name></Patient>""");
            }

            void AssertXmlResource(XElement entry, string identity, string expected)
            {
                Assert.Equal(new[] { "fullUrl", "resource" }, entry.Elements().Select(element => element.Name.LocalName));
                Assert.Equal(graph.Store.Config.BaseUrl + "/" + identity, Primitive(entry, "fullUrl"));
                WireContractAssertions.Body(new ResourceBodyExpectation(expected, graph.Bindings[identity]),
                    Assert.Single(entry.Element(_fhir + "resource")!.Elements()).ToString(SaveOptions.DisableFormatting),
                    format, graph.Store.Config.BaseUrl, host.Bindings);
            }
        }
        _output.WriteLine($"Captured {graph.Store.Config.FhirVersion}/{format}/{contentLevel}: {request.Method} {request.RawTarget}; Subscription/{SubscriptionId}; event 1; focus {graph.Store.Config.BaseUrl}/Encounter/{encounterId}; bytes={request.Body.Length}");
    }

    private static void AssertEnvelope(NotificationHost host, SubscriptionGraph graph, string id, string timestamp, string type)
    {
        Assert.True(Guid.TryParse(id, out _));
        Assert.InRange(DateTimeOffset.Parse(timestamp, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            host.Bindings.StartedAt, DateTimeOffset.UtcNow.AddSeconds(5));
        Assert.Equal(graph.Store.Config.FhirVersion == FhirReleases.FhirSequenceCodes.R5 ? "subscription-notification" : "history", type);
    }

    private static string StatusJson(SubscriptionGraph graph, string contentLevel, string encounterId, string id, string timestamp)
    {
        string baseUrl = graph.Store.Config.BaseUrl;
        if (graph.Store.Config.FhirVersion == FhirReleases.FhirSequenceCodes.R4)
        {
            string references = contentLevel == "empty" ? string.Empty
                : $$$""",{"name":"focus","valueReference":{"reference":"{{{baseUrl}}}/Encounter/{{{encounterId}}}"}},{"name":"additional-context","valueReference":{"reference":"{{{baseUrl}}}/Patient/sub-patient"}}""";
            return $$$"""
                {"resourceType":"Parameters","id":"{{{id}}}","parameter":[
                {"name":"subscription","valueReference":{"reference":"{{{baseUrl}}}/Subscription/local-subscription"}},
                {"name":"topic","valueCanonical":"http://localhost/SubscriptionTopic/sdk6-encounter"},
                {"name":"status","valueCode":"active"},{"name":"type","valueCode":"event-notification"},
                {"name":"events-since-subscription-start","valueString":"1"},
                {"name":"notification-event","part":[{"name":"event-number","valueString":"1"},{"name":"timestamp","valueInstant":"{{{timestamp}}}"}{{{references}}}]}]}
                """;
        }
        string context = contentLevel == "empty" ? string.Empty
            : $$$""","focus":{"reference":"{{{baseUrl}}}/Encounter/{{{encounterId}}}"},"additionalContext":[{"reference":"{{{baseUrl}}}/Patient/sub-patient"}]""";
        return $$$"""
            {"resourceType":"SubscriptionStatus","id":"{{{id}}}","status":"active","type":"event-notification","eventsSinceSubscriptionStart":"1",
             "notificationEvent":[{"eventNumber":"1","timestamp":"{{{timestamp}}}"{{{context}}}}],
             "subscription":{"reference":"{{{baseUrl}}}/Subscription/local-subscription"},"topic":"http://localhost/SubscriptionTopic/sdk6-encounter"}
            """;
    }

    private static string StatusXml(SubscriptionGraph graph, string contentLevel, string encounterId, string id, string timestamp)
    {
        string baseUrl = graph.Store.Config.BaseUrl;
        if (graph.Store.Config.FhirVersion == FhirReleases.FhirSequenceCodes.R4)
        {
            string references = contentLevel == "empty" ? string.Empty
                : $"""<part><name value="focus"/><valueReference><reference value="{baseUrl}/Encounter/{encounterId}"/></valueReference></part><part><name value="additional-context"/><valueReference><reference value="{baseUrl}/Patient/sub-patient"/></valueReference></part>""";
            return $"""
                <Parameters xmlns="http://hl7.org/fhir"><id value="{id}"/>
                <parameter><name value="subscription"/><valueReference><reference value="{baseUrl}/Subscription/local-subscription"/></valueReference></parameter>
                <parameter><name value="topic"/><valueCanonical value="http://localhost/SubscriptionTopic/sdk6-encounter"/></parameter>
                <parameter><name value="status"/><valueCode value="active"/></parameter><parameter><name value="type"/><valueCode value="event-notification"/></parameter>
                <parameter><name value="events-since-subscription-start"/><valueString value="1"/></parameter>
                <parameter><name value="notification-event"/><part><name value="event-number"/><valueString value="1"/></part><part><name value="timestamp"/><valueInstant value="{timestamp}"/></part>{references}</parameter></Parameters>
                """;
        }
        string context = contentLevel == "empty" ? string.Empty
            : $"""<focus><reference value="{baseUrl}/Encounter/{encounterId}"/></focus><additionalContext><reference value="{baseUrl}/Patient/sub-patient"/></additionalContext>""";
        return $"""
            <SubscriptionStatus xmlns="http://hl7.org/fhir"><id value="{id}"/><status value="active"/><type value="event-notification"/><eventsSinceSubscriptionStart value="1"/>
            <notificationEvent><eventNumber value="1"/><timestamp value="{timestamp}"/>{context}</notificationEvent>
            <subscription><reference value="{baseUrl}/Subscription/local-subscription"/></subscription><topic value="http://localhost/SubscriptionTopic/sdk6-encounter"/></SubscriptionStatus>
            """;
    }

    private static string EncounterXml(FhirReleases.FhirSequenceCodes version, string id)
    {
        string status = version == FhirReleases.FhirSequenceCodes.R5 ? "completed" : "finished";
        string encounterClass = version == FhirReleases.FhirSequenceCodes.R5
            ? """<class><coding><system value="http://terminology.hl7.org/CodeSystem/v3-ActCode"/><code value="AMB"/></coding></class>"""
            : """<class><system value="http://terminology.hl7.org/CodeSystem/v3-ActCode"/><code value="AMB"/></class>""";
        return $"""<Encounter xmlns="http://hl7.org/fhir"><id value="{id}"/><language value="en"/><status value="{status}"/>{encounterClass}<subject><reference value="Patient/sub-patient"/></subject></Encounter>""";
    }

    private static string Primitive(XElement parent, string name) =>
        parent.Element(_fhir + name)?.Attribute("value")?.Value ?? throw new InvalidOperationException("Missing FHIR XML primitive: " + name);

    private sealed class SubscriptionGraph(IFhirStore store)
    {
        internal IFhirStore Store { get; } = store;
        internal ParsedSubscription Subscription { get; set; } = null!;
        internal Dictionary<string, string> Json { get; } = new(StringComparer.Ordinal);
        internal Dictionary<string, GeneratedResourceExpectation> Bindings { get; } = new(StringComparer.Ordinal);
    }

    private sealed class NotificationHost : IAsyncDisposable
    {
        private readonly FhirPackageService _packages;
        private readonly FhirStoreManager _manager;
        private bool _disposed;

        internal NotificationHost(FhirReleases.FhirSequenceCodes version)
        {
            Files = new LocalPackageFixture();
            Bindings = new CompatibilityBindings();
            Primary = "notify-oak-" + LocalPackageFixture.VersionName(version);
            Sibling = "notify-elm-" + LocalPackageFixture.VersionName(version);
            Dictionary<string, TenantConfiguration> tenants = new(StringComparer.Ordinal);
            foreach (string name in new[] { Primary, Sibling })
            {
                TenantConfiguration tenant = new()
                {
                    FhirVersion = version,
                    ControllerName = name,
                    BaseUrl = "http://127.0.0.1/fhir/" + name,
                    AllowExistingId = true,
                    AllowCreateAsUpdate = true,
                };
                tenants.Add(name, tenant);
                WireContractAssertions.BindBaseUrl(Bindings, name, tenant.BaseUrl);
            }
            CandleConfig config = Files.Configuration();
            _packages = new FhirPackageService(NullLogger<FhirPackageService>.Instance, config);
            _manager = new FhirStoreManager(tenants, NullLogger<FhirStoreManager>.Instance, config, _packages);
            INotificationManager? service = null;
            try
            {
                _manager.Init();
                Stores = _manager;
                service = new NotificationManager(config, tenants, _manager, Log);
                Service = service;
                Assert.All(Stores.Values, store =>
                {
                    Assert.Empty(store["Patient"].Keys);
                    Assert.Empty(store["Encounter"].Keys);
                    Assert.Empty(store.CurrentSubscriptions);
                    Assert.Empty(store.CurrentTopics);
                });
            }
            catch
            {
                service?.Dispose();
                foreach (IFhirStore store in ((IFhirStoreManager)_manager).Values)
                {
                    store.Dispose();
                }
                ((IDisposable)_manager).Dispose();
                ((IDisposable)_packages).Dispose();
                Files.Dispose();
                throw;
            }
        }

        internal LocalPackageFixture Files { get; }
        internal CompatibilityBindings Bindings { get; }
        internal string Primary { get; }
        internal string Sibling { get; }
        internal IFhirStoreManager Stores { get; }
        internal INotificationManager Service { get; }
        internal NotificationLog Log { get; } = new();

        internal async Task StartAsync()
        {
            using CancellationTokenSource startup = new(LocalHttpFixture.Deadline);
            await ((IHostedService)_manager).StartAsync(startup.Token);
            await Service.StartAsync(startup.Token);
        }

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }
            try
            {
                await Service.StopAsync(CancellationToken.None).WaitAsync(LocalHttpFixture.Deadline);
            }
            finally
            {
                // Service.Dispose also joins a canceled stop before these stores can be released.
                Service.Dispose();
                await ((IHostedService)_manager).StopAsync(CancellationToken.None).WaitAsync(LocalHttpFixture.Deadline);
                foreach (IFhirStore store in Stores.Values)
                {
                    store.Dispose();
                }
                ((IDisposable)_manager).Dispose();
                ((IDisposable)_packages).Dispose();
                Files.Dispose();
                _disposed = true;
            }
        }
    }

    private sealed class NotificationLog : ILogger<NotificationManager>
    {
        private readonly ConcurrentQueue<(LogLevel Level, string Message, Exception? Exception)> _entries = new();

        internal IReadOnlyList<(LogLevel Level, string Message, Exception? Exception)> Entries => _entries.ToArray();

        /// <inheritdoc />
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        /// <inheritdoc />
        public bool IsEnabled(LogLevel logLevel) => true;

        /// <inheritdoc />
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            _entries.Enqueue((logLevel, formatter(state, exception), exception));
    }
}
