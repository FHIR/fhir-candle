// <copyright file="PerformanceEvidenceTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using System.Buffers.Binary;
using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FhirCandle.Performance;
using FhirCandle.Performance.Probe;

namespace fhir.candle.Tests;

/// <summary>Tooling negative controls; these fixtures are never released-artifact feasibility evidence.</summary>
public class PerformanceEvidenceTests
{
    // Pure configured identity; these fixtures never read or create this path.
    private static readonly string _sourceDirectory = OperatingSystem.IsWindows()
        ? @"C:\candle-perf-fixture\child\empty-source" : "/candle-perf-fixture/child/empty-source";

    /// <summary>The manifest cannot silently select another release, source, runtime, output or concurrency.</summary>
    [Fact]
    public void ManifestRejectsWrongArtifactOrRuntime()
    {
        ExperimentManifest manifest = Manifest();
        ExperimentRunner.ValidateManifest(manifest);
        ExperimentManifest[] invalid = [
            manifest with { Baseline = manifest.Baseline with { PackageVersion = "2025.611.1423" } },
            manifest with { Baseline = manifest.Baseline with { PackageUrl = "https://example.org/substitute.nupkg" } },
            manifest with { Baseline = manifest.Baseline with { FirelyPackageVersion = "6.2.1" } },
            manifest with { Candidate = manifest.Candidate with { SourceRevision = ExperimentRunner.BaselineRevision } },
            manifest with { Runtime = manifest.Runtime with { RuntimeVersion = "9.0.0" } },
            manifest with { Runtime = manifest.Runtime with { RuntimeVersion = "8.0.29" } },
            manifest with { Runtime = manifest.Runtime with { AspNetCoreVersion = "8.0.28" } },
            manifest with { Runtime = manifest.Runtime with { DotnetHostPath = @"C:\other\dotnet.exe" } },
            manifest with { Runtime = manifest.Runtime with { Architecture = "X86" } },
            manifest with { ReportPath = @"C:\ai\git\fhir-candle\scratch\other\performance.md" },
            manifest with { OutputDirectory = ExperimentRunner.DataRoot + @"\another-run" },
            manifest with { ProbePath = manifest.OutputDirectory + @"\probe.dll" },
            manifest with { RunId = @"..\escape" },
            manifest with { ProtocolVersion = "another-protocol" },
            manifest with { HttpWorkers = 2 },
            manifest with { Isolation = manifest.Isolation with { Authorized = false } },
            manifest with { Bounds = manifest.Bounds with { StartupSeconds = int.MaxValue } },
        ];
        foreach (ExperimentManifest wrong in invalid)
        {
            Assert.Throws<InvalidDataException>(() => ExperimentRunner.ValidateManifest(wrong));
        }
    }

    /// <summary>The launch presence gate uses sealed package ownership, not a historical filename-suffix guess.</summary>
    [Fact]
    public void RequiredLoadedModelUsesSealedPackageOwnership()
    {
        foreach (string fhirVersion in new[] { "R4", "R4B", "R5" })
        {
            foreach (string variant in new[] { "baseline", "candidate" })
            {
                (ArtifactIdentity artifact, AssemblyCorrelation[] correlations) = RequiredAssemblyFixture(variant, fhirVersion);
                AssemblyCorrelation model = correlations[0];
                AssemblyCorrelation[] common = correlations[1..];
                DependencyIdentity package = Assert.Single(artifact.Dependencies);
                ExperimentRunner.ValidateRequiredLoadedAssemblies(artifact, fhirVersion, correlations);

                AssemblyCorrelation[] invalidModels = [
                    model with { Dependency = "Hl7.Fhir.Base" },
                    model with { Dependency = "Hl7.Fhir." + (fhirVersion == "R4" ? "R4B" : "R4") },
                    model with { Dependency = package.Name + ".Core" },
                    model with { DependencyVersion = variant == "baseline" ? "6.2.1" : "5.11.4" },
                    model with { DependencyVersion = package.Version + ".0" },
                    model with { DependencySha512 = "not-the-sealed-package" },
                    model with { Origin = variant == "baseline" ? "candidate" : "baseline" },
                    model with { Origin = "Microsoft.NETCore.App/8.0.30" },
                    model with { Loaded = model.Loaded with { Name = model.Loaded.Name + ".Core" } },
                    model with
                    {
                        Loaded = model.Loaded with
                        {
                            Path = model.Loaded.Path.Replace(artifact.RootDirectory, artifact.RootDirectory + "-other", StringComparison.Ordinal),
                        },
                    },
                    model with { Loaded = model.Loaded with { Path = artifact.RootDirectory + "\\unsealed\\" + model.Loaded.Name + ".dll" } },
                ];
                foreach (AssemblyCorrelation wrong in invalidModels)
                {
                    InvalidDataException failure = Assert.Throws<InvalidDataException>(() =>
                        ExperimentRunner.ValidateRequiredLoadedAssemblies(artifact, fhirVersion, [wrong, .. common]));
                    Assert.Contains("Missing validated loaded addressed model", failure.Message, StringComparison.Ordinal);
                    Assert.Contains(package.Name + "/" + package.Version, failure.Message, StringComparison.Ordinal);
                    Assert.Contains(variant + "/" + fhirVersion, failure.Message, StringComparison.Ordinal);
                    Assert.Contains(artifact.RootDirectory, failure.Message, StringComparison.Ordinal);
                }

                InvalidDataException missing = Assert.Throws<InvalidDataException>(() =>
                    ExperimentRunner.ValidateRequiredLoadedAssemblies(artifact, fhirVersion, common));
                Assert.Contains(model.Loaded.Name, missing.Message, StringComparison.Ordinal);
                Assert.Contains(artifact.Assemblies[0].RelativePath, missing.Message, StringComparison.Ordinal);
                ArtifactIdentity[] invalidArtifacts = [
                    artifact with { Dependencies = [] },
                    artifact with { Dependencies = [package, package] },
                    artifact with { Dependencies = [package with { Name = package.Name + ".Core" }] },
                    artifact with { Dependencies = [package with { Version = package.Version + ".0" }] },
                    artifact with { Dependencies = [package with { Type = "project" }] },
                    artifact with { Dependencies = [package with { RuntimeAssets = [] }] },
                    artifact with { Dependencies = [package with { RuntimeAssets = ["lib/net8.0/" + model.Loaded.Name + ".Core.dll"] }] },
                    artifact with { Assemblies = [] },
                    artifact with { Assemblies = [artifact.Assemblies[0] with { Name = model.Loaded.Name + ".Core" }] },
                ];
                foreach (ArtifactIdentity wrong in invalidArtifacts)
                {
                    InvalidDataException failure = Assert.Throws<InvalidDataException>(() =>
                        ExperimentRunner.ValidateRequiredLoadedAssemblies(wrong, fhirVersion, correlations));
                    Assert.Contains(package.Name + "/" + package.Version, failure.Message, StringComparison.Ordinal);
                    Assert.Contains(artifact.RootDirectory, failure.Message, StringComparison.Ordinal);
                }

                // A suffixed PE is supported only by matching sealed metadata, not by either spelling alone.
                (ArtifactIdentity suffixed, AssemblyCorrelation[] suffixedCorrelations) =
                    RequiredAssemblyFixture(variant, fhirVersion, model.Loaded.Name + ".Core");
                ExperimentRunner.ValidateRequiredLoadedAssemblies(suffixed, fhirVersion, suffixedCorrelations);
                Assert.Throws<InvalidDataException>(() =>
                    ExperimentRunner.ValidateRequiredLoadedAssemblies(artifact, fhirVersion, suffixedCorrelations));
                Assert.Throws<InvalidDataException>(() =>
                    ExperimentRunner.ValidateRequiredLoadedAssemblies(suffixed, fhirVersion, correlations));
                ArtifactIdentity decoy = artifact with
                {
                    Files = [.. artifact.Files, .. suffixed.Files],
                    Assemblies = [.. artifact.Assemblies, .. suffixed.Assemblies],
                };
                Assert.Throws<InvalidDataException>(() =>
                    ExperimentRunner.ValidateRequiredLoadedAssemblies(decoy, fhirVersion, suffixedCorrelations));
                if (variant == "baseline")
                {
                    AssemblyFile otherFramework = artifact.Assemblies[0] with
                    {
                        RelativePath = artifact.Assemblies[0].RelativePath.Replace("net8.0", "net9.0", StringComparison.Ordinal),
                    };
                    AssemblyCorrelation otherClosure = model with
                    {
                        Loaded = model.Loaded with { Path = artifact.RootDirectory + "\\" + otherFramework.RelativePath },
                    };
                    Assert.Throws<InvalidDataException>(() => ExperimentRunner.ValidateRequiredLoadedAssemblies(
                        artifact with { Assemblies = [otherFramework] }, fhirVersion, [otherClosure, .. common]));
                }

                foreach (AssemblyCorrelation required in common)
                {
                    Assert.Throws<InvalidDataException>(() => ExperimentRunner.ValidateRequiredLoadedAssemblies(
                        artifact, fhirVersion, correlations.Where(item => item != required).ToArray()));
                }

                foreach (string runtimeAssembly in new[] { "System.Private.CoreLib", "Microsoft.AspNetCore.Server.Kestrel.Core" })
                {
                    AssemblyCorrelation[] wrongRuntime = correlations.Select(item => item.Loaded.Name == runtimeAssembly
                        ? item with { Origin = item.Origin.Replace("8.0.30", "9.0.0", StringComparison.Ordinal) } : item).ToArray();
                    Assert.Throws<InvalidDataException>(() =>
                        ExperimentRunner.ValidateRequiredLoadedAssemblies(artifact, fhirVersion, wrongRuntime));
                }

                Assert.Throws<InvalidDataException>(() => ExperimentRunner.ValidateRequiredLoadedAssemblies(
                    artifact, fhirVersion, [.. correlations, model with { Loaded = model.Loaded with { Name = "FhirCandle.Performance" } }]));
            }
        }
    }

    /// <summary>The real launch transformation removes legacy policies only from its exact-runtime child copy.</summary>
    [Fact]
    public void ChildRuntimeConfigPinsFrameworksWithoutMutatingSource()
    {
        byte[] sourceBytes = Encoding.UTF8.GetBytes("""
            {
              "runtimeOptions": {
                "tfm": "net8.0",
                "applyPatches": false,
                "rollForwardOnNoCandidateFx": 0,
                "frameworks": [
                  { "name": "Microsoft.AspNetCore.App", "version": "8.0.0", "rollForward": "LatestPatch" },
                  { "name": "Microsoft.NETCore.App", "version": "8.0.12", "applyPatches": true, "rollForwardOnNoCandidateFx": 2 }
                ],
                "additionalProbingPaths": ["unchanged-probing-path"],
                "configProperties": {
                  "System.GC.Server": false,
                  "System.GC.Concurrent": false,
                  "System.Runtime.TieredCompilation": false,
                  "System.Runtime.TieredPGO": false,
                  "System.Globalization.Invariant": false
                }
              },
              "fixtureMetadata": { "values": [1, "unchanged"] }
            }
            """);
        byte[] originalBytes = sourceBytes.ToArray();
        JsonObject source = JsonNode.Parse(sourceBytes)!.AsObject();
        string originalJson = source.ToJsonString();
        RuntimeSettings[] settings = [
            new(),
            new() { ServerGc = false, ConcurrentGc = true, TieredCompilation = false, TieredPgo = true },
            new() { ServerGc = true, ConcurrentGc = false, TieredCompilation = true, TieredPgo = false },
        ];
        foreach (RuntimeSettings runtime in settings)
        {
            JsonObject derived = ExperimentRunner.CreateChildRuntimeConfig(source, runtime);
            JsonObject options = derived["runtimeOptions"]!.AsObject();
            JsonObject sourceOptions = source["runtimeOptions"]!.AsObject();
            Assert.NotSame(source, derived);
            Assert.NotSame(sourceOptions, options);
            Assert.Equal("net8.0", options["tfm"]!.GetValue<string>());
            Assert.Equal("Disable", options["rollForward"]!.GetValue<string>());
            Assert.False(options.ContainsKey("applyPatches"));
            Assert.False(options.ContainsKey("rollForwardOnNoCandidateFx"));
            JsonArray frameworks = options["frameworks"]!.AsArray();
            JsonArray originalFrameworks = sourceOptions["frameworks"]!.AsArray();
            Assert.Equal(originalFrameworks.Count, frameworks.Count);
            for (int index = 0; index < frameworks.Count; index++)
            {
                JsonObject framework = frameworks[index]!.AsObject();
                Assert.Equal(originalFrameworks[index]!["name"]!.GetValue<string>(), framework["name"]!.GetValue<string>());
                Assert.Equal("8.0.30", framework["version"]!.GetValue<string>());
                Assert.Equal("Disable", framework["rollForward"]!.GetValue<string>());
                Assert.False(framework.ContainsKey("applyPatches"));
                Assert.False(framework.ContainsKey("rollForwardOnNoCandidateFx"));
            }

            JsonObject properties = options["configProperties"]!.AsObject();
            Assert.Equal(runtime.ServerGc, properties["System.GC.Server"]!.GetValue<bool>());
            Assert.Equal(runtime.ConcurrentGc, properties["System.GC.Concurrent"]!.GetValue<bool>());
            Assert.Equal(runtime.TieredCompilation, properties["System.Runtime.TieredCompilation"]!.GetValue<bool>());
            Assert.Equal(runtime.TieredPgo, properties["System.Runtime.TieredPGO"]!.GetValue<bool>());
            Assert.False(properties["System.Globalization.Invariant"]!.GetValue<bool>());
            Assert.True(JsonNode.DeepEquals(sourceOptions["additionalProbingPaths"], options["additionalProbingPaths"]));
            Assert.True(JsonNode.DeepEquals(source["fixtureMetadata"], derived["fixtureMetadata"]));
            Assert.Equal(originalJson, source.ToJsonString());
            Assert.Equal(originalBytes, sourceBytes);
        }
    }

