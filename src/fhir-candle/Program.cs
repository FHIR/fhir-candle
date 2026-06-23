// <copyright file="Program.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using System.CommandLine;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using fhir.candle.McpTools;
using fhir.candle.Services;
using FhirCandle.Configuration;
using FhirCandle.Models;
using FhirCandle.Strict;
using FhirCandle.Utils;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting.StaticWebAssets;
using Microsoft.Extensions.Configuration;
using Microsoft.FluentUI.AspNetCore.Components;
using Microsoft.JSInterop;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using SCL = System.CommandLine; // this is present to disambiguate Option from System.CommandLine and Microsoft.FluentUI.AspNetCore.Components


namespace fhir.candle;

/// <summary>A program.</summary>
public static partial class Program
{
    [GeneratedRegex("(http[s]*:\\/\\/.*(:\\d+)*)")]
    private static partial Regex InputUrlFormatRegex();

    private static int _retVal = 0;

    /// <summary>Main entry-point for this application.</summary>
    /// <param name="args">An array of command-line argument strings.</param>
    public static async Task<int> Main(string[] args)
    {
        // set up our configuration (command line > environment > appsettings.json)
        IConfiguration extConfig = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables("Candle")
            .AddEnvironmentVariables()
            .Build();

        RootCommand root = CliOptions.RootCommand;
        root.SetAction(async (ParseResult pr) => await RunServer(pr, extConfig));

        ParseResult pr = root.Parse(args, new ParserConfiguration()
        {
            ResponseFileTokenReplacer = null,
        });

        await pr.InvokeAsync();

        return _retVal;


        //// Create commands from CliOptions.Commands so the list is defined in one place
        ////foreach ((string name, Command cmd) in CliOptions.Commands)
        ////{
        ////    // set the handlers for each command
        ////    switch (name)
        ////    {
        ////        case CliLoadXmlCommand.CommandName:
        ////            cmd.SetAction((ParseResult pr) => loadCommandHandler(pr, configuration));
        ////            break;
        ////    }

        ////    root.Add(cmd);
        ////}


        ////Dictionary<string, SCL.Option> options = BuildCliOptions(typeof(CandleConfig), envConfig: envConfig);
        //ConfigurationOption[] configurationOptions = (new CandleConfig()).GetOptions();
        //List<string> envArgs = [];

        //// build our root command
        //SCL.RootCommand rootCommand = new("A lightweight in-memory FHIR server, for when a small FHIR will do.");
        //SCL.RootCommand envCommand = new("A lightweight in-memory FHIR server, for when a small FHIR will do.");
        //foreach (ConfigurationOption co in configurationOptions)
        //{
        //    // note that 'global' here is just recursive DOWNWARD
        //    rootCommand.AddGlobalOption(co.CliOption);
        //    envCommand.AddGlobalOption(co.CliOption);

        //    if (string.IsNullOrEmpty(co.EnvVarName))
        //    {
        //        continue;
        //    }

        //    IConfigurationSection section = envConfig.GetSection(co.EnvVarName);

        //    if (!section.Exists())
        //    {
        //        continue;
        //    }

        //    string? value = (section.Value ?? null) ?? string.Join(',', section.GetChildren().Select(c => c.Value));

        //    if (string.IsNullOrEmpty(value))
        //    {
        //        continue;
        //    }

        //    if (value.Contains(' '))
        //    {
        //        value = $"\"{value}\"";
        //    }

        //    if (co.CliOption.Aliases.Any())
        //    {
        //        envArgs.Add(co.CliOption.Aliases.First());
        //        envArgs.Add(value);
        //        continue;
        //    }

        //    if (!string.IsNullOrEmpty(co.CliOption.Name))
        //    {
        //        envArgs.Add(co.CliOption.Name);
        //        envArgs.Add(value);
        //        continue;
        //    }
        //}

        //// build a parser for our environment arguments
        //SCL.Parsing.Parser envParser = new CommandLineBuilder(envCommand).UseDefaults().Build();

        //// attempt a parse
        //SCL.Parsing.ParseResult envParseResult = envParser.Parse(string.Join(' ', envArgs));

        //// set our command handler
        //rootCommand.SetHandler(async (context) => await RunServer(context.ParseResult, envParseResult, context.GetCancellationToken()));

        //// run whatever the caller requested
        //return await rootCommand.InvokeAsync(args);
    }

