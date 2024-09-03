// <copyright file="Program.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using fhir.candle.Services;
using FhirCandle.Configuration;
using FhirCandle.Models;
using FhirCandle.Utils;
using Microsoft.AspNetCore.Hosting.StaticWebAssets;
using Microsoft.FluentUI.AspNetCore.Components;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using static Org.BouncyCastle.Math.EC.ECCurve;
using SCL = System.CommandLine; // this is present to disambiguate Option from System.CommandLine and Microsoft.FluentUI.AspNetCore.Components


namespace fhir.candle;

/// <summary>A program.</summary>
public static partial class Program
{
    private static List<SCL.Option> _optsWithEnums = [];

    [GeneratedRegex("(http[s]*:\\/\\/.*(:\\d+)*)")]
    private static partial Regex InputUrlFormatRegex();

    /// <summary>Main entry-point for this application.</summary>
    /// <param name="args">An array of command-line argument strings.</param>
    public static async Task<int> Main(string[] args)
    {
        // setup our configuration (command line > environment > appsettings.json)
        IConfiguration envConfig = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        //Dictionary<string, SCL.Option> options = BuildCliOptions(typeof(CandleConfig), envConfig: envConfig);
        ConfigurationOption[] configurationOptions = (new CandleConfig()).GetOptions();
        List<string> envArgs = [];

        // build our root command
        SCL.RootCommand rootCommand = new("A lightweight in-memory FHIR server, for when a small FHIR will do.");
        SCL.RootCommand envCommand = new("A lightweight in-memory FHIR server, for when a small FHIR will do.");
        foreach (ConfigurationOption co in configurationOptions)
        {
            // note that 'global' here is just recursive DOWNWARD
            rootCommand.AddGlobalOption(co.CliOption);
            envCommand.AddGlobalOption(co.CliOption);

            if (string.IsNullOrEmpty(co.EnvVarName))
            {
                continue;
            }

            IConfigurationSection section = envConfig.GetSection(co.EnvVarName);

            if (!section.Exists())
            {
                continue;
            }

            string? value = section.Value ?? null;

            if (value == null)
            {
                value = string.Join(',', section.GetChildren().Select(c => c.Value));
            }

            if (string.IsNullOrEmpty(value))
            {
                continue;
            }

            if (value.Contains(' '))
            {
                value = $"\"{value}\"";
            }

            if (co.CliOption.Aliases.Any())
            {
                envArgs.Add(co.CliOption.Aliases.First());
                envArgs.Add(value);
                continue;
            }

            if (!string.IsNullOrEmpty(co.CliOption.Name))
            {
                envArgs.Add(co.CliOption.Name);
                envArgs.Add(value);
                continue;
            }
        }

        // build a parser for our environment arguments
        SCL.Parsing.Parser envParser = new CommandLineBuilder(envCommand).UseDefaults().Build();

        // attempt a parse
        SCL.Parsing.ParseResult envParseResult = envParser.Parse(string.Join(' ', envArgs));

        // set our command handler
        rootCommand.SetHandler(async (context) => await RunServer(context.ParseResult, envParseResult, context.GetCancellationToken()));

        // run whatever the caller requested
        return await rootCommand.InvokeAsync(args);
    }

    /// <summary>
    /// Builds the command line options for the specified type.
    /// </summary>
    /// <param name="forType">The type for which to build the command line options.</param>
    /// <param name="excludeFromType">The type to exclude from the command line options.</param>
    /// <param name="envConfig">The environment configuration.</param>
    /// <returns>An enumerable collection of command line options.</returns>
    private static Dictionary<string, SCL.Option> BuildCliOptions(
        Type forType,
        Type? excludeFromType = null,
        IConfiguration? envConfig = null)
    {
        Dictionary<string, SCL.Option> options = [];
        HashSet<string> inheritedPropNames = [];

        if (excludeFromType != null)
        {
            PropertyInfo[] exProps = excludeFromType.GetProperties();
            foreach (PropertyInfo exProp in exProps)
            {
                inheritedPropNames.Add(exProp.Name);
            }
        }

        object? configDefault = null;
        if (forType.IsAbstract)
        {
            throw new Exception($"Config type cannot be abstract! {forType.Name}");
        }

        configDefault = Activator.CreateInstance(forType);

        if (configDefault is not CandleConfig config)
        {
            throw new Exception("Config type must be CandleConfig");
        }

        foreach (ConfigurationOption opt in config.GetOptions())
        {
            // need to configure default values
            if ((envConfig != null) &&
                (!string.IsNullOrEmpty(opt.EnvVarName)))
            {
                opt.CliOption.SetDefaultValueFactory(() => envConfig.GetSection(opt.EnvVarName).GetChildren().Select(c => c.Value));
            }
            else
            {
                opt.CliOption.SetDefaultValue(opt.DefaultValue);
            }

            options[opt.Name] = opt.CliOption;
        }

        return options;
    }

