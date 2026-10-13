# Sealed-artifact performance comparison

## Current boundary: fixed trials, calibration and evidence-only reporting

`FhirCandle.Performance` is a BCL-only external HTTP client. Its optional
reference to `FhirCandle.Performance.Probe` shares the bounded pipe protocol,
not application or Firely types. Neither project is referenced by production.
The probe is injected only into experiment-owned children through
`DOTNET_STARTUP_HOOKS`. Loading the probe without its activation configuration
does nothing.

Supported commands are **`prepare`**, **`run --preflight-only`**, **`run`** and
**`render`**. They are sequential stages of the **same unique run ID**.
Preflight preserves all **72 paired rows / 144 fresh semantic processes** at
the true corpus scales, with four untimed case operations and every seed
PUT/read-back validated. It then runs real normal-sampling and sampler-off
paired pilots for **every one of the 72 version/scale/case profiles**, including
idle/loaded control windows, 128 actual-case warm-up operations, restoration,
all footprints and full oracles. Calibration is **never an accepted trial**.

Only after paired coverage succeeds are shared counts and the alternating
measurement schedule frozen. Full `run` uses at least **432 additional fresh
children**: twelve cases × three versions × two scales × two variants ×
three independent repetitions. No reset, simultaneous variants, partial-batch
denominator changes, best-of retry, or pooling across count/protocol changes
is supported.

CLI usage errors return **2**. Execution/cancellation, failed gates and
incomplete rendering return **1**. **0** means exactly the requested stage
succeeded: preparation does not mean execution, calibration does not mean
accepted measurement, and only full saved-evidence completeness can support
a complete report. `render` never starts servers or accesses the network; it
writes an explicitly **INCOMPLETE** report when mandatory evidence is absent.
The legacy six-gate console sentence is removed.

The parent independently audited `phase2-001`: **72 pairs / 144 fresh
lifetimes passed**, with 871,200 seed PUTs, 871,200 full read-backs, 576 untimed
case operations, clean exits and actual .NET/ASP.NET 8.0.30. That evidence and
the earlier `phase1-006` challenges remain historical gate evidence, not
measurements. **Phase 3 is not complete; fresh corrected-tool verification and
full calibration remain required.** The following timeout is historical,
not the current blocker. The retained
`phase3-001` v1 pilot `pilot-0033` (baseline R4, 100 Patients/1,000 Observations,
`observation-token`) completed all 128 warm-ups but only **283/1,024** fixed
operations (284 attempted) in **59.8725985 seconds** before the common
60-second runner/probe window canceled. This is an inadequate duration gate,
**not a baseline/candidate benchmark finding**. All 177 prior children exited;
the full semantic rerun, 33 calibration attempts and failed partial batch
remain untouched.

The subsequent v2 capacity recovery introduced the common finite capacities
below. On the user-confirmed private network, fresh `phase3-003` passed all
72 semantic pairs / 144 children and 67 of 72 complete sampled/sampler-off
profiles before baseline large-scale R5 `observation-reference`, `pilot-0424`,
failed its warm-up audit. All 11,000 seed writes/read-backs and 128 warm
responses completed. Response 64 was HTTP 200: observed UTC went from
`2026-10-07T03:14:24.2135622Z` to `2026-10-07T03:14:23.9405349Z`, while the
independent 10,000,000-Hz Stopwatch interval was positive **0.7471468 seconds**.
A retained Windows clock-adjustment event corroborates the rollback.
This was an invalid wall-clock ordering assumption, **not a performance
finding**. All 568 children exited cleanly; all 23,491 files are preserved.
No accepted measurements or freeze exists for that run. Its diagnosis and
independent stop audit remain local historical evidence; neither is rewritten
or used as accepted data.

The authorized correction uses **`candle-perf-phase3-v3`**,
**`candle-perf-measurement-v3`** and **`candle-perf-preflight-v3`**. It preserves
actual UTC while using recorded monotonic evidence for elapsed/order checks.
It requires **fresh tool bytes/seals, a new schema-3 manifest/run ID and a
fresh full semantic preflight/calibration**, not an upgrade or continuation
of `phase3-001`, `phase3-002` or `phase3-003`. Unchanged sealed
application artifacts may be reverified; old protocol observations cannot be
pooled or accepted. Source/test success or a longer convenient timeout does
not prove actual calibration, Phase 3 completion, or the requested comparison.
The parent owns real launches, the frozen manifest and the populated report.
A completed live calibration alone does not establish offline evidence
validity or Phase 3 completion.
Phase 4 later records the actually validated final reproduction recipe.

Fresh v3 `phase3-004` completed all 144 semantic children and 72 sampled/off
profiles (446 retained attempts, a 432-trial frozen schedule, CLI exit 0).
Offline rendering then rejected retained `coverage-insufficient` pilot-0065:
its 1.0841506-second loaded control had 65 managed but only four process
samples, without sampler errors. All selected pairs were adequate; the
renderer incorrectly reapplied the control coverage minimum during
retention-only validation. This correction preserves every other gate and
the original insufficient status. `phase3-004` remains unmodified diagnostic
evidence, not accepted measurement or calibration reusable with changed
tool bytes. Fresh verification, tooling/seals and a new full run/calibration
remain required; no pooling is permitted. The schema stays **v3** because
serialized evidence, bounds and clock semantics are unchanged.

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

The recovery implementer's documented local checks are:

```powershell
dotnet build --configuration Release --framework net10.0
dotnet test --configuration Release --framework net10.0 --no-restore --verbosity normal --filter "FullyQualifiedName~fhir.candle.Tests.PerformanceEvidenceTests"
```

