// <copyright file="OpenTelemetryCompatibilityTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using System.Buffers.Binary;
using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using fhir.candle.Tests.Controllers;
using FhirCandle.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Xunit.Abstractions;

namespace fhir.candle.Tests;

/// <summary>Exercises the private production telemetry configuration with real retained exporters.</summary>
[Collection(CompatibilityHostCollection.Name)]
public sealed class OpenTelemetryCompatibilityTests
{
    private static readonly TimeSpan _deadline = TimeSpan.FromSeconds(30);
    private readonly ITestOutputHelper _output;

    /// <summary>Creates a check with runner-owned evidence output.</summary>
    public OpenTelemetryCompatibilityTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>No endpoint means no providers, console telemetry, or loopback export requests.</summary>
    [Fact]
    public async Task DisabledTelemetryDoesNotCreateExportTraffic()
    {
        using IsolatedProcessState state = new();
        await using Receiver receiver = new(false);
        await receiver.StartAsync();
        await using TelemetryHost host = new(new CandleConfig());
        await host.StartAsync();
        host.AssertProviders(string.Empty);
        await host.RequestAsync("/phase11/first");
        host.Flush();
        await host.DisposeAsync();
        await Task.Delay(250);
        Assert.Empty(receiver.Requests);
        Assert.Equal(string.Empty, state.ConsoleText);
        Report("disabled: no providers, console output, or receiver requests");
    }

    /// <summary>Real ASP.NET instrumentation extracts W3C parent, tracestate and baggage across await.</summary>
    [Fact]
    public async Task TelemetryApiRetainsTraceAndBaggageContext()
    {
        using IsolatedProcessState state = new();
        await using Receiver receiver = new(true);
        await receiver.StartAsync();
        await using TelemetryHost host = new(Configuration(receiver, "grpc", "traces", "signals"));
        await host.StartAsync();
        using HttpRequestMessage request = new(HttpMethod.Get, "/phase11/first");
        request.Headers.Add("traceparent", "00-0123456789abcdef0123456789abcdef-0123456789abcdef-01");
        request.Headers.Add("tracestate", "phase11=local");
        request.Headers.Add("baggage", "phase11=synthetic");
        using HttpResponseMessage response = await host.Client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("0123456789abcdef0123456789abcdef", body.RootElement.GetProperty("traceId").GetString());
        Assert.Equal("0123456789abcdef", body.RootElement.GetProperty("parentSpanId").GetString());
        Assert.Equal("phase11=local", body.RootElement.GetProperty("traceState").GetString());
        Assert.Equal("synthetic", body.RootElement.GetProperty("baggage").GetString());
        Assert.True(body.RootElement.GetProperty("recorded").GetBoolean());
        await host.FlushRequestAsync(receiver, "traces", "/phase11/first");
        AssertDelivery(receiver, "traces", "/phase11/first");
        byte[] span = receiver.Records("traces").Single(record => Attributes(record, 9).GetValueOrDefault("http.route") == "/phase11/first");
        Assert.Equal("0123456789ABCDEF0123456789ABCDEF", Convert.ToHexString(Assert.Single(Fields(span, 1))));
        Assert.Equal("0123456789ABCDEF", Convert.ToHexString(Assert.Single(Fields(span, 4))));
        Report("inbound W3C parent/tracestate/baggage retained across await and delivered over gRPC");
    }

    /// <summary>Preserves endpoint composition, conditional signal registration and service.name on the wire.</summary>
    [Theory]
    [InlineData("grpc", "all", "general")]
    [InlineData("http/protobuf", "all", "general")]
    [InlineData("http/protobuf", "all", "slash")]
    [InlineData("http/protobuf", "all", "v1")]
    [InlineData("http/protobuf", "all", "v1-slash")]
    [InlineData("protobuf", "all", "general")]
    [InlineData("GrPc", "all", "general")]
    [InlineData("unknown", "all", "general")]
    [InlineData("grpc", "all", "overrides")]
    [InlineData("http/protobuf", "all", "overrides")]
    [InlineData("grpc", "traces", "signals")]
    [InlineData("http/protobuf", "traces", "signals")]
    [InlineData("grpc", "metrics", "signals")]
    [InlineData("http/protobuf", "metrics", "signals")]
    [InlineData("grpc", "logs", "signals")]
    [InlineData("http/protobuf", "logs", "signals")]
    [InlineData("grpc", "traces,metrics", "signals")]
    [InlineData("http/protobuf", "traces,metrics", "signals")]
    [InlineData("http/protobuf", "traces,logs", "signals")]
    [InlineData("grpc", "metrics,logs", "signals")]
    public async Task ConfiguredTelemetryPreservesServiceNameAndSignals(string protocol, string signals, string endpoints)
    {
        using IsolatedProcessState state = new();
        bool grpc = protocol is not ("http/protobuf" or "protobuf");
        await using Receiver receiver = new(grpc);
        await receiver.StartAsync();
        await using TelemetryHost host = new(Configuration(receiver, protocol, signals, endpoints));
        await host.StartAsync();
        host.AssertProviders(signals);
        await host.RequestAsync("/phase11/first");
        await host.FlushRequestAsync(receiver, signals, "/phase11/first");
        AssertDelivery(receiver, signals, "/phase11/first");
        AssertConsole(state.ConsoleText, signals, "/phase11/first");
        Report($"{protocol}; {signals}; {endpoints}: actual console and OTLP delivery, resource and ForceFlush verified");
    }

