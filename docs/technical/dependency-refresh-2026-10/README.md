# NuGet dependency refresh — October 2026

Assessment date: **2026-10-15**. Source under assessment:
`5c2dd5f8817a9fc911a3e74785a970bd12eeb675`.

**The fresh unchanged-source Windows baseline passed on all three TFMs.
Phase-1 acceptance remains parent-owned.** This record contains the original
declared/resolved inventory, baseline results and public-metadata/supplier
assessment. No package declarations have changed. No candidate graph or
candidate pass is claimed; candidate acceptance and the final unit-to-commit
roll-up remain pending.

## Evidence boundary and Windows baseline

Command and architectural authority is [AGENTS.md](../../../AGENTS.md),
supplemented by [building and testing](../building-and-testing.md) and the
existing [build/test workflow](../../../.github/workflows/build-and-test.yml).
The September [SDK compatibility record](../sdk6-compatibility.md) remains
history; its passes and unresolved checks are not results for this assessment.

| Input | Status / meaning |
|---|---|
| Source/index pre-flight | Parent reported the identified source unchanged and clean for the baseline, with an empty index. Continuation read-only Git inspection confirms the same `HEAD`, no tracked changes and only this dossier directory untracked. |
| Windows environment | Parent's actual baseline: Windows `10.0.26200`, x64; SDK `10.0.400` (`9.0.317` and `8.0.424` also installed); .NET/ASP.NET Core runtimes `10.0.11`, `9.0.19`, `8.0.30`. Observed xUnit VSTest adapter `3.1.5`. Not candidate evidence. |
| Enabled feeds | All twelve fresh assets record `https://api.nuget.org/v3/index.json` and `C:\Program Files (x86)\Microsoft SDKs\NuGetPackages\` (Microsoft Visual Studio Offline Packages). Installed-cache acquisition provenance is distinguished from enabled restore sources below. |
| Outdated-report input | No supplied `dotnet-outdated` report is known. None was run, installed, or fabricated. |
| Fresh resolved assets | Inspected the parent's newly generated `obj/project.assets.json` in all twelve project directories: **36 project/TFM graphs**, identified and reconciled below. These are original-source graphs, not candidates. |
| Browser/offline prerequisites | Planning observed Edge `154.0.4258.53` and Chrome `155.0.8059.40`; no browser workflow was executed. Read-only checks of the specific RI cache layouts below still found neither pinned package. No isolated cache was created or supplied by this continuation. |
| Package acceptance | Zero upgrades accepted; no candidate build, browser, transport, packaging, or collection result is claimed here. |

The parent supplied these exact results from the documented all-target
sequence on the unchanged source. This documentation continuation did **not**
run restore/build/test. The all-target result also supplies the matching
net10.0 baseline gate; an additional unchanged net10.0 full run is unnecessary.

| Documented baseline command, from repository root | Result |
|---|---|
| `dotnet restore` | Exit **0**; fresh assets for all twelve projects |
| `dotnet build --configuration Release --no-restore` | Exit **0**; **24 warnings, 0 errors**, diagnosed below |
| `dotnet test --configuration Release --no-restore --verbosity normal` | Exit **0**; all three target summaries passed, with individual totals below |

| Windows target | Passed | Total | Failed | Skipped |
|---|---|---|---|---|
| net10.0 | 4,306 | 4,306 | 0 | 0 |
| net9.0 | 4,306 | 4,306 | 0 | 0 |
| net8.0 | 4,306 | 4,306 | 0 | 0 |

| OS | net10.0 | net9.0 | net8.0 |
|---|---|---|---|
| Windows | Baseline passed | Baseline passed | Baseline passed |
| Linux | Pending post-Windows-pass follow-up | Pending post-Windows-pass follow-up | Pending post-Windows-pass follow-up |
| macOS | Pending post-Windows-pass follow-up | Pending post-Windows-pass follow-up | Pending post-Windows-pass follow-up |

### Observed execution-name inventory and its attribution limit

The parent's complete captured **combined** execution display-name inventory
has **12,918 occurrences**, **4,297 unique names** and **52 class names**.
Of those unique names, **4,288 occur three times and nine occur six times**.
Duplicates and parameter text are retained, not collapsed into method counts.
Its SHA-256 is:

`329a20dbaeb5923f6c37012356257704dbe8e71e8f7d80c20969108f68886be4`

Normalization: retain the full emitted `fhir.candle.Tests.*` display names
including parameters and duplicates; remove the `Passed`/`Failed`/`Skipped`
prefix and trailing duration; ordinal-sort; join with LF and **no terminal
LF**; encode UTF-8 without BOM. The digest and counts describe all three runs
together, not one run multiplied by three.

**Normal output interleaves case lines without TFM identifiers.** The
individual summary totals above are observed, but this output does not
establish a per-TFM name multiset/hash or 52 classes in each TFM. Dividing the
combined inventory is not proof of that attribution. xUnit emits discovery
lifecycle messages, not a numeric discovered-case total; 4,306 is an
execution-summary total, not a quoted discovery count. Subsequent comparisons
must retain both the observed aggregate fingerprint and individual TFM
totals, investigate changes, and preserve this limitation. No September count
or inferred per-TFM hash is substituted for this evidence.

### Baseline diagnostics: existing Pkcs advisories, not upgrade regressions

The 24 build warning occurrences repeat these four code/version/advisory
pairs. Fresh Host and Tests asset `logs` independently identify the same
target graphs:

| Package / resolved version | Affected project/TFM graphs | Diagnostic | Advisory |
|---|---|---|---|
| `System.Security.Cryptography.Pkcs` 10.0.0 | H and T, net10.0 | NU1902, moderate | [GHSA-2fmv-rv2c-59x7](https://github.com/advisories/GHSA-2fmv-rv2c-59x7) |
| `System.Security.Cryptography.Pkcs` 10.0.0 | H and T, net10.0 | NU1903, high | [GHSA-846f-f764-76mx](https://github.com/advisories/GHSA-846f-f764-76mx) |
| `System.Security.Cryptography.Pkcs` 8.0.1 | H and T, net9.0 and net8.0 | NU1902, moderate | [GHSA-2fmv-rv2c-59x7](https://github.com/advisories/GHSA-2fmv-rv2c-59x7) |
| `System.Security.Cryptography.Pkcs` 8.0.1 | H and T, net9.0 and net8.0 | NU1903, high | [GHSA-846f-f764-76mx](https://github.com/advisories/GHSA-846f-f764-76mx) |

The original graph explains their source: Host's **MailKit 4.17.0 →
MimeKit 4.17.0 → Pkcs**, also inherited by Tests. MimeKit's selected net10
group requires Pkcs **[10.0.0, )**, resolving 10.0.0; its net8 group selected
for net9/net8 requires **[8.0.1, )**, resolving 8.0.1. Both groups resolve
BouncyCastle.Cryptography **2.6.2**. No direct Pkcs declaration was added.

These are **freshly observed existing baseline warnings**, not introduced by
an upgrade and **not suppressed**. The baseline has no failed command or
unexplained build diagnostic, but the vulnerabilities are not remediated or
waived. Future unit evidence must compare against this inventory and inspect
the actual Pkcs closure; MailKit candidate minimums alone do not prove an
advisory fixed. This is diagnostic attribution, not a full vulnerability
audit or permission for an unrelated transitive pin.

Linux/macOS execution is required **after** the completed Windows pass, not a
local acceptance prerequisite. **This phase-1 baseline is not final Windows
completion and does not supply local browser/RI evidence.** Only after all
unit dispositions and final local gates will completion mean “Windows pass
complete; Linux/macOS validation pending,” not cross-platform approval.

## Complete source inventory

Read-only XML reconciliation covered every tracked `.csproj`, `.props` and
`.projitems`: **16 files = twelve C# projects + two props + two projitems**.
There are **46 active `PackageReference` declarations, 36 distinct IDs, in
seven declaration owners**: Host 20, Common 5, Tests 14, each store 1, UI props
4. XML comments, including `PolySharp` and `Microsoft.SourceLink.GitHub` in
the shared build props, are excluded.

All twelve projects import `src/fhir-candle.props`: C# 14,
`net10.0;net9.0;net8.0`, nullable and implicit usings. There is no checked-in
central package manifest or lock file. Four UI-props items expand into **five
consumers**. Counting those imports and only the active TestHost branch gives
60 direct-reference applications per TFM (180 across the three TFMs), not
180 distinct packages.

Paths in this project table are relative to `src/`. H/C/T/U/R4/R4B/R5 are
declaration-owner abbreviations used in the package table.

| Project / owner | Direct declarations and imports | Project-reference consumers/dependencies |
|---|---|---|
| **H** `fhir-candle/fhir-candle.csproj` | 20 own + 4 from **U**, Web SDK, packable .NET tool | Common, all three stores, UI Common and all three versioned UIs |
| **C** `FhirStore.Common/FhirCandle.Common.csproj` | 5 own | Version-independent contracts/configuration; no versioned project reference |
| **R4** `FhirStore.R4/FhirCandle.R4.csproj` | 1 own; imports store shared source | Common |
| **R4B** `FhirStore.R4B/FhirCandle.R4B.csproj` | 1 own; imports store shared source | Common |
| **R5** `FhirStore.R5/FhirCandle.R5.csproj` | 1 own; imports store shared source | Common |
| **UC** `FhirCandle.Ui.Common/FhirCandle.Ui.Common.csproj` | 0 own + 4 from **U**, Razor SDK | Common; `Microsoft.AspNetCore.App` framework reference |
| **U4** `FhirCandle.Ui.R4/FhirCandle.Ui.R4.csproj` | 0 own + 4 from **U**, Razor SDK | UI Common, Common, R4; ASP.NET Core framework reference |
| **U4B** `FhirCandle.Ui.R4B/FhirCandle.Ui.R4B.csproj` | 0 own + 4 from **U**, Razor SDK | UI Common, Common, R4B; ASP.NET Core framework reference |
| **U5** `FhirCandle.Ui.R5/FhirCandle.Ui.R5.csproj` | 0 own + 4 from **U**, Razor SDK | UI Common, Common, R5; ASP.NET Core framework reference |
| **T** `fhir-candle.Tests/fhir-candle.Tests.csproj` | 14 own items, 12 active per TFM; not packable | Host, Common, all three stores, UI R4, Performance and Performance.Probe |
| **P** `FhirCandle.Performance/FhirCandle.Performance.csproj` | No packages; non-packable executable | Performance.Probe; BCL-only external HTTP/evidence consumer |
| **PP** `FhirCandle.Performance.Probe/FhirCandle.Performance.Probe.csproj` | No packages; non-packable library | No project references; BCL-only optional experiment startup hook |

**U** is `src/fhir-candle-ui.props`, not another project.
`FhirStore.CommonVersioned/FhirStore.CommonVersioned.projitems` has no packages
and is imported by R4/R4B/R5. It compiles the shared client, serialization,
storage, search, operations and interaction sources separately against each
model. `FhirCandle.Ui.Versioned/FhirCandle.Ui.Versioned.projitems` also has no
packages, but is **not imported** by the active UIs. Do not activate its pickers.

The performance projects are solution/test consumers, not production
dependencies. Their sealed-artifact comparison deliberately fixes historical
SDK 5.11.4/6.2.1 artifacts and source revisions; see
[performance comparison](../performance-comparison.md) and
`ExperimentRunner.ValidateManifest`. Those constants/negative controls must
not be relabelled as this refresh, used as new performance evidence, or
mistaken for a requirement to keep the live application's SDK at 6.2.1.

### Metadata and alias restrictions

- Preserve Host/Tests `AddPackageAliases`: `candleR4`, `candleR4B`, `candleR5`
  select store assemblies. Host assigns `coreR4`, `coreR4B`, `coreR5` to the
  transitive model assemblies. Tests assign `coreR4/R4B/R5/R6` directly on
  their four SDK package references. Base/Conformance are shared, not
  isolated by those aliases. Model filenames have no historical `.Core`
  suffix; see [assembly aliases](../assembly-aliases.md).
- R6 is **test-only**. The three product capability versions remain R4
  `4.0.1`, R4B `4.3.0`, R5 `5.0.0`; no R6 tenant or fictional R6 product
  acceptance suite is introduced.
- TestHost has three distinct item-group conditions:
  `$(TargetFramework) == net10.0/net9.0/net8.0`, selecting the corresponding
  version below. They are one ID/version vector, not three unrelated units.
- `xunit.runner.visualstudio` and `coverlet.collector` retain
  `PrivateAssets=all` and
  `IncludeAssets=runtime; build; native; contentfiles; analyzers; buildtransitive`.
  BlazorMonaco retains `IncludeAssets=all`. Other package references use
  normal defaults except the model aliases.
- Preserve fixture-copy items, all project/import edges, and Host
  `PackAsTool`/static-web-asset metadata. A source-run browser check does not
  establish packaged-tool asset delivery.

## Original resolved graphs — fresh Windows baseline

Read-only reconciliation of `project.frameworks[tfm].dependencies` and
`targets[tfm]` against the active declarations/imports found **all 180 direct
reference applications at their original versions**, including the three
matching TestHost branches and all five UI-props consumers. The package
table's original-version column below is also the resolved package-node
version for every direct occurrence. NuGet represents these declarations as
`[version, )`, not exact singleton constraints; resolution happened to select
the original lower bounds. No direct version discrepancy or candidate was
found.

**A retained package node is not necessarily a selected DLL.** On H/net10,
the five direct Configuration packages retain 10.0.9 nodes but their
compile/runtime paths are `lib/net10.0/_._`, with dependencies pruned.
Binder's build entry is `buildTransitive/netstandard2.0/_._`; the other four
have `buildTransitive/net8.0/_._`. They do not supply 10.0.9 application DLLs
there. UI/net10 graphs omit the corresponding transitive Configuration and
Hosting packages. C/store/T net10 graphs retain the real Configuration,
Binder and Hosting 10.0.9 libraries; H/T net9/net8 retain the providers too.
The assets' framework-specific `packagesToPrune` explain these differences,
not a source edit or an unexplained missing dependency.

The union contains **98 resolved package IDs / 128 ID-version identities**,
**946 package-node occurrences** (including placeholders) and **99 project
nodes** across 36 graphs. The SDK adds
`Microsoft.AspNetCore.App.Internal.Assets/10.0.11` only to H/net10:
`autoReferenced=true`, `suppressParent=All`. It is not a 37th source-declared
ID. P and PP still have no NuGet packages. All generated project-node
versions are `2026.1015.1418`, from the existing time-derived props; these
are not new package pins or a different source revision.

### Graph identity and reproduction

Each code below maps to the project path in the source inventory. Its input
is **`obj/project.assets.json` beside that csproj**. Package counts are in
net10/net9/net8 order; project-node counts are identical across those TFMs.
Raw SHA-256 covers the complete file, including `libraries` file lists and
SHA-512 package identities, restore metadata, pruning rules and diagnostics.

| Project | Package nodes (10 / 9 / 8) | Project nodes per TFM | Raw assets SHA-256 |
|---|---|---|---|
| H | 45 / 60 / 61 | 8 | `58654e32cdad2e7d762d1cfd442025ccdfbb043c29ef45d68aa1c29a583f73ed` |
| C | 15 / 16 / 17 | 0 | `ba93ec9d80590f5df6372815bc932a2311b7186085b66daf42af767339ff09c3` |
| R4 | 23 / 23 / 24 | 1 | `f555939ed0c5fd9ae250433c83d3bbe6fb60f6f4bc83d9b072b92736145dc676` |
| R4B | 23 / 23 / 24 | 1 | `238e974c8f378449d92368321015276602eb64dc03adba8dcef39b79730e0428` |
| R5 | 23 / 23 / 24 | 1 | `cf14a913439caa4b77471b7379e877083b870f0ccc035a81d4a64351f07af853` |
| UC | 9 / 20 / 21 | 1 | `4e7d33d1c842cbc14c12107dad28a3aed43a4818c388eddcb2bff54372d2398b` |
| U4 | 15 / 25 / 26 | 3 | `21db8451638a435c440c59a95591d0488a49944ce2201c40acda882cc33a5ee1` |
| U4B | 15 / 25 / 26 | 3 | `212d9a3b8ae82c55b7c4ada359350918e7ff3352cff701d37d9a44edfee4eeef` |
| U5 | 15 / 25 / 26 | 3 | `99154a4bcfd251cd33b9a75b295e1486e09e1cbf0f0e4dc07405bd27d90713c1` |
| T | 85 / 94 / 95 | 11 | `e52f71d6355ae6cbff973dda0afd77c683be4fefd6a176ae4a6c9b5bbd73d1f1` |
| P | 0 / 0 / 0 | 1 | `51c8abb7a624dc45e423f870a0df7d6c194627074f024ed858cabea2568d8303` |
| PP | 0 / 0 / 0 | 0 | `4b964f68f03a09bdd2fc9159aa22d2e11eb982656ed9867d88079f9cab0f4732` |

For the following **per-target** hashes, parse the JSON and hash the complete
`targets[tfm]` object, recursively sorting object keys, keeping array order,
with ASCII JSON escaping and no whitespace or terminal newline. In Python
standard-library terms, the hashed bytes are:

```python
json.dumps(assets["targets"][tfm], sort_keys=True,
           separators=(",", ":"), ensure_ascii=True).encode("utf-8")
