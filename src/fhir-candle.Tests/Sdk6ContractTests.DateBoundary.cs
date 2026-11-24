// <copyright file="Sdk6ContractTests.DateBoundary.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

extern alias candleR4;
extern alias coreR4;

using System.Net;
using candleR4::FhirCandle.Models;
using candleR4::FhirCandle.Search;
using candleR4::FhirCandle.Storage;
using fhir.candle.Tests.Extensions;
using fhir.candle.Tests.Models;
using FhirCandle.Models;
using FhirCandle.Storage;
using FhirCandle.Utils;
using Hl7.Fhir.ElementModel;
using Hl7.Fhir.FhirPath;
using Hl7.Fhir.Model;
using Observation = coreR4::Hl7.Fhir.Model.Observation;
using ObservationStatus = coreR4::Hl7.Fhir.Model.ObservationStatus;
using Patient = coreR4::Hl7.Fhir.Model.Patient;
using Task = System.Threading.Tasks.Task;

namespace fhir.candle.Tests;

public partial class Sdk6ContractTests
{
    private const string DateBoundaryFirstSecond = "0001-01-01T00:00:00Z";
    private const string DateBoundaryLastSecond = "9999-12-31T23:59:59Z";
    private const string DateBoundaryLastTick = "9999-12-31T23:59:59.9999999Z";
    private const string DateBoundaryFirstOffset = "0001-01-01T14:00:00+14:00";
    private const string DateBoundaryLastOffset = "9999-12-31T09:59:59-14:00";
    private const string DateBoundaryFirstPartial = "0001-01-01T13:59:59.5+14:00";
    private const string DateBoundaryLastPartial = "9999-12-31T09:59:59.5-14:00";
    private const string DateBoundaryBeforeUtc = "0001-01-01T13:59:59+14:00";
    private const string DateBoundaryAfterUtc = "9999-12-31T10:00:00-14:00";
    private const string DateBoundaryLocalCarry = "9999-12-31T23:59:59.99999999+1";
    private const string DateBoundaryInvalidHour = "9999-12-31T24:00:00+01:00";
    private const string DateBoundaryHourProjection = "9999-12-31T23:00:00Z";

    public static IEnumerable<object[]> DateBoundaryQueryCases => DateBoundaryReplayRows(
        "0001", "0001-01", "0001-01-01", "9999", "9999-12", "9999-12-31",
        DateBoundaryFirstSecond, DateBoundaryLastSecond, DateBoundaryLastTick,
        "strict-alone", "lenient-alone", "strict-mixed", "lenient-mixed",
        "0001-02-29", "9999-12-31T24:00:00Z", "9999-12-31T23:59:59+00:60",
        "9999-12-31T23:59:59+14:01", "9999-12-31T23:59:59-14:01",
        "9999-12-31T23:59:59+15:00", "9999-12-31T23:59:59-15:00",
        DateBoundaryBeforeUtc, DateBoundaryAfterUtc);

    public static IEnumerable<object[]> DateBoundaryStoredCases => DateBoundaryReplayRows(
        "9999", "9999-12", "9999-12-31", DateBoundaryLastSecond, DateBoundaryLastTick,
        "first-patient-precision", "last-patient-precision", "first-observation-precision", "last-observation-precision");

    public static IEnumerable<object[]> DateBoundaryOffsetCases => DateBoundaryReplayRows(
        "first-query", "first-stored", "first-partial-query",
        "last-query", "last-stored", "last-partial-query", "last-partial-stored",
        "upper-shift-query", "upper-shift-stored");

    public static IEnumerable<object[]> DateBoundaryApproximateCases => DateBoundaryReplayRows(
        "lower-overflow", "upper-overflow", "first-year", "last-year", "first-month", "last-month",
        "last-day", "first-second", "last-second");

