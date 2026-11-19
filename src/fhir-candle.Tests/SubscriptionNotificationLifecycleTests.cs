// <copyright file="SubscriptionNotificationLifecycleTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using System.Collections;
using System.Collections.Concurrent;
using System.Net;
using System.Reflection;
using fhir.candle.Services;
using FhirCandle.Configuration;
using FhirCandle.Models;
using FhirCandle.Storage;
using FhirCandle.Utils;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Outcome = FhirCandle.Models.SubscriptionNotificationOutcome;
using OutcomeKind = FhirCandle.Models.SubscriptionNotificationOutcome.OutcomeKind;

namespace fhir.candle.Tests;

/// <summary>In-process transport starts/completions, never remote-network timing.</summary>
public sealed class SubscriptionNotificationLifecycleTests
{
    public static IEnumerable<object[]> Versions => LifecycleTestStore.Versions;
    public static IEnumerable<object[]> Managers =>
        Versions.SelectMany(row => new[] { new object[] { row[0], false }, [row[0], true] });

    [Fact]
    public void ManagerVersionInventoryIsComplete()
    {
        Assert.Equal(6, Managers.Count());
        Assert.Equal(3, Managers.Select(row => row[0]).Distinct().Count());
        Assert.Equal(2, Managers.Select(row => row[1]).Distinct().Count());
    }

    [Theory]
    [MemberData(nameof(Managers))]
    public async Task StartupReconcilesPreinstalledRequestedWithoutRenewal(FhirReleases.FhirSequenceCodes version, bool restOnly)
    {
        using NotificationFixture fixture = new(version, restOnly);
        fixture.Store.PutSubscription("requested");
        ParsedSubscription subscription = fixture.Store.Subscription;
        long revision = subscription.Revision;
        long deadline = subscription.ExpirationTicks;
        fixture.Store.Clock.Advance(TimeSpan.FromSeconds(10));
        fixture.Start();
        fixture.Start();
        Assert.Equal(1, fixture.QueuedCount);
        Assert.Equal(1, fixture.HandshakeRevisionCount);
        Assert.Equal(revision, subscription.Revision);
        Assert.Equal(deadline, subscription.ExpirationTicks);
        await fixture.DrainOneAsync();
        Assert.Equal(1, fixture.Handler.Starts);
        fixture.Store.AssertViews("active");
        Assert.Equal(deadline, subscription.ExpirationTicks);
        Assert.Equal(fixture.Store.Clock.GetUtcNow().UtcTicks, subscription.LastCommunicationTicks);
        Assert.Equal(0, fixture.HandshakeRevisionCount);
        Assert.Empty(fixture.Log.Failures);
    }

    [Theory]
    [MemberData(nameof(Managers))]
    public void StartupSkipsInactiveAndExpiredSubscriptions(FhirReleases.FhirSequenceCodes version, bool restOnly)
    {
        foreach (string status in new[] { "off", "active", "expired-requested", "expired-active" })
        {
            using NotificationFixture fixture = new(version, restOnly);
            fixture.Store.PutSubscription(status.Replace("expired-", string.Empty, StringComparison.Ordinal));
            ParsedSubscription subscription = fixture.Store.Subscription;
            long deadline = subscription.ExpirationTicks;
            if (status.StartsWith("expired-", StringComparison.Ordinal))
            {
                fixture.Store.Clock.Advance(TimeSpan.FromMinutes(31));
            }
            fixture.Start();
            fixture.RunQueueTimer();
            Assert.Equal(0, fixture.QueuedCount);
            Assert.Equal(0, fixture.Handler.Starts);
            Assert.Equal(0, fixture.HandshakeRevisionCount);
            Assert.Equal(deadline, subscription.ExpirationTicks);
            fixture.Store.AssertViews(status == "active" ? "active" : "off");
        }
    }

