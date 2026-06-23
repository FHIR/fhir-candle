# `$convert` operation

`$convert` accepts a FHIR resource in the request body, parses it, and returns
the same resource in the response. Combined with HTTP content negotiation
(`Content-Type` on the way in, `Accept` on the way out) this performs **format
conversion** — for example, posting XML and requesting JSON.

> **Scope at a glance.** This is a round-trip parser, not a version translator.
> It parses the submitted resource against the tenant's FHIR version and returns
> it; it does **not** convert *between* FHIR versions (e.g. R4 → R5).

## Summary

| | |
|-|-|
| Operation name | `$convert` |
| Operation version | `0.0.1` |
| FHIR versions | R4, R4B, R5 |
| Canonical URL | `http://hl7.org/fhir/OperationDefinition/Resource-convert` |
| Affects state | No |
| HTTP method | `POST` only (no `GET`) |

## Invocation

`$convert` is invoked at the **system level** only:

- **System level** — `POST [base]/$convert`

It is not available at the type or instance level.

## Parameters

| Name | Use | Cardinality | Type | Notes |
|------|-----|-------------|------|-------|
| `resource` | in | `1..1` | `Resource` | The resource to convert. Supplied as the request body. |
| `return` | out | `1..1` | `Resource` | The resource after conversion (serialized in the requested format). |

To convert formats, set the request `Content-Type` to the input format and the
`Accept` header to the desired output format; the server parses with the former
and serializes with the latter.

## Response semantics

| Situation | HTTP status | Body |
|---|---|---|
| Conversion succeeded | `200 OK` | The parsed resource, serialized in the requested format |
| Request body is empty | `422 Unprocessable Entity` | `OperationOutcome` (`fatal` / `structure`, "Body is empty") |
| Body is not parseable as FHIR | `422 Unprocessable Entity` | `OperationOutcome` (`fatal` / `structure`, "Content is not parseable as FHIR") |

## Examples

The examples below assume the **local default** base URL
`http://localhost:5826/fhir/r4`. If you started the server with the README
Docker quick-start (`docker run -p 8080:5826 ...`), use
`http://localhost:8080/fhir/r4` instead.

### Convert XML to JSON

```bash
curl -X POST http://localhost:5826/fhir/r4/\$convert \
  -H "Content-Type: application/fhir+xml" \
  -H "Accept: application/fhir+json" \
  --data-binary @patient.xml
```

Response — `200 OK` (the same resource, now as JSON):

```json
{
  "resourceType": "Patient",
  "id": "example",
  "active": true
}
```

### Empty body

```bash
curl -X POST http://localhost:5826/fhir/r4/\$convert \
  -H "Content-Type: application/fhir+json"
```

Response — `422 Unprocessable Entity`:

```json
{
  "resourceType": "OperationOutcome",
  "issue": [
    {
      "severity": "fatal",
      "code": "structure",
      "diagnostics": "Body is empty"
    }
  ]
}
```

## See also

- [`$test-if-fhir`](test-if-fhir.md) — check whether a payload is parseable FHIR.
- [`$validate`](validate.md) — structural resource validation.
- [Operations index](README.md)
