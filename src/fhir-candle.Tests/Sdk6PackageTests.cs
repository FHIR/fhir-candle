// <copyright file="Sdk6PackageTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using fhir.candle._ForPackages;
using fhir.candle.Services;
using fhir.candle.Tests.Controllers;
using fhir.candle.Tests.Extensions;
using fhir.candle.Tests.Models;
using FhirCandle.Configuration;
using FhirCandle.Models;
using FhirCandle.Storage;
using FhirCandle.Utils;
using Firely.Fhir.Packages;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;
using DiskPackageCache = fhir.candle._ForPackages.DiskPackageCache;
using PackageManifest = fhir.candle._ForPackages.PackageManifest;

namespace fhir.candle.Tests;

/// <summary>Exercises the real package consumers without public registries, ambient data, or SDK-authored expectations.</summary>
[Collection(CompatibilityHostCollection.Name)]
public sealed class Sdk6PackageTests
{
    private static readonly FhirReleases.FhirSequenceCodes[] _versions =
        [FhirReleases.FhirSequenceCodes.R4, FhirReleases.FhirSequenceCodes.R4B, FhirReleases.FhirSequenceCodes.R5];
    private readonly ITestOutputHelper _output;

    /// <summary>Creates a package contract test with request/provenance output.</summary>
    /// <param name="output">The current xUnit test output.</param>
    public Sdk6PackageTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>Each supported version has an independent pinned archive and cache lifetime.</summary>
    public static IEnumerable<object[]> VersionCases => _versions.Select(version =>
        new object[] { $"offline-package / {version} / GET archive / JSON+XML load", version });

    /// <summary>Protects the labeled, nonempty version inventory from accidental theory removal.</summary>
    [Fact]
    public void PackageIntegrationInventoryIsNonempty()
    {
        object[][] rows = VersionCases.ToArray();
        Assert.Equal(3, rows.Length);
        Assert.Equal(_versions, rows.Select(row => Assert.IsType<FhirReleases.FhirSequenceCodes>(row[1])));
        Assert.All(rows, row => Assert.False(string.IsNullOrEmpty(Assert.IsType<string>(row[0]))));
        Assert.Equal(3, rows.Select(row => row[0]).Distinct().Count());
        _output.WriteLine("Package inventory: acquisition/repeat/fresh-cache/manager-first-load/corrupt/empty-cache; CI listing/GetPackage/InstallOrUpdate cold/warm/replacement/missing; six custom initial-data tenants, JSON+XML.");
    }

