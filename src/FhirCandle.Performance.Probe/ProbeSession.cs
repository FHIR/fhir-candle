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
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Win32.SafeHandles;

namespace FhirCandle.Performance.Probe;

/// <summary>Child-only activation and lifetime settings. The bearer nonce is never saved in evidence.</summary>
public sealed record ProbeConfiguration(
    string PipeName,
    int ParentProcessId,
    long ParentStartTimeUtcTicks,
    string OwnerSid,
    string Token,
    DateTimeOffset DeadlineUtc,
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

        if ((value.Length == 0) || (value.Length > ProbeProtocol.MaximumRequestBytes))
        {
            throw new InvalidDataException("Empty or oversized startup-hook configuration.");
        }

        ProbeConfiguration configuration = JsonSerializer.Deserialize<ProbeConfiguration>(value, ProbeProtocol.JsonOptions)
            ?? throw new InvalidDataException("Missing startup-hook configuration.");
        configuration.Validate();
        return configuration;
    }

    /// <summary>Rejects weak names, incomplete ownership and unbounded sessions before opening a pipe.</summary>
    public void Validate()
    {
        if (string.IsNullOrEmpty(PipeName) || (PipeName.Length != 44) || !PipeName.StartsWith("candle-perf-", StringComparison.Ordinal) ||
            !Guid.TryParseExact(PipeName[12..], "N", out _) ||
            (ParentProcessId <= 0) || (ParentStartTimeUtcTicks <= 0) || string.IsNullOrEmpty(OwnerSid) ||
            !ProbeProtocol.IsNonce(Token) || (RequestTimeoutSeconds < 1) || (RequestTimeoutSeconds > 60) ||
            (DeadlineUtc <= DateTimeOffset.UtcNow) || (DeadlineUtc > DateTimeOffset.UtcNow.AddMinutes(20)))
        {
            throw new InvalidDataException("Invalid bounded probe ownership configuration.");
        }
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

/// <summary>A stopped inspection or shutdown request; phase one has no counter-window operations.</summary>
public sealed record ProbeCommand(int Sequence, string Operation, string Tenant, string[] ResourceIdentities);

/// <summary>A bounded reply. A missing snapshot cannot be mistaken for successful inspection.</summary>
public sealed record ProbeReply(int Sequence, ProbeSnapshot? Snapshot, bool Shutdown);

/// <summary>Length-prefixed BCL protocol shared by the external runner and the injected net8 probe.</summary>
public static class ProbeProtocol
{
    /// <summary>Changing this protocol requires a new run ID and fresh preflight evidence.</summary>
    public const string Version = "candle-perf-phase1-v1";

    /// <summary>Maximum bytes in any command, hello or activation configuration.</summary>
    public const int MaximumRequestBytes = 4096;

    /// <summary>Maximum stopped-snapshot bytes; there is no unbounded read-to-end.</summary>
    public const int MaximumResponseBytes = 2 * 1024 * 1024;

    /// <summary>Maximum commands during one bounded child lifetime.</summary>
    public const int MaximumCommands = 64;

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
        byte[] header = new byte[sizeof(int)];
        await stream.ReadExactlyAsync(header, cancellationToken);
        int length = BinaryPrimitives.ReadInt32LittleEndian(header);
        if ((length <= 0) || (length > maximumBytes) || (maximumBytes > MaximumResponseBytes))
        {
            throw new InvalidDataException("Probe frame length exceeds the allowed bound.");
        }

        byte[] payload = new byte[length];
        await stream.ReadExactlyAsync(payload, cancellationToken);
        return JsonSerializer.Deserialize<T>(payload, JsonOptions)
            ?? throw new InvalidDataException("Null protocol payload.");
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
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions);
        if ((payload.Length == 0) || (payload.Length > maximumBytes) || (maximumBytes > MaximumResponseBytes))
        {
            throw new InvalidDataException("Probe reply exceeds the allowed bound.");
        }

        byte[] header = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(header, payload.Length);
        await stream.WriteAsync(header, cancellationToken);
        await stream.WriteAsync(payload, cancellationToken);
        await stream.FlushAsync(cancellationToken);
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

/// <summary>The child-side, opt-in stopped inspection session.</summary>
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
        using CancellationTokenSource lifetime = new(configuration.DeadlineUtc - DateTimeOffset.UtcNow);
        using NamedPipeClientStream pipe = new(".", configuration.PipeName, PipeDirection.InOut,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        await pipe.ConnectAsync(lifetime.Token);
        int peer = ProbeProtocol.GetPeerProcessId(pipe, server: false);
        using Process current = Process.GetCurrentProcess();
        ProbeHello hello = new(
            ProbeProtocol.Version, configuration.ParentProcessId, configuration.ParentStartTimeUtcTicks,
            current.Id, current.StartTime.ToUniversalTime().Ticks, configuration.OwnerSid, configuration.Token,
            ProbeProtocol.ProbeHash());
        using (CancellationTokenSource handshake = RequestDeadline(configuration, lifetime.Token))
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

        for (int sequence = 1; sequence <= ProbeProtocol.MaximumCommands; sequence++)
        {
            // Waiting for setup/readiness is bounded by the entire session; each received command has its own limit.
            ProbeCommand command = await ProbeProtocol.ReadCommandAsync(pipe, configuration.RequestTimeoutSeconds, lifetime.Token);
            VerifyParent(configuration);
            if ((command.Sequence != sequence) || (command.Tenant != "perf") ||
                (command.ResourceIdentities is null) || (command.ResourceIdentities.Length > 32))
            {
                throw new InvalidDataException("Invalid probe command sequence or tenant.");
            }

            using CancellationTokenSource request = RequestDeadline(configuration, lifetime.Token);
            switch (command.Operation)
            {
                case "snapshot":
                    ProbeSnapshot snapshot = StoreInspection.Capture(host.Services, command.Tenant, command.ResourceIdentities);
                    request.Token.ThrowIfCancellationRequested();
                    await ProbeProtocol.WriteAsync(pipe, new ProbeReply(sequence, snapshot, false),
                        ProbeProtocol.MaximumResponseBytes, request.Token);
                    break;
                case "shutdown":
                    await ProbeProtocol.WriteAsync(pipe, new ProbeReply(sequence, null, true),
                        ProbeProtocol.MaximumResponseBytes, request.Token);
                    StoreInspection.RequestShutdown(host.Services);
                    return;
                default:
                    throw new InvalidDataException("Unsupported phase-one probe operation.");
            }
        }

        throw new InvalidDataException("Probe command budget exhausted without shutdown.");
    }

    private static CancellationTokenSource RequestDeadline(ProbeConfiguration configuration, CancellationToken token)
    {
        CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(configuration.RequestTimeoutSeconds));
        return deadline;
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
