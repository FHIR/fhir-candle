// <copyright file="FhirController.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net;
using fhir.candle.Services;
using Fhir.Metrics;
using FhirCandle.Models;
using FhirCandle.Storage;
using Hl7.Fhir.Rest;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Net.Http.Headers;


namespace fhir.candle.Controllers;

/// <summary>A FHIR API controller.</summary>
[ApiController]
[Route("fhir", Order = 2)]
[Produces("application/fhir+json", new[] { "application/fhir+xml", "application/json", "application/xml" })]
public class FhirController : ControllerBase
{
    private IFhirStoreManager _fhirStoreManager;
    private ISmartAuthManager _smartAuthManager;

    private ILogger<FhirController> _logger;

    private readonly HashSet<string> _acceptMimeTypes =
    [
        "application/fhir+json",
        "application/fhir+xml",
        "application/json",
        "application/xml"
        //"json",
        //"xml",
    ];

    /// <summary>
    /// Initializes a new instance of the <see cref="FhirController"/> class.
    /// </summary>
    /// <param name="fhirStoreManager"></param>
    /// <param name="smartAuthManager"></param>
    /// <param name="logger"></param>
    /// <exception cref="ArgumentNullException"></exception>
    public FhirController(
        [FromServices] IFhirStoreManager fhirStoreManager,
        [FromServices] ISmartAuthManager smartAuthManager,
        [FromServices] ILogger<FhirController> logger)
    {
        _fhirStoreManager = fhirStoreManager ?? throw new ArgumentNullException(nameof(fhirStoreManager));
        _smartAuthManager = smartAuthManager ?? throw new ArgumentNullException(nameof(smartAuthManager));
        _logger = logger;

        //if (host != null)
        //{
        //    ICollection<string> addresses = host.Features?.Get<IServerAddressesFeature>()?.Addresses ?? Array.Empty<string>();
        //    foreach (string address in addresses)
        //    {
        //        Console.WriteLine($"Listening on: {address}");
        //    }
        //}

    }

    /// <summary>Gets the MIME type.</summary>
    /// <param name="queryParam">The query parameter.</param>
    /// <param name="request">   The request.</param>
    /// <param name="allowNonFhirReturn"></param>
    /// <returns>The mime type.</returns>
    private string getMimeType(string? queryParam, HttpRequest request, bool allowNonFhirReturn = false)
    {
        if (!string.IsNullOrEmpty(queryParam))
        {
            if (_acceptMimeTypes.Contains(queryParam) ||
                allowNonFhirReturn)
            {
                return queryParam;
            }

            if (queryParam.Contains(' '))
            {
                queryParam = queryParam.Replace(' ', '+');
            }

            if (_acceptMimeTypes.Contains(queryParam))
            {
                return queryParam;
            }
        }

        foreach (string? accept in request.Headers?.Accept ?? Array.Empty<string>())
        {
            if (string.IsNullOrEmpty(accept))
            {
                continue;
            }

            if (_acceptMimeTypes.Contains(accept) ||
                allowNonFhirReturn)
            {
                return accept;
            }
        }

        return "application/fhir+json";
    }

    private static FhirRequestContext.ForwardedInfo? getForwardedInfo(HttpRequest request)
    {
        if (request.Headers.TryGetValue("Forwarded", out StringValues forwarded))
        {
            return new FhirRequestContext.ForwardedInfo(forwarded.ToString());
        }

        string? forwardedFor = request.Headers.TryGetValue("X-Forwarded-For", out StringValues ff) ? ff.ToString() : null;
        string? forwardedHost = request.Headers.TryGetValue("X-Forwarded-Host", out StringValues fh) ? fh.ToString() : null;
        string? forwardedProto = request.Headers.TryGetValue("X-Forwarded-Proto", out StringValues fp) ? fp.ToString() : null;
        string? forwardedPrefix = request.Headers.TryGetValue("X-Forwarded-Prefix", out StringValues fpr) ? fpr.ToString() : null;

        if ((forwardedFor != null) || (forwardedHost != null) || (forwardedProto != null) || (forwardedPrefix != null))
        {
            return new FhirRequestContext.ForwardedInfo(forwardedFor, forwardedHost, forwardedProto, forwardedPrefix);
        }

        return null;
    }

    /// <summary>Adds a body.</summary>
    /// <param name="response">The response.</param>
    /// <param name="prefer">  The prefer.</param>
    /// <param name="resource">The resource.</param>
    /// <param name="outcome"> The outcome.</param>
    /// <returns>An asynchronous result.</returns>
    private async Task AddBody(HttpResponse response, string? prefer, string resource, string outcome)
    {
        switch (prefer)
        {
            case "return=minimal":
                break;

            default:
            case "return=representation":
                if (!string.IsNullOrEmpty(resource))
                {
                    await response.WriteAsync(resource);
                }
                else if (!string.IsNullOrEmpty(outcome))
                {
                    await response.WriteAsync(outcome);
                }
                break;

            case "return=OperationOutcome":
                if (!string.IsNullOrEmpty(outcome))
                {
                    await response.WriteAsync(outcome);
                }
                break;
        }
    }

    /// <summary>Adds a FHIR response.</summary>
    /// <param name="response">  The response.</param>
    /// <param name="prefer">    The prefer.</param>
    /// <param name="success">   True if the operation was a success, false if it failed.</param>
    /// <param name="opResponse">The operation response.</param>
    /// <returns>An asynchronous result.</returns>
    private async Task AddFhirResponse(HttpResponse response, string? prefer, bool success, FhirResponseContext opResponse)
    {
        if (!string.IsNullOrEmpty(opResponse.ETag))
        {
            Response.Headers[HeaderNames.ETag] = opResponse.ETag;
        }

        if (!string.IsNullOrEmpty(opResponse.LastModified))
        {
            Response.Headers[HeaderNames.LastModified] = opResponse.LastModified;
        }

        if (!string.IsNullOrEmpty(opResponse.Location))
        {
            Response.Headers[HeaderNames.Location] = opResponse.Location;
        }

        Response.ContentType = opResponse.MimeType;
        if (opResponse.StatusCode == null)
        {
            Response.StatusCode = success ? (int)HttpStatusCode.OK : (int)HttpStatusCode.InternalServerError;
        }
        else
        {
            Response.StatusCode = (int)opResponse.StatusCode;
        }

        // check for not modified (no body)
        if (Response.StatusCode == (int)HttpStatusCode.NotModified)
        {
            return;
        }

        switch (prefer)
        {
            case "return=minimal":
                break;

            default:
            case "return=representation":
                if (!string.IsNullOrEmpty(opResponse.SerializedResource))
                {
                    await response.WriteAsync(opResponse.SerializedResource);
                }
                else if (!string.IsNullOrEmpty(opResponse.SerializedOutcome))
                {
                    await response.WriteAsync(opResponse.SerializedOutcome);
                }
                break;

            case "return=OperationOutcome":
                if (!string.IsNullOrEmpty(opResponse.SerializedOutcome))
                {
                    await response.WriteAsync(opResponse.SerializedOutcome);
                }
                break;
        }
    }

    /// <summary>Logs and return error.</summary>
    /// <param name="response">The response.</param>
    /// <param name="code">    The code.</param>
    /// <param name="msg">     The message.</param>
    /// <returns>An asynchronous result.</returns>
    private async Task LogAndReturnError(HttpResponse response, int code, string msg)
    {
        _logger.LogError(msg);

        response.StatusCode = code;
        response.ContentType = "text/plain";
        await response.WriteAsync(msg);
    }

