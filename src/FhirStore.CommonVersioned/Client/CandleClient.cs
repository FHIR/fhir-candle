// <copyright file="CandleClient.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using FhirCandle.Models;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;

namespace FhirCandle.Client;

/// <summary>A candle client.</summary>
public class CandleClient
{
    /// <summary>The client.</summary>
    private BaseFhirClient _client = null!;

    /// <summary>Gets or initializes URL of the FHIR server.</summary>
    public required string FhirServerUrl { get; init; }

    /// <summary>Gets or initializes options for controlling this client.</summary>
    public CandleClientSettings Settings { get; init; } = new();

    /// <summary>Creates an underlying FHIR client if needed.</summary>
    private void CreateClientIfNeeded()
    {
        if (_client != null)
        {
            return;
        }

        FhirClientSettings fhirClientSettings = new()
        {
            VerifyFhirVersion = Settings.VerifyFhirVersion,
            ExplicitFhirVersion = Settings.ExplicitFhirVersion,
            PreferredFormat = Settings.PreferredFormat switch
            {
                CandleClientSettings.ResourceFormatCodes.Xml => ResourceFormat.Xml,
                CandleClientSettings.ResourceFormatCodes.Json => ResourceFormat.Json,
                _ => ResourceFormat.Unknown,
            },
            UseFormatParameter = Settings.UseFormatParameter,
            UseFhirVersionInAcceptHeader = Settings.UseFhirVersionInAcceptHeader,
            Timeout = Settings.Timeout,
            ReturnPreference = Settings.ReturnPreference switch
            {
                CandleClientSettings.ReturnPreferenceCodes.Representation => ReturnPreference.Representation,
                CandleClientSettings.ReturnPreferenceCodes.OperationOutcome => ReturnPreference.OperationOutcome,
                _ => ReturnPreference.Minimal,
            },
            UseAsync = Settings.UseAsync,
            PreferredParameterHandling = Settings.PreferredParameterHandling switch
            {
                CandleClientSettings.SearchParameterHandling.Strict => SearchParameterHandling.Strict,
                _ => SearchParameterHandling.Lenient,
            },
            PreferCompressedResponses = Settings.PreferCompressedResponses,
            RequestBodyCompressionMethod = Settings.RequestBodyCompressionMethod,
        };

        _client = new FhirClient(FhirServerUrl, fhirClientSettings).WithLenientSerializer();
    }

    /// <summary>Create a resource on a FHIR endpoint.</summary>
    /// <typeparam name="TResource">The type of resource to create.</typeparam>
    /// <param name="resource">The resource instance to create.</param>
    /// <param name="ct">      (Optional)</param>
    /// <returns>
    /// The resource as created on the server, or an exception if the create failed.
    /// </returns>
    public virtual Task<TResource?> CreateAsync<TResource>(
        TResource resource,
        CancellationToken? ct = null) 
            where TResource : Resource
    {
        CreateClientIfNeeded();
        return _client.CreateAsync(resource, ct);
    }

    /// <summary>Conditionally Create a resource on a FHIR endpoint.</summary>
    /// <typeparam name="TResource">The type of resource to create.</typeparam>
    /// <param name="resource"> The resource instance to create.</param>
    /// <param name="condition">The criteria.</param>
    /// <param name="ct">       (Optional)</param>
    /// <returns>
    /// The resource as created on the server, or an exception if the create failed.
    /// </returns>
    public virtual Task<TResource?> ConditionalCreateAsync<TResource>(
        TResource resource, 
        SearchParams condition, 
        CancellationToken? ct = null) 
            where TResource : Resource
    {
        CreateClientIfNeeded();
        return _client.ConditionalCreateAsync(resource, condition, ct);
    }

    /// <summary>Fetches a typed resource from a FHIR resource endpoint.</summary>
    /// <remarks>
    /// Since ResourceLocation is a subclass of Uri, you may pass in ResourceLocations too.
    /// </remarks>
    /// <typeparam name="TResource">The type of resource to read. Resource or DomainResource is
    ///  allowed if exact type is unknown.</typeparam>
    /// <param name="location">       The url of the Resource to fetch. This can be a Resource id url
    ///  or a version-specific Resource url.</param>
    /// <param name="ifNoneMatch">    (Optional) The (weak) ETag to use in a conditional read. Optional.</param>
    /// <param name="ifModifiedSince">(Optional) Last modified since date in a conditional read.
    ///  Optional. (refer to spec 2.1.0.5) If this is used, the client will throw an exception you
    ///  need.</param>
    /// <param name="ct">             (Optional)</param>
    /// <returns>
    /// The requested resource. This operation will throw an exception if the resource has been
    /// deleted or does not exist. The specified may be relative or absolute, if it is an absolute
    /// url, it must reference an address within the endpoint.
    /// </returns>
    public virtual Task<TResource?> ReadAsync<TResource>(
        Uri location, 
        string? ifNoneMatch = null, 
        DateTimeOffset? ifModifiedSince = null, 
        CancellationToken? ct = null) 
            where TResource : Resource
    {
        CreateClientIfNeeded();
        return _client.ReadAsync<TResource>(location, ifNoneMatch, ifModifiedSince, ct);
    }

