# FluentUI Components — deferred, 4.14.3 retained

**2026-10-16, phase 24 Windows-local assessment: retain Components 4.14.3; no candidate trial or accepted upgrade.**
Predecessor/source: `276972d2d2f141e45fa4a412c9d013310ff1273f`; initial index and worktree were clean.
Authority: [AGENTS.md](../../../AGENTS.md), phase 24/unit contract/V-accept/UI-assets gates, and the [SDK browser procedures](../sdk6-compatibility.md#browser-checklist).
Preserve the [phase-1 v5 cap](README.md#fluentui-cap-components-for-actual-migration-scope-not-frameworks): **5.0.1 requires broad active UI migration**.
**4.14.5 remains the eligible bounded assessment version**, but material local browser/installed-tool evidence is unavailable.
This is documentation-only deferral, not a failed candidate, demonstrated v4 incompatibility, or a Linux/macOS blocker.

## Feasibility established before candidate edits

Read the exact isolated store, subscriptions and CDex startup/checklist, host asset setup, and [publishing workflow](../../../.github/workflows/nuget-tool.yml).
That workflow combines restore/pack, three publish-tree `wwwroot` → `staticwebassets` moves, archive augmentation, and publication.
It was **not executed**, in whole or in part. [Getting started](../../user/getting-started.md) documents installing the published global tool,
not producing/installing this candidate locally. No sanctioned local packaged-candidate route or identified installed-candidate evidence was supplied or found in that guidance.
Source-host HTTP 200, NuGet archive contents, compiled Razor and UI data tests cannot establish delivery through the shipped tool's asset root.

Use attributed [phase-19 cache/browser evidence](hl7-fhir-sdk.md#material-local-prerequisite--rechecked-before-pins-or-tests), carried forward by [phase 23](firely-packages.md):
`hl7.fhir.uv.subscriptions-backport#1.1.0` **missing**; `hl7.fhir.us.davinci-cdex#2.0.0` **present**, but hrex **1.0.0**, pas **1.2.0-ballot**, carin-bb **2.0.0** and dtr **1.1.0-ballot** dependency manifests missing in its checked layouts.
The caller confirms these prerequisites unchanged; no new complete isolated cache or actual browser/RI result was supplied. No personal directories were rescanned here.
That dossier observed Edge/Chrome executables, not successful interactions; no applicable browser automation procedure was established, and none was installed here.
Thus the mandatory complete offline RI/browser setup and installed-tool gate remain unavailable **before any pin, source or test edit**.

## Fresh availability, supplier support and meaningful changes

Public NuGet indexes, registration/catalogs and nuspecs for Components/Icons/Emoji revalidate stable, listed releases below.
URL forms: `https://api.nuget.org/v3-flatcontainer/<lowercase-id>/index.json`, `<id>/<version>/<id>.nuspec`;
registration: `https://api.nuget.org/v3/registration5-gz-semver2/<lowercase-id>/<version>.json`.
All inspected catalog entries lack deprecation/vulnerability entries; this is not a closure security audit.

| Components version | NuGet publication UTC | Disposition |
|---|---|---|
| **4.14.3** original/retained | 2026-06-23 | No source/dependency change; browser acceptance not established. |
| **4.14.4** lower stable | 2026-07-30 | Listed fallback; does not remove the local evidence gap. |
| **4.14.5** highest stable v4 | 2026-09-30 | Eligible bounded trial, deferred without candidate resolution. |
| **5.0.1** highest stable overall | 2026-10-14 | Broad-migration cap, not a TFM cap. |

Nuspec repository commits: original `3565796c09a63884d7a0d408d08e674d0377c3e5`, 4.14.5 `195c06c88f260d292a1dcd47d4b5d9d79bfa88cf`, 5.0.1 `e31b214c2c1b499f0322013bdc2f1c4f05e3f5d0`.
The [v4 README][support4] says v4 is no longer supported except security updates; the [v5 README][support5] offers best-effort GitHub support, not official ASP.NET Core servicing.
Eligibility here is not a full-support/LTS promise. The actual [4.14.4 notes][release4] include dialog trimming annotations, disposed-menu cleanup and a DataGrid fix.
The [4.14.5 notes][release45] and [source diff][diff45] change anchored-region shadow-DOM focus, DataGrid column-popup Tab navigation,
and dialog-provider Add/Update from `Task.Run` to renderer `InvokeAsync`. These affect existing grid filters and `ShowPanelAsync<NavMenu>`, demanding browser focus/dialog checks.
[5.0.1 notes][release5] include select accessibility, ReadOnly inputs, tab/nav behavior, DataGrid roles/resizing, theme serialization and JS/checkbox fixes; they do not reverse the v5 migration.
[Migration index][migration], [layout][layout] and [navigation][nav] confirm removal of FluentDesignTheme, FluentHeader/BodyContent, FluentNavMenu/Group/Link and FluentTextField/Search.
New root providers, CSS theming, layout/navigation and input APIs extend beyond bounded compatibility edits. Both 4.14.5 and 5.0.1 contain native net8/net9/net10 DLLs;
5.0.1 additionally has an unused net11 group with preview dependencies. **No Candle TFM is dropped and no preview is selected.**

## Consumers and component/assets combinations

All paths below are under `src/`; imports and host references remain unchanged on net10.0/net9.0/net8.0.
`fhir-candle-ui.props` has four references and exactly five consumers:
`fhir-candle/fhir-candle.csproj`, `FhirCandle.Ui.Common/FhirCandle.Ui.Common.csproj`,
`FhirCandle.Ui.R4/FhirCandle.Ui.R4.csproj`, `FhirCandle.Ui.R4B/FhirCandle.Ui.R4B.csproj`, `FhirCandle.Ui.R5/FhirCandle.Ui.R5.csproj`.
`fhir-candle/Program.cs` retains `AddFluentUIComponents`; `fhir-candle/Layout/{MainLayout,NavMenu}.razor` retain theme/providers/panel navigation.
`fhir-candle/Pages/Store/{FhirStorePage,ResourceList,ResourceViewer}.razor` retain counts/hide-empty, table filters/events and pretty JSON/XML/metadata selection.
The viewer uses `FhirCandle.Ui.Common/Components/FhirEditor.razor` and each version's `Components/FhirEditorLinks.razor`.
Read-only active scope includes `fhir-candle/Pages/Smart/ClientDetailsPage.razor`, `fhir-candle/Pages/Subscriptions/*.razor`, `fhir-candle/Pages/RI/subscriptions/Tour.razor`,
`FhirCandle.Ui.R4/Subscriptions/UsCoreHti2ContentsR4.razor` and `FhirCandle.Ui.R4/davinci-cdex/TaskTable.razor`: removed text/search controls are active, not merely commented markup.
Navigation/DarkTheme, SMART tab and R4 Person glyphs are active. Index's explicit `new Icons...Warning()` is commented; its warning MessageBar remains active.
No active `Emoji.*` instantiation was found. Shared versioned pickers are not imported; notification format/pretty toggles and PAS claim controls remain dormant.

| Components / either asset | Actual Components dependency / supplier build | Assessment, not browser proof |
|---|---|---|
| 4.14.3 / retained 4.14.3 | **[4.14.2, )**; supplier build 4.14.2 | Existing resolved combination. |
| 4.14.5 / retained 4.14.3 | **[4.14.2, )** | Range-compatible v4 trial; no required co-movement established, not a blanket binary-support guarantee. |
| 4.14.5 / 5.0.1 | **[4.14.5, )**; [supplier build][pins5] actually references Components 4.14.5 | Supported supplier-built baseline for independent asset trials, not an executed Candle result. |
| 4.14.3 / 5.0.1 or 4.14.5 | **[4.14.5, )** / **[4.14.4, )** | Neither minimum is met by the currently retained Components pin. |
| 4.14.3 / 4.14.4 | **[4.14.2, )**; supplier build 4.14.2 | Eligible lower asset assessment, not selected in phase 24. |
| 5.0.1 / retained 4.14.3 | Minimum permits it; no inspected supplier certification of this binary combination | Unknown support plus broad component migration; not an accepted route. |

Icons/Emoji have no mutual dependency; their [Icons][icons] and [Emoji][emoji] projects use the shared supplier component pin.
**Do not cap asset 5.x by association with the component migration.** Phases 25/26 must use the then-accepted Components version: it remains **4.14.3**, not 4.14.5.
Unchanged Monaco **3.5.0**, `IncludeAssets=all`, has native assets on all three targets and no FluentUI dependency; its ASP.NET Core Components/Web minima are 10.0.0/9.0.0/8.0.0.
This permits coexistence by range, not certified JS/editor behavior; no Monaco or asset pin changed.

## Current graph versus conditional candidate and shipped assets

All **46 declarations / 36 IDs / seven owners** match HEAD, including aliases, conditions and asset metadata.
Before/after existing-assets normalization: **12 projects / 36 targets / 946 full package nodes / 387,301 bytes**;
SHA-256 **`55bdad44a44eac1ad97e18493d6da1a944581ff267b73ad585cd84808fefd1e1`**.
Format: sorted compact JSON mapping git-relative slash `csproj|target` to full package-only nodes, `ensure_ascii=False`, UTF-8, no final LF. Not a new restore.
Current Components/Icons/Emoji are **4.14.3**, Monaco **3.5.0** throughout their consumers; Components/Monaco select matching-TFM libraries, asset DLLs net8.
Tests retain Components.Web/Http **10.0.9/10.0.9**, **9.0.17/9.0.17**, **8.0.28/8.0.1** (net10/net9/net8); host/UI framework references prune those edges.
4.14.5 requires Components.Web >=**10.0.12/9.0.19/8.0.30**, Http >=**10.0.12/9.0.19/8.0.1**, and Hosting/Configuration abstractions as declared in its nuspec.
Accepted direct Hosting.Abstractions **10.0.13** already satisfies the net10 **10.0.12** minimum; Configuration.Abstractions is likewise 10.0.13 where resolved.
The conditional 4.14.4/Hosting-10.0.9 fallback is unnecessary here. These are conditional minima, **not a resolved candidate closure/pruning/runtime result**.
Public archives inspected in memory: Components 4.14.3 and 4.14.5 each describe **39 static assets / 75 endpoints**, but descriptor hashes differ; 5.0.1 describes **13/24**.
They retain `_content/Microsoft.FluentUI.AspNetCore.Components` and build/buildTransitive/buildMultiTargeting imports; v5 lacks the existing `js/loading-theme.js`.
Monaco describes **122/244** under `_content/BlazorMonaco`. `App.razor` retains `jsInterop.js`, `lib/monaco-editor/min/vs/loader.js`, `lib/monaco-editor/min/vs/editor/editor.main.js`.
`Program.cs` finds assembly-relative `staticwebassets` before `wwwroot`, sets WebRootPath, loads static-web-asset metadata and calls `UseStaticFiles`.
This packaged-tool path, module/CSS/theme payloads and Monaco initialization require actual installed-tool delivery; archive inspection is **not that gate**.

## Verification performed, unavailable checks and unblock condition

Performed: read-only Git/source/procedure review, public supplier GETs, in-memory archive inspection, XML/graph comparison, document-link/line-ending/whitespace checks.
Research-only retries corrected console Unicode encoding and malformed archive/migration URLs (404s); successful source/metadata reads supplied the assessment, not build failures.
**Not performed:** restore/build/tests (including B/T-ui), candidate graph resolution, source/installed server or receiver startup, browser actions, packaging or installation.
Counts/hide-empty/filter/list freshness, viewer JSON→XML→JSON, metadata/editor links, navigation/theme glyph interactions on r4/r4b/r5 all remain **not run**;
so do generated/received subscriptions, all-version tours, R4 HTI-2, CDex provider/payer Task controls and the incomplete PAS shell. No HTTP/data-test substitute is claimed.
Reconsider with an explicitly sanctioned local candidate packaging/install verification procedure or identified candidate artifact/route, complete isolated offline contents for both pinned RI packages,
and actual local browser access/results. Use the exact SDK checklist startups, empty owned roots, registries disabled, synthetic version-shaped resources and a real local subscription-hook receiver.
Deliberately supply `Task.code.Coding` for CDex browser seeds; no silent pre-existing repair. Record source/artifact/OS/browser/seed identities, actions/results, console/server errors and shutdown.
Then revalidate supplier availability/support/ranges, run catalogue **B + exact T-ui**, fresh all-TFM graphs and necessary target escalation, and every applicable browser/installed-asset gate.
No extra owner/direct pin, weakened assertion, new warning/skip, dormant activation or tooling is authorized. No helper was created or left running; no credentials, browser profiles, clinical data or live FHIR registry tests used.
Only this document changes; no accepted-candidate claim or historical evidence rewrite. Existing Pkcs advisories remain unresolved and unremeasured.
**Final Windows assembled-tree verification pending; Linux/macOS all three TFMs pending post-Windows-pass follow-up**, not the reason for this retention.

[support4]: https://raw.githubusercontent.com/microsoft/fluentui-blazor/v4.14.5/README.md
[support5]: https://raw.githubusercontent.com/microsoft/fluentui-blazor/v5.0.1/README.md
[release4]: https://github.com/microsoft/fluentui-blazor/releases/tag/v4.14.4
[release45]: https://github.com/microsoft/fluentui-blazor/releases/tag/v4.14.5
[release5]: https://github.com/microsoft/fluentui-blazor/releases/tag/v5.0.1
[diff45]: https://github.com/microsoft/fluentui-blazor/compare/v4.14.4...v4.14.5
[migration]: https://raw.githubusercontent.com/microsoft/fluentui-blazor/v5.0.1/examples/Demo/FluentUI.Demo.Client/Documentation/GetStarted/Migration/MigrationIndex.md
[layout]: https://raw.githubusercontent.com/microsoft/fluentui-blazor/v5.0.1/examples/Demo/FluentUI.Demo.Client/Documentation/GetStarted/Migration/MigrationFluentLayout.md
[nav]: https://raw.githubusercontent.com/microsoft/fluentui-blazor/v5.0.1/examples/Demo/FluentUI.Demo.Client/Documentation/GetStarted/Migration/MigrationFluentNavMenu.md
[pins5]: https://raw.githubusercontent.com/microsoft/fluentui-blazor/v5.0.1/Directory.Packages.props
[icons]: https://raw.githubusercontent.com/microsoft/fluentui-blazor/v5.0.1/src/Assets/FluentUI.Icons/Components.Icons.Package.csproj
[emoji]: https://raw.githubusercontent.com/microsoft/fluentui-blazor/v5.0.1/src/Assets/FluentUI.Emojis/Components.Emoji.Package.csproj