    /// <summary>(An Action that handles HTTP GET requests) gets smart well known.</summary>
    /// <remarks>
    /// Note that most "core" SMART stuff is in the <see cref="SmartController"/>, but this needs to
    /// remain here to be discoverable by FHIR clients.
    /// </remarks>
    /// <param name="storeName">The store.</param>
    /// <returns>An asynchronous result.</returns>
    [HttpGet, Route("{storeName}/.well-known/smart-configuration")]
    [Produces("application/json")]
    public async Task GetSmartWellKnown(
        [FromRoute] string storeName)
    {
        // make sure this store exists and has SMART enabled
        if (!_smartAuthManager.SmartConfigurationByTenant.TryGetValue(
                storeName, 
                out FhirStore.Smart.SmartWellKnown? smartConfig))
        {
            await LogAndReturnError(Response, 404, $"GetSmartWellKnown <<< no SMART config for {storeName}!");
            return;
        }

        // SMART well-known configuration is always returned as JSON
        Response.ContentType = "application/json";
        Response.StatusCode = (int)HttpStatusCode.OK;

        await Response.WriteAsync(FhirCandle.Serialization.SerializationCommon.SerializeObject(smartConfig));
    }

    /// <summary>(An Action that handles HTTP GET requests) gets a metadata.</summary>
    /// <param name="storeName">The store.</param>
    /// <param name="format">   Describes the format to use.</param>
    /// <param name="pretty">   The pretty.</param>
    /// <param name="authHeader"></param>
    /// <returns>An asynchronous result.</returns>
    [HttpGet, Route("{storeName}/metadata")]
    public async Task GetMetadata(
        [FromRoute] string storeName,
        [FromQuery(Name = "_format")] string? format,
        [FromQuery(Name = "_pretty")] string? pretty,
        [FromHeader(Name = "Authorization")] string? authHeader)
    {
        if (!_fhirStoreManager.TryGetValue(storeName, out IFhirStore? store))
        {
            await LogAndReturnError(Response, 404, $"GetMetadata <<< no tenant at {storeName}!");
            return;
        }

        FhirRequestContext ctx = new()
        {
            TenantName = storeName,
            Store = store,
            HttpMethod = Request.Method.ToUpperInvariant(),
            Url = Request.GetDisplayUrl(),
            UrlPath = Request.Path,
            UrlQuery = Request.QueryString.ToString(),
            RequestHeaders = Request.Headers.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            Forwarded = getForwardedInfo(Request),
            Authorization = _smartAuthManager.GetAuthorization(storeName, authHeader ?? string.Empty),
            DestinationFormat = getMimeType(format, Request),
            SerializePretty = pretty?.Equals("true", StringComparison.Ordinal) ?? false,
            Interaction = Common.StoreInteractionCodes.SystemCapabilities,
        };

        if (!_smartAuthManager.IsAuthorized(ctx))
        {
            Response.StatusCode = 401;
            return;
        }

        bool success = _fhirStoreManager[storeName].GetMetadata(
            ctx,
            out FhirResponseContext opResponse);

        await AddFhirResponse(Response, null, success, opResponse);
    }

    /// <summary>(An Action that handles HTTP GET requests) gets type operation.</summary>
    /// <param name="storeName">       The store.</param>
    /// <param name="resourceName">Name of the resource.</param>
    /// <param name="opName">      Name of the operation.</param>
    /// <param name="format">      Describes the format to use.</param>
    /// <param name="summary">     The summary.</param>
    /// <param name="pretty">      The pretty.</param>
    /// <param name="prefer">      The prefer.</param>
    /// <param name="authHeader"></param>
    /// <returns>An asynchronous result.</returns>
    [HttpGet, Route("{storeName}/{resourceName}/${opName}")]
    public async Task GetTypeOperation(
        [FromRoute] string storeName,
        [FromRoute] string resourceName,
        [FromRoute] string opName,
        [FromQuery(Name = "_format")] string? format,
        [FromQuery(Name = "_summary")] string? summary,
        [FromQuery(Name = "_pretty")] string? pretty,
        [FromHeader(Name = "Prefer")] string? prefer,
        [FromHeader(Name = "Authorization")] string? authHeader)
    {
        if (!_fhirStoreManager.TryGetValue(storeName, out IFhirStore? store))
        {
            await LogAndReturnError(Response, 404, $"GetTypeOperation <<< no tenant at {storeName}!");
            return;
        }

        if (!store.SupportsResource(resourceName))
        {
            await LogAndReturnError(Response, 404, $"GetTypeOperation <<< tenant {storeName} does not support resource {resourceName}!");
            return;
        }

        FhirRequestContext ctx = new()
        {
            TenantName = storeName,
            Store = store,
            HttpMethod = Request.Method.ToUpperInvariant(),
            Url = Request.GetDisplayUrl(),
            UrlPath = Request.Path,
            UrlQuery = Request.QueryString.ToString(),
            RequestHeaders = Request.Headers.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            Forwarded = getForwardedInfo(Request),
            Authorization = _smartAuthManager.GetAuthorization(storeName, authHeader ?? string.Empty),
            DestinationFormat = getMimeType(format, Request),
            SerializePretty = pretty?.Equals("true", StringComparison.Ordinal) ?? false,
            Interaction = Common.StoreInteractionCodes.TypeOperation,
            OperationName = "$" + opName,
        };

        if (!_smartAuthManager.IsAuthorized(ctx))
        {
            Response.StatusCode = 401;
            return;
        }

        bool success = store.TypeOperation(
            ctx,
            out FhirResponseContext opResponse);

        await AddFhirResponse(Response, prefer, success, opResponse);
    }

    /// <summary>(An Action that handles HTTP GET requests) gets resource instance.</summary>
    /// <param name="storeName">      The store.</param>
    /// <param name="resourceName">   Name of the resource.</param>
    /// <param name="id">             The identifier.</param>
    /// <param name="format">         Describes the format to use.</param>
    /// <param name="summary">        The summary.</param>
    /// <param name="pretty">         The pretty.</param>
    /// <param name="ifMatch">        A match specifying if.</param>
    /// <param name="ifModifiedSince">if modified since.</param>
    /// <param name="ifNoneMatch">    A match specifying if none.</param>
    /// <param name="authHeader">     The authentication header.</param>
    /// <returns>An asynchronous result.</returns>
    [HttpGet, Route("{storeName}/{resourceName}/{id}")]
    public async Task GetResourceInstance(
        [FromRoute] string storeName,
        [FromRoute] string resourceName,
        [FromRoute] string id,
        [FromQuery(Name = "_format")] string? format,
        [FromQuery(Name = "_summary")] string? summary,
        [FromQuery(Name = "_pretty")] string? pretty,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        [FromHeader(Name = "If-Modified-Since")] string? ifModifiedSince,
        [FromHeader(Name = "If-None-Match")] string? ifNoneMatch,
        [FromHeader(Name = "Authorization")] string? authHeader)
    {
        if (!_fhirStoreManager.TryGetValue(storeName, out IFhirStore? store))
        {
            await LogAndReturnError(Response, 404, $"GetResourceInstance <<< no tenant at {storeName}!");
            return;
        }

        if (!store.SupportsResource(resourceName))
        {
            await LogAndReturnError(Response, 404, $"GetResourceInstance <<< tenant {storeName} does not support resource {resourceName}!");
            return;
        }

        FhirRequestContext ctx = new()
        {
            TenantName = storeName,
            Store = store,
            HttpMethod = Request.Method.ToUpperInvariant(),
            Url = Request.GetDisplayUrl(),
            UrlPath = Request.Path,
            UrlQuery = Request.QueryString.ToString(),
            RequestHeaders = Request.Headers.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            Forwarded = getForwardedInfo(Request),
            Authorization = _smartAuthManager.GetAuthorization(storeName, authHeader ?? string.Empty),
            DestinationFormat = getMimeType(format, Request),
            SerializePretty = pretty?.Equals("true", StringComparison.Ordinal) ?? false,
            Interaction = Common.StoreInteractionCodes.InstanceRead,
            ResourceType = resourceName,
            Id = id,
            IfMatch = ifMatch ?? string.Empty,
            IfModifiedSince = ifModifiedSince ?? string.Empty,
            IfNoneMatch = ifNoneMatch ?? string.Empty,
        };

        if (!_smartAuthManager.IsAuthorized(ctx))
        {
            Response.StatusCode = 401;
            return;
        }

        bool success = store.InstanceRead(
            ctx,
            out FhirResponseContext opResponse);

        // cannot prefer outcome on a read
        await AddFhirResponse(Response, null, success, opResponse);
    }

