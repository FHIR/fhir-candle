// <copyright file="ConfigRoot.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using System;
using System.Collections;
using System.Collections.Generic;
using System.CommandLine.Parsing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#if NETSTANDARD2_0
using FhirCandle.Polyfill;
using Microsoft.Extensions.Configuration;
#endif

namespace FhirCandle.Configuration;

/// <summary>Main configuration class for FHIR-Candle.</summary>
public class CandleConfig
{
    /// <summary>(Immutable) The default listen port.</summary>
    private const int _defaultListenPort = 5826;

    /// <summary>Gets or sets URL of the public.</summary>
    [ConfigOption(
        ArgAliases = ["--url", "-u"],
        EnvName = "Public_Url",
        Description = "Public URL for the server")]
    public string PublicUrl { get; set; } = string.Empty;

    /// <summary>Gets the public URL option.</summary>
    private static ConfigurationOption PublicUrlParameter { get; } = new()
    {
        Name = "PublicUrl",
        EnvVarName = "Public_Url",
        DefaultValue = string.Empty,
        CliOption = new System.CommandLine.Option<string?>(["--url", "-u"], "Public URL for the server")
        {
            Arity = System.CommandLine.ArgumentArity.ZeroOrOne,
            IsRequired = false,
        },
    };

    /// <summary>Gets or sets the listen port.</summary>
    [ConfigOption(
        ArgName = "--port",
        EnvName = "Listen_Port",
        Description = "TCP port to listen on")]
    public int ListenPort { get; set; } = _defaultListenPort;

    /// <summary>Gets the listen port option.</summary>
    private static ConfigurationOption ListenPortParameter { get; } = new()
    {
        Name = "ListenPort",
        EnvVarName = "Listen_Port",
        DefaultValue = _defaultListenPort,
        CliOption = new System.CommandLine.Option<int?>("--port", "TCP port to listen on")
        {
            Arity = System.CommandLine.ArgumentArity.ZeroOrOne,
            IsRequired = false,
        },
    };

    /// <summary>Gets or sets a value indicating whether the browser should be opened.</summary>
    [ConfigOption(
        ArgAliases = ["--open-browser", "-o"],
        EnvName = "Open_Browser",
        Description = "Open the browser to the public URL once the server starts")]
    public bool OpenBrowser { get; set; } = false;

    /// <summary>Gets the open browser option.</summary>
    private static ConfigurationOption OpenBrowserParameter { get; } = new()
    {
        Name = "OpenBrowser",
        EnvVarName = "Open_Browser",
        DefaultValue = false,
        CliOption = new System.CommandLine.Option<bool>(["--open-browser", "-o"], "Open the browser to the public URL once the server starts")
        {
            Arity = System.CommandLine.ArgumentArity.ZeroOrOne,
            IsRequired = false,
        },
    };

    /// <summary>Gets or sets the maximum resources.</summary>
    [ConfigOption(
        ArgAliases = ["--max-resources", "-m"],
        EnvName = "Max_Resources",
        Description = "Maximum number of resources allowed per tenant")]
    public int MaxResourceCount { get; set; } = 0;

    /// <summary>Gets the maximum resources option.</summary>
    private static ConfigurationOption MaxResourceCountParameter { get; } = new()
    {
        Name = "MaxResources",
        EnvVarName = "Max_Resources",
        DefaultValue = 0,
        CliOption = new System.CommandLine.Option<int?>(["--max-resources", "-m"], "Maximum number of resources allowed per tenant")
        {
            Arity = System.CommandLine.ArgumentArity.ZeroOrOne,
            IsRequired = false,
        },
    };

    /// <summary>
    /// Gets or sets a value indicating whether the user interface is disabled.
    /// </summary>
    [ConfigOption(
        ArgName = "--disable-ui",
        EnvName = "Disable_Ui",
        Description = "Set to disable the UI (run server headless)")]
    public bool DisableUi { get; set; } = false;

    /// <summary>Gets the disable user interface option.</summary>
    private static ConfigurationOption DisableUiParameter { get; } = new()
    {
        Name = "DisableUi",
        EnvVarName = "Disable_Ui",
        DefaultValue = false,
        CliOption = new System.CommandLine.Option<bool>("--disable-ui", "Set to disable the UI (run server headless)")
        {
            Arity = System.CommandLine.ArgumentArity.ZeroOrOne,
            IsRequired = false,
        },
    };

    /// <summary>Gets or sets the pathname of the FHIR package cache directory.</summary>
    [ConfigOption(
        ArgName = "--fhir-package-cache",
        EnvName = "Fhir_Package_Cache",
        Description = "Location of the FHIR package cache, for use with registries and IG packages. Not specified defaults to ~/.fhir.")]
    public string? FhirCacheDirectory { get; set; } = null;

    /// <summary>Gets the FHIR package cache directory option.</summary>
    private static ConfigurationOption FhirCacheDirectoryParameter { get; } = new()
    {
        Name = "FhirPackageCache",
        EnvVarName = "Fhir_Package_Cache",
        DefaultValue = string.Empty,
        CliOption = new System.CommandLine.Option<string?>("--fhir-package-cache", "Location of the FHIR package cache, for use with registries and IG packages. Not specified defaults to ~/.fhir.")
        {
            Arity = System.CommandLine.ArgumentArity.ZeroOrOne,
            IsRequired = false,
        },
    };

    [ConfigOption(
        ArgName = "--use-official-registries",
        EnvName = "Use_Official_Registries",
        Description = "Use official FHIR registries to resolve packages.")]
    public bool UseOfficialRegistries { get; set; } = true;

