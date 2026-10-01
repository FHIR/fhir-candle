// <copyright file="ExperimentRunner.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO.Compression;
using System.IO.Pipes;
using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml;
using System.Xml.Linq;
using FhirCandle.Performance.Probe;

namespace FhirCandle.Performance;

/// <summary>Exact inputs and common bounds for one append-only experiment directory.</summary>
public sealed record ExperimentManifest
{
    /// <summary>The seed-manifest schema version.</summary>
    public int SchemaVersion { get; init; } = 1;
    /// <summary>The protocol cannot change within a run.</summary>
    public string ProtocolVersion { get; init; } = ProbeProtocol.Version;
    /// <summary>A unique evidence-directory name, never an artifact-directory name.</summary>
    public required string RunId { get; init; }
    /// <summary>The ignored output directory for this run only.</summary>
    public required string OutputDirectory { get; init; }
    /// <summary>The exact requested local report path, reserved for a later phase.</summary>
    public required string ReportPath { get; init; }
    /// <summary>Published release input paths and immutable pins.</summary>
    public required BaselineInput Baseline { get; init; }
    /// <summary>Separately built, sealed request-time application and retained provenance.</summary>
    public required CandidateInput Candidate { get; init; }
    /// <summary>The same explicit host and actual runtime patches for both variants.</summary>
    public RuntimeSettings Runtime { get; init; } = new();
    /// <summary>The identical net8.0 probe DLL injected into all children.</summary>
    public required string ProbePath { get; init; }
    /// <summary>Tool source revision, distinct from the measured application's revision.</summary>
    public required string ToolSourceRevision { get; init; }
    /// <summary>Describes uncommitted tooling, or the exact tool build; bytes are sealed separately.</summary>
    public required string ToolSourceDescription { get; init; }
    /// <summary>One recorded local port for all sequential children.</summary>
    public int Port { get; init; } = 58260;
    /// <summary>Only this stable tenant is allowed.</summary>
    public string Tenant { get; init; } = "perf";
    /// <summary>Exactly one HTTP worker is supported.</summary>
    public int HttpWorkers { get; init; } = 1;
    /// <summary>External, already-authorized isolation, required because the host binds wildcard addresses.</summary>
    public required IsolationSettings Isolation { get; init; }
    /// <summary>Common finite deadlines, including the later phase's frozen measurement defaults.</summary>
    public ExperimentBounds Bounds { get; init; } = new();
}

/// <summary>The published artifact is acquired, never silently rebuilt.</summary>
public sealed record BaselineInput
{
    /// <summary>Exact package ID.</summary>
    public string PackageId { get; init; } = "fhir-candle";
    /// <summary>Exact released version.</summary>
    public string PackageVersion { get; init; } = "2025.611.1422";
    /// <summary>Published repository revision.</summary>
    public string SourceRevision { get; init; } = ExperimentRunner.BaselineRevision;
    /// <summary>Expected actual SDK package cohort; model PE names come from sealed dependency metadata.</summary>
    public string FirelyPackageVersion { get; init; } = "5.11.4";
    /// <summary>Only the pinned NuGet payload URL is accepted.</summary>
    public string PackageUrl { get; init; } = ExperimentRunner.PackageUrl;
    /// <summary>Cached archive location outside the unique run directory.</summary>
    public required string ArchivePath { get; init; }
    /// <summary>Unchanged extracted package layout, outside the run directory.</summary>
    public required string ArtifactDirectory { get; init; }
}

/// <summary>A caller-owned build, not arbitrary or moving application bin contents.</summary>
public sealed record CandidateInput
{
    /// <summary>Request-time application revision.</summary>
    public string SourceRevision { get; init; } = ExperimentRunner.CandidateRevision;
    /// <summary>Expected SDK package cohort.</summary>
    public string FirelyPackageVersion { get; init; } = "6.2.1";
    /// <summary>Sealed net8 output and all required adjacent assets.</summary>
    public required string ArtifactDirectory { get; init; }
    /// <summary>Relative managed application entry point.</summary>
    public string EntryPointRelativePath { get; init; } = "fhir-candle.dll";
    /// <summary>Retained candidate-build.json produced by the independent builder.</summary>
    public required string BuildProvenancePath { get; init; }
}

/// <summary>Runtime selection is explicit and cannot roll forward to another major or patch.</summary>
public sealed record RuntimeSettings
{
    /// <summary>The pinned installed dotnet host.</summary>
    public string DotnetHostPath { get; init; } = @"C:\Program Files\dotnet\dotnet.exe";
    /// <summary>Actual Microsoft.NETCore.App version.</summary>
    public string RuntimeVersion { get; init; } = "8.0.30";
    /// <summary>Actual Microsoft.AspNetCore.App version.</summary>
    public string AspNetCoreVersion { get; init; } = "8.0.30";
    /// <summary>Process architecture, checked in each child.</summary>
    public string Architecture { get; init; } = "X64";
    /// <summary>Common GC mode.</summary>
    public bool ServerGc { get; init; } = true;
    /// <summary>Common concurrent GC setting.</summary>
    public bool ConcurrentGc { get; init; } = true;
    /// <summary>Common tiered compilation setting.</summary>
    public bool TieredCompilation { get; init; } = true;
    /// <summary>Common tiered PGO setting.</summary>
    public bool TieredPgo { get; init; } = true;
    /// <summary>Common ReadyToRun setting.</summary>
    public bool ReadyToRun { get; init; } = true;
}

/// <summary>Evidence of existing controls; the tool does not change firewall, policy or machine configuration.</summary>
public sealed record IsolationSettings(bool Authorized, string Description, string EvidencePath);

/// <summary>Common bounds for each fresh case process and subsequent measured trials.</summary>
public sealed record ExperimentBounds
{
    /// <summary>Startup/readiness deadline.</summary>
    public int StartupSeconds { get; init; } = 120;
    /// <summary>Entire corpus/setup/challenge deadline.</summary>
    public int SetupSeconds { get; init; } = 600;
    /// <summary>Future measured-batch deadline; the current preflight is entirely untimed.</summary>
    public int BatchSeconds { get; init; } = 60;
    /// <summary>Complete HTTP or pipe exchange deadline.</summary>
    public int RequestSeconds { get; init; } = 30;
    /// <summary>Future fixed settling interval.</summary>
    public int SettlingMilliseconds { get; init; } = 1000;
    /// <summary>Graceful shutdown, followed by bounded exact-child cleanup.</summary>
    public int ShutdownSeconds { get; init; } = 15;
    /// <summary>Future initial sample cadence, not evidence of sampler coverage.</summary>
    public int SampleMilliseconds { get; init; } = 10;
}

/// <summary>A file's immutable relative identity. Paths are relative to the named inventory root.</summary>
public sealed record FileSeal(string RelativePath, long Length, string Sha256);

/// <summary>Retained independent clean-detached-checkout build provenance supplied by the caller.</summary>
public sealed record CandidateBuildProvenance(
    int SchemaVersion,
    string SourceRevision,
    string SourceDirectory,
    bool DetachedHead,
    string StatusBefore,
    string StatusAfter,
    string Command,
    string WorkingDirectory,
    string BuildSdkVersion,
    DateTimeOffset StartedUtc,
    DateTimeOffset FinishedUtc,
    int ExitCode,
    string BuildLogPath,
    string BuildLogSha256,
    string ArtifactDirectory,
    string EntryPointRelativePath,
    FileSeal[] Files);

/// <summary>Versions and module identities read from PE metadata without loading application assemblies.</summary>
public sealed record AssemblyFile(
    string RelativePath, string Name, string AssemblyVersion, string FileVersion,
    string InformationalVersion, string Mvid, string TargetFramework);

/// <summary>Dependency metadata used to correlate loaded bytes with package, not just assembly, versions.</summary>
public sealed record DependencyIdentity(string Name, string Version, string Type, string Sha512, string[] RuntimeAssets);

/// <summary>Complete artifact identity established before any child execution.</summary>
public sealed record ArtifactIdentity(
    string Variant, string SourceRevision, string FirelyPackageVersion, string RootDirectory,
    string EntryPointRelativePath, FileSeal[] Files, AssemblyFile[] Assemblies, DependencyIdentity[] Dependencies);

/// <summary>Installed common framework files and dependency metadata.</summary>
public sealed record FrameworkIdentity(
    string Name, string Version, string RootDirectory, FileSeal[] Files,
    AssemblyFile[] Assemblies, DependencyIdentity[] Dependencies);

/// <summary>The immutable prepare-to-preflight handoff; it contains no accepted measured trials.</summary>
public sealed record PreparedExperiment(
    int SchemaVersion, string ProtocolVersion, string RunId, string ManifestSha256,
    ArtifactIdentity Baseline, ArtifactIdentity Candidate, FrameworkIdentity[] Frameworks,
    Dictionary<string, FileSeal> ToolFiles, FileSeal DotnetHost, string ProbeSha256,
    FileSeal CandidateProvenance, FileSeal CandidateBuildLog, FileSeal IsolationEvidence,
    FileSeal Archive, FileSeal[] FixtureFiles, WorkloadPreparation Workload, DateTimeOffset PreparedUtc);

/// <summary>One frozen request's method, address, preference and exact input byte identity.</summary>
public sealed record WorkloadInputSeal(int Number, string Method, string RelativeUrl, string? Prefer, int BodyBytes, string BodySha256);

/// <summary>Provenance for a complete corpus and its dependency-safe PUT order.</summary>
public sealed record CorpusFixture(
    string Key, string FhirVersion, int PatientCount, int ObservationCount, string FixtureFile,
    WorkloadInputSeal[] SeedRequests, string RequestOrderSha256, long PayloadBytes);

/// <summary>A complete, untimed case fixture rather than a measurement configuration.</summary>
public sealed record CaseFixture(
    WorkloadCase Case, string CorpusKey, string FixtureFile, int LogicalOperations,
    WorkloadInputSeal[] Requests, string RequestOrderSha256, long PayloadBytes);

/// <summary>The sealed full-matrix workload ledger, independent of the probe protocol version.</summary>
public sealed record WorkloadPreparation(
    string ContractVersion, string Algorithm, int Seed, int PreflightOperations,
    CorpusFixture[] Corpora, CaseFixture[] Cases, SuccessfulStatusProfile[] StatusProfiles);

/// <summary>One loaded assembly's correlation to sealed bytes and declared dependency ownership.</summary>
public sealed record AssemblyCorrelation(AssemblySnapshot Loaded, string Origin, string Dependency, string DependencyVersion, string DependencySha512);

/// <summary>Acquires sealed inputs and owns bounded, strictly sequential full-corpus preflight children.</summary>
public sealed class ExperimentRunner
{
    /// <summary>The published source commit, never substituted by another baseline.</summary>
    public const string BaselineRevision = "3f1b73e6bde167a019505ac55ca2aa18e0860e8d";
    /// <summary>The application revision pinned when the request was made.</summary>
    public const string CandidateRevision = "21fcb2e7ce7c44de9bc5f12b6af68bee06e90d9b";
    /// <summary>The exact released archive.</summary>
    public const string PackageUrl = "https://api.nuget.org/v3-flatcontainer/fhir-candle/2025.611.1422/fhir-candle.2025.611.1422.nupkg";
    /// <summary>The one authorized report target; untimed preflight never writes it.</summary>
    public const string RequiredReportPath = @"C:\ai\git\fhir-candle\scratch\1001-01\performance.md";
    /// <summary>All runtime evidence remains beneath the ignored experiment root.</summary>
    public const string DataRoot = @"C:\ai\git\fhir-candle\scratch\1001-01\performance-data";

    private const string RegistrationUrl = "https://api.nuget.org/v3/registration5-gz-semver2/fhir-candle/2025.611.1422.json";
    private const string CatalogUrl = "https://api.nuget.org/v3/catalog0/data/2025.06.11.14.31.45/fhir-candle.2025.611.1422.json";
    private const string PublishedSha512 = "8+BGKz3nKhM+zlxU90j3O/IHJlkHoSGZI7R3TvGt1i583IVI7ayOWsizncms8+KUoSYsO2sbWt35Ulah4pEfDg==";
    private const int MaximumHttpBytes = 4 * 1024 * 1024;
    private const long MaximumPackageBytes = 256L * 1024 * 1024;
    private static readonly JsonSerializerOptions _json = new(ProbeProtocol.JsonOptions) { WriteIndented = true };
    private readonly ExperimentManifest _manifest;
    private readonly byte[] _manifestBytes;
    private OwnedChild? _activeChild;
    private string? _preparedSha256;

    private ExperimentRunner(ExperimentManifest manifest, byte[] manifestBytes)
    {
        _manifest = manifest;
        _manifestBytes = manifestBytes;
    }

    /// <summary>Reads a bounded, strict seed manifest; does not build, acquire or launch anything.</summary>
    public static async Task<ExperimentRunner> FromManifestAsync(string path, CancellationToken cancellationToken)
    {
        Absolute(path);
        byte[] bytes = await ReadBoundedFileAsync(path, 1024 * 1024, cancellationToken);
        ExperimentManifest manifest = JsonSerializer.Deserialize<ExperimentManifest>(bytes, _json)
            ?? throw new InvalidDataException("Missing experiment manifest.");
        ValidateManifest(manifest);
        return new ExperimentRunner(manifest, bytes);
    }

