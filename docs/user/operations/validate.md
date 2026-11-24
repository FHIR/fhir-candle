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

### GET rejection and earlier guards

After the existing route, authorization, operation, and resource guards
succeed, GET on any of the three operation levels returns **405 Method Not
Allowed**, including when an instance target or request body is available.
The HTTP response has exactly **`Allow: POST`** and an OperationOutcome with
one issue:

- severity: `error`
- code: `not-supported`
- diagnostic: `$validate only supports POST; GET is not allowed.`

The outcome uses the negotiated FHIR JSON or XML format. The default response,
`Prefer: return=representation`, and `Prefer: return=OperationOutcome` return
that outcome. `Prefer: return=minimal` retains the existing empty-body behavior
and still sends `Allow: POST`. GET does not parse or validate a submitted body,
run operation hooks, or change the target's content or metadata.

The method rejection does **not** override earlier guards. Missing instances
remain 404 even when a body is supplied; `/Parameters/$validate` is still an
unsupported resource URL (404), as are unknown resource types. Unknown
operations and missing tenants retain their existing 404 responses.
Authorization rejection remains 401 before operation dispatch, including
before the instance-existence check. Early controller tenant/resource errors
retain their existing plain-text representation, not a newly negotiated
OperationOutcome.

This rejection is specific to GET `$validate`, not a change to other
operations' method enforcement. Other unsupported verbs retain their
existing routing behavior; for example PATCH on these operation routes is
405 with an empty body and the router's existing GET/POST Allow values.

## Target resolution

For POST, the resource that gets validated is resolved in this priority order:

1. If the request body is a `Parameters` resource with a `resource` parameter,
   the embedded resource is validated.
2. Otherwise, if the request body is a bare (non-`Parameters`) resource, that
   resource is validated.
3. Otherwise, the **instance focus** from the URL (instance-level invocation) is
   validated.
4. If none of the above yields a target, the server returns **422 Unprocessable
   Entity** with an `error` `OperationOutcome` explaining that a target is
   required.

An instance URL must identify an existing resource before body precedence is
considered; a supplied resource does not bypass a missing-instance 404.
`Parameters` is supported as a **body on a proper operation route**, not as
the URL's resource type.

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

The POST contract is unchanged: per FHIR convention, validation results are
conveyed through `OperationOutcome` issues, **not** through HTTP status codes.
Once a parseable target is found, the operation returns **200 OK** regardless
of whether validation passed or failed. Method and earlier-guard rejections
are not validation results.

| Situation | HTTP status | Body |
|---|---|---|
| Validation passed | `200 OK` | `OperationOutcome` with a single `information` "All OK" issue |
| Validation found problems | `200 OK` | `OperationOutcome` with one issue per problem (severity/code from the validator, optional `expression`) |
| `mode` / `profile` supplied | `200 OK` | The above **plus** one `information` "ignored" issue per parameter |
| POST with no target resource | `422 Unprocessable Entity` | `error`/`invalid` `OperationOutcome` requesting a target |
| Malformed POST JSON/XML | `415 Unsupported Media Type` | `error`/`invalid` `OperationOutcome` from parsing, before validation |
| GET after earlier guards succeed | `405 Method Not Allowed` | One `error`/`not-supported` issue as specified above; `Allow: POST` |
| Missing instance, unsupported Parameters/resource URL, unknown operation, or missing tenant | Existing `404 Not Found` | Existing boundary-specific error; not replaced by GET's method rejection |
| Model inspector failed to initialize at startup | `500 Internal Server Error` | `error` `OperationOutcome` (see server logs) |

The table describes normal body selection. The existing HTTP `Prefer` handling
is unchanged, including an empty body for `return=minimal`.

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
