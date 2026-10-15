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
using System.Reflection;
using System.Runtime.InteropServices;
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
    private static long _fixtureTimestamp = 1000000;

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
            manifest with { Runtime = manifest.Runtime with { ServerGc = false } },
            manifest with { Runtime = manifest.Runtime with { TieredPgo = false } },
            manifest with { ReportPath = @"C:\ai\git\fhir-candle\scratch\other\performance.md" },
            manifest with { OutputDirectory = ExperimentRunner.DataRoot + @"\another-run" },
            manifest with { ProbePath = manifest.OutputDirectory + @"\probe.dll" },
            manifest with { RunId = @"..\escape" },
            manifest with { ProtocolVersion = "another-protocol" },
            manifest with { SchemaVersion = 1 },
            manifest with { Measurement = manifest.Measurement with { TrialsPerVariant = 2 } },
            manifest with { Measurement = manifest.Measurement with { MinimumSamples = 9 } },
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
        ClockObservation issued = ClockObservation.Capture();
        ProbeConfiguration configuration = new(
            "candle-perf-" + Guid.NewGuid().ToString("N"), process.Id, process.StartTime.ToUniversalTime().Ticks,
            owner, new string('A', 64), issued, issued.Before + (issued.Frequency * ProbeProtocol.ChildLifetimeSeconds),
            ProbeProtocol.RequestSeconds);
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
            hello with { ProtocolVersion = "candle-perf-phase3-v1" },
            hello with { ProtocolVersion = "candle-perf-phase3-v2" },
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
            (ClockObservation bundleStart, ClockObservation bundleEnd) = FixtureClocks(storedAt.AddTicks(-1), storedAt.AddTicks(1));
            ResponseEvidence bundleRead = new(200, new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["Content-Type"] = ["application/fhir+json; charset=utf-8"],
                ["ETag"] = ["W/\"1\""], ["Last-Modified"] = [storedAt.UtcDateTime.ToString("r", CultureInfo.InvariantCulture)],
                ["Location"] = ["http://127.0.0.1:58260/fhir/perf/Bundle/" + challenge.BundleId],
            }, JsonSerializer.SerializeToUtf8Bytes(storedBundle), bundleStart, bundleEnd);
            _ = CorrectnessOracle.ValidateResponse(bundleRead, 200, challenge.Notification,
                "http://127.0.0.1:58260/fhir/perf");
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
                    200, challenge.Notification, "http://127.0.0.1:58260/fhir/perf"));
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
                        200, challenge.Notification, "http://127.0.0.1:58260/fhir/perf"));
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
            (ClockObservation receiptStart, ClockObservation receiptEnd) = FixtureClocks(now.AddMinutes(-1), now);
            CorrectnessOracle.ValidateReceipt(snapshot, challenge, receiptStart, receiptEnd);
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
                Assert.Throws<InvalidDataException>(() => CorrectnessOracle.ValidateReceipt(wrong, challenge, receiptStart, receiptEnd));
            }

            ResponseEvidence response = CreatedObservation(challenge, now);
            ResourceBinding binding = CorrectnessOracle.ValidateResponse(response, 201, challenge.Observation,
                "http://127.0.0.1:58260/fhir/perf");
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
                    201, challenge.Observation, "http://127.0.0.1:58260/fhir/perf"));
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
                201, challenge.Observation, "http://127.0.0.1:58260/fhir/perf"));
            Assert.Throws<InvalidDataException>(() => CorrectnessOracle.ValidateResponse(response with { StatusCode = 422 },
                201, challenge.Observation, "http://127.0.0.1:58260/fhir/perf"));
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
            ResourceBinding binding = CorrectnessOracle.ValidateResponse(response, 200, resource.Body, BaseUrl);
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
        _ = CorrectnessOracle.ValidateResponse(created, 201, input, BaseUrl);
        foreach ((string header, string value) in new[]
        {
            ("Location", BaseUrl + "/Patient/other"),
            ("ETag", "W/\"2\""),
            ("Last-Modified", now.AddMinutes(-2).UtcDateTime.ToString("r", CultureInfo.InvariantCulture)),
        })
        {
            Dictionary<string, string[]> headers = new(created.Headers, StringComparer.OrdinalIgnoreCase) { [header] = [value] };
            Assert.Throws<InvalidDataException>(() => CorrectnessOracle.ValidateResponse(
                created with { Headers = headers }, 201, input, BaseUrl));
        }

        JsonObject alteredBody = JsonNode.Parse(created.Body)!.AsObject();
        alteredBody["identifier"]![0]!["value"] = "another-request-sentinel";
        Assert.Throws<InvalidDataException>(() => CorrectnessOracle.ValidateResponse(
            created with { Body = JsonSerializer.SerializeToUtf8Bytes(alteredBody) }, 201, input, BaseUrl));
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
                ResourceResponse(existing.Body, existing.Id, 200, now), 200, existing.Body, BaseUrl);
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
            Assert.Equal(previous, CorrectnessOracle.ValidateResponse(finalRead, 200, previousContent, BaseUrl, previous));
        }

        WorkloadCase create = Workloads.CreateCases().Single(item =>
            item.FhirVersion == "R5" && item.PatientCount == 100 && item.CaseId == "patient-create");
        WorkloadRequest[] inputs = Workloads.CreateRequests(create, corpus);
        ResourceBinding[] created = inputs.Select(request => CorrectnessOracle.ValidateResponse(
            ResourceResponse(request.Body!, "new-" + request.Number.ToString("D8", CultureInfo.InvariantCulture), 201, now),
            201, request.Body!, BaseUrl)).ToArray();
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
                FixtureReceiptExpectation(Workloads.CreateNotification(version, number), now.AddMinutes(-1), now.AddMinutes(1))).ToArray();
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

    /// <summary>Scripted reads reproduce the observed rollback without changing the machine clock or waiting for NTP.</summary>
    [Fact]
    public void ClockObservationsBracketUtcAndBoundForwardBackwardSteps()
    {
        DateTimeOffset startedUtc = DateTimeOffset.Parse("2026-10-07T03:14:24.2135622Z", CultureInfo.InvariantCulture);
        DateTimeOffset completedUtc = DateTimeOffset.Parse("2026-10-07T03:14:23.9405349Z", CultureInfo.InvariantCulture);
        const long Start = 18581526495358;
        const long End = 18581533966826;
        ScriptedClock clock = new([Start + 1, Start + 2, End - 2, End - 1], [startedUtc, completedUtc], 10000000);
        ClockObservation first = ClockObservation.Capture(clock);
        ClockObservation last = ClockObservation.Capture(clock);
        ClockAssessment rollback = ClockObservation.Assess(first, last);
        Assert.Equal(startedUtc, first.Utc);
        Assert.Equal(completedUtc, last.Utc);
        Assert.True(last.Utc < first.Utc);
        Assert.Equal(0.7471468, (double)(End - Start) / first.Frequency, 7);
        Assert.Equal(End - Start - 4, rollback.MinimumElapsedTicks);
        Assert.Equal(End - Start - 2, rollback.MaximumElapsedTicks);
        Assert.True(rollback.AdjustmentDetected);
        Assert.True(rollback.MaximumAdjustmentTicks < -TimeSpan.TicksPerSecond);
        Assert.Equal(completedUtc.AddTicks(-rollback.MaximumElapsedTicks), rollback.EarliestUtc);
        Assert.Equal(startedUtc.AddTicks(rollback.MaximumElapsedTicks), rollback.LatestUtc);

        foreach (int stepSeconds in new[] { -2, 0, 2 })
        {
            ScriptedClock source = new([100, 102, 1000100, 1000102],
                [startedUtc, startedUtc.AddTicks(1000000).AddSeconds(stepSeconds)], 10000000);
            ClockObservation before = ClockObservation.Capture(source);
            ClockObservation after = ClockObservation.Capture(source);
            ClockAssessment assessment = ClockObservation.Assess(before, after);
            Assert.Equal(stepSeconds != 0, assessment.AdjustmentDetected);
            Assert.InRange(TimeSpan.FromSeconds(stepSeconds).Ticks, assessment.MinimumAdjustmentTicks, assessment.MaximumAdjustmentTicks);
            Assert.Equal(4, assessment.MaximumElapsedTicks - assessment.MinimumElapsedTicks);
            Assert.True(assessment.Contains(before.Utc));
            Assert.True(assessment.Contains(after.Utc));
            Assert.False(assessment.Contains(assessment.EarliestUtc.AddTicks(-1)));
            Assert.False(assessment.Contains(assessment.LatestUtc.AddTicks(1)));
        }

        // Rounding is outwards in UTC ticks, including a frequency that is not a multiple of ten million.
        ClockAssessment fractional = ClockObservation.Assess(new(startedUtc, 1, 1, 3), new(startedUtc.AddSeconds(1), 2, 2, 3));
        Assert.Equal(3333333, fractional.MinimumElapsedTicks);
        Assert.Equal(3333334, fractional.MaximumElapsedTicks);
    }

    /// <summary>All 128 R5 reference-search warm responses remain complete, including the real operation-64 rollback shape.</summary>
    [Fact]
    public void RollbackWarmupSearchRetainsEveryOperationAndIndependentTiming()
    {
        const string BaseUrl = "http://127.0.0.1:58260/fhir/perf";
        WorkloadCorpus corpus = Workloads.CreateCorpus("R5", 1000);
        WorkloadCase workload = Workloads.CreateCases().Single(item =>
            item.FhirVersion == "R5" && item.PatientCount == 1000 && item.CaseId == "observation-reference");
        TrialInputs inputs = Workloads.CreateTrialInputs(workload, corpus, 1024);
        DateTimeOffset utc = DateTimeOffset.Parse("2026-10-07T03:14:24.2135622Z", CultureInfo.InvariantCulture);
        DateTimeOffset rollbackUtc = DateTimeOffset.Parse("2026-10-07T03:14:23.9405349Z", CultureInfo.InvariantCulture);
        Dictionary<string, ExpectedResource> resources = corpus.Resources.ToDictionary(resource => resource.Identity,
            resource => new ExpectedResource(resource.Body, new(resource.ResourceType, resource.Id, "1", utc.AddDays(-1))));
        OperationResult[] results = inputs.Warmup.Select(request =>
        {
            long start = 18581526495358 + ((request.Number - 64L) * 10000000);
            long end = start + 7471468;
            DateTimeOffset first = utc.AddSeconds(request.Number - 64);
            if (request.Number > 64)
            {
                first = first.AddTicks((rollbackUtc - utc).Ticks - (end - start - 3));
            }
            DateTimeOffset last = request.Number == 64 ? rollbackUtc : first.AddTicks(end - start - 3);
            HttpExchangeEvidence page = SearchPage(BaseUrl + "/" + request.RelativeUrl,
                request.ExpectedIdentities.Select(identity => resources[identity]).ToArray(), 10, first);
            ResponseEvidence response = ObserveResponse(page.Response, first, last, start, end);
            return new OperationResult(new(request.Number, start, end, 1, true), [page with { Response = response }], 1);
        }).ToArray();
        CorrectnessOracle.ValidateCaseRequests(workload, corpus, inputs.Warmup, 128);
        MeasurementReport.ValidateOperationResponses(results, 10000000, results[0].Timing.Start, results[^1].Timing.End);
        BatchValidation valid = CorrectnessOracle.ValidateBatchResponses(workload, inputs.Warmup,
            results.Select(result => new CompletedOperation(result.Timing.Number, result.Exchanges)).ToArray(),
            resources, CorrectnessOracle.StatusProfile("baseline"), BaseUrl);
        Assert.Equal(128, valid.Operations.Length);
        Assert.All(valid.Operations, operation => Assert.Equal(10, operation.Bindings.Length));
        ResponseEvidence failedHistorically = results[63].Exchanges[0].Response;
        Assert.Equal(utc, failedHistorically.StartedUtc);
        Assert.Equal(rollbackUtc, failedHistorically.CompletedUtc);
        Assert.True(CorrectnessOracle.ValidateResponseTiming(failedHistorically).AdjustmentDetected);
        byte[] saved = MeasurementReport.SerializeEvidence(results);
        OperationResult[] decoded = JsonSerializer.Deserialize<OperationResult[]>(saved, ProbeProtocol.JsonOptions)!;
        MeasurementReport.ValidateOperationResponses(decoded, 10000000, results[0].Timing.Start, results[^1].Timing.End);
        Assert.Equal(saved, MeasurementReport.SerializeEvidence(decoded));
        // Exercise the same evidence-only clock aggregation used for raw seed/readiness/warm/audit streams.
        Type clockType = typeof(MeasurementReport).GetNestedType("ResponseClock", BindingFlags.NonPublic)!;
        object clocks = JsonSerializer.Deserialize(JsonSerializer.Serialize(decoded.Select(result => new
        {
            Label = "warmup:" + result.Timing.Number.ToString(CultureInfo.InvariantCulture),
            result.Exchanges[0].Response.Started, result.Exchanges[0].Response.Completed,
        }), ProbeProtocol.JsonOptions), typeof(List<>).MakeGenericType(clockType), ProbeProtocol.JsonOptions)!;
        ClockObservation firstClock = decoded[0].Exchanges[0].Response.Started;
        ClockObservation lastClock = decoded[^1].Exchanges[0].Response.Completed;
        ClockObservation launched = new(firstClock.Utc.AddTicks(-1000), firstClock.Before - 1000, firstClock.Before - 1000, firstClock.Frequency);
        ClockObservation exited = new(lastClock.Utc.AddTicks(1000), lastClock.After + 1000, lastClock.After + 1000, lastClock.Frequency);
        object summary = typeof(MeasurementReport).GetMethod("ValidateResponseClocks", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [clocks, launched, exited])!;
        JsonElement summaryJson = JsonSerializer.SerializeToElement(summary, ProbeProtocol.JsonOptions);
        Assert.Equal(128, summaryJson.GetProperty("Responses").GetInt32());
        Assert.Equal(1, summaryJson.GetProperty("AdjustedResponses").GetInt32());
        Assert.Contains(utc.ToString("O", CultureInfo.InvariantCulture),
            summaryJson.GetProperty("Examples")[0].GetString()!, StringComparison.Ordinal);
        Assert.Throws<InvalidDataException>(() => CorrectnessOracle.ValidateBatchResponses(workload, inputs.Warmup,
            decoded[..127].Select(result => new CompletedOperation(result.Timing.Number, result.Exchanges)).ToArray(),
            resources, CorrectnessOracle.StatusProfile("baseline"), BaseUrl));
    }

    /// <summary>Clock steps permit only request-derived mutation metadata; old read bindings, content, versions and headers stay exact.</summary>
    [Fact]
    public void ClockAwareMutationsAndImmutableReadsKeepStrictMetadata()
    {
        const string BaseUrl = "http://127.0.0.1:58260/fhir/perf";
        DateTimeOffset utc = DateTimeOffset.Parse("2026-10-07T03:14:24Z", CultureInfo.InvariantCulture);
        WorkloadCorpus corpus = Workloads.CreateCorpus("R5", 100);
        foreach (string caseId in new[] { "patient-update", "observation-update" })
        {
            WorkloadCase workload = Workloads.CreateCases().Single(item =>
                item.FhirVersion == "R5" && item.PatientCount == 100 && item.CaseId == caseId);
            WorkloadRequest request = Workloads.CreateRequests(workload, corpus)[0];
            CorpusResource original = corpus.Resources.Single(resource => resource.Identity == request.RelativeUrl);
            ResourceBinding previous = new(original.ResourceType, original.Id, "1", utc);
            foreach (int step in new[] { -2, 0, 2 })
            {
                // The request already starts after a between-operation rollback; an additional step can occur inside it.
                DateTimeOffset first = utc.AddSeconds(-5);
                DateTimeOffset last = first.AddMilliseconds(100).AddSeconds(step);
                DateTimeOffset assigned = last.AddTicks(-1);
                ResponseEvidence update = ObserveResponse(ResourceResponse(request.Body!, original.Id, 200, assigned, "2"),
                    first, last, 10000000, 11000000);
                ResourceBinding binding = CorrectnessOracle.ValidateUpdateResponse(
                    update, 200, original.Body, request.Body!, previous, BaseUrl);
                Assert.True(binding.LastUpdated < previous.LastUpdated);
                ClockAssessment interval = CorrectnessOracle.ValidateResponseTiming(update);
                foreach (DateTimeOffset outside in new[] { interval.EarliestUtc.AddTicks(-1), interval.LatestUtc.AddTicks(1) })
                {
                    ResponseEvidence wrong = ResourceResponse(request.Body!, original.Id, 200, outside, "2") with
                    {
                        Started = update.Started, Completed = update.Completed,
                    };
                    Assert.Throws<InvalidDataException>(() => CorrectnessOracle.ValidateUpdateResponse(
                        wrong, 200, original.Body, request.Body!, previous, BaseUrl));
                }

                ResponseEvidence read = ObserveResponse(ResourceResponse(request.Body!, original.Id, 200, assigned, "2"),
                    utc.AddDays(-1), utc.AddDays(-1).AddMilliseconds(100), 12000000, 13000000);
                CorrectnessOracle.ValidateSequentialResponses([update, read]);
                Assert.Equal(binding, CorrectnessOracle.ValidateResponse(read, 200, request.Body!, BaseUrl, binding));
                foreach (string alteredTimestamp in new[]
                {
                    assigned.AddTicks(1).ToString("O", CultureInfo.InvariantCulture),
                    assigned.ToOffset(TimeSpan.FromHours(1)).ToString("O", CultureInfo.InvariantCulture),
                })
                {
                    JsonObject wrong = JsonNode.Parse(read.Body)!.AsObject();
                    wrong["meta"]!["lastUpdated"] = alteredTimestamp;
                    Assert.Throws<InvalidDataException>(() => CorrectnessOracle.ValidateResponse(
                        read with { Body = JsonSerializer.SerializeToUtf8Bytes(wrong) }, 200, request.Body!, BaseUrl, binding));
                }

                Dictionary<string, string[]> wrongHeaders = new(update.Headers, StringComparer.OrdinalIgnoreCase) { ["ETag"] = ["W/\"3\""] };
                Assert.Throws<InvalidDataException>(() => CorrectnessOracle.ValidateUpdateResponse(
                    update with { Headers = wrongHeaders }, 200, original.Body, request.Body!, previous, BaseUrl));
                Assert.Throws<InvalidDataException>(() => CorrectnessOracle.ValidateUpdateResponse(
                    update, 200, request.Body!, request.Body!, binding, BaseUrl));
            }
        }

        WorkloadCase create = Workloads.CreateCases().Single(item =>
            item.FhirVersion == "R5" && item.PatientCount == 100 && item.CaseId == "patient-create");
        byte[] content = Workloads.CreateRequests(create, corpus)[0].Body!;
        ResponseEvidence created = ObserveResponse(ResourceResponse(content, "new-clock-bound-patient", 201, utc.AddMilliseconds(-250)),
            utc, utc.AddMilliseconds(-273), 18581526495358, 18581533966826);
        ResourceBinding newlyBound = CorrectnessOracle.ValidateResponse(created, 201, content, BaseUrl);
        Assert.Equal("1", newlyBound.VersionId);
        ResponseEvidence wrongCreation = ResourceResponse(content, newlyBound.Id, 201,
            CorrectnessOracle.ValidateResponseTiming(created).LatestUtc.AddTicks(1)) with
        {
            Started = created.Started, Completed = created.Completed,
        };
        Assert.Throws<InvalidDataException>(() => CorrectnessOracle.ValidateResponse(wrongCreation, 201, content, BaseUrl));
    }

    /// <summary>Receipts and first Bundle metadata bind to each POST, including steps during and between requests, never to a later GET.</summary>
    [Fact]
    public void HookReceiptRangesSurviveClockStepsWithoutAcceptingWrongTimestamps()
    {
        const string BaseUrl = "http://127.0.0.1:58260/fhir/perf";
        DateTimeOffset utc = DateTimeOffset.Parse("2026-10-07T03:14:24Z", CultureInfo.InvariantCulture);
        foreach (string version in new[] { "R4", "R4B", "R5" })
        {
            WorkloadCorpus corpus = Workloads.CreateCorpus(version, 100);
            WorkloadCase workload = Workloads.CreateCases().Single(item =>
                item.FhirVersion == version && item.PatientCount == 100 && item.CaseId == "subscription-receive");
            WorkloadRequest[] requests = Workloads.CreateRequests(workload, corpus);
            double[] starts = [0, -2, -1.8, 3.1];
            double[] ends = [0.1, -1.9, 3, 1.2];
            CompletedOperation[] completed = requests.Select((request, index) =>
            {
                ResponseEvidence outcome = new(200, new() { ["Content-Type"] = ["application/fhir+json"] },
                    Encoding.UTF8.GetBytes("""{"resourceType":"OperationOutcome","id":"00000000-0000-0000-0000-000000000001","issue":[{"severity":"information","code":"success","diagnostics":"Subscription Notification Received"}]}"""),
                    default, default);
                ResponseEvidence observed = ObserveResponse(outcome, utc.AddSeconds(starts[index]), utc.AddSeconds(ends[index]),
                    10000000 + (index * 2000000), 11000000 + (index * 2000000));
                return new CompletedOperation(request.Number, [new(BaseUrl + "/" + request.RelativeUrl, observed)]);
            }).ToArray();
            BatchValidation audit = CorrectnessOracle.ValidateBatchResponses(workload, requests, completed,
                new Dictionary<string, ExpectedResource>(), CorrectnessOracle.StatusProfile("candidate"), BaseUrl);
            ReceiptSnapshot[] receipts = audit.Receipts.Select(receipt =>
                Receipt(Workloads.CreateProbeChallenge(version, receipt.Notification.EventNumber)) with
                {
                    ProcessedDateTime = receipt.Completed.Utc.AddTicks(-1),
                }).ToArray();
            CorrectnessOracle.ValidateReceipt(ClinicalSnapshot(100, 1000, 4, receipts), audit.Receipts);
            foreach (int index in Enumerable.Range(0, 4))
            {
                ReceiptExpectation expected = audit.Receipts[index];
                ClockAssessment bounds = ClockObservation.Assess(expected.Started, expected.Completed);
                foreach (DateTimeOffset wrongTimestamp in new[] { bounds.EarliestUtc.AddTicks(-1), bounds.LatestUtc.AddTicks(1) })
                {
                    ReceiptSnapshot[] wrong = receipts.ToArray();
                    wrong[index] = wrong[index] with { ProcessedDateTime = wrongTimestamp };
                    Assert.Throws<InvalidDataException>(() => CorrectnessOracle.ValidateReceipt(
                        ClinicalSnapshot(100, 1000, 4, wrong), audit.Receipts));
                }

                ResponseEvidence bundleRead = ObserveResponse(ResourceResponse(expected.Notification.Body,
                    expected.Notification.BundleId, 200, expected.Completed.Utc.AddTicks(-2)),
                    utc.AddDays(-1), utc.AddDays(-1).AddMilliseconds(100), 30000000 + (index * 2000000), 31000000 + (index * 2000000));
                ResourceBinding binding = CorrectnessOracle.ValidateResponse(
                    bundleRead, 200, expected.Notification.Body, BaseUrl, received: expected);
                Assert.Equal(binding, CorrectnessOracle.ValidateResponse(bundleRead, 200, expected.Notification.Body, BaseUrl, binding, expected));
                Assert.Throws<InvalidDataException>(() => CorrectnessOracle.ValidateResponse(
                    bundleRead, 200, expected.Notification.Body, BaseUrl)); // The later GET did not create the Bundle.
                ResponseEvidence wrongMetadata = ResourceResponse(expected.Notification.Body,
                    expected.Notification.BundleId, 200, bounds.LatestUtc.AddTicks(1)) with
                {
                    Started = bundleRead.Started, Completed = bundleRead.Completed,
                };
                Assert.Throws<InvalidDataException>(() => CorrectnessOracle.ValidateResponse(
                    wrongMetadata, 200, expected.Notification.Body, BaseUrl, received: expected));
            }
        }
    }

    /// <summary>Strict readers and independent oracles cannot substitute defaults or operation timings for missing endpoint evidence.</summary>
    [Fact]
    public void ClockEvidenceRejectsMissingDefaultNegativeAndReorderedObservations()
    {
        DateTimeOffset utc = DateTimeOffset.Parse("2026-10-07T03:14:24Z", CultureInfo.InvariantCulture);
        CorpusResource resource = Workloads.CreateCorpus("R4", 100).Resources[0];
        ResponseEvidence valid = ObserveResponse(ResourceResponse(resource.Body, resource.Id, 200, utc.AddMilliseconds(50)),
            utc, utc.AddMilliseconds(100), 10000000, 11000000);
        ClockObservation[] wrongStarts = [
            default,
            valid.Started with { Before = 0 },
            valid.Started with { Before = -1 },
            valid.Started with { After = valid.Started.Before - 1 },
            valid.Started with { Frequency = 0 },
            valid.Started with { Frequency = -1 },
            valid.Started with { Frequency = long.MaxValue },
            valid.Started with { Utc = default },
            valid.Started with { Utc = utc.ToOffset(TimeSpan.FromHours(1)) },
            valid.Completed,
        ];
        foreach (ClockObservation wrong in wrongStarts)
        {
            Assert.Throws<InvalidDataException>(() => CorrectnessOracle.ValidateResponseTiming(valid with { Started = wrong }));
        }

        Assert.Throws<InvalidDataException>(() => CorrectnessOracle.ValidateResponseTiming(valid with { Completed = default }));
        Assert.Throws<InvalidDataException>(() => CorrectnessOracle.ValidateResponseTiming(
            valid with { Completed = valid.Completed with { Frequency = valid.Started.Frequency + 1 } }));
        Assert.Throws<InvalidDataException>(() => CorrectnessOracle.ValidateSequentialResponses([valid, valid]));
        Assert.Throws<InvalidDataException>(() => ClockObservation.Assess(
            new(DateTimeOffset.MaxValue, 1, 1, 1), new(DateTimeOffset.MaxValue, 2, 2, 1)));
        foreach (string endpoint in new[] { "Started", "Completed" })
        {
            JsonObject missing = JsonSerializer.SerializeToNode(valid, ProbeProtocol.JsonOptions)!.AsObject();
            Assert.True(missing.Remove(endpoint));
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ResponseEvidence>(missing.ToJsonString(), ProbeProtocol.JsonOptions));
            foreach (string field in new[] { "Utc", "Before", "After", "Frequency" })
            {
                missing = JsonSerializer.SerializeToNode(valid, ProbeProtocol.JsonOptions)!.AsObject();
                Assert.True(missing[endpoint]!.AsObject().Remove(field));
                Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ResponseEvidence>(missing.ToJsonString(), ProbeProtocol.JsonOptions));
            }
        }

        OperationResult result = new(new(64, 10000000, 11000000, 1, true), [new("unit-fixture", valid)], 1);
        MeasurementReport.ValidateOperationResponses([result], 10000000, 9999999, 11000001);
        foreach (OperationResult wrong in new[]
        {
            result with { Timing = result.Timing with { Start = valid.Started.Before + 1 } },
            result with { Timing = result.Timing with { End = valid.Completed.After - 1 } },
            result with { Timing = result.Timing with { End = result.Timing.Start } },
            result with { Timing = result.Timing with { Start = 0 } },
            result with { Timing = result.Timing with { Completed = false } },
            result with { AttemptedHttpRequests = 2 },
        })
        {
            Assert.Throws<InvalidDataException>(() => MeasurementReport.ValidateOperationResponses([wrong], 10000000, 1, 12000000));
        }

        Assert.Throws<InvalidDataException>(() => MeasurementReport.ValidateOperationResponses([result], 1000, 1, 12000000));
        foreach (string field in new[] { "Start", "End", "Number", "HttpRequests", "Completed" })
        {
            JsonObject missing = JsonSerializer.SerializeToNode(result.Timing, ProbeProtocol.JsonOptions)!.AsObject();
            Assert.True(missing.Remove(field));
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<OperationTiming>(missing.ToJsonString(), ProbeProtocol.JsonOptions));
        }

        TrialRecord trial = MeasurementTrialFixture(MeasurementFixtureSchedule()[0]);
        foreach (string field in new[] { "SchemaVersion", "Parent", "Started", "Finished" })
        {
            JsonObject missing = JsonSerializer.SerializeToNode(trial, ProbeProtocol.JsonOptions)!.AsObject();
            Assert.True(missing.Remove(field));
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<TrialRecord>(missing.ToJsonString(), ProbeProtocol.JsonOptions));
        }
    }

    /// <summary>Wall-clock steps neither overlap sequential children nor excuse forged identities, clocks or metric origins.</summary>
    [Fact]
    public void LifecycleOrderUsesParentMonotonicEvidenceNotUtcChronology()
    {
        (FrozenMeasurement frozen, TrialRecord[] trials) = MeasurementFixture();
        trials[0] = trials[0] with { Finished = trials[0].Finished with { Utc = trials[0].StartedUtc.AddSeconds(-1) } };
        trials[1] = trials[1] with
        {
            Started = trials[1].Started with { Utc = trials[0].StartedUtc.AddDays(-1) },
            Finished = trials[1].Finished with { Utc = trials[0].StartedUtc.AddDays(1) },
        };
        Assert.Empty(MeasurementReport.ValidateMatrix(frozen, trials));
        Assert.True(trials[0].ProcessStartUtcTicks > trials[0].FinishedUtc.UtcTicks);
        Assert.True(trials[1].StartedUtc < trials[0].FinishedUtc);
        foreach (TrialRecord invalid in new[]
        {
            trials[1] with { Parent = default },
            trials[1] with { Parent = trials[1].Parent with { ProcessId = trials[1].Parent.ProcessId + 1 } },
            trials[1] with { Parent = trials[1].Parent with { ProcessStartUtcTicks = trials[1].Parent.ProcessStartUtcTicks + 1 } },
            trials[1] with { Started = default },
            trials[1] with { Finished = trials[1].Started },
            trials[1] with { Started = trials[0].Started },
            trials[1] with { Started = trials[1].Started with { Frequency = 10000000 } },
            trials[1] with { ProcessStartUtcTicks = trials[1].ProcessStartUtcTicks + 1 },
            trials[1] with { SchemaVersion = 1 },
        })
        {
            TrialRecord[] wrong = trials.ToArray();
            wrong[1] = invalid;
            Assert.NotEmpty(MeasurementReport.ValidateMatrix(frozen, wrong));
        }
    }

    /// <summary>Saved launch/process/exit records retain exact OS identities while parent observations prove actual lifetime ordering.</summary>
    [Fact]
    public async Task SavedLifecycleRejectsForgedIdentityAndMissingClockEvidence()
    {
        string root = Path.Combine(Path.GetTempPath(), "candle-clock-lifecycle-fixture-" + Guid.NewGuid().ToString("N"));
        const string DirectoryName = "unit-child";
        Directory.CreateDirectory(Path.Combine(root, DirectoryName));
        try
        {
            TrialRecord trial = MeasurementTrialFixture(MeasurementFixtureSchedule()[0]);
            ClockObservation issued = trial.Started with { Before = 100010, After = 100011 };
            ClockObservation requested = issued with { Utc = trial.StartedUtc.AddSeconds(-2), Before = 100020, After = 100021 };
            ClockObservation launched = requested with { Before = 100030, After = 100031 };
            ClockObservation exited = requested with { Utc = trial.StartedUtc.AddSeconds(-3), Before = 125000, After = 125001 };
            async Task Save(string name, object value) => await File.WriteAllBytesAsync(Path.Combine(root, DirectoryName, name),
                JsonSerializer.SerializeToUtf8Bytes(value, ProbeProtocol.JsonOptions));
            await Save("launch.json", new
            {
                Requested = requested,
                ProbeConfiguration = new
                {
                    ParentProcessId = trial.Parent.ProcessId, ParentStartTimeUtcTicks = trial.Parent.ProcessStartUtcTicks,
                    Issued = issued, DeadlineTimestamp = issued.Before + (issued.Frequency * ProbeProtocol.ChildLifetimeSeconds),
                    RequestTimeoutSeconds = ProbeProtocol.RequestSeconds,
                },
            });
            await Save("process.json", new
            {
                trial.ProcessId, StartedUtc = new DateTimeOffset(trial.ProcessStartUtcTicks, TimeSpan.Zero),
                trial.Parent, Observed = launched,
            });
            JsonObject exit = JsonSerializer.SerializeToNode(new
            {
                trial.ProcessId, StartedUtc = new DateTimeOffset(trial.ProcessStartUtcTicks, TimeSpan.Zero),
                trial.Parent, Observed = exited, ExitCode = 0, ForcedTermination = false, GracefulFailure = (string?)null,
                StandardOutputOverflow = false, StandardErrorOverflow = false,
            }, ProbeProtocol.JsonOptions)!.AsObject();
            await Save("exit.json", exit);
            MethodInfo validate = typeof(MeasurementReport).GetMethod("ValidateExitAsync", BindingFlags.Static | BindingFlags.NonPublic)!;
            Task Validate() => (Task)validate.Invoke(null,
                [root, DirectoryName, trial.ProcessId, trial.ProcessStartUtcTicks, trial.Parent, trial.Started, trial.Finished, CancellationToken.None])!;
            await Validate();
            foreach (Action<JsonObject> mutate in new Action<JsonObject>[]
            {
                value => value["ProcessId"] = trial.ProcessId + 1,
                value => value["StartedUtc"] = new DateTimeOffset(trial.ProcessStartUtcTicks + 1, TimeSpan.Zero),
                value => value["Parent"]!["ProcessStartUtcTicks"] = trial.Parent.ProcessStartUtcTicks + 1,
                value => value["Observed"]!["Before"] = 0,
                value => value["Observed"] = JsonSerializer.SerializeToNode(requested, ProbeProtocol.JsonOptions),
                value => value["Observed"]!.AsObject().Remove("Frequency"),
                value => value.Remove("Observed"),
                value => value["ForcedTermination"] = true,
            })
            {
                JsonObject wrong = (JsonObject)exit.DeepClone();
                mutate(wrong);
                await Save("exit.json", wrong);
                Exception? failure = await Record.ExceptionAsync(Validate);
                Assert.NotNull(failure);
                Assert.True(failure is InvalidDataException or JsonException or KeyNotFoundException);
            }
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>Both real arrays and the manifest must describe the exact new common protocol, not just a larger timeout.</summary>
    [Fact]
    public void Phase3V3RequiresRevisedManifestAndFixedCapacities()
    {
        ExperimentManifest manifest = Manifest();
        ExperimentRunner.ValidateManifest(manifest);
        Assert.Equal(3, manifest.SchemaVersion);
        Assert.Equal("candle-perf-phase3-v3", manifest.ProtocolVersion);
        Assert.Equal("candle-perf-measurement-v4", MeasurementReport.ContractVersion);
        Assert.Equal(7200, manifest.Bounds.BatchSeconds);
        Assert.Equal(1200, manifest.Bounds.SetupSeconds);
        Assert.Equal(10, manifest.Bounds.SampleMilliseconds);
        Assert.Equal(4, ProbeProtocol.SampleBoundarySlots);
        Assert.Equal(720004, ProbeProtocol.SampleCapacity);
        Assert.Equal(ProbeProtocol.SampleCapacity,
            checked((manifest.Bounds.BatchSeconds * 1000 / manifest.Bounds.SampleMilliseconds) + 4));
        Assert.Equal(4096, ProbeProtocol.MaximumRequestBytes);
        Assert.Equal(4096, ProbeProtocol.MaximumCommands);
        Assert.Equal(32, ProbeProtocol.MaximumIdentities);
        Assert.Equal(256 * 1024 * 1024, ProbeProtocol.MaximumResponseBytes);
        Assert.Equal(1024 * 1024 * 1024, MeasurementReport.MaximumEvidenceBytes);
        Assert.InRange(ProbeProtocol.SampleCapacity, 1, Array.MaxLength);
        Assert.Equal(48, Marshal.SizeOf<ManagedSample>());
        Assert.Equal(48, Marshal.SizeOf<ProcessMemorySample>());
        Assert.Equal(69120384, checked(ProbeProtocol.SampleCapacity *
            (Marshal.SizeOf<ManagedSample>() + Marshal.SizeOf<ProcessMemorySample>())));

        using ProbeWindowRecorder recorder = new();
        ManagedSample[] managed = (ManagedSample[])typeof(ProbeWindowRecorder)
            .GetField("_samples", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(recorder)!;
        Assert.Equal(ProbeProtocol.SampleCapacity, managed.Length);
        using Process process = Process.GetCurrentProcess();
        Type samplerType = typeof(ExperimentRunner).GetNestedType("ProcessSampler", BindingFlags.NonPublic)!;
        using IDisposable sampler = (IDisposable)Activator.CreateInstance(samplerType,
            BindingFlags.Instance | BindingFlags.NonPublic, null,
            [process, process.StartTime.ToUniversalTime().Ticks], CultureInfo.InvariantCulture)!;
        ProcessMemorySample[] external = (ProcessMemorySample[])samplerType
            .GetField("_samples", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(sampler)!;
        Assert.Equal(ProbeProtocol.SampleCapacity, external.Length);

        ExperimentManifest[] incompatible = [
            manifest with { ProtocolVersion = "candle-perf-phase3-v1" },
            manifest with { ProtocolVersion = "candle-perf-phase3-v2" },
            manifest with { SchemaVersion = 2 },
            manifest with { Bounds = manifest.Bounds with { SetupSeconds = 600, BatchSeconds = 60 } },
            manifest with { Bounds = manifest.Bounds with { BatchSeconds = 7199 } },
            manifest with { Bounds = manifest.Bounds with { BatchSeconds = 7201 } },
            manifest with { Bounds = manifest.Bounds with { SetupSeconds = 1199 } },
            manifest with { Bounds = manifest.Bounds with { SetupSeconds = 1201 } },
            manifest with { Bounds = manifest.Bounds with { RequestSeconds = 120 } },
            manifest with { Bounds = manifest.Bounds with { SampleMilliseconds = 11 } },
            manifest with { Bounds = manifest.Bounds with { SettlingMilliseconds = 0 } },
            manifest with { Bounds = manifest.Bounds with { ShutdownSeconds = 30 } },
            manifest with { Measurement = manifest.Measurement with { InitialMutationOperations = 128 } },
            manifest with { Measurement = manifest.Measurement with { InitialOtherOperations = 512 } },
            manifest with { Measurement = manifest.Measurement with { MaximumPilotOperations = 16384 } },
            manifest with { Measurement = manifest.Measurement with { MinimumSamples = 9 } },
        ];
        Assert.All(incompatible, wrong => Assert.Throws<InvalidDataException>(() => ExperimentRunner.ValidateManifest(wrong)));
        foreach (string field in new[] { "SchemaVersion", "ProtocolVersion", "Bounds" })
        {
            JsonObject missing = JsonSerializer.SerializeToNode(manifest, ProbeProtocol.JsonOptions)!.AsObject();
            Assert.True(missing.Remove(field));
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ExperimentManifest>(missing.ToJsonString(), ProbeProtocol.JsonOptions));
        }

        foreach (PropertyInfo property in typeof(ExperimentBounds).GetProperties())
        {
            JsonObject missing = JsonSerializer.SerializeToNode(manifest.Bounds, ProbeProtocol.JsonOptions)!.AsObject();
            Assert.True(missing.Remove(property.Name));
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ExperimentBounds>(missing.ToJsonString(), ProbeProtocol.JsonOptions));
        }

        (FrozenMeasurement frozen, TrialRecord[] trials) = MeasurementFixture();
        Assert.NotEmpty(MeasurementReport.ValidateMatrix(frozen with { ContractVersion = "candle-perf-measurement-v1" }, trials));
        Assert.NotEmpty(MeasurementReport.ValidateMatrix(frozen with { ContractVersion = "candle-perf-measurement-v2" }, trials));
        Assert.NotEmpty(MeasurementReport.ValidateMatrix(frozen with { ContractVersion = "candle-perf-measurement-v3" }, trials));
        Assert.NotEmpty(MeasurementReport.ValidateMatrix(frozen with { SchemaVersion = 1 }, trials));
        Assert.NotEmpty(MeasurementReport.ValidateMatrix(frozen with { Bounds = frozen.Bounds with { BatchSeconds = 60 } }, trials));
        Assert.Equal(128, Workloads.WarmupOperations);
        Assert.Equal(72, frozen.Inputs.Length);
        Assert.Equal(432, frozen.Schedule.Length);
    }

    /// <summary>Old, omitted or altered headroom settings cannot silently become the new fixed calibration policy.</summary>
    [Fact]
    public void PilotHeadroomSettingsRequireVersionedFixedPolicy()
    {
        ExperimentManifest manifest = Manifest();
        MeasurementSettings settings = manifest.Measurement;
        Assert.Equal(2, settings.SchemaVersion);
        Assert.Equal(20, settings.PilotMinimumSamples);
        Assert.Equal(10, settings.MinimumSamples);
        Assert.Equal(3, settings.TrialsPerVariant);
        Assert.Equal(256, settings.InitialMutationOperations);
        Assert.Equal(1024, settings.InitialOtherOperations);
        Assert.Equal(8192, settings.MaximumPilotOperations);
        Assert.Equal(1000, settings.ControlMilliseconds);
        settings.Validate();
        Assert.Equal(settings, JsonSerializer.Deserialize<MeasurementSettings>(
            JsonSerializer.Serialize(settings, ProbeProtocol.JsonOptions), ProbeProtocol.JsonOptions));
        (FrozenMeasurement frozen, TrialRecord[] trials) = MeasurementFixture();
        MeasurementSettings[] incompatible = [
            settings with { SchemaVersion = 0 },
            settings with { SchemaVersion = 1 },
            settings with { SchemaVersion = 3 },
            settings with { PilotMinimumSamples = 0 },
            settings with { PilotMinimumSamples = 9 },
            settings with { PilotMinimumSamples = 10 },
            settings with { PilotMinimumSamples = 19 },
            settings with { PilotMinimumSamples = 21 },
            settings with { MinimumSamples = 20 },
            settings with { ControlMilliseconds = 2000 },
        ];
        foreach (MeasurementSettings wrong in incompatible)
        {
            MeasurementSettings read = JsonSerializer.Deserialize<MeasurementSettings>(
                JsonSerializer.Serialize(wrong, ProbeProtocol.JsonOptions), ProbeProtocol.JsonOptions)!;
            Assert.Throws<InvalidDataException>(read.Validate);
            Assert.Throws<InvalidDataException>(() => ExperimentRunner.ValidateManifest(manifest with { Measurement = read }));
            Assert.NotEmpty(MeasurementReport.ValidateMatrix(frozen with { Settings = read }, trials));
        }

        foreach (string field in new[] { "SchemaVersion", "PilotMinimumSamples" })
        {
            JsonObject missing = JsonSerializer.SerializeToNode(settings, ProbeProtocol.JsonOptions)!.AsObject();
            Assert.True(missing.Remove(field));
            Assert.Throws<JsonException>(() => missing.Deserialize<MeasurementSettings>(ProbeProtocol.JsonOptions));
            JsonObject seed = JsonSerializer.SerializeToNode(manifest, ProbeProtocol.JsonOptions)!.AsObject();
            seed["Measurement"] = missing.DeepClone();
            Assert.Throws<JsonException>(() => seed.Deserialize<ExperimentManifest>(ProbeProtocol.JsonOptions));
            JsonObject handoff = JsonSerializer.SerializeToNode(frozen, ProbeProtocol.JsonOptions)!.AsObject();
            handoff["Settings"] = missing.DeepClone();
            Assert.Throws<JsonException>(() => handoff.Deserialize<FrozenMeasurement>(ProbeProtocol.JsonOptions));
        }

        JsonObject legacy = JsonSerializer.SerializeToNode(settings, ProbeProtocol.JsonOptions)!.AsObject();
        legacy["SchemaVersion"] = 1;
        Assert.True(legacy.Remove("PilotMinimumSamples"));
        Assert.Throws<JsonException>(() => legacy.Deserialize<MeasurementSettings>(ProbeProtocol.JsonOptions));
        JsonObject missingSettings = JsonSerializer.SerializeToNode(manifest, ProbeProtocol.JsonOptions)!.AsObject();
        Assert.True(missingSettings.Remove("Measurement"));
        Assert.Throws<JsonException>(() => missingSettings.Deserialize<ExperimentManifest>(ProbeProtocol.JsonOptions));
        Assert.Throws<InvalidDataException>(() => ExperimentRunner.ValidateManifest(manifest with { Measurement = null! }));
        Assert.NotEmpty(MeasurementReport.ValidateMatrix(frozen with { Settings = null! }, trials));
        JsonObject missingFrozenSettings = JsonSerializer.SerializeToNode(frozen, ProbeProtocol.JsonOptions)!.AsObject();
        Assert.True(missingFrozenSettings.Remove("Settings"));
        Assert.Throws<JsonException>(() => missingFrozenSettings.Deserialize<FrozenMeasurement>(ProbeProtocol.JsonOptions));
    }

    /// <summary>Exact finite activation budget includes end/export/cleanup and feeds the actual lifetime CTS without a twenty-minute ceiling.</summary>
    [Fact]
    public void ProbeDeadlineCoversFiniteEndExportAndShutdownEnvelope()
    {
        DateTimeOffset now = DateTimeOffset.Parse("2026-01-01T00:00:00+00:00", CultureInfo.InvariantCulture);
        Assert.Equal(8710, ProbeProtocol.ChildLifetimeSeconds);
        Assert.Equal(120 + 1200 + 7200 + 30 + 120 + 10 + (2 * 15), ProbeProtocol.ChildLifetimeSeconds);
        ClockObservation issued = new(now, 1000, 1000, TimeSpan.TicksPerSecond);
        long deadline = issued.Before + (issued.Frequency * ProbeProtocol.ChildLifetimeSeconds);
        ProbeConfiguration configuration = new("candle-perf-" + Guid.NewGuid().ToString("N"), 1, 1, "unit-fixture-owner",
            new string('A', 64), issued, deadline, ProbeProtocol.RequestSeconds);
        TimeSpan remaining = configuration.RemainingLifetime(issued.After, issued.Frequency);
        Assert.Equal(TimeSpan.FromSeconds(8710), remaining);
        using CancellationTokenSource lifetime = new(remaining);
        Assert.False(lifetime.IsCancellationRequested);
        Assert.Equal(remaining - TimeSpan.FromSeconds(1), configuration.RemainingLifetime(issued.After + issued.Frequency, issued.Frequency));
        foreach (long invalid in new[] { deadline + 1, long.MaxValue, issued.After, issued.After - 1 })
        {
            Assert.Throws<InvalidDataException>(() => (configuration with { DeadlineTimestamp = invalid })
                .RemainingLifetime(issued.After, issued.Frequency));
        }

        Assert.Throws<InvalidDataException>(() => configuration.RemainingLifetime(deadline, issued.Frequency));
        Assert.Throws<InvalidDataException>(() => configuration.RemainingLifetime(deadline + 1, issued.Frequency));
        Assert.Throws<InvalidDataException>(() => configuration.RemainingLifetime(issued.After - 1, issued.Frequency));
        Assert.Throws<InvalidDataException>(() => configuration.RemainingLifetime(issued.After, 0));
        Assert.Throws<InvalidDataException>(() => (configuration with
        {
            Issued = issued with { Before = long.MaxValue - 1, After = long.MaxValue - 1 },
        }).RemainingLifetime(long.MaxValue - 1, issued.Frequency));
        foreach (DateTimeOffset adjusted in new[] { now.AddDays(-1), now.AddDays(1) })
        {
            Assert.Equal(remaining, (configuration with { Issued = issued with { Utc = adjusted } })
                .RemainingLifetime(issued.After, issued.Frequency));
        }
        foreach (string field in new[] { "Issued", "DeadlineTimestamp" })
        {
            JsonObject missing = JsonSerializer.SerializeToNode(configuration, ProbeProtocol.JsonOptions)!.AsObject();
            Assert.True(missing.Remove(field));
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ProbeConfiguration>(missing.ToJsonString(), ProbeProtocol.JsonOptions));
        }
        foreach (int incompatible in new[] { 0, 5, 29, 31, 60, 120, int.MaxValue })
        {
            Assert.Throws<InvalidDataException>(() => (configuration with { RequestTimeoutSeconds = incompatible })            .RemainingLifetime(issued.After, issued.Frequency));
        }

        // Regression: a configuration is limited in UTF-8 bytes, not UTF-16 characters.
        string json = JsonSerializer.Serialize(configuration,
            ProbeProtocol.JsonOptions).Replace("unit-fixture-owner", new string('\u00E9', 2000), StringComparison.Ordinal);
        Assert.True(json.Length < ProbeProtocol.MaximumRequestBytes);
        Assert.True(Encoding.UTF8.GetByteCount(json) > ProbeProtocol.MaximumRequestBytes);
        Assert.Throws<InvalidDataException>(() => ProbeConfiguration.FromEnvironment(_ => json));
    }

    /// <summary>End-ack cancellation cannot consume the separately created stopped export budget; lifetime cancellation still fails closed.</summary>
    [Fact]
    public async Task StoppedExportUsesIndependentBudgetWhileOrdinaryRemainsThirtySeconds()
    {
        Assert.Equal(120, ProbeProtocol.ExchangeSeconds("export-window"));
        foreach (string operation in new[] { "hello", "snapshot", "identities", "begin-window", "end-window",
            "idle-checkpoints", "loaded-checkpoints", "shutdown" })
        {
            Assert.Equal(30, ProbeProtocol.ExchangeSeconds(operation));
        }

        Assert.Equal(30, new ExperimentBounds().RequestSeconds); // Ordinary HTTP shares this unchanged bound.
        using CancellationTokenSource lifetime = new();
        using CancellationTokenSource ending = ProbeProtocol.CreateExchangeDeadline("end-window", lifetime.Token);
        using CancellationTokenSource ordinary = ProbeProtocol.CreateExchangeDeadline("snapshot", lifetime.Token);
        ending.Cancel();
        using CancellationTokenSource export = ProbeProtocol.CreateExchangeDeadline("export-window", lifetime.Token);
        Assert.False(export.IsCancellationRequested);
        Assert.False(ordinary.IsCancellationRequested);
        using MemoryStream stream = new();
        ProbeReply reply = new(2, null, false, WindowFixture("unit-fixture-export", 0, 1, 1, 1000, false).Server);
        await ProbeProtocol.WriteAsync(stream, reply, ProbeProtocol.MaximumResponseBytes, export.Token);
        stream.Position = 0;
        ProbeReply decoded = await ProbeProtocol.ReadAsync<ProbeReply>(stream, ProbeProtocol.MaximumResponseBytes, export.Token);
        Assert.Equal(reply.Window!.End, decoded.Window!.End);
        lifetime.Cancel();
        Assert.True(export.IsCancellationRequested);
        Assert.True(ordinary.IsCancellationRequested);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ProbeProtocol.WriteAsync(stream, reply, ProbeProtocol.MaximumResponseBytes, export.Token));

        using CancellationTokenSource expiredDuringRead = new();
        using CancelAfterReadStream interrupted = new(stream.ToArray(), expiredDuringRead);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ProbeProtocol.ReadAsync<ProbeReply>(interrupted, ProbeProtocol.MaximumResponseBytes, expiredDuringRead.Token));
    }

    /// <summary>Analytical compact-JSON proof covers worst-width numbers, every sample in all three windows, and full trial metadata.</summary>
    [Fact]
    public void MaximumSampleReplyAndTrialSizesFitWithoutTruncation()
    {
        // Negative extrema are wider than valid nonnegative metrics: this is size-only synthetic data, never live evidence.
        ManagedSample managed = new(long.MinValue, long.MinValue, long.MinValue, long.MinValue, long.MinValue, long.MinValue);
        ProcessMemorySample process = new(int.MinValue, long.MinValue, long.MinValue, long.MinValue, long.MinValue, long.MinValue);
        CounterReading counters = new(long.MinValue, long.MinValue, int.MinValue, int.MinValue, int.MinValue, long.MinValue, long.MinValue);
        Footprint point = new("post-immediate", false, int.MinValue, long.MinValue, long.MinValue,
            long.MinValue, long.MinValue, managed, counters);
        WindowEvidence server = new("GC-process-start", int.MinValue, long.MinValue, long.MinValue, true,
            ProbeProtocol.SampleCapacity, ProbeProtocol.SampleMilliseconds, counters, counters, [], int.MinValue,
            int.MinValue, false, [point, point with { Name = "post-settled" }, point with { Name = "post-forced", ForcedGc = true }]);
        OperationTiming operation = new(int.MinValue, long.MinValue, long.MinValue, int.MinValue, false);
        BatchWindow window = new("work", long.MinValue, long.MinValue, long.MinValue, long.MinValue, long.MinValue,
            long.MinValue, long.MinValue, int.MinValue, int.MinValue, int.MinValue,
            Enumerable.Repeat(operation, new MeasurementSettings().MaximumPilotOperations).ToArray(), server, [], int.MinValue, int.MinValue, null);
        ClockObservation widestClock = new(DateTimeOffset.MaxValue, long.MinValue, long.MinValue, long.MinValue);
        Assert.Equal(135, JsonSerializer.SerializeToUtf8Bytes(widestClock, ProbeProtocol.JsonOptions).Length);
        TrialRecord fixture = MeasurementTrialFixture(MeasurementFixtureSchedule()[0]) with
        {
            SchemaVersion = int.MinValue, Status = "size-only-fixture", ProcessId = int.MinValue,
            Started = widestClock, Finished = widestClock, Parent = new(int.MinValue, long.MinValue),
            ProcessStartUtcTicks = long.MinValue, ValidatedSeedReads = int.MinValue, ValidatedSeedWrites = int.MinValue,
            Warmup = new(int.MinValue, int.MinValue, int.MinValue, int.MinValue, int.MinValue, int.MinValue,
                new("Observation", new string('i', 64), new string('9', 20), DateTimeOffset.MaxValue),
                new("Observation", new string('i', 64), new string('9', 20), DateTimeOffset.MaxValue),
                new("Observation", new string('i', 64), new string('9', 20), DateTimeOffset.MaxValue)),
            Batch = window, Checkpoints = Enumerable.Repeat(point, 7).ToArray(),
            Controls = [window with { Name = "idle-control", Operations = [] }, window with { Name = "loaded-control", Operations = [] }],
            RawFiles = [],
        };
        long Growth<T>(T value, int count) => count == 0 ? 0 :
            checked(((long)JsonSerializer.SerializeToUtf8Bytes(value, ProbeProtocol.JsonOptions).Length + 1) * count - 1);
        Assert.Equal(216, JsonSerializer.SerializeToUtf8Bytes(managed, ProbeProtocol.JsonOptions).Length);
        Assert.Equal(209, JsonSerializer.SerializeToUtf8Bytes(process, ProbeProtocol.JsonOptions).Length);
        long managedArray = checked(2 + Growth(managed, ProbeProtocol.SampleCapacity));
        long processArray = checked(2 + Growth(process, ProbeProtocol.SampleCapacity));
        Assert.Equal(156240869, managedArray);
        Assert.Equal(151200841, processArray);

        ProbeReply emptyReply = new(int.MinValue, null, false, server);
        int replyEnvelope = JsonSerializer.SerializeToUtf8Bytes(emptyReply, ProbeProtocol.JsonOptions).Length;
        Assert.True(replyEnvelope < 1024 * 1024);
        long replyBound = checked(managedArray + (1024 * 1024));
        Assert.Equal(157289445, replyBound);
        Assert.True(replyBound < ProbeProtocol.MaximumResponseBytes);
        byte[] envelope = MeasurementReport.SerializeEvidence(fixture);
        Assert.DoesNotContain((byte)'\n', envelope);
        // Even the inventory's 50,000-entry ceiling with 1-KiB synthetic relative names is below the metadata reserve.
        EvidenceReference reference = new(new string('r', 1024), long.MinValue, new string('F', 64));
        long trialEnvelope = checked(envelope.LongLength + Growth(reference, 50000));
        Assert.True(trialEnvelope < 128L * 1024 * 1024);
        long trialBound = checked((3 * (managedArray + processArray)) + (128L * 1024 * 1024));
        Assert.Equal(1056542858, trialBound);
        Assert.True(trialBound < MeasurementReport.MaximumEvidenceBytes);
        Assert.True(trialBound < Array.MaxLength);

        // Check the additive proof against actual production serialization at two small cardinalities, without allocating 1 GiB.
        foreach (int count in new[] { 1, 2 })
        {
            BatchWindow Expand(BatchWindow source) => source with
            {
                Server = source.Server with { Samples = Enumerable.Repeat(managed, count).ToArray() },
                ProcessSamples = Enumerable.Repeat(process, count).ToArray(),
            };
            TrialRecord expanded = fixture with
            {
                Batch = Expand(fixture.Batch!),
                Controls = fixture.Controls.Select(Expand).ToArray(),
                RawFiles = Enumerable.Repeat(reference, count).ToArray(),
            };
            byte[] bytes = MeasurementReport.SerializeEvidence(expanded);
            Assert.Equal(envelope.LongLength + (3 * (Growth(managed, count) + Growth(process, count))) + Growth(reference, count),
                bytes.LongLength);
            TrialRecord decoded = JsonSerializer.Deserialize<TrialRecord>(bytes, ProbeProtocol.JsonOptions)!;
            Assert.Equal("unit-fixture", decoded.Origin);
            Assert.Equal(count, decoded.Batch!.Server.Samples.Length);
            Assert.All(decoded.Controls, control => Assert.Equal(count, control.ProcessSamples.Length));
            ProbeReply reply = emptyReply with { Window = server with { Samples = Enumerable.Repeat(managed, count).ToArray() } };
            Assert.Equal(replyEnvelope + Growth(managed, count),
                JsonSerializer.SerializeToUtf8Bytes(reply, ProbeProtocol.JsonOptions).LongLength);
        }

        // Full receipt growth is separate from window export, but also must fit the unchanged snapshot shape.
        ReceiptSnapshot receipt = Receipt(Workloads.CreateProbeChallenge("R5")) with
        {
            EventsSinceSubscriptionStart = long.MinValue,
            NotificationEvents = [new(new string('9', 20), long.MinValue, DateTimeOffset.MaxValue,
                "Observation/" + new string('i', 64), ["Patient/" + new string('i', 64)])],
        };
        long receiptArray = checked(2 + Growth(receipt, Workloads.WarmupOperations + new MeasurementSettings().MaximumPilotOperations));
        Assert.True(receiptArray + (16L * 1024 * 1024) < ProbeProtocol.MaximumResponseBytes);
    }

    /// <summary>Oversized prefixes/lengths fail before payload allocation; saved-file growth and truncation never become unbounded reads.</summary>
    [Fact]
    public async Task BoundedReadersRejectOversizedFramesAndChangingEvidenceBeforeAllocation()
    {
        Assert.Equal(MeasurementReport.MaximumEvidenceBytes,
            MeasurementReport.ValidateEvidenceLength(MeasurementReport.MaximumEvidenceBytes));
        Assert.Equal((512 * 1024 * 1024) + 1, MeasurementReport.ValidateEvidenceLength((512L * 1024 * 1024) + 1));
        Assert.Throws<InvalidDataException>(() => MeasurementReport.ValidateEvidenceLength((long)MeasurementReport.MaximumEvidenceBytes + 1));
        Assert.Throws<InvalidDataException>(() => MeasurementReport.ValidateEvidenceLength(-1));
        Assert.Throws<InvalidDataException>(() => MeasurementReport.ValidateEvidenceLength(long.MaxValue));
        Assert.Throws<InvalidDataException>(() => MeasurementReport.VerifyReference(Path.GetTempPath(),
            new("unit-fixture-not-written.json", (long)MeasurementReport.MaximumEvidenceBytes + 1, new string('A', 64))));
        foreach (int maximum in new[] { ProbeProtocol.MaximumRequestBytes, ProbeProtocol.MaximumResponseBytes })
        {
            foreach (int length in new[] { 0, -1, maximum + 1, int.MaxValue })
            {
                byte[] header = new byte[sizeof(int)];
                BinaryPrimitives.WriteInt32LittleEndian(header, length);
                using MemoryStream frame = new(header);
                long before = GC.GetAllocatedBytesForCurrentThread();
                await Assert.ThrowsAsync<InvalidDataException>(() => ProbeProtocol.ReadAsync<ProbeReply>(frame, maximum, CancellationToken.None));
                Assert.InRange(GC.GetAllocatedBytesForCurrentThread() - before, 0, 1024 * 1024);
                Assert.Equal(sizeof(int), frame.Position);
            }
        }

        using MemoryStream invalidBound = new();
        await Assert.ThrowsAsync<InvalidDataException>(() => ProbeProtocol.ReadAsync<ProbeReply>(
            invalidBound, ProbeProtocol.MaximumResponseBytes + 1, CancellationToken.None));
        Assert.Equal(0, invalidBound.Position);
        foreach (int maximum in new[] { 1024 * 1024, 32 * 1024 * 1024, MeasurementReport.MaximumEvidenceBytes })
        {
            using DeclaredLengthStream oversized = new([], (long)maximum + 1);
            await Assert.ThrowsAsync<InvalidDataException>(() => MeasurementReport.ReadBoundedBytesAsync(
                oversized, maximum, CancellationToken.None));
            Assert.False(oversized.ReadAttempted);
        }

        using DeclaredLengthStream invalidReaderBound = new([], 0);
        await Assert.ThrowsAsync<InvalidDataException>(() => MeasurementReport.ReadBoundedBytesAsync(
            invalidReaderBound, MeasurementReport.MaximumEvidenceBytes + 1, CancellationToken.None));
        Assert.False(invalidReaderBound.ReadAttempted);
        using DeclaredLengthStream grown = new([1, 2], 1);
        await Assert.ThrowsAsync<InvalidDataException>(() => MeasurementReport.ReadBoundedBytesAsync(grown, 1, CancellationToken.None));
        Assert.Equal(2, grown.Position);
        using DeclaredLengthStream truncated = new([1], 2);
        await Assert.ThrowsAsync<EndOfStreamException>(() => MeasurementReport.ReadBoundedBytesAsync(truncated, 2, CancellationToken.None));
        using MemoryStream valid = new([1, 2, 3]);
        Assert.Equal(new byte[] { 1, 2, 3 }, await MeasurementReport.ReadBoundedBytesAsync(
            valid, MeasurementReport.MaximumEvidenceBytes, CancellationToken.None));
    }

    /// <summary>The increased window still rejects an extra tick, overflowing clocks and negative origins; no accounting oracle is relaxed.</summary>
    [Fact]
    public void WindowDurationRejectsOverflowAndPastSupportedBoundary()
    {
        BatchWindow window = WindowFixture("unit-fixture-boundary", 256, 1, 1, 1000, true);
        long last = checked(window.Server.Begin.Timestamp + (window.Frequency * ProbeProtocol.WindowSeconds));
        BatchWindow boundary = window with
        {
            ClientEnd = last - 2, EndSent = last - 1, EndAcknowledged = last + 1,
            Server = window.Server with { End = window.Server.End with { Timestamp = last } },
        };
        Assert.Empty(MeasurementReport.ValidateWindow(boundary, 1, 1, true));
        Assert.Contains("counter duration/deadline", MeasurementReport.ValidateWindow(boundary with
        {
            Server = boundary.Server with { End = boundary.Server.End with { Timestamp = last + 1 } },
            EndAcknowledged = last + 2,
        }, 1, 1, true));
        Assert.Contains("counter duration/deadline", MeasurementReport.ValidateWindow(boundary with
        {
            Frequency = long.MaxValue, Server = boundary.Server with { Frequency = long.MaxValue },
        }, 1, 1, true));
        Assert.False(ProbeProtocol.IsWithinWindow(long.MinValue, long.MaxValue, 1000));
        Assert.False(ProbeProtocol.IsWithinWindow(0, 1, 0));
        Assert.False(ProbeProtocol.IsWithinWindow(0, 1, -1));
        Assert.False(ProbeProtocol.IsWithinWindow(1, 1, 1000));
        Assert.Contains(MeasurementReport.ValidateWindow(window with { Server = window.Server with { Samples = window.Server.Samples[..9] } },
            1, 1, true), error => error.StartsWith("coverage:", StringComparison.Ordinal));
        Assert.Contains(MeasurementReport.ValidateWindow(window with { ProcessSamples = window.ProcessSamples[..9] },
            1, 1, true), error => error.StartsWith("coverage:", StringComparison.Ordinal));
    }

    /// <summary>The actual schedule helper fixes N and creates separate alternating dispatches for every repetition/block.</summary>
    [Fact]
    public void TrialScheduleUsesFreshProcessesAndAlternatesPairs()
    {
        (FrozenMeasurement frozen, TrialRecord[] trials) = MeasurementFixture();
        Assert.Equal(432, frozen.Schedule.Length);
        Assert.Equal(432, frozen.Schedule.Select(plan => plan.AttemptId).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(432, frozen.Schedule.Select(plan => plan.RelativeDirectory).Distinct(StringComparer.Ordinal).Count());
        Assert.Empty(MeasurementReport.ValidateMatrix(frozen, trials));
        WorkloadCase[] cases = Workloads.CreateCases();
        for (int repetition = 1; repetition <= 3; repetition++)
        {
            for (int block = 0; block < cases.Length; block++)
            {
                TrialPlan[] pair = frozen.Schedule.Where(plan => plan.Repetition == repetition && plan.RowKey == cases[block].RowKey).ToArray();
                Assert.Equal(2, pair.Length);
                Assert.Equal((block + repetition - 1) % 2 == 0 ? "baseline" : "candidate", pair[0].Variant);
                Assert.NotEqual(pair[0].Variant, pair[1].Variant);
                Assert.Equal(pair[0].Operations, pair[1].Operations);
                Assert.All(pair, plan => Assert.Equal(new MeasurementSettings().InitialCount(cases[block]), plan.Operations));
            }
        }

        TrialRecord[] reused = trials.ToArray();
        reused[1] = reused[1] with { ProcessId = reused[0].ProcessId, ProcessStartUtcTicks = reused[0].ProcessStartUtcTicks };
        Assert.Contains(MeasurementReport.ValidateMatrix(frozen, reused), error => error.Contains("reused process", StringComparison.Ordinal));
        TrialRecord[] overlap = trials.ToArray();
        overlap[1] = overlap[1] with { Started = overlap[0].Started };
        Assert.Contains(MeasurementReport.ValidateMatrix(frozen, overlap), error => error.Contains("overlapping", StringComparison.Ordinal));
        Assert.Throws<InvalidDataException>(() => MeasurementReport.CreateSchedule(new Dictionary<string, int>(), 3));
        Assert.Throws<InvalidDataException>(() => MeasurementReport.CreateSchedule(
            frozen.Inputs.ToDictionary(input => input.Case.RowKey, input => input.Operations), 2));
    }

    /// <summary>Exercises generated 128/N inputs and the exact shared clinical/metadata/receipt oracles used by trials.</summary>
    [Fact]
    public void WarmupRestoresClinicalStateAndRetainsHookReceipts()
    {
        const string BaseUrl = "http://127.0.0.1:58260/fhir/perf";
        foreach (string version in new[] { "R4", "R4B", "R5" })
        {
            WorkloadCorpus corpus = Workloads.CreateCorpus(version, 100);
            DateTimeOffset now = DateTimeOffset.UtcNow;
            foreach (string caseId in new[] { "patient-update", "observation-update" })
            {
                WorkloadCase workload = Workloads.CreateCases().Single(item =>
                    item.FhirVersion == version && item.PatientCount == 100 && item.CaseId == caseId);
                TrialInputs inputs = Workloads.CreateTrialInputs(workload, corpus, 1024);
                CorrectnessOracle.ValidateCaseRequests(workload, corpus, inputs.Warmup, 128);
                CorrectnessOracle.ValidateCaseRequests(workload, corpus, inputs.Measured, 1024, 128);
                WorkloadRequest restore = Assert.IsType<WorkloadRequest>(inputs.Restore);
                CorpusResource original = corpus.Resources.Single(resource => resource.Identity == restore.RelativeUrl);
                ResourceBinding originalBinding = new(original.ResourceType, original.Id, "1", now);
                Dictionary<string, ExpectedResource> state = new() { [original.Identity] = new(original.Body, originalBinding) };
                CompletedOperation[] returned = inputs.Warmup.Select(request => new CompletedOperation(request.Number,
                    [new(BaseUrl + "/" + request.RelativeUrl, ResourceResponse(request.Body!, original.Id, 200,
                        now.AddMilliseconds(request.Number), (request.Number + 1).ToString(CultureInfo.InvariantCulture)))])).ToArray();
                BatchValidation warm = CorrectnessOracle.ValidateBatchResponses(workload, inputs.Warmup, returned,
                    state, CorrectnessOracle.StatusProfile("candidate"), BaseUrl);
                Assert.Equal(128, warm.Operations.Length);
                ExpectedResource warmed = warm.Resources[original.Identity];
                Assert.Equal("129", warmed.Binding.VersionId);
                bool issueRestore = !CorrectnessOracle.SameClinicalContent(warmed.Content, restore.Body!);
                Assert.Equal(caseId == "observation-update", issueRestore);
                ExpectedResource restored = warmed;
                ResponseEvidence? restoration = null;
                if (issueRestore)
                {
                    restoration = ResourceResponse(restore.Body!, original.Id, 200, now.AddMilliseconds(129), "130");
                    ResourceBinding binding = CorrectnessOracle.ValidateUpdateResponse(
                        restoration,
                        200, warmed.Content, restore.Body!, warmed.Binding, BaseUrl);
                    restored = new(restore.Body!, binding);
                }

                CorrectnessOracle.ValidateRestoration(workload, corpus, restore, warmed, restored, restoration);
                Assert.True(CorrectnessOracle.SameClinicalContent(original.Body, restored.Content));
                Assert.Equal(issueRestore ? "130" : "129", restored.Binding.VersionId);
                Assert.Throws<InvalidDataException>(() => CorrectnessOracle.ValidateRestoration(workload, corpus, restore, warmed,
                    restored with { Binding = restored.Binding with { VersionId = "1" } }, restoration));
                CompletedOperation[] hiddenBadUpdate = returned.ToArray();
                hiddenBadUpdate[63] = hiddenBadUpdate[63] with
                {
                    Exchanges = [hiddenBadUpdate[63].Exchanges[0] with
                    {
                        Response = ResourceResponse(inputs.Warmup[62].Body!, original.Id, 200, now.AddMilliseconds(64), "65") with
                        {
                            Started = returned[63].Exchanges[0].Response.Started,
                            Completed = returned[63].Exchanges[0].Response.Completed,
                        },
                    }],
                };
                Assert.Throws<InvalidDataException>(() => CorrectnessOracle.ValidateBatchResponses(workload, inputs.Warmup,
                    hiddenBadUpdate, state, CorrectnessOracle.StatusProfile("candidate"), BaseUrl));
            }

            foreach (string caseId in new[] { "patient-create", "observation-create" })
            {
                WorkloadCase workload = Workloads.CreateCases().Single(item =>
                    item.FhirVersion == version && item.PatientCount == 100 && item.CaseId == caseId);
                TrialInputs inputs = Workloads.CreateTrialInputs(workload, corpus, 256);
                Assert.Equal(Enumerable.Range(1, 128), inputs.Warmup.Select(request => request.Number));
                Assert.Equal(Enumerable.Range(129, 256), inputs.Measured.Select(request => request.Number));
                Assert.Null(inputs.Restore);
                Dictionary<string, ExpectedResource> state = corpus.Resources.ToDictionary(resource => resource.Identity,
                    resource => new ExpectedResource(resource.Body, new(resource.ResourceType, resource.Id, "1", now)));
                CompletedOperation[] responses = inputs.Warmup.Select(request => new CompletedOperation(request.Number,
                    [new(BaseUrl + "/" + request.RelativeUrl, ResourceResponse(request.Body!,
                        "warm-" + request.Number.ToString("D8", CultureInfo.InvariantCulture), 201, now))])).ToArray();
                BatchValidation warm = CorrectnessOracle.ValidateBatchResponses(workload, inputs.Warmup, responses,
                    state, CorrectnessOracle.StatusProfile("candidate"), BaseUrl);
                string type = warm.Created[0].ResourceType;
                string[] initial = corpus.Resources.Where(resource => resource.ResourceType == type).Select(resource => resource.Id).ToArray();
                CorrectnessOracle.ValidateCreateGrowth(type, initial, initial.Length, initial.Length + 128, 128, warm.Created);
                Assert.Throws<InvalidDataException>(() => CorrectnessOracle.ValidateCreateGrowth(type, initial,
                    initial.Length, initial.Length + 128, 128, warm.Created, warm.Created.Select(binding => binding.Id).ToArray()));
                foreach (ResourceBinding created in warm.Created)
                {
                    ExpectedResource resource = warm.Resources[created.ResourceType + "/" + created.Id];
                    ResponseEvidence deleted = ResourceResponse(resource.Content, created.Id, 200, now);
                    deleted.Headers.Remove("Location");
                    deleted.Headers.Remove("ETag");
                    deleted.Headers.Remove("Last-Modified");
                    CorrectnessOracle.ValidateDeleteResponse(deleted, 200, resource);
                }

                Dictionary<string, bool> absent = warm.Created.ToDictionary(binding => binding.ResourceType + "/" + binding.Id, _ => false);
                TenantSnapshot restored = ClinicalSnapshot(100, 1000, identities: absent.Keys.ToDictionary(key => key, _ => (ResourceIdentity?)null));
                CorrectnessOracle.ValidateState(ClinicalSnapshot(0, 0), restored, 100, 1000, 0, absent, _sourceDirectory);
                Assert.Throws<InvalidDataException>(() => CorrectnessOracle.ValidateState(ClinicalSnapshot(0, 0), restored with
                {
                    ResourceCounts = new SortedDictionary<string, int>(restored.ResourceCounts) { [type] = initial.Length + 1 },
                }, 100, 1000, 0, absent, _sourceDirectory));
            }

            WorkloadCase hook = Workloads.CreateCases().Single(item =>
                item.FhirVersion == version && item.PatientCount == 100 && item.CaseId == "subscription-receive");
            TrialInputs hookInputs = Workloads.CreateTrialInputs(hook, corpus, 256);
            ReceiptExpectation[] warmReceipts = hookInputs.Warmup.Select(request =>
                FixtureReceiptExpectation(request.Notification!, now.AddMinutes(-1), now.AddMinutes(1))).ToArray();
            ReceiptExpectation[] allReceipts = [.. warmReceipts, .. hookInputs.Measured.Select(request =>
                FixtureReceiptExpectation(request.Notification!, now.AddMinutes(-1), now.AddMinutes(1)))];
            ReceiptSnapshot[] actual = allReceipts.Select(receipt =>
                Receipt(Workloads.CreateProbeChallenge(version, receipt.Notification.EventNumber))).ToArray();
            CorrectnessOracle.ValidateReceipt(ClinicalSnapshot(100, 1000, 128, actual[..128]), warmReceipts);
            CorrectnessOracle.ValidateReceipt(ClinicalSnapshot(100, 1000, 384, actual), allReceipts);
            Assert.Throws<InvalidDataException>(() => CorrectnessOracle.ValidateReceipt(
                ClinicalSnapshot(100, 1000, 256, actual[128..]), allReceipts));
            Assert.Throws<InvalidDataException>(() => CorrectnessOracle.ValidateReceipt(
                ClinicalSnapshot(100, 1000, 384, actual.AsEnumerable().Reverse().ToArray()), allReceipts));
            Assert.All(allReceipts, receipt => CorrectnessOracle.ValidateNotification(receipt.Notification));
        }
    }

    /// <summary>The real recorder forbids diagnostics/GC while armed and snapshots before exporting diagnostic objects.</summary>
    [Fact]
    public void CounterWindowExcludesSetupAuditAndForcedGc()
    {
        using ProbeWindowRecorder recorder = new();
        byte[] setup = new byte[8192];
        Footprint idle = recorder.CaptureCheckpoint("fixture-idle", false);
        byte[] boundaryBuffer = new byte[ProbeProtocol.BoundaryBytes];
        CounterReading begin = recorder.BeginWindow(false);
        Assert.Throws<InvalidOperationException>(() => recorder.CaptureCheckpoint("forbidden", true));
        Assert.Throws<InvalidOperationException>(() => recorder.Export());
        byte[] work = new byte[4096];
        Thread.Sleep(1);
        CounterReading end = recorder.EndWindow();
        Assert.True(end.Timestamp > begin.Timestamp);
        Assert.True(end.AllocatedBytes >= begin.AllocatedBytes + work.Length);
        Assert.True(idle.Timestamp < begin.Timestamp);
        ProbeProtocol.EncodeBoundary(boundaryBuffer, 17, recorder.ProcessId, recorder.ProcessStartUtcTicks, end);
        Assert.Equal(end, ProbeProtocol.DecodeBoundary(boundaryBuffer, 17, recorder.ProcessId, recorder.ProcessStartUtcTicks));
        Assert.Throws<InvalidDataException>(() => ProbeProtocol.DecodeBoundary(boundaryBuffer, 18, recorder.ProcessId, recorder.ProcessStartUtcTicks));
        Assert.Throws<InvalidDataException>(() => ProbeProtocol.DecodeBoundary(boundaryBuffer, 17, recorder.ProcessId + 1, recorder.ProcessStartUtcTicks));
        WindowEvidence beforeAudit = recorder.Export();
        byte[] audit = new byte[16384];
        Footprint forced = recorder.CaptureCheckpoint("fixture-forced", true);
        WindowEvidence afterAudit = recorder.Export();
        Assert.Equal(beforeAudit.Begin, afterAudit.Begin);
        Assert.Equal(beforeAudit.End, afterAudit.End);
        Assert.True(forced.Counters.Gen2 >= end.Gen2 + 2);
        Assert.True(forced.Timestamp > end.Timestamp);
        Assert.Empty(afterAudit.Samples);
        Assert.Throws<InvalidOperationException>(() => recorder.Sample());
        CounterReading sampledBegin = recorder.BeginWindow(true);
        Thread.Sleep(150);
        CounterReading sampledEnd = recorder.EndWindow();
        WindowEvidence sampled = recorder.Export();
        Assert.NotEmpty(sampled.Samples);
        Assert.All(sampled.Samples, sample => Assert.InRange(sample.Timestamp, sampledBegin.Timestamp, sampledEnd.Timestamp));
        Assert.Equal(0, sampled.SamplingErrors);
        Assert.Equal(0, sampled.DroppedSamples);
        GC.KeepAlive(setup);
        GC.KeepAlive(work);
        GC.KeepAlive(audit);
        TrialRecord fixture = MeasurementTrialFixture(MeasurementFixtureSchedule()[0]);
        Assert.Contains(MeasurementReport.ValidateTrial(fixture with
        {
            Batch = fixture.Batch! with { Server = fixture.Batch.Server with { CounterOrigin = "thread-local" } },
        }), error => error.Contains("counter origin", StringComparison.Ordinal));
        Assert.Contains(MeasurementReport.ValidateTrial(fixture with
        {
            Batch = fixture.Batch! with { Server = fixture.Batch.Server with
            {
                End = fixture.Batch.Server.End with { Timestamp = fixture.Batch.ClientEnd - 1 },
            } },
        }), error => error.Contains("bracketing", StringComparison.Ordinal));
        Assert.Contains(MeasurementReport.ValidateTrial(fixture with
        {
            Batch = fixture.Batch! with { Server = fixture.Batch.Server with
            {
                Begin = fixture.Batch.Server.Begin with { AllocatedBytes = 0 },
            } },
        }), error => error.Contains("counter timeline", StringComparison.Ordinal));
    }

    /// <summary>A large before/after sample cannot turn into a workload peak, and another process is never accepted.</summary>
    [Fact]
    public void PeakUsesOnlyServerPidSamplesInsideWindow()
    {
        TrialRecord trial = MeasurementTrialFixture(MeasurementFixtureSchedule()[0]);
        BatchWindow batch = trial.Batch!;
        (long working, long privateBytes) = MeasurementReport.CalculatePeak(batch, trial.ProcessId, trial.ProcessStartUtcTicks);
        ProcessMemorySample outside = batch.ProcessSamples[0] with
        {
            Timestamp = batch.Server.Begin.Timestamp, WorkingSetBytes = long.MaxValue, PrivateBytes = long.MaxValue,
        };
        Assert.Equal((working, privateBytes), MeasurementReport.CalculatePeak(batch with
        {
            ProcessSamples = [outside, .. batch.ProcessSamples],
        }, trial.ProcessId, trial.ProcessStartUtcTicks));
        foreach (ProcessMemorySample wrong in new[]
        {
            outside with { ProcessId = trial.ProcessId + 1 },
            outside with { ProcessStartUtcTicks = trial.ProcessStartUtcTicks + 1 },
            outside with { Timestamp = batch.Server.Begin.Timestamp - 1 },
            outside with { Timestamp = batch.Server.End.Timestamp + 1 },
            outside with { WorkingSetBytes = -1 },
        })
        {
            Assert.Throws<InvalidDataException>(() => MeasurementReport.CalculatePeak(batch with
            {
                ProcessSamples = [wrong, .. batch.ProcessSamples],
            }, trial.ProcessId, trial.ProcessStartUtcTicks));
        }

        Assert.Throws<InvalidDataException>(() => MeasurementReport.CalculatePeak(batch with
        {
            ProcessSamples = [outside],
        }, trial.ProcessId, trial.ProcessStartUtcTicks));
        Assert.Contains(MeasurementReport.ValidateTrial(trial with
        {
            Batch = batch with { ProcessSamples = batch.ProcessSamples[..9] },
        }), error => error.StartsWith("coverage:", StringComparison.Ordinal));
    }

    /// <summary>Each work stream independently needs twenty pilot samples but only ten accepted samples; boundaries cannot pad it.</summary>
    [Fact]
    public void PilotHeadroomRequiresEachActualWorkStreamAtBoundary()
    {
        TrialRecord measured = MeasurementTrialFixture(MeasurementFixtureSchedule()[0]);
        foreach (string purpose in new[] { "pilot", "measurement" })
        {
            foreach (bool varyManaged in new[] { true, false })
            {
                foreach (int count in new[] { 9, 10, 19, 20, 21 })
                {
                    BatchWindow work = WindowWithSamples(measured.Batch!, varyManaged ? count : 21, varyManaged ? 21 : count);
                    work = work with
                    {
                        Server = work.Server with
                        {
                            Samples = [work.Server.Samples[0] with { Timestamp = work.Server.Begin.Timestamp },
                                .. work.Server.Samples, work.Server.Samples[^1] with { Timestamp = work.Server.End.Timestamp }],
                        },
                        ProcessSamples = [work.ProcessSamples[0] with { Timestamp = work.Server.Begin.Timestamp },
                            .. work.ProcessSamples, work.ProcessSamples[^1] with { Timestamp = work.Server.End.Timestamp }],
                    };
                    TrialRecord trial = measured with { Plan = measured.Plan with { Purpose = purpose }, Batch = work };
                    Assert.Equal(varyManaged ? count : 21, work.Server.Samples.Count(sample =>
                        sample.Timestamp >= work.ClientStart && sample.Timestamp <= work.ClientEnd));
                    Assert.Equal(varyManaged ? 21 : count,
                        MeasurementReport.WorkSamples(work, trial.ProcessId, trial.ProcessStartUtcTicks).Length);
                    string[] errors = MeasurementReport.ValidateTrial(trial);
                    Assert.Equal(errors, MeasurementReport.ValidateWindow(work, trial.ProcessId, trial.ProcessStartUtcTicks,
                        requireCoverage: true, pilotWork: purpose == "pilot"));
                    if (count >= (purpose == "pilot" ? 20 : 10))
                    {
                        Assert.Empty(errors);
                        Assert.NotEmpty(MeasurementReport.TrialMetrics(trial));
                    }
                    else
                    {
                        Assert.StartsWith("coverage:", Assert.Single(errors), StringComparison.Ordinal);
                        Assert.Throws<InvalidDataException>(() => MeasurementReport.TrialMetrics(trial));
                        if (purpose == "pilot")
                        {
                            TrialRecord retained = RetainedCoverageFixture(trial);
                            Assert.Empty(MeasurementReport.ValidateTrial(retained, requireCoverage: false));
                            Assert.NotEmpty(MeasurementReport.ValidateTrial(retained));
                            Assert.Equal(errors, MeasurementReport.ValidateTrial(retained with { Status = "passed", Failure = null }));
                        }
                    }
                }
            }
        }
    }

    /// <summary>Idle and loaded controls retain the ten-sample gate even for otherwise adequate twenty-sample pilots.</summary>
    [Fact]
    public void PilotControlCoverageKeepsTenSampleBoundary()
    {
        TrialRecord measured = MeasurementTrialFixture(MeasurementFixtureSchedule()[0]);
        foreach (string purpose in new[] { "pilot", "measurement" })
        {
            TrialRecord trial = measured with
            {
                Plan = measured.Plan with { Purpose = purpose },
                Batch = WindowWithSamples(measured.Batch!, purpose == "pilot" ? 20 : 10, purpose == "pilot" ? 20 : 10),
            };
            for (int index = 0; index < trial.Controls.Length; index++)
            {
                foreach (bool varyManaged in new[] { true, false })
                {
                    foreach (int count in new[] { 9, 10, 19, 20, 21 })
                    {
                        BatchWindow[] controls = trial.Controls.ToArray();
                        controls[index] = WindowWithSamples(controls[index], varyManaged ? count : 21, varyManaged ? 21 : count);
                        TrialRecord changed = trial with { Controls = controls };
                        string[] errors = MeasurementReport.ValidateTrial(changed);
                        if (count >= 10)
                        {
                            Assert.Empty(errors);
                        }
                        else
                        {
                            Assert.Equal("coverage: fewer than 10 actual samples in either stream", Assert.Single(errors));
                            if (purpose == "pilot")
                            {
                                Assert.Empty(MeasurementReport.ValidateTrial(RetainedCoverageFixture(changed), requireCoverage: false));
                            }
                        }
                    }
                }
            }
        }
    }

    /// <summary>The shared live/offline decision needs both variants at equal N in one round and stops at the unchanged finite cap.</summary>
    [Fact]
    public void PilotPairsDoubleTogetherFreezeTogetherAndFailAtCap()
    {
        MeasurementSettings settings = new();
        foreach (string caseId in new[] { "observation-create", "observation-read" })
        {
            WorkloadCase workload = Workloads.CreateCases().First(item => item.CaseId == caseId);
            int initial = settings.InitialCount(workload);
            TrialRecord[] pair = PilotPairFixture(workload, initial, 1);
            Assert.Null(MeasurementReport.NextPilotCount(pair, settings, initial));
            for (int variant = 0; variant < 2; variant++)
            {
                foreach (bool varyManaged in new[] { true, false })
                {
                    TrialRecord[] insufficient = pair.ToArray();
                    insufficient[variant] = RetainedCoverageFixture(insufficient[variant] with
                    {
                        Batch = WindowWithSamples(insufficient[variant].Batch!, varyManaged ? 19 : 20, varyManaged ? 20 : 19),
                    });
                    Assert.Equal(initial * 2, MeasurementReport.NextPilotCount(insufficient, settings, initial));
                    insufficient[variant] = insufficient[variant] with { Status = "passed", Failure = null };
                    Assert.Throws<InvalidDataException>(() => MeasurementReport.NextPilotCount(insufficient, settings, initial));
                }
            }

            TrialRecord[] next = PilotPairFixture(workload, initial * 2, 2);
            Assert.Null(MeasurementReport.NextPilotCount(next, settings, initial * 2));
            Dictionary<string, int> counts = Workloads.CreateCases().ToDictionary(item => item.RowKey, settings.InitialCount);
            counts[workload.RowKey] = initial * 2;
            TrialPlan[] scheduled = MeasurementReport.CreateSchedule(counts, settings.TrialsPerVariant);
            Assert.Equal(432, scheduled.Length);
            Assert.Equal(6, scheduled.Count(plan => plan.RowKey == workload.RowKey));
            Assert.All(scheduled.Where(plan => plan.RowKey == workload.RowKey), plan => Assert.Equal(initial * 2, plan.Operations));

            TrialRecord[][] invalid = [
                [pair[0]],
                [pair[0], pair[0]],
                [pair[0], pair[1] with { Plan = pair[1].Plan with { Operations = initial * 2 } }],
                [pair[0], pair[1] with { Plan = pair[1].Plan with { Repetition = 2 } }],
                [pair[0], pair[1] with { Plan = pair[1].Plan with { RowKey = "another-row" } }],
                [pair[0], pair[1] with { Plan = pair[1].Plan with { Sampling = false } }],
                [pair[0], pair[1] with { Plan = pair[1].Plan with { Purpose = "measurement" } }],
                [pair[0], pair[1] with { Inputs = FixtureInput("different-input") }],
                [pair[0], pair[1] with { Status = "failed", Failure = "oracle failed" }],
                [pair[0], pair[1] with { Status = "coverage-insufficient", Failure = "no actual inadequacy" }],
            ];
            Assert.All(invalid, wrong => Assert.Throws<InvalidDataException>(() => MeasurementReport.NextPilotCount(wrong, settings, initial)));
            Assert.Throws<InvalidDataException>(() => MeasurementReport.NextPilotCount(pair, settings, initial + 1));

            int[] pilotCounts = caseId == "observation-create" ? [256, 512, 1024, 2048, 4096, 8192] : [1024, 2048, 4096, 8192];
            for (int round = 0; round < pilotCounts.Length; round++)
            {
                int count = pilotCounts[round];
                TrialRecord[] capped = PilotPairFixture(workload, count, round + 1);
                Assert.Null(MeasurementReport.NextPilotCount(capped, settings, count));
                capped[1] = RetainedCoverageFixture(capped[1] with { Batch = WindowWithSamples(capped[1].Batch!, 20, 19) });
                if (count < 8192)
                {
                    Assert.Equal(pilotCounts[round + 1], MeasurementReport.NextPilotCount(capped, settings, count));
                }
                else
                {
                    InvalidDataException failure = Assert.Throws<InvalidDataException>(() => MeasurementReport.NextPilotCount(capped, settings, count));
                    Assert.Contains("8192-operation ceiling", failure.Message, StringComparison.Ordinal);
                    Assert.Contains("retain the failure and stop", failure.Message, StringComparison.Ordinal);
                }
            }
        }
    }

    /// <summary>The renderer's actual selection helper revalidates saved passed claims and cannot skip or splice pilot rounds.</summary>
    [Fact]
    public void CalibrationSelectionRevalidatesHeadroomAndFirstAdequatePair()
    {
        MeasurementSettings settings = new();
        WorkloadCase workload = Workloads.CreateCases().First(item => item.CaseId == "observation-create");
        TrialRecord[] first = PilotPairFixture(workload, 256, 1);
        first[0] = RetainedCoverageFixture(first[0] with { Batch = WindowWithSamples(first[0].Batch!, 20, 19) });
        TrialRecord[] selected = PilotPairFixture(workload, 512, 2);
        TrialRecord[] off = PilotPairFixture(workload, 512, 3).Select(trial => trial with
        {
            Plan = trial.Plan with { Purpose = "sampler-off", Sampling = false },
            Batch = trial.Batch! with { Server = trial.Batch.Server with { Sampling = false, Samples = [] }, ProcessSamples = [] },
            Controls = trial.Controls.Select(control => control with
            {
                Server = control.Server with { Sampling = false, Samples = [] }, ProcessSamples = [],
            }).ToArray(),
        }).ToArray();
        TrialRecord[] trials = [.. first, .. selected, .. off];
        EvidenceReference[] References(TrialRecord[] records) => records.Select(trial =>
            new EvidenceReference(Path.Combine(trial.Plan.RelativeDirectory, "trial.json"), 1, new string('E', 64))).ToArray();
        CalibrationProfile profile = new(workload.RowKey, 512, References(trials),
            selected.Select(trial => trial.Plan.AttemptId).ToArray(), off.Select(trial => trial.Plan.AttemptId).ToArray());
        FrozenCaseInput input = new(workload, 512, selected[0].Inputs, new string('C', 64), new string('D', 64), 1, 1);
        MeasurementReport.ValidateCalibrationSelection(profile, input, settings, trials);
        Assert.Empty(MeasurementReport.ValidateTrial(first[0], requireCoverage: false));

        for (int variant = 2; variant < 4; variant++)
        {
            foreach (bool varyManaged in new[] { true, false })
            {
                foreach (int count in new[] { 9, 10, 19, 20, 21 })
                {
                    TrialRecord[] changed = trials.ToArray();
                    changed[variant] = changed[variant] with
                    {
                        Batch = WindowWithSamples(changed[variant].Batch!, varyManaged ? count : 21, varyManaged ? 21 : count),
                    };
                    TrialRecord[] saved = JsonSerializer.Deserialize<TrialRecord[]>(
                        JsonSerializer.Serialize(changed, ProbeProtocol.JsonOptions), ProbeProtocol.JsonOptions)!;
                    Assert.Equal("passed", saved[variant].Status);
                    if (count >= 20)
                    {
                        MeasurementReport.ValidateCalibrationSelection(profile, input, settings, saved);
                    }
                    else
                    {
                        InvalidDataException failure = Assert.Throws<InvalidDataException>(() =>
                            MeasurementReport.ValidateCalibrationSelection(profile, input, settings, saved));
                        Assert.Contains("coverage: fewer than 20", failure.Message, StringComparison.Ordinal);
                    }
                }
            }
        }

        TrialRecord[] priorError = trials.ToArray();
        priorError[0] = priorError[0] with { Batch = priorError[0].Batch! with { ProcessSamplingErrors = 1 } };
        Assert.Throws<InvalidDataException>(() => MeasurementReport.ValidateCalibrationSelection(profile, input, settings, priorError));
        TrialRecord[] skippedAdequate = trials.ToArray();
        skippedAdequate[0] = first[0] with { Status = "passed", Failure = null, Batch = WindowWithSamples(first[0].Batch!, 20, 20) };
        Assert.Throws<InvalidDataException>(() => MeasurementReport.ValidateCalibrationSelection(profile, input, settings, skippedAdequate));
        TrialRecord[] droppedPair = trials[2..];
        Assert.Throws<InvalidDataException>(() => MeasurementReport.ValidateCalibrationSelection(
            profile with { Attempts = References(droppedPair) }, input, settings, droppedPair));
        TrialRecord[] extraPair = [.. first, .. selected, .. PilotPairFixture(workload, 1024, 4), .. off];
        Assert.Throws<InvalidDataException>(() => MeasurementReport.ValidateCalibrationSelection(
            profile with { Attempts = References(extraPair) }, input, settings, extraPair));
        Assert.Throws<InvalidDataException>(() => MeasurementReport.ValidateCalibrationSelection(
            profile with { NormalAttempts = [first[1].Plan.AttemptId, selected[0].Plan.AttemptId] }, input, settings, trials));
        TrialRecord[] offSamples = trials.ToArray();
        offSamples[4] = offSamples[4] with { Batch = offSamples[4].Batch! with { ProcessSamples = selected[0].Batch!.ProcessSamples } };
        Assert.Throws<InvalidDataException>(() => MeasurementReport.ValidateCalibrationSelection(profile, input, settings, offSamples));
    }

    /// <summary>Retention waives sample counts only, never work errors, missing metrics, clocks, lifetime, denominator or oracles.</summary>
    [Fact]
    public void PilotHeadroomRetentionDoesNotWaiveWorkFailures()
    {
        WorkloadCase workload = Workloads.CreateCases().First(item => item.CaseId == "observation-update");
        TrialRecord[] pair = PilotPairFixture(workload, 1024, 1);
        TrialRecord retained = RetainedCoverageFixture(pair[0] with { Batch = WindowWithSamples(pair[0].Batch!, 19, 20) });
        Assert.Empty(MeasurementReport.ValidateTrial(retained, requireCoverage: false));
        BatchWindow batch = retained.Batch!;
        BatchWindow[] invalidWindows = [
            batch with { ProcessSamplingErrors = 1 },
            batch with { ProcessDroppedSamples = 1 },
            batch with { Server = batch.Server with { SamplingErrors = 1 } },
            batch with { Server = batch.Server with { DroppedSamples = 1 } },
            batch with { Server = batch.Server with { DeadlineExceeded = true } },
            batch with { Server = batch.Server with { ProcessId = retained.ProcessId + 1 } },
            batch with { Server = batch.Server with { ProcessStartUtcTicks = retained.ProcessStartUtcTicks + 1 } },
            batch with { Server = batch.Server with { Frequency = batch.Frequency + 1 } },
            batch with { ProcessSamples = [batch.ProcessSamples[0] with { ProcessId = retained.ProcessId + 1 }, .. batch.ProcessSamples[1..]] },
            batch with { ProcessSamples = [batch.ProcessSamples[0] with { ProcessStartUtcTicks = retained.ProcessStartUtcTicks + 1 }, .. batch.ProcessSamples[1..]] },
            batch with { ProcessSamples = [batch.ProcessSamples[0] with { Timestamp = batch.Server.Begin.Timestamp - 1 }, .. batch.ProcessSamples[1..]] },
            batch with { ProcessSamples = batch.ProcessSamples.AsEnumerable().Reverse().ToArray() },
            batch with { Server = batch.Server with { Samples = batch.Server.Samples.AsEnumerable().Reverse().ToArray() } },
            batch with { Server = batch.Server with { Samples = [batch.Server.Samples[0] with { OccupiedBytes = -1 }, .. batch.Server.Samples[1..]] } },
            batch with { Server = batch.Server with { End = batch.Server.End with { AllocatedBytes = batch.Server.Begin.AllocatedBytes - 1 } } },
            batch with { CompletedOperations = batch.CompletedOperations - 1 },
            batch with { Operations = batch.Operations[..^1] },
            batch with { ClientEnd = batch.ClientStart },
            batch with { Failure = "work failure" },
        ];
        foreach (BatchWindow invalid in invalidWindows)
        {
            TrialRecord wrong = retained with { Batch = invalid };
            Assert.NotEmpty(MeasurementReport.ValidateTrial(wrong, requireCoverage: false));
            Assert.Throws<InvalidDataException>(() => MeasurementReport.NextPilotCount([wrong, pair[1]], new(), 1024));
        }

        TrialRecord[] invalidTrials = [
            retained with { Batch = null },
            retained with { OraclePassed = false },
            retained with { SealsBeforeAndAfter = false },
            retained with { CleanShutdown = false },
            retained with { Warmup = retained.Warmup! with { OriginalUpdate = retained.Warmup.OriginalUpdate! with { VersionId = "2" } } },
            retained with { Plan = retained.Plan with { Operations = 1023 } },
            retained with { Plan = retained.Plan with { Purpose = "measurement" } },
            retained with { Status = "failed" },
            retained with { Failure = null },
        ];
        Assert.All(invalidTrials, wrong => Assert.NotEmpty(MeasurementReport.ValidateTrial(wrong, requireCoverage: false)));
        string[][] missingFields = [
            ["ProcessSamples"], ["Server", "Samples"], ["Server", "Begin", "Gen0"], ["Server", "SamplingErrors"],
        ];
        foreach (string[] path in missingFields)
        {
            JsonObject document = JsonSerializer.SerializeToNode(retained, ProbeProtocol.JsonOptions)!.AsObject();
            JsonNode field = document["Batch"]!;
            foreach (string name in path[..^1])
            {
                field = field[name]!;
            }

            Assert.True(field.AsObject().Remove(path[^1]));
            Assert.Throws<JsonException>(() => MeasurementReport.ValidateTrial(document.Deserialize<TrialRecord>(ProbeProtocol.JsonOptions)!,
                requireCoverage: false));
        }
    }

    /// <summary>Retained control coverage failures cannot become adequate selected pilots or accepted measured trials.</summary>
    [Fact]
    public void RetainedPilotControlCoverageCannotBecomeSelectedOrAccepted()
    {
        const string Coverage = "coverage: fewer than 10 actual samples in either stream";
        (FrozenMeasurement frozen, TrialRecord[] trials) = MeasurementFixture();
        TrialRecord measured = trials.First(trial => trial.Plan.RowKey == "R4-100p-1000o-subscription-receive" &&
            trial.Plan.Variant == "candidate");
        TrialRecord pilot = measured with
        {
            Plan = measured.Plan with { Purpose = "pilot" }, FrozenSha256 = null,
            Batch = WindowWithSamples(measured.Batch!, 20, 20),
        };
        Assert.Empty(MeasurementReport.ValidateTrial(pilot));
        foreach ((int managedCount, int processCount) in new[] { (65, 4), (4, 65) })
        {
            BatchWindow control = pilot.Controls[1];
            BatchWindow insufficient = control with
            {
                Server = control.Server with
                {
                    Samples = Enumerable.Range(0, managedCount).Select(index => control.Server.Samples[0] with
                    {
                        Timestamp = control.ClientStart + 5 + (index * 10),
                    }).ToArray(),
                },
                ProcessSamples = Enumerable.Range(0, processCount).Select(index => control.ProcessSamples[0] with
                {
                    Timestamp = control.ClientStart + 5 + (index * 10),
                }).ToArray(),
            };
            Assert.Equal("loaded-control", insufficient.Name);
            Assert.Equal(1.0, (insufficient.ClientEnd - insufficient.ClientStart) / (double)insufficient.Frequency);
            Assert.Equal(managedCount, insufficient.Server.Samples.Count(sample =>
                sample.Timestamp >= insufficient.ClientStart && sample.Timestamp <= insufficient.ClientEnd));
            Assert.Equal(processCount, MeasurementReport.WorkSamples(insufficient, pilot.ProcessId, pilot.ProcessStartUtcTicks).Length);
            TrialRecord retained = pilot with
            {
                Status = "coverage-insufficient", Failure = Coverage, Controls = [pilot.Controls[0], insufficient],
            };
            Assert.Empty(MeasurementReport.ValidateTrial(retained, requireCoverage: false));
            Assert.Contains(Coverage, MeasurementReport.ValidateTrial(retained));
            TrialRecord selected = retained with { Status = "passed", Failure = null };
            Assert.Equal(Coverage, Assert.Single(MeasurementReport.ValidateTrial(selected)));
            TrialRecord accepted = measured with { Controls = retained.Controls };
            Assert.Equal(Coverage, Assert.Single(MeasurementReport.ValidateTrial(accepted)));
            Assert.Throws<InvalidDataException>(() => MeasurementReport.TrialMetrics(accepted));
            TrialRecord[] invalidMatrix = trials.ToArray();
            invalidMatrix[Array.IndexOf(trials, measured)] = accepted;
            Assert.Contains(accepted.Plan.AttemptId + ": " + Coverage, MeasurementReport.ValidateMatrix(frozen, invalidMatrix));

            BatchWindow[] invalidControls = [
                insufficient with { ProcessSamplingErrors = 1 },
                insufficient with { ProcessDroppedSamples = 1 },
                insufficient with { Server = insufficient.Server with { SamplingErrors = 1 } },
                insufficient with { Server = insufficient.Server with { DroppedSamples = 1 } },
                insufficient with { Server = insufficient.Server with { DeadlineExceeded = true } },
                insufficient with { Server = insufficient.Server with { ProcessId = pilot.ProcessId + 1 } },
                insufficient with { Server = insufficient.Server with { ProcessStartUtcTicks = pilot.ProcessStartUtcTicks + 1 } },
                insufficient with { Server = insufficient.Server with { Frequency = insufficient.Frequency + 1 } },
                insufficient with
                {
                    ProcessSamples = [insufficient.ProcessSamples[0] with { ProcessId = pilot.ProcessId + 1 },
                        .. insufficient.ProcessSamples[1..]],
                },
                insufficient with
                {
                    ProcessSamples = [insufficient.ProcessSamples[0] with { ProcessStartUtcTicks = pilot.ProcessStartUtcTicks + 1 },
                        .. insufficient.ProcessSamples[1..]],
                },
                insufficient with
                {
                    ProcessSamples = [insufficient.ProcessSamples[0] with { Timestamp = insufficient.Server.Begin.Timestamp - 1 },
                        .. insufficient.ProcessSamples[1..]],
                },
                insufficient with { ProcessSamples = insufficient.ProcessSamples.AsEnumerable().Reverse().ToArray() },
                insufficient with { Server = insufficient.Server with { Samples = insufficient.Server.Samples.AsEnumerable().Reverse().ToArray() } },
                insufficient with
                {
                    Server = insufficient.Server with
                    {
                        Samples = [insufficient.Server.Samples[0] with { Timestamp = insufficient.Server.Begin.Timestamp - 1 },
                            .. insufficient.Server.Samples[1..]],
                    },
                },
                insufficient with
                {
                    Server = insufficient.Server with
                    {
                        End = insufficient.Server.End with { AllocatedBytes = insufficient.Server.Begin.AllocatedBytes - 1 },
                    },
                },
                insufficient with { EndSent = insufficient.ClientEnd - 1 },
                insufficient with { ExpectedOperations = 1 },
                insufficient with { Failure = "retained control failure" },
            ];
            Assert.All(invalidControls, wrong => Assert.NotEmpty(MeasurementReport.ValidateTrial(retained with
            {
                Controls = [pilot.Controls[0], wrong],
            }, requireCoverage: false)));
            TrialRecord[] invalidTrials = [
                retained with { Controls = [] },
                retained with { Controls = [pilot.Controls[0]] },
                retained with { Controls = [insufficient, pilot.Controls[0]] },
                retained with { Batch = null },
                retained with { Batch = retained.Batch! with { CompletedOperations = retained.Plan.Operations - 1 } },
                retained with { Checkpoints = retained.Checkpoints[..^1] },
                retained with { OraclePassed = false },
                retained with { CleanShutdown = false },
                retained with { SealsBeforeAndAfter = false },
                retained with { Status = "failed" },
                retained with { Failure = null },
            ];
            Assert.All(invalidTrials, wrong => Assert.NotEmpty(MeasurementReport.ValidateTrial(wrong, requireCoverage: false)));
            string[][] missingFields = [
                ["ProcessSamples"], ["Server", "Samples"], ["Server", "Begin", "Gen0"], ["Server", "SamplingErrors"],
            ];
            foreach (string[] path in missingFields)
            {
                JsonObject document = JsonSerializer.SerializeToNode(retained, ProbeProtocol.JsonOptions)!.AsObject();
                JsonNode field = document["Controls"]![1]!;
                foreach (string name in path[..^1])
                {
                    field = field[name]!;
                }

                Assert.True(field.AsObject().Remove(path[^1]));
                Assert.Throws<JsonException>(() => MeasurementReport.ValidateTrial(
                    document.Deserialize<TrialRecord>(ProbeProtocol.JsonOptions)!, requireCoverage: false));
            }
        }
    }

    /// <summary>Disabling the retention coverage minimum never permits samples in sampler-off work or controls.</summary>
    [Fact]
    public void RetainedPilotCoverageFlagPreservesSamplerOffValidation()
    {
        TrialRecord sampled = MeasurementTrialFixture(MeasurementFixtureSchedule()[0]);
        BatchWindow batch = sampled.Batch!;
        TrialRecord off = sampled with
        {
            Plan = sampled.Plan with { Purpose = "sampler-off", Sampling = false }, FrozenSha256 = null,
            Batch = batch with { Server = batch.Server with { Sampling = false, Samples = [] }, ProcessSamples = [] },
            Controls = sampled.Controls.Select(control => control with
            {
                Server = control.Server with { Sampling = false, Samples = [] }, ProcessSamples = [],
            }).ToArray(),
        };
        Assert.Empty(MeasurementReport.ValidateTrial(off));
        Assert.Empty(MeasurementReport.ValidateTrial(off, requireCoverage: false));
        BatchWindow[] windows = [off.Batch!, .. off.Controls];
        foreach (BatchWindow window in windows)
        {
            BatchWindow original = window.Name == "work" ? batch : sampled.Controls.Single(control => control.Name == window.Name);
            foreach (int count in new[] { 1, 9, 10, 19, 20, 21 })
            {
                BatchWindow populated = WindowWithSamples(original, count, count);
                foreach (BatchWindow invalidWindow in new[]
                {
                    window with { ProcessSamples = populated.ProcessSamples },
                    window with { Server = window.Server with { Samples = populated.Server.Samples } },
                })
                {
                    TrialRecord invalid = off with
                    {
                        Batch = window.Name == "work" ? invalidWindow : off.Batch,
                        Controls = off.Controls.Select(control => control.Name == window.Name ? invalidWindow : control).ToArray(),
                    };
                    Assert.Contains("sampler-off window contains samples", MeasurementReport.ValidateTrial(invalid));
                    Assert.Contains("sampler-off window contains samples", MeasurementReport.ValidateTrial(invalid, requireCoverage: false));
                }
            }
        }
    }

    /// <summary>Completed HTTP errors/partial operations cannot be erased or relabelled with a smaller N.</summary>
    [Fact]
    public void FailedBatchCannotBecomeSuccessfulByChangingDenominator()
    {
        TrialRecord trial = MeasurementTrialFixture(MeasurementFixtureSchedule()[0]);
        BatchWindow batch = trial.Batch!;
        BatchWindow partial = batch with
        {
            CompletedOperations = batch.CompletedOperations - 1, Operations = batch.Operations[..^1],
            Failure = "retained transport failure",
        };
        Assert.NotEmpty(MeasurementReport.ValidateTrial(trial with { Batch = partial }));
        Assert.Throws<InvalidDataException>(() => MeasurementReport.TrialMetrics(trial with { Batch = partial }));
        BatchWindow relabelled = partial with
        {
            ExpectedOperations = partial.CompletedOperations, AttemptedOperations = partial.CompletedOperations, Failure = null,
        };
        Assert.Contains(MeasurementReport.ValidateTrial(trial with { Batch = relabelled }),
            error => error.Contains("fixed operation", StringComparison.Ordinal));
        Assert.Throws<InvalidDataException>(() => MeasurementReport.TrialMetrics(trial with { Batch = relabelled }));
        Assert.Throws<InvalidDataException>(() => MeasurementReport.TrialMetrics(trial with { Status = "failed" }));
        Assert.Throws<InvalidDataException>(() => MeasurementReport.TrialMetrics(trial with { OraclePassed = false }));
        Assert.Throws<InvalidDataException>(() => MeasurementReport.TrialMetrics(trial with
        {
            Batch = batch with { Server = batch.Server with { End = batch.Server.End with { AllocatedBytes = batch.Server.Begin.AllocatedBytes - 1 } } },
        }));
    }

    /// <summary>Unequal request populations do not weight independent trial central estimates.</summary>
    [Fact]
    public void SummaryUsesIndependentTrialMedians()
    {
        MetricSummary summary = MeasurementReport.CalculateSummary([(1.0, 1000), (4.0, 10), (100.0, 1)]);
        Assert.Equal(4, summary.Median);
        Assert.Equal(1, summary.Minimum);
        Assert.Equal(100, summary.Maximum);
        Assert.Equal(3, summary.Trials);
        Assert.Equal(new[] { 1000, 10, 1 }, summary.Operations);
        Assert.Equal(6, MeasurementReport.CalculateSummary([(4.0, 256), (8.0, 256)]).Median);
        Assert.Throws<InvalidDataException>(() => MeasurementReport.CalculateSummary([]));
        Assert.Throws<InvalidDataException>(() => MeasurementReport.CalculateSummary([(double.NaN, 256)]));
        Assert.Throws<InvalidDataException>(() => MeasurementReport.CalculateSummary([(1.0, 0)]));
        TrialRecord trial = MeasurementTrialFixture(MeasurementFixtureSchedule()[0]);
        Dictionary<string, double> metrics = MeasurementReport.TrialMetrics(trial);
        double seconds = (trial.Batch!.ClientEnd - trial.Batch.ClientStart) / (double)trial.Batch.Frequency;
        Assert.Equal(seconds * 1000 / trial.Plan.Operations, metrics["time.ms/op"]);
        Assert.Equal(trial.Plan.Operations / seconds, metrics["throughput.op/s"]);
        Assert.Equal((trial.Batch.Server.End.AllocatedBytes - trial.Batch.Server.Begin.AllocatedBytes) / (double)trial.Plan.Operations,
            metrics["allocation.B/op"]);
        Assert.DoesNotContain(metrics.Keys, key => key.Contains("gc.total", StringComparison.Ordinal));
    }

    /// <summary>Valid zero GC and negative incremental footprint baselines retain absolute changes without fictitious ratios.</summary>
    [Fact]
    public void RatiosHandleZeroAndNegativeBaselines()
    {
        MetricChange zero = MeasurementReport.CalculateChange(0, 2);
        Assert.Null(zero.Ratio);
        Assert.Null(zero.Percent);
        Assert.Equal(2, zero.Absolute);
        Assert.Equal("zero baseline", zero.Reason);
        MetricChange negative = MeasurementReport.CalculateChange(-10, -20);
        Assert.Null(negative.Ratio);
        Assert.Null(negative.Percent);
        Assert.Equal(-10, negative.Absolute);
        Assert.Equal("negative incremental baseline", negative.Reason);
        MetricChange decreased = MeasurementReport.CalculateChange(10, 0);
        Assert.Equal(0.0, decreased.Ratio);
        Assert.Equal(-100.0, decreased.Percent);
        Assert.Null(decreased.Reason);
        MetricChange increased = MeasurementReport.CalculateChange(4, 6);
        Assert.Equal(1.5, increased.Ratio);
        Assert.Equal(50.0, increased.Percent);
        Assert.Throws<InvalidDataException>(() => MeasurementReport.CalculateChange(double.NaN, 1));
        Assert.Throws<InvalidDataException>(() => MeasurementReport.CalculateChange(1, double.PositiveInfinity));
    }

    /// <summary>Exercises the used matrix/window validators and required JSON fields, including every memory/counter component.</summary>
    [Fact]
    public void RendererRejectsMissingVariantCaseTrialOrMetric()
    {
        (FrozenMeasurement frozen, TrialRecord[] trials) = MeasurementFixture();
        Assert.Empty(MeasurementReport.ValidateMatrix(frozen, trials));
        Assert.NotEmpty(MeasurementReport.ValidateMatrix(frozen, trials[..^1]));
        Assert.NotEmpty(MeasurementReport.ValidateMatrix(frozen, trials.Where(trial => trial.Plan.Variant != "candidate").ToArray()));
        Assert.NotEmpty(MeasurementReport.ValidateMatrix(frozen, trials.Where(trial => trial.Plan.RowKey != frozen.Inputs[0].Case.RowKey).ToArray()));
        Assert.NotEmpty(MeasurementReport.ValidateMatrix(frozen with { Inputs = frozen.Inputs[..^1] }, trials));
        TrialRecord valid = trials[0];
        Assert.All(trials, trial => Assert.Equal("unit-fixture", trial.Origin));
        for (int index = 0; index < valid.Checkpoints.Length; index++)
        {
            Assert.NotEmpty(MeasurementReport.ValidateTrial(valid with
            {
                Checkpoints = valid.Checkpoints.Where((_, position) => position != index).ToArray(),
            }));
        }

        TrialRecord[] missing = [
            valid with { Batch = null },
            valid with { Controls = [] },
            valid with { Warmup = null },
            valid with { ValidatedSeedReads = valid.ValidatedSeedReads - 1 },
            valid with { CleanShutdown = false },
            valid with { SealsBeforeAndAfter = false },
            valid with { Batch = valid.Batch! with { ProcessSamples = [] } },
            valid with { Batch = valid.Batch! with { Server = valid.Batch.Server with { Samples = [] } } },
            valid with { Batch = valid.Batch! with { ProcessSamplingErrors = 1 } },
            valid with { Batch = valid.Batch! with { ProcessDroppedSamples = 1 } },
            valid with { Batch = valid.Batch! with { Server = valid.Batch.Server with { SamplingErrors = 1 } } },
            valid with { Batch = valid.Batch! with { Server = valid.Batch.Server with { DroppedSamples = 1 } } },
            valid with { Batch = valid.Batch! with { Server = valid.Batch.Server with { DeadlineExceeded = true } } },
            valid with { Batch = valid.Batch! with { ClientEnd = valid.Batch.ClientStart } },
        ];
        Assert.All(missing, trial => Assert.NotEmpty(MeasurementReport.ValidateTrial(trial)));
        // JsonRequired makes an absent numeric field distinguishable from a genuine zero GC count.
        foreach ((object value, Type type, string[] fields) in new[]
        {
            ((object)valid.Batch!.Server.Begin, typeof(CounterReading), new[] { "Timestamp", "AllocatedBytes", "Gen0", "Gen1", "Gen2", "PauseTicks", "CpuTicks" }),
            ((object)valid.Batch.Server.Samples[0], typeof(ManagedSample), new[] { "Timestamp", "OccupiedBytes", "HeapBytes", "FragmentedBytes", "CommittedBytes", "LastGcIndex" }),
            ((object)valid.Batch.ProcessSamples[0], typeof(ProcessMemorySample), new[] { "ProcessId", "ProcessStartUtcTicks", "Timestamp", "WorkingSetBytes", "PrivateBytes", "CpuTicks" }),
            ((object)valid.Checkpoints[0], typeof(Footprint), new[] { "Name", "ForcedGc", "ProcessId", "Timestamp", "Frequency", "WorkingSetBytes", "PrivateBytes", "Managed", "Counters" }),
            ((object)valid.Batch.Server, typeof(WindowEvidence), new[] { "Begin", "End", "Samples", "DroppedSamples", "SamplingErrors", "PostWork", "CounterOrigin", "DeadlineExceeded" }),
            ((object)valid.Batch, typeof(BatchWindow), new[] { "Frequency", "ClientStart", "ClientEnd", "Server", "Operations", "ProcessSamples", "ExpectedOperations", "CompletedOperations" }),
        })
        {
            foreach (string field in fields)
            {
                JsonObject document = JsonSerializer.SerializeToNode(value, type, ProbeProtocol.JsonOptions)!.AsObject();
                Assert.True(document.Remove(field));
                Assert.Throws<JsonException>(() => JsonSerializer.Deserialize(document.ToJsonString(), type, ProbeProtocol.JsonOptions));
            }
        }
    }

    /// <summary>Rendering saved incomplete/unit evidence is deterministic, read-only and never tries to launch unavailable artifacts.</summary>
    [Fact]
    public async Task RendererReproducesSavedEvidenceWithoutServerExecution()
    {
        string directory = Path.Combine(Path.GetTempPath(), "candle-perf-render-fixture-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            (FrozenMeasurement frozen, TrialRecord[] trials) = MeasurementFixture();
            trials[0] = trials[0] with { Finished = trials[0].Finished with { Utc = trials[0].StartedUtc.AddSeconds(-1) } };
            ExperimentManifest manifest = Manifest();
            PreparedExperiment prepared = new(3, ProbeProtocol.Version, manifest.RunId, new string('A', 64),
                RequiredAssemblyFixture("baseline", "R4").Artifact, RequiredAssemblyFixture("candidate", "R4").Artifact,
                [], new Dictionary<string, FileSeal>(), new("unavailable-dotnet", 1, new string('B', 64)), new string('B', 64),
                new("unavailable-build", 1, new string('B', 64)), new("unavailable-log", 1, new string('B', 64)),
                new("unavailable-isolation", 1, new string('B', 64)), new("unavailable-archive", 1, new string('B', 64)), [],
                new(Workloads.ContractVersion, Workloads.Algorithm, Workloads.Seed, 4, [], [], []), DateTimeOffset.UnixEpoch);
            await File.WriteAllBytesAsync(Path.Combine(directory, "manifest.json"), JsonSerializer.SerializeToUtf8Bytes(manifest, ProbeProtocol.JsonOptions));
            await File.WriteAllBytesAsync(Path.Combine(directory, "prepared.json"), JsonSerializer.SerializeToUtf8Bytes(prepared, ProbeProtocol.JsonOptions));
            await File.WriteAllBytesAsync(Path.Combine(directory, "frozen-measurement.json"), JsonSerializer.SerializeToUtf8Bytes(frozen, ProbeProtocol.JsonOptions));
            await File.WriteAllTextAsync(Path.Combine(directory, "fixture-environment.json"), "{\"Origin\":\"unit-fixture\",\"Observation\":\"not live performance\"}");
            string trialRelative = Path.Combine(trials[0].Plan.RelativeDirectory, "trial.json");
            Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(directory, trialRelative))!);
            byte[] trialBytes = JsonSerializer.SerializeToUtf8Bytes(trials[0], ProbeProtocol.JsonOptions);
            await File.WriteAllBytesAsync(Path.Combine(directory, trialRelative), trialBytes);
            Directory.CreateDirectory(Path.Combine(directory, "measurement"));
            MeasurementLedger ledger = new(3, frozen.RunId,
                Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(directory, "frozen-measurement.json")))),
                trials[0].Started with { Utc = trials[0].StartedUtc.AddSeconds(-1), Before = trials[0].Started.Before - 1000, After = trials[0].Started.After - 1000 },
                trials[0].Finished with { Utc = trials[0].FinishedUtc.AddSeconds(1), Before = trials[0].Finished.Before + 1000, After = trials[0].Finished.After + 1000 },
                [new(trialRelative, trialBytes.Length, Convert.ToHexString(SHA256.HashData(trialBytes)))],
                frozen.Schedule[1..], "unit-fixture, not an executed schedule", trials[0].Parent);
            await File.WriteAllBytesAsync(Path.Combine(directory, "measurement", "ledger.json"), JsonSerializer.SerializeToUtf8Bytes(ledger, ProbeProtocol.JsonOptions));
            Dictionary<string, string> before = Directory.GetFiles(directory, "*", SearchOption.AllDirectories)
                .ToDictionary(path => Path.GetRelativePath(directory, path),
                path => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))));
            (string first, CompletenessResult result) = await MeasurementReport.RenderSavedAsync(directory, CancellationToken.None);
            (string second, CompletenessResult rerendered) = await MeasurementReport.RenderSavedAsync(directory, CancellationToken.None);
            Assert.False(result.Complete);
            Assert.False(rerendered.Complete);
            Assert.Equal(first, second);
            Assert.Contains("INCOMPLETE", first, StringComparison.Ordinal);
            Assert.Contains("Unit fixtures", first, StringComparison.Ordinal);
            Assert.Contains("trial-0001", first, StringComparison.Ordinal);
            Assert.Contains("parsed attempts: 1", first, StringComparison.Ordinal);
            Assert.Contains("Clock observations and interpretation", first, StringComparison.Ordinal);
            Assert.Contains("trial-0001 lifetime", first, StringComparison.Ordinal);
            Assert.Contains(trials[0].StartedUtc.ToString("O", CultureInfo.InvariantCulture) + " → " +
                trials[0].FinishedUtc.ToString("O", CultureInfo.InvariantCulture), first, StringComparison.Ordinal);
            Assert.Contains("net UTC adjustment", first, StringComparison.Ordinal);
            Assert.Contains("measured UTC-read uncertainty", first, StringComparison.Ordinal);
            Assert.Contains("endpoints cannot locate its instant or detect cancelling intermediate changes", first, StringComparison.Ordinal);
            Assert.All(Workloads.CreateCases(), workload => Assert.Contains(workload.RowKey, first, StringComparison.Ordinal));
            Assert.Equal(before.Count, Directory.GetFiles(directory, "*", SearchOption.AllDirectories).Length);
            foreach (string file in Directory.GetFiles(directory, "*", SearchOption.AllDirectories))
            {
                Assert.Equal(before[Path.GetRelativePath(directory, file)], Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))));
            }

            foreach (FrozenMeasurement incompatible in new[]
            {
                frozen with { Settings = frozen.Settings with { SchemaVersion = 1 } },
                frozen with { Settings = frozen.Settings with { PilotMinimumSamples = 10 } },
                frozen with { ContractVersion = "candle-perf-measurement-v3" },
            })
            {
                await File.WriteAllBytesAsync(Path.Combine(directory, "frozen-measurement.json"),
                    JsonSerializer.SerializeToUtf8Bytes(incompatible, ProbeProtocol.JsonOptions));
                (string policyMarkdown, CompletenessResult policyResult) =
                    await MeasurementReport.RenderSavedAsync(directory, CancellationToken.None);
                Assert.False(policyResult.Complete);
                Assert.Contains("Saved pilot/accepted/control coverage policy is unavailable:", policyMarkdown, StringComparison.Ordinal);
                Assert.DoesNotContain("Saved measurement settings schema 2:", policyMarkdown, StringComparison.Ordinal);
            }

            await File.WriteAllBytesAsync(Path.Combine(directory, "frozen-measurement.json"),
                JsonSerializer.SerializeToUtf8Bytes(frozen with
                {
                    Bounds = frozen.Bounds with { SetupSeconds = 600, BatchSeconds = 60 },
                }, ProbeProtocol.JsonOptions));
            (string incompatibleBounds, CompletenessResult incompatibleResult) =
                await MeasurementReport.RenderSavedAsync(directory, CancellationToken.None);
            Assert.False(incompatibleResult.Complete);

            File.Delete(Path.Combine(directory, "frozen-measurement.json"));
            (string manifestOnly, CompletenessResult manifestResult) =
                await MeasurementReport.RenderSavedAsync(directory, CancellationToken.None);
            Assert.False(manifestResult.Complete);
            foreach (string markdown in new[] { first, second, manifestOnly })
            {
                Assert.Contains("Saved measurement settings schema 2: at least 20 actual in-client-work samples per stream in each selected sampled pilot; at least 10 per stream in accepted measured work and every sampled control, including pilot controls.", markdown, StringComparison.Ordinal);
                Assert.Contains("Sampler-off streams must be empty.", markdown, StringComparison.Ordinal);
                Assert.Contains("Both variants must pass in the same paired round before common N freezes.", markdown, StringComparison.Ordinal);
                Assert.Contains("Initial create/hook N=256, other N=1024; coverage-only failures permit paired doubling up to 8192", markdown, StringComparison.Ordinal);
                Assert.Contains("Controls stay fixed at 1000 ms.", markdown, StringComparison.Ordinal);
                Assert.Contains("Pilot headroom is not a performance budget or a guarantee of later coverage", markdown, StringComparison.Ordinal);
                Assert.Contains("720004-slot buffers, 10 ms nominal cadence", markdown, StringComparison.Ordinal);
                Assert.Contains("Saved startup/setup/measured/request/settle/shutdown bounds: 120s/1200s/7200s/30s/1s/15s.", markdown, StringComparison.Ordinal);
                Assert.Contains("Setup+audit share the 1200s budget; the measured window has its separate 7200s limit.", markdown, StringComparison.Ordinal);
                Assert.Contains("stopped export has its own 120s budget after EndWindow, separate from setup/audit, measured work and the 30s end acknowledgement/ordinary HTTP/pipe exchange limit.", markdown, StringComparison.Ordinal);
                Assert.Contains("finite child lifetime is 8710s", markdown, StringComparison.Ordinal);
                Assert.Contains("10s boundary allowance and two 15s shutdown intervals.", markdown, StringComparison.Ordinal);
                Assert.Contains("configured limits, not observed durations.", markdown, StringComparison.Ordinal);
            }

            JsonObject incompleteManifest = JsonSerializer.SerializeToNode(manifest, ProbeProtocol.JsonOptions)!.AsObject();
            Assert.True(incompleteManifest.Remove("Bounds"));
            await File.WriteAllTextAsync(Path.Combine(directory, "manifest.json"), incompleteManifest.ToJsonString());
            (string missingBounds, CompletenessResult missingResult) =
                await MeasurementReport.RenderSavedAsync(directory, CancellationToken.None);
            Assert.False(missingResult.Complete);
            incompleteManifest["Bounds"] = null;
            await File.WriteAllTextAsync(Path.Combine(directory, "manifest.json"), incompleteManifest.ToJsonString());
            (string nullBounds, CompletenessResult nullResult) =
                await MeasurementReport.RenderSavedAsync(directory, CancellationToken.None);
            Assert.False(nullResult.Complete);

            await File.WriteAllBytesAsync(Path.Combine(directory, "manifest.json"),
                JsonSerializer.SerializeToUtf8Bytes(manifest with { ProtocolVersion = "candle-perf-phase3-v1" }, ProbeProtocol.JsonOptions));
            (string incompatibleProtocol, CompletenessResult protocolResult) =
                await MeasurementReport.RenderSavedAsync(directory, CancellationToken.None);
            Assert.False(protocolResult.Complete);
            foreach (string markdown in new[] { incompatibleBounds, missingBounds, nullBounds, incompatibleProtocol })
            {
                Assert.Contains("Saved timing bounds, stopped-export budget and child lifetime are unavailable:", markdown, StringComparison.Ordinal);
                Assert.DoesNotContain("Saved startup/setup/measured/request/settle/shutdown bounds:", markdown, StringComparison.Ordinal);
                Assert.DoesNotContain("stopped export has its own 120s budget", markdown, StringComparison.Ordinal);
                Assert.DoesNotContain("finite child lifetime is 8710s", markdown, StringComparison.Ordinal);
            }

            foreach (string markdown in new[] { first, second, manifestOnly, incompatibleBounds, missingBounds, nullBounds, incompatibleProtocol })
            {
                Assert.Contains("INCOMPLETE", markdown, StringComparison.Ordinal);
                Assert.DoesNotContain("600s", markdown, StringComparison.Ordinal);
                Assert.DoesNotContain("60s", markdown, StringComparison.Ordinal);
            }

            Assert.False(File.Exists(Path.Combine(directory, "performance.md")));
            Assert.False(Directory.Exists(Path.Combine(directory, "child")));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>Small in-memory input with a hostile advertised length; never allocates a maximum-sized evidence file.</summary>
    private sealed class DeclaredLengthStream(byte[] bytes, long declaredLength) : MemoryStream(bytes)
    {
        /// <inheritdoc />
        public override long Length => declaredLength;

        /// <summary>Proves that oversized metadata was rejected without attempting a payload read.</summary>
        public bool ReadAttempted { get; private set; }

        /// <inheritdoc />
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            ReadAttempted = true;
            return base.ReadAsync(buffer, cancellationToken);
        }
    }

    /// <summary>Expires an exchange after its payload read to exercise the post-deserialization deadline check deterministically.</summary>
    private sealed class CancelAfterReadStream(byte[] bytes, CancellationTokenSource deadline) : MemoryStream(bytes)
    {
        /// <inheritdoc />
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            ValueTask<int> result = base.ReadAsync(buffer, cancellationToken);
            if (Position == Length)
            {
                deadline.Cancel();
            }

            return result;
        }
    }

    private static TrialPlan[] MeasurementFixtureSchedule() => MeasurementReport.CreateSchedule(
        Workloads.CreateCases().ToDictionary(workload => workload.RowKey, workload => new MeasurementSettings().InitialCount(workload)), 3);

    private static (FrozenMeasurement Frozen, TrialRecord[] Trials) MeasurementFixture()
    {
        TrialPlan[] schedule = MeasurementFixtureSchedule();
        FrozenCaseInput[] inputs = Workloads.CreateCases().Select(workload => new FrozenCaseInput(workload,
            new MeasurementSettings().InitialCount(workload), FixtureInput(workload.RowKey), new string('C', 64), new string('D', 64), 1, 1)).ToArray();
        FrozenMeasurement frozen = new(3, MeasurementReport.ContractVersion, "unit-fixture", "fixture-run", new string('A', 64),
            new string('B', 64), new string('C', 64), new(DateTimeOffset.UnixEpoch, 1, 1, 1000), new(), new(), inputs, schedule, [], [],
            new("fixture-environment.json", 1, new string('D', 64)), new(42, 1));
        return (frozen, schedule.Select(MeasurementTrialFixture).ToArray());
    }

    private static EvidenceReference FixtureInput(string rowKey) =>
        new(Path.Combine("fixture-inputs", rowKey + ".json"), 1, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rowKey))));

    private static TrialRecord[] PilotPairFixture(WorkloadCase workload, int operations, int round) =>
        new[] { "baseline", "candidate" }.Select((variant, index) =>
        {
            int ordinal = ((round - 1) * 2) + index + 1;
            string id = "fixture-pilot-" + ordinal.ToString(CultureInfo.InvariantCulture);
            TrialPlan plan = new(ordinal, id, workload.RowKey, variant, round, operations, "pilot", true,
                Path.Combine("calibration", workload.RowKey, id));
            TrialRecord trial = MeasurementTrialFixture(plan);
            return trial with
            {
                FrozenSha256 = null, Inputs = FixtureInput(workload.RowKey + "-" + operations.ToString(CultureInfo.InvariantCulture)),
                Batch = WindowWithSamples(trial.Batch!, 20, 20),
            };
        }).ToArray();

    private static TrialRecord RetainedCoverageFixture(TrialRecord pilot)
    {
        string[] errors = MeasurementReport.ValidateTrial(pilot);
        Assert.NotEmpty(errors);
        Assert.All(errors, error => Assert.StartsWith("coverage:", error, StringComparison.Ordinal));
        return pilot with { Status = "coverage-insufficient", Failure = string.Join("; ", errors) };
    }

    private static TrialRecord MeasurementTrialFixture(TrialPlan plan)
    {
        WorkloadCase workload = Workloads.CreateCases().Single(item => item.RowKey == plan.RowKey);
        long clock = plan.Ordinal * 100000L;
        DateTimeOffset started = DateTimeOffset.Parse("2026-01-01T00:00:00+00:00", CultureInfo.InvariantCulture).AddMinutes(plan.Ordinal);
        int pid = 10000 + plan.Ordinal;
        long processStart = started.AddMilliseconds(1).UtcTicks;
        BatchWindow work = WindowFixture("work", plan.Operations, pid, processStart, clock + 9000, true);
        Footprint Point(string name, long timestamp) => new(name, name.EndsWith("-forced", StringComparison.Ordinal), pid, timestamp, 1000,
            100000, 200000, new(timestamp, 80000, 90000, 1000, 100000, 1),
            FixtureCounters(timestamp + 1));
        Footprint[] checkpoints = [
            Point("idle-natural", clock + 3000), Point("idle-forced", clock + 4000),
            Point("loaded-natural", clock + 7000), Point("loaded-forced", clock + 8000),
            Point("post-immediate", work.Server.End.Timestamp + 10),
            Point("post-settled", work.Server.End.Timestamp + 1010),
            Point("post-forced", work.Server.End.Timestamp + 2010),
        ];
        work = work with { Server = work.Server with { PostWork = checkpoints[4..] }, EndAcknowledged = checkpoints[^1].Timestamp + 10 };
        ResourceBinding? original = workload.CaseId.EndsWith("-update", StringComparison.Ordinal)
            ? new(workload.CaseId == "patient-update" ? "Patient" : "Observation", "fixture-id", "1", started) : null;
        return new()
        {
            Origin = "unit-fixture", Plan = plan, RunId = "fixture-run", PreparedSha256 = new string('A', 64),
            FrozenSha256 = new string('B', 64), Inputs = FixtureInput(plan.RowKey), Started = new(started, clock, clock, 1000),
            Finished = new(started.AddSeconds(30), clock + 30000, clock + 30000, 1000),
            Parent = new(43, 1), ProcessId = pid, ProcessStartUtcTicks = processStart,
            Status = "passed", SealsBeforeAndAfter = true, CleanShutdown = true, OraclePassed = true,
            ValidatedSeedWrites = workload.PatientCount + workload.ObservationCount,
            ValidatedSeedReads = workload.PatientCount + workload.ObservationCount,
            Warmup = new(128, 128, 128, workload.CaseId.EndsWith("-create", StringComparison.Ordinal) ? 128 : 0,
                workload.CaseId == "observation-update" ? 1 : 0, workload.CaseId == "subscription-receive" ? 128 : 0,
                original, original is null ? null : original with { VersionId = "129" },
                original is null ? null : original with { VersionId = workload.CaseId == "observation-update" ? "130" : "129" }),
            Batch = work, Checkpoints = checkpoints,
            Controls = [WindowFixture("idle-control", 0, pid, processStart, clock + 1000, true),
                WindowFixture("loaded-control", 0, pid, processStart, clock + 5000, true)],
        };
    }

    private static BatchWindow WindowFixture(string name, int count, int pid, long processStart, long timestamp, bool sampling)
    {
        long start = timestamp + 3;
        long end = start + Math.Max(count * 2, 1000);
        CounterReading begin = FixtureCounters(timestamp + 1);
        CounterReading finish = FixtureCounters(end + 2);
        ManagedSample[] managed = sampling ? Enumerable.Range(0, 10).Select(index =>
            new ManagedSample(start + 5 + (index * 10), 80000 + index, 90000, 1000, 100000, 1)).ToArray() : [];
        ProcessMemorySample[] process = sampling ? Enumerable.Range(0, 10).Select(index =>
            new ProcessMemorySample(pid, processStart, start + 5 + (index * 10), 100000 + index, 200000 + index, 1000)).ToArray() : [];
        WindowEvidence server = new("GC-process-start", pid, processStart, 1000, sampling, ProbeProtocol.SampleCapacity,
            ProbeProtocol.SampleMilliseconds, begin, finish, managed, 0, 0, false, []);
        return new(name, 1000, timestamp, timestamp + 2, start, end, end + 1, end + 3, count, count, count,
            Enumerable.Range(0, count).Select(index => new OperationTiming(129 + index, start + (index * 2),
                start + (index * 2) + 1, 1, true)).ToArray(), server, process, 0, 0, null);
    }

    private static BatchWindow WindowWithSamples(BatchWindow window, int managedCount, int processCount) => window with
    {
        Server = window.Server with
        {
            Samples = Enumerable.Range(0, managedCount).Select(index => window.Server.Samples[0] with
            {
                Timestamp = window.ClientStart + 5 + (index * 10),
            }).ToArray(),
        },
        ProcessSamples = Enumerable.Range(0, processCount).Select(index => window.ProcessSamples[0] with
        {
            Timestamp = window.ClientStart + 5 + (index * 10),
        }).ToArray(),
    };

    private static CounterReading FixtureCounters(long timestamp) => new(
        timestamp, timestamp * 1000, (int)(timestamp / 100), (int)(timestamp / 200), (int)(timestamp / 500),
        timestamp * 10, timestamp * 100);

    private static ResponseEvidence ResourceResponse(byte[] content, string id, int status, DateTimeOffset storedAt, string version = "1")
    {
        JsonObject resource = JsonNode.Parse(content)!.AsObject();
        resource["id"] = id;
        resource["meta"] ??= new JsonObject();
        resource["meta"]!["versionId"] = version;
        resource["meta"]!["lastUpdated"] = storedAt.ToString("O", CultureInfo.InvariantCulture);
        (ClockObservation started, ClockObservation completed) = FixtureClocks(storedAt.AddTicks(-1), storedAt.AddTicks(1));
        return new(status, new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["Content-Type"] = ["application/fhir+json; charset=utf-8"],
            ["ETag"] = ["W/\"" + version + "\""],
            ["Last-Modified"] = [storedAt.UtcDateTime.ToString("r", CultureInfo.InvariantCulture)],
            ["Location"] = ["http://127.0.0.1:58260/fhir/perf/" + resource["resourceType"]!.GetValue<string>() + "/" + id],
        }, JsonSerializer.SerializeToUtf8Bytes(resource), started, completed);
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

        (ClockObservation started, ClockObservation completed) = FixtureClocks(now.AddTicks(-1), now.AddTicks(1));
        return new(uri, new ResponseEvidence(200,
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase) { ["Content-Type"] = ["application/fhir+json"] },
            JsonSerializer.SerializeToUtf8Bytes(bundle), started, completed));
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
        RunId = "unit-fixture-v3", OutputDirectory = ExperimentRunner.DataRoot + @"\unit-fixture-v3",
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
        ToolSourceRevision = ExperimentRunner.CandidateRevision, ToolSourceDescription = "unit fixture only; never live execution evidence",
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
        (ClockObservation started, ClockObservation completed) = FixtureClocks(now.AddTicks(-1), now.AddTicks(1));
        return new ResponseEvidence(201, new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["Content-Type"] = ["application/fhir+json; charset=utf-8"],
            ["ETag"] = ["W/\"1\""], ["Last-Modified"] = [now.UtcDateTime.ToString("r", CultureInfo.InvariantCulture)],
            ["Location"] = ["http://127.0.0.1:58260/fhir/perf/Observation/server-assigned-1"],
        }, Encoding.UTF8.GetBytes(resource.ToJsonString()), started, completed);
    }

    private static ResponseEvidence ObserveResponse(
        ResponseEvidence response, DateTimeOffset started, DateTimeOffset completed, long start, long end)
    {
        ScriptedClock clock = new([start, start + 2, end - 2, end], [started, completed], 10000000);
        return response with { Started = ClockObservation.Capture(clock), Completed = ClockObservation.Capture(clock) };
    }

    /// <summary>Only returns supplied clock readings; it never sleeps, sets system time or runs a timer.</summary>
    private sealed class ScriptedClock(long[] timestamps, DateTimeOffset[] observations, long frequency) : TimeProvider
    {
        private readonly Queue<long> _timestamps = new(timestamps);
        private readonly Queue<DateTimeOffset> _observations = new(observations);

        /// <inheritdoc/>
        public override long TimestampFrequency => frequency;
        /// <inheritdoc/>
        public override long GetTimestamp() => _timestamps.Dequeue();
        /// <inheritdoc/>
        public override DateTimeOffset GetUtcNow() => _observations.Dequeue();
    }

    private static (ClockObservation Started, ClockObservation Completed) FixtureClocks(DateTimeOffset started, DateTimeOffset completed)
    {
        Assert.True(completed > started);
        long duration = (completed - started).Ticks;
        long end = Interlocked.Add(ref _fixtureTimestamp, duration + 10);
        return (new(started, end - duration, end - duration, TimeSpan.TicksPerSecond),
            new(completed, end, end, TimeSpan.TicksPerSecond));
    }

    private static ReceiptExpectation FixtureReceiptExpectation(NotificationInput notification, DateTimeOffset started, DateTimeOffset completed)
    {
        (ClockObservation first, ClockObservation last) = FixtureClocks(started, completed);
        return new(notification, first, last);
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
