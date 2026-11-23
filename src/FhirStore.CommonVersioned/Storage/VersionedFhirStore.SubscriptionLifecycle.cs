// <copyright file="VersionedFhirStore.SubscriptionLifecycle.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using FhirCandle.Models;
using Hl7.Fhir.Model;
using Task = System.Threading.Tasks.Task;

namespace FhirCandle.Storage;

public partial class VersionedFhirStore
{
    // Ordering: tenant write gate -> lifecycle (if needed) -> typed mutation -> registration/history.
    // Publication and awaiting transport/timer tasks always happen outside these locks.
    internal readonly object SubscriptionLifecycleLock = new();

    internal readonly struct LifecycleScope(object? gate) : IDisposable
    {
        public void Dispose()
        {
            if (gate is not null)
            {
                Monitor.Exit(gate);
            }
        }
    }

    internal LifecycleScope EnterLifecycle(bool needed)
    {
        if (needed)
        {
            Monitor.Enter(SubscriptionLifecycleLock);
        }
        return new(needed ? SubscriptionLifecycleLock : null);
    }
    private readonly TimeProvider _clock;
    private readonly ITimer _subscriptionTimer;
    private long _subscriptionRevision;
    private bool _lifecycleStopped;
    private readonly TaskCompletionSource _subscriptionTimerDrained = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static readonly TimeSpan _maximumTimerWait = TimeSpan.FromMilliseconds(uint.MaxValue - 1);
    private Action<string> _subscriptionDiagnosticWriter = Console.WriteLine;

    private static string RecordSubscriptionSendLocked(ParsedSubscription subscription, long eventNumber, string content)
    {
        subscription.SerializedEvents[eventNumber] = content;
        return $"Sending content for event: Subscription/{subscription.Id}:{eventNumber}: \n-----\n{content}\n-----";
    }

    private static string RecordSubscriptionErrorLocked(ParsedSubscription subscription, string error)
    {
        subscription.NotificationErrors.Add(error);
        return $" <<< Subscription/{subscription.Id}: {error}";
    }

    private long NextSubscriptionRevisionLocked() => checked(++_subscriptionRevision);

    internal void ValidateSubscriptionCommitLocked()
    {
        ObjectDisposedException.ThrowIf(_lifecycleStopped, this);
        _ = checked(_subscriptionRevision + 1);
    }

    private SubscriptionChangedEventArgs SubscriptionChangeLocked(ParsedSubscription subscription, bool removed = false) => new()
    {
        Tenant = _config,
        ChangedSubscription = removed ? null : subscription,
        RemovedSubscriptionId = removed ? subscription.Id : null,
        Revision = subscription.Revision,
        SendHandshake = !removed && subscription.CurrentStatus == "requested",
    };

    internal void PublishSubscriptionChanges(IEnumerable<SubscriptionChangedEventArgs> changes, List<string> diagnostics)
    {
        foreach (SubscriptionChangedEventArgs change in changes)
        {
            AfterMutation(() => OnSubscriptionsChanged?.Invoke(this, change));
        }
        foreach (string diagnostic in diagnostics)
        {
            AfterMutation(() => _subscriptionDiagnosticWriter(diagnostic), diagnostic: true);
        }
    }

    private void RemoveSubscriptionBindingsLocked(ParsedSubscription subscription)
    {
        foreach (IVersionedResourceStore resourceStore in _store.Values)
        {
            resourceStore.RemoveExecutableSubscription(subscription.TopicUrl, subscription.Id);
        }
    }

    private void SetStoredSubscriptionStatusLocked(string id, string status)
    {
        if (_store.TryGetValue("Subscription", out IVersionedResourceStore? resourceStore) &&
            resourceStore.ReadRawSubscription(id) is Subscription resource)
        {
            _subscriptionConverter.UpdateResourceStatus(resource, status);
        }
    }

