// <copyright file="WireContractAssertions.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using System.Globalization;
using System.Net;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using fhir.candle.Tests.Models;

namespace fhir.candle.Tests.Extensions;

/// <summary>BCL-only assertions over wire content, independent of the serializer under test.</summary>
internal static class WireContractAssertions
{
    private static readonly XNamespace _fhir = "http://hl7.org/fhir";
    private static readonly XNamespace _xhtml = "http://www.w3.org/1999/xhtml";

    internal static string MediaType(CompatibilityFormat format) => format switch
    {
        CompatibilityFormat.Json => "application/fhir+json",
        CompatibilityFormat.Xml => "application/fhir+xml",
        _ => throw new ArgumentOutOfRangeException(nameof(format)),
    };

    internal static void Status(HttpStatusCode expected, HttpStatusCode actual) =>
        Equal(expected, actual, "HTTP status");

    internal static void Content(string expected, string actual, CompatibilityFormat format)
    {
        if (format == CompatibilityFormat.Json)
        {
            using JsonDocument expectedDocument = JsonDocument.Parse(expected);
            using JsonDocument actualDocument = JsonDocument.Parse(actual);
            CompareJson(expectedDocument.RootElement, actualDocument.RootElement, "$");
            return;
        }

        XDocument expectedXml = ParseXml(expected);
        XDocument actualXml = ParseXml(actual);
        CompareXmlNodes(SignificantNodes(expectedXml), SignificantNodes(actualXml), "/");
    }

    internal static void Response(
        CompatibilityResponseExpectation expected,
        CompatibilityResponse actual,
        CompatibilityFormat format,
        string baseUrl,
        CompatibilityBindings bindings)
    {
        Status(expected.Status, actual.Status);
        Equal(expected.MediaType ?? MediaType(format), actual.MediaType, "media type");

        if (actual.Transport == CompatibilityTransport.Http)
        {
            Require(actual.Direct is null, "HTTP must not contain fabricated direct-store fields.");
            Require(actual.HttpBody is not null, "HTTP body was not captured.");
            Body(expected.HttpBody, actual.HttpBody!, format, baseUrl, bindings);

            foreach (CompatibilityHeaderExpectation header in expected.HttpHeaders)
            {
                Equal(header.Value, HttpHeader(actual, header.Name), $"HTTP header {header.Name}");
            }

            if (expected.HttpResourceHeadersBinding is string name)
            {
                ResourceHeaders(
                    ResourceBinding(bindings, name),
                    HttpHeader(actual, "ETag"),
                    HttpHeader(actual, "Location"),
                    HttpHeader(actual, "Last-Modified"));
            }

            return;
        }

        Require(actual.HttpBody is null && actual.HttpHeaders.Count == 0, "Direct replay must not invent an HTTP response.");
        CompatibilityDirectResponse direct = actual.Direct ?? throw Failure("Direct response was not captured.");
        Equal(expected.DirectSuccess, direct.Success, "PerformInteraction success");
        Body(expected.SerializedResource, direct.SerializedResource, format, baseUrl, bindings);
        Body(expected.SerializedOutcome, direct.SerializedOutcome, format, baseUrl, bindings);

        foreach (CompatibilityHeaderExpectation header in expected.DirectHeaders)
        {
            string value = header.Name switch
            {
                "ETag" => direct.ETag,
                "Location" => direct.Location,
                "Last-Modified" => direct.LastModified,
                _ => throw new ArgumentException($"Not a direct response field: {header.Name}", nameof(expected)),
            };
            Equal(header.Value ?? string.Empty, value, $"direct {header.Name}");
        }

        if (expected.DirectResourceBinding is string bindingName)
        {
            GeneratedResourceBinding binding = ResourceBinding(bindings, bindingName);
            Equal(binding.Id, direct.Id, "direct Id");
            Equal(binding.ResourceType, direct.ResourceType, "direct ResourceType");
            if (expected.CheckDirectResourceHeaders)
            {
                ResourceHeaders(binding, direct.ETag, direct.Location, direct.LastModified);
            }
        }
    }

