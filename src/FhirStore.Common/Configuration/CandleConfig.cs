// <copyright file="ConfigRoot.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using System;
using System.Collections;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

#if NETSTANDARD2_0
using FhirCandle.Polyfill;
using Microsoft.Extensions.Configuration;
#endif

namespace FhirCandle.Configuration;

public record class CliOptions
{
    public static readonly RootCommand RootCommand = new CliRootCommand();
    public static readonly List<(string, Command)> Commands = [];

    public Option<string?> PublicUrl { get; } = new("--url", "-u")
    {
        Description = "Public URL for the server",
        Arity = ArgumentArity.ZeroOrOne,
    };

    public Option<int?> ListenPort { get; } = new("--port")
    {
        Description = "TCP port to listen on",
        Arity = ArgumentArity.ZeroOrOne,
    };

    public Option<bool?> OpenBrowser { get; } = new("--open-browser", "-o")
    {
        Description = "Open the browser to the public URL once the server starts",
        Arity = ArgumentArity.ZeroOrOne,
        //DefaultValueFactory = (ar) => ar.Implicit == true ? true : (bool?)null,
    };
    
    public Option<int?> MaxResourceCount { get; } = new("--max-resources", "-m")
    {
        Description = "Maximum number of resources allowed per tenant",
        Arity = ArgumentArity.ZeroOrOne,
    };

    public Option<bool?> Headless { get; } = new("--disable-ui", "--headless")
    {
        Description = "Set to disable the UI (run server headless)",
        Arity = ArgumentArity.ZeroOrOne,
    };

    public Option<string?> FhirCacheDirectory { get; } = new("--fhir-package-cache")
    {
        Description = "Location of the FHIR package cache, for use with registries and IG packages. Not specified defaults to ~/.fhir.",
        Arity = ArgumentArity.ZeroOrOne,
    };

    public Option<bool?> UseOfficialRegistries { get; } = new("--use-official-registries")
    {
        Description = "Use official FHIR registries to resolve packages.",
        Arity = ArgumentArity.ZeroOrOne,
    };

    public Option<string[]?> AdditionalFhirRegistryUrls { get; } = new("--additional-fhir-registry-urls")
    {
        Description = "Additional FHIR registry URLs to use.",
        Arity = ArgumentArity.ZeroOrMore,
    };

    public Option<string[]?> AdditionalNpmRegistryUrls { get; } = new("--additional-npm-registry-urls")
    {
        Description = "Additional NPM registry URLs to use.",
        Arity = ArgumentArity.ZeroOrMore,
    };

    public Option<string[]?> PublishedPackages { get; } = new("--load-package", "-p")
    {
        Description = "FHIR package to load on startup, specified by directive. Can be specified multiple times.",
        Arity = ArgumentArity.ZeroOrMore,
    };

    public Option<string[]?> CiPackages { get; } = new("--ci-package")
    {
        Description = "FHIR package to load on startup, specified by directive. Can be specified multiple times.",
        Arity = ArgumentArity.ZeroOrMore,
    };

    public Option<bool?> LoadPackageExamples { get; } = new("--load-examples")
    {
        Description = "If package loading should include example instances",
        Arity = ArgumentArity.ZeroOrOne,
    };

    public Option<string?> ReferenceImplementation { get; } = new("--reference-implementation")
    {
        Description = "If running as the Reference Implementation, the package directive or literal.",
        Arity = ArgumentArity.ZeroOrOne,
    };

    public Option<bool?> EnableMcp { get; } = new("--enable-mcp")
    {
        Description = "True to enable Model Context Protocol (MCP) at [root]/mcp.",
        Arity = ArgumentArity.ZeroOrOne,
    };

    //public Option<string?> SourceRepository { get; } = new("--source-repository")
    //{
    //    Description = "A GitHub repository to load as a source of configuration and data",
    //    Arity = ArgumentArity.ZeroOrOne,
    //};

    //public Option<string?> SourceRepositoryPath { get; } = new("--source-repository-path")
    //{
    //    Description = "A path within the GitHub repository to load as a source of configuration and data",
    //    Arity = ArgumentArity.ZeroOrOne,
    //};

    public Option<string?> SourceDirectory { get; } = new("--fhir-source")
    {
        Description = "FHIR Contents to load, either in this directory or by subdirectories named per tenant.",
        Arity = ArgumentArity.ZeroOrOne,
    };