    /// <summary>Fetches a typed resource from a FHIR resource endpoint.</summary>
    /// <remarks>
    /// Since ResourceLocation is a subclass of Uri, you may pass in ResourceLocations too.
    /// </remarks>
    /// <typeparam name="TResource">The type of resource to read. Resource or DomainResource is
    ///  allowed if exact type is unknown.</typeparam>
    /// <param name="location">       The url of the Resource to fetch. This can be a Resource id url
    ///  or a version-specific Resource url.</param>
    /// <param name="ifNoneMatch">    (Optional) The (weak) ETag to use in a conditional read. Optional.</param>
    /// <param name="ifModifiedSince">(Optional) Last modified since date in a conditional read.
    ///  Optional. (refer to spec 2.1.0.5) If this is used, the client will throw an exception you
    ///  need.</param>
    /// <param name="ct">             (Optional)</param>
    /// <returns>
    /// The requested resource. This operation will throw an exception if the resource has been
    /// deleted or does not exist. The specified may be relative or absolute, if it is an absolute
    /// url, it must reference an address within the endpoint.
    /// </returns>
    public virtual Task<TResource?> ReadAsync<TResource>(
        string location, 
        string? ifNoneMatch = null, 
        DateTimeOffset? ifModifiedSince = null, 
        CancellationToken? ct = null) 
            where TResource : Resource
    {
        CreateClientIfNeeded();
        return _client.ReadAsync<TResource>(location, ifNoneMatch, ifModifiedSince, ct);
    }

    /// <summary>Update (or create) a resource.</summary>
    /// <remarks>
    /// Throws an exception when the update failed, in particular when an update conflict is detected
    /// and the server returns a HTTP 409. If the resource does not yet exist - and the server allows
    /// client-assigned id's - a new resource with the given id will be created.
    /// </remarks>
    /// <typeparam name="TResource">The type of resource that is being updated.</typeparam>
    /// <param name="resource">    The resource to update.</param>
    /// <param name="versionAware">(Optional) If true, asks the server to verify we are updating the
    ///  latest version.</param>
    /// <param name="ct">          (Optional)</param>
    /// <returns>
    /// The body of the updated resource, unless ReturnFullResource is set to "false".
    /// </returns>
    public virtual Task<TResource?> UpdateAsync<TResource>(
        TResource resource, 
        bool versionAware = false, 
        CancellationToken? ct = null) 
            where TResource : Resource
    {
        CreateClientIfNeeded();
        return _client.UpdateAsync(resource, versionAware, ct);
    }

    /// <summary>Conditionally update (or create) a resource.</summary>
    /// <remarks>
    /// Throws an exception when the update failed, in particular when an update conflict is detected
    /// and the server returns a HTTP 409. If the criteria passed in condition do not match a
    /// resource a new resource with a server assigned id will be created.
    /// </remarks>
    /// <typeparam name="TResource">The type of resource that is being updated.</typeparam>
    /// <param name="resource">    The resource to update.</param>
    /// <param name="condition">   Criteria used to locate the resource to update.</param>
    /// <param name="versionAware">(Optional) If true, asks the server to verify we are updating the
    ///  latest version.</param>
    /// <param name="ct">          (Optional)</param>
    /// <returns>
    /// The body of the updated resource, unless ReturnFullResource is set to "false".
    /// </returns>
    public virtual Task<TResource?> ConditionalUpdateAsync<TResource>(
        TResource resource, 
        SearchParams condition, 
        bool versionAware = false, 
        CancellationToken? ct = null) 
            where TResource : Resource
    {
        CreateClientIfNeeded();
        return _client.ConditionalUpdateAsync(resource, condition, versionAware, ct);
    }

    /// <summary>Delete a resource at the given endpoint.</summary>
    /// <param name="location">endpoint of the resource to delete.</param>
    /// <param name="ct">      (Optional)</param>
    /// <returns>
    /// Throws an exception when the delete failed, though this might just mean the server returned
    /// 404 (the resource didn't exist before) or 410 (the resource was already deleted).
    /// </returns>
    public virtual async System.Threading.Tasks.Task DeleteAsync(string location, CancellationToken? ct = null)
    {
        CreateClientIfNeeded();
        await _client.DeleteAsync(location, ct);
    }

    /// <summary>Delete a resource at the given endpoint.</summary>
    /// <param name="location">endpoint of the resource to delete.</param>
    /// <param name="ct">      (Optional)</param>
    /// <returns>
    /// Throws an exception when the delete failed, though this might just mean the server returned
    /// 404 (the resource didn't exist before) or 410 (the resource was already deleted).
    /// </returns>
    public virtual async System.Threading.Tasks.Task DeleteAsync(Uri location, CancellationToken? ct = null)
    {
        CreateClientIfNeeded();
        await _client.DeleteAsync(location, ct);
    }

