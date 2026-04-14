# Build
dotnet build --configuration Release --framework net10.0

# Run all unit tests
dotnet test --configuration Release --framework net10.0 --no-restore --verbosity normal

# Run R4 Patient Search Tests
dotnet test --configuration Release --framework net10.0 --no-restore --verbosity normal --filter "FullyQualifiedName~fhir.candle.Tests.R4TestsPatient.PatientSearch"

# Workflow
- When planning, write plans into a Markdown file in the scratch directory
- Be sure to build when you have made a series of code changes
- Prefer running single tests, and not the whole test suite, for performance (tests are using xUnit and the filter must use the FullyQualifiedName expression)