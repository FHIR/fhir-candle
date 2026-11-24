# Microsoft.NET.Test.Sdk — Windows unit verified

**2026-10-16, phase 28: accept 18.7.0 → 18.11.0; retain xUnit v2/VSTest.**
Predecessor: `7b409017b61b526d2f3e9d8dfaaeffe2c5d8cd46`; initial tracked tree and index clean.
Authority read: [AGENTS.md](../../../AGENTS.md), phase28/unit contract/T-runner/T-config/V-accept/V-windows-full, [testhost.md](testhost.md), and [build/test procedures](../building-and-testing.md).
Only the SDK pin and this record change. This is the unit's Windows acceptance evidence, not final plan completion or cross-platform approval; integration remains parent-owned.

## Fresh eligibility before changing the pin

The [NuGet index][index] still identifies **18.11.0** as highest stable; [registration][registration] marks it listed, published **2026-10-13 13:47:48.83 UTC**.
Fresh SDK/TestPlatform.TestHost/CodeCoverage nuspecs, archives, retained adapter/collector packages, and supplier guidance were inspected before trial.
Nuspec/archive base: `https://api.nuget.org/v3-flatcontainer/{lowercase-id}/{version}/`; registration base: `https://api.nuget.org/v3/registration5-gz-semver2/{lowercase-id}/{version}.json`.
SDK archive SHA-256: original `9b670f13963b950cf59e6ca395a7f86dff4ba400506b6e308a06bb1cf3782e04`; candidate `ff5312a909399817db8ab787139e935c016d65c0a7520b89cc26d775c59f4da7`.

| Boundary | Eligibility and disposition |
|---|---|
| SDK 18.11.0 | Native net8 group requires Microsoft.CodeCoverage and Microsoft.TestPlatform.TestHost **>=18.11.0**; applicable to net8/net9/net10. No new direct declaration. |
| TestPlatform.TestHost 18.11.0 | Native net8 group requires ObjectModel **>=18.11.0**, no longer Newtonsoft.Json. Distinct from ASP.NET Core TestHost. |
| ObjectModel / CodeCoverage 18.11.0 | ObjectModel net8 group has no package dependencies; CodeCoverage has no package dependencies. Restored libraries/build assets cover all three targets. |
| xunit.runner.visualstudio **3.1.5** | [Version-tagged supplier guidance][adapter] supports .NET 8+ and xUnit 1.9.2+. Its net8 group is empty; the >=17.13 ObjectModel edge belongs to net472, not these targets. Retain independently. |
| xunit **2.9.3** | Retain framework and all framework/abstractions/analyzer nodes. [Supplier v2 guide][xunit] documents the SDK + adapter VSTest integration on .NET 8+. |
| coverlet.collector **10.0.1** | Retain independently: no nuspec dependency edges, matching net8/net9/net10 build assets. Packaged and [tagged integration guide][collector] require SDK >=8.0.414 and Test SDK >=18.4.0; candidate satisfies both. |
| ASP.NET Core TestHost | Independently retain **10.0.13 / 9.0.21 / 8.0.32**, each existing TFM condition and every payload unchanged. No framework-host regrouping. |

Fresh auxiliary indexes show adapter 4.0.1 and collector 10.1.0 available; these are other phases' inputs, not required co-updates or trials here.
[VSTest 18.8–18.11 release notes][release] cover Newtonsoft removal, host/serialization fixes, generated-program guards, coverage publish isolation and logging fixes.
[Supplier serializer guidance][serializer] explicitly preserves older-host/new-platform compatibility in both directions; extensions relying on undeclared Newtonsoft are a stated risk.
Here Newtonsoft **13.0.4** remains supplied by Firely.Fhir.Packages/Hl7.Fhir.Base on every test target; no compensating pin is needed.
The [VSTest extensibility contract][contract] supports older extensions on newer platforms; this general contract plus retained-adapter support is eligibility evidence, not a substitute for execution.
18.10's Mono removal affects .NET Framework, not these .NET 8+ targets. 18.11 disables the MTP testhost by default; no MTP package enters the restored graphs.
The collector README's .NET 10 warning concerns MTP, while its example targets net8/net9. [Microsoft's CLI documentation][cli] confirms VSTest remains the default unless selected otherwise.
No global.json/runner opt-in exists here; no TestingPlatformDotnetTestSupport override, MTP flag, new runner, tool or runtime installation was needed.
Collector README minima are older than its packaged integration guide; use the stricter guide above. This assessment does **not** certify collection or published-test execution.

