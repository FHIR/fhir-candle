# FHIR tenants

By default, this software loads three FHIR 'tenants':

- a FHIR R4 endpoint at `/r4`,
- a FHIR R4B endpoint at `/r4b`, and
- a FHIR R5 endpoint at `/r5`.

The tenants can be controlled by command line arguments — note that manually
specifying any tenants overrides the default configuration and will *only* load
the ones specified. To load only an R4 endpoint at `fhir`, the arguments would
include `--r4 fhir`. You can specify multiple tenants for the same version; for
example, `--r5 fhir --r5 also-fhir` will create two endpoints.

## Bundle authorization

Each executable batch or transaction entry is checked against its own resource
type and interaction permissions, not just the permission to submit a Bundle.
Batch entries have independent outcomes: a denied entry does not prevent other
permitted entries from running in their normal input order.

Before any transaction preparation or mutation, every supported executable
descendant is authorized, including reads and entries inside nested batches or
transactions. An authorization denial prevents execution of that entire
transaction subtree. The outer HTTP response remains `200 OK`, with a
`batch-response` or `transaction-response` Bundle. A denied entry has
`401 Unauthorized` and an OperationOutcome `forbidden` issue identifying its
submitted method, URL, and parsed interaction. Authorized transaction entries
that did not run instead say that they were not executed because transaction
authorization failed; a refused nested wrapper also identifies its own denied
descendant paths and submitted requests. These collateral refusals do not claim
that the wrapper's own permissions were denied, and carry no successful resource,
ETag, Location, or last-modified metadata.

This includes supported programmatic nested Bundles and repeated or aliased
Bundle, entry, request, and resource objects. Shared object identity is an
in-process input property, not something represented by a serialized HTTP body.
Only executable batch/transaction Bundle requests are traversed recursively:
a `POST Bundle` resource body or an operation payload is not executed or
recursively admitted merely because it contains a Bundle.

Recursive authorization admission is bypassed only while the store's internal
load state is not `None` (initial loading). Outside loading, null authorization
follows the existing tenant policy: entries require authorization when
`SmartRequired` is true; optional access remains permitted when it is false.
A supplied restricted authorization still has its entry permissions checked.

### Transaction request URLs

Transaction identity remapping changes only a recognized ordinary simple
instance-ID segment. For example, `Patient/client?_tag=client` can become
`Patient/<allocated-id>?_tag=client`. The resource type, action tokens, URL
prefix, and query text remain literal; earlier accidental global substitutions
in resource types and query values are intentionally discontinued. Eligible
two-component instance-conditional targets use the same bounded ID-slot
remapping and retain their literal query.

Type-level URLs (including type-level conditional URLs), trailing-slash paths,
history, compartment, operation, and URN request targets gain no alias support
and are left literal. This does not add support for new interactions. Body
reference resolution, including URN references, is separate from request-URL
remapping and still observes live store state at the transaction's execution
position.

This is an authorization-before-mutation guarantee, not full FHIR transaction
atomicity, rollback, or isolation. An authorized transaction can still leave
earlier writes in place after a later non-authorization execution failure.
It also does not provide arbitrary-hook sandboxing, token-revocation guarantees,
or an end-to-end linearity guarantee.

## Related

- [Getting started](getting-started.md)
- [Loading initial data](loading-data.md)
