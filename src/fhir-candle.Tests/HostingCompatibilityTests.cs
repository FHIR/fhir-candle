// <copyright file="HostingCompatibilityTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using System.Collections.Concurrent;
using System.CommandLine;
using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using fhir.candle.Services;
using fhir.candle.Tests.Controllers;
using fhir.candle.Tests.Extensions;
using fhir.candle.Ui;
using FhirCandle.Configuration;
using FhirCandle.Models;
using FhirCandle.Storage;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.FluentUI.AspNetCore.Components;
using Xunit.Abstractions;

namespace fhir.candle.Tests;

/// <summary>Exercises production services, UI delivery, and cancellation-driven host lifetime.</summary>
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

    /// <summary>The real UI host prerenders all tenants and serves its selected local asset graph.</summary>
    [Fact]
    public async Task ProductionUiServesRoutesAndRequiredAssets()
    {
        await using ProductionHost host = new(_output);
        await host.StartAsync();
        await using (AsyncServiceScope first = host.Services.CreateAsyncScope())
        await using (AsyncServiceScope second = host.Services.CreateAsyncScope())
        {
            CandleThemeState state = first.ServiceProvider.GetRequiredService<CandleThemeState>();
            CandleThemeState other = second.ServiceProvider.GetRequiredService<CandleThemeState>();
            Assert.Same(state, first.ServiceProvider.GetRequiredService<CandleThemeState>());
            Assert.NotSame(state, other);
            state.Toggle();
            Assert.Equal(1, state.Revision);
            Assert.Equal(0, other.Revision);
            Assert.False(other.IsDarkMode);
            foreach (IServiceProvider scope in new[] { first.ServiceProvider, second.ServiceProvider })
            {
                AssertVendorService<IDialogService>(scope);
                AssertVendorService<INotificationService>(scope);
                AssertVendorService<IThemeService>(scope);
            }
        }

        new RouteComposition().AssertAssemblies();
        AssertForwarding(host.Services);
        await host.AssertUiAsync();
        host.AssertStartedOnce();
        await host.StopAsync();
        string root = host.Root;
        await host.DisposeAsync();
        Assert.False(Directory.Exists(root));

        static void AssertVendorService<TService>(IServiceProvider scope) where TService : class
        {
            TService service = scope.GetRequiredService<TService>();
            Assert.Equal(typeof(FluentProviders).Assembly, service.GetType().Assembly);
        }
    }

    // Inspect compiled Router parameters, not source text or inferred version coverage from /store.
    // Framework render-tree access is confined to this unmounted composition probe.
#pragma warning disable BL0006
    private sealed class RouteComposition : fhir.candle.Components.Routes
    {
        internal void AssertAssemblies()
        {
            using RenderTreeBuilder root = new();
            BuildRenderTree(root);
            RenderTreeFrame[] frames = Frames(root);
            Assert.Equal(typeof(CandleUiRoot),
                Assert.Single(frames, frame => frame.FrameType == RenderTreeFrameType.Component).ComponentType);
            RenderFragment content = Assert.IsType<RenderFragment>(Attribute(frames, nameof(CandleUiRoot.ChildContent)));
            using RenderTreeBuilder routed = new();
            content(routed);
            frames = Frames(routed);
            Assert.Equal(typeof(Router),
                Assert.Single(frames, frame => frame.FrameType == RenderTreeFrameType.Component).ComponentType);
            Assert.Same(typeof(Program).Assembly, Attribute(frames, nameof(Router.AppAssembly)));
            Assembly[] expected =
            [
                typeof(FhirCandle.Ui.R4.Subscriptions.TourUtils).Assembly,
                typeof(FhirCandle.Ui.R4B.Subscriptions.TourUtils).Assembly,
                typeof(FhirCandle.Ui.R5.Subscriptions.TourUtils).Assembly,
            ];
            Assembly[] additional = Assert.IsAssignableFrom<IEnumerable<Assembly>>(
                Attribute(frames, nameof(Router.AdditionalAssemblies))).ToArray();
            Assert.Equal(expected, additional);
            Assert.All(additional, assembly =>
                Assert.Contains(assembly.ExportedTypes, type => typeof(IComponent).IsAssignableFrom(type)));
            Assert.IsType<RenderFragment<RouteData>>(Attribute(frames, nameof(Router.Found)));
            Assert.IsType<RenderFragment>(Attribute(frames, "NotFound"));
        }

        private static RenderTreeFrame[] Frames(RenderTreeBuilder builder) =>
            builder.GetFrames().Array.Take(builder.GetFrames().Count).ToArray();

        private static object? Attribute(RenderTreeFrame[] frames, string name) =>
            Assert.Single(frames, frame => frame.FrameType == RenderTreeFrameType.Attribute &&
                frame.AttributeName == name).AttributeValue;
    }