    /// <summary>Acquisition, persistent cache reuse, and normal manager loading use three distinct service instances.</summary>
    [Theory]
    [MemberData(nameof(VersionCases))]
    public async Task LocalPackageAcquisitionCacheAndLoadingPreserveResources(string label, FhirReleases.FhirSequenceCodes version)
    {
        using LocalPackageFixture files = new();
        _output.WriteLine($"{label}; owned temporary root: {files.Root}");
        string name = LocalPackageFixture.PackageName(version);
        string directive = name + "#" + LocalPackageFixture.PackageVersion;
        string moniker = name + "@" + LocalPackageFixture.PackageVersion;
        byte[] archive = LocalPackageFixture.VersionArchive(version);
        await using LocalHttpFixture registry = await LocalHttpFixture.StartAsync();
        LocalHttpFixture.ExpectedRequest download = registry.Expect("GET", "/" + name + "/1.0.0",
            new LocalHttpFixture.RawResponse(HttpStatusCode.OK, "application/octet-stream", archive));

        using (FhirPackageService acquisition = PackageService(files.Configuration(registry.Address.AbsoluteUri.TrimEnd('/'))))
        {
            acquisition.Init();
            Assert.True(acquisition.IsConfigured);
            Assert.True(acquisition.IsReady);
            PackageReference installed = Assert.Single(await acquisition.InstallPackages([directive], null, [version]).WaitAsync(LocalHttpFixture.Deadline));
            Assert.Equal(moniker, installed.Moniker);
            Assert.Equal(name, installed.Name);
            Assert.Equal("1.0.0", installed.Version);
            await AssertInstalledManifest(files, acquisition, installed, version);

            // This is instance-local deduplication, not the persistent-cache assertion below.
            Assert.Empty(await acquisition.InstallPackages([directive, directive], null, [version]).WaitAsync(LocalHttpFixture.Deadline));
            LocalHttpFixture.CapturedRequest request = await download.WaitForRequestAsync();
            AssertRegistryRequest(request, registry, "/" + name + "/1.0.0");
            Assert.False(await download.WaitForCompletionAsync());
            Assert.Single(registry.Requests);
        }
        registry.AssertComplete();
        await registry.DisposeAsync();

        using (FhirPackageService warm = PackageService(files.Configuration()))
        {
            warm.Init();
            Assert.True(warm.IsReady);
            PackageReference cached = Assert.Single(await warm.InstallPackages([directive], [], [version]).WaitAsync(LocalHttpFixture.Deadline));
            Assert.Equal(moniker, cached.Moniker);
            await AssertInstalledManifest(files, warm, cached, version);
        }

        CompatibilityBindings bindings = new();
        CandleConfig managerConfig = files.Configuration() with { PublishedPackages = [directive] };
        using (FhirPackageService managerPackages = PackageService(managerConfig))
        {
            Dictionary<string, TenantConfiguration> tenants = _versions.ToDictionary(
                candidate => "package-" + LocalPackageFixture.VersionName(candidate),
                candidate => Tenant(candidate, "package-" + LocalPackageFixture.VersionName(candidate)));
            FhirStoreManager manager = new(tenants, NullLogger<FhirStoreManager>.Instance, managerConfig, managerPackages);
            try
            {
                // In particular, do NOT call InstallPackages on managerPackages before Init.
                manager.Init();
                Assert.True(managerPackages.IsReady);
                foreach (IFhirStore store in ((IFhirStoreManager)manager).Values)
                {
                    WireContractAssertions.BindBaseUrl(bindings, store.Config.ControllerName, store.Config.BaseUrl);
                    if (store.Config.FhirVersion != version)
                    {
                        Assert.Empty(store["Patient"].Keys);
                        Assert.Empty(store["Observation"].Keys);
                        Assert.DoesNotContain(moniker, store.LoadedPackageDirectives);
                        Assert.DoesNotContain(name, store.LoadedPackageIds);
                        continue;
                    }

                    Assert.Equal(new[] { moniker }, store.LoadedPackageDirectives);
                    Assert.Contains(name, store.LoadedPackageIds);
                    Assert.Equal(new[] { "wire-patient" }, store["Patient"].Keys);
                    Assert.Equal(new[] { "package-observation" }, store["Observation"].Keys);
                    AssertLoadedResource(store, bindings, version, "Patient", "wire-patient", "patient");
                    AssertLoadedResource(store, bindings, version, "Observation", "package-observation", "observation");
                }
            }
            finally
            {
                await StopAndDispose(manager);
            }
        }

        await AssertMissingAndCorruptPackages(files, version);
        Assert.Single(registry.Requests);
    }

