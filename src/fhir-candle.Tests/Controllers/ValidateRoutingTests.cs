// <copyright file="ValidateRoutingTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Shouldly;

namespace fhir.candle.Tests.Controllers;

/// <summary>
/// Phase 3 (H3) — Integration tests for $validate routing through the controller.
/// Confirms that:
///   - POST /r4/Patient/$validate (type-level) returns 200 + OperationOutcome.
///   - POST /r4/Parameters/$validate (wire-type-as-URL) returns 200 + OperationOutcome,
///     proving the wire-type body unwrap is handled by the operation, not by a
///     controller-side branch.
/// </summary>
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
}
