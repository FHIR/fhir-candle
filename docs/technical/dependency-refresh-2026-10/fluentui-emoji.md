# FluentUI Emoji — capped independently to 4.14.4

**2026-10-16, phase 25: accept Emoji 4.14.3 → 4.14.4 for the Windows-local dependency checkpoint.**
Predecessor/source: `555f0b966670ed86aac62780cf5e65662d171ded`; index and worktree initially clean.
Authority: [AGENTS.md](../../../AGENTS.md), phase 25/unit contract/V-accept, fresh [phase-24 evidence](fluentui-components.md).
Only `src/fhir-candle-ui.props`'s Emoji pin changes; Components/Icons stay **4.14.3**, Monaco **3.5.0**.
No source/test adaptation, additional direct pin, forced equality, transitive Components uplift or dormant UI activation.
Executable patch (`git diff --no-ext-diff --binary -- src/fhir-candle-ui.props`): **685 bytes**,
SHA-256 **`e7fdf34d601b8464893b2a446c3979d5d11711ab3b80e5c605c5b0b0f2bdcbae`**.
Raw props: **649 bytes**, CRLF, SHA-256 `d0e8919f718776e5115bf58a3ff00ec16543be7dbb0d1c065100e5242948cdd4`.

## Fresh candidates, support and combination decision

Fresh public [NuGet index][index], registration/catalog entries, nuspecs and all four archives were read on **2026-10-16**.
Registration URL: `https://api.nuget.org/v3/registration5-gz-semver2/microsoft.fluentui.aspnetcore.components.emoji/{version}.json`.
Nuspec URL: `https://api.nuget.org/v3-flatcontainer/microsoft.fluentui.aspnetcore.components.emoji/{version}/microsoft.fluentui.aspnetcore.components.emoji.nuspec`.
All four are stable/listed, with no deprecation/vulnerability entries in the inspected catalogs; this is not a closure security audit.

| Emoji | Publication UTC | Components minimum / supplier build pin | Decision |
|---|---|---|---|
| 4.14.3 original | 2026-06-23 | [4.14.2, ) / 4.14.2 | Baseline, superseded here. |
| **4.14.4 selected** | 2026-07-30 | **[4.14.2, ) / 4.14.2** | Compatible bounded trial passed below. |
| 4.14.5 highest original-major | 2026-09-30 | [4.14.4, ) / 4.14.4 | Retained Components 4.14.3 cannot satisfy minimum; not trialled. |
| 5.0.1 highest stable overall | 2026-10-14 | [4.14.5, ) / 4.14.5 | Same minimum barrier, **not** a major-association/migration rejection; not trialled. |

Version-tagged `Directory.Packages.props` and the [Emoji package project][project] confirm the actual supplier build pins.
Neither Emoji nor Icons depends on the other. The minimum is not equality or blanket binary certification.
The [4.14.4 README][support44] describes best-effort GitHub support, not ASP.NET Core servicing; the later [v4 policy][support4]
says v4 is no longer supported except security updates. [v5 support][support5] is also best effort. This cap is not an LTS/full-support promise.
Supplier nuspec commits: original `3565796c09a63884d7a0d408d08e674d0377c3e5`, selected `ec340baf96cf36dec7b7660c3c7adba91dab051c`.
The exact [source comparison][diff] has **8 commits / 36 changed files**, none in Emoji, its generator or Core's Emoji implementation.
Both non-truncated supplier trees identify `src/Assets/FluentUI.Emojis` as **`0327676ea5384d0dd32faccd8f303ec6e89a6fff`**,
and `src/Assets/Generator` as `08946ee4c3fbe1cb400721ec0018eb3bc570c119`; generated source and packaging projects are identical.
`Directory.Packages.props` and `Directory.Build.targets` are identical; `Directory.Build.props` changes only version fields.
[Release notes][release] primarily describe Components/Icon changes; those packages do not move here.

## Actual consumers, assembly surfaces and gate applicability