    /// <summary>Uses the real CI entry point and companion cache, including both inherited author representations.</summary>
    [Theory]
    [MemberData(nameof(VersionCases))]
    public async Task CiPackageClientPreservesListingDownloadAndManifestContract(string label, FhirReleases.FhirSequenceCodes version)
    {
        using LocalPackageFixture files = new();
        _output.WriteLine($"{label}; CI contract; owned temporary root: {files.Root}");
        await using LocalHttpFixture listener = await LocalHttpFixture.StartAsync();
        string name = LocalPackageFixture.PackageName(version) + ".ci";
        const string QaUri = "https://build.fhir.org/ig/qas.json";
        const string BranchesUri = "https://build.fhir.org/branches/";
        const string ArchivePath = "/ig/example/sdk6/branches/main/package.tgz";
        const string ArchiveUri = "https://build.fhir.org" + ArchivePath;
        string[] sequence = [QaUri, BranchesUri, ArchiveUri, ArchiveUri, QaUri, BranchesUri, ArchiveUri];
        using LocalHttpFixture.MappedLoopbackHandler mapping = listener.MapCiRequests(sequence);
        using HttpClient http = new(mapping) { Timeout = LocalHttpFixture.Deadline };
        using FhirCiClient client = new(client: http);
        DiskPackageCache cache = new(files.CacheDirectory);
        string coldManifest = CiManifest(version, name, replacement: false);
        string replacementManifest = CiManifest(version, name, replacement: true);
        const string OriginalResource = """{"resourceType":"Patient","id":"ci-original","active":true,"name":[{"family":"Original synthetic CI"}]}""";
        const string ReplacementResource = """{"resourceType":"Patient","id":"ci-replacement","active":false,"name":[{"family":"Replacement synthetic CI"}]}""";
        byte[] coldArchive = LocalPackageFixture.Archive(coldManifest, new Dictionary<string, string> { ["Patient-ci-original.json"] = OriginalResource });
        byte[] replacementArchive = LocalPackageFixture.Archive(replacementManifest,
            new Dictionary<string, string> { ["Patient-ci-replacement.json"] = ReplacementResource });
        List<LocalHttpFixture.ExpectedRequest> routes =
        [
            listener.Expect("GET", "/ig/qas.json", LocalHttpFixture.RawResponse.Text(HttpStatusCode.OK, "application/json", QaListing(version, name, false))),
            listener.Expect("GET", "/branches/", LocalHttpFixture.RawResponse.Text(HttpStatusCode.OK, "application/json", "[]")),
            listener.Expect("GET", ArchivePath, new LocalHttpFixture.RawResponse(HttpStatusCode.OK, "application/octet-stream", coldArchive)),
            listener.Expect("GET", ArchivePath, new LocalHttpFixture.RawResponse(HttpStatusCode.OK, "application/octet-stream", coldArchive)),
            listener.Expect("GET", "/ig/qas.json", LocalHttpFixture.RawResponse.Text(HttpStatusCode.OK, "application/json", QaListing(version, name, true))),
            listener.Expect("GET", "/branches/", LocalHttpFixture.RawResponse.Text(HttpStatusCode.OK, "application/json", "[]")),
            listener.Expect("GET", ArchivePath, new LocalHttpFixture.RawResponse(HttpStatusCode.OK, "application/octet-stream", replacementArchive)),
        ];

        PackageListing listing = Assert.IsType<PackageListing>(await client.DownloadListingAsync(name).AsTask().WaitAsync(LocalHttpFixture.Deadline));
        Assert.Equal(name, listing.Id);
        Assert.Equal("Synthetic SDK6 CI", listing.Name);
        Assert.Equal("CI Build of " + name, listing.Description);
        Assert.NotNull(listing.DistTags);
        Assert.Equal(2, listing.DistTags.Count);
        Assert.Equal("1.0.0-cibuild+20240101-000000Z", listing.DistTags["current"]);
        Assert.Equal(listing.DistTags["current"], listing.DistTags["current$main"]);
        PackageRelease release = Assert.Single(Assert.IsType<Dictionary<string, PackageRelease>>(listing.Versions)).Value;
        Assert.Equal(name, release.Name);
        Assert.Equal("1.0.0-cibuild+20240101-000000Z", release.Version);
        Assert.Equal(LocalPackageFixture.FhirVersion(version), release.FhirVersion);
        Assert.Equal("Synthetic QA description", release.Description);
        Assert.Equal("https://example.test/sdk6-ci", release.Url);
        Assert.Equal("https://example.test/sdk6-ci", Assert.IsType<Dist>(release.Dist).Tarball);

        (PackageReference tagged, PackageReference resolved) = await client.GetReferences(name, "current");
        AssertCiReferences(tagged, resolved, name, "1.0.0-cibuild+20240101-000000Z");
        Assert.Equal("current$main", (await client.GetTagBasedReference(name, "current$main")).Version);
        Assert.Equal(coldArchive, await client.GetPackage(resolved).WaitAsync(LocalHttpFixture.Deadline));
        Assert.False(await cache.IsInstalled(tagged));

        await client.InstallOrUpdate(resolved, cache).WaitAsync(LocalHttpFixture.Deadline);
        Assert.True(await cache.IsInstalled(tagged));
        Assert.False(await cache.IsInstalled(resolved));
        Assert.False(await client.IsPackageOutdated(tagged, cache));
        PackageManifest installed = Assert.IsType<PackageManifest>(await cache.ReadManifestEx(tagged));
        AssertCiManifest(installed, version, name, replacement: false);
        string contentDirectory = cache.PackageContentFolder(tagged);
        WireContractAssertions.Content(coldManifest, File.ReadAllText(Path.Combine(contentDirectory, "package.json")), CompatibilityFormat.Json);
        WireContractAssertions.Content(OriginalResource, File.ReadAllText(Path.Combine(contentDirectory, "Patient-ci-original.json")), CompatibilityFormat.Json);

        int beforeWarm = listener.Requests.Count;
        await client.InstallOrUpdate(tagged, cache).WaitAsync(LocalHttpFixture.Deadline);
        Assert.Equal(4, beforeWarm);
        Assert.Equal(beforeWarm, listener.Requests.Count);
        AssertCiManifest(Assert.IsType<PackageManifest>(await cache.ReadManifestEx(tagged)), version, name, false);

        await client.UpdateCiListingCache().WaitAsync(LocalHttpFixture.Deadline);
        Assert.True(await client.IsPackageOutdated(tagged, cache));
        (PackageReference newTag, PackageReference newResolved) = await client.GetReferences(name, "current");
        AssertCiReferences(newTag, newResolved, name, "1.1.0-cibuild+20240105-000000Z");
        Assert.Equal(tagged, newTag);
        await client.InstallOrUpdate(newResolved, cache).WaitAsync(LocalHttpFixture.Deadline);
        Assert.True(await cache.IsInstalled(tagged));
        Assert.False(await cache.IsInstalled(newResolved));
        Assert.False(await client.IsPackageOutdated(tagged, cache));
        AssertCiManifest(Assert.IsType<PackageManifest>(await cache.ReadManifestEx(tagged)), version, name, true);
        Assert.False(File.Exists(Path.Combine(contentDirectory, "Patient-ci-original.json")));
        WireContractAssertions.Content(ReplacementResource, File.ReadAllText(Path.Combine(contentDirectory, "Patient-ci-replacement.json")), CompatibilityFormat.Json);
        WireContractAssertions.Content(replacementManifest, File.ReadAllText(Path.Combine(contentDirectory, "package.json")), CompatibilityFormat.Json);

        PackageReference missing = new(FhirCiClient.FhirCiScope, "example.test.sdk6.missing", "current");
        Assert.Null(await client.DownloadListingAsync(missing.Name!));
        Assert.Equal(PackageReference.None, await client.GetTagBasedReference(missing.Name, missing.Version));
        Assert.Null(await client.IsPackageOutdated(missing, cache));
        Exception missingDownload = await Assert.ThrowsAsync<Exception>(() => client.GetPackage(missing));
        Assert.Contains("Cannot resolve", missingDownload.Message);
        Exception missingInstall = await Assert.ThrowsAsync<Exception>(() => client.InstallOrUpdate(missing, cache));
        Assert.Contains("Could not resolve", missingInstall.Message);
        Assert.False(await cache.IsInstalled(missing));
        await Assert.ThrowsAsync<InvalidOperationException>(() => http.GetAsync("https://build.fhir.org/ig/not-allowed/package.tgz"));
        Assert.Equal(sequence, mapping.Forwarded);
        Assert.Equal(sequence, listener.Requests.Select(request => request.Header("X-Sdk6-Original-Uri")));
        foreach (LocalHttpFixture.ExpectedRequest route in routes)
        {
            LocalHttpFixture.CapturedRequest request = await route.WaitForRequestAsync();
            AssertRegistryRequest(request, listener, route.Path);
            if (request.Path != ArchivePath)
            {
                Assert.Equal("application/json", request.Header("Accept"));
            }
            Assert.False(await route.WaitForCompletionAsync());
        }
        mapping.AssertComplete();
        listener.AssertComplete();
    }

