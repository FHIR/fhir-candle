# zulip-cs-lib — legacy prerelease retained

**Phase 31, 2026-10-16 local: retain 0.0.1-beta.5.**
No stable candidate exists in the freshly read public index; this is not an upgrade, new preview adoption or delivery certification.
Inspected predecessor `d0626ce5635095c5c4589af4dac1571987cf149b`; last implementation `839613a4e08bc39ce9fcf689db5233177c5b206f`.
This assessment belongs to the parent-owned phase-31 documentation commit, whose future SHA is not recorded here.

## Fresh availability and actual closure

At **2026-10-17 01:32 UTC / October 16 local**, the [NuGet index][index] lists **ten versions, all prereleases**: six alpha and four beta versions (no beta.2), ending at **0.0.1-beta.5**.
The [registration/catalog][listing] marks that pin listed, published **2026-06-22 16:58:26.537 UTC**.
Original, evaluated available preview and selected version are identical; **evaluated stable: none**.
The [nuspec][nuspec] identifies supplier repository commit **`7a1856a36a76b33efb9c96f23cea1aee5229fcba`** and empty net10.0/net9.0/net8.0 dependency groups.
It imposes **no transitive NuGet minimum or co-update**; this is not an assertion that HTTP/runtime services are unnecessary.

The sole declaration owner is [Host](../../../src/fhir-candle/fhir-candle.csproj), unconditional across all three TFMs.
Tests inherits it through Host; all six H/T graphs select the matching `lib/net10.0`, `lib/net9.0` or `lib/net8.0/zulip-cs-lib.dll` asset and have no package dependency edges for this node.
No stable replacement, alias/asset-policy change, transitive pin, package removal or consumer rewrite was introduced.

## Active consumer and limitations

[ZulipClientPool](../../../src/fhir-candle/Zulip/ZulipClientPool.cs) creates and reuses supplier `ZulipClient(site, email, key)` instances.
[CandleConfig](../../../src/FhirStore.Common/Configuration/CandleConfig.cs) retains external/CLI URL, email and key configuration.
[NotificationManager](../../../src/fhir-candle/Services/NotificationManager.cs) registers a client only with complete configuration and dispatches the **zulip** notification channel.
`TryNotifyZulip` allows subscription site/email/key overrides, rejects missing configuration, builds the message, and sends to parsed stream/user IDs through `Messages.TrySendStream` / `TrySendPrivate`.
These APIs are awaited but have **no cancellation-token overload**: caller cancellation is checked before each send, and a started send remains awaited during shutdown.
The returned `(success, details, messageId)` tuples are assigned but not used to decide success; after the loops the caller updates communication time and returns true unless an exception intervenes.
Cancellation is rethrown when requested; other exceptions log/register subscription errors and return false. These are existing contracts/limitations, not repaired or endorsed delivery semantics.
The disabled Zulip choice in the subscription-tour UI does not make the backend/package unused.

[LocalPackageFixture](../../../src/fhir-candle.Tests/Extensions/LocalPackageFixture.cs) clears Zulip configuration.
Existing [notification tests](../../../src/fhir-candle.Tests/Sdk6NotificationTests.cs) exercise REST hooks and lifecycle, **not authenticated Zulip delivery**.
The [September record](../sdk6-compatibility.md) retains its own limits; the [final Windows suite](README.md#final-windows-gate-reused-at-the-identical-implementation) does not turn those tests into Zulip wire evidence.
This phase ran no server, account request, restore/build/test or transport experiment; no credentials, personal configuration or clinical data were read or emitted.
Linux/macOS and actual Zulip delivery remain unverified. Retention preserves an active integration with no stable candidate rather than removing it or inventing safe replacement behavior.

## Next reconsideration

Reassess on a **listed stable release**, relevant supplier support/security or API/cancellation changes, or an explicitly authorized redesign/removal of this integration.
Revalidate the then-current index, all-target assets and API contract; any preview switch requires separate authorization.
Before a replacement is accepted, obtain an authorized controlled Zulip facility with synthetic credentials/data and bounded stream/private-send, error, cancellation and shutdown observations.
Check message construction, overrides, recipient parsing, response handling and lifecycle against retained/candidate behavior; compilation, empty configuration and REST-hook success are not substitutes.
A needed response-handling or client-pool repair is a separate product decision, not a hidden dependency change.
[AGENTS.md](../../../AGENTS.md) supplied conventions; only read-only source/metadata/graph checks and this documentation were performed. No sanctioned Zulip-specific execution procedure was invented.

[index]: https://api.nuget.org/v3-flatcontainer/zulip-cs-lib/index.json
[listing]: https://api.nuget.org/v3/registration5-gz-semver2/zulip-cs-lib/0.0.1-beta.5.json
[nuspec]: https://api.nuget.org/v3-flatcontainer/zulip-cs-lib/0.0.1-beta.5/zulip-cs-lib.nuspec
