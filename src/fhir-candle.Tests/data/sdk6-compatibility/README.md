# SDK 6 consumer-contract corpus

This corpus belongs to the existing xUnit project. It protects the R4, R4B, and
R5 consumer contracts; it is not an SDK snapshot recorder or a second FHIR
implementation. Expectations are reviewed C# records and raw synthetic inputs.
Neither the scenario definitions nor `WireContractAssertions` use Firely
parsers, models, or model inspectors.

## Authoring and provenance

Every `CompatibilityScenario` has an ID, an evidence citation, explicit named
tenants/version/configuration, raw seed resources, and ordered requests with
status, body, header, and generated-value expectations. Seeds use the documented
default lenient PUT-as-create behavior so a known logical ID can exist in two
tenants; ordinary POST still assigns a server ID. A later partial test can use
`CompatibilityReplay.StepAsync` for another finite assertion or `SendAsync` to
inspect the actual boundary response for domain-specific assertions.

Add an inventory row with this format when extending the corpus:

| Case ID / test | Versions / formats / boundaries | Input and expectation source | Deliberate local variability |
|---|---|---|---|
| `smoke-*` / `ReplaySmoke` | R4/R4B/R5; JSON/XML; HTTP/direct | `FhirStoreTests.cs`: `MetadataJson`, `MetadataXml`, `TestPostPutIdSemantics.PostWithoutClientIdAssignsServerId`, `TestPatientCRUD.PatientCRUD`, `PutOnMissingLenientCreatesWith201`; `docs/user/strict-mode.md`. Synthetic Patient languages `en` then `en-US`, Observation referring to the bound Patient URL. | Bind POST id, each resource version/time, metadata date and outcome ids. Exact content otherwise. |
| `mixed-tenants-*` / `MixedTenantMetadataAndIsolation` | All six oak/elm tenants together; JSON/XML; HTTP/direct | `docs/user/tenants.md`, `docs/user/strict-mode.md`, and the CRUD/upsert tests above. Same Patient `shared`, distinct `Oak`/`Elm` names and `en`/`de` languages. Update/delete only oak. | Independently bind metadata and each sibling's resource metadata. Elm's complete content and original bindings must survive oak changes. |
| `fresh-state-*` / `ReplayUsesFreshScenarioState` | R4/R4B/R5; JSON/XML; HTTP/direct; two sequential replays of the same definition | CRUD/upsert/POST tests above; `docs/user/loading-data.md`. Leave both an updated seed and a generated Patient in the first replay. | The second replay starts empty, seeds version 1/original content, and captures new values in different dictionaries. No shared mutable store fixture. |
| `FactoryDefaultsAreIsolatedAndKeepR4Identity`, `FactoryDisposesOnlyOwnedDirectories` | Host fixture | Existing `ValidateRoutingTests` default tenant and `FhirStoreManager.Init` loading behavior. | Owned empty source/cache directories; controlled caller-owned inputs are never deleted. |
| `WireContractAssertionsTests` | Pure BCL JSON/XML assertions; no store or host | Hand-authored equal and deliberately unequal examples, not values obtained from the SDK. | Only the comparison policy below; each binding defect is deliberately exercised. |

The capability literals are **4.0.1**, **4.3.0**, and **5.0.0**, not values taken
from an SDK model or from the response being checked. The documented tenant URL
is `CapabilityStatement.url`, equal to
`{tenant-base}/CapabilityStatement/metadata`; `implementation.url` is the
application's project URL, not the tenant endpoint. The capability assertion
checks selected documented structural fields rather than snapshotting all
generated conformance definitions. OperationOutcome assertions pin issue
counts, severity, code, generated-id syntax, and non-empty diagnostics; exact
diagnostic wording is required only when that case declares it. These are local
structural expectations, not a global exemption for metadata or outcomes.

Do not approve new expectations by capturing a passing SDK 6 run. Cite an
existing executable test, documented contract, or reviewed synthetic input.
Unresolved conflicts require scope review. Future raw fixtures go under
`r4/`, `r4b/`, or `r5/` and are loaded through `CompatibilityReplay.LoadFixture`;
the existing project copies the corpus recursively. Phase 2's deliberately
small synthetic inputs are inline in the foundation tests.

