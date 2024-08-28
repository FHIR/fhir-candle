// <copyright file="Program.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using SCL = System.CommandLine; // this is present to disambiguate Option from System.CommandLine and Microsoft.FluentUI.AspNetCore.Components
using System;
using System.CommandLine.Binding;
using System.CommandLine.Invocation;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using fhir.candle.Models;
using fhir.candle.Services;
using FhirCandle.Extensions;
using FhirCandle.Models;
using FhirCandle.Storage;
using FhirCandle.Utils;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Hosting.StaticWebAssets;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.FluentUI.AspNetCore.Components;
using fhir.candle;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using System.Text;
using System.CommandLine;
using FhirCandle.Configuration;
using System.Reflection;
using static Org.BouncyCastle.Math.EC.ECCurve;
using BlazorMonaco.Languages;

namespace fhir.candle;

/// <summary>A program.</summary>
public static partial class Program
{
    private static List<SCL.Option> _optsWithEnums = [];

    [GeneratedRegex("(http[s]*:\\/\\/.*(:\\d+)*)")]
    private static partial Regex InputUrlFormatRegex();

    /// <summary>(Immutable) The default subscription expiration.</summary>
    private static readonly int DefaultSubscriptionExpirationMinutes = 30;

    /// <summary>Main entry-point for this application.</summary>
    /// <param name="args">An array of command-line argument strings.</param>
    public static async Task<int> Main(string[] args)
    {
        // setup our configuration (command line > environment > appsettings.json)
        IConfiguration envConfig = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        //// in order to process help correctly we have to build a parser independent of the command
        //SCL.Parsing.Parser parser = BuildParser(envConfig);

        //// attempt a parse
        //SCL.Parsing.ParseResult pr = parser.Parse(args);

        SCL.RootCommand rootCommand = new("A lightweight in-memory FHIR server, for when a small FHIR will do.");
        foreach (SCL.Option option in BuildCliOptions(typeof(CandleConfig), envConfig: envConfig))
        {
            // note that 'global' here is just recursive DOWNWARD
            rootCommand.AddGlobalOption(option);
        }
        rootCommand.SetHandler(async (context) => await RunServer(context.ParseResult, context.GetCancellationToken()));

        return await rootCommand.InvokeAsync(args);

        //// check for invalid arguments, help, a generate command with no subcommand, or a generate with no packages to trigger the nicely formatted help
        //if (pr.UnmatchedTokens.Any() ||
        //    !pr.Tokens.Any() ||
        //    (!pr.CommandResult.Command.Parents?.Any() ?? false) ||
        //    pr.Tokens.Any(t => t.Value.Equals("-?", StringComparison.Ordinal)) ||
        //    pr.Tokens.Any(t => t.Value.Equals("-h", StringComparison.Ordinal)) ||
        //    pr.Tokens.Any(t => t.Value.Equals("--help", StringComparison.Ordinal)) ||
        //    pr.Tokens.Any(t => t.Value.Equals("help", StringComparison.Ordinal)))

        //{
        //    return await parser.InvokeAsync(args);
        //}


        //return await RunServer(pr);



        //// in order to process help correctly we have to build a parser independent of the command
        //SCL.Parsing.Parser parser = BuildParser(envConfig);

        //// attempt a parse
        //SCL.Parsing.ParseResult pr = parser.Parse(args);

        //return await parser.InvokeAsync(args);

        ////System.CommandLine.Parsing.Parser clParser = new System.CommandLine.Builder.CommandLineBuilder(_rootCommand).Build();

        //return await rootCommand.InvokeAsync(args);
    }


    private static SCL.Parsing.Parser BuildParser(IConfiguration envConfig)
    {
        SCL.RootCommand command = new("A lightweight in-memory FHIR server, for when a small FHIR will do.");
        foreach (SCL.Option option in BuildCliOptions(typeof(CandleConfig), envConfig: envConfig))
        {
            // note that 'global' here is just recursive DOWNWARD
            command.AddGlobalOption(option);
            TrackIfEnum(option);
        }

        //command.SetHandler(async (context) => await RunServer(context.ParseResult, context.GetCancellationToken()));

        SCL.Parsing.Parser parser = new CommandLineBuilder(command)
            .UseExceptionHandler((ex, ctx) =>
            {
                Console.WriteLine($"Error: {ex.Message}");
                ctx.ExitCode = 1;
            })
            .UseDefaults()
            .UseHelp(ctx =>
            {
                foreach (SCL.Option option in _optsWithEnums)
                {
                    StringBuilder sb = new();
                    if (option.Aliases.Count != 0)
                    {
                        sb.AppendLine(string.Join(", ", option.Aliases));
                    }
                    else
                    {
                        sb.AppendLine(option.Name);
                    }

                    Type et = option.ValueType;

                    if (option.ValueType.IsGenericType)
                    {
                        et = option.ValueType.GenericTypeArguments.First();
                    }

                    if (option.ValueType.IsArray)
                    {
                        et = option.ValueType.GetElementType()!;
                    }

                    foreach (MemberInfo mem in et.GetMembers(BindingFlags.Public | BindingFlags.Static).Where(m => m.DeclaringType == et).OrderBy(m => m.Name))
                    {
                        sb.AppendLine($"  opt: {mem.Name}");
                    }

                    ctx.HelpBuilder.CustomizeSymbol(
                        option,
                        firstColumnText: (ctx) => sb.ToString());
                    //secondColumnText: (ctx) => option.Description);
                }
            })
            .Build();

        return parser;

        void TrackIfEnum(SCL.Option option)
        {
            if (option.ValueType.IsEnum)
            {
                _optsWithEnums.Add(option);
                return;
            }

            if (option.ValueType.IsGenericType)
            {
                if (option.ValueType.GenericTypeArguments.First().IsEnum)
                {
                    _optsWithEnums.Add(option);
                }

                return;
            }

            if (option.ValueType.IsArray)
            {
                if (option.ValueType.GetElementType()!.IsEnum)
                {
                    _optsWithEnums.Add(option);
                }

                return;
            }
        }
    }

    private static IEnumerable<SCL.Option> BuildCliOptions(
        Type forType,
        Type? excludeFromType = null,
        IConfiguration? envConfig = null)
    {
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

            yield return opt.CliOption;
        }
    }

    /// <summary>Executes the server operation.</summary>
    /// <param name="config">           The configuration.</param>
    /// <param name="cancellationToken">A token that allows processing to be cancelled.</param>
    /// <returns>An asynchronous result that yields an int.</returns>
    public static async Task<int> RunServer(SCL.Parsing.ParseResult pr, CancellationToken? cancellationToken = null)
    { 
        try
        {
            CandleConfig config = new();

            // parse the arguments into the configuration object
            config.Parse(pr);

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

            //builder.WebHost.UseWebRoot("wwwroot");

            builder.WebHost.UseStaticWebAssets();

            builder.Services.AddCors();

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

            if (config.DisableUi == true)
            {
                // check for any SMART-enabled tenants - *requires* UI
                if (config.SmartRequiredTenants.Any() || config.SmartOptionalTenants.Any())
                {
                    Console.WriteLine("fhir-candle <<< ERROR: Cannot disable UI when SMART is configured.");
                    return -1;
                }
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
                builder.Services.AddHttpClient();
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
                SmartRequired = smartRequired.Contains(tenant),
                SmartAllowed = smartOptional.Contains(tenant),
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
                SmartRequired = smartRequired.Contains(tenant),
                SmartAllowed = smartOptional.Contains(tenant),
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
                SmartRequired = smartRequired.Contains(tenant),
                SmartAllowed = smartOptional.Contains(tenant),
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
