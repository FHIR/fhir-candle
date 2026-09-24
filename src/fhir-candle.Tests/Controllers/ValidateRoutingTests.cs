// <copyright file="ValidateRoutingTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using fhir.candle.Tests.Extensions;
using fhir.candle.Tests.Models;
using FhirCandle.Utils;
using Shouldly;

namespace fhir.candle.Tests.Controllers;

/// <summary>
/// Phase 3 (H3) — Integration tests for $validate routing through the controller.
/// Confirms that:
///   - POST /r4/Patient/$validate (type-level) returns 200 + OperationOutcome.
///   - POST /r4/Parameters/$validate (wire-type-as-URL) remains 404.
///   - A Parameters body on a proper system/type/instance route is unwrapped by
///     the operation, not by a controller-side wire-type routing bypass.
/// </summary>
[Collection(CompatibilityHostCollection.Name)]
public class ValidateRoutingTests : IClassFixture<CandleWebApplicationFactory>
{
    private readonly CandleWebApplicationFactory _factory;

    public ValidateRoutingTests(CandleWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ValidateViaTypePathControllerRouting()
    {
        string body = "{\"resourceType\":\"Patient\",\"gender\":\"male\"}";

        using HttpRequestMessage request = new(HttpMethod.Post, "/fhir/r4/Patient/$validate")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/fhir+json"),
        };

        using HttpResponseMessage response = await _factory.Client.SendAsync(request);

        string responseBody = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.OK, responseBody);
        responseBody.ShouldContain("\"resourceType\":\"OperationOutcome\"");
    }

    [Fact]
    public async Task ValidateViaParametersWireTypeControllerRouting()
    {
        // Per Phase 3 (H3) resolution: with the controller's Parameters wire-type bypass
        // removed, the URL segment "Parameters" is no longer a valid resource-type segment
        // in the FHIR REST API. The dispatcher (DoTypeOperation) treats "Parameters" as
        // an unknown resource type and returns 404 — which is the spec-correct answer.
        // Operations that need to accept Parameters-wrapped bodies are invoked via the
        // proper URL shapes: /<tenant>/$<op> (system-level) or /<tenant>/<Type>/$<op>
        // (type-level), and the operation itself unwraps the Parameters body via
        // OpValidate.ExtractParametersResource.
        string body = "{\"resourceType\":\"Parameters\",\"parameter\":[{\"name\":\"resource\",\"resource\":{\"resourceType\":\"Patient\",\"gender\":\"male\"}}]}";

        using HttpRequestMessage request = new(HttpMethod.Post, "/fhir/r4/Parameters/$validate")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/fhir+json"),
        };

        using HttpResponseMessage response = await _factory.Client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    /// <summary>Proper body wrapping is independent of the URL's resource-type segment.</summary>
    public static IEnumerable<object[]> ParametersRouteCases
    {
        get
        {
            foreach (FhirReleases.FhirSequenceCodes version in new[]
            {
                FhirReleases.FhirSequenceCodes.R4, FhirReleases.FhirSequenceCodes.R4B, FhirReleases.FhirSequenceCodes.R5,
            })
            {
                foreach (CompatibilityFormat format in Enum.GetValues<CompatibilityFormat>())
                {
                    foreach (string route in new[] { "$validate", "Patient/$validate", "Patient/focus/$validate", "Parameters/$validate" })
                    {
                        yield return [$"{version} / {format} / {route}", version, format, route];
                    }
                }
            }
        }
    }

    /// <summary>All three versions accept a Parameters body on real operation routes, never the Parameters URL.</summary>
    [Theory]
    [MemberData(nameof(ParametersRouteCases))]
    public async Task ParametersBodiesUseProperOperationRoutes(
        string label,
        FhirReleases.FhirSequenceCodes version,
        CompatibilityFormat format,
        string route)
    {
        Assert.NotEmpty(label);
        string name = $"routing-{version.ToString().ToLowerInvariant()}";
        CompatibilityTenant tenant = new(name, version, $"http://localhost/fhir/{name}");
        CompatibilityScenario scenario = new(label,
            "ValidateViaParametersWireTypeControllerRouting; docs/user/operations/validate.md target resolution",
            [tenant], []);
        await using CompatibilityReplay replay = new(scenario, CompatibilityTransport.Http);
        string patient = format == CompatibilityFormat.Json
            ? """{"resourceType":"Patient","id":"focus","active":true}"""
            : """<Patient xmlns="http://hl7.org/fhir"><id value="focus"/><active value="true"/></Patient>""";
        CompatibilityResponse seed = await replay.SendAsync(new(name, "PUT", "Patient/focus") { Format = format, Content = patient });
        Assert.Equal(HttpStatusCode.Created, seed.Status);
        string body = format == CompatibilityFormat.Json
            ? """{"resourceType":"Parameters","parameter":[{"name":"resource","resource":{"resourceType":"Patient","active":true}}]}"""
            : """<Parameters xmlns="http://hl7.org/fhir"><parameter><name value="resource"/><resource><Patient><active value="true"/></Patient></resource></parameter></Parameters>""";
        CompatibilityResponse response = await replay.SendAsync(new(name, "POST", route) { Format = format, Content = body });
        Assert.Equal(route == "Parameters/$validate" ? HttpStatusCode.NotFound : HttpStatusCode.OK, response.Status);
        if (route == "Parameters/$validate")
        {
            return;
        }
        Assert.Equal(WireContractAssertions.MediaType(format), response.MediaType);
        if (format == CompatibilityFormat.Json)
        {
            using JsonDocument outcome = JsonDocument.Parse(response.HttpBody!);
            Assert.Equal("OperationOutcome", outcome.RootElement.GetProperty("resourceType").GetString());
            JsonElement issue = Assert.Single(outcome.RootElement.GetProperty("issue").EnumerateArray());
            Assert.Equal("information", issue.GetProperty("severity").GetString());
            Assert.Equal("informational", issue.GetProperty("code").GetString());
            Assert.Equal("All OK", issue.GetProperty("diagnostics").GetString());
        }
        else
        {
            XNamespace fhir = "http://hl7.org/fhir";
            XElement outcome = XElement.Parse(response.HttpBody!, LoadOptions.PreserveWhitespace);
            Assert.Equal(fhir + "OperationOutcome", outcome.Name);
            XElement issue = Assert.Single(outcome.Elements(fhir + "issue"));
            Assert.Equal("information", (string?)issue.Element(fhir + "severity")?.Attribute("value"));
            Assert.Equal("informational", (string?)issue.Element(fhir + "code")?.Attribute("value"));
            Assert.Equal("All OK", (string?)issue.Element(fhir + "diagnostics")?.Attribute("value"));
        }
    }
}
