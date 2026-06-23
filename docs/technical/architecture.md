# Architecture

fhir-candle supports FHIR **R4**, **R4B**, and **R5** from a single codebase. The
core challenge it solves is compiling *the same* FHIR store logic three times —
once per FHIR version — without `#if` directives. It does this with a
**shared-project** pattern.

## Shared-project pattern

The core FHIR store logic lives in **`FhirStore.CommonVersioned`**, a shared
project (`.projitems`) that is *imported* into each version-specific library:

- `FhirCandle.R4`
- `FhirCandle.R4B`
- `FhirCandle.R5`

Each version-specific project imports the shared `.projitems` and references a
different Firely SDK NuGet package (`Hl7.Fhir.R4`, `Hl7.Fhir.R4B`,
`Hl7.Fhir.R5`). The same C# source compiles three times, and
`Hl7.Fhir.Model.*` types resolve to the version-specific types automatically —
**no `#if` directives needed**.

The same pattern applies to the Blazor UI: **`FhirCandle.Ui.Versioned`** shares
Razor components into `FhirCandle.Ui.R4`, `FhirCandle.Ui.R4B`, and
`FhirCandle.Ui.R5`.

> The shared store project lives under `src/FhirStore.CommonVersioned/`
> (`FhirStore.CommonVersioned.projitems`); the shared UI project lives under
> `src/FhirCandle.Ui.Versioned/`.

## Version-independent vs versioned code

- **`FhirStore.Common`** (`FhirCandle.Common`) — version-independent interfaces
  (`IFhirStore`, `IResourceStore`), models, configuration, and search
  definitions.
- **`FhirStore.CommonVersioned`** — the shared implementation: `VersionedFhirStore`,
  `ResourceStore<T>`, operations, search evaluators, and serialization.
- **`fhir-candle`** — the ASP.NET Core host with Blazor Server and a single
  `FhirController` that handles all FHIR REST interactions for every tenant.

## Project dependency graph

```
fhir-candle (app) → FhirCandle.R4/R4B/R5 + FhirCandle.Ui.R4/R4B/R5 + FhirCandle.Common
FhirCandle.R4/R4B/R5 → FhirCandle.Common + Hl7.Fhir.Rx + [shared] FhirStore.CommonVersioned
FhirCandle.Ui.R4/R4B/R5 → FhirCandle.Ui.Common + FhirCandle.Common + [shared] FhirCandle.Ui.Versioned
```

Because the R4/R4B/R5 assemblies share namespaces, the host app and tests
disambiguate them with **extern aliases** — see
[Assembly aliases](assembly-aliases.md).

## See also

- [Assembly aliases](assembly-aliases.md)
- [Storage model](storage-model.md)
- [Operations and hooks](operations-and-hooks.md)
