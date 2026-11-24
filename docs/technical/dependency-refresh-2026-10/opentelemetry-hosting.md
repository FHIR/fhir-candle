# OpenTelemetry hosting integration

**2026-10-16, Windows x64 — phase 15 local gates passed; recommend 1.20.0 for parent acceptance.**
Final Windows full-suite and Linux/macOS validation remain pending. No commit was made.

## Unit and identity

Clean predecessor reverified: `b2edb4dfb93a22373c7f9e9abd4a135fc85b8187`.
Authority: [AGENTS.md](../../../AGENTS.md), phase15/unit contract/V-accept; prior
[API/SDK](opentelemetry-api-sdk.md), [Console](opentelemetry-console.md),
[OTLP](opentelemetry-otlp.md) and [Hosting.Abstractions](hosting-abstractions.md) dossiers read-only.
Only direct move: **OpenTelemetry.Extensions.Hosting 1.16.0 -> 1.20.0**.
API/SDK/Console/OTLP/ProviderBuilderExtensions remain 1.20.0; instrumentation remains separately pinned **1.16.0**.
Hosting.Abstractions remains **10.0.13**. Earlier accepted prerequisites are required; arbitrary rollback is not certified.
Program is unchanged. Tests add one fact and bounded fixture stop/observation hooks, not a public facade or lifetime redesign.

Raw SHA-256 (CRLF); final test source is identical before/after pin:

| Identity | SHA-256 |
|---|---|
| Host csproj | `2b1214d2dd9c95f3fff2151aa7e3697a5ac994679322e81de414d24692262eb9` |
| Tests, 859 lines / 42,267 bytes | `35a4a9012015e2eae2979f504d93f051a6ed2d4ea7e6462a9fe22162b06453f3` |
| Unchanged Program | `6f73ee17e7c6b2a5b685e7854beaf509a343efe794a20beddc301b739f8dc7dd` |
| Test-only native `git diff --no-ext-diff --binary --`, 7,390 bytes | `c6162a48bb389dc1e9b99556776d81b9c9eb0a7353070104e9f413102ab2f63b` |
| Executable patch (Host + Tests), 8,304 bytes | `2b603ab30984622105317767c1ddc18577257ffe47122fc8225c728684dd3617` |

## Fresh eligibility and supplier changes

