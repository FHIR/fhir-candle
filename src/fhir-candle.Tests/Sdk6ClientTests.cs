// <copyright file="Sdk6ClientTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

extern alias candleR4;
extern alias candleR4B;
extern alias candleR5;
extern alias coreR4;
extern alias coreR4B;
extern alias coreR5;

using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using fhir.candle.Tests.Controllers;
using fhir.candle.Tests.Extensions;
using fhir.candle.Tests.Models;
using FhirCandle.Client;
using FhirCandle.Utils;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Xunit.Abstractions;
using R4Client = candleR4::FhirCandle.Client.CandleClient;
using R4BClient = candleR4B::FhirCandle.Client.CandleClient;
using R5Client = candleR5::FhirCandle.Client.CandleClient;
using R4Patient = coreR4::Hl7.Fhir.Model.Patient;
using R4BPatient = coreR4B::Hl7.Fhir.Model.Patient;
using R5Patient = coreR5::Hl7.Fhir.Model.Patient;

namespace fhir.candle.Tests;

/// <summary>Real aliased wrappers talk TCP to an independent, finite raw-wire peer.</summary>
[Collection(CompatibilityHostCollection.Name)]
public sealed class Sdk6ClientTests
{
    private enum ClientMethod
    {
        CreateAsync,
        ConditionalCreateAsync,
        ReadStringAsync,
        ReadUriAsync,
        UpdateAsync,
        ConditionalUpdateAsync,
        DeleteStringAsync,
        DeleteUriAsync,
        DeleteResourceAsync,
        PatchUriAsync,
        PatchTypedAsync,
        ConditionalPatchAsync,
        TypeHistoryAsync,
        CreateMinimalAsync,
        CreateOutcomeAsync,
    }

    private static readonly FhirReleases.FhirSequenceCodes[] _versions =
        [FhirReleases.FhirSequenceCodes.R4, FhirReleases.FhirSequenceCodes.R4B, FhirReleases.FhirSequenceCodes.R5];
    private static readonly ClientMethod[] _methods =
    [
        ClientMethod.CreateAsync, ClientMethod.ConditionalCreateAsync,
        ClientMethod.ReadStringAsync, ClientMethod.ReadUriAsync,
        ClientMethod.UpdateAsync, ClientMethod.ConditionalUpdateAsync,
        ClientMethod.DeleteStringAsync, ClientMethod.DeleteUriAsync, ClientMethod.DeleteResourceAsync,
        ClientMethod.PatchUriAsync, ClientMethod.PatchTypedAsync, ClientMethod.ConditionalPatchAsync,
        ClientMethod.TypeHistoryAsync,
    ];
    private static readonly DateTimeOffset _since = new(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset _replyTime = new(2024, 1, 2, 3, 4, 5, TimeSpan.Zero);
    private const string ReplyNarrative = """<div xmlns="http://www.w3.org/1999/xhtml"><p> Wire <b>reply</b>  stays. </p></div>""";
    private readonly ITestOutputHelper _output;

    /// <summary>Creates a client contract test with captured-request output.</summary>
    /// <param name="output">The current xUnit test output.</param>
    public Sdk6ClientTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>Both handling settings and both version-header settings run with Accept and with the format parameter.</summary>
    public static IEnumerable<object[]> ClientCases
    {
        get
        {
            foreach (object[] row in FailureCases)
            {
                foreach (string mode in new[] { "strict-version-accept", "lenient-plain-accept", "strict-version-format", "lenient-plain-format" })
                {
                    yield return [$"{row[0]} / {mode}", row[1], row[2], mode];
                }
            }
        }
    }

    /// <summary>Errors and in-flight cancellation cover every wrapper method/overload in both wire formats.</summary>
    public static IEnumerable<object[]> FailureCases
    {
        get
        {
            foreach (FhirReleases.FhirSequenceCodes version in _versions)
            {
                foreach (CompatibilityFormat format in new[] { CompatibilityFormat.Json, CompatibilityFormat.Xml })
                {
                    yield return [$"sdk-client / {version} / {format}", version, format];
                }
            }
        }
    }

    /// <summary>Checks the finite method/version/format inventory independently of successful HTTP statuses.</summary>
    [Fact]
    public void ClientIntegrationInventoryIsNonempty()
    {
        Assert.Equal(24, ClientCases.Count());
        Assert.Equal(6, FailureCases.Count());
        Assert.Equal(13, _methods.Length);
        Assert.Equal(13, _methods.Distinct().Count());
        Assert.All(ClientCases, row => Assert.False(string.IsNullOrEmpty(Assert.IsType<string>(row[0]))));
        Assert.Equal(24, ClientCases.Select(row => row[0]).Distinct().Count());
        foreach (FhirReleases.FhirSequenceCodes version in _versions)
        {
            Assert.Equal(8, ClientCases.Count(row => (FhirReleases.FhirSequenceCodes)row[1] == version));
        }
        _output.WriteLine("Client inventory: " + string.Join(", ", _methods) + "; plus ordinary Create return=minimal and return=OperationOutcome; all R4/R4B/R5, JSON/XML.");
    }

    /// <summary>Asserts complete outgoing bodies, request targets/preconditions/preferences, and independently authored replies.</summary>
    [Theory]
    [MemberData(nameof(ClientCases))]
    public async System.Threading.Tasks.Task CandleClientPreservesRequestAndResponseContract(
        string label, FhirReleases.FhirSequenceCodes version, CompatibilityFormat format, string mode)
    {
        _output.WriteLine(label);
        await using LocalHttpFixture peer = await LocalHttpFixture.StartAsync();
        Uri endpoint = new(peer.Address, "fhir/client-" + LocalPackageFixture.VersionName(version) + "/");
        CandleClientSettings settings = Settings(version, format, mode);
        Func<ClientMethod, CancellationToken, Task<Resource?>> invoke = Invoker(version, endpoint, settings);
        foreach (ClientMethod method in _methods)
        {
            await AssertSuccessfulCall(peer, endpoint, version, format, settings, method, invoke);
        }

        CandleClientSettings minimal = settings with { ReturnPreference = CandleClientSettings.ReturnPreferenceCodes.Minimal };
        await AssertSuccessfulCall(peer, endpoint, version, format, minimal, ClientMethod.CreateMinimalAsync, Invoker(version, endpoint, minimal));
        CandleClientSettings outcome = settings with { ReturnPreference = CandleClientSettings.ReturnPreferenceCodes.OperationOutcome };
        await AssertSuccessfulCall(peer, endpoint, version, format, outcome, ClientMethod.CreateOutcomeAsync, Invoker(version, endpoint, outcome));
        Assert.Equal(15, peer.Requests.Count);
        peer.AssertComplete();
    }

    /// <summary>Remote outcomes retain their issue content; cancellation reaches each actual socket request and the wrapper remains usable.</summary>
    [Theory]
    [MemberData(nameof(FailureCases))]
    public async System.Threading.Tasks.Task CandleClientPropagatesFailureAndCancellation(
        string label, FhirReleases.FhirSequenceCodes version, CompatibilityFormat format)
    {
        _output.WriteLine(label + " / every method: remote failure then signaled in-flight cancellation");
        await using LocalHttpFixture peer = await LocalHttpFixture.StartAsync();
        Uri endpoint = new(peer.Address, "fhir/client-" + LocalPackageFixture.VersionName(version) + "/");
        CandleClientSettings settings = Settings(version, format, "strict-version-accept");
        Func<ClientMethod, CancellationToken, Task<Resource?>> invoke = Invoker(version, endpoint, settings);

        foreach (ClientMethod method in _methods)
        {
            HttpStatusCode status = method switch
            {
                ClientMethod.ReadStringAsync or ClientMethod.ReadUriAsync or
                ClientMethod.DeleteStringAsync or ClientMethod.DeleteUriAsync or ClientMethod.DeleteResourceAsync => HttpStatusCode.NotFound,
                ClientMethod.ConditionalCreateAsync => HttpStatusCode.PreconditionFailed,
                _ => HttpStatusCode.Conflict,
            };
            LocalHttpFixture.ExpectedRequest expected = peer.Expect(Verb(method), PathFor(endpoint, method),
                LocalHttpFixture.RawResponse.Text(status, WireContractAssertions.MediaType(format), OutcomeWire(format, error: true)));
            using CancellationTokenSource deadline = new(LocalHttpFixture.Deadline);
            FhirOperationException exception = await Assert.ThrowsAsync<FhirOperationException>(
                () => invoke(method, deadline.Token).WaitAsync(LocalHttpFixture.Deadline));
            Assert.Equal(status, exception.Status);
            AssertOutcome(Assert.IsType<OperationOutcome>(exception.Outcome), error: true);
            AssertRequest(await expected.WaitForRequestAsync(), endpoint, version, format, settings, method);
            Assert.False(await expected.WaitForCompletionAsync());
        }

        foreach (ClientMethod method in _methods)
        {
            TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
            LocalHttpFixture.ExpectedRequest expected = peer.Expect(Verb(method), PathFor(endpoint, method),
                async (_, cancellationToken) =>
                {
                    await release.Task.WaitAsync(cancellationToken);
                    return Reply(endpoint, version, format, method);
                }, allowClientCancellation: true);
            using CancellationTokenSource cancellation = new();
            Task<Resource?> pending = invoke(method, cancellation.Token);
            try
            {
                AssertRequest(await expected.WaitForRequestAsync(), endpoint, version, format, settings, method);
                Assert.False(pending.IsCompleted);
                cancellation.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending.WaitAsync(LocalHttpFixture.Deadline));
                Assert.True(await expected.WaitForCompletionAsync());
                Assert.False(release.Task.IsCompleted);
                _output.WriteLine($"Canceled {version}/{format}/{method} after the listener captured its actual request.");
            }
            finally
            {
                release.TrySetResult();
                cancellation.Cancel();
            }
        }

        await AssertSuccessfulCall(peer, endpoint, version, format, settings, ClientMethod.ReadStringAsync, invoke);
        Assert.Equal(27, peer.Requests.Count);
        peer.AssertComplete();
    }

