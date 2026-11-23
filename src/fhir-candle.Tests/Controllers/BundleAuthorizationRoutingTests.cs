// <copyright file="BundleAuthorizationRoutingTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

extern alias candleR4;
extern alias candleR4B;
extern alias candleR5;

using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using fhir.candle.Services;
using FhirCandle.Configuration;
using FhirCandle.Models;
using FhirCandle.Smart;
using FhirCandle.Storage;
using FhirCandle.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace fhir.candle.Tests.Controllers;

/// <summary>Exercises entry authorization through the real controller and its host-owned SMART manager.</summary>
[Collection(CompatibilityHostCollection.Name)]
public class BundleAuthorizationRoutingTests
{
    private const string JsonFormat = "application/fhir+json";
    private const string XmlFormat = "application/fhir+xml";
    private const string CollateralDiagnostic = "Entry not executed because transaction authorization failed";
    private static readonly XNamespace _fhir = "http://hl7.org/fhir";

    /// <summary>Each supported version once for direct backend requests.</summary>
    public static IEnumerable<object[]> VersionCases =>
        from FhirReleases.FhirSequenceCodes version in new[]
        {
            FhirReleases.FhirSequenceCodes.R4, FhirReleases.FhirSequenceCodes.R4B, FhirReleases.FhirSequenceCodes.R5,
        }
        select new object[] { version };

    /// <summary>All supported versions and both executable bundle types.</summary>
    public static IEnumerable<object[]> BundleCases =>
        from FhirReleases.FhirSequenceCodes version in new[]
        {
            FhirReleases.FhirSequenceCodes.R4, FhirReleases.FhirSequenceCodes.R4B, FhirReleases.FhirSequenceCodes.R5,
        }
        from string kind in new[] { "batch", "transaction" }
        select new object[] { version, kind };

    /// <summary>Read-only authorization must reject each write through both HTTP boundaries.</summary>
    public static IEnumerable<object[]> RestrictedWriteCases =>
        from object[] bundle in BundleCases
        from string method in new[] { "DELETE", "POST", "PUT" }
        select new object[] { bundle[0], bundle[1], method };

    /// <summary>Transactions gate direct late reads and descendants inside a nested batch.</summary>
    public static IEnumerable<object[]> TransactionCases =>
        from object[] bundle in BundleCases
        where (string)bundle[1] == "transaction"
        from bool nested in new[] { false, true }
        select new object[] { bundle[0], nested };

    /// <summary>Both absent and unrecognized bearer headers resolve to null authorization.</summary>
    public static IEnumerable<object[]> MissingAuthorizationCases =>
        from object[] bundle in BundleCases
        from bool unknownBearer in new[] { false, true }
        select new object[] { bundle[0], bundle[1], unknownBearer };

    /// <summary>An exchanged backend token permits only its scoped reads and searches without changing the store.</summary>
    [Theory]
    [MemberData(nameof(VersionCases))]
    public async Task BackendAssertionTokenAuthorizesOnlyRequestedFhirRequests(FhirReleases.FhirSequenceCodes version)
    {
        using TestHost host = new(version);
        IFhirStore store = host.Store;
        Seed(store);
        MutationWatch watch = new(store);
        string[] before = Fingerprint(store);
        (JsonWebKey privateKey, JsonWebKey publicKey) = CreateAssertionKeys();
        string clientId = RegisterAssertionClient(host.AuthManager, publicKey);
        (string token, AuthorizationInfo authorization) = await ExchangeAssertion(
            host, clientId, privateKey, "system/Patient.rs");

        Assert.False(string.IsNullOrEmpty(authorization.Key));
        Assert.True(Guid.TryParse(authorization.Key, out Guid key));
        Assert.NotEqual(Guid.Empty, key);
        Assert.Empty(authorization.UserId);
        Assert.Empty(authorization.LaunchPatient);
        Assert.Empty(authorization.PatientScopes);
        Assert.DoesNotContain("*.*", authorization.UserScopes);
        Assert.DoesNotContain("*.*", authorization.PatientScopes);
        Assert.Equal(new[] { "Patient.r", "Patient.s" }, authorization.UserScopes.OrderBy(s => s, StringComparer.Ordinal));
        AssertUnchanged(store, before, watch);

        JsonNode seededPatient = JsonNode.Parse(Serialize(store, store["Patient"]["seed"]))!;
        using HttpResponseMessage read = await SendAsync(host, store, "GET", "Patient/seed", token);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.Equal(JsonFormat, read.Content.Headers.ContentType?.MediaType);
        Assert.True(JsonNode.DeepEquals(seededPatient, JsonNode.Parse(await read.Content.ReadAsStringAsync())));
        AssertUnchanged(store, before, watch);

        using HttpResponseMessage search = await SendAsync(host, store, "GET", "Patient", token);
        Assert.Equal(HttpStatusCode.OK, search.StatusCode);
        Assert.Equal(JsonFormat, search.Content.Headers.ContentType?.MediaType);
        JsonObject bundle = JsonNode.Parse(await search.Content.ReadAsStringAsync())!.AsObject();
        Assert.Equal("Bundle", bundle["resourceType"]!.GetValue<string>());
        Assert.Equal("searchset", bundle["type"]!.GetValue<string>());
        JsonObject match = Assert.Single(bundle["entry"]!.AsArray())!.AsObject();
        Assert.Equal("match", match["search"]!["mode"]!.GetValue<string>());
        Assert.True(JsonNode.DeepEquals(seededPatient, match["resource"]));
        AssertUnchanged(store, before, watch);

        using HttpResponseMessage unrelated = await SendAsync(host, store, "GET", "Observation", token);
        Assert.Equal(HttpStatusCode.Unauthorized, unrelated.StatusCode);
        AssertUnchanged(store, before, watch);

        using HttpResponseMessage update = await SendAsync(
            host, store, "PUT", "Patient/seed", token, Patient("seed", "MustNotPersist").ToJsonString());
        Assert.Equal(HttpStatusCode.Unauthorized, update.StatusCode);
        AssertUnchanged(store, before, watch);

        using HttpResponseMessage delete = await SendAsync(host, store, "DELETE", "Patient/seed", token);
        Assert.Equal(HttpStatusCode.Unauthorized, delete.StatusCode);
        AssertUnchanged(store, before, watch);
    }

