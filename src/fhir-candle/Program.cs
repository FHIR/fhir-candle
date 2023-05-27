// <copyright file="Program.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using System;
using System.CommandLine;
using System.CommandLine.Binding;
using System.CommandLine.Invocation;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using fhir.candle.Models;
using fhir.candle.Services;
using FhirStore.Models;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using MudBlazor.Services;

namespace fhir.candle;

/// <summary>A program.</summary>
public static partial class Program
{
    [GeneratedRegex("(http[s]*:\\/\\/[A-Za-z0-9\\.]*(:\\d+)*)")]
    private static partial Regex InputUrlFormatRegex();

    /// <summary>(Immutable) The default listen port.</summary>
    private const int _defaultListenPort = 5826;

    ///// <summary>Candle server delagate.</summary>
    ///// <param name="config">The configuration.</param>
    ///// <returns>An int.</returns>
    //public delegate int CandleServerDelagate(ServerConfiguration config);

    /// <summary>Main entry-point for this application.</summary>
    /// <param name="args">An array of command-line argument strings.</param>
    public static async Task<int> Main(string[] args)
    {
        // setup our configuration (command line > environment > appsettings.json)
        IConfiguration configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        Option<string> optPublicUrl = new(
            aliases: new[] { "--url", "-u" },
            getDefaultValue: () => configuration.GetValue("Public_Url", string.Empty) ?? string.Empty,
            "Public URL for the server");

        Option<int?> optListenPort = new(
            aliases: new[] { "--port", "-p" },
            getDefaultValue: () => configuration.GetValue<int>("Listen_Port", _defaultListenPort),
            "Listen port for the server");

        Option<bool?> optOpenBrowser = new(
            aliases: new[] { "--open-browser", "-o" },
            getDefaultValue: () => configuration.GetValue<bool>("Open_Browser", false),
            "Open a browser once the server starts.");

        Option<int?> optMaxResourceCount = new(
            aliases: new[] { "--max-resources", "-m" },
            getDefaultValue: () => configuration.GetValue<int?>("Max_Resources", null),
            "Maximum number of resources allowed per tenant.");

        Option<bool?> optNoGui = new(
            name: "--no-gui",
            getDefaultValue: () => configuration.GetValue<bool>("No_Gui", false),
            "Run without loading the GUI");

        Option<string> optDefaultPage = new(
            name:  "--default-page",
            getDefaultValue: () => configuration.GetValue<string>("Default_Page", "index-candle")!,
            "Default index page to route to");

        Option<string?> optSourceDirectory = new(
            name: "--fhir-source",
            getDefaultValue: () => null,
            "FHIR Contents to load, either in this directory or by subdirectories named per tenant");

        Option<bool?> optProtectLoadedContent = new(
            name: "--protect-source",
            getDefaultValue: () => null,
            "If any loaded FHIR contents cannot be altered.");

        Option<List<string>> optTenantsR4 = new(
            name: "--r4",
            getDefaultValue: () => new(),
            "FHIR R4 Tenants to provide");

        Option<List<string>> optTenantsR4B = new(
            name: "--r4b",
            getDefaultValue: () => new(),
            "FHIR R4B Tenants to provide");

        Option<List<string>> optTenantsR5 = new(
            name: "--r5",
            getDefaultValue: () => new(),
            "FHIR R5 Tenants to provide");

        Option<string> optZulipEmail = new(
            name: "--zulip-email",
            getDefaultValue: () => configuration.GetValue("Zulip_Email", string.Empty) ?? string.Empty,
            "Zulip bot email address");

        Option<string> optZulipKey = new(
            name: "--zulip-key",
            getDefaultValue: () => configuration.GetValue("Zulip_Key", string.Empty) ?? string.Empty,
            "Zulip bot API key");

        Option<string> optZulipUrl = new(
            name: "--zulip-url",
            getDefaultValue: () => configuration.GetValue("Zulip_Url", string.Empty) ?? string.Empty,
            "Zulip bot email address");

        Option<string> optSmtpHost = new(
            name: "--smtp-host",
            getDefaultValue: () => configuration.GetValue("SMTP_Host", string.Empty) ?? string.Empty,
            "SMTP Host name/address");

        Option<int?> optSmtpPort = new(
            name: "--smtp-port",
            getDefaultValue: () => configuration.GetValue<int?>("SMTP_Port", null),
            "SMTP Port");

        Option<string> optSmtpUser = new(
            name: "--smtp-user",
            getDefaultValue: () => configuration.GetValue("SMTP_User", string.Empty) ?? string.Empty,
            "SMTP Username");

        Option<string> optSmtpPassword = new(
            name: "--smtp-password",
            getDefaultValue: () => configuration.GetValue("SMTP_Password", string.Empty) ?? string.Empty,
            "SMTP Password");

        RootCommand rootCommand = new()
        {
            optPublicUrl,
            optListenPort,
            optOpenBrowser,
            optMaxResourceCount,
            optNoGui,
            optDefaultPage,
            optSourceDirectory,
            optProtectLoadedContent,
            optTenantsR4,
            optTenantsR4B,
            optTenantsR5,
            optZulipEmail,
            optZulipKey,
            optZulipUrl,
            optSmtpHost,
            optSmtpPort,
            optSmtpUser,
            optSmtpPassword,
        };

        rootCommand.Description = "A lightweight in-memory FHIR server, for when a small FHIR will do.";

        rootCommand.SetHandler(async (context) =>
        {
            ServerConfiguration config = new()
            {
                PublicUrl = context.ParseResult.GetValueForOption(optPublicUrl) ?? string.Empty,
                ListenPort = context.ParseResult.GetValueForOption(optListenPort) ?? _defaultListenPort,
                OpenBrowser = context.ParseResult.GetValueForOption(optOpenBrowser) ?? false,
                MaxResourceCount = context.ParseResult.GetValueForOption(optMaxResourceCount) ?? 0,
                NoGui = context.ParseResult.GetValueForOption(optNoGui) ?? false,
                DefaultIndexPage = context.ParseResult.GetValueForOption(optDefaultPage) ?? string.Empty,
                SourceDirectory = context.ParseResult.GetValueForOption(optSourceDirectory),
                ProtectLoadedContent = context.ParseResult.GetValueForOption(optProtectLoadedContent) ?? false,
                TenantsR4 = context.ParseResult.GetValueForOption(optTenantsR4) ?? new(),
                TenantsR4B = context.ParseResult.GetValueForOption(optTenantsR4B) ?? new(),
                TenantsR5 = context.ParseResult.GetValueForOption(optTenantsR5) ?? new(),
                ZulipEmail = context.ParseResult.GetValueForOption(optZulipEmail) ?? string.Empty,
                ZulipKey = context.ParseResult.GetValueForOption(optZulipKey) ?? string.Empty,
                ZulipUrl = context.ParseResult.GetValueForOption(optZulipUrl) ?? string.Empty,
                SmtpHost = context.ParseResult.GetValueForOption(optSmtpHost) ?? string.Empty,
                SmtpPort = context.ParseResult.GetValueForOption(optSmtpPort) ?? 0,
                SmtpUser = context.ParseResult.GetValueForOption(optSmtpUser) ?? string.Empty,
                SmtpPassword = context.ParseResult.GetValueForOption(optSmtpPassword) ?? string.Empty,
            };

            await RunServer(config, context.GetCancellationToken());
        });

        //System.CommandLine.Parsing.Parser clParser = new System.CommandLine.Builder.CommandLineBuilder(_rootCommand).Build();

        return await rootCommand.InvokeAsync(args);
    }

