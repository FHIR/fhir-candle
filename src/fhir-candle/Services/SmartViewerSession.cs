// <copyright file="SmartViewerSession.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using System.Net;
using System.Text.Json;
using FhirCandle.Models;
using FhirCandle.Storage;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.WebUtilities;

namespace fhir.candle.Services;

/// <summary>One circuit's explicitly selected inspection mode, credential and current display.</summary>
internal sealed class SmartViewerSession : IDisposable
{
    internal const string AdminLabel = "Admin — no sign-in";
    internal const string SmartLabel = "View as SMART caller";
    internal sealed record Request(string Tenant, string? Type = null, string? Id = null, string Format = "json");
    internal sealed record ResourceKind(string Name, int? Count);
    internal sealed record View
    {
        internal IFhirStore? AdminStore { get; init; }
        internal ResourceKind[] Types { get; init; } = [];
        internal InstanceTableRec[] Rows { get; init; } = [];
        internal string Body { get; init; } = string.Empty;
        internal HttpStatusCode? Status { get; init; }
    }

    private readonly object _gate = new();
    private readonly IFhirStoreManager _stores;
    private readonly ISmartAuthManager _auth;
    private readonly TimeProvider _clock;
    private readonly NavigationManager? _navigation;
    private ITimer? _timer;
    private string _tenant = string.Empty;
    private string? _credential;
    private Request? _activeRequest;
    private long _identity;
    private long _generation;
    private bool _disposed;
    private volatile View _display = new();

    internal View Display => _display;
    internal bool Smart { get; private set; }
    internal event Action? Expired;

    /// <summary>Creates the viewer for this circuit and observes tenant changes outside the inspector too.</summary>
    public SmartViewerSession(IFhirStoreManager stores, ISmartAuthManager auth, TimeProvider clock, NavigationManager? navigation = null)
    {
        _stores = stores;
        _auth = auth;
        _clock = clock;
        _navigation = navigation;
        if (navigation is not null)
        {
            navigation.LocationChanged += OnLocationChanged;
        }
    }

    private void OnLocationChanged(object? sender, LocationChangedEventArgs args)
    {
        Uri uri = new(args.Location);
        if (QueryHelpers.ParseQuery(uri.Query).TryGetValue("store", out Microsoft.Extensions.Primitives.StringValues tenant))
        {
            SelectTenant(tenant.ToString());
        }
    }

    internal void SelectTenant(string tenant)
    {
        lock (_gate)
        {
            if (_tenant != tenant)
            {
                Reset();
                _tenant = tenant;
            }
        }
    }

    internal void SelectMode(bool smart)
    {
        lock (_gate)
        {
            Reset();
            Smart = smart;
        }
    }

    private bool Resolve(string? token, out AuthorizationInfo grant) =>
        _auth.ResolveAccessToken(_tenant, token, out grant) == GrantStatus.Admitted &&
        grant.Key != Guid.Empty.ToString(); // Simulator access is not an issued credential.

    internal void SignIn(string token)
    {
        lock (_gate)
        {
            Reset();
            if (_disposed || !Smart || !Resolve(token, out AuthorizationInfo grant))
            {
                return;
            }

            _credential = token;
            long identity = _identity;
            DateTimeOffset deadline = grant.Expires;
            TimeSpan delay = deadline - _clock.GetUtcNow();
            _timer = _clock.CreateTimer(_ => Expire(identity, deadline), null,
                delay > TimeSpan.Zero ? delay : TimeSpan.Zero, Timeout.InfiniteTimeSpan);
        }
    }

    private void Expire(long identity, DateTimeOffset deadline)
    {
        lock (_gate)
        {
            if (_identity != identity)
            {
                return;
            }

            TimeSpan remaining = deadline - _clock.GetUtcNow();
            if (remaining > TimeSpan.Zero)
            {
                _timer!.Change(remaining, Timeout.InfiniteTimeSpan);
                return;
            }

            Reset();
            _display = new() { Status = HttpStatusCode.Unauthorized };
        }
        Expired?.Invoke();
    }

    private void Reset()
    {
        _identity++;
        _generation++;
        _credential = null;
        _activeRequest = null;
        _display = new();
        _timer?.Dispose();
        _timer = null;
    }

    internal void CloseView(Request? request)
    {
        lock (_gate)
        {
            // A route can be disposed after the next route has already started loading.
            if (request is not null && ReferenceEquals(request, _activeRequest))
            {
                _activeRequest = null;
                _generation++;
                _display = new();
            }
        }
    }

