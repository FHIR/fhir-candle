// <copyright file="NotificationManager.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using fhir.candle.Zulip;
using FhirCandle.Models;
using FhirCandle.Storage;
using System.Text;
using MailKit.Net.Smtp;
using MailKit;
using MimeKit;
using fhir.candle.Models;
using System.Collections.Concurrent;
using FhirCandle.Configuration;

namespace fhir.candle.Services;

/// <summary>Manager for notifications.</summary>
public class NotificationManager : INotificationManager
{
    /// <summary>True if has disposed, false if not.</summary>
    private bool _hasDisposed = false;

    /// <summary>The HTTP client for REST notifications.</summary>
    private HttpClient _httpClient = new();

    /// <summary>The logger.</summary>
    private ILogger _logger;

    /// <summary>Manager for store.</summary>
    private IFhirStoreManager _storeManager;

    /// <summary>The heartbeat timer.</summary>
    private Timer _heartbeatTimer = null!;

    private Timer _notificationTimer = null!;

    private record NotificationRequest
    {
        public required IFhirStore Store { get; init; }

        public required SubscriptionSendEventArgs Args { get; init; }
    }

    private ConcurrentQueue<NotificationRequest> _notificationRequestQ = new();

    /// <summary>The zulip site URL (e.g., https://chat.fhir.org/).</summary>
    private string _zulipUrl;

    /// <summary>The zulip bot email address.</summary>
    private string _zulipEmail;

    /// <summary>The zulip bot API key.</summary>
    private string _zulipKey;

    /// <summary>The SMTP host.</summary>
    private string _smtpHost;

    /// <summary>The SMTP port.</summary>
    private int _smtpPort;

    /// <summary>The SMTP user.</summary>
    private string _smtpUser;

    /// <summary>The SMTP password.</summary>
    private string _smtpPassword;

    /// <summary>Initializes a new instance of the <see cref="NotificationManager"/> class.</summary>
    /// <param name="serverConfig">    The server configuration.</param>
    /// <param name="tenants">         The tenants.</param>
    /// <param name="fhirStoreManager">Manager for FHIR store.</param>
    /// <param name="logger">          The logger.</param>
    public NotificationManager(
        CandleConfig serverConfig,
        Dictionary<string, TenantConfiguration> tenants,
        IFhirStoreManager fhirStoreManager,
        ILogger<NotificationManager> logger)
    {
        _logger = logger;
        _storeManager = fhirStoreManager;

        _zulipUrl = serverConfig.ZulipUrl ?? string.Empty;
        _zulipEmail = serverConfig.ZulipEmail ?? string.Empty;
        _zulipKey = serverConfig.ZulipKey ?? string.Empty;

        if (string.IsNullOrEmpty(_zulipUrl) ||
            string.IsNullOrEmpty(_zulipEmail) ||
            string.IsNullOrEmpty(_zulipKey))
        {
            _logger.LogInformation("Zulip information not found - Zulip notification will not be sent");
        }
        else
        {
            _logger.LogInformation($"Found Zulip configuration: {_zulipUrl} ({_zulipEmail})");
            ZulipClientPool.AddOrRegisterClient(_zulipUrl, _zulipEmail, _zulipKey);
        }

        _smtpHost = serverConfig.SmtpHost ?? string.Empty;
        _smtpPort = serverConfig.SmtpPort;
        _smtpUser = serverConfig.SmtpUser ?? string.Empty;
        _smtpPassword = serverConfig.SmtpPassword ?? string.Empty;

        if (string.IsNullOrEmpty(_smtpHost) ||
            string.IsNullOrEmpty(_smtpUser) ||
            string.IsNullOrEmpty(_smtpPassword) ||
            (_smtpPort == 0))
        {
            _logger.LogInformation("SMTP information not found - Email notification will not be sent");
        }
        else
        {
            _logger.LogInformation($"Found SMTP configuration: {_smtpHost}:{_smtpPort} ({_smtpUser})");
        }
    }