    /// <summary>Executes the server operation.</summary>
    /// <param name="pr">The parsed configuration data from launch.</param>
    /// <param name="envPR">The parsed configuration data from environment variables</param>
    /// <param name="cancellationToken">A token that allows processing to be cancelled.</param>
    /// <returns>An asynchronous result that yields an int.</returns>
    public static async Task RunServer(
        SCL.ParseResult pr,
        IConfiguration extConfig,
        CancellationToken? cancellationToken = null)
    {
        try
        {
            if (pr.RootCommandResult.Command is not CliRootCommand crc)
            {
                throw new InvalidOperationException("Root command is not a CliRootCommand");
            }

            CandleConfig config = new(crc.CommandCliOptions, pr, extConfig);

            if (string.IsNullOrEmpty(config.PublicUrl))
            {
                config.PublicUrl = $"http://localhost:{config.ListenPort}";
            }

            // update configuration to make sure listen url is properly formatted
            Match match = InputUrlFormatRegex().Match(config.PublicUrl);
            config.PublicUrl = match.ToString();

            if (config.PublicUrl.EndsWith('/'))
            {
                config.PublicUrl = config.PublicUrl.Substring(0, config.PublicUrl.Length - 1);
            }

            if (config.FhirPathLabUrl?.EndsWith('/') ?? false)
            {
                config.FhirPathLabUrl = config.FhirPathLabUrl.Substring(0, config.FhirPathLabUrl.Length - 1);
            }

            // check for no tenants (create defaults)
            if ((!config.TenantsR4.Any()) &&
                (!config.TenantsR4B.Any()) &&
                (!config.TenantsR5.Any()))
            {
                config.TenantsR4 = ["r4"];
                config.TenantsR4B = ["r4b"];
                config.TenantsR5 = ["r5"];
            }

            Dictionary<string, TenantConfiguration> tenants = BuildTenantConfigurations(config);

            WebApplicationBuilder? builder = null;

            // when packaging as a dotnet tool, we need to do some directory shenanigans for the static content root
            string root = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location ?? AppContext.BaseDirectory) ?? string.Empty;
            if (!string.IsNullOrEmpty(root))
            {
                string staticWebRoot = FindRelativeDir(root, "staticwebassets", false);
                string wwwRoot = FindRelativeDir(root, "wwwroot", false);

                if ((!string.IsNullOrEmpty(staticWebRoot)) && Directory.Exists(staticWebRoot))
                {
                    builder = WebApplication.CreateBuilder(new WebApplicationOptions()
                    {
                        WebRootPath = staticWebRoot,
                    });
                }
                else if ((!string.IsNullOrEmpty(wwwRoot)) && Directory.Exists(wwwRoot))
                {
                    builder = WebApplication.CreateBuilder(new WebApplicationOptions()
                    {
                        WebRootPath = wwwRoot,
                    });
                }
            }

            // if we didn't find a web root, use the default
            builder ??= WebApplication.CreateBuilder();

            StaticWebAssetsLoader.UseStaticWebAssets(builder.Environment, builder.Configuration);
            builder.WebHost.UseStaticWebAssets();

            string appCacheDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "fhir-candle-key-store");
            if (!Directory.Exists(appCacheDir))
            {
                Directory.CreateDirectory(appCacheDir);
            }

            builder.Services.AddDataProtection()
                .SetApplicationName("fhir-candle")
                //.SetDefaultKeyLifetime(TimeSpan.FromDays(90))
                .PersistKeysToFileSystem(new DirectoryInfo(appCacheDir));

            builder.Services.AddCors();

            // setup open telemetry if necessary
            ConfigureOpenTelemetry(config, builder);

            // add our configuration
            builder.Services.AddSingleton(config);
            builder.Services.AddSingleton(tenants);

