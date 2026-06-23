# `$status` operation (Subscription)

`Subscription/$status` returns the current status of one or more `Subscription`
resources as a searchset `Bundle` of subscription-status resources. It is part of
the FHIR Subscriptions framework (the R4/R4B Subscriptions Backport IG and the
R5 core).

## Summary

| | |
|-|-|
| Operation name | `$status` |
| Operation version | `0.0.1` |
| Applies to | `Subscription` |
| FHIR versions | R4, R4B, R5 |
| Canonical URL (R4/R4B) | `http://hl7.org/fhir/uv/subscriptions-backport/OperationDefinition/backport-subscription-status` |
| Canonical URL (R5) | `http://hl7.org/fhir/OperationDefinition/Subscription-status` |
| Affects state | No |
| HTTP method | `GET` or `POST` |

## Invocation

`$status` is invoked at the **type** and **instance** levels (not system level):

- **Type level** — `GET [base]/Subscription/$status` (optionally with `id` /
  `status` filters)
- **Instance level** — `GET [base]/Subscription/{id}/$status`

## Parameters

| Name | Use | Cardinality | Type | Notes |
|------|-----|-------------|------|-------|
| `id` | in | `0..*` | `id` | Subscription id(s) to report on. Multiple values are OR-joined. Ignored at the instance level. |
| `status` | in | `0..*` | `code` | Filter by subscription status (bound to `http://hl7.org/fhir/ValueSet/subscription-status`). Multiple values are OR-joined. Ignored at the instance level. |
| `return` | out | `1..1` | `Bundle` | A searchset `Bundle` containing one subscription-status resource per matched Subscription. |

`id` and `status` may be supplied as URL query parameters (comma-separated for
multiple values) or inside a `Parameters` request body.

## Behavior — target selection

- **Instance level** — the focus Subscription (from the URL `{id}`) is reported;
  `id` and `status` parameters are ignored.
- **Type level, no parameters** — every Subscription available to the caller is
  reported.
- **Type level, `status` only** — Subscriptions whose current status is in the
  filter set are reported.
- **Type level, `id` only** — the named Subscriptions are reported.
- **Type level, `id` + `status`** — the named Subscriptions are reported only if
  their current status is also in the filter set.

## Response semantics

| Situation | HTTP status | Body |
|---|---|---|
| Status returned | `200 OK` | A searchset `Bundle` of subscription-status resources (empty `Bundle` if nothing matched) |

## Examples

The examples below assume the **local default** base URL
`http://localhost:5826/fhir/r4`. If you started the server with the README
Docker quick-start (`docker run -p 8080:5826 ...`), use
`http://localhost:8080/fhir/r4` instead.

### Instance-level status

```bash
curl http://localhost:5826/fhir/r4/Subscription/example/\$status
```

Response — `200 OK`:

```json
{
  "resourceType": "Bundle",
  "type": "searchset",
  "entry": [
    {
      "fullUrl": "urn:uuid:...",
      "resource": {
        "resourceType": "Parameters",
        "id": "..."
      },
      "search": { "mode": "match" }
    }
  ]
}
```

> The status resource type varies by FHIR version (a backport `Parameters` /
> `SubscriptionStatus` for R4/R4B, a `SubscriptionStatus` for R5).

### Filter active subscriptions by status

```bash
curl "http://localhost:5826/fhir/r4/Subscription/\$status?status=active"
```

Response — `200 OK`: a searchset `Bundle` containing one entry per active
Subscription.

## See also

- [`$events`](events.md) — fetch prior notification events for a subscription.
- [`$subscription-hook`](subscription-hook.md) — receive notification bundles.
- [Subscriptions reference implementation](../subscriptions-ri.md)
- [Operations index](README.md)
