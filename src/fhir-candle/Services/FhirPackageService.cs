// <copyright file="FhirPackageService.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using fhir.candle._ForPackages;
using fhir.candle.Models;
using FhirCandle.Utils;
using FhirCandle.Configuration;
using FhirCandle.Extensions;
using FhirCandle.Models;
using FhirPkg.Registry;
using FhirPkg.Installation;
using Microsoft.Extensions.Logging.Abstractions;
using Hl7.Fhir.Specification;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using System.Collections.Concurrent;
using System.Formats.Tar;
using System.IO.Compression;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using PackageReference = FhirCandle.Models.PackageReference;
using PackageCatalogEntry = FhirCandle.Models.PackageCatalogEntry;
using NativePackageReference = FhirPkg.Models.PackageReference;
using SemVer = SemanticVersioning.Version;

namespace fhir.candle.Services;

/// <summary>A service for accessing FHIR packages.</summary>
public partial class FhirPackageService : IFhirPackageService, IDisposable
{
    internal enum VersionHandlingTypes
    {
        /// <summary>Unprocessed / unknown / SemVer / ranges / etc (pass through).</summary>
        Passthrough,

        /// <summary>Latest release.</summary>
        Latest,

        /// <summary>Local build.</summary>
        Local,

        /// <summary>CI Build.</summary>
        ContinuousIntegration,
    }

    /// <summary>Values that represent package load state enums.</summary>
    public enum PackageLoadStateEnum
    {
        /// <summary>The package is in an unknown state.</summary>
        Unknown,

        /// <summary>The package has not been loaded.</summary>
        NotLoaded,

        /// <summary>The package is queued for loading.</summary>
        Queued,

        /// <summary>The package is currently being loaded.</summary>
        InProgress,

        /// <summary>The package is currently loaded into memory.</summary>
        Loaded,

        /// <summary>The package has failed to load and cannot be used.</summary>
        Failed,

        /// <summary>The package has been parsed but not loaded into memory.</summary>
        Parsed,
    }

    /// <summary>(Immutable) The cache.</summary>
    private _ForPackages.DiskPackageCache? _cache = null;

    /// <summary>The validated first-Init policy, shared by cache imports and lazy registry construction.</summary>
    private FhirPkg.PackageInstallLimits? _installLimits;

    /// <summary>(Immutable) The package clients.</summary>
    private readonly List<Lazy<IRegistryClient?>> _packageClients = [];

    /// <summary>The HTTP transport owned by the introduced registry clients.</summary>
    private readonly HttpClient _registryHttpClient = new();

    /// <summary>(Immutable) The FHIR CI client (build.fhir.org).</summary>
    private readonly FhirCiClient _ciClient = new();

    private readonly HashSet<string> _processedMonikers = [];

    /// <summary>Information about a package in the cache.</summary>
    public readonly record struct PackageCacheRecord(
        string CacheDirective,
        PackageLoadStateEnum PackageState,
        string PackageName,
        string Version,
        FhirReleases.FhirSequenceCodes FhirVersion,
        string DownloadDateTime,
        long PackageSize,
        FhirNpmPackageDetails Details);

    /// <summary>(Immutable) The package registry URIs.</summary>
    private static readonly string[] _officialRegistryUrls =
    [
        "https://packages.fhir.org/",
        "https://packages2.fhir.org/packages/",
    ];

    /// <summary>The logger.</summary>
    private ILogger _logger;

    /// <summary>True if is initialized, false if not.</summary>
    private bool _isInitialized = false;

    /// <summary>Server configuration.</summary>
    private CandleConfig _config;

    /// <summary>Pathname of the cache package directory.</summary>
    private string _cachePackageDirectory = string.Empty;

    /// <summary>True to disposed value.</summary>
    private bool _disposedValue = false;

    /// <summary>The singleton.</summary>
    private static FhirPackageService _singleton = null!;

    /// <summary>The package records, by directive.</summary>
    private Dictionary<string, PackageCacheRecord> _packagesByDirective = new();

    /// <summary>Package versions, by package name.</summary>
    private Dictionary<string, List<string>> _versionsByName = new();

    /// <summary>Occurs when On Changed.</summary>
    public event EventHandler<EventArgs>? OnChanged = null;

