# `$validate` implementation note

This note describes how the structural `$validate` operation is implemented, for
contributors. For the caller-facing contract, see the
[`$validate` operation reference](../user/operations/validate.md). The
implementation lives in
`src/FhirStore.CommonVersioned/Operations/OpValidate.cs`.

## Cached, version-correct `ModelInspector`

Validation needs a Firely `ModelInspector` for the *correct FHIR version*. Because
`OpValidate` is compiled once per version (see [Architecture](architecture.md)),
it resolves the inspector from the version-specific model assembly:

- A `static readonly ModelInspector? _inspector` is initialized once via
  `ModelInfo.ModelInspector`.
- `typeof(Patient).Assembly` resolves to the concrete version-specific Firely
  model DLL (`Hl7.Fhir.R4` / `R4B` / `R5`), **not** `Hl7.Fhir.Base`, so the
  inspector reports the right FHIR version.

The initialization is wrapped in a helper that **swallows** the (very unlikely)
init failure, logs once to `stderr`, and leaves the field `null` — rather than
letting a `TypeInitializationException` make the entire FHIR type unreachable.

## Guards and target resolution

The existing shared dispatchers (`DoSystemOperation`, `DoTypeOperation`, and
`DoInstanceOperation`) reject **GET `$validate` only** after their existing
operation, level, resource-type, and instance-existence guards succeed. The
check is before `SourceObject` handling, `SourceContent` parsing, interaction
hooks, and `OpValidate.DoOperation`. Even a non-FHIR `SourceObject` or malformed
raw GET body is therefore rejected by the method guard, not with 415.

The dispatcher returns `false`, **405 Method Not Allowed**, no `Resource`,
and exactly one OperationOutcome issue:

- severity: `error`
- code: `not-supported`
- diagnostic: `$validate only supports POST; GET is not allowed.`

It calls `BuildOutcomeForRequest` with explicit
`OperationOutcome.IssueType.NotSupported`; the builder's default `exception`
code is not this contract. The existing operation wrappers serialize the
Outcome and leave `SerializedResource` empty. No Common response fields were
added.

The three HTTP GET operation actions retain their earlier tenant/resource
and authorization checks. Only after store dispatch reports this `$validate`
405 do they set `HeaderNames.Allow` to exactly `POST`, then call the normal
`AddFhirResponse`. Negotiated FHIR JSON/XML is preserved. Default,
`return=representation`, and `return=OperationOutcome` select the error
Outcome; `return=minimal` still selects an empty body but retains `Allow: POST`.
The GET actions still do not read request bodies.

Earlier rejections are not replaced by 405: an unauthorized HTTP request
remains 401, a missing instance remains 404 even with a supplied body, and
unknown operations, unsupported resource types, and missing HTTP tenants
retain their boundary-specific errors. In particular, HTTP early
tenant/resource errors remain plain text rather than invented FHIR outcomes.
An unsupported type URL is not a route for generic direct dispatch (501);
the explicit type/instance operation wrappers retain their resource-type
404 guard.

This is the approved O1 repair of a **pre-existing POST-only contract defect**,
not an SDK6-origin regression or evidence that 405 was historical behavior.
It does not enforce `AllowGet`/`AllowPost` generically or change other
operations. The existing PATCH boundary is also unchanged: HTTP routing
returns 405 with an empty body and GET/POST Allow values, while generic
direct dispatch returns 501.

For **POST**, target resolution and structural validation are unchanged.
`DoOperation` resolves the validation target in priority order — `Parameters`
body `resource` parameter → bare body resource → instance focus — and returns
**422** with an `error`/`invalid` outcome when none is present.

If `_inspector` is `null` (the startup init failed), the operation returns a
clear **500** `OperationOutcome` instead of throwing.

The URL and the body's wire type are different concerns. Use the existing
system (`/$validate`), type (`/Patient/$validate`, for example), or instance
(`/Patient/{id}/$validate`) routes. Each accepts a Parameters **body**.
`/Parameters/$validate` remains **404** because Parameters is not a REST
resource store; it is not a special controller routing bypass.

## POST parsing before validation

For POST, the three existing operation dispatchers (`DoSystemOperation`,
`DoTypeOperation`, and `DoInstanceOperation`) select the existing
`TryDeserializeFhir(..., lenient: true)` path **only for `$validate`**.
That path uses the version-local SDK 6 `OSTRICH` deserializer, allowing the
structural validator to inspect invalid-but-parseable primitives, missing
required elements, and Parameters wrappers. Ordinary create/update/exchange
parsing and other operations still use `BACKWARDSCOMPATIBLE`; no serializer
worker or global tolerance setting is changed.

Malformed POST JSON or XML is a separate pre-operation rejection: the dispatcher
returns **415** with an `error`/`invalid` outcome when no resource can be
parsed. This is not the validator's 200-with-issues result. A bodyless POST
system/type invocation or a parseable POST Parameters body with no target is
**422**. At an existing instance, the same no-target body falls back to the
stored focus; a missing URL instance is rejected by instance dispatch as
**404** before validation.

## "Validation issues are 200, not 4xx"

This is a deliberate design decision following FHIR convention: once a parseable
target exists, validation results are conveyed through `OperationOutcome`
**issues**, not through HTTP status. So both success and validation *failures*
return **200**. Success is reported as a single `information` "All OK" issue. The
accepted-but-ignored `mode` / `profile` parameters are surfaced as additional
`information` issues appended to the same outcome.

The validation call is:

```csharp
target.Validate(_inspector, NarrativeValidationKind.FhirXhtml, validator: null, validateRecursively: true);
```

Issues from `CodedValidationException` are mapped to `OperationOutcome` issues,
carrying severity, code, diagnostics, and (when available) an `expression`
derived from `InstancePath` or `MemberName`.

The SDK-independent compatibility tests pin issue count, severity, code, and
offending element relationships. The synthetic missing Observation.code
expects `error`/`required`; an invalid boolean and non-FHIR XHTML narrative
expect `error`/`value`. These mappings correspond to SDK 6.2.1's
`CodedValidationException` definitions (PVAL105/102, PVAL123/124, and PVAL115),
not expectations generated by parsing a response. Vendor diagnostic prose
is required to be nonempty, not byte-for-byte fixed. Candle's "All OK",
ignored-parameter notes, and no-target message remain exact expectations.

The inspector-init catch/null guard and its `error`/`exception` 500 mapping
remain unchanged. Tests do not mutate its static readonly inspector or add a
failure-injection API; the synthetic corpus does not claim to execute an
actual inspector initialization failure.

## Follow-up

This is a v1 **structural** validator. Profile, terminology (binding), and
slicing validation are out of scope and are tracked as a follow-up in
`scratch/0601-01/plan.md`.

## See also

- [`$validate` operation reference](../user/operations/validate.md)
- [Operations and hooks](operations-and-hooks.md)
