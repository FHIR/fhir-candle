// <copyright file="FhirStoreManager.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

extern alias candleR4;
extern alias candleR4B;
extern alias candleR5;

using System.Collections;
using System.Linq;
using fhir.candle.Models;
using FhirCandle.Configuration;
using FhirCandle.Extensions;
using FhirCandle.Models;
using FhirCandle.Storage;
using FhirCandle.Utils;
using FhirStore.Smart;
using Firely.Fhir.Packages;
using Hl7.Fhir.Utility;
using static Org.BouncyCastle.Math.EC.ECCurve;

namespace fhir.candle.Services;

/// <summary>Manager for FHIR stores.</summary>
public class FhirStoreManager : IFhirStoreManager, IDisposable
{
    /// <summary>True if has disposed, false if not.</summary>
    private bool _hasDisposed = false;

    /// <summary>True if is initialized, false if not.</summary>
    private bool _isInitialized = false;

    /// <summary>The logger.</summary>
    private ILogger _logger;

    /// <summary>The tenants.</summary>
    private Dictionary<string, TenantConfiguration> _tenants;

    /// <summary>The server configuration.</summary>
    private CandleConfig _serverConfig;

    /// <summary>The package service.</summary>
    private IFhirPackageService _packageService;

    /// <summary>Occurs when On Changed.</summary>
    public event EventHandler<EventArgs>? OnChanged;

    ///// <summary>The services.</summary>
    //private IEnumerable<IHostedService> _services;

    /// <summary>The stores by controller.</summary>
    private Dictionary<string, IFhirStore> _storesByController = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The additional pages by controller.</summary>
    private Dictionary<string, List<PackagePageInfo>> _additionalPagesByController = new();

    /// <summary>
    /// Gets an enumerable collection that contains the keys in the read-only dictionary.
    /// </summary>
    IEnumerable<string> IReadOnlyDictionary<string, IFhirStore>.Keys => _storesByController.Keys;

    /// <summary>
    /// Gets an enumerable collection that contains the values in the read-only dictionary.
    /// </summary>
    IEnumerable<IFhirStore> IReadOnlyDictionary<string, IFhirStore>.Values => _storesByController.Values;

    /// <summary>Gets the number of elements in the collection.</summary>
    int IReadOnlyCollection<KeyValuePair<string, IFhirStore>>.Count => _storesByController.Count;

    /// <summary>Gets the element that has the specified key in the read-only dictionary.</summary>
    /// <param name="key">The key to locate.</param>
    /// <returns>The element that has the specified key in the read-only dictionary.</returns>
    IFhirStore IReadOnlyDictionary<string, IFhirStore>.this[string key] => _storesByController[key];

    /// <summary>
    /// Determines whether the read-only dictionary contains an element that has the specified key.
    /// </summary>
    /// <param name="key">The key to locate.</param>
    /// <returns>
    /// <see langword="true" /> if the read-only dictionary contains an element that has the
    /// specified key; otherwise, <see langword="false" />.
    /// </returns>
    bool IReadOnlyDictionary<string, IFhirStore>.ContainsKey(string key) => _storesByController.ContainsKey(key);

    /// <summary>Gets the value that is associated with the specified key.</summary>
    /// <param name="key">  The key to locate.</param>
    /// <param name="value">[out] When this method returns, the value associated with the specified
    ///  key, if the key is found; otherwise, the default value for the type of the <paramref name="value" />
    ///  parameter. This parameter is passed uninitialized.</param>
    /// <returns>
    /// <see langword="true" /> if the object that implements the <see cref="T:System.Collections.Generic.IReadOnlyDictionary`2" />
    /// interface contains an element that has the specified key; otherwise, <see langword="false" />.
    /// </returns>
    bool IReadOnlyDictionary<string, IFhirStore>.TryGetValue(string key, out IFhirStore value) => _storesByController.TryGetValue(key, out value!);