    internal static void Body(
        WireBodyExpectation expected,
        string actual,
        CompatibilityFormat format,
        string baseUrl,
        CompatibilityBindings bindings)
    {
        switch (expected)
        {
            case EmptyBodyExpectation:
                Equal(string.Empty, actual, "empty body/serialized field");
                break;
            case ExactBodyExpectation exact:
                Content(exact.Content, actual, format);
                break;
            case ResourceBodyExpectation resource:
                GeneratedResourceBinding binding = BindResource(resource.Generated, actual, format, baseUrl, bindings);
                Content(WithGeneratedFields(resource.Content, format, binding), actual, format);
                break;
            case MetadataBodyExpectation metadata:
                Metadata(metadata, actual, format, baseUrl, bindings);
                break;
            case OutcomeBodyExpectation outcome:
                Outcome(outcome, actual, format, bindings);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(expected));
        }
    }

    internal static void BindBaseUrl(CompatibilityBindings bindings, string tenant, string baseUrl)
    {
        Require(Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri? uri) &&
            (uri.Scheme == "http" || uri.Scheme == "https") &&
            uri.AbsolutePath.EndsWith($"/fhir/{tenant}", StringComparison.Ordinal) &&
            string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment),
            $"Expected an absolute, tenant-specific base URL for {tenant}, got {baseUrl}.");
        Require(bindings.BaseUrls.TryAdd(tenant, baseUrl), $"Base URL {tenant} was bound more than once.");
    }

    internal static GeneratedResourceBinding ResourceBinding(CompatibilityBindings bindings, string name) =>
        bindings.Resources.TryGetValue(name, out GeneratedResourceBinding? value)
            ? value
            : throw Failure($"Resource binding {name} has not been captured.");

    internal static GeneratedResourceBinding BindResource(
        GeneratedResourceExpectation expected,
        string actual,
        CompatibilityFormat format,
        string baseUrl,
        CompatibilityBindings bindings)
    {
        string resourceType;
        string id;
        string version;
        string lastUpdated;
        if (format == CompatibilityFormat.Json)
        {
            using JsonDocument document = JsonDocument.Parse(actual);
            JsonElement root = document.RootElement;
            resourceType = RequiredString(root, "resourceType");
            id = RequiredString(root, "id");
            JsonElement meta = RequiredProperty(root, "meta");
            version = RequiredString(meta, "versionId");
            lastUpdated = RequiredString(meta, "lastUpdated");
        }
        else
        {
            XElement root = ParseXml(actual).Root ?? throw Failure("Missing XML resource.");
            Equal(_fhir.NamespaceName, root.Name.NamespaceName, "FHIR XML namespace");
            resourceType = root.Name.LocalName;
            id = Primitive(root, "id");
            XElement meta = Child(root, "meta");
            version = Primitive(meta, "versionId");
            lastUpdated = Primitive(meta, "lastUpdated");
        }

        Equal(expected.ResourceType, resourceType, "resource type");
        ValidId(id);
        Require(Regex.IsMatch(version, @"\A[1-9][0-9]*\z", RegexOptions.CultureInvariant), $"Invalid store version: {version}.");
        DateTimeOffset instant = Instant(lastUpdated);
        if (expected.ExpectedId is not null)
        {
            Equal(expected.ExpectedId, id, "resource id");
        }

        if (expected.ExpectedVersion is not null)
        {
            Equal(expected.ExpectedVersion, version, "meta.versionId");
        }

        GeneratedResourceBinding binding = new(
            resourceType, id, version, lastUpdated, instant, baseUrl, $"{baseUrl}/{resourceType}/{id}");

        if (expected.Mode == CompatibilityBindingMode.Reuse)
        {
            Equal(ResourceBinding(bindings, expected.Name), binding, $"reused binding {expected.Name}");
            return binding;
        }

        FreshInstant(instant, bindings);
        if (expected.PreviousBinding is string previousName)
        {
            GeneratedResourceBinding previous = ResourceBinding(bindings, previousName);
            Equal(previous.ResourceType, resourceType, "updated resource type");
            Equal(previous.Id, id, "updated resource id");
            Equal(previous.BaseUrl, baseUrl, "updated resource tenant");
            Equal(BigInteger.Parse(previous.Version, CultureInfo.InvariantCulture) + 1,
                BigInteger.Parse(version, CultureInfo.InvariantCulture), "version progression");
            Require(instant >= previous.Instant, "meta.lastUpdated moved backwards on update.");
        }

        Require(bindings.Resources.TryAdd(expected.Name, binding), $"Resource binding {expected.Name} was captured more than once.");
        return binding;
    }

    internal static void ResourceHeaders(
        GeneratedResourceBinding binding,
        string? eTag,
        string? location,
        string? lastModified)
    {
        Equal($"W/\"{binding.Version}\"", eTag, "weak ETag/meta.versionId");
        Require(Uri.TryCreate(location, UriKind.Absolute, out _), "Location must be an absolute URL.");
        Equal(binding.Location, location, "Location/base URL/resource id");
        Require(DateTimeOffset.TryParseExact(lastModified, "r", CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal, out DateTimeOffset modified), "Last-Modified must be an HTTP date.");
        Equal(binding.Instant.UtcTicks / TimeSpan.TicksPerSecond,
            modified.UtcTicks / TimeSpan.TicksPerSecond, "Last-Modified/meta.lastUpdated at HTTP-date precision");
    }

    private static void CompareJson(JsonElement expected, JsonElement actual, string path)
    {
        Equal(expected.ValueKind, actual.ValueKind, $"{path} token kind");
        switch (expected.ValueKind)
        {
            case JsonValueKind.Object:
                JsonProperty[] expectedProperties = expected.EnumerateObject().ToArray();
                JsonProperty[] actualProperties = actual.EnumerateObject().ToArray();
                Require(expectedProperties.Select(property => property.Name).Distinct(StringComparer.Ordinal).Count() == expectedProperties.Length &&
                    actualProperties.Select(property => property.Name).Distinct(StringComparer.Ordinal).Count() == actualProperties.Length,
                    $"{path} contains duplicate JSON property names.");
                Equal(expectedProperties.Length, actualProperties.Length, $"{path} property count");
                foreach (JsonProperty property in expectedProperties)
                {
                    Require(actual.TryGetProperty(property.Name, out JsonElement value), $"{path}.{property.Name} is missing.");
                    CompareJson(property.Value, value, $"{path}.{property.Name}");
                }
                break;
            case JsonValueKind.Array:
                Equal(expected.GetArrayLength(), actual.GetArrayLength(), $"{path} array length");
                for (int index = 0; index < expected.GetArrayLength(); index++)
                {
                    CompareJson(expected[index], actual[index], $"{path}[{index}]");
                }
                break;
            case JsonValueKind.String:
                Equal(expected.GetString(), actual.GetString(), path);
                break;
            case JsonValueKind.Number:
                // Never round-trip through double (or decimal); token spelling retains scale and large values.
                Equal(expected.GetRawText(), actual.GetRawText(), $"{path} number token/precision");
                break;
            default:
                Equal(expected.GetRawText(), actual.GetRawText(), path);
                break;
        }
    }

    private static XDocument ParseXml(string content)
    {
        using StringReader text = new(content);
        using XmlReader reader = XmlReader.Create(text, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
        });
        return XDocument.Load(reader, LoadOptions.PreserveWhitespace);
    }

    private static XNode[] SignificantNodes(XContainer container)
    {
        bool canIgnoreIndentation = container is XDocument ||
            (container is XElement element &&
                element.Name.Namespace == _fhir &&
                element.HasElements &&
                element.Nodes().OfType<XText>().All(text => IsXmlWhitespace(text.Value)) &&
                !element.AncestorsAndSelf().Any(ancestor => ancestor.Name.Namespace == _xhtml) &&
                element.AncestorsAndSelf().Select(ancestor => (string?)ancestor.Attribute(XNamespace.Xml + "space"))
                    .FirstOrDefault(value => value is not null) != "preserve");

        return container.Nodes().Where(node =>
            !(canIgnoreIndentation && node is XText text && node is not XCData && IsXmlWhitespace(text.Value))).ToArray();
    }

    private static bool IsXmlWhitespace(string value) => value.All(character => character is ' ' or '\t' or '\r' or '\n');

    private static void CompareXmlNodes(XNode[] expected, XNode[] actual, string path)
    {
        Equal(expected.Length, actual.Length, $"{path} XML node count");
        for (int index = 0; index < expected.Length; index++)
        {
            if (expected[index] is XElement expectedElement && actual[index] is XElement actualElement)
            {
                Equal(expectedElement.Name, actualElement.Name, $"{path}[{index}] element/choice name");
                string childPath = $"{path}/{expectedElement.Name.LocalName}[{index}]";
                XAttribute[] expectedAttributes = expectedElement.Attributes().Where(attribute => !attribute.IsNamespaceDeclaration).ToArray();
                XAttribute[] actualAttributes = actualElement.Attributes().Where(attribute => !attribute.IsNamespaceDeclaration).ToArray();
                Equal(expectedAttributes.Length, actualAttributes.Length, $"{childPath} attribute count");
                foreach (XAttribute attribute in expectedAttributes)
                {
                    Equal(attribute.Value, (string?)actualElement.Attribute(attribute.Name), $"{childPath}/@{attribute.Name}");
                }

                CompareXmlNodes(SignificantNodes(expectedElement), SignificantNodes(actualElement), childPath);
            }
            else
            {
                Equal(expected[index].NodeType, actual[index].NodeType, $"{path}[{index}] XML node kind");
                Equal(expected[index].ToString(SaveOptions.DisableFormatting),
                    actual[index].ToString(SaveOptions.DisableFormatting), $"{path}[{index}] XML text");
            }
        }
    }

    private static string WithGeneratedFields(string content, CompatibilityFormat format, GeneratedResourceBinding binding)
    {
        if (format == CompatibilityFormat.Json)
        {
            using JsonDocument document = JsonDocument.Parse(content);
            JsonElement root = document.RootElement;
            Equal(binding.ResourceType, RequiredString(root, "resourceType"), "expected resource type");
            using MemoryStream buffer = new();
            using (Utf8JsonWriter writer = new(buffer))
            {
                writer.WriteStartObject();
                foreach (JsonProperty property in root.EnumerateObject())
                {
                    if (property.Name == "id")
                    {
                        Equal(binding.Id, property.Value.GetString(), "explicit expected id");
                    }

                    if (property.Name != "meta")
                    {
                        property.WriteTo(writer);
                    }
                }

                if (!root.TryGetProperty("id", out _))
                {
                    writer.WriteString("id", binding.Id);
                }

                writer.WritePropertyName("meta");
                writer.WriteStartObject();
                bool hasMeta = root.TryGetProperty("meta", out JsonElement meta);
                if (hasMeta)
                {
                    foreach (JsonProperty property in meta.EnumerateObject())
                    {
                        if (property.Name == "versionId")
                        {
                            Equal(binding.Version, property.Value.GetString(), "explicit expected meta.versionId");
                        }
                        else if (property.Name == "lastUpdated")
                        {
                            Equal(binding.LastUpdated, property.Value.GetString(), "explicit expected meta.lastUpdated");
                        }

                        property.WriteTo(writer);
                    }
                }

                if (!hasMeta || !meta.TryGetProperty("versionId", out _))
                {
                    writer.WriteString("versionId", binding.Version);
                }

                if (!hasMeta || !meta.TryGetProperty("lastUpdated", out _))
                {
                    writer.WriteString("lastUpdated", binding.LastUpdated);
                }

                writer.WriteEndObject();
                writer.WriteEndObject();
            }

            return Encoding.UTF8.GetString(buffer.ToArray());
        }

        XDocument xml = ParseXml(content);
        XElement resource = xml.Root ?? throw Failure("Missing expected XML resource.");
        Equal(_fhir + binding.ResourceType, resource.Name, "expected resource type");
        XElement? id = resource.Element(_fhir + "id");
        if (id is null)
        {
            id = new XElement(_fhir + "id", new XAttribute("value", binding.Id));
            resource.AddFirst(id);
        }
        else
        {
            Equal(binding.Id, (string?)id.Attribute("value"), "explicit expected id");
        }

        XElement? xmlMeta = resource.Element(_fhir + "meta");
        if (xmlMeta is null)
        {
            xmlMeta = new XElement(_fhir + "meta");
            id.AddAfterSelf(xmlMeta);
        }

        XElement? versionId = xmlMeta.Element(_fhir + "versionId");
        if (versionId is null)
        {
            versionId = new XElement(_fhir + "versionId", new XAttribute("value", binding.Version));
            XElement? extension = xmlMeta.Elements(_fhir + "extension").LastOrDefault();
            if (extension is null)
            {
                xmlMeta.AddFirst(versionId);
            }
            else
            {
                extension.AddAfterSelf(versionId);
            }
        }
        else
        {
            Equal(binding.Version, (string?)versionId.Attribute("value"), "explicit expected meta.versionId");
        }

        XElement? lastUpdated = xmlMeta.Element(_fhir + "lastUpdated");
        if (lastUpdated is null)
        {
            versionId.AddAfterSelf(new XElement(_fhir + "lastUpdated", new XAttribute("value", binding.LastUpdated)));
        }
        else
        {
            Equal(binding.LastUpdated, (string?)lastUpdated.Attribute("value"), "explicit expected meta.lastUpdated");
        }

        return xml.ToString(SaveOptions.DisableFormatting);
    }

    private static void Metadata(
        MetadataBodyExpectation expected,
        string actual,
        CompatibilityFormat format,
        string baseUrl,
        CompatibilityBindings bindings)
    {
        GeneratedResourceBinding binding = BindResource(expected.Generated, actual, format, baseUrl, bindings);
        string date;
        if (format == CompatibilityFormat.Json)
        {
            using JsonDocument document = JsonDocument.Parse(actual);
            JsonElement root = document.RootElement;
            Equal(expected.FhirVersion, RequiredString(root, "fhirVersion"), "CapabilityStatement.fhirVersion");
            Equal(binding.Location, RequiredString(root, "url"), "CapabilityStatement tenant URL");
            Equal("active", RequiredString(root, "status"), "CapabilityStatement.status");
            Equal("instance", RequiredString(root, "kind"), "CapabilityStatement.kind");
            CompareJson(JsonSerializer.SerializeToElement(new[] { "application/fhir+json", "application/fhir+xml" }),
                RequiredProperty(root, "format"), "$.format");
            JsonElement rest = RequiredProperty(root, "rest");
            Equal(1, rest.GetArrayLength(), "CapabilityStatement.rest count");
            Equal("server", RequiredString(rest[0], "mode"), "CapabilityStatement.rest.mode");
            date = RequiredString(root, "date");
        }
        else
        {
            XElement root = ParseXml(actual).Root!;
            Equal(expected.FhirVersion, Primitive(root, "fhirVersion"), "CapabilityStatement.fhirVersion");
            Equal(binding.Location, Primitive(root, "url"), "CapabilityStatement tenant URL");
            Equal("active", Primitive(root, "status"), "CapabilityStatement.status");
            Equal("instance", Primitive(root, "kind"), "CapabilityStatement.kind");
            string?[] formats = root.Elements(_fhir + "format").Select(element => (string?)element.Attribute("value")).ToArray();
            Require(formats.SequenceEqual(new[] { "application/fhir+json", "application/fhir+xml" }), "CapabilityStatement.format changed.");
            Equal("server", Primitive(Child(root, "rest"), "mode"), "CapabilityStatement.rest.mode");
            date = Primitive(root, "date");
        }

        FreshInstant(Instant(date), bindings);
        BindValue(bindings, expected.DateBinding, date);
    }

    private static void Outcome(OutcomeBodyExpectation expected, string actual, CompatibilityFormat format, CompatibilityBindings bindings)
    {
        string id;
        if (format == CompatibilityFormat.Json)
        {
            using JsonDocument document = JsonDocument.Parse(actual);
            JsonElement root = document.RootElement;
            Equal("OperationOutcome", RequiredString(root, "resourceType"), "outcome resource type");
            id = RequiredString(root, "id");
            JsonElement issues = RequiredProperty(root, "issue");
            Equal(expected.Issues.Count, issues.GetArrayLength(), "outcome issue count");
            for (int index = 0; index < expected.Issues.Count; index++)
            {
                OutcomeIssueExpectation issue = expected.Issues[index];
                Equal(issue.Severity, RequiredString(issues[index], "severity"), $"issue[{index}].severity");
                Equal(issue.Code, RequiredString(issues[index], "code"), $"issue[{index}].code");
                Diagnostics(issue.Diagnostics, RequiredString(issues[index], "diagnostics"));
            }
        }
        else
        {
            XElement root = ParseXml(actual).Root ?? throw Failure("Missing XML outcome.");
            Equal(_fhir + "OperationOutcome", root.Name, "outcome resource type");
            id = Primitive(root, "id");
            XElement[] issues = root.Elements(_fhir + "issue").ToArray();
            Equal(expected.Issues.Count, issues.Length, "outcome issue count");
            for (int index = 0; index < expected.Issues.Count; index++)
            {
                OutcomeIssueExpectation issue = expected.Issues[index];
                Equal(issue.Severity, Primitive(issues[index], "severity"), $"issue[{index}].severity");
                Equal(issue.Code, Primitive(issues[index], "code"), $"issue[{index}].code");
                Diagnostics(issue.Diagnostics, Primitive(issues[index], "diagnostics"));
            }
        }

        ValidId(id);
        BindValue(bindings, expected.IdBinding, id);
    }

    private static void Diagnostics(string? expected, string actual)
    {
        Require(!string.IsNullOrEmpty(actual), "OperationOutcome diagnostics must be present.");
        if (expected is not null)
        {
            Equal(expected, actual, "OperationOutcome diagnostics");
        }
    }

    private static string? HttpHeader(CompatibilityResponse response, string name)
    {
        if (!response.HttpHeaders.TryGetValue(name, out string[]? values))
        {
            return null;
        }

        Equal(1, values.Length, $"{name} header value count");
        return values[0];
    }

    private static JsonElement RequiredProperty(JsonElement parent, string name)
    {
        Require(parent.ValueKind == JsonValueKind.Object, $"Expected an object containing {name}.");
        JsonProperty[] matches = parent.EnumerateObject().Where(property => property.Name == name).ToArray();
        Equal(1, matches.Length, $"required property {name} count");
        return matches[0].Value;
    }

    private static string RequiredString(JsonElement parent, string name)
    {
        JsonElement value = RequiredProperty(parent, name);
        Equal(JsonValueKind.String, value.ValueKind, $"{name} token kind");
        return value.GetString() ?? throw Failure($"Missing string {name}.");
    }

    private static XElement Child(XElement parent, string name)
    {
        XElement[] matches = parent.Elements(_fhir + name).ToArray();
        Equal(1, matches.Length, $"required element {name} count");
        return matches[0];
    }

    private static string Primitive(XElement parent, string name) =>
        (string?)Child(parent, name).Attribute("value") ?? throw Failure($"Missing {name}/@value.");

    private static void ValidId(string id) =>
        Require(Regex.IsMatch(id, @"\A[A-Za-z0-9\-.]{1,64}\z", RegexOptions.CultureInvariant), $"Invalid FHIR id: {id}.");

    private static DateTimeOffset Instant(string value)
    {
        Require(Regex.IsMatch(value, @"\A\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:Z|[+-]\d{2}:\d{2})\z", RegexOptions.CultureInvariant),
            $"Expected a timezone-qualified FHIR instant, got {value}.");
        Require(DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTimeOffset instant),
            $"Invalid FHIR instant: {value}.");
        return instant;
    }

    private static void FreshInstant(DateTimeOffset instant, CompatibilityBindings bindings) =>
        Require(instant >= bindings.StartedAt.AddSeconds(-1) && instant <= DateTimeOffset.UtcNow.AddSeconds(1),
            $"Generated instant {instant:O} is outside this replay's lifetime.");

    private static void BindValue(CompatibilityBindings bindings, string name, string value) =>
        Require(bindings.Values.TryAdd(name, new CompatibilityValueBinding(name, value)), $"Value binding {name} was captured more than once.");

    private static void Equal<T>(T expected, T actual, string path) =>
        Require(EqualityComparer<T>.Default.Equals(expected, actual), $"{path}: expected <{expected}>, actual <{actual}>.");

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw Failure(message);
        }
    }

    private static InvalidOperationException Failure(string message) => new($"Wire contract: {message}");
}
