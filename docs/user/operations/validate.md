# `$validate` operation

`$validate` is a **structural** validation operation. You POST a FHIR resource
(directly, wrapped in a `Parameters` body, or by targeting an existing instance)
and the server returns an `OperationOutcome` describing any structural problems.

> **Scope at a glance.** This is a *v1 structural* validator. It checks what the
> Firely POCO attributes describe — cardinality, primitive types, regex
> constraints, and `FhirXhtml` narrative — recursively. **Profile, terminology
> (binding), and slicing validation are out of scope.** The `mode` and `profile`
> parameters are *accepted but ignored*.

## Summary

| | |
|-|-|
| Operation name | `$validate` |
| Operation version | `0.0.1` |
| FHIR versions | R4, R4B, R5 |
| Canonical URL | `http://hl7.org/fhir/OperationDefinition/Resource-validate` |
| Affects state | No |
| HTTP method | `POST` only (no `GET`) |

## Invocation

`$validate` can be invoked at three levels:

- **System level** — `POST [base]/$validate`
- **Type level** — `POST [base]/{ResourceType}/$validate`
- **Instance level** — `POST [base]/{ResourceType}/{id}/$validate`

Only `POST` is supported; `GET` is not allowed.

## Target resolution

The resource that gets validated is resolved in this priority order:

1. If the request body is a `Parameters` resource with a `resource` parameter,
   the embedded resource is validated.
2. Otherwise, if the request body is a bare (non-`Parameters`) resource, that
   resource is validated.
3. Otherwise, the **instance focus** from the URL (instance-level invocation) is
   validated.
4. If none of the above yields a target, the server returns **422 Unprocessable
   Entity** with an `error` `OperationOutcome` explaining that a target is
   required.

## What is covered

Validation runs Firely's built-in POCO attribute validator recursively over the
target resource and everything it contains. It checks the **attribute-driven
POCO constraints**:

- required **cardinality** (missing required elements),
- **primitive types** (value-type correctness),
- **regex constraints** on primitive values,
- **`FhirXhtml`** narrative well-formedness,
- applied **recursively** to nested elements and contained resources.

## What is *not* covered (limitations)

- **Profile validation** — the resource is not checked against any
  `StructureDefinition` profile.
- **Terminology / binding validation** — coded values are not checked against
  value sets or code systems.
- **Slicing** — slice definitions are not evaluated.
- **Invariants / business rules** beyond what the POCO attributes express.
- The **`mode`** and **`profile`** input parameters are **accepted but ignored**.
  When they are supplied inside a `Parameters` body, the server appends an
  `information`-severity issue to the outcome for each, so callers can see they
  had no effect without reading the `OperationDefinition`.

The follow-up path to profile and terminology validation is tracked in the
[`$validate` implementation note](../../technical/validate-implementation.md).

## Response semantics

Per FHIR convention, validation results are conveyed through `OperationOutcome`
issues, **not** through HTTP status codes. Once a parseable target is found, the
operation returns **200 OK** regardless of whether validation passed or failed.

| Situation | HTTP status | Body |
|---|---|---|
| Validation passed | `200 OK` | `OperationOutcome` with a single `information` "All OK" issue |
| Validation found problems | `200 OK` | `OperationOutcome` with one issue per problem (severity/code from the validator, optional `expression`) |
| `mode` / `profile` supplied | `200 OK` | The above **plus** one `information` "ignored" issue per parameter |
| No target resource | `422 Unprocessable Entity` | `error` `OperationOutcome` requesting a target |
| Model inspector failed to initialize at startup | `500 Internal Server Error` | `error` `OperationOutcome` (see server logs) |

> **Note.** A successful validation is **not** an empty body or a 4xx — it is a
> 200 with an `information` "All OK" issue. If you also send `mode`/`profile`,
> the 200 outcome contains the "All OK" issue **and** the ignored-parameter
> notes together.

## Examples

The examples below assume the **local default** base URL
`http://localhost:5826/fhir/r4`. If you started the server with the README
Docker quick-start (`docker run -p 8080:5826 ...`), use
`http://localhost:8080/fhir/r4` instead.

### 1. Instance-level validate that passes

```bash
curl -X POST http://localhost:5826/fhir/r4/Patient/example/\$validate \
  -H "Content-Type: application/fhir+json"
```

Response — `200 OK`:

```json
{
  "resourceType": "OperationOutcome",
  "issue": [
    {
      "severity": "information",
      "code": "informational",
      "diagnostics": "All OK"
    }
  ]
}
```

### 2. `Parameters` body with an invalid embedded resource

Here a `Patient.gender` carries a value that is not in the required primitive
shape, so the validator reports an `error`.

```bash
curl -X POST http://localhost:5826/fhir/r4/Patient/\$validate \
  -H "Content-Type: application/fhir+json" \
  -d '{
        "resourceType": "Parameters",
        "parameter": [
          {
            "name": "resource",
            "resource": {
              "resourceType": "Patient",
              "gender": "not-a-valid-code"
            }
          }
        ]
      }'
```

Response — `200 OK`:

```json
{
  "resourceType": "OperationOutcome",
  "issue": [
    {
      "severity": "error",
      "code": "invalid",
      "diagnostics": "...validator message describing the offending value...",
      "expression": [ "Patient.gender" ]
    }
  ]
}
```

> The exact `diagnostics` text, `code`, and `expression` come from Firely's
> validator and may vary by FHIR version.

### 3. Body that includes the ignored `mode` / `profile` parameters

```bash
curl -X POST http://localhost:5826/fhir/r4/Patient/\$validate \
  -H "Content-Type: application/fhir+json" \
  -d '{
        "resourceType": "Parameters",
        "parameter": [
          { "name": "mode", "valueCode": "create" },
          { "name": "profile", "valueCanonical": "http://example.org/StructureDefinition/MyPatient" },
          {
            "name": "resource",
            "resource": { "resourceType": "Patient", "id": "example" }
          }
        ]
      }'
```

Response — `200 OK` (note the "ignored" notes alongside "All OK"):

```json
{
  "resourceType": "OperationOutcome",
  "issue": [
    {
      "severity": "information",
      "code": "informational",
      "diagnostics": "All OK"
    },
    {
      "severity": "information",
      "code": "informational",
      "diagnostics": "Parameter 'mode' is currently ignored by this $validate implementation."
    },
    {
      "severity": "information",
      "code": "informational",
      "diagnostics": "Parameter 'profile' is currently ignored by this $validate implementation."
    }
  ]
}
```

## See also

- [`$validate` implementation note](../../technical/validate-implementation.md)
  — internals and the follow-up path to profile/terminology validation.
- [Strict mode (`--strict`)](../strict-mode.md) — strict REST behaviors for
  conformance testing.
- [Operations index](README.md) — all documented operations.