    /// <summary>An exact child's exit fails the real pipe wait with diagnostics, without waiting for the startup deadline.</summary>
    [Fact]
    public async Task ProbeConnectionReportsOwnedChildExit()
    {
        const int ExitCode = 23;
        ProcessStartInfo start = new(OperatingSystem.IsWindows()
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe") : "/bin/sh")
        {
            UseShellExecute = false, CreateNoWindow = true,
        };
        if (OperatingSystem.IsWindows())
        {
            start.ArgumentList.Add("/d");
            start.ArgumentList.Add("/c");
        }
        else
        {
            start.ArgumentList.Add("-c");
        }

        start.ArgumentList.Add("exit " + ExitCode.ToString(CultureInfo.InvariantCulture));
        using Process child = new() { StartInfo = start };
        using NamedPipeServerStream pipe = ProbeProtocol.CreateServer("candle-perf-" + Guid.NewGuid().ToString("N"));
        using CancellationTokenSource deadline = new(TimeSpan.FromSeconds(10));
        string stderrPath = Path.Combine(Path.GetTempPath(), "candle-perf-fixture", "stderr.log");
        Assert.True(child.Start());
        try
        {
            InvalidDataException failure = await Assert.ThrowsAsync<InvalidDataException>(() =>
                ExperimentRunner.WaitForProbeConnectionAsync(pipe, child, stderrPath, deadline.Token));
            Assert.False(deadline.IsCancellationRequested);
            Assert.True(child.HasExited);
            Assert.Equal(ExitCode, child.ExitCode);
            Assert.Contains("PID " + child.Id.ToString(CultureInfo.InvariantCulture), failure.Message, StringComparison.Ordinal);
            Assert.Contains("exit code " + ExitCode.ToString(CultureInfo.InvariantCulture), failure.Message, StringComparison.Ordinal);
            Assert.Contains(stderrPath, failure.Message, StringComparison.Ordinal);
        }
        finally
        {
            if (!child.HasExited)
            {
                child.Kill(entireProcessTree: true);
            }

            using CancellationTokenSource cleanup = new(TimeSpan.FromSeconds(10));
            await child.WaitForExitAsync(cleanup.Token);
        }
    }

    /// <summary>Cancellation drains the pending accept; a later connection succeeds without exiting or disposing the process.</summary>
    [Fact]
    public async Task ProbeConnectionPreservesCancellationAndCallerOwnership()
    {
        using Process child = Process.GetCurrentProcess();
        string pipeName = "candle-perf-" + Guid.NewGuid().ToString("N");
        using NamedPipeServerStream pipe = ProbeProtocol.CreateServer(pipeName);
        using NamedPipeClientStream client = new(".", pipeName, PipeDirection.InOut,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        using CancellationTokenSource deadline = new(TimeSpan.FromSeconds(10));
        using CancellationTokenSource startup = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
        string stderrPath = Path.Combine(Path.GetTempPath(), "candle-perf-fixture", "stderr.log");
        Task canceled = ExperimentRunner.WaitForProbeConnectionAsync(pipe, child, stderrPath, startup.Token);
        startup.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => canceled.WaitAsync(deadline.Token));
        Assert.True(canceled.IsCanceled);
        Assert.False(pipe.IsConnected);

        Task connected = ExperimentRunner.WaitForProbeConnectionAsync(pipe, child, stderrPath, deadline.Token);
        await client.ConnectAsync(deadline.Token);
        await connected.WaitAsync(deadline.Token);
        Assert.True(pipe.IsConnected);
        Assert.False(child.HasExited);
        Assert.False(deadline.IsCancellationRequested);
    }

    /// <summary>Merely referencing/loading the hook does not activate a listener or background session.</summary>
    [Fact]
    public void ProbeIsInertWithoutConfiguration()
    {
        Assert.Null(ProbeConfiguration.FromEnvironment(name =>
        {
            Assert.Equal(ProbeConfiguration.EnvironmentVariable, name);
            return null;
        }));
        Assert.Null(Environment.GetEnvironmentVariable(ProbeConfiguration.EnvironmentVariable));
        Assert.False(global::StartupHook.IsActive);
        global::StartupHook.Initialize();
        global::StartupHook.Initialize();
        Assert.False(global::StartupHook.IsActive);
        Assert.Throws<InvalidDataException>(() => ProbeConfiguration.FromEnvironment(name => string.Empty));
        Assert.Throws<InvalidDataException>(() => ProbeConfiguration.FromEnvironment(name => new string('x', ProbeProtocol.MaximumRequestBytes + 1)));
    }

    /// <summary>Exercises framed messages, real Windows pipe peer attribution, and independent ownership failures.</summary>
    [Fact]
    public async Task ProbeRejectsWrongPeerAndOversizedMessage()
    {
        using Process process = Process.GetCurrentProcess();
        string owner = OperatingSystem.IsWindows() ? ProbeProtocol.CurrentOwnerSid() : "fixture-owner";
        ProbeConfiguration configuration = new(
            "candle-perf-" + Guid.NewGuid().ToString("N"), process.Id, process.StartTime.ToUniversalTime().Ticks,
            owner, new string('A', 64), DateTimeOffset.UtcNow.AddMinutes(1), 5);
        configuration.Validate();
        ProbeHello hello = new(ProbeProtocol.Version, process.Id, configuration.ParentStartTimeUtcTicks,
            process.Id, configuration.ParentStartTimeUtcTicks, owner, configuration.Token, new string('B', 64));
        using CancellationTokenSource deadline = new(TimeSpan.FromSeconds(10));
        int actualPeer = process.Id;
        if (OperatingSystem.IsWindows())
        {
            using NamedPipeServerStream server = ProbeProtocol.CreateServer(configuration.PipeName);
            using NamedPipeClientStream client = new(".", configuration.PipeName, PipeDirection.InOut,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            Task accepting = server.WaitForConnectionAsync(deadline.Token);
            await client.ConnectAsync(deadline.Token);
            await accepting;
            actualPeer = ProbeProtocol.GetPeerProcessId(server, server: true);
            Assert.Equal(process.Id, actualPeer);
            Assert.Equal(process.Id, ProbeProtocol.GetPeerProcessId(client, server: false));
            await ProbeProtocol.WriteAsync(client, hello, ProbeProtocol.MaximumRequestBytes, deadline.Token);
            hello = await ProbeProtocol.ReadAsync<ProbeHello>(server, ProbeProtocol.MaximumRequestBytes, deadline.Token);
        }

        ProbeProtocol.ValidateHello(hello, configuration, process.Id, configuration.ParentStartTimeUtcTicks,
            hello.ProbeSha256, actualPeer, process.Id, owner);
        Assert.Throws<UnauthorizedAccessException>(() => ProbeProtocol.ValidateHello(hello, configuration,
            process.Id + 1, hello.ChildStartTimeUtcTicks, hello.ProbeSha256, actualPeer, process.Id, owner));
        Assert.Throws<UnauthorizedAccessException>(() => ProbeProtocol.ValidateHello(hello, configuration,
            process.Id, hello.ChildStartTimeUtcTicks, hello.ProbeSha256, actualPeer + 1, process.Id, owner));
        ProbeHello[] wrongMessages = [
            hello with { Token = new string('C', 64) },
            hello with { OwnerSid = "wrong-owner" },
            hello with { ParentStartTimeUtcTicks = hello.ParentStartTimeUtcTicks + 1 },
            hello with { ChildStartTimeUtcTicks = hello.ChildStartTimeUtcTicks + 1 },
            hello with { ProbeSha256 = new string('D', 64) },
            hello with { ProtocolVersion = "unknown" },
        ];
        foreach (ProbeHello wrong in wrongMessages)
        {
            using MemoryStream stream = new();
            await ProbeProtocol.WriteAsync(stream, wrong, ProbeProtocol.MaximumRequestBytes, deadline.Token);
            stream.Position = 0;
            ProbeHello decoded = await ProbeProtocol.ReadAsync<ProbeHello>(stream, ProbeProtocol.MaximumRequestBytes, deadline.Token);
            Assert.Throws<UnauthorizedAccessException>(() => ProbeProtocol.ValidateHello(decoded, configuration,
                process.Id, configuration.ParentStartTimeUtcTicks, hello.ProbeSha256, actualPeer, process.Id, owner));
        }

        byte[] oversized = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(oversized, ProbeProtocol.MaximumRequestBytes + 1);
        using MemoryStream oversizedStream = new(oversized);
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            ProbeProtocol.ReadAsync<ProbeCommand>(oversizedStream, ProbeProtocol.MaximumRequestBytes, deadline.Token));
        using MemoryStream tooLargeToWrite = new();
        await Assert.ThrowsAsync<InvalidDataException>(() => ProbeProtocol.WriteAsync(tooLargeToWrite,
            new ProbeCommand(1, "snapshot", "perf", [new string('x', ProbeProtocol.MaximumRequestBytes)]),
            ProbeProtocol.MaximumRequestBytes, deadline.Token));
        byte[] truncated = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(truncated, 10);
        using MemoryStream truncatedStream = new(truncated);
        await Assert.ThrowsAsync<EndOfStreamException>(() =>
            ProbeProtocol.ReadAsync<ProbeCommand>(truncatedStream, ProbeProtocol.MaximumRequestBytes, deadline.Token));
    }

    /// <summary>Manager/store/resource dictionaries and store properties need not be concrete public members.</summary>
    [Fact]
    public void InspectionSupportsExplicitDictionaryInterfaces()
    {
        ProbeChallenge challenge = Workloads.CreateProbeChallenge("R4");
        ReceiptSnapshot first = Receipt(challenge);
        ReceiptSnapshot second = first with { BundleId = "another-bundle" };
        FakeStore store = Store([first, second]);
        ExplicitDictionary manager = new(new Dictionary<string, object> { ["perf"] = store });
        Assert.Null(manager.GetType().GetProperty("Count"));
        Assert.Null(store.GetType().GetProperty("ReceivedNotifications"));
        TenantSnapshot snapshot = StoreInspection.InspectTenant(manager, "perf", ["Patient/patient-00000000", "Bundle/absent"]);
        Assert.Equal(_sourceDirectory, snapshot.Configuration["LoadDirectory"]);
        Assert.Equal(new[] { _sourceDirectory }, snapshot.LoadedSupplements);
        Assert.Equal(1, snapshot.ResourceCounts["Patient"]);
        Assert.Equal(1, snapshot.ResourceCounts["Observation"]);
        Assert.Equal(1, snapshot.ResourceCounts["Bundle"]);
        Assert.Equal(new ResourceIdentity("Patient", "patient-00000000"), snapshot.RequestedResources["Patient/patient-00000000"]);
        Assert.Null(snapshot.RequestedResources["Bundle/absent"]);
        Assert.Equal(1, snapshot.ReceiptKeyCount);
        Assert.Equal(2, snapshot.ReceiptCount);
        Assert.Equal(2, snapshot.EventCount);
        ReceiptSnapshot[] inspected = Assert.Single(snapshot.ReceivedNotifications).Value;
        Assert.Equal(2, inspected.Length);
        Assert.Equal("another-bundle", inspected[1].BundleId);
        NotificationEventSnapshot notificationEvent = Assert.Single(inspected[0].NotificationEvents);
        Assert.Equal(first.NotificationEvents[0].EventNumber, notificationEvent.EventNumber);
        Assert.Equal(first.NotificationEvents[0].Timestamp, notificationEvent.Timestamp);
        Assert.Equal(first.NotificationEvents[0].FocusReference, notificationEvent.FocusReference);
        Assert.Equal(first.NotificationEvents[0].AdditionalContextReferences, notificationEvent.AdditionalContextReferences);
    }

    /// <summary>Dual explicit resource dictionaries must use the object view for Count, ContainsKey and Item.</summary>
    [Fact]
    public void InspectionUsesObjectViewForDualResourceDictionaries()
    {
        ExplicitResourceDictionary resourceStore = new(
            new Dictionary<string, object>
            {
                ["object-only"] = new FakeResource("Patient", "object-only"),
                ["shared"] = new FakeResource("Patient", "shared"),
            },
            new Dictionary<string, FakeResource>
            {
                ["shared"] = new FakeResource("Patient", "wrong-view"),
                ["typed-only"] = new FakeResource("Patient", "typed-only"),
                ["another-typed-only"] = new FakeResource("Patient", "another-typed-only"),
            });
        IReadOnlyDictionary<string, object> objectView = resourceStore;
        IReadOnlyDictionary<string, FakeResource> typedView = resourceStore;
        Assert.Equal(2, objectView.Count);
        Assert.Equal(3, typedView.Count);
        Assert.Null(resourceStore.GetType().GetProperty("Count"));
        Assert.Null(resourceStore.GetType().GetMethod("ContainsKey"));
        Assert.Null(resourceStore.GetType().GetProperty("Item"));
        FakeStore store = new(new Dictionary<string, object> { ["Patient"] = resourceStore },
            new Dictionary<string, List<object>>());
        TenantSnapshot snapshot = StoreInspection.InspectTenant(
            new ExplicitDictionary(new Dictionary<string, object> { ["perf"] = store }), "perf",
            ["Patient/object-only", "Patient/shared", "Patient/typed-only", "Patient/absent"]);
        Assert.Equal(2, Assert.Single(snapshot.ResourceCounts).Value);
        Assert.Equal(new ResourceIdentity("Patient", "object-only"), snapshot.RequestedResources["Patient/object-only"]);
        Assert.Equal(new ResourceIdentity("Patient", "shared"), snapshot.RequestedResources["Patient/shared"]);
        Assert.Null(snapshot.RequestedResources["Patient/typed-only"]);
        Assert.Null(snapshot.RequestedResources["Patient/absent"]);
    }

    /// <summary>Missing/ambiguous contracts, invalid counts and missing event fields cannot become success fallbacks.</summary>
    [Fact]
    public void InspectionRejectsMissingTenantOrReceiptContract()
    {
        FakeStore store = Store([]);
        Assert.Throws<InvalidDataException>(() => StoreInspection.InspectTenant(
            new ExplicitDictionary(new Dictionary<string, object> { ["other"] = store }), "perf", []));
        Assert.Throws<InvalidDataException>(() => StoreInspection.InspectTenant(
            new ExplicitDictionary(new Dictionary<string, object> { ["perf"] = store, ["extra"] = store }), "perf", []));
        InvalidDataException missing = Assert.Throws<InvalidDataException>(() => StoreInspection.InspectTenant(
            new ExplicitDictionary(new Dictionary<string, object> { ["perf"] = new MissingReceiptStore() }), "perf", []));
        Assert.Contains("ReceivedNotifications", missing.Message, StringComparison.Ordinal);
        Assert.Throws<InvalidDataException>(() => StoreInspection.InspectTenant(
            new ExplicitDictionary(new Dictionary<string, object> { ["perf"] = store }, countAdjustment: 1), "perf", []));
        object[] invalidManagers = [
            new object(),
            new Dictionary<int, object>(),
            new ExplicitResourceDictionary(new Dictionary<string, object> { ["perf"] = store },
                new Dictionary<string, FakeResource>()),
        ];
        foreach (object invalidManager in invalidManagers)
        {
            InvalidDataException invalid = Assert.Throws<InvalidDataException>(() =>
                StoreInspection.InspectTenant(invalidManager, "perf", []));
            Assert.Contains("IReadOnlyDictionary", invalid.Message, StringComparison.Ordinal);
            Assert.Contains(invalidManager.GetType().FullName!, invalid.Message, StringComparison.Ordinal);
        }

        FakeStore ambiguousReceipts = new(new Dictionary<string, object>(),
            new ExplicitResourceDictionary(new Dictionary<string, object>(), new Dictionary<string, FakeResource>()));
        InvalidDataException ambiguous = Assert.Throws<InvalidDataException>(() => StoreInspection.InspectTenant(
            new ExplicitDictionary(new Dictionary<string, object> { ["perf"] = ambiguousReceipts }), "perf", []));
        Assert.Contains(typeof(IReadOnlyDictionary<string, object>).ToString(), ambiguous.Message, StringComparison.Ordinal);
        Assert.Contains(typeof(IReadOnlyDictionary<string, FakeResource>).ToString(), ambiguous.Message, StringComparison.Ordinal);

        object[] invalidResources = [
            new Dictionary<string, FakeResource>(),
            new ExplicitResourceDictionary(new Dictionary<string, object>(), new Dictionary<string, FakeResource>(), countAdjustment: -1),
            new ExplicitResourceDictionary(new Dictionary<string, object>(), new Dictionary<string, FakeResource>(), countAdjustment: 100001),
        ];
        foreach (object invalidResource in invalidResources)
        {
            FakeStore invalidStore = new(new Dictionary<string, object> { ["Patient"] = invalidResource },
                new Dictionary<string, List<object>>());
            Assert.Throws<InvalidDataException>(() => StoreInspection.InspectTenant(
                new ExplicitDictionary(new Dictionary<string, object> { ["perf"] = invalidStore }), "perf", []));
        }

        object incomplete = new
        {
            BundleId = "bundle", ProcessedDateTime = DateTimeOffset.UtcNow, SubscriptionReference = "subscription",
            SubscriptionTopicCanonical = "topic", Status = "active", NotificationType = "EventNotification",
            EventsSinceSubscriptionStart = (long?)1, Errors = Array.Empty<string>(),
            NotificationEvents = new[] { new { Id = string.Empty } },
        };
        FakeStore malformed = StoreObjects([incomplete]);
        InvalidDataException missingEvent = Assert.Throws<InvalidDataException>(() => StoreInspection.InspectTenant(
            new ExplicitDictionary(new Dictionary<string, object> { ["perf"] = malformed }), "perf", []));
        Assert.Contains("EventNumber", missingEvent.Message, StringComparison.Ordinal);
    }

    /// <summary>The runner's source check requires one exact registration and rejects any directory activity.</summary>
    [Fact]
    public void EmptySourceRequiresExactRegistrationAndNoFileActivity()
    {
        DirectoryInfo root = Directory.CreateTempSubdirectory("candle-perf-source-");
        try
        {
            string source = Directory.CreateDirectory(Path.Combine(root.FullName, "empty-source")).FullName;
            string other = Directory.CreateDirectory(Path.Combine(root.FullName, "other-source")).FullName;
            TenantSnapshot snapshot = StoreInspection.InspectTenant(
                new ExplicitDictionary(new Dictionary<string, object> { ["perf"] = Store([]) }), "perf", []);
            snapshot = snapshot with
            {
                Configuration = new Dictionary<string, string?>(snapshot.Configuration, StringComparer.Ordinal) { ["LoadDirectory"] = source },
                LoadedSupplements = [source],
            };
            CorrectnessOracle.ValidateEmptySource(snapshot, source);
            Assert.Equal(new[] { source }, snapshot.LoadedSupplements);
            Dictionary<string, string?> missingSource = new(snapshot.Configuration, StringComparer.Ordinal);
            missingSource.Remove("LoadDirectory");
            TenantSnapshot[] invalid = [
                snapshot with { LoadedSupplements = [] },
                snapshot with { LoadedSupplements = [source, source] },
                snapshot with { LoadedSupplements = [source, other] },
                snapshot with { LoadedSupplements = [other] },
                snapshot with { Configuration = missingSource },
                snapshot with
                {
                    Configuration = new Dictionary<string, string?>(snapshot.Configuration, StringComparer.Ordinal) { ["LoadDirectory"] = null },
                },
                snapshot with
                {
                    Configuration = new Dictionary<string, string?>(snapshot.Configuration, StringComparer.Ordinal) { ["LoadDirectory"] = string.Empty },
                },
                snapshot with
                {
                    Configuration = new Dictionary<string, string?>(snapshot.Configuration, StringComparer.Ordinal) { ["LoadDirectory"] = other },
                },
                snapshot with
                {
                    Configuration = new Dictionary<string, string?>(snapshot.Configuration, StringComparer.Ordinal) { ["LoadDirectory"] = other },
                    LoadedSupplements = [other],
                },
            ];
            foreach (TenantSnapshot wrong in invalid)
            {
                Assert.Throws<InvalidDataException>(() => CorrectnessOracle.ValidateEmptySource(wrong, source));
            }

            Assert.Throws<InvalidDataException>(() => CorrectnessOracle.ValidateEmptySource(snapshot, other));
            Assert.Throws<InvalidDataException>(() => CorrectnessOracle.ValidateEmptySource(snapshot, string.Empty));
            Assert.Throws<InvalidDataException>(() => CorrectnessOracle.ValidateEmptySource(snapshot, "empty-source"));
            foreach (string name in new[] { "unexpected.json", "unexpected.txt" })
            {
                string file = Path.Combine(source, name);
                File.WriteAllText(file, "{}");
                Assert.Throws<InvalidDataException>(() => CorrectnessOracle.ValidateEmptySource(snapshot, source));
                File.Delete(file);
            }

            string nested = Path.Combine(source, "nested");
            Directory.CreateDirectory(nested);
            Assert.Throws<InvalidDataException>(() => CorrectnessOracle.ValidateEmptySource(snapshot, source));
            File.WriteAllText(Path.Combine(nested, "unexpected.xml"), "<Patient/>");
            Assert.Throws<InvalidDataException>(() => CorrectnessOracle.ValidateEmptySource(snapshot, source));
            Directory.Delete(nested, recursive: true);
            CorrectnessOracle.ValidateEmptySource(snapshot, source);
            Directory.Delete(source);
            Assert.Throws<InvalidDataException>(() => CorrectnessOracle.ValidateEmptySource(snapshot, source));
            File.WriteAllText(source, "{}");
            Assert.Throws<InvalidDataException>(() => CorrectnessOracle.ValidateEmptySource(snapshot, source));
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    /// <summary>All three notifications carry independent full content, and content/identity/receipt negatives fail.</summary>
    [Fact]
    public void NotificationChallengeCarriesFullContent()
    {
        foreach (string version in new[] { "R4", "R4B", "R5" })
        {
            ProbeChallenge challenge = Workloads.CreateProbeChallenge(version);
            CorrectnessOracle.ValidateChallenge(challenge);
            Assert.Equal(challenge.Notification, Workloads.CreateProbeChallenge(version).Notification);
            Assert.NotEqual(challenge.BundleId, Workloads.CreateProbeChallenge(version, 2).BundleId);
            CorrectnessOracle.ValidateChallenge(Workloads.CreateProbeChallenge(version, 2));
            JsonObject bundle = JsonNode.Parse(challenge.Notification)!.AsObject();
            Assert.Equal("Observation", bundle["entry"]![1]!["resource"]!["resourceType"]!.GetValue<string>());
            Assert.Equal("Patient", bundle["entry"]![2]!["resource"]!["resourceType"]!.GetValue<string>());
            Assert.Equal("2025-01-02T03:04:05+00:00", bundle["timestamp"]!.GetValue<string>());
            JsonNode status = bundle["entry"]![0]!["resource"]!;
            Assert.Equal("2025-01-02T03:04:05+00:00", (version == "R4"
                ? status["parameter"]![5]!["part"]![1]!["valueInstant"]
                : status["notificationEvent"]![0]!["timestamp"])!.GetValue<string>());
            Assert.Equal("2025-01-02T03:04:05Z", JsonNode.Parse(challenge.Observation)!["effectiveDateTime"]!.GetValue<string>());
            Assert.Equal("2025-01-02T03:04:05Z", bundle["entry"]![1]!["resource"]!["effectiveDateTime"]!.GetValue<string>());

            DateTimeOffset storedAt = DateTimeOffset.UtcNow;
            JsonObject storedBundle = (JsonObject)bundle.DeepClone();
            storedBundle["meta"] ??= new JsonObject();
            storedBundle["meta"]!["versionId"] = "1";
            storedBundle["meta"]!["lastUpdated"] = storedAt.ToString("O", CultureInfo.InvariantCulture);
            ResponseEvidence bundleRead = new(200, new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["Content-Type"] = ["application/fhir+json; charset=utf-8"],
                ["ETag"] = ["W/\"1\""], ["Last-Modified"] = [storedAt.UtcDateTime.ToString("r", CultureInfo.InvariantCulture)],
                ["Location"] = ["http://127.0.0.1:58260/fhir/perf/Bundle/" + challenge.BundleId],
            }, JsonSerializer.SerializeToUtf8Bytes(storedBundle), storedAt, storedAt);
            _ = CorrectnessOracle.ValidateResponse(bundleRead, 200, challenge.Notification,
                "http://127.0.0.1:58260/fhir/perf", storedAt.AddMinutes(-1));
            foreach (Action<JsonObject> mutate in new Action<JsonObject>[]
            {
                root => root["entry"]!.AsArray().RemoveAt(2),
                root => root["entry"]![1]!["resource"]!["valueQuantity"]!["value"] = 99,
                root => root["entry"]![1]!["resource"]!["subject"]!["reference"] = "Patient/wrong-context",
                root => root["entry"]![1]!["resource"]!["effectiveDateTime"] = "2025-01-02T03:04:05+00:00",
                root => root["entry"]![1]!["resource"]!["effectiveDateTime"] = "2025-01-02T03:04:06Z",
                root => root["entry"]![2]!["resource"]!["name"]![0]!["family"] = "Altered",
                root => root["entry"]![0]!["fullUrl"] = "urn:uuid:wrong",
            })
            {
                JsonObject changed = (JsonObject)bundle.DeepClone();
                mutate(changed);
                ProbeChallenge invalid = challenge with { Notification = JsonSerializer.SerializeToUtf8Bytes(changed) };
                Assert.Throws<InvalidDataException>(() => CorrectnessOracle.ValidateChallenge(invalid));
                JsonObject returned = (JsonObject)storedBundle.DeepClone();
                mutate(returned);
                Assert.Throws<InvalidDataException>(() => CorrectnessOracle.ValidateResponse(
                    bundleRead with { Body = JsonSerializer.SerializeToUtf8Bytes(returned) },
                    200, challenge.Notification, "http://127.0.0.1:58260/fhir/perf", storedAt.AddMinutes(-1)));
            }

            foreach (string timestamp in new[]
            {
                "2025-01-02T03:04:06+00:00",
                "2025-01-02T03:04:05+01:00",
                "2025-01-02T03:04:05.000+00:00",
                "2025-01-02T03:04:05Z",
            })
            {
                foreach (Action<JsonObject> mutate in new Action<JsonObject>[]
                {
                    root => root["timestamp"] = timestamp,
                    root =>
                    {
                        if (version == "R4")
                        {
                            root["entry"]![0]!["resource"]!["parameter"]![5]!["part"]![1]!["valueInstant"] = timestamp;
                        }
                        else
                        {
                            root["entry"]![0]!["resource"]!["notificationEvent"]![0]!["timestamp"] = timestamp;
                        }
                    },
                })
                {
                    JsonObject changed = (JsonObject)bundle.DeepClone();
                    mutate(changed);
                    Assert.Throws<InvalidDataException>(() => CorrectnessOracle.ValidateNotification(
                        challenge with { Notification = JsonSerializer.SerializeToUtf8Bytes(changed) }));
                    JsonObject returned = (JsonObject)storedBundle.DeepClone();
                    mutate(returned);
                    Assert.Throws<InvalidDataException>(() => CorrectnessOracle.ValidateResponse(
                        bundleRead with { Body = JsonSerializer.SerializeToUtf8Bytes(returned) },
                        200, challenge.Notification, "http://127.0.0.1:58260/fhir/perf", storedAt.AddMinutes(-1)));
                }
            }

            JsonObject wrongNumber = (JsonObject)bundle.DeepClone();
            if (version == "R4")
            {
                wrongNumber["entry"]![0]!["resource"]!["parameter"]![5]!["part"]![0]!["valueString"] = "2";
            }
            else
            {
                wrongNumber["entry"]![0]!["resource"]!["notificationEvent"]![0]!["eventNumber"] = "2";
            }

            Assert.Throws<InvalidDataException>(() => CorrectnessOracle.ValidateNotification(
                challenge with { Notification = JsonSerializer.SerializeToUtf8Bytes(wrongNumber) }));
            TenantSnapshot snapshot = StoreInspection.InspectTenant(
                new ExplicitDictionary(new Dictionary<string, object> { ["perf"] = Store([Receipt(challenge)]) }), "perf", []);
            DateTimeOffset now = DateTimeOffset.UtcNow;
            CorrectnessOracle.ValidateReceipt(snapshot, challenge, now.AddMinutes(-1), now);
            string key = Assert.Single(snapshot.ReceivedNotifications).Key;
            ReceiptSnapshot receipt = snapshot.ReceivedNotifications[key][0];
            ReceiptSnapshot[][] wrongReceipts = [
                [],
                [receipt, receipt],
                [receipt with { NotificationEvents = [] }],
                [receipt with { BundleId = "different-bundle" }],
                [receipt with { Errors = ["error"] }],
                [receipt with { NotificationEvents = [receipt.NotificationEvents[0] with { EventNumber = 99 }] }],
                [receipt with { NotificationEvents = [receipt.NotificationEvents[0] with { Timestamp = null }] }],
                [receipt with { NotificationEvents = [receipt.NotificationEvents[0] with { Timestamp = receipt.NotificationEvents[0].Timestamp?.AddSeconds(1) }] }],
                [receipt with { NotificationEvents = [receipt.NotificationEvents[0] with { AdditionalContextReferences = ["Patient/wrong"] }] }],
            ];
            foreach (ReceiptSnapshot[] invalid in wrongReceipts)
            {
                TenantSnapshot wrong = snapshot with
                {
                    // Leave the correct key and claimed totals intact: the lists/events must still be inspected.
                    ReceivedNotifications = new SortedDictionary<string, ReceiptSnapshot[]>(StringComparer.Ordinal) { [key] = invalid },
                };
                Assert.Throws<InvalidDataException>(() => CorrectnessOracle.ValidateReceipt(wrong, challenge, now.AddMinutes(-1), now));
            }

            ResponseEvidence response = CreatedObservation(challenge, now);
            ResourceBinding binding = CorrectnessOracle.ValidateResponse(response, 201, challenge.Observation,
                "http://127.0.0.1:58260/fhir/perf", now.AddMinutes(-1));
            Assert.Equal("server-assigned-1", binding.Id);
            foreach (string effectiveDateTime in new[] { "2025-01-02T03:04:05+00:00", "2025-01-02T03:04:06Z" })
            {
                JsonObject changed = JsonNode.Parse(challenge.Observation)!.AsObject();
                changed["effectiveDateTime"] = effectiveDateTime;
                Assert.Throws<InvalidDataException>(() => CorrectnessOracle.ValidateChallenge(
                    challenge with { Observation = JsonSerializer.SerializeToUtf8Bytes(changed) }));
                JsonObject returned = JsonNode.Parse(response.Body)!.AsObject();
                returned["effectiveDateTime"] = effectiveDateTime;
                Assert.Throws<InvalidDataException>(() => CorrectnessOracle.ValidateResponse(
                    response with { Body = JsonSerializer.SerializeToUtf8Bytes(returned) },
                    201, challenge.Observation, "http://127.0.0.1:58260/fhir/perf", now.AddMinutes(-1)));
            }

            TenantSnapshot initial = snapshot with
            {
                ResourceCounts = new SortedDictionary<string, int>(StringComparer.Ordinal) { ["Bundle"] = 0, ["Observation"] = 0, ["Patient"] = 0 },
                ReceivedNotifications = new SortedDictionary<string, ReceiptSnapshot[]>(StringComparer.Ordinal),
                ReceiptKeyCount = 0, ReceiptCount = 0, EventCount = 0,
            };
            TenantSnapshot final = snapshot with
            {
                RequestedResources = new Dictionary<string, ResourceIdentity?>(StringComparer.Ordinal)
                {
                    ["Patient/" + challenge.PatientId] = new("Patient", challenge.PatientId),
                    ["Observation/" + binding.Id] = new("Observation", binding.Id),
                    ["Bundle/" + challenge.BundleId] = new("Bundle", challenge.BundleId),
                    ["Patient/" + challenge.ContextId] = null,
                    ["Observation/" + challenge.FocusId] = null,
                },
            };
            CorrectnessOracle.ValidateState(initial, final, challenge, binding, _sourceDirectory, received: true);
            string otherSource = _sourceDirectory + "-other";
            Dictionary<string, string?> missingSource = new(final.Configuration, StringComparer.Ordinal);
            missingSource.Remove("LoadDirectory");
            TenantSnapshot[] invalidRegistrations = [
                final with { LoadedSupplements = [] },
                final with { LoadedSupplements = [_sourceDirectory, _sourceDirectory] },
                final with { LoadedSupplements = [_sourceDirectory, otherSource] },
                final with { LoadedSupplements = [otherSource] },
                final with { Configuration = missingSource },
                final with
                {
                    Configuration = new Dictionary<string, string?>(final.Configuration, StringComparer.Ordinal) { ["LoadDirectory"] = null },
                },
                final with
                {
                    Configuration = new Dictionary<string, string?>(final.Configuration, StringComparer.Ordinal) { ["LoadDirectory"] = string.Empty },
                },
                final with
                {
                    Configuration = new Dictionary<string, string?>(final.Configuration, StringComparer.Ordinal) { ["LoadDirectory"] = otherSource },
                },
                final with
                {
                    Configuration = new Dictionary<string, string?>(final.Configuration, StringComparer.Ordinal) { ["LoadDirectory"] = otherSource },
                    LoadedSupplements = [otherSource],
                },
                final with { LoadedPackageDirectives = ["unexpected#1.0.0"] },
                final with { LoadedPackageIds = ["unexpected"] },
            ];
            foreach (TenantSnapshot wrongCurrent in invalidRegistrations)
            {
                TenantSnapshot wrongInitial = initial with
                {
                    Configuration = wrongCurrent.Configuration,
                    LoadedSupplements = wrongCurrent.LoadedSupplements,
                    LoadedPackageDirectives = wrongCurrent.LoadedPackageDirectives,
                    LoadedPackageIds = wrongCurrent.LoadedPackageIds,
                };
                Assert.Throws<InvalidDataException>(() => CorrectnessOracle.ValidateState(
                    initial, wrongCurrent, challenge, binding, _sourceDirectory, received: true));
                Assert.Throws<InvalidDataException>(() => CorrectnessOracle.ValidateState(
                    wrongInitial, final, challenge, binding, _sourceDirectory, received: true));
                // Matching observed histories must not redefine the expected configured source or packages.
                Assert.Throws<InvalidDataException>(() => CorrectnessOracle.ValidateState(
                    wrongInitial, wrongCurrent, challenge, binding, _sourceDirectory, received: true));
            }

            Assert.Throws<InvalidDataException>(() => CorrectnessOracle.ValidateState(
                initial, final, challenge, binding, otherSource, received: true));
            SortedDictionary<string, int> doubleStored = new(final.ResourceCounts, StringComparer.Ordinal)
            {
                ["Observation"] = 2,
            };
            Assert.Throws<InvalidDataException>(() => CorrectnessOracle.ValidateState(
                initial, final with { ResourceCounts = doubleStored }, challenge, binding, _sourceDirectory, received: true));
            Dictionary<string, string[]> wrongHeaders = new(response.Headers, StringComparer.OrdinalIgnoreCase)
            {
                ["Location"] = ["http://127.0.0.1:58260/fhir/perf/Observation/wrong-id"],
            };
            Assert.Throws<InvalidDataException>(() => CorrectnessOracle.ValidateResponse(response with { Headers = wrongHeaders },
                201, challenge.Observation, "http://127.0.0.1:58260/fhir/perf", now.AddMinutes(-1)));
            Assert.Throws<InvalidDataException>(() => CorrectnessOracle.ValidateResponse(response with { StatusCode = 422 },
                201, challenge.Observation, "http://127.0.0.1:58260/fhir/perf", now.AddMinutes(-1)));
        }
    }

    /// <summary>Both actual scales have reproducible bytes, fixed-width IDs and complete ten-to-one references.</summary>
    [Fact]
    public void CorpusIsDeterministicAndReferenceComplete()
    {
        Assert.Equal("candle-perf-v1", Workloads.Algorithm);
        Assert.Equal(100101, Workloads.Seed);
        foreach (int patients in new[] { 100, 1000 })
        {
            WorkloadCorpus reference = Workloads.CreateCorpus("R4", patients);
            foreach (string version in new[] { "R4", "R4B", "R5" })
            {
                WorkloadCorpus corpus = Workloads.CreateCorpus(version, patients);
                WorkloadCorpus repeated = Workloads.CreateCorpus(version, patients);
                CorrectnessOracle.ValidateCorpus(corpus);
                Assert.Equal(patients, corpus.PatientCount);
                Assert.Equal(patients * 10, corpus.ObservationCount);
                Assert.Equal(patients * 11, corpus.Resources.Length);
                Assert.Equal(JsonSerializer.SerializeToUtf8Bytes(corpus), JsonSerializer.SerializeToUtf8Bytes(repeated));
                Assert.Equal(reference.PatientOrder, corpus.PatientOrder);
                Assert.Equal(reference.ObservationOrder, corpus.ObservationOrder);
                Assert.Equal(Enumerable.Range(0, patients), corpus.PatientOrder.Order());
                Assert.Equal(Enumerable.Range(0, patients * 10), corpus.ObservationOrder.Order());
                CorpusResource[] order = Workloads.SeedResources(corpus).ToArray();
                Assert.All(order.Take(patients), resource => Assert.Equal("Patient", resource.ResourceType));
                Assert.All(order.Skip(patients), resource => Assert.Equal("Observation", resource.ResourceType));
                Assert.Equal(corpus.Resources.Length, order.Select(resource => resource.Identity).Distinct().Count());
                int[] families = new int[100];
                int[] codes = new int[1000];
                for (int index = 0; index < corpus.Resources.Length; index++)
                {
                    CorpusResource resource = corpus.Resources[index];
                    Assert.Equal(reference.Resources[index].Body, resource.Body);
                    Assert.Equal(SHA256.HashData(resource.Body), SHA256.HashData(repeated.Resources[index].Body));
                    Assert.True(resource.Body.Length > 0);
                    using JsonDocument document = JsonDocument.Parse(resource.Body);
                    JsonElement root = document.RootElement;
                    if (index < patients)
                    {
                        Assert.Equal("patient-" + index.ToString("D8", CultureInfo.InvariantCulture), resource.Id);
                        Assert.Equal("Family" + (index % 100).ToString("D3", CultureInfo.InvariantCulture),
                            root.GetProperty("name")[0].GetProperty("family").GetString());
                        families[index % 100]++;
                    }
                    else
                    {
                        int logical = index - patients;
                        Assert.Equal("observation-" + logical.ToString("D8", CultureInfo.InvariantCulture), resource.Id);
                        Assert.Equal("Patient/patient-" + (logical / 10).ToString("D8", CultureInfo.InvariantCulture),
                            root.GetProperty("subject").GetProperty("reference").GetString());
                        Assert.Equal("Code" + (logical % 1000).ToString("D3", CultureInfo.InvariantCulture),
                            root.GetProperty("code").GetProperty("coding")[0].GetProperty("code").GetString());
                        codes[logical % 1000]++;
                    }
                }

                Assert.All(families, count => Assert.Equal(patients / 100, count));
                Assert.All(codes, count => Assert.Equal(patients / 100, count));
                CorpusResource[] altered = corpus.Resources.ToArray();
                JsonObject wrong = JsonNode.Parse(altered[patients].Body)!.AsObject();
                wrong["subject"]!["reference"] = "Patient/not-in-corpus";
                altered[patients] = altered[patients] with { Body = JsonSerializer.SerializeToUtf8Bytes(wrong) };
                Assert.Throws<InvalidDataException>(() => CorrectnessOracle.ValidateCorpus(corpus with { Resources = altered }));
                Assert.Throws<InvalidDataException>(() => CorrectnessOracle.ValidateCorpus(corpus with { PatientOrder = new int[patients] }));
                Assert.Throws<InvalidDataException>(() => CorrectnessOracle.ValidateCorpus(corpus with { Resources = corpus.Resources[..^1] }));
            }
        }

        Assert.Throws<ArgumentOutOfRangeException>(() => Workloads.CreateCorpus("R6", 100));
        Assert.Throws<ArgumentOutOfRangeException>(() => Workloads.CreateCorpus("R4", 10));
    }

    /// <summary>The full matrix and its four-operation request ledger cannot shrink into a smoke subset.</summary>
    [Fact]
    public void CaseMatrixContainsExactlySeventyTwoRows()
    {
        string[] expectedCases = ["patient-create", "observation-create", "patient-update", "observation-update",
            "patient-token", "patient-string", "observation-token", "observation-reference", "empty-search",
            "patient-read", "observation-read", "subscription-receive"];
        WorkloadCase[] cases = Workloads.CreateCases();
        Assert.Equal(72, cases.Length);
        Assert.Equal(72, cases.Select(workload => workload.RowKey).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(cases, Workloads.CreateCases());
        Assert.Equal(4, Workloads.PreflightOperations);
        foreach (string version in new[] { "R4", "R4B", "R5" })
        {
            foreach (int patients in new[] { 100, 1000 })
            {
                WorkloadCorpus corpus = Workloads.CreateCorpus(version, patients);
                WorkloadCase[] group = cases.Where(workload => workload.FhirVersion == version && workload.PatientCount == patients).ToArray();
                Assert.Equal(expectedCases, group.Select(workload => workload.CaseId));
                foreach (WorkloadCase workload in group)
                {
                    Assert.Equal(patients * 10, workload.ObservationCount);
                    Assert.Equal(corpus.Key + "-" + workload.CaseId, workload.RowKey);
                    WorkloadRequest[] requests = Workloads.CreateRequests(workload, corpus);
                    CorrectnessOracle.ValidateCaseRequests(workload, corpus, requests, 4);
                    Assert.Equal(new[] { 1, 2, 3, 4 }, requests.Select(request => request.Number));
                    Assert.Equal(JsonSerializer.SerializeToUtf8Bytes(requests),
                        JsonSerializer.SerializeToUtf8Bytes(Workloads.CreateRequests(workload, corpus)));
                    int expectedMatches = workload.CaseId switch
                    {
                        "patient-string" or "observation-token" => patients / 100,
                        "observation-reference" => 10,
                        "patient-create" or "observation-create" or "empty-search" => 0,
                        _ => 1,
                    };
                    Assert.All(requests, request => Assert.Equal(expectedMatches, request.ExpectedIdentities.Length));
                    if (workload.CaseId is "patient-token" or "observation-token" or "empty-search")
                    {
                        Assert.All(requests, request =>
                        {
                            Assert.Contains("%7C", request.RelativeUrl, StringComparison.Ordinal);
                            Assert.Contains("%3A%2F%2F", request.RelativeUrl, StringComparison.Ordinal);
                        });
                    }

                    if (workload.CaseId.EndsWith("-create", StringComparison.Ordinal))
                    {
                        Assert.All(requests, request => Assert.False(JsonNode.Parse(request.Body!)!.AsObject().ContainsKey("id")));
                        Assert.Equal(4, requests.Select(request => JsonNode.Parse(request.Body!)!["identifier"]![0]!["value"]!.GetValue<string>())
                            .Distinct(StringComparer.Ordinal).Count());
                    }

                    WorkloadRequest[] wrong = requests.ToArray();
                    wrong[0] = wrong[0] with { ExpectedIdentities = ["Patient/unexpected"] };
                    Assert.Throws<InvalidDataException>(() => CorrectnessOracle.ValidateCaseRequests(workload, corpus, wrong, 4));
                    Assert.Throws<InvalidDataException>(() => CorrectnessOracle.ValidateCaseRequests(workload, corpus, requests[..3], 4));
                }
            }
        }

        SuccessfulStatusProfile baseline = CorrectnessOracle.StatusProfile("baseline");
        SuccessfulStatusProfile candidate = CorrectnessOracle.StatusProfile("candidate");
        Assert.Equal(200, baseline.PutAsCreate);
        Assert.Equal(201, candidate.PutAsCreate);
        foreach (SuccessfulStatusProfile profile in new[] { baseline, candidate })
        {
            Assert.Equal(201, profile.Create);
            Assert.Equal(new[] { 200, 200, 200, 200, 200 },
                new[] { profile.Update, profile.Read, profile.Search, profile.Delete, profile.Hook });
        }

        Assert.Throws<InvalidDataException>(() => CorrectnessOracle.StatusProfile("moving-main"));
    }

    /// <summary>All notification versions carry exactly one event with distinct Bundle/status identities.</summary>
    [Fact]
    public void NotificationShapesHaveOneEventAndVersionCorrectStatus()
    {
        foreach (string version in new[] { "R4", "R4B", "R5" })
        {
            HashSet<string> bundles = new(StringComparer.Ordinal);
            HashSet<string> statuses = new(StringComparer.Ordinal);
            foreach (int number in new[] { 1, 2, 3, 4, 129 })
            {
                NotificationInput notification = Workloads.CreateNotification(version, number);
                CorrectnessOracle.ValidateNotification(notification);
                Assert.True(bundles.Add(notification.BundleId));
                Assert.True(statuses.Add(notification.StatusId));
                Assert.Equal(notification.Body, Workloads.CreateNotification(version, number).Body);
                JsonObject bundle = JsonNode.Parse(notification.Body)!.AsObject();
                JsonArray entries = bundle["entry"]!.AsArray();
                Assert.Equal(3, entries.Count);
                Assert.Equal(version == "R5" ? "subscription-notification" : "history", bundle["type"]!.GetValue<string>());
                JsonObject status = entries[0]!["resource"]!.AsObject();
                Assert.Equal(version == "R4" ? "Parameters" : "SubscriptionStatus", status["resourceType"]!.GetValue<string>());
                string expectedNumber = number.ToString(CultureInfo.InvariantCulture);
                if (version == "R4")
                {
                    Assert.Equal(6, status["parameter"]!.AsArray().Count);
                    Assert.Equal(expectedNumber, status["parameter"]![4]!["valueString"]!.GetValue<string>());
                    Assert.Equal(expectedNumber, status["parameter"]![5]!["part"]![0]!["valueString"]!.GetValue<string>());
                    status["parameter"]![5]!["part"]![0]!["valueString"] = number;
                }
                else
                {
                    Assert.Single(status["notificationEvent"]!.AsArray());
                    Assert.Equal(expectedNumber, status["eventsSinceSubscriptionStart"]!.GetValue<string>());
                    Assert.Equal(expectedNumber, status["notificationEvent"]![0]!["eventNumber"]!.GetValue<string>());
                    status["notificationEvent"]![0]!["eventNumber"] = number;
                }

                Assert.Throws<InvalidDataException>(() => CorrectnessOracle.ValidateNotification(
                    notification with { Body = JsonSerializer.SerializeToUtf8Bytes(bundle) }));
                JsonObject wrongContext = JsonNode.Parse(notification.Body)!.AsObject();
                wrongContext["entry"]![2]!["fullUrl"] = "https://example.org/fhir/source/Patient/wrong";
                Assert.Throws<InvalidDataException>(() => CorrectnessOracle.ValidateNotification(
                    notification with { Body = JsonSerializer.SerializeToUtf8Bytes(wrongContext) }));
                Assert.Throws<InvalidDataException>(() => CorrectnessOracle.ValidateNotification(
                    notification with { EventNumber = number + 1 }));
            }
        }

        Assert.Throws<ArgumentOutOfRangeException>(() => Workloads.CreateNotification("R4", 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Workloads.CreateNotification("R5", 100000000));
    }

    /// <summary>Search errors, incomplete pages, wrong content and unbound body/header identities are failures.</summary>
    [Fact]
    public void OracleRejectsErrorTruncatedSearchAndWrongIdentity()
    {
        const string BaseUrl = "http://127.0.0.1:58260/fhir/perf";
        DateTimeOffset now = DateTimeOffset.UtcNow;
        WorkloadCorpus corpus = Workloads.CreateCorpus("R4", 1000);
        string uri = BaseUrl + "/Patient?family=Family017&_count=100";
        Dictionary<string, ExpectedResource> expected = new(StringComparer.Ordinal);
        for (int index = 17; index < 1000; index += 100)
        {
            CorpusResource resource = corpus.Resources[index];
            ResponseEvidence response = ResourceResponse(resource.Body, resource.Id, 200, now);
            ResourceBinding binding = CorrectnessOracle.ValidateResponse(response, 200, resource.Body, BaseUrl, now.AddSeconds(-1));
            expected.Add(resource.Identity, new ExpectedResource(resource.Body, binding));
        }

        HttpExchangeEvidence page = SearchPage(uri, expected.Values.ToArray(), expected.Count, now);
        CorrectnessOracle.ValidateSearch([page], expected, uri, BaseUrl);
        string secondUri = uri + "&_page=2";
        HttpExchangeEvidence first = SearchPage(uri, expected.Values.Take(5).ToArray(), 10, now, secondUri);
        HttpExchangeEvidence second = SearchPage(secondUri, expected.Values.Skip(5).ToArray(), 10, now);
        Assert.Equal(secondUri, CorrectnessOracle.NextPageUri(first.Response, uri, BaseUrl));
        CorrectnessOracle.ValidateSearch([first, second], expected, uri, BaseUrl);
        Assert.Throws<InvalidDataException>(() => CorrectnessOracle.ValidateSearch([first], expected, uri, BaseUrl));
        Assert.Throws<InvalidDataException>(() => CorrectnessOracle.ValidateSearch([second, first], expected, uri, BaseUrl));
        Assert.Throws<InvalidDataException>(() => CorrectnessOracle.ValidateSearch(
            [SearchPage(uri, expected.Values.Take(5).ToArray(), 10, now)], expected, uri, BaseUrl));
        Assert.Throws<InvalidDataException>(() => CorrectnessOracle.ValidateSearch([page, page], expected, uri, BaseUrl));
        foreach (Action<JsonObject> mutate in new Action<JsonObject>[]
        {
            root => root["total"] = 9,
            root => root["type"] = "collection",
            root => root["entry"]!.AsArray().RemoveAt(0),
            root => root["entry"]!.AsArray().Add(root["entry"]![0]!.DeepClone()),
            root => root["entry"]![0]!["search"]!["mode"] = "outcome",
            root => root["entry"]![0]!["fullUrl"] = BaseUrl + "/Patient/wrong",
            root => root["entry"]![0]!["resource"]!["name"]![0]!["family"] = "Wrong",
            root => root["entry"]![0]!["resource"]!["id"] = "wrong",
            root => root["entry"]![0]!["resource"]!["meta"]!["versionId"] = "2",
            root => root["link"]![0]!["url"] = BaseUrl + "/Patient?family=Family018&_count=100",
            root => root["link"]!.AsArray().Add(new JsonObject { ["relation"] = "next", ["url"] = "https://example.org/other" }),
        })
        {
            JsonObject changed = JsonNode.Parse(page.Response.Body)!.AsObject();
            mutate(changed);
            HttpExchangeEvidence wrong = page with { Response = page.Response with { Body = JsonSerializer.SerializeToUtf8Bytes(changed) } };
            Assert.Throws<InvalidDataException>(() => CorrectnessOracle.ValidateSearch([wrong], expected, uri, BaseUrl));
        }

        foreach (int status in new[] { 201, 400, 500 })
        {
            Assert.Throws<InvalidDataException>(() => CorrectnessOracle.ValidateSearch(
                [page with { Response = page.Response with { StatusCode = status } }], expected, uri, BaseUrl));
        }

        byte[] outcome = Encoding.UTF8.GetBytes("""{"resourceType":"OperationOutcome","issue":[{"severity":"error","code":"invalid"}]}""");
        Assert.Throws<InvalidDataException>(() => CorrectnessOracle.ValidateSearch(
            [page with { Response = page.Response with { Body = outcome } }], expected, uri, BaseUrl));
        string emptyUri = BaseUrl + "/Observation?code=" + Uri.EscapeDataString("https://example.org/candle-perf/code|OutsideDomain") + "&_count=100";
        CorrectnessOracle.ValidateSearch([SearchPage(emptyUri, [], 0, now)],
            new Dictionary<string, ExpectedResource>(), emptyUri, BaseUrl);
        WorkloadCase create = Workloads.CreateCases().Single(workload =>
            workload.FhirVersion == "R4" && workload.PatientCount == 1000 && workload.CaseId == "patient-create");
        byte[] input = Workloads.CreateRequests(create, corpus)[0].Body!;
        ResponseEvidence created = ResourceResponse(input, "server-generated-1", 201, now);
        _ = CorrectnessOracle.ValidateResponse(created, 201, input, BaseUrl, now.AddSeconds(-1));
        foreach ((string header, string value) in new[]
        {
            ("Location", BaseUrl + "/Patient/other"),
            ("ETag", "W/\"2\""),
            ("Last-Modified", now.AddMinutes(-2).UtcDateTime.ToString("r", CultureInfo.InvariantCulture)),
        })
        {
            Dictionary<string, string[]> headers = new(created.Headers, StringComparer.OrdinalIgnoreCase) { [header] = [value] };
            Assert.Throws<InvalidDataException>(() => CorrectnessOracle.ValidateResponse(
                created with { Headers = headers }, 201, input, BaseUrl, now.AddSeconds(-1)));
        }

        JsonObject alteredBody = JsonNode.Parse(created.Body)!.AsObject();
        alteredBody["identifier"]![0]!["value"] = "another-request-sentinel";
        Assert.Throws<InvalidDataException>(() => CorrectnessOracle.ValidateResponse(
            created with { Body = JsonSerializer.SerializeToUtf8Bytes(alteredBody) }, 201, input, BaseUrl, now.AddSeconds(-1)));
    }

    /// <summary>Real field changes, every version step, distinct creations and exact growth are mandatory.</summary>
    [Fact]
    public void OracleRejectsNoOpUpdateAndWrongCreateGrowth()
    {
        const string BaseUrl = "http://127.0.0.1:58260/fhir/perf";
        WorkloadCorpus corpus = Workloads.CreateCorpus("R5", 100);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        foreach (string caseId in new[] { "patient-update", "observation-update" })
        {
            WorkloadCase workload = Workloads.CreateCases().Single(item =>
                item.FhirVersion == "R5" && item.PatientCount == 100 && item.CaseId == caseId);
            WorkloadRequest[] requests = Workloads.CreateRequests(workload, corpus);
            CorpusResource existing = corpus.Resources.Single(resource => resource.Identity == requests[0].RelativeUrl);
            byte[] previousContent = existing.Body;
            ResourceBinding previous = CorrectnessOracle.ValidateResponse(
                ResourceResponse(existing.Body, existing.Id, 200, now), 200, existing.Body, BaseUrl, now.AddSeconds(-1));
            foreach (WorkloadRequest request in requests)
            {
                string nextVersion = (request.Number + 1).ToString(CultureInfo.InvariantCulture);
                ResponseEvidence response = ResourceResponse(request.Body!, existing.Id, 200, now.AddSeconds(request.Number), nextVersion);
                Assert.Throws<InvalidDataException>(() => CorrectnessOracle.ValidateUpdateResponse(
                    response, 200, previousContent, previousContent, previous, BaseUrl));
                Assert.Throws<InvalidDataException>(() => CorrectnessOracle.ValidateUpdateResponse(
                    ResourceResponse(request.Body!, existing.Id, 200, now.AddSeconds(request.Number), previous.VersionId),
                    200, previousContent, request.Body!, previous, BaseUrl));
                Assert.Throws<InvalidDataException>(() => CorrectnessOracle.ValidateUpdateResponse(
                    ResourceResponse(request.Body!, "wrong-instance", 200, now.AddSeconds(request.Number), nextVersion),
                    200, previousContent, request.Body!, previous, BaseUrl));
                Assert.Throws<InvalidDataException>(() => CorrectnessOracle.ValidateUpdateResponse(
                    response with { StatusCode = 201 }, 200, previousContent, request.Body!, previous, BaseUrl));
                JsonObject wrong = JsonNode.Parse(response.Body)!.AsObject();
                if (caseId == "patient-update")
                {
                    wrong["active"] = !wrong["active"]!.GetValue<bool>();
                }
                else
                {
                    wrong["subject"]!["reference"] = "Patient/wrong-context";
                }

                Assert.Throws<InvalidDataException>(() => CorrectnessOracle.ValidateUpdateResponse(
                    response with { Body = JsonSerializer.SerializeToUtf8Bytes(wrong) },
                    200, previousContent, request.Body!, previous, BaseUrl));
                previous = CorrectnessOracle.ValidateUpdateResponse(response, 200, previousContent, request.Body!, previous, BaseUrl);
                previousContent = request.Body!;
                Assert.Equal(nextVersion, previous.VersionId);
            }

            ResponseEvidence finalRead = ResourceResponse(previousContent, existing.Id, 200, now.AddSeconds(4), "5");
            Assert.Equal(previous, CorrectnessOracle.ValidateResponse(finalRead, 200, previousContent, BaseUrl, now.AddSeconds(-1), previous));
        }

        WorkloadCase create = Workloads.CreateCases().Single(item =>
            item.FhirVersion == "R5" && item.PatientCount == 100 && item.CaseId == "patient-create");
        WorkloadRequest[] inputs = Workloads.CreateRequests(create, corpus);
        ResourceBinding[] created = inputs.Select(request => CorrectnessOracle.ValidateResponse(
            ResourceResponse(request.Body!, "new-" + request.Number.ToString("D8", CultureInfo.InvariantCulture), 201, now),
            201, request.Body!, BaseUrl, now.AddSeconds(-1))).ToArray();
        string[] initialIds = corpus.Resources.Where(resource => resource.ResourceType == "Patient").Select(resource => resource.Id).ToArray();
        CorrectnessOracle.ValidateCreateGrowth("Patient", initialIds, 100, 104, 4, created);
        foreach (int wrongCount in new[] { 100, 103, 105 })
        {
            Assert.Throws<InvalidDataException>(() => CorrectnessOracle.ValidateCreateGrowth("Patient", initialIds, 100, wrongCount, 4, created));
        }

        Assert.Throws<InvalidDataException>(() => CorrectnessOracle.ValidateCreateGrowth("Patient", initialIds, 100, 104, 4, created[..3]));
        ResourceBinding[][] invalid = [
            [created[0], created[0], created[2], created[3]],
            [created[0] with { Id = initialIds[0] }, .. created[1..]],
            [created[0] with { VersionId = "2" }, .. created[1..]],
            [created[0] with { ResourceType = "Observation" }, .. created[1..]],
        ];
        foreach (ResourceBinding[] bindings in invalid)
        {
            Assert.Throws<InvalidDataException>(() => CorrectnessOracle.ValidateCreateGrowth("Patient", initialIds, 100, 104, 4, bindings));
        }

        ResponseEvidence deletion = ResourceResponse(inputs[0].Body!, created[0].Id, 200, now);
        deletion.Headers.Remove("Location");
        deletion.Headers.Remove("ETag");
        deletion.Headers.Remove("Last-Modified");
        CorrectnessOracle.ValidateDeleteResponse(deletion, 200, new ExpectedResource(inputs[0].Body!, created[0]));
        Assert.Throws<InvalidDataException>(() => CorrectnessOracle.ValidateDeleteResponse(
            deletion with { StatusCode = 204 }, 200, new ExpectedResource(inputs[0].Body!, created[0])));
        TenantSnapshot initial = ClinicalSnapshot(0, 0);
        CorrectnessOracle.ValidateState(initial, ClinicalSnapshot(104, 1000), 104, 1000, 0,
            new Dictionary<string, bool>(), _sourceDirectory);
        Assert.Throws<InvalidDataException>(() => CorrectnessOracle.ValidateState(initial, ClinicalSnapshot(103, 1000),
            104, 1000, 0, new Dictionary<string, bool>(), _sourceDirectory));
    }

    /// <summary>Every receipt entry/event and absence of separately stored embedded clinical content are checked.</summary>
    [Fact]
    public void OracleRejectsMissingDuplicateOrMismatchedReceipt()
    {
        foreach (string version in new[] { "R4", "R4B", "R5" })
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            ReceiptExpectation[] expected = Enumerable.Range(1, 4).Select(number =>
                new ReceiptExpectation(Workloads.CreateNotification(version, number), now.AddMinutes(-1), now.AddMinutes(1))).ToArray();
            ReceiptSnapshot[] receipts = Enumerable.Range(1, 4).Select(number => Receipt(Workloads.CreateProbeChallenge(version, number))).ToArray();
            Dictionary<string, ResourceIdentity?> identities = new(StringComparer.Ordinal);
            Dictionary<string, bool> presence = new(StringComparer.Ordinal);
            foreach (ReceiptExpectation item in expected)
            {
                NotificationInput notification = item.Notification;
                identities.Add("Bundle/" + notification.BundleId, new("Bundle", notification.BundleId));
                identities.Add("Patient/" + notification.ContextId, null);
                identities.Add("Observation/" + notification.FocusId, null);
                presence.Add("Bundle/" + notification.BundleId, true);
                presence.Add("Patient/" + notification.ContextId, false);
                presence.Add("Observation/" + notification.FocusId, false);
            }

            TenantSnapshot initial = ClinicalSnapshot(0, 0);
            TenantSnapshot snapshot = ClinicalSnapshot(100, 1000, 4, receipts, identities);
            CorrectnessOracle.ValidateState(initial, snapshot, 100, 1000, 4, presence, _sourceDirectory);
            CorrectnessOracle.ValidateReceipt(snapshot, expected);
            string key = Assert.Single(snapshot.ReceivedNotifications).Key;
            foreach (ReceiptSnapshot[] wrong in new[]
            {
                Array.Empty<ReceiptSnapshot>(), receipts[..3],
                new[] { receipts[0], receipts[1], receipts[2], receipts[2] },
                receipts.AsEnumerable().Reverse().ToArray(),
            })
            {
                Assert.Throws<InvalidDataException>(() => CorrectnessOracle.ValidateReceipt(snapshot with
                {
                    ReceivedNotifications = new SortedDictionary<string, ReceiptSnapshot[]>(StringComparer.Ordinal) { [key] = wrong },
                }, expected));
            }

            foreach (Func<ReceiptSnapshot, ReceiptSnapshot> mutate in new Func<ReceiptSnapshot, ReceiptSnapshot>[]
            {
                receipt => receipt with { BundleId = "different" },
                receipt => receipt with { SubscriptionReference = "wrong-subscription" },
                receipt => receipt with { SubscriptionTopicCanonical = "wrong-topic" },
                receipt => receipt with { Status = "off" },
                receipt => receipt with { NotificationType = "Heartbeat" },
                receipt => receipt with { EventsSinceSubscriptionStart = 99 },
                receipt => receipt with { ProcessedDateTime = now.AddMinutes(-10) },
                receipt => receipt with { Errors = ["error"] },
                receipt => receipt with { NotificationEvents = [] },
                receipt => receipt with { NotificationEvents = [receipt.NotificationEvents[0], receipt.NotificationEvents[0]] },
                receipt => receipt with { NotificationEvents = [receipt.NotificationEvents[0] with { Id = "unexpected" }] },
                receipt => receipt with { NotificationEvents = [receipt.NotificationEvents[0] with { EventNumber = 99 }] },
                receipt => receipt with { NotificationEvents = [receipt.NotificationEvents[0] with { Timestamp = null }] },
                receipt => receipt with { NotificationEvents = [receipt.NotificationEvents[0] with { Timestamp = receipt.NotificationEvents[0].Timestamp?.AddSeconds(1) }] },
                receipt => receipt with { NotificationEvents = [receipt.NotificationEvents[0] with { FocusReference = "Observation/wrong" }] },
                receipt => receipt with { NotificationEvents = [receipt.NotificationEvents[0] with { AdditionalContextReferences = [] }] },
                receipt => receipt with { NotificationEvents = [receipt.NotificationEvents[0] with { AdditionalContextReferences = ["Patient/wrong"] }] },
                receipt => receipt with { NotificationEvents = [receipt.NotificationEvents[0] with
                {
                    AdditionalContextReferences = [.. receipt.NotificationEvents[0].AdditionalContextReferences,
                        .. receipt.NotificationEvents[0].AdditionalContextReferences],
                }] },
            })
            {
                ReceiptSnapshot[] wrong = receipts.ToArray();
                wrong[3] = mutate(wrong[3]);
                Assert.Throws<InvalidDataException>(() => CorrectnessOracle.ValidateReceipt(snapshot with
                {
                    ReceivedNotifications = new SortedDictionary<string, ReceiptSnapshot[]>(StringComparer.Ordinal) { [key] = wrong },
                }, expected));
            }

            Assert.Throws<InvalidDataException>(() => CorrectnessOracle.ValidateReceipt(snapshot with
            {
                ReceivedNotifications = new SortedDictionary<string, ReceiptSnapshot[]>(StringComparer.Ordinal) { ["wrong-key"] = receipts },
            }, expected));
            Assert.Throws<InvalidDataException>(() => CorrectnessOracle.ValidateReceipt(snapshot with { EventCount = 3 }, expected));
            Assert.Throws<InvalidDataException>(() => CorrectnessOracle.ValidateState(initial, snapshot with
            {
                ResourceCounts = new SortedDictionary<string, int>(snapshot.ResourceCounts, StringComparer.Ordinal) { ["Observation"] = 1001 },
            }, 100, 1000, 4, presence, _sourceDirectory));
            foreach ((string type, string id) in new[]
            {
                ("Patient", expected[3].Notification.ContextId), ("Observation", expected[3].Notification.FocusId),
            })
            {
                Dictionary<string, ResourceIdentity?> doubleStored = new(identities, StringComparer.Ordinal) { [type + "/" + id] = new(type, id) };
                Assert.Throws<InvalidDataException>(() => CorrectnessOracle.ValidateState(initial,
                    snapshot with { RequestedResources = doubleStored }, 100, 1000, 4, presence, _sourceDirectory));
            }
        }
    }

    private static ResponseEvidence ResourceResponse(byte[] content, string id, int status, DateTimeOffset storedAt, string version = "1")
    {
        JsonObject resource = JsonNode.Parse(content)!.AsObject();
        resource["id"] = id;
        resource["meta"] ??= new JsonObject();
        resource["meta"]!["versionId"] = version;
        resource["meta"]!["lastUpdated"] = storedAt.ToString("O", CultureInfo.InvariantCulture);
        return new(status, new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["Content-Type"] = ["application/fhir+json; charset=utf-8"],
            ["ETag"] = ["W/\"" + version + "\""],
            ["Last-Modified"] = [storedAt.UtcDateTime.ToString("r", CultureInfo.InvariantCulture)],
            ["Location"] = ["http://127.0.0.1:58260/fhir/perf/" + resource["resourceType"]!.GetValue<string>() + "/" + id],
        }, JsonSerializer.SerializeToUtf8Bytes(resource), storedAt, storedAt);
    }

    private static HttpExchangeEvidence SearchPage(
        string uri, ExpectedResource[] resources, int total, DateTimeOffset now, string? next = null)
    {
        JsonArray links = new(new JsonObject { ["relation"] = "self", ["url"] = uri });
        if (next is not null)
        {
            links.Add(new JsonObject { ["relation"] = "next", ["url"] = next });
        }

        JsonArray entries = new();
        foreach (ExpectedResource resource in resources)
        {
            ResourceBinding binding = resource.Binding;
            entries.Add(new JsonObject
            {
                ["fullUrl"] = "http://127.0.0.1:58260/fhir/perf/" + binding.ResourceType + "/" + binding.Id,
                ["resource"] = JsonNode.Parse(ResourceResponse(resource.Content, binding.Id, 200, binding.LastUpdated, binding.VersionId).Body),
                ["search"] = new JsonObject { ["mode"] = "match" },
            });
        }

        JsonObject bundle = new() { ["resourceType"] = "Bundle", ["type"] = "searchset", ["total"] = total, ["link"] = links };
        if (entries.Count > 0)
        {
            bundle["entry"] = entries;
        }

        return new(uri, new ResponseEvidence(200,
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase) { ["Content-Type"] = ["application/fhir+json"] },
            JsonSerializer.SerializeToUtf8Bytes(bundle), now, now));
    }

    private static TenantSnapshot ClinicalSnapshot(
        int patients, int observations, int bundles = 0, ReceiptSnapshot[]? receipts = null,
        Dictionary<string, ResourceIdentity?>? identities = null)
    {
        SortedDictionary<string, ReceiptSnapshot[]> received = new(StringComparer.Ordinal);
        if (receipts is not null)
        {
            received.Add("https://example.org/fhir/source/Subscription/perf-subscription", receipts);
        }

        return new(["perf"], new Dictionary<string, string?> { ["LoadDirectory"] = _sourceDirectory },
            new SortedDictionary<string, int>(StringComparer.Ordinal)
            {
                ["Bundle"] = bundles, ["CapabilityStatement"] = 3, ["Observation"] = observations, ["Patient"] = patients,
            }, identities ?? new Dictionary<string, ResourceIdentity?>(StringComparer.Ordinal), received,
            received.Count, receipts?.Length ?? 0, receipts?.Sum(receipt => receipt.NotificationEvents.Length) ?? 0,
            [], [], [_sourceDirectory], 0, 0);
    }

    private static ExperimentManifest Manifest() => new()
    {
        RunId = "phase1-001", OutputDirectory = ExperimentRunner.DataRoot + @"\phase1-001",
        ReportPath = ExperimentRunner.RequiredReportPath,
        Baseline = new BaselineInput
        {
            ArchivePath = ExperimentRunner.DataRoot + @"\artifacts-001\fhir-candle.2025.611.1422.nupkg",
            ArtifactDirectory = ExperimentRunner.DataRoot + @"\artifacts-001\baseline",
        },
        Candidate = new CandidateInput
        {
            ArtifactDirectory = ExperimentRunner.DataRoot + @"\artifacts-001\candidate-net8",
            BuildProvenancePath = ExperimentRunner.DataRoot + @"\artifacts-001\candidate-build.json",
        },
        ProbePath = @"C:\ai\git\fhir-candle\src\FhirCandle.Performance.Probe\bin\Release\net8.0\FhirCandle.Performance.Probe.dll",
        ToolSourceRevision = ExperimentRunner.CandidateRevision, ToolSourceDescription = "uncommitted phase-one tooling fixture",
        Isolation = new IsolationSettings(true, "unit-test manifest only, not live authorization",
            ExperimentRunner.DataRoot + @"\artifacts-001\isolation.txt"),
    };

    private static (ArtifactIdentity Artifact, AssemblyCorrelation[] Correlations) RequiredAssemblyFixture(
        string variant, string fhirVersion, string? modelName = null)
    {
        // The baseline R4 name/version/owner match phase1-004; all paths, hashes and other PE fields are synthetic.
        string packageName = "Hl7.Fhir." + fhirVersion;
        modelName ??= packageName;
        string packageVersion = variant == "baseline" ? "5.11.4" : "6.2.1";
        string root = @"C:\candle-perf-fixture\" + variant;
        string relativeDirectory = variant == "baseline" ? @"tools\net8.0\any\" : string.Empty;
        AssemblyFile assembly = new(relativeDirectory + modelName + ".dll", modelName, packageVersion + ".0",
            "fixture-file-version", "fixture-informational-version", "00000000-0000-0000-0000-000000000001", ".NETStandard,Version=v2.0");
        FileSeal file = new(assembly.RelativePath, 1, new string('A', 64));
        DependencyIdentity package = new(packageName, packageVersion, "package", Convert.ToBase64String(new byte[64]),
            ["lib/netstandard2.0/" + modelName + ".dll"]);
        ArtifactIdentity artifact = new(variant, variant == "baseline" ? ExperimentRunner.BaselineRevision : ExperimentRunner.CandidateRevision,
            packageVersion, root, relativeDirectory + "fhir-candle.dll", [file], [assembly], [package]);
        AssemblySnapshot loaded = new(assembly.Name, root + "\\" + assembly.RelativePath, assembly.AssemblyVersion,
            assembly.FileVersion, assembly.InformationalVersion, assembly.Mvid, file.Sha256);
        List<AssemblyCorrelation> correlations = [new(loaded, variant, package.Name, package.Version, package.Sha512)];
        // These stand in for the other already-validated correlations; this fixture never loads or seals files.
        foreach ((string name, string origin) in new[]
        {
            ("Hl7.Fhir.Base", variant),
            ("fhir-candle", variant),
            ("FhirCandle.Performance.Probe", "identical BCL startup hook"),
            ("System.Private.CoreLib", "Microsoft.NETCore.App/8.0.30"),
            ("Microsoft.AspNetCore.Server.Kestrel.Core", "Microsoft.AspNetCore.App/8.0.30"),
        })
        {
            correlations.Add(new AssemblyCorrelation(loaded with { Name = name, Path = root + "\\" + relativeDirectory + name + ".dll" },
                origin, name, "fixture-version", string.Empty));
        }

        return (artifact, correlations.ToArray());
    }

    private static ResponseEvidence CreatedObservation(ProbeChallenge challenge, DateTimeOffset now)
    {
        JsonObject resource = JsonNode.Parse(challenge.Observation)!.AsObject();
        resource["id"] = "server-assigned-1";
        resource["meta"] = new JsonObject { ["versionId"] = "1", ["lastUpdated"] = now.ToString("O", CultureInfo.InvariantCulture) };
        return new ResponseEvidence(201, new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["Content-Type"] = ["application/fhir+json; charset=utf-8"],
            ["ETag"] = ["W/\"1\""], ["Last-Modified"] = [now.UtcDateTime.ToString("r", CultureInfo.InvariantCulture)],
            ["Location"] = ["http://127.0.0.1:58260/fhir/perf/Observation/server-assigned-1"],
        }, Encoding.UTF8.GetBytes(resource.ToJsonString()), now, now);
    }

