# Microsoft.Extensions.Configuration.EnvironmentVariables — 2026-10-16

**Phase 5: scoped Windows gates passed; parent review/commit pending.** One candidate trial; B/T passed once; startup passed on its second check after correcting the probe, not application code.

## Unit, identity and preserved contract

Predecessor: **`ea54a11db694e77c2952d51c5b472270d0999e8e`**. Worktree/index initially clean; reused its accepted 99/99 T-config baseline without rerunning existing cases before the pin change.
Single-ID unit: only the host's EnvironmentVariables pin **10.0.9 → 10.0.13**. Configuration/Binder remain 10.0.13; every other declaration is unchanged. `Program.cs` needs no adaptation.
Added four `EnvironmentProvidersPreserveExistingPrecedence` rows: JSON < `Candle`-prefixed environment < unprefixed environment < explicit CLI. Each row uses a fresh GUID section, two unique process keys, synthetic in-memory JSON and a fresh parser; `finally` restores each exact prior environment value, including absence. Binding only that section excludes real settings.
All existing tenant-array, omitted/implicit/explicit-empty option, scalar, help/error and response-file assertions remain unchanged. Host default-tenant policy and production provider order are unchanged.
Raw code patch: **3,543 bytes**, SHA-256 **`f05c5f674c78195320b01115f5efa88bf2d1fbbbe4f0f710d2b952838e9b6914`**; hash raw stdout bytes, excluding this document:
```powershell
git --no-pager diff --no-ext-diff --binary ea54a11db694e77c2952d51c5b472270d0999e8e -- src/fhir-candle/fhir-candle.csproj src/fhir-candle/Program.cs src/fhir-candle.Tests/ConfigTests.cs
```
`ConfigTests.cs`: 55 added lines, 26,976 bytes, SHA-256 `18e384d044bc009bb5c0808fd9ba4a4eb53a1ccb4cc33fae05211de4ac0d5ec4`.

## Supplier, minima and selected boundary

