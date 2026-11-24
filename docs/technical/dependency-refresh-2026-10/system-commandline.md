# System.CommandLine — 2026-10-15

**Decision: upgrade 2.0.9 → 2.0.13**, still the latest listed stable.
Only Host/Common/Tests pins move; Windows-local checks passed. Integration/status remain parent-owned.

## Boundary, sources, and prerequisites

- Revalidated the [version index](https://api.nuget.org/v3-flatcontainer/system.commandline/index.json) and [2.0.13 registration](https://api.nuget.org/v3/registration5-gz-semver2/system.commandline/2.0.13.json): listed, published 2026-10-13; higher 3.0 entries are prereleases, not candidates.
- The [2.0.9 nuspec](https://api.nuget.org/v3-flatcontainer/system.commandline/2.0.9/system.commandline.nuspec) and [2.0.13 nuspec](https://api.nuget.org/v3-flatcontainer/system.commandline/2.0.13/system.commandline.nuspec) have identical dependency groups: net8.0 has no dependencies; netstandard2.0 requires System.Memory >=4.5.5. The latter group is not selected here.
- Retain both NamingConventionBinder **2.0.0-beta5.25306.1** declarations. Its [index](https://api.nuget.org/v3-flatcontainer/system.commandline.namingconventionbinder/index.json) still has no stable release; its listed [nuspec](https://api.nuget.org/v3-flatcontainer/system.commandline.namingconventionbinder/2.0.0-beta5.25306.1/system.commandline.namingconventionbinder.nuspec) requires CLI **[2.0.0-beta5.25306.1, )**, without an upper bound, on both groups. No active binder invocation was found; this is not general binary-compatibility certification of the unused binder.
- Reviewed the supplier [migration guide](https://learn.microsoft.com/en-us/dotnet/standard/commandline/migration-guide-2.0.0-beta5). Existing `SetAction`, `ParseResult`, `GetValue`, and `InvokeAsync` calls already use that API shape; no production adaptation is required.
- `Program.cs` and `CandleConfig.cs` are unchanged: option names/aliases/arities, arrays, `ResponseFileTokenReplacer = null`, JSON → prefixed environment → unprefixed environment → CLI selection, and `_retVal` handling remain intact. Only bounded `TestParseCli*` regressions and the test-file copyright header were added.
- Prerequisites: the existing .NET 10 SDK/runtimes, enabled NuGet feeds, and isolated empty startup inputs. No earlier package upgrade is required. No new group/path/architecture, tool, runner, suppression, or scope waiver was needed.
- Roll back the whole unit (three pins plus its tests/evidence), then restore through B and repeat T-config/graph checks. Recheck later dependents before reversing an assembled pass; unlocked restore is not an arbitrary rollback guarantee.

## Identified source and environment

Predecessor: **`dc7a51a60394a1e3570d2f50ff3288dd09aad6b4`** (phase-1 docs;
source `5c2dd5f8817a9fc911a3e74785a970bd12eeb675`). Preflight: empty index, clean owned paths.

Candidate SHA-256: **`e969856354c3b05c1f1ba812da8c753051eb5ae8c9393504b4888e3842a5aed2`**.
Hash the 7,632 stdout bytes below; this excludes evidence and is noncircular:

```powershell
git --no-pager diff --no-ext-diff --binary dc7a51a60394a1e3570d2f50ff3288dd09aad6b4 -- src/fhir-candle/fhir-candle.csproj src/FhirStore.Common/FhirCandle.Common.csproj src/fhir-candle.Tests/fhir-candle.Tests.csproj src/FhirStore.Common/Configuration/CandleConfig.cs src/fhir-candle/Program.cs src/fhir-candle.Tests/ConfigTests.cs
```

The pre-pin focused check used **2.0.9 plus these same new tests**, not unchanged source:
patch SHA-256 `1d87e982de1887c748a700d8d16fc561831ce2c322c4babbcab88c772b7b9a64`.

Windows **10.0.26200 x64**, SDK **10.0.400**, .NET/ASP.NET Core **10.0.11**; xUnit VSTest adapter **3.1.5**.
Installed 9.0.19/8.0.30 runtimes and the parent's 4,306-pass-per-TFM
[baseline](README.md#evidence-boundary-and-windows-baseline) are history, not candidate results.

## Exact automated gates

From the repository root: [AGENTS.md](../../../AGENTS.md), [related-filter authority](../building-and-testing.md).

```powershell
dotnet build --configuration Release --framework net10.0
dotnet test --configuration Release --framework net10.0 --no-restore --verbosity normal --filter "FullyQualifiedName~fhir.candle.Tests.ConfigTests|FullyQualifiedName~fhir.candle.Tests.StrictModeStartupTests"
```

| Identity / gate, in execution order | Exit | Result |
|---|---|---|
| 2.0.9 + regressions, T-config | 0 | 22 passed, 0 failed/skipped; build summary 8 warnings, 0 errors |
| 2.0.13 candidate, B | 0 | Restored all twelve projects; build summary 8 warnings, 0 errors |
| 2.0.13 candidate, T-config | 0 | 22 passed, 0 failed/skipped; build summary 8 warnings, 0 errors |

Each gate ran once, without failure/retry. Both runs executed the same **13 ConfigTests + 9 StrictModeStartupTests**
(15 existing + seven new cases). No numeric discovery total was emitted.
Observed execution names below have prefix `fhir.candle.Tests.`:

```text
ConfigTests.TestParseCliBool
ConfigTests.TestParseCliBoolFalse
ConfigTests.TestParseCliBoolTrue
ConfigTests.TestParseCliErrorsDoNotInvokeAction(argument: "--port", value: "not-a-number")
ConfigTests.TestParseCliErrorsDoNotInvokeAction(argument: "--unknown-cli-option", value: null)
ConfigTests.TestParseCliErrorsDoNotInvokeAction(argument: "@fhir-candle-cli-options.rsp", value: null)
ConfigTests.TestParseCliHelpDoesNotInvokeAction(helpOption: "--help")
ConfigTests.TestParseCliHelpDoesNotInvokeAction(helpOption: "-h")
ConfigTests.TestParseCliInt
ConfigTests.TestParseCliResponseFileValueRemainsLiteral
ConfigTests.TestParseCliString
ConfigTests.TestParseCliStringArray
ConfigTests.TestParseCliValuesOverrideExternalConfiguration
StrictModeStartupTests.ComputeStrictWarnings_EmptyWhenExplicitValueAgreesWithStrict
StrictModeStartupTests.ComputeStrictWarnings_EmptyWhenNoExplicitConflict
StrictModeStartupTests.ComputeStrictWarnings_EmptyWhenStrictDisabled
StrictModeStartupTests.ComputeStrictWarnings_WarnsOnExplicitConflict
StrictModeStartupTests.StrictRule_GetSpecUrl_ReturnsPerVersionUrls
StrictModeStartupTests.StrictRule_SpecUrls_CoversEveryRuleCode
StrictModeStartupTests.TenantConfigurationResolveStrict_FlipsAllowFlags
StrictModeStartupTests.TenantConfigurationResolveStrict_IsIdempotent
StrictModeStartupTests.TenantConfigurationResolveStrict_NoOpWhenNotStrict
```

Identical name-multiset SHA-256: `e3a49811d8cee232a40730820d11ab1a4134def1e98dfe09c57c5aec95429f72`
(full names, ordinal-sorted, LF-joined, no terminal LF, UTF-8 without BOM).

## Fresh all-TFM graph and diagnostic comparison

Read all twelve `obj/project.assets.json` files after B: **36 graphs**.
Host/Common/Tests, three stores, four UI projects carry CLI (30); Performance/Probe remain CLI-free (six).

| Targets | CLI before → after | Binder in Host/Tests | Selected compile/runtime assets |
|---|---|---|---|
| net10.0, net9.0, net8.0, each inspected | 2.0.9 → 2.0.13 | 2.0.0-beta5.25306.1 unchanged | `lib/net8.0/System.CommandLine.dll`; binder `lib/net8.0/System.CommandLine.NamingConventionBinder.dll`; related `.pdb;.xml` unchanged |

CLI keeps 13 resource locales: cs, de, es, fr, it, ja, ko, pl, pt-BR, ru, tr,
zh-Hans, zh-Hant. No new native/RID asset or known supported-OS constraint appeared.

**Every other package's resolved version, dependency and selected-asset node was identical**,
including binder/TestHost. Before/after SHA-256:
`22dbccf460ce84679605a7756ef29924ad25f7749d6a4452df19dbc621d1f0c7`.
Normalization: map `<repository-relative csproj>|<TFM>` to `type=package` target nodes
excluding `System.CommandLine/`; recursively sort JSON keys, separators `,`/`:`, UTF-8, no terminal newline.
Rechecked after manual runs. Enabled feeds remain nuget.org/Visual Studio Offline Packages;
candidate `.nupkg.metadata` records nuget.org acquisition.

No introduced compiler/analyzer/NuGet diagnostic. Eight-warning summaries are
a subset/repetition of the parent's 24-warning all-target baseline, not remediation.
Host/Tests still resolve MailKit 4.17.0 → MimeKit 4.17.0 → Pkcs 10.0.0 (net10.0),
8.0.1 (net9.0/net8.0). NU1902
([GHSA-2fmv-rv2c-59x7](https://github.com/advisories/GHSA-2fmv-rv2c-59x7)) and NU1903
([GHSA-846f-f764-76mx](https://github.com/advisories/GHSA-846f-f764-76mx)) messages and affected graphs match; none suppressed/repaired.

## Actual isolated help, error, and startup

Used the [isolated startup procedure](../sdk6-compatibility.md#isolated-startup)
with previously absent `$env:TEMP\fhir-candle-sdk6-1015-02` and empty `source`/`cache`.
No ambient Candle keys/nonempty notification credentials were found. No FHIR
packages/examples were requested; official FHIR registries were disabled.

Each command below ran once on **2.0.9 and 2.0.13**, using the documented
default Debug run form (not the Release test binaries); all four exits were **0**:

```powershell
dotnet run --project src\fhir-candle\fhir-candle.csproj --framework net10.0 -- --r4 r4 --r4b r4b --r5 r5 --fhir-source "$env:TEMP\fhir-candle-sdk6-1015-02\source" --fhir-package-cache "$env:TEMP\fhir-candle-sdk6-1015-02\cache" --use-official-registries false --help
dotnet run --project src\fhir-candle\fhir-candle.csproj --framework net10.0 -- --r4 r4 --r4b r4b --r5 r5 --fhir-source "$env:TEMP\fhir-candle-sdk6-1015-02\source" --fhir-package-cache "$env:TEMP\fhir-candle-sdk6-1015-02\cache" --use-official-registries false --port not-a-number
```

Help/aliases were unchanged; neither invocation started a server. Error retained:
`Cannot parse argument 'not-a-number' for option '--port' as expected type 'System.Int32'.` plus usage.
Parser invocation returns 1, but **Main discards it and returns `_retVal` (0 here)**; not repaired.

Candidate startup, once, with process-local synthetic environment values:

```powershell
$env:CandleListen_Port = '5827'
$env:Listen_Port = '5828'
dotnet run --project src\fhir-candle\fhir-candle.csproj --framework net10.0 -- --r4 r4 --r4b r4b --r5 r5 --fhir-source "$env:TEMP\fhir-candle-sdk6-1015-02\source" --fhir-package-cache "$env:TEMP\fhir-candle-sdk6-1015-02\cache" --use-official-registries false --port 5826 --url http://localhost:5826 --open-browser false
```

Observed r4/r4b/r5, isolated source/cache, each service start once, listening on `http://[::]:5826`.
CLI overrode both environment ports; no listeners on 5827/5828; inputs stayed empty.
Stopped owned session `phase2-candidate-startup` (PID 112192); verified PID/listeners gone.
No graceful-stop or normal process-exit result is claimed.

First-key creation emitted DataProtection `XmlKeyManager[35]`, “No XML encryptor configured.”
Source/graph attribution: predecessor `PersistKeysToFileSystem` already lacks an encryptor;
framework/runtime/non-CLI dependencies are unchanged. This inherited host condition
was not suppressed/fixed; no predecessor first-start replay or key inspection is claimed.

Limitations: no candidate net9/net8 execution or full-suite result is inferred from restore.
Linux/macOS remain pending post-pass, not blockers. Browser/RI, packaged-tool, and
graceful lifecycle checks are not claimed; no UI/hosting implementation changed.

Read-only lookup: one attempt, exit 1 (missing `sdk6-browser-checks.md`); read authoritative `sdk6-compatibility.md` instead.
