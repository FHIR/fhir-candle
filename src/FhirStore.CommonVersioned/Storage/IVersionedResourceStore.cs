// <copyright file="IVersionedResourceStore.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using FhirCandle.Interactions;
using FhirCandle.Models;
using FhirCandle.Storage;
using Hl7.Fhir.Model;
using Hl7.FhirPath;
using System.Diagnostics.CodeAnalysis;
using System.Net;

namespace FhirCandle.Storage;

/// <summary>Interface for resource store.</summary>
/// <typeparam name="T">Resource type parameter.</typeparam>
public interface IVersionedResourceStore : IResourceStore, IDisposable, IReadOnlyDictionary<string, Hl7.Fhir.Model.Resource>
{
    /// <summary>Reads a specific instance of a resource.</summary>
    /// <param name="id">[out] The identifier.</param>
    /// <returns>The requested resource or null.</returns>
    Hl7.Fhir.Model.Resource? InstanceRead(string id);

    /// <summary>Gets by canonical.</summary>
    /// <param name="url">URL of the resource.</param>
    /// <returns>The by canonical.</returns>
    Hl7.Fhir.Model.Resource? GetByCanonical(string url);

    /// <summary>
    /// Attempts to get by canonical a Hl7.Fhir.Model.Resource from the given string.
    /// </summary>
    /// <param name="url">     URL of the resource.</param>
    /// <param name="resource">[out] The resource.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    bool TryGetByCanonical(string url, out Hl7.Fhir.Model.Resource? resource);


    /// <summary>Create an instance of a resource.</summary>
    /// <param name="ctx">            The context.</param>
    /// <param name="source">         [out] The resource.</param>
    /// <param name="allowExistingId">True to allow, false to suppress the existing identifier.</param>
    /// <returns>The created resource, or null if it could not be created.</returns>
    Hl7.Fhir.Model.Resource? InstanceCreate(
        FhirRequestContext ctx,
        Hl7.Fhir.Model.Resource source,
        bool allowExistingId);

    /// <summary>Update a specific instance of a resource.</summary>
    /// <param name="source">            [out] The resource.</param>
    /// <param name="allowCreate">       True to allow, false to suppress the create.</param>
    /// <param name="ifMatch">           A match specifying if.</param>
    /// <param name="ifNoneMatch">       A match specifying if none.</param>
    /// <param name="protectedResources">The protected resources.</param>
    /// <param name="sc">                [out] The screen.</param>
    /// <param name="outcome">           [out] The outcome.</param>
    /// <returns>The updated resource, or null if it could not be performed.</returns>
    Hl7.Fhir.Model.Resource? InstanceUpdate(
        Hl7.Fhir.Model.Resource source,
        bool allowCreate,
        string ifMatch,
        string ifNoneMatch,
        HashSet<string> protectedResources,
        out HttpStatusCode sc,
        out OperationOutcome outcome);

    /// <summary>Instance delete.</summary>
    /// <param name="id">                [out] The identifier.</param>
    /// <param name="protectedResources">The protected resources.</param>
    /// <returns>The deleted resource or null.</returns>
    Hl7.Fhir.Model.Resource? InstanceDelete(
        string id,
        HashSet<string> protectedResources);

    /// <summary>
    /// Performs a type search in this resource store.
    /// </summary>
    /// <param name="parameters">The search parameters.</param>
    /// <param name="isNestedSearch">If this search has been triggered from inside another search request (for locking)</param>
    /// <returns>
    /// An enumerator that allows foreach to be used to process the search results in this collection.
    /// </returns>
    IEnumerable<Hl7.Fhir.Model.Resource> TypeSearch(IEnumerable<ParsedSearchParameter> parameters, bool isNestedSearch = false);

    /// <summary>
    /// Tests for the presence of any resources matching the given search parameter.
    /// </summary>
    /// <param name="link"></param>
    /// <param name="filter"></param>
    /// <returns>True if any resources match all requested search parameters, false otherwise.</returns>
    bool TestForAny(ParsedSearchParameter link, ParsedSearchParameter filter);

    /// <summary>Adds a search parameter definition.</summary>
    /// <param name="spDefinition">The sp definition.</param>
    void SetExecutableSearchParameter(Hl7.Fhir.Model.ModelInfo.SearchParamDefinition spDefinition);

    /// <summary>Removes the executable search parameter described by name.</summary>
    /// <param name="name">The name.</param>
    void RemoveExecutableSearchParameter(string name);

    /// <summary>
    /// Attempts to get search parameter definition a ModelInfo.SearchParamDefinition from the given
    /// string.
    /// </summary>
    /// <param name="name">        The name.</param>
    /// <param name="spDefinition">[out] The sp definition.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    bool TryGetSearchParamDefinition(
        string name,
        [NotNullWhen(true)] out Hl7.Fhir.Model.ModelInfo.SearchParamDefinition? spDefinition);

    /// <summary>Gets the search parameter definitions known by this store</summary>
    /// <returns>
    /// An enumerator that allows foreach to be used to process the search parameter definitions in
    /// this collection.
    /// </returns>
    IEnumerable<Hl7.Fhir.Model.ModelInfo.SearchParamDefinition> GetSearchParamDefinitions();

    /// <summary>Gets the search includes supported by this store.</summary>
    /// <returns>
    /// An enumerator that allows foreach to be used to process the search includes in this
    /// collection.
    /// </returns>
    IEnumerable<string> GetSearchIncludes();

    /// <summary>Gets the search reverse includes supported by this store.</summary>
    /// <returns>
    /// An enumerator that allows foreach to be used to process the search reverse includes in this
    /// collection.
    /// </returns>
    IEnumerable<string> GetSearchRevIncludes();

    /// <summary>Query if this type contains a resource with the specified identifier.</summary>
    /// <param name="system">The system.</param>
    /// <param name="value"> The value.</param>
    /// <param name="r">     [out] The resolved resource process.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    bool TryResolveIdentifier(string system, string value, out Hl7.Fhir.Model.Resource? r);

    /// <summary>Sets executable subscription information.</summary>
    /// <param name="url">             URL of the resource.</param>
    void SetExecutableSubscriptionTopic(
        string url,
        IEnumerable<ExecutableSubscriptionInfo.InteractionOnlyTrigger> interactionTriggers,
        IEnumerable<ExecutableSubscriptionInfo.CompiledFhirPathTrigger> fhirpathTriggers,
        IEnumerable<ExecutableSubscriptionInfo.CompiledQueryTrigger> queryTriggers,
        ParsedResultParameters? resultParameters);

    /// <summary>Removes the executable subscription information described by topicUrl.</summary>
    /// <param name="topicUrl">URL of the topic.</param>
    void RemoveExecutableSubscriptionTopic(string topicUrl);

    /// <summary>Sets executable subscription.</summary>
    /// <param name="topicUrl">URL of the topic.</param>
    /// <param name="id">      The subscription id.</param>
    /// <param name="filters"> The filters.</param>
    void SetExecutableSubscription(string topicUrl, string id, List<ParsedSearchParameter> filters);

    /// <summary>Removes the executable subscription.</summary>
    /// <param name="topicUrl">URL of the topic.</param>
    /// <param name="id">      The subscription id.</param>
    void RemoveExecutableSubscription(string topicUrl, string id);
}
