# User documentation

Guides for **running, configuring, and calling** fhir-candle. If you are
contributing to the server itself, see the [technical docs](../technical/)
instead.

## Pages

- [Getting started](getting-started.md) — install via the .NET tool, Docker, or
  by cloning the repo, then run the server.
- [FHIR tenants](tenants.md) — the default `/r4`, `/r4b`, `/r5` endpoints and how
  to override them with `--r4` / `--r4b` / `--r5`.
- [Loading initial data](loading-data.md) — seed the server with `--fhir-source`
  and how per-tenant subdirectories are resolved.
- [Strict mode (`--strict`)](strict-mode.md) — the strict spec-conformant REST
  posture for conformance test rigs.
- [Subscriptions reference implementation](subscriptions-ri.md) — run the FHIR
  Subscriptions RI stack.
- [Using OpenTelemetry](opentelemetry.md) — export traces via
  `--otel-otlp-endpoint` / `OTEL_EXPORTER_OTLP_ENDPOINT`.

### Operations

- [`$validate`](operations/validate.md) — structural validation operation: what
  it covers, what it does not, request shapes, and response semantics.