    /// <summary>Called only inside the Subscription resource mutation/lifecycle commit boundary.</summary>
    internal bool CommitSubscriptionLocked(
        ParsedSubscription subscription,
        Subscription? resource,
        bool remove,
        List<SubscriptionChangedEventArgs> changes,
        List<string> diagnostics)
    {
        if (_lifecycleStopped || !_subscriptionConverter.IsValidStatus(subscription.CurrentStatus) ||
            !SubscriptionLifecyclePolicy.IsValidExpirationTicks(subscription.ExpirationTicks))
        {
            return false;
        }

        _subscriptions.TryGetValue(subscription.Id, out ParsedSubscription? prior);
        // Deferred loading may retain a descriptor after replacement or deletion. Only a
        // never-issued direct-call descriptor may become a new parsed-only registration.
        if (resource is null && subscription.Revision != 0 && !ReferenceEquals(prior, subscription))
        {
            return false;
        }
        if (remove)
        {
            if (prior is null)
            {
                return false;
            }
            long revision = NextSubscriptionRevisionLocked();
            RemoveSubscriptionBindingsLocked(prior);
            _subscriptions.TryRemove(prior.Id, out _);
            prior.Revision = revision;
            changes.Add(SubscriptionChangeLocked(prior, true));
            RearmSubscriptionTimerLocked();
            return true;
        }

        // Reprocessing an installed descriptor is not a replacement or a renewal.
        if (ReferenceEquals(prior, subscription) && resource is null)
        {
            NormalizeSubscriptionLocked(subscription, _clock.GetUtcNow(), changes, diagnostics, deferTimerRearm: true);
            RemoveSubscriptionBindingsLocked(subscription);
            bool executable = InstallSubscriptionBindingsLocked(subscription, diagnostics);
            RearmSubscriptionTimerLocked();
            return executable;
        }

        DateTimeOffset now = _clock.GetUtcNow();
        long deadline = resource is not null
            ? _subscriptionConverter.ExpirationFor(resource, now)
            : SubscriptionLifecyclePolicy.CalculateExpirationTicks(now, _config.MaxSubscriptionExpirationMinutes,
                SubscriptionLifecyclePolicy.GetExpiration(subscription.ExpirationTicks));
        long nextRevision = NextSubscriptionRevisionLocked();
        if (prior is not null)
        {
            RemoveSubscriptionBindingsLocked(prior);
        }
        subscription.ExpirationTicks = deadline;
        subscription.Revision = nextRevision;
        if ((subscription.CurrentStatus is "active" or "requested") &&
            SubscriptionLifecyclePolicy.IsExpired(deadline, now))
        {
            subscription.CurrentStatus = "off";
        }
        _subscriptions[subscription.Id] = subscription;
        SetStoredSubscriptionStatusLocked(subscription.Id, subscription.CurrentStatus);
        bool installed = InstallSubscriptionBindingsLocked(subscription, diagnostics);
        changes.Add(SubscriptionChangeLocked(subscription));
        RearmSubscriptionTimerLocked();
        return installed;
    }

    private bool ChangeSubscriptionStatusLocked(
        ParsedSubscription subscription, string status, List<SubscriptionChangedEventArgs> changes, List<string> diagnostics,
        bool deferTimerRearm = false)
    {
        if (_lifecycleStopped || !_subscriptionConverter.IsValidStatus(status))
        {
            return false;
        }
        if ((status is "active" or "requested") &&
            SubscriptionLifecyclePolicy.IsExpired(subscription.ExpirationTicks, _clock.GetUtcNow()))
        {
            status = "off";
        }
        if (subscription.CurrentStatus == status)
        {
            return false;
        }

        long revision = NextSubscriptionRevisionLocked();
        RemoveSubscriptionBindingsLocked(subscription);
        subscription.CurrentStatus = status;
        subscription.Revision = revision;
        SetStoredSubscriptionStatusLocked(subscription.Id, status);
        InstallSubscriptionBindingsLocked(subscription, diagnostics);
        changes.Add(SubscriptionChangeLocked(subscription));
        if (!deferTimerRearm)
        {
            RearmSubscriptionTimerLocked();
        }
        return true;
    }

    private bool NormalizeSubscriptionLocked(
        ParsedSubscription subscription, DateTimeOffset now, List<SubscriptionChangedEventArgs> changes, List<string> diagnostics,
        bool deferTimerRearm = false)
    {
        return !_lifecycleStopped && (subscription.CurrentStatus is "active" or "requested") &&
            SubscriptionLifecyclePolicy.IsExpired(subscription.ExpirationTicks, now) &&
            ChangeSubscriptionStatusLocked(subscription, "off", changes, diagnostics, deferTimerRearm);
    }

