// <copyright file="MailKitCompatibilityTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using System.CommandLine;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using fhir.candle.Services;
using fhir.candle.Tests.Controllers;
using fhir.candle.Tests.Extensions;
using FhirCandle.Configuration;
using FhirCandle.Models;
using FhirCandle.Storage;
using FhirCandle.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MimeKit;

namespace fhir.candle.Tests;

/// <summary>Characterizes configuration, production MIME construction, and pre-network email paths only.</summary>
[Collection(CompatibilityHostCollection.Name)]
public sealed class MailKitCompatibilityTests
{
    private const long CommunicationSentinel = 123456789;
    private const string JsonPayload = "{\n  \"resourceType\": \"Bundle\",\r\n  \"type\": \"history\",\n  \"meta\": {\"tag\": [{\"display\": \"café 雪\"}]}\n}\n";
    private const string XmlPayload = "<Bundle xmlns=\"http://hl7.org/fhir\">\n  <type value=\"history\"/>\r\n  <meta><tag><display value=\"café 雪\"/></tag></meta>\n</Bundle>\n";
    private const string HandshakeBody = "Subscription mailkit-sub (http://127.0.0.1/fhir/mailkit-r4/Subscription/mailkit-sub) is sending you a Handshake notification.\n\nThe FHIR contents can be found as an attachment to this message.";
    private const string HeartbeatBody = "Subscription mailkit-sub (http://127.0.0.1/fhir/mailkit-r4/Subscription/mailkit-sub) is sending you a Heartbeat notification.\n\nThe FHIR contents can be found as an attachment to this message.";
    private const string SingleEventBody = "Subscription mailkit-sub (http://127.0.0.1/fhir/mailkit-r4/Subscription/mailkit-sub) is sending you a EventNotification notification for events 7.\n\nThe FHIR contents can be found as an attachment to this message.";
    private const string MultipleEventsBody = "Subscription mailkit-sub (http://127.0.0.1/fhir/mailkit-r4/Subscription/mailkit-sub) is sending you a EventNotification notification for events 9,2,7.\n\nThe FHIR contents can be found as an attachment to this message.";

    /// <summary>Six bounded MIME inputs with independently specified decoded expectations.</summary>
    public static IEnumerable<object[]> MimeCases =>
    [
        ["default handshake", string.Empty, ParsedSubscription.NotificationTypeCodes.Handshake, Array.Empty<long>(), "FHIR Notification",
            "Subscription mailkit-sub Handshake", "text/plain", "application/fhir+json", "notifications-Handshake.json", HandshakeBody],
        ["HTML JSON heartbeat / Unicode sender", "text/html;application/fhir+json", ParsedSubscription.NotificationTypeCodes.Heartbeat, Array.Empty<long>(), "Notificações 雪",
            "Subscription mailkit-sub Heartbeat", "text/html", "application/fhir+json", "notifications-Heartbeat.json", HeartbeatBody],
        ["plain XML single event", "text/plain;application/fhir+xml", ParsedSubscription.NotificationTypeCodes.EventNotification, new long[] { 7 }, "FHIR Notification",
            "Subscription mailkit-sub Event 7", "text/plain", "application/fhir+xml", "notification-7.xml", SingleEventBody],
        ["HTML XML unsorted events", "text/html;application/fhir+xml", ParsedSubscription.NotificationTypeCodes.EventNotification, new long[] { 9, 2, 7 }, "FHIR Notification",
            "Subscription mailkit-sub Events 2-9", "text/html", "application/fhir+xml", "notifications-2-9.xml", MultipleEventsBody],
        ["JSON shorthand single event", "application/json", ParsedSubscription.NotificationTypeCodes.EventNotification, new long[] { 7 }, "FHIR Notification",
            "Subscription mailkit-sub Event 7", "text/plain", "application/fhir+json", "notification-7.json", SingleEventBody],
        ["XML shorthand heartbeat", "xml", ParsedSubscription.NotificationTypeCodes.Heartbeat, Array.Empty<long>(), "FHIR Notification",
            "Subscription mailkit-sub Heartbeat", "text/plain", "application/fhir+xml", "notifications-Heartbeat.xml", HeartbeatBody],
    ];

