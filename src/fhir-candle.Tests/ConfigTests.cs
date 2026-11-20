// <copyright file="ConfigTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using System.Collections.Concurrent;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using fhir.candle.Tests.Extensions;
using fhir.candle.Tests.Models;
using FhirCandle.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.FileProviders;
using ModelContextProtocol.Protocol;
using Shouldly;
using Xunit.Abstractions;

namespace fhir.candle.Tests;

public class ConfigTests
{
    /// <summary>All package limits retain numeric width, external keys, and CLI precedence.</summary>
    [Fact]
    public void PackageLimitsBindDefaultsJsonAndCli()
    {
        CliRootCommand command = new();
        IConfiguration empty = new ConfigurationBuilder().Build();
        CandleConfig defaults = new(command.CommandCliOptions, command.Parse([]), empty);
        AssertLimits(new CandleConfig(), [104857600, 1073741824, 134217728, 50000, 1024, 32]);
        AssertLimits(defaults, [104857600, 1073741824, 134217728, 50000, 1024, 32]);
        using MemoryStream json = new(System.Text.Encoding.UTF8.GetBytes("""
            {
              "Package_Max_Compressed_Bytes": 3000000000,
              "Package_Max_Expanded_Bytes": 4000000000,
              "Package_Max_Entry_Bytes": 2500000000,
              "Package_Max_Archive_Entries": 123,
              "Package_Max_Archive_Path_Length": 456,
              "Package_Max_Archive_Depth": 33
            }
            """));
        IConfiguration external = new ConfigurationBuilder().AddJsonStream(json).Build();
        AssertLimits(external.Get<CandleConfig>()!, [3000000000, 4000000000, 2500000000, 123, 456, 33]);
        AssertLimits(new(command.CommandCliOptions, command.Parse([]), external),
            [3000000000, 4000000000, 2500000000, 123, 456, 33]);
        ParseResult parsed = command.Parse([
            "--package-max-compressed-bytes", "3000000001", "--package-max-expanded-bytes", "4000000001",
            "--package-max-entry-bytes", "2500000001", "--package-max-archive-entries", "124",
            "--package-max-archive-path-length", "457", "--package-max-archive-depth", "34"]);
        parsed.Errors.ShouldBeEmpty();
        AssertLimits(new(command.CommandCliOptions, parsed, external),
            [3000000001, 4000000001, 2500000001, 124, 457, 34]);
    }

