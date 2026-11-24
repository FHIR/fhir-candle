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

The R4, R4B, R5, and test-only R6 model packages are pinned to **6.9.0**.
FHIR tenant versions and [assembly aliases](assembly-aliases.md) are unchanged.
The host now uses **`fhir-pkg-lib` 2026.1113.1815** instead of
`Firely.Fhir.Packages`; see [package selection and import limits](../user/loading-data.md).

The November 12 Windows checkpoint used **Firely 6.8.0** and
**`Firely.Fhir.Packages` 5.0.2**. At that checkpoint the `net11.0` and
`net10.0` builds had no warnings or errors, and **4,458 tests per framework**
passed with no skips. Those results and the original **6.2.1** migration
inventory below describe their respective source trees, not qualification of
the current release candidate.

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
- **M10:** reached `_has` predicates evaluated with the `TypeSearch`
  enumeration's dictionary, including in Subscription snapshot searches, reuse
  the first materialized related-result array per key, even if it is empty.
  References are checked separately for each candidate. This is not an atomic
  snapshot across candidates or keys: later candidates reuse that first array,
  and different keys may first be evaluated at different times. Child
  `TypeSearch` enumerations apply the same rule with their own dictionaries, and
  new requests start fresh. Forward-reference recursion through
  `SearchTester.TestForMatch` does not receive the enumeration's dictionary.
  Direct GET regressions verify exact Bundle identities and complete inner-search
  tallies across R4, R4B, and R5, including status exclusion, the first Subscription
  search after a `Provenance.target` update, and identical filters in separate
  child enumerations.
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

At the final implementation tree, run the workflow's all-target .NET
sequence on each available Windows/Linux/macOS environment:

```powershell
dotnet restore
dotnet build --configuration Release --no-restore
dotnet test --configuration Release --no-build --verbosity normal -p:TestTfmsInParallel=false
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

Use `--no-launch-profile` to skip the R5-only development profile and its
live core-package request. Pass the isolated configuration after `--`:

```powershell
dotnet run --project src\fhir-candle\fhir-candle.csproj --framework net10.0 --no-launch-profile -- --r4 r4 --r4b r4b --r5 r5 --fhir-source "$env:TEMP\fhir-candle-sdk6-0919-01\source" --fhir-package-cache "$env:TEMP\fhir-candle-sdk6-0919-01\cache" --use-official-registries false
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
dotnet run --project src\fhir-candle\fhir-candle.csproj --framework net10.0 --no-launch-profile -- --reference-implementation subscriptions --load-package "hl7.fhir.uv.subscriptions-backport#1.1.0" --load-examples false --protect-source true -m 1000 --r4 r4 --r4b r4b --r5 r5 --fhir-source "$env:TEMP\fhir-candle-sdk6-0919-01\source" --fhir-package-cache "$env:TEMP\fhir-candle-sdk6-0919-01\cache" --use-official-registries false
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
dotnet run --project src\fhir-candle\fhir-candle.csproj --framework net10.0 --no-launch-profile -- --reference-implementation hl7.fhir.us.davinci-cdex --load-package "hl7.fhir.us.davinci-cdex#2.0.0" --load-examples false --r4 provider --r4 payer --fhir-source "$env:TEMP\fhir-candle-sdk6-0919-01\source" --fhir-package-cache "$env:TEMP\fhir-candle-sdk6-0919-01\cache" --use-official-registries false
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
  parser/local-build quirks remain unchanged. The migration also preserved
  `latest` quirks at the time; cross-registry `latest` now uses
  semantic precedence instead of text ordering. Explicit exact-version
  acquisition remains unchanged. On an ordinary miss, automatic acquisition
  still tries only the selected exact version in registry order. A cache key
  that aliases a different version casing is an error on the automatic path,
  not a hit or an ordinary miss. See
  [Automatic package version selection](../user/loading-data.md#automatic-package-version-selection).
  Scope is bare and Name unscoped; equality/hash ignore Scope and compare
  Name/Version case-sensitively.
  CI keeps resolved download identities distinct from installed tags.
- The physical content path remains
  `Path.Combine(configuredRoot, Name + "#" + Version, "package")`, ignoring
  Scope. An existing `packages` child does not change the selected root.
  Explicit exact-version lookup still checks only for the content directory;
  missing/corrupt manifest behavior and Candle's raw Newtonsoft
  manifest/version/date interpretation remain unchanged. Automatic lookup also
  checks the cache key for a version-case collision before accepting a hit.
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

## R1 package remediation evidence (2026-11-14)

This additive record updates the active host pin to **fhir-pkg-lib
2026.1113.1815** and records the local H1 registry-listing and H2 cold-manifest
remediation. The 1240 results above and the historical SDK migration remain
history, not 1815 qualification. After the Phase 3 checkpoint
`2b03c9266bd9ed6fb295312f700f47c6339ba33a`, the 1240 full suite passed
**4,472/4,472 per TFM**, with zero warnings/errors/failures/skips. That suite
was not rerun or relabeled as an 1815 result; R1 used the affected delta gates
below. One R1 review preceded remediation; tests are not a second code review.

### Exact supplier provenance versus downstream execution

The published 1815 NuGet archive is **771,784 bytes**, SHA256
`570378A1814291C8EE1D933DFFBABBA7CE47FA7A3619C5199D9DF7D53081AA7B`,
with nuspec source commit
`de9b68a943ab1f0ea67c27a6cf122c2d006058ec` in
`GinoCanessa/dotnet-fhir-packages`. Published/runtime DLL hashes match:

| Asset | SHA256 |
|---|---|
| `lib/net10.0/FhirPkg.dll` | `F976D3583BB6995B44D9443D85B40E274BCB1BBD43F375007CB1051E26965920` |
| `lib/net11.0/FhirPkg.dll` | `9AF548BEC3A9E7CF36009FED43D1CE8EEFB193F2C31BAA7086D2B3CC12532226` |

MIT attribution and the existing supplier notice are unchanged. Public parsing,
leaf registry/download, cache construction, validating `ImportAsync`, and
content-path APIs remain bound through the existing adapters. Supplier-owned
repository/date property converters now participate in mandatory full-manifest
identity validation: shorthand repository strings and compact numeric dates
are accepted without removing validation or reserializing distributed bytes.
Candle's raw publication and local Newtonsoft interpretation are unchanged.

**Separate, previously executed supplier evidence:** published-binary probes
passed 221 cases / 3,203 assertions per TFM, including 180 native metadata
imports/reopens per TFM; matching-source unit/integration suites passed
2,731 / 282 per TFM. Supplier CI included Windows/Linux/macOS qualification.
These establish supplier capability, not Candle runtime, platform, loading,
graph, or copied-cache acceptance. The downstream results below were measured
independently with the actual original and candidate binaries.

### H1: measured fallback, diagnostics, and limits

The original **Firely.Fhir.Packages 5.0.2** clients continued after malformed
listing JSON for **both FHIR-NPM and NPM** on both runtimes. The earlier
malformed-FHIR test expected propagation and refused the next legitimate
request; its **18 passed / 1 failed per TFM** result remains a failed
prescription, not successful fallback qualification. An owning-plan correction
repaired the finite scripts and archive helper before recovery attempt 2.
Four original NPM observations per TFM established continuation before final
strict assertions were pinned; final tests require continuation, not an
accept-either result.

`LatestRegistryTransportFailurePreservesFallback` has independent FHIR/NPM
theory rows. Each protocol covers 404, 503, transport failure, actual timeout,
and malformed JSON, before/after a usable or unusable winner: **40 scenarios
per TFM** across both protocols, plus configuration controls. Tests use real
clients, finite ordered handlers/loopback responses, exact listing/acquisition
sequences, no unlisted/live request, retained winners, and the original generic
latest-resolution failure when all versions are unusable. Explicit acquisition
adds no metadata/dependency request. Original `HttpClient.Timeout` and native
100-ms response-deadline controls are distinct; candidate tests directly
observe `RegistryResponseTimeoutException` with an infinite HttpClient timeout,
cancellation, and resource disposal.

Production containment surrounds only the native **listing await**:

| Listing result | Candidate behavior |
|---|---|
| 404/null | Empty versions, no outage warning. |
| `HttpRequestException` | Empty versions; `http` warning with status, or `transport` warning without status. |
| `RegistryResponseTimeoutException` | Empty versions and `deadline` warning. |
| `System.Text.Json.JsonException`, endpoint `FhirNpm` or `Npm` | Empty versions and `listing-json` warning, preserving measured continuation. |

Each contained non-404 failure emits one host warning with package identity,
registry scheme/host/path, category and status where available. Captured
message/structured-field assertions exclude URL credentials, user-info/query
values, body sentinels, exception messages and exception objects. No broad
catch, retry, new registry request, global logging or limit-policy change was
introduced. Version projection, resource/manifest parsing, archive import,
configuration, caller cancellation and unrelated exceptions remain outside
the new containment.

**Configuration distinction:** the original invalid-endpoint control fails in
`InstallPackages` with `System.Exception` / generic latest-resolution failure
and zero requests/acquisition. The candidate rejects it explicitly during
`Init` with `ArgumentException`, also without acquisition. The exception
type/lifecycle difference is disclosed, not asserted identical.
**Arbitrary listing-error parity is uncharacterized**, including mid-body
`IOException` / `HttpIOException`; the original's broader internal swallowing
does not prove equivalence for unexecuted errors. This is not a blanket
error-policy preservation or an approved observable incompatibility.

### H2: cold shapes and independently matched raw bytes

`ColdImportPreservesLegacyManifestShapes` executes **18 labeled rows per TFM**:
R4/R4B/R5 × repository string (`github:example/repo`), object, or absent × date
integer (`20240203123456`) or string (`"20240203123456"`). Integer-date fixtures
have string authors; string-date fixtures have object authors. Unknown metadata
and UTF-8 content remain authored bytes. A seekable BCL tar-input buffer fixes
only the test harness; no payload, production extraction or acceptance
assertion was weakened.

Original 5.0.2 black-box cold acquisition/loading passed all rows. Candidate
1815 passed separate public validating `ImportAsync(Unmodified=true)` controls
and real Candle cold acquisition, legacy paths/identity, local author
values/origin, local-time date interpretation, FHIR values, R4/R4B/R5 manager
JSON/XML retrieval, unchanged neighbors and operation-local cleanup.
All **36 candidate archive/manifest/Patient/Observation hash sets** exactly
match the original per-TFM records. The following archive/manifest pairs apply
identically to both suppliers on both TFMs:

| FHIR | Repository | Date | Archive SHA256 | Manifest SHA256 |
|---|---|---|---|---|
| R4 | absent | integer | `72B55262F58BC2118510EB2DF0ECA3474856DBDDF245DC94547E3A39187C6C52` | `DE6D93E6959DCDA5BD45CFAD1B488121D88DE5B6AC4F723F0126585A62035A5C` |
| R4 | absent | string | `8C1BD911EA5339DAFDB9CA3FFF1A43DFD3748A637BA9E4434CC9CC92EADCE4CE` | `E8F3531DFF153039010A3FE3E096F4941699B2A8F411ED08FB89603003347503` |
| R4 | object | integer | `FE118B62C2F04C9F10CE1825C62B27A144B910CB3FF7A3316A9975B5E547FF60` | `4CB82482145D8A0B0CFF2568E86BCD2E1780ED0A37F536F19B634D623C73861D` |
| R4 | object | string | `C97C004F0D7AACD906BAF97A23EEB471312D845F0FAF2C81085A51187978E999` | `34A74DA20D8ADC8BC76ECDB61D5CB654129C5404F0C9528FBE3DCED821E3AF7E` |
| R4 | string | integer | `F0D518A97C455209F9153EBDD72AC93123DB82C205592399FB21CD403566BAE7` | `2BC0754264E7F8AE641A6AEF4F84D157DFCF28DE27E58966B92CA316A68950CA` |
| R4 | string | string | `A4914F55F3F2E2D63C099B9E8CBEE59662D84459E126A2020F55BE10850F49C9` | `0396CD29D2E11828EE7839BCEBEC8F51F2A511B577380A86F310F9E85DE79C84` |
| R4B | absent | integer | `79CD5F3D78E82BB1F58E34E73A65AA63100F68D95D15C95BEE8286895BA8A8C0` | `FF1008AAA75F63CC1B678FFE9995E5875A183DC650432B2344CE4BAD3E6F73E1` |
| R4B | absent | string | `B859AC08BDA2F570535B9E0384332F0CFC9EDCB9EC32CD64CA16427BC24AC151` | `912ADAD413555B98882B056658D93980AC1474580175282834A51443F7355C6F` |
| R4B | object | integer | `165A613CA8B1614A515F9A2428E8530D6A3EED8C7A1640C5F332A1FF3BD6C9D1` | `F5DA82A179FAFF9902CD6F8BC52762F9EBA371D6992115296154CC18BC1532E0` |
| R4B | object | string | `AB1ED4BEE17CBC3BCEBF8690C424B2CDBC189225B7113E64686BA2E6A5CBDE2F` | `6F17FC3C3507622BFAB1746FBF3F2BC637FBF9BA6D5187EE81C839274E58BAA9` |
| R4B | string | integer | `0B68B947020DF48E0FACC01C1A5E58C0BD60C85B924D693C533B5B548D9C3DD2` | `CBB7E6311E8DE9D7FF2F14CFB0E78432DAD6C83032CD43439731AAC4707FC9CE` |
| R4B | string | string | `5557FE36FE4CB5E30F839F39A880FD6561A2E930DFCF1BB61CE45C3D9076D12E` | `E7FA6DA27941A4A98BF73F9374A73665B3FF2FC1FA7068D2EE08F07DED9134F2` |
| R5 | absent | integer | `119F0285E315DC527BBC9478754524DA8F94DFCE98F3813E21ECBCE574C574E0` | `43D6EAEE86042A5F6DC0440521AB3766273681E884EB63442AE8BB5AB6FEEDA4` |
| R5 | absent | string | `819D43DC1DB29EF6C542F6A542B8F719DB94B20B2AD6B32776B4C9E64D26FADB` | `7E02CF9C12E2B56602C482861AEF2D63E739B5433F9FB99C6B3519D4E5BA74F3` |
| R5 | object | integer | `5EFFBE0609EF0E08EB78631B1C97BF5B950E4A1692E33217675DD0DC505D1C74` | `E53AF63DFD919CB243D0ED3B95E52B8EAD1CD746A7C784DC625B12FC4DBE3771` |
| R5 | object | string | `ECC47C14EC31B048C59A4D7A051F05A7307B8CCAF3174C967FEFC16C9E00A694` | `4C8A9631DE17683AA924FFA0FFE4AF3CA9384B5253221C3E457259FAC815972B` |
| R5 | string | integer | `EE255970CC86E4655FE1F01E7B21C7E24B12025AABA652F05A000BEF4E61D4D7` | `B0CA9C47AE6369E7E25ED5EBABC54A0034B6F3CFA942838A8216055DCD4A6B21` |
| R5 | string | string | `4AC904EBCEF27975529FA554999833338CDD2BE8949FE0908E7FADD1C2A117AB` | `8153BFE674F4BF8F4DA4A4A1A3685B8408BB956D8BCE7E26C59983EF12ECC706` |

Every row's Patient JSON SHA256 is
`AAD7793B93A2C558308B6C7737B269F2249FBF5DAF3E067A61B1413BD492EA7A`;
Observation XML SHA256 is
`CECA55BE0117652F594CA58C6468509B097389225BB96F48D9AC10F29801800E`.

### Actual Windows delta gates, graphs, and copied-cache readback

Commands are the existing AGENTS/workflow build and sequential both-TFM
VSTest commands shown above, with the named focused selectors; no new runner
or full-suite escalation was needed. Runtimes were .NET **11.0.0** and
**10.0.15**, with the .NET 11 SDK.

| Gate | Actual result |
|---|---|
| Original 5.0.2 strict H1/H2, both TFMs | 20/20 per TFM: two protocol rows plus 18 cold rows; zero failures/skips/warnings/errors. |
| Candidate local net11 build and five-filter H1/H2/directive/protocol/native-publication gate | Build passed; 23/23, zero failures/skips/warnings/errors. |
| Candidate restore, both-TFM Release build, full affected package-class/manifest/hosting gate | Commands passed; 45/45 per TFM, zero failures/skips/warnings/errors, including existing CI/acquisition/loading/neighbor/cleanup controls. |
| Fresh 1815 export and independent-copy original-supplier readback | Original restore/build/test exited 0; `LegacyCacheIsReadableWithoutRegistries` passed 1/1 on each TFM, zero warnings/errors/failures/skips. |

An intermediate candidate export rerun had net10 **45 passed** and net11
**44 passed / 1 failed** in the unchanged Phase 3 variant-loading timestamp
assertion. The existing one-second freshness bound was diagnosed; no code,
assertion or tolerance changed. A bounded rerun of that same affected gate
with a fresh export passed 45/45 on each TFM. The failure remains recorded,
not waived or hidden by the green result.

Fresh complete host/test resolved package-record comparisons against 1240
change **only fhir-pkg-lib 1240 → 1815**. Counts remain host **53/38** and
tests **86/74**, net10/net11. There is no new package, pruning change or other
dependency/version delta. The previously documented minimum
Microsoft.Extensions **11.0.0** closure, models **6.8.0**, SDK aliases,
Newtonsoft.Json **13.0.4**, SemanticVersioning **3.0.0** and crypto pins remain.
Firely.Fhir.Packages/SharpZipLib remain absent from candidate graphs/outputs;
the four candidate output FhirPkg DLLs match their published per-TFM assets.

The fresh candidate export was copied before readback; it was not reseeded
with original-supplier output. In the existing host-prepared Phase 1 checkout,
original Firely **5.0.2**, assembly **5.0.2.0**, read ordinary explicit and tagged
CI R4/R4B/R5 contents without registries/public CI lookup, seeding or deletion.
Original graph counts were host **41/39**, tests **87/75**, net10/net11, with
no FhirPkg, models 6.8.0/aliases retained. All four original supplier DLLs match
SHA256 `85F0C6347FD2431C685C24586696BC164F2CEB2CB5268BFCD041C22FF8FD0166`.
The original HEAD remained
`561f2f97c01643efd32d62ce8d2d423903eb57b1`, tree/index
`30b984cb7a3e2951f3e796cc517a6ed2ae30912e`, with clean tracked/untracked/index
state and unchanged original test bytes. A harness-only graph-count audit
initially included project references; counting package entries corrected it
before the readback test, without rerunning restore/build or changing source.

The export and copy each remained **54 files / 21,400 bytes / 51 directories
including root / six empty directories**, canonical snapshot SHA256
`FE8EB702E8EF1E3FADECABB83CFE5401F7574FA3969A9C76BFA4D0FC4B86C12D`.
Final comparison matched every file hash and directory against the saved
pre/post snapshots. Only those two explicitly named verifier-owned temporary
roots were removed afterwards; durable evidence and the host worktree remain.
Replay/export environment values were restored exactly. This is fresh 1815
synthetic Windows copied-cache evidence, not the historical 1240 readback.

### Standing findings and unqualified release boundaries

**M1 deletion-before-publication, M2 unlisted-version filtering, and M3 native
limits/ambient-setting drift remain standing Medium findings, not fixed or
waived.** Disk staging/publication, local converters, default policy/factory
bindings, CI predeletion and Common contracts were deliberately unchanged.
Back up affected package trees before rollout; reverting code cannot restore
deleted cache content. Existing validation/negative import controls still
apply; no extractor/serializer bypass or consumer limit policy was added.

H1/H2 local delta gates do not establish arbitrary-error parity or shipping
approval. No Candle Linux/macOS, live registry, ambient/operator cache,
browser/RI, or precompiled-consumer CLR/ABI qualification was executed.
Supplier cross-platform results and historical SDK/full-suite passes cannot
close those boundaries. Common-type consumers still require rebinding and
recompilation; no additional review, push, PR or publishing action occurred.

## R2 Phase 5 host compatibility corrections (2026-11-15)

This additive record supersedes the R1 M2/configuration/body-error limitations
only for the cases measured below. The exact supplier pin remains
**fhir-pkg-lib 2026.1113.1815**. No dependency manifest, native policy, disk
publication, CI algorithm, shared serializer or public contract was changed.
Root `AGENTS.md` supplied the build/test commands.

### Original 5.0.2 measurements and exact restoration

Only the new portable black-box tests/helpers were temporarily inserted into
the existing test file in the host-prepared checkout
`E:\ai\worktrees\fhir-candle\t3-548272b7-7ea32808d642469e`.
The original Firely aliases remained; no native references, replacement-warning
assertions, production/project edits, restore/install, checkout lifecycle
operation, staging or commit were used there. Tests exercised the original
`FhirPackageService` and real manager, not a proxy implementation.

Initial and final verification (including every `finally` restoration) found:

- HEAD `561f2f97c01643efd32d62ce8d2d423903eb57b1`, tree/index
  `30b984cb7a3e2951f3e796cc517a6ed2ae30912e`; clean tracked/untracked/index state.
- Exact restored test bytes: **72,665**, SHA256
  `7202F5DD399933FEB9492DFC4A22ACADAC3B2BE0282A1D0F9A94ECBA9154E380`.
- Original supplier assembly **5.0.2.0**; all four host/test net10/net11 DLLs
  retained SHA256
  `85F0C6347FD2431C685C24586696BC164F2CEB2CB5268BFCD041C22FF8FD0166`.

Bounded original tests passed **6/6 per TFM**: FHIR/NPM rows for markers,
invalid-registry workflows and real truncated responses. A final marker-only
strengthening passed **2/2 per TFM**, using a lower-version fallback so an
incorrect partially retained listing cannot accidentally pass. Original
commands, from that checkout, were:

```powershell
dotnet test src\fhir-candle.Tests\fhir-candle.Tests.csproj --configuration Release --framework net11.0 --no-restore --verbosity normal --filter "FullyQualifiedName~fhir.candle.Tests.Sdk6PackageTests.UnlistedRegistryVersionsPreserveBaselineSelection|FullyQualifiedName~fhir.candle.Tests.Sdk6PackageTests.UnusedInvalidRegistryDoesNotPreventOfflineStartup|FullyQualifiedName~fhir.candle.Tests.Sdk6PackageTests.TruncatedRegistryResponsePreservesBaselineFallback" --logger "console;verbosity=detailed"
```

The same command ran with `--framework net10.0`; final marker-only runs used
`--filter "FullyQualifiedName~fhir.candle.Tests.Sdk6PackageTests.UnlistedRegistryVersionsPreserveBaselineSelection"`
on each TFM. All final original commands exited **0**, zero warnings/errors,
failures or skipped tests.

### Measured original/candidate behavior

- **M2:** one native listing, projected entry by entry, preserves original
  Newtonsoft string-marker eligibility: absent/null/empty-string eligible;
  nonempty strings (including `"false"` and whitespace), booleans and numeric
  scalars excluded. Object/array markers invalidate the **entire** listing;
  candidate emits exactly one sanitized `listing-json` warning. Both protocols
  cover 13 marker forms, prereleases/build metadata, all-unlisted variant
  ineligibility, eligible variant discovery followed by explicit acquisition
  of its hidden requested version, and metadata-free explicit acquisition.
  Existing semantic and cross-registry string ordering tests still pass.
- **M4:** listing-await containment adds only
  `HttpIOException` with `HttpRequestError.ResponseEnded`, returning empty
  versions and one sanitized `response-body` warning. Both protocols retain
  fallback before/after usable/unusable winners, with finite typed failing
  streams and actual Content-Length underflow: **8 scenarios per transport
  form per TFM**. The TCP helper binds only ephemeral loopback, drains the
  request header, verifies exact finite GET/path scripts, sends a valid JSON
  prefix with larger declared length, then uses orderly `Shutdown(Send)`.
  Direct response-stream controls prove `ResponseEnded`, not RST/JSON failure;
  request, stream/handler and owned socket disposal checks pass. Separate
  controls retain plain `IOException`, non-ResponseEnded `HttpIOException`
  and cancellation propagation. Existing non-configuration acquisition
  catches are unchanged.
- **M6:** registrations are ordered `Lazy<IRegistryClient?>` values. Init
  and Dispose do not construct unused clients. Only factory
  `ArgumentException` is diagnosed/cached as null; all three consumers skip
  that slot. One sanitized warning identifies ordinal/protocol, never the
  malformed URL or exception/inner exception. Other construction failures
  remain outside acquisition catches. Both protocols prove real manager
  startup with no package lists, invalid-only generic required-use failure,
  before/after-valid fallback across latest, explicit and variant-existence
  workflows, one-warning reuse, credential-bearing malformed URL redaction
  and no construction/warning/request on unused paths. The original R1
  configuration control now asserts Init success then generic resolution
  failure, matching the measured original lifecycle.
- **L1:** both cold-manifest and variant-loading tests construct
  `CompatibilityBindings` before `manager.Init()`. The FreshInstant oracle
  and one-second tolerance are unchanged; no sleep or clock mutation.

Two preliminary original runs each passed 4/6 per TFM because of test-helper
errors: buffered HttpContent wrapped the typed body exception, then a server
wait for the still-open client half caused a timeout. Direct stream reading
and closing the owned server socket after FIN corrected those controls.
Corresponding preliminary candidate runs passed 48/50; one earlier build
caught a misplaced helper scope (zero warnings, one compiler error). These
were local harness corrections, not waived service failures or relaxed
assertions; all final gates below pass.

### Final Windows verification and evidence

```powershell
dotnet build --configuration Release --framework net11.0
dotnet test src\fhir-candle.Tests\fhir-candle.Tests.csproj --configuration Release --framework net11.0 --no-restore --verbosity normal --filter "FullyQualifiedName~fhir.candle.Tests.Sdk6PackageTests"
dotnet build --configuration Release --no-restore
dotnet test --configuration Release --no-build --verbosity normal -p:TestTfmsInParallel=false --filter "FullyQualifiedName~fhir.candle.Tests.Sdk6PackageTests|FullyQualifiedName~fhir.candle.Tests.FhirNpmPackageTests|FullyQualifiedName~fhir.candle.Tests.HostingCompatibilityTests"
```

All commands exited **0**. Both builds had **zero warnings/errors**.
The net11 package-class gate passed **50/50**; the sequential affected gate
passed **53/53 on net11.0** and **53/53 on net10.0**, with zero failures/skips
or compiler warnings/errors. Runtimes were .NET **11.0.0** and **10.0.15**.
R4/R4B/R5 loading, CI, manifests, hosting and prior fallback scenarios remain
covered. No separate restore or full-suite run was added.

Durable session evidence root:
`C:\Users\ginoc\.copilot\session-state\beafb647-9d7c-48f9-afa1-f06c709ca1a5\files\r2-host-fixes\`.
`qualify_original.py` records exact commands and restores held bytes in
`finally`; `qualified-original-{net11.0,net10.0}.log` and
`final-markers-original-{net11.0,net10.0}.log` hold final original results.
`original-initial.json`, `original-final.json`,
`qualified-original-{initial,final}.json` and
`final-markers-original-{initial,final}.json` preserve restoration evidence;
the exact byte snapshots and ported slices are retained beside them.
`candidate-{build-net11,sdk6-net11,build-both,affected-both}-final.log`
contains the final prescribed commands/results. Earlier harness failure logs
are retained separately.

**Still outstanding:** M1 deletion-before-publication is explicitly excluded,
not fixed or waived. M3 finite native archive limits/ambient-policy drift
requires an approved behavior-preserving envelope; none was selected here.
M5 null Versions/null version entries fail inside native provenance before
Candle receives a listing and require supported supplier qualification.
There is no broad NRE catch, shared serializer mutation, second JSON
parser/request, local supplier implementation or supplier upgrade.
Phase 6 remains a prerequisite gate, not implemented work. These Windows
synthetic results do not establish Linux/macOS, live/operator, browser/RI,
precompiled CLR/ABI or complete selected-request closure.

## R2 Phase 6 configurable limits and null-listing fallback (2026-11-17)

The user's later explicit M3/M5 direction supersedes the earlier supplier-only
blocker above: use the existing native finite defaults with Candle-owned
configuration, and contain null-listing exceptions while trying remaining
registries. **M3 and M5 are resolved within this selected scope.** Finite
archive acceptance is an intentional, user-approved narrowing, not proof of
unbounded parity with the original installer. The exact supplier remains
**fhir-pkg-lib 2026.1113.1815**; dependency metadata, native archive/identity
validation, HTTP buffering, cache publication and the CI algorithm are unchanged.

### Policy and containment boundaries

- Common contains six neutral numeric properties, XML documentation, required
  numeric CLI values, external configuration keys and CLI-over-external-over-
  default binding. The complete default/CLI/JSON/unprefixed-environment/
  `Candle`-prefixed-environment matrix, validation rules and depth-33 example
  are in [Loading initial data](../user/loading-data.md#package-import-limits).
  The prefix concatenates directly with each key, without an extra underscore.
- One disk-adapter mapping explicitly assigns **all six** native limits and
  calls public `Validate()`. Enabled-cache Init validates/snapshots before
  readiness or directory creation. Nonpositive values, entry > expanded and
  checked sum/product overflow retain native `InvalidPolicy` /
  `PolicyValidation`, leaving IsReady/IsConfigured false and the cache path
  absent. Disabled-cache Init skips unused policy validation; non-numeric
  values remain CLI/binder failures rather than policy errors.
- The default public Candle disk constructor explicitly uses CandleConfig
  defaults. Service cache imports and the real lazy registry factory share the
  validated first-Init snapshot. All six `FHIRPKG_MAX_*` fields are ignored,
  even when invalid; filesystem retries still use the existing defaults and
  ambient `FHIRPKG_FS_*` policy. Later CandleConfig mutation cannot alter an
  initialized service. Warm-cache reads are not imports. Factory limits govern
  NPM publication preparation, **not download spooling**; there is no new HTTP
  buffering or memory ceiling.
- A typed six-limit-error-code catch before the existing published-acquisition
  catch-all emits only the stable enum and matching Candle CLI option, then
  preserves registry fallback. No exception object/message, archive path,
  manifest/body or URL/query is logged by that warning. CI retains its explicit
  propagated typed limit failure; other acquisition catches are not redesigned.
- Only the native `GetPackageListingAsync` await gains protocol-qualified
  `NullReferenceException` containment for FHIR/NPM, returning empty versions
  with one sanitized `listing-structure` warning. **Any native NRE within that
  await boundary is contained; the catch cannot prove which member was null.**
  This disclosed tradeoff does not catch later host projection/import failures
  or add general I/O/cancellation containment, a second parser/request, private
  serializer hooks, stack/message inspection or a supplier implementation.

### Measured Windows regressions

`NullVersionListingPreservesFallback` executes both real native clients against
null `versions` and null version entries. Direct native controls prove NREs
reach the await. Both protocols cover before/after usable/unusable winners
(16 service scenarios per TFM), exact ordered listing/acquisition scripts,
retained earlier winners, unchanged generic all-unusable failure, no acquisition
without a winner, disposed responses/handlers and a single warning without
credential/query/body/exception sentinels. Existing unrelated I/O, typed
non-ResponseEnded I/O and cancellation negative controls remain passing.

All six configured boundaries use the **same policy** for an accepted archive
at N and a rejected archive at N+1, with real validating imports:

| Measure | Setting / accepted N | Rejected N+1 |
|---|---:|---:|
| Actual gzip compressed bytes | 265 | 266 |
| Aggregate expanded file bytes | 643 | 644 |
| Largest entry bytes | 512 | 513 |
| Archive entries (including directories) | 4 | 5 |
| Normalized/exact-spelling path characters | 58 | 59 |
| Archive path components | 3 | 4 |

The gzip neighbor is selected from a finite deterministic payload inventory
using actual compressed length, never an estimate. The path fixture uses
decomposed Unicode whose exact spelling is longer than its NFC canonical path.
Directory entries count explicitly. Separate real archives establish default
32-component acceptance, default 33 rejection and configured depth-33 success;
the count includes `package` and filename. Raw manifest/payload bytes,
neighbor preservation, no rejected identity, and operation-staging cleanup
remain checked. Every limit also exercises real service acquisition with a
rejected first registry, exact sanitized option warning and successful second
registry. Real CI acquisition proves the service's configured import policy
and propagated typed failure. Snapshot tests mutate all six config values
after Init, before lazy NPM construction/import, and retain the original policy;
a restrictive warm service still reads already installed content.

`ConfigTests` checks six defaults, all six JSON keys and CLI precedence with
byte values beyond Int32, required numeric arguments and binder errors.
`AmbientNativePackageLimitsAreIgnored` starts **two child runners per TFM**:
all six native fields set to restrictive valid `1`, then all six set to
`not-a-number`. Only the child ProcessStartInfo environment is changed; no new
test mutates the shared process environment. The child-only marker selects the
assertion branch of the exact same test, preventing recursion/no-op success.
Each child uses the built Release TFM project with `dotnet test --no-build
--no-restore` and exact FullyQualifiedName. Inherited `VSTEST_*` connection state
is removed only there; DOTNET runtime/host paths remain intact. Both pipes are
drained concurrently, execution is bounded to 150 seconds, and failure cleanup
can kill only the owned child PID/tree.

Each child verifies delivered native environment values, native ambient-policy
controls, both real Candle environment providers, the default public cache
constructor and configured real lazy AddRegistry/NPM listing/download/import
(no reflection-injected clients). Parent TRX assertions require **exactly one
total/executed/passed test**, zero failed and the exact expected test name.
Temporary archives/TRX files belong to fixtures and are cleaned up.

### Executed verification and durable evidence

Root `AGENTS.md` is the command source. All four final commands ran locally:

```powershell
dotnet build --configuration Release --framework net11.0
dotnet test src\fhir-candle.Tests\fhir-candle.Tests.csproj --configuration Release --framework net11.0 --no-restore --verbosity normal --filter "FullyQualifiedName~fhir.candle.Tests.Sdk6PackageTests|FullyQualifiedName~fhir.candle.Tests.ConfigTests"
dotnet build --configuration Release --no-restore
dotnet test --configuration Release --no-build --verbosity normal -p:TestTfmsInParallel=false --filter "FullyQualifiedName~fhir.candle.Tests.Sdk6PackageTests|FullyQualifiedName~fhir.candle.Tests.ConfigTests|FullyQualifiedName~fhir.candle.Tests.FhirNpmPackageTests|FullyQualifiedName~fhir.candle.Tests.HostingCompatibilityTests"
```

Every command exited **0**, zero warnings/errors. The focused net11 gate
passed **168/168**; the sequential affected gate passed **171/171 on net11.0**
and **171/171 on net10.0**, zero failures/skips. R4/R4B/R5 loading, manifests,
CI and singleton hosting controls are included. Runtimes remain .NET 11.0.0 /
10.0.15. No standalone restore/install, original-checkout port or full suite
was required. Fresh host/test graphs retain counts 53/38 and 86/74
(net10/net11), exact1815 and no Firely.Fhir.Packages/SharpZipLib.

Evidence root:
`C:\Users\ginoc\.copilot\session-state\beafb647-9d7c-48f9-afa1-f06c709ca1a5\files\r2-final-m3-m5\`.
`{build-net11,focused-net11,build-both,affected-both}-accepted.log` and matching
`.result.json` files contain full final command output, exits, counts and tested
source hashes; `accepted-gate-results.json` aggregates those results.
`isolation-boundaries-both*.log` and matching result files record detailed
bounded child/actual boundary evidence. `graph-inventory.json` and
`final-file-hashes.json` retain dependency identities and final file hashes.

An initial build exposed one new xUnit2031 assertion-style warning, corrected
without suppression. The first focused run passed 162/168: six new fixture
archives incorrectly used leading `./`, which native validation correctly
rejects as traversal. The corrected decomposed-Unicode fixture passed 168/168;
the boundary tests were then strengthened to distinct exact N/N+1 archives
under one policy and all four final gates rerun successfully. Preliminary logs
remain separate; no validation or assertion was weakened.

**Still outside this result:** M1 disk deletion-before-publication remains
explicitly excluded, unchanged and not waived. Historical broader listing-error
qualification, Linux/macOS, live/operator caches and registries, browser/RI,
and precompiled CLR/ABI limitations remain; these Windows synthetic tests do
not establish those claims. The existing source-rebinding requirement and
historical prefix/platform records above are preserved.

## Original-review M4 identifier mutation ownership (2026-11-19)

This bounded addition addresses **original-review M4**, not the historical
migration phase-4 labels or unrelated package-remediation findings above.
It preserves completed H1/H2/M1/M2/M3 contracts and does not qualify the
remaining migration/platform/browser gates.

### Successful-write ownership before publication

The shared `ResourceStore<T>` extracts ordinal key sets from Firely's
`IIdentifiable<List<Identifier>>` and `IIdentifiable<Identifier>` SDK shapes.
Null/absent identifiers and empty lists contribute no keys. The existing
`GetIdentifierKey` representation remains exactly `system|value`, without
normalization, validation or filtering of empty components; duplicate keys
coalesce locally.

All three successful CRUD paths reconcile ownership inside the existing
resource writer lock, after primary assignment/removal and subscription commit
work, but **before publication or instance callbacks**. Update uses the actual
in-lock previous resource and runs only after precondition checks; delete uses
the resource actually removed. Departed keys (old minus current) are removed
only when their current mapped ID still equals the mutated resource ID.
Retained keys and another resource's live mapping are not evicted. Current keys
use insertion-only `TryAdd`: owner removal does not scan/promote surviving
duplicates, although an explicit later successful write can claim an unowned
key.

This replaces the dead local-interface checks and post-callback identifier
maintenance. Resource-then-subscription lock order and release, publication,
event order/count, evaluators and exception/response behavior remain unchanged;
no observer or evaluator moved into the writer lock. There is no resolver,
search, terminology, duplicate-policy or production M3 repair.

The guarantee is for successful quiescent writes and index state committed
before publication, not an atomic lock-free reader snapshot. Readers racing an
in-flight write retain the existing concurrency envelope; a callback can see a
later writer's content. Synchronous reentrancy cannot replay an outer write's
obsolete key tail. Key-set work is O(identifier count), with per-mutation
allocations and no resource-count scan; no performance benchmark was run.
Already populated stale maps are not rebuilt retrospectively: use fresh or
reloaded in-memory tenants. No persistence migration, rollback-on-callback
exception or automatic duplicate promotion is promised.

### Independent lifecycle and ordering proof

The [corpus inventory](../../src/fhir-candle.Tests/data/sdk6-compatibility/README.md#original-review-m4-identifier-mutation-inventory-2026-11-19)
documents **144 authored lifecycle rows**: `replace`, `put-create`,
`delete-reuse`, `non-owner-update`, `non-owner-delete`, and `remove` across
Patient/Observation list and collection Bundle/Provenance scalar shapes,
R4/R4B/R5, JSON/XML, and HTTP/direct. Six direct reentrant rows and one
Cartesian inventory fact bring the authored focused inventory to 151, not
proof of execution by themselves.

Primary mutation subjects are explicitly POST-created with initial ordinary
read/include proof and generated-ID capture; collection Bundles use the
resource-type endpoint. Only the explicit missing-ID PUT-as-create case uses
PUT for creation. Same-ID sibling setup uses the existing direct ingest
override, not an HTTP feature. Independently authored JSON/XML and the BCL
oracle retain complete submitted content and locally bound ID/version/time,
with stable identity, progressing versions, reused timestamps and matching
headers. GET/form POST searches check exact membership/modes, fullUrls, self
URLs and deduplication.

Controls cover retained identifiers, unchanged keys, destination collisions,
non-owner writes/deletes, no promotion and later explicit reclamation,
same-ID/key sibling isolation, literal references, untyped fallback, ordinary
identifier/reference-identifier search and empty key components. Deletion
alone can hide stale keys because lookup also requires a live resource;
different-generated-ID reuse proves cleanup. Stale If-Match must leave the
old content/version/ownership intact. Explicit JSON `identifier: []` remains
a 422/structure negative control; successful clearing uses omission with
independently authored XML absence expectations, not empty/missing
normalization.

Direct callbacks prove A→B→C ordering: the outer callback sees only B/version
2 before synchronously writing C/version 3, with exactly two update
notifications and one nested write. The outer response may still describe
B/version 2 while a later read observes C/version 3. A subsequent throwing
C→D callback preserves the exact direct exception; an ordinary read then binds
committed D/version 4 and only D includes the target. No successful response is
fabricated after that exception.

### Narrow dependent M3 expectation synchronization

The stale-If-Match assertion and adjacent comment in
`BundleAuthorizationTests.AuthorizedConditionalAndHeaderSemanticsRemainStable`
now require exact **412 PreconditionFailed**, not obsolete 500. The preexisting
resource-store rejection precedes assignment; committed M3 forwards that
classified status/outcome and both bundle failure branches retain it. The old
expectation predates M3. The exact status helper, `MustNotPersist` request,
stored `Updated` family/version 2, both bundle kinds, authorization/null cases,
all versions and every unrelated 500 expectation are preserved.

This is dependent-test consistency, not a new production response contract or
an observed response used as its own oracle. Attempt 1's focused gates passed
151/151 on both TFMs, but its net11.0 protection union executed 1,558, passed
1,555 and failed only the three R4/R4B/R5 occurrences at that stale assertion.
**No pre-repair protection-union run exists**; the earlier result is not an
old-baseline runtime reproduction. Those failures remain history; the fresh
green results below qualify the amended tree before commit.

### Actual resumed verification

Environment: **SDK 11.0.100**, **Windows 10.0.26300 win-x64**; **net11.0 /
.NET 11.0.0** and **net10.0 / .NET 10.0.15**, with **Firely 6.9.0** in all
three product libraries. This identifies this M4 run without rewriting older
dependency prose. Canonical root `AGENTS.md` and the owning phase plan supplied
these exact commands, run from the repository root in this order:

```powershell
dotnet test src\fhir-candle.Tests\fhir-candle.Tests.csproj --configuration Release --framework net11.0 --no-restore --verbosity normal --filter "FullyQualifiedName~fhir.candle.Tests.Sdk6ContractTests.IdentifierMutation"
dotnet test src\fhir-candle.Tests\fhir-candle.Tests.csproj --configuration Release --framework net10.0 --no-restore --verbosity normal --filter "FullyQualifiedName~fhir.candle.Tests.Sdk6ContractTests.IdentifierMutation"
dotnet test src\fhir-candle.Tests\fhir-candle.Tests.csproj --configuration Release --framework net11.0 --no-restore --verbosity normal --filter "FullyQualifiedName~Sdk6ContractTests.IdentifierMutation|FullyQualifiedName~Sdk6ContractTests.ReferenceChainsAndIncludesStayTenantLocal|FullyQualifiedName~Sdk6ContractTests.IncludedFullUrlsIdentifyReturnedResources|FullyQualifiedName~Sdk6ContractTests.LocalReferenceEquivalenceRejectsForeignBases|FullyQualifiedName~Sdk6ContractTests.SearchFamiliesReturnExpectedIds|FullyQualifiedName~Sdk6ContractTests.UpdateRejection|FullyQualifiedName~Sdk6ContractTests.SubscriptionEvents|FullyQualifiedName~fhir.candle.Tests.SubscriptionLifecycle|FullyQualifiedName~fhir.candle.Tests.SubscriptionNotificationLifecycleTests|FullyQualifiedName~BundleAuthorization"
dotnet test src\fhir-candle.Tests\fhir-candle.Tests.csproj --configuration Release --framework net10.0 --no-restore --verbosity normal --filter "FullyQualifiedName~Sdk6ContractTests.IdentifierMutation|FullyQualifiedName~Sdk6ContractTests.ReferenceChainsAndIncludesStayTenantLocal|FullyQualifiedName~Sdk6ContractTests.IncludedFullUrlsIdentifyReturnedResources|FullyQualifiedName~Sdk6ContractTests.LocalReferenceEquivalenceRejectsForeignBases|FullyQualifiedName~Sdk6ContractTests.SearchFamiliesReturnExpectedIds|FullyQualifiedName~Sdk6ContractTests.UpdateRejection|FullyQualifiedName~Sdk6ContractTests.SubscriptionEvents|FullyQualifiedName~fhir.candle.Tests.SubscriptionLifecycle|FullyQualifiedName~fhir.candle.Tests.SubscriptionNotificationLifecycleTests|FullyQualifiedName~BundleAuthorization"
```

| Command | Exit | Executed | Passed | Failed | Skipped | Compiler/analyzer warnings / errors |
|---|---:|---:|---:|---:|---:|---:|
| Focused net11.0 | 0 | 151 | 151 | 0 | 0 | 0 / 0 |
| Focused net10.0 | 0 | 151 | 151 | 0 | 0 | 0 / 0 |
| Protection union net11.0 | 0 | 1,558 | 1,558 | 0 | 0 | 0 / 0 |
| Protection union net10.0 | 0 | 1,558 | 1,558 | 0 | 0 | 0 / 0 |

Normal compilation remained enabled, building the test project and all three
shared-source store imports. No restore/install, logger switch, filter
exclusion or assertion relaxation was added. Output was summarized in memory,
not saved as new result files. The unions emitted the negative notification
controls' invalid-URI, rejected SMTP authentication and REST
InternalServerError diagnostics; these were not compiler errors or failed
tests.

Each focused run discovered **144 lifecycle + six reentrant + one fact**,
with 50 theory rows per product version, 78 direct/72 HTTP and 75 JSON/75 XML.
Every required protection family had nonzero observed discovery on **each**
TFM; the counts below were identical between the two unions:

| Required family | Executed and passed per TFM |
|---|---:|
| `IdentifierMutation*` | 151 |
| `ReferenceChainsAndIncludesStayTenantLocal` | 72 |
| `IncludedFullUrlsIdentifyReturnedResources` | 36 |
| `LocalReferenceEquivalenceRejectsForeignBases` | 168 |
| `SearchFamiliesReturnExpectedIds` | 96 |
| `UpdateRejection*` | 64 |
| `SubscriptionEvents*` | 483 |
| `SubscriptionLifecycleTests` | 49 |
| `SubscriptionLifecyclePolicyTests` | 6 |
| `SubscriptionNotificationLifecycleTests` | 97 |
| `BundleAuthorizationTests` | 273 |
| `Controllers.BundleAuthorizationRoutingTests` | 63 |

Version-labeled union results were **R4 510, R4B 518, R5 518**, plus twelve
unversioned facts. The corrected bundle theory passed all three version rows,
retaining its twelve internal version/kind/authorization combinations.
H2's 483 top-level results comprise 72 replay, 120 empty-range, 144
defaults/aliases/body-precedence, 120 negative-bound and 24 legacy-fallback
rows, plus two isolation facts and one inventory fact. The passing isolation
facts require **240 internal extreme rows** and **96 internal concurrency
rows** (72 gated conversions and 24 four-pair rendezvous rows). Their existing
owned deadline-bounded child mechanism verifies exactly one intended
executed/passed child test per fact; these internal inventories are not extra
top-level result rows. Normal console verbosity did not expose successful
child inventory text.

All four results precede this documentation-only append. They are **not a
post-commit final gate**, phase completion, a full-suite rerun, other-OS or
browser evidence, live-provider qualification, or migration-wide acceptance.
The parent retains integration, commit identity checks and post-commit final
verification. No M5 or unrelated acceptance claim is included.

## Original-review M7: zero-safe decimal approximation (2026-11-20)

This bounded accepted-behavior repair is **not an SDK-causation claim**.
Decimal-number and Quantity `ap` now share one internal predicate implementing
Candle's closed, query-centered interval with tolerance 10% of the requested
magnitude. Zero matches exactly zero, negative requests reflect the positive
window, and both endpoints match. Zero/opposite-sign guards precede
same-sign subtraction, avoiding division and overflowing endpoint construction.
FHIR permits a server-defined approximation degree; it does not universally
mandate this local 10% policy.

Decimal-number evaluation intentionally corrects integer-spelled `ap` requests
against native decimal nodes (`ap0` agrees with `ap0.0`), widening only eligible
already-parsed integer alternatives. Standalone integer-spelled **non-ap**
decimal-node requests remain ineligible. The parser still selects a typed array
for the whole request: `ap0,eq100` matches only zero, but `ap0.0,eq100` matches
zero and hundred. Ignored alternatives cannot contribute default zero; existing
request-literal self links remain exact. Quantity still checks units/system
before the predicate. Integer/date approximation, every other prefix/equality
rounding rule, missing-scalar handling, parser/dispatch, comparator TODOs and
unit-conversion exclusions are unchanged.

The additive isolated partial has **84 decimal-number + 84 Quantity + 12
retained integer/date rows + one inventory fact per framework**. Both affected
numeric families have seven fresh-graph groups across R4/R4B/R5 × JSON/XML × HTTP/direct.
Independently authored raw inputs are seeded by ordinary public PUT; the custom
number definition precedes native-decimal Observation candidates. Every probe
completes GET and form POST with exact memberships and complete raw resource,
metadata, mode and URL assertions. No historical graph/provider/expectation,
replay helper or wire normalization was changed.

See the [M7 corpus, exact commands and execution evidence](../../src/fhir-candle.Tests/data/sdk6-compatibility/README.md#original-review-m7-decimal-number-and-quantity-approximation-2026-11-20)
for all seven groups, literal memberships, command repetitions and timings,
warnings, first failing requests, per-family discovery and qualification limits.
Observed local Windows results before this documentation append:

| Gate | net11.0 | net10.0 |
|---|---|---|
| Unchanged-production focused RED | 181 discovered: 13 passed, 168 failed, 0 skipped; exit 1, reproduced after inventory-warning correction | 181 discovered: 13 passed, 168 failed, 0 skipped; exit 1 |
| Corrected focused GREEN | 181/181 passed, 0 failed/skipped; exit 0 | 181/181 passed, 0 failed/skipped; exit 0 |
| Bounded H1/H2/M1–M7 protection union | 1,592/1,592 passed; exit 0; 4.5075 min test time | 1,592/1,592 passed; exit 0; 4.4512 min test time |

Both scoped application builds passed without restore, warnings or errors.
GREEN and the corrected RED baseline have zero compiler/analyzer warnings or
errors. The initial net11 RED introduced two xUnit2031 inventory warnings;
using the existing predicate overload corrected them without suppression.
An intermediate repaired net11 run passed 169/181: twelve new self-link
expectations incorrectly removed an invalid alternative despite the unchanged
parser's request-literal echo. Only those two new expected self paths were
corrected; memberships and complete-content assertions were not relaxed.
One PowerShell union-summary wrapper parse failure preceded dotnet invocation
and discovered no tests; the unchanged sanctioned union then ran successfully.

RED reached the first explicit decimal GET in six groups per family:
direct `DivideByZeroException` (“Attempted to divide by zero.”), or HTTP 500.
No completed Bundle membership or failure-body contract was established for
those requests; subsequent POST/signed probes were not reached. Lexical rows
first completed eighteen non-ap GET/POST controls; the next number GET
`ap0,eq100` returned 200 with no entries instead of zero, while Quantity failed
on zero. Range RED observed division by zero, not overflow. GREEN completes
every probe: **60 M7 theory rows per version, fifteen per each of twelve
tuples**, plus the fact, and **2,424 search requests per framework**.

All retained protection families have nonzero observed discovery on both
frameworks; exact counts are in the linked corpus. Expected negative
notification diagnostics (invalid URI, rejected SMTP authentication and REST
InternalServerError) remain distinct from compiler diagnostics or test failures.
Existing H1/H2/M1–M6 code/tests and earlier evidence remain unchanged.

These are pre-commit, pre-documentation-final-gate results. The implementer will
rerun the exact final four commands on the documented tree and return that
evidence; parent integration and post-commit gates remain outstanding.
No full-suite, other-OS, browser, live-provider, numeric-precision, performance
or migration-wide qualification is inferred. No M8 work is included.

## M7 remediation R1: exact represented-input approximation (2026-11-20)

This explicitly supersedes only Phase 1's rounded-tolerance implementation
description and its then-known no-unresolved-bug qualification. The original
sections, inventories, results and limits above remain historical evidence.
The local closed, query-relative 10% policy is unchanged; this is neither
an SDK-causation claim nor a universal FHIR approximation requirement.

Source reasoning predicted outward rounding of
`abs(requested) * 0.1m`: at `0.0000000000000000000000000006`, the mathematical
`6e-29` tolerance becomes represented `1e-28`. Independently executed public
RED, rather than that prediction, confirmed the defect on **both** frameworks
with the helper byte-identical to Phase 1 commit
`5208670a3e9c6aee43a6a865fd4197910073957e`.

Separate positive/negative raw JSON and XML graphs use native decimal
Observation Quantities, UCUM mg, no zero, and definition-first public PUTs
of the same active `SearchParameter/m7-decimal`. The exact positive candidates
are `0.0000000000000000000000000005`,
`0.0000000000000000000000000006`,
`0.0000000000000000000000000007`, and
`78000000000000000000000000000`; the negative graph reflects all four.
Number requests are exactly
`Observation?m7-decimal=ap0.0000000000000000000000000006` and
`Observation?m7-decimal=ap-0.0000000000000000000000000006`.
Quantity uses `value-quantity` with those identical ordinary-decimal operands
and `|http://unitsofmeasure.org|mg` (encoded by unchanged request helpers).
The exact positive interval `[5.4e-28, 6.6e-28]` and negative reflection
`[-6.6e-28, -5.4e-28]` each contain only their represented center:
`Observation/scale28-p6` or `Observation/scale28-n6`, total 1.

Both RED runs discovered **49 tests: 1 passed / 48 failed / 0 skipped**,
exit 1. All seed PUTs passed complete raw-content assertions. All 48 GETs
returned **200 OK searchsets with three tiny entries / total 3**, instead
of the literal center-only expectation / total 1. Positive entries were
`scale28-p6,scale28-p7,scale28-p5`; negative entries were
`scale28-n5,scale28-n6,scale28-n7` (all Observation IDs).
Far candidates were absent. Every family/sign/version/format/boundary row
failed the total assertion; **none reached POST** or subsequent per-entry
content assertions. Direct outcomes had already passed
`information/success/Type search successful`; HTTP status/media/body checks
passed, with no separately observed HTTP OperationOutcome diagnostic.
The first failure on each framework was R4B/JSON/direct GET
`http://localhost/fhir/m7-decimal-number-scale28-positive-r4b/Observation?m7-decimal=ap0.0000000000000000000000000006`,
reported as `System.InvalidOperationException`, `Assert.Equal()` expected
1 / actual 3.

Only after both confirmations, the helper retained its zero/sign guards and
replaced the final tolerance comparison with same-sign
`decimal distance = decimal.Abs(candidate - requested)` and requested
magnitude, rejecting strictly `distance > decimal.MaxValue / 10m` before
multiplication. That exact constant quotient is
`7922816251426433759354395033.5`; equality proceeds. Otherwise it returns
`distance * 10m <= magnitude`, keeping endpoints inclusive without a rounded
scale-29 tolerance. The large same-sign candidates exercise the overflow
guard. The historical “no division” description is superseded only by this
constant guard: **no candidate or request division**, double conversion,
outer endpoints, epsilon or generalized numeric redesign is introduced.
Callers/parser/dispatch, lexical and unit eligibility, other prefixes and
equality rounding, integer/date policies and conversion exclusions remain
unchanged; original inclusive endpoint/range-safety coverage is retained.

The additive provider/theory/fact contributes **48 rows + one fact**, with
12 rows per family/sign, 24 per family or sign, 16 per FHIR version and four
per each version/format/boundary tuple. The separate inventory pins all
unique labels/tuples and definition-first exact five-resource graph identities.
All original 181 tests/providers/groups/probes and their fact are unchanged.
GREEN completes unchanged exact membership, full raw-resource, metadata,
mode and URL assertions on GET **and form POST**: **96 added search requests
per framework**. Combined M7 is **230 tests**, 228 theory rows (76 per version,
19 per tuple) plus two facts, and **2,520 primary requests** (2,424 + 96).
The expected bounded protection inventory is **1,641**, also actually passed.

See the [R1 corpus and exact command/request matrix](../../src/fhir-candle.Tests/data/sdk6-compatibility/README.md#m7-remediation-r1-represented-scale-28-windows-2026-11-20)
for every reached tuple, encoded paths, independent IDs, command literals
and diagnostic qualifications. Canonical root `AGENTS.md` and the phase plan
supplied the sequential no-restore commands. Observed pre-append results:

| Gate | net11.0 | net10.0 |
|---|---|---|
| Unchanged-helper scale-28 RED | 49: 1 passed, 48 failed, 0 skipped; exit 1; 8.5625 s tests / 19.17 s elapsed | 49: 1 passed, 48 failed, 0 skipped; exit 1; 8.7863 s / 23.68 s |
| Corrected application build | Exit 0; 11.13 s | Exit 0; 17.26 s |
| Focused M7 GREEN | 230/230; exit 0; 30.0366 s tests / 1:04.41 elapsed | 230/230; exit 0; 29.4071 s / 1:17.09 |
| Bounded protection union | 1,641/1,641; exit 0; 4.4730 min tests / 5:06.28 elapsed | 1,641/1,641; exit 0; 4.5122 min / 5:12.44 |

All dotnet commands had zero compiler/analyzer warnings/errors and no skips.
One overcomplicated reporting-wrapper PowerShell parse error occurred before
focused dotnet invocation (“The Try statement is missing its Catch or Finally
block”); a shorter line-separated grouped report fixed reporting only,
without rerunning an already executed test or changing sanctioned commands.
No setup/compile/content failure, assertion relaxation, numeric normalization,
install/restore, manifest change or product retry occurred.
All thirteen protection selections reconciled on each framework to
230,96,12,84,1,1,151,31,64,483,55,97,336. Expected negative notification
invalid-URI, rejected SMTP authentication and REST InternalServerError
messages remain distinct from test failures and compiler diagnostics.

These measurements precede this append; final documented-tree builds/unions
will be rerun and reported to the parent. Parent direct inspection,
independent verification, staging/commit/identity and post-commit gates remain
outstanding, not claimed here. This is bounded local Windows qualification
only, not full-suite, other-OS/browser/live-provider/performance or general
numeric-precision acceptance. H1/H2/M1–M6 and read-only review inputs remain
untouched; M8 is excluded.
