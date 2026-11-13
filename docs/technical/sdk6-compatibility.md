# Firely SDK 6 compatibility

This is the consumer inventory, oracle policy, and verification checklist for
the SDK 6 migration. **Final migration verification is blocked.** Authored
tests, a successful scoped gate, a browser exercise, and an OS/framework
result are different evidence. The records below do not substitute one for
another.

The 2026-09-30 recovery passes the normal Windows net10.0 full-suite and
scoped gates. Remaining platform, all-target, browser, and offline RI-package
requirements still prevent final acceptance; see the dated evidence below.

## Current dependency versions

The 2026-11-12 same-major dependency refresh uses **6.8.0** for the R4, R4B,
R5, and test-only R6 SDK packages, with matching Base/Conformance dependencies.
`Firely.Fhir.Packages` remains **5.0.2**. FHIR tenant versions and assembly
aliases are unchanged. The migration-time inventory and evidence below retain
their original **6.2.1** identities; they are not new acceptance evidence for
the refreshed dependencies.

On Windows, the refreshed solution builds for both `net11.0` and `net10.0`
with zero warnings or errors. The full test suite passes **4,458 tests per
framework**, with no failures or skips. This does not close the historical
platform, browser, or offline RI-package acceptance gates below.

## Migration-time dependency and product envelope

| Product | SDK package / model assembly | Capability `fhirVersion` | Store assembly |
|---|---|---|---|
| R4 | `Hl7.Fhir.R4` **6.2.1** / `Hl7.Fhir.R4.dll` | `4.0.1` | `FhirCandle.R4` |
| R4B | `Hl7.Fhir.R4B` **6.2.1** / `Hl7.Fhir.R4B.dll` | `4.3.0` | `FhirCandle.R4B` |
| R5 | `Hl7.Fhir.R5` **6.2.1** / `Hl7.Fhir.R5.dll` | `5.0.0` | `FhirCandle.R5` |

The compatible Base/Conformance closure remains **6.2.1** and the host uses
`Firely.Fhir.Packages` **5.0.2**. The existing `Hl7.Fhir.R6` **6.2.1**
reference is test-only; it creates no R6 product tenant. Pins remain explicit
in the existing projects, not in a new package-management layer. Fresh
Windows migration-time asset inventory agrees with this cohort across the ten
consumers and their then-current three target-framework graphs. This is dependency evidence,
not a passing all-target runtime matrix.

`src/fhir-candle.props` specifies C# 14 and
`net11.0;net10.0`. Windows, Linux, and macOS remain supported verification
obligations. The workflow installs .NET 11 and 10 and runs its
restore/build/test sequence on all three operating systems. Historical migration
evidence below retains its original framework identities; it does not establish
.NET 11 compatibility.

Preserved architectural boundaries:

- `FhirStore.Common` remains version independent. Shared store code in
  `FhirStore.CommonVersioned` is imported into each versioned store project.
- `candleR4`/`candleR4B`/`candleR5` select the store assemblies;
  `coreR4`/`coreR4B`/`coreR5` select the actual SDK model assemblies above.
  The model filenames no longer have the historical `.Core` suffix.
  Preserve `AddPackageAliases`; see [Assembly aliases](assembly-aliases.md).
- Named-tenant routing and `FhirRequestContext`/`FhirResponseContext` contracts,
  store-owned compiler/cache/parser lifetimes, reflection-discovered `Op*`
  operations and interaction hooks, and their version/package gates remain.
- Hosted singleton registrations continue to forward the existing singleton
  through `GetRequiredService`. There is no new runtime SDK facade, selection
  switch, service-lifetime architecture, or UI dependency upgrade.

## Actual UI consumers

The host project references `FhirCandle.Ui.Common` and all three
`FhirCandle.Ui.R4`, `.R4B`, and `.R5` Razor projects. Each imports the existing
build/UI props; each versioned UI references its matching store project.
“Included” below describes the current project graph, not a new build result.

| Included consumer / source | Current behavior and verification boundary |
|---|---|
| `FhirCandle.Ui.Common/Components/FhirEditor.razor` | Active Monaco editor used by the host viewer; no direct Firely dependency. Rendering, language switching, and editor content require a browser. |
| `fhir-candle/Pages/Store/FhirStorePage.razor` | Enumerates `IFhirStore` and each `IResourceStore.Count`; Hide Empty Resources filters zero counts. |
| `fhir-candle/Pages/Store/ResourceList.razor` | Uses `GetInstanceTableView()` and `InstanceTableRec`; ID/name/identifier filters and conformance URL/description columns use the resource-store flags. |
| `fhir-candle/Pages/Store/ResourceViewer.razor` | `LoadResourceContent` calls `GetMetadata` for `CapabilityStatement/metadata`, otherwise `InstanceRead`. Both use `SerializePretty=true`. The editor receives `SerializedResource`, not the separate `SerializedOutcome`. There is no pretty-toggle control. |
| Each version's `Components/FhirEditorLinks.razor` | R4/R4B/R5 links selected by the host viewer; current links remain, without making external sites part of the local oracle. |
| `Pages/Subscriptions/SubscriptionsLocal.razor`, `NotificationLocalViewer.razor` | Current subscriptions/status/errors and generated-event counts, focus/additional-context links via `TryGetResourceInfo`. The local viewer's serialized-content control is already commented out. |
| `Pages/Subscriptions/NotificationsReceived.razor`, `NotificationReceivedViewer.razor` | Received status/event/reference fields and links to stored Bundles; raw JSON/XML is viewed through the linked resource viewer, not a fictional notification format selector. |
| `Pages/RI/subscriptions/Tour.razor`; each version's `Subscriptions/TourStep01.razor` and `TourUtils.cs` | Active version-selected tour, topic text/encounter fixtures, subscription creation and event bindings. R4B/R5 tour/link components are real consumers, not R4-only coverage. |
| `Pages/RI/subscriptions/UsCoreHti2.razor` and `FhirCandle.Ui.R4/Subscriptions/UsCoreHti2ContentsR4.razor` | Active R4 HTI-2 Patient/subscription selectors and editing/content options. Not an R4B/R5 HTI-2 implementation. |
| `Pages/RI/davinci-cdex/TaskList.razor` and `FhirCandle.Ui.R4/davinci-cdex/TaskTable.razor` | Active R4 Task ID/status/code table and status buttons. Package setup and meaningful synthetic Task inputs are required. |
| `Pages/RI/davinci-cdex/PasWalkthrough.razor` and `FhirCandle.Ui.R4/davinci-pas/PasWalkthroughR4.razor` | PAS's current “incomplete” page shell and SDK-dependent compiled claim/bundle code. Its interactive markup was already commented out. Do not require or activate claim-entry/submission controls. |
| `FhirCandle.Ui.Versioned/FhirCandle.Ui.Versioned.projitems` | Shared Patient/Practitioner pickers are **not imported** by the current versioned UI projects. They are inspected inventory, not currently compiled or exercised controls. Do not activate them to expand coverage. |