#pragma warning restore BL0006

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
            using HttpClient client = CreateClient();
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

        internal async Task AssertUiAsync()
        {
            const string FluentContent = "/_content/Microsoft.FluentUI.AspNetCore.Components/";
            const string FluentBundle = FluentContent + "Microsoft.FluentUI.AspNetCore.Components.bundle.scp.css";
            const string FluentModule = FluentContent + "Microsoft.FluentUI.AspNetCore.Components.lib.module.js";
            const string MonacoContent = "/_content/BlazorMonaco/";
            using CancellationTokenSource requests = CancellationTokenSource.CreateLinkedTokenSource(_shutdown.Token);
            requests.CancelAfter(_deadline);
            CancellationToken token = requests.Token;
            using HttpClient client = CreateClient();
            IWebHostEnvironment environment = Services.GetRequiredService<IWebHostEnvironment>();
            Assert.Equal(typeof(Program).Assembly.GetName().Name, environment.ApplicationName);
            _output.WriteLine($"RunServer web root: {environment.WebRootPath}; provider: " +
                environment.WebRootFileProvider.GetType().FullName);
            Dictionary<string, string> bodies = new(StringComparer.Ordinal);
            Queue<(Uri Address, int Depth)> pending = new();
            HashSet<Uri> scheduled = [];
            Uri documentBase = new(client.BaseAddress!, "/");
            string[] styles = ["/fhir-candle.styles.css", "/css/candle-theme.css", "/css/app.css", "/css/monaco.css"];
            string[] scripts =
            [
                "/js/candle-theme.js", MonacoContent + "jsInterop.js",
                MonacoContent + "lib/monaco-editor/min/vs/loader.js",
                MonacoContent + "lib/monaco-editor/min/vs/editor/editor.main.js", "/_framework/blazor.web.js",
            ];
            foreach (string route in new[]
            {
                "/", "/store?store=hosting-r4", "/store?store=hosting-r4b", "/store?store=hosting-r5",
            })
            {
                using HttpResponseMessage response = await client.GetAsync(route, token);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
                string document = await response.Content.ReadAsStringAsync(token);
                AssertUiDocument(document);
                Assert.Equal("/", HtmlAttribute(Assert.Single(Matches(document, @"<base\b[^>]*>")).Value, "href"));
                string[] deliveredStyles = Matches(document, @"<link\b[^>]*>")
                    .Where(tag => HtmlAttribute(tag.Value, "rel") == "stylesheet")
                    .Select(tag => HtmlAttribute(tag.Value, "href")).ToArray();
                string[] deliveredScripts = Matches(document, @"<script\b[^>]*>")
                    .Select(tag => HtmlAttribute(tag.Value, "src")).Where(src => !string.IsNullOrEmpty(src)).ToArray();
                Assert.Equal(styles, deliveredStyles.Select(path => new Uri(documentBase, path).AbsolutePath));
                Assert.Equal(scripts, deliveredScripts.Select(path => new Uri(documentBase, path).AbsolutePath));
                foreach (string reference in deliveredStyles.Concat(deliveredScripts))
                {
                    Enqueue(documentBase, reference, 0);
                }

                string application = SingleDiv(document, "candle-ui-root");
                if (route == "/")
                {
                    Assert.Contains("Uploading real patient data is strictly prohibited.", application);
                    foreach ((string tenant, string release) in new[]
                    {
                        ("hosting-r4", "R4"), ("hosting-r4b", "R4B"), ("hosting-r5", "R5"),
                    })
                    {
                        Assert.Contains($"FHIR {release} endpoint at", application);
                        Assert.Contains($"http://127.0.0.1/fhir/{tenant}", application);
                        Assert.Contains($"/store?store={tenant}", application);
                    }
                }
                else
                {
                    string tenant = route.Split('=')[1];
                    Assert.Contains("FHIR Resources: " + tenant, application);
                    Assert.Contains("Hide Empty Resources", application);
                    Assert.Contains($"href=\"/store?store={tenant}\"", application);
                }
                _output.WriteLine($"{route}: 200; prerendered application and provider outlets.");
            }

            string manifestPath = "/" + environment.ApplicationName + ".modules.json";
            Enqueue(documentBase, manifestPath, 0);
            // FluentUIStyles fetches this dynamically; neither CSS @imports nor the manifest lists it.
            Enqueue(documentBase, FluentContent + "css/default-fuib.css", 0);
            // These active grid/drawer/tooltip controls import modules only after interactivity.
            // Read their released assembly references instead of guessing URLs from component names.
            Dictionary<string, string> componentModules = new(StringComparer.Ordinal);
            foreach ((Type component, string identity) in new (Type, string)[]
            {
                (typeof(FluentDataGrid<>), "DataGrid.Initialize"),
                (typeof(FluentGrid), "Grid.FluentGridInitialize"),
                (typeof(FluentNav), "Nav.Initialize"),
                (typeof(FluentTooltip), "Tooltip.FluentTooltipInitialize"),
            })
            {
                FieldInfo reference = Assert.IsAssignableFrom<FieldInfo>(
                    component.GetField("JAVASCRIPT_FILE", BindingFlags.NonPublic | BindingFlags.Static));
                string path = Assert.IsType<string>(reference.GetRawConstantValue());
                Assert.Contains(FluentContent + "Components/", path);
                componentModules.Add(new Uri(documentBase, path).AbsolutePath, identity);
                Enqueue(documentBase, path, 0);
            }

            while (pending.TryDequeue(out (Uri Address, int Depth) asset))
            {
                token.ThrowIfCancellationRequested();
                string path = asset.Address.AbsolutePath;
                string extension = Path.GetExtension(path);
                Assert.True(styles.Contains(path) || scripts.Contains(path) || path == manifestPath ||
                    (path.StartsWith(FluentContent, StringComparison.Ordinal) && extension is ".css" or ".js"),
                    $"Asset traversal escaped the required local graph: {asset.Address}");
                using HttpResponseMessage response = await client.GetAsync(asset.Address, token);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                string? mediaType = response.Content.Headers.ContentType?.MediaType;
                Assert.True(extension switch
                {
                    ".css" => mediaType == "text/css",
                    ".js" => mediaType is "text/javascript" or "application/javascript",
                    ".json" => mediaType == "application/json",
                    _ => false,
                }, $"{path}: unexpected content type '{mediaType}'.");
                byte[] bytes = await response.Content.ReadAsByteArrayAsync(token);
                string body = Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF', ' ', '\t', '\r', '\n');
                Assert.False(string.IsNullOrWhiteSpace(body), path);
                Assert.False(Matches(body, @"^(?:<!doctype\s+html\b|<html\b)").Any(), $"{path}: HTML fallback.");
                IFileInfo selected = environment.WebRootFileProvider.GetFileInfo(Uri.UnescapeDataString(path).TrimStart('/'));
                Assert.True(selected.Exists && !selected.IsDirectory, $"{path}: absent from RunServer's file provider.");
                await using (Stream stream = selected.CreateReadStream())
                {
                    Assert.Equal(await SHA256.HashDataAsync(stream, token), SHA256.HashData(bytes));
                }
                bodies.Add(path, body);
                _output.WriteLine($"{path}: 200; {mediaType}; {bytes.Length} bytes; selected-file SHA-256 matches.");

                if (extension == ".css")
                {
                    string css = Regex.Replace(body, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline, _deadline);
                    foreach (Match import in Matches(css, @"@import\s+(?:url\(\s*)?[""']?(?<url>[^""'\s);]+)"))
                    {
                        Enqueue(asset.Address, import.Groups["url"].Value, asset.Depth + 1);
                    }
                }
                else if (path == manifestPath)
                {
                    using JsonDocument manifest = JsonDocument.Parse(body);
                    Assert.Equal(JsonValueKind.Array, manifest.RootElement.ValueKind);
                    string[] modules = manifest.RootElement.EnumerateArray()
                        .Select(module => Assert.IsType<string>(module.GetString())).ToArray();
                    Assert.Single(modules, module => new Uri(documentBase, module).AbsolutePath == FluentModule);
                    foreach (string module in modules)
                    {
                        // The initializer manifest is document-base-relative, not relative to /_framework.
                        Enqueue(documentBase, module, asset.Depth + 1);
                    }
                }
                else if (path.StartsWith(FluentContent, StringComparison.Ordinal) && extension == ".js")
                {
                    foreach (Match import in Matches(body,
                        @"\b(?:import|export)\s+(?:[^;""']*?\s+from\s*)?[""'](?<url>[^""']+)[""']|\bimport\s*\(\s*[""'](?<url>[^""']+)[""']"))
                    {
                        Enqueue(asset.Address, import.Groups["url"].Value, asset.Depth + 1);
                    }
                }
            }

            Dictionary<string, string[]> identities = new(StringComparer.Ordinal)
            {
                ["/js/candle-theme.js"] = ["window.candleTheme", "localStorage.getItem(\"theme\")", "SystemChanged"],
                ["/css/candle-theme.css"] = [".candle-ui-root", ".candle-messages-top", "--colorNeutralBackground1"],
                ["/css/app.css"] = ["--candle-foreground", ".candle-header"],
                ["/css/monaco.css"] = [".monaco-editor-container"],
                ["/fhir-candle.styles.css"] = ["@import", FluentBundle.TrimStart('/')],
                [FluentBundle] = [".fluent-data-grid", ".fluent-message-bar-provider"],
                [FluentModule] = ["beforeWebStart", "afterWebStarted", "setBrandThemeToElementFromSettings"],
                [FluentContent + "css/default-fuib.css"] = ["Microsoft Fluent UI Blazor library - Required styles"],
                ["/_framework/blazor.web.js"] = ["Blazor", "beforeWebStart"],
                [MonacoContent + "jsInterop.js"] = ["window.blazorMonaco"],
                [MonacoContent + "lib/monaco-editor/min/vs/loader.js"] = ["AMDLoader"],
                [MonacoContent + "lib/monaco-editor/min/vs/editor/editor.main.js"] = ["vs/editor/editor.main"],
            };
            foreach ((string path, string[] markers) in identities)
            {
                Assert.True(bodies.ContainsKey(path), $"Required asset not reached: {path}");
                string body = bodies[path];
                Assert.All(markers, marker => Assert.Contains(marker, body));
            }
            Assert.Single(bodies.Where(asset => asset.Key.EndsWith(".css", StringComparison.Ordinal))
                .SelectMany(asset => Matches(asset.Value, @"@import\s+[^;]+;")),
                import => import.Value.Contains(FluentBundle.TrimStart('/'), StringComparison.Ordinal));
            Assert.DoesNotContain("reboot.css", bodies["/css/app.css"]);
            Assert.Contains("default-fuib.css", bodies[FluentModule]);
            foreach ((string path, string identity) in componentModules)
            {
                Assert.True(bodies.ContainsKey(path), $"Required component module not reached: {path}");
                Assert.Contains(identity, bodies[path]);
            }
            Assert.All(bodies.Where(asset => asset.Key.EndsWith(".razor.js", StringComparison.Ordinal)),
                asset => Assert.Contains("export var Microsoft;", asset.Value));

            void Enqueue(Uri parent, string reference, int depth)
            {
                Uri resolved = new(parent, WebUtility.HtmlDecode(reference));
                if (resolved.Scheme != documentBase.Scheme || resolved.Authority != documentBase.Authority)
                {
                    return; // No external styles, CDNs, source maps, or unrelated endpoints are crawled.
                }
                Uri canonical = new UriBuilder(resolved) { Fragment = string.Empty, Query = string.Empty }.Uri;
                if (!scheduled.Add(canonical))
                {
                    return;
                }
                Assert.InRange(depth, 0, 6);
                Assert.InRange(scheduled.Count, 1, 32);
                pending.Enqueue((canonical, depth));
            }
        }

        private static void AssertUiDocument(string document)
        {
            Assert.Contains("<!DOCTYPE html>", document, StringComparison.OrdinalIgnoreCase);
            Assert.False(Matches(document, @"loading-theme\.js|<loading-theme\b|<fluent-design-theme\b").Any());
            string application = SingleDiv(document, "candle-ui-root");
            string providers = SingleDiv(application, "fluent-providers");
            SingleDiv(document, "fluent-providers");
            foreach (string outlet in new[] { "fluent-dialog-provider", "fluent-toast-provider", "fluent-tooltip-provider" })
            {
                SingleDiv(providers, outlet);
                SingleDiv(document, outlet);
            }
            string feedback = SingleDiv(application, "candle-messages-top");
            SingleDiv(document, "candle-messages-top");
            Assert.Contains("Application feedback", feedback);
            SingleDiv(feedback, "fluent-message-bar-provider");
            SingleDiv(document, "fluent-message-bar-provider");
            Assert.True(application.IndexOf(feedback, StringComparison.Ordinal) <
                application.IndexOf("candle-header", StringComparison.Ordinal));
            Assert.Contains("Light/Dark Mode", application);
            Assert.Contains("blazor-error-ui", application);
            Assert.Contains("<svg", application);

            // HeadOutlet and Routes retain separate prerendered InteractiveServer boundaries.
            Match[] boundaries = Matches(document, @"<!--Blazor:(?<json>.*?)-->").ToArray();
            JsonElement[] markers = boundaries.Select(marker =>
            {
                using JsonDocument json = JsonDocument.Parse(marker.Groups["json"].Value);
                return json.RootElement.Clone();
            }).ToArray();
            int[] starts = Enumerable.Range(0, markers.Length)
                .Where(index => markers[index].TryGetProperty("type", out _)).ToArray();
            Assert.Equal(2, starts.Length);
            int headEnd = document.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);
            int applicationStart = document.IndexOf(application, StringComparison.Ordinal);
            Assert.InRange(boundaries[starts[0]].Index, 0, headEnd);
            Assert.InRange(boundaries[starts[1]].Index, headEnd, applicationStart);
            Assert.All(starts, index =>
            {
                JsonElement marker = markers[index];
                Assert.Equal("server", marker.GetProperty("type").GetString());
                string id = Assert.IsType<string>(marker.GetProperty("prerenderId").GetString());
                Assert.False(string.IsNullOrEmpty(id));
                int end = Assert.Single(Enumerable.Range(0, markers.Length), candidate =>
                    !markers[candidate].TryGetProperty("type", out _) &&
                    markers[candidate].GetProperty("prerenderId").GetString() == id);
                Assert.True(boundaries[index].Index < boundaries[end].Index);
                if (index == starts[1])
                {
                    Assert.True(boundaries[end].Index >= applicationStart + application.Length);
                }
            });
        }

        private static string SingleDiv(string document, string cssClass)
        {
            Match[] tags = Matches(document, @"</?div\b[^>]*>").ToArray();
            Match start = Assert.Single(tags, tag => !tag.Value.StartsWith("</", StringComparison.Ordinal) &&
                HtmlAttribute(tag.Value, "class").Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains(cssClass));
            int depth = 0;
            foreach (Match tag in tags.Where(tag => tag.Index >= start.Index))
            {
                depth += tag.Value.StartsWith("</", StringComparison.Ordinal) ? -1 : 1;
                if (depth == 0)
                {
                    return document.Substring(start.Index, tag.Index + tag.Length - start.Index);
                }
            }
            throw new InvalidOperationException($"Unclosed {cssClass} div in prerendered document.");
        }

        private static IEnumerable<Match> Matches(string input, string pattern) =>
            Regex.Matches(input, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, _deadline).Cast<Match>();

        private static string HtmlAttribute(string tag, string name) =>
            WebUtility.HtmlDecode(Regex.Match(tag, $@"\b{name}\s*=\s*(?:""(?<value>[^""]*)""|'(?<value>[^']*)')",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, _deadline).Groups["value"].Value);

        private HttpClient CreateClient()
        {
            IServer server = Services.GetRequiredService<IServer>();
            IServerAddressesFeature addresses = server.Features.Get<IServerAddressesFeature>()
                ?? throw new InvalidOperationException("Kestrel did not publish its address.");
            Uri address = new(Assert.Single(addresses.Addresses));
            Assert.Equal(IPAddress.Loopback.ToString(), address.Host);
            return new HttpClient(new HttpClientHandler { UseProxy = false, AllowAutoRedirect = false })
            {
                BaseAddress = address,
                Timeout = _deadline,
                MaxResponseContentBufferSize = 16 * 1024 * 1024,
            };
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
