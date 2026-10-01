# Sealed-artifact performance comparison

## Current boundary: full untimed semantics preflight

`FhirCandle.Performance` is a BCL-only external HTTP client. Its optional
reference to `FhirCandle.Performance.Probe` shares the bounded pipe protocol,
not application or Firely types. Neither project is referenced by production.
The probe is injected only into experiment-owned children through
`DOTNET_STARTUP_HOOKS`. Loading the probe without its activation configuration
does nothing.

The supported commands remain **`prepare`** and **`run --preflight-only`**.
Preflight now requires all **72 paired rows / 144 fresh artifact-case
processes**, at the actual 100 Patient + 1,000 Observation and 1,000 Patient +
10,000 Observation scales. Each child performs exactly **four untimed logical
case operations**, after complete corpus seeding and read-back validation.
A passing preflight is **not a measured trial**.
Full `run` and `render` explicitly return exit code **2** without creating a
performance report. Other failures return **1**. Only successful preparation
or the entire successful preflight returns **0**.

The eventual benchmark still requires twelve cases × three FHIR versions ×
two scales = **72 paired rows**, with at least three independent trials per
variant per row: **at least 432 measured child case-trials**. Executed full-scale
correctness is still a gate, not a consequence of this implementation.
128-operation warm-up/restoration, paired calibration, allocation
windows, memory sampling, retention checkpoints, statistics and rendering
remain Phase 3 work. Neither feasibility nor untimed case snapshots are
speed, allocation/op, sampled-peak or retention results.

The independently audited `phase1-006` six-artifact/version challenges passed
on the pinned artifacts and runtime, with clean child exits. That evidence
does not satisfy the expanded gate. This documentation does **not** claim a
Phase 2 live pass. Build/test/live execution and runtime output ownership
belong to the caller; only actual `preflight\completed.json` plus its complete
matrix establish the new gate.

## Preconditions and command sources

Repository guidance comes from root `AGENTS.md`. Build with the documented
.NET 10 SDK and the installed .NET 10, 9 and 8 prerequisites. The existing
all-target sequence in `.github\workflows\build-and-test.yml` and
`docs\technical\sdk6-compatibility.md` produces the required **net8.0** tooling:

```powershell
dotnet restore
dotnet build --configuration Release --no-restore
```

The scoped test command from `AGENTS.md` covers both existing tests and the
new `PerformanceEvidenceTests`:

```powershell
dotnet test src\fhir-candle.Tests\fhir-candle.Tests.csproj --configuration Release --framework net10.0 --no-restore --verbosity normal
```

The documented all-target test command is:

```powershell
dotnet test --configuration Release --no-restore --verbosity normal
```

These are verification instructions, **not a record that verification or live
feasibility has passed**. Do not run a stale net8 tool after changing source
and building only net10. In the initial implementation dispatch, the caller
owns candidate building and all build/test/live execution; the implementer
does not build in parallel with it.

The installed host must be exactly
`C:\Program Files\dotnet\dotnet.exe`, Windows x64. Both applications must
actually load **Microsoft.NETCore.App 8.0.30** and
**Microsoft.AspNetCore.App 8.0.30**. Compilation SDK, timestamp-derived
application assembly versions, and measured runtime are separate identities.
No application-control-policy bypass, machine-wide hook, runtime major
roll-forward, development launch profile, source-baseline fallback or product
change is supported.

## Separate artifact inputs from unique run output

For a fresh Phase 2 attempt (retain all earlier phase-one directories):

```text
C:\ai\git\fhir-candle\scratch\1001-01\
  experiment.json
  performance.md                         reserved; NOT written by untimed preflight
  performance-data\
    artifacts-001\                       reusable, independently verified inputs
      candidate-source\                  caller's clean detached checkout
      candidate-net8\                     caller's sealed output and assets
      candidate-build.json               retained provenance (schema below)
      candidate-build.log                actual log path may differ
      isolation.txt                      actual authorized-environment evidence
      fhir-candle.2025.611.1422.nupkg       downloaded or verified cached archive
      baseline\                          unchanged full extracted package layout
    phase2-001\                          unique OUTPUT; must not exist before prepare
      manifest.json
      prepare-started.json
      prepared.json
      fixtures\
      preflight\
```

Preparation files are expected: **`prepare` followed by preflight for the same
manifest/run ID is supported**. Preflight does not reject the directory merely
because preparation wrote files. It does reject unknown root entries, a
failed preparation, any earlier preflight, or evidence from another manifest,
tool/probe binary or protocol. It does not resume partial evidence or append
new results into an old preflight.

Use a new ID (`phase2-002`, etc.) and matching `OutputDirectory` after failures,
tool/probe/protocol edits or another preflight. Preserve failed directories.
Inputs can be reused only after hash verification. Do not place artifacts or
the probe beneath the unique output directory. Never replace a seal with
moving `bin` contents while using the same run ID.

All of these runtime inputs/outputs are ignored local data, not source paths
to stage or force-add. The exact eventual report target remains:

`C:\ai\git\fhir-candle\scratch\1001-01\performance.md`

## Seed manifest (schema 1)

