// <copyright file="HostingCompatibilityTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using System.Collections.Concurrent;
using System.CommandLine;
using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using fhir.candle.Services;
using fhir.candle.Tests.Controllers;
using fhir.candle.Tests.Extensions;
using FhirCandle.Configuration;
using FhirCandle.Models;
using FhirCandle.Storage;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace fhir.candle.Tests;

/// <summary>Exercises the production singleton registrations and cancellation-driven host lifetime.</summary>
[Collection(CompatibilityHostCollection.Name)]
public sealed class HostingCompatibilityTests
{
    private static readonly TimeSpan _deadline = TimeSpan.FromSeconds(30);
    private readonly ITestOutputHelper _output;

    /// <summary>Creates a hosting check with captured lifecycle diagnostics.</summary>
    /// <param name="output">The current test output.</param>
    public HostingCompatibilityTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>Production hosted registrations forward the same concrete singleton across resolutions and scopes.</summary>
    [Fact]
    public async Task HostedServiceRegistrationForwardsExistingSingleton()
    {
        await using ProductionHost host = new(_output);
        await host.StartAsync();

        IHostedService[] singletons = AssertForwarding(host.Services);
        using IServiceScope scope = host.Services.CreateScope();
        IHostedService[] scoped = AssertForwarding(scope.ServiceProvider);
        for (int index = 0; index < singletons.Length; index++)
        {
            Assert.Same(singletons[index], scoped[index]);
        }

        Assert.Same(singletons[0], FhirStoreManager.Instance);
        Assert.Same(singletons[2], FhirPackageService.Current);
        host.AssertStartedOnce();
    }

