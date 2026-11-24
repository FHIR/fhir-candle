# OpenTelemetry ASP.NET Core instrumentation

**2026-10-16, Windows x64 — phase 16 local gates passed; recommend 1.20.0 for parent acceptance.**
Final Windows full-suite and Linux/macOS validation remain pending. No commit was made.

## Unit and identity

Clean predecessor reverified: `39595c2989f1ed17f0c207652435892d69fa3ca8`.
Authority: [AGENTS.md](../../../AGENTS.md), phase16/unit contract/V-accept; preceding
[API/SDK](opentelemetry-api-sdk.md), [Console](opentelemetry-console.md),
[OTLP](opentelemetry-otlp.md) and [Hosting](opentelemetry-hosting.md) dossiers read-only.
Only direct move: **OpenTelemetry.Instrumentation.AspNetCore 1.16.0 -> 1.20.0**.
API/SDK/Console/OTLP/Hosting and transitive ProviderBuilderExtensions remain **1.20.0**.
Every other pin/option is unchanged. Accepted prerequisites satisfy the closure; arbitrary rollback is not certified.
Program is unchanged: real private ConfigureOpenTelemetry, normal IServiceCollection registration, no new defaults.
Tests reuse the existing Kestrel receiver/host, decoder, isolation and bounded cleanup; all old assertions remain.

Raw SHA-256 (CRLF); test source is identical before/after pin:

| Identity | SHA-256 |
|---|---|
| Host csproj | `84cca47782ca34308fe80ab0ea8e6e33d0e0b78bff87ff5c51883f0b114db5a6` |
| Tests, 970 lines / 48,588 bytes | `eca628995756a548a89a41048c4cf2fb86805aa0dfbff61dd52f0b8b95402cb3` |
| Unchanged Program | `6f73ee17e7c6b2a5b685e7854beaf509a343efe794a20beddc301b739f8dc7dd` |
| Test-only native `git diff --no-ext-diff --binary --`, 9,043 bytes | `f9726458845e88392f34a651d0d34f3c7b8d72e8fb6007ddbb3a1f7e22567ced` |
| Executable patch (Host + Tests), 9,966 bytes | `3ccd652aaea81103008e6ec63442dc3121c2f1a9ff36fb4a42408ddc50b0dc7a` |

## Fresh supplier assessment