    /// <summary>The documented custom tenant directory fallback loads JSON and separately authored XML without sibling leakage.</summary>
    [Fact]
    public async Task InitialDataUsesTenantDirectories()
    {
        using LocalPackageFixture files = new();
        CompatibilityBindings bindings = new();
        Dictionary<string, TenantConfiguration> tenants = new(StringComparer.Ordinal);
        foreach (FhirReleases.FhirSequenceCodes version in _versions)
        {
            foreach (string sibling in new[] { "oak", "elm" })
            {
                string name = $"source-{sibling}-{LocalPackageFixture.VersionName(version)}";
                tenants.Add(name, Tenant(version, name));
                files.WriteInitialResource(name, "patient.json",
                    LocalPackageFixture.WithId(CompatibilityReplay.LoadFixture(version, "patient.json"), name + "-patient"));
                files.WriteInitialResource(name, "observation.xml", LocalPackageFixture.WithId(
                    CompatibilityReplay.LoadFixture(version, "observation.xml").Replace("Patient/wire-patient", "Patient/" + name + "-patient", StringComparison.Ordinal),
                    name + "-observation"));
            }
        }
        _output.WriteLine($"InitialDataUsesTenantDirectories / R4,R4B,R5 / JSON+XML / {string.Join(", ", tenants.Keys)}; owned root: {files.Root}");
        CandleConfig config = files.Configuration();
        using FhirPackageService packages = PackageService(config);
        FhirStoreManager manager = new(tenants, NullLogger<FhirStoreManager>.Instance, config, packages);
        try
        {
            manager.Init();
            Assert.Equal(6, ((IFhirStoreManager)manager).Count);
            foreach (IFhirStore store in ((IFhirStoreManager)manager).Values)
            {
                string name = store.Config.ControllerName;
                WireContractAssertions.BindBaseUrl(bindings, name, store.Config.BaseUrl);
                Assert.Equal(new[] { name + "-patient" }, store["Patient"].Keys);
                Assert.Equal(new[] { name + "-observation" }, store["Observation"].Keys);
                Assert.Empty(store.LoadedPackageDirectives);
                Assert.Empty(store.LoadedPackageIds);
                Assert.Equal(new[] { Path.Combine(files.SourceDirectory, name) }, store.LoadedSupplements);
                AssertLoadedResource(store, bindings, store.Config.FhirVersion, "Patient", name + "-patient", "patient");
                AssertLoadedResource(store, bindings, store.Config.FhirVersion, "Observation", name + "-observation", "observation", name + "-patient");
                foreach (string sibling in tenants.Keys.Where(candidate => candidate != name))
                {
                    Assert.False(store["Patient"].ContainsKey(sibling + "-patient"));
                    Assert.False(store["Observation"].ContainsKey(sibling + "-observation"));
                }
            }
        }
        finally
        {
            await StopAndDispose(manager);
        }
    }

