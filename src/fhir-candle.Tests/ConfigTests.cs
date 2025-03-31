using fhir.candle.Tests.Models;
using System.Text.Json;
using Xunit.Abstractions;
using fhir.candle.Tests.Extensions;
using Shouldly;
using System.Net;
using FhirCandle.Configuration;
using Microsoft.Extensions.Configuration;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using System.ComponentModel.Design;

namespace fhir.candle.Tests;

public class ConfigTests
{
    [Fact]
    public void TestParseCliInt()
    {
        ConfigurationOption[] configurationOptions = (new CandleConfig()).GetOptions();

        // build our root command
        RootCommand rootCommand = new("A lightweight in-memory FHIR server, for when a small FHIR will do.");
        foreach (ConfigurationOption co in configurationOptions)
        {
            // note that 'global' here is just recursive DOWNWARD
            rootCommand.AddGlobalOption(co.CliOption);
        }

        Parser parser = new CommandLineBuilder(rootCommand).UseDefaults().Build();

        string[] args = ["--port", "8080"];

        // attempt a parse
        ParseResult pr = parser.Parse(args);

        CandleConfig config = new();

        // parse the arguments into the configuration object
        config.Parse(pr, null);

        // check our value
        config.ListenPort.ShouldBe(8080);
    }

    [Fact]
    public void TestParseCliString()
    {
        ConfigurationOption[] configurationOptions = (new CandleConfig()).GetOptions();

        // build our root command
        RootCommand rootCommand = new("A lightweight in-memory FHIR server, for when a small FHIR will do.");
        foreach (ConfigurationOption co in configurationOptions)
        {
            // note that 'global' here is just recursive DOWNWARD
            rootCommand.AddGlobalOption(co.CliOption);
        }

        Parser parser = new CommandLineBuilder(rootCommand).UseDefaults().Build();

        string[] args = ["--url", "http://test.me/"];

        // attempt a parse
        ParseResult pr = parser.Parse(args);

        CandleConfig config = new();

        // parse the arguments into the configuration object
        config.Parse(pr, null);

        // check our value
        config.PublicUrl.ShouldBe("http://test.me/");
    }

    [Fact]
    public void TestParseCliBool()
    {
        ConfigurationOption[] configurationOptions = (new CandleConfig()).GetOptions();

        // build our root command
        RootCommand rootCommand = new("A lightweight in-memory FHIR server, for when a small FHIR will do.");
        foreach (ConfigurationOption co in configurationOptions)
        {
            // note that 'global' here is just recursive DOWNWARD
            rootCommand.AddGlobalOption(co.CliOption);
        }

        Parser parser = new CommandLineBuilder(rootCommand).UseDefaults().Build();

        string[] args = ["--open-browser"];

        // attempt a parse
        ParseResult pr = parser.Parse(args);

        CandleConfig config = new();

        // parse the arguments into the configuration object
        config.Parse(pr, null);

        // check our value
        config.OpenBrowser.ShouldBe(true);
    }

    [Fact]
    public void TestParseCliBoolTrue()
    {
        ConfigurationOption[] configurationOptions = (new CandleConfig()).GetOptions();

        // build our root command
        RootCommand rootCommand = new("A lightweight in-memory FHIR server, for when a small FHIR will do.");
        foreach (ConfigurationOption co in configurationOptions)
        {
            // note that 'global' here is just recursive DOWNWARD
            rootCommand.AddGlobalOption(co.CliOption);
        }

        Parser parser = new CommandLineBuilder(rootCommand).UseDefaults().Build();

        string[] args = ["--open-browser", "true"];

        // attempt a parse
        ParseResult pr = parser.Parse(args);

        CandleConfig config = new();

        // parse the arguments into the configuration object
        config.Parse(pr, null);

        // check our value
        config.OpenBrowser.ShouldBe(true);
    }


    [Fact]
    public void TestParseCliBoolFalse()
    {
        ConfigurationOption[] configurationOptions = (new CandleConfig()).GetOptions();

        // build our root command
        RootCommand rootCommand = new("A lightweight in-memory FHIR server, for when a small FHIR will do.");
        foreach (ConfigurationOption co in configurationOptions)
        {
            // note that 'global' here is just recursive DOWNWARD
            rootCommand.AddGlobalOption(co.CliOption);
        }

        Parser parser = new CommandLineBuilder(rootCommand).UseDefaults().Build();

        string[] args = ["--open-browser", "false"];

        // attempt a parse
        ParseResult pr = parser.Parse(args);

        CandleConfig config = new();

        // parse the arguments into the configuration object
        config.Parse(pr, null);

        // check our value
        config.OpenBrowser.ShouldBe(false);
    }

    [Fact]
    public void TestParseCliStringArray()
    {
        ConfigurationOption[] configurationOptions = (new CandleConfig()).GetOptions();

        // build our root command
        RootCommand rootCommand = new("A lightweight in-memory FHIR server, for when a small FHIR will do.");
        foreach (ConfigurationOption co in configurationOptions)
        {
            // note that 'global' here is just recursive DOWNWARD
            rootCommand.AddGlobalOption(co.CliOption);
        }

        Parser parser = new CommandLineBuilder(rootCommand).UseDefaults().Build();

        string[] args = ["--additional-fhir-registry-urls", "http://a.co/", "--additional-fhir-registry-urls", "http://b.co"];

        // attempt a parse
        ParseResult pr = parser.Parse(args);

        CandleConfig config = new();

        // parse the arguments into the configuration object
        config.Parse(pr, null);

        // check our value
        config.AdditionalFhirRegistryUrls.Length.ShouldBe(2);
        config.AdditionalFhirRegistryUrls.Any(v => v == "http://a.co/").ShouldBe(true);
        config.AdditionalFhirRegistryUrls.Any(v => v == "http://b.co").ShouldBe(true);
    }
}