    private static IEnumerable<object[]> DateBoundaryReplayRows(params string[] kinds)
    {
        foreach (object[] row in ReplayCases)
        {
            foreach (bool form in new[] { false, true })
            {
                foreach (string kind in kinds)
                {
                    yield return [row[1], row[2], row[3], form, kind];
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(DateBoundaryQueryCases))]
    public async Task DateBoundaryQueryHandling(FhirReleases.FhirSequenceCodes version, CompatibilityTransport transport,
        CompatibilityFormat format, bool form, string kind)
    {
        SearchGraph graph = DateBoundaryGraph(version, format);
        await using CompatibilityReplay replay = new(SearchScenario("m12-query", graph), transport);
        string alpha = await WriteDateBoundaryScalar(replay, graph, "Patient", "2000-02-29", "Alpha");
        await WriteDateBoundaryScalar(replay, graph, "Patient", "1999-12-31", "Beta");
        await WriteDateBoundaryScalar(replay, graph, "Observation", "2000-02-29T12:34:56Z", "Ordinary");

        switch (kind)
        {
            case "strict-alone":
                await AssertDateBoundaryFailure(replay, graph, form, kind, "Patient?name=Alpha&birthdate=0000", "birthdate");
                break;
            case "lenient-alone":
                await AssertDateBoundarySearch(replay, graph, form,
                    new(kind, "Patient?name=Alpha&birthdate=0000", [alpha]) { AppliedPath = "Patient?name=Alpha" }, strict: false);
                break;
            case "strict-mixed":
                await AssertDateBoundaryFailure(replay, graph, form, kind, "Patient?birthdate=0000,ge2000", "birthdate", "0000");
                break;
            case "lenient-mixed":
                await AssertDateBoundarySearch(replay, graph, form, new(kind, "Patient?birthdate=0000,ge2000", [alpha]), strict: false);
                break;
            case "0001":
            case "0001-01":
            case "0001-01-01":
            case "9999":
            case "9999-12":
            case "9999-12-31":
                await AssertDateBoundarySearch(replay, graph, form, new(kind, $"Patient?birthdate={kind}", []));
                break;
            case DateBoundaryFirstSecond:
            case DateBoundaryLastSecond:
            case DateBoundaryLastTick:
                await AssertDateBoundarySearch(replay, graph, form, new(kind, $"Observation?date={kind}", []));
                break;
            case "0001-02-29":
                await AssertDateBoundaryFailure(replay, graph, form, kind, $"Patient?birthdate={kind}", "birthdate");
                break;
            default:
                await AssertDateBoundaryFailure(replay, graph, form, kind, $"Observation?date={kind}", "date");
                break;
        }
    }

    [Theory]
    [MemberData(nameof(DateBoundaryStoredCases))]
    public async Task DateBoundaryStoredValues(FhirReleases.FhirSequenceCodes version, CompatibilityTransport transport,
        CompatibilityFormat format, bool form, string kind)
    {
        SearchGraph graph = DateBoundaryGraph(version, format);
        await using CompatibilityReplay replay = new(SearchScenario("m12-stored", graph), transport);
        if (kind.EndsWith("-patient-precision", StringComparison.Ordinal))
        {
            bool lower = kind.StartsWith("first", StringComparison.Ordinal);
            string year = lower ? "0001" : "9999";
            string month = lower ? "0001-01" : "9999-12";
            string day = lower ? "0001-01-01" : "9999-12-31";
            string yearId = await WriteDateBoundaryScalar(replay, graph, "Patient", year, "Year");
            string monthId = await WriteDateBoundaryScalar(replay, graph, "Patient", month, "Month");
            string dayId = await WriteDateBoundaryScalar(replay, graph, "Patient", day, "Day");
            await WriteDateBoundaryScalar(replay, graph, "Patient", "2000-02-29", "Ordinary");
            await AssertDateBoundarySearch(replay, graph, form, new("year", $"Patient?birthdate=eq{year}", [yearId, monthId, dayId]));
            await AssertDateBoundarySearch(replay, graph, form, new("month", $"Patient?birthdate=eq{month}", [monthId, dayId]));
            await AssertDateBoundarySearch(replay, graph, form, new("day", $"Patient?birthdate=eq{day}", [dayId]));
        }
        else if (kind == "first-observation-precision")
        {
            string first = await WriteDateBoundaryScalar(replay, graph, "Observation", DateBoundaryFirstSecond, "First");
            await WriteDateBoundaryScalar(replay, graph, "Observation", "0001-01-01T00:00:00.0000001Z", "NextTick");
            await WriteDateBoundaryScalar(replay, graph, "Observation", "2000-02-29T00:00:00Z", "Ordinary");
            await AssertDateBoundarySearch(replay, graph, form, new("first-eq", $"Observation?date=eq{DateBoundaryFirstSecond}", [first]));
            await AssertDateBoundarySearch(replay, graph, form, new("first-lt", $"Observation?date=lt{DateBoundaryFirstSecond}", []));
            await AssertDateBoundarySearch(replay, graph, form, new("next-tick-lt", "Observation?date=lt0001-01-01T00:00:00.0000001Z", [first]));
        }
        else if (kind == "last-observation-precision")
        {
            string last = await WriteDateBoundaryScalar(replay, graph, "Observation", DateBoundaryLastTick, "Last");
            await WriteDateBoundaryScalar(replay, graph, "Observation", "2000-02-29T00:00:00Z", "Ordinary");
            foreach (string literal in new[] { "9999", "9999-12", "9999-12-31", DateBoundaryLastSecond })
            {
                await AssertDateBoundarySearch(replay, graph, form, new($"last-eq-{literal}", $"Observation?date=eq{literal}", [last]));
            }
            await AssertDateBoundarySearch(replay, graph, form, new("last-gt", $"Observation?date=gt{DateBoundaryLastSecond}", []));
            await AssertDateBoundarySearch(replay, graph, form, new("last-sa", $"Observation?date=sa{DateBoundaryLastSecond}", []));
        }
        else
        {
            // The search literal is ordinary: any terminal overflow must come from matching the stored value.
            bool observation = kind.Contains('T');
            string type = observation ? "Observation" : "Patient";
            string key = observation ? "date" : "birthdate";
            string last = await WriteDateBoundaryScalar(replay, graph, type, kind, "Last");
            await WriteDateBoundaryScalar(replay, graph, type, observation ? "1999-12-31T23:59:59Z" : "1999-12-31", "Earlier");
            await AssertDateBoundarySearch(replay, graph, form, new("stored-" + kind, $"{type}?{key}=ge2000", [last]));
        }
    }

    [Theory]
    [MemberData(nameof(DateBoundaryOffsetCases))]
    public async Task DateBoundaryOffsetsProjectInUtc(FhirReleases.FhirSequenceCodes version, CompatibilityTransport transport,
        CompatibilityFormat format, bool form, string kind)
    {
        SearchGraph graph = DateBoundaryGraph(version, format);
        await using CompatibilityReplay replay = new(SearchScenario("m12-offset", graph), transport);
        (string query, string match, string control) = kind switch
        {
            "first-query" => ("eq" + DateBoundaryFirstOffset, DateBoundaryFirstSecond, "0001-01-01T00:00:00.5Z"),
            "first-stored" => ("eq" + DateBoundaryFirstSecond, DateBoundaryFirstOffset, "0001-01-01T14:00:01+14:00"),
            "first-partial-query" => ("le" + DateBoundaryFirstPartial, DateBoundaryFirstSecond, "0001-01-01T00:00:00.5Z"),
            "last-query" => ("eq" + DateBoundaryLastOffset, DateBoundaryLastSecond, "9999-12-31T23:59:58Z"),
            "last-stored" => ("eq" + DateBoundaryLastSecond, DateBoundaryLastOffset, "9999-12-31T09:59:58-14:00"),
            "last-partial-query" => ("eq" + DateBoundaryLastPartial, "9999-12-31T23:59:59.5Z", DateBoundaryLastSecond),
            "last-partial-stored" => ("eq9999-12-31T23:59:59.5Z", DateBoundaryLastPartial, DateBoundaryLastOffset),
            "upper-shift-query" => ("eq9999-12-31T23:59:59+01:00", "9999-12-31T22:59:59Z", DateBoundaryLastSecond),
            "upper-shift-stored" => ("eq9999-12-31T22:59:59Z", "9999-12-31T23:59:59+01:00", DateBoundaryLastSecond),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        string id = await WriteDateBoundaryScalar(replay, graph, "Observation", match, "Match");
        // Keep the UTC terminal-second control independent of the stored string parser for the shifted case.
        await WriteDateBoundaryScalar(replay, graph, "Observation", control, "Control", period: kind.StartsWith("upper-shift", StringComparison.Ordinal));
        await AssertDateBoundarySearch(replay, graph, form, new(kind, $"Observation?date={query}", [id]));
    }

    [Theory]
    [MemberData(nameof(DateBoundaryApproximateCases))]
    public async Task DateBoundaryApproximateWindows(FhirReleases.FhirSequenceCodes version, CompatibilityTransport transport,
        CompatibilityFormat format, bool form, string kind)
    {
        SearchGraph graph = DateBoundaryGraph(version, format);
        await using CompatibilityReplay replay = new(SearchScenario("m12-ap", graph), transport);
        (string literal, string inside, string outside) = kind switch
        {
            "lower-overflow" => ("0001-01-01", "0001-02-01", "0001-02-02"),
            "upper-overflow" => ("9999-12-30", "9999-11-29", "9999-11-28"),
            "first-year" => ("0001", "0002-12-31", "0003-01-01"),
            "last-year" => ("9999", "9998-01-01", "9997-12-31"),
            "first-month" => ("0001-01", "0001-04-06", "0001-04-07"),
            "last-month" => ("9999-12", "9999-09-27", "9999-09-26"),
            "last-day" => ("9999-12-31", "9999-11-30", "9999-11-29"),
            "first-second" => (DateBoundaryFirstSecond, "0001-01-02T00:00:00Z", "0001-01-02T00:00:01Z"),
            "last-second" => (DateBoundaryLastSecond, "9999-12-30T23:59:59Z", "9999-12-30T23:59:58Z"),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        bool observation = literal.Contains('T');
        string type = observation ? "Observation" : "Patient";
        string key = observation ? "date" : "birthdate";
        string match = await WriteDateBoundaryScalar(replay, graph, type, inside, "Inside");
        await WriteDateBoundaryScalar(replay, graph, type, outside, "Outside");
        if (kind.EndsWith("-overflow", StringComparison.Ordinal))
        {
            await AssertDateBoundarySearch(replay, graph, form, new("non-ap-control", $"{type}?{key}=eq{literal}", []));
        }
        await AssertDateBoundarySearch(replay, graph, form, new(kind, $"{type}?{key}=ap{literal}", [match]));
    }

    [Theory]
    [MemberData(nameof(ReplayCases))]
    public async Task DateBoundaryRetainsSupportedSpellingsAndPrecisionInSearch(string label, FhirReleases.FhirSequenceCodes version,
        CompatibilityTransport transport, CompatibilityFormat format)
    {
        Assert.NotEmpty(label);
        SearchGraph graph = DateBoundaryGraph(version, format);
        await using CompatibilityReplay replay = new(SearchScenario("m12-ordinary", graph), transport);
        string year = await WriteDateBoundaryScalar(replay, graph, "Patient", "2000", "Year");
        string inside = await WriteDateBoundaryScalar(replay, graph, "Patient", "2000-02-29", "Inside");
        string before = await WriteDateBoundaryScalar(replay, graph, "Patient", "2000-02-28", "Before");
        string after = await WriteDateBoundaryScalar(replay, graph, "Patient", "2000-03-01", "After");
        string second = await WriteDateBoundaryScalar(replay, graph, "Observation", "2000-02-29T12:00:00Z", "Second");
        string shifted = await WriteDateBoundaryScalar(replay, graph, "Observation", "2000-02-29T12:00:00.5Z", "Shifted");
        foreach (bool form in new[] { false, true })
        {
            foreach ((string prefix, string[] matches) in new (string, string[])[]
            {
                ("eq", [inside]), ("ne", [year, before, after]), ("gt", [year, after]), ("sa", [after]),
                ("lt", [year, before]), ("eb", [before]), ("ge", [year, inside, after]), ("le", [year, inside, before]),
            })
            {
                await AssertDateBoundarySearch(replay, graph, form, new(prefix, $"Patient?birthdate={prefix}2000-02-29", matches));
            }

            await AssertDateBoundarySearch(replay, graph, form, new("whole-second", "Observation?date=2000-02-29T12:00:00Z", [second]));
            await AssertDateBoundarySearch(replay, graph, form, new("shifted-second",
                "Observation?date=ge2000-02-29T12:00:01.25Z", [shifted]));
        }

        string minute = await WriteDateBoundaryScalar(replay, graph, "Observation", "2000-02-29T12:00:59Z", "MinuteEnd");
        foreach (bool form in new[] { false, true })
        {
            await AssertDateBoundarySearch(replay, graph, form, new("bare-minute", "Observation?date=2000-02-29T12:00", [second, shifted, minute]));
            await AssertDateBoundarySearch(replay, graph, form, new("offset-minute", "Observation?date=2000-02-29T13:00+01:00", [second]));
        }
    }

    public static IEnumerable<object?[]> DateBoundaryLexicalCases()
    {
        DateTimeOffset day = DateBoundaryUtc(2000, 2, 29);
        DateTimeOffset minute = day.AddHours(12).AddMinutes(34);
        DateTimeOffset second = minute.AddSeconds(56);
        DateTimeOffset half = second.AddMilliseconds(500);
        yield return ["2000", DateBoundaryUtc(2000, 1, 1), TimeSpan.TicksPerDay * 366, 365];
        yield return ["2000-02", DateBoundaryUtc(2000, 2, 1), TimeSpan.TicksPerDay * 29, 65];
        yield return ["1900-02", DateBoundaryUtc(1900, 2, 1), TimeSpan.TicksPerDay * 28, 65];
        yield return ["2000-02-29", day, TimeSpan.TicksPerDay, 31];
        yield return ["2000-02-29T12:34", minute, TimeSpan.TicksPerMinute, 1];
        yield return ["2000-02-29T12:34+01:00", minute.AddHours(-1), TimeSpan.TicksPerSecond, 1];
        yield return ["2000-02-29T12:34-01", minute.AddHours(1), TimeSpan.TicksPerSecond, 1];
        yield return ["2000-02-29T12:34+100", minute.AddHours(-1), TimeSpan.TicksPerSecond, 1];
        yield return ["2000-12-31T23:59:59.99999999+1", DateBoundaryUtc(2000, 12, 31, 23), TimeSpan.TicksPerSecond, 1];

        foreach (string suffix in new[] { "", "Z" })
        {
            yield return ["2000-02-29T12:34:56" + suffix, second, TimeSpan.TicksPerSecond, 1];
        }
        yield return ["2000-02-29t12:34:56z", second, TimeSpan.TicksPerSecond, 1];
        foreach (string sign in new[] { "+", "-" })
        {
            foreach (string zone in new[] { "01", "0100", "01:00", "100", "1:00", "1:0", "01:0" })
            {
                yield return ["2000-02-29T12:34:56" + sign + zone, second.AddHours(sign == "+" ? -1 : 1), TimeSpan.TicksPerSecond, 1];
            }
        }
        yield return ["2000-02-29T12:34:56+01:30", second.AddMinutes(-90), TimeSpan.TicksPerSecond, 1];
        yield return ["2000-02-29T12:34:56-01:30", second.AddMinutes(90), TimeSpan.TicksPerSecond, 1];

        foreach ((string fraction, DateTimeOffset start) in new (string, DateTimeOffset)[]
        {
            (".50", half), (".500", half), (".5000", half), (".50000", half), (".5000000", half),
            (".50000000", half), (".500000001", half), (".5000000500", half),
            (".5Z", half), (".50Z", half), (".500Z", half), (".5000Z", half), (".500000Z", half),
            (".5000000Z", half), (".50000005Z", half), (".50000015Z", half.AddTicks(2)), (".500000051Z", half.AddTicks(1)),
            (".00000005Z", second.AddTicks(1)), (".12345665Z", second.AddTicks(1_234_567)),
            (",5Z", half), (".5+1", half.AddHours(-1)), (".50-01", half.AddHours(1)), (".50+0100", half.AddHours(-1)),
            (".5+01:00", half.AddHours(-1)), (".50+01:00", half.AddHours(-1)),
            (".500-01:00", half.AddHours(1)), (".5000+01:00", half.AddHours(-1)),
        })
        {
            yield return ["2000-02-29T12:34:56" + fraction, start, TimeSpan.TicksPerSecond, 1];
        }

        foreach (string rejected in new[]
        {
            "", "1900-02-29", "2000-02-29T12", "2000-02-29T12Z", "2000-02-29T12:34Z",
            "2000-02-29T12:34+1", "2000-02-29T12:34+0100", "2000-02-29T12:34+1:00", "2000-02-29T12:34.50Z",
            "2000-02-29T12:34:56+1", "2000-02-29T12:34:56-1", "2000-02-29T12:34:56.",
            "2000-02-29T12:34:56.5", "2000-02-29T12:34:56.500000", "2000-02-29T12:34:56.50000Z",
            "2000-02-29T12:34:56.5+0100", "2000-02-29T12:34:56.50000+01:00", "2000-02-29T12:34:56.50000005001",
        })
        {
            yield return [rejected, null, 0L, 0];
        }
    }

    [Theory]
    [MemberData(nameof(DateBoundaryLexicalCases))]
    public void DateBoundaryRetainsSupportedSpellingsAndPrecision(string literal, DateTimeOffset? start, long width, int deltaDays)
    {
        using CompatibilityReplay replay = DateBoundaryNativeReplay();
        ParsedSearchParameter query = ParseDateBoundaryQuery(replay, "Patient", "birthdate=2000");
        if (start is DateTimeOffset expected)
        {
            AssertDateBoundaryAdapters(query, literal, expected, expected.AddTicks(width - 1), deltaDays);
        }
        else
        {
            Assert.False(query.TryParseDateString(literal, out DateTimeOffset queryStart, out DateTimeOffset queryEnd, out TimeSpan delta));
            Assert.Equal(DateTimeOffset.MinValue, queryStart);
            Assert.Equal(DateTimeOffset.MaxValue, queryEnd);
            Assert.Equal(TimeSpan.Zero, delta);
            Assert.Equal(literal.Length == 0 ? "Empty date string" : $"Invalid date format: {literal}", query.IgnoredReason);
            Assert.False(ParsedSearchParameter.TryParseFhirDate(literal, out DateTimeOffset storedStart, out DateTimeOffset storedEnd));
            Assert.Equal(DateTimeOffset.MinValue, storedStart);
            Assert.Equal(DateTimeOffset.MaxValue, storedEnd);
        }
    }

    [Theory]
    [InlineData(DateBoundaryInvalidHour, DateBoundaryHourProjection, 9999, 12, 31, 23, 0, 0)]
    [InlineData("2000-02-29T12:34:56-14:01", "2000-03-01T02:35:56Z", 2000, 3, 1, 2, 35, 56)]
    [InlineData("2000-02-29T12:34:56-15:00", "2000-03-01T03:34:56Z", 2000, 3, 1, 3, 34, 56)]
    public void DateBoundaryRejectsMalformedFieldsBeforeUtcProjection(string malformed, string projectedUtc,
        int year, int month, int day, int hour, int minute, int second)
    {
        using CompatibilityReplay replay = DateBoundaryNativeReplay();
        ParsedSearchParameter query = ParseDateBoundaryQuery(replay, "Observation", "date=eq2000");
        Assert.False(query.TryParseDateString(malformed, out _, out _, out _));
        Assert.False(ParsedSearchParameter.TryParseFhirDate(malformed, out _, out _));

        DateTimeOffset start = DateBoundaryUtc(year, month, day, hour, minute, second);
        AssertDateBoundaryAdapters(query, projectedUtc, start, start.AddTicks(TimeSpan.TicksPerSecond - 1), 1);
    }

    [Theory]
    [InlineData("0001", 1, 1, 1, 365)]
    [InlineData("0001-01", 1, 1, 1, 65)]
    [InlineData("0001-01-01", 1, 1, 1, 31)]
    [InlineData(DateBoundaryFirstSecond, 1, 1, 1, 1)]
    [InlineData("9999", 9999, 1, 1, 365)]
    [InlineData("9999-12", 9999, 12, 1, 65)]
    [InlineData("9999-12-31", 9999, 12, 31, 31)]
    [InlineData(DateBoundaryLastSecond, 9999, 12, 31, 1)]
    public void DateBoundaryEndpointsUseIndependentTicks(string literal, int year, int month, int day, int deltaDays)
    {
        using CompatibilityReplay replay = DateBoundaryNativeReplay();
        bool upper = year == 9999;
        DateTimeOffset start = upper && deltaDays == 1 ? DateBoundaryUtc(year, month, day, 23, 59, 59) : DateBoundaryUtc(year, month, day);
        DateTimeOffset end = upper ? DateTimeOffset.MaxValue : deltaDays switch
        {
            365 => DateBoundaryUtc(1, 12, 31, 23, 59, 59).AddTicks(9_999_999),
            65 => DateBoundaryUtc(1, 1, 31, 23, 59, 59).AddTicks(9_999_999),
            31 => DateBoundaryUtc(1, 1, 1, 23, 59, 59).AddTicks(9_999_999),
            _ => DateTimeOffset.MinValue.AddTicks(9_999_999),
        };
        PocoNode instant = new Instant(upper ? DateTimeOffset.MaxValue : DateTimeOffset.MinValue).ToPocoNode();
        ParsedSearchParameter query = ParseDateBoundaryQuery(replay, "Observation", "date=eq" + literal);
        AssertDateBoundaryTicks(start, Assert.Single(query.ValueDateStarts!));
        AssertDateBoundaryTicks(end, Assert.Single(query.ValueDateEnds!));
        Assert.True(EvalDateSearch.TestDate(instant, query));
        AssertDateBoundaryAdapters(query, literal, start, end, deltaDays);
        if (upper)
        {
            Assert.False(EvalDateSearch.TestDate(instant, ParseDateBoundaryQuery(replay, "Observation", "date=gt" + literal)));
            Assert.False(EvalDateSearch.TestDate(instant, ParseDateBoundaryQuery(replay, "Observation", "date=sa" + literal)));
        }
        else
        {
            Assert.False(EvalDateSearch.TestDate(instant, ParseDateBoundaryQuery(replay, "Observation", "date=lt" + literal)));
            Assert.True(EvalDateSearch.TestDate(instant, ParseDateBoundaryQuery(replay, "Observation", "date=lt0001-01-01T00:00:00.0000001Z")));
        }
    }

    [Theory]
    [InlineData("9999", 1, 1)]
    [InlineData("9999-12", 12, 1)]
    [InlineData("9999-12-31", 12, 31)]
    [InlineData(DateBoundaryLastSecond, 12, 31)]
    [InlineData(DateBoundaryLastTick, 12, 31)]
    public void DateBoundaryEndpointsUseIndependentTicksForStoredScalars(string literal, int month, int day)
    {
        using CompatibilityReplay replay = DateBoundaryNativeReplay();
        DateTimeOffset start = literal == DateBoundaryLastTick ? DateTimeOffset.MaxValue : literal == DateBoundaryLastSecond
            ? DateBoundaryUtc(9999, 12, 31, 23, 59, 59) : DateBoundaryUtc(9999, month, day);
        PocoNode scalar = (literal.Contains('T') ? (PrimitiveType)new FhirDateTime(literal) : new Date(literal)).ToPocoNode();
        ParsedSearchParameter query = ParseDateBoundaryQuery(replay, "Observation", "date=eq2000");
        AssertDateBoundaryContained(scalar, query, start, DateTimeOffset.MaxValue);
    }

    [Theory]
    [InlineData(DateBoundaryFirstOffset)]
    [InlineData(DateBoundaryLastOffset)]
    [InlineData(DateBoundaryFirstPartial)]
    [InlineData(DateBoundaryLastPartial)]
    [InlineData("9999-12-31T23:59:59+01:00")]
    [InlineData(DateBoundaryLocalCarry)]
    [InlineData(DateBoundaryBeforeUtc)]
    [InlineData(DateBoundaryAfterUtc)]
    public void DateBoundaryOffsetsProjectInUtcAtParserAndEvaluator(string literal)
    {
        using CompatibilityReplay replay = DateBoundaryNativeReplay();
        ParsedSearchParameter query = ParseDateBoundaryQuery(replay, "Observation", "date=ne2000");
        if (literal is DateBoundaryBeforeUtc or DateBoundaryAfterUtc)
        {
            Assert.False(EvalDateSearch.TestDate(new FhirDateTime(literal).ToPocoNode(), query));
            Assert.False(ParsedSearchParameter.TryParseFhirDate(literal, out _, out _));
            Assert.False(query.TryParseDateString(literal, out _, out _, out _));
            return;
        }

        DateTimeOffset lastSecond = DateBoundaryUtc(9999, 12, 31, 23, 59, 59);
        (DateTimeOffset start, DateTimeOffset end) = literal switch
        {
            DateBoundaryFirstOffset => (DateTimeOffset.MinValue, DateTimeOffset.MinValue.AddTicks(9_999_999)),
            DateBoundaryLastOffset => (lastSecond, DateTimeOffset.MaxValue),
            DateBoundaryFirstPartial => (DateTimeOffset.MinValue, DateTimeOffset.MinValue.AddTicks(4_999_999)),
            DateBoundaryLastPartial => (lastSecond.AddMilliseconds(500), DateTimeOffset.MaxValue),
            "9999-12-31T23:59:59+01:00" => (lastSecond.AddHours(-1), lastSecond.AddHours(-1).AddTicks(9_999_999)),
            DateBoundaryLocalCarry => (DateBoundaryUtc(9999, 12, 31, 23), DateBoundaryUtc(9999, 12, 31, 23).AddTicks(9_999_999)),
            _ => throw new ArgumentOutOfRangeException(nameof(literal)),
        };
        if (literal is DateBoundaryFirstPartial or DateBoundaryLastPartial)
        {
            // Use an independently set query even for a primitive Firely cannot accept on the wire.
            AssertDateBoundaryContained(new FhirDateTime(literal).ToPocoNode(),
                ParseDateBoundaryQuery(replay, "Observation", "date=eq2000"), start, end);
        }
        AssertDateBoundaryAdapters(query, literal, start, end, 1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DateBoundaryApproximateWindowsIncludeEdgeTicksAndOverlap(bool upper)
    {
        using CompatibilityReplay replay = DateBoundaryNativeReplay();
        ParsedSearchParameter query = ParseDateBoundaryQuery(replay, "Observation", upper ? "date=ap9999-12-31" : "date=ap0001-01-01");
        DateTimeOffset edge = upper ? DateBoundaryUtc(9999, 11, 30) : DateBoundaryUtc(1, 2, 1, 23, 59, 59).AddTicks(9_999_999);
        DateTimeOffset outside = edge.AddTicks(upper ? -1 : 1);
        DateTimeOffset cap = upper ? DateTimeOffset.MaxValue : DateTimeOffset.MinValue;
        Assert.True(EvalDateSearch.TestDate(new Instant(edge).ToPocoNode(), query));
        Assert.False(EvalDateSearch.TestDate(new Instant(outside).ToPocoNode(), query));
        Assert.True(EvalDateSearch.TestDate(new Instant(cap).ToPocoNode(), query));
        Period crossing = new()
        {
            StartElement = new FhirDateTime(upper ? outside : edge),
            EndElement = new FhirDateTime(upper ? edge : outside),
        };
        Assert.True(EvalDateSearch.TestDate(crossing.ToPocoNode(), query));
    }

    [Theory]
    [InlineData("Patient", "birthdate", "0000", "1999-12-31")]
    [InlineData("Observation", "date", DateBoundaryInvalidHour, "1999-12-31T23:59:59Z")]
    public void DateBoundaryInvalidStoredPrimitiveIsNotAMatch(string type, string key, string invalid, string valid)
    {
        using CompatibilityReplay replay = DateBoundaryNativeReplay();
        IFhirStore store = replay.GetOwnedStore("m12-native");
        ParsedSearchParameter query = ParseDateBoundaryQuery(replay, type, key + "=ne2000");
        bool patient = type == "Patient";
        PrimitiveType invalidScalar = patient ? new Date(invalid) : new FhirDateTime(invalid);
        PrimitiveType validScalar = patient ? new Date(valid) : new FhirDateTime(valid);
        Resource control = patient
            ? new Patient { BirthDateElement = (Date)validScalar }
            : new Observation { Status = ObservationStatus.Final, Code = new() { Text = "control" }, Effective = validScalar };
        Resource malformed = patient
            ? new Patient { BirthDateElement = (Date)invalidScalar }
            : new Observation { Status = ObservationStatus.Final, Code = new() { Text = "malformed" }, Effective = invalidScalar };

        Assert.True(TryWriteDateBoundaryNative(store, control, out FhirResponseContext controlResponse));
        PocoNode controlNode = Assert.Single(ExtractDateBoundaryNative(query, Assert.IsAssignableFrom<Resource>(controlResponse.Resource)));
        Assert.Equal(valid, Assert.IsAssignableFrom<PrimitiveType>(controlNode.Poco).JsonValue);
        Assert.True(EvalDateSearch.TestDate(controlNode, query));
        if (!patient)
        {
            Assert.True(EvalDateSearch.TestDate(new FhirDateTime(DateBoundaryHourProjection).ToPocoNode(), query));
        }

        bool written = TryWriteDateBoundaryNative(store, malformed, out FhirResponseContext malformedResponse);
        PocoNode[] extracted = written && malformedResponse.Resource is Resource admitted
            ? ExtractDateBoundaryNative(query, admitted) : [];
        if (extracted.Length == 0)
        {
            // Admission/extraction is not evidence that the evaluator rejects this primitive.
            Assert.Equal(invalid, invalidScalar.JsonValue);
            Assert.False(EvalDateSearch.TestDate(invalidScalar.ToPocoNode(), query));
            return;
        }

        PocoNode malformedNode = Assert.Single(extracted);
        Assert.Equal(invalid, Assert.IsAssignableFrom<PrimitiveType>(malformedNode.Poco).JsonValue);
        Assert.False(EvalDateSearch.TestDate(malformedNode, query));
        Assert.True(store.PerformInteraction(new(store, "GET", $"{type}?{key}=ne2000"),
            out FhirResponseContext response, serializeReturn: false));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Bundle bundle = Assert.IsType<Bundle>(response.Resource);
        Assert.Equal(1, bundle.Total);
        Assert.Equal(controlResponse.Id, Assert.IsAssignableFrom<Resource>(Assert.Single(bundle.Entry).Resource).Id);
    }

    private static SearchGraph DateBoundaryGraph(FhirReleases.FhirSequenceCodes version, CompatibilityFormat format) =>
        new(Tenant(version, $"m12-{VersionName(version)}"), format, new(StringComparer.Ordinal), new(StringComparer.Ordinal));

    private static async Task<string> WriteDateBoundaryScalar(CompatibilityReplay replay, SearchGraph graph,
        string type, string value, string name, bool period = false)
    {
        bool patient = type == "Patient";
        string primitive = patient ? "birthDate" : period ? "effectivePeriod" : "effectiveDateTime";
        string content = (graph.Format, patient, period) switch
        {
            (CompatibilityFormat.Json, true, _) =>
                $$$"""{"resourceType":"Patient","name":[{"family":"{{{name}}}"}],"birthDate":"{{{value}}}"}""",
            (CompatibilityFormat.Json, false, false) =>
                $$$"""{"resourceType":"Observation","status":"final","code":{"text":"{{{name}}}"},"effectiveDateTime":"{{{value}}}"}""",
            (CompatibilityFormat.Json, false, true) =>
                $$$"""{"resourceType":"Observation","status":"final","code":{"text":"{{{name}}}"},"effectivePeriod":{"start":"{{{value}}}","end":"{{{value}}}"}}""",
            (_, true, _) =>
                $"""<Patient xmlns="http://hl7.org/fhir"><name><family value="{name}"/></name><birthDate value="{value}"/></Patient>""",
            (_, false, false) =>
                $"""<Observation xmlns="http://hl7.org/fhir"><status value="final"/><code><text value="{name}"/></code><effectiveDateTime value="{value}"/></Observation>""",
            _ =>
                $"""<Observation xmlns="http://hl7.org/fhir"><status value="final"/><code><text value="{name}"/></code><effectivePeriod><start value="{value}"/><end value="{value}"/></effectivePeriod></Observation>""",
        };
        GeneratedResourceExpectation generated = new($"date-{name}", type, "1");
        await replay.StepAsync(new CompatibilityStep("write-" + name,
            new CompatibilityRequest(graph.Tenant.Name, "POST", type) { Format = graph.Format, Content = content },
            ResourceResponse("write-" + name, content, generated, HttpStatusCode.Created)));
        CompatibilityResponse read = await replay.StepAsync(BoundRead("read-" + name, graph.Tenant, graph.Format, content, generated));
        string resource = ExchangeResource(read);
        if (period)
        {
            string interval = ExchangeChild(resource, graph.Format, primitive);
            Assert.Equal(value, ExchangePrimitive(interval, graph.Format, "start"));
            Assert.Equal(value, ExchangePrimitive(interval, graph.Format, "end"));
        }
        else
        {
            Assert.Equal(value, ExchangePrimitive(resource, graph.Format, primitive));
        }

        string identity = type + "/" + replay.Bindings.Resources[generated.Name].Id;
        graph.Resources.Add(identity, content);
        graph.Generated.Add(identity, generated);
        return identity;
    }

    private static Task AssertDateBoundarySearch(CompatibilityReplay replay, SearchGraph graph, bool form, SearchProbe probe, bool strict = true) =>
        AssertSearchRequest(replay, graph, probe, SearchRequest(graph, probe.Path, form, DateBoundaryHeaders(strict)));

    private static async Task AssertDateBoundaryFailure(CompatibilityReplay replay, SearchGraph graph, bool form,
        string id, string path, string key, string? rejectedAlternative = null)
    {
        CompatibilityRequest request = SearchRequest(graph, path, form, DateBoundaryHeaders(strict: true));
        string diagnostic = rejectedAlternative is null
            ? $"Search parameter '{key}' was rejected as malformed (modifier, chain, or value parse failure)."
            : $"Search parameter '{key}' value '{rejectedAlternative}' was rejected as malformed.";
        try
        {
            CompatibilityResponse response = await replay.SendAsync(request);
            OutcomeBodyExpectation outcome = new($"{id}-{request.Method}-outcome",
                [new("error", "invalid", $"{diagnostic} (see http://hl7.org/fhir/{graph.Tenant.Version}/search.html#errors)")]);
            WireContractAssertions.Response(
                new CompatibilityResponseExpectation(HttpStatusCode.BadRequest, false, outcome, new EmptyBodyExpectation(), outcome)
                {
                    HttpHeaders = NoExchangeHeaders(),
                    DirectHeaders = NoExchangeHeaders(),
                }, response, graph.Format, graph.Tenant.BaseUrl, replay.Bindings);
            if (response.Direct is CompatibilityDirectResponse direct)
            {
                Assert.Empty(direct.ResourceType);
                Assert.Empty(direct.Id);
            }
        }
        catch (Exception exception) when (exception is Xunit.Sdk.XunitException or InvalidOperationException)
        {
            throw new InvalidOperationException($"{SearchDiagnostic(replay, graph, id, request)}: {exception.Message}", exception);
        }
    }

    private static Dictionary<string, string> DateBoundaryHeaders(bool strict) =>
        new() { ["Prefer"] = strict ? "handling=strict" : "handling=lenient" };

    private static CompatibilityReplay DateBoundaryNativeReplay()
    {
        SearchGraph graph = new(Tenant(FhirReleases.FhirSequenceCodes.R4, "m12-native"),
            CompatibilityFormat.Json, new(StringComparer.Ordinal), new(StringComparer.Ordinal));
        return new(SearchScenario("m12-native", graph), CompatibilityTransport.Direct);
    }

    private static ParsedSearchParameter ParseDateBoundaryQuery(CompatibilityReplay replay, string type, string query)
    {
        IFhirStore store = replay.GetOwnedStore("m12-native");
        ParsedSearchParameter parsed = Assert.Single(ParsedSearchParameter.Parse(query,
            (VersionedFhirStore)store, (IVersionedResourceStore)store[type], type, out List<string> unknown));
        Assert.Empty(unknown);
        Assert.False(parsed.IgnoredParameter);
        Assert.False(Assert.Single(parsed.IgnoredValueFlags));
        return parsed;
    }

    private static void AssertDateBoundaryAdapters(ParsedSearchParameter query, string literal,
        DateTimeOffset expectedStart, DateTimeOffset expectedEnd, int deltaDays)
    {
        Assert.True(query.TryParseDateString(literal, out DateTimeOffset start, out DateTimeOffset end, out TimeSpan delta), literal);
        AssertDateBoundaryTicks(expectedStart, start);
        AssertDateBoundaryTicks(expectedEnd, end);
        Assert.Equal(TimeSpan.FromDays(deltaDays), delta);
        Assert.True(ParsedSearchParameter.TryParseFhirDate(literal, out DateTimeOffset storedStart, out DateTimeOffset storedEnd), literal);
        AssertDateBoundaryTicks(expectedStart, storedStart);
        AssertDateBoundaryTicks(expectedEnd, storedEnd);
    }

    private static DateTimeOffset DateBoundaryUtc(int year, int month, int day, int hour = 0, int minute = 0, int second = 0) =>
        new(year, month, day, hour, minute, second, TimeSpan.Zero);

    private static void AssertDateBoundaryTicks(DateTimeOffset expected, DateTimeOffset actual)
    {
        Assert.Equal(TimeSpan.Zero, actual.Offset);
        Assert.Equal(expected.Ticks, actual.Ticks);
    }

    private static void AssertDateBoundaryContained(PocoNode scalar, ParsedSearchParameter query, DateTimeOffset start, DateTimeOffset end)
    {
        query.ValueDateStarts = [start];
        query.ValueDateEnds = [end];
        Assert.True(EvalDateSearch.TestDate(scalar, query));
        query.ValueDateEnds = [end.AddTicks(-1)];
        Assert.False(EvalDateSearch.TestDate(scalar, query));
        if (start < end)
        {
            query.ValueDateEnds = [end];
            query.ValueDateStarts = [start.AddTicks(1)];
            Assert.False(EvalDateSearch.TestDate(scalar, query));
        }
    }

    private static bool TryWriteDateBoundaryNative(IFhirStore store, Resource input, out FhirResponseContext response)
    {
        bool written = store.PerformInteraction(new(store, "POST", input.TypeName) { SourceObject = input },
            out response, serializeReturn: false);
        if (written)
        {
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            Assert.Empty(response.SerializedResource);
            Assert.NotEmpty(response.Id);
        }
        return written;
    }

    private static PocoNode[] ExtractDateBoundaryNative(ParsedSearchParameter query, Resource resource)
    {
        PocoNode node = resource.ToPocoNode();
        return query.CompiledExpression!.Invoke(node, new FhirEvaluationContext { Resource = node }).ToArray();
    }
}