    private static FhirPackageService PackageService(CandleConfig config) => new(NullLogger<FhirPackageService>.Instance, config);

    private static TenantConfiguration Tenant(FhirReleases.FhirSequenceCodes version, string name) => new()
    {
        FhirVersion = version,
        ControllerName = name,
        BaseUrl = "http://127.0.0.1/fhir/" + name,
        AllowExistingId = true,
        AllowCreateAsUpdate = true,
    };

    private async Task AssertMissingAndCorruptPackages(LocalPackageFixture files, FhirReleases.FhirSequenceCodes version)
    {
        string name = LocalPackageFixture.PackageName(version);
        string directive = name + "#1.0.0";
        CandleConfig emptyConfig = files.Configuration() with { FhirCacheDirectory = files.CreateDirectory("empty-cache") };
        using (FhirPackageService missing = PackageService(emptyConfig))
        {
            missing.Init();
            Exception exception = await Assert.ThrowsAsync<Exception>(() => missing.InstallPackages([directive], [], [version]));
            Assert.Equal($"Failed to install package {name}@1.0.0 as requested by {directive}", exception.Message);
        }

        await using LocalHttpFixture registry = await LocalHttpFixture.StartAsync();
        LocalHttpFixture.ExpectedRequest corrupt = registry.Expect("GET", "/" + name + "/1.0.0",
            new LocalHttpFixture.RawResponse(HttpStatusCode.OK, "application/octet-stream", Encoding.UTF8.GetBytes("Synthetic invalid gzip, not a tar archive.")));
        CandleConfig corruptConfig = files.Configuration(registry.Address.AbsoluteUri.TrimEnd('/')) with
        {
            FhirCacheDirectory = files.CreateDirectory("corrupt-cache"),
        };
        using (FhirPackageService broken = PackageService(corruptConfig))
        {
            broken.Init();
            Exception exception = await Assert.ThrowsAsync<Exception>(() => broken.InstallPackages([directive], [], [version]).WaitAsync(LocalHttpFixture.Deadline));
            Assert.Equal($"Failed to install package {name}@1.0.0 as requested by {directive}", exception.Message);
            DiskPackageCache cache = new(corruptConfig.FhirCacheDirectory);
            PackageReference reference = new(name, "1.0.0");
            Assert.Null(await cache.ReadManifestEx(reference));
            Assert.False(File.Exists(Path.Combine(cache.PackageContentFolder(reference), "Patient-wire-patient.json")));
        }
        AssertRegistryRequest(await corrupt.WaitForRequestAsync(), registry, "/" + name + "/1.0.0");
        Assert.False(await corrupt.WaitForCompletionAsync());
        registry.AssertComplete();
    }