    /// <summary>Backend normalization retains exact grants and ordinary interaction boundaries for each supported syntax.</summary>
    [Theory]
    [MemberData(nameof(VersionCases))]
    public async Task BackendAssertionScopesNormalizeWithoutBroadeningPermissions(FhirReleases.FhirSequenceCodes version)
    {
        using TestHost host = new(version);
        IFhirStore store = host.Store;
        Seed(store);
        MutationWatch watch = new(store);
        string[] before = Fingerprint(store);
        (JsonWebKey privateKey, JsonWebKey publicKey) = CreateAssertionKeys();
        string clientId = RegisterAssertionClient(host.AuthManager, publicKey);
        (string Scope, string[] Grants, string PatientActions, string ObservationActions)[] cases =
        [
            ("system/Patient.rs", ["Patient.r", "Patient.s"], "rs", string.Empty),
            ("system/Patient.sr", ["Patient.r", "Patient.s"], "rs", string.Empty),
            ("system/Patient.read", ["Patient.r", "Patient.s"], "rs", string.Empty),
            ("system/Patient.write", ["Patient.c", "Patient.u", "Patient.d"], "cud", string.Empty),
            ("system/Patient.*", ["Patient.c", "Patient.r", "Patient.u", "Patient.d", "Patient.s"], "cruds", string.Empty),
            ("system/Patient.cruds", ["Patient.c", "Patient.r", "Patient.u", "Patient.d", "Patient.s"], "cruds", string.Empty),
            ("system/Patient.sdcur", ["Patient.c", "Patient.r", "Patient.u", "Patient.d", "Patient.s"], "cruds", string.Empty),
            ("system/Patient.c", ["Patient.c"], "c", string.Empty),
            ("system/Patient.r", ["Patient.r"], "r", string.Empty),
            ("system/Patient.u", ["Patient.u"], "u", string.Empty),
            ("system/Patient.d", ["Patient.d"], "d", string.Empty),
            ("system/Patient.s", ["Patient.s"], "s", string.Empty),
            ("system/*.read", ["*.r", "*.s"], "rs", "rs"),
            ("system/*.rs", ["*.r", "*.s"], "rs", "rs"),
            ("system/*.write", ["*.c", "*.u", "*.d"], "cud", "cud"),
            ("system/*.*", ["*.c", "*.r", "*.u", "*.d", "*.s"], "cruds", "cruds"),
            ("system/Patient.r system/Observation.s", ["Patient.r", "Observation.s"], "r", "s"),
            ("openid profile", [], string.Empty, string.Empty),
        ];
        Assert.Equal(18, cases.Length);
        HashSet<string> issuedKeys = new(StringComparer.Ordinal);
        HashSet<string> issuedTokens = new(StringComparer.Ordinal);
        foreach ((string scope, string[] grants, string patientActions, string observationActions) in cases)
        {
            (string token, AuthorizationInfo authorization) = await ExchangeAssertion(host, clientId, privateKey, scope);
            Assert.True(issuedKeys.Add(authorization.Key));
            Assert.True(issuedTokens.Add(token));
            AssertBackendIdentity(authorization);
            AssertEffectiveScopes(authorization, grants, []);
            AssertOrdinaryPermissions(host, authorization, patientActions, observationActions);
            if (scope == "openid profile")
            {
                foreach (string url in new[] { "Patient/seed", "Patient" })
                {
                    using HttpResponseMessage denied = await SendAsync(host, store, "GET", url, token);
                    Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
                    AssertUnchanged(store, before, watch);
                }
            }

            AssertUnchanged(store, before, watch);
        }
    }

    /// <summary>A single form scope value keeps grants on their requested resources through real read and search requests.</summary>
    [Theory]
    [MemberData(nameof(VersionCases))]
    public async Task BackendAssertionSpaceDelimitedScopesPreserveResourceActionBoundaries(FhirReleases.FhirSequenceCodes version)
    {
        using TestHost host = new(version);
        IFhirStore store = host.Store;
        Seed(store);
        MutationWatch watch = new(store);
        string[] before = Fingerprint(store);
        (JsonWebKey privateKey, JsonWebKey publicKey) = CreateAssertionKeys();
        string clientId = RegisterAssertionClient(host.AuthManager, publicKey);
        (string token, AuthorizationInfo authorization) = await ExchangeAssertion(
            host, clientId, privateKey, "system/Patient.r system/Observation.s");
        AssertBackendIdentity(authorization);
        AssertEffectiveScopes(authorization, ["Patient.r", "Observation.s"], []);
        AssertOrdinaryPermissions(host, authorization, "r", "s");

        await AssertSeededRead(host, store, "Patient", token);
        AssertUnchanged(store, before, watch);
        await AssertSeededSearch(host, store, "Observation", token);
        AssertUnchanged(store, before, watch);
        foreach (string url in new[] { "Patient", "Observation/seed" })
        {
            using HttpResponseMessage denied = await SendAsync(host, store, "GET", url, token);
            Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
            AssertUnchanged(store, before, watch);
        }
    }

    /// <summary>Backend exchanges retain user and patient contexts without inventing a launch patient.</summary>
    [Theory]
    [MemberData(nameof(VersionCases))]
    public async Task BackendAssertionPreservesUserAndPatientScopeContexts(FhirReleases.FhirSequenceCodes version)
    {
        using TestHost host = new(version);
        (JsonWebKey privateKey, JsonWebKey publicKey) = CreateAssertionKeys();
        string clientId = RegisterAssertionClient(host.AuthManager, publicKey);
        const string Scope = "user/Patient.r patient/Observation.s system/Patient.s";
        (_, AuthorizationInfo authorization) = await ExchangeAssertion(host, clientId, privateKey, Scope);
        AssertBackendIdentity(authorization);
        Assert.Equal(Scope, authorization.RequestParameters.Scope);
        Assert.Equal(
            Scope.Split(' ').OrderBy(s => s, StringComparer.Ordinal),
            authorization.Scopes.Keys.OrderBy(s => s, StringComparer.Ordinal));
        Assert.All(authorization.Scopes.Values, approved => Assert.True(approved));
        AssertEffectiveScopes(authorization, ["Patient.r", "Patient.s"], ["Observation.s"]);
        // These are request-level decisions, not assertions about patient-compartment search contents.
        AssertOrdinaryPermissions(host, authorization, "rs", "s");
    }

