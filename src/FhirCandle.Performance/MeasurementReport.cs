// <copyright file="MeasurementReport.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FhirCandle.Performance.Probe;

namespace FhirCandle.Performance;

/// <summary>Versioned measurement settings, distinct from the unchanged four-operation semantic gate.</summary>
public sealed record MeasurementSettings
{
    /// <summary>Settings schema, not an application version.</summary>
    public int SchemaVersion { get; init; } = 1;
    /// <summary>Independent successful repetitions; fixed before calibration.</summary>
    public int TrialsPerVariant { get; init; } = 3;
    /// <summary>Initial create/hook count, increased only by retained paired coverage pilots.</summary>
    public int InitialMutationOperations { get; init; } = 256;
    /// <summary>Initial update/read/search count.</summary>
    public int InitialOtherOperations { get; init; } = 1024;
    /// <summary>Common finite calibration ceiling; exhaustion requires a new explicitly revised experiment.</summary>
    public int MaximumPilotOperations { get; init; } = 8192;
    /// <summary>Actual samples inside client work, separately required from each sampler.</summary>
    public int MinimumSamples { get; init; } = 10;
    /// <summary>Identical idle and loaded no-HTTP control durations.</summary>
    public int ControlMilliseconds { get; init; } = 1000;

    /// <summary>Rejects unversioned changes, unequal counts, reduced coverage and unbounded calibration.</summary>
    public void Validate()
    {
        if (SchemaVersion != 1 || TrialsPerVariant is < 3 or > 10 || InitialMutationOperations != 256 ||
            InitialOtherOperations != 1024 || MaximumPilotOperations != 8192 || MinimumSamples != 10 ||
            ControlMilliseconds != 1000)
        {
            throw new InvalidDataException("Unsupported fixed measurement settings.");
        }
    }

    /// <summary>The common first pilot count for a case, not a time-driven denominator.</summary>
    public int InitialCount(WorkloadCase workload) =>
        workload.CaseId.EndsWith("-create", StringComparison.Ordinal) || workload.CaseId == "subscription-receive"
            ? InitialMutationOperations : InitialOtherOperations;
}

/// <summary>Caller-observed conditions; the tool never invents a quiet machine or changes power/security settings.</summary>
public sealed record MachineConditions(
    DateTimeOffset ObservedUtc, string Hardware, long PhysicalMemoryBytes, string PowerConditions,
    string BackgroundConditions, string ToolBuildSdk, string ObservationSource);

/// <summary>A frozen sequential dispatch. Each entry must result in its own PID/start-time lifetime.</summary>
public sealed record TrialPlan(
    int Ordinal, string AttemptId, string RowKey, string Variant, int Repetition, int Operations,
    string Purpose, bool Sampling, string RelativeDirectory);

/// <summary>A raw reference is relative to the run directory and sealed by both length and SHA-256.</summary>
public sealed record EvidenceReference(string Path, long Length, string Sha256);

/// <summary>The final input bytes/order for a row, shared by both variants and every repetition.</summary>
public sealed record FrozenCaseInput(
    WorkloadCase Case, int Operations, EvidenceReference File, string WarmupOrderSha256, string MeasuredOrderSha256,
    long WarmupPayloadBytes, long MeasuredPayloadBytes);

/// <summary>All calibration attempts, including inadequate-coverage pilots, with the selected adequate pair.</summary>
public sealed record CalibrationProfile(
    string RowKey, int Operations, EvidenceReference[] Attempts, string[] NormalAttempts, string[] SamplerOffAttempts);

/// <summary>Immutable handoff produced only after the entire semantic matrix and paired calibration pass.</summary>
public sealed record FrozenMeasurement(
    [property: JsonRequired] int SchemaVersion, string ContractVersion, string Origin, string RunId, string PreparedSha256,
    string ManifestSha256, string ProbeSha256, [property: JsonRequired] ClockObservation Frozen, MeasurementSettings Settings,
    ExperimentBounds Bounds, FrozenCaseInput[] Inputs, TrialPlan[] Schedule, CalibrationProfile[] Calibration,
    EvidenceReference[] SemanticFiles, EvidenceReference Environment, [property: JsonRequired] ParentIdentity Parent)
{
    /// <summary>Unmodified wall-clock observation, not a chronology gate between separate runner invocations.</summary>
    [JsonIgnore]
    public DateTimeOffset FrozenUtc => Frozen.Utc;
}

/// <summary>Only the owned server's process memory; timestamps use the common Windows monotonic clock.</summary>
public readonly record struct ProcessMemorySample(
    [property: JsonRequired] int ProcessId, [property: JsonRequired] long ProcessStartUtcTicks,
    [property: JsonRequired] long Timestamp, [property: JsonRequired] long WorkingSetBytes,
    [property: JsonRequired] long PrivateBytes, [property: JsonRequired] long CpuTicks);

/// <summary>One logical-operation interval includes all pages and complete response-body consumption.</summary>
public sealed record OperationTiming(
    [property: JsonRequired] int Number, [property: JsonRequired] long Start, [property: JsonRequired] long End,
    [property: JsonRequired] int HttpRequests, [property: JsonRequired] bool Completed);

/// <summary>Complete or partially consumed logical work, retained even on failure, outside the summary record.</summary>
public sealed record OperationResult(
    OperationTiming Timing, HttpExchangeEvidence[] Exchanges, int AttemptedHttpRequests,
    HttpExchangeEvidence? IncompleteResponse = null, string? Failure = null);

/// <summary>Client/server boundary evidence, not an inferred matching of independently timed counters.</summary>
public sealed record BatchWindow(
    [property: JsonRequired] string Name, [property: JsonRequired] long Frequency,
    [property: JsonRequired] long BeginSent, [property: JsonRequired] long BeginAcknowledged,
    [property: JsonRequired] long ClientStart, [property: JsonRequired] long ClientEnd,
    [property: JsonRequired] long EndSent, [property: JsonRequired] long EndAcknowledged,
    [property: JsonRequired] int ExpectedOperations, [property: JsonRequired] int AttemptedOperations,
    [property: JsonRequired] int CompletedOperations, [property: JsonRequired] OperationTiming[] Operations,
    [property: JsonRequired] WindowEvidence Server, [property: JsonRequired] ProcessMemorySample[] ProcessSamples,
    [property: JsonRequired] int ProcessDroppedSamples, [property: JsonRequired] int ProcessSamplingErrors,
    [property: JsonRequired] string? Failure);

/// <summary>Warm clinical state is restored, but real versions/caches and hook receipts are intentionally retained.</summary>
public sealed record WarmupEvidence(
    int Requested, int Completed, int Validated, int DeletedCreates, int RestorePuts, int RetainedBundles,
    ResourceBinding? OriginalUpdate, ResourceBinding? WarmedUpdate, ResourceBinding? RestoredUpdate);

/// <summary>An ordinary audit interaction; never included in logical operation or server counter timings.</summary>
public sealed record AuditInteraction(string Method, string Identity, ResponseEvidence Response);

/// <summary>Full audit responses and every bounded identity chunk, not just a boolean success flag.</summary>
public sealed record TrialAudit(
    bool Passed, OperationAudit[] WarmupOperations, OperationAudit[] MeasuredOperations,
    AuditInteraction[] WarmupReadbacks, AuditInteraction[] WarmupDeletes, AuditInteraction? Restoration,
    AuditInteraction[] FinalReadbacks, IdentityInspection[] DeletedIdentityChunks, IdentityInspection[] HookIdentityChunks);

/// <summary>Versioned append-only trial result. A failed attempt never becomes a shorter successful batch.</summary>
public sealed record TrialRecord
{
    /// <summary>Raw-trial schema.</summary>
    [JsonRequired]
    public int SchemaVersion { get; init; } = 3;
    /// <summary>Tests deliberately retain their distinct origin and cannot complete a live report.</summary>
    public string Origin { get; init; } = "unit-fixture";
    /// <summary>Exact dispatch, including the unreduced denominator.</summary>
    public required TrialPlan Plan { get; init; }
    /// <summary>Unique owning run.</summary>
    public required string RunId { get; init; }
    /// <summary>All app/runtime/tool/probe/corpus/configuration identity is rooted in this seal.</summary>
    public required string PreparedSha256 { get; init; }
    /// <summary>Accepted trials additionally bind the post-calibration schedule/count seal.</summary>
    public string? FrozenSha256 { get; init; }
    /// <summary>Actual generated input file for this attempt, including calibration's possibly different count.</summary>
    public required EvidenceReference Inputs { get; init; }
    /// <summary>Start of the attempt, before launching the fresh child.</summary>
    [JsonRequired]
    public ClockObservation Started { get; init; }
    /// <summary>End of the attempt, after bounded child cleanup.</summary>
    [JsonRequired]
    public ClockObservation Finished { get; init; }
    /// <summary>Parent identity scopes the recorded monotonic lifecycle, independently of the child's OS start identity.</summary>
    [JsonRequired]
    public ParentIdentity Parent { get; init; }
    /// <summary>Actual start wall-clock observation, retained even when UTC later moves backwards.</summary>
    [JsonIgnore]
    public DateTimeOffset StartedUtc => Started.Utc;
    /// <summary>Actual finish wall-clock observation; elapsed time comes only from the monotonic brackets.</summary>
    [JsonIgnore]
    public DateTimeOffset FinishedUtc => Finished.Utc;
    /// <summary>OS-authenticated server PID.</summary>
    public int ProcessId { get; init; }
    /// <summary>OS-observed child lifetime, not PID alone.</summary>
    public long ProcessStartUtcTicks { get; init; }
    /// <summary>passed, coverage-insufficient, or failed; no implicit retry/reclassification.</summary>
    public string Status { get; init; } = "failed";
    /// <summary>Retained exception or inadequacy reason.</summary>
    public string? Failure { get; init; }
    /// <summary>Artifact/tool/protocol seals checked around the entire child lifetime.</summary>
    public bool SealsBeforeAndAfter { get; init; }
    /// <summary>Only clean owned shutdown can support a successful trial.</summary>
    public bool CleanShutdown { get; init; }
    /// <summary>Entire full-content and persisted-state oracle, not just HTTP success.</summary>
    public bool OraclePassed { get; init; }
    /// <summary>Every seed PUT and read-back must be validated.</summary>
    public int ValidatedSeedWrites { get; init; }
    /// <summary>Every seed read-back, not a 32-ID probe sample.</summary>
    public int ValidatedSeedReads { get; init; }
    /// <summary>Actual validated warm-up/restoration history.</summary>
    public WarmupEvidence? Warmup { get; init; }
    /// <summary>Counter and client work intervals; null is missing, never zero.</summary>
    public BatchWindow? Batch { get; init; }
    /// <summary>Matched idle/loaded controls, with their own probe-inclusive counters and CPU.</summary>
    public BatchWindow[] Controls { get; init; } = [];
    /// <summary>Idle/loaded natural/forced and immediate/settled/forced post-work observations.</summary>
    public Footprint[] Checkpoints { get; init; } = [];
    /// <summary>All saved raw files for this child except this self-describing record.</summary>
    public EvidenceReference[] RawFiles { get; init; } = [];
}

/// <summary>The complete attempted/not-attempted ledger, written even when execution stops on a failure.</summary>
public sealed record MeasurementLedger(
    [property: JsonRequired] int SchemaVersion, string RunId, string FrozenSha256,
    [property: JsonRequired] ClockObservation Started, [property: JsonRequired] ClockObservation Finished,
    EvidenceReference[] Attempts, TrialPlan[] NotAttempted, string? Failure, [property: JsonRequired] ParentIdentity Parent)
{
    /// <summary>Actual UTC observations remain documentary evidence, not elapsed/order clocks.</summary>
    [JsonIgnore]
    public DateTimeOffset StartedUtc => Started.Utc;
    /// <summary>Actual end observation, without clock correction or clamping.</summary>
    [JsonIgnore]
    public DateTimeOffset FinishedUtc => Finished.Utc;
}

/// <summary>Independent-trial central estimate and spread; request observations are not replicates.</summary>
public sealed record MetricSummary(double Median, double Minimum, double Maximum, int Trials, int[] Operations);

/// <summary>Unavailable ratios retain an explicit reason and a meaningful absolute change.</summary>
public sealed record MetricChange(double? Ratio, double? Percent, double Absolute, string? Reason);

/// <summary>Validation errors distinguish incompleteness from valid but noisy observations.</summary>
public sealed record CompletenessResult(bool Complete, string[] Errors);

/// <summary>Versioned evidence-only calculations. No server launcher, HTTP client or assembly loader is used here.</summary>
public static class MeasurementReport
{
    /// <summary>Counter, lifecycle, calibration and reporting contract for this implementation.</summary>
    public const string ContractVersion = "candle-perf-measurement-v3";
    /// <summary>Set only by the real runner; fixtures must not use this provenance claim.</summary>
    public const string LiveOrigin = "sealed-release-http";
    /// <summary>Common saved-record reader ceiling; dedicated manifest, corpus and prepared limits remain smaller.</summary>
    public const int MaximumEvidenceBytes = 1024 * 1024 * 1024;
    private static readonly JsonSerializerOptions _json = new(ProbeProtocol.JsonOptions) { WriteIndented = true };
    private static readonly string[] _checkpointNames =
    [
        "idle-natural", "idle-forced", "loaded-natural", "loaded-forced",
        "post-immediate", "post-settled", "post-forced",
    ];

    /// <summary>Compact windows/trials fit all three full-capacity sample pairs without changing or truncating evidence.</summary>
    public static byte[] SerializeEvidence<T>(T value)
    {
        // Other files retain their indented representation, including the sealed workload input/order hashes.
        JsonSerializerOptions options = value is TrialRecord or BatchWindow ? ProbeProtocol.JsonOptions : _json;
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(value, options);
        _ = ValidateEvidenceLength(bytes.Length);
        return bytes;
    }

    /// <summary>The same finite array-size gate applies to saved references, writers and both readers, before any read allocation.</summary>
    public static int ValidateEvidenceLength(long length, int maximumBytes = MaximumEvidenceBytes)
    {
        Require(maximumBytes > 0 && maximumBytes <= MaximumEvidenceBytes && maximumBytes <= Array.MaxLength &&
            length >= 0 && length <= maximumBytes && length <= Array.MaxLength, "Saved evidence exceeds its bounded reader.");
        return checked((int)length);
    }

    /// <summary>Shared saved-file reader: validates length before allocation and rejects growth instead of reading to end.</summary>
    public static async Task<byte[]> ReadBoundedBytesAsync(Stream input, int maximumBytes, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(input);
        Require(input.CanSeek && input.Position == 0, "Invalid saved-evidence input stream.");
        int length = ValidateEvidenceLength(input.Length, maximumBytes);
        token.ThrowIfCancellationRequested();
        byte[] bytes = new byte[length];
        await input.ReadExactlyAsync(bytes, token);
        byte[] extra = new byte[1];
        Require(await input.ReadAsync(extra, token) == 0, "Saved evidence changed length while being read.");
        return bytes;
    }

    /// <summary>Builds the actual used paired schedule, alternating by repetition and case block.</summary>
    public static TrialPlan[] CreateSchedule(IReadOnlyDictionary<string, int> counts, int repetitions)
    {
        if (repetitions < 3 || repetitions > 10)
        {
            throw new InvalidDataException("At least three bounded independent repetitions are required.");
        }

        List<TrialPlan> schedule = [];
        WorkloadCase[] cases = Workloads.CreateCases();
        if (counts.Count != cases.Length || cases.Any(workload => !counts.ContainsKey(workload.RowKey)))
        {
            throw new InvalidDataException("The schedule requires exactly all seventy-two case keys.");
        }

        for (int repetition = 1; repetition <= repetitions; repetition++)
        {
            for (int block = 0; block < cases.Length; block++)
            {
                WorkloadCase workload = cases[block];
                string[] order = ((block + repetition - 1) % 2 == 0) ? ["baseline", "candidate"] : ["candidate", "baseline"];
                foreach (string variant in order)
                {
                    int ordinal = schedule.Count + 1;
                    string id = "trial-" + ordinal.ToString("D4", CultureInfo.InvariantCulture);
                    schedule.Add(new(ordinal, id, workload.RowKey, variant, repetition, counts[workload.RowKey],
                        "measurement", true, Path.Combine("trials", id)));
                }
            }
        }

        return schedule.ToArray();
    }

    /// <summary>Median/min/max across independent trial values, never across their individual requests.</summary>
    public static MetricSummary CalculateSummary(IEnumerable<(double Value, int Operations)> trials)
    {
        (double Value, int Operations)[] values = trials.ToArray();
        if (values.Length == 0 || values.Any(item => !double.IsFinite(item.Value) || item.Operations <= 0))
        {
            throw new InvalidDataException("Missing or invalid independent trial observations.");
        }

        double[] sorted = values.Select(item => item.Value).Order().ToArray();
        int middle = sorted.Length / 2;
        double median = sorted.Length % 2 == 0 ? (sorted[middle - 1] / 2) + (sorted[middle] / 2) : sorted[middle];
        return new(median, sorted[0], sorted[^1], values.Length, values.Select(item => item.Operations).ToArray());
    }

    /// <summary>Zero/negative baseline denominators are not useful relative changes, including memory deltas.</summary>
    public static MetricChange CalculateChange(double baseline, double candidate)
    {
        if (!double.IsFinite(baseline) || !double.IsFinite(candidate))
        {
            throw new InvalidDataException("An unavailable metric must not be replaced by a numeric ratio.");
        }

        return baseline <= 0
            ? new(null, null, candidate - baseline, baseline == 0 ? "zero baseline" : "negative incremental baseline")
            : new(candidate / baseline, 100 * (candidate - baseline) / baseline, candidate - baseline, null);
    }