    private static ConfigurationOption UseOfficialRegistriesParameter { get; } = new()
    {
        Name = "UseOfficialRegistries",
        EnvVarName = "Use_Official_Registries",
        DefaultValue = true,
        CliOption = new System.CommandLine.Option<bool>("--use-official-registries", "Use official FHIR registries to resolve packages.")
        {
            Arity = System.CommandLine.ArgumentArity.ZeroOrOne,
            IsRequired = false,
        },
    };

    [ConfigOption(
        ArgName = "--additional-fhir-registry-urls",
        EnvName = "Additional_FHIR_Registry_Urls",
        ArgArity = "0..*",
        Description = "Additional FHIR registry URLs to use.")]
    public string[] AdditionalFhirRegistryUrls { get; set; } = Array.Empty<string>();

    private static ConfigurationOption AdditionalFhirRegistryUrlsParameter { get; } = new()
    {
        Name = "AdditionalFhirRegistryUrls",
        EnvVarName = "Additional_FHIR_Registry_Urls",
        DefaultValue = Array.Empty<string>(),
        CliOption = new System.CommandLine.Option<string[]>("--additional-fhir-registry-urls", "Additional FHIR registry URLs to use.")
        {
            Arity = System.CommandLine.ArgumentArity.ZeroOrMore,
            IsRequired = false,
        },
    };

    [ConfigOption(
    ArgName = "--additional-npm-registry-urls",
    EnvName = "Additional_NPM_Registry_Urls",
    ArgArity = "0..*",
    Description = "Additional NPM registry URLs to use.")]
    public string[] AdditionalNpmRegistryUrls { get; set; } = Array.Empty<string>();

    private static ConfigurationOption AdditionalNpmRegistryUrlsParameter { get; } = new()
    {
        Name = "AdditionalNpmRegistryUrls",
        EnvVarName = "Additional_NPM_Registry_Urls",
        DefaultValue = Array.Empty<string>(),
        CliOption = new System.CommandLine.Option<string[]>("--additional-npm-registry-urls", "Additional NPM registry URLs to use.")
        {
            Arity = System.CommandLine.ArgumentArity.ZeroOrMore,
            IsRequired = false,
        },
    };

    /// <summary>Gets or sets the FHIR packages.</summary>
    [ConfigOption(
        ArgAliases = ["--load-package", "-p"],
        EnvName = "Fhir_Package",
        Description = "FHIR package to load on startup, specified by directive. Can be specified multiple times.")]
    public string[] PublishedPackages { get; set; } = [];

    /// <summary>Gets the FHIR packages option.</summary>
    private static ConfigurationOption PublishedPackagesParameter { get; } = new()
    {
        Name = "FhirPackages",
        EnvVarName = "Fhir_Package",
        DefaultValue = Array.Empty<string>(),
        CliOption = new System.CommandLine.Option<string[]>(["--load-package", "-p"], "FHIR package to load on startup, specified by directive. Can be specified multiple times.")
        {
            Arity = System.CommandLine.ArgumentArity.ZeroOrMore,
            IsRequired = false,
        },
    };

    /// <summary>Gets or sets the FHIR ci packages.</summary>
    [ConfigOption(
        ArgName = "--ci-package",
        EnvName = "Fhir_Ci_Package",
        Description = "FHIR package to load on startup, specified by directive. Can be specified multiple times.")]
    public string[] CiPackages { get; set; } = [];

    /// <summary>Gets the FHIR ci packages option.</summary>
    private static ConfigurationOption CiPackagesParameter { get; } = new()
    {
        Name = "FhirCiPackages",
        EnvVarName = "Fhir_Ci_Package",
        DefaultValue = Array.Empty<string>(),
        CliOption = new System.CommandLine.Option<string[]>("--ci-package", "FHIR package to load on startup, specified by directive. Can be specified multiple times.")
        {
            Arity = System.CommandLine.ArgumentArity.ZeroOrMore,
            IsRequired = false,
        },
    };

    /// <summary>Gets or sets a value indicating whether the examples should be loaded.</summary>
    [ConfigOption(
        ArgName = "--load-examples",
        EnvName = "Load_Examples",
        Description = "If package loading should include example instances")]
    public bool LoadPackageExamples { get; set; } = false;

    /// <summary>Gets the load examples option.</summary>
    private static ConfigurationOption LoadPackageExamplesParameter { get; } = new()
    {
        Name = "LoadExamples",
        EnvVarName = "Load_Examples",
        DefaultValue = false,
        CliOption = new System.CommandLine.Option<bool>("--load-examples", "If package loading should include example instances")
        {
            Arity = System.CommandLine.ArgumentArity.ZeroOrOne,
            IsRequired = false,
        },
    };

    /// <summary>Gets or sets the reference implementation.</summary>
    [ConfigOption(
        ArgName = "--reference-implementation",
        EnvName = "Reference_Implementation",
        Description = "If running as the Reference Implementation, the package directive or literal.")]
    public string? ReferenceImplementation { get; set; } = null;

    /// <summary>Gets the reference implementation option.</summary>
    private static ConfigurationOption ReferenceImplementationParameter { get; } = new()
    {
        Name = "ReferenceImplementation",
        EnvVarName = "Reference_Implementation",
        DefaultValue = string.Empty,
        CliOption = new System.CommandLine.Option<string?>("--reference-implementation", "If running as the Reference Implementation, the package directive or literal.")
        {
            Arity = System.CommandLine.ArgumentArity.ZeroOrOne,
            IsRequired = false,
        },
    };

    //[ConfigOption(
    //    ArgName = "--source-repository",
    //    EnvName = "Source_Repository",
    //    Description = "A GitHub repository to load as a source of configuration and data.")]
    //public string? SourceRepository { get; set; } = null;
    //public static ConfigurationOption SourceRepositoryParameter { get; } = new()
    //{
    //    Name = "SourceRepository",
    //    EnvVarName = "Source_Repository",
    //    DefaultValue = string.Empty,
    //    CliOption = new System.CommandLine.Option<string?>("--source-repository", "A GitHub repository to load as a source of configuration and data.")
    //    {
    //        Arity = System.CommandLine.ArgumentArity.ZeroOrOne,
    //        IsRequired = false,
    //    },
    //};

