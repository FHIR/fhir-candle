# Framework-conditioned TestHost — Windows unit verified

**2026-10-16, phase 27: accept 10.0.13 / 9.0.21 / 8.0.32 for net10.0 / net9.0 / net8.0, respectively.**
Predecessor and fresh assembled-prefix baseline: `de5d2d4afbc62a11f9dbb5818e9cf5d771bfeb7c`; initially clean tracked tree and index.
Authority read: [AGENTS.md](../../../AGENTS.md), phase27/T-http/V-accept/V-windows-full, [initial dossier](README.md), and [build/test procedures](../building-and-testing.md).
Only the three conditioned TestHost references and this record change. No factory, routing-test, production-source, fixture or runner adaptation was necessary.
This is the **TestHost unit's Windows pass**, not the final plan roll-up or cross-platform approval. Parent integration/commit remains pending.

## Fresh availability, supplier requirements and runtime eligibility

The public [version index][index], registration/catalog entries and both old/new nuspecs were read afresh.
All three selected versions are the highest stable in their corresponding major, listed, published **2026-10-13**; no prerelease or highest-ID-on-all-targets substitution.
Registration URL: `https://api.nuget.org/v3/registration5-gz-semver2/microsoft.aspnetcore.testhost/{version}.json`.
Nuspec/archive base: `https://api.nuget.org/v3-flatcontainer/microsoft.aspnetcore.testhost/{version}/`.

| Test target | Previous → accepted | Published UTC | Declared package dependencies | Actual test runtime |
|---|---|---|---|---|
| net10.0 | 10.0.9 → **10.0.13** | 17:07:33.553 | Empty native net10 group | .NET / ASP.NET Core 10.0.11 |
| net9.0 | 9.0.17 → **9.0.21** | 17:18:59.597 | Empty native net9 group | .NET / ASP.NET Core 9.0.19 |
| net8.0 | 8.0.28 → **8.0.32** | 17:13:23.543 | System.IO.Pipelines **[8.0.0, )**, excluding Build/Analyzers | .NET / ASP.NET Core 8.0.30 |

All six nuspecs require the matching `Microsoft.AspNetCore.App` framework; none declares a minimum servicing-patch runtime or SDK uplift.
Each candidate archive contains only its matching-TFM TestHost DLL/XML plus package metadata/notices; no consumer build hook, analyzer, native/RID or static-web asset.
Archive SHA-256, net10/net9/net8: `bb4f9fb3f9c554c72c2e5975f8e9097f6de98c91bb51c73c382ab889c49a956b`, `09bed84535742fff7808859dc1572961bfd8c79af3a9862f44605f2338b31c2a`, `4e4d5a57732aaef624bfa91e4942ee365cb7b0ae1b3d3a644ac021304aa8a18d`.
Supplier [10.0.13][release10], [9.0.21][release9] and [8.0.32][release8] notes identify October cumulative security/non-security servicing, not a TestHost runner migration.
Version-tagged `dotnet/aspnetcore/v{version}/src/Hosting/TestHost/src/` comparisons found `TestServer.cs`, `ClientHandler.cs`, `HttpContextBuilder.cs` and `WebHostBuilderExtensions.cs` byte-identical within each old/new pair.
This bounded API/lifecycle review is not a claim that the entire ASP.NET Core release is unchanged.
[Supplier support][support]: net10 LTS through **2028-11-14**; net9 STS and net8 LTS through **2026-11-10**. Support requires current servicing patches.
Installed SDK **10.0.400**, Windows **10.0.26200 win-x64**; the installed runtime patches above are older than October servicing and are **not claimed current for supplier support/security**.
Generated test runtimeconfigs still request `Microsoft.NETCore.App` and `Microsoft.AspNetCore.App` **10.0.0 / 9.0.0 / 8.0.0** without a patch override; actual runner banners confirm the installed .NET versions above.
Metadata alone was not accepted as compatibility proof: candidate build, all-target execution and focused HTTP execution succeeded on this installed environment.
No missing-runtime failure occurred; no SDK/runtime/tool installation, runtimeconfig override or roll-forward workaround was used.

## Source, consumer and asset identity