## Exchange inventory (phase 3)

The original inputs and exchange assertions were authored before the initial
exchange gate against production code. The summary recovery controls and
semantic replacement described below were authored after its failures, under
the explicitly reviewed 2026-09-22 representation contract. They are synthetic,
not exported patient data, SDK snapshots, or expected-side Firely model round
trips. The `.test` URLs are literal identifiers/references; no case contacts them.

| Case ID / test | Versions / formats / boundaries | Input and expectation source | Deliberate local variability |
|---|---|---|---|
| `ResourceExchangePreservesWireContent` | 12 rows: R4/R4B/R5 × JSON/XML × HTTP/direct; **each row exercises Patient, Observation, and Binary CRUD** | `TestPatientCRUD.PatientCRUD`, `TestPostPutIdSemantics`, and the raw fixtures below. Create/read/update/read/delete/missing-read statuses are 201/200/200/200/200/404. Patient `en` becomes `en-US`; Observation's quantity changes only its last declared decimal digits; Binary's bytes change to a second declared byte sequence. | Capture each server ID and version/time once, compare complete content, reuse on reads/deletes, and require version 1 → 2. Observation.subject and Binary.securityContext use the captured Patient's **absolute tenant URL**, not a stripped ID. |
| `ResourceExchangePreservesWireContentForReturnPreferences` | 12 rows; same version/format/boundary matrix | `FhirController.AddFhirResponse` and `GetResourceInstance`; CRUD oracle above. Update with `return=minimal`, `return=OperationOutcome`, and `return=representation`, and independently read each submitted language. | HTTP selects an empty body, outcome, or resource respectively; direct always asserts both serialized fields. A following read binds metadata for bodyless writes, then the actual write headers must match that binding. Reads ignore an outcome return preference. |
| `ConditionalInteractionsPreserveState` | 24 rows: the 12-row matrix separately for default and strict tenants | `R4TestConditionals`, `R4TestConditionalUpdates`, `R4TestConditionalCreateViaUrlQuery`; `TestPostPutIdSemantics`, `TestStrictModeIdSemantics`, `TestLenientModeIdSemantics`, `TestConditionalControlParameters`; `docs/user/strict-mode.md`. If-None-Exist zero/one/multiple → 201/200/412; conditional update one/multiple → 200/412; default zero-match conditional PUT → 201; conditional delete zero/one/multiple → 404/200/412. | One-match create must return the original **complete content and metadata**, not the new submitted language. All rejected writes/deletes leave both bound resources unchanged. Matching URL/body IDs and If-Match advance ETags; URL/body disagreement remains 422. Strict rejects client-ID POST (400), empty-ID PUT (422), and missing instance PUT (404); default reassigns/stamps/creates. Strict zero-match conditional **create** is tested; no new strict conditional-upsert policy is invented. |
| Conditional header/control cases in the preceding test | All 24 policy rows; URL-conditional POST additionally in the 6 default/direct rows | Existing conditional tests and `DoInstanceCreate`'s explicit comment distinguishing REST If-None-Exist from direct URL dispatch. | Header criteria win over conflicting URL criteria. Control-only `_format`/`_pretty` and If-None-Exist are not criteria: create is new, criteria-less type delete is rejected. `SupportNotChanged=true` is explicit: matching If-None-Match returns 304, empty HTTP body, and the existing direct false/serialized-outcome result. Stale If-Match on a read remains 412. |
| `ConditionalInteractionsPreserveStateForExplicitCreate` | 12 direct-only rows: R4/R4B/R5 × JSON/XML × default/strict | `TestDuplicateExplicitIdCreate.DuplicateExplicitIdCreateReturnsConflict` and the existing `IFhirStore.InstanceCreate(..., forceAllowExistingId: true)` ingest boundary. | The override is not an HTTP feature. A duplicate is 409/`duplicate`, not an overwrite, in either tenant policy; a subsequent normal `PerformInteraction` read must match the original content/version/time. The fresh aliased store and bindings are disposed locally. |
| `BundleEntriesPreserveOutcomes` | 24 rows: 12-row matrix × batch/transaction | `R4TestBundleConditionals`; `TestPostPutIdSemantics.BundleTransactionPostStillSucceedsAfterM2Flip`; existing `ProcessBatch`/`ProcessTransaction` entry and error shapes. | Assert exact entry order/count/fullUrl/status, complete resources, outcomes, and entry etag/location/lastModified relationships. Batch covers PUT/create, GET, If-None-Exist no-op, PUT/update, missing GET, missing request, DELETE, and a post-delete missing GET. Transaction covers generated POST IDs, URN → `Patient/{bound-id}` reference rewriting, rewritten instance GET, missing GET, conditional POST one-match, conditional PUT zero/one-match, and subsequent reads. Preserve current per-entry failure behavior; do **not** require new atomic rollback or transaction conformance. Bundle/outcome UUIDs are bound locally. |
| `SerializationModesPreserveContract` | 144 rows: 120 common mode rows plus 12 output-fallback rows and 12 boundary-specific rows; equally split HTTP/direct and JSON/XML | `R4TestsSummary.Search`, FHIR `search.html#summary`, the independently inspected Patient/Observation element definitions listed below, `SerializationUtils.TryDeserializeFhir/SerializeFhir`, and `FhirController.getMimeType`. Each of false/true/text/data/count retains its own row and both pretty/compact requests. Each summary row now exercises **both Patient and Observation**, with the explicit request flag, query-only GET, and form-body POST search; non-count rows also check instance reads. | `false` compares complete input; `true` asserts exact type-specific summary fields, including Patient identifier/active/name/gender/month-precision birthDate/managingOrganization; `text` retains identity/meta/narrative and Observation's mandatory status/code, not other data; `data` preserves every submitted non-narrative member, including primitive extensions, references, decimal tokens and date precision; `count` has total 1 and **no entries/resources** for each type. Require the exact native metadata pairs below, exact self URL, and entry identity/mode. Reads retain bound identity/version/time and related headers; direct outcomes remain separate and unfiltered. Pretty changes only formatting. Subsequent ordinary reads and full searches compare complete original content/metadata and bindings, rejecting accumulated tags. |
| `SummaryMetadataOracleChecksExactPairs` | 40 pure BCL rows: 20 hand-authored cases × JSON/XML; no replay/store/host | The explicit 2026-09-22 user-reviewed native pair contract, not captured output or a Firely oracle. Eight positive rows and 32 negative rows; see the control inventory below. | Only the relative order of the two newly generated pairs may vary. Original order/multiplicity/content and exact coding members remain observable. Same-sized wrong/repeated pairs, changed/reordered original tags, and extra coding content must fail. |
| `R4TestsSummary.Search` (legacy suite, semantic replacement) | All 47 original inputs, all five modes; direct R4 JSON type/system/mixed-resource/compartment searches, including empty results | Original `data/r4` Patient/Observation files and the Patient in `Bundle-transaction-cdex-load-payer.json`; FHIR R4 element definitions and search summary rules. Expected identity sets are hand-authored for each existing query, not copied from its result. | Bind the transaction-created Patient ID once by its authored identifier; validate/bind each resource's version/time before filtering. Assert status, total, self-link relationship, exact entry/match counts, duplicate-free identities, and type-specific content projections. Subsequent ordinary search/read results must retain complete input and original metadata; read headers must match those bindings. No response-length ordering or metadata-size allowance remains. |
| Parsing/negotiation cases in the preceding test | R4/R4B/R5; JSON/XML; HTTP/direct, with the real boundary distinctions | The ordinary BACKWARDSCOMPATIBLE parser calls in `SerializationUtils`; SDK 6.2.1's cached `FhirXmlDeserializer.BACKWARDSCOMPATIBLE` API documentation explicitly allows unknown coded values. Input/expected literals are authored in the Exchange partial. | Canonical MIME types, application/json or application/xml, and charset/fhirVersion parameters preserve the whole resource. Direct-only `json`/`xml` and `fhir+json`/`fhir+xml` aliases exercise both input/output aliases. HTTP `_format` MIME values override conflicting Accept, unescaped `+` compatibility is retained, unknown format falls back to Accept, and unknown Accept falls back to JSON. Valid JSON/XML with `text/plain` uses **$convert**, whose HTTP route admits that media type, to exercise parser fallback at both boundaries; ordinary create has a Consumes gate. Unknown direct output format retains the existing reported format while serializing JSON; HTTP instead selects canonical JSON. |
| Tolerated coded input in the preceding test | 12 rows, all versions/formats/boundaries | Reviewed synthetic Patient `gender="future-value"`, the existing ordinary BACKWARDSCOMPATIBLE policy, and its documented unknown-coded-value tolerance. | Assert the unknown code survives create/read exactly, not that it is structurally valid. The invalid-boolean cases below must still fail. This is **not** `$validate`'s operation-specific lenient path and does not authorize global OSTRICH parsing; that operation's structural validation belongs to phase 5. |
| `ConvertPreservesCrossFormatContent` | 36 rows: R4/R4B/R5 × HTTP/direct × JSON→XML/XML→JSON × Patient/Observation/Binary | `OpConvert.DoOperation`'s existing stateless conversion contract; independently authored fixture pairs (Binary XML is a reviewed literal in the owned Exchange partial). | Explicit input IDs and synthetic metadata remain unchanged; conversion adds no persistence metadata/headers and leaves stores empty. Direct resource and `success`/`success` conversion outcome are separate. Expected destination content is never generated from the input with Firely, nor copied from an actual result. |
| `InvalidInputPreservesFailureMapping` | 96 rows: 12-row matrix × eight independent error kinds | `TestResourceWrongLocation`, `TestResourceInvalidElement`, existing parser/interaction failure branches, and the native unknown-type policy explained below. | Malformed syntax and invalid boolean produce 422/`structure`; unknown `NotAResource` sent to Patient produces 422/`invalid` at the existing type/location check. Patient sent to Observation on create/update produces 422/`invalid`; non-Bundle submitted as a Bundle produces 422/`invalid`. Malformed $convert and unrecognized-format non-FHIR $convert input produce 415/`invalid`. Assert empty direct resource, outcome count/severity/code, missing resource headers, and empty post-failure stores. Pin Candle diagnostics; SDK diagnostics are only nonempty after the documented Candle prefix. Existing early parse-error MIME fields are not a new MIME contract. |