    //[ConfigOption(
    //    ArgName = "--source-repository-path",
    //    EnvName = "Source_Repository_Path",
    //    Description = "A path within the GitHub repository to load as a source of configuration and data.")]
    //public string? SourceRepositoryPath { get; set; } = null;
    //public static ConfigurationOption SourceRepositoryPathParameter { get; } = new()
    //{
    //    Name = "SourceRepositoryPath",
    //    EnvVarName = "Source_Repository_Path",
    //    DefaultValue = string.Empty,
    //    CliOption = new System.CommandLine.Option<string?>("--source-repository-path", "A path within the GitHub repository to load as a source of configuration and data.")
    //    {
    //        Arity = System.CommandLine.ArgumentArity.ZeroOrOne,
    //        IsRequired = false,
    //    },
    //};

    /// <summary>Gets or sets the pathname of the FHIR source directory.</summary>
    [ConfigOption(
        ArgName = "--fhir-source",
        EnvName = "Fhir_Source_Directory",
        Description = "FHIR Contents to load, either in this directory or by subdirectories named per tenant.")]
    public string? SourceDirectory { get; set; } = null;

    /// <summary>Gets the FHIR source directory option.</summary>
    private static ConfigurationOption SourceDirectoryParameter { get; } = new()
    {
        Name = "FhirSourceDirectory",
        EnvVarName = "Fhir_Source_Directory",
        DefaultValue = string.Empty,
        CliOption = new System.CommandLine.Option<string?>("--fhir-source", "FHIR Contents to load, either in this directory or by subdirectories named per tenant.")
        {
            Arity = System.CommandLine.ArgumentArity.ZeroOrOne,
            IsRequired = false,
        },
    };

    /// <summary>Gets or sets a value indicating whether the protect loaded content.</summary>
    [ConfigOption(
        ArgAliases = ["--protect-source", "--protect-loaded-content"],
        EnvName = "Protect_Source",
        Description = "If set, loaded content will be protected from modification.")]
    public bool ProtectLoadedContent { get; set; } = false;

    /// <summary>Gets the protect loaded content option.</summary>
    private static ConfigurationOption ProtectLoadedContentParameter { get; } = new()
    {
        Name = "ProtectLoadedContent",
        EnvVarName = "Protect_Source",
        DefaultValue = false,
        CliOption = new System.CommandLine.Option<bool>(["--protect-source", "--protect-loaded-content"], "If set, loaded content will be protected from modification.")
        {
            Arity = System.CommandLine.ArgumentArity.ZeroOrOne,
            IsRequired = false,
        },
    };

    /// <summary>The FHIR R4 tenants.</summary>
    [ConfigOption(
        ArgName = "--r4",
        EnvName = "Tenants_R4",
        Description = "FHIR R4 Tenants to create. Can be specified multiple times.")]
    public string[] TenantsR4 = [];

    /// <summary>Gets the FHIR R4 tenants parameter.</summary>
    private static ConfigurationOption TenantsR4Parameter { get; } = new()
    {
        Name = "TenantsR4",
        EnvVarName = "Tenants_R4",
        DefaultValue = Array.Empty<string>(),
        CliOption = new System.CommandLine.Option<string[]>("--r4", "FHIR R4 Tenants to create. Can be specified multiple times.")
        {
            Arity = System.CommandLine.ArgumentArity.ZeroOrMore,
            IsRequired = false,
        },
    };

    /// <summary>The FHIR R4B tenants.</summary>
    [ConfigOption(
        ArgName = "--r4b",
        EnvName = "Tenants_R4B",
        Description = "FHIR R4B Tenants to create. Can be specified multiple times.")]
    public string[] TenantsR4B = [];

    /// <summary>Gets the FHIR R4B tenants parameter.</summary>
    private static ConfigurationOption TenantsR4BParameter { get; } = new()
    {
        Name = "TenantsR4B",
        EnvVarName = "Tenants_R4B",
        DefaultValue = Array.Empty<string>(),
        CliOption = new System.CommandLine.Option<string[]>("--r4b", "FHIR R4B Tenants to create. Can be specified multiple times.")
        {
            Arity = System.CommandLine.ArgumentArity.ZeroOrMore,
            IsRequired = false,
        },
    };

    /// <summary>The FHIR R5 tenants.</summary>
    [ConfigOption(
        ArgName = "--r5",
        EnvName = "Tenants_R5",
        Description = "FHIR R5 Tenants to create. Can be specified multiple times.")]
    public string[] TenantsR5 = [];

    /// <summary>Gets the FHIR R4 tenants parameter.</summary>
    private static ConfigurationOption TenantsR5Parameter { get; } = new()
    {
        Name = "TenantsR5",
        EnvVarName = "Tenants_R5",
        DefaultValue = Array.Empty<string>(),
        CliOption = new System.CommandLine.Option<string[]>("--r5", "FHIR R5 Tenants to create. Can be specified multiple times.")
        {
            Arity = System.CommandLine.ArgumentArity.ZeroOrMore,
            IsRequired = false,
        },
    };

    /// <summary>The tenants that will require SMART authorization.</summary>
    [ConfigOption(
        ArgName = "--support-not-changed",
        EnvName = "Support_Not_Changed",
        Description = "When enabled, the server will support checking if the resource is changed.")]
    public bool SupportNotChanged = true;

    /// <summary>The smart required tenants.</summary>
    [ConfigOption(
        ArgName = "--smart-required",
        EnvName = "Smart_Required_Tenants",
        Description = "Tenants that require SMART on FHIR support, * for all.")]
    public string[] SmartRequiredTenants = [];