    public Option<bool?> ProtectLoadedContent { get; } = new("--protect-source", "--protect-loaded-content")
    {
        Description = "If set, loaded content will be protected from modification.",
        Arity = ArgumentArity.ZeroOrOne,
    };

    public Option<string[]?> TenantsR4 { get; } = new("--r4")
    {
        Description = "FHIR R4 Tenants to create. Can be specified multiple times.",
        Arity = ArgumentArity.ZeroOrMore,
    };

    public Option<string[]?> TenantsR4B { get; } = new("--r4b")
    {
        Description = "FHIR R4B Tenants to create. Can be specified multiple times.",
        Arity = ArgumentArity.ZeroOrMore,
    };

    public Option<string[]?> TenantsR5 { get; } = new("--r5")
    {
        Description = "FHIR R5 Tenants to create. Can be specified multiple times.",
        Arity = ArgumentArity.ZeroOrMore,
    };

    public Option<string[]?> TenantsR6 { get; } = new("--r6")
    {
        Description = "FHIR R6 Tenants to create. Can be specified multiple times.",
        Arity = ArgumentArity.ZeroOrMore,
    };

    public Option<string[]?> SmartRequiredTenants { get; } = new("--smart-required")
    {
        Description = "Tenants that require SMART on FHIR support, * for all.",
        Arity = ArgumentArity.ZeroOrMore,
    };

    public Option<string[]?> SmartOptionalTenants { get; } = new("--smart-optional")
    {
        Description = "Tenants that support SMART on FHIR but do not require it, * for all.",
        Arity = ArgumentArity.ZeroOrMore,
    };

    public Option<bool?> SupportNotChanged { get; } = new("--detect-unchanged")
    {
        Description = "True to enable unchanged resource detection",
        Arity = ArgumentArity.ZeroOrOne,
    };

    public Option<bool?> AllowExistingId { get; } = new("--create-existing-id")
    {
        Description = "Allow Create interactions (POST) to specify an ID.",
        Arity = ArgumentArity.ZeroOrOne,
    };

    public Option<bool?> AllowCreateAsUpdate { get; } = new("--create-as-update")
    {
        Description = "Allow Update interactions (PUT) to create new resources.",
        Arity = ArgumentArity.ZeroOrOne,
    };

    public Option<int?> MaxSubscriptionExpirationMinutes { get; } = new("--max-subscription-minutes")
    {
        Description = "Maximum number of minutes a subscription can be active.",
        Arity = ArgumentArity.ZeroOrOne,
    };

    public Option<string?> ZulipEmail { get; } = new("--zulip-email")
    {
        Description = "Zulip bot email address to use for Zulip notifications.",
        Arity = ArgumentArity.ZeroOrOne,
    };

    public Option<string?> ZulipKey { get; } = new("--zulip-key")
    {
        Description = "Zulip bot API key to use for Zulip notifications.",
        Arity = ArgumentArity.ZeroOrOne,
    };

    public Option<string?> ZulipUrl { get; } = new("--zulip-url")
    {
        Description = "Zulip server URL to use for Zulip notifications.",
        Arity = ArgumentArity.ZeroOrOne,
    };

    public Option<string?> SmtpHost { get; } = new("--smtp-host")
    {
        Description = "SMTP host to use for email notifications.",
        Arity = ArgumentArity.ZeroOrOne,
    };

    // default 465
    public Option<int?> SmtpPort { get; } = new("--smtp-port")
    {
        Description = "SMTP port to use for email notifications.",
        Arity = ArgumentArity.ZeroOrOne,
    };

    public Option<string?> SmtpUser { get; } = new("--smtp-user")
    {
        Description = "SMTP user to use for email notifications.",
        Arity = ArgumentArity.ZeroOrOne,
    };

    public Option<string?> SmtpPassword { get; } = new("--smtp-password")
    {
        Description = "SMTP password to use for email notifications.",
        Arity = ArgumentArity.ZeroOrOne,
    };

    public Option<string?> FhirPathLabUrl { get; } = new("--fhirpath-lab-url")
    {
        Description = "FHIRPath Lab URL to use for external FHIRPath tests.",
        Arity = ArgumentArity.ZeroOrOne,
    };


