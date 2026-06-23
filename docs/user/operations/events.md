# `$events` operation (Subscription)

`Subscription/$events` returns prior notification events for a specific
`Subscription` as a notification `Bundle`. It lets a client fetch (or re-fetch) a
range of events by event number. It is part of the FHIR Subscriptions framework
(the R4/R4B Subscriptions Backport IG and the R5 core).

## Summary

| | |
|-|-|
| Operation name | `$events` |
| Operation version | `0.0.1` |
| Applies to | `Subscription` |
| FHIR versions | R4, R4B, R5 |
| Canonical URL (R4/R4B) | `http://hl7.org/fhir/uv/subscriptions-backport/OperationDefinition/backport-subscription-events` |
| Canonical URL (R5) | `http://hl7.org/fhir/OperationDefinition/Subscription-events` |
| Affects state | No |
| HTTP method | `GET` or `POST` |

## Invocation

`$events` is invoked at the **instance level** only:

- **Instance level** — `GET [base]/Subscription/{id}/$events` (optionally with
  the parameters below)

It is not available at the system or type level.

## Parameters

| Name | Use | Cardinality | Type | Notes |
|------|-----|-------------|------|-------|
| `eventsSinceNumber` | in | `0..1` | `integer64` | Starting event number, inclusive (lower bound). Defaults to `0`. |
| `eventsUntilNumber` | in | `0..1` | `integer64` | Ending event number, inclusive (upper bound). Defaults to the subscription's current highest event number. |
| `content` | in | `0..1` | `code` | Requested payload content style (e.g. `empty`, `id-only`, `full-resource`). A hint only; the server MAY ignore it. |
| `return` | out | `1..1` | `Bundle` | A notification `Bundle` for the requested events. |

The parameters may be supplied as URL query parameters or inside a `Parameters`
request body. The query form also accepts the lowercase/dashed spellings
`events-since-number` and `events-until-number`.

## Behavior

The server collects every event number in the inclusive range
`[eventsSinceNumber, eventsUntilNumber]` and returns a notification `Bundle` for
those events. If `eventsSinceNumber` is omitted it defaults to `0`; if
`eventsUntilNumber` is omitted it defaults to the subscription's current highest
event number.

The returned `Bundle` shape depends on the FHIR version: a `history` bundle whose
first entry is a `Parameters` (R4) or `SubscriptionStatus` (R4B) resource, or a
`subscription-notification` bundle led by a `SubscriptionStatus` (R5).

## Response semantics

| Situation | HTTP status | Body |
|---|---|---|
| Events returned | `200 OK` | A notification `Bundle` for the requested events |
| Subscription id missing or unknown | `404 Not Found` | `OperationOutcome` ("Subscription {id} was not found.") |

## Examples

The examples below assume the **local default** base URL
`http://localhost:5826/fhir/r4`. If you started the server with the README
Docker quick-start (`docker run -p 8080:5826 ...`), use
`http://localhost:8080/fhir/r4` instead.

### Fetch all events for a subscription

```bash
curl http://localhost:5826/fhir/r4/Subscription/example/\$events
```

Response — `200 OK`: a notification `Bundle` covering events `0` through the
subscription's current highest event number.

### Fetch a bounded range, ids only

```bash
curl "http://localhost:5826/fhir/r4/Subscription/example/\$events?eventsSinceNumber=5&eventsUntilNumber=10&content=id-only"
```

Response — `200 OK`: a notification `Bundle` for events 5–10.

### Unknown subscription

```bash
curl http://localhost:5826/fhir/r4/Subscription/does-not-exist/\$events
```

Response — `404 Not Found`:

```json
{
  "resourceType": "OperationOutcome",
  "issue": [
    {
      "severity": "error",
      "code": "not-found",
      "diagnostics": "Subscription does-not-exist was not found."
    }
  ]
}
```

## See also

- [`$status`](status.md) — current status of subscriptions.
- [`$subscription-hook`](subscription-hook.md) — receive notification bundles.
- [Subscriptions reference implementation](../subscriptions-ri.md)
- [Operations index](README.md)
