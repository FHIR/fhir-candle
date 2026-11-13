# Building and testing

## Multi-targeting

The solution targets `net11.0;net10.0` via `src/fhir-candle.props`. CI
is configured to build and test both on Windows, Linux, and macOS.
Use the .NET 11 SDK and run the commands below from the repository root,
as documented in [AGENTS.md](../../AGENTS.md). When iterating locally,
select `net11.0`; repeat relevant checks with `--framework net10.0` for
compatibility. A local result does not establish the other matrix cells.

.NET 8 and 9 reached end of support on November 10, 2026 and are no longer
project targets. See the [Microsoft support policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core).

## Build

```powershell
dotnet build --configuration Release --framework net11.0
```

Or build just the host project:

```powershell
dotnet build src\fhir-candle\fhir-candle.csproj --configuration Release --framework net11.0
```

## Run the server

```powershell
dotnet run --project src\fhir-candle\fhir-candle.csproj --framework net11.0
```

Pass application arguments after `--`. The default launch profile is not an
isolated, three-version compatibility setup. Use the explicit tenant, source,
cache, and RI configurations in the [SDK 6 browser checklist](sdk6-compatibility.md#browser-checklist)
for that verification.

## Test

Run the full suite:

```powershell
dotnet test --configuration Release --framework net11.0 --no-restore --verbosity normal
```

Or scope to the existing test project:

```powershell
dotnet test src\fhir-candle.Tests\fhir-candle.Tests.csproj --configuration Release --framework net11.0 --no-restore --verbosity normal
```

Tests use **xUnit v2 with VSTest**, through `Microsoft.NET.Test.Sdk` and
`xunit.runner.visualstudio`, not Microsoft.Testing.Platform `-class` or
`-method` filters. Use AGENTS.md's `FullyQualifiedName~...` form:

```powershell
dotnet test --configuration Release --framework net11.0 --no-restore --verbosity normal --filter "FullyQualifiedName~fhir.candle.Tests.R4TestsPatient.PatientSearch"
```

Prefer the smallest relevant filter while iterating; combine related names
with `|`. For the UI data-path and cross-version contract cases:

```powershell
dotnet test --configuration Release --framework net11.0 --no-restore --verbosity normal --filter "FullyQualifiedName~fhir.candle.Tests.Sdk6UiContractTests|FullyQualifiedName~fhir.candle.Tests.Sdk6ContractTests"
```

The normal build restores dependencies; these test commands reuse that
restore. No separate lint/format command or new runner is required.
CI builds both targets before running tests with `--no-build` and
`-p:TestTfmsInParallel=false`, avoiding a second build and concurrent
framework test hosts. It also runs the PowerShell release-validator
regressions in `.github/scripts/tests/Test-ReleaseScripts.ps1`.

## Test organization

Tests use xUnit with `IClassFixture<T>`. A fixture class (`R4Tests`, `R4BTests`,
`R5Tests`) creates a store and loads test data in its constructor; nested test
classes share that fixture:

```
R4Tests (fixture — creates store + loads test data)
  ├── R4TestsPatient : IClassFixture<R4Tests>
  ├── R4TestsObservation : IClassFixture<R4Tests>
  ├── R4TestConditionals : IClassFixture<R4Tests>
  └── ...
```

`FhirStoreTests` is cross-version and uses `[MemberData]` for parameterized
tests across R4/R4B/R5. Test data lives in
`src/fhir-candle.Tests/data/{r4,r4b,r5,common}/`.

The SDK 6 consumer-contract corpus also lives in this same test project:
`Sdk6ContractTests` and its domain partials, dependency/oracle controls, and
the package/client/notification/UI contract classes. Raw synthetic fixtures
under `data/sdk6-compatibility/` are copied recursively by the existing
project. The [corpus README](../../src/fhir-candle.Tests/data/sdk6-compatibility/README.md)
maps authored expectations to source contracts. Replays own fresh versioned
stores or isolated hosts and their bindings; these cases do not reuse mutable
legacy store fixtures. BCL JSON/XML assertions are independent of the SDK
serializer, and expectations are not recorded snapshots.

## Final verification is separate

A focused pass is not a full-suite pass, a browser check, or platform evidence.
The [SDK 6 compatibility record](sdk6-compatibility.md) separates the required
local full-suite commands, the existing workflow's all-target sequence, all
six current OS/framework cells, and the manual browser records. UI data-path tests
do not exercise Blazor rendering or user interaction. Keep unexecuted cells
and workflows explicitly pending rather than treating compilation as a pass.

## Delivery

The NuGet tool contains `net11.0` and `net10.0` payloads, each with its own
published `wwwroot`. Startup prefers the selected payload's assets so that
Blazor scripts and component assets are not merged across frameworks.
Publishing uses `dotnet pack` without rewriting the resulting archive.
Publication starts only from a published `v<version>` GitHub Release, after
exact-source CI and qualification of one immutable tool package on all six
OS/framework combinations. Manual release runs validate without publishing.
See [Releasing](releasing.md) for trusted-publishing setup, changelog/tag
preparation, candidate hashes, and retry rules.
Both container workflows build the same Dockerfile for Linux amd64 and arm64, publishing
`net11.0` with the .NET 11 SDK. The standard runtime is
`mcr.microsoft.com/dotnet/aspnet:11.0`; the minimal workflow selects
`mcr.microsoft.com/dotnet/aspnet:11.0-resolute-chiseled-extra` through the
`RUNTIME_IMAGE` build argument. These rolling tags receive servicing updates.
Do not add shell commands to the shared runtime stage: chiseled images have
no shell. The application creates its own per-user FHIR package cache.

The standard image has `<version>` and `latest` tags; the minimal image has
`<version>-minimal` and `minimal` tags. The Kubernetes manifests continue to
consume `ghcr.io/fhir/fhir-candle:latest` with `imagePullPolicy: Always`.
The deployment workflow restarts them after the complete published-release
workflow succeeds, never after validation-only runs; no cluster runtime
installation or endpoint changes are needed.

## See also

- [Architecture](architecture.md)
- [Assembly aliases](assembly-aliases.md)
- [Contributing notes](contributing-notes.md)
- [SDK 6 compatibility](sdk6-compatibility.md)