There are **360 retained exchange replay theory rows** (120 per version: 58 HTTP
and 62 direct), **40 additional pure summary-metadata controls**, and
`ExchangeTheoryDataIsNonEmptyAndLabeled`. The existing 47 legacy R4 summary rows
are retained, not replaced with a smaller selection. This is the authored
inventory, not an execution/pass count. `TestPatientCRUD.PatientCRUD` also
independently reads the two submitted language literals and the created, read,
updated, and deleted resource language using `System.Text.Json`.

Recovery verification on 2026-09-22 used both unchanged phase-3 commands from
the implementation plan, sanctioned by `AGENTS.md`. The scoped net10.0 Release
application build passed with zero warnings/errors. The complete original
test filter passed **605/605**, with no failures/skips and zero compiler/analyzer
warnings/errors: the retained 565 cases plus all 40 metadata controls (eight
positive, 32 negative). All 47 legacy R4 summary rows and all 144 serialization
rows ran; serialization includes 48 rows per FHIR version, evenly split across
JSON/XML and HTTP/direct, with Patient and Observation exercised in every
summary row. Downstream assertions now execute rather than stopping at the
former metadata check.

Earlier failures remain evidence, not retroactive passes: the prior run was
**565 executed / 526 passed / 39 failed** (36 Exchange metadata failures and
three legacy length failures); recovery attempt 4 stopped before any tests
executed on a missing required context initializer and three xUnit analyzer
warnings. Attempt 5 corrected those new helper findings without suppressions
and passed the complete filter. This records phase-3 Windows/net10.0 evidence
only, not remaining migration, final-suite, browser, or platform acceptance.

