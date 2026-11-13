// <copyright file="Sdk6PackageTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using System.Net;
using System.Formats.Tar;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
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
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;
using DiskPackageCache = fhir.candle._ForPackages.DiskPackageCache;
using PackageManifest = fhir.candle._ForPackages.PackageManifest;
using PackageReference = FhirCandle.Models.PackageReference;
using PackageListing = FhirCandle.Models.PackageListing;
using PackageRelease = FhirCandle.Models.PackageRelease;
using Dist = FhirCandle.Models.Dist;

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

    /// <summary>The neutral reference retains the original supplier's data identity, not its parser.</summary>
    [Fact]
    public void PackageReferenceCompatibilityPreservesLegacyIdentity()
    {
        (string? scope, string? name, string? version)[] cases =
        [
            (null, null, null), (null, string.Empty, string.Empty), (null, "Example.Name", null),
            (string.Empty, "Example.Name", string.Empty), ("scope", "Example.Name", "1.0.0"),
            ("other", "example.name", "1.0.0-BETA+Build"), ("@literal", "name@literal", "dev$feature"),
        ];
        string[] monikers = ["@", "@", "Example.Name@", "Example.Name@", "Example.Name@1.0.0",
            "example.name@1.0.0-BETA+Build", "name@literal@dev$feature"];
        bool[] found = [false, true, false, true, true, true, true];
        List<PackageReference> neutral = [];
        for (int index = 0; index < cases.Length; index++)
        {
            (string? scope, string? name, string? version) = cases[index];
            PackageReference newReference = new(scope, name!, version);
            Assert.Equal(scope, newReference.Scope);
            Assert.Equal(name, newReference.Name);
            Assert.Equal(version, newReference.Version);
            Assert.Equal(monikers[index], newReference.Moniker);
            Assert.Equal(monikers[index] + (found[index] ? string.Empty : " (NOT FOUND)"), newReference.ToString());
            Assert.Equal(found[index], newReference.Found);
            Assert.Equal(!found[index], newReference.NotFound);
            // The characterized hash is the name/version tuple hash, not a runtime-stable number.
            Assert.Equal((name, version).GetHashCode(), newReference.GetHashCode());
            (string? newName, string? newVersion) = newReference;
            Assert.Equal((name, version), (newName, newVersion));
            neutral.Add(newReference);
            newReference.Scope = "changed";
            Assert.True(newReference == neutral[^1]);
            Assert.Equal((name, version).GetHashCode(), newReference.GetHashCode());
            Assert.Equal(monikers[index], newReference.Moniker);
        }
        for (int left = 0; left < cases.Length; left++)
        {
            for (int right = 0; right < cases.Length; right++)
            {
                bool equal = cases[left].name == cases[right].name && cases[left].version == cases[right].version;
                Assert.Equal(equal, neutral[left] == neutral[right]);
                Assert.Equal(!equal, neutral[left] != neutral[right]);
                Assert.Equal(equal, neutral[left].Equals((object)neutral[right]));
            }
        }
        Assert.Equal("@", PackageReference.None.Moniker);
        Assert.Equal(((string?)null, (string?)null).GetHashCode(), PackageReference.None.GetHashCode());
        Assert.Equal(default, PackageReference.None);
        Assert.False(PackageReference.None.Equals(null));
        Assert.False(PackageReference.None.Equals(((string?)null, (string?)null)));
        PackageReference unscopedNew = new("Case.Name", "1.0.0-BETA");
        Assert.Null(unscopedNew.Scope);
        Assert.Equal("Case.Name@1.0.0-BETA", unscopedNew.Moniker);
        foreach ((string name, string version) in new[] { ("case.name", "1.0.0-BETA"), ("Case.Name", "1.0.0-beta") })
        {
            Assert.False(unscopedNew == new PackageReference(name, version));
        }
        Assert.True(unscopedNew == new PackageReference("other", "Case.Name", "1.0.0-BETA"));
        Assert.Equal("FhirCandle.Common", typeof(PackageReference).Assembly.GetName().Name);
        Assert.DoesNotContain(typeof(FhirPackageService).Assembly.GetReferencedAssemblies(),
            assembly => assembly.Name == "Firely.Fhir.Packages");

        // The remaining Common shapes carry data only, including nullable CI dictionaries.
        FhirCandle.Models.PackageCatalogEntry catalog = new() { Name = "name", Description = "repo/branches/feature/qa.json", FhirVersion = "5.0.0" };
        FhirCandle.Models.PackageRelease release = new()
        {
            Name = catalog.Name, Version = "1.0.0-cibuild+20240101-000000Z", Description = catalog.Description,
            FhirVersion = catalog.FhirVersion, Url = "https://example.test/ci",
            Dist = new() { Tarball = "https://example.test/ci" },
        };
        FhirCandle.Models.PackageListing listing = new();
        Assert.Null(listing.Id);
        Assert.Null(listing.Name);
        Assert.Null(listing.Description);
        Assert.Null(listing.DistTags);
        Assert.Null(listing.Versions);
        listing.Id = catalog.Name;
        listing.Name = "Synthetic listing";
        listing.Description = "Synthetic description";
        listing.Versions = new() { [release.Version] = release };
        listing.DistTags = new() { ["current$feature"] = release.Version };
        Assert.Same(release, listing.Versions[listing.DistTags["current$feature"]]);
        Assert.Equal(release.Url, release.Dist.Tarball);
        Assert.True(new FhirCandle.Models.Versions().IsEmpty);
        Assert.Empty(new FhirCandle.Models.Versions().Items);
        string[] projected = ["10.0.0", "2.0.0-beta+meta", "not parsed in Common"];
        FhirCandle.Models.Versions versions = new(projected);
        Assert.False(versions.IsEmpty);
        Assert.Equal(projected, versions.Items);
        projected[0] = "changed outside";
        Assert.Equal("10.0.0", versions.Items.First());
    }

    /// <summary>Real service resolution pins parser quirks and prerelease-inclusive, cross-registry string ordering.</summary>
    [Fact]
    public async Task PackageDirectiveAndVersionPoliciesPreserveBaseline()
    {
        using LocalPackageFixture files = new();
        await using LocalHttpFixture registry = await LocalHttpFixture.StartAsync();
        string root = registry.Address.AbsoluteUri.TrimEnd('/');
        List<LocalHttpFixture.ExpectedRequest> routes = [];
        string[] latestDirectives = ["missing", "empty#", "latest@latest", "repeated##1.0.0"];
        foreach (string suffix in latestDirectives)
        {
            string directive = "example.test.policy." + suffix;
            string name = directive.Split('#', '@')[0];
            routes.Add(registry.Expect("GET", "/fhir/" + name, LocalHttpFixture.RawResponse.Text(HttpStatusCode.OK, "application/json",
                RegistryListing(name, "1.0.0", "2.0.0", "10.0.0-beta.2", "invalid-version"))));
            routes.Add(registry.Expect("GET", "/npm/" + name, LocalHttpFixture.RawResponse.Text(HttpStatusCode.OK, "application/json",
                RegistryListing(name, "1.0.0", "9.0.0-beta.1+build", "invalid-version"))));
            routes.Add(registry.Expect("GET", "/fhir/" + name + "/9.0.0-beta.1+build", ArchiveResponse(name)));
            using FhirPackageService service = PackageService(files.Configuration(root + "/fhir/") with
            {
                AdditionalNpmRegistryUrls = [root + "/npm/"],
            });
            service.Init();
            PackageReference resolved = Assert.Single(await service.InstallPackages([directive], null, null).WaitAsync(LocalHttpFixture.Deadline));
            Assert.Equal(name, resolved.Name);
            Assert.Equal("9.0.0-beta.1+build", resolved.Version);
            Assert.Null(resolved.Scope);
        }
        // Within one registry, semantic ordering chooses 10 over 2 and includes prerelease/build metadata.
        const string SemanticName = "example.test.policy.semantic";
        routes.Add(registry.Expect("GET", "/fhir/" + SemanticName, LocalHttpFixture.RawResponse.Text(HttpStatusCode.OK, "application/json",
            RegistryListing(SemanticName, "1.0.0", "2.0.0", "10.0.0-beta.2+meta", "invalid-version"))));
        routes.Add(registry.Expect("GET", "/fhir/" + SemanticName + "/10.0.0-beta.2+meta", ArchiveResponse(SemanticName)));
        using (FhirPackageService service = PackageService(files.Configuration(root + "/fhir")))
        {
            service.Init();
            Assert.Equal("10.0.0-beta.2+meta", Assert.Single(await service.InstallPackages([SemanticName], [], null)
                .WaitAsync(LocalHttpFixture.Deadline)).Version);
        }

        (string input, string expectedVersion)[] passthrough =
        [
            ("hash#1.0.0", "1.0.0"), ("at@1.0.0", "1.0.0"), ("repeat@1.0.0@discarded", "1.0.0"),
            ("range#1.x", "1.x"), ("tilde@~1.0.0", "~1.0.0"),
            ("prerelease#1.2.3-beta.1", "1.2.3-beta.1"), ("build@1.2.3+meta", "1.2.3+meta"),
        ];
        foreach ((string input, string expectedVersion) in passthrough)
        {
            string directive = "example.test.policy." + input;
            string name = directive.Split('#', '@')[0];
            routes.Add(registry.Expect("GET", "/fhir/" + name + "/" + expectedVersion, ArchiveResponse(name)));
            using FhirPackageService service = PackageService(files.Configuration(root + "/fhir"));
            service.Init();
            PackageReference reference = Assert.Single(await service.InstallPackages([directive], [], null).WaitAsync(LocalHttpFixture.Deadline));
            Assert.Equal(name, reference.Name);
            Assert.Equal(expectedVersion, reference.Version);
        }
        // An existing @ prevents # normalization. The # remains in Name and becomes a URI fragment.
        const string ScopedName = "example.test.policy.scoped";
        routes.Add(registry.Expect("GET", "/fhir/" + ScopedName,
            LocalHttpFixture.RawResponse.Text(HttpStatusCode.OK, "application/json", RegistryListing(ScopedName, "invalid-version"))));
        using (FhirPackageService service = PackageService(files.Configuration(root + "/fhir")))
        {
            service.Init();
            string directive = "@scope/" + ScopedName + "#1.0.0";
            Exception failure = await Assert.ThrowsAsync<Exception>(() => service.InstallPackages([directive], null, null).WaitAsync(LocalHttpFixture.Deadline));
            Assert.Equal($"Failed to resolve latest version of {ScopedName}#1.0.0 ({directive})", failure.Message);
            await Assert.ThrowsAsync<IndexOutOfRangeException>(() => service.InstallPackages(["@scope"], null, null));
        }
        await AssertRoutes(registry, routes);
    }

    /// <summary>Configured FHIR clients precede NPM clients, with archive-only first-success fallback.</summary>
    [Fact]
    public async Task RegistryProtocolAndFallbackPreserveBaseline()
    {
        using LocalPackageFixture files = new();
        await using LocalHttpFixture registry = await LocalHttpFixture.StartAsync();
        string root = registry.Address.AbsoluteUri.TrimEnd('/');
        List<LocalHttpFixture.ExpectedRequest> routes = [];
        const string Name = "example.test.protocol";
        routes.Add(registry.Expect("GET", "/first/" + Name + "/1.0.0", LocalHttpFixture.RawResponse.Empty(HttpStatusCode.NotFound)));
        routes.Add(registry.Expect("GET", "/second/" + Name + "/1.0.0",
            new LocalHttpFixture.RawResponse(HttpStatusCode.OK, "application/octet-stream", Encoding.UTF8.GetBytes("invalid gzip"))));
        routes.Add(registry.Expect("GET", "/npm/" + Name + "/-/" + Name + "-1.0.0.tgz", ArchiveResponse(Name)));
        using (FhirPackageService service = PackageService(files.Configuration(root + "/first///", root + "/second/") with
        {
            AdditionalNpmRegistryUrls = [root + "/npm///", root + "/unused"],
        }))
        {
            service.Init();
            PackageReference reference = Assert.Single(await service.InstallPackages([Name + "#1.0.0"], [], null).WaitAsync(LocalHttpFixture.Deadline));
            Assert.Equal(Name + "@1.0.0", reference.Moniker);
            PackageManifest manifest = Assert.IsType<PackageManifest>(await new DiskPackageCache(files.CacheDirectory).ReadManifestEx(reference));
            Assert.Equal("1.0.0", manifest.Dependencies!["example.test.do-not-restore"]);
        }
        const string ScopedName = "example.test.scoped-npm";
        routes.Add(registry.Expect("GET", "/npm/@scope/" + ScopedName + "/-/" + ScopedName + "-1.0.0.tgz", ArchiveResponse(ScopedName)));
        using (FhirPackageService service = PackageService(files.Configuration() with { AdditionalNpmRegistryUrls = [root + "/npm/"] }))
        {
            service.Init();
            PackageReference reference = Assert.Single(await service.InstallPackages(["@scope/" + ScopedName + "@1.0.0"], null, null)
                .WaitAsync(LocalHttpFixture.Deadline));
            Assert.Equal("scope", reference.Scope);
            Assert.Equal(ScopedName, reference.Name);
            Assert.Equal(ScopedName + "@1.0.0", reference.Moniker);
        }
        const string ScopedFhirName = "example.test.scoped-fhir";
        routes.Add(registry.Expect("GET", "/first/" + ScopedFhirName + "/1.0.0", ArchiveResponse(ScopedFhirName)));
        using (FhirPackageService service = PackageService(files.Configuration(root + "/first/")))
        {
            service.Init();
            PackageReference reference = Assert.Single(await service.InstallPackages(["@scope/" + ScopedFhirName + "@1.0.0"], null, null)
                .WaitAsync(LocalHttpFixture.Deadline));
            Assert.Equal("scope", reference.Scope);
            Assert.Equal(ScopedFhirName, reference.Name);
        }
        const string MissingName = "example.test.protocol.missing";
        routes.Add(registry.Expect("GET", "/first/" + MissingName + "/1.x", LocalHttpFixture.RawResponse.Empty(HttpStatusCode.InternalServerError)));
        routes.Add(registry.Expect("GET", "/npm/" + MissingName + "/-/" + MissingName + "-1.x.tgz", LocalHttpFixture.RawResponse.Empty(HttpStatusCode.NotFound)));
        using (FhirPackageService service = PackageService(files.Configuration(root + "/first") with { AdditionalNpmRegistryUrls = [root + "/npm"] }))
        {
            service.Init();
            Exception failure = await Assert.ThrowsAsync<Exception>(() => service.InstallPackages([MissingName + "@1.x"], null, null).WaitAsync(LocalHttpFixture.Deadline));
            Assert.Equal($"Failed to install package {MissingName}@1.x as requested by {MissingName}@1.x", failure.Message);
        }
        await AssertRoutes(registry, routes);
    }

    /// <summary>Direct configuration, disabled state, and local-build quirks use only owned roots and loopback acquisition.</summary>
    [Fact]
    public async Task LocalBuildAndDirectCacheConfigurationPreserveBaseline()
    {
        using LocalPackageFixture files = new();
        await using LocalHttpFixture registry = await LocalHttpFixture.StartAsync();
        List<LocalHttpFixture.ExpectedRequest> routes = [];
        using (FhirPackageService service = PackageService(files.Configuration(registry.Address.AbsoluteUri.TrimEnd('/'))))
        {
            Assert.False(service.IsConfigured);
            Assert.False(service.IsReady);
            await ((IHostedService)service).StartAsync(CancellationToken.None);
            Assert.False(service.IsConfigured);
            Assert.False(service.IsReady);
            service.Init();
            service.Init();
            Assert.True(service.IsConfigured);
            Assert.True(service.IsReady);
            foreach (string version in new[] { "dev", "dev$feature" })
            {
                PackageReference reference = new("example.test.local", version);
                WritePinnedCache(files.CacheDirectory, reference, LocalPackageFixture.Manifest(_versions[0]), new Dictionary<string, string>());
                // The local branch checks existence but leaves needsInstall true: installed dev still downloads.
                routes.Add(registry.Expect("GET", "/example.test.local/" + version, new LocalHttpFixture.RawResponse(
                    HttpStatusCode.OK, "application/octet-stream",
                    LocalPackageFixture.Archive(LocalPackageFixture.Manifest(_versions[0]), new Dictionary<string, string>()))));
                Assert.Equal(reference, Assert.Single(await service.InstallPackages([reference.Moniker], null, null).WaitAsync(LocalHttpFixture.Deadline)));
                Assert.Equal(LegacyContentPath(files.CacheDirectory, reference), service.GetPackageContentDirectory(reference));
                Assert.Equal(new[] { _versions[0] }, await service.InstalledPackageFhirVersions(reference));
                string missing = "example.test.local.missing@" + version;
                Exception failure = await Assert.ThrowsAsync<Exception>(() => service.InstallPackages([missing], [], null).WaitAsync(LocalHttpFixture.Deadline));
                Assert.Equal($"Local build of example.test.local.missing is not installed ({missing})", failure.Message);
                using FhirPackageService offlineLocal = PackageService(files.Configuration());
                offlineLocal.Init();
                Exception offlineFailure = await Assert.ThrowsAsync<Exception>(() => offlineLocal.InstallPackages([reference.Moniker], [], null));
                Assert.Equal($"Failed to install package {reference.Moniker} as requested by {reference.Moniker}", offlineFailure.Message);
            }
            PackageReference absentManifest = new("example.test.local.no-manifest", "1.0.0");
            Directory.CreateDirectory(LegacyContentPath(files.CacheDirectory, absentManifest));
            Assert.Equal(absentManifest, Assert.Single(await service.InstallPackages([absentManifest.Moniker], [], null)));
            Exception manifestFailure = await Assert.ThrowsAsync<Exception>(() => service.InstalledPackageFhirVersions(absentManifest));
            Assert.Equal("Failed to load package manifest", manifestFailure.Message);
            PackageReference corruptManifest = new("example.test.local.corrupt", "1.0.0");
            WritePinnedCache(files.CacheDirectory, corruptManifest, "{invalid-json", new Dictionary<string, string>());
            Assert.Equal(corruptManifest, Assert.Single(await service.InstallPackages([corruptManifest.Moniker], [], null)));
            await Assert.ThrowsAsync<Newtonsoft.Json.JsonReaderException>(() => service.InstalledPackageFhirVersions(corruptManifest));
            using FhirPackageService filtered = PackageService(files.Configuration());
            filtered.Init();
            Exception filteredFailure = await Assert.ThrowsAsync<Exception>(() => filtered.InstallPackages([absentManifest.Moniker], [], [_versions[0]]));
            Assert.Equal("Failed to load package manifest", filteredFailure.Message);
        }
        await AssertRoutes(registry, routes);
        using (FhirPackageService disabled = PackageService(files.Configuration() with { FhirCacheDirectory = string.Empty }))
        {
            disabled.Init();
            await ((IHostedService)disabled).StartAsync(CancellationToken.None);
            Assert.False(disabled.IsConfigured);
            Assert.False(disabled.IsReady);
            Assert.Empty(await disabled.InstallPackages(["example.test.disabled@1.0.0"], [], null));
            Assert.Empty(await disabled.InstallPackages(null, null, null));
            Assert.Null(disabled.GetPackageContentDirectory(new("name", "1.0.0")));
            Assert.Null(await disabled.InstalledPackageFhirVersions(new("name", "1.0.0")));
            disabled.DeletePackage("name#1.0.0");
        }
        string homeVariable = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "UserProfile" : "HOME";
        string? originalHome = Environment.GetEnvironmentVariable(homeVariable);
        try
        {
            string ownedHome = files.CreateDirectory("home");
            Environment.SetEnvironmentVariable(homeVariable, ownedHome);
            CandleConfig fallbackConfig = files.Configuration() with { FhirCacheDirectory = null };
            using (FhirPackageService fallback = PackageService(fallbackConfig))
            {
                fallback.Init();
                string expectedRoot = Path.Combine(ownedHome, ".fhir", "packages");
                Assert.Equal(expectedRoot, fallbackConfig.FhirCacheDirectory);
                Assert.True(fallback.IsConfigured);
                Assert.True(fallback.IsReady);
                Assert.True(Directory.Exists(Path.Combine(expectedRoot, "packages")));
                Assert.Equal(Path.Combine(expectedRoot, "name#1.0.0", "package"), fallback.GetPackageContentDirectory(new("name", "1.0.0")));
            }
            string explicitRoot = Path.Combine(files.Root, "new-explicit-root");
            using (FhirPackageService explicitService = PackageService(files.Configuration() with { FhirCacheDirectory = explicitRoot }))
            {
                explicitService.Init();
                Assert.True(Directory.Exists(Path.Combine(explicitRoot, "packages")));
                Assert.Equal(Path.Combine(explicitRoot, "name#1.0.0", "package"), explicitService.GetPackageContentDirectory(new("name", "1.0.0")));
            }
            string relativeRoot = Path.GetRelativePath(Environment.CurrentDirectory, files.CreateDirectory("relative-root"));
            using (FhirPackageService relative = PackageService(files.Configuration() with { FhirCacheDirectory = relativeRoot }))
            {
                relative.Init();
                Assert.True(relative.IsConfigured);
                Assert.True(relative.IsReady);
                Assert.Equal(Path.Combine(relativeRoot, "name#1.0.0", "package"), relative.GetPackageContentDirectory(new("name", "1.0.0")));
            }
            Environment.SetEnvironmentVariable(homeVariable, null);
            using FhirPackageService noHome = PackageService(files.Configuration() with { FhirCacheDirectory = null });
            Exception homeFailure = Assert.Throws<Exception>(noHome.Init);
            Assert.Equal("Cannot determine rootpath of operating system", homeFailure.Message);
            Assert.False(noHome.IsReady);
            Assert.False(noHome.IsConfigured);
        }
        finally
        {
            Environment.SetEnvironmentVariable(homeVariable, originalHome);
        }
    }

    /// <summary>Independent legacy trees remain readable at exactly the configured root, including optional copied-cache replay.</summary>
    [Fact]
    public async Task LegacyCacheIsReadableWithoutRegistries()
    {
        string? replayRoot = Environment.GetEnvironmentVariable("FHIR_CANDLE_PACKAGE_CACHE_READBACK_ROOT");
        if (!string.IsNullOrEmpty(replayRoot))
        {
            Assert.Contains(Environment.Version.Major, new[] { 10, 11 });
            string runtimeRoot = Path.Combine(replayRoot, $"net{Environment.Version.Major}.0");
            Assert.True(Directory.Exists(runtimeRoot), $"Read-only replay input does not exist: {runtimeRoot}");
            _output.WriteLine($"Original-supplier read-only copied-cache replay: {runtimeRoot}");
            // Never seed or delete a supplied copy, and never use a public CI lookup to read it.
            await AssertPinnedLegacyCache(runtimeRoot, seed: false);
            return;
        }
        using LocalPackageFixture files = new();
        string plain = files.CreateDirectory("legacy-plain");
        string outer = files.CreateDirectory("legacy-outer");
        string nested = Path.Combine(outer, "packages");
        Directory.CreateDirectory(nested);
        foreach (string root in new[] { plain, outer, nested })
        {
            await AssertPinnedLegacyCache(root, seed: true);
        }
        PackageReference branch = new(FhirCiClient.FhirCiScope, "example.test.sdk6.r5.ci", "current$feature");
        WritePinnedCache(outer, branch, CiManifest(_versions[2], branch.Name!, replacement: true), new Dictionary<string, string>());
        DiskPackageCache cache = new(outer);
        Assert.True(await cache.IsInstalled(branch));
        Assert.Equal(Path.Combine(outer, branch.Name + "#current$feature", "package"), cache.PackageContentFolder(branch));
        Assert.Equal(branch.Name, Assert.IsType<PackageManifest>(await cache.ReadManifestEx(branch)).Name);
        Assert.False(await new DiskPackageCache(nested).IsInstalled(branch));
    }

    /// <summary>Local raw manifests retain precedence, permissive fields, author forms, and compact local-time dates.</summary>
    [Fact]
    public async Task ManifestVersionFallbackPreservesBaseline()
    {
        using LocalPackageFixture files = new();
        DiskPackageCache cache = new(files.CacheDirectory);
        (string fields, string[] expected)[] cases =
        [
            ("""
                "fhirVersions":["5.0.0"],"fhir-version-list":["4.3.0"],"dependencies":{"hl7.fhir.r4.core":"4.0.1"}
                """, ["5.0.0"]),
            ("""
                "fhirVersions":[],"fhir-version-list":["4.3.0"],"dependencies":{"hl7.fhir.r4.core":"4.0.1"}
                """, ["4.3.0"]),
            ("""
                "fhirVersions":[],"fhir-version-list":[],"dependencies":{"hl7.fhir.r4.core":"4.0.1","other":"9.9.9","hl7.fhir.r5.core":"5.0.0"}
                """, ["4.0.1", "5.0.0"]),
            ("""
                "dependencies":{"hl7.fhir.r4.core":"","hl7.fhir.r4b.core":null,"HL7.fhir.r5.core":"5.0.0","hl7.fhir.r5.examples":"5.0.0"}
                """, ["HL7.FHIR.R4.CORE", "HL7.FHIR.R4B.CORE"]),
            ("""
                "fhirVersions":[],"fhir-version-list":[],"dependencies":{}
                """, []),
            ("""
                "fhirVersions":null,"fhir-version-list":null,"dependencies":null
                """, []),
        ];
        for (int index = 0; index < cases.Length; index++)
        {
            foreach (bool stringAuthor in new[] { false, true })
            {
                PackageReference reference = new("example.test.manifest." + index + "." + stringAuthor, "1.0.0");
                string author = stringAuthor
                    ? JsonValue.Create("Pinned Author <pinned@example.test> (https://example.test/pinned)")!.ToJsonString()
                    : """{"name":"Pinned Author","email":"pinned@example.test","url":"https://example.test/pinned"}""";
                string manifest = $$$"""{"name":"{{{reference.Name}}}","version":"1.0.0",{{{cases[index].fields}}},"author":{{{author}}},"date":"20240203123456","unknown-field":{"retained":"raw fixture"}}""";
                WritePinnedCache(files.CacheDirectory, reference, manifest, new Dictionary<string, string>());
                PackageManifest parsed = Assert.IsType<PackageManifest>(await cache.ReadManifestEx(reference));
                Assert.Equal(cases[index].expected, parsed.AnyFhirVersions);
                AssertAuthor(parsed, "Pinned Author", "pinned@example.test", "https://example.test/pinned");
                FieldInfo origin = typeof(fhir.candle._ForPackages.AuthorInfo).GetField("ParsedFromString", BindingFlags.Instance | BindingFlags.NonPublic)!;
                Assert.Equal(stringAuthor, origin.GetValue(parsed.AuthorInformation));
                DateTime local = new(2024, 2, 3, 12, 34, 56, DateTimeKind.Unspecified);
                Assert.Equal(new DateTimeOffset(local, TimeZoneInfo.Local.GetUtcOffset(local)), parsed.Date);
                Assert.Equal(Encoding.UTF8.GetBytes(manifest), File.ReadAllBytes(Path.Combine(LegacyContentPath(files.CacheDirectory, reference), "package.json")));
            }
        }
        PackageReference missing = new("example.test.manifest.missing", "1.0.0");
        Assert.False(await cache.IsInstalled(missing));
        Assert.Null(await cache.ReadManifestEx(missing));
        Directory.CreateDirectory(LegacyContentPath(files.CacheDirectory, missing));
        Assert.True(await cache.IsInstalled(missing));
        Assert.Null(await cache.ReadManifestEx(missing));
        WritePinnedCache(files.CacheDirectory, missing, "null", new Dictionary<string, string>());
        Assert.Null(await cache.ReadManifestEx(missing));
    }

    /// <summary>Bounded CI transport exercises feature branches, catalog lookup, and private service literal precedence.</summary>
    [Fact]
    public async Task CiBranchAndLiteralLookupPreserveBaseline()
    {
        using LocalPackageFixture files = new();
        await using LocalHttpFixture listener = await LocalHttpFixture.StartAsync();
        const string Name = "example.test.sdk6.branch";
        const string QaUri = "https://build.fhir.org/ig/qas.json";
        const string BranchesUri = "https://build.fhir.org/branches/";
        const string ArchivePath = "/ig/example/sdk6/branches/feature/package.tgz";
        string[] sequence = [QaUri, BranchesUri, "https://build.fhir.org" + ArchivePath];
        using LocalHttpFixture.MappedLoopbackHandler mapping = listener.MapCiRequests(sequence);
        using HttpClient http = new(mapping) { Timeout = LocalHttpFixture.Deadline };
        using FhirCiClient client = new(client: http);
        JsonArray qas = JsonNode.Parse(QaListing(_versions[2], Name, false))!.AsArray();
        qas[0]!["repo"] = "example/sdk6/branches/feature/qa.json";
        qas[0]!["url"] = "https://example.test/branch";
        // Both repo and package-name match this literal; repo must win in ResolveCiLiterals.
        JsonObject competing = qas[0]!.DeepClone().AsObject();
        competing["package-id"] = "example/sdk6";
        competing["repo"] = "example/other/branches/main/qa.json";
        competing["url"] = "https://example.test/other";
        qas.Add(competing);
        byte[] archive = LocalPackageFixture.Archive(ProtocolManifest(Name), new Dictionary<string, string>());
        List<LocalHttpFixture.ExpectedRequest> routes =
        [
            listener.Expect("GET", "/ig/qas.json", LocalHttpFixture.RawResponse.Text(HttpStatusCode.OK, "application/json", qas.ToJsonString())),
            listener.Expect("GET", "/branches/", LocalHttpFixture.RawResponse.Text(HttpStatusCode.OK, "application/json", "[]")),
            listener.Expect("GET", ArchivePath, new LocalHttpFixture.RawResponse(HttpStatusCode.OK, "application/octet-stream", archive)),
        ];
        PackageListing listing = Assert.IsType<PackageListing>(await client.DownloadListingAsync(Name).AsTask().WaitAsync(LocalHttpFixture.Deadline));
        Assert.Equal("1.0.0-cibuild+20240101-000000Z", Assert.Single(listing.DistTags!).Value);
        Assert.Equal("current$feature", Assert.Single(listing.DistTags!).Key);
        Assert.False(listing.DistTags!.ContainsKey("current"));
        foreach (string discriminator in new[] { "feature", "current$feature", "1.0.0-cibuild+20240101-000000Z" })
        {
            (PackageReference tagged, PackageReference resolved) = await client.GetReferences(Name, discriminator).AsTask().WaitAsync(LocalHttpFixture.Deadline);
            Assert.Equal(FhirCiClient.FhirCiScope, tagged.Scope);
            Assert.Equal(FhirCiClient.FhirCiScope, resolved.Scope);
            Assert.Equal(Name, tagged.Name);
            Assert.Equal(Name, resolved.Name);
            Assert.Equal("current$feature", tagged.Version);
            Assert.Equal("1.0.0-cibuild+20240101-000000Z", resolved.Version);
            Assert.NotEqual(tagged, resolved);
        }
        Assert.Equal(PackageReference.None, await client.GetTagBasedReference(Name, "current"));
        AssertCatalog(Assert.Single(await client.CatalogPackagesAsync(repo: "https://github.com/example/sdk6", branch: "feature")), Name);
        AssertCatalog(Assert.Single(await client.CatalogPackagesAsync(site: "https://example.test/branch/")), Name);
        AssertCatalog(Assert.Single(await client.CatalogPackagesAsync(pkgname: Name, fhirversion: "5.0.0")), Name);
        Assert.Empty(await client.CatalogPackagesAsync(pkgname: Name, branch: "other"));
        Assert.Empty(await client.CatalogPackagesAsync(pkgname: Name, fhirversion: "4.0.1"));
        Assert.Equal(new[] { "1.0.0-cibuild+20240101-000000Z" }, (await client.GetVersions(Name))!.Items.Select(item => item.ToString()));
        Assert.Equal(("feature", false), FhirCiClient.GetBranchNameRepoLiteral("example/sdk6/branches/feature/qa.json"));
        Assert.Equal(("main", true), FhirCiClient.GetBranchNameRepoLiteral("example/sdk6/branches/main/qa.json"));
        PackageReference download = await client.GetResolvedReference(Name, "feature");
        Assert.Equal(archive, await client.GetPackage(download).WaitAsync(LocalHttpFixture.Deadline));

        using FhirPackageService service = PackageService(files.Configuration());
        FieldInfo ciField = typeof(FhirPackageService).GetField("_ciClient", BindingFlags.Instance | BindingFlags.NonPublic)!;
        MethodInfo resolve = typeof(FhirPackageService).GetMethod("ResolveCiLiterals", BindingFlags.Instance | BindingFlags.NonPublic)!;
        FhirCiClient original = Assert.IsType<FhirCiClient>(ciField.GetValue(service));
        try
        {
            ciField.SetValue(service, client);
            string[] literals = ["example/sdk6", "https://example.test/branch/", Name, "example/sdk6#current", Name + "#current$feature", "missing"];
            Task<List<string>> resolution = Assert.IsType<Task<List<string>>>(resolve.Invoke(service, [literals]));
            Assert.Equal(new[] { Name + "#current$feature", Name + "#current$feature", Name + "#current$feature",
                "example/sdk6#current", Name + "#current$feature" }, await resolution.WaitAsync(LocalHttpFixture.Deadline));
            Assert.Empty(await Assert.IsType<Task<List<string>>>(resolve.Invoke(service, [null])).WaitAsync(LocalHttpFixture.Deadline));
        }
        finally
        {
            ciField.SetValue(service, original);
            ((IDisposable)original).Dispose();
        }
        Assert.Equal(sequence, mapping.Forwarded);
        mapping.AssertComplete();
        await AssertRoutes(listener, routes);
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

    /// <summary>Uses the real CI entry point and companion cache, including both authored author representations.</summary>
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
        Assert.Equal(Encoding.UTF8.GetBytes(coldManifest), File.ReadAllBytes(Path.Combine(contentDirectory, "package.json")));
        Assert.Equal(Encoding.UTF8.GetBytes(OriginalResource), File.ReadAllBytes(Path.Combine(contentDirectory, "Patient-ci-original.json")));

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
        Assert.Equal(Encoding.UTF8.GetBytes(replacementManifest), File.ReadAllBytes(Path.Combine(contentDirectory, "package.json")));
        Assert.Equal(Encoding.UTF8.GetBytes(ReplacementResource), File.ReadAllBytes(Path.Combine(contentDirectory, "Patient-ci-replacement.json")));

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

    /// <summary>Native imports preserve raw distribution bytes, requested paths, neighbors, and operation-local cleanup.</summary>
    [Fact]
    public async Task NativeImportPreservesLegacyPublicationAndCleanup()
    {
        string? exportRoot = Environment.GetEnvironmentVariable("FHIR_CANDLE_PACKAGE_CACHE_EXPORT_ROOT");
        if (!string.IsNullOrEmpty(exportRoot))
        {
            Assert.True(Path.IsPathFullyQualified(exportRoot), "Export requires a verifier-owned absolute temporary root.");
            Assert.Contains(Environment.Version.Major, new[] { 10, 11 });
            string runtimeRoot = Path.Combine(exportRoot, $"net{Environment.Version.Major}.0");
            Assert.False(Directory.Exists(runtimeRoot) || File.Exists(runtimeRoot), $"Refusing to overwrite export: {runtimeRoot}");
            Directory.CreateDirectory(runtimeRoot);
        }
        foreach (FhirReleases.FhirSequenceCodes version in _versions)
        {
            using LocalPackageFixture files = new();
            DiskPackageCache cache = new(files.CacheDirectory);
            string name = LocalPackageFixture.PackageName(version);
            PackageReference ordinary = new(name, "1.0.0");
            PackageReference neighbor = new(name + ".neighbor", "2.0.0");
            HashSet<string> persistentEntries = [name + "#1.0.0", neighbor.Name + "#" + neighbor.Version];
            WritePinnedCache(files.CacheDirectory, neighbor, ProtocolManifest(neighbor.Name!), new Dictionary<string, string>());
            string neighborPath = LegacyContentPath(files.CacheDirectory, neighbor);
            byte[] neighborBytes = File.ReadAllBytes(Path.Combine(neighborPath, "package.json"));
            string parent = Path.GetDirectoryName(LegacyContentPath(files.CacheDirectory, ordinary))!;
            Directory.CreateDirectory(parent);
            byte[] metadata = Encoding.UTF8.GetBytes("Pinned parent metadata: café");
            File.WriteAllBytes(Path.Combine(parent, "neighbor-metadata.txt"), metadata);

            Dictionary<string, byte[]> distribution = new(StringComparer.Ordinal)
            {
                ["package/package.json"] = Encoding.UTF8.GetBytes(LocalPackageFixture.Manifest(version)),
                ["package/Patient-wire-patient.json"] = Encoding.UTF8.GetBytes(
                    LocalPackageFixture.WithId(CompatibilityReplay.LoadFixture(version, "patient.json"), "wire-patient")),
                ["package/Observation-package-observation.xml"] = Encoding.UTF8.GetBytes(
                    LocalPackageFixture.WithId(CompatibilityReplay.LoadFixture(version, "observation.xml"), "package-observation")),
                ["examples/Patient-example.json"] = Encoding.UTF8.GetBytes("""{"resourceType":"Patient","id":"example","name":[{"family":"café"}]}"""),
                ["siblings/nested/authored.bin"] = [0, 1, 2, 128, 255],
                ["authored-readme.txt"] = Encoding.UTF8.GetBytes("Distributed sibling: café\r\n"),
            };
            byte[] archive = DistributedArchive(distribution);
            string? originalHome = Environment.GetEnvironmentVariable("UserProfile");
            string? originalUnixHome = Environment.GetEnvironmentVariable("HOME");
            string? originalNativeRoot = Environment.GetEnvironmentVariable("PACKAGE_CACHE_FOLDER");
            string ownedHome = Path.Combine(files.Root, "unused-home");
            string nativeDefault = Path.Combine(files.Root, "unused-native-cache");
            try
            {
                Environment.SetEnvironmentVariable("UserProfile", ownedHome);
                Environment.SetEnvironmentVariable("HOME", ownedHome);
                Environment.SetEnvironmentVariable("PACKAGE_CACHE_FOLDER", nativeDefault);
                await cache.Install(ordinary, archive).WaitAsync(LocalHttpFixture.Deadline);
                AssertDistribution(parent, distribution);
                Assert.True(Directory.Exists(Path.Combine(parent, "empty-distributed-directory")));
                PackageManifest ordinaryManifest = Assert.IsType<PackageManifest>(await cache.ReadManifestEx(ordinary));
                AssertAuthor(ordinaryManifest, "Synthetic Package Author", "author@example.test", "https://example.test/sdk6");
                Assert.False(ordinaryManifest.AuthorInformation!.ParsedFromString);
                Assert.Equal(metadata, File.ReadAllBytes(Path.Combine(parent, "neighbor-metadata.txt")));
                Assert.Equal(neighborBytes, File.ReadAllBytes(Path.Combine(neighborPath, "package.json")));
                AssertClean();

                // Import discovers the concrete identity, but publication retains the requested scope/tag/range.
                foreach (PackageReference requested in new PackageReference[]
                {
                    new("scope", name + ".requested", "current$feature"),
                    new("scope", name + ".requested", "1.x"),
                    new("another-scope", name + ".requested", "~1.0.0"),
                })
                {
                    await cache.Install(requested, archive).WaitAsync(LocalHttpFixture.Deadline);
                    persistentEntries.Add(requested.Name + "#" + requested.Version);
                    string requestedRoot = Path.GetDirectoryName(LegacyContentPath(files.CacheDirectory, requested))!;
                    Assert.Equal(Path.Combine(files.CacheDirectory, requested.Name + "#" + requested.Version, "package"), cache.PackageContentFolder(requested));
                    AssertDistribution(requestedRoot, distribution);
                    Assert.Equal(name, Assert.IsType<PackageManifest>(await cache.ReadManifestEx(requested)).Name);
                    AssertClean();
                }

                (Dictionary<string, byte[]> files, string[] directories) beforeFailures = Snapshot(files.CacheDirectory);
                foreach (byte[] invalid in new[]
                {
                    Encoding.UTF8.GetBytes("invalid gzip"),
                    LocalPackageFixture.Archive("{invalid-json", new Dictionary<string, string>()),
                    LocalPackageFixture.Archive("""{"name":"invalid","version":null}""", new Dictionary<string, string>()),
                })
                {
                    await Assert.ThrowsAnyAsync<Exception>(() => cache.Install(ordinary, invalid).WaitAsync(LocalHttpFixture.Deadline));
                    AssertSnapshot(beforeFailures, files.CacheDirectory);
                    AssertClean();
                }
                PackageReference blocked = new(name + ".blocked", "1.0.0");
                string blockedRoot = Path.GetDirectoryName(LegacyContentPath(files.CacheDirectory, blocked))!;
                byte[] obstacle = Encoding.UTF8.GetBytes("Owned publication obstacle");
                File.WriteAllBytes(blockedRoot, obstacle);
                persistentEntries.Add(blocked.Name + "#" + blocked.Version);
                (Dictionary<string, byte[]> files, string[] directories) beforePublicationFailure = Snapshot(files.CacheDirectory);
                await Assert.ThrowsAnyAsync<IOException>(() => cache.Install(blocked, archive).WaitAsync(LocalHttpFixture.Deadline));
                AssertSnapshot(beforePublicationFailure, files.CacheDirectory);
                Assert.Equal(obstacle, File.ReadAllBytes(blockedRoot));
                AssertClean();

                // Deletion affects only content. Reinstallation retains parent metadata and neighbors.
                await cache.Delete(ordinary);
                Assert.False(await cache.IsInstalled(ordinary));
                Assert.Equal(metadata, File.ReadAllBytes(Path.Combine(parent, "neighbor-metadata.txt")));
                Assert.Equal(distribution["examples/Patient-example.json"], File.ReadAllBytes(Path.Combine(parent, "examples", "Patient-example.json")));
                Assert.Equal(neighborBytes, File.ReadAllBytes(Path.Combine(neighborPath, "package.json")));
                await cache.Install(ordinary, archive).WaitAsync(LocalHttpFixture.Deadline);
                AssertDistribution(parent, distribution);
                AssertClean();

                PackageReference ci = new(FhirCiClient.FhirCiScope, name + ".ci", "current");
                string finalCiManifest = CiManifest(version, ci.Name!, replacement: true);
                Dictionary<string, string> finalCiResources = new()
                {
                    ["Patient-ci-replacement.json"] = """{"resourceType":"Patient","id":"ci-replacement","active":false,"name":[{"family":"Replacement synthetic CI"}]}""",
                };
                await cache.Install(ci, LocalPackageFixture.Archive(finalCiManifest, finalCiResources)).WaitAsync(LocalHttpFixture.Deadline);
                persistentEntries.Add(ci.Name + "#" + ci.Version);
                Assert.Equal(Encoding.UTF8.GetBytes(finalCiManifest), File.ReadAllBytes(Path.Combine(cache.PackageContentFolder(ci), "package.json")));
                Assert.Equal(Encoding.UTF8.GetBytes(finalCiResources.Single().Value),
                    File.ReadAllBytes(Path.Combine(cache.PackageContentFolder(ci), finalCiResources.Single().Key)));
                PackageManifest ciManifest = Assert.IsType<PackageManifest>(await cache.ReadManifestEx(ci));
                AssertCiManifest(ciManifest, version, ci.Name!, replacement: true);
                Assert.True(ciManifest.AuthorInformation!.ParsedFromString);
                Assert.Equal(new[] { "Patient-ci-replacement.json", "package.json" },
                    Directory.GetFiles(Path.GetDirectoryName(cache.PackageContentFolder(ci))!, "*", SearchOption.AllDirectories)
                        .Select(Path.GetFileName).Order(StringComparer.Ordinal));
                AssertClean();

                if (!string.IsNullOrEmpty(exportRoot))
                {
                    ExportTree(parent, ordinary);
                    ExportTree(Path.GetDirectoryName(cache.PackageContentFolder(ci))!, ci);
                }
                _output.WriteLine($"Native import / net{Environment.Version.Major}.0 / {version}: exact UTF8/XML/binary bytes, full tree, requested identities, targeted delete/reinstall, import/publication cleanup.");
            }
            finally
            {
                Environment.SetEnvironmentVariable("UserProfile", originalHome);
                Environment.SetEnvironmentVariable("HOME", originalUnixHome);
                Environment.SetEnvironmentVariable("PACKAGE_CACHE_FOLDER", originalNativeRoot);
            }

            void AssertClean()
            {
                Assert.Empty(Directory.GetDirectories(files.CacheDirectory, ".candle-package-import-*"));
                Assert.False(Directory.Exists(ownedHome));
                Assert.False(Directory.Exists(nativeDefault));
                // Every persistent root entry is independently fixture-authored.
                Assert.Equal(persistentEntries.Order(), Directory.GetFileSystemEntries(files.CacheDirectory)
                    .Select(Path.GetFileName).Order());
            }

            void ExportTree(string source, PackageReference reference)
            {
                string destination = Path.Combine(exportRoot!, $"net{Environment.Version.Major}.0", reference.Name + "#" + reference.Version);
                Assert.False(Directory.Exists(destination) || File.Exists(destination), $"Refusing to overwrite export package: {destination}");
                CopyExportTree(source, destination);
                AssertSnapshot(Snapshot(source), destination);
                _output.WriteLine($"Verifier-owned replacement cache export: {destination}");
            }
        }

        static void AssertDistribution(string root, IReadOnlyDictionary<string, byte[]> expected)
        {
            foreach (KeyValuePair<string, byte[]> file in expected)
            {
                Assert.Equal(file.Value, File.ReadAllBytes(Path.Combine(root, file.Key)));
            }
            string[] authored = expected.Keys.Append("neighbor-metadata.txt")
                .Where(path => File.Exists(Path.Combine(root, path))).Order().ToArray();
            Assert.Equal(authored, Directory.GetFiles(root, "*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/')).Order());
            HashSet<string> directories = ["empty-distributed-directory"];
            foreach (string file in expected.Keys)
            {
                string? directory = Path.GetDirectoryName(file);
                while (!string.IsNullOrEmpty(directory))
                {
                    directories.Add(directory.Replace('\\', '/'));
                    directory = Path.GetDirectoryName(directory);
                }
            }
            Assert.Equal(directories.Order(), Directory.GetDirectories(root, "*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/')).Order());
        }
    }

    private static (Dictionary<string, byte[]> files, string[] directories) Snapshot(string root) =>
        (Directory.GetFiles(root, "*", SearchOption.AllDirectories)
            .ToDictionary(path => Path.GetRelativePath(root, path), File.ReadAllBytes, StringComparer.Ordinal),
        Directory.GetDirectories(root, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(root, path)).Order().ToArray());

    private static void AssertSnapshot((Dictionary<string, byte[]> files, string[] directories) expected, string root)
    {
        (Dictionary<string, byte[]> files, string[] directories) actual = Snapshot(root);
        Assert.Equal(expected.directories, actual.directories);
        Assert.Equal(expected.files.Keys.Order(), actual.files.Keys.Order());
        foreach (KeyValuePair<string, byte[]> file in expected.files)
        {
            Assert.Equal(file.Value, actual.files[file.Key]);
        }
    }

    private static byte[] DistributedArchive(IReadOnlyDictionary<string, byte[]> files)
    {
        using MemoryStream output = new();
        using (GZipStream gzip = new(output, CompressionLevel.SmallestSize, leaveOpen: true))
        using (TarWriter tar = new(gzip, TarEntryFormat.Ustar, leaveOpen: true))
        {
            tar.WriteEntry(new UstarTarEntry(TarEntryType.Directory, "empty-distributed-directory/"));
            foreach (KeyValuePair<string, byte[]> file in files)
            {
                using MemoryStream content = new(file.Value, writable: false);
                tar.WriteEntry(new UstarTarEntry(TarEntryType.RegularFile, file.Key) { DataStream = content });
            }
        }
        return output.ToArray();
    }

    private static void CopyExportTree(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (string directory in Directory.GetDirectories(source))
        {
            CopyExportTree(directory, Path.Combine(destination, Path.GetFileName(directory)));
        }
        foreach (string file in Directory.GetFiles(source))
        {
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), overwrite: false);
        }
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

    private static string RegistryListing(string name, params string[] versions)
    {
        JsonObject releases = new();
        foreach (string version in versions)
        {
            releases[version] = new JsonObject { ["name"] = name, ["version"] = version };
        }
        return new JsonObject
        {
            ["_id"] = name, ["name"] = name, ["dist-tags"] = new JsonObject { ["latest"] = "1.0.0" },
            ["versions"] = releases,
        }.ToJsonString();
    }

    private static string ProtocolManifest(string name) =>
        $$$"""{"name":"{{{name}}}","version":"1.0.0","fhirVersions":["5.0.0"],"dependencies":{"example.test.do-not-restore":"1.0.0"}}""";

    private static LocalHttpFixture.RawResponse ArchiveResponse(string name) =>
        new(HttpStatusCode.OK, "application/octet-stream", LocalPackageFixture.Archive(ProtocolManifest(name), new Dictionary<string, string>()));

    private async Task AssertRoutes(LocalHttpFixture fixture, IEnumerable<LocalHttpFixture.ExpectedRequest> routes)
    {
        LocalHttpFixture.ExpectedRequest[] expected = routes.ToArray();
        Assert.Equal(expected.Select(route => route.Path), fixture.Requests.Select(request => request.Path));
        foreach (LocalHttpFixture.ExpectedRequest route in expected)
        {
            AssertRegistryRequest(await route.WaitForRequestAsync(), fixture, route.Path);
            Assert.False(await route.WaitForCompletionAsync());
        }
        fixture.AssertComplete();
    }

    private static void AssertCatalog(PackageCatalogEntry entry, string name)
    {
        Assert.Equal(name, entry.Name);
        Assert.Equal("example/sdk6/branches/feature/qa.json", entry.Description);
        Assert.Equal("5.0.0", entry.FhirVersion);
    }

    // Independent physical expectations: never ask an installer or cache adapter where to seed files.
    private static string LegacyContentPath(string root, PackageReference reference) =>
        Path.Combine(root, reference.Name + "#" + reference.Version, "package");

    private static void WritePinnedCache(string root, PackageReference reference, string manifest, IReadOnlyDictionary<string, string> resources)
    {
        string directory = LegacyContentPath(root, reference);
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, "package.json"), Encoding.UTF8.GetBytes(manifest));
        foreach (KeyValuePair<string, string> resource in resources)
        {
            File.WriteAllBytes(Path.Combine(directory, resource.Key), Encoding.UTF8.GetBytes(resource.Value));
        }
    }

    private static async Task AssertPinnedLegacyCache(string root, bool seed)
    {
        Assert.True(Directory.Exists(root));
        foreach (FhirReleases.FhirSequenceCodes version in _versions)
        {
            string name = LocalPackageFixture.PackageName(version);
            PackageReference ordinary = new(name, "1.0.0");
            PackageReference ci = new(FhirCiClient.FhirCiScope, name + ".ci", "current");
            Dictionary<string, string> ordinaryResources = new()
            {
                ["Patient-wire-patient.json"] = LocalPackageFixture.WithId(CompatibilityReplay.LoadFixture(version, "patient.json"), "wire-patient"),
                ["Observation-package-observation.xml"] = LocalPackageFixture.WithId(CompatibilityReplay.LoadFixture(version, "observation.xml"), "package-observation"),
            };
            Dictionary<string, string> ciResources = new()
            {
                ["Patient-ci-replacement.json"] = """{"resourceType":"Patient","id":"ci-replacement","active":false,"name":[{"family":"Replacement synthetic CI"}]}""",
            };
            string ordinaryManifest = LocalPackageFixture.Manifest(version);
            string ciManifest = CiManifest(version, ci.Name!, replacement: true);
            if (seed)
            {
                WritePinnedCache(root, ordinary, ordinaryManifest, ordinaryResources);
                WritePinnedCache(root, ci, ciManifest, ciResources);
                File.WriteAllBytes(Path.Combine(root, name + "#1.0.0", "neighbor-metadata.txt"), Encoding.UTF8.GetBytes("Pinned parent metadata: café"));
            }
            Assert.True(Directory.Exists(LegacyContentPath(root, ordinary)));
            Assert.True(Directory.Exists(LegacyContentPath(root, ci)));
            AssertPinnedFiles(ordinary, ordinaryManifest, ordinaryResources);
            AssertPinnedFiles(ci, ciManifest, ciResources);

            Dictionary<string, byte[]> before = Directory.GetFiles(root, "*", SearchOption.AllDirectories)
                .ToDictionary(path => Path.GetRelativePath(root, path), File.ReadAllBytes, StringComparer.Ordinal);
            CandleConfig config = new()
            {
                FhirCacheDirectory = root,
                UseOfficialRegistries = false,
                AdditionalFhirRegistryUrls = [],
                AdditionalNpmRegistryUrls = [],
                PublishedPackages = [],
                CiPackages = [],
            };
            using FhirPackageService service = PackageService(config);
            service.Init();
            Assert.True(service.IsReady);
            Assert.True(service.IsConfigured);
            Assert.Equal(ordinary, Assert.Single(await service.InstallPackages([name + "#1.0.0"], [], [version]).WaitAsync(LocalHttpFixture.Deadline)));
            Assert.Equal(LegacyContentPath(root, ordinary), service.GetPackageContentDirectory(ordinary));
            Assert.Equal(new[] { version }, await service.InstalledPackageFhirVersions(ordinary));
            DiskPackageCache cache = new(root);
            Assert.Equal(root, cache.Root);
            Assert.True(await cache.IsInstalled(ci));
            Assert.Equal(LegacyContentPath(root, ci), cache.PackageContentFolder(ci));
            Assert.Equal(cache.PackageContentFolder(ci), cache.PackageContentFolder(new PackageReference("another-scope", ci.Name!, ci.Version)));
            AssertCiManifest(Assert.IsType<PackageManifest>(await cache.ReadManifestEx(ci)), version, ci.Name!, replacement: true);
            Assert.False(await cache.IsInstalled(new(FhirCiClient.FhirCiScope, ci.Name!, "1.1.0-cibuild+20240105-000000Z")));
            Assert.Equal(before.Keys.Order(), Directory.GetFiles(root, "*", SearchOption.AllDirectories).Select(path => Path.GetRelativePath(root, path)).Order());
            foreach (KeyValuePair<string, byte[]> file in before)
            {
                Assert.Equal(file.Value, File.ReadAllBytes(Path.Combine(root, file.Key)));
            }

            void AssertPinnedFiles(PackageReference reference, string manifest, IReadOnlyDictionary<string, string> resources)
            {
                string directory = LegacyContentPath(root, reference);
                Assert.Equal(Encoding.UTF8.GetBytes(manifest), File.ReadAllBytes(Path.Combine(directory, "package.json")));
                foreach (KeyValuePair<string, string> resource in resources)
                {
                    Assert.Equal(Encoding.UTF8.GetBytes(resource.Value), File.ReadAllBytes(Path.Combine(directory, resource.Key)));
                }
            }
        }
    }

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
        Assert.Equal(Encoding.UTF8.GetBytes(LocalPackageFixture.Manifest(version)), File.ReadAllBytes(Path.Combine(directory, "package.json")));
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
        fhir.candle._ForPackages.AuthorInfo author = Assert.IsAssignableFrom<fhir.candle._ForPackages.AuthorInfo>(manifest.AuthorInformation);
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
