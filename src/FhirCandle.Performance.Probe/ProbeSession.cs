// <copyright file="ProbeSession.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using System.Buffers.Binary;
using System.ComponentModel;
using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Win32.SafeHandles;

namespace FhirCandle.Performance.Probe;

/// <summary>An unmodified UTC read bracketed by two independent monotonic readings on the same machine.</summary>
public readonly record struct ClockObservation(
    [property: JsonRequired] DateTimeOffset Utc, [property: JsonRequired] long Before,
    [property: JsonRequired] long After, [property: JsonRequired] long Frequency)
{
    /// <summary>Uses the real clocks in live work; a controlled BCL provider supplies deterministic regression inputs.</summary>
    public static ClockObservation Capture(TimeProvider? provider = null)
    {
        TimeProvider clock = provider ?? TimeProvider.System;
        long before = clock.GetTimestamp();
        DateTimeOffset utc = clock.GetUtcNow();
        long after = clock.GetTimestamp();
        return new(utc, before, after, clock.TimestampFrequency);
    }

    /// <summary>Missing/default clocks, inverted brackets and non-UTC observations are not usable evidence.</summary>
    public bool IsValid() => Utc != default && Utc.Offset == TimeSpan.Zero && Before > 0 && After >= Before &&
        Frequency > 0 && Frequency <= long.MaxValue / ProbeProtocol.ChildLifetimeSeconds;

    /// <summary>Ordering uses disjoint monotonic brackets, never the sign of a wall-clock difference.</summary>
    public static bool IsOrdered(ClockObservation first, ClockObservation second) =>
        first.IsValid() && second.IsValid() && first.Frequency == second.Frequency && first.After < second.Before;

    /// <summary>
    /// Bounds elapsed time and net UTC adjustment by the actual read brackets, rounding outwards to 100 ns.
    /// The UTC envelope is the union's outer bounds of the two endpoint clock trajectories during the request.
    /// It does not reconstruct a step's instant, or claim to detect cancelling/unobserved intermediate steps.
    /// </summary>
    public static ClockAssessment Assess(ClockObservation first, ClockObservation second)
    {
        if (!IsOrdered(first, second))
        {
            throw new InvalidDataException("Missing, default, mismatched or reordered monotonic clock observations.");
        }

        decimal minimum = decimal.Floor((decimal)(second.Before - first.After) * TimeSpan.TicksPerSecond / first.Frequency);
        decimal maximum = decimal.Ceiling((decimal)(second.After - first.Before) * TimeSpan.TicksPerSecond / first.Frequency);
        decimal earliest = Math.Min(first.Utc.UtcTicks, second.Utc.UtcTicks - maximum);
        decimal latest = Math.Max(second.Utc.UtcTicks, first.Utc.UtcTicks + maximum);
        if (maximum > long.MaxValue || earliest < DateTimeOffset.MinValue.UtcTicks || latest > DateTimeOffset.MaxValue.UtcTicks)
        {
            throw new InvalidDataException("Clock-derived UTC envelope exceeds the representable evidence range.");
        }

        long elapsed = second.Utc.UtcTicks - first.Utc.UtcTicks;
        return new((long)minimum, (long)maximum, checked(elapsed - (long)maximum), checked(elapsed - (long)minimum),
            new DateTimeOffset((long)earliest, TimeSpan.Zero), new DateTimeOffset((long)latest, TimeSpan.Zero));
    }
}

/// <summary>Derived uncertainty, not replacement UTC observations or a blanket timestamp tolerance.</summary>
public readonly record struct ClockAssessment(
    long MinimumElapsedTicks, long MaximumElapsedTicks, long MinimumAdjustmentTicks, long MaximumAdjustmentTicks,
    DateTimeOffset EarliestUtc, DateTimeOffset LatestUtc)
{
    /// <summary>A net clock change is detectable only when zero lies outside the endpoint uncertainty interval.</summary>
    public bool AdjustmentDetected => MinimumAdjustmentTicks > 0 || MaximumAdjustmentTicks < 0;

    /// <summary>Only this evidence-derived request envelope may bind newly assigned persistence/receipt timestamps.</summary>
    public bool Contains(DateTimeOffset timestamp) => timestamp != default && timestamp >= EarliestUtc && timestamp <= LatestUtc;
}

/// <summary>Exact parent identity scopes lifecycle ordering; its OS start timestamp is an identity, not an elapsed clock.</summary>
public readonly record struct ParentIdentity(
    [property: JsonRequired] int ProcessId, [property: JsonRequired] long ProcessStartUtcTicks)
{
    /// <summary>Captures the same process identity used by the authenticated startup-hook handshake.</summary>
    public static ParentIdentity Capture()
    {
        using Process process = Process.GetCurrentProcess();
        return new(process.Id, process.StartTime.ToUniversalTime().Ticks);
    }

    /// <summary>Default or missing process identity cannot establish sequential ownership.</summary>
    public bool IsValid() => ProcessId > 0 && ProcessStartUtcTicks > 0;
}

