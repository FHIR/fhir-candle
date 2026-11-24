# Subscriptions reference implementation

This project contains the reference stack for FHIR Subscriptions. To use the
default landing page of the subscriptions RI, the following command can be used:

```
fhir-candle --reference-implementation subscriptions --load-package hl7.fhir.uv.subscriptions-backport#1.1.0 --load-examples false --protect-source true -m 1000
```

## Resource-trigger interactions

Supported resource interactions apply before FHIRPath criteria are evaluated. An update-only
topic cannot generate create or delete events merely because its expression matches. When
interaction restrictions are omitted, the trigger remains eligible for create, update, and delete.

## Subscription lifecycle

Only active, unexpired subscriptions generate events or begin event/heartbeat delivery.
Requested, unexpired subscriptions may handshake; a successful handshake activates only that
same still-current requested revision. Supported inactive and terminal statuses are preserved.

Subscription deadlines are absolute UTC instants. The default maximum lifetime is 30 minutes:
the effective deadline is the earlier of the client's End and acceptance time plus the configured
maximum. Zero disables only the configured cap; an explicit End still applies. Zero with no End
is unlimited. Negative maximums are rejected. The client's stored End representation is preserved;
reverse conversion exposes the effective deadline.

Accepted resource replacements establish a new deadline and revision. Status changes, handshake
completion, heartbeat activity, and topic/startup reprocessing do not renew it. Loaded and protected
startup content obey the same limits. A dedicated store timer enforces expiry independently of
resource-capacity monitoring; reads and delivery admission also enforce reached deadlines.

Turning a subscription off, replacing it, deleting it, or reaching its deadline invalidates queued
work. Each actual transport start is admitted against its captured revision, including SMTP send
after connection/authentication and each Zulip recipient. Already-started I/O may finish, but late
completion cannot activate or change the bookkeeping of a newer lifecycle. Queueing a heartbeat
is not successful communication. Reserved example-domain shortcuts remain simulations, not delivery.

Inactive subscriptions retain their existing history for `$events` and `$status` inspection.
Ordinary replacement does not transfer the previous subscription's event count or history.

## Notification delivery limits

| CLI option | Configuration key | Default (per server) |
| --- | --- | --- |
| `--notification-queue-capacity` | `Notification_Queue_Capacity` | 256 waiting notifications |
| `--notification-concurrency` | `Notification_Concurrency` | 4 active deliveries |

Both limits are positive integers. Waiting capacity must be at least the number of configured
tenants. Set either key in `appsettings.json` or as an environment variable. Environment variables
also accept the `Candle` prefix, for example `CandleNotification_Concurrency=2`. An unprefixed
variable overrides its prefixed counterpart, and a CLI option overrides the corresponding key.

Events, handshakes, and heartbeats share these limits. Each tenant has a FIFO queue. Waiting
slots are divided evenly among tenants, with any remainder assigned deterministically; unused
slots are not lent to other tenants. When a tenant's queue is full, the server declines the new
notification without evicting an accepted one. A `queue_saturated` diagnostic reports refusals,
not transport failures. As workers become available, they take turns among nonempty tenant queues.

## Zulip delivery

- To use the operator bot, configure `Zulip_Url`, `Zulip_Email`, and `Zulip_Key` together on the
  server. Omit subscription parameters `site`, `email`, and `key`. Alternatively, `site` alone
  may repeat the configured HTTPS base: host case, an explicit default port, and a final slash
  do not matter, but the host, effective port and case-sensitive base path must otherwise match.
- To use a subscription's own bot, provide one value each for `site`, `email`, and `key`.
  Missing credentials are never filled in from the operator bot.

Use an absolute HTTPS site with simple, unescaped path segments and no user information, query
or fragment. Messages for `streamId` and `userId` are posted to `api/v1/messages` beneath that
base. Redirects fail the send; configure the final site rather than a redirecting URL.

## Related

- [Getting started](getting-started.md)
- [Loading initial data](loading-data.md)