    /// <summary>Console output retains synthetic signal fields and the configured logger's scope metadata.</summary>
    [Theory]
    [InlineData("traces")]
    [InlineData("metrics")]
    [InlineData("logs")]
    [InlineData("all")]
    public async Task ConsoleExporterEmitsConfiguredSignals(string signals)
    {
        using IsolatedProcessState state = new();
        await using Receiver receiver = new(false);
        await receiver.StartAsync();
        await using TelemetryHost host = new(Configuration(receiver, "http/protobuf", signals, "signals"));
        await host.StartAsync();
        host.AssertProviders(signals);
        await host.RequestAsync("/phase11/first");
        await host.FlushRequestAsync(receiver, signals, "/phase11/first");
        AssertDelivery(receiver, signals, "/phase11/first");
        string text = state.ConsoleText;
        AssertConsole(text, signals, "/phase11/first");
        string[] lines = text.Split('\n').Select(line => line.Trim()).ToArray();
        if (Enabled(signals, "traces"))
        {
            Assert.Contains(lines, line => line.StartsWith("Activity.DisplayName:", StringComparison.Ordinal) &&
                line.EndsWith("GET /phase11/first", StringComparison.Ordinal));
            Assert.Contains(lines, line => line.StartsWith("Activity.Kind:", StringComparison.Ordinal) &&
                line.EndsWith("Server", StringComparison.Ordinal));
            Assert.Contains("http.route: /phase11/first", lines);
            Assert.Contains("Resource associated with Activity:", lines);
        }
        if (Enabled(signals, "metrics"))
        {
            Assert.Contains(lines, line => line.StartsWith("Metric Name: http.server.request.duration", StringComparison.Ordinal));
            Assert.Contains("Instrumentation scope (Meter):", lines);
            Assert.Contains("Name: Microsoft.AspNetCore.Hosting", lines);
            Assert.Contains("Resource associated with Metrics:", lines);
        }
        if (Enabled(signals, "logs"))
        {
            Assert.Contains(lines, line => line.StartsWith("LogRecord.CategoryName:", StringComparison.Ordinal) &&
                line.EndsWith("fhir-candle", StringComparison.Ordinal));
            Assert.Contains(lines, line => line.StartsWith("LogRecord.SeverityText:", StringComparison.Ordinal) &&
                line.EndsWith("Warning", StringComparison.Ordinal));
            Assert.Contains(lines, line => line.StartsWith("LogRecord.Body:", StringComparison.Ordinal) &&
                line.EndsWith("phase11 {Marker}", StringComparison.Ordinal));
            Assert.Contains("Marker: /phase11/first", lines);
            Assert.Contains("Resource associated with LogRecord:", lines);
        }
        // 1.20 adds logger scope rendering; keep the same regression runnable on retained 1.16.
        string exporterVersion = typeof(OpenTelemetry.Exporter.ConsoleExporterOptions).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;
        Version consoleVersion = Version.Parse(exporterVersion.Split('+')[0]);
        bool writesLoggerScope = consoleVersion >= new Version(1, 20, 0);
        Assert.Equal(Enabled(signals, "logs") && writesLoggerScope, lines.Contains("Instrumentation scope (Logger):"));
        if (Enabled(signals, "logs") && writesLoggerScope)
        {
            Assert.Contains("Name: fhir-candle", lines);
        }
        // 1.17 adds the schema URL supplied by the SDK's default resource, distinct from logger metadata.
        Assert.Equal(consoleVersion >= new Version(1, 17, 0),
            lines.Any(line => line.StartsWith("Schema URL: https://opentelemetry.io/schemas/", StringComparison.Ordinal)));
        if (signals == "logs" && writesLoggerScope)
        {
            // Production does not configure a logger version or logger schema URL.
            string[] loggerScope = lines.SkipWhile(line => line != "Instrumentation scope (Logger):").Skip(1)
                .TakeWhile(line => line != "Resource associated with LogRecord:").ToArray();
            Assert.DoesNotContain(loggerScope, line => line.StartsWith("Version:", StringComparison.Ordinal) ||
                line.StartsWith("Schema URL:", StringComparison.Ordinal));
        }
        Report($"{signals}: console signal fields, service resource and configured scope metadata verified");
    }

