// <copyright file="Sdk6PackageTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using System.Net;
using System.Formats.Tar;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
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
using Microsoft.Extensions.Logging;
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
        _output.WriteLine("Package inventory: acquisition/repeat/fresh-cache/manager-first-load/corrupt/empty-cache; CI listing/GetPackage/InstallOrUpdate cold/warm/replacement/missing; six custom initial-data tenants, JSON+XML; R4/R4B/R5 variants/examples/supplement precedence and independently populated deletion/reinstall/rollback trees.");
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

    #region R1 portable H1 tests and transport helpers

    /// <summary>Unavailable leaf registries do not discard a latest winner, in either configured order.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LatestRegistryTransportFailurePreservesFallback(bool npm)
    {
        foreach (R1ListingFailure failure in new[]
        {
            R1ListingFailure.NotFound, R1ListingFailure.HttpStatus,
            R1ListingFailure.Transport, R1ListingFailure.Deadline, R1ListingFailure.Malformed,
        })
        {
            foreach (bool failureFirst in new[] { true, false })
            {
                await AssertR1LatestScenario(npm, failure, failureFirst, usableWinner: true);
                await AssertR1LatestScenario(npm, failure, failureFirst, usableWinner: false);
            }
        }
        AssertR1InvalidRegistryConfiguration(npm);
        await AssertR1NativeDeadline(npm);
    }

    private async Task AssertR1LatestScenario(
        bool npm, R1ListingFailure failure, bool failureFirst, bool usableWinner)
    {
        using LocalPackageFixture files = new();
        await using LocalHttpFixture registry = await LocalHttpFixture.StartAsync();
        string name = $"example.test.r1.{(npm ? "npm" : "fhir")}.{failure.ToString().ToLowerInvariant()}.{(failureFirst ? "before" : "after")}.{(usableWinner ? "winner" : "empty")}";
        string root = registry.Address.AbsoluteUri.TrimEnd('/');
        bool synthetic = failure is R1ListingFailure.Transport or R1ListingFailure.Deadline;
        // These are never sent to a network transport: the finite handler owns both failure cases.
        string failedRoot = synthetic
            ? "https://credential-user:credential-password@registry.example.test/failed?query-secret=hidden"
            : root + "/failed";
        string winnerRoot = root + "/winner";
        string[] endpoints = failureFirst ? [failedRoot, winnerRoot] : [winnerRoot, failedRoot];
        List<LocalHttpFixture.ExpectedRequest> routes = [];
        using R1RegistryHandler handler = new();
        using HttpClient http = new(handler) { Timeout = Timeout.InfiniteTimeSpan };
        R1PackageLogger logger = new();
        using FhirPackageService service = new(logger, files.Configuration());
        service.Init();

        foreach (string endpoint in endpoints)
        {
            bool failed = endpoint == failedRoot;
            string uri = R1ListingUri(endpoint, name);
            if (failed && synthetic)
            {
                handler.ExpectFailure(uri, failure);
            }
            else
            {
                LocalHttpFixture.RawResponse response = failed ? failure switch
                {
                    R1ListingFailure.NotFound => LocalHttpFixture.RawResponse.Empty(HttpStatusCode.NotFound),
                    R1ListingFailure.HttpStatus => LocalHttpFixture.RawResponse.Text(
                        HttpStatusCode.ServiceUnavailable, "text/plain", "response-body-secret"),
                    R1ListingFailure.Malformed => LocalHttpFixture.RawResponse.Text(
                        HttpStatusCode.OK, "application/json", "{malformed-body-secret"),
                    _ => throw new InvalidOperationException("Unscripted listing failure."),
                } : LocalHttpFixture.RawResponse.Text(HttpStatusCode.OK, "application/json",
                    RegistryListing(name, usableWinner ? "1.0.0" : "invalid-version"));
                routes.Add(registry.Expect("GET", new Uri(uri).AbsolutePath, response));
                handler.ExpectLoopback(uri);
            }
        }
        if (usableWinner)
        {
            foreach (string endpoint in endpoints)
            {
                string path = npm ? $"{name}/-/{name}-1.0.0.tgz" : $"{name}/1.0.0";
                string uri = new Uri(endpoint.TrimEnd('/') + "/" + path).AbsoluteUri;
                if (endpoint == failedRoot && synthetic)
                {
                    handler.ExpectResponse(uri, LocalHttpFixture.RawResponse.Empty(HttpStatusCode.NotFound));
                }
                else
                {
                    LocalHttpFixture.RawResponse response = endpoint == failedRoot
                        ? LocalHttpFixture.RawResponse.Empty(HttpStatusCode.NotFound) : ArchiveResponse(name);
                    routes.Add(registry.Expect("GET", new Uri(uri).AbsolutePath, response));
                    handler.ExpectLoopback(uri);
                }
                if (endpoint == winnerRoot)
                {
                    break;
                }
            }
        }

        using (R1RegistryClients clients = new(service, http, endpoints, npm))
        {
            List<PackageReference>? installedPackages = null;
            Exception? error = await Record.ExceptionAsync(async () =>
            {
                installedPackages = await service.InstallPackages([name], [], null).WaitAsync(LocalHttpFixture.Deadline);
            });
            if (!usableWinner)
            {
                Assert.Equal($"Failed to resolve latest version of {name} ({name})", Assert.IsType<Exception>(error).Message);
                Assert.Null(installedPackages);
                Assert.Empty(Directory.GetFileSystemEntries(files.CacheDirectory));
            }
            else
            {
                Assert.Null(error);
                PackageReference installed = Assert.Single(Assert.IsType<List<PackageReference>>(installedPackages));
                Assert.Equal(name, installed.Name);
                Assert.Equal("1.0.0", installed.Version);
                Assert.Null(installed.Scope);
                Assert.Equal(name + "@1.0.0", installed.Moniker);
                Assert.Equal(LegacyContentPath(files.CacheDirectory, installed), service.GetPackageContentDirectory(installed));
                Assert.Equal(Encoding.UTF8.GetBytes(ProtocolManifest(name)),
                    File.ReadAllBytes(Path.Combine(LegacyContentPath(files.CacheDirectory, installed), "package.json")));
            }
            AssertR1UnavailableWarnings(logger, name, failedRoot, failure);
            handler.AssertComplete(failure == R1ListingFailure.Deadline ? 1 : 0);
            await AssertRoutes(registry, routes);
        }
        // HttpClient owns the handler; explicit disposal lets the test observe the lifetime boundary.
        http.Dispose();
        Assert.True(handler.Disposed);
        _output.WriteLine($"R1 latest / {(npm ? "NPM" : "FHIR")} / {failure} / failure {(failureFirst ? "before" : "after")} / usable winner={usableWinner}; strict fallback script complete.");
    }

    private enum R1ListingFailure { NotFound, HttpStatus, Transport, Deadline, Malformed }

    private static string R1ListingUri(string root, string name) =>
        new Uri(new Uri(root.TrimEnd('/') + "/").AbsoluteUri.TrimEnd('/') + "/" + Uri.EscapeDataString(name)).AbsoluteUri;

    /// <summary>A finite ordered script; only explicitly enumerated loopback requests can reach sockets.</summary>
    private sealed class R1RegistryHandler : DelegatingHandler
    {
        private readonly Queue<(string uri, R1ListingFailure? failure, LocalHttpFixture.RawResponse? response)> _remaining = new();
        private readonly List<string> _expected = [];
        private readonly List<string> _requests = [];
        private readonly List<R1ResponseContent> _contents = [];
        private readonly List<string> _unexpected = [];
        private int _cancellations;

        internal R1RegistryHandler() : base(new SocketsHttpHandler { AllowAutoRedirect = false, UseProxy = false }) { }

        internal bool Disposed { get; private set; }

        internal void ExpectLoopback(string uri)
        {
            Uri target = new(uri);
            Assert.Equal("http", target.Scheme);
            Assert.Equal("127.0.0.1", target.Host);
            Add(uri, null, null);
        }

        internal void ExpectFailure(string uri, R1ListingFailure failure) => Add(uri, failure, null);

        internal void ExpectResponse(string uri, LocalHttpFixture.RawResponse response) => Add(uri, null, response);

        private void Add(string uri, R1ListingFailure? failure, LocalHttpFixture.RawResponse? response)
        {
            _expected.Add("GET " + uri);
            _remaining.Enqueue((uri, failure, response));
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string actual = request.Method + " " + request.RequestUri!.AbsoluteUri;
            _requests.Add(actual);
            if (_remaining.Count == 0 || request.Method != HttpMethod.Get || _remaining.Peek().uri != request.RequestUri.AbsoluteUri)
            {
                _unexpected.Add(actual);
                throw new InvalidOperationException("Unlisted R1 registry request; outbound I/O refused.");
            }
            (string uri, R1ListingFailure? failure, LocalHttpFixture.RawResponse? response) = _remaining.Dequeue();
            if (failure == R1ListingFailure.Transport)
            {
                throw new HttpRequestException("exception-message-secret https://credential-user:credential-password@registry.example.test/failed?query-secret=hidden response-body-secret");
            }
            if (failure == R1ListingFailure.Deadline)
            {
                Assert.True(cancellationToken.CanBeCanceled);
                try
                {
                    await Task.Delay(LocalHttpFixture.Deadline, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    _cancellations++;
                    throw;
                }
                throw new InvalidOperationException("The scripted delay completed without registry cancellation.");
            }
            if (response is not null)
            {
                R1ResponseContent content = new(response.Body);
                _contents.Add(content);
                if (response.ContentType is not null)
                {
                    content.Headers.ContentType = new(response.ContentType);
                }
                return new HttpResponseMessage(response.Status) { RequestMessage = request, Content = content };
            }
            Assert.Equal("127.0.0.1", new Uri(uri).Host);
            return await base.SendAsync(request, cancellationToken);
        }

        internal void AssertComplete(int cancellations)
        {
            Assert.Empty(_unexpected);
            Assert.Equal(_expected, _requests);
            Assert.Empty(_remaining);
            Assert.Equal(cancellations, _cancellations);
            Assert.All(_contents, content => Assert.True(content.Disposed));
        }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }

    private sealed class R1ResponseContent(byte[] bytes) : ByteArrayContent(bytes)
    {
        internal bool Disposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }

    private sealed class R1PackageLogger : ILogger<FhirPackageService>
    {
        internal List<(LogLevel level, string message, Exception? exception, string fields)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            string fields = state is IEnumerable<KeyValuePair<string, object?>> values
                ? string.Join("|", values.Select(value => value.Key + "=" + value.Value)) : string.Empty;
            Entries.Add((logLevel, formatter(state, exception), exception, fields));
        }
    }

    #endregion

    #region R1 replacement-only H1 binding and diagnostics

    private sealed class R1RegistryClients : IDisposable
    {
        private readonly List<FhirPkg.Registry.IRegistryClient> _target;
        private readonly FhirPkg.Registry.IRegistryClient[] _original;
        private readonly List<FhirPkg.Registry.IRegistryClient> _owned = [];

        internal R1RegistryClients(FhirPackageService service, HttpClient http, IEnumerable<string> endpoints, bool npm)
        {
            Assert.Equal(Timeout.InfiniteTimeSpan, http.Timeout);
            FieldInfo field = typeof(FhirPackageService).GetField("_packageClients", BindingFlags.Instance | BindingFlags.NonPublic)!;
            _target = Assert.IsType<List<FhirPkg.Registry.IRegistryClient>>(field.GetValue(service));
            _original = _target.ToArray();
            try
            {
                foreach (string url in endpoints)
                {
                    FhirPkg.Registry.RegistryEndpoint endpoint = new()
                    {
                        Url = url,
                        Type = npm ? FhirPkg.Models.RegistryType.Npm : FhirPkg.Models.RegistryType.FhirNpm,
                    };
                    _owned.Add(FhirPkg.Registry.RegistryClientFactory.CreateClientForEndpoint(endpoint,
                        FhirPkg.Registry.RegistryHttpTransport.CreateRedirectControlled(http, TimeSpan.FromMilliseconds(100), 2),
                        NullLoggerFactory.Instance, null));
                }
                _target.Clear();
                _target.AddRange(_owned);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            _target.Clear();
            _target.AddRange(_original);
            foreach (FhirPkg.Registry.IRegistryClient client in _owned)
            {
                (client as IDisposable)?.Dispose();
            }
            _owned.Clear();
        }
    }

    private void AssertR1InvalidRegistryConfiguration(bool npm)
    {
        using LocalPackageFixture files = new();
        CandleConfig config = files.Configuration() with
        {
            AdditionalFhirRegistryUrls = npm ? [] : ["not-an-absolute-registry"],
            AdditionalNpmRegistryUrls = npm ? ["not-an-absolute-registry"] : [],
        };
        R1PackageLogger logger = new();
        using FhirPackageService service = new(logger, config);
        ArgumentException error = Assert.Throws<ArgumentException>(service.Init);
        _output.WriteLine($"R1-NATIVE-CONFIG|net{Environment.Version.Major}.0|protocol={(npm ? "NPM" : "FHIR")}|stage=Init|exception={error.GetType().FullName}; no acquisition attempted.");
        Assert.DoesNotContain(logger.Entries, entry => entry.level == LogLevel.Warning);
        Assert.Empty(Directory.GetFileSystemEntries(files.CacheDirectory));
    }

    private static async Task AssertR1NativeDeadline(bool npm)
    {
        using LocalPackageFixture files = new();
        using FhirPackageService service = PackageService(files.Configuration());
        service.Init();
        const string Root = "https://registry.example.test/deadline";
        using R1RegistryHandler handler = new();
        handler.ExpectFailure(R1ListingUri(Root, "example.test.r1.deadline-control"), R1ListingFailure.Deadline);
        using HttpClient http = new(handler) { Timeout = Timeout.InfiniteTimeSpan };
        using (R1RegistryClients clients = new(service, http, [Root], npm))
        {
            FieldInfo field = typeof(FhirPackageService).GetField("_packageClients", BindingFlags.Instance | BindingFlags.NonPublic)!;
            FhirPkg.Registry.IRegistryClient client = Assert.Single(
                Assert.IsType<List<FhirPkg.Registry.IRegistryClient>>(field.GetValue(service)));
            await Assert.ThrowsAsync<FhirPkg.Registry.RegistryResponseTimeoutException>(() =>
                client.GetPackageListingAsync("example.test.r1.deadline-control").WaitAsync(LocalHttpFixture.Deadline));
            handler.AssertComplete(1);
        }
        http.Dispose();
        Assert.True(handler.Disposed);
    }

    private static void AssertR1UnavailableWarnings(R1PackageLogger logger, string name, string endpoint, R1ListingFailure failure)
    {
        (LogLevel level, string message, Exception? exception, string fields)[] warnings =
            logger.Entries.Where(entry => entry.level == LogLevel.Warning).ToArray();
        if (failure == R1ListingFailure.NotFound)
        {
            Assert.Empty(warnings);
            return;
        }
        (LogLevel level, string message, Exception? exception, string fields) warning = Assert.Single(warnings);
        Assert.Null(warning.exception);
        Assert.Contains(name, warning.message, StringComparison.Ordinal);
        Uri uri = new(endpoint);
        Assert.Contains(uri.Host, warning.message, StringComparison.Ordinal);
        Assert.Contains(uri.AbsolutePath, warning.message, StringComparison.Ordinal);
        string category = failure switch
        {
            R1ListingFailure.HttpStatus => "http",
            R1ListingFailure.Transport => "transport",
            R1ListingFailure.Deadline => "deadline",
            R1ListingFailure.Malformed => "listing-json",
            _ => throw new InvalidOperationException("No warning category for this case."),
        };
        Assert.Contains(category, warning.message, StringComparison.OrdinalIgnoreCase);
        if (failure == R1ListingFailure.HttpStatus)
        {
            Assert.Contains("503", warning.message, StringComparison.Ordinal);
        }
        foreach (string sentinel in new[]
        {
            "credential-user", "credential-password", "query-secret", "hidden",
            "response-body-secret", "exception-message-secret", "malformed-body-secret",
        })
        {
            Assert.DoesNotContain(sentinel, warning.message + warning.fields, StringComparison.Ordinal);
        }
    }

    #endregion

    #region R1 portable H2 cold acquisition and byte evidence

    /// <summary>All six metadata forms execute actual cold acquisition independently for each supported FHIR version.</summary>
    public static IEnumerable<object[]> ColdManifestCases =>
        from version in _versions
        from repository in new[] { "string", "object", "absent" }
        from integerDate in new[] { true, false }
        select new object[] { $"R1 cold / {version} / repository={repository} / date={(integerDate ? "integer" : "string")}",
            version, repository, integerDate };

    /// <summary>Legacy repository/date and both author shapes survive validating cold import and real tenant loading.</summary>
    [Theory]
    [MemberData(nameof(ColdManifestCases))]
    public async Task ColdImportPreservesLegacyManifestShapes(
        string label, FhirReleases.FhirSequenceCodes version, string repository, bool integerDate)
    {
        Assert.Equal(18, ColdManifestCases.Count());
        using LocalPackageFixture files = new();
        string name = $"example.test.r1.cold.{LocalPackageFixture.VersionName(version)}.repo-{repository}.date-{(integerDate ? "integer" : "string")}";
        PackageReference reference = new(name, "1.0.0");
        string manifest = R1ColdManifest(name, version, repository, integerDate);
        Dictionary<string, string> resources = new(StringComparer.Ordinal)
        {
            ["Patient-cold-patient.json"] = R1ColdResource("Patient", CompatibilityFormat.Json),
            ["Observation-cold-observation.xml"] = R1ColdResource("Observation", CompatibilityFormat.Xml),
        };
        Dictionary<string, byte[]> distributed = resources.ToDictionary(
            resource => "package/" + resource.Key, resource => Encoding.UTF8.GetBytes(resource.Value), StringComparer.Ordinal);
        distributed.Add("package/package.json", Encoding.UTF8.GetBytes(manifest));
        byte[] archive = R1DeterministicArchive(manifest, resources);
        Assert.Equal(archive, R1DeterministicArchive(manifest, resources));
        string hashes = $"R1-SHA256|net{Environment.Version.Major}.0|{version}|repository={repository}|date={(integerDate ? "integer" : "string")}|author={(integerDate ? "string" : "object")}|name={name}" +
            $"|archive={Convert.ToHexString(SHA256.HashData(archive))}" +
            string.Concat(distributed.OrderBy(file => file.Key, StringComparer.Ordinal)
                .Select(file => $"|{file.Key}={Convert.ToHexString(SHA256.HashData(file.Value))}"));
        _output.WriteLine(hashes);
        Console.WriteLine(hashes);

        List<(string root, Dictionary<string, byte[]> files, string[] directories)> neighbors = [];
        foreach (string identity in new[] { name + "#2.0.0", name + ".neighbor#1.0.0" })
        {
            string root = Path.Combine(files.CacheDirectory, identity);
            Directory.CreateDirectory(Path.Combine(root, "package", "empty"));
            File.WriteAllBytes(Path.Combine(root, "parent-metadata.bin"), [0, 255, 128, 10]);
            File.WriteAllBytes(Path.Combine(root, "package", "package.json"),
                Encoding.UTF8.GetBytes(ProtocolManifest(identity.Split('#')[0])));
            File.WriteAllBytes(Path.Combine(root, "package", "Patient-neighbor.json"),
                Encoding.UTF8.GetBytes("""{"resourceType":"Patient","id":"neighbor","active":false}"""));
            (Dictionary<string, byte[]> bytes, string[] directories) = R1TreeSnapshot(root);
            neighbors.Add((root, bytes, directories));
        }
        string requestedRoot = Path.Combine(files.CacheDirectory, name + "#1.0.0");
        Assert.False(Directory.Exists(requestedRoot));

        // Replacement-only control: omit this call and its helper from the original-supplier port.
        await AssertR1NativeColdImport(files, reference, archive, distributed);

        await using LocalHttpFixture registry = await LocalHttpFixture.StartAsync();
        LocalHttpFixture.ExpectedRequest download = registry.Expect("GET", "/" + name + "/1.0.0",
            new LocalHttpFixture.RawResponse(HttpStatusCode.OK, "application/octet-stream", archive));
        using (FhirPackageService acquisition = PackageService(files.Configuration(registry.Address.AbsoluteUri.TrimEnd('/'))))
        {
            acquisition.Init();
            PackageReference installed = Assert.Single(await acquisition.InstallPackages([reference.Moniker], [], [version])
                .WaitAsync(LocalHttpFixture.Deadline));
            Assert.Equal(reference, installed);
            Assert.Null(installed.Scope);
            Assert.Equal(LegacyContentPath(files.CacheDirectory, reference), acquisition.GetPackageContentDirectory(installed));
            Assert.Equal(new[] { version }, await acquisition.InstalledPackageFhirVersions(installed));
            PackageManifest parsed = Assert.IsType<PackageManifest>(await new DiskPackageCache(files.CacheDirectory).ReadManifestEx(installed));
            Assert.Equal(name, parsed.Name);
            Assert.Equal("1.0.0", parsed.Version);
            Assert.Equal(new[] { LocalPackageFixture.FhirVersion(version) }, parsed.AnyFhirVersions);
            AssertAuthor(parsed, "Synthetic Package Author", "author@example.test", "https://example.test/sdk6");
            FieldInfo origin = typeof(fhir.candle._ForPackages.AuthorInfo).GetField("ParsedFromString", BindingFlags.Instance | BindingFlags.NonPublic)!;
            Assert.Equal(integerDate, origin.GetValue(parsed.AuthorInformation));
            DateTime local = new(2024, 2, 3, 12, 34, 56, DateTimeKind.Unspecified);
            Assert.Equal(new DateTimeOffset(local, TimeZoneInfo.Local.GetUtcOffset(local)), parsed.Date);
            Assert.Equal("Conformance", parsed.Type);
            Assert.Equal("MIT", parsed.License);
            Assert.Equal("package", parsed.Directories!["lib"]);
            Assert.Empty(parsed.Dependencies!);
        }
        await AssertRoutes(registry, [download]);
        R1AssertDistributedBytes(requestedRoot, distributed);
        AssertR1ColdPublicationTree(requestedRoot, distributed);
        (Dictionary<string, byte[]> files, string[] directories) published = R1TreeSnapshot(requestedRoot);

        CandleConfig config = files.Configuration() with { PublishedPackages = [reference.Moniker] };
        using FhirPackageService packages = PackageService(config);
        string tenant = "cold-" + LocalPackageFixture.VersionName(version);
        FhirStoreManager manager = new(new Dictionary<string, TenantConfiguration> { [tenant] = Tenant(version, tenant) },
            NullLogger<FhirStoreManager>.Instance, config, packages);
        try
        {
            manager.Init();
            IFhirStore store = ((IFhirStoreManager)manager)[tenant];
            Assert.Equal(new[] { reference.Moniker }, store.LoadedPackageDirectives);
            Assert.Equal(new[] { name }, store.LoadedPackageIds);
            Assert.Equal(new[] { "cold-patient" }, store["Patient"].Keys);
            Assert.Equal(new[] { "cold-observation" }, store["Observation"].Keys);
            Assert.False(store["Patient"].ContainsKey("neighbor"));
            CompatibilityBindings bindings = new();
            WireContractAssertions.BindBaseUrl(bindings, tenant, store.Config.BaseUrl);
            foreach (string resourceType in new[] { "Patient", "Observation" })
            {
                string id = resourceType == "Patient" ? "cold-patient" : "cold-observation";
                GeneratedResourceExpectation binding = new(tenant + "/" + id, resourceType, "1", id);
                foreach (CompatibilityFormat format in new[] { CompatibilityFormat.Json, CompatibilityFormat.Xml })
                {
                    FhirRequestContext request = new(store, "GET", resourceType + "/" + id)
                    {
                        DestinationFormat = WireContractAssertions.MediaType(format),
                    };
                    Assert.True(store.PerformInteraction(request, out FhirResponseContext response, serializeReturn: true));
                    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                    Assert.Equal(resourceType, response.ResourceType);
                    Assert.Equal(id, response.Id);
                    WireContractAssertions.Body(new ResourceBodyExpectation(R1ColdResource(resourceType, format), binding),
                        response.SerializedResource, format, store.Config.BaseUrl, bindings);
                    binding = binding with { Mode = CompatibilityBindingMode.Reuse };
                }
            }
        }
        finally
        {
            await StopAndDispose(manager);
        }
        R1AssertDistributedBytes(requestedRoot, distributed);
        R1AssertTreeSnapshot(published, requestedRoot);
        foreach ((string root, Dictionary<string, byte[]> bytes, string[] directories) in neighbors)
        {
            R1AssertTreeSnapshot((bytes, directories), root);
        }
        Assert.Empty(Directory.GetDirectories(files.CacheDirectory, ".candle-package-import-*"));
        Assert.Equal(neighbors.Select(neighbor => Path.GetFileName(neighbor.root)).Append(name + "#1.0.0").Order(StringComparer.Ordinal),
            Directory.GetFileSystemEntries(files.CacheDirectory).Select(Path.GetFileName).Order(StringComparer.Ordinal));
        Assert.Equal(new[] { "cache", "source" }, Directory.GetFileSystemEntries(files.Root).Select(Path.GetFileName).Order(StringComparer.Ordinal));
        Assert.Single(registry.Requests);
        _output.WriteLine($"{label}; cold acquisition + raw SHA256 + local author/date/FHIR + manager JSON/XML + neighbors/cleanup passed.");
    }

    private static string R1ColdManifest(string name, FhirReleases.FhirSequenceCodes version, string repository, bool integerDate)
    {
        string repositoryField = repository switch
        {
            "string" => "\"repository\":\"github:example/repo\",",
            "object" => "\"repository\":{\"type\":\"git\",\"url\":\"https://example.test/repo.git\"},",
            "absent" => string.Empty,
            _ => throw new ArgumentOutOfRangeException(nameof(repository)),
        };
        string author = integerDate
            ? "\"Synthetic Package Author <author@example.test> (https://example.test/sdk6)\""
            : """{"name":"Synthetic Package Author","email":"author@example.test","url":"https://example.test/sdk6"}""";
        string date = integerDate ? "20240203123456" : "\"20240203123456\"";
        return $$$"""{"name":"{{{name}}}","version":"1.0.0","type":"Conformance","title":"Synthetic R1 cold manifest","description":"Independently authored metadata acceptance bytes; café.","fhirVersions":["{{{LocalPackageFixture.FhirVersion(version)}}}"],"dependencies":{},"license":"MIT","author":{{{author}}},"canonical":"https://example.test/sdk6/{{{LocalPackageFixture.VersionName(version)}}}","directories":{"lib":"package"},{{{repositoryField}}}"date":{{{date}}},"unknown-field":{"retained":"raw fixture","values":[1,true,"café"]}}""";
    }

    private static string R1ColdResource(string resourceType, CompatibilityFormat format) => (resourceType, format) switch
    {
        ("Patient", CompatibilityFormat.Json) => """{"resourceType":"Patient","id":"cold-patient","active":true,"name":[{"family":"Pinned café"}]}""",
        ("Patient", CompatibilityFormat.Xml) => """<Patient xmlns="http://hl7.org/fhir"><id value="cold-patient"/><active value="true"/><name><family value="Pinned café"/></name></Patient>""",
        ("Observation", CompatibilityFormat.Json) => """{"resourceType":"Observation","id":"cold-observation","status":"final","code":{"text":"Pinned cold metadata"},"subject":{"reference":"Patient/cold-patient"},"valueString":"café"}""",
        ("Observation", CompatibilityFormat.Xml) => """<Observation xmlns="http://hl7.org/fhir"><id value="cold-observation"/><status value="final"/><code><text value="Pinned cold metadata"/></code><subject><reference value="Patient/cold-patient"/></subject><valueString value="café"/></Observation>""",
        _ => throw new ArgumentOutOfRangeException(nameof(resourceType)),
    };

    private static byte[] R1DeterministicArchive(string manifest, IReadOnlyDictionary<string, string> resources)
    {
        // Reuse the fixture, but normalize its unpinned package/ directory mtime through BCL APIs.
        // NoCompression avoids compressor-version drift between the two runtime/supplier baselines.
        using MemoryStream authored = new(LocalPackageFixture.Archive(manifest, resources), writable: false);
        using GZipStream input = new(authored, CompressionMode.Decompress);
        using MemoryStream buffer = new();
        input.CopyTo(buffer);
        buffer.Position = 0;
        using TarReader reader = new(buffer);
        using MemoryStream output = new();
        using (GZipStream gzip = new(output, CompressionLevel.NoCompression, leaveOpen: true))
        using (TarWriter writer = new(gzip, TarEntryFormat.Ustar, leaveOpen: true))
        {
            while (reader.GetNextEntry() is TarEntry original)
            {
                UstarTarEntry entry = new(original.EntryType, original.Name)
                {
                    ModificationTime = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
                    Uid = 0,
                    Gid = 0,
                    UserName = string.Empty,
                    GroupName = string.Empty,
                    Mode = original.EntryType == TarEntryType.Directory
                        ? UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                        : UnixFileMode.UserRead | UnixFileMode.UserWrite,
                };
                if (original.DataStream is not null)
                {
                    entry.DataStream = original.DataStream;
                }
                writer.WriteEntry(entry);
            }
        }
        return output.ToArray();
    }

    private static (Dictionary<string, byte[]> files, string[] directories) R1TreeSnapshot(string root) =>
        (Directory.GetFiles(root, "*", SearchOption.AllDirectories)
            .ToDictionary(path => Path.GetRelativePath(root, path), File.ReadAllBytes, StringComparer.Ordinal),
        Directory.GetDirectories(root, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(root, path)).Order(StringComparer.Ordinal).ToArray());

    private static void R1AssertTreeSnapshot((Dictionary<string, byte[]> files, string[] directories) expected, string root)
    {
        (Dictionary<string, byte[]> files, string[] directories) actual = R1TreeSnapshot(root);
        Assert.Equal(expected.directories, actual.directories);
        Assert.Equal(expected.files.Keys.Order(StringComparer.Ordinal), actual.files.Keys.Order(StringComparer.Ordinal));
        foreach (KeyValuePair<string, byte[]> file in expected.files)
        {
            Assert.Equal(file.Value, actual.files[file.Key]);
        }
    }

    private static void R1AssertDistributedBytes(string root, IReadOnlyDictionary<string, byte[]> expected)
    {
        foreach (KeyValuePair<string, byte[]> file in expected)
        {
            byte[] actual = File.ReadAllBytes(Path.Combine(root, file.Key));
            Assert.Equal(file.Value, actual);
            Assert.Equal(Convert.ToHexString(SHA256.HashData(file.Value)), Convert.ToHexString(SHA256.HashData(actual)));
        }
    }

    private static void AssertR1ColdPublicationTree(string root, IReadOnlyDictionary<string, byte[]> expected)
    {
        Assert.Equal(expected.Keys.Order(StringComparer.Ordinal), Directory.GetFiles(root, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/')).Order(StringComparer.Ordinal));
        Assert.Equal(new[] { "package" }, Directory.GetDirectories(root, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(root, path)));
    }

    #endregion

    #region R1 replacement-only H2 validating native control

    private static async Task AssertR1NativeColdImport(
        LocalPackageFixture files, PackageReference reference, byte[] archive, IReadOnlyDictionary<string, byte[]> expected)
    {
        string root = files.CreateDirectory("r1-native-import");
        (Dictionary<string, byte[]> files, string[] directories) before = R1TreeSnapshot(files.CacheDirectory);
        try
        {
            using FhirPkg.Cache.DiskPackageCache cache = new(root, NullLogger<FhirPkg.Cache.DiskPackageCache>.Instance);
            using MemoryStream stream = new(archive, writable: false);
            FhirPkg.Models.PackageRecord installed = await cache.ImportAsync(
                stream, new FhirPkg.Cache.InstallCacheOptions { Unmodified = true }).WaitAsync(LocalHttpFixture.Deadline);
            Assert.Equal(reference.Name, installed.Reference.Name);
            Assert.Equal(reference.Version, installed.Reference.Version);
            Assert.Equal(Path.Combine(installed.DirectoryPath, "package"), cache.GetPackageContentPath(installed.Reference));
            R1AssertDistributedBytes(installed.DirectoryPath, expected);
            AssertR1ColdPublicationTree(installed.DirectoryPath, expected);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
        Assert.False(Directory.Exists(root));
        R1AssertTreeSnapshot(before, files.CacheDirectory);
    }

    #endregion

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

    /// <summary>Variant recursion, preference, examples, supplements, and ordering retain the existing named-store boundary.</summary>
    [Theory]
    [MemberData(nameof(VersionCases))]
    public async Task PackageVariantsExamplesAndSupplementsPreserveTenantLoading(string label, FhirReleases.FhirSequenceCodes version)
    {
        using LocalPackageFixture files = new();
        _output.WriteLine($"{label}; variants/examples/supplements; owned root: {files.Root}");
        FhirReleases.FhirSequenceCodes other = _versions[(_versions.ToList().IndexOf(version) + 1) % _versions.Length];
        string suffix = LocalPackageFixture.VersionName(version);
        await using LocalHttpFixture registry = await LocalHttpFixture.StartAsync();
        string root = registry.Address.AbsoluteUri.TrimEnd('/');
        List<LocalHttpFixture.ExpectedRequest> routes = [];

        foreach (bool replaceSuffix in new[] { false, true })
        {
            string baseName = "example.test.variant." + suffix + (replaceSuffix ? ".replace" : ".umbrella");
            string originalName = baseName + (replaceSuffix ? "." + LocalPackageFixture.VersionName(other) : string.Empty);
            string desiredName = baseName + "." + suffix;
            string originalManifest = AuthoredManifest(originalName, "1.0.0", other);
            string desiredManifest = AuthoredManifest(desiredName, "1.0.0", version);
            Dictionary<string, string> resources = new()
            {
                ["Patient-variant-patient.json"] = LocalPackageFixture.WithId(CompatibilityReplay.LoadFixture(version, "patient.json"), "variant-patient"),
                ["Observation-variant-observation.xml"] = LocalPackageFixture.WithId(CompatibilityReplay.LoadFixture(version, "observation.xml"), "variant-observation"),
            };
            routes.Add(registry.Expect("GET", "/fhir/" + originalName + "/1.0.0",
                new LocalHttpFixture.RawResponse(HttpStatusCode.OK, "application/octet-stream",
                    LocalPackageFixture.Archive(originalManifest, new Dictionary<string, string>()))));
            routes.Add(registry.Expect("GET", "/fhir/" + desiredName,
                LocalHttpFixture.RawResponse.Text(HttpStatusCode.OK, "application/json", RegistryListing(desiredName, "invalid-version"))));
            routes.Add(registry.Expect("GET", "/npm/" + desiredName,
                LocalHttpFixture.RawResponse.Text(HttpStatusCode.OK, "application/json", RegistryListing(desiredName, "1.0.0"))));
            routes.Add(registry.Expect("GET", "/fhir/" + desiredName + "/1.0.0", LocalHttpFixture.RawResponse.Empty(HttpStatusCode.NotFound)));
            routes.Add(registry.Expect("GET", "/npm/" + desiredName + "/-/" + desiredName + "-1.0.0.tgz",
                new LocalHttpFixture.RawResponse(HttpStatusCode.OK, "application/octet-stream", LocalPackageFixture.Archive(desiredManifest, resources))));
            using FhirPackageService service = PackageService(files.Configuration(root + "/fhir") with { AdditionalNpmRegistryUrls = [root + "/npm"] });
            service.Init();
            List<PackageReference> installed = await service.InstallPackages([originalName + "#1.0.0"], [], [version]).WaitAsync(LocalHttpFixture.Deadline);
            Assert.Equal(new[] { originalName + "@1.0.0", desiredName + "@1.0.0" }, installed.Select(reference => reference.Moniker));
            Assert.Equal(new[] { other }, await service.InstalledPackageFhirVersions(installed[0]));
            Assert.Equal(new[] { version }, await service.InstalledPackageFhirVersions(installed[1]));
            string content = LegacyContentPath(files.CacheDirectory, installed[1]);
            Assert.Equal(content, service.GetPackageContentDirectory(installed[1]));
            Assert.Equal(Encoding.UTF8.GetBytes(desiredManifest), File.ReadAllBytes(Path.Combine(content, "package.json")));
            foreach (KeyValuePair<string, string> resource in resources)
            {
                Assert.Equal(Encoding.UTF8.GetBytes(resource.Value), File.ReadAllBytes(Path.Combine(content, resource.Key)));
            }
            Assert.Empty(await service.InstallPackages([originalName + "@1.0.0", desiredName + "#1.0.0"], [], [version]));
        }

        string missingName = "example.test.variant." + suffix + ".missing";
        string missingVariant = missingName + "." + suffix;
        routes.Add(registry.Expect("GET", "/fhir/" + missingName + "/1.0.0",
            new LocalHttpFixture.RawResponse(HttpStatusCode.OK, "application/octet-stream",
                LocalPackageFixture.Archive(AuthoredManifest(missingName, "1.0.0", other), new Dictionary<string, string>()))));
        routes.Add(registry.Expect("GET", "/fhir/" + missingVariant, LocalHttpFixture.RawResponse.Empty(HttpStatusCode.NotFound)));
        routes.Add(registry.Expect("GET", "/npm/" + missingVariant,
            LocalHttpFixture.RawResponse.Text(HttpStatusCode.OK, "application/json", RegistryListing(missingVariant))));
        using (FhirPackageService service = PackageService(files.Configuration(root + "/fhir") with { AdditionalNpmRegistryUrls = [root + "/npm"] }))
        {
            service.Init();
            PackageReference installed = Assert.Single(await service.InstallPackages([missingName + "#1.0.0"], [], [version]).WaitAsync(LocalHttpFixture.Deadline));
            Assert.Equal(missingName + "@1.0.0", installed.Moniker);
            Assert.Equal(new[] { other }, await service.InstalledPackageFhirVersions(installed));
            Assert.False(await new DiskPackageCache(files.CacheDirectory).IsInstalled(new(missingVariant, "1.0.0")));
        }
        await AssertRoutes(registry, routes);

        string missingTenant = "missing-" + suffix;
        Directory.CreateDirectory(Path.Combine(files.SourceDirectory, missingTenant));
        CandleConfig missingConfig = files.Configuration() with { PublishedPackages = [missingName + "#1.0.0"] };
        using (FhirPackageService packages = PackageService(missingConfig))
        {
            FhirStoreManager manager = new(new() { [missingTenant] = Tenant(version, missingTenant) },
                NullLogger<FhirStoreManager>.Instance, missingConfig, packages);
            try
            {
                manager.Init();
                IFhirStore store = ((IFhirStoreManager)manager)[missingTenant];
                Assert.Empty(store.LoadedPackageDirectives);
                Assert.Empty(store.LoadedPackageIds);
                Assert.Equal(new[] { Path.Combine(files.SourceDirectory, missingTenant) }, store.LoadedSupplements);
                Assert.Empty(store["Patient"].Keys);
                Assert.Empty(store["Observation"].Keys);
            }
            finally
            {
                await StopAndDispose(manager);
            }
        }

        string umbrellaName = "example.test.preference." + suffix;
        PackageReference umbrella = new(umbrellaName, "1.0.0");
        PackageReference preferred = new(umbrellaName + "." + suffix, "1.0.0");
        string orderName = "example.test.order." + suffix;
        PackageReference older = new(orderName, "1.0.0");
        PackageReference newer = new(orderName, "2.0.0");
        DiskPackageCache cache = new(files.CacheDirectory);
        await cache.Install(umbrella, LocalPackageFixture.Archive(AuthoredManifest(umbrellaName, "1.0.0", _versions),
            new Dictionary<string, string> { ["Patient-umbrella.json"] = AuthoredPatientJson("umbrella", "Umbrella", true) })).WaitAsync(LocalHttpFixture.Deadline);
        Dictionary<string, byte[]> distribution = new(StringComparer.Ordinal)
        {
            ["package/package.json"] = Encoding.UTF8.GetBytes(AuthoredManifest(preferred.Name!, "1.0.0", version)),
            ["package/Patient-preferred.json"] = Encoding.UTF8.GetBytes(LocalPackageFixture.WithId(CompatibilityReplay.LoadFixture(version, "patient.json"), "preferred")),
            ["package/Observation-preferred.xml"] = Encoding.UTF8.GetBytes(LocalPackageFixture.WithId(
                CompatibilityReplay.LoadFixture(version, "observation.xml").Replace("Patient/wire-patient", "Patient/preferred", StringComparison.Ordinal), "preferred-observation")),
            ["package/examples/Patient-example.json"] = Encoding.UTF8.GetBytes(AuthoredPatientJson("example", "Nested example", true)),
            ["package/examples/Observation-example.xml"] = Encoding.UTF8.GetBytes(LocalPackageFixture.WithId(
                CompatibilityReplay.LoadFixture(version, "observation.xml"), "example-observation")),
            ["examples/Patient-outside.xml"] = Encoding.UTF8.GetBytes(AuthoredPatientXml("outside", "Distributed outside", false)),
            ["siblings/authored.bin"] = [0, 128, 255],
        };
        await cache.Install(preferred, DistributedArchive(distribution)).WaitAsync(LocalHttpFixture.Deadline);
        foreach ((PackageReference reference, string family, bool active) in new[]
        {
            (older, "Older", true), (newer, "Newest", false),
        })
        {
            await cache.Install(reference, LocalPackageFixture.Archive(AuthoredManifest(reference.Name!, reference.Version!, version),
                new Dictionary<string, string> { ["Patient-ordered.json"] = AuthoredPatientJson("ordered", family, active) })).WaitAsync(LocalHttpFixture.Deadline);
        }
        string preferredRoot = Path.GetDirectoryName(LegacyContentPath(files.CacheDirectory, preferred))!;
        (Dictionary<string, byte[]> files, string[] directories) distributedBefore = Snapshot(preferredRoot);
        Assert.Equal(distribution.Keys.Order(StringComparer.Ordinal), distributedBefore.files.Keys.Select(path => path.Replace('\\', '/')).Order(StringComparer.Ordinal));
        foreach (KeyValuePair<string, byte[]> file in distribution)
        {
            Assert.Equal(file.Value, File.ReadAllBytes(Path.Combine(preferredRoot, file.Key)));
        }
        Assert.Contains("empty-distributed-directory", distributedBefore.directories);

        // Each fresh manager has its own independently authored precedence candidates.
        foreach (int precedence in new[] { 0, 1, 2 })
        {
            string source = files.CreateDirectory("precedence-" + precedence);
            string[] supplementNames = [preferred.Moniker, preferred.Name + "#" + preferred.Version, preferred.Name!];
            for (int candidate = precedence; candidate < supplementNames.Length; candidate++)
            {
                string directory = Path.Combine(source, supplementNames[candidate]);
                Directory.CreateDirectory(directory);
                File.WriteAllBytes(Path.Combine(directory, "Patient-supplement.json"), Encoding.UTF8.GetBytes(
                    AuthoredPatientJson("supplement-" + candidate, "Supplement " + candidate, true)));
                File.WriteAllBytes(Path.Combine(directory, "Observation-supplement.xml"), Encoding.UTF8.GetBytes(
                    LocalPackageFixture.WithId(CompatibilityReplay.LoadFixture(version, "observation.xml"), "supplement-observation-" + candidate)));
            }
            bool examples = precedence != 0;
            Dictionary<string, TenantConfiguration> tenants = new(StringComparer.Ordinal);
            foreach (FhirReleases.FhirSequenceCodes candidate in _versions)
            {
                string name = "variant-" + LocalPackageFixture.VersionName(candidate);
                tenants.Add(name, Tenant(candidate, name));
            }
            string targetName = "variant-" + suffix;
            string siblingName = targetName + "-sibling";
            tenants.Add(siblingName, Tenant(version, siblingName));
            foreach (string name in tenants.Keys)
            {
                Directory.CreateDirectory(Path.Combine(source, name));
            }
            File.WriteAllBytes(Path.Combine(source, targetName, "Patient-local.json"),
                Encoding.UTF8.GetBytes(AuthoredPatientJson("tenant-local", "Named tenant only", true)));
            CandleConfig config = files.Configuration() with
            {
                SourceDirectory = source,
                PublishedPackages = [newer.Moniker, umbrella.Moniker, preferred.Moniker, older.Moniker],
                LoadPackageExamples = examples,
            };
            using FhirPackageService packages = PackageService(config);
            FhirStoreManager manager = new(tenants, NullLogger<FhirStoreManager>.Instance, config, packages);
            try
            {
                manager.Init();
                CompatibilityBindings bindings = new();
                foreach (IFhirStore store in ((IFhirStoreManager)manager).Values)
                {
                    string tenant = store.Config.ControllerName;
                    WireContractAssertions.BindBaseUrl(bindings, tenant, store.Config.BaseUrl);
                    if (store.Config.FhirVersion != version)
                    {
                        Assert.Equal(new[] { umbrella.Moniker }, store.LoadedPackageDirectives);
                        Assert.Equal(new[] { umbrellaName }, store.LoadedPackageIds);
                        Assert.Equal(new[] { Path.Combine(source, tenant) }, store.LoadedSupplements);
                        Assert.Equal(new[] { "umbrella" }, store["Patient"].Keys);
                        Assert.Empty(store["Observation"].Keys);
                        AssertAuthoredPatient(store, bindings, "umbrella", "Umbrella", true);
                        continue;
                    }
                    Assert.Equal(new[] { preferred.Moniker, older.Moniker, newer.Moniker }, store.LoadedPackageDirectives);
                    Assert.Equal(new[] { umbrellaName, preferred.Name!, "example.test.order", orderName }.Order(StringComparer.Ordinal),
                        store.LoadedPackageIds.Order(StringComparer.Ordinal));
                    Assert.Equal(new[] { Path.Combine(source, supplementNames[precedence]), Path.Combine(source, tenant) }.Order(StringComparer.Ordinal),
                        store.LoadedSupplements.Order(StringComparer.Ordinal));
                    List<string> patients = ["preferred", "ordered", "supplement-" + precedence];
                    if (examples)
                    {
                        patients.Add("example");
                    }
                    if (tenant == targetName)
                    {
                        patients.Add("tenant-local");
                        AssertAuthoredPatient(store, bindings, "tenant-local", "Named tenant only", true);
                    }
                    Assert.Equal(patients.Order(StringComparer.Ordinal), store["Patient"].Keys.Order(StringComparer.Ordinal));
                    List<string> observations = ["preferred-observation", "supplement-observation-" + precedence];
                    if (examples)
                    {
                        observations.Add("example-observation");
                    }
                    Assert.Equal(observations.Order(StringComparer.Ordinal), store["Observation"].Keys.Order(StringComparer.Ordinal));
                    AssertLoadedResource(store, bindings, version, "Patient", "preferred", "patient");
                    AssertLoadedResource(store, bindings, version, "Observation", "preferred-observation", "observation", "preferred");
                    AssertLoadedResource(store, bindings, version, "Observation", "supplement-observation-" + precedence, "observation");
                    AssertAuthoredPatient(store, bindings, "ordered", "Newest", false, resourceVersion: "2");
                    AssertAuthoredPatient(store, bindings, "supplement-" + precedence, "Supplement " + precedence, true);
                    if (examples)
                    {
                        AssertAuthoredPatient(store, bindings, "example", "Nested example", true);
                        AssertLoadedResource(store, bindings, version, "Observation", "example-observation", "observation");
                    }
                    // Outside siblings are preserved, but the manager still loads from /package.
                    Assert.False(store["Patient"].ContainsKey("outside"));
                    Assert.False(store["Patient"].ContainsKey("umbrella"));
                }
                IFhirStore target = ((IFhirStoreManager)manager)[targetName];
                IFhirStore sibling = ((IFhirStoreManager)manager)[siblingName];
                Assert.True(target.PerformInteraction(new(target, "DELETE", "Patient/preferred"), out FhirResponseContext deleted, serializeReturn: true));
                Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
                Assert.False(target["Patient"].ContainsKey("preferred"));
                AssertLoadedResource(sibling, bindings, version, "Patient", "preferred", "patient", bindingMode: CompatibilityBindingMode.Reuse);
                Assert.False(sibling["Patient"].ContainsKey("tenant-local"));
                AssertSnapshot(distributedBefore, preferredRoot);
            }
            finally
            {
                await StopAndDispose(manager);
            }
        }
    }

    /// <summary>Raw independently seeded trees survive replacement, targeted deletion/reinstallation, and snapshot rollback.</summary>
    [Theory]
    [MemberData(nameof(VersionCases))]
    public async Task PopulatedCacheDeletionReinstallationAndRollbackPreserveNeighbors(string label, FhirReleases.FhirSequenceCodes version)
    {
        using LocalPackageFixture files = new();
        _output.WriteLine($"{label}; populated legacy tree/deletion/reinstall/snapshot rollback; owned root: {files.Root}");
        string name = "example.test.rollback." + LocalPackageFixture.VersionName(version);
        PackageReference requested = new("legacy-scope", name, "1.0.0");
        PackageReference sameNameNeighbor = new(name, "2.0.0");
        PackageReference ciNeighbor = new(FhirCiClient.FhirCiScope, name + ".ci", "current$feature");
        string originalManifest = AuthoredManifest(name, "1.0.0", version);
        WritePinnedCache(files.CacheDirectory, requested, originalManifest, new Dictionary<string, string>
        {
            ["Patient-original.json"] = AuthoredPatientJson("original", "Before replacement", true),
            ["Observation-original.xml"] = LocalPackageFixture.WithId(CompatibilityReplay.LoadFixture(version, "observation.xml"), "original-observation"),
        });
        WritePinnedCache(files.CacheDirectory, sameNameNeighbor, AuthoredManifest(name, "2.0.0", version), new Dictionary<string, string>
        {
            ["Patient-neighbor.xml"] = AuthoredPatientXml("neighbor", "Same name other version", false),
        });
        WritePinnedCache(files.CacheDirectory, ciNeighbor, CiManifest(version, ciNeighbor.Name!, replacement: true), new Dictionary<string, string>
        {
            ["Patient-ci.json"] = AuthoredPatientJson("ci-neighbor", "Tagged neighbor", true),
        });
        string affectedRoot = Path.Combine(files.CacheDirectory, name + "#1.0.0");
        string content = Path.Combine(affectedRoot, "package");
        Directory.CreateDirectory(Path.Combine(files.CacheDirectory, "packages", "empty-neighbor"));
        Directory.CreateDirectory(Path.Combine(affectedRoot, "examples", "empty"));
        Directory.CreateDirectory(Path.Combine(affectedRoot, "package", "old-empty"));
        File.WriteAllBytes(Path.Combine(affectedRoot, "parent-metadata.bin"), [0, 255, 128, 10]);
        File.WriteAllBytes(Path.Combine(affectedRoot, "examples", "Patient-example.xml"), Encoding.UTF8.GetBytes(AuthoredPatientXml("old-example", "Old outside example", true)));
        File.WriteAllBytes(Path.Combine(LegacyContentPath(files.CacheDirectory, ciNeighbor), "authored.bin"), [1, 2, 0, 255]);
        (Dictionary<string, byte[]> files, string[] directories) original = Snapshot(files.CacheDirectory);
        string backup = files.CreateDirectory("affected-backup");
        CopyExportTree(affectedRoot, backup);
        AssertSnapshot(Snapshot(affectedRoot), backup);
        DiskPackageCache cache = new(files.CacheDirectory);
        using (FhirPackageService offline = PackageService(files.Configuration()))
        {
            offline.Init();
            Assert.Equal(requested, Assert.Single(await offline.InstallPackages([name + "#1.0.0"], [], [version]).WaitAsync(LocalHttpFixture.Deadline)));
            Assert.Equal(content, offline.GetPackageContentDirectory(requested));
            Assert.Equal(content, cache.PackageContentFolder(new("different-scope", name, "1.0.0")));
            Assert.Equal(new[] { version }, await offline.InstalledPackageFhirVersions(requested));
            AssertSnapshot(original, files.CacheDirectory);
        }

        JsonObject replacementJson = JsonNode.Parse(AuthoredManifest(name, "1.0.0", version))!.AsObject();
        replacementJson["description"] = "Replacement pinned raw bytes: café";
        string replacementManifest = replacementJson.ToJsonString();
        Assert.NotEqual(originalManifest, replacementManifest);
        Dictionary<string, byte[]> distribution = new(StringComparer.Ordinal)
        {
            ["package/package.json"] = Encoding.UTF8.GetBytes(replacementManifest),
            ["package/Patient-reinstalled.json"] = Encoding.UTF8.GetBytes(AuthoredPatientJson("reinstalled", "Replacement café", false)),
            ["package/Observation-reinstalled.xml"] = Encoding.UTF8.GetBytes(LocalPackageFixture.WithId(
                CompatibilityReplay.LoadFixture(version, "observation.xml"), "reinstalled-observation")),
            ["examples/Patient-example.xml"] = Encoding.UTF8.GetBytes(AuthoredPatientXml("new-example", "New outside example", false)),
            ["siblings/nested/authored.bin"] = [255, 0, 128, 3],
        };
        byte[] archive = DistributedArchive(distribution);
        await cache.Install(requested, archive).WaitAsync(LocalHttpFixture.Deadline);
        string affectedPrefix = name + "#1.0.0" + Path.DirectorySeparatorChar;
        Dictionary<string, byte[]> expectedFiles = original.files.Where(file => !file.Key.StartsWith(affectedPrefix + "package" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            .ToDictionary(file => file.Key, file => file.Value, StringComparer.Ordinal);
        foreach (KeyValuePair<string, byte[]> file in distribution)
        {
            expectedFiles[affectedPrefix + file.Key.Replace('/', Path.DirectorySeparatorChar)] = file.Value;
        }
        string[] expectedDirectories = original.directories.Where(path => path != affectedPrefix + "package" &&
            !path.StartsWith(affectedPrefix + "package" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            .Concat(new[] { "package", "empty-distributed-directory", "siblings", Path.Combine("siblings", "nested") }.Select(path => affectedPrefix + path))
            .Order().ToArray();
        (Dictionary<string, byte[]> files, string[] directories) replacement = (expectedFiles, expectedDirectories);
        AssertSnapshot(replacement, files.CacheDirectory);
        Assert.False(File.Exists(Path.Combine(content, "Patient-original.json")));
        Assert.False(Directory.Exists(Path.Combine(content, "old-empty")));
        AssertAuthor(Assert.IsType<PackageManifest>(await cache.ReadManifestEx(requested)), "Synthetic Package Author", "author@example.test", "https://example.test/sdk6");
        await Assert.ThrowsAnyAsync<Exception>(() => cache.Install(requested, Encoding.UTF8.GetBytes("invalid gzip")).WaitAsync(LocalHttpFixture.Deadline));
        AssertSnapshot(replacement, files.CacheDirectory);

        using (FhirPackageService deletion = PackageService(files.Configuration()))
        {
            deletion.Init();
            deletion.DeletePackage(name + "#1.0.0");
            Assert.False(await cache.IsInstalled(requested));
            Assert.Null(await cache.ReadManifestEx(requested));
            Assert.Equal(content, deletion.GetPackageContentDirectory(requested));
            Assert.Null(await deletion.InstalledPackageFhirVersions(requested));
            (Dictionary<string, byte[]> files, string[] directories) deleted = (
                expectedFiles.Where(file => !file.Key.StartsWith(affectedPrefix + "package" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                    .ToDictionary(file => file.Key, file => file.Value, StringComparer.Ordinal),
                expectedDirectories.Where(path => path != affectedPrefix + "package" &&
                    !path.StartsWith(affectedPrefix + "package" + Path.DirectorySeparatorChar, StringComparison.Ordinal)).ToArray());
            AssertSnapshot(deleted, files.CacheDirectory);
            deletion.DeletePackage(requested.Moniker);
            AssertSnapshot(deleted, files.CacheDirectory);
        }
        await using (LocalHttpFixture registry = await LocalHttpFixture.StartAsync())
        {
            LocalHttpFixture.ExpectedRequest download = registry.Expect("GET", "/" + name + "/1.0.0",
                new LocalHttpFixture.RawResponse(HttpStatusCode.OK, "application/octet-stream", archive));
            using FhirPackageService reinstall = PackageService(files.Configuration(registry.Address.AbsoluteUri.TrimEnd('/')));
            reinstall.Init();
            PackageReference installed = Assert.Single(await reinstall.InstallPackages([requested.Moniker], [], [version]).WaitAsync(LocalHttpFixture.Deadline));
            Assert.Equal(requested, installed);
            Assert.Equal(content, reinstall.GetPackageContentDirectory(installed));
            AssertSnapshot(replacement, files.CacheDirectory);
            await AssertRoutes(registry, [download]);
        }
        using (FhirPackageService reopened = PackageService(files.Configuration()))
        {
            reopened.Init();
            Assert.Equal(requested, Assert.Single(await reopened.InstallPackages([name + "#1.0.0"], [], [version]).WaitAsync(LocalHttpFixture.Deadline)));
            Assert.Equal(content, reopened.GetPackageContentDirectory(requested));
            Assert.Equal(new[] { version }, await reopened.InstalledPackageFhirVersions(requested));
            Assert.True(await cache.IsInstalled(sameNameNeighbor));
            Assert.True(await cache.IsInstalled(ciNeighbor));
            AssertSnapshot(replacement, files.CacheDirectory);
        }

        // A source revert cannot restore deleted content: restore only the independently backed-up identity.
        Directory.Delete(affectedRoot, recursive: true);
        CopyExportTree(backup, affectedRoot);
        AssertSnapshot(original, files.CacheDirectory);
        using (FhirPackageService rollback = PackageService(files.Configuration()))
        {
            rollback.Init();
            Assert.Equal(requested, Assert.Single(await rollback.InstallPackages([requested.Moniker], [], [version]).WaitAsync(LocalHttpFixture.Deadline)));
            Assert.Equal(content, rollback.GetPackageContentDirectory(requested));
            Assert.Equal(originalManifest, File.ReadAllText(Path.Combine(content, "package.json")));
            Assert.Equal(new[] { version }, await rollback.InstalledPackageFhirVersions(requested));
            AssertSnapshot(original, files.CacheDirectory);
        }
        _output.WriteLine("Snapshot rollback validates the legacy seam, not original-supplier or precompiled-consumer interoperability.");
    }

    private static string AuthoredManifest(string name, string version, params FhirReleases.FhirSequenceCodes[] sequences)
    {
        JsonObject manifest = JsonNode.Parse(LocalPackageFixture.Manifest(sequences[0]))!.AsObject();
        manifest["name"] = name;
        manifest["version"] = version;
        manifest["fhirVersions"] = new JsonArray(sequences.Select(sequence => (JsonNode?)JsonValue.Create(LocalPackageFixture.FhirVersion(sequence))).ToArray());
        return manifest.ToJsonString();
    }

    private static string AuthoredPatientJson(string id, string family, bool active) =>
        $$$"""{"resourceType":"Patient","id":"{{{id}}}","active":{{{(active ? "true" : "false")}}},"name":[{"family":"{{{family}}}"}]}""";

    private static string AuthoredPatientXml(string id, string family, bool active) =>
        $$$"""<Patient xmlns="http://hl7.org/fhir"><id value="{{{id}}}"/><active value="{{{(active ? "true" : "false")}}}"/><name><family value="{{{family}}}"/></name></Patient>""";

    private static void AssertAuthoredPatient(IFhirStore store, CompatibilityBindings bindings, string id, string family, bool active, string resourceVersion = "1")
    {
        GeneratedResourceExpectation binding = new(store.Config.ControllerName + "/" + id, "Patient", resourceVersion, id);
        foreach (CompatibilityFormat format in new[] { CompatibilityFormat.Json, CompatibilityFormat.Xml })
        {
            FhirRequestContext request = new(store, "GET", "Patient/" + id) { DestinationFormat = WireContractAssertions.MediaType(format) };
            Assert.True(store.PerformInteraction(request, out FhirResponseContext response, serializeReturn: true));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("Patient", response.ResourceType);
            Assert.Equal(id, response.Id);
            string expected = format == CompatibilityFormat.Json ? AuthoredPatientJson(id, family, active) : AuthoredPatientXml(id, family, active);
            WireContractAssertions.Body(new ResourceBodyExpectation(expected, binding), response.SerializedResource, format, store.Config.BaseUrl, bindings);
            binding = binding with { Mode = CompatibilityBindingMode.Reuse };
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
        string resourceType, string id, string fixtureName, string? patientId = null,
        CompatibilityBindingMode bindingMode = CompatibilityBindingMode.Capture)
    {
        GeneratedResourceExpectation binding = new(store.Config.ControllerName + "/" + id, resourceType, "1", id) { Mode = bindingMode };
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