Windows **10.0.26200 win-x64**, SDK **10.0.400**; actual xUnit banners report .NET **10.0.11 / 9.0.19 / 8.0.30**, matching installed ASP.NET Core patches.
Generated runtimeconfigs still request Microsoft.NETCore.App/Microsoft.AspNetCore.App **10.0.0 / 9.0.0 / 8.0.0**, without a patch override.
Candidate metadata requires no higher servicing-patch runtime; all-target execution confirms this installed environment. Installed patches are not claimed current for supplier security/support.

## Exact source, declaration and runner-asset identity

Source maps normalize sorted compact JSON of git-relative `src/` path → raw-file SHA-256, UTF-8, `ensure_ascii=False`, no final LF.
Both maps cover **384 files / 47,054 bytes**: baseline `1a740ea10586054c37574e12ada0f815d636a079efad92eb8d02b8813aa353b6` → candidate **`13cdb81907fb2e1214fe65c155a0925b8e7fd19c1b8101355da1f41618291764`**; post-full identity unchanged.
Executable patch (`git diff --no-ext-diff --binary -- src`): **688 bytes**, SHA-256 **`91575c77fe656e8b8a55669d191011dc5242bcafc2bd1bcad81e798bfeee512f`**.
Raw csproj: **4,951→4,952 bytes**, `58716714a5c70fe22eb03fd35a8857de2f1abd18854041f872ef3a8d5b8694fc` → `48979e3aff41cbd208a319dd4d6c9f6b788bdc96b54065409a9c435a1802a91e`; BOM and 110 CRLF lines preserved.
Test-data map unchanged: **101 files / 13,498 bytes**, `c2af1ed1ad3ac8fb69932e76fc35f62c63d27f8aa5ccb03eb1812ad72cf824f4`.
Exact substitution and XML comparisons confirm all **46 direct references / 36 IDs / seven owners** unchanged except the SDK version; all restored framework metadata differs only in its direct range.
Project references, aliases/AddPackageAliases, fixture/resource copies, exclusions, packability and runner privacy/IncludeAssets metadata remain intact.
Adapter/collector retain `PrivateAssets=all` and `runtime; build; native; contentfiles; analyzers; buildtransitive`; no propagated asset policy changed.
`xunit.runner.json` remains schema-only, copied byte-identically to all outputs: existing theory expansion, default parallelism/thread limits and fixture behavior remain; no blanket disable, assertion/test-source edit, skip or quarantine.

Both full package-only snapshots contain **12 projects / 36 targets / 946 nodes**:

| Snapshot | Compact JSON bytes | SHA-256 |
|---|---|---|
| Baseline, existing assets reused | 387,302 | `c3a1f62ad4a2c234d41eaddaa0d8b365a8ee167bbad055474d8ed4915fb143f0` |
| Candidate, documented restore | 387,338 | **`7a050cb6e89c4ce0c618aca5fd2055c135ee9a8e1cf7dac44c77fef7a1e489f8`** |

Normalization: sorted compact JSON mapping slash-separated git-relative `csproj|target` → **full package-only nodes**, retaining all metadata, `ensure_ascii=False`, UTF-8, no final LF.
Complete delta: only Tests' three targets replace SDK, Microsoft.TestPlatform.TestHost, Microsoft.TestPlatform.ObjectModel and Microsoft.CodeCoverage **18.7.0→18.11.0**.
SDK's two minimum edges and host's ObjectModel edge advance; host's Newtonsoft edge disappears. ObjectModel gains related `.xml` metadata on its main compile/runtime DLL.
All other node keys/payloads/edges/assets are equal; CodeCoverage's node payload is equal despite changed package bytes. No package is added or removed overall.
SDK/host/ObjectModel use net8 assets on all three targets; CodeCoverage uses net8 shim + netstandard2.0 props/targets; adapter uses net8 props, collector its matching-TFM targets.
Generated NuGet imports retain per-TFM/Exists/ExcludeRestorePackageImports conditions and SDK outer-build props. Complete import text, node metadata and package file hashes are archived below.
SDK's GenerateProgramFile now also guards UseWinUI/UseUwpTools; generated Main uses ExcludeFromCodeCoverage instead of TestSDKAutoGeneratedCode. Neither guard changes this non-WinUI/UWP project.
Host props still copy x86 only for Platform/PlatformTarget=x86 on Windows, otherwise x64 on Windows; parent-process-query runtime option still defaults true except UWP.
All three outputs' x64 testhost.exe/DLL, ObjectModel DLL, retained adapter and xunit.abstractions match their restored package bytes.
CodeCoverage now isolates publish artifacts under Microsoft.CodeCoverage/, excludes them from WASM bundles, avoids design-time source-map work and parallelizes its referenced-map build; source-map compilation succeeds here.
Coverage collectors/tools/native resources are package infrastructure, not proof of collection; no coverage/publish procedure was run or invented.
Raw Tests assets: `0b4eba38893b3f17af56a62b45bebc62058e88bb6072efa85a56e84bcb4db2d2` → `7424afc44f6a61f4849f44491f630d62ff487a921aa9015cf3c83845ffd73354`.
Clock-derived project version **2026.1016.1952** is expected build metadata, not dependency drift; no raw-assets byte-identity claim.