    /// <summary>Interactive consent still controls user/patient grants and does not opt into system scopes.</summary>
    [Theory]
    [MemberData(nameof(VersionCases))]
    public async Task AuthorizationCodePermissionsRemainContextAndConsentBound(FhirReleases.FhirSequenceCodes version)
    {
        using TestHost host = new(version);
        IFhirStore store = host.Store;
        Seed(store);
        MutationWatch watch = new(store);
        string[] before = Fingerprint(store);
        const string Scope = "launch/patient user/Patient.rs patient/Observation.rs user/Patient.write patient/Observation.write";
        (AuthorizationInfo authorization, AuthorizationInfo.SmartResponse response) = ExchangeAuthorizationCode(
            host, Scope, "Patient/seed", "user/Patient.write", "patient/Observation.write");
        Assert.Equal(Scope, authorization.RequestParameters.Scope);
        Assert.Equal("Patient/seed", authorization.LaunchPatient);
        Assert.Equal("Patient/seed", response.PatientId);
        Assert.True(authorization.Scopes["user/Patient.rs"]);
        Assert.True(authorization.Scopes["patient/Observation.rs"]);
        Assert.False(authorization.Scopes["user/Patient.write"]);
        Assert.False(authorization.Scopes["patient/Observation.write"]);
        Assert.Equal(
            new[] { "launch/patient", "patient/Observation.rs", "user/Patient.rs" },
            response.Scopes.Split(' ').OrderBy(s => s, StringComparer.Ordinal));
        AssertEffectiveScopes(authorization, ["Patient.r", "Patient.s"], ["Observation.r", "Observation.s"]);
        AssertOrdinaryPermissions(host, authorization, "rs", "rs");
        AssertUnchanged(store, before, watch);

        (AuthorizationInfo systemOnly, AuthorizationInfo.SmartResponse systemResponse) = ExchangeAuthorizationCode(
            host, "system/Patient.rs", string.Empty);
        Assert.NotEqual(authorization.Key, systemOnly.Key);
        Assert.Equal("system/Patient.rs", systemResponse.Scopes);
        Assert.True(systemOnly.Scopes["system/Patient.rs"]);
        Assert.Empty(systemOnly.LaunchPatient);
        AssertEffectiveScopes(systemOnly, [], []);
        AssertOrdinaryPermissions(host, systemOnly, string.Empty, string.Empty);
        foreach (string url in new[] { "Patient/seed", "Patient" })
        {
            using HttpResponseMessage denied = await SendAsync(host, store, "GET", url, systemResponse.AccessToken);
            Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
            AssertUnchanged(store, before, watch);
        }
    }

    /// <summary>The same exchanged bearer token cannot resolve or read a distinct required tenant.</summary>
    [Theory]
    [MemberData(nameof(VersionCases))]
    public async Task BackendAssertionTokenRemainsTenantBound(FhirReleases.FhirSequenceCodes version)
    {
        using TestHost host = new(version, includePeerRequired: true);
        IFhirStore store = host.Store;
        IFhirStore peer = host.PeerRequiredStore;
        Assert.NotSame(store, peer);
        Assert.NotEqual(store.Config.ControllerName, peer.Config.ControllerName);
        Assert.NotEqual(store.Config.BaseUrl, peer.Config.BaseUrl);
        Assert.True(store.Config.SmartRequired);
        Assert.True(peer.Config.SmartRequired);
        Seed(store);
        Seed(peer);
        MutationWatch watch = new(store);
        MutationWatch peerWatch = new(peer);
        string[] before = Fingerprint(store);
        string[] peerBefore = Fingerprint(peer);
        (JsonWebKey privateKey, JsonWebKey publicKey) = CreateAssertionKeys();
        string clientId = RegisterAssertionClient(host.AuthManager, publicKey);
        (string token, AuthorizationInfo authorization) = await ExchangeAssertion(host, clientId, privateKey, "system/Patient.rs");
        AssertBackendIdentity(authorization);
        AssertEffectiveScopes(authorization, ["Patient.r", "Patient.s"], []);
        await AssertSeededRead(host, store, "Patient", token);
        AssertUnchanged(store, before, watch);
        AssertUnchanged(peer, peerBefore, peerWatch);

        Assert.Equal(GrantStatus.NotCurrent, host.AuthManager.ResolveAccessToken(peer.Config.ControllerName, token, out _));
        using HttpResponseMessage denied = await SendAsync(host, peer, "GET", "Patient/seed", token);
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        AssertUnchanged(store, before, watch);
        AssertUnchanged(peer, peerBefore, peerWatch);
    }

    /// <summary>Limited backend grants leave the initialized administrator and anonymous tenant policy intact.</summary>
    [Theory]
    [MemberData(nameof(VersionCases))]
    public async Task BackendAssertionPermissionsDoNotReplaceAdministrativeOrTenantPolicy(FhirReleases.FhirSequenceCodes version)
    {
        using TestHost host = new(version, includeOptional: true, includeDisabled: true);
        IFhirStore store = host.Store;
        IFhirStore optional = host.OptionalStore;
        IFhirStore disabled = host.DisabledStore;
        Assert.True(store.Config.SmartRequired);
        Assert.False(store.Config.SmartAllowed);
        Assert.False(optional.Config.SmartRequired);
        Assert.True(optional.Config.SmartAllowed);
        Assert.False(disabled.Config.SmartRequired);
        Assert.False(disabled.Config.SmartAllowed);
        foreach (IFhirStore tenantStore in new[] { store, optional, disabled })
        {
            Seed(tenantStore);
            // Materialize the store's lazy CapabilityStatement during setup, before observing read-only controls.
            using HttpResponseMessage initialized = await SendAsync(host, tenantStore, "GET", "metadata", null);
            Assert.Equal(HttpStatusCode.OK, initialized.StatusCode);
        }
        (IFhirStore Store, string[] Before, MutationWatch Watch)[] snapshots =
        [
            (store, Fingerprint(store), new(store)),
            (optional, Fingerprint(optional), new(optional)),
            (disabled, Fingerprint(disabled), new(disabled)),
        ];
        (JsonWebKey privateKey, JsonWebKey publicKey) = CreateAssertionKeys();
        string clientId = RegisterAssertionClient(host.AuthManager, publicKey);
        (string token, AuthorizationInfo authorization) = await ExchangeAssertion(host, clientId, privateKey, "system/Patient.rs");
        AssertBackendIdentity(authorization);
        AssertEffectiveScopes(authorization, ["Patient.r", "Patient.s"], []);
        foreach (string url in new[] { "Observation/seed", "Observation" })
        {
            using HttpResponseMessage denied = await SendAsync(host, store, "GET", url, token);
            Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        }

        Assert.True(host.AuthManager.TryGetSimulator(store.Config.ControllerName, out AuthorizationInfo administrator));
        Assert.Equal(Guid.Empty.ToString(), administrator.Key);
        Assert.Equal("administrator", administrator.UserId);
        AssertEffectiveScopes(administrator, ["*.*"], []);
        AuthorizationInfo.SmartResponse adminResponse = Assert.IsType<AuthorizationInfo.SmartResponse>(administrator.Response);
        Assert.False(string.IsNullOrEmpty(adminResponse.AccessToken));
        Assert.Equal(GrantStatus.Admitted, host.AuthManager.ResolveAccessToken(
            store.Config.ControllerName, adminResponse.AccessToken, out AuthorizationInfo resolvedAdmin));
        Assert.NotSame(administrator, resolvedAdmin);
        await AssertSeededRead(host, store, "Observation", adminResponse.AccessToken);
        await AssertSeededSearch(host, store, "Observation", adminResponse.AccessToken);
        foreach ((IFhirStore tenantStore, string[] before, MutationWatch watch) in snapshots)
        {
            AssertUnchanged(tenantStore, before, watch);
            if (tenantStore.Config.SmartRequired)
            {
                using HttpResponseMessage denied = await SendAsync(host, tenantStore, "GET", "Patient/seed", null);
                Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
            }
            else
            {
                await AssertSeededRead(host, tenantStore, "Patient", null);
            }
            AssertUnchanged(tenantStore, before, watch);

            using HttpResponseMessage capabilities = await SendAsync(host, tenantStore, "GET", "metadata", null);
            Assert.Equal(HttpStatusCode.OK, capabilities.StatusCode);
            Assert.Equal(JsonFormat, capabilities.Content.Headers.ContentType?.MediaType);
            Assert.Equal("CapabilityStatement",
                JsonNode.Parse(await capabilities.Content.ReadAsStringAsync())!["resourceType"]!.GetValue<string>());
            AssertUnchanged(tenantStore, before, watch);
        }
        Assert.Equal(Guid.Empty.ToString(), administrator.Key);
        AssertEffectiveScopes(administrator, ["*.*"], []);
    }

