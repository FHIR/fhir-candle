# Microsoft.Extensions.Configuration — 2026-10-15

**Disposition: SCOPE_EXCEEDED at the pre-pin regression; no upgrade trialed or accepted.**
Both direct Configuration pins remain **10.0.9**. The proposed CLI-absent
test's external-array expectation is not baseline-compatible; the original
22 focused cases still pass. This is not evidence that 10.0.13 is incompatible.

## Evaluated boundary and prerequisites

- Revalidated the [version index](https://api.nuget.org/v3-flatcontainer/microsoft.extensions.configuration/index.json) and [10.0.13 registration](https://api.nuget.org/v3/registration5-gz-semver2/microsoft.extensions.configuration/10.0.13.json): **10.0.13** is still the highest stable, listed, published 2026-10-13.
- The [current nuspec](https://api.nuget.org/v3-flatcontainer/microsoft.extensions.configuration/10.0.9/microsoft.extensions.configuration.nuspec) and [candidate nuspec](https://api.nuget.org/v3-flatcontainer/microsoft.extensions.configuration/10.0.13/microsoft.extensions.configuration.nuspec) require Configuration.Abstractions and Primitives **[10.0.9, ) → [10.0.13, )** on each of net8/net9/net10. Abstractions requires the corresponding Primitives minimum; Primitives has no dependencies on those targets.
- Retain Binder, EnvironmentVariables, Json and UserSecrets **10.0.9**, including both Binder declarations. Read their 10.0.9/10.0.13 nuspecs at the same flat-container endpoint. Binder/EnvironmentVariables require Configuration and Abstractions **[10.0.9, )**; Json additionally requires FileExtensions/FileProviders.Abstractions and, on net8/net9, System.Text.Json **[10.0.9, )**. UserSecrets requires Abstractions/Json/FileProviders.Abstractions/Physical **[10.0.9, )**. None imposes an upper bound or forces a co-update.
- The supplier's [.NET October servicing notes](https://raw.githubusercontent.com/dotnet/core/main/release-notes/10.0/10.0.13/10.0.13.md) include Configuration/Abstractions/Primitives 10.0.13: an eligible servicing trial, not behavioral proof or a runtime installation requirement inferred from the SDK download table.
- Future Binder/EnvironmentVariables/Json **10.0.13** require Configuration **[10.0.13, )**; UserSecrets 10.0.13 requires Json **[10.0.13, )**. They remain separate pending units. If subsequently accepted, those minima prevent arbitrary rollback of the earlier core upgrade: reconsider dependent pins, restore through B, and repeat graph/behavior checks.
- Prerequisites remain the identified predecessor, empty index/owned-path gates, existing Windows SDK/runtimes and enabled feeds. No higher candidate, additional direct occurrence, required co-movement or new tooling was found.

## Source identity and failed baseline-compatible-test attempt

Predecessor: **`0cbb7656447866e4990de8431cd297e8a098b2dc`**, including CLI 2.0.13.
Initial worktree/index were clean. Only the new test and this record changed.
The test is left **unaccepted and failing for parent review**, not a green regression.

Exact code/project/test patch: **1,591 bytes**, SHA-256
**`067dc3bcfa6378cc9fb6a72d15dc3353a9451ee189a607648aa04853ca201ddc`**.
Hash the raw stdout bytes of this command, without newline/encoding conversion;
the evidence document is excluded, so the identity is noncircular:

```powershell
git --no-pager diff --no-ext-diff --binary 0cbb7656447866e4990de8431cd297e8a098b2dc -- src/fhir-candle/fhir-candle.csproj src/FhirStore.Common/FhirCandle.Common.csproj src/fhir-candle.Tests/ConfigTests.cs
```

`ExternalConfigurationAppliesWhenCliIsAbsent` uses only synthetic in-memory
keys, an empty argument array and the real `CandleConfig` constructor.
ListenPort=9090, PublicUrl=`http://external.invalid/` and OpenBrowser=true
assertions passed; `TenantsR4` expected `["external-primary", "external-secondary"]`
but was **`[]`**, failing at ConfigTests.cs:211 before any pin edit.
The unchanged constructor selects
`pr.GetValue(opt.TenantsR4) ?? envConfig?.TenantsR4 ?? []` (CandleConfig.cs:544).
Making the proposed external-array expectation hold would change production
fallback behavior outside this dispatch. Parent must resolve that contract
premise/scope; no assertion was weakened or production code repaired.

## Exact Windows gates and observed cases

Authority: [AGENTS.md](../../../AGENTS.md), [related filters](../building-and-testing.md),
phase 3/V-accept. Supplied Windows **10.0.26200 x64**, SDK **10.0.400**;
the runner observed **64-bit .NET 10.0.11**, xUnit VSTest adapter **3.1.5**.
Installed 9.0.19/8.0.30 runtimes are availability, not execution evidence here.

```powershell
dotnet build --configuration Release --framework net10.0
dotnet test --configuration Release --framework net10.0 --no-restore --verbosity normal --filter "FullyQualifiedName~fhir.candle.Tests.ConfigTests|FullyQualifiedName~fhir.candle.Tests.StrictModeStartupTests"
```

| Order / identity | Exit | Actual result |
|-|-|-|
| 1. B, Configuration 10.0.9 + proposed test | 0 | All twelve projects restored; 8 warnings, 0 errors |
| 2. T-config, same identity | 1 | 23 executed: 22 passed, 1 failed, 0 skipped; 8 warnings, 0 compiler errors; VSTest returned false (MSB4181) |

Each command ran once; **zero retries**. Stopped on the diagnosed red pre-pin
gate. Candidate B/T-config, candidate graphs and V-windows-full were not run.
No numeric discovery total was emitted. Observed names have prefix `fhir.candle.Tests.`:

```text
ConfigTests.ExternalConfigurationAppliesWhenCliIsAbsent
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

**14 Config + 9 Strict cases**, only the first failed. Name-multiset SHA-256:
`e96b021df894a22ee9ee394ecc617e8058c2d02e55c01140d1013c63ef5fe90e`.
Full emitted names including parameters, ordinal-sorted, LF-joined without
terminal LF, UTF-8 without BOM. Removing only the new case reproduces the
[phase-2](system-commandline.md) 22-name hash `e3a49811d8cee232a40730820d11ab1a4134def1e98dfe09c57c5aec95429f72`;
all those cases passed, including the reused CLI-over-external case.

## Fresh graphs, diagnostics and limits

Inspected all **36 graphs** in twelve assets files after B and again after T-config.
No package changed: 946 package-node occurrences. Configuration/Abstractions/Primitives:

| Graphs | Actual retained resolution / compile and runtime selection |
|-|-|
| Host net10 | Configuration 10.0.9 placeholder `lib/net10.0/_._`; its dependencies pruned, no Abstractions/Primitives nodes |
| Common, three stores, Tests net10 | All three 10.0.9, real `lib/net10.0/<ID>.dll` (and related `.xml`) |
| Four UI net10; Performance/Probe all TFMs | No affected nodes |
| All ten non-performance projects, net9 and net8 separately | All three 10.0.9, real matching `lib/<TFM>/<ID>.dll` (and related `.xml`) |

Host net10's unchanged prune range is `(,10.0.32767]`; its Binder/providers are also placeholders. Common/Tests retain real Binder 10.0.9; Host/Tests net9/net8 retain real providers/FileExtensions 10.0.9. Enabled feeds remain nuget.org and Visual Studio Offline Packages.
Every package target node is identical before/after the failed test: **385,976 normalized bytes**, SHA-256 `91ebedf8083924c7fad156ddda99d2ddea89d7cb5a9601008195c38e06befe0c`. Map `<repository-relative csproj>|<TFM>` to all `type=package` target nodes; recursively sort JSON keys, separators `,`/`:`, UTF-8, no terminal newline. This is a predecessor comparison, not candidate closure evidence.

The 8-warning summaries match the existing NU1902/NU1903 Pkcs advisories [GHSA-2fmv-rv2c-59x7](https://github.com/advisories/GHSA-2fmv-rv2c-59x7)/[GHSA-846f-f764-76mx](https://github.com/advisories/GHSA-846f-f764-76mx): Host/Tests MailKit 4.17.0 → MimeKit 4.17.0 → Pkcs 10.0.0 (net10), 8.0.1 (net9/net8). No introduced compiler/analyzer warning or suppression; the new failed assertion remains a blocker.

Program/CandleConfig are unchanged: JSON → prefixed Candle environment → unprefixed environment → CLI selection and typed `.Get<CandleConfig>` remain intact. No new production startup/browser/lifecycle result is claimed; Strict tests are config-only. No helper was started, no real environment/credentials/data used, and no production source, plan/approach artifact, baseline README, neighboring pin, stage, commit or publication changed. Windows lower-TFM execution and Linux/macOS post-pass validation remain unperformed here; non-Windows availability did not cause this stop.

---

## 2026-10-16 — phase-3 recovery, attempt 2: diagnosis and prerequisite repair

The entire preceding record is the unchanged historical attempt-1 evidence.
This recovery implements the separately authorized tenant-fallback correction;
it is **not a Configuration package trial or an accepted phase checkpoint**.
CLI remains 2.0.13, and both Configuration declarations, Binder and providers
remain 10.0.9. No dependency declaration was edited.

Recovery inspection matched HEAD `0cbb7656447866e4990de8431cd297e8a098b2dc`,
an empty index, the retained `ConfigTests.cs` SHA-256
`3A82B8C61801D76B93320A49315A29A8536B76435384812DFE8AC80E9B98DEC9`,
the untracked evidence SHA-256
`2A753AA41C0590BD12F85B82A7113EEA83A06BEB63F6C819B34D2244057E9C8C`,
and clean `CandleConfig.cs` SHA-256
`1C0AC022538DB3F77926E92FEF9201EF5780D05195FE89374FAD7E08F91AB715`.
The known retained test was not removed, renamed, or weakened.

### Attributed user-run pre-CLI comparison

The caller supplied the user's comparison in the read-only diagnostic worktree
`C:\ai\git\fhir-candle\scratch\1015-01\cli-baseline`, at
`dc7a51a60394a1e3570d2f50ff3288dd09aad6b4` with the retained test overlaid.
The user reported a successful documented net10.0 build with eight existing
Pkcs warnings, followed by the single
`ExternalConfigurationAppliesWhenCliIsAbsent` filter: one executed, one
failed, zero skipped, with the same two-external-tenants-versus-`[]` assertion.
The supplied recovery evidence also reports read-only confirmation of
CLI 2.0.9 / Configuration 10.0.9 and the identical overlaid test-file hash.

This is **attributed user-run evidence**, not an execution by this implementer.
The full original user command text was not supplied with this dispatch.
The diagnostic worktree was neither searched nor rerun here. That comparison
establishes that the failure predates the CLI upgrade; by itself it does not
isolate raw binding, parser defaults, or production startup.

### Pre-repair layer characterization — executed in attempt 2

Authority: [AGENTS.md](../../../AGENTS.md) and the documented
[related VSTest filter form](../building-and-testing.md). Only the two new
diagnostic methods ran, before any production repair; the unchanged red
constructor test was not blindly rerun.

Exact commands from `C:\ai\git\fhir-candle`:

```powershell
dotnet --version
dotnet test src\fhir-candle.Tests\fhir-candle.Tests.csproj --configuration Release --framework net10.0 --no-restore --verbosity normal --filter "FullyQualifiedName~fhir.candle.Tests.ConfigTests.ExternalConfigurationBindsTenantArraysWithoutCli|FullyQualifiedName~fhir.candle.Tests.ConfigTests.TenantOptionParsingDistinguishesOmittedAndExplicitEmpty"
```

`dotnet --version` returned **10.0.400**, exit 0. The diagnostic test command
ran **once**, exit **0**: **24 executed, 24 passed, zero failed/skipped**;
test time 1.1241 seconds. The runner reported xUnit VSTest adapter
3.1.5+1b188a7b0a, 64-bit .NET 10.0.11 on the supplied Windows environment.
The build within that invocation succeeded with **8 warnings, 0 errors**.
All eight warnings were the already-recorded Host/Tests NU1902/NU1903 Pkcs
10.0.0 / 8.0.1 advisories; no new compiler/analyzer warning was observed.
Normal output was captured in memory and the case names, warnings and
summaries inspected; no test logger or redirected output file was requested.

Diagnostic code patch identity, excluding this document: **6,711 bytes**,
SHA-256 **`4b6561f782c21c4fe44c4e60979e5d44951db4e1fbdbfb2416d411892027b58e`**,
using the raw diff command below. At that point only diagnostic tests/helpers
had been added to the retained test file; production still had its clean
recovery hash above.

```powershell
git --no-pager diff --no-ext-diff --binary 0cbb7656447866e4990de8431cd297e8a098b2dc -- src\FhirStore.Common\Configuration\CandleConfig.cs src\fhir-candle.Tests\ConfigTests.cs
```

Actual observed method names have prefix `fhir.candle.Tests.ConfigTests.`:

| Method | Executed rows |
|-|-|
| `ExternalConfigurationBindsTenantArraysWithoutCli` | 4: `(optionName: "--r4", configurationKey: "Tenants_R4")`, `("--r4b", "Tenants_R4B")`, `("--r5", "Tenants_R5")`, `("--r6", "Tenants_R6")` |
| `TenantOptionParsingDistinguishesOmittedAndExplicitEmpty` | 20: each `optionName` in `--r4`, `--r4b`, `--r5`, `--r6`, crossed with each `scenario` in `omitted`, `explicit-empty`, `explicit-values`, `implicit-empty`, `implicit-values` |

All 24 emitted names/parameter rows matched that inventory with no missing
or duplicate row. The name-multiset SHA-256 is
`22f7b0b5d5e83210d74c16ebd54e636785b805c1307d9c053e5561861acdf2bb`
(full emitted names including parameter labels, ordinal-sorted, LF-joined
without terminal LF, UTF-8 without BOM). The runner printed discovery start
and completion but no independent numeric discovery total.

Raw `IConfiguration.Get<CandleConfig>` with synthetic indexed keys and the
constructor's binder options produced
`["external-secondary", "external-primary"]` for **each of the four properties**.
No CLI parser or selecting constructor participates in those four rows.
The independent parser observations were identical for each tenant flag:

| Input / test-only default | `GetResult(typedOption)` | `GetValue(typedOption)` |
|-|-|-|
| Omitted; no default factory | `null` | `[]`, non-null |
| Explicit flag without names | Present, `Implicit=false` | `[]`, non-null |
| Repeated explicit names | Present, `Implicit=false` | `["cli-secondary", "cli-primary", "cli-secondary"]` |
| Omitted; synthesized empty default | Present, `Implicit=true` | `[]`, non-null |
| Omitted; synthesized nonempty default | Present, `Implicit=true` | `["implicit-secondary", "implicit-primary"]` |

**Diagnosis:** raw binding is not losing these indexed tenants. The old
constructor's `GetValue(...) ?? external` selection accepts the parser's
non-null empty array even when no option result exists, masking the correctly
bound external array. Array length/nullability cannot distinguish omission
from explicit clearing, and mere result existence would misclassify the
tested implicit defaults. Default factories were installed only on fresh
test-owned command instances; production option declarations are unchanged.

### Bounded repair and authored regressions — parent gates pending

Only the four constructor tenant-array selections changed. Each now tests
`pr.GetResult(typedOption) is { Implicit: false }`: explicit input selects
the parsed array or `[]`; a null or implicit result selects that version's
external array or `[]`. This preserves explicit-empty clearing and parsed
value order/duplicates without merging, sorting, or deduplicating.
No other array/scalar selection, public API, option name/arity, response-file
policy, or `Program.RunServer` default-tenant policy changed.

The new tests use fresh `CliRootCommand` instances and synthetic in-memory
configuration. Empty external arrays use an in-memory JSON stream containing
the actual `[]` value, with a raw-binding assertion to distinguish them from
an absent configuration root. The isolation test checks all four arrays
after overriding just one; R6 remains configuration-only, not hosted support.
Every pre-existing test byte, including all assertions in
`ExternalConfigurationAppliesWhenCliIsAbsent` and the existing
scalar/bool/CLI precedence test, is preserved.

Source-authored inventory (not a post-repair execution/discovery claim):

| New method | Authored rows and coverage |
|-|-|
| `ExternalConfigurationBindsTenantArraysWithoutCli` | 4; each flag/key pair above |
| `TenantOptionParsingDistinguishesOmittedAndExplicitEmpty` | 20; four flags × five parser scenarios above |
| `ExternalTenantArraysApplyWhenCliIsAbsent` | 12; four flag/key pairs × `defaultKind` = `none`, `empty`, `values` |
| `ExplicitEmptyTenantCliOverridesExternalConfiguration` | 4; each flag/key pair, nonempty external values must not survive clearing |
| `ExplicitTenantCliValuesOverrideExternalConfiguration` | 4; each flag/key pair, repeated names retain order and duplicates and replace external values |
| `AbsentTenantConfigurationUsesEmptyDefaults` | 24; four flag/key pairs × `externalArrayIsEmpty` = false/true × `defaultKind` = `none`, `empty`, `values` |
| `TenantCliOverridesOnlyItsOwnVersion` | 8; four flags × `explicitEmpty` = false/true, with all other versions externally configured |

There are **76 added rows**, making **90 authored ConfigTests rows** including
the retained 14. Together with the unchanged nine StrictModeStartupTests
rows, the source-derived intended T-config inventory is **99**. Only the
24 pre-repair diagnostic rows above have been executed by this implementer;
the remaining rows and the repaired constructor await the parent's gates.

Repair code patch identity from the same raw diff command: **17,468 bytes**,
SHA-256 **`e15317a800fd64ba9ef360f72409d3555334f1a672fc8683e2c1ecf14e9753ef`**.
File SHA-256 values at code-batch handoff:

| Path | SHA-256 |
|-|-|
| `src\FhirStore.Common\Configuration\CandleConfig.cs` | `c85f90c3d32e81401af97d7c337aa268f53633e21b27945c5bd6a4bd6829a42a` |
| `src\fhir-candle.Tests\ConfigTests.cs` | `3e4298f736ac6f581213c83f9483c4c59f75de3b984e3b0aa11a210602fc7b2e` |

### Attempt attribution and remaining evidence

- **Attempt 1:** the historical one-case constructor failure and scope yield
  remain unchanged above. The attributed user comparison is not an agent
  retry, package-candidate result, or a third recovery attempt.
- **Attempt 2:** the diagnostic test invocation passed on its first run,
  with zero test retries. One read-only byte-preservation check initially
  exited 1 because its in-memory reconstruction appended an extra CRLF;
  correcting that check, without changing any source, reproduced the exact
  retained 10,806-byte test hash. This was a check-script error, not a
  build/test failure or evidence of changed assertions.
- Subsequent source checks reproduced that retained hash after excluding
  only newly added blocks/helpers. `git --no-pager diff --check` passed.
  Both source files retain CRLF/BOM conventions. The historical evidence
  remains the original 9,828-byte prefix, not a rewritten account.
- Per the caller's handoff, the post-repair application build, full T-config
  execution, fresh all-TFM graph comparison, and isolated supported-tenant
  startup/explicit-empty smoke are **pending parent execution and append-only
  evidence completion**. Unedited declarations alone are not a fresh
  resolved-graph result. No repaired-baseline acceptance is claimed yet.
- No helper was started, process environment changed, real data/credential
  used, plan edited, file staged, or commit created. No Configuration
  candidate result, final Windows gate, lower-TFM execution, Linux/macOS
  validation, or R6 hosting result is claimed.

### Repaired-baseline verification — 2026-10-16, attempt 2

The parent subsequently executed the required gates on the **17,468-byte
repair patch identified above**, still over predecessor
`0cbb7656447866e4990de8431cd297e8a098b2dc`. This supersedes only the pending
gate state of the preceding handoff, not its historical observations.

```powershell
dotnet build src\fhir-candle\fhir-candle.csproj --configuration Release --framework net10.0
dotnet test --configuration Release --framework net10.0 --no-restore --verbosity normal --filter "FullyQualifiedName~fhir.candle.Tests.ConfigTests|FullyQualifiedName~fhir.candle.Tests.StrictModeStartupTests"
```

Both exited **0** on their first post-repair execution. The host build
reported **4 existing Pkcs warnings, 0 errors**; T-config reported **8
existing Pkcs warnings, 0 errors** and **99/99 passed, zero failed/skipped**.
The smaller build's warning count reflects its host-only scope, not a
warning suppression or a dependency fix. No new diagnostic occurred.
Windows 10.0.26200 x64, SDK 10.0.400, runtime 10.0.11 and adapter 3.1.5
remain unchanged. No net9/net8 execution is claimed for this repair gate.

All 76 new parameterized rows in the preceding table executed and passed,
as did the 23 retained occurrences, including the original two-external-
tenants assertion. The full 99-name occurrence fingerprint is
`aa0f16ebb429ff54513a060f3784571110bc1af3f468466265363b7cf762cc51`
under the same ordinal-sorted, LF-joined, no-terminal-LF normalization.
The source-derived row inventory is now observed execution evidence:
90 ConfigTests and nine StrictModeStartupTests. Discovery lifecycle
messages still do not supply an independent numeric discovery total.

The parent inspected all twelve fresh assets files / **36 TFM graphs**
before diagnosis and after these gates. All **946 package nodes**,
including dependency edges and selected assets, are unchanged:
**385,976 normalized bytes**, SHA-256
`91ebedf8083924c7fad156ddda99d2ddea89d7cb5a9601008195c38e06befe0c`.
The normalization is the historical package-only graph map above, which
excludes time-derived project-node versions. CLI remains **2.0.13**;
Configuration, Binder and providers remain **10.0.9** at every direct
declaration. This is a code-only repair, not a package upgrade.

#### Actual isolated production tenant selection

Used the existing SDK compatibility document's startup procedure with a
fresh owned temporary root, empty `source` and `cache`, disabled official
registries, no package arguments and no browser launch. The exact command
form for each of the four runs was:

```powershell
dotnet run --project src\fhir-candle\fhir-candle.csproj --framework net10.0 -- <tenant arguments below> --fhir-source "$root\source" --fhir-package-cache "$root\cache" --use-official-registries false --open-browser false --port 5826
```

Only child-process environment copies received synthetic
`Tenants_R4__0=external-r4`, `Tenants_R4B__0=external-r4b`,
`Tenants_R5__0=external-r5` and `Listen_Port=5827`. Thus the observed
listener on **5826** also confirms explicit CLI port precedence. No
parent/user environment value was changed. HTTP GETs used
`/fhir/{tenant}/metadata` and `Accept: application/fhir+json`.

| Tenant arguments | Observed 200 metadata and exact `fhirVersion` | Observed 404 absence |
|-|-|-|
| None: all tenant flags omitted | external-r4: 4.0.1; external-r4b: 4.3.0; external-r5: 5.0.0 | Not requested |
| `--r4 --r5 keep-r5` | external-r4b: 4.3.0; keep-r5: 5.0.0 | external-r4; external-r5 |
| `--r4b --r4 keep-r4` | keep-r4: 4.0.1; external-r5: 5.0.0 | external-r4b; external-r4 |
| `--r5 --r4 keep-r4` | keep-r4: 4.0.1; external-r4b: 4.3.0 | external-r5; external-r4 |

Every explicit-empty check retained another explicitly named supported
tenant, avoiding the unchanged all-empty host-default policy. Startup
reported the owned source/cache and no unexpected package load. The
negative metadata probes produced the expected existing controller
`GetMetadata <<< no tenant` error logs; these are observed 404 evidence,
not unexplained startup failures. Other diagnostics were the existing
Pkcs advisories. No R6 host, browser rendering, RI package, graceful
shutdown or complete hosted-service lifecycle is certified.

All four owned process trees were stopped by their specific PIDs
(96684, 133708, 113392, 101128 and their enumerated descendants).
The owned temporary root was removed; no helper is intended to survive.
No live registry, clinical input, SMTP/Zulip account or credential was used.
The separate phase-32 Configuration trial, final Windows all-target gate,
and Linux/macOS post-pass checks remain outstanding.
