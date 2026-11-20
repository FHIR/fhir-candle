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
| `BundleEntriesPreserveOutcomes` | 24 rows: 12-row matrix × batch/transaction | `R4TestBundleConditionals`; `TestPostPutIdSemantics.BundleTransactionPostStillSucceedsAfterM2Flip`; existing `ProcessBatch`/`ProcessTransaction` entry and error shapes. | Assert exact entry order/count/fullUrl/status, complete resources, outcomes, and entry etag/location/lastModified relationships. Batch covers independent, ordered PUT/create, GET, If-None-Exist no-op, PUT/update, missing GET, missing request, DELETE, and post-delete missing GET outcomes. Transaction characterizes non-atomic execution: an authorized missing GET returns 404 after two creates and a remapped GET; independent reads find both writes and the Observation reference rewritten to `Patient/{bound-id}`. Conditional POST one-match and conditional PUT zero/one-match still execute against live state. This is not a transaction conformance claim. Bundle/outcome UUIDs are bound locally. |
| `TransactionAdmissionWire` | 6 rows: R4/R4B/R5 × JSON/XML, HTTP only | Served CapabilityStatement and the required-body rule in `VersionedFhirStore.DoProcessBundle`; matching-ID Patient PUTs and a late bodyless PUT authored inline. | `metadata` keeps the ordered `batch`, `search-system`, `transaction` codes and documents non-atomic execution on `transaction` only. The late bodyless PUT produces an ordered `200` `transaction-response` with outcome-only `424`/`400` entries and no writes. Independent reads retain the seeded boolean, version, and ETag and find the bodyless target absent. The exact skipped PUT succeeds when sent alone, and a read confirms the new boolean and version. |
| `SerializationModesPreserveContract` | 144 rows: 120 common mode rows plus 12 output-fallback rows and 12 boundary-specific rows; equally split HTTP/direct and JSON/XML | `R4TestsSummary.Search`, FHIR `search.html#summary`, the independently inspected Patient/Observation element definitions listed below, `SerializationUtils.TryDeserializeFhir/SerializeFhir`, and `FhirController.getMimeType`. Each of false/true/text/data/count retains its own row and both pretty/compact requests. Each summary row now exercises **both Patient and Observation**, with the explicit request flag, query-only GET, and form-body POST search; non-count rows also check instance reads. | `false` compares complete input; `true` asserts exact type-specific summary fields, including Patient identifier/active/name/gender/month-precision birthDate/managingOrganization; `text` retains identity/meta/narrative and Observation's mandatory status/code, not other data; `data` preserves every submitted non-narrative member, including primitive extensions, references, decimal tokens and date precision; `count` has total 1 and **no entries/resources** for each type. Require the exact native metadata pairs below, exact self URL, and entry identity/mode. Reads retain bound identity/version/time and related headers; direct outcomes remain separate and unfiltered. Pretty changes only formatting. Subsequent ordinary reads and full searches compare complete original content/metadata and bindings, rejecting accumulated tags. |
| `SummaryMetadataOracleChecksExactPairs` | 40 pure BCL rows: 20 hand-authored cases × JSON/XML; no replay/store/host | The explicit 2026-09-22 user-reviewed native pair contract, not captured output or a Firely oracle. Eight positive rows and 32 negative rows; see the control inventory below. | Only the relative order of the two newly generated pairs may vary. Original order/multiplicity/content and exact coding members remain observable. Same-sized wrong/repeated pairs, changed/reordered original tags, and extra coding content must fail. |
| `R4TestsSummary.Search` (legacy suite, semantic replacement) | All 47 original inputs, all five modes; direct R4 JSON type/system/mixed-resource/compartment searches, including empty results | Original `data/r4` Patient/Observation files and the Patient in `Bundle-transaction-cdex-load-payer.json`; FHIR R4 element definitions and search summary rules. Expected identity sets are hand-authored for each existing query, not copied from its result. | Bind the transaction-created Patient ID once by its authored identifier; validate/bind each resource's version/time before filtering. Assert status, total, self-link relationship, exact entry/match counts, duplicate-free identities, and type-specific content projections. Subsequent ordinary search/read results must retain complete input and original metadata; read headers must match those bindings. No response-length ordering or metadata-size allowance remains. |
| Parsing/negotiation cases in the preceding test | R4/R4B/R5; JSON/XML; HTTP/direct, with the real boundary distinctions | The ordinary BACKWARDSCOMPATIBLE parser calls in `SerializationUtils`; SDK 6.2.1's cached `FhirXmlDeserializer.BACKWARDSCOMPATIBLE` API documentation explicitly allows unknown coded values. Input/expected literals are authored in the Exchange partial. | Canonical MIME types, application/json or application/xml, and charset/fhirVersion parameters preserve the whole resource. Direct-only `json`/`xml` and `fhir+json`/`fhir+xml` aliases exercise both input/output aliases. HTTP `_format` MIME values override conflicting Accept, unescaped `+` compatibility is retained, unknown format falls back to Accept, and unknown Accept falls back to JSON. Valid JSON/XML with `text/plain` uses **$convert**, whose HTTP route admits that media type, to exercise parser fallback at both boundaries; ordinary create has a Consumes gate. Unknown direct output format retains the existing reported format while serializing JSON; HTTP instead selects canonical JSON. |
| Tolerated coded input in the preceding test | 12 rows, all versions/formats/boundaries | Reviewed synthetic Patient `gender="future-value"`, the existing ordinary BACKWARDSCOMPATIBLE policy, and its documented unknown-coded-value tolerance. | Assert the unknown code survives create/read exactly, not that it is structurally valid. The invalid-boolean cases below must still fail. This is **not** `$validate`'s operation-specific lenient path and does not authorize global OSTRICH parsing; that operation's structural validation belongs to phase 5. |
| `ConvertPreservesCrossFormatContent` | 36 rows: R4/R4B/R5 × HTTP/direct × JSON→XML/XML→JSON × Patient/Observation/Binary | `OpConvert.DoOperation`'s existing stateless conversion contract; independently authored fixture pairs (Binary XML is a reviewed literal in the owned Exchange partial). | Explicit input IDs and synthetic metadata remain unchanged; conversion adds no persistence metadata/headers and leaves stores empty. Direct resource and `success`/`success` conversion outcome are separate. Expected destination content is never generated from the input with Firely, nor copied from an actual result. |
| `InvalidInputPreservesFailureMapping` | 96 rows: 12-row matrix × eight independent error kinds | `TestResourceWrongLocation`, `TestResourceInvalidElement`, existing parser/interaction failure branches, and the native unknown-type policy explained below. | Malformed syntax and invalid boolean produce 422/`structure`; unknown `NotAResource` sent to Patient produces 422/`invalid` at the existing type/location check. Patient sent to Observation on create/update produces 422/`invalid`; non-Bundle submitted as a Bundle produces 422/`invalid`. Malformed $convert and unrecognized-format non-FHIR $convert input produce 415/`invalid`. Assert empty direct resource, outcome count/severity/code, missing resource headers, and empty post-failure stores. Pin Candle diagnostics; SDK diagnostics are only nonempty after the documented Candle prefix. Existing early parse-error MIME fields are not a new MIME contract. |

There are **360 original retained exchange replay theory rows** (120 per version:
58 HTTP and 62 direct), **six later HTTP transaction admission rows**,
**40 additional pure summary-metadata controls**, and
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

Each of `r4/`, `r4b/`, and `r5/` contains the following five raw exchange fixtures:

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

### M3 PUT rejection inventory (authored 2026-11-19)

This M3 coverage is **authored, pending final qualification on net11.0 and
net10.0**; no new passes are claimed here. All earlier execution records remain
historical. Four 12-row `ExchangeCases` matrices and two 8-row
`ExchangeUpdateTopicCases` matrices add **64 labeled rows per target framework**.
The original **360 retained exchange replay rows** remain unchanged, for
**424 authored replay rows per target framework** before the later six-row
transaction admission replay. The **40 pure summary-metadata controls** and
`ExchangeTheoryDataIsNonEmptyAndLabeled`
inventory fact are separate from that total; the 47 legacy R4 summary rows
also remain unchanged.

Direct rows explicitly call
`store.InstanceUpdate(FhirRequestContext, out FhirResponseContext)` and retain
the Boolean and native response, rather than substituting replay dispatch or
storage-only calls. HTTP rows issue actual hosted PUT requests through
TestServer. Each row uses a fresh host and that host's tenant store, with
public setup/read operations at its selected boundary.

| Test | Versions / formats / boundaries | Input and original response contract | State/control assertions |
|---|---|---|---|
| `UpdateRejectionStaleIfMatchPreservesResponseAndState` | 12: R4/R4B/R5 × JSON/XML × direct/HTTP | POST a Patient, update with ETag 1, then reject fresh changed content using stale ETag 1 against version 2. Preserve 412 and the exact format-specific diagnostic below. | Complete detached before/after reads for every rejection, including each HTTP preference probe. |
| `UpdateRejectionProtectedPatientPreservesResponseAndState` | 12: R4/R4B/R5 × JSON/XML × direct/HTTP | Load a test-owned transaction POST bundle with protection enabled; publicly find the Patient by its unique authored identifier and bind the assigned ID. Preserve 401 and exactly `Resource Patient/<id> is protected and cannot be changed`, not a bare authorization rejection. | The protected Patient's complete content/metadata remain unchanged; dispose the host before deleting only its test-owned input directory. |
| `UpdateRejectionInvalidSubscriptionPreservesResponseAndState` | 12: R4/R4B/R5 × JSON/XML × direct/HTTP | Seed a valid topic and active Subscription; replace with parseable `off` status and non-HTTP criteria (R4/R4B) or topic (R5). Preserve 400 and exactly `Subscription could not be parsed!`, not a parser error. | Accepted resources, descriptors, public status, and Encounter registration remain unchanged, with no rejected-update publication. |
| `UpdateRejectionNativeTopicPreservesResponseAndState` | 8: R4B/R5 × JSON/XML × direct/HTTP | Retain the required URL primitive with a data-absent-reason extension but no value, reaching the native converter. Preserve 400 and exactly `SubscriptionTopic could not be parsed!`. | Accepted topic, dependent Subscription, descriptors, public status, and registration remain unchanged. |
| `UpdateRejectionBasicTopicPreservesResponseAndState` | 8: R4B/R5 × JSON/XML × direct/HTTP | Replace an ordinary Basic with fresh matching-ID content bearing coding system `http://hl7.org/fhir/fhir-types` / code `SubscriptionTopic`. Preserve 400 and exactly `Basic-wrapped SubscriptionTopic could not be parsed!`. | Original Basic and unrelated accepted topic/Subscription content, descriptors, public status, and registration remain unchanged. |
| `UpdateRejectionUnexpectedExceptionRetainsBoundaryBehavior` | 12: R4/R4B/R5 × JSON/XML × direct/HTTP | An `OnInstanceUpdated` callback throws a distinctive `InvalidOperationException`. Direct propagates that same exception; HTTP retains 500, `text/plain`, and exactly `PutResourceInstance <<< caught: <message>`, not a routine rejection outcome. | Assignment commits before the callback throws: GET must show the submitted content and version 2, not rollback. Always unsubscribe the callback. |

The stale diagnostic's native and JSON wire literal is exactly
`"Conditional update query returned a match with version: 2, If-Match: "`
(69 characters, including the final space). Its XML wire literal is exactly
`"Conditional update query returned a match with version: 2, If-Match:"`
(68 characters, no final space). Firely's unchanged XML primitive serializer
calls `String.Trim` before writing the value attribute; this is a narrowly
authored format-specific oracle, not normalization or a serializer repair.
The direct serialized outcome and HTTP default, `return=OperationOutcome`,
and `return=representation` bodies pin these literals independently of the
native issue assertion. Stale HTTP `return=minimal` retains 412 with an empty
body. Preferences add requests within rows, not extra rows.

For the first five theories, direct returns false with the exact status, null
resource, empty serialized resource/identity/header fields, and exactly one
native OperationOutcome issue: `error` / `exception`, the original exact
diagnostic, and no invented details/extensions. Independent JSON/XML outcome
checks retain the generated ID's agreement with the native outcome for that
response and reject `Failed to update resource`. HTTP pins the original
status and canonical negotiated FHIR media type, with no successful resource
representation, ETag, Last-Modified, or Location.

Before each rejected write, detached complete public GET snapshots preserve
all content semantically, ID, full `meta.versionId` / `meta.lastUpdated`, ETag,
and available Last-Modified for comparison after rejection. Subscription and
topic cases additionally copy descriptor scalar/collection values and require
unchanged object identity, revision, accepted status/topic, filters, expiry,
event counts/history, and topic values. Public `$status` and read-only locked
Encounter registration snapshots pin the accepted revision/filters with no
added or replaced registration; rejected writes publish neither subscription
changes nor instance updates.

Both topic matrices exclude R4 by applicability, not by skipping required
rows: R4's Basic-wrapper converter has no corresponding reachable rejection
under the valid-ID/marker setup. Existing R4 topic/lifecycle controls and
earlier H1/H2/M1/M2 coverage are retained.

## Search inventory (phase 4)

`Sdk6ContractTests.Search.cs` and the three `search-resources.json` files were
authored against the existing search tests and contracts **before running the
new cases against the runtime**. Neither membership nor order is recorded from
an SDK result. The JSON files are corpus containers, not FHIR Bundles: each has
the literal FHIR version, 14 raw resources, and seven complete sibling-resource
overrides. `SearchGraphXml` contains separately authored XML counterparts in
the Search partial; it does not convert the JSON with Firely or a generic
JSON-to-XML adapter. Both formats are used for seeds, definition writes, reads,
and search responses.

| Case ID / test | Versions / formats / boundaries | Input and expectation source | Deliberate local variability |
|---|---|---|---|
| `SearchFamiliesReturnExpectedIds` | 96 rows: eight families × R4/R4B/R5 × JSON/XML × HTTP/direct; every query uses GET and form POST | `R4TestsPatient`/`R4BTestsPatient`/`R5TestsPatient` string, gender, boolean, identifier, multiplebirth, birthdate, and reference cases; the three Observation suites' code/value-quantity/composite/date/profile cases; `R5TestsEncounter` reference-identifier/chaining cases; existing `_source` URI declaration in `ParsedSearchParameter._allResourceParameters` and native URI evaluator. | 67 literal probes cover positive/negative/absent values. Expected identities are literal C# arrays. `:missing=true/false` is exercised for applicable ordinary families; the supported composite's missing-component resource must not match, without inventing a composite `:missing` modifier. No unit conversion, quantity precision TODO, or new decimal-conformance policy is asserted. |
| `ReferenceChainsAndIncludesStayTenantLocal` | 72 rows: six groups × the 12-row version/format/boundary matrix; GET and form POST | The Patient suites' `_has:Observation:patient/subject:_id` and `_revinclude` cases, Observation subject chains/`_include`, Encounter subject chains, documented named tenants, and `StoreTerminologyService.VsContains`/`EvalTokenSearch.TestTokenInCodeableConcept`'s existing local `:in` contract. | Each replay hosts oak and elm of the same version. Identical resource IDs have different Patient names, Organization names, and Observation targets; the same ValueSet canonical contains `weight` in oak and `pulse` in elm. Run oak → elm → oak, using the same expressions. Assert forward/typed/multihop/reverse chains, absent targets, include/revinclude identities and modes, deduplication, and full resource content. Unknown ValueSet and absent coding return no matches; no external terminology service is used. |
| `SortReturnsExplicitIdentityOrder` | 168 rows: seven key/tie groups × two directions × the 12-row matrix; GET and form POST | The three Patient/Observation sort suites; the explicitly settled `FhirSortComparer.Compare` missing-value branches, not the legacy contradictory comments. | Birthdate, Observation date/status, and `_lastUpdated`; exact nonempty membership is checked before exact match order. Equal date keys use an explicit opposite-direction `_id` secondary key. Status ties use a date secondary key, including missing dates. `_lastUpdated` order is authored from four sequential ordinary updates, with separately bound versions/timestamps and a strict monotonic-time precondition, never by sorting actual search output. |
| `DynamicSearchParameterAndCompartmentStateIsFresh` | 24 rows: SearchParameter and CompartmentDefinition lifecycle groups × the 12-row matrix; sibling tenant in each replay; GET and form POST searches | `FhirStoreTestsR4`/`R4B` create/capability tests and `FhirStoreTestsR5.SearchParameterCreate/CapabilityCount`; `AuthCompartmentTests.TestCompartmentTypeSearch`, the Patient compartment rows, and existing definition create/update/delete dispatch. | PUT-as-create, PUT with bound If-Match, DELETE, and ordinary reads only. Warm `compat-family` against family, change its expression to given, verify old/new membership and changed capability documentation, delete and require absence/strict rejection, then recreate to reject stale compiled state. Capability count deltas follow the existing tests; the added declaration's name/canonical/type/documentation are authored exact values, not generated expectations. A custom Patient compartment first includes only Observation, then only Encounter; deletion removes that registration (no invented fallback to a built-in definition). Sibling default compartments and definitions remain unaffected. |
| `StrictAndPreferHandlingPreserveOutcomes` | 24 rows: default/strict tenants × the 12-row matrix; GET and form POST | Every unknown/multiple-unknown/malformed/partially-malformed and override case in `StrictSearchHandlingTests`; `docs/user/strict-mode.md`; the documented last-directive-wins rule in `EffectiveSearchHandling`. | Create two idless Patients via POST, so strict setup does not bypass ID/upsert policy. Bind their generated identities once. Exercise tenant default, explicit lenient/strict, and both orders of two handling directives. Strict errors require exact issue count/order, severity, code, offending parameter/value and version-specific `search.html#errors` URL; no direct resource or resource headers. Lenient responses must still contain exactly the Alpha Patient, not merely return 200. A final full search reuses both original content/metadata bindings. |
| `SearchTheoryDataIsNonEmptyAndLabeled` | Pure BCL inventory, no host/store | Authored family/group/version/format/transport lists and raw corpus. | Pins all 384 replay rows and checks unique labels, literal fixture FHIR versions, 14 unique identities, and seven declared sibling overrides. No Firely expected-side parsing/model inspection. |

### Authored search graph and identities

The base graph's insertion order is deliberately not a correct sort order:
Patients are `beta`, `gamma`, `alpha`, `missing`; Observations are `late`, `twin`,
`early`, `missing`. This avoids an accidentally stable, no-op sorter passing.

- Patient `alpha`: Muñoz/Ada, female, active, birthDate `1980-02-03`,
  multipleBirthInteger `2`, identifier `A`, Québec address, Organization/shared.
  `beta`: Baker/Bea, male, inactive, `1990-04-05`, integer `4`, identifier `B`,
  Organization/other. `gamma`: Muñoz/Zoe, female, active, `1980-02-03`, identifier
  `G`, no multiple birth or organization. `missing` has only id/language.