- Revalidated the [version index](https://api.nuget.org/v3-flatcontainer/microsoft.extensions.configuration.environmentvariables/index.json) and [registration](https://api.nuget.org/v3/registration5-gz-semver2/microsoft.extensions.configuration.environmentvariables/10.0.13.json): **10.0.13** remains newest stable/listed, published 2026-10-13.
- [Original nuspec](https://api.nuget.org/v3-flatcontainer/microsoft.extensions.configuration.environmentvariables/10.0.9/microsoft.extensions.configuration.environmentvariables.nuspec) / [candidate nuspec](https://api.nuget.org/v3-flatcontainer/microsoft.extensions.configuration.environmentvariables/10.0.13/microsoft.extensions.configuration.environmentvariables.nuspec): Configuration and Configuration.Abstractions minima **[10.0.9, ) → [10.0.13, )** on net8/net9/net10; these are not equality constraints.
- Re-read Configuration, Binder, Abstractions and Primitives 10.0.13 nuspecs. The accepted prerequisite supplies core/Binder in **26 graphs each**, Abstractions/Primitives in **25 each**; the host/net10 closure is framework-pruned, not missing its prerequisite.
- Retained [Json](https://api.nuget.org/v3-flatcontainer/microsoft.extensions.configuration.json/10.0.9/microsoft.extensions.configuration.json.nuspec), [UserSecrets](https://api.nuget.org/v3-flatcontainer/microsoft.extensions.configuration.usersecrets/10.0.9/microsoft.extensions.configuration.usersecrets.nuspec) and [FileExtensions](https://api.nuget.org/v3-flatcontainer/microsoft.extensions.configuration.fileextensions/10.0.9/microsoft.extensions.configuration.fileextensions.nuspec) impose compatible minima, no upper bounds/co-update. No package-node reverse constraint requires another EnvironmentVariables pin.
- Reviewed [October servicing notes](https://raw.githubusercontent.com/dotnet/core/main/release-notes/10.0/10.0.13/10.0.13.md), including this package. Provider, source and extension source files are byte-identical between nuspec commits `901ca941248413c79832d2fdbd709da0c4386353` and `4099f83977f4d29604a9aeb971fb0a7bc1d5f699` in the [supplier source](https://github.com/dotnet/dotnet/tree/4099f83977f4d29604a9aeb971fb0a7bc1d5f699/src/runtime/src/libraries/Microsoft.Extensions.Configuration.EnvironmentVariables/src).
- Both package archives, inspected in memory, contain real DLL/XML assets for all three TFMs and unchanged buildTransitive paths. Candidate archive SHA-256: `8490ab1984fec0ea1149c9f49c35891dcf0d39b5481e41f68644c7e96dc6f17c`. No tool/runtime installation or additional direct unit was required.

## Fresh graphs, assets and pruning

B restored all twelve tracked C# projects. Inspected their **36 TFM graphs / 946 package nodes** before B, after B/T and after both startups. Direct inventory remains **36 IDs / 46 declarations / seven files**, with one EnvironmentVariables declaration.
Normalization: map `<repository-relative csproj>|<TFM>` to full package-only target nodes; recursively sorted compact JSON, UTF-8, no terminal LF.
Baseline: **386,203 bytes**, SHA-256 **`8f6e95f37bdf332a79baa1d6256d28222ee15b9fa72b10b8e0ff92759d653a8d`**.
Candidate: **386,216 bytes**, SHA-256 **`fcb8930daea43c64a0e1cd061d4becd8cd74951e8a06854679cdb85b00698a79`**; unchanged through final inspection.
Full-node comparison: exactly **five provider versions** advance; **four × two dependency minima** advance. **941 other nodes identical**, zero added/removed IDs; all compile/runtime/build fields and retained versions unchanged. Verified 23 distinct selected non-placeholder asset paths across the relevant retained/candidate closure exist.

| Provider graph inventory | Selected compile/runtime assets |
|-|-|
| Host net10 (1) | 10.0.13 `lib/net10.0/_._` with related XML metadata; dependencies pruned |
| Host and Tests, each net9/net8 (4) | 10.0.13 `lib/{net9.0,net8.0}/Microsoft.Extensions.Configuration.EnvironmentVariables.dll` plus XML |
| Remaining graphs (31), including Tests/net10 | No provider package node; unchanged |

All five nodes retain build selection `buildTransitive/net8.0/_._`. Host prune ranges remain `(,10.0.32767]`, `(,9.0.32767]`, `(,8.0.32767]`; only net10 prunes this 10.x provider.
Host Release/Debug and Tests Release net10 deps omit the provider package. Execution uses installed ASP.NET Core **10.0.11**, provider file version **10.0.1126.37416**, not the 10.0.13 package DLL. Lower-TFM package execution remains pending.

| Project directories under `src` (multiplicity) | Package nodes per project: net10 / net9 / net8 |
|-|-|
| `FhirCandle.Performance`, `FhirCandle.Performance.Probe` (2) | 0 / 0 / 0 |
| `FhirCandle.Ui.Common` (1) | 9 / 20 / 21 |
| `FhirCandle.Ui.R4`, `.R4B`, `.R5` (3) | 15 / 25 / 26 |
| `FhirStore.Common` (1) | 15 / 16 / 17 |
| `FhirStore.R4`, `.R4B`, `.R5` (3) | 23 / 23 / 24 |
| `fhir-candle.Tests` (1) | 85 / 94 / 95 |
| `fhir-candle` (1) | 45 / 60 / 61 |

Feeds remain nuget.org and Visual Studio Offline Packages. Assets diagnostics contain only the existing NU1902/NU1903 Pkcs warnings.

## Exact Windows commands and case inventories

Authority: [AGENTS.md](../../../AGENTS.md), [related-filter documentation](../building-and-testing.md), phase-5 B/T-config/V-accept. Windows **10.0.26200 x64**, SDK **10.0.400**, .NET/ASP.NET Core **10.0.11**, xUnit VSTest adapter **3.1.5+1b188a7b0a**.
```powershell
dotnet test --configuration Release --framework net10.0 --no-restore --verbosity normal --filter "FullyQualifiedName~fhir.candle.Tests.ConfigTests.EnvironmentProvidersPreserveExistingPrecedence"
dotnet build --configuration Release --framework net10.0
dotnet test --configuration Release --framework net10.0 --no-restore --verbosity normal --filter "FullyQualifiedName~fhir.candle.Tests.ConfigTests|FullyQualifiedName~fhir.candle.Tests.StrictModeStartupTests"
```
| Gate (each once, in that order) | Exit | Result |
|-|-|-|
| New rows with predecessor pins | 0 | **4/4 passed**, 0 failed/skipped; test time 1.1943 s |
| Candidate B | 0 | Build succeeded; **8 existing warnings / 0 errors**; elapsed 4.93 s |
| Candidate full T-config | 0 | **103/103 passed**, 0 failed/skipped; test time 1.2638 s |

Both test builds also reported eight existing warnings and zero errors. Normal stdout/stderr was captured in memory, without diagnostic-hiding flags or authored log files. MailKit 4.17.0 → MimeKit 4.17.0 → Pkcs **10.0.0** (net10), **8.0.1** (net9/net8) is unchanged: NU1902 [GHSA-2fmv-rv2c-59x7](https://github.com/advisories/GHSA-2fmv-rv2c-59x7), NU1903 [GHSA-846f-f764-76mx](https://github.com/advisories/GHSA-846f-f764-76mx). No new compiler/analyzer warning, suppression or weakened assertion.

| Executed inventory (`fhir.candle.Tests.` prefix) | Passed occurrences |
|-|-|
| ConfigTests: `ExternalConfigurationBindsTenantArraysWithoutCli` | 4 |
| `TenantOptionParsingDistinguishesOmittedAndExplicitEmpty` / `ExternalTenantArraysApplyWhenCliIsAbsent` | 20 + 12 |
| `ExplicitEmptyTenantCliOverridesExternalConfiguration` / `ExplicitTenantCliValuesOverrideExternalConfiguration` | 4 + 4 |
| `AbsentTenantConfigurationUsesEmptyDefaults` / `TenantCliOverridesOnlyItsOwnVersion` | 24 + 8 |
| `TestParseCliInt`, `TestParseCliString`, `TestParseCliBool`, `TestParseCliBoolTrue`, `TestParseCliBoolFalse`, `TestParseCliStringArray` | 6 |
| `TestParseCliResponseFileValueRemainsLiteral`, `ExternalConfigurationAppliesWhenCliIsAbsent`, `TestParseCliValuesOverrideExternalConfiguration` | 3 |
| `TestParseCliHelpDoesNotInvokeAction` / `TestParseCliErrorsDoNotInvokeAction` | 2 + 3 |
| New `EnvironmentProvidersPreserveExistingPrecedence` | 4 |
| StrictModeStartupTests: four `ComputeStrictWarnings_*`, three `TenantConfigurationResolveStrict_*`, two `StrictRule_*` | 9 |

New parameter rows `(prefixedPort, unprefixedPort, cliPort → expectedPort)`: `(null,null,null → 8090)`, `("8091",null,null → 8091)`, `("8091","8092",null → 8092)`, `("8091","8092","8093" → 8093)`; all passed pre-pin and candidate.
Full **94 Config + nine Strict = 103** executed occurrences, no duplicates/missing rows. Full names including parameters, ordinal-sorted, LF-joined without terminal LF, UTF-8 SHA-256: **`b583d4fbe19a9c74ee39cc9ca66ca64ad7e6c1dcea0d56506c0fcd41950807e1`**. No separate numeric discovery total was emitted.
Retained 99-row hash **`aa0f16ebb429ff54513a060f3784571110bc1af3f468466265363b7cf762cc51`** exactly matches the predecessor; new four-row hash **`5fba21a6aa62c221cfa84c316e01f2571dd60b6908101b0088dc5989d0689f26`** exactly matches the pre-pin run.

## Isolated actual startup, failure and cleanup

Used the [sanctioned Debug/net10 startup form](../sdk6-compatibility.md#isolated-startup), twice with separate fresh owned TEMP roots, empty `source`/`cache`, official registries disabled and no packages/real resources/accounts. Paths recorded privately; no real configuration values copied into fixtures/evidence.
Child-only environment: `CandleTenants_R4__0=prefixed-r4`, `Tenants_R4__0=external-r4`, `CandleTenants_R4B__0=prefixed-r4b`, `CandleTenants_R5__0=prefixed-r5`, `Tenants_R5__0=external-r5`, `CandleListen_Port=5828`, `Listen_Port=5827`, `DOTNET_ENVIRONMENT=Production`. Unrelated configuration was screened only in the child copy.
```powershell
dotnet run --project src\fhir-candle\fhir-candle.csproj --framework net10.0 -- --r5 cli-r5 --fhir-source "$root\source" --fhir-package-cache "$root\cache" --use-official-registries false --open-browser false --port 5826
```
**Startup check 1 failed (wrapper exit 1):** all five HTTP expectations passed, but the probe expected `GetMetadata <<< no tenant:` instead of the actual route-specific `GetMetadata <<< no tenant at {tenant}!`. Three `DetermineInteraction ... cannot be parsed!` diagnostics also appeared because requests used `127.0.0.1` against canonical `localhost`. Inspected `FhirController.GetMetadata` and `FhirRequestContext` URL matching; no source edit.
**Startup check 2 passed (wrapper exit 0):** same patch/command, HTTP host corrected to documented `localhost`, exact per-route missing-tenant assertions, and additional assertions rejecting URL-parse/unexpected failure diagnostics. Ready in **7.815 s**, Production environment, listener `http://[::]:5826` owned by the child, proving CLI port precedence.
GET `http://localhost:5826/fhir/{tenant}/metadata`, `Accept: application/fhir+json`: **external-r4 → 200/4.0.1**, **prefixed-r4b → 200/4.3.0**, **cli-r5 → 200/5.0.0**; each is a CapabilityStatement with canonical `http://localhost:5826/fhir/{tenant}/CapabilityStatement/metadata`.
`prefixed-r4` and `external-r5` returned expected **404**, with exactly their two failure-level missing-tenant logs: unprefixed replacement and CLI replacement. Retry had no URL-parse/unexpected failure diagnostic. Four service-start messages each appeared once; SMTP/Zulip absent, source/cache confirmed empty, no packages loaded; only four existing host Pkcs diagnostics repeated across restore/build.
Cleanup used `Stop-Process -Id <PID> -Force` separately for each owned child: check 1 **3040, 5692, 93940, 85900, 137344**; check 2 **142096, 23976, 37000, 90516, 60520**. Dotnet-run parents **67136 / 62636** exited **4294967295 (-1)** after forced termination.
Both checks verified all owned PIDs absent, their TEMP root removed, parent environment unchanged and port 5826 bindable; final reinspection confirmed cleanup. This is **not graceful shutdown or singleton lifecycle proof**. No build/test rerun was needed for the unchanged source identity.

## Other probe corrections, rollback and limits

Read-only probes corrected two wrong file paths (`docs\sdk6-compatibility.md` and the guessed `Models\CandleConfig.cs`) using tracked paths. A namespace-blind XML inventory undercounted 32/42/6; namespace-aware inspection established 36/46/7. A conservative isolation preflight rejected two nonempty appsettings entries; key/type-only review identified numeric `Listen_Port` (CLI overridden) and `SMTP_Port` (no host/account), resolving the check without copying values or editing settings.
Prerequisites: the accepted tenant-fallback repair, Configuration-core 10.0.13 and Binder checkpoint remain. Revert the complete EnvironmentVariables unit, not unrelated pins; do **not** roll core below 10.0.13 while retaining this provider/Binder. Restore through B and repeat graph/T-config/startup checks; unlocked resolution is not an arbitrary rollback guarantee.
Windows net9/net8 candidate execution and final all-TFM/full-suite gates remain pending; Linux/macOS validation remains pending after the completed Windows pass. No browser/RI, R6 product hosting, graceful lifecycle, 10.0.13 provider-DLL runtime, or final cross-platform acceptance is claimed.
No plan/skills/other source or pin edits, assertion weakening, tool installs, staging, commits, publication or additional agents/stages. Source patch and graph hashes remained unchanged after the startup retry.
