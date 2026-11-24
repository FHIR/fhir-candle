# User documentation

Guides for **running, configuring, and calling** fhir-candle. If you are
contributing to the server itself, see the [technical docs](../technical/)
instead.

## Pages

- [Getting started](getting-started.md) — install via the .NET tool, Docker, or
  by cloning the repo, then run the server.
- [FHIR tenants](tenants.md) — configure `/fhir/r4`, `/fhir/r4b`, and `/fhir/r5`,
  limit resources, page searches, and compare admin and SMART caller views.
- [Loading initial data](loading-data.md) — seed tenants with `--fhir-source`,
  select package versions, and configure archive import limits.
- [Strict mode (`--strict`)](strict-mode.md) — the strict spec-conformant REST
  posture for conformance test rigs.
- [Subscriptions reference implementation](subscriptions-ri.md) — run the RI,
  configure subscription lifetimes and delivery limits, and set up Zulip.
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
