﻿// <copyright file="ISmartAuthManager.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using fhir.candle.Models;
using FhirStore.Smart;
using Microsoft.AspNetCore.Mvc;
using System.CommandLine;

namespace fhir.candle.Services;
public interface ISmartAuthManager : IHostedService
{
    /// <summary>Initializes the FHIR Store Manager and tenants.</summary>
    void Init();

    /// <summary>Query if 'tenant' exists.</summary>
    /// <param name="tenant">The tenant name.</param>
    /// <returns>True if the tenant exists, false if not.</returns>
    bool HasTenant(string tenant);

    /// <summary>Gets the smart configuration by tenant.</summary>
    Dictionary<string, SmartWellKnown> SmartConfigurationByTenant { get; }

    /// <summary>Attempts to get authorization.</summary>
    /// <param name="tenant">The tenant name.</param>
    /// <param name="key">   The key.</param>
    /// <param name="auth">  [out] The authentication.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    bool TryGetAuthorization(string tenant, string key, out AuthorizationInfo auth);

    /// <summary>Query if this request is authorized.</summary>
    /// <param name="tenant">         The tenant name.</param>
    /// <param name="accessToken">    The access token.</param>
    /// <param name="httpMethod">     The HTTP method.</param>
    /// <param name="interaction">    The interaction.</param>
    /// <param name="resourceType">   Type of the resource.</param>
    /// <param name="operationName">  Name of the operation.</param>
    /// <param name="compartmentType">Type of the compartment.</param>
    /// <returns>True if authorized, false if not.</returns>
    bool IsAuthorized(
        string tenant,
        string accessToken,
        string httpMethod,
        FhirCandle.Storage.Common.StoreInteractionCodes interaction,
        string resourceType,
        string operationName,
        string compartmentType);

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
        out string redirectDestination);
}