```

This retains dependency ranges, project nodes, compile/runtime/resource and
RID assets, aliases, and selected build/buildMultiTargeting entries. It is a
snapshot fingerprint, not a portable lock file: generated project versions
can change on a later restore. Raw-file hashes additionally cover metadata
outside `targets`; neither hash by itself proves browser delivery or binary
compatibility. PP's three target objects are all `{}`, hence equal hashes.

| Project | net10.0 target SHA-256 | net9.0 target SHA-256 | net8.0 target SHA-256 |
|---|---|---|---|
| H | `1e2e86e8cc7e9973a0389a3974cb7f323a73f8dadcb37a0b501e8a150e6b1379` | `1de3ca6eeaebefe6394bf7268e1fe375a9ecf56e0d2ad960f3330748bfbfd1db` | `f7b40afad2463f0fe3157bae896441d469a1f33a57b430d14d8afc344a403e8a` |
| C | `847b506c73703d8bcf073188e70de517d6329409f085693086f257808887b60e` | `580c50b4d3f50f35143fe532fb2f536d51fbaedb45a2f7e5b07603ad495a1a8e` | `d920870109e8b918fecee58a35c3dd3054c745acdd9e0bf09ea352673f18ae5a` |
| R4 | `76dd7b3104562fb41a1e473b0351a5d52da423313cbac2e0527927a8f69931cd` | `24c906687f58be73e340ce39672db4a227b80d34a1bc4686030ea17308663bd1` | `342286a52ca89d3ceec2ae42af15d4d18b85f50a526b7881b6a77b60e62fddb6` |
| R4B | `98f0fff580e3a2d6aaf368f3a7e770d2a6a55184c57bf2f519b0019d3bf9fede` | `ea441b6389ffb7d852f162456e52115aa105a2513a0ba7743c052acbef491155` | `fa8bf875c495bb078a054c522263bb335cc008a72818adc01003e0aa950bf537` |
| R5 | `7554012b127456d6a129a8597bf146ab131cf3ecb079a1c14a609031e61152b4` | `45bf2b1e17233d244644cb310eb734ba0b873c89eb4f7abc8940b86f44242964` | `0a2afc52a85feb9830bbeb8cadca892cefaf82a80d3dd56e6dca880aea26dd79` |
| UC | `6813c7ab598dac28910a25fde27b8603a69bd3d3a9a87c205a227d5c39224ac5` | `da78148040ddef0455e8213d177f8e19d61260f37d015af7850e03d5b1fab2ff` | `1c3778297111a1f3a475c9f2f8d690c3bf08a91f553adfba9457b2d44c44f721` |
| U4 | `ff1ee71669898cb0bc9e02265ff2047184193d5ca8844852b5ff0ad1c7f9e460` | `c16a9136273f351fb1004b895586e337447cfc9deb0fc32bfe71f71c7112d2e3` | `a980c5b2e0ba2d8c348a27b01708370d9fea27886197913bc91b3acf5e6435ec` |
| U4B | `60a746e139e402dfebac15d1891439452e0bd348f4ede2f7d9d80b89e95e9069` | `17d509be80964f9cbcff8f051e5a01e72a84ab135ce6b87da1c67de8b9e1a45c` | `c34d4796845513f6af23c33f53fb8911b1bf83e0ca82dd83beb808f3de5643e1` |
| U5 | `6763c4f3b49fc1bda8d14f38bc63d6fbe908a318b7aba8f8ef2034eb88557f65` | `2b41e3d50e617773e4a43810da5c731d7a71d2aba35b2f2f393497f0f14fa0eb` | `a593b3c49ada767232640ea88ed091648da70a485a19c13d3d810e662dcd898d` |
| T | `6509d134471270111546c9dbbb5dcf17b02f4dd0e256a3000fa98a505acfb4ca` | `4646c6d88e6bbe52f0eabe9a772b668a1dcb13c30c3ea99ba27d83f9436c50d0` | `56507e0346083280da014ce24f5f8527985851fd8d47a4017a7e13edbfea3c14` |
| P | `10ba66d0502acf0610d9eeb93a5a3a7ef257bb9250fbcac44dbe266ba4eb33b6` | `79292dfbb24dd87c6bc53a105c6ca25f0888f2b52f988b93fe6e17c43d18c39e` | `4d4026d66e8d62bf3186c187f51132ab5bcf517902f6e9087aef20b0023b1f1b` |
| PP | `44136fa355b3678a1146ad16f7e8649e94fb4fc21fe77e8310c060f61caaff8a` | `44136fa355b3678a1146ad16f7e8649e94fb4fc21fe77e8310c060f61caaff8a` | `44136fa355b3678a1146ad16f7e8649e94fb4fc21fe77e8310c060f61caaff8a` |

### Actual original transitive variations

Here **E.** abbreviates `Microsoft.Extensions.`; version vectors are always
**net10 / net9 / net8**. These are selected baseline nodes/edges, not
predictions of candidate resolution. Pruned edges can differ from the
supplier's full nuspec dependency group.

| Closure / consumers | Observed original selection and relevant constraints |
|---|---|
| Configuration/hosting, C and its consumers | E.Configuration, Configuration.Abstractions, Configuration.Binder, DependencyInjection.Abstractions, Diagnostics.Abstractions, FileProviders.Abstractions, Hosting.Abstractions, Logging.Abstractions, Options and Primitives resolve **10.0.9** wherever retained. All are real package libraries in C/R4/R4B/R5/T at net10 and all ten non-performance graphs at net9/net8; H/UI net10 pruning is described above. Hosting's five abstraction dependencies require >=10.0.9; retained Binder requires Configuration/Configuration.Abstractions >=10.0.9. |
| Provider/JSON closure, H and T | At net9/net8: EnvironmentVariables/Json/UserSecrets, E.Configuration.FileExtensions, E.FileProviders.Physical, E.FileSystemGlobbing, System.Text.Json, System.Text.Encodings.Web and System.IO.Pipelines resolve **10.0.9**. At net10: no such transitive nodes in T; H retains only the three direct provider placeholders. Json/UserSecrets' selected dependency edges retain their original >=10.0.9 minima. |
| Diagnostics, all ten non-performance projects | System.Diagnostics.DiagnosticSource **10.0.9** on net9/net8, absent as a package on net10. Thus OTel API's lower-TFM >=10.0.0 requirement is already satisfied by 10.0.9 in the baseline. |
| Identity, all ten non-performance projects | Tokens, IdentityModel.Logging and IdentityModel.Abstractions **8.19.1**; Microsoft.Bcl.Cryptography **10.0.2**. JsonWebTokens **8.19.1** occurs in H/T and requires Tokens >=8.19.1. Bcl.Cryptography brings System.Formats.Asn1 **10.0.2 only on net8**; net10/net9 have no Asn1 package node. Tokens' Logging.Abstractions >=8.0.0 edge is pruned in H/UI, retained in C/stores/T and resolves 10.0.9. |
| SDK, R4/R4B/R5, their UIs, H and T | Models → Conformance → Base all **6.2.1**; each edge is >=6.2.1. H has the three product models; T additionally has R6. Base resolves Fhir.Metrics **1.3.1**, Newtonsoft.Json **13.0.4**, and System.ComponentModel.Annotations **5.0.0 on net10 only**. Annotations is pruned on net9/net8. System.Reflection.Emit.Lightweight has **no node in any baseline graph**, despite the original published dependency. |
| SDK caching / MCP intersection | Base's E.Caching.Memory >=8.0.1 resolves **8.0.1** in the three stores and T on every TFM, but is pruned in H/versioned UIs. E.Caching.Abstractions resolves **8.0.0** in each store, **10.0.7** in T on all TFMs and H/net9/net8 because of MCP; it is absent from H/net10 and all UIs. Shared package names do not imply identical project graphs. |
| Package service, H/T | Firely.Fhir.Packages **5.0.2** requires Base >=6.1.1, resolved **6.2.1**, plus Newtonsoft.Json **13.0.4**, SemanticVersioning **3.0.0**, SharpZipLib **1.4.2**. It does not resolve Base 6.1.1 alongside the model cohort. |
| MCP, H/T | Protocol, ASP.NET Core and Core all **1.4.0**, with original minimum (not singleton) inter-package edges; AI.Abstractions **10.5.2** on all TFMs. Protocol's Hosting.Abstractions >=10.0.7 resolves **10.0.9** and Caching.Abstractions >=10.0.7 resolves **10.0.7**, where not pruned. Core's ServerSentEvents >=10.0.7 resolves **10.0.7** on net9/net8; its net8 Pipelines >=10.0.7 resolves **10.0.9**. No ServerSentEvents/Pipelines package node on net10. |
| OTel, H/T | All six direct IDs plus Api.ProviderBuilderExtensions resolve **1.16.0**. Core → ProviderBuilderExtensions → Api retain >=1.16.0; instrumentation retains **[1.16.0, 2.0.0)**. Exporters and Extensions.Hosting require core >=1.16.0. T additionally retains E.Logging.Configuration **10.0.0 / 9.0.0 / 8.0.0** and target-specific DI/Diagnostics/Hosting edges pruned in H. T/net8 OTLP's Binder >=8.0.2 resolves 10.0.9. |
| FluentUI/Monaco, all five UI-props consumers plus T | Components/Emoji/Icons **4.14.3**, BlazorMonaco **3.5.0**. Each asset package requires Components >=4.14.2. Only T retains Microsoft.AspNetCore.Authorization, Components, Components.Analyzers, Components.Forms, Components.Web, Metadata and Microsoft.JSInterop package vectors **10.0.9 / 9.0.17 / 8.0.28**; H/UI framework references prune those edges. |
| FluentUI's additional T-only Extensions closure | E.Http, DependencyInjection, Diagnostics and Logging: **10.0.9 / 9.0.17 / 8.0.1**. E.Options.ConfigurationExtensions: **10.0.9 / 9.0.17 / 8.0.0**. E.Validation **10.0.9** occurs only at net10. These coexist with the retained 10.0.9 abstractions, rather than forcing every Extensions ID to the same patch. |
| Mail, H/T | MailKit/MimeKit **4.17.0**, BouncyCastle.Cryptography **2.6.2**; Pkcs **10.0.0 / 8.0.1 / 8.0.1**, the diagnosed baseline advisory source above. |
| Test infrastructure, T only | Microsoft.NET.Test.Sdk, Microsoft.TestPlatform.TestHost, ObjectModel and Microsoft.CodeCoverage **18.7.0**. TestPlatform.TestHost's Newtonsoft.Json >=13.0.3 converges on the SDK's **13.0.4**. ASP.NET TestHost resolves **10.0.9 / 9.0.17 / 8.0.28**; its net8 Pipelines >=8.0.0 also converges on **10.0.9**. |
| Assertions/runner, T only | xunit/core/assert/extensibility.core/extensibility.execution **2.9.3**, xunit.abstractions **2.0.3**, analyzers **1.18.0**, VS adapter **3.1.5**, collector **10.0.1**. xunit → core and core → extensibility use **[2.9.3]**. Shouldly **4.3.0** resolves DiffEngine **11.3.0**, EmptyFiles **4.4.0**, then System.Management **6.0.1** and System.CodeDom **6.0.0**. |

### Selected compile/runtime, build, static and alias metadata

`lib/<tfm>/<ID>.dll` below denotes the exact same path in `compile` and
`runtime`, unless an exception is stated. `native` means
net10.0/net9.0/net8.0 respectively, not a native-code asset. This table is
the **original restored selection**, distinct from the public candidate
asset map later in this record.

| Package group | Selected compile/runtime paths |
|---|---|
| Configuration family / Hosting.Abstractions; IdentityModel; MCP including Core; OTel API/core/exporters/Hosting; zulip-cs-lib | Native `lib/<tfm>/<ID>.dll`; Configuration/pruning exceptions above. |
| CLI and NamingConventionBinder | `lib/net8.0/System.CommandLine.dll` and `lib/net8.0/System.CommandLine.NamingConventionBinder.dll` on all three TFMs, with related `.pdb;.xml`. CLI also selects 13 `lib/net8.0/<locale>/System.CommandLine.resources.dll` entries: cs, de, es, fr, it, ja, ko, pl, pt-BR, ru, tr, zh-Hans, zh-Hant. |
| SDK models, Base, Conformance and Firely.Fhir.Packages | `lib/net8.0/<ID>.dll` on all TFMs; e.g. `Hl7.Fhir.R4.dll`, not `Hl7.Fhir.R4.Core.dll`. Base/Conformance remain single shared assemblies in each graph. |
| OTel ASP.NET instrumentation; MailKit/MimeKit | `lib/net10.0/<ID>.dll` for net10; `lib/net8.0/<ID>.dll` for net9/net8. OTel core/API/exporters/Hosting carry related `.dll.sigstore.json;.xml`; instrumentation `.xml`; MailKit/MimeKit `.dll.config;.pdb;.xml`. |
| FluentUI Components / BlazorMonaco | Native `lib/<tfm>/Microsoft.FluentUI.AspNetCore.Components.dll` (related `.xml`) and `lib/<tfm>/BlazorMonaco.dll`. |
| FluentUI Emoji | Ten net8 DLLs on every TFM: `lib/net8.0/Microsoft.FluentUI.AspNetCore.Components.Emoji.dll` plus `Microsoft.FluentUI.AspNetCore.Components.Emojis.<category>.dll` in that directory for **Activities, AnimalsNature, Flags, FoodDrink, Objects, PeopleBody, SmileysEmotion, Symbols, TravelPlaces**. |
| FluentUI Icons | Five net8 DLLs on every TFM: `lib/net8.0/Microsoft.FluentUI.AspNetCore.Components.Icons.dll` and `.Icons.Color.dll`, `.Icons.Filled.dll`, `.Icons.Light.dll`, `.Icons.Regular.dll` with the same full prefix. |
| ASP.NET TestHost | Native `lib/<tfm>/Microsoft.AspNetCore.TestHost.dll` with `frameworkReferences: ["Microsoft.AspNetCore.App"]`. MCP.AspNetCore and OTel instrumentation also retain that framework reference. |
| Shouldly / ini-parser | Shouldly uses `lib/net9.0/Shouldly.dll` for net10/net9, `lib/net8.0/Shouldly.dll` for net8. All H/T targets select **`lib/netcoreapp3.1/IniParser.dll`**, not its netstandard2.0 alternative. |
| Test SDK / VS adapter / collector | SDK and adapter have `lib/net8.0/_._` compile/runtime placeholders on all targets. Collector has **no compile/runtime entry**; its target-specific build integration is below. xunit itself is also a metapackage without compile/runtime assets. |
| xUnit's executable/assertion closure | All targets: `lib/netstandard1.1/xunit.core.dll`, `lib/netstandard1.1/xunit.execution.dotnet.dll`, `lib/netstandard2.0/xunit.abstractions.dll`, `lib/net6.0/xunit.assert.dll`, through their respective resolved packages. |
| Pkcs and Windows-specific test transitive | Pkcs selects `lib/net10.0/System.Security.Cryptography.Pkcs.dll` on net10, `lib/net8.0/System.Security.Cryptography.Pkcs.dll` on net9/net8, plus corresponding `runtimes/win/lib/<tfm>/System.Security.Cryptography.Pkcs.dll` entries with `assetType=runtime`, `rid=win`. T's System.Management uses `lib/net6.0/System.Management.dll` plus `runtimes/win/lib/net6.0/System.Management.dll`. These are RID asset entries, not additional RID-specific target graphs or non-Windows execution evidence. |

The Tests assets preserve `aliases=coreR4/coreR4B/coreR5/coreR6` both on the
direct framework dependency metadata and on the respective **compile**
model DLL entries; runtime entries have no alias. Host's `coreR4/coreR4B/coreR5`
and Host/Tests' `candleR4/candleR4B/candleR5` are assigned later by the existing
`AddPackageAliases` targets, not serialized as aliases in these restore
nodes. No alias for Base/Conformance was introduced. The adapter/collector
framework dependencies retain `suppressParent=All` and
`include=Runtime, Build, Native, ContentFiles, Analyzers, BuildTransitive`;
BlazorMonaco's source `IncludeAssets=all` is represented by default inclusion.

Selected build integration (paths relative to each installed package):

- Binder: `buildTransitive/netstandard2.0/Microsoft.Extensions.Configuration.Binder.targets`
  where retained, versus H/net10's `_._`. Its package file inventory includes
  `analyzers/dotnet/cs/Microsoft.Extensions.Configuration.Binder.SourceGeneration.dll`;
  that inventory alone does not prove a generator ran.
- UserSecrets: `buildTransitive/net8.0/Microsoft.Extensions.Configuration.UserSecrets.props`
  and `.targets` in H/T net9/net8; H/net10 is `_._`.
- Test SDK: `build/net8.0/Microsoft.NET.Test.Sdk.props` and `.targets`;
  TestPlatform.TestHost: matching `build/net8.0/Microsoft.TestPlatform.TestHost.props`
  and `.targets`, with its `lib/net8.0/testhost.dll` and TestPlatform DLLs.
  CodeCoverage uses `build/netstandard2.0/Microsoft.CodeCoverage.props` and
  `.targets` plus `lib/net8.0/Microsoft.VisualStudio.CodeCoverage.Shim.dll`.
- Adapter: `build/net8.0/xunit.runner.visualstudio.props`, whose installed
  directory contains `xunit.runner.visualstudio.testadapter.dll`.
  Collector: `build/<tfm>/coverlet.collector.targets` for each matching TFM,
  with the corresponding bundled collector DLLs. Bundled dependencies are
  not separate resolved package nodes; ordinary tests do not prove collection.
- xunit.core: `build/xunit.core.props` and `.targets`, also selected under
  `buildMultiTargeting/`; xunit.analyzers' file inventory contains
  `analyzers/dotnet/cs/xunit.analyzers.dll` and `xunit.analyzers.fixes.dll`.
  Shouldly selects `buildTransitive/Shouldly.targets`.
- Components/Monaco: selected `buildTransitive/<ID>.props` and
  `buildMultiTargeting/<ID>.props`; these import `build/<ID>.props`, which
  imports both `Microsoft.AspNetCore.StaticWebAssets.props` and
  `Microsoft.AspNetCore.StaticWebAssetEndpoints.props`. Emoji/Icons have
  neither selected build entries nor a `staticwebassets/` directory.
- H/net10's SDK-generated Internal.Assets selects
  `build/Microsoft.AspNetCore.App.Internal.Assets.props`,
  `buildTransitive/Microsoft.AspNetCore.App.Internal.Assets.targets` and
  `buildMultiTargeting/Microsoft.AspNetCore.App.Internal.Assets.targets`.
  Its build target contributes `_framework/blazor.web.js` and
  `_framework/blazor.server.js` for the Web executable, with debug maps
  conditional on `UseBlazorFrameworkDebugAssets`.

Installed Components **4.14.3** describes **39 static assets / 75 endpoints**;
Monaco **3.5.0** describes **122 / 244**. Their base paths are
`_content/Microsoft.FluentUI.AspNetCore.Components` and `_content/BlazorMonaco`,
with `SourceType=Package`, the matching package `SourceId`,
`AssetKind=All`, `AssetRole=Primary`, `CopyToOutputDirectory=Never` and
`CopyToPublishDirectory=PreserveNewest`. Mode is `All` except Components'
`Microsoft.FluentUI.AspNetCore.Components.lcdo7z9xd2.bundle.scp.css`, which is
`Reference` with `ScopedCss/ProjectBundle`. Other important payloads include
Components' `.lib.module.js`, and Monaco's `jsInterop.js`,
`lib/monaco-editor/min/vs/loader.js`, `editor/editor.main.js` and
`editor/editor.main.css` under that same `vs` directory.

The following raw-file SHA-256 values identify the **full installed static
descriptors**, including relative paths, fingerprint/integrity, endpoint
selectors/headers and copy metadata, without treating every packaged file as
a browser-tested asset:

| Installed package | `build/Microsoft.AspNetCore.StaticWebAssets.props` | `build/Microsoft.AspNetCore.StaticWebAssetEndpoints.props` |
|---|---|---|
| Components 4.14.3 | `829791855c87e5442c4225c56721c95b190808a06a1897f4c4701668f1866408` | `17c07531da635243933da92d60dfc697bd183bde0ff1b413bd01e9c9997a6185` |
| BlazorMonaco 3.5.0 | `5d5ba87a6748a5d9dbc130f9c61c7286ff542450e3cbd8072f1854992401e528` | `59ba0a2b86e76057be3070ac0be306efdc58dd8201e2ee1fb162dab61f253ec8` |

### Enabled feeds versus installed-cache provenance

The two enabled restore sources are recorded above. The assets also list
the user global-packages folder and the Visual Studio shared NuGet fallback
folder. All **128 selected ID-version identities** had `.nupkg.metadata` in
the existing user global-packages folder; none required the fallback for
this inspection. Each metadata `contentHash` matched the corresponding
`libraries[ID/version].sha512` in every asset file containing that identity.

| Installed metadata `source` | Selected ID-version identities |
|---|---|
| `https://api.nuget.org/v3/index.json` | 71 |
| `https://packagefeedproxy.microsoft.io/nuget/v3/index.json` | 57 |

