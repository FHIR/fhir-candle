// <copyright file="SubscriptionLifecycleTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

extern alias candleR4;
extern alias candleR4B;
extern alias candleR5;

using System.Collections;
using System.Net;
using System.Reflection;
using System.Text.Json.Nodes;
using fhir.candle.Tests.Infrastructure;
using FhirCandle.Models;
using FhirCandle.Storage;
using FhirCandle.Utils;
using Hl7.FhirPath;

namespace fhir.candle.Tests;

/// <summary>Deterministic store/converter lifecycle regressions on all importing SDKs.</summary>
public sealed class SubscriptionLifecycleTests
{
    public static IEnumerable<object[]> Versions => LifecycleTestStore.Versions;

    [Fact]
    public void VersionInventoryIsComplete() =>
        Assert.Equal(new[] { "R4", "R4B", "R5" }, Versions.Select(row => row[0].ToString()).ToArray());

    [Theory]
    [MemberData(nameof(Versions))]
    public void IncomingStatusesAndRoundTripsPreserveSdkLiterals(FhirReleases.FhirSequenceCodes version)
    {
        using LifecycleTestStore fixture = new(version);
        fixture.Seed();
        object native = fixture.RawSubscription();
        Type statusType = Nullable.GetUnderlyingType(native.GetType().GetProperty("Status")!.PropertyType)!;
        string[] statuses = Enum.GetValues(statusType).Cast<Enum>()
            .Select(value => Hl7.Fhir.Utility.EnumUtility.GetLiteral(value)!).ToArray();
        Assert.NotEmpty(statuses);
        Assert.Contains("requested", statuses);
        Assert.Contains("active", statuses);
        Assert.Contains("off", statuses);
        foreach (string status in statuses)
        {
            fixture.PutSubscription(status);
            Assert.Equal(status, fixture.Subscription.CurrentStatus);
            Assert.Equal(status, fixture.Get("Subscription/" + LifecycleTestStore.Id)["status"]!.GetValue<string>());
            Assert.True(fixture.Store.TrySerializeToSubscription(fixture.Subscription, out string serialized, false));
            Assert.Equal(status, JsonNode.Parse(serialized)!["status"]!.GetValue<string>());
            Assert.Equal(status, fixture.StatusLiteral(fixture.Get("Subscription/" + LifecycleTestStore.Id + "/$status")));
        }

        JsonObject missing = fixture.SubscriptionJson();
        missing.Remove("status");
        Assert.False(fixture.Interact("PUT", "Subscription/" + LifecycleTestStore.Id, missing).Success);
        JsonObject invalid = fixture.SubscriptionJson();
        invalid["status"] = "not-a-status";
        Assert.False(fixture.Interact("PUT", "Subscription/" + LifecycleTestStore.Id, invalid).Success);

        fixture.PutSubscription();
        ParsedSubscription parsed = fixture.Subscription;
        foreach (long invalidTicks in new[] { -2L, long.MaxValue })
        {
            parsed.ExpirationTicks = invalidTicks;
            Assert.False(fixture.Store.TrySerializeToSubscription(parsed, out _, false));
        }
        parsed.ExpirationTicks = SubscriptionLifecyclePolicy.UnlimitedExpirationTicks;
        Assert.True(fixture.Store.TrySerializeToSubscription(parsed, out string unlimited, false));
        Assert.Null(JsonNode.Parse(unlimited)!["end"]);
        parsed.ExpirationTicks = LifecycleTestStore.Now.AddMinutes(10).UtcTicks;
        Assert.True(fixture.Store.TrySerializeToSubscription(parsed, out string finite, false));
        Assert.Equal(LifecycleTestStore.Now.AddMinutes(10), DateTimeOffset.Parse(JsonNode.Parse(finite)!["end"]!.GetValue<string>()));
        parsed.ExpirationTicks = 0;
        Assert.True(fixture.Store.TrySerializeToSubscription(parsed, out string zero, false));
        Assert.Equal(DateTimeOffset.MinValue, DateTimeOffset.Parse(JsonNode.Parse(zero)!["end"]!.GetValue<string>()));
        parsed.ExpirationTicks = -1;
    }