    /// <summary>Try notify via the appropriate channel type.</summary>
    /// <param name="store">The store.</param>
    /// <param name="e">    Subscription event information.</param>
    /// <returns>An asynchronous result that yields true if it succeeds, false if it fails.</returns>
    private async Task<bool> TryNotify(IFhirStore store, SubscriptionSendEventArgs e)
    {
        string contents;

        switch (e.NotificationType)
        {
            case ParsedSubscription.NotificationTypeCodes.Handshake:
                {
                    contents = store.SerializeSubscriptionEvents(
                                    e.Subscription.Id,
                                    Array.Empty<long>(),
                                    "handshake",
                                    false);
                }
                break;

            case ParsedSubscription.NotificationTypeCodes.Heartbeat:
                {
                    contents = store.SerializeSubscriptionEvents(
                                    e.Subscription.Id,
                                    Array.Empty<long>(),
                                    "heartbeat",
                                    false);
                }
                break;

            case ParsedSubscription.NotificationTypeCodes.EventNotification:
                {
                    if (!e.NotificationEvents.Any())
                    {
                        return false;
                    }

                    contents = store.SerializeSubscriptionEvents(
                                    e.Subscription.Id,
                                    e.NotificationEvents.Select(ne => ne.EventNumber),
                                    "event-notification",
                                    false);
                }
                break;

            case ParsedSubscription.NotificationTypeCodes.QueryStatus:
                throw new NotImplementedException("TryNotify <<< QueryStatus is not an implemented mode for notifications");
            //break;

            case ParsedSubscription.NotificationTypeCodes.QueryEvent:
                throw new NotImplementedException("TryNotify <<< QueryEvent is not an implemented mode for notifications");
            //break;

            default:
                _logger.LogError($"TryNotify <<< Unknown notification type: {e.NotificationType}");
                return false;
        }

        bool success;

        switch (e.Subscription.ChannelCode.ToLowerInvariant())
        {
            case "email":
                success = await TryNotifyEmail(store, e, contents);
                break;

            case "rest-hook":
                success = await TryNotifyRestHook(store, e, contents);
                break;

            case "zulip":
                success = await TryNotifyZulip(store, e, contents);
                break;

            case "websocket":
            default:
                _logger.LogError($"TryNotify <<< unimplemented channel type: {e.Subscription.ChannelCode}");
                return false;
        }

        if ((e.NotificationType == ParsedSubscription.NotificationTypeCodes.Handshake) &&
            success)
        {
            store.ChangeSubscriptionStatus(e.Subscription.Id, "active");
        }

        if (e.NotificationEvents.Any())
        {
            e.Subscription.RegisterSerializedSend(e.NotificationEvents.Select(ne => ne.EventNumber).Max(), contents);
        }
        else
        {
            e.Subscription.RegisterSerializedSend(0, contents);
        }

        return success;
    }

