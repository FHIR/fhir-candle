// <copyright file="SubscriptionLifecyclePolicyTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using fhir.candle.Tests.Infrastructure;
using FhirCandle.Models;

namespace fhir.candle.Tests;

/// <summary>Pure deadline/eligibility contracts and the controlled clock used by lifecycle tests.</summary>
public sealed class SubscriptionLifecyclePolicyTests
{
    private static readonly DateTimeOffset _utcNow = new(2026, 11, 19, 20, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan _watchdog = TimeSpan.FromSeconds(10);

    /// <summary>Caps are absolute UTC instants, and an earlier client end always wins.</summary>
    [Fact]
    public void DeadlineUsesAbsoluteUtcAndEarlierEnd()
    {
        DateTimeOffset cap = new(2026, 11, 19, 20, 30, 0, TimeSpan.Zero);
        DateTimeOffset earlier = new(2026, 11, 19, 20, 10, 0, TimeSpan.Zero);
        DateTimeOffset later = new(2026, 11, 19, 21, 0, 0, TimeSpan.Zero);
        DateTimeOffset offsetEnd = new(2026, 11, 19, 21, 10, 0, TimeSpan.FromHours(1));
        DateTimeOffset offsetNow = new(2026, 11, 19, 21, 0, 0, TimeSpan.FromHours(1));

        Assert.Equal(cap.UtcTicks, SubscriptionLifecyclePolicy.CalculateExpirationTicks(_utcNow, 30));
        Assert.Equal(cap.UtcTicks, SubscriptionLifecyclePolicy.CalculateExpirationTicks(_utcNow, 30, later));
        Assert.Equal(cap.UtcTicks, SubscriptionLifecyclePolicy.CalculateExpirationTicks(_utcNow, 30, cap));
        Assert.Equal(earlier.UtcTicks, SubscriptionLifecyclePolicy.CalculateExpirationTicks(_utcNow, 30, earlier));
        Assert.Equal(earlier.UtcTicks, SubscriptionLifecyclePolicy.CalculateExpirationTicks(_utcNow, 30, offsetEnd));
        Assert.Equal(cap.UtcTicks, SubscriptionLifecyclePolicy.CalculateExpirationTicks(offsetNow, 30));
        Assert.Equal(_utcNow.UtcTicks, SubscriptionLifecyclePolicy.CalculateExpirationTicks(_utcNow, 30, _utcNow));
        Assert.Equal(_utcNow.UtcTicks - 1, SubscriptionLifecyclePolicy.CalculateExpirationTicks(_utcNow, 30, _utcNow.AddTicks(-1)));
        Assert.Equal(0, SubscriptionLifecyclePolicy.CalculateExpirationTicks(_utcNow, 30, DateTimeOffset.MinValue));

        DateTimeOffset reversed = Assert.IsType<DateTimeOffset>(SubscriptionLifecyclePolicy.GetExpiration(offsetEnd.UtcTicks));
        Assert.Equal(earlier, reversed);
        Assert.Equal(TimeSpan.Zero, reversed.Offset);
        Assert.False(SubscriptionLifecyclePolicy.IsExpired(cap.UtcTicks, _utcNow));
        Assert.False(SubscriptionLifecyclePolicy.IsExpired(cap.UtcTicks, cap.AddTicks(-1)));
        Assert.True(SubscriptionLifecyclePolicy.IsExpired(cap.UtcTicks, cap));
        Assert.True(SubscriptionLifecyclePolicy.IsExpired(cap.UtcTicks, cap.AddTicks(1)));
        Assert.True(SubscriptionLifecyclePolicy.IsExpired(_utcNow.UtcTicks, offsetNow));
    }

    /// <summary>Zero removes only the configured cap; zero ticks is still an already reached finite end.</summary>
    [Fact]
    public void ZeroMaximumPreservesExplicitEndOrUnlimited()
    {
        DateTimeOffset end = new(2026, 11, 20, 21, 0, 0, TimeSpan.FromHours(1));
        DateTimeOffset expected = new(2026, 11, 20, 20, 0, 0, TimeSpan.Zero);
        Assert.Equal(-1, SubscriptionLifecyclePolicy.UnlimitedExpirationTicks);
        Assert.Equal(-1, SubscriptionLifecyclePolicy.CalculateExpirationTicks(_utcNow, 0));
        Assert.Equal(expected.UtcTicks, SubscriptionLifecyclePolicy.CalculateExpirationTicks(_utcNow, 0, end));
        Assert.Equal(_utcNow.UtcTicks, SubscriptionLifecyclePolicy.CalculateExpirationTicks(_utcNow, 0, _utcNow));
        Assert.Equal(_utcNow.UtcTicks - 1, SubscriptionLifecyclePolicy.CalculateExpirationTicks(_utcNow, 0, _utcNow.AddTicks(-1)));
        Assert.Equal(0, SubscriptionLifecyclePolicy.CalculateExpirationTicks(_utcNow, 0, DateTimeOffset.MinValue));
        Assert.Null(SubscriptionLifecyclePolicy.GetExpiration(-1));
        Assert.Equal(DateTimeOffset.MinValue, SubscriptionLifecyclePolicy.GetExpiration(0));
        Assert.True(SubscriptionLifecyclePolicy.IsValidExpirationTicks(0));
        Assert.True(SubscriptionLifecyclePolicy.IsExpired(0, DateTimeOffset.MinValue));
        Assert.True(SubscriptionLifecyclePolicy.IsExpired(0, _utcNow));
        Assert.False(SubscriptionLifecyclePolicy.IsExpired(-1, DateTimeOffset.MaxValue));
    }

    /// <summary>Invalid configuration and all non-sentinel out-of-range deadlines fail before date construction.</summary>
    [Fact]
    public void InvalidConfigurationAndReverseTicksAreRejected()
    {
        foreach (int maximum in new[] { -1, int.MinValue })
        {
            Assert.Equal("maximumExpirationMinutes",
                Assert.Throws<ArgumentOutOfRangeException>(() =>
                    SubscriptionLifecyclePolicy.ValidateMaximumExpirationMinutes(maximum)).ParamName);
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                SubscriptionLifecyclePolicy.CalculateExpirationTicks(_utcNow, maximum));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                SubscriptionLifecyclePolicy.CalculateExpirationTicks(_utcNow, maximum, _utcNow));
        }

