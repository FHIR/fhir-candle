// <copyright file="McpTransportCompatibilityTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using System.CommandLine;
using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using fhir.candle.McpTools;
using fhir.candle.Services;
using fhir.candle.Tests.Controllers;
using fhir.candle.Tests.Extensions;
using FhirCandle.Configuration;
using FhirCandle.Storage;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace fhir.candle.Tests.McpTests;

/// <summary>Checks the real RunServer MCP registration and routes over loopback HTTP.</summary>
[Collection(CompatibilityHostCollection.Name)]
public sealed class McpTransportCompatibilityTests
{
    private const string ProtocolVersion = "2025-11-25";
    private static readonly TimeSpan _deadline = TimeSpan.FromSeconds(30);

    /// <summary>Initialization retains a session, all nine tools, schemas and GET/DELETE lifecycle.</summary>
    [Fact]
    public async Task HttpTransportInitializesAndListsTools()
    {
        await using ProductionHost host = new();
        await host.StartAsync(true);
        await host.InitializeAsync();
        using JsonDocument listed = await host.RequestAsync(2, "tools/list", new { });
        JsonElement[] tools = listed.RootElement.GetProperty("result").GetProperty("tools").EnumerateArray().ToArray();
        Assert.Equal(new[]
        {
            "getDataTypeDefinition", "getDataTypeList", "getResourceDefinition", "getResourceList",
            "getSearchParameters", "getSearchTypeDefinition", "getSearchTypeList", "getStoreList", "validateTypeSearch",
        }, tools.Select(tool => tool.GetProperty("name").GetString()).OrderBy(name => name, StringComparer.Ordinal));
        Assert.All(tools, tool =>
        {
            Assert.False(string.IsNullOrWhiteSpace(tool.GetProperty("description").GetString()));
            Assert.Equal(JsonValueKind.Object, tool.GetProperty("inputSchema").ValueKind);
        });
        JsonElement resourceList = tools.Single(tool => tool.GetProperty("name").GetString() == "getResourceList");
        JsonElement schema = resourceList.GetProperty("inputSchema");
        Assert.Equal("object", schema.GetProperty("type").GetString());
        Assert.Equal("string", schema.GetProperty("properties").GetProperty("store").GetProperty("type").GetString());
        Assert.Equal("store", Assert.Single(schema.GetProperty("required").EnumerateArray()).GetString());

        using (HttpRequestMessage request = new(HttpMethod.Get, "/mcp"))
        using (HttpResponseMessage stream = await host.Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead))
        {
            Assert.Equal(HttpStatusCode.OK, stream.StatusCode);
            Assert.Equal("text/event-stream", stream.Content.Headers.ContentType?.MediaType);
        }
        using HttpResponseMessage deleted = await host.Client.DeleteAsync("/mcp");
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        using HttpResponseMessage stale = await host.PostAsync("""{"jsonrpc":"2.0","id":3,"method":"tools/list","params":{}}""");
        Assert.Equal(HttpStatusCode.NotFound, stale.StatusCode);
        Console.WriteLine("MCP: initialize/list nine tools; GET stream closed; DELETE 200; deleted session 404.");
    }

    /// <summary>The production call handler returns the exact three synthetic tenant descriptions.</summary>
    [Fact]
    public async Task HttpTransportCallsStoreList()
    {
        await using ProductionHost host = new();
        await host.StartAsync(true);
        await host.InitializeAsync();
        using JsonDocument called = await host.RequestAsync(2, "tools/call", new { name = "getStoreList", arguments = new { } });
        Assert.Equal(new[]
        {
            "mcp-r4: URL is http://127.0.0.1/fhir/mcp-r4/, FHIR version is R4",
            "mcp-r4b: URL is http://127.0.0.1/fhir/mcp-r4b/, FHIR version is R4B",
            "mcp-r5: URL is http://127.0.0.1/fhir/mcp-r5/, FHIR version is R5",
        }, TextResults(called));
        Console.WriteLine("MCP: tools/call getStoreList returned exact empty-source R4/R4B/R5 tenant content.");
    }

    /// <summary>Invalid tool requests retain Candle's text responses, not invented protocol errors.</summary>
    [Theory]
    [InlineData("missingTool", "Unknown tool: missingTool.")]
    [InlineData("getResourceList", "Store name is missing or not provided and is required")]
    public async Task HttpTransportInvalidToolRequestPreservesResponse(string name, string expected)
    {
        await using ProductionHost host = new();
        await host.StartAsync(true);
        await host.InitializeAsync();
        using JsonDocument called = await host.RequestAsync(2, "tools/call", new { name, arguments = new { } });
        Assert.Equal(expected, Assert.Single(TextResults(called)));
        Console.WriteLine($"MCP: invalid tools/call {name}: {expected}");
    }

    /// <summary>An unsupported JSON-RPC method returns MethodNotFound without killing the session.</summary>
    [Fact]
    public async Task HttpTransportUnsupportedMethodReturnsProtocolError()
    {
        await using ProductionHost host = new();
        await host.StartAsync(true);
        await host.InitializeAsync();
        using JsonDocument failed = await host.RequestAsync(2, "candle/unsupported", new { });
        Assert.Equal(-32601, failed.RootElement.GetProperty("error").GetProperty("code").GetInt32());
        using JsonDocument called = await host.RequestAsync(3, "tools/call", new { name = "getStoreList", arguments = new { } });
        Assert.Equal(3, TextResults(called).Length);
        Console.WriteLine("MCP: unsupported method -32601; same session remains callable.");
    }

    /// <summary>Both omitted and explicitly false enablement leave the production MCP route absent.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData(false)]
    public async Task DisabledMcpDoesNotMapTransport(bool? enabled)
    {
        await using ProductionHost host = new();
        await host.StartAsync(enabled);
        Assert.Null(host.Services.GetService<FhirMcpTools>());
        using HttpResponseMessage post = await host.PostAsync(InitializeRequest());
        Assert.Equal(HttpStatusCode.NotFound, post.StatusCode);
        using HttpResponseMessage get = await host.Client.GetAsync("/mcp");
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
        Console.WriteLine($"MCP: enable={enabled?.ToString() ?? "omitted"}; no tool singleton; POST/GET /mcp 404.");
    }

    private static string InitializeRequest() => JsonSerializer.Serialize(new
    {
        jsonrpc = "2.0", id = 1, method = "initialize",
        @params = new { protocolVersion = ProtocolVersion, capabilities = new { }, clientInfo = new { name = "candle-compatibility", version = "1.0" } },
    });

    private static string[] TextResults(JsonDocument response)
    {
        JsonElement result = response.RootElement.GetProperty("result");
        Assert.False(result.TryGetProperty("isError", out JsonElement error) && error.GetBoolean());
        Assert.False(result.TryGetProperty("structuredContent", out _));
        return result.GetProperty("content").EnumerateArray().Select(content =>
        {
            Assert.Equal("text", content.GetProperty("type").GetString());
            return content.GetProperty("text").GetString()!;
        }).ToArray();
    }

    // Same diagnostic boundary as HostingCompatibilityTests: no copied MCP registration,
    // fake handler, public startup facade, or controller-only BuildAppForTesting.
    private sealed class ProductionHost : IAsyncDisposable, IObserver<DiagnosticListener>, IObserver<KeyValuePair<string, object?>>
    {
        private static readonly AsyncLocal<ProductionHost?> _current = new();
        private readonly LocalPackageFixture _files = new();
        private readonly CancellationTokenSource _shutdown = new();
        private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly Dictionary<string, string?> _environment = new(StringComparer.Ordinal);
        private readonly List<IDisposable> _subscriptions = [];
        private readonly RSA _key = RSA.Create(2048);
        private readonly X509Certificate2 _certificate;
        private readonly Dictionary<FieldInfo, object?> _statics = new();
        private IHost? _host;
        private Task? _run;
        private HttpClient? _client;

        internal ProductionHost()
        {
            CertificateRequest request = new("CN=fhir-candle-mcp-tests", _key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            _certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1));
            foreach ((Type type, string name) in new[]
            {
                (typeof(FhirStoreManager), "<Instance>k__BackingField"),
                (typeof(FhirPackageService), "_singleton"), (typeof(Program), "_retVal"),
            })
            {
                FieldInfo field = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Static)
                    ?? throw new InvalidOperationException($"Missing process-state field: {type.Name}.{name}");
                _statics.Add(field, field.GetValue(null));
            }
        }

        internal IServiceProvider Services => (_host ?? throw new InvalidOperationException("Host was not built.")).Services;
        internal HttpClient Client => _client ?? throw new InvalidOperationException("Host was not started.");
        private IHostApplicationLifetime Lifetime => Services.GetRequiredService<IHostApplicationLifetime>();

        internal async Task StartAsync(bool? enabled)
        {
            foreach (string prefix in new[] { "DOTNET_", "ASPNETCORE_" })
            {
                SetEnvironment(prefix + "APPLICATIONNAME", typeof(Program).Assembly.GetName().Name!);
                SetEnvironment(prefix + "CONTENTROOT", _files.Root);
                SetEnvironment(prefix + "ENVIRONMENT", Environments.Production);
            }
            _subscriptions.Add(DiagnosticListener.AllListeners.Subscribe(this));
            List<string> arguments =
            [
                "--r4", "mcp-r4", "--r4b", "mcp-r4b", "--r5", "mcp-r5",
                "--port", "0", "--url", "http://127.0.0.1", "--open-browser", "false", "--headless", "true",
                "--fhir-source", _files.SourceDirectory, "--fhir-package-cache", _files.CacheDirectory,
                "--use-official-registries", "false",
            ];
            if (enabled.HasValue)
            {
                arguments.AddRange(["--enable-mcp", enabled.Value.ToString()]);
            }
            CliRootCommand command = new();
            ParseResult parsed = command.Parse(arguments.ToArray(), new ParserConfiguration { ResponseFileTokenReplacer = null });
            Assert.Empty(parsed.Errors);
            IConfiguration external = new ConfigurationBuilder().Build();
            _run = Task.Run(async () =>
            {
                _current.Value = this;
                try
                {
                    await Program.RunServer(parsed, external, _shutdown.Token);
                }
                finally
                {
                    _current.Value = null;
                }
            });
            Task completed = await Task.WhenAny(_started.Task, _run).WaitAsync(_deadline);
            Assert.Same(_started.Task, completed);
            await _started.Task;
            Assert.False(_run.IsCompleted);
            Assert.Equal(enabled == true, Services.GetRequiredService<CandleConfig>().EnableMcp);
            IFhirStoreManager stores = Services.GetRequiredService<IFhirStoreManager>();
            Assert.Same(stores, FhirStoreManager.IInstance);
            Assert.Equal(new[] { "mcp-r4", "mcp-r4b", "mcp-r5" }, stores.Keys.OrderBy(name => name, StringComparer.Ordinal));
            Assert.All(stores.Values, store =>
            {
                Assert.Empty(store["Patient"].Keys);
                Assert.Empty(store.CurrentSubscriptions);
            });
            IServerAddressesFeature addresses = Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()
                ?? throw new InvalidOperationException("No Kestrel address.");
            Uri address = new(Assert.Single(addresses.Addresses));
            Assert.Equal(IPAddress.Loopback.ToString(), address.Host);
            _client = new HttpClient(new HttpClientHandler { UseProxy = false }) { BaseAddress = address, Timeout = _deadline };
            Client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            Client.DefaultRequestHeaders.Accept.ParseAdd("text/event-stream");
            Assembly[] mcpAssemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(assembly => assembly.GetName().Name?.StartsWith("ModelContextProtocol", StringComparison.Ordinal) == true)
                .OrderBy(assembly => assembly.GetName().Name, StringComparer.Ordinal).ToArray();
            if (enabled == true)
            {
                Assert.Equal(new[] { "ModelContextProtocol", "ModelContextProtocol.AspNetCore", "ModelContextProtocol.Core" },
                    mcpAssemblies.Select(assembly => assembly.GetName().Name));
            }
            foreach (Assembly assembly in mcpAssemblies.Append(typeof(IHostedService).Assembly))
            {
                Console.WriteLine($"MCP loaded: {assembly.GetName().Name}; {assembly.Location}; file {FileVersionInfo.GetVersionInfo(assembly.Location).FileVersion}");
            }
        }

        internal async Task InitializeAsync()
        {
            using HttpResponseMessage response = await PostAsync(InitializeRequest());
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            string session = Assert.Single(response.Headers.GetValues("Mcp-Session-Id"));
            Assert.False(string.IsNullOrWhiteSpace(session));
            using JsonDocument initialized = await ReadRpcAsync(response, 1);
            JsonElement result = initialized.RootElement.GetProperty("result");
            Assert.Equal(ProtocolVersion, result.GetProperty("protocolVersion").GetString());
            Assert.Equal(JsonValueKind.Object, result.GetProperty("capabilities").GetProperty("tools").ValueKind);
            Assert.False(string.IsNullOrWhiteSpace(result.GetProperty("serverInfo").GetProperty("name").GetString()));
            Client.DefaultRequestHeaders.Add("Mcp-Session-Id", session);
            Client.DefaultRequestHeaders.Add("MCP-Protocol-Version", ProtocolVersion);
            using HttpResponseMessage notification = await PostAsync("""{"jsonrpc":"2.0","method":"notifications/initialized"}""");
            Assert.Equal(HttpStatusCode.Accepted, notification.StatusCode);
        }

        internal async Task<JsonDocument> RequestAsync(int id, string method, object parameters)
        {
            using HttpResponseMessage response = await PostAsync(JsonSerializer.Serialize(new { jsonrpc = "2.0", id, method, @params = parameters }));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return await ReadRpcAsync(response, id);
        }

        internal async Task<HttpResponseMessage> PostAsync(string json)
        {
            using StringContent content = new(json, Encoding.UTF8, "application/json");
            return await Client.PostAsync("/mcp", content);
        }

        private static async Task<JsonDocument> ReadRpcAsync(HttpResponseMessage response, int id)
        {
            string body = await response.Content.ReadAsStringAsync();
            Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);
            string data = Assert.Single(body.Split('\n'), line => line.StartsWith("data: ", StringComparison.Ordinal));
            JsonDocument document = JsonDocument.Parse(data["data: ".Length..]);
            Assert.Equal("2.0", document.RootElement.GetProperty("jsonrpc").GetString());
            Assert.Equal(id, document.RootElement.GetProperty("id").GetInt32());
            return document;
        }

        private void SetEnvironment(string name, string value)
        {
            _environment.Add(name, Environment.GetEnvironmentVariable(name));
            Environment.SetEnvironmentVariable(name, value);
        }

        /// <inheritdoc />
        public void OnNext(DiagnosticListener listener)
        {
            if (listener.Name == "Microsoft.Extensions.Hosting" && ReferenceEquals(_current.Value, this))
            {
                _subscriptions.Add(listener.Subscribe(this));
            }
        }

        /// <inheritdoc />
        public void OnNext(KeyValuePair<string, object?> diagnostic)
        {
            if (diagnostic.Key == "HostBuilding" && diagnostic.Value is IHostBuilder builder)
            {
                builder.ConfigureAppConfiguration((context, configuration) =>
                {
                    configuration.Sources.Clear();
                    configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        [HostDefaults.ApplicationKey] = context.HostingEnvironment.ApplicationName,
                        [HostDefaults.EnvironmentKey] = context.HostingEnvironment.EnvironmentName,
                        [HostDefaults.ContentRootKey] = context.HostingEnvironment.ContentRootPath,
                        [WebHostDefaults.ServerUrlsKey] = "http://127.0.0.1:0",
                    });
                });
                builder.ConfigureServices((_, services) =>
                {
                    // Isolate the warmup repository as well as the provider: never read the real user key ring.
                    services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(_files.CreateDirectory("keys")))
                        .ProtectKeysWithCertificate(_certificate);
                    services.AddLogging(logging => logging.ClearProviders());
                    services.Configure<HostOptions>(options => options.ShutdownTimeout = _deadline);
                });
            }
            else if (diagnostic.Key == "HostBuilt" && diagnostic.Value is IHost host)
            {
                _host = host;
                _subscriptions.Add(Lifetime.ApplicationStarted.Register(() => _started.TrySetResult()));
            }
        }

        /// <inheritdoc />
        public void OnCompleted()
        {
        }

        /// <inheritdoc />
        public void OnError(Exception error) => _started.TrySetException(error);

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            _client?.Dispose();
            try
            {
                _shutdown.Cancel();
                if (_run is not null)
                {
                    await _run.WaitAsync(_deadline);
                    Assert.True(Lifetime.ApplicationStopping.IsCancellationRequested);
                    Assert.True(Lifetime.ApplicationStopped.IsCancellationRequested);
                }
            }
            finally
            {
                try
                {
                    if (_host is not null)
                    {
                        IFhirStore[] stores = Services.GetRequiredService<IFhirStoreManager>().Values.ToArray();
                        try
                        {
                            try
                            {
                                using CancellationTokenSource cleanup = new(_deadline);
                                await _host.StopAsync(cleanup.Token).WaitAsync(_deadline);
                            }
                            finally
                            {
                                await ((IAsyncDisposable)_host).DisposeAsync().AsTask().WaitAsync(_deadline);
                            }
                        }
                        finally
                        {
                            foreach (IFhirStore store in stores)
                            {
                                store.Dispose();
                            }
                        }
                    }
                }
                finally
                {
                    foreach (IDisposable subscription in _subscriptions)
                    {
                        subscription.Dispose();
                    }
                    foreach ((string name, string? value) in _environment)
                    {
                        Environment.SetEnvironmentVariable(name, value);
                    }
                    foreach ((FieldInfo field, object? value) in _statics)
                    {
                        field.SetValue(null, value);
                    }
                    _shutdown.Dispose();
                    _certificate.Dispose();
                    _key.Dispose();
                    try
                    {
                        Assert.Empty(Directory.EnumerateFileSystemEntries(_files.SourceDirectory));
                        Assert.Empty(Directory.EnumerateFileSystemEntries(_files.CacheDirectory));
                    }
                    finally
                    {
                        _files.Dispose();
                    }
                    Assert.False(Directory.Exists(_files.Root));
                    Console.WriteLine("MCP: host stopped; client/stores/listeners disposed; statics/environment restored; TEMP root removed.");
                }
            }
        }
    }
}
