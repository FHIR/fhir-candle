# Operations

fhir-candle exposes a set of FHIR operations. Each page below documents an
operation by its **code**, covering scope, invocation levels, parameters,
response semantics, and worked examples.

## General operations

- [`$validate`](validate.md) — structural validation of a resource.
- [`$convert`](convert.md) — parse and re-serialize a resource (format
  conversion via content negotiation).
- [`$test-if-fhir`](test-if-fhir.md) — report whether a payload is parseable
  FHIR.
- [`$feature-query`](feature-query.md) — query the server's supported capability
  features.

## Store management

- [`$reset-store`](reset-store.md) — delete all non-protected resources.

## Subscriptions

- [`$status`](status.md) — current status of one or more `Subscription`
  resources.
- [`$events`](events.md) — fetch prior notification events for a `Subscription`.
- [`$subscription-hook`](subscription-hook.md) — receive an incoming
  notification `Bundle` (notification sink).

See also the [Subscriptions reference implementation](../subscriptions-ri.md).
