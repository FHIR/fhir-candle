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

## Related

- [Getting started](getting-started.md)
- [Loading initial data](loading-data.md)