    /// <summary>SMTP defaults, external binding, and explicit CLI precedence use no ambient configuration.</summary>
    [Theory]
    [InlineData("defaults", null, 465, null, null)]
    [InlineData("external", "external.unit.test", 2465, "external-user", "synthetic-external-password")]
    [InlineData("cli", "cli.unit.test", 3465, "cli-user", "synthetic-cli-password")]
    public void SmtpConfigurationPreservesDefaultsExternalValuesAndCliPrecedence(
        string mode, string? expectedHost, int expectedPort, string? expectedUser, string? expectedPassword)
    {
        using LocalPackageFixture files = new();
        Dictionary<string, string?> values = mode == "defaults" ? [] : new()
        {
            ["SMTP_Host"] = "external.unit.test",
            ["SMTP_Port"] = "2465",
            ["SMTP_User"] = "external-user",
            ["SMTP_Password"] = "synthetic-external-password",
        };
        using ConfigurationRoot external = (ConfigurationRoot)new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        List<string> arguments =
        [
            "--fhir-source", files.SourceDirectory, "--fhir-package-cache", files.CacheDirectory,
            "--use-official-registries", "false",
        ];
        if (mode == "cli")
        {
            arguments.AddRange(
            [
                "--smtp-host", "cli.unit.test", "--smtp-port", "3465",
                "--smtp-user", "cli-user", "--smtp-password", "synthetic-cli-password",
            ]);
        }
        CliRootCommand command = new();
        ParseResult parsed = command.Parse(arguments.ToArray(), new ParserConfiguration { ResponseFileTokenReplacer = null });
        Assert.Empty(parsed.Errors);
        CandleConfig config = new(command.CommandCliOptions, parsed, external);
        Assert.Equal(files.SourceDirectory, config.SourceDirectory);
        Assert.Equal(files.CacheDirectory, config.FhirCacheDirectory);
        Assert.Equal(expectedHost, config.SmtpHost);
        Assert.Equal(expectedPort, config.SmtpPort);
        Assert.Equal(expectedUser, config.SmtpUser);
        Assert.Equal(expectedPassword, config.SmtpPassword);
    }

    /// <summary>Serialize the actual private builder's message, then inspect decoded wire content without SMTP.</summary>
    [Theory]
    [MemberData(nameof(MimeCases))]
    public void EmailMimeMessagePreservesContentAndAttachmentBytes(
        string label, string contentType, ParsedSubscription.NotificationTypeCodes notificationType, long[] eventNumbers,
        string from, string expectedSubject, string expectedBodyMime, string expectedAttachmentMime,
        string expectedFileName, string expectedBody)
    {
        Assert.False(string.IsNullOrEmpty(label));
        using NotificationHost host = new();
        SubscriptionSendEventArgs args = EventArgs(host, notificationType, contentType, eventNumbers);
        byte[] payload = Encoding.UTF8.GetBytes(expectedAttachmentMime == "application/fhir+xml" ? XmlPayload : JsonPayload);
        using MemoryStream input = new(payload);
        MethodInfo builder = ResolveMethod("BuildEmailMimeMessage", typeof(MimeMessage),
            typeof(IFhirStore), typeof(SubscriptionSendEventArgs), typeof(string), typeof(string), typeof(Stream));
        using MimeMessage message = Assert.IsType<MimeMessage>(
            Invoke(builder, host.Service, host.Store, args, from, "recipient@unit.test", input));
        Assert.True(input.CanRead, "The builder must not close its caller-owned attachment stream.");
        using MemoryStream wire = new();
        message.WriteTo(wire);
        Assert.True(input.CanRead, "The attachment input must remain open through serialization.");
        wire.Position = 0;
        using MimeMessage decoded = MimeMessage.Load(wire);
        MailboxAddress sender = Assert.IsType<MailboxAddress>(Assert.Single(decoded.From));
        Assert.Equal("sender@unit.test", sender.Address);
        Assert.Equal(from, sender.Name);
        MailboxAddress recipient = Assert.IsType<MailboxAddress>(Assert.Single(decoded.To));
        Assert.Equal("recipient@unit.test", recipient.Address);
        Assert.Equal("recipient@unit.test", recipient.Name);
        Assert.Equal(expectedSubject, decoded.Subject);
        TextPart body = Assert.Single(decoded.BodyParts.OfType<TextPart>());
        Assert.Equal(expectedBodyMime, body.ContentType.MimeType);
        Assert.Equal(expectedBody, body.Text.ReplaceLineEndings("\n"));
        if (expectedBodyMime == "text/html")
        {
            Assert.Null(decoded.TextBody);
            Assert.Equal(expectedBody, Assert.IsType<string>(decoded.HtmlBody).ReplaceLineEndings("\n"));
        }
        else
        {
            Assert.Null(decoded.HtmlBody);
            Assert.Equal(expectedBody, Assert.IsType<string>(decoded.TextBody).ReplaceLineEndings("\n"));
        }
        MimePart attachment = Assert.IsType<MimePart>(Assert.Single(decoded.Attachments));
        Assert.Equal(expectedAttachmentMime, attachment.ContentType.MimeType);
        Assert.Equal(expectedFileName, attachment.FileName);
        using MemoryStream attachmentBytes = new();
        Assert.NotNull(attachment.Content);
        attachment.Content.DecodeTo(attachmentBytes);
        Assert.Equal(payload, attachmentBytes.ToArray());
    }

