# xunit.runner.visualstudio — Windows unit verified

**2026-10-16, phase 29: accept 3.1.5 → 4.0.1; retain xUnit 2.9.3 and VSTest.**
Predecessor **`be8e9c6d4774b50bc4f9d3368b06d1d1cb6cb9bf`**; initial index and tracked/owned paths clean.
Authority read: [AGENTS.md](../../../AGENTS.md), phase29/unit contract/T-runner/T-config/V-accept/V-windows-full, [Test SDK](test-sdk.md), [TestHost](testhost.md), and [build/test procedures](../building-and-testing.md).
Only the adapter pin and this dossier change. This is unit acceptance on Windows, not final plan completion or cross-platform approval; integration and archival remain parent-owned.

## Fresh availability, support and prerequisites

The [NuGet index][index] still identifies **4.0.1** as highest stable; fresh [registration][registration]/catalog mark it listed, published **2026-10-09 17:08:27.827 UTC**.
Original **3.1.5** remains listed and is also the newest stable in its original major. No capped-version trial was necessary.
Both downloaded nuspecs and [4.0.1 tagged README][readme] explicitly support xUnit v1/v2/v3, .NET 8+ and .NET Framework 4.7.2+; the adapter major is **not** a test-framework migration.
Package repository commits: 3.1.5 `1b188a7b0a069d7fc94ae3c0b251f1302b602b63`; 4.0.1 **`0f7d528729972578553fb146cbde5f95b316cdc4`**.
Archive base: `https://api.nuget.org/v3-flatcontainer/xunit.runner.visualstudio/{version}/xunit.runner.visualstudio.{version}.nupkg`.
Archive SHA-256: 3.1.5 `3b9eb9efcf3840695db33b045901420d14d7562141999e06182fb8894faeb124`; 4.0.1 **`73d8d9a69b8b405f6b0fa246afedfc3afb87061589f972b250fa4340ff469ff5`**.

| Boundary | Eligibility and retained prerequisites |
|---|---|
| Adapter 4.0.1 | Empty net8 dependency group applies to all three targets. ObjectModel >=17.13.0 is only a net472 nuspec edge, not a new direct requirement here. |
| Supplier build/source | Targets net472/net8; builds against ObjectModel 17.13.0. Tagged source retains a v2 sample and v2 reflection-controller execution. Embedded runner utility/common 4.0.2 are adapter implementation, not project xUnit v3 packages. |
| Microsoft.NET.Test.Sdk | Retain **18.11.0**, with TestPlatform.TestHost, ObjectModel and CodeCoverage **18.11.0** from phase 28. Actual build/execution verifies this combination; no minimum-range-as-equality assumption. |
| xunit / collector | Retain **2.9.3 / 10.0.1**, all framework/abstractions/analyzer nodes. Collector's packaged/tagged guide requires SDK >=8.0.414 and Test SDK >=18.4.0, already satisfied. |
| ASP.NET Core TestHost | Retain framework-conditioned **10.0.13 / 9.0.21 / 8.0.32**, independently of TestPlatform.TestHost; no co-update. |

[4.0.0 notes][release4] add v3 4.0/schema/unpublished Native AOT support and fix shutdown message ordering with surviving foreground threads.
[4.0.1 notes][release401] fix tiny thread multipliers yielding zero by enforcing at least one thread. This repository supplies no multiplier; no compensating thread override is appropriate.
The supplier mentions possible future adapter deprecation, not a current requirement to switch to MTP. No new architecture, package, runner flag, global.json or runtime override was needed.
Collector guidance has an MTP/.NET 10 caveat; it does not authorize altering this VSTest project. Ordinary test passes here **do not certify coverlet/CodeCoverage collection** or published/AOT execution.
Windows **10.0.26200 win-x64**, SDK **10.0.400**, MSBuild **18.9.6**; adapter banners confirm .NET **10.0.11 / 9.0.19 / 8.0.30**.
Installed ASP.NET Core patches match those runtimes. Runtimeconfigs still request .NET/ASP.NET Core **10.0.0 / 9.0.0 / 8.0.0**, without patch overrides.
No candidate servicing-patch uplift was declared or needed for execution; these installed patches are not claimed current for supplier security/support.

## Exact source, configuration and asset identity