- Observation `early`: amended, `weight`, `10 mg`, Patient/alpha, timestamp
  `2020-01-02T03:04:05Z`, source `/source/a` and profile `/profile/vitals`.
  `twin`: preliminary, `pulse`, `20 mg`, the same Patient and timestamp, no
  source/profile. `late`: final, `weight`, `30 g`, an **absolute configured
  tenant URL** to Patient/beta, `2022-06-07T08:09:10Z`, source `/source/b` and
  profile `/profile/lab`. `missing`: final with a text-only mandatory code,
  and no subject/effective/value/source/profile.
- Organization/shared is Oak Clinic; Organization/other is Cedar Clinic.
  Encounter/visit refers to Patient/alpha and carries identifier `A`, AMB class,
  and a one-hour June 2021 period; Encounter/other-visit refers to Patient/beta,
  carries IMP class, and has no period. The multiplebirth SearchParameter is
  derived from the existing version-specific raw fixtures. The local ValueSet
  includes only the synthetic `weight` code.
- Sibling overrides make Organization/shared Elm Clinic; Patient/alpha's
  family Elm and Patient/beta's family Muñoz, both with `language=de`.
  Observation early/twin instead refer to Patient/beta, while late uses that
  sibling's absolute Patient/alpha URL. Its same-canonical ValueSet includes
  only `pulse`. Complete entry content and generated bindings are checked
  against the appropriate sibling input, not just overlapping IDs.

All canonical systems/URLs include the declared `r4`, `r4b`, or `r5` suffix.
The only JSON template slot is `{{tenant-base}}`, replaced with the configured
tenant base without stripping prefixes. R4/R4B Encounter uses `finished`,
a single Coding `class`, and `period`; R5 uses `completed`, an array of
CodeableConcept `class` values, and `actualPeriod`. R5 SearchParameters include
`processingMode=normal`. These are explicit specification-version differences,
not relaxed comparisons. `_source` actually exercises URI extraction;
`_profile` separately exercises the existing canonical-reference parameter.

The reviewed sort identity orders are:

| Key group | Ascending match order | Descending match order |
|---|---|---|
| Patient birthdate, unique present keys plus missing | missing, alpha, beta | beta, alpha, missing |
| Patient birthdate with opposite-direction `_id` secondary | missing, gamma, alpha, beta | beta, alpha, gamma, missing |
| Observation date, unique present keys plus missing | missing, early, late | late, early, missing |
| Observation date with opposite-direction `_id` secondary | missing, twin, early, late | late, early, twin, missing |
| Observation status, unique keys | early, late, twin | twin, late, early |
| Observation status, date secondary, `_id` tertiary | early, missing, late, twin | twin, late, missing, early |
| Observation `_lastUpdated` after twin → late → missing → early updates | twin, late, missing, early | early, missing, late, twin |

Missing sort values are first ascending and last descending. Status is
mandatory on these version-correct Observations and `meta.lastUpdated` is
generated for every persisted resource, so missing values are exercised on
birthdate/date, including a secondary date key, rather than by seeding invalid
resources or editing store internals. Time values are captured only as permitted
generated bindings, with 25 ms between controlled updates; the expected ID order
above remains literal.

### Search assertion boundaries and execution status

Every successful search checks the searchset type, exact total, exact entry and
match/include counts, no duplicate/unexpected/absent identities, permitted
`search.mode` values, a tenant-specific applied self URL, and a fullUrl equal to
the **entry resource's** tenant/type/id. Each complete same-format entry resource
must equal its authored input plus previously bound generated metadata. Empty
results require no entries. Unsorted membership is a set; sorted match order is
additionally an exact sequence, and no resource array is normalized.

Direct outcomes remain separate from serialized Bundles and have the expected
successful issue semantics; HTTP assertions inspect the actual selected body.
Strict failure outcomes are checked on the actual boundary with no fabricated
HTTP fields. Fully ignored parameters disappear from a lenient self link.
The partially valid date-list case preserves the existing
`ParsedSearchParameter.GetAppliedQueryString` request-literal echo while
requiring only the valid date to affect membership; that is not an assertion
that a malformed value matched.

This is an **authored coverage inventory, not a phase-4 pass claim**. Initial
verification uses the exact phase-4 application build and combined test filter
from the plan/`AGENTS.md`, against unchanged production search code first.
Failures must be reported with evidence before any repair or retry. The
phase-3 execution evidence and every earlier corpus inventory above remain
unchanged; these new cases do not establish later phases or other OS/framework
cells.

### Phase-4 attempt-2 partial recovery evidence (2026-09-22)

The original attempt remains **1,443 executed / 1,353 passed / 90 failed**,
including all 1,058 earlier selected cases passing. The reviewed first-failure
classification is P1 repeated forms **12**, P2 complex presence **30**, P3
inclusion fullUrls **24**, and P4 local references **18**: 84 pre-existing
defect cases, not established SDK6-origin regressions. U1 accounts for the
other six cases: successful compartment searches were incorrectly requested
through the unsupported generic dispatcher. These are six cases with two
diagnoses, not twelve failures.

Attempt 2 retains all 384 original replay rows and their inventory fact.
The three original JSON graphs are byte-identical to recovery intake;
the original family/reference/sort probes, both definition lifecycles,
and the separately authored XML graph are unchanged. Additional controls
use independent hand-authored JSON/XML literals in the Search partial.
The authored additions are:

| Test | Additional rows | Contract guarded |
|---|---:|---|
| `ReverseChainUsesExistentialMatchesAndDistinctFilters` | 144 | Twelve independently labeled predicates across the 12-row matrix: zero/one/multiple candidates, one/multiple targets, distinct names and repeated same keys in both orders, different related resources satisfying separate filters, nested forward-filter identity, and oak/elm/oak revisits. All references in this graph are relative so M1 reproduction does not require P4. |
| `PostSearchPreservesRepeatedParametersAndEncoding` | 192 | Four routes × four groups × the matrix: single/comma/repeated values, encoded values/modifiers/keys, body-before-query repetitions, and empty forms/values. The original string/date probes remain. System searches explicitly declare `_type=Observation`. |
| `MissingSearchDistinguishesComplexValuesAndPrimitiveExtensions` | 84 | Seven presence groups: HumanName, Quantity, Reference, extension-only birthDate, false, zero, and multivalued given names with one extension-only and one populated primitive. |
| `IncludedFullUrlsIdentifyReturnedResources` | 36 | Include/revinclude at the configured root on both boundaries, plus independent HTTP forwarded-root rows, retaining exact content, total, mode, deduplication and identity assertions. |
| `LocalReferenceEquivalenceRejectsForeignBases` | 168 | Fourteen groups covering both local spellings, exact foreign references with local-negative controls, authority/tenant/prefix boundaries, URN/OID/type/canonical preservation, reverse chains, reverse includes, and both compartment routes. |
| `CompartmentReplayUsesExistingMethods` | 12 | The complete original successful compartment definition/type/sibling/create/update/delete lifecycle through the supported boundary. |
| `GenericCompartmentDispatchRemainsUnsupported` | 6 | Direct generic GET/form dispatch for both compartment routes remains unsuccessful/501, with its actual empty serialized fields kept distinct from its outcome object. |
| `SearchRecoveryTheoryDataIsNonEmptyAndLabeled` | 1 fact | Pins the additional 642 replay rows and their labels without replacing the original inventory fact. |

Search helper failures now identify version, format, transport, tenant, probe,
actual method/path/form body, and expected/actual identities before total
assertions. Assertion and wire-parser failures are rethrown with context,
not swallowed. Search probes still use `SendAsync`; they are **not** counted
as recorded `StepAsync` responses. Original content, outcome, ordering, URL,
membership and status assertions remain.

The only completed adapter edit selects the existing `CompartmentSearch` or
`CompartmentTypeSearch` from the parsed interaction **before** invoking it.
All other direct calls still use `PerformInteraction(..., serializeReturn:
true)`; no fallback after 501 or production generic dispatch was added.
This edit has not executed in a test run yet. P1-P4 production code and the
M1 branch/cache remain **unmodified** at this hand-back.

The new POST controls deliberately keep boundary distinctions: direct form
controls provide complete `SourceContent`, because merging a separate URL
query is controller behavior. New system controls preserve the current
one-literal-per-parameter-name self-link projection while requiring every
occurrence to affect membership. Neither decision changes an original
expectation or authorizes a search-parser/self-link repair.

#### Actual diagnostic executions

The command authority is `AGENTS.md`. Only these two diagnostic test commands
ran; neither is an acceptance gate:

```powershell
dotnet test --configuration Release --framework net10.0 --no-restore --verbosity normal --filter "FullyQualifiedName~fhir.candle.Tests.Sdk6ContractTests.ReverseChainUsesExistentialMatchesAndDistinctFilters"
dotnet test --configuration Release --framework net10.0 --no-restore --verbosity normal --filter "FullyQualifiedName~fhir.candle.Tests.Sdk6ContractTests.PostSearchPreservesRepeatedParametersAndEncoding|FullyQualifiedName~fhir.candle.Tests.Sdk6ContractTests.MissingSearchDistinguishesComplexValuesAndPrimitiveExtensions|FullyQualifiedName~fhir.candle.Tests.Sdk6ContractTests.IncludedFullUrlsIdentifyReturnedResources|FullyQualifiedName~fhir.candle.Tests.Sdk6ContractTests.LocalReferenceEquivalenceRejectsForeignBases|FullyQualifiedName~fhir.candle.Tests.Sdk6ContractTests.CompartmentReplayUsesExistingMethods|FullyQualifiedName~fhir.candle.Tests.Sdk6ContractTests.GenericCompartmentDispatchRemainsUnsupported"
```

1. **M1 regression-first:** exit **1**, **144 executed / 48 passed / 96
   failed**, no skips, **0 compiler warnings / 0 compiler errors**. Production
   and the replay adapter were unchanged. The 84 M1 first failures were
   `zero`, `multiple-targets`, `distinct-names`, `same-key`,
   `same-key-reversed`, `nested-filters`, and `nested-filters-reversed`,
   twelve each. Examples: no candidates incorrectly returned all three
   Patients; final-status candidates for two Patients incorrectly returned
   none; incompatible repeated `_id` or nested-family filters returned the
   Patient selected by the first filter. The other twelve first failures
   were the known P1 form-flattening mechanism in the six HTTP
   `independent-related` and six HTTP reversed-order rows. These are new
   guard executions, not a recount of the original 90.
2. **Additional guards before production repair:** exit **1**, **no tests
   executed**, **0 warnings / 2 compiler errors**. The compiler stopped at
   `Sdk6ContractTests.Search.cs:415` with CS8506 (no best type for the switch
   expression) and CS8131 (deconstruction requires a typed right-hand-side).
   The new presence-control deconstruction contains untyped collection
   expressions in its switch arms. This is an introduced test-authoring
   compile blocker, not a newly diagnosed product defect. The 498 selected
   guard rows remain unexecuted. The mandated compile-failure stop was
   honored: no corrective edit or diagnostic retry followed.

Raw evidence is under ignored `scratch/0919-01/`:

- `dev-do-phase4-attempt2-m1-before-20260922-183933.log` records the first
  command/exit but the noninteractive transcript omitted native output.
- `dev-do-phase4-attempt2-m1-before-raw-20260922-184018.log` preserves that
  command's complete tool-retained output via `Get-Content | Out-Host` inside
  a new `Start-Transcript -NoClobber` session. This is an explicitly labeled
  output recovery, **not a rerun**; the recovery command exited 0.
- `dev-do-phase4-attempt2-guards-before-20260922-185023.log` directly captures
  the second command through `Out-Host`, its compile errors and exit 1.

The final exact application build and complete phase-4 test gate were **not
run** because diagnostic compilation blocked this attempt. The original
masked probes, repair controls, and later platform/framework gates are not
claimed to pass. This section records partial work and a hand-back, not
phase completion or a refreshed attempt budget.

### Phase-4 attempt-3 verification evidence (2026-09-22)

This continues the **original final attempt 3**. The attempt-1 failure,
attempt-2 M1 reproduction, and attempt-2 compile failure above remain history.
The first edit supplied explicitly typed arrays to the new presence-control
switch; its literals, selected rows, and assertions did not change. The
unchanged-production additional-guard diagnostic then compiled and executed
**498 cases: 210 passed, 288 failed, none skipped**, with **zero compiler
warnings/errors**. The already recorded M1 baseline was not rerun.

The additional-guard first failures independently reproduced only the
approved mechanisms: 96 HTTP form/encoding/query-merge failures (P1), 60
presence failures (P2), 36 inclusion-fullUrl failures (P3), 84 local-reference
failures (P4), and 12 dedicated-compartment lifecycle failures at the known
P4 sibling probe. All six generic-dispatch 501 controls passed. In particular,
the corrected direct adapter reached the supported lifecycle rather than
falling back after a 501.

#### Bounded implementation

- **P1:** All four existing POST handlers use one private helper.
  `QueryString.Create(Request.Form).Add(Request.QueryString)` encodes each
  parsed key/value occurrence and appends the URL query after the body once.
  Repeated AND, comma OR, encoded delimiters/Unicode/keys, and empty
  values/forms retain their meaning. The non-form stream fallback remains;
  authorization, negotiation, request headers, summary, and response handling
  are not replaced. System self links retain the previously documented
  one-literal-per-name projection; all occurrences still constrain membership.
- **P2:** `SearchTester` decides `:missing` across the extracted values.
  A primitive must have a scalar value (including real false/zero), while a
  populated complex node has children. One extension-only primitive cannot
  make a multivalued parameter missing when another member is populated.
  This is confined to presence testing, not a general value-extraction rewrite.
- **P3:** Both inclusion loops build fullUrls from the included resource's
  type/id and the supplied `rootUrl`. Forwarded roots, content, membership,
  search modes, totals, and deduplication remain independently asserted.
- **P4:** `TestReference` receives the explicit current store's
  `Config.BaseUrl`. The comparison equates `Type/id` with exactly
  `<that-base>/Type/id` in both directions. Foreign authority, sibling tenant,
  base-prefix lookalike, and descendant-path controls remain nonlocal; exact
  foreign absolute matches, typed references, URN/OID, and canonical controls
  remain valid. No suffix-based `Resolve` proof or resolver/versioning redesign
  was introduced. The existing reference-search, reverse-chain, reverse-include,
  and compartment callers obtain this context through their store's tester.
- **M1:** The existing reverse branch requires **any** related match, so an
  empty candidate set is false. Each repeated `_has` still applies separately
  with AND. Its cache key includes the complete requested reverse-chain
  literal, including the nested filter's names/order/values. Cache lifetime
  and store ownership remain unchanged; no `ParsedSearchParameter` or
  `ResourceStore` plumbing change was needed.
- **U1:** The attempt-2 adapter edit is retained and now executed: select
  `CompartmentSearch` / `CompartmentTypeSearch` before dispatch; use ordinary
  `PerformInteraction(..., serializeReturn: true)` for other requests.
  Successful serialized resource/outcome assertions remain separate from
  the explicit unsupported generic-dispatch controls. Production generic
  compartment dispatch was not added.

These are the approved repairs to existing behavior, **not evidence that
SDK 6 caused P1-P4 or M1**. No comparer, factory, scenario model, Exchange
partial, legacy test, compiler ownership, sorting, Unicode evaluator,
terminology, unit-conversion, or unsupported-capability repair was made.

#### Observed regression-before/after matrix

The after column is observed both in the complete Search-partial diagnostic
and in the unchanged cumulative acceptance filter, not inferred from source.

| Added guard | Before production repair (passed / failed) | After (passed / failed) |
|---|---:|---:|
| Reverse-chain existential/filter identity | 48 / 96, attempt 2: 84 M1 and 12 P1 first failures | 144 / 0 |
| Four POST routes, encoding, merging, empty controls | 96 / 96 | 192 / 0 |
| Complex/primitive/multivalued presence | 24 / 60 | 84 / 0 |
| Included identity and configured/forwarded roots | 0 / 36 | 36 / 0 |
| Local/foreign reference boundaries and consumers | 84 / 84 | 168 / 0 |
| Dedicated compartment lifecycle | 0 / 12, stopped at P4 | 12 / 0 |
| Generic compartment dispatch remains 501 | 6 / 0 | 6 / 0 |
| Additional inventory fact | Not selected by the before diagnostics | 1 / 0 |

All originally masked probes now execute successfully: quantity/reference
POSTs and later profile/identifier checks; HTTP string address/presence and
date/Period checks; included content, sibling and oak-revisit checks;
absent/no-target reverse chains; and complete compartment create/update/delete,
type, sibling, and final ordinary-search isolation checks on both boundaries.
The 168 original sort rows, strict/Prefer cases, SearchParameter lifecycle,
terminology cases, and selected legacy accent/exact cases remain in the gates.
Search probes still use `SendAsync`; they are not relabeled as recorded
`StepAsync` responses.

The Search diagnostic executed **1,028 / 1,028 passing cases**. The exact final
cumulative gate executed **2,086 / 2,086 passing cases**, with **no skips and
zero compiler warnings/errors**. Its result-header multiset retains all
**1,443 original cases**, including the original 90 failures, with identical
multiplicity: **1,058 earlier cases + 384 original search rows + one original
inventory fact**. The difference is exactly **642 additional rows + one
additional inventory fact**, not a reduced or substituted original selection.
For each FHIR version, the added theory matrix has 53 Direct/JSON, 53
Direct/XML, 54 HTTP/JSON, and 54 HTTP/XML rows; the forwarded-root and
generic-dispatch boundary differences explain those counts.

The three original JSON graphs retain their intake SHA256 values:

| Graph | SHA256 |
|---|---|
| R4 | `4FD0662932314FDAE4B52CA136E3D0CB80EFE4CC73A92A233338252C2D28F59D` |
| R4B | `F5ACE1DCD6FFE244A1C3E1E4B9CF4AE80166261BF7A624BDA057C5E9D5B4AC2F` |
| R5 | `C8F6DF8FB99439A22FF6554E6B742E42F29F9AEEEB7959D0076BBFC00DBA7BA5` |

No original absolute reference, Unicode value, XML graph literal, probe,
expected identity, or assertion was changed. No additional cases beyond the
attempt-2 inventory were introduced in attempt 3.

#### Actual commands and retained raw evidence

`AGENTS.md` is the command authority. These are the four actual build/test
executions, in order; the first and third are supplementary diagnostics:

```powershell
dotnet test --configuration Release --framework net10.0 --no-restore --verbosity normal --filter "FullyQualifiedName~fhir.candle.Tests.Sdk6ContractTests.PostSearchPreservesRepeatedParametersAndEncoding|FullyQualifiedName~fhir.candle.Tests.Sdk6ContractTests.MissingSearchDistinguishesComplexValuesAndPrimitiveExtensions|FullyQualifiedName~fhir.candle.Tests.Sdk6ContractTests.IncludedFullUrlsIdentifyReturnedResources|FullyQualifiedName~fhir.candle.Tests.Sdk6ContractTests.LocalReferenceEquivalenceRejectsForeignBases|FullyQualifiedName~fhir.candle.Tests.Sdk6ContractTests.CompartmentReplayUsesExistingMethods|FullyQualifiedName~fhir.candle.Tests.Sdk6ContractTests.GenericCompartmentDispatchRemainsUnsupported"
dotnet build src\fhir-candle\fhir-candle.csproj --configuration Release --framework net10.0
dotnet test --configuration Release --framework net10.0 --no-restore --verbosity normal --filter "FullyQualifiedName~fhir.candle.Tests.Sdk6ContractTests.SearchFamiliesReturnExpectedIds|FullyQualifiedName~fhir.candle.Tests.Sdk6ContractTests.ReferenceChainsAndIncludesStayTenantLocal|FullyQualifiedName~fhir.candle.Tests.Sdk6ContractTests.SortReturnsExplicitIdentityOrder|FullyQualifiedName~fhir.candle.Tests.Sdk6ContractTests.DynamicSearchParameterAndCompartmentStateIsFresh|FullyQualifiedName~fhir.candle.Tests.Sdk6ContractTests.StrictAndPreferHandlingPreserveOutcomes|FullyQualifiedName~fhir.candle.Tests.Sdk6ContractTests.SearchTheoryDataIsNonEmptyAndLabeled|FullyQualifiedName~fhir.candle.Tests.Sdk6ContractTests.ReverseChainUsesExistentialMatchesAndDistinctFilters|FullyQualifiedName~fhir.candle.Tests.Sdk6ContractTests.PostSearchPreservesRepeatedParametersAndEncoding|FullyQualifiedName~fhir.candle.Tests.Sdk6ContractTests.MissingSearchDistinguishesComplexValuesAndPrimitiveExtensions|FullyQualifiedName~fhir.candle.Tests.Sdk6ContractTests.IncludedFullUrlsIdentifyReturnedResources|FullyQualifiedName~fhir.candle.Tests.Sdk6ContractTests.LocalReferenceEquivalenceRejectsForeignBases|FullyQualifiedName~fhir.candle.Tests.Sdk6ContractTests.CompartmentReplayUsesExistingMethods|FullyQualifiedName~fhir.candle.Tests.Sdk6ContractTests.GenericCompartmentDispatchRemainsUnsupported|FullyQualifiedName~fhir.candle.Tests.Sdk6ContractTests.SearchRecoveryTheoryDataIsNonEmptyAndLabeled"
dotnet test --configuration Release --framework net10.0 --no-restore --verbosity normal --filter "FullyQualifiedName~fhir.candle.Tests.Sdk6ContractTests|FullyQualifiedName~fhir.candle.Tests.R4TestsPatient|FullyQualifiedName~fhir.candle.Tests.R4TestsObservation|FullyQualifiedName~fhir.candle.Tests.R4BTestsPatient|FullyQualifiedName~fhir.candle.Tests.R4BTestsObservation|FullyQualifiedName~fhir.candle.Tests.R5TestsPatient|FullyQualifiedName~fhir.candle.Tests.R5TestsObservation|FullyQualifiedName~fhir.candle.Tests.R5TestsEncounter|FullyQualifiedName~fhir.candle.Tests.StrictSearchHandlingTests|FullyQualifiedName~fhir.candle.Tests.AuthCompartmentTests|FullyQualifiedName~fhir.candle.Tests.FhirStoreTestsR4|FullyQualifiedName~fhir.candle.Tests.FhirStoreTestsR5"
```

All logs below are under ignored `scratch/0919-01/`, captured with
`Start-Transcript -NoClobber` and native output piped through `Out-Host`;
the native exit was captured immediately, printed, and returned after
`Stop-Transcript`.

| Raw log | Exit | Actual result |
|---|---:|---|
| `dev-do-phase4-attempt3-guards-before-20260922-185811.log` | 1 | 498 executed, 210 passed, 288 approved-mechanism failures; no skips; 0 warnings/errors |
| `dev-do-phase4-attempt3-build-20260922-190815.log` | 0 | Exact application build; 0 warnings/errors |
| `dev-do-phase4-attempt3-search-after-20260922-190951.log` | 0 | 1,028 executed/passed; no skips; 0 warnings/errors |
| `dev-do-phase4-attempt3-final-test-20260922-191457.log` | 0 | Exact cumulative filter; 2,086 executed/passed; no skips; 0 warnings/errors |
| `dev-do-phase4-attempt3-result-audit-20260922-192104.log` | 1 | Read-only result/hash inspection; graph hashes and retention counts agreed, but an extra display-name-uniqueness assumption failed |
| `dev-do-phase4-attempt3-multiplicity-audit-20260922-192326.log` | 0 | Corrected read-only multiset inspection: all 1,443 original multiplicities retained; exactly 643 additions |

The audit failure is retained, not hidden or counted as a test failure:
xUnit truncates two legacy R4 `identifier:of-type` case display names to the
same text. Their original and final multiplicities are both **two**.
The corrected inspection compares those multiplicities instead of requiring
unique rendered names; all added case display identities are distinct.
It made no product/test edit and reran no test command.

Only this README evidence section was added after the successful final test.
The implementation and test sources are unchanged since the exact build and
gate; the integrating runner must repeat the exact gate after this documentation
update before staging. This records Windows/net10.0 R4/R4B/R5 execution, not
later phases, other target frameworks, browser checks, or other OS cells.
No phase status, staging, or commit is claimed here.

## Operations and subscriptions inventory (phase 5)

These inputs are synthetic local-only resources, not SDK-generated snapshots
or copies of real patient data. XML expectations are independently authored
in the two domain partials; no expected-side Firely parser or converter is
used. The original foundation, Exchange, Search, and legacy subscription
tests are unchanged.

| Test / matrix | Authored rows and boundaries | Independent provenance and contract |
|---|---|---|
| `ValidatePreservesTargetAndIssueContract` | 468: 13 targets × system/type/instance × R4/R4B/R5 × JSON/XML × HTTP/direct | `TestValidateOperation` H1/H2/M1/L4 and `docs/user/operations/validate.md`. Bare/wrapped valid targets, missing Observation.code, invalid boolean, invalid FHIR XHTML, ignored mode/profile, no body, no-target Parameters, malformed syntax. Valid/invalid parseable targets are 200; no target is 422; malformed syntax is pre-operation 415. Valid stored Patient focus is unchanged when an invalid Observation body takes precedence. |
| `OperationStoreAccessorPreservesOwnership` | 12: all three versions/formats/boundaries, two same-version tenants each | Existing replay/factory ownership. Actual store sees the boundary's Patient write; sibling does not. Unknown/case-mismatched tenants and disposed access throw. No process-static manager, replacement store, or HTTP bypass. |
| `OperationDiscoveryIsVersionAndPackageCorrect` | 24: absent/PAS package × full 12-row matrix | `IFhirOperation` declarations, `GetDefinition`, `CheckLoadedOperations`, and `docs/technical/operations-and-hooks.md`. Exact eight common operation identities/canonicals, generated IDs, implementation versions/levels/state/resource restrictions, Op class naming and GET/POST declarations. R4-only `$submit`/`$inquire` additionally require PAS and use the supplied literal definitions below, not generated IDs; R4B/R5 cannot acquire them by loading its package ID. Definitions, dispatch, and capability advertisements are separately checked on the same store, with metadata read before loading and again afterward. |
| `OperationRoutesPreserveSupportedBoundaries` | 204: 17 independently labeled route/method probes × full matrix | Existing operation level/resource restrictions and the documented POST-only `$validate` contract. Supported system `$convert`, GET/POST Subscription `$status`; wrong levels/resources, missing instances, invalid hook input, unknown operation, PATCH operation boundary, and GET `$validate` rejection. All twelve original GET rows now require the approved 405/error/not-supported diagnostic, no direct resource, and exactly HTTP `Allow: POST`, followed by an unchanged focus read. HTTP's unsupported PATCH is still 405/empty with GET/POST Allow; the unsupported generic direct dispatch remains 501 without invented serialized fields. |
| `InteractionHookRequiresPackageAndEligibleTask` | 72: six independent controls × full matrix | The actual R4 `CDexTaskProcess`/`IFhirInteractionHook`, not `$subscription-hook`. Package absent, eligible PUT, eligible POST, wrong status, wrong intent, missing query; R4B/R5 never acquire the R4 hook. Completed Task must contain one result Bundle with the exact synthetic Patient and a matching output reference. Both declared and actual loaded hook identities are checked. |
| `ParametersBodiesUseProperOperationRoutes` in `ValidateRoutingTests` | 24 HTTP rows: three versions × two formats × four routes | Existing executable `/Parameters/$validate` 404 and documented system/type/instance body unwrapping. Eighteen proper-route rows require 200/information/informational/All OK; six Parameters-URL rows retain 404. The original two tests remain; only their stale class summary is corrected. |
| `SubscriptionConversionAndFiltersPreserveEvents` | 312: 26 independent probes × full matrix | `R4TestSubscriptions`, `R4BTestSubscriptions`, `R5TestSubscriptions`, and `FhirStoreTestsR5`. Query, FHIRPath `%previous`/`%current`, and interaction-only topics; positive/negative patient filters; positive/negative create/update/delete and already-terminal update. Actual topic/subscription create/read content and parsed filter/channel/status shapes are asserted. Each scenario proves both directions of same-version tenant isolation. |
| `SubscriptionNotificationContentPreservesReferences` | 108: handshake/heartbeat/event × empty/id-only/full-resource × full matrix | Existing `SerializeSubscriptionEvents`, converters, legacy handshake tests, and the store event contract. Fresh handshake/heartbeat have zero events; event scenarios capture two real events. BCL oracle binds event numbers/timestamps, exact tenant subscription/focus/additional-context references, status UUID/fullUrl relationships, and complete resource content. GET/POST `$status`, negative status filter, and GET/POST `$events` selecting only event 2 are checked. Independently authored inbound notification goes through `$subscription-hook` and must populate only the receiver's actual received state and Bundle store. |
| `SubscriptionNotificationOracleRejectsChangedReferences` | 36 pure BCL controls: six cases × three versions × two formats | Hand-authored inbound status literals: six positives and thirty negatives reject changed subscription tenant, focus tenant, additional context, event number, or total. No store, SDK parser, or captured expected output. |
| `OperationTheoryDataIsNonEmptyAndLabeled`, `SubscriptionTheoryDataIsNonEmptyAndVersionCorrect` | Two inventory facts | Explicit nonzero row counts/unique labels; version-specific topic and Encounter shapes, local-only endpoints. |

Original phase-5 authored additions total **1,262 tests**: 1,200 replay theory rows (600
HTTP/600 direct), 36 pure BCL oracle rows, two inventory facts, and 24
controller routing rows. These are authored counts, **not a final gate
pass count**. A failure within a scenario can still mask its later checks;
runner output and the implementation hand-back must identify those paths.

### Bounded O1/O2 recovery additions

O1 is a **pre-existing POST-only contract defect**, not an SDK6-origin
regression. The initial new tests assumed 405; the recovery's exact
405/error/not-supported outcome and HTTP `Allow: POST` are now explicitly
approved. The exact diagnostic is
`$validate only supports POST; GET is not allowed.` This is validate-only
enforcement, not generic enforcement of operation declarations.

O2 is a **new fixture/oracle assumption error**, not a production PAS defect.
The PAS implementations deliberately return null from `GetDefinition`.
The corrected R4 package supplies independently authored definitions through
normal `LoadPackage`; built-in generated-definition expectations stay
separate. Neither PAS implementation is changed.

| Added test | Authored rows and boundaries | Independent contract |
|---|---|---|
| `ValidateRejectsGetAtEveryOperationLevel` | 90: 36 bodyless rows across all versions/formats/boundaries/levels, plus 18 direct raw-body, 18 direct malformed-body, and 18 direct non-FHIR-object rows | Exact 405, one error/not-supported issue with the approved diagnostic, false direct success, empty serialized resource and identity/header fields, and HTTP Allow exactly POST. The object rows use complete `FhirRequestContext` values (including Authorization) with `SourceObject = new object()` through each existing system/type/instance wrapper and additionally assert null Resource. Rejection precedes both parsing branches; focus content/version/time survive. |
| `ValidateMethodRejectionPreservesEarlierGuards` | 156: 120 common rows (ten probes × full matrix), plus 36 HTTP-only tenant/auth rows | Missing instance GET with/without a body and POST with a body; unsupported Parameters/unknown type at type and instance levels; unknown operations at all three levels. Unsupported-type HTTP errors remain 404/plain text; generic direct dispatch is still 501 without serialized output, while explicit operation wrappers retain their 404/error/not-supported resource guard even with a non-FHIR object. Unknown operation and missing-instance diagnostics remain exact. Real existing factory/SMART configuration checks missing tenants and authorization at all three levels; no auth service replacement or invented direct tenant routing. None adds Allow. |
| `ValidateGetPreferencesPreserveState` | 72 HTTP rows: all versions/formats/levels × default/representation/OperationOutcome/minimal | Normal negotiated JSON/XML outcome selection, or the existing minimal empty body, always with exactly Allow POST and no resource headers. A subsequent ordinary read reuses the complete content and original ID/version/timestamp/header bindings. |
| `OtherOperationVerbBehaviorIsUnchanged` | 44: 12 system hook GET, 24 Subscription status GET/POST, eight R4 PAS type GET rows | No operation discovery assertions precede these dispatch probes. Bodyless `$subscription-hook` remains 422/error/exception with exact `Posted content is not a valid Subscription notification bundle`, no received notification and no stored Bundle. `$status` GET/POST remain supported. Independently loaded R4 PAS `Claim/$submit` and `Claim/$inquire` GET each retain 422/fatal/structure with exact `PAS Claim Submit requires a PASRequestBundle as input.` / `PAS Claim Inquiry requires a PASClaimInquiryBundle as input.`, and no HTTP Allow. |
| `PasDefinitionsComeFromPackageContent` | Eight R4 rows: manifest-only/definition-containing × HTTP/direct × JSON/XML | Both package shapes enable executable PAS dispatch (bodyless POST remains 422/fatal/structure). Only definition-containing packages supply the two literal resources; manifest-only reads retain the existing 404/error/exception missing-resource response. Independently authored complete JSON/XML expectations and normal generated-meta bindings verify supplied content and later reads. No cache invalidation or store recreation. |

Recovery adds **370 theory rows: 212 HTTP and 158 direct**, including 36
HTTP rows using the existing factory directly for tenant/authorization
boundaries. `OperationTheoryDataIsNonEmptyAndLabeled` checks these additions
as well as every original count and the twelve retained GET rows. All
original fixtures, the 468 validation rows, 24 discovery rows, 204 route
rows, subscription cases, and original result-header multiplicities remain.
Thus all **2,788 original selected case occurrences** (1,526 retained
regressions plus 1,262 original phase-5 additions) remain under the unchanged
cumulative filter. The authored recovered inventory is **3,158 selected
cases**, not an executed/discovered/pass count; phase-5 additions now total
1,632.

### Raw fixture and version provenance

- Each `validation-resources.json` contains six raw targets/wrappers; the
  synthetic common subset is deliberately identical across R4/R4B/R5.
  Wrapping/ignored-parameter variants are authored in the Operations partial.
- Each `subscription-resources.json` contains eight raw resources: two
  Patients, planned/terminal Encounters, three topic criterion families, and
  an active local Subscription. R4 uses Basic/cross-version topic extensions;
  R4B uses native SubscriptionTopic with backport Subscription primitive
  extensions; R5 uses native topic/filter/channel/content fields.
  R4/R4B Encounter.status remains **finished** and class is a Coding;
  R5 remains **completed** and class is a CodeableConcept array. These are
  distinct literals, not a runtime coercion to one model.
- `r4/cdex-task-resources.json` contains the synthetic Patient and four Task
  shapes. The data-query is `Patient?_id=cdex-patient`; it cannot contact a
  remote server. The expanded package manifest is explicitly synthetic,
  empty, and not official IG content. Tests copy it to their own temporary
  directory and call `IFhirStore.LoadPackage` with
  `hl7.fhir.us.davinci-cdex#0.0.0-sdk6-test`. PAS uses the same manifest
  template with its own package name/version declaration, not CDex resources.
  For R4, `ExpandedOperationPackage` writes two raw JSON OperationDefinitions
  into that temporary package **before** normal `LoadPackage`:
  - `sdk6-pas-claim-submit`, canonical
    `http://hl7.org/fhir/us/davinci-pas/OperationDefinition/Claim-submit`,
    name `$submit`, code `submit`, affectsState true;
  - `sdk6-pas-claim-inquiry`, canonical
    `http://hl7.org/fhir/us/davinci-pas/OperationDefinition/Claim-inquiry`,
    name `$inquire`, code `inquire`, affectsState false.
  Both have kind operation, status draft, resource Claim, system false,
  type true, and instance false. These minimal synthetic definitions omit a
  business `version` element; the fixture directive's `0.0.0-sdk6-test` is
  distinct from the implementation's `OperationVersion` of `1.2.0`.
  IDs are literals derived from neither version. Expected JSON and XML are
  separately authored in the Operations partial, not parsed with Firely,
  copied from responses, or official IG contents. Only ordinary generated
  `meta.versionId`/`meta.lastUpdated` are bound. R4B/R5 PAS packages remain
  manifest-only negative version controls; the new R4 manifest-only case
  separately distinguishes executable gating from supplied definitions.
  No archive helper or registry is involved.
- New notification endpoints are loopback URLs. The existing isolated factory
  does not host NotificationManager, and event handlers are unsubscribed before
  replay disposal. This phase verifies generation/serialization and inbound
  operation state, **not actual outbound delivery** or phase-6 teardown.
- Validation issue mappings were authored against the structural contract and
  read-only inspection of SDK 6.2.1 `CodedValidationException` definitions:
  PVAL105/102 → error/required; PVAL123/124 and PVAL115 → error/value.
  No parser/validator was run to manufacture expected values. Vendor prose
  is only nonempty; Candle messages and target/element relationships remain
  explicit. Inspector initialization failure mapping is preserved but not
  fault-injected or claimed executed.

### Attempt 1 diagnostic evidence (before the exact acceptance gate)

The documented focused command, filtering only
`Sdk6ContractTests.ValidatePreservesTargetAndIssueContract`, compiled with
zero warnings/errors and ran **468 tests: 132 passed, 336 failed**, none
skipped. Raw transcript:
`scratch/0919-01/dev-do-phase5-attempt1-validation-regression-20260922-194732.log`.
Of the failures, 216 were ordinary parser rejections at 415 of missing required
fields, invalid primitives, or empty Parameters; 120 were new test-helper
duplicate binding/emptiness-check IDs. The latter were corrected in the owned
Operations partial, not in completed assertion/replay behavior.

The bounded runtime change selects the **existing lenient parser only for
`$validate`** in `DoSystemOperation`, `DoTypeOperation`, and
`DoInstanceOperation`. It leaves ordinary parsing, other operations, route
support, the inspector guard, and completed exchange/search/compartment
contracts unchanged. The exact phase-5 build/test commands are still required
on the final implementation tree; this regression run is not their substitute
and does not establish that the repaired or newly authored paths pass.

### Recorded attempt-1 gate and recovery verification state

The runner's retained attempt-1 evidence, **not rerun by this implementation
slice**, is: the exact application build passed with zero warnings/errors;
the unchanged cumulative test command executed **2,788 cases: 2,772 passed,
16 failed, zero skipped**, with no compiler/analyzer warnings/errors.
All 1,526 retained regressions and all 468 validation cases passed. The
sixteen failures were the twelve O1 GET rows and four R4 O2 discovery rows.
Full retained test output:
`scratch\0919-01\dev-do-phase5-attempt1-tests-20260922-200917.log`.
These failures remain historical evidence, not retroactive passes.