JSON property names are case-sensitive; unknown properties are rejected.
The example below supplies the actual pinned paths and settings. Before use,
the caller must supply real candidate provenance and an existing isolation
evidence file, and change `Isolation.Authorized` to `true` **only after**
confirming the described controls. `false` deliberately prevents accidental
launch; it is not a request for the runner to create controls.

```json
{
  "SchemaVersion": 1,
  "ProtocolVersion": "candle-perf-phase1-v1",
  "RunId": "phase2-001",
  "OutputDirectory": "C:\\ai\\git\\fhir-candle\\scratch\\1001-01\\performance-data\\phase2-001",
  "ReportPath": "C:\\ai\\git\\fhir-candle\\scratch\\1001-01\\performance.md",
  "Baseline": {
    "PackageId": "fhir-candle",
    "PackageVersion": "2025.611.1422",
    "SourceRevision": "3f1b73e6bde167a019505ac55ca2aa18e0860e8d",
    "FirelyPackageVersion": "5.11.4",
    "PackageUrl": "https://api.nuget.org/v3-flatcontainer/fhir-candle/2025.611.1422/fhir-candle.2025.611.1422.nupkg",
    "ArchivePath": "C:\\ai\\git\\fhir-candle\\scratch\\1001-01\\performance-data\\artifacts-001\\fhir-candle.2025.611.1422.nupkg",
    "ArtifactDirectory": "C:\\ai\\git\\fhir-candle\\scratch\\1001-01\\performance-data\\artifacts-001\\baseline"
  },
  "Candidate": {
    "SourceRevision": "21fcb2e7ce7c44de9bc5f12b6af68bee06e90d9b",
    "FirelyPackageVersion": "6.2.1",
    "ArtifactDirectory": "C:\\ai\\git\\fhir-candle\\scratch\\1001-01\\performance-data\\artifacts-001\\candidate-net8",
    "EntryPointRelativePath": "fhir-candle.dll",
    "BuildProvenancePath": "C:\\ai\\git\\fhir-candle\\scratch\\1001-01\\performance-data\\artifacts-001\\candidate-build.json"
  },
  "Runtime": {
    "DotnetHostPath": "C:\\Program Files\\dotnet\\dotnet.exe",
    "RuntimeVersion": "8.0.30",
    "AspNetCoreVersion": "8.0.30",
    "Architecture": "X64",
    "ServerGc": true,
    "ConcurrentGc": true,
    "TieredCompilation": true,
    "TieredPgo": true,
    "ReadyToRun": true
  },
  "ProbePath": "C:\\ai\\git\\fhir-candle\\src\\FhirCandle.Performance.Probe\\bin\\Release\\net8.0\\FhirCandle.Performance.Probe.dll",
  "ToolSourceRevision": "01edcd71caf223c7738fa65957bd8fae61ebe0ab",
  "ToolSourceDescription": "Uncommitted phase-two tooling on the named revision; prepare seals the actual tool and probe bytes separately.",
  "Port": 58260,
  "Tenant": "perf",
  "HttpWorkers": 1,
  "Isolation": {
    "Authorized": false,
    "Description": "Replace with the actual existing local isolation, authorization, quiet-window and background-load conditions.",
    "EvidencePath": "C:\\ai\\git\\fhir-candle\\scratch\\1001-01\\performance-data\\artifacts-001\\isolation.txt"
  },
  "Bounds": {
    "StartupSeconds": 120,
    "SetupSeconds": 600,
    "BatchSeconds": 60,
    "RequestSeconds": 30,
    "SettlingMilliseconds": 1000,
    "ShutdownSeconds": 15,
    "SampleMilliseconds": 10
  }
}
```

`SchemaVersion`, protocol, pins, runtime settings, port, tenant, worker count
and bounds have the defaults shown. Paths, run ID, both artifact input
objects, tool source identity/description and isolation evidence are required.
The C# definitions beside `ExperimentRunner` are the authoritative typed
schema. A changed manifest, including changed serialized bytes, requires new
preparation under a new run ID.

The seed schema and CLI arguments have not changed. The probe protocol remains
`candle-perf-phase1-v1` because its wire messages and limits are unchanged.
The **prepared handoff schema is now 2**, with a mandatory `Workload` ledger
whose `ContractVersion` is **`candle-perf-preflight-v2`**. Old preparation or
preflight evidence cannot be upgraded/reused in place. The workload ledger
records `Algorithm`, `Seed`, `PreflightOperations`, six `Corpora`, seventy-two
`Cases`, and both exact `StatusProfiles`. Every corpus/case descriptor records
its fixture path, ordered request seals, total payload bytes and
`RequestOrderSha256`. Each request seal includes number, method, relative URL,
Prefer, body byte length and SHA-256 (the empty-byte hash for bodyless GETs).
The order hash covers the indented UTF-8 serialized seal array, without a BOM.
`FixtureFiles` independently seals all **78** full fixture files.

## Caller-owned candidate provenance

Use a separate clean detached checkout of
`21fcb2e7ce7c44de9bc5f12b6af68bee06e90d9b`, not the implementation worktree.
The application build documented in `docs\user\getting-started.md` is, from
that checkout's root:

```powershell
dotnet build src\fhir-candle\fhir-candle.csproj -c Release
```

