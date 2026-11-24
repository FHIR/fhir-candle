# Microsoft.Extensions.Hosting.Abstractions — 2026-10-16

**Phase 8 Windows gates passed; parent integration/commit pending.** One candidate, one direct pin, two phase attempts total. Final full-suite and Linux/macOS validation remain pending.

## Scope and identity

Predecessor **`72a13ca36618737ab42d7c9cb2152007ab2d1f91`**; initial worktree/index clean. Guidance: [AGENTS.md](../../../AGENTS.md), [documented filter forms](../building-and-testing.md), and phase-8/unit/final requirements.
Only `FhirStore.Common/FhirCandle.Common.csproj`'s Hosting.Abstractions pin changes **10.0.9 → 10.0.13**. Other Microsoft.Extensions direct pins stay 10.0.13; all other declarations and `Program.cs` are unchanged.
`HostingCompatibilityTests.cs` adds the two required facts against the existing **`Program.RunServer`** boundary, not `BuildAppForTesting`. No production facade, registration rewrite, new runner, or duplicated notification-delivery matrix.
Final test-file SHA-256: **`35c88ef4bbafdc15acaa8fda83e95319dd2dbda3c36b30158ac1979849ca18a5`** (20,258 bytes). Common project: **`ef1549033069099567236ee5e0927ed13832cf0f43e95fbf98e407260cb8f0bd`**.
Raw source patch, excluding this document: **21,354 bytes**, SHA-256 **`759b5746df8f709adfcb88ae7c4199c92aeed5c717f5c53ed068030c46e092d3`**. Identity is the byte concatenation of these stdout streams, in order; the new-file diff's exit 1 means differences, not failure:
```powershell
git --no-pager diff --no-ext-diff --binary 72a13ca36618737ab42d7c9cb2152007ab2d1f91 -- src/FhirStore.Common/FhirCandle.Common.csproj src/fhir-candle/Program.cs
git --no-pager diff --no-ext-diff --binary --no-index -- NUL src/fhir-candle.Tests/HostingCompatibilityTests.cs
```

## Supplier and frozen single-ID unit