    private void NormalizeSubscriptionsLocked(List<SubscriptionChangedEventArgs> changes, List<string> diagnostics, bool deferTimerRearm = false)
    {
        DateTimeOffset now = _clock.GetUtcNow();
        bool changed = false;
        foreach (ParsedSubscription subscription in _subscriptions.Values)
        {
            changed |= NormalizeSubscriptionLocked(subscription, now, changes, diagnostics, deferTimerRearm: true);
        }
        if (changed && !deferTimerRearm)
        {
            RearmSubscriptionTimerLocked();
        }
    }

    private ParsedSubscription[] SubscriptionSnapshots()
    {
        lock (SubscriptionLifecycleLock)
        {
            DateTimeOffset now = _clock.GetUtcNow();
            ParsedSubscription[] current = _subscriptions.Values.ToArray();
            if (_lifecycleStopped || !current.Any(subscription => subscription.CurrentStatus is ("active" or "requested") &&
                SubscriptionLifecyclePolicy.IsExpired(subscription.ExpirationTicks, now)))
            {
                return current;
            }
        }
        using MutationScope mutation = BeginMutation();
        List<SubscriptionChangedEventArgs> changes = [];
        List<string> diagnostics = [];
        ParsedSubscription[] subscriptions;
        lock (SubscriptionLifecycleLock)
        {
            NormalizeSubscriptionsLocked(changes, diagnostics);
            subscriptions = _subscriptions.Values.ToArray();
        }
        PublishSubscriptionChanges(changes, diagnostics);
        return subscriptions;
    }

    internal Subscription? SubscriptionResourceSnapshot(string id)
        => SubscriptionForSearch(id, _clock.GetUtcNow(), detach: true);

    internal Subscription? SubscriptionForSearch(string id, DateTimeOffset instant, bool detach = false, Action? onDetach = null)
    {
        List<SubscriptionChangedEventArgs> changes = [];
        List<string> diagnostics = [];
        Subscription? snapshot;
        using (MutationScope mutation = BeginMutation())
        {
            lock (SubscriptionLifecycleLock)
            {
                if (_subscriptions.TryGetValue(id, out ParsedSubscription? subscription))
                {
                    NormalizeSubscriptionLocked(subscription, instant, changes, diagnostics);
                }
                snapshot = _store.TryGetValue("Subscription", out IVersionedResourceStore? resourceStore)
                    ? resourceStore.ReadRawSubscription(id)
                    : null;
                if (detach && snapshot is not null)
                {
                    snapshot = (Subscription)snapshot.DeepCopy();
                }
            }
            PublishSubscriptionChanges(changes, diagnostics);
        }
        if (detach && snapshot is not null)
        {
            onDetach?.Invoke();
        }
        return snapshot;
    }

    internal Subscription[] SubscriptionResourceSnapshots()
    {
        using MutationScope mutation = BeginMutation();
        List<SubscriptionChangedEventArgs> changes = [];
        List<string> diagnostics = [];
        Subscription[] snapshots;
        lock (SubscriptionLifecycleLock)
        {
            NormalizeSubscriptionsLocked(changes, diagnostics);
            snapshots = _store.TryGetValue("Subscription", out IVersionedResourceStore? resourceStore)
                ? ((IReadOnlyDictionary<string, Resource>)resourceStore).Values.Cast<Subscription>()
                    .Select(resource => (Subscription)resource.DeepCopy()).ToArray()
                : [];
        }
        PublishSubscriptionChanges(changes, diagnostics);
        return snapshots;
    }

    internal (string Id, string Status, Resource Resource)[] SubscriptionStatusSnapshots(string notificationType)
    {
        using MutationScope mutation = BeginMutation();
        List<SubscriptionChangedEventArgs> changes = [];
        List<string> diagnostics = [];
        (string, string, Resource)[] snapshots;
        lock (SubscriptionLifecycleLock)
        {
            NormalizeSubscriptionsLocked(changes, diagnostics);
            snapshots = _subscriptions.Values.Select(subscription =>
                (subscription.Id, subscription.CurrentStatus,
                    _subscriptionConverter.StatusForSubscription(subscription, notificationType, _config.BaseUrl))).ToArray();
        }
        PublishSubscriptionChanges(changes, diagnostics);
        return snapshots;
    }