    /// <summary>Pure pin/path/protocol validation, also exercised by negative-control tests.</summary>
    public static void ValidateManifest(ExperimentManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        Require(manifest.SchemaVersion == 1 && manifest.ProtocolVersion == ProbeProtocol.Version, "Unsupported manifest/protocol version.");
        Require(manifest.RunId.Length is >= 3 and <= 64 &&
            manifest.RunId.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_'), "Unsafe run ID.");
        Absolute(manifest.OutputDirectory);
        Absolute(manifest.ReportPath);
        Require(SamePath(manifest.OutputDirectory, DataRoot + "\\" + manifest.RunId), "Run output must match its unique ID under performance-data.");
        Require(SamePath(manifest.ReportPath, RequiredReportPath), "Wrong report path.");
        Require(manifest.Baseline.PackageId == "fhir-candle" && manifest.Baseline.PackageVersion == "2025.611.1422" &&
            manifest.Baseline.SourceRevision == BaselineRevision && manifest.Baseline.FirelyPackageVersion == "5.11.4" &&
            manifest.Baseline.PackageUrl == PackageUrl, "Wrong released artifact identity.");
        Require(manifest.Candidate.SourceRevision == CandidateRevision && manifest.Candidate.FirelyPackageVersion == "6.2.1" &&
            manifest.Candidate.EntryPointRelativePath == "fhir-candle.dll", "Wrong candidate artifact identity.");
        Require(manifest.Runtime.DotnetHostPath == @"C:\Program Files\dotnet\dotnet.exe" &&
            manifest.Runtime.RuntimeVersion == "8.0.30" && manifest.Runtime.AspNetCoreVersion == "8.0.30" &&
            manifest.Runtime.Architecture == "X64", "Wrong host, runtime patch or architecture; roll-forward is forbidden.");
        Require(manifest.Tenant == "perf" && manifest.HttpWorkers == 1 && manifest.Port is >= 1024 and <= 65535,
            "Expected one perf tenant, one HTTP worker and a non-privileged port.");
        Require(manifest.ToolSourceRevision.Length == 40 && manifest.ToolSourceRevision.All(Uri.IsHexDigit) &&
            !string.IsNullOrEmpty(manifest.ToolSourceDescription), "Missing separate tool source identity.");
        foreach (string path in new[] { manifest.Baseline.ArchivePath, manifest.Baseline.ArtifactDirectory,
            manifest.Candidate.ArtifactDirectory, manifest.Candidate.BuildProvenancePath, manifest.ProbePath, manifest.Isolation.EvidencePath })
        {
            Absolute(path);
            Require(!Within(path, manifest.OutputDirectory), "Artifact inputs must be separate from the unique run output.");
        }

        Require(Within(manifest.Baseline.ArtifactDirectory, DataRoot) &&
            Within(manifest.Baseline.ArchivePath, DataRoot) && Within(manifest.Candidate.ArtifactDirectory, DataRoot),
            "Artifacts must remain in the ignored experiment input area.");
        Require(!Within(manifest.Baseline.ArtifactDirectory, manifest.Candidate.ArtifactDirectory) &&
            !Within(manifest.Candidate.ArtifactDirectory, manifest.Baseline.ArtifactDirectory) &&
            !Within(manifest.OutputDirectory, manifest.Baseline.ArtifactDirectory) &&
            !Within(manifest.OutputDirectory, manifest.Candidate.ArtifactDirectory) &&
            !Within(manifest.Baseline.ArchivePath, manifest.Baseline.ArtifactDirectory) &&
            !Within(manifest.Candidate.BuildProvenancePath, manifest.Candidate.ArtifactDirectory),
            "Artifact roots, archive and provenance must not overlap.");
        Require(manifest.Isolation.Authorized && !string.IsNullOrEmpty(manifest.Isolation.Description),
            "Existing authorized network isolation is required; --url does not constrain listener interfaces.");
        ExperimentBounds bounds = manifest.Bounds;
        Require(bounds.StartupSeconds is >= 1 and <= 300 && bounds.SetupSeconds is >= 1 and <= 600 &&
            bounds.BatchSeconds is >= 1 and <= 120 && bounds.RequestSeconds is >= 1 and <= 60 &&
            bounds.ShutdownSeconds is >= 1 and <= 30 && bounds.SettlingMilliseconds == 1000 && bounds.SampleMilliseconds == 10 &&
            bounds.StartupSeconds + bounds.SetupSeconds + (2 * bounds.ShutdownSeconds) < 1200, "Invalid or unbounded experiment limits.");
    }

    /// <summary>Acquires/verifies unchanged inputs and prepares a fresh run without compiling source.</summary>
    public async Task PrepareAsync(CancellationToken cancellationToken)
    {
        RequireWindowsAndUninjectedRunner();
        using CancellationTokenSource preparation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        preparation.CancelAfter(TimeSpan.FromSeconds(_manifest.Bounds.SetupSeconds));
        cancellationToken = preparation.Token;
        Require(!Directory.Exists(_manifest.OutputDirectory) && !File.Exists(_manifest.OutputDirectory),
            "Run directory already exists. Use a fresh run ID; prepare does not overwrite or resume unknown evidence.");
        EnsureNoReparseParents(_manifest.OutputDirectory);
        Directory.CreateDirectory(_manifest.OutputDirectory);
        await WriteBytesNewAsync(Output("manifest.json"), _manifestBytes, cancellationToken);
        await WriteNewAsync(Output("prepare-started.json"), new
        {
            _manifest.RunId, ProtocolVersion = ProbeProtocol.Version, StartedUtc = DateTimeOffset.UtcNow,
            ManifestSha256 = HashBytes(_manifestBytes), MeasuredTrials = 0,
        }, cancellationToken);
        try
        {
            FileSeal isolation = SealFile(_manifest.Isolation.EvidencePath);
            Require(isolation.Length > 0, "Isolation evidence cannot be empty.");
            byte[] provenanceBytes = await ReadBoundedFileAsync(_manifest.Candidate.BuildProvenancePath, 16 * 1024 * 1024, cancellationToken);
            CandidateBuildProvenance provenance = JsonSerializer.Deserialize<CandidateBuildProvenance>(provenanceBytes, _json)
                ?? throw new InvalidDataException("Missing retained candidate build provenance.");
            ValidateCandidateProvenance(provenance);
            VerifyFiles(_manifest.Candidate.ArtifactDirectory, provenance.Files);
            ArtifactIdentity candidate = InspectArtifact("candidate", _manifest.Candidate.SourceRevision, "6.2.1",
                _manifest.Candidate.ArtifactDirectory, _manifest.Candidate.EntryPointRelativePath);
            ArtifactIdentity baseline = await AcquireBaselineAsync(cancellationToken);
            FrameworkIdentity[] frameworks = [
                InspectFramework("Microsoft.NETCore.App", _manifest.Runtime.RuntimeVersion),
                InspectFramework("Microsoft.AspNetCore.App", _manifest.Runtime.AspNetCoreVersion),
            ];
            Dictionary<string, FileSeal> tools = SealTools();
            AssemblyFile probe = ReadAssemblyFile(_manifest.ProbePath, Path.GetFileName(_manifest.ProbePath))
                ?? throw new InvalidDataException("Probe is not a managed assembly.");
            Require(probe.TargetFramework == ".NETCoreApp,Version=v8.0" && probe.Name == "FhirCandle.Performance.Probe",
                "Only the actual net8.0 probe may be injected.");
            WorkloadPreparation workload = await PrepareWorkloadsAsync(cancellationToken);

            PreparedExperiment prepared = new(2, ProbeProtocol.Version, _manifest.RunId, HashBytes(_manifestBytes),
                baseline, candidate, frameworks, tools, SealFile(_manifest.Runtime.DotnetHostPath),
                SealFile(_manifest.ProbePath).Sha256, SealFile(_manifest.Candidate.BuildProvenancePath),
                SealFile(provenance.BuildLogPath), isolation, SealFile(_manifest.Baseline.ArchivePath),
                SealDirectory(Output("fixtures")), workload, DateTimeOffset.UtcNow);
            await WriteNewAsync(Output("candidate-build.json"), provenance, cancellationToken);
            await WriteNewAsync(Output("prepared.json"), prepared, cancellationToken);
        }
        catch (Exception exception)
        {
            await SaveFailureAsync(Output("prepare-failed.json"), exception);
            throw;
        }
    }

    private async Task<WorkloadPreparation> PrepareWorkloadsAsync(CancellationToken token)
    {
        List<CorpusFixture> corpora = [];
        List<CaseFixture> fixtures = [];
        WorkloadCase[] cases = Workloads.CreateCases();
        foreach (string version in new[] { "R4", "R4B", "R5" })
        {
            foreach (int patients in new[] { 100, 1000 })
            {
                token.ThrowIfCancellationRequested();
                WorkloadCorpus corpus = Workloads.CreateCorpus(version, patients);
                CorrectnessOracle.ValidateCorpus(corpus);
                CorpusFixture descriptor = DescribeCorpus(corpus);
                await WriteNewAsync(Output("fixtures", descriptor.FixtureFile), corpus, token);
                corpora.Add(descriptor);
                foreach (WorkloadCase workload in cases.Where(item => item.FhirVersion == version && item.PatientCount == patients))
                {
                    WorkloadRequest[] requests = Workloads.CreateRequests(workload, corpus);
                    CorrectnessOracle.ValidateCaseRequests(workload, corpus, requests, Workloads.PreflightOperations);
                    CaseFixture fixture = DescribeCase(workload, corpus.Key, requests);
                    await WriteNewAsync(Output("fixtures", fixture.FixtureFile), requests, token);
                    fixtures.Add(fixture);
                }
            }
        }

        return new(Workloads.ContractVersion, Workloads.Algorithm, Workloads.Seed, Workloads.PreflightOperations,
            corpora.ToArray(), fixtures.ToArray(),
            [CorrectnessOracle.StatusProfile("baseline"), CorrectnessOracle.StatusProfile("candidate")]);
    }

    private static CorpusFixture DescribeCorpus(WorkloadCorpus corpus)
    {
        WorkloadInputSeal[] inputs = Workloads.SeedResources(corpus).Select((resource, index) =>
            new WorkloadInputSeal(index + 1, "PUT", resource.Identity, "return=representation",
                resource.Body.Length, HashBytes(resource.Body))).ToArray();
        return new(corpus.Key, corpus.FhirVersion, corpus.PatientCount, corpus.ObservationCount,
            Path.Combine("corpora", corpus.Key + ".json"), inputs,
            HashBytes(JsonSerializer.SerializeToUtf8Bytes(inputs, _json)), inputs.Sum(input => (long)input.BodyBytes));
    }

    private static CaseFixture DescribeCase(WorkloadCase workload, string corpusKey, WorkloadRequest[] requests)
    {
        WorkloadInputSeal[] inputs = requests.Select(request => new WorkloadInputSeal(request.Number, request.Method,
            request.RelativeUrl, request.Prefer, request.Body?.Length ?? 0, HashBytes(request.Body ?? []))).ToArray();
        return new(workload, corpusKey, Path.Combine("cases", workload.RowKey + ".json"), requests.Length, inputs,
            HashBytes(JsonSerializer.SerializeToUtf8Bytes(inputs, _json)), inputs.Sum(input => (long)input.BodyBytes));
    }

    private async Task<PreparedInputs> ReadWorkloadsAsync(PreparedExperiment prepared, CancellationToken token)
    {
        Dictionary<string, WorkloadCorpus> corpora = new(StringComparer.Ordinal);
        Dictionary<string, WorkloadRequest[]> requests = new(StringComparer.Ordinal);
        foreach (CorpusFixture fixture in prepared.Workload.Corpora)
        {
            byte[] bytes = await ReadBoundedFileAsync(Output("fixtures", fixture.FixtureFile), 32 * 1024 * 1024, token);
            WorkloadCorpus corpus = JsonSerializer.Deserialize<WorkloadCorpus>(bytes, _json)
                ?? throw new InvalidDataException("Missing complete corpus fixture.");
            CorrectnessOracle.ValidateCorpus(corpus);
            Require(bytes.SequenceEqual(JsonSerializer.SerializeToUtf8Bytes(Workloads.CreateCorpus(
                corpus.FhirVersion, corpus.PatientCount), _json)), "Corpus bytes/order differ from the frozen algorithm.");
            Require(JsonSerializer.Serialize(DescribeCorpus(corpus), _json) == JsonSerializer.Serialize(fixture, _json),
                "Corpus provenance, input hashes or lengths differ.");
            Require(corpora.TryAdd(corpus.Key, corpus), "Duplicate corpus fixture.");
        }

        foreach (CaseFixture fixture in prepared.Workload.Cases)
        {
            Require(corpora.TryGetValue(fixture.CorpusKey, out WorkloadCorpus? corpus), "Case has no complete corpus.");
            byte[] bytes = await ReadBoundedFileAsync(Output("fixtures", fixture.FixtureFile), MaximumHttpBytes, token);
            WorkloadRequest[] operations = JsonSerializer.Deserialize<WorkloadRequest[]>(bytes, _json)
                ?? throw new InvalidDataException("Missing case fixture.");
            CorrectnessOracle.ValidateCaseRequests(fixture.Case, corpus!, operations, Workloads.PreflightOperations);
            Require(bytes.SequenceEqual(JsonSerializer.SerializeToUtf8Bytes(Workloads.CreateRequests(fixture.Case, corpus!), _json)) &&
                JsonSerializer.Serialize(DescribeCase(fixture.Case, corpus!.Key, operations), _json) == JsonSerializer.Serialize(fixture, _json),
                "Case bytes, order, input hashes or lengths differ.");
            Require(requests.TryAdd(fixture.Case.RowKey, operations), "Duplicate case fixture.");
        }

        return new(corpora, requests);
    }

    /// <summary>Runs all seventy-two paired rows in 144 fresh processes, with zero measured trials.</summary>
    public async Task RunPreflightAsync(CancellationToken cancellationToken)
    {
        RequireWindowsAndUninjectedRunner();
        string[] allowed = ["manifest.json", "prepare-started.json", "baseline-registration.json", "baseline-catalog.json",
            "baseline-acquisition.json", "baseline.nuspec", "baseline-tool-settings.xml", "candidate-build.json", "fixtures", "prepared.json"];
        Require(Directory.Exists(_manifest.OutputDirectory) &&
            Directory.GetFileSystemEntries(_manifest.OutputDirectory).All(path => allowed.Contains(Path.GetFileName(path), StringComparer.Ordinal)),
            "Expected only this run's completed preparation. Existing/failed preflight or unknown evidence requires a new ID.");
        Directory.CreateDirectory(Output("preflight"));
        WorkloadCase[] cases = Workloads.CreateCases();
        PreflightAttempt[] attempts = cases.SelectMany(workload => new[] { "baseline", "candidate" }.Select(variant =>
            new PreflightAttempt(0, workload.RowKey, variant, Path.Combine(workload.RowKey, variant),
                "not-attempted", null, null, new CaseProgress(), null)))
            .Select((attempt, index) => attempt with { Ordinal = index + 1 }).ToArray();
        string startedPath = Output("preflight", "started.json");
        EnsureNoReparseParents(startedPath);
        FileStream startRecord = new(startedPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        Exception? failure = null;
        try
        {
            await using (startRecord)
            {
                byte[] started = JsonSerializer.SerializeToUtf8Bytes(new
                {
                    _manifest.RunId, ProtocolVersion = ProbeProtocol.Version, Workloads.ContractVersion,
                    StartedUtc = DateTimeOffset.UtcNow, Machine = Environment.MachineName,
                    OperatingSystem = RuntimeInformation.OSDescription, Architecture = RuntimeInformation.OSArchitecture.ToString(),
                    LogicalProcessors = Environment.ProcessorCount, Processor = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER"),
                    _manifest.ToolSourceRevision, _manifest.ToolSourceDescription,
                    Schedule = attempts.Select(attempt => new { attempt.Ordinal, attempt.RowKey, attempt.Variant, attempt.EvidenceDirectory }),
                    RequiredPairedRows = 72, RequiredArtifactCases = 144, Workloads.PreflightOperations,
                    MeasuredTrials = 0, BenchmarkComplete = false,
                }, _json);
                await startRecord.WriteAsync(started, cancellationToken);
                await startRecord.FlushAsync(cancellationToken);
            }

            byte[] preparedBytes = await ReadBoundedFileAsync(Output("prepared.json"), 32 * 1024 * 1024, cancellationToken);
            _preparedSha256 = HashBytes(preparedBytes);
            PreparedExperiment prepared = JsonSerializer.Deserialize<PreparedExperiment>(preparedBytes, _json)
                ?? throw new InvalidDataException("Missing completed preparation.");
            VerifyPrepared(prepared);
            PreparedInputs inputs = await ReadWorkloadsAsync(prepared, cancellationToken);
            await WriteNewAsync(Output("preflight", "input-seal.json"), new
            {
                PreparedSha256 = _preparedSha256, prepared.ManifestSha256, prepared.ProbeSha256,
                prepared.ToolFiles, Workloads.ContractVersion, prepared.Workload.StatusProfiles,
                SeedWritesPerVariantAcrossMatrix = prepared.Workload.Cases.Sum(fixture =>
                    fixture.Case.PatientCount + fixture.Case.ObservationCount),
                SeedReadsPerVariantAcrossMatrix = prepared.Workload.Cases.Sum(fixture =>
                    fixture.Case.PatientCount + fixture.Case.ObservationCount),
            }, cancellationToken);
            for (int index = 0; index < attempts.Length; index++)
            {
                PreflightAttempt attempt = attempts[index] with { Status = "in-progress", StartedUtc = DateTimeOffset.UtcNow };
                attempts[index] = attempt;
                string directory = Output("preflight", attempt.EvidenceDirectory);
                try
                {
                    Directory.CreateDirectory(directory);
                    cancellationToken.ThrowIfCancellationRequested();
                    VerifyPrepared(prepared);
                    CaseFixture fixture = prepared.Workload.Cases.Single(item => item.Case.RowKey == attempt.RowKey);
                    ArtifactIdentity artifact = attempt.Variant == "baseline" ? prepared.Baseline : prepared.Candidate;
                    await WriteNewAsync(Path.Combine(directory, "attempt.json"), new
                    {
                        attempt.Ordinal, attempt.RowKey, attempt.Variant, attempt.StartedUtc,
                        Fixture = fixture, StatusProfile = CorrectnessOracle.StatusProfile(artifact.Variant),
                        PreparedSha256 = _preparedSha256, Untimed = true, MeasuredTrials = 0,
                    }, cancellationToken);
                    try
                    {
                        await RunCasePreflightAsync(artifact, fixture.Case, inputs.Corpora[fixture.CorpusKey],
                            inputs.Requests[attempt.RowKey], directory, prepared, attempt.Progress, cancellationToken);
                    }
                    finally
                    {
                        VerifyPrepared(prepared);
                    }

                    await WriteNewAsync(Path.Combine(directory, "passed.json"), new
                    {
                        PassedUtc = DateTimeOffset.UtcNow, attempt.RowKey, attempt.Variant,
                        UntimedSemanticsOnly = true, attempt.Progress, MeasuredTrials = 0, BenchmarkComplete = false,
                    }, cancellationToken);
                    attempts[index] = attempt with { Status = "passed", FinishedUtc = DateTimeOffset.UtcNow };
                }
                catch (Exception exception)
                {
                    attempts[index] = attempt with
                    {
                        Status = "failed", FinishedUtc = DateTimeOffset.UtcNow,
                        Failure = exception.GetType().FullName + ": " + exception.Message,
                    };
                    await SaveFailureAsync(Path.Combine(directory, "failed.json"), exception);
                    throw;
                }
            }

            Require(cases.Length == 72 && attempts.Length == 144 && attempts.All(attempt => attempt.Status == "passed" &&
                attempt.Progress.CompletedOperations == Workloads.PreflightOperations &&
                attempt.Progress.ValidatedOperations == Workloads.PreflightOperations) &&
                cases.All(workload => attempts.Where(attempt => attempt.RowKey == workload.RowKey).All(attempt =>
                    attempt.Progress.ValidatedSeedWrites == workload.PatientCount + workload.ObservationCount &&
                    attempt.Progress.ValidatedSeedReads == workload.PatientCount + workload.ObservationCount)) &&
                attempts.All(attempt => attempt.Progress.ProcessId > 0) &&
                attempts.Select(attempt => (attempt.Progress.ProcessId, attempt.Progress.ProcessStartedUtc)).Distinct().Count() == 144,
                "The complete successful 144-process preflight matrix is missing.");
            VerifyPrepared(prepared);
        }
        catch (Exception exception)
        {
            failure = exception;
            await WriteNewAsync(Output("preflight", "failed.json"), new
            {
                FailedUtc = DateTimeOffset.UtcNow, exception.Message, Type = exception.GetType().FullName,
                AttemptedArtifactCases = attempts.Count(attempt => attempt.Status != "not-attempted"),
                SuccessfulArtifactCases = attempts.Count(attempt => attempt.Status == "passed"),
                MeasuredTrials = 0, BenchmarkComplete = false,
            }, CancellationToken.None);
            throw;
        }
        finally
        {
            await WriteNewAsync(Output("preflight", "matrix.json"), new
            {
                Workloads.ContractVersion, RequiredPairedRows = 72, RequiredArtifactCases = 144,
                SuccessfulArtifactCases = attempts.Count(attempt => attempt.Status == "passed"),
                SuccessfulPairedRows = cases.Count(workload =>
                    attempts.Where(attempt => attempt.RowKey == workload.RowKey).All(attempt => attempt.Status == "passed")),
                Rows = cases.Select(workload => new
                {
                    workload.RowKey, workload.CaseId, workload.FhirVersion, workload.PatientCount, workload.ObservationCount,
                    Baseline = attempts.Single(attempt => attempt.RowKey == workload.RowKey && attempt.Variant == "baseline"),
                    Candidate = attempts.Single(attempt => attempt.RowKey == workload.RowKey && attempt.Variant == "candidate"),
                }),
                SemanticGatePassed = failure is null && attempts.All(attempt => attempt.Status == "passed"),
                MeasuredTrials = 0, BenchmarkComplete = false,
            }, CancellationToken.None);
            await WriteNewAsync(Output("preflight", "failures.json"), new
            {
                Failure = failure is null ? null : new { Type = failure.GetType().FullName, failure.Message },
                FailedAttempts = attempts.Where(attempt => attempt.Status == "failed").ToArray(),
                NotAttempted = attempts.Where(attempt => attempt.Status == "not-attempted")
                    .Select(attempt => new { attempt.Ordinal, attempt.RowKey, attempt.Variant }).ToArray(),
                MeasuredTrials = 0, BenchmarkComplete = false,
            }, CancellationToken.None);
        }

        try
        {
            await WriteNewAsync(Output("preflight", "completed.json"), new
            {
                CompletedUtc = DateTimeOffset.UtcNow, Workloads.ContractVersion,
                RequiredPairedRows = 72, SuccessfulPairedRows = 72, RequiredArtifactCases = 144, SuccessfulArtifactCases = 144,
                Workloads.PreflightOperations, WarmupOperations = 0, MeasuredTrials = 0, BenchmarkComplete = false,
                Matrix = "matrix.json", FailureLedger = "failures.json", MinimumFutureMeasuredChildTrials = 432,
                RemainingGates = new[] { "128-operation warm-up/restoration", "paired calibration", "fixed-window metrics",
                    "all measured trials", "complete evidence-only rendering" },
            }, cancellationToken);
        }
        catch (Exception exception)
        {
            await SaveFailureAsync(Output("preflight", "finalization-failed.json"), exception);
            throw;
        }

        Console.WriteLine("Full untimed preflight passed: 72 paired rows, 144 fresh case processes, zero measured trials. Evidence: " +
            Output("preflight", "completed.json"));
    }

    private async Task RunCasePreflightAsync(
        ArtifactIdentity artifact, WorkloadCase workload, WorkloadCorpus corpus, WorkloadRequest[] requests,
        string directory, PreparedExperiment prepared, CaseProgress progress, CancellationToken token)
    {
        OwnedChild child = await LaunchServerAsync(artifact, workload.FhirVersion, directory, prepared, token);
        progress.ProcessId = child.Process.Id;
        progress.ProcessStartedUtc = child.StartedUtc;
        try
        {
            using CancellationTokenSource setup = CancellationTokenSource.CreateLinkedTokenSource(token);
            setup.CancelAfter(TimeSpan.FromSeconds(_manifest.Bounds.SetupSeconds));
            progress.Stage = "initial";
            ProbeSnapshot initial = await CaptureAsync(child, "ready", [], prepared, setup.Token);
            CorrectnessOracle.ValidateState(initial.Tenant, initial.Tenant, 0, 0, 0,
                new Dictionary<string, bool>(StringComparer.Ordinal), child.SourceDirectory);
            SeededCorpus seeded = await SeedAsync(child, corpus, initial, prepared, progress, setup.Token);
            PreparedCase context = await PrepareCaseAsync(child, workload, seeded, requests, initial, prepared, progress, setup.Token);
            CompletedOperation[] responses = await ExecuteCaseAsync(child, context, progress, setup.Token);
            await AuditCaseAsync(child, context, responses, prepared, progress, setup.Token);
            Require(!child.Process.HasExited && !child.StandardOutput.Overflow && !child.StandardError.Overflow,
                "Child exited unexpectedly or exceeded log evidence bounds.");
            setup.Token.ThrowIfCancellationRequested();
            progress.Stage = "audited";
        }
        catch (Exception exception)
        {
            await SaveFailureAsync(Path.Combine(directory, "case-failed.json"), exception);
            throw;
        }
        finally
        {
            progress.TotalHttpRequests = child.AttemptedHttpRequests;
            progress.TotalCompletedResponses = child.CompletedHttpResponses;
            try
            {
                await StopServerAsync(child);
            }
            finally
            {
                await WriteNewAsync(Path.Combine(directory, "case-progress.json"), progress, CancellationToken.None);
                VerifyPrepared(prepared);
            }
        }
    }

    private async Task<SeededCorpus> SeedAsync(
        OwnedChild child, WorkloadCorpus corpus, ProbeSnapshot initial, PreparedExperiment prepared,
        CaseProgress progress, CancellationToken token)
    {
        SuccessfulStatusProfile profile = CorrectnessOracle.StatusProfile(child.Artifact.Variant);
        CorpusResource[] order = Workloads.SeedResources(corpus).ToArray();
        Dictionary<string, ExpectedResource> resources = new(StringComparer.Ordinal);
        List<SeedResourceEvidence> evidence = new(order.Length);
        ResponseEvidence? observed = null;
        string? identity = null;
        bool passed = false;
        await WriteNewAsync(Path.Combine(child.Directory, "seed-started.json"), new
        {
            corpus.Key, Workloads.Algorithm, Workloads.Seed, corpus.PatientCount, corpus.ObservationCount,
            RequiredWrites = order.Length, RequiredReadbacks = order.Length,
            RequestOrder = "All Patient PUTs, all Observation PUTs, then every instance GET in the same frozen order.",
            StartedUtc = DateTimeOffset.UtcNow, Untimed = true,
        }, token);
        try
        {
            progress.Stage = "seed-put";
            foreach (CorpusResource resource in order)
            {
                token.ThrowIfCancellationRequested();
                identity = resource.Identity;
                observed = null;
                progress.AttemptedSeedWrites++;
                observed = await SendAsync(child, HttpMethod.Put, identity, resource.Body, "return=representation", token);
                progress.CompletedSeedWrites++;
                ResourceBinding binding = CorrectnessOracle.ValidateResponse(
                    observed, profile.PutAsCreate, resource.Body, child.BaseUrl, child.StartedUtc);
                Require(resources.TryAdd(identity, new ExpectedResource(resource.Body, binding)), "Duplicate seed identity.");
                evidence.Add(new SeedResourceEvidence(evidence.Count + 1, identity, resource.Body.Length,
                    HashBytes(resource.Body), binding, Digest(observed), null));
                progress.ValidatedSeedWrites++;
            }

            progress.Stage = "seed-readback";
            for (int index = 0; index < order.Length; index++)
            {
                token.ThrowIfCancellationRequested();
                CorpusResource resource = order[index];
                identity = resource.Identity;
                observed = null;
                progress.AttemptedSeedReads++;
                observed = await SendAsync(child, HttpMethod.Get, identity, null, null, token);
                progress.CompletedSeedReads++;
                _ = CorrectnessOracle.ValidateResponse(observed, profile.Read, resource.Body, child.BaseUrl,
                    child.StartedUtc, resources[identity].Binding);
                evidence[index] = evidence[index] with { Readback = Digest(observed) };
                progress.ValidatedSeedReads++;
            }

            Require(progress.ValidatedSeedWrites == corpus.PatientCount + corpus.ObservationCount &&
                progress.ValidatedSeedReads == progress.ValidatedSeedWrites, "Incomplete full-corpus audit.");
            ProbeSnapshot seeded = await CaptureAsync(child, "seeded", [], prepared, token);
            CorrectnessOracle.ValidateState(initial.Tenant, seeded.Tenant, corpus.PatientCount, corpus.ObservationCount, 0,
                new Dictionary<string, bool>(StringComparer.Ordinal), child.SourceDirectory);
            CorrectnessOracle.ValidateReceipt(seeded.Tenant, []);
            passed = true;
            return new(corpus, resources, seeded);
        }
        catch (Exception exception)
        {
            await WriteNewAsync(Path.Combine(child.Directory, "seed-failed.json"), new
            {
                progress.Stage, Identity = identity, ObservedResponse = observed, progress.AttemptedSeedWrites,
                progress.CompletedSeedWrites, progress.ValidatedSeedWrites, progress.AttemptedSeedReads,
                progress.CompletedSeedReads, progress.ValidatedSeedReads,
                FailedUtc = DateTimeOffset.UtcNow, Type = exception.GetType().FullName, exception.Message,
                MeasuredTrials = 0,
            }, CancellationToken.None);
            throw;
        }
        finally
        {
            await WriteNewAsync(Path.Combine(child.Directory, "seed-audit.json"), new
            {
                Passed = passed, corpus.Key, RequiredResources = order.Length,
                progress.AttemptedSeedWrites, progress.CompletedSeedWrites, progress.ValidatedSeedWrites,
                progress.AttemptedSeedReads, progress.CompletedSeedReads, progress.ValidatedSeedReads,
                ValidatedResources = evidence, CompletedUtc = DateTimeOffset.UtcNow,
                EvidencePolicy = "Only actually validated responses are summarized; a failing full response is retained separately.",
                MeasuredTrials = 0,
            }, CancellationToken.None);
        }
    }

    private async Task<PreparedCase> PrepareCaseAsync(
        OwnedChild child, WorkloadCase workload, SeededCorpus seeded, WorkloadRequest[] requests,
        ProbeSnapshot initial, PreparedExperiment prepared, CaseProgress progress, CancellationToken token)
    {
        progress.Stage = "case-prepare";
        CorrectnessOracle.ValidateCaseRequests(workload, seeded.Corpus, requests, Workloads.PreflightOperations);
        SuccessfulStatusProfile profile = CorrectnessOracle.StatusProfile(child.Artifact.Variant);
        Dictionary<string, bool> identities = CaseIdentities(requests, received: false);
        if (workload.CaseId.EndsWith("-update", StringComparison.Ordinal))
        {
            string identity = requests[0].RelativeUrl;
            ExpectedResource existing = seeded.Resources[identity];
            ResponseEvidence read = await SendAndSaveAsync(child, "update-preexisting-read", HttpMethod.Get, identity, null, null, token);
            _ = CorrectnessOracle.ValidateResponse(read, profile.Read, existing.Content, child.BaseUrl, child.StartedUtc, existing.Binding);
        }

        ProbeSnapshot snapshot = await CaptureAsync(child, "prepared", identities.Keys.ToArray(), prepared, token);
        CorrectnessOracle.ValidateState(initial.Tenant, snapshot.Tenant, seeded.Corpus.PatientCount, seeded.Corpus.ObservationCount,
            0, identities, child.SourceDirectory);
        CorrectnessOracle.ValidateReceipt(snapshot.Tenant, []);
        await WriteNewAsync(Path.Combine(child.Directory, "case-prepared.json"), new
        {
            workload.RowKey, seeded.Corpus.Key, LogicalOperations = requests.Length, WarmupOperations = 0,
            StatusProfile = profile, Input = DescribeCase(workload, seeded.Corpus.Key, requests),
            PreparedInventory = "prepared-snapshot.json", ClinicalContentAudit = "seed-audit.json",
            ProbeIdentityChallengeAdded = false, Untimed = true, MeasuredTrials = 0,
        }, token);
        return new(workload, seeded, requests, initial, snapshot, profile);
    }

    private async Task<CompletedOperation[]> ExecuteCaseAsync(
        OwnedChild child, PreparedCase context, CaseProgress progress, CancellationToken token)
    {
        progress.Stage = "case-requests";
        List<CompletedOperation> completed = [];
        foreach (WorkloadRequest request in context.Requests)
        {
            token.ThrowIfCancellationRequested();
            progress.AttemptedOperations++;
            List<HttpExchangeEvidence> exchanges = [];
            HashSet<string> visited = new(StringComparer.Ordinal);
            string? uri = child.BaseUrl + "/" + request.RelativeUrl;
            bool search = request.Method == "GET" && request.RelativeUrl.Contains('?');
            do
            {
                Require(visited.Add(uri) && exchanges.Count <= context.Seeded.Corpus.Resources.Length,
                    "Cyclic or unbounded search continuation.");
                string relative = uri[(child.BaseUrl.Length + 1)..];
                string label = "case-" + request.Number.ToString("D4", CultureInfo.InvariantCulture) +
                    "-page-" + (exchanges.Count + 1).ToString("D4", CultureInfo.InvariantCulture);
                progress.AttemptedCaseHttpRequests++;
                int before = child.CompletedHttpResponses;
                ResponseEvidence response;
                try
                {
                    response = await SendAndSaveAsync(child, label, new HttpMethod(request.Method), relative,
                        request.Body, request.Prefer, token);
                }
                finally
                {
                    progress.CompletedCaseHttpResponses += child.CompletedHttpResponses - before;
                }

                exchanges.Add(new HttpExchangeEvidence(uri, response));
                uri = search ? CorrectnessOracle.NextPageUri(response, uri, child.BaseUrl) : null;
            }
            while (uri is not null);
            completed.Add(new CompletedOperation(request.Number, exchanges.ToArray()));
            progress.CompletedOperations++;
        }

        return completed.ToArray();
    }

    private async Task AuditCaseAsync(
        OwnedChild child, PreparedCase context, CompletedOperation[] completed, PreparedExperiment prepared,
        CaseProgress progress, CancellationToken token)
    {
        progress.Stage = "case-audit";
        Require(completed.Length == context.Requests.Length && progress.CompletedOperations == context.Requests.Length,
            "A partial case cannot change its denominator.");
        Dictionary<string, ExpectedResource> resources = new(context.Seeded.Resources, StringComparer.Ordinal);
        List<ResourceBinding> created = [];
        List<string> readbacks = [];
        List<ReceiptExpectation> receipts = [];
        List<OperationAudit> audits = [];
        bool create = context.Workload.CaseId.EndsWith("-create", StringComparison.Ordinal);
        bool update = context.Workload.CaseId.EndsWith("-update", StringComparison.Ordinal);
        bool hook = context.Workload.CaseId == "subscription-receive";
        for (int index = 0; index < context.Requests.Length; index++)
        {
            token.ThrowIfCancellationRequested();
            WorkloadRequest request = context.Requests[index];
            CompletedOperation operation = completed[index];
            Require(operation.Number == request.Number && operation.Exchanges.Length > 0 &&
                operation.Exchanges[0].Uri == child.BaseUrl + "/" + request.RelativeUrl, "Wrong response/request correspondence.");
            bool search = request.Method == "GET" && request.RelativeUrl.Contains('?');
            Require(search || operation.Exchanges.Length == 1, "Non-search operation has extra responses.");
            ResponseEvidence response = operation.Exchanges[0].Response;
            ResourceBinding[] bindings = [];
            string[] identities = request.ExpectedIdentities;
            if (create)
            {
                ResourceBinding binding = CorrectnessOracle.ValidateResponse(
                    response, context.Profile.Create, request.Body!, child.BaseUrl, child.StartedUtc);
                string identity = binding.ResourceType + "/" + binding.Id;
                Require(resources.TryAdd(identity, new ExpectedResource(request.Body!, binding)),
                    "Create returned an already-existing identity.");
                created.Add(binding);
                readbacks.Add(identity);
                identities = [identity];
                bindings = [binding];
            }
            else if (update)
            {
                ExpectedResource previous = resources[request.RelativeUrl];
                ResourceBinding binding = CorrectnessOracle.ValidateUpdateResponse(response, context.Profile.Update,
                    previous.Content, request.Body!, previous.Binding, child.BaseUrl);
                resources[request.RelativeUrl] = new ExpectedResource(request.Body!, binding);
                readbacks.Add(request.RelativeUrl);
                bindings = [binding];
            }
            else if (search)
            {
                Dictionary<string, ExpectedResource> expected = request.ExpectedIdentities
                    .ToDictionary(identity => identity, identity => resources[identity], StringComparer.Ordinal);
                CorrectnessOracle.ValidateSearch(operation.Exchanges, expected,
                    child.BaseUrl + "/" + request.RelativeUrl, child.BaseUrl, context.Profile.Search);
                bindings = expected.Values.Select(resource => resource.Binding).ToArray();
            }
            else if (hook)
            {
                Require(response.StatusCode == context.Profile.Hook, "Hook status differs from its sealed successful profile.");
                CorrectnessOracle.ValidateHookResponse(response);
                NotificationInput notification = request.Notification ?? throw new InvalidDataException("Missing hook input.");
                receipts.Add(new ReceiptExpectation(notification, response.StartedUtc, response.CompletedUtc));
            }
            else
            {
                ExpectedResource expected = resources[request.RelativeUrl];
                bindings = [CorrectnessOracle.ValidateResponse(response, context.Profile.Read, expected.Content,
                    child.BaseUrl, child.StartedUtc, expected.Binding)];
            }

            audits.Add(new OperationAudit(request.Number, operation.Exchanges.Length,
                operation.Exchanges.Select(exchange => exchange.Response.StatusCode).ToArray(), identities, bindings));
            progress.ValidatedOperations++;
        }

        int readOrdinal = 0;
        foreach (string identity in readbacks.Distinct(StringComparer.Ordinal))
        {
            ExpectedResource expected = resources[identity];
            ResponseEvidence read = await SendAndSaveAsync(child, "audit-read-" + (++readOrdinal).ToString("D4", CultureInfo.InvariantCulture),
                HttpMethod.Get, identity, null, null, token);
            _ = CorrectnessOracle.ValidateResponse(read, context.Profile.Read, expected.Content,
                child.BaseUrl, child.StartedUtc, expected.Binding);
            progress.AuditedReadbacks++;
        }

        List<ResourceBinding> bundles = [];
        foreach (ReceiptExpectation receipt in receipts)
        {
            NotificationInput notification = receipt.Notification;
            ResponseEvidence read = await SendAndSaveAsync(child, "audit-bundle-" + notification.EventNumber.ToString("D4", CultureInfo.InvariantCulture),
                HttpMethod.Get, "Bundle/" + notification.BundleId, null, null, token);
            bundles.Add(CorrectnessOracle.ValidateResponse(read, context.Profile.Read, notification.Body,
                child.BaseUrl, receipt.Earliest));
            progress.AuditedReadbacks++;
        }

        Dictionary<string, bool> identitiesAfter = CaseIdentities(context.Requests, received: hook);
        foreach (ResourceBinding binding in created)
        {
            identitiesAfter.Add(binding.ResourceType + "/" + binding.Id, true);
        }

        WorkloadCorpus corpus = context.Seeded.Corpus;
        int patients = corpus.PatientCount + (context.Workload.CaseId == "patient-create" ? context.Requests.Length : 0);
        int observations = corpus.ObservationCount + (context.Workload.CaseId == "observation-create" ? context.Requests.Length : 0);
        ProbeSnapshot audited = await CaptureAsync(child, "audited", identitiesAfter.Keys.ToArray(), prepared, token);
        CorrectnessOracle.ValidateState(context.Initial.Tenant, audited.Tenant, patients, observations,
            hook ? context.Requests.Length : 0, identitiesAfter, child.SourceDirectory);
        CorrectnessOracle.ValidateReceipt(audited.Tenant, receipts);
        if (create)
        {
            string type = context.Workload.CaseId == "patient-create" ? "Patient" : "Observation";
            string[] initialIds = corpus.Resources.Where(resource => resource.ResourceType == type).Select(resource => resource.Id).ToArray();
            CorrectnessOracle.ValidateCreateGrowth(type, initialIds, context.Prepared.Tenant.ResourceCounts[type],
                audited.Tenant.ResourceCounts[type], context.Requests.Length, created);
            await DeleteCreatedAsync(child, context, created, resources, prepared, progress, token);
        }

        await WriteNewAsync(Path.Combine(child.Directory, "oracle.json"), new
        {
            Passed = true, context.Workload.RowKey, context.Profile, Workloads.ContractVersion,
            LogicalOperations = context.Requests.Length, CompletedOperations = completed.Length,
            HttpRequests = completed.Sum(operation => operation.Exchanges.Length), Operations = audits,
            BundleBindings = bundles, ExpectedReceipts = receipts,
            InitialInventory = "ready-snapshot.json", SeededInventory = "seeded-snapshot.json",
            PreparedInventory = "prepared-snapshot.json", PostWorkInventory = "audited-snapshot.json",
            CleanupInventory = create ? "cleanup-snapshot.json" : null,
            SeedEvidence = "seed-audit.json", WarmupOperations = 0, MeasuredTrials = 0, BenchmarkComplete = false,
        }, token);
    }

    private async Task DeleteCreatedAsync(
        OwnedChild child, PreparedCase context, IReadOnlyList<ResourceBinding> created,
        IReadOnlyDictionary<string, ExpectedResource> resources, PreparedExperiment prepared, CaseProgress progress, CancellationToken token)
    {
        progress.Stage = "create-cleanup-audit";
        Dictionary<string, bool> absent = new(StringComparer.Ordinal);
        for (int index = 0; index < created.Count; index++)
        {
            ResourceBinding binding = created[index];
            string identity = binding.ResourceType + "/" + binding.Id;
            progress.AttemptedCleanupDeletes++;
            int before = child.CompletedHttpResponses;
            ResponseEvidence response;
            try
            {
                response = await SendAndSaveAsync(child, "cleanup-delete-" + (index + 1).ToString("D4", CultureInfo.InvariantCulture),
                    HttpMethod.Delete, identity, null, "return=representation", token);
            }
            finally
            {
                progress.CompletedCleanupDeletes += child.CompletedHttpResponses - before;
            }

            CorrectnessOracle.ValidateDeleteResponse(response, context.Profile.Delete, resources[identity]);
            progress.ValidatedCleanupDeletes++;
            absent.Add(identity, false);
        }

        ProbeSnapshot restored = await CaptureAsync(child, "cleanup", absent.Keys.ToArray(), prepared, token);
        CorrectnessOracle.ValidateState(context.Initial.Tenant, restored.Tenant,
            context.Seeded.Corpus.PatientCount, context.Seeded.Corpus.ObservationCount, 0, absent, child.SourceDirectory);
        CorrectnessOracle.ValidateReceipt(restored.Tenant, []);
    }

    private static Dictionary<string, bool> CaseIdentities(WorkloadRequest[] requests, bool received)
    {
        Dictionary<string, bool> identities = new(StringComparer.Ordinal);
        foreach (WorkloadRequest request in requests)
        {
            foreach (string identity in request.ExpectedIdentities)
            {
                identities[identity] = request.Notification is null || received;
            }

            if (request.Notification is NotificationInput notification)
            {
                identities.Add("Patient/" + notification.ContextId, false);
                identities.Add("Observation/" + notification.FocusId, false);
            }
        }

        Require(identities.Count <= 32, "The fixed case exceeds the probe's declared identity bound.");
        return identities;
    }

    private static ResponseDigest Digest(ResponseEvidence response) => new(response.StatusCode, response.Body.Length,
        HashBytes(response.Body), HashBytes(JsonSerializer.SerializeToUtf8Bytes(response, _json)), response.StartedUtc, response.CompletedUtc);

    private async Task<OwnedChild> LaunchServerAsync(
        ArtifactIdentity artifact, string version, string directory, PreparedExperiment prepared, CancellationToken token)
    {
        Require(_activeChild is null, "Only one owned child may run at a time.");
        Require(!System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners()
            .Any(endpoint => endpoint.Port == _manifest.Port), "The selected port already has a listener.");
        string childRoot = Path.Combine(directory, "child");
        string work = Path.Combine(childRoot, "work");
        string profile = Path.Combine(childRoot, "profile");
        string data = Path.Combine(childRoot, "empty-source");
        string cache = Path.Combine(profile, ".fhir", "packages");
        foreach (string path in new[] { work, profile, data, cache, Path.Combine(profile, "AppData", "Local"),
            Path.Combine(profile, "AppData", "Roaming"), Path.Combine(childRoot, "temp") })
        {
            Directory.CreateDirectory(path);
        }

        string runtimeConfig = Path.Combine(childRoot, "effective.runtimeconfig.json");
        await WriteRuntimeConfigAsync(artifact, runtimeConfig, token);
        using Process parent = Process.GetCurrentProcess();
        ProbeConfiguration configuration = new("candle-perf-" + Guid.NewGuid().ToString("N"),
            parent.Id, parent.StartTime.ToUniversalTime().Ticks, ProbeProtocol.CurrentOwnerSid(),
            Convert.ToHexString(RandomNumberGenerator.GetBytes(32)),
            DateTimeOffset.UtcNow.AddSeconds(_manifest.Bounds.StartupSeconds + _manifest.Bounds.SetupSeconds +
                (2 * _manifest.Bounds.ShutdownSeconds)), _manifest.Bounds.RequestSeconds);
        configuration.Validate();
        NamedPipeServerStream pipe = ProbeProtocol.CreateServer(configuration.PipeName);
        ProcessStartInfo start = new(_manifest.Runtime.DotnetHostPath)
        {
            UseShellExecute = false, WorkingDirectory = work, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true,
        };
        string entryPoint = Path.Combine(artifact.RootDirectory, artifact.EntryPointRelativePath);
        foreach (string argument in new[] { "exec", "--runtimeconfig", runtimeConfig, "--depsfile",
            Path.ChangeExtension(entryPoint, ".deps.json"), "--roll-forward", "Disable", entryPoint,
            "--" + version.ToLowerInvariant(), "perf", "--port", _manifest.Port.ToString(CultureInfo.InvariantCulture),
            "--url", "http://127.0.0.1:" + _manifest.Port.ToString(CultureInfo.InvariantCulture),
            "--max-resources", "0", "--fhir-source", data, "--fhir-package-cache", cache,
            "--use-official-registries", "false", "--max-subscription-minutes", "0",
            "--create-existing-id", "true", "--create-as-update", "true", "--open-browser", "false",
            "--enable-mcp", "false", "--load-examples", "false", "--protect-source", "false" })
        {
            start.ArgumentList.Add(argument);
        }

        start.Environment.Clear();
        Dictionary<string, string> environment = ChildEnvironment(childRoot, profile);
        foreach (KeyValuePair<string, string> pair in environment)
        {
            start.Environment.Add(pair.Key, pair.Value);
        }

        start.Environment.Add("DOTNET_STARTUP_HOOKS", _manifest.ProbePath);
        start.Environment.Add(ProbeConfiguration.EnvironmentVariable, JsonSerializer.Serialize(configuration, ProbeProtocol.JsonOptions));
        Process process = new() { StartInfo = start };
        OwnedChild child = new(process, pipe, configuration, artifact, version, directory, childRoot, profile, data, cache, _manifest.Port);
        _activeChild = child;
        try
        {
            await WriteNewAsync(Path.Combine(directory, "launch.json"), new
            {
                Executable = start.FileName, Arguments = start.ArgumentList.ToArray(), start.WorkingDirectory,
                EnvironmentAllowlist = environment, StartupHook = _manifest.ProbePath, prepared.ProbeSha256,
                ProbeActivationVariable = ProbeConfiguration.EnvironmentVariable,
                ProbeConfiguration = new { configuration.PipeName, configuration.ParentProcessId,
                    configuration.ParentStartTimeUtcTicks, configuration.OwnerSid, configuration.DeadlineUtc, configuration.RequestTimeoutSeconds },
                ActivationNonce = "redacted; child-local and never retained", EffectiveRuntimeConfig = SealFile(runtimeConfig),
                _manifest.Isolation, RequestedUtc = DateTimeOffset.UtcNow,
            }, token);
            Require(process.Start(), "Failed to start the owned application child.");
            child.Started = true;
            child.StartedUtc = process.StartTime.ToUniversalTime();
            child.OutputPump = child.StandardOutput.PumpAsync(process.StandardOutput);
            child.ErrorPump = child.StandardError.PumpAsync(process.StandardError);
            await WriteNewAsync(Path.Combine(directory, "process.json"), new
            {
                ProcessId = process.Id, child.StartedUtc, ExpectedParentProcessId = parent.Id,
            }, token);
            using CancellationTokenSource startup = CancellationTokenSource.CreateLinkedTokenSource(token);
            startup.CancelAfter(TimeSpan.FromSeconds(_manifest.Bounds.StartupSeconds));
            await WaitForProbeConnectionAsync(pipe, process, Path.Combine(directory, "stderr.log"), startup.Token);
            int actualPeer = ProbeProtocol.GetPeerProcessId(pipe, server: true);
            Require(actualPeer == process.Id, "The connected probe pipe belongs to the wrong process.");
            using (CancellationTokenSource handshake = CancellationTokenSource.CreateLinkedTokenSource(startup.Token))
            {
                handshake.CancelAfter(TimeSpan.FromSeconds(_manifest.Bounds.RequestSeconds));
                ProbeHello hello = await ProbeProtocol.ReadAsync<ProbeHello>(pipe, ProbeProtocol.MaximumRequestBytes, handshake.Token);
                ProbeProtocol.ValidateHello(hello, configuration, process.Id, child.StartedUtc.UtcTicks,
                    prepared.ProbeSha256, actualPeer, process.Id, ProbeProtocol.CurrentOwnerSid());
                await ProbeProtocol.WriteAsync(pipe, hello, ProbeProtocol.MaximumRequestBytes, handshake.Token);
                child.Authenticated = true;
                await WriteNewAsync(Path.Combine(directory, "handshake.json"), new
                {
                    hello.ProtocolVersion, hello.ParentProcessId, hello.ParentStartTimeUtcTicks,
                    hello.ChildProcessId, hello.ChildStartTimeUtcTicks, hello.OwnerSid, hello.ProbeSha256,
                    OsVerifiedPeerProcessId = actualPeer, CurrentUserOnly = true, NonceMatched = true,
                }, handshake.Token);
            }

            await WaitForReadyAsync(child, startup.Token);
            return child;
        }
        catch (Exception exception)
        {
            try
            {
                await StopServerAsync(child);
            }
            catch (Exception cleanup)
            {
                throw new AggregateException("Launch and owned-child cleanup failed.", exception, cleanup);
            }

            throw;
        }
    }

    /// <summary>Waits for the owned child's pipe or early exit; process/log cleanup remains the caller's responsibility.</summary>
    public static async Task WaitForProbeConnectionAsync(
        NamedPipeServerStream pipe, Process childProcess, string standardErrorPath, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pipe);
        ArgumentNullException.ThrowIfNull(childProcess);
        using CancellationTokenSource pending = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Task connection = pipe.WaitForConnectionAsync(pending.Token);
        Task exited = childProcess.WaitForExitAsync(pending.Token);
        try
        {
            Task completed = await Task.WhenAny(connection, exited);
            await completed;
            if (completed == exited || childProcess.HasExited)
            {
                throw new InvalidDataException(
                    "Owned child PID " + childProcess.Id.ToString(CultureInfo.InvariantCulture) +
                    " exited before probe connection with exit code " + childProcess.ExitCode.ToString(CultureInfo.InvariantCulture) +
                    ". Retained stderr: " + standardErrorPath);
            }
        }
        finally
        {
            pending.Cancel();
            try
            {
                await Task.WhenAll(connection, exited);
            }
            catch (OperationCanceledException) when (pending.IsCancellationRequested)
            {
                // Observe the losing wait's cancellation before the caller reuses or disposes either handle.
            }
        }
    }

    private Dictionary<string, string> ChildEnvironment(string childRoot, string profile)
    {
        string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        string dotnetRoot = Path.GetDirectoryName(_manifest.Runtime.DotnetHostPath)!;
        RuntimeSettings runtime = _manifest.Runtime;
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["SystemRoot"] = windows, ["WINDIR"] = windows,
            ["PATH"] = dotnetRoot + ";" + Path.Combine(windows, "System32"),
            ["USERPROFILE"] = profile, ["HOME"] = profile, ["HOMEDRIVE"] = Path.GetPathRoot(profile)![..2],
            ["HOMEPATH"] = profile[2..], ["LOCALAPPDATA"] = Path.Combine(profile, "AppData", "Local"),
            ["APPDATA"] = Path.Combine(profile, "AppData", "Roaming"),
            ["TEMP"] = Path.Combine(childRoot, "temp"), ["TMP"] = Path.Combine(childRoot, "temp"),
            ["NUGET_PACKAGES"] = Path.Combine(profile, ".nuget", "packages"), ["DOTNET_CLI_HOME"] = profile,
            ["DOTNET_ROOT"] = dotnetRoot, ["DOTNET_MULTILEVEL_LOOKUP"] = "0",
            ["DOTNET_ROLL_FORWARD"] = "Disable", ["DOTNET_ROLL_FORWARD_TO_PRERELEASE"] = "0",
            ["DOTNET_ENVIRONMENT"] = "Production", ["ASPNETCORE_ENVIRONMENT"] = "Production",
            ["DOTNET_gcServer"] = runtime.ServerGc ? "1" : "0",
            ["DOTNET_gcConcurrent"] = runtime.ConcurrentGc ? "1" : "0",
            ["DOTNET_TieredCompilation"] = runtime.TieredCompilation ? "1" : "0",
            ["DOTNET_TieredPGO"] = runtime.TieredPgo ? "1" : "0",
            ["DOTNET_ReadyToRun"] = runtime.ReadyToRun ? "1" : "0",
            ["Logging__LogLevel__Default"] = "Warning",
            ["Logging__LogLevel__Microsoft.AspNetCore"] = "Warning",
            ["Logging__LogLevel__Microsoft.Hosting.Lifetime"] = "Information",
        };
    }

