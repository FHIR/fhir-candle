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
}