    private static ReceiptSnapshot Receipt(ProbeChallenge challenge) => new(
        challenge.BundleId, DateTimeOffset.UtcNow, "https://example.org/fhir/source/Subscription/perf-subscription",
        "https://example.org/candle-perf/SubscriptionTopic/observation", "active", "EventNotification", challenge.EventNumber,
        [new NotificationEventSnapshot(string.Empty, challenge.EventNumber,
            DateTimeOffset.Parse("2025-01-02T03:04:05Z", CultureInfo.InvariantCulture),
            "https://example.org/fhir/source/Observation/" + challenge.FocusId,
            ["https://example.org/fhir/source/Patient/" + challenge.ContextId])], []);

    private static FakeStore Store(ReceiptSnapshot[] receipts) => StoreObjects(receipts.Cast<object>().ToArray());

    private static FakeStore StoreObjects(object[] receipts)
    {
        ConcurrentDictionary<string, List<object>> received = new();
        if (receipts.Length > 0)
        {
            received["https://example.org/fhir/source/Subscription/perf-subscription"] = receipts.ToList();
        }

        return new FakeStore(new Dictionary<string, object>
        {
            ["Patient"] = new ExplicitDictionary(new Dictionary<string, object> { ["patient-00000000"] = new FakeResource("Patient", "patient-00000000") }),
            ["Observation"] = new ExplicitDictionary(new Dictionary<string, object> { ["observation"] = new FakeResource("Observation", "observation") }),
            ["Bundle"] = new ExplicitDictionary(new Dictionary<string, object> { ["notification-00000001"] = new FakeResource("Bundle", "notification-00000001") }),
        }, received);
    }