These are verification instructions, **not a record that live feasibility or
calibration has passed**. Do not run a stale net8 tool after changing source
and building only net10. The caller owns T-project, the all-target sequence,
candidate provenance, immutable tool copies and all live execution. Do not
run build/test load concurrently with calibration or measurements.

The installed host must be exactly
`C:\Program Files\dotnet\dotnet.exe`, Windows x64. Both applications must
actually load **Microsoft.NETCore.App 8.0.30** and
**Microsoft.AspNetCore.App 8.0.30**. Compilation SDK, timestamp-derived
application assembly versions, and measured runtime are separate identities.
No application-control-policy bypass, machine-wide hook, runtime major
roll-forward, development launch profile, source-baseline fallback or product
change is supported.

## Separate artifact inputs from unique run output

For a fresh Phase 3/4 attempt (retain all earlier development directories):

```text
C:\ai\git\fhir-candle\scratch\1001-01\
  experiment-clock-run-NEW.json           replace NEW consistently with an unused run ID
  performance.md                         written only by evidence-only render
  performance-data\
    artifacts-001\                       reusable, independently verified inputs
      candidate-source\                  caller's clean detached checkout
      candidate-net8\                     caller's sealed output and assets
      candidate-build.json               retained provenance (schema below)
      candidate-build.log                actual log path may differ
      isolation.txt                      actual authorized-environment evidence
      fhir-candle.2025.611.1422.nupkg       downloaded or verified cached archive
      baseline\                          unchanged full extracted package layout
      clock-run-NEW-tools\                fresh complete immutable net8 runner/probe copy
    phase3-001\                          retained failed v1 evidence; never edited/reused
    phase3-002\                          retained v2 evidence; never edited/reused
    phase3-003\                          retained clock-failure evidence; never edited/reused
    clock-run-NEW\                       fresh v3 OUTPUT; must not exist before prepare
      manifest.json
      prepare-started.json
      prepared.json
      fixtures\
      preflight\
      environment.json
      calibration\
      frozen-measurement.json
      measurement\
      trials\
```

Preparation and successful preflight/calibration files are expected:
**`prepare` → preflight → `run` → repeated `render` for the same manifest/run ID
is supported**. Exclusive create-new stage/attempt records prevent accidental
reuse. Preparation does not overwrite an existing directory; preflight does
not resume an earlier preflight; measured `run` rejects any existing measured
attempt directory. It does not reject the matching preparation/preflight
files. Only the derived markdown may be overwritten by re-rendering.

Use a new ID (replace the portable `clock-run-NEW` placeholder consistently)
and matching `OutputDirectory` after failures,
tool/probe/protocol edits or another preflight. Preserve failed directories.
Inputs can be reused only after hash verification. Do not place artifacts or
the probe beneath the unique output directory. Never replace a seal with
moving `bin` contents while using the same run ID.

All of these runtime inputs/outputs are ignored local data, not source paths
to stage or force-add. The exact eventual report target remains:

`C:\ai\git\fhir-candle\scratch\1001-01\performance.md`

## Seed manifest (schema 3)

JSON property names are case-sensitive; unknown properties are rejected.
The example below supplies the actual pinned paths and settings. Before use,
the caller must supply real candidate provenance, an existing isolation
evidence file, and actual `Conditions`. Change `Isolation.Authorized` to
`true` **only after** confirming the described controls. `false` and the
zero/unobserved machine-condition example deliberately prevent accidental
launch. They are not requests for the runner to create controls, invent
hardware or assert a quiet machine.

```json
{
  "SchemaVersion": 3,
  "ProtocolVersion": "candle-perf-phase3-v3",
  "RunId": "clock-run-NEW",
  "OutputDirectory": "C:\\ai\\git\\fhir-candle\\scratch\\1001-01\\performance-data\\clock-run-NEW",
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
  "ProbePath": "C:\\ai\\git\\fhir-candle\\scratch\\1001-01\\performance-data\\artifacts-001\\clock-run-NEW-tools\\FhirCandle.Performance.Probe.dll",
  "ToolSourceRevision": "e72c517d21ccb7c2f98b4dbd0b32ae4c9071db80",
  "ToolSourceDescription": "Uncommitted phase-three v3 clock-aware recovery on the named revision; replace with the actual verified source/build identity. prepare seals fresh immutable tool/probe bytes separately from both unchanged applications.",
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
    "SetupSeconds": 1200,
    "BatchSeconds": 7200,
    "RequestSeconds": 30,
    "SettlingMilliseconds": 1000,
    "ShutdownSeconds": 15,
    "SampleMilliseconds": 10
  },
  "Measurement": {
    "SchemaVersion": 1,
    "TrialsPerVariant": 3,
    "InitialMutationOperations": 256,
    "InitialOtherOperations": 1024,
    "MaximumPilotOperations": 8192,
    "MinimumSamples": 10,
    "ControlMilliseconds": 1000
  },
  "Conditions": {
    "ObservedUtc": "1970-01-01T00:00:00+00:00",
    "Hardware": "REPLACE with actually observed machine/processor description",
    "PhysicalMemoryBytes": 0,
    "PowerConditions": "REPLACE with observed power policy and AC/battery conditions; do not change global settings",
    "BackgroundConditions": "REPLACE with observed background activity and reserved execution-window conditions",
    "ToolBuildSdk": "10.0.400",
    "ObservationSource": "REPLACE with the actual local observation record and observation time"
  }
}
```

The C# defaults for schema, protocol, pins, runtime, port, tenant, worker count
and bounds are shown above. Serialized `SchemaVersion`, `ProtocolVersion`,
`Bounds` and every bound field are **required**, so omitted old settings
cannot silently acquire v3 evidence semantics. Paths, run ID, both artifact input
objects, tool source identity/description, isolation evidence and actual
execution `Conditions` are required. The example build SDK must likewise be
confirmed, not copied as evidence. The tool records its actual OS,
architecture, logical processors, machine name, runtime and invocation in
`environment.json`; physical RAM/power/background conditions come from the
caller, never from the renderer's current machine.
The C# definitions beside `ExperimentRunner` are the authoritative typed
schema. A changed manifest, including changed serialized bytes, requires new
preparation under a new run ID.

