// <copyright file="CompatibilityScenario.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using System.Net;
using FhirCandle.Utils;

namespace fhir.candle.Tests.Models;

/// <summary>The two consumer boundaries exercised by the corpus.</summary>
public enum CompatibilityTransport
{
    /// <summary>Real controller routing through TestServer.</summary>
    Http,

    /// <summary>The versioned IFhirStore interaction interface.</summary>
    Direct,
}

/// <summary>The independently authored wire formats.</summary>
public enum CompatibilityFormat
{
    /// <summary>FHIR JSON.</summary>
    Json,

    /// <summary>FHIR XML.</summary>
    Xml,
}

/// <summary>An ordered, synthetic contract case, not an executable expression language.</summary>
internal sealed record CompatibilityScenario(
    string Id,
    string Evidence,
    IReadOnlyList<CompatibilityTenant> Tenants,
    IReadOnlyList<CompatibilityStep> Steps)
{
    /// <summary>Gets the raw resources submitted before the ordered steps.</summary>
    public IReadOnlyList<CompatibilitySeed> Seeds { get; init; } = [];
}

/// <summary>Only the tenant settings needed by resource-interaction cases.</summary>
internal sealed record CompatibilityTenant(
    string Name,
    FhirReleases.FhirSequenceCodes Version,
    string BaseUrl)
{
    /// <summary>Gets whether the existing strict tenant policy is enabled.</summary>
    public bool Strict { get; init; }

    /// <summary>Gets whether existing IDs are allowed on applicable store interactions.</summary>
    public bool AllowExistingId { get; init; } = true;

    /// <summary>Gets whether an ordinary PUT may create a missing resource.</summary>
    public bool AllowCreateAsUpdate { get; init; } = true;

    /// <summary>Gets whether conditional reads can return NotModified.</summary>
    public bool SupportNotChanged { get; init; }
}

/// <summary>A raw seed installed with the documented lenient PUT-as-create interaction.</summary>
internal sealed record CompatibilitySeed(
    string Id,
    string Tenant,
    string ResourceType,
    string ResourceId,
    CompatibilityFormat Format,
    string Content,
    CompatibilityResponseExpectation Expected);

/// <summary>A labeled request and its independently authored expectations.</summary>
internal sealed record CompatibilityStep(
    string Id,
    CompatibilityRequest Request,
    CompatibilityResponseExpectation Expected);

/// <summary>Raw request data. Bindings have only the explicit target and If-Match uses below.</summary>
internal sealed record CompatibilityRequest(string Tenant, string Method, string Path)
{
    /// <summary>Gets the same-format assertion format and default request MIME type.</summary>
    public CompatibilityFormat Format { get; init; } = CompatibilityFormat.Json;

    /// <summary>Gets the raw request body, or null for a bodyless request.</summary>
    public string? Content { get; init; }

    /// <summary>Gets an explicit input MIME type for alias or conversion cases.</summary>
    public string? SourceMediaType { get; init; }

    /// <summary>Gets an explicit direct output MIME type or HTTP Accept value.</summary>
    public string? DestinationMediaType { get; init; }

    /// <summary>Gets literal request headers.</summary>
    public IReadOnlyDictionary<string, string> Headers { get; init; } = new Dictionary<string, string>();

    /// <summary>Gets a bound resource URL to use instead of a literal path.</summary>
    public string? TargetBinding { get; init; }

    /// <summary>Gets a bound resource version to use as a weak If-Match value.</summary>
    public string? IfMatchBinding { get; init; }

    /// <summary>Gets whether pretty serialization is explicitly requested.</summary>
    public bool Pretty { get; init; }

    /// <summary>Gets an explicit summary flag.</summary>
    public string Summary { get; init; } = string.Empty;
}

/// <summary>HTTP has one selected body; a direct interaction has two separate serialized fields.</summary>
internal sealed record CompatibilityResponseExpectation(
    HttpStatusCode Status,
    bool DirectSuccess,
    WireBodyExpectation HttpBody,
    WireBodyExpectation SerializedResource,
    WireBodyExpectation SerializedOutcome)
{
    /// <summary>Gets an explicit response MIME type, or the request format's default.</summary>
    public string? MediaType { get; init; }

    /// <summary>Gets checks against actual HTTP response headers.</summary>
    public IReadOnlyList<CompatibilityHeaderExpectation> HttpHeaders { get; init; } = [];

    /// <summary>Gets checks against the separate direct ETag, Location, and LastModified fields.</summary>
    public IReadOnlyList<CompatibilityHeaderExpectation> DirectHeaders { get; init; } = [];

    /// <summary>Gets the binding related to actual HTTP resource headers, if declared.</summary>
    public string? HttpResourceHeadersBinding { get; init; }

    /// <summary>Gets the binding related to the direct resource identity fields, if declared.</summary>
    public string? DirectResourceBinding { get; init; }

    /// <summary>Gets whether that direct resource binding must also match its response header fields.</summary>
    public bool CheckDirectResourceHeaders { get; init; } = true;
}