    /// <summary>Public resource identity fixture, not a Firely model.</summary>
    public sealed record FakeResource(string TypeName, string Id);

    /// <summary>An explicit-only dictionary, as in both historical and current manager implementations.</summary>
    public class ExplicitDictionary : IReadOnlyDictionary<string, object>
    {
        private readonly Dictionary<string, object> _values;
        private readonly int _countAdjustment;

        /// <summary>Constructs a dictionary; a deliberately wrong Count exercises inspection failure.</summary>
        public ExplicitDictionary(Dictionary<string, object> values, int countAdjustment = 0)
        {
            _values = values;
            _countAdjustment = countAdjustment;
        }

        object IReadOnlyDictionary<string, object>.this[string key] => _values[key];
        IEnumerable<string> IReadOnlyDictionary<string, object>.Keys => _values.Keys;
        IEnumerable<object> IReadOnlyDictionary<string, object>.Values => _values.Values;
        int IReadOnlyCollection<KeyValuePair<string, object>>.Count => _values.Count + _countAdjustment;
        bool IReadOnlyDictionary<string, object>.ContainsKey(string key) => _values.ContainsKey(key);
        bool IReadOnlyDictionary<string, object>.TryGetValue(string key, out object value) => _values.TryGetValue(key, out value!);
        IEnumerator<KeyValuePair<string, object>> IEnumerable<KeyValuePair<string, object>>.GetEnumerator() => _values.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => _values.GetEnumerator();
    }

