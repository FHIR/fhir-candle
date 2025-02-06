// <copyright file="AuthorizationInfo.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using FhirCandle.Storage;
using System.Net;
using System.Text.Json.Serialization;

namespace FhirCandle.Models;

/// <summary>Authorization information for internal tracking.</summary>
public class AuthorizationInfo
{
    private SmartRequest _requestParameters = null!;

    /// <summary>Information about the authentication activity.</summary>
    public readonly record struct AuthActivityRecord
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AuthActivityRecord"/> class.
        /// </summary>
        public AuthActivityRecord() { }

        /// <summary>Gets or initializes the type of the request.</summary>
        public required string RequestType { get; init; }

        /// <summary>Gets or initializes a value indicating whether the success.</summary>
        public required bool Success { get; init; }

        /// <summary>Gets or initializes the message.</summary>
        public string Message { get; init; } = string.Empty;

        /// <summary>Gets or initializes the timestamp.</summary>
        public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    }

    /// <summary>An introspection response.</summary>
    public readonly record struct IntrospectionResponse
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IntrospectionResponse"/> class.
        /// </summary>
        public IntrospectionResponse() { }

        [JsonPropertyName("active")]
        public required bool Active { get; init; }

        [JsonPropertyName("scope")]
        public required string Scopes { get; init; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonPropertyName("client_id")]
        public string? ClientId { get; init; } = null;

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonPropertyName("username")]
        public string? Username { get; init; } = null;

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonPropertyName("token_type")]
        public string? TokenType { get; init; } = null;

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonPropertyName("exp")]
        public long? ExpiresAt { get; init; } = null;

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonPropertyName("iat")]
        public long? IssuedAt { get; init; } = null;

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonPropertyName("nbf")]
        public long? NotUsedBefore { get; init; } = null;

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonPropertyName("sub")]
        public required string Subject { get; init; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonPropertyName("aud")]
        public required string Audience { get; init; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonPropertyName("iss")]
        public string? Issuer { get; init; } = null;

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonPropertyName("jti")]
        public string? TokenIdentifier { get; init; } = null;
    }

    /// <summary>Authorization request parameters.</summary>
    public class SmartRequest
    {
        [JsonPropertyName("response_type")]
        public string ResponseType { get; init; } = string.Empty;

        [JsonPropertyName("client_id")]
        public string ClientId { get; init; } = string.Empty;

        [JsonPropertyName("redirect_uri")]
        public string RedirectUri { get; init; } = string.Empty;

        [JsonPropertyName("launch")]
        public string? Launch { get; init; } = string.Empty;

        [JsonPropertyName("scope")]
        public string Scope { get; init; } = string.Empty;

        [JsonPropertyName("state")]
        public string State { get; init; } = string.Empty;

        [JsonPropertyName("aud")]
        public string Audience { get; init; } = string.Empty;

        [JsonPropertyName("code_challenge")]
        public string? PkceChallenge { get; init; } = string.Empty;

        [JsonPropertyName("code_challenge_method")]
        public string? PkceMethod { get; init; } = string.Empty;
    }

    public record class SmartFhirContext
    {
        [JsonPropertyName("type")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string? Type { get; init; } = null;

        [JsonPropertyName("reference")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string? Reference { get; init; } = null;

        [JsonPropertyName("canonical")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string? Canonical { get; init; } = null;

        [JsonPropertyName("identifier")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string? Identifier { get; init; } = null;

        [JsonPropertyName("role")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string? Role { get; init; } = null;
    }

    /// <summary>A smart response.</summary>
    public record class SmartResponse
    {
        [JsonPropertyName("need_patient_banner")]
        public bool NeedPatientBanner { get; init; } = true;

        [JsonPropertyName("smart_style_url")]
        public string SmartStyleUrl { get; init; } = string.Empty;

        [JsonPropertyName("patient")]
        public string PatientId { get; init; } = string.Empty;

        [JsonPropertyName("fhirContext")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public IEnumerable<SmartFhirContext>? FhirContext { get; init; } = null;

        [JsonPropertyName("token_type")]
        public string TokenType { get; init; } = string.Empty;

        [JsonPropertyName("scope")]
        public string Scopes { get; init; } = string.Empty;

        [JsonPropertyName("client_id")]
        public string ClientId { get; init; } = string.Empty;

        [JsonPropertyName("expires_in")]
        public int ExpiresInSeconds { get; init; } = 60 * 10;

        [JsonPropertyName("id_token")]
        public string IdToken { get; init; } = string.Empty;

        [JsonPropertyName("access_token")]
        public string AccessToken { get; init; } = string.Empty;

        [JsonPropertyName("refresh_token")]
        public string RefreshToken { get; init; } = string.Empty;
    }

    /// <summary>Gets or initializes the key.</summary>
    public string Key { get; init; } = Guid.NewGuid().ToString();

    /// <summary>Gets or initializes the tenant name.</summary>
    public required string Tenant { get; init; }

    /// <summary>Gets or initializes options for controlling the request.</summary>
    public required SmartRequest RequestParameters 
    { 
        get => _requestParameters;
        init
        {
            _requestParameters = value;

            Scopes = System.Web.HttpUtility.UrlDecode(value.Scope)
                .Split(' ')
                .ToDictionary(s => s, s => true);
        }
    }

    /// <summary>Gets or initializes the remote IP address.</summary>
    public required string RemoteIpAddress { get; init; }

    /// <summary>Gets or initializes the created.</summary>
    public DateTimeOffset Created { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Gets or initializes the last accessed.</summary>
    public DateTimeOffset LastAccessed { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Gets or sets the expiration time.</summary>
    public DateTimeOffset Expires { get; set; } = DateTimeOffset.UtcNow.AddMinutes(30);

    /// <summary>Gets or sets the identifier of the user.</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>Gets or initializes the launch patient.</summary>
    public string LaunchPatient { get; set; } = string.Empty;

    /// <summary>Gets or initializes the launch practitioner.</summary>
    public string LaunchPractitioner { get; set; } = string.Empty;

    /// <summary>Gets or initializes the scopes.</summary>
    public Dictionary<string, bool> Scopes { get; init; } = [];

    /// <summary>Gets or initializes the normalized allowed patient-based scopes.</summary>
    public HashSet<string> PatientScopes { get; set; } = [];

    /// <summary>Gets or initializes the normalized allowed patient-based scopes.</summary>
    public HashSet<string> UserScopes { get; set; } = [];

    /// <summary>Gets or sets the authentication code.</summary>
    public string AuthCode { get; set; } = string.Empty;

    /// <summary>The latest response.</summary>
    public SmartResponse? Response = null;

    /// <summary>Gets the activity.</summary>
    public List<AuthActivityRecord> Activity { get; } = [];

    /// <summary>Query if this object is authorized.</summary>
    /// <returns>True if authorized, false if not.</returns>
    public bool IsAuthorized(
        Common.StoreInteractionCodes interaction,
        string httpMethod,
        string resourceType)
    {
        if (UserScopes.Contains("*.*"))
        {
            return true;
        }

        switch (interaction)
        {
            case Storage.Common.StoreInteractionCodes.InstanceDelete:
            case Storage.Common.StoreInteractionCodes.InstanceDeleteHistory:
            case Storage.Common.StoreInteractionCodes.InstanceDeleteVersion:
            case Storage.Common.StoreInteractionCodes.TypeDeleteConditional:
                {
                    if (PatientScopes.Contains("*.d") ||
                        UserScopes.Contains("*.d") ||
                        PatientScopes.Contains(resourceType + ".d") ||
                        UserScopes.Contains(resourceType + ".d"))
                    {
                        return true;
                    }

                    return false;
                }

            case Storage.Common.StoreInteractionCodes.InstanceOperation:
            case Storage.Common.StoreInteractionCodes.TypeOperation:
                {
                    switch (httpMethod)
                    {
                        case "HEAD":
                        case "GET":
                            {
                                if (PatientScopes.Contains("*.r") ||
                                    UserScopes.Contains("*.r") ||
                                    PatientScopes.Contains(resourceType + ".r") ||
                                    UserScopes.Contains(resourceType + ".r"))
                                {
                                    return true;
                                }

                                return false;
                            }

                        case "POST":
                        case "PUT":
                            {
                                if (PatientScopes.Contains("*.u") ||
                                    UserScopes.Contains("*.u") ||
                                    PatientScopes.Contains(resourceType + ".u") ||
                                    UserScopes.Contains(resourceType + ".u"))
                                {
                                    return true;
                                }

                                return false;
                            }
                    }

                    return false;
                }

            case Storage.Common.StoreInteractionCodes.InstancePatch:
            case Storage.Common.StoreInteractionCodes.InstanceUpdate:
                {
                    if (PatientScopes.Contains("*.u") ||
                        UserScopes.Contains("*.u") ||
                        PatientScopes.Contains(resourceType + ".u") ||
                        UserScopes.Contains(resourceType + ".u"))
                    {
                        return true;
                    }

                    return false;
                }

            case Storage.Common.StoreInteractionCodes.InstanceRead:
            case Storage.Common.StoreInteractionCodes.InstanceReadHistory:
            case Storage.Common.StoreInteractionCodes.InstanceReadVersion:
            case Storage.Common.StoreInteractionCodes.TypeHistory:
                {
                    if (PatientScopes.Contains("*.r") ||
                        UserScopes.Contains("*.r") ||
                        PatientScopes.Contains(resourceType + ".r") ||
                        UserScopes.Contains(resourceType + ".r"))
                    {
                        return true;
                    }

                    return false;
                }

            case Storage.Common.StoreInteractionCodes.CompartmentTypeSearch:
            case Storage.Common.StoreInteractionCodes.TypeSearch:
                {
                    if (PatientScopes.Contains("*.s") ||
                        UserScopes.Contains("*.s") ||
                        PatientScopes.Contains(resourceType + ".s") ||
                        UserScopes.Contains(resourceType + ".s"))
                    {
                        return true;
                    }

                    return false;
                }

            case Storage.Common.StoreInteractionCodes.TypeCreate:
            case Storage.Common.StoreInteractionCodes.TypeCreateConditional:
                {
                    if (PatientScopes.Contains("*.c") ||
                        UserScopes.Contains("*.c") ||
                        PatientScopes.Contains(resourceType + ".c") ||
                        UserScopes.Contains(resourceType + ".c"))
                    {
                        return true;
                    }

                    return false;
                }

            case Storage.Common.StoreInteractionCodes.SystemCapabilities:
                {
                    // always allow capabilities test
                    return true;
                }

            case Storage.Common.StoreInteractionCodes.SystemBundle:
                {
                    // system bundle requests are authorized here and can fail in individual requests
                    return true;
                }

            case Storage.Common.StoreInteractionCodes.SystemDeleteConditional:
                {
                    if (PatientScopes.Contains("*.d") ||
                        UserScopes.Contains("*.d"))
                    {
                        return true;
                    }

                    return false;
                }

            case Storage.Common.StoreInteractionCodes.SystemHistory:
                {
                    if (PatientScopes.Contains("*.r") ||
                        UserScopes.Contains("*.r"))
                    {
                        return true;
                    }

                    return false;
                }

            case Storage.Common.StoreInteractionCodes.SystemOperation:
                {
                    switch (httpMethod)
                    {
                        case "HEAD":
                        case "GET":
                            {
                                if (PatientScopes.Contains("*.r") ||
                                    UserScopes.Contains("*.r"))
                                {
                                    return true;
                                }

                                return false;
                            }

                        case "POST":
                        case "PUT":
                            {
                                if (PatientScopes.Contains("*.u") ||
                                    UserScopes.Contains("*.u"))
                                {
                                    return true;
                                }

                                return false;
                            }
                    }

                    return false;
                }

            // TODO: compartment search should be clever and check the intersection of the sets of resources in the compartment
            case Storage.Common.StoreInteractionCodes.CompartmentSearch:
            case Storage.Common.StoreInteractionCodes.SystemSearch:
                {
                    if (PatientScopes.Contains("*.s") ||
                        UserScopes.Contains("*.s"))
                    {
                        return true;
                    }

                    return false;
                }

            default:
                break;
        }

        return false;
    }
}