    /// <summary>The same effective permissions allow a direct read but reject direct and bundled writes.</summary>
    [Theory]
    [MemberData(nameof(RestrictedWriteCases))]
    public async Task DirectAndBundledRestrictedRequestsAgree(
        FhirReleases.FhirSequenceCodes version,
        string kind,
        string method)
    {
        using TestHost host = new(version);
        IFhirStore store = host.Store;
        Seed(store);
        MutationWatch watch = new(store);
        string[] before = Fingerprint(store);
        (AuthorizationInfo authorization, AuthorizationInfo.SmartResponse grant) = ExchangeAuthorizationCode(host, "user/Patient.r", string.Empty);
        string url = method == "POST" ? "Patient" : "Patient/seed";
        JsonObject? body = method == "DELETE" ? null : Patient(method == "POST" ? "attempt" : "seed", "MustNotPersist");
        Assert.True(host.AuthManager.IsAuthorized(Context(store, "GET", "Patient/seed", authorization)));
        Assert.False(host.AuthManager.IsAuthorized(Context(store, method, url, authorization)));

        using HttpResponseMessage read = await SendAsync(host, store, "GET", "Patient/seed", grant.AccessToken);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.Equal(JsonFormat, read.Content.Headers.ContentType?.MediaType);
        Assert.True(JsonNode.DeepEquals(
            JsonNode.Parse(Serialize(store, store["Patient"]["seed"])),
            JsonNode.Parse(await read.Content.ReadAsStringAsync())));
        AssertUnchanged(store, before, watch);

        using HttpResponseMessage direct = await SendAsync(host, store, method, url, grant.AccessToken, body?.ToJsonString());
        Assert.Equal(HttpStatusCode.Unauthorized, direct.StatusCode);
        AssertUnchanged(store, before, watch);

        JsonObject response = await SendJsonBundle(host, store, Bundle(kind, Entry("denied", method, url, body)), grant.AccessToken);
        JsonObject denied = Assert.Single(response["entry"]!.AsArray())!.AsObject();
        Assert.Equal("denied", denied["fullUrl"]!.GetValue<string>());
        AssertOwnDenial(denied, method, url, method switch
        {
            "DELETE" => "InstanceDelete",
            "POST" => "TypeCreate",
            _ => "InstanceUpdate",
        });
        AssertUnchanged(store, before, watch);
    }

    /// <summary>A late authorization failure, including a nested read, prevents all earlier mutations.</summary>
    [Theory]
    [MemberData(nameof(TransactionCases))]
    public async Task TransactionHttpAdmissionPreventsEarlierWrites(FhirReleases.FhirSequenceCodes version, bool nested)
    {
        using TestHost host = new(version);
        IFhirStore store = host.Store;
        Seed(store);
        MutationWatch watch = new(store);
        string[] before = Fingerprint(store);
        (AuthorizationInfo authorization, AuthorizationInfo.SmartResponse grant) = ExchangeAuthorizationCode(host, "user/Patient.c", string.Empty);
        Assert.True(host.AuthManager.IsAuthorized(Context(store, "POST", "Patient", authorization)));
        Assert.False(host.AuthManager.IsAuthorized(Context(store, "GET", "Patient/seed", authorization)));
        JsonObject late = nested
            ? Entry("wrapper", "POST", "/", Bundle("batch",
                Entry("nested-allowed", "POST", "Patient", Patient("nested-client", "MustNotPersist")),
                Entry("nested-wrapper", "POST", "/", Bundle("transaction",
                    Entry("denied", "GET", "Patient/seed")))))
            : Entry("denied", "GET", "Patient/seed");
        JsonObject response = await SendJsonBundle(host, store, Bundle("transaction",
            Entry("allowed", "POST", "Patient", Patient("client", "MustNotPersist")), late), grant.AccessToken);
        JsonArray entries = response["entry"]!.AsArray();
        Assert.Equal(2, entries.Count);
        JsonObject earlier = entries[0]!.AsObject();
        Assert.Equal("allowed", earlier["fullUrl"]!.GetValue<string>());
        AssertCollateralDenial(earlier);
        Assert.Single(Issues(earlier));
        JsonObject refused = entries[1]!.AsObject();
        if (nested)
        {
            Assert.Equal("wrapper", refused["fullUrl"]!.GetValue<string>());
            AssertCollateralDenial(refused);
            Assert.Equal(2, Issues(refused).Count);
            Assert.Equal(
                "Denied descendant Bundle.entry[1].entry[1].entry[0]: Unauthorized request: GET Patient/seed, parsed interaction: InstanceRead",
                Issues(refused)[1]!["diagnostics"]!.GetValue<string>());
            Assert.DoesNotContain("Unauthorized request: POST /", refused.ToJsonString());
        }
        else
        {
            Assert.Equal("denied", refused["fullUrl"]!.GetValue<string>());
            AssertOwnDenial(refused, "GET", "Patient/seed", "InstanceRead");
        }

        AssertUnchanged(store, before, watch);
    }