### Recovery provenance and native repairs

The unknown-resource-type expectation was corrected from `structure` to
`invalid` during recovery, based on source policy rather than observed wire
output. `TestResourceInvalidElement.ResourceWrongLocation` requires an
unsuccessful create, but does not assert an issue code. The ordinary parser
selection in `SerializationUtils` remains `BACKWARDSCOMPATIBLE` (introduced in
`6f54130`), not `OSTRICH`. SDK 6.2.1's `Hl7.Fhir.Base.xml` documents
`DynamicResource` and ad-hoc mappings for unknown resource types. Read-only
inspection of its native `BaseFhirJsonDeserializer.determineResourceClassMappingFromInstance`
and `BaseFhirXmlDeserializer.determineClassMappingFromInstance` confirms both
construct a named `DynamicResource` mapping when no known resource mapping
exists. Candle's unchanged `DoInstanceCreate` type/location branch (present
since `b5539166`) then rejects `NotAResource` at the Patient endpoint with
422/`invalid`. The case now requires its exact Candle diagnostic,
`Resource type: NotAResource does not match request: Patient`, as well as the
existing empty-resource/no-header/no-persistence assertions. This does not
accept unknown resources into a supported store, relax malformed syntax or
invalid booleans, or assert a new policy for unknown types in other operations.

