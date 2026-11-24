# OpenTelemetry Console exporter

**2026-10-16, Windows x64 — phase 13 local gates passed; recommend Console
1.20.0 for parent review.** Final Windows full-suite and Linux/macOS validation
remain pending. No commit was made.

## Unit, prerequisite and identity

Predecessor: `77e4507e152bebd8808555ca96b9e20125751e08`; read-only pre-flight
confirmed that HEAD, clean worktree and empty index. Command/style authority:
[AGENTS.md](../../../AGENTS.md). The [API/SDK dossier](opentelemetry-api-sdk.md)
was read and remains unchanged.

| Package | Before -> selected |
|---|---|
| OpenTelemetry.Exporter.Console | **1.16.0 -> 1.20.0**, only changed direct pin |
| OpenTelemetry / OpenTelemetry.Api | 1.20.0 -> 1.20.0, required prior checkpoint |
| OpenTelemetry.Api.ProviderBuilderExtensions | 1.20.0 -> 1.20.0, transitive, not newly pinned |
| OTLP exporter / Extensions.Hosting / Instrumentation.AspNetCore | 1.16.0 -> 1.16.0, independently retained |

Only the Host csproj Console entry, **71 additive lines** in the existing
telemetry fixture, and this dossier change. All original assertions and the
real private `Program.ConfigureOpenTelemetry` remain unchanged. No extra
project, library, public facade, production adaptation or prerequisite repair.

Final raw-file SHA-256:

| Identity | SHA-256 |
|---|---|
| Host csproj | `dfcdaca11a62f83063cef9483efd8a360ffa0e2e4d1084d52b9ac7410a658ea7` |
| Tests, identical final baseline/candidate source | `2de64a03bcb571a3b96f770fb25eb495a26b4934bee02a66eb1901b5294c8955` |
| Unchanged Program | `6f73ee17e7c6b2a5b685e7854beaf509a343efe794a20beddc301b739f8dc7dd` |
| Unchanged API/SDK dossier | `f7e861d1b61547c5a5d486313f3ea682312411d8265ae86808c03493aaf82b92` |
| Executable `git diff --binary --` Host csproj and Tests, 5,992 bytes | `817f7cef3e1b3cfa403bdcbf0dfa9286cf5de55548f66e8272280bf3fe8a1e7e` |

## Fresh availability and supplier review

Re-read NuGet indexes for Console, SDK, API, OTLP, Hosting and instrumentation:
each still has newest stable **1.20.0**. Console registration/catalog mark
1.20.0 listed, non-prerelease, published **2026-10-15T13:54:11.397Z**, without
deprecation/vulnerability entries. This is not a general vulnerability audit.