Source identity hashes normalize a sorted compact JSON mapping git-relative `src/` path to raw-file SHA-256, UTF-8, `ensure_ascii=False`, no final LF.
The baseline/candidate maps cover **384 files / 47,054 bytes**: `f734bad579f247ae79b5ed12f62d3989d04a226f0c82d9792029ee23b31551d1` → `1a740ea10586054c37574e12ada0f815d636a079efad92eb8d02b8813aa353b6`.
Executable patch (`git diff --no-ext-diff --binary -- src`): **1,184 bytes**, SHA-256 `ed7ffdec61c102a0593c48dc1ec7359d9b8d293c901d0c4d34454d85a25e34c4`.
Raw test csproj: **4,950→4,951 bytes**, `697846ecfe3503259a215bc399fcf42e58b49172fc0fb84e3ea00982e215f649` → `58716714a5c70fe22eb03fd35a8857de2f1abd18854041f872ef3a8d5b8694fc`; BOM/110 CRLF lines preserved.
Test-data map remains **101 files / 13,498 bytes**, SHA-256 `c2af1ed1ad3ac8fb69932e76fc35f62c63d27f8aa5ccb03eb1812ad72cf824f4`.
Exact substitution comparison with HEAD confirms **all other csproj content unchanged**: conditions, aliases, AddPackageAliases, project references, fixture copies, resources, exclusions and asset/privacy metadata.
All **46 direct references / 36 IDs / seven owners** retain their declarations except this vector. Test SDK **18.7.0**, adapter **3.1.5**, xunit **2.9.3**, collector **10.0.1** remain separate.
`CandleWebApplicationFactory` still owns isolated tenants/source/cache, uses UseTestServer/CreateClient, stops/disposes its app/stores and deletes only its owned directory.
Existing `Sdk6ContractTests` verify fresh replay state, post-disposal rejection, default R4 identity, idempotent disposal and preservation of caller-owned directories.
Existing `ValidateRoutingTests` cover JSON/XML proper system/type/instance routes across R4/R4B/R5 and the rejected Parameters URL; no real coverage gap required new tests.
`Program.BuildAppForTesting` omits production hosted-service/MCP/telemetry registration. This factory is **not** evidence for those boundaries; separately committed real-RunServer tests remain intact and execute in the full suite.

Both full package-only graph snapshots cover **12 projects / 36 targets / 946 nodes**:

| Snapshot | Compact JSON bytes | SHA-256 |
|---|---|---|
| Baseline, existing assets reused | 387,301 | `cabfafe0f57e0b4bb6420a0d5c6cdeb3e6773af5ec2a5d9ef1ee0f3862ad402f` |
| Candidate, documented restore | 387,302 | `c3a1f62ad4a2c234d41eaddaa0d8b365a8ee167bbad055474d8ed4915fb143f0` |

Normalization: sorted compact JSON mapping slash-separated git-relative `csproj|target` to **full package-only nodes**, preserving all node metadata, `ensure_ascii=False`, UTF-8, no final LF.
**Complete package delta:** only Tests/net10 TestHost10.0.9→10.0.13, Tests/net9 TestHost9.0.17→9.0.21 and Tests/net8 TestHost8.0.28→8.0.32 keys change; their node payloads are identical. All other package nodes/edges/assets are equal.
Compile/runtime paths stay `lib/{matching TFM}/Microsoft.AspNetCore.TestHost.dll`, related XML and framework reference unchanged. Restored Tests framework objects differ only in the three direct version ranges.
Pipelines remains **10.0.13** on Tests/net9/net8 with matching-TFM libraries, absent as a package on net10; net8's >=8.0.0 edge already resolves above minimum. No transitive co-update or new direct pin.
Copied candidate TestHost DLLs match the restored package DLL bytes on all three targets. Runner build/resource assets and FHIR aliases remain unchanged.
Raw Tests assets SHA-256: `eb9b0ff700c0a05cd5ed557a39f4766f73a4073cf0d61345a45101ed81fe01bd` → `0b4eba38893b3f17af56a62b45bebc62058e88bb6072efa85a56e84bcb4db2d2`.
Raw asset hashes are not package-only hashes: the repository's clock-derived project versions regenerate on restore (candidate **2026.1016.1924**); no byte-identical raw-assets claim.

## Executed Windows gates and complete occurrence comparison

Commands ran from the repository root. Baseline was established **before pins**, not reused from the original 4,306-case phase-1 run.
Baseline restore was deliberately omitted: unchanged manifests and available assets; neither a redundant restore nor a fresh-restore claim.

| Exact command | Baseline | Candidate |
|---|---|---|
| `dotnet restore` | Omitted/reused assets | Exit 0 after manifest change |
| `dotnet build --configuration Release --no-restore` | Exit 0; 24 warnings, 0 errors | Exit 0; 24 warnings, 0 errors |
| `dotnet test --configuration Release --no-restore --verbosity normal` | Native exit 0; 11:18.60 | Exit 0; 11:21.74 |

