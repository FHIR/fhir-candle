// <copyright file="ResourceStore.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using FhirCandle.Search;
using FhirCandle.Extensions;
using FhirCandle.Models;
using FhirCandle.Subscriptions;
using Hl7.Fhir.ElementModel;
using Hl7.Fhir.FhirPath;
using Hl7.Fhir.Model;
using Hl7.FhirPath;
using System.Collections.Concurrent;
using System.Collections;
using System.Net;
using FhirCandle.Serialization;
using System.Diagnostics.CodeAnalysis;
using Hl7.Fhir.Utility;

namespace FhirCandle.Storage;

/// <summary>A resource store.</summary>
/// <typeparam name="T">Resource type parameter.</typeparam>
public class ResourceStore<T> : IVersionedResourceStore
    where T : Resource
{
    /// <summary>The store.</summary>
    private readonly VersionedFhirStore _store;

    /// <summary>Name of the resource.</summary>
    private string _resourceName = typeof(T).Name;

    /// <summary>True if this resource type implements IConformanceResource.</summary>
    private bool _tIsConformanceResource;

    /// <summary>True if this resource type has an 'identifier' element, false if not.</summary>
    private bool _tIsIdentifiable;

    /// <summary>True if this resource type has a 'name' element, false if not.</summary>
    private bool _tHasName;

    /// <summary>True if has disposed, false if not.</summary>
    private bool _hasDisposed = false;

    /// <summary>The resource store.</summary>
    private readonly ConcurrentDictionary<string, T> _resourceStore = new();

    /// <summary>(Immutable) Conformance URL to ID Map.</summary>
    internal readonly ConcurrentDictionary<string, string> _conformanceUrlToId = new();

    /// <summary>(Immutable) Identifier for the identifier to.</summary>
    internal readonly ConcurrentDictionary<string, string> _identifierToId = new();

    /// <summary>The lock object.</summary>
    private object _lockObject = new();

    /// <summary>Occurs when On Instance Created.</summary>
    public event EventHandler<StoreInstanceEventArgs>? OnInstanceCreated;

    /// <summary>Occurs when On Instance Updated.</summary>
    public event EventHandler<StoreInstanceEventArgs>? OnInstanceUpdated;

    /// <summary>Occurs when On Instance Deleted.</summary>
    public event EventHandler<StoreInstanceEventArgs>? OnInstanceDeleted;

    /// <summary>The search tester.</summary>
    public required SearchTester _searchTester;

    /// <summary>The topic converter.</summary>
    public required TopicConverter _topicConverter;

    /// <summary>The subscription converter.</summary>
    public required SubscriptionConverter _subscriptionConverter;

    /// <summary>The search parameters for this resource, by Name.</summary>
    private Dictionary<string, ModelInfo.SearchParamDefinition> _searchParameters = new();

    /// <summary>List of names of the search parameter urls toes.</summary>
    private Dictionary<string, string> _searchParamUrlsToNames = new();

    /// <summary>The executable subscriptions, by subscription topic url.</summary>
    private Dictionary<string, ExecutableSubscriptionInfo> _executableSubscriptions = new();

    /// <summary>The supported includes.</summary>
    private string[] _supportedIncludes = [];

    /// <summary>The supported reverse includes.</summary>
    private string[] _supportedRevIncludes = [];

    /// <summary>Gets the keys.</summary>
    /// <typeparam name="string">  Type of the string.</typeparam>
    /// <typeparam name="Resource">Type of the resource.</typeparam>
    IEnumerable<string> IReadOnlyDictionary<string, Resource>.Keys => _resourceStore.Keys;

    /// <summary>Gets the values.</summary>
    /// <typeparam name="string">  Type of the string.</typeparam>
    /// <typeparam name="Resource">Type of the resource.</typeparam>
    IEnumerable<Resource> IReadOnlyDictionary<string, Resource>.Values => _resourceStore.Values;

    /// <summary>Gets the number of. </summary>
    /// <typeparam name="string">   Type of the string.</typeparam>
    /// <typeparam name="Resource>">Type of the resource></typeparam>
    int IReadOnlyCollection<KeyValuePair<string, Resource>>.Count => _resourceStore.Count;

    /// <summary>Indexer to get items within this collection using array index syntax.</summary>
    /// <typeparam name="string">  Type of the string.</typeparam>
    /// <typeparam name="Resource">Type of the resource.</typeparam>
    /// <param name="key">The key.</param>
    /// <returns>The indexed item.</returns>
    Resource IReadOnlyDictionary<string, Resource>.this[string key] => _resourceStore[key];

    /// <summary>Query if 'key' contains key.</summary>
    /// <typeparam name="string">  Type of the string.</typeparam>
    /// <typeparam name="Resource">Type of the resource.</typeparam>
    /// <param name="key">The key.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    bool IReadOnlyDictionary<string, Resource>.ContainsKey(string key) => _resourceStore.ContainsKey(key);

    /// <summary>Attempts to get value a Resource from the given string.</summary>
    /// <typeparam name="string">  Type of the string.</typeparam>
    /// <typeparam name="Resource">Type of the resource.</typeparam>
    /// <param name="key">  The key.</param>
    /// <param name="value">[out] The value.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    bool IReadOnlyDictionary<string, Resource>.TryGetValue(string key, out Resource value)
    {
        bool result = _resourceStore.TryGetValue(key, out T? tVal);
        value = tVal ?? null!;
        return result;
    }

    /// <summary>Gets the enumerator.</summary>
    /// <typeparam name="string">   Type of the string.</typeparam>
    /// <typeparam name="Resource>">Type of the resource></typeparam>
    /// <returns>The enumerator.</returns>
    IEnumerator<KeyValuePair<string, Resource>> IEnumerable<KeyValuePair<string, Resource>>.GetEnumerator() =>
            _resourceStore.Select(kvp => new KeyValuePair<string, Resource>(kvp.Key, kvp.Value)).GetEnumerator();

    /// <summary>Gets the enumerator.</summary>
    /// <returns>The enumerator.</returns>
    IEnumerator IEnumerable.GetEnumerator() =>
            _resourceStore.Select(kvp => new KeyValuePair<string, Resource>(kvp.Key, kvp.Value)).GetEnumerator();

    /// <summary>Gets the keys.</summary>
    /// <typeparam name="string">Type of the string.</typeparam>
    /// <typeparam name="object">Type of the object.</typeparam>
    IEnumerable<string> IReadOnlyDictionary<string, object>.Keys => _resourceStore.Keys;

    /// <summary>Gets the values.</summary>
    /// <typeparam name="string">Type of the string.</typeparam>
    /// <typeparam name="object">Type of the object.</typeparam>
    IEnumerable<object> IReadOnlyDictionary<string, object>.Values => _resourceStore.Values;

    /// <summary>Gets the number of. </summary>
    /// <typeparam name="string"> Type of the string.</typeparam>
    /// <typeparam name="object>">Type of the object></typeparam>
    int IReadOnlyCollection<KeyValuePair<string, object>>.Count => _resourceStore.Count;

    /// <summary>Indexer to get items within this collection using array index syntax.</summary>
    /// <typeparam name="string">Type of the string.</typeparam>
    /// <typeparam name="object">Type of the object.</typeparam>
    /// <param name="key">The key.</param>
    /// <returns>The indexed item.</returns>
    object IReadOnlyDictionary<string, object>.this[string key] => _resourceStore[key];

    /// <summary>Query if 'key' contains key.</summary>
    /// <typeparam name="string">Type of the string.</typeparam>
    /// <typeparam name="object">Type of the object.</typeparam>
    /// <param name="key">The key.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    bool IReadOnlyDictionary<string, object>.ContainsKey(string key) => _resourceStore.ContainsKey(key);

    /// <summary>Attempts to get value an object from the given string.</summary>
    /// <typeparam name="string">Type of the string.</typeparam>
    /// <typeparam name="object">Type of the object.</typeparam>
    /// <param name="key">  The key.</param>
    /// <param name="value">[out] The value.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    bool IReadOnlyDictionary<string, object>.TryGetValue(string key, out object value)
    {
        bool result = _resourceStore.TryGetValue(key, out T? tVal);
        value = tVal ?? null!;
        return result;
    }

    /// <summary>Gets the enumerator.</summary>
    /// <typeparam name="string"> Type of the string.</typeparam>
    /// <typeparam name="object>">Type of the object></typeparam>
    /// <returns>The enumerator.</returns>
    IEnumerator<KeyValuePair<string, object>> IEnumerable<KeyValuePair<string, object>>.GetEnumerator() =>
        _resourceStore.Select(kvp => new KeyValuePair<string, object>(kvp.Key, kvp.Value)).GetEnumerator();

    /// <summary>Initializes a new instance of the <see cref="ResourceStore{T}"/> class.</summary>
    /// <param name="fhirStore">            The FHIR store.</param>
    /// <param name="searchTester">         The search tester.</param>
    /// <param name="topicConverter">       The topic converter.</param>
    /// <param name="subscriptionConverter">The subscription converter.</param>
    [SetsRequiredMembers]
    public ResourceStore(
        VersionedFhirStore fhirStore,
        SearchTester searchTester,
        TopicConverter topicConverter,
        SubscriptionConverter subscriptionConverter)
    {
        _store = fhirStore;
        _searchTester = searchTester;
        _topicConverter = topicConverter;
        _subscriptionConverter = subscriptionConverter;

        _tIsConformanceResource = typeof(T).GetInterface("IConformanceResource") != null;
        _tIsIdentifiable = typeof(T).GetInterface("IIdentifiable") != null;
        _tHasName = typeof(T).GetProperties().Any(p => p.Name == "Name");
    }

    /// <summary>Gets a name.</summary>
    /// <param name="resource">The resource.</param>
    /// <returns>The name.</returns>
    private string GetName(T resource)
    {
        if (!_tHasName)
        {
            return string.Empty;
        }

        switch (resource)
        {
            case IConformanceResource icr:
                return DisplayFor(icr.Name);

            case Account account:
                return DisplayFor(account.Name);

            case Patient patient:
                return DisplayFor(patient.Name);

            case Practitioner practitioner:
                return DisplayFor(practitioner.Name);

            case Organization organization:
                return DisplayFor(organization.Name);

            default:
                return string.Empty;
        }
    }

    /// <summary>Gets an identifier.</summary>
    /// <param name="resource">The resource.</param>
    /// <returns>The identifier.</returns>
    private string GetIdentifier(T resource)
    {
        if (!_tIsIdentifiable)
        {
            return string.Empty;
        }

        switch (resource)
        {
            case IIdentifiable<List<Identifier>> array:
                return DisplayFor(array.Identifier);

            case IIdentifiable<Identifier> scalar:
                return DisplayFor(scalar.Identifier);

            default:
                return string.Empty;
        }
    }

    /// <summary>
    /// Gets a value indicating whether this resource store contains conformance resources.
    /// </summary>
    public bool ResourcesAreConformance => _tIsConformanceResource;

    /// <summary>Gets a value indicating whether the resources have identifier.</summary>
    public bool ResourcesAreIdentifiable => _tIsIdentifiable;

    /// <summary>Gets a value indicating whether the resources have name.</summary>
    public bool ResourcesHaveName => _tHasName;

    /// <summary>Gets instance table view.</summary>
    /// <returns>The instance table view.</returns>
    public IQueryable<InstanceTableRec> GetInstanceTableView()
    {
        return _resourceStore.Select(kvp => new InstanceTableRec()
        {
            Id = kvp.Key,
            Name = GetName(kvp.Value),
            Url = _tIsConformanceResource ? (kvp.Value as IConformanceResource)!.Url : string.Empty,
            Description = _tIsConformanceResource ? (kvp.Value as IConformanceResource)!.Description : string.Empty,
            Identifiers = GetIdentifier(kvp.Value),
        }).AsQueryable();
    }

    private string DisplayFor(object o)
    {
        if (o == null)
        {
            return string.Empty;
        }

        switch (o)
        {
            case Hl7.Fhir.Model.Patient p:
                return $"{p.Id}: {string.Join(", ", p.Name.Select(n => $"{n.Family}, {string.Join(' ', n.Given)}"))}";

            case IEnumerable<Hl7.Fhir.Model.HumanName> hns:
                return string.Join(", ", hns.Select(hn => $"{hn.Family}, {string.Join(' ', hn.Given)}"));

            case Hl7.Fhir.Model.HumanName hn:
                return $"{hn.Family}, {string.Join(' ', hn.Given)}";

            case Hl7.Fhir.Model.FhirString s:
                return s.Value;

            case Hl7.Fhir.Model.Code c:
                return c.Value;

            case Hl7.Fhir.Model.Coding coding:
                return string.IsNullOrEmpty(coding.Display) ? $"{coding.System}|{coding.Code}" : coding.Display;

            case IEnumerable<Hl7.Fhir.Model.Identifier> ids:
                return string.Join(", ", ids.Select(id => DisplayFor(id)));

            case Hl7.Fhir.Model.Identifier i:
                {
                    if (!string.IsNullOrEmpty(i.System) || !string.IsNullOrEmpty(i.Value))
                    {
                        return $"{i.System}|{i.Value}";
                    }

                    if (i.Type != null)
                    {
                        return DisplayFor(i.Type);
                    }
                }
                break;

            case Hl7.Fhir.Model.ResourceReference rr:
                {
                    if (!string.IsNullOrEmpty(rr.Display))
                    {
                        return rr.Display;
                    }

                    if (!string.IsNullOrEmpty(rr.Reference))
                    {
                        return rr.Reference;
                    }

                    if (rr.Identifier != null)
                    {
                        DisplayFor(rr.Identifier);
                    }
                }
                break;

            case Hl7.Fhir.Model.CodeableConcept cc:
                {
                    if (!string.IsNullOrEmpty(cc.Text))
                    {
                        return cc.Text;
                    }

                    return string.Join(", ", cc.Coding.Select(c => string.IsNullOrEmpty(c.Display) ? $"{c.System}|{c.Code}" : c.Display));
                }

            case Hl7.Fhir.Model.Resource r:
                return r.TypeName + "/" + r.Id;
        }

        return o.ToString() ?? string.Empty;
    }

    /// <summary>Gets by canonical.</summary>
    /// <param name="url">URL of the resource.</param>
    /// <returns>The by canonical.</returns>
    public Resource? GetByCanonical(string url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return null;
        }

        if (!_conformanceUrlToId.TryGetValue(url, out string? id))
        {
            return null;
        }

        if (!_resourceStore.TryGetValue(id, out T? resource))
        {
            return null;
        }

        return resource;
    }

    /// <summary>
    /// Attempts to get by canonical a Hl7.Fhir.Model.Resource from the given string.
    /// </summary>
    /// <param name="url">     URL of the resource.</param>
    /// <param name="resource">[out] The resource.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    public bool TryGetByCanonical(string url, out Resource? resource)
    {
        if (string.IsNullOrEmpty(url))
        {
            resource = null;
            return false;
        }

        if (!_conformanceUrlToId.TryGetValue(url, out string? id))
        {
            resource = null;
            return false;
        }

        if (!_resourceStore.TryGetValue(id, out T? r))
        {
            resource = null;
            return false;
        }

        resource = r;
        return true;
    }

    /// <summary>Reads a specific instance of a resource.</summary>
    /// <param name="id">[out] The identifier.</param>
    /// <returns>The requested resource or null.</returns>
    public Resource? InstanceRead(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        if (!_resourceStore.TryGetValue(id, out T? resource))
        {
            return null;
        }

        return resource;
    }

    /// <summary>Interface for has identifier.</summary>
    internal interface IHasIdentifier
    {
        List<Hl7.Fhir.Model.Identifier> Identifier { get; }
    }

    /// <summary>Gets identifier key.</summary>
    /// <param name="id">[out] The identifier.</param>
    /// <returns>The identifier key.</returns>
    internal string GetIdentifierKey(Hl7.Fhir.Model.Identifier id)
    {
        return $"{id.System}|{id.Value}";
    }

    /// <summary>Attempts to resolve identifier.</summary>
    /// <param name="system">The system.</param>
    /// <param name="value"> The value.</param>
    /// <param name="r">     [out] The resolved resource process.</param>
    /// <returns>True if identifier, false if not.</returns>
    public bool TryResolveIdentifier(string system, string value, out Hl7.Fhir.Model.Resource? r)
    {
        if (_identifierToId.TryGetValue($"{system}|{value}", out string? id) &&
            !string.IsNullOrEmpty(id) &&
            _resourceStore.TryGetValue(id, out T? resource))
        {
            r = resource;
            return true;
        }

        r = null;
        return false;
    }

    /// <summary>
    /// Attempts to get a resource, returning a default value rather than throwing an exception if it
    /// fails.
    /// </summary>
    /// <param name="identifier">The identifier.</param>
    /// <returns>A T?</returns>
    internal T? TryGetResource(Hl7.Fhir.Model.Identifier identifier)
    {
        // lookup
        string key = GetIdentifierKey(identifier);
        if (_identifierToId.TryGetValue(key, out string? id))
        {
            if (_resourceStore.TryGetValue(id, out T? resource))
            {
                return resource;
            }
        }

        // TODO: Consider if we want a slow lookup here as well.

        return null;
    }

    /// <summary>Create an instance of a resource.</summary>
    /// <param name="ctx">            The context.</param>
    /// <param name="source">         [out] The resource.</param>
    /// <param name="allowExistingId">True to allow, false to suppress the existing identifier.</param>
    /// <returns>The created resource, or null if it could not be created.</returns>
    public Resource? InstanceCreate(
        FhirRequestContext ctx,
        Resource source,
        bool allowExistingId)
    {
        if ((source == null) ||
            (source is not T))
        {
            return null;
        }

        if ((!allowExistingId) || string.IsNullOrEmpty(source.Id))
        {
            source.Id = Guid.NewGuid().ToString();
        }

        ParsedSubscriptionTopic? parsedSubscriptionTopic = null;
        ParsedSubscription? parsedSubscription = null;

        // perform any mandatory validation
        switch (source.TypeName)
        {
            case "Basic":
                {
                    if (source is not Basic b)
                    {
                        return null;
                    }

                    string? basicFhirType = b.Code?.Coding?.FirstOrDefault(c => c.System == "http://hl7.org/fhir/fhir-types")?.Code;

                    switch (basicFhirType)
                    {
                        case "SubscriptionTopic":
                            if (!_topicConverter.TryParse(source, out parsedSubscriptionTopic))
                            {
                                return null;
                            }
                            break;
                    }
                }
                break;

            case "Observation":
                {
                    // special handling for Vitals Write Project - https://hackmd.io/jgLf4IF4RNCqtDABAmVrug?view
                    if (source is Hl7.Fhir.Model.Observation obs)
                    {
                        // if there is authorization info, check to see if this is a patient launch
                        if (ctx.Authorization?.UserId.StartsWith("Patient", StringComparison.Ordinal) ?? false)
                        {
                            if (source.Meta == null)
                            {
                                source.Meta = new Meta();
                            }

                            if (source.Meta.Tag == null)
                            {
                                source.Meta.Tag = new List<Coding>();
                            }

                            if (!source.Meta.Tag.Any(c =>
                                c.System.Equals("http://hl7.org/fhir/us/core/CodeSystem/us-core-tags", StringComparison.Ordinal) &&
                                c.Code.Equals("patient-supplied")))
                            {
                                source.Meta.Tag.Add(new Coding("http://hl7.org/fhir/us/core/CodeSystem/us-core-tags", "patient-supplied"));
                            }
                        }

                        // check for the performer being the subject
                        if (obs.Performer.Any(r => r.Reference.Equals(obs.Subject.Reference, StringComparison.Ordinal)))
                        {
                            if (source.Meta == null)
                            {
                                source.Meta = new Meta();
                            }

                            if (source.Meta.Tag == null)
                            {
                                source.Meta.Tag = new List<Coding>();
                            }

                            if (!source.Meta.Tag.Any(c =>
                                c.System.Equals("http://hl7.org/fhir/us/core/CodeSystem/us-core-tags", StringComparison.Ordinal) &&
                                c.Code.Equals("patient-supplied")))
                            {
                                source.Meta.Tag.Add(new Coding("http://hl7.org/fhir/us/core/CodeSystem/us-core-tags", "patient-supplied"));
                            }
                        }
                    }
                }
                break;

            case "SubscriptionTopic":
                // fail the request if this fails
                if (!_topicConverter.TryParse(source, out parsedSubscriptionTopic))
                {
                    return null;
                }
                break;

            case "Subscription":
                // fail the request if this fails
                if (!_subscriptionConverter.TryParse((Subscription)source, out parsedSubscription))
                {
                    return null;
                }
                break;
        }

        lock (_lockObject)
        {
            if (_resourceStore.ContainsKey(source.Id))
            {
                return null;
            }

            if (source.Meta == null)
            {
                source.Meta = new Meta();
            }

            source.Meta.VersionId = "1";
            source.Meta.LastUpdated = DateTimeOffset.UtcNow;

            if (!_resourceStore.TryAdd(source.Id, (T)source))
            {
                return null;
            }
        }

        RegisterInstanceCreated(source.Id);
        _store.RegisterInstanceCreated(_resourceName, source.Id);

        if (source is IConformanceResource cr)
        {
            if (!string.IsNullOrEmpty(cr.Url))
            {
                _ = _conformanceUrlToId.TryAdd(cr.Url, source.Id);
            }
        }

        if (source is IIdentifiable<List<Identifier>> sil)
        {
            foreach (Identifier i in sil.Identifier)
            {
                if (sil.Identifier != null)
                {
                    _ = _identifierToId.TryAdd(GetIdentifierKey(i), source.Id);
                }
            }
        }
        else if (source is IIdentifiable<Identifier> si)
        {
            if (si.Identifier != null)
            {
                _ = _identifierToId.TryAdd(GetIdentifierKey(si.Identifier), source.Id);
            }
        }

        TestCreateAgainstSubscriptions((T)source);

        if (parsedSubscriptionTopic != null)
        {
            _ = _store.StoreProcessSubscriptionTopic(parsedSubscriptionTopic);
        }

        if (parsedSubscription != null)
        {
            _ = _store.StoreProcessSubscription(parsedSubscription);
        }

        switch (source)
        {
            case CompartmentDefinition cd:
                _store.RegisterCompartmentDefinition(cd);
                break;

            case SearchParameter sp:
                SetExecutableSearchParameter(sp);
                break;

            case ValueSet vs:
                _ = _store.Terminology.StoreProcessValueSet(vs);
                break;
        }

        return source;
    }

    /// <summary>Update a specific instance of a resource.</summary>
    /// <param name="source">            [out] The resource.</param>
    /// <param name="allowCreate">       True to allow, false to suppress the create.</param>
    /// <param name="ifMatch">           A match specifying if.</param>
    /// <param name="ifNoneMatch">       A match specifying if none.</param>
    /// <param name="protectedResources">The protected resources.</param>
    /// <param name="sc">                [out] The screen.</param>
    /// <param name="outcome">           [out] The outcome.</param>
    /// <returns>The updated resource, or null if it could not be performed.</returns>
    public Resource? InstanceUpdate(
        Resource source,
        bool allowCreate,
        string ifMatch,
        string ifNoneMatch,
        HashSet<string> protectedResources,
        out HttpStatusCode sc,
        out OperationOutcome outcome)
    {
        if ((source == null) ||
            (source is not T))
        {
            outcome = SerializationUtils.BuildOutcomeForRequest(HttpStatusCode.BadRequest, $"Invalid resource content for {_resourceName}");
            sc = HttpStatusCode.BadRequest;
            return null;
        }

        if (string.IsNullOrEmpty(source.Id))
        {
            outcome = SerializationUtils.BuildOutcomeForRequest(HttpStatusCode.BadRequest, "Cannot update resources without an ID");
            sc = HttpStatusCode.BadRequest;
            return null;
        }

        if (source.Meta == null)
        {
            source.Meta = new Meta();
        }

        if (protectedResources.Any() && protectedResources.Contains(_resourceName + "/" + source.Id))
        {
            outcome = SerializationUtils.BuildOutcomeForRequest(HttpStatusCode.Unauthorized, $"Resource {_resourceName}/{source.Id} is protected and cannot be changed");
            sc = HttpStatusCode.Unauthorized;
            return null;
        }

        ParsedSubscriptionTopic? parsedSubscriptionTopic = null;
        ParsedSubscription? parsedSubscription = null;

        // perform any mandatory validation
        switch (source.TypeName)
        {
            case "Basic":
                {
                    // fail the request if this fails
                    if ((source is Hl7.Fhir.Model.Basic b) &&
                        (b.Code?.Coding?.Any() ?? false) &&
                        (b.Code.Coding.Any(c =>
                            c.Code.Equals("SubscriptionTopic", StringComparison.Ordinal) &&
                            c.System.Equals("http://hl7.org/fhir/fhir-types", StringComparison.Ordinal))))
                    {
                        if (!_topicConverter.TryParse(source, out parsedSubscriptionTopic))
                        {
                            outcome = SerializationUtils.BuildOutcomeForRequest(
                                HttpStatusCode.BadRequest,
                                $"Basic-wrapped SubscriptionTopic could not be parsed!");
                            sc = HttpStatusCode.BadRequest;
                            return null;
                        }
                    }
                }
                break;

            case "SubscriptionTopic":
                // fail the request if this fails
                if (!_topicConverter.TryParse(source, out parsedSubscriptionTopic))
                {
                    outcome = SerializationUtils.BuildOutcomeForRequest(
                        HttpStatusCode.BadRequest,
                        $"SubscriptionTopic could not be parsed!");
                    sc = HttpStatusCode.BadRequest;
                    return null;
                }
                break;

            case "Subscription":
                // fail the request if this fails
                if (!_subscriptionConverter.TryParse((Subscription)source, out parsedSubscription))
                {
                    outcome = SerializationUtils.BuildOutcomeForRequest(
                        HttpStatusCode.BadRequest,
                        $"Subscription could not be parsed!");
                    sc = HttpStatusCode.BadRequest;
                    return null;
                }
                break;
        }

        T? previous;

        lock (_lockObject)
        {
            if (!_resourceStore.ContainsKey(source.Id))
            {
                if (allowCreate)
                {
                    source.Meta.VersionId = "1";
                    previous = null;
                }
                else
                {
                    outcome = SerializationUtils.BuildOutcomeForRequest(
                        HttpStatusCode.BadRequest,
                        $"Update as Create is disabled");
                    sc = HttpStatusCode.BadRequest;
                    return null;
                }
            }
            else if (int.TryParse(_resourceStore[source.Id].Meta?.VersionId ?? string.Empty, out int version))
            {
                previous = (T)_resourceStore[source.Id].DeepCopy();
                source.Meta.VersionId = (version + 1).ToString();
            }
            else
            {
                previous = (T)_resourceStore[source.Id].DeepCopy();
                source.Meta.VersionId = "1";
            }

            // check preconditions
            if (ifNoneMatch.Equals("*", StringComparison.Ordinal))
            {
                outcome = SerializationUtils.BuildOutcomeForRequest(
                    HttpStatusCode.PreconditionFailed,
                    "Prior version exists, but If-None-Match is *");
                sc = HttpStatusCode.PreconditionFailed;
                return null;
            }

            if (!string.IsNullOrEmpty(ifNoneMatch))
            {
                if (ifNoneMatch.Equals($"W/\"{previous?.Meta.VersionId ?? string.Empty}\"", StringComparison.Ordinal))
                {
                    outcome = SerializationUtils.BuildOutcomeForRequest(
                        HttpStatusCode.PreconditionFailed,
                        $"Conditional update query returned a match with version: {previous?.Meta.VersionId}, If-None-Match: {ifNoneMatch}");
                    sc = HttpStatusCode.PreconditionFailed;
                    return null;
                }
            }

            if (!string.IsNullOrEmpty(ifMatch))
            {
                if (!ifMatch.Equals($"W/\"{previous?.Meta.VersionId}\"", StringComparison.Ordinal))
                {
                    outcome = SerializationUtils.BuildOutcomeForRequest(
                        HttpStatusCode.PreconditionFailed,
                        $"Conditional update query returned a match with version: {previous?.Meta.VersionId}, If-Match: {ifNoneMatch}");
                    sc = HttpStatusCode.PreconditionFailed;
                    return null;
                }
            }

            // update the last updated time
            source.Meta.LastUpdated = DateTimeOffset.UtcNow;

            // store
            _resourceStore[source.Id] = (T)source;
        }

        RegisterInstanceUpdated(source.Id);
        _store.RegisterInstanceUpdated(_resourceName, source.Id);

        if (source is IConformanceResource cr)
        {
            if (previous is IConformanceResource pcr)
            {
                if (!string.IsNullOrEmpty(pcr.Url))
                {
                    _ = _conformanceUrlToId.TryRemove(pcr.Url, out _);
                }
            }

            if (!string.IsNullOrEmpty(cr.Url))
            {
                _ = _conformanceUrlToId.TryAdd(cr.Url, source.Id);
            }
        }

        if (source is IHasIdentifier hasId)
        {
            if (previous is IHasIdentifier pId)
            {
                foreach (Identifier i in pId.Identifier)
                {
                    _ = _identifierToId.TryRemove(GetIdentifierKey(i), out _);
                }
            }

            foreach (Identifier i in hasId.Identifier)
            {
                _ = _identifierToId.TryAdd(GetIdentifierKey(i), source.Id);
            }
        }

        if (previous == null)
        {
            TestCreateAgainstSubscriptions((T)source);
        }
        else
        {
            TestUpdateAgainstSubscriptions((T)source, previous);
        }

        if (parsedSubscriptionTopic != null)
        {
            _ = _store.StoreProcessSubscriptionTopic(parsedSubscriptionTopic);
        }

        if (parsedSubscription != null)
        {
            _ = _store.StoreProcessSubscription(parsedSubscription);
        }

        switch (source)
        {
            case CompartmentDefinition cd:
                _store.RegisterCompartmentDefinition(cd);
                break;

            case SearchParameter sp:
                SetExecutableSearchParameter(sp);
                break;

            case ValueSet vs:
                _ = _store.Terminology.StoreProcessValueSet(vs);
                break;
        }

        outcome = SerializationUtils.BuildOutcomeForRequest(HttpStatusCode.OK, $"Updated {_resourceName}/{source.Id} to version {source.Meta.VersionId}");
        sc = HttpStatusCode.OK;
        return source;
    }

    /// <summary>Instance delete.</summary>
    /// <param name="id">                [out] The identifier.</param>
    /// <param name="protectedResources">The protected resources.</param>
    /// <returns>The deleted resource or null.</returns>
    public Resource? InstanceDelete(string id, HashSet<string> protectedResources)
    {
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        if (!_resourceStore.ContainsKey(id))
        {
            return null;
        }

        if (protectedResources.Any() && protectedResources.Contains(_resourceName + "/" + id))
        {
            return null;
        }

        T? previous;

        lock (_lockObject)
        {
            _ = _resourceStore.TryRemove(id, out previous);
        }

        if (previous == null)
        {
            return null;
        }

        RegisterInstanceDeleted(id);
        _store.RegisterInstanceDeleted(_resourceName, id);

        if (previous is IConformanceResource pcr)
        {
            if (!string.IsNullOrEmpty(pcr.Url))
            {
                _ = _conformanceUrlToId.TryRemove(pcr.Url, out _);
            }
        }

        if (previous is IHasIdentifier pId)
        {
            foreach (Identifier i in pId.Identifier)
            {
                _ = _identifierToId.TryRemove(GetIdentifierKey(i), out _);
            }
        }

        TestDeleteAgainstSubscriptions(previous);

        switch (previous?.TypeName)
        {
            case "CompartmentDefinition":
                {
                    if ((previous is CompartmentDefinition cd) && (cd.Code != null))
                    {
                        _store.RemoveCompartmentDefinition(cd.Code.GetLiteral()!);
                    }
                }
                break;

            case "SearchParameter":
                RemoveExecutableSearchParameter((SearchParameter)(Resource)previous);
                break;

            case "SubscriptionTopic":
                {
                    // get a common subscription topic for execution
                    if (_topicConverter.TryParse(previous, out ParsedSubscriptionTopic topic))
                    {
                        _ = _store.StoreProcessSubscriptionTopic(topic, true);
                    }

                }
                break;

            case "Subscription":
                {
                    if (_subscriptionConverter.TryParse(previous, out ParsedSubscription subscription))
                    {
                        _ = _store.StoreProcessSubscription(subscription, true);
                    }
                }
                break;

            case "ValueSet":
                _ = _store.Terminology.StoreProcessValueSet((ValueSet)(Resource)previous, true);
                break;
        }

        return previous;
    }

    /// <summary>Sets executable subscription topic.</summary>
    /// <param name="url">             URL of the resource.</param>
    /// <param name="compiledTriggers">The compiled triggers.</param>
    public void SetExecutableSubscriptionTopic(
        string url,
        IEnumerable<ExecutableSubscriptionInfo.InteractionOnlyTrigger> interactionTriggers,
        IEnumerable<ExecutableSubscriptionInfo.CompiledFhirPathTrigger> fhirpathTriggers,
        IEnumerable<ExecutableSubscriptionInfo.CompiledQueryTrigger> queryTriggers,
        ParsedResultParameters? resultParameters)
    {
        if (_executableSubscriptions.ContainsKey(url))
        {
            _executableSubscriptions[url].InteractionTriggers = interactionTriggers;
            _executableSubscriptions[url].FhirPathTriggers = fhirpathTriggers;
            _executableSubscriptions[url].QueryTriggers = queryTriggers;
            _executableSubscriptions[url].AdditionalContext = resultParameters;
        }
        else
        {
            _executableSubscriptions.Add(url, new()
            {
                TopicUrl = url,
                InteractionTriggers = interactionTriggers,
                FhirPathTriggers = fhirpathTriggers,
                QueryTriggers = queryTriggers,
                AdditionalContext = resultParameters,
            });
        }
    }

    /// <summary>Sets executable subscription.</summary>
    /// <param name="topicUrl">URL of the topic.</param>
    /// <param name="id">      The subscription id.</param>
    /// <param name="filters"> The compiled filters.</param>
    public void SetExecutableSubscription(string topicUrl, string id, List<ParsedSearchParameter> filters)
    {
        if (!_executableSubscriptions.ContainsKey(topicUrl))
        {
            _executableSubscriptions.Add(topicUrl, new()
            {
                TopicUrl = topicUrl,
            });
        }

        if (_executableSubscriptions[topicUrl].FiltersBySubscription.ContainsKey(id))
        {
            _executableSubscriptions[topicUrl].FiltersBySubscription[id] = filters;
        }
        else
        {
            _executableSubscriptions[topicUrl].FiltersBySubscription.Add(id, filters);
        }
    }

    /// <summary>Removes the executable subscription described by subscriptionTopicUrl.</summary>
    /// <param name="subscriptionTopicUrl">URL of the subscription topic.</param>
    public void RemoveExecutableSubscriptionTopic(string subscriptionTopicUrl)
    {
        if (_executableSubscriptions.ContainsKey(subscriptionTopicUrl))
        {
            _executableSubscriptions.Remove(subscriptionTopicUrl);
        }
    }

    /// <summary>Removes the executable subscription.</summary>
    /// <param name="topicUrl">URL of the topic.</param>
    /// <param name="id">      The subscription id.</param>
    public void RemoveExecutableSubscription(string topicUrl, string id)
    {
        if (!_executableSubscriptions.ContainsKey(topicUrl))
        {
            return;
        }

        if (!_executableSubscriptions[topicUrl].FiltersBySubscription.ContainsKey(id))
        {
            return;
        }

        _executableSubscriptions[topicUrl].FiltersBySubscription.Remove(id);
    }


    /// <summary>Performs the subscription test action.</summary>
    /// <param name="current">  The current resource POCO</param>
    /// <param name="currentTE">The current resource ITypedElement.</param>
    /// <param name="fpContext">The FHIRPath evaluation context.</param>
    private void PerformSubscriptionTest(
        T? current,
        ITypedElement? currentTE,
        T? previous,
        ITypedElement? previousTE,
        FhirEvaluationContext fpContext,
        ExecutableSubscriptionInfo.InteractionTypes interaction)
    {
        // sanity check
        switch (interaction)
        {
            case ExecutableSubscriptionInfo.InteractionTypes.Create:
                if ((current == null) ||
                    (currentTE == null))
                {
                    return;
                }
                break;

            case ExecutableSubscriptionInfo.InteractionTypes.Update:
                if ((current == null) ||
                    (currentTE == null) ||
                    (previous == null) ||
                    (previousTE == null))
                {
                    return;
                }
                break;

            case ExecutableSubscriptionInfo.InteractionTypes.Delete:
                if ((previous == null) ||
                    (previousTE == null))
                {
                    return;
                }
                break;
        }

        HashSet<string> notifiedSubscriptions = new();
        List<string> matchedTopics = new();

        Dictionary<string, List<string>> topicErrors = new();

        foreach ((string topicUrl, ExecutableSubscriptionInfo executable) in _executableSubscriptions)
        {
            bool matched = false;

            // first, check for interaction types
            if (executable.InteractionTriggers.Any())
            {
                switch (interaction)
                {
                    case ExecutableSubscriptionInfo.InteractionTypes.Create:
                        if (executable.InteractionTriggers.Any(it => it.OnCreate == true))
                        {
                            matched = true;
                            matchedTopics.Add(topicUrl);
                            break;
                        }
                        break;

                    case ExecutableSubscriptionInfo.InteractionTypes.Update:
                        if (executable.InteractionTriggers.Any(it => it.OnUpdate == true))
                        {
                            matched = true;
                            matchedTopics.Add(topicUrl);
                            break;
                        }
                        break;

                    case ExecutableSubscriptionInfo.InteractionTypes.Delete:
                        if (executable.InteractionTriggers.Any(it => it.OnDelete == true))
                        {
                            matched = true;
                            matchedTopics.Add(topicUrl);
                            break;
                        }
                        break;
                }
            }

            if (matched)
            {
                continue;
            }

            // second, test FhirPath
            if (executable.FhirPathTriggers.Any())
            {
                foreach (ExecutableSubscriptionInfo.CompiledFhirPathTrigger cfp in executable.FhirPathTriggers)
                {
                    try
                    {
                        bool result;

                        if (currentTE != null)
                        {
                            result = cfp.FhirPathTrigger.IsTrue(currentTE, fpContext);
                        }
                        else if (previousTE != null)
                        {
                            //result = cfp.FhirPathTrigger.IsTrue(previousTE, fpContext).First() ?? null;
                            result = cfp.FhirPathTrigger.IsTrue(previousTE, fpContext);
                        }
                        else
                        {
                            continue;
                        }

                        if (!result)
                        {
                            continue;
                        }

                        //if ((result == null) ||
                        //    (result.Value == null) ||
                        //    (!(result.Value is bool val)) ||
                        //    (val == false))
                        //{
                        //    continue;
                        //}

                        matched = true;
                        matchedTopics.Add(topicUrl);
                        break;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"ResourceStore[{_resourceName}] <<< Error evaluating FhirPath trigger for topic {topicUrl}: {ex.Message}");

                        if (!topicErrors.ContainsKey(topicUrl))
                        {
                            topicErrors.Add(topicUrl, new());
                        }

                        if (ex.InnerException == null)
                        {
                            topicErrors[topicUrl].Add($"Error while evaluating FhirPath trigger for topic {topicUrl} on resource {_resourceName}: {ex.Message}");
                        }
                        else
                        {
                            topicErrors[topicUrl].Add($"Error while evaluating FhirPath trigger for topic {topicUrl} on resource {_resourceName}: {ex.Message}:{ex.InnerException.Message}");
                        }

                        continue;
                    }
                }
            }

            if (matched)
            {
                continue;
            }

            // finally, test query
            if (executable.QueryTriggers.Any())
            {
                bool previousPassed = false;
                bool currentPassed = false;

                foreach (ExecutableSubscriptionInfo.CompiledQueryTrigger cq in executable.QueryTriggers)
                {
                    try
                    {

                        switch (interaction)
                        {
                            case ExecutableSubscriptionInfo.InteractionTypes.Create:
                                {
                                    if (!cq.OnCreate)
                                    {
                                        continue;
                                    }

                                    previousPassed = cq.CreateAutoPasses;
                                    currentPassed = _searchTester.TestForMatch(
                                        currentTE!,
                                        cq.CurrentTest,
                                        fpContext);
                                }
                                break;

                            case ExecutableSubscriptionInfo.InteractionTypes.Update:
                                {
                                    if (!cq.OnUpdate)
                                    {
                                        continue;
                                    }

                                    previousPassed = _searchTester.TestForMatch(
                                        previousTE!,
                                        cq.PreviousTest,
                                        fpContext);
                                    currentPassed = _searchTester.TestForMatch(
                                        currentTE!,
                                        cq.CurrentTest,
                                        fpContext);
                                }
                                break;

                            case ExecutableSubscriptionInfo.InteractionTypes.Delete:
                                {
                                    if (!cq.OnDelete)
                                    {
                                        continue;
                                    }

                                    previousPassed = _searchTester.TestForMatch(
                                        previousTE!,
                                        cq.PreviousTest,
                                        fpContext);
                                    currentPassed = cq.DeleteAutoPasses;
                                }
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"ResourceStore[{_resourceName}] <<< Error evaluating Query trigger for topic {topicUrl}: {ex.Message}");

                        if (!topicErrors.ContainsKey(topicUrl))
                        {
                            topicErrors.Add(topicUrl, new());
                        }

                        if (ex.InnerException == null)
                        {
                            topicErrors[topicUrl].Add($"Error while evaluating Query trigger for topic {topicUrl} on resource {_resourceName}: {ex.Message}");
                        }
                        else
                        {
                            topicErrors[topicUrl].Add($"Error while evaluating Query trigger for topic {topicUrl} on resource {_resourceName}: {ex.Message}:{ex.InnerException.Message}");
                        }

                        continue;
                    }

                    if ((cq.RequireBothTests && previousPassed && currentPassed) ||
                        ((!cq.RequireBothTests) && (previousPassed || currentPassed)))
                    {
                        matched = true;
                        matchedTopics.Add(topicUrl);
                        break;
                    }
                }
            }

            if (matched)
            {
                continue;
            }
        }

        Resource focus = current ?? previous!;
        ITypedElement focusTE = currentTE ?? previousTE!;

        Dictionary<string, List<string>> subscriptionErrors = new();

        // traverse the list of matched topics to test against subscription filters
        foreach (string topicUrl in matchedTopics)
        {
            ParsedResultParameters? additions = _executableSubscriptions[topicUrl].AdditionalContext;

            foreach ((string subscriptionId, List<ParsedSearchParameter> filters) in _executableSubscriptions[topicUrl].FiltersBySubscription)
            {
                // don't trigger twice on multiple passing filters
                if (notifiedSubscriptions.Contains(subscriptionId))
                {
                    continue;
                }

                if ((!filters.Any()) ||
                    (_searchTester.TestForMatch(focusTE, filters, fpContext)))
                {
                    notifiedSubscriptions.Add(subscriptionId);

                    List<object> additionalContext = new();

                    if (additions != null)
                    {
                        HashSet<string> addedIds = new();
                        addedIds.Add($"{focus.TypeName}/{focus.Id}");

                        IEnumerable<Resource> inclusions = _store.ResolveInclusions(
                            focus,
                            focusTE,
                            additions,
                            addedIds,
                            fpContext);

                        if (inclusions.Any())
                        {
                            additionalContext.AddRange(inclusions);
                        }

                        IEnumerable<Resource> reverses = _store.ResolveReverseInclusions(
                            focus,
                            additions,
                            addedIds);

                        if (reverses.Any())
                        {
                            additionalContext.AddRange(reverses);
                        }
                    }

                    SubscriptionEvent subEvent = new()
                    {
                        SubscriptionId = subscriptionId,
                        TopicUrl = topicUrl,
                        EventNumber = _store.GetSubscriptionEventCount(subscriptionId, true),
                        Focus = focus,
                        AdditionalContext = additionalContext.AsEnumerable<object>(),
                    };

                    _store.RegisterSendEvent(subscriptionId, subEvent);
                }
            }
        }

        foreach ((string topicUrl, List<string> errors) in topicErrors)
        {
            if (!_executableSubscriptions.ContainsKey(topicUrl))
            {
                continue;
            }

            foreach (string subId in _executableSubscriptions[topicUrl].FiltersBySubscription.Keys)
            {
                foreach (string error in errors)
                {
                    _store.RegisterError(subId, error);
                }
            }
        }
    }

    /// <summary>Tests a create interaction against all subscriptions.</summary>
    /// <param name="current">The current resource version.</param>
    public void TestCreateAgainstSubscriptions(T current)
    {
        // TODO: Change this to async

        if (!_executableSubscriptions.Any())
        {
            return;
        }

        try
        {
            ITypedElement currentTE = current.ToTypedElement();

            FhirEvaluationContext fpContext = new FhirEvaluationContext()
            {
                Resource = currentTE,
                TerminologyService = _store.Terminology,
                ElementResolver = _store.Resolve,
                Environment = new Dictionary<string, IEnumerable<ITypedElement>>()
                {
                    { "current", [currentTE] },
                    { "previous", [] },
                },
            };

            PerformSubscriptionTest(
                current,
                currentTE,
                null,
                null,
                fpContext,
                ExecutableSubscriptionInfo.InteractionTypes.Create);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ResourceStore[{_resourceName}] <<< TestCreateAgainstSubscriptions caught: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"ResourceStore[{_resourceName}] <<< TestCreateAgainstSubscriptions caught: {ex.InnerException.Message}");
            }
        }
    }

    /// <summary>Tests an update interaction against all subscriptions.</summary>
    /// <param name="current"> The current resource version.</param>
    /// <param name="previous">The previous resource version.</param>
    public void TestUpdateAgainstSubscriptions(T current, T previous)
    {
        // TODO: Change this to async

        if (!_executableSubscriptions.Any())
        {
            return;
        }

        try
        {
            ITypedElement currentTE = current.ToTypedElement();
            ITypedElement previousTE = previous.ToTypedElement();

            FhirEvaluationContext fpContext = new FhirEvaluationContext()
            {
                Resource = currentTE,
                TerminologyService = _store.Terminology,
                ElementResolver = _store.Resolve,
                Environment = new Dictionary<string, IEnumerable<ITypedElement>>()
                {
                    { "current", [currentTE] },
                    { "previous", [previousTE] },
                },
            };

            //string test = "meta.tag.memberOf('http://hl7.org/fhir/us/davinci-cdex/ValueSet/cdex-work-queue')";
            //bool evalRes = current.IsTrue(test, fpContext);

            PerformSubscriptionTest(
                current,
                currentTE,
                previous,
                previousTE,
                fpContext, ExecutableSubscriptionInfo.InteractionTypes.Update);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ResourceStore[{_resourceName}] <<< TestUpdateAgainstSubscriptions caught: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"ResourceStore[{_resourceName}] <<< TestUpdateAgainstSubscriptions caught: {ex.InnerException.Message}");
            }
        }
    }

    /// <summary>Tests a delete interaction against all subscriptions.</summary>
    /// <param name="previous">The previous resource version.</param>
    public void TestDeleteAgainstSubscriptions(T previous)
    {
        // TODO: Change this to async

        if (!_executableSubscriptions.Any())
        {
            return;
        }

        try
        {
            ITypedElement previousTE = previous.ToTypedElement();

            FhirEvaluationContext fpContext = new FhirEvaluationContext()
            {
                Resource = previousTE,
                TerminologyService = _store.Terminology,
                ElementResolver = _store.Resolve,
                Environment = new Dictionary<string, IEnumerable<ITypedElement>>()
                {
                    { "current", [] },
                    { "previous", [ previousTE ] },
                },
            };

            PerformSubscriptionTest(
                null,
                null,
                previous,
                previousTE,
                fpContext,
                ExecutableSubscriptionInfo.InteractionTypes.Delete);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ResourceStore[{_resourceName}] <<< TestDeleteAgainstSubscriptions caught: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"ResourceStore[{_resourceName}] <<< TestDeleteAgainstSubscriptions caught: {ex.InnerException.Message}");
            }
        }
    }

    /// <summary>Adds or updates an executable search parameter based on a SearchParameter resource.</summary>
    /// <param name="sp">    The sp.</param>
    /// <param name="delete">(Optional) True to delete.</param>
    private void SetExecutableSearchParameter(SearchParameter sp)
    {
        if ((sp == null) ||
            (sp.Type == null))
        {
            return;
        }

        string name = sp.Code ?? sp.Name ?? sp.Id;

        ModelInfo.SearchParamDefinition spDefinition = new()
        {
            Name = name,
            Url = sp.Url,
            Description = sp.Description,
            Expression = sp.Expression,
            Target = ResourceTypeExtensions.CopyTargetsToRt(sp.Target),
            Type = (SearchParamType)sp.Type!,
            Component = sp.Component?.Select(cp => new ModelInfo.SearchParamComponent(cp.Definition, cp.Expression)).ToArray(),
        };

        foreach (ResourceType rt in ResourceTypeExtensions.CopyTargetsToRt(sp.Base) ?? Array.Empty<ResourceType>())
        {
            try
            {
                spDefinition.Resource = ModelInfo.ResourceTypeToFhirTypeName(rt)!;
                _store.TrySetExecutableSearchParameter(spDefinition.Resource, spDefinition);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ResourceStore[{_resourceName}] <<< Exception Setting Executable Search Parameter {rt}.{name}: {ex.Message}");
            }
        }
    }

    /// <summary>Removes the executable search parameter described by name.</summary>
    /// <param name="sp">The sp.</param>
    private void RemoveExecutableSearchParameter(SearchParameter sp)
    {
        if ((sp == null) ||
            (sp.Type == null))
        {
            return;
        }

        string name = sp.Code ?? sp.Name ?? sp.Id;

        foreach (ResourceType rt in ResourceTypeExtensions.CopyTargetsToRt(sp.Base) ?? Array.Empty<ResourceType>())
        {
            _store.TryRemoveExecutableSearchParameter(ModelInfo.ResourceTypeToFhirTypeName(rt)!, name);
        }
    }

    /// <summary>Removes the executable search parameter described by name.</summary>
    /// <param name="name">The name.</param>
    public void RemoveExecutableSearchParameter(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return;
        }

        if (_searchParameters.TryGetValue(name, out ModelInfo.SearchParamDefinition? spd))
        {
            if ((!string.IsNullOrEmpty(spd.Url)) &&
                _searchParamUrlsToNames.ContainsKey(spd.Url))
            {
                _ = _searchParamUrlsToNames.Remove(spd.Url);
            }

            _searchParameters.Remove(name);
        }
    }

    /// <summary>Adds a search parameter definition.</summary>
    /// <param name="spDefinition">The sp definition.</param>
    public void SetExecutableSearchParameter(ModelInfo.SearchParamDefinition spDefinition)
    {
        if (string.IsNullOrEmpty(spDefinition?.Name))
        {
            return;
        }

        if (spDefinition.Resource != _resourceName)
        {
            return;
        }

        if (_searchParameters.ContainsKey(spDefinition.Name))
        {
            _searchParameters[spDefinition.Name] = spDefinition;
        }
        else
        {
            _searchParameters.Add(spDefinition.Name, spDefinition);
        }

        if (!string.IsNullOrEmpty(spDefinition.Url))
        {
            _ = _searchParamUrlsToNames.TryAdd(spDefinition.Url, spDefinition.Name);
        }

        //// check for not having a matching search parameter resource
        //if (!_store.TryResolve($"SearchParameter/{_resourceName}-{spDefinition.Name}", out ITypedElement? _))
        //{
        //    SearchParameter sp = new()
        //    {
        //        Id = $"{_resourceName}-{spDefinition.Name}",
        //        Name = spDefinition.Name,
        //        Code = spDefinition.Name,
        //        Url = spDefinition.Url,
        //        Description = spDefinition.Description,
        //        Expression = spDefinition.Expression,
        //        Target = VersionedShims.CopyTargetsNullable(spDefinition.Target),
        //        Type = spDefinition.Type,
        //    };

        //    if (spDefinition.Component?.Any() ?? false)
        //    {
        //        sp.Component = new();

        //        foreach (ModelInfo.SearchParamComponent component in spDefinition.Component)
        //        {
        //            sp.Component.Add(new()
        //            {
        //                Definition = component.Definition,
        //                Expression = component.Expression,
        //            });
        //        }
        //    }
        //}
    }

    /// <summary>
    /// Attempts to get search parameter definition a ModelInfo.SearchParamDefinition from the given
    /// string.
    /// </summary>
    /// <param name="name">        The name.</param>
    /// <param name="spDefinition">[out] The sp definition.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    public bool TryGetSearchParamDefinition(string name, [NotNullWhen(true)] out ModelInfo.SearchParamDefinition? spDefinition)
    {
        if (ParsedSearchParameter._allResourceParameters.ContainsKey(name))
        {
            spDefinition = ParsedSearchParameter._allResourceParameters[name];
            return true;
        }

        if (_searchParameters.TryGetValue(name, out spDefinition))
        {
            return true;
        }

        if (_searchParamUrlsToNames.TryGetValue(name, out string? spName))
        {
            return _searchParameters.TryGetValue(spName, out spDefinition);
        }

        return false;
    }

    /// <summary>Gets the search parameter definitions known by this store.</summary>
    /// <returns>
    /// An enumerator that allows foreach to be used to process the search parameter definitions in
    /// this collection.
    /// </returns>
    public IEnumerable<ModelInfo.SearchParamDefinition> GetSearchParamDefinitions() => _searchParameters.Values;

    /// <summary>Gets the search includes supported by this store.</summary>
    /// <returns>
    /// An enumerator that allows foreach to be used to process the search includes in this
    /// collection.
    /// </returns>
    public IEnumerable<string> GetSearchIncludes() => _searchParameters.Values
        .Where(sp => sp.Type == SearchParamType.Reference)
        .Where(sp => sp.Name != null)
        .Select(sp => this._resourceName + ":" + sp.Name!)
        .Order();

    /// <summary>Gets the search reverse includes supported by this store.</summary>
    /// <returns>
    /// An enumerator that allows foreach to be used to process the search reverse includes in this
    /// collection.
    /// </returns>
    public IEnumerable<string> GetSearchRevIncludes() => _supportedRevIncludes;

    /// <summary>
    /// Performs a type search in this resource store.
    /// </summary>
    /// <param name="parameters">The search parameters.</param>
    /// <param name="isNestedSearch">If this search has been triggered from inside another search request (for locking)</param>
    /// <returns>
    /// An enumerator that allows foreach to be used to process the search results in this collection.
    /// </returns>
    public IEnumerable<Resource> TypeSearch(IEnumerable<ParsedSearchParameter> parameters, bool isNestedSearch = false)
    {
        Dictionary<string, Resource[]> reverseChainCache = [];

        if (isNestedSearch)
        {
            foreach (T resource in _resourceStore.Values)
            {
                ITypedElement r = resource.ToTypedElement();

                if (_searchTester.TestForMatch(r, parameters, reverseChainCache: reverseChainCache))
                {
                    yield return resource;
                }
            }
        }
        else
        {
            lock (_lockObject)
            {
                foreach (T resource in _resourceStore.Values)
                {
                    ITypedElement r = resource.ToTypedElement();

                    if (_searchTester.TestForMatch(r, parameters))
                    {
                        yield return resource;
                    }
                }
            }
        }
    }

    public bool TestForAny(ParsedSearchParameter link, ParsedSearchParameter filter)
    {
        IEnumerable<T>? source = null;

        switch (filter.Name)
        {
            case "_id":
            case "id":
                {
                    foreach (ParsedSearchParameter.SegmentedReference valueRef in filter.ValueReferences ?? [])
                    {
                        if (!string.IsNullOrEmpty(valueRef.Id))
                        {
                            if (_resourceStore.TryGetValue(valueRef.Id, out T? res))
                            {
                                source = new T[] { res };
                                break;
                            }
                        }

                        if (!string.IsNullOrEmpty(valueRef.Url))
                        {
                            if (_conformanceUrlToId.TryGetValue(valueRef.Url, out string? id) &&
                                _resourceStore.TryGetValue(id, out T? res))
                            {
                                source = new T[] { res };
                                break;
                            }
                        }
                    }

                    foreach (string valueString in filter.Values ?? [])
                    {
                        if (_resourceStore.TryGetValue(valueString, out T? res))
                        {
                            source = new T[] { res };
                            break;
                        }
                        else if (valueString.Contains('/') && _resourceStore.TryGetValue(valueString.Split('/')[1], out res))
                        {
                            source = new T[] { res };
                            break;
                        }
                    }
                }
                break;

            case "identifier":
                {
                    foreach (Hl7.Fhir.ElementModel.Types.Code valueCode in filter.ValueFhirCodes ?? [])
                    {
                        if (_identifierToId.TryGetValue(valueCode.System + "|" + valueCode.Value, out string? id) &&
                            _resourceStore.TryGetValue(id, out T? res))
                        {
                            source = new T[] { res };
                            break;
                        }
                    }
                }
                break;
        }

        if (source == null)
        {
            foreach (T resource in _resourceStore.Values)
            {
                ITypedElement r = resource.ToTypedElement();

                if (_searchTester.TestForMatch(r, [link, filter]))
                {
                    return true;
                }
            }
        }
        else
        {
            foreach (T resource in source)
            {
                ITypedElement r = resource.ToTypedElement();

                if (_searchTester.TestForMatch(r, [link]))
                {
                    return true;
                }
            }
        }


        return false;
    }

    /// <summary>Registers that an instance has been created.</summary>
    /// <param name="resourceId">Identifier for the resource.</param>
    public void RegisterInstanceCreated(string resourceId)
    {
        EventHandler<StoreInstanceEventArgs>? handler = OnInstanceCreated;

        if (handler != null)
        {
            handler(this, new()
            {
                ResourceId = resourceId,
                ResourceType = _resourceName,
            });
        }
    }

    /// <summary>Registers that an instance has been updated.</summary>
    /// <param name="resourceId">Identifier for the resource.</param>
    public void RegisterInstanceUpdated(string resourceId)
    {
        EventHandler<StoreInstanceEventArgs>? handler = OnInstanceUpdated;

        if (handler != null)
        {
            handler(this, new()
            {
                ResourceId = resourceId,
                ResourceType = _resourceName,
            });
        }
    }

    /// <summary>Registers that an instance has been deleted.</summary>
    /// <param name="resourceType">Type of the resource.</param>
    /// <param name="resourceId">  Identifier for the resource.</param>
    public void RegisterInstanceDeleted(string resourceId)
    {
        EventHandler<StoreInstanceEventArgs>? handler = OnInstanceDeleted;

        if (handler != null)
        {
            handler(this, new()
            {
                ResourceId = resourceId,
                ResourceType = _resourceName,
            });
        }
    }

    /// <summary>
    /// Releases the unmanaged resources used by the
    /// FhirModelComparer.Server.Services.FhirManagerService and optionally releases the managed
    /// resources.
    /// </summary>
    /// <param name="disposing">True to release both managed and unmanaged resources; false to
    ///  release only unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_hasDisposed)
        {
            if (disposing)
            {
                // TODO: dispose managed state (managed objects)
            }

            // TODO: free unmanaged resources (unmanaged objects) and override finalizer
            // TODO: set large fields to null
            _hasDisposed = true;
        }
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged
    /// resources.
    /// </summary>
    void IDisposable.Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
