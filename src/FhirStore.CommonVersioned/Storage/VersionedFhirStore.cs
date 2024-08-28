// <copyright file="FhirStore.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using FhirCandle.Search;
using FhirCandle.Models;
using FhirCandle.Operations;
using FhirCandle.Subscriptions;
using FhirCandle.Utils;
using Hl7.Fhir.ElementModel;
using Hl7.Fhir.FhirPath;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Hl7.Fhir.Support;
using Hl7.FhirPath;
using Hl7.FhirPath.Expressions;
using System.Net;
using System.Text.RegularExpressions;
using System.Collections;
using System.Collections.Concurrent;
using static FhirCandle.Search.SearchDefinitions;
using FhirCandle.Serialization;
using System.Xml.Linq;
using FhirCandle.Interactions;
using Hl7.Fhir.Language.Debugging;
using System.Security.AccessControl;
using System.Reflection.Metadata;
using static Hl7.Fhir.Model.VerificationResult;
using System.Linq;
using Hl7.Fhir.Utility;
using System.Diagnostics.CodeAnalysis;

namespace FhirCandle.Storage;

/// <summary>A FHIR store.</summary>
public partial class VersionedFhirStore : IFhirStore
{
    /// <summary>True if has disposed, false if not.</summary>
    private bool _hasDisposed;

    /// <summary>Occurs when On Instance Created.</summary>
    public event EventHandler<StoreInstanceEventArgs>? OnInstanceCreated;

    /// <summary>Occurs when On Instance Updated.</summary>
    public event EventHandler<StoreInstanceEventArgs>? OnInstanceUpdated;

    /// <summary>Occurs when On Instance Deleted.</summary>
    public event EventHandler<StoreInstanceEventArgs>? OnInstanceDeleted;

    /// <summary>Occurs when a Subscription or SubscriptionTopic resource has changed.</summary>
    public event EventHandler<SubscriptionChangedEventArgs>? OnSubscriptionsChanged;

    /// <summary>Occurs when On Changed.</summary>
    public event EventHandler<SubscriptionSendEventArgs>? OnSubscriptionSendEvent;

    /// <summary>Occurs when a received subscription has changed.</summary>
    public event EventHandler<ReceivedSubscriptionChangedEventArgs>? OnReceivedSubscriptionChanged;

    /// <summary>Occurs when On Changed.</summary>
    public event EventHandler<ReceivedSubscriptionEventArgs>? OnReceivedSubscriptionEvent;

    /// <summary>The compiler.</summary>
    private static FhirPathCompiler _compiler = null!;

    /// <summary>The store.</summary>
    private Dictionary<string, IVersionedResourceStore> _store = [];

    /// <summary>The search tester.</summary>
    private SearchTester _searchTester;

    /// <summary>Gets the supported resources.</summary>
    public IEnumerable<string> SupportedResources => _store.Keys.ToArray();

    /// <summary>(Immutable) The cache of compiled search parameter extraction functions.</summary>
    private readonly ConcurrentDictionary<string, CompiledExpression> _compiledSearchParameters = [];

    /// <summary>The sp lock object.</summary>
    private object _spLockObject = new();

    /// <summary>The subscription topic converter.</summary>
    internal static TopicConverter _topicConverter = new();
    
    /// <summary>The subscription converter.</summary>
    internal static SubscriptionConverter _subscriptionConverter = null!;

    /// <summary>(Immutable) The topics, by id.</summary>
    internal readonly ConcurrentDictionary<string, ParsedSubscriptionTopic> _topics = [];

    /// <summary>(Immutable) The subscriptions, by id.</summary>
    internal readonly ConcurrentDictionary<string, ParsedSubscription> _subscriptions = [];

    /// <summary>(Immutable) The FHIRPath variable matcher.</summary>
    [GeneratedRegex("[%][\\w\\-]+", RegexOptions.Compiled)]
    private static partial Regex _fhirpathVarMatcher();

    /// <summary>The configuration.</summary>
    private TenantConfiguration _config = null!;

    /// <summary>True if capabilities are stale.</summary>
    private bool _capabilitiesAreStale = true;

    /// <summary>(Immutable) Identifier for the capability statement.</summary>
    private const string _capabilityStatementId = "metadata";

    /// <summary>The operations supported by this server, by name.</summary>
    private Dictionary<string, IFhirOperation> _operations = [];

    /// <summary>The loaded hooks.</summary>
    private Dictionary<string, string> _hookNamesById = [];

    /// <summary>The system hooks.</summary>
    private Dictionary<string, Dictionary<Common.StoreInteractionCodes, IFhirInteractionHook[]>> _hooksByInteractionByResource = [];

    /// <summary>The loaded directives.</summary>
    private HashSet<string> _loadedDirectives = [];

    /// <summary>List of ids of the loaded packages.</summary>
    private HashSet<string> _loadedPackageIds = [];

    /// <summary>The loaded supplements.</summary>
    private HashSet<string> _loadedSupplements = [];

    /// <summary>Values that represent load state codes.</summary>
    private enum LoadStateCodes
    {
        None,
        Read,
        Process,
    }

    /// <summary>True while the store is loading initial content.</summary>
    private LoadStateCodes _loadState = LoadStateCodes.None;

    /// <summary>Items to reprocess after a load completes.</summary>
    private Dictionary<string, List<object>>? _loadReprocess = null;

    /// <summary>Number of maximum resources.</summary>
    private int _maxResourceCount = 0;

    /// <summary>Queue of identifiers of resources (used for max resource cleaning).</summary>
    private ConcurrentQueue<string> _resourceQ = [];

    /// <summary>The received notifications.</summary>
    private ConcurrentDictionary<string, List<ParsedSubscriptionStatus>> _receivedNotifications = [];

    /// <summary>(Immutable) The received notification window ticks.</summary>
    private static readonly long _receivedNotificationWindowTicks = TimeSpan.FromMinutes(10).Ticks;

    /// <summary>True if this store has protected content.</summary>
    private bool _hasProtected = false;

    /// <summary>List of identifiers for the protected.</summary>
    private HashSet<string> _protectedResources = [];

    /// <summary>The storage capacity timer.</summary>
    private System.Threading.Timer? _capacityMonitor = null;

    /// <summary>The terminology.</summary>
    private StoreTerminologyService _terminology = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="VersionedFhirStore"/> class.
    /// </summary>
    public VersionedFhirStore()
    {
        _searchTester = new() { FhirStore = this, };
    }

    /// <summary>Gets a list of the loaded package directives.</summary>
    public HashSet<string> LoadedPackageDirectives => _loadedDirectives;

    /// <summary>Gets a list of identifiers of the loaded packages.</summary>
    public HashSet<string> LoadedPackageIds => _loadedPackageIds;

    /// <summary>Gets the loaded supplements.</summary>
    public HashSet<string> LoadedSupplements => _loadedSupplements;

    /// <summary>Initializes this object.</summary>
    /// <exception cref="ArgumentNullException">Thrown when one or more required arguments are null.</exception>
    /// <param name="config">The configuration.</param>
    public void Init(TenantConfiguration config)
    {
        if (config == null)
        {
            throw new ArgumentNullException(nameof(config));
        }

        if (string.IsNullOrEmpty(config.ControllerName))
        {
            throw new ArgumentNullException(nameof(config.ControllerName));
        }

        if (string.IsNullOrEmpty(config.BaseUrl))
        {
            throw new ArgumentNullException(nameof(config.BaseUrl));
        }

        _config = config;
        //_baseUri = new Uri(config.ControllerName);

        if (_subscriptionConverter == null!)
        {
            _subscriptionConverter = new(config.MaxSubscriptionExpirationMinutes);
        }

        SymbolTable st = new SymbolTable().AddStandardFP().AddFhirExtensions();
        _compiler = new(st);

        Type rsType = typeof(ResourceStore<>);

        // traverse known resource types to create individual resource stores
        foreach ((string tn, Type t) in ModelInfo.FhirTypeToCsType)
        {
            // skip non-resources
            if (!ModelInfo.IsKnownResource(tn))
            {
                continue;
            }

            // skip resources we do not store (per spec)
            switch (tn)
            {
                case "Parameters":
                case "OperationOutcome":
                case "SubscriptionStatus":
                    continue;
            }

            Type[] tArgs = { t };

            IVersionedResourceStore? irs = (IVersionedResourceStore?)Activator.CreateInstance(
                rsType.MakeGenericType(tArgs),
                this,
                _searchTester,
                _topicConverter,
                _subscriptionConverter);

            if (irs != null)
            {
                _store.Add(tn, irs);
            }
        }

        // create executable versions of known search parameters
        foreach (ModelInfo.SearchParamDefinition spDefinition in ModelInfo.SearchParameters)
        {
            if (spDefinition.Resource != null)
            {
                if (_store.TryGetValue(spDefinition.Resource, out IVersionedResourceStore? rs))
                {
                    rs.SetExecutableSearchParameter(spDefinition);
                }
            }
        }
        
        // check for a load directory
        if (config.LoadDirectory != null)
        {
            _hasProtected = config.ProtectLoadedContent;
            _loadReprocess = new();
            _loadState = LoadStateCodes.Read;

            bool success;

            foreach (FileInfo file in config.LoadDirectory.GetFiles("*.*", SearchOption.AllDirectories))
            {
                switch (file.Extension.ToLowerInvariant())
                {
                    case ".json":
                        {
                            success = TryInstanceUpdate(
                                File.ReadAllText(file.FullName),
                                "application/fhir+json",
                                out _,
                                out _);
                        }
                        break;

                    case ".xml":
                        {
                            success = TryInstanceUpdate(
                                File.ReadAllText(file.FullName),
                                "application/fhir+xml",
                                out _,
                                out _);
                        }
                        break;

                    default:
                        continue;
                }

                if (success)
                {
                    Console.WriteLine($"{config.ControllerName} <<<      loaded: {file.FullName}");
                }
                else
                {
                    Console.WriteLine($"{config.ControllerName} <<< load FAILED: {file.FullName}");
                }
            }

            _loadState = LoadStateCodes.Process;

            // reload any subscriptions in case they loaded before topics
            if (_loadReprocess.Any())
            {
                foreach ((string key, List<object> list) in _loadReprocess)
                {
                    switch (key)
                    {
                        case "Subscription":
                            {
                                foreach (object sub in list)
                                {
                                    _ = StoreProcessSubscription((ParsedSubscription)sub);
                                }
                            }
                            break;
                    }
                }
            }

            _loadState = LoadStateCodes.None;
            _loadReprocess = null;
        }

        CheckLoadedOperations();
        DiscoverInteractionHooks();

        // create a timer to check max resource count if we are monitoring that
        _maxResourceCount = config.MaxResourceCount;
        if (_maxResourceCount > 0)
        {
            _capacityMonitor = new System.Threading.Timer(
                CheckUsage,
                null,
                TimeSpan.Zero,
                TimeSpan.FromSeconds(30));
        }
    }

