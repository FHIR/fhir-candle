// <copyright file="SmartController.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using System.Net;
using System.Xml.Linq;
using fhir.candle.Models;
using fhir.candle.Services;
using FhirCandle.Models;
using FhirCandle.Smart;
using Hl7.Fhir.Rest;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;

//using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace fhir.candle.Controllers;

/// <summary>A FHIR API controller.</summary>
[ApiController]
[Route("_smart", Order = 1)]
[Produces("application/json")]
public class SmartController : ControllerBase
{
    private ISmartAuthManager _smartAuthManager;

    private ILogger<ISmartAuthManager> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SmartController"/> class.
    /// </summary>
    /// <param name="smartAuthManager"></param>
    /// <param name="logger"></param>
    /// <exception cref="ArgumentNullException">Thrown when one or more required arguments are null.</exception>
    public SmartController(
        [FromServices] ISmartAuthManager smartAuthManager,
        [FromServices] ILogger<ISmartAuthManager> logger)
    {
        _smartAuthManager = smartAuthManager ?? throw new ArgumentNullException(nameof(smartAuthManager));
        _logger = logger;
    }

    /// <summary>(An Action that handles HTTP GET requests) gets smart well known.</summary>
    /// <param name="storeName">The store.</param>
    /// <returns>An asynchronous result.</returns>
    [HttpGet, Route("{storeName}/.well-known/smart-configuration")]
    public async Task GetSmartWellKnown(
        [FromRoute] string storeName)
    {
        // make sure this store exists and has SMART enabled
        if (!_smartAuthManager.SmartConfigurationByTenant.TryGetValue(
                storeName,
                out FhirStore.Smart.SmartWellKnown? smartConfig))
        {
            _logger.LogWarning($"GetSmartWellKnown <<< tenant {storeName} does not exist!");
            Response.StatusCode = 404;
            return;
        }

        // SMART well-known configuration is always returned as JSON
        Response.ContentType = "application/json";
        Response.StatusCode = (int)HttpStatusCode.OK;

        await Response.WriteAsync(FhirCandle.Serialization.SerializationCommon.SerializeObject(smartConfig));
    }
    