These attributable all-target builds supply B's net10 branch (`dotnet build --configuration Release --framework net10.0`); no duplicate build/implicit restore was run.
Candidate **T-http**, once, native/wrapper exit **0**, **7:26.31**, **3,106 passed / 0 failed / 0 skipped** (26 ValidateRoutingTests + 3,080 Sdk6ContractTests), 8 existing warnings:

```powershell
dotnet test --configuration Release --framework net10.0 --no-restore --verbosity normal --filter "FullyQualifiedName~fhir.candle.Tests.Controllers.ValidateRoutingTests|FullyQualifiedName~fhir.candle.Tests.Sdk6ContractTests"
```

| Windows target | Baseline passed/total | Candidate passed/total | Failed / skipped, both |
|---|---|---|---|
| net10.0 | 4,454 / 4,454 | 4,454 / 4,454 | 0 / 0 |
| net9.0 | 4,454 / 4,454 | 4,454 / 4,454 | 0 / 0 |
| net8.0 | 4,454 / 4,454 | 4,454 / 4,454 | 0 / 0 |

Baseline and candidate complete aggregate inventories are **exactly equal**: **13,362 occurrences / 4,451 distinct display names**, 4,448 names occurring three times and three occurring six times.
Both normalized inventories: **2,545,646 bytes**, SHA-256 **`129151667759ffdf40439f8cdeebe49270bb4e601e0b04c3dd15760bc83300a3`**; added/missing occurrence lists both empty.
Retain full emitted display names/parameter text and duplicate occurrences; ordinal-sort, join LF without terminal LF, UTF-8 without BOM. Do not Unicode-normalize, replace ellipses, deduplicate or synthesize parameters.
Normal all-target stdout interleaves prefixes/newlines. Extraction uses intact `fhir.candle.Tests.*` names ending immediately before numeric `[duration ms/s/m]`, not a line-anchored Passed prefix.
Exact extraction pattern: `r'(fhir\.candle\.Tests\.[^\r\n]*?) \[(?:< )?\d+(?:[.,]\d+)? (?:ms|s|m)\]'`; apply only to the native-output section, not appended normalized inventories.
The first baseline line parser captured only 13,300 occurrences; it was rejected. Re-reading preserved native output recovered all 13,362, with coherent multiplicities; candidate yielded the same complete multiset. No lower-count green or rerun to hide missing cases.
Per-TFM execution totals are observed; names are an **aggregate inventory, not TFM-labelled**. No inferred per-TFM hashes or numeric discovery totals are claimed; xUnit reports discovery lifecycle messages.
Focused inventory: **3,106 unique occurrences / 670,176 bytes**, SHA-256 `75d496fbf1a90a19167c16f2aeb2a25ce064a7b23038feae17f371f9982e6b41`; three copies exactly equal the corresponding full-baseline subset.
No test source/expected value, skip, quarantine, runner, parallelism setting or assertion changed.
Both full logs have the same diagnostic multiset: 48 printed warning occurrences including repeated summaries, 24 reported warnings, solely existing Pkcs **NU1902/NU1903** for **10.0.0/8.0.1**. No suppression or vulnerability-remediation claim.

## Evidence retention, failed attempts and limits