    /// <summary>Each existing required-field guard returns before even invalid-port validation.</summary>
    [Theory]
    [InlineData("destination")]
    [InlineData("host")]
    [InlineData("user")]
    [InlineData("port")]
    public async Task EmailMissingDestinationOrRequiredConfigurationReturnsFalseWithoutCommunication(string missing)
    {
        using NotificationHost host = new(config =>
        {
            if (missing == "host")
            {
                config.SmtpHost = string.Empty;
            }
            if (missing == "user")
            {
                config.SmtpUser = string.Empty;
            }
            if (missing == "port")
            {
                config.SmtpPort = 0;
            }
        });
        SubscriptionSendEventArgs args = EventArgs(host);
        if (missing == "destination")
        {
            args.Subscription.Endpoint = string.Empty;
        }
        Assert.False(await NotifyAsync(host, args));
        AssertNoCommunication(host, args);
    }

    /// <summary>The reserved-domain shortcut is not authenticated transport or a communication timestamp.</summary>
    [Theory]
    [InlineData("recipient@example.org")]
    [InlineData("MaIlTo: recipient@Example.org ")]
    public async Task EmailExampleDomainShortcutIsNotTransportDelivery(string endpoint)
    {
        using NotificationHost host = new();
        SubscriptionSendEventArgs args = EventArgs(host);
        args.Subscription.Endpoint = endpoint;
        Assert.True(await NotifyAsync(host, args));
        AssertNoCommunication(host, args);
    }

