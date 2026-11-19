# Build
dotnet build --configuration Release --framework net11.0

# Run all unit tests
dotnet test --configuration Release --framework net11.0 --no-restore --verbosity normal

# Run R4 Patient Search Tests
dotnet test --configuration Release --framework net11.0 --no-restore --verbosity normal --filter "FullyQualifiedName~fhir.candle.Tests.R4TestsPatient.PatientSearch"

# Theme adapter JavaScript
Requires Node.js 24 or later (locally verified with 24.22.1), with no npm
install. Executes the actual script with isolated browser-boundary doubles,
not a browser. Ordinary .NET tests do not require Node.

```powershell
node --test .github\scripts\tests\candle-theme.test.cjs
```

# Workflow
- When planning, write plans into a Markdown file in the scratch directory
- Be sure to build when you have made a series of code changes
- Prefer running single tests, and not the whole test suite, for performance (tests are using xUnit and the filter must use the FullyQualifiedName expression)