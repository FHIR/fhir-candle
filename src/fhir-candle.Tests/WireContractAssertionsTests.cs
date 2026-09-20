// <copyright file="WireContractAssertionsTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using System.Globalization;
using System.Net;
using System.Text.Json;
using fhir.candle.Tests.Extensions;
using fhir.candle.Tests.Models;

namespace fhir.candle.Tests;

/// <summary>Tests the oracle itself using deliberately wrong wire examples, without starting any stores.</summary>
public class WireContractAssertionsTests
{
    private const string BaseUrl = "http://localhost/fhir/oak-r4";

    /// <summary>Both same-format comparers must reject the applicable counterexamples.</summary>
    public static IEnumerable<object[]> Formats =>
    [
        ["JSON", CompatibilityFormat.Json],
        ["XML", CompatibilityFormat.Xml],
    ];

    /// <summary>Scale, large integers, and high-precision fractional tokens must not be rounded.</summary>
    public static IEnumerable<object[]> DecimalCases
    {
        get
        {
            foreach (object[] row in Formats)
            {
                foreach ((string expected, string actual) in new[]
                {
                    ("1.2300", "1.23"),
                    ("9007199254740993", "9007199254740992"),
                    ("0.123456789012345678901234567891", "0.123456789012345678901234567890"),
                    ("1.20e2", "120"),
                })
                {
                    yield return [$"{row[0]} / {expected}", row[1], expected, actual];
                }
            }
        }
    }

    /// <summary>Each generated value and each relationship gets an independently broken example.</summary>
    public static IEnumerable<object[]> BrokenBindings
    {
        get
        {
            foreach (object[] row in Formats)
            {
                foreach (string defect in new[]
                {
                    "id", "version", "instant", "missing-meta", "invalid-id", "invalid-version",
                    "etag", "strong-etag", "tenant", "location-id", "last-modified",
                    "reuse", "rebind", "update-id", "update-version", "update-time",
                })
                {
                    yield return [$"{row[0]} / {defect}", row[1], defect];
                }
            }
        }
    }

    /// <summary>Each generated field must reject a final LF in either valid wire encoding.</summary>
    public static IEnumerable<object[]> TrailingNewlineBindings
    {
        get
        {
            foreach (object[] row in Formats)
            {
                foreach (string defect in new[] { "id", "version", "instant" })
                {
                    yield return [$"{row[0]} / {defect}", row[1], defect];
                }
            }
        }
    }

    /// <summary>A primitive extension is content, even when its primitive value is unchanged.</summary>
    [Theory]
    [MemberData(nameof(Formats))]
    public void RejectsDroppedPrimitiveExtension(string label, CompatibilityFormat format)
    {
        Assert.NotEmpty(label);
        string expected = format == CompatibilityFormat.Json
            ? """{"resourceType":"Patient","birthDate":"2001","_birthDate":{"extension":[{"url":"urn:example:precision","valueString":"year"}]}}"""
            : """<Patient xmlns="http://hl7.org/fhir"><birthDate value="2001"><extension url="urn:example:precision"><valueString value="year"/></extension></birthDate></Patient>""";
        string actual = format == CompatibilityFormat.Json
            ? """{"resourceType":"Patient","birthDate":"2001"}"""
            : """<Patient xmlns="http://hl7.org/fhir"><birthDate value="2001"/></Patient>""";
        Mismatch(expected, actual, format);
    }

    /// <summary>Success-like status substitutions and swallowed failures are both observable.</summary>
    [Theory]
    [InlineData(HttpStatusCode.Created, HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.NotFound, HttpStatusCode.OK)]
    public void RejectsChangedHttpStatus(HttpStatusCode expected, HttpStatusCode actual)
    {
        WireContractAssertions.Status(expected, expected);
        Rejected(() => WireContractAssertions.Status(expected, actual));
    }