    /// <summary>Required SMART rejects null authorization at the controller; optional null access still executes.</summary>
    [Theory]
    [MemberData(nameof(MissingAuthorizationCases))]
    public async Task MissingAuthorizationRemainsRejected(
        FhirReleases.FhirSequenceCodes version,
        string kind,
        bool unknownBearer)
    {
        using TestHost host = new(version, includeOptional: true);
        IFhirStore required = host.Store;
        IFhirStore optional = host.OptionalStore;
        Seed(required);
        Seed(optional);
        MutationWatch requiredWatch = new(required);
        MutationWatch optionalWatch = new(optional);
        string[] before = Fingerprint(required);
        string optionalPatient = Serialize(optional, optional["Patient"]["seed"]);
        string optionalObservation = Serialize(optional, optional["Observation"]["seed"]);
        string? token = unknownBearer ? Guid.NewGuid().ToString() : null;
        Assert.True(required.Config.SmartRequired);
        Assert.False(required.Config.SmartAllowed);
        Assert.Equal(unknownBearer ? GrantStatus.NotCurrent : GrantStatus.Missing, host.AuthManager.ResolveAccessToken(required.Config.ControllerName, token, out _));
        Assert.False(host.AuthManager.IsAuthorized(Context(required, "POST", string.Empty, null)));
        JsonObject request = Bundle(kind, Entry("write", "PUT", "Patient/attempt", Patient("attempt", "OptionalWrite")));
        using HttpResponseMessage denied = await SendAsync(host, required, "POST", string.Empty, token, request.ToJsonString());
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        AssertUnchanged(required, before, requiredWatch);

        Assert.False(optional.Config.SmartRequired);
        Assert.True(optional.Config.SmartAllowed);
        Assert.Equal(unknownBearer ? GrantStatus.NotCurrent : GrantStatus.Missing, host.AuthManager.ResolveAccessToken(optional.Config.ControllerName, token, out _));
        Assert.True(host.AuthManager.IsAuthorized(Context(optional, "POST", string.Empty, null)));
        JsonObject response = await SendJsonBundle(host, optional, request, token);
        JsonObject created = Assert.Single(response["entry"]!.AsArray())!.AsObject();
        Assert.Equal("201 Created", created["response"]!["status"]!.GetValue<string>());
        Assert.Equal("attempt", created["resource"]!["id"]!.GetValue<string>());
        Assert.EndsWith("/Patient/attempt", created["response"]!["location"]!.GetValue<string>());
        Assert.Equal("1", created["resource"]!["meta"]!["versionId"]!.GetValue<string>());
        Assert.Equal(2, optional["Patient"].Count);
        Assert.Single(optional["Observation"]);
        Assert.Equal("OptionalWrite", JsonNode.Parse(Serialize(optional, optional["Patient"]["attempt"]))!["name"]![0]!["family"]!.GetValue<string>());
        Assert.Equal(optionalPatient, Serialize(optional, optional["Patient"]["seed"]));
        Assert.Equal(optionalObservation, Serialize(optional, optional["Observation"]["seed"]));
        // The existing PUT create-as-update path emits an update event even on 201.
        Assert.Equal(new[] { "update:Patient/attempt" }, optionalWatch.Events);
        AssertUnchanged(required, before, requiredWatch);
    }

    /// <summary>XML uses the same refusal envelope and preserves independent batch reads versus collateral refusals.</summary>
    [Theory]
    [MemberData(nameof(BundleCases))]
    public async Task XmlBundleDenialsPreserveEnvelope(FhirReleases.FhirSequenceCodes version, string kind)
    {
        using TestHost host = new(version);
        IFhirStore store = host.Store;
        Seed(store);
        MutationWatch watch = new(store);
        string[] before = Fingerprint(store);
        (AuthorizationInfo authorization, AuthorizationInfo.SmartResponse grant) = ExchangeAuthorizationCode(host, "user/Patient.r", string.Empty);
        Assert.True(host.AuthManager.IsAuthorized(Context(store, "GET", "Patient/seed", authorization)));
        Assert.False(host.AuthManager.IsAuthorized(Context(store, "DELETE", "Patient/seed", authorization)));
        XElement request = new(_fhir + "Bundle",
            Primitive("type", kind),
            XmlEntry("read", "GET", "Patient/seed"),
            XmlEntry("denied", "DELETE", "Patient/seed"));
        using HttpResponseMessage response = await SendAsync(
            host, store, "POST", string.Empty, grant.AccessToken, request.ToString(SaveOptions.DisableFormatting), XmlFormat);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(XmlFormat, response.Content.Headers.ContentType?.MediaType);
        XElement bundle = XElement.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(_fhir + "Bundle", bundle.Name);
        Assert.Equal($"{kind}-response", XmlValue(bundle, "type"));
        XElement[] entries = bundle.Elements(_fhir + "entry").ToArray();
        Assert.Equal(2, entries.Length);
        Assert.Equal(kind == "batch" ? new[] { "read", "denied" } : new[] { "denied", "read" },
            entries.Select(e => XmlValue(e, "fullUrl")));
        XElement denied = entries.Single(e => XmlValue(e, "fullUrl") == "denied");
        XElement ownIssue = Assert.Single(XmlRefusal(denied));
        Assert.Equal("Unauthorized request: DELETE Patient/seed, parsed interaction: InstanceDelete", XmlValue(ownIssue, "diagnostics"));
        XElement read = entries.Single(e => XmlValue(e, "fullUrl") == "read");
        if (kind == "transaction")
        {
            XElement collateralIssue = Assert.Single(XmlRefusal(read));
            Assert.Equal(CollateralDiagnostic, XmlValue(collateralIssue, "diagnostics"));
        }
        else
        {
            Assert.Equal("200 OK", XmlValue(read.Element(_fhir + "response")!, "status"));
            XElement patient = Assert.Single(read.Element(_fhir + "resource")!.Elements());
            Assert.Equal(_fhir + "Patient", patient.Name);
            Assert.Equal("seed", XmlValue(patient, "id"));
            Assert.Equal("Seed", XmlValue(Assert.Single(patient.Elements(_fhir + "name")), "family"));
            Assert.Equal("1", XmlValue(patient.Element(_fhir + "meta")!, "versionId"));
        }

        AssertUnchanged(store, before, watch);
    }

    private static (JsonWebKey PrivateKey, JsonWebKey PublicKey) CreateAssertionKeys()
    {
        using RSA rsa = RSA.Create(2048);
        JsonWebKey privateKey = JsonWebKeyConverter.ConvertFromRSASecurityKey(new RsaSecurityKey(rsa.ExportParameters(true)));
        JsonWebKey publicKey = JsonWebKeyConverter.ConvertFromRSASecurityKey(new RsaSecurityKey(rsa.ExportParameters(false)));
        string keyId = Guid.NewGuid().ToString();
        privateKey.Alg = "RS384";
        privateKey.Kid = keyId;
        privateKey.KeyOps.Add("sign");
        publicKey.Alg = "RS384";
        publicKey.Kid = keyId;
        publicKey.KeyOps.Add("verify");
        Assert.True(string.IsNullOrEmpty(publicKey.D));
        return (privateKey, publicKey);
    }

    private static string RegisterAssertionClient(ISmartAuthManager manager, JsonWebKey publicKey)
    {
        JsonWebKeySet keySet = new();
        keySet.Keys.Add(publicKey);
        SmartClientRegistration registration = new()
        {
            ClientName = "https://client.example.test/" + Guid.NewGuid().ToString(),
            KeySet = keySet,
        };
        Assert.True(manager.TryRegisterClient(registration, out string clientId, out List<string> messages), string.Join("\n", messages));
        Assert.Empty(messages);
        Assert.Equal(registration.ClientName, clientId);
        Assert.True(manager.SmartClients[clientId].Keys.ContainsKey(publicKey.Kid));
        return clientId;
    }