    /// <summary>Initializes a new instance of the <see cref="FhirPackageService"/> class.</summary>
    /// <param name="logger">             The logger.</param>
    /// <param name="serverConfiguration">The server configuration.</param>
    public FhirPackageService(
        ILogger<FhirPackageService> logger,
        CandleConfig serverConfiguration)
    {
        _logger = logger;
        _config = serverConfiguration;
        _singleton = this;
        _cache = null;
    }

    /// <summary>Gets the current singleton.</summary>
    public static FhirPackageService Current => _singleton;

    /// <summary>Gets the packages by directive.</summary>
    public Dictionary<string, PackageCacheRecord> PackagesByDirective => _packagesByDirective;

    /// <summary>Gets a value indicating whether this object is available.</summary>
    public bool IsConfigured => _cache is not null;

    /// <summary>Gets a value indicating whether the package service is ready.</summary>
    public bool IsReady => _isInitialized;

    /// <summary>The completed requests.</summary>
    private readonly HashSet<string> _processed = new();

    /// <summary>Initializes this object.</summary>
    public void Init()
    {
        if (_isInitialized)
        {
            return;
        }

        if (_config.FhirCacheDirectory == string.Empty)
        {
            _logger.LogInformation("Disabling FhirPackageService, --fhir-package-cache set to empty.");
            return;
        }

        _installLimits = _ForPackages.DiskPackageCache.CreateInstallLimits(_config);

        if (_config.FhirCacheDirectory is null)
        {
            _config.FhirCacheDirectory = _ForPackages.DiskPackageCache.GetDefaultRoot();
        }

        _logger.LogInformation($"Initializing FhirPackageService with cache: {_config.FhirCacheDirectory}");
        _isInitialized = true;

        if (!Directory.Exists(_config.FhirCacheDirectory))
        {
            Directory.CreateDirectory(_config.FhirCacheDirectory);
            Directory.CreateDirectory(Path.Combine(_config.FhirCacheDirectory, "packages"));
        }

        if (Directory.Exists(Path.Combine(_config.FhirCacheDirectory, "packages")))
        {
            _cachePackageDirectory = Path.Combine(_config.FhirCacheDirectory, "packages");
        }
        else
        {
            _cachePackageDirectory = _config.FhirCacheDirectory;
        }

        _cache = new(_config.FhirCacheDirectory, _installLimits);

        // check if we are using the official registries
        if (_config.UseOfficialRegistries == true)
        {
            foreach (string url in _officialRegistryUrls)
            {
                AddRegistry(url, FhirPkg.Models.RegistryType.FhirNpm);
            }
        }

        if (_config.AdditionalFhirRegistryUrls.Any())
        {
            foreach (string url in _config.AdditionalFhirRegistryUrls)
            {
                AddRegistry(url, FhirPkg.Models.RegistryType.FhirNpm);
            }
        }

        if (_config.AdditionalNpmRegistryUrls.Any())
        {
            foreach (string url in _config.AdditionalNpmRegistryUrls)
            {
                AddRegistry(url, FhirPkg.Models.RegistryType.Npm);
            }
        }
    }

    private void AddRegistry(string url, FhirPkg.Models.RegistryType type)
    {
        int ordinal = _packageClients.Count + 1;
        _packageClients.Add(new Lazy<IRegistryClient?>(() =>
        {
            try
            {
                RegistryEndpoint endpoint = new() { Url = url, Type = type };
                return RegistryClientFactory.CreateClientForEndpoint(
                    endpoint, _registryHttpClient, NullLoggerFactory.Instance,
                    timeProvider: null, new FhirPkg.FileSystemRetryOptions(), _installLimits!);
            }
            catch (ArgumentException)
            {
                _logger.LogWarning("Registry configuration unavailable for slot {Ordinal} ({Protocol}): configuration.",
                    ordinal, type);
                return null;
            }
        }));
    }

    private static PackageReference ParseReference(string directive)
    {
        // Retain the old accepted envelope: bare scope, first slash segment and
        // first @ version, without trimming or interpreting a scoped #.
        string? scope = null;
        string input = directive;
        if (input.StartsWith('@'))
        {
            string[] segments = input.Split('/');
            scope = segments[0][1..];
            input = segments[1];
        }
        string[] parts = input.Split('@');
        string name = parts[0];
        string? version = parts.Length > 1 ? parts[1] : null;
        if (string.IsNullOrWhiteSpace(name))
        {
            return new(scope, name, version);
        }
        NativePackageReference native = NativePackageReference.Parse(input);
        return new(scope, native.Name == name ? native.Name : name,
            native.Version == version ? native.Version : version);
    }