The seed and prepared schemas are **3**, the probe protocol is
**`candle-perf-phase3-v3`**, and the distinct measurement contract is
**`candle-perf-measurement-v3`**. The mandatory prepared `Workload` ledger
has `ContractVersion` **`candle-perf-preflight-v3`**; this versions the
changed response/receipt evidence, **not the unchanged clinical generator**. Old preparation or
preflight evidence cannot be upgraded/reused in place. The workload ledger
records `Algorithm`, `Seed`, `PreflightOperations`, six `Corpora`, seventy-two
`Cases`, and both exact `StatusProfiles`. Every corpus/case descriptor records
its fixture path, ordered request seals, total payload bytes and
`RequestOrderSha256`. Each request seal includes number, method, relative URL,
Prefer, body byte length and SHA-256 (the empty-byte hash for bodyless GETs).
The order hash covers the indented UTF-8 serialized seal array, without a BOM.
`FixtureFiles` independently seals all **78** full fixture files.

`Measurement.SchemaVersion` is 1. Defaults are intentionally fixed except
`TrialsPerVariant`, which permits 3–10 before preparation. Exactly 128 warm-up
operations, 10 ms sampling, ten actual samples per stream, the 8,192-operation
pilot ceiling and the common bounds are part of this versioned protocol.
If bounds/capacity are inadequate, retain the failure and explicitly revise
the common protocol before a **new** run; do not edit an accepted freeze.
Schema-1/2 manifests, v1/v2 probe/measurement/preflight contracts, old 600/60-second bounds
and earlier prepared directories are rejected, not silently upgraded.
The parent writes a **new** manifest such as `experiment-clock-run-NEW.json`; the implementation
does not edit an existing manifest or any prior runtime evidence.

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
implementation repository root, after the caller fills the fresh v3 seed,
verifies all targets and copies the complete net8 runner/probe closure into
the new immutable input directory (never over an older copy):

