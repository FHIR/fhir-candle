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
| `coreR4` | `Hl7.Fhir.R4.Core` |
| `coreR4B` | `Hl7.Fhir.R4B.Core` |
| `coreR5` | `Hl7.Fhir.R5.Core` |

## How they are wired

The aliases are assigned by an MSBuild `AddPackageAliases` target (in
`src/fhir-candle/fhir-candle.csproj` and the test project) that runs
`BeforeTargets="ResolveReferences"` and sets the `<Aliases>` metadata on the
matching `ReferencePath`:

```xml
<Target Name="AddPackageAliases" BeforeTargets="ResolveReferences" Outputs="%(PackageReference.Identity)">
  <ItemGroup>
    <ReferencePath Condition="'%(FileName)'=='FhirCandle.R4'">
      <Aliases>candleR4</Aliases>
    </ReferencePath>
    <!-- ...candleR4B, candleR5, coreR4, coreR4B, coreR5... -->
  </ItemGroup>
</Target>
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

## See also

- [Architecture](architecture.md)
- [Building and testing](building-and-testing.md)