    /// <summary>A reference's tenant prefix cannot be normalized away.</summary>
    [Theory]
    [MemberData(nameof(Formats))]
    public void RejectsWrongTenantReference(string label, CompatibilityFormat format)
    {
        Assert.NotEmpty(label);
        string expected = format == CompatibilityFormat.Json
            ? """{"resourceType":"Observation","subject":{"reference":"http://localhost/fhir/oak-r4/Patient/shared"}}"""
            : """<Observation xmlns="http://hl7.org/fhir"><subject><reference value="http://localhost/fhir/oak-r4/Patient/shared"/></subject></Observation>""";
        Mismatch(expected, expected.Replace("/oak-r4/", "/elm-r4/", StringComparison.Ordinal), format);
    }

    /// <summary>Array and repeated-element order remain significant.</summary>
    [Theory]
    [MemberData(nameof(Formats))]
    public void RejectsArrayReordering(string label, CompatibilityFormat format)
    {
        Assert.NotEmpty(label);
        string expected = format == CompatibilityFormat.Json
            ? """{"resourceType":"Patient","name":[{"family":"First"},{"family":"Second"}]}"""
            : """<Patient xmlns="http://hl7.org/fhir"><name><family value="First"/></name><name><family value="Second"/></name></Patient>""";
        string actual = format == CompatibilityFormat.Json
            ? """{"resourceType":"Patient","name":[{"family":"Second"},{"family":"First"}]}"""
            : """<Patient xmlns="http://hl7.org/fhir"><name><family value="Second"/></name><name><family value="First"/></name></Patient>""";
        Mismatch(expected, actual, format);
    }

    /// <summary>Number token precision is checked without a double or decimal round trip.</summary>
    [Theory]
    [MemberData(nameof(DecimalCases))]
    public void RejectsLostDecimalPrecision(string label, CompatibilityFormat format, string expectedNumber, string actualNumber)
    {
        Assert.NotEmpty(label);
        string expected = format == CompatibilityFormat.Json
            ? $$$"""{"resourceType":"Observation","valueQuantity":{"value":{{{expectedNumber}}}}}"""
            : $"""<Observation xmlns="http://hl7.org/fhir"><valueQuantity><value value="{expectedNumber}"/></valueQuantity></Observation>""";
        string actual = expected.Replace(expectedNumber, actualNumber, StringComparison.Ordinal);
        WireContractAssertions.Content(expected, expected, format);
        Mismatch(expected, actual, format);
    }

    /// <summary>Missing is not null, empty, false, or present; additions are not silently ignored.</summary>
    [Theory]
    [MemberData(nameof(Formats))]
    public void RejectsMissingOrAddedElement(string label, CompatibilityFormat format)
    {
        Assert.NotEmpty(label);
        string absent = format == CompatibilityFormat.Json
            ? """{"resourceType":"Patient"}"""
            : """<Patient xmlns="http://hl7.org/fhir"/>""";
        string present = format == CompatibilityFormat.Json
            ? """{"resourceType":"Patient","active":false}"""
            : """<Patient xmlns="http://hl7.org/fhir"><active value="false"/></Patient>""";
        string empty = format == CompatibilityFormat.Json
            ? """{"resourceType":"Patient","active":null}"""
            : """<Patient xmlns="http://hl7.org/fhir"><active/></Patient>""";
        Mismatch(present, absent, format);
        Mismatch(absent, present, format);
        Mismatch(absent, empty, format);
        Mismatch(present, empty, format);
        if (format == CompatibilityFormat.Json)
        {
            Mismatch(absent, """{"resourceType":"Patient","name":[]}""", format);
            Mismatch(present, """{"resourceType":"Patient","active":"false"}""", format);
        }
    }

    /// <summary>Both mixed-content spaces and whitespace-only XHTML text nodes are content.</summary>
    [Theory]
    [MemberData(nameof(Formats))]
    public void PreservesNarrativeWhitespace(string label, CompatibilityFormat format)
    {
        Assert.NotEmpty(label);
        const string div = """<div xmlns="http://www.w3.org/1999/xhtml"><p>Alpha <b>beta</b> gamma</p> <p> tail </p></div>""";
        string expected = Narrative(format, div);
        WireContractAssertions.Content(expected, expected, format);
        Mismatch(expected, Narrative(format, div.Replace(" gamma", "gamma", StringComparison.Ordinal)), format);
        Mismatch(expected, Narrative(format, div.Replace("</p> <p>", "</p><p>", StringComparison.Ordinal)), format);
        Mismatch(expected, Narrative(format, div.Replace(" tail ", "tail", StringComparison.Ordinal)), format);
    }

