# Microsoft.Extensions.Configuration.Binder — 2026-10-16

**Phase 4, attempt 1: scoped Windows gates passed; parent acceptance/commit pending.**

## Identity and preserved contract

Predecessor: **`863d6f496741f6e7c5bc0984160b89dcbf379037`**, the accepted Configuration-core checkpoint. Initial worktree/index were clean. Reused its unchanged B/T-config baseline (99/99), without re-execution.
Only Binder **10.0.9 → 10.0.13** changed in `src\fhir-candle\fhir-candle.csproj` and `src\FhirStore.Common\FhirCandle.Common.csproj`; all other pins remain unchanged.
`CandleConfig.cs`, `ConfigTests.cs`, options and host behavior are unchanged. Existing tests cover int/bool/string CLI/external binding, raw arrays, null/empty defaults and implicit option presence; no materially missing production-model type justified a duplicate named regression.
Preserved tenant contract, independently for R4/R4B/R5/R6 properties: omitted/implicit CLI inherits ordered external names; absent/empty external arrays give `[]`; explicit names replace external values; explicit empty clears. Host default-tenant policy is unchanged.
`ConfigTests.cs`: 24,706 bytes, SHA-256 `3e4298f736ac6f581213c83f9483c4c59f75de3b984e3b0aa11a210602fc7b2e`; every assertion retained.

Code patch: **1,637 bytes**, SHA-256 **`d0617218faa615f5e73457f1ba3ec4aabfea179835f7343d699753e6062fb68a`**. Hash raw stdout bytes, excluding this document:
```powershell
git --no-pager diff --no-ext-diff --binary 863d6f496741f6e7c5bc0984160b89dcbf379037 -- src/fhir-candle/fhir-candle.csproj src/FhirStore.Common/FhirCandle.Common.csproj src/FhirStore.Common/Configuration/CandleConfig.cs src/fhir-candle.Tests/ConfigTests.cs
```

## Selection, supplier and prerequisite