    /// <summary>Explicit flush and subsequent host disposal both deliver pending records, then stop exporting.</summary>
    [Theory]
    [InlineData("grpc")]
    [InlineData("http/protobuf")]
    public async Task TelemetryProvidersFlushAndDispose(string protocol)
    {
        using IsolatedProcessState state = new();
        await using Receiver receiver = new(protocol == "grpc");
        await receiver.StartAsync();
        await using TelemetryHost host = new(Configuration(receiver, protocol, "all", "general"));
        await host.StartAsync();
        await host.RequestAsync("/phase11/first");
        await host.FlushRequestAsync(receiver, "all", "/phase11/first");
        AssertDelivery(receiver, "all", "/phase11/first");
        AssertConsole(state.ConsoleText, "all", "/phase11/first");
        await host.RequestAsync("/phase11/final");
        // No explicit flush for this request: use the actual host/provider disposal path.
        await host.DisposeAsync();
        Assert.True(host.Lifetime.ApplicationStopped.IsCancellationRequested);
        Assert.Throws<ObjectDisposedException>(() => host.Services.GetRequiredService<TracerProvider>());
        AssertDelivery(receiver, "all", "/phase11/final");
        AssertConsole(state.ConsoleText, "all", "/phase11/final");
        int requests = receiver.Requests.Count;
        string console = state.ConsoleText;
        await Task.Delay(300);
        Assert.Equal(requests, receiver.Requests.Count);
        Assert.Equal(console, state.ConsoleText);
        Report($"{protocol}: explicit flush plus final unflushed request delivered on disposal; export traffic stopped");
    }

    /// <summary>The real hosted initializer creates singleton providers once; host disposal owns their shutdown.</summary>
    [Fact]
    public async Task TelemetryHostedProvidersStartAndStopOnce()
    {
        using IsolatedProcessState state = new();
        await using Receiver receiver = new(false);
        await receiver.StartAsync();
        int traceBuilds = 0;
        int metricBuilds = 0;
        int logBuilds = 0;
        await using TelemetryHost host = new(Configuration(receiver, "http/protobuf", "all", "general"), services =>
        {
            // Observe real SDK construction without resolving providers before the hosted initializer.
            services.ConfigureOpenTelemetryTracerProvider((_, _) => Interlocked.Increment(ref traceBuilds));
            services.ConfigureOpenTelemetryMeterProvider((_, _) => Interlocked.Increment(ref metricBuilds));
            services.ConfigureOpenTelemetryLoggerProvider((_, _) => Interlocked.Increment(ref logBuilds));
        });
        Assert.Equal(0, traceBuilds);
        Assert.Equal(0, metricBuilds);
        int starts = 0;
        int stops = 0;
        using CancellationTokenRegistration started = host.Lifetime.ApplicationStarted.Register(() => Interlocked.Increment(ref starts));
        using CancellationTokenRegistration stopped = host.Lifetime.ApplicationStopped.Register(() => Interlocked.Increment(ref stops));
        await host.StartAsync();
        Assert.Equal(1, starts);
        Assert.Equal(0, stops);
        AssertBuildsOnce();
        IHostedService initializer = Assert.Single(host.Services.GetServices<IHostedService>(),
            service => service.GetType().FullName == "OpenTelemetry.Extensions.Hosting.Implementation.TelemetryHostedService");
        TracerProvider traces = Assert.Single(host.Services.GetServices<TracerProvider>());
        MeterProvider metrics = Assert.Single(host.Services.GetServices<MeterProvider>());
        LoggerProvider logs = Assert.Single(host.Services.GetServices<LoggerProvider>());
        Assert.Single(host.Services.GetServices<ILoggerProvider>().OfType<OpenTelemetryLoggerProvider>());
        using (IServiceScope scope = host.Services.CreateScope())
        {
            AssertSameProviders(scope.ServiceProvider);
        }
        await host.RequestAsync("/phase11/first");
        await host.FlushRequestAsync(receiver, "all", "/phase11/first");
        AssertDelivery(receiver, "all", "/phase11/first");
        AssertConsole(state.ConsoleText, "all", "/phase11/first");
        await host.RequestAsync("/phase11/final");
        await host.StopAsync();
        Assert.True(host.Lifetime.ApplicationStopping.IsCancellationRequested);
        Assert.Equal(1, stops);
        AssertSameProviders(host.Services);
        AssertBuildsOnce();
        // TelemetryHostedService.StopAsync is a no-op in both versions; DI disposal flushes providers.
        await host.DisposeAsync();
        Assert.Throws<ObjectDisposedException>(() => host.Services.GetRequiredService<TracerProvider>());
        AssertDelivery(receiver, "all", "/phase11/final");
        AssertConsole(state.ConsoleText, "all", "/phase11/final");
        foreach (string route in new[] { "/phase11/first", "/phase11/final" })
        {
            foreach (string signal in new[] { "traces", "logs" })
            {
                Assert.Single(receiver.Records(signal), record => IsRequestRecord(record, signal, route));
            }
        }
        int requests = receiver.Requests.Count;
        string console = state.ConsoleText;
        await host.DisposeAsync();
        await Task.Delay(300);
        Assert.Equal(requests, receiver.Requests.Count);
        Assert.Equal(console, state.ConsoleText);
        Assert.Equal(1, starts);
        Assert.Equal(1, stops);
        AssertBuildsOnce();
        Report("one real hosted initializer; providers constructed once and identical across scopes/stop; " +
            "explicit flush and disposal-only final delivery without duplicate spans/logs; exports stopped");
        _output.WriteLine($"Hosting: {initializer.GetType().Assembly.Location}");

        void AssertBuildsOnce()
        {
            Assert.Equal(1, traceBuilds);
            Assert.Equal(1, metricBuilds);
            Assert.Equal(1, logBuilds);
        }

        void AssertSameProviders(IServiceProvider services)
        {
            Assert.Same(traces, Assert.Single(services.GetServices<TracerProvider>()));
            Assert.Same(metrics, Assert.Single(services.GetServices<MeterProvider>()));
            Assert.Same(logs, Assert.Single(services.GetServices<LoggerProvider>()));
            Assert.Same(initializer, Assert.Single(services.GetServices<IHostedService>(),
                service => service.GetType() == initializer.GetType()));
        }
    }

