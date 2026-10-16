# OpenTelemetry API / SDK joint unit

Assessment: **2026-10-16, Windows x64**. **Local phase-11 gates passed;
recommend the joint 1.20.0 checkpoint for parent review.** This is not final
Windows-suite or Linux/macOS approval. No commit was made.

## Identity and disposition

Predecessor: `d23a2e81cb1b7184f70fd2386131e3402a9124ba`. Read-only pre-flight
confirmed that HEAD, an empty index and clean worktree. Command/style authority:
[AGENTS.md](../../../AGENTS.md); grouping history:
[inventory dossier](README.md). The final executable patch is the two Host
pin changes plus the new `OpenTelemetryCompatibilityTests.cs`; `Program.cs`
is unchanged. No other declaration, project, production behavior or public
test facade changed.

| Package | Original | Evaluated newest stable | Selected |
|---|---|---|---|
| OpenTelemetry | 1.16.0 | 1.20.0 | **1.20.0** |
| OpenTelemetry.Api | 1.16.0 | 1.20.0 | **1.20.0** |
| OpenTelemetry.Api.ProviderBuilderExtensions (transitive) | 1.16.0 | 1.20.0 | **1.20.0**, resolved, not directly pinned |
| OpenTelemetry.Exporter.Console | 1.16.0 | 1.20.0 available; not trialled here | **1.16.0** |
| OpenTelemetry.Exporter.OpenTelemetryProtocol | 1.16.0 | 1.20.0 available; not trialled here | **1.16.0** |
| OpenTelemetry.Extensions.Hosting | 1.16.0 | 1.20.0 available; not trialled here | **1.16.0** |
| OpenTelemetry.Instrumentation.AspNetCore | 1.16.0 | 1.20.0 available; not trialled here | **1.16.0** |

Final file SHA-256 identities (raw bytes, including CRLF):

- Host csproj: `d5d374fc033c28dc9fc5fdb08e9692db306321b85c5ab200be0199dba70fed35`.
- New tests: `2ea6206a0d5b6515260398655b6f9331ee3fb71821b6628dc836801f1ca8e03c`.
  This exact test source passed on **both** package pairs on all three TFMs.
- Unchanged Program: `6f73ee17e7c6b2a5b685e7854beaf509a343efe794a20beddc301b739f8dc7dd`.

IdentityModel Tokens/JsonWebTokens **8.23.0**, Hosting.Abstractions **10.0.13**,
all accepted Configuration pins and every unrelated pin are preserved. This
unit depends on its predecessor's resolved closure; arbitrary removal of
earlier accepted upgrades is not certified.

## Fresh supplier and release review

Re-read the public NuGet flat-container indexes and nuspecs on the assessment
date, including both 1.16.0/1.20.0 API, SDK and ProviderBuilderExtensions and
the four retained consumers' 1.16.0 groups. All seven indexes still end at
stable **1.20.0**. API/SDK 1.20.0 registration/catalog entries are listed,
published **2026-10-15** (SDK 13:54:13.230Z; API 13:54:13.933Z), with no
deprecation or vulnerability field in those entries. This is availability,
not a general vulnerability audit or runtime result.