Source maps: sorted compact JSON of git-relative `src/` path → raw-file SHA-256, UTF-8, `ensure_ascii=False`, no final LF; **384 files / 47,054 bytes**.
Baseline **`13cdb81907fb2e1214fe65c155a0925b8e7fd19c1b8101355da1f41618291764`** → candidate **`49d74c79a2e1418a52ebc6303cae01f35ffbfb35d62ebff6dcd7f8d896ec5cf3`**; post-full identity unchanged.
Executable patch (`git diff --no-ext-diff --binary -- src`): **761 bytes**, SHA-256 **`24d69f34e3cb4e76959446695b56ceb5bbba768c0451cc2b1a90e4d6ea18eb71`**.
Raw csproj: **4,952 bytes** both; `48979e3aff41cbd208a319dd4d6c9f6b788bdc96b54065409a9c435a1802a91e` → `b416f20955580cd4cc1e279379046381a75046459325369d7744b6061193a1cd`; BOM/110 CRLF lines preserved.
Exact byte substitution and namespace-aware XML comparison prove **46 references / 36 IDs / seven owners** differ only in this pin. Conditions, aliases/AddPackageAliases, references, fixture/resource copies and exclusions are intact.
Adapter/collector both preserve `PrivateAssets=all` and `IncludeAssets=runtime; build; native; contentfiles; analyzers; buildtransitive`; no asset propagation policy change.
Test-data map unchanged: **101 files / 13,498 bytes**, SHA-256 **`c2af1ed1ad3ac8fb69932e76fc35f62c63d27f8aa5ccb03eb1812ad72cf824f4`**. No test, assertion, fixture or production-source edit.

`xunit.runner.json` remains **schema-only**, byte-identical in all outputs: SHA-256 `1631a08eb6ec9a58b9ea094f5960149738dfdc932731af1003ef8dd546603fe2`.
Repository inspection found no assembly-wide CollectionBehavior, thread-limit, parallel-algorithm, pre-enumeration or runsettings override.
The existing **Compatibility hosts** collection alone has `DisableParallelization=true`: routing, hosting, MCP transport, telemetry, SDK client/contracts/notifications/packages/UI retain membership.
Other collections retain framework defaults: collection parallelism enabled, same-collection cases serial, conservative scheduling, CPU-count thread limit, assembly parallelization opt-in false.
Tagged [VsTestRunner][runner] loads configuration/runsettings and still defaults `PreEnumerateTheories` to true in discovery and execution; legacy FhirStoreTests MemberData and InlineData rows remain expanded.
Embedded utility source at **`0c7aa4991fa340689155d52043ce88f52f2f7421`** preserves configuration defaults and forwards v2 options; its v2 controller bulk-deserializes and calls the retained framework executor without thread/parallel overrides.
4.0's ParallelMode/config plumbing and ID-based support for unserialized newer-framework cases do not migrate these v2 serialized cases. Full exact occurrence comparison below verifies no case loss or display drift.
This is configuration/source plus real-suite evidence, **not an instrumented measurement of worker counts or scheduling timing**, nor a test of unused multiplier settings. No blanket disable or new test/runner flags masked a regression.

Both full package-only snapshots cover **12 projects / 36 targets / 946 nodes / 387,338 bytes**:

| Snapshot | SHA-256 |
|---|---|
| Original existing assets, reused without restore | **`7a050cb6e89c4ce0c618aca5fd2055c135ee9a8e1cf7dac44c77fef7a1e489f8`** |
| Candidate after documented restore | **`1069cacc596aab8d664278b593c251fe746fbfea494d6af5c55876d45eb4a464`** |

Normalization: sorted compact JSON, slash-separated git-relative `csproj|target` → **full package-only nodes**, all metadata retained, `ensure_ascii=False`, UTF-8, no final LF.
**Complete graph delta:** only Tests' three targets replace `xunit.runner.visualstudio/3.1.5` with `/4.0.1`; node payloads are identical. Every other package key/edge/asset is equal.
Adapter compile/runtime remain `lib/net8.0/_._`; build remains `build/net8.0/xunit.runner.visualstudio.props`. No added MTP, analyzer, protocol, native/RID or transitive package.
Generated imports differ only in the three adapter version paths and matching Exists paths; TFM/ExcludeRestorePackageImports conditions, all other props/targets and SDK outer imports are equal.
Restored project framework objects differ only in the adapter direct range. SDK/TestPlatform net8 libraries/props, CodeCoverage net8 shim/netstandard2.0 hooks, collector matching-TFM targets and xUnit assets stay intact.
**Complete archive delta:** both net472/net8 adapter DLLs change; nuspec, README, signature, content-types and relationships change; core-properties filename changes from `45b3730c6f034788875e0fcb562e4b26.psmdcp` to `nuget.psmdcp`.
Both props files, both abstractions DLLs, both `lib/*/_._` placeholders and logo are byte-identical. No other archive entry is added/removed/changed; full file hashes are retained.
Unchanged props copy the adapter and abstractions with PreserveNewest/Visible=false and retain TestContainer capability. All three outputs match candidate package bytes; net8 adapter DLL SHA **`ab3e8b088bd82e55963eedf765f87f6a3bffbe8ccd866d3e15a5f40be5c473a4`**.
Copied testhost/ObjectModel also match retained packages; complete SDK/protocol/collector package-file hashes and build-hook text are archived, not inferred from a package name alone.
Raw Tests assets SHA: `7424afc44f6a61f4849f44491f630d62ff487a921aa9015cf3c83845ffd73354` → `f796feddbef5e163d545fde8e4b8d84b6d7a238dbcb9b70e0a97e8bc3d966a65`.
Clock-derived project version **2026.1016.2011** is expected build metadata, not dependency drift; raw-assets byte identity is not asserted.