    private async Task<List<SemVer>> GetVersions(IRegistryClient client, string name)
    {
        // A legacy # in Name was a URI fragment, not an escaped package ID.
        string transportName = name.Split('#')[0];
        FhirPkg.Models.PackageListing? listing;
        try
        {
            listing = await client.GetPackageListingAsync(transportName);
        }
        catch (HttpRequestException exception)
        {
            if (exception.StatusCode != HttpStatusCode.NotFound)
            {
                LogRegistryListingFailure(client.Endpoint, name,
                    exception.StatusCode.HasValue ? "http" : "transport", exception.StatusCode);
            }
            return [];
        }
        catch (RegistryResponseTimeoutException)
        {
            LogRegistryListingFailure(client.Endpoint, name, "deadline");
            return [];
        }
        catch (HttpIOException exception) when (exception.HttpRequestError == HttpRequestError.ResponseEnded)
        {
            LogRegistryListingFailure(client.Endpoint, name, "response-body");
            return [];
        }
        catch (System.Text.Json.JsonException) when (
            client.Endpoint.Type is FhirPkg.Models.RegistryType.FhirNpm or FhirPkg.Models.RegistryType.Npm)
        {
            LogRegistryListingFailure(client.Endpoint, name, "listing-json");
            return [];
        }
        catch (NullReferenceException) when (
            client.Endpoint.Type is FhirPkg.Models.RegistryType.FhirNpm or FhirPkg.Models.RegistryType.Npm)
        {
            LogRegistryListingFailure(client.Endpoint, name, "listing-structure");
            return [];
        }
        List<SemVer> versions = [];
        foreach (KeyValuePair<string, FhirPkg.Models.PackageVersionInfo> entry in
            listing?.Versions ?? Enumerable.Empty<KeyValuePair<string, FhirPkg.Models.PackageVersionInfo>>())
        {
            JsonElement marker = entry.Value.Unlisted;
            if (marker.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
            {
                LogRegistryListingFailure(client.Endpoint, name, "listing-json");
                return [];
            }
            if (marker.ValueKind is not (JsonValueKind.Undefined or JsonValueKind.Null)
                && !(marker.ValueKind == JsonValueKind.String && marker.GetString() == string.Empty))
            {
                continue;
            }
            if (SemVer.TryParse(entry.Key, out SemVer? version) && version is not null)
            {
                versions.Add(version);
            }
        }
        versions.Sort();
        return versions;
    }

    private void LogRegistryListingFailure(RegistryEndpoint endpoint, string name, string category, HttpStatusCode? statusCode = null)
    {
        string registry = new Uri(endpoint.Url).GetComponents(
            UriComponents.SchemeAndServer | UriComponents.Path, UriFormat.UriEscaped);
        _logger.LogWarning("GetVersions <<< Registry listing unavailable for {Package} at {Registry}: {Category}, HTTP status {Status}.",
            name, registry, category, (int?)statusCode);
    }

    /// <summary>Triggered when the application host is ready to start the service.</summary>
    /// <param name="cancellationToken">Indicates that the start process has been aborted.</param>
    /// <returns>An asynchronous result.</returns>
    Task IHostedService.StartAsync(CancellationToken cancellationToken)
    {
        if (_cache is null)
        {
            _logger.LogInformation("Disabling FhirPackageService, --fhir-package-cache set to empty.");
            return Task.CompletedTask;
        }

        _logger.LogInformation($"Starting FhirPackageService...");

        Init();

        return Task.CompletedTask;
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
    /// A record struct representing a package cache entry.
    /// </summary>
    /// <param name="fhirVersion">The FHIR version of the package.</param>
    /// <param name="directory">The directory where the package is stored.</param>
    /// <param name="resolvedDirective">The resolved directive of the package.</param>
    /// <param name="name">The name of the package.</param>
    /// <param name="version">The version of the package.</param>
    /// <param name="umbrellaPackageName">The umbrella package name that this package is part of.</param>
    public record struct PackageCacheEntry(
        FhirReleases.FhirSequenceCodes fhirVersion,
        string directory,
        string resolvedDirective,
        string name,
        string version,
        string umbrellaPackageName);

    public async Task<List<PackageReference>> InstallPackages(
        string[]? packageDirectives,
        string[]? ciLiterals,
        List<FhirReleases.FhirSequenceCodes>? fhirVersions)
    {
        List<PackageReference> localPackages = [];

        List<string> directives = packageDirectives?.ToList() ?? new();

        directives.AddRange(await ResolveCiLiterals(ciLiterals));

        if (directives.Count == 0)
        {
            return [];
        }

        if (_cache is null)
        {
            _logger.LogError("InstallPackages <<< Packages have been requested, but no cache has been configured!");
            return [];
        }

        // traverse our package directives
        foreach (string inputDirective in directives)
        {
            // TODO(ginoc): PR in to Parse FHIR-style directives, remove when added.
            string directive = inputDirective.Contains('@')
                ? inputDirective
                : inputDirective.Replace('#', '@');

            PackageReference packageReference = ParseReference(directive);

            if (packageReference.Name is null)
            {
                _logger.LogWarning($"InstallPackages <<< Failed to parse package reference: {directive}");
                continue;
            }

            bool needsInstall = true;

            VersionHandlingTypes vht = GetVersionHandlingType(packageReference.Version);

            // do special handling for versions if necessary
            switch (vht)
            {
                case VersionHandlingTypes.Latest:
                    {
                        // Resolve the version before retaining the legacy requested cache identity.
                        PackageReference pr = await ResolveLatest(packageReference.Name);

                        if ((pr == PackageReference.None) || (pr.Name is null))
                        {
                            throw new Exception($"Failed to resolve latest version of {packageReference.Name} ({directive})");
                        }

                        packageReference = pr;
                        needsInstall = !await _cache.IsInstalled(packageReference);
                    }
                    break;

                case VersionHandlingTypes.Local:
                    // ensure there is a local build, there is no other source
                    {
                        if (!_cache.IsInstalled(packageReference).Result)
                        {
                            throw new Exception($"Local build of {packageReference.Name} is not installed ({directive})");
                        }
                    }
                    break;

                case VersionHandlingTypes.ContinuousIntegration:
                    // always trigger install/update for CI builds
                    needsInstall = true;
                    packageReference.Scope = FhirCiClient.FhirCiScope;
                    break;

                default:
                    needsInstall = !await _cache.IsInstalled(packageReference);
                    break;
            }

            // skip if we have already loaded this package
            if (_processedMonikers.Contains(packageReference.Moniker))
            {
                _logger.LogInformation($"Skipping already loaded dependency: {packageReference.Moniker}");
                continue;
            }
            _processedMonikers.Add(packageReference.Moniker);

            _logger.LogInformation($"Processing {packageReference.Moniker}...");

            // check to see if this package needs to be installed
            if (needsInstall &&
                (await InstallPackage(packageReference) == false))
            {
                // failed to install
                throw new Exception($"Failed to install package {packageReference.Moniker} as requested by {inputDirective}");
            }

            // add this package
            localPackages.Add(packageReference);

            // check to see if we have a specified FHIR versions and need to filter
            if (fhirVersions?.Count > 0)
            {
                // read the manifest to pull the FHIR version of the package
                _ForPackages.PackageManifest manifest = await _cache.ReadManifestEx(packageReference) ?? throw new Exception("Failed to load package manifest");

                if (manifest.AnyFhirVersions?.FirstOrDefault() is not string manifestFhirVersion)
                {
                    _logger.LogInformation($"InstallPackages <<< Package {packageReference.Moniker} does not report a FHIR version!");
                    continue;
                }

                // get the FHIR version of the package
                FhirReleases.FhirSequenceCodes packageFhirSequence = FhirReleases.FhirVersionToSequence(manifestFhirVersion);

                // iterate over our requested FHIR versions
                foreach (FhirReleases.FhirSequenceCodes fhirSequence in fhirVersions)
                {
                    if (packageFhirSequence == fhirSequence)
                    {
                        continue;
                    }

                    _logger.LogInformation($"InstallPackages <<< {packageReference.Moniker} ({manifestFhirVersion}) does not match requested FHIR version {fhirSequence}!");

                    string packageIdSuffix = packageReference.Name.Split('.')[^1];
                    FhirReleases.FhirSequenceCodes packageIdSuffixCode = FhirReleases.FhirVersionToSequence(packageIdSuffix);

                    string requiredRLiteral = fhirSequence.ToRLiteral().ToLowerInvariant();
                    string desiredName = (packageIdSuffixCode == FhirReleases.FhirSequenceCodes.Unknown)
                        ? $"{packageReference.Name}.{requiredRLiteral}"
                        : $"{string.Join('.', packageReference.Name.Split('.')[..^1])}.{requiredRLiteral}";
                    string desiredMoniker = $"{desiredName}@{packageReference.Version}";

                    // check to see if this package exists anywhere
                    if (!await PackageExists(desiredName))
                    {
                        continue;
                    }

                    // install this package
                    List<PackageReference> deps = await InstallPackages([desiredMoniker], null, fhirVersions);

                    if (_processedMonikers.Contains(desiredMoniker))
                    {
                        _logger.LogInformation($"Package {desiredMoniker} loaded for {packageReference.Moniker}!");
                    }
                    else
                    {
                        _logger.LogInformation($"Could not find substitute for {packageReference.Moniker} - please specify manually if this is required!");
                    }

                    localPackages.AddRange(deps);
                }
            }
        }

        return localPackages;
    }

    private async ValueTask<PackageReference> ResolveLatest(string name)
    {
        List<SemVer> orderedRegistryWinners = [];

        foreach (Lazy<IRegistryClient?> registration in _packageClients)
        {
            if (registration.Value is not IRegistryClient server)
            {
                continue;
            }
            List<SemVer> versions = await GetVersions(server, name);
            if (versions.Count == 0)
            {
                continue;
            }
            orderedRegistryWinners.Add(versions[^1]);
        }

        SemVer? winner = RegistryLatestVersionSelector.SelectWinner(orderedRegistryWinners);
        return winner is null ? PackageReference.None : new PackageReference(name, winner.ToString());
    }

    /// <summary>
    /// Installs a package.
    /// </summary>
    /// <param name="packageReference">The package reference.</param>
    /// <returns>A task representing the asynchronous operation. The task result contains a boolean value indicating whether the package was installed successfully.</returns>
    private async Task<bool> InstallPackage(PackageReference packageReference)
    {
        if (_cache is null)
        {
            return false;
        }

        if (packageReference.Scope == FhirCiClient.FhirCiScope)
        {
            await _ciClient.InstallOrUpdate(packageReference, _cache);
            return true;
        }

        foreach (Lazy<IRegistryClient?> registration in _packageClients)
        {
            if (registration.Value is not IRegistryClient pc)
            {
                continue;
            }
            try
            {
                // try to download this package
                string root = pc.Endpoint.Url.TrimEnd('/');
                string name = packageReference.Name!;
                string version = packageReference.Version!;
                string path = pc.Endpoint.Type == FhirPkg.Models.RegistryType.Npm
                    ? (string.IsNullOrEmpty(packageReference.Scope) ? string.Empty : "@" + packageReference.Scope + "/")
                        + $"{name}/-/{name}-{version}.tgz"
                    : $"{name}/{version}";
                FhirPkg.Models.ResolvedDirective resolved = new()
                {
                    Reference = new NativePackageReference(name, version),
                    TarballUri = new Uri(root + "/" + path),
                    SourceRegistry = pc.Endpoint,
                };
                await using FhirPkg.Models.PackageDownloadResult? download = await pc.DownloadAsync(resolved);
                if (download is null)
                {
                    continue;
                }
                using MemoryStream buffer = new();
                await download.Content.CopyToAsync(buffer);

                // try to install this package
                await _cache.Install(packageReference, buffer.ToArray());

                // only need to install from first hit
                return true;
            }
            catch (PackageInstallException exception) when (LimitSetting(exception.ErrorCode) is not null)
            {
                _logger.LogWarning("Package import limit exceeded: {Code}; configure {Setting}.",
                    exception.ErrorCode, LimitSetting(exception.ErrorCode));
            }
            catch (Exception)
            {
                // ignore
            }
        }

        return false;
    }

    private static string? LimitSetting(PackageInstallErrorCode code) => code switch
    {
        PackageInstallErrorCode.CompressedSizeLimitExceeded => "--package-max-compressed-bytes",
        PackageInstallErrorCode.ExpandedSizeLimitExceeded => "--package-max-expanded-bytes",
        PackageInstallErrorCode.EntrySizeLimitExceeded => "--package-max-entry-bytes",
        PackageInstallErrorCode.ArchiveEntryCountLimitExceeded => "--package-max-archive-entries",
        PackageInstallErrorCode.ArchivePathLengthLimitExceeded => "--package-max-archive-path-length",
        PackageInstallErrorCode.ArchiveDepthLimitExceeded => "--package-max-archive-depth",
        _ => null,
    };

    private async Task<bool> PackageExists(string packageId)
    {
        if (_cache is null)
        {
            return false;
        }

        foreach (Lazy<IRegistryClient?> registration in _packageClients)
        {
            if (registration.Value is not IRegistryClient pc)
            {
                continue;
            }
            try
            {
                List<SemVer> versions = await GetVersions(pc, packageId);

                if (versions.Count > 0)
                {
                    return true;
                }
            }
            catch (Exception)
            {
                // ignore
            }
        }

        return false;
    }

    /// <summary>
    /// Retrieves the FHIR versions supported by a package.
    /// </summary>
    /// <param name="packageReference">The package reference.</param>
    /// <returns>A list of FHIR sequence codes representing the supported versions.</returns>
    public async Task<List<FhirReleases.FhirSequenceCodes>?> InstalledPackageFhirVersions(PackageReference packageReference)
    {
        if (_cache is null)
        {
            return null;
        }

        if (!await _cache.IsInstalled(packageReference))
        {
            return null;
        }

        _ForPackages.PackageManifest manifest = await _cache.ReadManifestEx(packageReference) ?? throw new Exception("Failed to load package manifest");

        return manifest.AnyFhirVersions?.Select(FhirReleases.FhirVersionToSequence).ToList();
    }

    /// <summary>
    /// Gets the content directory for a specific package.
    /// </summary>
    /// <param name="packageReference">The package reference.</param>
    /// <returns>The content directory for the package, or null if the cache is not configured.</returns>
    public string? GetPackageContentDirectory(PackageReference packageReference)
    {
        if (_cache is null)
        {
            return null;
        }

        return _cache.PackageContentFolder(packageReference);
    }

    /// <summary>
    /// Deletes a package based on the provided package directive.
    /// </summary>
    /// <param name="packageDirective">The package directive specifying the package to delete.</param>
    public void DeletePackage(string packageDirective)
    {
        if (_cache is null)
        {
            return;
        }

        string[] components = packageDirective.Split('@', '#');

        if (components.Length != 2)
        {
            _logger.LogWarning($"DeletePackage <<< invalid package directive: {packageDirective}");
            return;
        }

        _ = _cache.Delete(new PackageReference(components[0], components[1]));
    }

    /// <summary>
    /// Gets the version handling type based on the provided version string.
    /// </summary>
    /// <param name="version">The version string.</param>
    /// <returns>The version handling type.</returns>
    private VersionHandlingTypes GetVersionHandlingType(string? version)
    {
        // handle simple literals
        switch (version)
        {
            case null:
            case "":
            case "latest":
                return VersionHandlingTypes.Latest;

            case "current":
                return VersionHandlingTypes.ContinuousIntegration;

            case "dev":
                return VersionHandlingTypes.Local;
        }

        // check for local or current with branch names
        if (version.StartsWith("current$", StringComparison.Ordinal))
        {
            return VersionHandlingTypes.ContinuousIntegration;
        }

        if (version.StartsWith("dev$", StringComparison.Ordinal))
        {
            return VersionHandlingTypes.Local;
        }

        return VersionHandlingTypes.Passthrough;
    }

    /// <summary>
    /// Resolves the CI literals into standard directives.
    /// </summary>
    /// <param name="ciLiterals">The CI literals to resolve.</param>
    /// <returns>A list of resolved directives.</returns>
    private async Task<List<string>> ResolveCiLiterals(string[]? ciLiterals)
    {
        List<string> directives = [];

        // iterate over CI directives to resolve them into standard directives
        foreach (string literal in ciLiterals ?? Array.Empty<string>())
        {
            // check to see if this is a tagged package literal
            if (literal.EndsWith("current") ||
                literal.Contains("current$"))
            {
                directives.Add(literal);
                continue;
            }

            // try the repository reference first
            List<PackageCatalogEntry> entries = await _ciClient.CatalogPackagesAsync(repo: literal);
            if (entries.Count == 0)
            {
                // check for a publication URL
                entries = await _ciClient.CatalogPackagesAsync(site: literal);
            }

            if (entries.Count == 0)
            {
                // check for a package name
                entries = await _ciClient.CatalogPackagesAsync(pkgname: literal);
            }

            if (entries.Count == 0)
            {
                _logger.LogWarning($"ResolveCiLiterals <<< cannot resolve CI directive: {literal}!");
                continue;
            }

            PackageCatalogEntry entry = entries.First();

            // check to see if we have a package name and repository URL
            if (string.IsNullOrEmpty(entry.Name) ||
                string.IsNullOrEmpty(entry.Description) ||
                !entry.Description.Contains('/'))
            {
                _logger.LogWarning($"ResolveCiLiterals <<< invalid resolution for CI directive: {literal}! Name: {entry.Name}, Description: {entry.Description}");
                continue;
            }

            // get the branch name from the repo url
            (string? branchName, bool isDefaultBranch) = FhirCiClient.GetBranchNameRepoLiteral(entry.Description);

            if (isDefaultBranch)
            {
                directives.Add(entry.Name + "#current");
                continue;
            }

            if (string.IsNullOrEmpty(branchName))
            {
                _logger.LogWarning($"ResolveCiLiterals <<< invalid resolution for CI directive: {literal} - no branch name and not default branch!");
                continue;
            }

            directives.Add(entry.Name + "#current$" + branchName);
        }

        return directives;
    }

    /// <summary>Updates the package state.</summary>
    /// <param name="directive">      The directive.</param>
    /// <param name="resolvedName">   Name of the resolved.</param>
    /// <param name="resolvedVersion">The resolved version.</param>
    /// <param name="toState">        State of to.</param>
    public void UpdatePackageState(
        string directive,
        string resolvedName,
        string resolvedVersion,
        PackageLoadStateEnum toState)
    {
        if (!_packagesByDirective.ContainsKey(directive))
        {
            _packagesByDirective.Add(directive, new()
            {
                CacheDirective = directive,
                PackageState = toState,
            });
        }

        _packagesByDirective[directive] = _packagesByDirective[directive] with
        {
            PackageState = toState,
            PackageName = string.IsNullOrEmpty(resolvedName) ? _packagesByDirective[directive].PackageName : resolvedName,
            Version = string.IsNullOrEmpty(resolvedVersion) ? _packagesByDirective[directive].Version : resolvedVersion,
        };

        StateHasChanged();
    }

    /// <summary>
    /// Attempts to get a package state, returning a default value rather than throwing an exception
    /// if it fails.
    /// </summary>
    /// <param name="directive">The directive.</param>
    /// <param name="state">    [out] The state.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    public bool TryGetPackageState(string directive, out PackageLoadStateEnum state)
    {
        if (!_packagesByDirective.TryGetValue(directive, out PackageCacheRecord cacheRecord))
        {
            state = PackageLoadStateEnum.Unknown;
            return false;
        }

        state = cacheRecord.PackageState;
        return true;
    }

    /// <summary>
    /// Releases the unmanaged resources used by the <see cref="FhirPackageService"/>
    /// and optionally releases the managed resources.
    /// </summary>
    /// <param name="disposing">True to release both managed and unmanaged resources; false to
    ///  release only unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                foreach (Lazy<IRegistryClient?> registration in _packageClients)
                {
                    if (registration.IsValueCreated && registration.Value is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                }
                _registryHttpClient.Dispose();
            }

            // free unmanaged resources (unmanaged objects) and override finalizer
            // set large fields to null
            _disposedValue = true;
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

    /// <summary>State has changed.</summary>
    public void StateHasChanged()
    {
        OnChanged?.Invoke(this, new());
    }
}
