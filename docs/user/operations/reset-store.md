# `$reset-store` operation

`$reset-store` deletes all non-protected resources from the tenant's in-memory
store, returning it to a clean state. This is a testing/development convenience —
it is one of the features fhir-candle deliberately provides that would not be
appropriate in a production server.

> **Scope at a glance.** This **mutates the store** (deletes data). Protected
> resources are never removed. Conformance resources are removed by default but
> can be preserved with the `keep-conformance` parameter.

## Summary

| | |
|-|-|
| Operation name | `$reset-store` |
| Operation version | `0.0.1` |
| FHIR versions | R4, R4B, R5 |
| Canonical URL | `http://ginoc.io/fhir/OperationDefinition/reset-store` |
| Affects state | **Yes** — deletes resources |
| HTTP method | `POST` only (no `GET`) |

## Invocation

`$reset-store` is invoked at the **system level** only:

- **System level** — `POST [base]/$reset-store`

It is not available at the type or instance level.

## Parameters

| Name | Use | Cardinality | Type | Notes |
|------|-----|-------------|------|-------|
| `keep-conformance` | in | `0..1` | `boolean` | `true` keeps conformance resources; `false` (default) deletes them. |
| `return` | out | `1..1` | `OperationOutcome` | The result of the request. |

`keep-conformance` may be supplied either as a **URL query parameter**
(`?keep-conformance=true`) or inside a `Parameters` request body.

## Behavior

The operation removes every resource that is not marked protected. The
`keep-conformance` flag controls whether conformance resources (e.g.
`StructureDefinition`, `CapabilityStatement`, `SearchParameter`) survive the
reset:

- `keep-conformance=false` (default) — all non-protected resources are removed.
- `keep-conformance=true` — all non-protected, **non-conformance** resources are
  removed; conformance resources are retained.

## Response semantics

| Situation | HTTP status | Body |
|---|---|---|
| Store reset (default) | `200 OK` | `OperationOutcome` (`success`, "All non-protected resources have been removed.") |
| Store reset, conformance kept | `200 OK` | `OperationOutcome` (`success`, "All non-protected and non-conformance resources have been removed.") |

## Examples

The examples below assume the **local default** base URL
`http://localhost:5826/fhir/r4`. If you started the server with the README
Docker quick-start (`docker run -p 8080:5826 ...`), use
`http://localhost:8080/fhir/r4` instead.

### Reset everything non-protected

```bash
curl -X POST http://localhost:5826/fhir/r4/\$reset-store
```

Response — `200 OK`:

```json
{
  "resourceType": "OperationOutcome",
  "issue": [
    {
      "severity": "success",
      "code": "success",
      "diagnostics": "All non-protected resources have been removed."
    }
  ]
}
```

### Reset but keep conformance resources

Using the query parameter:

```bash
curl -X POST "http://localhost:5826/fhir/r4/\$reset-store?keep-conformance=true"
```

Or using a `Parameters` body:

```bash
curl -X POST http://localhost:5826/fhir/r4/\$reset-store \
  -H "Content-Type: application/fhir+json" \
  -d '{
        "resourceType": "Parameters",
        "parameter": [
          { "name": "keep-conformance", "valueBoolean": true }
        ]
      }'
```

Response — `200 OK`:

```json
{
  "resourceType": "OperationOutcome",
  "issue": [
    {
      "severity": "success",
      "code": "success",
      "diagnostics": "All non-protected and non-conformance resources have been removed."
    }
  ]
}
```

## See also

- [Loading initial data](../loading-data.md) — seed the store after a reset.
- [Operations index](README.md)