All paths in this table are relative to `src/`. Generic editor/link/topic
wrappers and tour fixtures remain consumers even when they require no edit.
A defect in a consumer is not permission to redesign or activate it.

### Version-specific contracts

The store/table/viewer wire expectations apply to all three product versions,
with the literal capability versions above and version-specific fixture tags
and identifiers. Same-version sibling tenants must remain isolated.

Subscriptions deliberately differ: R4 topics are encoded as `Basic` with
backport extensions; R4B/R5 use `SubscriptionTopic`. R4/R4B subscriptions use
the backport criteria/channel representation, while R5 uses native topic,
filter, and content fields. R4 notifications use Parameters-based status;
R4B/R5 use `SubscriptionStatus`. The authored Encounter inputs use `finished`
and a Coding `class` in R4/R4B, versus `completed` and a CodeableConcept array
in R5. Do not normalize away these differences.

R4 CDex hooks and PAS operations retain their existing package/version gates;
R4B/R5 negative cases must not be turned into new product capabilities.
Client Accept version parameters are `4.0`/`4.3`/`5.0`, distinct from the full
CapabilityStatement version literals.

## Independent oracle and source map

All additions use the existing xUnit v2/VSTest project,
`src/fhir-candle.Tests/fhir-candle.Tests.csproj`. Reviewed raw fixtures under
`data/sdk6-compatibility/{r4,r4b,r5}/` are copied by that project. See the
[corpus provenance inventory](../../src/fhir-candle.Tests/data/sdk6-compatibility/README.md)
for individual scenarios.

`CompatibilityReplay`, the typed records in `Models/CompatibilityScenario.cs`,
and `WireContractAssertions` are test infrastructure, not a second runtime
implementation or an oracle DSL. Expected content is authored from synthetic
inputs and existing caller contracts, never recorded from a successful SDK
run or evaluated with expected-side Firely models/parsers.

| Tests | Consumer/source contract |
|---|---|
| `Sdk6DependencyTests` | Aliased model assembly and inspector identity; actual R4/R4B/R5 store metadata. |
| `WireContractAssertionsTests`, foundation `Sdk6ContractTests` | BCL comparator/binding negative controls; fresh replays, mixed named tenants, HTTP/direct boundary fidelity. |
| `Sdk6ContractTests.Exchange` | `SerializationUtils`, store CRUD/bundles/conditionals and controller negotiation; full authored Patient/Observation/Binary content, independent JSON/XML pairs, summaries, malformed/tolerated inputs. Legacy `R4TestsSummary.Search` retains its original inputs with semantic rather than length assertions. |
| `Sdk6ContractTests.Search` | Search parameters, sorting, includes, tenant-local reference resolution, reverse chains and dedicated compartment methods; concrete IDs and non-empty result expectations. |
| `Sdk6ContractTests.Operations`, `.Subscriptions` | Reflection/package gates, `$validate`, R4 CDex hooks, PAS supplied definitions/capability refresh, versioned converters, event/reference content and inbound notification state. |
| `Sdk6PackageTests`, `Sdk6ClientTests`, `Sdk6NotificationTests` | Offline local registry/cache/CI-client paths, real aliased client calls over loopback HTTP, REST-hook delivery, and shutdown joining timers/in-flight sends. Not live-registry, SMTP/Zulip-account, or browser evidence. |
| `Sdk6UiContractTests.StoreAndResourceTablesPreserveDisplayValues` | Six authored rows: R4/R4B/R5 × JSON/XML input. Exact table values and column flags for Patient, Observation, and an inline synthetic StructureDefinition; per-type counts, filters, update/delete freshness, and same-ID sibling rows. |
| `Sdk6UiContractTests.ResourceViewerReadsMetadataAndBothFormats` | Six authored rows, each reading JSON → XML → JSON through the exact pretty `GetMetadata`/`InstanceRead` contexts. Complete Patient/Observation content, bound headers/identity, observed updates, deletion/missing results, and an unchanged sibling. |
| `Sdk6UiContractTests.UiTheoryDataIsNonEmptyAndLabeled` | Inventory fact requires six rows per scenario, all three versions, both input formats, and the ordered viewer format sequence. |

The new UI inventory is **12 theory rows + one fact**, not an executed count.
Every scenario owns/disposes its replay and stores; the class uses the shared
non-parallel compatibility-host collection. The UI data methods do not render
Razor, dispatch browser events, or prove authentication UI behavior.

### Exact content versus generated bindings