MailKit 4.17.0, MimeKit 4.17.0 and both warning-producing Pkcs versions record
nuget.org provenance. The SDK-generated Internal.Assets 10.0.11 records the
proxy. These are **recorded earlier cache acquisition sources**, not proof
that this restore downloaded packages, enabled the proxy, or contacted it.
Metadata agreement is not a new binary-signature/content audit.

Read-only offline-feed filename inventory found **263 `.nupkg` files**, none
matching any of the 36 direct IDs' standard `<ID>.<version>.nupkg` filenames.
No selected identity's installed metadata names that offline feed. This
does not prove feed-wide content equality with nuget.org or authenticate
arbitrarily named archives. No credentials, package downloads, new caches or
tool installs were needed; public candidate availability remains the dated
nuget.org research below.

## Dated availability, not approved versions

For **all 36 IDs**, read the nuget.org flat-container version index, then
registration leaves and nuspecs for the originals, highest stable and
original-major fallback. Every stable candidate shown below was confirmed
**listed**; all were published by the assessment date. Result:
**30 IDs have a higher listed stable, four are current, two have no stable
version indexed**. This reconciles the planning snapshot, not an unavailable
outdated-tool report.

Status vocabulary:

- **unassessed**: availability/initial migration research exists; candidate
  execution and acceptance remain open.