All seven telemetry indexes still identify **1.20.0** as highest stable.
[Instrumentation index](https://api.nuget.org/v3-flatcontainer/opentelemetry.instrumentation.aspnetcore/index.json),
[registration](https://api.nuget.org/v3/registration5-gz-semver2/opentelemetry.instrumentation.aspnetcore/1.20.0.json)
and [catalog](https://api.nuget.org/v3/catalog0/data/2026.10.15.16.54.59/opentelemetry.instrumentation.aspnetcore.1.20.0.json)
mark it listed/non-prerelease, published **2026-10-15T16:50:10.86Z**, without deprecation/vulnerability entries (not a security audit).
Fresh nuspecs: `https://api.nuget.org/v3-flatcontainer/{lowercase-id}/{version}/{lowercase-id}.nuspec`.
Instrumentation's net10/net8 groups require ProviderBuilderExtensions **[1.16.0,2.0.0) -> [1.20.0,2.0.0)**.
SDK -> ProviderBuilderExtensions -> API minima are already 1.20; Console/OTLP/Hosting require SDK >=1.20.
Hosting.Abstractions 10.0.13 satisfies Hosting's 10/9/8.0.0 minima; OTLP net8 Binder >=8.0.2 is already satisfied.
No second direct pin, package/project or broad migration is required; unit frozen before trial.
Tagged [core VERSIONING](https://raw.githubusercontent.com/open-telemetry/opentelemetry-dotnet/core-1.20.0/VERSIONING.md)
supports API `a` with SDK `[a,next-major)` and stable public API compatibility within major 1.
Tagged [contrib policy](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/blob/Instrumentation.AspNetCore-1.20.0/README.md#stable)
requires SemVer for stable components; the instrumentation README labels this component stable.
README/source describe built-in ASP.NET metrics on .NET 8+, with net10/net8 assets and Microsoft.AspNetCore.App references retained.

Reviewed both supplier tags' instrumentation C# sources/project, tagged
[changelog](https://raw.githubusercontent.com/open-telemetry/opentelemetry-dotnet-contrib/Instrumentation.AspNetCore-1.20.0/src/OpenTelemetry.Instrumentation.AspNetCore/CHANGELOG.md)
and [README](https://raw.githubusercontent.com/open-telemetry/opentelemetry-dotnet-contrib/Instrumentation.AspNetCore-1.20.0/src/OpenTelemetry.Instrumentation.AspNetCore/README.md):
- **1.17:** cosign signature sidecars; **1.18/1.19:** core minimum updates.
- **1.20:** [#5458](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/5458) adds `error.type = "500"`
  for returned 500 responses on spans and duration metrics. On .NET 8-10 a DI-registered IStartupFilter supplies metric tags;
  unhandled exceptions retain their exception-type tag. This additive supplier fix is tested, **not called unchanged behavior**.
- The new-root-span environment switch is opt-in, not enabled here; existing W3C parent/baggage assertions still pass.
  Event filtering/host caching/display-name optimizations need no adaptation. No performance or experimental-mode claim.
- No product repair or candidate compatibility adaptation was needed; no failed authoring attempt or gate.

Supplier commits: 1.16 `61fd68a15de747c9accdd07273041b28ef454f99`; 1.20 `3aaa7709178529748ade65555422dd14c55be6c9`.
HTTP-body SHA-256: index `569c27fc27e72f479bd2ee5fada06104662d39ee607c6d6791c0c4c644f77437`;
nuspec 1.16 `3e8addcf1e1ba0b76148fa70ced6ec80fd6efb69320f1b4c9f745e6d8be94d79`,
1.20 `eed9450af26f231aa9441dda55e4c58d3da19b06159c61d073413256f5a8add2`;
changelog `ccef6f34bd66dacbce345adbcd14e8f419774398d107402d42f6fecb2d1f5918`.

## Full graph and asset delta

Captured original full package-only nodes and compared every field after restore.
Normalization: git-relative slash `csproj|target` keys, recursively sorted compact JSON, `ensure_ascii=False`, UTF-8/no final LF.
Both: **12 projects / 36 targets / 946 nodes**.
Before, reproduced after baseline B: **387,010 bytes**, `2b3426f4bc60894399b95c324852fbd09f924c2df96c7425ab90b6fe61ae0e7b`.
After: **387,238 bytes**, `95b71592e5a36a41a90264e94e25065ba77684d90889138aa2dd4d9a1f55943a`.
Exactly **six graphs** change: Host/Tests x net10/net9/net8, only the instrumentation node identity, dependency minimum,
and compile/runtime `related` metadata **`.xml` -> `.dll.sigstore.json;.xml`**. Other **30 graphs/every unrelated field** identical.
No added/removed package ID or native/RID payload. Assets are target-specific **managed** assemblies, not native binaries.
Instrumentation compile/runtime remain `lib/net10.0/OpenTelemetry.Instrumentation.AspNetCore.dll` on net10;
net9 **still selects lib/net8.0**, as does net8. Framework references stay Microsoft.AspNetCore.App.
Other telemetry DLLs use matching-TFM assets. DiagnosticSource remains framework-provided on net10, package 10.0.13 on net9/net8.
All environment/logging/DI/Binder/Hosting.Abstractions closure nodes are identical, including existing framework pruning.
These differing assets warrant all-target focused execution, not a net10-only compatibility claim.
All **21** deployed telemetry DLLs matched selected package bytes on baseline/candidate at
`C:\ai\git\fhir-candle\src\fhir-candle.Tests\bin\Release\<TFM>\`.
Candidate instrumentation DLL SHA-256: net10 `10398265b32699e6f1087ade9c11ee5e331a9975b87b530dc418946fe4612fab`;
net9/net8 `dabbfc81d8670f83aa35502d70d961aa207f505085308e88666be4d85c813225`.
Cache metadata names nuget.org; contentHash matches assets. Nuget.org/VS offline feeds remain enabled; no signature-audit claim.
Namespace-aware direct XML comparison, including parent conditions: **46 references / 36 IDs**, only instrumentation changed.
Sorted compact `[path,parent-attributes,reference-attributes]` JSON, 5,160 bytes: before
`e94ab5750ca107246987dc7fb94bd619d75426f6264e22b498612bf383b48dc2`; after
`26cbe816168b9429c1f3b1d7434385d9b9f5211603c6eb4a5dd650cc85361cba`.

## Windows gates and actual requests

Windows 10.0.26200/win-x64; SDK 10.0.400/MSBuild 18.9.6; .NET/ASP.NET 10.0.11/9.0.19/8.0.30; xUnit adapter 3.1.5.
Both sequences ran from repository root, synchronously with 180-second initial waits:
```powershell
dotnet build --configuration Release --framework net10.0
dotnet test --configuration Release --no-restore --verbosity normal --filter "FullyQualifiedName~fhir.candle.Tests.OpenTelemetryCompatibilityTests"
```
TA is the documented all-target telemetry gate; its net10 run supplies T-telemetry without a redundant individual-target replay.

| Sequence | Instrumentation / fixture | B exit; warnings/errors | TA exit | net10 P/F/S | net9 P/F/S | net8 P/F/S; TA warnings |
|---|---|---|---|---|---|---|
| Baseline | 1.16 / final new cases | 0; 8/0 | 0 | 36/0/0 | 36/0/0 | 36/0/0; 24 |
| Candidate | 1.20 / identical source | 0; 8/0 | 0 | 36/0/0 | 36/0/0 | 36/0/0; 24 |

Only existing Pkcs 10.0.0/8.0.1 **NU1902/NU1903** remain (GHSA-2fmv-rv2c-59x7 / GHSA-846f-f764-76mx).
No new warnings/suppressions, failed/skipped/missing cases or lowered discovery. Numeric discovery total is not emitted.
Each TA: **108 full-name/parameter occurrences / 36 unique names / each three times**, exact baseline/candidate multiset equality.
Full inventories are in the native evidence below. Interleaving prevents per-name TFM attribution; all three identified runs pass 36.
Normalization: strip status/duration, ordinal-sort full emitted names/parameters retaining duplicates, LF join/no terminal LF, UTF-8.
Inventory SHA-256: `3e24e9beed603be930b3d90821f351c3645b563ab9ac32dc091e847ba58b9c39`.
Old 33 subset: **99 occurrences**, unchanged `e3a4e96d09a47ab3122663c2314343f1180c766571ad6b8ae4b7fba03e65821d`.
Added `AspNetCoreRequestProducesConfiguredTelemetry(outcome, statusCode)` rows: `("success",200)`,
`("status-error",500)`, `("exception-error",500)`; all previous names/parameters remain as recorded in the Hosting dossier.

- Real private Program registration, HTTP request to `/phase16/request/{outcome}`, actual decoded OTLP HTTP/protobuf delivery.
  Exactly one matching server span; template-based name/route, method/scheme/path/protocol, integer response status, start/end times.
  HTTP 200 retains UNSET span status; both 500 paths retain ERROR. Every received resource has `service.name = fhir-candle`.
  Duration histogram retains seconds/cumulative temporality, route/method/scheme/protocol/status, count **1**, positive bounded sum.
- Success has no error.type; thrown InvalidOperationException has `System.InvalidOperationException` in span and metric on both pins.
  Returned 500 has no error.type on 1.16 and `"500"` on 1.20, explicitly version-qualified against supplier #5458, not a relaxed old assertion.
- Existing disabled fact now also sends the exception request: no providers, console or receiver traffic after stop/dispose/observation.
  Existing gRPC HTTP/2 and HTTP/1.1 protobuf peers, endpoints/signals/context, rejection/recovery, flush/disposal/singleton cases all retained.
- Isolation clears/restores ambient OTEL/proxy variables, console and initialized propagation/baggage state. Only synthetic loopback data.
  Existing 10-second flush/30-second request/start/stop/dispose bounds retained; all hosts/clients disposed, no surviving host/client/helper.
  No collector/library/project/facade, tool install, TLS, load, trim/AOT, whole FHIR startup or packaging certification.

## Native evidence handoff

Direct output redirection was restricted. Parent should archive these actual spools to session `files\phase16-*`, not transcribe logs.
Every filename below is under **`C:\Users\ginoc\AppData\Local\Temp\`**; hashes cover complete spool bytes.

| Evidence | Spool filename | SHA-256 |
|---|---|---|
| Original full graph/direct inventory | `1792191819803-copilot-tool-output-33128-3db40813-f91b-4c64-8ae6-4bdf4692e73a.txt` | `b17f517212d654edb0bc6abd1c6b6b9bfb8c6bee0b48d9b3c97065ced59de098` |
| Fresh metadata/policy/changelog/README | `1792191820508-copilot-tool-output-33128-e6f6c759-e0ef-45d4-8b2e-bf08ec374020.txt` | `9cae108c56eac5677ebda222b3dcdb8d311951dd45c5c21d38d4872dde3a084a` |
| Both-tag actual supplier sources | `1792191832540-copilot-tool-output-33128-58378d30-1a81-4e9f-a118-f36d47d5e13e.txt` | `b668d1f3b8db4f66884a652e15f2f78607c2f00786e54ec240c04350b2c2f556` |
| Supplier source comparison | `1792191847549-copilot-tool-output-33128-82696712-414d-48ab-9ce2-7f58b693c323.txt` | `7527e71675d10c94c6b2845a76cab4658988c4a744b914a7f074fc8213284191` |
| Baseline B+TA | `1792191950406-copilot-tool-output-33128-4e9d4729-277d-4afe-a12b-b8235aa01f94.txt` | `05cdc60effe6606a0c27d09a58159126881267e8dc7adbed145dead8f4ebe09b` |
| Baseline inventory/graph/assets/test patch | `1792191983469-copilot-tool-output-33128-0f182e4d-da04-45a3-8a73-087cbfc9cade.txt` | `9e81a5556daf0edb09b89366bdd85eed475b718a60e677256ea420219bf24656` |
| Candidate B+TA | `1792192006186-copilot-tool-output-33128-03a9e201-c43d-44eb-a65e-d5363bcf1ed3.txt` | `e78e194d88d98189643da0e6152c1d6630ed178563c1f103129fe8a001cc109b` |
| Candidate full graph/deltas/assets/patch/both inventories | `1792192048398-copilot-tool-output-33128-4e5ab39e-699d-4448-b852-07127aa8cd82.txt` | `a4eccbffefab1f41e5ba5b412cab1999cbd009bdd9025dfb342fe72096a9780e` |

`git diff --check` passed. Only three owned deliverables changed; index/plan/prior dossiers untouched.
**Final Windows full-suite and all six Linux/macOS target cells remain pending**, not waived by this local focused pass.