    private static async Task AssertInstalledManifest(
        LocalPackageFixture files, FhirPackageService service, PackageReference reference, FhirReleases.FhirSequenceCodes version)
    {
        Assert.Equal(new[] { version }, await service.InstalledPackageFhirVersions(reference));
        string directory = Assert.IsType<string>(service.GetPackageContentDirectory(reference));
        Assert.StartsWith(files.CacheDirectory + Path.DirectorySeparatorChar, directory, StringComparison.OrdinalIgnoreCase);
        DiskPackageCache cache = new(files.CacheDirectory);
        PackageManifest manifest = Assert.IsType<PackageManifest>(await cache.ReadManifestEx(reference));
        Assert.Equal(LocalPackageFixture.PackageName(version), manifest.Name);
        Assert.Equal("1.0.0", manifest.Version);
        Assert.Equal(new[] { LocalPackageFixture.FhirVersion(version) }, manifest.AnyFhirVersions);
        Assert.NotNull(manifest.Dependencies);
        Assert.Empty(manifest.Dependencies);
        Assert.Equal("https://example.test/sdk6/" + LocalPackageFixture.VersionName(version), manifest.Canonical);
        AssertAuthor(manifest, "Synthetic Package Author", "author@example.test", "https://example.test/sdk6");
        WireContractAssertions.Content(LocalPackageFixture.Manifest(version), File.ReadAllText(Path.Combine(directory, "package.json")), CompatibilityFormat.Json);
        WireContractAssertions.Content(LocalPackageFixture.WithId(CompatibilityReplay.LoadFixture(version, "patient.json"), "wire-patient"),
            File.ReadAllText(Path.Combine(directory, "Patient-wire-patient.json")), CompatibilityFormat.Json);
        WireContractAssertions.Content(LocalPackageFixture.WithId(CompatibilityReplay.LoadFixture(version, "observation.xml"), "package-observation"),
            File.ReadAllText(Path.Combine(directory, "Observation-package-observation.xml")), CompatibilityFormat.Xml);
    }

    private void AssertRegistryRequest(LocalHttpFixture.CapturedRequest request, LocalHttpFixture fixture, string path)
    {
        Assert.Equal("GET", request.Method);
        Assert.Equal(path, request.Path);
        Assert.Equal(path, request.RawTarget);
        Assert.Empty(request.Query);
        Assert.Empty(request.Body);
        Assert.Equal(fixture.Address.Authority, request.Header("Host"));
        _output.WriteLine($"Captured {request.Method} {fixture.Address.GetLeftPart(UriPartial.Authority)}{request.RawTarget}; original CI URI: {request.Header("X-Sdk6-Original-Uri")}");
    }

    private static void AssertLoadedResource(
        IFhirStore store, CompatibilityBindings bindings, FhirReleases.FhirSequenceCodes version,
        string resourceType, string id, string fixtureName, string? patientId = null)
    {
        GeneratedResourceExpectation binding = new(store.Config.ControllerName + "/" + id, resourceType, "1", id);
        foreach (CompatibilityFormat format in new[] { CompatibilityFormat.Json, CompatibilityFormat.Xml })
        {
            string suffix = format == CompatibilityFormat.Json ? ".json" : ".xml";
            string expected = LocalPackageFixture.WithId(CompatibilityReplay.LoadFixture(version, fixtureName + suffix), id);
            if (patientId is not null)
            {
                expected = expected.Replace("Patient/wire-patient", "Patient/" + patientId, StringComparison.Ordinal);
            }
            FhirRequestContext request = new(store, "GET", resourceType + "/" + id)
            {
                DestinationFormat = WireContractAssertions.MediaType(format),
            };
            Assert.True(store.PerformInteraction(request, out FhirResponseContext response, serializeReturn: true));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(resourceType, response.ResourceType);
            Assert.Equal(id, response.Id);
            Assert.False(string.IsNullOrEmpty(response.SerializedOutcome));
            WireContractAssertions.Body(new ResourceBodyExpectation(expected, binding), response.SerializedResource, format, store.Config.BaseUrl, bindings);
            binding = binding with { Mode = CompatibilityBindingMode.Reuse };
        }
    }