    /// <summary>Filters only actual workload-window samples, rejecting wrong PID/lifetime and invalid readings.</summary>
    public static ProcessMemorySample[] WorkSamples(BatchWindow window, int processId, long processStartTicks)
    {
        if (window.ProcessSamples.Any(sample => sample.ProcessId != processId ||
            sample.ProcessStartUtcTicks != processStartTicks || sample.WorkingSetBytes <= 0 || sample.PrivateBytes <= 0 ||
            sample.Timestamp < window.Server.Begin.Timestamp || sample.Timestamp > window.Server.End.Timestamp))
        {
            throw new InvalidDataException("Process stream contains the wrong server lifetime, clock or footprint.");
        }

        return window.ProcessSamples.Where(sample => sample.Timestamp >= window.ClientStart &&
            sample.Timestamp <= window.ClientEnd).ToArray();
    }

    /// <summary>The sampled maximum is a lower bound inside work, not a process-lifetime high-water mark.</summary>
    public static (long WorkingSet, long PrivateBytes) CalculatePeak(BatchWindow window, int processId, long processStartTicks)
    {
        ProcessMemorySample[] samples = WorkSamples(window, processId, processStartTicks);
        if (samples.Length == 0)
        {
            throw new InvalidDataException("No actual server-PID samples inside the workload interval.");
        }

        return (samples.Max(sample => sample.WorkingSetBytes), samples.Max(sample => sample.PrivateBytes));
    }

    /// <summary>Used by both the live lifecycle and the offline renderer; coverage is the only pilot-retryable error.</summary>
    public static string[] ValidateWindow(BatchWindow window, int processId, long processStartTicks, bool requireCoverage)
    {
        List<string> errors = [];
        WindowEvidence server = window.Server;
        CounterReading begin = server.Begin;
        CounterReading end = server.End;
        Check(errors, window.Frequency > 0 && server.Frequency == window.Frequency &&
            server.ProcessId == processId && server.ProcessStartUtcTicks == processStartTicks &&
            server.CounterOrigin == "GC-process-start", "counter origin/process/clock");
        Check(errors, window.BeginSent <= begin.Timestamp && begin.Timestamp <= window.BeginAcknowledged &&
            window.BeginAcknowledged <= window.ClientStart && window.ClientStart < window.ClientEnd &&
            window.ClientEnd <= window.EndSent && window.EndSent <= end.Timestamp &&
            end.Timestamp <= window.EndAcknowledged, "counter/client bracketing");
        Check(errors, ProbeProtocol.IsWithinWindow(begin.Timestamp, end.Timestamp, server.Frequency), "counter duration/deadline");
        Check(errors, end.AllocatedBytes >= begin.AllocatedBytes && begin.AllocatedBytes >= 0 &&
            end.Gen0 >= begin.Gen0 && end.Gen1 >= begin.Gen1 && end.Gen2 >= begin.Gen2 &&
            begin.Gen0 >= 0 && begin.Gen1 >= 0 && begin.Gen2 >= 0 && end.PauseTicks >= begin.PauseTicks &&
            begin.PauseTicks >= 0 && end.CpuTicks >= begin.CpuTicks && begin.CpuTicks >= 0, "negative counter delta");
        Check(errors, !server.DeadlineExceeded && server.DroppedSamples == 0 && server.SamplingErrors == 0 &&
            window.ProcessDroppedSamples == 0 && window.ProcessSamplingErrors == 0 &&
            server.Capacity == ProbeProtocol.SampleCapacity && server.CadenceMilliseconds == ProbeProtocol.SampleMilliseconds &&
            server.Samples.Length <= server.Capacity && window.ProcessSamples.Length <= server.Capacity,
            "sampler error/overrun/protocol");
        Check(errors, window.Failure is null && window.ExpectedOperations >= 0 &&
            window.AttemptedOperations == window.ExpectedOperations && window.CompletedOperations == window.ExpectedOperations &&
            window.Operations.Length == window.ExpectedOperations, "incomplete batch/changed denominator");
        long previous = window.ClientStart;
        foreach (OperationTiming operation in window.Operations)
        {
            Check(errors, operation.Completed && operation.HttpRequests > 0 &&
                operation.Start >= previous && operation.End > operation.Start && operation.End <= window.ClientEnd,
                "incomplete/nonsequential operation");
            previous = operation.End;
        }

        Check(errors, server.Samples.All(sample => sample.Timestamp >= begin.Timestamp && sample.Timestamp <= end.Timestamp &&
            sample.OccupiedBytes >= 0 && sample.HeapBytes >= 0 && sample.FragmentedBytes >= 0 &&
            sample.CommittedBytes >= 0 && sample.LastGcIndex >= 0), "invalid managed sample");
        Check(errors, StrictlyIncreasing(server.Samples.Select(sample => sample.Timestamp)) &&
            StrictlyIncreasing(window.ProcessSamples.Select(sample => sample.Timestamp)), "nonmonotonic sample stream");
        try
        {
            ProcessMemorySample[] process = WorkSamples(window, processId, processStartTicks);
            int managed = server.Samples.Count(sample => sample.Timestamp >= window.ClientStart && sample.Timestamp <= window.ClientEnd);
            if (server.Sampling)
            {
                if (requireCoverage)
                {
                    Check(errors, process.Length >= 10 && managed >= 10, "coverage: fewer than ten actual samples in either stream");
                }
            }
            else
            {
                Check(errors, process.Length == 0 && server.Samples.Length == 0 && window.ProcessSamples.Length == 0,
                    "sampler-off window contains samples");
            }
        }
        catch (InvalidDataException exception)
        {
            errors.Add(exception.Message);
        }

        return errors.Distinct(StringComparer.Ordinal).ToArray();
    }

    /// <summary>Response observations must fit the separately recorded, unchanged logical-operation Stopwatch boundaries.</summary>
    public static void ValidateOperationResponses(
        IReadOnlyList<OperationResult> results, long frequency, long earliest, long latest)
    {
        Require(frequency > 0 && earliest > 0 && latest > earliest && results.Count > 0, "Missing raw operation timing bounds.");
        long previous = earliest;
        foreach (OperationResult result in results)
        {
            OperationTiming timing = result.Timing;
            Require(timing.Completed && timing.Start >= previous && timing.End > timing.Start && timing.End <= latest &&
                timing.HttpRequests > 0 && timing.HttpRequests == result.Exchanges.Length &&
                result.AttemptedHttpRequests == timing.HttpRequests && result.IncompleteResponse is null && result.Failure is null,
                "Incomplete or reordered raw operation timing.");
            CorrectnessOracle.ValidateSequentialResponses(result.Exchanges.Select(exchange => exchange.Response));
            foreach (HttpExchangeEvidence exchange in result.Exchanges)
            {
                Require(exchange.Response.Started.Frequency == frequency && exchange.Response.Started.Before >= timing.Start &&
                    exchange.Response.Completed.After <= timing.End, "Response clocks escape their independently timed operation.");
            }

            previous = timing.End;
        }
    }

    /// <summary>Checks mandatory counters, streams and every checkpoint independently of the saved status flag.</summary>
    public static string[] ValidateTrial(TrialRecord trial, bool requireCoverage = true)
    {
        List<string> errors = [];
        WorkloadCase? workload = Workloads.CreateCases().SingleOrDefault(item => item.RowKey == trial.Plan.RowKey);
        Check(errors, trial.SchemaVersion == 3 && workload is not null && trial.ProcessId > 0 &&
            trial.ProcessStartUtcTicks > 0 && trial.Parent.IsValid() &&
            ClockObservation.IsOrdered(trial.Started, trial.Finished), "trial identity/lifetime/schema");
        try
        {
            _ = ClockObservation.Assess(trial.Started, trial.Finished);
        }
        catch (InvalidDataException exception)
        {
            errors.Add("trial clock: " + exception.Message);
        }
        Check(errors, trial.SealsBeforeAndAfter && trial.CleanShutdown && trial.OraclePassed &&
            ((trial.Status == "passed" && trial.Failure is null) ||
                (!requireCoverage && trial.Status == "coverage-insufficient" && trial.Failure is not null)),
            "trial failure/oracle/seal/shutdown");
        if (workload is not null)
        {
            Check(errors, trial.ValidatedSeedWrites == workload.PatientCount + workload.ObservationCount &&
                trial.ValidatedSeedReads == trial.ValidatedSeedWrites, "complete corpus audit");
        }

        WarmupEvidence? warm = trial.Warmup;
        Check(errors, warm is not null && warm.Requested == Workloads.WarmupOperations &&
            warm.Completed == warm.Requested && warm.Validated == warm.Requested &&
            warm.DeletedCreates == (workload?.CaseId.EndsWith("-create", StringComparison.Ordinal) == true ? 128 : 0) &&
            warm.RetainedBundles == (workload?.CaseId == "subscription-receive" ? 128 : 0) &&
            warm.RestorePuts == (workload?.CaseId == "observation-update" ? 1 : 0), "warm-up history");
        if (workload?.CaseId.EndsWith("-update", StringComparison.Ordinal) == true)
        {
            Check(errors, warm?.OriginalUpdate is not null && warm.WarmedUpdate is not null && warm.RestoredUpdate is not null &&
                warm.OriginalUpdate.VersionId == "1" && warm.WarmedUpdate.VersionId == "129" &&
                warm.RestoredUpdate.VersionId == (workload.CaseId == "observation-update" ? "130" : "129"),
                "missing original/warm/restored server metadata");
        }
        if (trial.Batch is BatchWindow batch)
        {
            Check(errors, batch.Name == "work" && batch.ExpectedOperations == trial.Plan.Operations &&
                batch.Frequency == trial.Started.Frequency && batch.BeginSent >= trial.Started.After &&
                batch.EndAcknowledged < trial.Finished.Before &&
                batch.Server.Sampling == trial.Plan.Sampling &&
                batch.Operations.Select(operation => operation.Number).SequenceEqual(
                    Enumerable.Range(Workloads.WarmupOperations + 1, trial.Plan.Operations)), "fixed operation count/order");
            errors.AddRange(ValidateWindow(batch, trial.ProcessId, trial.ProcessStartUtcTicks, requireCoverage));
        }
        else
        {
            errors.Add("missing work window");
        }

        Check(errors, trial.Controls.Length == 2 &&
            trial.Controls.Select(control => control.Name).SequenceEqual(new[] { "idle-control", "loaded-control" }), "missing control windows");
        foreach (BatchWindow control in trial.Controls)
        {
            errors.AddRange(ValidateWindow(control, trial.ProcessId, trial.ProcessStartUtcTicks, requireCoverage: requireCoverage && control.Server.Sampling));
            Check(errors, control.ExpectedOperations == 0 && control.Server.PostWork.Length == 0 &&
                control.Frequency == trial.Started.Frequency && control.BeginSent >= trial.Started.After &&
                control.EndAcknowledged < trial.Finished.Before &&
                control.Server.Sampling == trial.Plan.Sampling &&
                (double)(control.ClientEnd - control.ClientStart) / control.Frequency >= 1, "control duration/profile");
        }

        Check(errors, trial.Checkpoints.Select(point => point.Name).SequenceEqual(_checkpointNames), "missing/duplicate checkpoint");
        foreach (Footprint point in trial.Checkpoints)
        {
            Check(errors, point.ProcessId == trial.ProcessId && point.Frequency == trial.Batch?.Frequency &&
                point.ForcedGc == point.Name.EndsWith("-forced", StringComparison.Ordinal) &&
                point.Timestamp == point.Managed.Timestamp && point.WorkingSetBytes > 0 && point.PrivateBytes > 0 &&
                point.Managed.OccupiedBytes >= 0 && point.Managed.HeapBytes >= 0 && point.Managed.FragmentedBytes >= 0 &&
                point.Managed.CommittedBytes >= 0 && point.Managed.LastGcIndex >= 0 &&
                point.Counters.Timestamp >= point.Timestamp && point.Counters.AllocatedBytes >= 0 &&
                point.Counters.Gen0 >= 0 && point.Counters.Gen1 >= 0 && point.Counters.Gen2 >= 0 &&
                point.Counters.CpuTicks >= 0 && point.Counters.PauseTicks >= 0, "invalid checkpoint footprint");
        }

        if (trial.Batch is not null && trial.Checkpoints.Length == 7)
        {
            Footprint[] points = trial.Checkpoints;
            Check(errors, StrictlyIncreasing(points.Select(point => point.Timestamp)) &&
                points[3].Timestamp < trial.Batch.Server.Begin.Timestamp &&
                points[4].Timestamp >= trial.Batch.Server.End.Timestamp &&
                points[5].Timestamp - points[4].Timestamp >= trial.Batch.Frequency &&
                points[6].Timestamp - points[5].Timestamp >= trial.Batch.Frequency &&
                points[6].Timestamp <= trial.Batch.EndAcknowledged &&
                trial.Batch.Server.PostWork.SequenceEqual(points[4..]) &&
                trial.Controls.Length == 2 && trial.Controls[0].Server.End.Timestamp < points[0].Timestamp &&
                points[1].Timestamp < trial.Controls[1].Server.Begin.Timestamp &&
                trial.Controls[1].Server.End.Timestamp < points[2].Timestamp &&
                points[1].Timestamp - points[0].Timestamp >= trial.Batch.Frequency &&
                points[3].Timestamp - points[2].Timestamp >= trial.Batch.Frequency, "checkpoint/forced-GC window ordering");
            if (trial.Controls.Length == 2)
            {
                CounterReading[] timeline = [
                    trial.Controls[0].Server.Begin, trial.Controls[0].Server.End, points[0].Counters, points[1].Counters,
                    trial.Controls[1].Server.Begin, trial.Controls[1].Server.End, points[2].Counters, points[3].Counters,
                    trial.Batch.Server.Begin, trial.Batch.Server.End, points[4].Counters, points[5].Counters, points[6].Counters,
                ];
                Check(errors, timeline.Zip(timeline.Skip(1), (before, after) =>
                    after.Timestamp >= before.Timestamp && after.AllocatedBytes >= before.AllocatedBytes &&
                    after.Gen0 >= before.Gen0 && after.Gen1 >= before.Gen1 && after.Gen2 >= before.Gen2 &&
                    after.PauseTicks >= before.PauseTicks && after.CpuTicks >= before.CpuTicks).All(value => value),
                    "counter timeline changed origin or includes setup/audit/forced-GC boundaries");
                Check(errors, points[1].Counters.Gen2 >= points[0].Counters.Gen2 + 2 &&
                    points[3].Counters.Gen2 >= points[2].Counters.Gen2 + 2 &&
                    points[6].Counters.Gen2 >= points[5].Counters.Gen2 + 2, "missing two-collection forced-GC protocol");
            }
        }

        return errors.Distinct(StringComparer.Ordinal).ToArray();
    }

    /// <summary>Exact case/variant/repetition matrix and independent lifetimes, used before rendering any complete report.</summary>
    public static string[] ValidateMatrix(FrozenMeasurement frozen, IReadOnlyList<TrialRecord> trials)
    {
        List<string> errors = [];
        WorkloadCase[] cases = Workloads.CreateCases();
        Check(errors, frozen.SchemaVersion == 3 && frozen.ContractVersion == ContractVersion &&
            frozen.Frozen.IsValid() && frozen.Parent.IsValid() &&
            frozen.Inputs.Length == 72 && frozen.Inputs.Select(input => input.Case).SequenceEqual(cases),
            "frozen case/schema inventory");
        try
        {
            frozen.Settings.Validate();
            frozen.Bounds.Validate();
            Dictionary<string, int> counts = frozen.Inputs.ToDictionary(input => input.Case.RowKey, input => input.Operations, StringComparer.Ordinal);
            Check(errors, frozen.Schedule.SequenceEqual(CreateSchedule(counts, frozen.Settings.TrialsPerVariant)), "frozen alternating schedule");
            Check(errors, frozen.Inputs.All(input => IsPilotCount(input.Operations, frozen.Settings.InitialCount(input.Case),
                frozen.Settings.MaximumPilotOperations)), "uncalibrated operation count");
        }
        catch (Exception exception) when (exception is InvalidDataException or ArgumentException)
        {
            errors.Add(exception.Message);
        }

        Check(errors, trials.Count == 72 * 2 * frozen.Settings.TrialsPerVariant &&
            trials.Select(trial => trial.Plan).SequenceEqual(frozen.Schedule), "missing/extra/reordered variant/case/trial");
        Check(errors, trials.Select(trial => (trial.ProcessId, trial.ProcessStartUtcTicks)).Distinct().Count() == trials.Count &&
            trials.Select(trial => trial.Plan.AttemptId).Distinct(StringComparer.Ordinal).Count() == trials.Count,
            "reused process lifetime or attempt");
        for (int index = 0; index < trials.Count; index++)
        {
            TrialRecord trial = trials[index];
            Check(errors, trial.Plan.Purpose == "measurement" && trial.Plan.Sampling && trial.RunId == frozen.RunId &&
                trial.PreparedSha256 == frozen.PreparedSha256 && trial.Inputs.Sha256 ==
                frozen.Inputs.FirstOrDefault(input => input.Case.RowKey == trial.Plan.RowKey)?.File.Sha256, "mixed identity/input/protocol");
            if (index > 0)
            {
                Check(errors, trials[index - 1].Parent == trial.Parent &&
                    ClockObservation.IsOrdered(trials[index - 1].Finished, trial.Started),
                    "overlapping server lifetimes or changed parent clock identity");
            }

            errors.AddRange(ValidateTrial(trial).Select(error => trial.Plan.AttemptId + ": " + error));
        }

        return errors.Distinct(StringComparer.Ordinal).ToArray();
    }