    /// <summary>(An Action that handles HTTP GET requests) gets instance operation.</summary>
    /// <param name="storeName">       The store.</param>
    /// <param name="resourceName">Name of the resource.</param>
    /// <param name="id">          The identifier.</param>
    /// <param name="opName">      Name of the operation.</param>
    /// <param name="format">      Describes the format to use.</param>
    /// <param name="summary">     The summary.</param>
    /// <param name="pretty">      The pretty.</param>
    /// <param name="prefer"></param>
    /// <param name="authHeader"></param>
    /// <returns>An asynchronous result.</returns>
    [HttpGet, Route("{storeName}/{resourceName}/{id}/${opName}")]
    public async Task GetInstanceOperation(
        [FromRoute] string storeName,
        [FromRoute] string resourceName,
        [FromRoute] string id,
        [FromRoute] string opName,
        [FromQuery(Name = "_format")] string? format,
        [FromQuery(Name = "_summary")] string? summary,
        [FromQuery(Name = "_pretty")] string? pretty,
        [FromHeader(Name = "Prefer")] string? prefer,
        [FromHeader(Name = "Authorization")] string? authHeader)
    {
        if (!_fhirStoreManager.TryGetValue(storeName, out IFhirStore? store))
        {
            await LogAndReturnError(Response, 404, $"GetInstanceOperation <<< no tenant at {storeName}!");
            return;
        }

        if (!store.SupportsResource(resourceName))
        {
            await LogAndReturnError(Response, 404, $"GetInstanceOperation <<< tenant {storeName} does not support resource {resourceName}!");
            return;
        }

        FhirRequestContext ctx = new()
        {
            TenantName = storeName,
            Store = store,
            HttpMethod = Request.Method.ToUpperInvariant(),
            Url = Request.GetDisplayUrl(),
            UrlPath = Request.Path,
            UrlQuery = Request.QueryString.ToString(),
            RequestHeaders = Request.Headers.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            Forwarded = getForwardedInfo(Request),
            Authorization = _smartAuthManager.GetAuthorization(storeName, authHeader ?? string.Empty),
            DestinationFormat = getMimeType(format, Request),
            SerializePretty = pretty?.Equals("true", StringComparison.Ordinal) ?? false,
            SerializeSummaryFlag = summary ?? string.Empty,
            Interaction = Common.StoreInteractionCodes.InstanceOperation,
            ResourceType = resourceName,
            Id = id,
            OperationName = "$" + opName,
        };

        if (!_smartAuthManager.IsAuthorized(ctx))
        {
            Response.StatusCode = 401;
            return;
        }

        // operation
        bool success = store.InstanceOperation(
            ctx,
            out FhirResponseContext opResponse);

        await AddFhirResponse(Response, prefer, success, opResponse);
    }

    /// <summary>
    /// (An Action that handles HTTP POST requests) posts an instance operation.
    /// </summary>
    /// <param name="storeName"></param>
    /// <param name="resourceName">Name of the resource.</param>
    /// <param name="id">          The identifier.</param>
    /// <param name="opName">      Name of the operation.</param>
    /// <param name="format">      Describes the format to use.</param>
    /// <param name="summary">     The summary.</param>
    /// <param name="pretty">      The pretty.</param>
    /// <param name="prefer"></param>
    /// <param name="authHeader"></param>
    /// <returns>An asynchronous result.</returns>
    [HttpPost, Route("{storeName}/{resourceName}/{id}/${opName}")]
    //[Consumes("application/fhir+json", new[] { "application/fhir+xml", "application/json", "application/xml" })]
    public async Task PostInstanceOperation(
        [FromRoute] string storeName,
        [FromRoute] string resourceName,
        [FromRoute] string id,
        [FromRoute] string opName,
        [FromQuery(Name = "_format")] string? format,
        [FromQuery(Name = "_summary")] string? summary,
        [FromQuery(Name = "_pretty")] string? pretty,
        [FromHeader(Name = "Prefer")] string? prefer,
        [FromHeader(Name = "Authorization")] string? authHeader)
    {
        if (!_fhirStoreManager.TryGetValue(storeName, out IFhirStore? store))
        {
            await LogAndReturnError(Response, 404, $"PostInstanceOperation <<< no tenant at {storeName}!");
            return;
        }

        if (!store.SupportsResource(resourceName))
        {
            await LogAndReturnError(Response, 404, $"PostInstanceOperation <<< tenant {storeName} does not support resource {resourceName}!");
            return;
        }

        try
        {
            // read the post body to process
            using StreamReader reader = new StreamReader(Request.Body);
            string content = await reader.ReadToEndAsync();

            FhirRequestContext ctx = new()
            {
                TenantName = storeName,
                Store = store,
                HttpMethod = Request.Method.ToUpperInvariant(),
                Url = Request.GetDisplayUrl(),
                UrlPath = Request.Path,
                UrlQuery = Request.QueryString.ToString(),
                RequestHeaders = Request.Headers.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                Forwarded = getForwardedInfo(Request),
                Authorization = _smartAuthManager.GetAuthorization(storeName, authHeader ?? string.Empty),
                DestinationFormat = getMimeType(format, Request),
                SerializePretty = pretty?.Equals("true", StringComparison.Ordinal) ?? false,
                Interaction = Common.StoreInteractionCodes.InstanceOperation,
                ResourceType = resourceName,
                Id = id,
                OperationName = "$" + opName,
                SourceFormat = Request.ContentType ?? string.Empty,
                SourceContent = content,
            };

            if (!_smartAuthManager.IsAuthorized(ctx))
            {
                Response.StatusCode = 401;
                return;
            }

            // operation
            bool success = store.InstanceOperation(
                ctx,
                out FhirResponseContext opResponse);

            await AddFhirResponse(Response, prefer, success, opResponse);
        }
        catch (Exception ex)
        {
            string msg = ex.InnerException == null ? $"PostInstanceOperation <<< caught: {ex.Message}" : $"PostInstanceOperation <<< caught: {ex.Message}, inner: {ex.InnerException.Message}";
            await LogAndReturnError(Response, 500, msg);
            return;
        }
    }