    /// <summary>Default no-retry exports report failure without poisoning subsequent exports.</summary>
    [Theory]
    [InlineData("grpc")]
    [InlineData("http/protobuf")]
    public async Task OtlpExporterRecoversAfterTransientReceiverFailure(string protocol)
    {
        using IsolatedProcessState state = new();
        await using Receiver receiver = new(protocol == "grpc", "/phase11/first", true);
        await receiver.StartAsync();
        await using TelemetryHost host = new(Configuration(receiver, protocol, "all", "general"));
        await host.StartAsync();
        await host.RequestAsync("/phase11/first");
        Stopwatch elapsed = Stopwatch.StartNew();
        bool metricExportFailed = false;
        do
        {
            // Batch trace/log ForceFlush reports draining, not exporter success. Metrics reports export failure.
            Assert.True(host.Services.GetRequiredService<TracerProvider>().ForceFlush(10000));
            metricExportFailed |= !host.Services.GetRequiredService<MeterProvider>().ForceFlush(10000);
            Assert.True(host.Services.GetRequiredService<LoggerProvider>().ForceFlush(10000));
            Assert.Empty(receiver.Errors);
            if (receiver.RejectedRequests.Count == 3)
            {
                break;
            }
            await Task.Delay(25);
        }
        while (elapsed.Elapsed < _deadline);
        Assert.True(metricExportFailed);
        foreach (string signal in new[] { "traces", "metrics", "logs" })
        {
            ExportRequest rejected = Assert.Single(receiver.RejectedRequests, request => request.Signal == signal);
            AssertWireRequest(receiver, rejected);
            Assert.Contains(Records(rejected), record => IsRequestRecord(record, signal, "/phase11/first"));
        }
        await host.RequestAsync("/phase11/final");
        await host.FlushRequestAsync(receiver, "all", "/phase11/final");
        AssertDelivery(receiver, "all", "/phase11/final");
        AssertConsole(state.ConsoleText, "all", "/phase11/final");
        Assert.Equal(3, receiver.RejectedRequests.Count);
        // Cumulative metrics may include the earlier route; dropped spans/logs must not be replayed.
        foreach (string signal in new[] { "traces", "logs" })
        {
            Assert.DoesNotContain(receiver.Records(signal), record => IsRequestRecord(record, signal, "/phase11/first"));
        }
        Report($"{protocol}: all signals rejected once with {(receiver.Grpc ? "grpc-status 14" : "HTTP 503")}, " +
            "metric flush failed, trace/log batches drained without replay; subsequent request delivered");
    }

