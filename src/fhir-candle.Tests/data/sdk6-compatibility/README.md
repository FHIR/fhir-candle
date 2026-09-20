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