    /// <summary>All metric keys are generated from validated raw counters/checkpoints; absence is never a zero.</summary>
    public static Dictionary<string, double> TrialMetrics(TrialRecord trial)
    {
        string[] invalid = ValidateTrial(trial);
        if (invalid.Length != 0)
        {
            throw new InvalidDataException("Invalid trial cannot supply metrics: " + string.Join("; ", invalid));
        }

        BatchWindow batch = trial.Batch ?? throw new InvalidDataException("Missing batch.");
        double n = batch.ExpectedOperations;
        double seconds = (double)(batch.ClientEnd - batch.ClientStart) / batch.Frequency;
        if (n <= 0 || n != trial.Plan.Operations || seconds <= 0 || batch.CompletedOperations != n || batch.Failure is not null)
        {
            throw new InvalidDataException("Cannot calculate successful-work metrics from an incomplete batch.");
        }

        CounterReading begin = batch.Server.Begin;
        CounterReading end = batch.Server.End;
        Dictionary<string, double> metrics = new(StringComparer.Ordinal)
        {
            ["time.ms/op"] = seconds * 1000 / n,
            ["throughput.op/s"] = n / seconds,
            ["allocation.B/op"] = (end.AllocatedBytes - begin.AllocatedBytes) / n,
            ["gc.gen0.count"] = end.Gen0 - begin.Gen0,
            ["gc.gen1.count"] = end.Gen1 - begin.Gen1,
            ["gc.gen2.count"] = end.Gen2 - begin.Gen2,
            ["gc.gen0/1000op"] = (end.Gen0 - begin.Gen0) * 1000 / n,
            ["gc.gen1/1000op"] = (end.Gen1 - begin.Gen1) * 1000 / n,
            ["gc.gen2/1000op"] = (end.Gen2 - begin.Gen2) * 1000 / n,
            ["gc.pause.ms"] = (end.PauseTicks - begin.PauseTicks) / (double)TimeSpan.TicksPerMillisecond,
            ["cpu.ms"] = (end.CpuTicks - begin.CpuTicks) / (double)TimeSpan.TicksPerMillisecond,
            ["gap.begin.ms"] = (batch.ClientStart - begin.Timestamp) * 1000.0 / batch.Frequency,
            ["gap.end.ms"] = (end.Timestamp - batch.ClientEnd) * 1000.0 / batch.Frequency,
        };
        (long working, long privateBytes) = CalculatePeak(batch, trial.ProcessId, trial.ProcessStartUtcTicks);
        metrics.Add("process.peak-working.B", working);
        metrics.Add("process.peak-private.B", privateBytes);
        ManagedSample[] samples = batch.Server.Samples.Where(sample => sample.Timestamp >= batch.ClientStart &&
            sample.Timestamp <= batch.ClientEnd).ToArray();
        if (samples.Length == 0)
        {
            throw new InvalidDataException("Missing occupied/last-GC memory stream.");
        }

        metrics.Add("managed.peak-occupied.B", samples.Max(sample => sample.OccupiedBytes));
        metrics.Add("managed.peak-lastGC-heap.B", samples.Max(sample => sample.HeapBytes));
        metrics.Add("managed.peak-lastGC-fragmented.B", samples.Max(sample => sample.FragmentedBytes));
        metrics.Add("managed.peak-lastGC-committed.B", samples.Max(sample => sample.CommittedBytes));
        ProcessMemorySample[] processSamples = WorkSamples(batch, trial.ProcessId, trial.ProcessStartUtcTicks);
        metrics.Add("sampling.managed.count", samples.Length);
        metrics.Add("sampling.process.count", processSamples.Length);
        metrics.Add("sampling.managed-mean-interval.ms",
            (samples[^1].Timestamp - samples[0].Timestamp) * 1000.0 / batch.Frequency / (samples.Length - 1));
        metrics.Add("sampling.process-mean-interval.ms",
            (processSamples[^1].Timestamp - processSamples[0].Timestamp) * 1000.0 / batch.Frequency / (processSamples.Length - 1));
        foreach (Footprint point in trial.Checkpoints)
        {
            metrics.Add("process." + point.Name + "-working.B", point.WorkingSetBytes);
            metrics.Add("process." + point.Name + "-private.B", point.PrivateBytes);
            metrics.Add("managed." + point.Name + "-occupied.B", point.Managed.OccupiedBytes);
            metrics.Add("managed." + point.Name + "-lastGC-heap.B", point.Managed.HeapBytes);
            metrics.Add("managed." + point.Name + "-lastGC-fragmented.B", point.Managed.FragmentedBytes);
            metrics.Add("managed." + point.Name + "-lastGC-committed.B", point.Managed.CommittedBytes);
        }

        foreach ((string label, string from, string to) in new[]
        {
            ("loaded-idle-natural", "idle-natural", "loaded-natural"),
            ("loaded-idle-forced", "idle-forced", "loaded-forced"),
            ("post-loaded-natural", "loaded-natural", "post-settled"),
            ("post-loaded-forced", "loaded-forced", "post-forced"),
        })
        {
            Footprint before = trial.Checkpoints.Single(point => point.Name == from);
            Footprint after = trial.Checkpoints.Single(point => point.Name == to);
            metrics.Add("retention." + label + "-working.B", after.WorkingSetBytes - before.WorkingSetBytes);
            metrics.Add("retention." + label + "-private.B", after.PrivateBytes - before.PrivateBytes);
            metrics.Add("retention." + label + "-occupied.B", after.Managed.OccupiedBytes - before.Managed.OccupiedBytes);
        }

        return metrics;
    }

