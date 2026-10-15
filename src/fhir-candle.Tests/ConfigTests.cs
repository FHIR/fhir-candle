// <copyright file="ConfigTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using System.CommandLine;
using System.CommandLine.Parsing;
using System.ComponentModel.Design;
using System.Net;
using System.Text.Json;
using fhir.candle.Tests.Extensions;
using fhir.candle.Tests.Models;
using FhirCandle.Configuration;
using Microsoft.Extensions.Configuration;
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
}
