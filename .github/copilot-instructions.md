# Copilot Instructions for fhir-candle

fhir-candle is a small in-memory FHIR server for testing and development. It supports FHIR R4, R4B, and R5 via multi-tenant endpoints. Built with C# / .NET, Blazor Server UI with FluentUI components.

## Build and Test

```bash
# Build (multi-targets net10.0, net9.0, net8.0)
dotnet build --configuration Release --framework net10.0

# Run all tests
dotnet test --configuration Release --framework net10.0 --no-restore --verbosity normal

# Run a single test (xUnit — use FullyQualifiedName filter)
dotnet test --configuration Release --framework net10.0 --no-restore --verbosity normal \
  --filter "FullyQualifiedName~fhir.candle.Tests.R4TestsPatient.PatientSearch"

# Run the server locally
dotnet run --project src/fhir-candle/fhir-candle.csproj
```

Prefer running single tests over the full suite for performance. Tests use xUnit with `FullyQualifiedName` filter expressions.

## Architecture

### Shared Project Pattern (Key Design)

The core FHIR store logic lives in **`FhirStore.CommonVersioned`**, a shared project (`.shproj`) that gets compiled into each version-specific library (`FhirStore.R4`, `FhirStore.R4B`, `FhirStore.R5`). Each version-specific project imports the shared `.projitems` and references a different Firely SDK NuGet package (`Hl7.Fhir.R4`, `Hl7.Fhir.R4B`, `Hl7.Fhir.R5`). The same C# code compiles three times — `Hl7.Fhir.Model.*` types resolve to version-specific types automatically. No `#if` directives needed.

The same pattern applies to UI: **`FhirCandle.Ui.Versioned`** shares Razor components into `Ui.R4`, `Ui.R4B`, `Ui.R5`.

### Assembly Aliases

Since R4/R4B/R5 assemblies share namespaces, the main app and tests use **extern aliases** (`candleR4`, `candleR4B`, `candleR5`) set via MSBuild `AddPackageAliases` targets. Code that references version-specific stores must use the alias:

```csharp
extern alias candleR4;
// ...
new candleR4::FhirCandle.Storage.VersionedFhirStore();
```

### Project Dependency Graph

```
fhir-candle (app) → FhirCandle.R4/R4B/R5 + FhirCandle.Ui.R4/R4B/R5 + FhirCandle.Common
FhirCandle.R4/R4B/R5 → FhirCandle.Common + Hl7.Fhir.Rx + [shared] FhirStore.CommonVersioned
FhirCandle.Ui.R4/R4B/R5 → FhirCandle.Ui.Common + FhirCandle.Common + [shared] FhirCandle.Ui.Versioned
```

- **`FhirStore.Common`** (`FhirCandle.Common`): Version-independent interfaces (`IFhirStore`, `IResourceStore`), models, configuration, search definitions
- **`FhirStore.CommonVersioned`**: Shared implementation — `VersionedFhirStore`, `ResourceStore<T>`, operations, search evaluators, serialization
- **`fhir-candle`**: ASP.NET Core host with Blazor Server, single `FhirController` for all FHIR REST interactions

### Key Interfaces

- **`IFhirStore`** (`FhirStore.Common/Storage/`): Central store interface. Extends `IReadOnlyDictionary<string, IResourceStore>`. All FHIR interactions use `FhirRequestContext` → `FhirResponseContext`.
- **`IFhirStoreManager`** (`fhir-candle/Services/`): Multi-tenant manager. `IHostedService` + `IReadOnlyDictionary<string, IFhirStore>`. Each tenant is a named store keyed by controller name.

### Plugin Discovery

Operations (`IFhirOperation`) and interaction hooks (`IFhirInteractionHook`) are discovered via reflection at store initialization. They live in `FhirStore.CommonVersioned/Operations/` (shared) or `FhirStore.R4/Operations/` (version-specific). Operations declare their FHIR version support and required packages.

### DI Pattern

Services are registered as singletons and also as hosted services via `GetRequiredService` forwarding:

```csharp
builder.Services.AddSingleton<IFhirStoreManager, FhirStoreManager>();
builder.Services.AddHostedService<IFhirStoreManager>(sp => sp.GetRequiredService<IFhirStoreManager>());
```

### Test Organization

Tests use xUnit with `IClassFixture<T>`. Fixture classes (`R4Tests`, `R4BTests`, `R5Tests`) create a store in their constructor. Nested classes share the fixture:

```
R4Tests (fixture — creates store + loads test data)
  ├── R4TestsPatient : IClassFixture<R4Tests>
  ├── R4TestsObservation : IClassFixture<R4Tests>
  ├── R4TestConditionals : IClassFixture<R4Tests>
  └── ...
```

`FhirStoreTests` is cross-version — uses `[MemberData]` for parameterized tests across R4/R4B/R5. Test data lives in `src/fhir-candle.Tests/data/{r4,r4b,r5,common}/`.

## Conventions

### C# Style (enforced by `.editorconfig`)

- **Explicit types** — avoid `var` even when the type is apparent
- **Private/static fields**: `_camelCase` (underscore prefix)
- **Constants**: `PascalCase`
- **File-scoped namespaces** (`namespace X;`)
- **Allman braces** (opening brace on new line)
- **Accessibility modifiers** required on all non-interface members (warning level)
- **XML doc comments** (`///`) on all public members and most private fields
- **Copyright header** on every file:
  ```csharp
  // <copyright file="FileName.cs" company="Microsoft Corporation">
  //     Copyright (c) Microsoft Corporation. All rights reserved.
  //     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
  // </copyright>
  ```

### Namespace Naming

- Library projects: `FhirCandle.*` (PascalCase) — e.g., `FhirCandle.Storage`, `FhirCandle.Models`
- App project: `fhir.candle.*` (lowercase) — e.g., `fhir.candle.Controllers`, `fhir.candle.Services`

### Null Handling

- Nullable reference types enabled throughout
- `is not null` / `is null` pattern matching for null checks
- `ArgumentNullException` with `nameof` for guard clauses
- `null!` (null-forgiving) for fields initialized after construction (e.g., in `Init()`)
- `string.IsNullOrEmpty()` preferred over `string.IsNullOrWhiteSpace()`
- `string.Empty` preferred over `""`

### Operation Classes

Named with `Op` prefix (e.g., `OpConvert`, `OpResetStore`). Implement `IFhirOperation` with `bool DoOperation(...)` returning success/failure and `out FhirResponseContext`.

### Multi-targeting

The solution targets `net10.0;net9.0;net8.0` via `src/fhir-candle.props`. CI tests on all three. When building locally, specify `--framework net10.0`.
