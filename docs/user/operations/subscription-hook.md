# `$subscription-hook` operation

`$subscription-hook` is a **receiving endpoint**: it accepts an incoming
`Subscription` notification `Bundle`, stores it, and registers that a
notification was received. It is used to make a fhir-candle tenant act as the
*destination* (notification sink) for another server's `rest-hook` subscription —
useful when testing subscription senders.

> **Scope at a glance.** This **mutates the store**: the posted notification
> bundle is persisted as a `Bundle` resource and recorded as a received
> notification.

## Summary

| | |
|-|-|
| Operation name | `$subscription-hook` |
| Operation version | `0.0.1` |
| FHIR versions | R4, R4B, R5 |
| Canonical URL | `http://argo.run/fhir/OperationDefinition/subscription-hook` |
| Affects state | **Yes** — stores the notification bundle |
| HTTP method | `POST` only (no `GET`) |

## Invocation

`$subscription-hook` is invoked at the **system level** only:

- **System level** — `POST [base]/$subscription-hook`

It is not available at the type or instance level.

## Parameters

| Name | Use | Cardinality | Type | Notes |
|------|-----|-------------|------|-------|
| `resource` | in | `1..1` | `Bundle` | The subscription notification bundle. Supplied as the request body. |
| `return` | out | `1..1` | `OperationOutcome` | The result of receiving the notification. |

## Behavior

1. The request body must be a notification `Bundle` with at least one entry whose
   first entry has a resource. If the bundle has no `id`, the server assigns one.
2. The bundle is parsed as a subscription notification. If it cannot be parsed as
   a valid notification bundle, the request fails with `422`.
3. The bundle is stored as a `Bundle` resource in the tenant's store.
4. The server registers that a notification was received for the bundle.

## Response semantics

| Situation | HTTP status | Body |
|---|---|---|
| Notification received and stored | `200 OK` | `OperationOutcome` ("Subscription Notification Received") |
| Body is not a valid notification bundle | `422 Unprocessable Entity` | `OperationOutcome` ("Posted content is not a valid Subscription notification bundle") |
| Storing the bundle failed | (status from the create) | `OperationOutcome` from the failed create |

## Examples

The examples below assume the **local default** base URL
`http://localhost:5826/fhir/r4`. If you started the server with the README
Docker quick-start (`docker run -p 8080:5826 ...`), use
`http://localhost:8080/fhir/r4` instead.

### Post a notification bundle

```bash
curl -X POST http://localhost:5826/fhir/r4/\$subscription-hook \
  -H "Content-Type: application/fhir+json" \
  --data-binary @notification-bundle.json
```

Response — `200 OK`:

```json
{
  "resourceType": "OperationOutcome",
  "issue": [
    {
      "severity": "information",
      "code": "success",
      "diagnostics": "Subscription Notification Received"
    }
  ]
}
```

### Invalid payload

```bash
curl -X POST http://localhost:5826/fhir/r4/\$subscription-hook \
  -H "Content-Type: application/fhir+json" \
  -d '{ "resourceType": "Patient", "id": "example" }'
```

Response — `422 Unprocessable Entity`:

```json
{
  "resourceType": "OperationOutcome",
  "issue": [
    {
      "severity": "error",
      "code": "exception",
      "diagnostics": "Posted content is not a valid Subscription notification bundle"
    }
  ]
}
```

## See also

- [`$status`](status.md) — current status of subscriptions.
- [`$events`](events.md) — fetch prior notification events.
- [Subscriptions reference implementation](../subscriptions-ri.md)
- [Operations index](README.md)