    /// <summary>Executes the server operation.</summary>
    /// <param name="config">           The configuration.</param>
    /// <param name="cancellationToken">A token that allows processing to be cancelled.</param>
    /// <returns>An asynchronous result that yields an int.</returns>
    public static async Task<int> RunServer(ServerConfiguration config, CancellationToken cancellationToken)
    { 
        try
        {
            // update configuration to make sure listen url is properly formatted
            Match match = InputUrlFormatRegex().Match(config.PublicUrl);
            config.PublicUrl = match.ToString();

            // check for no tenants (create defaults)
            if ((!config.TenantsR4.Any()) &&
                (!config.TenantsR4B.Any()) &&
                (!config.TenantsR5.Any()))
            {
                config.TenantsR4.Add("r4");
                config.TenantsR4B.Add("r4b");
                config.TenantsR5.Add("r5");
            }

            Dictionary<string, TenantConfiguration> tenants = BuildTeantConfigurations(config);

            WebApplicationBuilder builder = WebApplication.CreateBuilder();

            builder.Services.AddCors();

            // add our configuration
            builder.Services.AddSingleton(config);
            builder.Services.AddSingleton(tenants);

            // add a FHIR-Store singleton, then register as a hosted service
            builder.Services.AddSingleton<IFhirStoreManager, FhirStoreManager>();
            builder.Services.AddHostedService<IFhirStoreManager>(sp => sp.GetRequiredService<IFhirStoreManager>());

            // add a FHIR-Store singleton, then register as a hosted service
            builder.Services.AddSingleton<INotificationManager, NotificationManager>();
            builder.Services.AddHostedService<INotificationManager>(sp => sp.GetRequiredService<INotificationManager>());

            builder.Services.AddControllers();

            if (!config.NoGui)
            {
                builder.Services.AddRazorPages(options =>
                {
                    options.Conventions.AddPageRoute("/store", "/store/{storeName}");
                });
                builder.Services.AddServerSideBlazor();
                builder.Services.AddMudServices();

                // set our default UI page
                Pages.Index.RootPage = string.IsNullOrEmpty(config.DefaultIndexPage)
                    ? "/index-subscriptions"
                    : "/" + config.DefaultIndexPage;
            }

            string localUrl = $"http://localhost:{config.ListenPort}";

            builder.WebHost.UseUrls(localUrl);
            builder.WebHost.UseStaticWebAssets();

            WebApplication app = builder.Build();

            // we want to essentially disable CORS
            app.UseCors(builder => builder
                .AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader());

            app.UseStaticFiles();

            app.UseRouting();

            app.MapControllers();

            if (!config.NoGui)
            {
                app.MapBlazorHub();
                app.MapFallbackToPage("/_Host");
            }

            // run the server
            //await app.RunAsync(cancellationToken);
            _ = app.StartAsync();

            //_ = app.RunAsync(cancellationToken);
            AfterServerStart(app, config);
            await app.WaitForShutdownAsync(cancellationToken);


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

    private static void AfterServerStart(WebApplication app, ServerConfiguration config)
    {
        Console.WriteLine("Press CTRL+C to exit");

        if (config.OpenBrowser == true)
        {
            string url = $"http://localhost:{config.ListenPort}";

            LaunchBrowser(url);
        }
    }

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

    /// <summary>Builds an enumeration of teant configurations for this application.</summary>
    /// <param name="config">The configuration.</param>
    /// <returns>
    /// An enumerator that allows foreach to be used to process build teant configurations in this
    /// collection.
    /// </returns>
    private static Dictionary<string, TenantConfiguration> BuildTeantConfigurations(ServerConfiguration config)
    {
        Dictionary<string, TenantConfiguration> tenants = new();

        foreach (string tenant in config.TenantsR4)
        {
            tenants.Add(tenant, new()
            {
                FhirVersion = TenantConfiguration.SupportedFhirVersions.R4,
                ControllerName = tenant,
                BaseUrl = config.PublicUrl + "/fhir/" + tenant,
                ProtectLoadedContent = config.ProtectLoadedContent,
                MaxResourceCount = config.MaxResourceCount,
            });
        }

        foreach (string tenant in config.TenantsR4B)
        {
            tenants.Add(tenant, new()
            {
                FhirVersion = TenantConfiguration.SupportedFhirVersions.R4B,
                ControllerName = tenant,
                BaseUrl = config.PublicUrl + "/fhir/" + tenant,
                ProtectLoadedContent = config.ProtectLoadedContent,
                MaxResourceCount = config.MaxResourceCount,
            });
        }

        foreach (string tenant in config.TenantsR5)
        {
            tenants.Add(tenant, new()
            {
                FhirVersion = TenantConfiguration.SupportedFhirVersions.R5,
                ControllerName = tenant,
                BaseUrl = config.PublicUrl + "/fhir/" + tenant,
                ProtectLoadedContent = config.ProtectLoadedContent,
                MaxResourceCount = config.MaxResourceCount,
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
                string relativeDir = FindRelativeDir(config.SourceDirectory, false);

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
    /// <param name="thowIfNotFound">(Optional) True to thow if not found.</param>
    /// <returns>The found FHIR directory.</returns>
    public static string FindRelativeDir(
        string dirName,
        bool thowIfNotFound = true)
    {
        string currentDir = Path.GetDirectoryName(AppContext.BaseDirectory) ?? string.Empty;
        string testDir = Path.Combine(currentDir, dirName);

        while (!Directory.Exists(testDir))
        {
            currentDir = Path.GetFullPath(Path.Combine(currentDir, ".."));

            if (currentDir == Path.GetPathRoot(currentDir))
            {
                if (thowIfNotFound)
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