    /// <summary>
    /// (An Action that handles HTTP POST requests) posts a resource type search.
    /// </summary>
    /// <param name="storeName">       The store.</param>
    /// <param name="resourceName">Name of the resource.</param>
    /// <param name="format">      Describes the format to use.</param>
    /// <param name="pretty">      The pretty.</param>
    /// <param name="summary">     The summary.</param>
    /// <param name="authHeader"></param>
    /// <returns>An asynchronous result.</returns>
    [HttpPost, Route("{storeName}/{resourceName}/_search")]
    [Consumes("application/x-www-form-urlencoded")]
    public async Task PostResourceTypeSearch(
        [FromRoute] string storeName,
        [FromRoute] string resourceName,
        [FromQuery(Name = "_format")] string? format,
        [FromQuery(Name = "_pretty")] string? pretty,
        [FromQuery(Name = "_summary")] string? summary,
        [FromHeader(Name = "Authorization")] string? authHeader)
    {
        if (!_fhirStoreManager.TryGetValue(storeName, out IFhirStore? store))
        {
            await LogAndReturnError(Response, 404, $"PostResourceTypeSearch <<< no tenant at {storeName}!");
            return;
        }

        if (!store.SupportsResource(resourceName))
        {
            await LogAndReturnError(Response, 404, $"PostResourceTypeSearch <<< tenant {storeName} does not support resource {resourceName}!");
            return;
        }

        try
        {
            // read the post body to process
            using StreamReader reader = new StreamReader(Request.Body);
            string content = await reader.ReadToEndAsync();
            string queryString = Request.QueryString.ToString();

            if (!string.IsNullOrEmpty(queryString))
            {
                if (string.IsNullOrEmpty(content))
                {
                    content = queryString;
                }
                else
                {
                    content += $"&{queryString}";
                }
            }

            FhirRequestContext ctx = new()
            {
                TenantName = storeName,
                Store = store,
                HttpMethod = Request.Method.ToUpperInvariant(),
                Url = Request.GetDisplayUrl(),
                UrlPath = Request.Path,
                UrlQuery = queryString,
                RequestHeaders = Request.Headers.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                Forwarded = getForwardedInfo(Request),
                Authorization = _smartAuthManager.GetAuthorization(storeName, authHeader ?? string.Empty),
                DestinationFormat = getMimeType(format, Request),
                SerializePretty = pretty?.Equals("true", StringComparison.Ordinal) ?? false,
                SerializeSummaryFlag = summary ?? string.Empty,
                Interaction = Common.StoreInteractionCodes.TypeSearch,
                ResourceType = resourceName,
                SourceContent = content,
                SourceFormat = Request.ContentType ?? string.Empty,
            };

            if (!_smartAuthManager.IsAuthorized(ctx))
            {
                Response.StatusCode = 401;
                return;
            }

            bool success = _fhirStoreManager[storeName].TypeSearch(
                ctx,
                out FhirResponseContext opResponse);

            // cannot prefer outcome on search
            await AddFhirResponse(Response, null, success, opResponse);
        }
        catch (Exception ex)
        {
            string msg = ex.InnerException == null ? $"PostResourceTypeSearch <<< caught: {ex.Message}" : $"PostResourceTypeSearch <<< caught: {ex.Message}, inner: {ex.InnerException.Message}";
            await LogAndReturnError(Response, 500, msg);
            return;
        }
    }

    /// <summary>(An Action that handles HTTP POST requests) posts a type operation.</summary>
    /// <param name="storeName">   The store.</param>
    /// <param name="resourceName">Name of the resource.</param>
    /// <param name="opName">      Name of the operation.</param>
    /// <param name="format">      Describes the format to use.</param>
    /// <param name="summary">     The summary.</param>
    /// <param name="pretty">      The pretty.</param>
    /// <param name="prefer">      The prefer.</param>
    /// <param name="authHeader">  The authentication header.</param>
    /// <returns>An asynchronous result.</returns>
    [HttpPost, Route("{storeName}/{resourceName}/${opName}")]
    //[Consumes("application/fhir+json", new[] { "application/fhir+xml", "application/json", "application/xml" })]
    public async Task PostTypeOperation(
        [FromRoute] string storeName,
        [FromRoute] string resourceName,
        [FromRoute] string opName,
        [FromQuery(Name = "_format")] string? format,
        [FromQuery(Name = "_summary")] string? summary,
        [FromQuery(Name = "_pretty")] string? pretty,
        [FromHeader(Name = "Prefer")] string? prefer,
        [FromHeader(Name = "Authorization")] string? authHeader)
    {
        if (!_fhirStoreManager.TryGetValue(storeName, out IFhirStore? store))
        {
            await LogAndReturnError(Response, 404, $"PostTypeOperation <<< no tenant at {storeName}!");
            return;
        }

        if (!store.SupportsResource(resourceName))
        {
            await LogAndReturnError(Response, 404, $"PostTypeOperation <<< tenant {storeName} does not support resource {resourceName}!");
            return;
        }

        try
        {
            // read the post body to process
            using StreamReader reader = new StreamReader(Request.Body);
            string content = await reader.ReadToEndAsync();

            FhirRequestContext ctx = new()
            {
                TenantName = storeName,
                Store = store,
                HttpMethod = Request.Method.ToUpperInvariant(),
                Url = Request.GetDisplayUrl(),
                UrlPath = Request.Path,
                UrlQuery = Request.QueryString.ToString(),
                RequestHeaders = Request.Headers.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                Forwarded = getForwardedInfo(Request),
                Authorization = _smartAuthManager.GetAuthorization(storeName, authHeader ?? string.Empty),
                DestinationFormat = getMimeType(format, Request),
                SerializePretty = pretty?.Equals("true", StringComparison.Ordinal) ?? false,
                SerializeSummaryFlag = summary ?? string.Empty,
                Interaction = Common.StoreInteractionCodes.TypeOperation,
                ResourceType = resourceName,
                OperationName = "$" + opName,
                SourceFormat = Request.ContentType ?? string.Empty,
                SourceContent = content,
            };

            if (!_smartAuthManager.IsAuthorized(ctx))
            {
                Response.StatusCode = 401;
                return;
            }

            // operation
            bool success = store.TypeOperation(
                ctx,
                out FhirResponseContext opResponse);

            await AddFhirResponse(Response, prefer, success, opResponse);
        }
        catch (Exception ex)
        {
            string msg = ex.InnerException == null ? $"PostTypeOperation <<< caught: {ex.Message}" : $"PostTypeOperation <<< caught: {ex.Message}, inner: {ex.InnerException.Message}";
            await LogAndReturnError(Response, 500, msg);
            return;
        }
    }

