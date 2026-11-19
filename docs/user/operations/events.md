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
| `eventsSinceNumber` | in | `0..1` | `integer64` | Nonnegative starting event number, inclusive (lower bound). Omitted or unparseable values default to `0`. |
| `eventsUntilNumber` | in | `0..1` | `integer64` | Nonnegative ending event number, inclusive (upper bound). Omitted or unparseable values default to the captured current highest event number; explicit values are clipped to that number. |
| `content` | in | `0..1` | `code` | Requested payload content style (e.g. `empty`, `id-only`, `full-resource`). A hint only; the server MAY ignore it. |
| `return` | out | `1..1` | `Bundle` | A notification `Bundle` for the requested events. |

The parameters may be supplied as URL query parameters or inside a `Parameters`
request body. Complete body parameters override the corresponding query values.
The query form also accepts `eventssincenumber` / `eventsuntilnumber` and the
dashed spellings `events-since-number` / `events-until-number`.
For POST, supply both bounds and `content` together; missing body-member parsing
is not covered by the fallback behavior described here.

## Behavior

The server captures the subscription's current highest event number (`H`) once,
then selects retained events in the inclusive range
`[eventsSinceNumber, min(eventsUntilNumber, H)]`, sorted by event number ascending.
An omitted or unparseable lower bound defaults to `0`; an omitted or unparseable
upper bound defaults to `H`. Either successfully parsed negative bound returns
`400 Bad Request`, even if the range is also reversed.

Selection scans retained history rather than expanding every integer between
the bounds. There is no numeric-width cap or silent truncation: wide sparse
ranges, including the maximum nonnegative `integer64` upper bound, are valid.
This does not limit retained history or the size of a legitimate response.

An empty subscription, cleared history, a range with no retained matches, or a
nonnegative reversed range returns a successful status-only notification bundle:
one status entry, no notification events, and no event resources, including with
`content=full-resource`. Otherwise, content follows the requested style, or the
subscription's configured content level when no override is supplied.

History selection and bundle construction share a per-subscription monitor with
event registration and clearing. Replay has a finite selection bounded by the
captured `H`; later registration need not appear. The status's informational
total-event counter is not the selected-event count and can include numbers
reserved before their events are registered. This is not a general concurrency
guarantee for subscription lifecycle or other state.

The returned `Bundle` shape depends on the FHIR version: a `history` bundle whose
first entry is a `Parameters` (R4) or `SubscriptionStatus` (R4B) resource, or a
`subscription-notification` bundle led by a `SubscriptionStatus` (R5).

## Response semantics

| Situation | HTTP status | Body |
|---|---|---|
| Events returned | `200 OK` | A notification `Bundle` for the requested events |
| Empty, cleared, reversed, or no-match selection | `200 OK` | Native notification `Bundle` with exactly one status entry and no notification events or event resources |
| Either parsed bound is negative | `400 Bad Request` | `OperationOutcome` |
| Subscription id missing or unknown | `404 Not Found` | `OperationOutcome` |

## Examples

The examples below assume the **local default** base URL
`http://localhost:5826/fhir/r4`. If you started the server with the README
Docker quick-start (`docker run -p 8080:5826 ...`), use
`http://localhost:8080/fhir/r4` instead.

### Fetch all events for a subscription

```bash
curl http://localhost:5826/fhir/r4/Subscription/example/\$events
```

Response — `200 OK`: a notification `Bundle` containing retained events from `0`
through the captured current highest event number.

### Fetch a bounded range, ids only

```bash
curl "http://localhost:5826/fhir/r4/Subscription/example/\$events?eventsSinceNumber=5&eventsUntilNumber=10&content=id-only"
```

Response — `200 OK`: a notification `Bundle` for retained events 5–10, clipped to
the captured current highest event number.

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
      "diagnostics": "Instance Subscription/does-not-exist does not exist on this server."
    }
  ]
}
```

## See also

- [`$status`](status.md) — current status of subscriptions.
- [`$subscription-hook`](subscription-hook.md) — receive notification bundles.
- [Subscriptions reference implementation](../subscriptions-ri.md)
- [Operations index](README.md)
