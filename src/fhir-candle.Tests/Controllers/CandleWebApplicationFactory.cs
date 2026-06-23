// <copyright file="CandleWebApplicationFactory.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using fhir.candle;
using FhirCandle.Configuration;
using FhirCandle.Models;
using FhirCandle.Utils;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;

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
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="CandleWebApplicationFactory"/> class.
    /// </summary>
    public CandleWebApplicationFactory()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        CandleConfig config = new()
        {
            PublicUrl = "http://localhost",
            ListenPort = 0,
            TenantsR4 = ["r4"],
            TenantsR4B = [],
            TenantsR5 = [],
            TenantsR6 = [],
            ProtectLoadedContent = false,
        };

        Dictionary<string, TenantConfiguration> tenants = new()
        {
            ["r4"] = new()
            {
                FhirVersion = FhirReleases.FhirSequenceCodes.R4,
                ControllerName = "r4",
                BaseUrl = "http://localhost/fhir/r4",
                AllowExistingId = true,
                AllowCreateAsUpdate = true,
            },
        };

        _app = Program.BuildAppForTesting(builder, config, tenants);
        _app.StartAsync().GetAwaiter().GetResult();

        TestServer server = _app.GetTestServer();
        _client = server.CreateClient();
        _client.BaseAddress = new Uri("http://localhost/");
    }

    /// <summary>Gets the HttpClient backed by the in-process test server.</summary>
    public HttpClient Client => _client;

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _client.Dispose();
        _app.StopAsync().GetAwaiter().GetResult();
        ((IAsyncDisposable)_app).DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _client.Dispose();
        await _app.StopAsync();
        await ((IAsyncDisposable)_app).DisposeAsync();
    }
}