## Executed Windows gates and complete inventories

Reused the immediately preceding exact full baseline from [phase 27](testhost.md), on the same committed executable source/environment; no unchanged baseline build/test/restore was repeated.
Parent's baseline JSON: `C:\Users\ginoc\.copilot\session-state\085617bb-4d9a-475b-99ad-a7708b939326\files\phase27-full-inventory.json`; preceding native candidate is `phase27-capture-03.log` in that folder.

| Exact candidate command, repository root | Result |
|---|---|
| `dotnet restore` | Native/wrapper exit 0; 2.52 s; eight existing warning occurrences |
| `dotnet build --configuration Release --no-restore` | Exit 0; 20.35 s native; 24 warnings, 0 errors; all targets, also supplies B/net10 |
| `dotnet test --configuration Release --framework net10.0 --no-restore --verbosity normal --filter "FullyQualifiedName~fhir.candle.Tests.ConfigTests\|FullyQualifiedName~fhir.candle.Tests.StrictModeStartupTests"` | Exit 0; once; 108 passed, 0 failed/skipped; eight existing warnings |
| `dotnet test --configuration Release --no-restore --verbosity normal` | Native/wrapper exit 0; once; 11:18.18 native; 24 warnings, 0 errors; also supplies net10 full |

The Markdown-escaped `\|` above represents the literal `|` in the quoted filter, not an extra command argument.
Smoke: **99 ConfigTests + 9 StrictModeStartupTests**, 108 unique occurrences / 14,150 normalized bytes, SHA-256 `7683bac4a3ce03256961200bedb65b356d397bb3dd8ebbecc39c6542bc12c92d`; three copies exactly equal the full-baseline subset.

| Windows target | Reused baseline passed/total | Candidate passed/total | Failed / skipped, both |
|---|---|---|---|
| net10.0 | 4,454 / 4,454 | 4,454 / 4,454 | 0 / 0 |
| net9.0 | 4,454 / 4,454 | 4,454 / 4,454 | 0 / 0 |
| net8.0 | 4,454 / 4,454 | 4,454 / 4,454 | 0 / 0 |

Complete baseline/candidate multisets are **exactly equal: 13,362 occurrences / 4,451 distinct display names**; 4,448 names occur three times and three truncated display names occur six times.
Both normalized inventories: **2,545,646 bytes**, SHA-256 **`129151667759ffdf40439f8cdeebe49270bb4e601e0b04c3dd15760bc83300a3`**; added/missing lists empty.
Preserve full parameter/display text and duplicates; ordinal-sort, LF-join without final LF, UTF-8 without BOM. No Unicode normalization, ellipsis replacement, deduplication or fabricated names.
Parser: `r'(fhir\.candle\.Tests\.[^\r\n]*?) \[(?:< )?\d+(?:[.,]\d+)? (?:ms|s|m)\]'`; pre-filter lines containing numeric duration, then extract from native output only, never appended JSON.
Independent spool re-extraction reproduces the entire hash/count. Interleaved output provides aggregate names, not TFM-labelled inventories; no inferred per-TFM hashes or numeric discovery counts.
Full warning multisets match exactly: **48 printed / 24 reported**, solely existing Pkcs **NU1902/NU1903**, versions **10.0.0/8.0.1**, advisories GHSA-2fmv-rv2c-59x7 and GHSA-846f-f764-76mx. No new warning or suppression.

## Native retention, retries and limits