Read-only tracked-source search (`git grep -n -i 'emoji' -- src`) finds only the pin and **five commented Razor aliases**.
The five props importers remain host `fhir-candle.csproj`, `FhirCandle.Ui.Common.csproj`, and `FhirCandle.Ui.R4/R4B/R5.csproj`, in their existing directories.
No active `Emoji`/`Emojis`, `FluentEmoji`, `EmojiInfo`, `AllEmojis` or `GetAllEmojis` use was found in that bounded source scope.
`Components/Routes.razor`, `Models/Utils.cs` and `FhirStoreManager.LoadPackagePages` discover only named host/versioned UI assemblies;
store operation/hook reflection stays within the executing store assembly. The performance probe inspects already-loaded assemblies, not Emoji loading.
Retained supplier `ServiceCollectionExtensions.AddFluentUIComponents` registers services/design tokens, not Emoji discovery.
Supplier [Emoji implementation][core] requires explicit construction/rendering; `EmojiExtensions` loads group assemblies only on explicit discovery calls.
The generator emits nested classes whose constructors supply compressed SVG bytes to `Emoji`; no automatic page, JS loader or asset registration is involved.

| Surface / gate | Independently established applicability and result |
|---|---|
| Compile/runtime package assets | Both archives contain the same ten **net8.0 DLL paths**, selected on net10/net9/net8. Actual DLL bytes/version stamps differ. |
| Supplier DLL metadata | PEReader inspection: versions **4.14.3.26174 → 4.14.4.26210**, identical type/method counts and references; all ten have zero module methods/resources. |
| Components binary combination | Nine group DLLs in both releases reference **Components 4.14.2.26138**, plus System.Runtime 8.0.0.0; empty wrapper references only System.Runtime. Unchanged source/API and no used symbols support this combination beyond a minimum-only inference. |
| Actual compiled consumers | All five newly built net10 assemblies have **zero Emoji TypeRefs/AssemblyRefs**; retained Components/Icons and Monaco references remain. This is metadata evidence, not observed rendering. |
| Static assets/build hooks | Archives have no `wwwroot`, static-web-asset descriptors, build/buildTransitive/buildMultiTargeting imports, analyzers or contentFiles. Supplier packaging targets are not shipped consumer hooks. |
| Browser/installed-tool/RI gates | **Not applicable to this delta, not passed or waived:** no changed active rendering/data surface, static payload, hook or transitive dependency. No control was exercised. |

`App.razor`'s Components theme and Monaco scripts/CSS and `Program.cs`'s assembly-relative `staticwebassets`/`wwwroot` selection remain real but unchanged consumers.
The publishing workflow was read, not run; there is no new installed-tool evidence. Phase-24's offline RI/packaged-browser gap does **not** transfer to these unused generated symbols.
If a future change introduces Emoji use, changes an asset/hook or moves Components, reassess the actual browser/installed-asset gates; this record does not pre-qualify that change.

## Complete closure and artifact comparison

Namespace-aware XML comparison with HEAD: **46 direct references / 36 IDs / seven owners**, only the single Emoji version differs; conditions, aliases and asset metadata unchanged.
Full package-node normalization: sorted compact JSON, git-relative slash `csproj|target` keys, `ensure_ascii=False`, UTF-8, no terminal LF.
Original and freshly restored candidate: **12 projects / 36 targets / 946 nodes / 387,301 bytes** each.
Original SHA-256: **`55bdad44a44eac1ad97e18493d6da1a944581ff267b73ad585cd84808fefd1e1`**.
Candidate SHA-256: **`cabfafe0f57e0b4bb6420a0d5c6cdeb3e6773af5ec2a5d9ef1ee0f3862ad402f`**.
Exactly **18 Emoji keys** change 4.14.3 → 4.14.4 (five importers plus tests × three TFMs); their full node bodies are identical.
All other nodes/edges/assets and the other 18 targets are identical: no added/removed package ID or unexpected change, including conditional framework/pruned closure.
All **78 existing staticwebassets JSONs + 24 NuGet generated imports** are byte-identical before/after, with no Emoji entry.
Net10 build assets were refreshed by B; existing publish/pack and net9/net8 manifests are **not** claimed newly built or installed.
Both cached archives and extracted DLLs match fresh public NuGet bytes: original SHA-256 `f1de98afc2cea9587b599d5706f09ba8272fef1a3785e54e1463dd89f974d9c1`,
selected SHA-256 `dacf9c4219c5f2e18f9210538edf6244254049023e2b3ed677229764a1790b51`; both caches identify `https://api.nuget.org/v3/index.json`.
All ten net10 host and ten test output Emoji DLLs match the selected archive; all six consumer `.deps.json` files select 4.14.4.
The four UI library output directories contain no copied Emoji DLLs; NuGet compile/runtime graph selection is not an assertion that every library copies dependencies.

