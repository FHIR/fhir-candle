# Strict mode (`--strict`)

The `--strict` flag puts the server in its strictest spec-conformant REST posture
so it can be used as the system-under-test for FHIR conformance test rigs
(Touchstone, Inferno, IG-specific test plans, custom CI suites) without lenient
behaviors masking client mistakes. It is **monolithic**, **opt-in**, and
**per-tenant**: a single boolean flips on every enforced behavior listed below,
and rejected requests carry the relevant FHIR spec URL inline in
`OperationOutcome.diagnostics`.

When `--strict` is combined with a conflicting explicit per-feature flag (e.g.
`--strict --create-as-update true`), strict wins and a one-line startup warning
names the override.

| Behavior | Status code | Lenient default | Spec |
|---|---|---|---|
| `PUT` body missing `Resource.id` | 422 | Stamps URL id onto empty body | `http.html#update` |
| `PUT` body id ≠ URL id (always on) | 422 | Same — always rejected | `http.html#update` |
| `POST` with client-supplied `Resource.id` | 400 | Silently re-assigns id (with `--create-existing-id`) | `http.html#create` |
| `PUT` on a missing resource id | 404 | Creates as update (with `--create-as-update`) | `http.html#upsert` |
| `Resource.id` not matching `[A-Za-z0-9\-\.]{1,64}` | 400 (or 422 at parse) | Best-effort accept | `datatypes.html#id` |
| Unknown / malformed search parameter | 400 | Silently dropped | `search.html#errors` |
| `CapabilityStatement.url` matches tenant base URL | n/a | Same — already correct | n/a |
| `CapabilityStatement.fhirVersion` matches tenant version | n/a | Same — already correct | n/a |

Search handling honors the standard `Prefer: handling=strict` /
`Prefer: handling=lenient` header. An explicit header wins over the tenant
default, so a strict tenant can still accept a lenient request, and a lenient
tenant can still serve a strict request, by client choice.

Strict mode is **not** advertised in `CapabilityStatement` — clients running
conformance tests should not have to read out-of-band metadata to know the server
is spec-correct.

## Related

- [`$validate` operation](operations/validate.md)
- [Getting started](getting-started.md)