    /// <summary>Both explicit resource views; differing fixture data detects accidental typed-view dispatch.</summary>
    public sealed class ExplicitResourceDictionary : ExplicitDictionary, IReadOnlyDictionary<string, FakeResource>
    {
        private readonly Dictionary<string, FakeResource> _typedValues;

        /// <summary>Constructs independent views; only the object view is valid for resource inspection.</summary>
        public ExplicitResourceDictionary(Dictionary<string, object> values, Dictionary<string, FakeResource> typedValues,
            int countAdjustment = 0) : base(values, countAdjustment)
        {
            _typedValues = typedValues;
        }

        FakeResource IReadOnlyDictionary<string, FakeResource>.this[string key] => _typedValues[key];
        IEnumerable<string> IReadOnlyDictionary<string, FakeResource>.Keys => _typedValues.Keys;
        IEnumerable<FakeResource> IReadOnlyDictionary<string, FakeResource>.Values => _typedValues.Values;
        int IReadOnlyCollection<KeyValuePair<string, FakeResource>>.Count => _typedValues.Count;
        bool IReadOnlyDictionary<string, FakeResource>.ContainsKey(string key) => _typedValues.ContainsKey(key);
        bool IReadOnlyDictionary<string, FakeResource>.TryGetValue(string key, out FakeResource value) => _typedValues.TryGetValue(key, out value!);
        IEnumerator<KeyValuePair<string, FakeResource>> IEnumerable<KeyValuePair<string, FakeResource>>.GetEnumerator() => _typedValues.GetEnumerator();
    }