    [Theory]
    [MemberData(nameof(Versions))]
    public async Task ConfiguredDeadlineUsesUtcAndTenantOwnedConverter(FhirReleases.FhirSequenceCodes version)
    {
        (int Maximum, string? End, long Deadline)[] cases =
        [
            (30, null, LifecycleTestStore.Now.AddMinutes(30).UtcTicks),
            (30, "2026-11-19T20:10:00Z", LifecycleTestStore.Now.AddMinutes(10).UtcTicks),
            (30, "2026-11-19T21:00:00Z", LifecycleTestStore.Now.AddMinutes(30).UtcTicks),
            (30, "2026-11-19T21:10:00+01:00", LifecycleTestStore.Now.AddMinutes(10).UtcTicks),
            (0, null, -1),
            (0, "2026-11-19T20:10:00Z", LifecycleTestStore.Now.AddMinutes(10).UtcTicks),
            (30, "2026-11-19T20:00:00Z", LifecycleTestStore.Now.UtcTicks),
            (30, "2026-11-19T19:59:00Z", LifecycleTestStore.Now.AddMinutes(-1).UtcTicks),
        ];
        Assert.Equal(8, cases.Length);
        foreach ((int maximum, string? end, long deadline) in cases)
        {
            using LifecycleTestStore fixture = new(version, maximum);
            fixture.Seed(end: end);
            Assert.Equal(deadline, fixture.Subscription.ExpirationTicks);
            Assert.Equal(end is not null && deadline <= LifecycleTestStore.Now.UtcTicks ? "off" : "active", fixture.Subscription.CurrentStatus);
            if (end is not null)
            {
                Assert.Equal(DateTimeOffset.Parse(end), DateTimeOffset.Parse(fixture.Get("Subscription/" + LifecycleTestStore.Id)["end"]!.GetValue<string>()));
            }
        }
        foreach (bool reversed in new[] { false, true })
        {
            using LifecycleTestStore first = new(version, reversed ? 45 : 5);
            using LifecycleTestStore second = new(version, reversed ? 5 : 45);
            first.Seed();
            second.Seed();
            Assert.Equal(LifecycleTestStore.Now.AddMinutes(reversed ? 45 : 5).UtcTicks, first.Subscription.ExpirationTicks);
            Assert.Equal(LifecycleTestStore.Now.AddMinutes(reversed ? 5 : 45).UtcTicks, second.Subscription.ExpirationTicks);
        }
        using LifecycleTestStore contended = new(version, 1);
        contended.Seed();
        object resourceStore = contended.Store["Subscription"];
        System.Threading.Lock mutation = (System.Threading.Lock)resourceStore.GetType()
            .GetField("_lockObject", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(resourceStore)!;
        using ManualResetEventSlim parsed = new();
        using ManualResetEventSlim release = new();
        using ManualResetEventSlim holding = new();
        AsyncLocal<bool> updating = new();
        List<(DateTimeOffset Now, bool Lifecycle, bool Mutation)> reads = [];
        contended.ClockRead = now =>
        {
            if (updating.Value)
            {
                bool lifecycle = Monitor.IsEntered(contended.Field("SubscriptionLifecycleLock"));
                reads.Add((now, lifecycle, mutation.IsHeldByCurrentThread));
                if (!lifecycle)
                {
                    parsed.Set();
                }
            }
        };
        Task holder = Task.Run(() =>
        {
            lock (mutation)
            {
                holding.Set();
                Assert.True(release.Wait(TimeSpan.FromSeconds(10)));
            }
        });
        Task? update = null;
        try
        {
            Assert.True(holding.Wait(TimeSpan.FromSeconds(10)));
            update = Task.Run(() =>
            {
                updating.Value = true;
                contended.PutSubscription();
            });
            Assert.True(parsed.Wait(TimeSpan.FromSeconds(10)));
            contended.Clock.Advance(TimeSpan.FromSeconds(20));
        }
        finally
        {
            release.Set();
            await Task.WhenAll(update is null ? [holder] : [holder, update]).WaitAsync(TimeSpan.FromSeconds(10));
            contended.ClockRead = null;
        }
        Assert.Equal(LifecycleTestStore.Now, reads[0].Now);
        (DateTimeOffset commitNow, bool _, bool mutationHeld) = reads.First(read => read.Lifecycle);
        Assert.Equal(LifecycleTestStore.Now.AddSeconds(20), commitNow);
        Assert.True(mutationHeld);
        Assert.Equal(LifecycleTestStore.Now.AddSeconds(80).UtcTicks, contended.Subscription.ExpirationTicks);
    }

    [Theory]
    [MemberData(nameof(Versions))]
    public async Task StatusChangesAndResourceUpdatesReconcileEligibility(FhirReleases.FhirSequenceCodes version)
    {
        using LifecycleTestStore fixture = new(version);
        fixture.Seed();
        fixture.Match("positive");
        ParsedSubscription subscription = fixture.Subscription;
        Assert.Equal(1, subscription.CurrentEventCount);
        fixture.Store.ChangeSubscriptionStatus(LifecycleTestStore.Id, "off");
        long offRevision = subscription.Revision;
        fixture.AssertViews("off");
        fixture.Match("inactive");
        fixture.Reprocess(subscription);
        fixture.Match("reprocessed");
        Assert.Equal(1, subscription.CurrentEventCount);
        fixture.Store.ChangeSubscriptionStatus(LifecycleTestStore.Id, "invalid");
        fixture.Store.ChangeSubscriptionStatus(LifecycleTestStore.Id, "off");
        Assert.Equal(offRevision, subscription.Revision);

        fixture.PutSubscription("requested");
        SubscriptionSendEventArgs handshake = fixture.Args(ParsedSubscription.NotificationTypeCodes.Handshake);
        Assert.True(fixture.Store.TryDispatchSubscriptionNotification(handshake,
            () => Task.FromResult(new SubscriptionNotificationOutcome(SubscriptionNotificationOutcome.OutcomeKind.Delivered)),
            out Task<SubscriptionNotificationOutcome> delivery));
        Assert.True((await delivery).Succeeded);
        Assert.True(fixture.Store.TryCompleteSubscriptionHandshake(handshake));
        fixture.AssertViews("active");
        fixture.Match("activated");
        Assert.Equal(1, fixture.Subscription.CurrentEventCount);
        fixture.PutSubscription("off");
        fixture.AssertViews("off");
        fixture.Match("put-off");
        Assert.Equal(0, fixture.Subscription.CurrentEventCount);
    }

    [Theory]
    [MemberData(nameof(Versions))]
    public async Task PublicationCallbacksObserveOneLifecycleRevision(FhirReleases.FhirSequenceCodes version)
    {
        using LifecycleTestStore fixture = new(version);
        fixture.Seed(subscription: false);
        int callbacks = 0;
        fixture.Store.OnInstanceCreated += Observe;
        fixture.Store.OnInstanceUpdated += Observe;
        fixture.Store.OnInstanceDeleted += Observe;
        void Observe(object? sender, StoreInstanceEventArgs args)
        {
            if (args.ResourceType != "Subscription")
            {
                return;
            }
            fixture.AssertOutsideLocks();
            callbacks++;
            ParsedSubscription? current = fixture.Store.CurrentSubscriptions.SingleOrDefault();
            if (current is not null)
            {
                fixture.AssertViews(current.CurrentStatus);
                fixture.Match("callback-" + callbacks);
            }
            else
            {
                Assert.False(fixture.Interact("GET", "Subscription/" + LifecycleTestStore.Id).Success);
            }
        }
        (bool created, FhirResponseContext createdResponse) = fixture.Interact("POST", "Subscription", fixture.SubscriptionJson());
        Assert.True(created, createdResponse.SerializedOutcome);
        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        using ManualResetEventSlim reached = new();
        using ManualResetEventSlim release = new();
        fixture.Store.OnSubscriptionsChanged += (_, args) =>
        {
            if (args.ChangedSubscription?.CurrentStatus == "off")
            {
                reached.Set();
                Assert.True(release.Wait(TimeSpan.FromSeconds(10)));
            }
        };
        Task status = Task.Run(() => fixture.Store.ChangeSubscriptionStatus(LifecycleTestStore.Id, "off"));
        try
        {
            Assert.True(reached.Wait(TimeSpan.FromSeconds(10)));
            // A callback is held, not a store lock. Concurrent CRUD must finish and read current state.
            await Task.Run(() => fixture.PutSubscription("requested")).WaitAsync(TimeSpan.FromSeconds(10));
            fixture.AssertViews("requested");
        }
        finally
        {
            release.Set();
            await status.WaitAsync(TimeSpan.FromSeconds(10));
        }
        fixture.Delete();
        Assert.Equal(3, callbacks);
    }

    [Theory]
    [MemberData(nameof(Versions))]
    public async Task ReplacementAndRecreationRejectCapturedExecution(FhirReleases.FhirSequenceCodes version)
    {
        foreach (string transition in new[] { "cycle", "replace", "topic", "filter", "delete" })
        {
            using LifecycleTestStore fixture = new(version);
            fixture.Seed();
            ParsedSubscription prior = fixture.Subscription;
            using LifecycleTestStore.ExecutionGate gate = fixture.HoldMatcher();
            Task match = Task.Run(() => fixture.Match("held"));
            try
            {
                Assert.True(gate.Reached.Wait(TimeSpan.FromSeconds(10)));
                if (transition == "cycle")
                {
                    fixture.Store.ChangeSubscriptionStatus(LifecycleTestStore.Id, "off");
                    fixture.Store.ChangeSubscriptionStatus(LifecycleTestStore.Id, "active");
                }
                else
                {
                    if (transition == "delete")
                    {
                        fixture.Delete();
                    }
                    JsonObject replacement = fixture.SubscriptionJson();
                    if (transition == "topic")
                    {
                        replacement[version == FhirReleases.FhirSequenceCodes.R5 ? "topic" : "criteria"] = "http://localhost/SubscriptionTopic/other";
                    }
                    if (transition == "filter")
                    {
                        // Keep the native fixture's shape; only change the subscription filter value.
                        string changed = replacement.ToJsonString().Replace("Patient/sub-patient", "Patient/other-patient", StringComparison.Ordinal);
                        replacement = JsonNode.Parse(changed)!.AsObject();
                    }
                    fixture.Put(replacement);
                }
            }
            finally
            {
                gate.Release.Set();
                await match.WaitAsync(TimeSpan.FromSeconds(10));
            }
            Assert.Equal(0, prior.CurrentEventCount);
            Assert.Equal(0, fixture.Subscription.CurrentEventCount);
            Assert.Empty(fixture.Sent);
            fixture.Match("current");
            Assert.Equal(transition is "topic" or "filter" ? 0 : 1, fixture.Subscription.CurrentEventCount);
        }
        using LifecycleTestStore concurrent = new(version);
        concurrent.Seed();
        using Barrier rounds = new(2);
        await Task.WhenAll(Task.Run(() =>
        {
            for (int i = 0; i < 16; i++)
            {
                Assert.True(rounds.SignalAndWait(TimeSpan.FromSeconds(10)));
                concurrent.Match("concurrent-" + i);
            }
        }), Task.Run(() =>
        {
            for (int i = 0; i < 16; i++)
            {
                Assert.True(rounds.SignalAndWait(TimeSpan.FromSeconds(10)));
                concurrent.Store.ChangeSubscriptionStatus(LifecycleTestStore.Id, "off");
                concurrent.Store.ChangeSubscriptionStatus(LifecycleTestStore.Id, "active");
            }
        })).WaitAsync(TimeSpan.FromSeconds(10));
        long count = concurrent.Subscription.CurrentEventCount;
        concurrent.Match("after-concurrent-remove");
        Assert.Equal(count + 1, concurrent.Subscription.CurrentEventCount);
    }

    [Theory]
    [MemberData(nameof(Versions))]
    public void ExpirationRunsWithoutCapacityMonitoringOrInteraction(FhirReleases.FhirSequenceCodes version)
    {
        foreach (int capacity in new[] { 0, 1000 })
        {
            using LifecycleTestStore fixture = new(version, maximum: 1, capacity: capacity);
            fixture.Seed();
            ParsedSubscription subscription = fixture.Subscription;
            fixture.Clock.Advance(TimeSpan.FromMinutes(1));
            Assert.Equal(1, fixture.Clock.RunDueCallbacks());
            // Inspect the original object before any lazy-normalizing entry point.
            Assert.Equal("off", subscription.CurrentStatus);
            fixture.AssertViews("off");
            fixture.Match("expired");
            Assert.Equal(0, subscription.CurrentEventCount);
        }
        string directory = Path.Combine(Path.GetTempPath(), "candle-lifecycle-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            using LifecycleTestStore source = new(version);
            File.WriteAllText(Path.Combine(directory, "01-subscription.json"), source.SubscriptionJson().ToJsonString());
            File.WriteAllText(Path.Combine(directory, "02-topic.json"), source.Fixture("topic-fhirpath").ToJsonString());
            using LifecycleTestStore loaded = new(version, 1, loadDirectory: directory);
            ParsedSubscription subscription = loaded.Subscription;
            Assert.Equal(LifecycleTestStore.Now.AddMinutes(1).UtcTicks, subscription.ExpirationTicks);
            loaded.Clock.Advance(TimeSpan.FromMinutes(1));
            loaded.Clock.RunDueCallbacks();
            Assert.Equal("off", subscription.CurrentStatus);
            Assert.Equal("off", loaded.Get("Subscription/" + LifecycleTestStore.Id)["status"]!.GetValue<string>());
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Theory]
    [MemberData(nameof(Versions))]
    public async Task DeferredLoadingRejectsReplacedAndDeletedDescriptors(FhirReleases.FhirSequenceCodes version)
    {
        foreach (bool initialize in new[] { false, true })
        {
            foreach (string transition in new[] { "current", "off", "delete" })
            {
                string directory = Path.Combine(Path.GetTempPath(), "candle-deferred-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(directory);
                try
                {
                    using LifecycleTestStore source = new(version);
                    File.WriteAllText(Path.Combine(directory, "subscription.json"), source.SubscriptionJson().ToJsonString());
                    ParsedSubscription? prior = null;
                    ParsedSubscription? replacement = null;
                    SubscriptionSendEventArgs? captured = null;
                    long priorRevision = 0;
                    long replacementRevision = 0;
                    long replacementDeadline = 0;
                    int publications = 0;
                    int publicationsAfterTransition = 0;
                    void Configure(LifecycleTestStore fixture)
                    {
                        fixture.Store.OnSubscriptionsChanged += (_, _) => publications++;
                        fixture.Store.OnInstanceUpdated += (_, args) =>
                        {
                            if (args.ResourceType != "Subscription" || prior is not null)
                            {
                                return;
                            }
                            prior = fixture.Subscription;
                            Assert.True(prior.Revision > 0);
                            Assert.Equal(NowDeadline(), prior.ExpirationTicks);
                            Dictionary<string, List<object>> deferred = (Dictionary<string, List<object>>)fixture.Field("_loadReprocess");
                            Assert.Same(prior, Assert.Single(deferred["Subscription"]));
                            captured = fixture.Args();
                            fixture.Put(fixture.Fixture("topic-fhirpath"));
                            Assert.Equal(prior.Revision, fixture.ExecutableRevision(prior));
                            fixture.Clock.Advance(TimeSpan.FromSeconds(10));
                            if (transition == "delete")
                            {
                                fixture.Delete();
                            }
                            else if (transition == "off")
                            {
                                fixture.PutSubscription("off");
                                replacement = fixture.Subscription;
                                replacementRevision = replacement.Revision;
                                replacementDeadline = replacement.ExpirationTicks;
                            }
                            priorRevision = prior.Revision;
                            publicationsAfterTransition = publications;
                        };
                    }
                    long NowDeadline() => LifecycleTestStore.Now.AddMinutes(1).UtcTicks;

                    using LifecycleTestStore loaded = new(version, maximum: 1,
                        loadDirectory: initialize ? directory : null, protectLoadedContent: false, beforeInit: Configure);
                    if (!initialize)
                    {
                        loaded.Store.LoadPackage("unit.deferred#1.0.0", directory, string.Empty, false);
                    }
                    loaded.Put(loaded.Fixture("patient"));
                    ParsedSubscription original = Assert.IsType<ParsedSubscription>(prior);
                    Assert.Equal(publicationsAfterTransition, publications);
                    Assert.Equal(NowDeadline(), original.ExpirationTicks);
                    Assert.Equal(priorRevision, original.Revision);
                    Assert.False(loaded.Reprocess(new ParsedSubscription
                    {
                        Id = LifecycleTestStore.Id,
                        TopicUrl = LifecycleTestStore.Topic,
                        ExpirationTicks = -1,
                        Revision = original.Revision,
                    }));
                    if (transition == "current")
                    {
                        Assert.Same(original, loaded.Subscription);
                        loaded.AssertViews("active");
                        Assert.Equal(original.Revision, loaded.ExecutableRevision(original));
                        loaded.Match("current-deferred");
                        Assert.Equal(1, original.CurrentEventCount);
                        lock (original.GeneratedEvents)
                        {
                            Assert.Single(original.GeneratedEvents);
                        }
                        Assert.Single(loaded.Sent);
                        continue;
                    }

                    Assert.False(loaded.Reprocess(original));
                    Assert.False(loaded.Reprocess(original, remove: true));
                    if (transition == "delete")
                    {
                        Assert.Empty(loaded.Store.CurrentSubscriptions);
                        Assert.Null(loaded.RawSubscription());
                        Assert.False(loaded.Interact("GET", "Subscription/" + LifecycleTestStore.Id).Success);
                        Assert.Equal(0, loaded.Entries(loaded.Get("Subscription")));
                    }
                    else
                    {
                        Assert.Same(replacement, loaded.Subscription);
                        loaded.AssertViews("off");
                        Assert.Equal(replacementRevision, loaded.Subscription.Revision);
                        Assert.Equal(replacementDeadline, loaded.Subscription.ExpirationTicks);
                        Assert.Equal(LifecycleTestStore.Now.AddSeconds(10).AddMinutes(1).UtcTicks, replacementDeadline);
                        Assert.Equal(0, loaded.Subscription.CurrentEventCount);
                        lock (loaded.Subscription.GeneratedEvents)
                        {
                            Assert.Empty(loaded.Subscription.GeneratedEvents);
                        }
                    }
                    Assert.Null(loaded.ExecutableRevision(original));
                    loaded.Match("obsolete-deferred");
                    Assert.False(loaded.Store.TryDispatchSubscriptionNotification(
                        Assert.IsType<SubscriptionSendEventArgs>(captured),
                        () => throw new Xunit.Sdk.XunitException("obsolete deferred transport started"),
                        out Task<SubscriptionNotificationOutcome> suppressed));
                    Assert.Equal(SubscriptionNotificationOutcome.OutcomeKind.Suppressed, (await suppressed).Kind);
                    Assert.Equal(NowDeadline(), original.ExpirationTicks);
                    Assert.Equal(priorRevision, original.Revision);
                    Assert.Equal(0, original.CurrentEventCount);
                    lock (original.GeneratedEvents)
                    {
                        Assert.Empty(original.GeneratedEvents);
                    }
                    Assert.Empty(loaded.Sent);
                    Assert.Equal(publicationsAfterTransition, publications);
                }
                finally
                {
                    Directory.Delete(directory, true);
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(Versions))]
    public void NewDirectDescriptorsInstallAndCurrentReprocessingDoesNotRenew(FhirReleases.FhirSequenceCodes version)
    {
        using LifecycleTestStore fixture = new(version, maximum: 1);
        fixture.Seed(subscription: false);
        ParsedSubscription NewDescriptor() => new()
        {
            Id = LifecycleTestStore.Id,
            TopicUrl = LifecycleTestStore.Topic,
            ExpirationTicks = SubscriptionLifecyclePolicy.UnlimitedExpirationTicks,
        };
        ParsedSubscription original = NewDescriptor();
        Assert.Equal(0, original.Revision);
        Assert.True(fixture.Reprocess(original));
        long revision = original.Revision;
        long deadline = original.ExpirationTicks;
        Assert.True(revision > 0);
        Assert.Equal(LifecycleTestStore.Now.AddMinutes(1).UtcTicks, deadline);
        Assert.Null(fixture.RawSubscription());
        fixture.Match("direct-positive");
        Assert.Equal(1, original.CurrentEventCount);
        fixture.Clock.Advance(TimeSpan.FromSeconds(10));
        Assert.True(fixture.Reprocess(original));
        Assert.Same(original, fixture.Subscription);
        Assert.Equal(revision, original.Revision);
        Assert.Equal(deadline, original.ExpirationTicks);
        ParsedSubscription replacement = NewDescriptor();
        Assert.True(fixture.Reprocess(replacement));
        Assert.Same(replacement, fixture.Subscription);
        Assert.True(replacement.Revision > revision);
        Assert.Equal(LifecycleTestStore.Now.AddSeconds(10).AddMinutes(1).UtcTicks, replacement.ExpirationTicks);
        Assert.Null(fixture.RawSubscription());
        Assert.Equal(0, replacement.CurrentEventCount);
        lock (replacement.GeneratedEvents)
        {
            Assert.Empty(replacement.GeneratedEvents);
        }
        Assert.False(fixture.Reprocess(original));
        Assert.False(fixture.Reprocess(original, remove: true));
        fixture.Match("replacement-positive");
        Assert.Equal(1, original.CurrentEventCount);
        Assert.Equal(1, replacement.CurrentEventCount);
        Assert.Equal(2, fixture.Sent.Count);
    }

    [Theory]
    [MemberData(nameof(Versions))]
    public async Task DelayedExpiryCallbackCannotExposeOrDeliverActiveState(FhirReleases.FhirSequenceCodes version)
    {
        foreach (string entry in new[] { "get", "search", "nested", "status", "id-status", "instance-status", "match", "dispatch" })
        {
            using LifecycleTestStore fixture = new(version, 1);
            fixture.Seed();
            SubscriptionSendEventArgs args = fixture.Args();
            ParsedSubscription subscription = fixture.Subscription;
            fixture.Clock.Advance(TimeSpan.FromMinutes(1));
            switch (entry)
            {
                case "get":
                    JsonObject resource = fixture.Get("Subscription/" + LifecycleTestStore.Id);
                    Assert.Equal("off", resource["status"]!.GetValue<string>());
                    break;
                case "search":
                    JsonObject search = fixture.Get("Subscription?status=active");
                    Assert.Equal(0, fixture.Entries(search));
                    break;
                case "nested": Assert.Empty(fixture.NestedActiveSearch()); break;
                case "status":
                    JsonObject status = fixture.Get("Subscription/$status?status=active");
                    Assert.Equal(0, fixture.Entries(status));
                    break;
                case "id-status":
                    JsonObject idStatus = fixture.Get("Subscription/$status?id=" + LifecycleTestStore.Id + "&status=active");
                    Assert.Equal(0, fixture.Entries(idStatus));
                    break;
                case "instance-status":
                    JsonObject instanceStatus = fixture.Get("Subscription/" + LifecycleTestStore.Id + "/$status");
                    Assert.Equal("off", fixture.StatusLiteral(instanceStatus));
                    break;
                case "match": fixture.Match("due"); break;
                case "dispatch":
                    Assert.False(fixture.Store.TryDispatchSubscriptionNotification(args, () => throw new Xunit.Sdk.XunitException("stale start"), out Task<SubscriptionNotificationOutcome> task));
                    Assert.Equal(SubscriptionNotificationOutcome.OutcomeKind.Suppressed, (await task).Kind);
                    break;
            }
            Assert.Equal("off", subscription.CurrentStatus);
            fixture.AssertViews("off");
            Assert.Equal(0, subscription.CurrentEventCount);
        }
    }

    [Theory]
    [MemberData(nameof(Versions))]
    public async Task InternalTransitionsDoNotRenewButAcceptedReplacementDoes(FhirReleases.FhirSequenceCodes version)
    {
        using LifecycleTestStore fixture = new(version, 1);
        fixture.Seed(status: "requested");
        ParsedSubscription subscription = fixture.Subscription;
        long deadline = subscription.ExpirationTicks;
        fixture.Clock.Advance(TimeSpan.FromSeconds(10));
        Assert.True(fixture.Store.TryCompleteSubscriptionHandshake(fixture.Args(ParsedSubscription.NotificationTypeCodes.Handshake)));
        fixture.Reprocess(subscription);
        fixture.Store.ChangeSubscriptionStatus(LifecycleTestStore.Id, "off");
        fixture.Store.ChangeSubscriptionStatus(LifecycleTestStore.Id, "active");
        SubscriptionSendEventArgs args = fixture.Args();
        fixture.Store.TryDispatchSubscriptionNotification(args,
            () => Task.FromResult(new SubscriptionNotificationOutcome(SubscriptionNotificationOutcome.OutcomeKind.Delivered)),
            out Task<SubscriptionNotificationOutcome> task);
        await task;
        Assert.Equal(deadline, subscription.ExpirationTicks);
        fixture.Clock.Advance(TimeSpan.FromSeconds(50));
        ManualTimeProvider.CapturedCallback callback = Assert.Single(fixture.Clock.CaptureDueCallbacks());
        fixture.PutSubscription();
        long replacementDeadline = fixture.Subscription.ExpirationTicks;
        int changes = fixture.SubscriptionTimerChanges;
        callback.Invoke();
        Assert.Equal(1, fixture.SubscriptionTimerChanges - changes);
        Assert.Equal(LifecycleTestStore.Now.AddMinutes(2).UtcTicks, replacementDeadline);
        Assert.Equal("active", fixture.Subscription.CurrentStatus);
    }

    [Theory]
    [MemberData(nameof(Versions))]
    public void ExpirationBatchRearmsOnce(FhirReleases.FhirSequenceCodes version)
    {
        foreach (int batchSize in new[] { 4, 16 })
        {
            foreach (string entry in new[] { "timer", "current", "status" })
            {
                using LifecycleTestStore fixture = new(version, maximum: 0);
                fixture.Seed(subscription: false);
                DateTimeOffset deadline = LifecycleTestStore.Now.AddMinutes(1);
                for (int i = 0; i < batchSize; i++)
                {
                    PutSubscription(fixture, "due-" + i, i % 2 == 0 ? "active" : "requested", deadline);
                }
                PutSubscription(fixture, "retained-off", "off", deadline);
                PutSubscription(fixture, "later", "active", deadline.AddMinutes(2));
                PutSubscription(fixture, "unlimited", "active", null);
                Dictionary<string, ParsedSubscription> retained = fixture.Store.CurrentSubscriptions.ToDictionary(subscription => subscription.Id);
                Dictionary<string, long> revisions = retained.ToDictionary(pair => pair.Key, pair => pair.Value.Revision);
                long lastRevision = revisions.Values.Max();
                List<SubscriptionChangedEventArgs> publications = [];
                fixture.Store.OnSubscriptionsChanged += (_, args) =>
                {
                    fixture.AssertOutsideLocks();
                    Assert.All(retained.Values, subscription => Assert.False(Monitor.IsEntered(subscription.GeneratedEvents)));
                    Assert.All(retained.Where(pair => pair.Key.StartsWith("due-", StringComparison.Ordinal)),
                        pair => Assert.Equal("off", pair.Value.CurrentStatus));
                    publications.Add(args);
                };
                int changes = fixture.SubscriptionTimerChanges;
                fixture.Clock.Advance(TimeSpan.FromMinutes(1));
                switch (entry)
                {
                    case "timer":
                        Assert.Single(fixture.Clock.CaptureDueCallbacks()).Invoke();
                        break;
                    case "current":
                        Assert.Equal(batchSize + 3, fixture.Store.CurrentSubscriptions.Count());
                        break;
                    case "status":
                        Assert.Equal(batchSize + 3, fixture.Entries(fixture.Get("Subscription/$status")));
                        break;
                }

                // In the timer branch these assertions precede every normalizing read.
                Assert.Equal(1, fixture.SubscriptionTimerChanges - changes);
                Assert.Equal(batchSize, publications.Count);
                Assert.Equal(Enumerable.Range(1, batchSize).Select(offset => lastRevision + offset),
                    publications.Select(publication => publication.Revision).OrderBy(revision => revision));
                foreach ((string id, ParsedSubscription subscription) in retained)
                {
                    if (id.StartsWith("due-", StringComparison.Ordinal))
                    {
                        Assert.Equal("off", subscription.CurrentStatus);
                        Assert.Equal(deadline.UtcTicks, subscription.ExpirationTicks);
                        Assert.True(subscription.Revision > revisions[id]);
                        SubscriptionChangedEventArgs publication = Assert.Single(publications,
                            publication => publication.ChangedSubscription?.Id == id);
                        Assert.Same(subscription, publication.ChangedSubscription);
                        Assert.Equal(subscription.Revision, publication.Revision);
                        Assert.Null(publication.RemovedSubscriptionId);
                        Assert.False(publication.SendHandshake);
                        Assert.Null(fixture.ExecutableRevision(subscription));
                    }
                    else
                    {
                        Assert.Equal(id == "retained-off" ? "off" : "active", subscription.CurrentStatus);
                        Assert.Equal(revisions[id], subscription.Revision);
                        Assert.Equal(id == "unlimited" ? SubscriptionLifecyclePolicy.UnlimitedExpirationTicks :
                            (id == "later" ? deadline.AddMinutes(2) : deadline).UtcTicks, subscription.ExpirationTicks);
                        Assert.Equal(id == "retained-off" ? (long?)null : subscription.Revision, fixture.ExecutableRevision(subscription));
                    }
                }
                Assert.Equal(deadline.AddMinutes(2), fixture.Clock.NextDueTime);
                changes = fixture.SubscriptionTimerChanges;
                Assert.Equal(batchSize + 3, fixture.Store.CurrentSubscriptions.Count());
                Assert.Equal(batchSize + 3, fixture.Entries(fixture.Get("Subscription/$status")));
                Assert.Equal(0, fixture.SubscriptionTimerChanges - changes);
                Assert.Equal(batchSize, publications.Count);
            }
        }

        foreach (bool reprocess in new[] { false, true })
        {
            using LifecycleTestStore fixture = new(version, maximum: 1);
            fixture.Seed();
            ParsedSubscription subscription = fixture.Subscription;
            long revision = subscription.Revision;
            int changes = fixture.SubscriptionTimerChanges;
            fixture.Clock.Advance(TimeSpan.FromMinutes(1));
            if (reprocess)
            {
                Assert.True(fixture.Reprocess(subscription));
            }
            else
            {
                Assert.Equal("off", fixture.Get("Subscription/" + LifecycleTestStore.Id)["status"]!.GetValue<string>());
            }
            Assert.Equal("off", subscription.CurrentStatus);
            Assert.Equal(revision + 1, subscription.Revision);
            Assert.Equal(1, fixture.SubscriptionTimerChanges - changes);
            Assert.Null(fixture.Clock.NextDueTime);
        }
    }

    [Theory]
    [MemberData(nameof(Versions))]
    public void ExpirationTimerSelectsEarliestOfSeveralSubscriptions(FhirReleases.FhirSequenceCodes version)
    {
        DateTimeOffset earlier = LifecycleTestStore.Now.AddMinutes(1);
        DateTimeOffset later = LifecycleTestStore.Now.AddMinutes(3);
        foreach (bool earlierFirst in new[] { false, true })
        {
            using LifecycleTestStore fixture = new(version, maximum: 0);
            fixture.Seed(subscription: false);
            (string Id, DateTimeOffset? End)[] subscriptions = earlierFirst
                ? [(LifecycleTestStore.Id, earlier), ("unlimited", null), ("later", later)]
                : [("later", later), ("unlimited", null), (LifecycleTestStore.Id, earlier)];
            foreach ((string id, DateTimeOffset? end) in subscriptions)
            {
                PutSubscription(fixture, id, "active", end);
            }
            Dictionary<string, ParsedSubscription> retained = fixture.Store.CurrentSubscriptions.ToDictionary(subscription => subscription.Id);
            Dictionary<string, long> revisions = retained.ToDictionary(pair => pair.Key, pair => pair.Value.Revision);
            Assert.Equal(earlier, fixture.Clock.NextDueTime);
            int changes = fixture.SubscriptionTimerChanges;
            fixture.Clock.Advance(TimeSpan.FromMinutes(1));
            Assert.Single(fixture.Clock.CaptureDueCallbacks()).Invoke();
            Assert.Equal("off", retained[LifecycleTestStore.Id].CurrentStatus);
            Assert.True(retained[LifecycleTestStore.Id].Revision > revisions[LifecycleTestStore.Id]);
            Assert.Equal("active", retained["later"].CurrentStatus);
            Assert.Equal(revisions["later"], retained["later"].Revision);
            Assert.Equal("active", retained["unlimited"].CurrentStatus);
            Assert.Equal(revisions["unlimited"], retained["unlimited"].Revision);
            Assert.Equal(1, fixture.SubscriptionTimerChanges - changes);
            Assert.Equal(later, fixture.Clock.NextDueTime);
            long expiredRevision = retained[LifecycleTestStore.Id].Revision;
            changes = fixture.SubscriptionTimerChanges;
            fixture.Clock.Advance(TimeSpan.FromMinutes(2));
            Assert.Single(fixture.Clock.CaptureDueCallbacks()).Invoke();
            Assert.Equal("off", retained["later"].CurrentStatus);
            Assert.True(retained["later"].Revision > revisions["later"]);
            Assert.Equal(expiredRevision, retained[LifecycleTestStore.Id].Revision);
            Assert.Equal("active", retained["unlimited"].CurrentStatus);
            Assert.Equal(SubscriptionLifecyclePolicy.UnlimitedExpirationTicks, retained["unlimited"].ExpirationTicks);
            Assert.Equal(revisions["unlimited"], retained["unlimited"].Revision);
            Assert.Equal(retained["unlimited"].Revision, fixture.ExecutableRevision(retained["unlimited"]));
            Assert.Equal(1, fixture.SubscriptionTimerChanges - changes);
            Assert.Null(fixture.Clock.NextDueTime);
        }

        using LifecycleTestStore competing = new(version, maximum: 0);
        competing.Seed(subscription: false);
        PutSubscription(competing, LifecycleTestStore.Id, "active", earlier);
        PutSubscription(competing, "later", "active", later);
        PutSubscription(competing, "unlimited", "active", null);
        Dictionary<string, ParsedSubscription> remaining = competing.Store.CurrentSubscriptions.ToDictionary(subscription => subscription.Id);
        int before = competing.SubscriptionTimerChanges;
        competing.Store.ChangeSubscriptionStatus(LifecycleTestStore.Id, "off");
        Assert.Equal(1, competing.SubscriptionTimerChanges - before);
        Assert.Equal(later, competing.Clock.NextDueTime);
        before = competing.SubscriptionTimerChanges;
        competing.Store.ChangeSubscriptionStatus(LifecycleTestStore.Id, "active");
        Assert.Equal(1, competing.SubscriptionTimerChanges - before);
        Assert.Equal(earlier, competing.Clock.NextDueTime);
        foreach (DateTimeOffset replacementEnd in new[] { earlier.AddMinutes(1), earlier })
        {
            before = competing.SubscriptionTimerChanges;
            PutSubscription(competing, LifecycleTestStore.Id, "active", replacementEnd);
            Assert.Equal(1, competing.SubscriptionTimerChanges - before);
            Assert.Equal(replacementEnd, competing.Clock.NextDueTime);
        }
        before = competing.SubscriptionTimerChanges;
        competing.Delete();
        Assert.Equal(1, competing.SubscriptionTimerChanges - before);
        Assert.Equal(later, competing.Clock.NextDueTime);
        competing.Clock.Advance(TimeSpan.FromMinutes(1));
        Assert.Empty(competing.Clock.CaptureDueCallbacks());
        Assert.Equal("active", remaining["later"].CurrentStatus);
        before = competing.SubscriptionTimerChanges;
        competing.Clock.Advance(TimeSpan.FromMinutes(2));
        Assert.Single(competing.Clock.CaptureDueCallbacks()).Invoke();
        Assert.Equal("off", remaining["later"].CurrentStatus);
        Assert.Equal("active", remaining["unlimited"].CurrentStatus);
        Assert.Equal(1, competing.SubscriptionTimerChanges - before);
        Assert.Null(competing.Clock.NextDueTime);
    }

    private static void PutSubscription(LifecycleTestStore fixture, string id, string status, DateTimeOffset? end)
    {
        JsonObject resource = fixture.SubscriptionJson(status, end?.ToString("O"));
        resource["id"] = id;
        fixture.Put(resource);
    }

    [Theory]
    [MemberData(nameof(Versions))]
    public async Task ExpirationTimerRearmsAndDrainsAtDisposal(FhirReleases.FhirSequenceCodes version)
    {
        LifecycleTestStore fixture = new(version, 0);
        try
        {
            Assert.Null(fixture.Clock.NextDueTime);
            fixture.Seed(end: LifecycleTestStore.Now.AddMinutes(2).ToString("O"));
            Assert.Equal(LifecycleTestStore.Now.AddMinutes(2), fixture.Clock.NextDueTime);
            fixture.PutSubscription(end: LifecycleTestStore.Now.AddMinutes(1).ToString("O"));
            Assert.Equal(LifecycleTestStore.Now.AddMinutes(1), fixture.Clock.NextDueTime);
            fixture.Delete();
            Assert.Null(fixture.Clock.NextDueTime);
            fixture.PutSubscription();
            Assert.Null(fixture.Clock.NextDueTime);
            fixture.PutSubscription(end: DateTimeOffset.MaxValue.ToString("O"));
            Assert.Equal(LifecycleTestStore.Now + ManualTimeProvider.MaximumTimerInterval, fixture.Clock.NextDueTime);
            fixture.PutSubscription(end: LifecycleTestStore.Now.AddMinutes(1).ToString("O"));
            fixture.Clock.Advance(TimeSpan.FromMinutes(1));
            ManualTimeProvider.CapturedCallback callback = Assert.Single(fixture.Clock.CaptureDueCallbacks());
            using ManualResetEventSlim reached = new();
            using ManualResetEventSlim release = new();
            fixture.Store.OnSubscriptionsChanged += (_, _) =>
            {
                Assert.False(Monitor.IsEntered(fixture.Field("SubscriptionLifecycleLock")));
                reached.Set();
                Assert.True(release.Wait(TimeSpan.FromSeconds(10)));
            };
            Task running = Task.Run(callback.Invoke);
            Task? disposal = null;
            try
            {
                Assert.True(reached.Wait(TimeSpan.FromSeconds(10)));
                disposal = Task.Run(fixture.Dispose);
                await fixture.DisposalStarted.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.False(disposal.IsCompleted);
                Assert.False((bool)fixture.Store["Subscription"].GetType()
                    .GetField("_hasDisposed", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(fixture.Store["Subscription"])!);
            }
            finally
            {
                release.Set();
                await Task.WhenAll(disposal is null ? [running] : [running, disposal]).WaitAsync(TimeSpan.FromSeconds(10));
            }
            await fixture.Clock.WaitForCallbacksAsync().WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Null(fixture.Clock.NextDueTime);
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Theory]
    [MemberData(nameof(Versions))]
    public async Task InactiveHistoryRemainsInspectableWithFixedHistoryLock(FhirReleases.FhirSequenceCodes version)
    {
        using LifecycleTestStore fixture = new(version);
        fixture.Seed();
        fixture.Match("retained");
        ParsedSubscription subscription = fixture.Subscription;
        object history = subscription.GeneratedEvents;
        object stored = fixture.RawSubscription();
        SubscriptionEvent original;
        lock (fixture.Field("SubscriptionLifecycleLock"))
        {
            lock (subscription.GeneratedEvents)
            {
                original = subscription.GeneratedEvents[1];
                subscription.GeneratedEvents[1] = original with { AdditionalContext = [stored] };
            }
        }
        List<object> detachedContexts = [];
        try
        {
            object direct = fixture.Store.GetType().GetMethod("BundleForSubscriptionEvents")!
                .Invoke(fixture.Store, [LifecycleTestStore.Id, new long[] { 1 }, "query-event", "full-resource", false])!;
            object retained = fixture.Interact("GET", "Subscription/" + LifecycleTestStore.Id + "/$events?content=full-resource").Response.Resource!;
            foreach (object bundle in new[] { direct, retained })
            {
                object context = ((IEnumerable)bundle.GetType().GetProperty("Entry")!.GetValue(bundle)!).Cast<object>()
                    .Select(entry => entry.GetType().GetProperty("Resource")!.GetValue(entry))
                    .Single(resource => resource?.GetType().Name == "Subscription")!;
                Assert.NotSame(stored, context);
                detachedContexts.Add(context);
            }
        }
        finally
        {
            lock (fixture.Field("SubscriptionLifecycleLock"))
            {
                lock (subscription.GeneratedEvents)
                {
                    subscription.GeneratedEvents[1] = original;
                }
            }
        }
        fixture.Store.ChangeSubscriptionStatus(LifecycleTestStore.Id, "off");
        foreach (object context in detachedContexts)
        {
            Assert.Equal("active", Hl7.Fhir.Utility.EnumUtility.GetLiteral((Enum)context.GetType().GetProperty("Status")!.GetValue(context)!));
        }
        Assert.Same(history, subscription.GeneratedEvents);
        Assert.Equal(3, fixture.Entries(fixture.Get("Subscription/" + LifecycleTestStore.Id + "/$events?content=full-resource")));
        Assert.Equal(1, fixture.Entries(fixture.Get("Subscription/" + LifecycleTestStore.Id + "/$events?eventsSinceNumber=9&eventsUntilNumber=10&content=full-resource")));
        await fixture.AssertReplayOverlap();
        fixture.Clock.Advance(TimeSpan.FromMinutes(31));
        Assert.Equal(3, fixture.Entries(fixture.Get("Subscription/" + LifecycleTestStore.Id + "/$events?content=full-resource")));
        Assert.Equal("off", subscription.CurrentStatus);
        Assert.Same(history, subscription.GeneratedEvents);
    }

    [Theory]
    [MemberData(nameof(Versions))]
    public async Task ReplaySelectionAndEventAcceptanceRespectLifecycleHistoryOrder(FhirReleases.FhirSequenceCodes version)
    {
        using LifecycleTestStore fixture = new(version);
        fixture.Seed();
        fixture.Match("retained");
        await fixture.AssertReplayOverlap(replace: true);
    }
}

/// <summary>Small in-process fixture shared by the two lifecycle test classes.</summary>
internal sealed class LifecycleTestStore : IDisposable
{
    internal static readonly DateTimeOffset Now = new(2026, 11, 19, 20, 0, 0, TimeSpan.Zero);
    internal const string Id = "local-subscription";
    internal const string Topic = "http://localhost/SubscriptionTopic/sdk6-encounter";
    internal static IEnumerable<object[]> Versions =>
    [
        [FhirReleases.FhirSequenceCodes.R4],
        [FhirReleases.FhirSequenceCodes.R4B],
        [FhirReleases.FhirSequenceCodes.R5],
    ];
    private readonly JsonObject _resources;
    private readonly ObservedClock _observedClock;
    private bool _disposed;
    internal IFhirStore Store { get; }
    internal ManualTimeProvider Clock { get; }
    internal FhirReleases.FhirSequenceCodes Version { get; }
    internal Task DisposalStarted { get; }
    internal Action<DateTimeOffset>? ClockRead { set => _observedClock.OnRead = value; }
    internal int SubscriptionTimerChanges
    {
        get
        {
            Assert.Equal(1, _observedClock.TimersCreated);
            return _observedClock.AcceptedChanges;
        }
    }
    internal List<SubscriptionSendEventArgs> Sent { get; } = [];
    internal ParsedSubscription Subscription => Assert.Single(Store.CurrentSubscriptions);

    internal LifecycleTestStore(FhirReleases.FhirSequenceCodes version, int maximum = 30, int capacity = 0,
        string? loadDirectory = null, bool protectLoadedContent = true, Action<LifecycleTestStore>? beforeInit = null)
    {
        Version = version;
        Clock = new(Now);
        _observedClock = new(Clock);
        DisposalStarted = _observedClock.Started.Task;
        _resources = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "sdk6-compatibility",
            version.ToString().ToLowerInvariant(), "subscription-resources.json")))!.AsObject();
        Store = version switch
        {
            FhirReleases.FhirSequenceCodes.R4 => new candleR4::FhirCandle.Storage.VersionedFhirStore(_observedClock),
            FhirReleases.FhirSequenceCodes.R4B => new candleR4B::FhirCandle.Storage.VersionedFhirStore(_observedClock),
            FhirReleases.FhirSequenceCodes.R5 => new candleR5::FhirCandle.Storage.VersionedFhirStore(_observedClock),
            _ => throw new ArgumentOutOfRangeException(nameof(version)),
        };
        beforeInit?.Invoke(this);
        Store.Init(new()
        {
            FhirVersion = version,
            ControllerName = "lifecycle",
            BaseUrl = "http://unit.test/fhir/lifecycle",
            MaxSubscriptionExpirationMinutes = maximum,
            MaxResourceCount = capacity,
            LoadDirectory = loadDirectory is null ? null : new DirectoryInfo(loadDirectory),
            ProtectLoadedContent = loadDirectory is not null && protectLoadedContent,
        });
        Store.OnSubscriptionsChanged += (_, _) => AssertOutsideLocks();
        Store.OnSubscriptionSendEvent += (_, args) =>
        {
            AssertOutsideLocks();
            Sent.Add(args);
        };
    }

    internal JsonObject Fixture(string name) => _resources[name]!.DeepClone().AsObject();
    internal JsonObject SubscriptionJson(string status = "active", string? end = null)
    {
        JsonObject resource = Fixture("subscription");
        resource["status"] = status;
        if (end is null)
        {
            resource.Remove("end");
        }
        else
        {
            resource["end"] = end;
        }
        if (Version == FhirReleases.FhirSequenceCodes.R5)
        {
            resource["endpoint"] = "https://receiver.unit.test/notify";
        }
        else
        {
            resource["channel"]!["endpoint"] = "https://receiver.unit.test/notify";
        }
        return resource;
    }
    internal JsonObject SubscriptionWithBindingParameters(string status, params string[] parameters)
    {
        JsonObject resource = SubscriptionJson(status);
        foreach (string parameter in parameters)
        {
            if (Version == FhirReleases.FhirSequenceCodes.R5)
            {
                resource["filterBy"]!.AsArray().Add(new JsonObject
                {
                    ["resourceType"] = "Encounter",
                    ["filterParameter"] = parameter,
                    ["value"] = "ignored",
                });
            }
            else
            {
                JsonNode criteria = resource["_criteria"]!["extension"]![0]!;
                criteria["valueString"] = criteria["valueString"]!.GetValue<string>() + $"&{parameter}=ignored";
            }
        }
        return resource;
    }
    internal ParsedSubscription SubscriptionWithBindingModifiers(string status, params string[] modifiers)
    {
        lock (Field("SubscriptionLifecycleLock"))
        {
            ParsedSubscription source = (ParsedSubscription)((IDictionary)Field("_subscriptions"))[Id]!;
            ParsedSubscription.SubscriptionFilter patient = Assert.Single(source.Filters["Encounter"]);
            Assert.Equal("patient", patient.Name);
            ParsedSubscription subscription = new()
            {
                Id = source.Id,
                TopicUrl = source.TopicUrl,
                ExpirationTicks = source.ExpirationTicks,
                CurrentStatus = status,
                ChannelSystem = source.ChannelSystem,
                ChannelCode = source.ChannelCode,
                Endpoint = source.Endpoint,
                ContentType = source.ContentType,
                ContentLevel = source.ContentLevel,
                Filters = source.Filters.ToDictionary(entry => entry.Key, entry => new List<ParsedSubscription.SubscriptionFilter>(entry.Value)),
            };
            foreach (string modifier in modifiers)
            {
                subscription.Filters["Encounter"].Add(patient with { Modifier = modifier });
            }
            return subscription;
        }
    }
    internal void Seed(string status = "active", string? end = null, bool subscription = true)
    {
        Put(Fixture("patient"));
        Put(Fixture("topic-fhirpath"));
        if (subscription)
        {
            PutSubscription(status, end);
        }
    }
    internal void PutSubscription(string status = "active", string? end = null) => Put(SubscriptionJson(status, end));
    internal void Put(JsonObject resource)
    {
        (bool success, FhirResponseContext response) = Interact("PUT", resource["resourceType"] + "/" + resource["id"], resource);
        Assert.True(success, response.SerializedOutcome);
        Assert.True(response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Created, response.SerializedOutcome);
    }
    internal void Match(string id)
    {
        JsonObject resource = Fixture("complete");
        resource["id"] = id;
        Put(resource);
    }
    internal void Delete() => Assert.True(Interact("DELETE", "Subscription/" + Id).Success);
    internal JsonObject Get(string path)
    {
        (bool success, FhirResponseContext response) = Interact("GET", path);
        Assert.True(success, response.SerializedOutcome);
        return JsonNode.Parse(response.SerializedResource)!.AsObject();
    }
    internal (bool Success, FhirResponseContext Response) Interact(string method, string path, JsonObject? content = null)
    {
        FhirRequestContext context = new(Store, method, path)
        {
            SourceContent = content?.ToJsonString() ?? string.Empty,
            SourceFormat = "application/fhir+json",
            DestinationFormat = "application/fhir+json",
        };
        bool success = Store.PerformInteraction(context, out FhirResponseContext response, true, true);
        return (success, response);
    }
    internal int Entries(JsonObject bundle) => bundle["entry"]?.AsArray().Count ?? 0;
    internal string StatusLiteral(JsonObject bundle)
    {
        JsonNode status = bundle["entry"]![0]!["resource"]!;
        return Version == FhirReleases.FhirSequenceCodes.R4
            ? status["parameter"]!.AsArray().Single(parameter => parameter!["name"]!.GetValue<string>() == "status")!["valueCode"]!.GetValue<string>()
            : status["status"]!.GetValue<string>();
    }
    internal void AssertViews(string expected)
    {
        Assert.Equal(expected, Subscription.CurrentStatus);
        Assert.Equal(expected, Hl7.Fhir.Utility.EnumUtility.GetLiteral((Enum)RawSubscription().GetType().GetProperty("Status")!.GetValue(RawSubscription())!));
        Assert.Equal(expected, Get("Subscription/" + Id)["status"]!.GetValue<string>());
        Assert.Equal(expected == "active" ? 1 : 0, Entries(Get("Subscription?status=active")));
        Assert.Equal(expected == "off" ? 1 : 0, Entries(Get("Subscription?status=off")));
        Assert.Equal(expected, StatusLiteral(Get("Subscription/" + Id + "/$status")));
        Assert.Equal(1, Entries(Get("Subscription/$status?status=" + expected)));
    }
    internal SubscriptionSendEventArgs Args(ParsedSubscription.NotificationTypeCodes type = ParsedSubscription.NotificationTypeCodes.Heartbeat) => new()
    {
        Tenant = Store.Config,
        Subscription = Subscription,
        Revision = Subscription.Revision,
        NotificationType = type,
    };
    internal object Field(string name) => Store.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!.GetValue(Store)!;
    internal void AssertOutsideLocks()
    {
        Assert.False(Monitor.IsEntered(Field("SubscriptionLifecycleLock")));
        object resourceStore = Store["Subscription"];
        object mutation = resourceStore.GetType().GetField("_lockObject", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(resourceStore)!;
        Assert.False(mutation is System.Threading.Lock resourceLock ? resourceLock.IsHeldByCurrentThread : Monitor.IsEntered(mutation));
        object registration = resourceStore.GetType().GetField("_subscriptionRegistrationLock", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(resourceStore)!;
        Assert.False(Monitor.IsEntered(registration));
    }
    internal object RawSubscription() => Store["Subscription"].GetType().GetMethod("ReadRawSubscription")!.Invoke(Store["Subscription"], [Id])!;
    internal bool Reprocess(ParsedSubscription subscription, bool remove = false) =>
        (bool)Store.GetType().GetMethod("StoreProcessSubscription")!.Invoke(Store, [subscription, remove])!;
    internal long? ExecutableRevision(ParsedSubscription subscription)
    {
        object resourceStore = Store["Encounter"];
        object registration = resourceStore.GetType().GetField("_subscriptionRegistrationLock", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(resourceStore)!;
        lock (registration)
        {
            IDictionary topics = (IDictionary)resourceStore.GetType().GetField("_executableSubscriptions", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(resourceStore)!;
            object? executable = topics[subscription.TopicUrl];
            IDictionary? bindings = (IDictionary?)executable?.GetType().GetProperty("FiltersBySubscription")!.GetValue(executable);
            object? binding = bindings?[subscription.Id];
            return (long?)binding?.GetType().GetProperty("Revision")!.GetValue(binding);
        }
    }
    internal IEnumerable<object> NestedActiveSearch()
    {
        object resourceStore = Store["Subscription"];
        Type parameters = Store.GetType().Assembly.GetType("FhirCandle.Models.ParsedSearchParameter")!;
        object parsed = parameters.GetMethod("Parse", [typeof(string), Store.GetType(),
            Store.GetType().Assembly.GetType("FhirCandle.Storage.IVersionedResourceStore")!, typeof(string)])!
            .Invoke(null, ["status=active", Store, resourceStore, "Subscription"])!;
        return ((IEnumerable)resourceStore.GetType().GetMethod("TypeSearch")!.Invoke(resourceStore, [parsed, true])!).Cast<object>().ToArray();
    }
    internal ExecutionGate HoldMatcher() => new(this);
    internal sealed class ExecutionGate : IDisposable
    {
        internal ManualResetEventSlim Reached { get; } = new();
        internal ManualResetEventSlim Release { get; } = new();
        private readonly object _registration;
        private readonly object _executable;
        private readonly PropertyInfo _property;
        private readonly object _originalTriggers;
        internal ExecutionGate(LifecycleTestStore fixture)
        {
            object resourceStore = fixture.Store["Encounter"];
            object registration = resourceStore.GetType().GetField("_subscriptionRegistrationLock", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(resourceStore)!;
            _registration = registration;
            lock (registration)
            {
                IDictionary topics = (IDictionary)resourceStore.GetType().GetField("_executableSubscriptions", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(resourceStore)!;
                object executable = topics[Topic]!;
                PropertyInfo property = executable.GetType().GetProperty("FhirPathTriggers")!;
                _executable = executable;
                _property = property;
                _originalTriggers = property.GetValue(executable)!;
                object original = ((IEnumerable)property.GetValue(executable)!).Cast<object>().Single();
                CompiledExpression expression = (CompiledExpression)original.GetType().GetProperty("FhirPathTrigger")!.GetValue(original)!;
                CompiledExpression held = (root, context) =>
                {
                    Reached.Set();
                    Assert.True(Release.Wait(TimeSpan.FromSeconds(10)));
                    return expression(root, context);
                };
                Array triggers = Array.CreateInstance(original.GetType(), 1);
                triggers.SetValue(Activator.CreateInstance(original.GetType(), true, true, true, held), 0);
                property.SetValue(executable, triggers);
            }
        }
        public void Dispose()
        {
            Release.Set();
            lock (_registration)
            {
                _property.SetValue(_executable, _originalTriggers);
            }
            Reached.Dispose();
            Release.Dispose();
        }
    }
    internal async Task AssertReplayOverlap(bool replace = false)
    {
        ParsedSubscription prior = Subscription;
        object history = prior.GeneratedEvents;
        using ManualResetEventSlim reached = new();
        using ManualResetEventSlim release = new();
        using ManualResetEventSlim mutationAttempted = new();
        using ExecutionGate gate = HoldMatcher();
        SubscriptionEvent original;
        lock (prior.GeneratedEvents)
        {
            original = prior.GeneratedEvents[1];
            prior.GeneratedEvents[1] = original with { AdditionalContext = HoldContext() };
        }
        IEnumerable<object> HoldContext()
        {
            Assert.True(Monitor.IsEntered(prior.GeneratedEvents));
            Assert.True(Monitor.IsEntered(Field("SubscriptionLifecycleLock")));
            reached.Set();
            Assert.True(release.Wait(TimeSpan.FromSeconds(10)));
            yield break;
        }
        AsyncLocal<bool> replaying = new();
        int admissionReads = 0;
        ClockRead = _ =>
        {
            if (replaying.Value)
            {
                // The real operation has lifecycle protection before it ever takes history.
                Assert.True(Monitor.IsEntered(Field("SubscriptionLifecycleLock")));
                Assert.False(Monitor.IsEntered(history));
                admissionReads++;
            }
        };
        Task<JsonObject> replay = Task.Run(() =>
        {
            replaying.Value = true;
            return Get("Subscription/" + Id + "/$events?eventsSinceNumber=0&eventsUntilNumber=9223372036854775807&content=full-resource");
        });
        Task? mutation = null;
        Task? match = null;
        try
        {
            Assert.True(reached.Wait(TimeSpan.FromSeconds(10)));
            match = Task.Run(() => Match("during-replay"));
            Assert.True(gate.Reached.Wait(TimeSpan.FromSeconds(10)));
            gate.Release.Set();
            mutation = Task.Run(() =>
            {
                object lifecycle = Field("SubscriptionLifecycleLock");
                bool acquired = Monitor.TryEnter(lifecycle);
                if (acquired)
                {
                    Monitor.Exit(lifecycle);
                }
                Assert.False(acquired);
                mutationAttempted.Set();
                if (replace)
                {
                    PutSubscription();
                }
                else
                {
                    Store.ChangeSubscriptionStatus(Id, "active");
                }
            });
            Assert.True(mutationAttempted.Wait(TimeSpan.FromSeconds(10)));
            Assert.False(mutation.IsCompleted);
        }
        finally
        {
            gate.Release.Set();
            release.Set();
            await Task.WhenAll(new Task?[] { replay, match, mutation }.OfType<Task>()).WaitAsync(TimeSpan.FromSeconds(10));
            ClockRead = null;
            lock (Field("SubscriptionLifecycleLock"))
            {
                lock (prior.GeneratedEvents)
                {
                    prior.GeneratedEvents[1] = original;
                }
            }
        }
        Assert.Equal(2, Entries(await replay));
        Assert.True(admissionReads > 0);
        Assert.Equal(replace ? 0 : 1, Subscription.CurrentEventCount);
        Assert.Same(history, prior.GeneratedEvents);
    }

    // Observe the existing manual clock's reads/changes/disposal without changing phase-1 infrastructure.
    private sealed class ObservedClock(ManualTimeProvider clock) : TimeProvider
    {
        private int _timersCreated;
        private int _acceptedChanges;
        internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal Action<DateTimeOffset>? OnRead { get; set; }
        internal int TimersCreated => Volatile.Read(ref _timersCreated);
        internal int AcceptedChanges => Volatile.Read(ref _acceptedChanges);
        public override DateTimeOffset GetUtcNow()
        {
            DateTimeOffset now = clock.GetUtcNow();
            OnRead?.Invoke(now);
            return now;
        }
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            ITimer timer = clock.CreateTimer(callback, state, dueTime, period);
            Interlocked.Increment(ref _timersCreated);
            return new ObservedTimer(timer, this);
        }

        private sealed class ObservedTimer(ITimer timer, ObservedClock owner) : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period)
            {
                bool accepted = timer.Change(dueTime, period);
                if (accepted)
                {
                    Interlocked.Increment(ref owner._acceptedChanges);
                }
                return accepted;
            }
            public void Dispose()
            {
                owner.Started.TrySetResult();
                timer.Dispose();
            }
            public ValueTask DisposeAsync()
            {
                owner.Started.TrySetResult();
                return timer.DisposeAsync();
            }
        }
    }
    public void Dispose()
    {
        if (!_disposed)
        {
            Store.Dispose();
            _disposed = true;
        }
    }
}
