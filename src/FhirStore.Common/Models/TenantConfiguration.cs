// <copyright file="TenantConfiguration.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using FhirCandle.Extensions;
using FhirCandle.Utils;

namespace FhirCandle.Models;

/// <summary>
/// A provider configuration.
/// </summary>
public class TenantConfiguration
{
    /// <summary>
    /// Gets or sets the supported FHIR versions.
    /// </summary>
    public static readonly List<FhirReleases.FhirSequenceCodes> SupportedFhirVersions = [
        FhirReleases.FhirSequenceCodes.R4,
        FhirReleases.FhirSequenceCodes.R4B,
        FhirReleases.FhirSequenceCodes.R5
    ];

    /// <summary>
    /// Information about the FHIR package.
    /// </summary>
    public readonly record struct FhirPackageInfo
    {
        /// <summary>
        /// Gets the identifier.
        /// </summary>
        public string Id { get; init; }

        /// <summary>
        /// Gets the version.
        /// </summary>
        public string Version { get; init; }

        /// <summary>
        /// Gets the registry.
        /// </summary>
        public string Registry { get; init; }
    }

    /// <summary>
    /// Gets or sets the FHIR version.
    /// </summary>
    public required FhirReleases.FhirSequenceCodes FhirVersion { get; set; }

    /// <summary>
    /// Gets or sets the supported resources.
    /// </summary>
    public IEnumerable<string> SupportedResources { get; set; } = [];

    /// <summary>
    /// Gets or sets the supported MIME formats.
    /// </summary>
    public IEnumerable<string> SupportedFormats { get; set; } = [
        "application/fhir+json",
        "application/fhir+xml"
    ];

    /// <summary>
    /// Gets or sets the route controller name.
    /// </summary>
    public required string ControllerName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the absolute base URL of this store.
    /// </summary>
    public required string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets the FHIR packages.
    /// </summary>
    public Dictionary<string, FhirPackageInfo> FhirPackages { get; } = [];

    /// <summary>
    /// Gets or sets the load directory path.
    /// </summary>
    public System.IO.DirectoryInfo? LoadDirectory { get; set; } = null;

    /// <summary>
    /// Gets or sets a value indicating whether to protect loaded content.
    /// </summary>
    public bool ProtectLoadedContent { get; set; } = false;

    /// <summary>
    /// Gets or sets the maximum resource count.
    /// </summary>
    public int MaxResourceCount { get; set; } = 0;

    /// <summary>
    /// Gets or sets the maximum allowed subscription expiration minutes.
    /// </summary>
    public int MaxSubscriptionExpirationMinutes { get; set; } = 30;

    /// <summary>
    /// Whether or not to check for changed resources and send NotModified if not changed.
    /// </summary>
    public bool SupportNotChanged { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether SMART is required.
    /// </summary>
    public bool SmartRequired { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether SMART is allowed.
    /// </summary>
    public bool SmartAllowed { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether to allow existing identifier.
    /// </summary>
    public bool AllowExistingId { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to allow create as update.
    /// </summary>
    public bool AllowCreateAsUpdate { get; set; } = true;
}