Sources: [Console index](https://api.nuget.org/v3-flatcontainer/opentelemetry.exporter.console/index.json),
[registration](https://api.nuget.org/v3/registration5-gz-semver2/opentelemetry.exporter.console/1.20.0.json),
[catalog](https://api.nuget.org/v3/catalog0/data/2026.10.15.13.59.14/opentelemetry.exporter.console.1.20.0.json).
Nuspecs were freshly read at
`https://api.nuget.org/v3-flatcontainer/{lowercase-id}/{version}/{lowercase-id}.nuspec`:
Console 1.16/1.20, SDK/API 1.20, retained OTLP/Hosting/instrumentation 1.16.

Console 1.20 requires **SDK >=1.20.0** in native net10/net9/net8 groups, already
selected here. Retained OTLP/Hosting require SDK >=1.16; instrumentation
requires ProviderBuilderExtensions **[1.16.0, 2.0.0)**. Their other minima
remain satisfied by the predecessor closure. The version-tagged
[supplier policy](https://raw.githubusercontent.com/open-telemetry/opentelemetry-dotnet/core-1.20.0/VERSIONING.md)
supports API `a` with SDK `[a, next-major)` and stable public API compatibility
within major 1. Shared release numbering is not an equality constraint:
no second direct move is required. Removing the earlier API/SDK upgrade is
not certified safe.

Reviewed tagged [Console changelog](https://raw.githubusercontent.com/open-telemetry/opentelemetry-dotnet/core-1.20.0/src/OpenTelemetry.Exporter.Console/CHANGELOG.md),
[release notes](https://raw.githubusercontent.com/open-telemetry/opentelemetry-dotnet/core-1.20.0/RELEASENOTES.md),
Console log/activity/metric exporters, and SDK resource-builder sources:

- **1.17:** resource schema URL rendering is relevant: the SDK's default
  resource/AddService supplies a schema. New cases verify that rendering
  separately from unset logger schema metadata. Trim/AOT compatibility is
  supplier metadata, not a tested publishing mode here.
- **1.18/1.19:** expanded key/value-list/dictionary JSON rendering does not
  require adaptation for this configured path's scalar route, service and
  log-marker attributes. No dictionary-shape serialization certification.
- **1.20:** logger instrumentation-scope section is relevant. Assert its
  `Name: fhir-candle`, and absence of unset logger Version/Schema URL within
  that section. No new logger options are enabled.
- Release-wide OTLP changes and cold first-log-export-during-disposal fixes
  are not Console changes and are not claimed for retained OTLP 1.16.

Fresh source SHA-256 (HTTP bodies; registration decompressed):

| Source | SHA-256 |
|---|---|
| Console index | `59b2a8c9f3deca62db02c11f69d3900be9d3e18879c8e8210083a47143e911de` |
| Console 1.16 nuspec | `e9029272c6e5e4d7abd583ce4e630be78e471e36cf450b66edb217754acf9c9b` |
| Console 1.20 nuspec | `180307fd10fa7d8bdee2587d5a6dbddc4a4ab747b88e7675e1a81f0f27482ab5` |
| Console registration / catalog | `afef2ca3788173fc878e903882b7e3072dabf08296614c90794116502dd5ea87` / `791f49b205319a47010933608cf4b466388d8efaa84f3627459d3ce02f9022b9` |
| Console changelog | `98266b2134756fc814363511edca12aa4a2955e6c289218b1301aaf1d3410176` |
| VERSIONING / RELEASENOTES | `e8b4f3f7b4206c994e31fe71a3f17878a23579bbae0a6417e32f36d89728af75` / `41be9dd0b9e4fd14066687d1edc06a2bda97427441b88b5150ef9caf066c402e` |

## Complete graph and deployed assets

Captured all full package-only target nodes in memory before trial, then
compared every node/field after restore. Normalization: git-relative slash
`csproj|target` keys, recursively sorted compact JSON, `ensure_ascii=False`,
UTF-8, no final LF. Both graphs: **12 projects / 36 targets / 946 nodes /
387,010 bytes**. Each restored baseline reproduced the original hash.

| Graph | SHA-256 |
|---|---|
| Before | `0540b923a3db7c6ed51e3a0070a538ebc82568846e7991c5075bfa9bd4d994b3` |
| After, reproduced at final candidate | `4294a3a97959694976f312aa7c739446688bd7fb6bcb33eef9d8a6701d618b0c` |

Exactly **six** graphs change: Host and Tests, each net10/net9/net8. In each,
Console/1.16.0 is replaced by Console/1.20.0 and its SDK dependency minimum
changes 1.16 -> 1.20. Compile/runtime paths remain
`lib/<matching-TFM>/OpenTelemetry.Exporter.Console.dll`, with the same related
`.dll.sigstore.json;.xml` metadata. No other node/field changes; **30 graphs**
are entirely unchanged. No new/removed package ID or unexplained drift.

These are native-TFM **managed** assemblies, not new native/RID binaries.
SDK/API/ProviderBuilderExtensions, retained OTLP and Hosting likewise use
matching-TFM assets. Instrumentation retains net10 and net8 assets, with
net9 selecting **net8**. DiagnosticSource remains framework-provided on
net10 and package **10.0.13** on net9/net8. The differing environment,
logging and DI closures detailed in the prior dossier are unchanged.
This target-specific selection justified all-target focused execution.

All **21** deployed telemetry DLLs in Tests matched their selected package
runtime assets byte-for-byte on the corrected baseline and final candidate.
Candidate Console DLL SHA-256, net10/net9/net8 respectively:
`bd1fe4f53a97a77429a5d82ea044cb8e7e6ab50e98a6fbd10ee2a27d9515bade`,
`cd136cafc0511b145ca3121b7908134ba7a1ea221cb5b925728b60e2463c5efe`,
`f4e96b949208c79c8822e5befaa85bb9079d4339bbfc88179045fb4a0df63375`.
Console cache metadata names nuget.org; its content hash equals the assets
library hash. Nuget.org and VS offline feed remain enabled. No signature
audit or fresh-network-download claim.

Namespace-aware XML inspection of all tracked csproj/props, including parent
conditions: **46 references / 36 IDs**, only Console changes. Compact sorted
JSON of `[path,parent-attributes,reference-attributes]` in file/document order
is 5,160 bytes; SHA-256 before
`f62ac6c6751c0c49dad9c0de0d050db48e012f0b1844563a0ee64542ef331f3e`,
after `d86687c3b5368767237a975e24544858c519367f40f585b6f4248fd77e67362e`.

## Windows execution and case inventory

Windows **10.0.26200**, win-x64, SDK **10.0.400**, MSBuild **18.9.6**;
.NET/ASP.NET **10.0.11 / 9.0.19 / 8.0.30**, xUnit VSTest adapter **3.1.5**.
Commands ran from repository root, synchronously, 180-second initial wait:

```powershell
# B
dotnet build --configuration Release --framework net10.0
# TA: documented all-target form of T-telemetry
dotnet test --configuration Release --no-restore --verbosity normal --filter "FullyQualifiedName~fhir.candle.Tests.OpenTelemetryCompatibilityTests"
```

TA's three identified test binaries and three summaries cover net10/net9/net8;
net10 supplies T-telemetry without a redundant separate net10 invocation.
Every B exited **0**, zero errors, **8** baseline warnings. Each TA had
**24** baseline warnings. No new compiler/analyzer diagnostic, suppression,
skipped case or zero-discovery success.

| Sequence | Console / fixture | TA exit | Per-TFM passed / failed / skipped |
|---|---|---:|---|
| 1 | 1.16 / initial addition | 0 | 28 / 0 / 0 |
| 2 | 1.20 / initial addition | **1** | 26 / 2 / 0 |
| 3 | 1.16 / informational-version correction | 0 | 28 / 0 / 0 |
| 4 | 1.20 / informational-version correction | **1** | 27 / 1 / 0 |
| 5 | 1.16 / final fixture | 0 | **28 / 0 / 0** |
| 6 | 1.20 / identical final fixture | 0 | **28 / 0 / 0** |

Each row was preceded by B. **Test-authoring failures retained:** row 2's
`all`/`logs` cases mistook assembly identity 1.0.0.0 (both package versions)
for package version. Informational versions are 1.16.0/1.20.0 plus supplier
commit. Row 4's `logs` case incorrectly asserted no schema anywhere in
output: SDK default resource schema is distinct from unset logger schema.
Supplier source confirmed both causes. Corrected only the new assertions'
version source/section boundary, added positive resource-schema checks, and
reproved the identical final fixture on original Console before repinning.
Neither failure implicated an existing assertion or required production repair.

All six TA logs contain **84 executed name occurrences, 28 unique names,
each three times**. Original 24: the unchanged 20 configuration rows,
disabled/context facts, and two protocol lifecycle rows enumerated in the
prior dossier. Added four:
`ConsoleExporterEmitsConfiguredSignals(signals: "traces"|"metrics"|"logs"|"all")`.
Discovery lifecycle and named execution establish nonzero intended discovery;
the adapter does not emit a separate numeric discovered total.

Sorted name multiset SHA-256 (complete emitted names/parameters, strip
status/duration, retain duplicates, LF join without final LF):
**`b9602f655a7cb5320aee25e767c49360f173fdd79e8b4172dbcc2c88e004699f`**
before/after. Original-24 subset remains
`6738c4c9b34030b6c6bd3f8ba7466c2f7a8867139ba2be4f59c8a7a06f1fa6d6`.
Inventory reads accounted for interleaved status prefixes and duplicate
xUnit `[FAIL]` notices; these are not additional executions.

Only existing Pkcs **10.0.0 / 8.0.1** NU1902
([advisory](https://github.com/advisories/GHSA-2fmv-rv2c-59x7)) and NU1903
([advisory](https://github.com/advisories/GHSA-846f-f764-76mx)) appear.
Counts above are build summaries, not repeated warning display lines.

## Actual emission, lifecycle and limits

The four added cases reuse the existing isolated console capture, private
production configuration reflection, loopback Kestrel hosts, request and
bounded flush/observe helpers. They check actual ASP.NET span display name,
server kind and route; duration metric/instrumentation meter; warning log
category, body and structured marker; service resource and version-appropriate
scope/schema rendering. Single-signal cases also exclude disabled signals.
No timestamp, ordering or complete-output snapshots; no real service payloads.

The unchanged 24 cases still verify OTLP HTTP/protobuf **and HTTP/2 gRPC**,
endpoints/overrides, W3C context, conditional/disabled signals and resource
delivery. Both lifecycle cases flush once, issue a final unflushed request,
stop/dispose the real host/providers, require all final signals in console
and OTLP, then observe **300 ms** with no further export. **This is not cold
first-use disposal coverage.** No TLS, outage/retry, sustained-load,
trim/AOT, packaged-tool or whole FHIR startup certification.

All endpoints/clients are disposed by the existing bounded fixture; process
environment, console and context are restored. The temporary in-memory graph
comparison finished normally. No installed tool, external collector or owned
helper remains. Final full Windows suite and all Linux/macOS target cells
remain **pending**, not waived by this focused local pass.

## Native evidence handoff

Output redirection was unavailable in this execution context. No transcript
or manually transcribed native log substitutes for it. The tool retained
the actual output under
**`C:\Users\ginoc\AppData\Local\Temp\`**; parent archival to the requested
session `files\phase13-*` directory is still needed. Final B+TA outputs are
complete in rows 5/6. Exits and `Get-Content` tails were checked.

| Row | Full native spool filename under the directory above | SHA-256 |
|---|---|---|
| 1 TA | `1792190054201-copilot-tool-output-33128-b58f2f55-c651-4576-88e9-238c6ec0f0bd.txt` | `283564a61fdc69ec40bd95db248ba155c05516730d1e9766c929e7540a9e225c` |
| 2 TA | `1792190093066-copilot-tool-output-33128-137290c9-fce6-48be-9997-d7ba3bd4f475.txt` | `27d4feaa48d895ae95c83b9e0fdbcf4b6544b71782c6a6106806028540d5d908` |
| 3 B+TA | `1792190158682-copilot-tool-output-33128-eed15903-fe5a-487c-afa9-04541e395b8e.txt` | `cc2589c037621ade70300a60bbf1199cc67b347422be8e90ec67cf7a0b668a2d` |
| 4 B+TA | `1792190217715-copilot-tool-output-33128-7bc86868-6dcf-4aae-b29d-bf9570e71136.txt` | `4c68da0794139ce6214e2f9d66041c35672829c2e40b11f608966c2ebe19b8f2` |
| 5 B+TA | `1792190290574-copilot-tool-output-33128-b92fafb3-0d54-400f-a4e8-5b9785c49a61.txt` | `cddef68e432a2e0af1f827e66dd89abc941fed4ec23f2e95080030024d474af2` |
| 6 B+TA | `1792190322650-copilot-tool-output-33128-fbb3b00a-62b1-41d5-aa7b-347790ef9f68.txt` | `5ac00945e188113a84458017eeaf89b11718a2ed928eda7d3ebdeb283adebc24` |

Initial B outputs remain in tool sessions 564/569; full-node before/after
comparison in completed `phase13-graph`. `git diff --check` passed; only the
three owned files differ. Index, plan and all prior dossiers remain untouched.