    internal void AcceptSubscriptionEvent(
        string id, string topicUrl, long revision, Resource focus, IEnumerable<object> additionalContext)
    {
        using MutationScope mutation = BeginMutation();
        List<SubscriptionChangedEventArgs> changes = [];
        List<string> diagnostics = [];
        SubscriptionSendEventArgs? send = null;
        lock (SubscriptionLifecycleLock)
        {
            if (!_lifecycleStopped && _subscriptions.TryGetValue(id, out ParsedSubscription? subscription))
            {
                DateTimeOffset now = _clock.GetUtcNow();
                NormalizeSubscriptionLocked(subscription, now, changes, diagnostics);
                if (subscription.Revision == revision && subscription.TopicUrl == topicUrl &&
                    SubscriptionLifecyclePolicy.CanGenerateEvents(subscription.CurrentStatus, subscription.ExpirationTicks, now))
                {
                    lock (subscription.GeneratedEvents)
                    {
                        SubscriptionEvent subscriptionEvent = new()
                        {
                            SubscriptionId = id,
                            TopicUrl = topicUrl,
                            EventNumber = subscription.IncrementEventCount(),
                            Timestamp = now,
                            Focus = focus,
                            AdditionalContext = additionalContext,
                        };
                        subscription.RegisterEvent(subscriptionEvent);
                        send = SendArgumentsLocked(subscription, subscriptionEvent);
                    }
                }
            }
        }
        PublishSubscriptionChanges(changes, diagnostics);
        if (send is not null)
        {
            AfterMutation(() => OnSubscriptionSendEvent?.Invoke(this, send));
        }
    }

    private SubscriptionSendEventArgs SendArgumentsLocked(ParsedSubscription subscription, SubscriptionEvent subscriptionEvent) => new()
    {
        Tenant = _config,
        Subscription = subscription,
        Revision = subscription.Revision,
        NotificationEvents = [subscriptionEvent],
        NotificationType = ParsedSubscription.NotificationTypeCodes.EventNotification,
    };

    internal void RegisterSubscriptionExecutionError(string id, long revision, string error)
    {
        using MutationScope mutation = BeginMutation();
        List<SubscriptionChangedEventArgs> changes = [];
        List<string> diagnostics = [];
        string? diagnostic = null;
        lock (SubscriptionLifecycleLock)
        {
            if (!_lifecycleStopped && _subscriptions.TryGetValue(id, out ParsedSubscription? subscription))
            {
                DateTimeOffset now = _clock.GetUtcNow();
                NormalizeSubscriptionLocked(subscription, now, changes, diagnostics);
                if (subscription.Revision == revision &&
                    SubscriptionLifecyclePolicy.CanGenerateEvents(subscription.CurrentStatus, subscription.ExpirationTicks, now))
                {
                    diagnostic = RecordSubscriptionErrorLocked(subscription, error);
                }
            }
        }
        PublishSubscriptionChanges(changes, diagnostics);
        if (diagnostic is not null)
        {
            AfterMutation(() => _subscriptionDiagnosticWriter(diagnostic), diagnostic: true);
        }
    }

    private bool CanDispatchLocked(SubscriptionSendEventArgs args, List<SubscriptionChangedEventArgs> changes, List<string> diagnostics)
    {
        if (_lifecycleStopped || !_subscriptions.TryGetValue(args.Subscription.Id, out ParsedSubscription? current) ||
            !ReferenceEquals(current, args.Subscription))
        {
            return false;
        }
        DateTimeOffset now = _clock.GetUtcNow();
        NormalizeSubscriptionLocked(current, now, changes, diagnostics);
        return current.Revision == args.Revision &&
            SubscriptionLifecyclePolicy.CanDeliverNotification(current.CurrentStatus, current.ExpirationTicks, args.NotificationType, now);
    }