    /// <summary>Returns an enumerator that iterates through the collection.</summary>
    /// <returns>An enumerator that can be used to iterate through the collection.</returns>
    IEnumerator<KeyValuePair<string, IFhirStore>> IEnumerable<KeyValuePair<string, IFhirStore>>.GetEnumerator() => _storesByController.GetEnumerator();

    /// <summary>Returns an enumerator that iterates through a collection.</summary>
    /// <returns>
    /// An <see cref="T:System.Collections.IEnumerator" /> object that can be used to iterate through
    /// the collection.
    /// </returns>
    IEnumerator IEnumerable.GetEnumerator() => (IEnumerator)_storesByController.GetEnumerator();

    /// <summary>Initializes a new instance of the <see cref="FhirStoreManager"/> class.</summary>
    /// <param name="tenants">            The tenants.</param>
    /// <param name="logger">             The logger.</param>
    /// <param name="serverConfiguration">The server configuration.</param>
    /// <param name="fhirPackageService"> The FHIR package service.</param>
    public FhirStoreManager(
        Dictionary<string, TenantConfiguration> tenants,
        ILogger<FhirStoreManager> logger,
        CandleConfig serverConfiguration,
        IFhirPackageService fhirPackageService)
    {
        _tenants = tenants;
        _logger = logger;
        _serverConfig = serverConfiguration;
        _packageService = fhirPackageService;
    }