    /// <summary>Each numeric option requires exactly one parseable value; binder errors are not policy errors.</summary>
    [Theory]
    [InlineData("--package-max-compressed-bytes", "Package_Max_Compressed_Bytes")]
    [InlineData("--package-max-expanded-bytes", "Package_Max_Expanded_Bytes")]
    [InlineData("--package-max-entry-bytes", "Package_Max_Entry_Bytes")]
    [InlineData("--package-max-archive-entries", "Package_Max_Archive_Entries")]
    [InlineData("--package-max-archive-path-length", "Package_Max_Archive_Path_Length")]
    [InlineData("--package-max-archive-depth", "Package_Max_Archive_Depth")]
    public void PackageLimitsRequireNumericValue(string option, string key)
    {
        CliRootCommand command = new();
        command.Parse([option]).Errors.ShouldNotBeEmpty();
        command.Parse([option, "not-a-number"]).Errors.ShouldNotBeEmpty();
        command.Parse([option, "1", "2"]).Errors.ShouldNotBeEmpty();
        command.Parse([option, "1"]).Errors.ShouldBeEmpty();
        IConfiguration external = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [key] = "not-a-number" }).Build();
        Should.Throw<InvalidOperationException>(() => external.Get<CandleConfig>());
    }

    private static void AssertLimits(CandleConfig config, long[] expected) =>
        new long[] { config.PackageMaxCompressedBytes, config.PackageMaxExpandedBytes, config.PackageMaxEntryBytes,
            config.PackageMaxArchiveEntries, config.PackageMaxArchivePathLength, config.PackageMaxArchiveDepth }.ShouldBe(expected);

    [Fact]
    public void TestParseCliDefaultsPreserveConstructorDistinctions()
    {
        CandleConfig constructorDefaults = new();

        constructorDefaults.ListenPort.ShouldBe(5826);
        constructorDefaults.SmtpPort.ShouldBe(465);
        constructorDefaults.OpenTelemetryProtocol.ShouldBe("grpc");
        constructorDefaults.SupportNotChanged.ShouldBeTrue();
        constructorDefaults.RawSupportNotChanged.ShouldBeNull();

        IConfiguration extConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();
        CliRootCommand rootCommand = new();
        rootCommand.SetAction((ParseResult _) => 0);
        ParseResult pr = rootCommand.Parse([], new ParserConfiguration()
        {
            ResponseFileTokenReplacer = null,
        });

        CandleConfig parsedDefaults = new(rootCommand.CommandCliOptions, pr, extConfig);

        pr.Errors.ShouldBeEmpty();
        extConfig.AsEnumerable().ShouldBeEmpty();
        parsedDefaults.ListenPort.ShouldBe(5826);
        parsedDefaults.SmtpPort.ShouldBe(465);
        parsedDefaults.OpenTelemetryProtocol.ShouldBe("grpc");
        parsedDefaults.SupportNotChanged.ShouldBeFalse();
        parsedDefaults.RawSupportNotChanged.ShouldBeNull();
    }

    [Fact]
    public void TestParseCliInt()
    {
        string[] args = ["--port", "8080"];

        IConfiguration extConfig = new ConfigurationBuilder().Build();
        RootCommand rootCommand = CliOptions.RootCommand;
        ParseResult pr = rootCommand.Parse(args, new ParserConfiguration()
        {
            ResponseFileTokenReplacer = null,
        });

        if (rootCommand is not CliRootCommand crc)
        {
            throw new InvalidOperationException("Root command is not a CliRootCommand");
        }

        CandleConfig config = new(crc.CommandCliOptions, pr, extConfig);

        // check our value
        config.ListenPort.ShouldBe(8080);
    }

    [Fact]
    public void TestParseCliString()
    {
        string[] args = ["--url", "http://test.me/"];

        IConfiguration extConfig = new ConfigurationBuilder().Build();
        RootCommand rootCommand = CliOptions.RootCommand;
        ParseResult pr = rootCommand.Parse(args, new ParserConfiguration()
        {
            ResponseFileTokenReplacer = null,
        });

        if (rootCommand is not CliRootCommand crc)
        {
            throw new InvalidOperationException("Root command is not a CliRootCommand");
        }

        CandleConfig config = new(crc.CommandCliOptions, pr, extConfig);

        // check our value
        config.PublicUrl.ShouldBe("http://test.me/");
    }

    [Fact]
    public void TestParseCliBool()
    {
        string[] args = ["--open-browser"];

        IConfiguration extConfig = new ConfigurationBuilder().Build();
        RootCommand rootCommand = CliOptions.RootCommand;
        ParseResult pr = rootCommand.Parse(args, new ParserConfiguration()
        {
            ResponseFileTokenReplacer = null,
        });

        if (rootCommand is not CliRootCommand crc)
        {
            throw new InvalidOperationException("Root command is not a CliRootCommand");
        }

        CandleConfig config = new(crc.CommandCliOptions, pr, extConfig);

        // check our value
        config.OpenBrowser.ShouldBe(true);
    }

    [Fact]
    public void TestParseCliBoolTrue()
    {
        string[] args = ["--open-browser", "true"];

        IConfiguration extConfig = new ConfigurationBuilder().Build();
        RootCommand rootCommand = CliOptions.RootCommand;
        ParseResult pr = rootCommand.Parse(args, new ParserConfiguration()
        {
            ResponseFileTokenReplacer = null,
        });

        if (rootCommand is not CliRootCommand crc)
        {
            throw new InvalidOperationException("Root command is not a CliRootCommand");
        }

        CandleConfig config = new(crc.CommandCliOptions, pr, extConfig);

        // check our value
        config.OpenBrowser.ShouldBe(true);
    }


    [Fact]
    public void TestParseCliBoolFalse()
    {
        string[] args = ["--open-browser", "false"];

        IConfiguration extConfig = new ConfigurationBuilder().Build();
        RootCommand rootCommand = CliOptions.RootCommand;
        ParseResult pr = rootCommand.Parse(args, new ParserConfiguration()
        {
            ResponseFileTokenReplacer = null,
        });

        if (rootCommand is not CliRootCommand crc)
        {
            throw new InvalidOperationException("Root command is not a CliRootCommand");
        }

        CandleConfig config = new(crc.CommandCliOptions, pr, extConfig);

        // check our value
        config.OpenBrowser.ShouldBe(false);
    }

    [Fact]
    public void TestParseCliStringArray()
    {
        string[] args = ["--additional-fhir-registry-urls", "http://a.co/", "--additional-fhir-registry-urls", "http://b.co"];

        IConfiguration extConfig = new ConfigurationBuilder().Build();
        RootCommand rootCommand = CliOptions.RootCommand;
        ParseResult pr = rootCommand.Parse(args, new ParserConfiguration()
        {
            ResponseFileTokenReplacer = null,
        });

        if (rootCommand is not CliRootCommand crc)
        {
            throw new InvalidOperationException("Root command is not a CliRootCommand");
        }

        CandleConfig config = new(crc.CommandCliOptions, pr, extConfig);

        // check our value
        config.AdditionalFhirRegistryUrls.Length.ShouldBe(2);
        config.AdditionalFhirRegistryUrls.Any(v => v == "http://a.co/").ShouldBe(true);
        config.AdditionalFhirRegistryUrls.Any(v => v == "http://b.co").ShouldBe(true);
    }

    [Fact]
    public void TestParseCliResponseFileValueRemainsLiteral()
    {
        string[] args = ["--url", "@fhir-candle-cli-options.rsp"];

        IConfiguration extConfig = new ConfigurationBuilder().Build();
        CliRootCommand rootCommand = new();
        rootCommand.SetAction((ParseResult _) => 0);
        ParseResult pr = rootCommand.Parse(args, new ParserConfiguration()
        {
            ResponseFileTokenReplacer = null,
        });

        CandleConfig config = new(rootCommand.CommandCliOptions, pr, extConfig);

        pr.Errors.ShouldBeEmpty();
        config.PublicUrl.ShouldBe("@fhir-candle-cli-options.rsp");
    }

    [Fact]
    public void ExternalConfigurationAppliesWhenCliIsAbsent()
    {
        string[] args = [];

        IConfiguration extConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Listen_Port"] = "9090",
                ["Public_Url"] = "http://external.invalid/",
                ["Open_Browser"] = "true",
                ["Tenants_R4:0"] = "external-primary",
                ["Tenants_R4:1"] = "external-secondary",
            })
            .Build();
        CliRootCommand rootCommand = new();
        rootCommand.SetAction((ParseResult _) => 0);
        ParseResult pr = rootCommand.Parse(args, new ParserConfiguration()
        {
            ResponseFileTokenReplacer = null,
        });

        CandleConfig config = new(rootCommand.CommandCliOptions, pr, extConfig);

        pr.Errors.ShouldBeEmpty();
        config.ListenPort.ShouldBe(9090);
        config.PublicUrl.ShouldBe("http://external.invalid/");
        config.OpenBrowser.ShouldBeTrue();
        config.TenantsR4.ShouldBe(new[] { "external-primary", "external-secondary" });
    }

    [Fact]
    public void OptionalJsonConfigurationPreservesMissingFileBehavior()
    {
        DirectoryInfo directory = Directory.CreateTempSubdirectory("fhir-candle-json-");

        try
        {
            using PhysicalFileProvider files = new(directory.FullName);
            using ConfigurationRoot extConfig = (ConfigurationRoot)new ConfigurationBuilder()
                .AddJsonFile(files, "optional.json", optional: true, reloadOnChange: false)
                .Build();

            File.Exists(Path.Combine(directory.FullName, "optional.json")).ShouldBeFalse();
            extConfig.AsEnumerable().ShouldBeEmpty();
            extConfig.Get<CandleConfig>().ShouldBeNull();
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Theory]
    [InlineData("{\"Listen_Port\":", true)]
    [InlineData("[]", false)]
    [InlineData("{\"Listen_Port\":8090,\"listen_port\":8091}", false)]
    public void OptionalJsonConfigurationPreservesParseErrors(string json, bool hasJsonException)
    {
        DirectoryInfo directory = Directory.CreateTempSubdirectory("fhir-candle-json-");

        try
        {
            string path = Path.Combine(directory.FullName, "invalid.json");
            File.WriteAllText(path, json);

            using PhysicalFileProvider files = new(directory.FullName);
            ConfigurationBuilder builder = new();
            builder.AddJsonFile(files, "invalid.json", optional: true, reloadOnChange: false);
            using JsonConfigurationProvider provider = (JsonConfigurationProvider)builder.Sources.Single().Build(builder);

            InvalidDataException error = Should.Throw<InvalidDataException>(() =>
            {
                using ConfigurationRoot extConfig = new([provider]);
            });

            error.Message.ShouldContain(path);
            FormatException parseError = error.InnerException.ShouldBeOfType<FormatException>();
            if (hasJsonException)
            {
                parseError.InnerException.ShouldBeAssignableTo<JsonException>();
            }
            else
            {
                parseError.InnerException.ShouldBeNull();
            }
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void OptionalUserSecretsConfigurationPreservesMissingAttributeBehavior()
    {
        AssemblyBuilder assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName($"UserSecretsAbsence_{Guid.NewGuid():N}"),
            AssemblyBuilderAccess.RunAndCollect);
        ConfigurationBuilder builder = new();

        IConfigurationBuilder result = builder.AddUserSecrets(assembly, optional: true, reloadOnChange: true);

        result.ShouldBeSameAs(builder);
        builder.Sources.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(null, null, null, 8090)]
    [InlineData("8091", null, null, 8091)]
    [InlineData("8091", "8092", null, 8092)]
    [InlineData("8091", "8092", "8093", 8093)]
    public void EnvironmentProvidersPreserveExistingPrecedence(
        string? prefixedPort,
        string? unprefixedPort,
        string? cliPort,
        int expectedPort)
    {
        string sectionName = $"EnvironmentPrecedence_{Guid.NewGuid():N}";
        string prefixedKey = $"Candle{sectionName}__Listen_Port";
        string unprefixedKey = $"{sectionName}__Listen_Port";
        string? originalPrefixedValue = Environment.GetEnvironmentVariable(prefixedKey);
        string? originalUnprefixedValue = Environment.GetEnvironmentVariable(unprefixedKey);

        try
        {
            Environment.SetEnvironmentVariable(prefixedKey, prefixedPort);
            Environment.SetEnvironmentVariable(unprefixedKey, unprefixedPort);

            using MemoryStream json = new(JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, Dictionary<string, int>>
            {
                [sectionName] = new()
                {
                    ["Listen_Port"] = 8090,
                },
            }));
            IConfiguration extConfig = new ConfigurationBuilder()
                .AddJsonStream(json)
                .AddEnvironmentVariables("Candle")
                .AddEnvironmentVariables()
                .Build()
                .GetSection(sectionName);
            CliRootCommand rootCommand = new();
            rootCommand.SetAction((ParseResult _) => 0);
            string[] args = cliPort is null ? [] : ["--port", cliPort];
            ParseResult pr = rootCommand.Parse(args, new ParserConfiguration()
            {
                ResponseFileTokenReplacer = null,
            });

            CandleConfig config = new(rootCommand.CommandCliOptions, pr, extConfig);

            pr.Errors.ShouldBeEmpty();
            config.ListenPort.ShouldBe(expectedPort);
        }
        finally
        {
            Environment.SetEnvironmentVariable(prefixedKey, originalPrefixedValue);
            Environment.SetEnvironmentVariable(unprefixedKey, originalUnprefixedValue);
        }
    }

    [Theory]
    [InlineData("--r4", "Tenants_R4")]
    [InlineData("--r4b", "Tenants_R4B")]
    [InlineData("--r5", "Tenants_R5")]
    [InlineData("--r6", "Tenants_R6")]
    public void ExternalConfigurationBindsTenantArraysWithoutCli(string optionName, string configurationKey)
    {
        IConfiguration extConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{configurationKey}:0"] = "external-secondary",
                [$"{configurationKey}:1"] = "external-primary",
            })
            .Build();

        CandleConfig? config = extConfig.Get<CandleConfig>((options) =>
        {
            options.BindNonPublicProperties = false;
            options.ErrorOnUnknownConfiguration = false;
        });

        config.ShouldNotBeNull();
        GetTenantArray(config, optionName).ShouldBe(new[] { "external-secondary", "external-primary" });
    }

    [Theory]
    [InlineData("--r4", "omitted")]
    [InlineData("--r4", "explicit-empty")]
    [InlineData("--r4", "explicit-values")]
    [InlineData("--r4", "implicit-empty")]
    [InlineData("--r4", "implicit-values")]
    [InlineData("--r4b", "omitted")]
    [InlineData("--r4b", "explicit-empty")]
    [InlineData("--r4b", "explicit-values")]
    [InlineData("--r4b", "implicit-empty")]
    [InlineData("--r4b", "implicit-values")]
    [InlineData("--r5", "omitted")]
    [InlineData("--r5", "explicit-empty")]
    [InlineData("--r5", "explicit-values")]
    [InlineData("--r5", "implicit-empty")]
    [InlineData("--r5", "implicit-values")]
    [InlineData("--r6", "omitted")]
    [InlineData("--r6", "explicit-empty")]
    [InlineData("--r6", "explicit-values")]
    [InlineData("--r6", "implicit-empty")]
    [InlineData("--r6", "implicit-values")]
    public void TenantOptionParsingDistinguishesOmittedAndExplicitEmpty(string optionName, string scenario)
    {
        CliRootCommand rootCommand = new();
        rootCommand.SetAction((ParseResult _) => 0);
        Option<string[]?> option = GetTenantOption(rootCommand.CommandCliOptions, optionName);
        string[] args = scenario switch
        {
            "explicit-empty" => [optionName],
            "explicit-values" => [optionName, "cli-secondary", optionName, "cli-primary", optionName, "cli-secondary"],
            _ => [],
        };
        if (scenario == "implicit-empty")
        {
            SetTenantDefault(option, "empty");
        }
        else if (scenario == "implicit-values")
        {
            SetTenantDefault(option, "values");
        }

        ParseResult pr = rootCommand.Parse(args, new ParserConfiguration()
        {
            ResponseFileTokenReplacer = null,
        });
        OptionResult? optionResult = pr.GetResult(option);
        string[]? parsedValues = pr.GetValue(option);

        pr.Errors.ShouldBeEmpty();
        if (scenario == "omitted")
        {
            optionResult.ShouldBeNull();
        }
        else
        {
            optionResult.ShouldNotBeNull();
            optionResult.Implicit.ShouldBe(scenario is "implicit-empty" or "implicit-values");
        }

        string[] expectedValues = scenario switch
        {
            "explicit-values" => ["cli-secondary", "cli-primary", "cli-secondary"],
            "implicit-values" => ["implicit-secondary", "implicit-primary"],
            _ => [],
        };
        parsedValues.ShouldNotBeNull();
        parsedValues.ShouldBe(expectedValues);
    }

    [Theory]
    [InlineData("--r4", "Tenants_R4", "none")]
    [InlineData("--r4", "Tenants_R4", "empty")]
    [InlineData("--r4", "Tenants_R4", "values")]
    [InlineData("--r4b", "Tenants_R4B", "none")]
    [InlineData("--r4b", "Tenants_R4B", "empty")]
    [InlineData("--r4b", "Tenants_R4B", "values")]
    [InlineData("--r5", "Tenants_R5", "none")]
    [InlineData("--r5", "Tenants_R5", "empty")]
    [InlineData("--r5", "Tenants_R5", "values")]
    [InlineData("--r6", "Tenants_R6", "none")]
    [InlineData("--r6", "Tenants_R6", "empty")]
    [InlineData("--r6", "Tenants_R6", "values")]
    public void ExternalTenantArraysApplyWhenCliIsAbsent(string optionName, string configurationKey, string defaultKind)
    {
        IConfiguration extConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{configurationKey}:0"] = "external-secondary",
                [$"{configurationKey}:1"] = "external-primary",
            })
            .Build();
        CliRootCommand rootCommand = new();
        rootCommand.SetAction((ParseResult _) => 0);
        SetTenantDefault(GetTenantOption(rootCommand.CommandCliOptions, optionName), defaultKind);
        ParseResult pr = rootCommand.Parse([], new ParserConfiguration()
        {
            ResponseFileTokenReplacer = null,
        });

        CandleConfig config = new(rootCommand.CommandCliOptions, pr, extConfig);

        pr.Errors.ShouldBeEmpty();
        GetTenantArray(config, optionName).ShouldBe(new[] { "external-secondary", "external-primary" });
    }

    [Theory]
    [InlineData("--r4", "Tenants_R4")]
    [InlineData("--r4b", "Tenants_R4B")]
    [InlineData("--r5", "Tenants_R5")]
    [InlineData("--r6", "Tenants_R6")]
    public void ExplicitEmptyTenantCliOverridesExternalConfiguration(string optionName, string configurationKey)
    {
        IConfiguration extConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{configurationKey}:0"] = "external-secondary",
                [$"{configurationKey}:1"] = "external-primary",
            })
            .Build();
        CliRootCommand rootCommand = new();
        rootCommand.SetAction((ParseResult _) => 0);
        ParseResult pr = rootCommand.Parse([optionName], new ParserConfiguration()
        {
            ResponseFileTokenReplacer = null,
        });

        CandleConfig config = new(rootCommand.CommandCliOptions, pr, extConfig);

        pr.Errors.ShouldBeEmpty();
        GetTenantArray(config, optionName).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("--r4", "Tenants_R4")]
    [InlineData("--r4b", "Tenants_R4B")]
    [InlineData("--r5", "Tenants_R5")]
    [InlineData("--r6", "Tenants_R6")]
    public void ExplicitTenantCliValuesOverrideExternalConfiguration(string optionName, string configurationKey)
    {
        string[] args = [optionName, "cli-secondary", optionName, "cli-primary", optionName, "cli-secondary"];

        IConfiguration extConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{configurationKey}:0"] = "external-secondary",
                [$"{configurationKey}:1"] = "external-primary",
            })
            .Build();
        CliRootCommand rootCommand = new();
        rootCommand.SetAction((ParseResult _) => 0);
        ParseResult pr = rootCommand.Parse(args, new ParserConfiguration()
        {
            ResponseFileTokenReplacer = null,
        });

        CandleConfig config = new(rootCommand.CommandCliOptions, pr, extConfig);

        pr.Errors.ShouldBeEmpty();
        GetTenantArray(config, optionName).ShouldBe(new[] { "cli-secondary", "cli-primary", "cli-secondary" });
    }

    [Theory]
    [InlineData("--r4", "Tenants_R4", false, "none")]
    [InlineData("--r4", "Tenants_R4", false, "empty")]
    [InlineData("--r4", "Tenants_R4", false, "values")]
    [InlineData("--r4", "Tenants_R4", true, "none")]
    [InlineData("--r4", "Tenants_R4", true, "empty")]
    [InlineData("--r4", "Tenants_R4", true, "values")]
    [InlineData("--r4b", "Tenants_R4B", false, "none")]
    [InlineData("--r4b", "Tenants_R4B", false, "empty")]
    [InlineData("--r4b", "Tenants_R4B", false, "values")]
    [InlineData("--r4b", "Tenants_R4B", true, "none")]
    [InlineData("--r4b", "Tenants_R4B", true, "empty")]
    [InlineData("--r4b", "Tenants_R4B", true, "values")]
    [InlineData("--r5", "Tenants_R5", false, "none")]
    [InlineData("--r5", "Tenants_R5", false, "empty")]
    [InlineData("--r5", "Tenants_R5", false, "values")]
    [InlineData("--r5", "Tenants_R5", true, "none")]
    [InlineData("--r5", "Tenants_R5", true, "empty")]
    [InlineData("--r5", "Tenants_R5", true, "values")]
    [InlineData("--r6", "Tenants_R6", false, "none")]
    [InlineData("--r6", "Tenants_R6", false, "empty")]
    [InlineData("--r6", "Tenants_R6", false, "values")]
    [InlineData("--r6", "Tenants_R6", true, "none")]
    [InlineData("--r6", "Tenants_R6", true, "empty")]
    [InlineData("--r6", "Tenants_R6", true, "values")]
    public void AbsentTenantConfigurationUsesEmptyDefaults(
        string optionName,
        string configurationKey,
        bool externalArrayIsEmpty,
        string defaultKind)
    {
        Dictionary<string, string[]> externalValues = new();
        if (externalArrayIsEmpty)
        {
            externalValues[configurationKey] = [];
        }

        using MemoryStream json = new(JsonSerializer.SerializeToUtf8Bytes(externalValues));
        IConfiguration extConfig = new ConfigurationBuilder().AddJsonStream(json).Build();
        CandleConfig? boundConfig = extConfig.Get<CandleConfig>();
        if (externalArrayIsEmpty)
        {
            boundConfig.ShouldNotBeNull();
            GetTenantArray(boundConfig, optionName).ShouldBeEmpty();
        }
        else
        {
            boundConfig.ShouldBeNull();
        }

        CliRootCommand rootCommand = new();
        rootCommand.SetAction((ParseResult _) => 0);
        SetTenantDefault(GetTenantOption(rootCommand.CommandCliOptions, optionName), defaultKind);
        ParseResult pr = rootCommand.Parse([], new ParserConfiguration()
        {
            ResponseFileTokenReplacer = null,
        });

        CandleConfig config = new(rootCommand.CommandCliOptions, pr, extConfig);

        pr.Errors.ShouldBeEmpty();
        GetTenantArray(config, optionName).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("--r4", false)]
    [InlineData("--r4", true)]
    [InlineData("--r4b", false)]
    [InlineData("--r4b", true)]
    [InlineData("--r5", false)]
    [InlineData("--r5", true)]
    [InlineData("--r6", false)]
    [InlineData("--r6", true)]
    public void TenantCliOverridesOnlyItsOwnVersion(string optionName, bool explicitEmpty)
    {
        string[] args = explicitEmpty
            ? [optionName]
            : [optionName, "cli-secondary", optionName, "cli-primary", optionName, "cli-secondary"];

        IConfiguration extConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Tenants_R4:0"] = "external-r4-secondary",
                ["Tenants_R4:1"] = "external-r4-primary",
                ["Tenants_R4B:0"] = "external-r4b-secondary",
                ["Tenants_R4B:1"] = "external-r4b-primary",
                ["Tenants_R5:0"] = "external-r5-secondary",
                ["Tenants_R5:1"] = "external-r5-primary",
                ["Tenants_R6:0"] = "external-r6-secondary",
                ["Tenants_R6:1"] = "external-r6-primary",
            })
            .Build();
        CliRootCommand rootCommand = new();
        rootCommand.SetAction((ParseResult _) => 0);
        ParseResult pr = rootCommand.Parse(args, new ParserConfiguration()
        {
            ResponseFileTokenReplacer = null,
        });

        CandleConfig config = new(rootCommand.CommandCliOptions, pr, extConfig);

        pr.Errors.ShouldBeEmpty();
        string[] expectedCliTenants = explicitEmpty ? [] : ["cli-secondary", "cli-primary", "cli-secondary"];
        config.TenantsR4.ShouldBe(optionName == "--r4" ? expectedCliTenants : new[] { "external-r4-secondary", "external-r4-primary" });
        config.TenantsR4B.ShouldBe(optionName == "--r4b" ? expectedCliTenants : new[] { "external-r4b-secondary", "external-r4b-primary" });
        config.TenantsR5.ShouldBe(optionName == "--r5" ? expectedCliTenants : new[] { "external-r5-secondary", "external-r5-primary" });
        config.TenantsR6.ShouldBe(optionName == "--r6" ? expectedCliTenants : new[] { "external-r6-secondary", "external-r6-primary" });
    }

    [Fact]
    public void TestParseCliValuesOverrideExternalConfiguration()
    {
        string[] args = ["--port", "8080", "-u", "http://cli.invalid/", "-o", "false", "--r4", "primary", "--r4", "secondary"];

        IConfiguration extConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Listen_Port"] = "9090",
                ["Public_Url"] = "http://external.invalid/",
                ["Open_Browser"] = "true",
                ["Tenants_R4:0"] = "external",
            })
            .Build();
        CliRootCommand rootCommand = new();
        rootCommand.SetAction((ParseResult _) => 0);
        ParseResult pr = rootCommand.Parse(args, new ParserConfiguration()
        {
            ResponseFileTokenReplacer = null,
        });

        CandleConfig config = new(rootCommand.CommandCliOptions, pr, extConfig);

        pr.Errors.ShouldBeEmpty();
        config.ListenPort.ShouldBe(8080);
        config.PublicUrl.ShouldBe("http://cli.invalid/");
        config.OpenBrowser.ShouldBeFalse();
        config.TenantsR4.ShouldBe(new[] { "primary", "secondary" });
    }

    [Theory]
    [InlineData("--url", "-u")]
    [InlineData("--open-browser", "-o")]
    [InlineData("--max-resources", "-m")]
    [InlineData("--disable-ui", "--headless")]
    [InlineData("--load-package", "-p")]
    [InlineData("--protect-source", "--protect-loaded-content")]
    public void TestParseCliAliasesMatchPrimaryOptions(string primaryOption, string alias)
    {
        IConfiguration extConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Open_Browser"] = "true",
                ["Disable_Ui"] = "true",
                ["Protect_Source"] = "true",
            })
            .Build();

        foreach (string option in new[] { primaryOption, alias })
        {
            string[] args = primaryOption switch
            {
                "--url" => [option, "http://alias.invalid/"],
                "--max-resources" => [option, "1234"],
                "--load-package" => [option, "a", option, "b", option, "a"],
                _ => [option, "false"],
            };
            CliRootCommand rootCommand = new();
            rootCommand.SetAction((ParseResult _) => 0);
            ParseResult pr = rootCommand.Parse(args, new ParserConfiguration()
            {
                ResponseFileTokenReplacer = null,
            });

            CandleConfig config = new(rootCommand.CommandCliOptions, pr, extConfig);

            pr.Errors.ShouldBeEmpty();
            switch (primaryOption)
            {
                case "--url":
                    config.PublicUrl.ShouldBe("http://alias.invalid/");
                    break;
                case "--open-browser":
                    config.OpenBrowser.ShouldBeFalse();
                    break;
                case "--max-resources":
                    config.MaxResourceCount.ShouldBe(1234);
                    break;
                case "--disable-ui":
                    config.Headless.ShouldBeFalse();
                    break;
                case "--load-package":
                    config.PublishedPackages.ShouldBe(new[] { "a", "b", "a" });
                    break;
                case "--protect-source":
                    config.ProtectLoadedContent.ShouldBeFalse();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(primaryOption));
            }
        }
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    [InlineData("/h")]
    [InlineData("-?")]
    [InlineData("/?")]
    public async Task TestParseCliHelpDoesNotInvokeAction(string helpOption)
    {
        bool actionInvoked = false;
        CliRootCommand rootCommand = new();
        rootCommand.SetAction((ParseResult _) =>
        {
            actionInvoked = true;
        });
        ParseResult pr = rootCommand.Parse([helpOption], new ParserConfiguration()
        {
            ResponseFileTokenReplacer = null,
        });
        using StringWriter output = new();
        using StringWriter error = new();

        int exitCode = await pr.InvokeAsync(new InvocationConfiguration()
        {
            Output = output,
            Error = error,
        });

        pr.Errors.ShouldBeEmpty();
        exitCode.ShouldBe(0);
        actionInvoked.ShouldBeFalse();
        error.ToString().ShouldBeEmpty();

        string[] expectedOptions =
        [
            "--url", "-u", "--port", "--open-browser", "-o", "--max-resources", "-m", "--disable-ui", "--headless",
            "--fhir-package-cache", "--package-max-compressed-bytes", "--package-max-expanded-bytes",
            "--package-max-entry-bytes", "--package-max-archive-entries", "--package-max-archive-path-length",
            "--package-max-archive-depth", "--use-official-registries", "--additional-fhir-registry-urls",
            "--additional-npm-registry-urls", "--load-package", "-p", "--ci-package", "--load-examples",
            "--reference-implementation", "--enable-mcp", "--fhir-source", "--protect-source", "--protect-loaded-content",
            "--r4", "--r4b", "--r5", "--r6", "--smart-required", "--smart-optional", "--detect-unchanged",
            "--create-existing-id", "--create-as-update", "--strict", "--max-subscription-minutes",
            "--zulip-email", "--zulip-key", "--zulip-url", "--smtp-host", "--smtp-port", "--smtp-user", "--smtp-password",
            "--fhirpath-lab-url", "--otel-otlp-endpoint", "--otel-otlp-protocol", "--otel-otlp-traces-endpoint",
            "--otel-otlp-metrics-endpoint", "--otel-otlp-logs-endpoint", "--version",
        ];
        // v2 indents labels by two spaces and separates descriptions with at least two more.
        // Wrapped descriptions stay in the description column, even when they mention other options.
        string[] optionLabelTokens = output.ToString()
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            .Where(line => line.StartsWith("  -", StringComparison.Ordinal))
            .Select(line => line[2..].Split("  ", 2, StringSplitOptions.None)[0])
            .SelectMany(label => label.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries))
            .ToArray();

        foreach (string option in expectedOptions)
        {
            optionLabelTokens.ShouldContain(option);
        }
    }

    [Fact]
    public async Task TestParseCliVersionDoesNotInvokeAction()
    {
        bool actionInvoked = false;
        CliRootCommand rootCommand = new();
        rootCommand.SetAction((ParseResult _) =>
        {
            actionInvoked = true;
        });
        ParseResult pr = rootCommand.Parse(["--version"], new ParserConfiguration()
        {
            ResponseFileTokenReplacer = null,
        });
        using StringWriter output = new();
        using StringWriter error = new();

        int exitCode = await pr.InvokeAsync(new InvocationConfiguration()
        {
            Output = output,
            Error = error,
        });

        pr.Errors.ShouldBeEmpty();
        exitCode.ShouldBe(0);
        actionInvoked.ShouldBeFalse();
        error.ToString().ShouldBeEmpty();
        output.ToString().ShouldNotBeNullOrWhiteSpace();
        Assembly? entryAssembly = Assembly.GetEntryAssembly();
        entryAssembly.ShouldNotBeNull();
        AssemblyInformationalVersionAttribute? version = entryAssembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        version.ShouldNotBeNull();
        output.ToString().Trim().ShouldBe(version.InformationalVersion);
    }

    [Fact]
    public async Task TestParseCliSuccessInvokesAsyncActionOnce()
    {
        int actionCount = 0;
        ParseResult? receivedParseResult = null;
        int? receivedPort = null;
        CliRootCommand rootCommand = new();
        rootCommand.SetAction(async (ParseResult pr) =>
        {
            actionCount++;
            receivedParseResult = pr;
            receivedPort = pr.GetValue(rootCommand.CommandCliOptions.ListenPort);
            await Task.CompletedTask;
        });
        ParseResult pr = rootCommand.Parse(["--port", "8080"], new ParserConfiguration()
        {
            ResponseFileTokenReplacer = null,
        });
        using StringWriter output = new();
        using StringWriter error = new();

        int exitCode = await pr.InvokeAsync(new InvocationConfiguration()
        {
            Output = output,
            Error = error,
        });

        pr.Errors.ShouldBeEmpty();
        actionCount.ShouldBe(1);
        receivedParseResult.ShouldBeSameAs(pr);
        receivedPort.ShouldBe(8080);
        exitCode.ShouldBe(0);
        error.ToString().ShouldBeEmpty();
    }

    [Theory]
    [InlineData("--port", "not-a-number")]
    [InlineData("--unknown-cli-option", null)]
    [InlineData("@fhir-candle-cli-options.rsp", null)]
    public async Task TestParseCliErrorsDoNotInvokeAction(string argument, string? value)
    {
        string[] args = value is null ? [argument] : [argument, value];
        bool actionInvoked = false;
        CliRootCommand rootCommand = new();
        rootCommand.SetAction((ParseResult _) =>
        {
            actionInvoked = true;
        });
        ParseResult pr = rootCommand.Parse(args, new ParserConfiguration()
        {
            ResponseFileTokenReplacer = null,
        });
        using StringWriter output = new();
        using StringWriter error = new();

        int exitCode = await pr.InvokeAsync(new InvocationConfiguration()
        {
            Output = output,
            Error = error,
        });

        pr.Errors.Count.ShouldBe(1);
        if (value is null)
        {
            pr.UnmatchedTokens.ShouldContain(argument);
        }

        exitCode.ShouldBe(1);
        actionInvoked.ShouldBeFalse();
        error.ToString().ShouldContain(value ?? argument);
    }

    /// <summary>Checks process status and output without letting the CLI start a host.</summary>
    [Fact]
    public async Task TestExecutableCliExitAndHostDispatch()
    {
        string assemblyPath = typeof(ConfigTests).Assembly.Location;
        string? assemblyDirectory = Path.GetDirectoryName(assemblyPath);
        if (string.IsNullOrEmpty(assemblyDirectory))
        {
            throw new InvalidOperationException($"Cannot locate the test output directory from '{assemblyPath}'.");
        }

        DirectoryInfo testOutput = new(assemblyDirectory);
        DirectoryInfo? testProject = testOutput;
        while (testProject is not null && !File.Exists(Path.Combine(testProject.FullName, "fhir-candle.Tests.csproj")))
        {
            testProject = testProject.Parent;
        }

        string tfm = testOutput.Name;
        if (testProject is null || testProject.Parent is not DirectoryInfo src || src.Name != "src" ||
            src.Parent is not DirectoryInfo repo ||
            testOutput.Parent is not DirectoryInfo configuration ||
            configuration.Parent is not DirectoryInfo bin || bin.Name != "bin" ||
            bin.Parent?.FullName != testProject.FullName ||
            !tfm.StartsWith("net", StringComparison.Ordinal) || !Version.TryParse(tfm[3..], out _))
        {
            throw new InvalidOperationException(
                $"Expected '{assemblyPath}' under fhir-candle.Tests/bin/<Configuration>/<TFM> beside its project.");
        }

        string appDirectory = Path.Combine(src.FullName, "fhir-candle", "bin", configuration.Name, tfm);
        string appDll = Path.Combine(appDirectory, "fhir-candle.dll");
        File.Exists(appDll).ShouldBeTrue($"Missing project-reference app output for {tfm}: {appDll}");
        string version = typeof(fhir.candle.Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? throw new InvalidOperationException($"The app reference for {appDll} has no informational version.");

        const string SmtpPortKey = "SMTP_Port";
        const string InvalidSmtpPort = "m13-invalid-host-config";
        const string CaughtHostException = "fhir-candle <<< caught exception:";
        string runId = Guid.NewGuid().ToString("N");
        string cacheSentinel = $"m13-missing-cache-{runId}";
        string[] portOnly = ["--port", "8080"];
        string[] control = [..portOnly, "--fhir-package-cache", cacheSentinel];
        CliRootCommand command = new();
        ParserConfiguration parser = new() { ResponseFileTokenReplacer = null };
        ParseResult parsedPort = command.Parse(portOnly, parser);
        ParseResult parsedControl = command.Parse(control, parser);
        parsedPort.Errors.ShouldBeEmpty();
        parsedControl.Errors.ShouldBeEmpty();

        // Check both environment guards without a CLI cache value that could mask the environment fallback.
        IConfiguration invalidSmtp = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { [SmtpPortKey] = InvalidSmtpPort }).Build();
        InvalidOperationException smtpFailure = Should.Throw<InvalidOperationException>(
            () => new CandleConfig(command.CommandCliOptions, parsedPort, invalidSmtp));
        Assert.Contains(SmtpPortKey, smtpFailure.Message, StringComparison.Ordinal);

        IConfiguration missingCache = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { [SmtpPortKey] = "465", ["Fhir_Package_Cache"] = cacheSentinel }).Build();
        DirectoryNotFoundException cacheFailure = Should.Throw<DirectoryNotFoundException>(
            () => new CandleConfig(command.CommandCliOptions, parsedPort, missingCache));
        Assert.Contains(cacheSentinel, cacheFailure.Message, StringComparison.Ordinal);

        // The first child also has a fallback independent of the environment provider.
        IConfiguration empty = new ConfigurationBuilder().Build();
        DirectoryNotFoundException cliCacheFailure = Should.Throw<DirectoryNotFoundException>(
            () => new CandleConfig(command.CommandCliOptions, parsedControl, empty));
        Assert.Contains(cacheSentinel, cliCacheFailure.Message, StringComparison.Ordinal);

        // The child's relative cache lookup starts at the application base, not its working directory.
        for (DirectoryInfo? directory = new(appDirectory); directory is not null; directory = directory.Parent)
        {
            string candidate = Path.Combine(directory.FullName, cacheSentinel);
            Directory.Exists(candidate).ShouldBeFalse($"Cache guard must be a nonexistent directory: {candidate}");
        }

        string? dotnetHost = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
        if (string.IsNullOrEmpty(dotnetHost))
        {
            dotnetHost = "dotnet";
        }
        else if (!Path.IsPathFullyQualified(dotnetHost) || !File.Exists(dotnetHost))
        {
            throw new InvalidOperationException($"DOTNET_HOST_PATH must name an existing absolute path: '{dotnetHost}'.");
        }

        string workingDirectory = Path.Combine(repo.FullName, "scratch", $"m13-cli-{runId}");
        Path.Exists(workingDirectory).ShouldBeFalse($"The child working directory must be owned by this run: {workingDirectory}");
        Directory.CreateDirectory(workingDirectory);
        Exception? caseFailure = null;
        try
        {
            (int controlExit, string controlOutput, _, string controlDetails) = await RunExecutableCliAsync(
                dotnetHost, appDll, workingDirectory, InvalidSmtpPort, cacheSentinel, control);
            int hResult = new InvalidOperationException().HResult;
            controlExit.ShouldBe(OperatingSystem.IsWindows() ? hResult : (hResult & 0xFF), controlDetails);
            controlOutput.Split('\n').Count(line => line.Contains(CaughtHostException, StringComparison.Ordinal)
                && line.Contains(SmtpPortKey, StringComparison.Ordinal)).ShouldBe(1, controlDetails);

            (string[] Arguments, int ExitCode, string? ErrorToken)[] cases =
            [
                (["--port", "not-a-number"], 1, "not-a-number"),
                (["--unknown-cli-option"], 1, "--unknown-cli-option"),
                (["--help"], 0, null),
                (["--version"], 0, null),
            ];
            foreach ((string[] arguments, int expectedExit, string? errorToken) in cases)
            {
                (int exitCode, string output, string errors, string details) = await RunExecutableCliAsync(
                    dotnetHost, appDll, workingDirectory, InvalidSmtpPort, cacheSentinel, arguments);
                exitCode.ShouldBe(expectedExit, details);
                output.Contains(CaughtHostException, StringComparison.Ordinal).ShouldBeFalse(details);
                errors.Contains(CaughtHostException, StringComparison.Ordinal).ShouldBeFalse(details);
                if (errorToken is not null)
                {
                    errors.Contains(errorToken, StringComparison.Ordinal).ShouldBeTrue(details);
                    continue;
                }

                errors.ShouldBe(string.Empty, details);
                if (arguments[0] == "--help")
                {
                    string[] optionLabelTokens = output
                        .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
                        .Where(line => line.StartsWith("  -", StringComparison.Ordinal))
                        .Select(line => line[2..].Split("  ", 2, StringSplitOptions.None)[0])
                        .SelectMany(label => label.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries))
                        .ToArray();
                    optionLabelTokens.Contains("--port", StringComparer.Ordinal).ShouldBeTrue(details);
                    optionLabelTokens.Contains("--version", StringComparer.Ordinal).ShouldBeTrue(details);
                }
                else
                {
                    output.Trim().ShouldBe(version, details);
                }
            }
        }
        catch (Exception ex)
        {
            caseFailure = ex;
            throw;
        }
        finally
        {
            try
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
            catch (Exception ex) when (caseFailure is not null)
            {
                throw new AggregateException($"Could not remove the owned working directory '{workingDirectory}'.", caseFailure, ex);
            }
        }
    }

    private static async Task<(int ExitCode, string Stdout, string Stderr, string Details)> RunExecutableCliAsync(
        string dotnetHost, string appDll, string workingDirectory, string smtpGuard, string cacheSentinel, string[] arguments)
    {
        ProcessStartInfo start = new(dotnetHost)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        start.ArgumentList.Add(appDll);
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        start.Environment.Clear();
        string architectureRoot = $"DOTNET_ROOT_{RuntimeInformation.ProcessArchitecture.ToString().ToUpperInvariant()}";
        string[] hostVariables = OperatingSystem.IsWindows()
            ? ["PATH", "SystemRoot", architectureRoot, "DOTNET_ROOT(x86)", "DOTNET_ROOT"]
            : ["PATH", architectureRoot, "DOTNET_ROOT"];
        foreach (string name in hostVariables)
        {
            if (Environment.GetEnvironmentVariable(name) is string value)
            {
                start.Environment[name] = value;
            }
        }
        start.Environment["TEMP"] = workingDirectory;
        start.Environment["TMP"] = workingDirectory;
        start.Environment["TMPDIR"] = workingDirectory;
        start.Environment["DOTNET_EnableDiagnostics"] = "0";
        start.Environment["SMTP_Port"] = smtpGuard;
        start.Environment["Fhir_Package_Cache"] = cacheSentinel;

        Process process = new() { StartInfo = start };
        CancellationTokenSource childCancellation = new();
        using CancellationTokenSource exitDeadline = new(TimeSpan.FromSeconds(30));
        CliOutputCapture outputCapture = new();
        CliOutputCapture errorCapture = new();
        TaskCompletionSource<int> confirmedExit = new();
        bool started = false;
        bool timedOut = false;
        int? pid = null;
        Exception? runFailure = null;
        ConcurrentQueue<string> cleanupFailures = new();
        try
        {
            started = process.Start();
            if (!started)
            {
                throw new InvalidOperationException($"Could not start the dotnet host '{dotnetHost}'.");
            }

            pid = process.Id;
            Task stdout = outputCapture.StartDrain(process.StandardOutput, childCancellation.Token);
            Task stderr = errorCapture.StartDrain(process.StandardError, childCancellation.Token);
            Task exit = process.WaitForExitAsync(childCancellation.Token);
            await foreach (Task completed in Task.WhenEach(exit, stdout, stderr).WithCancellation(exitDeadline.Token))
            {
                // Surface a read fault even when another read or the exit wait is still pending.
                await completed;
            }
            exitDeadline.Token.ThrowIfCancellationRequested();
            confirmedExit.SetResult(process.ExitCode);
        }
        catch (Exception ex)
        {
            timedOut = ex is OperationCanceledException && exitDeadline.IsCancellationRequested;
            runFailure = ex;
        }
        finally
        {
            using CancellationTokenSource cleanupDeadline = new(TimeSpan.FromSeconds(10));
            CancellationToken cleanupToken = cleanupDeadline.Token;

            // Synchronous kill/close operations must not keep the caller past the teardown deadline.
            Task processCleanup = Task.Run(async () =>
            {
                try
                {
                    if (started && !confirmedExit.Task.IsCompletedSuccessfully)
                    {
                        try
                        {
                            if (!process.HasExited)
                            {
                                cleanupToken.ThrowIfCancellationRequested();
                                process.Kill(entireProcessTree: true);
                            }
                        }
                        catch (InvalidOperationException) when (process.HasExited)
                        {
                            // The child exited between the liveness check and the kill attempt.
                        }
                        catch (Exception ex)
                        {
                            cleanupFailures.Enqueue($"Could not terminate the owned child tree: {ex}");
                        }

                        // Pipe completion or cancellation is not evidence that the process exited.
                        try
                        {
                            await process.WaitForExitAsync(cleanupToken);
                            confirmedExit.SetResult(process.ExitCode);
                        }
                        catch (Exception ex)
                        {
                            cleanupFailures.Enqueue($"Could not confirm child exit: {ex}");
                        }
                    }
                }
                finally
                {
                    process.Dispose();
                }
            });

            Task cancelChildWaits = CancelChildWaitsAsync();
            Task closeOutput = outputCapture.CloseAsync(cleanupToken);
            Task closeError = errorCapture.CloseAsync(cleanupToken);
            try
            {
                await foreach (Task _ in Task.WhenEach(processCleanup, cancelChildWaits, closeOutput, closeError,
                    outputCapture.Drain ?? Task.CompletedTask, errorCapture.Drain ?? Task.CompletedTask).WithCancellation(cleanupToken))
                {
                    // Inspect each operation and the two captures separately after the bounded wait.
                }
                cleanupToken.ThrowIfCancellationRequested();
            }
            catch (OperationCanceledException) when (cleanupToken.IsCancellationRequested)
            {
                cleanupFailures.Enqueue("Child teardown did not complete within 10 seconds.");
            }

            RecordCleanupFailure(processCleanup, "Process teardown");
            RecordCleanupFailure(cancelChildWaits, "Child wait cancellation");
            RecordCleanupFailure(closeOutput, "Stdout close");
            RecordCleanupFailure(closeError, "Stderr close");
        }

        int? exitCode = confirmedExit.Task.IsCompletedSuccessfully ? await confirmedExit.Task : null;
        if (started && exitCode is null)
        {
            cleanupFailures.Enqueue("Owned child exit was not confirmed.");
        }
        (string output, string stdoutState, bool stdoutTruncated) = outputCapture.Snapshot();
        (string errors, string stderrState, bool stderrTruncated) = errorCapture.Snapshot();
        string tfm = Path.GetFileName(Path.GetDirectoryName(appDll))!;
        string details = $"CLI case '{string.Join(' ', arguments)}', DLL '{appDll}', TFM {tfm}, " +
            $"PID {pid?.ToString() ?? "not available"}, exit {exitCode?.ToString() ?? "unconfirmed"}\n" +
            $"stdout ({stdoutState}{(stdoutTruncated ? ", truncated" : string.Empty)}):\n{output}\n" +
            $"stderr ({stderrState}{(stderrTruncated ? ", truncated" : string.Empty)}):\n{errors}";
        string[] cleanupErrors = cleanupFailures.ToArray();
        if (runFailure is not null || stdoutTruncated || stderrTruncated || cleanupErrors.Length != 0)
        {
            string reason = timedOut ? "Child exit and output did not complete within 30 seconds."
                : runFailure is not null ? "Child launch or output capture failed."
                : stdoutTruncated || stderrTruncated ? "Child output exceeded 64 Ki characters per stream."
                : "Child cleanup failed.";
            string cleanup = cleanupErrors.Length == 0 ? string.Empty : "\n" + string.Join('\n', cleanupErrors);
            throw new InvalidOperationException($"{reason}\n{details}{cleanup}", runFailure);
        }

        return (exitCode!.Value, output, errors, details);

        async Task CancelChildWaitsAsync()
        {
            try
            {
                if (runFailure is not null)
                {
                    await childCancellation.CancelAsync();
                }
            }
            finally
            {
                childCancellation.Dispose();
            }
        }

        void RecordCleanupFailure(Task task, string operation)
        {
            if (!task.IsCompletedSuccessfully)
            {
                cleanupFailures.Enqueue($"{operation}: {DescribeCliTask(task)}");
            }
        }
    }

    private static string DescribeCliTask(Task? task) => task?.Status switch
    {
        null => "not started",
        TaskStatus.RanToCompletion => "complete",
        TaskStatus.Faulted => $"faulted: {task?.Exception}",
        TaskStatus.Canceled => "canceled",
        _ => "unfinished",
    };

    private sealed class CliOutputCapture
    {
        private const int CaptureLimit = 64 * 1024;
        private readonly object _gate = new();
        private readonly StringBuilder _text = new();
        private StreamReader? _reader;
        private bool _truncated;

        public Task? Drain { get; private set; }

        public Task StartDrain(StreamReader reader, CancellationToken token)
        {
            _reader = reader;
            return Drain = Task.Run(async () =>
            {
                char[] buffer = new char[4096];
                int count;
                while ((count = await reader.ReadAsync(buffer.AsMemory(), token)) != 0)
                {
                    lock (_gate)
                    {
                        int retained = Math.Min(count, CaptureLimit - _text.Length);
                        _text.Append(buffer, 0, retained);
                        _truncated |= retained < count;
                    }
                }
            });
        }

        public Task CloseAsync(CancellationToken deadline)
        {
            if (_reader is not StreamReader reader)
            {
                return Task.CompletedTask;
            }

            return Task.Run(async () =>
            {
                if (Drain is { IsCompleted: false } drain)
                {
                    // Abort the pipe without modifying the reader's buffers during a pending read.
                    reader.BaseStream.Dispose();
                    try
                    {
                        await drain.WaitAsync(deadline);
                    }
                    catch (Exception) when (drain.IsCompleted)
                    {
                        // The capture snapshot reports the read outcome.
                    }
                }

                reader.Dispose();
            });
        }

        public (string Text, string State, bool Truncated) Snapshot()
        {
            lock (_gate)
            {
                return (_text.ToString(), DescribeCliTask(Drain), _truncated);
            }
        }
    }

    private static Option<string[]?> GetTenantOption(CliOptions options, string optionName)
    {
        return optionName switch
        {
            "--r4" => options.TenantsR4,
            "--r4b" => options.TenantsR4B,
            "--r5" => options.TenantsR5,
            "--r6" => options.TenantsR6,
            _ => throw new ArgumentOutOfRangeException(nameof(optionName)),
        };
    }

    private static string[] GetTenantArray(CandleConfig config, string optionName)
    {
        return optionName switch
        {
            "--r4" => config.TenantsR4,
            "--r4b" => config.TenantsR4B,
            "--r5" => config.TenantsR5,
            "--r6" => config.TenantsR6,
            _ => throw new ArgumentOutOfRangeException(nameof(optionName)),
        };
    }

    private static void SetTenantDefault(Option<string[]?> option, string defaultKind)
    {
        switch (defaultKind)
        {
            case "none":
                break;
            case "empty":
                option.DefaultValueFactory = (_) => [];
                break;
            case "values":
                option.DefaultValueFactory = (_) => ["implicit-secondary", "implicit-primary"];
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(defaultKind));
        }
    }
}