            // add a FHIR-Store singleton, then register as a hosted service
            builder.Services.AddSingleton<IFhirStoreManager, FhirStoreManager>();
            builder.Services.AddHostedService<IFhirStoreManager>(sp => sp.GetRequiredService<IFhirStoreManager>());

            // add a notification manager singleton, then register as a hosted service
            builder.Services.AddSingleton<INotificationManager, NotificationManager>();
            builder.Services.AddHostedService<INotificationManager>(sp => sp.GetRequiredService<INotificationManager>());

            // add a package service singleton, then register as a hosted service
            builder.Services.AddSingleton<IFhirPackageService, FhirPackageService>();
            builder.Services.AddHostedService<IFhirPackageService>(sp => sp.GetRequiredService<IFhirPackageService>());

            // add a SMART Authorization singleton, then register as a hosted service
            builder.Services.AddSingleton<ISmartAuthManager, SmartAuthManager>();
            builder.Services.AddHostedService<ISmartAuthManager>(sp => sp.GetRequiredService<ISmartAuthManager>());

            if (config.EnableMcp == true)
            {
                FhirMcpTools fMcpTools = new();
                builder.Services.AddSingleton<FhirMcpTools>(fMcpTools);

                // add MCP services
                builder.Services.AddMcpServer()
                    .WithHttpTransport()
                    .WithListToolsHandler(fMcpTools.HandleListToolsRequest)
                    .WithCallToolHandler(fMcpTools.HandleCallToolRequest);
                // .WithToolsFromAssembly();
            }

            builder.Services.AddHttpContextAccessor();
            builder.Services.AddControllers();
            builder.Services.AddHttpClient();

            if (config.Headless == true)
            {
                // check for any SMART-enabled tenants - *requires* UI
                if (config.SmartRequiredTenants.Any() || config.SmartOptionalTenants.Any())
                {
                    Console.WriteLine("fhir-candle <<< ERROR: Cannot disable UI when SMART is configured.");
                    _retVal = -1;
                    return;
                }

                builder.Services.AddAntiforgery();
            }
            else
            {
                builder.Services.AddRazorComponents()
                    .AddInteractiveServerComponents();
                //builder.Services.AddRazorPages(options =>
                //{
                //    options.Conventions.AddPageRoute("/store", "/store/{storeName}");
                //});
                //builder.Services.AddServerSideBlazor();
                builder.Services.AddFluentUIComponents();

                // set our default UI page
                //Pages.Index.Mode = config.UiMode;
            }

            string localUrl = $"http://*:{config.ListenPort}";

            builder.WebHost.UseUrls(localUrl);
            //builder.WebHost.UseStaticWebAssets();

            WebApplication app = builder.Build();

            // we want to essentially disable CORS
            app.UseCors(b => b
                .AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader()
                .WithExposedHeaders(new[] { "Content-Location", "Location", "Etag", "Last-Modified" }));

            app.UseStaticFiles();

            app.UseRouting();
            app.UseAntiforgery();
            app.MapControllers();

            // this is developer tooling - always respond with as much detail as we can
            app.UseDeveloperExceptionPage();

            if (config.Headless != true)
            {
                app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
                //app.MapRazorComponents<Components.App>()
                //    .AddInteractiveServerRenderMode();
                //app.MapBlazorHub();
                //app.MapFallbackToPage("/_Host");
            }

            IFhirPackageService ps = app.Services.GetRequiredService<IFhirPackageService>();
            IFhirStoreManager sm = app.Services.GetRequiredService<IFhirStoreManager>();
            ISmartAuthManager am = app.Services.GetRequiredService<ISmartAuthManager>();

            // perform slow initialization of services
            ps.Init();          // store manager requires Package Service to be initialized
            sm.Init();          // store manager may need to download packages
            am.Init();          // spin up authorization manager

            if (config.EnableMcp == true)
            {
                // map our MCP services, use a /mcp prefix to avoid collisions with FHIR and UI services
                app.MapMcp("/mcp");
            }

            // run the server
            //await app.RunAsync(cancellationToken);
            _ = app.StartAsync();

            cancellationToken ??= new CancellationToken();

            AfterServerStart(app, config);
            await app.WaitForShutdownAsync((CancellationToken)cancellationToken);