    /// <summary>Attempt to send a notification via REST Hook.</summary>
    /// <param name="store">   The store.</param>
    /// <param name="e">       Subscription event information.</param>
    /// <param name="contents">Serialized contents of the notification.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    private async Task<bool> TryNotifyRestHook(
        IFhirStore store,
        SubscriptionSendEventArgs e,
        string contents)
    {
        // auto-pass any notifications to example.org
        if (e.Subscription.Endpoint.Contains("example.org", StringComparison.Ordinal))
        {
            return true;
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
            if ((e.Subscription.Parameters != null) && e.Subscription.Parameters.Any())
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
            HttpResponseMessage response = await _httpClient.SendAsync(request);

            // check the status code
            if ((response.StatusCode != System.Net.HttpStatusCode.OK) &&
                (response.StatusCode != System.Net.HttpStatusCode.Accepted) &&
                (response.StatusCode != System.Net.HttpStatusCode.NoContent))
            {
                // failure
                e.Subscription.RegisterError($"REST POST to {e.Subscription.Endpoint} failed: {response.StatusCode}");
                return false;
            }

            e.Subscription.LastCommunicationTicks = DateTime.Now.Ticks;

            if (e.NotificationEvents.Any())
            {
                _logger.LogInformation(
                    $" <<< Subscription/{e.Subscription.Id}" +
                    $" POST: {e.Subscription.Endpoint}" +
                    $" Events: {string.Join(',', e.NotificationEvents.Select(ne => ne.EventNumber))}");
            }
            else
            {
                _logger.LogInformation(
                    $" <<< Subscription/{e.Subscription.Id}" +
                    $" POST {e.NotificationType}: {e.Subscription.Endpoint}");
            }
        }
        catch (Exception ex)
        {
            e.Subscription.RegisterError($"REST POST {e.NotificationType} to {e.Subscription.Endpoint} failed: {ex.Message}");
            return false;
        }
        finally
        {
            if (request != null)
            {
                request.Dispose();
            }
        }

        return true;
    }

    /// <summary>Attempt to send a notification via Zulip.</summary>
    /// <param name="store">   The store.</param>
    /// <param name="e">       Subscription event information.</param>
    /// <param name="contents">Serialized contents of the notification.</param>
    /// <returns>An asynchronous result that yields true if it succeeds, false if it fails.</returns>
    private async Task<bool> TryNotifyZulip(
        IFhirStore store,
        SubscriptionSendEventArgs e,
        string contents)
    {
        string zulipSite = e.Subscription.Parameters.ContainsKey("site") ? e.Subscription.Parameters["site"].First() : _zulipUrl;
        string zulipEmail = e.Subscription.Parameters.ContainsKey("email") ? e.Subscription.Parameters["email"].First() : _zulipEmail;
        string zulipKey = e.Subscription.Parameters.ContainsKey("key") ? e.Subscription.Parameters["key"].First() : _zulipKey;

        if (string.IsNullOrEmpty(zulipSite) ||
            string.IsNullOrEmpty(zulipEmail) ||
            string.IsNullOrEmpty(zulipKey))
        {
            return false;
        }

        // send the request to the endpoint
        try
        {
            zulip_cs_lib.ZulipClient client = ZulipClientPool.GetOrCreateClient(zulipSite, zulipEmail, zulipKey);

            if (client == null)
            {
                return false;
            }

            string messageText = BuildZulipMessage(store, e, contents);

            if (e.Subscription.Parameters.ContainsKey("streamId"))
            {
                foreach (string value in e.Subscription.Parameters["streamId"])
                {
                    if (!int.TryParse(value, out int id))
                    {
                        continue;
                    }

                    (bool success, string details, ulong messageId) result = await client.Messages.TrySendStream(
                        messageText,
                        "Subscription Notification",
                        new int[] { id });
                }
            }

            if (e.Subscription.Parameters.ContainsKey("userId"))
            {
                foreach (string value in e.Subscription.Parameters["userId"])
                {
                    if (!int.TryParse(value, out int id))
                    {
                        continue;
                    }

                    (bool success, string details, ulong messageId) result = await client.Messages.TrySendPrivate(
                        messageText,
                        new int[] { id });
                }
            }

            e.Subscription.LastCommunicationTicks = DateTime.Now.Ticks;
        }
        catch (Exception ex)
        {
            e.Subscription.RegisterError($"Zulip {e.NotificationType} to {e.Subscription.Endpoint} failed: {ex.Message}");
            return false;
        }

        return true;
    }

    /// <summary>Parse email mime.</summary>
    /// <param name="mime">          The mime.</param>
    /// <param name="messageMime">   [out] The message mime.</param>
    /// <param name="attachmentMime">[out] The attachment mime.</param>
    private void ParseEmailMime(string mime, out string messageMime, out string attachmentMime)
    {
        if (string.IsNullOrEmpty(mime))
        {
            messageMime = "text/plain";
            attachmentMime = "application/fhir+json";
            return;
        }

        string[] mimeComponents = mime.Split(';');

        string message;
        string attach;

        if (mimeComponents.Length == 1)
        {
            message = mimeComponents[0].Trim();
            attach = message;
        }
        else
        {
            message = mimeComponents[0].Trim();
            attach = mimeComponents[1].Trim();
        }

        // single mime, determine if this is message or attachment
        switch (message)
        {
            case "text/plain":
            case "text/html":
                messageMime = message;
                break;

            default:
                messageMime = "text/plain";
                break;
        }

        switch (attach)
        {
            default:
            case "json":
            case "application/json":
            case "application/fhir+json":
                attachmentMime = "application/fhir+json";
                break;

            case "xml":
            case "application/xml":
            case "application/fhir+xml":
                attachmentMime = "application/fhir+xml";
                break;
        }
    }

    /// <summary>Attempt to send a notification via Email.</summary>
    /// <param name="store">   The store.</param>
    /// <param name="e">       Subscription event information.</param>
    /// <param name="contents">Serialized contents of the notification.</param>
    /// <returns>An asynchronous result that yields true if it succeeds, false if it fails.</returns>
    private async Task<bool> TryNotifyEmail(
        IFhirStore store,
        SubscriptionSendEventArgs e,
        string contents)
    {
        string from = e.Subscription.Parameters.ContainsKey("from") ? e.Subscription.Parameters["from"].First() : "FHIR Notification";
        string destination = e.Subscription.Endpoint ?? string.Empty;

        if (string.IsNullOrEmpty(destination) ||
            string.IsNullOrEmpty(_smtpHost) ||
            string.IsNullOrEmpty(_smtpUser) ||
            (_smtpPort == 0))
        {
            return false;
        }

        if (destination.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
        {
            destination = destination.Substring(7).Trim();
        }

        if (destination.Contains("example.org", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // send the request to the endpoint
        try
        {
            using (MemoryStream ms = new MemoryStream((byte[])System.Text.Encoding.UTF8.GetBytes(contents)))
            {
                long eventLo = e.NotificationEvents.Any() ? e.NotificationEvents.Min(ne => ne.EventNumber) : 0;
                long eventHi = e.NotificationEvents.Any() ? e.NotificationEvents.Max(ne => ne.EventNumber) : 0;

                ParseEmailMime(e.Subscription.ContentType, out string messageMime, out string attachmentMime);

                string shortMime = attachmentMime.EndsWith("json", StringComparison.Ordinal)
                    ? "json"
                    : "xml";

                MimeMessage message = new();
                message.From.Add(new MailboxAddress(from, _smtpUser));
                message.To.Add(new MailboxAddress(destination, destination));

                BodyBuilder bodyBuilder = new();
                if (messageMime.Equals("text/html", StringComparison.Ordinal))
                {
                    bodyBuilder.HtmlBody = BuildEmailMessage(store, e);
                }
                else
                {
                    bodyBuilder.TextBody = BuildEmailMessage(store, e);
                }

                // no events included
                if (eventHi == 0)
                {
                    message.Subject = $"Subscription {e.Subscription.Id} {e.NotificationType}";

                    bodyBuilder.Attachments.Add(
                        $"notifications-{e.NotificationType}.{shortMime}",
                        ms,
                        ContentType.Parse(attachmentMime));
                }

                // one event included
                else if (eventLo == eventHi)
                {
                    message.Subject = $"Subscription {e.Subscription.Id} Event {eventLo}";

                    bodyBuilder.Attachments.Add(
                        $"notification-{eventLo}.{shortMime}",
                        ms,
                        ContentType.Parse(attachmentMime));
                }

                // multiple events included
                else
                {
                    message.Subject = $"Subscription {e.Subscription.Id} Events {eventLo}-{eventHi}";

                    bodyBuilder.Attachments.Add(
                        $"notifications-{eventLo}-{eventHi}.{shortMime}",
                        ms,
                        ContentType.Parse(attachmentMime));
                }

                message.Body = bodyBuilder.ToMessageBody();

                using (SmtpClient client = new())
                {
                    client.Connect(_smtpHost, _smtpPort, true);
                    client.Authenticate(_smtpUser, _smtpPassword);
                    await client.SendAsync(message);
                    client.Disconnect(true);
                }
            }

            e.Subscription.LastCommunicationTicks = DateTime.Now.Ticks;
        }
        catch (Exception ex)
        {
            e.Subscription.RegisterError($"Email {e.NotificationType} to {e.Subscription.Endpoint} failed: {ex.Message}");
            return false;
        }

        return true;
    }

    /// <summary>Builds an email message.</summary>
    /// <param name="store">   The store.</param>
    /// <param name="e">       Subscription event information.</param>
    /// <param name="contents">Serialized contents of the notification.</param>
    /// <returns>A string.</returns>
    private string BuildEmailMessage(IFhirStore store, SubscriptionSendEventArgs e)
    {
        // TODO: need to build a persistent opt-out

        if (e.NotificationEvents.Any())
        {
            return $"Subscription {e.Subscription.Id} ({store.Config.BaseUrl}/Subscription/{e.Subscription.Id})" +
                $" is sending you a {e.NotificationType} notification for events" +
                $" {string.Join(',', e.NotificationEvents.Select(ne => ne.EventNumber))}.\n\n" +
                $"The FHIR contents can be found as an attachment to this message.";
        }

        return $"Subscription {e.Subscription.Id} ({store.Config.BaseUrl}/Subscription/{e.Subscription.Id})" +
            $" is sending you a {e.NotificationType} notification.\n\n" +
            $"The FHIR contents can be found as an attachment to this message.";
    }

    /// <summary>Builds a zulip message.</summary>
    /// <param name="store">   The store.</param>
    /// <param name="e">       Subscription event information.</param>
    /// <param name="contents">Serialized contents of the notification.</param>
    /// <returns>A string.</returns>
    private string BuildZulipMessage(IFhirStore store, SubscriptionSendEventArgs e, string contents)
    {
        // TODO: need to build a persistent opt-out

        string shortMime = e.Subscription.ContentType.Contains("json", StringComparison.OrdinalIgnoreCase)
            ? "json"
            : "xml";

        switch (e.NotificationType)
        {
            case ParsedSubscription.NotificationTypeCodes.Handshake:
                return $"[{e.Subscription.Id}]({store.Config.BaseUrl}/Subscription/{e.Subscription.Id})" +
                    $" is going to send you notifications.\n" +
                    $"To opt out, please click ~~here~~\n" +
                    $"```spoiler Bundle\n```{shortMime}\n{contents}\n```\n```\n";

            case ParsedSubscription.NotificationTypeCodes.Heartbeat:
                return $"[{e.Subscription.Id}]({store.Config.BaseUrl}/Subscription/{e.Subscription.Id})" +
                    $" is alive with nothing to report.\n" +
                    $"To opt out, please click ~~here~~\n" +
                    $"```spoiler Bundle\n```{shortMime}\n{contents}\n```\n```\n";

            case ParsedSubscription.NotificationTypeCodes.EventNotification:
                return $"[{e.Subscription.Id}]({store.Config.BaseUrl}/Subscription/{e.Subscription.Id})" +
                    $" is reporting notification" +
                    $" {string.Join(',', e.NotificationEvents.Select(ne => ne.EventNumber))}.\n" +
                    $"To opt out, please click ~~here~~\n" +
                    $"```spoiler Bundle\n```{shortMime}\n{contents}\n```\n```\n";

            case ParsedSubscription.NotificationTypeCodes.QueryStatus:
                return $"[{e.Subscription.Id}]({store.Config.BaseUrl}/Subscription/{e.Subscription.Id})" +
                    $" is responding to a status query" +
                    $" {string.Join(',', e.NotificationEvents.Select(ne => ne.EventNumber))}.\n" +
                    $"To opt out, please click ~~here~~" +
                    $"```spoiler Bundle\n```{shortMime}\n{contents}\n```\n```\n";

            case ParsedSubscription.NotificationTypeCodes.QueryEvent:
                return $"[{e.Subscription.Id}]({store.Config.BaseUrl}/Subscription/{e.Subscription.Id})" +
                    $" is resending events" +
                    $" {string.Join(',', e.NotificationEvents.Select(ne => ne.EventNumber))}.\n" +
                    $"To opt out, please click ~~here~~\n" +
                    $"```spoiler Bundle\n```{shortMime}\n{contents}\n```\n```\n";

            default:
                return $"[{e.Subscription.Id}]({store.Config.BaseUrl}/Subscription/{e.Subscription.Id})" +
                    $" type: `{e.NotificationType}`." +
                    $"To opt out, please click ~~here~~\n" +
                    $"```spoiler Bundle\n```{shortMime}\n{contents}\n```\n```\n";
        }
    }

    /// <summary>Triggered when the application host is ready to start the service.</summary>
    /// <param name="cancellationToken">Indicates that the start process has been aborted.</param>
    /// <returns>An asynchronous result.</returns>
    Task IHostedService.StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting NotificationManager...");

        // traverse stores and initialize our event handlers
        foreach (IFhirStore store in _storeManager.Values)
        {
            // register our event handlers
            store.OnSubscriptionsChanged += Store_OnSubscriptionsChanged;
            store.OnSubscriptionSendEvent += Store_OnSubscriptionSendEvent;
        }

        // start our heartbeat timer
        _heartbeatTimer = new Timer(
            CheckAndSendHeartbeats,
            null,
            TimeSpan.Zero,
            TimeSpan.FromSeconds(2));

        _notificationTimer = new Timer(
            CheckNotificationQ,
            null,
            TimeSpan.Zero,
            TimeSpan.FromSeconds(1));

        return Task.CompletedTask;
    }

    /// <summary>Check notification q.</summary>
    /// <exception cref="Exception">Thrown when an exception error condition occurs.</exception>
    /// <param name="state">The state.</param>
    private void CheckNotificationQ(object? state)
    {
        while (_notificationRequestQ.Any())
        {
            if ((!_notificationRequestQ.TryDequeue(out NotificationRequest? req)) ||
                (req == null))
            {
                return;
            }

            _ = TryNotify(req.Store, req.Args);
        }
    }

    /// <summary>Check and send heartbeats.</summary>
    /// <param name="state">The state.</param>
    private void CheckAndSendHeartbeats(object? state)
    {
        long currentTicks = DateTime.Now.Ticks;

        // traverse stores to check subscriptions
        foreach (IFhirStore store in _storeManager.Values)
        {
            // traverse active subscriptions
            foreach (ParsedSubscription sub in store.CurrentSubscriptions)
            {
                if ((!sub.CurrentStatus.Equals("active", StringComparison.Ordinal)) ||
                    (sub.HeartbeatSeconds == null) ||
                    (sub.HeartbeatSeconds <= 0))
                {
                    continue;
                }

                // wait one offset if the subscription is new
                if (sub.LastCommunicationTicks == 0)
                {
                    sub.LastCommunicationTicks = currentTicks + ((long)sub.HeartbeatSeconds - 1 * TimeSpan.TicksPerSecond);
                    continue;
                }

                long threshold = currentTicks - ((long)sub.HeartbeatSeconds! * TimeSpan.TicksPerSecond);

                if (sub.LastCommunicationTicks < threshold)
                {
                    sub.LastCommunicationTicks = currentTicks;

                    _notificationRequestQ.Enqueue(new()
                    {
                        Store = store,
                        Args = new()
                        {
                            Tenant = store.Config,
                            Subscription = sub,
                            NotificationType = ParsedSubscription.NotificationTypeCodes.Heartbeat,
                        },
                    });
                }
            }
        }
    }

    /// <summary>Event handler. Called by Store for on subscription events.</summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">     Subscription event information.</param>
    private void Store_OnSubscriptionSendEvent(object? sender, SubscriptionSendEventArgs e)
    {
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

    /// <summary>Event handler. Called by Store for on subscriptions changed events.</summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">     Subscription changed event information.</param>
    private void Store_OnSubscriptionsChanged(object? sender, SubscriptionChangedEventArgs e)
    {
        // make sure the store we want exists
        if (!_storeManager.ContainsKey(e.Tenant.ControllerName))
        {
            return;
        }

        // check for a deleted subscription
        if (!string.IsNullOrEmpty(e.RemovedSubscriptionId))
        {
            // TODO: Remove any existing heartbeat record
        }

        IFhirStore store = _storeManager[e.Tenant.ControllerName];

        // check for a new subscription
        if (e.SendHandshake)
        {
            if (e.ChangedSubscription == null)
            {
                return;
            }

            _notificationRequestQ.Enqueue(new()
            {
                Store = store,
                Args = new()
                {
                    Tenant = e.Tenant,
                    Subscription = e.ChangedSubscription!,
                    NotificationType = ParsedSubscription.NotificationTypeCodes.Handshake,
                }
            });
        }

        // check for a changed subscription
        if (e.ChangedSubscription != null)
        {
            // TODO: Check for changes to the heartbeat interval
        }
    }

    /// <summary>Triggered when the application host is performing a graceful shutdown.</summary>
    /// <param name="cancellationToken">Indicates that the shutdown process should no longer be
    ///  graceful.</param>
    /// <returns>An asynchronous result.</returns>
    Task IHostedService.StopAsync(CancellationToken cancellationToken)
    {
        _heartbeatTimer?.Change(Timeout.Infinite, 0);

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
            if (disposing)
            {
                // TODO: dispose managed state (managed objects)
                _heartbeatTimer?.Dispose();
            }

            // TODO: free unmanaged resources (unmanaged objects) and override finalizer
            // TODO: set large fields to null
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