    internal async Task LoadAsync(Request request, Func<IFhirStore, Request, View> adminLoader)
    {
        long generation;
        bool smart;
        AuthorizationInfo? grant = null;
        IFhirStore? store;
        lock (_gate)
        {
            if (_disposed || _tenant != request.Tenant)
            {
                return;
            }
            generation = ++_generation;
            _activeRequest = request;
            _display = new();
            smart = Smart;
            if (smart && !Resolve(_credential, out grant))
            {
                Reset();
                _display = new() { Status = HttpStatusCode.Unauthorized };
                return;
            }
            if (!_stores.TryGetValue(_tenant, out store))
            {
                _display = new() { Status = HttpStatusCode.NotFound };
                return;
            }
        }

        View result;
        try
        {
            result = await Task.Run(() => smart
                ? LoadCaller(store, request, grant!)
                : adminLoader(store, request));
        }
        catch (Exception)
        {
            // Do not display arbitrary store/hook exception content.
            result = new() { Status = HttpStatusCode.InternalServerError };
        }

        lock (_gate)
        {
            if (generation != _generation)
            {
                return;
            }
            if (Smart && !Resolve(_credential, out _))
            {
                Reset();
                result = new() { Status = HttpStatusCode.Unauthorized };
            }
            _display = result;
        }
    }

    internal static View LoadAdmin(IFhirStore store, Request request)
    {
        if (request.Type is null)
        {
            return new() { AdminStore = store, Types = store.Select(p => new ResourceKind(p.Key, p.Value.Count)).ToArray() };
        }
        if (!store.TryGetValue(request.Type, out IResourceStore? resources))
        {
            return new() { Status = HttpStatusCode.NotFound };
        }
        if (request.Id is null)
        {
            return new() { AdminStore = store, Rows = resources.GetInstanceTableView().ToArray() };
        }

        bool metadata = request.Type == "CapabilityStatement" && request.Id == "metadata";
        FhirRequestContext ctx = Context(store, request, null, metadata);
        bool ok = metadata ? store.GetMetadata(ctx, out FhirResponseContext response) : store.InstanceRead(ctx, out response);
        return new() { AdminStore = store, Status = response.StatusCode, Body = ok ? response.SerializedResource : string.Empty };
    }

    private View LoadCaller(IFhirStore store, Request request, AuthorizationInfo grant)
    {
        if (request.Type is null)
        {
            // Schema only: never enumerate the stores' instances or counts.
            return new() { Types = store.Keys.Select(type => new ResourceKind(type, null)).ToArray() };
        }
        if (!store.SupportsResource(request.Type))
        {
            return new() { Status = HttpStatusCode.NotFound };
        }
        FhirRequestContext ctx = Context(store, request, grant);
        if (!_auth.IsAuthorized(ctx))
        {
            return new() { Status = HttpStatusCode.Unauthorized };
        }
        if (request.Id is not null)
        {
            bool ok = store.InstanceRead(ctx, out FhirResponseContext response);
            return new() { Status = response.StatusCode, Body = ok ? response.SerializedResource : string.Empty };
        }
        if (!store.TypeSearch(ctx, out FhirResponseContext bundle))
        {
            return new() { Status = bundle.StatusCode };
        }

        using JsonDocument document = JsonDocument.Parse(bundle.SerializedResource);
        return new()
        {
            Status = bundle.StatusCode,
            Rows = document.RootElement.TryGetProperty("entry", out JsonElement entries)
                ? entries.EnumerateArray().Select(e => new InstanceTableRec
                {
                    Id = e.GetProperty("resource").GetProperty("id").GetString()!,
                }).ToArray() : [],
        };
    }

    private static FhirRequestContext Context(IFhirStore store, Request request, AuthorizationInfo? grant, bool metadata = false)
    {
        string path = metadata ? "metadata" : Uri.EscapeDataString(request.Type!);
        if (!metadata && request.Id is not null)
        {
            path += "/" + Uri.EscapeDataString(request.Id);
        }
        return new(store, "GET", $"{store.Config.BaseUrl}/{path}")
        {
            TenantName = request.Tenant,
            Authorization = grant,
            ResourceType = request.Type!,
            Id = request.Id ?? string.Empty,
            Interaction = metadata ? Common.StoreInteractionCodes.SystemCapabilities :
                request.Id is null ? Common.StoreInteractionCodes.TypeSearch : Common.StoreInteractionCodes.InstanceRead,
            DestinationFormat = request.Format == "xml" ? "application/fhir+xml" : "application/fhir+json",
            SerializePretty = true,
        };
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_navigation is not null)
        {
            _navigation.LocationChanged -= OnLocationChanged;
        }
        lock (_gate)
        {
            _disposed = true;
            Reset();
            Expired = null;
        }
    }
}
