// <copyright file="CandleWebApplicationFactory.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using fhir.candle;
using fhir.candle.Services;
using FhirCandle.Configuration;
using FhirCandle.Models;
using FhirCandle.Storage;
using FhirCandle.Utils;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace fhir.candle.Tests.Controllers;

/// <summary>
/// Spins up a minimal in-process <see cref="WebApplication"/> for testing
/// <c>FhirController</c> routing end-to-end, using <see cref="TestServer"/>
/// so requests can be issued via a real <see cref="HttpClient"/> without
/// binding a real port.
/// </summary>
public sealed class CandleWebApplicationFactory : IDisposable, IAsyncDisposable
{
    private readonly WebApplication _app;
    private readonly HttpClient _client;
    private readonly IFhirStore[] _stores;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="CandleWebApplicationFactory"/> class.
    /// </summary>
    public CandleWebApplicationFactory()
        : this(
        [
            new TenantConfiguration
            {
                FhirVersion = FhirReleases.FhirSequenceCodes.R4,
                ControllerName = "r4",
                BaseUrl = "http://localhost/fhir/r4",
                AllowExistingId = true,
                AllowCreateAsUpdate = true,
            },
        ])
    {
    }

    /// <summary>Creates an isolated host with explicit tenants and optional controlled loading inputs.</summary>
    /// <param name="tenants">The tenants owned by this host; configurations are copied before initialization.</param>
    /// <param name="configuration">Optional test-controlled configuration. Caller-owned paths are not deleted.</param>
    internal CandleWebApplicationFactory(
        IEnumerable<TenantConfiguration> tenants,
        CandleConfig? configuration = null)
    {
        Dictionary<string, TenantConfiguration> ownedTenants = tenants.ToDictionary(
            tenant => tenant.ControllerName,
            CopyTenant,
            StringComparer.Ordinal);

        OwnedDirectory = Path.Combine(Path.GetTempPath(), $"fhir-candle-compatibility-{Guid.NewGuid():N}");
        string sourceDirectory = Path.Combine(OwnedDirectory, "source");
        string cacheDirectory = Path.Combine(OwnedDirectory, "cache");
        Directory.CreateDirectory(sourceDirectory);
        Directory.CreateDirectory(cacheDirectory);

        CandleConfig config = (configuration ?? new CandleConfig()) with
        {
            PublicUrl = configuration?.PublicUrl is { Length: > 0 } publicUrl ? publicUrl : "http://localhost",
            ListenPort = 0,
            SourceDirectory = configuration?.SourceDirectory ?? sourceDirectory,
            FhirCacheDirectory = configuration?.FhirCacheDirectory ?? cacheDirectory,
            UseOfficialRegistries = false,
            AdditionalFhirRegistryUrls = configuration?.AdditionalFhirRegistryUrls.ToArray() ?? [],
            AdditionalNpmRegistryUrls = configuration?.AdditionalNpmRegistryUrls.ToArray() ?? [],
            PublishedPackages = configuration?.PublishedPackages.ToArray() ?? [],
            CiPackages = configuration?.CiPackages.ToArray() ?? [],
            ReferenceImplementation = configuration?.ReferenceImplementation,
            TenantsR4 = TenantNames(FhirReleases.FhirSequenceCodes.R4),
            TenantsR4B = TenantNames(FhirReleases.FhirSequenceCodes.R4B),
            TenantsR5 = TenantNames(FhirReleases.FhirSequenceCodes.R5),
            TenantsR6 = [],
        };

        WebApplication? app = null;
        IFhirStore[] stores = [];
        try
        {
            if (string.IsNullOrEmpty(config.SourceDirectory) || !Directory.Exists(config.SourceDirectory))
            {
                throw new ArgumentException("An explicit source directory must exist; ambient fhirData is not a test input.", nameof(configuration));
            }

            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            app = Program.BuildAppForTesting(builder, config, ownedTenants);
            stores = app.Services.GetRequiredService<IFhirStoreManager>().Values.ToArray();
            app.StartAsync().GetAwaiter().GetResult();
            _client = app.GetTestServer().CreateClient();
            _client.BaseAddress = new Uri(config.PublicUrl.TrimEnd('/') + "/");
            _app = app;
            _stores = stores;
        }
        catch
        {
            foreach (IFhirStore store in stores)
            {
                store.Dispose();
            }

            if (app is not null)
            {
                ((IAsyncDisposable)app).DisposeAsync().AsTask().GetAwaiter().GetResult();
            }

            Directory.Delete(OwnedDirectory, recursive: true);
            throw;
        }

        string[] TenantNames(FhirReleases.FhirSequenceCodes version) =>
            ownedTenants.Values.Where(tenant => tenant.FhirVersion == version).Select(tenant => tenant.ControllerName).ToArray();
    }

    /// <summary>Gets the HttpClient backed by the in-process test server.</summary>
    public HttpClient Client => _client;

    /// <summary>Gets this application's services, not process-static service instances.</summary>
    public IServiceProvider Services => _app.Services;

    /// <summary>Gets the temporary root owned by this factory and removed after app disposal.</summary>
    public string OwnedDirectory { get; }

    /// <inheritdoc />
    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _client.Dispose();
        try
        {
            await _app.StopAsync();
        }
        finally
        {
            try
            {
                // The manager owns these stores but its current Dispose does not release them.
                foreach (IFhirStore store in _stores)
                {
                    store.Dispose();
                }
            }
            finally
            {
                await ((IAsyncDisposable)_app).DisposeAsync();
                Directory.Delete(OwnedDirectory, recursive: true);
            }
        }
    }

    private static TenantConfiguration CopyTenant(TenantConfiguration tenant)
    {
        TenantConfiguration copy = new()
        {
            FhirVersion = tenant.FhirVersion,
            ControllerName = tenant.ControllerName,
            BaseUrl = tenant.BaseUrl,
            SupportedResources = tenant.SupportedResources.ToArray(),
            SupportedFormats = tenant.SupportedFormats.ToArray(),
            LoadDirectory = tenant.LoadDirectory,
            ProtectLoadedContent = tenant.ProtectLoadedContent,
            MaxResourceCount = tenant.MaxResourceCount,
            MaxSubscriptionExpirationMinutes = tenant.MaxSubscriptionExpirationMinutes,
            SupportNotChanged = tenant.SupportNotChanged,
            SmartRequired = tenant.SmartRequired,
            SmartAllowed = tenant.SmartAllowed,
            AllowExistingId = tenant.AllowExistingId,
            AllowCreateAsUpdate = tenant.AllowCreateAsUpdate,
            Strict = tenant.Strict,
        };

        foreach (KeyValuePair<string, TenantConfiguration.FhirPackageInfo> package in tenant.FhirPackages)
        {
            copy.FhirPackages.Add(package.Key, package.Value);
        }

        return copy;
    }
}