    /// <inheritdoc/>
    public bool TryDispatchSubscriptionNotification(
        SubscriptionSendEventArgs args,
        Func<Task<SubscriptionNotificationOutcome>> startTransport,
        out Task<SubscriptionNotificationOutcome> deliveryTask)
    {
        ArgumentNullException.ThrowIfNull(startTransport);
        List<SubscriptionChangedEventArgs> changes = [];
        List<string> diagnostics = [];
        Task<SubscriptionNotificationOutcome>? operation = null;
        using (BeginMutation())
        {
            lock (SubscriptionLifecycleLock)
            {
                if (CanDispatchLocked(args, changes, diagnostics))
                {
                    try
                    {
                        operation = startTransport();
                    }
                    catch (Exception exception)
                    {
                        operation = Task.FromException<SubscriptionNotificationOutcome>(exception);
                    }
                }
            }
            PublishSubscriptionChanges(changes, diagnostics);
        }
        // Even an already-completed or synchronously throwing starter is observed after unlock.
        deliveryTask = operation is null
            ? Task.FromResult(new SubscriptionNotificationOutcome(SubscriptionNotificationOutcome.OutcomeKind.Suppressed))
            : CompleteSubscriptionDispatchAsync(args, operation);
        return operation is not null;
    }

    private async Task<SubscriptionNotificationOutcome> CompleteSubscriptionDispatchAsync(
        SubscriptionSendEventArgs args, Task<SubscriptionNotificationOutcome> operation)
    {
        SubscriptionNotificationOutcome outcome;
        try
        {
            outcome = await operation.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            outcome = new(SubscriptionNotificationOutcome.OutcomeKind.Failed, Error: exception.Message);
        }

        List<SubscriptionChangedEventArgs> changes = [];
        List<string> diagnostics = [];
        using MutationScope mutation = BeginMutation();
        lock (SubscriptionLifecycleLock)
        {
            if (!CanDispatchLocked(args, changes, diagnostics))
            {
                outcome = new(SubscriptionNotificationOutcome.OutcomeKind.Suppressed);
            }
            else
            {
                if (outcome.Kind == SubscriptionNotificationOutcome.OutcomeKind.Delivered)
                {
                    args.Subscription.LastCommunicationTicks = _clock.GetUtcNow().UtcTicks;
                }
                if (outcome.SerializedContent is not null)
                {
                    long number = args.NotificationEvents.Select(subscriptionEvent => subscriptionEvent.EventNumber).DefaultIfEmpty(0).Max();
                    diagnostics.Add(RecordSubscriptionSendLocked(args.Subscription, number, outcome.SerializedContent));
                }
                if (outcome.Kind == SubscriptionNotificationOutcome.OutcomeKind.Failed && !string.IsNullOrEmpty(outcome.Error))
                {
                    diagnostics.Add(RecordSubscriptionErrorLocked(args.Subscription, outcome.Error));
                }
            }
        }
        PublishSubscriptionChanges(changes, diagnostics);
        return outcome;
    }

    /// <inheritdoc/>
    public bool TryCompleteSubscriptionHandshake(SubscriptionSendEventArgs args)
    {
        using MutationScope mutation = BeginMutation();
        List<SubscriptionChangedEventArgs> changes = [];
        List<string> diagnostics = [];
        bool activated;
        lock (SubscriptionLifecycleLock)
        {
            activated = args.NotificationType == ParsedSubscription.NotificationTypeCodes.Handshake &&
                CanDispatchLocked(args, changes, diagnostics) &&
                ChangeSubscriptionStatusLocked(args.Subscription, "active", changes, diagnostics);
        }
        PublishSubscriptionChanges(changes, diagnostics);
        return activated;
    }

    private (Bundle? Bundle, string ContentType) BuildSubscriptionEvents(
        string id, IEnumerable<long> numbers, string notificationType, string contentLevel, bool allowEmptySelectionFallback)
    {
        Bundle? bundle = null;
        string contentType = string.Empty;
        ReadSubscriptionHistory(id, subscription =>
        {
            contentType = subscription.ContentType;
            bundle = _subscriptionConverter.BundleForSubscriptionEvents(subscription, numbers, notificationType,
                _config.BaseUrl, contentLevel, allowEmptySelectionFallback);
            bundle = (Bundle?)bundle?.DeepCopy();
        });
        return (bundle, contentType);
    }