- **current**: original equals highest listed stable for this ID; not a
  general safety or support certification.
- **upgraded**: accepted version change with its unit evidence; **none yet**.
- **capped**: a lower stable chosen for assessment after assessing a higher
  candidate; a trial cap is not an accepted version change.
- **deferred**: retain the declaration because the stated stable candidate
  or required evidence is unavailable. It does not mean all releases are
  incompatible.

`same` in the fallback column means the newest stable remains in the
original major. The parent has merged original phases **11/12 into P11**
(API/SDK) and **19–22 into P19** (aligned models). Stable IDs are retained:
12 and 20–22 are absorbed, not completed or separately executable.
There are **27 executable phases**. P24 now trials Components **4.14.5** with
conditional **4.14.4** if P8 retains Hosting.Abstractions 10.0.9. None of these
decisions accepts a candidate; each future unit still needs its own evidence.

| Package ID | Original declared / resolved node version | Owners (every occurrence) | Highest listed stable | Original-major fallback | Initial disposition |
|---|---|---|---|---|---|
| `System.CommandLine` | 2.0.9 | H, C, T | 2.0.13 | same | unassessed, P2 |
| `Microsoft.Extensions.Configuration` | 10.0.9 | H, C | 10.0.13 | same | unassessed, P3 |
| `Microsoft.Extensions.Configuration.Binder` | 10.0.9 | H, C | 10.0.13 | same | unassessed, P4 |
| `Microsoft.Extensions.Configuration.EnvironmentVariables` | 10.0.9 | H | 10.0.13 | same | unassessed, P5 |
| `Microsoft.Extensions.Configuration.Json` | 10.0.9 | H | 10.0.13 | same | unassessed, P6 |
| `Microsoft.Extensions.Configuration.UserSecrets` | 10.0.9 | H | 10.0.13 | same | unassessed, P7 |
| `Microsoft.Extensions.Hosting.Abstractions` | 10.0.9 | C | 10.0.13 | same | unassessed, P8 |
| `Microsoft.IdentityModel.Tokens` | 8.19.1 | C | 8.23.0 | same | unassessed, P9 |
| `Microsoft.IdentityModel.JsonWebTokens` | 8.19.1 | H | 8.23.0 | same | unassessed, P10 |
| `OpenTelemetry.Api` | 1.16.0 | H | 1.20.0 | same | unassessed, P11 API/SDK unit |
| `OpenTelemetry` | 1.16.0 | H | 1.20.0 | same | unassessed, P11 API/SDK unit |
| `OpenTelemetry.Exporter.Console` | 1.16.0 | H | 1.20.0 | same | unassessed, P13 |
| `OpenTelemetry.Exporter.OpenTelemetryProtocol` | 1.16.0 | H | 1.20.0 | same | unassessed, P14 |
| `OpenTelemetry.Extensions.Hosting` | 1.16.0 | H | 1.20.0 | same | unassessed, P15 |
| `OpenTelemetry.Instrumentation.AspNetCore` | 1.16.0 | H | 1.20.0 | same | unassessed, P16 |
| `ModelContextProtocol` | 1.4.0 | H | 2.2.0 | 1.4.1 | unassessed, P17 pair |
| `ModelContextProtocol.AspNetCore` | 1.4.0 | H | 2.2.0 | 1.4.1 | unassessed, P17 pair |
| `MailKit` | 4.17.0 | H | 4.18.1 | same | unassessed, P18 |
| `Hl7.Fhir.R4` | 6.2.1 | R4, T | 6.7.0 | same | unassessed, P19 aligned cohort |
| `Hl7.Fhir.R4B` | 6.2.1 | R4B, T | 6.7.0 | same | unassessed, P19 aligned cohort |
| `Hl7.Fhir.R5` | 6.2.1 | R5, T | 6.7.0 | same | unassessed, P19 aligned cohort |
| `Hl7.Fhir.R6` | 6.2.1 | T only | 6.7.0 | same | unassessed, P19 aligned cohort |
| `Firely.Fhir.Packages` | 5.0.2 | H | 6.2.0 | 5.0.2 | unassessed, P23 |
| `Microsoft.FluentUI.AspNetCore.Components` | 4.14.3 | U → five projects | 5.0.1 | 4.14.5 | capped trial, P24: 4.14.5; conditional 4.14.4; neither accepted |
| `Microsoft.FluentUI.AspNetCore.Components.Emoji` | 4.14.3 | U → five projects | 5.0.1 | 4.14.5 | unassessed, P25 |
| `Microsoft.FluentUI.AspNetCore.Components.Icons` | 4.14.3 | U → five projects | 5.0.1 | 4.14.5 | unassessed, P26 |
| `Microsoft.AspNetCore.TestHost` | net10: 10.0.9; net9: 9.0.17; net8: 8.0.28 | T, three conditioned items | 10.0.13 | net10: 10.0.13; net9: 9.0.21; net8: 8.0.32 | unassessed, P27 vector |
| `Microsoft.NET.Test.Sdk` | 18.7.0 | T | 18.11.0 | same | unassessed, P28 |
| `xunit.runner.visualstudio` | 3.1.5 | T | 4.0.1 | 3.1.5 | unassessed, P29 |
| `coverlet.collector` | 10.0.1 | T | 10.1.0 | same | deferred, P30 collection evidence |
| `BlazorMonaco` | 3.5.0 | U → five projects | 3.5.0 | same | current |
| `ini-parser-netcore` | 3.0.0 | H | 3.0.0 | same | current |
| `Shouldly` | 4.3.0 | T | 4.3.0 | same | current |
| `xunit` | 2.9.3 | T | 2.9.3 | same | current; preserve v2 |
| `System.CommandLine.NamingConventionBinder` | 2.0.0-beta5.25306.1 | H, T | none | none | deferred: no stable; retain preview pin |
| `zulip-cs-lib` | 0.0.1-beta.5 | H | none | none | deferred: no stable; retain preview pin |

