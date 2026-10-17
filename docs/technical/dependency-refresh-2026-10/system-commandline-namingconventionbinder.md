# System.CommandLine.NamingConventionBinder — legacy prerelease retained

**Phase 31, 2026-10-16 local: retain 2.0.0-beta5.25306.1 in Host and Tests.**
No stable candidate exists in the freshly read public index; this is neither an upgrade nor a newly adopted preview.
Inspected predecessor `d0626ce5635095c5c4589af4dac1571987cf149b`; last implementation `839613a4e08bc39ce9fcf689db5233177c5b206f`.
This assessment belongs to the parent-owned phase-31 documentation commit; no self-SHA is fabricated.

## Fresh availability and constraints

At **2026-10-17 01:32 UTC / October 16 local**, the [NuGet index][index] contains **ten versions, all prereleases**, ending at the retained beta5.
The [registration/catalog][listing] marks **2.0.0-beta5.25306.1 listed**, published **2025-06-19 18:54:10.993 UTC**.
Original, evaluated available preview and selected version are identical; **evaluated stable: none**.
The [nuspec][nuspec] identifies supplier repository commit **`806a6d90bdd57f268bff30f36e2132a26371b08a`**.
Its net8.0 and netstandard2.0 groups each require System.CommandLine **[2.0.0-beta5.25306.1, )**, excluding dependency Build/Analyzers assets.
This is a minimum with no upper bound, **not equality and not a stable-CLI binary-compatibility guarantee**.

Actual owners:
- [Host project](../../../src/fhir-candle/fhir-candle.csproj): one unconditional reference.
- [Tests project](../../../src/fhir-candle.Tests/fhir-candle.Tests.csproj): one unconditional reference.

Both apply to net10.0/net9.0/net8.0. All six H/T graphs retain the beta and select `lib/net8.0/System.CommandLine.NamingConventionBinder.dll`.
Its sole package dependency resolves **System.CommandLine 2.0.13**, directly declared by H/C/T; CLI's selected net8 group has no package dependencies.
No netstandard-only System.Memory edge is selected for these packages. No direct transitive pin, downgrade, alias/asset-policy change or removal was made.

## Actual consumer review, not “already beta”

Tracked source search found **no active NamingConventionBinder, CommandHandler.Create or binder API invocation**.
[Program.Main](../../../src/fhir-candle/Program.cs) instead registers `RootCommand.SetAction`, parses with `ResponseFileTokenReplacer = null`, and calls `ParseResult.InvokeAsync`.
[CandleConfig](../../../src/FhirStore.Common/Configuration/CandleConfig.cs) uses typed `GetValue` and explicit-option `GetResult`/`Implicit` presence checks; configuration binding is the separate Microsoft.Extensions.Configuration.Binder.
[ConfigTests](../../../src/fhir-candle.Tests/ConfigTests.cs) exercises those parser/constructor contracts, not legacy convention-based handler binding.
The [CLI unit](system-commandline.md) reviewed the supplier [beta5 migration guide][migration] and passed actual help/error/startup checks while retaining this package.
CLI's actual upgrade commit is **`0cbb7656447866e4990de8431cd297e8a098b2dc`**, not a NamingConventionBinder upgrade.
The separately authorized tenant fallback repair and later Configuration upgrades remain distinct in the [final commit map](README.md#actual-local-commit-map).

Retention therefore preserves the existing declaration/asset envelope without pretending an unused binding API was exercised.
Range eligibility and the [matching final Windows full gate](README.md#final-windows-gate-reused-at-the-identical-implementation) support the **actual application paths only**.
No new restore/build/test or general binder binary-compatibility experiment was run here; Linux/macOS remain pending.
No supported stable replacement was established, and removal/migration is outside this documentation-only phase.

## Next reconsideration

Reassess when a **listed stable release appears**, a supplier support/security change affects this pin/CLI combination, or an authorized change introduces convention-based handlers or removes the declaration.
Before adopting any replacement, inspect the then-current stable index, supplier migration/support and all-target closure; identify real binder call sites and qualify their original/candidate behavior.
Preserve CLI precedence, option names/arities, response-file disabling, help/errors and external-tenant semantics. A new preview, CLI co-update or removal requires its own authorization.
No external credentials, helper, tooling or source/package/test edit was used. [AGENTS.md](../../../AGENTS.md) supplied repository conventions; this record's evidence is read-only metadata/source/graph inspection.

[index]: https://api.nuget.org/v3-flatcontainer/system.commandline.namingconventionbinder/index.json
[listing]: https://api.nuget.org/v3/registration5-gz-semver2/system.commandline.namingconventionbinder/2.0.0-beta5.25306.1.json
[nuspec]: https://api.nuget.org/v3-flatcontainer/system.commandline.namingconventionbinder/2.0.0-beta5.25306.1/system.commandline.namingconventionbinder.nuspec
[migration]: https://learn.microsoft.com/en-us/dotnet/standard/commandline/migration-guide-2.0.0-beta5
