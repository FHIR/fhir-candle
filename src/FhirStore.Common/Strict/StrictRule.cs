// <copyright file="StrictRule.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using FhirCandle.Utils;

namespace FhirCandle.Strict;

/// <summary>
/// Stable identifiers for spec-strict enforcement rules surfaced by <c>--strict</c>.
/// Each rule maps to a per-FHIR-version canonical spec URL via
/// <see cref="StrictRule.SpecUrls"/> / <see cref="StrictRule.GetSpecUrl"/>; the URL is
/// emitted inline in <c>OperationOutcome.diagnostics</c> so test-rig operators can
/// justify the rejection without out-of-band lookup.
/// </summary>
public enum StrictRuleCode
{
    /// <summary>
    /// PUT request body is missing <c>Resource.id</c>. FHIR REST §3.1.0.7 requires the
    /// body id to be present and equal to the URL id; lenient mode stamps the URL id
    /// onto the empty body and falls through.
    /// </summary>
    PutEmptyBodyId = 1,

    /// <summary>
    /// PUT request body <c>Resource.id</c> is present but does not equal the URL id.
    /// Always rejected (independent of strict mode) — FHIR REST §3.1.0.7.1 permits
    /// either 400 or 422; we return 422 since the request is well-formed FHIR but
    /// semantically inconsistent.
    /// </summary>
    PutBodyIdMismatch = 2,

    /// <summary>
    /// POST/create request body includes a client-supplied <c>Resource.id</c>.
    /// FHIR REST §3.1.0.6 says the server SHALL assign the id on create; lenient
    /// mode silently re-IDs, strict mode rejects with 400.
    /// </summary>
    PostClientSuppliedId = 3,

    /// <summary>
    /// PUT/update on a resource id that does not exist. Lenient mode (with
    /// <c>AllowCreateAsUpdate</c>) creates the resource; strict mode rejects with 404
    /// per FHIR REST §3.1.0.7 "Update as Create" guidance.
    /// </summary>
    PutCreateAsUpdateDisallowed = 4,

    /// <summary>
    /// <c>Resource.id</c> does not match the FHIR id datatype regex
    /// <c>[A-Za-z0-9\-\.]{1,64}</c>. Lenient mode accepts non-conforming ids; strict
    /// mode rejects with 400.
    /// </summary>
    ResourceIdRegex = 5,

    /// <summary>
    /// Search query string contains a parameter name not recognized for the target
    /// resource type. Lenient mode silently ignores; strict (<c>Prefer: handling=strict</c>)
    /// rejects with 400.
    /// </summary>
    SearchUnknownParameter = 6,

    /// <summary>
    /// Search query string contains a parameter with a malformed value (bad date format,
    /// unknown modifier, etc.) or partially-malformed multi-value list. Lenient mode
    /// silently drops the offending value; strict rejects with 400.
    /// </summary>
    SearchMalformedParameter = 7,
}

/// <summary>
/// Per-FHIR-version canonical spec URLs for each <see cref="StrictRuleCode"/>.
/// URLs follow the published HL7 site path conventions
/// (<c>http://hl7.org/fhir/R{4,4B,5}/...</c>) and were verified against the local
/// dev mirrors at <c>C:\ai\support\fhir-r{4,4b,5}\</c>. The URL is emitted inline
/// in <c>OperationOutcome.diagnostics</c> by the strict outcome helpers in
/// <c>SerializationUtils</c>.
/// </summary>
public static class StrictRule
{
    private const string R4Http = "http://hl7.org/fhir/R4/http.html";
    private const string R4BHttp = "http://hl7.org/fhir/R4B/http.html";
    private const string R5Http = "http://hl7.org/fhir/R5/http.html";

    private const string R4Search = "http://hl7.org/fhir/R4/search.html";
    private const string R4BSearch = "http://hl7.org/fhir/R4B/search.html";
    private const string R5Search = "http://hl7.org/fhir/R5/search.html";

    private const string R4Datatypes = "http://hl7.org/fhir/R4/datatypes.html";
    private const string R4BDatatypes = "http://hl7.org/fhir/R4B/datatypes.html";
    private const string R5Datatypes = "http://hl7.org/fhir/R5/datatypes.html";

