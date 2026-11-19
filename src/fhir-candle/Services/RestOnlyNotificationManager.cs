using FhirCandle.Models;
using FhirCandle.Storage;
using System.Text;
using fhir.candle.Models;
using System.Collections.Concurrent;
using FhirCandle.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Outcome = FhirCandle.Models.SubscriptionNotificationOutcome;
using OutcomeKind = FhirCandle.Models.SubscriptionNotificationOutcome.OutcomeKind;


namespace fhir.candle.Services;

public class RestOnlyNotificationManager : INotificationManager
{
    /// <summary>True if has disposed, false if not.</summary>
    private bool _hasDisposed = false;

    /// <summary>The HTTP client for REST notifications.</summary>
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private readonly TimeProvider _clock;
    private readonly object _schedulingLock = new();
    private readonly Dictionary<(IFhirStore Store, string Id, long Revision), long> _heartbeatReservations = [];
    private readonly Dictionary<(IFhirStore Store, string Id), long> _handshakeRevisions = [];
    private bool _stopped;
    private bool _started;

    /// <summary>The logger.</summary>
    private ILogger _logger;

    /// <summary>Manager for store.</summary>
    private IFhirStoreManager _storeManager;

    /// <summary>The heartbeat timer.</summary>
    private ITimer? _heartbeatTimer;

    private ITimer? _notificationTimer;

    private record NotificationRequest
    {
        public required IFhirStore Store { get; init; }

        public required SubscriptionSendEventArgs Args { get; init; }
    }

    private ConcurrentQueue<NotificationRequest> _notificationRequestQ = new();

    /// <summary>Initializes a new instance of the <see cref="NotificationManager"/> class.</summary>
    /// <param name="serverConfig">    The server configuration.</param>
    /// <param name="tenants">         The tenants.</param>
    /// <param name="fhirStoreManager">Manager for FHIR store.</param>
    /// <param name="logger">          The logger.</param>
    public RestOnlyNotificationManager(
        CandleConfig serverConfig,
        Dictionary<string, TenantConfiguration> tenants,
        IFhirStoreManager fhirStoreManager,
        ILogger<RestOnlyNotificationManager> logger)
        : this(serverConfig, tenants, fhirStoreManager, logger, TimeProvider.System, null)
    {
    }

