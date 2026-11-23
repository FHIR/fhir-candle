// <copyright file="SmartViewerSessionTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using System.Net;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using fhir.candle.Services;
using fhir.candle.Tests.Controllers;
using fhir.candle.Tests.Infrastructure;
using FhirCandle.Configuration;
using FhirCandle.Models;
using FhirCandle.Storage;
using FhirCandle.Utils;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace fhir.candle.Tests;

/// <summary>Checks the circuit boundary against the in-process REST controller.</summary>
[Collection(CompatibilityHostCollection.Name)]
public class SmartViewerSessionTests
{
    /// <summary>Caller inspection is tenant-bound, expires while idle and never repopulates from admin.</summary>
    [Fact]
    public async Task AdminAndCallerReadDifferentViews()
    {
        TenantConfiguration tenant = new()
        {
            ControllerName = "viewer",
            BaseUrl = "http://localhost/fhir/viewer",
            FhirVersion = FhirReleases.FhirSequenceCodes.R4,
            SmartAllowed = true,
            AllowCreateAsUpdate = true,
            AllowExistingId = true,
        };
        DirectoryInfo root = Directory.CreateTempSubdirectory("candle-viewer-");
        try
        {
            ManualTimeProvider clock = new(DateTimeOffset.UtcNow);
            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Services.AddSingleton<TimeProvider>(clock);
            builder.Services.AddScoped<SmartViewerSession>();
            CandleConfig config = new()
            {
                PublicUrl = "http://localhost",
                SourceDirectory = root.FullName,
                FhirCacheDirectory = root.FullName,
                UseOfficialRegistries = false,
                TenantsR4 = [tenant.ControllerName],
            };
            await using WebApplication app = Program.BuildAppForTesting(builder, config, new() { [tenant.ControllerName] = tenant });
            await app.StartAsync();
            using IFhirStore store = app.Services.GetRequiredService<IFhirStoreManager>()[tenant.ControllerName];
            ISmartAuthManager auth = app.Services.GetRequiredService<ISmartAuthManager>();
            using HttpClient rest = app.GetTestClient();
            using IServiceScope first = app.Services.CreateScope();
            using IServiceScope second = app.Services.CreateScope();
            SmartViewerSession session = first.ServiceProvider.GetRequiredService<SmartViewerSession>();
            SmartViewerSession peer = second.ServiceProvider.GetRequiredService<SmartViewerSession>();
            Assert.NotSame(peer, session);

            foreach (string id in new[] { "included", "excluded" })
            {
                FhirRequestContext ctx = new(store, "PUT", $"Patient/{id}")
                {
                    SourceContent = $$"""{"resourceType":"Patient","id":"{{id}}"}""",
                    SourceFormat = "application/fhir+json",
                };
                Assert.True(store.InstanceUpdate(ctx, out FhirResponseContext response), response.SerializedOutcome);
            }

            Assert.True(auth.RequestAuth(tenant.ControllerName, "127.0.0.1", "code", "viewer-test", "http://localhost/return",
                string.Empty, "patient/Patient.rs", string.Empty, tenant.BaseUrl, string.Empty, string.Empty, out _, out string key));
            Assert.True(auth.TryGetPendingAuthorization(tenant.ControllerName, key, out AuthorizationInfo consent));
            consent.UserId = "test-user";
            consent.LaunchPatient = "Patient/included";
            consent.Scopes["patient/Patient.rs"] = true;
            Assert.True(auth.TryUpdateAuth(tenant.ControllerName, key, consent));
            Assert.True(auth.TryCreateSmartResponse(tenant.ControllerName, consent.AuthCode, "viewer-test", string.Empty,
                string.Empty, out AuthorizationInfo.SmartResponse issued));
            rest.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", issued.AccessToken);

            SmartViewerSession.Request list = new(tenant.ControllerName, "Patient");
            SmartViewerSession.Request allowed = list with { Id = "included" };
            SmartViewerSession.Request denied = list with { Id = "excluded" };
            session.SelectTenant(list.Tenant);
            peer.SelectTenant(list.Tenant);
            await session.LoadAsync(list, SmartViewerSession.LoadAdmin);
            Assert.Equal(2, session.Display.Rows.Length);
            await peer.LoadAsync(denied, SmartViewerSession.LoadAdmin);
            Assert.Contains("excluded", peer.Display.Body);

            // An old admin completion cannot overwrite even a successful result in caller mode.
            TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
            using ManualResetEventSlim release = new();
            Task late = session.LoadAsync(denied, (s, r) =>
            {
                entered.SetResult();
                if (!release.Wait(TimeSpan.FromSeconds(30)))
                {
                    throw new TimeoutException();
                }
                return SmartViewerSession.LoadAdmin(s, r);
            });
            await entered.Task;
            int adminCalls = 0;
            Task Load(SmartViewerSession.Request request) => session.LoadAsync(request, (s, r) =>
            {
                Interlocked.Increment(ref adminCalls);
                return SmartViewerSession.LoadAdmin(s, r);
            });
            try
            {
                session.SelectMode(true);
                await Load(list);
                AssertDenied(session);
                session.SignIn(issued.AccessToken);
                await Load(new(list.Tenant));
                Assert.Contains(session.Display.Types, t => t.Name == "Patient");
                Assert.All(session.Display.Types, t => Assert.Null(t.Count));
                await Load(list);
                JsonNode? bundle = JsonNode.Parse(await rest.GetStringAsync($"{tenant.BaseUrl}/Patient"));
                Assert.Equal("included", Assert.Single(bundle!["entry"]!.AsArray())!["resource"]!["id"]!.GetValue<string>());
                Assert.Equal("included", Assert.Single(session.Display.Rows).Id);

                using HttpResponseMessage read = await rest.GetAsync($"{tenant.BaseUrl}/Patient/included");
                await Load(allowed);
                Assert.Equal(HttpStatusCode.OK, read.StatusCode);
                Assert.Equal(read.StatusCode, session.Display.Status);
                Assert.True(JsonNode.DeepEquals(JsonNode.Parse(await read.Content.ReadAsStringAsync()), JsonNode.Parse(session.Display.Body)));
            }
            finally
            {
                release.Set();
                await late;
            }
            Assert.Contains("included", session.Display.Body);
            Assert.Null(session.Display.AdminStore);

            using HttpResponseMessage rejected = await rest.GetAsync($"{tenant.BaseUrl}/Patient/excluded");
            await Load(denied);
            Assert.False(rejected.IsSuccessStatusCode);
            Assert.Equal(rejected.StatusCode, session.Display.Status);
            AssertEmpty(session);
            using HttpResponseMessage forbiddenSearch = await rest.GetAsync($"{tenant.BaseUrl}/Observation");
            await Load(list with { Type = "Observation" });
            Assert.Equal(HttpStatusCode.Unauthorized, forbiddenSearch.StatusCode);
            Assert.Equal(forbiddenSearch.StatusCode, session.Display.Status);
            AssertEmpty(session);

            await Load(allowed with { Format = "xml" });
            Assert.Contains("<Patient", session.Display.Body);
            Assert.True(auth.TrySmartRefresh(tenant.ControllerName, issued.RefreshToken, "viewer-test", out issued));
            await Load(allowed with { Format = "json" });
            AssertDenied(session);
            session.SignIn(issued.AccessToken);
            await Load(allowed);
            Assert.Contains("included", session.Display.Body);
            session.SignIn("not-issued");
            await Load(allowed with { Format = "json" });
            AssertDenied(session);

            session.SignIn(issued.AccessToken);
            await Load(list);
            session.SelectTenant("other");
            Assert.True(session.Smart);
            AssertEmpty(session);
            session.SelectTenant(tenant.ControllerName);
            await Load(allowed);
            AssertDenied(session);

            session.SignIn(issued.AccessToken);
            await Load(allowed);
            Assert.Contains("included", session.Display.Body);
            int expired = 0;
            session.Expired += () => expired++;
            clock.Advance(clock.NextDueTime!.Value - clock.GetUtcNow());
            Assert.Equal(1, clock.RunDueCallbacks());
            Assert.Equal(1, expired);
            AssertDenied(session);
            await Load(allowed with { Format = "xml" });
            AssertDenied(session);
            Assert.False(peer.Smart);
            Assert.Contains("excluded", peer.Display.Body);
            Assert.Equal(0, adminCalls);

            session.SelectMode(false);
            await Load(denied);
            Assert.Equal(1, adminCalls);
            Assert.Contains("excluded", session.Display.Body);
            await app.StopAsync();
        }
        finally
        {
            root.Delete(true);
        }
    }

    private static void AssertDenied(SmartViewerSession session)
    {
        Assert.True(session.Smart);
        Assert.Equal(HttpStatusCode.Unauthorized, session.Display.Status);
        AssertEmpty(session);
    }

    private static void AssertEmpty(SmartViewerSession session)
    {
        Assert.Null(session.Display.AdminStore);
        Assert.Empty(session.Display.Body);
        Assert.Empty(session.Display.Rows);
        Assert.Empty(session.Display.Types);
    }
}
