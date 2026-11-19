// <copyright file="BundleAuthorizationRoutingTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

extern alias candleR4;
extern alias candleR4B;
extern alias candleR5;

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using fhir.candle.Services;
using FhirCandle.Configuration;
using FhirCandle.Models;
using FhirCandle.Storage;
using FhirCandle.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace fhir.candle.Tests.Controllers;

/// <summary>Exercises entry authorization through the real controller and its host-owned SMART manager.</summary>
[Collection(CompatibilityHostCollection.Name)]
public class BundleAuthorizationRoutingTests
{
    private const string JsonFormat = "application/fhir+json";
    private const string XmlFormat = "application/fhir+xml";
    private const string CollateralDiagnostic = "Entry not executed because transaction authorization failed";
    private static readonly XNamespace _fhir = "http://hl7.org/fhir";

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
        AuthorizationInfo authorization = InstallAuthorization(host, "Patient.r");
        string url = method == "POST" ? "Patient" : "Patient/seed";
        JsonObject? body = method == "DELETE" ? null : Patient(method == "POST" ? "attempt" : "seed", "MustNotPersist");
        Assert.True(host.AuthManager.IsAuthorized(Context(store, "GET", "Patient/seed", authorization)));
        Assert.False(host.AuthManager.IsAuthorized(Context(store, method, url, authorization)));

        using HttpResponseMessage read = await SendAsync(host, store, "GET", "Patient/seed", authorization.Key);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.Equal(JsonFormat, read.Content.Headers.ContentType?.MediaType);
        Assert.True(JsonNode.DeepEquals(
            JsonNode.Parse(Serialize(store, store["Patient"]["seed"])),
            JsonNode.Parse(await read.Content.ReadAsStringAsync())));
        AssertUnchanged(store, before, watch);

        using HttpResponseMessage direct = await SendAsync(host, store, method, url, authorization.Key, body?.ToJsonString());
        Assert.Equal(HttpStatusCode.Unauthorized, direct.StatusCode);
        AssertUnchanged(store, before, watch);

        JsonObject response = await SendJsonBundle(host, store, Bundle(kind, Entry("denied", method, url, body)), authorization.Key);
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
        AuthorizationInfo authorization = InstallAuthorization(host, "Patient.c");
        Assert.True(host.AuthManager.IsAuthorized(Context(store, "POST", "Patient", authorization)));
        Assert.False(host.AuthManager.IsAuthorized(Context(store, "GET", "Patient/seed", authorization)));
        JsonObject late = nested
            ? Entry("wrapper", "POST", "/", Bundle("batch",
                Entry("nested-allowed", "POST", "Patient", Patient("nested-client", "MustNotPersist")),
                Entry("nested-wrapper", "POST", "/", Bundle("transaction",
                    Entry("denied", "GET", "Patient/seed")))))
            : Entry("denied", "GET", "Patient/seed");
        JsonObject response = await SendJsonBundle(host, store, Bundle("transaction",
            Entry("allowed", "POST", "Patient", Patient("client", "MustNotPersist")), late), authorization.Key);
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
        Assert.False(host.AuthManager.TryGetAuthorization(required.Config.ControllerName, token is null ? string.Empty : $"Bearer {token}", out _));
        Assert.False(host.AuthManager.IsAuthorized(Context(required, "POST", string.Empty, null)));
        JsonObject request = Bundle(kind, Entry("write", "PUT", "Patient/attempt", Patient("attempt", "OptionalWrite")));
        using HttpResponseMessage denied = await SendAsync(host, required, "POST", string.Empty, token, request.ToJsonString());
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        AssertUnchanged(required, before, requiredWatch);

        Assert.False(optional.Config.SmartRequired);
        Assert.True(optional.Config.SmartAllowed);
        Assert.False(host.AuthManager.TryGetAuthorization(optional.Config.ControllerName, token is null ? string.Empty : $"Bearer {token}", out _));
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
        AuthorizationInfo authorization = InstallAuthorization(host, "Patient.r");
        Assert.True(host.AuthManager.IsAuthorized(Context(store, "GET", "Patient/seed", authorization)));
        Assert.False(host.AuthManager.IsAuthorized(Context(store, "DELETE", "Patient/seed", authorization)));
        XElement request = new(_fhir + "Bundle",
            Primitive("type", kind),
            XmlEntry("read", "GET", "Patient/seed"),
            XmlEntry("denied", "DELETE", "Patient/seed"));
        using HttpResponseMessage response = await SendAsync(
            host, store, "POST", string.Empty, authorization.Key, request.ToString(SaveOptions.DisableFormatting), XmlFormat);
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

    private static AuthorizationInfo InstallAuthorization(TestHost host, params string[] scopes)
    {
        string token = Guid.NewGuid().ToString();
        AuthorizationInfo authorization = new()
        {
            Key = token,
            Tenant = host.Store.Config.ControllerName,
            RemoteIpAddress = "127.0.0.1",
            RequestParameters = new()
            {
                ClientId = "bundle-authorization-routing",
                Audience = host.Store.Config.BaseUrl,
                Scope = string.Join(' ', scopes.Select(s => $"user/{s}")),
            },
            UserId = "restricted-test-user",
            UserScopes = new(scopes, StringComparer.Ordinal),
            Expires = DateTimeOffset.UtcNow.AddHours(1),
        };
        host.AuthManager.SmartAuthorizations.Add($"{authorization.Tenant}:{token}", authorization);
        Assert.IsType<SmartAuthManager>(host.AuthManager);
        Assert.True(Guid.TryParse(token, out Guid parsed));
        Assert.NotEqual(Guid.Empty, parsed);
        Assert.DoesNotContain("*.*", authorization.UserScopes);
        Assert.True(authorization.UserScopes.SetEquals(scopes));
        Assert.Empty(authorization.PatientScopes);
        Assert.True(host.AuthManager.TryGetAuthorization(authorization.Tenant, $"Bearer {token}", out AuthorizationInfo resolved));
        Assert.Same(authorization, resolved);
        Assert.True(host.AuthManager.IsAuthorized(Context(host.Store, "POST", string.Empty, resolved)));
        return resolved;
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

        public ISmartAuthManager AuthManager { get; }

        public TestHost(FhirReleases.FhirSequenceCodes version, bool includeOptional = false)
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

        private static TenantConfiguration Tenant(string name, FhirReleases.FhirSequenceCodes version, bool required) => new()
        {
            FhirVersion = version,
            ControllerName = name,
            BaseUrl = $"http://localhost/fhir/{name}",
            AllowExistingId = true,
            AllowCreateAsUpdate = true,
            SmartRequired = required,
            SmartAllowed = !required,
        };
    }
}