    /// <summary>Gets the tenants that will require SMART authorization parameter.</summary>
    private static ConfigurationOption SmartRequiredTenantsParameter { get; } = new()
    {
        Name = "SmartRequiredTenants",
        EnvVarName = "Smart_Required_Tenants",
        DefaultValue = Array.Empty<string>(),
        CliOption = new System.CommandLine.Option<string[]>("--smart-required", "Tenants that require SMART on FHIR support, * for all.")
        {
            Arity = System.CommandLine.ArgumentArity.ZeroOrMore,
            IsRequired = false,
        },
    };

    /// <summary>The tenants that allow SMART authorization.</summary>
    [ConfigOption(
        ArgName = "--smart-optional",
        EnvName = "Smart_Optional_Tenants",
        Description = "Tenants that support SMART on FHIR but do not require it, * for all.")]
    public string[] SmartOptionalTenants = [];

    /// <summary>Gets the tenants that allo SMART authorization parameter.</summary>
    private static ConfigurationOption SmartOptionalTenantsParameter { get; } = new()
    {
        Name = "SmartOptionalTenants",
        EnvVarName = "Smart_Optional_Tenants",
        DefaultValue = Array.Empty<string>(),
        CliOption = new System.CommandLine.Option<string[]>("--smart-optional", "Tenants that support SMART on FHIR but do not require it, * for all.")
        {
            Arity = System.CommandLine.ArgumentArity.ZeroOrMore,
            IsRequired = false,
        },
    };

    /// <summary>
    /// Gets or sets a value indicating whether the create existing identifier is enabled.
    /// </summary>
    [ConfigOption(
        ArgName = "--create-existing-id",
        EnvName = "Create_Existing_Id",
        Description = "Allow Create interactions (POST) to specify an ID.")]
    public bool AllowExistingId { get; set; } = true;

    /// <summary>Gets the enable create existing identifier option.</summary>
    private static ConfigurationOption AllowExistingIdParameter { get; } = new()
    {
        Name = "CreateExistingId",
        EnvVarName = "Create_Existing_Id",
        DefaultValue = true,
        CliOption = new System.CommandLine.Option<bool>("--create-existing-id", "Allow Create interactions (POST) to specify an ID.")
        {
            Arity = System.CommandLine.ArgumentArity.ZeroOrOne,
            IsRequired = false,
        },
    };

    /// <summary>
    /// Gets or sets a value indicating whether the create as update is enabled.
    /// </summary>
    [ConfigOption(
        ArgName = "--create-as-update",
        EnvName = "Create_As_Update",
        Description = "Allow Update interactions (PUT) to create new resources.")]
    public bool AllowCreateAsUpdate { get; set; } = true;

    /// <summary>Gets the enable create as update option.</summary>
    private static ConfigurationOption AllowCreateAsUpdateParameter { get; } = new()
    {
        Name = "CreateAsUpdate",
        EnvVarName = "Create_As_Update",
        DefaultValue = true,
        CliOption = new System.CommandLine.Option<bool>("--create-as-update", "Allow Update interactions (PUT) to create new resources.")
        {
            Arity = System.CommandLine.ArgumentArity.ZeroOrOne,
            IsRequired = false,
        },
    };

    /// <summary>Gets or sets the maximum subscription minutes.</summary>
    [ConfigOption(
        ArgName = "--max-subscription-minutes",
        EnvName = "Max_Subscription_Minutes",
        Description = "Maximum number of minutes a subscription can be active.")]
    public int MaxSubscriptionExpirationMinutes { get; set; } = 0;

    /// <summary>Gets the maximum subscription minutes option.</summary>
    private static ConfigurationOption MaxSubscriptionExpirationMinutesParameter { get; } = new()
    {
        Name = "MaxSubscriptionMinutes",
        EnvVarName = "Max_Subscription_Minutes",
        DefaultValue = 0,
        CliOption = new System.CommandLine.Option<int?>("--max-subscription-minutes", "Maximum number of minutes a subscription can be active.")
        {
            Arity = System.CommandLine.ArgumentArity.ZeroOrOne,
            IsRequired = false,
        },
    };


    /// <summary>Gets or sets the zulip email.</summary>
    [ConfigOption(
        ArgName = "--zulip-email",
        EnvName = "Zulip_Email",
        Description = "Zulip bot email address to use for Zulip notifications.")]
    public string? ZulipEmail { get; set; } = null;

    /// <summary>Gets the zulip email option.</summary>
    private static ConfigurationOption ZulipEmailParameter { get; } = new()
    {
        Name = "ZulipEmail",
        EnvVarName = "Zulip_Email",
        DefaultValue = string.Empty,
        CliOption = new System.CommandLine.Option<string?>("--zulip-email", "Zulip bot email address to use for Zulip notifications.")
        {
            Arity = System.CommandLine.ArgumentArity.ZeroOrOne,
            IsRequired = false,
        },
    };

    /// <summary>Gets or sets the zulip key.</summary>
    [ConfigOption(
        ArgName = "--zulip-key",
        EnvName = "Zulip_Key",
        Description = "Zulip bot API key to use for Zulip notifications.")]
    public string? ZulipKey { get; set; } = null;

    /// <summary>Gets the zulip key option.</summary>
    private static ConfigurationOption ZulipKeyParameter { get; } = new()
    {
        Name = "ZulipKey",
        EnvVarName = "Zulip_Key",
        DefaultValue = string.Empty,
        CliOption = new System.CommandLine.Option<string?>("--zulip-key", "Zulip bot API key to use for Zulip notifications.")
        {
            Arity = System.CommandLine.ArgumentArity.ZeroOrOne,
            IsRequired = false,
        },
    };