    /// <summary>Non-retryable receiver failures do not prevent warmed providers from shutting down.</summary>
    [Theory]
    [InlineData("grpc")]
    [InlineData("http/protobuf")]
    public async Task OtlpExporterShutsDownAfterReceiverRejection(string protocol)
    {
        using IsolatedProcessState state = new();
        await using Receiver receiver = new(protocol == "grpc", "/phase11/final", false);
        await receiver.StartAsync();
        await using TelemetryHost host = new(Configuration(receiver, protocol, "all", "general"));
        await host.StartAsync();
        await host.RequestAsync("/phase11/first");
        await host.FlushRequestAsync(receiver, "all", "/phase11/first");
        AssertDelivery(receiver, "all", "/phase11/first");
        await host.RequestAsync("/phase11/final");
        // Do not flush again: disposal must attempt the pending export despite rejection.
        await host.DisposeAsync();
        Assert.True(host.Lifetime.ApplicationStopped.IsCancellationRequested);
        Assert.Throws<ObjectDisposedException>(() => host.Services.GetRequiredService<TracerProvider>());
        Assert.Empty(receiver.Errors);
        foreach (string signal in new[] { "traces", "metrics", "logs" })
        {
            ExportRequest[] rejected = receiver.RejectedRequests.Where(request => request.Signal == signal).ToArray();
            Assert.NotEmpty(rejected);
            Assert.All(rejected, request => AssertWireRequest(receiver, request));
            Assert.Contains(rejected.SelectMany(Records), record => IsRequestRecord(record, signal, "/phase11/final"));
            Assert.DoesNotContain(receiver.Records(signal), record => IsRequestRecord(record, signal, "/phase11/final"));
        }
        AssertConsole(state.ConsoleText, "all", "/phase11/final");
        int requests = receiver.Requests.Count;
        int rejections = receiver.RejectedRequests.Count;
        string console = state.ConsoleText;
        await Task.Delay(300);
        Assert.Equal(requests, receiver.Requests.Count);
        Assert.Equal(rejections, receiver.RejectedRequests.Count);
        Assert.Equal(console, state.ConsoleText);
        Report($"{protocol}: final unflushed records rejected with {(receiver.Grpc ? "grpc-status 3" : "HTTP 400")}; " +
            "no false delivery, bounded disposal completed and all export traffic stopped");
    }

    private void Report(string result)
    {
        _output.WriteLine($"{System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}: {result}");
        _output.WriteLine($"SDK: {typeof(Sdk).Assembly.Location}; API: {typeof(Baggage).Assembly.Location}");
        _output.WriteLine($"OTLP: {typeof(OpenTelemetry.Exporter.OtlpExporterOptions).Assembly.Location}");
    }

    private static bool Enabled(string signals, string signal) => signals == "all" || signals.Split(',').Contains(signal);

    private static CandleConfig Configuration(Receiver receiver, string protocol, string signals, string endpoints)
    {
        CandleConfig config = new() { OpenTelemetryProtocol = protocol };
        string address = receiver.Address.TrimEnd('/');
        if (endpoints != "signals")
        {
            config.OpenTelemetryEndpoint = address + (endpoints switch
            {
                "slash" => "/",
                "v1" => "/v1",
                "v1-slash" => "/v1/",
                "overrides" => "/must-not-receive",
                _ => string.Empty,
            });
        }
        if (endpoints is "signals" or "overrides")
        {
            config.OpenTelemetryTracesEndpoint = Enabled(signals, "traces") ? receiver.Endpoint("traces") : null;
            config.OpenTelemetryMetricsEndpoint = Enabled(signals, "metrics") ? receiver.Endpoint("metrics") : null;
            config.OpenTelemetryLogsEndpoint = Enabled(signals, "logs") ? receiver.Endpoint("logs") : null;
        }
        return config;
    }

    private static void AssertConsole(string text, string signals, string route)
    {
        Assert.Equal(Enabled(signals, "traces"), text.Contains("Activity.TraceId:", StringComparison.Ordinal));
        Assert.Equal(Enabled(signals, "metrics"), text.Contains("http.server.request.duration", StringComparison.Ordinal));
        Assert.Equal(Enabled(signals, "logs"), text.Contains("LogRecord.", StringComparison.Ordinal));
        Assert.Contains("service.name: fhir-candle", text);
        Assert.Contains(route, text);
    }

    private static void AssertDelivery(Receiver receiver, string signals, string route)
    {
        Assert.Empty(receiver.Errors);
        foreach (string signal in new[] { "traces", "metrics", "logs" })
        {
            ExportRequest[] requests = receiver.Requests.Where(request => request.Signal == signal).ToArray();
            if (!Enabled(signals, signal))
            {
                Assert.Empty(requests);
                continue;
            }
            Assert.True(requests.Length != 0, $"No {signal} export requests received.");
            foreach (ExportRequest request in requests)
            {
                AssertWireRequest(receiver, request);
            }
            byte[][] records = receiver.Records(signal).ToArray();
            Assert.Contains(records, record => IsRequestRecord(record, signal, route));
        }
        Assert.All(receiver.Requests, request => Assert.Contains(request.Signal, new[] { "traces", "metrics", "logs" }));
    }