Revalidated the [version index](https://api.nuget.org/v3-flatcontainer/microsoft.extensions.hosting.abstractions/index.json) and [registration](https://api.nuget.org/v3/registration5-gz-semver2/microsoft.extensions.hosting.abstractions/10.0.13.json): **10.0.13 remains newest stable/listed**, published 2026-10-13.
[Original](https://api.nuget.org/v3-flatcontainer/microsoft.extensions.hosting.abstractions/10.0.9/microsoft.extensions.hosting.abstractions.nuspec) / [candidate](https://api.nuget.org/v3-flatcontainer/microsoft.extensions.hosting.abstractions/10.0.13/microsoft.extensions.hosting.abstractions.nuspec) nuspecs require Configuration, DependencyInjection, Diagnostics, FileProviders and Logging **Abstractions >=10.0.9 → >=10.0.13** on net8/net9/net10; no equality or upper bounds.
Diagnostics.Abstractions additionally raises Options to >=10.0.13; Diagnostics/Logging abstractions raise DiagnosticSource to >=10.0.13 on net8/net9. Options/FileProviders require Primitives >=10.0.13. Reviewed those supplier ranges and retained DI/Logging/Options.ConfigurationExtensions/OTel ranges: **no other direct co-update is required**.
Retained MCP 1.4.0 requires Hosting >=10.0.7; OTel.Hosting 1.16.0 requires >=10.0.0/net10, >=9.0.0/net9, >=8.0.0/net8. Retained FluentUI 4.14.3 requires >=10.0.9/net10, >=9.0.17/net9, >=8.0.1/net8. These lower bounds accept 10.0.13.
Downstream [MCP 2.2.0](https://api.nuget.org/v3-flatcontainer/modelcontextprotocol/2.2.0/modelcontextprotocol.nuspec) requires Hosting **>=10.0.10** on all three targets; ASP.NET Core MCP 2.2.0 requires protocol **[2.2.0]**. Hosting 10.0.13 satisfies that minimum, not the remaining phase-17 gates.
[FluentUI 4.14.5](https://api.nuget.org/v3-flatcontainer/microsoft.fluentui.aspnetcore.components/4.14.5/microsoft.fluentui.aspnetcore.components.nuspec), the phase-24 current-major fallback, requires Hosting **>=10.0.12 on net10** (>=9.0.19/net9, >=8.0.1/net8). FluentUI 5.0.1 instead declares Components.Web/Http >=10.0.12 on net10; do not attribute the fallback's direct Hosting edge to 5.0.1 or infer UI acceptance.
Reviewed package documentation and [October servicing notes](https://raw.githubusercontent.com/dotnet/core/main/release-notes/10.0/10.0.13/10.0.13.md). `ServiceCollectionHostedServiceExtensions`, `HostingAbstractionsHostExtensions`, `IHostedService`, and `IHostApplicationLifetime` source are byte-identical between supplier commits `901ca941248413c79832d2fdbd709da0c4386353` and `4099f83977f4d29604a9aeb971fb0a7bc1d5f699` in [dotnet/dotnet](https://github.com/dotnet/dotnet/tree/4099f83977f4d29604a9aeb971fb0a7bc1d5f699/src/runtime/src/libraries/Microsoft.Extensions.Hosting.Abstractions/src).
Both Hosting archives retain managed net8/net9/net10 DLL/XML assets. Candidate archive **232,236 bytes**, SHA-256 **`0bad01c257d0dbb11b744439e418458f5904ffc6d53ee2c93567ba46e427c190`**, matches the restored cache. No native/RID payload in any of the seven advancing packages; their selected net8 build assets are byte-identical.

## Fresh all-target closure and assets

B refreshed **12 assets files / 36 target graphs / 946 package nodes**. Direct inventory remains **36 IDs / 46 declarations / seven files**, including namespaced UI props.
Normalization: `<repository-relative csproj>|<TFM>` → full package-only target nodes; recursively sorted compact JSON, UTF-8, no terminal LF.
Before: **386,334 bytes**, SHA-256 **`396eaefb064d4b6b75ed6c7cde02982ccf47c22e558f3e6478a3638a0dea2a43`**; rechecked after pre-pin tests.
After: **386,811 bytes**, SHA-256 **`06c0659d2a30e4e33ad9b359c97a3395450d9c79cd0af1c8f3e7b84c196c209b`**; unchanged after all candidate tests.

| Package advancing 10.0.9 → 10.0.13 | Target-node occurrences |
|-|-|
| Hosting.Abstractions | 25 |
| DependencyInjection.Abstractions | 25 |
| Diagnostics.Abstractions | 25 |
| Logging.Abstractions | 25 |
| Options | 25 |
| FileProviders.Abstractions | 21 |
| System.Diagnostics.DiagnosticSource | 20 |

Exactly **166 version advances / 311 dependency-minimum changes / 780 identical nodes**, zero added/removed package IDs. Replacing changed nodes with the recorded pre-pin nodes reproduces the complete baseline hash, not merely a version summary.
Configuration.Abstractions/Primitives remain 10.0.13; Physical FileProviders remain 10.0.13 in four graphs. FileProviders.Abstractions was already 10.0.13 for host/tests net9/net8. Concrete DI/Logging/Diagnostics remain 10.0.9/net10, 9.0.17/net9, 8.0.1/net8 wherever present: higher abstraction minimums do not require those implementations to move.
Every selected compile/runtime/build/native/runtimeTargets/content field is unchanged. Verified **22 distinct real selected files, 20 related XML files, five placeholders** exist. Pruning, restore feeds/folders and assets diagnostics reproduce the baseline metadata hash after normalizing only Common's three direct Hosting ranges: **`daedd0e07b2f950cd6c7417305ae53581a4c1924c819168e21e984a367692fc4`**.
Host/net10 prunes this closure to shared frameworks; its Hosting DLL/deps entry is absent. Tests/net10 retains six candidate DLLs. Host/tests net9/net8 retain seven each; **all 34 copied DLLs match the candidate cache bytes and deps versions**. Host pruning ranges remain `(,10.0.32767]`, `(,9.0.32767]`, `(,8.0.32767]`.
The initial pre-pin failure's captured diagnostics explicitly loaded shared Hosting **10.0.11** on net10. Passing normal-verbosity runs do not print captured assembly locations; do not equate stand-alone host pruning with the test executable's different closure.

## Exact Windows commands and inventories

Windows **10.0.26200 x64**, SDK **10.0.400**, .NET/ASP.NET Core **10.0.11 / 9.0.19 / 8.0.30**; existing xUnit v2/VSTest adapter 3.1.5. Differing lower-TFM assemblies warranted the same documented focused form across all targets.
These four literal commands were executed; the net10 T-hosting command ran once before and once after the pin:
```powershell
dotnet test --configuration Release --framework net10.0 --no-restore --verbosity normal --filter "FullyQualifiedName~fhir.candle.Tests.HostingCompatibilityTests|FullyQualifiedName~fhir.candle.Tests.Sdk6NotificationTests"
dotnet test --configuration Release --no-restore --verbosity normal --filter "FullyQualifiedName~fhir.candle.Tests.HostingCompatibilityTests"
dotnet build --configuration Release --framework net10.0
dotnet test --configuration Release --no-restore --verbosity normal --filter "FullyQualifiedName~fhir.candle.Tests.HostingCompatibilityTests|FullyQualifiedName~fhir.candle.Tests.Sdk6NotificationTests"
```
| Gate, chronological | Result | MSBuild elapsed / warnings |
|-|-|-|
| Attempt 1, pre-pin net10 T-hosting | Exit 1: **38 passed / one new fact failed / zero skipped** | 74.63 s / eight existing + one new xUnit2031 |
| Attempt 2, corrected new class, pre-pin all targets | Exit 0: **2 × 3 passed**, zero failed/skipped | 62.43 s / 24 existing |
| Attempt 2, candidate B | Exit 0, zero errors | 5.93 s / eight existing |
| Attempt 2, candidate exact net10 T-hosting | Exit 0: **39/39 passed**, zero failed/skipped | 72.12 s / eight existing |
| Attempt 2, candidate all-target T-hosting | Exit 0: **39 × 3 passed**, zero failed/skipped | 83.10 s / 24 existing |

Final **39 distinct case names** hash **`6be59958264d4398cc81260f75da0aec35cf9cad96941d0125d121ce33131def`**; **117 occurrences** hash **`3e6fd45f648fba683fd37c19b24df00752daa292c00de581139f46845b50af86`**. Three 39-pass summaries, each complete name occurs three times; no unsupported per-framework hash attribution to interleaved output.
The **37 unchanged notification names** match the successful pre-pin notification subset: **`7380e01b04ef88b9861f9a52fdc75497d517063964cd5d8bfd6619d5b2c0da16`**; 111-occurrence hash **`92a70fccd0595e25503414124caedc1462c180f20d62ee83ea21a4fd38c79d94`**.
The **two new names** hash **`463bdc8f5d1e4513423d452a04b8e379ae2b160f92deac1f74314561278c1c01`**; six-occurrence hash **`41f22625654bdd9953a7b2dff36e76c2248b49bbbdacabcc69e4b100463f4304`**, identical before/after the pin. Hashes use complete names/parameters, ordinal sorting, LF joining, UTF-8, no terminal LF.
Final diagnostics are only existing Pkcs **NU1902/NU1903**, [GHSA-2fmv-rv2c-59x7](https://github.com/advisories/GHSA-2fmv-rv2c-59x7) / [GHSA-846f-f764-76mx](https://github.com/advisories/GHSA-846f-f764-76mx): unchanged 10.0.0/net10 and 8.0.1/net9/net8 closure. No final compiler/analyzer warning introduced; no warning suppression, lost cases, weakened assertions, or notification-test edits.

## Actual service lifetime and isolation

Both facts call the existing `RunServer` using a fresh CLI parser and empty external configuration. A private, execution-context-scoped Hosting diagnostic observer captures its real host; the four singleton and `AddHostedService(sp => sp.GetRequiredService<...>())` registrations are never replaced.
Assertions observe exactly one concrete store/notification/package/SMART singleton, identical through hosted enumeration, repeated interface resolution and a new DI scope. Each actual service emits its start message exactly once; source/cache stay empty and captured start/stop logs contain no warning/error.
The lifecycle fact observes three empty R4/R4B/R5 stores, initialized package service and SMART configurations, and real loopback Kestrel GETs: `/fhir/hosting-r4/metadata` **200/4.0.1**, `hosting-r4b` **200/4.3.0**, `hosting-r5` **200/5.0.0**, with exact tenant canonical URLs.
Cancellation of **RunServer's own optional token** completes `WaitForShutdownAsync`, `ApplicationStopping`, `ApplicationStopped`, and the runner task before disposal. All four references remain identical; the same notification singleton rejects restart as stopped, joins repeated Stop, then rejects restart as disposed after cleanup. Package/SMART Stop implementations are no-ops; no invented per-service stop counter is claimed.
The existing **37 `Sdk6NotificationTests`** supply real REST-hook delivery plus held-send drain/cancel/disposal coverage; the new facts do not duplicate that matrix or claim SMTP/Zulip coverage.
Isolation: serialized existing host collection; owned empty TEMP source/cache/content root; Production environment with six host variables restored; no package inputs, official/additional registries, browser launch, external accounts or telemetry endpoints. Host-building configuration restricts Kestrel to an ephemeral loopback HTTP port and captures logging.
DataProtection's separate hosted warmup is redirected to owned TEMP keys encrypted with a freshly generated in-memory certificate, not merely an ephemeral provider. No TLS trust policy changes or real key/secret-store reads. `RunServer` retains its existing default key-directory existence/create check; that directory is not a fixture deletion target.
Every wait/HTTP operation has a 30-second bound; shutdown, host/provider disposal, stores, diagnostic subscriptions, certificate/key and fixture directories are cleaned in finally paths. All build/test sessions exited without forced termination; no helper server process was launched or killed. Final probe found **zero `fhir-candle-sdk6-package-*` TEMP roots**.

## Failed gate, bounded correction, and limitations

Attempt 1's test source hash was **`438d01f37f355a2c4ae305998a283006fd5677cbc7ae5f4a518210741180bd44`**. Registration passed and all 37 notification cases passed; lifecycle stopped at `Assert.True(smart.IsEnabled)`. Read-only inspection showed `CandleConfig` does not copy its SMART CLI options into the constructed configuration: the fixture had not enabled SMART.
Attempt 2 supplies synthetic `SmartAllowed` flags on the already-created test-owned tenant dictionary at HostBuilding, without changing the production registrations or any SMART assertion. This exercises real SMART initialization, **not SMART CLI semantics**; the unrelated constructor behavior is not repaired or blamed on Hosting. `Assert.Single`'s predicate overload fixes xUnit2031 without suppressing it. No third attempt or second package candidate was used.
Read-only probe corrections: nonexistent standalone `Configuration/CliOptions.cs` lookup (exit 1; actual types share CandleConfig.cs); namespace-blind inventory initially omitted four UI refs; initial pruning lookup used the wrong key. Corrected namespace-aware inventory and actual `packagesToPrune`/metadata checks supply the results above. These were not product retries.
The final style probe also exited 1 by requiring an EOF newline in the pre-existing Common project. HEAD already lacks that newline; a corrected, line-ending-normalized comparison proves the pin is its only change. Both new files have CRLF, final newlines and no trailing whitespace; the original project EOF is preserved.
Only the three owned deliverables changed; no plan/skills edit, staging, commit, installation, publication, authored output/log file or diagnostic-worktree search. Tool-managed response spools are retained. Checks used stdout/in-memory metadata plus generated build/restore and owned fixture artifacts.
Scope limits: controlled in-process **production boundary**, not a separate CLI process launch, browser/UI acceptance, real secrets/accounts, or full-suite/cross-platform certification. The recorded host-building fixture overrides are not evidence for untouched default configuration or SMART CLI precedence.
Rollback is the complete unit (pin, new tests, this record), followed by B and focused graph/lifetime verification. Do not independently lower required abstraction prerequisites or assume a later MCP/FluentUI minimum permits removing this checkpoint. Final Windows full-suite and post-pass Linux/macOS gates remain outstanding.