    /// <summary>Gets or sets URL of the zulip.</summary>
    [ConfigOption(
        ArgName = "--zulip-url",
        EnvName = "Zulip_Url",
        Description = "Zulip server URL to use for Zulip notifications.")]
    public string? ZulipUrl { get; set; } = null;

    /// <summary>Gets the zulip URL option.</summary>
    private static ConfigurationOption ZulipUrlParameter { get; } = new()
    {
        Name = "ZulipUrl",
        EnvVarName = "Zulip_Url",
        DefaultValue = string.Empty,
        CliOption = new System.CommandLine.Option<string?>("--zulip-url", "Zulip server URL to use for Zulip notifications.")
        {
            Arity = System.CommandLine.ArgumentArity.ZeroOrOne,
            IsRequired = false,
        },
    };

    /// <summary>Gets or sets the SMTP host.</summary>
    [ConfigOption(
        ArgName = "--smtp-host",
        EnvName = "SMTP_Host",
        Description = "SMTP host to use for email notifications.")]
    public string? SmtpHost { get; set; } = null;

    /// <summary>Gets the SMTP host option.</summary>
    private static ConfigurationOption SmtpHostParameter { get; } = new()
    {
        Name = "SmtpHost",
        EnvVarName = "SMTP_Host",
        DefaultValue = string.Empty,
        CliOption = new System.CommandLine.Option<string?>("--smtp-host", "SMTP host to use for email notifications.")
        {
            Arity = System.CommandLine.ArgumentArity.ZeroOrOne,
            IsRequired = false,
        },
    };

    /// <summary>Gets or sets the SMTP port.</summary>
    [ConfigOption(
        ArgName = "--smtp-port",
        EnvName = "SMTP_Port",
        Description = "SMTP port to use for email notifications.")]
    public int SmtpPort { get; set; } = 465;

    /// <summary>Gets the SMTP port option.</summary>
    private static ConfigurationOption SmtpPortParameter { get; } = new()
    {
        Name = "SmtpPort",
        EnvVarName = "SMTP_Port",
        DefaultValue = 465,
        CliOption = new System.CommandLine.Option<int?>("--smtp-port", "SMTP port to use for email notifications.")
        {
            Arity = System.CommandLine.ArgumentArity.ZeroOrOne,
            IsRequired = false,
        },
    };

    /// <summary>Gets or sets the SMTP user.</summary>
    [ConfigOption(
        ArgName = "--smtp-user",
        EnvName = "SMTP_User",
        Description = "SMTP user to use for email notifications.")]
    public string? SmtpUser { get; set; } = null;

    /// <summary>Gets the SMTP user option.</summary>
    private static ConfigurationOption SmtpUserParameter { get; } = new()
    {
        Name = "SmtpUser",
        EnvVarName = "SMTP_User",
        DefaultValue = string.Empty,
        CliOption = new System.CommandLine.Option<string?>("--smtp-user", "SMTP user to use for email notifications.")
        {
            Arity = System.CommandLine.ArgumentArity.ZeroOrOne,
            IsRequired = false,
        },
    };

    /// <summary>Gets or sets the SMTP password.</summary>
    [ConfigOption(
        ArgName = "--smtp-password",
        EnvName = "SMTP_Password",
        Description = "SMTP password to use for email notifications.")]
    public string? SmtpPassword { get; set; } = null;

    /// <summary>Gets the SMTP password option.</summary>
    private static ConfigurationOption SmtpPasswordParameter { get; } = new()
    {
        Name = "SmtpPassword",
        EnvVarName = "SMTP_Password",
        DefaultValue = string.Empty,
        CliOption = new System.CommandLine.Option<string?>("--smtp-password", "SMTP password to use for email notifications.")
        {
            Arity = System.CommandLine.ArgumentArity.ZeroOrOne,
            IsRequired = false,
        },
    };

    /// <summary>Gets or sets URL of the FHIR path lab.</summary>
    [ConfigOption(
        ArgName = "--fhirpath-lab-url",
        EnvName = "FhirPath_Lab_Url",
        Description = "FHIRPath Lab URL to use for external FHIRPath tests.")]
    public string? FhirPathLabUrl { get; set; } = null;

    /// <summary>Gets the FHIR path lab URL option.</summary>
    private static ConfigurationOption FhirPathLabUrlParameter { get; } = new()
    {
        Name = "FhirPathLabUrl",
        EnvVarName = "FhirPath_Lab_Url",
        DefaultValue = string.Empty,
        CliOption = new System.CommandLine.Option<string?>("--fhirpath-lab-url", "FHIRPath Lab URL to use for external FHIRPath tests.")
        {
            Arity = System.CommandLine.ArgumentArity.ZeroOrOne,
            IsRequired = false,
        },
    };

    [ConfigOption(
        ArgName = "--otel-otlp-endpoint",
        EnvName = "OTEL_EXPORTER_OTLP_ENDPOINT",
        Description = "Enables OpenTelemetry and sends traces, metrics, and logs via OLTP to the specified endpoint")]
    public string? OpenTelemetryEndpoint { get; set; } = null;

    private static ConfigurationOption OpenTelemetryEndpointParameter { get; } = new()
    {
        Name = "OpenTelemetryProtocolEndpoint",
        EnvVarName = "OTEL_EXPORTER_OTLP_ENDPOINT",
        DefaultValue = string.Empty,
        CliOption = new System.CommandLine.Option<string>("--otel-otlp-endpoint", "Enables OpenTelemetry and sends traces, metrics, and logs via OLTP to the specified endpoint")
        {
            Arity = System.CommandLine.ArgumentArity.ZeroOrOne,
            IsRequired = false,
        },
    };