### Public target/asset map

These are published dependency groups and asset layouts, **not candidate
restore results**; original local selections are recorded above. `netN`
below means `netN.0`. Native groups are available at each
application TFM unless a fallback is stated. Extra .NET Framework/Standard
groups are not evidence that this application targets those frameworks.

| Packages / original → assessed versions | Applicability to net10 / net9 / net8 |
|---|---|
| Configuration family, Hosting.Abstractions; IdentityModel; MCP pair | Native net10 / net9 / net8 groups for both originals and candidates, including MCP 1.4.1 fallback |
| CLI 2.0.9 → 2.0.13; retained NamingConventionBinder | `lib/net8.0` for all three; also publish netstandard2.0 |
| OTel API/core/exporters/Hosting 1.16.0 → 1.20.0 | Native net10 / net9 / net8 groups |
| OTel ASP.NET instrumentation 1.16.0 → 1.20.0 | net10 / net8 / net8; ASP.NET Core framework reference; unused netstandard2.0 group has different dependencies |
| SDK models and Base/Conformance 6.2.1 → 6.7.0 | Original net8 for all three; candidate net10 / net8 / net8; netstandard2.1 also retained |
| Firely.Fhir.Packages 5.0.2 → 6.2.0 | Original net8 for all three; candidate net10 / net8 / net8; netstandard2.1 also retained |
| MailKit 4.17.0 → 4.18.1 | net10 / net8 / net8 groups |
| FluentUI Components 4.14.3 → 4.14.5 or 5.0.1 | Native net10 / net9 / net8 libraries plus build/buildTransitive static-web-asset props. 5.0.1 also has an **unused net11** group with preview dependencies; it does not drop net8/net9. |
| FluentUI Emoji/Icons 4.14.3 → 4.14.5 or 5.0.1 | net8 libraries for all three; asset/category DLLs included in each package. No separate staticwebassets directory in the inspected asset packages. |
| BlazorMonaco 3.5.0 | Native net10 / net9 / net8 plus static web assets; supplier bundles Monaco 0.55.1 |
| TestHost version vector | Each branch has only its matching native TFM; all reference `Microsoft.AspNetCore.App`. net8 additionally depends on System.IO.Pipelines >=8.0.0. |
| Test SDK 18.7.0 → 18.11.0; adapter 3.1.5 → 4.0.1 | Build integration, not ordinary compile libraries; applicable net8 build assets for these application TFMs. Test SDK supplies TestPlatform.TestHost/CodeCoverage transitively. |
| Collector 10.0.1 → 10.1.0 | TFM-specific build targets/DLLs for net10/net9/net8; candidate additionally has netstandard2.0 build fallback |
| Shouldly 4.3.0 | net9 / net9 / net8 libraries; DiffEngine >=11.3.0 and EmptyFiles >=4.4.0 |
| xunit 2.9.3 | Metapackage: xunit.core **[2.9.3]**, xunit.assert >=2.9.3, xunit.analyzers >=1.18.0 |
| ini-parser-netcore 3.0.0 | Publishes netcoreapp3.1 and netstandard2.0 libraries; fresh original H/T graphs select `lib/netcoreapp3.1/IniParser.dll` on all three targets |
| zulip-cs-lib 0.0.1-beta.5 | Native net10 / net9 / net8 groups; no package dependencies declared |

