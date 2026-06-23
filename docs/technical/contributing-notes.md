# Contributing notes

This page captures the in-repo coding conventions. For the contribution
*process*, see [CONTRIBUTING.MD](../../CONTRIBUTING.MD).

## C# style (enforced by `.editorconfig`)

- **Explicit types** — avoid `var` even when the type is apparent.
- **Empty collections** — use `[]` rather than `new List<T>()` or `new()`.
- **Private/static fields** — `_camelCase` (underscore prefix).
- **Constants** — `PascalCase`.
- **File-scoped namespaces** (`namespace X;`).
- **Allman braces** (opening brace on its own new line).
- **Accessibility modifiers** required on all non-interface members (warning
  level).
- **XML doc comments** (`///`) on all public members and most private fields.

## Copyright header

Every source file starts with:

```csharp
// <copyright file="FileName.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>
```

## Namespace naming

- **Library projects** use `FhirCandle.*` (PascalCase) — e.g.
  `FhirCandle.Storage`, `FhirCandle.Models`.
- **The app project** uses `fhir.candle.*` (lowercase) — e.g.
  `fhir.candle.Controllers`, `fhir.candle.Services`.

## Null handling

- Nullable reference types are enabled throughout.
- Use `is not null` / `is null` pattern matching for null checks.
- Use `ArgumentNullException` with `nameof` for guard clauses.
- Use `null!` (null-forgiving) for fields initialized after construction (e.g. in
  an `Init()` method).
- Prefer `string.IsNullOrEmpty()` over `string.IsNullOrWhiteSpace()`.
- Prefer `string.Empty` over `""`.

## See also

- [Building and testing](building-and-testing.md)
- [Architecture](architecture.md)
