// <copyright file="ConfigTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using System.CommandLine;
using System.CommandLine.Parsing;
using System.ComponentModel.Design;
using System.Net;
using System.Reflection;
using System.Reflection.Emit;
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
    [InlineData("--help")]
    [InlineData("-h")]
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
        output.ToString().ShouldContain("--port");
        output.ToString().ShouldContain("--url");
        output.ToString().ShouldContain("--additional-fhir-registry-urls");
        output.ToString().ShouldContain("--r4");
        output.ToString().ShouldContain("--r4b");
        output.ToString().ShouldContain("--r5");
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

        // Program.Main currently discards this parser exit code.
        exitCode.ShouldBe(1);
        actionInvoked.ShouldBeFalse();
        error.ToString().ShouldContain(value ?? argument);
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