The whitespace repair retains the SDK's XML sanitization, comment/processing
instruction handling, DTD prohibition, and ordinary/operation-specific parser
selection. SDK 6.2.1's string deserialization overload calls
`SerializationUtil.WrapXmlReader`, which sets `IgnoreWhitespace=true`;
`BaseFhirXmlDeserializer.readXhtml` then reads that already-filtered XML with
`ReadOuterXml`. Passing a whitespace-preserving reader to the native reader
overload prevents loss of the authored `<p> </p>`. The same-format and
independently authored cross-format expectations are unchanged.

The summary failure was a missing call-site argument, not evidence that the
native summary filters should be replaced. `SerializeFhir` already selects
`SerializationFilter.ForSummary/ForText/ForData/ForCount`, but the read/search
response wrappers omitted the flag. Those existing wrappers now pass the
explicit serialization flag or the request's query/form `_summary`, using the
same search-input selection as their `Do*Search` methods. Only the resource
serialization is filtered; the separate OperationOutcome and stored POCO are
not altered. `R4TestsSummary.Search` also exercises URL-only direct type,
system, and compartment searches. No claim is made that this pre-existing
argument omission originated in SDK 6, and no passing run is inferred from
this source inspection.

### Raw fixture content

Each of `r4/`, `r4b/`, and `r5/` contains the following five raw files:

| Files | Reviewed content and exact preservation checks |
|---|---|
| `patient.json`, `patient.xml` | `en`, ordered primary/secondary identifiers, ordered `given` names, a mixed-content XHTML narrative with double spaces, a newline, and a whitespace-only paragraph, `birthDate=1980-02` with a primitive extension, `multipleBirthInteger=2`, a non-summary photo, and literal Practitioner/Organization references. |
| `observation.json`, `observation.xml` | `effectiveDateTime=2024-06-03T10:11:12.3400-05:00`, `valueQuantity.value=1234567890.123400`, ordered derivedFrom references and component choices (`valueString`, `valueBoolean`), and the explicit Patient reference slot. Update changes the quantity token to `1234567890.123450`. |
| `binary.json` | application/octet-stream, the explicit Patient securityContext slot, and `AAECA/7/QUJDCg==`, independently declared as bytes `[0,1,2,3,254,255,65,66,67,10]`. Update uses `CQgHBgUEMjEwCg==`, bytes `[9,8,7,6,5,4,50,49,48,10]`. The XML counterpart is authored inline, not converted from this JSON. |