    private static readonly IReadOnlyDictionary<StrictRuleCode, IReadOnlyDictionary<FhirReleases.FhirSequenceCodes, string>> _specUrls =
        new Dictionary<StrictRuleCode, IReadOnlyDictionary<FhirReleases.FhirSequenceCodes, string>>
        {
            [StrictRuleCode.PutEmptyBodyId] = HttpUpdate(),
            [StrictRuleCode.PutBodyIdMismatch] = HttpUpdate(),
            [StrictRuleCode.PostClientSuppliedId] = HttpCreate(),
            [StrictRuleCode.PutCreateAsUpdateDisallowed] = HttpUpsert(),
            [StrictRuleCode.ResourceIdRegex] = DatatypesId(),
            [StrictRuleCode.SearchUnknownParameter] = SearchErrors(),
            [StrictRuleCode.SearchMalformedParameter] = SearchErrors(),
        };

    /// <summary>
    /// Per-rule, per-FHIR-version canonical spec URL map. Public for callers (tests,
    /// docs generators) that want to introspect rule metadata.
    /// </summary>
    public static IReadOnlyDictionary<StrictRuleCode, IReadOnlyDictionary<FhirReleases.FhirSequenceCodes, string>> SpecUrls => _specUrls;

    /// <summary>Resolves the canonical spec URL for <paramref name="rule"/> at <paramref name="version"/>.</summary>
    /// <param name="rule">The strict rule code.</param>
    /// <param name="version">The target FHIR version (must be R4, R4B, or R5; other values fall back to R4).</param>
    /// <returns>The canonical spec URL; empty string if the rule is not registered.</returns>
    public static string GetSpecUrl(StrictRuleCode rule, FhirReleases.FhirSequenceCodes version)
    {
        if (!_specUrls.TryGetValue(rule, out IReadOnlyDictionary<FhirReleases.FhirSequenceCodes, string>? perVersion))
        {
            return string.Empty;
        }

        if (perVersion.TryGetValue(version, out string? url))
        {
            return url;
        }

        return perVersion.TryGetValue(FhirReleases.FhirSequenceCodes.R4, out string? fallback)
            ? fallback
            : string.Empty;
    }

    private static IReadOnlyDictionary<FhirReleases.FhirSequenceCodes, string> HttpUpdate() => new Dictionary<FhirReleases.FhirSequenceCodes, string>
    {
        [FhirReleases.FhirSequenceCodes.R4] = R4Http + "#update",
        [FhirReleases.FhirSequenceCodes.R4B] = R4BHttp + "#update",
        [FhirReleases.FhirSequenceCodes.R5] = R5Http + "#update",
    };

    private static IReadOnlyDictionary<FhirReleases.FhirSequenceCodes, string> HttpCreate() => new Dictionary<FhirReleases.FhirSequenceCodes, string>
    {
        [FhirReleases.FhirSequenceCodes.R4] = R4Http + "#create",
        [FhirReleases.FhirSequenceCodes.R4B] = R4BHttp + "#create",
        [FhirReleases.FhirSequenceCodes.R5] = R5Http + "#create",
    };

    private static IReadOnlyDictionary<FhirReleases.FhirSequenceCodes, string> HttpUpsert() => new Dictionary<FhirReleases.FhirSequenceCodes, string>
    {
        [FhirReleases.FhirSequenceCodes.R4] = R4Http + "#upsert",
        [FhirReleases.FhirSequenceCodes.R4B] = R4BHttp + "#upsert",
        [FhirReleases.FhirSequenceCodes.R5] = R5Http + "#upsert",
    };

    private static IReadOnlyDictionary<FhirReleases.FhirSequenceCodes, string> DatatypesId() => new Dictionary<FhirReleases.FhirSequenceCodes, string>
    {
        [FhirReleases.FhirSequenceCodes.R4] = R4Datatypes + "#id",
        [FhirReleases.FhirSequenceCodes.R4B] = R4BDatatypes + "#id",
        [FhirReleases.FhirSequenceCodes.R5] = R5Datatypes + "#id",
    };

    private static IReadOnlyDictionary<FhirReleases.FhirSequenceCodes, string> SearchErrors() => new Dictionary<FhirReleases.FhirSequenceCodes, string>
    {
        [FhirReleases.FhirSequenceCodes.R4] = R4Search + "#errors",
        [FhirReleases.FhirSequenceCodes.R4B] = R4BSearch + "#errors",
        [FhirReleases.FhirSequenceCodes.R5] = R5Search + "#errors",
    };
}