    /// <summary>Only the public contract is inspectable; concrete members are explicit.</summary>
    public interface IFakeStore
    {
        /// <summary>Effective store settings.</summary>
        FakeConfiguration Config { get; }
        /// <summary>Dictionary of lists, not a scalar receipt counter.</summary>
        object ReceivedNotifications { get; }
        /// <summary>Loaded directives.</summary>
        string[] LoadedPackageDirectives { get; }
        /// <summary>Loaded package IDs.</summary>
        string[] LoadedPackageIds { get; }
        /// <summary>Loaded supplements.</summary>
        string[] LoadedSupplements { get; }
        /// <summary>Outbound subscriptions.</summary>
        object[] CurrentSubscriptions { get; }
        /// <summary>Topics.</summary>
        object[] CurrentTopics { get; }
    }

    /// <summary>Minimal in-process store contract fixture.</summary>
    public sealed class FakeStore : ExplicitDictionary, IFakeStore
    {
        private readonly object _receipts;

        /// <summary>Initializes only fixture state, never the real manager.</summary>
        public FakeStore(Dictionary<string, object> resources, object receipts) : base(resources)
        {
            _receipts = receipts;
        }

        FakeConfiguration IFakeStore.Config => new();
        object IFakeStore.ReceivedNotifications => _receipts;
        string[] IFakeStore.LoadedPackageDirectives => [];
        string[] IFakeStore.LoadedPackageIds => [];
        string[] IFakeStore.LoadedSupplements => [_sourceDirectory];
        object[] IFakeStore.CurrentSubscriptions => [];
        object[] IFakeStore.CurrentTopics => [];
    }