The caller retains the actual build log, checkout identity/clean status,
build SDK, timestamps, exit code, and sealed net8 output plus required assets.
Do not modify the checkout or build/copy its output concurrently with that
independent build. `prepare` never invokes Git, MSBuild or a source build.

`CandidateBuildProvenance` expects the following fields in
`candidate-build.json`; values are evidence, not assertions to invent:

| Field | Required value / meaning |
|-|-|
| `SchemaVersion` | `1` |
| `SourceRevision` | Exact pinned 40-character candidate SHA |
| `SourceDirectory` | Absolute `...\artifacts-001\candidate-source` checkout root |
| `DetachedHead` | `true`, established by the independent builder |
| `StatusBefore`, `StatusAfter` | Actual clean status outputs: empty strings, including an untracked-file check |
| `Command` | `dotnet build src\fhir-candle\fhir-candle.csproj -c Release` (forward slash spelling is accepted too) |
| `WorkingDirectory` | Same absolute checkout root |
| `BuildSdkVersion` | Actual .NET 10 SDK version, e.g. `10.0.400`, not the measured runtime version |
| `StartedUtc`, `FinishedUtc` | Actual ISO-8601 build timestamps in chronological order |
| `ExitCode` | `0` |
| `BuildLogPath` | Absolute path of the actual retained complete build/restore log |
| `BuildLogSha256` | 64 hex characters for that log's bytes |
| `ArtifactDirectory` | Same absolute `candidate-net8` path as the seed |
| `EntryPointRelativePath` | `fhir-candle.dll` |
| `Files` | Every sealed output/asset file, no omissions or extras |

Each `Files` element is a `FileSeal`:

```text
{ "RelativePath": "relative\\file.ext", "Length": actual-byte-count, "Sha256": "actual-64-hex-digit-hash" }
```

This schematic is intentionally not fabricated provenance. The complete
inventory must include `.deps.json`, `.runtimeconfig.json`, managed/native
dependencies and required adjacent assets. The dependency metadata is retained
unchanged and expanded into package identities by `prepare`; actual assembly
versions, informational/file versions, MVIDs and hashes are read from the
sealed PE files without loading them into the runner. A retained source SHA
alone cannot authenticate arbitrary build output.

## Published release acquisition

`prepare` downloads the exact pinned `.nupkg`, or verifies the cached file. It
also retains the NuGet registration and catalog responses:

- [Registration](https://api.nuget.org/v3/registration5-gz-semver2/fhir-candle/2025.611.1422.json)
- [Published catalog/digest](https://api.nuget.org/v3/catalog0/data/2025.06.11.14.31.45/fhir-candle.2025.611.1422.json)

The catalog's SHA-512 is:

```text
8+BGKz3nKhM+zlxU90j3O/IHJlkHoSGZI7R3TvGt1i583IVI7ayOWsizncms8+KUoSYsO2sbWt35Ulah4pEfDg==
```

Both the published digest/size and a separately retained SHA-256 are checked.
The nuspec must identify `fhir-candle` `2025.611.1422` and repository commit
`3f1b73e6bde167a019505ac55ca2aa18e0860e8d`. The runner discovers the actual
net8 entry point from the extracted runtime configurations and validates the
adjacent `DotnetToolSettings.xml`; it does not assume a `tools` folder layout.
The full package, including its other framework files, remains unchanged.
Only its actual net8 closure is launched.
The published catalog lists `tools\net8.0\any\fhir-candle.dll` and adjacent
runtime/tool settings. That metadata supports discovery; it does not replace
verification of the actual downloaded archive or successful activation.

Actual phase-one preparation verified the published **123,039,530-byte**
archive, including its published SHA-512, and retained SHA-256
`8F4644DD75120ABF5EB69E529F4910994107A58E5740155B5EF7D770F196C482`.
It discovered that same net8 entry point in the unchanged extracted payload.
This authenticates the package provenance, not completion of live challenges
or measurements; neither application pin has changed.

Extraction rejects traversal, absolute/duplicate/Windows-reserved paths and
links/reparse points. Download size is bounded to 256 MiB, expanded contents
to 2 GiB and archive entries to 40,000. An existing extracted root must exactly
match archive file hashes. Partial downloads/extractions are retained as
failures, not trusted caches. No baseline source-build fallback exists.

## Prepare and bounded live preflight

These are **new tool invocations**, not alternative build recipes. From the
implementation repository root, with the seed filled and net8 tools built:

```powershell
& "C:\Program Files\dotnet\dotnet.exe" .\src\FhirCandle.Performance\bin\Release\net8.0\FhirCandle.Performance.dll prepare --manifest C:\ai\git\fhir-candle\scratch\1001-01\experiment.json
& "C:\Program Files\dotnet\dotnet.exe" .\src\FhirCandle.Performance\bin\Release\net8.0\FhirCandle.Performance.dll run --manifest C:\ai\git\fhir-candle\scratch\1001-01\experiment.json --preflight-only
```

The runner itself must not inherit `DOTNET_STARTUP_HOOKS` or
`FHIR_CANDLE_PERFORMANCE_PROBE`. Do not set either globally. Referencing the
probe for protocol DTOs does not call `StartupHook.Initialize`.

### Launch and isolation

Each child has a fresh working directory, empty source directory and
child-local cache/profile/temp environment. The environment is **cleared**,
then populated only with recorded Windows runtime necessities, owned path
settings, explicit GC/JIT/tiering settings, Production environment and
fixed logging levels. Credentials, arbitrary Candle settings, proxies,
exporters and inherited profiler/hook variables are not copied. HTTP is
sequential, direct loopback, without cookies, proxying or redirects.

The unchanged application's ordinary host/UI remains enabled. Common
arguments select only `--r4 perf`, `--r4b perf` or `--r5 perf`, plus the common
port/public URL, `--max-resources 0`, owned empty `--fhir-source`, requested
owned `--fhir-package-cache`, no official registry use/examples/browser/MCP,
unprotected source, and enabled deterministic PUT seeding. No SMART tenants,
RI, subscriptions, sender credentials or telemetry exporters are configured.

`LoadedSupplements` records source registration, not a count of loaded files.
Read-only grounding is `VersionedFhirStore.Init` in
`src/FhirStore.CommonVersioned/Storage/VersionedFhirStore.cs` at release
`3f1b73e6bde167a019505ac55ca2aa18e0860e8d` and candidate
`21fcb2e7ce7c44de9bc5f12b6af68bee06e90d9b`: its non-null load-directory
condition calls `_loadedSupplements.Add(config.LoadDirectory.FullName)`
**before** `config.LoadDirectory.GetFiles("*.*", SearchOption.AllDirectories)`.
The required owned empty `--fhir-source` therefore registers exactly once
even when there are no files.

At every stopped snapshot the runner requires the configured `LoadDirectory`
and sole `LoadedSupplements` entry to match the independently supplied
`<child>\empty-source` full path exactly, and checks that this directory
exists with **no filesystem entries**, including subdirectories. The state
oracle checks both initial and current registrations against that expected
path and requires stable initial/current package/source marker arrays; it
does not adopt arbitrary observed markers as the expectation. Missing,
duplicate, wrong or extra source markers, changed history and actual source
files all fail. Package directives/IDs, configured/cached packages,
subscriptions/topics and initial clinical populations must still be empty.
This expected source-registration bookkeeping is neither a package nor
evidence of loaded data; no marker is deleted or generally exempted.
Pure state fixtures use a deterministic configured path without opening it;
filesystem negatives exercise the runner's actual source helper in an owned
temporary directory. Neither kind of fixture establishes live feasibility.

**`--url` is not listener isolation.** Both inspected applications call
`UseUrls("http://*:<port>")`. Preflight requires an existing authorized
environment preventing unrelated inbound/outbound work. The nonempty
isolation evidence file is hashed, never treated as permission to change
security policy. Public `IServerAddressesFeature` addresses and PID-attributed
Windows TCP tables are retained. Unexpected listeners, outbound TCP or
non-loopback peers fail the gate. These stopped observations are not a
continuous network monitor or a replacement for external isolation.

The historical cache-option parser differs. The public, **effective**
`CandleConfig.FhirCacheDirectory` must equal the owned
`<child>\profile\.fhir\packages` path; an echoed CLI option does not pass.
Both pinned package-service libraries derive this default from the Windows
`UserProfile` environment variable. The runner therefore sets child-only
`USERPROFILE` and requests that same default path explicitly. This matches
the candidate's recognized option and the historical fallback without
changing product configuration code. Read-only grounding is
`Firely.Fhir.Packages` 4.9.0 commit
`c3ff806927ad66bbd10b3f9c07f950be57037da2` and 5.0.2 commit
`25d3e80090046453acdb392396055bc92be3fb0f`, `Utils\Platform.cs`.
These package-service versions are distinct from the Firely SDK cohorts.
Effective tenant load directory, flags, packages, clinical counts,
subscriptions and logging are also checked.
Windows known-folder APIs may ignore environment profile overrides:
`UserProfile` and `LocalApplicationData` are recorded as actually observed.
The common host's ordinary data-protection key-store location is
`LocalApplicationData\fhir-candle-key-store`; key contents are never copied
or logged, and the tool does not change the machine's known-folder settings.
Disclose this host cost and authorize the normal host behavior in the local
isolation record. An escaped **FHIR package cache** still blocks preflight.

The direct-DLL launch shape comes from `docs\user\getting-started.md`.
The runner uses the explicit host with `exec --runtimeconfig <child-copy>
--depsfile <sealed-deps> --roll-forward Disable <sealed-entry-point>`.
The child-local derived runtime configuration preserves unrelated application
settings and framework membership, pins both existing framework references to
`8.0.30`, and supplies common GC/tiering flags. `rollForward: Disable` requires
the exact requested version, including its patch; no separate patch-selection
flag is needed. Legacy `applyPatches` and `rollForwardOnNoCandidateFx` options
are removed from runtime options and framework references in the **child copy
only**, because they cannot be combined with `rollForward`. It does **not**
rewrite the sealed application runtime configuration. The derived bytes and
invocation are retained; the original artifact bytes remain sealed and
rechecked. Actual loaded runtime paths, versions, hashes and MVIDs must agree
with the sealed installed framework inventories.

### Probe and lifetime

The probe observes `HostBuilt` through BCL `DiagnosticListener` to obtain the
already-created host's public service provider. It resolves the already-loaded
`FhirStoreManager.Instance` and `IInstance`, requiring the same object. It
does not create another manager, load an alternate store, use private fields,
or reference application/SDK assemblies.

Inspection dispatches through public implemented `IReadOnlyDictionary`
interfaces, including explicit implementations and their inherited `Count`.
For resource counts and requested identities (`ContainsKey` and `Item`), it
deliberately selects the exact `IReadOnlyDictionary<string, object>` contract
inherited by `IResourceStore` in both pinned sources. Versioned resource stores
also explicitly implement `IReadOnlyDictionary<string, Hl7.Fhir.Model.Resource>`;
that typed view is not selected. Counts and identity lookups use the same
object-valued view, and a missing object view fails closed. Every other
dictionary role still requires exactly one matching string-keyed contract:
there is no first-interface fallback or global preference for object values.
It reads actual resource dictionaries, public `Config`, loaded package
collections, and every `ReceivedNotifications` list entry and parsed event.
Missing/ambiguous contracts fail; an empty list under a correct key is not a
receipt. Public fields are supported only for historical public configuration
members, not for private state scraping.

The runner creates one unpredictable, current-user-only named pipe before
launch. The handshake checks actual Windows pipe peer PIDs, process start
times (PID-reuse defense), current-user SID, a 256-bit child-only nonce,
protocol version and probe hash in both directions. The nonce is redacted
from evidence. Requests/hellos are limited to 4 KiB, snapshot replies to 2 MiB,
and a child to 64 commands. Idle setup is bounded by a finite session deadline;
partial frames and exchanges have the individual request deadline. The
default total child session is startup + setup + two shutdown intervals
(750 seconds), and no configured session can exceed 20 minutes.

Readiness uses fully consumed FHIR metadata responses and checks the declared
FHIR version. Startup, setup and requests have the manifest deadlines; HTTP
bodies are capped at 4 MiB and each log stream at two million characters.
Pipe establishment races the exact owned child's exit: an early exit fails
with its PID, exit code and retained `stderr.log` path rather than waiting for
the full startup deadline.
Cancellation/failure preserves evidence and enters owned-child cleanup.
Graceful shutdown uses the existing public `IHostApplicationLifetime`;
after its bound, cleanup kills **only the owned process/PID tree** and awaits
exit. Forced cleanup is recorded and does not turn a failed shutdown or
challenge into a successful gate. There is no name-based process killing.

### Preserved phase-one HTTP/receipt challenge

Inputs use BCL UTF-8 JSON, fixed timestamps and fixed-width IDs, algorithm
`candle-perf-v1`, and reserved order seed `100101`. They contain synthetic
`example.org` systems/URLs only. Each disposable child:

1. Proves exactly one empty clinical tenant and retains all built-in
   conformance/resource-type counts.
2. PUTs a deterministic Patient, then POSTs an ID-less Observation referencing
   it. Binds the generated Observation ID to the body, Location and store.
3. Reads both resources, comparing the entire expected representation.
4. POSTs a one-event **full-content** Bundle to `/fhir/perf/$subscription-hook`
   with `Prefer: return=OperationOutcome`, and reads the stored Bundle.
5. Proves one Bundle and one registered receipt/event, correct
   subscription/topic/status/type/count/timestamp/focus/context, no errors,
   and **no separately stored embedded Patient or Observation**.

Notifications have status first, followed by Observation focus and Patient
context with matching fullUrls/references. R4 uses Parameters in a backport
history Bundle with the `backport-subscription-status-r4` and
`backport-subscription-notification-r4` profiles. R4B uses SubscriptionStatus
in a history Bundle with `backport-subscription-notification` (no `-r4b`
suffix). History entries retain required request/response components. R5 uses
a `subscription-notification` Bundle. R4 string-valued counts and R4B/R5
string-encoded counts are preserved. No live FHIR package-registry access is
needed. Profile grounding:
[R4](https://hl7.org/fhir/uv/subscriptions-backport/StructureDefinition-backport-subscription-notification-r4.json),
[R4B](https://hl7.org/fhir/uv/subscriptions-backport/StructureDefinition-backport-subscription-notification.json).

Before corpus freeze, notification FHIR `instant` inputs use the canonical
UTC spelling **`2025-01-02T03:04:05+00:00`** at exactly these positions:

- All versions: `Bundle.timestamp`.
- R4: `Bundle.entry[0].resource.parameter[5].part[1].valueInstant`, the
  `notification-event` / `timestamp` part.
- R4B/R5: `Bundle.entry[0].resource.notificationEvent[0].timestamp`.

The fixed instant and seconds precision are unchanged. The standalone
Observation and embedded Observation focus keep
`effectiveDateTime: "2025-01-02T03:04:05Z"`; other synthetic resource fields
are unchanged. The independent notification contract requires the new
spelling, not a normalization of responses. Complete returned content still
compares strictly, and receipts still require the parsed fixed timestamp.
Negative controls cover Bundle and event timestamp drift, offset/precision/
spelling changes, missing or shifted parsed receipt timestamps, and clinical
`dateTime` invariance for all three FHIR versions.

The independent status/receipt contract is grounded in
`Sdk6ContractTests.Subscriptions.AssertSyntheticInboundNotification` and
`SyntheticSubscriptionStatus`; the status-only fixture is **not** reused
unchanged. New full clinical-content and relationship assertions reject
modified focus, context, numbers, representations, identity headers, empty
receipt lists, duplicates and embedded-resource double storage.

Response expectations are specific, not “any 2xx”:

| Interaction | Baseline | Candidate | Source justification |
|-|-:|-:|-|
| First PUT Patient or Observation (seeding) | 200 | 201 | Each pinned `ResourceStore.InstanceUpdate` success branch |
| ID-less POST Patient or Observation | 201 | 201 | `VersionedFhirStore.DoInstanceCreate` |
| PUT an existing, changed Patient or Observation | 200 | 200 | `ResourceStore.InstanceUpdate`, numeric version increment |
| Subsequent instance read | 200 | 200 | Instance read response contract |
| Type search | 200 | 200 | `VersionedFhirStore.DoTypeSearch`, successful searchset |
| DELETE a created instance, returning its representation | 200 | 200 | `VersionedFhirStore.DoInstanceDelete` |
| Incoming hook | 200 | 200 | `OpSubscriptionHook`, materialized success OperationOutcome |

Both pinned `ResourceStore.cs` / `VersionedFhirStore.cs` sources ground these
profiles. The six retained live challenges confirm seed/create/read/hook
behavior; update/search/delete still require the full live preflight.
The delete contract returns the complete removed resource but supplies no
ETag, Location or Last-Modified; `FhirController.AddFhirResponse` only adds
those headers when the response context supplies them. Its absence is
checked explicitly, not treated as an exemption for create/update/read.
All actual codes and bodies are retained or, for successful seed interactions,
validated before summarization.

First stored version is `"1"`; only the explicitly bound ID and validated server
`meta.versionId`/`meta.lastUpdated` are added to expected resource content.
ETag, Location and Last-Modified must match. Arrays, all remaining metadata,
fixed timestamps and cross-resource references are not globally discarded.
No timing is performed by the oracle.

### Full deterministic workload and preflight lifecycle

`CreateCorpus`, `CreateCases` and `CreateNotification` use BCL UTF-8 JSON,
not a Firely serializer. Algorithm **`candle-perf-v1`**, seed **`100101`**, and
the four-operation preflight batch are fixed, not seed-manifest options.
Clinical inputs are logically identical across all three FHIR versions.
IDs start at zero: `patient-00000000` and `observation-00000000`, with eight
decimal digits throughout. Patient index `i` has family `Family{i % 100:D3}`
and given name `Given{i:D8}`. Observation index `j` has code
`Code{j % 1000:D3}` and subject `Patient/patient-{floor(j / 10):D8}`.
Identifiers use their logical IDs and the synthetic `example.org` systems.
Initial quantity is 37.5 synthetic units; clinical `effectiveDateTime` is
`2025-01-02T03:04:05Z`.

Request permutations are reproducible across runtime versions: initialize a
32-bit unsigned state to 100101, shuffle ascending Patient indices, then
ascending Observation indices, carrying the state between them. For each
descending Fisher-Yates position `i = count - 1 ... 1`, compute
`state = (1664525 * state + 1013904223) mod 2^32`, select
`other = state mod (i + 1)`, and swap. No runtime-dependent `Random` or
observed server identity chooses expected inputs.

Rows are ordered R4, R4B, R5; within each version, 100 then 1,000 Patients;
within each scale, the case order below. Each row runs baseline then candidate,
**never concurrently**. The row key is
`<version>-<patients>p-<observations>o-<case-id>`, for example
`R4-100p-1000o-patient-create`. Every artifact/case starts a new process and
empty clinical tenant. The Phase 1 challenge is **not** inserted into these
children: no unexplained Patient, Observation, Bundle or receipt is added.
No `$reset-store` or production change is used.

| Case ID | Four-operation input order and exact independent success evidence |
|-|-|
| `patient-create` | ID-less individual POSTs, identifiers `created-patient-00000001` through `00000004`; new distinct body/Location-bound IDs, full content/read-backs, exactly +4 Patients. |
| `observation-create` | ID-less individual POSTs with unique `created-observation-...` identifiers and the first four permuted seeded Patients as subjects; full content/read-backs, exactly +4 Observations. |
| `patient-update` | Repeat PUT to the first permuted seeded Patient, toggling active false/true/false/true. Pre-read proves existence; each return must advance versions 1 -> 2 -> 3 -> 4 -> 5 and change the field. Final read preserves version 5; no count growth. |
| `observation-update` | Repeat PUT to the first permuted seeded Observation; quantities 38.5/39.5/38.5/39.5 and effective times `...06Z`/`...07Z` alternate together. Subject stays unchanged. Every version step and final persisted content/count are checked. |
| `patient-token` | First four permuted Patient identifiers, one complete expected match per operation. |
| `patient-string` | Repeat `family=Family017`, exactly indices 17, 117, ...; 1 / 10 complete known matches at the two scales. |
| `observation-token` | Repeat system/code token `Code017`, exactly indices 17, 1017, ...; 1 / 10 complete known matches. |
| `observation-reference` | Repeat subject of the first permuted seeded Patient; exactly its ten known Observations. |
| `empty-search` | Repeat code token `OutsideDomain`; valid searchset total 0 and no entries, not an OperationOutcome. |
| `patient-read` | First four permuted seeded IDs; full resource and bound metadata/header comparison. |
| `observation-read` | First four permuted seeded IDs; full content including quantity, time and subject. |
| `subscription-receive` | Four unique full-content Bundles/status IDs, event numbers 1 through 4. Full stored Bundle reads, +4 Bundles, one dictionary key with four receipts and four individually correct events; no separately stored embedded resources. |

Search values are percent-encoded; requests include `_count=100`. The count
parameter is **not** a completeness assertion. The client follows every
`next` link sequentially within the logical operation, rejecting cycles or
escaped tenant/origin/resource URLs. Evidence retains each page/request.
The independent audit requires a successful searchset, the exact total and
identity set across all pages, no duplicates/errors/includes, full resource
content and previously bound metadata, matching fullUrls and self links, and
no unconsumed next link. URI query escaping/order can differ in a self link
only if the decoded name/value pairs are identical; clinical JSON is never
normalized this way. Current required result sets fit one page, but the
oracle also has positive multi-page and truncated-page negative controls.

Within each child, `SeedAsync` first PUTs **every Patient**, then **every
Observation**, following the frozen permutations. It checks every successful
response's exact status, entire resource, fixed ID, first version and
Location/ETag/Last-Modified. It then GETs **every seeded resource** in the
same order, comparing all content and bound metadata after all PUTs have
finished. Every reference is independently checked against `floor(j / 10)`.
The probe checks the complete resource-type inventory and exact 1,100 /
11,000 clinical population, not a 32-identity sample as a substitute for
the full HTTP content audit. Built-in conformance counts remain untouched.
The full matrix therefore performs **871,200 seed PUTs and 871,200 seed
read-backs**, in addition to readiness, case and audit requests.

`PrepareCaseAsync` validates the fixture ledger, proves update pre-existence
with another ordinary read, and captures prepared counts/identities.
`AuditCaseAsync` validates every completed case response, reads back all
creates/final updates/stored Bundles, and checks post-work counts plus every
receipt-list entry. Hook snapshots explicitly inspect absence of all eight
embedded clinical identities. Create cases additionally DELETE their four
new resources **after** recording +4 growth and read-backs, validating each
200 deleted representation and the restored corpus counts/absent IDs.
This untimed cleanup exercises the future restoration primitive; it is not
128-operation warm-up or a measured create batch.

The unchanged probe limits suffice: at most 12 requested identities for a
four-event hook, five stopped snapshots for create cases, and bounded
2 MiB snapshot replies / 64 commands. Effective settings, actual empty-source
registration/filesystem, network ownership and sealed loaded assemblies are
checked at **every** snapshot, including after the exercised case and cleanup.
The common **600-second setup deadline includes all seeding, read-backs,
case operations, audits and cleanup**. Request deadlines and all existing
startup/shutdown bounds remain active. Exceeding a bound is a retained gate
failure, never permission to reduce the corpus, omit audits or silently retry.

### Evidence and failure interpretation

`prepared.json` seals the archive, both artifacts, installed common frameworks,
tool/probe files, generated fixtures, candidate provenance/log and isolation evidence. Files are
rechecked before and after child execution. Loaded core/ASP.NET/application/
Firely assemblies include paths, assembly/file/informational versions, MVIDs
and SHA-256, correlated to runtime assets and package identities in `.deps.json`.
The addressed-model presence gate uses the exact `Hl7.Fhir.<FHIR-version>`
package at the artifact's `FirelyPackageVersion` (baseline **5.11.4**, candidate
**6.2.1**), not a baseline/candidate filename-suffix rule. After per-assembly
seal/closure validation, `ValidateRequiredLoadedAssemblies` joins that exact
package's runtime assets to the sealed PE inventory within the actual net8
entry-point closure. A loaded correlation must match the model PE name/path,
artifact variant/root and declared package identity/version/digest.
Missing package metadata, missing sealed model assets and missing loaded
models have separate diagnostics identifying the addressed version, package
and artifact. A `.Core` spelling is supported only when the sealed metadata
actually owns that PE; accepting either spelling by name alone is forbidden.
`Hl7.Fhir.Base`, the application, identical probe, core runtime and Kestrel
remain mandatory. Assembly version alone is never used as a package version.
Mixed SDK/runtime or escaped loaded closures still fail.

The retained `phase1-004` baseline R4 `ready-snapshot.json` records the
**unsuffixed `Hl7.Fhir.R4`**, assembly **5.11.4.0**, owned by the prepared
`Hl7.Fhir.R4/5.11.4` dependency from that verified published archive.
Runtime/source/cache/tenant/probe and individual assembly-correlation checks
were reached successfully, but the old final required-name check incorrectly
demanded `Hl7.Fhir.R4.Core`. The child exited normally; **zero complete
challenges and zero measurements** resulted. This observation corrects the
speculative suffix expectation, not the release/runtime selection. Baseline
R4 subsequently passed the corrected identity gate in `phase1-005`.

The regression fixture calls the same required-identity helper as launch,
using in-memory metadata only. It covers unsuffixed model ownership, a
metadata-supported suffixed PE, wrong package/version/origin, missing models,
unowned lookalike files, and the retained mandatory identities. Synthetic
fixture identities do not establish additional live artifact feasibility.

In `phase1-005`, baseline R4 passed startup/runtime/configuration/assembly
gates, Patient PUT, Observation POST, both audited reads, seeded state, and
the successful hook OperationOutcome. The stored Bundle read returned 200,
but strict full-content comparison rejected its `timestamp`: the original
input used `2025-01-02T03:04:05Z`, while the released serializer returned
`2025-01-02T03:04:05+00:00` at both Bundle and R4 event instant positions.
The embedded Observation's `effectiveDateTime` retained `Z`. The unmodified
response is retained at
`performance-data\phase1-005\preflight\baseline-R4\06-bundle-read.json`
under the local slot. PID 19596 exited normally. The pre-freeze input spelling
above matches this observed serialized representation without a semantic
product change or any response-oracle exemption.

The corrected `phase1-006` subsequently passed all six real challenges,
including stored Bundle and receipt checks, on .NET/ASP.NET 8.0.30, with
exit 0 and no forced termination for all six children. Preserve that evidence
and its archived tool/probe bytes. It does not establish the 72-row full-scale
gate, new update/search/delete semantics, or any performance measurement.
Rebuild the Phase 2 tools, choose a **fresh Phase 2 run ID**, and prepare new
tool/fixture seals before attempting the expanded preflight.

`prepared.json` now contains the full workload ledger described above;
`fixtures\corpora\<corpus-key>.json` contains all clinical bytes and
permutations, and `fixtures\cases\<row-key>.json` contains the complete four
requests/expected identities/version-specific notification bytes.
Preflight re-generates and independently validates the sealed inputs before
launching. The prepared file itself is hashed in `preflight\input-seal.json`
and rechecked along with artifact/runtime/tool/probe/fixture/manifest seals
before and after children.

Each `preflight\<row-key>\<variant>` retains launch/handshake/process/exit
records, capped stdout/stderr, readiness, ready/seeded/prepared/audited (and
create-cleanup) snapshots, network observations, dependency correlations and
oracle results. `case-prepared.json` records the fixed input order and zero
warm-up. `case-NNNN-page-NNNN-request.json` and the matching response file
retain every case request and complete response headers/bytes; audit reads
and deletes also retain full responses. Byte arrays are base64-encoded,
unmodified bytes, not reserialized approximations.

Successful seed interactions use **one `seed-audit.json` per child**, not one
raw file per request. It records required/attempted/completed/validated
write and read counts, each validated input hash/length, bound identity/
metadata, and each validated response's status, byte length, body SHA-256,
whole-`ResponseEvidence` SHA-256 and response interval. Only responses that
actually passed full content/header validation enter this summary.
`seed-failed.json` retains the failing full response when available, its
identity/stage and counters; earlier validated evidence is still written.
The seed hash policy does not apply to case or audit responses.

`case-progress.json` keeps attempted, completed and validated operation
counts separately, as well as actual HTTP and cleanup counts. Failure or
cancellation preserves progress, raw responses and child cleanup evidence.
There is no reattempt/resume within a run ID and no best-of selection.
The full preflight fails fast on an incompatible case, but
**`preflight\matrix.json` always enumerates all 72 rows and both variants**,
including failed and `not-attempted` entries. `preflight\failures.json`
retains the overall failure, failed attempts and every unattempted case;
`failed.json` additionally records the overall error. A failed preflight,
including a handoff/seal failure before launch, consumes the run directory.
Atomic create-new ownership of `started.json` prevents a competing invocation
from appending failures into another invocation's active evidence.

Only **`preflight\completed.json`** establishes the full untimed gate. It
requires 72 successful pairs, 144 successful fresh process instances
(PID **and start time**, since OS PIDs may be reused), four completed and
validated operations per child, clean shutdown, and unchanged seals.
It explicitly records **`MeasuredTrials: 0`**, **`BenchmarkComplete: false`**
and the remaining Phase 3/4 gates. The matrix also carries those flags.
Completion also requires a zero CLI exit and no failure markers; a cancelled
or failed final evidence write retains `finalization-failed.json` and cannot
be accepted merely because a partial `completed.json` exists.
The unchanged Phase 1 `Program` wrapper still prints its legacy six-gate
sentence after the runner's new full-matrix message; that sentence is not
the Phase 2 completion criterion. Full `run`/`render` remain refused.

Six additional named `PerformanceEvidenceTests` facts cover deterministic
corpora, the exact matrix, all notification shapes, error/truncated/wrong-ID
searches, no-op/version/growth failures, and every receipt entry including
embedded double-storage. The twelve earlier tooling facts and existing
application tests are unchanged. These synthetic controls never count as
live preflight or measured trials.

Unit fixtures, source inspection, this documentation, or existence of
an output file do not establish live full-matrix success. Missing package
provenance, policy rejection, host observation failure, an escaped effective
cache, wrong runtime, mismatched content/receipt or failed cleanup is an
execution blocker. Retain it and return to the caller; do not weaken a check,
patch production or substitute a different baseline/runtime.

The later report must distinguish observed branch-versus-release differences
from SDK-only causation, preserve all 72 rows and mandatory allocation/GC/
footprint/retention evidence, and label valid noisy comparisons inconclusive.
No performance budget, numeric win, smaller matrix or synthetic report is a
substitute for that executed outcome.