/// <summary>Child-only activation and lifetime settings. The bearer nonce is never saved in evidence.</summary>
public sealed record ProbeConfiguration(
    string PipeName,
    int ParentProcessId,
    long ParentStartTimeUtcTicks,
    string OwnerSid,
    string Token,
    [property: JsonRequired] ClockObservation Issued,
    [property: JsonRequired] long DeadlineTimestamp,
    int RequestTimeoutSeconds)
{
    /// <summary>The only activation variable read by the hook.</summary>
    public const string EnvironmentVariable = "FHIR_CANDLE_PERFORMANCE_PROBE";

    /// <summary>Returns null without allocating a session, pipe or observer when configuration is absent.</summary>
    public static ProbeConfiguration? FromEnvironment(Func<string, string?> getVariable)
    {
        ArgumentNullException.ThrowIfNull(getVariable);
        string? value = getVariable(EnvironmentVariable);
        if (value is null)
        {
            return null;
        }

        if ((value.Length == 0) || (value.Length > ProbeProtocol.MaximumRequestBytes) ||
            (Encoding.UTF8.GetByteCount(value) > ProbeProtocol.MaximumRequestBytes))
        {
            throw new InvalidDataException("Empty or oversized startup-hook configuration.");
        }

        ProbeConfiguration configuration = JsonSerializer.Deserialize<ProbeConfiguration>(value, ProbeProtocol.JsonOptions)
            ?? throw new InvalidDataException("Missing startup-hook configuration.");
        configuration.Validate();
        return configuration;
    }

    /// <summary>Rejects weak names, incomplete ownership and unbounded sessions before opening a pipe.</summary>
    public void Validate() => _ = RemainingLifetime(Stopwatch.GetTimestamp(), Stopwatch.Frequency);

    /// <summary>UTC steps cannot extend or consume the exact finite monotonic activation/CTS budget.</summary>
    public TimeSpan RemainingLifetime(long timestamp, long frequency)
    {
        if (string.IsNullOrEmpty(PipeName) || (PipeName.Length != 44) || !PipeName.StartsWith("candle-perf-", StringComparison.Ordinal) ||
            !Guid.TryParseExact(PipeName[12..], "N", out _) ||
            (ParentProcessId <= 0) || (ParentStartTimeUtcTicks <= 0) || string.IsNullOrEmpty(OwnerSid) ||
            !ProbeProtocol.IsNonce(Token) || (RequestTimeoutSeconds != ProbeProtocol.RequestSeconds) ||
            !Issued.IsValid() || frequency != Issued.Frequency || timestamp < Issued.After ||
            Issued.Before > long.MaxValue - (frequency * ProbeProtocol.ChildLifetimeSeconds) ||
            DeadlineTimestamp != Issued.Before + (frequency * ProbeProtocol.ChildLifetimeSeconds) || timestamp >= DeadlineTimestamp)
        {
            throw new InvalidDataException("Invalid bounded probe ownership configuration.");
        }

        return TimeSpan.FromTicks(checked((long)decimal.Floor(
            (decimal)(DeadlineTimestamp - timestamp) * TimeSpan.TicksPerSecond / frequency)));
    }
}

/// <summary>Authenticated process ownership and identical probe identity in both directions.</summary>
public sealed record ProbeHello(
    string ProtocolVersion,
    int ParentProcessId,
    long ParentStartTimeUtcTicks,
    int ChildProcessId,
    long ChildStartTimeUtcTicks,
    string OwnerSid,
    string Token,
    string ProbeSha256);

/// <summary>A stopped command. An armed window accepts only the fixed binary end marker.</summary>
public sealed record ProbeCommand(
    int Sequence, string Operation, string Tenant, string[] ResourceIdentities,
    bool Sampling = true, bool PostWorkCheckpoints = false);

/// <summary>A bounded reply. A missing snapshot cannot be mistaken for successful inspection.</summary>
public sealed record ProbeReply(
    int Sequence, ProbeSnapshot? Snapshot, bool Shutdown, WindowEvidence? Window = null,
    Footprint[]? Checkpoints = null, IdentityInspection? Identities = null);

/// <summary>A stopped identity chunk; the complete receipt inventory is exported separately, never sampled.</summary>
public sealed record IdentityInspection(
    int ProcessId, Dictionary<string, ResourceIdentity?> Resources, SortedDictionary<string, int> Counts,
    int ReceiptCount, int EventCount);

/// <summary>All counters have the same process-start origin and the same begin/end interval.</summary>
public readonly record struct CounterReading(
    [property: JsonRequired] long Timestamp, [property: JsonRequired] long AllocatedBytes,
    [property: JsonRequired] int Gen0, [property: JsonRequired] int Gen1, [property: JsonRequired] int Gen2,
    [property: JsonRequired] long PauseTicks, [property: JsonRequired] long CpuTicks);

