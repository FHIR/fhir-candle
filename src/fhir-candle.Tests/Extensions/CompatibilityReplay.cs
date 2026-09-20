// <copyright file="CompatibilityReplay.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

extern alias candleR4;
extern alias candleR4B;
extern alias candleR5;

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using fhir.candle.Tests.Controllers;
using fhir.candle.Tests.Models;
using FhirCandle.Models;
using FhirCandle.Storage;
using FhirCandle.Utils;
using Microsoft.Extensions.Primitives;

namespace fhir.candle.Tests.Extensions;

/// <summary>Owns one scenario's stores/host, generated bindings, and ordered request replay.</summary>
internal sealed class CompatibilityReplay : IDisposable, IAsyncDisposable
{
    private readonly CompatibilityScenario _scenario;
    private readonly Dictionary<string, CompatibilityTenant> _tenants;
    private readonly Dictionary<string, IFhirStore> _stores = new(StringComparer.Ordinal);
    private readonly CandleWebApplicationFactory? _factory;
    private readonly List<KeyValuePair<string, CompatibilityResponse>> _responses = [];
    private bool _hasRun;
    private bool _disposed;

    internal CompatibilityReplay(CompatibilityScenario scenario, CompatibilityTransport transport)
    {
        if (string.IsNullOrWhiteSpace(scenario.Id) || string.IsNullOrWhiteSpace(scenario.Evidence) || scenario.Tenants.Count == 0)
        {
            throw new ArgumentException("A scenario requires an id, evidence citation, and explicit tenants.", nameof(scenario));
        }

        _scenario = scenario;
        Transport = transport;
        _tenants = scenario.Tenants.ToDictionary(tenant => tenant.Name, StringComparer.Ordinal);
        foreach (CompatibilityTenant tenant in _tenants.Values)
        {
            WireContractAssertions.BindBaseUrl(Bindings, tenant.Name, tenant.BaseUrl);
        }

        try
        {
            if (transport == CompatibilityTransport.Http)
            {
                _factory = new CandleWebApplicationFactory(_tenants.Values.Select(TenantConfiguration));
            }
            else
            {
                foreach (CompatibilityTenant tenant in _tenants.Values)
                {
                    IFhirStore store = tenant.Version switch
                    {
                        FhirReleases.FhirSequenceCodes.R4 => new candleR4::FhirCandle.Storage.VersionedFhirStore(),
                        FhirReleases.FhirSequenceCodes.R4B => new candleR4B::FhirCandle.Storage.VersionedFhirStore(),
                        FhirReleases.FhirSequenceCodes.R5 => new candleR5::FhirCandle.Storage.VersionedFhirStore(),
                        _ => throw new ArgumentOutOfRangeException(nameof(scenario), "Only R4, R4B, and R5 are product tenants."),
                    };
                    _stores.Add(tenant.Name, store);
                    store.Init(TenantConfiguration(tenant));
                }
            }
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    internal CompatibilityTransport Transport { get; }
    internal CompatibilityBindings Bindings { get; } = new();
    internal IReadOnlyList<KeyValuePair<string, CompatibilityResponse>> Responses => _responses;
    internal string? OwnedDirectory => _factory?.OwnedDirectory;

    /// <summary>Runs pre-seed emptiness checks, raw seeds, then the declared ordered requests.</summary>
    internal async Task RunAsync()
    {
        ThrowIfDisposed();
        if (_hasRun || _scenario.Steps.Count == 0)
        {
            throw new InvalidOperationException("A non-empty scenario can run only once per replay; create a fresh replay.");
        }

        _hasRun = true;
        await AssertNoClinicalResourcesAsync(_scenario.Steps[0].Request.Format);
        foreach (CompatibilitySeed seed in _scenario.Seeds)
        {
            await StepAsync(new CompatibilityStep(
                seed.Id,
                new CompatibilityRequest(seed.Tenant, "PUT", $"{seed.ResourceType}/{seed.ResourceId}")
                {
                    Format = seed.Format,
                    Content = seed.Content,
                },
                seed.Expected));
        }

        foreach (CompatibilityStep step in _scenario.Steps)
        {
            await StepAsync(step);
        }
    }

    /// <summary>Asserts no ambient clinical resources, without assuming conformance stores are empty.</summary>
    internal async Task AssertNoClinicalResourcesAsync(CompatibilityFormat format)
    {
        foreach (CompatibilityTenant tenant in _tenants.Values)
        {
            foreach (string resourceType in new[] { "Patient", "Observation" })
            {
                string id = $"initial-empty/{tenant.Name}/{resourceType}";
                string content = format == CompatibilityFormat.Json
                    ? $$$"""{"resourceType":"Bundle","type":"searchset","total":0,"link":[{"relation":"self","url":"{{{tenant.BaseUrl}}}/{{{resourceType}}}"}]}"""
                    : $"""<Bundle xmlns="http://hl7.org/fhir"><type value="searchset"/><total value="0"/><link><relation value="self"/><url value="{tenant.BaseUrl}/{resourceType}"/></link></Bundle>""";
                ExactBodyExpectation body = new(content);
                CompatibilityResponseExpectation expected = new(
                    HttpStatusCode.OK,
                    true,
                    body,
                    body,
                    new OutcomeBodyExpectation(id + "/outcome", [new("information", "success", "Type search successful")]));
                await StepAsync(new CompatibilityStep(id, new CompatibilityRequest(tenant.Name, "GET", resourceType) { Format = format }, expected));
            }
        }
    }

    /// <summary>Runs and asserts an additional typed step, useful to later domain-specific partial tests.</summary>
    internal async Task<CompatibilityResponse> StepAsync(CompatibilityStep step)
    {
        CompatibilityResponse response = await SendAsync(step.Request);
        _responses.Add(new KeyValuePair<string, CompatibilityResponse>(step.Id, response));
        try
        {
            WireContractAssertions.Response(
                step.Expected, response, step.Request.Format, Bindings.BaseUrls[step.Request.Tenant], Bindings);
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.Text.Json.JsonException or System.Xml.XmlException)
        {
            throw new InvalidOperationException(
                $"Scenario {_scenario.Id}, {Transport}, step {step.Id}, tenant {step.Request.Tenant}: {exception.Message}", exception);
        }

        return response;
    }

    /// <summary>Returns unmodified boundary output for domain-specific assertions, without interpreting SDK models.</summary>
    internal async Task<CompatibilityResponse> SendAsync(CompatibilityRequest request)
    {
        ThrowIfDisposed();
        string baseUrl = Bindings.BaseUrls[request.Tenant];
        string url;
        if (request.TargetBinding is string targetName)
        {
            if (!string.IsNullOrEmpty(request.Path))
            {
                throw new ArgumentException("Choose a literal path or a resource target binding, not both.", nameof(request));
            }

            GeneratedResourceBinding target = WireContractAssertions.ResourceBinding(Bindings, targetName);
            if (target.BaseUrl != baseUrl)
            {
                throw new ArgumentException("A bound request target cannot silently change tenants.", nameof(request));
            }

            url = target.Location;
        }
        else
        {
            if (Uri.TryCreate(request.Path, UriKind.Absolute, out _))
            {
                throw new ArgumentException("Request paths are relative to the explicitly configured tenant.", nameof(request));
            }

            url = $"{baseUrl}/{request.Path.TrimStart('/')}";
        }

        if (request.Pretty)
        {
            url = AddQuery(url, "_pretty=true");
        }

        if (!string.IsNullOrEmpty(request.Summary))
        {
            url = AddQuery(url, "_summary=" + Uri.EscapeDataString(request.Summary));
        }

        Dictionary<string, string> headers = new(request.Headers, StringComparer.OrdinalIgnoreCase);
        if (request.IfMatchBinding is string matchName)
        {
            GeneratedResourceBinding match = WireContractAssertions.ResourceBinding(Bindings, matchName);
            if (match.BaseUrl != baseUrl || !headers.TryAdd("If-Match", $"W/\"{match.Version}\""))
            {
                throw new ArgumentException("If-Match must use one explicit, same-tenant binding.", nameof(request));
            }
        }

        string sourceMediaType = request.SourceMediaType ?? WireContractAssertions.MediaType(request.Format);
        string destinationMediaType = request.DestinationMediaType ?? WireContractAssertions.MediaType(request.Format);
        if (Transport == CompatibilityTransport.Http)
        {
            using HttpRequestMessage message = new(new HttpMethod(request.Method), url);
            if (request.Content is not null)
            {
                message.Content = new StringContent(request.Content, Encoding.UTF8);
                message.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(sourceMediaType);
            }

            headers.TryAdd("Accept", destinationMediaType);
            foreach (KeyValuePair<string, string> header in headers)
            {
                if (!message.Headers.TryAddWithoutValidation(header.Key, header.Value) &&
                    !(message.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value) ?? false))
                {
                    throw new ArgumentException($"Cannot send request header {header.Key}.", nameof(request));
                }
            }

            using HttpResponseMessage response = await _factory!.Client.SendAsync(message);
            string body = await response.Content.ReadAsStringAsync();
            Dictionary<string, string[]> responseHeaders = response.Headers.Concat(response.Content.Headers)
                .ToDictionary(header => header.Key, header => header.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
            return new CompatibilityResponse(Transport, response.StatusCode, response.Content.Headers.ContentType?.MediaType ?? string.Empty)
            {
                HttpBody = body,
                HttpHeaders = responseHeaders,
            };
        }

        FhirRequestContext context = new()
        {
            TenantName = request.Tenant,
            Store = _stores[request.Tenant],
            HttpMethod = request.Method,
            Url = url,
            Authorization = null,
            SourceContent = request.Content ?? string.Empty,
            SourceFormat = sourceMediaType,
            DestinationFormat = destinationMediaType,
            RequestHeaders = headers.ToDictionary(header => header.Key, header => new StringValues(header.Value), StringComparer.OrdinalIgnoreCase),
            IfMatch = Header("If-Match"),
            IfNoneMatch = Header("If-None-Match"),
            IfNoneExist = Header("If-None-Exist"),
            IfModifiedSince = Header("If-Modified-Since"),
            SerializePretty = request.Pretty,
            SerializeSummaryFlag = request.Summary,
        };
        bool success = _stores[request.Tenant].PerformInteraction(context, out FhirResponseContext direct, serializeReturn: true);
        return new CompatibilityResponse(Transport, direct.StatusCode ?? throw new InvalidOperationException("Missing direct status."), direct.MimeType)
        {
            Direct = new CompatibilityDirectResponse(
                success, direct.SerializedResource, direct.SerializedOutcome,
                direct.ResourceType, direct.Id, direct.ETag, direct.LastModified, direct.Location),
        };

        string Header(string name) => headers.TryGetValue(name, out string? value) ? value : string.Empty;
    }

    /// <summary>Loads reviewed raw corpus data copied by the existing test project.</summary>
    internal static string LoadFixture(FhirReleases.FhirSequenceCodes version, string relativePath) =>
        LoadFixture(Path.Combine(version.ToString().ToLowerInvariant(), relativePath));

    internal static string LoadFixture(string relativePath)
    {
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "data", "sdk6-compatibility"));
        string path = Path.GetFullPath(Path.Combine(root, relativePath));
        if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("A fixture must be inside the checked-in corpus.", nameof(relativePath));
        }

        return File.ReadAllText(path);
    }

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
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        foreach (IFhirStore store in _stores.Values)
        {
            store.Dispose();
        }

        _stores.Clear();
    }

    private static TenantConfiguration TenantConfiguration(CompatibilityTenant tenant) => new()
    {
        FhirVersion = tenant.Version,
        ControllerName = tenant.Name,
        BaseUrl = tenant.BaseUrl,
        Strict = tenant.Strict,
        AllowExistingId = tenant.AllowExistingId,
        AllowCreateAsUpdate = tenant.AllowCreateAsUpdate,
        SupportNotChanged = tenant.SupportNotChanged,
        LoadDirectory = null,
    };

    private static string AddQuery(string url, string parameter) => url + (url.Contains('?') ? "&" : "?") + parameter;

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(CompatibilityReplay));
        }
    }
}