**Recovery verification has not yet run.** No build or test command was run
for this bounded implementation slice. All 24 original discovery rows retain
the initial metadata read, the same replay/store, definition reads, canonical
and implementation-version checks, both PAS route probes, negative
package/version cases, and later capability assertions. The formerly masked
per-definition checks, **eight positive PAS dispatch probes**, and post-load
capability checks in the four R4 rows have **not been observed executing after
this correction**. Neither have the 370 new controls. Potential capability
freshness is a source-predicted concern, not an additional executed failure;
no production discovery/cache repair or test cache workaround is included.
The runner must run both exact unchanged acceptance commands on this tree
and record newly reached probes and any remaining failures. This inventory
does not claim recovered acceptance, later migration gates, or other
framework/OS/browser results.

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

## Phase-5 attempt-2 gate and bounded O3 recovery

This appendix records the runner's **2026-09-22 attempt-2 evidence**, not a
rerun by this implementation slice. Earlier diagnostic, failed-gate, and
pre-gate descriptions above remain their original chronology.

- The exact application build exited 0 with zero warnings/errors.
- The unchanged cumulative test command exited 1: **3,158 executed / 3,154
  passed / four failed / zero skipped**, with zero compiler/analyzer
  warnings/errors. Result-header multiset reconciliation retained all
  **2,788 original occurrences plus 370 passing recovery controls**,
  including duplicated truncated display names.
- All original O1 GET rejection rows and the O1 recovery controls passed.
  O2's supplied-definition inventory/content, generated metadata bindings,
  per-definition reads, implementation/version gates, and all **eight
  positive PAS POST dispatch probes** executed successfully.
- The four R4 PAS discovery rows (HTTP/direct x JSON/XML) reached the
  post-load capability checks. System/Patient/Subscription advertisements
  passed; Claim's exact name assertion expected
  `[$inquire, $submit, $validate]` but received `[$validate]`.
  The subsequent Claim canonical assertions were **not yet executed**.

O3 is the explicitly approved **pre-existing capability-cache freshness
defect**, not an SDK6 incompatibility or a remaining PAS fixture error.
`LoadPackage` reruns `CheckLoadedOperations`, but successful executable
registration did not invalidate the cached CapabilityStatement. PAS's
optional definitions deliberately return null, so definition-resource
writes cannot serve as the registration signal. The plan records source
provenance before this migration: discovery/load in `c7511424` (2023-08-28),
`TryAdd` in `01885bd9` (2025-02-05), and the cached return in `18588bbe`
(2024-09-03).

The bounded repair sets the existing `_capabilitiesAreStale` flag to true
immediately inside successful `_operations.TryAdd`, before the existing
`try` / optional `GetDefinition`. It also covers an already registered
executable whose definition processing throws, without changing the catch
or logging. Startup still generates capabilities after discovery; normal
package rediscovery and the lazy cached/deep-copy/forwarded-root paths are
unchanged. A duplicate or ineligible registration neither invalidates a
warmed cache nor clears pending staleness. There is no eager regeneration,
unconditional package-load invalidation, cache replacement, or method-policy
change.

### Authored O3 controls (2026-09-24; not executed by this slice)

| Added test / matrix | Authored rows and boundaries | Independent contract |
|---|---|---|
| `PackageOperationRegistrationRefreshesCachedMetadata` / `OperationCapabilityRefreshCases` | **12**: one manifest-only kind x R4/R4B/R5 x HTTP/direct x JSON/XML; four per version, six per transport, six per format | Settled O3 successful-registration contract, literal `OperationContracts`, existing BCL assertions, and normal replay-owned package loading. Each row owns two differently named same-version tenants. |

Each row primes ordinary metadata for the target and its package-absent
sibling, then checks their independent built-in names/canonicals and
tenant/FHIR identities. It loads the same manifest-only PAS package twice
consecutively into the target's existing store **before any post-load
metadata read**. No supplied definitions or resource-write side effects
can mask registration-driven invalidation, and the immediate duplicate
cannot clear R4's pending refresh. Only R4 then advertises exactly Claim
`$inquire`, `$submit`, and `$validate`, with the literal canonicals;
system/Patient/Subscription sets remain exact. R4B/R5 retain built-ins and
unchanged content, and the sibling retains its independent identity,
built-ins, and complete content.

The next target read obtains the warmed persisted cache; it is deliberately
not required to equal the first genuinely regenerated response, which may
not yet carry persisted metadata. A repeated ordinary read and another
load of the same manifest-only package must each preserve the full warmed
content under `WireContractAssertions.Content`, including `date`,
`meta.versionId`, and `meta.lastUpdated` when present. None is excluded as
volatile. No exact version increment or timestamp is required for the
genuine rebuild. The captured warmed content is only a relational
no-mutation check, never the operation-name/canonical oracle. Every metadata
response retains independent exact advertisement multiplicity/canonical
and tenant/version checks; the sibling is checked again after the no-op
reload. Both tenants' definition searches must contain only the independent
built-ins, omit PAS canonicals, and return the existing 404 outcome for the
literal supplied PAS definition IDs. The fixture's `0.0.0-sdk6-test` version
and the implementation's `1.2.0` version remain distinct.

All requests use ordinary metadata without Forwarded or cache-forcing
flags. No private generator, stale-field access, cache reset, replacement
store, production API, static state, registry, or network fixture is added.
The original **24** `OperationDiscoveryIsVersionAndPackageCorrect` rows
and `AssertOperationAdvertisements` are untouched, including the original
metadata-before-load -> single normal package-load -> metadata-after-load
sequence, supplied-definition reads, eight positive dispatch probes, and
all canonical assertions. The extra loads exist only in the new test.
The inventory fact adds the exact twelve-row count and labeled/nonempty
matrix checks without changing any previous count or matrix.

The unchanged-filter total is **projected 3,170 = 3,158 retained + 12
authored additions**, not an executed/discovered/pass count. No build,
test, or install was run for this O3 implementation slice, and inspection
does not establish compilation or passing gates. The parent must run both
exact phase-5 commands, reconcile all retained result-header
multiplicities, and record whether the four formerly failing discovery
rows now reach their Claim canonical assertions. Final verification and
any remaining failures belong in that subsequent record; no phase-6/7,
browser, other-framework, or other-platform result is claimed here.

### Recorded O3 required-gate result (2026-09-24)

The runner subsequently executed both exact phase-5 commands on Windows
with Release/net10.0. The application build passed with zero warnings and
errors. The unchanged cumulative test gate passed **3,170/3,170**, with
zero failures, skips, or compiler/analyzer warnings/errors.

Result-header multiset reconciliation against attempt 2 retained all
**3,158** previous occurrences, including the indistinguishably truncated
legacy displays, and added exactly the **12** capability-refresh rows.
All 24 original discovery rows now pass, including the four R4 PAS rows
whose Claim-name failure previously masked canonical assertions. All
twelve new rows pass: four per FHIR version, six per transport, and six
per format. The original single-load regression and the new pending-stale,
warmed-cache/no-op, manifest-only, and sibling controls all execute.

This records the first successful required gate, before this evidence
append. The execution record separately requires the same two commands
again on the final documentation tree before staging and commit; no new
runtime or test change accompanies this record. It does not establish
phase-6/7 integration, browser behavior, other frameworks, other platforms,
or completion of the migration.

## Phase-6 offline integration inventory (authored 2026-09-24)

This append belongs to **original phase 6, attempt 1**, within the ongoing
`dev-do-search-recovery` stage work attempt 3. It does not replace any
earlier execution record. The cases below are **authored, not executed
gate results**. The implementation worker has not run a build, test,
restore, install, application host, or public-network probe. The parent
owns the exact phase-6 build/test commands, failed-gate accounting, the
required repeat after the first passing test gate, and subsequent results.

### Labeled case and method inventory

All three new classes join `CompatibilityHostCollection.Name`. Each has
an inventory fact asserting nonempty, unique labels and the explicit
version matrix; these are not claims about test discovery or pass counts.
There are **76 authored top-level cases** (8 package, 31 client, 37
notification), subject to the parent's actual discovery and execution.

| Case | Authored versions / formats / methods | Content and negative assertions |
|---|---|---|
| `PackageIntegrationInventoryIsNonempty` | R4/R4B/R5; named archive/loading inventory | Three unique labeled version inputs. |
| `LocalPackageAcquisitionCacheAndLoadingPreserveResources` | Three version rows; real registry GET; Patient JSON and independently authored Observation XML inputs; both JSON/XML store reads | Captured acquisition request; exact archive resource contents and manifest/author/FHIR version; same-instance empty second result; stopped registry plus a fresh no-registry cache consumer; a **third** package service first consumed by normal `FhirStoreManager.Init`; exact loaded directive/IDs; two wrong-version tenant negatives; separate empty-cache/no-registry and invalid-gzip controls. |
| `CiPackageClientPreservesListingDownloadAndManifestContract` | Three version rows; `DownloadListingAsync`, `GetReferences`, `GetTagBasedReference`, `GetPackage`, `IsPackageOutdated`, `InstallOrUpdate`, `UpdateCiListingCache` | Authored listing fields; resolved versus tagged identities; cold tag installation; warm no-download result; newer QA replacement removing the old resource; raw replacement file/manifest; inherited author fields for object and string author forms; unresolved package and pre-I/O denied URI controls; exact seven-request sequence. |
| `InitialDataUsesTenantDirectories` | Six simultaneous custom tenants: oak/elm for each R4/R4B/R5; JSON Patient and separately authored XML Observation | Normal manager loading, exactly one clinical resource of each type per tenant, all ten sibling IDs absent from each tenant, exact tenant supplements, no package directives, and full independent JSON/XML reads including the tenant-specific Observation subject. |
| `ClientIntegrationInventoryIsNonempty` | 24 settings rows, six error/cancellation rows, 13 named public method/overload probes | Nonempty unique labels, all versions and formats, fixed method count; no invented search API. |
| `CandleClientPreservesRequestAndResponseContract` | R4/R4B/R5 x JSON/XML x four explicit handling/version/format-setting modes | Each row calls `CreateAsync`, `ConditionalCreateAsync`, `ReadAsync(string)`, `ReadAsync(Uri)`, `UpdateAsync`, `ConditionalUpdateAsync`, `DeleteAsync(string)`, `DeleteAsync(Uri)`, `DeleteAsync(Resource)`, `PatchAsync(Uri, Parameters)`, `PatchAsync<T>(id, Parameters, versionId)`, `ConditionalPatchAsync<T>`, and `TypeHistoryAsync`; adds ordinary Create with minimal and OperationOutcome return preferences. |
| `CandleClientPropagatesFailureAndCancellation` | Six version/format rows; all 13 method/overload probes | Actual 404/409/412 outcomes with exact id/severity/code/details/diagnostic/expression; a separately held, captured request canceled for every method; receiver observes cancellation before release; same client succeeds on a subsequent read. |
| `NotificationIntegrationInventoryIsNonempty` | 18 delivery and 18 shutdown rows | Exact nonempty version/format/content-level and lifecycle-mode matrix. |
| `RestHookNotificationDeliversVersionCorrectWireBody` | R4/R4B/R5 x JSON/XML x empty/id-only/full-resource; real POST | Actual matching store event after a filter-miss control; correct status resource and Bundle type, media type, tenant header, subscription/topic/event/focus/additional-context identities; exact content-level absence/presence; complete focus/Patient resources with bound metadata; same-version sibling and no-inbound-notification controls. |
| `NotificationManagerStopsDeliveryBeforeFixtureTeardown` | R4/R4B/R5 x JSON/XML x drain/cancel/dispose; two real held POSTs | Sends from two different tenants remain in flight across queue callbacks. Concurrent stops cannot complete while either graceful send is held. Releasing only one cannot finish stop. Canceled stop throws, followed by a separate join of the same drain. Concurrent Dispose cancels and joins sends. Repeated stop/dispose and actual events after stop and after receiver disposal produce no further serialized sends or requests. No sleep establishes success. |

Client request assertions inspect method, exact tenant path/raw target,
decoded query key/value multiplicity, Host, Accept, content type, the
format parameter, return preference, If-None-Exist, If-Match,
If-None-Match, If-Modified-Since, and the entire raw Patient/Parameters
body. Canned replies are separately authored, not SDK snapshots. Reply
assertions require the version-specific Patient type, exact server ID,
version/date/tag, changed language/active/name/identifiers, month-only
birth date and its primitive extension, integer choice, binary bytes,
practitioner reference/display, XHTML mixed-content whitespace, and
absence of caller-only gender/organization fields. History asserts both
the populated PUT entry and the resource-less DELETE entry, their order,
request/response details, and the tenant-specific self/fullUrl values.
OperationOutcome return content is checked through `CreateAsync<Resource>`
using a correctly aliased Patient as input; no dynamic or reflected
replacement client is involved.

### Synthetic inputs and local-only transport

The three new `package-manifest.json` files declare
`example.test.sdk6.r4`, `example.test.sdk6.r4b`, and
`example.test.sdk6.r5`, each pinned to stable-shaped **1.0.0** with
explicit **4.0.1 / 4.3.0 / 5.0.0** `fhirVersions` and empty dependencies.
They are local synthetic packages, not published HL7 packages or claims
about official registry availability. Author addresses and canonicals
under `example.test` are inert data, never network destinations.

`LocalPackageFixture` builds USTAR/gzip bytes with
`System.Formats.Tar.TarWriter` and `GZipStream`, containing
`package/package.json`, a JSON Patient, and an XML Observation. The
companion cache generates its own index. No archive is checked in, no
Firely serializer authors the expected side, and no user cache is used.
The resource originals are the existing per-version `patient.json/xml`
and `observation.json/xml`. BCL edits supply only explicit IDs and, for
initial-data cases, tenant-specific subject references.

Every service configuration names an existing, owned, empty source
directory and a unique owned temporary cache, clears
`ReferenceImplementation`, disables official registries, and leaves NPM,
CI, and requested package arrays empty except for the particular local
package under test. `Init` with this configured cache sets the current
`IsReady`; the tests do not invent a hosted-start prerequisite.
Acquisition, fresh warm-cache lookup, and manager loading use distinct
service instances. In particular, the manager's service is never
pre-consumed by an earlier `InstallPackages`, whose `_processedMonikers`
would otherwise skip the requested content. Initial-data tests create
custom tenant folders only, not generic `r4`/`r4b`/`r5` directories.
This is the custom-directory fallback documented in
`docs/user/loading-data.md` and implemented by
`FhirStoreManager.loadContents`, not a test-specific loader.
Owned stores are explicitly released just as in the existing isolated
host fixture; no other service's production lifetime is changed.

`LocalHttpFixture` uses the existing ASP.NET Core stack, with Kestrel
listening only at **127.0.0.1:0** and its actual address read from
`IServerAddressesFeature` after start. It has finite expected method/path
occurrences, captures raw bytes/headers without FHIR parsing, records
unexpected requests or response failures, bounds startup/request/wait/
shutdown operations to 30 seconds, and awaits listener teardown.
The minimal host also owns and deletes a unique empty temporary content
root, so appsettings files in an ambient working directory are not inputs.
Slow-response and cancellation cases use completion signals, not sleeps
or an assumption that TestServer owns a TCP port. Client wrappers create
their own actual Firely clients against this listener.

The CI fixture uses `FhirCiClient`'s existing injected `HttpClient`.
Its finite handler permits only the enumerated HTTPS build host/path
sequence, checks method and original absolute URI **before outbound I/O**,
then rewrites to the numeric loopback listener. Its underlying handler
disables proxies and redirects. The authored sequence is:

1. GET `/ig/qas.json` (original synthetic QA).
2. GET `/branches/` (authored empty core listing).
3. GET `/ig/example/sdk6/branches/main/package.tgz` (`GetPackage`).
4. GET the same archive (`InstallOrUpdate` cold install).
5. GET `/ig/qas.json` (newer synthetic QA).
6. GET `/branches/` (same empty core listing).
7. GET the archive (replacement install).

The warm install and unresolved-package checks add no download.
QA versions resolve literally to `1.0.0-cibuild+20240101-000000Z` and
`1.1.0-cibuild+20240105-000000Z`; installation remains under `current`,
with the explicit `current$main` tag also checked. Manifest dates retain
the existing compact **local-time** parsing policy. The authored manifest
dates are later than their corresponding QA date in every time zone;
the replacement QA is later than the original manifest. Date comparison,
branch selection, tag policy, and BCL helper behavior are not repaired or
redefined by these tests. Object author fields in the initial manifest
and `"name <email> (url)"` in the replacement both traverse the local
adapter and its inherited `Firely.Fhir.Packages.AuthorInfo`.

Notification seeds reuse the completed `subscription-resources.json`
fixtures read-only: Basic topic/backport Subscription for R4, native
SubscriptionTopic/backport Subscription for R4B, and native R5 resources.
Only local endpoints, content format/level, test headers, and a zero
heartbeat period are adjusted for finite transport cases. Independent
XML Patient/Encounter and JSON/XML status expectations live in the new
notification test, not in made-public completed private helpers. R4
status is Parameters in a history Bundle; R4B is SubscriptionStatus in a
history Bundle; R5 is SubscriptionStatus in a subscription-notification
Bundle. Generated UUIDs/timestamps are checked and bound locally, and the
wire event timestamp must equal the actual generated store event.
SMTP/Zulip configuration stays empty; no external destination is tested.

### API provenance and the precisely accepted production exception

Conventions and command authority were read from `AGENTS.md` (with
`.editorconfig` and `.github/copilot-instructions.md` for style).
Codegraph first located the consumer/caller paths, followed by exact
source reads of `CandleClient`, `CandleClientSettings`, `FhirPackageService`,
`FhirStoreManager`, `FhirCiClient`, `DiskPackageCache`, `JsonModels`,
`AuthorJsonConverter`, `NotificationManager`, and the completed independent
oracle/fixtures. The new source preserves explicit types, nullable checks,
public XML documentation, copyright headers, and CRLF.

Cached `Hl7.Fhir.Base` **6.2.1** XML documentation grounds the wrapper
overloads, `SearchParams.Add`, explicit settings, lenient serializer,
FHIR patch Parameters, TypeHistory, and `FhirOperationException` outcome/
status contract. Installed assembly metadata/IL was also inspected for
the exact request builder, model/nullable identities, and reply
extraction. `Firely.Fhir.Packages` **5.0.2** ships no XML documentation in
the installed package; its public assembly signatures and URL/cache
implementation metadata, plus the exact local callers, ground the
companion calls instead. No dependency/tool installation was performed.

Two important SDK request boundaries are explicitly retained, not
silently turned into failing speculative expectations:

- SDK 6's `EntryToHttpExtensions` emits `handling=` for **Search only**.
  Both strict and lenient settings are supplied, and every exposed
  wrapper interaction asserts its absence. This does not claim server
  search handling coverage or justify adding a wrapper search method.
