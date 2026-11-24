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
using FhirCandle.Configuration;
using Outcome = FhirCandle.Models.SubscriptionNotificationOutcome;
using OutcomeKind = FhirCandle.Models.SubscriptionNotificationOutcome.OutcomeKind;
using SaturationWarning = fhir.candle.Services.NotificationScheduler.SaturationWarning;

namespace fhir.candle.Services;

/// <summary>Manager for notifications.</summary>
public class NotificationManager : INotificationManager
{
    /// <summary>True if has disposed, false if not.</summary>
    private bool _hasDisposed = false;

    /// <summary>The HTTP client for REST notifications.</summary>
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private readonly TimeProvider _clock;

    /// <summary>The logger.</summary>
    private ILogger _logger;

    /// <summary>Manager for store.</summary>
    private IFhirStoreManager _storeManager;

    /// <summary>The heartbeat timer.</summary>
    private ITimer? _heartbeatTimer;

    internal NotificationScheduler Scheduler { get; }

    private readonly Dictionary<(IFhirStore Store, string Id, long Revision), long> _heartbeatReservations = [];
    private readonly Dictionary<(IFhirStore Store, string Id), long> _handshakeRevisions = [];

    internal Func<SmtpClient, string, int, CancellationToken, Task> SmtpConnect { get; set; } =
        (client, host, port, token) => client.ConnectAsync(host, port, true, token);
    internal Func<SmtpClient, string, string, CancellationToken, Task> SmtpAuthenticate { get; set; } =
        (client, user, password, token) => client.AuthenticateAsync(user, password, token);
    internal Func<SmtpClient, MimeMessage, CancellationToken, Task> SmtpSend { get; set; } =
        (client, message, token) => client.SendAsync(message, token);
    internal Func<SmtpClient, CancellationToken, Task> SmtpDisconnect { get; set; } =
        (client, token) => client.IsConnected ? client.DisconnectAsync(true, token) : Task.CompletedTask;
    internal Func<ZulipSender.Destination, string, int, bool, CancellationToken, Task<(bool success, string? details, ulong messageId)>> ZulipSend { get; set; }

    /// <summary>True after the service has attached its store event handlers.</summary>
    private bool _started;

    private readonly ZulipSender _zulipSender;

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
        : this(serverConfig, tenants, fhirStoreManager, logger, TimeProvider.System, null)
    {
    }

    /// <summary>Creates a manager with a controlled clock and an optional caller-owned HTTP client.</summary>
    /// <param name="serverConfig">The server configuration.</param>
    /// <param name="tenants">The tenants.</param>
    /// <param name="fhirStoreManager">The store manager.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="clock">The UTC clock and service timer factory.</param>
    /// <param name="httpClient">An optional caller-owned transport client.</param>
    public NotificationManager(
        CandleConfig serverConfig,
        Dictionary<string, TenantConfiguration> tenants,
        IFhirStoreManager fhirStoreManager,
        ILogger<NotificationManager> logger,
        TimeProvider clock,
        HttpClient? httpClient)
        : this(serverConfig, tenants, fhirStoreManager, logger, clock, httpClient, null)
    {
    }

    internal NotificationManager(
        CandleConfig serverConfig,
        Dictionary<string, TenantConfiguration> tenants,
        IFhirStoreManager fhirStoreManager,
        ILogger<NotificationManager> logger,
        TimeProvider clock,
        HttpClient? httpClient,
        HttpMessageHandler? zulipHandler)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        Scheduler = new(serverConfig, tenants.Keys, logger, _clock, DeliverAsync);
        _ownsHttpClient = httpClient is null;
        _httpClient = httpClient ?? new();
        _logger = logger;
        _storeManager = fhirStoreManager;

