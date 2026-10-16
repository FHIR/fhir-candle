# Firely SDK cohort — deferred, 6.2.1 retained

**2026-10-16, Windows-local assessment: retain aligned R4/R4B/R5 and test-only R6 6.2.1.**
Newest stable **6.7.0** was assessed but not trialled. This is phase 19's documentation-only retention route,
not an accepted upgrade, demonstrated compatibility ceiling, failed candidate, or completed SDK/browser gate.
Predecessor/source: `3bb62a3baf4382d01c319d15783f6ecfdf93dad9`; index and tracked worktree were clean.
[AGENTS.md](../../../AGENTS.md), phase 19, the unit contract, V-accept and manual gates supplied the instructions.
The [initial SDK grouping evidence](README.md#sdk-aligned-p19-cohort-trial-mixed-binaries-remain-unproven) and
[historical SDK record](../sdk6-compatibility.md) remain unchanged; no new envelope was accepted.

## Material local prerequisite — rechecked before pins or tests

Read the exact [browser procedures](../sdk6-compatibility.md#browser-checklist), [subscriptions RI](../../user/subscriptions-ri.md),
launch profiles, package-service cache setup and synthetic package fixtures. No browser result/cache was supplied for this dispatch.
Fresh inspection **does not repeat the historical claim that both pinned packages are absent**:

- Backport `hl7.fhir.uv.subscriptions-backport#1.1.0` was absent at each checked layout:
  `~/.fhir/packages/<id>#<version>`, `~/.fhir/packages/<id>/<version>`, `~/.fhir/<id>/<version>`.
- CDex **2.0.0 is present** at `~/.fhir/packages/hl7.fhir.us.davinci-cdex#2.0.0/package/package.json`.
  Its manifest identifies the expected name/version; SHA-256 `cd96696eddac968146865ba74ad86572e142653da4caaba831f3289bf77de71b`.
  Expanded conformance filenames were observed, not loaded or certified as a complete offline closure.
- Four of its eight direct dependency manifests were absent in all three layouts: `hl7.fhir.us.davinci-hrex#1.0.0`,
  `hl7.fhir.us.davinci-pas#1.2.0-ballot`, `hl7.fhir.us.carin-bb#2.0.0`, `hl7.fhir.us.davinci-dtr#1.1.0-ballot`.
  R4 core 4.0.1, terminology.r4 5.0.0, US Core 3.1.1 and SDC 3.0.0 manifests were present in the hash layout.
  Presence is not validation of their recursive contents; no existing ballot dependency was changed or fetched.
- The bounded `%TEMP%/fhir-candle-sdk6-*` inspection found `fhir-candle-sdk6-1015-02`: both source and cache are empty.
  It is pre-existing, not owned by this dispatch; it was not reused, modified or deleted.
- Repository filename/directory inspection covered `temp`, `nupkg`, `fhirData`, `scratch`, and `src/fhir-candle.Tests/data`,
  excluding `.git`, `bin`, `obj`, and `node_modules`. Hits were application archives and CDex resource directories, not a supplied RI cache.
  ZIP-directory inspection of the two `nupkg` application archives and the scratch performance baseline archive found no
  `package.json`, `.tgz`, `.tar.gz`, backport-named or `#2.0.0` entries. This was not a disk-wide cache search.
- Test manifests are deliberately `example.test.sdk6.*#1.0.0` and synthetic CDex `0.0.0-sdk6-test`, not the pinned official packages.
  Existing `LocalPackageFixture` archive construction is test infrastructure, not authority to fabricate an official RI package.
- Edge **154.0.4258.53** and Chrome **155.0.8059.80** executable versions were observed. Browser availability is not interaction evidence.
  No applicable browser automation procedure was found in the inspected repository guidance/source; none was installed.

**The mandatory complete local offline RI setup is unavailable in that inspected scope.**
No FHIR registry was contacted, clinical examples loaded, personal credentials inspected, or new packaging/browser tool introduced.
Missing Linux/macOS execution is **not** this decision's cause; those platforms remain post-Windows-pass follow-up.

## Fresh availability, supplier support and compatibility review

The public NuGet version indexes for [R4][r4], [R4B][r4b], [R5][r5], [R6][r6] each identify **6.7.0** as highest stable.
Fresh registration/catalog GETs for all four at both versions mark **6.2.1 and 6.7.0 listed**, published respectively
**2026-07-01** and **2026-10-15**, with no deprecation/vulnerability entries there. This is not a closure security audit.
Registration URL form: `https://api.nuget.org/v3/registration5-gz-semver2/<lowercase-id>/<version>.json`.
Original/candidate nuspec repository commits agree across all six SDK packages:
`8d45ad8d6a9b8d8765a6449977baf3da738dd0d5` / `390dd59e631748d098d087b196de358547942cb6`.
At those commits, `src/Hl7.Fhir.{R4,R4B,R5,R6}/Model/Generated/Template-ModelInfo.cs` is byte-identical per model,
retaining `4.0.1` / `4.3.0` / `5.0.0` / test-only `6.0.0-ballot3`. This is source metadata, not a binary/runtime pass.

The tagged [README][sdk-readme] promises effort toward **compile, not binary, compatibility** between minor releases;
its binary-checking section explicitly permits exceptions. Community/commercial support prose is not a per-patch LTS guarantee.
[6.6.0 notes][sdk-66] retain net8 until SDK 7 and add net10; net9 applications use net8 assets. No Candle TFM drop is established.
The [SDK 6 migration guide][migration] describes already-inherited model/inspector, parser and PocoNode changes, not a new migration mandate.
The multi-version guide returned HTTP 403 on fresh retrieval; its earlier aligned-alias assessment remains attributed to the initial inventory.
Tagged `SECURITY.md` and a `v6.2.2` release lookup returned 404; successful README and 6.3–6.7 release notes supplied this review.

| Supplier change | Existing consumer boundary / required protection |
|---|---|
| [6.3][sdk-63]: wrong-cased names fail strict parsing; non-strict binding remains lenient | `SerializationUtils` uses BACKWARDSCOMPATIBLE and explicit OSTRICH, not application strict-policy flags as SDK parser settings. |
| [6.4][sdk-64]: opt-in XML comment retention; serializers emit SourceComments | Existing whitespace-preserving XmlReader and independently authored JSON/XML/narrative expectations remain authoritative. |
| [6.5][sdk-65]: empty objects/elements suppressed, including summary-filtered POCOs | Full/summary serialization must not lose protected content; no expected fixtures regenerated. |
| 6.5: dynamic model inference uses FHIR definitions; snapshot issue renumbering and sequential terminology resolution | Preserve version-specific types, model-inspector identity, operation outcomes and resolver/tenant behavior; not proof that generated models are interchangeable. |
| [6.6][sdk-66]: `HtmlChecks(PocoNode)` changes `bool` to `bool?`; FHIRPath string support | A concrete common-API binary break requiring caller recompilation, not proof that Candle or retained package-service binaries fail. |
| 6.6: resource-body FhirClient operation overloads, existing Parameters overloads retained | `CandleClient`'s native client/settings/lenient serializer and JSON/XML HTTP contracts need their existing tests, not a wrapper redesign. |
| [6.7][sdk-67]: duplicate primitive values from `name`/`_name` now fatal JSON135 outside Ostrich, either order/arrays | BACKWARDSCOMPATIBLE input acceptance is materially affected; independent parse regressions are required if trialled, not a blanket leniency switch. |
| 6.7: invalid-primitive FHIRPath, contained-reference resolution, repeated snapshot extensions, terminology result/parameter and issue-text changes | Protect search, `$validate`, package operations, subscriptions and authored outcomes; these are risks to verify, not executed failures. PrimitiveValueConverter remains opt-in. |

## Actual ranges/assets; conditional closure, not a candidate restore

Fresh nuspecs for all four models and [Base 6.2.1](https://api.nuget.org/v3-flatcontainer/hl7.fhir.base/6.2.1/hl7.fhir.base.nuspec) / [6.7.0](https://api.nuget.org/v3-flatcontainer/hl7.fhir.base/6.7.0/hl7.fhir.base.nuspec)
and [Conformance 6.2.1](https://api.nuget.org/v3-flatcontainer/hl7.fhir.conformance/6.2.1/hl7.fhir.conformance.nuspec) / [6.7.0](https://api.nuget.org/v3-flatcontainer/hl7.fhir.conformance/6.7.0/hl7.fhir.conformance.nuspec) confirm model → Conformance → Base
minimums **[6.2.1, )** / **[6.7.0, )**. These are not equality constraints. The aliases isolate model assemblies,
not common Base/Conformance. Together with the supplier binary caveat, this preserves the authorized **single aligned cohort**;
no mixed original-model/new-common checkpoint was attempted and no universal lockstep rule is inferred.

Candidate archives for all six SDK packages contain `lib/net10.0`, `lib/net8.0`, `lib/netstandard2.1` DLLs, no net9 DLL,
and no `runtimes/`, `native/`, `build/` or `buildTransitive/` entries. Model filenames remain `Hl7.Fhir.R4/R4B/R5/R6.dll`.
Current graphs select net8 SDK DLLs for all three Candle TFMs; candidate net10 would select its new assets, net9/net8 use net8.
Base 6.7.0 requires Fhir.Metrics >=1.4.0 (current 1.3.1); Fhir.Metrics 1.4.0's net10/netstandard2.0 groups are dependency-free.
Base's Caching.Memory minimum rises 8.0.1 → **10.0.9 on net10**, remaining 8.0.1 on net8; Newtonsoft.Json stays >=13.0.4.
The net10 Annotations >=5.0.0 edge remains; net8 drops it, and Reflection.Emit.Lightweight is removed (already pruned from current graphs).
Recursive net10 Caching.Memory metadata requires Caching.Abstractions, DI.Abstractions, Logging.Abstractions, Options and Primitives >=10.0.9.
Their net10 edges close through those same packages; existing DI/Logging/Options/Primitives 10.0.13 satisfy these minima.
Stores currently have cache abstractions 8.0.0 / memory 8.0.1; Tests has abstractions 10.0.10 / memory 8.0.1; Host prunes these on net10.
This predicts affected minima, **not** resolved candidate versions or identical framework pruning.

Retained [Firely.Fhir.Packages 5.0.2 nuspec][packages] requires Base **[6.1.1, )**, permitting 6.7.0 without proving binary compatibility.
Its archive retains net8/netstandard2.1 DLLs and no native/runtime/build entries; existing Host/Tests use net8.
Other declared minima remain Newtonsoft.Json 13.0.4, SemanticVersioning 3.0.0, SharpZipLib 1.4.2,
System.Memory 4.6.3, System.Net.Http 4.3.4, Tasks.Extensions 4.6.3; the last three are pruned in current Host/Tests graphs.
`FhirPackageService`'s DiskPackageCache/PackageClient/manifest/recursive installation paths still require T-packages and real RI checks.
No need for a second direct pin is established by these ranges; no retained-binary pass is claimed. Phase 23 remains a separate assessment.

## Verification and disposition

Performed: read-only Git/source/procedure/cache checks, public supplier GETs, in-memory ZIP inspection, XML/complete graph checks, and document-link/whitespace checks.
Initial metadata attempts hit a gzip-decoding error and a TLS timeout; the bounded gzip-aware retry completed without TLS bypass.
All **46 direct declarations / 36 IDs / seven owners** remain unchanged, including conditions, aliases and assets; all seven SDK pins remain 6.2.1.
Before/after full package-only graph normalization reproduces **12 projects / 36 targets / 946 nodes, 387,301 bytes**:
SHA-256 `55bdad44a44eac1ad97e18493d6da1a944581ff267b73ad585cd84808fefd1e1`.
Normalization: sorted compact JSON mapping git-relative slash `csproj|target` to **full package-only nodes**, `ensure_ascii=False`, UTF-8, no final LF.
These are read-only existing assets, not a fresh restore. Host/Tests AddPackageAliases and test `coreR6` remain unchanged.
R4 `4.0.1`, R4B `4.3.0`, R5 `5.0.0` remain the product contracts; R6 remains test-only, never a host tenant.
Existing Pkcs NU1902/NU1903 baseline advisories remain unresolved, not suppressed or remeasured by a build.

**Not performed:** restore/build/tests, baseline or candidate B/T-sdk/T-packages, candidate graph resolution, browser actions or server/receiver startup.
Store/list/viewer/metadata JSON→XML→JSON, generated/received subscriptions and tours on all three tenants, R4 HTI-2,
CDex provider/payer Task controls and PAS shell all remain **not run**. No API-test result is substituted for browser evidence.
No source, pins, tests, fixtures, historical SDK envelope or plan changed; no helper was created or left running.

Reconsider only with a complete isolated offline cache for both exact RI packages and required locally resolved contents, and actual local browser access.
Use the documented empty-source/cache startup commands with registries disabled, synthetic version-shaped seeds and the real local subscription-hook receiver.
CDex browser Tasks must deliberately include `Task.code.Coding`; the API fixture's omission is not permission to repair the table.
Then revalidate availability/support/closure and run original/candidate **B and exact combined T-sdk**, retained **T-packages**, all-TFM graphs/aliases,
necessary focused target escalation, and every applicable manual gate with source/seed/browser/results/errors/shutdown identities.
Any additional direct pin, source owner or pre-existing repair requires separate authorization. Lower stable releases do not remove this local evidence gap.

[r4]: https://api.nuget.org/v3-flatcontainer/hl7.fhir.r4/index.json
[r4b]: https://api.nuget.org/v3-flatcontainer/hl7.fhir.r4b/index.json
[r5]: https://api.nuget.org/v3-flatcontainer/hl7.fhir.r5/index.json
[r6]: https://api.nuget.org/v3-flatcontainer/hl7.fhir.r6/index.json
[sdk-readme]: https://raw.githubusercontent.com/FirelyTeam/firely-net-sdk/v6.7.0/README.md
[migration]: https://raw.githubusercontent.com/wiki/FirelyTeam/firely-net-sdk/Breaking-changes-in-6.0.md
[sdk-63]: https://github.com/FirelyTeam/firely-net-sdk/releases/tag/v6.3.0
[sdk-64]: https://github.com/FirelyTeam/firely-net-sdk/releases/tag/v6.4.0
[sdk-65]: https://github.com/FirelyTeam/firely-net-sdk/releases/tag/v6.5.0
[sdk-66]: https://github.com/FirelyTeam/firely-net-sdk/releases/tag/v6.6.0
[sdk-67]: https://github.com/FirelyTeam/firely-net-sdk/releases/tag/v6.7.0
[packages]: https://api.nuget.org/v3-flatcontainer/firely.fhir.packages/5.0.2/firely.fhir.packages.nuspec