    public Option<string?> OpenTelemetryEndpoint { get; } = new("--otel-otlp-endpoint")
    {
        Description = "Enables OpenTelemetry and sends traces, metrics, and logs via OLTP to the specified endpoint",
        Arity = ArgumentArity.ZeroOrOne,
    };

    public Option<string?> OpenTelemetryProtocol { get; } = new("--otel-otlp-protocol")
    {
        Description = "Specifies the OTLP transport protocol to be used for all telemetry data. Valid values are 'grpc' and 'http/protobuf'",
        Arity = ArgumentArity.ZeroOrOne,
    };

    public Option<string?> OpenTelemetryTracesEndpoint { get; } = new("--otel-otlp-traces-endpoint")
    {
        Description = "Enables OpenTelemetry and sends traces via OLTP to the specified endpoint",
        Arity = ArgumentArity.ZeroOrOne,
    };

    public Option<string?> OpenTelemetryMetricsEndpoint { get; } = new("--otel-otlp-metrics-endpoint")
    {
        Description = "Enables OpenTelemetry and sends metrics via OLTP to the specified endpoint",
        Arity = ArgumentArity.ZeroOrOne,
    };

    public Option<string?> OpenTelemetryLogsEndpoint { get; } = new("--otel-otlp-logs-endpoint")
    {
        Description = "Enables OpenTelemetry and sends logs via OLTP to the specified endpoint",
        Arity = ArgumentArity.ZeroOrOne,
    };
}


/// <summary>Main configuration class for FHIR-Candle.</summary>
public record class CandleConfig
{
    /// <summary>(Immutable) The default listen port.</summary>
    private const int _defaultListenPort = 5826;
    private const int _defaultSmtpPort = 465;
    private const string _defaultOtelProtocol = "grpc";


    [ConfigurationKeyName("Public_Url")]
    public string PublicUrl { get; set; } = string.Empty;

    [ConfigurationKeyName("Listen_Port")]
    public int ListenPort { get; set; } = _defaultListenPort;

    [ConfigurationKeyName("Open_Browser")]
    public bool OpenBrowser { get; set; } = false;


    /// <summary>Gets or sets the maximum number of resources allowed per tenant (0 for no limit).</summary>
    [ConfigurationKeyName("Max_Resources")]
    public int MaxResourceCount { get; set; } = 0;

    [ConfigurationKeyName("Disable_Ui")]
    public bool Headless { get; set; } = false;

    [ConfigurationKeyName("Fhir_Package_Cache")]
    public string? FhirCacheDirectory { get; set; } = null;

    [ConfigurationKeyName("Use_Official_Registries")]
    public bool UseOfficialRegistries { get; set; } = true;

    [ConfigurationKeyName("Additional_FHIR_Registry_Urls")]
    public string[] AdditionalFhirRegistryUrls { get; set; } = Array.Empty<string>();

    [ConfigurationKeyName("Additional_NPM_Registry_Urls")]
    public string[] AdditionalNpmRegistryUrls { get; set; } = Array.Empty<string>();

    [ConfigurationKeyName("Fhir_Package")]
    public string[] PublishedPackages { get; set; } = [];

    [ConfigurationKeyName("Fhir_Ci_Package")]
    public string[] CiPackages { get; set; } = [];

    [ConfigurationKeyName("Load_Examples")]
    public bool LoadPackageExamples { get; set; } = false;

    [ConfigurationKeyName("Reference_Implementation")]
    public string? ReferenceImplementation { get; set; } = null;

    [ConfigurationKeyName("Enable_Mcp")]
    public bool EnableMcp { get; set; } = false;

    //[ConfigurationKeyName("Source_Repository")]
    //public string? SourceRepository { get; set; } = null;

    //[ConfigurationKeyName("Source_Repository_Path")]
    //public string? SourceRepositoryPath { get; set; } = null;

    /// <summary>Gets or sets the pathname of a FHIR source directory to load additional content from.</summary>
    [ConfigurationKeyName("Fhir_Source_Directory")]
    public string? SourceDirectory { get; set; } = null;

    [ConfigurationKeyName("Protect_Source")]
    public bool ProtectLoadedContent { get; set; } = false;

    [ConfigurationKeyName("Tenants_R4")]
    public string[] TenantsR4 { get; set; } = [];