    /// <summary>Only object order and non-significant FHIR XML layout are general normalizations.</summary>
    [Fact]
    public void IgnoresOnlyJsonPropertyOrderAndXmlFormatting()
    {
        const string json = """{"resourceType":"Patient","id":"one","meta":{"versionId":"1"},"active":true}""";
        const string reordered = """
            {
              "active": true,
              "meta": { "versionId": "1" },
              "id": "one",
              "resourceType": "Patient"
            }
            """;
        WireContractAssertions.Content(json, reordered, CompatibilityFormat.Json);
        Mismatch(json, json.Replace("\"versionId\":\"1\"", "\"versionId\":\"2\"", StringComparison.Ordinal), CompatibilityFormat.Json);

        const string xml = """<Patient xmlns="http://hl7.org/fhir"><id value="one"/><birthDate value="2001" id="birth"><extension url="urn:example:precision"><valueString value="year"/></extension></birthDate></Patient>""";
        const string formatted = """
            <?xml version="1.0"?>
            <f:Patient xmlns:f="http://hl7.org/fhir">
              <f:id value="one" />
              <f:birthDate id="birth" value="2001">
                <f:extension url="urn:example:precision">
                  <f:valueString value="year" />
                </f:extension>
              </f:birthDate>
            </f:Patient>
            """;
        WireContractAssertions.Content(xml, formatted, CompatibilityFormat.Xml);
        Mismatch(xml, xml.Replace("value=\"one\"", "value=\"two\"", StringComparison.Ordinal), CompatibilityFormat.Xml);
    }

    /// <summary>Choice element names cannot be mapped onto a common name by the oracle.</summary>
    [Theory]
    [MemberData(nameof(Formats))]
    public void RejectsChangedChoiceElement(string label, CompatibilityFormat format)
    {
        Assert.NotEmpty(label);
        string expected = format == CompatibilityFormat.Json
            ? """{"resourceType":"Observation","effectiveDateTime":"2024-01-01T00:00:00Z"}"""
            : """<Observation xmlns="http://hl7.org/fhir"><effectiveDateTime value="2024-01-01T00:00:00Z"/></Observation>""";
        Mismatch(expected, expected.Replace("effectiveDateTime", "effectiveInstant", StringComparison.Ordinal), format);
    }

