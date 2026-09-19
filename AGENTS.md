# AGENTS.md

Repository conventions for automated agents working in **FHIR/fhir-candle**.
The `.github\skills\dev-*` skills read this file before naming build, test,
or lint commands.

**Precedence.** This file records commands and development invariants.
`README.md` and `docs\` explain configuration, operation, and design rationale.
If this file contradicts the repository's build metadata, the metadata wins:
correct this file rather than inventing a command. Keep command changes
consistent with `.github\copilot-instructions.md` and `CLAUDE.md`.

## What this repository is

fhir-candle is a small, in-memory FHIR server for testing and development,
not production workloads. It supports FHIR R4, R4B, and R5 through
multi-tenant endpoints, with an ASP.NET Core host and Blazor Server UI
using FluentUI. Fast startup, dynamic configuration, and no database-backed
persistence are deliberate design choices.

## Repository layout

| Path | Contents |
|-|-|
| `fhir-candle.sln` | Main solution |
| `src\fhir-candle\` | ASP.NET Core host, controllers, and hosted services |
| `src\FhirStore.Common\` | Version-independent interfaces, models, and configuration |
| `src\FhirStore.CommonVersioned\` | Shared store implementation compiled into each FHIR-version library |
| `src\FhirStore.R4\`, `src\FhirStore.R4B\`, `src\FhirStore.R5\` | Version-specific libraries and Firely SDK references |
| `src\FhirCandle.Ui.Common\`, `src\FhirCandle.Ui.Versioned\` | Common and shared versioned UI components |
| `src\FhirCandle.Ui.R4\`, `src\FhirCandle.Ui.R4B\`, `src\FhirCandle.Ui.R5\` | Version-specific UI libraries |
| `src\fhir-candle.Tests\` | xUnit tests and fixtures under `data\` |
| `src\fhir-candle.props`, `src\fhir-candle-ui.props` | Shared build and UI dependency settings |
| `fhirData\` | Resources packaged with the application |
| `docs\user\`, `docs\technical\` | User and contributor documentation |
| `.github\skills\`, `.github\agents\` | Shared dev skills and agent definitions |
| `scratch\` | Ignored local development slots; never committed |

## Toolchain pins

- `src\fhir-candle.props` sets C# **14.0**, targets
  **net10.0;net9.0;net8.0**, and enables nullable reference types and
  implicit usings. Use the .NET 10 SDK for local development.
- There is no checked-in `global.json` pinning an exact SDK patch.
  `.github\workflows\build-and-test.yml` installs .NET 10, 9, and 8 and
  builds/tests on Windows, Linux, and macOS.
- NuGet versions are explicit in project files and shared props; there is
  no checked-in central package manifest or package lock file.
- There is no checked-in .NET local-tool manifest or separate tool-restore
  step.
- No repository-wide `TreatWarningsAsErrors` or `EnforceCodeStyleInBuild`
  setting is declared. Do not assume editor suggestions are build errors.
  Do not introduce new warnings or add blanket diagnostic suppressions.

## Build

Run commands from the repository root. The documented local build is:

```powershell
dotnet build --configuration Release --framework net10.0
```

Scoped to the application and its dependencies:

```powershell
dotnet build src\fhir-candle\fhir-candle.csproj --configuration Release --framework net10.0
```

Build after a series of code changes. Success requires zero errors; a
warning-free baseline is not documented. Investigate diagnostics and compare
with `HEAD` before calling them regressions.

CI covers all three target frameworks by omitting `--framework`. Changes to
shared versioned code must remain compatible with all three Firely SDKs,
not merely the first version compiled locally.

## Test

Tests use **xUnit v2 with VSTest**, through `Microsoft.NET.Test.Sdk` and
`xunit.runner.visualstudio` in
`src\fhir-candle.Tests\fhir-candle.Tests.csproj`. Use
`--filter "FullyQualifiedName~..."`; these are not Microsoft.Testing.Platform
`-class` or `-method` filters.

### Full suite

```powershell
dotnet test --configuration Release --framework net10.0 --no-restore --verbosity normal
```

### Scoped - one project

```powershell
dotnet test src\fhir-candle.Tests\fhir-candle.Tests.csproj --configuration Release --framework net10.0 --no-restore --verbosity normal
```

### Focused - one class or one test

```powershell
dotnet test --configuration Release --framework net10.0 --no-restore --verbosity normal --filter "FullyQualifiedName~fhir.candle.Tests.R4TestsPatient.PatientSearch"
```

**Prefer the smallest command that covers the change.** Combine related
filters in one runner invocation and escalate only when focused results
require it. The normal build restores packages; the test commands above
reuse that restore.

Version-specific tests use `IClassFixture<T>` with `R4Tests`, `R4BTests`,
and `R5Tests` fixtures. `FhirStoreTests` covers multiple FHIR versions with
`MemberData`. Test data lives under `src\fhir-candle.Tests\data\` and is
copied by the test project; do not replace it with machine-specific paths.

## Lint / format

No separate lint or format command is documented or configured in the
build/test workflow. Use the build for compiler/analyzer diagnostics and
follow `.editorconfig`; many style settings are suggestions or silent.
Do not introduce a formatter, analyzer package, or new lint gate merely to
complete a task.

## Run

The application is multi-targeted, so select the local target framework:

```powershell
dotnet run --project src\fhir-candle\fhir-candle.csproj --framework net10.0
```

Pass application arguments after `--`. See `docs\user\getting-started.md`
and the other user reference pages for tenant, data-loading, and runtime
configuration options.

## Code style

`.editorconfig` and `.github\copilot-instructions.md` contain the detailed
rules. Match surrounding code and avoid unrelated formatting.

- Four-space indentation by default; XML/project/build configuration files
  use two spaces. Use a final newline, trim trailing whitespace, and follow
  the configured CRLF endings except for shell scripts, which use LF.
- Prefer explicit C# types, not `var`, including when the type is apparent.
- Use Allman braces, `_camelCase` private/static fields, PascalCase
  constants, and explicit accessibility on non-interface members.
- The Copilot guidance calls for file-scoped namespaces; the current
  `.editorconfig` still has `block_scoped:silent`. Preserve surrounding
  style rather than converting existing files as an unrelated change.
- Use XML documentation for public members and follow existing field
  documentation. New source files carry the Microsoft Corporation MIT
  copyright header shown in `.github\copilot-instructions.md`.
- Use `is null` / `is not null`, `ArgumentNullException` with `nameof`,
  `string.Empty`, and `string.IsNullOrEmpty()` consistently with existing
  code. Nullable reference types are enabled.
- Library namespaces use `FhirCandle.*`; host namespaces use `fhir.candle.*`.

### Architectural invariants

- Keep version-independent contracts in `FhirStore.Common`. Shared store
  logic belongs in `FhirStore.CommonVersioned`, whose `.projitems` are
  imported by the R4, R4B, and R5 projects. The same source resolves against
  each project's Firely model types; do not duplicate shared fixes across
  version-specific libraries.
- The UI follows the same shared-project pattern through
  `FhirCandle.Ui.Versioned`. Changes must work for all importing UI projects.
- Preserve assembly aliases such as `candleR4`, `candleR4B`, and `candleR5`,
  and the `AddPackageAliases` targets. They disambiguate assemblies with
  identical namespaces.
- Preserve the `FhirRequestContext` / `FhirResponseContext` interaction
  contracts and named-tenant routing through the store manager.
- Operations and interaction hooks are discovered by reflection. Operation
  classes use the `Op` prefix and implement `IFhirOperation`; retain their
  FHIR-version and package requirements.
- Singleton hosted services must forward the existing singleton through
  `GetRequiredService`, rather than creating a second service instance.

## Commit conventions

- Prefer concise conventional commits: `<type>(<scope>): <subject>`, with
  an optional scope and an imperative subject. Recent history uses `fix`,
  `test`, and `docs`; no exhaustive allowed-type list is declared.
- Keep one logical change per commit. Address review feedback in additional
  commits rather than amending, as required by `CONTRIBUTING.MD`.
- Include the required trailer:

  ```text
  Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>
  ```

- When GitHub integration is enabled and a slot is issue-bound, `dev-do`
  also adds `Issue: #N`. Disabled integration or an unbound slot adds none.