## Candidate-specific unit constraints and migration eligibility

Range notation is NuGet notation: bare nuspec `x` means **[x, )**, not
`[x]`; square-bracket singleton `[x]` means exact. “Range-compatible” below
does not mean the local behavioral gate has passed. Shared prefixes, files
and transitive assemblies alone do not establish lockstep.

### CLI, configuration, hosting and identity

| Boundary | Exact relevant constraints and initial conclusion |
|---|---|
| CLI P2, retained binder | Binder beta5 requires CLI **[2.0.0-beta5.25306.1, )** on both its groups, with no upper bound. 2.0.13 satisfies that range; no active NamingConventionBinder invocation was found. Keep both preview declarations, not a new preview or a removal. Existing `SetAction`/`ParseResult` usage already follows the supplier's post-beta migration shape. Preserve `ResponseFileTokenReplacer = null`, CLI precedence, option arrays, help/errors. Eligible servicing trial, not proven binary compatibility of the unused legacy binder. |
| Configuration P3–P7 | Binder, EnvironmentVariables and Json **10.0.13** require Configuration **[10.0.13, )**; UserSecrets **10.0.13** requires Json **[10.0.13, )**, on all three targets. Retained 10.0.9 providers require their corresponding dependencies **[10.0.9, )**, without upper bounds, so they do not prohibit Configuration 10.0.13 first. Keep P3 before P4/P5/P6 and P6 before P7; no forced whole-family merge. Json also raises FileExtensions/FileProviders and, on net8/net9, System.Text.Json to >=10.0.13. |
| Hosting P8 | 10.0.13 raises its Configuration/DI/Diagnostics/FileProviders/Logging abstractions to **[10.0.13, )**. These are transitive updates, not additional direct-ID units. MCP 2.2.0 needs Hosting.Abstractions >=10.0.10; FluentUI Components 4.14.5 needs >=10.0.12 on net10. A retained 10.0.9 direct pin does not satisfy either; cap or revise pending units rather than allowing a downgrade warning. |
| Identity P9/P10 | JsonWebTokens **8.23.0** requires Tokens **[8.23.0, )**; retained JsonWebTokens 8.19.1 requires **[8.19.1, )**, no upper bound. Tokens 8.23.0 raises IdentityModel.Logging to >=8.23.0. The supplier supports the 8.x line; no equality constraint was established by these nuspecs. Tokens-first remains a range-compatible trial, followed by JWT, with both positive and negative SMART/key/token tests. |

The [.NET October servicing notes][dotnet-servicing] and
[CLI migration guidance][cli-migration] provide an eligible servicing route,
not a reason to reject every Microsoft package. The [IdentityModel support
policy][identity-support] retains the 8.x line. Its release history through
[8.23.0][identity-release] covers serializer/JWE handling, token-header/claims
allocation changes, optional audience matching and key support. Do not enable
new auth behavior or weaken validation merely to adopt 8.23.0.

### OpenTelemetry: required P11 API/SDK unit, not all six IDs

The supplier's [versioning policy][otel-versioning] supports API version `a`
with SDK versions **[a, next-major)**. Consequently API **1.20.0** with
retained SDK **1.16.0** is **outside the documented support envelope**, even
though retained ProviderBuilderExtensions 1.16.0 only declares API >=1.16.0.
Conversely SDK 1.20.0 requires ProviderBuilderExtensions >=1.20.0, which
requires API >=1.20.0. Neither direct pin can move alone at this checkpoint.
If SDK 1.16.0 is retained, the documented supported API ceiling is 1.16.0
within major 1; an API-only upgrade is not a supported fallback.

**Parent decision applied: P11 absorbs original P12**, retaining the union of
Host csproj, `Program.cs`, `OpenTelemetryCompatibilityTests.cs`, and the
disabled/context/resource/signal/flush checks. Its planned evidence record
is `opentelemetry-api-sdk.md`, not two independent upgrade records. The
joint 1.20.0 trial remains unexecuted.

Retained-consumer checks:

- Console, OTLP and Extensions.Hosting 1.16.0 require SDK **[1.16.0, )**;
  their 1.20.0 candidates require **[1.20.0, )**. The retained versions do not
  force all exporters into the API/SDK unit.
- ASP.NET instrumentation 1.16.0 requires ProviderBuilderExtensions
  **[1.16.0, 2.0.0)**; candidate 1.20.0 requires **[1.20.0, 2.0.0)**.
  ProviderBuilderExtensions 1.20.0 satisfies both. The retained upper bound
  must not disappear from the eventual graph record.
- SDK 1.20.0 adds EnvironmentVariables minimums 10.0.0/9.0.0/8.0.0 by TFM;
  the existing direct 10.0.9 satisfies them. API's DiagnosticSource >=10.0.0
  on net8/net9 is already present in its original 1.16.0 metadata, not a new
  1.20.0 dependency change.

P13–P16 can remain separate **after** the API/SDK checkpoint. If that
checkpoint is retained, their new minima must be re-evaluated; no arbitrary
rollback independence is promised. [1.20 release notes][otel-release] change
attribute-limit fallbacks, byte-array truncation, console log scope output,
OTLP log schema URLs and shutdown/resource handling.
[Instrumentation notes][otel-aspnet] change 5xx `error.type` reporting.
These are eligible same-major candidates requiring real configured-signal
and disposal/transport checks, not automatic rejection or a metadata-only pass.

### SDK: aligned P19 cohort trial; mixed binaries remain unproven