/// <summary>Estimated occupied memory and last-GC values, not continuous live-retention observations.</summary>
public readonly record struct ManagedSample(
    [property: JsonRequired] long Timestamp, [property: JsonRequired] long OccupiedBytes,
    [property: JsonRequired] long HeapBytes, [property: JsonRequired] long FragmentedBytes,
    [property: JsonRequired] long CommittedBytes, [property: JsonRequired] long LastGcIndex);

/// <summary>A stopped footprint captured before diagnostic DTOs, reflection or response export.</summary>
public readonly record struct Footprint(
    [property: JsonRequired] string Name, [property: JsonRequired] bool ForcedGc,
    [property: JsonRequired] int ProcessId, [property: JsonRequired] long Timestamp, [property: JsonRequired] long Frequency,
    [property: JsonRequired] long WorkingSetBytes, [property: JsonRequired] long PrivateBytes,
    [property: JsonRequired] ManagedSample Managed, [property: JsonRequired] CounterReading Counters);

/// <summary>Stopped export of one bounded counter window; empty streams are not zero-valued measurements.</summary>
public sealed record WindowEvidence(
    [property: JsonRequired] string CounterOrigin, [property: JsonRequired] int ProcessId,
    [property: JsonRequired] long ProcessStartUtcTicks, [property: JsonRequired] long Frequency,
    [property: JsonRequired] bool Sampling, [property: JsonRequired] int Capacity, [property: JsonRequired] int CadenceMilliseconds,
    [property: JsonRequired] CounterReading Begin, [property: JsonRequired] CounterReading End,
    [property: JsonRequired] ManagedSample[] Samples, [property: JsonRequired] int DroppedSamples,
    [property: JsonRequired] int SamplingErrors, [property: JsonRequired] bool DeadlineExceeded,
    [property: JsonRequired] Footprint[] PostWork);

/// <summary>Length-prefixed BCL protocol shared by the external runner and the injected net8 probe.</summary>
public static class ProbeProtocol
{
    /// <summary>Changing this protocol requires a new run ID and fresh preflight evidence.</summary>
    public const string Version = "candle-perf-phase3-v3";

    /// <summary>Maximum bytes in any command, hello or activation configuration.</summary>
    public const int MaximumRequestBytes = 4096;

    /// <summary>Maximum stopped-snapshot bytes; there is no unbounded read-to-end.</summary>
    public const int MaximumResponseBytes = 256 * 1024 * 1024;

    /// <summary>Maximum commands during one bounded child lifetime.</summary>
    public const int MaximumCommands = 4096;

    /// <summary>Maximum identities per stopped inspection; every chunk is still audited.</summary>
    public const int MaximumIdentities = 32;

    /// <summary>Common startup/readiness allowance, not added again after activation.</summary>
    public const int StartupSeconds = 120;

    /// <summary>Cumulative setup and audit allowance outside the fixed work window.</summary>
    public const int SetupSeconds = 1200;

    /// <summary>Common fixed window deadline; changing it requires another protocol and run.</summary>
    public const int WindowSeconds = 7200;

    /// <summary>Ordinary HTTP/pipe exchange and binary end-acknowledgement deadline.</summary>
    public const int RequestSeconds = 30;

    /// <summary>Separate stopped export budget on both peers, never borrowed from the end acknowledgement.</summary>
    public const int StoppedExportSeconds = 120;

    /// <summary>Identical natural and post-full-GC settling interval.</summary>
    public const int SettlingMilliseconds = 1000;

    /// <summary>Each graceful or exact-owned-child termination interval.</summary>
    public const int ShutdownSeconds = 15;

    /// <summary>Finite allowance for boundary/settling work outside setup and the operation interval.</summary>
    public const int BoundaryAllowanceSeconds = 10;

    /// <summary>Exact supported activation ceiling, including end acknowledgement, export and both shutdown intervals.</summary>
    public const int ChildLifetimeSeconds = checked(StartupSeconds + SetupSeconds + WindowSeconds +
        RequestSeconds + StoppedExportSeconds + BoundaryAllowanceSeconds + (2 * ShutdownSeconds));

    /// <summary>Identical cadence in both samplers and artifacts.</summary>
    public const int SampleMilliseconds = 10;

    /// <summary>Additional fixed storage for boundary observations in each stream.</summary>
    public const int SampleBoundarySlots = 4;

    /// <summary>Room for the full deadline and boundary observations, not a license to extrapolate peaks.</summary>
    public const int SampleCapacity = checked((WindowSeconds * 1000 / SampleMilliseconds) + SampleBoundarySlots);

    /// <summary>Fixed, preallocated boundary payload size (no JSON while armed).</summary>
    public const int BoundaryBytes = 88;

    /// <summary>The sole byte accepted in place of an armed-window command.</summary>
    public const byte EndMarker = 0x45;

    /// <summary>Only stopped window export receives the larger budget; ordinary exchanges remain thirty seconds.</summary>
    public static int ExchangeSeconds(string operation) =>
        operation == "export-window" ? StoppedExportSeconds : RequestSeconds;

