# Fluent UI 5.1.0 integration

The host and common/R4/R4B/R5 UI projects import
`src/fhir-candle-ui.props`. Components, Emoji, and Icons are directly pinned
together to **5.1.0**. BlazorMonaco remains **3.5.0**, including its existing
asset metadata. Both **net11.0** and **net10.0** remain product targets.
This is a release-bound integration contract, not a visual compatibility claim.

## Release authority

All three 5.1.0 nuspecs bind `microsoft/fluentui-blazor` source commit
**`a257ca53f747596029ec6dbe8d83357306a9bb97`**. Use shipped assembly/XML,
assets, and that commit ahead of mutable migration guidance:

- [Providers](https://github.com/microsoft/fluentui-blazor/blob/a257ca53f747596029ec6dbe8d83357306a9bb97/src/Core/Infrastructure/FluentProviders.razor)
  and its adjacent code-behind define the outlet composition.
- [IThemeService](https://github.com/microsoft/fluentui-blazor/blob/a257ca53f747596029ec6dbe8d83357306a9bb97/src/Core/Components/Theme/Services/IThemeService.cs)
  and `ThemeService.cs` define the element/global theme distinction.
- [Startup](https://github.com/microsoft/fluentui-blazor/blob/a257ca53f747596029ec6dbe8d83357306a9bb97/src/Core.Scripts/src/Startup.ts)
  and [Theme utilities](https://github.com/microsoft/fluentui-blazor/blob/a257ca53f747596029ec6dbe8d83357306a9bb97/src/Core.Scripts/src/Utilities/Theme.ts)
  define initializer hooks, `Blazor.theme`, and vendor persistence.
- The package authority is the
  [Components 5.1.0 archive](https://api.nuget.org/v3-flatcontainer/microsoft.fluentui.aspnetcore.components/5.1.0/microsoft.fluentui.aspnetcore.components.5.1.0.nupkg)
  and corresponding `.emoji` / `.icons` archives.

Components supplies target-specific net10/net11 assemblies. Emoji/Icons have
a net8 dependency group with a Components lower bound of 4.14.5; that is
**not** an exact 4.x requirement or permission to mix resolved family versions.

## Host, circuit, and provider ownership

`Program.RunServer` retains production web-root selection, static-file
middleware, singleton hosted-service forwarding, and the headless/SMART
guard. Its UI-enabled branch registers
`AddRazorComponents().AddInteractiveServerComponents()`,
`AddFluentUIComponents()`, and scoped `CandleThemeState`; mapping remains
`MapRazorComponents<App>().AddInteractiveServerRenderMode()`.
`BuildAppForTesting` intentionally is not a UI host.

```text
App: document, CSS, early bootstrap, HeadOutlet [InteractiveServer]
  Routes [InteractiveServer, prerender enabled]
    CandleUiRoot: one theme-bearing element
      fixed top application-feedback region
        FluentMessageBarProvider (Section="MESSAGES_TOP")
      Router: host AppAssembly + R4/R4B/R5 AdditionalAssemblies
        MainLayout / routed content
      FluentProviders
        dialog, toast, tooltip, key-code outlets
        optional configured breakpoint outlet
```

Released `FluentProviders` is an **outlet without `ChildContent`**, not a
wrapper or a cascading application shell. It still creates the toast outlet
but does **not** create a message-bar outlet. `CandleUiRoot` owns the routed
fragment and both kinds of outlets as siblings under the same element,
inside the existing interactive boundary. Provider-created content therefore
inherits the root's applied tokens.

The separate feedback region precedes the Router. Its app-owned CSS is fixed
at the top with `inset: 0 1rem auto`, `max-height: 40dvh`, scrollable overflow,
and z-index 100 (above chrome at 85, below released modal content at 999).
It must not add flow height to the full-viewport layout or place errors below
it where default overflow can clip them. Managed assertions pin composition
and these CSS declarations; they do not prove actual positioning.

`MainLayout` retains navigation, breadcrumbs, `INavTracker.IsDarkMode` and
`OnThemeChanged`, and a changing `IsDarkMode` cascading value for the
resource viewer. It updates the mounted Monaco editor (`vs` / `vs-dark`)
without reloading the resource or replacing the model. The root/state do not
own store, SMART, subscription, or editor logic. Versioned UI assemblies,
aliases/shared imports, Router layouts/focus navigation, HeadOutlet, Monaco
scripts, and Blazor startup remain separate preservation boundaries.

Necessary released API bindings include:

| Consumer | 5.1.0 binding |
|---|---|
| Navigation drawer | `IDialogService.ShowDrawerAsync<NavMenu>(DialogOptions)`; the start-aligned drawer sets the width (at most `20rem` or `90vw`, whichever is smaller), and `FluentNav` fills its content area |
| Navigation / links | `FluentNav`, `FluentNavCategory`, `FluentNavItem`; `FluentLink` / `FluentAnchorButton`; native breadcrumb anchors inherit the header's neutral theme foreground |
| Text / number inputs | `FluentTextInput` for text and Blazor `InputNumber` for the editable HTI-2 heartbeat; search uses immediate input with zero vendor delay while retaining the application's debounce and immediate clear |
| Select / autocomplete | `FluentSelect<TOption,TValue>` uses `ValueChanged` for single selection; autocomplete uses its own `OnOptionsSearch` / `SelectedItemsChanged` contract |
| Published feedback | `INotificationService.ShowMessageBarAsync(MessageBarOptions)` targeting `MESSAGES_TOP`, not removed `IMessageService` |

The subscriptions tour and its notification viewers use read-only text fields
for heartbeat seconds and event numbers. The HTI-2 heartbeat remains an
optional integer. Neither displaying nor editing these values loads IMask.

Managed regressions exercise drawer/feedback delivery, search/row activation,
autocomplete selection, and SMART key-family RSA→RS384 / EC→ES384 callbacks.
They do not infer working callbacks from unmatched attributes compiling.

Use `FluentText` rather than `FluentLabel` for sentences containing inline
links or code. The released label uses `inline-flex`, which separates text
and links into flex items and loses the spaces between them. In body text,
inline links and copyable values move to the next line as a unit and can
wrap internally if wider than their container.

The picker ignores header/non-data rows before mutating selection or notifying
its consumer. Managed checks dispatch the rendered sortable header event and
bubble through the released row handler, retaining selected IDs and callback
counts in both single- and multiple-selection modes while sorting still works.

The service-owned drawer binds `FluentNav.OnItemClick` to cancellation of its
own `DialogInstance` for enabled destinations. It retains the normal anchor
`Href` and default behavior, with no extra `NavigateTo` or layout-hamburger
shortcut. Managed tests await the complete rendered anchor event, including
the released item's continuation after cancellation/disposal, check renderer
errors, and verify that the dialog disappears but its provider remains.
Category expansion and default Escape/backdrop dismissal remain vendor-owned.
`FluentDialogBody` renders the configured FHIR Candle title and the version
in its `ActionTemplate` footer; submit/close actions remain hidden. Tenant
links render explicit child-content icons because 5.1.0 suppresses `IconRest`
on category subitems. Each affected link's own subtree is checked for its icon.
These checks do not establish native browser-anchor navigation or visual layout.

## Preference and theme lifecycle

The canonical application preference remains the **plaintext** local-storage
key **`theme`**, with lowercase fields, for example:

```json
{"mode":"dark","primaryColor":"#6264a7","neutralColor":"#abc","future":{"keep":true}}
```

`ThemePreference` retains the full object, including unknown/nested members
and original color data. A mode-only edit does not serialize a new CLR-cased
schema or use ProtectedLocalStorage. Lowercase `light` / `dark` are explicit;
missing, `system`, or unrecognized modes request System. Initialization is
read-only, preserving absent/unrecognized members rather than normalizing
storage. Missing, malformed, non-object, or inaccessible storage gives a
usable in-memory System fallback; only an intentional toggle may replace it.

Only validated #RGB/#RRGGBB colors and the finite legacy Office primary
palette become styles. Exposed hex values normalize to six digits;
unusable primary values fall back to **#6264a7**. Invalid raw color data stays
in the retained object, not in CSS. Valid neutral data feeds the limited
application semantic mapping, not a recreation of all v4 design tokens.

`CandleThemeState` distinguishes requested Light/Dark/System from effective
dark appearance. System notifications change effective appearance only in
System mode. A toggle chooses the opposite effective appearance and makes it
explicit. Monotonic user revisions prevent late hydration from reversing a
toggle while still recovering saved colors/unknown fields. Hydration and
system changes never request persistence.

`js/candle-theme.js` supplies early document appearance, a read-only snapshot,
and the media-query listener. After interactivity, `CandleUiRoot` reconciles
the snapshot and serializes synchronization through its gate. It uses
`IThemeService`'s element API:

```csharp
await themeService.SetThemeToElementAsync(element,
    new ThemeSettings(Color: preference.PrimaryColor,
        Mode: effectiveMode, IsExact: true));
```

Here `effectiveMode` is released `Microsoft.FluentUI.AspNetCore.Components.ThemeMode.Light`
or `.Dark`, never the application's requested System mode. This element API
does **not** persist. Global `SetThemeAsync` mode/settings overloads persist to
the vendor key **`fluentui-blazor:theme-settings`**, and vendor startup installs its own
system listener. The application neither cuts over to, dual-writes, nor
clears that key. Explicit tokens on the encompassing root isolate visible
controls from vendor document-level changes.

The app independently owns `html[data-candle-theme]` and document background /
color-scheme fallback; it does not depend on vendor `body.dataset.theme`
remaining unchanged. `candle-theme.css` maps released `--colorBrandBackground`
and `--colorNeutral*` tokens into small app-owned `--candle-*` semantics.
In dark mode, links in page content use a light-blue palette rather than
the saved brand color. They stay underlined and maintain at least 4.5:1
contrast on dark content backgrounds, including hovered grid rows. Header
controls and light-mode links retain their existing colors.

Only user revisions persist through the adapter. Before its synchronous
mode-only write, it rereads storage on **every** write so newer colors, nested
and added members survive and deleted fields stay deleted, even with repeated
stale managed payloads. A valid empty `{}` is authoritative, not a parse error.
Only missing, unreadable, invalid, or non-object storage falls back to the valid
managed object. Revisions remain monotonic per connection, and disconnected
writers cannot write. Storage denial is an in-memory fallback. Root disposal unsubscribes managed
events, waits for synchronization, removes the browser listener, and disposes
the callback reference. Expected `JSDisconnectedException` is handled at the
interop boundary; unrelated JS errors are not silently swallowed.

## Delivered CSS and JavaScript

`App.razor` deliberately loads, in order, the host
`fhir-candle.styles.css`, `css/candle-theme.css`, and `css/app.css`, then the
early `js/candle-theme.js` bootstrap. The host-generated scoped bundle imports
the released
`_content/Microsoft.FluentUI.AspNetCore.Components/Microsoft.FluentUI.AspNetCore.Components.bundle.scp.css`
**once**; do not add a duplicate explicit library link.

Blazor's initializer manifest (`fhir-candle.modules.json`) supplies
`Microsoft.FluentUI.AspNetCore.Components.lib.module.js`. Its before/after
hooks register `Blazor.theme` after startup. Do not manually start the vendor
module a second time. The released scoped CSS bundle itself has no imports, but
`FluentUIStyles` dynamically fetches **`css/default-fuib.css`** under the same
package content prefix. Keep those defaults enabled and explicitly qualify
that file: a CSS-import/initializer-manifest walk cannot discover the fetch.

The old `js/loading-theme.js` and loading-theme custom element are gone.
`css/reboot.css` still ships in 5.1.0; the application stopped importing it to
avoid conflicting normalization, **not** because it was removed upstream.
Existing `css/monaco.css`, Monaco interop/loader/editor-main scripts, and
`_framework/blazor.web.js` remain in the document.

The production HTTP regression uses the existing serialized `ProductionHost`
and real `RunServer`, loopback ephemeral port, synthetic R4/R4B/R5 tenants,
isolated configuration/key storage, cancellation/deadlines, and cleanup.
It resolves actual production UI services, checks independent state scopes,
prerenders `/` and all three store URLs, and inspects compiled Router
parameters separately for all three additional assemblies. Three host store
routes alone are not evidence of version-library route registration.

The small asset traversal follows document styles/scripts, relative CSS
imports, initializer manifest entries, and Fluent module imports. Released
component references for DataGrid, Grid, Nav, and Tooltip are read from their
assembly constants because prerendering does not execute their JS imports.
The dynamic default stylesheet is an explicit seed. Traversal stays
same-origin and allowlisted, normalizes/deduplicates URLs with a cycle guard,
and is bounded to 32 assets / depth 6; it is not an unrelated-site crawler or
a full Monaco dependency walk. Each response must have the right MIME,
nonempty/non-HTML content, expected identity markers, and SHA-256 equality
with the **live RunServer-selected `WebRootFileProvider`** file. No shadow web
root, copied passing fixture assets, or SPA fallback HTML qualifies.

### Package qualification contract

Existing `.github/scripts/Test-ReleasePackage.ps1`, `Test-ToolConsumer.ps1`,
and `tests/Test-ReleaseScripts.ps1` require these nine assets independently
for each net10/net11 tool payload:

- `js/candle-theme.js`, `css/candle-theme.css`, `css/app.css`
- `fhir-candle.styles.css`
- `_content/Microsoft.FluentUI.AspNetCore.Components/css/default-fuib.css`
- The Fluent `Microsoft.FluentUI.AspNetCore.Components.bundle.scp.css` and
  `Microsoft.FluentUI.AspNetCore.Components.lib.module.js` under that content prefix
- `_framework/blazor.web.js`, `_content/BlazorMonaco/jsInterop.js`

The consumer checks MIME/non-HTML content and SHA-512 identity against the
exact candidate archive entry for the selected framework. Package provenance,
hash, duplicate-entry checks, and cleanup remain required. Synthetic fixtures
cover both payload frameworks and each missing replacement asset, without
requiring the retired loader. These contracts are not evidence that a package
was packed, installed, or qualified.

## Verification commands and evidence limits

Conventions/commands come from repository-root `AGENTS.md` (no fallback).
Its VSTest filter/combination rule supplies the focused command. The matching
net10 variants are authorized by this migration's plan and supported by
`src/fhir-candle.props` / CI. Run from the repository root:

```powershell
dotnet test --configuration Release --framework net11.0 --no-restore --verbosity normal --filter "FullyQualifiedName~fhir.candle.Tests.HostingCompatibilityTests|FullyQualifiedName~fhir.candle.Tests.Sdk6UiContractTests"
dotnet test --configuration Release --framework net10.0 --no-restore --verbosity normal --filter "FullyQualifiedName~fhir.candle.Tests.HostingCompatibilityTests|FullyQualifiedName~fhir.candle.Tests.Sdk6UiContractTests"
dotnet build --configuration Release --framework net11.0
dotnet build --configuration Release --framework net10.0
dotnet test --configuration Release --framework net11.0 --no-restore --verbosity normal
dotnet test --configuration Release --framework net10.0 --no-restore --verbosity normal
```

Normal builds provide the restore prerequisite; focused `--no-restore` runs
reuse existing restored assets. Check discovery of the new
`HostingCompatibilityTests.ProductionUiServesRoutesAndRequiredAssets` fact
on each target, alongside both original hosting facts and `Sdk6UiContractTests`.
Retain `CandleThemeTests`, `FluentUiIntegrationTests`, and `BlazorInteropTests`
as complementary preference/state, managed binding, and disconnection checks.

For the bounded UI-remediation gate, build both targets above, then run:

```powershell
dotnet test --configuration Release --framework net11.0 --no-restore --verbosity normal --filter "FullyQualifiedName~fhir.candle.Tests.CandleThemeTests|FullyQualifiedName~fhir.candle.Tests.FluentUiIntegrationTests|FullyQualifiedName~fhir.candle.Tests.BlazorInteropTests|FullyQualifiedName~fhir.candle.Tests.Sdk6UiContractTests"
dotnet test --configuration Release --framework net10.0 --no-restore --verbosity normal --filter "FullyQualifiedName~fhir.candle.Tests.CandleThemeTests|FullyQualifiedName~fhir.candle.Tests.FluentUiIntegrationTests|FullyQualifiedName~fhir.candle.Tests.BlazorInteropTests|FullyQualifiedName~fhir.candle.Tests.Sdk6UiContractTests"
node --test .github\scripts\tests\candle-theme.test.cjs
```

The separate JavaScript check requires Node.js 24+ (locally 24.22.1), with
no npm install or .NET dependency. CI runs the same script on its existing
OS matrix. It executes the **actual** adapter in `node:vm` with isolated
storage/media/document doubles and `node:test`: successive stale writes after
external objects (including `{}`), invalid/unavailable storage, initialization,
revisions, and disconnect behavior. This is JavaScript execution, not a
real-browser, media-query implementation, visual, or end-to-end result.

Recorded Phase 2 evidence at `90e1da3` on Windows: host builds for both targets
had zero diagnostics; focused theme/managed-integration/interop checks passed
**43/43 per target**. All five props importers resolved all three Fluent
packages to 5.1.0 and Monaco to 3.5.0 on both targets. Both generated host CSS
bundles had one Fluent import; both initializer manifests included the Fluent
module, and selected assets included `default-fuib.css`.

Phase 3 evidence snapshot (2026-11-18): the focused hosting/UI-contract pair,
including the new production HTTP fact, passed **16/16 per target** in
net11.0 session 507 and net10.0 session 508, each with **zero errors**.
Each run reported one CS0618 and two xUnit2031 warnings; the subsequent
warning-correction edit replaces the obsolete `nameof` reference with the
literal parameter name and uses the predicate `Assert.Single` overload.

At the time of that edit, the **final focused rerun and final solution
build/full-suite matrix had not yet been executed**. The execution ledger in
local `scratch/1118-06/plan.md` is authoritative for subsequent verification
and final results; the commands above do not assert a future pass.
Release-script fixtures were source/AST-inspected **only**, not executed.
No release-fixture command is invented here; existing CI remains separate.
No pack, tool install, publish, browser, or preview qualification was performed.
The original historical net11 baseline was **CANCELLED**; group 4's historical
full-suite passes are not Fluent v5 evidence.

Managed interop doubles do not execute the real bootstrap; the separate Node
check does, against boundary doubles rather than native browser storage, media
queries, or browser disposal. Prerender/HTTP/manifest/hash checks do not prove
focus, contrast, viewport/scroll behavior, actual CSS application, editor
interaction, or end-to-end browser equivalence. Windows evidence is not an
OS-matrix result. Revert the coherent UI/package/asset/qualification changes
together if needed, not package pins alone; retain the legacy `theme` object.