Native stdout/stderr were captured in memory and emitted to the tool's automatic spools; no redirected evidence files, new scripts/projects/packages, or Start-Transcript substitute.
Archive the following **actual tool files** under parent-owned phase27 evidence. All are beneath `C:\Users\ginoc\AppData\Local\Temp\`; names below are literal, not proposed paths.

| Evidence | Actual spool filename | File SHA-256 |
|---|---|---|
| Baseline full native output + capture diagnostic | `1792196578876-copilot-tool-output-33128-b7df28fe-16b9-4810-aa6c-aa33d9fe592a.txt` | `af89f82f5ae97b5542feccde10095f5d86f1f3adc067d6f0065cbd5b3efacaba` |
| Recovered complete baseline normalized inventory | `1792196620395-copilot-tool-output-33128-9633c053-222c-49ea-8ec0-72587a9361bc.txt` | `258610915a228e8d0dfccc9e5a81beab1699d34dafb6b28a2fd7a7118b30e6ac` |
| Candidate full native output + complete normalized inventory/comparison | `1792197381742-copilot-tool-output-33128-c7c002de-50cd-4e85-a5b3-29bae346cc83.txt` | `85d26dcb9c2993ed30df43b47ae2510f827666dcf453ce5c178d3b2c580bd9a5` |
| Candidate focused native output + normalized inventory | `1792197839566-copilot-tool-output-33128-7853cf92-323e-4061-9728-d5b1e92f1bb9.txt` | `6459183e40b135fc5ee7861a2d5a0f1d1fbc7322340aed41c07670b30d0fa03a` |
| Baseline full package graph + source/data maps | `1792195841572-copilot-tool-output-33128-17b0c94f-678e-4b2e-af30-b24d612c3c1d.txt` | `0d54173b93c41adfd9cdf103f515bedc03621362f9aaddd483904a01d6f97552` |
| Baseline direct references/raw hashes/framework metadata | `1792196494475-copilot-tool-output-33128-d26b6855-de8d-4dca-bba6-e30831915fa7.txt` | `8f91b834ca22941b4f0ace62cd3c627353b6951dc0f37554daa2468a617f95b5` |
| Candidate full graph/delta/source map/framework metadata/raw hashes | `1792196677005-copilot-tool-output-33128-47c95258-1ddc-4f04-a7b8-f7c3bbc19a8c.txt` | `d10f7ec6cedaa14906102e275f77583ce04c58db3a49293c29b435efdf2e79f1` |
| Fresh NuGet catalog/nuspecs | `1792195860083-copilot-tool-output-33128-6eb9d35d-ff0f-4b92-95cc-6a76c1c728dc.txt` | `cf08070c2acf934528badbfcb6635334daee355444396c0ac4944c3097be978c` |
| Supplier release/support research | `1792195900116-copilot-tool-output-33128-b8106e1f-9812-47f9-b683-e4524b282dd6.txt` | `a6eb9f1c4932d0d79789beafd3a92057170bf1493b41e4e3cd12dbc3562b783f` |
| Archive inspection/baseline runtime requests | `1792196280581-copilot-tool-output-33128-e05223cb-76bc-4f39-9f43-da0dd9932e41.txt` | `57e4319b3f24e9cc255a6c3ee927b751977e95f872c9e0a1ebe0aed1de54c89c` |

Tool transport normalizes line endings; spool hashes identify archival bytes, separately from pre-transport native-stream hashes. JSON inventory arrays retain all occurrences; the inventory digest hashes the LF-joined array elements, not the JSON wrapper.
Native SHA-256 baseline full / candidate full / focused: `16bba696ea76a3d986bd2508178a060f7d64bbe52599c6d2043a31be39b79397` / `8edc552c08db11d6d730083bc8f354fac9fa6cf9aafe517486bef6445a06f7ee` / `802bee0ee13b1d2057f2099565cdc95da920932fb2baad022a1c65824a45d373`.
Build/restore native output remains in completed tool calls **864 / 881**: baseline build SHA `9561982af22ebd5b26a545ea8465a26f1b533b229f6e9cea6cb29f5f313247b1`; candidate restore `3deab7858ca87193af8f96f90de0a2dcf43125b82d81c454b26a43ef5361ac4a`; candidate build `0c277c13c89f7dca08d4046517fcb822fc7d9ed4316164f345707c2125d52d7b`.
Failed attempts: first NuGet registration decode needed gzip handling; one archive GET timed out and succeeded on bounded retry; guessed TestServers/three component-note paths returned 404, replaced by actual package/tagged sources and release pages.
Baseline native tests exited **0**, but its wrapper exited **1 after emitting the native log** when cp1252 could not print combining-Unicode inventory text. UTF-8 re-extraction fixed capture, not tests; no baseline/candidate test failure occurred.
A tuple/list inspection comparison produced false metadata differences; exact three-substitution and restored-framework comparisons confirmed invariants. One inspection-only Python escape warning did not affect .NET gates.
All started testhosts exited themselves; no owned test runner/helper remains and no `fhir-candle-compatibility-*` directories remained. No user process was killed; no credential/profile or external-service setup was performed.
Plan, other dependency dossiers and all undeclared source owners remain untouched; no stage/commit/push/PR.
This candidate's full Windows identity/inventory may supply later test-infrastructure baseline/final reuse only while source/dependencies remain identical. **Final Windows whole-pass roll-up remains pending; Linux/macOS all three TFMs remain pending post-pass validation.**

[index]: https://api.nuget.org/v3-flatcontainer/microsoft.aspnetcore.testhost/index.json
[release10]: https://raw.githubusercontent.com/dotnet/core/main/release-notes/10.0/10.0.13/10.0.13.md
[release9]: https://raw.githubusercontent.com/dotnet/core/main/release-notes/9.0/9.0.21/9.0.21.md
[release8]: https://raw.githubusercontent.com/dotnet/core/main/release-notes/8.0/8.0.32/8.0.32.md
[support]: https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core