    /// <summary>(An Action that handles HTTP POST requests) posts a system search.</summary>
    /// <param name="storeName">  The store.</param>
    /// <param name="format"> Describes the format to use.</param>
    /// <param name="pretty"> The pretty.</param>
    /// <param name="summary">The summary.</param>
    /// <param name="authHeader"></param>
    /// <returns>An asynchronous result.</returns>
    [HttpPost, Route("{storeName}/_search")]
    [Consumes("application/x-www-form-urlencoded")]
    public async Task PostSystemSearch(
        [FromRoute] string storeName,
        [FromQuery(Name = "_format")] string? format,
        [FromQuery(Name = "_pretty")] string? pretty,
        [FromQuery(Name = "_summary")] string? summary,
        [FromHeader(Name = "Authorization")] string? authHeader)
    {
        if (!_fhirStoreManager.TryGetValue(storeName, out IFhirStore? store))
        {
            await LogAndReturnError(Response, 404, $"PostSystemSearch <<< no tenant at {storeName}!");
            return;
        }

        try
        {
            // read the post body to process
            using StreamReader reader = new StreamReader(Request.Body);
            string content = await reader.ReadToEndAsync();
            string queryString = Request.QueryString.ToString();

            if (!string.IsNullOrEmpty(queryString))
            {
                if (string.IsNullOrEmpty(content))
                {
                    content = queryString;
                }
                else
                {
                    content += $"&{queryString}";
                }
            }

            FhirRequestContext ctx = new()
            {
                TenantName = storeName,
                Store = store,
                HttpMethod = Request.Method.ToUpperInvariant(),
                Url = Request.GetDisplayUrl(),
                UrlPath = Request.Path,
                UrlQuery = queryString,
                RequestHeaders = Request.Headers.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                Forwarded = getForwardedInfo(Request),
                Authorization = _smartAuthManager.GetAuthorization(storeName, authHeader ?? string.Empty),
                DestinationFormat = getMimeType(format, Request),
                SerializePretty = pretty?.Equals("true", StringComparison.Ordinal) ?? false,
                SerializeSummaryFlag = summary ?? string.Empty,
                Interaction = Common.StoreInteractionCodes.SystemSearch,
                SourceFormat = Request.ContentType ?? string.Empty,
                SourceContent = content,
            };

            if (!_smartAuthManager.IsAuthorized(ctx))
            {
                Response.StatusCode = 401;
                return;
            }

            bool success = store.SystemSearch(
                ctx,
                out FhirResponseContext opResponse);

            // cannot prefer outcome on search
            await AddFhirResponse(Response, null, success, opResponse);
        }
        catch (Exception ex)
        {
            string msg = ex.InnerException == null ? $"PostSystemSearch <<< caught: {ex.Message}" : $"PostSystemSearch <<< caught: {ex.Message}, inner: {ex.InnerException.Message}";
            await LogAndReturnError(Response, 500, msg);
            return;
        }
    }

    /// <summary>(An Action that handles HTTP POST requests) posts a system operation.</summary>
    /// <param name="storeName"> The store.</param>
    /// <param name="opName">    Name of the operation.</param>
    /// <param name="format">    Describes the format to use.</param>
    /// <param name="pretty">    The pretty.</param>
    /// <param name="prefer">    The prefer.</param>
    /// <param name="authHeader">The authentication header.</param>
    /// <returns>An asynchronous result.</returns>
    [HttpPost, Route("{storeName}/${opName}")]
    //[Consumes("application/fhir+json", new[] { "application/fhir+xml", "application/json", "application/xml" })]
    public async Task PostSystemOperation(
        [FromRoute] string storeName,
        [FromRoute] string opName,
        [FromQuery(Name = "_format")] string? format,
        [FromQuery(Name = "_pretty")] string? pretty,
        [FromHeader(Name = "Prefer")] string? prefer,
        [FromHeader(Name = "Authorization")] string? authHeader)
    {
        if (!_fhirStoreManager.TryGetValue(storeName, out IFhirStore? store))
        {
            await LogAndReturnError(Response, 404, $"PostSystemOperation <<< no tenant at {storeName}!");
            return;
        }

        try
        {
            // read the post body to process
            using (StreamReader reader = new StreamReader(Request.Body))
            {
                string content = await reader.ReadToEndAsync();

                FhirRequestContext ctx = new()
                {
                    TenantName = storeName,
                    Store = store,
                    HttpMethod = Request.Method.ToUpperInvariant(),
                    Url = Request.GetDisplayUrl(),
                    UrlPath = Request.Path,
                    UrlQuery = Request.QueryString.ToString(),
                    RequestHeaders = Request.Headers.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                    Forwarded = getForwardedInfo(Request),
                    Authorization = _smartAuthManager.GetAuthorization(storeName, authHeader ?? string.Empty),
                    DestinationFormat = getMimeType(format, Request),
                    SerializePretty = pretty?.Equals("true", StringComparison.Ordinal) ?? false,
                    Interaction = Common.StoreInteractionCodes.SystemOperation,
                    OperationName = "$" + opName,
                    SourceFormat = Request.ContentType ?? string.Empty,
                    SourceContent = content,
                };

                if (!_smartAuthManager.IsAuthorized(ctx))
                {
                    Response.StatusCode = 401;
                    return;
                }

                // re-add the prefix $ character since it was stripped during routing

                bool success= store.SystemOperation(
                    ctx,
                    out FhirResponseContext opResponse);

                await AddFhirResponse(Response, prefer, success, opResponse);
            }
        }
        catch (Exception ex)
        {
            string msg = ex.InnerException == null ? $"PostSystemOperation <<< caught: {ex.Message}" : $"PostSystemOperation <<< caught: {ex.Message}, inner: {ex.InnerException.Message}";
            await LogAndReturnError(Response, 500, msg);
            return;
        }
    }

    
    /// <summary>(An Action that handles HTTP POST requests) posts a system operation.</summary>
    /// <param name="storeName"> The store.</param>
    /// <param name="opName">    Name of the operation.</param>
    /// <param name="format">    Describes the format to use.</param>
    /// <param name="pretty">    The pretty.</param>
    /// <param name="prefer">    The prefer.</param>
    /// <param name="authHeader">The authentication header.</param>
    /// <returns>An asynchronous result.</returns>
    [HttpGet, Route("{storeName}/${opName}")]
    //[Consumes("application/fhir+json", new[] { "application/fhir+xml", "application/json", "application/xml" })]
    public async Task GetSystemOperation(
        [FromRoute] string storeName,
        [FromRoute] string opName,
        [FromQuery(Name = "_format")] string? format,
        [FromQuery(Name = "_pretty")] string? pretty,
        [FromHeader(Name = "Prefer")] string? prefer,
        [FromHeader(Name = "Authorization")] string? authHeader)
    {
        if (!_fhirStoreManager.TryGetValue(storeName, out IFhirStore? store))
        {
            await LogAndReturnError(Response, 404, $"GetSystemOperation <<< no tenant at {storeName}!");
            return;
        }

        try
        {
            FhirRequestContext ctx = new()
            {
                TenantName = storeName,
                Store = store,
                HttpMethod = Request.Method.ToUpperInvariant(),
                Url = Request.GetDisplayUrl(),
                UrlPath = Request.Path,
                UrlQuery = Request.QueryString.ToString(),
                RequestHeaders = Request.Headers.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                Forwarded = getForwardedInfo(Request),
                Authorization = _smartAuthManager.GetAuthorization(storeName, authHeader ?? string.Empty),
                DestinationFormat = getMimeType(format, Request),
                SerializePretty = pretty?.Equals("true", StringComparison.Ordinal) ?? false,
                Interaction = Common.StoreInteractionCodes.SystemOperation,
                OperationName = "$" + opName,
            };

            if (!_smartAuthManager.IsAuthorized(ctx))
            {
                Response.StatusCode = 401;
                return;
            }

            // re-add the prefix $ character since it was stripped during routing

            bool success= store.SystemOperation(
                ctx,
                out FhirResponseContext opResponse);

            await AddFhirResponse(Response, prefer, success, opResponse);
        }
        catch (Exception ex)
        {
            string msg = ex.InnerException == null ? $"GetSystemOperation <<< caught: {ex.Message}" : $"PostSystemOperation <<< caught: {ex.Message}, inner: {ex.InnerException.Message}";
            await LogAndReturnError(Response, 500, msg);
            return;
        }
    }
    
