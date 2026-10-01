# Firely SDK 6 compatibility

This is the consumer inventory, oracle policy, and verification checklist for
the SDK 6 migration. **Final migration verification is blocked.** Authored
tests, a successful scoped gate, a browser exercise, and an OS/framework
result are different evidence. The records below do not substitute one for
another.

The 2026-09-30 recovery passes the normal Windows net10.0 full-suite and
scoped gates. Remaining platform, all-target, browser, and offline RI-package
requirements still prevent final acceptance; see the dated evidence below.

## Accepted dependency and product envelope

| Product | SDK package / model assembly | Capability `fhirVersion` | Store assembly |
|---|---|---|---|
| R4 | `Hl7.Fhir.R4` **6.2.1** / `Hl7.Fhir.R4.dll` | `4.0.1` | `FhirCandle.R4` |
| R4B | `Hl7.Fhir.R4B` **6.2.1** / `Hl7.Fhir.R4B.dll` | `4.3.0` | `FhirCandle.R4B` |
| R5 | `Hl7.Fhir.R5` **6.2.1** / `Hl7.Fhir.R5.dll` | `5.0.0` | `FhirCandle.R5` |

The compatible Base/Conformance closure remains **6.2.1** and the host uses
`Firely.Fhir.Packages` **5.0.2**. The existing `Hl7.Fhir.R6` **6.2.1**
reference is test-only; it creates no R6 product tenant. Pins remain explicit
in the existing projects, not in a new package-management layer. Fresh
Windows asset inventory agrees with this cohort across the ten consumers
and their three target-framework graphs. This is dependency evidence,
not a passing all-target runtime matrix.

`src/fhir-candle.props` specifies C# 14 and
`net10.0;net9.0;net8.0`. Windows, Linux, and macOS remain supported verification
obligations. The existing workflow installs .NET 10, 9, and 8 and runs its
unchanged restore/build/test sequence on all three operating systems.

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
dotnet build --configuration Release --framework net10.0
dotnet test --configuration Release --framework net10.0 --no-restore --verbosity normal
```

At the final implementation tree, run the existing workflow's exact all-target
sequence on each available Windows/Linux/macOS environment:

```powershell
dotnet restore
dotnet build --configuration Release --no-restore
dotnet test --configuration Release --no-restore --verbosity normal
```

Prerequisites are the .NET 10, 9 and 8 SDKs/runtimes. Record tree/commit identity,
command outcome and diagnostics per cell. Require the full existing suite plus
new cases, no new skips/quarantines or weakened expectations, zero errors and
no newly introduced warnings. Recheck dependency assets/aliases. Do not replace
missing operating systems with cross-OS claims, containers, or a remote workflow
publication. The CI edit only adds symmetric path triggers for props, shared
project items, Razor, corpus data and the workflow itself; jobs, action versions,
branch filters, SDK/OS matrix, and commands are unchanged.

### Nine-cell framework/platform record

| OS | Target | Evidence / identity | Remaining requirement |
|---|---|---|---|
| Windows | net10.0 | 2026-09-30: full suite 4,258/4,258 and scoped UI/contract gate 3,093/3,093; zero warnings/errors/skips; runtime tree and UI source identified above | Local gate passes; the final-tree Windows all-target sequence remains required |
| Windows | net9.0 | Not run; SDK/runtime available, no final-tree command result or warning count | Required Windows all-target sequence outstanding |
| Windows | net8.0 | Not run; SDK/runtime available, no final-tree command result or warning count | Required Windows all-target sequence outstanding |
| Linux | net10.0 | Not run; no tree/result/diagnostics | Required environment and build/test evidence outstanding |
| Linux | net9.0 | Not run; no tree/result/diagnostics | Required environment and build/test evidence outstanding |
| Linux | net8.0 | Not run; no tree/result/diagnostics | Required environment and build/test evidence outstanding |
| macOS | net10.0 | Not run; no tree/result/diagnostics | Required environment and build/test evidence outstanding |
| macOS | net9.0 | Not run; no tree/result/diagnostics | Required environment and build/test evidence outstanding |
| macOS | net8.0 | Not run; no tree/result/diagnostics | Required environment and build/test evidence outstanding |

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
