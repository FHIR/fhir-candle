# OpenTelemetry OTLP exporter

**2026-10-16, Windows x64 — phase 14 local gates passed; recommend 1.20.0 for parent acceptance.**
Final Windows full-suite and Linux/macOS validation remain pending. No commit was made.

## Unit and source identity

Verified clean predecessor: `e261e0d8e28d76643e79347a41b2f292adff8d50`.
Authority: [AGENTS.md](../../../AGENTS.md), phase14/unit contract/V-accept; [API/SDK](opentelemetry-api-sdk.md), [Console](opentelemetry-console.md) and inventory dossiers read-only.
Earlier prerequisite repairs remain in the predecessor; no new product correction was needed or authorized.

- Only direct move: **OpenTelemetry.Exporter.OpenTelemetryProtocol 1.16.0 -> 1.20.0**.
- API/SDK/Console and transitive ProviderBuilderExtensions remain **1.20.0**; Hosting/instrumentation **1.16.0**.
  Earlier SDK/Binder upgrades satisfy prerequisites; arbitrary rollback of those upgrades is not certified.
- Tests: four rows, controlled receiver rejection, reused wire assertions/decoder, OTLP location reporting; Program/endpoints/retry policy unchanged.

Raw SHA-256 (CRLF preserved); identical final test source before/after pin. Patches: native `git diff --binary --` on named paths.

| Identity | SHA-256 |
|---|---|
| Host csproj | `f5704e180e68199b340bcc130e21b792594de93b7bddb940247ca54579656e48` |
| Tests, 758 lines | `7836d3d8eff1e9f915a22bcd4c2aacd61ea40bde3bc95553f11f50f306389d27` |
| Unchanged Program | `6f73ee17e7c6b2a5b685e7854beaf509a343efe794a20beddc301b739f8dc7dd` |
| Final test-only patch, 11,785 bytes | `abd49ad3de5404af6e8bd62ded9f751beed891c367854262c040aab4d7c4bb84` |
| Executable patch (Host + Tests), 12,670 bytes | `31c8e710058789949eb2c7c21fc46f60c37e8f2003e877e7905f92ea16dc12e8` |

## Fresh eligibility and supplier review

