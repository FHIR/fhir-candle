# FluentUI Icons — deferred, 4.14.3 retained

**2026-10-16, phase 26: retain Icons 4.14.3; documentation-only assessment, not an accepted upgrade or failed candidate.**
Predecessor/source: `11394f5e24e78c42336d4f9a424b3a962df9302e`; initial index/worktree clean.
Authority: [AGENTS.md](../../../AGENTS.md), phase26/unit contract/V-accept, [Components](fluentui-components.md), [Emoji](fluentui-emoji.md), and [actual browser procedures](../sdk6-compatibility.md#browser-checklist).
Only this dossier changes. Executable patch against predecessor: **0 bytes**, SHA-256 `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855`.
Components remains **4.14.3**, independently accepted Emoji **4.14.4**, Monaco **3.5.0**; no speculative Icons pin, adaptation, test edit or hidden Components uplift.
Raw unchanged `src/fhir-candle-ui.props`: **649 bytes**, SHA-256 `d0e8919f718776e5115bf58a3ff00ec16543be7dbb0d1c065100e5242948cdd4`.

## Fresh candidates, support and independent boundary

Public [NuGet index][index], registration/catalog entries, all four nuspecs/archives and tagged supplier sources were freshly read on **2026-10-16**.
Registration: `https://api.nuget.org/v3/registration5-gz-semver2/microsoft.fluentui.aspnetcore.components.icons/{version}.json`.
Nuspec: `https://api.nuget.org/v3-flatcontainer/microsoft.fluentui.aspnetcore.components.icons/{version}/microsoft.fluentui.aspnetcore.components.icons.nuspec`.
All four stable releases are listed; inspected catalogs contain no deprecation/vulnerability entries. This is not a recursive security audit.

| Icons version | Published UTC | Components minimum / actual supplier build pin | Disposition |
|---|---|---|---|
| 5.0.1, latest stable | 2026-10-14 | [4.14.5, ) / 4.14.5 | Minimum exceeds retained Components; not trialled. |
| 4.14.5, latest original-major | 2026-09-30 | [4.14.4, ) / 4.14.4 | Same minimum barrier; not trialled. |
| **4.14.4, lower stable** | 2026-07-30 | **[4.14.2, ) / 4.14.2** | Independently eligible; deferred for material browser evidence below. |
| **4.14.3, original/retained** | 2026-06-23 | [4.14.2, ) / 4.14.2 | Unchanged, not newly runtime-certified here. |

Version-tagged `Directory.Packages.props` and [Icons package project][project] establish build pins, not exact-version dependency constraints.
Icons has one net8 dependency group, on Components only; no Icons↔Emoji dependency or automatic major-family coupling.
The [4.14.4 policy][support44] offers best-effort GitHub support, not official ASP.NET Core servicing; [later v4 policy][support4] limits v4 to security updates.
[v5 support][support5] is also best effort. Eligibility is not LTS/full support or certification of every mixed binary combination.
Supplier repository commits: original `3565796c09a63884d7a0d408d08e674d0377c3e5`; lower candidate `ec340baf96cf36dec7b7660c3c7adba91dab051c`;
4.14.5 `195c06c88f260d292a1dcd47d4b5d9d79bfa88cf`; 5.0.1 `e31b214c2c1b499f0322013bdc2f1c4f05e3f5d0`.
Neither newer Icons candidate is rejected for Components v5's broad migration or a fictional TFM drop; moving a second direct pin is outside this unit.

## Actual generated glyphs, callers and asset applicability

The exact [4.14.3→4.14.4 comparison][diff] has **8 commits / 36 files**, including **15 generated Filled/Regular/Light files**; [release notes][release] explicitly update icons.
Unlike Emoji, generated Icons source changes. Regular20 adds **40** glyphs and changes **12** existing constructors/payloads; Regular24 adds **6**, changes **10**; neither removes a glyph.
Changed Regular20 names: ArrowRotateClockwise, ArrowRotateCounterclockwise, Color, Copy, CopyAdd, CopyArrowRight, CopySelect, GaugeRotate315, PulseSquare, SaveCopy, SendCopy, SlideLink.
Regular24 changes Copy, CopyAdd, CopyArrowRight, CopySelect, GaugeRotate315, PulseSquare, SaveCopy, SendCopy, SlideLink, SlideMicrophone.
**All twelve active Candle glyph constructor/API/SVG source lines below are identical** between original and lower candidate; this is source evidence, not observed rendering.

| Active owner under `src/` | Actual Regular glyphs and behavior requiring preservation |
|---|---|
| `fhir-candle/Layout/MainLayout.razor` | Size24.Navigation opens `ShowPanelAsync<NavMenu>`; Size20.DarkTheme toggles light/dark in both header branches. `Components/Routes.razor` selects this layout. |
| `fhir-candle/Layout/NavMenu.razor` | Size24.Home, DesktopSignal (SMART enabled), CubeMultiple, Fire; Size20.ArrowExport, CloudArrowDown. Tenant groups link store/local subscriptions/received notifications. |
| `FhirCandle.Ui.R4/Subscriptions/UsCoreHti2ContentsR4.razor` | Size20.Person in Patient selector label; called by `/subscriptions/uscore-hti-2?store=r4`, with active selection/dismiss/edit controls. |
| `fhir-candle/Pages/Smart/ClientDetailsPage.razor` (read-only) | Size20.Braces, ArrowDownload, TextField on active JWKS JSON/URI/manual tabs. No needed adaptation established; any required edit here must stop for ownership. |

Index's explicit Size24.Warning is **commented**, not an active Icons consumer. Its live warning MessageBar uses retained Components' `CoreIcons.Filled.Size20.Warning`, not this package.
Legacy MudBlazor markup and unimported shared pickers remain inactive; no dormant UI is enabled. All five props importers remain host, Ui.Common and Ui.R4/R4B/R5.
Supplier `Icon.cs`, `FluentIcon.razor`, generator, package project, shared package pins and `Directory.Build.targets` are byte-identical across 4.14.3/4.14.4.
Generated constructors pass SVG strings to `Icon`; retained `FluentIcon` renders inline SVG/MarkupString. There is no icon font/download URL to substitute for a rendering check.
All four inspected archives contain the same **five net8.0 DLL paths** (wrapper plus Color/Filled/Light/Regular), with no staticwebassets/wwwroot, JS/CSS, contentFiles, analyzers, runtime-native assets or consumer build hooks.
Original/lower PE metadata: versions **4.14.3.26174→4.14.4.26210**; all five DLLs have **zero module methods and zero manifest resources**: no new module initializer.
Group references remain Components **4.14.2.26138** and System.Runtime **8.0.0.0**; wrapper references only System.Runtime. Filled/Regular add 101 types/methods each, Light 8; Color unchanged in counts.
Archive SHA-256 original: `f4aa4311b21fa0ecee1e4243706ce6ea2fa4e7b5ded71bf2385cfd4a22c97bc5`; lower: `4009f125716e86ae7b536c722eace5bc84f19d4ae8464bce70b2eacb036a935a`.
No new Icons static payload/hook exists, but **active runtime rendering remains a mandatory phase26 gate**, even with unchanged used SVG source. Emoji's unused-assembly disposition cannot supply it.
`App.razor` keeps Components loading-theme plus Monaco jsInterop/loader/editor scripts; `css/app.css` imports Components reboot.css. No Monaco delta or invented new-module issue is asserted.
`Program.cs` chooses assembly-relative `staticwebassets` before `wwwroot`, loads metadata and serves static files. Real host/tool rendering and relevant asset delivery are not proved by archives or HTTP status alone.

## Current graph and conditional lower-candidate closure

Namespace-aware XML comparison with HEAD: **46 references / 36 IDs / seven declaration owners**, all unchanged including aliases, conditions and asset metadata.
Read-only existing assets, before/after: **12 projects / 36 targets / 946 full package nodes / 387,301 bytes**;
SHA-256 **`cabfafe0f57e0b4bb6420a0d5c6cdeb3e6773af5ec2a5d9ef1ee0f3862ad402f`**.
Normalization: sorted compact JSON mapping git-relative slash `csproj|target` to full package-only nodes, `ensure_ascii=False`, UTF-8, no final LF; **not a new restore**.
Five importers plus Tests select Icons4.14.3/Emoji4.14.4 net8 DLLs on net10/net9/net8; Components4.14.3/Monaco3.5.0 use matching-TFM libraries.
Tests retain Components.Web **10.0.9/9.0.17/8.0.28** and Extensions.Http **10.0.9/9.0.17/8.0.1**; Configuration/Hosting.Abstractions resolve **10.0.13**. Host/UI framework references prune those component dependency edges.
Icons4.14.4 keeps the same sole Components>=4.14.2 edge and library paths, so no transitive uplift is required by metadata. **No candidate resolution, pruning equivalence or runtime pass is claimed.**

## Performed gates, precise deferral and unblock

Performed: read-only Git/source/procedure review, public supplier GETs, in-memory ZIP/PE checks, semantic XML and all-target graph normalization, document links/whitespace/line-ending checks.
Research corrections: raw Git-blob byte comparisons initially reported checkout line-ending differences; semantic XML confirmed no changes. An invalid multiline glyph parser and one inline-script syntax error were corrected; only the guarded single-line comparison supplies glyph evidence.
**Not performed:** B, T-ui, restore, any build/test, candidate graph, server/receiver startup, browser actions, packaging, installation or publishing. No warnings, skips or test counts were measured or claimed.
No data-path delta is proposed; `Sdk6UiContractTests` verifies store tables/viewer serialization, not Razor glyphs, navigation events, theme contrast or HTI-2 rendering.
The sanctioned SDK startup/checklist was read, not executed. Actual navigation opening/link selection, light→dark→light, SMART tab glyphs and R4 Patient selection/dismissal remain **not run**.
Use attributed [phase19 evidence](hl7-fhir-sdk.md#material-local-prerequisite--rechecked-before-pins-or-tests), carried through phase24 and confirmed unchanged by the caller: backport **1.1.0 missing**; CDex **2.0.0 present**, but hrex1.0.0, pas1.2.0-ballot, carin-bb2.0.0 and dtr1.1.0-ballot dependency manifests missing in checked layouts.
No personal-cache rescan occurred. CDex's gap is contextual, not a newly invented Icons dependency; missing backport blocks the sanctioned complete HTI-2 setup.
Existing browser executables are not interaction evidence; no new provided browser results/access procedure or identified installed candidate was supplied.
The [publishing workflow](../../../.github/workflows/nuget-tool.yml) mixes pack, publish-tree asset moves, archive augmentation and push; it was not executed. [Getting started](../../user/getting-started.md) documents published-tool installation, not a sanctioned local candidate packaging route.
**Retain before speculative pins:** eligible4.14.4 is deferred for missing material active-browser evidence, not demonstrated runtime incompatibility, automatic asset coupling, or Linux/macOS availability.
Unblock with actual local browser access/results, complete isolated backport contents and a sanctioned candidate host/tool asset-delivery route or identified candidate artifact; use exact SDK isolated startups, registries disabled, synthetic version-shaped seeds and the real local receiver where required.
Record candidate/artifact/OS/browser/seed identities, all relevant navigation/theme/SMART/HTI-2 actions, delivered assets, console/server errors and shutdown. No dormant controls or pre-existing UI repair; CDex checks, if implicated, require deliberate Task.code.Coding seeds.
Then revalidate candidates/support/ranges, run catalogue **B** (`dotnet build --configuration Release --framework net10.0`), fresh all-TFM graphs/assets and exact **T-ui where data paths are affected**, escalating only as warranted, with no new warnings/skips or weakened assertions.
No helper was created/left running, tool installed, credential/profile/live clinical data used, or additional owner/direct pin changed. Prior records/plan untouched; no stage/commit/push.
**Final assembled-tree Windows verification pending; Linux/macOS net10/net9/net8 remain post-Windows-pass follow-up**, not this retention's cause.

[index]: https://api.nuget.org/v3-flatcontainer/microsoft.fluentui.aspnetcore.components.icons/index.json
[project]: https://raw.githubusercontent.com/microsoft/fluentui-blazor/v4.14.4/src/Assets/FluentUI.Icons/Components.Icons.Package.csproj
[support44]: https://raw.githubusercontent.com/microsoft/fluentui-blazor/v4.14.4/README.md
[support4]: https://raw.githubusercontent.com/microsoft/fluentui-blazor/v4.14.5/README.md
[support5]: https://raw.githubusercontent.com/microsoft/fluentui-blazor/v5.0.1/README.md
[diff]: https://github.com/microsoft/fluentui-blazor/compare/3565796c09a63884d7a0d408d08e674d0377c3e5...ec340baf96cf36dec7b7660c3c7adba91dab051c
[release]: https://github.com/microsoft/fluentui-blazor/releases/tag/v4.14.4