For R4/R4B/R5/**test-only R6**, every 6.7.0 model nuspec requires Conformance
**[6.7.0, )**, and Conformance requires Base **[6.7.0, )**. Originals require
the corresponding **[6.2.1, )**. Thus NuGet ranges permit old models with
new common assemblies; they do **not** prove that combination supported.
The aliases isolate model names, not Base/Conformance.

Firely's [multi-version guide][firely-multiple] demonstrates same-release
models with aliases, not mixed SDK minor releases. Its
[SDK README][firely-readme] promises effort toward **compile, not binary,
compatibility** between minors. [6.6.0][firely-66] specifically changes
`HtmlChecks(PocoNode)` from `bool` to `bool?` and requires recompilation.
No positive supplier guarantee for **6.2.1 model binaries + 6.7.0 common
binaries** was established. This boundary is **unknown**, not a claimed
universal exact-version rule or a claim that aligned 6.7.0 is unsafe.

**Parent decision applied: P19 absorbs original P20–P22** as one aligned
6.7.0 cohort trial, with the union of their literal owned paths, verification
and planned `hl7-fhir-sdk.md` record: three store pins plus all four test
pins. Mixed-version binary support remains unproven, but that does not make
the aligned trial unsafe or accepted. Shared-source adaptation owners and
the dated SDK-envelope addendum remain required; the historical migration
record is not rewritten.

The same-major newest candidate is 6.7.0. Supplier notes identify genuine
behavioral checks rather than a blanket major-migration rejection:

- [6.3.0][firely-63]: wrong-cased names fail in strict parsing, while
  non-strict modes retain lenient binding. The store's parser uses
  `BACKWARDSCOMPATIBLE` and its explicit lenient path uses `OSTRICH`;
  application strict search/CRUD policy is not automatically SDK strict parsing.
- [6.5.0][firely-65]: empty objects/elements are no longer serialized;
  snapshot issue codes and resolver concurrency change.
- [6.7.0][firely-67]: duplicate primitive values under `name` and `_name`
  produce fatal `JSON135` outside Ostrich, invalid-primitive FHIRPath handling
  changes, and terminology/issue output changes. Protect the authored
  parse/wire/search/operation oracles instead of regenerating expectations.

Base 6.7.0 also raises Fhir.Metrics 1.3.1 → 1.4.0, uses Caching.Memory
>=10.0.9 on net10 (>=8.0.1 on net8), and removes the old
Reflection.Emit.Lightweight dependency. Fresh resolved closures and the
retained package-service integration must be checked in the cohort trial.
Lower stable SDK releases are not accepted or ruled out by this initial review.

### Firely package service: a separate version sequence and retained consumer

Retained Firely.Fhir.Packages **5.0.2** requires Base **[6.1.1, )**, with
**no declared upper bound**. Candidate **6.2.0** requires Base **[6.6.0, )**.
The latter fits an aligned 6.7.0 SDK, but would raise Base underneath retained
6.2.1 models/Conformance. Do not independently trial that mixed closure if the
SDK cohort was retained. Re-scope prerequisites or retain 5.0.2, which is also
the highest stable in major 5. Conversely, the 5.0.2 minimum alone does not
force package service into an aligned SDK upgrade; test its retained binaries
there before claiming that checkpoint is independent.

[Package-service 6.0 migration notes][packages-60] introduce multiple versions
per FHIR package closure, lock-file format 2 unreadable by 5.x, changed struct
equality and canonical-resolution defaults. [6.2 notes][packages-62] add
cache-location configuration and replaceable dependency redirection; all
three application TFMs remain eligible. These FHIR package lock files are
not NuGet lock files.

The inspected application uses PackageClient and its own DiskPackageCache,
manifest reader and dependency recursion; no active call to PackageClosure,
LockFileJson or the changed canonical-default methods was found in those
adapters. A broad resolver migration is therefore **not established merely
by the major number**. P23 should assess the real cache/name/equality behavior
with synthetic local registry/cache tests. Do not convert an existing shared
cache to a format old consumers cannot read or use live clinical packages.

### FluentUI: cap Components for actual migration scope, not frameworks

[v5 migration guidance][fluent-migration] removes FluentDesignTheme,
FluentHeader/BodyContent, FluentNavMenu/Group/Link and FluentTextField/Search;
it changes root providers, CSS theming, navigation and input patterns.
These are active consumers in MainLayout, NavMenu, resource pages, SMART
pages, subscription Tour, R4 HTI-2 and the CDex Task table.

**Parent decision applied: cap Components 5.0.1 for broad migration scope;
P24 trials 4.14.5**, with the conditional listed 4.14.4 route below. Adopting
Components 5.0.1 is not a pin-only or bounded owned-page adaptation: for example
`Pages/Smart/ClientDetailsPage.razor`, `Pages/RI/subscriptions/Tour.razor`,
`FhirCandle.Ui.R4/Subscriptions/UsCoreHti2ContentsR4.razor` and
`FhirCandle.Ui.R4/davinci-cdex/TaskTable.razor` would need non-icon changes
outside P24's ownership. This is a demonstrated broad-migration/scope issue,
**not** a fictional net8/net9 removal. No extra ownership or UI rewrite is
requested in this documentation slice.

The asset packages have no dependency on each other. The supplier's
[v5.0.1 build pins][fluent-pins-5], [Icons project][fluent-icons-project] and
[Emoji project][fluent-emoji-project] compile Icons/Emoji
against **Components 4.14.5**, corroborating a specific cross-major
combination rather than assuming matching version numbers:

| Components / either asset package | NuGet constraint / supplier evidence | Assessment |
|---|---|---|
| 4.14.3 / 4.14.3 | Asset requires **[4.14.2, )** | Original resolved combination; all-target Windows baseline passed, browser behavior unverified |
| 4.14.3 / 4.14.4 | Asset requires **[4.14.2, )**; supplier 4.14.4 build pin is 4.14.2 | Eligible lower-stable route if Components is retained; not a browser pass |
| 4.14.3 / 4.14.5 | Asset requires **[4.14.4, )** | Incompatible with the retained direct Components pin |
| 4.14.3 / 5.0.1 | Asset requires **[4.14.5, )** | Incompatible with the retained direct Components pin |
| 4.14.5 / retained 4.14.3 or fallback 4.14.5 | Respective minimums **[4.14.2, )** / **[4.14.4, )** | Range-compatible v4 trial; no proven co-movement requirement |
| 4.14.5 / 5.0.1 | **[4.14.5, )** and exact supplier build baseline 4.14.5 | Supplier-built compatible baseline; eligible separate asset trials, local behavior unverified |
| 5.0.1 / retained 4.14.3 | Nuspec minimum permits it; inspected guidance does not certify this old-asset/new-component binary combination | Support unknown; not a reason to claim every minimum-satisfying version supported |
| 5.0.1 / 5.0.1 | Co-released v5 packages; assets still declare **[4.14.5, )** | Intended v5 route, but current application's component migration remains out of bounded scope |

Keep P25/P26 separate after an accepted Components prerequisite unless new
evidence proves co-movement. Newest assets 5.0.1 are **not automatically
capped with Components**; assess their actual icon APIs/rendering against
4.14.5. No active Emoji instantiation was found; do not invent a page.
Icons are active in navigation/theme, Index and R4 HTI-2. Monaco remains 3.5.0
and an actual editor/browser/static-asset consumer.

Additional lower candidate **4.14.4** for all three FluentUI IDs was checked
and is listed; its [supplier pins][fluent-pins-4144] corroborate the asset
minimum above. Components 4.14.4 retains original net10 minimums
Hosting.Abstractions/Http/Components.Web >=10.0.9. Components 4.14.5 instead
requires Hosting.Abstractions/Http >=10.0.12 on net10 and Components.Web
>=10.0.12 / 9.0.19 / 8.0.30. Components 5.0.1 requires Components.Web
>=10.0.12 / 9.0.20 / 8.0.31. These are package constraints, not proof of a
specific installed runtime patch requirement. Recheck fresh graphs/runtime
behavior against the reported Windows installation. If P8 retains 10.0.9,
assess the 4.14.4 route rather than forcing 4.14.5 or rejecting all UI updates.
Browser and applicable packaged-asset evidence remain necessary for any route.

### MCP: exact newest pair; a real fallback remains eligible

On net8/net9/net10, ASP.NET Core **2.2.0** requires ModelContextProtocol
**[2.2.0]**, which requires Core **[2.2.0]**. P17's two direct IDs are one
required newest-candidate unit. ModelContextProtocol also needs
Hosting.Abstractions and Caching.Abstractions **[10.0.10, )**; P8 must
actually satisfy that direct hosting prerequisite. Core adds AI.Abstractions
>=10.8.3 and target-dependent Logging/Pipelines/ServerSentEvents >=10.0.10.

[2.0 migration notes][mcp-20] change HTTP to stateless by default and introduce
discovery-first negotiation while retaining down-level interoperability;
[2.2][mcp-22] adds hybrid session modes. The application's bare
`WithHttpTransport()` would inherit new defaults. Existing tool handlers can
remain an eligible bounded migration, but preserving legacy session/routes,
initialization, tool schemas, errors and calls requires explicit assessment
and actual HTTP tests, not controller-only TestServer or tool-construction
tests. Do not add Tasks/Apps packages or suppress migration diagnostics.

The **1.4.1 fallback** is a supplier servicing fix for the HTTP/SSE response
stream leak ([release notes][mcp-141]); no broad migration is indicated.
Its ASP.NET package requires ModelContextProtocol **[1.4.1, )**, then Core
**[1.4.1, )**, with Hosting.Abstractions **[10.0.7, )**. Unlike 2.2.0,
those are **minimums, not exact pins**. Preserve the planned pair assessment
unless the parent explicitly revises that fallback boundary. Both newest
and fallback remain unaccepted pending real transport evidence.

### Mail and test infrastructure

- **MailKit 4.18.1:** [supplier changes][mail-release] include native NTLM
  authentication fixes and a Linux/macOS duration correction; MimeKit
  4.18.1 fixes corrupt-TNEF integer overflows. The candidate requires MimeKit
  **[4.18.1, )** and System.Formats.Asn1 **[10.0.0, )**; MimeKit brings
  BouncyCastle.Cryptography >=2.7.0 and System.Security.Cryptography.Pkcs
  >=10.0.0 on the applicable native groups. Eligible same-major trial.
  REST-hook tests and the `example.org` success shortcut are not SMTP,
  TLS/authentication, MIME attachment or delivery evidence.
- **TestHost:** retain the framework-matched **10.0.13 / 9.0.21 / 8.0.32**
  vector. Highest-ID version 10.0.13 is not a candidate for net9/net8.
  October [.NET 9][dotnet-9] and [.NET 8][dotnet-8] servicing releases retain
  those branches; require the full Windows case inventory for this unit.
- **Test SDK 18.11.0:** requires TestPlatform.TestHost and CodeCoverage
  **[18.11.0, )** on net8 (used by all three application targets).
  [Release notes][vstest-release] include disabling the MTP testhost by
  default and logging/host fixes. This is an eligible VSTest update, not
  permission to migrate runners. Require all-target discovery/execution.
- **VS adapter 4.0.1:** the [tagged supplier README][xunit-adapter] explicitly
  supports **.NET 8+ and xUnit 1.9.2+**, including retained xunit 2.9.3.
  [4.0][xunit-adapter-40] adds newer v3 support without requiring v3;
  [4.0.1][xunit-adapter-401] fixes small thread-multiplier handling. Do not
  reject it just because its major changed. Original-major fallback 3.1.5 is
  unchanged; newest eligibility still requires full Windows runner evidence.
- **Collector 10.1.0:** [supplier requirements][coverlet-readme] are VSTest,
  SDK >=8.0.112 and Test SDK >=17.12.0, already within the reported envelope.
  [Changes][coverlet-release] address coverage/instrumentation and process
  exit races. No dependency requirement forces a Test SDK/adapter merge.
  **Defer collection acceptance:** this repository documents no sanctioned
  collector-enabled invocation. An ordinary test pass does not exercise
  collection; do not import an upstream command as a new repository gate.

The four `current` IDs were independently rechecked. [Monaco notes][monaco]
identify its existing editor/TFM support; [Shouldly 4.3.0][shouldly] is still
the latest listed; [xUnit 2.9.3][xunit-v2] anticipates the end of v2 releases
but does not authorize replacing the framework. The ini-parser supplier URL
advertised by NuGet returned 404 during README lookup; no new supplier
migration guidance was verified for that current ID. The stable-less
[Zulip supplier README][zulip] explicitly calls its library early development.
Neither stable-less package is advanced, removed or replaced here.

## Phase-1 hand-off and remaining unit-specific local evidence

1. **Baseline prerequisite recorded:** the fresh unchanged-source Windows
   commands all exited 0, diagnostics are attributed, and all 36 original
   graphs are identified. The full aggregate display-name fingerprint and
   individual TFM totals are recorded without inventing numeric discovery or
   per-TFM name/class attribution. Parent owns phase acceptance/status and
   commit; this continuation does not mark phase 1 complete.
2. **Assessment map frozen before trials:** P11 is the required API/SDK unit;
   P19 is the aligned model cohort; P24 trials 4.14.5 after the 5.0.1 broad
   migration cap, conditionally assessing 4.14.4 if Hosting remains 10.0.9.
   P25/P26 still independently assess newest assets 5.0.1 against an accepted
   Components prerequisite. Retained/capped prerequisites must be rechecked
   downstream, especially P17, P23 and the two asset units. No candidate
   acceptance follows from a unit merge, cap or original baseline pass.
3. **SDK/package/UI local prerequisites:** use the existing isolated
   [browser checklist](../sdk6-compatibility.md#browser-checklist).
   Continuation read-only checks found no cache hit for
   `hl7.fhir.uv.subscriptions-backport#1.1.0` and
   `hl7.fhir.us.davinci-cdex#2.0.0` in
   `~/.fhir/packages/<id>#<version>`, `~/.fhir/packages/<id>/<version>`,
   `~/.fhir/<id>#<version>` or `~/.fhir/<id>/<version>`. This is not proof of
   universal absence. An isolated cache/receiver and actual browser/RI results
   still need to be supplied for the affected units, or those units retained
   with a specific evidence gap; none was created or run in this continuation.
   Use synthetic source fixtures, including an appropriately shaped CDex Task
   with `code`, not live clinical examples. Do not activate dormant PAS/pickers.
4. **UI delivery:** browser presence is not rendering/event/static-asset
   evidence. Applicable packaged/installed-tool asset verification lacks a
   sanctioned local route here; do not run the publishing workflow or invent
   a pack/install command. Resolve or defer the affected UI candidate.
5. **Transport/lifecycle:** MCP needs real local HTTP negotiation/calls;
   telemetry needs enabled/disabled signals, actual configured transports and
   flush/disposal (HTTP/protobuf is not gRPC evidence); mail needs controlled
   SMTP/TLS/authentication without real accounts or trust-policy relaxation.
   Availability/results for these facilities are unverified, not failures.
6. **Collector:** keep P30 assessment/deferral-only unless separately
   sanctioned collection guidance is supplied and that pending phase revised.

Missing browser/offline/transport/packaging/collector evidence defers only
affected units. It does not make all candidates unsafe or block independent,
locally verifiable units. Missing Linux/macOS hosts are post-pass follow-up,
never a reason to defer a Windows-verifiable upgrade.

## Public sources and reproduction boundary

The initial inventory used read-only public metadata, supplier
documentation/source and local source/test contracts. This narrow continuation
reused that research and added read-only inspection of the parent's fresh
assets, installed `.nupkg.metadata` and build/static descriptors, plus local
prerequisite existence checks. The parent executed the original baseline
commands recorded above; these documentation slices did not run a candidate
restore, install a package/tool, download clinical examples or access real
credentials. Inventory checks used `git ls-files`, XML/JSON inspection and
targeted source searches; the initial public metadata research used HTTP GETs.

For every package-table ID/version, the verified NuGet endpoints have these
forms (lowercase package ID):

- `https://api.nuget.org/v3-flatcontainer/{id}/index.json`
- `https://api.nuget.org/v3/registration5-gz-semver2/{id}/{version}.json`
  (`listed`, publication date and catalog link)
- `https://api.nuget.org/v3-flatcontainer/{id}/{version}/{id}.nuspec`
  (framework groups, dependencies and supplier repository)
- The registration leaf's `catalogEntry` supplies public `packageEntries`.
  Library/build/static layouts were additionally checked for SDK R4,
  FluentUI, CLI/binder, Monaco, ini-parser, Shouldly, xunit and runner/collector
  packages without downloading nupkg binaries.

Relevant transitive nuspecs were also read: Base/Conformance 6.2.1 and 6.7.0,
ProviderBuilderExtensions 1.16.0 and 1.20.0, MCP.Core 2.2.0 and MimeKit
4.18.1. These public checks establish declared constraints, not a full
vulnerability audit, offline-feed equality, or candidate resolution/acceptance.
The separately identified fresh assets establish original resolution only.
Failed guessed documentation URLs were not used as evidence; the working
supplier sources below were read on the assessment date.

[dotnet-servicing]: https://raw.githubusercontent.com/dotnet/core/main/release-notes/10.0/10.0.13/10.0.13.md
[dotnet-9]: https://raw.githubusercontent.com/dotnet/core/main/release-notes/9.0/9.0.21/9.0.21.md
[dotnet-8]: https://raw.githubusercontent.com/dotnet/core/main/release-notes/8.0/8.0.32/8.0.32.md
[cli-migration]: https://learn.microsoft.com/en-us/dotnet/standard/commandline/migration-guide-2.0.0-beta5
[identity-release]: https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet/releases/tag/8.23.0
[identity-support]: https://raw.githubusercontent.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet/8.23.0/supportPolicy.md
[otel-versioning]: https://raw.githubusercontent.com/open-telemetry/opentelemetry-dotnet/core-1.20.0/VERSIONING.md
[otel-release]: https://raw.githubusercontent.com/open-telemetry/opentelemetry-dotnet/core-1.20.0/RELEASENOTES.md
[otel-aspnet]: https://raw.githubusercontent.com/open-telemetry/opentelemetry-dotnet-contrib/3aaa7709178529748ade65555422dd14c55be6c9/src/OpenTelemetry.Instrumentation.AspNetCore/CHANGELOG.md
[firely-multiple]: https://docs.fire.ly/projects/Firely-NET-SDK/en/latest/model/multiple-fhir-versions.html
[firely-readme]: https://raw.githubusercontent.com/FirelyTeam/firely-net-sdk/v6.7.0/README.md
[firely-63]: https://github.com/FirelyTeam/firely-net-sdk/releases/tag/v6.3.0
[firely-65]: https://github.com/FirelyTeam/firely-net-sdk/releases/tag/v6.5.0
[firely-66]: https://github.com/FirelyTeam/firely-net-sdk/releases/tag/v6.6.0
[firely-67]: https://github.com/FirelyTeam/firely-net-sdk/releases/tag/v6.7.0
[packages-60]: https://github.com/FirelyTeam/Firely.Fhir.Packages/releases/tag/v6.0.0
[packages-62]: https://github.com/FirelyTeam/Firely.Fhir.Packages/releases/tag/v6.2.0
[fluent-migration]: https://github.com/microsoft/fluentui-blazor/blob/v5.0.1/examples/Demo/FluentUI.Demo.Client/Documentation/GetStarted/Migration/MigrationIndex.md
[fluent-pins-5]: https://raw.githubusercontent.com/microsoft/fluentui-blazor/v5.0.1/Directory.Packages.props
[fluent-pins-4144]: https://raw.githubusercontent.com/microsoft/fluentui-blazor/v4.14.4/Directory.Packages.props
[fluent-icons-project]: https://raw.githubusercontent.com/microsoft/fluentui-blazor/v5.0.1/src/Assets/FluentUI.Icons/Components.Icons.Package.csproj
[fluent-emoji-project]: https://raw.githubusercontent.com/microsoft/fluentui-blazor/v5.0.1/src/Assets/FluentUI.Emojis/Components.Emoji.Package.csproj
[mcp-20]: https://github.com/modelcontextprotocol/csharp-sdk/releases/tag/v2.0.0
[mcp-22]: https://github.com/modelcontextprotocol/csharp-sdk/releases/tag/v2.2.0
[mcp-141]: https://github.com/modelcontextprotocol/csharp-sdk/releases/tag/v1.4.1
[mail-release]: https://raw.githubusercontent.com/jstedfast/MailKit/4.18.1/ReleaseNotes.md
[vstest-release]: https://github.com/microsoft/vstest/releases/tag/v18.11.0
[xunit-adapter]: https://raw.githubusercontent.com/xunit/visualstudio.xunit/4.0.1/README.md
[xunit-adapter-40]: https://xunit.net/releases/visualstudio/4.0.0
[xunit-adapter-401]: https://xunit.net/releases/visualstudio/4.0.1
[coverlet-readme]: https://raw.githubusercontent.com/coverlet-coverage/coverlet/v10.1.0/README.md
[coverlet-release]: https://github.com/coverlet-coverage/coverlet/releases/tag/v10.1.0
[monaco]: https://raw.githubusercontent.com/serdarciplak/BlazorMonaco/8a02d23d3b208f0a98373618269b6c8943e9d7b7/CHANGELOG.md
[shouldly]: https://github.com/shouldly/shouldly/releases/tag/4.3.0
[xunit-v2]: https://xunit.net/releases/v2/2.9.3
[zulip]: https://raw.githubusercontent.com/GinoCanessa/zulip-cs/7a1856a36a76b33efb9c96f23cea1aee5229fcba/README.md