All three versions deliberately use the common specification subset for these
resource types. Their synthetic tag and identifier suffixes are `r4`, `r4b`,
and `r5`, so loading a different version's fixture is detectable. No clinical
version difference is erased. Only reviewed input-side BCL editing adds a
declared ID, changes Patient.language, substitutes the one reference slot, or
changes the declared decimal/base64 token; all other original content remains
part of the expectation.

### Summary assertions and independent provenance

Summary assertions are finite, domain-specific assertions in the Exchange
partial and the authorized `R4TestsSummary` helpers, not an expansion of the
phase 2 oracle. The native metadata representation is **newly user-reviewed on
2026-09-22**, not a claim that a historical SDK 5 response contained these pairs
or that any arbitrary SDK 6 output is acceptable. Production still uses its
native filters and cloning; nothing suppresses or substitutes their tags.

For `true`, `text`, and `data`, preserve the entire original `meta.tag` sequence
unchanged in content, order, and multiplicity, followed by exactly one of each:

| System | Code | Other coding members |
|---|---|---|
| `http://hl7.org/fhir/v3/ObservationValue` | `SUBSETTED` | None, including no `display` |
| `http://terminology.hl7.org/CodeSystem/v3-ObservationValue` | `SUBSETTED` | None, including no `display` |

Only the relative order of these **two appended codings** is unconstrained.
Equal codes under different systems are not duplicates. An unrelated original
system's `SUBSETTED` code and legitimate original duplicate tags remain intact;
they are not removed or counted as native markers. Missing/wrong/repeated native
pairs, extra tags, primitive extensions/duplicate members on generated codings,
and added native displays are rejected. This is not merely a `tag.Length + 2`
check and is not a global metadata/array ignore rule. Included contained
resources in the legacy `data` case have the same exact pair contract, but do
not acquire a stored resource's version/time. No extra tags or other content
exception applies to `false`, subsequent ordinary reads/full searches, CRUD,
or conversion.

The pure control inventory is hand-authored separately in JSON and XML:

- Four positive cases per format: exact pair, reversed generated-pair order,
  unrelated-system original `SUBSETTED`, and original duplicates (including an
  original display).
- Sixteen negative cases per format: both markers missing; legacy missing;
  current missing; wrong system; wrong code; legacy repeated; current repeated;
  entire pair repeated; extra tag; extra native coding member; changed original;
  dropped original; reordered originals; added native display; extra primitive
  member/extension; and duplicate native `code` member.
- Control syntax is parsed before the negative assertion. Failures must come
  from the semantic assertion, not malformed JSON/XML. Several negatives have
  exactly the permitted number of tags.

Mode projections were authored from FHIR's `search.html#summary` and the
official core packages' `StructureDefinition` snapshots: R4 **4.0.1**, R4B
**4.3.0**, and R5 **5.0.0** Patient/Observation definitions (`isSummary` and
top-level minimum cardinalities), plus R4 Identifier, HumanName, ContactPoint,
Address, Reference, Coding, CodeableConcept, Quantity, Period, Element, and the
Patient.link/Observation.component definitions. The corresponding published
references are `https://hl7.org/fhir/R4/patient-definitions.html`,
`https://hl7.org/fhir/R4/observation-definitions.html`, and their R4B/R5
counterparts. Cached official JSON definitions were read during authoring;
tests do **not** depend on a local core-package cache or query an SDK inspector.

For the controlled Patient, `true` preserves identifier/active/name/gender,
the exact `1980-02` birthDate value, and managingOrganization, while excluding
text/language/photo/multipleBirthInteger/generalPractitioner and the
non-summary birthDate extension. `text` retains narrative and identity/meta;
Observation additionally requires status and code even when it has no
narrative. `data` retains all submitted non-narrative content, including the
Patient primitive extension and Observation quantity/date/reference/ordered
component content. The legacy true-mode helper also applies independently
listed datatype and component summary fields; it must not retain non-summary
Coding extensions or Observation.component dataAbsentReason/interpretation
merely because their parent component is a summary element.

