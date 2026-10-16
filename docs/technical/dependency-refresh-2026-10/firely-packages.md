# Firely.Fhir.Packages — deferred, 5.0.2 retained

**2026-10-16, phase 23 Windows-local assessment: retain 5.0.2; no candidate trial or accepted upgrade.**
Predecessor/source: `e088f73b1f7f594d61da9c1ad33d80aeaeb33e72`; index and tracked tree were clean.
Instructions: [AGENTS.md](../../../AGENTS.md), phase 23, unit contract and V-accept.
The [SDK disposition](hl7-fhir-sdk.md) retains the aligned **6.2.1** model/Conformance/Base cohort.
Every newer stable package release requires a higher common Base; supported mixed retained-model/new-Base binaries are not established.
The same mandatory offline RI/browser evidence gap also remains material to package/cache/loading consumers.
This is an evidence/prerequisite cap, **not demonstrated runtime incompatibility, a universal lockstep rule, or a non-Windows blocker**.
No source, pin, test, fixture or [historical accepted envelope](../sdk6-compatibility.md) changes are warranted.

## Fresh availability and support review

Public [NuGet index][versions] and registration/catalog GETs revalidate the following stable, listed releases:

| Version | NuGet publication (UTC) | Base minimum, all declared TFM groups | Eligibility here |
|---|---|---|---|
| **5.0.2** original/retained | 2026-04-13 | **6.1.1** | Highest stable in original major 5; retained with existing 6.2.1 closure. |
| [6.0.0](https://api.nuget.org/v3-flatcontainer/firely.fhir.packages/6.0.0/firely.fhir.packages.nuspec) lower major candidate | 2026-08-06 | **6.4.0** | Does not preserve the retained common assembly boundary. |
| [6.1.0](https://api.nuget.org/v3-flatcontainer/firely.fhir.packages/6.1.0/firely.fhir.packages.nuspec) lower candidate | 2026-09-14 | **6.5.0** | Same unresolved boundary and local evidence gap. |
| **6.2.0** highest stable | 2026-10-07 | **6.6.0** | Assessment input only, not a selected/resolved candidate. |

No higher 5.x patch exists in that index; older 5.x releases are not upgrades. No preview was selected.
Registration form: `https://api.nuget.org/v3/registration5-gz-semver2/firely.fhir.packages/<version>.json`.
All four catalog entries had no deprecation/vulnerability entries; this is not a recursive security audit.
[Package support README][readme] offers GitHub/Zulip support, not a per-patch LTS or mixed-SDK binary guarantee.
Both the [retained SDK README][sdk-retained] and [6.6 SDK README][sdk-candidate] promise effort toward compile, **not binary**, compatibility between minors;
their binary-check section permits exceptions. Lower 6.x minima do not resolve that uncertainty.
The package's tagged `ReleaseNotes.md` still describes version 5; the actual [6.0][release60], [6.1][release61], [6.2][release62] release pages supplied migration review.

## Actual retained closure versus conditional candidate requirements

Fresh [5.0.2][nuspec5] / [6.2.0][nuspec6] nuspecs identify repository commits
`25d3e80090046453acdb392396055bc92be3fb0f` / `fa41be0382d9fb98b118d6f0e45e8af008027244`.
In-memory archive inspection: 5.0.2 has net8/netstandard2.1 DLLs; 6.2.0 adds net10, with no net9 DLL.
Neither archive contains `runtimes/`, `native/`, `build/` or `buildTransitive/` assets.
Current Host/Tests select **net8** package and SDK assets on all three TFMs; candidate net10 would select net10, net9/net8 use net8.
Actual Host/Tests nodes: Packages **5.0.2**, Base/Conformance/models **6.2.1**, Fhir.Metrics **1.3.1**,
Newtonsoft.Json **13.0.4**, SemanticVersioning **3.0.0**, SharpZipLib **1.4.2**; R6 remains test-only.
Read-only PE metadata confirms retained model DLLs reference Base and Conformance **6.2.1.0**;
Conformance references Base **6.2.1.0**, while Packages **5.0.2.0** references Base **6.1.1.0**.
This existing accepted combination is not proof of support for a further common-assembly substitution.
Fresh retained model → Conformance → Base nuspec minima are **[6.2.1, )**, not exact constraints.
Aliases select model assemblies, not separate common assemblies; no Base uplift behind retained models or hidden SDK pin was attempted.

The candidate keeps Newtonsoft/SemanticVersioning/SharpZipLib minima unchanged and removes System.Memory **4.6.3**,
System.Net.Http **4.3.4**, Tasks.Extensions **4.6.3** dependencies (already absent/pruned in current Host/Tests graphs).
[Base 6.6.0 metadata][base66] requires Fhir.Metrics >=**1.4.0** (dependency-free net10/netstandard2.0 groups),
Caching.Memory >=**10.0.9 on net10**, >=**8.0.1 on net8**, and Newtonsoft >=13.0.4.
Its net10 Annotations >=5.0.0 remains; net8 drops it; Reflection.Emit.Lightweight is removed.
Caching.Memory 10.0.9 requires cache abstractions, DI/logging abstractions, Options and Primitives >=10.0.9.
Current Tests have cache abstractions 10.0.10 / memory 8.0.1; Host prunes memory (and net10 cache abstractions).
These are conditional minima, **not a candidate restore, resolved closure, pruning prediction or runtime result**.

## Migration mapped to current consumers

Read the service/interface, cache/CI/manifest adapters, `FhirStoreManager.LoadRequestedPackages`, registrations and existing package tests.
The [candidate reference source][reference] changes `Name` from field to property with obsolete setter, lowercases constructor name/scope,
and changes the three-argument constructor to four arguments with optional alias. Current CI construction needs recompilation, not an asserted compile failure.
Compared with original source, equality/hash now include case-insensitive scope/name and case-sensitive version; **Alias is not included** in these overrides.
Thus the 6.0 release's general struct-equality warning is not substituted for inspection of the exact 6.2.0 implementation.

| Supplier change | Existing behavior / future verification boundary |
|---|---|
| 6.0 multi-version closure, lock-file v2 lists, newer-lock rejection; 5.x cannot read new locks | No direct `PackageClosure`, lock-file or `FhirPackageSource` calls found in Candle. Do not invent a lock migration; isolate shared caches and assess interoperability if these APIs become applicable. |
| 6.0 canonical best-candidate default becomes true; core extension/tool lists float at latest | No direct canonical/core-source calls found. Preserve explicit RI versions; do not introduce floating/prerelease resolution. |
| 6.1 lowercase names, version-pattern/build-metadata fixes; injectable authenticated source client | Protect directives, registry URLs, monikers, CI tags/resolved dates and repeat/warm-cache behavior; no new authentication facility needed here. |
| 6.2 configurable root and dependency mapper | `FhirPackageService.Init` calls supplier `Platform.GetFhirPackageRoot` only when config is null; empty disables, explicit root passes to cache. New process property/environment override and no-profile fallback would change null-config behavior, not explicit isolated fixtures. Do not opt into a mapper. |

Current service converts `#` to `@`, deduplicates monikers, handles latest/local/CI, and recurses for FHIR-version variants, not a general manifest-dependency restore.
The adapter reads manifests with unknown fields ignored and existing author/FHIR-version fallbacks; CI replaces older content under its tag using resolved downloads.
Manager routes installed content by declared FHIR version and preserves tenant directories. No architecture or pre-existing behavior repair was attempted.
Preserve `LocalPackageAcquisitionCacheAndLoadingPreserveResources`, `CiPackageClientPreservesListingDownloadAndManifestContract`,
`InitialDataUsesTenantDirectories`, and `FhirNpmPackageTests`; their synthetic local fixtures are not official RI packages or browser evidence.

## Shared material prerequisite and disposition

Use **attributed phase-19 evidence at the predecessor above**, not a duplicated cache investigation: backport **1.1.0 missing** in the checked layouts;
CDex **2.0.0 present**, but hrex **1.0.0**, pas **1.2.0-ballot**, carin-bb **2.0.0**, dtr **1.1.0-ballot** dependency manifests missing there.
The [SDK dossier](hl7-fhir-sdk.md#material-local-prerequisite--rechecked-before-pins-or-tests) records exact scope, manifest hash and limitations;
presence of other dependencies was not recursive validation. No new complete cache or actual browser result was supplied for this dispatch.
Re-read the [browser procedures](../sdk6-compatibility.md#browser-checklist) and [subscriptions RI](../../user/subscriptions-ri.md); the unchanged prerequisites remain applicable.
Store/list/viewer/metadata JSON→XML→JSON, generated/received subscriptions, all-version tours, R4 HTI-2, CDex Task controls and PAS shell remain **not run**.
Installed browser binaries, synthetic API tests and installed package files are not substitutes for these observations.

Performed: read-only Git/source/PE inspection, public supplier GETs, in-memory ZIP inspection, XML/complete graph and document-link/whitespace checks.
All **46 declarations / 36 IDs / seven owners** remain unchanged, including aliases, conditions and assets; all seven direct SDK pins remain 6.2.1.
Before/after existing-assets normalization: **12 projects / 36 targets / 946 package nodes / 387,301 bytes**,
SHA-256 **`55bdad44a44eac1ad97e18493d6da1a944581ff267b73ad585cd84808fefd1e1`**.
Format: sorted compact JSON, git-relative slash `csproj|target` → full package-only nodes, `ensure_ascii=False`, UTF-8, no final LF.
**Not performed:** restore/build/test, baseline or candidate B/T-packages, candidate graph resolution, server/receiver/browser startup, or manual gates.
No new warning/test-pass claim; existing Pkcs advisories remain unresolved and were not remeasured. No helper, download file or fixture was created.
Unblock through separately authorized acceptance of an aligned SDK cohort meeting the minimum, or a new stable package release supported on the retained cohort; complete isolated offline RI inputs and actual browser access remain required.
Then revalidate releases/support/migration, run original/candidate **B + exact combined T-packages**, all-TFM fresh graphs and applicable SDK escalation,
and real browser/RI gates with synthetic version-shaped seeds, registries disabled, local receiver, identities/results/errors/shutdown recorded.
CDex browser Tasks must deliberately include `Task.code.Coding`; no silent table repair. Any extra owner/direct pin or prerequisite repair requires authorization before edits.
Final Windows assembled-tree verification remains pending; Linux/macOS all three TFMs remain post-Windows-pass follow-up, not the retention cause.

[versions]: https://api.nuget.org/v3-flatcontainer/firely.fhir.packages/index.json
[nuspec5]: https://api.nuget.org/v3-flatcontainer/firely.fhir.packages/5.0.2/firely.fhir.packages.nuspec
[nuspec6]: https://api.nuget.org/v3-flatcontainer/firely.fhir.packages/6.2.0/firely.fhir.packages.nuspec
[base66]: https://api.nuget.org/v3-flatcontainer/hl7.fhir.base/6.6.0/hl7.fhir.base.nuspec
[readme]: https://raw.githubusercontent.com/FirelyTeam/Firely.Fhir.Packages/fa41be0382d9fb98b118d6f0e45e8af008027244/readme.md
[reference]: https://raw.githubusercontent.com/FirelyTeam/Firely.Fhir.Packages/fa41be0382d9fb98b118d6f0e45e8af008027244/Firely.Fhir.Packages/Models/PackageReference.cs
[sdk-retained]: https://raw.githubusercontent.com/FirelyTeam/firely-net-sdk/v6.2.1/README.md
[sdk-candidate]: https://raw.githubusercontent.com/FirelyTeam/firely-net-sdk/v6.6.0/README.md
[release60]: https://github.com/FirelyTeam/Firely.Fhir.Packages/releases/tag/v6.0.0
[release61]: https://github.com/FirelyTeam/Firely.Fhir.Packages/releases/tag/v6.1.0
[release62]: https://github.com/FirelyTeam/Firely.Fhir.Packages/releases/tag/v6.2.0
