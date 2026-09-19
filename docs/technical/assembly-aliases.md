# Assembly aliases

The `FhirCandle.R4`, `FhirCandle.R4B`, and `FhirCandle.R5` assemblies are all
compiled from the same shared source (see [Architecture](architecture.md)), so
they **share namespaces and type names**. A type like
`FhirCandle.Storage.VersionedFhirStore` therefore exists three times — once per
FHIR version. To reference a specific version's type unambiguously, the host app
and tests use C# **extern aliases**.

## The aliases

| Alias | Assembly |
|-------|----------|
| `candleR4` | `FhirCandle.R4` |
| `candleR4B` | `FhirCandle.R4B` |
| `candleR5` | `FhirCandle.R5` |
| `coreR4` | `Hl7.Fhir.R4` |
| `coreR4B` | `Hl7.Fhir.R4B` |
| `coreR5` | `Hl7.Fhir.R5` |
| `coreR6` (tests only) | `Hl7.Fhir.R6` |

The `candle*` aliases select fhir-candle's versioned store assemblies. The
`core*` aliases select the Firely SDK model assemblies, not the stores. With
SDK 6.2.1, the model files are `Hl7.Fhir.R4.dll`, `Hl7.Fhir.R4B.dll`, and
`Hl7.Fhir.R5.dll`; the historical alias names remain, but the assembly names
have no `.Core` suffix. The existing R6 package alias is test-only and does not
add R6 server support.

## How they are wired

The store aliases are assigned by an MSBuild `AddPackageAliases` target in
`src/fhir-candle/fhir-candle.csproj` and the test project. It runs
`BeforeTargets="ResolveReferences"` and sets the `<Aliases>` metadata on the
matching `ReferencePath`. The host also assigns the model aliases this way
because it receives the SDK assemblies transitively:

```xml
<Target Name="AddPackageAliases" BeforeTargets="ResolveReferences" Outputs="%(PackageReference.Identity)">
  <ItemGroup>
    <ReferencePath Condition="'%(FileName)'=='FhirCandle.R4'">
      <Aliases>candleR4</Aliases>
    </ReferencePath>
    <ReferencePath Condition="'%(FileName)'=='Hl7.Fhir.R4'">
      <Aliases>coreR4</Aliases>
    </ReferencePath>
    <!-- ...corresponding R4B and R5 entries... -->
  </ItemGroup>
</Target>
```

The test project assigns model aliases directly on its `PackageReference`
items instead. Its active `AddPackageAliases` entries only assign store
aliases:

```xml
<PackageReference Include="Hl7.Fhir.R4" Version="6.2.1" Aliases="coreR4" />
<PackageReference Include="Hl7.Fhir.R4B" Version="6.2.1" Aliases="coreR4B" />
<PackageReference Include="Hl7.Fhir.R5" Version="6.2.1" Aliases="coreR5" />
<PackageReference Include="Hl7.Fhir.R6" Version="6.2.1" Aliases="coreR6" />
```

## Using an alias in code

Declare the alias at the top of the file (before `using` directives), then use
the `alias::Namespace.Type` syntax:

```csharp
extern alias candleR4;
extern alias candleR4B;
extern alias candleR5;

// ...

_storesByController.Add(name, new candleR4::FhirCandle.Storage.VersionedFhirStore());
```

See `src/fhir-candle/Services/FhirStoreManager.cs` for a real example.

`src/fhir-candle.Tests/Sdk6DependencyTests.cs` checks the aliased Patient
assemblies, each model inspector's Patient mapping and FHIR version, and raw
store metadata for R4 (4.0.1), R4B (4.3.0), and R5 (5.0.0).

## See also

- [Architecture](architecture.md)
- [Building and testing](building-and-testing.md)
