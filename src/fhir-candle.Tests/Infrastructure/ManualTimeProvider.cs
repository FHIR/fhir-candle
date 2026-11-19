// <copyright file="ManualTimeProvider.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

namespace fhir.candle.Tests.Infrastructure;

/// <summary>
/// A controlled UTC clock. Advancing time, capturing due callbacks, and invoking them are separate
/// operations; no callback runs automatically or under the timer lock.
/// </summary>
internal sealed class ManualTimeProvider : TimeProvider
{
    /// <summary>The largest interval supported by a platform timer.</summary>
    internal static readonly TimeSpan MaximumTimerInterval = TimeSpan.FromMilliseconds(uint.MaxValue - 1);

    private readonly object _timerLock = new();
    private readonly List<ManualTimer> _timers = [];
    private DateTimeOffset _utcNow;

    /// <summary>Creates a clock at a fixed instant, normalized to offset zero.</summary>
    /// <param name="utcNow">The initial instant.</param>
    internal ManualTimeProvider(DateTimeOffset utcNow)
    {
        _utcNow = utcNow.ToUniversalTime();
    }

    /// <summary>Gets the UTC local time zone, independent of the test machine.</summary>
    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

    /// <summary>Gets the number of timestamp ticks per second.</summary>
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    /// <summary>Gets the controlled UTC instant.</summary>
    /// <returns>The current instant at offset zero.</returns>
    public override DateTimeOffset GetUtcNow()
    {
        lock (_timerLock)
        {
            return _utcNow;
        }
    }

    /// <summary>Gets a timestamp that advances only with the controlled clock.</summary>
    /// <returns>The current UTC tick count.</returns>
    public override long GetTimestamp() => GetUtcNow().UtcTicks;

    /// <summary>
    /// Gets the earliest scheduled instant, or null when all timers are disabled/disposed.
    /// Captured callbacks are no longer scheduled. Instants beyond the UTC range display as its maximum.
    /// </summary>
    internal DateTimeOffset? NextDueTime
    {
        get
        {
            lock (_timerLock)
            {
                long? nextTicks = null;
                foreach (ManualTimer timer in _timers)
                {
                    if (timer.NextDueTicks is long dueTicks && (nextTicks is null || dueTicks < nextTicks.Value))
                    {
                        nextTicks = dueTicks;
                    }
                }

                return nextTicks is null
                    ? null
                    : new DateTimeOffset(Math.Min(nextTicks.Value, DateTimeOffset.MaxValue.UtcTicks), TimeSpan.Zero);
            }
        }
    }