    /// <summary>Executes the server operation.</summary>
    /// <param name="config">           The configuration.</param>
    /// <param name="cancellationToken">A token that allows processing to be cancelled.</param>
    /// <returns>An asynchronous result that yields an int.</returns>
    public static async Task<int> RunServer(
        SCL.Parsing.ParseResult pr,
        SCL.Parsing.ParseResult envPR,
        CancellationToken? cancellationToken = null)
    { 
        try
        {
            CandleConfig config = new();

            // parse the arguments into the configuration object
            config.Parse(pr, envPR);

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

            WebApplicationBuilder builder = null!;

            // when packaging as a dotnet tool, we need to do some directory shenanigans for the static content root
            string root = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location ?? AppContext.BaseDirectory) ?? string.Empty;
            if (!string.IsNullOrEmpty(root))
            {
                string webRoot = FindRelativeDir(root, "staticwebassets", false);

                if ((!string.IsNullOrEmpty(webRoot)) && Directory.Exists(webRoot))
                {
                    builder = WebApplication.CreateBuilder(new WebApplicationOptions()
                    {
                        WebRootPath = webRoot,
                    });
                }
            }

            if (builder == null)
            {
                builder = WebApplication.CreateBuilder();
            }

            StaticWebAssetsLoader.UseStaticWebAssets(builder.Environment, builder.Configuration);
            builder.WebHost.UseStaticWebAssets();

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

            builder.Services.AddControllers();
            builder.Services.AddHttpClient();

            if (config.DisableUi == true)
            {
                // check for any SMART-enabled tenants - *requires* UI
                if (config.SmartRequiredTenants.Any() || config.SmartOptionalTenants.Any())
                {
                    Console.WriteLine("fhir-candle <<< ERROR: Cannot disable UI when SMART is configured.");
                    return -1;
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
            app.UseCors(builder => builder
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

            if (config.DisableUi != true)
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

            // run the server
            //await app.RunAsync(cancellationToken);
            _ = app.StartAsync();

            cancellationToken ??= new CancellationToken();

            AfterServerStart(app, config);
            await app.WaitForShutdownAsync((CancellationToken)cancellationToken);

            return 0;
        }
        catch (OperationCanceledException)
        {
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"fhir-candle <<< caught exception: {ex.Message}");
            return -1;
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

            if (general.EndsWith('/'))
            {
                return general + relative;
            }

            return general + "/" + relative;
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
            "v1/traces",
            protocol);

        string? metricsEndpoint = getTelemetryUrl(
            config.OpenTelemetryMetricsEndpoint,
            config.OpenTelemetryEndpoint,
            "v1/metrics",
            protocol);

        string? logsEndpoint = getTelemetryUrl(
            config.OpenTelemetryLogsEndpoint,
            config.OpenTelemetryEndpoint,
            "v1/logs",
            protocol);

        if (logsEndpoint != null)
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

        if ((traceEndpoint != null) && (metricsEndpoint != null))
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
        else if (traceEndpoint != null)
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
        else if (metricsEndpoint != null)
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

        Dictionary<string, TenantConfiguration> tenants = new();

        foreach (string tenant in config.TenantsR4)
        {
            tenants.Add(tenant, new()
            {
                FhirVersion = FhirReleases.FhirSequenceCodes.R4,
                ControllerName = tenant,
                BaseUrl = config.PublicUrl + "/fhir/" + tenant,
                ProtectLoadedContent = config.ProtectLoadedContent,
                MaxResourceCount = config.MaxResourceCount,
                MaxSubscriptionExpirationMinutes = config.MaxSubscriptionExpirationMinutes,
                SmartRequired = allRequired || smartRequired.Contains(tenant),
                SmartAllowed = allOptional || smartOptional.Contains(tenant),
                AllowExistingId = config.AllowExistingId,
                AllowCreateAsUpdate = config.AllowCreateAsUpdate,
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

        if (loadDir != null)
        {
            foreach (TenantConfiguration tenant in tenants.Values)
            {
                // check for a tenant-named sub-directory
                string subPath = Path.Combine(loadDir.FullName, tenant.ControllerName);
                if (Directory.Exists(subPath))
                {
                    tenant.LoadDirectory = new DirectoryInfo(subPath);
                }
                else
                {
                    tenant.LoadDirectory = loadDir;
                }
            }
        }

        return tenants;
    }

    /// <summary>Searches for the FHIR specification directory.</summary>
    /// <exception cref="DirectoryNotFoundException">Thrown when the requested directory is not
    ///  present.</exception>
    /// <param name="dirName">       The name of the directory we are searching for.</param>
    /// <param name="throwIfNotFound">(Optional) True to throw if not found.</param>
    /// <returns>The found FHIR directory.</returns>
    public static string FindRelativeDir(
        string startDir,
        string dirName,
        bool throwIfNotFound = true)
    {
        string currentDir = string.IsNullOrEmpty(startDir) ? Path.GetDirectoryName(AppContext.BaseDirectory) ?? string.Empty : startDir;
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
