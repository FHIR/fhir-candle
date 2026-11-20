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
- **Instance level** — `POST [base]/Subscription/{id}/$events` (optionally with
  query parameters and a `Parameters` request body)

It is not available at the system or type level.

## Parameters

| Name | Use | Cardinality | Type | Notes |
|------|-----|-------------|------|-------|
| `eventsSinceNumber` | in | `0..1` | `integer64` | Nonnegative starting event number, inclusive (lower bound). Omitted or unparseable values default to `0`. |
| `eventsUntilNumber` | in | `0..1` | `integer64` | Nonnegative ending event number, inclusive (upper bound). Omitted or unparseable values default to the captured current highest event number; explicit values are clipped to that number. |
| `content` | in | `0..1` | `code` | Requested payload content style (e.g. `empty`, `id-only`, `full-resource`). A hint only; the server MAY ignore it. |
| `return` | out | `1..1` | `Bundle` | A notification `Bundle` for the requested events. |

The parameters may be supplied as URL query parameters or inside a POST
`Parameters` request body. Each body member is independently optional: a present
matching body member overrides only its corresponding query value. An absent
body member retains that query value. A valid `Parameters` envelope with zero
operation parameters or with only unrelated members adds no recognized
overrides. Body names are case-sensitive and use the canonical names in the
table. The query form also accepts `eventssincenumber` /
`eventsuntilnumber` and the dashed spellings
`events-since-number` / `events-until-number`.

After resolving each source independently, the lower bound falls back to `0`,
the upper bound falls back to the high-water event number `H` captured inside the
store, and, when there is no effective content override, the subscription's
configured content style applies. A present parser-accepted empty or unparseable
body bound still overrides the query and invokes that bound's normal fallback;
it does not revive the query value. This does not promise acceptance of invalid
wire primitives.
POST body bounds use `valueString` for R4/R4B and `valueInteger64` for R5;
R5 `integer64` JSON values are strings.

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

The literal `content=empty` is a meaningful style: it retains selected event
metadata but suppresses focus references, additional-context references, and
event resource payloads. It does not suppress the selected events themselves.

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

### POST with zero operation parameters

These JSON and XML bodies contain no `parameter` components:

```json
{"resourceType":"Parameters","id":"events-empty-envelope"}
```

```xml
<Parameters xmlns="http://hl7.org/fhir"><id value="events-empty-envelope"/></Parameters>
```

Both use any query parameters as supplied, or the normal defaults if the query
is omitted. The `id` is ordinary resource metadata, not an operation parameter
or the Subscription id; it is not required on every `Parameters` request.

The current SDK deserialization path rejects the raw element-empty forms
`{"resourceType":"Parameters"}` and `<Parameters xmlns="http://hl7.org/fhir" />`.
`$events` returns `415 Unsupported Media Type` for those bodies, even with
valid query parameters.

### POST content only (JSON or XML)

This JSON body requests ids only while both bounds use their defaults:

```bash
curl -X POST "http://localhost:5826/fhir/r4/Subscription/example/\$events" \
  -H "Content-Type: application/fhir+json" -H "Accept: application/fhir+json" \
  --data '{"resourceType":"Parameters","parameter":[{"name":"content","valueCode":"id-only"}]}'
```

The equivalent XML request is:

```bash
curl -X POST "http://localhost:5826/fhir/r4/Subscription/example/\$events" \
  -H "Content-Type: application/fhir+xml" -H "Accept: application/fhir+xml" \
  --data '<Parameters xmlns="http://hl7.org/fhir"><parameter><name value="content"/><valueCode value="id-only"/></parameter></Parameters>'
```

Both return `200 OK` with retained events from `0` through the captured `H`,
including focus/context references but no event resource payloads. These bodies
also work at R4B and R5 tenant endpoints.

### POST content override with query bounds

```bash
curl -X POST "http://localhost:5826/fhir/r4/Subscription/example/\$events?eventsSinceNumber=5&eventsUntilNumber=10&content=full-resource" \
  -H "Content-Type: application/fhir+json" -H "Accept: application/fhir+json" \
  --data '{"resourceType":"Parameters","parameter":[{"name":"content","valueCode":"empty"}]}'
```

Only `content` is overridden: query bounds 5–10 remain effective, clipped to the
captured `H`. The `200 OK` notification `Bundle` retains the selected event
numbers and timestamps, but no focus/context references or event resources.
If there are no retained matches, it contains only its native status entry.

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