    /// <summary>Checks local raw bytes only; application/runtime binaries are never loaded or executed by rendering.</summary>
    public static void VerifyReference(string runDirectory, EvidenceReference reference)
    {
        _ = ValidateEvidenceLength(reference.Length);
        string path = EvidencePath(runDirectory, reference.Path);
        FileInfo file = new(path);
        if (!file.Exists || file.Length != reference.Length ||
            reference.Sha256.Length != 64 || !reference.Sha256.All(Uri.IsHexDigit))
        {
            throw new InvalidDataException("Missing/invalid raw reference: " + reference.Path);
        }

        using FileStream stream = File.OpenRead(path);
        if (!string.Equals(Convert.ToHexString(SHA256.HashData(stream)), reference.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Raw evidence hash changed: " + reference.Path);
        }
    }

    /// <summary>Rechecks the full semantic matrix, every calibration profile and failed coverage attempt from saved data.</summary>
    public static async Task<string[]> ValidateCalibrationAsync(
        string runDirectory, FrozenMeasurement frozen, CancellationToken token) =>
        await ValidateCalibrationAsync(runDirectory, frozen, null, token);

    private static async Task<string[]> ValidateCalibrationAsync(
        string runDirectory, FrozenMeasurement frozen, Dictionary<string, ClockSummary>? clocks, CancellationToken token)
    {
        List<string> errors = [];
        try
        {
            ExperimentManifest manifest = await ReadAsync<ExperimentManifest>(runDirectory, "manifest.json", token);
            PreparedExperiment prepared = await ReadAsync<PreparedExperiment>(runDirectory, "prepared.json", token);
            ValidateFrozen(runDirectory, frozen, manifest, prepared);
            Dictionary<string, WorkloadCorpus> corpora = await ReadCorporaAsync(runDirectory, prepared, token);
            using JsonDocument environment = await ReadDocumentAsync(runDirectory, frozen.Environment.Path, token);
            Require(environment.RootElement.GetProperty("Origin").GetString() == LiveOrigin &&
                environment.RootElement.GetProperty("RunnerRuntime").GetString() == "8.0.30" &&
                JsonSerializer.Serialize(environment.RootElement.GetProperty("Conditions").Deserialize<MachineConditions>(_json), _json) ==
                    JsonSerializer.Serialize(manifest.Conditions, _json),
                "Missing or synthetic saved machine/build/runtime conditions.");
            foreach (FrozenCaseInput input in frozen.Inputs)
            {
                TrialInputs requests = await ReadAsync<TrialInputs>(runDirectory, input.File.Path, token);
                string HashOrder(WorkloadRequest[] operations) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(
                    operations.Select(request => new WorkloadInputSeal(request.Number, request.Method, request.RelativeUrl, request.Prefer,
                        request.Body?.Length ?? 0, Convert.ToHexString(SHA256.HashData(request.Body ?? [])))).ToArray(), _json)));
                Require(input.WarmupOrderSha256 == HashOrder(requests.Warmup) && input.MeasuredOrderSha256 == HashOrder(requests.Measured) &&
                    input.WarmupPayloadBytes == requests.Warmup.Sum(request => (long)(request.Body?.Length ?? 0)) &&
                    input.MeasuredPayloadBytes == requests.Measured.Sum(request => (long)(request.Body?.Length ?? 0)),
                    "Frozen request order/payload ledger differs.");
            }

            ClockObservation lastFinished = await ValidateSemanticsAsync(runDirectory, frozen, prepared, corpora, clocks, token);
            Require(frozen.Calibration.Length == 72 &&
                frozen.Calibration.Select(profile => profile.RowKey).SequenceEqual(Workloads.CreateCases().Select(workload => workload.RowKey)),
                "Missing or reordered calibration profile inventory.");
            HashSet<(int, long)> lifetimes = [];
            HashSet<string> attemptIds = new(StringComparer.Ordinal);
            int ordinal = 0;
            foreach (CalibrationProfile profile in frozen.Calibration)
            {
                FrozenCaseInput input = frozen.Inputs.Single(item => item.Case.RowKey == profile.RowKey);
                Require(profile.Operations == input.Operations && profile.NormalAttempts.Length == 2 &&
                    profile.SamplerOffAttempts.Length == 2 && profile.Attempts.Length >= 4, "Incomplete paired calibration.");
                List<TrialRecord> trials = [];
                foreach (EvidenceReference reference in profile.Attempts)
                {
                    VerifyReference(runDirectory, reference);
                    TrialRecord trial = await ReadAsync<TrialRecord>(runDirectory, reference.Path, token);
                    Require(trial.Origin == LiveOrigin && trial.Plan.Purpose is "pilot" or "sampler-off" &&
                        trial.FrozenSha256 is null && trial.RunId == frozen.RunId && trial.PreparedSha256 == frozen.PreparedSha256 &&
                        trial.Plan.RowKey == profile.RowKey && trial.Plan.Ordinal == ++ordinal &&
                        trial.Parent == frozen.Parent && ClockObservation.IsOrdered(lastFinished, trial.Started) &&
                        ClockObservation.IsOrdered(trial.Finished, frozen.Frozen) &&
                        lifetimes.Add((trial.ProcessId, trial.ProcessStartUtcTicks)) && attemptIds.Add(trial.Plan.AttemptId),
                        "Calibration is synthetic, mixed, overlapping or reuses a child.");
                    lastFinished = trial.Finished;
                    bool adequate = trial.Status == "passed";
                    Require(adequate || trial.Status == "coverage-insufficient" && trial.Plan.Purpose == "pilot",
                        "Failed calibration cannot be silently dropped.");
                    string[] metrics = ValidateTrial(trial, requireCoverage: adequate);
                    Require(metrics.Length == 0, "Calibration metrics: " + string.Join("; ", metrics));
                    if (!adequate)
                    {
                        string[] inadequacy = ValidateTrial(trial);
                        Require(inadequacy.Any(error => error.StartsWith("coverage:", StringComparison.Ordinal)) &&
                            trial.Failure is not null, "A coverage retry lacks retained actual inadequacy.");
                    }

                    PopulationSummary population = await ValidateRawTrialAsync(runDirectory, trial, manifest, prepared, corpora, token);
                    clocks?.Add(trial.Plan.RelativeDirectory, population.Clocks);
                    trials.Add(trial);
                }

                TrialRecord[] normal = trials.Where(trial => profile.NormalAttempts.Contains(trial.Plan.AttemptId, StringComparer.Ordinal)).ToArray();
                TrialRecord[] off = trials.Where(trial => profile.SamplerOffAttempts.Contains(trial.Plan.AttemptId, StringComparer.Ordinal)).ToArray();
                foreach ((TrialRecord[] pair, bool sampling, string purpose) in new[]
                {
                    (normal, true, "pilot"), (off, false, "sampler-off"),
                })
                {
                    Require(pair.Length == 2 && pair.Select(trial => trial.Plan.Variant).Order(StringComparer.Ordinal)
                        .SequenceEqual(new[] { "baseline", "candidate" }) &&
                        pair.All(trial => trial.Status == "passed" && trial.Plan.Operations == profile.Operations &&
                            trial.Plan.Purpose == purpose && trial.Plan.Sampling == sampling &&
                            trial.Inputs.Sha256 == input.File.Sha256), "Missing adequate paired normal/off calibration at frozen N.");
                }

                int expectedCount = frozen.Settings.InitialCount(input.Case);
                TrialRecord[] pilots = trials.Where(trial => trial.Plan.Purpose == "pilot").ToArray();
                foreach (TrialRecord[] pair in pilots.Chunk(2))
                {
                    Require(pair.Length == 2 && pair.All(trial => trial.Plan.Operations == expectedCount) &&
                        pair.Select(trial => trial.Plan.Variant).Distinct(StringComparer.Ordinal).Count() == 2 &&
                        (pair.All(trial => trial.Status == "passed") == (expectedCount == profile.Operations)),
                        "Changed pilot counts, dropped failed pair, or best-of calibration selection.");
                    expectedCount *= 2;
                }
            }

            HashSet<string> indexed = frozen.Calibration.SelectMany(profile => profile.Attempts)
                .Select(reference => reference.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
            Require(Directory.EnumerateFiles(EvidencePath(runDirectory, "calibration"), "trial.json", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(runDirectory, path)).ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(indexed),
                "Unindexed calibration attempts exist.");
            Require(!File.Exists(EvidencePath(runDirectory, @"calibration\failed.json")), "Calibration has a retained failure marker.");
            using JsonDocument completed = await ReadDocumentAsync(runDirectory, @"calibration\completed.json", token);
            Require(completed.RootElement.GetProperty("Passed").GetBoolean() &&
                completed.RootElement.GetProperty("RequiredProfiles").GetInt32() == 72 &&
                JsonSerializer.Serialize(frozen.Calibration, _json) ==
                JsonSerializer.Serialize(completed.RootElement.GetProperty("Profiles").Deserialize<CalibrationProfile[]>(_json), _json),
                "Calibration completion receipt differs from the frozen profiles.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            errors.Add("Calibration/semantic gate: " + exception.Message);
        }

        return errors.ToArray();
    }

    /// <summary>Independently validates every required cell, metric, raw reference and oracle, not an exit code or markdown file.</summary>
    public static async Task<CompletenessResult> ValidateCompletenessAsync(string runDirectory, CancellationToken token)
    {
        ReportEvidence evidence = await InspectAsync(runDirectory, token);
        return new(evidence.Errors.Count == 0, evidence.Errors.ToArray());
    }

    /// <summary>Re-renders only saved evidence to the requested absolute path, including an explicit incomplete report on failure.</summary>
    public static async Task<CompletenessResult> RenderAsync(string runDirectory, CancellationToken token)
    {
        (string markdown, CompletenessResult result) = await RenderSavedAsync(runDirectory, token);
        string expectedRoot = Path.GetFullPath(ExperimentRunner.DataRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        Require(Path.GetFullPath(runDirectory).StartsWith(expectedRoot, StringComparison.OrdinalIgnoreCase),
            "Only ignored experiment run directories can write the requested performance report.");
        await File.WriteAllTextAsync(ExperimentRunner.RequiredReportPath, markdown, new UTF8Encoding(false), token);
        return result;
    }

    /// <summary>
    /// Evidence-only rendering seam. Tests may render synthetic/incomplete saved data to a string,
    /// but it cannot be marked complete and never writes the requested live performance report.
    /// </summary>
    public static async Task<(string Markdown, CompletenessResult Result)> RenderSavedAsync(string runDirectory, CancellationToken token)
    {
        ReportEvidence evidence = await InspectAsync(runDirectory, token);
        CompletenessResult result = new(evidence.Errors.Count == 0, evidence.Errors.ToArray());
        return (Render(evidence, runDirectory), result);
    }

    private static async Task<ReportEvidence> InspectAsync(string root, CancellationToken token)
    {
        ReportEvidence evidence = new();
        try
        {
            evidence.Manifest = await ReadAsync<ExperimentManifest>(root, "manifest.json", token);
            evidence.Prepared = await ReadAsync<PreparedExperiment>(root, "prepared.json", token);
            evidence.Frozen = await ReadAsync<FrozenMeasurement>(root, "frozen-measurement.json", token);
            evidence.Errors.AddRange(await ValidateCalibrationAsync(root, evidence.Frozen, evidence.Clocks, token));
            Dictionary<string, WorkloadCorpus> corpora = new(StringComparer.Ordinal);
            try
            {
                evidence.EnvironmentJson = Encoding.UTF8.GetString(await ReadBytesAsync(root, evidence.Frozen.Environment.Path, token));
                corpora = await ReadCorporaAsync(root, evidence.Prepared, token);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // Continue to show readable attempted/failed records; missing prerequisites still block every result.
                evidence.Errors.Add("Saved prerequisites: " + exception.Message);
            }
            MeasurementLedger ledger = await ReadAsync<MeasurementLedger>(root, @"measurement\ledger.json", token);
            evidence.Ledger = ledger;
            string frozenHash = HashFile(EvidencePath(root, "frozen-measurement.json"));
            Require(ledger.SchemaVersion == 3 && ledger.RunId == evidence.Frozen.RunId &&
                ledger.FrozenSha256 == frozenHash && ledger.Parent.IsValid() &&
                ClockObservation.IsOrdered(ledger.Started, ledger.Finished), "Wrong measurement ledger identity/time.");
            Check(evidence.Errors, ledger.Failure is null && ledger.NotAttempted.Length == 0, "Failed or interrupted measurement schedule.");
            HashSet<(int, long)> calibrationLifetimes = [];
            foreach (EvidenceReference reference in evidence.Frozen.Calibration.SelectMany(profile => profile.Attempts))
            {
                try
                {
                    TrialRecord calibration = await ReadAsync<TrialRecord>(root, reference.Path, token);
                    evidence.Calibration.Add(calibration);
                    calibrationLifetimes.Add((calibration.ProcessId, calibration.ProcessStartUtcTicks));
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    evidence.Errors.Add(reference.Path + ": " + exception.Message);
                }
            }

            foreach (EvidenceReference reference in ledger.Attempts)
            {
                try
                {
                    VerifyReference(root, reference);
                    TrialRecord trial = await ReadAsync<TrialRecord>(root, reference.Path, token);
                    evidence.Trials.Add(trial);
                    Require(trial.Origin == LiveOrigin && trial.FrozenSha256 == frozenHash &&
                        !calibrationLifetimes.Contains((trial.ProcessId, trial.ProcessStartUtcTicks)) &&
                        trial.Parent == ledger.Parent && ClockObservation.IsOrdered(ledger.Started, trial.Started) &&
                        ClockObservation.IsOrdered(trial.Finished, ledger.Finished),
                        "Measurement is synthetic, reused calibration, or has mixed freeze identity.");
                    evidence.Populations[trial.Plan.AttemptId] =
                        await ValidateRawTrialAsync(root, trial, evidence.Manifest, evidence.Prepared, corpora, token);
                    evidence.Clocks.Add(trial.Plan.RelativeDirectory, evidence.Populations[trial.Plan.AttemptId].Clocks);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    evidence.Errors.Add(reference.Path + ": " + exception.Message);
                }
            }

            HashSet<string> attemptedFiles = ledger.Attempts.Select(reference => reference.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
            Require(Directory.EnumerateFiles(EvidencePath(root, "trials"), "trial.json", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(root, path)).ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(attemptedFiles),
                "Unindexed measured attempts exist.");
            evidence.Errors.AddRange(ValidateMatrix(evidence.Frozen, evidence.Trials));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            evidence.Errors.Add(exception.GetType().Name + ": " + exception.Message);
        }

        return evidence;
    }

    private static void ValidateFrozen(
        string root, FrozenMeasurement frozen, ExperimentManifest manifest, PreparedExperiment prepared)
    {
        ExperimentRunner.ValidateManifest(manifest);
        Require(frozen.Origin == LiveOrigin && frozen.SchemaVersion == 3 && frozen.ContractVersion == ContractVersion &&
            frozen.Frozen.IsValid() && frozen.Parent.IsValid() &&
            frozen.RunId == manifest.RunId && prepared.RunId == manifest.RunId &&
            Path.GetFullPath(root).Equals(Path.GetFullPath(manifest.OutputDirectory), StringComparison.OrdinalIgnoreCase) &&
            frozen.ManifestSha256 == HashFile(EvidencePath(root, "manifest.json")) &&
            frozen.PreparedSha256 == HashFile(EvidencePath(root, "prepared.json")) &&
            prepared.ManifestSha256 == frozen.ManifestSha256 && prepared.SchemaVersion == 3 &&
            prepared.ProtocolVersion == ProbeProtocol.Version && frozen.ProbeSha256 == prepared.ProbeSha256 &&
            prepared.Baseline.SourceRevision == ExperimentRunner.BaselineRevision &&
            prepared.Candidate.SourceRevision == ExperimentRunner.CandidateRevision &&
            prepared.Baseline.FirelyPackageVersion == "5.11.4" && prepared.Candidate.FirelyPackageVersion == "6.2.1" &&
            prepared.Frameworks.Length == 2 && prepared.Frameworks.All(framework => framework.Version == "8.0.30") &&
            prepared.ToolFiles.Count >= 4 && prepared.ToolFiles.Values.All(file => file.Sha256.Length == 64) &&
            prepared.PreparedUtc != default, "Frozen app/tool/probe/runtime/manifest identity is incomplete or mixed.");
        Require(JsonSerializer.Serialize(manifest.Measurement, _json) == JsonSerializer.Serialize(frozen.Settings, _json) &&
            JsonSerializer.Serialize(manifest.Bounds, _json) == JsonSerializer.Serialize(frozen.Bounds, _json) &&
            manifest.Conditions is not null && manifest.Conditions.PhysicalMemoryBytes > 0,
            "Missing real conditions or changed common measurement configuration.");
        frozen.Settings.Validate();
        Require(frozen.Inputs.Select(input => input.Case).SequenceEqual(Workloads.CreateCases()), "Frozen inputs do not cover the exact 72 rows.");
        Dictionary<string, int> counts = frozen.Inputs.ToDictionary(input => input.Case.RowKey, input => input.Operations, StringComparer.Ordinal);
        Require(frozen.Schedule.SequenceEqual(CreateSchedule(counts, frozen.Settings.TrialsPerVariant)), "Frozen schedule changed.");
        VerifyReference(root, frozen.Environment);
        foreach (FrozenCaseInput input in frozen.Inputs)
        {
            VerifyReference(root, input.File);
            Require(IsPilotCount(input.Operations, frozen.Settings.InitialCount(input.Case), frozen.Settings.MaximumPilotOperations),
                "An accepted count is not a common bounded pilot count.");
        }
    }

    private static async Task<Dictionary<string, WorkloadCorpus>> ReadCorporaAsync(
        string root, PreparedExperiment prepared, CancellationToken token)
    {
        Require(prepared.Workload.ContractVersion == Workloads.ContractVersion &&
            prepared.Workload.Algorithm == Workloads.Algorithm && prepared.Workload.Seed == Workloads.Seed &&
            prepared.Workload.PreflightOperations == 4 && prepared.Workload.Corpora.Length == 6 &&
            prepared.Workload.Cases.Select(fixture => fixture.Case).SequenceEqual(Workloads.CreateCases()) &&
            prepared.FixtureFiles.Length == 78, "Incomplete original workload seal.");
        foreach (FileSeal seal in prepared.FixtureFiles)
        {
            VerifyReference(root, new(Path.Combine("fixtures", seal.RelativePath), seal.Length, seal.Sha256));
        }

        Dictionary<string, WorkloadCorpus> corpora = new(StringComparer.Ordinal);
        foreach (CorpusFixture fixture in prepared.Workload.Corpora)
        {
            WorkloadCorpus corpus = await ReadAsync<WorkloadCorpus>(root, Path.Combine("fixtures", fixture.FixtureFile), token);
            CorrectnessOracle.ValidateCorpus(corpus);
            Require(corpus.Key == fixture.Key && corpus.Resources.Sum(resource => (long)resource.Body.Length) == fixture.PayloadBytes,
                "Saved corpus sizes differ.");
            corpora.Add(corpus.Key, corpus);
        }

        return corpora;
    }

    private static async Task<ClockObservation> ValidateSemanticsAsync(
        string root, FrozenMeasurement frozen, PreparedExperiment prepared, Dictionary<string, WorkloadCorpus> corpora,
        Dictionary<string, ClockSummary>? clocks, CancellationToken token)
    {
        foreach (EvidenceReference reference in frozen.SemanticFiles)
        {
            Require(reference.Path.StartsWith("preflight" + Path.DirectorySeparatorChar, StringComparison.Ordinal),
                "A semantic seal escapes preflight.");
            VerifyReference(root, reference);
        }

        HashSet<string> paths = frozen.SemanticFiles.Select(reference => reference.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Require(paths.Count == frozen.SemanticFiles.Length && Directory.EnumerateFiles(EvidencePath(root, "preflight"), "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(root, path)).ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(paths), "Semantic files were added/dropped.");
        using JsonDocument completed = await ReadDocumentAsync(root, @"preflight\completed.json", token);
        Require(completed.RootElement.GetProperty("SuccessfulPairedRows").GetInt32() == 72 &&
            completed.RootElement.GetProperty("SuccessfulArtifactCases").GetInt32() == 144 &&
            completed.RootElement.GetProperty("MeasuredTrials").GetInt32() == 0, "The full semantic gate is missing.");
        using JsonDocument failure = await ReadDocumentAsync(root, @"preflight\failures.json", token);
        Require(failure.RootElement.GetProperty("Failure").ValueKind == JsonValueKind.Null &&
            failure.RootElement.GetProperty("FailedAttempts").GetArrayLength() == 0 &&
            failure.RootElement.GetProperty("NotAttempted").GetArrayLength() == 0, "Semantic failures were retained; comparison is blocked.");
        using JsonDocument seal = await ReadDocumentAsync(root, @"preflight\input-seal.json", token);
        Require(seal.RootElement.GetProperty("PreparedSha256").GetString() == frozen.PreparedSha256 &&
            seal.RootElement.GetProperty("ProbeSha256").GetString() == frozen.ProbeSha256 &&
            seal.RootElement.GetProperty("ContractVersion").GetString() == Workloads.ContractVersion,
            "Semantic gate used different tools/inputs.");
        using JsonDocument matrix = await ReadDocumentAsync(root, @"preflight\matrix.json", token);
        JsonElement rows = matrix.RootElement.GetProperty("Rows");
        Require(matrix.RootElement.GetProperty("SemanticGatePassed").GetBoolean() && rows.GetArrayLength() == 72 &&
            rows.EnumerateArray().Select(row => row.GetProperty("RowKey").GetString())
                .SequenceEqual(Workloads.CreateCases().Select(workload => workload.RowKey)), "Wrong semantic case matrix.");
        HashSet<(int, long)> lifetimes = [];
        ClockObservation? lastFinished = null;
        foreach (JsonElement row in rows.EnumerateArray())
        {
            string key = row.GetProperty("RowKey").GetString()!;
            WorkloadCase workload = prepared.Workload.Cases.Single(fixture => fixture.Case.RowKey == key).Case;
            foreach (string variant in new[] { "baseline", "candidate" })
            {
                JsonElement attempt = row.GetProperty(variant == "baseline" ? "Baseline" : "Candidate");
                JsonElement progress = attempt.GetProperty("Progress");
                int count = workload.PatientCount + workload.ObservationCount;
                int pid = progress.GetProperty("ProcessId").GetInt32();
                long started = progress.GetProperty("ProcessStartedUtc").GetDateTimeOffset().UtcTicks;
                ClockObservation attemptStart = ReadClock(attempt, "Started");
                ClockObservation attemptEnd = ReadClock(attempt, "Finished");
                Require(attempt.GetProperty("Parent").Deserialize<ParentIdentity>(_json) == frozen.Parent &&
                    ClockObservation.IsOrdered(attemptStart, attemptEnd) &&
                    ClockObservation.IsOrdered(attemptEnd, frozen.Frozen) &&
                    (lastFinished is null || ClockObservation.IsOrdered(lastFinished.Value, attemptStart)),
                    "Semantic children overlap or lack parent monotonic lifecycle evidence.");
                lastFinished = attemptEnd;
                Require(attempt.GetProperty("Status").GetString() == "passed" && progress.GetProperty("ValidatedSeedWrites").GetInt32() == count &&
                    progress.GetProperty("ValidatedSeedReads").GetInt32() == count &&
                    progress.GetProperty("CompletedOperations").GetInt32() == 4 &&
                    progress.GetProperty("ValidatedOperations").GetInt32() == 4 && pid > 0 && lifetimes.Add((pid, started)),
                    "Missing successful full-scale semantic attempt.");
                string directory = Path.Combine("preflight", key, variant);
                using JsonDocument oracle = await ReadDocumentAsync(root, Path.Combine(directory, "oracle.json"), token);
                Require(oracle.RootElement.GetProperty("Passed").GetBoolean() &&
                    oracle.RootElement.GetProperty("LogicalOperations").GetInt32() == 4 &&
                    oracle.RootElement.GetProperty("CompletedOperations").GetInt32() == 4, "Semantic oracle/denominator differs.");
                (ClockObservation launched, ClockObservation exited) =
                    await ValidateExitAsync(root, directory, pid, started, frozen.Parent, attemptStart, attemptEnd, token);
                using JsonDocument seed = await ReadDocumentAsync(root, Path.Combine(directory, "seed-audit.json"), token);
                WorkloadCorpus corpus = corpora[prepared.Workload.Cases.Single(fixture => fixture.Case == workload).CorpusKey];
                _ = ReadSeedResources(seed.RootElement, corpus, variant, attemptStart, attemptEnd);
                List<ResponseClock> responses = [];
                await AddSavedResponsesAsync(root, directory, "readiness-", responses, token);
                AddSeedClocks(seed.RootElement, responses);
                foreach (string prefix in new[] { "update-preexisting-read", "case-", "audit-read-", "audit-bundle-", "cleanup-delete-" })
                {
                    await AddSavedResponsesAsync(root, directory, prefix, responses, token);
                }

                ClockSummary summary = ValidateResponseClocks(responses, launched, exited);
                clocks?.Add(directory, summary);
            }
        }

        return lastFinished ?? throw new InvalidDataException("Missing semantic lifecycle evidence.");
    }

    private static async Task<PopulationSummary> ValidateRawTrialAsync(
        string root, TrialRecord trial, ExperimentManifest manifest, PreparedExperiment prepared,
        Dictionary<string, WorkloadCorpus> corpora, CancellationToken token)
    {
        Require(trial.RawFiles.Length > 0 && trial.RawFiles.Select(reference => reference.Path).Distinct(StringComparer.OrdinalIgnoreCase)
            .Count() == trial.RawFiles.Length, "Missing/duplicate raw file inventory.");
        string prefix = trial.Plan.RelativeDirectory + Path.DirectorySeparatorChar;
        foreach (EvidenceReference reference in trial.RawFiles)
        {
            Require(reference.Path.StartsWith(prefix, StringComparison.Ordinal) &&
                reference.Path != Path.Combine(trial.Plan.RelativeDirectory, "trial.json"), "Raw evidence escapes its child or seals itself.");
            VerifyReference(root, reference);
        }

        HashSet<string> actual = Directory.EnumerateFiles(EvidencePath(root, trial.Plan.RelativeDirectory), "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(root, path)).Where(path => path != Path.Combine(trial.Plan.RelativeDirectory, "trial.json"))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        Require(actual.SetEquals(trial.RawFiles.Select(reference => reference.Path)), "Unindexed child evidence or failures exist.");
        string directory = trial.Plan.RelativeDirectory;
        foreach (string name in new[] { "seals-before.json", "seals-after.json" })
        {
            using JsonDocument seal = await ReadDocumentAsync(root, Path.Combine(directory, name), token);
            Require(seal.RootElement.GetProperty("Matched").GetBoolean() &&
                seal.RootElement.GetProperty("PreparedSha256").GetString() == trial.PreparedSha256 &&
                seal.RootElement.GetProperty("ProbeSha256").GetString() == prepared.ProbeSha256 &&
                seal.RootElement.GetProperty("ProtocolVersion").GetString() == ProbeProtocol.Version &&
                seal.RootElement.GetProperty("ContractVersion").GetString() == ContractVersion,
                "Missing before/after artifact/tool/protocol seal.");
        }

        using JsonDocument handshake = await ReadDocumentAsync(root, Path.Combine(directory, "handshake.json"), token);
        JsonElement peer = handshake.RootElement;
        Require(peer.GetProperty("ProtocolVersion").GetString() == ProbeProtocol.Version &&
            peer.GetProperty("ProbeSha256").GetString() == prepared.ProbeSha256 &&
            peer.GetProperty("ChildProcessId").GetInt32() == trial.ProcessId &&
            peer.GetProperty("ChildStartTimeUtcTicks").GetInt64() == trial.ProcessStartUtcTicks &&
            peer.GetProperty("ParentProcessId").GetInt32() == trial.Parent.ProcessId &&
            peer.GetProperty("ParentStartTimeUtcTicks").GetInt64() == trial.Parent.ProcessStartUtcTicks &&
            peer.GetProperty("OsVerifiedPeerProcessId").GetInt32() == trial.ProcessId &&
            peer.GetProperty("CurrentUserOnly").GetBoolean() && peer.GetProperty("NonceMatched").GetBoolean(),
            "Missing actual OS/nonce/probe/lifetime authentication.");
        (ClockObservation launched, ClockObservation exited) = await ValidateExitAsync(
            root, directory, trial.ProcessId, trial.ProcessStartUtcTicks, trial.Parent, trial.Started, trial.Finished, token);
        using JsonDocument launch = await ReadDocumentAsync(root, Path.Combine(directory, "launch.json"), token);
        Require(launch.RootElement.GetProperty("Executable").GetString() == manifest.Runtime.DotnetHostPath &&
            launch.RootElement.GetProperty("ProbeSha256").GetString() == prepared.ProbeSha256 &&
            launch.RootElement.GetProperty("EnvironmentAllowlist").GetProperty("DOTNET_ENVIRONMENT").GetString() == "Production",
            "Missing actual launch/environment evidence.");
        JsonElement limits = launch.RootElement.GetProperty("ProtocolLimits");
        JsonElement configuration = launch.RootElement.GetProperty("ProbeConfiguration");
        ClockObservation issued = ReadClock(configuration, "Issued");
        ClockObservation requested = ReadClock(launch.RootElement, "Requested");
        long deadline = configuration.GetProperty("DeadlineTimestamp").GetInt64();
        Require(JsonSerializer.Serialize(launch.RootElement.GetProperty("Bounds").Deserialize<ExperimentBounds>(_json), _json) ==
                JsonSerializer.Serialize(manifest.Bounds, _json) &&
            limits.GetProperty("Version").GetString() == ProbeProtocol.Version &&
            limits.GetProperty("ContractVersion").GetString() == ContractVersion &&
            limits.GetProperty("ChildLifetimeSeconds").GetInt32() == ProbeProtocol.ChildLifetimeSeconds &&
            limits.GetProperty("StoppedExportSeconds").GetInt32() == ProbeProtocol.StoppedExportSeconds &&
            limits.GetProperty("BoundaryAllowanceSeconds").GetInt32() == ProbeProtocol.BoundaryAllowanceSeconds &&
            limits.GetProperty("SampleCapacity").GetInt32() == ProbeProtocol.SampleCapacity &&
            limits.GetProperty("SampleBoundarySlots").GetInt32() == ProbeProtocol.SampleBoundarySlots &&
            limits.GetProperty("MaximumRequestBytes").GetInt32() == ProbeProtocol.MaximumRequestBytes &&
            limits.GetProperty("MaximumResponseBytes").GetInt32() == ProbeProtocol.MaximumResponseBytes &&
            limits.GetProperty("MaximumCommands").GetInt32() == ProbeProtocol.MaximumCommands &&
            limits.GetProperty("MaximumIdentities").GetInt32() == ProbeProtocol.MaximumIdentities &&
            limits.GetProperty("MaximumEvidenceBytes").GetInt32() == MaximumEvidenceBytes &&
            limits.GetProperty("MaximumHttpBytes").GetInt32() == 4 * 1024 * 1024 &&
            configuration.GetProperty("RequestTimeoutSeconds").GetInt32() == ProbeProtocol.RequestSeconds &&
            ClockObservation.IsOrdered(issued, requested) &&
            issued.Before <= long.MaxValue - (issued.Frequency * ProbeProtocol.ChildLifetimeSeconds) &&
            deadline == issued.Before + (issued.Frequency * ProbeProtocol.ChildLifetimeSeconds) && exited.After < deadline,
            "Missing or incompatible common launch capacity/deadline evidence.");
        VerifyReference(root, trial.Inputs);
        TrialInputs inputs = await ReadAsync<TrialInputs>(root, trial.Inputs.Path, token);
        CaseFixture fixture = prepared.Workload.Cases.Single(item => item.Case.RowKey == trial.Plan.RowKey);
        WorkloadCase workload = fixture.Case;
        WorkloadCorpus corpus = corpora[fixture.CorpusKey];
        CorrectnessOracle.ValidateCaseRequests(workload, corpus, inputs.Warmup, 128);
        CorrectnessOracle.ValidateCaseRequests(workload, corpus, inputs.Measured, trial.Plan.Operations, 128);
        ProbeSnapshot initial = await ReadSnapshotAsync(root, directory, "ready", trial, manifest, prepared, workload, token);
        _ = await ReadSnapshotAsync(root, directory, "probe-warmed", trial, manifest, prepared, workload, token);
        ProbeSnapshot warmed = await ReadSnapshotAsync(root, directory, "warm-audited", trial, manifest, prepared, workload, token);
        ProbeSnapshot loaded = await ReadSnapshotAsync(root, directory, "warm-prepared", trial, manifest, prepared, workload, token);
        ProbeSnapshot post = await ReadSnapshotAsync(root, directory, "post-audit", trial, manifest, prepared, workload, token);
        string source = Path.Combine(manifest.OutputDirectory, directory, "child", "empty-source");
        Dictionary<string, bool> noIdentities = new(StringComparer.Ordinal);
        CorrectnessOracle.ValidateState(initial.Tenant, initial.Tenant, 0, 0, 0, noIdentities, source);
        using JsonDocument seed = await ReadDocumentAsync(root, Path.Combine(directory, "seed-audit.json"), token);
        Dictionary<string, ExpectedResource> resources = ReadSeedResources(
            seed.RootElement, corpus, trial.Plan.Variant, trial.Started, trial.Finished);
        SuccessfulStatusProfile profile = CorrectnessOracle.StatusProfile(trial.Plan.Variant);
        OperationResult[] warmRaw = await ReadAsync<OperationResult[]>(root, Path.Combine(directory, "warmup-raw.json"), token);
        OperationResult[] measuredRaw = await ReadAsync<OperationResult[]>(root, Path.Combine(directory, "measured-raw.json"), token);
        Require(warmRaw.Length == 128 && measuredRaw.Length == trial.Plan.Operations &&
            warmRaw.All(result => result.Timing.Completed && result.Timing.HttpRequests == result.Exchanges.Length &&
                result.AttemptedHttpRequests == result.Exchanges.Length && result.IncompleteResponse is null && result.Failure is null) &&
            measuredRaw.All(result => result.Timing.Completed && result.Timing.HttpRequests == result.Exchanges.Length &&
                result.AttemptedHttpRequests == result.Exchanges.Length && result.IncompleteResponse is null && result.Failure is null) &&
            trial.Batch is not null && measuredRaw.Select(result => result.Timing).SequenceEqual(trial.Batch.Operations) &&
            warmRaw[^1].Timing.End < trial.Batch.Server.Begin.Timestamp, "Incomplete raw logical batch/order/timing.");
        TrialAudit audit = await ReadAsync<TrialAudit>(root, Path.Combine(directory, "audit.json"), token);
        Require(audit.Passed && trial.OraclePassed, "Missing full raw oracle.");
        ValidateOperationResponses(warmRaw, trial.Started.Frequency, launched.After, trial.Batch.Server.Begin.Timestamp);
        ValidateOperationResponses(measuredRaw, trial.Batch.Frequency, trial.Batch.ClientStart, trial.Batch.ClientEnd);
        List<ResponseClock> responseClocks = [];
        await AddSavedResponsesAsync(root, directory, "readiness-", responseClocks, token);
        AddSeedClocks(seed.RootElement, responseClocks);
        AddOperationClocks("warmup", warmRaw, responseClocks);
        AddAuditClocks(audit.WarmupReadbacks.Where(read => read.Method == "GET"), responseClocks);
        AddAuditClocks(audit.WarmupDeletes, responseClocks);
        if (audit.Restoration is not null)
        {
            AddAuditClocks([audit.Restoration], responseClocks);
        }

        AddAuditClocks(audit.WarmupReadbacks.Where(read => read.Method == "GET-restored"), responseClocks);
        AddOperationClocks("measured", measuredRaw, responseClocks);
        AddAuditClocks(audit.FinalReadbacks, responseClocks);
        ClockSummary clockSummary = ValidateResponseClocks(responseClocks, launched, exited);
        string baseUrl = "http://127.0.0.1:" + manifest.Port.ToString(CultureInfo.InvariantCulture) + "/fhir/perf";
        BatchValidation warm = CorrectnessOracle.ValidateBatchResponses(workload, inputs.Warmup,
            warmRaw.Select(result => new CompletedOperation(result.Timing.Number, result.Exchanges)).ToArray(),
            resources, profile, baseUrl);
        ValidateReadbacks(warm, warm.Receipts, audit.WarmupReadbacks.Where(read => read.Method == "GET").ToArray(), baseUrl);
        CorrectnessOracle.ValidateState(initial.Tenant, warmed.Tenant,
            corpus.PatientCount + (workload.CaseId == "patient-create" ? 128 : 0),
            corpus.ObservationCount + (workload.CaseId == "observation-create" ? 128 : 0),
            warm.Receipts.Length, noIdentities, source);
        CorrectnessOracle.ValidateReceipt(warmed.Tenant, warm.Receipts);
        if (warm.Created.Length != 0)
        {
            string type = warm.Created[0].ResourceType;
            CorrectnessOracle.ValidateCreateGrowth(type, corpus.Resources.Where(resource => resource.ResourceType == type)
                .Select(resource => resource.Id).ToArray(), loaded.Tenant.ResourceCounts[type], warmed.Tenant.ResourceCounts[type],
                128, warm.Created);
        }
        Require(audit.WarmupDeletes.Length == warm.Created.Length, "Missing warm create deletions.");
        for (int index = 0; index < warm.Created.Length; index++)
        {
            ResourceBinding created = warm.Created[index];
            string identity = created.ResourceType + "/" + created.Id;
            AuditInteraction deletion = audit.WarmupDeletes[index];
            Require(deletion.Method == "DELETE" && deletion.Identity == identity, "Warm-up deletion targets changed.");
            CorrectnessOracle.ValidateDeleteResponse(deletion.Response, profile.Delete, warm.Resources[identity]);
            warm.Resources.Remove(identity);
        }

        if (inputs.Restore is WorkloadRequest restore)
        {
            ExpectedResource before = warm.Resources[restore.RelativeUrl];
            ExpectedResource restored = before;
            if (audit.Restoration is AuditInteraction interaction)
            {
                Require(interaction.Method == "PUT" && interaction.Identity == restore.RelativeUrl, "Wrong ordinary restoration.");
                restored = new(restore.Body!, CorrectnessOracle.ValidateUpdateResponse(interaction.Response, profile.Update,
                    before.Content, restore.Body!, before.Binding, baseUrl));
            }

            CorrectnessOracle.ValidateRestoration(workload, corpus, restore, before, restored, audit.Restoration?.Response);
            Require(trial.Warmup is not null && trial.Warmup.OriginalUpdate == resources[restore.RelativeUrl].Binding &&
                trial.Warmup.WarmedUpdate == before.Binding && trial.Warmup.RestoredUpdate == restored.Binding, "Warm metadata/history receipt differs.");
            AuditInteraction read = audit.WarmupReadbacks.Single(item => item.Method == "GET-restored" && item.Identity == restore.RelativeUrl);
            _ = CorrectnessOracle.ValidateResponse(read.Response, 200, restored.Content, baseUrl, restored.Binding);
            warm.Resources[restore.RelativeUrl] = restored;
        }
        else
        {
            Require(audit.Restoration is null && !audit.WarmupReadbacks.Any(read => read.Method == "GET-restored"), "Unexplained restoration.");
        }

        CorrectnessOracle.ValidateState(initial.Tenant, loaded.Tenant, corpus.PatientCount, corpus.ObservationCount,
            warm.Receipts.Length, noIdentities, source);
        CorrectnessOracle.ValidateReceipt(loaded.Tenant, warm.Receipts);
        ValidateIdentityChunks(audit.DeletedIdentityChunks, warm.Created.ToDictionary(
            binding => binding.ResourceType + "/" + binding.Id, _ => false, StringComparer.Ordinal), loaded.Tenant, trial.ProcessId);
        BatchValidation measured = CorrectnessOracle.ValidateBatchResponses(workload, inputs.Measured,
            measuredRaw.Select(result => new CompletedOperation(result.Timing.Number, result.Exchanges)).ToArray(),
            warm.Resources, profile, baseUrl);
        ReceiptExpectation[] receipts = [.. warm.Receipts, .. measured.Receipts];
        ValidateReadbacks(measured, receipts, audit.FinalReadbacks, baseUrl);
        CorrectnessOracle.ValidateState(initial.Tenant, post.Tenant,
            corpus.PatientCount + (workload.CaseId == "patient-create" ? trial.Plan.Operations : 0),
            corpus.ObservationCount + (workload.CaseId == "observation-create" ? trial.Plan.Operations : 0),
            receipts.Length, noIdentities, source);
        CorrectnessOracle.ValidateReceipt(post.Tenant, receipts);
        if (measured.Created.Length != 0)
        {
            string type = measured.Created[0].ResourceType;
            CorrectnessOracle.ValidateCreateGrowth(type, corpus.Resources.Where(resource => resource.ResourceType == type)
                .Select(resource => resource.Id).ToArray(), loaded.Tenant.ResourceCounts[type], post.Tenant.ResourceCounts[type],
                trial.Plan.Operations, measured.Created, warm.Created.Select(binding => binding.Id).ToArray());
        }
        Dictionary<string, bool> expectedHook = new(StringComparer.Ordinal);
        foreach (ReceiptExpectation receipt in receipts)
        {
            expectedHook.Add("Bundle/" + receipt.Notification.BundleId, true);
            expectedHook.Add("Patient/" + receipt.Notification.ContextId, false);
            expectedHook.Add("Observation/" + receipt.Notification.FocusId, false);
        }

        ValidateIdentityChunks(audit.HookIdentityChunks, expectedHook, post.Tenant, trial.ProcessId);
        Require(JsonSerializer.Serialize(warm.Operations, _json) == JsonSerializer.Serialize(audit.WarmupOperations, _json) &&
            JsonSerializer.Serialize(measured.Operations, _json) == JsonSerializer.Serialize(audit.MeasuredOperations, _json),
            "Recomputed full-content oracle differs from the saved receipt.");
        return new(initial.Tenant.ResourceCounts, loaded.Tenant.ResourceCounts, post.Tenant.ResourceCounts,
            loaded.Tenant.ReceiptCount, post.Tenant.ReceiptCount, clockSummary);
    }

    private static void ValidateReadbacks(
        BatchValidation validation, ReceiptExpectation[] receipts, AuditInteraction[] reads, string baseUrl)
    {
        string[] expected = [.. validation.Readbacks, .. receipts.Select(receipt => "Bundle/" + receipt.Notification.BundleId)];
        Require(reads.Length == expected.Length && reads.Select(read => read.Identity).SequenceEqual(expected) &&
            reads.All(read => read.Method == "GET"), "Missing, duplicate or changed full-content read-backs.");
        foreach (AuditInteraction read in reads)
        {
            validation.Resources.TryGetValue(read.Identity, out ExpectedResource? resource);
            ReceiptExpectation? receipt = receipts.SingleOrDefault(item => "Bundle/" + item.Notification.BundleId == read.Identity);
            byte[] content = receipt?.Notification.Body ?? resource?.Content ?? throw new InvalidDataException("Unknown audit read.");
            ResourceBinding binding = CorrectnessOracle.ValidateResponse(read.Response, 200, content, baseUrl,
                resource?.Binding, receipt);
            validation.Resources[read.Identity] = new(content, binding);
        }
    }

    private static void ValidateIdentityChunks(
        IdentityInspection[] chunks, Dictionary<string, bool> expected, TenantSnapshot inventory, int processId)
    {
        Dictionary<string, ResourceIdentity?> actual = new(StringComparer.Ordinal);
        foreach (IdentityInspection chunk in chunks)
        {
            Require(chunk.ProcessId == processId && chunk.Resources.Count is > 0 and <= 32 &&
                chunk.Counts.SequenceEqual(inventory.ResourceCounts) && chunk.ReceiptCount == inventory.ReceiptCount &&
                chunk.EventCount == inventory.EventCount, "Invalid bounded stopped identity chunk.");
            foreach (KeyValuePair<string, ResourceIdentity?> pair in chunk.Resources)
            {
                Require(actual.TryAdd(pair.Key, pair.Value), "Duplicate identity audit.");
            }
        }

        Require(actual.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(expected.Keys), "A 32-ID sample cannot replace all required identity audits.");
        foreach (KeyValuePair<string, bool> pair in expected)
        {
            ResourceIdentity? value = actual[pair.Key];
            Require(pair.Value ? value is not null && value.ResourceType + "/" + value.Id == pair.Key : value is null,
                "Missing retained identity or unexpectedly persisted/deleted clinical resource.");
        }
    }

    private static Dictionary<string, ExpectedResource> ReadSeedResources(
        JsonElement seed, WorkloadCorpus corpus, string variant, ClockObservation started, ClockObservation finished)
    {
        int count = corpus.Resources.Length;
        ValidateSeedTotals(seed, count);
        Dictionary<string, ExpectedResource> resources = new(StringComparer.Ordinal);
        CorpusResource[] order = Workloads.SeedResources(corpus).ToArray();
        JsonElement values = seed.GetProperty("ValidatedResources");
        Require(values.GetArrayLength() == count, "Missing per-resource full seed validation receipts.");
        int index = 0;
        foreach (JsonElement value in values.EnumerateArray())
        {
            CorpusResource expected = order[index++];
            ResourceBinding binding = value.GetProperty("Binding").Deserialize<ResourceBinding>(_json)
                ?? throw new InvalidDataException("Missing original seed metadata.");
            Require(value.GetProperty("Number").GetInt32() == index && value.GetProperty("Identity").GetString() == expected.Identity &&
                value.GetProperty("InputBytes").GetInt32() == expected.Body.Length &&
                value.GetProperty("InputSha256").GetString() == Convert.ToHexString(SHA256.HashData(expected.Body)) &&
                binding.ResourceType + "/" + binding.Id == expected.Identity && binding.VersionId == "1",
                "Seed provenance/order/metadata differs.");
            foreach (string phase in new[] { "Put", "Readback" })
            {
                JsonElement response = value.GetProperty(phase);
                Require(response.GetProperty("StatusCode").GetInt32() == (phase == "Put"
                    ? CorrectnessOracle.StatusProfile(variant).PutAsCreate : 200) &&
                    response.GetProperty("BodyBytes").GetInt32() > 0 &&
                    response.GetProperty("BodySha256").GetString()!.Length == 64 &&
                    response.GetProperty("CompleteEvidenceSha256").GetString()!.Length == 64, "Incomplete validated seed digest.");
                ClockObservation responseStart = ReadClock(response, "Started");
                ClockObservation responseEnd = ReadClock(response, "Completed");
                ClockAssessment interval = ClockObservation.Assess(responseStart, responseEnd);
                Require(ClockObservation.IsOrdered(started, responseStart) && ClockObservation.IsOrdered(responseEnd, finished) &&
                    (phase != "Put" || interval.Contains(binding.LastUpdated)), "Unbound seed mutation/response clock evidence.");
            }

            resources.Add(expected.Identity, new(expected.Body, binding));
        }

        return resources;
    }

    private static void ValidateSeedTotals(JsonElement seed, int count)
    {
        Require(seed.GetProperty("Passed").GetBoolean() && seed.GetProperty("RequiredResources").GetInt32() == count,
            "Full seed content validation failed.");
        foreach (string field in new[] { "AttemptedSeedWrites", "CompletedSeedWrites", "ValidatedSeedWrites",
            "AttemptedSeedReads", "CompletedSeedReads", "ValidatedSeedReads" })
        {
            Require(seed.GetProperty(field).GetInt32() == count, "Missing full seed phase: " + field);
        }
    }

    private static async Task<(ClockObservation Launched, ClockObservation Exited)> ValidateExitAsync(
        string root, string directory, int pid, long startTicks, ParentIdentity parent,
        ClockObservation started, ClockObservation finished, CancellationToken token)
    {
        using JsonDocument exit = await ReadDocumentAsync(root, Path.Combine(directory, "exit.json"), token);
        JsonElement value = exit.RootElement;
        Require(value.GetProperty("ProcessId").GetInt32() == pid &&
            value.GetProperty("StartedUtc").GetDateTimeOffset().UtcTicks == startTicks &&
            value.GetProperty("Parent").Deserialize<ParentIdentity>(_json) == parent && parent.IsValid() &&
            value.GetProperty("ExitCode").GetInt32() == 0 && !value.GetProperty("ForcedTermination").GetBoolean() &&
            value.GetProperty("GracefulFailure").ValueKind == JsonValueKind.Null &&
            !value.GetProperty("StandardOutputOverflow").GetBoolean() && !value.GetProperty("StandardErrorOverflow").GetBoolean(),
            "Missing clean exact-child exit.");
        ClockObservation exited = ReadClock(value, "Observed");
        using JsonDocument process = await ReadDocumentAsync(root, Path.Combine(directory, "process.json"), token);
        Require(process.RootElement.GetProperty("ProcessId").GetInt32() == pid &&
            process.RootElement.GetProperty("StartedUtc").GetDateTimeOffset().UtcTicks == startTicks &&
            process.RootElement.GetProperty("Parent").Deserialize<ParentIdentity>(_json) == parent,
            "Process launch and exit identities differ.");
        ClockObservation launched = ReadClock(process.RootElement, "Observed");
        using JsonDocument launch = await ReadDocumentAsync(root, Path.Combine(directory, "launch.json"), token);
        ClockObservation requested = ReadClock(launch.RootElement, "Requested");
        JsonElement configuration = launch.RootElement.GetProperty("ProbeConfiguration");
        ClockObservation issued = ReadClock(configuration, "Issued");
        long deadline = configuration.GetProperty("DeadlineTimestamp").GetInt64();
        Require(configuration.GetProperty("ParentProcessId").GetInt32() == parent.ProcessId &&
            configuration.GetProperty("ParentStartTimeUtcTicks").GetInt64() == parent.ProcessStartUtcTicks &&
            configuration.GetProperty("RequestTimeoutSeconds").GetInt32() == ProbeProtocol.RequestSeconds &&
            issued.Before <= long.MaxValue - (issued.Frequency * ProbeProtocol.ChildLifetimeSeconds) &&
            deadline == issued.Before + (issued.Frequency * ProbeProtocol.ChildLifetimeSeconds) && exited.After < deadline &&
            ClockObservation.IsOrdered(started, issued) && ClockObservation.IsOrdered(issued, requested) &&
            ClockObservation.IsOrdered(requested, launched) && ClockObservation.IsOrdered(launched, exited) &&
            ClockObservation.IsOrdered(exited, finished), "Missing or reordered parent-observed child lifecycle.");
        return (launched, exited);
    }

    private static ClockObservation ReadClock(JsonElement owner, string name)
    {
        ClockObservation observation = owner.GetProperty(name).Deserialize<ClockObservation>(_json);
        Require(observation.IsValid(), "Missing/default monotonic observation: " + name);
        return observation;
    }

    private static async Task AddSavedResponsesAsync(
        string root, string directory, string prefix, List<ResponseClock> responses, CancellationToken token)
    {
        int before = responses.Count;
        foreach (string path in Directory.EnumerateFiles(EvidencePath(root, directory), prefix + "*.json").Order(StringComparer.Ordinal))
        {
            string name = Path.GetFileName(path);
            if (name.EndsWith("-request.json", StringComparison.Ordinal) || name.EndsWith("-failed.json", StringComparison.Ordinal))
            {
                continue;
            }

            using JsonDocument document = await ReadDocumentAsync(root, Path.Combine(directory, name), token);
            if (document.RootElement.TryGetProperty("Response", out JsonElement value))
            {
                ResponseEvidence response = value.Deserialize<ResponseEvidence>(_json)
                    ?? throw new InvalidDataException("Missing complete saved response.");
                responses.Add(new(name, response.Started, response.Completed));
            }
        }

        Require(prefix != "readiness-" || responses.Count > before, "Missing readiness response clock evidence.");
    }

    private static void AddSeedClocks(JsonElement seed, List<ResponseClock> responses)
    {
        // All PUTs precede all read-backs, even though their compact receipts are stored per resource.
        foreach (string phase in new[] { "Put", "Readback" })
        {
            foreach (JsonElement resource in seed.GetProperty("ValidatedResources").EnumerateArray())
            {
                JsonElement response = resource.GetProperty(phase);
                responses.Add(new("seed:" + phase + ":" + resource.GetProperty("Identity").GetString(),
                    ReadClock(response, "Started"), ReadClock(response, "Completed")));
            }
        }
    }

    private static void AddOperationClocks(string phase, OperationResult[] results, List<ResponseClock> responses)
    {
        foreach (OperationResult result in results)
        {
            for (int page = 0; page < result.Exchanges.Length; page++)
            {
                ResponseEvidence response = result.Exchanges[page].Response;
                responses.Add(new(phase + ":" + result.Timing.Number.ToString(CultureInfo.InvariantCulture) +
                    ":page-" + (page + 1).ToString(CultureInfo.InvariantCulture), response.Started, response.Completed));
            }
        }
    }

    private static void AddAuditClocks(IEnumerable<AuditInteraction> interactions, List<ResponseClock> responses)
    {
        foreach (AuditInteraction interaction in interactions)
        {
            responses.Add(new("audit:" + interaction.Method + ":" + interaction.Identity,
                interaction.Response.Started, interaction.Response.Completed));
        }
    }

    private static ClockSummary ValidateResponseClocks(
        IReadOnlyList<ResponseClock> responses, ClockObservation launched, ClockObservation exited)
    {
        Require(responses.Count > 0, "No response clocks to audit.");
        ClockObservation previous = launched;
        int during = 0;
        int between = 0;
        long uncertainty = 0;
        List<string> examples = [];
        void Observe(string label, ClockObservation first, ClockObservation second, bool gap)
        {
            ClockAssessment assessment = ClockObservation.Assess(first, second);
            uncertainty = Math.Max(uncertainty, assessment.MaximumElapsedTicks - assessment.MinimumElapsedTicks);
            if (assessment.AdjustmentDetected)
            {
                if (gap)
                {
                    between++;
                }
                else
                {
                    during++;
                }

                if (examples.Count < 8)
                {
                    examples.Add(label + ": " + DescribeClock(first, second, assessment));
                }
            }
        }

        foreach (ResponseClock response in responses)
        {
            Observe("before " + response.Label, previous, response.Started, gap: true);
            Observe(response.Label, response.Started, response.Completed, gap: false);
            previous = response.Completed;
        }

        Observe("after final response / child exit", previous, exited, gap: true);
        return new(responses.Count, during, between, uncertainty, examples.ToArray());
    }

    private static string DescribeClock(ClockObservation first, ClockObservation second, ClockAssessment assessment) =>
        first.Utc.ToString("O", CultureInfo.InvariantCulture) + " → " + second.Utc.ToString("O", CultureInfo.InvariantCulture) +
        "; monotonic elapsed [" + assessment.MinimumElapsedTicks.ToString(CultureInfo.InvariantCulture) + ", " +
        assessment.MaximumElapsedTicks.ToString(CultureInfo.InvariantCulture) + "] ticks; net UTC adjustment [" +
        assessment.MinimumAdjustmentTicks.ToString(CultureInfo.InvariantCulture) + ", " +
        assessment.MaximumAdjustmentTicks.ToString(CultureInfo.InvariantCulture) + "] ticks (100 ns/tick).";

    private static async Task<ProbeSnapshot> ReadSnapshotAsync(
        string root, string directory, string label, TrialRecord trial, ExperimentManifest manifest,
        PreparedExperiment prepared, WorkloadCase workload, CancellationToken token)
    {
        ProbeReply reply = await ReadAsync<ProbeReply>(root, Path.Combine(directory, label + "-snapshot.json"), token);
        ProbeSnapshot snapshot = reply.Snapshot ?? throw new InvalidDataException("Missing stopped inventory.");
        Require(!reply.Shutdown && snapshot.ProcessId == trial.ProcessId && snapshot.RuntimeVersion == "8.0.30" &&
            snapshot.FrameworkDescription == ".NET 8.0.30" && snapshot.Architecture == "X64" &&
            snapshot.ServerGc == manifest.Runtime.ServerGc && snapshot.Tenant.Configuration["FhirVersion"] == workload.FhirVersion &&
            snapshot.Tenant.Configuration["MaxResourceCount"] == "0" &&
            snapshot.Tenant.Configuration["AllowExistingId"] == "True" && snapshot.Tenant.Configuration["AllowCreateAsUpdate"] == "True" &&
            snapshot.Host.Configuration["UiDisabled"] == "False" && snapshot.Host.CachedPackageCount == 0,
            "Loaded runtime/effective host/tenant configuration differs.");
        foreach ((string key, string expected) in new[]
        {
            ("DOTNET_gcServer", manifest.Runtime.ServerGc ? "1" : "0"),
            ("DOTNET_gcConcurrent", manifest.Runtime.ConcurrentGc ? "1" : "0"),
            ("DOTNET_TieredCompilation", manifest.Runtime.TieredCompilation ? "1" : "0"),
            ("DOTNET_TieredPGO", manifest.Runtime.TieredPgo ? "1" : "0"),
            ("DOTNET_ReadyToRun", manifest.Runtime.ReadyToRun ? "1" : "0"),
            ("DOTNET_ROLL_FORWARD", "Disable"), ("DOTNET_MULTILEVEL_LOOKUP", "0"),
        })
        {
            Require(snapshot.RuntimeEnvironment.TryGetValue(key, out string? actual) && actual == expected, "Saved runtime setting differs: " + key);
        }

        ArtifactIdentity artifact = trial.Plan.Variant == "baseline" ? prepared.Baseline : prepared.Candidate;
        AssemblyCorrelation[] correlations = await ReadAsync<AssemblyCorrelation[]>(root,
            Path.Combine(directory, label + "-assembly-correlation.json"), token);
        Require(JsonSerializer.Serialize(correlations.Select(item => item.Loaded).ToArray(), _json) ==
            JsonSerializer.Serialize(snapshot.Assemblies, _json), "Loaded assembly correlation set changed.");
        ExperimentRunner.ValidateRequiredLoadedAssemblies(artifact, workload.FhirVersion, correlations);
        foreach (AssemblyCorrelation correlation in correlations)
        {
            if (correlation.Loaded.Name == "FhirCandle.Performance.Probe")
            {
                Require(correlation.Loaded.Sha256 == prepared.ProbeSha256, "Mixed loaded probe bytes.");
                continue;
            }

            string inventoryRoot;
            FileSeal[] files;
            AssemblyFile[] assemblies;
            DependencyIdentity[] dependencies;
            if (correlation.Origin == artifact.Variant)
            {
                inventoryRoot = artifact.RootDirectory;
                files = artifact.Files;
                assemblies = artifact.Assemblies;
                dependencies = artifact.Dependencies;
            }
            else
            {
                FrameworkIdentity framework = prepared.Frameworks.Single(item => item.Name + "/" + item.Version == correlation.Origin);
                inventoryRoot = framework.RootDirectory;
                files = framework.Files;
                assemblies = framework.Assemblies;
                dependencies = framework.Dependencies;
            }

            string relative = Path.GetRelativePath(inventoryRoot, correlation.Loaded.Path);
            FileSeal file = files.Single(item => item.RelativePath.Equals(relative, StringComparison.OrdinalIgnoreCase));
            AssemblyFile assembly = assemblies.Single(item => item.RelativePath.Equals(relative, StringComparison.OrdinalIgnoreCase));
            Require(file.Sha256 == correlation.Loaded.Sha256 && assembly.Mvid == correlation.Loaded.Mvid &&
                assembly.AssemblyVersion == correlation.Loaded.AssemblyVersion &&
                assembly.FileVersion == correlation.Loaded.FileVersion && assembly.InformationalVersion == correlation.Loaded.InformationalVersion &&
                dependencies.Any(item => item.Name == correlation.Dependency && item.Version == correlation.DependencyVersion &&
                    item.Sha512 == correlation.DependencySha512), "Loaded assembly bytes/metadata/package seal differs.");
        }

        return snapshot;
    }

    private static async Task<T> ReadAsync<T>(string root, string relative, CancellationToken token)
    {
        int maximum = typeof(T) == typeof(ExperimentManifest) ? 1024 * 1024
            : typeof(T) == typeof(PreparedExperiment) || typeof(T) == typeof(WorkloadCorpus) ? 32 * 1024 * 1024
            : MaximumEvidenceBytes;
        byte[] bytes = await ReadBytesAsync(root, relative, token, maximum);
        return JsonSerializer.Deserialize<T>(bytes, _json) ?? throw new InvalidDataException("Missing saved " + typeof(T).Name + ".");
    }

    private static async Task<JsonDocument> ReadDocumentAsync(string root, string relative, CancellationToken token) =>
        JsonDocument.Parse(await ReadBytesAsync(root, relative, token), new JsonDocumentOptions { MaxDepth = 64 });

    private static async Task<byte[]> ReadBytesAsync(
        string root, string relative, CancellationToken token, int maximumBytes = MaximumEvidenceBytes)
    {
        string path = EvidencePath(root, relative);
        await using FileStream input = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return await ReadBoundedBytesAsync(input, maximumBytes, token);
    }

    private static string EvidencePath(string root, string relative)
    {
        Require(!string.IsNullOrEmpty(relative) && !Path.IsPathRooted(relative) && !relative.Contains(':') &&
            relative.Replace('\\', '/').Split('/').All(segment => segment.Length > 0 && segment is not "." and not ".."),
            "Unsafe saved evidence reference.");
        string normalized = relative.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
        string path = Path.GetFullPath(Path.Combine(root, normalized));
        string prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        Require(path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase), "Saved reference escaped its run.");
        string? current = path;
        while (current is not null)
        {
            if (File.Exists(current) || Directory.Exists(current))
            {
                Require((File.GetAttributes(current) & FileAttributes.ReparsePoint) == 0, "Reparse points cannot supply evidence.");
            }

            current = Path.GetDirectoryName(current);
        }

        return path;
    }

    private static string HashFile(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static void Require([DoesNotReturnIf(false)] bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidDataException(message);
        }
    }

    private sealed class ReportEvidence
    {
        internal ExperimentManifest? Manifest;
        internal PreparedExperiment? Prepared;
        internal FrozenMeasurement? Frozen;
        internal MeasurementLedger? Ledger;
        internal string? EnvironmentJson;
        internal readonly List<TrialRecord> Trials = [];
        internal readonly List<TrialRecord> Calibration = [];
        internal readonly List<string> Errors = [];
        internal readonly Dictionary<string, PopulationSummary> Populations = new(StringComparer.Ordinal);
        internal readonly Dictionary<string, ClockSummary> Clocks = new(StringComparer.Ordinal);
    }

    private sealed record ResponseClock(string Label, ClockObservation Started, ClockObservation Completed);

    private sealed record ClockSummary(int Responses, int AdjustedResponses, int AdjustedGaps, long MaximumUncertaintyTicks, string[] Examples);

    private sealed record PopulationSummary(
        SortedDictionary<string, int> Initial, SortedDictionary<string, int> Loaded,
        SortedDictionary<string, int> Post, int LoadedReceipts, int PostReceipts, ClockSummary Clocks);

    private static void AppendClockEvidence(StringBuilder text, ReportEvidence evidence)
    {
        text.AppendLine();
        text.AppendLine("## Clock observations and interpretation");
        text.AppendLine();
        text.AppendLine("Raw UTC observations are unchanged wall-clock evidence, not elapsed/order clocks. " +
            "Positive, ordered monotonic brackets establish response and parent-owned lifecycle chronology; OS PID/start-time equality still authenticates identity. " +
            "Separate prepare/preflight/run invocations are linked by immutable handoff hashes, not UTC ordering.");
        text.AppendLine("Clock adjustment ranges below include the measured UTC-read uncertainty (100 ns/tick), with no arbitrary tolerance. " +
            "A detectable net UTC discrepancy may be a step, slew or clock-rate difference; endpoints cannot locate its instant or detect cancelling intermediate changes. " +
            "Mutation metadata and hook receipts must remain in their own endpoint-derived request UTC envelope; immutable reads retain exact bound metadata even if it is now in the wall-clock future. " +
            "The envelope is not proof of an exact wall-time event or absence of intermediate adjustments. Clinical fixture timestamps remain strict.");
        if (evidence.Clocks.Count == 0)
        {
            text.AppendLine("- No fully audited raw response clock streams are available; this is not evidence that the clock was unchanged.");
        }
        else
        {
            text.AppendLine("- Audited response intervals: " + evidence.Clocks.Values.Sum(clock => (long)clock.Responses).ToString(CultureInfo.InvariantCulture) +
                "; detected within-response discrepancies: " + evidence.Clocks.Values.Sum(clock => (long)clock.AdjustedResponses).ToString(CultureInfo.InvariantCulture) +
                "; detected between-response/lifecycle-boundary discrepancies: " + evidence.Clocks.Values.Sum(clock => (long)clock.AdjustedGaps).ToString(CultureInfo.InvariantCulture) +
                "; maximum derived elapsed uncertainty: " + evidence.Clocks.Values.Max(clock => clock.MaximumUncertaintyTicks).ToString(CultureInfo.InvariantCulture) + " ticks.");
            text.AppendLine("- Bounded examples follow (at most eight per child, sixteen displayed); every endpoint remains in its sealed raw response/seed evidence.");
            foreach (string example in evidence.Clocks.SelectMany(pair => pair.Value.Examples.Select(value => pair.Key + " / " + value)).Take(16))
            {
                text.AppendLine("  - " + Escape(example));
            }
        }

        List<string> lifecycle = [];
        TrialRecord? previous = null;
        int discrepancies = 0;
        foreach (TrialRecord trial in evidence.Calibration.Concat(evidence.Trials))
        {
            foreach ((string label, ClockObservation first, ClockObservation second) in new[]
            {
                (trial.Plan.AttemptId + " lifetime", trial.Started, trial.Finished),
                (trial.Plan.AttemptId + " preceding-parent-gap", previous?.Finished ?? default, trial.Started),
            })
            {
                if (!ClockObservation.IsOrdered(first, second) ||
                    (label.EndsWith("preceding-parent-gap", StringComparison.Ordinal) && previous?.Parent != trial.Parent))
                {
                    continue;
                }

                ClockAssessment assessment = ClockObservation.Assess(first, second);
                if (assessment.AdjustmentDetected)
                {
                    discrepancies++;
                    if (lifecycle.Count < 16)
                    {
                        lifecycle.Add(label + ": " + DescribeClock(first, second, assessment));
                    }
                }
            }

            previous = trial;
        }

        text.AppendLine("- Parsed trial lifetime/parent-gap net clock discrepancies: " + discrepancies.ToString(CultureInfo.InvariantCulture) +
            " (documentary, including incomplete attempts; not accepted performance results).");
        foreach (string example in lifecycle)
        {
            text.AppendLine("  - " + Escape(example));
        }
    }

    private static string Render(ReportEvidence evidence, string root)
    {
        bool complete = evidence.Errors.Count == 0;
        StringBuilder text = new();
        text.AppendLine("# Sealed release versus pinned candidate — observed performance");
        text.AppendLine();
        text.AppendLine(complete
            ? "**COMPLETE saved-evidence matrix: 72 paired rows, all mandatory metrics and independent trials validated.**"
            : "**INCOMPLETE — not a performance comparison result. Missing/invalid evidence blocks completion.**");
        text.AppendLine();
        text.AppendLine("- Raw run: `" + Escape(root) + "`.");
        text.AppendLine("- Baseline: released NuGet **fhir-candle 2025.611.1422**, source `" + ExperimentRunner.BaselineRevision + "`, Firely **5.11.4** cohort.");
        text.AppendLine("- Candidate: clean request-time application `" + ExperimentRunner.CandidateRevision + "`, Firely **6.2.1** cohort.");
        text.AppendLine("- These are historical **application-versus-application** observations, not an isolated SDK swap.");
        if (evidence.Frozen is FrozenMeasurement frozen)
        {
            text.AppendLine("- Frozen protocol: `" + frozen.ContractVersion + "` / `" + ProbeProtocol.Version + "`; frozen UTC: " + frozen.FrozenUtc.ToString("O", CultureInfo.InvariantCulture) + ".");
            text.AppendLine("- Prepared identity: `" + frozen.PreparedSha256 + "`; identical injected probe: `" + frozen.ProbeSha256 + "`.");
            text.AppendLine("- Scheduled trials: " + frozen.Schedule.Length.ToString(CultureInfo.InvariantCulture) +
                "; parsed attempts: " + evidence.Trials.Count.ToString(CultureInfo.InvariantCulture) +
                "; calibration is separate and never counted as accepted trials.");
        }

        if (evidence.Ledger is MeasurementLedger ledger)
        {
            text.AppendLine("- Measurement UTC: " + ledger.StartedUtc.ToString("O", CultureInfo.InvariantCulture) +
                " to " + ledger.FinishedUtc.ToString("O", CultureInfo.InvariantCulture) + ".");
            text.AppendLine("- Not attempted: " + ledger.NotAttempted.Length.ToString(CultureInfo.InvariantCulture) +
                "; ledger failure: " + Escape(ledger.Failure ?? "none") + ".");
        }

        AppendClockEvidence(text, evidence);
        text.AppendLine();
        text.AppendLine("## Executive findings");
        text.AppendLine();
        if (!complete)
        {
            text.AppendLine("**Inconclusive/incomplete.** No improved/regressed claim is supported by this incomplete matrix. " +
                "Unit fixtures, successful exits, earlier semantics preflights and existing markdown are not measurement evidence.");
            text.AppendLine();
            foreach (string error in evidence.Errors.Distinct(StringComparer.Ordinal))
            {
                text.AppendLine("- " + Escape(error));
            }
        }
        else
        {
            Dictionary<string, Dictionary<string, double>> values = evidence.Trials
                .ToDictionary(trial => trial.Plan.AttemptId, TrialMetrics, StringComparer.Ordinal);
            foreach (string metric in new[] { "time.ms/op", "allocation.B/op", "process.peak-working.B", "managed.post-forced-occupied.B" })
            {
                string[] findings = Workloads.CreateCases().Select(workload =>
                {
                    (MetricSummary baseline, MetricSummary candidate) = Summaries(evidence.Trials, values, workload.RowKey, metric);
                    return Interpretation(metric, baseline, candidate, CalibrationSpan(evidence, workload.RowKey, metric));
                }).ToArray();
                text.AppendLine("- **" + metric + "**: " + findings.Count(value => value == "improved").ToString(CultureInfo.InvariantCulture) +
                    " observed improved, " + findings.Count(value => value == "regressed").ToString(CultureInfo.InvariantCulture) +
                    " observed regressed, " + findings.Count(value => value == "inconclusive").ToString(CultureInfo.InvariantCulture) +
                    " inconclusive rows.");
            }

            text.AppendLine();
            text.AppendLine("Directional labels require disjoint observed trial min–max ranges and a positive baseline. " +
                "Overlapping ranges, zero/negative incremental denominators, equal values or a difference no larger than the " +
                "observed paired normal/off perturbation for that metric are inconclusive. " +
                "This conservative descriptive rule is not a significance test or regression budget. " +
                "Background/control cost comparable to a difference weakens interpretation; no overhead constant is subtracted.");
            AppendMetricTables(text, evidence, values);
            AppendTrialTable(text, evidence);
            AppendCalibration(text, evidence);
            AppendPopulations(text, evidence);
        }

        if (!complete)
        {
            text.AppendLine();
            text.AppendLine("## Required fixed matrix (status only; no missing values imputed)");
            text.AppendLine();
            text.AppendLine("| Row key | Baseline recorded passed / attempted | Candidate recorded passed / attempted |");
            text.AppendLine("|-|-:|-:|");
            foreach (WorkloadCase workload in Workloads.CreateCases())
            {
                string[] cells = new[] { "baseline", "candidate" }.Select(variant =>
                {
                    TrialRecord[] attempts = evidence.Trials.Where(trial => trial.Plan.RowKey == workload.RowKey && trial.Plan.Variant == variant).ToArray();
                    return attempts.Count(trial => trial.Status == "passed").ToString(CultureInfo.InvariantCulture) + " / " +
                        attempts.Length.ToString(CultureInfo.InvariantCulture);
                }).ToArray();
                text.AppendLine("| " + workload.RowKey + " | " + cells[0] + " | " + cells[1] + " |");
            }
        }

        text.AppendLine();
        text.AppendLine("## Provenance, machine and execution conditions");
        text.AppendLine();
        if (evidence.Prepared is PreparedExperiment prepared)
        {
            foreach (ArtifactIdentity artifact in new[] { prepared.Baseline, prepared.Candidate })
            {
                AssemblyFile? app = artifact.Assemblies.SingleOrDefault(assembly =>
                    assembly.RelativePath.Equals(artifact.EntryPointRelativePath, StringComparison.OrdinalIgnoreCase));
                text.AppendLine("- " + artifact.Variant + ": `" + Escape(artifact.RootDirectory) + "\\" +
                    Escape(artifact.EntryPointRelativePath) + "`; " + artifact.Files.Length.ToString(CultureInfo.InvariantCulture) +
                    " sealed files; app assembly/file/informational versions: `" + Escape(app?.AssemblyVersion ?? "missing") + "` / `" +
                    Escape(app?.FileVersion ?? "missing") + "` / `" + Escape(app?.InformationalVersion ?? "missing") + "`.");
            }

            text.AppendLine("- Actual common runtime: .NET and ASP.NET **8.0.30**, Windows x64. Build SDK and timestamp-derived app versions are separate evidence.");
            text.AppendLine("- Exact tool files/source, published archive provenance, dependency packages, PE identities and installed runtime inventories: `prepared.json`, `candidate-build.json`, `manifest.json`.");
        }

        if (evidence.EnvironmentJson is not null)
        {
            // This is the allowlisted saved observation, not current-renderer host memory or inherited environment.
            text.AppendLine();
            text.AppendLine("Saved preparation observation (the caller's conditions are assertions with their stated observation source):");
            text.AppendLine("```json");
            text.AppendLine(evidence.EnvironmentJson.TrimEnd());
            text.AppendLine("```");
        }

        text.AppendLine();
        text.AppendLine("## Boundaries, workload, state and memory methods");
        text.AppendLine();
        text.AppendLine("- Twelve cases × R4/R4B/R5 × 100P+1,000O / 1,000P+10,000O. Algorithm `candle-perf-v1`, seed `100101`; exact input bytes/order and payload sizes are sealed.");
        text.AppendLine("- Fresh child per case/trial; never simultaneous variants or store reset. One HTTP worker; alternating pair order by repetition and case block. At least three independent trials/variant/row.");
        text.AppendLine("- Full corpus PUTs and full read-backs, probe/reflection/control warm-up and input/message/result-buffer preparation are outside timing. Exactly 128 actual-case warm-up operations; creates deleted and absence audited; updates clinically restored by ordinary requests without erasing versions/caches. Patient's even toggle already restores true and records no extra PUT.");
        text.AppendLine("- Hook retains all 128 full-content warm Bundles/receipts. Default N=256 adds 256: loaded/post Bundles and receipts 128/384, with no separately stored embedded clinical resources. Increased pilot N uses the same +N rule and measured event/create numbers start at 129.");
        text.AppendLine("- Client batch T includes loopback HTTP, controller/parser/store/serialization, full body consumption, client response retention/copy/header costs and minimal paging discovery. Logical operation times include every page; independent content oracles run afterward. T/N is ms/op; N/T is op/s. Request observations are not independent trials.");
        text.AppendLine("- Process-wide allocation is GC.GetTotalAllocatedBytes(true) end−begin / full N. Individual gen0/1/2 collection and total-pause deltas use the same server window; checkpoint-induced collections are excluded. Generation counts are not summed into a fictitious exclusive total.");
        text.AppendLine("- Server monotonic counter boundaries precede binary acknowledgements. Begin/end gaps relative to client T are retained. Probe/control/pipe/host/background allocation and CPU are included, never guessed away. Export, reflection, correctness parsing, evidence I/O and forced GC occur only after EndWindow.");
        text.AppendLine("- Both samplers use preallocated " + ProbeProtocol.SampleCapacity.ToString(CultureInfo.InvariantCulture) +
            "-slot buffers, 10 ms nominal cadence and actual timestamps. At least ten actual samples from each must lie inside the client workload interval. Process samples include only the authenticated owned server PID/start time, never the runner/testhost. Maxima are sampled lower bounds, not continuous peaks; no lifetime high-water is substituted.");
        text.AppendLine("- Managed occupied bytes are GC.GetTotalMemory(false) estimates. Heap/fragmentation/committed values describe the last GC, not continuous live retention. Last-GC indices and raw timestamps are retained.");
        text.AppendLine("- Seven mandatory footprints: idle natural/forced, loaded natural/forced, immediate post-work, fixed-one-second settled post-work, forced post-work. Capture precedes diagnostic DTO/reflection/audit reads. Forced protocol: blocking full GC, finalizer drain, second blocking full GC, same one-second settle. Natural and forced deltas stay separate.");
        try
        {
            ExperimentBounds? bounds = evidence.Frozen?.Bounds ?? evidence.Manifest?.Bounds;
            Require(bounds is not null, "No readable manifest/frozen bounds.");
            Require(evidence.Manifest?.ProtocolVersion == ProbeProtocol.Version &&
                (evidence.Frozen is null || evidence.Frozen.ContractVersion == ContractVersion),
                "Saved timing protocol is missing or incompatible.");
            bounds.Validate();
            text.AppendLine("- Saved startup/setup/measured/request/settle/shutdown bounds: " +
                bounds.StartupSeconds.ToString(CultureInfo.InvariantCulture) + "s/" +
                bounds.SetupSeconds.ToString(CultureInfo.InvariantCulture) + "s/" +
                bounds.BatchSeconds.ToString(CultureInfo.InvariantCulture) + "s/" +
                bounds.RequestSeconds.ToString(CultureInfo.InvariantCulture) + "s/" +
                (bounds.SettlingMilliseconds / 1000.0).ToString(CultureInfo.InvariantCulture) + "s/" +
                bounds.ShutdownSeconds.ToString(CultureInfo.InvariantCulture) + "s. Setup+audit share the " +
                bounds.SetupSeconds.ToString(CultureInfo.InvariantCulture) + "s budget; the measured window has its separate " +
                bounds.BatchSeconds.ToString(CultureInfo.InvariantCulture) + "s limit.");
            text.AppendLine("- Saved `" + ProbeProtocol.Version + "` protocol: stopped export has its own " +
                ProbeProtocol.StoppedExportSeconds.ToString(CultureInfo.InvariantCulture) +
                "s budget after EndWindow, separate from setup/audit, measured work and the " +
                bounds.RequestSeconds.ToString(CultureInfo.InvariantCulture) +
                "s end acknowledgement/ordinary HTTP/pipe exchange limit. The finite child lifetime is " +
                ProbeProtocol.ChildLifetimeSeconds.ToString(CultureInfo.InvariantCulture) +
                "s, including startup, setup/audit, measured work, end acknowledgement, stopped export, " +
                ProbeProtocol.BoundaryAllowanceSeconds.ToString(CultureInfo.InvariantCulture) +
                "s boundary allowance and two " + bounds.ShutdownSeconds.ToString(CultureInfo.InvariantCulture) +
                "s shutdown intervals. These are configured limits, not observed durations.");
        }
        catch (InvalidDataException exception)
        {
            text.AppendLine("- Saved timing bounds, stopped-export budget and child lifetime are unavailable: " + Escape(exception.Message));
        }
        text.AppendLine("- Overrun/missing streams/wrong PID/invalid counters/partial work block completion.");
        text.AppendLine();
        text.AppendLine("## Correctness, failures and raw audit");
        text.AppendLine();
        text.AppendLine("- `preflight\\matrix.json` / `completed.json` retain all 72 pairs and 144 semantic lifetimes. `calibration\\completed.json` plus `frozen-measurement.json` retain every pilot attempt, chosen counts and matched sampler-off/control evidence.");
        text.AppendLine("- `measurement\\ledger.json` lists all measured attempts and not-attempted dispatches. No retries, dropped operations, concatenated partial batches, best-of selection or pooling across changed counts/artifacts is permitted.");
        text.AppendLine("- Each child retains `launch.json` (actual arguments/allowlist), `handshake.json`, before/after seals, ready/warm/post inventories and assembly correlations, all seed hashes after full validation, complete warm/measured response bodies/headers/times, ordinary audit responses, every identity chunk and clean/forced exit details.");
        text.AppendLine("- Saved response bytes are unmodified base64 JSON fields. Renderer rechecks raw hashes, seed order/digests, every measured and warm response, all persisted read-backs, update versions, exact populations, complete ordered receipt contents and missing/duplicate identities. Synthetic origin is always rejected.");
        text.AppendLine();
        text.AppendLine("## Confounders and limitations");
        text.AppendLine();
        text.AppendLine("These sealed releases also differ in historical application, dependencies, compiler/build versions, built-in conformance inventory and specifically proven successful status profiles (seed PUT 200 versus 201). Ordinary host/UI, logging, timers, data-protection location, JIT/tiering/PGO, HTTP and identical probe costs remain. " +
            "The test does not establish SDK-only causality, prove a leak from a peak, turn working-set growth into allocation, or extrapolate one Windows machine to production/concurrent workloads. Calibration and outliers remain visible; there is no fabricated win, performance budget, quiet-host assertion or fixed-cost subtraction.");
        text.AppendLine();
        text.AppendLine("Reproduction invocations are saved in preparation, preflight/calibration, measurement and every child launch. " +
            "Use the documented `prepare` → `run --preflight-only` → `run` → `render` sequence with one matching run ID; after any tool/probe/protocol change use a fresh ID and preserve the old evidence. " +
            "This report is a deterministic projection of those files; re-rendering does not launch a server, use the network, load product assemblies or borrow current machine measurements.");
        return text.ToString();
    }

    private static void AppendMetricTables(
        StringBuilder text, ReportEvidence evidence, Dictionary<string, Dictionary<string, double>> values)
    {
        text.AppendLine();
        text.AppendLine("## Paired speed, allocation, GC, process, managed-memory and retention tables");
        text.AppendLine();
        text.AppendLine("Every metric below has exactly 72 paired rows. Cells are **median [min, max] across independent trials**. " +
            "N is the fixed successful logical operations per trial; B/C trial counts and all N values are explicit. " +
            "Ratio is candidate/baseline; % is 100×(candidate−baseline)/baseline; Δ is candidate−baseline in the named unit. " +
            "`n/a` retains its denominator reason, not a fabricated ratio. GC counts are descriptive, not automatically good/bad.");
        string[] metrics = values.First().Value.Keys.ToArray();
        foreach (string metric in metrics)
        {
            text.AppendLine();
            text.AppendLine("### " + metric + (metric.StartsWith("throughput.", StringComparison.Ordinal) ? " (higher is better)" :
                metric.StartsWith("gc.", StringComparison.Ordinal) || metric.StartsWith("gap.", StringComparison.Ordinal) ||
                    metric.StartsWith("sampling.", StringComparison.Ordinal)
                    ? " (descriptive)" : " (lower is generally better; see limitations)"));
            text.AppendLine();
            text.AppendLine("| Version / scale / case | Baseline median [min,max] | Candidate median [min,max] | Trials B/C; N B/C | C/B | Change % | Absolute Δ | Observed |");
            text.AppendLine("|-|-:|-:|-|-:|-:|-:|-|");
            foreach (WorkloadCase workload in Workloads.CreateCases())
            {
                (MetricSummary baseline, MetricSummary candidate) = Summaries(evidence.Trials, values, workload.RowKey, metric);
                MetricChange change = CalculateChange(baseline.Median, candidate.Median);
                text.AppendLine("| " + workload.RowKey + " | " + SummaryCell(baseline) + " | " + SummaryCell(candidate) + " | " +
                    baseline.Trials.ToString(CultureInfo.InvariantCulture) + "/" + candidate.Trials.ToString(CultureInfo.InvariantCulture) +
                    "; " + string.Join(",", baseline.Operations) + " / " + string.Join(",", candidate.Operations) + " | " +
                    (change.Ratio is double ratio ? Number(ratio) : "n/a: " + change.Reason) + " | " +
                    (change.Percent is double percent ? Number(percent) : "n/a: " + change.Reason) + " | " + Number(change.Absolute) +
                    " | " + Interpretation(metric, baseline, candidate, CalibrationSpan(evidence, workload.RowKey, metric)) + " |");
            }
        }
    }

    private static void AppendTrialTable(StringBuilder text, ReportEvidence evidence)
    {
        text.AppendLine();
        text.AppendLine("## Individual trials, coverage and boundary gaps (outliers retained)");
        text.AppendLine();
        text.AppendLine("| Attempt / raw directory | Row / variant / repetition | PID / start UTC ticks | N / HTTP requests | T ms | Allocated B | gen0,1,2 / pause ms | Actual managed/process samples | Begin/end gap ms |");
        text.AppendLine("|-|-|-|-:|-:|-:|-|-:|-|");
        foreach (TrialRecord trial in evidence.Trials)
        {
            BatchWindow batch = trial.Batch!;
            CounterReading begin = batch.Server.Begin;
            CounterReading end = batch.Server.End;
            int managed = batch.Server.Samples.Count(sample => sample.Timestamp >= batch.ClientStart && sample.Timestamp <= batch.ClientEnd);
            int process = WorkSamples(batch, trial.ProcessId, trial.ProcessStartUtcTicks).Length;
            text.AppendLine("| `" + Escape(trial.Plan.RelativeDirectory) + "` | " + trial.Plan.RowKey + " / " + trial.Plan.Variant + " / " +
                trial.Plan.Repetition.ToString(CultureInfo.InvariantCulture) + " | " + trial.ProcessId.ToString(CultureInfo.InvariantCulture) + " / " +
                trial.ProcessStartUtcTicks.ToString(CultureInfo.InvariantCulture) + " | " + batch.CompletedOperations.ToString(CultureInfo.InvariantCulture) +
                " / " + batch.Operations.Sum(operation => operation.HttpRequests).ToString(CultureInfo.InvariantCulture) + " | " +
                Number((batch.ClientEnd - batch.ClientStart) * 1000.0 / batch.Frequency) + " | " +
                (end.AllocatedBytes - begin.AllocatedBytes).ToString(CultureInfo.InvariantCulture) + " | " +
                (end.Gen0 - begin.Gen0).ToString(CultureInfo.InvariantCulture) + "," + (end.Gen1 - begin.Gen1).ToString(CultureInfo.InvariantCulture) +
                "," + (end.Gen2 - begin.Gen2).ToString(CultureInfo.InvariantCulture) + " / " +
                Number((end.PauseTicks - begin.PauseTicks) / (double)TimeSpan.TicksPerMillisecond) + " | " +
                managed.ToString(CultureInfo.InvariantCulture) + "/" + process.ToString(CultureInfo.InvariantCulture) + " | " +
                Number((batch.ClientStart - begin.Timestamp) * 1000.0 / batch.Frequency) + " / " +
                Number((end.Timestamp - batch.ClientEnd) * 1000.0 / batch.Frequency) + " |");
        }
    }

    private static void AppendCalibration(StringBuilder text, ReportEvidence evidence)
    {
        text.AppendLine();
        text.AppendLine("## Paired calibration and probe-inclusive background controls");
        text.AppendLine();
        text.AppendLine("Each profile covers both artifacts, the true corpus, normal sampling, sampler-off work, and idle/loaded one-second no-HTTP controls. " +
            "Cells show normal → off: work ms/op, B/op, server CPU ms; controls show allocation B / CPU ms for idle and loaded. " +
            "These single paired pilots diagnose perturbation/resolution, not accepted repetitions or a removable fixed cost. " +
            "Counter boundary offsets and per-control GC/memory timestamps remain in each trial JSON.");
        text.AppendLine();
        text.AppendLine("| Row | Frozen N | Baseline normal → off | Candidate normal → off | Baseline idle/loaded controls (normal; off) | Candidate idle/loaded controls (normal; off) | Retained attempts |");
        text.AppendLine("|-|-:|-|-|-|-|-:|");
        foreach (CalibrationProfile profile in evidence.Frozen!.Calibration)
        {
            string[] values = new[] { "baseline", "candidate" }.Select(variant =>
            {
                TrialRecord normal = evidence.Calibration.Single(trial => profile.NormalAttempts.Contains(trial.Plan.AttemptId) && trial.Plan.Variant == variant);
                TrialRecord off = evidence.Calibration.Single(trial => profile.SamplerOffAttempts.Contains(trial.Plan.AttemptId) && trial.Plan.Variant == variant);
                return WorkCalibrationCell(normal.Batch!) + " → " + WorkCalibrationCell(off.Batch!);
            }).ToArray();
            string[] controls = new[] { "baseline", "candidate" }.Select(variant =>
            {
                TrialRecord normal = evidence.Calibration.Single(trial => profile.NormalAttempts.Contains(trial.Plan.AttemptId) && trial.Plan.Variant == variant);
                TrialRecord off = evidence.Calibration.Single(trial => profile.SamplerOffAttempts.Contains(trial.Plan.AttemptId) && trial.Plan.Variant == variant);
                return string.Join(", ", normal.Controls.Select(ControlCell)) + "; " + string.Join(", ", off.Controls.Select(ControlCell));
            }).ToArray();
            text.AppendLine("| " + profile.RowKey + " | " + profile.Operations.ToString(CultureInfo.InvariantCulture) + " | " +
                values[0] + " | " + values[1] + " | " + controls[0] + " | " + controls[1] + " | " +
                profile.Attempts.Length.ToString(CultureInfo.InvariantCulture) + " |");
        }

        TrialRecord[] inadequate = evidence.Calibration.Where(trial => trial.Status != "passed").ToArray();
        text.AppendLine();
        text.AppendLine("Retained coverage-insufficient pilots: " + inadequate.Length.ToString(CultureInfo.InvariantCulture) + ".");
        foreach (TrialRecord trial in inadequate)
        {
            text.AppendLine("- `" + Escape(trial.Plan.RelativeDirectory) + "` N=" + trial.Plan.Operations.ToString(CultureInfo.InvariantCulture) +
                ": " + Escape(trial.Failure ?? trial.Status));
        }
    }

    private static void AppendPopulations(StringBuilder text, ReportEvidence evidence)
    {
        text.AppendLine();
        text.AppendLine("## State/population and input-size ledger");
        text.AppendLine();
        text.AppendLine("The counts below are checked in every independent trial; each raw initial snapshot retains **all resource types, including zeros**. " +
            "Built-in conformance populations are preserved rather than normalized away. Natural/forced footprint deltas above are whole-server deltas, not clinical-object-only sizes.");
        text.AppendLine();
        text.AppendLine("| Row | Warm/measured payload B | Prepared P/O/Bundle/receipt | Post-work P/O/Bundle/receipt |");
        text.AppendLine("|-|-:|-|-|");
        foreach (FrozenCaseInput input in evidence.Frozen!.Inputs)
        {
            TrialRecord[] trials = evidence.Trials.Where(trial => trial.Plan.RowKey == input.Case.RowKey).ToArray();
            string[] loaded = trials.Select(trial => PopulationCell(evidence.Populations[trial.Plan.AttemptId], false)).Distinct(StringComparer.Ordinal).ToArray();
            string[] post = trials.Select(trial => PopulationCell(evidence.Populations[trial.Plan.AttemptId], true)).Distinct(StringComparer.Ordinal).ToArray();
            text.AppendLine("| " + input.Case.RowKey + " | " + input.WarmupPayloadBytes.ToString(CultureInfo.InvariantCulture) + " / " +
                input.MeasuredPayloadBytes.ToString(CultureInfo.InvariantCulture) + " | " + string.Join("; ", loaded) + " | " + string.Join("; ", post) + " |");
        }

        text.AppendLine();
        text.AppendLine("Initial nonzero built-in inventories (complete zero-inclusive maps are in every `ready-snapshot.json`):");
        foreach (string version in new[] { "R4", "R4B", "R5" })
        {
            foreach (string variant in new[] { "baseline", "candidate" })
            {
                string[] inventories = evidence.Trials.Where(trial => trial.Plan.Variant == variant && trial.Plan.RowKey.StartsWith(version + "-", StringComparison.Ordinal))
                    .Select(trial => string.Join(", ", evidence.Populations[trial.Plan.AttemptId].Initial.Where(pair => pair.Value != 0)
                        .Select(pair => pair.Key + "=" + pair.Value.ToString(CultureInfo.InvariantCulture)))).Distinct(StringComparer.Ordinal).ToArray();
                text.AppendLine("- " + version + " / " + variant + ": " + string.Join("; distinct inventory: ", inventories));
            }
        }
    }

    private static string PopulationCell(PopulationSummary population, bool post)
    {
        SortedDictionary<string, int> counts = post ? population.Post : population.Loaded;
        return counts["Patient"].ToString(CultureInfo.InvariantCulture) + "/" + counts["Observation"].ToString(CultureInfo.InvariantCulture) +
            "/" + counts["Bundle"].ToString(CultureInfo.InvariantCulture) + "/" +
            (post ? population.PostReceipts : population.LoadedReceipts).ToString(CultureInfo.InvariantCulture);
    }

    private static string ControlCell(BatchWindow window) =>
        (window.Server.End.AllocatedBytes - window.Server.Begin.AllocatedBytes).ToString(CultureInfo.InvariantCulture) + " / " +
        Number((window.Server.End.CpuTicks - window.Server.Begin.CpuTicks) / (double)TimeSpan.TicksPerMillisecond);

    private static string WorkCalibrationCell(BatchWindow window) =>
        Number((window.ClientEnd - window.ClientStart) * 1000.0 / window.Frequency / window.ExpectedOperations) + ", " +
        Number((window.Server.End.AllocatedBytes - window.Server.Begin.AllocatedBytes) / (double)window.ExpectedOperations) + ", " +
        Number((window.Server.End.CpuTicks - window.Server.Begin.CpuTicks) / (double)TimeSpan.TicksPerMillisecond);

    private static (MetricSummary Baseline, MetricSummary Candidate) Summaries(
        IEnumerable<TrialRecord> trials, Dictionary<string, Dictionary<string, double>> values, string row, string metric)
    {
        MetricSummary Summary(string variant) => CalculateSummary(trials.Where(trial => trial.Plan.RowKey == row && trial.Plan.Variant == variant)
            .Select(trial => (values[trial.Plan.AttemptId][metric], trial.Plan.Operations)));
        return (Summary("baseline"), Summary("candidate"));
    }

    private static double CalibrationSpan(ReportEvidence evidence, string row, string metric)
    {
        CalibrationProfile profile = evidence.Frozen!.Calibration.Single(item => item.RowKey == row);
        double span = 0;
        foreach (string variant in new[] { "baseline", "candidate" })
        {
            TrialRecord normal = evidence.Calibration.Single(trial => profile.NormalAttempts.Contains(trial.Plan.AttemptId) && trial.Plan.Variant == variant);
            TrialRecord off = evidence.Calibration.Single(trial => profile.SamplerOffAttempts.Contains(trial.Plan.AttemptId) && trial.Plan.Variant == variant);
            double? Value(TrialRecord trial)
            {
                BatchWindow window = trial.Batch!;
                if (metric == "time.ms/op")
                {
                    return (window.ClientEnd - window.ClientStart) * 1000.0 / window.Frequency / window.ExpectedOperations;
                }

                if (metric == "throughput.op/s")
                {
                    return window.ExpectedOperations * (double)window.Frequency / (window.ClientEnd - window.ClientStart);
                }

                if (metric == "allocation.B/op")
                {
                    return (window.Server.End.AllocatedBytes - window.Server.Begin.AllocatedBytes) / (double)window.ExpectedOperations;
                }

                foreach (Footprint point in trial.Checkpoints)
                {
                    if (metric == "managed." + point.Name + "-occupied.B")
                    {
                        return point.Managed.OccupiedBytes;
                    }

                    if (metric == "process." + point.Name + "-working.B")
                    {
                        return point.WorkingSetBytes;
                    }

                    if (metric == "process." + point.Name + "-private.B")
                    {
                        return point.PrivateBytes;
                    }
                }

                // Sampler-off has no active peak; never invent one to classify perturbation.
                return null;
            }

            double? sampled = Value(normal);
            double? unsampled = Value(off);
            if (sampled is not null && unsampled is not null)
            {
                span = Math.Max(span, Math.Abs(sampled.Value - unsampled.Value));
            }
        }

        return span;
    }

    private static string Interpretation(string metric, MetricSummary baseline, MetricSummary candidate, double calibrationSpan = 0)
    {
        if (metric.StartsWith("gc.", StringComparison.Ordinal) || metric.StartsWith("gap.", StringComparison.Ordinal) ||
            metric.StartsWith("sampling.", StringComparison.Ordinal))
        {
            return "descriptive";
        }

        if (baseline.Median <= 0 || Math.Abs(baseline.Median - candidate.Median) <= calibrationSpan ||
            (candidate.Minimum <= baseline.Maximum && baseline.Minimum <= candidate.Maximum))
        {
            return "inconclusive";
        }

        bool increase = candidate.Median > baseline.Median;
        bool higherBetter = metric.StartsWith("throughput.", StringComparison.Ordinal);
        return increase == higherBetter ? "improved" : "regressed";
    }

    private static string Number(double value) => value.ToString("G9", CultureInfo.InvariantCulture);
    private static string SummaryCell(MetricSummary summary) => Number(summary.Median) + " [" + Number(summary.Minimum) + ", " + Number(summary.Maximum) + "]";
    private static string Escape(string value) => value.Replace("|", "\\|", StringComparison.Ordinal).Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);

    private static bool IsPilotCount(int count, int initial, int maximum) =>
        count >= initial && count <= maximum && count % initial == 0 &&
        ((count / initial) & ((count / initial) - 1)) == 0;

    private static bool StrictlyIncreasing(IEnumerable<long> timestamps)
    {
        long previous = -1;
        foreach (long timestamp in timestamps)
        {
            if (timestamp <= previous)
            {
                return false;
            }

            previous = timestamp;
        }

        return true;
    }

    private static void Check(List<string> errors, bool condition, string message)
    {
        if (!condition)
        {
            errors.Add(message);
        }
    }
}