    [ConfigurationKeyName("Tenants_R4B")]
    public string[] TenantsR4B { get; set; } = [];

    [ConfigurationKeyName("Tenants_R5")]
    public string[] TenantsR5 { get; set; } = [];

    [ConfigurationKeyName("Tenants_R6")]
    public string[] TenantsR6 { get; set; } = [];

    [ConfigurationKeyName("Detect_Not_Changed")]
    public bool SupportNotChanged { get; set; } = true;

    /// <summary>Tenants that require SMART on FHIR support.</summary>
    [ConfigurationKeyName("Smart_Required_Tenants")]
    public string[] SmartRequiredTenants { get; set; } = [];

    /// <summary>The tenants that allow SMART authorization.</summary>
    [ConfigurationKeyName("Smart_Optional_Tenants")]
    public string[] SmartOptionalTenants { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether the create interactions can specify an ID.
    /// </summary>
    [ConfigurationKeyName("Create_Existing_Id")]
    public bool AllowExistingId { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the create as update is enabled.
    /// </summary>
    [ConfigurationKeyName("Create_As_Update")]
    public bool AllowCreateAsUpdate { get; set; } = true;

    /// <summary>Gets or sets the maximum number of minutes a subscription can be open.</summary>
    [ConfigurationKeyName("Max_Subscription_Minutes")]
    public int MaxSubscriptionExpirationMinutes { get; set; } = 0;

    [ConfigurationKeyName("Zulip_Email")]
    public string? ZulipEmail { get; set; } = null;

    [ConfigurationKeyName("Zulip_Key")]
    public string? ZulipKey { get; set; } = null;

    [ConfigurationKeyName("Zulip_Url")]
    public string? ZulipUrl { get; set; } = null;

    [ConfigurationKeyName("SMTP_Host")]
    public string? SmtpHost { get; set; } = null;

    [ConfigurationKeyName("SMTP_Port")]
    public int SmtpPort { get; set; } = _defaultSmtpPort;

    [ConfigurationKeyName("SMTP_User")]
    public string? SmtpUser { get; set; } = null;

    [ConfigurationKeyName("SMTP_Password")]
    public string? SmtpPassword { get; set; } = null;

    [ConfigurationKeyName("FhirPath_Lab_Url")]
    public string? FhirPathLabUrl { get; set; } = null;

    [ConfigurationKeyName("OTEL_EXPORTER_OTLP_ENDPOINT")]
    public string? OpenTelemetryEndpoint { get; set; } = null;

    [ConfigurationKeyName("OTEL_EXPORTER_OTLP_PROTOCOL")]
    public string OpenTelemetryProtocol { get; set; } = _defaultOtelProtocol;

    [ConfigurationKeyName("OTEL_EXPORTER_OTLP_TRACES_ENDPOINT")]
    public string? OpenTelemetryTracesEndpoint { get; set; } = null;

    [ConfigurationKeyName("OTEL_EXPORTER_OTLP_METRICS_ENDPOINT")]
    public string? OpenTelemetryMetricsEndpoint { get; set; } = null;

    [ConfigurationKeyName("OTEL_EXPORTER_OTLP_LOGS_ENDPOINT")]
    public string? OpenTelemetryLogsEndpoint { get; set; } = null;


    public IConfiguration Configuration { get; init; }

    public CandleConfig() { Configuration = null!; }

    private int? filterZero(int? value)
    {
        if (value == 0)
        {
            return null;
        }
        return value;
    }

    public CandleConfig(CliOptions opt, ParseResult pr, IConfiguration configuration)
    {
        Configuration = configuration;

        CandleConfig? envConfig = configuration.Get<CandleConfig>((opt) =>
        {
            opt.BindNonPublicProperties = false;
            opt.ErrorOnUnknownConfiguration = false;
        });

        PublicUrl = pr.GetValue(opt.PublicUrl) ?? envConfig?.PublicUrl ?? string.Empty;
        ListenPort = pr.GetValue(opt.ListenPort) ?? filterZero(envConfig?.ListenPort) ?? _defaultListenPort;
        OpenBrowser = pr.GetValue(opt.OpenBrowser) ?? envConfig?.OpenBrowser ?? false;
        MaxResourceCount = pr.GetValue(opt.MaxResourceCount) ?? envConfig?.MaxResourceCount ?? 0;
        Headless = pr.GetValue(opt.Headless) ?? envConfig?.Headless ?? false;

        string? dir = pr.GetValue(opt.FhirCacheDirectory) ?? envConfig?.FhirCacheDirectory;
        if ((dir is not null) && dir.EndsWith(".fhir", StringComparison.Ordinal))
        {
            dir = Path.Combine(dir, "packages");
        }

        if (string.IsNullOrEmpty(dir))
        {
            // Resolve the default path without requiring it to exist yet;
            // FhirPackageService.Init() will create it when needed.
            dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".fhir",
                "packages");
        }
        else if (!Path.IsPathRooted(dir))
        {
            dir = FindRelativeDir(string.Empty, dir);
        }

        FhirCacheDirectory = dir;

        UseOfficialRegistries = pr.GetValue(opt.UseOfficialRegistries) ?? envConfig?.UseOfficialRegistries ?? true;
        AdditionalFhirRegistryUrls = pr.GetValue(opt.AdditionalFhirRegistryUrls) ?? envConfig?.AdditionalFhirRegistryUrls ?? [];
        AdditionalNpmRegistryUrls = pr.GetValue(opt.AdditionalNpmRegistryUrls) ?? envConfig?.AdditionalNpmRegistryUrls ?? [];
        PublishedPackages = pr.GetValue(opt.PublishedPackages) ?? envConfig?.PublishedPackages ?? [];
        CiPackages = pr.GetValue(opt.CiPackages) ?? envConfig?.CiPackages ?? [];
        LoadPackageExamples = pr.GetValue(opt.LoadPackageExamples) ?? envConfig?.LoadPackageExamples ?? false;
        ReferenceImplementation = pr.GetValue(opt.ReferenceImplementation) ?? envConfig?.ReferenceImplementation;
        EnableMcp = pr.GetValue(opt.EnableMcp) ?? envConfig?.EnableMcp ?? false;
        //SourceRepository = pr.GetValue(opt.SourceRepository) ?? envConfig?.SourceRepository;
        //SourceRepositoryPath = pr.GetValue(opt.SourceRepositoryPath) ?? envConfig?.SourceRepositoryPath;

        dir = pr.GetValue(opt.SourceDirectory) ?? envConfig?.SourceDirectory;
        if (string.IsNullOrEmpty(dir))
        {
            SourceDirectory = null;
        }
        else
        {
            SourceDirectory = FindRelativeDir(string.Empty, dir);
        }

        ProtectLoadedContent = pr.GetValue(opt.ProtectLoadedContent) ?? envConfig?.ProtectLoadedContent ?? false;
        TenantsR4 = pr.GetValue(opt.TenantsR4) ?? envConfig?.TenantsR4 ?? [];
        TenantsR4B = pr.GetValue(opt.TenantsR4B) ?? envConfig?.TenantsR4B ?? [];
        TenantsR5 = pr.GetValue(opt.TenantsR5) ?? envConfig?.TenantsR5 ?? [];
        TenantsR6 = pr.GetValue(opt.TenantsR6) ?? envConfig?.TenantsR6 ?? [];
        SupportNotChanged = pr.GetValue(opt.SupportNotChanged) ?? envConfig?.SupportNotChanged ?? false;
        AllowExistingId = pr.GetValue(opt.AllowExistingId) ?? envConfig?.AllowExistingId ?? true;
        AllowCreateAsUpdate = pr.GetValue(opt.AllowCreateAsUpdate) ?? envConfig?.AllowCreateAsUpdate ?? true;
        MaxSubscriptionExpirationMinutes = pr.GetValue(opt.MaxSubscriptionExpirationMinutes) ?? envConfig?.MaxSubscriptionExpirationMinutes ?? 0;

        ZulipEmail = pr.GetValue(opt.ZulipEmail) ?? envConfig?.ZulipEmail;
        ZulipKey = pr.GetValue(opt.ZulipKey) ?? envConfig?.ZulipKey;
        ZulipUrl = pr.GetValue(opt.ZulipUrl) ?? envConfig?.ZulipUrl;
        SmtpHost = pr.GetValue(opt.SmtpHost) ?? envConfig?.SmtpHost;
        SmtpPort = pr.GetValue(opt.SmtpPort) ?? filterZero(envConfig?.SmtpPort) ?? _defaultSmtpPort;
        SmtpUser = pr.GetValue(opt.SmtpUser) ?? envConfig?.SmtpUser;
        SmtpPassword = pr.GetValue(opt.SmtpPassword) ?? envConfig?.SmtpPassword;

        FhirPathLabUrl = pr.GetValue(opt.FhirPathLabUrl) ?? envConfig?.FhirPathLabUrl;

        OpenTelemetryEndpoint = pr.GetValue(opt.OpenTelemetryEndpoint) ?? envConfig?.OpenTelemetryEndpoint;
        OpenTelemetryProtocol = pr.GetValue(opt.OpenTelemetryProtocol) ?? envConfig?.OpenTelemetryProtocol ?? _defaultOtelProtocol;
        OpenTelemetryTracesEndpoint = pr.GetValue(opt.OpenTelemetryTracesEndpoint) ?? envConfig?.OpenTelemetryTracesEndpoint;
        OpenTelemetryMetricsEndpoint = pr.GetValue(opt.OpenTelemetryMetricsEndpoint) ?? envConfig?.OpenTelemetryMetricsEndpoint;
        OpenTelemetryLogsEndpoint = pr.GetValue(opt.OpenTelemetryLogsEndpoint) ?? envConfig?.OpenTelemetryLogsEndpoint;
    }

    ///// <summary>Gets an option.</summary>
    ///// <typeparam name="T">Generic type parameter.</typeparam>
    ///// <param name="parseResult"> The parse result.</param>
    ///// <param name="opt">         The option.</param>
    ///// <param name="defaultValue">The default value.</param>
    ///// <returns>The option.</returns>
    //internal T GetOpt<T>(
    //    ParseResult parseResult,
    //    ParseResult? envParseResult,
    //    Option opt,
    //    T defaultValue)
    //{
    //    ParseResult? pr = parseResult.HasOption(opt)
    //        ? parseResult
    //        : envParseResult?.HasOption(opt) == true
    //        ? envParseResult
    //        : null;

    //    if (pr is null)
    //    {
    //        return defaultValue;
    //    }

    //    object? parsed = pr.GetValueForOption(opt);

    //    if (parsed is System.CommandLine.Parsing.Token t)
    //    {
    //        switch (defaultValue)
    //        {
    //            case bool:
    //                return (T)((object?)Convert.ToBoolean(t.Value) ?? defaultValue);
    //            case int:
    //                return (T)((object?)Convert.ToInt32(t.Value) ?? defaultValue);
    //            case long:
    //                return (T)((object?)Convert.ToInt64(t.Value) ?? defaultValue);
    //            case float:
    //                return (T)((object?)Convert.ToSingle(t.Value) ?? defaultValue);
    //            case double:
    //                return (T)((object?)Convert.ToDouble(t.Value) ?? defaultValue);
    //            case decimal:
    //                return (T)((object?)Convert.ToDecimal(t.Value) ?? defaultValue);
    //            case string:
    //                return (T)((object?)Convert.ToString(t.Value) ?? defaultValue);
    //            default:
    //                {
    //                    if ((t.Value is not null) &&
    //                        (t.Value is T typed))
    //                    {
    //                        return typed;
    //                    }
    //                }
    //                break;
    //        }
    //    }

    //    switch (parsed)
    //    {
    //        case bool:
    //            return (T)((object?)Convert.ToBoolean(parsed) ?? defaultValue!);
    //        case int:
    //            return (T)((object?)Convert.ToInt32(parsed) ?? defaultValue!);
    //        case long:
    //            return (T)((object?)Convert.ToInt64(parsed) ?? defaultValue!);
    //        case float:
    //            return (T)((object?)Convert.ToSingle(parsed) ?? defaultValue!);
    //        case double:
    //            return (T)((object?)Convert.ToDouble(parsed) ?? defaultValue!);
    //        case decimal:
    //            return (T)((object?)Convert.ToDecimal(parsed) ?? defaultValue!);
    //        case string:
    //            return (T)((object?)Convert.ToString(parsed) ?? defaultValue!);
    //        default:
    //            {
    //                if ((parsed is not null) &&
    //                    (parsed is T typed))
    //                {
    //                    return typed;
    //                }
    //            }
    //            break;
    //    }

    //    return defaultValue;
    //}

    ///// <summary>Gets option array.</summary>
    ///// <exception cref="Exception">Thrown when an exception error condition occurs.</exception>
    ///// <typeparam name="T">Generic type parameter.</typeparam>
    ///// <param name="parseResult">    The parse result.</param>
    ///// <param name="opt">            The option.</param>
    ///// <param name="defaultValue">   The default value.</param>
    ///// <param name="singleSplitChar">(Optional) The single split character.</param>
    ///// <returns>An array of t.</returns>
    //internal T[] GetOptArray<T>(
    //    ParseResult parseResult,
    //    ParseResult? envParseResult,
    //    Option opt,
    //    T[] defaultValue,
    //    char? singleSplitChar = null)
    //{
    //    ParseResult? pr = parseResult.HasOption(opt)
    //        ? parseResult
    //        : envParseResult?.HasOption(opt) == true
    //        ? envParseResult
    //        : null;

    //    if (pr is null)
    //    {
    //        return defaultValue;
    //    }

    //    object? parsed = pr.GetValueForOption(opt);

    //    if (parsed is null)
    //    {
    //        return defaultValue;
    //    }

    //    List<T> values = [];

    //    if (parsed is T[] array)
    //    {
    //        if ((array.Length == 1) &&
    //            (singleSplitChar is not null) &&
    //            (array is string[] sA))
    //        {
    //            string[] splitValues = sA.First().Split(singleSplitChar.Value);

    //            values.Clear();
    //            foreach (string v in splitValues)
    //            {
    //                if (v is T tV)
    //                {
    //                    values.Add(tV);
    //                }
    //            }

    //            return [.. values];
    //        }

    //        return array;
    //    }
    //    else if (parsed is IEnumerator genericEnumerator)
    //    {
    //        // use the enumerator to add values to the array
    //        while (genericEnumerator.MoveNext())
    //        {
    //            if (genericEnumerator.Current is T tValue)
    //            {
    //                values.Add(tValue);
    //            }
    //            else
    //            {
    //                throw new Exception("Should not be here!");
    //            }
    //        }
    //    }
    //    else if (parsed is IEnumerator<T> enumerator)
    //    {
    //        // use the enumerator to add values to the array
    //        while (enumerator.MoveNext())
    //        {
    //            values.Add(enumerator.Current);
    //        }
    //    }
    //    else
    //    {
    //        throw new Exception("Should not be here!");
    //    }

    //    // if no values were added, return the default - parser cannot tell the difference between no values and default values
    //    if (values.Count == 0)
    //    {
    //        return defaultValue;
    //    }

    //    if ((values.Count == 1) &&
    //        (singleSplitChar is not null) &&
    //        (values is List<string> stringValues))
    //    {
    //        string[] splitValues = stringValues.First().Split(singleSplitChar.Value);

    //        values.Clear();
    //        foreach (string v in splitValues)
    //        {
    //            if (v is T tV)
    //            {
    //                values.Add(tV);
    //            }
    //        }
    //    }

    //    return [.. values];
    //}

    ///// <summary>Gets option hash.</summary>
    ///// <exception cref="Exception">Thrown when an exception error condition occurs.</exception>
    ///// <typeparam name="T">Generic type parameter.</typeparam>
    ///// <param name="parseResult"> The parse result.</param>
    ///// <param name="opt">         The option.</param>
    ///// <param name="defaultValue">The default value.</param>
    ///// <returns>The option hash.</returns>
    //internal HashSet<T> GetOptHash<T>(
    //    ParseResult parseResult,
    //    ParseResult envParseResult,
    //    Option opt,
    //    HashSet<T> defaultValue)
    //{
    //    ParseResult? pr = parseResult.HasOption(opt)
    //        ? parseResult
    //        : envParseResult.HasOption(opt)
    //        ? envParseResult
    //        : null;

    //    if (pr is null)
    //    {
    //        return defaultValue;
    //    }

    //    object? parsed = pr.GetValueForOption(opt);

    //    if (parsed is null)
    //    {
    //        return defaultValue;
    //    }

    //    HashSet<T> values = [];

    //    if (parsed is IEnumerator<T> typed)
    //    {
    //        // use the enumerator to add values to the array
    //        while (typed.MoveNext())
    //        {
    //            values.Add(typed.Current);
    //        }
    //    }
    //    else
    //    {
    //        throw new Exception("Should not be here!");
    //    }

    //    // if no values were added, return the default - parser cannot tell the difference between no values and default values
    //    if (values.Count == 0)
    //    {
    //        return defaultValue;
    //    }

    //    return values;
    //}

    /// <summary>Searches for the first relative dir.</summary>
    /// <exception cref="DirectoryNotFoundException">Thrown when the requested directory is not
    ///  present.</exception>
    /// <param name="startDir">       The start dir.</param>
    /// <param name="dirName">        Pathname of the directory.</param>
    /// <param name="throwIfNotFound">(Optional) True to throw if not found.</param>
    /// <returns>The found relative dir.</returns>
    internal string FindRelativeDir(
        string startDir,
        string dirName,
        bool throwIfNotFound = true)
    {
        string currentDir;

        if (string.IsNullOrEmpty(startDir))
        {
            if (dirName.StartsWith('~'))
            {
                currentDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

                if (dirName.Length > 1)
                {
                    dirName = dirName[2..];
                }
                else
                {
                    dirName = string.Empty;
                }
            }
            else
            {
                currentDir = Path.GetDirectoryName(AppContext.BaseDirectory) ?? string.Empty;
            }
        }
        else if (startDir.StartsWith('~'))
        {
            // check if the path was only the user dir or the user dir plus a separator
            if ((startDir.Length == 1) || (startDir.Length == 2))
            {
                currentDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
            }
            else
            {
                // skip the separator
                currentDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), startDir[2..]);
            }
        }
        else
        {
            currentDir = startDir;
        }

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