/// <summary>A literal header expectation; null means the header must be absent.</summary>
internal sealed record CompatibilityHeaderExpectation(string Name, string? Value);

/// <summary>The finite body assertion shapes used by the foundation.</summary>
internal abstract record WireBodyExpectation;

/// <summary>An actually empty body or serialized field.</summary>
internal sealed record EmptyBodyExpectation : WireBodyExpectation;

/// <summary>Exact same-format content, with only the documented formatting normalization.</summary>
internal sealed record ExactBodyExpectation(string Content) : WireBodyExpectation;

/// <summary>Exact submitted content plus explicitly checked, bound id/meta values.</summary>
internal sealed record ResourceBodyExpectation(
    string Content,
    GeneratedResourceExpectation Generated) : WireBodyExpectation;

/// <summary>Selected, documented capability fields, not a snapshot of generated conformance data.</summary>
internal sealed record MetadataBodyExpectation(
    string FhirVersion,
    GeneratedResourceExpectation Generated,
    string DateBinding) : WireBodyExpectation;

/// <summary>Explicit issues and a scenario-local generated OperationOutcome id.</summary>
internal sealed record OutcomeBodyExpectation(
    string IdBinding,
    IReadOnlyList<OutcomeIssueExpectation> Issues) : WireBodyExpectation;

/// <summary>Null diagnostics means non-empty diagnostic text, not an SDK-specific wording contract.</summary>
internal sealed record OutcomeIssueExpectation(string Severity, string Code, string? Diagnostics = null);

/// <summary>Capture once, or require all previously bound resource values to be reused.</summary>
internal enum CompatibilityBindingMode
{
    Capture,
    Reuse,
}

/// <summary>A local declaration of generated fields, never a global metadata ignore rule.</summary>
internal sealed record GeneratedResourceExpectation(
    string Name,
    string ResourceType,
    string? ExpectedVersion,
    string? ExpectedId = null,
    CompatibilityBindingMode Mode = CompatibilityBindingMode.Capture,
    string? PreviousBinding = null);

/// <summary>The identity, exact timestamp text, and URL captured for one resource version.</summary>
internal sealed record GeneratedResourceBinding(
    string ResourceType,
    string Id,
    string Version,
    string LastUpdated,
    DateTimeOffset Instant,
    string BaseUrl,
    string Location);

/// <summary>A named generated scalar, such as a capability date or outcome id.</summary>
internal sealed record CompatibilityValueBinding(string Name, string Value);

/// <summary>Mutable state belongs to one replay only; scenario definitions remain reusable.</summary>
internal sealed class CompatibilityBindings
{
    /// <summary>Gets the replay's timestamp lower bound, captured before initialization.</summary>
    public DateTimeOffset StartedAt { get; } = DateTimeOffset.UtcNow;

    /// <summary>Gets the configured base URLs, bound with their tenant prefixes intact.</summary>
    public Dictionary<string, string> BaseUrls { get; } = new(StringComparer.Ordinal);

    /// <summary>Gets the resources captured once in this replay.</summary>
    public Dictionary<string, GeneratedResourceBinding> Resources { get; } = new(StringComparer.Ordinal);

    /// <summary>Gets the other explicitly named generated values.</summary>
    public Dictionary<string, CompatibilityValueBinding> Values { get; } = new(StringComparer.Ordinal);
}

/// <summary>Actual transport output. HTTP headers are never synthesized from store fields.</summary>
internal sealed record CompatibilityResponse(
    CompatibilityTransport Transport,
    HttpStatusCode Status,
    string MediaType)
{
    /// <summary>Gets the actual selected HTTP body, or null for direct replay.</summary>
    public string? HttpBody { get; init; }

    /// <summary>Gets only headers received on the HTTP response, including content headers.</summary>
    public IReadOnlyDictionary<string, string[]> HttpHeaders { get; init; } = new Dictionary<string, string[]>();

    /// <summary>Gets direct interaction output, or null for HTTP replay.</summary>
    public CompatibilityDirectResponse? Direct { get; init; }
}

/// <summary>The observable direct-store fields, retained separately from any HTTP representation.</summary>
internal sealed record CompatibilityDirectResponse(
    bool Success,
    string SerializedResource,
    string SerializedOutcome,
    string ResourceType,
    string Id,
    string ETag,
    string LastModified,
    string Location);