    /// <summary>
    /// Handles HTTP GET requests for SMART authorization.
    /// </summary>
    /// <param name="storeName">The name of the store.</param>
    /// <param name="responseType">The type of the response.</param>
    /// <param name="clientId">The client ID.</param>
    /// <param name="redirectUri">The redirect URI.</param>
    /// <param name="launch">The launch parameter.</param>
    /// <param name="scope">The scope of the request.</param>
    /// <param name="state">The state of the request.</param>
    /// <param name="audience">The audience of the request.</param>
    /// <param name="pkceChallenge">The PKCE challenge.</param>
    /// <param name="pkceMethod">The PKCE method.</param>
    /// <param name="queryAuthBypass">The query authorization bypass.</param>
    /// <param name="queryPatient">The query patient.</param>
    /// <param name="queryPractitioner">The query practitioner.</param>
    /// <param name="headerAuthBypass">The header authorization bypass.</param>
    /// <param name="headerPatient">The header patient.</param>
    /// <param name="headerPractitioner">The header practitioner.</param>
    [HttpGet, Route("{storeName}/authorize")]
    public void GetSmartAuthorize(
        [FromRoute] string storeName,
        [FromQuery(Name = "response_type")] string responseType,
        [FromQuery(Name = "client_id")] string clientId,
        [FromQuery(Name = "redirect_uri")] string redirectUri,
        [FromQuery(Name = "launch")] string? launch,
        [FromQuery(Name = "scope")] string scope,
        [FromQuery(Name = "state")] string state,
        [FromQuery(Name = "aud")] string audience,
        [FromQuery(Name = "code_challenge")] string? pkceChallenge,
        [FromQuery(Name = "code_challenge_method")] string? pkceMethod,
        [FromQuery(Name = "candle_auth_bypass")] string? queryAuthBypass,
        [FromQuery(Name = "candle_patient")] string? queryPatient,
        [FromQuery(Name = "candle_practitioner")] string? queryPractitioner,
        [FromHeader(Name = "candle-auth-bypass")] string? headerAuthBypass,
        [FromHeader(Name = "candle-patient")] string? headerPatient,
        [FromHeader(Name = "candle-practitioner")] string? headerPractitioner)
    {
        if (!_smartAuthManager.RequestAuth(
                storeName,
                Request.HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty,
                responseType,
                clientId,
                redirectUri,
                launch,
                scope,
                state,
                audience,
                pkceChallenge,
                pkceMethod,
                out string redirectDestination,
                out string authKey))
        {
            _logger.LogWarning($"GetSmartAuthorize <<< request for {clientId} failed!");
            Response.StatusCode = 404;
            return;
        }

        string bypassType = queryAuthBypass ?? headerAuthBypass ?? string.Empty;

        if (!string.IsNullOrEmpty(bypassType.ToLowerInvariant()))
        {
            _ = _smartAuthManager.TryGetAuthorization(storeName, authKey, out AuthorizationInfo auth);

            string patient = queryPatient ?? headerPatient ?? string.Empty;
            string practitioner = queryPractitioner ?? headerPractitioner ?? string.Empty;

            switch (bypassType)
            {
                case "admin":
                case "administrator":
                case "user":
                    {
                        auth.UserId = "administrator";
                        auth.LaunchPatient = patient;
                        auth.LaunchPractitioner = practitioner;
                    }
                    break;

                case "patient":
                    {
                        auth.UserId = patient;
                        auth.LaunchPatient = patient;
                    }
                    break;

                case "practitioner":
                    {
                        auth.UserId = practitioner;
                        auth.LaunchPatient = patient;
                        auth.LaunchPractitioner = practitioner;
                    }
                    break;
            }

            // perform login step
            _ = _smartAuthManager.TryUpdateAuth(storeName, authKey, auth);

            // approve all scopes
            foreach (string scopeKey in auth.Scopes.Keys)
            {
                auth.Scopes[scopeKey] = true;
            }

            // perform authorization step
            _ = _smartAuthManager.TryUpdateAuth(storeName, authKey, auth);

            // redirect back to client
            if (_smartAuthManager.TryGetClientRedirect(storeName, authKey, out string redirect))
            {
                Response.Redirect(redirect);
                return;
            }
        }

        Response.Redirect(redirectDestination);
    }

