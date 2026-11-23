// <copyright file="SubscriptionNotificationLifecycleTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using System.Collections;
using System.Collections.Concurrent;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using fhir.candle.Services;
using fhir.candle.Tests.Infrastructure;
using FhirCandle.Configuration;
using FhirCandle.Models;
using FhirCandle.Storage;
using FhirCandle.Utils;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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
                        await fixture.Handler.WaitForStartAsync();
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
            await fixture.Handler.WaitForStartAsync();
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
    [MemberData(nameof(Managers))]
    public async Task CurrentRevisionRestFailuresRemainFailures(FhirReleases.FhirSequenceCodes version, bool restOnly)
    {
        const string HandlerFailure = "held current REST handler fault";
        const string StatusFailure = "REST POST to https://receiver.unit.test/notify failed: InternalServerError";
        foreach (bool queued in new[] { false, true })
        {
            ParsedSubscription.NotificationTypeCodes[] types = queued
                ? [ParsedSubscription.NotificationTypeCodes.Handshake]
                : [ParsedSubscription.NotificationTypeCodes.Handshake, ParsedSubscription.NotificationTypeCodes.Heartbeat,
                    ParsedSubscription.NotificationTypeCodes.EventNotification];
            foreach (ParsedSubscription.NotificationTypeCodes type in types)
            {
                foreach (string result in new[] { "http-500", "fault", "success" })
                {
                    using NotificationFixture fixture = new(version, restOnly);
                    bool handshake = type == ParsedSubscription.NotificationTypeCodes.Handshake;
                    string status = handshake ? "requested" : "active";
                    if (handshake)
                    {
                        fixture.Store.PutSubscription(status);
                    }
                    else if (type == ParsedSubscription.NotificationTypeCodes.EventNotification)
                    {
                        fixture.Store.Match("current-rest-event");
                    }
                    ParsedSubscription subscription = fixture.Store.Subscription;
                    long revision = subscription.Revision;
                    long priorCommunication = LifecycleTestStore.Now.AddMinutes(-1).UtcTicks;
                    subscription.LastCommunicationTicks = priorCommunication;
                    TaskCompletionSource reached = new(TaskCreationOptions.RunContinuationsAsynchronously);
                    TaskCompletionSource<HttpResponseMessage> response = new(TaskCreationOptions.RunContinuationsAsynchronously);
                    fixture.Handler.StartOperation = _ =>
                    {
                        reached.SetResult();
                        return response.Task;
                    };
                    Task<Outcome>? direct = null;
                    Task delivery;
                    if (queued)
                    {
                        fixture.Start();
                        Assert.Equal(1, fixture.QueuedCount);
                        delivery = fixture.DrainOneAsync();
                    }
                    else
                    {
                        SubscriptionSendEventArgs args = type == ParsedSubscription.NotificationTypeCodes.EventNotification
                            ? Assert.Single(fixture.Store.Sent) : fixture.Store.Args(type);
                        direct = fixture.Notify(args);
                        delivery = direct;
                    }
                    try
                    {
                        await reached.Task.WaitAsync(TimeSpan.FromSeconds(10));
                        Assert.Equal(1, fixture.Handler.Starts);
                        Assert.False(response.Task.IsCompleted);
                        Assert.False(delivery.IsCompleted);
                        Assert.Equal(priorCommunication, subscription.LastCommunicationTicks);
                        Assert.Equal(revision, subscription.Revision);
                        Assert.Equal(status, subscription.CurrentStatus);
                        Assert.Empty(subscription.SerializedEvents);
                        Assert.Empty(subscription.NotificationErrors);
                        Assert.Empty(fixture.Log.Failures);
                        fixture.Store.Clock.Advance(TimeSpan.FromSeconds(1));
                    }
                    finally
                    {
                        if (result == "fault")
                        {
                            response.TrySetException(new HttpRequestException(HandlerFailure));
                        }
                        else
                        {
                            response.TrySetResult(new HttpResponseMessage(result == "http-500"
                                ? HttpStatusCode.InternalServerError : HttpStatusCode.NoContent));
                        }
                        await delivery.WaitAsync(TimeSpan.FromSeconds(10));
                    }
                    bool succeeded = result == "success";
                    string? expectedError = succeeded ? null : result == "fault" ? HandlerFailure : StatusFailure;
                    if (direct is not null)
                    {
                        Outcome outcome = await direct;
                        Assert.Equal(succeeded ? OutcomeKind.Delivered : OutcomeKind.Failed, outcome.Kind);
                        Assert.Equal(succeeded, outcome.Succeeded);
                        Assert.Equal(expectedError, outcome.Error);
                        if (succeeded)
                        {
                            Assert.Equal(Assert.Single(subscription.SerializedEvents).Value, outcome.SerializedContent);
                            Assert.False(string.IsNullOrEmpty(outcome.SerializedContent));
                        }
                        else
                        {
                            Assert.Null(outcome.SerializedContent);
                        }
                    }
                    Assert.Equal(1, fixture.Handler.Starts);
                    Assert.Same(subscription, fixture.Store.Subscription);
                    Assert.Equal(succeeded ? fixture.Store.Clock.GetUtcNow().UtcTicks : priorCommunication, subscription.LastCommunicationTicks);
                    if (succeeded)
                    {
                        Assert.Empty(subscription.NotificationErrors);
                        Assert.False(string.IsNullOrEmpty(Assert.Single(subscription.SerializedEvents).Value));
                    }
                    else
                    {
                        Assert.Empty(subscription.SerializedEvents);
                        Assert.Equal(expectedError, Assert.Single(subscription.NotificationErrors));
                    }
                    if (succeeded && handshake)
                    {
                        Assert.NotEqual(revision, subscription.Revision);
                    }
                    else
                    {
                        Assert.Equal(revision, subscription.Revision);
                    }
                    fixture.Store.AssertViews(succeeded && handshake ? "active" : status);
                    if (queued && !succeeded)
                    {
                        Assert.Equal((LogLevel.Warning, $"Notification delivery failed for Subscription/{LifecycleTestStore.Id}"),
                            Assert.Single(fixture.Log.Entries, entry => entry.Level >= LogLevel.Warning));
                        Assert.Single(fixture.Log.Failures);
                    }
                    else
                    {
                        Assert.Empty(fixture.Log.Failures);
                    }
                    Assert.Equal(0, fixture.QueuedCount);
                    if (queued)
                    {
                        Assert.Equal(succeeded ? 0 : 1, fixture.HandshakeRevisionCount);
                    }
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(Versions))]
    public async Task CurrentRevisionSmtpAuthenticationFailureDoesNotSend(FhirReleases.FhirSequenceCodes version)
    {
        const string AuthenticationFailure = "held current SMTP authentication rejected";
        const string ExpectedError = "Email Handshake to recipient@unit.test failed: " + AuthenticationFailure;
        foreach (bool queued in new[] { false, true })
        {
            using NotificationFixture fixture = new(version, false);
            fixture.Email();
            ParsedSubscription subscription = fixture.Store.Subscription;
            long revision = subscription.Revision;
            long priorCommunication = LifecycleTestStore.Now.AddMinutes(-1).UtcTicks;
            subscription.LastCommunicationTicks = priorCommunication;
            NotificationManager manager = (NotificationManager)fixture.Service;
            TaskCompletionSource reached = new(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource authentication = new(TaskCreationOptions.RunContinuationsAsynchronously);
            int connects = 0;
            int authentications = 0;
            int sends = 0;
            int cleanups = 0;
            manager.SmtpConnect = (_, _, _, _) =>
            {
                connects++;
                return Task.CompletedTask;
            };
            manager.SmtpAuthenticate = (_, _, _, _) =>
            {
                authentications++;
                reached.SetResult();
                return authentication.Task;
            };
            manager.SmtpSend = (_, _, _) =>
            {
                sends++;
                return Task.CompletedTask;
            };
            manager.SmtpDisconnect = (_, _) =>
            {
                cleanups++;
                return Task.CompletedTask;
            };
            Task<Outcome>? direct = null;
            Task delivery;
            if (queued)
            {
                fixture.Start();
                Assert.Equal(1, fixture.QueuedCount);
                delivery = fixture.DrainOneAsync();
            }
            else
            {
                direct = fixture.Notify(fixture.Store.Args(ParsedSubscription.NotificationTypeCodes.Handshake));
                delivery = direct;
            }
            try
            {
                await reached.Task.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.False(authentication.Task.IsCompleted);
                Assert.False(delivery.IsCompleted);
                Assert.Equal(1, connects);
                Assert.Equal(1, authentications);
                Assert.Equal(0, sends);
                Assert.Equal(0, cleanups);
                Assert.Equal(priorCommunication, subscription.LastCommunicationTicks);
                Assert.Equal(revision, subscription.Revision);
                Assert.Equal("requested", subscription.CurrentStatus);
                Assert.Empty(subscription.SerializedEvents);
                Assert.Empty(subscription.NotificationErrors);
                Assert.Empty(fixture.Log.Failures);
                fixture.Store.Clock.Advance(TimeSpan.FromSeconds(1));
            }
            finally
            {
                authentication.TrySetException(new MailKit.Security.AuthenticationException(AuthenticationFailure));
                await delivery.WaitAsync(TimeSpan.FromSeconds(10));
            }
            if (direct is not null)
            {
                Outcome outcome = await direct;
                Assert.Equal(OutcomeKind.Failed, outcome.Kind);
                Assert.False(outcome.Succeeded);
                Assert.Equal(ExpectedError, outcome.Error);
                Assert.Null(outcome.SerializedContent);
            }
            Assert.Equal(1, connects);
            Assert.Equal(1, authentications);
            Assert.Equal(0, sends);
            Assert.Equal(1, cleanups);
            Assert.Equal(0, fixture.Handler.Starts);
            Assert.Same(subscription, fixture.Store.Subscription);
            Assert.Equal(revision, subscription.Revision);
            Assert.Equal(priorCommunication, subscription.LastCommunicationTicks);
            Assert.Empty(subscription.SerializedEvents);
            Assert.Equal(ExpectedError, Assert.Single(subscription.NotificationErrors));
            fixture.Store.AssertViews("requested");
            (LogLevel Level, string Message) error = (LogLevel.Error, $"Email notification delivery failed for Subscription/{LifecycleTestStore.Id}");
            (LogLevel Level, string Message) warning = (LogLevel.Warning, $"Notification delivery failed for Subscription/{LifecycleTestStore.Id}");
            Assert.Equal(queued ? new[] { error, warning } : [error],
                fixture.Log.Entries.Where(entry => entry.Level >= LogLevel.Warning));
            Assert.Equal(queued ? 2 : 1, fixture.Log.Failures.Count);
            Assert.Equal(0, fixture.QueuedCount);
            Assert.Equal(queued ? 1 : 0, fixture.HandshakeRevisionCount);
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
    public async Task ZulipCredentialsStayWithApprovedSender(FhirReleases.FhirSequenceCodes version)
    {
        const string Approved = "https://operator.unit.test/chat";
        const string Independent = "https://subscription.unit.test:8443/other";
        ZulipHandler handler = new();
        using NotificationFixture fixture = new(version, false, handler);
        fixture.Zulip();

        async Task Check(string? site, string? email, string? key, string? label, string? route,
            bool stream = true, bool redirect = false)
        {
            Dictionary<string, List<string>> parameters = new()
            {
                [stream ? "streamId" : "userId"] = ["42"],
            };
            if (site is not null) { parameters["site"] = [site]; }
            if (email is not null) { parameters["email"] = [email]; }
            if (key is not null) { parameters["key"] = [key]; }
            fixture.Store.Subscription.Parameters = parameters;
            handler.Stream = stream;
            handler.Redirect = redirect;
            handler.Calls.Clear();
            Outcome result = await fixture.Notify(fixture.Store.Args());
            if (label is null)
            {
                Assert.Empty(handler.Calls);
                Assert.Equal(OutcomeKind.Failed, result.Kind);
                return;
            }
            Assert.Equal((label, route + "/api/v1/messages"), Assert.Single(handler.Calls));
            Assert.Equal(redirect ? OutcomeKind.Failed : OutcomeKind.Delivered, result.Kind);
        }

        await Check(null, null, null, "operator", Approved);
        await Check("HTTPS://OPERATOR.UNIT.TEST:443/chat/", null, null, "operator", Approved, stream: false);
        foreach (string invalid in new[] { Independent, "https://operator.unit.test:8443/chat", Approved + "/other",
            "https://operator.unit.test/Chat", "http://operator.unit.test/chat", "https://user@operator.unit.test/chat",
            Approved + "?query", Approved + "#fragment", Approved + "/.", Approved + "//", Approved + "/%2e" })
        {
            await Check(invalid, null, null, null, null);
        }
        await Check(null, "subscriber", "synthetic-sub", null, null);
        await Check(Approved, "subscriber", null, null, null);
        await Check(Approved, null, "synthetic-sub", null, null);
        await Check(Approved, "subscriber", string.Empty, null, null);
        await Check("http://subscription.unit.test", "subscriber", "synthetic-sub", null, null);
        await Check(Independent, "subscriber", "synthetic-sub", "subscription", Independent, stream: false);
        await Check(null, null, null, "operator", Approved, redirect: true);
    }

    private sealed class ZulipHandler : HttpMessageHandler
    {
        internal List<(string Label, string Destination)> Calls { get; } = [];
        internal bool Stream { get; set; }
        internal bool Redirect { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string? header = request.Headers.Authorization?.Parameter;
            string label = header == Basic("operator:synthetic-op") ? "operator" :
                header == Basic("subscriber:synthetic-sub") ? "subscription" : "unknown";
            Calls.Add((label, request.RequestUri!.AbsoluteUri));
            Assert.Equal("Basic", request.Headers.Authorization?.Scheme);
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("application/x-www-form-urlencoded", request.Content!.Headers.ContentType?.MediaType);
            System.Collections.Specialized.NameValueCollection fields = System.Web.HttpUtility.ParseQueryString(
                await request.Content.ReadAsStringAsync(cancellationToken));
            Assert.Equal(Stream ? "channel" : "direct", fields["type"]);
            Assert.Equal(Stream ? "Subscription Notification" : null, fields["topic"]);
            Assert.Equal("[42]", fields["to"]);
            Assert.False(string.IsNullOrEmpty(fields["content"]));
            if (Redirect)
            {
                return new(HttpStatusCode.TemporaryRedirect) { Headers = { Location = new("https://redirect.unit.test/") } };
            }
            return new(HttpStatusCode.OK) { Content = new StringContent("""{"result":"success","id":17}""") };
        }

        private static string Basic(string synthetic) => Convert.ToBase64String(Encoding.UTF8.GetBytes(synthetic));
    }

    [Theory]
    [MemberData(nameof(Versions))]
    public async Task ZulipOrdinaryFailureContinuesEligibleRecipients(FhirReleases.FhirSequenceCodes version)
    {
        foreach (bool failPrivate in new[] { false, true })
        {
            using NotificationFixture fixture = new(version, false);
            fixture.Zulip(requested: true);
            ParsedSubscription subscription = fixture.Store.Subscription;
            long revision = subscription.Revision;
            NotificationManager manager = (NotificationManager)fixture.Service;
            TaskCompletionSource<(bool success, string? details, ulong messageId)>[] results = Enumerable.Range(1, 4)
                .Select(_ => new TaskCompletionSource<(bool success, string? details, ulong messageId)>(TaskCreationOptions.RunContinuationsAsynchronously)).ToArray();
            TaskCompletionSource[] reached = Enumerable.Range(1, 4)
                .Select(_ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)).ToArray();
            List<int> starts = [];
            Task<(bool success, string? details, ulong messageId)> Start(int id)
            {
                starts.Add(id);
                reached[id - 1].SetResult();
                return results[id - 1].Task;
            }
            manager.ZulipSend = (_, _, id, _, _) => Start(id);
            Task<Outcome> delivery = fixture.Notify(fixture.Store.Args(ParsedSubscription.NotificationTypeCodes.Handshake));
            long expectedCommunication = 0;
            List<string> errors = [];
            try
            {
                for (int id = 1; id <= 4; id++)
                {
                    await reached[id - 1].Task.WaitAsync(TimeSpan.FromSeconds(10));
                    Assert.Equal(Enumerable.Range(1, id), starts);
                    Assert.False(delivery.IsCompleted);
                    Assert.Equal(expectedCommunication, subscription.LastCommunicationTicks);
                    Assert.Equal(errors, subscription.NotificationErrors);
                    if (expectedCommunication == 0)
                    {
                        Assert.Empty(subscription.SerializedEvents);
                    }
                    fixture.Store.Clock.Advance(TimeSpan.FromSeconds(1));
                    string? error = id == 1 ? "stream denied" : id == 3 && failPrivate ? "private denied" : null;
                    if (error is null)
                    {
                        expectedCommunication = fixture.Store.Clock.GetUtcNow().UtcTicks;
                    }
                    else
                    {
                        errors.Add(error);
                    }
                    results[id - 1].SetResult((error is null, error, error is null ? (ulong)id : 0));
                }
            }
            finally
            {
                foreach (TaskCompletionSource<(bool success, string? details, ulong messageId)> result in results)
                {
                    result.TrySetResult((true, null, 1));
                }
                await delivery.WaitAsync(TimeSpan.FromSeconds(10));
            }
            Outcome outcome = await delivery;
            Assert.Equal(new[] { 1, 2, 3, 4 }, starts);
            Assert.Equal(OutcomeKind.Failed, outcome.Kind);
            Assert.False(outcome.Succeeded);
            Assert.Equal(failPrivate ? "stream denied" + Environment.NewLine + "private denied" : "stream denied", outcome.Error);
            Assert.Null(outcome.SerializedContent);
            Assert.Equal(errors, subscription.NotificationErrors);
            Assert.Single(subscription.SerializedEvents);
            Assert.Equal(expectedCommunication, subscription.LastCommunicationTicks);
            Assert.Equal(revision, subscription.Revision);
            fixture.Store.AssertViews("requested");
            Assert.Empty(fixture.Log.Failures);
        }
    }

    [Theory]
    [MemberData(nameof(Versions))]
    public async Task CurrentRevisionZulipFailuresRemainFailures(FhirReleases.FhirSequenceCodes version)
    {
        foreach (bool queued in new[] { false, true })
        {
            using NotificationFixture fixture = new(version, false);
            fixture.Zulip(requested: true);
            ParsedSubscription subscription = fixture.Store.Subscription;
            long revision = subscription.Revision;
            long priorCommunication = LifecycleTestStore.Now.AddMinutes(-1).UtcTicks;
            subscription.LastCommunicationTicks = priorCommunication;
            NotificationManager manager = (NotificationManager)fixture.Service;
            string[] errors = ["stream denied", "second stream denied", "private denied", "second private denied"];
            TaskCompletionSource<(bool success, string? details, ulong messageId)>[] results = Enumerable.Range(1, 4)
                .Select(_ => new TaskCompletionSource<(bool success, string? details, ulong messageId)>(TaskCreationOptions.RunContinuationsAsynchronously)).ToArray();
            TaskCompletionSource[] reached = Enumerable.Range(1, 4)
                .Select(_ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)).ToArray();
            List<int> starts = [];
            Task<(bool success, string? details, ulong messageId)> Start(int id)
            {
                starts.Add(id);
                reached[id - 1].SetResult();
                return results[id - 1].Task;
            }
            manager.ZulipSend = (_, _, id, _, _) => Start(id);
            Task<Outcome>? direct = null;
            Task delivery;
            if (queued)
            {
                fixture.Start();
                Assert.Equal(1, fixture.QueuedCount);
                delivery = fixture.DrainOneAsync();
            }
            else
            {
                direct = fixture.Notify(fixture.Store.Args(ParsedSubscription.NotificationTypeCodes.Handshake));
                delivery = direct;
            }
            try
            {
                for (int id = 1; id <= 4; id++)
                {
                    await reached[id - 1].Task.WaitAsync(TimeSpan.FromSeconds(10));
                    Assert.Equal(Enumerable.Range(1, id), starts);
                    Assert.False(delivery.IsCompleted);
                    Assert.Equal(errors.Take(id - 1), subscription.NotificationErrors);
                    Assert.Empty(subscription.SerializedEvents);
                    Assert.Equal(priorCommunication, subscription.LastCommunicationTicks);
                    Assert.Empty(fixture.Log.Failures);
                    results[id - 1].SetResult((false, errors[id - 1], 0));
                }
            }
            finally
            {
                for (int index = 0; index < results.Length; index++)
                {
                    results[index].TrySetResult((false, errors[index], 0));
                }
                await delivery.WaitAsync(TimeSpan.FromSeconds(10));
            }
            if (direct is not null)
            {
                Outcome outcome = await direct;
                Assert.Equal(OutcomeKind.Failed, outcome.Kind);
                Assert.False(outcome.Succeeded);
                Assert.Equal(string.Join(Environment.NewLine, errors), outcome.Error);
                Assert.Null(outcome.SerializedContent);
            }
            Assert.Equal(new[] { 1, 2, 3, 4 }, starts);
            Assert.Equal(errors, subscription.NotificationErrors);
            Assert.Empty(subscription.SerializedEvents);
            Assert.Equal(priorCommunication, subscription.LastCommunicationTicks);
            Assert.Equal(revision, subscription.Revision);
            fixture.Store.AssertViews("requested");
            if (queued)
            {
                Assert.Equal((LogLevel.Warning, $"Notification delivery failed for Subscription/{LifecycleTestStore.Id}"),
                    Assert.Single(fixture.Log.Entries, entry => entry.Level >= LogLevel.Warning));
                Assert.Single(fixture.Log.Failures);
                Assert.Equal(0, fixture.QueuedCount);
            }
            else
            {
                Assert.Empty(fixture.Log.Failures);
            }
        }
    }

    [Theory]
    [MemberData(nameof(Versions))]
    public async Task ZulipRechecksEveryStreamAndPrivateRecipient(FhirReleases.FhirSequenceCodes version)
    {
        foreach (string mode in new[] { "stream", "private", "positive" })
        {
            foreach (string change in mode == "positive" ? new[] { "none" } : ["off", "expire", "cancel", "failed-off", "failed-expire", "failed-cancel"])
            {
                using NotificationFixture fixture = new(version, false);
                fixture.Zulip();
                ParsedSubscription subscription = fixture.Store.Subscription;
                if (mode == "private")
                {
                    subscription.Parameters["streamId"] = [];
                }
                NotificationManager manager = (NotificationManager)fixture.Service;
                TaskCompletionSource<(bool success, string? details, ulong messageId)> held = new(TaskCreationOptions.RunContinuationsAsynchronously);
                List<int> starts = [];
                Task<(bool success, string? details, ulong messageId)> Start(int id)
                {
                    starts.Add(id);
                    return mode != "positive" && starts.Count == 1
                        ? held.Task : Task.FromResult((true, (string?)null, 1UL));
                }
                manager.ZulipSend = (_, _, id, _, _) => Start(id);
                using CancellationTokenSource cancellation = new();
                bool canceled = change.EndsWith("cancel", StringComparison.Ordinal);
                bool failed = change.StartsWith("failed-", StringComparison.Ordinal);
                Task<Outcome> delivery = fixture.Notify(fixture.Store.Args(), cancellation.Token);
                try
                {
                    if (mode != "positive")
                    {
                        Assert.Single(starts);
                        if (canceled)
                        {
                            cancellation.Cancel();
                        }
                        else
                        {
                            fixture.Revoke(change.Replace("failed-", string.Empty, StringComparison.Ordinal));
                        }
                        Assert.False(delivery.IsCompleted);
                    }
                }
                finally
                {
                    if (canceled)
                    {
                        cancellation.Cancel();
                    }
                    held.TrySetResult((!failed, failed ? "recipient denied" : null, failed ? 0UL : 1UL));
                    if (canceled)
                    {
                        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => delivery.WaitAsync(TimeSpan.FromSeconds(10)));
                    }
                    else
                    {
                        await delivery.WaitAsync(TimeSpan.FromSeconds(10));
                    }
                }
                Assert.Equal(mode == "positive" ? new[] { 1, 2, 3, 4 } : mode == "private" ? [3] : [1], starts);
                if (!canceled)
                {
                    Assert.Equal(mode == "positive" ? OutcomeKind.Delivered : OutcomeKind.Suppressed, (await delivery).Kind);
                }
                bool delivered = mode == "positive" || (canceled && !failed);
                Assert.Equal(delivered ? fixture.Store.Clock.GetUtcNow().UtcTicks : 0, subscription.LastCommunicationTicks);
                Assert.Equal(canceled && failed ? new[] { "recipient denied" } : [], subscription.NotificationErrors);
                if (delivered)
                {
                    Assert.Single(subscription.SerializedEvents);
                }
                else
                {
                    Assert.Empty(subscription.SerializedEvents);
                }
                Assert.Empty(fixture.Log.Failures);
            }
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
            await fixture.Handler.WaitForStartAsync();
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
            await fixture.Handler.WaitForStartAsync();
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
    [MemberData(nameof(Versions))]
    public async Task BindingParameterDiagnosticsDoNotHoldStoreLocks(FhirReleases.FhirSequenceCodes version)
    {
        string[] parameters = ["missing-binding-parameter", "missing-binding-parameter-second", "missing-binding-parameter"];
        string[] expected = parameters.Select(parameter =>
            $"Cannot apply filter with no search parameter definition Encounter?{parameter}").ToArray();
        foreach (string entry in new[] { "post", "put-create", "put-replace", "reprocess", "topic-reprocess", "topic-replace", "reactivate", "handshake" })
        {
            await AssertBindingDiagnosticsUnlockedAsync(version, expected, [string.Empty],
                preparationEmits: entry is "reprocess" or "topic-reprocess" or "topic-replace",
                fixture =>
                {
                    fixture.Seed(subscription: false);
                    if (entry == "post")
                    {
                        JsonObject resource = fixture.SubscriptionWithBindingParameters("active", parameters);
                        return () =>
                        {
                            (bool success, FhirResponseContext response) = fixture.Interact("POST", "Subscription", resource);
                            Assert.True(success, response.SerializedOutcome);
                            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
                        };
                    }
                    if (entry == "put-create")
                    {
                        return () =>
                        {
                            (bool success, FhirResponseContext response) = fixture.Interact("PUT", "Subscription/" + LifecycleTestStore.Id,
                                fixture.SubscriptionWithBindingParameters("active", parameters));
                            Assert.True(success, response.SerializedOutcome);
                            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
                        };
                    }
                    if (entry == "put-replace")
                    {
                        fixture.PutSubscription();
                        return () =>
                        {
                            (bool success, FhirResponseContext response) = fixture.Interact("PUT", "Subscription/" + LifecycleTestStore.Id,
                                fixture.SubscriptionWithBindingParameters("active", parameters));
                            Assert.True(success, response.SerializedOutcome);
                            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                        };
                    }

                    string status = entry == "reactivate" ? "off" : entry == "handshake" ? "requested" : "active";
                    fixture.Put(fixture.SubscriptionWithBindingParameters(status, parameters));
                    ParsedSubscription subscription = fixture.Subscription;
                    if (status != "active")
                    {
                        Assert.Null(fixture.ExecutableRevision(subscription));
                        Assert.True(fixture.Reprocess(subscription));
                        Assert.True(ReprocessBindingTopic(fixture));
                        Assert.Null(fixture.ExecutableRevision(subscription));
                        fixture.AssertViews(status);
                    }
                    return entry switch
                    {
                        "reprocess" => () => Assert.True(fixture.Reprocess(subscription)),
                        "topic-reprocess" => () => Assert.True(ReprocessBindingTopic(fixture)),
                        "topic-replace" => () => fixture.Put(fixture.Fixture("topic-query")),
                        "reactivate" => () => fixture.Store.ChangeSubscriptionStatus(LifecycleTestStore.Id, "active"),
                        "handshake" => ConditionalBindingHandshake(fixture),
                        _ => throw new InvalidOperationException(entry),
                    };
                });
        }

        // A rejected replacement must not install or capture binding warnings.
        using LifecycleTestStore rejected = new(version);
        rejected.Seed();
        ParsedSubscription prior = rejected.Subscription;
        long revision = prior.Revision;
        string rawVersion = rejected.Get("Subscription/" + LifecycleTestStore.Id)["meta"]!["versionId"]!.GetValue<string>();
        using DiagnosticWriter writer = new(_ => AssertDiagnosticOutsideLocks(rejected, prior.GeneratedEvents));
        SetDiagnosticWriter(rejected, writer);
        FhirRequestContext context = new(rejected.Store, "PUT", "Subscription/" + LifecycleTestStore.Id)
        {
            SourceContent = rejected.SubscriptionWithBindingParameters("active", parameters).ToJsonString(),
            SourceFormat = "application/fhir+json",
            DestinationFormat = "application/fhir+json",
            IfMatch = "W/\"not-current\"",
        };
        Assert.False(rejected.Store.PerformInteraction(context, out FhirResponseContext failure, true, true));
        Assert.Null(failure.Resource);
        Assert.Empty(writer.Lines);
        Assert.Same(prior, rejected.Subscription);
        Assert.Equal(revision, prior.Revision);
        Assert.Equal(revision, rejected.ExecutableRevision(prior));
        Assert.Equal(rawVersion, rejected.Get("Subscription/" + LifecycleTestStore.Id)["meta"]!["versionId"]!.GetValue<string>());
        rejected.Delete();
        Assert.Empty(writer.Lines);
    }

    [Theory]
    [MemberData(nameof(Versions))]
    public async Task BindingModifierDiagnosticsDoNotHoldStoreLocks(FhirReleases.FhirSequenceCodes version)
    {
        string[] modifiers = ["unknown-binding-modifier", "unknown-binding-modifier-second", "unknown-binding-modifier"];
        string[] expected = modifiers.Select(modifier => $"Ignoring unknown modifier: Encounter?patient:{modifier}").ToArray();
        foreach (string entry in new[] { "direct", "reprocess", "topic-reprocess", "topic-replace", "reactivate", "handshake" })
        {
            await AssertBindingDiagnosticsUnlockedAsync(version, expected, [string.Empty, .. modifiers],
                preparationEmits: entry is "reprocess" or "topic-reprocess" or "topic-replace",
                fixture =>
                {
                    fixture.Seed("off");
                    string status = entry == "reactivate" ? "off" : entry == "handshake" ? "requested" : "active";
                    // This is a new programmatic descriptor, not an invalid native R5 enum.
                    ParsedSubscription subscription = fixture.SubscriptionWithBindingModifiers(status, modifiers);
                    Assert.Equal(0, subscription.Revision);
                    if (entry == "direct")
                    {
                        return () => Assert.True(fixture.Reprocess(subscription));
                    }
                    Assert.True(fixture.Reprocess(subscription));
                    Assert.Same(subscription, fixture.Subscription);
                    Assert.True(subscription.Revision > 0);
                    if (status != "active")
                    {
                        Assert.Null(fixture.ExecutableRevision(subscription));
                        Assert.True(fixture.Reprocess(subscription));
                        Assert.True(ReprocessBindingTopic(fixture));
                        Assert.Null(fixture.ExecutableRevision(subscription));
                        fixture.AssertViews(status);
                    }
                    return entry switch
                    {
                        "reprocess" => () => Assert.True(fixture.Reprocess(subscription)),
                        "topic-reprocess" => () => Assert.True(ReprocessBindingTopic(fixture)),
                        "topic-replace" => () => fixture.Put(fixture.Fixture("topic-query")),
                        "reactivate" => () => fixture.Store.ChangeSubscriptionStatus(LifecycleTestStore.Id, "active"),
                        "handshake" => ConditionalBindingHandshake(fixture),
                        _ => throw new InvalidOperationException(entry),
                    };
                });
        }
    }

    private static bool ReprocessBindingTopic(LifecycleTestStore fixture)
    {
        ParsedSubscriptionTopic topic;
        lock (fixture.Field("SubscriptionLifecycleLock"))
        {
            topic = (ParsedSubscriptionTopic)((IDictionary)fixture.Field("_topics"))[LifecycleTestStore.Topic]!;
        }
        return (bool)fixture.Store.GetType().GetMethod("StoreProcessSubscriptionTopic")!.Invoke(fixture.Store, [topic, false])!;
    }

    private static Action ConditionalBindingHandshake(LifecycleTestStore fixture)
    {
        SubscriptionSendEventArgs args = fixture.Args(ParsedSubscription.NotificationTypeCodes.Handshake);
        Assert.False(fixture.Store.TryCompleteSubscriptionHandshake(fixture.Args()));
        Assert.Equal("requested", args.Subscription.CurrentStatus);
        return () =>
        {
            Assert.True(fixture.Store.TryCompleteSubscriptionHandshake(args));
            Assert.False(fixture.Store.TryCompleteSubscriptionHandshake(args));
        };
    }

    private static async Task AssertBindingDiagnosticsUnlockedAsync(
        FhirReleases.FhirSequenceCodes version, string[] expected, string[] filterModifiers,
        bool preparationEmits, Func<LifecycleTestStore, Action> prepare)
    {
        using LifecycleTestStore fixture = new(version);
        ConcurrentQueue<ParsedSubscription> descriptors = new();
        fixture.Store.OnSubscriptionsChanged += (_, change) =>
        {
            if (change.ChangedSubscription is not null)
            {
                descriptors.Enqueue(change.ChangedSubscription);
            }
        };
        Dictionary<long, SubscriptionEvent>[] Histories() => descriptors.Select(subscription => subscription.GeneratedEvents).Distinct().ToArray();
        using DiagnosticWriter setupWriter = new(_ => AssertDiagnosticOutsideLocks(fixture, Histories()));
        SetDiagnosticWriter(fixture, setupWriter);
        Action install = prepare(fixture);
        Assert.Equal(preparationEmits ? expected : [], setupWriter.Lines.ToArray());
        fixture.Put(fixture.Fixture("other-patient"));
        ParsedSubscription? prior = descriptors.LastOrDefault();
        long? priorRevision = prior?.Revision;
        long? priorDeadline = prior?.ExpirationTicks;
        string? priorStatus = prior?.CurrentStatus;
        using ManualResetEventSlim reached = new();
        using ManualResetEventSlim release = new();
        int written = 0;
        using DiagnosticWriter writer = new(line =>
        {
            Dictionary<long, SubscriptionEvent>[] histories = Histories();
            Assert.NotEmpty(histories);
            AssertDiagnosticOutsideLocks(fixture, histories);
            int index = Interlocked.Increment(ref written) - 1;
            Assert.InRange(index, 0, expected.Length - 1);
            Assert.Equal(expected[index], line);
            if (index == 0)
            {
                reached.Set();
                Assert.True(release.Wait(TimeSpan.FromSeconds(10)));
            }
        });
        SetDiagnosticWriter(fixture, writer);
        Task worker = Task.Run(install);
        Task? off = null;
        ParsedSubscription? installed = null;
        Dictionary<long, SubscriptionEvent>? history = null;
        long installedRevision = 0;
        try
        {
            Assert.True(reached.Wait(TimeSpan.FromSeconds(10)));
            Assert.False(worker.IsCompleted);
            Assert.Equal(expected[0], Assert.Single(writer.Lines));
            installed = fixture.Subscription;
            history = installed.GeneratedEvents;
            installedRevision = installed.Revision;
            Assert.Equal(installedRevision, fixture.ExecutableRevision(installed));
            Assert.True(installedRevision > 0);
            AssertBindingFilters(fixture, installed, filterModifiers);
            if (ReferenceEquals(prior, installed))
            {
                Assert.Equal(priorDeadline, installed.ExpirationTicks);
                if (priorStatus == "active")
                {
                    Assert.Equal(priorRevision, installedRevision);
                }
                else
                {
                    Assert.True(installedRevision > priorRevision);
                }
            }
            fixture.AssertViews("active");
            fixture.Store.ChangeSubscriptionStatus(LifecycleTestStore.Id, "active");
            Assert.Equal(installedRevision, installed.Revision);
            Assert.Equal(expected[0], Assert.Single(writer.Lines));

            // Both the original valid patient filter and modifier fallback must still filter.
            long count = installed.CurrentEventCount;
            int sends = fixture.Sent.Count;
            JsonObject miss = fixture.Fixture("complete");
            miss["id"] = "binding-filter-miss";
            miss["subject"]!["reference"] = "Patient/other-patient";
            fixture.Put(miss);
            Assert.Equal(count, installed.CurrentEventCount);
            Assert.Equal(sends, fixture.Sent.Count);
            fixture.Match("binding-filter-positive");
            SubscriptionSendEventArgs sent = Assert.Single(fixture.Sent.Skip(sends));
            Assert.Same(installed, sent.Subscription);
            Assert.Equal(installedRevision, sent.Revision);
            Assert.Equal(count + 1, installed.CurrentEventCount);
            lock (fixture.Field("SubscriptionLifecycleLock"))
            {
                lock (history)
                {
                    Assert.Equal(count + 1, Assert.Single(sent.NotificationEvents).EventNumber);
                    Assert.Same(Assert.Single(sent.NotificationEvents), history[count + 1]);
                }
            }

            off = Task.Run(() =>
            {
                fixture.Store.ChangeSubscriptionStatus(LifecycleTestStore.Id, "off");
                fixture.AssertViews("off");
                Assert.Null(fixture.ExecutableRevision(installed));
            });
            await off.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(release.IsSet);
            Assert.False(worker.IsCompleted);
            Assert.Equal(expected[0], Assert.Single(writer.Lines));
            Assert.True(installed.Revision > installedRevision);
            fixture.Match("binding-after-off");
            Assert.Equal(count + 1, installed.CurrentEventCount);
            Assert.Equal(sends + 1, fixture.Sent.Count);
        }
        finally
        {
            release.Set();
            await Task.WhenAll(off is null ? [worker] : [worker, off]).WaitAsync(TimeSpan.FromSeconds(10));
        }
        Assert.Equal(expected, writer.Lines.ToArray());
        Assert.Same(installed, fixture.Subscription);
        Assert.Same(history, installed!.GeneratedEvents);
        fixture.AssertViews("off");
        fixture.Delete();
        Assert.Equal(expected, writer.Lines.ToArray());
    }

    private static void AssertBindingFilters(LifecycleTestStore fixture, ParsedSubscription subscription, string[] modifiers)
    {
        object resourceStore = fixture.Store["Encounter"];
        object registration = resourceStore.GetType().GetField("_subscriptionRegistrationLock", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(resourceStore)!;
        lock (registration)
        {
            IDictionary topics = (IDictionary)resourceStore.GetType().GetField("_executableSubscriptions", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(resourceStore)!;
            object executable = topics[subscription.TopicUrl]!;
            IDictionary bindings = (IDictionary)executable.GetType().GetProperty("FiltersBySubscription")!.GetValue(executable)!;
            object binding = bindings[subscription.Id]!;
            object[] filters = ((IEnumerable)binding.GetType().GetProperty("Filters")!.GetValue(binding)!).Cast<object>().ToArray();
            Assert.Equal(modifiers, filters.Select(filter => (string?)filter.GetType().GetProperty("ModifierLiteral")!.GetValue(filter)).ToArray());
            foreach (object filter in filters)
            {
                Assert.Equal("patient", filter.GetType().GetProperty("Name")!.GetValue(filter));
                Assert.Equal(["Patient/sub-patient"], (string[])filter.GetType().GetProperty("Values")!.GetValue(filter)!);
                Assert.Equal("None", filter.GetType().GetProperty("Modifier")!.GetValue(filter)!.ToString());
                Assert.Equal(false, filter.GetType().GetProperty("IgnoredParameter")!.GetValue(filter));
            }
        }
    }

    [Theory]
    [MemberData(nameof(Versions))]
    public async Task CompletionDiagnosticsDoNotHoldLifecycleLock(FhirReleases.FhirSequenceCodes version)
    {
        const string Payload = "{\"diagnostic\":\"accepted payload\"}\nsecond line";
        const string Error = "accepted diagnostic error\nsecond line";
        foreach (string entry in new[] { "delivered", "failed", "execution-error", "public-error" })
        {
            using LifecycleTestStore fixture = new(version);
            fixture.Seed();
            fixture.Match("diagnostic-event");
            SubscriptionSendEventArgs args = Assert.Single(fixture.Sent);
            ParsedSubscription subscription = args.Subscription;
            Dictionary<long, SubscriptionEvent> history = subscription.GeneratedEvents;
            object lifecycle = fixture.Field("SubscriptionLifecycleLock");
            long priorCommunication = LifecycleTestStore.Now.AddMinutes(-2).UtcTicks;
            lock (lifecycle)
            {
                subscription.LastCommunicationTicks = priorCommunication;
            }
            bool delivered = entry == "delivered";
            string expectedLine = delivered
                ? $"Sending content for event: Subscription/{LifecycleTestStore.Id}:1: \n-----\n{Payload}\n-----"
                : $" <<< Subscription/{LifecycleTestStore.Id}: {Error}";
            using ManualResetEventSlim reached = new();
            using ManualResetEventSlim release = new();
            using DiagnosticWriter writer = new(line =>
            {
                AssertDiagnosticOutsideLocks(fixture, history);
                Assert.Equal(expectedLine, line);
                reached.Set();
                Assert.True(release.Wait(TimeSpan.FromSeconds(10)));
            });
            SetDiagnosticWriter(fixture, writer);
            Task worker = Task.Run(async () =>
            {
                if (entry is "delivered" or "failed")
                {
                    Outcome expected = delivered ? new(OutcomeKind.Delivered, Payload) : new(OutcomeKind.Failed, Error: Error);
                    Assert.True(fixture.Store.TryDispatchSubscriptionNotification(args, () => Task.FromResult(expected),
                        out Task<Outcome> completion));
                    Assert.Equal(expected, await completion);
                }
                else if (entry == "execution-error")
                {
                    RegisterExecutionError(fixture, args.Revision, Error);
                }
                else
                {
                    RegisterIdOnlyError(fixture, LifecycleTestStore.Id, Error);
                }
            });
            Task? off = null;
            void AssertAcceptedBookkeeping()
            {
                lock (lifecycle)
                {
                    Assert.Equal(delivered ? LifecycleTestStore.Now.UtcTicks : priorCommunication, subscription.LastCommunicationTicks);
                    if (delivered)
                    {
                        KeyValuePair<long, string> record = Assert.Single(subscription.SerializedEvents);
                        Assert.Equal(1, record.Key);
                        Assert.Equal(Payload, record.Value);
                        Assert.Empty(subscription.NotificationErrors);
                    }
                    else
                    {
                        Assert.Empty(subscription.SerializedEvents);
                        Assert.Equal(Error, Assert.Single(subscription.NotificationErrors));
                    }
                    Assert.Same(history, subscription.GeneratedEvents);
                    lock (history)
                    {
                        Assert.Equal(1, Assert.Single(history).Key);
                        Assert.Equal(1, subscription.CurrentEventCount);
                    }
                }
            }
            try
            {
                Assert.True(reached.Wait(TimeSpan.FromSeconds(10)));
                Assert.False(worker.IsCompleted);
                Assert.Equal(expectedLine, Assert.Single(writer.Lines));
                AssertAcceptedBookkeeping();
                off = Task.Run(() => fixture.Store.ChangeSubscriptionStatus(LifecycleTestStore.Id, "off"));
                await off.WaitAsync(TimeSpan.FromSeconds(10));
                // Output is still held: the transition and all normalized views must already agree.
                Assert.False(release.IsSet);
                Assert.False(worker.IsCompleted);
                fixture.AssertViews("off");
                Assert.NotEqual(args.Revision, subscription.Revision);
                AssertAcceptedBookkeeping();
            }
            finally
            {
                release.Set();
                await Task.WhenAll(off is null ? [worker] : [worker, off]).WaitAsync(TimeSpan.FromSeconds(10));
            }
            Assert.Equal(expectedLine, Assert.Single(writer.Lines));
            AssertAcceptedBookkeeping();
        }
    }

    [Theory]
    [MemberData(nameof(Versions))]
    public async Task StaleDiagnosticsDoNotRecordOrWrite(FhirReleases.FhirSequenceCodes version)
    {
        foreach (string change in new[] { "cycle", "replace" })
        {
            foreach (bool failed in new[] { false, true })
            {
                using LifecycleTestStore fixture = new(version);
                fixture.Seed();
                SubscriptionSendEventArgs args = fixture.Args();
                ParsedSubscription prior = args.Subscription;
                Dictionary<long, SubscriptionEvent> history = prior.GeneratedEvents;
                using DiagnosticWriter writer = new(_ => AssertDiagnosticOutsideLocks(fixture, history));
                SetDiagnosticWriter(fixture, writer);
                TaskCompletionSource<Outcome> transport = new(TaskCreationOptions.RunContinuationsAsynchronously);
                Assert.True(fixture.Store.TryDispatchSubscriptionNotification(args, () => transport.Task, out Task<Outcome> completion));
                Outcome completed;
                try
                {
                    Assert.False(completion.IsCompleted);
                    if (change == "cycle")
                    {
                        fixture.Store.ChangeSubscriptionStatus(LifecycleTestStore.Id, "off");
                        fixture.Store.ChangeSubscriptionStatus(LifecycleTestStore.Id, "active");
                    }
                    else
                    {
                        fixture.PutSubscription();
                    }
                    RegisterExecutionError(fixture, args.Revision, "stale execution diagnostic");
                }
                finally
                {
                    transport.TrySetResult(failed
                        ? new(OutcomeKind.Failed, "stale failed payload", "stale completion error")
                        : new(OutcomeKind.Delivered, "stale delivered payload"));
                    completed = await completion.WaitAsync(TimeSpan.FromSeconds(10));
                }
                Assert.Equal(OutcomeKind.Suppressed, completed.Kind);
                Assert.Empty(writer.Lines);
                lock (fixture.Field("SubscriptionLifecycleLock"))
                {
                    ParsedSubscription current = fixture.Subscription;
                    Assert.NotEqual(args.Revision, current.Revision);
                    Assert.Equal("active", current.CurrentStatus);
                    foreach (ParsedSubscription subscription in new[] { prior, current })
                    {
                        Assert.Equal(0, subscription.LastCommunicationTicks);
                        Assert.Empty(subscription.SerializedEvents);
                        Assert.Empty(subscription.NotificationErrors);
                    }
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(Versions))]
    public void ErrorDiagnosticsPreserveEntryPointContracts(FhirReleases.FhirSequenceCodes version)
    {
        foreach (string state in new[] { "off", "requested", "expired", "replaced" })
        {
            using LifecycleTestStore fixture = new(version);
            fixture.Seed(state is "expired" or "replaced" ? "active" : state);
            ParsedSubscription prior = fixture.Subscription;
            long revision = prior.Revision;
            if (state == "expired")
            {
                fixture.Clock.Advance(TimeSpan.FromMinutes(31));
            }
            else if (state == "replaced")
            {
                fixture.PutSubscription();
            }
            using DiagnosticWriter writer = new(_ => AssertDiagnosticOutsideLocks(fixture, prior.GeneratedEvents));
            SetDiagnosticWriter(fixture, writer);
            RegisterExecutionError(fixture, revision, "ineligible execution diagnostic");
            Assert.Empty(writer.Lines);
            Assert.Empty(prior.NotificationErrors);
            ParsedSubscription current = fixture.Subscription;
            Assert.Equal(state == "replaced" ? "active" : state == "expired" ? "off" : state, current.CurrentStatus);
            long currentRevision = current.Revision;
            long deadline = current.ExpirationTicks;
            RegisterIdOnlyError(fixture, "missing-subscription", "unknown id diagnostic");
            Assert.Empty(writer.Lines);
            // This public API intentionally records by current id, even when inactive or replaced.
            RegisterIdOnlyError(fixture, LifecycleTestStore.Id, "current id-only diagnostic");
            Assert.Equal($" <<< Subscription/{LifecycleTestStore.Id}: current id-only diagnostic", Assert.Single(writer.Lines));
            lock (fixture.Field("SubscriptionLifecycleLock"))
            {
                Assert.Equal("current id-only diagnostic", Assert.Single(current.NotificationErrors));
                Assert.Empty(current.SerializedEvents);
                Assert.Equal(0, current.LastCommunicationTicks);
                Assert.Equal(currentRevision, current.Revision);
                Assert.Equal(deadline, current.ExpirationTicks);
                if (state == "replaced")
                {
                    Assert.NotSame(prior, current);
                    Assert.Empty(prior.NotificationErrors);
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(Managers))]
    public async Task BoundedQueueMakesProgressAcrossTenants(FhirReleases.FhirSequenceCodes version, bool restOnly)
    {
        foreach (int concurrency in new[] { 1, 3 })
        {
            using LifecycleTestStore sibling = new(version);
            sibling.Store.Config.ControllerName = "sibling";
            sibling.Seed();
            using NotificationFixture fixture = new(version, restOnly, sibling: sibling, capacity: 5, concurrency: concurrency,
                liveWorkers: true);
            Channel<(string Id, TaskCompletionSource Release)> arrivals = Channel.CreateUnbounded<(string, TaskCompletionSource)>();
            TaskCompletionSource cleanup = new(TaskCreationOptions.RunContinuationsAsynchronously);
            int active = 0;
            fixture.Handler.SendOperation = async (request, token) =>
            {
                int count = Interlocked.Increment(ref active);
                try
                {
                    Assert.InRange(count, 1, concurrency);
                    JsonNode bundle = JsonNode.Parse(await request.Content!.ReadAsStringAsync(token))!;
                    JsonNode encounter = Assert.Single(bundle["entry"]!.AsArray().Select(entry => entry!["resource"]!),
                        resource => (string?)resource["resourceType"] == "Encounter");
                    TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
                    arrivals.Writer.TryWrite((encounter["id"]!.GetValue<string>(), release));
                    await Task.WhenAny(cleanup.Task, release.Task);
                    return new(HttpStatusCode.NoContent);
                }
                catch (Exception exception)
                {
                    arrivals.Writer.TryComplete(exception);
                    throw;
                }
                finally
                {
                    Interlocked.Decrement(ref active);
                }
            };
            Task<(string Id, TaskCompletionSource Release)> Next() =>
                arrivals.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
            fixture.Start();
            Queue<TaskCompletionSource> held = new();
            try
            {
                for (int i = 0; i < concurrency; i++)
                {
                    fixture.Store.Match($"held-{i}");
                    (string id, TaskCompletionSource release) = await Next();
                    Assert.Equal($"held-{i}", id);
                    held.Enqueue(release);
                }
                // Five waiting slots: the lexically first tenant has three; it cannot borrow the other two.
                foreach (string id in new[] { "a-1", "a-2", "a-3", "a-refused" })
                {
                    fixture.Store.Match(id);
                }
                Assert.Equal(3, fixture.QueuedCount);
                foreach (string id in new[] { "b-1", "b-2", "b-refused" })
                {
                    sibling.Match(id);
                }
                Assert.Equal(5, fixture.QueuedCount);
                Assert.Equal(3, fixture.Scheduler.WaitingFor(fixture.Store.Store.Config.ControllerName));
                Assert.Equal(2, fixture.Scheduler.WaitingFor(sibling.Store.Config.ControllerName));
                Assert.Equal(2, fixture.Scheduler.RefusedCount);
                Assert.Equal(concurrency, fixture.Handler.Starts);
                int a = 0;
                int b = 0;
                char previous = '\0';
                for (int remaining = 4; remaining >= 0; remaining--)
                {
                    held.Dequeue().SetResult();
                    (string id, TaskCompletionSource release) = await Next();
                    held.Enqueue(release);
                    if (a < 3 && b < 2)
                    {
                        Assert.NotEqual(previous, id[0]);
                    }
                    previous = id[0];
                    Assert.Equal(id[0] == 'a' ? $"a-{++a}" : $"b-{++b}", id);
                    Assert.Equal(remaining, fixture.QueuedCount);
                    Assert.Equal(concurrency, fixture.Scheduler.ActiveCount);
                    Assert.Equal(concurrency, Volatile.Read(ref active));
                }
                Assert.Equal((3, 2), (a, b));
            }
            finally
            {
                Task stop = fixture.Stop();
                cleanup.TrySetResult();
                await stop.WaitAsync(TimeSpan.FromSeconds(10));
            }
            Assert.Equal(concurrency + 5, fixture.Handler.Starts);
            Assert.Equal(concurrency + 3, fixture.Store.Subscription.SerializedEvents.Count);
            Assert.Equal(2, sibling.Subscription.SerializedEvents.Count);
            Assert.Empty(fixture.Store.Subscription.NotificationErrors);
            Assert.Empty(sibling.Subscription.NotificationErrors);
        }
    }

    [Theory]
    [MemberData(nameof(Managers))]
    public async Task SaturationDoesNotReserveOrCompleteDelivery(FhirReleases.FhirSequenceCodes version, bool restOnly)
    {
        foreach (string kind in new[] { "event", "heartbeat", "handshake" })
        {
            using NotificationFixture fixture = new(version, restOnly, capacity: 1);
            fixture.Start();
            fixture.Scan();
            fixture.Store.Match("fill");
            SubscriptionChangedEventArgs? publication = null;
            fixture.Store.Store.OnSubscriptionsChanged += (_, change) =>
            {
                if (change.SendHandshake) { publication = change; }
            };
            void Produce()
            {
                switch (kind)
                {
                    case "event": fixture.Store.Match("refused-" + fixture.Store.Sent.Count); break;
                    case "heartbeat": fixture.Scan(); break;
                    case "handshake": fixture.Publish(Assert.IsType<SubscriptionChangedEventArgs>(publication)); break;
                }
            }
            fixture.Store.Clock.Advance(TimeSpan.FromSeconds(120));
            if (kind == "handshake")
            {
                fixture.Store.PutSubscription("requested");
            }
            for (int attempt = 0; attempt < 3; attempt++) { Produce(); }
            Assert.Equal(1, fixture.QueuedCount);
            Assert.Equal(0, fixture.Handler.Starts);
            Assert.Equal(0, fixture.HandshakeRevisionCount);
            Assert.DoesNotContain(long.MaxValue, fixture.HeartbeatSchedule().Values);
            ParsedSubscription sub = fixture.Store.Subscription;
            Assert.Empty(sub.SerializedEvents);
            Assert.Empty(sub.NotificationErrors);
            Assert.Equal(0, sub.LastCommunicationTicks);
            IReadOnlyDictionary<string, object?> warning = Assert.Single(fixture.Log.Saturation);
            Assert.Equal(fixture.Store.Store.Config.ControllerName, warning["Tenant"]);
            Assert.Equal(kind switch
            {
                "event" => ParsedSubscription.NotificationTypeCodes.EventNotification,
                "heartbeat" => ParsedSubscription.NotificationTypeCodes.Heartbeat,
                _ => ParsedSubscription.NotificationTypeCodes.Handshake,
            }, warning["Kind"]);
            Assert.Equal(1, warning["Depth"]);
            Assert.Equal(1L, warning["RefusedCount"]);
            fixture.Store.Clock.Advance(TimeSpan.FromMinutes(1));
            Produce();
            Assert.Equal(2, fixture.Log.Saturation.Count);
            Assert.Equal(kind == "handshake" ? 5L : 4L, fixture.Scheduler.RefusedCount);
            Assert.Equal(fixture.Scheduler.RefusedCount, fixture.Log.Saturation.Last()["RefusedCount"]);
            Assert.DoesNotContain(fixture.Log.Entries, entry => entry.Message.Contains("delivery failed", StringComparison.OrdinalIgnoreCase));

            // The requested revision invalidates the filler. In the other cases fail it at transport
            // so the heartbeat remains due and we can retry admission on the very same revision.
            fixture.Handler.StartOperation = _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
            await fixture.DrainOneAsync();
            if (kind == "handshake")
            {
                Assert.Equal(0, fixture.Handler.Starts);
                Assert.Empty(sub.NotificationErrors);
            }
            else
            {
                Assert.Equal(1, fixture.Handler.Starts);
                Assert.Single(sub.NotificationErrors);
                Assert.Contains(fixture.Log.Entries, entry => entry.Message.Contains("delivery failed", StringComparison.OrdinalIgnoreCase));
            }
            fixture.Handler.StartOperation = _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
            Produce();
            Assert.Equal(1, fixture.QueuedCount);
            await fixture.DrainOneAsync();
            Assert.Equal(kind == "handshake" ? 1 : 2, fixture.Handler.Starts);
            Assert.Equal("active", fixture.Store.Subscription.CurrentStatus);
            Assert.Single(fixture.Store.Subscription.SerializedEvents);
        }
    }

    [Theory]
    [MemberData(nameof(Managers))]
    public async Task StartupReconciliationSharesHandshakeBudget(FhirReleases.FhirSequenceCodes version, bool restOnly)
    {
        using NotificationFixture fixture = new(version, restOnly, capacity: 1);
        fixture.Store.PutSubscription("requested");
        JsonObject second = fixture.Store.SubscriptionJson("requested");
        second["id"] = "second-requested";
        fixture.Store.Put(second);
        fixture.Start();
        Assert.Equal(1, fixture.QueuedCount);
        Assert.Equal(1, fixture.HandshakeRevisionCount);
        Assert.Equal(1, fixture.Scheduler.RefusedCount);
        await fixture.DrainOneAsync();
        ParsedSubscription declined = Assert.Single(fixture.Store.Store.CurrentSubscriptions, sub => sub.CurrentStatus == "requested");
        Assert.Equal(0, declined.LastCommunicationTicks);
        Assert.Empty(declined.NotificationErrors);
        Assert.Empty(declined.SerializedEvents);
        fixture.Publish(new()
        {
            Tenant = fixture.Store.Store.Config,
            ChangedSubscription = declined,
            Revision = declined.Revision,
            SendHandshake = true,
        });
        await fixture.DrainOneAsync();
        Assert.All(fixture.Store.Store.CurrentSubscriptions, sub => Assert.Equal("active", sub.CurrentStatus));
        Assert.Equal(2, fixture.Handler.Starts);
    }

    [Theory]
    [MemberData(nameof(Managers))]
    public async Task ManagerStopPreservesInFlightDrainAndTimerOwnership(FhirReleases.FhirSequenceCodes version, bool restOnly)
    {
        foreach (string mode in new[] { "drain", "cancel", "dispose" })
        {
            bool ownsClient = mode == "dispose";
            using NotificationFixture fixture = new(version, restOnly, concurrency: 1, liveWorkers: true, ownsClient: ownsClient);
            using CancellationTokenSource cancellation = new();
            TaskCompletionSource<CancellationToken> started = new(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<HttpResponseMessage> release = new(TaskCreationOptions.RunContinuationsAsynchronously);
            fixture.Handler.StartOperation = token =>
            {
                started.TrySetResult(token);
                return release.Task; // Intentionally ignore cancellation until explicitly released.
            };
            fixture.Start();
            fixture.Store.Match("active");
            Task? dispose = null;
            Task? repeated = null;
            try
            {
                CancellationToken transport = await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
                TaskCompletionSource canceled = new(TaskCreationOptions.RunContinuationsAsynchronously);
                using CancellationTokenRegistration registration = transport.Register(() => canceled.TrySetResult());
                fixture.Store.Match("waiting");
                Assert.Equal(1, fixture.QueuedCount);
                Task stop = fixture.Stop(cancellation.Token);
                repeated = fixture.Stop();
                Assert.False(stop.IsCompleted);
                Assert.False(repeated.IsCompleted);
                Assert.False(transport.IsCancellationRequested);
                Assert.Equal(0, fixture.QueuedCount);
                Assert.Equal(1, Assert.Single(fixture.Log.Discards));
                fixture.Store.Clock.Advance(TimeSpan.FromMinutes(3));
                fixture.Store.Clock.RunDueCallbacks();
                fixture.Store.Match("after-stop");
                Assert.Equal(0, fixture.QueuedCount);
                if (mode == "cancel")
                {
                    cancellation.Cancel();
                    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => stop.WaitAsync(TimeSpan.FromSeconds(10)));
                }
                else if (mode == "dispose")
                {
                    dispose = Task.Run(fixture.Service.Dispose);
                }
                if (mode != "drain")
                {
                    await canceled.Task.WaitAsync(TimeSpan.FromSeconds(10));
                    if (dispose is not null) { Assert.False(dispose.IsCompleted); }
                }
                Assert.False(repeated.IsCompleted);
                Assert.False(fixture.Handler.Disposed);
                release.TrySetResult(new(HttpStatusCode.NoContent));
                if (mode != "cancel") { await stop.WaitAsync(TimeSpan.FromSeconds(10)); }
                if (mode == "drain")
                {
                    Assert.False(transport.IsCancellationRequested);
                    Assert.Equal(1, Assert.Single(fixture.Store.Subscription.SerializedEvents).Key);
                }
            }
            finally
            {
                if (!release.Task.IsCompleted) { release.SetResult(new(HttpStatusCode.NoContent)); }
                if (repeated is not null) { await repeated.WaitAsync(TimeSpan.FromSeconds(10)); }
                if (dispose is not null) { await dispose.WaitAsync(TimeSpan.FromSeconds(10)); }
            }
            await fixture.Stop().WaitAsync(TimeSpan.FromSeconds(10));
            if (ownsClient) { Assert.True(fixture.Handler.Disposed); }
            fixture.Service.Dispose();
            Assert.Equal(1, fixture.Handler.Starts);
            Assert.Equal(ownsClient, fixture.Handler.Disposed);
            if (!ownsClient)
            {
                fixture.Handler.StartOperation = _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
                using HttpResponseMessage probe = await fixture.Client.GetAsync("https://ownership.unit.test");
                Assert.Equal(HttpStatusCode.NoContent, probe.StatusCode);
            }
        }

        // A timer callback captured before shutdown is still owned even with no active send.
        using NotificationFixture timers = new(version, restOnly);
        timers.Start();
        timers.Scan();
        timers.Store.Clock.Advance(TimeSpan.FromMinutes(2));
        IReadOnlyList<ManualTimeProvider.CapturedCallback> callbacks = timers.Store.Clock.CaptureDueCallbacks();
        Task timerDrain;
        try
        {
            timerDrain = timers.Stop();
            Assert.NotEmpty(callbacks);
            Assert.False(timerDrain.IsCompleted);
        }
        finally
        {
            foreach (ManualTimeProvider.CapturedCallback callback in callbacks) { callback.Invoke(); }
        }
        await timerDrain.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(0, timers.QueuedCount);
        Assert.Equal(0, timers.Handler.Starts);
    }

    private static void SetDiagnosticWriter(LifecycleTestStore fixture, TextWriter writer) => fixture.Store.GetType()
        .GetField("_subscriptionDiagnosticWriter", BindingFlags.Instance | BindingFlags.NonPublic)!
        .SetValue(fixture.Store, new Action<string>(writer.WriteLine));

    private static void RegisterExecutionError(LifecycleTestStore fixture, long revision, string error) => fixture.Store.GetType()
        .GetMethod("RegisterSubscriptionExecutionError", BindingFlags.Instance | BindingFlags.NonPublic)!
        .Invoke(fixture.Store, [LifecycleTestStore.Id, revision, error]);

    private static void RegisterIdOnlyError(LifecycleTestStore fixture, string id, string error) => fixture.Store.GetType()
        .GetMethod("RegisterError", BindingFlags.Instance | BindingFlags.Public)!
        .Invoke(fixture.Store, [id, error]);

    private static void AssertDiagnosticOutsideLocks(LifecycleTestStore fixture, params Dictionary<long, SubscriptionEvent>[] histories)
    {
        fixture.AssertOutsideLocks();
        foreach (Dictionary<long, SubscriptionEvent> history in histories)
        {
            Assert.False(Monitor.IsEntered(history));
        }
        foreach (IResourceStore resourceStore in fixture.Store.Values)
        {
            object mutation = resourceStore.GetType().GetField("_lockObject", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(resourceStore)!;
            Assert.False(mutation is System.Threading.Lock resourceLock ? resourceLock.IsHeldByCurrentThread : Monitor.IsEntered(mutation));
            object registration = resourceStore.GetType().GetField("_subscriptionRegistrationLock", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(resourceStore)!;
            Assert.False(Monitor.IsEntered(registration));
        }
    }

    private sealed class DiagnosticWriter : TextWriter
    {
        private readonly Action<string> _onWriteLine;
        internal ConcurrentQueue<string> Lines { get; } = new();
        public override Encoding Encoding => Encoding.UTF8;
        internal DiagnosticWriter(Action<string> onWriteLine) => _onWriteLine = onWriteLine;
        public override void WriteLine(string? value)
        {
            string line = Assert.IsType<string>(value);
            Lines.Enqueue(line);
            _onWriteLine(line);
        }
    }

    private sealed class NotificationFixture : IDisposable
    {
        internal LifecycleTestStore Store { get; }
        internal INotificationManager Service { get; }
        internal RecordingHandler Handler { get; } = new();
        internal HttpClient Client { get; }
        internal RecordingLog Log { get; } = new();
        internal NotificationScheduler Scheduler => Service is RestOnlyNotificationManager rest
            ? rest.Scheduler : ((NotificationManager)Service).Scheduler;
        internal NotificationFixture(FhirReleases.FhirSequenceCodes version, bool restOnly, ZulipHandler? zulipHandler = null,
            LifecycleTestStore? sibling = null, int capacity = 256, int concurrency = 4, bool liveWorkers = false, bool ownsClient = false)
        {
            Store = new(version);
            Store.Seed();
            Client = new(Handler);
            TestStores stores = new(Store.Store);
            Dictionary<string, TenantConfiguration> tenants = new() { [Store.Store.Config.ControllerName] = Store.Store.Config };
            if (sibling is not null)
            {
                stores.Add(sibling.Store.Config.ControllerName, sibling.Store);
                tenants.Add(sibling.Store.Config.ControllerName, sibling.Store.Config);
            }
            CandleConfig config = new()
            {
                NotificationQueueCapacity = capacity,
                NotificationConcurrency = concurrency,
                SmtpHost = "smtp.unit.test",
                SmtpPort = 465,
                SmtpUser = "sender@unit.test",
                SmtpPassword = "synthetic-only",
                ZulipUrl = zulipHandler is null ? null : "https://operator.unit.test/chat/",
                ZulipEmail = zulipHandler is null ? null : "operator",
                ZulipKey = zulipHandler is null ? null : "synthetic-op",
            };
            Service = restOnly
                ? new RestOnlyNotificationManager(config, tenants, stores, Log, Store.Clock, Client)
                : new NotificationManager(config, tenants, stores, Log, Store.Clock, Client, zulipHandler);
            if (ownsClient)
            {
                // Exercise the manager-owned disposal path without ever installing a network transport.
                Service.GetType().GetField("_ownsHttpClient", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(Service, true);
            }
            if (!liveWorkers) { Scheduler.PauseWorkers(); }
        }
        internal void Start() => ((IHostedService)Service).StartAsync(CancellationToken.None).GetAwaiter().GetResult();
        internal Task Stop(CancellationToken token = default) => ((IHostedService)Service).StopAsync(token);
        internal void Email()
        {
            Store.PutSubscription("requested");
            Store.Subscription.ChannelCode = "email";
            Store.Subscription.Endpoint = "recipient@unit.test";
        }
        internal void Zulip(bool requested = false)
        {
            if (requested)
            {
                Store.PutSubscription("requested");
            }
            ParsedSubscription subscription = Store.Subscription;
            subscription.ChannelCode = "zulip";
            subscription.Parameters = new()
            {
                ["site"] = ["https://zulip.unit.test"],
                ["email"] = ["bot@unit.test"],
                ["key"] = ["synthetic-local-only"],
                ["streamId"] = ["1", "2"],
                ["userId"] = ["3", "4"],
            };
        }
        internal Task<Outcome> Notify(SubscriptionSendEventArgs args, CancellationToken cancellationToken = default)
        {
            MethodInfo method = Service.GetType().GetMethod("TryNotify", BindingFlags.Instance | BindingFlags.NonPublic)!;
            return (Task<Outcome>)method.Invoke(Service, [Store.Store, args, cancellationToken])!;
        }
        internal void Scan() => Invoke("CheckAndSendHeartbeats", [null]);
        internal int QueuedCount => Scheduler.WaitingCount;
        internal int HandshakeRevisionCount => ((IDictionary)Service.GetType()
            .GetField("_handshakeRevisions", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Service)!).Count;
        internal void Publish(SubscriptionChangedEventArgs change) => Store.Store.GetType()
            .GetMethod("PublishSubscriptionChanges", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(Store.Store, [new[] { change }, new List<string>()]);
        internal Dictionary<object, long> HeartbeatSchedule()
        {
            IDictionary reservations = (IDictionary)Service.GetType()
                .GetField("_heartbeatReservations", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Service)!;
            return reservations.Keys.Cast<object>().ToDictionary(key => key, key => (long)reservations[key]!);
        }
        internal Task DrainOneAsync() => Scheduler.DrainOneAsync().WaitAsync(TimeSpan.FromSeconds(10));
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
        internal Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? SendOperation { get; set; }
        private readonly SemaphoreSlim _arrivals = new(0);
        internal Task WaitForStartAsync() => _arrivals.WaitAsync().WaitAsync(TimeSpan.FromSeconds(10));
        private int _starts;
        internal int Starts => Volatile.Read(ref _starts);
        internal bool Disposed { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            ObjectDisposedException.ThrowIf(Disposed, this);
            Interlocked.Increment(ref _starts);
            _arrivals.Release();
            return SendOperation is null ? StartOperation(cancellationToken) : SendOperation(request, cancellationToken);
        }
        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            if (disposing) { _arrivals.Dispose(); }
            base.Dispose(disposing);
        }
    }

    private sealed class RecordingLog : ILogger<NotificationManager>, ILogger<RestOnlyNotificationManager>
    {
        internal ConcurrentQueue<string> Failures { get; } = new();
        internal ConcurrentQueue<(LogLevel Level, string Message)> Entries { get; } = new();
        internal ConcurrentQueue<IReadOnlyDictionary<string, object?>> Saturation { get; } = new();
        internal ConcurrentQueue<int> Discards { get; } = new();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            string message = formatter(state, exception);
            Entries.Enqueue((logLevel, message));
            if (state is IEnumerable<KeyValuePair<string, object?>> fields)
            {
                if (message.StartsWith("queue_saturated", StringComparison.Ordinal))
                {
                    Saturation.Enqueue(fields.ToDictionary(pair => pair.Key, pair => pair.Value));
                }
                if (message.StartsWith("queue_discarded", StringComparison.Ordinal))
                {
                    Discards.Enqueue((int)fields.Single(pair => pair.Key == "DiscardedCount").Value!);
                }
            }
            if (logLevel >= LogLevel.Warning)
            {
                Failures.Enqueue(message);
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