    [ConfigOption(
    ArgName = "--otel-otlp-protocol",
    EnvName = "OTEL_EXPORTER_OTLP_PROTOCOL",
    Description = "Specifies the OTLP transport protocol to be used for all telemetry data. Valid values are 'grpc' and 'http/protobuf'.")]
    public string OpenTelemetryProtocol { get; set; } = "grpc";

    private static ConfigurationOption OpenTelemetryProtocolParameter { get; } = new()
    {
        Name = "OpenTelemetryProtocol",
        EnvVarName = "OTEL_EXPORTER_OTLP_PROTOCOL",
        DefaultValue = "grpc",
        CliOption = new System.CommandLine.Option<string>("--otel-otlp-protocol", "Specifies the OTLP transport protocol to be used for all telemetry data. Valid values are 'grpc' and 'http/protobuf'. ")
        {
            Arity = System.CommandLine.ArgumentArity.ZeroOrOne,
            IsRequired = false,
        },
    };

    [ConfigOption(
        ArgName = "--otel-otlp-traces-endpoint",
        EnvName = "OTEL_EXPORTER_OTLP_TRACES_ENDPOINT",
        Description = "Enables OpenTelemetry and sends traces via OLTP to the specified endpoint")]
    public string? OpenTelemetryTracesEndpoint { get; set; } = null;

    private static ConfigurationOption OpenTelemetryTracesEndpointParameter { get; } = new()
    {
        Name = "OpenTelemetryProtocolTracesEndpoint",
        EnvVarName = "OTEL_EXPORTER_OTLP_TRACES_ENDPOINT",
        DefaultValue = string.Empty,
        CliOption = new System.CommandLine.Option<string>("--otel-otlp-traces-endpoint", "Enables OpenTelemetry and sends traces via OLTP to the specified endpoint")
        {
            Arity = System.CommandLine.ArgumentArity.ZeroOrOne,
            IsRequired = false,
        },
    };

    [ConfigOption(
        ArgName = "--otel-otlp-metrics-endpoint",
        EnvName = "OTEL_EXPORTER_OTLP_METRICS_ENDPOINT",
        Description = "Enables OpenTelemetry and sends metrics via OLTP to the specified endpoint")]
    public string? OpenTelemetryMetricsEndpoint { get; set; } = null;

    private static ConfigurationOption OpenTelemetryMetricsEndpointParameter { get; } = new()
    {
        Name = "OpenTelemetryProtocolMetricsEndpoint",
        EnvVarName = "OTEL_EXPORTER_OTLP_METRICS_ENDPOINT",
        DefaultValue = string.Empty,
        CliOption = new System.CommandLine.Option<string>("--otel-otlp-metrics-endpoint", "Enables OpenTelemetry and sends metrics via OLTP to the specified endpoint")
        {
            Arity = System.CommandLine.ArgumentArity.ZeroOrOne,
            IsRequired = false,
        },
    };

    [ConfigOption(
        ArgName = "--otel-otlp-logs-endpoint",
        EnvName = "OTEL_EXPORTER_OTLP_LOGS_ENDPOINT",
        Description = "Enables OpenTelemetry and sends logs via OLTP to the specified endpoint")]
    public string? OpenTelemetryLogsEndpoint { get; set; } = null;

    private static ConfigurationOption OpenTelemetryLogsEndpointParameter { get; } = new()
    {
        Name = "OpenTelemetryProtocolLogsEndpoint",
        EnvVarName = "OTEL_EXPORTER_OTLP_LOGS_ENDPOINT",
        DefaultValue = string.Empty,
        CliOption = new System.CommandLine.Option<string>("--otel-otlp-logs-endpoint", "Enables OpenTelemetry and sends logs via OLTP to the specified endpoint")
        {
            Arity = System.CommandLine.ArgumentArity.ZeroOrOne,
            IsRequired = false,
        },
    };

    /// <summary>(Immutable) Options for controlling the operation.</summary>
    private static readonly ConfigurationOption[] _options =
    [
        PublicUrlParameter,
        ListenPortParameter,
        OpenBrowserParameter,
        MaxResourceCountParameter,
        DisableUiParameter,
        FhirCacheDirectoryParameter,
        UseOfficialRegistriesParameter,
        AdditionalFhirRegistryUrlsParameter,
        AdditionalNpmRegistryUrlsParameter,
        PublishedPackagesParameter,
        CiPackagesParameter,
        LoadPackageExamplesParameter,
        ReferenceImplementationParameter,
        //SourceRepositoryParameter,
        //SourceRepositoryPathParameter,
        SourceDirectoryParameter,
        ProtectLoadedContentParameter,
        TenantsR4Parameter,
        TenantsR4BParameter,
        TenantsR5Parameter,
        SmartRequiredTenantsParameter,
        SmartOptionalTenantsParameter,
        AllowExistingIdParameter,
        AllowCreateAsUpdateParameter,
        MaxSubscriptionExpirationMinutesParameter,
        ZulipEmailParameter,
        ZulipKeyParameter,
        ZulipUrlParameter,
        SmtpHostParameter,
        SmtpPortParameter,
        SmtpUserParameter,
        SmtpPasswordParameter,
        FhirPathLabUrlParameter,
        OpenTelemetryEndpointParameter,
        OpenTelemetryProtocolParameter,
        OpenTelemetryTracesEndpointParameter,
        OpenTelemetryMetricsEndpointParameter,
        OpenTelemetryLogsEndpointParameter,
    ];