    /// <summary>A successful initial bind must not make any subsequent broken relationship pass.</summary>
    [Theory]
    [MemberData(nameof(BrokenBindings))]
    public void RejectsBrokenGeneratedValueBinding(string label, CompatibilityFormat format, string defect)
    {
        Assert.NotEmpty(label);
        CompatibilityBindings bindings = new();
        string timestamp = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        GeneratedResourceExpectation expected = new("patient-v1", "Patient", "1", "one");
        string body = BoundPatient(format, "one", "1", timestamp);
        GeneratedResourceBinding binding = WireContractAssertions.BindResource(expected, body, format, BaseUrl, bindings);
        string modified = binding.Instant.ToString("r", CultureInfo.InvariantCulture);
        WireContractAssertions.ResourceHeaders(binding, "W/\"1\"", binding.Location, modified);

        GeneratedResourceExpectation fresh = expected with { Name = "other" };
        GeneratedResourceExpectation update = new("patient-v2", "Patient", null, PreviousBinding: "patient-v1");
        Action broken = defect switch
        {
            "id" => () => WireContractAssertions.BindResource(fresh, BoundPatient(format, "two", "1", timestamp), format, BaseUrl, bindings),
            "version" => () => WireContractAssertions.BindResource(fresh, BoundPatient(format, "one", "2", timestamp), format, BaseUrl, bindings),
            "instant" => () => WireContractAssertions.BindResource(fresh, BoundPatient(format, "one", "1", "2026-09-19"), format, BaseUrl, bindings),
            "missing-meta" => () => WireContractAssertions.BindResource(fresh, Patient(format), format, BaseUrl, bindings),
            "invalid-id" => () => WireContractAssertions.BindResource(fresh with { ExpectedId = null }, BoundPatient(format, "bad/id", "1", timestamp), format, BaseUrl, bindings),
            "invalid-version" => () => WireContractAssertions.BindResource(fresh with { ExpectedVersion = null }, BoundPatient(format, "one", "not-a-version", timestamp), format, BaseUrl, bindings),
            "etag" => () => WireContractAssertions.ResourceHeaders(binding, "W/\"2\"", binding.Location, modified),
            "strong-etag" => () => WireContractAssertions.ResourceHeaders(binding, "\"1\"", binding.Location, modified),
            "tenant" => () => WireContractAssertions.ResourceHeaders(binding, "W/\"1\"", binding.Location.Replace("oak-r4", "elm-r4", StringComparison.Ordinal), modified),
            "location-id" => () => WireContractAssertions.ResourceHeaders(binding, "W/\"1\"", BaseUrl + "/Patient/two", modified),
            "last-modified" => () => WireContractAssertions.ResourceHeaders(binding, "W/\"1\"", binding.Location, binding.Instant.AddSeconds(1).ToString("r", CultureInfo.InvariantCulture)),
            "reuse" => () => WireContractAssertions.BindResource(expected with { Mode = CompatibilityBindingMode.Reuse },
                BoundPatient(format, "one", "1", binding.Instant.AddTicks(1).ToString("O", CultureInfo.InvariantCulture)), format, BaseUrl, bindings),
            "rebind" => () => WireContractAssertions.BindResource(expected, body, format, BaseUrl, bindings),
            "update-id" => () => WireContractAssertions.BindResource(update, BoundPatient(format, "two", "2", timestamp), format, BaseUrl, bindings),
            "update-version" => () => WireContractAssertions.BindResource(update, BoundPatient(format, "one", "3", timestamp), format, BaseUrl, bindings),
            "update-time" => () => WireContractAssertions.BindResource(update,
                BoundPatient(format, "one", "2", binding.Instant.AddTicks(-1).ToString("O", CultureInfo.InvariantCulture)), format, BaseUrl, bindings),
            _ => throw new ArgumentOutOfRangeException(nameof(defect)),
        };
        Rejected(broken);
    }

