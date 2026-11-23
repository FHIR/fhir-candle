// <copyright file="ReceiptRetentionTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

extern alias candleR4;
extern alias candleR4B;
extern alias candleR5;

using System.Reflection;
using System.Text.Json.Nodes;
using fhir.candle.Tests.Infrastructure;
using FhirCandle.Models;
using FhirCandle.Performance;
using FhirCandle.Storage;
using FhirCandle.Utils;

namespace fhir.candle.Tests;

/// <summary>Receipt snapshots and maintenance on all importing stores.</summary>
public sealed class ReceiptRetentionTests
{
    public static IEnumerable<object[]> Versions => LifecycleTestStore.Versions;

    [Theory]
    [MemberData(nameof(Versions))]
    public void ZeroCapExpiresOnlyOldReceipts(FhirReleases.FhirSequenceCodes version)
    {
        using LifecycleTestStore fixture = new(version);
        IFhirStore store = fixture.Store;
        Assert.Equal(0, store.Config.MaxResourceCount);
        DateTimeOffset now = fixture.Clock.GetUtcNow();
        Action<string, ParsedSubscriptionStatus> register = store switch
        {
            candleR4::FhirCandle.Storage.VersionedFhirStore s => s.RegisterReceivedNotification,
            candleR4B::FhirCandle.Storage.VersionedFhirStore s => s.RegisterReceivedNotification,
            candleR5::FhirCandle.Storage.VersionedFhirStore s => s.RegisterReceivedNotification,
            _ => throw new ArgumentOutOfRangeException(nameof(version)),
        };
        List<string> context = ["original"];
        List<string> errors = ["error"];
        List<ParsedSubscriptionStatus.ParsedNotificationEvent> events = [new() { EventNumber = 1, AdditionalContextReferences = context }];
        void Add(string key, string id, DateTimeOffset instant) => register(id, new()
        {
            SubscriptionReference = key, BundleId = id, ProcessedDateTime = instant, SubscriptionTopicCanonical = "topic",
            NotificationType = ParsedSubscription.NotificationTypeCodes.EventNotification, Errors = errors, NotificationEvents = events,
        });
        object gate = Field(store, "_receiptGate");
        int registrations = 0;
        store.OnReceivedSubscriptionEvent += (_, _) =>
        {
            Assert.False(Monitor.IsEntered(gate));
            Interlocked.Increment(ref registrations);
        };
        Add("rejected", "boundary", now.AddMinutes(-10));
        Add("rejected", "older", now.AddMinutes(-11));
        Add("mixed", "fresh", now);
        Add("mixed", "boundary", now.AddMinutes(-9).ToOffset(TimeSpan.FromHours(1)));
        Add("mixed", "inside", now.AddMinutes(-9).AddTicks(1));
        Add("gone", "expired", now.AddMinutes(-9));
        Assert.Equal(4, registrations);
        Assert.Equal(now.AddSeconds(30), fixture.Clock.NextDueTime);
        IReadOnlyDictionary<string, IReadOnlyList<ParsedSubscriptionStatus>> prior = store.ReceivedNotifications;
        context.Clear();
        errors.Clear();
        events.Clear();
        ParsedSubscriptionStatus retained = prior["mixed"][0];
        Assert.Equal("original", Assert.Single(Assert.Single(retained.NotificationEvents).AdditionalContextReferences!));
        Assert.Equal("error", Assert.Single(retained.Errors));
        Assert.Throws<NotSupportedException>(() => ((IList<string>)retained.Errors).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<ParsedSubscriptionStatus>)prior["mixed"]).Clear());

        fixture.Clock.Advance(TimeSpan.FromMinutes(1));
        IReadOnlyDictionary<string, IReadOnlyList<ParsedSubscriptionStatus>> current = store.ReceivedNotifications;
        Assert.Equal(new[] { "fresh", "inside" }, Assert.Single(current).Value.Select(n => n.BundleId));
        Assert.Contains("gone", Backing(store).Keys);
        bool reentered = false;
        store.OnReceivedSubscriptionChanged += (_, change) =>
        {
            Assert.False(Monitor.IsEntered(gate));
            if (change.SubscriptionReference == "mixed")
            {
                Assert.Contains("inside", store.ReceivedNotifications["mixed"].Select(n => n.BundleId));
                Add("mixed", "reentrant", fixture.Clock.GetUtcNow());
                reentered = true;
            }
        };
        Parallel.Invoke(() => Assert.Equal(1, fixture.Clock.RunDueCallbacks()),
            () => Parallel.For(0, 32, i => Add("mixed", "parallel-" + i, fixture.Clock.GetUtcNow())));
        Assert.True(reentered);
        Assert.Equal(35, Backing(store)["mixed"].Select(n => n.BundleId).Distinct().Count());
        Assert.DoesNotContain("gone", Backing(store).Keys);
        Assert.Equal(3, prior["mixed"].Count);
        Assert.Single(prior["gone"]);
        fixture.Clock.Advance(TimeSpan.FromMinutes(11));
        ManualTimeProvider.CapturedCallback pending = Assert.Single(fixture.Clock.CaptureDueCallbacks());
        fixture.Dispose();
        pending.Invoke();
        Assert.Null(fixture.Clock.NextDueTime);
        Assert.Equal(35, Backing(store)["mixed"].Count);
    }

    [Theory]
    [MemberData(nameof(Versions))]
    public void HookReceiptsUseTenantProcessingTime(FhirReleases.FhirSequenceCodes version)
    {
        using LifecycleTestStore fixture = new(version);
        fixture.Clock.Advance(TimeSpan.FromDays(36500));
        DateTimeOffset processed = fixture.Clock.GetUtcNow();
        ProbeChallenge challenge = Workloads.CreateProbeChallenge(version.ToString());
        (bool success, FhirResponseContext response) = fixture.Interact("POST", "$subscription-hook",
            JsonNode.Parse(challenge.Notification)!.AsObject());
        Assert.True(success, response.SerializedOutcome);
        fixture.Clock.Advance(TimeSpan.FromMinutes(10) - TimeSpan.FromTicks(1));
        ParsedSubscriptionStatus status = Assert.Single(Assert.Single(fixture.Store.ReceivedNotifications).Value);
        Assert.Equal(processed, status.ProcessedDateTime);
        Assert.Equal(challenge.BundleId, status.BundleId);
        fixture.Clock.Advance(TimeSpan.FromTicks(1));
        Assert.Empty(fixture.Store.ReceivedNotifications);
        Assert.Single(Backing(fixture.Store));
        Assert.Equal(1, fixture.Clock.RunDueCallbacks());
        Assert.Empty(Backing(fixture.Store));
    }

    private static object Field(IFhirStore store, string name) =>
        store.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(store)!;

    private static Dictionary<string, List<ParsedSubscriptionStatus>> Backing(IFhirStore store) =>
        (Dictionary<string, List<ParsedSubscriptionStatus>>)Field(store, "_receivedNotifications");
}