    /// <summary>(An Action that handles HTTP POST requests) posts a resource type.</summary>
    /// <param name="storeName">   The store.</param>
    /// <param name="resourceName">Name of the resource.</param>
    /// <param name="format">      Describes the format to use.</param>
    /// <param name="pretty">      The pretty.</param>
    /// <param name="prefer">      The prefer.</param>
    /// <param name="ifNoneExist"> if none exist.</param>
    /// <param name="authHeader">  The authentication header.</param>
    /// <returns>An asynchronous result.</returns>
    [HttpPost, Route("{storeName}/{resourceName}")]
    [Consumes("application/fhir+json", new[] { "application/fhir+xml", "application/json", "application/xml" })]
    public async Task PostResourceType(
        [FromRoute] string storeName,
        [FromRoute] string resourceName,
        [FromQuery(Name = "_format")] string? format,
        [FromQuery(Name = "_pretty")] string? pretty,
        [FromHeader(Name = "Prefer")] string? prefer,
        [FromHeader(Name = "If-None-Exist")] string? ifNoneExist,
        [FromHeader(Name = "Authorization")] string? authHeader)
    {
        if (!_fhirStoreManager.TryGetValue(storeName, out IFhirStore? store))
        {
            await LogAndReturnError(Response, 404, $"PostResourceType <<< no tenant at {storeName}!");
            return;
        }

        if (!store.SupportsResource(resourceName))
        {
            await LogAndReturnError(Response, 404, $"PostResourceType <<< tenant {storeName} does not support resource {resourceName}!");
            return;
        }

        try
        {
            // read the post body to process
            using StreamReader reader = new StreamReader(Request.Body);
            string content = await reader.ReadToEndAsync();

            FhirRequestContext ctx = new()
            {
                TenantName = storeName,
                Store = store,
                HttpMethod = Request.Method.ToUpperInvariant(),
                Url = Request.GetDisplayUrl(),
                UrlPath = Request.Path,
                UrlQuery = Request.QueryString.ToString(),
                RequestHeaders = Request.Headers.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                Forwarded = getForwardedInfo(Request),
                Authorization = _smartAuthManager.GetAuthorization(storeName, authHeader ?? string.Empty),
                DestinationFormat = getMimeType(format, Request),
                SerializePretty = pretty?.Equals("true", StringComparison.Ordinal) ?? false,
                Interaction = Common.StoreInteractionCodes.TypeCreate,
                ResourceType = resourceName,
                SourceFormat = Request.ContentType ?? string.Empty,
                SourceContent = content,
                IfNoneExist = ifNoneExist ?? string.Empty,
            };

            if (!_smartAuthManager.IsAuthorized(ctx))
            {
                Response.StatusCode = 401;
                return;
            }

            bool success = store.InstanceCreate(
                ctx,
                out FhirResponseContext opResponse);

            await AddFhirResponse(Response, prefer, success, opResponse);
        }
        catch (Exception ex)
        {
            string msg = ex.InnerException == null ? $"PostResourceType <<< caught: {ex.Message}" : $"PostResourceType <<< caught: {ex.Message}, inner: {ex.InnerException.Message}";
            await LogAndReturnError(Response, 500, msg);
            return;
        }
    }

    /// <summary>(An Action that handles HTTP PUT requests) puts resource instance.</summary>
    /// <param name="storeName">   The store.</param>
    /// <param name="resourceName">Name of the resource.</param>
    /// <param name="id">          The identifier.</param>
    /// <param name="format">      Describes the format to use.</param>
    /// <param name="pretty">      The pretty.</param>
    /// <param name="prefer">      The prefer.</param>
    /// <param name="ifMatch">     A match specifying if.</param>
    /// <param name="ifNoneMatch"> A match specifying if none.</param>
    /// <param name="authHeader">  The authentication header.</param>
    /// <returns>An asynchronous result.</returns>
    [HttpPut, Route("{storeName}/{resourceName}/{id}")]
    [Consumes("application/fhir+json", new[] { "application/fhir+xml", "application/json", "application/xml" })]
    public async Task PutResourceInstance(
        [FromRoute] string storeName,
        [FromRoute] string resourceName,
        [FromRoute] string id,
        [FromQuery(Name = "_format")] string? format,
        [FromQuery(Name = "_pretty")] string? pretty,
        [FromHeader(Name = "Prefer")] string? prefer,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        [FromHeader(Name = "If-None-Match")] string? ifNoneMatch,
        [FromHeader(Name = "Authorization")] string? authHeader)
    {
        if (!_fhirStoreManager.TryGetValue(storeName, out IFhirStore? store))
        {
            await LogAndReturnError(Response, 404, $"PutResourceInstance <<< no tenant at {storeName}!");
            return;
        }

        if (!store.SupportsResource(resourceName))
        {
            await LogAndReturnError(Response, 404, $"PutResourceInstance <<< tenant {storeName} does not support resource {resourceName}!");
            return;
        }

        try
        {
            // read the post body to process
            using StreamReader reader = new StreamReader(Request.Body);
            string content = await reader.ReadToEndAsync();

            FhirRequestContext ctx = new()
            {
                TenantName = storeName,
                Store = store,
                HttpMethod = Request.Method.ToUpperInvariant(),
                Url = Request.GetDisplayUrl(),
                UrlPath = Request.Path,
                UrlQuery = Request.QueryString.ToString(),
                RequestHeaders = Request.Headers.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                Forwarded = getForwardedInfo(Request),
                Authorization = _smartAuthManager.GetAuthorization(storeName, authHeader ?? string.Empty),
                DestinationFormat = getMimeType(format, Request),
                SerializePretty = pretty?.Equals("true", StringComparison.Ordinal) ?? false,
                Interaction = Common.StoreInteractionCodes.InstanceUpdate,
                ResourceType = resourceName,
                Id = id,
                IfMatch = ifMatch ?? string.Empty,
                IfNoneMatch = ifNoneMatch ?? string.Empty,
                SourceFormat = Request.ContentType ?? string.Empty,
                SourceContent = content,
            };

            if (!_smartAuthManager.IsAuthorized(ctx))
            {
                Response.StatusCode = 401;
                return;
            }

            bool success = store.InstanceUpdate(
                ctx,
                out FhirResponseContext opResponse);

            await AddFhirResponse(Response, prefer, success, opResponse);
        }
        catch (Exception ex)
        {
            string msg = ex.InnerException == null ? $"PutResourceInstance <<< caught: {ex.Message}" : $"PutResourceInstance <<< caught: {ex.Message}, inner: {ex.InnerException.Message}";
            await LogAndReturnError(Response, 500, msg);
            return;
        }
    }