Native stdout/stderr were captured in memory, strictly UTF-8 decoded and emitted to automatic tool spools, not redirected files or transcript-only substitutes. Archive these actual files under parent-owned phase28 evidence.
All filenames below are beneath **`C:\Users\ginoc\AppData\Local\Temp\`**; hashes identify archival bytes, separately from pre-transport native hashes.

| Evidence | Actual spool filename | SHA-256 |
|---|---|---|
| Full native + complete JSON inventory/comparison | `1792199097180-copilot-tool-output-33128-62030487-0d99-4fc8-b943-2e69b122d5ed.txt` | `62ef217aad0b8ed3021146bf34783443496df206f8b4819bd5663c94645313fd` |
| Smoke native + inventory | `1792198379009-copilot-tool-output-33128-6f225462-88fe-42a6-b911-12a957dd7ede.txt` | `f3674c3e7d22dd980025616cf725332b378d7adf6078f2e3ae570c325773aced` |
| Full baseline graphs/source/data/direct metadata | `1792198240026-copilot-tool-output-33128-0a75ae22-2dad-4907-826f-187a0069d0ea.txt` | `c065b242e40524fa52183d326f73bd3c6da2ea210cd79d9d1d86e3f0b4c61905` |
| Full candidate graphs/delta/source/imports | `1792198373254-copilot-tool-output-33128-81078da1-ab47-4d9b-9a55-036238cdb5a7.txt` | `fba22311c9f39245278e6766012cc7941205973e308e88522fd5a93affa5db51` |
| Runner package file hashes/copied assets | `1792198418835-copilot-tool-output-33128-2a0b88c8-6793-4d1d-99aa-b8f17ee789c8.txt` | `a09fbdf60f754e86cdbff8a387a562b52c8f2258a49e5ad11008332692e1cb92` |
| Fresh NuGet metadata/nuspecs/archive assets | `1792198215472-copilot-tool-output-33128-4a606187-f260-4017-8113-4e342d62abfe.txt` | `f8f6c43ef08f5db94716bcc0c19b98eeb27a31c4d3fc47039a39c196ea344992` |
| Supplier support | `1792198241005-copilot-tool-output-33128-3b7d86b7-b134-4898-8964-81ae1c650069.txt` | `d26762b268f99bd6fa59323aa0a961cee5dcff319bb67be00e9620f191996449` |
| Release/protocol/collector guidance + build-hook delta | `1792198273761-copilot-tool-output-33128-90f99732-e159-46a1-a817-a66a4aa748f8.txt` | `4bed6df87c980b119ccefeeeb786df6a22023947e8660a60e6607d8e80bc2476` |

Full native stream: **16,903,810 bytes**, SHA-256 `1a1cb661739e5d4278eca3caa5ed376314ac9dcd7eaaa8e1616acd434a611089`; smoke native SHA `6b6483440200349e4e756da92d705a9178ef3680211307c2419d0ea591a5743d`.
Restore/build full native output remains in completed tool call **920**, SHA-256 respectively `6e3af3195a2d88ed09cfb522863a1c2fefb406ed9789b880bd8a50b2cfbc67bd` / `b352f2e8b12af7f90643e80623d300ee69e0f879496cab2b0ab6e46d5ab4e143`.
Transport may normalize CRLF. Both full native logs already emit 5,136 U+FFFD characters in non-case diagnostic text (e.g. malformed search values); zero duration/name lines contain them. Strict decoding and exact Unicode inventory comparison required no replacement.
Inspection-only failures: one guessed supplier support path returned 404; one text-section lookup missed its heading; the first pre-build byte guard compared Git LF against worktree CRLF and stopped before restore/build. Correct CRLF comparison proved the one-pin substitution; no source repair or test retry.
No failed candidate gate, capped-version trial, duplicate test run or lowered acceptance threshold. If a co-update/out-of-owner repair had been required, this unit would stop for regrouping, not add a pin.
Owned full-run PID **75260** and testhosts exited themselves; final process check found none remaining, nor owned compatibility temp directories. No process was killed.
Plan and all unowned files untouched; no stage/commit/push/PR/delegation. **Final Windows whole-pass roll-up and Linux/macOS net10/net9/net8 remain pending.** Ordinary passes do not claim coverlet/CodeCoverage collection.

[index]: https://api.nuget.org/v3-flatcontainer/microsoft.net.test.sdk/index.json
[registration]: https://api.nuget.org/v3/registration5-gz-semver2/microsoft.net.test.sdk/18.11.0.json
[adapter]: https://raw.githubusercontent.com/xunit/visualstudio.xunit/3.1.5/README.md
[xunit]: https://xunit.net/docs/getting-started/v2/getting-started
[collector]: https://raw.githubusercontent.com/coverlet-coverage/coverlet/v10.0.1/Documentation/VSTestIntegration.md
[release]: https://github.com/microsoft/vstest/releases/tag/v18.11.0
[serializer]: https://devblogs.microsoft.com/dotnet/vs-test-is-removing-its-newtonsoft-json-dependency/
[contract]: https://raw.githubusercontent.com/microsoft/vstest/v18.11.0/docs/RFCs/0005-Test-Platform-SDK.md
[cli]: https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-test
