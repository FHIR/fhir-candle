// <copyright file="MinimalBundle.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using System.Text.Json.Serialization;

namespace fhir.candle.Tests.Models;

/// <summary>A minimal Bundle structure, for fast deserialization.</summary>
public class MinimalBundle
{
    /// <summary>A minimal Bundle.entry structure.</summary>
    public class MinimalEntry
    {
        /// <summary>A minimal Bundle.entry.search structure.</summary>
        public class MinimalSearch
        {
            /// <summary>Gets or sets the mode.</summary>
            [JsonPropertyName("mode")]
            public string Mode { get; set; } = string.Empty;
        }

        public class MinimalMeta
        {
            [JsonPropertyName("versionId")]
            public string? VersionId { get; set; } = null;

            [JsonPropertyName("lastUpdated")]
            public string? LastUpdated { get; set; } = null;
        }

        public class MinimalR5Event
        {
            [JsonPropertyName("eventNumber")]
            public string EventNumber { get; set; } = string.Empty;

            [JsonPropertyName("timestamp")]
            public string Timestamp { get; set; } = string.Empty;

            [JsonPropertyName("focus")]
            public object? Focus { get; set; } = null;

            [JsonPropertyName("additionalContext")]
            public IEnumerable<object>? AdditionalContext { get; set; } = null;
        }


        public class MinimalResource
        {
            [JsonPropertyName("id")]
            public string Id { get; set; } = string.Empty;

            [JsonPropertyName("resourceType")]
            public string ResourceType { get; set; } = string.Empty;

            [JsonPropertyName("meta")]
            public MinimalMeta? Meta { get; set; } = null;

            // notification status R5 properties
            [JsonPropertyName("status")]
            public object? Status { get; set; } = null;

            [JsonPropertyName("type")]
            public object? NotificationType { get; set; } = null;

            [JsonPropertyName("eventsSinceSubscriptionStart")]
            public object? EventsSinceSubscriptionStart { get; set; } = null;
        }

        public class MinimalIssue
        {
            [JsonPropertyName("severity")]
            public string Severity { get; set; } = string.Empty;

            [JsonPropertyName("code")]
            public string Code { get; set; } = string.Empty;

            [JsonPropertyName("diagnostics")]
            public string? Diagnostics { get; set; } = null;
        }

        public class MinimalOutcome : MinimalResource
        {
            [JsonPropertyName("issue")]
            public IEnumerable<MinimalIssue> Issues { get; set; } = [];
        }

        public class MinimalRequest
        {
            [JsonPropertyName("method")]
            public string Method { get; set; } = string.Empty;
            [JsonPropertyName("url")]
            public string Url { get; set; } = string.Empty;
        }

        public class MinimalResponse
        {
            [JsonPropertyName("status")]
            public string Status { get; set; } = string.Empty;

            [JsonPropertyName("location")]
            public string? Location { get; set; } = null;

            [JsonPropertyName("etag")]
            public string? ETag { get; set; } = null;

            [JsonPropertyName("lastModified")]
            public string? LastModified { get; set; } = null;

            [JsonPropertyName("outcome")]
            public MinimalOutcome? Outcome { get; set; } = null;
        }

        /// <summary>Gets or sets URL of the full.</summary>
        [JsonPropertyName("fullUrl")]
        public string FullUrl { get; set; } = string.Empty;

        /// <summary>Gets or sets the search.</summary>
        [JsonPropertyName("search")]
        public MinimalSearch? Search { get; set; } = null;

        /// <summary>
        /// Gets or sets the resource.
        /// </summary>
        [JsonPropertyName("resource")]
        public MinimalResource? Resource { get; set; } = null;

        [JsonPropertyName("request")]
        public MinimalRequest? Request { get; set; } = null;

        [JsonPropertyName("response")]
        public MinimalResponse? Response { get; set; } = null;
    }

    /// <summary>A minimal link.</summary>
    public class MinimalLink
    {
        /// <summary>Gets or sets the relation.</summary>
        [JsonPropertyName("relation")]
        public string Relation { get; set; } = string.Empty;

        /// <summary>Gets or sets URL of the document.</summary>
        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;
    }

    /// <summary>Gets or sets the type of the bundle.</summary>
    [JsonPropertyName("type")]
    public string BundleType { get; set; } = string.Empty;

    /// <summary>Gets or sets the total number of matches, if this is a search bundle. </summary>
    [JsonPropertyName("total")]
    public int Total { get; set; } = -1;

    /// <summary>Gets or sets the links.</summary>
    [JsonPropertyName("link")]
    public IEnumerable<MinimalLink>? Links { get; set; } = null;

    /// <summary>Gets or sets the entries.</summary>
    [JsonPropertyName("entry")]
    public IEnumerable<MinimalEntry>? Entries { get; set; } = null;
}