    private static async Task<(string AccessToken, AuthorizationInfo Authorization)> ExchangeAssertion(
        TestHost host, string clientId, JsonWebKey signingKey, string scope)
    {
        TenantConfiguration tenant = host.Store.Config;
        string assertion = host.AuthManager.GenerateSignedJwt(
            clientId, clientId, tenant.BaseUrl, Guid.NewGuid().ToString(), DateTime.UtcNow.AddMinutes(10), signingKey);
        Assert.False(string.IsNullOrEmpty(assertion));
        using FormUrlEncodedContent form = new(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_assertion_type"] = "urn:ietf:params:oauth:client-assertion-type:jwt-bearer",
            ["client_assertion"] = assertion,
            ["scope"] = scope,
        });
        using HttpResponseMessage response = await host.Factory.Client.PostAsync($"/_smart/{tenant.ControllerName}/token", form);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        JsonObject body = JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();
        Assert.Equal("bearer", body["token_type"]!.GetValue<string>());
        Assert.Equal(scope, body["scope"]!.GetValue<string>());
        Assert.Equal(clientId, body["client_id"]!.GetValue<string>());
        string token = body["access_token"]!.GetValue<string>();
        Assert.False(string.IsNullOrEmpty(token));
        Assert.Equal(GrantStatus.Admitted, host.AuthManager.ResolveAccessToken(tenant.ControllerName, token, out AuthorizationInfo authorization));
        Assert.Equal(tenant.ControllerName, authorization.Tenant);
        Assert.Equal(clientId, authorization.RequestParameters.ClientId);
        Assert.Equal(tenant.BaseUrl, authorization.RequestParameters.Audience);
        Assert.Equal(scope, authorization.RequestParameters.Scope);
        return (token, authorization);
    }

    private static (AuthorizationInfo Authorization, AuthorizationInfo.SmartResponse Response) ExchangeAuthorizationCode(
        TestHost host, string scope, string launchPatient, params string[] declinedScopes)
    {
        TenantConfiguration tenant = host.Store.Config;
        string clientId = "interactive-" + Guid.NewGuid().ToString();
        Assert.True(host.AuthManager.RequestAuth(
            tenant.ControllerName, "127.0.0.1", "code", clientId, "http://localhost/dev/null", string.Empty,
            scope, string.Empty, tenant.BaseUrl, string.Empty, string.Empty, out string redirect, out string key));
        Assert.EndsWith("key=" + key, redirect);
        Assert.True(Guid.TryParse(key, out Guid parsed));
        Assert.NotEqual(Guid.Empty, parsed);
        Assert.True(host.AuthManager.TryGetPendingAuthorization(tenant.ControllerName, key, out AuthorizationInfo authorization));
        Assert.Equal(tenant.ControllerName, authorization.Tenant);
        Assert.Equal(clientId, authorization.RequestParameters.ClientId);
        Assert.Equal(tenant.BaseUrl, authorization.RequestParameters.Audience);
        Assert.Equal(scope, authorization.RequestParameters.Scope);
        AssertEffectiveScopes(authorization, [], []);
        authorization.UserId = "consenting-test-user";
        authorization.LaunchPatient = launchPatient;
        foreach (string requested in authorization.Scopes.Keys)
        {
            authorization.Scopes[requested] = !declinedScopes.Contains(requested, StringComparer.Ordinal);
        }
        Assert.True(host.AuthManager.TryUpdateAuth(tenant.ControllerName, key, authorization));
        Assert.True(host.AuthManager.TryCreateSmartResponse(
            tenant.ControllerName, authorization.AuthCode, clientId, string.Empty, string.Empty,
            out AuthorizationInfo.SmartResponse response));
        Assert.Equal("bearer", response.TokenType);
        Assert.Equal(clientId, response.ClientId);
        Assert.False(string.IsNullOrEmpty(response.AccessToken));
        Assert.Equal(GrantStatus.Admitted, host.AuthManager.ResolveAccessToken(
            tenant.ControllerName, response.AccessToken, out AuthorizationInfo resolved));
        Assert.NotSame(authorization, resolved);
        Assert.Null(resolved.Response);
        Assert.DoesNotContain("*.*", resolved.UserScopes);
        Assert.DoesNotContain("*.*", resolved.PatientScopes);
        foreach (string declined in declinedScopes)
        {
            Assert.False(resolved.Scopes[declined]);
            Assert.DoesNotContain(declined, response.Scopes.Split(' '));
        }
        return (resolved, response);
    }

    private static void AssertBackendIdentity(AuthorizationInfo authorization)
    {
        Assert.False(string.IsNullOrEmpty(authorization.Key));
        Assert.True(Guid.TryParse(authorization.Key, out Guid key));
        Assert.NotEqual(Guid.Empty, key);
        Assert.Empty(authorization.UserId);
        Assert.Empty(authorization.LaunchPatient);
        Assert.DoesNotContain("*.*", authorization.UserScopes);
        Assert.DoesNotContain("*.*", authorization.PatientScopes);
    }

    private static void AssertEffectiveScopes(AuthorizationInfo authorization, string[] userScopes, string[] patientScopes)
    {
        Assert.Equal(
            userScopes.OrderBy(s => s, StringComparer.Ordinal),
            authorization.UserScopes.OrderBy(s => s, StringComparer.Ordinal));
        Assert.Equal(
            patientScopes.OrderBy(s => s, StringComparer.Ordinal),
            authorization.PatientScopes.OrderBy(s => s, StringComparer.Ordinal));
    }

    private static void AssertOrdinaryPermissions(
        TestHost host, AuthorizationInfo authorization, string patientActions, string observationActions)
    {
        foreach ((string resource, string actions) in new[] { ("Patient", patientActions), ("Observation", observationActions) })
        {
            foreach ((string method, string suffix, char action) in new[]
            {
                ("POST", string.Empty, 'c'), ("GET", "/seed", 'r'), ("PUT", "/seed", 'u'),
                ("DELETE", "/seed", 'd'), ("GET", string.Empty, 's'),
            })
            {
                bool allowed = host.AuthManager.IsAuthorized(Context(host.Store, method, resource + suffix, authorization));
                Assert.True(actions.Contains(action) == allowed,
                    $"{authorization.RequestParameters.Scope}: {method} {resource + suffix} expected {actions.Contains(action)}, got {allowed}");
            }
        }
    }

    private static async Task AssertSeededRead(TestHost host, IFhirStore store, string resource, string? token)
    {
        using HttpResponseMessage response = await SendAsync(host, store, "GET", resource + "/seed", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(JsonFormat, response.Content.Headers.ContentType?.MediaType);
        Assert.True(JsonNode.DeepEquals(
            JsonNode.Parse(Serialize(store, store[resource]["seed"])),
            JsonNode.Parse(await response.Content.ReadAsStringAsync())));
    }

    private static async Task AssertSeededSearch(TestHost host, IFhirStore store, string resource, string? token)
    {
        using HttpResponseMessage response = await SendAsync(host, store, "GET", resource, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(JsonFormat, response.Content.Headers.ContentType?.MediaType);
        JsonObject bundle = JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();
        Assert.Equal("Bundle", bundle["resourceType"]!.GetValue<string>());
        Assert.Equal("searchset", bundle["type"]!.GetValue<string>());
        JsonObject match = Assert.Single(bundle["entry"]!.AsArray())!.AsObject();
        Assert.Equal("match", match["search"]!["mode"]!.GetValue<string>());
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(Serialize(store, store[resource]["seed"])), match["resource"]));
    }

    [Fact]
    public async Task IntrospectionReturnsOnlyActiveFlagForNoncurrentTokens()
    {
        using TestHost host = new(FhirReleases.FhirSequenceCodes.R4, includePeerRequired: true);
        string tenant = host.Store.Config.ControllerName;
        (_, AuthorizationInfo.SmartResponse grant) = ExchangeAuthorizationCode(host, "user/Patient.r", string.Empty);
        using HttpResponseMessage active = await Post(tenant, grant.AccessToken);
        Assert.Equal(HttpStatusCode.OK, active.StatusCode);
        JsonObject body = JsonNode.Parse(await active.Content.ReadAsStringAsync())!.AsObject();
        Assert.True(body["active"]!.GetValue<bool>());
        Assert.Equal("user/Patient.r", body["scope"]!.GetValue<string>());

        Assert.True(host.AuthManager.TrySmartRefresh(tenant, grant.RefreshToken, grant.ClientId, out AuthorizationInfo.SmartResponse rotated));
        foreach ((string name, string token) in new[]
        {
            (tenant, grant.AccessToken), (tenant, rotated.RefreshToken),
            (host.PeerRequiredStore.Config.ControllerName, rotated.AccessToken),
        })
        {
            using HttpResponseMessage inactive = await Post(name, token);
            Assert.Equal(HttpStatusCode.OK, inactive.StatusCode);
            Assert.Equal("application/json", inactive.Content.Headers.ContentType?.MediaType);
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse("{\"active\":false}"), JsonNode.Parse(await inactive.Content.ReadAsStringAsync())));
        }

        using HttpResponseMessage malformed = await Post(tenant, string.Empty);
        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);

        async Task<HttpResponseMessage> Post(string name, string token)
        {
            using FormUrlEncodedContent form = new(new Dictionary<string, string> { ["token"] = token });
            return await host.Factory.Client.PostAsync($"/_smart/{name}/introspect", form);
        }
    }

    private static FhirRequestContext Context(IFhirStore store, string method, string url, AuthorizationInfo? authorization) => new()
    {
        TenantName = store.Config.ControllerName,
        Store = store,
        Authorization = authorization,
        HttpMethod = method,
        Url = url,
    };

    private static async Task<HttpResponseMessage> SendAsync(
        TestHost host,
        IFhirStore store,
        string method,
        string url,
        string? token,
        string? content = null,
        string format = JsonFormat)
    {
        using HttpRequestMessage request = new(new HttpMethod(method), $"/fhir/{store.Config.ControllerName}/{url}");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(format));
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        if (content is not null)
        {
            request.Content = new StringContent(content, Encoding.UTF8, format);
        }

        return await host.Factory.Client.SendAsync(request);
    }

    private static async Task<JsonObject> SendJsonBundle(TestHost host, IFhirStore store, JsonObject request, string? token)
    {
        using HttpResponseMessage response = await SendAsync(host, store, "POST", string.Empty, token, request.ToJsonString());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(JsonFormat, response.Content.Headers.ContentType?.MediaType);
        JsonObject bundle = JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();
        Assert.Equal("Bundle", bundle["resourceType"]!.GetValue<string>());
        Assert.Equal($"{request["type"]!.GetValue<string>()}-response", bundle["type"]!.GetValue<string>());
        return bundle;
    }

    private static JsonObject Patient(string id, string family) => new()
    {
        ["resourceType"] = "Patient",
        ["id"] = id,
        ["active"] = true,
        ["identifier"] = new JsonArray(new JsonObject { ["system"] = "urn:bundle-http", ["value"] = id }),
        ["name"] = new JsonArray(new JsonObject { ["family"] = family }),
    };

    private static JsonObject Entry(string label, string method, string url, JsonObject? resource = null)
    {
        JsonObject entry = new()
        {
            ["fullUrl"] = label,
            ["request"] = new JsonObject { ["method"] = method, ["url"] = url },
        };
        if (resource is not null)
        {
            entry["resource"] = resource;
        }
        return entry;
    }

    private static JsonObject Bundle(string kind, params JsonObject[] entries) => new()
    {
        ["resourceType"] = "Bundle",
        ["type"] = kind,
        ["identifier"] = new JsonObject { ["system"] = "urn:bundle-http", ["value"] = "bundle" },
        ["entry"] = new JsonArray(entries.Cast<JsonNode?>().ToArray()),
    };

    private static JsonArray Issues(JsonObject entry) => entry["response"]!["outcome"]!["issue"]!.AsArray();

    private static void AssertRefusal(JsonObject entry)
    {
        Assert.Equal("401 Unauthorized", entry["response"]!["status"]!.GetValue<string>());
        Assert.Null(entry["resource"]);
        Assert.Null(entry["response"]!["etag"]);
        Assert.Null(entry["response"]!["location"]);
        Assert.Null(entry["response"]!["lastModified"]);
        Assert.Equal("OperationOutcome", entry["response"]!["outcome"]!["resourceType"]!.GetValue<string>());
        Assert.NotEmpty(Issues(entry));
        Assert.All(Issues(entry), issue =>
        {
            Assert.Equal("error", issue!["severity"]!.GetValue<string>());
            Assert.Equal("forbidden", issue["code"]!.GetValue<string>());
        });
    }

    private static void AssertOwnDenial(JsonObject entry, string method, string url, string interaction)
    {
        AssertRefusal(entry);
        JsonNode issue = Assert.Single(Issues(entry))!;
        Assert.Equal($"Unauthorized request: {method} {url}, parsed interaction: {interaction}", issue["diagnostics"]!.GetValue<string>());
    }

    private static void AssertCollateralDenial(JsonObject entry)
    {
        AssertRefusal(entry);
        Assert.Equal(CollateralDiagnostic, Issues(entry)[0]!["diagnostics"]!.GetValue<string>());
    }

    private static XElement Primitive(string name, string value) => new(_fhir + name, new XAttribute("value", value));

    private static XElement XmlEntry(string label, string method, string url) => new(_fhir + "entry",
        Primitive("fullUrl", label),
        new XElement(_fhir + "request", Primitive("method", method), Primitive("url", url)));

    private static string? XmlValue(XElement element, string name) => (string?)element.Element(_fhir + name)?.Attribute("value");

    private static XElement[] XmlRefusal(XElement entry)
    {
        Assert.Null(entry.Element(_fhir + "resource"));
        XElement response = Assert.Single(entry.Elements(_fhir + "response"));
        Assert.Equal("401 Unauthorized", XmlValue(response, "status"));
        Assert.Null(response.Element(_fhir + "etag"));
        Assert.Null(response.Element(_fhir + "location"));
        Assert.Null(response.Element(_fhir + "lastModified"));
        XElement outcome = Assert.Single(response.Element(_fhir + "outcome")!.Elements());
        Assert.Equal(_fhir + "OperationOutcome", outcome.Name);
        XElement[] issues = outcome.Elements(_fhir + "issue").ToArray();
        Assert.NotEmpty(issues);
        Assert.All(issues, issue =>
        {
            Assert.Equal("error", XmlValue(issue, "severity"));
            Assert.Equal("forbidden", XmlValue(issue, "code"));
        });
        return issues;
    }

    private static void Seed(IFhirStore store)
    {
        SeedResource(store, "Patient", Patient("seed", "Seed"));
        SeedResource(store, "Observation", new()
        {
            ["resourceType"] = "Observation",
            ["id"] = "seed",
            ["status"] = "final",
            ["code"] = new JsonObject { ["text"] = "Preserve complete clinical state" },
            ["subject"] = new JsonObject { ["reference"] = "Patient/seed" },
        });
    }

    private static void SeedResource(IFhirStore store, string type, JsonObject resource)
    {
        AuthorizationInfo privileged = new()
        {
            Tenant = store.Config.ControllerName,
            RemoteIpAddress = "127.0.0.1",
            RequestParameters = new() { Scope = "user/*.*" },
            UserScopes = ["*.*"],
        };
        FhirRequestContext ctx = Context(store, "PUT", $"{type}/seed", privileged) with
        {
            SourceContent = resource.ToJsonString(),
            SourceFormat = JsonFormat,
        };
        Assert.True(store.InstanceUpdate(ctx, out FhirResponseContext response), response.SerializedOutcome);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static string Serialize(IFhirStore store, object resource)
    {
        Hl7.Fhir.Model.Resource model = Assert.IsAssignableFrom<Hl7.Fhir.Model.Resource>(resource);
        return store.Config.FhirVersion switch
        {
            FhirReleases.FhirSequenceCodes.R4 => candleR4::FhirCandle.Serialization.SerializationUtils.SerializeFhir(model, JsonFormat, false),
            FhirReleases.FhirSequenceCodes.R4B => candleR4B::FhirCandle.Serialization.SerializationUtils.SerializeFhir(model, JsonFormat, false),
            FhirReleases.FhirSequenceCodes.R5 => candleR5::FhirCandle.Serialization.SerializationUtils.SerializeFhir(model, JsonFormat, false),
            _ => throw new ArgumentOutOfRangeException(nameof(store)),
        };
    }

    private static string[] Fingerprint(IFhirStore store) => store.OrderBy(s => s.Key, StringComparer.Ordinal)
        .SelectMany(s => new[] { $"{s.Key}:count={s.Value.Count}" }.Concat(s.Value.OrderBy(r => r.Key, StringComparer.Ordinal)
            .Select(r => $"{s.Key}/{r.Key}:{Serialize(store, r.Value)}"))).ToArray();

    private static void AssertUnchanged(IFhirStore store, string[] before, MutationWatch watch)
    {
        Assert.Equal(before, Fingerprint(store));
        Assert.Empty(watch.Events);
    }

    private sealed class MutationWatch
    {
        public List<string> Events { get; } = [];

        public MutationWatch(IFhirStore store)
        {
            store.OnInstanceCreated += (_, e) => Events.Add($"create:{e.ResourceType}/{e.ResourceId}");
            store.OnInstanceUpdated += (_, e) => Events.Add($"update:{e.ResourceType}/{e.ResourceId}");
            store.OnInstanceDeleted += (_, e) => Events.Add($"delete:{e.ResourceType}/{e.ResourceId}");
        }
    }

    private sealed class TestHost : IDisposable
    {
        private readonly string _directory;

        public CandleWebApplicationFactory Factory { get; } = null!;

        public IFhirStore Store { get; }

        public IFhirStore OptionalStore => Factory.Services.GetRequiredService<IFhirStoreManager>()[Store.Config.ControllerName + "-optional"];

        public IFhirStore PeerRequiredStore => Factory.Services.GetRequiredService<IFhirStoreManager>()[Store.Config.ControllerName + "-peer"];

        public IFhirStore DisabledStore => Factory.Services.GetRequiredService<IFhirStoreManager>()[Store.Config.ControllerName + "-disabled"];

        public ISmartAuthManager AuthManager { get; }

        public TestHost(
            FhirReleases.FhirSequenceCodes version,
            bool includeOptional = false,
            bool includePeerRequired = false,
            bool includeDisabled = false)
        {
            DirectoryInfo root = new(AppContext.BaseDirectory);
            while (!File.Exists(Path.Combine(root.FullName, "fhir-candle.sln")))
            {
                root = root.Parent ?? throw new InvalidOperationException("Repository root not found.");
            }
            _directory = Path.Combine(root.FullName, "scratch", "1119-02", $"bundle-http-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_directory);
            string name = $"bundle-{version.ToString().ToLowerInvariant()}";
            List<TenantConfiguration> tenants = [Tenant(name, version, required: true)];
            if (includeOptional)
            {
                tenants.Add(Tenant(name + "-optional", version, required: false));
            }
            if (includePeerRequired)
            {
                tenants.Add(Tenant(name + "-peer", version, required: true));
            }
            if (includeDisabled)
            {
                tenants.Add(Tenant(name + "-disabled", version, required: false, allowed: false));
            }

            // The unchanged factory uses GetTempPath. This non-parallel collection keeps
            // its files repository-local, restoring the process environment immediately.
            string[] variables = ["TEMP", "TMP", "TMPDIR"];
            string?[] previous = variables.Select(Environment.GetEnvironmentVariable).ToArray();
            try
            {
                try
                {
                    foreach (string variable in variables)
                    {
                        Environment.SetEnvironmentVariable(variable, _directory);
                    }
                    Factory = new(tenants);
                }
                finally
                {
                    for (int index = 0; index < variables.Length; index++)
                    {
                        Environment.SetEnvironmentVariable(variables[index], previous[index]);
                    }
                }
                Assert.StartsWith(_directory + Path.DirectorySeparatorChar, Factory.OwnedDirectory, StringComparison.OrdinalIgnoreCase);
                Store = Factory.Services.GetRequiredService<IFhirStoreManager>()[name];
                AuthManager = Factory.Services.GetRequiredService<ISmartAuthManager>();
            }
            catch
            {
                Factory?.Dispose();
                Directory.Delete(_directory, recursive: true);
                throw;
            }
        }

        public void Dispose()
        {
            try
            {
                Factory.Dispose();
            }
            finally
            {
                Directory.Delete(_directory, recursive: true);
            }
        }

        private static TenantConfiguration Tenant(
            string name, FhirReleases.FhirSequenceCodes version, bool required, bool? allowed = null) => new()
        {
            FhirVersion = version,
            ControllerName = name,
            BaseUrl = $"http://localhost/fhir/{name}",
            AllowExistingId = true,
            AllowCreateAsUpdate = true,
            SmartRequired = required,
            SmartAllowed = allowed ?? !required,
        };
    }
}