    /// <summary>Delete a resource.</summary>
    /// <param name="resource">The resource to delete.</param>
    /// <param name="ct">      (Optional)</param>
    /// <returns>A System.Threading.Tasks.Task.</returns>
    public virtual async System.Threading.Tasks.Task DeleteAsync(Resource resource, CancellationToken? ct = null)
    {
        CreateClientIfNeeded();
        await _client.DeleteAsync(resource, ct);
    }

    /// <summary>Conditionally delete a single resource.</summary>
    /// <param name="resourceType">The type of resource to delete.</param>
    /// <param name="condition">   Criteria to use to match the resource to delete.</param>
    /// <param name="ct">          (Optional)</param>
    /// <returns>A System.Threading.Tasks.Task.</returns>
    public virtual async System.Threading.Tasks.Task ConditionalDeleteSingleAsync(string resourceType, SearchParams condition, CancellationToken? ct = null)
    {
        CreateClientIfNeeded();
        await _client.ConditionalDeleteSingleAsync(condition, resourceType, ct: ct);
    }

    /// <summary>Conditionally delete one or more resources.</summary>
    /// <param name="resourceType">The type of resource to delete.</param>
    /// <param name="condition">   Criteria to use to match the resource to delete.</param>
    /// <param name="ct">          (Optional)</param>
    /// <returns>A System.Threading.Tasks.Task.</returns>
    public virtual async System.Threading.Tasks.Task ConditionalDeleteMultipleAsync(string resourceType, SearchParams condition, CancellationToken? ct = null)
    {
        CreateClientIfNeeded();
        await _client.ConditionalDeleteMultipleAsync(condition, resourceType, ct);
    }


    /// <summary>Patch a resource on a FHIR Endpoint.</summary>
    /// <param name="location">       Location of the resource.</param>
    /// <param name="patchParameters">A Parameters resource that includes the patch operation(s) to
    ///  perform.</param>
    /// <param name="ct">             (Optional)</param>
    /// <returns>The patched resource.</returns>
    public virtual Task<Resource?> PatchAsync(Uri location, Parameters patchParameters, CancellationToken? ct = null)
    {
        CreateClientIfNeeded();
        return _client.PatchAsync(location, patchParameters, ct);
    }

    /// <summary>Patch a resource on a FHIR Endpoint.</summary>
    /// <typeparam name="TResource">Type of resource to patch.</typeparam>
    /// <param name="id">             Id of the resource to patch.</param>
    /// <param name="patchParameters">A Parameters resource that includes the patch operation(s) to
    ///  perform.</param>
    /// <param name="versionId">      (Optional) version id of the resource to patch.</param>
    /// <param name="ct">             (Optional)</param>
    /// <returns>The patched resource.</returns>
    public virtual Task<TResource?> PatchAsync<TResource>(
        string id, 
        Parameters patchParameters, 
        string? versionId = null, 
        CancellationToken? ct = null) 
            where TResource : Resource
    {
        CreateClientIfNeeded();
        return _client.PatchAsync<TResource>(id, patchParameters, versionId, ct);
    }

    /// <summary>Conditionally patch a resource on a FHIR Endpoint.</summary>
    /// <typeparam name="TResource">Type of resource to patch.</typeparam>
    /// <param name="condition">      Criteria used to locate the resource to update.</param>
    /// <param name="patchParameters">A Parameters resource that includes the patch operation(s) to
    ///  perform.</param>
    /// <param name="ct">             (Optional)</param>
    /// <returns>The patched resource.</returns>
    public Task<TResource?> ConditionalPatchAsync<TResource>(
        SearchParams condition, 
        Parameters patchParameters, 
        CancellationToken? ct = null) 
            where TResource : Resource
    {
        CreateClientIfNeeded();
        return _client.ConditionalPatchAsync<TResource>(condition, patchParameters, ct);
    }

    /// <summary>Retrieve the version history for a specific resource type.</summary>
    /// <param name="resourceType">The type of Resource to get the history for.</param>
    /// <param name="since">       (Optional) Optional. Returns only changes after the given date.</param>
    /// <param name="pageSize">    (Optional) Optional. Asks server to limit the number of entries
    ///  per page returned.</param>
    /// <param name="summary">     (Optional) Optional. Asks the server to only provide the fields
    ///  defined for the summary.</param>
    /// <param name="ct">          (Optional)</param>
    /// <returns>
    /// A bundle with the history for the indicated instance, may contain both ResourceEntries and
    /// DeletedEntries.
    /// </returns>
    public Task<Bundle?> TypeHistoryAsync(
        string resourceType, 
        DateTimeOffset? since = null, 
        int? pageSize = null, 
        SummaryType? summary = null, 
        CancellationToken? ct = null)
    {
        CreateClientIfNeeded();
        return _client.TypeHistoryAsync(resourceType, since, pageSize, summary, ct);
    }
}