All seven telemetry indexes still identify **1.20.0** as highest stable.
[Hosting index](https://api.nuget.org/v3-flatcontainer/opentelemetry.extensions.hosting/index.json),
[registration](https://api.nuget.org/v3/registration5-gz-semver2/opentelemetry.extensions.hosting/1.20.0.json)
and [catalog](https://api.nuget.org/v3/catalog0/data/2026.10.15.13.59.14/opentelemetry.extensions.hosting.1.20.0.json)
mark it listed/non-prerelease, published **2026-10-15T13:54:09.58Z**, without deprecation/vulnerability entries (not a security audit).
Fresh nuspecs: `https://api.nuget.org/v3-flatcontainer/{lowercase-id}/{version}/{lowercase-id}.nuspec`.
Hosting's native net10/net9/net8 groups require SDK **>=1.16 -> >=1.20**, already accepted.
Hosting.Abstractions minimums remain **>=10.0.0 / >=9.0.0 / >=8.0.0**, respectively, satisfied by 10.0.13.
SDK -> ProviderBuilderExtensions -> API minima are 1.20; Console/OTLP require SDK >=1.20;
retained instrumentation admits ProviderBuilderExtensions **[1.16.0, 2.0.0)**. No second direct move is required.
Tagged [VERSIONING](https://raw.githubusercontent.com/open-telemetry/opentelemetry-dotnet/core-1.20.0/VERSIONING.md)
supports API `a` with SDK `[a,next-major)` and stable public API compatibility within major 1; shared numbering is not equality.

Reviewed [release notes](https://raw.githubusercontent.com/open-telemetry/opentelemetry-dotnet/core-1.20.0/RELEASENOTES.md),
[Hosting changelog](https://raw.githubusercontent.com/open-telemetry/opentelemetry-dotnet/core-1.20.0/src/OpenTelemetry.Extensions.Hosting/CHANGELOG.md),
README, all Hosting C# sources in both supplier tags, and accepted SDK provider/disposal sources:
- **1.17:** trim/AOT annotation; no publishing-mode certification here.
- **1.18:** `ITelemetryHostInitializer` separates initialization from the hosted service. Actual hosted startup now delegates
  to its DI singleton, still resolving real meter/tracer/logger providers. `StopAsync` remains a completed no-op.
- **1.19:** new `IHostApplicationBuilder.AddOpenTelemetry` seeds host resource defaults/live configuration. Production still
  calls **IServiceCollection.AddOpenTelemetry**, so it does not opt into that overload or its new defaults.
- **1.20:** no additional Hosting-specific changelog entry. Existing registration de-duplication and DI-owned disposal remain.
No adaptation or pre-existing behavior repair was needed. Two research 404s (guessed hosted filename, then a new interface
requested at the old tag) were corrected by reading each tag's actual tree, not treated as positive evidence.
Supplier commits: 1.16 `611868a27afda59761da60b98610f8d3b85497dc`; 1.20 `d5b2f8a818f7a2b4f3a4c3108d1a27b11395b10e`.
HTTP-body SHA-256: index `5280fce65c10407605e75c061d8091e82278e56363cd7bfa292a7cd4a5ff1e6f`;
nuspec 1.16 `92e8dd15f234af546c25e539f6d9aa42edc7f130032374b65ca52a83282a2c6c`,
1.20 `c68ae4bca528354915468564845efede2da741c47d5c36855ed8ff2e76a3af9b`.

## Full graph, assets and unrelated pins

Captured original full package-only target nodes; compared every field after restore.
Normalization: git-relative slash `csproj|target` keys, recursively sorted compact JSON, `ensure_ascii=False`, UTF-8/no final LF.
Both: **12 projects / 36 targets / 946 nodes / 387,010 bytes**.
Before, also reproduced after baseline B: `9f8dbbeaeea43da091dc4c4af24b42c2c86c2f7f0d9361947e360ba4bd43dafb`.
After: `2b3426f4bc60894399b95c324852fbd09f924c2df96c7425ab90b6fe61ae0e7b`.
Exactly **six graphs** change: Host/Tests x net10/net9/net8, only Hosting identity 1.16 -> 1.20 and SDK minimum 1.16 -> 1.20.
Other **30 graphs and every unrelated node/field are identical**; zero added/removed package IDs or native/RID payloads.
Hosting compile/runtime remain `lib/<matching-TFM>/OpenTelemetry.Extensions.Hosting.dll`, related `.dll.sigstore.json;.xml`.
Tests retain the Hosting.Abstractions edge at the respective 10/9/8 minima; redundant Host edges remain pruned.
Hosting.Abstractions 10.0.13 uses matching-TFM DLLs on Tests all targets and Host net9/net8; Host net10 is framework-pruned.
Its Configuration/DI/Diagnostics/FileProviders/Logging abstraction minima remain >=10.0.13, unchanged.
Other telemetry uses matching-TFM managed DLLs except instrumentation net9 -> net8. DiagnosticSource is framework-provided
on net10, package 10.0.13 on net9/net8. All environment/logging/DI/Binder closure fields remain unchanged.
These are native-TFM **managed assemblies**, not native binaries. Different assets warrant all-target focused execution.
All **21** deployed telemetry DLLs match selected package bytes on baseline/candidate; candidate Hosting.Abstractions' three also match.
Executed roots: `C:\ai\git\fhir-candle\src\fhir-candle.Tests\bin\Release\<TFM>\`.
Hosting cache metadata names nuget.org; contentHash matches assets. Nuget.org/VS offline feeds remain enabled; no signature-audit claim.
Namespace-aware XML comparison (including parent conditions): **46 references / 36 IDs**, only Hosting changed.
Sorted compact `[path,parent-attributes,reference-attributes]` JSON, 5,160 bytes: before
`ef73e584e6ccde2d6032f841222262a23d6b1faa6df5543e586200a50387370d`; after
`e94ab5750ca107246987dc7fb94bd619d75426f6264e22b498612bf383b48dc2`.

## Windows gates and lifecycle findings

Windows 10.0.26200/win-x64; SDK 10.0.400/MSBuild 18.9.6; runtimes 10.0.11/9.0.19/8.0.30; xUnit adapter 3.1.5.
Each sequence ran from repository root, synchronously with a 180-second initial wait:
```powershell
dotnet build --configuration Release --framework net10.0
dotnet test --configuration Release --no-restore --verbosity normal --filter "FullyQualifiedName~fhir.candle.Tests.OpenTelemetryCompatibilityTests"
```
TA is the documented all-target same-filter form; its net10 run supplies T10 without duplicate execution. No full suite.

| Sequence | Hosting / fixture | B exit; warnings/errors | TA exit; net10 P/F/S | net9 P/F/S | net8 P/F/S; TA warnings |
|---|---|---|---|---|---|
| 1 | 1.16 / initial new fact | 0; 8/0 | **1; 32/1/0** | **32/1/0** | **32/1/0**; 24 |
| 2 | 1.16 / corrected final fact | 0; 8/0 | **0; 33/0/0** | **33/0/0** | **33/0/0**; 24 |
| 3 | 1.20 / identical final source | 0; 8/0 | **0; 33/0/0** | **33/0/0** | **33/0/0**; 24 |

Initial test-authoring failure: single-argument ConfigureOpenTelemetry callbacks execute immediately, not at provider construction.
Supplier source confirmed the two-argument IServiceProvider overload is deferred; corrected only the new callbacks and rebaselined.
Every old assertion is preserved. Only existing Pkcs 10.0.0/8.0.1 **NU1902/NU1903** remain
(GHSA-2fmv-rv2c-59x7 / GHSA-846f-f764-76mx); no new compiler/analyzer warning or suppression.
Each TA: **99 occurrences / 33 unique names / each three times**, exact baseline/candidate multiset equality.
Sorted full emitted names/parameters, duplicates retained, status/duration removed, LF join/no terminal LF SHA-256:
`e3a4e96d09a47ab3122663c2314343f1180c766571ad6b8ae4b7fba03e65821d`.
Old-32 subset: **96 occurrences**, unchanged `3c54f3fb612f44524f8e67fd7dd6160350f4cd3c5b0e5d45084bd6b61d5107fd`.
Interleaving prevents individual-name TFM attribution; all three identified runs summarize 33 passes. Numeric discovery total is not emitted.

- New `TelemetryHostedProvidersStartAndStopOnce`: private production configurator, real Kestrel host/providers and HTTP/protobuf receiver.
  Trace/meter deferred construction counts are zero before start, all three providers exactly one after start and through shutdown.
  One actual TelemetryHostedService and one provider per signal; references stay identical across a new scope and explicit host stop.
  ApplicationStarted/Stopped callbacks each fire once. First request ForceFlush delivers; final unflushed request delivers all signals
  on disposal, with exactly one subject span/log per route, disposed DI and no subsequent traffic/console during 300 ms observation.
- **Ownership distinction:** hosted StopAsync does not flush/dispose providers; DI disposal does. Host lifecycle callbacks are not
  claimed as per-provider StopAsync invocation counters. No wrapper, replacement exporter, generic mock or manual initializer call.
- Reuse all 32 existing cases for gRPC HTTP/2, HTTP/protobuf, endpoints/signals/resource/context, flush/dispose and failure/rejection.
  No second transport matrix merely to duplicate the new lifecycle name. Success-shutdown and rejection-shutdown paths remain exercised.
- Existing 10-second flush and 30-second network/start/stop/dispose bounds retained. All hosts/clients disposed; environment, console,
  propagator/activity/baggage restored. No helper survives; no install/new package/external telemetry endpoint.
- Not cold first-use disposal, restart/stress, TLS, trim/AOT, whole FHIR startup, packaging or collector-persistence certification.

## Native evidence handoff

Direct redirection was restricted. Parent should archive these actual spools to session `files\phase15-*`, not transcribe logs.
Every filename below is under **`C:\Users\ginoc\AppData\Local\Temp\`**; SHA-256 hashes cover complete spool bytes.

| Evidence | Spool filename | SHA-256 |
|---|---|---|
| Original full graph/direct inventory | `1792191309372-copilot-tool-output-33128-73630f5f-e009-47c8-a82b-25b30ad4bc14.txt` | `11891df8eac892896d5868e5982a1cb491b7ee556ddcf070908837dfc4ebcf3b` |
| Fresh metadata/policy/releases, ending in first 404 | `1792191310046-copilot-tool-output-33128-ae8d6c3d-729e-4c81-a63a-c7e228c3feb2.txt` | `fd3e99c83ab2d3eb998060ed9faab52b1ecd4156f3a4a92587a4d27e96875d6e` |
| Both-tag actual Hosting sources | `1792191341484-copilot-tool-output-33128-475b9189-2987-45b8-a400-b4e1bf70ee5e.txt` | `f07c622fba3fa2de352ac83aba10e903b6a8e2a2a737d688330a492701130f20` |
| Hosting README / SDK provider sources | `1792191367839-copilot-tool-output-33128-9ca11bab-dcc6-49ae-b3b8-7ad767b12969.txt` | `a85e1eee231a1cb03dbe21aad70214a3ab156c2d65ab8bb512aa2ce1aa8cc972` |
| Sequence 1 B+TA, authoring failure | `1792191416806-copilot-tool-output-33128-7d7e4910-0346-4e66-8689-69a2a148a691.txt` | `3fda9498a79d1ed52f33d1893dff2370e0095ced18910dff1fd559aaa4749b12` |
| Sequence 2 B+TA, final baseline | `1792191470497-copilot-tool-output-33128-725d5221-0eab-4c06-ad1c-c1707b44a575.txt` | `32d64255b61dfb4e77772c14cea2d8ca8809b3a3e8a81bc6f5e7f87a1ebcacbc` |
| Baseline inventory/graph/assets/test patch | `1792191498430-copilot-tool-output-33128-95f25f17-e2eb-4f4b-a201-76762f7b03c8.txt` | `cffd3f7f33457df8d4393cd037f5b9718fc0b996c1ccf2dd931f5bd8870021f0` |
| Sequence 3 B+TA, candidate | `1792191515427-copilot-tool-output-33128-c52373a0-491c-4da7-8aa5-5a23965902f3.txt` | `a9e2148fa27b85a666fd30d2ecd48acf740ae9766289252c34be3320d5dc5920` |
| Candidate full graph/deltas/assets/executable patch | `1792191558324-copilot-tool-output-33128-e65868dd-c9e9-4fce-9ecb-f91a66576e0b.txt` | `a8401255fc97883fd4f493b140fb2d93c8a2eba405d60a4a36c1a7d1f5e82646` |

`git diff --check` passed. Only three owned deliverables changed; index/plan/prior dossiers untouched.
Final read-only prior-file byte comparison initially hit Git LF versus worktree CRLF; normalized comparison passed.
All three deliverables retain CRLF/final newlines; no trailing whitespace. This probe was not a build/test failure.
**Final Windows full-suite and all six Linux/macOS target cells remain pending**, not waived by this local focused pass.