## Commands, results and remaining verification

Windows **10.0.26200 x64**, SDK **10.0.400**, MSBuild **18.9.6**; .NET/ASP.NET runtimes **10.0.11 / 9.0.19 / 8.0.30** (`dotnet --info`).
**B:** `dotnet build --configuration Release --framework net10.0` — session `phase25-candidate-build`, exit **0**, **8 warnings / 0 errors**, 9.27 s.
Warnings are the existing host/test Pkcs **NU1902/NU1903**, versions 10.0.0/8.0.1; no new diagnostics/suppression. [MCP baseline](mcp.md) has the same eight; `git diff 3502b04 HEAD -- src` is empty.
**T-ui not performed/not applicable:** `Sdk6UiContractTests` covers store tables/viewer serialization, whose source and dependencies are unchanged. No tests, discovery count, skips or rendering pass are claimed.
Performed: Git/source/XML/graph review, public supplier GETs, in-memory ZIP/PE metadata inspection, output-byte comparison and documentation checks.
Research-only retries handled gzip metadata and a wrong Routes path; an invalid abbreviated Git revision probe failed. These were not build/test failures; corrected reads supplied the evidence.
No separate restore, lower-TFM build/test, full suite, server/receiver startup, browser action, pack, install or publishing command was run.
No helper/source/test/tool/procedure was added; no process remains owned, no live clinical input/credentials used. Prior dossiers and plan untouched; no stage/commit/push.
**Final assembled-tree Windows all-target verification pending; Linux/macOS net10/net9/net8 pending post-Windows-pass follow-up.**
Native captures are under `C:\Users\ginoc\AppData\Local\Temp\`; archive originals rather than manually transcribing output. B stdout remains in its named tool session.
Original full graph: `1792194681748-copilot-tool-output-33128-4024b347-3f4c-47d0-8cf6-0391014b041a.txt` (387,301 bytes; original graph hash above).
Candidate full graph: `1792195116909-copilot-tool-output-33128-5d97d416-8e9b-44cc-b9e8-5c7f841ef17e.txt` (387,301 bytes; candidate graph hash above).
Original asset/import fingerprints: `1792194734059-copilot-tool-output-33128-b8c5d71e-8d51-4c70-b7a8-76b6387d255b.txt` (24,943 bytes; SHA-256 `5c384593c9fcfd1615fefe9f20a34ba6898774c184a7cb89fac9799f31585da5`).

[index]: https://api.nuget.org/v3-flatcontainer/microsoft.fluentui.aspnetcore.components.emoji/index.json
[project]: https://raw.githubusercontent.com/microsoft/fluentui-blazor/v4.14.4/src/Assets/FluentUI.Emojis/Components.Emoji.Package.csproj
[support44]: https://raw.githubusercontent.com/microsoft/fluentui-blazor/v4.14.4/README.md
[support4]: https://raw.githubusercontent.com/microsoft/fluentui-blazor/v4.14.5/README.md
[support5]: https://raw.githubusercontent.com/microsoft/fluentui-blazor/v5.0.1/README.md
[diff]: https://github.com/microsoft/fluentui-blazor/compare/3565796c09a63884d7a0d408d08e674d0377c3e5...ec340baf96cf36dec7b7660c3c7adba91dab051c
[release]: https://github.com/microsoft/fluentui-blazor/releases/tag/v4.14.4
[core]: https://github.com/microsoft/fluentui-blazor/tree/3565796c09a63884d7a0d408d08e674d0377c3e5/src/Core/Components/Emojis