    /// <summary>
    /// (An Action that handles HTTP DELETE requests) deletes the resource instance.
    /// </summary>
    /// <param name="storeName">   The store.</param>
    /// <param name="resourceName">Name of the resource.</param>
    /// <param name="id">          The identifier.</param>
    /// <param name="format">      Describes the format to use.</param>
    /// <param name="pretty">      The pretty.</param>
    /// <param name="prefer">      The prefer.</param>
    /// <param name="ifMatch">     A match specifying if.</param>
    /// <param name="authHeader">  The authentication header.</param>
    /// <returns>An asynchronous result.</returns>
    [HttpDelete, Route("{storeName}/{resourceName}/{id}")]
    public async Task DeleteResourceInstance(
        [FromRoute] string storeName,
        [FromRoute] string resourceName,
        [FromRoute] string id,
        [FromQuery(Name = "_format")] string? format,
        [FromQuery(Name = "_pretty")] string? pretty,
        [FromHeader(Name = "Prefer")] string? prefer,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        [FromHeader(Name = "Authorization")] string? authHeader)
    {
        if (!_fhirStoreManager.TryGetValue(storeName, out IFhirStore? store))
        {
            await LogAndReturnError(Response, 404, $"DeleteResourceInstance <<< no tenant at {storeName}!");
            return;
        }

        if (!store.SupportsResource(resourceName))
        {
            await LogAndReturnError(Response, 404, $"DeleteResourceInstance <<< tenant {storeName} does not support resource {resourceName}!");
            return;
        }

        FhirRequestContext ctx = new()
        {
            TenantName = storeName,
            Store = store,
            HttpMethod = Request.Method.ToUpperInvariant(),
            Url = Request.GetDisplayUrl(),
            UrlPath = Request.Path,
            UrlQuery = Request.QueryString.ToString(),
            RequestHeaders = Request.Headers.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            Forwarded = getForwardedInfo(Request),
            Authorization = _smartAuthManager.GetAuthorization(storeName, authHeader ?? string.Empty),
            DestinationFormat = getMimeType(format, Request),
            SerializePretty = pretty?.Equals("true", StringComparison.Ordinal) ?? false,
            Interaction = Common.StoreInteractionCodes.InstanceDelete,
            ResourceType = resourceName,
            Id = id,
            IfMatch = ifMatch ?? string.Empty,
        };

        if (!_smartAuthManager.IsAuthorized(ctx))
        {
            Response.StatusCode = 401;
            return;
        }

        bool success = store.InstanceDelete(
            ctx,
            out FhirResponseContext opResponse);

        await AddFhirResponse(Response, prefer, success, opResponse);
    }

    /// <summary>(An Action that handles HTTP GET requests) gets resource type search.</summary>
    /// <param name="storeName">   The store.</param>
    /// <param name="resourceName">Name of the resource.</param>
    /// <param name="format">      Describes the format to use.</param>
    /// <param name="pretty">      The pretty.</param>
    /// <param name="summary">     The summary.</param>
    /// <param name="authHeader">  The authentication header.</param>
    /// <returns>An asynchronous result.</returns>
    [HttpGet, Route("{storeName}/{resourceName}")]
    public async Task GetResourceTypeSearch(
        [FromRoute] string storeName,
        [FromRoute] string resourceName,
        [FromQuery(Name = "_format")] string? format,
        [FromQuery(Name = "_pretty")] string? pretty,
        [FromQuery(Name = "_summary")] string? summary,
        [FromHeader(Name = "Authorization")] string? authHeader)
    {
        if (!_fhirStoreManager.TryGetValue(storeName, out IFhirStore? store))
        {
            await LogAndReturnError(Response, 404, $"GetResourceTypeSearch <<< no tenant at {storeName}!");
            return;
        }

        if (!store.SupportsResource(resourceName))
        {
            await LogAndReturnError(Response, 404, $"GetResourceTypeSearch <<< tenant {storeName} does not support resource {resourceName}!");
            return;
        }

        FhirRequestContext ctx = new()
        {
            TenantName = storeName,
            Store = store,
            HttpMethod = Request.Method.ToUpperInvariant(),
            Url = Request.GetDisplayUrl(),
            UrlPath = Request.Path,
            UrlQuery = Request.QueryString.ToString(),
            RequestHeaders = Request.Headers.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            Forwarded = getForwardedInfo(Request),
            Authorization = _smartAuthManager.GetAuthorization(storeName, authHeader ?? string.Empty),
            DestinationFormat = getMimeType(format, Request),
            SerializePretty = pretty?.Equals("true", StringComparison.Ordinal) ?? false,
            Interaction = Common.StoreInteractionCodes.TypeSearch,
            ResourceType = resourceName,
        };

        if (!_smartAuthManager.IsAuthorized(ctx))
        {
            Response.StatusCode = 401;
            return;
        }

        bool success = store.TypeSearch(
            ctx,
            out FhirResponseContext opResponse);

        // cannot prefer outcome on search
        await AddFhirResponse(Response, null, success, opResponse);
    }

    /// <summary>
    /// (An Action that handles HTTP DELETE requests) deletes a resource based on type search.
    /// </summary>
    /// <param name="storeName">   The store.</param>
    /// <param name="resourceName">Name of the resource.</param>
    /// <param name="format">      Describes the format to use.</param>
    /// <param name="pretty">      The pretty.</param>
    /// <param name="summary">     The summary.</param>
    /// <param name="prefer">      The prefer.</param>
    /// <param name="authHeader">  The authentication header.</param>
    /// <returns>An asynchronous result.</returns>
    [HttpDelete, Route("{storeName}/{resourceName}")]
    public async Task DeleteResourceConditional(
        [FromRoute] string storeName,
        [FromRoute] string resourceName,
        [FromQuery(Name = "_format")] string? format,
        [FromQuery(Name = "_pretty")] string? pretty,
        [FromQuery(Name = "_summary")] string? summary,
        [FromHeader(Name = "Prefer")] string? prefer,
        [FromHeader(Name = "Authorization")] string? authHeader)
    {
        if (!_fhirStoreManager.TryGetValue(storeName, out IFhirStore? store))
        {
            await LogAndReturnError(Response, 404, $"DeleteResourceConditional <<< no tenant at {storeName}!");
            return;
        }

        if (!store.SupportsResource(resourceName))
        {
            await LogAndReturnError(Response, 404, $"DeleteResourceConditional <<< tenant {storeName} does not support resource {resourceName}!");
            return;
        }

        FhirRequestContext ctx = new()
        {
            TenantName = storeName,
            Store = store,
            HttpMethod = Request.Method.ToUpperInvariant(),
            Url = Request.GetDisplayUrl(),
            UrlPath = Request.Path,
            UrlQuery = Request.QueryString.ToString(),
            RequestHeaders = Request.Headers.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            Forwarded = getForwardedInfo(Request),
            Authorization = _smartAuthManager.GetAuthorization(storeName, authHeader ?? string.Empty),
            DestinationFormat = getMimeType(format, Request),
            SerializePretty = pretty?.Equals("true", StringComparison.Ordinal) ?? false,
            SerializeSummaryFlag = summary ?? string.Empty,
            Interaction = Common.StoreInteractionCodes.TypeDeleteConditional,
            ResourceType = resourceName,
        };

        if (!_smartAuthManager.IsAuthorized(ctx))
        {
            Response.StatusCode = 401;
            return;
        }

        try
        {
            bool success = store.TypeDelete(
                ctx,
                out FhirResponseContext opResponse);

            await AddFhirResponse(Response, prefer, success, opResponse);
        }
        catch (Exception ex)
        {
            string msg = ex.InnerException == null ? $"DeleteResourceConditional <<< caught: {ex.Message}" : $"DeleteResourceConditional <<< caught: {ex.Message}, inner: {ex.InnerException.Message}";
            await LogAndReturnError(Response, 500, msg);
            return;
        }
    }