    /// <summary>Deliberately omits the required public receipt contract.</summary>
    public sealed class MissingReceiptStore : ExplicitDictionary
    {
        /// <summary>Initializes empty resource state.</summary>
        public MissingReceiptStore() : base(new Dictionary<string, object>())
        {
        }

        /// <summary>Otherwise valid settings must not hide a missing receipt seam.</summary>
        public FakeConfiguration Config => new();
    }

    /// <summary>Public configuration fixture with the inspected common settings.</summary>
    public sealed class FakeConfiguration
    {
        /// <summary>FHIR version.</summary>
        public string FhirVersion => "R4";
        /// <summary>Tenant identity.</summary>
        public string ControllerName => "perf";
        /// <summary>Public FHIR URL.</summary>
        public string BaseUrl => "http://127.0.0.1:58260/fhir/perf";
        /// <summary>No capacity eviction.</summary>
        public int MaxResourceCount => 0;
        /// <summary>Common subscription limit.</summary>
        public int MaxSubscriptionExpirationMinutes => 0;
        /// <summary>No protected content.</summary>
        public bool ProtectLoadedContent => false;
        /// <summary>Common R4 default.</summary>
        public bool SupportNotChanged => true;
        /// <summary>No SMART requirement.</summary>
        public bool SmartRequired => false;
        /// <summary>No SMART optional activity.</summary>
        public bool SmartAllowed => false;
        /// <summary>Allow deterministic seeding.</summary>
        public bool AllowExistingId => true;
        /// <summary>Allow PUT creation.</summary>
        public bool AllowCreateAsUpdate => true;
        /// <summary>Deterministic configured identity only; no filesystem access.</summary>
        public DirectoryInfo LoadDirectory => new(_sourceDirectory);
        /// <summary>No packages.</summary>
        public Dictionary<string, object> FhirPackages => new();
    }
}
