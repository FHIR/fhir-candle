// <copyright file="NotificationScheduler.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using FhirCandle.Configuration;
using FhirCandle.Models;
using FhirCandle.Storage;

namespace fhir.candle.Services;

/// <summary>Bounded waiting shares and a single pool of delivery workers for all hosted tenants.</summary>
internal sealed class NotificationScheduler
{
    private sealed class TenantQueue(int capacity)
    {
        internal int Capacity { get; } = capacity;
        internal Queue<(IFhirStore Store, SubscriptionSendEventArgs Args)> Requests { get; } = new();
        internal Dictionary<ParsedSubscription.NotificationTypeCodes, DateTimeOffset> LastWarning { get; } = [];
        internal long Refused;
    }

    // Producers also hold this lock while updating their revision reservations.
    internal object Gate { get; } = new();
    private readonly ILogger _logger;
    private readonly TimeProvider _clock;
    private readonly Func<IFhirStore, SubscriptionSendEventArgs, CancellationToken, Task> _deliver;
    private readonly Dictionary<string, TenantQueue> _byName = new(StringComparer.Ordinal);
    private readonly TenantQueue[] _queues;
    private readonly int _concurrency;
    private readonly CancellationTokenSource _sendCancellation = new();
    private Task? _cancellation;
    private Task _workers = Task.CompletedTask;
    private Task? _shutdown;
    private TaskCompletionSource? _workAvailable;
    private int _nextTenant;
    private int _waiting;
    private int _active;
    private long _refused;
    private bool _paused;
    private readonly Queue<TaskCompletionSource> _manualPermits = new();

    internal NotificationScheduler(CandleConfig config, IEnumerable<string> tenants, ILogger logger, TimeProvider clock,
        Func<IFhirStore, SubscriptionSendEventArgs, CancellationToken, Task> deliver)
    {
        string[] names = tenants.Order(StringComparer.Ordinal).ToArray();
        config.ValidateNotificationBudgets(names.Length);
        _concurrency = config.NotificationConcurrency;
        _deliver = deliver;
        _clock = clock;
        _logger = logger;
        _queues = new TenantQueue[names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            TenantQueue queue = new(config.NotificationQueueCapacity / names.Length +
                (i < config.NotificationQueueCapacity % names.Length ? 1 : 0));
            _byName.Add(names[i], queue);
            _queues[i] = queue;
        }
    }

    internal bool IsStopped { get; private set; }
    internal int WaitingCount { get { lock (Gate) { return _waiting; } } }
    internal int ActiveCount { get { lock (Gate) { return _active; } } }
    internal long RefusedCount { get { lock (Gate) { return _refused; } } }
    internal int WaitingFor(string tenant) { lock (Gate) { return _byName[tenant].Requests.Count; } }

    internal void Start()
    {
        _workers = Task.WhenAll(Enumerable.Range(0, _concurrency).Select(_ => Task.Run(WorkerAsync)));
    }

    internal sealed record SaturationWarning(string Tenant, ParsedSubscription.NotificationTypeCodes Kind, int Depth, long RefusedCount);

    internal bool TryEnqueue(IFhirStore store, SubscriptionSendEventArgs args, out SaturationWarning? warning)
    {
        lock (Gate)
        {
            warning = null;
            if (IsStopped)
            {
                return false;
            }
            string tenant = args.Tenant.ControllerName;
            TenantQueue queue = _byName[tenant];
            if (queue.Requests.Count >= queue.Capacity)
            {
                _refused++;
                queue.Refused++;
                DateTimeOffset now = _clock.GetUtcNow();
                if (!queue.LastWarning.TryGetValue(args.NotificationType, out DateTimeOffset last) ||
                    now - last >= TimeSpan.FromMinutes(1))
                {
                    queue.LastWarning[args.NotificationType] = now;
                    warning = new(tenant, args.NotificationType, queue.Requests.Count, queue.Refused);
                }
                return false;
            }
            queue.Requests.Enqueue((store, args));
            _waiting++;
            if (!_paused)
            {
                WakeWorkers();
            }
            return true;
        }
    }