    /// <summary>An entry-canceled token propagates without delivery or failure bookkeeping.</summary>
    [Fact]
    public async Task EmailPreCanceledTokenPropagatesWithoutBookkeeping()
    {
        using NotificationHost host = new(config => config.SmtpPort = 465);
        SubscriptionSendEventArgs args = EventArgs(host);
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => NotifyAsync(host, args, cancellation.Token));
        AssertNoCommunication(host, args);
    }

    /// <summary>An impossible TCP port proves local argument-validation failure, not SMTP rejection.</summary>
    [Fact]
    public async Task EmailInvalidPortRegistersFailureWithoutCommunication()
    {
        using NotificationHost host = new();
        SubscriptionSendEventArgs args = EventArgs(host);
        Assert.False(await NotifyAsync(host, args));
        Assert.Equal(CommunicationSentinel, args.Subscription.LastCommunicationTicks);
        string error = Assert.Single(args.Subscription.NotificationErrors);
        Assert.Contains("Email Handshake", error, StringComparison.Ordinal);
        Assert.Contains("recipient@unit.test", error, StringComparison.Ordinal);
        Assert.Contains("failed:", error, StringComparison.Ordinal);
        (LogLevel Level, string Message, Exception? Exception) logged =
            Assert.Single(host.Log.Entries, entry => entry.Level >= LogLevel.Error);
        Assert.Equal(LogLevel.Error, logged.Level);
        Assert.Contains("Email notification delivery failed", logged.Message, StringComparison.Ordinal);
        Assert.Contains("Subscription/mailkit-sub", logged.Message, StringComparison.Ordinal);
        ArgumentOutOfRangeException exception = Assert.IsType<ArgumentOutOfRangeException>(logged.Exception);
        Assert.Equal("port", exception.ParamName);
    }

    private static SubscriptionSendEventArgs EventArgs(
        NotificationHost host,
        ParsedSubscription.NotificationTypeCodes notificationType = ParsedSubscription.NotificationTypeCodes.Handshake,
        string contentType = "",
        params long[] eventNumbers) => new()
        {
            Tenant = host.Store.Config,
            Subscription = new()
            {
                Id = "mailkit-sub",
                TopicUrl = "http://unit.test/SubscriptionTopic/mailkit",
                ExpirationTicks = DateTime.MaxValue.Ticks,
                ChannelCode = "email",
                Endpoint = "recipient@unit.test",
                ContentType = contentType,
                LastCommunicationTicks = CommunicationSentinel,
            },
            NotificationType = notificationType,
            NotificationEvents = eventNumbers.Select(number => new SubscriptionEvent
            {
                SubscriptionId = "mailkit-sub",
                TopicUrl = "http://unit.test/SubscriptionTopic/mailkit",
                EventNumber = number,
                Focus = new object(),
            }).ToArray(),
        };

    private static void AssertNoCommunication(NotificationHost host, SubscriptionSendEventArgs args)
    {
        Assert.Equal(CommunicationSentinel, args.Subscription.LastCommunicationTicks);
        Assert.Empty(args.Subscription.NotificationErrors);
        Assert.DoesNotContain(host.Log.Entries, entry => entry.Level >= LogLevel.Error);
    }

    private static MethodInfo ResolveMethod(string name, Type returnType, params Type[] parameterTypes)
    {
        MethodInfo? method = typeof(NotificationManager).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null, types: parameterTypes, modifiers: null);
        Assert.True(method is not null, $"Missing NotificationManager.{name}({string.Join(", ", parameterTypes.Select(type => type.Name))}).");
        Assert.True(method.IsPrivate, $"NotificationManager.{name} must remain private.");
        Assert.Equal(returnType, method.ReturnType);
        return method;
    }

    private static object? Invoke(MethodInfo method, NotificationManager service, params object[] arguments)
    {
        try
        {
            return method.Invoke(service, arguments);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }

    private static async Task<bool> NotifyAsync(
        NotificationHost host, SubscriptionSendEventArgs args, CancellationToken cancellationToken = default)
    {
        MethodInfo method = ResolveMethod("TryNotifyEmail", typeof(Task<bool>),
            typeof(IFhirStore), typeof(SubscriptionSendEventArgs), typeof(string), typeof(CancellationToken));
        Task<bool> delivery = Assert.IsAssignableFrom<Task<bool>>(
            Invoke(method, host.Service, host.Store, args, JsonPayload, cancellationToken));
        return await delivery;
    }

    private sealed class NotificationHost : IDisposable
    {
        private readonly FhirPackageService? _packages;
        private readonly FhirStoreManager? _manager;
        private bool _disposed;

        internal NotificationHost(Action<CandleConfig>? configure = null)
        {
            Files = new LocalPackageFixture();
            try
            {
                CandleConfig config = Files.Configuration();
                config.SmtpHost = "127.0.0.1";
                config.SmtpPort = -1;
                config.SmtpUser = "sender@unit.test";
                config.SmtpPassword = "synthetic-password";
                configure?.Invoke(config);
                TenantConfiguration tenant = new()
                {
                    FhirVersion = FhirReleases.FhirSequenceCodes.R4,
                    ControllerName = "mailkit-r4",
                    BaseUrl = "http://127.0.0.1/fhir/mailkit-r4",
                };
                Dictionary<string, TenantConfiguration> tenants = new(StringComparer.Ordinal)
                {
                    [tenant.ControllerName] = tenant,
                };
                _packages = new FhirPackageService(NullLogger<FhirPackageService>.Instance, config);
                _manager = new FhirStoreManager(tenants, NullLogger<FhirStoreManager>.Instance, config, _packages);
                _manager.Init();
                Store = Assert.Single(((IFhirStoreManager)_manager).Values);
                Service = new NotificationManager(config, tenants, _manager, Log);
                // Neither hosted service is started; no subscription handlers or notification timers run.
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        private LocalPackageFixture Files { get; }
        internal IFhirStore Store { get; } = null!;
        internal NotificationManager Service { get; } = null!;
        internal NotificationLog Log { get; } = new();

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            using (Files)
            using ((IDisposable?)_packages)
            using ((IDisposable?)_manager)
            {
                try
                {
                    ((IDisposable?)Service)?.Dispose();
                }
                finally
                {
                    if (_manager is not null)
                    {
                        foreach (IFhirStore store in ((IFhirStoreManager)_manager).Values)
                        {
                            store.Dispose();
                        }
                    }
                }
            }
        }
    }

    private sealed class NotificationLog : ILogger<NotificationManager>
    {
        internal List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = [];

        /// <inheritdoc />
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        /// <inheritdoc />
        public bool IsEnabled(LogLevel logLevel) => true;

        /// <inheritdoc />
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception), exception));
    }
}