- Agents do not push or open pull requests unless explicitly asked.
  Refreshing the dev skills never stages, commits, or pushes.

## GitHub Integration

Publishing is opt-in and remains disabled. The other settings are resolved
as `n/a`, not missing configuration. Preserve text outside the managed
block; `dev-setup`, `dev-issue`, and `dev-pr-open` update it only in place.

<!-- >>> dev-* github integration (managed by dev-* skills) >>> -->
| Setting | Value |
|-|-|
| Enabled | no |
| Repository | n/a |
| Label — feature request | n/a |
| Label — bug report | n/a |
| Label — docs-only (additive) | n/a |
| Changelog file | n/a |
| Changelog entry format | n/a |
| PR opens as draft | n/a |
<!-- <<< dev-* github integration (managed by dev-* skills) <<< -->

## Scratch / slot convention

Use `scratch\<MMDD>-<##>\`, with the local month/day and a zero-padded
two-digit slot number. A slot can contain `featurerequest.md`,
`bugreport.md`, `approach-a.md`, `approach-b.md`, `approach-c.md`,
`approach.md`, `plan.md`, and `analysis.md`, as authored by the dev skills.

The existing `/scratch` rule in `.gitignore` ignores the workspace. Plans
and other slot artifacts are never staged or committed. No implementation
phase may declare a scratch path as an owned source path; `plan.md` is a
control file, not a deliverable to commit.

## Agent guardrails

- Read this file before proposing build, test, or lint commands. Never
  invent a command; identify missing documentation explicitly.
- Preserve unrelated worktree changes. Do not use destructive git commands
  or amend commits without explicit permission.
- Build after code changes and prefer targeted tests. Do not add new
  build, lint, or test tooling without being asked.
- Keep skill and agent copies identical to the canonical source in
  `C:\ai\git\devskills`; refresh them through its `dev-setup` workflow.
  Repository-specific guidance belongs here, not inside copied skills.
- Prefer built-in `explore` for mechanical discovery and `task` for
  documented command execution; reserve reasoning agents for judgment.
  Keep delegation bounded and avoid duplicating a delegated investigation.
- Subagents follow the policy below. Do not pin a `model:` in the shared
  agent definitions.

### Subagent model policy

Preserve the conservative pre-policy default: inherit the spawning agent's
model configuration rather than choosing a new mechanical-tier model.

| Setting | Value |
|-|-|
| Policy | uniform |
| Mechanical-tier model | n/a |

This is a resolved default, not an instruction to re-prompt on each run.
A switch to `tiered` requires an explicitly selected mechanical-tier model.
