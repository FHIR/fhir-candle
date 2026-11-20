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

## SMART backend scopes

For testing and development, a registered backend client can exchange a fresh
signed JWT assertion with `POST /_smart/<tenant>/token`. Send
`application/x-www-form-urlencoded` fields `grant_type=client_credentials`,
`client_assertion_type=urn:ietf:params:oauth:client-assertion-type:jwt-bearer`,
`client_assertion=<signed-assertion>`, and `scope=system/Patient.rs`.
After a successful exchange, use the actual returned `access_token` as
`Authorization: Bearer <access_token>` on requests to `/fhir/<tenant>/...`.
Do not use the assertion itself as the FHIR bearer token.

On a SMART-required tenant, that limited token allows `GET
/fhir/<tenant>/Patient/<id>` and `GET /fhir/<tenant>/Patient`, including matching
Patient resources in search results. Observation read/search requests remain
denied. Creating, updating, or deleting either Patient or Observation resources
is also denied: for example, `POST /fhir/<tenant>/Patient`, `PUT
/fhir/<tenant>/Patient/<id>`, and `DELETE /fhir/<tenant>/Patient/<id>` return the
existing direct-request `401 Unauthorized` response. These statuses describe
direct requests; Bundle entry responses follow the separate contract below.

Use one `scope` form field with space-delimited tokens, such as
`system/Patient.r system/Observation.s`. This grants Patient read and Observation
search, not Patient search or Observation read. Supported actions include v1
`read` (read/search) and `write` (create/update/delete), action `*` (all five),
and unordered v2 action letters `c`, `r`, `u`, `d`, `s`. A lone `r` does not
include search. Resource wildcard `*` applies those actions across resource
types, for example `system/*.rs`.

System scopes supply non-patient resource/action permissions, not a human-user
identity or patient launch context. Neither limited backend scopes nor wildcard
grants such as `system/*.*` install the intentional built-in administrator's
special `*.*` permission sentinel. Interactive user/patient consent and tenant
SMART policy remain separate.

Effective grants live only in the existing in-memory authorization state.
Perform a fresh exchange for newly corrected grants; previously issued state is
not rebuilt from scope text on each request. This is development-server
behavior, not a production-security guarantee, durable authorization storage,
or new granular-filter enforcement.

## Bundle authorization

Transactions are non-atomic and have no general rollback or isolation. An
authorized transaction can still leave earlier writes in place if a later
entry fails during execution. Authorization and required-body refusals prevent
execution instead of undoing writes: an HTTP `200 OK` with a
`transaction-response` does not mean every entry ran or succeeded.

Each executable batch or transaction entry is checked against its own resource
type and interaction permissions, not just the permission to submit a Bundle.
Ordinary batch entries have independent outcomes: a denied or failed entry
does not prevent other permitted entries from running in their normal input
order. A nested transaction reached by a batch applies its own checks without
refusing the batch's other entries.

Before any transaction preparation or mutation, every supported executable
descendant is checked for authorization, including reads and entries inside
nested batches or transactions. An authorization denial prevents execution of
that entire transaction subtree. The outer HTTP response remains `200 OK`, with a
`batch-response` or `transaction-response` Bundle. A denied entry has
`401 Unauthorized` and an OperationOutcome `forbidden` issue identifying its
submitted method, URL, and parsed interaction. Authorized transaction entries
that did not run instead say that they were not executed because transaction
authorization failed; a refused nested wrapper also identifies its own denied
descendant paths and submitted requests. These collateral refusals do not claim
that the wrapper's own permissions were denied, and carry no successful resource,
ETag, Location, or last-modified metadata.

Authorization denials take precedence in the response. If there are no denials
but an executable create or update (including conditional forms) lacks its
resource body, the transaction is refused before preparation or
execution. An entry missing its required body receives `400 Bad Request` with
a `structure` issue; other parsed entries that did not run receive
`424 Failed Dependency`. Skipped nested wrappers identify the ordinal paths
of their own invalid descendants. Skips have no success resource or metadata.

This includes supported programmatic nested Bundles and repeated or aliased
Bundle, entry, request, and resource objects. Shared object identity is an
in-process input property, not something represented by a serialized HTTP body.
Only executable batch/transaction Bundle requests are traversed recursively:
a `POST Bundle` resource body or an operation payload is not executed or
recursively admitted merely because it contains a Bundle.

Bundle admission, including recursive authorization and transaction
required-body checks, is bypassed only while the store's internal load state
is not `None` (initial loading). Outside loading, null authorization follows
the existing tenant policy: entries require authorization when
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

Bundle admission does not provide arbitrary-hook sandboxing, token-revocation
guarantees, or an end-to-end linearity guarantee.

## Related

- [Getting started](getting-started.md)
- [Loading initial data](loading-data.md)