    private async System.Threading.Tasks.Task AssertSuccessfulCall(
        LocalHttpFixture peer, Uri endpoint, FhirReleases.FhirSequenceCodes version, CompatibilityFormat format,
        CandleClientSettings settings, ClientMethod method, Func<ClientMethod, CancellationToken, Task<Resource?>> invoke)
    {
        LocalHttpFixture.ExpectedRequest expected = peer.Expect(Verb(method), PathFor(endpoint, method), Reply(endpoint, version, format, method));
        using CancellationTokenSource deadline = new(LocalHttpFixture.Deadline);
        Resource? result = await invoke(method, deadline.Token).WaitAsync(LocalHttpFixture.Deadline);
        AssertRequest(await expected.WaitForRequestAsync(), endpoint, version, format, settings, method);
        Assert.False(await expected.WaitForCompletionAsync());
        if (method is ClientMethod.DeleteStringAsync or ClientMethod.DeleteUriAsync or ClientMethod.DeleteResourceAsync or ClientMethod.CreateMinimalAsync)
        {
            Assert.Null(result);
        }
        else if (method == ClientMethod.TypeHistoryAsync)
        {
            AssertHistory(Assert.IsType<Bundle>(result), endpoint, version);
        }
        else if (method == ClientMethod.CreateOutcomeAsync)
        {
            AssertOutcome(Assert.IsType<OperationOutcome>(result), error: false);
        }
        else
        {
            AssertReplyPatient(result, version);
        }
    }