All seven telemetry NuGet indexes still identify **1.20.0** as highest stable:
[OTLP index](https://api.nuget.org/v3-flatcontainer/opentelemetry.exporter.opentelemetryprotocol/index.json),
[registration](https://api.nuget.org/v3/registration5-gz-semver2/opentelemetry.exporter.opentelemetryprotocol/1.20.0.json)
and [catalog](https://api.nuget.org/v3/catalog0/data/2026.10.15.14.00.40/opentelemetry.exporter.opentelemetryprotocol.1.20.0.json)
mark it listed/non-prerelease, published **2026-10-15T13:54:12Z**, without deprecation/vulnerability entries (not a security audit).
Fresh nuspecs: `https://api.nuget.org/v3-flatcontainer/{lowercase-id}/{version}/{lowercase-id}.nuspec`.
OTLP 1.16/1.20 provide native net10/net9/net8 groups; SDK minimum changes 1.16 -> **1.20**.
Net8 retains Binder **>=8.0.2**, already satisfied by **10.0.13**. SDK -> ProviderBuilderExtensions -> API minima are 1.20.
Console requires SDK >=1.20; retained Hosting requires >=1.16 plus Hosting.Abstractions minima 10/9/8.0.0;
instrumentation admits ProviderBuilderExtensions **[1.16.0, 2.0.0)**. No second direct pin required; unit frozen before trial.

Tagged [VERSIONING](https://raw.githubusercontent.com/open-telemetry/opentelemetry-dotnet/core-1.20.0/VERSIONING.md)
supports API `a` with SDK `[a,next-major)` and stable public API compatibility within major 1; release numbering is not equality.
Reviewed [release notes](https://raw.githubusercontent.com/open-telemetry/opentelemetry-dotnet/core-1.20.0/RELEASENOTES.md),
[OTLP changelog](https://raw.githubusercontent.com/open-telemetry/opentelemetry-dotnet/core-1.20.0/src/OpenTelemetry.Exporter.OpenTelemetryProtocol/CHANGELOG.md),
and both tags' HTTP/gRPC clients, options, transmission and experimental retry sources:

- 1.17 resource schema/log processor fixes; 1.18 serialization cleanup, retry/response handling, logger version/request limits;
  1.19 dictionary/PEM/cache fixes; 1.20 scope schema, HTTP logging and attribute limits need no adaptation for this small scalar path.
- Defaults cap requests at **64 MiB rather than 128 MiB**, responses at 4 MiB; byte-array/event/link/scope limits changed.
  No compensating option added. Large batches, custom limits/dictionaries, TLS/mTLS, compression, trim/AOT and WASM not certified.
- Retry is opt-in via `OTEL_DOTNET_EXPERIMENTAL_OTLP_RETRY`; both tags default to a failure-returning handler. No retry enabled.
- Supplier [#7916](https://github.com/open-telemetry/opentelemetry-dotnet/pull/7916)
  fixes cold first HTTP log export during disposal by falling back when resolving IHttpClientFactory throws ObjectDisposedException.
  Source-reviewed, **not reproduced here**: both lifecycle fixtures warm exporters. No passing cold baseline/candidate claim.

Source-body SHA-256: OTLP index `863e96c6e49e3094856f2737b02804f0958ab06c8f0c7d91ac898198fbde915f`;
1.16/1.20 nuspecs `d21c937b3cf7dbff4f88b27e96d52d38fe74bccddee5327c3cf39ac8fe46816c` /
`7e21a8bdad9adb8963359ee228ddb05fff0bcb8af2528ebe6366e2038f9b147a`;
changelog `faf83bfc2cb4ecbddd921cc7ddc29d82fc63d8a96a8ef309d758335b6cd4d7b2`.
Tagged supplier tree: `d5b2f8a818f7a2b4f3a4c3108d1a27b11395b10e`.

## Full graph and selected assets

Captured original **full package-only nodes**, compared every field after restore. Normalization: git-relative slash
`csproj|target` keys, recursively sorted compact JSON, `ensure_ascii=False`, UTF-8, no final LF.
Both graphs: **12 projects / 36 targets / 946 nodes / 387,010 bytes**.
Before (reproduced by baseline B): `4294a3a97959694976f312aa7c739446688bd7fb6bcb33eef9d8a6701d618b0c`;
after: `9f8dbbeaeea43da091dc4c4af24b42c2c86c2f7f0d9361947e360ba4bd43dafb`.
Exactly **six** graphs change: Host/Tests x net10/net9/net8; only OTLP identity 1.16 -> 1.20 and its SDK minimum.
Other **30 graphs and every unrelated node/field are identical**. No added/removed package ID or native/RID binary.

OTLP compile/runtime assets remain `lib/<matching-TFM>/OpenTelemetry.Exporter.OpenTelemetryProtocol.dll`
with `.dll.sigstore.json;.xml` metadata. Binder 10.0.13 uses matching-TFM DLLs on Tests all targets and Host net9/net8;
Host net10 retains framework-pruned `_._`. Net8 Binder edge remains in Tests, pruned in Host, unchanged.
Other telemetry uses matching-TFM managed DLLs except instrumentation net9 -> net8. DiagnosticSource is framework-provided
on net10, package 10.0.13 matching-TFM on net9/net8. Logging/DI/environment closures unchanged.
Differing assets justify all-target execution. All **21** deployed telemetry DLLs matched package assets on baseline/candidate;
candidate Binder's three also matched. Executed roots: `C:\ai\git\fhir-candle\src\fhir-candle.Tests\bin\Release\<TFM>\`.
OTLP cache source is nuget.org; contentHash matches assets. Nuget.org/VS offline feed enabled; no download/signature-audit claim.
XML comparison: **46 references / 36 IDs**, parent conditions included, only OTLP changed. Sorted compact
`[path,parent-attributes,reference-attributes]` JSON: 5,160 bytes, before `d86687c3b5368767237a975e24544858c519367f40f585b6f4248fd77e67362e`,
after `ef73e584e6ccde2d6032f841222262a23d6b1faa6df5543e586200a50387370d`.

## Windows gates, actual protocols and attempts

Windows 10.0.26200/win-x64; SDK 10.0.400, MSBuild 18.9.6; test runtimes 10.0.11/9.0.19/8.0.30; xUnit adapter 3.1.5.
From repository root, synchronous 180-second initial waits, each sequence ran:

```powershell
dotnet build --configuration Release --framework net10.0
dotnet test --configuration Release --no-restore --verbosity normal --filter "FullyQualifiedName~fhir.candle.Tests.OpenTelemetryCompatibilityTests"
```

| Sequence | OTLP / fixture | B exit; warnings/errors | TA exit; each net10/net9/net8 passed/failed/skipped; warnings |
|---|---|---|---|
| 1 | 1.16 / initial new rows | 0; 9/0 | **1; 30/2/0; 27** |
| 2 | 1.16 / corrected final rows | 0; 8/0 | **0; 32/0/0; 24** |
| 3 | 1.20 / identical final rows | 0; 8/0 | **0; 32/0/0; 24** |

Initial test-authoring error: recovery rows assumed default retries/successful metric ForceFlush after rejection.
Both tags require opt-in retry; metric ForceFlush correctly returned false. Corrected only new cases to assert
default failure/recovery; retained old assertions and production policy. Fixed new xUnit2031 using Assert.Single's predicate.
No other failed attempt or candidate failure. Final warnings: only existing Pkcs 10.0.0/8.0.1 NU1902/NU1903
(GHSA-2fmv-rv2c-59x7 / GHSA-846f-f764-76mx), not suppressed/repaired. TA's net10 supplies T-telemetry;
no cosmetic single-TFM replay or unrelated/full suite.

Final baseline/candidate: **96 occurrences, 32 unique names, each three times**, exact multiset equality.
New methods `OtlpExporterRecoversAfterTransientReceiverFailure` and `OtlpExporterShutsDownAfterReceiverRejection`
each have `protocol: "grpc"` / `protocol: "http/protobuf"`. Other 28 names/parameters match the Console dossier.
Sorted emitted names/parameters, duplicates retained, status/duration removed, LF-joined/no final LF SHA-256:
`3c54f3fb612f44524f8e67fd7dd6160350f4cd3c5b0e5d45084bd6b61d5107fd`;
old-28 subset remains `b9602f655a7cb5320aee25e767c49360f173fdd79e8b4172dbcc2c88e004699f`.
Initial 96 occurrences instead hash `79c75d4035927b6e336f86a54f87c4448db7533f8308567c4ddbcb21c100480f`
(recovery method renamed). Interleaved output cannot attribute individual names to TFMs; all three identified summaries pass 32.
No numeric discovery total emitted; named execution establishes intended nonzero discovery.

- **Justified reuse:** `ConfiguredTelemetryPreservesServiceNameAndSignals` HTTP rows exactly cover planned
  `OtlpHttpExporterUsesConfiguredEndpointAndFlushes`: real private Program configuration, general/explicit/override composition,
  every signal, decoded delivery, service resource and ForceFlush. Existing lifecycle rows prove unflushed disposal delivery.
  No redundant renamed test; Console is not substituted for wire evidence.
- Both real paths: HTTP/1.1 protobuf `/v1/{traces,metrics,logs}`; HTTP/2 gRPC collector Export paths, frames/content-type/protobuf/trailers.
  W3C parent/tracestate/baggage, console output and disabled traffic retained.
- Recovery rows reject the first subject batch per signal with HTTP **503** or
  gRPC **14**; decode rejected resources/records separately from accepted requests. Metric flush fails; batch trace/log flush
  means drain, not delivery. Later requests deliver all signals; dropped spans/logs are not replayed.
  Cumulative metrics can include earlier measurements. Recovery, **not retry**.
- Shutdown: warmed signals, final unflushed records rejected HTTP **400** / gRPC **3**; no false delivery, disposed providers, no traffic/console for 300 ms.
  Both success-shutdown rows still pass. No prolonged outage/stress, collector persistence, whole FHIR startup or packaging claim.
- Loopback-only synthetic data; 10-second flush/30-second network/observation/start/stop/dispose bounds retained.
  Hosts/clients disposed; process environment/console/context restored. No surviving helper, install, new package/project,
  public facade or external telemetry endpoint.

## Native evidence handoff and cleanup

Direct redirection was restricted; actual native spools below are under **`C:\Users\ginoc\AppData\Local\Temp\`**.
No manually transcribed logs. Parent archival to session `files\phase14-*` remains.

| Evidence | Full spool filename under that directory |
|---|---|
| Original full graph | `1792190661970-copilot-tool-output-33128-942d71d3-ce6f-4d26-b4d9-d58f94187b36.txt` |
| Fresh metadata/policy/releases | `1792190663032-copilot-tool-output-33128-b5c6e479-e310-4373-8ed4-6b49f9569930.txt` |
| Both-tag transport/options sources | `1792190689369-copilot-tool-output-33128-bc8649da-146d-4da5-8adb-828166f7b4b0.txt` |
| Sequence 1 B+TA, failed authoring attempt | `1792190761783-copilot-tool-output-33128-21502eae-eaa0-4251-b952-9820f775f288.txt` |
| Sequence 2 B+TA, final baseline | `1792190842681-copilot-tool-output-33128-9a6fd439-e7a1-4296-9b27-edbe46309022.txt` |
| Sequence 3 B+TA, candidate | `1792190885751-copilot-tool-output-33128-4f0437d7-69e4-403b-a180-8db27ac161de.txt` |
| Candidate full graph/deltas/executable patch | `1792190954960-copilot-tool-output-33128-22002003-d6e0-4266-89ec-ce5219db73e6.txt` |

B+TA log SHA-256, sequences 1/2/3:
`9b72d361dc5146a3ec1f42826ff1963b42f27fac9b88aebd53cfca2c88083cf6`,
`7b00c682b177506c10335a5a9fa730cd6e78ada3aa7397c4442109e29d9d95e5`,
`e174aa6a87ad103421aad90d90afef84430d9eeb381aa9f077d874b0137a8b8c`.
`git diff --check` passed. Index/plan/prior dossiers untouched; only three owned files changed. Final Windows/six Linux/macOS cells **pending**.