    private async Task WriteRuntimeConfigAsync(ArtifactIdentity artifact, string destination, CancellationToken token)
    {
        string entryPoint = Path.Combine(artifact.RootDirectory, artifact.EntryPointRelativePath);
        JsonObject source = JsonNode.Parse(await File.ReadAllBytesAsync(
            Path.ChangeExtension(entryPoint, ".runtimeconfig.json"), token))!.AsObject();
        JsonObject document = CreateChildRuntimeConfig(source, _manifest.Runtime);
        await WriteBytesNewAsync(destination, Encoding.UTF8.GetBytes(document.ToJsonString(_json)), token);
    }

    /// <summary>Derives exact framework/GC/tiering settings without changing the sealed source configuration.</summary>
    public static JsonObject CreateChildRuntimeConfig(JsonObject source, RuntimeSettings runtime)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(runtime);
        JsonObject document = (JsonObject)source.DeepClone();
        JsonObject options = document["runtimeOptions"]!.AsObject();
        options["rollForward"] = "Disable";
        options.Remove("applyPatches");
        options.Remove("rollForwardOnNoCandidateFx");
        JsonArray frameworks = options["frameworks"]!.AsArray();
        foreach (JsonNode? node in frameworks)
        {
            JsonObject framework = node!.AsObject();
            framework["version"] = framework["name"]!.GetValue<string>() == "Microsoft.NETCore.App"
                ? runtime.RuntimeVersion : runtime.AspNetCoreVersion;
            framework["rollForward"] = "Disable";
            framework.Remove("applyPatches");
            framework.Remove("rollForwardOnNoCandidateFx");
        }

