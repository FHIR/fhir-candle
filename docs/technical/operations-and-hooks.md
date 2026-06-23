# Operations and hooks

fhir-candle extends FHIR behavior through two plugin interfaces that are
discovered by **reflection at store initialization**:

- **`IFhirOperation`** — a FHIR operation (e.g. `$validate`, `$convert`).
- **`IFhirInteractionHook`** — a hook that runs around FHIR interactions.

## Discovery

Implementations are found by reflection when a store initializes. They can live
in:

- `src/FhirStore.CommonVersioned/Operations/` — **shared** across all FHIR
  versions (compiled into each version-specific library), or
- `src/FhirStore.R4/Operations/` (and the R4B/R5 equivalents) — **version
  specific**.

Each operation declares the FHIR versions it supports and any FHIR package it
requires, so the store only wires up operations that are valid for that tenant.

## Operation conventions

- Operation classes are named with an **`Op` prefix** — e.g. `OpValidate`,
  `OpConvert`, `OpResetStore`.
- They implement `IFhirOperation` and expose
  `bool DoOperation(... out FhirResponseContext opResponse)`, returning success
  or failure and writing the response to the `out` parameter.
- Capability flags on the operation (`AllowGet`, `AllowPost`, `AllowSystemLevel`,
  `AllowResourceLevel`, `AllowInstanceLevel`, `AffectsState`, `RequiresPackage`,
  `SupportedResources`, …) tell the store how and where the operation may be
  invoked.
- `GetDefinition(fhirVersion)` returns the `OperationDefinition` advertised in the
  `CapabilityStatement`.

`src/FhirStore.CommonVersioned/Operations/OpValidate.cs` is a complete reference
example; its behavior is documented for callers in the
[`$validate` operation reference](../user/operations/validate.md) and for
contributors in the
[`$validate` implementation note](validate-implementation.md).

## See also

- [Storage model](storage-model.md)
- [`$validate` implementation note](validate-implementation.md)