            _retVal = 0;
            return;
        }
        catch (OperationCanceledException)
        {
            _retVal = 0;
            return;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"fhir-candle <<< caught exception: {ex.Message}");
            _retVal = ex.HResult;
            return;
        }
    }

    private static string? getTelemetryUrl(string? granular, string? general, string relative, OpenTelemetry.Exporter.OtlpExportProtocol protocol)
    {
        if (!string.IsNullOrEmpty(granular))
        {
            return granular;
        }

        if (!string.IsNullOrEmpty(general))
        {
            if (protocol != OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf)
            {
                return general;
            }

            if (general.EndsWith("v1/"))
            {
                return general + relative;
            }

            if (general.EndsWith("v1"))
            {
                return general + "/" + relative;
            }

            if (general.EndsWith('/'))
            {
                return general + "v1/" + relative;
            }

            return general + "/v1/" + relative;
        }

        return null;
    }

    /// <summary>
    /// Configures OpenTelemetry for the application.
    /// </summary>
    /// <param name="config">The CandleConfig object containing the configuration settings.</param>
    /// <param name="builder">The WebApplicationBuilder used to configure the application.</param>
    private static void ConfigureOpenTelemetry(CandleConfig config, WebApplicationBuilder builder)
    {
        OpenTelemetry.Exporter.OtlpExportProtocol protocol = config.OpenTelemetryProtocol.ToLowerInvariant() switch
        {
            "grpc" => OpenTelemetry.Exporter.OtlpExportProtocol.Grpc,
            "http/protobuf" => OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf,
            "protobuf" => OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf,
            _ => OpenTelemetry.Exporter.OtlpExportProtocol.Grpc,
        };

        string? traceEndpoint = getTelemetryUrl(
            config.OpenTelemetryTracesEndpoint,
            config.OpenTelemetryEndpoint,
            "traces",
            protocol);

        string? metricsEndpoint = getTelemetryUrl(
            config.OpenTelemetryMetricsEndpoint,
            config.OpenTelemetryEndpoint,
            "metrics",
            protocol);

        string? logsEndpoint = getTelemetryUrl(
            config.OpenTelemetryLogsEndpoint,
            config.OpenTelemetryEndpoint,
            "logs",
            protocol);

        if (logsEndpoint is not null)
        {
            builder.Logging.AddOpenTelemetry(options =>
            {
                options
                    .SetResourceBuilder(
                        ResourceBuilder.CreateDefault()
                            .AddService("fhir-candle"))
                    .AddConsoleExporter()
                    .AddOtlpExporter(exporterOptions =>
                    {
                        exporterOptions.Endpoint = new Uri(logsEndpoint);
                        exporterOptions.Protocol = protocol;
                    });
            });
        }

        if ((traceEndpoint is not null) && (metricsEndpoint is not null))
        {
            builder.Services.AddOpenTelemetry()
                .ConfigureResource(resource => resource.AddService("fhir-candle"))
                .WithTracing(tracing => tracing
                    .AddAspNetCoreInstrumentation()
                    .AddConsoleExporter()
                    .AddOtlpExporter(exporterOptions =>
                    {
                        exporterOptions.Endpoint = new Uri(traceEndpoint);
                        exporterOptions.Protocol = protocol;
                    }))
                .WithMetrics(metrics => metrics
                    .AddAspNetCoreInstrumentation()
                    .AddConsoleExporter()
                    .AddOtlpExporter(exporterOptions =>
                    {
                        exporterOptions.Endpoint = new Uri(metricsEndpoint);
                        exporterOptions.Protocol = protocol;
                    }));
        }
        else if (traceEndpoint is not null)
        {
            builder.Services.AddOpenTelemetry()
                .ConfigureResource(resource => resource.AddService("fhir-candle"))
                .WithTracing(tracing => tracing
                    .AddAspNetCoreInstrumentation()
                    .AddConsoleExporter()
                    .AddOtlpExporter(exporterOptions =>
                    {
                        exporterOptions.Endpoint = new Uri(traceEndpoint);
                        exporterOptions.Protocol = protocol;
                    }));
        }
        else if (metricsEndpoint is not null)
        {
            builder.Services.AddOpenTelemetry()
                .ConfigureResource(resource => resource.AddService("fhir-candle"))
                .WithMetrics(metrics => metrics
                    .AddAspNetCoreInstrumentation()
                    .AddConsoleExporter()
                    .AddOtlpExporter(exporterOptions =>
                    {
                        exporterOptions.Endpoint = new Uri(metricsEndpoint);
                        exporterOptions.Protocol = protocol;
                    }));
        }
    }

    /// <summary>After server start.</summary>
    /// <param name="app">   The application.</param>
    /// <param name="config">The configuration.</param>
    private static void AfterServerStart(WebApplication app, CandleConfig config)
    {
        Console.WriteLine("Press CTRL+C to exit");

        if (config.OpenBrowser == true)
        {
            string url = $"http://localhost:{config.ListenPort}";

            LaunchBrowser(url);
        }
    }

    /// <summary>Executes the browser operation.</summary>
    /// <param name="url">URL of the resource.</param>
    private static void LaunchBrowser(string url)
    {
        ProcessStartInfo psi = new();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            psi.FileName = "open";
            psi.ArgumentList.Add(url);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            psi.FileName = "xdg-open";
            psi.ArgumentList.Add(url);
        }
        else
        {
            psi.FileName = "cmd";
            psi.ArgumentList.Add("/C");
            psi.ArgumentList.Add("start");
            psi.ArgumentList.Add(url);
        }

        Process.Start(psi);
    }

    /// <summary>
    /// Builds a minimal <see cref="WebApplication"/> for in-process integration testing of
    /// <c>FhirController</c>. Wires only the services the controller depends on
    /// (FhirStoreManager + SmartAuthManager + FhirPackageService + DataProtection + MVC),
    /// skipping the production UI / Blazor / FluentUI / OpenTelemetry / MCP registrations.
    /// The caller is responsible for any additional setup (e.g. <c>UseTestServer()</c>) on
    /// the supplied <see cref="WebApplicationBuilder"/> before this method runs.
    /// </summary>
    /// <param name="builder">The pre-configured web application builder.</param>
    /// <param name="config">The fhir-candle configuration.</param>
    /// <param name="tenants">The tenant configurations keyed by controller name.</param>
    /// <returns>The fully built and initialized <see cref="WebApplication"/>, not yet started.</returns>
    public static WebApplication BuildAppForTesting(
        WebApplicationBuilder builder,
        CandleConfig config,
        Dictionary<string, TenantConfiguration> tenants)
    {
        builder.Services.AddDataProtection().SetApplicationName("fhir-candle-tests");
        builder.Services.AddCors();

        builder.Services.AddSingleton(config);
        builder.Services.AddSingleton(tenants);

        builder.Services.AddSingleton<IFhirPackageService, FhirPackageService>();
        builder.Services.AddSingleton<IFhirStoreManager, FhirStoreManager>();
        builder.Services.AddSingleton<ISmartAuthManager, SmartAuthManager>();

        builder.Services.AddHttpContextAccessor();
        builder.Services.AddControllers()
            .AddApplicationPart(typeof(fhir.candle.Controllers.FhirController).Assembly);
        builder.Services.AddHttpClient();

        WebApplication app = builder.Build();

        app.UseCors(b => b
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader()
            .WithExposedHeaders(new[] { "Content-Location", "Location", "Etag", "Last-Modified" }));

        app.UseRouting();
        app.MapControllers();
        app.UseDeveloperExceptionPage();

        IFhirPackageService ps = app.Services.GetRequiredService<IFhirPackageService>();
        IFhirStoreManager sm = app.Services.GetRequiredService<IFhirStoreManager>();
        ISmartAuthManager am = app.Services.GetRequiredService<ISmartAuthManager>();

        ps.Init();
        sm.Init();
        am.Init();

        return app;
    }

    /// <summary>Builds an enumeration of tenant configurations for this application.</summary>
    /// <param name="config">The configuration.</param>
    /// <returns>
    /// An enumerator that allows foreach to be used to process build tenant configurations in this
    /// collection.
    /// </returns>
    private static Dictionary<string, TenantConfiguration> BuildTenantConfigurations(CandleConfig config)
    {
        HashSet<string> smartRequired = config.SmartRequiredTenants.ToHashSet();
        HashSet<string> smartOptional = config.SmartOptionalTenants.ToHashSet();

        bool allRequired = smartRequired.Contains("*");
        bool allOptional = smartOptional.Contains("*");

        // Emit per-override strict conflict warnings before building tenants, so they
        // surface even if tenant construction throws.
        foreach (StrictConflictWarning warning in ComputeStrictWarnings(config))
        {
            Console.Error.WriteLine(
                $"fhir-candle <<< WARN: --strict overrides --{warning.Flag} {warning.RawValue}; " +
                $"spec-enforced value is {warning.StrictEnforced}. " +
                $"See {StrictRule.GetSpecUrl(warning.Rule, FhirReleases.FhirSequenceCodes.R4)}.");
        }

        Dictionary<string, TenantConfiguration> tenants = new();

        foreach (string tenant in config.TenantsR4)
        {
            tenants.Add(tenant, new()
            {
                FhirVersion = FhirReleases.FhirSequenceCodes.R4,
                ControllerName = tenant,
                BaseUrl = config.PublicUrl + "/fhir/" + tenant,
                ProtectLoadedContent = config.ProtectLoadedContent,
                SupportNotChanged = config.SupportNotChanged,
                MaxResourceCount = config.MaxResourceCount,
                MaxSubscriptionExpirationMinutes = config.MaxSubscriptionExpirationMinutes,
                SmartRequired = allRequired || smartRequired.Contains(tenant),
                SmartAllowed = allOptional || smartOptional.Contains(tenant),
                AllowExistingId = config.AllowExistingId,
                AllowCreateAsUpdate = config.AllowCreateAsUpdate,
                Strict = config.Strict,
            });
        }

        foreach (string tenant in config.TenantsR4B)
        {
            tenants.Add(tenant, new()
            {
                FhirVersion = FhirReleases.FhirSequenceCodes.R4B,
                ControllerName = tenant,
                BaseUrl = config.PublicUrl + "/fhir/" + tenant,
                ProtectLoadedContent = config.ProtectLoadedContent,
                MaxResourceCount = config.MaxResourceCount,
                MaxSubscriptionExpirationMinutes = config.MaxSubscriptionExpirationMinutes,
                SmartRequired = allRequired || smartRequired.Contains(tenant),
                SmartAllowed = allOptional || smartOptional.Contains(tenant),
                AllowExistingId = config.AllowExistingId,
                AllowCreateAsUpdate = config.AllowCreateAsUpdate,
                Strict = config.Strict,
            });
        }

        foreach (string tenant in config.TenantsR5)
        {
            tenants.Add(tenant, new()
            {
                FhirVersion = FhirReleases.FhirSequenceCodes.R5,
                ControllerName = tenant,
                BaseUrl = config.PublicUrl + "/fhir/" + tenant,
                ProtectLoadedContent = config.ProtectLoadedContent,
                MaxResourceCount = config.MaxResourceCount,
                MaxSubscriptionExpirationMinutes = config.MaxSubscriptionExpirationMinutes,
                SmartRequired = allRequired || smartRequired.Contains(tenant),
                SmartAllowed = allOptional || smartOptional.Contains(tenant),
                AllowExistingId = config.AllowExistingId,
                AllowCreateAsUpdate = config.AllowCreateAsUpdate,
                Strict = config.Strict,
            });
        }

        DirectoryInfo? loadDir = null;

        if (!string.IsNullOrEmpty(config.SourceDirectory))
        {
            if (Path.IsPathRooted(config.SourceDirectory) &&
                Directory.Exists(config.SourceDirectory))
            {
                loadDir = new DirectoryInfo(config.SourceDirectory);
            }
            else
            {
                // look for a relative directory, starting in the running directory
                string relativeDir = FindRelativeDir(string.Empty, config.SourceDirectory, false);

                if (!string.IsNullOrEmpty(relativeDir))
                {
                    loadDir = new DirectoryInfo(relativeDir);
                }
            }
        }

        if (loadDir is not null)
        {
            foreach (TenantConfiguration tenant in tenants.Values)
            {
                // check for a tenant-named subdirectory
                string subPath = Path.Combine(loadDir.FullName, tenant.ControllerName);
                tenant.LoadDirectory = Directory.Exists(subPath) ? new DirectoryInfo(subPath) : loadDir;
            }
        }

        // Resolve --strict composition on each tenant config before it leaves this
        // builder so the visible tenant config matches what VersionedFhirStore.Init
        // will see. Idempotent — Init will call ResolveStrict() again defensively.
        foreach (TenantConfiguration tenant in tenants.Values)
        {
            tenant.ResolveStrict();

            if (tenant.Strict)
            {
                Console.WriteLine($"fhir-candle <<< Strict mode active for tenant '{tenant.ControllerName}'.");
            }
        }

        return tenants;
    }

    /// <summary>A single strict-mode override warning surfaced at startup.</summary>
    /// <param name="Flag">         The CLI flag name (without the leading <c>--</c>).</param>
    /// <param name="RawValue">     The user-supplied value (or <c>null</c> if defaulted).</param>
    /// <param name="StrictEnforced">The value that <c>--strict</c> forces.</param>
    /// <param name="Rule">         The strict rule that justifies the override.</param>
    internal readonly record struct StrictConflictWarning(
        string Flag,
        bool? RawValue,
        bool StrictEnforced,
        StrictRuleCode Rule);

    /// <summary>
    /// Computes per-feature override warnings to emit when <c>--strict</c> is paired
    /// with an explicit conflicting flag value. Returns an empty list if strict is off
    /// or no conflicts exist. Pure / no side-effects so it can be unit-tested.
    /// </summary>
    /// <param name="config">The CLI configuration.</param>
    /// <returns>One <see cref="StrictConflictWarning"/> per conflicting flag.</returns>
    internal static List<StrictConflictWarning> ComputeStrictWarnings(CandleConfig config)
    {
        List<StrictConflictWarning> warnings = [];

        if (!config.Strict)
        {
            return warnings;
        }

        if (config.RawAllowExistingId == true)
        {
            warnings.Add(new StrictConflictWarning(
                "create-existing-id",
                config.RawAllowExistingId,
                false,
                StrictRuleCode.PostClientSuppliedId));
        }

        if (config.RawAllowCreateAsUpdate == true)
        {
            warnings.Add(new StrictConflictWarning(
                "create-as-update",
                config.RawAllowCreateAsUpdate,
                false,
                StrictRuleCode.PutCreateAsUpdateDisallowed));
        }

        return warnings;
    }

    /// <summary>Searches for the FHIR specification directory.</summary>
    /// <exception cref="DirectoryNotFoundException">Thrown when the requested directory is not
    ///  present.</exception>
    /// <param name="startDir"></param>
    /// <param name="dirName">       The name of the directory we are searching for.</param>
    /// <param name="throwIfNotFound">(Optional) True to throw if not found.</param>
    /// <returns>The found FHIR directory.</returns>
    public static string FindRelativeDir(
        string startDir,
        string dirName,
        bool throwIfNotFound = true)
    {
        if (dirName.Contains('~'))
        {
            // we have a relative path from the user directory
            dirName = dirName.Replace("~", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        }

        if (Directory.Exists(dirName))
        {
            return Path.GetFullPath(dirName);
        }

        string currentDir = string.IsNullOrEmpty(startDir)
            ? Path.GetDirectoryName(AppContext.BaseDirectory) ?? string.Empty
            : startDir;
        string testDir = Path.Combine(currentDir, dirName);

        while (!Directory.Exists(testDir))
        {
            currentDir = Path.GetFullPath(Path.Combine(currentDir, ".."));

            if (currentDir == Path.GetPathRoot(currentDir))
            {
                if (throwIfNotFound)
                {
                    throw new DirectoryNotFoundException($"Could not find directory {dirName}!");
                }

                return string.Empty;
            }

            testDir = Path.Combine(currentDir, dirName);
        }

        return testDir;
    }
}