## Executed gates and complete inventories

Reused the immediately preceding phase-28 full baseline at the committed predecessor; **no redundant baseline restore/build/test**.
Parent canonical JSON: `C:\Users\ginoc\.copilot\session-state\085617bb-4d9a-475b-99ad-a7708b939326\files\phase27-full-inventory.json`.
Independently re-extracted that same exact inventory from adjacent `phase28-capture-01.log` (19,550,218 bytes; SHA `62ef217aad0b8ed3021146bf34783443496df206f8b4819bd5663c94645313fd`).

| Exact candidate command, repository root | Native/wrapper result |
|---|---|
| `dotnet restore` | Exit 0; 1.61 s; eight existing warning occurrences |
| `dotnet build --configuration Release --no-restore` | Exit 0; 8.90 s native; 24 warnings / 0 errors; all targets, also supplies B/net10 |
| `dotnet test --configuration Release --framework net10.0 --no-restore --verbosity normal --filter "FullyQualifiedName~fhir.candle.Tests.ConfigTests\|FullyQualifiedName~fhir.candle.Tests.StrictModeStartupTests"` | Exit 0; once; 5.49 s native; 108 passed / 0 failed / 0 skipped; eight existing warnings |
| `dotnet test --configuration Release --no-restore --verbosity normal` | Exit 0; once; 11:18.02 native; 24 warnings / 0 errors; also supplies net10 full |

The Markdown-escaped `\|` above is the literal `|` in the quoted filter. Smoke: **99 ConfigTests + 9 StrictModeStartupTests**, 108 unique occurrences / 14,150 normalized bytes.
Smoke SHA **`7683bac4a3ce03256961200bedb65b356d397bb3dd8ebbecc39c6542bc12c92d`**; three copies exactly equal the full-baseline subset.

| Windows target | Reused baseline passed/total | Candidate passed/total | Failed / skipped, both |
|---|---|---|---|
| net10.0 | 4,454 / 4,454 | 4,454 / 4,454 | 0 / 0 |
| net9.0 | 4,454 / 4,454 | 4,454 / 4,454 | 0 / 0 |
| net8.0 | 4,454 / 4,454 | 4,454 / 4,454 | 0 / 0 |

Complete aggregate baseline/candidate multisets are **exactly equal: 13,362 occurrences / 4,451 distinct display names / 2,545,646 normalized bytes**.
Both SHA-256 **`129151667759ffdf40439f8cdeebe49270bb4e601e0b04c3dd15760bc83300a3`**; missing/added lists empty; 4,448 names occur three times and three already-truncated display names occur six times.
Ordinal-sort names, LF-join without final LF, UTF-8 without BOM; preserve parameters and duplicates. No Unicode normalization, ellipsis substitution, deduplication, truncation or fabricated names.
Parser: `r'(fhir\.candle\.Tests\.[^\r\n]*?) \[(?:< )?\d+(?:[.,]\d+)? (?:ms|s|m)\]'`; **prefilter each native line for numeric duration before extraction**, stop at NATIVE_END and exclude appended JSON.
Independent spool re-extraction reproduces the full count/hash. Interleaved output supplies aggregate names and observed per-target execution summaries, not TFM-labelled name hashes or numeric discovery totals.
Warnings match the baseline multiset exactly: **48 printed / 24 reported**, solely Pkcs NU1902/NU1903, versions 10.0.0/8.0.1, advisories GHSA-2fmv-rv2c-59x7 and GHSA-846f-f764-76mx. No new diagnostic or suppression.

## Native retention, cleanup and limits

