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

See the [operations index](operations/README.md) for all documented operations.

- [`$validate`](operations/validate.md) — structural resource validation.
- [`$convert`](operations/convert.md) — parse and re-serialize a resource.
- [`$test-if-fhir`](operations/test-if-fhir.md) — is a payload parseable FHIR?
- [`$feature-query`](operations/feature-query.md) — query supported capability
  features.
- [`$reset-store`](operations/reset-store.md) — delete all non-protected
  resources.
- [`$status`](operations/status.md) — current status of `Subscription`
  resources.
- [`$events`](operations/events.md) — fetch prior `Subscription` notification
  events.
- [`$subscription-hook`](operations/subscription-hook.md) — receive a
  notification `Bundle`.