    /// <summary>
    /// (An Action that handles HTTP POST requests) posts a smart token request.
    /// </summary>
    /// <param name="storeName">     The store.</param>
    /// <param name="authHeader">(Optional) The authentication header.</param>
    /// <returns>An asynchronous result.</returns>
    [HttpPost, Route("{storeName}/token")]
    [Consumes("application/x-www-form-urlencoded")]
    [Produces("application/json")]
    public async Task PostSmartTokenRequest(
        [FromRoute] string storeName,
        [FromHeader(Name = "Authorization")] string? authHeader = null)
    {
        // make sure this store exists and has SMART enabled
        if (!_smartAuthManager.SmartConfigurationByTenant.TryGetValue(
                storeName,
                out FhirStore.Smart.SmartWellKnown? smartConfig))
        {
            _logger.LogWarning($"PostSmartTokenRequest <<< tenant {storeName} does not exist!");
            Response.StatusCode = 404;
            return;
        }
        
        try
        {
            string grantType = string.Empty;
            string authCode = string.Empty;
            string refreshToken = string.Empty;
            string redirectUri = string.Empty;
            string clientId = string.Empty;
            string clientSecret = string.Empty;
            string codeVerifier = string.Empty;
            string clientAssertionType = string.Empty;
            string clientAssertion = string.Empty;
            IEnumerable<string> scopes = Enumerable.Empty<string>();

            foreach ((string key, Microsoft.Extensions.Primitives.StringValues values) in Request.Form)
            {
                switch (key.ToLowerInvariant())
                {
                    case "grant_type":
                        grantType = values.FirstOrDefault() ?? string.Empty;
                        break;
                    case "code":
                        authCode = values.FirstOrDefault() ?? string.Empty;
                        break;
                    case "redirect_uri":
                        redirectUri = values.FirstOrDefault() ?? string.Empty;
                        break;
                    case "client_id":
                        clientId = values.FirstOrDefault() ?? string.Empty;
                        break;
                    case "refresh_token":
                        refreshToken = values.FirstOrDefault() ?? string.Empty;
                        break;
                    case "client_secret":
                        clientSecret = values.FirstOrDefault() ?? string.Empty;
                        break;
                    case "code_verifier":
                        codeVerifier = values.FirstOrDefault() ?? string.Empty;
                        break;
                    case "client_assertion_type":
                        clientAssertionType = values.FirstOrDefault() ?? string.Empty;
                        break;
                    case "client_assertion":
                        clientAssertion = values.FirstOrDefault() ?? string.Empty;
                        break;
                    case "scope":
                        scopes = values;
                        break;
                }
            }

            if (!string.IsNullOrEmpty(authHeader))
            {
                string[] authComponents = authHeader.Split(' ', StringSplitOptions.TrimEntries);

                if (authComponents.Length == 2)
                {
                    if (authComponents[0].ToLowerInvariant() == "basic")
                    {
                        string[] clientCreds = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(authComponents[1])).Split(':', StringSplitOptions.TrimEntries);

                        if (clientCreds.Length == 2)
                        {
                            clientId = clientCreds[0];
                            clientSecret = clientCreds[1];
                        }
                    }
                }   
            }

            AuthorizationInfo.SmartResponse? smart = null;

            switch (grantType)
            {
                case "authorization_code":
                    {
                        if (!_smartAuthManager.TryCreateSmartResponse(
                                storeName,
                                authCode,
                                clientId,
                                clientSecret,
                                codeVerifier,
                                out smart))
                        {
                            _logger.LogWarning($"PostSmartTokenRequest <<< exchange code {authCode} failed!");
                            Response.StatusCode = 400;
                            return;
                        }
                    }
                    break;

                case "refresh_token":
                    {
                        if (!_smartAuthManager.TrySmartRefresh(
                                storeName,
                                refreshToken,
                                clientId,
                                out smart))
                        {
                            _logger.LogWarning($"PostSmartTokenRequest <<< exchange refresh token {refreshToken} failed!");
                            Response.StatusCode = 400;
                            return;
                        }
                    }
                    break;

                case "client_credentials":
                    {
                        if (!_smartAuthManager.TryClientAssertionExchange(
                                storeName,
                                Request.HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty,
                                clientAssertionType,
                                clientAssertion,
                                scopes,
                                out smart,
                                out List<string> messages))
                        {
                            _logger.LogWarning($"PostSmartTokenRequest <<< exchange client assertion {clientAssertion} failed!");
                            
                            Response.ContentType = "text/plain";
                            Response.StatusCode = (int)HttpStatusCode.BadRequest;
                            await Response.WriteAsync(string.Join("\n", messages));
                            return;
                        }
                    }
                    break;
            }

            if (smart == null)
            {
                _logger.LogWarning($"PostSmartTokenRequest <<< request for {grantType} failed!");
                Response.StatusCode = 400;
                return;
            }

            // SMART token exchange response is JSON
            Response.ContentType = "application/json";
            Response.StatusCode = (int)HttpStatusCode.OK;

            await Response.WriteAsync(FhirCandle.Serialization.SerializationCommon.SerializeObject(smart));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.InnerException == null
                ? $"PostSmartTokenRequest <<< caught: {ex.Message}"
                : $"PostSmartTokenRequest <<< caught: {ex.Message}, inner: {ex.InnerException.Message}");