    /// <summary>Parses the given parse result.</summary>
    /// <param name="pr">   The parse result.</param>
    /// <param name="envPR">The environment parse result.</param>
    public virtual void Parse(
        System.CommandLine.Parsing.ParseResult pr,
        System.CommandLine.Parsing.ParseResult envPR)
    {
        foreach (ConfigurationOption opt in _options)
        {
            switch (opt.Name)
            {
                case "PublicUrl":
                    PublicUrl = GetOpt(pr, envPR, opt.CliOption, PublicUrl);
                    break;
                case "ListenPort":
                    ListenPort = GetOpt(pr, envPR, opt.CliOption, ListenPort);
                    break;
                case "OpenBrowser":
                    OpenBrowser = GetOpt(pr, envPR, opt.CliOption, OpenBrowser);
                    break;
                case "MaxResources":
                    MaxResourceCount = GetOpt(pr, envPR, opt.CliOption, MaxResourceCount);
                    break;
                case "DisableUi":
                    DisableUi = GetOpt(pr, envPR, opt.CliOption, DisableUi);
                    break;
                case "FhirPackageCacheDirectory":
                    {
                        string? dir = GetOpt(pr, envPR, opt.CliOption, FhirCacheDirectory);
                        FhirCacheDirectory = string.IsNullOrEmpty(dir) ? null : dir;
                    }
                    break;
                case "UseOfficialRegistries":
                    UseOfficialRegistries = GetOpt(pr, envPR, opt.CliOption, UseOfficialRegistries);
                    break;
                case "AdditionalFhirRegistryUrls":
                    AdditionalFhirRegistryUrls = GetOptArray(pr, envPR, opt.CliOption, AdditionalFhirRegistryUrls, ',');
                    break;
                case "AdditionalNpmRegistryUrls":
                    AdditionalNpmRegistryUrls = GetOptArray(pr, envPR, opt.CliOption, AdditionalNpmRegistryUrls, ',');
                    break;
                case "FhirPackages":
                    PublishedPackages = GetOptArray(pr, envPR, opt.CliOption, PublishedPackages, ',');
                    break;
                case "FhirCiPackages":
                    CiPackages = GetOptArray(pr, envPR, opt.CliOption, CiPackages, ',');
                    break;
                case "LoadExamples":
                    LoadPackageExamples = GetOpt(pr, envPR, opt.CliOption, LoadPackageExamples);
                    break;
                case "ReferenceImplementation":
                    ReferenceImplementation = GetOpt(pr, envPR, opt.CliOption, ReferenceImplementation);
                    break;
                //case "SourceRepository":
                //    SourceRepository = GetOpt(pr, envPR, opt.CliOption, SourceRepository);
                //    break;
                //case "SourceRepositoryPath":
                //    SourceRepositoryPath = GetOpt(pr, envPR, opt.CliOption, SourceRepositoryPath);
                //    break;
                case "FhirSourceDirectory":
                    {
                        string? dir = GetOpt(pr, envPR, opt.CliOption, SourceDirectory);
                        SourceDirectory = string.IsNullOrEmpty(dir) ? null : dir;
                    }
                    break;
                case "ProtectLoadedContent":
                    ProtectLoadedContent = GetOpt(pr, envPR, opt.CliOption, ProtectLoadedContent);
                    break;
                case "TenantsR4":
                    TenantsR4 = GetOptArray(pr, envPR, opt.CliOption, TenantsR4, ',');
                    break;
                case "TenantsR4B":
                    TenantsR4B = GetOptArray(pr, envPR, opt.CliOption, TenantsR4B, ',');
                    break;
                case "TenantsR5":
                    TenantsR5 = GetOptArray(pr, envPR, opt.CliOption, TenantsR5, ',');
                    break;
                case "SmartRequiredTenants":
                    SmartRequiredTenants = GetOptArray(pr, envPR, opt.CliOption, SmartRequiredTenants, ',');
                    break;
                case "SmartOptionalTenants":
                    SmartOptionalTenants = GetOptArray(pr, envPR, opt.CliOption, SmartOptionalTenants, ',');
                    break;
                case "CreateExistingId":
                    AllowExistingId = GetOpt(pr, envPR, opt.CliOption, AllowExistingId);
                    break;
                case "CreateAsUpdate":
                    AllowCreateAsUpdate = GetOpt(pr, envPR, opt.CliOption, AllowCreateAsUpdate);
                    break;
                case "MaxSubscriptionMinutes":
                    MaxSubscriptionExpirationMinutes = GetOpt(pr, envPR, opt.CliOption, MaxSubscriptionExpirationMinutes);
                    break;
                case "ZulipEmail":
                    ZulipEmail = GetOpt(pr, envPR, opt.CliOption, ZulipEmail);
                    break;
                case "ZulipKey":
                    ZulipKey = GetOpt(pr, envPR, opt.CliOption, ZulipKey);
                    break;
                case "ZulipUrl":
                    ZulipUrl = GetOpt(pr, envPR, opt.CliOption, ZulipUrl);
                    break;
                case "SmtpHost":
                    SmtpHost = GetOpt(pr, envPR, opt.CliOption, SmtpHost);
                    break;
                case "SmtpPort":
                    SmtpPort = GetOpt(pr, envPR, opt.CliOption, SmtpPort);
                    break;
                case "SmtpUser":
                    SmtpUser = GetOpt(pr, envPR, opt.CliOption, SmtpUser);
                    break;
                case "SmtpPassword":
                    SmtpPassword = GetOpt(pr, envPR, opt.CliOption, SmtpPassword);
                    break;
                case "FhirPathLabUrl":
                    FhirPathLabUrl = GetOpt(pr, envPR, opt.CliOption, FhirPathLabUrl);
                    break;
                case "OpenTelemetryProtocolEndpoint":
                    OpenTelemetryEndpoint = GetOpt(pr, envPR, opt.CliOption, OpenTelemetryEndpoint);
                    break;
                case "OpenTelemetryProtocol":
                    OpenTelemetryProtocol = GetOpt(pr, envPR, opt.CliOption, OpenTelemetryProtocol);
                    break;
                case "OpenTelemetryProtocolTracesEndpoint":
                    OpenTelemetryTracesEndpoint = GetOpt(pr, envPR, opt.CliOption, OpenTelemetryTracesEndpoint);
                    break;
                case "OpenTelemetryProtocolMetricsEndpoint":
                    OpenTelemetryMetricsEndpoint = GetOpt(pr, envPR, opt.CliOption, OpenTelemetryMetricsEndpoint);
                    break;
                case "OpenTelemetryProtocolLogsEndpoint":
                    OpenTelemetryLogsEndpoint = GetOpt(pr, envPR, opt.CliOption, OpenTelemetryLogsEndpoint);
                    break;
            }
        }
    }