    /// <summary>The real RunServer token stops the started services before fixture disposal, without resolving replacements.</summary>
    [Fact]
    public async Task HostedServiceStopsWithoutDuplicateInstance()
    {
        await using ProductionHost host = new(_output);
        await host.StartAsync();
        IHostedService[] before = AssertForwarding(host.Services);
        INotificationManager notifications = host.Services.GetRequiredService<INotificationManager>();
        IFhirStoreManager stores = host.Services.GetRequiredService<IFhirStoreManager>();
        IFhirPackageService packages = host.Services.GetRequiredService<IFhirPackageService>();
        ISmartAuthManager smart = host.Services.GetRequiredService<ISmartAuthManager>();

        Assert.Equal(new[] { "hosting-r4", "hosting-r4b", "hosting-r5" }, stores.Keys.OrderBy(name => name, StringComparer.Ordinal));
        Assert.All(stores.Values, store =>
        {
            Assert.Empty(store["Patient"].Keys);
            Assert.Empty(store.CurrentSubscriptions);
        });
        Assert.True(packages.IsConfigured);
        Assert.True(packages.IsReady);
        Assert.True(smart.IsEnabled);
        Assert.Equal(stores.Keys.OrderBy(name => name, StringComparer.Ordinal),
            smart.SmartConfigurationByTenant.Keys.OrderBy(name => name, StringComparer.Ordinal));
        await host.AssertMetadataAsync();

        Assert.True(host.Lifetime.ApplicationStarted.IsCancellationRequested);
        Assert.False(host.Lifetime.ApplicationStopping.IsCancellationRequested);
        Assert.False(host.Lifetime.ApplicationStopped.IsCancellationRequested);
        await host.StopAsync();
        Assert.True(host.Lifetime.ApplicationStopping.IsCancellationRequested);
        Assert.True(host.Lifetime.ApplicationStopped.IsCancellationRequested);

        IHostedService[] after = AssertForwarding(host.Services);
        for (int index = 0; index < before.Length; index++)
        {
            Assert.Same(before[index], after[index]);
        }
        InvalidOperationException stopped = await Assert.ThrowsAsync<InvalidOperationException>(
            () => notifications.StartAsync(CancellationToken.None));
        Assert.Equal("NotificationManager has already stopped.", stopped.Message);
        await notifications.StopAsync(CancellationToken.None).WaitAsync(_deadline);
        host.AssertStartedOnce();

        string root = host.Root;
        await host.DisposeAsync();
        Assert.False(Directory.Exists(root));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => notifications.StartAsync(CancellationToken.None));
    }

    private static IHostedService[] AssertForwarding(IServiceProvider services)
    {
        IHostedService[] hosted = services.GetServices<IHostedService>()
            .Where(service => service is IFhirStoreManager or INotificationManager or IFhirPackageService or ISmartAuthManager)
            .ToArray();
        Assert.Equal(4, hosted.Length);
        return
        [
            Assert.IsType<FhirStoreManager>(Forwarded<IFhirStoreManager>()),
            Assert.IsType<NotificationManager>(Forwarded<INotificationManager>()),
            Assert.IsType<FhirPackageService>(Forwarded<IFhirPackageService>()),
            Assert.IsType<SmartAuthManager>(Forwarded<ISmartAuthManager>()),
        ];

        TService Forwarded<TService>() where TService : class, IHostedService
        {
            TService singleton = Assert.Single(services.GetServices<TService>());
            Assert.Same(singleton, services.GetRequiredService<TService>());
            Assert.Same(singleton, Assert.Single(hosted.OfType<TService>()));
            return singleton;
        }
    }

    // Observe RunServer's existing host boundary, not the controller-only BuildAppForTesting.
    // Only configuration/logging/key storage are isolated; the four service registrations are untouched.
    private sealed class ProductionHost : IAsyncDisposable, IObserver<DiagnosticListener>, IObserver<KeyValuePair<string, object?>>
    {
        private static readonly AsyncLocal<ProductionHost?> _current = new();
        private readonly LocalPackageFixture _files = new();
        private readonly CancellationTokenSource _shutdown = new();
        private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly Dictionary<string, string?> _environment = new(StringComparer.Ordinal);
        private readonly List<IDisposable> _subscriptions = [];
        private readonly HostLog _log = new();
        private readonly RSA _key = RSA.Create(2048);
        private readonly X509Certificate2 _certificate;
        private readonly ITestOutputHelper _output;
        private IHost? _host;
        private Task? _run;
        private bool _disposed;

        internal ProductionHost(ITestOutputHelper output)
        {
            _output = output;
            CertificateRequest request = new("CN=fhir-candle-hosting-tests", _key,
                HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            _certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1));
        }

        internal string Root => _files.Root;
        internal IServiceProvider Services => (_host ?? throw new InvalidOperationException("Host was not built.")).Services;
        internal IHostApplicationLifetime Lifetime => Services.GetRequiredService<IHostApplicationLifetime>();

        internal async Task StartAsync()
        {
            // Default-builder files must be absent, and Development user secrets must not be consulted.
            foreach (string prefix in new[] { "DOTNET_", "ASPNETCORE_" })
            {
                SetEnvironment(prefix + "APPLICATIONNAME", typeof(Program).Assembly.GetName().Name!);
                SetEnvironment(prefix + "CONTENTROOT", Root);
                SetEnvironment(prefix + "ENVIRONMENT", Environments.Production);
            }
            _subscriptions.Add(DiagnosticListener.AllListeners.Subscribe(this));
            CliRootCommand command = new();
            ParseResult parsed = command.Parse(
            [
                "--r4", "hosting-r4", "--r4b", "hosting-r4b", "--r5", "hosting-r5",
                "--port", "0", "--url", "http://127.0.0.1", "--open-browser", "false",
                "--fhir-source", _files.SourceDirectory, "--fhir-package-cache", _files.CacheDirectory,
                "--use-official-registries", "false",
            ], new ParserConfiguration { ResponseFileTokenReplacer = null });
            Assert.Empty(parsed.Errors);
            IConfiguration external = new ConfigurationBuilder().Build();
            _run = Task.Run(async () =>
            {
                _current.Value = this;
                try
                {
                    await Program.RunServer(parsed, external, _shutdown.Token);
                }
                finally
                {
                    _current.Value = null;
                }
            });

            Task completed = await Task.WhenAny(_started.Task, _run).WaitAsync(_deadline);
            Assert.Same(_started.Task, completed);
            await _started.Task;
            Assert.False(_run.IsCompleted, "RunServer must wait for shutdown after ApplicationStarted.");
            Assert.Equal(Environments.Production, Services.GetRequiredService<IHostEnvironment>().EnvironmentName);
            _output.WriteLine($"Production RunServer started; empty source/cache: {Root}");
            _output.WriteLine($"Hosting abstractions loaded: {typeof(IHostedService).Assembly.Location}; " +
                $"file version {FileVersionInfo.GetVersionInfo(typeof(IHostedService).Assembly.Location).FileVersion}");
        }

        internal async Task AssertMetadataAsync()
        {
            IServer server = Services.GetRequiredService<IServer>();
            IServerAddressesFeature addresses = server.Features.Get<IServerAddressesFeature>()
                ?? throw new InvalidOperationException("Kestrel did not publish its address.");
            Uri address = new(Assert.Single(addresses.Addresses));
            Assert.Equal(IPAddress.Loopback.ToString(), address.Host);
            using HttpClient client = new(new HttpClientHandler { UseProxy = false })
            {
                BaseAddress = address,
                Timeout = _deadline,
            };
            client.DefaultRequestHeaders.Accept.ParseAdd("application/fhir+json");
            foreach ((string tenant, string version) in new[]
            {
                ("hosting-r4", "4.0.1"), ("hosting-r4b", "4.3.0"), ("hosting-r5", "5.0.0"),
            })
            {
                using HttpResponseMessage response = await client.GetAsync("/fhir/" + tenant + "/metadata");
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                using JsonDocument metadata = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                Assert.Equal("CapabilityStatement", metadata.RootElement.GetProperty("resourceType").GetString());
                Assert.Equal(version, metadata.RootElement.GetProperty("fhirVersion").GetString());
                Assert.Equal($"http://127.0.0.1/fhir/{tenant}/CapabilityStatement/metadata",
                    metadata.RootElement.GetProperty("url").GetString());
                _output.WriteLine($"{tenant}/metadata: 200; FHIR {version}");
            }
        }

        internal async Task StopAsync()
        {
            _shutdown.Cancel();
            if (_run is not null)
            {
                await _run.WaitAsync(_deadline);
            }
            Assert.True(Lifetime.ApplicationStopped.IsCancellationRequested,
                "RunServer returned before the production host completed graceful shutdown.");
            _output.WriteLine("RunServer cancellation completed ApplicationStopping and ApplicationStopped.");
        }

        internal void AssertStartedOnce()
        {
            foreach (string service in new[] { "FhirStoreManager", "NotificationManager", "FhirPackageService", "SmartAuthManager" })
            {
                Assert.Single(_log.Entries, entry => entry.Message == $"Starting {service}...");
            }
            Assert.DoesNotContain(_log.Entries, entry => entry.Level >= LogLevel.Warning);
            Assert.Empty(Directory.EnumerateFileSystemEntries(_files.SourceDirectory));
            Assert.Empty(Directory.EnumerateFileSystemEntries(_files.CacheDirectory));
        }

        private void SetEnvironment(string name, string value)
        {
            _environment.Add(name, Environment.GetEnvironmentVariable(name));
            Environment.SetEnvironmentVariable(name, value);
        }

        /// <inheritdoc />
        public void OnNext(DiagnosticListener listener)
        {
            if (listener.Name == "Microsoft.Extensions.Hosting" && ReferenceEquals(_current.Value, this))
            {
                _subscriptions.Add(listener.Subscribe(this));
            }
        }

        /// <inheritdoc />
        public void OnNext(KeyValuePair<string, object?> diagnostic)
        {
            if (diagnostic.Key == "HostBuilding" && diagnostic.Value is IHostBuilder builder)
            {
                builder.ConfigureAppConfiguration((context, configuration) =>
                {
                    configuration.Sources.Clear();
                    configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        [HostDefaults.ApplicationKey] = context.HostingEnvironment.ApplicationName,
                        [HostDefaults.EnvironmentKey] = context.HostingEnvironment.EnvironmentName,
                        [HostDefaults.ContentRootKey] = context.HostingEnvironment.ContentRootPath,
                        [WebHostDefaults.ServerUrlsKey] = "http://127.0.0.1:0",
                    });
                });
                builder.ConfigureServices((_, services) =>
                {
                    // CandleConfig does not currently copy the SMART CLI options. Supply synthetic
                    // tenant settings here to exercise SMART initialization, not claim CLI coverage.
                    Dictionary<string, TenantConfiguration> tenants = Assert.IsType<Dictionary<string, TenantConfiguration>>(
                        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(Dictionary<string, TenantConfiguration>))
                            .ImplementationInstance);
                    foreach (TenantConfiguration tenant in tenants.Values)
                    {
                        tenant.SmartAllowed = true;
                    }

                    // DataProtection has its own key-ring warmup hosted service; an ephemeral provider alone
                    // would not prevent it reading the production key directory. Isolate that repository too.
                    services.AddDataProtection()
                        .PersistKeysToFileSystem(new DirectoryInfo(_files.CreateDirectory("keys")))
                        .ProtectKeysWithCertificate(_certificate);
                    services.AddLogging(logging => logging.ClearProviders().AddProvider(_log).SetMinimumLevel(LogLevel.Information));
                    services.Configure<HostOptions>(options => options.ShutdownTimeout = _deadline);
                });
            }
            else if (diagnostic.Key == "HostBuilt" && diagnostic.Value is IHost host)
            {
                _host = host;
                _subscriptions.Add(Lifetime.ApplicationStarted.Register(() => _started.TrySetResult()));
            }
        }

        /// <inheritdoc />
        public void OnCompleted()
        {
        }

        /// <inheritdoc />
        public void OnError(Exception error) => _started.TrySetException(error);

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            try
            {
                _shutdown.Cancel();
                if (_run is not null)
                {
                    await _run.WaitAsync(_deadline);
                }
            }
            finally
            {
                try
                {
                    if (_host is not null)
                    {
                        IFhirStore[] stores = Services.GetRequiredService<IFhirStoreManager>().Values.ToArray();
                        try
                        {
                            if (!Lifetime.ApplicationStopped.IsCancellationRequested)
                            {
                                using CancellationTokenSource cleanup = new(_deadline);
                                await _host.StopAsync(cleanup.Token).WaitAsync(_deadline);
                            }
                        }
                        finally
                        {
                            try
                            {
                                await ((IAsyncDisposable)_host).DisposeAsync().AsTask().WaitAsync(_deadline);
                            }
                            finally
                            {
                                foreach (IFhirStore store in stores)
                                {
                                    store.Dispose();
                                }
                            }
                        }
                    }
                }
                finally
                {
                    foreach (IDisposable subscription in _subscriptions)
                    {
                        subscription.Dispose();
                    }
                    foreach ((string name, string? value) in _environment)
                    {
                        Environment.SetEnvironmentVariable(name, value);
                    }
                    _shutdown.Dispose();
                    _certificate.Dispose();
                    _key.Dispose();
                    _files.Dispose();
                    _output.WriteLine("Owned host/stores/listeners disposed; environment restored; TEMP root removed.");
                }
            }
        }
    }

    private sealed class HostLog : ILoggerProvider
    {
        private readonly ConcurrentQueue<(LogLevel Level, string Message)> _entries = new();

        internal IEnumerable<(LogLevel Level, string Message)> Entries => _entries.ToArray();

        /// <inheritdoc />
        public ILogger CreateLogger(string categoryName) => new Logger(_entries);

        /// <inheritdoc />
        public void Dispose()
        {
        }

        private sealed class Logger(ConcurrentQueue<(LogLevel Level, string Message)> entries) : ILogger
        {
            /// <inheritdoc />
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            /// <inheritdoc />
            public bool IsEnabled(LogLevel logLevel) => true;

            /// <inheritdoc />
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter) => entries.Enqueue((logLevel, formatter(state, exception)));
        }
    }
}