    [Theory]
    [MemberData(nameof(Managers))]
    public async Task StartupHandshakeCannotActivateRevokedRevision(FhirReleases.FhirSequenceCodes version, bool restOnly)
    {
        foreach (bool inFlight in new[] { false, true })
        {
            foreach (string change in new[] { "off", "cycle", "replace", "delete", "recreate", "expire" })
            {
                using NotificationFixture fixture = new(version, restOnly);
                fixture.Store.PutSubscription("requested");
                ParsedSubscription prior = fixture.Store.Subscription;
                long deadline = prior.ExpirationTicks;
                fixture.Start();
                Assert.Equal(1, fixture.QueuedCount);
                TaskCompletionSource<HttpResponseMessage> response = new(TaskCreationOptions.RunContinuationsAsynchronously);
                fixture.Handler.StartOperation = _ => response.Task;
                Task? delivery = null;
                try
                {
                    if (inFlight)
                    {
                        delivery = fixture.DrainOneAsync();
                        Assert.Equal(1, fixture.Handler.Starts);
                    }
                    fixture.Revoke(change, requested: true);
                }
                finally
                {
                    response.TrySetResult(new HttpResponseMessage(HttpStatusCode.NoContent));
                    if (delivery is not null)
                    {
                        await delivery.WaitAsync(TimeSpan.FromSeconds(10));
                    }
                }
                if (!inFlight)
                {
                    await fixture.DrainOneAsync();
                }
                Assert.Equal(inFlight ? 1 : 0, fixture.Handler.Starts);
                Assert.Equal(deadline, prior.ExpirationTicks);
                Assert.Equal(0, prior.LastCommunicationTicks);
                Assert.Empty(prior.SerializedEvents);
                Assert.Empty(prior.NotificationErrors);
                if (change == "delete")
                {
                    Assert.Empty(fixture.Store.Store.CurrentSubscriptions);
                    Assert.Equal(0, fixture.HandshakeRevisionCount);
                }
                else
                {
                    fixture.Store.AssertViews(change is "off" or "expire" ? "off" : "requested");
                    Assert.Equal(0, fixture.Store.Subscription.LastCommunicationTicks);
                    Assert.Empty(fixture.Store.Subscription.SerializedEvents);
                    Assert.Empty(fixture.Store.Subscription.NotificationErrors);
                    Assert.Equal(change is "off" or "expire" ? 0 : 1, fixture.HandshakeRevisionCount);
                }
                Assert.Empty(fixture.Log.Failures);
            }
        }
    }