            Response.StatusCode = 500;
            return;
        }

    }

    /// <summary>
    /// (An Action that handles HTTP POST requests) posts a smart token request.
    /// </summary>
    /// <param name="storeName">     The store.</param>
    /// <param name="authHeader">(Optional) The authentication header.</param>
    /// <returns>An asynchronous result.</returns>
    [HttpPost, Route("{storeName}/register")]
    [Consumes("application/json")]
    [Produces("application/json")]
    public async Task PostSmartRegistrationRequest(
        [FromRoute] string storeName,
        [FromHeader(Name = "Authorization")] string? authHeader = null)
    {
        // make sure this store exists and has SMART enabled
        if (!_smartAuthManager.SmartConfigurationByTenant.TryGetValue(
                storeName,
                out FhirStore.Smart.SmartWellKnown? smartConfig))
        {
            _logger.LogWarning($"PostSmartRegistrationRequest <<< tenant {storeName} does not exist!");
            Response.StatusCode = 404;
            return;
        }
        
        try
        {
            // read the post body to process
            using (StreamReader reader = new StreamReader(Request.Body))
            {
                string content = await reader.ReadToEndAsync();

                // pull the key set
                JsonWebKeySet clientKeySet = new(content);

                // pull any other elements
                SmartClientRegistration? smartClientRegistration = System.Text.Json.JsonSerializer.Deserialize<SmartClientRegistration>(content);
                if (smartClientRegistration == null)
                {
                    Response.ContentType = "text/plain";
                    Response.StatusCode = (int)HttpStatusCode.BadRequest;
                    await Response.WriteAsync($"Parsing of content failed!\nInvalid content:\n----\n{content}\n----");
                    return;
                }

                smartClientRegistration.KeySet = clientKeySet;

                // operation
                bool success = _smartAuthManager.TryRegisterClient(
                    smartClientRegistration, 
                    out string clientId,
                    out List<string> messages);

                if (!success)
                {
                    _logger.LogError("PostSmartRegistrationRequest <<< TryRegisterClient failed.");

                    Response.ContentType = "text/plain";
                    Response.StatusCode = (int)HttpStatusCode.BadRequest;
                    await Response.WriteAsync(string.Join("\n", messages));

                    return;
                }

                SmartClientRegistratonResponse resp = new()
                {
                    ClientId = clientId,
                };

                // SMART token exchange response is JSON
                Response.ContentType = "application/json";
                Response.StatusCode = (int)HttpStatusCode.OK;

                await Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(resp));
            }
        }
        catch (Exception ex)
        {
            string msg = ex.InnerException == null ? $"PostSmartRegistrationRequest <<< caught: {ex.Message}" : $"PostSmartRegistrationRequest <<< caught: {ex.Message}, inner: {ex.InnerException.Message}";
            _logger.LogError(msg);
            Response.StatusCode = 500;
            return;
        }
    }

    /// <summary>
    /// (An Action that handles HTTP POST requests) posts a smart token introspect.
    /// </summary>
    /// <param name="storeName">The store.</param>
    /// <returns>An asynchronous result.</returns>
    [HttpPost, Route("{storeName}/introspect")]
    [Consumes("application/x-www-form-urlencoded")]
    [Produces("application/json")]
    public async Task PostSmartTokenIntrospect(
        [FromRoute] string storeName)
    {
        // make sure this store exists and has SMART enabled
        if (!_smartAuthManager.SmartConfigurationByTenant.TryGetValue(
                storeName,
                out FhirStore.Smart.SmartWellKnown? smartConfig))
        {
            _logger.LogWarning($"PostSmartTokenIntrospect <<< tenant {storeName} does not exist!");
            Response.StatusCode = 404;
            return;
        }
        
        try
        {
            string token = string.Empty;
            string tokenTypeHint = string.Empty;

            foreach ((string key, Microsoft.Extensions.Primitives.StringValues values) in Request.Form)
            {
                switch (key.ToLowerInvariant())
                {
                    case "token":
                        token = values.FirstOrDefault() ?? string.Empty;
                        break;
                    case "token_type_hint":
                        tokenTypeHint = values.FirstOrDefault() ?? string.Empty;
                        break;
                }
            }

            if (!_smartAuthManager.TryIntrospection(
                    storeName,
                    token,
                    out AuthorizationInfo.IntrospectionResponse? resp))
            {
                _logger.LogWarning($"PostSmartTokenIntrospect <<< introspection failed!");
                Response.StatusCode = 400;
                return;
            }

            // SMART token exchange response is JSON
            Response.ContentType = "application/json";
            Response.StatusCode = (int)HttpStatusCode.OK;

            await Response.WriteAsync(FhirCandle.Serialization.SerializationCommon.SerializeObject(resp));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.InnerException == null
                ? $"PostSmartTokenIntrospect <<< caught: {ex.Message}"
                : $"PostSmartTokenIntrospect <<< caught: {ex.Message}, inner: {ex.InnerException.Message}");

            Response.StatusCode = 500;
            return;
        }
    }
}