- `return=` is emitted for ordinary Create/Update/Patch (and Transaction,
  outside this wrapper inventory), not their conditional interaction
  variants. These positive and absent headers are asserted separately.
  The version-in-Accept MIME parameter is **4.0 / 4.3 / 5.0**, not the
  full patch release; `_format` mode replaces Accept rather than adding
  a second negotiation mechanism. Unrelated nullable-default mappings
  and the wrapper's existing public lifecycle remain unchanged.

The **only production edit** in this phase is the previously authorized,
pre-existing `NotificationManager` teardown defect—not a claimed SDK 6
regression. It remains in the same manager and public hosted/disposal
lifecycle:

- Scheduling and shutdown share a manager-local guard. Shutdown disables
  both timers, detaches subscription events, clears queued work, and
  prevents racing callbacks/events from starting another delivery.
- Both timers are disposed asynchronously to join already queued
  callbacks. Every started delivery is tracked and joined before a
  successful stop can return. Multiple stops share that same drain.
- Canceling hosted stop cancels supported sends and throws if the caller
  stops waiting; it never reports an incomplete drain as success.
  Subsequent stop/dispose still joins the retained drain. Dispose is
  idempotent, cancels pending supported transport work, waits for it, and
  only then disposes the owned HTTP client/cancellation source.
- The existing HTTP `SendAsync` and MailKit **4.17.0** connect/authenticate/
  send/disconnect overloads accept cancellation, as verified in their
  installed docs. REST responses are disposed. The installed Zulip
  **0.0.1-beta.5** `TrySendStream`/`TrySendPrivate` APIs have **no**
  cancellation overload; started calls stay awaited and no fictional
  cancellation API or fire-and-forget replacement is introduced.
  Existing logging distinguishes failed sends from shutdown cancellation.

No conditional SDK/client/companion/store-loading production repair was
demonstrated or made by this implementation. `RestOnlyNotificationManager`,
Common contracts, other DI services, bootstrap/project/alias/copy wiring,
date/version helpers, and all completed parser/search/O1/O3 changes remain
untouched. Live registry behavior, SMTP/Zulip account delivery, browser
workflows, other framework/platform cells, and all phase-6 acceptance
gates remain unverified by this authored slice.

Execution needs only the existing restored repository dependencies,
an available OS-assigned IPv4 loopback socket, and writable temporary
storage outside the repository. The parent's required commands and repeat
are unchanged; their actual discovery, compilation, pass/fail, warnings,
teardown, and repeat results must be appended later, not inferred from
this inventory or static inspection.

The implementation-only checks were read-only source/API inspection,
in-memory Roslyn **syntax parsing only** (no compilation or emission),
`git diff --check`, line-ending/path/index checks, and verification that
the previous README remains an unchanged prefix. These are not the
parent's build/test gates, and they establish no runtime pass result.

### Recorded phase-6 execution through attempt 3 (2026-09-24)

The runner's exact application build passed in every attempt with zero
warnings/errors. Attempt 1's exact test command stopped at compilation:
two new `PerformInteraction` calls placed the named serialization argument
before the unnamed out argument, and three Patient initializers needed
their version-local `AdministrativeGender` aliases. Correcting these five
test-authoring errors retained the original `Other` enum values, all
inputs, and all assertions.

Attempt 2 executed **3,157 cases: 3,121 passed, 36 failed, zero skipped**,
with zero compiler/analyzer warnings/errors. All 3,081 retained cases
passed. All eight package cases, 18 delivery cases, six disposal cases,
six client error/cancellation cases, and the three inventories passed.
The 24 client-success rows failed at successful DELETE; six graceful
shutdown rows recorded a local-fixture response error; six canceled-stop
rows did not cancel the outstanding sends before the fixture deadline.

The final attempt corrected only new phase-owned mechanisms:

- A 204 fixture response must have an empty authored body and sends no
  Content-Length or body write. Even a zero-length write was entering
  Kestrel's forbidden-body path. The expected 204 statuses remain unchanged.
- Canceled fixture responses abort the connection instead of becoming an
  implicit successful 200. Unexpected cancellation still fails; fixture
  assertions now expose full diagnostic messages rather than truncated
  collection entries.
- A canceled hosted-stop wait explicitly cancels tracked transports and
  rethrows. It no longer relies on a separate token callback that the
  wait's cancellation continuation could dispose before invocation.
  The same drain remains available for subsequent stop/dispose calls.

Both exact phase-6 commands then passed on Windows Release/net10.0:
**3,157/3,157**, zero failures/skips, and zero compiler/analyzer warnings
or errors. Result-header multiset comparison retained every attempt-2
occurrence unchanged. All **76** new cases pass (8 package, 31 client,
37 notification), including every previously failing row and its later
method/teardown assertions. No contract, case, header, status, or
content assertion was relaxed.

This is the first passing gate before this evidence append. The required
repeat of the unchanged focused command, with the exact build again on
the final documentation tree, remains separately recorded by the runner
before any commit. These results do not establish browser or other
framework/platform gates or phase-7 completion.

## UI data-path inventory (phase 7, authored 2026-09-24)

`Sdk6UiContractTests.cs` adds **12 theory rows and one inventory fact** to the
existing xUnit project: six table rows and six viewer rows, each spanning
R4/R4B/R5 and JSON/XML input. These are authored counts, **not execution
results**. Each ordered scenario owns/disposes its actual `CompatibilityReplay`
stores and bindings; the class joins `CompatibilityHostCollection`. It reuses
the completed replay/typed BCL assertions and checked-in fixture pairs read-only,
without a new oracle, expected-side Firely model evaluation, or output capture.

| Test / rows | Consumer and expectation source | Exact content and deliberate variability |
|---|---|---|
| `StoreAndResourceTablesPreserveDisplayValues` / 6 | `FhirStorePage.GetFilteredItems`, `ResourceList.GetFilteredItems`, `IResourceStore.GetInstanceTableView`, `InstanceTableRec`, and `ResourceStore.GetName/GetIdentifier/DisplayFor`; existing lenient PUT/create/update/delete contracts. Raw versioned Patient/Observation fixtures plus independently authored inline JSON/XML StructureDefinition. | Per-type counts 0 → 1 → 1 → 0; exact ID/name/ordered identifiers, generic empty URL/description versus conformance URL/description, column flags and case-insensitive filters. Primary updates/deletes must not change same-ID sibling rows/counts. Mutation responses bind version 1 → 2 and matching headers; delete retains the bound resource content with absent resource headers. |
| `ResourceViewerReadsMetadataAndBothFormats` / 6 | `ResourceViewer.LoadResourceContent`, `VersionedFhirStore.GetMetadata/InstanceRead`, original CRUD fixtures/contracts and the already authored cross-format pairs. | Each input-format row reads JSON → XML → JSON with the actual tenant/resource/ID context and `SerializePretty=true`, not generic dispatch. Complete Patient/Observation content, generated ID/version/time/header relationships, independently expected capability versions/tenant URL, observed update values, unchanged sibling, deletion and missing resource/outcome separation. Reads reuse bindings rather than inventing metadata increments. |
| `UiTheoryDataIsNonEmptyAndLabeled` / 1 fact | Existing corpus inventory convention. | Requires six uniquely labeled rows in each set, all three versions and both input formats, and the ordered viewer format sequence. This prevents empty/missing-version declarations; only execution can establish a pass. |

The table literals are independently authored: `ui-patient`,
`Wire, Avery Quinn`, and ordered
`https://example.test/identifier|wire-{r4|r4b|r5}, https://example.test/secondary|second-{r4|r4b|r5}`.
The update is `Revised, Avery Quinn` with primary identifier
`updated-{version}`; the sibling remains `Sibling, Avery Quinn` with
`sibling-{version}`. Observation has no name/URL/description and displays
`https://example.test/identifier|quantity-{version}`, then
`updated-quantity-{version}` under that same system. The inline
StructureDefinition uses `ui-profile`, name `UiPatientProfile`, canonical
`https://example.test/fhir/StructureDefinition/ui-profile`, description
`Synthetic table profile.`, and identifier
`https://example.test/ui-profiles|ui-profile`. Its name, canonical, description
and identifier all receive explicit updated literals; its ID is unchanged.
Its separate `title` is deliberately not the Name column. No new fixture path
or existing raw fixture was changed.

Viewer assertions preserve the fixture's original tags, `language=en`,
ordered identifiers/names, month-precision birthDate and primitive extension,
XHTML spaces/newline/whitespace-only paragraph, multipleBirth and Observation
choice names, relative references, bound absolute Patient reference,
`1234567890.123400`, and `2024-06-03T10:11:12.3400-05:00`. Updates require
`en-US`, the revised family/identifier, `1234567890.123450`, and
`2024-06-04T10:11:12.3400-05:00`. Decimal tokens never pass through `double`.
IDs, versions, timestamps and URLs are locally bound; the same bindings must
match repeated read content, Location, weak ETag and Last-Modified at HTTP-date
precision. Metadata asserts literal `4.0.1`/`4.3.0`/`5.0.0` and
`{tenant-base}/CapabilityStatement/metadata`, not `implementation.url`.

Pretty output is checked for structural indentation, not size ordering or
the narrative's own newline. Direct `Resource`/`Outcome` and their serialized
fields remain distinct. A valid-context missing read returns false/404, empty
resource content and the exact `Resource: {type}/{id} not found`
error/exception outcome, without identity headers. The current viewer only
displays `SerializedResource`; this test does not claim an outcome banner,
browser authentication behavior, Razor rendering, or browser event coverage.

### Gate distinctions and supplied verified evidence

The supplied final phase-5 evidence is Windows Release/net10.0:
the required build and scoped **3,170/3,170** gate passed twice with zero
warnings/errors/failures/skips, committed as
`69c8ddb591c14fc4dd8673c613204100028c65bd`.
The supplied final phase-6 evidence is Windows Release/net10.0:
the required build and scoped **3,157/3,157** gate passed twice with zero
warnings/errors/failures/skips, committed as
`47913dada668331195c212903bc91060a551c2d9`. The latter includes all
**76** additions (8 package, 31 client, 37 notification). This records the
verified repeat subsequent to the earlier phase-6 entry without rewriting it.
These are different scoped filters, not final full-suite totals or matrix
results.

The phase-7 UI additions remain **unexecuted** in this implementation slice.
No production change has been demonstrated or made here. The
[SDK 6 compatibility document](../../../../docs/technical/sdk6-compatibility.md)
maps the actual UI imports/consumers, preserved aliases/Common/DI/reflection
boundaries, accepted SDK/companion cohort, and version-specific differences.
It distinguishes SDK-facing adaptations from the accepted pre-existing
P1–P4/M1, O1/O3 and notification teardown repairs, fixture-only O2, U1's
dedicated-method boundary, and the local dual-SUBSETTED contract.

That document separately records the final local/all-target commands,
nine OS/framework cells, explicit offline RI/browser startup and routes,
and pending manual results. Windows net8/net9 and final net10 full-suite/UI
evidence remain pending; Linux/macOS remain not run. Pinned offline RI cache
setup is outstanding, not a live-registry permission. The current CDex
table's `Task.code` assumption versus the minimal API hook fixtures is an
explicit source-level browser-seed caveat, not a speculative runtime repair.
Neither this inventory nor static inspection marks phase 7 complete or the
migration finally verified.

### Recorded phase-7 gates and final blocker (2026-09-24)

The exact scoped build passed with zero warnings/errors. Attempt 1's
scoped test run executed **3,093 cases: 3,087 passed, six failed, zero
skipped**. All table rows and retained contract cases passed; all viewer
rows stopped on a new helper's duplicate `metadata-date` scalar capture.
The completed common oracle deliberately captures each named scalar once.

Attempt 2 changed only that new test helper: later metadata reads receive
distinct scalar names and explicitly require equality with the original
captured date. Resource/header bindings remain reused, and no date/content
assertion or case was removed. Both exact phase-7 commands then passed:
**3,093/3,093**, zero failures/skips and zero compiler/analyzer warnings
or errors. Multiset comparison retained every prior case occurrence;
all **13** new UI cases now execute their full sequences.

The required final solution build for Release/net10.0 also passed without
warnings/errors. The **unfiltered** suite then failed:
**4,258 executed / 2,668 passed / 1,590 failed / zero skipped**, with zero
compiler/analyzer warnings/errors. Every failure reports Windows
Application Control blocking `FhirCandle.Ui.R4B.dll` with **0x800711C7**:
1,564 direct `FileLoadException` failures plus 26 fixture-constructor
failures with that same inner exception. The observed path is
`FhirStoreManager.LoadPackagePages` during store-manager initialization.

These are failed tests, not skips, proven SDK regressions, or permission
to bypass the operating-system policy. No discovery workaround, assertion
waiver, changed launch configuration, or policy bypass was attempted.
Phase 7 remains **Blocked and uncommitted**, with its owned changes
preserved. The tested UI source SHA256 is
`63AB2B9DD121AE7908101E72B4A66CAF12433962B51159029E8F42635629D044`;
the runtime source remains at phase-6 commit
`47913dada668331195c212903bc91060a551c2d9`. Only evidence documentation
changed after the failing full gate.

An authorized environment resolution and a fresh full gate are required;
assertions masked by the assembly-load failure remain unverified. The
Windows all-target sequence, all Linux/macOS cells, and all browser/RI
workflows were not started after this blocker. Their outstanding
environment/offline-package prerequisites are not waived. Review remains
conductor-managed after execution prerequisites pass.

### Bounded phase-7 recovery evidence (2026-09-30)

Original phase-7 attempt 3 revalidated the retained tree without changing
runtime code, test cases, or assertions. Runtime source remains at
`47913dada668331195c212903bc91060a551c2d9`; the unchanged UI-test source
SHA256 remains
`63AB2B9DD121AE7908101E72B4A66CAF12433962B51159029E8F42635629D044`.
All five retained phase-7 files matched their intake bytes through verification.

| Exact required gate | Windows Release/net10.0 result |
|---|---|
| Solution build and unfiltered suite | Build passed; **4,258/4,258** passed, zero failures/skips and zero compiler/analyzer warnings/errors. |
| Phase-7 application build and scoped UI/contract suite | Build passed; **3,093/3,093** passed, including all 3,080 contract cases and thirteen UI cases; zero failures/skips and zero compiler/analyzer warnings/errors. |

The environment was Windows x64 build 26200 with SDK **10.0.400** and
runtime **10.0.11**. Exact result-header multiset comparisons against the
hash-verified historical logs preserve every full-suite and scoped occurrence;
all **1,590** previously failed occurrences now pass. Normal
`FhirStoreManager.Init` / `LoadPackagePages`, including its R4B assembly scan,
remains enabled.

The ordinary documented build/test commands regenerated the R4B UI DLL at
the same normal test-output path. No forced clean, output relocation,
policy/trust change, discovery suppression, or weakened assertion was used.
This is passing current runtime evidence, not a separate load test of the
historical DLL or proof of a particular administrative policy change. The
earlier failed results above remain historical facts, not retroactive passes.
Fresh assets retain the accepted SDK/companion cohort across ten consumers
and thirty framework graphs, with R6 only in tests.

Recovery stopped on the remaining mandatory setup/evidence gates: WSL has
no installed distribution, no macOS execution host was supplied, and the
exact subscriptions-backport **1.1.0** and CDex **2.0.0** packages were not
found in the checked default cache layouts. Windows SDKs/runtimes 8/9/10 and
Edge/Chrome are available; the Windows all-target sequence and actual
browser/RI workflows remain **not run**, not failed or waived. No public
registry fetch, new environment/tool installation, or remote workflow was
used as a substitute.

Phase 7 remains **blocked for final acceptance**. Only evidence documentation
was updated after these passing runs. A local checkpoint commit does not
replace the remaining matrix/browser gates, final-tree verification, or
conductor-managed review. See the updated
[compatibility evidence](../../../../docs/technical/sdk6-compatibility.md)
for separate OS/framework cells and browser prerequisites.

## Original-review M4 identifier mutation inventory (2026-11-19)

This additive inventory concerns **original-review M4 (identifier mutation
ownership)**, not the historical migration phase-4 labels above.
`Sdk6ContractTests.IdentifierMutations.cs` contains independently authored
synthetic JSON and XML; the existing BCL `WireContractAssertions` oracle and
search assertions compare submitted expectations, not captured SDK output or
expected-side Firely models/parsers.

| Test / cases | Authored matrix | Contract and provenance |
|---|---|---|
| `IdentifierMutationLifecyclePreservesLogicalIncludes` | **144 rows**: six kinds × two shapes × R4/R4B/R5 × JSON/XML × HTTP/direct | Kinds are `replace`, `put-create`, `delete-reuse`, `non-owner-update`, `non-owner-delete`, and `remove`. List identifiers use Patient with identifier-only Observation.subject; scalar identifiers use an ordinary collection Bundle with identifier-only Provenance.target. Logical edges contain no literal reference. |
| `IdentifierMutationReentrantWriteCommitsLatestKeysBeforePublication` | **Six direct rows**: R4/R4B/R5 × JSON/XML | A synchronous one-shot callback observes A departed/B current at version 2, then completes B→C; the final read/include requires only C/version 3. A separately attached throwing C→D callback preserves exception identity while a subsequent ordinary read binds D/version 4 and only D resolves. The outer response remains B/version 2; no response is fabricated after the exception. |
| `IdentifierMutationTheoryDataIsNonEmptyAndLabeled` | **One inventory fact** | Requires unique labels and every Cartesian lifecycle/shape/version/boundary/format combination, plus all six direct reentrant combinations. Counts describe authored coverage, not results by themselves. |

Each lifecycle row starts fresh with empty seeds and explicitly **POSTs** the
primary target and referrers, captures generated IDs, and verifies ordinary
read-back and initial inclusion. Collection Bundles are POSTed to the `Bundle`
resource endpoint, not the system batch/transaction endpoint. Provenance has
independently authored recorded/agent content. Only `put-create` uses a known
missing target ID and PUT-as-create (201/version 1); it never seeds that target
first. Only the same-ID sibling setup uses the existing direct explicit-ID
create override, not a claimed HTTP feature.

Expected graph content changes only after successful writes. Generated
ID/version/time bindings require stable identity, version progression and
nondecreasing time; repeated reads/includes reuse exact bindings and check
header relationships. GET and form POST include probes assert exact matches,
includes, modes, self/full URLs, duplicate-free identities and complete content,
not merely counts. Two logical matches must deduplicate their included target.

Controls retain a second list identifier, unchanged-key ownership, destination
collisions, non-owner update/delete, no automatic duplicate promotion, and an
explicit later write claiming an unowned key. Literal inclusion, type-omitted
logical fallback, ordinary identifier and reference-identifier search, empty
system/value components, and unchanged same-ID/key sibling content remain
checked. A stale-If-Match identifier change must retain 412 and the old
content/version/inclusion before the accepted replacement. Deletion alone can
mask a stale key because resolution also requires a stored resource: the
delete/reuse case therefore POSTs a different generated ID with the old key and
requires inclusion of only that new resource.