    [Theory]
    [MemberData(nameof(Managers))]
    public async Task StartupSnapshotAndDuplicatePublicationQueueOneHandshake(FhirReleases.FhirSequenceCodes version, bool restOnly)
    {
        using NotificationFixture fixture = new(version, restOnly);
        SubscriptionChangedEventArgs? requested = null;
        using ManualResetEventSlim snapshotReached = new();
        using ManualResetEventSlim releaseSnapshot = new();
        using ManualResetEventSlim publicationReached = new();
        fixture.Store.Store.OnSubscriptionsChanged += (_, change) =>
        {
            if (change.SendHandshake)
            {
                requested = change;
                publicationReached.Set();
            }
        };
        fixture.Store.PutSubscription("requested");
        SubscriptionChangedEventArgs publication = Assert.IsType<SubscriptionChangedEventArgs>(requested);
        publicationReached.Reset();
        bool held = false;
        fixture.Store.ClockRead = _ =>
        {
            if (!held)
            {
                held = true;
                snapshotReached.Set();
                Assert.True(releaseSnapshot.Wait(TimeSpan.FromSeconds(10)));
            }
        };
        Task startup = Task.Run(fixture.Start);
        Task? duplicate = null;
        try
        {
            Assert.True(snapshotReached.Wait(TimeSpan.FromSeconds(10)));
            duplicate = Task.Run(() => fixture.Publish(publication));
            Assert.True(publicationReached.Wait(TimeSpan.FromSeconds(10)));
        }
        finally
        {
            releaseSnapshot.Set();
            await Task.WhenAll(duplicate is null ? [startup] : [startup, duplicate]).WaitAsync(TimeSpan.FromSeconds(10));
            fixture.Store.ClockRead = null;
        }
        Assert.Equal(1, fixture.QueuedCount);
        Assert.Equal(1, fixture.HandshakeRevisionCount);
        TaskCompletionSource<HttpResponseMessage> response = new(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Handler.StartOperation = _ => response.Task;
        Task delivery = fixture.DrainOneAsync();
        try
        {
            Assert.Equal(1, fixture.Handler.Starts);
            fixture.Publish(publication);
            Assert.Equal(0, fixture.QueuedCount);
            Assert.Equal(1, fixture.HandshakeRevisionCount);
        }
        finally
        {
            response.TrySetResult(new HttpResponseMessage(HttpStatusCode.NoContent));
            await delivery.WaitAsync(TimeSpan.FromSeconds(10));
        }
        fixture.Publish(publication);
        Assert.Equal(0, fixture.QueuedCount);
        Assert.Equal(0, fixture.HandshakeRevisionCount);
        fixture.Store.PutSubscription("requested");
        Assert.Equal(1, fixture.HandshakeRevisionCount);
        fixture.Store.Delete();
        fixture.Publish(publication);
        Assert.Equal(0, fixture.HandshakeRevisionCount);
        Assert.Empty(fixture.Log.Failures);
    }

    [Theory]
    [MemberData(nameof(Managers))]
    public async Task QueuedEventHeartbeatAndHandshakeRespectCapturedRevision(FhirReleases.FhirSequenceCodes version, bool restOnly)
    {
        foreach (string work in new[] { "event", "heartbeat", "handshake" })
        {
            foreach (string change in new[] { "off", "cycle", "replace", "delete", "expire" })
            {
                using NotificationFixture fixture = new(version, restOnly);
                fixture.Start();
                if (work == "event")
                {
                    fixture.Store.Match("queued-event");
                }
                else if (work == "handshake")
                {
                    fixture.Store.PutSubscription("requested");
                }
                else
                {
                    fixture.Scan();
                    fixture.Store.Clock.Advance(TimeSpan.FromSeconds(120));
                    fixture.Scan();
                }
                Assert.Equal(1, fixture.QueuedCount);
                long oldRevision = fixture.Store.Subscription.Revision;
                fixture.Revoke(change, work == "handshake");
                await fixture.DrainOneAsync();
                Assert.Equal(0, fixture.Handler.Starts);
                Assert.Empty(fixture.Log.Failures);
                if (change != "delete")
                {
                    Assert.NotEqual(oldRevision, fixture.Store.Subscription.Revision);
                    Assert.Equal(0, fixture.Store.Subscription.LastCommunicationTicks);
                    Assert.Empty(fixture.Store.Subscription.SerializedEvents);
                    Assert.Empty(fixture.Store.Subscription.NotificationErrors);
                }
            }
        }
        using NotificationFixture positive = new(version, restOnly);
        positive.Start();
        positive.Store.Match("delivered-event");
        await positive.DrainOneAsync();
        Assert.Equal(1, positive.Handler.Starts);
        Assert.Equal(positive.Store.Clock.GetUtcNow().UtcTicks, positive.Store.Subscription.LastCommunicationTicks);
        Assert.Single(positive.Store.Subscription.SerializedEvents);
        positive.Store.PutSubscription("requested");
        await positive.DrainOneAsync();
        Assert.Equal(2, positive.Handler.Starts);
        Assert.Equal("active", positive.Store.Subscription.CurrentStatus);
    }

    [Theory]
    [MemberData(nameof(Managers))]
    public async Task FinalAdmissionSerializesActualTransportStartWithOff(FhirReleases.FhirSequenceCodes version, bool restOnly)
    {
        using NotificationFixture fixture = new(version, restOnly);
        using ManualResetEventSlim reached = new();
        using ManualResetEventSlim release = new();
        using ManualResetEventSlim attempted = new();
        fixture.Handler.StartOperation = _ =>
        {
            reached.Set();
            Assert.True(release.Wait(TimeSpan.FromSeconds(10)));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
        };
        Task<Outcome> delivery = Task.Run(() => fixture.Notify(fixture.Store.Args()));
        Task? off = null;
        try
        {
            Assert.True(reached.Wait(TimeSpan.FromSeconds(10)));
            off = Task.Run(() =>
            {
                object lifecycle = fixture.Store.Field("SubscriptionLifecycleLock");
                bool acquired = Monitor.TryEnter(lifecycle);
                if (acquired)
                {
                    Monitor.Exit(lifecycle);
                }
                Assert.False(acquired);
                attempted.Set();
                fixture.Store.Store.ChangeSubscriptionStatus(LifecycleTestStore.Id, "off");
            });
            Assert.True(attempted.Wait(TimeSpan.FromSeconds(10)));
            Assert.False(off.IsCompleted);
        }
        finally
        {
            release.Set();
            await Task.WhenAll(off is null ? [delivery] : [delivery, off]).WaitAsync(TimeSpan.FromSeconds(10));
        }
        Assert.Equal(1, fixture.Handler.Starts);
        Assert.Equal(OutcomeKind.Suppressed, (await fixture.Notify(fixture.Store.Args())).Kind);
        Assert.Equal(1, fixture.Handler.Starts);
    }

    [Theory]
    [MemberData(nameof(Managers))]
    public async Task LateCompletionCannotChangeNewLifecycleBookkeeping(FhirReleases.FhirSequenceCodes version, bool restOnly)
    {
        foreach (ParsedSubscription.NotificationTypeCodes type in new[]
            {
                ParsedSubscription.NotificationTypeCodes.Handshake,
                ParsedSubscription.NotificationTypeCodes.Heartbeat,
                ParsedSubscription.NotificationTypeCodes.EventNotification,
            })
        {
            foreach (bool failed in new[] { false, true })
            {
                foreach (string change in new[] { "cycle", "replace", "recreate", "expire" })
                {
                    using NotificationFixture fixture = new(version, restOnly);
                    bool handshake = type == ParsedSubscription.NotificationTypeCodes.Handshake;
                    if (handshake)
                    {
                        fixture.Store.PutSubscription("requested");
                    }
                    else if (type == ParsedSubscription.NotificationTypeCodes.EventNotification)
                    {
                        fixture.Store.Match("late-event");
                    }
                    fixture.Start();
                    fixture.Scan();
                    TaskCompletionSource<HttpResponseMessage> response = new(TaskCreationOptions.RunContinuationsAsynchronously);
                    fixture.Handler.StartOperation = _ => response.Task;
                    ParsedSubscription prior = fixture.Store.Subscription;
                    SubscriptionSendEventArgs args = type == ParsedSubscription.NotificationTypeCodes.EventNotification
                        ? Assert.Single(fixture.Store.Sent) : fixture.Store.Args(type);
                    Task<Outcome> delivery = fixture.Notify(args);
                    Dictionary<object, long> schedule;
                    try
                    {
                        Assert.Equal(1, fixture.Handler.Starts);
                        fixture.Revoke(change, requested: handshake);
                        fixture.Scan();
                        schedule = fixture.HeartbeatSchedule();
                    }
                    finally
                    {
                        response.TrySetResult(new HttpResponseMessage(failed ? HttpStatusCode.InternalServerError : HttpStatusCode.NoContent));
                    }
                    Assert.Equal(OutcomeKind.Suppressed, (await delivery.WaitAsync(TimeSpan.FromSeconds(10))).Kind);
                    Assert.Equal(0, prior.LastCommunicationTicks);
                    Assert.Empty(prior.SerializedEvents);
                    Assert.Empty(prior.NotificationErrors);
                    Assert.Equal(0, fixture.Store.Subscription.LastCommunicationTicks);
                    Assert.Empty(fixture.Store.Subscription.SerializedEvents);
                    Assert.Empty(fixture.Store.Subscription.NotificationErrors);
                    Assert.Equal(change == "expire" ? "off" : handshake ? "requested" : "active", fixture.Store.Subscription.CurrentStatus);
                    Assert.Equal(schedule, fixture.HeartbeatSchedule());
                    Assert.Empty(fixture.Log.Failures);
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(Versions))]
    public async Task EmailRechecksAfterConnectAndAuthentication(FhirReleases.FhirSequenceCodes version)
    {
        foreach (string heldStage in new[] { "connect", "authenticate", "send", "none" })
        {
            foreach (bool expire in new[] { false, true })
            {
                using NotificationFixture fixture = new(version, false);
                fixture.Email();
                NotificationManager manager = (NotificationManager)fixture.Service;
                TaskCompletionSource held = new(TaskCreationOptions.RunContinuationsAsynchronously);
                int starts = 0;
                int cleanups = 0;
                manager.SmtpConnect = (_, _, _, _) => heldStage == "connect" ? held.Task : Task.CompletedTask;
                manager.SmtpAuthenticate = (_, _, _, _) => heldStage == "authenticate" ? held.Task : Task.CompletedTask;
                manager.SmtpSend = (_, _, _) =>
                {
                    starts++;
                    return heldStage == "send" ? held.Task : Task.CompletedTask;
                };
                manager.SmtpDisconnect = (_, _) =>
                {
                    cleanups++;
                    return Task.CompletedTask;
                };
                Task<Outcome> delivery = fixture.Notify(fixture.Store.Args(ParsedSubscription.NotificationTypeCodes.Handshake));
                try
                {
                    if (heldStage != "none")
                    {
                        fixture.Revoke(expire ? "expire" : "off", true);
                    }
                }
                finally
                {
                    held.TrySetResult();
                }
                Outcome outcome = await delivery.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.Equal(heldStage is "send" or "none" ? 1 : 0, starts);
                Assert.Equal(1, cleanups);
                Assert.Equal(heldStage == "none" ? OutcomeKind.Delivered : OutcomeKind.Suppressed, outcome.Kind);
                Assert.Equal(heldStage == "none" ? "active" : "off", fixture.Store.Subscription.CurrentStatus);
                Assert.Equal(heldStage == "none" ? fixture.Store.Clock.GetUtcNow().UtcTicks : 0, fixture.Store.Subscription.LastCommunicationTicks);
            }
        }
    }

    [Theory]
    [MemberData(nameof(Versions))]
    public async Task ZulipRechecksEveryStreamAndPrivateRecipient(FhirReleases.FhirSequenceCodes version)
    {
        foreach (string mode in new[] { "stream", "private", "positive" })
        {
            using NotificationFixture fixture = new(version, false);
            ParsedSubscription subscription = fixture.Store.Subscription;
            subscription.ChannelCode = "zulip";
            subscription.Parameters = new()
            {
                ["site"] = ["https://zulip.unit.test"],
                ["email"] = ["bot@unit.test"],
                ["key"] = ["synthetic-local-only"],
                ["streamId"] = mode == "private" ? [] : ["1", "2"],
                ["userId"] = ["3", "4"],
            };
            NotificationManager manager = (NotificationManager)fixture.Service;
            TaskCompletionSource<(bool success, string? details, ulong messageId)> held = new(TaskCreationOptions.RunContinuationsAsynchronously);
            List<int> starts = [];
            Task<(bool success, string? details, ulong messageId)> Start(int id)
            {
                starts.Add(id);
                return mode != "positive" && starts.Count == 1
                    ? held.Task : Task.FromResult((true, (string?)null, 1UL));
            }
            manager.ZulipStream = (_, _, id) => Start(id);
            manager.ZulipPrivate = (_, _, id) => Start(id);
            Task<Outcome> delivery = fixture.Notify(fixture.Store.Args());
            try
            {
                if (mode != "positive")
                {
                    Assert.Single(starts);
                    fixture.Revoke("off");
                }
            }
            finally
            {
                held.TrySetResult((true, null, 1));
            }
            Outcome outcome = await delivery.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(mode == "positive" ? new[] { 1, 2, 3, 4 } : mode == "private" ? [3] : [1], starts);
            Assert.Equal(mode == "positive" ? OutcomeKind.Delivered : OutcomeKind.Suppressed, outcome.Kind);
        }
    }

    [Theory]
    [MemberData(nameof(Managers))]
    public async Task HeartbeatsUseUtcClockAndRevisionReservations(FhirReleases.FhirSequenceCodes version, bool restOnly)
    {
        using NotificationFixture fixture = new(version, restOnly);
        fixture.Start();
        fixture.Scan();
        Assert.Equal(0, fixture.QueuedCount);
        Assert.Equal(0, fixture.Store.Subscription.LastCommunicationTicks);
        fixture.Store.Clock.Advance(TimeSpan.FromSeconds(119));
        fixture.Scan();
        Assert.Equal(0, fixture.QueuedCount);
        fixture.Store.Clock.Advance(TimeSpan.FromSeconds(1));
        fixture.Scan();
        Assert.Equal(1, fixture.QueuedCount);
        fixture.Scan();
        Assert.Equal(1, fixture.QueuedCount);
        TaskCompletionSource<HttpResponseMessage> response = new(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Handler.StartOperation = _ => response.Task;
        Task delivery = fixture.DrainOneAsync();
        try
        {
            Assert.Equal(1, fixture.Handler.Starts);
            fixture.Store.Clock.Advance(TimeSpan.FromSeconds(120));
            fixture.Scan();
            Assert.Equal(0, fixture.QueuedCount);
            Assert.Equal(0, fixture.Store.Subscription.LastCommunicationTicks);
        }
        finally
        {
            response.TrySetResult(new HttpResponseMessage(HttpStatusCode.NoContent));
        }
        await delivery.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(fixture.Store.Clock.GetUtcNow().UtcTicks, fixture.Store.Subscription.LastCommunicationTicks);
        fixture.Scan();
        Assert.Equal(0, fixture.QueuedCount);
        fixture.Store.Clock.Advance(TimeSpan.FromSeconds(120));
        fixture.Scan();
        Assert.Equal(1, fixture.QueuedCount);
        response = new(TaskCreationOptions.RunContinuationsAsynchronously);
        delivery = fixture.DrainOneAsync();
        Dictionary<object, long> renewedSchedule;
        try
        {
            Assert.Equal(2, fixture.Handler.Starts);
            fixture.Revoke("cycle");
            fixture.Scan();
            renewedSchedule = fixture.HeartbeatSchedule();
        }
        finally
        {
            response.TrySetResult(new HttpResponseMessage(HttpStatusCode.NoContent));
        }
        await delivery.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(renewedSchedule, fixture.HeartbeatSchedule());
        fixture.Store.Clock.Advance(TimeSpan.FromSeconds(120));
        fixture.Scan();
        Assert.Equal(1, fixture.QueuedCount);
        fixture.Revoke("cycle");
        await fixture.DrainOneAsync();
        Assert.Equal(2, fixture.Handler.Starts);
        Assert.Empty(fixture.Log.Failures);
        fixture.Revoke("expire");
        fixture.Scan();
        Assert.Equal(0, fixture.QueuedCount);
    }

    [Theory]
    [MemberData(nameof(Managers))]
    public async Task SynchronousOutcomeAndPreparationFailureRespectLifecycle(FhirReleases.FhirSequenceCodes version, bool restOnly)
    {
        using NotificationFixture fixture = new(version, restOnly);
        SubscriptionSendEventArgs args = fixture.Store.Args();
        object lifecycle = fixture.Store.Field("SubscriptionLifecycleLock");
        fixture.Store.Store.OnSubscriptionsChanged += (_, _) => Assert.False(Monitor.IsEntered(lifecycle));
        Assert.True(fixture.Store.Store.TryDispatchSubscriptionNotification(args,
            () => Task.FromResult(new Outcome(OutcomeKind.Delivered, "immediate")), out Task<Outcome> immediate));
        Assert.True((await immediate).Succeeded);
        Assert.Equal(LifecycleTestStore.Now.UtcTicks, fixture.Store.Subscription.LastCommunicationTicks);
        Assert.True(fixture.Store.Store.TryDispatchSubscriptionNotification(args,
            () => throw new InvalidOperationException("synchronous start failure"), out Task<Outcome> thrown));
        Assert.Equal(OutcomeKind.Failed, (await thrown).Kind);
        Assert.Contains("synchronous start failure", fixture.Store.Subscription.NotificationErrors);

        fixture.Store.PutSubscription();
        fixture.Store.Subscription.Endpoint = "not an absolute uri";
        Outcome invalid = await fixture.Notify(fixture.Store.Args());
        Assert.Equal(OutcomeKind.Failed, invalid.Kind);
        Assert.Equal(0, fixture.Handler.Starts);
        fixture.Store.PutSubscription("requested");
        fixture.Store.Subscription.Endpoint = "https://example.org/receiver";
        Outcome simulated = await fixture.Notify(fixture.Store.Args(ParsedSubscription.NotificationTypeCodes.Handshake));
        Assert.Equal(OutcomeKind.Simulated, simulated.Kind);
        Assert.Equal(0, fixture.Handler.Starts);
        Assert.Equal(0, fixture.Store.Subscription.LastCommunicationTicks);
        Assert.Equal("active", fixture.Store.Subscription.CurrentStatus);
        fixture.Revoke("off");
        Assert.Equal(OutcomeKind.Suppressed, (await fixture.Notify(fixture.Store.Args())).Kind);
    }

    [Theory]
    [MemberData(nameof(Managers))]
    public async Task ManagerStopPreservesInFlightDrainAndTimerOwnership(FhirReleases.FhirSequenceCodes version, bool restOnly)
    {
        using NotificationFixture fixture = new(version, restOnly);
        fixture.Start();
        if (restOnly)
        {
            await ((IHostedService)fixture.Service).StopAsync(CancellationToken.None);
            int starts = fixture.Handler.Starts;
            fixture.Store.Clock.Advance(TimeSpan.FromMinutes(1));
            fixture.Store.Clock.RunDueCallbacks();
            fixture.Store.Match("stopped");
            fixture.RunQueueTimer();
            Assert.Equal(starts, fixture.Handler.Starts);
            Assert.Equal(0, fixture.QueuedCount);
            return;
        }

        TaskCompletionSource<HttpResponseMessage> first = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<HttpResponseMessage> second = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Queue<Task<HttpResponseMessage>> held = new([first.Task, second.Task]);
        fixture.Handler.StartOperation = _ => held.Dequeue();
        fixture.Store.Match("first");
        fixture.Store.Match("second");
        fixture.RunQueueTimer();
        Assert.Equal(2, fixture.Handler.Starts);
        Task stop = ((IHostedService)fixture.Service).StopAsync(CancellationToken.None);
        try
        {
            Assert.False(stop.IsCompleted);
            using CancellationTokenSource canceled = new();
            canceled.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ((IHostedService)fixture.Service).StopAsync(canceled.Token));
            Assert.False(fixture.Handler.Disposed);
        }
        finally
        {
            first.TrySetResult(new HttpResponseMessage(HttpStatusCode.NoContent));
            second.TrySetResult(new HttpResponseMessage(HttpStatusCode.NoContent));
        }
        await stop.WaitAsync(TimeSpan.FromSeconds(10));
        await ((IHostedService)fixture.Service).StopAsync(CancellationToken.None);
        fixture.Service.Dispose();
        // The injected client is caller-owned and remains usable after manager teardown.
        Assert.False(fixture.Handler.Disposed);
        fixture.Handler.StartOperation = _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
        using HttpResponseMessage probe = await fixture.Client.GetAsync("https://ownership.unit.test");
        Assert.Equal(HttpStatusCode.NoContent, probe.StatusCode);
    }

    private sealed class NotificationFixture : IDisposable
    {
        internal LifecycleTestStore Store { get; }
        internal INotificationManager Service { get; }
        internal RecordingHandler Handler { get; } = new();
        internal HttpClient Client { get; }
        internal RecordingLog Log { get; } = new();
        private readonly bool _restOnly;
        internal NotificationFixture(FhirReleases.FhirSequenceCodes version, bool restOnly)
        {
            _restOnly = restOnly;
            Store = new(version);
            Store.Seed();
            Client = new(Handler);
            TestStores stores = new(Store.Store);
            Dictionary<string, TenantConfiguration> tenants = new() { [Store.Store.Config.ControllerName] = Store.Store.Config };
            CandleConfig config = new()
            {
                SmtpHost = "smtp.unit.test",
                SmtpPort = 465,
                SmtpUser = "sender@unit.test",
                SmtpPassword = "synthetic-only",
            };
            Service = restOnly
                ? new RestOnlyNotificationManager(config, tenants, stores, Log, Store.Clock, Client)
                : new NotificationManager(config, tenants, stores, Log, Store.Clock, Client);
        }
        internal void Start() => ((IHostedService)Service).StartAsync(CancellationToken.None).GetAwaiter().GetResult();
        internal void Email()
        {
            Store.PutSubscription("requested");
            Store.Subscription.ChannelCode = "email";
            Store.Subscription.Endpoint = "recipient@unit.test";
        }
        internal Task<Outcome> Notify(SubscriptionSendEventArgs args)
        {
            MethodInfo method = Service.GetType().GetMethod("TryNotify", BindingFlags.Instance | BindingFlags.NonPublic)!;
            return (Task<Outcome>)method.Invoke(Service, _restOnly ? [Store.Store, args] : [Store.Store, args, CancellationToken.None])!;
        }
        internal void Scan() => Invoke("CheckAndSendHeartbeats", [null]);
        internal void RunQueueTimer() => Invoke("CheckNotificationQ", [null]);
        private object Queue => Service.GetType().GetField("_notificationRequestQ", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Service)!;
        internal int QueuedCount => ((IEnumerable)Queue).Cast<object>().Count();
        internal int HandshakeRevisionCount => ((IDictionary)Service.GetType()
            .GetField("_handshakeRevisions", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Service)!).Count;
        internal void Publish(SubscriptionChangedEventArgs change) => Store.Store.GetType()
            .GetMethod("PublishSubscriptionChanges", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(Store.Store, [new[] { change }]);
        internal Dictionary<object, long> HeartbeatSchedule()
        {
            IDictionary reservations = (IDictionary)Service.GetType()
                .GetField("_heartbeatReservations", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Service)!;
            return reservations.Keys.Cast<object>().ToDictionary(key => key, key => (long)reservations[key]!);
        }
        internal Task DrainOneAsync()
        {
            // Run exactly the same per-request method as the timer, but retain its task for rendezvous.
            object?[] arguments = [null];
            Assert.True((bool)Queue.GetType().GetMethod("TryDequeue")!.Invoke(Queue, arguments)!);
            object request = arguments[0]!;
            object args = request.GetType().GetProperty("Args")!.GetValue(request)!;
            return (Task)Invoke("DeliverAsync", _restOnly ? [Store.Store, args] : [request])!;
        }
        private object? Invoke(string method, object?[] arguments) =>
            Service.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(Service, arguments);
        internal void Revoke(string change, bool requested = false)
        {
            switch (change)
            {
                case "off": Store.Store.ChangeSubscriptionStatus(LifecycleTestStore.Id, "off"); break;
                case "cycle":
                    Store.Store.ChangeSubscriptionStatus(LifecycleTestStore.Id, "off");
                    Store.Store.ChangeSubscriptionStatus(LifecycleTestStore.Id, requested ? "requested" : "active");
                    break;
                case "replace": Store.PutSubscription(requested ? "requested" : "active"); break;
                case "delete": Store.Delete(); break;
                case "recreate":
                    Store.Delete();
                    Store.PutSubscription(requested ? "requested" : "active");
                    break;
                case "expire": Store.Clock.Advance(TimeSpan.FromMinutes(31)); break;
                default: throw new ArgumentOutOfRangeException(nameof(change));
            }
        }
        public void Dispose()
        {
            Service.Dispose();
            Client.Dispose();
            Store.Dispose();
        }
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        internal Func<CancellationToken, Task<HttpResponseMessage>> StartOperation { get; set; } =
            _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
        private int _starts;
        internal int Starts => Volatile.Read(ref _starts);
        internal bool Disposed { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            ObjectDisposedException.ThrowIf(Disposed, this);
            Interlocked.Increment(ref _starts);
            return StartOperation(cancellationToken);
        }
        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }

    private sealed class RecordingLog : ILogger<NotificationManager>, ILogger<RestOnlyNotificationManager>
    {
        internal ConcurrentQueue<string> Failures { get; } = new();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel >= LogLevel.Warning)
            {
                Failures.Enqueue(formatter(state, exception));
            }
        }
    }

    private sealed class TestStores : Dictionary<string, IFhirStore>, IFhirStoreManager
    {
        internal TestStores(IFhirStore store) => Add(store.Config.ControllerName, store);
        public event EventHandler<EventArgs>? OnChanged;
        public IReadOnlyDictionary<string, IQueryable<PackagePageInfo>> AdditionalPagesByTenant { get; } =
            new Dictionary<string, IQueryable<PackagePageInfo>>();
        public void Init() { }
        public void LoadRiContents(string dir) { }
        public Task LoadRequestedPackages(string supplementalRoot, bool loadExamples) => Task.CompletedTask;
        public void StateHasChanged() => OnChanged?.Invoke(this, EventArgs.Empty);
        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