JSON property order and insignificant FHIR XML indentation may vary. Arrays,
missing members, choice names, references, primitive extensions, decimal token
precision, partial dates, and XHTML mixed-content/whitespace remain observable.
Cross-format expectations come from separately authored JSON/XML, not from
converting the response under test. Pretty output is checked for structural
indentation, not byte-size ordering.

Generated resource IDs, numeric versions, timestamps and tenant URLs are bound
once per replay. Repeated reads reuse those bindings; updates require version
progression and nondecreasing time. `Location`, weak ETag and `meta` must agree;
Last-Modified is compared at HTTP-date precision. No tenant-prefix stripping
or global ID/metadata/time exclusion is allowed. Metadata reads do not require
new versions merely because the format changed. Capability `url` is
`{tenant-base}/CapabilityStatement/metadata`, not `implementation.url`.

Direct responses keep resource and outcome fields distinct. A missing viewer
resource currently yields direct `false`, 404, an empty `SerializedResource`,
and a separate error/exception outcome. The data test does not claim that a
browser shows that outcome or implements an error banner.

## Adaptations and accepted existing behavior repairs

SDK-facing adaptations include the actual SDK 6 model assembly identities and
native parsing/model-inspector/FHIRPath call sites. The whitespace-preserving
XML reader adaptation avoids losing authored XHTML whitespace through the
SDK string-reader path. This is not authority to loosen the wire oracle.

The following bounded repairs/decisions are **not claims that SDK 6 introduced
the underlying defect**:

- **P1–P4 / M1:** repeated form-parameter reconstruction at the four existing
  controller search boundaries; complex/primitive presence; inclusion fullUrls;
  tenant-safe local-reference matching; and existential, filter-distinct
  reverse-chain behavior/cache isolation. These repair existing searches, not
  a new conformance surface.
- **U1:** direct compartment tests call the existing `CompartmentSearch` /
  `CompartmentTypeSearch` methods. Generic `PerformInteraction` still has its
  separately tested unsupported boundary; no new generic compartment dispatch
  was added.
- **O1:** the exact GET `$validate` 405 contract is newly approved behavior:
  one `error`/`not-supported` issue with diagnostic
  `$validate only supports POST; GET is not allowed.`, and HTTP `Allow: POST`.
  It is not a historical pass or generic enforcement for every operation.
  Earlier authorization/not-found guards, POST behavior, and the unsupported
  `/Parameters/$validate` route remain; see
  [validation](../user/operations/validate.md).
- **O2:** PAS definitions are independently authored package fixture contents
  loaded through `LoadPackage`, not new built-in PAS definitions or a runtime
  repair. A manifest-only package proves its ID gate, not its resources.
- **O3:** successful operation registration marks the existing capability stale
  flag before optional definition processing. Registration is the invalidation
  event even when PAS returns no definition. No eager rebuild/cache redesign
  or test-side invalidation substitutes for this behavior.
- **Notification shutdown:** the existing manager stops and joins its timers
  and in-flight delivery work during teardown. This bounded pre-existing
  lifecycle repair does not replace its public lifecycle or DI ownership.
- Summary flag forwarding corrects a pre-existing call-site omission. The
  controlled Patient-summary contract accepts both exact `SUBSETTED` pairs,
  under `http://hl7.org/fhir/v3/ObservationValue` and
  `http://terminology.hl7.org/CodeSystem/v3-ObservationValue`, without newly
  added `display`. Original tag order/content and exact occurrence counts
  remain checked. This local contract is not a global metadata/array ignore
  rule, and ordinary full reads remain unchanged.

This UI closure authors tests/docs/CI triggers only; it has not demonstrated
a need for another production adaptation.

## Recorded command evidence

Verified evidence supplied for the prior committed trees on **2026-09-24**:

| Tree / gate | Environment | Verified result |
|---|---|---|
| Phase 5, `69c8ddb591c14fc4dd8673c613204100028c65bd` | Windows, Release/net10.0 | Required application build and scoped **3,170/3,170** test gate passed twice; zero warnings, errors, failures, or skips. |
| Phase 6, `47913dada668331195c212903bc91060a551c2d9` | Windows, Release/net10.0 | Required application build and scoped **3,157/3,157** test gate passed twice; zero warnings, errors, failures, or skips. Includes 8 package, 31 client, and 37 notification additions. |
| Phase 7 UI additions over `47913dada668331195c212903bc91060a551c2d9` | Windows, Release/net10.0 | Exact scoped application build and **3,093/3,093** UI/contract gate pass after correcting a new test-only scalar-binding error; zero warnings, errors, failures, or skips. All thirteen UI cases execute. No phase-7 commit. |
| Required final local solution build / unfiltered suite | Windows, Release/net10.0 | Solution build passes with zero warnings/errors. Full suite **4,258 executed: 2,668 passed, 1,590 failed, zero skipped**; zero compiler/analyzer warnings/errors. Every failure reports an Application Control assembly-load block, directly or through fixture construction. |

The two passing counts use **different scoped filters**. Their sizes are not
a full-suite total, a removed-test comparison, or final matrix evidence.
Static source/whitespace inspection is not a build/test result.

At the 2026-09-24 hand-back, the full-suite blocker was `System.IO.FileLoadException` for
`src\fhir-candle.Tests\bin\Release\net10.0\FhirCandle.Ui.R4B.dll`:
**"An Application Control policy has blocked this file. (0x800711C7)"**.
There were 1,564 direct loader failures and 26 fixture-constructor failures
with that inner exception, through `FhirStoreManager.LoadPackagePages`.
Those results remain recorded failures, not skips or presumed passes; that
run did not certify assertions masked by assembly loading. No policy bypass, assembly
discovery workaround, changed configuration, or weakened assertion was
attempted. An authorized environment resolution and a fresh complete gate
were required. All-target and browser execution stopped at this blocker.