```powershell
& "C:\Program Files\dotnet\dotnet.exe" .\scratch\1001-01\performance-data\artifacts-001\clock-run-NEW-tools\FhirCandle.Performance.dll prepare --manifest C:\ai\git\fhir-candle\scratch\1001-01\experiment-clock-run-NEW.json
& "C:\Program Files\dotnet\dotnet.exe" .\scratch\1001-01\performance-data\artifacts-001\clock-run-NEW-tools\FhirCandle.Performance.dll run --manifest C:\ai\git\fhir-candle\scratch\1001-01\experiment-clock-run-NEW.json --preflight-only
& "C:\Program Files\dotnet\dotnet.exe" .\scratch\1001-01\performance-data\artifacts-001\clock-run-NEW-tools\FhirCandle.Performance.dll run --manifest C:\ai\git\fhir-candle\scratch\1001-01\experiment-clock-run-NEW.json
& "C:\Program Files\dotnet\dotnet.exe" .\scratch\1001-01\performance-data\artifacts-001\clock-run-NEW-tools\FhirCandle.Performance.dll render --run-directory C:\ai\git\fhir-candle\scratch\1001-01\performance-data\clock-run-NEW
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

For the authorized recovery, the user confirmed a **private network with no
other clients connecting**. This is operational isolation, not a claim that
the firewall blocks external clients. The retained enabled Private-profile
`.NET Host` inbound allow permits the actual host broadly; it does not make
the wildcard listeners private. No additional firewall rule is requested or
changed. Preserve that disclosure and the user's confirmation in the fresh
manifest's isolation evidence, along with all original runtime/network,
tenant, population, response and receipt gates.

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
from evidence. Commands, hellos and activation configuration remain limited
to **4 KiB UTF-8**; stopped replies are **256 MiB**. A child still permits
**4,096 commands** and stopped **32-identity chunks**, not a sample of the
identity audit or an unbounded protocol. Oversized frame prefixes are
rejected **before** payload allocation.

`ProbeProtocol` defines the fixed common v3 contract with unchanged v2 capacities. The manifest's
`Bounds` must match; these are not additional general-purpose tuning knobs.
The frozen handoff retains those bounds and the versioned contract.
`launch.json` records `Bounds`, the bracketed `Issued` observation and monotonic
`DeadlineTimestamp`,
`RequestTimeoutSeconds` and `ProtocolLimits` (versions, lifetime/export/
boundary limits, sample capacity, all frame/command/identity caps and saved/
HTTP caps). Offline validation checks them against the same constants:

| Bound | Both variants, v3 |
|-|-|
| Startup/readiness | 120 seconds |
| Cumulative setup/audit (outside work) | 1,200 seconds |
| Full fixed batch and probe window | 7,200 seconds |
| Individual HTTP / ordinary pipe exchange / end acknowledgement | 30 seconds |
| **Stopped window export** | **Separate 120 seconds on each peer** |
| Fixed natural/full-GC settling | 1,000 ms |
| Boundary allowance | 10 seconds |
| Each graceful / exact-child shutdown interval | 15 seconds |
| Nominal cadence / fixed capacity per stream | 10 ms / **720,004** |
| Required actual in-work samples | At least **10 independently from each stream** |
| Saved large-record reader ceiling | **1 GiB = 1,073,741,824 bytes** |

The exact supported child lifetime is **120 + 1,200 + 7,200 + 30 + 120 +
10 + 2×15 = 8,710 seconds**. Both activation validation and the actual
lifetime CTS use the remaining **monotonic** deadline, exactly
`Issued.Before + Issued.Frequency × 8710`. The bracketed `Issued.Utc`
remains evidence but cannot extend or consume this budget. A greater,
expired or unbounded lifetime is rejected; the obsolete 20-minute ceiling
does not coexist with the longer work window. Parent startup and owned-child
shutdown limits still apply independently.

Ordinary exchanges and partial command frames remain 30 seconds. Only
`export-window` selects 120 seconds in the shared exchange-deadline helper.
The parent disposes the end-ack deadline before starting a fresh export
exchange, without linking it to the expired end/batch CTS or applying
`CommandAsync`'s ordinary limit. The child likewise links export only to
its overall lifetime, not a preceding request. Serialization, buffer copies,
pipe export and inspection stay **after end counters and all post-work
footprints**; no capacity repair moves them into an armed window.

Readiness uses fully consumed FHIR metadata responses and checks the declared
FHIR version. Startup, setup and requests have the manifest deadlines; HTTP
bodies remain capped at **4 MiB** and each log stream at **2,097,152 characters**.
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
The oracle samples no clock; it audits independently recorded evidence.

### Clock observations, UTC envelopes and limits

Every fully consumed response, including untimed readiness/seed/read-back/
audit/delete interactions, records required `Started` and `Completed`
`ClockObservation` values. Each contains the **actual unmodified** `Utc`,
`Before` and `After` monotonic ticks bracketing that UTC read, and positive
`Frequency`. Default/missing fields, negative/zero clock identities,
inverted brackets, frequency mismatch and nonsequential intervals fail.
No UTC is clamped, synthesized from Stopwatch, or relabelled as an elapsed
clock. Old response records cannot silently acquire these fields.

The existing batch/per-operation Stopwatch boundaries and fixed operation
counts are unchanged. Each response bracket must fit its independently
recorded logical-operation interval, including all search pages. Parent
observations before launch, after `Process.Start`, and after awaited exit
prove child ordering inside the enclosing trial observations. Parent
PID/start-time identity must be constant across a sequential invocation;
the child PID and OS process-start timestamp still match **exactly** across
handshake, process, counters/samples and exit records. OS start timestamps
authenticate identity, not chronological relationships to UTC reads.
Separate preparation, preflight/freeze and measurement invocations are
causally linked by verified immutable handoff hashes, not comparisons of
unrelated parent clocks or wall-clock dates.

For a request with UTC observations `Us`, `Ue`, monotonic brackets
`[s0,s1]`, `[e0,e1]`, and common frequency `f`, require `0 < s0 <= s1 < e0 <= e1`.
In 100-ns UTC ticks the evidence gives:

- `dLo = floor((e0-s1) × 10,000,000 / f)` and
  `dHi = ceil((e1-s0) × 10,000,000 / f)`.
- Net UTC discrepancy lies in
  `[(Ue-Us).Ticks-dHi, (Ue-Us).Ticks-dLo]`.
  A net adjustment is detectable only when zero is outside that interval.
  This detects forwards/backwards steps or rate/slew discrepancies, without
  an arbitrary tolerance or a claim about the OS cause.
- Newly assigned persistence metadata and hook processing times must lie
  within **`[min(Us, Ue-dHi), max(Ue, Us+dHi)]`**. These are the outer bounds
  of the two endpoint clock trajectories over that request, not a global
  process-lifetime allowance. Ordinary unchanged clocks yield the observed
  interval plus only the measured endpoint uncertainty. The old blanket
  extra second is not retained.

Creates still require new identity, version `"1"`, correct headers and full
content. Updates/restoration still require actual changed clinical content,
identity continuity and exactly one version increment, now with timestamp
binding to **that mutation request**, not a requirement that UTC exceed the
previous version's timestamp. Immutable reads/searches/deletes retain exact
bound versions and `meta.lastUpdated` (including its offset), even if a
previously validated timestamp lies in the newly corrected clock's future.
The HTTP Last-Modified comparison retains its defined whole-second precision;
it is not a metadata timing tolerance.

Each hook expectation carries its own POST observations. Every registered
processing time and the first persisted Bundle metadata are bound to that
POST's envelope, not the later audit GET. Repeated Bundle reads retain the
first exact binding. Receipt ordering, all event/Bundle/subscription/topic/
context fields, absence of double storage and strict fixed clinical
timestamp **spellings/offsets** are unchanged.

Offline completeness rechecks seed digest clocks, all response endpoints and
cross-operation gaps in execution order, operation/batch nesting, launch/
exit observations, and exact process identities. Reports disclose detected
within-response, between-response and parent-lifecycle net discrepancies,
their uncertainty, raw UTC examples and references. **Endpoint observations
cannot locate a step's exact instant or detect cancelling intermediate
changes.** The derived envelope is an explicit interpretation limit, not
proof of an exact wall-time event or a guarantee that no adjustment occurred;
out-of-envelope metadata still fails rather than widening the bound.
No clock-setting operation, sleep to provoke NTP, or OS-event-log permission
is needed for reproduction. Event logs support the historical diagnosis only.

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
This untimed cleanup exercises the shared restoration primitive; it is not
128-operation warm-up or a measured create batch.

The semantic substage needs at most 12 requested identities for a four-event
hook and five stopped snapshots for create cases. It uses the same versioned
bounded protocol as calibration/measurements. Effective settings, actual empty-source
registration/filesystem, network ownership and sealed loaded assemblies are
checked at **every** snapshot, including after the exercised case and cleanup.
The common **1,200-second setup deadline includes all seeding, read-backs,
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
The later `phase2-001` passed the entire expanded semantic gate, independently
audited as described above. Neither establishes a Phase 3 metric. Rebuild the
new tools, choose a **fresh Phase 3 run ID**, and prepare new tool/protocol
seals before the semantic-plus-calibration preflight.

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

Only **`preflight\completed.json`**, its complete matrix/raw seals and absence
of failure markers establish the full untimed gate. It
requires 72 successful pairs, 144 successful fresh process instances
(PID **and start time**, since OS PIDs may be reused), four completed and
validated operations per child, clean shutdown, and unchanged seals.
It explicitly records **`MeasuredTrials: 0`**, **`BenchmarkComplete: false`**
and the remaining calibration/measurement/report gates. The matrix also carries those flags.
Completion also requires a zero CLI exit and no failure markers; a cancelled
or failed final evidence write retains `finalization-failed.json` and cannot
be accepted merely because a partial `completed.json` exists.
The CLI now reports the completed stage precisely. After this semantic
substage it continues into the calibration below; it does not print the
legacy six-gate sentence or claim a complete benchmark.

## Calibration, count freezing and expected execution cost

There is deliberately **no small-smoke substitute, implicit calibration skip,
or separate command that can bless existing phase-two measurements**.
`run --preflight-only` performs these stages sequentially:

1. The unchanged full 72-pair semantic matrix: **144 children**, four case
   operations each, true full corpora and all seed read-backs.
2. For each of the **72 exact version/scale/case profiles**, a fresh sampled
   baseline/candidate pilot pair, starting at **256** logical operations for
   `patient-create`, `observation-create`, `subscription-receive`, and
   **1,024** for every update/read/search case. Both pilots include the real
   128-operation warm-up/restoration and both one-second controls.
3. If (and only if) a pilot's only validation failures are fewer than ten
   actual samples in either stream in a sampled work or control window,
   retain **both** results, including the `coverage-insufficient` status,
   and double N for **both** variants.
   Any other counter, protocol, oracle, timeout, buffer, process, shutdown or
   state failure stops the run. Continue only up to common N=8,192.
   Offline retention validation waives only this minimum, not any other
   evidence check; insufficient pilots cannot be selected or accepted.
   Accepted repetitions never pool pilot observations or change N.
4. At the adequate shared N, run a fresh **sampler-off pair**, with the
   identical corpus, warm-up, histories, boundaries and controls. Counters,
   CPU and stopped footprints remain active; both in-window sample streams
   must actually be empty. Sampler-off work is calibration, not a measured
   cell with missing mandatory memory.
5. Save `calibration\completed.json` and **`frozen-measurement.json`** only
   after all profiles have complete adequate selected normal/off pairs.
   Selected sampled pairs and accepted trials require at least ten actual
   samples in each stream in both controls **and** work. The frozen handoff
   (schema 3) binds the prepared
   manifest/app/runtime/tool/probe identity, settings/bounds, 72 input-file
   seals/order hashes/payload sizes, every pilot attempt and selected normal/
   off pair, full semantic raw-file seals, environment and exact schedule.

Without coverage retries this preflight uses **432 fresh children**:
144 semantic + 144 normal pilot + 144 sampler-off. Each inadequate normal
pair adds two children. Minimum setup is **2,613,600 seed PUTs plus 2,613,600
full read-backs** across those stages. Calibration adds 36,864 actual warm-up
operations, their audits, controls and measured pilot batches.

The separate accepted `run` defaults to **432 more children**, 2,613,600
seed PUTs plus the same number of read-backs, 55,296 warm-up operations, and
the fixed measured batches. `TrialsPerVariant` 4–10 increases these counts
proportionally. Scheduling is repetition, then the frozen 72-row order;
the first variant of a pair is baseline when
`(zero-based-case-block + repetition - 1) % 2 == 0`, candidate otherwise.
Each dispatch has a unique attempt ID/directory; PID **and start time**
must be independent, sequential and non-overlapping, established by the
recorded parent monotonic lifecycle rather than wall-clock ordering.

The parent's observed phase-two semantic-only run took about **35 minutes**.
Using its setup rate alone, the new no-retry preflight and accepted run each
have about **105 minutes of equivalent seed work**. This is an extrapolation,
**not a measured Phase 3 duration**, and it seriously understates total work.
The retained four-operation **untimed semantic diagnostics** project up to
**2,472.65 seconds for the unchanged 1,024-operation large Observation batch**.
For the **36 Observation-search variant/version/scale profiles alone**, two
calibration plus three measured repetitions project **43.62 hours**, or
**49.08 hours including 128-operation warm-ups**, before other cases, setup,
audits, settling, hashing and I/O. These are planning projections only,
**never benchmark comparison results or guarantees**.

The 7,200-second common window gives roughly 2.91× that largest diagnostic
batch projection; it is a finite adequacy allowance, not proof that a run
will finish or a performance budget. Reserve a genuinely adequate multi-day
execution window and memory/disk capacity. No operation reduction, skipped
fresh gate, duration-driven count, simultaneous variant, reuse of prior
bytes/run IDs or pooling is justified by that cost. Rendering
rehashes and revalidates potentially large raw data; allow minutes or longer
according to actual local evidence volume, with no server execution.

Once prepared, **do not rebuild over the accepted tool/probe output paths**.
`prepare` seals runner DLL/deps/runtimeconfig, its referenced probe and the
injected probe; hashes are rechecked around every lifetime. An archived
tool directory can be used if it contains the complete matching net8 tool
closure and the manifest names those exact probe bytes. Moving paths or
rebuilding after preparation changes identity and requires a fresh run.
Application artifacts remain the independently sealed release and candidate,
never the moving implementation application's `bin` output.

## Fixed measurement lifecycle and accounting

All calibration and accepted case children use the same lifecycle:

1. Prepare warm/measured inputs once per row/count, request messages, body
   buffers, response lists and sample storage outside timing. Establish
   authenticated readiness; warm the probe's real window/export, checkpoint
   and reflection paths in a no-HTTP primer before IDLE. Retain readiness/
   warmed inventories, then the one-second **idle control**.
2. Capture **idle-natural** and **idle-forced**. Full-GC means blocking full
   collection, finalizer drain, second blocking full collection, then the
   fixed one-second settle; no compaction-only or candidate-only alternative.
3. Seed and validate every corpus instance through normal PUT/GET. Warm the
   **actual case exactly 128 times**. The same shared response oracle used
   by semantic preflight validates every returned update, create, read,
   search page and hook outcome; it cannot check only the final update.
4. Read every created/final-updated resource or full-content warm Bundle.
   Record +128 growth before deleting warm creates, validate every delete
   representation, and audit all 128 absent identities in stopped chunks.
   Restore update clinical fields by an ordinary PUT only when needed:
   Patient's even 128-toggle batch already ends `active: true`, so it
   keeps actual version **129** with **zero** extra PUTs. Observation
   restores quantity 37.5 and original `...05Z` time, preserving the Patient
   reference and advancing version **129 → 130**. Retain original/warmed/
   restored metadata plus the final restored read in all cases.
5. Retain all **128 warm Bundles and all receipt entries** for the hook.
   No hook resource/receipt cleanup is attempted. Full stored contents,
   count/order/event timestamps/focus/context and absence of embedded
   instances are checked. The original empty-source marker and every
   built-in resource count remain visible.
6. Capture the complete warm/prepared inventory, the one-second
   **loaded control**, then **loaded-natural** and **loaded-forced**.
   Controls measure their own all-server allocation/GC/CPU/samples; they do
   not share a denominator with work and are not subtracted.
7. `begin-window` is an ordinary bounded/authenticated command parsed while
   stopped. The server snapshots `GC.GetTotalAllocatedBytes(true)`, individual
   generation counts, total GC pause duration and process CPU into a
   preallocated **88-byte binary boundary**. It starts only its window-scoped
   sampler. No JSON command parsing, diagnostic DTO/export or reflection
   occurs while armed: only the fixed one-byte end marker is accepted.
8. The client records begin-command/ack timestamps, then whole-batch and
   each logical-operation monotonic timestamps. It sends fixed N sequential
   requests and fully consumes bodies. It preallocates primary messages,
   stream buffers and result lists. Client header/body retention and minimal
   continuation parsing/continuation-message creation are included and
   disclosed, not called pure store time. Paging remains part of the same
   logical operation. All full-content validation is outside the window.
9. Stop client timing before stopping its PID sampler and sending End.
   Server End stops its sampler before taking the matching end counters.
   Before any export/diagnostic enumeration or audit reads, the probe
   captures **post-immediate**, waits one second, captures **post-settled**,
   then applies the same full-GC/one-second-settle protocol and captures
   **post-forced**. Only then is the binary end acknowledged and stopped JSON
   export allowed. The late acknowledgement/GC wait is **not** client T or
   the saved server counter interval.
10. Audit all collected responses, persisted creates/final updates, all
    retained warm plus measured Bundles and every receipt. Full measured
    create IDs and hook event numbers start at **129**, so none collides with
    warm-up. Record final inventories; save raw evidence; request ordinary
    shutdown and await the exact owned child before another starts.

The **1,200-second setup/audit budget** is cumulative outside the separate work
window. Any incomplete/failed response batch retains attempted/completed
counts and raw available responses. An HTTP error that fully consumed a
body is still an **invalid logical batch**, never a faster success.
Failure cannot change planned N or concatenate later work.

### Memory definitions and observer cost

Both samplers have **720,004 preallocated slots each** (7,200×1,000/10 + four
boundary slots), nominal 10 ms cadence, actual monotonic timestamps and explicit
dropped/error counts. The probe stores structs containing
`GC.GetTotalMemory(false)` plus last-GC heap, fragmentation, committed bytes
and GC index. The external sampler opens no unrelated PID: it samples only
the owned `Process` handle and verified start time's working set, private
bytes and CPU. Each struct is 48 bytes: **34,560,192 bytes per array,
69,120,384 bytes combined**, before array/object overhead and stopped export
copies. This identical larger reservation is disclosed observer cost, not an
optimization or a reason to subtract memory. It sleeps outside windows;
runner/testhost memory is excluded from server metrics.

At least **ten actual samples from each stream inside client work** are
required, independently of boundary/control samples. Wrong PID/lifetime,
out-of-server-window timestamps, nonmonotonic time, overrun, stream absence,
negative counters and nonpositive durations invalidate completion.
The maximum inside work is a **sampled lower bound**. No process-lifetime
high-water substitutes for it. Last-GC metrics are explicitly distinct from
occupied estimates and forced retention.

Stopped footprint structs capture process/managed values **before** creating
diagnostic DTOs, enumerating stores/assemblies or issuing further audit
requests. They retain both natural and forced observations and counters,
with corresponding known inventories. Natural loaded−idle and
settled-post−loaded deltas are separate from forced loaded−idle and
forced-post−loaded deltas. Working-set growth is not allocation; neither
a peak nor one forced checkpoint proves a leak.

Binary handshakes, pipe/BCL bookkeeping, the sampler and ordinary server
timers/background work remain in whole-server counter deltas. Capture
control allocation/GC/CPU and normal-versus-off work on every profile;
interpret their magnitude and accepted-trial spread, **never subtract a
guessed constant**. Missing adequacy is a blocker; adequate but noisy data
can be inconclusive. Identical bytes/settings do not imply zero observer
perturbation.

## Raw schema, offline completeness and report interpretation

New durable files/types are versioned and append-only:

| Evidence | Meaning |
|-|-|
| `environment.json` | Actual preparation OS/architecture/processor/runtime/invocation plus caller-observed hardware/RAM/power/background/build SDK; no inherited secret dump |
| `calibration\<row>\pilot-NN-inputs.json` | The exact once-generated `TrialInputs`: 128 warm requests, measured requests at offset 128, optional original-content restore PUT |
| `calibration\...\trial.json` | Schema-3 `TrialRecord` for each normal/off/failed-coverage attempt; never accepted measurement |
| `frozen-measurement.json` | Schema-3 immutable 72-profile identity/input/count/calibration/schedule handoff, parent identity and bracketed freeze observation |
| `measurement\started.json`, `ledger.json`, `completed.json` | Actual runner invocation, schema-3 ledger with bracketed lifetime/parent identity, all attempted/unattempted dispatches, failure status and independently checked completion |
| `trials\trial-NNNN\trial.json` | Origin, unique plan/process lifetime, prepare/freeze/input seals, status/failure, seed totals, warm histories, windows, controls, checkpoints and all raw references |
| `warmup-raw.json`, `measured-raw.json` | Every logical operation's unchanged monotonic times, separate required response UTC/monotonic observations, attempt/completion/page counts, unmodified bodies and headers; unfinished slots, errors and available partial-body/header evidence remain explicitly incomplete |
| `audit.json` | Every warm/measured per-operation oracle, ordinary read/delete/restore response and all stopped identity chunks |
| `seals-before.json`, `seals-after.json` | Exact prepared/probe/protocol/input identity checked around the entire lifetime |

All raw references are relative to the run, carry byte length and SHA-256,
and reject traversal/reparse points. Trial raw references enumerate the
entire child directory except the self-describing `trial.json`; the ledger
or calibration profile hashes that record separately. Seed digests are
allowed **only after full body/header validation**, retaining every
original identity/version/timestamp rather than discarding metadata.
All other warm/measured/audit response bodies are retained.

The v3 writer retains **compact JSON for `BatchWindow` and `TrialRecord`**,
including the work window and both controls. It does not drop sample
timestamps, fields or responses. Other input/handoff files retain their
indented serialization and clinical/order identities. Both saved-evidence
readers and raw-reference verifier share the **1-GiB** large-record cap, check the advertised file
length before allocating and reject truncation or growth instead of using
an unbounded read-to-end. Dedicated 1-MiB manifest, 32-MiB prepared/corpus
and 4-MiB HTTP limits remain smaller. Oversized writes fail, not truncate.

The deterministic capacity proof uses the widest signed numeric spellings
(even wider than valid nonnegative metrics), actual serializers and checked
arithmetic:

- A compact managed sample is at most **216 bytes**; 720,004 entries plus
  commas/brackets occupy **156,240,869 bytes**. A complete stopped window
  reply, including counters and all three post-work footprints, fits
  **157,289,445 bytes even with 1 MiB reserved for its envelope**, below
  268,435,456 bytes. Full 128+8,192 receipt growth is checked separately.
- A compact process sample is at most **209 bytes**; its complete array is
  **151,200,841 bytes**. Conservatively allowing **all three** saved
  windows to fill **both** arrays gives **922,325,130 bytes**.
  Adding **128 MiB for non-sample fields** totals **1,056,542,858 bytes**,
  below the 1-GiB reader cap. The tested full record includes all 8,192
  operation timings, warm metadata, checkpoints and raw references; even a
  50,000-reference inventory with synthetic 1-KiB relative names fits that
  envelope reserve. The updated proof serializes both added maximum-width
  lifecycle observations (**135 bytes each**) and maximum-width parent
  identity as part of the actual envelope, not a waived size limit.
  Response/seed observations are retained in their separately bounded raw
  files, not added to the sample arrays. Arbitrarily enlarged strings/bodies are not promised
  acceptance; actual writer/reader caps still fail closed.
- Small one-/two-entry production-serialization roundtrips verify the
  analytical expansion of the **whole** records, without a 1-GiB test
  allocation or a two-hour sleep. In-memory fixtures remain explicitly
  `unit-fixture`/size-only data, never saved live evidence.

`MeasurementReport.ValidateCompletenessAsync` independently checks:

- Exactly the frozen 72 keys, both variants and all configured independent
  repetitions; equal N for every paired cell and every repetition; identical
  schedule/input/config/protocol/app/runtime/tool/probe identity.
- Full 144-child semantics, complete adequate selected normal/off/control
  calibration for all profiles, retained coverage-only inadequate pairs
  (work or sampled controls) and doubling history, no unindexed attempt
  files or silently dropped failures.
- Every raw hash/length, clean exact-child shutdown and authenticated
  PID/start-time/nonce/probe receipt; repeated loaded-assembly/package seals
  and actual runtime/effective configuration, not a guessed `.Core` name.
- Every counter component, both sample streams, coverage, ordering,
  nonnegative deltas, controls, all seven checkpoints and forced-GC ordering.
  Mandatory JSON numeric fields are marked required: missing gen0 is not
  mistaken for a valid zero collection count.
- Full saved corpus digest/order/metadata, all warm/measured returned
  resources/pages through the same independent oracle, ordinary restoration/
  deletion/read-backs, exact post populations and every retained Bundle/
  ordered receipt/event. A 32-ID sample never stands in for a full audit.
- Real execution origin and all failure/attempt statuses. Unit fixtures
  retain `Origin: "unit-fixture"` and can never complete a live report.

The renderer reads only these saved files. It does not rehash/open external
application assemblies, start a server, access a network service, call
`prepare`, or borrow current-host memory/power state. Missing preparation,
calibration, a variant/trial/case/metric or raw oracle produces an explicit
**INCOMPLETE** markdown report and exit **1**, including the auditable fixed
72-row status shape. It never infers completion from an old report.
Re-rendering the same unchanged evidence yields the same markdown.

A complete report includes executive **observed improved/regressed/
inconclusive** findings; artifact/package/source/build/runtime dates and
names; actual conditions and invocations; all 72 paired rows in every metric
family; individual trial values/outliers; normal/off/control evidence;
payload/population/built-in inventories; correctness/failures and raw links.
Every metric has units, median **across independent trials**, min–max,
trial counts and N values, candidate/baseline ratio, percent and absolute
change. Primary time is T/N and throughput N/T; managed B/op uses the
entire server allocation delta over the same fixed successful N. Gen0/1/2
counts and counts/1,000 are separate; pause duration is not a fictitious sum
of generation counts.

Zero-baseline and negative incremental-memory denominator ratios are
`n/a` with their reason and absolute change. Descriptive direction labels
require disjoint observed trial ranges and a positive baseline; overlap,
equal values and unavailable relative interpretation are inconclusive.
Where normal/off share a metric (time/throughput/allocation and stopped
occupied/process footprints), a difference no larger than the maximum
observed within-variant normal/off difference is also inconclusive. No
sampler-off active peak is invented, and this diagnostic rule subtracts
nothing. Actual sample counts and mean observed inter-sample intervals have
their own 72-row descriptive tables. This is not a significance test or a
regression budget. Historical app/
SDK/dependency/compiler/built-in/status differences, loopback/client/probe/
background overhead, ordinary UI/host/data-protection and one-machine scope
are explicit confounders. The report never attributes differences to the SDK
alone or promises a win.

## Verification ownership and remaining execution gates

The interrupted implementation added these **nine** facts in the existing xUnit/VSTest
class, exercising used helpers and real recorder state guards with
non-performance fixtures:

- `TrialScheduleUsesFreshProcessesAndAlternatesPairs`
- `WarmupRestoresClinicalStateAndRetainsHookReceipts`
- `CounterWindowExcludesSetupAuditAndForcedGc`
- `PeakUsesOnlyServerPidSamplesInsideWindow`
- `FailedBatchCannotBecomeSuccessfulByChangingDenominator`
- `SummaryUsesIndependentTrialMedians`
- `RatiosHandleZeroAndNegativeBaselines`
- `RendererRejectsMissingVariantCaseTrialOrMetric`
- `RendererReproducesSavedEvidenceWithoutServerExecution`

The capacity recovery adds six coupled boundary/proof facts:

- `Phase3V3RequiresRevisedManifestAndFixedCapacities` (updated contract; capacities unchanged)
- `ProbeDeadlineCoversFiniteEndExportAndShutdownEnvelope`
- `StoppedExportUsesIndependentBudgetWhileOrdinaryRemainsThirtySeconds`
- `MaximumSampleReplyAndTrialSizesFitWithoutTruncation`
- `BoundedReadersRejectOversizedFramesAndChangingEvidenceBeforeAllocation`
- `WindowDurationRejectsOverflowAndPastSupportedBoundary`

They also pin UTF-8 (not character-count) configuration limits, cancellation
during reply deserialization, finite clock arithmetic and independent
coverage. Existing incomplete-batch/denominator/counter/oracle tests remain.
The clock-aware recovery adds seven deterministic facts:

- `ClockObservationsBracketUtcAndBoundForwardBackwardSteps`
- `RollbackWarmupSearchRetainsEveryOperationAndIndependentTiming`
- `ClockAwareMutationsAndImmutableReadsKeepStrictMetadata`
- `HookReceiptRangesSurviveClockStepsWithoutAcceptingWrongTimestamps`
- `ClockEvidenceRejectsMissingDefaultNegativeAndReorderedObservations`
- `LifecycleOrderUsesParentMonotonicEvidenceNotUtcChronology`
- `SavedLifecycleRejectsForgedIdentityAndMissingClockEvidence`

The existing saved-render roundtrip also checks unmodified rollback UTC,
adjustment/uncertainty disclosure and interpretation limits. The size proof
includes the new fields, and activation tests retain finite ceilings while
proving UTC steps cannot change the monotonic budget. The retention-only
coverage correction adds two facts:

- `RetainedPilotControlCoverageCannotBecomeSelectedOrAccepted`
- `RetainedPilotCoverageFlagPreservesSamplerOffValidation`

They exercise the actual trial validator with one-second loaded controls
containing 65 managed/four process samples and the inverse, strict
selected/accepted rejection, invalid/missing control evidence, and unchanged
sampler-off behavior. There are **42 tooling facts** (the preceding 40 plus
two), with **4,300 expected project tests**
if the previously verified 4,258 application tests remain unchanged. These
are source counts/expectations, **not a claim of new full-suite or live results**.
The implementer runs the documented net10 build and focused class; the parent
must run T-project from `AGENTS.md`, then the
documented all-target restore/build/test sequence before using the net8 CLI.
It must seal a fresh complete tool closure, use a fresh schema-3 seed/run ID,
and run the complete semantic plus
actual calibration gate, inspect boundary gaps/coverage/cleanup, exercise
incomplete and reproducible rendering, and only then collect the accepted
full matrix. Phase 3 is not operationally complete until those gates pass;
Phase 4's actual populated report remains a separate required outcome.

Six additional named `PerformanceEvidenceTests` facts cover deterministic
corpora, the exact matrix, all notification shapes, error/truncated/wrong-ID
searches, no-op/version/growth failures, and every receipt entry including
embedded double-storage. The earlier artifact/semantic tooling controls and existing
application assertions remain in place. These synthetic controls never count as
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