- Revalidated the [version index](https://api.nuget.org/v3-flatcontainer/microsoft.extensions.configuration.binder/index.json) and [registration](https://api.nuget.org/v3/registration5-gz-semver2/microsoft.extensions.configuration.binder/10.0.13.json): **10.0.13** remains newest stable/listed, published 2026-10-13.
- [Original nuspec](https://api.nuget.org/v3-flatcontainer/microsoft.extensions.configuration.binder/10.0.9/microsoft.extensions.configuration.binder.nuspec) / [candidate nuspec](https://api.nuget.org/v3-flatcontainer/microsoft.extensions.configuration.binder/10.0.13/microsoft.extensions.configuration.binder.nuspec): Configuration and Configuration.Abstractions minima **[10.0.9, ) → [10.0.13, )** on net8/net9/net10, not equality constraints.
- Read [supplier October servicing notes](https://raw.githubusercontent.com/dotnet/core/main/release-notes/10.0/10.0.13/10.0.13.md), which include Binder, and both in-memory package archives/READMEs. Real DLL/XML assets exist for all three TFMs; buildTransitive targets are unchanged. The optional binding generator remains disabled; reflection-based `Get<CandleConfig>` and `[ConfigurationKeyName]` mappings remain.
- Re-read core/Abstractions/Primitives 10.0.13 and retained EnvironmentVariables/Json/UserSecrets 10.0.9 nuspecs at the same flat-container source. Retained providers impose minima, no upper bound or co-update. Core/Abstractions already supply Primitives 10.0.13.
- Reverse Binder constraints remain compatible: Logging.Configuration minima 10.0.0/9.0.0/8.0.0, Options.ConfigurationExtensions 10.0.9/9.0.17/8.0.0, OTLP exporter 1.16.0's net8 minimum 8.0.2; supplier nuspecs impose no upper bounds. No newly required direct unit was found.
- Phase 32 supplies core **10.0.13 in 26 graphs**, Abstractions/Primitives **10.0.13 in 25**. The remaining affected host/net10 dependency closure is framework-pruned, not missing a prerequisite. Inventory remains **36 direct IDs / 46 declarations / seven files**, exactly two Binder pins.

## Fresh dependency and asset evidence

Read twelve assets via `git ls-files '*.csproj'`, before editing and after B/T/startup: **36 TFM graphs / 946 package nodes**. B checked restore and reported all projects up-to-date; all twelve current assets contain the candidate graph.
Normalization: map `<repository-relative csproj>|<TFM>` to package-only target nodes; recursively sorted compact JSON, UTF-8, no terminal LF.
Baseline: **386,127 bytes**, SHA-256 **`b795db5dabb2b641aa7d4a14cc87db964d9a883afbac5667fc9f5f0f46df9e24`**.
Candidate after B and T/startup: **386,203 bytes** (+76), SHA-256 **`8f6e95f37bdf332a79baa1d6256d28222ee15b9fa72b10b8e0ff92759d653a8d`**.
Only **26 Binder nodes** advance; **25 × two dependency minima** advance. **920 other nodes identical**, zero added/removed IDs. Inverse Binder-only version/minimum substitution exactly reproduces the pre-edit baseline hash.

| Graphs | Binder compile/runtime and build selection |
|-|-|
| Host net10 (1) | 10.0.13 `lib/net10.0/_._`, build `buildTransitive/netstandard2.0/_._`; dependencies pruned |
| Common, three stores, Tests net10 (5) | Real matching `lib/net10.0/Microsoft.Extensions.Configuration.Binder.dll` plus XML |
| Ten non-performance projects, each of net9/net8 (20) | Real matching net9/net8 DLL/XML |
| Four UI net10; both performance projects/all TFMs (10) | No Binder node; unchanged |

The 25 unpruned nodes retain `buildTransitive/netstandard2.0/Microsoft.Extensions.Configuration.Binder.targets`. Asset paths/analyzer availability are unchanged. Host prune ranges remain `(,10.0.32767]`, `(,9.0.32767]`, `(,8.0.32767]`; only net10 prunes Binder 10.x.
Tests' Release/net10 deps select Binder 10.0.13 (file version 10.0.1326.50504); host Debug/net10 deps omit it. Startup uses the shared framework, not a 10.0.13 package DLL. Feeds remain nuget.org and Visual Studio Offline Packages.

## Actual Windows commands, cases and diagnostics

Authority: [AGENTS.md](../../../AGENTS.md), [related-filter documentation](../building-and-testing.md), phase-4 B/T-config/V-accept. Windows **10.0.26200 x64**, SDK **10.0.400**, .NET/ASP.NET Core **10.0.11**, xUnit VSTest adapter **3.1.5+1b188a7b0a**.
```powershell
dotnet build --configuration Release --framework net10.0
dotnet test --configuration Release --framework net10.0 --no-restore --verbosity normal --filter "FullyQualifiedName~fhir.candle.Tests.ConfigTests|FullyQualifiedName~fhir.candle.Tests.StrictModeStartupTests"
```

| Gate, each run once | Exit | Result |
|-|-|-|
| B | 0 | Build succeeded; 8 existing warnings, 0 errors; elapsed 3.78 s |
| T-config | 0 | **99/99 passed**, 0 failed/skipped; 8 existing warnings, 0 errors; test time 1.2017 s |

Normal logs were captured in memory; no diagnostic-hiding flags or files. Warnings match baseline: Host/Tests MailKit 4.17.0 → MimeKit 4.17.0 → Pkcs **10.0.0** (net10), **8.0.1** (net9/net8), NU1902 [GHSA-2fmv-rv2c-59x7](https://github.com/advisories/GHSA-2fmv-rv2c-59x7) / NU1903 [GHSA-846f-f764-76mx](https://github.com/advisories/GHSA-846f-f764-76mx). **No new warning, suppression or weakened assertion.**

| Executed inventory (`fhir.candle.Tests.` prefix) | Passed occurrences |
|-|-|
| ConfigTests: `ExternalConfigurationBindsTenantArraysWithoutCli` | 4 |
| `TenantOptionParsingDistinguishesOmittedAndExplicitEmpty` | 20 |
| `ExternalTenantArraysApplyWhenCliIsAbsent` | 12 |
| `ExplicitEmptyTenantCliOverridesExternalConfiguration` / `ExplicitTenantCliValuesOverrideExternalConfiguration` | 4 + 4 |
| `AbsentTenantConfigurationUsesEmptyDefaults` / `TenantCliOverridesOnlyItsOwnVersion` | 24 + 8 |
| `TestParseCliInt`, `TestParseCliString`, `TestParseCliBool`, `TestParseCliBoolTrue`, `TestParseCliBoolFalse`, `TestParseCliStringArray` | 6 |
| `TestParseCliResponseFileValueRemainsLiteral`, `ExternalConfigurationAppliesWhenCliIsAbsent`, `TestParseCliValuesOverrideExternalConfiguration` | 3 |
| `TestParseCliHelpDoesNotInvokeAction` / `TestParseCliErrorsDoNotInvokeAction` | 2 + 3 |
| StrictModeStartupTests: four `ComputeStrictWarnings_*`, three `TenantConfigurationResolveStrict_*`, two `StrictRule_*` facts | 9 |

**90 Config + nine Strict = 99**, all 23 original + 76 repair occurrences. Full names including parameters, ordinal-sorted, LF-joined without terminal LF, UTF-8 SHA-256 **`aa0f16ebb429ff54513a060f3784571110bc1af3f468466265363b7cf762cc51`** exactly match baseline; no missing/added/duplicate rows. No independent numeric discovery total was emitted.

## Actual isolated production precedence and cleanup

Used the [documented isolated Debug/net10 startup form](../sdk6-compatibility.md#isolated-startup). Fresh owned TEMP `$root` (recorded privately), empty `source`/`cache`, no packages/real data/accounts. Child-only environment: `Tenants_R4__0=external-r4`, `Tenants_R4B__0=external-r4b`, `Tenants_R5__0=external-r5`, `Listen_Port=5827`; unrelated configuration screened only in that child copy.
```powershell
dotnet run --project src\fhir-candle\fhir-candle.csproj --framework net10.0 -- --r5 cli-r5 --fhir-source "$root\source" --fhir-package-cache "$root\cache" --use-official-registries false --open-browser false --port 5826
```
Ready in **5.872 s**, listening at `http://[::]:5826`, proving CLI port precedence. Loopback GET `/fhir/{tenant}/metadata`, `Accept: application/fhir+json`: **external-r4 200/4.0.1**, **external-r4b 200/4.3.0**, **cli-r5 200/5.0.0**, with canonical `http://localhost:5826/fhir/{tenant}/CapabilityStatement/metadata`.
`external-r5` returned expected **404** and `GetMetadata <<< no tenant` failure-level log: CLI replacement, not an unexplained startup error. Four service-start messages appeared once; source/cache paths confirmed and stayed empty. No packages loaded; SMTP/Zulip absent. Only the four existing host Pkcs diagnostics repeated across restore/build.
Cleanup: **`Stop-Process -Id 53576 -Force`**; owned dotnet-run PID **41560** then exited **4294967295 (-1)**. Wrapper exit **0**; both PIDs absent, owned root removed, parent environment unchanged, port 5826 free (successful socket bind). This was forced termination, **not graceful/singleton lifecycle proof**.

## Failures, rollback and limits

No build/test/startup-check failure or gate retry. One pre-edit metadata probe failed to decode gzip registration; explicit decompression corrected it. A cleanup TCP query emitted “no matching objects” for the absent listener; socket verification independently passed. No tooling installed, output redirected to files, stage, commit, publication, plan or other pin changed.
Reverting this unit's two Binder pins retains the phase-3 repair, Configuration 10.0.13 and CLI 2.0.13. **Do not roll back core below 10.0.13 while keeping Binder 10.0.13**: reconsider dependent units first, restore through B and repeat graph/T-config/startup gates; unlocked resolution is not an arbitrary rollback guarantee.
Windows net9/net8 execution and final all-TFM/full-suite verification remain pending. Linux/macOS validation is pending after the completed Windows pass. No browser/RI, R6 product hosting, graceful lifecycle or final SDK/cross-platform acceptance is claimed.