    /// <summary>Creates a manager with a controlled clock and an optional caller-owned client.</summary>
    /// <param name="serverConfig">The server configuration.</param>
    /// <param name="tenants">The tenants.</param>
    /// <param name="fhirStoreManager">The store manager.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="clock">The UTC clock and service timer factory.</param>
    /// <param name="httpClient">An optional caller-owned transport client.</param>
    public RestOnlyNotificationManager(
        CandleConfig serverConfig,
        Dictionary<string, TenantConfiguration> tenants,
        IFhirStoreManager fhirStoreManager,
        ILogger<RestOnlyNotificationManager> logger,
        TimeProvider clock,
        HttpClient? httpClient)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _ownsHttpClient = httpClient is null;
        _httpClient = httpClient ?? new();
        _logger = logger;
        _storeManager = fhirStoreManager;
    }

    /// <summary>Try notify via the appropriate channel type.</summary>
    /// <param name="store">The store.</param>
    /// <param name="e">    Subscription event information.</param>
    /// <returns>The revision-fenced notification outcome.</returns>
    private async Task<Outcome> TryNotify(IFhirStore store, SubscriptionSendEventArgs e)
    {
        Outcome admission = await DispatchAsync(store, e, () => Task.FromResult(new Outcome(OutcomeKind.SetupComplete))).ConfigureAwait(false);
        if (admission.Kind == OutcomeKind.Suppressed)
        {
            return admission;
        }
        try
        {
            string notificationType = e.NotificationType switch
            {
                ParsedSubscription.NotificationTypeCodes.Handshake => "handshake",
                ParsedSubscription.NotificationTypeCodes.Heartbeat => "heartbeat",
                ParsedSubscription.NotificationTypeCodes.EventNotification => "event-notification",
                _ => throw new NotSupportedException($"Unknown delivery type: {e.NotificationType}"),
            };
            if (e.NotificationType == ParsedSubscription.NotificationTypeCodes.EventNotification && !e.NotificationEvents.Any())
            {
                throw new InvalidOperationException("Event notification has no events.");
            }
            string contents = store.SerializeSubscriptionEvents(e.Subscription.Id,
                e.NotificationEvents.Select(notificationEvent => notificationEvent.EventNumber), notificationType, false);
            if (!e.Subscription.ChannelCode.Equals("rest-hook", StringComparison.OrdinalIgnoreCase))
            {
                throw new NotSupportedException($"Unimplemented channel type: {e.Subscription.ChannelCode}");
            }
            Outcome outcome = await TryNotifyRestHook(store, e, contents).ConfigureAwait(false);
            if (e.NotificationType == ParsedSubscription.NotificationTypeCodes.Handshake && outcome.Succeeded)
            {
                store.TryCompleteSubscriptionHandshake(e);
            }
            return outcome;
        }
        catch (Exception exception)
        {
            return await DispatchAsync(store, e, () => Task.FromResult(new Outcome(OutcomeKind.Failed,
                Error: $"Notification preparation failed: {exception.Message}"))).ConfigureAwait(false);
        }
    }

    /// <summary>Attempt to send a notification via REST Hook.</summary>
    /// <param name="store">   The store.</param>
    /// <param name="e">       Subscription event information.</param>
    /// <param name="contents">Serialized contents of the notification.</param>
    /// <returns>The revision-fenced REST outcome.</returns>
    private async Task<Outcome> TryNotifyRestHook(
        IFhirStore store,
        SubscriptionSendEventArgs e,
        string contents)
    {
        // auto-pass any notifications to example.org
        if (e.Subscription.Endpoint.Contains("example.org", StringComparison.Ordinal))
        {
            return await DispatchAsync(store, e, () => Task.FromResult(new Outcome(OutcomeKind.Simulated, contents))).ConfigureAwait(false);
        }

        HttpRequestMessage request = null!;

        // send the request to the endpoint
        try
        {
            // build our request
            request = new HttpRequestMessage()
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri(e.Subscription.Endpoint),
                Content = new StringContent(contents, Encoding.UTF8, e.Subscription.ContentType),
            };

            // check for additional headers
            if ((e.Subscription.Parameters is not null) && e.Subscription.Parameters.Any())
            {
                // add headers
                foreach ((string param, List<string> values) in e.Subscription.Parameters)
                {
                    if (string.IsNullOrEmpty(param) ||
                        (!values.Any()))
                    {
                        continue;
                    }

                    request.Headers.Add(param, values);
                }
            }

            // send our request
            return await DispatchAsync(store, e,
                () => RestOutcomeAsync(_httpClient.SendAsync(request), contents, e.Subscription.Endpoint)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return await DispatchAsync(store, e, () => Task.FromResult(new Outcome(OutcomeKind.Failed,
                Error: $"REST POST {e.NotificationType} to {e.Subscription.Endpoint} failed: {ex.Message}"))).ConfigureAwait(false);
        }
        finally
        {
            if (request is not null)
            {
                request.Dispose();
            }
        }

    }

    private static Task<Outcome> DispatchAsync(IFhirStore store, SubscriptionSendEventArgs args, Func<Task<Outcome>> start)
    {
        store.TryDispatchSubscriptionNotification(args, start, out Task<Outcome> completion);
        return completion;
    }

    private static async Task<Outcome> RestOutcomeAsync(Task<HttpResponseMessage> operation, string contents, string endpoint)
    {
        using HttpResponseMessage response = await operation.ConfigureAwait(false);
        return response.StatusCode is System.Net.HttpStatusCode.OK or System.Net.HttpStatusCode.Accepted or System.Net.HttpStatusCode.NoContent
            ? new(OutcomeKind.Delivered, contents)
            : new(OutcomeKind.Failed, Error: $"REST POST to {endpoint} failed: {response.StatusCode}");
    }

    /// <summary>Triggered when the application host is ready to start the service.</summary>
    /// <param name="cancellationToken">Indicates that the start process has been aborted.</param>
    /// <returns>An asynchronous result.</returns>
    Task IHostedService.StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_schedulingLock)
        {
            ObjectDisposedException.ThrowIf(_hasDisposed, this);
            if (_stopped)
            {
                throw new InvalidOperationException("RestOnlyNotificationManager has already stopped.");
            }
            if (_started)
            {
                return Task.CompletedTask;
            }
            _started = true;
            _logger.LogInformation("Starting NotificationManager...");

            foreach (IFhirStore store in _storeManager.Values)
            {
                store.OnSubscriptionsChanged += Store_OnSubscriptionsChanged;
                store.OnSubscriptionSendEvent += Store_OnSubscriptionSendEvent;
                foreach (ParsedSubscription subscription in store.CurrentSubscriptions)
                {
                    long revision = subscription.Revision;
                    Store_OnSubscriptionsChanged(store, new()
                    {
                        Tenant = store.Config,
                        ChangedSubscription = subscription,
                        Revision = revision,
                        SendHandshake = subscription.CurrentStatus == "requested",
                    });
                }
            }

            _heartbeatTimer = _clock.CreateTimer(
                CheckAndSendHeartbeats,
                null,
                TimeSpan.Zero,
                TimeSpan.FromSeconds(2));

            _notificationTimer = _clock.CreateTimer(
                CheckNotificationQ,
                null,
                TimeSpan.Zero,
                TimeSpan.FromSeconds(1));
        }
        return Task.CompletedTask;
    }

    /// <summary>Check notification q.</summary>
    /// <exception cref="Exception">Thrown when an exception error condition occurs.</exception>
    /// <param name="state">The state.</param>
    private void CheckNotificationQ(object? state)
    {
        lock (_schedulingLock)
        {
            if (_stopped)
            {
                return;
            }
            while (_notificationRequestQ.TryDequeue(out NotificationRequest? request))
            {
                _ = DeliverAsync(request.Store, request.Args);
            }
        }
    }

    private async Task DeliverAsync(IFhirStore store, SubscriptionSendEventArgs args)
    {
        try
        {
            Outcome outcome = await TryNotify(store, args).ConfigureAwait(false);
            if (outcome.Kind == OutcomeKind.Failed)
            {
                _logger.LogWarning("Notification delivery failed for Subscription/{SubscriptionId}", args.Subscription.Id);
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Notification delivery failed for Subscription/{SubscriptionId}", args.Subscription.Id);
        }
        finally
        {
            if (args.NotificationType == ParsedSubscription.NotificationTypeCodes.Heartbeat)
            {
                lock (_schedulingLock)
                {
                    (IFhirStore, string, long) key = (store, args.Subscription.Id, args.Revision);
                    if (!_stopped && _heartbeatReservations.ContainsKey(key))
                    {
                        _heartbeatReservations[key] = _clock.GetUtcNow().UtcTicks +
                            (long)(args.Subscription.HeartbeatSeconds ?? 0) * TimeSpan.TicksPerSecond;
                    }
                }
            }
        }
    }

    /// <summary>Check and send heartbeats.</summary>
    /// <param name="state">The state.</param>
    private void CheckAndSendHeartbeats(object? state)
    {
        lock (_schedulingLock)
        {
            if (_stopped)
            {
                return;
            }
            DateTimeOffset now = _clock.GetUtcNow();
            long currentTicks = now.UtcTicks;
            HashSet<(IFhirStore Store, string Id, long Revision)> current = [];

            foreach (IFhirStore store in _storeManager.Values)
            {
                foreach (ParsedSubscription sub in store.CurrentSubscriptions)
                {
                    long revision = sub.Revision;
                    (IFhirStore, string, long) key = (store, sub.Id, revision);
                    current.Add(key);
                    if ((!SubscriptionLifecyclePolicy.CanGenerateEvents(sub.CurrentStatus, sub.ExpirationTicks, now)) ||
                        (sub.HeartbeatSeconds is null) ||
                        (sub.HeartbeatSeconds <= 0))
                    {
                        continue;
                    }

                    long interval = (long)sub.HeartbeatSeconds * TimeSpan.TicksPerSecond;
                    if (!_heartbeatReservations.TryGetValue(key, out long next))
                    {
                        next = currentTicks + interval;
                        _heartbeatReservations[key] = next;
                    }
                    if (next != long.MaxValue && currentTicks >= Math.Max(next, sub.LastCommunicationTicks + interval))
                    {
                        _heartbeatReservations[key] = long.MaxValue;

                        _notificationRequestQ.Enqueue(new()
                        {
                            Store = store,
                            Args = new()
                            {
                                Tenant = store.Config,
                                Subscription = sub,
                                Revision = revision,
                                NotificationType = ParsedSubscription.NotificationTypeCodes.Heartbeat,
                            },
                        });
                    }
                }
            }
            foreach ((IFhirStore Store, string Id, long Revision) key in _heartbeatReservations.Keys.Where(key => !current.Contains(key)).ToArray())
            {
                _heartbeatReservations.Remove(key);
            }
            foreach (KeyValuePair<(IFhirStore Store, string Id), long> entry in _handshakeRevisions
                .Where(entry => !current.Contains((entry.Key.Store, entry.Key.Id, entry.Value))).ToArray())
            {
                _handshakeRevisions.Remove(entry.Key);
            }
        }
    }

    /// <summary>Event handler. Called by Store for on subscription events.</summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">     Subscription event information.</param>
    private void Store_OnSubscriptionSendEvent(object? sender, SubscriptionSendEventArgs e)
    {
        lock (_schedulingLock)
        {
            if (_stopped)
            {
                return;
            }
            if (!_storeManager.ContainsKey(e.Tenant.ControllerName))
            {
                _logger.LogError($"Cannot send subscription for non-existing tenant: {e.Tenant.ControllerName}");
                return;
            }

            _notificationRequestQ.Enqueue(new()
            {
                Store = _storeManager[e.Tenant.ControllerName],
                Args = e,
            });
        }
    }

    /// <summary>Event handler. Called by Store for on subscriptions changed events.</summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">     Subscription changed event information.</param>
    private void Store_OnSubscriptionsChanged(object? sender, SubscriptionChangedEventArgs e)
    {
        lock (_schedulingLock)
        {
            if (_stopped || !_storeManager.ContainsKey(e.Tenant.ControllerName))
            {
                return;
            }

            IFhirStore store = _storeManager[e.Tenant.ControllerName];
            string? changedId = string.IsNullOrEmpty(e.RemovedSubscriptionId) ? e.ChangedSubscription?.Id : e.RemovedSubscriptionId;
            foreach ((IFhirStore Store, string Id, long Revision) key in _heartbeatReservations.Keys
                .Where(key => ReferenceEquals(key.Store, store) && key.Id == changedId && key.Revision < e.Revision).ToArray())
            {
                _heartbeatReservations.Remove(key);
            }
            if (changedId is not null && _handshakeRevisions.TryGetValue((store, changedId), out long priorRevision) &&
                priorRevision < e.Revision)
            {
                _handshakeRevisions.Remove((store, changedId));
            }

            if (e.SendHandshake && e.ChangedSubscription is not null)
            {
                // Subscription changes publish after unlocking and may race the startup snapshot.
                ParsedSubscription? current = store.CurrentSubscriptions.FirstOrDefault(subscription => subscription.Id == changedId);
                if (current is null || !ReferenceEquals(current, e.ChangedSubscription) || current.Revision != e.Revision ||
                    !SubscriptionLifecyclePolicy.CanDeliverNotification(current.CurrentStatus, current.ExpirationTicks,
                        ParsedSubscription.NotificationTypeCodes.Handshake, _clock.GetUtcNow()) ||
                    (_handshakeRevisions.TryGetValue((store, current.Id), out long queuedRevision) && queuedRevision >= e.Revision))
                {
                    return;
                }

                _handshakeRevisions[(store, current.Id)] = e.Revision;
                _notificationRequestQ.Enqueue(new()
                {
                    Store = store,
                    Args = new()
                    {
                        Tenant = e.Tenant,
                        Subscription = e.ChangedSubscription,
                        Revision = e.Revision,
                        NotificationType = ParsedSubscription.NotificationTypeCodes.Handshake,
                    }
                });
            }
        }
    }

    /// <summary>Triggered when the application host is performing a graceful shutdown.</summary>
    /// <param name="cancellationToken">Indicates that the shutdown process should no longer be
    ///  graceful.</param>
    /// <returns>An asynchronous result.</returns>
    Task IHostedService.StopAsync(CancellationToken cancellationToken)
    {
        lock (_schedulingLock)
        {
            if (_stopped)
            {
                return Task.CompletedTask;
            }
            _stopped = true;
            _heartbeatTimer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            _notificationTimer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            foreach (IFhirStore store in _storeManager.Values)
            {
                store.OnSubscriptionsChanged -= Store_OnSubscriptionsChanged;
                store.OnSubscriptionSendEvent -= Store_OnSubscriptionSendEvent;
            }
            _notificationRequestQ.Clear();
            _heartbeatReservations.Clear();
            _handshakeRevisions.Clear();
        }
        _heartbeatTimer?.Dispose();
        _notificationTimer?.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Releases the unmanaged resources used by the
    /// FhirModelComparer.Server.Services.FhirManagerService and optionally releases the managed
    /// resources.
    /// </summary>
    /// <param name="disposing">True to release both managed and unmanaged resources; false to
    ///  release only unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_hasDisposed)
        {
            // dispose managed state (managed objects)
            if (disposing)
            {
                ((IHostedService)this).StopAsync(CancellationToken.None).GetAwaiter().GetResult();
                _heartbeatTimer?.Dispose();
                _notificationTimer?.Dispose();
                if (_ownsHttpClient)
                {
                    _httpClient.Dispose();
                }
            }

            // free unmanaged resources (unmanaged objects) and override finalizer
            // set large fields to null
            _hasDisposed = true;
        }
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged
    /// resources.
    /// </summary>
    void IDisposable.Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