- [SDK index](https://api.nuget.org/v3-flatcontainer/opentelemetry/index.json),
  [API index](https://api.nuget.org/v3-flatcontainer/opentelemetry.api/index.json).
- [SDK registration](https://api.nuget.org/v3/registration5-gz-semver2/opentelemetry/1.20.0.json),
  [API registration](https://api.nuget.org/v3/registration5-gz-semver2/opentelemetry.api/1.20.0.json).
- Nuspec source for each lowercase ID/version:
  `https://api.nuget.org/v3-flatcontainer/{id}/{version}/{id}.nuspec`.
- [Version-tagged supplier policy](https://raw.githubusercontent.com/open-telemetry/opentelemetry-dotnet/core-1.20.0/VERSIONING.md)
  supports API version `a` with SDK `[a, next-major)`. Thus API 1.20 with
  SDK 1.16 is unsupported. SDK 1.20 also requires ProviderBuilderExtensions
  >=1.20, which requires API >=1.20. **Joint movement is required**, not
  inferred from the shared prefix.
- Retained Console/OTLP/Hosting require SDK >=1.16; retained instrumentation
  requires ProviderBuilderExtensions **[1.16.0, 2.0.0)**. These are not equality
  constraints. The policy promises stable public-API backward compatibility
  within major 1; simultaneous release numbering does not require replacing
  every consumer. Actual mixed-closure execution below supplements that policy.

Reviewed [release notes](https://raw.githubusercontent.com/open-telemetry/opentelemetry-dotnet/core-1.20.0/RELEASENOTES.md)
and the version-tagged `src/OpenTelemetry`, `OpenTelemetry.Api`,
`OpenTelemetry.Api.ProviderBuilderExtensions`, `OpenTelemetry.Exporter.Console`
and `OpenTelemetry.Exporter.OpenTelemetryProtocol` **CHANGELOG.md** files:

- SDK 1.17 replaces vendored environment configuration with a package
  dependency; already-satisfied Configuration remains outside this unit.
  Resource schema URLs, metric storage/collection fixes and logger caching
  changes do not require a production adaptation.
- SDK 1.18 changes span-processor shutdown handling and fixes provider-thread
  leaks on construction failure; 1.20 fixes async suppression-context and
  metric lifetime/race issues. Real flush/disposal checks are required, not
  just successful construction. No concurrency stress or exception-in-
  constructor leak certification is claimed.
- API trace-state parsing/validation changes were reviewed. The real inbound
  valid W3C parent/tracestate/baggage case preserves context across `await`.
  Experimental LoggerProvider extensibility breaks do not apply to a custom
  subclass here; there is none.
- The new resource-environment fallback applies when host configuration lacks
  those settings. Production uses the normal environment-aware builder and
  explicitly calls `AddService("fhir-candle")`; tests isolate ambient OTEL
  settings and verify the explicit service on every received resource.
  They do not certify arbitrary external sampler/resource configurations.
- Release-wide OTLP byte-array/attribute-limit changes, 128-to-64-MiB request
  default, HTTP-client logging changes, first-log-export-during-disposal fix,
  and new console scope rendering belong to **exporter upgrades**, not this
  SDK/API move. Exporters remain 1.16.0; those changes/fixes are not claimed.
  The lifecycle test explicitly flushes once before its final disposal-only
  request; it does not certify cold first-use export during disposal.

Research retries: an initial registration read failed on gzip decoding and
was retried with decompression; a guessed logging-source URL returned 404
and was corrected using the supplier tree. Neither was used as positive
evidence or counted as a build/test failure.

## Actual graphs and selected assets

Captured the complete package-only baseline graph in memory before candidate
B and compared every full target node afterwards, not just version minima.
Fresh assets cover **12 projects / 36 targets / 946 package-node occurrences**.
Normalization: git-relative slash `csproj|target` keys to full package-only
target nodes, recursively sorted compact JSON, `ensure_ascii=False`, UTF-8,
no terminal LF.

| Graph | Bytes | SHA-256 |
|---|---:|---|
| Before; also reproduced after pre-pin B | 386,811 | `3c965533d9a1d4224024c3d61da4ee886faaef63dbd2819cfc929930d3e80089` |
| After candidate B | 387,010 | `0540b923a3db7c6ed51e3a0070a538ebc82568846e7991c5075bfa9bd4d994b3` |

Exactly **six** graphs change: Host and Tests, each net10/net9/net8.
Exactly **three** package identities per changed graph move 1.16.0 -> 1.20.0:
SDK, API, ProviderBuilderExtensions (**18 replaced node identities**).
All other package nodes/fields and the other **30 graphs** are unchanged.
No new or removed package ID, additional direct pin, or unexplained drift.

| Consumer/TFM | Actual before -> after and selected compile/runtime assets |
|---|---|
| H/T, all three | API/SDK/ProviderBuilderExtensions select `lib/<matching-TFM>/<ID>.dll`, 1.16 -> 1.20. Console/OTLP/Hosting retain their matching-TFM 1.16 DLLs. |
| H/T, net10 | Instrumentation uses `lib/net10.0`; DiagnosticSource has no package node (shared framework). |
| H/T, net9 | Instrumentation uses **lib/net8.0**, unchanged. DiagnosticSource **10.0.13**, `lib/net9.0`, unchanged. |
| H/T, net8 | Instrumentation uses `lib/net8.0`. DiagnosticSource **10.0.13**, `lib/net8.0`, unchanged. |
| SDK dependency fields | ProviderBuilderExtensions minimum changes to 1.20; its API minimum changes to 1.20. New EnvironmentVariables minimum 10/9/8.0.0 appears in the respective Tests SDK nodes; redundant Host edges are pruned. |
| Existing environment closure | H/T net9/net8 select EnvironmentVariables **10.0.13** matching-TFM DLLs. On net10, Host retains its **10.0.13** placeholder `_._`; Tests has no EnvironmentVariables package node (framework pruning). No environment package version changes. |
| Existing other closure | Hosting/DI/Diagnostics abstractions remain **10.0.13** where present. Tests Logging.Configuration remains **10.0.0 / 9.0.0 / 8.0.0** respectively; Host's corresponding framework edges are pruned. |

DiagnosticSource >=10.0.0 on net8/net9 was already in API **1.16** metadata;
it is not a new dependency requirement. Retained Hosting's framework-specific
8/9/10.0.0 minimums and instrumentation's bounded range admit the actual
closure. Actual binary checks matched **all 21 deployed telemetry DLLs** in
the three test output directories byte-for-byte to their selected package
runtime assets. This does not substitute for execution or prove packaging.

Assets retain nuget.org and the VS offline feed as enabled sources. The
three selected 1.20 package-cache metadata records identify nuget.org, and
their content hashes match the Host/Tests library records. This identifies
cache provenance, not a fresh signature audit or proof of network download.

## Executed Windows gates

Windows **10.0.26200**, win-x64; SDK **10.0.400**, MSBuild **18.9.6**.
Installed SDKs also include 9.0.317 / 8.0.424. Actual .NET/ASP.NET runtimes:
**10.0.11 / 9.0.19 / 8.0.30**. xUnit VSTest adapter **3.1.5**.
All commands ran from the repository root, synchronously with a 180-second
initial tool wait. These labels abbreviate the following exact commands:

```powershell
# B
dotnet build --configuration Release --framework net10.0
# T10
dotnet test --configuration Release --framework net10.0 --no-restore --verbosity normal --filter "FullyQualifiedName~fhir.candle.Tests.OpenTelemetryCompatibilityTests"
# TA
dotnet test --configuration Release --no-restore --verbosity normal --filter "FullyQualifiedName~fhir.candle.Tests.OpenTelemetryCompatibilityTests"
# T9
dotnet test --configuration Release --framework net9.0 --no-restore --verbosity normal --filter "FullyQualifiedName~fhir.candle.Tests.OpenTelemetryCompatibilityTests"
# T8
dotnet test --configuration Release --framework net8.0 --no-restore --verbosity normal --filter "FullyQualifiedName~fhir.candle.Tests.OpenTelemetryCompatibilityTests"
```

Native target-specific SDK/API/exporter assets, the net9 instrumentation
fallback and differing runtime/DI closures warranted the all-target focused
gate. TA interleaves names without TFM identifiers; separate T9/T8 invocations
then provided unambiguous per-TFM occurrence inventories rather than inferring
them by division. No full suite or unrelated filter was run.

| Sequence | Pair / fixture | Command | Exit / execution | Warning summary |
|---|---|---|---|---:|
| 1 | 1.16 / initial fixture | B | 0; zero errors | 8 |
| 2 | 1.16 / initial fixture | T10 | **1; 21 passed, 3 failed, 0 skipped** | 8 |
| 3 | 1.16 / corrected final fixture | T10 | 0; 24/24, 0 skipped | 8 |
| 4 | 1.16 / final fixture | TA | 0; 24/24 on each TFM, 0 skipped | 24 |
| 5 | 1.16 / final fixture | T9; T8 (separate invocations) | both 0; each 24/24, 0 skipped | 8 each |
| 6 | Joint 1.20 / identical final fixture | B | 0; zero errors; graph comparison above | 8 |
| 7 | 1.20 | T10 | 0; 24/24, 0 skipped | 8 |
| 8 | 1.20 | TA | 0; 24/24 on each TFM, 0 skipped | 24 |
| 9 | 1.20 | T9; T8 (separate invocations) | both 0; each 24/24, 0 skipped | 8 each |

**Initial fixture failures, not erased:** `ConfiguredTelemetryPreservesServiceNameAndSignals`
failed the empty-export assertion for `(http/protobuf, traces,logs, signals)`
and `(grpc, all, overrides)`; the context case saw null rather than
`synthetic` baggage. The fixture had restored the API's **pre-SDK no-op**
propagator after the SDK's one-time static initialization. Supplier
`Sdk.cs`/`Propagators.cs` confirmed this; it now snapshots initialized defaults.
It also treated client-response completion as ASP.NET recording completion.
The corrected bounded flush-and-observe loop waits for the actual request
record in every enabled signal; all original wire/service/context expectations
remain. No production fix, assertion-value relaxation, skipped row, or pin
change was used to obtain the pre-pin pass. The exact corrected source then
passed on all TFMs before the joint move.

**Diagnostics:** no new compiler/analyzer diagnostic. Every warning remains
the baseline `System.Security.Cryptography.Pkcs` **10.0.0 / 8.0.1** NU1902
([moderate advisory](https://github.com/advisories/GHSA-2fmv-rv2c-59x7)) and
NU1903 ([high advisory](https://github.com/advisories/GHSA-846f-f764-76mx)) in
Host/Tests. Normal output repeats warnings in its summary; table counts are
the build summaries, not counts of repeated display lines. Vulnerabilities
are neither suppressed nor remediated by this unit.

### Case/parameter occurrence inventory

Every individual final-fixture baseline/candidate TFM run executed **24
unique names once each**, one class, four methods, zero failures/skips.
Numeric discovered totals are not emitted; discovery lifecycle messages plus
the actual named executions establish non-zero intended discovery.

| Method | Exact parameter rows (each once per individual TFM run) |
|---|---|
| DisabledTelemetryDoesNotCreateExportTraffic | Fact, 1 |
| TelemetryApiRetainsTraceAndBaggageContext | Fact, 1 |
| ConfiguredTelemetryPreservesServiceNameAndSignals | `(protocol, signals, endpoints)`: `("grpc","all","general")`, `("http/protobuf","all","general")`, `("http/protobuf","all","slash")`, `("http/protobuf","all","v1")`, `("http/protobuf","all","v1-slash")`, `("protobuf","all","general")`, `("GrPc","all","general")`, `("unknown","all","general")`, `("grpc","all","overrides")`, `("http/protobuf","all","overrides")`; `grpc` and `http/protobuf` each with `traces`, `metrics`, `logs`, `traces,metrics` and endpoints `signals` (8 rows); `("http/protobuf","traces,logs","signals")`, `("grpc","metrics,logs","signals")`. Total 20. |
| TelemetryProvidersFlushAndDispose | `(protocol: "grpc")`, `(protocol: "http/protobuf")`, 2 |

Per-TFM name-multiset SHA-256, identical before/after on **net10, net9, net8**:
`4fd0c1e40309c114adb79131da066fca86da474e1decc65ace2e51778ab074a0`.
Each TA capture independently contains 72 occurrences, 24 unique names, each
occurring three times; both hash to
`6738c4c9b34030b6c6bd3f8ba7466c2f7a8867139ba2be4f59c8a7a06f1fa6d6`.
Normalization retains complete emitted `fhir.candle.Tests.*` names and
parameter text, strips status/duration, ordinal-sorts with duplicates, joins
with LF and no terminal LF, UTF-8 without BOM. No lowered discovery is used
as success. Existing-suite inventory awaits the final full gate.

## Real integration coverage and limits

Tests invoke the **real private `Program.ConfigureOpenTelemetry` by reflection**
on fresh builders. They do not call the intentionally telemetry-free
`BuildAppForTesting`, duplicate registrations, mock exporters, or modify
production options. The minimal request host and receiver use built-in Kestrel
at `127.0.0.1:0`; all routes, baggage and log markers are synthetic.

- Actual ASP.NET request spans, `http.server.request.duration` histogram
  points with matching `http.route`, and structured log `Marker` attributes
  arrive in decoded OTLP resource/scope/record fields. Every received resource
  must have `service.name = fhir-candle`. Console trace/metric/log output and
  that service name are also observed, not replaced with callbacks.
- HTTP/1.1 protobuf reaches `/v1/traces`, `/v1/metrics`, `/v1/logs`.
  A distinct HTTP/2-only receiver validates uncompressed gRPC frame lengths,
  service `Export` paths, content type and actual protobuf payloads, responding
  with an empty response frame and `grpc-status: 0` trailer. HTTP evidence is
  not reused as gRPC certification.
- General endpoints with no suffix, `/`, `/v1`, `/v1/`, explicit per-signal
  overrides, protocol alias/case/fallback, every signal combination and
  disabled configuration are exercised. Wrong paths abort and fail the case.
  Disabled configuration has no telemetry providers, console output or
  receiver requests after request/stop/dispose plus a 250-ms observation.
- Enabled providers actually `ForceFlush` successfully; lifecycle rows send
  a final request **without another explicit flush**, stop/dispose the host,
  verify its stopped/disposed state and final OTLP records for all signals,
  then observe 300 ms without further console/export traffic. No claim of
  collector persistence, TLS/mTLS, compressed transport, outage/retry policy,
  cold first-export shutdown or prolonged load is made.
- Global ASP.NET instrumentation can also observe the in-process receiver.
  Assertions require the subject's exact synthetic route/marker, not any
  incidental receiver span or mere nonempty traffic. Normal exporter batching
  is retained; each provider flush is bounded at 10 seconds, observation
  polling uses a 30-second deadline, and network/start/stop/disposal waits are
  bounded. Both hosts and all clients are stopped/disposed in the test process.
- The existing nonparallel host collection protects process-global state.
  Ambient OTEL/proxy variables are temporarily removed and restored; builder
  configuration is cleared, console capture restored, and initialized
  propagator/activity-format/baggage state restored. No collector/tool install,
  new project/dependency, external telemetry endpoint, clinical data, fixture
  files or surviving helper process is required.

These tests supply the scoped local integration gate for this private
production boundary, **not** whole `RunServer`/FHIR startup, publishing or
packaged-tool verification. The final Windows full suite and all six
Linux/macOS target cells remain **pending**.

## Native-output evidence locations

Native output was retained by the command tool, not manually transcribed or
redirected to the requested session `phase11-*` filenames. The initial B is
in tool session **528** (exit 0); subsequent full captures below are under
`%LOCALAPPDATA%\Temp\`. Parent archival to its preferred session directory is
still needed. Graph before/after full changed nodes are in candidate B's
capture. These temporary files are evidence inputs, not commit deliverables.

| Gate | Native capture filename |
|---|---|
| Initial failed T10 | `1792189109445-copilot-tool-output-33128-4d017931-fdab-486b-a585-da4945a67731.txt` |
| Corrected baseline T10 | `1792189198157-copilot-tool-output-33128-61833cb1-f9e7-461a-b0a0-1d288abccec4.txt` |
| Baseline TA | `1792189264432-copilot-tool-output-33128-ebe3ff18-0e73-4200-a60e-72abed626479.txt` |
| Baseline T9 | `1792189304438-copilot-tool-output-33128-28630888-c622-4738-82be-a890b3eabf0d.txt` |
| Baseline T8 | `1792189304916-copilot-tool-output-33128-c5b8807d-2aa2-4147-a261-51a5377717b4.txt` |
| Candidate B + full-node comparison | `1792189352336-copilot-tool-output-33128-baa06540-6e24-4d53-ab97-130c30e9f75c.txt` |
| Candidate T10 | `1792189363319-copilot-tool-output-33128-d817ce84-a78c-4292-ae83-718d7246bb73.txt` |
| Candidate TA | `1792189394197-copilot-tool-output-33128-47798fa4-f6d9-4597-9fe3-56b8bf09f84a.txt` |
| Candidate T9 | `1792189444442-copilot-tool-output-33128-7fb7f89a-6add-448c-b6f8-8664eb27ccfe.txt` |
| Candidate T8 | `1792189444718-copilot-tool-output-33128-61a83254-c220-4ddf-a90f-ae1922ffa150.txt` |

Read-only final checks: `git diff --check` passed; the index remained empty.
No staging, commit, plan edit, push/PR, or unrelated source change occurred.