    /// <summary>(An Action that handles HTTP POST requests) posts to the server root.</summary>
    /// <param name="storeName"> The store.</param>
    /// <param name="format">    Describes the format to use.</param>
    /// <param name="pretty">    The pretty.</param>
    /// <param name="prefer">    The prefer.</param>
    /// <param name="authHeader">The authentication header.</param>
    /// <returns>An asynchronous result.</returns>
    [HttpPost, Route("{storeName}")]
    [Consumes("application/fhir+json", new[] { "application/fhir+xml", "application/json", "application/xml" })]
    public async Task PostSystemBundle(
        [FromRoute] string storeName,
        [FromQuery(Name = "_format")] string? format,
        [FromQuery(Name = "_pretty")] string? pretty,
        [FromHeader(Name = "Prefer")] string? prefer,
        [FromHeader(Name = "Authorization")] string? authHeader)
    {
        if (!_fhirStoreManager.TryGetValue(storeName, out IFhirStore? store))
        {
            await LogAndReturnError(Response, 404, $"PostSystemBundle <<< no tenant at {storeName}!");
            return;
        }
        
        try
        {
            // read the post body to process
            using StreamReader reader = new StreamReader(Request.Body);
            string content = await reader.ReadToEndAsync();

            FhirRequestContext ctx = new()
            {
                TenantName = storeName,
                Store = store,
                HttpMethod = Request.Method.ToUpperInvariant(),
                Url = Request.GetDisplayUrl(),
                UrlPath = Request.Path,
                UrlQuery = Request.QueryString.ToString(),
                RequestHeaders = Request.Headers.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                Forwarded = getForwardedInfo(Request),
                Authorization = _smartAuthManager.GetAuthorization(storeName, authHeader ?? string.Empty),
                DestinationFormat = getMimeType(format, Request),
                SerializePretty = pretty?.Equals("true", StringComparison.Ordinal) ?? false,
                Interaction = Common.StoreInteractionCodes.SystemBundle,
                SourceFormat = Request.ContentType ?? string.Empty,
                SourceContent = content,
            };

            if (!_smartAuthManager.IsAuthorized(ctx))
            {
                Response.StatusCode = 401;
                return;
            }

            bool success = store.ProcessBundle(
                ctx,
                out FhirResponseContext opResponse);

            await AddFhirResponse(Response, prefer, success, opResponse);
        }
        catch (Exception ex)
        {
            string msg = ex.InnerException == null ? $"PostSystemBundle <<< caught: {ex.Message}" : $"PostSystemBundle <<< caught: {ex.Message}, inner: {ex.InnerException.Message}";
            await LogAndReturnError(Response, 500, msg);
            return;
        }
    }

    /// <summary>(An Action that handles HTTP GET requests) gets system search.</summary>
    /// <param name="storeName"> The store.</param>
    /// <param name="format">    Describes the format to use.</param>
    /// <param name="pretty">    The pretty.</param>
    /// <param name="summary">   The summary.</param>
    /// <param name="authHeader">The authentication header.</param>
    /// <returns>An asynchronous result.</returns>
    [HttpGet, Route("{storeName}")]
    public async Task GetSystemSearch(
        [FromRoute] string storeName,
        [FromQuery(Name = "_format")] string? format,
        [FromQuery(Name = "_pretty")] string? pretty,
        [FromQuery(Name = "_summary")] string? summary,
        [FromHeader(Name = "Authorization")] string? authHeader)
    {
        if (!_fhirStoreManager.TryGetValue(storeName, out IFhirStore? store))
        {
            await LogAndReturnError(Response, 404, $"GetSystemSearch <<< no tenant at {storeName}!");
            return;
        }

        FhirRequestContext ctx = new()
        {
            TenantName = storeName,
            Store = store,
            HttpMethod = Request.Method.ToUpperInvariant(),
            Url = Request.GetDisplayUrl(),
            UrlPath = Request.Path,
            UrlQuery = Request.QueryString.ToString(),
            RequestHeaders = Request.Headers.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            Forwarded = getForwardedInfo(Request),
            Authorization = _smartAuthManager.GetAuthorization(storeName, authHeader ?? string.Empty),
            DestinationFormat = getMimeType(format, Request),
            SerializePretty = pretty?.Equals("true", StringComparison.Ordinal) ?? false,
            SerializeSummaryFlag = summary ?? string.Empty,
            Interaction = Common.StoreInteractionCodes.SystemSearch,
        };

        if (!_smartAuthManager.IsAuthorized(ctx))
        {
            Response.StatusCode = 401;
            return;
        }

        bool success = store.SystemSearch(
            ctx,
            out FhirResponseContext opResponse);

        // cannot prefer outcome on search
        await AddFhirResponse(Response, null, success, opResponse);
    }

    /// <summary>
    /// (An Action that handles HTTP DELETE requests) deletes the system conditional.
    /// </summary>
    /// <param name="storeName"> The store.</param>
    /// <param name="format">    Describes the format to use.</param>
    /// <param name="pretty">    The pretty.</param>
    /// <param name="summary">   The summary.</param>
    /// <param name="prefer">    The prefer.</param>
    /// <param name="authHeader">The authentication header.</param>
    /// <returns>An asynchronous result.</returns>
    [HttpDelete, Route("{storeName}")]
    public async Task DeleteSystemConditional(
        [FromRoute] string storeName,
        [FromQuery(Name = "_format")] string? format,
        [FromQuery(Name = "_pretty")] string? pretty,
        [FromQuery(Name = "_summary")] string? summary,
        [FromHeader(Name = "Prefer")] string? prefer,
        [FromHeader(Name = "Authorization")] string? authHeader)
    {
        if (!_fhirStoreManager.TryGetValue(storeName, out IFhirStore? store))
        {
            await LogAndReturnError(Response, 404, $"DeleteSystemConditional <<< no tenant at {storeName}!");
            return;
        }

        FhirRequestContext ctx = new()
        {
            TenantName = storeName,
            Store = store,
            HttpMethod = Request.Method.ToUpperInvariant(),
            Url = Request.GetDisplayUrl(),
            UrlPath = Request.Path,
            UrlQuery = Request.QueryString.ToString(),
            RequestHeaders = Request.Headers.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            Forwarded = getForwardedInfo(Request),
            Authorization = _smartAuthManager.GetAuthorization(storeName, authHeader ?? string.Empty),
            DestinationFormat = getMimeType(format, Request),
            SerializePretty = pretty?.Equals("true", StringComparison.Ordinal) ?? false,
            SerializeSummaryFlag = summary ?? string.Empty,
            Interaction = Common.StoreInteractionCodes.SystemDeleteConditional,
        };

        if (!_smartAuthManager.IsAuthorized(ctx))
        {
            Response.StatusCode = 401;
            return;
        }

        try
        {
            bool success = store.SystemDelete(
                ctx,
                out FhirResponseContext opResponse);

            await AddFhirResponse(Response, prefer, success, opResponse);
        }
        catch (Exception ex)
        {
            string msg = ex.InnerException == null ? $"DeleteSystemConditional <<< caught: {ex.Message}" : $"DeleteSystemConditional <<< caught: {ex.Message}, inner: {ex.InnerException.Message}";
            await LogAndReturnError(Response, 500, msg);
            return;
        }
    }
}