    /// <summary>Starts a new exchange budget, linked only to the supplied lifetime/caller cancellation.</summary>
    public static CancellationTokenSource CreateExchangeDeadline(string operation, CancellationToken token)
    {
        CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(ExchangeSeconds(operation)));
        return deadline;
    }

    /// <summary>Checks positive monotonic duration without overflowing either clock multiplication or subtraction.</summary>
    public static bool IsWithinWindow(long begin, long end, long frequency) =>
        frequency > 0 && frequency <= long.MaxValue / WindowSeconds && begin >= 0 && end > begin &&
        end - begin <= frequency * WindowSeconds;

    /// <summary>Writes a fixed binary boundary into an already allocated buffer.</summary>
    public static void EncodeBoundary(byte[] buffer, int sequence, int processId, long startTicks, CounterReading reading)
    {
        if (buffer.Length != BoundaryBytes)
        {
            throw new InvalidDataException("Incorrect boundary buffer capacity.");
        }

        BinaryPrimitives.WriteInt32LittleEndian(buffer, 0x33504643);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(4), sequence);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(8), processId);
        BinaryPrimitives.WriteInt64LittleEndian(buffer.AsSpan(12), startTicks);
        BinaryPrimitives.WriteInt64LittleEndian(buffer.AsSpan(20), Stopwatch.Frequency);
        BinaryPrimitives.WriteInt64LittleEndian(buffer.AsSpan(28), reading.Timestamp);
        BinaryPrimitives.WriteInt64LittleEndian(buffer.AsSpan(36), reading.AllocatedBytes);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(44), reading.Gen0);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(48), reading.Gen1);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(52), reading.Gen2);
        BinaryPrimitives.WriteInt64LittleEndian(buffer.AsSpan(56), reading.PauseTicks);
        BinaryPrimitives.WriteInt64LittleEndian(buffer.AsSpan(64), reading.CpuTicks);
        BinaryPrimitives.WriteInt64LittleEndian(buffer.AsSpan(72), 0);
        BinaryPrimitives.WriteInt64LittleEndian(buffer.AsSpan(80), 0);
    }

    /// <summary>Authenticates a binary boundary against the already authenticated child lifetime.</summary>
    public static CounterReading DecodeBoundary(byte[] buffer, int sequence, int processId, long startTicks)
    {
        if (buffer.Length != BoundaryBytes || BinaryPrimitives.ReadInt32LittleEndian(buffer) != 0x33504643 ||
            BinaryPrimitives.ReadInt32LittleEndian(buffer.AsSpan(4)) != sequence ||
            BinaryPrimitives.ReadInt32LittleEndian(buffer.AsSpan(8)) != processId ||
            BinaryPrimitives.ReadInt64LittleEndian(buffer.AsSpan(12)) != startTicks ||
            BinaryPrimitives.ReadInt64LittleEndian(buffer.AsSpan(20)) != Stopwatch.Frequency ||
            BinaryPrimitives.ReadInt64LittleEndian(buffer.AsSpan(72)) != 0 ||
            BinaryPrimitives.ReadInt64LittleEndian(buffer.AsSpan(80)) != 0)
        {
            throw new InvalidDataException("Wrong binary counter boundary, process lifetime or clock.");
        }

        return new(BinaryPrimitives.ReadInt64LittleEndian(buffer.AsSpan(28)),
            BinaryPrimitives.ReadInt64LittleEndian(buffer.AsSpan(36)),
            BinaryPrimitives.ReadInt32LittleEndian(buffer.AsSpan(44)),
            BinaryPrimitives.ReadInt32LittleEndian(buffer.AsSpan(48)),
            BinaryPrimitives.ReadInt32LittleEndian(buffer.AsSpan(52)),
            BinaryPrimitives.ReadInt64LittleEndian(buffer.AsSpan(56)),
            BinaryPrimitives.ReadInt64LittleEndian(buffer.AsSpan(64)));
    }

    /// <summary>Strict protocol parsing rejects unknown fields and excessive JSON depth.</summary>
    public static JsonSerializerOptions JsonOptions { get; } = new()
    {
        MaxDepth = 64,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    /// <summary>Creates the runner's non-inheritable, single-instance, current-user-only pipe.</summary>
    public static NamedPipeServerStream CreateServer(string name) =>
        new(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly,
            MaximumRequestBytes, MaximumRequestBytes);

    /// <summary>Reads a bounded frame before allocating its payload.</summary>
    public static async Task<T> ReadAsync<T>(Stream stream, int maximumBytes, CancellationToken cancellationToken)
    {
        ValidateFrameBound(maximumBytes);
        byte[] header = new byte[sizeof(int)];
        await stream.ReadExactlyAsync(header, cancellationToken);
        int length = BinaryPrimitives.ReadInt32LittleEndian(header);
        if ((length <= 0) || (length > maximumBytes) || (length > Array.MaxLength))
        {
            throw new InvalidDataException("Probe frame length exceeds the allowed bound.");
        }

        byte[] payload = new byte[length];
        await stream.ReadExactlyAsync(payload, cancellationToken);
        T value = JsonSerializer.Deserialize<T>(payload, JsonOptions)
            ?? throw new InvalidDataException("Null protocol payload.");
        cancellationToken.ThrowIfCancellationRequested();
        return value;
    }

    internal static async Task<ProbeCommand> ReadCommandAsync(
        Stream stream, int requestSeconds, CancellationToken lifetime)
    {
        // Setup may be idle for longer than one exchange, but a partial frame must not borrow that budget.
        byte[] header = new byte[sizeof(int)];
        await stream.ReadExactlyAsync(header.AsMemory(0, 1), lifetime);
        using CancellationTokenSource frame = CancellationTokenSource.CreateLinkedTokenSource(lifetime);
        frame.CancelAfter(TimeSpan.FromSeconds(requestSeconds));
        await stream.ReadExactlyAsync(header.AsMemory(1), frame.Token);
        int length = BinaryPrimitives.ReadInt32LittleEndian(header);
        if ((length <= 0) || (length > MaximumRequestBytes))
        {
            throw new InvalidDataException("Probe command frame exceeds its bound.");
        }

        byte[] payload = new byte[length];
        await stream.ReadExactlyAsync(payload, frame.Token);
        return JsonSerializer.Deserialize<ProbeCommand>(payload, JsonOptions)
            ?? throw new InvalidDataException("Null probe command.");
    }

    /// <summary>Writes exactly one bounded frame.</summary>
    public static async Task WriteAsync<T>(Stream stream, T value, int maximumBytes, CancellationToken cancellationToken)
    {
        ValidateFrameBound(maximumBytes);
        cancellationToken.ThrowIfCancellationRequested();
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions);
        if ((payload.Length == 0) || (payload.Length > maximumBytes))
        {
            throw new InvalidDataException("Probe reply exceeds the allowed bound.");
        }

        byte[] header = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(header, payload.Length);
        await stream.WriteAsync(header, cancellationToken);
        await stream.WriteAsync(payload, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    private static void ValidateFrameBound(int maximumBytes)
    {
        if ((maximumBytes <= 0) || (maximumBytes > MaximumResponseBytes) || (maximumBytes > Array.MaxLength))
        {
            throw new InvalidDataException("Invalid finite probe frame bound.");
        }
    }

    /// <summary>Checks OS peer identity as well as the nonce, owner, process start times and probe hash.</summary>
    public static void ValidateHello(
        ProbeHello hello,
        ProbeConfiguration configuration,
        int expectedChildProcessId,
        long expectedChildStartTimeUtcTicks,
        string expectedProbeSha256,
        int actualPeerProcessId,
        int expectedPeerProcessId,
        string currentOwnerSid)
    {
        if ((actualPeerProcessId != expectedPeerProcessId) || (hello.ProtocolVersion != Version) ||
            (hello.ParentProcessId != configuration.ParentProcessId) ||
            (hello.ParentStartTimeUtcTicks != configuration.ParentStartTimeUtcTicks) ||
            (hello.ChildProcessId != expectedChildProcessId) || (hello.ChildStartTimeUtcTicks != expectedChildStartTimeUtcTicks) ||
            (hello.OwnerSid != configuration.OwnerSid) || (hello.OwnerSid != currentOwnerSid) ||
            !IsNonce(hello.Token) || !IsNonce(configuration.Token) ||
            !CryptographicOperations.FixedTimeEquals(Convert.FromHexString(hello.Token), Convert.FromHexString(configuration.Token)) ||
            !string.Equals(hello.ProbeSha256, expectedProbeSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("Probe ownership, peer PID, protocol or binary identity did not match.");
        }
    }

    /// <summary>Returns the actual connected Windows pipe peer PID, not the PID claimed in JSON.</summary>
    public static int GetPeerProcessId(PipeStream stream, bool server)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("This sealed experiment requires Windows pipe PID verification.");
        }

        uint processId;
        bool success = server
            ? GetNamedPipeClientProcessId(stream.SafePipeHandle, out processId)
            : GetNamedPipeServerProcessId(stream.SafePipeHandle, out processId);
        return success ? checked((int)processId) : throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    /// <summary>Returns the current Windows user SID without exporting credentials.</summary>
    public static string CurrentOwnerSid()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("This experiment requires current-user Windows pipes.");
        }

        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        return identity.User?.Value ?? throw new UnauthorizedAccessException("No current-user SID.");
    }

    /// <summary>Hashes the actual probe bytes rather than trusting its assembly version.</summary>
    public static string ProbeHash()
    {
        using FileStream stream = File.OpenRead(typeof(ProbeSession).Assembly.Location);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    internal static bool IsNonce(string? value) =>
        (value is not null) && (value.Length == 64) && value.All(Uri.IsHexDigit);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetNamedPipeClientProcessId(SafePipeHandle pipe, out uint clientProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetNamedPipeServerProcessId(SafePipeHandle pipe, out uint serverProcessId);
}

/// <summary>
/// Preallocated process-wide accounting. The sampler thread is idle outside windows; diagnostics
/// and forced collections are forbidden while armed. Its own cost is deliberately not subtracted.
/// </summary>
public sealed class ProbeWindowRecorder : IDisposable
{
    private readonly Process _process = Process.GetCurrentProcess();
    private readonly ManagedSample[] _samples = new ManagedSample[ProbeProtocol.SampleCapacity];
    private readonly Footprint[] _postWork = new Footprint[3];
    private readonly AutoResetEvent _wake = new(false);
    private readonly ManualResetEvent _idle = new(true);
    private readonly Thread _thread;
    private volatile bool _armed;
    private volatile bool _sampling;
    private volatile bool _disposed;
    private bool _enabled;
    private bool _deadlineExceeded;
    private int _count;
    private int _dropped;
    private int _errors;
    private int _postCount;
    private CounterReading _begin;
    private CounterReading _end;

    /// <summary>Allocates all observer storage and creates its sleeping thread before the idle checkpoint.</summary>
    public ProbeWindowRecorder()
    {
        ProcessId = _process.Id;
        ProcessStartUtcTicks = _process.StartTime.ToUniversalTime().Ticks;
        _thread = new Thread(SamplingLoop) { IsBackground = true, Name = "candle-perf-window-sampler" };
        _thread.Start();
        _ = ReadCounters();
        _ = ReadMemory();
    }

    /// <summary>The exact current child PID.</summary>
    public int ProcessId { get; }
    /// <summary>PID-reuse defense, retained in every exported window.</summary>
    public long ProcessStartUtcTicks { get; }
    /// <summary>Used by the session's failure cleanup, not a background diagnostic poll.</summary>
    public bool IsArmed => _armed;

    /// <summary>Snapshots counters before the fixed acknowledgement; no JSON is constructed here.</summary>
    public CounterReading BeginWindow(bool sampling)
    {
        RequireStopped();
        ObjectDisposedException.ThrowIf(_disposed, this);
        _count = 0;
        _dropped = 0;
        _errors = 0;
        _postCount = 0;
        _deadlineExceeded = false;
        _enabled = sampling;
        _begin = ReadCounters();
        _armed = true;
        if (sampling)
        {
            _idle.Reset();
            _sampling = true;
            _wake.Set();
        }

        return _begin;
    }

    /// <summary>Records one actual sample into a struct slot; overruns/errors remain explicit evidence.</summary>
    public void Sample()
    {
        if (!_armed)
        {
            throw new InvalidOperationException("Sampling outside the armed counter interval is forbidden.");
        }

        if (_count == _samples.Length)
        {
            _dropped++;
            return;
        }

        _samples[_count++] = ReadMemory();
    }

    /// <summary>Stops and joins the sampling iteration before taking end counters and exporting anything.</summary>
    public CounterReading EndWindow()
    {
        if (!_armed)
        {
            throw new InvalidOperationException("No window is armed.");
        }

        _sampling = false;
        _idle.WaitOne();
        _end = ReadCounters();
        _armed = false;
        _deadlineExceeded |= !ProbeProtocol.IsWithinWindow(_begin.Timestamp, _end.Timestamp, Stopwatch.Frequency);
        return _end;
    }

    /// <summary>Natural or blocking full-GC/finalizer/full-GC/one-second-settle footprint, before DTO creation.</summary>
    public Footprint CaptureCheckpoint(string name, bool forcedGc)
    {
        RequireStopped();
        if (forcedGc)
        {
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: false);
            GC.WaitForPendingFinalizers();
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: false);
            Settle();
        }

        _process.Refresh();
        long working = _process.WorkingSet64;
        long privateBytes = _process.PrivateMemorySize64;
        ManagedSample memory = ReadMemory();
        CounterReading counters = ReadCounters();
        return new(name, forcedGc, ProcessId, memory.Timestamp, Stopwatch.Frequency, working, privateBytes, memory, counters);
    }

    /// <summary>Captures all three post-work footprints before any reply/export, reflection or audit request.</summary>
    public void CapturePostWork()
    {
        RequireStopped();
        _postWork[0] = CaptureCheckpoint("post-immediate", false);
        Settle();
        _postWork[1] = CaptureCheckpoint("post-settled", false);
        _postWork[2] = CaptureCheckpoint("post-forced", true);
        _postCount = 3;
    }

    /// <summary>Allocation-heavy diagnostic export is allowed only after the counter/retention windows stop.</summary>
    public WindowEvidence Export()
    {
        RequireStopped();
        if (_end.Timestamp <= _begin.Timestamp)
        {
            throw new InvalidOperationException("No completed counter window.");
        }

        return new("GC-process-start", ProcessId, ProcessStartUtcTicks, Stopwatch.Frequency, _enabled,
            _samples.Length, ProbeProtocol.SampleMilliseconds, _begin, _end, _samples[.._count],
            _dropped, _errors, _deadlineExceeded, _postWork[.._postCount]);
    }

    /// <summary>Stops only this observer's own thread and handles.</summary>
    public void Dispose()
    {
        if (_armed)
        {
            EndWindow();
        }

        _disposed = true;
        _wake.Set();
        _thread.Join();
        _wake.Dispose();
        _idle.Dispose();
        _process.Dispose();
    }

    private CounterReading ReadCounters()
    {
        long cpu = _process.TotalProcessorTime.Ticks;
        long allocated = GC.GetTotalAllocatedBytes(true);
        int gen0 = GC.CollectionCount(0);
        int gen1 = GC.CollectionCount(1);
        int gen2 = GC.CollectionCount(2);
        long pause = GC.GetTotalPauseDuration().Ticks;
        return new(Stopwatch.GetTimestamp(), allocated, gen0, gen1, gen2, pause, cpu);
    }

    private static ManagedSample ReadMemory()
    {
        long occupied = GC.GetTotalMemory(false);
        GCMemoryInfo memory = GC.GetGCMemoryInfo();
        return new(Stopwatch.GetTimestamp(), occupied, memory.HeapSizeBytes, memory.FragmentedBytes,
            memory.TotalCommittedBytes, memory.Index);
    }

    private static void Settle()
    {
        long until = Stopwatch.GetTimestamp() + Stopwatch.Frequency;
        Thread.Sleep(ProbeProtocol.SettlingMilliseconds);
        while (Stopwatch.GetTimestamp() < until)
        {
            Thread.Sleep(1);
        }
    }

    private void SamplingLoop()
    {
        while (true)
        {
            _wake.WaitOne();
            if (_disposed)
            {
                return;
            }

            while (_sampling)
            {
                try
                {
                    Sample();
                }
                catch (Exception)
                {
                    // Never allocate diagnostic messages while armed. A nonzero count invalidates the window.
                    _errors++;
                    _sampling = false;
                }

                if (Stopwatch.GetTimestamp() - _begin.Timestamp > checked(Stopwatch.Frequency * ProbeProtocol.WindowSeconds))
                {
                    _deadlineExceeded = true;
                    _sampling = false;
                }

                if (_sampling)
                {
                    Thread.Sleep(ProbeProtocol.SampleMilliseconds);
                }
            }

            _idle.Set();
        }
    }

    private void RequireStopped()
    {
        if (_armed)
        {
            throw new InvalidOperationException("Inspection/export/forced GC is forbidden during a counter window.");
        }
    }
}

