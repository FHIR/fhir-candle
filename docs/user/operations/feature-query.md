# `$feature-query` operation

`$feature-query` lets a client ask the server which **capability features** it
supports, and optionally whether a feature has a particular value in a given
context. It implements the feature-query operation from the FHIR Capabilities
(`capstmt`) implementation guide.

## Summary

| | |
|-|-|
| Operation name | `$feature-query` |
| Operation version | `0.0.1` |
| FHIR versions | R4, R4B, R5 |
| Canonical URL | `http://www.hl7.org/fhir/uv/capstmt/OperationDefinition/feature-query` |
| Affects state | No |
| HTTP method | `GET` or `POST` |

## Invocation

`$feature-query` is invoked at the **system level** only:

- **System level** — `GET [base]/$feature-query?param=...` or
  `POST [base]/$feature-query`

It is not available at the type or instance level.

## Parameters

| Name | Use | Cardinality | Type | Notes |
|------|-----|-------------|------|-------|
| `param` | in | `0..*` | `string` | A feature request in the form `feature[@context][(value)]`. |
| `feature` | in | `0..*` | (complex) | A `Parameters` part-based form with `feature`, `context`, and `value` parts. |
| `return` | out | `1..1` | `Parameters` | One `feature` part per request, with the server's answer. |

### Request string format (`param`)

Each `param` value uses the form:

```
feature[@context][(value)]
```

- **feature** (required) — the feature URI.
- **@context** (optional) — a context URI to scope the query.
- **(value)** (optional, in parentheses) — a value to test against.

General patterns:

- *feature alone* — returns the list of values the server supports for that
  feature (may be empty; check `processing-status`).
- *feature + context* — returns the values supported in that context.
- *feature + value* — answers whether all contexts match the supplied value.
- *feature + context + value* — answers whether that context matches the value.

## Response

The response is a `Parameters` resource with one `feature` part per requested
feature. Each `feature` part may contain:

| Part | Meaning |
|------|---------|
| `name` | The feature URI. |
| `context` | Present if a context was supplied (echoes the request). |
| `value` | The supported value(s), or the supplied value when one was given. |
| `matches` | Boolean — present when a value was supplied and processing succeeded. |
| `processing-status` | A code describing how the server handled the request (e.g. `all-ok`, `not-supported`). |

## Response semantics

| Situation | HTTP status | Body |
|---|---|---|
| Query processed | `200 OK` | `Parameters` with the per-feature results; accompanying `OperationOutcome` is `success` ("Feature request query has been processed.") |

> Whether an individual feature is supported is conveyed by its
> `processing-status` part, **not** by the HTTP status code — a successful
> `200` may still report `not-supported` for a given feature.

## Examples

The examples below assume the **local default** base URL
`http://localhost:5826/fhir/r4`. If you started the server with the README
Docker quick-start (`docker run -p 8080:5826 ...`), use
`http://localhost:8080/fhir/r4` instead.

### Query a feature via GET

```bash
curl "http://localhost:5826/fhir/r4/\$feature-query?param=http://hl7.org/fhir/feature/example"
```

Response — `200 OK`:

```json
{
  "resourceType": "Parameters",
  "parameter": [
    {
      "name": "feature",
      "part": [
        { "name": "name", "valueUri": "http://hl7.org/fhir/feature/example" },
        { "name": "processing-status", "valueCode": "all-ok" }
      ]
    }
  ]
}
```

### Test a feature value via a Parameters body

```bash
curl -X POST http://localhost:5826/fhir/r4/\$feature-query \
  -H "Content-Type: application/fhir+json" \
  -d '{
        "resourceType": "Parameters",
        "parameter": [
          {
            "name": "feature",
            "part": [
              { "name": "feature", "valueString": "http://hl7.org/fhir/feature/example" },
              { "name": "value", "valueString": "true" }
            ]
          }
        ]
      }'
```

Response — `200 OK`:

```json
{
  "resourceType": "Parameters",
  "parameter": [
    {
      "name": "feature",
      "part": [
        { "name": "name", "valueUri": "http://hl7.org/fhir/feature/example" },
        { "name": "value", "valueString": "true" },
        { "name": "matches", "valueBoolean": true },
        { "name": "processing-status", "valueCode": "all-ok" }
      ]
    }
  ]
}
```

## See also

- [Operations index](README.md)