The tested UI source SHA256 is
`63AB2B9DD121AE7908101E72B4A66CAF12433962B51159029E8F42635629D044`;
runtime source remains at the phase-6 commit above. These documentation
records were updated after the failed gate without further code changes.

### Bounded recovery evidence (2026-09-30)

The recovery used the same runtime source at
`47913dada668331195c212903bc91060a551c2d9` and the unchanged UI-test source
hash above. All five retained phase-7 files were byte-identical through the
following runs. Only this evidence document and the corpus inventory were
subsequently updated; no runtime code, test case, or assertion changed.

| Gate | Environment | Actual result |
|---|---|---|
| Required solution build and unfiltered suite | Windows x64 build 26200, SDK 10.0.400, Release/net10.0, runtime 10.0.11 | Build passed; **4,258/4,258** tests passed. Zero warnings, errors, failures, or skips. |
| Exact phase-7 application build and scoped UI/contract suite | Same environment | Build passed; **3,093/3,093** tests passed, including 3,080 contract cases and all thirteen UI cases. Zero warnings, errors, failures, or skips. |

Result-header multiset comparisons against the hash-verified prior logs
preserve all 4,258 full-suite occurrences and all 3,093 scoped occurrences,
not merely their totals. All **1,590** formerly failed occurrences now pass.
Normal package-page discovery, including its unchanged R4B assembly scan,
remains enabled.

The ordinary build and test commands regenerated `FhirCandle.Ui.R4B.dll`
at its normal test-output path. No forced clean, relocation, policy or trust
change, discovery suppression, or test workaround was used. This proves
the current normal-path gate passes; it does not separately establish that
the historical blocked DLL is now loadable or identify an administrative
policy change. Fresh assets across ten consumers and thirty target graphs
retain the accepted dependency cohort and test-only R6 reference.

The recovery then stopped on missing required platform/offline-package
prerequisites. WSL lists no installed distribution; no macOS execution host
was supplied. The exact subscriptions-backport 1.1.0 and CDex 2.0.0 packages
were not found in the checked default cache layouts. This is not a claim
that no other cache exists. Windows SDKs/runtimes 8/9/10 and local browsers
are present, but the Windows all-target sequence and every actual browser/RI
workflow remain **not run**. No environment/tool installation, public
package fetch, or remote workflow was substituted.

Phase 7 remains blocked for final acceptance. A local checkpoint commit does
not waive the remaining matrix/browser requirements or the conductor-managed
review. Required gates must be rerun on the final implementation tree before
final acceptance.

### Required final local and all-target commands

From the repository root, using [AGENTS.md](../../AGENTS.md):

```powershell
dotnet build --configuration Release --framework net11.0
dotnet test --configuration Release --framework net11.0 --no-restore --verbosity normal
```

At the final implementation tree, run the existing workflow's exact all-target
sequence on each available Windows/Linux/macOS environment:

```powershell
dotnet restore
dotnet build --configuration Release --no-restore
dotnet test --configuration Release --no-restore --verbosity normal
```

Prerequisites are the .NET 11 SDK and .NET 11 and 10 runtimes. Record tree/commit identity,
command outcome and diagnostics per cell. Require the full existing suite plus
new cases, no new skips/quarantines or weakened expectations, zero errors and
no newly introduced warnings. Recheck dependency assets/aliases. Do not replace
missing operating systems with cross-OS claims, containers, or a remote workflow
publication. The migration's CI edit added symmetric path triggers for props,
shared project items, Razor, corpus data and the workflow itself. The current
SDK matrix has since moved to .NET 11 and 10.

### Six-cell current framework/platform record

| OS | Target | Evidence / identity | Remaining requirement |
|---|---|---|---|
| Windows | net11.0 | No full-suite migration acceptance record | Required final-tree Windows all-target sequence outstanding |
| Windows | net10.0 | 2026-09-30: full suite 4,258/4,258 and scoped UI/contract gate 3,093/3,093; zero warnings/errors/skips; runtime tree and UI source identified above | Local gate passes; the final-tree Windows all-target sequence remains required |
| Linux | net11.0 | Not run; no tree/result/diagnostics | Required environment and build/test evidence outstanding |
| Linux | net10.0 | Not run; no tree/result/diagnostics | Required environment and build/test evidence outstanding |
| macOS | net11.0 | Not run; no tree/result/diagnostics | Required environment and build/test evidence outstanding |
| macOS | net10.0 | Not run; no tree/result/diagnostics | Required environment and build/test evidence outstanding |

Environment inspection on 2026-09-30 confirmed Windows SDKs
**8.0.424, 9.0.317, 10.0.400**, corresponding runtime availability, Edge
**153.0.4234.32**, and Chrome **154.0.8037.58**. Availability is not execution
evidence. WSL listed no distribution, and no macOS environment was supplied.
The two pinned RI packages below were not found in the checked default cache
layouts. This is outstanding setup, not a claim of universal absence or
permission to fetch from a public registry.

## Browser checklist

**Not executed; required browser/RI evidence remains outstanding.** The passing
net10.0 gates do not replace browser actions, and the pinned offline RI-package
prerequisites remain unresolved. Use synthetic local data and an existing
browser/local request facility. Do not add browser automation packages, a linter, or another
test framework. Record the actual OS/browser/version, final tree identity,
tenant, route, actions, results, and browser-console/server exceptions
independently of test-run records.

### Isolated startup

Create owned, empty `source` and `cache` directories under the previously
absent `$env:TEMP\fhir-candle-sdk6-0919-01` root. If it exists, choose a new
empty root consistently and record it privately; do not delete unknown data.
This environment-variable path is a reproducible example, not an installed
machine path. Ensure unrelated configuration supplies no extra packages,
registries, notification credentials, or initial resources.

The bare run command inherits an R5-only launch profile and a live core-package
request. Pass these explicit application arguments after `--`; they take
precedence over profile arguments without editing launch settings:

```powershell
dotnet run --project src\fhir-candle\fhir-candle.csproj --framework net10.0 -- --r4 r4 --r4b r4b --r5 r5 --fhir-source "$env:TEMP\fhir-candle-sdk6-0919-01\source" --fhir-package-cache "$env:TEMP\fhir-candle-sdk6-0919-01\cache" --use-official-registries false
```

Confirm startup names all three tenants and the isolated source/cache, has no
unexpected packages, and reports its actual listening URL. Use the documented
`http://localhost:5826` unless the recorded configuration chooses another.

### Store, list, viewer, and metadata (R4, R4B, R5)

1. For each `/fhir/r4`, `/fhir/r4b`, `/fhir/r5`, POST its corpus `patient.json`
   (or independently authored XML) to `Patient` with matching FHIR Content-Type
   and Accept. Capture returned ID, Location and ETag. Replace
   `Patient/wire-patient` in that version's `observation` fixture with the
   returned absolute Patient Location, then POST it to `Observation`.
   These are local resources, not ambient/package examples.
2. Open `/store?store=r4` (then r4b/r5). Verify Patient/Observation counts,
   toggle **Hide Empty Resources**, and open
   `/store/resource-list?store=r4&type=Patient`. Filter by captured ID and
   `Wire`/`Avery`; check `Wire, Avery Quinn` and the ordered primary/secondary
   identifiers. Inspect the Observation list as well.
3. Open `/store/resource-viewer?store=r4&type=Patient&id={captured-id}` and the
   Observation viewer. Switch **JSON → XML → JSON**. Inspect `language=en`,
   month-precision `birthDate=1980-02` and its extension, exact narrative
   spaces/newline/whitespace-only paragraph, identifiers, reference and choice
   elements, `1234567890.123400`, and the offset/fractional dateTime. Verify
   pretty indentation without a byte-length comparison. Repeat all versions.
4. Open `/store/resource-viewer?store=r4&type=CapabilityStatement&id=metadata`
   (then r4b/r5), switch the same formats and check the literal FHIR version
   and `{tenant-base}/CapabilityStatement/metadata` URL. Mere reads/format
   switches must not require version increments. Exercise the existing editor
   link controls without treating external service availability as the oracle.
5. PUT the same Patient with its ID and `language=en-US`, family `Revised`,
   primary identifier `updated-{version}`. PUT the Observation with its ID,
   quantity `1234567890.123450`, and dateTime
   `2024-06-04T10:11:12.3400-05:00`, retaining its bound Patient reference.
   Capture new headers. Refresh/navigate list and viewer; inspect changed,
   not stale, values in both formats. DELETE them, confirm counts/rows are
   removed, and visit the missing instance using current viewer behavior:
   empty resource content, not an invented outcome banner.

### Subscriptions, generated and received events, RI tour and R4 HTI-2

Prepare an isolated Firely-compatible offline cache containing
**`hl7.fhir.uv.subscriptions-backport#1.1.0`**, including required locally
resolved contents. The version is pinned by
[Subscriptions RI](../user/subscriptions-ri.md). The owned source and its
`subscriptions` subdirectory must exist and be empty before deliberate
seeding. Stop the preceding server and run:

```powershell
dotnet run --project src\fhir-candle\fhir-candle.csproj --framework net10.0 -- --reference-implementation subscriptions --load-package "hl7.fhir.uv.subscriptions-backport#1.1.0" --load-examples false --protect-source true -m 1000 --r4 r4 --r4b r4b --r5 r5 --fhir-source "$env:TEMP\fhir-candle-sdk6-0919-01\source" --fhir-package-cache "$env:TEMP\fhir-candle-sdk6-0919-01\cache" --use-official-registries false
```

1. Confirm RI/package/version/tenants and no unintended outbound requests.
   Seed each version's synthetic `subscription-resources.json` entries as
   individual resources, following `Sdk6ContractTests.Subscriptions`:
   Patient/other Patient, topic, subscription, planned then completed
   Encounter. The fixture file is a named input collection, not itself a
   FHIR Bundle to POST.
2. Use an **actual local receiver**, for example this running host's
   `/fhir/{tenant}/$subscription-hook` endpoint at its recorded listening URL.
   Rewrite the fixture's `local-receiver` endpoint accordingly; it is not a
   configured tenant in this setup. Preserve the version-correct channel/topic/
   filter representation. Confirm real delivery and received state rather
   than counting a generated event as delivery evidence.
3. Visit `/subscriptions/local?store={tenant}` and its generated-event link
   `/subscriptions/notification-local-viewer?store={tenant}&id={subscription-id}`.
   Inspect status/topic/channel, event number/count, focus and additional
   context. Trigger a matching Encounter and a filter-miss Encounter referencing
   the other Patient; verify the latter does not add a matching event.
4. Visit `/subscriptions/notifications-received?store={tenant}` and follow
   `/subscriptions/notification-received-viewer?store={tenant}&ref={subscription-reference}`
   (URL-encode the reference). Check actual received status/event/reference
   values and the linked stored Bundle. Use the Bundle's resource viewer for
   JSON/XML content. Do not activate commented notification-content controls
   or invent a format selector on event cards.
5. Exercise `/subscriptions/tour?store=r4`, then r4b/r5, using the current
   versioned tour steps and their local notification target. Visit
   `/subscriptions/uscore-hti-2?store=r4` for the active Patient/subscription
   selector/editor workflow. Record its actual exposed content options and
   results, not an assumed R4B/R5 HTI-2 feature.

### Active R4 CDex and the existing PAS shell

Prepare the isolated cache with
**`hl7.fhir.us.davinci-cdex#2.0.0`**, the version in the checked-in CDex launch
profile, and required locally resolved contents. The owned source and its
`hl7.fhir.us.davinci-cdex` subdirectory must exist and be empty before deliberate
seeding. Stop the subscriptions server before running:

```powershell
dotnet run --project src\fhir-candle\fhir-candle.csproj --framework net10.0 -- --reference-implementation hl7.fhir.us.davinci-cdex --load-package "hl7.fhir.us.davinci-cdex#2.0.0" --load-examples false --r4 provider --r4 payer --fhir-source "$env:TEMP\fhir-candle-sdk6-0919-01\source" --fhir-package-cache "$env:TEMP\fhir-candle-sdk6-0919-01\cache" --use-official-registries false
```

1. Confirm both R4 tenants and the exact package, with no package examples or
   unplanned outbound requests. Use local copies of the synthetic Patient/Task
   inputs in `r4/cdex-task-resources.json` and the hook sequence in
   `Sdk6ContractTests.Operations`; never use patient data from package examples.
2. Open `/davinci-cdex/tasks?store=provider`, verify Task ID/status/code and
   filters, exercise the currently exposed status buttons, and inspect the
   resulting Task/output content through the resource viewer. Check actual
   hook-generated results as well as rendered status text.
   **Source-level setup caveat:** the API hook fixtures omit optional
   `Task.code`, while the current table column dereferences `t.Code!.Coding`.
   Do not claim the unmodified API fixture is a proven browser seed or
   silently repair this pre-existing UI input mismatch. Record the actual
   seed and any resulting exception; any required behavior repair needs
   separate scope disposition.
3. Open `/davinci-pas/walkthrough?store=provider` and verify only its current
   incomplete shell with provider/payer context. Its SDK-dependent code remains
   a compiled consumer; claim-entry/submission markup is already disabled.
   Do not load the unrelated unpinned PAS CI package or enable dormant controls.

Run only one configuration at a time, and stop each locally started process
by its specific attached session/PID. Missing packages, environments, browser
access, or local receiver facilities remain explicit outstanding prerequisites;
neither the default launch profile nor a successful compilation substitutes.

### Manual record (separate from command/matrix evidence)

| Workflow | Tenants | Browser/actions/result |
|---|---|---|
| Store/count/filter/viewer/update/delete/metadata | r4, r4b, r5 | Not run |
| Local generated and received subscriptions | r4, r4b, r5 | Not run; offline RI setup/receiver evidence outstanding |
| RI tour | r4, r4b, r5 | Not run |
| Active HTI-2 | r4 | Not run |
| CDex Task table/status/output | provider/payer, R4 | Not run; pinned cache and Task seed caveat outstanding |
| Current PAS incomplete shell | provider/payer, R4 | Not run; no claim-submission workflow asserted |

Append actual records with OS/browser/version, tree identity, exact seed and
configuration, route/actions, result and exceptions. Required missing evidence
blocks final acceptance; it does not reduce the supported envelope.

## Package supplier replacement evidence (2026-11-13)

This additive record supersedes only the earlier package-supplier statement:
the host now pins **fhir-pkg-lib 2026.1113.1240**, replacing
`Firely.Fhir.Packages` **5.0.2**. It does not rewrite the historical SDK
migration, dependency-refresh, platform, or browser evidence above. The
Phase 2 adapter checkpoint is
`b1c33bdb4f0a23c7d47d70eb23ac443124494d33`; Phase 3 tests/docs were verified
over that checkpoint. **At the Phase 3 commit checkpoint**, final unfiltered
verification had not yet run; subsequent verification is recorded in the
slot's `plan.md`.

### Published supplier and acceptance evidence

The exact published NuGet archive and its source at nuspec commit
**`9059ebb310d0c0ad8a7079f806d238bc9b25611c`** in
`GinoCanessa/dotnet-fhir-packages` were inspected during qualification.
The archive is 769,151 bytes, SHA256
`A87E68BE7C80AF12CC8C14077338A88325F9D1E5FF3BFE0AA1BBA5B075534910`.
It provides `lib/net10.0/FhirPkg.dll` and `lib/net11.0/FhirPkg.dll`.
The MIT license attributes **Copyright (c) Gino Canessa. All rights reserved.**;
the supplier notice was added to `THIRDPARTYNOTICES.md`, retaining the Firely
SDK and unrelated notices.

| Inspected public API | Candle use |
|---|---|
| `FhirPkg.Models.PackageReference.Parse` | Private translation to the legacy data identity; the native reference is not exposed. |
| `RegistryClientFactory.CreateClientForEndpoint` / `RegistryEndpoint` | Explicit FHIR-NPM or NPM clients, one per configured endpoint in the existing order. |
| `IRegistryClient.GetPackageListingAsync` / `DownloadAsync(ResolvedDirective)` | Listings projected with the retained SemanticVersioning policy; existing explicit archive URLs, not a new native resolver/dependency engine. |
| `DiskPackageCache.ImportAsync(Stream, InstallCacheOptions, CancellationToken)` | Operation-local native import with `Unmodified = true`; concrete manifest identity discovery does not replace the requested legacy identity. |
| `PackageRecord.DirectoryPath` / `GetPackageContentPath` | Locate the imported distribution for publication; installed lookup, raw manifest reads, and deletion remain Candle-owned. |

The nuspec requires Microsoft.Extensions DI/logging abstractions, Options,
and Http **11.0.0** for net10.0, and Http **11.0.0** for net11.0. Both published
assets compile and bind through the existing adapters on the supported runtimes.
Source inspection alone was not acceptance evidence: the fixed manifest's
property-scoped author converter still projects object authors into the
supplier's string-valued `Author` property. That projection must never be
serialized back into the cache.

**Failed-candidate history:** the exact **2026.1112.1330** importer rejected
the unchanged object-valued author fixtures on both runtimes with
`PackageInstallException` / unreadable manifest JSON and an inner conversion
failure at `$.author`. `Unmodified` did not bypass validation. No manifest
rewrite, validator bypass, local archive parser, or weakened fixture was used.

