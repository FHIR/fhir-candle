# Building and testing

## Multi-targeting

The solution targets `net10.0;net9.0;net8.0` via `src/fhir-candle.props`. CI
tests on all three. When building or testing locally, specify a single framework
to keep things fast — the examples below use `net10.0`.

## Build

```bash
dotnet build --configuration Release --framework net10.0
```

Or build just the host project:

```bash
dotnet build src/fhir-candle/fhir-candle.csproj -c Release --framework net10.0
```

## Run the server

```bash
dotnet run --project src/fhir-candle/fhir-candle.csproj
```

## Test

Run the full suite:

```bash
dotnet test --configuration Release --framework net10.0 --no-restore --verbosity normal
```

Tests use **xUnit**. To run a single test, filter on its **fully-qualified
name** (xUnit requires `FullyQualifiedName` filter expressions):

```bash
dotnet test --configuration Release --framework net10.0 --no-restore --verbosity normal \
  --filter "FullyQualifiedName~fhir.candle.Tests.R4TestsPatient.PatientSearch"
```

> Prefer running a single test over the full suite while iterating — it is much
> faster.

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

## See also

- [Architecture](architecture.md)
- [Assembly aliases](assembly-aliases.md)
- [Contributing notes](contributing-notes.md)