Native stdout/stderr were captured in memory, strictly UTF-8 decoded and emitted to automatic tool spools; no redirected/transcribed files. Parent must archive these actual files as phase29 evidence.
All following literal filenames are beneath **`C:\Users\ginoc\AppData\Local\Temp\`**; hashes identify archival bytes, separately from native-stream hashes.

| Evidence | Actual spool filename | SHA-256 |
|---|---|---|
| Full native + complete JSON inventory/comparison | `1792200236123-copilot-tool-output-33128-bd0d0d79-fa92-486f-a67a-9259e6fea274.txt` | `3ea280681eed9378950699d87b57e800c6bfd5a623642e334012b746dd999e7f` |
| Smoke native + complete inventory | `1792199522303-copilot-tool-output-33128-ef6ee10d-6030-4ba3-b5b8-105794996091.txt` | `13d08523e5c8c72948e8be5d6a789356bfdec8c971b7d3cbbffcb8fc2926727c` |
| Full original graphs/source/data/imports | `1792199381188-copilot-tool-output-33128-08fae2e8-56bd-443a-b785-901f178fbae4.txt` | `7250b2248e837d2c872bd764d33fc6ac3b0cab47ab65caa621a812c7e761daaa` |
| Full candidate graphs/delta/source/imports | `1792199516664-copilot-tool-output-33128-21e69079-26a1-4cdf-823e-ba29e62b5e64.txt` | `3979e4d0eee364f2c7b216b106606c51d60ce3efd509d38e0d9c8d1766f3e0ee` |
| All direct metadata/package-file hashes/copied assets | `1792199558910-copilot-tool-output-33128-b3b443a2-c8bb-42cf-b624-e4ebc3d921e3.txt` | `727d7f49dc6cddd3cb31d40b82b79d105219424a15d96a12dd9d551ad9afe5a7` |
| Fresh NuGet/nuspec/archive/source metadata | `1792199382075-copilot-tool-output-33128-fc8e5377-a8e9-47b6-918f-6eff502822e1.txt` | `2cd80db7c03ce9d65e5a45010acde265aa235d69c3a251c1c181cf3514237f7a` |
| Releases/tagged adapter source/defaults/collector guidance | `1792199427460-copilot-tool-output-33128-f53cb548-d7a4-48a3-8c42-9606faa6fce1.txt` | `548c72472c87dd65f645dc1313368de53fd6dbf04aea179efeb06953123a3a9a` |
| Embedded utility configuration/options source | `1792199468002-copilot-tool-output-33128-a35b8fd0-9b8f-46d8-825d-19cb10d761dc.txt` | `abfc2e7a7cba2fd84cfc9230751ff2b6de109613b19d8737c977d40974133059` |
| Concrete v2 controller source | `1792200285030-copilot-tool-output-33128-b3ac2846-2ae5-44b8-94f8-3da8387b6a53.txt` | `0241b1cf6db90f6e6abd4a1badd8ade7b4fd838d490f760d8b69cd8204f437c8` |

Full native: **16,906,476 bytes**, SHA `6cabdbcecde584e68e782362a8a6a24f21aee9c4915b3c81580c16a0b734af6f`; full archival spool **19,554,254 bytes** includes JSON.
Smoke native: **121,420 bytes**, SHA `dc9b6e5bf000ef77f7fbd4eae9c72d551138d80ae3bc6cb0ff3d16aa131bfb02`.
Restore/build native output remains in completed tool call **950**: SHA `63857aa7f81b0faa932165208a6a33e1c0c8e3b62aa31ac786dbed6c62fcf8cb` / `dabc287e610bc2a384ccb4b3a9d2838583e67f56473ba98419430e0bcd588582`.
Transport can normalize CRLF. Both full native logs emit 5,136 U+FFFD in non-case diagnostics; zero duration/name lines contain them. Strict decoding and exact inventory comparison required no replacement.
Inspection-only setbacks: two metadata/source text-extraction scripts exited 1; structured/range reads corrected them. Namespace-aware XML inspection completed the initial unqualified-element inventory. No restore/build/test retry or failed candidate gate.
Initial variable-PID cleanup was rejected before execution; literal PIDs were then ownership-verified. Build parent 137808's twelve nodes **134968, 5096, 61200, 46788, 120192, 44608, 90844, 27284, 126476, 94596, 37488, 95040** were stopped individually by explicit IDs.
Restore/build/smoke/full root PIDs **109860 / 137808 / 82700 / 137204** and testhosts exited; final checks found no owned descendant/testhost or compatibility temp directory. No unrelated process was killed.
No lower-count green, changed expectations, skip/quarantine, additional direct pin or relaxed threshold. Required out-of-owner repair/co-update/architecture change would have stopped this unit, not been forced through.
No plan/unowned file edit, stage, commit, push, PR or delegation. **Final Windows roll-up and all six Linux/macOS × net10/net9/net8 follow-up cells remain pending.**

[index]: https://api.nuget.org/v3-flatcontainer/xunit.runner.visualstudio/index.json
[registration]: https://api.nuget.org/v3/registration5-gz-semver2/xunit.runner.visualstudio/4.0.1.json
[readme]: https://raw.githubusercontent.com/xunit/visualstudio.xunit/4.0.1/README.md
[release4]: https://xunit.net/releases/visualstudio/4.0.0
[release401]: https://xunit.net/releases/visualstudio/4.0.1
[runner]: https://raw.githubusercontent.com/xunit/visualstudio.xunit/4.0.1/src/xunit.runner.visualstudio/VsTestRunner.cs