    /// <summary>Gets the additional pages by tenant.</summary>
    public IReadOnlyDictionary<string, IQueryable<PackagePageInfo>> AdditionalPagesByTenant => _additionalPagesByController.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.AsQueryable());

    /// <summary>State has changed.</summary>
    public void StateHasChanged()
    {
        EventHandler<EventArgs>? handler = OnChanged;

        if (handler != null)
        {
            handler(this, EventArgs.Empty);
        }
    }

    /// <summary>Initializes this object.</summary>
    /// <exception cref="Exception">Thrown when an exception error condition occurs.</exception>
    public void Init()
    {
        if (_isInitialized)
        {
            return;
        }

        _isInitialized = true;

        // make sure the package service has been initalized
        _packageService.Init();

        _logger.LogInformation("FhirStoreManager <<< Creating FHIR tenants...");

        // initialize the requested fhir stores
        foreach ((string name, TenantConfiguration config) in _tenants)
        {
            if (_storesByController.ContainsKey(config.ControllerName))
            {
                throw new Exception($"Duplicate controller names configured!: {config.ControllerName}");
            }

            switch (config.FhirVersion)
            {
                case FhirReleases.FhirSequenceCodes.R4:
                    _storesByController.Add(name, new candleR4::FhirCandle.Storage.VersionedFhirStore());
                    break;

                case FhirReleases.FhirSequenceCodes.R4B:
                    _storesByController.Add(name, new candleR4B::FhirCandle.Storage.VersionedFhirStore());
                    break;

                case FhirReleases.FhirSequenceCodes.R5:
                    _storesByController.Add(name, new candleR5::FhirCandle.Storage.VersionedFhirStore());
                    break;
            }

            _storesByController[name].Init(config);
            //_storesByController[config.ControllerName].OnSubscriptionSendEvent += FhirStoreManager_OnSubscriptionSendEvent;
        }

        string root =
            Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location ?? AppContext.BaseDirectory) ??
            Environment.CurrentDirectory ??
            string.Empty;

        // check for loading packages
        if (_packageService.IsConfigured &&
            (_serverConfig.PublishedPackages.Any() || _serverConfig.CiPackages.Any()))
        {
            // look for a package supplemental directory
            string supplemental = string.IsNullOrEmpty(_serverConfig.SourceDirectory)
                ? Program.FindRelativeDir(root, "fhirData", false)
            : _serverConfig.SourceDirectory;

            LoadRequestedPackages(supplemental, _serverConfig.LoadPackageExamples == true).Wait();
        }

        // sort through RI info
        if (!string.IsNullOrEmpty(_serverConfig.ReferenceImplementation))
        {
            // look for a package supplemental directory
            string supplemental = string.IsNullOrEmpty(_serverConfig.SourceDirectory)
                ? Program.FindRelativeDir(root, Path.Combine("fhirData", _serverConfig.ReferenceImplementation), false)
                : Path.Combine(_serverConfig.SourceDirectory, _serverConfig.ReferenceImplementation);

            LoadRiContents(supplemental);
        }

        // load packages
        LoadPackagePages();
    }

    /// <summary>Triggered when the application host is ready to start the service.</summary>
    /// <param name="cancellationToken">Indicates that the start process has been aborted.</param>
    /// <returns>An asynchronous result.</returns>
    Task IHostedService.StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting FhirStoreManager...");

        Init();

        return Task.CompletedTask;
    }

    /// <summary>Loads package pages.</summary>
    /// <returns>The package pages.</returns>
    private void LoadPackagePages()
    {
        _logger.LogInformation("FhirStoreManager <<< Discovering package-based pages...");

        // get all page types
        List<PackagePageInfo> pages = [];

        pages.AddRange(typeof(fhir.candle.Pages.RI.subscriptions.Tour).Assembly.GetTypes()
            .Where(t => t.GetInterfaces().Contains(typeof(IPackagePage)))
            .Select(pt => new PackagePageInfo()
            {
                ContentFor = pt.GetProperty("ContentFor", typeof(string))?.GetValue(null) as string ?? string.Empty,
                PageName = pt.GetProperty("PageName", typeof(string))?.GetValue(null) as string ?? string.Empty,
                Description = pt.GetProperty("Description", typeof(string))?.GetValue(null) as string ?? string.Empty,
                RoutePath = pt.GetProperty("RoutePath", typeof(string))?.GetValue(null, null) as string ?? string.Empty,
                FhirVersionLiteral = pt.GetProperty("FhirVersionLiteral", typeof(string))?.GetValue(null) as string ?? string.Empty,
                FhirVersionNumeric = pt.GetProperty("FhirVersionNumeric", typeof(string))?.GetValue(null) as string ?? string.Empty,
                OnlyShowOnEndpoint = pt.GetProperty("OnlyShowOnEndpoint", typeof(string))?.GetValue(null) as string ?? string.Empty,
            }));

        pages.AddRange(typeof(FhirCandle.Ui.R4.Subscriptions.TourUtils).Assembly.GetTypes()
            .Where(t => t.GetInterfaces().Contains(typeof(IPackagePage)))
            .Select(pt => new PackagePageInfo()
            {
                ContentFor = pt.GetProperty("ContentFor", typeof(string))?.GetValue(null) as string ?? string.Empty,
                PageName = pt.GetProperty("PageName", typeof(string))?.GetValue(null) as string ?? string.Empty,
                Description = pt.GetProperty("Description", typeof(string))?.GetValue(null) as string ?? string.Empty,
                RoutePath = pt.GetProperty("RoutePath", typeof(string))?.GetValue(null) as string ?? string.Empty,
                FhirVersionLiteral = pt.GetProperty("FhirVersionLiteral", typeof(string))?.GetValue(null) as string ?? string.Empty,
                FhirVersionNumeric = pt.GetProperty("FhirVersionNumeric", typeof(string))?.GetValue(null) as string ?? string.Empty,
                OnlyShowOnEndpoint = pt.GetProperty("OnlyShowOnEndpoint", typeof(string))?.GetValue(null) as string ?? string.Empty,
            }));

        pages.AddRange(typeof(FhirCandle.Ui.R4B.Subscriptions.TourUtils).Assembly.GetTypes()
            .Where(t => t.GetInterfaces().Contains(typeof(IPackagePage)))
            .Select(pt => new PackagePageInfo()
            {
                ContentFor = pt.GetProperty("ContentFor", typeof(string))?.GetValue(null) as string ?? string.Empty,
                PageName = pt.GetProperty("PageName", typeof(string))?.GetValue(null) as string ?? string.Empty,
                Description = pt.GetProperty("Description", typeof(string))?.GetValue(null) as string ?? string.Empty,
                RoutePath = pt.GetProperty("RoutePath", typeof(string))?.GetValue(null) as string ?? string.Empty,
                FhirVersionLiteral = pt.GetProperty("FhirVersionLiteral", typeof(string))?.GetValue(null) as string ?? string.Empty,
                FhirVersionNumeric = pt.GetProperty("FhirVersionNumeric", typeof(string))?.GetValue(null) as string ?? string.Empty,
                OnlyShowOnEndpoint = pt.GetProperty("OnlyShowOnEndpoint", typeof(string))?.GetValue(null) as string ?? string.Empty,
            }));

        pages.AddRange(typeof(FhirCandle.Ui.R5.Subscriptions.TourUtils).Assembly.GetTypes()
            .Where(t => t.GetInterfaces().Contains(typeof(IPackagePage)))
            .Select(pt => new PackagePageInfo()
            {
                ContentFor = pt.GetProperty("ContentFor", typeof(string))?.GetValue(null) as string ?? string.Empty,
                PageName = pt.GetProperty("PageName", typeof(string))?.GetValue(null) as string ?? string.Empty,
                Description = pt.GetProperty("Description", typeof(string))?.GetValue(null) as string ?? string.Empty,
                RoutePath = pt.GetProperty("RoutePath", typeof(string))?.GetValue(null) as string ?? string.Empty,
                FhirVersionLiteral = pt.GetProperty("FhirVersionLiteral", typeof(string))?.GetValue(null) as string ?? string.Empty,
                FhirVersionNumeric = pt.GetProperty("FhirVersionNumeric", typeof(string))?.GetValue(null) as string ?? string.Empty,
                OnlyShowOnEndpoint = pt.GetProperty("OnlyShowOnEndpoint", typeof(string))?.GetValue(null) as string ?? string.Empty,
            }));

        _additionalPagesByController = new();
        foreach (string tenant in _tenants.Keys)
        {
            _additionalPagesByController.Add(tenant, new List<PackagePageInfo>());
        }

        // traverse page types to build package info
        foreach (PackagePageInfo page in pages)
        {
            Console.WriteLine($"Package page: {page.PageName}, FhirVersion: {page.FhirVersionLiteral} ({page.FhirVersionNumeric}), ContentFor: {page.ContentFor}, OnlyOnEndpoint: {page.OnlyShowOnEndpoint}");

            if (string.IsNullOrEmpty(page.FhirVersionLiteral))
            {
                foreach ((string name, IFhirStore store) in _storesByController)
                {
                    if ((page.ContentFor == _serverConfig.ReferenceImplementation) ||
                        store.LoadedPackageDirectives.Contains(page.ContentFor) ||
                        store.LoadedPackageIds.Contains(page.ContentFor) ||
                        store.LoadedSupplements.Contains(page.ContentFor))
                    {
                        Console.WriteLine($"Testing page: {page.PageName} (only for: {page.OnlyShowOnEndpoint}) against store {name}");

                        if (string.IsNullOrEmpty(page.OnlyShowOnEndpoint) || page.OnlyShowOnEndpoint.Equals(name, StringComparison.OrdinalIgnoreCase))
                        {
                            _additionalPagesByController[name].Add(page);
                        }
                        else
                        {
                            Console.WriteLine($"Skipping page: {page.PageName} against store {name} - no endpoint match");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Skipping page: {page.PageName} against store {name} - no content match");
                    }
                }

                continue;
            }

            if (!FhirReleases.TryGetSequence(page.FhirVersionLiteral, out FhirReleases.FhirSequenceCodes pageFhirVersion))
            {
                continue;
            }

            // traverse stores to marry contents
            foreach ((string name, IFhirStore store) in _storesByController)
            {
                if ((store.Config.FhirVersion == pageFhirVersion) &&
                    ((page.ContentFor == _serverConfig.ReferenceImplementation) ||
                     store.LoadedPackageDirectives.Contains(page.ContentFor) ||
                     store.LoadedPackageIds.Contains(page.ContentFor) ||
                     store.LoadedSupplements.Contains(page.ContentFor)))
                {
                    Console.WriteLine($"Testing page: {page.PageName} (only for: {page.OnlyShowOnEndpoint}) against store {name}");

                    if (string.IsNullOrEmpty(page.OnlyShowOnEndpoint) || page.OnlyShowOnEndpoint.Equals(name, StringComparison.OrdinalIgnoreCase))
                    {
                        _additionalPagesByController[name].Add(page);
                    }
                    else
                    {
                        Console.WriteLine($"Skipping page: {page.PageName} against store {name} - no endpoint match");
                    }
                }
                else
                {
                    Console.WriteLine($"Skipping page: {page.PageName} against store {name} - no FHIR version or content match");
                }
            }
        }
    }

    /// <summary>Loads ri contents.</summary>
    /// <param name="dir">The dir.</param>
    public void LoadRiContents(string dir)
    {
        if (string.IsNullOrEmpty(dir) ||
            !Directory.Exists(dir))
        {
            return;
        }

        _logger.LogInformation("FhirStoreManager <<< Loading RI contents...");

        // loop over controllers to see where we can add this
        foreach ((string tenantName, TenantConfiguration config) in _tenants)
        {
            switch (config.FhirVersion)
            {
                case FhirReleases.FhirSequenceCodes.R4:
                    if (Directory.Exists(Path.Combine(dir, "r4")))
                    {
                        _storesByController[tenantName].LoadPackage(
                            string.Empty,
                            string.Empty,
                            Path.Combine(dir, "r4"),
                            true);
                    }
                    else if (Directory.Exists(Path.Combine(dir, tenantName)))
                    {
                        _storesByController[tenantName].LoadPackage(
                            string.Empty,
                            string.Empty,
                            Path.Combine(dir, tenantName),
                            true);
                    }
                    else
                    {
                        _storesByController[tenantName].LoadPackage(
                            string.Empty,
                            string.Empty,
                            dir,
                            true);
                    }
                    break;
                case FhirReleases.FhirSequenceCodes.R4B:
                    if (Directory.Exists(Path.Combine(dir, "r4b")))
                    {
                        _storesByController[tenantName].LoadPackage(
                            string.Empty,
                            string.Empty,
                            Path.Combine(dir, "r4b"),
                            true);
                    }
                    else if (Directory.Exists(Path.Combine(dir, tenantName)))
                    {
                        _storesByController[tenantName].LoadPackage(
                            string.Empty,
                            string.Empty,
                            Path.Combine(dir, tenantName),
                            true);
                    }
                    else
                    {
                        _storesByController[tenantName].LoadPackage(
                            string.Empty,
                            string.Empty,
                            dir,
                            true);
                    }
                    break;
                case FhirReleases.FhirSequenceCodes.R5:
                    if (Directory.Exists(Path.Combine(dir, "r5")))
                    {
                        _storesByController[tenantName].LoadPackage(
                            string.Empty,
                            string.Empty,
                            Path.Combine(dir, "r5"),
                            true);
                    }
                    else if (Directory.Exists(Path.Combine(dir, tenantName)))
                    {
                        _storesByController[tenantName].LoadPackage(
                            string.Empty,
                            string.Empty,
                            Path.Combine(dir, tenantName),
                            true);
                    }
                    else
                    {
                        _storesByController[tenantName].LoadPackage(
                            string.Empty,
                            string.Empty,
                            dir,
                            true);
                    }
                    break;
                default:
                    break;
            }
        }
    }

    /// <summary>Loads requested packages.</summary>
    /// <exception cref="Exception">Thrown when an exception error condition occurs.</exception>
    /// <param name="supplementalRoot">The supplemental root.</param>
    /// <param name="loadExamples">    True to load examples.</param>
    /// <returns>An asynchronous result.</returns>
    public async Task LoadRequestedPackages(string supplementalRoot, bool loadExamples)
    {
        _logger.LogInformation("FhirStoreManager <<< loading requested packages...");

        // check for requested packages
        int waitCount = 0;
        while (!_packageService.IsReady)
        {
            _logger.LogInformation("FhirStoreManager <<< Waiting for package service...");

            await Task.Delay(100);
            waitCount++;

            if (waitCount > 200)
            {
                throw new Exception("Package service is not responding!");
            }
        }

        if (!_packageService.IsConfigured)
        {
            _logger.LogInformation("FhirStoreManager <<< Package service is not configured and will not be available!");
            return;
        }

        List<FhirReleases.FhirSequenceCodes> allTenantFhirVersions = _tenants.Values.Select(t => t.FhirVersion).Distinct().ToList();

        List<PackageReference> localPackages = await _packageService.InstallPackages(
            _serverConfig.PublishedPackages,
            _serverConfig.CiPackages,
            allTenantFhirVersions);

        // loop over package references to load - go in ascending version order the newest versions are loaded last
        foreach (PackageReference pr in localPackages.OrderBy(r => r.Version))
        {
            _logger.LogInformation($"FhirStoreManager <<< discovering and loading additional content for {pr.Moniker}...");

            List<FhirReleases.FhirSequenceCodes>? packageFhirVersions = await _packageService.InstalledPackageFhirVersions(pr);

            // loop over controllers to see where we can add this
            foreach ((string tenantName, TenantConfiguration config) in _tenants)
            {
                // if this package lists FHIR versions and it doesn't include the tenant's version, skip it
                if ((packageFhirVersions != null) &&
                    !packageFhirVersions.Contains(config.FhirVersion))
                {
                    continue;
                }

                // make sure this package exists on disk
                if (_packageService.GetPackageContentDirectory(pr) is string contentDir)
                {
                    // check to see if we should skip this package for this tenant because a FHIR-version-specific package exists
                    string packageName = pr.Name! + "." + config.FhirVersion.ToRLiteral().ToLowerInvariant();
                    if (localPackages.Any(r => r.Name == packageName))
                    {
                        continue;
                    }

                    _storesByController[tenantName].LoadPackage(
                        pr.Moniker,
                        contentDir,
                        GetSupplementDir(supplementalRoot, pr),
                        loadExamples);
                }
            }
        }
    }

    /// <summary>Gets supplement dir.</summary>
    /// <param name="supplementalRoot"> The supplemental root.</param>
    /// <param name="entry">The resolved directive entry.</param>
    /// <returns>The supplement dir.</returns>
    private string GetSupplementDir(string supplementalRoot, PackageReference packageReference)
    {
        if (string.IsNullOrEmpty(supplementalRoot))
        {
            return string.Empty;
        }

        string dir;

        // check to see if we have an exact match
        dir = Path.Combine(supplementalRoot, packageReference.Moniker);
        if (Directory.Exists(dir))
        {
            return dir;
        }

        dir = Path.Combine(supplementalRoot, packageReference.Moniker.Replace('@', '#'));
        if (Directory.Exists(dir))
        {
            return dir;
        }

        // check for named package without version
        if (!string.IsNullOrEmpty(packageReference.Name))
        {
            dir = Path.Combine(supplementalRoot, packageReference.Name);
            if (Directory.Exists(dir))
            {
                return dir;
            }
        }

        return string.Empty;
    }

    /// <summary>Triggered when the application host is performing a graceful shutdown.</summary>
    /// <param name="cancellationToken">Indicates that the shutdown process should no longer be
    ///  graceful.</param>
    /// <returns>An asynchronous result.</returns>
    Task IHostedService.StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
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

                //foreach (IFhirStore store in _storesByController.Values)
                //{
                //    store.OnSubscriptionSendEvent -= FhirStoreManager_OnSubscriptionSendEvent;
                //}
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