    private void AssertRequest(
        LocalHttpFixture.CapturedRequest request, Uri endpoint, FhirReleases.FhirSequenceCodes version,
        CompatibilityFormat format, CandleClientSettings settings, ClientMethod method)
    {
        Assert.Equal(Verb(method), request.Method);
        Assert.Equal(PathFor(endpoint, method), request.Path);
        Assert.Equal(request.Path + request.Query, request.RawTarget);
        Assert.Equal(endpoint.Authority, request.Header("Host"));
        Dictionary<string, string> query = Query(request.Query);
        if (settings.UseFormatParameter)
        {
            Assert.Equal(format == CompatibilityFormat.Json ? "json" : "xml", query["_format"]);
            query.Remove("_format");
            Assert.Empty(request.Header("Accept"));
        }
        else
        {
            MediaTypeWithQualityHeaderValue accept = MediaTypeWithQualityHeaderValue.Parse(request.Header("Accept"));
            Assert.Equal(WireContractAssertions.MediaType(format), accept.MediaType);
            NameValueHeaderValue? fhirVersion = accept.Parameters.SingleOrDefault(parameter => parameter.Name == "fhirVersion");
            Assert.Equal(settings.UseFhirVersionInAcceptHeader ? AcceptVersion(version) : null, fhirVersion?.Value);
            Assert.Equal(settings.UseFhirVersionInAcceptHeader ? 1 : 0, accept.Parameters.Count);
            Assert.Equal("utf-8", request.Header("Accept-Charset"));
        }

        if (method is ClientMethod.ConditionalUpdateAsync or ClientMethod.ConditionalPatchAsync)
        {
            AssertCondition(query, version);
        }
        else if (method == ClientMethod.TypeHistoryAsync)
        {
            Assert.Equal(3, query.Count);
            Assert.Equal("2", query["_count"]);
            Assert.Equal("false", query["_summary"]);
            Assert.Equal(_since, DateTimeOffset.Parse(query["_since"], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));
        }
        else
        {
            Assert.Empty(query);
        }

        if (method == ClientMethod.ConditionalCreateAsync)
        {
            AssertCondition(Query(request.Header("If-None-Exist")), version);
        }
        else
        {
            Assert.Empty(request.Header("If-None-Exist"));
        }
        Assert.Equal(method is ClientMethod.UpdateAsync or ClientMethod.ConditionalUpdateAsync or ClientMethod.PatchTypedAsync
            ? "W/\"7\"" : string.Empty, request.Header("If-Match"));
        Assert.Equal(method is ClientMethod.ReadStringAsync or ClientMethod.ReadUriAsync
            ? "W/\"7\"" : string.Empty, request.Header("If-None-Match"));
        Assert.Equal(method == ClientMethod.ReadUriAsync
            ? "Mon, 01 Jan 2024 00:00:00 GMT" : string.Empty, request.Header("If-Modified-Since"));

        // SDK 6 applies handling only to Search (not exposed by this wrapper), and return only to ordinary writes.
        string preference = method switch
        {
            ClientMethod.CreateAsync or ClientMethod.UpdateAsync or ClientMethod.PatchUriAsync or ClientMethod.PatchTypedAsync => "return=representation",
            ClientMethod.CreateMinimalAsync => "return=minimal",
            ClientMethod.CreateOutcomeAsync => "return=OperationOutcome",
            _ => string.Empty,
        };
        Assert.Equal(preference, request.Header("Prefer"));
        Assert.DoesNotContain("handling=", request.Header("Prefer"), StringComparison.Ordinal);

        if (method is ClientMethod.CreateAsync or ClientMethod.ConditionalCreateAsync or ClientMethod.CreateMinimalAsync or ClientMethod.CreateOutcomeAsync or
            ClientMethod.UpdateAsync or ClientMethod.ConditionalUpdateAsync)
        {
            AssertBodyContentType(request, format);
            WireContractAssertions.Content(CallerWire(version, format, method), request.Text, format);
        }
        else if (method is ClientMethod.PatchUriAsync or ClientMethod.PatchTypedAsync or ClientMethod.ConditionalPatchAsync)
        {
            AssertBodyContentType(request, format);
            WireContractAssertions.Content(PatchWire(format), request.Text, format);
        }
        else
        {
            Assert.Empty(request.Body);
        }
        _output.WriteLine($"Captured {version}/{format}/{method}: {request.Method} {request.RawTarget}; Accept={request.Header("Accept")}; Prefer={request.Header("Prefer")}; body bytes={request.Body.Length}");
    }

    private static void AssertBodyContentType(LocalHttpFixture.CapturedRequest request, CompatibilityFormat format)
    {
        MediaTypeHeaderValue type = MediaTypeHeaderValue.Parse(request.Header("Content-Type"));
        Assert.Equal(WireContractAssertions.MediaType(format), type.MediaType);
        Assert.Equal("utf-8", type.CharSet);
    }