    /// <summary>Advances UTC and timestamps without capturing or invoking any callback.</summary>
    /// <param name="elapsed">A nonnegative duration within the representable UTC range.</param>
    internal void Advance(TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(elapsed));
        }

        lock (_timerLock)
        {
            _utcNow = _utcNow.Add(elapsed);
        }
    }

    /// <summary>Creates a timer using the platform timer interval limits.</summary>
    /// <param name="callback">The synchronous callback to invoke outside timer protection.</param>
    /// <param name="state">The state passed unchanged to each callback.</param>
    /// <param name="dueTime">The first delay, or infinite to disable.</param>
    /// <param name="period">The repeat interval; zero or infinite makes the timer one-shot.</param>
    /// <returns>A manually driven timer.</returns>
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        ArgumentNullException.ThrowIfNull(callback, nameof(callback));
        ValidateInterval(dueTime, nameof(dueTime));
        ValidateInterval(period, nameof(period));

        lock (_timerLock)
        {
            ManualTimer timer = new(this, callback, state);
            timer.SetSchedule(dueTime, period);
            _timers.Add(timer);
            return timer;
        }
    }

    /// <summary>
    /// Captures one callback per due timer in due-time order, preserving registration order for ties.
    /// Missed periodic ticks coalesce; the next tick retains the original cadence and is after now.
    /// Captured work survives Change/Dispose, just like work already queued by a real timer.
    /// </summary>
    /// <returns>Callbacks that can be held and individually invoked exactly once.</returns>
    internal IReadOnlyList<CapturedCallback> CaptureDueCallbacks()
    {
        lock (_timerLock)
        {
            ManualTimer[] dueTimers = _timers
                .Where(timer => timer.NextDueTicks is long dueTicks && dueTicks <= _utcNow.UtcTicks)
                .OrderBy(timer => timer.NextDueTicks)
                .ToArray();
            List<CapturedCallback> callbacks = [];
            foreach (ManualTimer timer in dueTimers)
            {
                callbacks.Add(timer.Capture());
            }

            return callbacks;
        }
    }

    /// <summary>Captures and invokes one due batch, observing every callback even if one throws.</summary>
    /// <returns>The number of callbacks invoked.</returns>
    internal int RunDueCallbacks()
    {
        IReadOnlyList<CapturedCallback> callbacks = CaptureDueCallbacks();
        List<Exception> errors = [];
        foreach (CapturedCallback callback in callbacks)
        {
            try
            {
                callback.Invoke();
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }
        }

        if (errors.Count != 0)
        {
            throw new AggregateException(errors);
        }

        return callbacks.Count;
    }

    /// <summary>
    /// Waits for all currently captured/running callbacks, including callbacks of disposed timers.
    /// Future captures are not part of this rendezvous; uncaptured due timers do not delay it.
    /// </summary>
    /// <returns>A task completed when the current callback batches have drained.</returns>
    internal Task WaitForCallbacksAsync()
    {
        lock (_timerLock)
        {
            return Task.WhenAll(_timers.Select(timer => timer.DrainTask).ToArray());
        }
    }

    private static void ValidateInterval(TimeSpan interval, string parameterName)
    {
        if (interval != Timeout.InfiniteTimeSpan && (interval < TimeSpan.Zero || interval > MaximumTimerInterval))
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    /// <summary>A captured callback that can be released independently of its timer's schedule.</summary>
    internal sealed class CapturedCallback
    {
        private readonly TimerCallback _callback;
        private readonly object? _state;
        private readonly Action _complete;
        private int _invoked;

        internal CapturedCallback(TimerCallback callback, object? state, Action complete)
        {
            _callback = callback;
            _state = state;
            _complete = complete;
        }

        /// <summary>Invokes the captured work once and signals drain even if the callback throws.</summary>
        internal void Invoke()
        {
            if (Interlocked.Exchange(ref _invoked, 1) != 0)
            {
                throw new InvalidOperationException("The captured timer callback has already been invoked.");
            }

            try
            {
                _callback(_state);
            }
            finally
            {
                _complete();
            }
        }
    }

    private sealed class ManualTimer : ITimer
    {
        private readonly ManualTimeProvider _owner;
        private readonly TimerCallback _callback;
        private readonly object? _state;
        private TimeSpan _period;
        private bool _disposed;
        private int _pendingCallbacks;
        private TaskCompletionSource? _drained;

        internal ManualTimer(ManualTimeProvider owner, TimerCallback callback, object? state)
        {
            _owner = owner;
            _callback = callback;
            _state = state;
        }

        internal long? NextDueTicks { get; private set; }

        internal Task DrainTask => _drained?.Task ?? Task.CompletedTask;

        internal void SetSchedule(TimeSpan dueTime, TimeSpan period)
        {
            NextDueTicks = dueTime == Timeout.InfiniteTimeSpan ? null : _owner._utcNow.UtcTicks + dueTime.Ticks;
            _period = period;
        }

        internal CapturedCallback Capture()
        {
            long dueTicks = NextDueTicks!.Value;
            if (_period == Timeout.InfiniteTimeSpan || _period == TimeSpan.Zero)
            {
                NextDueTicks = null;
            }
            else
            {
                long nowTicks = _owner._utcNow.UtcTicks;
                long untilNextTicks = _period.Ticks - ((nowTicks - dueTicks) % _period.Ticks);
                NextDueTicks = nowTicks + untilNextTicks;
            }

            if (_pendingCallbacks++ == 0)
            {
                _drained = new(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            return new(_callback, _state, CompleteCallback);
        }

        /// <summary>Changes future scheduling without retracting captured callbacks; false after disposal.</summary>
        /// <param name="dueTime">The first delay, or infinite to disable.</param>
        /// <param name="period">The repeat interval; zero or infinite makes the timer one-shot.</param>
        /// <returns>True if the timer was not disposed.</returns>
        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            ValidateInterval(dueTime, nameof(dueTime));
            ValidateInterval(period, nameof(period));
            lock (_owner._timerLock)
            {
                if (_disposed)
                {
                    return false;
                }

                SetSchedule(dueTime, period);
                return true;
            }
        }

        /// <summary>Stops future scheduling without waiting for already captured callbacks.</summary>
        public void Dispose()
        {
            lock (_owner._timerLock)
            {
                Stop();
            }
        }

        /// <summary>Stops scheduling and waits for all captured/running callbacks of this timer.</summary>
        /// <returns>A rendezvous that completes after callback bodies have returned.</returns>
        public ValueTask DisposeAsync()
        {
            lock (_owner._timerLock)
            {
                Stop();
                return new(DrainTask);
            }
        }

        private void Stop()
        {
            _disposed = true;
            NextDueTicks = null;
            if (_pendingCallbacks == 0)
            {
                _owner._timers.Remove(this);
            }
        }

        private void CompleteCallback()
        {
            lock (_owner._timerLock)
            {
                if (--_pendingCallbacks == 0)
                {
                    _drained!.TrySetResult();
                    if (_disposed)
                    {
                        _owner._timers.Remove(this);
                    }
                }
            }
        }
    }
}