**Fixed-candidate execution:** before resumed Phase 2 source edits, isolated
reflection imports of the unchanged string/object-author archives passed
**12/12 on each runtime**, .NET **11.0.0** and **10.0.15**. The subsequent
Phase 2 focused integration gate passed **19/19 per TFM**, including all
original R4/R4B/R5 rows, the seven-request CI cold/update sequence and
four-request warm checkpoint with no additional download. Final Phase 2
builds had zero warnings/errors. Exact raw manifest/resource bytes, local
Newtonsoft author name/email/url and string/object origin, full distributions,
empty directories, invalid-import/publication cleanup, and opt-in export passed.
These executed results, not the release note, resolve the earlier author gate.

### Identity, byte, path, disposal, and source-binding contract

- Configuration, registry order/URLs, directive syntax and characterized
  parser/latest/local-build quirks remain unchanged. Scope is bare and Name
  unscoped; equality/hash ignore Scope and compare Name/Version case-sensitively.
  CI keeps resolved download identities distinct from installed tags.
- The physical content path remains
  `Path.Combine(configuredRoot, Name + "#" + Version, "package")`, ignoring
  Scope. An existing `packages` child does not change the selected root.
  Directory existence, missing/corrupt manifest behavior, and Candle's raw
  Newtonsoft manifest/version/date interpretation remain the lookup contract.
- Native import uses a unique `.candle-package-import-*` temporary root on
  the selected cache filesystem. Native cache/stream objects are disposed
  before publication; native download results/streams and the introduced
  registry transport have bounded ownership. Success/failure cleanup removes
  only the operation's temporary root. No native default cache, root metadata,
  journal, lock, second persistent backend, or operator cache conversion is
  published.
- Publication preserves distributed manifest/resource bytes and the complete
  tree, including sibling files, examples outside `package`, and empty
  directories. Parent metadata not replaced by distributed files and unrelated
  identities survive. `Delete` removes only the requested content directory.
  The manager still passes `package` to `LoadPackage`: the example flag controls
  recursive examples within that directory, not loading arbitrary outside
  siblings. CI still deletes old content before downloading; no transactional
  update or automatic rollback is promised.
- The six data-only contracts (`PackageReference`, `PackageCatalogEntry`,
  `PackageListing`, `PackageRelease`, `Dist`, `Versions`) now live in
  **`FhirCandle.Models`**, assembly **`FhirCandle.Common`**. Service/helper
  consumers naming retired supplier CLR types must **rebind and recompile**;
  there is no precompiled-consumer binary compatibility claim or impersonated
  Firely namespace. Rollback requires rebinding/recompiling those consumers
  again, plus separately restoring affected cache snapshots when needed.
- SDK/model **6.8.0**, all `candleR4`/`candleR4B`/`candleR5` and
  `coreR4`/`coreR4B`/`coreR5` aliases and `AddPackageAliases`, tenant routing,
  operation/reflection gates, singleton forwarding, shared version settings,
  DateTime-based version generation, and operator configuration remain intact.

### Actual resolved graph changes

Phase 2 compared fresh host/test graphs against the preserved failed-candidate
graphs. The fixed pin's minimum supplier-reachable closure is a real net10.0
major-line change, not “no retained graph drift.” All entries below resolve
to exactly **11.0.0**, not a later patch.

| Microsoft.Extensions package | Host net10.0 | Tests net10.0 |
|---|---|---|
| Configuration | Added | 10.0.15 → 11.0.0 |
| Configuration.Abstractions | Added | 10.0.15 → 11.0.0 |
| Configuration.Binder | Added | 10.0.15 → 11.0.0 |
| DependencyInjection | Added | 10.0.12 → 11.0.0 |
| DependencyInjection.Abstractions | Added | 10.0.15 → 11.0.0 |
| Diagnostics | Added | 10.0.12 → 11.0.0 |
| Diagnostics.Abstractions | Added | 10.0.15 → 11.0.0 |
| Http | Added | 10.0.12 → 11.0.0 |
| Logging | Added | 10.0.12 → 11.0.0 |
| Logging.Abstractions | Added | 10.0.15 → 11.0.0 |
| Options | Added | 10.0.15 → 11.0.0 |
| Options.ConfigurationExtensions | Added | 10.0.12 → 11.0.0 |
| Primitives | Added | 10.0.15 → 11.0.0 |

Explicit package counts are host net10.0 **40 → 53**, host net11.0
**38 → 38**, tests net10.0 **86 → 86**, and tests net11.0 **74 → 74**.
The host net11.0 closure remains framework-pruned. Tests net11.0 upgrade
only **Http** and **Diagnostics**, **10.0.12 → 11.0.0**; the other required
explicit closure entries were already 11.0.0, with framework entries
distinguished from packages. No unrelated version deltas or new pruning
occurred against those snapshots.

The initial replacement removed `Firely.Fhir.Packages` and `SharpZipLib`
and introduced `fhir-pkg-lib`; both retired packages are absent from all four
current host/test graphs. Newtonsoft.Json **13.0.4**, SemanticVersioning
**3.0.0**, and models **6.8.0** remain. DiagnosticSource **11.0.0** is unchanged
where explicitly resolved (net10.0); net11.0 framework pruning is not a new
package upgrade. Pkcs remains **10.0.15** in host/test net10.0 and **11.0.0**
in host net11.0; test net11.0 Pkcs remains framework-pruned. Phase 3's normal
restore reconfirmed the four current counts, exact pin, retained versions,
and retired-package absence. All four full resolved package-record sets are
identical to the qualified Phase 2 snapshots; no unrelated dependency changed.

### Phase 3 inventory and Windows command results