    /// <summary>Check loaded operations.</summary>
    private void CheckLoadedOperations()
    {
        // load operations for this fhir version
        IEnumerable<Type> operationTypes = System.Reflection.Assembly.GetExecutingAssembly().GetTypes()
            .Where(t => t.GetInterfaces().Contains(typeof(IFhirOperation)));

        foreach (Type opType in operationTypes)
        {
            IFhirOperation? fhirOp = (IFhirOperation?)Activator.CreateInstance(opType);

            if ((fhirOp == null) ||
                (!fhirOp.CanonicalByFhirVersion.ContainsKey(_config.FhirVersion)))
            {
                continue;
            }

            if ((!string.IsNullOrEmpty(fhirOp.RequiresPackage)) &&
                (!_loadedDirectives.Contains(fhirOp.RequiresPackage)) &&
                (!_loadedPackageIds.Contains(fhirOp.RequiresPackage)))
            {
                continue;
            }

            if (!_operations.ContainsKey(fhirOp.OperationName))
            {
                _operations.Add(fhirOp.OperationName, fhirOp);

                try
                {
                    Hl7.Fhir.Model.OperationDefinition? opDef = fhirOp.GetDefinition(_config.FhirVersion);

                    if (opDef != null)
                    {
                        _ = InstanceCreate(new FhirRequestContext(this, "POST", "OperationDefinition", opDef), out _);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading operation definition {fhirOp.OperationName}: {ex.Message}");
                }
            }
        }
    }

    /// <summary>Discover interaction hooks.</summary>
    private void DiscoverInteractionHooks()
    {
        // load hooks for this fhir version
        IEnumerable<Type> hookTypes = System.Reflection.Assembly.GetExecutingAssembly().GetTypes()
            .Where(t => t.GetInterfaces().Contains(typeof(IFhirInteractionHook)));

        foreach (Type hookType in hookTypes)
        {
            IFhirInteractionHook? hook = (IFhirInteractionHook?)Activator.CreateInstance(hookType);

            if ((hook == null) ||
                (hook.SupportedFhirVersions.Any() && !hook.SupportedFhirVersions.Contains(_config.FhirVersion)))
            {
                continue;
            }

            if ((!string.IsNullOrEmpty(hook.RequiresPackage)) &&
                (!_loadedDirectives.Contains(hook.RequiresPackage)) &&
                (!_loadedPackageIds.Contains(hook.RequiresPackage)))
            {
                continue;
            }

            if (_hookNamesById.ContainsKey(hook.Id))
            {
                continue;
            }

            // determine where this hook belongs
            foreach ((string resource, HashSet<Common.StoreInteractionCodes> interactions) in hook.InteractionsByResource)
            {
                if (string.IsNullOrEmpty(resource) || 
                    resource.Equals("*", StringComparison.Ordinal) ||
                    resource.Equals("Resource", StringComparison.Ordinal))
                {
                    // add to all resources - use VersionedFhirStore
                    foreach (string resourceType in _store.Keys)
                    {
                        if (!_hooksByInteractionByResource.ContainsKey(resourceType))
                        {
                            _hooksByInteractionByResource.Add(resourceType, new());
                        }

                        foreach (Common.StoreInteractionCodes interaction in interactions)
                        {
                            if (!_hooksByInteractionByResource[resourceType].ContainsKey(interaction))
                            {
                                _hooksByInteractionByResource[resourceType].Add(interaction, new IFhirInteractionHook[] { hook });
                            }
                            else
                            {
                                _hooksByInteractionByResource[resourceType][interaction] = _hooksByInteractionByResource[resourceType][interaction].Append(hook).ToArray();
                            }
                        }
                    }

                    continue;
                }

                // add to a single resource - use ResourceStore
                if (!_hooksByInteractionByResource.ContainsKey(resource))
                {
                    _hooksByInteractionByResource.Add(resource, new());
                }

                foreach (Common.StoreInteractionCodes interaction in interactions)
                {
                    if (!_hooksByInteractionByResource[resource].ContainsKey(interaction))
                    {
                        _hooksByInteractionByResource[resource].Add(interaction, new IFhirInteractionHook[] { hook });
                    }
                    else
                    {
                        _hooksByInteractionByResource[resource][interaction] = _hooksByInteractionByResource[resource][interaction].Append(hook).ToArray();
                    }
                }
            }

            // log we loaded this hook
            _hookNamesById.Add(hook.Id, hook.Name);
        }
    }

    /// <summary>Loads a package.</summary>
    /// <param name="directive">         The directive.</param>
    /// <param name="directory">         Pathname of the directory.</param>
    /// <param name="packageSupplements">The package supplements.</param>
    /// <param name="includeExamples">    True to include, false to exclude the examples.</param>
    public void LoadPackage(
        string directive, 
        string directory, 
        string packageSupplements, 
        bool includeExamples)
    {
        _loadReprocess = new();
        _loadState = LoadStateCodes.Read;
        
        bool success;

        DirectoryInfo di;

        string fhirVersionSuffix = "." + _config.FhirVersion.ToRLiteral().ToLowerInvariant();

        if ((!string.IsNullOrEmpty(directive)) &&
            (!string.IsNullOrEmpty(directory)))
        {
            _loadedDirectives.Add(directive);

            string packageId = directive.Split('#', '@').First();
            _ = _loadedPackageIds.Add(packageId);
            if (packageId.EndsWith(fhirVersionSuffix))
            {
                _ = _loadedPackageIds.Add(packageId[..^(fhirVersionSuffix.Length)]);
            }

            Console.WriteLine($"Store[{_config.ControllerName}] loading {directive}");

            di = new(directory);
            string libDir = string.Empty;

            // look for an package.json so we can determine examples
            foreach (FileInfo file in di.GetFiles("package.json", SearchOption.AllDirectories))
            {
                try
                {
                    FhirNpmPackageDetails details = FhirNpmPackageDetails.Load(file.FullName);

                    if (details.Directories?.ContainsKey("lib") ?? false)
                    {
                        libDir = details.Directories["lib"];
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Store[{_config.ControllerName}]:{directive} <<< {ex.Message}");
                }
            }

            FileInfo[] files;
            if ((!includeExamples) &&
                (!string.IsNullOrEmpty(libDir)) &&
                Directory.Exists(Path.Combine(directory, libDir)))
            {
                di = new(Path.Combine(directory, libDir));
                files = di.GetFiles("*.*", SearchOption.TopDirectoryOnly);
            }
            else
            {
                files = di.GetFiles("*.*", SearchOption.AllDirectories);
            }

            // traverse all files
            foreach (FileInfo file in files)
            {
                switch (file.Name)
                {
                    // skip
                    case ".index.json":
                    case "package.json":
                        continue;

                    // process normally
                    default:
                        if (file.Name.EndsWith(".openapi.json") ||
                            file.Name.EndsWith(".schema.json"))
                        {
                            continue;
                        }
                        break;
                }

                switch (file.Extension.ToLowerInvariant())
                {
                    case ".json":
                        {
                            success = TryInstanceUpdate(
                                File.ReadAllText(file.FullName),
                                "application/fhir+json",
                                out _,
                                out _);
                        }
                        break;

                    case ".xml":
                        {
                            success = TryInstanceUpdate(
                                File.ReadAllText(file.FullName),
                                "application/fhir+xml",
                                out _,
                                out _);
                        }
                        break;

                    default:
                        continue;
                }

                if (success)
                {
                    Console.WriteLine($"{_config.ControllerName}:{directive} <<<      loaded: {file.FullName}");
                }
                else
                {
                    Console.WriteLine($"{_config.ControllerName}:{directive} <<< load FAILED: {file.FullName}");
                }
            }
        }

        if ((!string.IsNullOrEmpty(packageSupplements)) &&
            Directory.Exists(packageSupplements) &&
            (!_loadedSupplements.Contains(packageSupplements)))
        {
            Console.WriteLine($"Store[{_config.ControllerName}] loading contents from {packageSupplements}");
            _loadedSupplements.Add(packageSupplements);
            di = new(packageSupplements);

            foreach (FileInfo file in di.GetFiles("*.*", SearchOption.AllDirectories))
            {
                switch (file.Extension.ToLowerInvariant())
                {
                    case ".json":
                        {
                            success = TryInstanceUpdate(
                                File.ReadAllText(file.FullName),
                                "application/fhir+json",
                                out _,
                                out _);
                        }
                        break;

                    case ".xml":
                        {
                            success = TryInstanceUpdate(
                                File.ReadAllText(file.FullName),
                                "application/fhir+xml",
                                out _,
                                out _);
                        }
                        break;

                    default:
                        continue;
                }

                if (success)
                {
                    Console.WriteLine($"{_config.ControllerName}:{directive} <<<      loaded: {file.FullName}");
                }
                else
                {
                    Console.WriteLine($"{_config.ControllerName}:{directive} <<< load FAILED: {file.FullName}");
                }
            }
        }

        _loadState = LoadStateCodes.Process;

        // reload any subscriptions in case they loaded before topics
        if (_loadReprocess.Any())
        {
            foreach ((string key, List<object> list) in _loadReprocess)
            {
                switch (key)
                {
                    case "Subscription":
                        {
                            foreach (object sub in list)
                            {
                                _ = StoreProcessSubscription((ParsedSubscription)sub);
                            }
                        }
                        break;
                }
            }
        }

        CheckLoadedOperations();
        DiscoverInteractionHooks();

        _loadState = LoadStateCodes.None;
        _loadReprocess = null;
    }

    /// <summary>Gets the configuration.</summary>
    public TenantConfiguration Config => _config;

    /// <summary>Gets the terminology service for this store.</summary>
    public StoreTerminologyService Terminology => _terminology;

    /// <summary>Supports resource.</summary>
    /// <param name="resourceName">Name of the resource.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    public bool SupportsResource(string resourceName) => _store.ContainsKey(resourceName);

    public bool TryGetResourceInfo(object resource, out string resourceName, out string id)
    {
        if (resource is not Resource r)
        {
            resourceName = string.Empty;
            id = string.Empty;
            return false;
        }

        resourceName = r.TypeName;
        id = r.Id;
        return true;
    }

    /// <summary>
    /// Gets an enumerable collection that contains the keys in the read-only dictionary.
    /// </summary>
    /// <typeparam name="string">        Type of the string.</typeparam>
    /// <typeparam name="IResourceStore">Type of the resource store.</typeparam>
    IEnumerable<string> IReadOnlyDictionary<string, IResourceStore>.Keys => _store.Keys;

    /// <summary>
    /// Gets an enumerable collection that contains the values in the read-only dictionary.
    /// </summary>
    /// <typeparam name="string">        Type of the string.</typeparam>
    /// <typeparam name="IResourceStore">Type of the resource store.</typeparam>
    IEnumerable<IResourceStore> IReadOnlyDictionary<string, IResourceStore>.Values => _store.Values;

    /// <summary>Gets the number of elements in the collection.</summary>
    /// <typeparam name="string">         Type of the string.</typeparam>
    /// <typeparam name="IResourceStore>">Type of the resource store></typeparam>
    int IReadOnlyCollection<KeyValuePair<string, IResourceStore>>.Count => _store.Count;

    /// <summary>Gets the current topics.</summary>
    public IEnumerable<ParsedSubscriptionTopic> CurrentTopics => _topics.Values;

    /// <summary>Gets the current subscriptions.</summary>
    public IEnumerable<ParsedSubscription> CurrentSubscriptions => _subscriptions.Values;

    /// <summary>Gets the received notifications.</summary>
    public ConcurrentDictionary<string, List<ParsedSubscriptionStatus>> ReceivedNotifications => _receivedNotifications;

    /// <summary>Gets the element that has the specified key in the read-only dictionary.</summary>
    /// <typeparam name="string">        Type of the string.</typeparam>
    /// <typeparam name="IResourceStore">Type of the resource store.</typeparam>
    /// <param name="key">The key to locate.</param>
    /// <returns>The element that has the specified key in the read-only dictionary.</returns>
    IResourceStore IReadOnlyDictionary<string, IResourceStore>.this[string key] => _store[key];

    /// <summary>
    /// Determines whether the read-only dictionary contains an element that has the specified key.
    /// </summary>
    /// <typeparam name="string">        Type of the string.</typeparam>
    /// <typeparam name="IResourceStore">Type of the resource store.</typeparam>
    /// <param name="key">The key to locate.</param>
    /// <returns>
    /// <see langword="true" /> if the read-only dictionary contains an element that has the
    /// specified key; otherwise, <see langword="false" />.
    /// </returns>
    bool IReadOnlyDictionary<string, IResourceStore>.ContainsKey(string key) => _store.ContainsKey(key);

    /// <summary>Gets the value that is associated with the specified key.</summary>
    /// <typeparam name="string">        Type of the string.</typeparam>
    /// <typeparam name="IResourceStore">Type of the resource store.</typeparam>
    /// <param name="key">  The key to locate.</param>
    /// <param name="value">[out] When this method returns, the value associated with the specified
    ///  key, if the key is found; otherwise, the default value for the type of the <paramref name="value" />
    ///  parameter. This parameter is passed uninitialized.</param>
    /// <returns>
    /// <see langword="true" /> if the object that implements the <see cref="T:System.Collections.Generic.IReadOnlyDictionary`2" />
    /// interface contains an element that has the specified key; otherwise, <see langword="false" />.
    /// </returns>
    bool IReadOnlyDictionary<string, IResourceStore>.TryGetValue(string key, out IResourceStore value)
    {
        bool result = _store.TryGetValue(key, out IVersionedResourceStore? rStore);
        value = rStore ?? null!;
        return result;
    }

    /// <summary>Returns an enumerator that iterates through the collection.</summary>
    /// <typeparam name="string">         Type of the string.</typeparam>
    /// <typeparam name="IResourceStore>">Type of the resource store></typeparam>
    /// <returns>An enumerator that can be used to iterate through the collection.</returns>
    IEnumerator<KeyValuePair<string, IResourceStore>> IEnumerable<KeyValuePair<string, IResourceStore>>.GetEnumerator() =>
        _store.Select(kvp => new KeyValuePair<string, IResourceStore>(kvp.Key, kvp.Value)).GetEnumerator();

    /// <summary>Returns an enumerator that iterates through a collection.</summary>
    /// <returns>
    /// An <see cref="T:System.Collections.IEnumerator" /> object that can be used to iterate through
    /// the collection.
    /// </returns>
    IEnumerator IEnumerable.GetEnumerator() =>
        _store.Select(kvp => new KeyValuePair<string, IResourceStore>(kvp.Key, kvp.Value)).GetEnumerator();

    /// <summary>Gets a compiled search parameter expression.</summary>
    /// <param name="resourceType">Type of the resource.</param>
    /// <param name="name">        The sp name/code/id.</param>
    /// <param name="expression">  The FHIRPath expression.</param>
    /// <returns>The compiled.</returns>
    public CompiledExpression GetCompiledSearchParameter(string resourceType, string name, string expression)
    {
        string c = resourceType + "." + name;

        lock (_spLockObject)
        {
            if (!_compiledSearchParameters.ContainsKey(c))
            {
                _ = _compiledSearchParameters.TryAdd(c, _compiler.Compile(expression));
            }
        }

        return _compiledSearchParameters[c];
    }

    /// <summary>Check resource usage.</summary>
    private void CheckResourceUsage()
    {
        // check for total resources
        if ((_maxResourceCount == 0) ||
            (_resourceQ.Count <= _maxResourceCount))
        {
            return;
        }

        int numberToRemove = _resourceQ.Count - _maxResourceCount;

        for (int i = 0; i < numberToRemove; i++)
        {
            if (_resourceQ.TryDequeue(out string? id) &&
                (!string.IsNullOrEmpty(id)))
            {
                string[] components = id.Split('/');

                switch (components.Length)
                {
                    // resource and id
                    case 2:
                        {
                            if (_store.ContainsKey(components[0]))
                            {
                                _store[components[0]].InstanceDelete(components[1], _protectedResources);
                            }
                        }
                        break;

                    // TODO: handle versioned resources
                    // resource, id, and version
                    case 3:
                        {
                            if (_store.ContainsKey(components[0]))
                            {
                                _store[components[0]].InstanceDelete(components[1], _protectedResources);
                            }
                        }
                        break;
                }
            }
        }
    }

    /// <summary>Check received notification usage.</summary>
    private void CheckReceivedNotificationUsage()
    {
        // check received notification usage
        if (!_receivedNotifications.Any())
        {
            return;
        }

        List<string> idsToRemove = new();
        long windowTicks = DateTimeOffset.Now.Ticks - _receivedNotificationWindowTicks;

        foreach ((string id, List<ParsedSubscriptionStatus> notifications) in _receivedNotifications)
        {
            if (!notifications.Any())
            {
                idsToRemove.Add(id);
                continue;
            }

            // check oldest notification
            if (notifications.First().ProcessedDateTime.Ticks > windowTicks)
            {
                continue;
            }

            // remove all notifications that are too old
            notifications.RemoveAll(n => n.ProcessedDateTime.Ticks < windowTicks);

            if (notifications.Any())
            {
                RegisterReceivedSubscriptionChanged(id, notifications.Count, false);
            }
        }

        if (idsToRemove.Any())
        {
            foreach (string id in idsToRemove)
            {
                _ = _receivedNotifications.TryRemove(id, out _);
                RegisterReceivedSubscriptionChanged(id, 0, true);
            }
        }
    }

    /// <summary>Check expired subscriptions.</summary>
    private void CheckExpiredSubscriptions()
    {
        if (!_subscriptions.Any())
        {
            return;
        }

        long currentTicks = DateTimeOffset.Now.Ticks;

        HashSet<string> idsToRemove = new();

        // traverse subscriptions to find the ones we need to remove
        foreach (ParsedSubscription sub in _subscriptions.Values)
        {
            if ((sub.ExpirationTicks == -1) ||
                (sub.ExpirationTicks > currentTicks))
            {
                continue;
            }

            idsToRemove.Add(sub.Id);
        }

        // remove the parsed subscription and update the resource to be off
        foreach (string id in idsToRemove)
        {
            // remove the executable version of this subscription
            _ = _subscriptions.TryRemove(id, out _);

            // look for a subscription resource to modify
            if (_store.TryGetValue("Subscription", out IVersionedResourceStore? resourceStore) &&
                resourceStore!.TryGetValue(id, out object? resourceObj) &&
                (resourceObj is Subscription r))
            {
                r.Status = SubscriptionConverter.OffCode;
                resourceStore.InstanceUpdate(
                    r, 
                    false, 
                    string.Empty, 
                    string.Empty, 
                    _protectedResources,
                    out _,
                    out _);
            }
        }
    }

    /// <summary>Check and send heartbeats.</summary>
    /// <param name="state">The state.</param>
    private void CheckUsage(object? state)
    {
        CheckReceivedNotificationUsage();
        CheckResourceUsage();
    }

    /// <summary>Attempts to resolve an ITypedElement from the given string.</summary>
    /// <param name="uri">     URI of the resource.</param>
    /// <param name="resource">[out] The resource.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    public bool TryResolve(string uri, out ITypedElement? resource)
    {
        string[] components = uri.Split('/');

        if (components.Length < 2)
        {
            resource = null;
            return false;
        }

        string resourceType = components[^2];
        string id = components[^1];

        if (!_store.TryGetValue(resourceType, out IVersionedResourceStore? rs))
        {
            resource = null;
            return false;
        }

        Resource? resolved = rs.InstanceRead(id);

        if (resolved == null)
        {
            resource = null;
            return false;
        }

        resource = resolved.ToTypedElement().ToScopedNode();
        return true;
    }


    /// <summary>Attempts to resolve an ITypedElement from the given string.</summary>
    /// <param name="uri">     URI of the resource.</param>
    /// <param name="resource">[out] The resource.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    public bool TryResolveAsResource(string uri, out Resource? resource)
    {
        if (string.IsNullOrEmpty(uri))
        {
            resource = null;
            return false;
        }
        
        string[] components = uri.Split('/');

        if (components.Length < 2)
        {
            resource = null;
            return false;
        }

        string resourceType = components[^2];
        string id = components[^1];

        if (!_store.TryGetValue(resourceType, out IVersionedResourceStore? rs))
        {
            resource = null;
            return false;
        }

        resource = rs.InstanceRead(id);

        if (resource == null)
        {
            return false;
        }

        return true;
    }

    /// <summary>Resolves the given URI into a resource.</summary>
    /// <exception cref="ArgumentException">Thrown when one or more arguments have unsupported or
    ///  illegal values.</exception>
    /// <param name="uri">URI of the resource.</param>
    /// <returns>An ITypedElement.</returns>
    public ITypedElement Resolve(string uri)
    {
        string[] components = uri.Split('/');

        // TODO: handle contained resources
        // TODO: handle bundle-local references

        if (components.Length < 2)
        {
            return null!;
            //throw new ArgumentException("Invalid URI", nameof(uri));
        }

        string resourceType = components[^2];
        string id = components[^1];

        if (!_store.TryGetValue(resourceType, out IVersionedResourceStore? rs))
        {
            return null!;
            //throw new ArgumentException("Invalid URI - unsupported resource type", nameof(uri));
        }

        Resource? resource = rs.InstanceRead(id);

        if (resource == null)
        {
            return null!;
            //throw new ArgumentException("Invalid URI - ID not found", nameof(uri));
        }

        return resource.ToTypedElement().ToScopedNode();
    }

    /// <summary>Performs the interaction specified in the request.</summary>
    /// <param name="ctx">            The request context.</param>
    /// <param name="response">       [out] The response data.</param>
    /// <param name="serializeReturn">(Optional) True to serialize return.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    public bool PerformInteraction(
        FhirRequestContext ctx,
        out FhirResponseContext response,
        bool serializeReturn = true)
    {
        switch (ctx.Interaction)
        {
            case Common.StoreInteractionCodes.InstanceDelete:
                {
                    if (serializeReturn)
                    {
                        return InstanceDelete(ctx, out response);
                    }
                    else
                    {
                        return DoInstanceDelete(ctx, out response);
                    }
                }

            case Common.StoreInteractionCodes.InstanceOperation:
                {
                    if (serializeReturn)
                    {
                        return InstanceOperation(ctx, out response);
                    }
                    else
                    {
                        return DoInstanceOperation(ctx, out response);
                    }
                }

            case Common.StoreInteractionCodes.InstanceRead:
                {
                    if (serializeReturn)
                    {
                        return InstanceRead(ctx, out response);
                    }
                    else
                    {
                        return DoInstanceRead(ctx, out response);
                    }
                }

            case Common.StoreInteractionCodes.InstanceUpdate:
            case Common.StoreInteractionCodes.InstanceUpdateConditional:
                {
                    if (serializeReturn || (ctx.SourceObject == null) || (ctx.SourceObject is not Resource r))
                    {
                        return InstanceUpdate(ctx, out response);
                    }
                    else
                    {
                        return DoInstanceUpdate(ctx, r, out response);
                    }
                }

            case Common.StoreInteractionCodes.TypeCreate:
            case Common.StoreInteractionCodes.TypeCreateConditional:
                {
                    if (serializeReturn || (ctx.SourceObject == null) || (ctx.SourceObject is not Resource r))
                    {
                        return InstanceCreate(ctx, out response);
                    }
                    else
                    {
                        return DoInstanceCreate(ctx, r, out response);
                    }
                }

            case Common.StoreInteractionCodes.TypeDeleteConditional:
            case Common.StoreInteractionCodes.TypeDeleteConditionalSingle:
            case Common.StoreInteractionCodes.TypeDeleteConditionalMultiple:
                {
                    if (serializeReturn)
                    {
                        return TypeDelete(ctx, out response);
                    }
                    else
                    {
                        return DoTypeDelete(ctx, out response);
                    }
                }

            case Common.StoreInteractionCodes.TypeOperation:
                {
                    if (serializeReturn)
                    {
                        return TypeOperation(ctx, out response);
                    }
                    else
                    {
                        return DoTypeOperation(ctx, out response);
                    }
                }

            case Common.StoreInteractionCodes.TypeSearch:
                {
                    if (serializeReturn)
                    {
                        return TypeSearch(ctx, out response);
                    }
                    else
                    {
                        return DoTypeSearch(ctx, out response);
                    }
                }

            case Common.StoreInteractionCodes.SystemCapabilities:
                {
                    if (serializeReturn)
                    {
                        return GetMetadata(ctx, out response);
                    }
                    else
                    {
                        return DoGetMetadata(ctx, out response);
                    }
                }

            case Common.StoreInteractionCodes.SystemBundle:
                {
                    if (serializeReturn || (ctx.SourceObject == null) || (ctx.SourceObject is not Bundle b))
                    {
                        return ProcessBundle(ctx, out response);
                    }
                    else
                    {
                        return DoProcessBundle(ctx, b, out response);
                    }
                }

            case Common.StoreInteractionCodes.SystemDeleteConditional:
                {
                    if (serializeReturn)
                    {
                        return SystemDelete(ctx, out response);
                    }
                    else
                    {
                        return DoSystemDelete(ctx, out response);
                    }
                }

            case Common.StoreInteractionCodes.SystemOperation:
                {
                    if (serializeReturn)
                    {
                        return SystemOperation(ctx, out response);
                    }
                    else
                    {
                        return DoSystemOperation(ctx, out response);
                    }
                }

            case Common.StoreInteractionCodes.SystemSearch:
                {
                    if (serializeReturn)
                    {
                        return SystemSearch(ctx, out response);
                    }
                    else
                    {
                        return DoSystemSearch(ctx, out response);
                    }
                }

            case Common.StoreInteractionCodes.CompartmentOperation:
            case Common.StoreInteractionCodes.CompartmentSearch:
            case Common.StoreInteractionCodes.CompartmentTypeSearch:
            case Common.StoreInteractionCodes.InstanceDeleteHistory:
            case Common.StoreInteractionCodes.InstanceDeleteVersion:
            case Common.StoreInteractionCodes.InstancePatch:
            case Common.StoreInteractionCodes.InstancePatchConditional:
            case Common.StoreInteractionCodes.InstanceReadHistory:
            case Common.StoreInteractionCodes.InstanceReadVersion:
            case Common.StoreInteractionCodes.TypeHistory:
            case Common.StoreInteractionCodes.SystemHistory:
            default:
                response = new()
                {
                    Outcome = SerializationUtils.BuildOutcomeForRequest(
                        HttpStatusCode.NotImplemented,
                        $"Interaction not implemented: {ctx.Interaction}",
                        OperationOutcome.IssueType.NotSupported),
                    StatusCode = HttpStatusCode.NotImplemented,
                };
                return false;
        }
    }

    /// <summary>Gets the hooks.</summary>
    /// <param name="resourceType">Type of the resource.</param>
    /// <param name="interaction"> The interaction.</param>
    /// <returns>An array of i FHIR interaction hook.</returns>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private IEnumerable<IFhirInteractionHook> GetHooks(string resourceType, Common.StoreInteractionCodes interaction)
    {
        if (!_hooksByInteractionByResource.TryGetValue(resourceType, out Dictionary<Common.StoreInteractionCodes, IFhirInteractionHook[]>? hooksByInteraction))
        {
            return [];
        }

        if (!hooksByInteraction.TryGetValue(interaction, out IFhirInteractionHook[]? hooks))
        {
            return [];
        }

        return hooks;
    }

    /// <summary>Gets the hooks.</summary>
    /// <param name="resourceType">Type of the resource.</param>
    /// <param name="interactions">The interactions.</param>
    /// <returns>An array of i FHIR interaction hook.</returns>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private IEnumerable<IFhirInteractionHook> GetHooks(string resourceType, IEnumerable<Common.StoreInteractionCodes> interactions)
    {
        if (!_hooksByInteractionByResource.TryGetValue(resourceType, out Dictionary<Common.StoreInteractionCodes, IFhirInteractionHook[]>? hooksByInteraction))
        {
            return [];
        }

        List<IFhirInteractionHook[]> collector = new();

        foreach (Common.StoreInteractionCodes interaction in interactions)
        {
            if (!hooksByInteraction.TryGetValue(interaction, out IFhirInteractionHook[]? hooks))
            {
                continue;
            }

            collector.Add(hooks);
        }

        return collector.SelectMany(v => v);
    }

    /// <summary>Instance create.</summary>
    /// <param name="ctx">     The request context.</param>
    /// <param name="response">[out] The response data.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    public bool InstanceCreate(
        FhirRequestContext ctx,
        out FhirResponseContext response)
    {
        Resource? r;

        if (ctx.SourceObject is Resource resource)
        {
            r = resource;
        }
        else
        {
            HttpStatusCode sc = SerializationUtils.TryDeserializeFhir(
                ctx.SourceContent,
                ctx.SourceFormat,
                out r,
                out string exMessage);

            if ((!sc.IsSuccessful()) || (r == null))
            {
                OperationOutcome outcome = SerializationUtils.BuildOutcomeForRequest(
                    sc,
                    $"Failed to deserialize resource, format: {ctx.SourceFormat}, error: {exMessage}",
                    OperationOutcome.IssueType.Structure);

                response = new()
                {
                    Outcome = outcome,
                    SerializedOutcome = SerializationUtils.SerializeFhir(outcome, ctx.DestinationFormat, ctx.SerializePretty),
                    StatusCode = sc,
                };

                return false;
            }
        }

        bool success = DoInstanceCreate(
            ctx,
            r!,
            out response);

        string sr = response.Resource == null ? string.Empty : SerializationUtils.SerializeFhir((Resource)response.Resource, ctx.DestinationFormat, ctx.SerializePretty);
        string so = response.Outcome == null ? string.Empty : SerializationUtils.SerializeFhir((Resource)response.Outcome, ctx.DestinationFormat, ctx.SerializePretty);

        response = response with
        {
            MimeType = ctx.DestinationFormat,
            SerializedResource = sr,
            SerializedOutcome = so,
        };

        return success;
    }