    private static void AssertWireRequest(Receiver receiver, ExportRequest request)
    {
        Assert.Equal("POST", request.Method);
        Assert.Equal(receiver.Grpc ? "HTTP/2" : "HTTP/1.1", request.Protocol);
        Assert.Equal(receiver.Grpc ? "application/grpc" : "application/x-protobuf", request.ContentType);
        foreach (byte[] resourceSignals in Fields(request.Body, 1))
        {
            byte[] resource = Assert.Single(Fields(resourceSignals, 1));
            Assert.Equal("fhir-candle", Attributes(resource, 1)["service.name"]);
        }
    }

    private static IEnumerable<byte[]> Records(ExportRequest request) => Fields(request.Body, 1)
        .SelectMany(resource => Fields(resource, 2))
        .SelectMany(scope => Fields(scope, 2));

    private static bool IsRequestRecord(byte[] record, string signal, string route) => signal switch
    {
        "traces" => Attributes(record, 9).GetValueOrDefault("http.route") == route,
        "metrics" => Text(record, 1) == "http.server.request.duration" &&
            Fields(record, 9).SelectMany(histogram => Fields(histogram, 1))
                .Any(point => Attributes(point, 9).GetValueOrDefault("http.route") == route),
        "logs" => Attributes(record, 6).GetValueOrDefault("Marker") == route,
        _ => false,
    };

    private static Dictionary<string, string> Attributes(byte[] message, int field)
    {
        Dictionary<string, string> result = new(StringComparer.Ordinal);
        foreach (byte[] pair in Fields(message, field))
        {
            byte[] value = Assert.Single(Fields(pair, 2));
            result[Text(pair, 1)] = Text(value, 1);
        }
        return result;
    }

    private static string Text(byte[] message, int field) =>
        Encoding.UTF8.GetString(Fields(message, field).SingleOrDefault() ?? []);

    // A bounded wire reader for the OTLP fields asserted above, not a generated telemetry model.
    private static IEnumerable<byte[]> Fields(byte[] message, int field)
    {
        int offset = 0;
        while (offset < message.Length)
        {
            ulong tag = ReadVarint();
            int wire = (int)(tag & 7);
            if (wire == 2)
            {
                int length = checked((int)ReadVarint());
                Assert.InRange(length, 0, message.Length - offset);
                if ((int)(tag >> 3) == field)
                {
                    yield return message.AsSpan(offset, length).ToArray();
                }
                offset += length;
            }
            else if (wire == 0)
            {
                _ = ReadVarint();
            }
            else
            {
                Assert.Contains(wire, new[] { 1, 5 });
                offset += wire == 1 ? 8 : 4;
                Assert.InRange(offset, 0, message.Length);
            }
        }

        ulong ReadVarint()
        {
            ulong result = 0;
            for (int shift = 0; shift < 64; shift += 7)
            {
                Assert.True(offset < message.Length, "Truncated protobuf varint.");
                byte value = message[offset++];
                result |= (ulong)(value & 127) << shift;
                if ((value & 128) == 0)
                {
                    return result;
                }
            }
            throw new InvalidDataException("Oversized protobuf varint.");
        }
    }