    internal Bundle? RetainedSubscriptionEvents(string id, long since, long? until, string contentLevel)
    {
        Bundle? bundle = null;
        ReadSubscriptionHistory(id, subscription =>
        {
            long upperBound = Math.Min(until ?? subscription.CurrentEventCount, subscription.CurrentEventCount);
            long[] numbers = since > upperBound ? [] : subscription.GeneratedEvents.Keys
                .Where(number => since <= number && number <= upperBound).OrderBy(number => number).ToArray();
            bundle = _subscriptionConverter.BundleForSubscriptionEvents(subscription, numbers, "query-event",
                _config.BaseUrl, contentLevel, allowEmptySelectionFallback: false);
            bundle = (Bundle?)bundle?.DeepCopy();
        });
        return bundle;
    }

    // Replay needs a coherent lifecycle/history pair, not exclusive access to unrelated
    // tenant rows. If normalization is necessary, drop the read locks before mutating.
    private void ReadSubscriptionHistory(string id, Action<ParsedSubscription> read)
    {
        while (true)
        {
            lock (SubscriptionLifecycleLock)
            {
                if (!_subscriptions.TryGetValue(id, out ParsedSubscription? subscription))
                {
                    return;
                }
                DateTimeOffset now = _clock.GetUtcNow();
                if (_lifecycleStopped || subscription.CurrentStatus is not ("active" or "requested") ||
                    !SubscriptionLifecyclePolicy.IsExpired(subscription.ExpirationTicks, now))
                {
                    lock (subscription.GeneratedEvents)
                    {
                        read(subscription);
                    }
                    return;
                }
            }
            using MutationScope mutation = BeginMutation();
            List<SubscriptionChangedEventArgs> changes = [];
            List<string> diagnostics = [];
            lock (SubscriptionLifecycleLock)
            {
                if (_subscriptions.TryGetValue(id, out ParsedSubscription? current))
                {
                    NormalizeSubscriptionLocked(current, _clock.GetUtcNow(), changes, diagnostics);
                }
            }
            PublishSubscriptionChanges(changes, diagnostics);
        }
    }

    private void RearmSubscriptionTimerLocked()
    {
        if (_lifecycleStopped)
        {
            return;
        }
        long? earliest = _subscriptions.Values
            .Where(subscription => subscription.CurrentStatus is "active" or "requested")
            .Where(subscription => subscription.ExpirationTicks != SubscriptionLifecyclePolicy.UnlimitedExpirationTicks)
            .Select(subscription => (long?)subscription.ExpirationTicks).Min();
        TimeSpan due = earliest is null ? Timeout.InfiniteTimeSpan
            : TimeSpan.FromTicks(Math.Clamp(earliest.Value - _clock.GetUtcNow().UtcTicks, 0, _maximumTimerWait.Ticks));
        _subscriptionTimer.Change(due, Timeout.InfiniteTimeSpan);
    }

    private void ExpireSubscriptions(object? state)
    {
        using MutationScope mutation = BeginMutation();
        List<SubscriptionChangedEventArgs> changes = [];
        List<string> diagnostics = [];
        lock (SubscriptionLifecycleLock)
        {
            if (_lifecycleStopped)
            {
                return;
            }
            NormalizeSubscriptionsLocked(changes, diagnostics, deferTimerRearm: true);
            RearmSubscriptionTimerLocked();
        }
        PublishSubscriptionChanges(changes, diagnostics);
    }

    private void StopSubscriptionLifecycle()
    {
        bool drain;
        using (BeginMutation())
        lock (SubscriptionLifecycleLock)
        {
            drain = !_lifecycleStopped;
            if (drain)
            {
                _lifecycleStopped = true;
                _subscriptionTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            }
        }
        if (drain)
        {
            try
            {
                _subscriptionTimer.DisposeAsync().AsTask().GetAwaiter().GetResult();
                _subscriptionTimerDrained.TrySetResult();
            }
            catch (Exception exception)
            {
                _subscriptionTimerDrained.TrySetException(exception);
            }
        }
        _subscriptionTimerDrained.Task.GetAwaiter().GetResult();
    }
}