    /// <summary>Executes the instance create operation.</summary>
    /// <param name="ctx">     The request context.</param>
    /// <param name="content"> The content.</param>
    /// <param name="response">[out] The response data.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    internal bool DoInstanceCreate(
        FhirRequestContext ctx,
        Resource content,
        out FhirResponseContext response)
    {
        string resourceType = string.IsNullOrEmpty(ctx.ResourceType) ? content.TypeName : ctx.ResourceType;

        if (content.TypeName != resourceType)
        {
            response = new()
            {
                Outcome = SerializationUtils.BuildOutcomeForRequest(
                    HttpStatusCode.UnprocessableEntity,
                    $"Resource type: {content.TypeName} does not match request: {resourceType}",
                    OperationOutcome.IssueType.Invalid),
                StatusCode = HttpStatusCode.UnprocessableEntity,
            };
            return false;
        }

        if (!_store.ContainsKey(resourceType))
        {
            response = new()
            {
                Outcome = SerializationUtils.BuildOutcomeForRequest(
                    HttpStatusCode.NotFound,
                    $"Resource type: {resourceType} is not supported",
                    OperationOutcome.IssueType.NotSupported),
                StatusCode = HttpStatusCode.NotFound,
            };
            return false;
        }

        IEnumerable<IFhirInteractionHook> hooks = GetHooks(
            resourceType,
            string.IsNullOrEmpty(ctx.IfNoneExist) ? Common.StoreInteractionCodes.TypeCreate : Common.StoreInteractionCodes.TypeCreateConditional);

        foreach (IFhirInteractionHook hook in hooks ?? [])
        {
            if (hook.HookRequestStates.Contains(Common.HookRequestStateCodes.Pre))
            {
                _ = hook.DoInteractionHook(
                        ctx,
                        this,
                        _store[resourceType],
                        content,
                        out FhirResponseContext hr);

                // check for the hook indicating processing is complete
                if (hr.StatusCode != null)
                {
                    response = hr;
                    return true;
                }

                // if the hook modified the resource, use that moving forward
                if (hr.Resource != null)
                {
                    content = (Resource)hr.Resource;
                }
            }
        }

        // check for conditional create
        if (!string.IsNullOrEmpty(ctx.IfNoneExist))
        {
            bool success = DoTypeSearch(
                ctx with { UrlQuery = ctx.IfNoneExist },
                out FhirResponseContext searchResp);

            if (success &&
                (searchResp.Resource != null) &&
                (searchResp.Resource is Bundle bundle))
            {
                switch (bundle?.Total)
                {
                    // no matches - continue with store as normal
                    case 0:
                        break;

                    // one match - return the match as if just stored except with OK instead of Created
                    case 1:
                        {
                            Resource r = bundle.Entry[0].Resource;

                            response = new()
                            {
                                Resource = r,
                                ResourceType = r.TypeName,
                                Id = r.Id,
                                ETag = string.IsNullOrEmpty(r.Meta?.VersionId) ? string.Empty : $"W/\"{r.Meta.VersionId}\"",
                                LastModified = r.Meta?.LastUpdated == null ? string.Empty : r.Meta.LastUpdated.Value.UtcDateTime.ToString("r"),
                                Location = $"{_config.BaseUrl}/{resourceType}/{r.Id}",
                                Outcome = SerializationUtils.BuildOutcomeForRequest(
                                    HttpStatusCode.OK,
                                    $"Created {resourceType}/{r.Id}"),
                                StatusCode = HttpStatusCode.OK,
                            };
                            return true;
                        }

                    // multiple matches - fail the request
                    default:
                        {
                            response = new()
                            {
                                Outcome = SerializationUtils.BuildOutcomeForRequest(
                                    HttpStatusCode.PreconditionFailed,
                                    $"If-None-Exist query returned too many matches: {bundle?.Total}"),
                                StatusCode = HttpStatusCode.PreconditionFailed,
                            };
                            return false;
                        }
                }
            }
            else
            {
                response = new()
                {
                    Outcome = searchResp.Outcome ?? SerializationUtils.BuildOutcomeForRequest(
                        HttpStatusCode.PreconditionFailed,
                        $"If-None-Exist search failed: {ctx.IfNoneExist}"),
                    StatusCode = HttpStatusCode.PreconditionFailed,
                };
                return false;
            }
        }

        // create the resource
        Resource? stored = _store[resourceType].InstanceCreate(ctx, content, _config.AllowExistingId);

        Resource? sForHook = null;

        foreach (IFhirInteractionHook hook in hooks ?? [])
        {
            sForHook ??= (Resource?)stored?.DeepCopy();
            
            if (hook.HookRequestStates.Contains(Common.HookRequestStateCodes.Post))
            {
                _ = hook.DoInteractionHook(
                        ctx,
                        this,
                        _store[resourceType],
                        sForHook,
                        out FhirResponseContext hr);

                // check for the hook indicating processing is complete
                if (hr.StatusCode != null)
                {
                    response = hr;
                    return true;
                }

                // if the hook modified the resource, use that moving forward
                if (hr.Resource != null)
                {
                    sForHook = (Resource)hr.Resource;
                    stored = sForHook;
                }
            }
        }

        if (stored == null)
        {
            response = new()
            {
                Outcome = SerializationUtils.BuildOutcomeForRequest(
                    HttpStatusCode.InternalServerError,
                    "Failed to create resource"),
                StatusCode = HttpStatusCode.InternalServerError,
            };
            return false;
        }

        if ((_loadState != LoadStateCodes.None) && _hasProtected)
        {
            _protectedResources.Add(resourceType + "/" + stored.Id);
        }
        else if (_maxResourceCount != 0)
        {
            _resourceQ.Enqueue(resourceType + "/" + stored.Id + "/" + stored.Meta.VersionId);
        }

        response = new()
        {
            Resource = stored,
            ResourceType = stored.TypeName,
            Id = stored.Id,
            ETag = string.IsNullOrEmpty(stored.Meta?.VersionId) ? string.Empty : $"W/\"{stored.Meta.VersionId}\"",
            LastModified = stored.Meta?.LastUpdated == null ? string.Empty : stored.Meta.LastUpdated.Value.UtcDateTime.ToString("r"),
            Location = $"{_config.BaseUrl}/{resourceType}/{stored.Id}",
            Outcome = SerializationUtils.BuildOutcomeForRequest(
                HttpStatusCode.Created,
                $"Created {resourceType}/{stored.Id}"),
            StatusCode = HttpStatusCode.Created,
        };
        return true;
    }

    /// <summary>Process a Batch or Transaction bundle.</summary>
    /// <param name="ctx">     The request context.</param>
    /// <param name="response">[out] The response data.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    public bool ProcessBundle(
        FhirRequestContext ctx,
        out FhirResponseContext response)
    {
        const string resourceType = "Bundle";

        Resource? r;

        if (ctx.SourceObject is Resource resource)
        {
            r = resource;
        }
        else
        {
            HttpStatusCode sc = SerializationUtils.TryDeserializeFhir(
                ctx.SourceContent,
                ctx.SourceFormat,
                out r,
                out string exMessage);

            if ((!sc.IsSuccessful()) || (r == null))
            {
                OperationOutcome outcome = SerializationUtils.BuildOutcomeForRequest(
                    sc,
                    $"Failed to deserialize resource, format: {ctx.SourceFormat}, error: {exMessage}",
                    OperationOutcome.IssueType.Structure);

                response = new()
                {
                    Outcome = outcome,
                    SerializedOutcome = SerializationUtils.SerializeFhir(outcome, ctx.DestinationFormat, ctx.SerializePretty),
                    StatusCode = sc,
                };

                return false;
            }
        }

        if ((r!.TypeName != resourceType) ||
            (r is not Bundle requestBundle))
        {
            OperationOutcome outcome = SerializationUtils.BuildOutcomeForRequest(
                HttpStatusCode.UnprocessableEntity,
                $"Cannot process non-Bundle resource type ({r.TypeName}) as a Bundle",
                OperationOutcome.IssueType.Invalid);

            response = new()
            {
                Outcome = outcome,
                SerializedOutcome = SerializationUtils.SerializeFhir(outcome, ctx.DestinationFormat, ctx.SerializePretty),
                StatusCode = HttpStatusCode.UnprocessableEntity,
            };

            return false;
        }

        bool success = DoProcessBundle(
            ctx,
            requestBundle,
            out response);

        string sr = response.Resource == null ? string.Empty : SerializationUtils.SerializeFhir((Resource)response.Resource, ctx.DestinationFormat, ctx.SerializePretty);
        string so = response.Outcome == null ? string.Empty : SerializationUtils.SerializeFhir((Resource)response.Outcome, ctx.DestinationFormat, ctx.SerializePretty);

        response = response with
        {
            MimeType = ctx.DestinationFormat,
            SerializedResource = sr,
            SerializedOutcome = so,
        };

        return success;
    }

    /// <summary>Executes the process bundle operation.</summary>
    /// <param name="ctx">          The request context.</param>
    /// <param name="requestBundle">The request bundle.</param>
    /// <param name="response">     [out] The response data.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    internal bool DoProcessBundle(
        FhirRequestContext ctx,
        Bundle requestBundle,
        out FhirResponseContext response)
    {
        Bundle responseBundle = new Bundle()
        {
            Id = Guid.NewGuid().ToString(),
        };

        switch (requestBundle.Type)
        {
            // case Bundle.BundleType.Transaction:
            //    responseBundle.Type = Bundle.BundleType.TransactionResponse;
            //    ProcessTransaction(requestBundle, responseBundle);
            //    break;

            case Bundle.BundleType.Batch:
                responseBundle.Type = Bundle.BundleType.BatchResponse;
                ProcessBatch(ctx, requestBundle, responseBundle);
                break;

            default:
                {
                    OperationOutcome outcome = SerializationUtils.BuildOutcomeForRequest(
                        HttpStatusCode.UnprocessableEntity,
                        $"Unsupported Bundle process request! Type: {requestBundle.Type}",
                        OperationOutcome.IssueType.NotSupported);

                    response = new()
                    {
                        Outcome = outcome,
                        SerializedOutcome = SerializationUtils.SerializeFhir(outcome, ctx.DestinationFormat, ctx.SerializePretty),
                        StatusCode = HttpStatusCode.UnprocessableEntity,
                    };

                    return false;
                }
        }

        response = new()
        {
            Resource = responseBundle,
            Outcome = SerializationUtils.BuildOutcomeForRequest(HttpStatusCode.OK, $"Processed {requestBundle.Type} bundle"),
            StatusCode = HttpStatusCode.OK,
        };

        return true;
    }

    /// <summary>Instance delete.</summary>
    /// <param name="ctx">     The request context.</param>
    /// <param name="response">[out] The response data.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    public bool InstanceDelete(
        FhirRequestContext ctx,
        out FhirResponseContext response)
    {
        bool success = DoInstanceDelete(
            ctx,
            out response);

        string sr = response.Resource == null ? string.Empty : SerializationUtils.SerializeFhir((Resource)response.Resource, ctx.DestinationFormat, ctx.SerializePretty);
        string so = response.Outcome == null ? string.Empty : SerializationUtils.SerializeFhir((Resource)response.Outcome, ctx.DestinationFormat, ctx.SerializePretty);

        response = response with
        {
            MimeType = ctx.DestinationFormat,
            SerializedResource = sr,
            SerializedOutcome = so,
        };

        return success;
    }

    /// <summary>Executes the instance delete operation.</summary>
    /// <param name="ctx">     The request context.</param>
    /// <param name="response">[out] The response data.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    internal bool DoInstanceDelete(
        FhirRequestContext ctx,
        out FhirResponseContext response)
    {
        if (!_store.ContainsKey(ctx.ResourceType))
        {
            response = new()
            {
                Outcome = SerializationUtils.BuildOutcomeForRequest(
                    HttpStatusCode.NotFound,
                    $"Resource type: {ctx.ResourceType} is not supported",
                    OperationOutcome.IssueType.NotSupported),
                StatusCode = HttpStatusCode.NotFound,
            };
            return false;
        }

        IEnumerable<IFhirInteractionHook> hooks = GetHooks(ctx.ResourceType, Common.StoreInteractionCodes.InstanceDelete);

        foreach (IFhirInteractionHook hook in hooks ?? [])
        {
            if (hook.HookRequestStates.Contains(Common.HookRequestStateCodes.Pre))
            {
                _ = hook.DoInteractionHook(
                        ctx,
                        this,
                        _store[ctx.ResourceType],
                        null,
                        out FhirResponseContext hr);

                // check for the hook indicating processing is complete
                if (hr.StatusCode != null)
                {
                    response = hr;
                    return true;
                }
            }
        }

        // attempt delete
        Resource? resource = _store[ctx.ResourceType].InstanceDelete(ctx.Id, _protectedResources);

        Resource? sForHook = null;

        foreach (IFhirInteractionHook hook in hooks ?? [])
        {
            sForHook ??= (Resource?)resource?.DeepCopy();
            
            if (hook.HookRequestStates.Contains(Common.HookRequestStateCodes.Post))
            {
                _ = hook.DoInteractionHook(
                        ctx,
                        this,
                        _store[ctx.ResourceType],
                        sForHook,
                        out FhirResponseContext hr);

                // check for the hook indicating processing is complete
                if (hr.StatusCode != null)
                {
                    response = hr;
                    return true;
                }

                // if the hook modified the resource, use that moving forward
                if (hr.Resource != null)
                {
                    sForHook = (Resource)hr.Resource;
                    resource = sForHook;
                }
            }
        }

        if (resource == null)
        {
            response = new()
            {
                Outcome = SerializationUtils.BuildOutcomeForRequest(
                    HttpStatusCode.NotFound,
                    $"Resource {ctx.ResourceType}/{ctx.Id} not found"),
                StatusCode = HttpStatusCode.NotFound,
            };
            return false;
        }

        response = new()
        {
            Resource = resource,
            ResourceType = resource.TypeName,
            Id = resource.Id,
            Outcome = SerializationUtils.BuildOutcomeForRequest(HttpStatusCode.OK, $"Deleted {ctx.ResourceType}/{ctx.Id}"),
            StatusCode = HttpStatusCode.OK,
        };
        return true;
    }

    /// <summary>Attempts to read with minimal processing (e.g., no Hooks are called).</summary>
    /// <param name="resourceType">Type of the resource.</param>
    /// <param name="id">          [out] The identifier.</param>
    /// <param name="resource">    [out] The resource.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    public bool TryInstanceRead(string resourceType, string id, out object? resource)
    {
        if ((!_store.ContainsKey(resourceType)) ||
            (!((IReadOnlyDictionary<string, Hl7.Fhir.Model.Resource>)_store[resourceType]).ContainsKey(id)))
        {
            resource = null;
            return false;
        }

        resource = ((IReadOnlyDictionary<string, Hl7.Fhir.Model.Resource>)_store[resourceType])[id].DeepCopy();
        return true;
    }

    /// <summary>Instance read.</summary>
    /// <param name="ctx">     The request context.</param>
    /// <param name="response">[out] The response and related information</param>
    /// <returns>A HttpStatusCode.</returns>
    public bool InstanceRead(
        FhirRequestContext ctx,
        out FhirResponseContext response)
    {
        bool success = DoInstanceRead(ctx, out response);

        string sr = response.Resource == null ? string.Empty : SerializationUtils.SerializeFhir((Resource)response.Resource, ctx.DestinationFormat, ctx.SerializePretty);
        string so = response.Outcome == null ? string.Empty : SerializationUtils.SerializeFhir((Resource)response.Outcome, ctx.DestinationFormat, ctx.SerializePretty);

        response = response with
        {
            MimeType = ctx.DestinationFormat,
            SerializedResource = sr,
            SerializedOutcome = so,
        };

        return success;
    }

    /// <summary>Executes the instance read operation.</summary>
    /// <param name="ctx">     The request context.</param>
    /// <param name="response">[out] The response data.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    internal bool DoInstanceRead(
        FhirRequestContext ctx,
        out FhirResponseContext response)
    {
        if (string.IsNullOrEmpty(ctx.ResourceType))
        {
            response = new()
            {
                Outcome = SerializationUtils.BuildOutcomeForRequest(
                    HttpStatusCode.BadRequest,
                    "Resource type is required",
                    OperationOutcome.IssueType.Structure),
                StatusCode = HttpStatusCode.BadRequest,
            };
            return false;
        }

        if (!_store.ContainsKey(ctx.ResourceType))
        {
            response = new()
            {
                Outcome = SerializationUtils.BuildOutcomeForRequest(
                    HttpStatusCode.NotFound,
                    $"Resource type: {ctx.ResourceType} is not supported",
                    OperationOutcome.IssueType.NotSupported),
                StatusCode = HttpStatusCode.NotFound,
            };
            return false;
        }

        if (string.IsNullOrEmpty(ctx.Id))
        {
            response = new()
            {
                Outcome = SerializationUtils.BuildOutcomeForRequest(
                    HttpStatusCode.BadRequest,
                    "ID required for instance level read.",
                    OperationOutcome.IssueType.Structure),
                StatusCode = HttpStatusCode.BadRequest,
            };
            return false;
        }

        IEnumerable<IFhirInteractionHook> hooks = GetHooks(ctx.ResourceType, Common.StoreInteractionCodes.InstanceRead);

        foreach (IFhirInteractionHook hook in hooks ?? [])
        {
            if (hook.HookRequestStates.Contains(Common.HookRequestStateCodes.Pre))
            {
                _ = hook.DoInteractionHook(
                        ctx,
                        this,
                        _store[ctx.ResourceType],
                        null,
                        out FhirResponseContext hr);

                // check for the hook indicating processing is complete
                if (hr.StatusCode != null)
                {
                    response = hr;
                    return true;
                }
            }
        }

        Resource? r = _store[ctx.ResourceType].InstanceRead(ctx.Id);

        Resource? sForHook = null;

        foreach (IFhirInteractionHook hook in hooks ?? [])
        {
            sForHook ??= (Resource?)r?.DeepCopy();

            if (hook.HookRequestStates.Contains(Common.HookRequestStateCodes.Post))
            {
                _ = hook.DoInteractionHook(
                        ctx,
                        this,
                        _store[ctx.ResourceType],
                        sForHook,
                        out FhirResponseContext hr);

                if (hr.StatusCode != null)
                {
                    response = hr;
                    return true;
                }

                if (hr.Resource != null)
                {
                    sForHook = (Resource?)hr.Resource;
                    r = sForHook;
                }
            }
        }

        if (r == null)
        {
            response = new()
            {
                Outcome = SerializationUtils.BuildOutcomeForRequest(
                    HttpStatusCode.NotFound,
                    $"Resource: {ctx.ResourceType}/{ctx.Id} not found",
                    OperationOutcome.IssueType.Exception),
                StatusCode = HttpStatusCode.NotFound,
            };

            return false;
        }

        string eTag = string.IsNullOrEmpty(r.Meta?.VersionId) ? string.Empty : $"W/\"{r.Meta.VersionId}\"";

        if ((!string.IsNullOrEmpty(ctx.IfMatch)) &&
            (!eTag.Equals(ctx.IfMatch, StringComparison.Ordinal)))
        {
            response = new()
            {
                Outcome = SerializationUtils.BuildOutcomeForRequest(
                    HttpStatusCode.PreconditionFailed,
                    $"If-Match: {ctx.IfMatch} does not equal found eTag: {eTag}",
                    OperationOutcome.IssueType.BusinessRule),
                StatusCode = HttpStatusCode.PreconditionFailed,
            };

            return false;
        }

        string lastModified = r.Meta?.LastUpdated == null ? string.Empty : r.Meta.LastUpdated.Value.UtcDateTime.ToString("r");

        if ((!string.IsNullOrEmpty(ctx.IfModifiedSince)) &&
            (String.Compare(lastModified, ctx.IfModifiedSince, StringComparison.Ordinal) < 0))
        {
            response = new()
            {
                Outcome = SerializationUtils.BuildOutcomeForRequest(
                    HttpStatusCode.NotModified,
                    $"Last modified: {lastModified} is prior to If-Modified-Since: {ctx.IfModifiedSince}",
                    OperationOutcome.IssueType.Informational),
                ETag = eTag,
                LastModified = lastModified,
                StatusCode = HttpStatusCode.NotModified,
            };

            return true;
        }

        if (ctx.IfNoneMatch.Equals("*", StringComparison.Ordinal))
        {
            response = new()
            {
                Outcome = SerializationUtils.BuildOutcomeForRequest(
                    HttpStatusCode.PreconditionFailed,
                    "Prior version exists, but If-None-Match is *"),
                StatusCode = HttpStatusCode.PreconditionFailed,
            };

            return false;
        }

        if (!string.IsNullOrEmpty(ctx.IfNoneMatch))
        {
            if (ctx.IfNoneMatch.Equals(eTag, StringComparison.Ordinal))
            {
                response = new()
                {
                    Outcome = SerializationUtils.BuildOutcomeForRequest(
                        HttpStatusCode.NotModified,
                        $"Read {ctx.ResourceType}/{ctx.Id} found version: {eTag}, equals If-None-Match: {ctx.IfNoneMatch}"),
                    StatusCode = HttpStatusCode.NotModified,
                };

                return false;

            }
        }

        response = new()
        {
            Resource = r,
            ResourceType = r.TypeName,
            Id = r.Id,
            ETag = eTag,
            LastModified = lastModified,
            Location = string.IsNullOrEmpty(r.Id) ? string.Empty : $"{_config.BaseUrl}/{r.TypeName}/{r.Id}",
            Outcome = SerializationUtils.BuildOutcomeForRequest(HttpStatusCode.OK, $"Read {r.TypeName}/{r.Id}"),
            StatusCode = HttpStatusCode.OK,
        };

        return true;
    }

    /// <summary>Attempts to update.</summary>
    /// <param name="content">     The content.</param>
    /// <param name="mimeType">    Type of the mime.</param>
    /// <param name="resourceType">[out] Type of the resource.</param>
    /// <param name="id">          [out] The identifier.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    public bool TryInstanceUpdate(
        string content,
        string mimeType,
        out string resourceType,
        out string id)
    {
        HttpStatusCode sc = SerializationUtils.TryDeserializeFhir(
            content,
            mimeType,
            out Resource? r,
            out _,
            _loadState == LoadStateCodes.Read);

        if ((!sc.IsSuccessful()) || (r == null))
        {
            resourceType = string.Empty;
            id = string.Empty;
            return false;
        }

        return TryInstanceUpdate(r, out resourceType, out id);
    }


    /// <summary>Attempts to update.</summary>
    /// <param name="resource">    The resource.</param>
    /// <param name="resourceType">[out] Type of the resource.</param>
    /// <param name="id">          [out] The identifier.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    public bool TryInstanceUpdate(
        object? resource,
        out string resourceType,
        out string id)
    {
        if (resource is not Hl7.Fhir.Model.Resource r)
        {
            resourceType = string.Empty;
            id = string.Empty;
            return false;
        }

        resourceType = r.TypeName;

        if (string.IsNullOrEmpty(r.Id))
        {
            r.Id = Guid.NewGuid().ToString();
        }

        id = r.Id;

        if (!_store.ContainsKey(resourceType))
        {
            return false;
        }

        if ((!_config.AllowCreateAsUpdate) &&
            (!((IReadOnlyDictionary<string, Hl7.Fhir.Model.Resource>)_store[resourceType]).ContainsKey(id)))
        {
            return false;
        }

        FhirRequestContext ctx = new()
        {
            TenantName = _config.ControllerName,
            Store = this,
            HttpMethod = "PUT",
            Url = _config.BaseUrl + "/" + resourceType + "/" + id,
            UrlPath = resourceType + "/" + id,
            Authorization = null,
            Interaction = Common.StoreInteractionCodes.InstanceUpdate,
            ResourceType = resourceType,
            Id = id,
        };

        bool success = DoInstanceUpdate(
            ctx,
            r,
            out _);

        return success;
    }

    /// <summary>Instance update.</summary>
    /// <param name="ctx">     The request context.</param>
    /// <param name="response">[out] The response data.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    public bool InstanceUpdate(
        FhirRequestContext ctx,
        out FhirResponseContext response)
    {
        Resource? r;

        if ((ctx.SourceObject != null) &&
            (ctx.SourceObject is Resource))
        {
            r = ctx.SourceObject as Resource;
        }
        else
        {
            HttpStatusCode sc = SerializationUtils.TryDeserializeFhir(
                ctx.SourceContent,
                ctx.SourceFormat,
                out r,
                out string exMessage);

            if ((!sc.IsSuccessful()) || (r == null))
            {
                OperationOutcome outcome = SerializationUtils.BuildOutcomeForRequest(
                    sc,
                    $"Failed to deserialize resource, format: {ctx.SourceFormat}, error: {exMessage}",
                    OperationOutcome.IssueType.Structure);

                response = new()
                {
                    Outcome = outcome,
                    SerializedOutcome = SerializationUtils.SerializeFhir(outcome, ctx.DestinationFormat, ctx.SerializePretty),
                    StatusCode = sc,
                };

                return false;
            }
        }

        if (r == null)
        {
            response = new()
            {
                Outcome = SerializationUtils.BuildOutcomeForRequest(
                    HttpStatusCode.BadRequest,
                    "Resource is required",
                    OperationOutcome.IssueType.Structure),
                StatusCode = HttpStatusCode.BadRequest,
            };
            return false;
        }

        bool success = DoInstanceUpdate(
            ctx,
            r,
            out response);

        string sr = response.Resource == null ? string.Empty : SerializationUtils.SerializeFhir((Resource)response.Resource, ctx.DestinationFormat, ctx.SerializePretty);
        string so = response.Outcome == null ? string.Empty : SerializationUtils.SerializeFhir((Resource)response.Outcome, ctx.DestinationFormat, ctx.SerializePretty);

        response = response with
        {
            MimeType = ctx.DestinationFormat,
            SerializedResource = sr,
            SerializedOutcome = so,
        };

        return success;
    }

    /// <summary>Executes the instance update operation.</summary>
    /// <param name="ctx">     The request context.</param>
    /// <param name="content"> The content.</param>
    /// <param name="response">[out] The response data.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    internal bool DoInstanceUpdate(
        FhirRequestContext ctx,
        Resource content,
        out FhirResponseContext response)
    {
        string resourceType = string.IsNullOrEmpty(ctx.ResourceType) ? content.TypeName : ctx.ResourceType;
        string id = ctx.Id;

        if (_loadState == LoadStateCodes.Read)
        {
            // allow empty ids during load
            if (string.IsNullOrEmpty(id))
            {
                id = content.Id;
            }
        }

        if (content.TypeName != resourceType)
        {
            response = new()
            {
                Outcome = SerializationUtils.BuildOutcomeForRequest(
                    HttpStatusCode.UnprocessableEntity,
                    $"Resource type: {content.TypeName} does not match request: {resourceType}",
                    OperationOutcome.IssueType.Invalid),
                StatusCode = HttpStatusCode.UnprocessableEntity,
            };
            return false;
        }

        if (!_store.ContainsKey(resourceType))
        {
            response = new()
            {
                Outcome = SerializationUtils.BuildOutcomeForRequest(
                    HttpStatusCode.NotFound,
                    $"Resource type: {resourceType} is not supported",
                    OperationOutcome.IssueType.NotSupported),
                StatusCode = HttpStatusCode.NotFound,
            };
            return false;
        }

        HttpStatusCode sc;

        IEnumerable<IFhirInteractionHook> hooks = GetHooks(
            ctx.ResourceType,
            string.IsNullOrEmpty(ctx.UrlQuery) ? Common.StoreInteractionCodes.InstanceUpdate : Common.StoreInteractionCodes.InstanceUpdateConditional);

        foreach (IFhirInteractionHook hook in hooks ?? [])
        {
            if (hook.HookRequestStates.Contains(Common.HookRequestStateCodes.Pre))
            {
                _ = hook.DoInteractionHook(
                        ctx,
                        this,
                        _store[ctx.ResourceType],
                        content,
                        out FhirResponseContext hr);

                // check for the hook indicating processing is complete
                if (hr.StatusCode != null)
                {
                    response = hr;
                    return true;
                }

                // if the hook modified the resource, use that moving forward
                if (hr.Resource != null)
                {
                    content = (Resource)hr.Resource;
                }
            }
        }

        OperationOutcome outcome;

        // check for conditional update
        if (!string.IsNullOrEmpty(ctx.UrlQuery))
        {
            bool success = DoTypeSearch(
                ctx,
                out FhirResponseContext searchResp);

            if (success &&
                (searchResp.Resource != null) &&
                (searchResp.Resource is Bundle bundle))
            {
                switch (bundle?.Total)
                {
                    // no matches - continue with update as create
                    case 0:
                        break;

                    // one match - check extra conditions and continue with update if they pass
                    case 1:
                        {
                            if ((!string.IsNullOrEmpty(id)) &&
                                (!bundle.Entry[0].Resource.Id.Equals(id, StringComparison.Ordinal)))
                            {
                                response = new()
                                {
                                    Outcome = SerializationUtils.BuildOutcomeForRequest(
                                        HttpStatusCode.PreconditionFailed,
                                        $"Conditional update query returned a match with a id: {bundle.Entry[0].Resource.Id}, expected {id}"),
                                    StatusCode = HttpStatusCode.PreconditionFailed,
                                };
                                return false;
                            }
                        }
                        break;

                    // multiple matches - fail the request
                    default:
                        {
                            response = new()
                            {
                                Outcome = SerializationUtils.BuildOutcomeForRequest(
                                    HttpStatusCode.PreconditionFailed,
                                    $"Conditional update query returned too many matches: {bundle?.Total}"),
                                StatusCode = HttpStatusCode.PreconditionFailed,
                            };
                            return false;
                        }
                }
            }
            else
            {
                response = new()
                {
                    Outcome = searchResp.Outcome ?? SerializationUtils.BuildOutcomeForRequest(
                        HttpStatusCode.PreconditionFailed,
                        $"Conditional update query failed: {ctx.UrlQuery}"),
                    StatusCode = HttpStatusCode.PreconditionFailed,
                };
                return false;
            }
        }

        Resource? resource = _store[resourceType].InstanceUpdate(
            content,
            _config.AllowCreateAsUpdate,
            ctx.IfMatch,
            ctx.IfNoneMatch,
            _protectedResources,
            out sc,
            out outcome);

        Resource? sForHook = null;

        foreach (IFhirInteractionHook hook in hooks ?? [])
        {
            sForHook ??= (Resource?)resource?.DeepCopy();

            if (hook.HookRequestStates.Contains(Common.HookRequestStateCodes.Post))
            {
                _ = hook.DoInteractionHook(
                        ctx,
                        this,
                        _store[ctx.ResourceType],
                        sForHook,
                        out FhirResponseContext hr);

                // check for the hook indicating processing is complete
                if (hr.StatusCode != null)
                {
                    response = hr;
                    return true;
                }

                // if the hook modified the resource, use that moving forward
                if (hr.Resource != null)
                {
                    sForHook = (Resource)hr.Resource;
                    resource = sForHook;
                }
            }
        }

        if (resource == null)
        {
            response = new()
            {
                Outcome = SerializationUtils.BuildOutcomeForRequest(
                    HttpStatusCode.InternalServerError,
                    "Failed to update resource"),
                StatusCode = HttpStatusCode.InternalServerError,
            };
            return false;
        }

        response = new()
        {
            Resource = resource,
            ResourceType = resource.TypeName,
            Id = resource.Id,
            ETag = string.IsNullOrEmpty(resource.Meta?.VersionId) ? string.Empty : $"W/\"{resource.Meta.VersionId}\"",
            LastModified = resource.Meta?.LastUpdated == null ? string.Empty : resource.Meta.LastUpdated.Value.UtcDateTime.ToString("r"),
            Location = $"{_config.BaseUrl}/{resourceType}/{resource.Id}",
            Outcome = outcome,
            StatusCode = sc,
        };
        return true;
    }

    /// <summary>
    /// Attempts to add an executable search parameter to a given resource.
    /// </summary>
    /// <param name="resourceType">Type of the resource.</param>
    /// <param name="spDefinition">The sp definition.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    public bool TrySetExecutableSearchParameter(string resourceType, ModelInfo.SearchParamDefinition spDefinition)
    {
        if (!_store.ContainsKey(resourceType))
        {
            return false;
        }

        string c = resourceType + "." + spDefinition.Name;

        lock (_spLockObject)
        {
            if (_compiledSearchParameters.ContainsKey(c))
            {
                _ = _compiledSearchParameters.TryRemove(c, out _);
            }
        }

        _capabilitiesAreStale = true;
        _store[resourceType].SetExecutableSearchParameter(spDefinition);
        return true;
    }

    /// <summary>Attempts to remove an executable search parameter to a given resource.</summary>
    /// <param name="resourceType">Type of the resource.</param>
    /// <param name="name">        The sp name/code/id.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    public bool TryRemoveExecutableSearchParameter(string resourceType, string name)
    {
        if (!_store.ContainsKey(resourceType))
        {
            return false;
        }

        string c = resourceType + "." + name;

        lock (_spLockObject)
        {
            if (_compiledSearchParameters.ContainsKey(c))
            {
                _ = _compiledSearchParameters.TryRemove(c, out _);
            }
        }

        _capabilitiesAreStale = true;
        _store[resourceType].RemoveExecutableSearchParameter(name);
        return true;
    }

    /// <summary>
    /// Attempts to get search parameter definition a ModelInfo.SearchParamDefinition from the given
    /// string.
    /// </summary>
    /// <param name="resourceName">    [out] The resource.</param>
    /// <param name="spName">        The name.</param>
    /// <param name="spDefinition">[out] The sp definition.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    public bool TryGetSearchParamDefinition(string resourceName, string spName, out ModelInfo.SearchParamDefinition? spDefinition)
    {
        if (!_store.TryGetValue(resourceName, out IVersionedResourceStore? resourceStore))
        {
            spDefinition = null;
            return false;
        }

        if (ParsedSearchParameter._allResourceParameters.TryGetValue(spName, out spDefinition))
        {
            return true;
        }

        return resourceStore.TryGetSearchParamDefinition(spName, out spDefinition);
    }

    /// <summary>Compile FHIR path criteria.</summary>
    /// <param name="fpc">The fpc.</param>
    /// <returns>A CompiledExpression.</returns>
    public static CompiledExpression CompileFhirPathCriteria(string fpc)
    {
        MatchCollection matches = _fhirpathVarMatcher().Matches(fpc);

        // replace the variable with a resolve call
        foreach (string matchValue in matches.Select(m => m.Value).Distinct())
        {
            fpc = fpc.Replace(matchValue, $"'{FhirPathVariableResolver._fhirPathPrefix}{matchValue.Substring(1)}'.resolve()");
        }

        return _compiler.Compile(fpc);
    }

    /// <summary>Processes a parsed SubscriptionTopic resource.</summary>
    /// <param name="topic"> The topic.</param>
    /// <param name="remove">(Optional) True to remove.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    public bool StoreProcessSubscriptionTopic(ParsedSubscriptionTopic topic, bool remove = false)
    {
        if (remove)
        {
            if (!_topics.ContainsKey(topic.Url))
            {
                return false;
            }

            // remove from all resources
            foreach (IVersionedResourceStore rs in _store.Values)
            {
                rs.RemoveExecutableSubscriptionTopic(topic.Url);
            }

            return true;
        }

        bool priorExisted = _topics.ContainsKey(topic.Url);

        // set our local reference
        if (priorExisted)
        {
            _topics[topic.Url] = topic;
        }
        else
        {
            _ = _topics.TryAdd(topic.Url, topic);
        }

        // check for no resource triggers
        if (!topic.ResourceTriggers.Any())
        {
            // remove from all resources
            foreach (IVersionedResourceStore rs in _store.Values)
            {
                rs.RemoveExecutableSubscriptionTopic(topic.Url);
            }

            // if we cannot execute, fail the update
            return false;
        }

        bool canExecute = false;

        // loop over all resources to account for a topic changing resources
        foreach ((string resourceName, IVersionedResourceStore rs) in _store)
        {
            bool executesOnResource = false;

            if (!topic.ResourceTriggers.ContainsKey(resourceName))
            {
                if (priorExisted)
                {
                    rs.RemoveExecutableSubscriptionTopic(topic.Url);
                }
                continue;
            }

            List<ExecutableSubscriptionInfo.InteractionOnlyTrigger> interactionTriggers = new();
            List<ExecutableSubscriptionInfo.CompiledFhirPathTrigger> fhirPathTriggers = new();
            List<ExecutableSubscriptionInfo.CompiledQueryTrigger> queryTriggers = new();
            ParsedResultParameters? resultParameters = null;


            string[] keys = new string[3] { resourceName, "*", "Resource" };

            foreach (string key in keys)
            {
                // TODO: Make sure to reduce full resource URI down to stub (e.g., not http://hl7.org/fhir/StructureDefinition/Patient)
                // TODO: Need to check event triggers once they are added
                if (!topic.ResourceTriggers.TryGetValue(key, out List<ParsedSubscriptionTopic.ResourceTrigger>? rts))
                {
                    continue;
                }

                // build our trigger definitions
                foreach (ParsedSubscriptionTopic.ResourceTrigger rt in rts)
                {
                    bool onCreate = rt.OnCreate;
                    bool onUpdate = rt.OnUpdate;
                    bool onDelete = rt.OnDelete;

                    // not filled out means trigger on any
                    if ((!onCreate) && (!onUpdate) && (!onDelete))
                    {
                        onCreate = true;
                        onUpdate = true;
                        onDelete = true;
                    }

                    // prefer FHIRPath if present
                    if (!string.IsNullOrEmpty(rt.FhirPathCriteria))
                    {
                        fhirPathTriggers.Add(new(
                            onCreate,
                            onUpdate,
                            onDelete,
                            CompileFhirPathCriteria(rt.FhirPathCriteria)));

                        canExecute = true;
                        executesOnResource = true;

                        continue;
                    }

                    // for query-based criteria
                    if ((!string.IsNullOrEmpty(rt.QueryPrevious)) || (!string.IsNullOrEmpty(rt.QueryCurrent)))
                    {
                        IEnumerable<ParsedSearchParameter> previousTest;
                        IEnumerable<ParsedSearchParameter> currentTest;

                        if (string.IsNullOrEmpty(rt.QueryPrevious))
                        {
                            previousTest = Array.Empty<ParsedSearchParameter>();
                        }
                        else
                        {
                            previousTest = ParsedSearchParameter.Parse(rt.QueryPrevious, this, rs, resourceName);
                        }

                        if (string.IsNullOrEmpty(rt.QueryCurrent))
                        {
                            currentTest = Array.Empty<ParsedSearchParameter>();
                        }
                        else
                        {
                            currentTest = ParsedSearchParameter.Parse(rt.QueryCurrent, this, rs, resourceName);
                        }

                        queryTriggers.Add(new(
                            onCreate,
                            onUpdate,
                            onDelete,
                            previousTest,
                            rt.CreateAutoFail,
                            rt.CreateAutoPass,
                            currentTest,
                            rt.DeleteAutoFail,
                            rt.DeleteAutoPass,
                            rt.RequireBothQueries));

                        canExecute = true;
                        executesOnResource = true;

                        continue;
                    }

                    // add triggers that do not have inherent filters beyond interactions
                    if (onCreate || onUpdate || onDelete)
                    {
                        interactionTriggers.Add(new(
                            onCreate,
                            onUpdate,
                            onDelete));

                        canExecute = true;
                        executesOnResource = true;

                        continue;
                    }
                }

                // build our inclusions
                if (topic.NotificationShapes.ContainsKey(key) &&
                    topic.NotificationShapes[key].Any())
                {
                    string includes = string.Empty;
                    string reverseIncludes = string.Empty;

                    // TODO: use first matching shape for now
                    ParsedSubscriptionTopic.NotificationShape shape = topic.NotificationShapes[key].First();

                    if (shape.Includes?.Any() ?? false)
                    {
                        includes = string.Join('&', shape.Includes);
                    }

                    if (shape.ReverseIncludes?.Any() ?? false)
                    {
                        reverseIncludes = string.Join('&', shape.ReverseIncludes);
                    }

                    if (string.IsNullOrEmpty(includes) && string.IsNullOrEmpty(reverseIncludes))
                    {
                        resultParameters = null;
                    }
                    else if (string.IsNullOrEmpty(includes))
                    {
                        resultParameters = new(reverseIncludes, this);
                    }
                    else if (string.IsNullOrEmpty(reverseIncludes))
                    {
                        resultParameters = new(includes, this);
                    }
                    else
                    {
                        resultParameters = new(includes + "&" + reverseIncludes, this);
                    }
                }

                // either update or remove this topic from this resource
                if (executesOnResource)
                {
                    // update the executable definition for the current resource
                    rs.SetExecutableSubscriptionTopic(
                        topic.Url,
                        interactionTriggers,
                        fhirPathTriggers,
                        queryTriggers,
                        resultParameters);
                }
                else
                {
                    rs.RemoveExecutableSubscriptionTopic(topic.Url);
                }
            }
        }

        //RegisterSubscriptionsChanged();

        return canExecute;
    }

    /// <summary>Gets subscription event count.</summary>
    /// <param name="subscriptionId">Identifier for the subscription.</param>
    /// <param name="increment">     True to increment.</param>
    /// <returns>The subscription event count.</returns>
    public long GetSubscriptionEventCount(string subscriptionId, bool increment)
    {
        if (!_subscriptions.TryGetValue(subscriptionId, out ParsedSubscription? subscription))
        {
            return 0;
        }

        if (increment)
        {
            return subscription.IncrementEventCount();
        }

        return subscription.CurrentEventCount;
    }

    /// <summary>Registers the subscriptions changed.</summary>
    /// <param name="subscription"> The subscription.</param>
    /// <param name="removed">      (Optional) True if removed.</param>
    /// <param name="sendHandshake">(Optional) True to send handshake.</param>
    public void RegisterSubscriptionsChanged(
        ParsedSubscription? subscription,
        bool removed = false,
        bool sendHandshake = false)
    {
        EventHandler<SubscriptionChangedEventArgs>? handler = OnSubscriptionsChanged;

        if (handler != null)
        {
            handler(this, new()
            {
                Tenant = _config,
                ChangedSubscription = subscription,
                RemovedSubscriptionId = removed ? subscription?.Id : null,
                SendHandshake = sendHandshake,
            });
        }
    }

    /// <summary>Change subscription status.</summary>
    /// <param name="id">    The identifier.</param>
    /// <param name="status">The status.</param>
    public void ChangeSubscriptionStatus(string id, string status)
    {
        if (!_subscriptions.TryGetValue(id, out ParsedSubscription? subscription) ||
            (subscription == null))
        {
            return;
        }

        subscription.CurrentStatus = status;
        RegisterSubscriptionsChanged(subscription);
    }

    /// <summary>Registers the event.</summary>
    /// <param name="subscriptionId">   Identifier for the subscription.</param>
    /// <param name="subscriptionEvent">The subscription event.</param>
    public void RegisterSendEvent(string subscriptionId, SubscriptionEvent subscriptionEvent)
    {
        _subscriptions[subscriptionId].RegisterEvent(subscriptionEvent);

        EventHandler<SubscriptionSendEventArgs>? handler = OnSubscriptionSendEvent;

        if (handler != null)
        {
            handler(this, new()
            {
                Tenant = _config,
                Subscription = _subscriptions[subscriptionId],
                NotificationEvents = new List<SubscriptionEvent>() { subscriptionEvent },
                NotificationType = ParsedSubscription.NotificationTypeCodes.EventNotification,
            });
        }

        //StateHasChanged();
    }

    /// <summary>Adds a subscription error.</summary>
    /// <param name="id">          The subscription id.</param>
    /// <param name="errorMessage">Message describing the error.</param>
    public void RegisterError(string id, string errorMessage)
    {
        if (!_subscriptions.ContainsKey(id))
        {
            return;
        }

        _subscriptions[id].RegisterError(errorMessage);
    }

    /// <summary>Registers the received subscription changed.</summary>
    /// <param name="subscriptionReference">  The subscription reference.</param>
    /// <param name="cachedNotificationCount">Number of cached notifications.</param>
    /// <param name="removed">                True if removed.</param>
    public void RegisterReceivedSubscriptionChanged(
        string subscriptionReference,
        int cachedNotificationCount,
        bool removed)
    {
        EventHandler<ReceivedSubscriptionChangedEventArgs>? handler = OnReceivedSubscriptionChanged;

        if (handler != null)
        {
            handler(this, new()
            {
                Tenant = _config,
                SubscriptionReference = subscriptionReference,
                CurrentBundleCount = cachedNotificationCount,
                Removed = removed,
            });
        }
    }

    /// <summary>Registers the received notification.</summary>
    /// <param name="bundleId">Identifier for the bundle.</param>
    /// <param name="status">  The parsed SubscriptionStatus information from the notification.</param>
    public void RegisterReceivedNotification(string bundleId, ParsedSubscriptionStatus status)
    {
        if (!_receivedNotifications.ContainsKey(status.SubscriptionReference))
        {
            _ = _receivedNotifications.TryAdd(status.SubscriptionReference, new());
        }

        _receivedNotifications[status.SubscriptionReference].Add(status);

        EventHandler<ReceivedSubscriptionEventArgs>? handler = OnReceivedSubscriptionEvent;

        if (handler != null)
        {
            handler(this, new()
            {
                Tenant = _config,
                BundleId = bundleId,
                Status = status,
            });
        }
    }


    /// <summary>
    /// Serialize one or more subscription events into the desired format and content level.
    /// </summary>
    /// <param name="subscriptionId">  The subscription id of the subscription the events belong to.</param>
    /// <param name="eventNumbers">    One or more event numbers to include.</param>
    /// <param name="notificationType">Type of notification (e.g., 'notification-event')</param>
    /// <param name="contentType">     Override for the content type specified in the subscription.</param>
    /// <param name="contentLevel">    Override for the content level specified in the subscription.</param>
    /// <returns></returns>
    public string SerializeSubscriptionEvents(
        string subscriptionId,
        IEnumerable<long> eventNumbers,
        string notificationType,
        bool pretty,
        string contentType = "",
        string contentLevel = "")
    {
        if (_subscriptions.ContainsKey(subscriptionId))
        {
            Bundle? bundle = _subscriptionConverter.BundleForSubscriptionEvents(
                _subscriptions[subscriptionId],
                eventNumbers,
                notificationType,
                _config.BaseUrl,
                contentLevel);

            if (bundle == null)
            {
                return string.Empty;
            }

            string serialized = SerializationUtils.SerializeFhir(
                bundle,
                string.IsNullOrEmpty(contentType) ? _subscriptions[subscriptionId].ContentType : contentType,
                pretty,
                string.Empty);

            return serialized;
        }

        return string.Empty;
    }

    /// <summary>Attempts to serialize to subscription.</summary>
    /// <param name="subscriptionInfo">Information describing the subscription.</param>
    /// <param name="serialized">      [out] The serialized.</param>
    /// <param name="pretty">          If the output should be 'pretty' formatted.</param>
    /// <param name="destFormat">      (Optional) Destination format.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    public bool TrySerializeToSubscription(
        ParsedSubscription subscriptionInfo,
        out string serialized,
        bool pretty,
        string destFormat = "application/fhir+json")
    {
        if (!_subscriptionConverter.TryParse(subscriptionInfo, out Hl7.Fhir.Model.Subscription subscription))
        {
            serialized = string.Empty;
            return false;
        }

        if (string.IsNullOrEmpty(destFormat))
        {
            destFormat = "application/fhir+json";
        }

        serialized = SerializationUtils.SerializeFhir(subscription, destFormat, pretty);
        return true;
    }

    /// <summary>Attempts to serialize to subscription.</summary>
    /// <param name="parsed">      Information describing the subscription.</param>
    /// <param name="subscription">[out] The serialized.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    public bool TryGetSubscription(
        ParsedSubscription parsed,
        out object? subscription)
    {
        if (_subscriptionConverter.TryParse(parsed, out Hl7.Fhir.Model.Subscription s))
        {
            subscription = s;
            return true;
        }

        subscription = null;
        return false;
    }

    /// <summary>Bundle for subscription events.</summary>
    /// <param name="subscriptionId">  Identifier for the subscription.</param>
    /// <param name="eventNumbers">    One or more event numbers to include.</param>
    /// <param name="notificationType">Type of notification (e.g., 'notification-event')</param>
    /// <param name="contentLevel">    (Optional) Override for the content level specified in the
    ///  subscription.</param>
    /// <returns>A Bundle?</returns>
    public Bundle? BundleForSubscriptionEvents(
        string subscriptionId,
        IEnumerable<long> eventNumbers,
        string notificationType,
        string contentLevel = "")
    {
        if (_subscriptions.ContainsKey(subscriptionId))
        {
            Bundle? bundle = _subscriptionConverter.BundleForSubscriptionEvents(
                _subscriptions[subscriptionId],
                eventNumbers,
                notificationType,
                _config.BaseUrl,
                contentLevel);

            return bundle;
        }

        return null;
    }

    /// <summary>Parse notification bundle.</summary>
    /// <param name="bundle">The bundle.</param>
    /// <returns>A ParsedSubscriptionStatus?</returns>
    public ParsedSubscriptionStatus? ParseNotificationBundle(
        Bundle bundle)
    {
        if ((!bundle.Entry.Any()) ||
            (bundle.Entry.First().Resource == null))
        {
            return null;
        }

        if (!_subscriptionConverter.TryParse(bundle.Entry.First().Resource, bundle.Id, out ParsedSubscriptionStatus status))
        {
            return null;
        }

        return status;
    }

    /// <summary>Status for subscription.</summary>
    /// <param name="subscriptionId">  Identifier for the subscription.</param>
    /// <param name="notificationType">Type of notification (e.g., 'notification-event')</param>
    /// <returns>A Hl7.Fhir.Model.Resource?</returns>
    public Hl7.Fhir.Model.Resource? StatusForSubscription(
        string subscriptionId,
        string notificationType)
    {
        if (_subscriptions.ContainsKey(subscriptionId))
        {
            return _subscriptionConverter.StatusForSubscription(
                _subscriptions[subscriptionId],
                notificationType,
                _config.BaseUrl);
        }

        return null;
    }

    /// <summary>Process the subscription.</summary>
    /// <param name="subscription">The subscription.</param>
    /// <param name="remove">      (Optional) True to remove.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    public bool StoreProcessSubscription(ParsedSubscription subscription, bool remove = false)
    {
        if (remove)
        {
            if (!_subscriptions.ContainsKey(subscription.Id))
            {
                return false;
            }

            // remove from all resources
            foreach (IVersionedResourceStore rs in _store.Values)
            {
                rs.RemoveExecutableSubscription(subscription.TopicUrl, subscription.Id);
            }

            _ = _subscriptions.TryRemove(subscription.Id, out _);

            RegisterSubscriptionsChanged(subscription, true);

            return true;
        }

        // check for existing record
        bool priorExisted = _subscriptions.ContainsKey(subscription.Id);
        string priorState;

        if (priorExisted)
        {
            priorState = _subscriptions[subscription.Id].CurrentStatus;
            _subscriptions[subscription.Id] = subscription;
        }
        else
        {
            priorState = "off";
            _ = _subscriptions.TryAdd(subscription.Id, subscription);
        }

        // check to see if we have this topic
        if (!_topics.ContainsKey(subscription.TopicUrl))
        {
            if (_loadState == LoadStateCodes.Read)
            {
                if (!_loadReprocess!.ContainsKey("Subscription"))
                {
                    _loadReprocess.Add("Subscription", new());
                }

                _loadReprocess["Subscription"].Add(subscription);
            }

            return false;
        }

        // check for overriding the expiration of subscriptions
        if (_loadState == LoadStateCodes.Process)
        {
            subscription.ExpirationTicks = -1;
        }

        ParsedSubscriptionTopic topic = _topics[subscription.TopicUrl];

        // loop over all resources to account for a topic changing resources
        foreach ((string resourceName, IVersionedResourceStore rs) in _store)
        {
            if (!topic.ResourceTriggers.ContainsKey(resourceName))
            {
                continue;
            }

            if (!subscription.Filters.ContainsKey(resourceName) &&
                !subscription.Filters.ContainsKey("*") &&
                !subscription.Filters.ContainsKey("Resource"))
            {
                // add an empty filter record so the engine knows about the subscription
                rs.SetExecutableSubscription(subscription.TopicUrl, subscription.Id, new());
                continue;
            }

            List<ParsedSearchParameter> parsedFilters = new();

            string[] keys = new string[3] { resourceName, "*", "Resource" };

            foreach (string key in keys)
            {
                if (!subscription.Filters.ContainsKey(key))
                {
                    continue;
                }

                foreach (ParsedSubscription.SubscriptionFilter filter in subscription.Filters[key])
                {
                    // TODO: check support for chained parameters in filters

                    // TODO: validate this is working for generic parameters (e.g., _id)

                    // TODO: support inline-defined parameters
                    if (!rs.TryGetSearchParamDefinition(filter.Name, out ModelInfo.SearchParamDefinition? spd) ||
                        spd == null)
                    {
                        Console.WriteLine($"Cannot apply filter with no search parameter definition {resourceName}?{filter.Name}");
                        continue;
                    }

                    SearchModifierCodes modifierCode = SearchModifierCodes.None;

                    if (!string.IsNullOrEmpty(filter.Modifier))
                    {
                        if (!Enum.TryParse(filter.Modifier, true, out modifierCode))
                        {
                            Console.WriteLine($"Ignoring unknown modifier: {resourceName}?{filter.Name}:{filter.Modifier}");
                        }
                    }

                    ParsedSearchParameter sp = new(
                        this,
                        rs,
                        key.Equals("*") ? "Resource" : key,
                        filter.Name,
                        filter.Modifier,
                        modifierCode,
                        string.IsNullOrEmpty(filter.Comparator) ? filter.Value : filter.Comparator + filter.Value,
                        spd);

                    parsedFilters.Add(sp);
                }

                rs.SetExecutableSubscription(subscription.TopicUrl, subscription.Id, parsedFilters);
            }
        }

        RegisterSubscriptionsChanged(subscription, false, priorState.Equals("off"));

        return true;
    }

    /// <summary>Perform a FHIR System-level operation.</summary>
    /// <param name="ctx">     The request context.</param>
    /// <param name="response">[out] The response data.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    public bool SystemOperation(
        FhirRequestContext ctx,
        out FhirResponseContext response)
    {
        bool success = DoSystemOperation(
            ctx,
            out response);

        string sr = response.Resource == null ? string.Empty : SerializationUtils.SerializeFhir((Resource)response.Resource, ctx.DestinationFormat, ctx.SerializePretty);
        string so = response.Outcome == null ? string.Empty : SerializationUtils.SerializeFhir((Resource)response.Outcome, ctx.DestinationFormat, ctx.SerializePretty);

        response = response with
        {
            MimeType = ctx.DestinationFormat,
            SerializedResource = sr,
            SerializedOutcome = so,
        };

        return success;
    }

    /// <summary>Executes the system operation operation.</summary>
    /// <param name="ctx">            The request context.</param>
    /// <param name="contentResource">The content resource.</param>
    /// <param name="resource">       [out] The resource.</param>
    /// <param name="outcome">        [out] The outcome.</param>
    /// <returns>A HttpStatusCode.</returns>
    internal bool DoSystemOperation(
        FhirRequestContext ctx,
        out FhirResponseContext response)
    {
        if (!_operations.ContainsKey(ctx.OperationName))
        {
            response = new()
            {
                Outcome = SerializationUtils.BuildOutcomeForRequest(
                    HttpStatusCode.NotFound,
                    $"Operation {ctx.OperationName} does not have an executable implementation on this server."),
                StatusCode = HttpStatusCode.NotFound,
            };
            return false;
        }

        IFhirOperation op = _operations[ctx.OperationName];

        if (!op.AllowSystemLevel)
        {
            response = new()
            {
                Outcome = SerializationUtils.BuildOutcomeForRequest(
                    HttpStatusCode.UnprocessableEntity,
                    $"Operation {ctx.OperationName} does not allow system-level execution.",
                    OperationOutcome.IssueType.NotSupported),
                StatusCode = HttpStatusCode.UnprocessableEntity,
            };
            return false;
        }

        Resource? r = null;

        if (ctx.SourceObject != null)
        {
            if (ctx.SourceObject is Resource)
            {
                r = ctx.SourceObject as Resource;
            }
            else if (!op.AcceptsNonFhir)
            {
                response = new()
                {
                    Outcome = SerializationUtils.BuildOutcomeForRequest(
                        HttpStatusCode.UnsupportedMediaType,
                        $"Operation {ctx.OperationName} does not consume non-FHIR content.",
                        OperationOutcome.IssueType.Invalid),
                    StatusCode = HttpStatusCode.UnsupportedMediaType,
                };
                return false;
            }
        }
        else if (!string.IsNullOrEmpty(ctx.SourceContent))
        {
            HttpStatusCode deserializeSc = SerializationUtils.TryDeserializeFhir(ctx.SourceContent, ctx.SourceFormat, out r, out _);

            if ((!deserializeSc.IsSuccessful()) &&
                (!op.AcceptsNonFhir))
            {
                response = new()
                {
                    Outcome = SerializationUtils.BuildOutcomeForRequest(
                        HttpStatusCode.UnsupportedMediaType,
                        $"Operation {ctx.OperationName} does not consume non-FHIR content.",
                        OperationOutcome.IssueType.Invalid),
                    StatusCode = HttpStatusCode.UnsupportedMediaType,
                };
                return false;
            }
        }

        IEnumerable<IFhirInteractionHook> hooks = GetHooks(ctx.ResourceType, Common.StoreInteractionCodes.SystemOperation);

        if (hooks?.Any() ?? false)
        {
            foreach (IFhirInteractionHook hook in hooks)
            {
                if (hook.HookRequestStates.Contains(Common.HookRequestStateCodes.Pre))
                {
                    _ = hook.DoInteractionHook(
                            ctx,
                            this,
                            null,
                            null,
                            out FhirResponseContext hr);

                    // check for the hook indicating processing is complete
                    if (hr.StatusCode != null)
                    {
                        response = hr;
                        return true;
                    }

                    // if the hook modified the content resource, use that moving forward
                    if (hr.Resource != null)
                    {
                        r = (Resource)hr.Resource;
                    }
                }
            }
        }

        bool success = op.DoOperation(
            ctx,
            this,
            null,
            null,
            r,
            out FhirResponseContext opResponse);

        if ((opResponse.Resource != null) &&
            (opResponse.Resource is Resource))
        {
            r = (Resource)opResponse.Resource;
        }

        if (hooks?.Any() ?? false)
        {
            Resource? sForHook = (Resource?)r?.DeepCopy();

            foreach (IFhirInteractionHook hook in hooks)
            {
                if (hook.HookRequestStates.Contains(Common.HookRequestStateCodes.Post))
                {
                    _ = hook.DoInteractionHook(
                            ctx,
                            this,
                            null,
                            sForHook,
                            out FhirResponseContext hr);

                    // check for the hook indicating processing is complete
                    if (hr.StatusCode != null)
                    {
                        response = hr;
                        return true;
                    }

                    // if the hook modified the resource, use that moving forward
                    if (hr.Resource != null)
                    {
                        sForHook = (Resource)hr.Resource;
                        r = sForHook;
                    }
                }
            }
        }

        response = new()
        {
            Resource = r,
            ResourceType = r?.TypeName ?? string.Empty,
            Id = r?.Id ?? string.Empty,
            Outcome = opResponse.Outcome ?? SerializationUtils.BuildOutcomeForRequest(
                opResponse.StatusCode ?? (success ? HttpStatusCode.OK : HttpStatusCode.InternalServerError), 
                $"System-Level Operation {ctx.OperationName} {(success ? "succeeded" : "failed")}: {opResponse.StatusCode}"),
            StatusCode = success ? HttpStatusCode.OK : HttpStatusCode.InternalServerError,
        };
        return success;
    }

    /// <summary>Perform a FHIR Type-level operation.</summary>
    /// <param name="ctx">     The request context.</param>
    /// <param name="response">[out] The response data.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    public bool TypeOperation(
        FhirRequestContext ctx,
        out FhirResponseContext response)
    {
        bool success = DoTypeOperation(
            ctx,
            out response);

        string sr = response.Resource == null ? string.Empty : SerializationUtils.SerializeFhir((Resource)response.Resource, ctx.DestinationFormat, ctx.SerializePretty);
        string so = response.Outcome == null ? string.Empty : SerializationUtils.SerializeFhir((Resource)response.Outcome, ctx.DestinationFormat, ctx.SerializePretty);

        response = response with
        {
            MimeType = ctx.DestinationFormat,
            SerializedResource = sr,
            SerializedOutcome = so,
        };

        return success;
    }

    /// <summary>Executes the type operation operation.</summary>
    /// <param name="ctx">     The request context.</param>
    /// <param name="response">[out] The response data.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    internal bool DoTypeOperation(
        FhirRequestContext ctx,
        out FhirResponseContext response)
    {
        if (!_store.ContainsKey(ctx.ResourceType))
        {
            response = new()
            {
                Outcome = SerializationUtils.BuildOutcomeForRequest(
                    HttpStatusCode.NotFound,
                    $"Resource type {ctx.ResourceType} does not exist on this server.",
                    OperationOutcome.IssueType.NotSupported),
                StatusCode = HttpStatusCode.NotFound,
            };
            return false;
        }

        if (!_operations.ContainsKey(ctx.OperationName))
        {
            response = new()
            {
                Outcome = SerializationUtils.BuildOutcomeForRequest(
                    HttpStatusCode.NotFound,
                    $"Operation {ctx.OperationName} does not have an executable implementation on this server."),
                StatusCode = HttpStatusCode.NotFound,
            };
            return false;
        }

        IFhirOperation op = _operations[ctx.OperationName];

        if (!op.AllowResourceLevel)
        {
            response = new()
            {
                Outcome = SerializationUtils.BuildOutcomeForRequest(
                    HttpStatusCode.UnprocessableEntity,
                    $"Operation {ctx.OperationName} does not allow type-level execution.",
                    OperationOutcome.IssueType.NotSupported),
                StatusCode = HttpStatusCode.UnprocessableEntity,
            };
            return false;
        }

        if (op.SupportedResources.Any() && (!op.SupportedResources.Contains(ctx.ResourceType)))
        {
            response = new()
            {
                Outcome = SerializationUtils.BuildOutcomeForRequest(
                    HttpStatusCode.UnprocessableEntity,
                    $"Operation {ctx.OperationName} is not defined for resource: {ctx.ResourceType}.",
                    OperationOutcome.IssueType.NotSupported),
                StatusCode = HttpStatusCode.UnprocessableEntity,
            };
            return false;
        }

        Resource? r = null;

        if (ctx.SourceObject != null)
        {
            if (ctx.SourceObject is Resource)
            {
                r = ctx.SourceObject as Resource;
            }
            else if (!op.AcceptsNonFhir)
            {
                response = new()
                {
                    Outcome = SerializationUtils.BuildOutcomeForRequest(
                        HttpStatusCode.UnsupportedMediaType,
                        $"Operation {ctx.OperationName} does not consume non-FHIR content.",
                        OperationOutcome.IssueType.Invalid),
                    StatusCode = HttpStatusCode.UnsupportedMediaType,
                };
                return false;
            }
        }
        else if (!string.IsNullOrEmpty(ctx.SourceContent))
        {
            HttpStatusCode deserializeSc = SerializationUtils.TryDeserializeFhir(ctx.SourceContent, ctx.SourceFormat, out r, out _);

            if ((!deserializeSc.IsSuccessful()) &&
                (!op.AcceptsNonFhir))
            {
                response = new()
                {
                    Outcome = SerializationUtils.BuildOutcomeForRequest(
                        HttpStatusCode.UnsupportedMediaType,
                        $"Operation {ctx.OperationName} does not consume non-FHIR content.",
                        OperationOutcome.IssueType.Invalid),
                    StatusCode = HttpStatusCode.UnsupportedMediaType,
                };
                return false;
            }
        }

        IEnumerable<IFhirInteractionHook> hooks = GetHooks(ctx.ResourceType, Common.StoreInteractionCodes.TypeOperation);

        if (hooks?.Any() ?? false)
        {
            foreach (IFhirInteractionHook hook in hooks)
            {
                if (hook.HookRequestStates.Contains(Common.HookRequestStateCodes.Pre))
                {
                    _ = hook.DoInteractionHook(
                            ctx,
                            this,
                            _store[ctx.ResourceType],
                            r,
                            out FhirResponseContext hr);

                    // check for the hook indicating processing is complete
                    if (hr.StatusCode != null)
                    {
                        response = hr;
                        return true;
                    }

                    // if the hook modified the resource, use that moving forward
                    if (hr.Resource != null)
                    {
                        r = (Resource)hr.Resource;
                    }
                }
            }
        }

        bool success = op.DoOperation(
            ctx,
            this,
            _store[ctx.ResourceType],
            null,
            r,
            out FhirResponseContext opResponse);

        if ((opResponse.Resource != null) &&
            (opResponse.Resource is Resource))
        {
            r = (Resource)opResponse.Resource;
        }

        if (hooks?.Any() ?? false)
        {
            Resource? sForHook = (Resource?)r?.DeepCopy();

            foreach (IFhirInteractionHook hook in hooks)
            {
                if (hook.HookRequestStates.Contains(Common.HookRequestStateCodes.Post))
                {
                    _ = hook.DoInteractionHook(
                            ctx,
                            this,
                            _store[ctx.ResourceType],
                            sForHook,
                            out FhirResponseContext hr);

                    // check for the hook indicating processing is complete
                    if (hr.StatusCode != null)
                    {
                        response = hr;
                        return true;
                    }

                    // if the hook modified the resource, use that moving forward
                    if (hr.Resource != null)
                    {
                        sForHook = (Resource)hr.Resource;
                        r = sForHook;
                    }
                }
            }
        }

        response = new()
        {
            Resource = r,
            ResourceType = r?.TypeName ?? string.Empty,
            Id = r?.Id ?? string.Empty,
            Outcome = opResponse.Outcome ?? SerializationUtils.BuildOutcomeForRequest(
                opResponse.StatusCode ?? (success ? HttpStatusCode.OK : HttpStatusCode.InternalServerError),
                $"Type-Level Operation {ctx.ResourceType}/{ctx.OperationName} {(success ? "succeeded" : "failed")}: {opResponse.StatusCode}"),
            StatusCode = success ? HttpStatusCode.OK : HttpStatusCode.InternalServerError,
        };
        return success;
    }

    /// <summary>Performa FHIR Instance-level operation.</summary>
    /// <param name="ctx">     The request context.</param>
    /// <param name="response">[out] The response data.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    public bool InstanceOperation(
        FhirRequestContext ctx,
        out FhirResponseContext response)
    {
        bool success = DoInstanceOperation(
            ctx,
            out response);

        string sr = response.Resource == null ? string.Empty : SerializationUtils.SerializeFhir((Resource)response.Resource, ctx.DestinationFormat, ctx.SerializePretty);
        string so = response.Outcome == null ? string.Empty : SerializationUtils.SerializeFhir((Resource)response.Outcome, ctx.DestinationFormat, ctx.SerializePretty);

        response = response with
        {
            MimeType = ctx.DestinationFormat,
            SerializedResource = sr,
            SerializedOutcome = so,
        };

        return success;
    }

    /// <summary>Executes the instance operation operation.</summary>
    /// <param name="ctx">     The request context.</param>
    /// <param name="response">[out] The response data.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    internal bool DoInstanceOperation(
        FhirRequestContext ctx,
        out FhirResponseContext response)
    {
        if (!_store.ContainsKey(ctx.ResourceType))
        {
            response = new()
            {
                Outcome = SerializationUtils.BuildOutcomeForRequest(
                    HttpStatusCode.NotFound,
                    $"Resource type {ctx.ResourceType} does not exist on this server.",
                    OperationOutcome.IssueType.NotSupported),
                StatusCode = HttpStatusCode.NotFound,
            };
            return false;
        }

        if (string.IsNullOrEmpty(ctx.Id) ||
            !((IReadOnlyDictionary<string, Resource>)_store[ctx.ResourceType]).ContainsKey(ctx.Id))
        {
            response = new()
            {
                Outcome = SerializationUtils.BuildOutcomeForRequest(
                    HttpStatusCode.NotFound,
                    $"Instance {ctx.ResourceType}/{ctx.Id} does not exist on this server."),
                StatusCode = HttpStatusCode.NotFound,
            };
            return false;
        }

        if (!_operations.ContainsKey(ctx.OperationName))
        {
            response = new()
            {
                Outcome = SerializationUtils.BuildOutcomeForRequest(
                    HttpStatusCode.NotFound,
                    $"Operation {ctx.OperationName} does not have an executable implementation on this server."),
                StatusCode = HttpStatusCode.NotFound,
            };
            return false;
        }

        IFhirOperation op = _operations[ctx.OperationName];

        if (!op.AllowInstanceLevel)
        {
            response = new()
            {
                Outcome = SerializationUtils.BuildOutcomeForRequest(
                    HttpStatusCode.UnprocessableEntity,
                    $"Operation {ctx.OperationName} does not allow instance-level execution.",
                    OperationOutcome.IssueType.NotSupported),
                StatusCode = HttpStatusCode.UnprocessableEntity,
            };
            return false;
        }

        if (op.SupportedResources.Any() && (!op.SupportedResources.Contains(ctx.ResourceType)))
        {
            response = new()
            {
                Outcome = SerializationUtils.BuildOutcomeForRequest(
                    HttpStatusCode.UnprocessableEntity,
                    $"Operation {ctx.OperationName} is not defined for resource: {ctx.ResourceType}.",
                    OperationOutcome.IssueType.NotSupported),
                StatusCode = HttpStatusCode.UnprocessableEntity,
            };
            return false;
        }

        Resource? r = null;

        if (ctx.SourceObject != null)
        {
            if (ctx.SourceObject is Resource)
            {
                r = ctx.SourceObject as Resource;
            }
            else if (!op.AcceptsNonFhir)
            {
                response = new()
                {
                    Outcome = SerializationUtils.BuildOutcomeForRequest(
                        HttpStatusCode.UnsupportedMediaType,
                        $"Operation {ctx.OperationName} does not consume non-FHIR content.",
                        OperationOutcome.IssueType.Invalid),
                    StatusCode = HttpStatusCode.UnsupportedMediaType,
                };
                return false;
            }
        }
        else if (!string.IsNullOrEmpty(ctx.SourceContent))
        {
            HttpStatusCode deserializeSc = SerializationUtils.TryDeserializeFhir(ctx.SourceContent, ctx.SourceFormat, out r, out _);

            if ((!deserializeSc.IsSuccessful()) &&
                (!op.AcceptsNonFhir))
            {
                response = new()
                {
                    Outcome = SerializationUtils.BuildOutcomeForRequest(
                        HttpStatusCode.UnsupportedMediaType,
                        $"Operation {ctx.OperationName} does not consume non-FHIR content.",
                        OperationOutcome.IssueType.Invalid),
                    StatusCode = HttpStatusCode.UnsupportedMediaType,
                };
                return false;
            }
        }

        IEnumerable<IFhirInteractionHook> hooks = GetHooks(ctx.ResourceType, Common.StoreInteractionCodes.InstanceOperation);

        if (hooks?.Any() ?? false)
        {
            foreach (IFhirInteractionHook hook in hooks)
            {
                if (hook.HookRequestStates.Contains(Common.HookRequestStateCodes.Pre))
                {
                    _ = hook.DoInteractionHook(
                            ctx,
                            this,
                            _store[ctx.ResourceType],
                            r,
                            out FhirResponseContext hr);

                    // check for the hook indicating processing is complete
                    if (hr.StatusCode != null)
                    {
                        response = hr;
                        return true;
                    }

                    // if the hook modified the resource, use that moving forward
                    if (hr.Resource != null)
                    {
                        r = (Resource)hr.Resource;
                    }
                }
            }
        }

        Resource focusResource = ((IReadOnlyDictionary<string, Resource>)_store[ctx.ResourceType])[ctx.Id];

        bool success = op.DoOperation(
            ctx,
            this,
            _store[ctx.ResourceType],
            focusResource,
            r,
            out FhirResponseContext opResponse);

        if ((opResponse.Resource != null) &&
            (opResponse.Resource is Resource))
        {
            r = (Resource)opResponse.Resource;
        }

        if (hooks?.Any() ?? false)
        {
            Resource? sForHook = (Resource?)r?.DeepCopy();

            foreach (IFhirInteractionHook hook in hooks)
            {
                if (hook.HookRequestStates.Contains(Common.HookRequestStateCodes.Post))
                {
                    _ = hook.DoInteractionHook(
                            ctx,
                            this,
                            _store[ctx.ResourceType],
                            sForHook,
                            out FhirResponseContext hr);

                    // check for the hook indicating processing is complete
                    if (hr.StatusCode != null)
                    {
                        response = hr;
                        return true;
                    }

                    // if the hook modified the resource, use that moving forward
                    if (hr.Resource != null)
                    {
                        sForHook = (Resource)hr.Resource;
                        r = sForHook;
                    }
                }
            }
        }

        response = new()
        {
            Resource = r,
            ResourceType = r?.TypeName ?? string.Empty,
            Id = r?.Id ?? string.Empty,
            Outcome = opResponse.Outcome ?? SerializationUtils.BuildOutcomeForRequest(
                opResponse.StatusCode ?? (success ? HttpStatusCode.OK : HttpStatusCode.InternalServerError),
                $"Instance-Level Operation {ctx.ResourceType}/{ctx.Id}/{ctx.OperationName} {(success ? "succeeded" : "failed")}: {opResponse.StatusCode}"),
            StatusCode = success ? HttpStatusCode.OK : HttpStatusCode.InternalServerError,
        };
        return success;
    }

    /// <summary>System delete.</summary>
    /// <param name="ctx">     The request context.</param>
    /// <param name="response">[out] The response data.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    public bool SystemDelete(
        FhirRequestContext ctx,
        out FhirResponseContext response)
    {
        bool success = DoSystemDelete(
            ctx,
            out response);

        string sr = response.Resource == null ? string.Empty : SerializationUtils.SerializeFhir((Resource)response.Resource, ctx.DestinationFormat, ctx.SerializePretty);
        string so = response.Outcome == null ? string.Empty : SerializationUtils.SerializeFhir((Resource)response.Outcome, ctx.DestinationFormat, ctx.SerializePretty);

        response = response with
        {
            MimeType = ctx.DestinationFormat,
            SerializedResource = sr,
            SerializedOutcome = so,
        };

        return success;
    }

    /// <summary>Executes the system delete operation.</summary>
    /// <param name="queryString">The query string.</param>
    /// <param name="response">   [out] The response data.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    internal bool DoSystemDelete(
        FhirRequestContext ctx,
        out FhirResponseContext response)
    {
        bool success = DoSystemSearch(ctx, out FhirResponseContext searchResp);

        // check for failed search
        if ((!success) ||
            (searchResp.Resource == null) ||
            (searchResp.Resource is not Bundle resultBundle))
        {
            response = new()
            {
                Outcome = searchResp.Outcome ?? SerializationUtils.BuildOutcomeForRequest(
                    HttpStatusCode.InternalServerError,
                    "System search failed"),
                StatusCode = HttpStatusCode.InternalServerError,
            };
            return false;
        }

        // we are done if there are no results found
        if (resultBundle.Total == 0)
        {
            response = new()
            {
                Outcome = SerializationUtils.BuildOutcomeForRequest(
                    HttpStatusCode.NotFound,
                    "No matches found for system delete"),
                StatusCode = HttpStatusCode.NotFound,
            };
            return false;
        }

        // TODO(ginoc): Determine if we want to support conditional-delete-multiple
        if (resultBundle.Total > 1)
        {
            response = new()
            {
                Outcome = SerializationUtils.BuildOutcomeForRequest(
                    HttpStatusCode.PreconditionFailed,
                    $"Too many matches found for system delete: ({resultBundle.Total})",
                    OperationOutcome.IssueType.MultipleMatches),
                StatusCode = HttpStatusCode.PreconditionFailed,
            };
            return false;
        }

        Resource? match = resultBundle.Entry.First().Resource;

        if (match == null)
        {
            response = new()
            {
                Outcome = SerializationUtils.BuildOutcomeForRequest(
                    HttpStatusCode.InternalServerError,
                    $"Resource ({resultBundle.Entry.First().FullUrl}) not accessible post search!",
                    OperationOutcome.IssueType.Processing),
                StatusCode = HttpStatusCode.InternalServerError,
            };
            return false;
        }

        IEnumerable<IFhirInteractionHook> hooks = GetHooks(ctx.ResourceType, Common.StoreInteractionCodes.SystemDeleteConditional);

        if (hooks?.Any() ?? false)
        {
            foreach (IFhirInteractionHook hook in hooks)
            {
                if (hook.HookRequestStates.Contains(Common.HookRequestStateCodes.Pre))
                {
                    _ = hook.DoInteractionHook(
                            ctx,
                            this,
                            _store[ctx.ResourceType],
                            match,
                            out FhirResponseContext hr);

                    // check for the hook indicating processing is complete
                    if (hr.StatusCode != null)
                    {
                        response = hr;
                        return true;
                    }

                    // if the hook modified the resource, use that moving forward
                    if (hr.Resource != null)
                    {
                        match = (Resource)hr.Resource;
                    }
                }
            }
        }

        string resourceType = match.TypeName;
        string id = match.Id;

        // attempt delete
        Resource? resource = _store[resourceType].InstanceDelete(id, _protectedResources);

        if (hooks?.Any() ?? false)
        {
            Resource? sForHook = (Resource?)resource?.DeepCopy();

            foreach (IFhirInteractionHook hook in hooks)
            {
                if (hook.HookRequestStates.Contains(Common.HookRequestStateCodes.Post))
                {
                    _ = hook.DoInteractionHook(
                            ctx,
                            this,
                            _store[ctx.ResourceType],
                            sForHook,
                            out FhirResponseContext hr);

                    // check for the hook indicating processing is complete
                    if (hr.StatusCode != null)
                    {
                        response = hr;
                        return true;
                    }

                    // if the hook modified the resource, use that moving forward
                    if (hr.Resource != null)
                    {
                        sForHook = (Resource)hr.Resource;
                        resource = sForHook;
                    }
                }
            }
        }

        if (resource == null)
        {
            response = new()
            {
                Outcome = SerializationUtils.BuildOutcomeForRequest(
                    HttpStatusCode.InternalServerError,
                    $"Matched delete resource {id} could not be deleted"),
                StatusCode = HttpStatusCode.InternalServerError,
            };
            return false;
        }

        response = new()
        {
            Resource = resource,
            ResourceType = resourceType,
            Id = id,
            Outcome = SerializationUtils.BuildOutcomeForRequest(HttpStatusCode.OK, $"Deleted {resourceType}/{id}"),
            StatusCode = HttpStatusCode.OK,
        };
        return true;
    }

    /// <summary>Type delete.</summary>
    /// <param name="ctx">     The request context.</param>
    /// <param name="response">[out] The serialized resource.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    public bool TypeDelete(
        FhirRequestContext ctx,
        out FhirResponseContext response)
    {
        bool success = DoTypeDelete(
            ctx,
            out response);

        string sr = response.Resource == null ? string.Empty : SerializationUtils.SerializeFhir((Resource)response.Resource, ctx.DestinationFormat, ctx.SerializePretty);
        string so = response.Outcome == null ? string.Empty : SerializationUtils.SerializeFhir((Resource)response.Outcome, ctx.DestinationFormat, ctx.SerializePretty);

        response = response with
        {
            MimeType = ctx.DestinationFormat,
            SerializedResource = sr,
            SerializedOutcome = so,
        };

        return success;
    }

    /// <summary>Executes the type delete operation.</summary>
    /// <param name="ctx">     The request context.</param>
    /// <param name="response">[out] The response data.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    internal bool DoTypeDelete(
        FhirRequestContext ctx,
        out FhirResponseContext response)
    {
        if (string.IsNullOrEmpty(ctx.ResourceType))
        {
            response = new()
            {
                Outcome = SerializationUtils.BuildOutcomeForRequest(
                    HttpStatusCode.BadRequest,
                    "Resource type is required for type-delete interactions",
                    OperationOutcome.IssueType.Structure),
                StatusCode = HttpStatusCode.BadRequest,
            };
            return false;
        }

        if (!_store.ContainsKey(ctx.ResourceType))
        {
            response = new()
            {
                Outcome = SerializationUtils.BuildOutcomeForRequest(
                    HttpStatusCode.NotFound,
                    $"Resource type: {ctx.ResourceType} is not supported",
                    OperationOutcome.IssueType.NotSupported),
                StatusCode = HttpStatusCode.NotFound,
            };
            return false;
        }

        bool success = DoTypeSearch(ctx, out FhirResponseContext searchResp);

        // check for failed search
        if ((!success) ||
            (searchResp.Resource == null) ||
            (searchResp.Resource is not Bundle resultBundle))
        {
            response = new()
            {
                Outcome = searchResp.Outcome ?? SerializationUtils.BuildOutcomeForRequest(
                    HttpStatusCode.InternalServerError,
                    $"Type search against {ctx.ResourceType} failed"),
                StatusCode = searchResp.StatusCode ?? HttpStatusCode.InternalServerError,
            };
            return false;
        }

        // we are done if there are no results found
        if (resultBundle.Total == 0)
        {
            response = new()
            {
                Outcome = SerializationUtils.BuildOutcomeForRequest(
                    HttpStatusCode.NotFound,
                    $"No matches found for type ({ctx.ResourceType}) delete"),
                StatusCode = HttpStatusCode.NotFound,
            };
            return false;
        }

        // TODO(ginoc): Determine if we want to support conditional-delete-multiple
        if (resultBundle.Total > 1)
        {
            response = new()
            {
                Outcome = SerializationUtils.BuildOutcomeForRequest(
                    HttpStatusCode.PreconditionFailed,
                    $"Too many matches found for type ({ctx.ResourceType}) delete: ({resultBundle.Total})",
                    OperationOutcome.IssueType.MultipleMatches),
                StatusCode = HttpStatusCode.PreconditionFailed,
            };
            return false;
        }

        Resource? match = resultBundle.Entry.First().Resource;

        if (match == null)
        {
            response = new()
            {
                Outcome = SerializationUtils.BuildOutcomeForRequest(
                    HttpStatusCode.InternalServerError,
                    $"Resource ({resultBundle.Entry.First().FullUrl}) not accessible post search!",
                    OperationOutcome.IssueType.Processing),
                StatusCode = HttpStatusCode.InternalServerError,
            };
            return false;
        }

        IEnumerable<IFhirInteractionHook> hooks = GetHooks(
            ctx.ResourceType,
            new Common.StoreInteractionCodes[] 
            { 
                Common.StoreInteractionCodes.TypeDeleteConditional,
                Common.StoreInteractionCodes.TypeDeleteConditionalSingle,
                Common.StoreInteractionCodes.TypeDeleteConditionalMultiple,
            });
        if (hooks?.Any() ?? false)
        {
            foreach (IFhirInteractionHook hook in hooks)
            {
                if (hook.HookRequestStates.Contains(Common.HookRequestStateCodes.Pre))
                {
                    _ = hook.DoInteractionHook(
                            ctx,
                            this,
                            _store[ctx.ResourceType],
                            match,
                            out FhirResponseContext hr);

                    // check for the hook indicating processing is complete
                    if (hr.StatusCode != null)
                    {
                        response = hr;
                        return true;
                    }

                    // if the hook modified the resource, use that moving forward
                    if (hr.Resource != null)
                    {
                        match = (Resource)hr.Resource;
                    }
                }
            }
        }

        string resourceType = match.TypeName;
        string id = match.Id;

        // attempt delete
        Resource? resource = _store[resourceType].InstanceDelete(id, _protectedResources);

        if (hooks?.Any() ?? false)
        {
            Resource? sForHook = (Resource?)resource?.DeepCopy();

            foreach (IFhirInteractionHook hook in hooks)
            {
                if (hook.HookRequestStates.Contains(Common.HookRequestStateCodes.Post))
                {
                    _ = hook.DoInteractionHook(
                            ctx,
                            this,
                            _store[ctx.ResourceType],
                            sForHook,
                            out FhirResponseContext hr);

                    // check for the hook indicating processing is complete
                    if (hr.StatusCode != null)
                    {
                        response = hr;
                        return true;
                    }

                    // if the hook modified the resource, use that moving forward
                    if (hr.Resource != null)
                    {
                        sForHook = (Resource)hr.Resource;
                        resource = sForHook;
                    }
                }
            }
        }

        if (resource == null)
        {
            response = new()
            {
                Outcome = SerializationUtils.BuildOutcomeForRequest(
                    HttpStatusCode.InternalServerError,
                    $"Matched delete resource {id} could not be deleted"),
                StatusCode = HttpStatusCode.InternalServerError,
            };
            return false;
        }

        response = new()
        {
            Resource = resource,
            ResourceType = resourceType,
            Id = id,
            Outcome = SerializationUtils.BuildOutcomeForRequest(HttpStatusCode.OK, $"Deleted {resourceType}/{id}"),
            StatusCode = HttpStatusCode.OK,
        };
        return true;
    }

    /// <summary>Type search.</summary>
    /// <param name="ctx">     The request context.</param>
    /// <param name="response">[out] The response data.</param>
    /// <returns>A HttpStatusCode.</returns>
    public bool TypeSearch(
        FhirRequestContext ctx,
        out FhirResponseContext response)
    {
        bool success = DoTypeSearch(
            ctx,
            out response);

        string sr = response.Resource == null ? string.Empty : SerializationUtils.SerializeFhir((Resource)response.Resource, ctx.DestinationFormat, ctx.SerializePretty);
        string so = response.Outcome == null ? string.Empty : SerializationUtils.SerializeFhir((Resource)response.Outcome, ctx.DestinationFormat, ctx.SerializePretty);

        response = response with
        {
            MimeType = ctx.DestinationFormat,
            SerializedResource = sr,
            SerializedOutcome = so,
        };

        return success;
    }

    /// <summary>Executes the type search operation.</summary>
    /// <param name="resourceType">Type of the resource.</param>
    /// <param name="queryString"> The query string.</param>
    /// <param name="bundle">      [out] The bundle.</param>
    /// <param name="outcome">     [out] The outcome.</param>
    /// <returns>A HttpStatusCode.</returns>
    internal bool DoTypeSearch(
        FhirRequestContext ctx,
        out FhirResponseContext response)
    {
        if (string.IsNullOrEmpty(ctx.ResourceType))
        {
            response = new()
            {
                Outcome = SerializationUtils.BuildOutcomeForRequest(
                    HttpStatusCode.BadRequest,
                    "Resource type is required for type-delete interactions",
                    OperationOutcome.IssueType.Structure),
                StatusCode = HttpStatusCode.BadRequest,
            };
            return false;
        }

        if (!_store.ContainsKey(ctx.ResourceType))
        {
            response = new()
            {
                Outcome = SerializationUtils.BuildOutcomeForRequest(
                    HttpStatusCode.NotFound,
                    $"Resource type: {ctx.ResourceType} is not supported",
                    OperationOutcome.IssueType.NotSupported),
                StatusCode = HttpStatusCode.NotFound,
            };
            return false;
        }

        IEnumerable<IFhirInteractionHook> hooks = GetHooks(ctx.ResourceType, Common.StoreInteractionCodes.TypeSearch);
        if (hooks?.Any() ?? false)
        {
            foreach (IFhirInteractionHook hook in hooks)
            {
                if (hook.HookRequestStates.Contains(Common.HookRequestStateCodes.Pre))
                {
                    _ = hook.DoInteractionHook(
                            ctx,
                            this,
                            _store[ctx.ResourceType],
                            null,
                            out FhirResponseContext hr);

                    // check for the hook indicating processing is complete
                    if (hr.StatusCode != null)
                    {
                        response = hr;
                        return true;
                    }
                }
            }
        }

        // parse search parameters
        IEnumerable<ParsedSearchParameter> parameters = ParsedSearchParameter.Parse(
            ctx.UrlQuery,
            this,
            _store[ctx.ResourceType],
            ctx.ResourceType);

        // execute search
        IEnumerable<Resource>? results = _store[ctx.ResourceType].TypeSearch(parameters);

        // parse search result parameters
        ParsedResultParameters resultParameters = new ParsedResultParameters(ctx.UrlQuery, this);

        // null results indicates failure
        if (results == null)
        {
            response = new()
            {
                Outcome = SerializationUtils.BuildOutcomeForRequest(
                    HttpStatusCode.InternalServerError,
                    $"Type Search against {ctx.ResourceType} failed",
                    OperationOutcome.IssueType.Processing),
                StatusCode = HttpStatusCode.InternalServerError,
            };
            return false;
        }

        string selfLink = $"{_config.BaseUrl}/{ctx.ResourceType}";
        string selfSearchParams = string.Join('&', parameters.Where(p => !p.IgnoredParameter).Select(p => p.GetAppliedQueryString()));
        string selfResultParams = resultParameters.GetAppliedQueryString();

        if (!string.IsNullOrEmpty(selfSearchParams))
        {
            selfLink = selfLink + "?" + selfSearchParams;
        }

        if (!string.IsNullOrEmpty(selfResultParams))
        {
            selfLink = selfLink + (selfLink.Contains('?') ? '&' : '?') + selfResultParams;
        }

        // create our bundle for results
        Bundle bundle = new Bundle
        {
            Type = Bundle.BundleType.Searchset,
            Total = results.Count(),
            Link = new()
            {
                new Bundle.LinkComponent()
                {
                    Relation = "self",
                    Url = selfLink,
                }
            },
        };

        // TODO: check for a sort and apply to results

        HashSet<string> addedIds = new();

        foreach (Resource resource in results)
        {
            string relativeUrl = $"{resource.TypeName}/{resource.Id}";

            if (addedIds.Contains(relativeUrl))
            {
                // promote to match
                bundle.FindEntry(new ResourceReference(relativeUrl)).First().Search.Mode = Bundle.SearchEntryMode.Match;
            }
            else
            {
                // add the matched result to the bundle
                bundle.AddSearchEntry(resource, $"{_config.BaseUrl}/{relativeUrl}", Bundle.SearchEntryMode.Match);

                // track we have added this id
                addedIds.Add(relativeUrl);
            }

            // add any included resources
            AddInclusions(bundle, resource, resultParameters, addedIds);

            // check for include:iterate directives

            // add any reverse included resources
            AddReverseInclusions(bundle, resource, resultParameters, addedIds);
        }

        if (hooks?.Any() ?? false)
        {
            foreach (IFhirInteractionHook hook in hooks)
            {
                if (hook.HookRequestStates.Contains(Common.HookRequestStateCodes.Post))
                {
                    _ = hook.DoInteractionHook(
                            ctx,
                            this,
                            _store[ctx.ResourceType],
                            bundle,
                            out FhirResponseContext hr);

                    // check for the hook indicating processing is complete
                    if (hr.StatusCode != null)
                    {
                        response = hr;
                        return true;
                    }

                    // if the hook modified the resource, use that moving forward
                    if ((hr.Resource != null) &&
                        (hr.Resource is Bundle opBundle))
                    {
                        bundle = opBundle;
                    }
                }
            }
        }

        response = new()
        {
            Resource = bundle,
            ResourceType = "Bundle",
            Outcome = SerializationUtils.BuildOutcomeForRequest(HttpStatusCode.OK, $"System search successful"),
            StatusCode = HttpStatusCode.OK,
        };
        return true;
    }

    /// <summary>System search.</summary>
    /// <param name="ctx">     The request context.</param>
    /// <param name="response">[out] The response data.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    public bool SystemSearch(
        FhirRequestContext ctx,
        out FhirResponseContext response)
    {
        bool success = DoSystemSearch(
            ctx,
            out response);

        string sr = response.Resource == null ? string.Empty : SerializationUtils.SerializeFhir((Resource)response.Resource, ctx.DestinationFormat, ctx.SerializePretty);
        string so = response.Outcome == null ? string.Empty : SerializationUtils.SerializeFhir((Resource)response.Outcome, ctx.DestinationFormat, ctx.SerializePretty);

        response = response with
        {
            MimeType = ctx.DestinationFormat,
            SerializedResource = sr,
            SerializedOutcome = so,
        };

        return success;
    }

    /// <summary>Executes the system search operation.</summary>
    /// <param name="ctx">     The request context.</param>
    /// <param name="response">[out] The response data.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    internal bool DoSystemSearch(
        FhirRequestContext ctx,
        out FhirResponseContext response)
    {
        string[] resourceTypes = Array.Empty<string>();

        // check for _type parameter
        System.Collections.Specialized.NameValueCollection query = System.Web.HttpUtility.ParseQueryString(ctx.UrlQuery);

        foreach (string key in query)
        {
            if (!key.Equals("_type", StringComparison.Ordinal))
            {
                continue;
            }

            resourceTypes = query[key]!.Split(',');
        }

        if (!resourceTypes.Any())
        {
            response = new()
            {
                Outcome = SerializationUtils.BuildOutcomeForRequest(
                    HttpStatusCode.Forbidden,
                    $"System search with no resource types is too costly.",
                    OperationOutcome.IssueType.TooCostly),
                StatusCode = HttpStatusCode.Forbidden,
            };
            return false;
        }

        IEnumerable<IFhirInteractionHook> hooks = GetHooks(ctx.ResourceType, Common.StoreInteractionCodes.SystemSearch);
        if (hooks?.Any() ?? false)
        {
            foreach (IFhirInteractionHook hook in hooks)
            {
                if (hook.HookRequestStates.Contains(Common.HookRequestStateCodes.Pre))
                {
                    _ = hook.DoInteractionHook(
                            ctx,
                            this,
                            _store[ctx.ResourceType],
                            null,
                            out FhirResponseContext hr);

                    // check for the hook indicating processing is complete
                    if (hr.StatusCode != null)
                    {
                        response = hr;
                        return true;
                    }
                }
            }
        }

        List<IEnumerable<ParsedSearchParameter>> allParameters = new();
        List<IEnumerable<Resource>> allResults = new();

        foreach (string resourceType in resourceTypes)
        {
            // parse search parameters
            IEnumerable<ParsedSearchParameter> parameters = ParsedSearchParameter.Parse(
                ctx.UrlQuery,
                this,
                _store[resourceType],
                resourceType);

            // execute search
            IEnumerable<Resource>? results = _store[resourceType].TypeSearch(parameters);

            // null results indicates failure
            if (results == null)
            {
                response = new()
                {
                    Outcome = SerializationUtils.BuildOutcomeForRequest(
                        HttpStatusCode.InternalServerError,
                        $"System search into {resourceType} failed"),
                    StatusCode = HttpStatusCode.InternalServerError,
                };
                return false;
            }

            allParameters.Add(parameters);
            allResults.Add(results);
        }

        // parse search result parameters
        ParsedResultParameters resultParameters = new ParsedResultParameters(ctx.UrlQuery, this);

        // filter parameters from use across all performed searches
        IEnumerable<ParsedSearchParameter> filteredParameters = allParameters.SelectMany(e => e.Select(p => p)).DistinctBy(p => p.Name);

        string selfLink = $"{_config.BaseUrl}";
        string selfSearchParams = string.Join('&', filteredParameters.Where(p => !p.IgnoredParameter).Select(p => p.GetAppliedQueryString()));
        string selfResultParams = resultParameters.GetAppliedQueryString();

        if (!string.IsNullOrEmpty(selfSearchParams))
        {
            selfLink = selfLink + "?" + selfSearchParams;
        }

        if (!string.IsNullOrEmpty(selfResultParams))
        {
            selfLink = selfLink + (selfLink.Contains('?') ? '&' : '?') + selfResultParams;
        }

        // create our bundle for results
        Bundle bundle = new()
        {
            Type = Bundle.BundleType.Searchset,
            Link = new()
            {
                new Bundle.LinkComponent()
                {
                    Relation = "self",
                    Url = selfLink,
                }
            },
        };

        // TODO: check for a sort and apply to results

        HashSet<string> addedIds = new();
        int resultCount = 0;

        foreach (Resource resource in allResults.SelectMany(e => e.Select(r => r)))
        {
            resultCount++;

            string relativeUrl = $"{resource.TypeName}/{resource.Id}";

            if (addedIds.Contains(relativeUrl))
            {
                // promote to match
                bundle.FindEntry(new ResourceReference(relativeUrl)).First().Search.Mode = Bundle.SearchEntryMode.Match;
            }
            else
            {
                // add the matched result to the bundle
                bundle.AddSearchEntry(resource, $"{_config.BaseUrl}/{relativeUrl}", Bundle.SearchEntryMode.Match);

                // track we have added this id
                addedIds.Add(relativeUrl);
            }

            // add any included resources
            AddInclusions(bundle, resource, resultParameters, addedIds);

            // check for include:iterate directives

            // add any reverse included resources
            AddReverseInclusions(bundle, resource, resultParameters, addedIds);
        }

        if (hooks?.Any() ?? false)
        {
            foreach (IFhirInteractionHook hook in hooks)
            {
                if (hook.HookRequestStates.Contains(Common.HookRequestStateCodes.Post))
                {
                    _ = hook.DoInteractionHook(
                            ctx,
                            this,
                            _store[ctx.ResourceType],
                            bundle,
                            out FhirResponseContext hr);

                    // check for the hook indicating processing is complete
                    if (hr.StatusCode != null)
                    {
                        response = hr;
                        return true;
                    }

                    // if the hook modified the resource, use that moving forward
                    if ((hr.Resource != null) &&
                        (hr.Resource is Bundle opBundle))
                    {
                        bundle = opBundle;
                    }
                }
            }
        }

        // set the total number of results aggregated across types
        bundle.Total = resultCount;

        response = new()
        {
            Resource = bundle,
            ResourceType = "Bundle",
            Outcome = SerializationUtils.BuildOutcomeForRequest(HttpStatusCode.OK, $"System search successful"),
            StatusCode = HttpStatusCode.OK,
        };
        return true;
    }

    private void AddIterativeInclusions()
    {
        // TODO(ginoc): Add iterative inclusions!
    }

    /// <summary>Enumerates resolve reverse inclusions in this collection.</summary>
    /// <param name="focus">           The focus.</param>
    /// <param name="resultParameters">Options for controlling the result.</param>
    /// <param name="addedIds">        List of identifiers for the added.</param>
    /// <returns>
    /// An enumerator that allows foreach to be used to process resolve reverse inclusions in this
    /// collection.
    /// </returns>
    internal IEnumerable<Resource> ResolveReverseInclusions(
        Resource focus,
        ParsedResultParameters resultParameters,
        HashSet<string> addedIds)
    {
        List<Resource> inclusions = new();

        string matchId = $"{focus.TypeName}/{focus.Id}";

        foreach ((string reverseResourceType, List<ModelInfo.SearchParamDefinition> sps) in resultParameters.ReverseInclusions)
        {
            if (!_store.ContainsKey(reverseResourceType))
            {
                continue;
            }

            foreach (ModelInfo.SearchParamDefinition sp in sps)
            {
                List<ParsedSearchParameter> parameters = new()
                {
                    new ParsedSearchParameter(
                        this,
                        _store[reverseResourceType],
                        reverseResourceType,
                        sp.Name!,
                        string.Empty,
                        SearchModifierCodes.None,
                        matchId,
                        sp),
                };

                // execute search
                IEnumerable<Resource>? results = _store[reverseResourceType].TypeSearch(parameters);

                if (results?.Any() ?? false)
                {
                    foreach (Resource revIncludeRes in results)
                    {
                        string id = $"{revIncludeRes.TypeName}/{revIncludeRes.Id}";

                        if (!addedIds.Contains(id))
                        {
                            // add the result to the list
                            inclusions.Add(revIncludeRes);

                            // track we have added this id
                            addedIds.Add(id);
                        }
                    }
                }
            }
        }

        return inclusions;
    }

    /// <summary>Adds a reverse inclusions.</summary>
    /// <param name="bundle">          The bundle.</param>
    /// <param name="resource">        [out] The resource.</param>
    /// <param name="resultParameters">Options for controlling the result.</param>
    /// <param name="addedIds">        List of identifiers for the added.</param>
    private void AddReverseInclusions(
        Bundle bundle,
        Resource resource,
        ParsedResultParameters resultParameters,
        HashSet<string> addedIds)
    {
        if (!resultParameters.ReverseInclusions.Any())
        {
            return;
        }

        IEnumerable<Resource> reverseInclusions = ResolveReverseInclusions(resource, resultParameters, addedIds);

        foreach (Resource inclusion in reverseInclusions)
        {
            // add the matched result to the bundle
            bundle.AddSearchEntry(inclusion, $"{_config.BaseUrl}/{resource.TypeName}/{resource.Id}", Bundle.SearchEntryMode.Include);
        }
    }

    /// <summary>Enumerates resolve inclusions in this collection.</summary>
    /// <param name="focus">           The focus.</param>
    /// <param name="focusTE">         The focus te.</param>
    /// <param name="resultParameters">Options for controlling the result.</param>
    /// <param name="addedIds">        List of identifiers for the added.</param>
    /// <param name="fpContext">       The context.</param>
    /// <returns>
    /// An enumerator that allows foreach to be used to process resolve inclusions in this collection.
    /// </returns>
    internal IEnumerable<Resource> ResolveInclusions(
        Resource focus,
        ITypedElement focusTE,
        ParsedResultParameters resultParameters,
        HashSet<string> addedIds,
        FhirEvaluationContext? fpContext)
    {
        // check for include directives
        if (!resultParameters.Inclusions.ContainsKey(focus.TypeName))
        {
            return Array.Empty<Resource>();
        }

        if (fpContext == null)
        {
            fpContext = new FhirEvaluationContext(focusTE.ToScopedNode())
            {
                TerminologyService = _terminology,
                ElementResolver = Resolve,
            };
        }

        List<Resource> inclusions = new();

        foreach (ModelInfo.SearchParamDefinition sp in resultParameters.Inclusions[focus.TypeName])
        {
            if (string.IsNullOrEmpty(sp.Expression))
            {
                continue;
            }

            IEnumerable<ITypedElement> extracted = GetCompiledSearchParameter(
                focus.TypeName,
                sp.Name ?? string.Empty,
                sp.Expression)
                .Invoke(focusTE, fpContext);

            if (!extracted.Any())
            {
                continue;
            }

            foreach (ITypedElement element in extracted)
            {
                switch (element.InstanceType)
                {
                    case "Reference":
                    case "ResourceReference":
                        break;
                    default:
                        // skip non references
                        Console.WriteLine($"AddInclusions <<< cannot include based on element of type {element.InstanceType}");
                        continue;
                }

                ResourceReference reference = element.ToPoco<ResourceReference>();
                Resource? resolved = null;

                if ((!string.IsNullOrEmpty(reference.Reference)) &&
                    TryResolveAsResource(reference.Reference, out resolved) &&
                    (resolved != null))
                {
                    if (sp.Target?.Any() ?? false)
                    {
                        // verify this is a valid target type
                        Hl7.Fhir.Model.ResourceType? rt = ModelInfo.FhirTypeNameToResourceType(resolved.TypeName);

                        if (rt == null ||
                            !sp.Target.Contains(rt.Value))
                        {
                            continue;
                        }
                    }

                    string includedId = $"{resolved.TypeName}/{resolved.Id}";
                    if (addedIds.Contains(includedId))
                    {
                        continue;
                    }

                    // add the matched result
                    inclusions.Add(resolved);

                    // track we have added this id
                    addedIds.Add(includedId);

                    continue;
                }
                
                if (reference.Identifier != null)
                {
                    // check if a type was specified
                    if (!string.IsNullOrEmpty(reference.Type) && _store.ContainsKey(reference.Type))
                    {
                        if (_store[reference.Type].TryResolveIdentifier(reference.Identifier.System, reference.Identifier.Value, out resolved) &&
                            (resolved != null))
                        {
                            string includedId = $"{resolved.TypeName}/{resolved.Id}";
                            if (addedIds.Contains(includedId))
                            {
                                continue;
                            }

                            // add the matched result
                            inclusions.Add(resolved);

                            // track we have added this id
                            addedIds.Add(includedId);

                            continue;
                        }
                    }

                    // look through all resources
                    foreach (string resourceType in _store.Keys)
                    {
                        if (_store[resourceType].TryResolveIdentifier(reference.Identifier.System, reference.Identifier.Value, out resolved) &&
                            (resolved != null))
                        {
                            string includedId = $"{resolved.TypeName}/{resolved.Id}";
                            if (addedIds.Contains(includedId))
                            {
                                continue;
                            }

                            // add the matched result
                            inclusions.Add(resolved);

                            // track we have added this id
                            addedIds.Add(includedId);

                            continue;
                        }
                    }
                }
            }
        }

        return inclusions;
    }

    /// <summary>Adds the inclusions.</summary>
    /// <param name="bundle">          The bundle.</param>
    /// <param name="resource">        [out] The resource.</param>
    /// <param name="resultParameters">Options for controlling the result.</param>
    /// <param name="addedIds">        List of identifiers for the added.</param>
    private void AddInclusions(
        Bundle bundle,
        Resource resource,
        ParsedResultParameters resultParameters,
        HashSet<string> addedIds)
    {
        // check for include directives
        if (!resultParameters.Inclusions.ContainsKey(resource.TypeName))
        {
            return;
        }

        ITypedElement resourceTE = resource.ToTypedElement();

        FhirEvaluationContext fpContext = new FhirEvaluationContext(resourceTE.ToScopedNode())
        { 
            TerminologyService = _terminology,
            ElementResolver = Resolve,
        };

        IEnumerable<Resource> inclusions = ResolveInclusions(resource, resourceTE, resultParameters, addedIds, fpContext);

        foreach (Resource inclusion in inclusions)
        {
            // add the matched result to the bundle
            bundle.AddSearchEntry(inclusion, $"{_config.BaseUrl}/{resource.TypeName}/{resource.Id}", Bundle.SearchEntryMode.Include);
        }
    }

    /// <summary>Gets the server capabilities.</summary>
    /// <param name="ctx">     The authorization information, if available.</param>
    /// <param name="response">[out] The response data.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    public bool GetMetadata(
        FhirRequestContext ctx,
        out FhirResponseContext response)
    {
        bool success = DoGetMetadata(ctx, out response);

        string sr = response.Resource == null ? string.Empty : SerializationUtils.SerializeFhir((Resource)response.Resource, ctx.DestinationFormat, ctx.SerializePretty);
        string so = response.Outcome == null ? string.Empty : SerializationUtils.SerializeFhir((Resource)response.Outcome, ctx.DestinationFormat, ctx.SerializePretty);

        response = response with
        {
            MimeType = ctx.DestinationFormat,
            SerializedResource = sr,
            SerializedOutcome = so,
        };

        return success;
    }

    /// <summary>Executes the get metadata operation.</summary>
    /// <param name="ctx">     The request context.</param>
    /// <param name="response">[out] The response data.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    internal bool DoGetMetadata(
        FhirRequestContext ctx,
        out FhirResponseContext response)
    {
        IEnumerable<IFhirInteractionHook> hooks = GetHooks(ctx.ResourceType, Common.StoreInteractionCodes.SystemCapabilities);
        if (hooks?.Any() ?? false)
        {
            foreach (IFhirInteractionHook hook in hooks)
            {
                if (hook.HookRequestStates.Contains(Common.HookRequestStateCodes.Pre))
                {
                    _ = hook.DoInteractionHook(
                            ctx,
                            this,
                            _store["CapabilityStatement"],
                            null,
                            out FhirResponseContext hr);

                    // check for the hook indicating processing is complete
                    if (hr.StatusCode != null)
                    {
                        response = hr;
                        return true;
                    }
                }
            }
        }

        Hl7.Fhir.Model.Resource? r;

        if (_capabilitiesAreStale)
        {
            r = UpdateCapabilities();
        }
        else
        {
            // bypass read to avoid instance read hooks (firing meta hooks)
            r = (Resource)((IReadOnlyDictionary<string, Hl7.Fhir.Model.Resource>)_store["CapabilityStatement"])[_capabilityStatementId].DeepCopy();
        }

        if (hooks?.Any() ?? false)
        {
            Resource? sForHook = (Resource?)r?.DeepCopy();

            foreach (IFhirInteractionHook hook in hooks)
            {
                if (hook.HookRequestStates.Contains(Common.HookRequestStateCodes.Post))
                {
                    _ = hook.DoInteractionHook(
                            ctx,
                            this,
                            _store["CapabilityStatement"],
                            sForHook,
                            out FhirResponseContext hr);

                    // check for the hook indicating processing is complete
                    if (hr.StatusCode != null)
                    {
                        response = hr;
                        return true;
                    }

                    // if the hook modified the resource, use that moving forward
                    if (hr.Resource != null)
                    {
                        sForHook = (Resource)hr.Resource;
                        r = sForHook;
                    }
                }
            }
        }

        if (r == null)
        {
            response = new()
            {
                Outcome = SerializationUtils.BuildOutcomeForRequest(
                    HttpStatusCode.InternalServerError,
                    $"CapabilityStatement could not be retrieved",
                    OperationOutcome.IssueType.Exception),
                StatusCode = HttpStatusCode.InternalServerError,
            };

            return false;
        }

        response = new()
        {
            Resource = r,
            ResourceType = "CapabilityStatement",
            Id = _capabilityStatementId,
            Outcome = SerializationUtils.BuildOutcomeForRequest(
                HttpStatusCode.OK,
                $"Retrieved current CapabilityStatement",
                OperationOutcome.IssueType.Success),
            ETag = string.IsNullOrEmpty(r.Meta?.VersionId) ? string.Empty : $"W/\"{r.Meta.VersionId}\"",
            LastModified = r.Meta?.LastUpdated == null ? string.Empty : r.Meta.LastUpdated.Value.UtcDateTime.ToString("r"),
            Location = string.IsNullOrEmpty(r.Id) ? string.Empty : $"{_config.BaseUrl}/{r.TypeName}/{r.Id}",
            StatusCode = HttpStatusCode.OK,
        };

        return true;
    }

    public record class FeatureQueryResponse
    {
        /// <summary>
        /// name: name of the feature (uri)
        /// </summary>
        public required string Name { get; init; }
        
        /// <summary>
        /// name: present if provided, used to match responses to requests (uri)
        /// </summary>
        public required string? Context { get; init; }
        
        /// <summary>
        /// processing-status: code from the server about processing the request (e.g., all-ok, not-supported, etc.)
        /// </summary>
        public required string ProcessingStatus { get; init; }
        
        /// <summary>
        ///  value:
        ///     if provided in input: the value requested (datatype as defined by the feature) (even if processing fails) (if read from HTTP Query Parameter, default to fhir string type)
        ///     if not provided: the value of the feature (can have multiple repetitions) (uses datatype of feature)
        /// </summary>
        public required List<DataType> Value { get; init; }
        
        /// <summary>
        /// matches:
        ///     only present if processing was successful (all-ok)
        ///     if a value is provided, does the supplied value match the server feature-supported value
        ///     if a value is not provided, NOT PRESENT
        /// </summary>
        public required bool? Matches { get; init; }
    }

    public bool TryQueryCapabilityFeature(
        string featureName,
        string context,
        DataType? inputValue,
        string? inputRawValue, 
        out FeatureQueryResponse response)
    {
        CapabilityStatement cs;
        
        if (_capabilitiesAreStale)
        {
            cs = UpdateCapabilities();
        }
        else
        {
            // bypass read to avoid instance read hooks (firing meta hooks)
            cs = (CapabilityStatement)((IReadOnlyDictionary<string, Hl7.Fhir.Model.Resource>)_store["CapabilityStatement"])[_capabilityStatementId];
        }
        
        switch (featureName)
        {
            case "instantiates":
                return TryTestCapabilityFeatureInstantiates(cs, featureName, context, inputValue, inputRawValue, out response);
            
            case "read":
                return TryTestCapabilityFeatureInteraction(cs, featureName, context, inputValue, inputRawValue, CapabilityStatement.TypeRestfulInteraction.Read, out response);

            case "create":
                return TryTestCapabilityFeatureInteraction(cs, featureName, context, inputValue, inputRawValue, CapabilityStatement.TypeRestfulInteraction.Create, out response);

            case "update":
                return TryTestCapabilityFeatureInteraction(cs, featureName, context, inputValue, inputRawValue, CapabilityStatement.TypeRestfulInteraction.Update, out response);
            
            default:
                response = new()
                {
                    Name = featureName,
                    Context = string.IsNullOrEmpty(context) ? null : context,
                    Value = inputValue != null ? [ inputValue ] : !string.IsNullOrEmpty(inputRawValue) ? [new FhirString(inputRawValue)] : [],
                    Matches = null,
                    ProcessingStatus = "unknown",
                };
                return true;
        }
    }

    private bool TryTestCapabilityFeatureInteraction(
        CapabilityStatement cs,
        string featureName,
        string context,
        DataType? inputValue,
        string? inputRawValue,
        CapabilityStatement.TypeRestfulInteraction interaction,
        out FeatureQueryResponse response)
    {
        // check for no or 'all' context and no value
        if ((string.IsNullOrEmpty(context) || (context == "*")) &&
            (inputValue == null) &&
            string.IsNullOrEmpty(inputRawValue))
        {
            // List<DataType> rValues = cs.Rest
            //     .SelectMany(r => r.Resource)
            //     .Where(r => r.Interaction.Any(i => i.Code == interaction))
            //     .Select(r => (DataType)new Code(r.Type))
            //     .ToList();
            
            response = new()
            {
                Name = featureName,
                Context = context,
                Value = [ new FhirBoolean(true), new FhirBoolean(false)],
                // Value = rValues,
                Matches = null, // cs.Rest.Any(rest => rest.Resource.Any(r => r.Interaction.Any(i => i.Code == interaction))),
                ProcessingStatus = "all-ok",
            };
            return true;
        }
        
        // check for specific context and no value
        if ((inputValue == null) &&
            string.IsNullOrEmpty(inputRawValue))
        {
            // return allowed values
            response = new()
            {
                Name = featureName,
                Context = context,
                Value = [ new FhirBoolean(true), new FhirBoolean(false)],
                Matches = null, // cs.Rest.Any(rest => rest.Resource.Any(r => (r.Type == context) && r.Interaction.Any(i => i.Code == interaction))),
                ProcessingStatus = "all-ok",
            };
            return true;
        }
        
        // check for valid values
        bool? testValue = inputValue switch
        {
            FhirBoolean fb => fb.Value,
            FhirString fs => bool.TryParse(fs.Value, out bool b) ? b : null,
            _ => null,
        };

        testValue ??= string.IsNullOrEmpty(inputRawValue) 
            ? null 
            : bool.TryParse(inputRawValue, out bool bv) 
                ? bv 
                : null; 
        
        // check for invalid values
        if (testValue == null)
        {
            response = new()
            {
                Name = featureName,
                Context = context,
                Value = inputValue != null ? [ inputValue] : !string.IsNullOrEmpty(inputRawValue) ? [new FhirString(inputRawValue)] : [],
                Matches = null,
                ProcessingStatus = "invalid-value",
            };
            return true;
        }
        
        // check for no or 'all' context and value (tested above)
        if (string.IsNullOrEmpty(context) || (context == "*"))
        {
            response = new()
            {
                Name = featureName,
                Context = context,
                Value = [ new FhirBoolean(testValue) ],
                Matches = cs.Rest.Any(rest => rest.Resource.Any(r => r.Interaction.Any(i => i.Code == interaction) == testValue)),
                ProcessingStatus = "all-ok",
            };
            return true;
        }
        
        // have context and value
        response = new()
        {
            Name = featureName,
            Context = context,
            Value = [ new FhirBoolean(testValue) ],
            Matches = cs.Rest.Any(rest => rest.Resource.Any(r => (r.Type == context) && (r.Interaction.Any(i => i.Code == interaction) == testValue))),
            ProcessingStatus = "all-ok",
        };
        return true;
    }
    
    private bool TryTestCapabilityFeatureInstantiates(
        CapabilityStatement cs, 
        string featureName,
        string context, 
        DataType? inputValue, 
        string? inputRawValue, 
        out FeatureQueryResponse response)
    {
        // check for specific value request
        string testValue = !string.IsNullOrEmpty(inputRawValue)
            ? inputRawValue
            : inputValue?.ToString() ?? string.Empty;
        bool? testBool = bool.TryParse(testValue, out bool b) ? b : null;

        // check for context and fail it if present
        if (!string.IsNullOrEmpty(context))
        {
            response = new()
            {
                Name = featureName,
                Context = context,
                Value =  testBool != null ? [ new FhirBoolean(testBool) ] : [],
                Matches = null,
                ProcessingStatus = "invalid-context",
            };
            return true;
        }
        
        // check for enumeration request
        if (string.IsNullOrEmpty(inputRawValue) && (inputValue == null))
        {
            List<DataType> rValues = cs.Instantiates.Select(i => (DataType)new Canonical(i)).ToList();

            if (rValues.Count == 0)
            {
                rValues = inputValue != null ? [inputValue] : !string.IsNullOrEmpty(inputRawValue) ? [new FhirString(inputRawValue)] : [];
            }
            
            response = new()
            {
                Name = featureName,
                Context = null,
                Value = rValues,
                Matches = null,
                ProcessingStatus = "all-ok",
            };

            return true;
        }

        Canonical testCanonical = new Canonical(testValue);
        
        List<DataType> responseValues = cs.Instantiates
            .Where(i => i.Equals(testValue, StringComparison.Ordinal))
            .Select(i => (DataType)new Canonical(i))
            .ToList();

        response = new()
        {
            Name = featureName,
            Context = null,
            Value = responseValues.Count != 0 ? responseValues : [testCanonical],
            Matches = responseValues.Count != 0,
            ProcessingStatus = "all-ok",
        };
        
        return true;
    }

    /// <summary>Common to Firely version.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when one or more arguments are outside the
    ///  required range.</exception>
    /// <param name="v">The SupportedFhirVersions to process.</param>
    /// <returns>A FHIRVersion.</returns>
    private FHIRVersion CommonToFirelyVersion(FhirReleases.FhirSequenceCodes v)
    {
        switch (v)
        {
            case FhirReleases.FhirSequenceCodes.R4:
                return FHIRVersion.N4_0_1;

            case FhirReleases.FhirSequenceCodes.R4B:
                return FHIRVersion.N4_3_0;

            case FhirReleases.FhirSequenceCodes.R5:
                return FHIRVersion.N5_0_0;

            default:
                throw new ArgumentOutOfRangeException(nameof(v), $"Unsupported FHIR version: {v}");
        }
    }

    /// <summary>Updates the current capabilities of this store.</summary>
    private CapabilityStatement UpdateCapabilities()
    {
        CapabilityStatement cs = new()
        {
            Id = _capabilityStatementId,
            Url = $"{_config.BaseUrl}/CapabilityStatement/{_capabilityStatementId}",
            Name = "Capabilities" + _config.FhirVersion,
            Status = PublicationStatus.Active,
            Date = DateTimeOffset.Now.ToFhirDateTime(),
            Kind = CapabilityStatementKind.Instance,
            Software = new()
            {
                Name = "fhir-candle",
                Version = @GetType()?.Assembly?.GetName()?.Version?.ToString() ?? "0.0.0.0",
            },
            Implementation = new()
            {
                Description = "fhir-candle: A FHIR Server for testing and development",
                Url = "https://github.com/GinoCanessa/fhir-candle",
            },
            FhirVersion = CommonToFirelyVersion(_config.FhirVersion),
            Format = _config.SupportedFormats,
            Rest = new(),
        };

        // start building our rest component
        // commented-out capabilities are ones that are not yet implemented
        CapabilityStatement.RestComponent restComponent = new()
        {
            Mode = CapabilityStatement.RestfulCapabilityMode.Server,
            Resource = new(),
            Interaction = new()
            {
                new() { Code = Hl7.Fhir.Model.CapabilityStatement.SystemRestfulInteraction.Batch },
                //new() { Code = Hl7.Fhir.Model.CapabilityStatement.SystemRestfulInteraction.HistorySystem },
                new() { Code = Hl7.Fhir.Model.CapabilityStatement.SystemRestfulInteraction.SearchSystem },
                new() { Code = Hl7.Fhir.Model.CapabilityStatement.SystemRestfulInteraction.Transaction },
            },
            //SearchParam = new(),      // currently, search parameters are expanded out to all-resource
            Operation = _operations.Values
                    .Where(o => o.AllowSystemLevel)
                    .Select(o => new CapabilityStatement.OperationComponent()
                    {
                        Name = o.OperationName,
                        Definition = o.CanonicalByFhirVersion[_config.FhirVersion],
                    }).ToList(),
            //Compartment = new(),
        };

        string securityCodeSystemUrl = _config.FhirVersion switch
        {
            FhirReleases.FhirSequenceCodes.R4 => "http://terminology.hl7.org/CodeSystem/restful-security-service",
            FhirReleases.FhirSequenceCodes.R4B => "http://terminology.hl7.org/CodeSystem/restful-security-service",
            FhirReleases.FhirSequenceCodes.R5 => "http://hl7.org/fhir/restful-security-service",
            _ => "http://hl7.org/fhir/restful-security-service",
        };

        if (_config.SmartRequired || _config.SmartAllowed)
        {
            restComponent.Security = new()
            {
                Cors = true,
                Service = new() { new CodeableConcept(securityCodeSystemUrl, "SMART-on-FHIR") },
            };

            Extension ext = new()
            {
                Url = "http://fhir-registry.smarthealthit.org/StructureDefinition/oauth-uris",
                Extension = new()
                {
                    new Extension("token", new FhirUri($"{_config.BaseUrl.Replace("/fhir/", "/_smart/")}/token")),
                    new Extension("authorize", new FhirUri($"{_config.BaseUrl.Replace("/fhir/", "/_smart/")}/authorize")),
                    new Extension("register", new FhirUri($"{_config.BaseUrl.Replace("/fhir/", "/_smart/")}/register")),
                    new Extension("manage", new FhirUri($"{_config.BaseUrl.Replace("/fhir/", "/")}/clients")),
                }
            };

            restComponent.Security.Extension.Add(ext);
        }

        // add our resources
        foreach ((string resourceName, IVersionedResourceStore resourceStore) in _store)
        {
            // commented-out capabilities are ones that are not yet implemented
            CapabilityStatement.ResourceComponent rc = new()
            {
                Type = resourceName,
                Interaction = new()
                {
                    new() { Code = CapabilityStatement.TypeRestfulInteraction.Create },
                    new() { Code = CapabilityStatement.TypeRestfulInteraction.Delete },
                    //new() { Code = Hl7.Fhir.Model.CapabilityStatement.TypeRestfulInteraction.HistoryInstance },
                    //new() { Code = Hl7.Fhir.Model.CapabilityStatement.TypeRestfulInteraction.HistoryType },
                    //new() { Code = Hl7.Fhir.Model.CapabilityStatement.TypeRestfulInteraction.Patch },
                    new() { Code = CapabilityStatement.TypeRestfulInteraction.Read },
                    new() { Code = CapabilityStatement.TypeRestfulInteraction.SearchType },
                    new() { Code = CapabilityStatement.TypeRestfulInteraction.Update },
                    //new() { Code = Hl7.Fhir.Model.CapabilityStatement.TypeRestfulInteraction.Vread },
                },
                Versioning = CapabilityStatement.ResourceVersionPolicy.NoVersion,
                //ReadHistory = true,
                UpdateCreate = true,
                //ConditionalCreate = true,
                ConditionalRead = CapabilityStatement.ConditionalReadStatus.NotSupported,
                //ConditionalUpdate = true,
                //ConditionalPatch = true,
                ConditionalDelete = CapabilityStatement.ConditionalDeleteStatus.NotSupported,
                ReferencePolicy = new CapabilityStatement.ReferenceHandlingPolicy?[]
                {
                    CapabilityStatement.ReferenceHandlingPolicy.Literal,
                    //CapabilityStatement.ReferenceHandlingPolicy.Logical,
                    //CapabilityStatement.ReferenceHandlingPolicy.Resolves,
                    //CapabilityStatement.ReferenceHandlingPolicy.Enforced,
                    CapabilityStatement.ReferenceHandlingPolicy.Local,
                },
                SearchInclude = resourceStore.GetSearchIncludes(),
                SearchRevInclude = resourceStore.GetSearchRevIncludes(),
                SearchParam = resourceStore.GetSearchParamDefinitions().Select(sp => new CapabilityStatement.SearchParamComponent()
                    {
                        Name = sp.Name,
                        Definition = sp.Url,
                        Type = sp.Type,
                        Documentation = string.IsNullOrEmpty(sp.Description) ? null : sp.Description,
                    }).ToList(),
                Operation = _operations.Values
                    .Where(o => 
                        (o.AllowInstanceLevel || o.AllowResourceLevel) && 
                        ((!o.SupportedResources.Any()) || 
                          o.SupportedResources.Contains(resourceName) || 
                          o.SupportedResources.Contains("Resource") ||
                          o.SupportedResources.Contains("DomainResource")))
                    .Select(o => new CapabilityStatement.OperationComponent()
                    {
                        Name = o.OperationName,
                        Definition = o.CanonicalByFhirVersion[_config.FhirVersion],
                    }).ToList(),
            };

            // add our resource component
            restComponent.Resource.Add(rc);
        }

        // add our rest component to the capability statement
        cs.Rest.Add(restComponent);

        // update our current capabilities
        _store["CapabilityStatement"].InstanceUpdate(
            cs, 
            true,
            string.Empty,
            string.Empty,
            _protectedResources,
            out _,
            out _);
        _capabilitiesAreStale = false;

        return cs;
    }

    private record class TransactionResourceInfo
    {
        public required string FullUrl { get; init; }

        public required string OriginalId { get; init; }

        public required string NewId { get; init; }

        public required bool IsRoot { get; init; }
    }

    private record class TransactionReferenceInfo
    {
        public required string FullUrl { get; init; }

        public required string ReferenceLiteral { get; init; }

        public required string ReferenceLiteralFragment { get; init; }

        public required string IdentifierSystem { get; init; }

        public required string IdentifierValue { get; init; }

        public string LocalReference { get; set; } = string.Empty;
    }

    private void FindTransactionReferences(
        string fullUrl, 
        object o, 
        List<TransactionReferenceInfo> references,
        List<TransactionResourceInfo> resources,
        bool isRoot = false)
    {
        if (o == null)
        {
            return;
        }

        switch (o)
        {
            case null:
            case Hl7.Fhir.Model.PrimitiveType:
                return;

            case Hl7.Fhir.Model.Resource resource:
                {
                    resources.Add(new()
                    {
                        FullUrl = fullUrl,
                        OriginalId = ((Hl7.Fhir.Model.Resource)o).Id,
                        NewId = Guid.NewGuid().ToString(),
                        IsRoot = isRoot,
                    });

                    foreach (Base child in resource.Children)
                    {
                        FindTransactionReferences(
                            fullUrl, 
                            child,
                            references,
                            resources,
                            false);
                    }

                    return;
                }

            case Hl7.Fhir.Model.ResourceReference rr:
                {
                    string rl = ((Hl7.Fhir.Model.ResourceReference)o).Reference ?? string.Empty;
                    string frag;

                    if (rl.Contains('#'))
                    {
                        rl = rl.Substring(0, rl.IndexOf('#'));
                        frag = rl.Substring(rl.IndexOf('#') + 1);
                    }
                    else
                    {
                        frag = string.Empty;
                    }

                    string system = ((Hl7.Fhir.Model.ResourceReference)o).Identifier?.System ?? string.Empty;
                    string value = ((Hl7.Fhir.Model.ResourceReference)o).Identifier?.Value ?? string.Empty;

                    references.Add(new()
                    {
                        FullUrl = fullUrl,
                        ReferenceLiteral = rl,
                        ReferenceLiteralFragment = frag,
                        IdentifierSystem = system,
                        IdentifierValue = value,
                        LocalReference = Guid.NewGuid().ToString(),
                    });

                    return;
                }

            case Hl7.Fhir.Model.Base b:
                foreach (Base child in b.Children)
                {
                    FindTransactionReferences(
                        fullUrl, 
                        child,
                        references,
                        resources,
                        false);
                }
                break;
        }
    }

    /// <summary>Process the transaction.</summary>
    /// <param name="transaction">The transaction.</param>
    /// <param name="response">   The response.</param>
    private void ProcessTransaction(
        Bundle transaction,
        Bundle response)
    {
        List<TransactionReferenceInfo> references = new();
        List<TransactionResourceInfo> resources = new();

        foreach (Bundle.EntryComponent entry in transaction.Entry)
        {
            FindTransactionReferences(entry.FullUrl, entry.Resource, references, resources, true);
        }

        Dictionary<string, string> knownResources = new();

        foreach (TransactionResourceInfo ri in resources)
        {
            Console.WriteLine($"ProcessTransaction <<< {ri.FullUrl} {ri.OriginalId} {ri.NewId} {ri.IsRoot}");
            knownResources.Add(ri.OriginalId, ri.NewId);
        }

        foreach (TransactionReferenceInfo i in references)
        {
            if (string.IsNullOrEmpty(i.ReferenceLiteral))
            {
                continue;
            }

            Console.WriteLine($"ProcessTransaction <<< {i.FullUrl} contains {i.ReferenceLiteral}");

            if (!knownResources.ContainsKey(i.ReferenceLiteral))
            {
                Console.WriteLine($"ProcessTransaction <<< {i.FullUrl} contains unknown reference literal: {i.ReferenceLiteral}");
            }
        }

        // TODO: finish implementing transaction support
        throw new NotImplementedException("Transaction support is not complete!");
    }

    /// <summary>Process a batch request.</summary>
    /// <param name="ctx">     The request context.</param>
    /// <param name="batch">   The batch.</param>
    /// <param name="response">The response.</param>
    private void ProcessBatch(
        FhirRequestContext ctx,
        Bundle batch,
        Bundle response)
    {
        bool opSuccess;
        FhirResponseContext opResponse;

        foreach (Bundle.EntryComponent entry in batch.Entry)
        {
            if (entry.Request == null)
            {
                response.Entry.Add(new Bundle.EntryComponent()
                {
                    FullUrl = entry.FullUrl,
                    Response = new Bundle.ResponseComponent()
                    {
                        Status = HttpStatusCode.BadRequest.ToString(),
                        Outcome = SerializationUtils.BuildOutcomeForRequest(
                            HttpStatusCode.UnprocessableEntity,
                            "Entry is missing a request",
                            OperationOutcome.IssueType.Required),
                    },
                });

                continue;
            }

            FhirRequestContext entryCtx = new()
            {
                TenantName = ctx.TenantName,
                Store = ctx.Store,
                Authorization = ctx.Authorization,
                RequestHeaders = ctx.RequestHeaders,

                HttpMethod = entry.Request.Method?.ToString() ?? string.Empty,
                Url = entry.Request.Url,
                IfMatch = entry.Request.IfMatch ?? string.Empty,
                IfModifiedSince = entry.Request.IfModifiedSince?.ToFhirDateTime() ?? string.Empty,
                IfNoneMatch = entry.Request.IfNoneMatch ?? string.Empty,
                IfNoneExist = entry.Request.IfNoneExist ?? string.Empty,

                SourceObject = entry.Resource,
            };

            if (entryCtx.Interaction == null)
            {
                response.Entry.Add(new Bundle.EntryComponent()
                {
                    FullUrl = entry.FullUrl,
                    Response = new Bundle.ResponseComponent()
                    {
                        Status = HttpStatusCode.InternalServerError.ToString(),
                        Outcome = SerializationUtils.BuildOutcomeForRequest(
                            HttpStatusCode.NotImplemented,
                            $"Request could not be parsed to known interaction: {entry.Request.Method} {entry.Request.Url}",
                            OperationOutcome.IssueType.NotSupported),
                    },
                });

                continue;
            }

            // check authorization on individual requests within a bundle if we are not in loading state
            if ((_loadState == LoadStateCodes.None) &&
                (!ctx.IsAuthorized()))
            {
                response.Entry.Add(new Bundle.EntryComponent()
                {
                    FullUrl = entry.FullUrl,
                    Response = new Bundle.ResponseComponent()
                    {
                        Status = HttpStatusCode.Unauthorized.ToString(),
                        Outcome = SerializationUtils.BuildOutcomeForRequest(
                            HttpStatusCode.Unauthorized,
                            $"Unauthorized request: {entry.Request.Method} {entry.Request.Url}, parsed interaction: {entryCtx.Interaction}",
                            OperationOutcome.IssueType.Forbidden),
                    },
                });

                continue;
            }

            // attempt the request specified
            opSuccess = PerformInteraction(entryCtx, out opResponse, false);
            if (opSuccess)
            {
                response.Entry.Add(new Bundle.EntryComponent()
                {
                    FullUrl = entry.FullUrl,
                    Resource = (Resource?)opResponse.Resource,
                    Response = new Bundle.ResponseComponent()
                    {
                        Status = (opResponse.StatusCode ?? HttpStatusCode.OK).ToString(),
                        Outcome = (Resource?)opResponse.Outcome,
                        Etag = opResponse.ETag ?? string.Empty,
                        LastModified = ((Resource?)opResponse.Resource)?.Meta?.LastUpdated ?? null,
                        Location = opResponse.Location ?? string.Empty,
                    },
                });
            }
            else
            {
                if ((opResponse.Outcome == null) || (opResponse.Outcome is not OperationOutcome oo))
                {
                    oo = SerializationUtils.BuildOutcomeForRequest(
                            HttpStatusCode.NotImplemented,
                            $"Unsupported request: {entry.Request.Method} {entry.Request.Url}, parsed interaction: {entryCtx.Interaction}",
                            OperationOutcome.IssueType.NotSupported);
                }
                else
                {
                    oo.Issue.Add(new OperationOutcome.IssueComponent()
                    {
                        Severity = OperationOutcome.IssueSeverity.Error,
                        Code = OperationOutcome.IssueType.NotSupported,
                        Diagnostics = $"Unsupported request: {entry.Request.Method} {entry.Request.Url}, parsed interaction: {entryCtx.Interaction}",
                    });
                }

                response.Entry.Add(new Bundle.EntryComponent()
                {
                    FullUrl = entry.FullUrl,
                    Response = new Bundle.ResponseComponent()
                    {
                        Status = (opResponse.StatusCode ?? HttpStatusCode.InternalServerError).ToString(),
                        Outcome = oo,
                    },
                });
            }
        }
    }

    /// <summary>Registers that an instance has been created.</summary>
    /// <param name="resourceId">Identifier for the resource.</param>
    public void RegisterInstanceCreated(string resourceType, string resourceId)
    {
        EventHandler<StoreInstanceEventArgs>? handler = OnInstanceCreated;

        if (handler != null)
        {
            handler(this, new()
            {
                ResourceId = resourceId,
                ResourceType = resourceType,
            });
        }
    }

    /// <summary>Registers that an instance has been updated.</summary>
    /// <param name="resourceId">Identifier for the resource.</param>
    public void RegisterInstanceUpdated(string resourceType, string resourceId)
    {
        EventHandler<StoreInstanceEventArgs>? handler = OnInstanceUpdated;

        if (handler != null)
        {
            handler(this, new()
            {
                ResourceId = resourceId,
                ResourceType = resourceType,
            });
        }
    }

    /// <summary>Registers that an instance has been deleted.</summary>
    /// <param name="resourceType">Type of the resource.</param>
    /// <param name="resourceId">  Identifier for the resource.</param>
    public void RegisterInstanceDeleted(string resourceType, string resourceId)
    {
        EventHandler<StoreInstanceEventArgs>? handler = OnInstanceDeleted;

        if (handler != null)
        {
            handler(this, new()
            {
                ResourceId = resourceId,
                ResourceType = resourceType,
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
            // dispose managed state (managed objects)
            if (disposing)
            {
                _capacityMonitor?.Dispose();

                foreach (IResourceStore rs in _store.Values)
                {
                    rs.Dispose();
                    //rs.OnChanged -= ResourceStore_OnChanged;
                }
            }

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
