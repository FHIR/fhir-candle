# Storage model

All FHIR data in fhir-candle lives **in memory**. The storage model is built
around a small set of interfaces split between the version-independent
`FhirCandle.Common` project and the shared `FhirStore.CommonVersioned`
implementation.

## `IFhirStore`

`IFhirStore` (in `FhirStore.Common/Storage/`) is the central store interface for
a single tenant. It **extends `IReadOnlyDictionary<string, IResourceStore>`**, so
a store *is* a read-only map from resource type name (e.g. `"Patient"`) to that
type's `IResourceStore`.

All FHIR interactions flow through a request/response pair:

```
FhirRequestContext  →  (store)  →  FhirResponseContext
```

`FhirRequestContext` carries the parsed request (method, URL, headers, body,
authorization, etc.); `FhirResponseContext` carries the result (status code,
resource/outcome, serialized body). Operations receive the request context and
produce a response context — see [Operations and hooks](operations-and-hooks.md).

## `IResourceStore`

`IResourceStore` represents the storage for a single resource type. The shared
implementation is the generic `ResourceStore<T>` in
`FhirStore.CommonVersioned`, which compiles once per FHIR version against the
version-specific `Hl7.Fhir.Model` types.

## `IFhirStoreManager` — multi-tenancy

`IFhirStoreManager` (in `src/fhir-candle/Services/`) manages **all tenants**. It
is both an `IHostedService` and an
`IReadOnlyDictionary<string, IFhirStore>` — each tenant is a named store keyed by
its controller name (e.g. `r4`, `r4b`, `r5`).

Because each tenant's store is a version-specific type that shares namespaces
with the others, the manager constructs them through
[extern aliases](assembly-aliases.md):

```csharp
_storesByController.Add(name, new candleR4::FhirCandle.Storage.VersionedFhirStore());
```

## Dependency injection pattern

Services are registered as **singletons** and *also* exposed as hosted services
by forwarding to the same singleton instance with `GetRequiredService`:

```csharp
builder.Services.AddSingleton<IFhirStoreManager, FhirStoreManager>();
builder.Services.AddHostedService<IFhirStoreManager>(sp => sp.GetRequiredService<IFhirStoreManager>());
```

This guarantees the hosted-service lifecycle and the injected singleton are the
**same object**.

## See also

- [Operations and hooks](operations-and-hooks.md)
- [Architecture](architecture.md)