        _zulipSender = new(serverConfig.ZulipUrl, serverConfig.ZulipEmail, serverConfig.ZulipKey, zulipHandler);
        ZulipSend = _zulipSender.SendAsync;

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
    /// <param name="cancellationToken">Cancellation of delivery during shutdown.</param>
    /// <returns>The revision-fenced notification outcome.</returns>
    private async Task<Outcome> TryNotify(IFhirStore store, SubscriptionSendEventArgs e, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
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
            Outcome outcome = e.Subscription.ChannelCode.ToLowerInvariant() switch
            {
                "email" => await TryNotifyEmail(store, e, contents, cancellationToken).ConfigureAwait(false),
                "rest-hook" => await TryNotifyRestHook(store, e, contents, cancellationToken).ConfigureAwait(false),
                "zulip" => await TryNotifyZulip(store, e, contents, cancellationToken).ConfigureAwait(false),
                _ => throw new NotSupportedException($"Unimplemented channel type: {e.Subscription.ChannelCode}"),
            };

            if (e.NotificationType == ParsedSubscription.NotificationTypeCodes.Handshake && outcome.Succeeded)
            {
                store.TryCompleteSubscriptionHandshake(e);
            }
            return outcome;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return await PreparationFailureAsync(store, e, "Notification", exception).ConfigureAwait(false);
        }
    }

    /// <summary>Attempt to send a notification via REST Hook.</summary>
    /// <param name="store">   The store.</param>
    /// <param name="e">       Subscription event information.</param>
    /// <param name="contents">Serialized contents of the notification.</param>
    /// <param name="cancellationToken">Cancellation of delivery during shutdown.</param>
    /// <returns>The revision-fenced REST outcome.</returns>
    private async Task<Outcome> TryNotifyRestHook(
        IFhirStore store,
        SubscriptionSendEventArgs e,
        string contents,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
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
                () => RestOutcomeAsync(_httpClient.SendAsync(request, cancellationToken), contents, e.Subscription.Endpoint)).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return await PreparationFailureAsync(store, e, "REST", ex).ConfigureAwait(false);
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

    private async Task<Outcome> PreparationFailureAsync(IFhirStore store, SubscriptionSendEventArgs args, string channel, Exception exception)
    {
        Outcome outcome = await DispatchAsync(store, args, () => Task.FromResult(new Outcome(OutcomeKind.Failed,
            Error: $"{channel} {args.NotificationType} to {args.Subscription.Endpoint} failed: {exception.Message}"))).ConfigureAwait(false);
        if (outcome.Kind == OutcomeKind.Failed)
        {
            _logger.LogError(exception, "{Channel} notification delivery failed for Subscription/{SubscriptionId}", channel, args.Subscription.Id);
        }
        return outcome;
    }

    /// <summary>Attempt to send a notification via Zulip.</summary>
    /// <param name="store">   The store.</param>
    /// <param name="e">       Subscription event information.</param>
    /// <param name="contents">Serialized contents of the notification.</param>
    /// <param name="cancellationToken">Cancellation of delivery during shutdown.</param>
    /// <returns>The revision-fenced Zulip outcome.</returns>
    private async Task<Outcome> TryNotifyZulip(
        IFhirStore store,
        SubscriptionSendEventArgs e,
        string contents,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // send the request to the endpoint
        try
        {
            ZulipSender.Destination? destination = _zulipSender.Bind(e.Subscription.Parameters);
            if (destination is null)
            {
                return await DispatchAsync(store, e, () => Task.FromResult(new Outcome(OutcomeKind.Failed))).ConfigureAwait(false);
            }

            string messageText = BuildZulipMessage(store, e, contents);
            Outcome outcome = new(OutcomeKind.SetupComplete);
            List<Outcome> failures = [];

            if (e.Subscription.Parameters.ContainsKey("streamId"))
            {
                foreach (string value in e.Subscription.Parameters["streamId"])
                {
                    if (!int.TryParse(value, out int id))
                    {
                        continue;
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                    outcome = await DispatchAsync(store, e,
                        () => ZulipOutcomeAsync(ZulipSend(destination, messageText, id, true, cancellationToken), contents)).ConfigureAwait(false);
                    if (outcome.Kind == OutcomeKind.Suppressed)
                    {
                        return outcome;
                    }
                    if (outcome.Kind == OutcomeKind.Failed)
                    {
                        failures.Add(outcome);
                    }
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

                    cancellationToken.ThrowIfCancellationRequested();
                    outcome = await DispatchAsync(store, e,
                        () => ZulipOutcomeAsync(ZulipSend(destination, messageText, id, false, cancellationToken), contents)).ConfigureAwait(false);
                    if (outcome.Kind == OutcomeKind.Suppressed)
                    {
                        return outcome;
                    }
                    if (outcome.Kind == OutcomeKind.Failed)
                    {
                        failures.Add(outcome);
                    }
                }
            }

            // Each recipient has already committed its own revision-fenced bookkeeping.
            return failures.Count == 0 ? outcome : new(OutcomeKind.Failed,
                Error: string.Join(Environment.NewLine, failures.Select(failure => failure.Error)
                    .Where(error => !string.IsNullOrEmpty(error)).Distinct(StringComparer.Ordinal)));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return await PreparationFailureAsync(store, e, "Zulip", ex).ConfigureAwait(false);
        }

    }

    private static async Task<Outcome> ZulipOutcomeAsync(Task<(bool success, string? details, ulong messageId)> operation, string contents)
    {
        (bool success, string? details, ulong messageId) result = await operation.ConfigureAwait(false);
        return result.success ? new(OutcomeKind.Delivered, contents) : new(OutcomeKind.Failed, Error: result.details);
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

    /// <summary>Builds the email notification and its attachment.</summary>
    /// <param name="store">The store.</param>
    /// <param name="e">Subscription event information.</param>
    /// <param name="from">The sender display name.</param>
    /// <param name="destination">The normalized destination.</param>
    /// <param name="attachmentStream">The caller-owned serialized notification stream.</param>
    /// <returns>The MIME message.</returns>
    private MimeMessage BuildEmailMimeMessage(IFhirStore store, SubscriptionSendEventArgs e, string from, string destination, Stream attachmentStream)
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
                attachmentStream,
                ContentType.Parse(attachmentMime));
        }

        // one event included
        else if (eventLo == eventHi)
        {
            message.Subject = $"Subscription {e.Subscription.Id} Event {eventLo}";

            bodyBuilder.Attachments.Add(
                $"notification-{eventLo}.{shortMime}",
                attachmentStream,
                ContentType.Parse(attachmentMime));
        }

        // multiple events included
        else
        {
            message.Subject = $"Subscription {e.Subscription.Id} Events {eventLo}-{eventHi}";

            bodyBuilder.Attachments.Add(
                $"notifications-{eventLo}-{eventHi}.{shortMime}",
                attachmentStream,
                ContentType.Parse(attachmentMime));
        }

        message.Body = bodyBuilder.ToMessageBody();
        return message;
    }

    /// <summary>Attempt to send a notification via Email.</summary>
    /// <param name="store">   The store.</param>
    /// <param name="e">       Subscription event information.</param>
    /// <param name="contents">Serialized contents of the notification.</param>
    /// <param name="cancellationToken">Cancellation of delivery during shutdown.</param>
    /// <returns>The revision-fenced email outcome.</returns>
    private async Task<Outcome> TryNotifyEmail(
        IFhirStore store,
        SubscriptionSendEventArgs e,
        string contents,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string from = e.Subscription.Parameters.ContainsKey("from") ? e.Subscription.Parameters["from"].First() : "FHIR Notification";
        string destination = e.Subscription.Endpoint ?? string.Empty;

        if (string.IsNullOrEmpty(destination) ||
            string.IsNullOrEmpty(_smtpHost) ||
            string.IsNullOrEmpty(_smtpUser) ||
            (_smtpPort == 0))
        {
            return await DispatchAsync(store, e, () => Task.FromResult(new Outcome(OutcomeKind.Failed))).ConfigureAwait(false);
        }

        if (destination.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
        {
            destination = destination.Substring(7).Trim();
        }

        if (destination.Contains("example.org", StringComparison.OrdinalIgnoreCase))
        {
            return await DispatchAsync(store, e, () => Task.FromResult(new Outcome(OutcomeKind.Simulated, contents))).ConfigureAwait(false);
        }

        // send the request to the endpoint
        try
        {
            using (MemoryStream ms = new MemoryStream((byte[])System.Text.Encoding.UTF8.GetBytes(contents)))
            {
                using MimeMessage message = BuildEmailMimeMessage(store, e, from, destination, ms);

                using (SmtpClient client = new())
                {
                    try
                    {
                        Outcome outcome = await EmailStepAsync(store, e,
                            () => SmtpConnect(client, _smtpHost, _smtpPort, cancellationToken), null, cancellationToken).ConfigureAwait(false);
                        if (outcome.Kind != OutcomeKind.SetupComplete)
                        {
                            return outcome;
                        }
                        outcome = await EmailStepAsync(store, e,
                            () => SmtpAuthenticate(client, _smtpUser, _smtpPassword, cancellationToken), null, cancellationToken).ConfigureAwait(false);
                        if (outcome.Kind != OutcomeKind.SetupComplete)
                        {
                            return outcome;
                        }
                        return await EmailStepAsync(store, e,
                            () => SmtpSend(client, message, cancellationToken), contents, cancellationToken).ConfigureAwait(false);
                    }
                    finally
                    {
                        // Cleanup is not delivery and remains permitted after revocation.
                        await SmtpDisconnect(client, cancellationToken).ConfigureAwait(false);
                    }
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return await PreparationFailureAsync(store, e, "Email", ex).ConfigureAwait(false);
        }

    }

    private async Task<Outcome> EmailStepAsync(IFhirStore store, SubscriptionSendEventArgs args,
        Func<Task> start, string? contents, CancellationToken cancellationToken)
    {
        Exception? failure = null;
        Task<Outcome> Start()
        {
            // Invoke the existing MailKit API before the starter returns, including synchronous errors.
            try
            {
                return Observe(start());
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                failure = exception;
                return Task.FromResult(new Outcome(OutcomeKind.Failed,
                    Error: $"Email {args.NotificationType} to {args.Subscription.Endpoint} failed: {exception.Message}"));
            }
        }
        async Task<Outcome> Observe(Task operation)
        {
            try
            {
                await operation.ConfigureAwait(false);
                return new(contents is null ? OutcomeKind.SetupComplete : OutcomeKind.Delivered, contents);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                failure = exception;
                return new(OutcomeKind.Failed,
                    Error: $"Email {args.NotificationType} to {args.Subscription.Endpoint} failed: {exception.Message}");
            }
        }
        Outcome outcome = await DispatchAsync(store, args, Start).ConfigureAwait(false);
        if (outcome.Kind == OutcomeKind.Failed && failure is not null)
        {
            _logger.LogError(failure, "Email notification delivery failed for Subscription/{SubscriptionId}", args.Subscription.Id);
        }
        return outcome;
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
        cancellationToken.ThrowIfCancellationRequested();
        List<SaturationWarning> warnings = [];
        lock (Scheduler.Gate)
        {
            ObjectDisposedException.ThrowIf(_hasDisposed, this);
            if (Scheduler.IsStopped)
            {
                throw new InvalidOperationException("NotificationManager has already stopped.");
            }
            if (_started)
            {
                return Task.CompletedTask;
            }
            _started = true;
            Scheduler.Start();
            _logger.LogInformation("Starting NotificationManager...");

            foreach (IFhirStore store in _storeManager.Values)
            {
                store.OnSubscriptionsChanged += Store_OnSubscriptionsChanged;
                store.OnSubscriptionSendEvent += Store_OnSubscriptionSendEvent;
                foreach (ParsedSubscription subscription in store.CurrentSubscriptions)
                {
                    long revision = subscription.Revision;
                    SaturationWarning? warning = ApplySubscriptionChangeLocked(store, new()
                    {
                        Tenant = store.Config,
                        ChangedSubscription = subscription,
                        Revision = revision,
                        SendHandshake = subscription.CurrentStatus == "requested",
                    });
                    if (warning is not null) { warnings.Add(warning); }
                }
            }

            _heartbeatTimer = _clock.CreateTimer(
                CheckAndSendHeartbeats,
                null,
                TimeSpan.Zero,
                TimeSpan.FromSeconds(2));
        }
        foreach (SaturationWarning warning in warnings) { Scheduler.EmitWarning(warning); }
        return Task.CompletedTask;
    }

    /// <summary>Observes every delivery, including failures before a channel has been selected.</summary>
    private async Task DeliverAsync(IFhirStore store, SubscriptionSendEventArgs args, CancellationToken cancellationToken)
    {
        try
        {
            Outcome outcome = await TryNotify(store, args, cancellationToken).ConfigureAwait(false);
            if (outcome.Kind == OutcomeKind.Failed)
            {
                _logger.LogWarning("Notification delivery failed for Subscription/{SubscriptionId}", args.Subscription.Id);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Notification delivery canceled during shutdown for Subscription/{SubscriptionId}", args.Subscription.Id);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Notification delivery failed for Subscription/{SubscriptionId}", args.Subscription.Id);
        }
        finally
        {
            if (args.NotificationType == ParsedSubscription.NotificationTypeCodes.Heartbeat)
            {
                lock (Scheduler.Gate)
                {
                    (IFhirStore, string, long) key = (store, args.Subscription.Id, args.Revision);
                    if (!Scheduler.IsStopped && _heartbeatReservations.ContainsKey(key))
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
        List<SaturationWarning> warnings = [];
        lock (Scheduler.Gate)
        {
            if (Scheduler.IsStopped)
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
                    if (next == long.MaxValue || currentTicks < Math.Max(next, sub.LastCommunicationTicks + interval))
                    {
                        continue;
                    }
                    if (Scheduler.TryEnqueue(store, new()
                    {
                        Tenant = store.Config,
                        Subscription = sub,
                        Revision = revision,
                        NotificationType = ParsedSubscription.NotificationTypeCodes.Heartbeat,
                    }, out SaturationWarning? warning))
                    {
                        _heartbeatReservations[key] = long.MaxValue;
                    }
                    else if (warning is not null) { warnings.Add(warning); }
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
        foreach (SaturationWarning warning in warnings) { Scheduler.EmitWarning(warning); }
    }

    /// <summary>Event handler. Called by Store for on subscription events.</summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">     Subscription event information.</param>
    private void Store_OnSubscriptionSendEvent(object? sender, SubscriptionSendEventArgs e)
    {
        SaturationWarning? warning;
        lock (Scheduler.Gate)
        {
            if (Scheduler.IsStopped)
            {
                return;
            }
            if (!_storeManager.ContainsKey(e.Tenant.ControllerName))
            {
                _logger.LogError($"Cannot send subscription for non-existing tenant: {e.Tenant.ControllerName}");
                return;
            }

            Scheduler.TryEnqueue(_storeManager[e.Tenant.ControllerName], e, out warning);
        }
        Scheduler.EmitWarning(warning);
    }

    /// <summary>Event handler. Called by Store for on subscriptions changed events.</summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">     Subscription changed event information.</param>
    private void Store_OnSubscriptionsChanged(object? sender, SubscriptionChangedEventArgs e)
    {
        SaturationWarning? warning;
        lock (Scheduler.Gate)
        {
            if (Scheduler.IsStopped || !_storeManager.ContainsKey(e.Tenant.ControllerName))
            {
                return;
            }

            warning = ApplySubscriptionChangeLocked(_storeManager[e.Tenant.ControllerName], e);
        }
        Scheduler.EmitWarning(warning);
    }

    private SaturationWarning? ApplySubscriptionChangeLocked(IFhirStore store, SubscriptionChangedEventArgs e)
    {
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

        if (!e.SendHandshake || e.ChangedSubscription is null) { return null; }
        // Subscription changes publish after unlocking and may race the startup snapshot.
        ParsedSubscription? current = store.CurrentSubscriptions.FirstOrDefault(subscription => subscription.Id == changedId);
        if (current is null || !ReferenceEquals(current, e.ChangedSubscription) || current.Revision != e.Revision ||
            !SubscriptionLifecyclePolicy.CanDeliverNotification(current.CurrentStatus, current.ExpirationTicks,
                ParsedSubscription.NotificationTypeCodes.Handshake, _clock.GetUtcNow()) ||
            (_handshakeRevisions.TryGetValue((store, current.Id), out long queuedRevision) && queuedRevision >= e.Revision))
        {
            return null;
        }

        if (Scheduler.TryEnqueue(store, new()
        {
            Tenant = e.Tenant,
            Subscription = current,
            Revision = e.Revision,
            NotificationType = ParsedSubscription.NotificationTypeCodes.Handshake,
        }, out SaturationWarning? warning))
        {
            _handshakeRevisions[(store, current.Id)] = e.Revision;
        }
        return warning;
    }

    /// <summary>Triggered when the application host is performing a graceful shutdown.</summary>
    /// <param name="cancellationToken">Indicates that the shutdown process should no longer be
    ///  graceful.</param>
    /// <returns>An asynchronous result.</returns>
    Task IHostedService.StopAsync(CancellationToken cancellationToken) => Scheduler.StopAsync(DetachProducers, cancellationToken);

    private ValueTask DetachProducers()
    {
        if (_started)
        {
            foreach (IFhirStore store in _storeManager.Values)
            {
                store.OnSubscriptionsChanged -= Store_OnSubscriptionsChanged;
                store.OnSubscriptionSendEvent -= Store_OnSubscriptionSendEvent;
            }
        }
        _heartbeatReservations.Clear();
        _handshakeRevisions.Clear();
        return _heartbeatTimer?.DisposeAsync() ?? ValueTask.CompletedTask;
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
        if (!disposing)
        {
            return;
        }

        Scheduler.DisposeAfterDrain(DetachProducers);
        lock (Scheduler.Gate)
        {
            if (_hasDisposed)
            {
                return;
            }
            if (_ownsHttpClient)
            {
                _httpClient.Dispose();
            }
            _zulipSender.Dispose();
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