    /// <summary>Gets the array of configuration options.</summary>
    /// <returns>An array of configuration option.</returns>
    public virtual ConfigurationOption[] GetOptions() => _options;

    /// <summary>Gets an option.</summary>
    /// <typeparam name="T">Generic type parameter.</typeparam>
    /// <param name="parseResult"> The parse result.</param>
    /// <param name="opt">         The option.</param>
    /// <param name="defaultValue">The default value.</param>
    /// <returns>The option.</returns>
    internal T GetOpt<T>(
        System.CommandLine.Parsing.ParseResult parseResult,
        System.CommandLine.Parsing.ParseResult envParseResult,
        System.CommandLine.Option opt,
        T defaultValue)
    {
        ParseResult? pr = parseResult.HasOption(opt)
            ? parseResult
            : envParseResult.HasOption(opt)
            ? envParseResult
            : null;

        if (pr == null)
        {
            return defaultValue;
        }

        object? parsed = pr.GetValueForOption(opt);

        if ((parsed != null) &&
            (parsed is T typed))
        {
            return typed;
        }

        return defaultValue;
    }

    /// <summary>Gets option array.</summary>
    /// <exception cref="Exception">Thrown when an exception error condition occurs.</exception>
    /// <typeparam name="T">Generic type parameter.</typeparam>
    /// <param name="parseResult">    The parse result.</param>
    /// <param name="opt">            The option.</param>
    /// <param name="defaultValue">   The default value.</param>
    /// <param name="singleSplitChar">(Optional) The single split character.</param>
    /// <returns>An array of t.</returns>
    internal T[] GetOptArray<T>(
        System.CommandLine.Parsing.ParseResult parseResult,
        System.CommandLine.Parsing.ParseResult envParseResult,
        System.CommandLine.Option opt,
        T[] defaultValue,
        char? singleSplitChar = null)
    {
        ParseResult? pr = parseResult.HasOption(opt)
            ? parseResult
            : envParseResult.HasOption(opt)
            ? envParseResult
            : null;

        if (pr == null)
        {
            return defaultValue;
        }

        object? parsed = pr.GetValueForOption(opt);

        if (parsed == null)
        {
            return defaultValue;
        }

        List<T> values = [];

        if (parsed is T[] array)
        {
            if ((array.Length == 1) &&
                (singleSplitChar != null) &&
                (array is string[] sA))
            {
                string[] splitValues = sA.First().Split(singleSplitChar.Value);

                values.Clear();
                foreach (string v in splitValues)
                {
                    if (v is T tV)
                    {
                        values.Add(tV);
                    }
                }

                return [.. values];
            }

            return array;
        }
        else if (parsed is IEnumerator genericEnumerator)
        {
            // use the enumerator to add values to the array
            while (genericEnumerator.MoveNext())
            {
                if (genericEnumerator.Current is T tValue)
                {
                    values.Add(tValue);
                }
                else
                {
                    throw new Exception("Should not be here!");
                }
            }
        }
        else if (parsed is IEnumerator<T> enumerator)
        {
            // use the enumerator to add values to the array
            while (enumerator.MoveNext())
            {
                values.Add(enumerator.Current);
            }
        }
        else
        {
            throw new Exception("Should not be here!");
        }

        // if no values were added, return the default - parser cannot tell the difference between no values and default values
        if (values.Count == 0)
        {
            return defaultValue;
        }

        if ((values.Count == 1) &&
            (singleSplitChar != null) &&
            (values is List<string> stringValues))
        {
            string[] splitValues = stringValues.First().Split(singleSplitChar.Value);

            values.Clear();
            foreach (string v in splitValues)
            {
                if (v is T tV)
                {
                    values.Add(tV);
                }
            }
        }

        return [.. values];
    }

    /// <summary>Gets option hash.</summary>
    /// <exception cref="Exception">Thrown when an exception error condition occurs.</exception>
    /// <typeparam name="T">Generic type parameter.</typeparam>
    /// <param name="parseResult"> The parse result.</param>
    /// <param name="opt">         The option.</param>
    /// <param name="defaultValue">The default value.</param>
    /// <returns>The option hash.</returns>
    internal HashSet<T> GetOptHash<T>(
        System.CommandLine.Parsing.ParseResult parseResult,
        System.CommandLine.Parsing.ParseResult envParseResult,
        System.CommandLine.Option opt,
        HashSet<T> defaultValue)
    {
        ParseResult? pr = parseResult.HasOption(opt)
            ? parseResult
            : envParseResult.HasOption(opt)
            ? envParseResult
            : null;

        if (pr == null)
        {
            return defaultValue;
        }

        object? parsed = pr.GetValueForOption(opt);

        if (parsed == null)
        {
            return defaultValue;
        }

        HashSet<T> values = [];

        if (parsed is IEnumerator<T> typed)
        {
            // use the enumerator to add values to the array
            while (typed.MoveNext())
            {
                values.Add(typed.Current);
            }
        }
        else
        {
            throw new Exception("Should not be here!");
        }

        // if no values were added, return the default - parser cannot tell the difference between no values and default values
        if (values.Count == 0)
        {
            return defaultValue;
        }

        return values;
    }

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