        return Path.GetFullPath(testDir);
    }
}

public class CliRootCommand : RootCommand
{
    private CliOptions _cliOptions = new();
    public CliOptions CommandCliOptions => _cliOptions;

    public CliRootCommand() : base("A lightweight in-memory FHIR server, for when a small FHIR will do")
    {
        Add(_cliOptions.PublicUrl);
        Add(_cliOptions.ListenPort);
        Add(_cliOptions.OpenBrowser);

        Add(_cliOptions.MaxResourceCount);
        Add(_cliOptions.Headless);

        Add(_cliOptions.FhirCacheDirectory);

        Add(_cliOptions.UseOfficialRegistries);
        Add(_cliOptions.AdditionalFhirRegistryUrls);
        Add(_cliOptions.AdditionalNpmRegistryUrls);

        Add(_cliOptions.PublishedPackages);
        Add(_cliOptions.CiPackages);
        Add(_cliOptions.LoadPackageExamples);

        Add(_cliOptions.ReferenceImplementation);

        Add(_cliOptions.EnableMcp);

        //Add(_cliOptions.SourceRepository);
        //Add(_cliOptions.SourceRepositoryPath);
        Add(_cliOptions.SourceDirectory);
        Add(_cliOptions.ProtectLoadedContent);

        Add(_cliOptions.TenantsR4);
        Add(_cliOptions.TenantsR4B);
        Add(_cliOptions.TenantsR5);
        Add(_cliOptions.TenantsR6);

        Add(_cliOptions.SmartRequiredTenants);
        Add(_cliOptions.SmartOptionalTenants);

        Add(_cliOptions.SupportNotChanged);
        Add(_cliOptions.AllowExistingId);
        Add(_cliOptions.AllowCreateAsUpdate);

        Add(_cliOptions.MaxSubscriptionExpirationMinutes);
        Add(_cliOptions.ZulipEmail);
        Add(_cliOptions.ZulipKey);
        Add(_cliOptions.ZulipUrl);
        Add(_cliOptions.SmtpHost);
        Add(_cliOptions.SmtpPort);
        Add(_cliOptions.SmtpUser);
        Add(_cliOptions.SmtpPassword);

        Add(_cliOptions.FhirPathLabUrl);

        Add(_cliOptions.OpenTelemetryEndpoint);
        Add(_cliOptions.OpenTelemetryProtocol);
        Add(_cliOptions.OpenTelemetryTracesEndpoint);
        Add(_cliOptions.OpenTelemetryMetricsEndpoint);
        Add(_cliOptions.OpenTelemetryLogsEndpoint);
    }
}