    private static Dictionary<string, string> Query(string query)
    {
        Dictionary<string, string> values = new(StringComparer.Ordinal);
        foreach (string item in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] parts = item.Split('=', 2);
            Assert.Equal(2, parts.Length);
            Assert.True(values.TryAdd(Uri.UnescapeDataString(parts[0].Replace("+", " ", StringComparison.Ordinal)),
                Uri.UnescapeDataString(parts[1].Replace("+", " ", StringComparison.Ordinal))), "Unexpected duplicate query key.");
        }
        return values;
    }

    private static void AssertCondition(Dictionary<string, string> query, FhirReleases.FhirSequenceCodes version)
    {
        Assert.Equal(2, query.Count);
        Assert.Equal("https://example.test/identifier|wire-" + LocalPackageFixture.VersionName(version), query["identifier"]);
        Assert.Equal("true", query["active"]);
    }

    private static CandleClientSettings Settings(FhirReleases.FhirSequenceCodes version, CompatibilityFormat format, string mode) => new()
    {
        VerifyFhirVersion = false,
        ExplicitFhirVersion = LocalPackageFixture.FhirVersion(version),
        PreferredFormat = format == CompatibilityFormat.Json ? CandleClientSettings.ResourceFormatCodes.Json : CandleClientSettings.ResourceFormatCodes.Xml,
        UseFormatParameter = mode.EndsWith("format", StringComparison.Ordinal),
        UseFhirVersionInAcceptHeader = mode.StartsWith("strict-", StringComparison.Ordinal),
        Timeout = checked((int)LocalHttpFixture.Deadline.TotalMilliseconds),
        ReturnPreference = CandleClientSettings.ReturnPreferenceCodes.Representation,
        PreferredParameterHandling = mode.StartsWith("strict-", StringComparison.Ordinal)
            ? CandleClientSettings.SearchParameterHandling.Strict : CandleClientSettings.SearchParameterHandling.Lenient,
        UseAsync = false,
        PreferCompressedResponses = false,
        RequestBodyCompressionMethod = DecompressionMethods.None,
    };

    private static string AcceptVersion(FhirReleases.FhirSequenceCodes version) => version switch
    {
        FhirReleases.FhirSequenceCodes.R4 => "4.0",
        FhirReleases.FhirSequenceCodes.R4B => "4.3",
        FhirReleases.FhirSequenceCodes.R5 => "5.0",
        _ => throw new ArgumentOutOfRangeException(nameof(version)),
    };

    private static string Verb(ClientMethod method) => method switch
    {
        ClientMethod.CreateAsync or ClientMethod.ConditionalCreateAsync or ClientMethod.CreateMinimalAsync or ClientMethod.CreateOutcomeAsync => "POST",
        ClientMethod.UpdateAsync or ClientMethod.ConditionalUpdateAsync => "PUT",
        ClientMethod.DeleteStringAsync or ClientMethod.DeleteUriAsync or ClientMethod.DeleteResourceAsync => "DELETE",
        ClientMethod.PatchUriAsync or ClientMethod.PatchTypedAsync or ClientMethod.ConditionalPatchAsync => "PATCH",
        _ => "GET",
    };

    private static string PathFor(Uri endpoint, ClientMethod method) => endpoint.AbsolutePath + (method switch
    {
        ClientMethod.CreateAsync or ClientMethod.ConditionalCreateAsync or ClientMethod.CreateMinimalAsync or ClientMethod.CreateOutcomeAsync or
        ClientMethod.ConditionalUpdateAsync or ClientMethod.ConditionalPatchAsync => "Patient",
        ClientMethod.TypeHistoryAsync => "Patient/_history",
        _ => "Patient/sdk6-client",
    });

    private static Func<ClientMethod, CancellationToken, Task<Resource?>> Invoker(
        FhirReleases.FhirSequenceCodes version, Uri endpoint, CandleClientSettings settings)
    {
        switch (version)
        {
            case FhirReleases.FhirSequenceCodes.R4:
                R4Client r4 = new() { FhirServerUrl = endpoint.AbsoluteUri, Settings = settings };
                return (method, cancellationToken) => InvokeR4(r4, endpoint, method, cancellationToken);
            case FhirReleases.FhirSequenceCodes.R4B:
                R4BClient r4b = new() { FhirServerUrl = endpoint.AbsoluteUri, Settings = settings };
                return (method, cancellationToken) => InvokeR4B(r4b, endpoint, method, cancellationToken);
            case FhirReleases.FhirSequenceCodes.R5:
                R5Client r5 = new() { FhirServerUrl = endpoint.AbsoluteUri, Settings = settings };
                return (method, cancellationToken) => InvokeR5(r5, endpoint, method, cancellationToken);
            default:
                throw new ArgumentOutOfRangeException(nameof(version));
        }
    }

    private static async Task<Resource?> InvokeR4(R4Client client, Uri endpoint, ClientMethod method, CancellationToken cancellationToken)
    {
        R4Patient patient = CallerR4();
        SetCallerIdentity(patient, method);
        SearchParams condition = Condition(FhirReleases.FhirSequenceCodes.R4);
        Parameters patch = PatchParameters();
        switch (method)
        {
            case ClientMethod.CreateAsync:
            case ClientMethod.CreateMinimalAsync:
                return await client.CreateAsync(patient, cancellationToken);
            case ClientMethod.CreateOutcomeAsync:
                return await client.CreateAsync<Resource>(patient, cancellationToken);
            case ClientMethod.ConditionalCreateAsync:
                return await client.ConditionalCreateAsync(patient, condition, cancellationToken);
            case ClientMethod.ReadStringAsync:
                return await client.ReadAsync<R4Patient>("Patient/sdk6-client", ifNoneMatch: "W/\"7\"", ct: cancellationToken);
            case ClientMethod.ReadUriAsync:
                return await client.ReadAsync<R4Patient>(new Uri(endpoint, "Patient/sdk6-client"), "W/\"7\"", _since, cancellationToken);
            case ClientMethod.UpdateAsync:
                return await client.UpdateAsync(patient, versionAware: true, ct: cancellationToken);
            case ClientMethod.ConditionalUpdateAsync:
                return await client.ConditionalUpdateAsync(patient, condition, versionAware: true, ct: cancellationToken);
            case ClientMethod.DeleteStringAsync:
                await client.DeleteAsync("Patient/sdk6-client", cancellationToken);
                return null;
            case ClientMethod.DeleteUriAsync:
                await client.DeleteAsync(new Uri(endpoint, "Patient/sdk6-client"), cancellationToken);
                return null;
            case ClientMethod.DeleteResourceAsync:
                await client.DeleteAsync(patient, cancellationToken);
                return null;
            case ClientMethod.PatchUriAsync:
                return await client.PatchAsync(new Uri(endpoint, "Patient/sdk6-client"), patch, cancellationToken);
            case ClientMethod.PatchTypedAsync:
                return await client.PatchAsync<R4Patient>("sdk6-client", patch, "7", cancellationToken);
            case ClientMethod.ConditionalPatchAsync:
                return await client.ConditionalPatchAsync<R4Patient>(condition, patch, cancellationToken);
            case ClientMethod.TypeHistoryAsync:
                return await client.TypeHistoryAsync("Patient", _since, 2, SummaryType.False, cancellationToken);
            default:
                throw new ArgumentOutOfRangeException(nameof(method));
        }
    }

    private static async Task<Resource?> InvokeR4B(R4BClient client, Uri endpoint, ClientMethod method, CancellationToken cancellationToken)
    {
        R4BPatient patient = CallerR4B();
        SetCallerIdentity(patient, method);
        SearchParams condition = Condition(FhirReleases.FhirSequenceCodes.R4B);
        Parameters patch = PatchParameters();
        switch (method)
        {
            case ClientMethod.CreateAsync:
            case ClientMethod.CreateMinimalAsync:
                return await client.CreateAsync(patient, cancellationToken);
            case ClientMethod.CreateOutcomeAsync:
                return await client.CreateAsync<Resource>(patient, cancellationToken);
            case ClientMethod.ConditionalCreateAsync:
                return await client.ConditionalCreateAsync(patient, condition, cancellationToken);
            case ClientMethod.ReadStringAsync:
                return await client.ReadAsync<R4BPatient>("Patient/sdk6-client", ifNoneMatch: "W/\"7\"", ct: cancellationToken);
            case ClientMethod.ReadUriAsync:
                return await client.ReadAsync<R4BPatient>(new Uri(endpoint, "Patient/sdk6-client"), "W/\"7\"", _since, cancellationToken);
            case ClientMethod.UpdateAsync:
                return await client.UpdateAsync(patient, versionAware: true, ct: cancellationToken);
            case ClientMethod.ConditionalUpdateAsync:
                return await client.ConditionalUpdateAsync(patient, condition, versionAware: true, ct: cancellationToken);
            case ClientMethod.DeleteStringAsync:
                await client.DeleteAsync("Patient/sdk6-client", cancellationToken);
                return null;
            case ClientMethod.DeleteUriAsync:
                await client.DeleteAsync(new Uri(endpoint, "Patient/sdk6-client"), cancellationToken);
                return null;
            case ClientMethod.DeleteResourceAsync:
                await client.DeleteAsync(patient, cancellationToken);
                return null;
            case ClientMethod.PatchUriAsync:
                return await client.PatchAsync(new Uri(endpoint, "Patient/sdk6-client"), patch, cancellationToken);
            case ClientMethod.PatchTypedAsync:
                return await client.PatchAsync<R4BPatient>("sdk6-client", patch, "7", cancellationToken);
            case ClientMethod.ConditionalPatchAsync:
                return await client.ConditionalPatchAsync<R4BPatient>(condition, patch, cancellationToken);
            case ClientMethod.TypeHistoryAsync:
                return await client.TypeHistoryAsync("Patient", _since, 2, SummaryType.False, cancellationToken);
            default:
                throw new ArgumentOutOfRangeException(nameof(method));
        }
    }

    private static async Task<Resource?> InvokeR5(R5Client client, Uri endpoint, ClientMethod method, CancellationToken cancellationToken)
    {
        R5Patient patient = CallerR5();
        SetCallerIdentity(patient, method);
        SearchParams condition = Condition(FhirReleases.FhirSequenceCodes.R5);
        Parameters patch = PatchParameters();
        switch (method)
        {
            case ClientMethod.CreateAsync:
            case ClientMethod.CreateMinimalAsync:
                return await client.CreateAsync(patient, cancellationToken);
            case ClientMethod.CreateOutcomeAsync:
                return await client.CreateAsync<Resource>(patient, cancellationToken);
            case ClientMethod.ConditionalCreateAsync:
                return await client.ConditionalCreateAsync(patient, condition, cancellationToken);
            case ClientMethod.ReadStringAsync:
                return await client.ReadAsync<R5Patient>("Patient/sdk6-client", ifNoneMatch: "W/\"7\"", ct: cancellationToken);
            case ClientMethod.ReadUriAsync:
                return await client.ReadAsync<R5Patient>(new Uri(endpoint, "Patient/sdk6-client"), "W/\"7\"", _since, cancellationToken);
            case ClientMethod.UpdateAsync:
                return await client.UpdateAsync(patient, versionAware: true, ct: cancellationToken);
            case ClientMethod.ConditionalUpdateAsync:
                return await client.ConditionalUpdateAsync(patient, condition, versionAware: true, ct: cancellationToken);
            case ClientMethod.DeleteStringAsync:
                await client.DeleteAsync("Patient/sdk6-client", cancellationToken);
                return null;
            case ClientMethod.DeleteUriAsync:
                await client.DeleteAsync(new Uri(endpoint, "Patient/sdk6-client"), cancellationToken);
                return null;
            case ClientMethod.DeleteResourceAsync:
                await client.DeleteAsync(patient, cancellationToken);
                return null;
            case ClientMethod.PatchUriAsync:
                return await client.PatchAsync(new Uri(endpoint, "Patient/sdk6-client"), patch, cancellationToken);
            case ClientMethod.PatchTypedAsync:
                return await client.PatchAsync<R5Patient>("sdk6-client", patch, "7", cancellationToken);
            case ClientMethod.ConditionalPatchAsync:
                return await client.ConditionalPatchAsync<R5Patient>(condition, patch, cancellationToken);
            case ClientMethod.TypeHistoryAsync:
                return await client.TypeHistoryAsync("Patient", _since, 2, SummaryType.False, cancellationToken);
            default:
                throw new ArgumentOutOfRangeException(nameof(method));
        }
    }

    private static SearchParams Condition(FhirReleases.FhirSequenceCodes version)
    {
        SearchParams condition = new();
        condition.Add("identifier", "https://example.test/identifier|wire-" + LocalPackageFixture.VersionName(version));
        condition.Add("active", "true");
        return condition;
    }

    private static void SetCallerIdentity(Resource resource, ClientMethod method)
    {
        if (method is ClientMethod.UpdateAsync or ClientMethod.ConditionalUpdateAsync or ClientMethod.DeleteResourceAsync)
        {
            resource.Id = "sdk6-client";
        }
        if (method is ClientMethod.UpdateAsync or ClientMethod.ConditionalUpdateAsync)
        {
            Assert.IsType<Meta>(resource.Meta).VersionId = "7";
        }
    }

    private static R4Patient CallerR4() => new()
    {
        Meta = CallerMeta("r4"), Language = "en", Text = CallerNarrative(),
        Identifier = CallerIdentifiers("r4"), Active = true,
        Name = [new HumanName { Use = HumanName.NameUse.Official, Family = "Wire", Given = ["Avery", "Quinn"] }],
        Gender = coreR4::Hl7.Fhir.Model.AdministrativeGender.Other, BirthDateElement = CallerBirthDate(), MultipleBirth = new Integer(2),
        Photo = [new Attachment { ContentType = "image/png", Data = [1, 2, 3, 4], Title = "Synthetic non-summary photo" }],
        GeneralPractitioner = [new ResourceReference("Practitioner/wire-clinician") { Display = "Synthetic clinician" }],
        ManagingOrganization = new ResourceReference("Organization/wire-organization"),
    };

    private static R4BPatient CallerR4B() => new()
    {
        Meta = CallerMeta("r4b"), Language = "en", Text = CallerNarrative(),
        Identifier = CallerIdentifiers("r4b"), Active = true,
        Name = [new HumanName { Use = HumanName.NameUse.Official, Family = "Wire", Given = ["Avery", "Quinn"] }],
        Gender = coreR4B::Hl7.Fhir.Model.AdministrativeGender.Other, BirthDateElement = CallerBirthDate(), MultipleBirth = new Integer(2),
        Photo = [new Attachment { ContentType = "image/png", Data = [1, 2, 3, 4], Title = "Synthetic non-summary photo" }],
        GeneralPractitioner = [new ResourceReference("Practitioner/wire-clinician") { Display = "Synthetic clinician" }],
        ManagingOrganization = new ResourceReference("Organization/wire-organization"),
    };

    private static R5Patient CallerR5() => new()
    {
        Meta = CallerMeta("r5"), Language = "en", Text = CallerNarrative(),
        Identifier = CallerIdentifiers("r5"), Active = true,
        Name = [new HumanName { Use = HumanName.NameUse.Official, Family = "Wire", Given = ["Avery", "Quinn"] }],
        Gender = coreR5::Hl7.Fhir.Model.AdministrativeGender.Other, BirthDateElement = CallerBirthDate(), MultipleBirth = new Integer(2),
        Photo = [new Attachment { ContentType = "image/png", Data = [1, 2, 3, 4], Title = "Synthetic non-summary photo" }],
        GeneralPractitioner = [new ResourceReference("Practitioner/wire-clinician") { Display = "Synthetic clinician" }],
        ManagingOrganization = new ResourceReference("Organization/wire-organization"),
    };

    private static Meta CallerMeta(string version) => new()
    {
        Tag = [new Coding { System = "https://example.test/corpus", Code = "synthetic-" + version }],
    };

    private static Narrative CallerNarrative() => new()
    {
        Status = Narrative.NarrativeStatus.Generated,
        Div = "<div xmlns=\"http://www.w3.org/1999/xhtml\"><p>  Synthetic <b>Avery</b>  &amp; <i>Quinn</i>\n next line.  </p><p> </p></div>",
    };

    private static List<Identifier> CallerIdentifiers(string version) =>
    [
        new Identifier { System = "https://example.test/identifier", Value = "wire-" + version },
        new Identifier { System = "https://example.test/secondary", Value = "second-" + version },
    ];

    private static Date CallerBirthDate() => new("1980-02")
    {
        Extension = [new Extension { Url = "https://example.test/fhir/StructureDefinition/date-quality", Value = new FhirString("month known") }],
    };

    private static Parameters PatchParameters() => new()
    {
        Parameter =
        [
            new Parameters.ParameterComponent
            {
                Name = "operation",
                Part =
                [
                    new Parameters.ParameterComponent { Name = "type", Value = new Code("replace") },
                    new Parameters.ParameterComponent { Name = "path", Value = new FhirString("Patient.active") },
                    new Parameters.ParameterComponent { Name = "value", Value = new FhirBoolean(false) },
                ],
            },
        ],
    };

    private static string CallerWire(FhirReleases.FhirSequenceCodes version, CompatibilityFormat format, ClientMethod method)
    {
        string expected = CompatibilityReplay.LoadFixture(version, format == CompatibilityFormat.Json ? "patient.json" : "patient.xml");
        if (method is not (ClientMethod.UpdateAsync or ClientMethod.ConditionalUpdateAsync))
        {
            return expected;
        }
        expected = LocalPackageFixture.WithId(expected, "sdk6-client");
        if (format == CompatibilityFormat.Json)
        {
            JsonObject json = JsonNode.Parse(expected)!.AsObject();
            json["meta"]!["versionId"] = "7";
            return json.ToJsonString();
        }
        XElement xml = XElement.Parse(expected, LoadOptions.PreserveWhitespace);
        XNamespace fhir = "http://hl7.org/fhir";
        xml.Element(fhir + "meta")!.AddFirst(new XElement(fhir + "versionId", new XAttribute("value", "7")));
        return xml.ToString(SaveOptions.DisableFormatting);
    }

    private static string PatchWire(CompatibilityFormat format) => format == CompatibilityFormat.Json
        ? """{"resourceType":"Parameters","parameter":[{"name":"operation","part":[{"name":"type","valueCode":"replace"},{"name":"path","valueString":"Patient.active"},{"name":"value","valueBoolean":false}]}]}"""
        : """<Parameters xmlns="http://hl7.org/fhir"><parameter><name value="operation"/><part><name value="type"/><valueCode value="replace"/></part><part><name value="path"/><valueString value="Patient.active"/></part><part><name value="value"/><valueBoolean value="false"/></part></parameter></Parameters>""";

    private static LocalHttpFixture.RawResponse Reply(Uri endpoint, FhirReleases.FhirSequenceCodes version, CompatibilityFormat format, ClientMethod method)
    {
        if (method is ClientMethod.DeleteStringAsync or ClientMethod.DeleteUriAsync or ClientMethod.DeleteResourceAsync)
        {
            return LocalHttpFixture.RawResponse.Empty();
        }
        HttpStatusCode status = method is ClientMethod.CreateAsync or ClientMethod.ConditionalCreateAsync or ClientMethod.CreateMinimalAsync or ClientMethod.CreateOutcomeAsync
            ? HttpStatusCode.Created : HttpStatusCode.OK;
        LocalHttpFixture.RawResponse response = method switch
        {
            ClientMethod.CreateMinimalAsync => LocalHttpFixture.RawResponse.Empty(status),
            ClientMethod.CreateOutcomeAsync => LocalHttpFixture.RawResponse.Text(status, WireContractAssertions.MediaType(format), OutcomeWire(format, false)),
            ClientMethod.TypeHistoryAsync => LocalHttpFixture.RawResponse.Text(status, WireContractAssertions.MediaType(format), HistoryWire(endpoint, version, format)),
            _ => LocalHttpFixture.RawResponse.Text(status, WireContractAssertions.MediaType(format), ReplyPatientWire(version, format)),
        };
        return response with
        {
            Headers = new Dictionary<string, string>
            {
                ["Location"] = new Uri(endpoint, "Patient/sdk6-client/_history/8").AbsoluteUri,
                ["ETag"] = "W/\"8\"",
                ["Last-Modified"] = "Tue, 02 Jan 2024 03:04:05 GMT",
            },
        };
    }

    private static string ReplyPatientWire(FhirReleases.FhirSequenceCodes version, CompatibilityFormat format)
    {
        string versionName = LocalPackageFixture.VersionName(version);
        return format == CompatibilityFormat.Json
            ? $$"""
                {"resourceType":"Patient","id":"sdk6-client","meta":{"versionId":"8","lastUpdated":"2024-01-02T03:04:05Z","tag":[{"system":"https://example.test/client","code":"reply-{{versionName}}"}]},
                 "language":"fr","text":{"status":"generated","div":"<div xmlns=\"http://www.w3.org/1999/xhtml\"><p> Wire <b>reply</b>  stays. </p></div>"},
                 "identifier":[{"system":"urn:sdk6:reply","value":"first"},{"system":"urn:sdk6:reply","value":"second"}],"active":false,
                 "name":[{"family":"Response","given":["Independent","{{versionName}}"]}],"birthDate":"1999-12",
                 "_birthDate":{"extension":[{"url":"https://example.test/reply-quality","valueString":"reply month"}]},
                 "multipleBirthInteger":3,"photo":[{"contentType":"image/png","data":"BQYHCA==","title":"Reply bytes"}],
                 "generalPractitioner":[{"reference":"Practitioner/reply-clinician","display":"Reply clinician"}]}
                """
            : $"""
                <Patient xmlns="http://hl7.org/fhir"><id value="sdk6-client"/><meta><versionId value="8"/><lastUpdated value="2024-01-02T03:04:05Z"/><tag><system value="https://example.test/client"/><code value="reply-{versionName}"/></tag></meta>
                <language value="fr"/><text><status value="generated"/><div xmlns="http://www.w3.org/1999/xhtml"><p> Wire <b>reply</b>  stays. </p></div></text>
                <identifier><system value="urn:sdk6:reply"/><value value="first"/></identifier><identifier><system value="urn:sdk6:reply"/><value value="second"/></identifier><active value="false"/>
                <name><family value="Response"/><given value="Independent"/><given value="{versionName}"/></name>
                <birthDate value="1999-12"><extension url="https://example.test/reply-quality"><valueString value="reply month"/></extension></birthDate>
                <multipleBirthInteger value="3"/><photo><contentType value="image/png"/><data value="BQYHCA=="/><title value="Reply bytes"/></photo>
                <generalPractitioner><reference value="Practitioner/reply-clinician"/><display value="Reply clinician"/></generalPractitioner></Patient>
                """;
    }

    private static string OutcomeWire(CompatibilityFormat format, bool error)
    {
        string id = error ? "sdk6-client-error" : "sdk6-client-outcome";
        string severity = error ? "error" : "information";
        string code = error ? "processing" : "informational";
        return format == CompatibilityFormat.Json
            ? $$"""{"resourceType":"OperationOutcome","id":"{{id}}","issue":[{"severity":"{{severity}}","code":"{{code}}","details":{"text":"Independent wire details"},"diagnostics":"Independent wire diagnostic","expression":["Patient.active"]}]}"""
            : $"""<OperationOutcome xmlns="http://hl7.org/fhir"><id value="{id}"/><issue><severity value="{severity}"/><code value="{code}"/><details><text value="Independent wire details"/></details><diagnostics value="Independent wire diagnostic"/><expression value="Patient.active"/></issue></OperationOutcome>""";
    }

    private static string HistoryWire(Uri endpoint, FhirReleases.FhirSequenceCodes version, CompatibilityFormat format)
    {
        string patient = ReplyPatientWire(version, format);
        string patientUrl = new Uri(endpoint, "Patient/sdk6-client").AbsoluteUri;
        string historyUrl = new Uri(endpoint, "Patient/_history").AbsoluteUri;
        return format == CompatibilityFormat.Json
            ? $$$"""
                {"resourceType":"Bundle","id":"sdk6-client-history","type":"history","total":2,"link":[{"relation":"self","url":"{{{historyUrl}}}"}],
                 "entry":[{"fullUrl":"{{{patientUrl}}}","resource":{{{patient}}},"request":{"method":"PUT","url":"Patient/sdk6-client"},"response":{"status":"200","etag":"W/\"8\""}},
                          {"request":{"method":"DELETE","url":"Patient/removed"},"response":{"status":"204"}}]}
                """
            : $"""
                <Bundle xmlns="http://hl7.org/fhir"><id value="sdk6-client-history"/><type value="history"/><total value="2"/><link><relation value="self"/><url value="{historyUrl}"/></link>
                <entry><fullUrl value="{patientUrl}"/><resource>{patient}</resource><request><method value="PUT"/><url value="Patient/sdk6-client"/></request><response><status value="200"/><etag value="W/&quot;8&quot;"/></response></entry>
                <entry><request><method value="DELETE"/><url value="Patient/removed"/></request><response><status value="204"/></response></entry></Bundle>
                """;
    }

    private static void AssertReplyPatient(Resource? resource, FhirReleases.FhirSequenceCodes version)
    {
        switch (version)
        {
            case FhirReleases.FhirSequenceCodes.R4:
                R4Patient r4 = Assert.IsType<R4Patient>(resource);
                AssertReplyFields(r4, r4.Identifier, r4.Active, r4.Name, r4.BirthDateElement, r4.MultipleBirth, r4.Photo, r4.GeneralPractitioner, version);
                Assert.Null(r4.Gender);
                Assert.Null(r4.ManagingOrganization);
                break;
            case FhirReleases.FhirSequenceCodes.R4B:
                R4BPatient r4b = Assert.IsType<R4BPatient>(resource);
                AssertReplyFields(r4b, r4b.Identifier, r4b.Active, r4b.Name, r4b.BirthDateElement, r4b.MultipleBirth, r4b.Photo, r4b.GeneralPractitioner, version);
                Assert.Null(r4b.Gender);
                Assert.Null(r4b.ManagingOrganization);
                break;
            case FhirReleases.FhirSequenceCodes.R5:
                R5Patient r5 = Assert.IsType<R5Patient>(resource);
                AssertReplyFields(r5, r5.Identifier, r5.Active, r5.Name, r5.BirthDateElement, r5.MultipleBirth, r5.Photo, r5.GeneralPractitioner, version);
                Assert.Null(r5.Gender);
                Assert.Null(r5.ManagingOrganization);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(version));
        }
    }

    private static void AssertReplyFields(
        DomainResource patient, List<Identifier> identifiers, bool? active, List<HumanName> names, Date? birthDate,
        DataType? multipleBirth, List<Attachment> photos, List<ResourceReference> practitioners, FhirReleases.FhirSequenceCodes version)
    {
        Assert.Equal("Patient", patient.TypeName);
        Assert.Equal("sdk6-client", patient.Id);
        Meta metadata = Assert.IsType<Meta>(patient.Meta);
        Assert.Equal("8", metadata.VersionId);
        Assert.Equal(_replyTime, metadata.LastUpdated);
        Coding tag = Assert.Single(metadata.Tag);
        Assert.Equal("https://example.test/client", tag.System);
        Assert.Equal("reply-" + LocalPackageFixture.VersionName(version), tag.Code);
        Assert.Equal("fr", patient.Language);
        Narrative narrative = Assert.IsType<Narrative>(patient.Text);
        Assert.Equal(Narrative.NarrativeStatus.Generated, narrative.Status);
        WireContractAssertions.Content(ReplyNarrative, Assert.IsType<string>(narrative.Div), CompatibilityFormat.Xml);
        Assert.Equal(new[] { "first", "second" }, identifiers.Select(identifier => identifier.Value));
        Assert.All(identifiers, identifier => Assert.Equal("urn:sdk6:reply", identifier.System));
        Assert.False(active);
        HumanName name = Assert.Single(names);
        Assert.Equal("Response", name.Family);
        Assert.Equal(new[] { "Independent", LocalPackageFixture.VersionName(version) }, name.Given);
        Date date = Assert.IsType<Date>(birthDate);
        Assert.Equal("1999-12", date.Value);
        Extension extension = Assert.Single(date.Extension);
        Assert.Equal("https://example.test/reply-quality", extension.Url);
        Assert.Equal("reply month", Assert.IsType<FhirString>(extension.Value).Value);
        Assert.Equal(3, Assert.IsType<Integer>(multipleBirth).Value);
        Attachment photo = Assert.Single(photos);
        Assert.Equal("image/png", photo.ContentType);
        Assert.Equal(new byte[] { 5, 6, 7, 8 }, photo.Data);
        Assert.Equal("Reply bytes", photo.Title);
        ResourceReference practitioner = Assert.Single(practitioners);
        Assert.Equal("Practitioner/reply-clinician", practitioner.Reference);
        Assert.Equal("Reply clinician", practitioner.Display);
    }

    private static void AssertOutcome(OperationOutcome outcome, bool error)
    {
        Assert.Equal(error ? "sdk6-client-error" : "sdk6-client-outcome", outcome.Id);
        OperationOutcome.IssueComponent issue = Assert.Single(outcome.Issue);
        Assert.Equal(error ? OperationOutcome.IssueSeverity.Error : OperationOutcome.IssueSeverity.Information, issue.Severity);
        Assert.Equal(error ? OperationOutcome.IssueType.Processing : OperationOutcome.IssueType.Informational, issue.Code);
        Assert.Equal("Independent wire details", Assert.IsType<CodeableConcept>(issue.Details).Text);
        Assert.Equal("Independent wire diagnostic", issue.Diagnostics);
        Assert.Equal(new[] { "Patient.active" }, issue.Expression);
    }

    private static void AssertHistory(Bundle history, Uri endpoint, FhirReleases.FhirSequenceCodes version)
    {
        Assert.Equal("sdk6-client-history", history.Id);
        Assert.Equal(Bundle.BundleType.History, history.Type);
        Assert.Equal(2, history.Total);
        Bundle.LinkComponent link = Assert.Single(history.Link);
        Assert.Equal("self", link.Relation);
        Assert.Equal(new Uri(endpoint, "Patient/_history").AbsoluteUri, link.Url);
        Assert.Equal(2, history.Entry.Count);
        Bundle.EntryComponent present = history.Entry[0];
        Assert.Equal(new Uri(endpoint, "Patient/sdk6-client").AbsoluteUri, present.FullUrl);
        AssertReplyPatient(present.Resource, version);
        Bundle.RequestComponent presentRequest = Assert.IsType<Bundle.RequestComponent>(present.Request);
        Bundle.ResponseComponent presentResponse = Assert.IsType<Bundle.ResponseComponent>(present.Response);
        Assert.Equal(Bundle.HTTPVerb.PUT, presentRequest.Method);
        Assert.Equal("Patient/sdk6-client", presentRequest.Url);
        Assert.Equal("200", presentResponse.Status);
        Assert.Equal("W/\"8\"", presentResponse.Etag);
        Bundle.EntryComponent deleted = history.Entry[1];
        Assert.Null(deleted.Resource);
        Bundle.RequestComponent deletedRequest = Assert.IsType<Bundle.RequestComponent>(deleted.Request);
        Bundle.ResponseComponent deletedResponse = Assert.IsType<Bundle.ResponseComponent>(deleted.Response);
        Assert.Equal(Bundle.HTTPVerb.DELETE, deletedRequest.Method);
        Assert.Equal("Patient/removed", deletedRequest.Url);
        Assert.Equal("204", deletedResponse.Status);
    }
}
