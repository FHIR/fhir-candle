// <copyright file="LocalHttpFixture.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using System.Net;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace fhir.candle.Tests.Controllers;

/// <summary>A finite raw-wire peer, not a FHIR server or a Firely serialization oracle.</summary>
internal sealed class LocalHttpFixture : IAsyncDisposable
{
    internal static readonly TimeSpan Deadline = TimeSpan.FromSeconds(30);

    private readonly object _gate = new();
    private readonly CancellationTokenSource _lifetime = new();
    private readonly string _ownedDirectory = Path.Combine(Path.GetTempPath(), $"fhir-candle-sdk6-http-{Guid.NewGuid():N}");
    private readonly List<ExpectedRequest> _expected = [];
    private readonly List<CapturedRequest> _requests = [];
    private readonly List<string> _failures = [];
    private WebApplication? _app;
    private Task? _disposeTask;

    private LocalHttpFixture()
    {
        Directory.CreateDirectory(_ownedDirectory);
    }

    internal Uri Address { get; private set; } = null!;

    internal IReadOnlyList<CapturedRequest> Requests
    {
        get
        {
            lock (_gate)
            {
                return _requests.ToArray();
            }
        }
    }

    internal static async Task<LocalHttpFixture> StartAsync()
    {
        LocalHttpFixture fixture = new();
        try
        {
            WebApplicationBuilder builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
            {
                ApplicationName = typeof(LocalHttpFixture).Assembly.GetName().Name,
                EnvironmentName = "Testing",
                ContentRootPath = fixture._ownedDirectory,
            });
            builder.Configuration.Sources.Clear();
            builder.Logging.ClearProviders();
            builder.WebHost.UseKestrel(options => options.Listen(IPAddress.Loopback, 0));
            WebApplication app = builder.Build();
            fixture._app = app;
            app.Run(fixture.HandleAsync);

            using CancellationTokenSource startup = new(Deadline);
            await app.StartAsync(startup.Token);
            IServer server = app.Services.GetRequiredService<IServer>();
            IServerAddressesFeature addresses = server.Features.Get<IServerAddressesFeature>()
                ?? throw new InvalidOperationException("Kestrel did not publish its bound address.");
            fixture.Address = new Uri(Assert.Single(addresses.Addresses).TrimEnd('/') + "/");
            Assert.Equal("127.0.0.1", fixture.Address.Host);
            Assert.True(fixture.Address.Port > 0);
            return fixture;
        }
        catch
        {
            await fixture.DisposeAsync();
            throw;
        }
    }

    internal ExpectedRequest Expect(string method, string path, RawResponse response) =>
        Expect(method, path, (_, _) => Task.FromResult(response));

    internal ExpectedRequest Expect(
        string method,
        string path,
        Func<CapturedRequest, CancellationToken, Task<RawResponse>> respond,
        bool allowClientCancellation = false)
    {
        if (!path.StartsWith('/') || path.Contains('?') || path.Contains('#'))
        {
            throw new ArgumentException("Supply an exact absolute path; inspect query strings on the capture.", nameof(path));
        }

        ExpectedRequest expected = new(method, path, respond, allowClientCancellation);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposeTask is not null, this);
            _expected.Add(expected);
        }
        return expected;
    }

    internal void AssertComplete()
    {
        lock (_gate)
        {
            Assert.True(_failures.Count == 0, string.Join(Environment.NewLine, _failures));
            Assert.NotEmpty(_expected);
            Assert.All(_expected, expected => Assert.True(expected.Claimed, $"Missing {expected.Method} {expected.Path}"));
            Assert.Equal(_expected.Count, _requests.Count);
        }
    }

    internal MappedLoopbackHandler MapCiRequests(params string[] expectedUris) => new(Address, expectedUris);

    private async Task HandleAsync(HttpContext context)
    {
        ExpectedRequest? expected = null;
        bool canceled = false;
        using CancellationTokenSource requestLifetime = CancellationTokenSource.CreateLinkedTokenSource(
            context.RequestAborted, _lifetime.Token);
        requestLifetime.CancelAfter(Deadline);
        try
        {
            using MemoryStream body = new();
            await context.Request.Body.CopyToAsync(body, requestLifetime.Token);
            CapturedRequest request = new(
                context.Request.Method,
                context.Request.Path.Value ?? string.Empty,
                context.Request.QueryString.Value ?? string.Empty,
                context.Features.Get<IHttpRequestFeature>()?.RawTarget ?? string.Empty,
                context.Request.Headers.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.OrdinalIgnoreCase),
                body.ToArray());

            lock (_gate)
            {
                _requests.Add(request);
                expected = _expected.FirstOrDefault(candidate =>
                    !candidate.Claimed && candidate.Method == request.Method && candidate.Path == request.Path);
                if (expected is null)
                {
                    _failures.Add($"Unexpected request: {request.Method} {request.RawTarget}");
                }
                else
                {
                    expected.Claimed = true;
                }
            }

            if (expected is null)
            {
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await context.Response.WriteAsync("Unexpected local fixture request.", requestLifetime.Token);
                return;
            }

            expected.Captured.TrySetResult(request);
            RawResponse response = await expected.Respond(request, requestLifetime.Token);
            context.Response.StatusCode = (int)response.Status;
            if (response.ContentType is not null)
            {
                context.Response.ContentType = response.ContentType;
            }
            foreach (KeyValuePair<string, string> header in response.Headers)
            {
                context.Response.Headers[header.Key] = header.Value;
            }
            if (response.Status == HttpStatusCode.NoContent)
            {
                if (response.Body.Length != 0)
                {
                    throw new InvalidOperationException($"A 204 local fixture response must have an empty body, but had {response.Body.Length} bytes.");
                }
                return;
            }
            context.Response.ContentLength = response.Body.Length;
            await context.Response.Body.WriteAsync(response.Body, requestLifetime.Token);
        }
        catch (OperationCanceledException) when (requestLifetime.IsCancellationRequested)
        {
            canceled = true;
            if (expected?.AllowClientCancellation != true || !context.RequestAborted.IsCancellationRequested)
            {
                lock (_gate)
                {
                    _failures.Add($"Local response canceled without an expected client cancellation: {context.Request.Path}");
                }
            }
            context.Abort();
        }
        catch (Exception exception)
        {
            lock (_gate)
            {
                _failures.Add($"{context.Request.Path}: {exception}");
            }
            context.Abort();
        }
        finally
        {
            expected?.Finished.TrySetResult(canceled);
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            _disposeTask ??= StopAsync();
            return new ValueTask(_disposeTask);
        }
    }

    private async Task StopAsync()
    {
        await _lifetime.CancelAsync();
        try
        {
            if (_app is not null)
            {
                using CancellationTokenSource shutdown = new(Deadline);
                await _app.StopAsync(shutdown.Token);
            }
        }
        finally
        {
            try
            {
                if (_app is not null)
                {
                    await _app.DisposeAsync();
                }
            }
            finally
            {
                _lifetime.Dispose();
                Directory.Delete(_ownedDirectory, recursive: true);
            }
        }
    }

    internal sealed record CapturedRequest(
        string Method,
        string Path,
        string Query,
        string RawTarget,
        IReadOnlyDictionary<string, string?[]> Headers,
        byte[] Body)
    {
        internal string Text => Encoding.UTF8.GetString(Body);

        internal string Header(string name) =>
            Headers.TryGetValue(name, out string?[]? values) ? string.Join(", ", values) : string.Empty;
    }

    internal sealed record RawResponse(HttpStatusCode Status, string? ContentType, byte[] Body)
    {
        internal IReadOnlyDictionary<string, string> Headers { get; init; } = new Dictionary<string, string>();

        internal static RawResponse Text(HttpStatusCode status, string contentType, string body) =>
            new(status, contentType, Encoding.UTF8.GetBytes(body));

        internal static RawResponse Empty(HttpStatusCode status = HttpStatusCode.NoContent) => new(status, null, []);
    }

    internal sealed class ExpectedRequest(
        string method,
        string path,
        Func<CapturedRequest, CancellationToken, Task<RawResponse>> respond,
        bool allowClientCancellation)
    {
        internal string Method { get; } = method;
        internal string Path { get; } = path;
        internal Func<CapturedRequest, CancellationToken, Task<RawResponse>> Respond { get; } = respond;
        internal bool AllowClientCancellation { get; } = allowClientCancellation;
        internal bool Claimed { get; set; }
        internal TaskCompletionSource<CapturedRequest> Captured { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource<bool> Finished { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal Task<CapturedRequest> WaitForRequestAsync() => Captured.Task.WaitAsync(Deadline);

        // True means the peer canceled, not that the authored response was successfully delivered.
        internal Task<bool> WaitForCompletionAsync() => Finished.Task.WaitAsync(Deadline);
    }

    /// <summary>Maps a finite, ordered CI request list before the handler can perform any outbound I/O.</summary>
    internal sealed class MappedLoopbackHandler : DelegatingHandler
    {
        private readonly object _gate = new();
        private readonly Uri _listener;
        private readonly Queue<string> _remaining;
        private readonly List<string> _forwarded = [];

        internal MappedLoopbackHandler(Uri listener, IEnumerable<string> expectedUris)
            : base(new SocketsHttpHandler { AllowAutoRedirect = false, UseProxy = false })
        {
            _listener = listener;
            _remaining = new Queue<string>(expectedUris);
            if (_remaining.Count == 0 || _remaining.Any(value =>
                !Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) ||
                uri.Scheme != "https" || uri.Host != "build.fhir.org" || !uri.IsDefaultPort))
            {
                throw new ArgumentException("Only explicitly enumerated build.fhir.org HTTPS requests may be mapped.", nameof(expectedUris));
            }
        }

        internal IReadOnlyList<string> Forwarded
        {
            get
            {
                lock (_gate)
                {
                    return _forwarded.ToArray();
                }
            }
        }

        internal void AssertComplete()
        {
            lock (_gate)
            {
                Assert.Empty(_remaining);
            }
        }

        /// <inheritdoc />
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string original = request.RequestUri?.AbsoluteUri ?? string.Empty;
            lock (_gate)
            {
                if (request.Method != HttpMethod.Get || _remaining.Count == 0 || _remaining.Peek() != original)
                {
                    throw new InvalidOperationException($"Blocked unexpected CI request before outbound I/O: {request.Method} {original}");
                }
                _remaining.Dequeue();
                _forwarded.Add(original);
            }

            request.Headers.Add("X-Sdk6-Original-Uri", original);
            request.RequestUri = new Uri(_listener, request.RequestUri!.PathAndQuery);
            return base.SendAsync(request, cancellationToken);
        }
    }
}