`Sdk6PackageTests` now executes **22 cases**, plus the unchanged
`FhirNpmPackageTests` manifest case and two `HostingCompatibilityTests`:
**25 focused cases per TFM**. The two new theories each execute R4/R4B/R5:

- `PackageVariantsExamplesAndSupplementsPreserveTenantLoading`: umbrella
  substitution, existing-suffix replacement, listing/download fallback,
  missing variant, version-specific manager preference, newest-last ordering,
  named-tenant/same-version sibling isolation, false/true JSON/XML examples,
  full-distribution retention, exact `name@version` then `name#version` then
  name-only supplements, exact loaded directives/IDs/supplements and retrieved
  authored resources.
- `PopulatedCacheDeletionReinstallationAndRollbackPreserveNeighbors`:
  independently seeded legacy raw-byte trees, full affected-tree snapshots,
  replacement, targeted/idempotent deletion, bounded registry reinstall,
  offline reopening and snapshot restoration, with same-name other-version
  and scoped CI neighbors, parent metadata and empty directories retained.
  This validates the legacy seam, not an original-supplier binary replay.

Commands came from root `AGENTS.md` and the existing both-framework workflow,
run from the repository root on Windows with the .NET 11 SDK:

```powershell
dotnet build --configuration Release --framework net11.0
dotnet test --configuration Release --framework net11.0 --no-restore --verbosity normal --filter "FullyQualifiedName~fhir.candle.Tests.Sdk6PackageTests|FullyQualifiedName~fhir.candle.Tests.FhirNpmPackageTests|FullyQualifiedName~fhir.candle.Tests.HostingCompatibilityTests"
dotnet restore
dotnet build --configuration Release --no-restore
dotnet test --configuration Release --no-build --verbosity normal -p:TestTfmsInParallel=false --filter "FullyQualifiedName~fhir.candle.Tests.Sdk6PackageTests|FullyQualifiedName~fhir.candle.Tests.FhirNpmPackageTests|FullyQualifiedName~fhir.candle.Tests.HostingCompatibilityTests"
```

| Gate | Actual result |
|---|---|
| Release/net11.0 build and focused tests, runtime 11.0.0 | Passed; 25/25, zero failures/skips, zero warnings/errors. |
| Normal restore and unqualified Release build | Passed; both TFMs built with zero warnings/errors. |
| Unqualified focused tests, sequential TFMs | net11.0/.NET 11.0.0: 25/25; net10.0/.NET 10.0.15: 25/25. Zero failures/skips/warnings/errors. |

One initial focused run passed 22/25: the new sibling reread tried to capture
an existing binding twice. Its test-only correction uses the existing Reuse
mode; an intermediate build also caught a misnamed enum member. No production
or fixture expectation changed; the corrected gates above pass.

### Still-provisional rollback and release qualification

Phase 2's explicit verifier-owned export contains **six ordinary/CI package
trees, 27 files and 24 directories per TFM**, with identical bytes/directories
between TFMs. The original delegate completed the original-supplier readback
at **2026-11-13T20:03:22Z**, thread
`thread:delegated-task:command%3Amcp%3A46ff7ead-68d6-4d35-b282-13bafd29e075%3Adelegate-task%3A5ed26c10-c461-42e3-9907-bc3344cff3cc`.
Its host-prepared worktree
`E:\ai\worktrees\fhir-candle\t3-548272b7-7ea32808d642469e` remained at the
Phase 1 checkpoint **`561f2f97c01643efd32d62ce8d2d423903eb57b1`**;
initial and final index and tracked diffs were empty.

The exact commands ran from that worktree root:

```powershell
dotnet restore
dotnet build --configuration Release --no-restore
dotnet test --configuration Release --no-build --verbosity normal -p:TestTfmsInParallel=false --filter "FullyQualifiedName~fhir.candle.Tests.Sdk6PackageTests.LegacyCacheIsReadableWithoutRegistries"
```

All three commands exited **0**, with zero warnings/errors. The readback
passed **1/1 on net10.0/.NET 10.0.15** and **1/1 on net11.0/.NET 11.0.0**.
It consumed replacement-written ordinary and tagged CI copies across
R4/R4B/R5, with no registry acquisition or public CI lookup.

Both-TFM host/test graphs and runtime dependency/type origins identified the
original **Firely.Fhir.Packages 5.0.2**, assembly **5.0.2.0**; the replacement
was absent. All four inspected DLLs matched the resolved original asset.
SDK/models **6.8.0** and all `candleR4`/`candleR4B`/`candleR5` and
`coreR4`/`coreR4B`/`coreR5` aliases were retained.

The export and each verifier copy were unchanged by direct byte/directory
comparison: **54 files, 21,400 bytes, 51 directories including the root,
and six empty directories**. The canonical snapshot SHA256 was
`C7C9702DA7675B6C3AD57F425081DCC023E7880EFBFF6475063E8FF26C2B420A`.
Both GUID-named verifier copies were removed and the environment restored
exactly in `finally`; the host-managed worktree was not shell-removed.
Harness-only null-environment restoration and metadata-overload issues were
corrected before the final result, not package failures.

**This qualifies synthetic original-supplier cache-write readback on Windows
only**, not external precompiled CLR/ABI compatibility, live/operator caches,
or Linux/macOS. The new snapshot-restoration test alone does not establish
original-supplier interoperability. Before rollout, independently back up
affected package trees; source rollback alone cannot recover
delete-before-download losses.

**At the Phase 3 commit checkpoint**, the conductor's final unfiltered local
and both-TFM suite gates had not yet run and were scheduled after the Phase 3
commit. Subsequent verification is recorded in `scratch/1113-01/plan.md`.
No Linux/macOS execution, live registry, browser/RI workflow, ambient operator
cache migration, or operator qualification was performed here. Windows
synthetic/offline tests are not evidence for those obligations; neither this
section nor historical SDK success closes them.