/// <summary>The child-side, opt-in inspection and fixed-window session.</summary>
public static class ProbeSession
{
    internal static async Task RunAsync(ProbeConfiguration configuration, HostObservation host)
    {
        configuration.Validate();
        if ((Environment.Version.Major != 8) || (ProbeProtocol.CurrentOwnerSid() != configuration.OwnerSid))
        {
            throw new InvalidDataException("The injected probe requires the selected .NET 8/current-user context.");
        }

        VerifyParent(configuration);
        using CancellationTokenSource lifetime = new(configuration.RemainingLifetime(Stopwatch.GetTimestamp(), Stopwatch.Frequency));
        using NamedPipeClientStream pipe = new(".", configuration.PipeName, PipeDirection.InOut,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        await pipe.ConnectAsync(lifetime.Token);
        int peer = ProbeProtocol.GetPeerProcessId(pipe, server: false);
        using Process current = Process.GetCurrentProcess();
        ProbeHello hello = new(
            ProbeProtocol.Version, configuration.ParentProcessId, configuration.ParentStartTimeUtcTicks,
            current.Id, current.StartTime.ToUniversalTime().Ticks, configuration.OwnerSid, configuration.Token,
            ProbeProtocol.ProbeHash());
        using (CancellationTokenSource handshake = ProbeProtocol.CreateExchangeDeadline("hello", lifetime.Token))
        {
            // Validate the OS peer before sending the bearer nonce.
            if (peer != configuration.ParentProcessId)
            {
                throw new UnauthorizedAccessException("Unexpected pipe server PID.");
            }

            await ProbeProtocol.WriteAsync(pipe, hello, ProbeProtocol.MaximumRequestBytes, handshake.Token);
            ProbeHello acknowledgement = await ProbeProtocol.ReadAsync<ProbeHello>(
                pipe, ProbeProtocol.MaximumRequestBytes, handshake.Token);
            ProbeProtocol.ValidateHello(acknowledgement, configuration, current.Id, hello.ChildStartTimeUtcTicks,
                hello.ProbeSha256, peer, configuration.ParentProcessId, ProbeProtocol.CurrentOwnerSid());
        }

        using ProbeWindowRecorder recorder = new();
        byte[] beginBuffer = new byte[ProbeProtocol.BoundaryBytes];
        byte[] endBuffer = new byte[ProbeProtocol.BoundaryBytes];
        byte[] endMarker = new byte[1];
        bool exportPending = false;
        for (int sequence = 1; sequence <= ProbeProtocol.MaximumCommands; sequence++)
        {
            // Waiting for setup/readiness is bounded by the entire session; each received command has its own limit.
            ProbeCommand command = await ProbeProtocol.ReadCommandAsync(pipe, configuration.RequestTimeoutSeconds, lifetime.Token);
            VerifyParent(configuration);
            if ((command.Sequence != sequence) || (command.Tenant != "perf") ||
                (command.ResourceIdentities is null) || (command.ResourceIdentities.Length > ProbeProtocol.MaximumIdentities))
            {
                throw new InvalidDataException("Invalid probe command sequence or tenant.");
            }

            using CancellationTokenSource request = ProbeProtocol.CreateExchangeDeadline(command.Operation, lifetime.Token);
            switch (command.Operation)
            {
                case "snapshot":
                    ProbeSnapshot snapshot = StoreInspection.Capture(host.Services, command.Tenant, command.ResourceIdentities);
                    request.Token.ThrowIfCancellationRequested();
                    await ProbeProtocol.WriteAsync(pipe, new ProbeReply(sequence, snapshot, false),
                        ProbeProtocol.MaximumResponseBytes, request.Token);
                    break;
                case "identities":
                    TenantSnapshot inspected = StoreInspection.InspectTenant(StoreInspection.ResolveManager(), command.Tenant, command.ResourceIdentities);
                    IdentityInspection identities = new(current.Id, inspected.RequestedResources, inspected.ResourceCounts,
                        inspected.ReceiptCount, inspected.EventCount);
                    await ProbeProtocol.WriteAsync(pipe, new ProbeReply(sequence, null, false, Identities: identities),
                        ProbeProtocol.MaximumResponseBytes, request.Token);
                    break;
                case "idle-checkpoints":
                case "loaded-checkpoints":
                    string prefix = command.Operation == "idle-checkpoints" ? "idle" : "loaded";
                    Footprint natural = recorder.CaptureCheckpoint(prefix + "-natural", false);
                    Footprint forced = recorder.CaptureCheckpoint(prefix + "-forced", true);
                    await ProbeProtocol.WriteAsync(pipe, new ProbeReply(sequence, null, false, Checkpoints: [natural, forced]),
                        ProbeProtocol.MaximumResponseBytes, request.Token);
                    break;
                case "begin-window":
                    if (exportPending || command.ResourceIdentities.Length != 0)
                    {
                        throw new InvalidDataException("The previous window must be exported before another begins.");
                    }

                    // Allocate timers and buffers before arming. Until EndWindow, there is no command
                    // deserialization, reflection, JSON reply or diagnostic DTO construction.
                    using (CancellationTokenSource window = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token))
                    {
                        window.CancelAfter(TimeSpan.FromSeconds(ProbeProtocol.WindowSeconds));
                        CounterReading begin = recorder.BeginWindow(command.Sampling);
                        try
                        {
                            ProbeProtocol.EncodeBoundary(beginBuffer, sequence, current.Id, hello.ChildStartTimeUtcTicks, begin);
                            await pipe.WriteAsync(beginBuffer, request.Token);
                            await pipe.ReadExactlyAsync(endMarker, window.Token);
                            if (endMarker[0] != ProbeProtocol.EndMarker)
                            {
                                throw new InvalidDataException("Invalid fixed end marker.");
                            }
                        }
                        finally
                        {
                            CounterReading end = recorder.EndWindow();
                            ProbeProtocol.EncodeBoundary(endBuffer, sequence, current.Id, hello.ChildStartTimeUtcTicks, end);
                        }
                    }

                    if (command.PostWorkCheckpoints)
                    {
                        recorder.CapturePostWork();
                    }

                    // The acknowledgement can arrive after retention settling; the saved counter end
                    // precedes it and the client must not include this wait in batch elapsed time.
                    using (CancellationTokenSource ending = ProbeProtocol.CreateExchangeDeadline("end-window", lifetime.Token))
                    {
                        await pipe.WriteAsync(endBuffer, ending.Token);
                    }
                    exportPending = true;
                    break;
                case "export-window":
                    if (!exportPending)
                    {
                        throw new InvalidDataException("No stopped window is available for export.");
                    }

                    WindowEvidence evidence = recorder.Export();
                    await ProbeProtocol.WriteAsync(pipe, new ProbeReply(sequence, null, false, Window: evidence),
                        ProbeProtocol.MaximumResponseBytes, request.Token);
                    exportPending = false;
                    break;
                case "shutdown":
                    await ProbeProtocol.WriteAsync(pipe, new ProbeReply(sequence, null, true),
                        ProbeProtocol.MaximumResponseBytes, request.Token);
                    StoreInspection.RequestShutdown(host.Services);
                    return;
                default:
                    throw new InvalidDataException("Unsupported bounded probe operation.");
            }
        }

        throw new InvalidDataException("Probe command budget exhausted without shutdown.");
    }

    private static void VerifyParent(ProbeConfiguration configuration)
    {
        using Process parent = Process.GetProcessById(configuration.ParentProcessId);
        if (parent.HasExited || (parent.StartTime.ToUniversalTime().Ticks != configuration.ParentStartTimeUtcTicks))
        {
            throw new UnauthorizedAccessException("The owning process exited or its PID was reused.");
        }
    }
}