        JsonObject properties = options["configProperties"]?.AsObject() ?? new JsonObject();
        if (options["configProperties"] is null)
        {
            options["configProperties"] = properties;
        }

        properties["System.GC.Server"] = runtime.ServerGc;
        properties["System.GC.Concurrent"] = runtime.ConcurrentGc;
        properties["System.Runtime.TieredCompilation"] = runtime.TieredCompilation;
        properties["System.Runtime.TieredPGO"] = runtime.TieredPgo;
        return document;
    }

    private async Task WaitForReadyAsync(OwnedChild child, CancellationToken token)
    {
        int attempt = 0;
        while (true)
        {
            token.ThrowIfCancellationRequested();
            Require(!child.Process.HasExited, "Application exited before readiness.");
            attempt++;
            try
            {
                ResponseEvidence response = await SendAndSaveAsync(child, "readiness-" + attempt.ToString("D4", CultureInfo.InvariantCulture),
                    HttpMethod.Get, "metadata", null, null, token);
                if (response.StatusCode == 200)
                {
                    using JsonDocument document = JsonDocument.Parse(response.Body);
                    Require(document.RootElement.GetProperty("resourceType").GetString() == "CapabilityStatement", "Readiness returned the wrong content.");
                    string expectedFhirVersion = child.FhirVersion switch { "R4" => "4.0.1", "R4B" => "4.3.0", "R5" => "5.0.0", _ => throw new InvalidDataException() };
                    Require(document.RootElement.GetProperty("fhirVersion").GetString() == expectedFhirVersion, "HTTP readiness addressed the wrong FHIR version.");
                    return;
                }
            }
            catch (HttpRequestException exception)
            {
                await WriteNewAsync(Path.Combine(child.Directory, "readiness-connection-" + attempt.ToString("D4", CultureInfo.InvariantCulture) + ".json"),
                    new { Utc = DateTimeOffset.UtcNow, exception.Message }, token);
            }

            Require(attempt <= (_manifest.Bounds.StartupSeconds * 4), "Readiness attempt budget exhausted.");
            await Task.Delay(250, token);
        }
    }

    private async Task<ResponseEvidence> SendAndSaveAsync(
        OwnedChild child, string label, HttpMethod method, string relative, byte[]? body, string? prefer, CancellationToken token)
    {
        string uri = child.BaseUrl + "/" + relative;
        DateTimeOffset started = DateTimeOffset.UtcNow;
        await WriteNewAsync(Path.Combine(child.Directory, label + "-request.json"), new
        {
            Method = method.Method, Uri = uri, RequestBody = body, Prefer = prefer, StartedUtc = started,
            Accept = "application/fhir+json", ContentType = body is null ? null : "application/fhir+json",
            AttemptedRequests = 1, Untimed = true, Workloads.ContractVersion,
        }, token);
        ResponseEvidence? observed = null;
        try
        {
            ResponseEvidence evidence = await SendAsync(child, method, relative, body, prefer, token, started);
            observed = evidence;
            await WriteNewAsync(Path.Combine(child.Directory, label + ".json"), new
            {
                Method = method.Method, Uri = uri, RequestBody = body, Prefer = prefer, Response = evidence,
                ResponseSha256 = HashBytes(evidence.Body), ResponseBytes = evidence.Body.Length,
                AttemptedRequests = 1, CompletedResponses = 1, Untimed = true, Workloads.ContractVersion,
            }, token);
            return evidence;
        }
        catch (Exception exception)
        {
            await WriteNewAsync(Path.Combine(child.Directory, label + "-failed.json"), new
            {
                Method = method.Method, Uri = uri, FailedUtc = DateTimeOffset.UtcNow,
                AttemptedRequests = 1, CompletedResponses = observed is null ? 0 : 1, Response = observed,
                exception.Message, Type = exception.GetType().FullName,
            }, CancellationToken.None);
            throw;
        }
    }

    private async Task<ResponseEvidence> SendAsync(
        OwnedChild child, HttpMethod method, string relative, byte[]? body, string? prefer,
        CancellationToken token, DateTimeOffset? startedUtc = null)
    {
        using CancellationTokenSource requestDeadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        requestDeadline.CancelAfter(TimeSpan.FromSeconds(_manifest.Bounds.RequestSeconds));
        using HttpRequestMessage request = new(method, child.BaseUrl + "/" + relative);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/fhir+json"));
        if (prefer is not null)
        {
            request.Headers.Add("Prefer", prefer);
        }

        if (body is not null)
        {
            request.Content = new ByteArrayContent(body);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/fhir+json");
        }

        DateTimeOffset started = startedUtc ?? DateTimeOffset.UtcNow;
        child.AttemptedHttpRequests++;
        using HttpResponseMessage response = await child.Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, requestDeadline.Token);
        await using Stream input = await response.Content.ReadAsStreamAsync(requestDeadline.Token);
        using MemoryStream output = new();
        await CopyBoundedAsync(input, output, MaximumHttpBytes, requestDeadline.Token);
        child.CompletedHttpResponses++;
        Dictionary<string, string[]> headers = response.Headers.Concat(response.Content.Headers)
            .ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
        return new ResponseEvidence((int)response.StatusCode, headers, output.ToArray(), started, DateTimeOffset.UtcNow);
    }

    private async Task<ProbeSnapshot> CaptureAsync(
        OwnedChild child, string label, string[] identities, PreparedExperiment prepared, CancellationToken token)
    {
        using CancellationTokenSource request = CancellationTokenSource.CreateLinkedTokenSource(token);
        request.CancelAfter(TimeSpan.FromSeconds(_manifest.Bounds.RequestSeconds));
        ProbeCommand command = new(++child.Sequence, "snapshot", "perf", identities);
        await ProbeProtocol.WriteAsync(child.Pipe, command, ProbeProtocol.MaximumRequestBytes, request.Token);
        ProbeReply reply = await ProbeProtocol.ReadAsync<ProbeReply>(child.Pipe, ProbeProtocol.MaximumResponseBytes, request.Token);
        await WriteNewAsync(Path.Combine(child.Directory, label + "-snapshot.json"), reply, token);
        Require(reply.Sequence == command.Sequence && !reply.Shutdown && reply.Snapshot is not null, "Invalid/missing stopped probe reply.");
        ProbeSnapshot snapshot = reply.Snapshot!;
        Require(snapshot.ProcessId == child.Process.Id, "Snapshot belongs to the wrong PID.");
        TcpObservation[] network = CaptureTcp(child.Process.Id);
        await WriteNewAsync(Path.Combine(child.Directory, label + "-network.json"), network, token);
        ValidateEffectiveSettings(child, snapshot);
        AssemblyCorrelation[] correlations = ValidateLoadedAssemblies(child, snapshot, prepared);
        Require(network.Any(connection => connection.State == 2 && connection.LocalPort == _manifest.Port),
            "No OS-observed listener owned by the child PID.");
        Require(network.All(connection => connection.LocalPort == _manifest.Port &&
            (connection.State == 2 || IsLoopbackAddress(connection.RemoteAddress))),
            "Unexpected listener, non-loopback peer or outbound child TCP activity. Resolve authorized isolation, not product behavior.");
        await WriteNewAsync(Path.Combine(child.Directory, label + "-assembly-correlation.json"), correlations, token);
        return snapshot;
    }

    private void ValidateEffectiveSettings(OwnedChild child, ProbeSnapshot snapshot)
    {
        Require(snapshot.ProcessId == child.Process.Id && snapshot.RuntimeVersion == "8.0.30" &&
            snapshot.FrameworkDescription == ".NET 8.0.30" && snapshot.Architecture == _manifest.Runtime.Architecture &&
            snapshot.ServerGc == _manifest.Runtime.ServerGc, "Wrong actual child runtime, architecture or GC mode.");
        foreach (KeyValuePair<string, string> setting in ChildEnvironment(child.ChildRoot, child.ProfileDirectory)
            .Where(pair => snapshot.RuntimeEnvironment.ContainsKey(pair.Key)))
        {
            Setting(snapshot.RuntimeEnvironment, setting.Key, setting.Value);
        }

        Require(snapshot.GcConfiguration.Count > 0, "Missing effective runtime GC configuration.");
        Dictionary<string, string?> config = snapshot.Tenant.Configuration;
        Setting(config, "FhirVersion", child.FhirVersion);
        Setting(config, "ControllerName", "perf");
        Setting(config, "BaseUrl", child.BaseUrl);
        Setting(config, "MaxResourceCount", "0");
        Setting(config, "MaxSubscriptionExpirationMinutes", "0");
        Setting(config, "FhirPackageCount", "0");
        foreach (string name in new[] { "SmartRequired", "SmartAllowed", "ProtectLoadedContent" })
        {
            Setting(config, name, "False");
        }

        Setting(config, "AllowExistingId", "True");
        Setting(config, "AllowCreateAsUpdate", "True");
        Setting(config, "SupportNotChanged", child.FhirVersion == "R4" ? "True" : "False");
        Require(string.IsNullOrEmpty(config["Strict"]) || config["Strict"] == "False", "Unexpected strict-mode policy.");
        CorrectnessOracle.ValidateEmptySource(snapshot.Tenant, child.SourceDirectory);
        Require(snapshot.Tenant.TenantNames.SequenceEqual(new[] { "perf" }) &&
            snapshot.Tenant.LoadedPackageDirectives.Length == 0 && snapshot.Tenant.LoadedPackageIds.Length == 0 &&
            snapshot.Tenant.SubscriptionCount == 0 && snapshot.Tenant.TopicCount == 0 &&
            snapshot.Host.CachedPackageCount == 0, "Unexpected tenants/packages/subscriptions.");
        Dictionary<string, string?> host = snapshot.Host.Configuration;
        Setting(host, "PublicUrl", "http://127.0.0.1:" + _manifest.Port.ToString(CultureInfo.InvariantCulture));
        Setting(host, "ListenPort", _manifest.Port.ToString(CultureInfo.InvariantCulture));
        Setting(host, "MaxResourceCount", "0");
        Setting(host, "MaxSubscriptionExpirationMinutes", "0");
        Setting(host, "AllowExistingId", "True");
        Setting(host, "AllowCreateAsUpdate", "True");
        foreach (string name in new[] { "OpenBrowser", "LoadPackageExamples", "EnableMcp", "ProtectLoadedContent",
            "UseOfficialRegistries", "UiDisabled", "HasSenderConfiguration" })
        {
            Setting(host, name, "False");
        }

        foreach (string name in new[] { "PublishedPackages", "CiPackages", "ReferenceImplementation",
            "SmartRequiredTenants", "SmartOptionalTenants", "OpenTelemetryEndpoint", "OpenTelemetryTracesEndpoint",
            "OpenTelemetryMetricsEndpoint", "OpenTelemetryLogsEndpoint" })
        {
            Require(string.IsNullOrEmpty(host[name]), "Unexpected effective activity setting: " + name);
        }

        foreach (string version in new[] { "R4", "R4B", "R5" })
        {
            Setting(host, "Tenants" + version, version == child.FhirVersion ? "perf" : string.Empty);
        }

        Require(SamePath(host["SourceDirectory"] ?? string.Empty, child.SourceDirectory) &&
            SamePath(snapshot.Host.WorkingDirectory, Path.Combine(child.ChildRoot, "work")),
            "Effective source/cwd isolation failed.");
        Require(!string.IsNullOrEmpty(host["FhirCacheDirectory"]) &&
            SamePath(host["FhirCacheDirectory"]!, child.CacheDirectory),
            "Effective package cache is not the common child-owned path. Environment/--fhir-package-cache alone is not proof of isolation.");
        // Windows known-folder APIs may ignore environment profile overrides. Keep the ordinary, common
        // data-protection host behavior, record its location, and never copy key contents into evidence.
        Require(!string.IsNullOrEmpty(snapshot.Host.LocalApplicationData) && !string.IsNullOrEmpty(snapshot.Host.UserProfile),
            "The host's actual known-folder locations were not observed.");
        Require(snapshot.Host.ListenerAddresses.Length > 0, "No live host listener addresses.");
        foreach (string address in snapshot.Host.ListenerAddresses)
        {
            Require(Uri.TryCreate(address.Replace("*", "0.0.0.0", StringComparison.Ordinal), UriKind.Absolute, out Uri? listener) &&
                listener.Scheme == "http" && listener.Port == _manifest.Port &&
                listener.Host is "0.0.0.0" or "[::]" or "::" or "127.0.0.1" or "localhost" or "[::1]",
                "Unexpected effective listener address: " + address);
        }

        Setting(snapshot.Host.Logging, "Logging:LogLevel:Default", "Warning");
        Setting(snapshot.Host.Logging, "Logging:LogLevel:Microsoft.AspNetCore", "Warning");
        Setting(snapshot.Host.Logging, "Logging:LogLevel:Microsoft.Hosting.Lifetime", "Information");
    }

    private AssemblyCorrelation[] ValidateLoadedAssemblies(OwnedChild child, ProbeSnapshot snapshot, PreparedExperiment prepared)
    {
        List<AssemblyCorrelation> result = [];
        string entryDirectory = Path.GetDirectoryName(Path.Combine(child.Artifact.RootDirectory, child.Artifact.EntryPointRelativePath))!;
        foreach (AssemblySnapshot loaded in snapshot.Assemblies)
        {
            if (loaded.Name == "FhirCandle.Performance.Probe")
            {
                Require(SamePath(loaded.Path, _manifest.ProbePath) && SameHash(loaded.Sha256, prepared.ProbeSha256),
                    "Unexpected injected probe bytes or path.");
                result.Add(new AssemblyCorrelation(loaded, "identical BCL startup hook", "FhirCandle.Performance.Probe", ProbeProtocol.Version, string.Empty));
                continue;
            }

            FileSeal[] files;
            AssemblyFile[] assemblies;
            DependencyIdentity[] dependencies;
            string root;
            string origin;
            if (Within(loaded.Path, child.Artifact.RootDirectory))
            {
                files = child.Artifact.Files;
                assemblies = child.Artifact.Assemblies;
                dependencies = child.Artifact.Dependencies;
                root = child.Artifact.RootDirectory;
                origin = child.Artifact.Variant;
                Require(Within(loaded.Path, entryDirectory), "Loaded application assembly escaped the actual net8 closure.");
            }
            else
            {
                FrameworkIdentity[] matches = prepared.Frameworks.Where(framework => Within(loaded.Path, framework.RootDirectory)).ToArray();
                Require(matches.Length == 1, "Loaded assembly escaped all sealed artifact/runtime roots: " + loaded.Path);
                FrameworkIdentity framework = matches[0];
                files = framework.Files;
                assemblies = framework.Assemblies;
                dependencies = framework.Dependencies;
                root = framework.RootDirectory;
                origin = framework.Name + "/" + framework.Version;
            }

            string relative = Path.GetRelativePath(root, loaded.Path);
            FileSeal file = files.Single(seal => SameRelative(seal.RelativePath, relative));
            AssemblyFile assembly = assemblies.Single(item => SameRelative(item.RelativePath, relative));
            Require(SameHash(file.Sha256, loaded.Sha256) && assembly.Mvid == loaded.Mvid &&
                assembly.AssemblyVersion == loaded.AssemblyVersion && assembly.FileVersion == loaded.FileVersion &&
                assembly.InformationalVersion == loaded.InformationalVersion, "Loaded assembly identity differs from the seal: " + loaded.Name);
            DependencyIdentity[] owners = dependencies.Where(dependency => dependency.RuntimeAssets.Any(asset =>
                SameRelative(Path.GetFileName(asset.Replace('/', '\\')), Path.GetFileName(loaded.Path)))).ToArray();
            Require(owners.Length == 1, "Loaded assembly has missing/ambiguous dependency ownership: " + loaded.Name);
            if (loaded.Name.StartsWith("Hl7.Fhir.", StringComparison.Ordinal))
            {
                Require(origin == child.Artifact.Variant && owners[0].Name.StartsWith("Hl7.Fhir.", StringComparison.Ordinal) &&
                    owners[0].Version == child.Artifact.FirelyPackageVersion && owners[0].Type == "package",
                    "Loaded SDK assembly is not in the expected declared package cohort.");
            }

            result.Add(new AssemblyCorrelation(loaded, origin, owners[0].Name, owners[0].Version, owners[0].Sha512));
        }

        ValidateRequiredLoadedAssemblies(child.Artifact, child.FhirVersion, result);
        return result.ToArray();
    }

    /// <summary>Requires the addressed model and mandatory identities after per-assembly seal/closure validation.</summary>
    public static void ValidateRequiredLoadedAssemblies(
        ArtifactIdentity artifact, string fhirVersion, IReadOnlyCollection<AssemblyCorrelation> correlations)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        ArgumentNullException.ThrowIfNull(correlations);
        string modelPackage = "Hl7.Fhir." + fhirVersion;
        string context = artifact.Variant + "/" + fhirVersion + " (" + modelPackage + "/" +
            artifact.FirelyPackageVersion + " under " + artifact.RootDirectory + ")";
        DependencyIdentity[] packages = artifact.Dependencies.Where(dependency => dependency.Name == modelPackage).ToArray();
        Require(packages.Length == 1 && packages[0].Version == artifact.FirelyPackageVersion && packages[0].Type == "package",
            "Missing or ambiguous exact sealed addressed-model package for " + context + ".");
        DependencyIdentity package = packages[0];
        string entryPoint = NormalizeWindowsPath(artifact.RootDirectory + "\\" + artifact.EntryPointRelativePath.Replace('/', '\\'));
        string entryDirectory = entryPoint[..(entryPoint.LastIndexOf('\\') + 1)];
        // The package inventory spans multiple TFMs. Only the actual entry-point closure can supply the model.
        AssemblyFile[] models = artifact.Assemblies.Where(assembly =>
            Within(artifact.RootDirectory + "\\" + assembly.RelativePath.Replace('/', '\\'), entryDirectory) &&
            package.RuntimeAssets.Any(asset => SameRelative(asset.Replace('\\', '/').Split('/')[^1],
                assembly.RelativePath.Replace('\\', '/').Split('/')[^1]))).ToArray();
        Require(models.Length > 0, "Missing sealed addressed-model runtime assembly for " + context + ".");
        Require(correlations.Any(item => item.Origin == artifact.Variant && item.Dependency == package.Name &&
            item.DependencyVersion == package.Version && item.DependencySha512 == package.Sha512 &&
            models.Any(model => item.Loaded.Name == model.Name &&
                SamePath(item.Loaded.Path, artifact.RootDirectory + "\\" + model.RelativePath.Replace('/', '\\')))),
            "Missing validated loaded addressed model for " + context + "; expected sealed runtime assembly: " +
            string.Join(", ", models.Select(model => model.Name + " [" + model.RelativePath + "]")) + ".");

        Require(correlations.Any(item => item.Loaded.Name == "Hl7.Fhir.Base") &&
            correlations.Any(item => item.Loaded.Name == "FhirCandle.Performance.Probe") &&
            correlations.Any(item => item.Loaded.Name == "fhir-candle") &&
            correlations.Any(item => item.Loaded.Name == "System.Private.CoreLib" && item.Origin == "Microsoft.NETCore.App/8.0.30") &&
            correlations.Any(item => item.Loaded.Name == "Microsoft.AspNetCore.Server.Kestrel.Core" && item.Origin == "Microsoft.AspNetCore.App/8.0.30"),
            "Required loaded Base/core/ASP.NET/probe/application identities are absent.");
        Require(!correlations.Any(item => item.Loaded.Name == "FhirCandle.Performance"), "The runner leaked into the server closure.");
    }

    private async Task StopServerAsync(OwnedChild child)
    {
        bool forced = false;
        string? gracefulFailure = null;
        try
        {
            if (child.Started && child.Process.HasExited)
            {
                gracefulFailure = "The child exited before its owned shutdown request.";
            }

            if (child.Started && !child.Process.HasExited)
            {
                using CancellationTokenSource graceful = new(TimeSpan.FromSeconds(_manifest.Bounds.ShutdownSeconds));
                try
                {
                    Require(child.Authenticated && child.Pipe.IsConnected, "No authenticated probe is available for graceful shutdown.");
                    ProbeCommand shutdown = new(++child.Sequence, "shutdown", "perf", []);
                    await ProbeProtocol.WriteAsync(child.Pipe, shutdown, ProbeProtocol.MaximumRequestBytes, graceful.Token);
                    ProbeReply reply = await ProbeProtocol.ReadAsync<ProbeReply>(child.Pipe, ProbeProtocol.MaximumResponseBytes, graceful.Token);
                    Require(reply.Sequence == shutdown.Sequence && reply.Shutdown && reply.Snapshot is null, "Invalid shutdown acknowledgement.");
                    await child.Process.WaitForExitAsync(graceful.Token);
                }
                catch (Exception exception)
                {
                    // Cleanup recovery is recorded explicitly, never treated as successful semantic evidence.
                    gracefulFailure = exception.GetType().Name + ": " + exception.Message;
                }

                if (!child.Process.HasExited)
                {
                    forced = true;
                    child.Process.Kill(entireProcessTree: true);
                    using CancellationTokenSource killed = new(TimeSpan.FromSeconds(_manifest.Bounds.ShutdownSeconds));
                    await child.Process.WaitForExitAsync(killed.Token);
                }
            }

            if (child.Started)
            {
                using CancellationTokenSource drain = new(TimeSpan.FromSeconds(_manifest.Bounds.ShutdownSeconds));
                await Task.WhenAll(child.OutputPump, child.ErrorPump).WaitAsync(drain.Token);
                await WriteBytesNewAsync(Path.Combine(child.Directory, "stdout.log"), Encoding.UTF8.GetBytes(child.StandardOutput.Text), CancellationToken.None);
                await WriteBytesNewAsync(Path.Combine(child.Directory, "stderr.log"), Encoding.UTF8.GetBytes(child.StandardError.Text), CancellationToken.None);
                await WriteNewAsync(Path.Combine(child.Directory, "exit.json"), new
                {
                    ProcessId = child.Process.Id, child.StartedUtc, ExitedUtc = DateTimeOffset.UtcNow,
                    child.Process.ExitCode, ForcedTermination = forced, GracefulFailure = gracefulFailure,
                    StandardOutputOverflow = child.StandardOutput.Overflow, StandardErrorOverflow = child.StandardError.Overflow,
                }, CancellationToken.None);
                Require(!child.StandardOutput.Overflow && !child.StandardError.Overflow, "Child logs exceeded their bound.");
                Require(gracefulFailure is null && !forced && child.Process.ExitCode == 0,
                    "Application/shutdown gate failed; exact-PID forced cleanup, if needed, is retained in exit.json.");
            }
        }
        finally
        {
            try
            {
                if (child.Started && !child.Process.HasExited)
                {
                    child.Process.Kill(entireProcessTree: true);
                    using CancellationTokenSource cleanup = new(TimeSpan.FromSeconds(_manifest.Bounds.ShutdownSeconds));
                    await child.Process.WaitForExitAsync(cleanup.Token);
                }
            }
            finally
            {
                child.Dispose();
                _activeChild = null;
            }
        }
    }

    private void VerifyPrepared(PreparedExperiment prepared)
    {
        Require(prepared.SchemaVersion == 2 && prepared.ProtocolVersion == ProbeProtocol.Version && prepared.RunId == _manifest.RunId &&
            SameHash(prepared.ManifestSha256, HashBytes(_manifestBytes)) &&
            SameHash(prepared.ManifestSha256, SealFile(Output("manifest.json")).Sha256), "Prepare/preflight manifest or protocol mismatch.");
        Require(_preparedSha256 is not null && SameHash(_preparedSha256, SealFile(Output("prepared.json")).Sha256),
            "The prepared handoff changed during preflight.");
        WorkloadPreparation workload = prepared.Workload ?? throw new InvalidDataException("Missing full-matrix workload ledger.");
        Require(workload.ContractVersion == Workloads.ContractVersion && workload.Algorithm == Workloads.Algorithm &&
            workload.Seed == Workloads.Seed && workload.PreflightOperations == Workloads.PreflightOperations &&
            workload.Cases.Length == 72 && workload.Cases.Select(fixture => fixture.Case).SequenceEqual(Workloads.CreateCases()) &&
            workload.Corpora.Length == 6 && workload.Corpora.Select(fixture => fixture.Key).Distinct(StringComparer.Ordinal).Count() == 6 &&
            prepared.FixtureFiles.Length == 78 &&
            workload.StatusProfiles.SequenceEqual(new[] { CorrectnessOracle.StatusProfile("baseline"), CorrectnessOracle.StatusProfile("candidate") }),
            "Wrong full-matrix workload/status contract. Phase-one preparation cannot be reused.");
        foreach (CorpusFixture fixture in workload.Corpora)
        {
            string key = fixture.FhirVersion + "-" + fixture.PatientCount.ToString(CultureInfo.InvariantCulture) +
                "p-" + fixture.ObservationCount.ToString(CultureInfo.InvariantCulture) + "o";
            Require((fixture.FhirVersion is "R4" or "R4B" or "R5") && (fixture.PatientCount is 100 or 1000) &&
                fixture.ObservationCount == fixture.PatientCount * 10 && fixture.Key == key &&
                fixture.FixtureFile == Path.Combine("corpora", key + ".json") &&
                fixture.SeedRequests.Length == fixture.PatientCount + fixture.ObservationCount,
                "Invalid bounded corpus descriptor.");
        }

        foreach (CaseFixture fixture in workload.Cases)
        {
            Require(fixture.FixtureFile == Path.Combine("cases", fixture.Case.RowKey + ".json") &&
                fixture.LogicalOperations == Workloads.PreflightOperations && fixture.Requests.Length == Workloads.PreflightOperations,
                "Invalid fixed case descriptor.");
        }

        Require(prepared.Baseline.SourceRevision == BaselineRevision && prepared.Candidate.SourceRevision == CandidateRevision &&
            prepared.Baseline.Variant == "baseline" && prepared.Candidate.Variant == "candidate" &&
            prepared.Baseline.FirelyPackageVersion == "5.11.4" && prepared.Candidate.FirelyPackageVersion == "6.2.1" &&
            prepared.Candidate.EntryPointRelativePath == _manifest.Candidate.EntryPointRelativePath &&
            SamePath(prepared.Baseline.RootDirectory, _manifest.Baseline.ArtifactDirectory) &&
            SamePath(prepared.Candidate.RootDirectory, _manifest.Candidate.ArtifactDirectory), "Prepared artifact pins differ.");
        SafeRelative(prepared.Baseline.EntryPointRelativePath);
        VerifyFiles(prepared.Baseline.RootDirectory, prepared.Baseline.Files);
        VerifyFiles(prepared.Candidate.RootDirectory, prepared.Candidate.Files);
        VerifyFiles(Output("fixtures"), prepared.FixtureFiles);
        Require(prepared.Frameworks.Length == 2 && prepared.Frameworks.Select(framework => framework.Name)
            .Order(StringComparer.Ordinal).SequenceEqual(new[] { "Microsoft.AspNetCore.App", "Microsoft.NETCore.App" }),
            "Missing or mixed prepared shared framework set.");
        foreach (FrameworkIdentity framework in prepared.Frameworks)
        {
            Require(framework.Version == "8.0.30" && SamePath(framework.RootDirectory,
                Path.Combine(Path.GetDirectoryName(_manifest.Runtime.DotnetHostPath)!, "shared", framework.Name, "8.0.30")),
                "Prepared runtime version/root differs.");
            VerifyFiles(framework.RootDirectory, framework.Files);
        }

        Dictionary<string, FileSeal> currentTools = SealTools();
        Require(currentTools.Count == prepared.ToolFiles.Count && currentTools.All(pair =>
            prepared.ToolFiles.TryGetValue(pair.Key, out FileSeal? seal) && SameHash(pair.Value.Sha256, seal.Sha256)),
            "The active tool bytes/set changed. Use a new run ID after rebuilding or editing the protocol.");
        Require(SameHash(SealFile(_manifest.Runtime.DotnetHostPath).Sha256, prepared.DotnetHost.Sha256) &&
            SameHash(SealFile(_manifest.ProbePath).Sha256, prepared.ProbeSha256) &&
            SameHash(SealFile(_manifest.Candidate.BuildProvenancePath).Sha256, prepared.CandidateProvenance.Sha256) &&
            SameHash(SealFile(_manifest.Baseline.ArchivePath).Sha256, prepared.Archive.Sha256) &&
            SameHash(SealFile(_manifest.Isolation.EvidencePath).Sha256, prepared.IsolationEvidence.Sha256),
            "Host/probe/provenance/archive/isolation evidence changed.");
        CandidateBuildProvenance provenance = JsonSerializer.Deserialize<CandidateBuildProvenance>(
            File.ReadAllBytes(_manifest.Candidate.BuildProvenancePath), _json)!;
        ValidateCandidateProvenance(provenance);
        Require(SameHash(SealFile(provenance.BuildLogPath).Sha256, prepared.CandidateBuildLog.Sha256), "Retained build log changed.");
    }

    private static void Setting(Dictionary<string, string?> settings, string name, string expected) =>
        Require(settings.TryGetValue(name, out string? value) && value == expected, "Unexpected effective setting: " + name);

    private void ValidateCandidateProvenance(CandidateBuildProvenance provenance)
    {
        Require(provenance.SchemaVersion == 1 && provenance.SourceRevision == CandidateRevision &&
            provenance.DetachedHead && provenance.StatusBefore == string.Empty && provenance.StatusAfter == string.Empty,
            "Candidate must have retained clean, detached, exact-revision provenance.");
        Absolute(provenance.SourceDirectory);
        Absolute(provenance.WorkingDirectory);
        Absolute(provenance.BuildLogPath);
        Require(SamePath(provenance.SourceDirectory, provenance.WorkingDirectory) &&
            Within(provenance.SourceDirectory, DataRoot) &&
            !Within(provenance.ArtifactDirectory, provenance.SourceDirectory), "Build cwd and sealed output are not isolated.");
        Require(provenance.Command.Replace('/', '\\') == @"dotnet build src\fhir-candle\fhir-candle.csproj -c Release" &&
            Version.TryParse(provenance.BuildSdkVersion, out Version? sdk) && sdk.Major == 10 &&
            provenance.ExitCode == 0 && provenance.FinishedUtc >= provenance.StartedUtc &&
            provenance.StartedUtc > DateTimeOffset.UnixEpoch && provenance.FinishedUtc <= DateTimeOffset.UtcNow.AddMinutes(1),
            "Missing successful documented Release build, .NET 10 build SDK or timestamps.");
        Require(SamePath(provenance.ArtifactDirectory, _manifest.Candidate.ArtifactDirectory) &&
            provenance.EntryPointRelativePath == _manifest.Candidate.EntryPointRelativePath &&
            provenance.Files.Length > 0, "Candidate build output identity does not match the manifest.");
        Require(SameHash(SealFile(provenance.BuildLogPath).Sha256, provenance.BuildLogSha256), "Candidate build log hash mismatch.");
    }

    private async Task<ArtifactIdentity> AcquireBaselineAsync(CancellationToken cancellationToken)
    {
        using HttpClient client = new(new HttpClientHandler
        {
            AllowAutoRedirect = false, UseProxy = false, AutomaticDecompression = DecompressionMethods.All,
        })
        {
            Timeout = TimeSpan.FromSeconds(_manifest.Bounds.SetupSeconds),
        };
        byte[] registration = await DownloadBytesAsync(client, RegistrationUrl, 1024 * 1024, cancellationToken);
        using JsonDocument registrationJson = JsonDocument.Parse(registration);
        Require(registrationJson.RootElement.GetProperty("catalogEntry").GetString() == CatalogUrl &&
            registrationJson.RootElement.GetProperty("packageContent").GetString() == PackageUrl, "NuGet registration identity changed.");
        await WriteBytesNewAsync(Output("baseline-registration.json"), registration, cancellationToken);
        byte[] catalog = await DownloadBytesAsync(client, CatalogUrl, 4 * 1024 * 1024, cancellationToken);
        using JsonDocument catalogJson = JsonDocument.Parse(catalog);
        JsonElement metadata = catalogJson.RootElement;
        Require(metadata.GetProperty("id").GetString() == "fhir-candle" &&
            metadata.GetProperty("version").GetString() == "2025.611.1422" &&
            metadata.GetProperty("packageHashAlgorithm").GetString() == "SHA512" &&
            metadata.GetProperty("packageHash").GetString() == PublishedSha512, "Published NuGet digest/identity changed.");
        await WriteBytesNewAsync(Output("baseline-catalog.json"), catalog, cancellationToken);
        bool cached = File.Exists(_manifest.Baseline.ArchivePath);
        if (!cached)
        {
            EnsureNoReparseParents(_manifest.Baseline.ArchivePath);
            Directory.CreateDirectory(Path.GetDirectoryName(_manifest.Baseline.ArchivePath)!);
            using HttpResponseMessage response = await client.GetAsync(PackageUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            Require(response.StatusCode == HttpStatusCode.OK, "The pinned package download failed.");
            await using Stream input = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using FileStream output = new(_manifest.Baseline.ArchivePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await CopyBoundedAsync(input, output, MaximumPackageBytes, cancellationToken);
        }

        FileSeal archive = SealFile(_manifest.Baseline.ArchivePath);
        Require(archive.Length == metadata.GetProperty("packageSize").GetInt64(), "Published package size mismatch.");
        using (FileStream input = File.OpenRead(_manifest.Baseline.ArchivePath))
        {
            Require(Convert.ToBase64String(SHA512.HashData(input)) == PublishedSha512, "Published package SHA-512 mismatch.");
        }

        await WriteNewAsync(Output("baseline-acquisition.json"), new
        {
            PackageUrl, RegistrationUrl, CatalogUrl, VerifiedUtc = DateTimeOffset.UtcNow,
            AcquiredUtc = cached ? (DateTimeOffset?)null : DateTimeOffset.UtcNow, UsedCachedArchive = cached,
            Archive = archive, PublishedDigestAlgorithm = "SHA512", PublishedDigest = PublishedSha512,
            MaximumPackageBytes, AutomaticRedirects = false, SourceBuildFallback = false,
        }, cancellationToken);
        await ExtractOrVerifyAsync(_manifest.Baseline.ArchivePath, _manifest.Baseline.ArtifactDirectory, cancellationToken);
        string[] nuspecs = Directory.GetFiles(_manifest.Baseline.ArtifactDirectory, "*.nuspec", SearchOption.TopDirectoryOnly);
        Require(nuspecs.Length == 1, "Missing or ambiguous package nuspec.");
        XDocument nuspec = ReadXml(nuspecs[0]);
        XElement packageMetadata = nuspec.Descendants().Single(element => element.Name.LocalName == "metadata");
        Require(packageMetadata.Elements().Single(element => element.Name.LocalName == "id").Value == "fhir-candle" &&
            packageMetadata.Elements().Single(element => element.Name.LocalName == "version").Value == "2025.611.1422" &&
            packageMetadata.Elements().Single(element => element.Name.LocalName == "repository").Attribute("commit")?.Value == BaselineRevision,
            "Nuspec package/source provenance mismatch.");
        await WriteBytesNewAsync(Output("baseline.nuspec"), await File.ReadAllBytesAsync(nuspecs[0], cancellationToken), cancellationToken);
        List<string> entryPoints = [];
        foreach (string runtimeConfig in Directory.GetFiles(_manifest.Baseline.ArtifactDirectory, "fhir-candle.runtimeconfig.json", SearchOption.AllDirectories))
        {
            using JsonDocument document = JsonDocument.Parse(await ReadBoundedFileAsync(runtimeConfig, 1024 * 1024, cancellationToken));
            if (document.RootElement.GetProperty("runtimeOptions").GetProperty("tfm").GetString() == "net8.0")
            {
                entryPoints.Add(Path.ChangeExtension(Path.ChangeExtension(runtimeConfig, null), ".dll"));
            }
        }

        Require(entryPoints.Count == 1, "The published payload has no unique actual net8.0 fhir-candle entry point.");
        string entryPoint = entryPoints[0];
        string toolSettingsPath = Path.Combine(Path.GetDirectoryName(entryPoint)!, "DotnetToolSettings.xml");
        XDocument toolSettings = ReadXml(toolSettingsPath);
        XElement[] commands = toolSettings.Descendants().Where(element => element.Name.LocalName == "Command").ToArray();
        Require(commands.Length == 1 && commands[0].Attribute("Name")?.Value == "fhir-candle" &&
            commands[0].Attribute("EntryPoint")?.Value == Path.GetFileName(entryPoint) &&
            commands[0].Attribute("Runner")?.Value == "dotnet", "Published tool entry-point settings are not supported.");
        await WriteBytesNewAsync(Output("baseline-tool-settings.xml"),
            await File.ReadAllBytesAsync(toolSettingsPath, cancellationToken), cancellationToken);
        return InspectArtifact("baseline", BaselineRevision, "5.11.4", _manifest.Baseline.ArtifactDirectory,
            Path.GetRelativePath(_manifest.Baseline.ArtifactDirectory, entryPoint));
    }

    private static async Task ExtractOrVerifyAsync(string archivePath, string root, CancellationToken token)
    {
        EnsureNoReparseParents(root);
        bool exists = Directory.Exists(root);
        if (!exists)
        {
            Directory.CreateDirectory(root);
        }

        using ZipArchive archive = ZipFile.OpenRead(archivePath);
        Require(archive.Entries.Count is > 0 and <= 40000, "Unbounded archive entry count.");
        HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);
        List<FileSeal> expected = [];
        long expanded = 0;
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            string relative = entry.FullName.Replace('/', '\\').TrimEnd('\\');
            SafeRelative(relative);
            Require(names.Add(relative), "Duplicate archive path.");
            Require(((entry.ExternalAttributes >> 16) & 0xF000) != 0xA000 &&
                (entry.ExternalAttributes & (int)FileAttributes.ReparsePoint) == 0, "Archive links are forbidden.");
            string destination = Path.Combine(root, relative);
            EnsureNoReparseParents(destination);
            if (entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\'))
            {
                if (!exists)
                {
                    Directory.CreateDirectory(destination);
                }

                continue;
            }

            expanded = checked(expanded + entry.Length);
            Require(entry.Length >= 0 && expanded <= 2L * 1024 * 1024 * 1024, "Archive expansion limit exceeded.");
            using Stream input = entry.Open();
            if (!exists)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                await using FileStream output = new(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                await CopyBoundedAsync(input, output, entry.Length, token);
            }
            else
            {
                string digest = Convert.ToHexString(await SHA256.HashDataAsync(input, token));
                expected.Add(new FileSeal(relative, entry.Length, digest));
            }
        }

        if (exists)
        {
            VerifyFiles(root, expected.ToArray());
        }
    }

    private static ArtifactIdentity InspectArtifact(string variant, string revision, string firely, string root, string entryPoint)
    {
        SafeRelative(entryPoint);
        string entry = Path.Combine(root, entryPoint);
        Require(File.Exists(entry), "Missing managed application entry point.");
        using JsonDocument runtime = JsonDocument.Parse(File.ReadAllBytes(Path.ChangeExtension(entry, ".runtimeconfig.json")));
        JsonElement options = runtime.RootElement.GetProperty("runtimeOptions");
        Require(options.GetProperty("tfm").GetString() == "net8.0", "Artifact is not the actual net8.0 application.");
        JsonElement frameworks = options.GetProperty("frameworks");
        Require(frameworks.GetArrayLength() == 2 && frameworks.EnumerateArray().All(framework =>
            (framework.GetProperty("name").GetString() is "Microsoft.NETCore.App" or "Microsoft.AspNetCore.App") &&
            framework.GetProperty("version").GetString()!.StartsWith("8.0.", StringComparison.Ordinal)),
            "Artifact framework declarations do not support the common .NET/ASP.NET 8 pair.");
        DependencyIdentity[] dependencies = ReadDependencies(Path.ChangeExtension(entry, ".deps.json"));
        DependencyIdentity[] cohort = dependencies.Where(dependency => dependency.Name.StartsWith("Hl7.Fhir.", StringComparison.Ordinal)).ToArray();
        Require(cohort.Length >= 3 && cohort.All(dependency => dependency.Version == firely && dependency.Type == "package"),
            "Wrong or mixed Firely SDK dependency cohort.");
        foreach (string model in new[] { "Hl7.Fhir.R4", "Hl7.Fhir.R4B", "Hl7.Fhir.R5" })
        {
            Require(cohort.Any(dependency => dependency.Name == model), "Missing declared FHIR model cohort: " + model);
        }

        FileSeal[] files = SealDirectory(root);
        AssemblyFile[] assemblies = ReadAssemblyFiles(root, files);
        Require(assemblies.Any(assembly => SameRelative(assembly.RelativePath, entryPoint) &&
            assembly.Name == "fhir-candle" && assembly.TargetFramework == ".NETCoreApp,Version=v8.0"), "Entry-point PE identity mismatch.");
        return new ArtifactIdentity(variant, revision, firely, root, entryPoint, files, assemblies, dependencies);
    }

    private FrameworkIdentity InspectFramework(string name, string version)
    {
        string root = Path.Combine(Path.GetDirectoryName(_manifest.Runtime.DotnetHostPath)!, "shared", name, version);
        Require(Directory.Exists(root), "The exact shared runtime is unavailable: " + root);
        FileSeal[] files = SealDirectory(root);
        return new FrameworkIdentity(name, version, root, files, ReadAssemblyFiles(root, files),
            ReadDependencies(Path.Combine(root, name + ".deps.json")));
    }

    private Dictionary<string, FileSeal> SealTools()
    {
        string runner = typeof(ExperimentRunner).Assembly.Location;
        Require(typeof(ExperimentRunner).Assembly.GetCustomAttribute<System.Runtime.Versioning.TargetFrameworkAttribute>()?.FrameworkName ==
            ".NETCoreApp,Version=v8.0", "Run the documented net8.0 tool output, not a net9/net10 tooling binary.");
        Require(SameHash(SealFile(typeof(ProbeSession).Assembly.Location).Sha256, SealFile(_manifest.ProbePath).Sha256),
            "Runner protocol reference and injected probe bytes differ.");
        Dictionary<string, FileSeal> files = new(StringComparer.OrdinalIgnoreCase);
        foreach (string path in new[] { runner, Path.ChangeExtension(runner, ".deps.json"),
            Path.ChangeExtension(runner, ".runtimeconfig.json"), typeof(ProbeSession).Assembly.Location, _manifest.ProbePath })
        {
            files[path] = SealFile(path);
        }

        return files;
    }

    private static DependencyIdentity[] ReadDependencies(string path)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(path));
        JsonElement root = document.RootElement;
        string targetName = root.GetProperty("runtimeTarget").GetProperty("name").GetString()
            ?? throw new InvalidDataException("Missing dependency runtime target.");
        JsonElement target = root.GetProperty("targets").GetProperty(targetName);
        JsonElement libraries = root.GetProperty("libraries");
        List<DependencyIdentity> dependencies = [];
        foreach (JsonProperty library in target.EnumerateObject())
        {
            int slash = library.Name.LastIndexOf('/');
            Require(slash > 0, "Invalid dependency identity.");
            JsonElement details = libraries.GetProperty(library.Name);
            List<string> assets = [];
            foreach (string kind in new[] { "runtime", "runtimeTargets", "native", "resources" })
            {
                if (library.Value.TryGetProperty(kind, out JsonElement values))
                {
                    assets.AddRange(values.EnumerateObject().Select(asset => asset.Name));
                }
            }

            dependencies.Add(new DependencyIdentity(library.Name[..slash], library.Name[(slash + 1)..],
                details.GetProperty("type").GetString()!, details.TryGetProperty("sha512", out JsonElement hash) ? hash.GetString()! : string.Empty,
                assets.Order(StringComparer.Ordinal).ToArray()));
        }

        return dependencies.OrderBy(dependency => dependency.Name, StringComparer.Ordinal).ToArray();
    }

    private static AssemblyFile[] ReadAssemblyFiles(string root, FileSeal[] files) =>
        files.Where(file => file.RelativePath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(file => ReadAssemblyFile(Path.Combine(root, file.RelativePath), file.RelativePath))
            .OfType<AssemblyFile>().ToArray();

    private static AssemblyFile? ReadAssemblyFile(string path, string relative)
    {
        using FileStream stream = File.OpenRead(path);
        using PEReader pe = new(stream);
        if (!pe.HasMetadata)
        {
            return null;
        }

        MetadataReader metadata = pe.GetMetadataReader();
        if (!metadata.IsAssembly)
        {
            return null;
        }

        AssemblyDefinition definition = metadata.GetAssemblyDefinition();
        Dictionary<string, string> attributes = new(StringComparer.Ordinal);
        foreach (CustomAttributeHandle handle in definition.GetCustomAttributes())
        {
            CustomAttribute attribute = metadata.GetCustomAttribute(handle);
            EntityHandle declaringType;
            if (attribute.Constructor.Kind == HandleKind.MemberReference)
            {
                declaringType = metadata.GetMemberReference((MemberReferenceHandle)attribute.Constructor).Parent;
            }
            else if (attribute.Constructor.Kind == HandleKind.MethodDefinition)
            {
                declaringType = metadata.GetMethodDefinition((MethodDefinitionHandle)attribute.Constructor).GetDeclaringType();
            }
            else
            {
                continue;
            }

            string name;
            if (declaringType.Kind == HandleKind.TypeReference)
            {
                TypeReference type = metadata.GetTypeReference((TypeReferenceHandle)declaringType);
                name = metadata.GetString(type.Namespace) + "." + metadata.GetString(type.Name);
            }
            else if (declaringType.Kind == HandleKind.TypeDefinition)
            {
                TypeDefinition type = metadata.GetTypeDefinition((TypeDefinitionHandle)declaringType);
                name = metadata.GetString(type.Namespace) + "." + metadata.GetString(type.Name);
            }
            else
            {
                continue;
            }
            if (name is "System.Runtime.Versioning.TargetFrameworkAttribute" or "System.Reflection.AssemblyInformationalVersionAttribute")
            {
                BlobReader blob = metadata.GetBlobReader(attribute.Value);
                Require(blob.ReadUInt16() == 1, "Invalid assembly attribute prolog.");
                attributes.Add(name, blob.ReadSerializedString() ?? string.Empty);
            }
        }

        return new AssemblyFile(relative, metadata.GetString(definition.Name), definition.Version.ToString(),
            FileVersionInfo.GetVersionInfo(path).FileVersion ?? string.Empty,
            attributes.GetValueOrDefault("System.Reflection.AssemblyInformationalVersionAttribute", string.Empty),
            metadata.GetGuid(metadata.GetModuleDefinition().Mvid).ToString("D"),
            attributes.GetValueOrDefault("System.Runtime.Versioning.TargetFrameworkAttribute", string.Empty));
    }

    private string Output(params string[] parts) => Path.Combine([_manifest.OutputDirectory, .. parts]);

    private static FileSeal SealFile(string path, string? relative = null)
    {
        Absolute(path);
        EnsureNoReparseParents(path);
        using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return new FileSeal(relative ?? Path.GetFileName(path), stream.Length, Convert.ToHexString(SHA256.HashData(stream)));
    }

    private static FileSeal[] SealDirectory(string root)
    {
        Absolute(root);
        EnsureNoReparseParents(root);
        List<string> files = [];
        Stack<string> pending = new();
        pending.Push(root);
        int entries = 0;
        while (pending.TryPop(out string? directory))
        {
            foreach (string path in Directory.EnumerateFileSystemEntries(directory))
            {
                Require(++entries <= 50000, "File inventory exceeds its bound.");
                FileAttributes attributes = File.GetAttributes(path);
                Require((attributes & FileAttributes.ReparsePoint) == 0, "Reparse points cannot be traversed by the artifact inventory.");
                if ((attributes & FileAttributes.Directory) != 0)
                {
                    pending.Push(path);
                }
                else
                {
                    files.Add(path);
                }
            }
        }

        return files.Order(StringComparer.OrdinalIgnoreCase).Select(path => SealFile(path, Path.GetRelativePath(root, path))).ToArray();
    }

    private static void VerifyFiles(string root, FileSeal[] expected)
    {
        foreach (FileSeal file in expected)
        {
            SafeRelative(file.RelativePath);
            Require(file.Length >= 0 && file.Sha256.Length == 64 && file.Sha256.All(Uri.IsHexDigit), "Invalid file seal.");
        }

        FileSeal[] actual = SealDirectory(root);
        Require(actual.Length == expected.Length && expected.Select(file => file.RelativePath).Distinct(StringComparer.OrdinalIgnoreCase).Count() == expected.Length,
            "Sealed file inventory changed: " + root);
        Dictionary<string, FileSeal> inventory = actual.ToDictionary(file => file.RelativePath, StringComparer.OrdinalIgnoreCase);
        foreach (FileSeal file in expected)
        {
            Require(inventory.TryGetValue(file.RelativePath.Replace('/', '\\'), out FileSeal? observed) &&
                observed.Length == file.Length && SameHash(observed.Sha256, file.Sha256), "Sealed file changed: " + file.RelativePath);
        }
    }

    private static async Task WriteNewAsync<T>(string path, T value, CancellationToken token) =>
        await WriteBytesNewAsync(path, JsonSerializer.SerializeToUtf8Bytes(value, _json), token);

    private static async Task WriteBytesNewAsync(string path, byte[] bytes, CancellationToken token)
    {
        EnsureNoReparseParents(path);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using FileStream stream = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        await stream.WriteAsync(bytes, token);
        await stream.FlushAsync(token);
    }

    private static async Task SaveFailureAsync(string path, Exception exception) =>
        await WriteNewAsync(path, new { FailedUtc = DateTimeOffset.UtcNow, Type = exception.GetType().FullName, exception.Message, MeasuredTrials = 0 }, CancellationToken.None);

    private static async Task<byte[]> DownloadBytesAsync(HttpClient client, string uri, int maximum, CancellationToken token)
    {
        using HttpResponseMessage response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, token);
        Require(response.StatusCode == HttpStatusCode.OK, "Pinned metadata download failed: " + uri);
        await using Stream input = await response.Content.ReadAsStreamAsync(token);
        using MemoryStream output = new();
        await CopyBoundedAsync(input, output, maximum, token);
        return output.ToArray();
    }

    private static async Task<byte[]> ReadBoundedFileAsync(string path, int maximum, CancellationToken token)
    {
        Absolute(path);
        EnsureNoReparseParents(path);
        await using FileStream input = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        Require(input.Length <= maximum, "Input file exceeds bounds: " + path);
        using MemoryStream output = new();
        await CopyBoundedAsync(input, output, maximum, token);
        return output.ToArray();
    }

    private static async Task CopyBoundedAsync(Stream input, Stream output, long maximum, CancellationToken token)
    {
        byte[] buffer = new byte[65536];
        long count = 0;
        int read;
        while ((read = await input.ReadAsync(buffer, token)) != 0)
        {
            count = checked(count + read);
            Require(count <= maximum, "Payload exceeded its bounded size.");
            await output.WriteAsync(buffer.AsMemory(0, read), token);
        }
    }

    private static XDocument ReadXml(string path)
    {
        using XmlReader reader = XmlReader.Create(path, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 4 * 1024 * 1024,
        });
        return XDocument.Load(reader);
    }

    private static string HashBytes(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    private static bool SameHash(string left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static bool SameRelative(string left, string right) => string.Equals(left.Replace('/', '\\'), right.Replace('/', '\\'), StringComparison.OrdinalIgnoreCase);

    private static bool SamePath(string left, string right) =>
        string.Equals(NormalizeWindowsPath(left), NormalizeWindowsPath(right), StringComparison.OrdinalIgnoreCase);

    private static bool Within(string path, string root) => SamePath(path, root) ||
        NormalizeWindowsPath(path).StartsWith(NormalizeWindowsPath(root).TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase);

    private static void Absolute(string path)
    {
        Require(!string.IsNullOrEmpty(path) && path.Length >= 3 &&
            char.IsAsciiLetter(path[0]) && path[1] == ':' && path[2] == '\\' && !path.Contains('/') &&
            path.IndexOf(':', 2) < 0 && path.All(character => character >= ' ') &&
            path.IndexOfAny(['<', '>', '"', '|', '?', '*']) < 0,
            "An absolute local Windows path is required: " + path);
    }

    private static string NormalizeWindowsPath(string path)
    {
        // Manifest validation is pure and can be tested on any build OS; execution remains Windows-only.
        Absolute(path);
        List<string> segments = [];
        foreach (string segment in path[3..].Split('\\', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == ".")
            {
                continue;
            }

            if (segment == "..")
            {
                Require(segments.Count > 0, "A Windows path escapes its drive root.");
                segments.RemoveAt(segments.Count - 1);
            }
            else
            {
                Require(!segment.EndsWith('.') && !segment.EndsWith(' '), "Ambiguous Windows path segment.");
                segments.Add(segment);
            }
        }

        return char.ToUpperInvariant(path[0]) + ":\\" + string.Join("\\", segments);
    }

    private static void SafeRelative(string path)
    {
        Require(!string.IsNullOrEmpty(path) && !Path.IsPathRooted(path), "Absolute or empty archive/relative path.");
        foreach (string segment in path.Replace('/', '\\').Split('\\'))
        {
            Require(segment.Length > 0 && segment is not ("." or "..") && !segment.EndsWith('.') && !segment.EndsWith(' ') &&
                segment.All(character => character >= ' ') &&
                segment.IndexOfAny(['<', '>', ':', '"', '|', '?', '*']) == -1, "Unsafe archive or inventory path.");
            string name = segment.Split('.')[0].ToUpperInvariant();
            Require(name is not ("CON" or "PRN" or "AUX" or "NUL") &&
                !(name.Length == 4 && (name.StartsWith("COM", StringComparison.Ordinal) || name.StartsWith("LPT", StringComparison.Ordinal)) &&
                name[3] is >= '0' and <= '9'), "Reserved Windows path.");
        }
    }

    private static void EnsureNoReparseParents(string path)
    {
        string? current = Path.GetFullPath(path);
        while (current is not null)
        {
            if (File.Exists(current) || Directory.Exists(current))
            {
                Require((File.GetAttributes(current) & FileAttributes.ReparsePoint) == 0, "Reparse points are not allowed in experiment inputs/outputs.");
            }

            current = Path.GetDirectoryName(current);
        }
    }

    private static void RequireWindowsAndUninjectedRunner()
    {
        Require(OperatingSystem.IsWindows() && RuntimeInformation.ProcessArchitecture == Architecture.X64,
            "This pinned experiment requires Windows x64.");
        Require(string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOTNET_STARTUP_HOOKS")) &&
            Environment.GetEnvironmentVariable(ProbeConfiguration.EnvironmentVariable) is null,
            "The runner must not inherit any startup hook or probe activation.");
    }

    private sealed record PreparedInputs(
        Dictionary<string, WorkloadCorpus> Corpora, Dictionary<string, WorkloadRequest[]> Requests);

    private sealed record SeededCorpus(
        WorkloadCorpus Corpus, Dictionary<string, ExpectedResource> Resources, ProbeSnapshot Snapshot);

    private sealed record PreparedCase(
        WorkloadCase Workload, SeededCorpus Seeded, WorkloadRequest[] Requests,
        ProbeSnapshot Initial, ProbeSnapshot Prepared, SuccessfulStatusProfile Profile);

    private sealed record CompletedOperation(int Number, HttpExchangeEvidence[] Exchanges);

    private sealed record OperationAudit(int Number, int HttpRequests, int[] StatusCodes, string[] Identities, ResourceBinding[] Bindings);

    private sealed record ResponseDigest(
        int StatusCode, int BodyBytes, string BodySha256, string CompleteEvidenceSha256, DateTimeOffset StartedUtc, DateTimeOffset CompletedUtc);

    private sealed record SeedResourceEvidence(
        int Number, string Identity, int InputBytes, string InputSha256,
        ResourceBinding Binding, ResponseDigest Put, ResponseDigest? Readback);

    private sealed record PreflightAttempt(
        int Ordinal, string RowKey, string Variant, string EvidenceDirectory, string Status,
        DateTimeOffset? StartedUtc, DateTimeOffset? FinishedUtc, CaseProgress Progress, string? Failure);

    private sealed class CaseProgress
    {
        public string Stage { get; set; } = "not-started";
        public int ProcessId { get; set; }
        public DateTimeOffset ProcessStartedUtc { get; set; }
        public int AttemptedSeedWrites { get; set; }
        public int CompletedSeedWrites { get; set; }
        public int ValidatedSeedWrites { get; set; }
        public int AttemptedSeedReads { get; set; }
        public int CompletedSeedReads { get; set; }
        public int ValidatedSeedReads { get; set; }
        public int AttemptedOperations { get; set; }
        public int CompletedOperations { get; set; }
        public int ValidatedOperations { get; set; }
        public int AttemptedCaseHttpRequests { get; set; }
        public int CompletedCaseHttpResponses { get; set; }
        public int AuditedReadbacks { get; set; }
        public int AttemptedCleanupDeletes { get; set; }
        public int CompletedCleanupDeletes { get; set; }
        public int ValidatedCleanupDeletes { get; set; }
        public int TotalHttpRequests { get; set; }
        public int TotalCompletedResponses { get; set; }
    }

    private sealed class OwnedChild : IDisposable
    {
        internal readonly Process Process;
        internal readonly NamedPipeServerStream Pipe;
        internal readonly ProbeConfiguration Configuration;
        internal readonly ArtifactIdentity Artifact;
        internal readonly string FhirVersion;
        internal readonly string Directory;
        internal readonly string ChildRoot;
        internal readonly string ProfileDirectory;
        internal readonly string SourceDirectory;
        internal readonly string CacheDirectory;
        internal readonly string BaseUrl;
        internal readonly HttpClient Http;
        internal readonly BoundedLog StandardOutput = new();
        internal readonly BoundedLog StandardError = new();
        internal Task OutputPump = Task.CompletedTask;
        internal Task ErrorPump = Task.CompletedTask;
        internal bool Started;
        internal bool Authenticated;
        internal DateTimeOffset StartedUtc;
        internal int Sequence;
        internal int AttemptedHttpRequests;
        internal int CompletedHttpResponses;

        internal OwnedChild(
            Process process, NamedPipeServerStream pipe, ProbeConfiguration configuration, ArtifactIdentity artifact,
            string fhirVersion, string directory, string childRoot, string profile, string source, string cache, int port)
        {
            Process = process;
            Pipe = pipe;
            Configuration = configuration;
            Artifact = artifact;
            FhirVersion = fhirVersion;
            Directory = directory;
            ChildRoot = childRoot;
            ProfileDirectory = profile;
            SourceDirectory = source;
            CacheDirectory = cache;
            BaseUrl = "http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture) + "/fhir/perf";
            Http = new HttpClient(new HttpClientHandler
            {
                AllowAutoRedirect = false, UseProxy = false, UseCookies = false, MaxConnectionsPerServer = 1,
                AutomaticDecompression = DecompressionMethods.None,
            }) { Timeout = Timeout.InfiniteTimeSpan };
        }

        public void Dispose()
        {
            Http.Dispose();
            Pipe.Dispose();
            Process.Dispose();
        }
    }

    private sealed class BoundedLog
    {
        private const int MaximumCharacters = 2 * 1024 * 1024;
        private readonly StringBuilder _text = new();
        private readonly object _sync = new();
        private bool _overflow;

        internal bool Overflow
        {
            get
            {
                lock (_sync)
                {
                    return _overflow;
                }
            }
        }

        internal string Text
        {
            get
            {
                lock (_sync)
                {
                    return _text.ToString();
                }
            }
        }

        internal async Task PumpAsync(StreamReader reader)
        {
            char[] buffer = new char[4096];
            int count;
            while ((count = await reader.ReadAsync(buffer)) != 0)
            {
                lock (_sync)
                {
                    int remaining = MaximumCharacters - _text.Length;
                    _text.Append(buffer, 0, Math.Min(count, remaining));
                    _overflow |= count > remaining;
                }
            }
        }
    }

    private sealed record TcpObservation(int ProcessId, int State, string LocalAddress, int LocalPort, string RemoteAddress, int RemotePort);

    private static TcpObservation[] CaptureTcp(int processId)
    {
        Require(OperatingSystem.IsWindows(), "PID-attributed network inspection requires Windows.");
        List<TcpObservation> observations = [];
        foreach (int family in new[] { 2, 23 })
        {
            int length = 0;
            uint result = GetExtendedTcpTable(IntPtr.Zero, ref length, true, family, 5, 0);
            Require(result == 122 && length is > 0 and <= 4 * 1024 * 1024, "Cannot size bounded OS TCP ownership table.");
            IntPtr buffer = Marshal.AllocHGlobal(length);
            try
            {
                result = GetExtendedTcpTable(buffer, ref length, true, family, 5, 0);
                Require(result == 0, "Cannot inspect child TCP ownership: " + result.ToString(CultureInfo.InvariantCulture));
                int count = Marshal.ReadInt32(buffer);
                int rowSize = family == 2 ? 24 : 56;
                Require(count >= 0 && count <= (length - 4) / rowSize, "Invalid OS TCP ownership table.");
                for (int index = 0; index < count; index++)
                {
                    IntPtr row = IntPtr.Add(buffer, 4 + (index * rowSize));
                    int owner = Marshal.ReadInt32(row, family == 2 ? 20 : 52);
                    if (owner != processId)
                    {
                        continue;
                    }

                    byte[] local = new byte[family == 2 ? 4 : 16];
                    byte[] remote = new byte[local.Length];
                    Marshal.Copy(IntPtr.Add(row, family == 2 ? 4 : 0), local, 0, local.Length);
                    Marshal.Copy(IntPtr.Add(row, family == 2 ? 12 : 24), remote, 0, remote.Length);
                    IPAddress localAddress = family == 2 ? new IPAddress(local) : new IPAddress(local, (uint)Marshal.ReadInt32(row, 16));
                    IPAddress remoteAddress = family == 2 ? new IPAddress(remote) : new IPAddress(remote, (uint)Marshal.ReadInt32(row, 40));
                    observations.Add(new TcpObservation(owner, Marshal.ReadInt32(row, family == 2 ? 0 : 48),
                        localAddress.ToString(), NetworkPort(row, family == 2 ? 8 : 20),
                        remoteAddress.ToString(), NetworkPort(row, family == 2 ? 16 : 44)));
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        return observations.ToArray();
    }

    private static int NetworkPort(IntPtr row, int offset) => (Marshal.ReadByte(row, offset) << 8) | Marshal.ReadByte(row, offset + 1);

    private static bool IsLoopbackAddress(string address)
    {
        IPAddress parsed = IPAddress.Parse(address);
        return IPAddress.IsLoopback(parsed.IsIPv4MappedToIPv6 ? parsed.MapToIPv4() : parsed);
    }

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern uint GetExtendedTcpTable(
        IntPtr table, ref int size, [MarshalAs(UnmanagedType.Bool)] bool order, int addressFamily, int tableClass, uint reserved);

    private static void Require([DoesNotReturnIf(false)] bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidDataException(message);
        }
    }
}
