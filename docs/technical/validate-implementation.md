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

`DoOperation` resolves the validation target in priority order — `Parameters`
body `resource` parameter → bare body resource → instance focus — and returns
**422** when none is present.

If `_inspector` is `null` (the startup init failed), the operation returns a
clear **500** `OperationOutcome` instead of throwing.

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

## Follow-up

This is a v1 **structural** validator. Profile, terminology (binding), and
slicing validation are out of scope and are tracked as a follow-up in
`scratch/0601-01/plan.md`.

## See also

- [`$validate` operation reference](../user/operations/validate.md)
- [Operations and hooks](operations-and-hooks.md)