    private static WebApplicationBuilder Builder(HttpProtocols protocol)
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
        {
            Args = [],
            ApplicationName = typeof(Program).Assembly.GetName().Name,
            EnvironmentName = Environments.Production,
            ContentRootPath = AppContext.BaseDirectory,
        });
        builder.Configuration.Sources.Clear();
        builder.Logging.ClearProviders();
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Limits.MaxRequestBodySize = 1024 * 1024;
            options.Listen(IPAddress.Loopback, 0, listen => listen.Protocols = protocol);
        });
        return builder;
    }

    private static string AddressOf(WebApplication app) =>
        Assert.Single(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses);

    private sealed class TelemetryHost : IAsyncDisposable
    {
        private readonly WebApplication _app;
        private bool _disposed;
        private bool _stopped;

        internal TelemetryHost(CandleConfig config, Action<IServiceCollection>? observe = null)
        {
            WebApplicationBuilder builder = Builder(HttpProtocols.Http1);
            MethodInfo configure = typeof(Program).GetMethod("ConfigureOpenTelemetry", BindingFlags.Static | BindingFlags.NonPublic)
                ?? throw new MissingMethodException(typeof(Program).FullName, "ConfigureOpenTelemetry");
            configure.Invoke(null, [config, builder]);
            observe?.Invoke(builder.Services);
            _app = builder.Build();
            Lifetime = _app.Services.GetRequiredService<IHostApplicationLifetime>();
            foreach (string route in new[] { "/phase11/first", "/phase11/final" })
            {
                _app.MapGet(route, async (HttpContext context) =>
                {
                    await Task.Yield();
                    _app.Logger.LogWarning("phase11 {Marker}", context.Request.Path.Value);
                    Activity? activity = Activity.Current;
                    return Results.Json(new
                    {
                        traceId = activity?.TraceId.ToString(),
                        parentSpanId = activity?.ParentSpanId.ToString(),
                        traceState = activity?.TraceStateString,
                        baggage = Baggage.GetBaggage("phase11"),
                        recorded = activity?.Recorded ?? false,
                    });
                });
            }
        }

        internal IServiceProvider Services => _app.Services;
        internal IHostApplicationLifetime Lifetime { get; }
        internal HttpClient Client { get; private set; } = null!;

        internal async Task StartAsync()
        {
            using CancellationTokenSource timeout = new(_deadline);
            await _app.StartAsync(timeout.Token);
            Client = new HttpClient(new HttpClientHandler { UseProxy = false })
            {
                BaseAddress = new Uri(AddressOf(_app)),
                Timeout = _deadline,
            };
        }

        internal async Task RequestAsync(string route)
        {
            using HttpResponseMessage response = await Client.GetAsync(route);
            response.EnsureSuccessStatusCode();
        }

        internal void AssertProviders(string signals)
        {
            Assert.Equal(Enabled(signals, "traces"), Services.GetService<TracerProvider>() is not null);
            Assert.Equal(Enabled(signals, "metrics"), Services.GetService<MeterProvider>() is not null);
            Assert.Equal(Enabled(signals, "logs"), Services.GetServices<ILoggerProvider>().OfType<OpenTelemetryLoggerProvider>().Any());
        }

        internal void Flush()
        {
            if (Services.GetService<TracerProvider>() is TracerProvider traces)
            {
                Assert.True(traces.ForceFlush(10000));
            }
            if (Services.GetService<MeterProvider>() is MeterProvider metrics)
            {
                Assert.True(metrics.ForceFlush(10000));
            }
            if (Services.GetServices<ILoggerProvider>().OfType<OpenTelemetryLoggerProvider>().Any())
            {
                LoggerProvider logs = Services.GetRequiredService<LoggerProvider>();
                Assert.True(logs.ForceFlush(10000));
            }
        }

        internal async Task FlushRequestAsync(Receiver receiver, string signals, string route)
        {
            // Client response completion can precede ASP.NET's Activity.Stop and duration measurement.
            // ForceFlush drains completed records; wait for the real request in every enabled signal.
            Stopwatch elapsed = Stopwatch.StartNew();
            do
            {
                Flush();
                Assert.Empty(receiver.Errors);
                if (new[] { "traces", "metrics", "logs" }.Where(signal => Enabled(signals, signal))
                    .All(signal => receiver.Records(signal).Any(record => IsRequestRecord(record, signal, route))))
                {
                    return;
                }
                await Task.Delay(25);
            }
            while (elapsed.Elapsed < _deadline);
            AssertDelivery(receiver, signals, route);
        }

        internal async Task StopAsync()
        {
            if (_stopped)
            {
                return;
            }
            using CancellationTokenSource timeout = new(_deadline);
            await _app.StopAsync(timeout.Token);
            _stopped = true;
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            Client?.Dispose();
            try
            {
                await StopAsync();
            }
            finally
            {
                await _app.DisposeAsync().AsTask().WaitAsync(_deadline);
            }
        }
    }

    private sealed record ExportRequest(string Signal, string Method, string Protocol, string ContentType, byte[] Body);

    private sealed class Receiver : IAsyncDisposable
    {
        private readonly WebApplication _app;
        private readonly ConcurrentDictionary<string, byte> _rejectedSignals = new(StringComparer.Ordinal);

        internal Receiver(bool grpc, string? rejectRoute = null, bool retryable = false)
        {
            Grpc = grpc;
            _app = Builder(grpc ? HttpProtocols.Http2 : HttpProtocols.Http1).Build();
            _app.Run(async context =>
            {
                try
                {
                    using CancellationTokenSource timeout = new(_deadline);
                    using MemoryStream body = new();
                    await context.Request.Body.CopyToAsync(body, timeout.Token);
                    byte[] bytes = body.ToArray();
                    Assert.InRange(bytes.Length, 1, 1024 * 1024);
                    if (Grpc)
                    {
                        Assert.True(bytes.Length >= 5);
                        Assert.Equal(0, bytes[0]);
                        Assert.Equal(bytes.Length - 5, BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(1, 4)));
                        bytes = bytes[5..];
                    }
                    string path = context.Request.Path.Value!;
                    string signal = new[] { "traces", "metrics", "logs" }.Single(name => path == PathFor(name));
                    ExportRequest request = new(signal, context.Request.Method, context.Request.Protocol, context.Request.ContentType!, bytes);
                    bool reject = rejectRoute is not null &&
                        OpenTelemetryCompatibilityTests.Records(request).Any(record => IsRequestRecord(record, signal, rejectRoute)) &&
                        (!retryable || _rejectedSignals.TryAdd(signal, 0));
                    (reject ? RejectedRequests : Requests).Enqueue(request);
                    context.Response.ContentType = Grpc ? "application/grpc" : "application/x-protobuf";
                    if (Grpc)
                    {
                        context.Response.DeclareTrailer("grpc-status");
                        await context.Response.Body.WriteAsync(new byte[5], timeout.Token);
                        context.Response.AppendTrailer("grpc-status", reject ? (retryable ? "14" : "3") : "0");
                    }
                    else if (reject)
                    {
                        context.Response.StatusCode = retryable ? StatusCodes.Status503ServiceUnavailable : StatusCodes.Status400BadRequest;
                        if (retryable)
                        {
                            context.Response.Headers.RetryAfter = "0";
                        }
                    }
                }
                catch (Exception exception)
                {
                    Errors.Enqueue(exception);
                    context.Abort();
                }
            });
        }

        internal bool Grpc { get; }
        internal string Address => AddressOf(_app);
        internal ConcurrentQueue<ExportRequest> Requests { get; } = new();
        internal ConcurrentQueue<ExportRequest> RejectedRequests { get; } = new();
        internal ConcurrentQueue<Exception> Errors { get; } = new();
        internal string Endpoint(string signal) => Grpc ? Address : Address.TrimEnd('/') + PathFor(signal);

        private string PathFor(string signal) => Grpc
            ? $"/opentelemetry.proto.collector.{(signal == "traces" ? "trace" : signal)}.v1.{(signal == "traces" ? "Trace" : signal == "metrics" ? "Metrics" : "Logs")}Service/Export"
            : "/v1/" + signal;

        internal IEnumerable<byte[]> Records(string signal) => Requests.Where(request => request.Signal == signal)
            .SelectMany(OpenTelemetryCompatibilityTests.Records);

        internal async Task StartAsync()
        {
            using CancellationTokenSource timeout = new(_deadline);
            await _app.StartAsync(timeout.Token);
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                using CancellationTokenSource timeout = new(_deadline);
                await _app.StopAsync(timeout.Token);
            }
            finally
            {
                await _app.DisposeAsync().AsTask().WaitAsync(_deadline);
            }
        }
    }

    private sealed class IsolatedProcessState : IDisposable
    {
        private readonly Dictionary<string, string?> _environment = new(StringComparer.OrdinalIgnoreCase);
        private readonly TextWriter _console = Console.Out;
        private readonly StringWriter _capture = new();
        private readonly TextMapPropagator _propagator;
        private readonly ActivityIdFormat _idFormat;
        private readonly Baggage _baggage = Baggage.Current;

        internal IsolatedProcessState()
        {
            foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
            {
                string key = (string)entry.Key;
                if (key.StartsWith("OTEL_", StringComparison.OrdinalIgnoreCase) ||
                    new[] { "HTTP_PROXY", "HTTPS_PROXY", "ALL_PROXY", "NO_PROXY" }.Contains(key, StringComparer.OrdinalIgnoreCase))
                {
                    _environment[key] = (string?)entry.Value;
                    Environment.SetEnvironmentVariable(key, null);
                }
            }
            // Snapshot initialized SDK defaults, not the API's pre-SDK no-op propagator.
            _ = Sdk.SuppressInstrumentation;
            _propagator = Propagators.DefaultTextMapPropagator;
            _idFormat = Activity.DefaultIdFormat;
            Console.SetOut(TextWriter.Synchronized(_capture));
        }

        internal string ConsoleText
        {
            get
            {
                lock (Console.Out)
                {
                    return _capture.ToString();
                }
            }
        }

        public void Dispose()
        {
            Console.SetOut(_console);
            _capture.Dispose();
            foreach ((string key, string? value) in _environment)
            {
                Environment.SetEnvironmentVariable(key, value);
            }
            Sdk.SetDefaultTextMapPropagator(_propagator);
            Activity.DefaultIdFormat = _idFormat;
            Baggage.Current = _baggage;
        }
    }
}
