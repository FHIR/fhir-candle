# `$test-if-fhir` operation

`$test-if-fhir` reports whether the request body is **structurally parseable as a
FHIR resource**. It is a lightweight probe: it does not validate the content
beyond confirming that it can be parsed into a resource.

> **Scope at a glance.** This only answers "is this parseable FHIR?". For
> structural validation (cardinality, primitive types, regex, narrative), use
> [`$validate`](validate.md).

## Summary

| | |
|-|-|
| Operation name | `$test-if-fhir` |
| Operation version | `0.0.1` |
| FHIR versions | R4, R4B, R5 |
| Canonical URL | `http://ginoc.io/fhir/OperationDefinition/test-if-fhir` |
| Affects state | No |
| HTTP method | `POST` only (no `GET`) |
| Accepts non-FHIR | Yes — the body need not be FHIR (that is the point of the test) |

## Invocation

`$test-if-fhir` is invoked at the **system level** only:

- **System level** — `POST [base]/$test-if-fhir`

It is not available at the type or instance level.

## Parameters

| Name | Use | Cardinality | Type | Notes |
|------|-----|-------------|------|-------|
| `return` | out | `1..1` | `OperationOutcome` | The result of the test. |

The payload to test is supplied directly as the request body; there is no input
`Parameters` wrapper.

## Response semantics

| Situation | HTTP status | Body |
|---|---|---|
| Body is parseable FHIR | `200 OK` | `OperationOutcome` (`success` / `success`, "Content is a structurally-parseable FHIR resource") |
| Request body is empty | `422 Unprocessable Entity` | `OperationOutcome` (`fatal` / `structure`, "Body is empty") |
| Body is not parseable as FHIR | `422 Unprocessable Entity` | `OperationOutcome` (`fatal` / `structure`, "Content is not parseable as FHIR") |

## Examples

The examples below assume the **local default** base URL
`http://localhost:5826/fhir/r4`. If you started the server with the README
Docker quick-start (`docker run -p 8080:5826 ...`), use
`http://localhost:8080/fhir/r4` instead.

### Parseable FHIR

```bash
curl -X POST http://localhost:5826/fhir/r4/\$test-if-fhir \
  -H "Content-Type: application/fhir+json" \
  -d '{ "resourceType": "Patient", "id": "example" }'
```

Response — `200 OK`:

```json
{
  "resourceType": "OperationOutcome",
  "issue": [
    {
      "severity": "success",
      "code": "success",
      "diagnostics": "Content is a structurally-parseable FHIR resource"
    }
  ]
}
```

### Non-FHIR content

```bash
curl -X POST http://localhost:5826/fhir/r4/\$test-if-fhir \
  -H "Content-Type: application/fhir+json" \
  -d '{ "hello": "world" }'
```

Response — `422 Unprocessable Entity`:

```json
{
  "resourceType": "OperationOutcome",
  "issue": [
    {
      "severity": "fatal",
      "code": "structure",
      "diagnostics": "Content is not parseable as FHIR"
    }
  ]
}
```

## See also

- [`$convert`](convert.md) — parse and re-serialize a resource.
- [`$validate`](validate.md) — structural resource validation.
- [Operations index](README.md)
