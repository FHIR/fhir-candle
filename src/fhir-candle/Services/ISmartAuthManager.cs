// <copyright file="ISmartAuthManager.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using FhirCandle.Models;
using FhirCandle.Smart;
using FhirStore.Smart;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Hosting;

namespace fhir.candle.Services;

/// <summary>Interface for smart authentication manager.</summary>
public interface ISmartAuthManager : IHostedService
{
    /// <summary>Initializes the FHIR Store Manager and tenants.</summary>
    void Init();

    /// <summary>Query if 'tenant' exists.</summary>
    /// <param name="tenant">The tenant name.</param>
    /// <returns>True if the tenant exists, false if not.</returns>
    bool HasTenant(string tenant);

    /// <summary>Gets a value indicating whether this object is enabled.</summary>
    bool IsEnabled { get; }

    /// <summary>Gets the smart configuration by tenant.</summary>
    Dictionary<string, SmartWellKnown> SmartConfigurationByTenant { get; }

    /// <summary>Gets the authorizations.</summary>
    Dictionary<string, AuthorizationInfo> SmartAuthorizations { get; }

    /// <summary>Gets the clients.</summary>
    Dictionary<string, ClientInfo> SmartClients { get; }

    /// <summary>Attempts to get authorization.</summary>
    /// <param name="tenant">The tenant name.</param>
    /// <param name="key">   The key.</param>
    /// <param name="auth">  [out] The authentication.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    bool TryGetAuthorization(string tenant, string key, out AuthorizationInfo auth);

    /// <summary>Gets an authorization.</summary>
    /// <param name="tenant">The tenant name.</param>
    /// <param name="key">   The key.</param>
    /// <returns>The authorization.</returns>
    AuthorizationInfo? GetAuthorization(string tenant, string key);

    /// <summary>Query if this request is authorized.</summary>
    /// <param name="ctx">The context.</param>
    /// <returns>True if authorized, false if not.</returns>
    bool IsAuthorized(FhirRequestContext ctx);

    /// <summary>Attempts to update authentication.</summary>
    /// <param name="tenant">The tenant name.</param>
    /// <param name="key">   The key.</param>
    /// <param name="auth">  [out] The authentication.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    bool TryUpdateAuth(string tenant, string key, AuthorizationInfo auth);

    /// <summary>Attempts to get the authorization client redirect URL.</summary>
    /// <param name="tenant">  The tenant name.</param>
    /// <param name="key">     The key.</param>
    /// <param name="redirect">[out] The redirect.</param>
    /// <param name="error"></param>
    /// <param name="errorDescription"></param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    bool TryGetClientRedirect(
        string tenant, 
        string key, 
        out string redirect,
        string error = "",
        string errorDescription = "");

    /// <summary>Attempts to create smart response.</summary>
    /// <param name="tenant">      The tenant name.</param>
    /// <param name="authCode">    The authentication code.</param>
    /// <param name="clientId">    The client's identifier.</param>
    /// <param name="clientSecret">The client secret.</param>
    /// <param name="codeVerifier">The code verifier.</param>
    /// <param name="response">    [out] The response.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    bool TryCreateSmartResponse(
        string tenant, 
        string authCode, 
        string clientId,
        string clientSecret,
        string codeVerifier,
        out AuthorizationInfo.SmartResponse response);

    /// <summary>Attempts to client assertion exchange.</summary>
    /// <param name="tenantName">         The tenant name.</param>
    /// <param name="remoteIpAddress">    The remote IP address.</param>
    /// <param name="clientAssertionType">Type of the client assertion.</param>
    /// <param name="clientAssertion">    The client assertion.</param>
    /// <param name="scopes">             The scopes.</param>
    /// <param name="response">           [out] The response.</param>
    /// <param name="messages">           [out] The messages.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    bool TryClientAssertionExchange(
        string tenantName,
        string remoteIpAddress,
        string clientAssertionType,
        string clientAssertion,
        IEnumerable<string> scopes,
        out AuthorizationInfo.SmartResponse response,
        out List<string> messages);

    /// <summary>Generates a signed jwt.</summary>
    /// <param name="issuer">    The issuer.</param>
    /// <param name="subject">   The subject.</param>
    /// <param name="audience">  URL of the EHR resource server from which the app wishes to retrieve
    ///  FHIR data.</param>
    /// <param name="jti">       The jti.</param>
    /// <param name="expiration">The expiration Date/Time.</param>
    /// <param name="webKey">    The web key.</param>
    /// <returns>The signed jwt.</returns>
    string GenerateSignedJwt(
        string issuer,
        string subject,
        string audience,
        string jti,
        DateTime expiration,
        JsonWebKey webKey);

    /// <summary>
    /// Attempts to register client a string from the given SmartClientRegistration.
    /// </summary>
    /// <param name="registration">The registration.</param>
    /// <param name="clientId">    [out] The client's identifier.</param>
    /// <param name="messages">    [out] The messages.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    bool TryRegisterClient(
        SmartClientRegistration registration,
        out string clientId,
        out List<string> messages);

    /// <summary>Attempts to exchange a refresh token for a new access token.</summary>
    /// <param name="tenant">      The tenant.</param>
    /// <param name="refreshToken">The refresh token.</param>
    /// <param name="clientId">    The client's identifier.</param>
    /// <param name="response">    [out] The response.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    bool TrySmartRefresh(
        string tenant,
        string refreshToken,
        string clientId,
        out AuthorizationInfo.SmartResponse response);

    /// <summary>Attempts to introspection.</summary>
    /// <param name="tenant">  The tenant name.</param>
    /// <param name="token">   The token.</param>
    /// <param name="response">[out] The response.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    bool TryIntrospection(
        string tenant,
        string token,
        out AuthorizationInfo.IntrospectionResponse? response);

    /// <summary>Request authentication.</summary>
    /// <param name="tenant">             The tenant.</param>
    /// <param name="remoteIpAddress">    The remote IP address.</param>
    /// <param name="responseType">       Fixed value: code.</param>
    /// <param name="clientId">           The client's identifier.</param>
    /// <param name="redirectUri">        Must match one of the client's pre-registered redirect URIs.</param>
    /// <param name="launch">             When using the EHR Launch flow, this must match the launch
    ///  value received from the EHR. Omitted when using the Standalone Launch.</param>
    /// <param name="scope">              Must describe the access that the app needs.</param>
    /// <param name="state">              An opaque value used by the client to maintain state between
    ///  the request and callback.</param>
    /// <param name="audience">           URL of the EHR resource server from which the app wishes to
    ///  retrieve FHIR data.</param>
    /// <param name="pkceChallenge">      This parameter is generated by the app and used for the code
    ///  challenge, as specified by PKCE. (required v2, opt v1)</param>
    /// <param name="pkceMethod">         Method used for the code_challenge parameter. (required v2,
    ///  opt v1)</param>
    /// <param name="redirectDestination">[out] The redirect destination.</param>
    /// <param name="authKey"></param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    bool RequestAuth(
        string tenant,
        string remoteIpAddress,
        string responseType,
        string clientId,
        string redirectUri,
        string? launch,
        string scope,
        string state,
        string audience,
        string? pkceChallenge,
        string? pkceMethod,
        out string redirectDestination,
        out string authKey);
}