    private static string QaListing(FhirReleases.FhirSequenceCodes version, string name, bool replacement) => $$"""
        [{
          "url":"https://example.test/sdk6-ci/ImplementationGuide/synthetic",
          "name":"Synthetic SDK6 CI",
          "title":"Synthetic CI title",
          "description":"Synthetic QA description",
          "status":"active",
          "package-id":"{{name}}",
          "ig-ver":"{{(replacement ? "1.1.0" : "1.0.0")}}",
          "dateISO8601":"{{(replacement ? "2024-01-05T00:00:00Z" : "2024-01-01T00:00:00Z")}}",
          "version":"{{LocalPackageFixture.FhirVersion(version)}}",
          "repo":"example/sdk6/branches/main/qa.json"
        }]
        """;

    private static string CiManifest(FhirReleases.FhirSequenceCodes version, string name, bool replacement)
    {
        JsonObject manifest = JsonNode.Parse(LocalPackageFixture.Manifest(version))!.AsObject();
        manifest["name"] = name;
        manifest["version"] = replacement ? "1.1.0" : "1.0.0";
        manifest["date"] = replacement ? "20240106120000" : "20240102120000";
        if (replacement)
        {
            manifest["author"] = "Synthetic CI Author <ci@example.test> (https://example.test/ci)";
        }
        return manifest.ToJsonString();
    }

    private static void AssertCiReferences(PackageReference tagged, PackageReference resolved, string name, string resolvedVersion)
    {
        Assert.Equal(FhirCiClient.FhirCiScope, tagged.Scope);
        Assert.Equal(FhirCiClient.FhirCiScope, resolved.Scope);
        Assert.Equal(name, tagged.Name);
        Assert.Equal(name, resolved.Name);
        Assert.Equal("current", tagged.Version);
        Assert.Equal(resolvedVersion, resolved.Version);
        Assert.NotEqual(tagged, resolved);
    }

    private static void AssertCiManifest(PackageManifest manifest, FhirReleases.FhirSequenceCodes version, string name, bool replacement)
    {
        Assert.Equal(name, manifest.Name);
        Assert.Equal(replacement ? "1.1.0" : "1.0.0", manifest.Version);
        Assert.Equal(new[] { LocalPackageFixture.FhirVersion(version) }, manifest.AnyFhirVersions);
        Assert.Equal("Conformance", manifest.Type);
        Assert.Equal("MIT", manifest.License);
        Assert.Equal("package", manifest.Directories!["lib"]);
        Assert.NotNull(manifest.Dependencies);
        Assert.Empty(manifest.Dependencies);
        // The existing compact manifest-date contract is local time, not ISO/UTC parsing.
        DateTime localDate = new(2024, 1, replacement ? 6 : 2, 12, 0, 0, DateTimeKind.Unspecified);
        Assert.Equal(new DateTimeOffset(localDate, TimeZoneInfo.Local.GetUtcOffset(localDate)), manifest.Date);
        AssertAuthor(manifest,
            replacement ? "Synthetic CI Author" : "Synthetic Package Author",
            replacement ? "ci@example.test" : "author@example.test",
            replacement ? "https://example.test/ci" : "https://example.test/sdk6");
    }

    private static void AssertAuthor(PackageManifest manifest, string name, string email, string url)
    {
        Firely.Fhir.Packages.AuthorInfo author = Assert.IsAssignableFrom<Firely.Fhir.Packages.AuthorInfo>(manifest.AuthorInformation);
        Assert.Equal(name, author.Name);
        Assert.Equal(email, author.Email);
        Assert.Equal(url, author.Url);
        Assert.Equal($"{name} <{email}> ({url})", manifest.Author);
    }

    private static async Task StopAndDispose(FhirStoreManager manager)
    {
        try
        {
            await ((IHostedService)manager).StopAsync(CancellationToken.None).WaitAsync(LocalHttpFixture.Deadline);
        }
        finally
        {
            foreach (IFhirStore store in ((IFhirStoreManager)manager).Values)
            {
                store.Dispose();
            }
            ((IDisposable)manager).Dispose();
        }
    }
}