    internal void EmitWarning(SaturationWarning? warning)
    {
        if (warning is not null)
        {
            _logger.LogWarning("queue_saturated tenant={Tenant} kind={Kind} depth={Depth} refused={RefusedCount}",
                warning.Tenant, warning.Kind, warning.Depth, warning.RefusedCount);
        }
    }

    private void WakeWorkers()
    {
        _workAvailable?.TrySetResult();
        _workAvailable = null;
    }

    private async Task WorkerAsync()
    {
        while (true)
        {
            TaskCompletionSource? permit;
            (IFhirStore Store, SubscriptionSendEventArgs Args) request;
            while (true)
            {
                Task wait;
                lock (Gate)
                {
                    if (IsStopped)
                    {
                        return;
                    }
                    if (_waiting != 0 && (!_paused || _manualPermits.Count != 0))
                    {
                        _manualPermits.TryDequeue(out permit);
                        request = DequeueNextTenant();
                        _waiting--;
                        _active++;
                        break;
                    }
                    _workAvailable ??= new(TaskCreationOptions.RunContinuationsAsynchronously);
                    wait = _workAvailable.Task;
                }
                await wait.ConfigureAwait(false);
            }
            try
            {
                await _deliver(request.Store, request.Args, _sendCancellation.Token).ConfigureAwait(false);
            }
            finally
            {
                lock (Gate)
                {
                    _active--;
                    permit?.TrySetResult();
                }
            }
        }
    }

    private (IFhirStore Store, SubscriptionSendEventArgs Args) DequeueNextTenant()
    {
        TenantQueue queue;
        do
        {
            queue = _queues[_nextTenant];
            _nextTenant = (_nextTenant + 1) % _queues.Length;
        }
        while (queue.Requests.Count == 0);
        return queue.Requests.Dequeue();
    }

    // The manual gate changes only worker eligibility, not dequeue, delivery, or drain ownership.
    internal void PauseWorkers() { lock (Gate) { _paused = true; } }
    internal void ResumeWorkers()
    {
        lock (Gate)
        {
            _paused = false;
            WakeWorkers();
        }
    }

    internal Task DrainOneAsync()
    {
        lock (Gate)
        {
            if (!_paused || _waiting <= _manualPermits.Count)
            {
                throw new InvalidOperationException("A manual drain requires paused workers and a waiting notification.");
            }
            TaskCompletionSource permit = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _manualPermits.Enqueue(permit);
            WakeWorkers();
            return permit.Task;
        }
    }

    private Task BeginShutdown(Func<ValueTask> detachProducers)
    {
        lock (Gate)
        {
            if (_shutdown is not null)
            {
                return _shutdown;
            }
            IsStopped = true;
            if (_waiting != 0)
            {
                _logger.LogWarning("queue_discarded: discarded {DiscardedCount} waiting notifications during shutdown", _waiting);
            }
            foreach (TenantQueue queue in _queues)
            {
                queue.Requests.Clear();
            }
            _waiting = 0;
            while (_manualPermits.TryDequeue(out TaskCompletionSource? permit))
            {
                permit.TrySetCanceled();
            }
            WakeWorkers();
            _shutdown = Task.WhenAll(_workers, detachProducers().AsTask());
            return _shutdown;
        }
    }

    internal async Task StopAsync(Func<ValueTask> detachProducers, CancellationToken cancellationToken)
    {
        Task shutdown = BeginShutdown(detachProducers);
        try
        {
            await shutdown.WaitAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The caller may leave immediately. Disposal still joins both the callbacks and workers.
            _ = CancelSends();
            throw;
        }
    }

    private Task CancelSends()
    {
        lock (Gate)
        {
            return _cancellation ??= _sendCancellation.CancelAsync();
        }
    }

    internal void DisposeAfterDrain(Func<ValueTask> detachProducers)
    {
        Task shutdown = BeginShutdown(detachProducers);
        Task.WhenAll(shutdown, CancelSends()).GetAwaiter().GetResult();
        lock (Gate)
        {
            _sendCancellation.Dispose();
        }
    }
}