Explicit JSON `identifier: []` is a **422/structure negative control** with
unchanged stored content; the parser diagnostic includes “An array needs to
have at least one element.” Successful list/scalar clearing uses omission,
with separately authored XML absence expectations. There is no global
empty-array/missing normalization.

### Observed resumed M4 gates

On Windows **10.0.26300 win-x64**, SDK **11.0.100**, Firely **6.9.0**, the
four exact commands reproduced in the
[M4 technical evidence](../../../../docs/technical/sdk6-compatibility.md#original-review-m4-identifier-mutation-ownership-2026-11-19)
ran in order with Release compilation and `--no-restore --verbosity normal`:

| Gate | TFM / runtime | Exit | Executed / passed / failed / skipped |
|---|---|---:|---|
| M4 focused | net11.0 / 11.0.0 | 0 | **151 / 151 / 0 / 0** |
| M4 focused | net10.0 / 10.0.15 | 0 | **151 / 151 / 0 / 0** |
| Complete M4/H1/H2/M1/M2/M3 protection union | net11.0 / 11.0.0 | 0 | **1,558 / 1,558 / 0 / 0** |
| Same complete protection union | net10.0 / 10.0.15 | 0 | **1,558 / 1,558 / 0 / 0** |

Every command reported **zero compiler/analyzer warnings and errors**; no
restore/install was needed. Each focused run discovered 144 lifecycle rows,
six reentrant rows and the fact: 50 theory rows per version, 78 direct/72 HTTP,
75 JSON/75 XML. Each union discovered every required family, including both
direct and controller bundle authorization; its version-labeled rows were
R4 510, R4B 518 and R5 518. H2 contributed 483 top-level results; its passing
isolated facts additionally require 240 extreme rows and 96 concurrency rows
(72 gated conversions, 24 four-pair rendezvous rows), not extra discovery rows.
Normal console verbosity did not print the successful child inventory text.
Negative notification controls emitted invalid-URI, rejected SMTP
authentication and REST InternalServerError diagnostics while passing.

The only dependent existing-test correction changes the stale-If-Match
expectation/comment in
`BundleAuthorizationTests.AuthorizedConditionalAndHeaderSemanticsRemainStable`
from obsolete 500 to exact `412 PreconditionFailed`, enforcing preexisting
M3 status forwarding. Its batch/transaction, authorization, stored-content and
version controls and exact status helper are unchanged; no production M3
behavior changed. Attempt 1's net11.0 union remains historical
1,558 executed / 1,555 passed / three failures at that assertion across
R4/R4B/R5. **No pre-repair protection union was run**; source/history, not those
observed values, justified the narrow synchronization.

These are pre-commit scoped results before this documentation append, not a
post-commit final gate or migration-wide/full-suite qualification. The parent
retains integration and final verification; no other OS, browser, live-state
index rebuild, atomic reader snapshot or performance qualification is claimed.

## Original-review M5: stored ValueSet membership lifecycle

The M5 oracle is the independently authored two-coded-Observation example from
the M5 report and its selected commit-local reconciliation plan. It is not
captured SDK output or a second terminology extractor. The new isolated partial
`Sdk6ContractTests.ValueSetMembership.cs` leaves H1/H2/M1-M4 tests and helpers
unchanged and does not call the M4-specific helpers.

| Test | Authored inventory / boundary | Required observations |
|---|---|---|
| `ValueSetMembershipLifecycleIsFresh` | **24 HTTP/JSON rows**: R4/R4B/R5 × compose/expansion × replace-rename-delete / ordinary-delete / remove-url / empty-membership | Same-URL codeA→codeB replacement; unchanged PUT followed by stale If-Match 412 and unchanged full content/version/membership; U→V rename and deletion; ordinary deletion followed by same-ID PUT-as-create (201/version 1) and another deletion; URL omission/restoration and URL-less deletion; empty replacement/restoration. |
| `ValueSetMembershipCommitsBeforePublication` | **Three direct/JSON rows**, one per version | Create callback can already search U/codeA; the update callback reads version 2/codeB and synchronously searches it, then performs one nested rename to version 3. Exactly two update notifications, one nested write, a separate outer version-2 response, and an ordinary current version-3 read are required. A separate throwing callback preserves exception identity/count while a subsequent read binds committed version 4/codeA under V. Delete callback already sees both keys absent. Every handler is removed in `finally`. |
| `ValueSetMembershipInlineRegistrationKeepsFirstDefinition` | **Three direct-SDK rows**, one per version; each exercises both representations on fresh standalone services and owned tenants | Repeated inline registration retains the first snapshot; qualified/bare/null/empty-system and exact-key/null-code controls; null versus empty URL; expansion precedence, empty-expansion compose fallback, coded-parent recursion and skipped code-less-parent subtree. An actual tenant's inline codeA survives POST of its sole stored codeB definition, but same-ID PUT replaces it. Separate SourceObject-only stored empty-string→replacement→null→empty-string→delete controls use new shared SDK ValueSet objects, never mutate stored/readback objects, and never serialize invalid empty URLs. |
| `ValueSetMembershipTheoryDataIsNonEmptyAndLabeled` | **One inventory fact** | Exact Cartesian lifecycle coverage, all three publication and inline versions, boundaries/formats, unique labels and representation/kind labels. |

These are **31 new cases per framework**, not pass counts merely because they
are authored. Each lifecycle owns unseeded fresh sibling tenants A and B. Each
tenant receives exactly two ordinary POST-created Observations (`status: final`,
codes `codeA` and `codeB`, system `https://example.org/m5/codes`) and one
POST-created ValueSet (`status: active`). Server IDs are validated/bound rather
than seeded or hard-coded. U is `https://example.org/ValueSet/m5`; V is
`https://example.org/ValueSet/m5-renamed`. Compose uses explicit include concepts;
expansion uses explicit contains and timestamp `2026-01-02T03:04:05Z`, with no
compose fallback content. Empty compose-derived membership omits both
representations; empty expansion has that timestamp and total 0, without
contains or compose. No empty JSON arrays or remote terminology are submitted.

Baseline and every mutation/rejection boundary probe A→B→A with GET only, both
`:in`/`:not-in` modifiers, and both U/V (V stays absent in B). For these two coded
Observations an absent canonical means exactly **no positive matches and both
negative matches**, not an error, fallback, or count-only success. The existing
BCL wire oracle checks complete Bundle structure, totals, exact identities,
modes, self/full URLs, duplicate absence and all matched content/metadata.
Expected content comes only from authored requests and validated bindings.
Ordinary readbacks reuse the original B ValueSet/Observation bindings and A
Observation bindings at every boundary, preserving content, versions and exact
timestamps. Callback searches use the same explicit identity/content oracle
through synchronous direct store searches, never sync-over-async replay.

Stored replacements publish a complete snapshot before synchronous callbacks
under the existing resource-store mutation lock. Inline registration and
create-time insertion remain first-publication-only. Lookup stays ordinal and
exact, with no ID/version/alternate-canonical resolution, false for missing
keys/null codes, and bare-code matching for null/empty systems. The empty-string
SDK key remains distinct from null.

This is not duplicate-stored-canonical arbitration, retrospective repair of
historical orphan keys, remote expansion, additional terminology support,
conformance-index repair, cross-key/full-search snapshot isolation, or
concurrency/large-expansion throughput qualification. M5 adds no XML lifecycle
axis, form-POST lifecycle axis, browser, non-Windows, full-suite, or release
qualification. M6 remains parent-gated.

### M5 implementation validation

Canonical commands/conventions came from root `AGENTS.md`, with the plan's
existing-assets `--no-restore` build adaptation and net10.0 repetition. Only
the four commands below were used for build/test iterations, sequentially:

```powershell
dotnet build src\fhir-candle\fhir-candle.csproj --configuration Release --framework net11.0 --no-restore
dotnet build src\fhir-candle\fhir-candle.csproj --configuration Release --framework net10.0 --no-restore
dotnet test --configuration Release --framework net11.0 --no-restore --verbosity normal --filter "FullyQualifiedName~fhir.candle.Tests.Sdk6ContractTests.ValueSetMembership|FullyQualifiedName~fhir.candle.Tests.Sdk6ContractTests.ReferenceChainsAndIncludesStayTenantLocal"
dotnet test --configuration Release --framework net10.0 --no-restore --verbosity normal --filter "FullyQualifiedName~fhir.candle.Tests.Sdk6ContractTests.ValueSetMembership|FullyQualifiedName~fhir.candle.Tests.Sdk6ContractTests.ReferenceChainsAndIncludesStayTenantLocal"
```

Initial implementation iterations: both app builds passed with zero warnings
and errors. The first net11.0 test command stopped at test-source compilation
with CS1513/CS1022 (a misplaced closing brace), zero warnings and no discovery or
execution. After correction it discovered/passed **103/103**, zero failed or
skipped, but reported three new nullable warnings (CS8600 and two CS8602).
Those were corrected with explicit ID/metadata assertions, not suppressions;
the warning-bearing pass is not the final clean gate.

The final four-command sequence on Windows, 2026-11-19, used the supplied
SDK **11.0.100**, installed runtimes **11.0.0 / 10.0.15**, Firely **6.9.0** and
existing assets, with no restore/install:

| Final gate | Exit | Discovered / passed / failed / skipped | Compiler/analyzer warnings / errors |
|---|---:|---|---|
| Release application build, net11.0 | 0 | Not a test run | **0 / 0** |
| Release application build, net10.0 | 0 | Not a test run | **0 / 0** |
| M5 + unchanged reference/membership/tenant union, net11.0 | 0 | **103 / 103 / 0 / 0** | **0 / 0** |
| Same union, net10.0 | 0 | **103 / 103 / 0 / 0** | **0 / 0** |

Both test outputs contain all **24 unique lifecycle tuples**, three publication
rows, three inline-SDK rows, the inventory fact and all **72 unchanged**
`ReferenceChainsAndIncludesStayTenantLocal` rows. Each version contributes
eight lifecycle + one publication + one inline row and 24 existing controls
(34 version-labeled results each); the inventory fact is version-independent.
This is scoped local pre-integration evidence, not the runner's independent
verification or any broader qualification listed above.

## Original-review M6: composite conjunction and same-root matching

**Initial inventory: authored/unexecuted when added.** This is the inventory for
original-review finding M6, not historical migration phase 6. The reviewed synthetic inputs
and exact expected identities come from `scratch/1119-09/bugreport.md`, selected
Approach A (`scratch/1119-09/approach-a.md`), and its implementation plan. They
are not generated from actual responses or SDK models. The planned counts and
predicted failure below describe that initial inventory; subsequent observed
baseline/after-repair results are appended below. Final documented-tree and
post-commit gates remain runner-owned and are not claimed here. Earlier
H1/H2/M1-M5 evidence and historical gate statements above remain unchanged.

### Composite-family conjunction inventory

`SearchFamiliesReturnExpectedIds` retains its 96 rows. Its 12 composite rows
(R4/R4B/R5 × JSON/XML × HTTP/direct) now declare **13 probes**: the original
six, unchanged and in their original order, followed by seven conjunction
controls. Every probe uses the unchanged `AssertSearchProbe`/`SearchRequest`
helpers for GET and form POST, preserving ordered repeated query pairs.

In the table, `{root}` is `SearchCanonicalRoot(tenant.Version)`:
`https://example.test/search/r4`, `/r4b`, or `/r5` with the same authority/path
prefix. `C10` means
`code-value-quantity={root}/codes|weight$10|http://unitsofmeasure.org|mg`;
`C20` substitutes `20` and `Cge10` substitutes `ge10`. These are documentation
shorthand only; requests contain the expanded parameter names and values.

| Probe ID, in execution order | Predicates after `Observation?` | Exact expected identities |
|---|---|---|
| `composite-equal` (retained) | `C10` | `Observation/early` |
| `composite-second` (retained) | `code-value-quantity={root}/codes\|weight$30\|http://unitsofmeasure.org\|g` | `Observation/late` |
| `composite-no-cross-resource-match` (retained) | `code-value-quantity={root}/codes\|pulse$10\|http://unitsofmeasure.org\|mg` | none |
| `composite-negative` (retained) | `C20` | none |
| `composite-or-component` (retained) | `code-value-quantity={root}/codes\|weight,{root}/codes\|pulse$20\|http://unitsofmeasure.org\|mg` | `Observation/twin` |
| `composite-absent-components` (retained) | `_id=missing&C10` | none |
| `composite-then-ordinary-negative` | `C10&_id=doesnotexist` | none |
| `ordinary-then-composite-negative` | `_id=doesnotexist&C10` | none |
| `composite-then-ordinary-positive` | `C10&_id=early` | `Observation/early` |
| `ordinary-then-composite-positive` | `_id=early&C10` | `Observation/early` |
| `composite-then-failing-composite` | `C10&C20` | none |
| `failing-composite-then-composite` | `C20&C10` | none |
| `composite-all-matching` | `C10&Cge10` | `Observation/early` |

Two composite occurrences are two ordered pairs with the same key, not comma-OR
or a dictionary overwrite. Grouped probes remain fail-fast. A baseline failure
at `composite-then-ordinary-negative` would not prove the later probes or that
row's form POST executed. Returning `Observation/early` for that first added GET
is the predicted M6 defect, not an observed result in this authored inventory.

### Isolated same-root inventory

`CompositeComponentsRemainWithinOneRoot` adds **12 rows**, reusing the unchanged
`SearchCases` version/format/boundary matrix. `CompositeRootControlGraph` holds
four ordinary Observations as independently authored complete JSON and XML
literals, seeded through the existing helpers. Each has `status=final` and a
text-only mandatory top-level `code`. Component coding uses `{root}/codes`;
quantities have the literal numeric values below, `unit=mg`,
`system=http://unitsofmeasure.org`, and `code=mg`.

| Identity | Ordered component code / quantity pairs |
|---|---|
| `Observation/component-first` | `weight / 10 mg`, `pulse / 20 mg` |
| `Observation/component-later` | `pulse / 20 mg`, `weight / 10 mg` |
| `Observation/component-split` | `weight / 20 mg`, `pulse / 10 mg` |
| `Observation/component-empty` | no components; the `component` member is omitted |

Each row declares these four probes in order, each through GET and form POST:

| Probe ID | Exact relative path with `{root}` expanded | Exact expected identities |
|---|---|---|
| `component-root-correlated` | `Observation?component-code-value-quantity={root}/codes\|weight$10\|http://unitsofmeasure.org\|mg` | `Observation/component-first`, `Observation/component-later` |
| `component-split-code-present` | `Observation?_id=component-split&component-code={root}/codes\|weight` | `Observation/component-split` |
| `component-split-quantity-present` | `Observation?_id=component-split&component-value-quantity=10\|http://unitsofmeasure.org\|mg` | `Observation/component-split` |
| `component-root-no-match` | `Observation?component-code-value-quantity={root}/codes\|weight$99\|http://unitsofmeasure.org\|mg` | none |

The first probe requires first/later-root success and split/empty-root rejection.
The ordinary split-resource controls independently require both sought values
to exist; the final probe requires failure despite nonempty candidate roots.
Standard built-in component parameters must be applied on every SDK: exact self
links and exact positive/negative membership are required, not ignored-parameter
success. No custom SearchParameters are added.

The unchanged helpers still check complete resource content, bound generated
metadata, total, status/outcome, applied self/full URLs, match modes, exact
identities and duplicate absence. `LoadSearchGraph`, `SearchGraphXml`, all three
raw `search-resources.json` files, their 14-resource/seven-sibling-override
inventories, and the original **384-row** search matrix remain unchanged. The
new root graph is isolated from those fixtures; no provider or assertion changes
are required.

The planned focused gate inventory is **254 tests per framework**: 96 existing
family rows + 12 new root rows + 144 unchanged reverse-chain rows + two existing
inventory facts. The 252 version-labeled rows are 84 per FHIR version, equally
split JSON/XML and HTTP/direct. At complete execution, the seven added family
probes contribute 168 searches and the four root probes contribute 96 searches
per framework; these are requests within rows, not extra discovered tests.
These counts are authored expectations, not pass counts or qualification of
either target framework.

### Observed baseline and after-repair validation

On Windows, 2026-11-20, the runner executed this exact four-command sequence
twice, sequentially: first with the new tests and unchanged production, then
after the surgical composite repair. These are observed pre-documentation
RED/GREEN gates, not final documented-tree or post-commit gates. The supplied
toolchain was .NET SDK **11.0.100**, with runtimes **11.0.0 / 10.0.15**. Application
builds performed their normal restore; no explicit restore or install was run.
Command authority is root `AGENTS.md`; commands ran from
`E:\ai\git\fhir-candle`:

```powershell
dotnet build src\fhir-candle\fhir-candle.csproj --configuration Release --framework net11.0
dotnet build src\fhir-candle\fhir-candle.csproj --configuration Release --framework net10.0
dotnet test src\fhir-candle.Tests\fhir-candle.Tests.csproj --configuration Release --framework net11.0 --no-restore --verbosity normal --filter "FullyQualifiedName~fhir.candle.Tests.Sdk6ContractTests.SearchFamiliesReturnExpectedIds|FullyQualifiedName~fhir.candle.Tests.Sdk6ContractTests.CompositeComponentsRemainWithinOneRoot|FullyQualifiedName~fhir.candle.Tests.Sdk6ContractTests.ReverseChainUsesExistentialMatchesAndDistinctFilters|FullyQualifiedName~fhir.candle.Tests.Sdk6ContractTests.SearchTheoryDataIsNonEmptyAndLabeled|FullyQualifiedName~fhir.candle.Tests.Sdk6ContractTests.SearchRecoveryTheoryDataIsNonEmptyAndLabeled"
dotnet test src\fhir-candle.Tests\fhir-candle.Tests.csproj --configuration Release --framework net10.0 --no-restore --verbosity normal --filter "FullyQualifiedName~fhir.candle.Tests.Sdk6ContractTests.SearchFamiliesReturnExpectedIds|FullyQualifiedName~fhir.candle.Tests.Sdk6ContractTests.CompositeComponentsRemainWithinOneRoot|FullyQualifiedName~fhir.candle.Tests.Sdk6ContractTests.ReverseChainUsesExistentialMatchesAndDistinctFilters|FullyQualifiedName~fhir.candle.Tests.Sdk6ContractTests.SearchTheoryDataIsNonEmptyAndLabeled|FullyQualifiedName~fhir.candle.Tests.Sdk6ContractTests.SearchRecoveryTheoryDataIsNonEmptyAndLabeled"
```

| Observed stage | Command / framework | Exit | Discovered / passed / failed / skipped | Compiler/analyzer warnings / errors |
|---|---|---:|---|---|
| RED, unchanged production | Application build, net11.0 | 0 | Not a test run | **0 / 0** |
| RED, unchanged production | Application build, net10.0 | 0 | Not a test run | **0 / 0** |
| RED, unchanged production | Focused test union, net11.0 | 1 | **254 / 242 / 12 / 0** | **0 / 0** |
| RED, unchanged production | Focused test union, net10.0 | 1 | **254 / 242 / 12 / 0** | **0 / 0** |
| GREEN, after repair | Application build, net11.0 | 0 | Not a test run | **0 / 0** |
| GREEN, after repair | Application build, net10.0 | 0 | Not a test run | **0 / 0** |
| GREEN, after repair | Focused test union, net11.0 | 0 | **254 / 254 / 0 / 0** | **0 / 0** |
| GREEN, after repair | Focused test union, net10.0 | 0 | **254 / 254 / 0 / 0** | **0 / 0** |

For RED, `SearchTester.cs` was byte-identical to the planning baseline, SHA256
`4B57477970532E1271EF4018660B86BC15D4537F84991081ADA1686C6B4EF832`.
All 12 failures per framework were the first added
`composite-then-ordinary-negative` GET, across R4/R4B/R5 × JSON/XML × HTTP/direct.
Each reported `expected matches [], includes [] / actual entries [Observation/early]`.
Thus the predicted M6 false positive was actually observed, not an
authoring/environment failure. The RED test exits reflect those failed
assertions, not compiler/analyzer errors.

Those grouped rows stopped at that GET. RED does **not** prove that later
reversed-order or repeated-composite probes, or the failing probe's form POST,
executed. The independent root controls already passed on the unchanged
baseline, as did the other families, adjacent reverse-chain controls and facts:

| Selected method | Rows per framework | RED passed / failed, each TFM | GREEN passed / failed, each TFM |
|---|---:|---|---|
| `SearchFamiliesReturnExpectedIds` | 96 | 84 / 12 | 96 / 0 |
| `CompositeComponentsRemainWithinOneRoot` | 12 | 12 / 0 | 12 / 0 |
| `ReverseChainUsesExistentialMatchesAndDistinctFilters` | 144 | 144 / 0 | 144 / 0 |
| `SearchTheoryDataIsNonEmptyAndLabeled` | 1 | 1 / 0 | 1 / 0 |
| `SearchRecoveryTheoryDataIsNonEmptyAndLabeled` | 1 | 1 / 0 | 1 / 0 |
| **Total** | **254** | **242 / 12** | **254 / 0** |

Each RED/GREEN test output reconciles the exact unique 254-row set, not just a
summary count. The 252 version-labeled rows are **84 per FHIR version**, with
**21 per version/format/boundary tuple**; the two facts are version-independent.
On RED each version had 80 passes and four failures; on GREEN all 84 passed.
This holds separately for net11.0 and net10.0, with no missing or duplicate rows
and no skips.

After the parameter-local composite repair, all grouped probes completed GET
and form POST: all 13 probes in each of the 12 composite-family rows and all
four probes in each root row. GREEN therefore observes both predicate orders,
positive conjunctions, repeated composite pairs, same-root correlation,
later-root success, split/empty-root rejection and nonempty-root mismatch.
Standard built-in component metadata was applied in all three FHIR versions:
exact self links, positive/negative membership and the unchanged full assertions
passed, rather than accepting ignored parameters.

The added searches per framework are **168 conjunction + 96 root-control
requests**, not additional discovered tests. The original **384-row** matrix,
14-resource fixtures, raw versioned graphs, providers and assertion/replay
helpers remain unchanged. No test expectations were obtained from either
run's outputs or generated SDK models.

### Retained evidence and qualification limits

Evidence is retained in project-local `scratch/1119-09/`:

- `dev-do-preflight.log` and `m6-baseline-patch.log`: preflight and the full
  baseline patch/source state; the runner's `plan.md` also records the supplied
  SDK/runtime versions and observed build summaries.
- `m6-red-build-net11.log`, `m6-red-build-net10.log`,
  `m6-red-test-net11.log` and `m6-red-test-net10.log`: RED command/exit records.
- `m6-red-test-net11-output.log` and `m6-red-test-net10.log`: full native RED
  test output, including version/format/boundary/probe and expected/actual
  identities.
- `m6-green-build-net11.log`, `m6-green-build-net10.log`,
  `m6-green-test-net11.log` and `m6-green-test-net10.log`: full after-repair
  command output.
- `m6-matrix-red-green-v2.log`: exact RED/GREEN row-set reconciliation using
  explicit family/kind, version, format and transport arguments because VSTest
  truncates some display labels. This evidence-only parser correction changed
  no tests, expectations, verification commands or outcomes and required no
  verification rerun.

The initial RED build transcripts retain commands/exits only, **not full native
build output**. Their zero-warning/error results above are the runner-observed
summaries recorded in `plan.md`, not summaries recoverable from those two logs.
The initial net11.0 test transcript also omitted native output; its full returned
output was separately retained in `m6-red-test-net11-output.log`. Subsequent
transcript capture used `Out-Host` without changing the verification commands
or selector.

This documentation slice ran no builds or tests. After this edit the runner
still owns the unchanged four-command Final Verification gate on the exact
final deliverable tree, and its repeat after the verified local phase commit.
Those actual results are to be recorded by the runner in
`scratch/1119-09/plan.md` and retained scratch logs; neither gate is claimed
executed here. The observed RED/GREEN evidence above is focused local Windows
coverage, not full-suite, non-Windows, browser or migration-wide qualification.

## Original-review M7: decimal-number and Quantity approximation (2026-11-20)

This additive repair concerns the original review's M7, not a new SDK-origin
claim. `Sdk6ContractTests.ApproximateSearch.cs` supplies independently authored
raw JSON/XML and literal memberships. It reuses the unchanged public PUT
seeding, `SearchScenario`, `SearchRequest`, and `AssertSearchProbe` helpers;
there is no evaluator-only substitute, expected-side SDK conversion, numeric
normalization, or historical graph/provider/expectation change.

### Settled local policy and corpus

The server's decimal `ap` policy is a **closed, query-centered 10% interval**,
using the magnitude of the request, not the candidate. Thus `ap100.0` includes
90 through 110 and `ap-100.0` includes -110 through -90, endpoints included.
A zero request matches exactly zero; zero candidates do not match nonzero
requests. This is Candle's chosen approximation degree, not a universal FHIR
10% mandate or evidence that Firely SDK 6 caused the defect.

One internal decimal predicate guards zero and opposite signs before
same-sign subtraction. It neither divides nor constructs potentially
overflowing outer endpoints. Decimal-number evaluation selects the existing
whole-request decimal array when present; otherwise only integer-backed
`ap` alternatives are widened losslessly. Consequently `ap0`/`ap0.0` agree
against native decimal nodes. **Standalone integer-spelled non-`ap` requests
against decimal nodes intentionally remain ineligible.** `ap0,eq100` and
its reverse return only zero, whereas `ap0.0,eq100` and its reverse return
zero and hundred because the unchanged parser types the whole list as decimal.
Ignored alternatives retain their indices and cannot contribute default zero.
The existing request-literal self-link echo retains `apbad,ap0` even though
only the valid alternative affects membership.

Quantity still applies the existing units/system gate first. Integer/date
approximation, default prefixes, all other operators and equality rounding,
native missing-scalar checks, parser/dispatch, comparator TODOs and unit
conversion exclusions remain unchanged. This is not a general equality
precision or numeric parse-range repair.

| Test / data source | Exact inventory per framework |
|---|---|
| `ApproximateSearchDecimalNumberReturnsExpectedIds` / `ApproximateSearchDecimalCases` | **84 rows**: seven fresh-graph groups × R4/R4B/R5 × JSON/XML × HTTP/direct |
| `ApproximateSearchQuantityReturnsExpectedIds` / `ApproximateSearchQuantityCases` | **84 rows**, the same complete matrix |
| `ApproximateSearchUnchangedIntegerAndDatePolicies` / `ApproximateSearchPolicyCases` | **12 rows**, the original unchanged 14-resource graph |
| `ApproximateSearchTheoryDataIsNonEmptyAndLabeled` | **One fact**: counts, unique family/group/version/format/transport labels, every Cartesian tuple, declared graph identities, and definition-first insertion |

Each numeric graph first PUTs active `SearchParameter/m7-decimal`, with
version-qualified canonical, base Observation, number type and expression
`Observation.value.ofType(Quantity).value`. Only R5 includes
`processingMode=normal`. Every numeric candidate is an Observation Quantity's
native FHIR decimal, including resource tokens spelled `0`. Values carry
UCUM `mg` code/system except the explicit absent-scalar controls.

| Fresh group | Independently authored candidates and oracle |
|---|---|
| `isolated-zero` | Only `zero=0.0`; requests in order `ap0.0`, `ap1.0`, `ap-1.0`, `ap0`, `ap1`, `ap-1`. Only the two zero requests match `Observation/zero`. Explicit decimal request is first. |
| `positive-window` | `p899=89.9,p900=90,p901=90.1,p905=90.5,p1000=100,p1099=109.9,p1100=110,p1101=110.1,p1110=111`, zero and opposite -100. Both request spellings of ap100 return exactly `p900,p901,p905,p1000,p1099,p1100`. |
| `negative-window` | Reflected values with `n...` IDs, zero and opposite +100. Both spellings of ap-100 return exactly `n900,n901,n905,n1000,n1099,n1100`. |
| `zero-window` | `minus-one=-1,minus-tenth=-0.1,zero=0,plus-tenth=0.1,plus-one=1`; `ap0.0` and `ap0` return only zero. |
| `lexical-prefix-isolation` | `negative-hundred=-100,negative-one=-1,zero=0,one=1,hundred=100,two-hundred=200`. All nine decimal-spelled default/eq/ne/gt/ge/lt/le/sa/eb controls, their integer-spelled counterparts, mixed-prefix lists, number-only invalid alternatives and whole-request decimal typing have literal memberships. Quantity retains integer/decimal non-ap equivalence; malformed Quantity parsing is not broadened. |
| `presence-units` | `zero,hundred,absent,extension-only`. Only scalar values match. Quantity additionally checks unitless, `||mg`, full UCUM matching, wrong `g` code and wrong system for both zero/nonzero request spellings. Number matches the scalar independently of units. |
| `range-safety` | `p78,p79,n78,n79,zero`, magnitudes 78000000000000000000000000000 and 79000000000000000000000000000. Decimal-spelled positive/negative requests return the two same-sign IDs; zero returns only zero. Exact raw content/precision round-trips are retained. |

All abbreviated candidate IDs above are `Observation/<id>`. Retained policy
probes require `multiplebirth=ap3` → Patient alpha/beta,
`birthdate=ap1981` → alpha/gamma, and `birthdate=ap2030` → empty.
Every passing probe completes **both GET and form POST**, asserting successful
searchset status, exact totals/IDs, modes, self/full URLs, no duplicates and
complete authored resource content with reused generated metadata. The
181-test set comprises 180 version-labeled rows (60 per version, 15 per each
of the twelve version/format/boundary tuples) plus the fact. It executes
**2,424 search requests per framework**: 1,032 decimal-number, 1,320 Quantity
and 72 retained-policy requests, not extra discovered tests.

### Observed RED, corrections and reached-probe limits

Canonical root `AGENTS.md` and the phase plan supplied the exact commands
below. All ran synchronously on Windows with the existing .NET 11.0.100 SDK
and net11/net10 assets; there was no install, restore, manifest change or
warning suppression. Results were summarized in memory and returned through
tool output, not redirected into repository log files.

The first observed failing row in the initial net11 run was R4B/XML/HTTP
`presence-units`, actual request:

```text
GET http://localhost/fhir/m7-number-presence-units-r4b/Observation?m7-decimal=ap0.0
```

It returned **500 InternalServerError**, not the expected successful Bundle.
Direct rows threw **System.DivideByZeroException: Attempted to divide by
zero.** HTTP runtime output also reported that exception. The unchanged helper
stopped at status, so no HTTP OperationOutcome body/diagnostics or actual
membership was asserted or retained; this does not establish a failure-body
contract.

Both unchanged-production framework runs discovered 181 tests, **13 passed /
168 failed / zero skipped**. A warning-free net11 confirmation reproduced
the exact same matrix: every one of the fourteen family/group combinations
had twelve failures, covering all versions, formats and boundaries. The
net10 row-by-row evidence agrees:

| First reached failing GET, both families unless noted | Expected IDs | Actual boundary result |
|---|---|---|
| `isolated-zero`: `ap0.0` | zero | Direct divide-by-zero; HTTP 500; membership unavailable |
| `positive-window`: `ap100.0` | p900,p901,p905,p1000,p1099,p1100 | Same; no completed membership |
| `negative-window`: `ap-100.0` | n900,n901,n905,n1000,n1099,n1100 | Same |
| `zero-window`: `ap0.0` | zero | Same |
| `presence-units`: `ap0.0` | zero | Same |
| `range-safety`: `ap78000000000000000000000000000.0` | p78,p79 | Same; RED observed division by zero, **not** an overflow |
| Number `lexical-prefix-isolation`: `ap0,eq100` | zero | Successful 200 Bundle, actual empty entries/total 0 versus expected 1 |
| Quantity `lexical-prefix-isolation`: `ap0,eq100` | zero,hundred | Direct divide-by-zero; HTTP 500 |

Number paths use `Observation?m7-decimal=...`; Quantity uses
`Observation?value-quantity=...`, with `|http://unitsofmeasure.org|mg` on
each alternative, encoded by unchanged `SearchRequest` (pipes `%7C`, comma
`%2C`, colon `%3A`, slash `%2F`). In each lexical row, all eighteen preceding
non-ap probes completed GET **and POST** before this nineteenth GET failed.
No failing GET's POST or later probe ran. All other affected groups stopped
on their first GET, so RED does not establish later signed/lexical/window
memberships or the range-overflow mechanism. All twelve retained-policy rows
completed, as did the fact. Setup PUTs, including near-limit complete content,
succeeded before these search failures.

Two introduced xUnit2031 warnings at new inventory assertions (initial lines
113 and 118) were corrected by using `Assert.Single`'s predicate overload;
they were not baseline production warnings or suppressed diagnostics.
Net10 RED and the repeated net11 RED then had zero warnings/errors.

The first repaired net11 run was **169 passed / 12 failed**, zero skips,
warnings or compiler errors. Only the new number lexical rows failed:
`apbad,ap0` returned the correct zero resource but the authored self-link
expectation wrongly removed `apbad`. Inspection confirmed the unchanged
`GetAppliedQueryString` request-literal early return, also documented for
existing date-list controls. Removing only the two new `AppliedPath` overrides
retains exact self-link checking; no membership, raw assertion, parser or
harness changed. Later invalid-alternative and whole-request-type probes
were not reached by that failed run; GREEN below executes them.

One initial final-union **PowerShell summary wrapper** failed parsing
`foreach (...) { ... } | Format-Table` (“An empty pipe element is not
allowed”), exit 1 **before dotnet ran**: no discovery or tests. Assigning the
loop's result before piping corrected only shell reporting, not the sanctioned
test command or repository code.

### Exact commands and measured GREEN qualification

```powershell
dotnet build src\fhir-candle\fhir-candle.csproj --configuration Release --framework net11.0 --no-restore
dotnet build src\fhir-candle\fhir-candle.csproj --configuration Release --framework net10.0 --no-restore
dotnet test src\fhir-candle.Tests\fhir-candle.Tests.csproj --configuration Release --framework net11.0 --no-restore --verbosity normal --filter "FullyQualifiedName~fhir.candle.Tests.Sdk6ContractTests.ApproximateSearch"
dotnet test src\fhir-candle.Tests\fhir-candle.Tests.csproj --configuration Release --framework net10.0 --no-restore --verbosity normal --filter "FullyQualifiedName~fhir.candle.Tests.Sdk6ContractTests.ApproximateSearch"
dotnet test src\fhir-candle.Tests\fhir-candle.Tests.csproj --configuration Release --framework net11.0 --no-restore --verbosity normal --filter "FullyQualifiedName~Sdk6ContractTests.ApproximateSearch|FullyQualifiedName~Sdk6ContractTests.SearchFamiliesReturnExpectedIds|FullyQualifiedName~Sdk6ContractTests.CompositeComponentsRemainWithinOneRoot|FullyQualifiedName~Sdk6ContractTests.MissingSearchDistinguishesComplexValuesAndPrimitiveExtensions|FullyQualifiedName~Sdk6ContractTests.SearchTheoryDataIsNonEmptyAndLabeled|FullyQualifiedName~Sdk6ContractTests.SearchRecoveryTheoryDataIsNonEmptyAndLabeled|FullyQualifiedName~Sdk6ContractTests.IdentifierMutation|FullyQualifiedName~Sdk6ContractTests.ValueSetMembership|FullyQualifiedName~Sdk6ContractTests.UpdateRejection|FullyQualifiedName~Sdk6ContractTests.SubscriptionEvents|FullyQualifiedName~fhir.candle.Tests.SubscriptionLifecycle|FullyQualifiedName~fhir.candle.Tests.SubscriptionNotificationLifecycleTests|FullyQualifiedName~BundleAuthorization"
dotnet test src\fhir-candle.Tests\fhir-candle.Tests.csproj --configuration Release --framework net10.0 --no-restore --verbosity normal --filter "FullyQualifiedName~Sdk6ContractTests.ApproximateSearch|FullyQualifiedName~Sdk6ContractTests.SearchFamiliesReturnExpectedIds|FullyQualifiedName~Sdk6ContractTests.CompositeComponentsRemainWithinOneRoot|FullyQualifiedName~Sdk6ContractTests.MissingSearchDistinguishesComplexValuesAndPrimitiveExtensions|FullyQualifiedName~Sdk6ContractTests.SearchTheoryDataIsNonEmptyAndLabeled|FullyQualifiedName~Sdk6ContractTests.SearchRecoveryTheoryDataIsNonEmptyAndLabeled|FullyQualifiedName~Sdk6ContractTests.IdentifierMutation|FullyQualifiedName~Sdk6ContractTests.ValueSetMembership|FullyQualifiedName~Sdk6ContractTests.UpdateRejection|FullyQualifiedName~Sdk6ContractTests.SubscriptionEvents|FullyQualifiedName~fhir.candle.Tests.SubscriptionLifecycle|FullyQualifiedName~fhir.candle.Tests.SubscriptionNotificationLifecycleTests|FullyQualifiedName~BundleAuthorization"
```

These six literal commands identify the repeated invocations below; builds
have no test-discovery count. The first three RED invocations precede **all**
production edits.

| Invocation in execution order | Exit | Discovered / passed / failed / skipped | Test time / command elapsed |
|---|---:|---|---|
| Initial focused net11 RED | 1 | 181 / 13 / 168 / 0 | 25.73 s / 37.01 s; two introduced xUnit2031 warnings |
| Focused net10 RED, inventory warning corrected | 1 | 181 / 13 / 168 / 0 | 24.85 s / 57.32 s |
| Confirmed net11 RED, inventory warning corrected | 1 | 181 / 13 / 168 / 0 | 25.13 s / 64.28 s |
| Initial repaired builds net11 / net10 | 0 / 0 | n/a | 27.05 s / 21.14 s; zero warnings/errors |
| First repaired focused net11, new self-link expectation error | 1 | 181 / 169 / 12 / 0 | 25.52 s / 68.28 s |
| Corrected four-command sequence: builds net11 / net10 | 0 / 0 | n/a | 29.28 s / 31.75 s; zero warnings/errors |
| Corrected focused net11 GREEN | 0 | 181 / 181 / 0 / 0 | 25.99 s / 82.92 s |
| Focused net10 GREEN | 0 | 181 / 181 / 0 / 0 | 25.20 s / 68.26 s |
| First bounded final builds net11 / net10 | 0 / 0 | n/a | 13.57 s / 24.84 s; zero warnings/errors |
| Initial net11 union summary-wrapper parse error | 1 | Not invoked; zero tests | n/a |
| Bounded final union net11 | 0 | 1,592 / 1,592 / 0 / 0 | 4.5075 min / 5:16.37 |
| Bounded final union net10 | 0 | 1,592 / 1,592 / 0 / 0 | 4.4512 min / 5:10.13 |

All invocations other than the explicitly identified initial warnings have
zero compiler/analyzer warnings and errors. The successful unions reconcile
these **observed** counts independently on each framework, not just a
successful runner exit:

| Protection family | Discovered and passed per framework |
|---|---:|
| M7 decimal-number / Quantity / retained integer-date / inventory fact | 84 / 84 / 12 / 1 |
| Original `SearchFamiliesReturnExpectedIds` | 96 |
| `CompositeComponentsRemainWithinOneRoot` | 12 |
| `MissingSearchDistinguishesComplexValuesAndPrimitiveExtensions` | 84 |
| Original search inventory / recovery inventory | 1 / 1 |
| M4 `IdentifierMutation*` | 151 |
| M5 `ValueSetMembership*` | 31 |
| M3 `UpdateRejection*` | 64 |
| H2 `SubscriptionEvents*` | 483 |
| M2 `SubscriptionLifecycleTests` / `SubscriptionLifecyclePolicyTests` | 49 / 6 |
| `SubscriptionNotificationLifecycleTests` | 97 |
| H1/M1 `BundleAuthorizationTests` / controller routing tests | 273 / 63 |
| **Total** | **1,592** |

Each of M7's fourteen family/group sets has exactly twelve passing rows.
All twelve version/format/boundary tuples have exactly fifteen passing M7
theory rows, with 60 per FHIR version plus the unversioned fact. Both methods
of every new probe now finish, including exact near-limit raw content,
signed inclusive endpoints, ignored-alternative index guards and unit controls.
H2's existing isolated child-process facts pass as part of the 483 top-level
results; internal child inventories are not extra discovered rows.
Passing negative notification controls emitted invalid-URI, rejected SMTP
authentication and REST InternalServerError messages, not build diagnostics
or test failures.

These measurements precede this documentation append. Final documented-tree
verification is still to be rerun and reported to the parent; no post-commit
gate is claimed here. Parent integration, commit review/identity checks and
post-commit verification remain outstanding. This is bounded Windows local
qualification only: no full suite, other OS, browser, live provider, performance,
general numeric precision/unit conversion or migration-wide acceptance claim.
H1/H2/M1–M6 source/tests and all earlier inventories remain unchanged; M8 is
out of scope.

## M7 remediation R1: represented scale-28 windows (2026-11-20)

This addendum supersedes only the original M7 arithmetic description and
its then-known no-unresolved-bug qualification. All preceding inventories,
measurements, corrections and limits remain historical Phase 1 evidence.
This is a correction to Candle's chosen closed, query-relative 10% policy,
not an SDK-causation claim or a universal FHIR approximation mandate.

### Independent counterexample and exact arithmetic

Source reasoning predicted that `abs(requested) * 0.1m` at requested magnitude
`0.0000000000000000000000000006` rounds the mathematical tolerance `6e-29`
outward to `1e-28`, incorrectly admitting both adjacent represented values.
That prediction was not treated as runtime evidence: both RED commands below
ran with the helper byte-identical to committed
`5208670a3e9c6aee43a6a865fd4197910073957e` (Git blob
`a89e4cc89315af9001bbf315fbbe853b123d8ee1`) before its arithmetic changed.

The independently authored JSON and XML graphs contain these exact scalar
spellings, each in a native decimal Observation Quantity with UCUM mg:

| Graph | Candidate identities and values | Literal operand / independent expected membership |
|---|---|---|
| `scale28-positive` | `Observation/scale28-p5` = `0.0000000000000000000000000005`; `Observation/scale28-p6` = `0.0000000000000000000000000006`; `Observation/scale28-p7` = `0.0000000000000000000000000007`; `Observation/scale28-pfar` = `78000000000000000000000000000` | `ap0.0000000000000000000000000006`; only `Observation/scale28-p6`, total 1 |
| `scale28-negative` | `Observation/scale28-n5` = `-0.0000000000000000000000000005`; `Observation/scale28-n6` = `-0.0000000000000000000000000006`; `Observation/scale28-n7` = `-0.0000000000000000000000000007`; `Observation/scale28-nfar` = `-78000000000000000000000000000` | `ap-0.0000000000000000000000000006`; only `Observation/scale28-n6`, total 1 |

The positive mathematical interval is `[5.4e-28, 6.6e-28]`; its negative
reflection is `[-6.6e-28, -5.4e-28]`. Only the represented center belongs
in either graph. No expected ID is computed from arithmetic, the helper,
SDK conversion or actual responses. There is no zero candidate to mask
this defect with an unrelated zero failure.

After both public RED confirmations, only the helper's final arithmetic
changed. Existing requested-zero and candidate-zero/opposite-sign guards
remain. Same-sign subtraction stays in range; compute
`decimal distance = decimal.Abs(candidate - requested)` and the requested
magnitude. Reject strictly `distance > decimal.MaxValue / 10m` **before**
multiplication; the exact constant quotient is
`7922816251426433759354395033.5`. Equality proceeds, rather than being
rejected. Larger distances cannot satisfy the comparison for any
representable request. Otherwise compare `distance * 10m <= magnitude`
inclusively. This avoids materializing a scale-29 rounded tolerance and
guards multiplication overflow; the same-sign far candidate in each new
graph exercises the rejection.

The old statement that the predicate never divides is superseded **only**
by this exact constant range guard: there is no candidate/request division,
outer-endpoint construction, double conversion, epsilon, exception-as-nonmatch
or numeric framework. Both evaluator callers, parser/dispatch, native decimal
and unit eligibility, lexical/prefix policies, integer/date approximation and
all other equality/precision/conversion exclusions are unchanged. Ordinary
inclusive endpoints and the original range-safety groups remain protected.

### Actually reached RED requests

Both frameworks discovered **49 tests: one inventory fact passed, all 48
theory rows failed, zero skipped**, exit 1. Every graph's definition-first
public PUT and four Observation PUTs succeeded with the unchanged complete
wire/content assertions, including tiny and near-limit decimal values.
`SearchParameter/m7-decimal` still selects
`Observation.value.ofType(Quantity).value`; only R5 has `processingMode=normal`.
The ordinary-decimal operands below therefore exercise native decimal-number
and Quantity paths, not integer-backed surrogate nodes.

Here are the exact four reached request paths. Each is **GET**; prefix with
`http://localhost/fhir/m7-{family}-{graph}-{version}/`, where family is the
literal `decimal-number` or `quantity`, graph is the table's signed graph,
and version is exactly `r4`, `r4b` or `r5` for the tuple below. Both formats
and boundaries use the same path; pipes/colon/slashes are encoded only by
the unchanged `SearchRequest`.

| Key / family / graph | Exact GET path | Expected IDs / total | Actual entry order / total |
|---|---|---|---|
| D+ / decimal-number / scale28-positive | `Observation?m7-decimal=ap0.0000000000000000000000000006` | `[Observation/scale28-p6]` / 1 | `[Observation/scale28-p6, Observation/scale28-p7, Observation/scale28-p5]` / 3 |
| D− / decimal-number / scale28-negative | `Observation?m7-decimal=ap-0.0000000000000000000000000006` | `[Observation/scale28-n6]` / 1 | `[Observation/scale28-n5, Observation/scale28-n6, Observation/scale28-n7]` / 3 |
| Q+ / quantity / scale28-positive | `Observation?value-quantity=ap0.0000000000000000000000000006%7Chttp%3A%2F%2Funitsofmeasure.org%7Cmg` | `[Observation/scale28-p6]` / 1 | `[Observation/scale28-p6, Observation/scale28-p7, Observation/scale28-p5]` / 3 |
| Q− / quantity / scale28-negative | `Observation?value-quantity=ap-0.0000000000000000000000000006%7Chttp%3A%2F%2Funitsofmeasure.org%7Cmg` | `[Observation/scale28-n6]` / 1 | `[Observation/scale28-n5, Observation/scale28-n6, Observation/scale28-n7]` / 3 |

Every cell below records the corresponding reached result on **each**
framework: status **200 OK**, successful searchset, expected total 1 versus
actual 3 and the exact IDs in the keyed table above. This enumerates all
48 reached rows per framework, not an extrapolation from one version.

| Version / format / boundary | D+ | D− | Q+ | Q− |
|---|---|---|---|---|
| R4 / JSON / direct | 200, 1→3 | 200, 1→3 | 200, 1→3 | 200, 1→3 |
| R4 / XML / direct | 200, 1→3 | 200, 1→3 | 200, 1→3 | 200, 1→3 |
| R4 / JSON / HTTP | 200, 1→3 | 200, 1→3 | 200, 1→3 | 200, 1→3 |
| R4 / XML / HTTP | 200, 1→3 | 200, 1→3 | 200, 1→3 | 200, 1→3 |
| R4B / JSON / direct | 200, 1→3 | 200, 1→3 | 200, 1→3 | 200, 1→3 |
| R4B / XML / direct | 200, 1→3 | 200, 1→3 | 200, 1→3 | 200, 1→3 |
| R4B / JSON / HTTP | 200, 1→3 | 200, 1→3 | 200, 1→3 | 200, 1→3 |
| R4B / XML / HTTP | 200, 1→3 | 200, 1→3 | 200, 1→3 | 200, 1→3 |
| R5 / JSON / direct | 200, 1→3 | 200, 1→3 | 200, 1→3 | 200, 1→3 |
| R5 / XML / direct | 200, 1→3 | 200, 1→3 | 200, 1→3 | 200, 1→3 |
| R5 / JSON / HTTP | 200, 1→3 | 200, 1→3 | 200, 1→3 | 200, 1→3 |
| R5 / XML / HTTP | 200, 1→3 | 200, 1→3 | 200, 1→3 | 200, 1→3 |

The first observed failure on both frameworks was D+, R4B/JSON/direct:
`GET http://localhost/fhir/m7-decimal-number-scale28-positive-r4b/Observation?m7-decimal=ap0.0000000000000000000000000006`.
The unchanged assertion reported `System.InvalidOperationException`,
`Assert.Equal() Failure: Values differ`, `Expected: 1`, `Actual: 3`,
with the expected/actual IDs above. All 48 rows failed at this total check.
Direct responses had already passed success/Bundle and the exact
`information` / `success` / `Type search successful` outcome assertion.
HTTP passed status/media/body checks; no separate HTTP OperationOutcome
diagnostic was observed or asserted. Far candidates were absent from the
actual entries. **No failing RED GET reached its subsequent form POST**,
nor the later per-entry full-content assertions; successful setup PUTs
retain their full-content evidence independently.

### Additive inventory and observed GREEN

`ApproximateSearchScale28Cases` projects the existing `ReplayCases` into
two families × two signs × R4/R4B/R5 × JSON/XML × HTTP/direct: **48 uniquely
labeled rows**, each with a fresh default/lenient replay.
`ApproximateSearchScale28WindowReturnsOnlyCenter` executes one literal signed
probe per row. The separate
`ApproximateSearchScale28TheoryDataIsNonEmptyAndLabeled` fact requires all
48 unique labels/Cartesian tuples, 24 per family and per sign, 16 per version,
definition-first seeding, exact four Observation identities plus the number
definition, resource/generated-key agreement and unchanged `SearchIdentity`
for every version/format/sign graph.

These are **49 additional tests**, not edits to the old seven groups,
three providers, 181 tests, probes or inventory fact. New rows reconcile to
12 per family/sign, 24 per format or boundary, and four per each of the
twelve version/format/boundary tuples. GREEN completes both GET and form
POST for every primary probe: **96 added search requests per framework**
(48 of each method), excluding setup. The unchanged helper assertions
require exact successful searchset/total/IDs, modes, self/full URLs, no
duplicates and complete authored raw resources; no wire assertion or numeric
normalization changed. Far candidates are excluded without overflow.

Combined M7 inventory is **230 tests**: original 84 decimal + 84 Quantity +
12 retained-policy rows + one old fact, plus 48 R1 rows + one new fact.
That is 228 theory rows, 76 per version and 19 per tuple, plus two facts.
It executes **2,520 primary search requests per framework**
(unchanged 2,424 + 96). The expected **1,641-test** protection union was
also actually discovered and passed on both frameworks.

Canonical root `AGENTS.md` and the phase plan authorize these exact commands,
executed sequentially from the repository root without restore:

```powershell
dotnet test src\fhir-candle.Tests\fhir-candle.Tests.csproj --configuration Release --framework net11.0 --no-restore --verbosity normal --filter "FullyQualifiedName~fhir.candle.Tests.Sdk6ContractTests.ApproximateSearchScale28"
dotnet test src\fhir-candle.Tests\fhir-candle.Tests.csproj --configuration Release --framework net10.0 --no-restore --verbosity normal --filter "FullyQualifiedName~fhir.candle.Tests.Sdk6ContractTests.ApproximateSearchScale28"
dotnet build src\fhir-candle\fhir-candle.csproj --configuration Release --framework net11.0 --no-restore
dotnet build src\fhir-candle\fhir-candle.csproj --configuration Release --framework net10.0 --no-restore
dotnet test src\fhir-candle.Tests\fhir-candle.Tests.csproj --configuration Release --framework net11.0 --no-restore --verbosity normal --filter "FullyQualifiedName~fhir.candle.Tests.Sdk6ContractTests.ApproximateSearch"
dotnet test src\fhir-candle.Tests\fhir-candle.Tests.csproj --configuration Release --framework net10.0 --no-restore --verbosity normal --filter "FullyQualifiedName~fhir.candle.Tests.Sdk6ContractTests.ApproximateSearch"
dotnet test src\fhir-candle.Tests\fhir-candle.Tests.csproj --configuration Release --framework net11.0 --no-restore --verbosity normal --filter "FullyQualifiedName~Sdk6ContractTests.ApproximateSearch|FullyQualifiedName~Sdk6ContractTests.SearchFamiliesReturnExpectedIds|FullyQualifiedName~Sdk6ContractTests.CompositeComponentsRemainWithinOneRoot|FullyQualifiedName~Sdk6ContractTests.MissingSearchDistinguishesComplexValuesAndPrimitiveExtensions|FullyQualifiedName~Sdk6ContractTests.SearchTheoryDataIsNonEmptyAndLabeled|FullyQualifiedName~Sdk6ContractTests.SearchRecoveryTheoryDataIsNonEmptyAndLabeled|FullyQualifiedName~Sdk6ContractTests.IdentifierMutation|FullyQualifiedName~Sdk6ContractTests.ValueSetMembership|FullyQualifiedName~Sdk6ContractTests.UpdateRejection|FullyQualifiedName~Sdk6ContractTests.SubscriptionEvents|FullyQualifiedName~fhir.candle.Tests.SubscriptionLifecycle|FullyQualifiedName~fhir.candle.Tests.SubscriptionNotificationLifecycleTests|FullyQualifiedName~BundleAuthorization"
dotnet test src\fhir-candle.Tests\fhir-candle.Tests.csproj --configuration Release --framework net10.0 --no-restore --verbosity normal --filter "FullyQualifiedName~Sdk6ContractTests.ApproximateSearch|FullyQualifiedName~Sdk6ContractTests.SearchFamiliesReturnExpectedIds|FullyQualifiedName~Sdk6ContractTests.CompositeComponentsRemainWithinOneRoot|FullyQualifiedName~Sdk6ContractTests.MissingSearchDistinguishesComplexValuesAndPrimitiveExtensions|FullyQualifiedName~Sdk6ContractTests.SearchTheoryDataIsNonEmptyAndLabeled|FullyQualifiedName~Sdk6ContractTests.SearchRecoveryTheoryDataIsNonEmptyAndLabeled|FullyQualifiedName~Sdk6ContractTests.IdentifierMutation|FullyQualifiedName~Sdk6ContractTests.ValueSetMembership|FullyQualifiedName~Sdk6ContractTests.UpdateRejection|FullyQualifiedName~Sdk6ContractTests.SubscriptionEvents|FullyQualifiedName~fhir.candle.Tests.SubscriptionLifecycle|FullyQualifiedName~fhir.candle.Tests.SubscriptionNotificationLifecycleTests|FullyQualifiedName~BundleAuthorization"
```

| R1 invocation | Exit | Discovered / passed / failed / skipped | Test time / dotnet elapsed |
|---|---:|---|---|
| Unchanged-helper RED net11.0 | 1 | 49 / 1 / 48 / 0 | 8.5625 s / 19.17 s |
| Unchanged-helper RED net10.0 | 1 | 49 / 1 / 48 / 0 | 8.7863 s / 23.68 s |
| Corrected application build net11.0 | 0 | n/a | n/a / 11.13 s |
| Corrected application build net10.0 | 0 | n/a | n/a / 17.26 s |
| Focused GREEN net11.0 | 0 | 230 / 230 / 0 / 0 | 30.0366 s / 1:04.41 |
| Focused GREEN net10.0 | 0 | 230 / 230 / 0 / 0 | 29.4071 s / 1:17.09 |
| Bounded protection union net11.0 | 0 | 1,641 / 1,641 / 0 / 0 | 4.4730 min / 5:06.28 |
| Bounded protection union net10.0 | 0 | 1,641 / 1,641 / 0 / 0 | 4.5122 min / 5:12.44 |

All actual dotnet invocations above had zero compiler/analyzer warnings and
errors. Between the builds and focused GREEN, one reporting-wrapper
PowerShell parse error (“The Try statement is missing its Catch or Finally
block”) occurred before dotnet ran: no tests were invoked or discovered.
The overcomplicated nested quoted summary was replaced with a shorter
line-separated, grouped report; the sanctioned commands and repository code
did not change. There was no setup/compile/content failure, product retry,
install/restore, manifest change, warning suppression or assertion relaxation.
Output was captured in memory and reported, not redirected into scratch logs.

Every selected protection family was independently reconciled in each union:
**230, 96, 12, 84, 1, 1, 151, 31, 64, 483, 55, 97, 336** in the exact filter
order above. Each original numeric family/group retained twelve passes;
each new family/sign had twelve passes. All twelve M7 tuples had nineteen
passing theory rows. Passing negative notification controls still emitted
invalid-URI, held-current SMTP authentication rejection and REST
`InternalServerError` diagnostics; these are not compiler diagnostics or
failed tests.

These R1 measurements precede this append. The final documented-tree
four gates (both builds, then both unions) remain to be rerun and returned
to the parent; no post-commit result is claimed here. Parent inspection,
independent verification, staging/commit and identity gates remain its own.
Qualification is bounded local Windows execution, not a full suite,
cross-OS/browser/live-provider/performance or generalized numeric precision
claim. Historical sections, H1/H2/M1–M6 source/tests and all read-only review
inputs remain unchanged; no M8 work is included.