Legacy expected content is read from the original checked-in inputs with BCL
JSON only. An initial ordinary search supplies **only** bound generated
IDs/version/timestamps; its full content is checked against those inputs before
any summary is requested. The original fixture is initialized before each
test's binding step, so its timestamps are validated and reused, not treated
as newly created per query. Only unspecified search membership order is a set;
all resource arrays, narrative text, decimal tokens, choice names and references
remain exact. Full reads/searches after summaries reject stored mutation or
tag accumulation. No native output is recorded as an expected snapshot.

## Comparison and binding policy

- JSON object property order and insignificant FHIR XML formatting may differ.
  XML expanded names and attribute values are compared independently of prefix
  spelling/attribute order. Duplicate JSON property names are rejected.
- Arrays and repeated XML elements remain ordered. Numbers are compared as raw
  JSON tokens, never via `double` or `decimal`; XML primitive values remain
  exact strings. Scale, exponent spelling, and arbitrary precision are retained.
- Missing, null, empty, and present are distinct. Primitive extensions, choice
  element names, reference strings, additional metadata, IDs, and tenant
  prefixes are not dropped or renamed.
- XML is loaded with whitespace preservation and DTD processing prohibited.
  Only XML whitespace between children of FHIR elements can be formatting.
  XHTML text, mixed content, whitespace-only narrative nodes, `xml:space`, and
  non-XML whitespace remain significant. JSON narrative strings are exact.
- A `GeneratedResourceExpectation` names a capture or a reuse. Capture validates
  FHIR id syntax, the store's positive numeric version, and a timezone-qualified
  timestamp within the replay lifetime. Updates retain identity and tenant,
  advance the version by one, and cannot move time backwards. Reuse checks the
  entire original binding, including exact timestamp text.
- The expected resource is augmented only with its explicitly declared id,
  `meta.versionId`, and `meta.lastUpdated`. Existing expected metadata is
  preserved and compared; conflicting explicit values fail. Nothing is
  globally stripped from the actual payload.
- Resource header assertions relate the actual weak `ETag` to the bound
  version, actual absolute `Location` to the configured tenant/type/id, and
  actual `Last-Modified` to `meta.lastUpdated` at whole-second HTTP-date
  precision. They do not demand false subsecond equality.
- Direct replay calls the aliased versioned `IFhirStore.PerformInteraction`
  with `serializeReturn: true` and separately checks `SerializedResource`,
  `SerializedOutcome`, success, and the explicitly expected response fields.
  HTTP checks the actual selected body and received response/content headers.
  No direct field or missing header is manufactured on the HTTP side.

The named negative tests reject dropped primitive extensions, changed HTTP
status, wrong-tenant references, broken generated-value relationships, reordered
arrays, lost number precision, and missing/added elements. Positive formatting
and HTTP-date-precision tests prevent an oracle that merely rejects everything.
Additional checks cover choice names, undeclared metadata, duplicate members,
and separation of HTTP versus direct resource/outcome observations.

## Isolation and execution

Each replay constructs and disposes its own stores or application, bindings,
and factory-owned directories. Before *any* seed, both Patient and Observation
searches must be exactly empty for every declared tenant; built-in search and
conformance definitions are not presumed absent. Default factories retain
tenant `r4` for the older controller tests. They supply existing empty owned
`SourceDirectory` and `FhirCacheDirectory`, no official/additional registries or
package requests, and null `ReferenceImplementation`. Explicit loading tests
may supply controlled configuration; the factory copies tenant settings and
never deletes caller-owned paths.

The factory uses the unchanged `Program.BuildAppForTesting` and exposes that
app's services, not process-static singleton access. It stops the app, explicitly
disposes its stores (the current manager does not), disposes the app, and only
then deletes its owned root. It does not enable notification or Blazor hosting.
Host-owning tests, including `ValidateRoutingTests`, join the non-parallel
`CompatibilityHostCollection`; pure comparer tests do not. Serialization does
not replace resource ownership or teardown.

Use the commands documented in `AGENTS.md` and the phase plan. The foundation
gate runs the focused comparer/contract/controller filter twice, unchanged, to
detect disposal and state dependence. These tests do not certify browser
workflows, package/network integrations, or unexecuted framework/OS matrix cells.