        foreach (long invalidTicks in new[] { long.MinValue, -2, DateTimeOffset.MaxValue.UtcTicks + 1, long.MaxValue })
        {
            Assert.False(SubscriptionLifecyclePolicy.IsValidExpirationTicks(invalidTicks));
            Assert.Equal("expirationTicks",
                Assert.Throws<ArgumentOutOfRangeException>(() =>
                    SubscriptionLifecyclePolicy.ValidateExpirationTicks(invalidTicks)).ParamName);
            Assert.Throws<ArgumentOutOfRangeException>(() => SubscriptionLifecyclePolicy.GetExpiration(invalidTicks));
            Assert.Throws<ArgumentOutOfRangeException>(() => SubscriptionLifecyclePolicy.IsExpired(invalidTicks, _utcNow));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                SubscriptionLifecyclePolicy.CanGenerateEvents("off", invalidTicks, _utcNow));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                SubscriptionLifecyclePolicy.CanDeliverNotification("off", invalidTicks,
                    ParsedSubscription.NotificationTypeCodes.Handshake, _utcNow));
        }

        foreach (long validTicks in new[] { -1, 0, _utcNow.UtcTicks, DateTimeOffset.MaxValue.UtcTicks })
        {
            Assert.True(SubscriptionLifecyclePolicy.IsValidExpirationTicks(validTicks));
            SubscriptionLifecyclePolicy.ValidateExpirationTicks(validTicks);
        }
        SubscriptionLifecyclePolicy.ValidateMaximumExpirationMinutes(0);
        SubscriptionLifecyclePolicy.ValidateMaximumExpirationMinutes(int.MaxValue);
    }

    /// <summary>Large caps and near-maximum instants cannot wrap or throw during cap arithmetic.</summary>
    [Fact]
    public void DeadlineArithmeticSaturates()
    {
        DateTimeOffset maximum = DateTimeOffset.MaxValue;
        DateTimeOffset nearMaximum = maximum.AddTicks(-1);
        DateTimeOffset beforeMaximum = maximum.AddMinutes(-30);
        Assert.Equal(maximum.UtcTicks, SubscriptionLifecyclePolicy.CalculateExpirationTicks(nearMaximum, 1));
        Assert.Equal(maximum.UtcTicks, SubscriptionLifecyclePolicy.CalculateExpirationTicks(maximum, int.MaxValue));
        Assert.Equal(maximum.UtcTicks, SubscriptionLifecyclePolicy.CalculateExpirationTicks(beforeMaximum, 30));
        Assert.Equal(maximum.UtcTicks, SubscriptionLifecyclePolicy.CalculateExpirationTicks(nearMaximum, int.MaxValue));
        Assert.Equal(beforeMaximum.UtcTicks, SubscriptionLifecyclePolicy.CalculateExpirationTicks(nearMaximum, int.MaxValue, beforeMaximum));
        Assert.Equal(nearMaximum.UtcTicks, SubscriptionLifecyclePolicy.CalculateExpirationTicks(nearMaximum, 30, nearMaximum));
        Assert.Equal((long)int.MaxValue * TimeSpan.TicksPerMinute,
            SubscriptionLifecyclePolicy.CalculateExpirationTicks(DateTimeOffset.MinValue, int.MaxValue));
        Assert.Equal(maximum, SubscriptionLifecyclePolicy.GetExpiration(maximum.UtcTicks));
        Assert.False(SubscriptionLifecyclePolicy.IsExpired(maximum.UtcTicks, nearMaximum));
        Assert.True(SubscriptionLifecyclePolicy.IsExpired(maximum.UtcTicks, maximum));
    }

    /// <summary>Only requested handshakes and active event/heartbeat deliveries are eligible before the deadline.</summary>
    [Fact]
    public void EligibilitySeparatesRequestedAndActive()
    {
        ParsedSubscription.NotificationTypeCodes[] types = Enum.GetValues<ParsedSubscription.NotificationTypeCodes>();
        Assert.Equal(5, types.Length);
        string?[] statuses = ["requested", "active", "off", "error", "entered-in-error", "Active", "REQUESTED", "", null];
        foreach (string? status in statuses)
        {
            foreach (long expirationTicks in new[] { -1, _utcNow.UtcTicks + 1, _utcNow.UtcTicks, _utcNow.UtcTicks - 1, 0 })
            {
                bool unexpired = expirationTicks == -1 || expirationTicks == _utcNow.UtcTicks + 1;
                Assert.Equal(unexpired && status == "active",
                    SubscriptionLifecyclePolicy.CanGenerateEvents(status, expirationTicks, _utcNow));
                foreach (ParsedSubscription.NotificationTypeCodes type in types)
                {
                    bool expected = unexpired && (type == ParsedSubscription.NotificationTypeCodes.Handshake
                        ? status == "requested"
                        : (type == ParsedSubscription.NotificationTypeCodes.EventNotification ||
                            type == ParsedSubscription.NotificationTypeCodes.Heartbeat) && status == "active");
                    Assert.Equal(expected,
                        SubscriptionLifecyclePolicy.CanDeliverNotification(status, expirationTicks, type, _utcNow));
                }
            }
        }

        Assert.False(SubscriptionLifecyclePolicy.CanDeliverNotification("active", -1,
            (ParsedSubscription.NotificationTypeCodes)int.MaxValue, _utcNow));
    }

    /// <summary>UTC movement, due capture, callback execution, and timer drain/disposal are independent.</summary>
    [Fact]
    public async Task ManualClockSeparatesAdvanceCallbacksAndDisposal()
    {
        ManualTimeProvider clock = new(_utcNow.ToOffset(TimeSpan.FromHours(1)));
        object state = new();
        int calls = 0;
        long timestamp = clock.GetTimestamp();
        using ITimer timer = clock.CreateTimer(value =>
        {
            Assert.Same(state, value);
            Interlocked.Increment(ref calls);
        }, state, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(2));
        Assert.Equal(_utcNow, clock.GetUtcNow());
        Assert.Equal(TimeSpan.Zero, clock.GetUtcNow().Offset);
        Assert.Equal(TimeZoneInfo.Utc, clock.LocalTimeZone);
        Assert.Equal(_utcNow.AddSeconds(5), clock.NextDueTime);
        Assert.True(clock.WaitForCallbacksAsync().IsCompletedSuccessfully);
        Assert.Equal(0, clock.RunDueCallbacks());
        clock.Advance(TimeSpan.FromSeconds(5));
        Assert.Equal(TimeSpan.FromSeconds(5), clock.GetElapsedTime(timestamp));
        Assert.Equal(0, calls);
        IReadOnlyList<ManualTimeProvider.CapturedCallback> captured = clock.CaptureDueCallbacks();
        ManualTimeProvider.CapturedCallback first = Assert.Single(captured);
        Task drain = clock.WaitForCallbacksAsync();
        try
        {
            Assert.Equal(_utcNow.AddSeconds(7), clock.NextDueTime);
            Assert.False(drain.IsCompleted);
            Assert.True(timer.Change(TimeSpan.FromSeconds(20), Timeout.InfiniteTimeSpan));
            Assert.Equal(_utcNow.AddSeconds(25), clock.NextDueTime);
        }
        finally
        {
            first.Invoke();
        }
        await drain.WaitAsync(_watchdog);
        Assert.Equal(1, calls);
        Assert.Throws<InvalidOperationException>(first.Invoke);
        Assert.True(timer.Change(TimeSpan.Zero, TimeSpan.Zero));
        Assert.Equal(1, clock.RunDueCallbacks());
        Assert.Equal(2, calls);
        Assert.Null(clock.NextDueTime);

        Assert.True(timer.Change(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2)));
        clock.Advance(TimeSpan.FromSeconds(8));
        Assert.Equal(2, calls);
        Assert.Equal(1, clock.RunDueCallbacks());
        Assert.Equal(3, calls);
        Assert.Equal(_utcNow.AddSeconds(14), clock.NextDueTime);
        Assert.True(timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan));
        Assert.Null(clock.NextDueTime);
        Assert.Equal(0, clock.RunDueCallbacks());
        Assert.Throws<ArgumentOutOfRangeException>(() => clock.Advance(TimeSpan.FromTicks(-1)));
        Assert.Throws<ArgumentNullException>(() =>
            clock.CreateTimer(null!, null, TimeSpan.Zero, Timeout.InfiniteTimeSpan));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            clock.CreateTimer(_ => { }, null, TimeSpan.FromMilliseconds(-2), Timeout.InfiniteTimeSpan));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            timer.Change(TimeSpan.Zero, ManualTimeProvider.MaximumTimerInterval.Add(TimeSpan.FromTicks(1))));
        Assert.Null(clock.NextDueTime);
        Assert.True(timer.Change(ManualTimeProvider.MaximumTimerInterval, Timeout.InfiniteTimeSpan));
        Assert.Equal(clock.GetUtcNow() + ManualTimeProvider.MaximumTimerInterval, clock.NextDueTime);
        timer.Dispose();
        timer.Dispose();
        Assert.Null(clock.NextDueTime);
        Assert.False(timer.Change(TimeSpan.Zero, Timeout.InfiniteTimeSpan));
        await timer.DisposeAsync();

        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using ManualResetEventSlim release = new(false);
        using ITimer heldTimer = clock.CreateTimer(_ =>
        {
            entered.TrySetResult();
            Assert.True(release.Wait(_watchdog), "The callback release rendezvous timed out.");
        }, null, TimeSpan.Zero, Timeout.InfiniteTimeSpan);
        ManualTimeProvider.CapturedCallback heldCallback = Assert.Single(clock.CaptureDueCallbacks());
        Task heldDrain = clock.WaitForCallbacksAsync();
        Task callbackTask = Task.Run(heldCallback.Invoke);
        Task probe = Task.CompletedTask;
        Task disposal = Task.CompletedTask;
        try
        {
            await entered.Task.WaitAsync(_watchdog);
            // A second thread must be able to inspect/change/dispose while the callback body is held.
            probe = Task.Run(() =>
            {
                Assert.Equal(_utcNow.AddSeconds(13), clock.GetUtcNow());
                Assert.True(heldTimer.Change(TimeSpan.FromSeconds(1), Timeout.InfiniteTimeSpan));
                heldTimer.Dispose();
                Assert.Null(clock.NextDueTime);
            });
            await probe.WaitAsync(_watchdog);
            disposal = heldTimer.DisposeAsync().AsTask();
            Assert.False(disposal.IsCompleted);
            Assert.False(heldDrain.IsCompleted);
            Assert.False(clock.WaitForCallbacksAsync().IsCompleted);
        }
        finally
        {
            release.Set();
            await Task.WhenAll(callbackTask, probe, disposal, heldDrain).WaitAsync(_watchdog);
        }
        Assert.True(clock.WaitForCallbacksAsync().IsCompletedSuccessfully);
        Assert.Equal(0, clock.RunDueCallbacks());

        using ITimer staleTimer = clock.CreateTimer(_ => calls++, null, TimeSpan.Zero, Timeout.InfiniteTimeSpan);
        ManualTimeProvider.CapturedCallback staleCallback = Assert.Single(clock.CaptureDueCallbacks());
        staleTimer.Dispose();
        Task staleDisposal = staleTimer.DisposeAsync().AsTask();
        try
        {
            Assert.False(staleDisposal.IsCompleted);
            Assert.Null(clock.NextDueTime);
        }
        finally
        {
            staleCallback.Invoke();
            await staleDisposal.WaitAsync(_watchdog);
        }
        Assert.Equal(4, calls);

        int afterFailure = 0;
        using ITimer throwingTimer = clock.CreateTimer(_ => throw new InvalidOperationException("controlled"),
            null, TimeSpan.Zero, Timeout.InfiniteTimeSpan);
        using ITimer followingTimer = clock.CreateTimer(_ => afterFailure++, null, TimeSpan.Zero, Timeout.InfiniteTimeSpan);
        AggregateException failure = Assert.Throws<AggregateException>(() => clock.RunDueCallbacks());
        Assert.IsType<InvalidOperationException>(Assert.Single(failure.InnerExceptions));
        Assert.Equal(1, afterFailure);
        Assert.True(clock.WaitForCallbacksAsync().IsCompletedSuccessfully);
    }
}