    /// <summary>Matching generated content and headers cannot make invalid syntax approve itself.</summary>
    [Theory]
    [MemberData(nameof(TrailingNewlineBindings))]
    public void RejectsGeneratedValueTrailingNewlines(string label, CompatibilityFormat format, string field)
    {
        Assert.NotEmpty(label);
        CompatibilityBindings validBindings = new();
        CompatibilityBindings invalidBindings = new();
        DateTimeOffset instant = DateTimeOffset.UtcNow;
        string timestamp = instant.ToString("O", CultureInfo.InvariantCulture);
        string modified = instant.ToString("r", CultureInfo.InvariantCulture);
        ResourceBodyExpectation expected = new(
            format == CompatibilityFormat.Json
                ? """{"resourceType":"Patient","language":"en"}"""
                : """<Patient xmlns="http://hl7.org/fhir"><language value="en"/></Patient>""",
            new("patient", "Patient", null));
        WireContractAssertions.Body(expected, BoundPatient(format, "one", "1", timestamp), format, BaseUrl, validBindings);
        WireContractAssertions.ResourceHeaders(WireContractAssertions.ResourceBinding(validBindings, "patient"),
            "W/\"1\"", BaseUrl + "/Patient/one", modified);

        string id = field == "id" ? "one\n" : "one";
        string version = field == "version" ? "1\n" : "1";
        string lastUpdated = field == "instant" ? timestamp + "\n" : timestamp;
        string escapedNewline = format == CompatibilityFormat.Json ? @"\n" : "&#xA;";
        string actual = BoundPatient(format,
            id.Replace("\n", escapedNewline, StringComparison.Ordinal),
            version.Replace("\n", escapedNewline, StringComparison.Ordinal),
            lastUpdated.Replace("\n", escapedNewline, StringComparison.Ordinal));
        WireContractAssertions.Content(actual, actual, format);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
        {
            WireContractAssertions.Body(expected, actual, format, BaseUrl, invalidBindings);
            WireContractAssertions.ResourceHeaders(WireContractAssertions.ResourceBinding(invalidBindings, "patient"),
                $"W/\"{version}\"", $"{BaseUrl}/Patient/{id}", modified);
        });
        string failure = field switch
        {
            "id" => "Wire contract: Invalid FHIR id:",
            "version" => "Wire contract: Invalid store version:",
            "instant" => "Wire contract: Expected a timezone-qualified FHIR instant,",
            _ => throw new ArgumentOutOfRangeException(nameof(field)),
        };
        Assert.StartsWith(failure, exception.Message, StringComparison.Ordinal);
        Assert.Empty(invalidBindings.Resources);
    }

    /// <summary>FHIR subsecond timestamps and whole-second HTTP dates describe the same modification.</summary>
    [Theory]
    [MemberData(nameof(Formats))]
    public void UsesHttpDatePrecisionForLastModified(string label, CompatibilityFormat format)
    {
        Assert.NotEmpty(label);
        CompatibilityBindings bindings = new();
        long ticks = DateTimeOffset.UtcNow.UtcTicks;
        DateTimeOffset instant = new(ticks - (ticks % TimeSpan.TicksPerSecond) + 1234567, TimeSpan.Zero);
        string timestamp = instant.ToString("O", CultureInfo.InvariantCulture);
        ResourceBodyExpectation expected = new(Patient(format), new("patient", "Patient", "1", "one"));
        WireContractAssertions.Body(expected, BoundPatient(format, "one", "1", timestamp), format, BaseUrl, bindings);
        GeneratedResourceBinding binding = bindings.Resources["patient"];
        WireContractAssertions.ResourceHeaders(binding, "W/\"1\"", BaseUrl + "/Patient/one", instant.ToString("r", CultureInfo.InvariantCulture));
        WireContractAssertions.Body(expected with { Generated = expected.Generated with { Mode = CompatibilityBindingMode.Reuse } },
            BoundPatient(format, "one", "1", timestamp), format, BaseUrl, bindings);
    }

    /// <summary>Declaring generated version/time does not discard additional metadata.</summary>
    [Theory]
    [MemberData(nameof(Formats))]
    public void RejectsUndeclaredMetadata(string label, CompatibilityFormat format)
    {
        Assert.NotEmpty(label);
        CompatibilityBindings bindings = new();
        string timestamp = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        string actual = BoundPatient(format, "one", "1", timestamp);
        actual = format == CompatibilityFormat.Json
            ? actual.Replace("\"versionId\":", "\"source\":\"urn:unexpected\",\"versionId\":", StringComparison.Ordinal)
            : actual.Replace("</meta>", "<source value=\"urn:unexpected\"/></meta>", StringComparison.Ordinal);
        Rejected(() => WireContractAssertions.Body(
            new ResourceBodyExpectation(Patient(format), new("patient", "Patient", "1", "one")), actual, format, BaseUrl, bindings));
    }

    /// <summary>Duplicate JSON members, XML space preservation, and non-XML whitespace are not formatting.</summary>
    [Fact]
    public void RejectsAmbiguousOrSignificantFormatting()
    {
        Mismatch("""{"resourceType":"Patient","id":"one"}""", """{"resourceType":"Patient","id":"one","id":"one"}""", CompatibilityFormat.Json);
        const string preserve = """<Patient xmlns="http://hl7.org/fhir" xml:space="preserve"><id value="one"/> <active value="true"/></Patient>""";
        Mismatch(preserve, preserve.Replace("/> <", "/><", StringComparison.Ordinal), CompatibilityFormat.Xml);
        string nonBreaking = "<Patient xmlns=\"http://hl7.org/fhir\"><id value=\"one\"/>\u00A0<active value=\"true\"/></Patient>";
        Mismatch(nonBreaking, nonBreaking.Replace("\u00A0", string.Empty, StringComparison.Ordinal), CompatibilityFormat.Xml);
    }

    /// <summary>Direct callers observe both serialized fields, not a body selected like HTTP.</summary>
    [Fact]
    public void DirectResourceAndOutcomeAreNotInterchangeable()
    {
        const string resource = """{"resourceType":"Patient","id":"one"}""";
        const string outcome = """{"resourceType":"OperationOutcome","issue":[{"severity":"information","code":"success"}]}""";
        CompatibilityResponseExpectation expected = new(HttpStatusCode.OK, true,
            new ExactBodyExpectation(resource), new ExactBodyExpectation(resource), new ExactBodyExpectation(outcome));
        CompatibilityDirectResponse direct = new(true, resource, outcome, "Patient", "one", string.Empty, string.Empty, string.Empty);
        CompatibilityResponse actual = new(CompatibilityTransport.Direct, HttpStatusCode.OK, "application/fhir+json")
        {
            Direct = direct,
        };
        WireContractAssertions.Response(expected, actual, CompatibilityFormat.Json, BaseUrl, new());
        Rejected(() => WireContractAssertions.Response(expected,
            actual with { Direct = direct with { SerializedResource = outcome, SerializedOutcome = resource } },
            CompatibilityFormat.Json, BaseUrl, new()));
    }

    /// <summary>HTTP assertions inspect only the selected body and real headers, never inventing missing ones.</summary>
    [Fact]
    public void HttpDoesNotRequireUnsentDirectFieldsOrInventHeaders()
    {
        const string resource = """{"resourceType":"Patient","id":"one"}""";
        CompatibilityResponseExpectation expected = new(HttpStatusCode.OK, true,
            new ExactBodyExpectation(resource), new ExactBodyExpectation(resource),
            new OutcomeBodyExpectation("not-on-wire", [new("information", "success")]));
        CompatibilityResponse actual = new(CompatibilityTransport.Http, HttpStatusCode.OK, "application/fhir+json")
        {
            HttpBody = resource,
        };
        CompatibilityBindings bindings = new();
        WireContractAssertions.Response(expected, actual, CompatibilityFormat.Json, BaseUrl, bindings);
        Assert.Empty(bindings.Values);
        Rejected(() => WireContractAssertions.Response(expected with
        {
            HttpHeaders = [new("Location", BaseUrl + "/Patient/one")],
        }, actual, CompatibilityFormat.Json, BaseUrl, bindings));
    }

    private static string Patient(CompatibilityFormat format) => format == CompatibilityFormat.Json
        ? """{"resourceType":"Patient","id":"one","language":"en"}"""
        : """<Patient xmlns="http://hl7.org/fhir"><id value="one"/><language value="en"/></Patient>""";

    private static string BoundPatient(CompatibilityFormat format, string id, string version, string timestamp) => format == CompatibilityFormat.Json
        ? $$$"""{"resourceType":"Patient","id":"{{{id}}}","meta":{"versionId":"{{{version}}}","lastUpdated":"{{{timestamp}}}"},"language":"en"}"""
        : $"""<Patient xmlns="http://hl7.org/fhir"><id value="{id}"/><meta><versionId value="{version}"/><lastUpdated value="{timestamp}"/></meta><language value="en"/></Patient>""";

    private static string Narrative(CompatibilityFormat format, string div) => format == CompatibilityFormat.Json
        ? $$$"""{"resourceType":"Patient","text":{"status":"generated","div":{{{JsonSerializer.Serialize(div)}}}}}"""
        : $"""<Patient xmlns="http://hl7.org/fhir"><text><status value="generated"/>{div}</text></Patient>""";

    private static void Mismatch(string expected, string actual, CompatibilityFormat format) =>
        Rejected(() => WireContractAssertions.Content(expected, actual, format));

    private static void Rejected(Action assertion)
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(assertion);
        Assert.StartsWith("Wire contract:", exception.Message, StringComparison.Ordinal);
    }
}
