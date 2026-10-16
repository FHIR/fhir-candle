# Microsoft.Extensions.Configuration.UserSecrets — 2026-10-16

**Phase 7 candidate gates passed; parent acceptance/commit pending.** One package candidate, no production adaptation. Verification-wrapper failures and retries are retained below.

## Identity and actual absence boundary

Predecessor **`71fb959d3c9c533c3ffcb35b7b64baf4a185e363`**; initial worktree/index clean. Reused its accepted 107-case / 321-all-target baseline without rerunning unchanged cases before the pin.
Only the host UserSecrets declaration changes **10.0.9 → 10.0.13**. Core/Binder/environment/Json stay 10.0.13; all other declarations remain unchanged.
`ConfigTests.cs` adds 16 lines: one `OptionalUserSecretsConfigurationPreservesMissingAttributeBehavior` fact and two imports. A fresh collectible in-memory assembly has no secret ID; optional, reload-enabled `AddUserSecrets` must return the same builder with no sources. No file, secret-store path, environment mutation, or unrelated provider semantics are introduced.
This case is material because the existing 107 cases do not exercise the default builder's missing-assembly-attribute boundary, and lower TFMs select real UserSecrets package DLLs. It passed on all three targets **before** the pin changed; no existing assertion changed.
Read-only inspection: `Program.Main` builds optional appsettings JSON → `Candle`-prefixed environment → unprefixed environment; parsed CLI selection constructs `CandleConfig` before `RunServer` calls `WebApplication.CreateBuilder`. There is no explicit production `AddUserSecrets` call or configured secret ID.
The default builder loads optional assembly-based UserSecrets in Development. Without `UserSecretsIdAttribute`, the extension returns before path resolution; this is attribute absence, not a claim about any real secret file's existence.
PE metadata inspection found **zero** such attributes in host Release net10/net9/net8 and Debug net10 before trial, and in all rebuilt host/test Release assemblies plus the actual Debug startup assembly afterward. Generated assembly metadata also lacked the attribute. No real secrets were inspected.
Raw code patch, excluding this document: **2,076 bytes**, SHA-256 **`2f4f938e294d8bc0d56b4d2361cd53a386aedc7650be3ba7b63029b362be145c`**:
```powershell
git --no-pager diff --no-ext-diff --binary 71fb959d3c9c533c3ffcb35b7b64baf4a185e363 -- src/fhir-candle/fhir-candle.csproj src/fhir-candle.Tests/ConfigTests.cs
```

## Supplier and independent unit

Revalidated the [version index](https://api.nuget.org/v3-flatcontainer/microsoft.extensions.configuration.usersecrets/index.json) and [registration](https://api.nuget.org/v3/registration5-gz-semver2/microsoft.extensions.configuration.usersecrets/10.0.13.json): **10.0.13** remains highest stable/listed, published 2026-10-13.
[Original nuspec](https://api.nuget.org/v3-flatcontainer/microsoft.extensions.configuration.usersecrets/10.0.9/microsoft.extensions.configuration.usersecrets.nuspec) / [candidate nuspec](https://api.nuget.org/v3-flatcontainer/microsoft.extensions.configuration.usersecrets/10.0.13/microsoft.extensions.configuration.usersecrets.nuspec): net8/net9/net10 require Configuration.Abstractions, Configuration.Json, FileProviders.Abstractions and FileProviders.Physical **>=10.0.9 → >=10.0.13**, with no equality/upper-bound constraint.
Re-read Json/FileExtensions/FileProviders/Primitives/FileSystemGlobbing and retained Hosting.Abstractions ranges. The accepted JSON/core closure already supplies every candidate minimum; no reverse package constraint requires another direct update. **Single direct-ID unit**, dependent on the accepted JSON checkpoint.
Reviewed package documentation and [October servicing notes](https://raw.githubusercontent.com/dotnet/core/main/release-notes/10.0/10.0.13/10.0.13.md). `PathHelper.cs`, `UserSecretsConfigurationExtensions.cs` and `UserSecretsIdAttribute.cs` are byte-identical between nuspec commits `901ca941248413c79832d2fdbd709da0c4386353` and `4099f83977f4d29604a9aeb971fb0a7bc1d5f699` in [supplier source](https://github.com/dotnet/dotnet/tree/4099f83977f4d29604a9aeb971fb0a7bc1d5f699/src/runtime/src/libraries/Microsoft.Extensions.Configuration.UserSecrets/src).
Inspected [default hosting configuration](https://github.com/dotnet/dotnet/blob/4099f83977f4d29604a9aeb971fb0a7bc1d5f699/src/runtime/src/libraries/Microsoft.Extensions.Hosting/src/HostingHostBuilderExtensions.cs) and WebApplicationBuilder source; effective defaults need no production adjustment.
Both archives retain managed DLL/XML assets for net8/net9/net10 and no native/RID payload. Selected `buildTransitive/net8.0` props/targets are byte-identical: LocalUserSecrets capability and attribute generation only when `UserSecretsId` is nonempty. Unselected netcoreapp2.0/net461 warning targets differ only in version text; no suppression added.
Candidate archive SHA-256 **`468f0b5ccc7c2ed9ead0e438f9dc629b92795a8d515e02b119f9f3bcefe88b05`** matches the restored cache. Archive path differences are only generated `.psmdcp` names.

## Complete graph, assets and diagnostics

B refreshed **12 assets files / 36 targets / 946 package nodes**. Direct inventory stays **36 IDs / 46 declarations / seven files**.
Normalization: `<repository-relative csproj>|<TFM>` → full package-only target nodes, recursively sorted compact JSON, UTF-8, no terminal LF.
Before: **386,313 bytes**, SHA-256 **`df3a9ce6e1b65310c8145315886775c2d01606019459252ddb062f4e254adad3`**.
After: **386,334 bytes**, SHA-256 **`396eaefb064d4b6b75ed6c7cde02982ccf47c22e558f3e6478a3638a0dea2a43`**; unchanged after tests and startups.
Exactly **five UserSecrets version advances**, **16 dependency-minimum advances**, **941 identical full nodes**, zero added/removed package IDs. Every selected compile/runtime/build/native/runtimeTargets/content field is unchanged; only UserSecrets library version paths, content hashes and generated metadata filename change.
Host/net10 retains `lib/net10.0/_._` and `buildTransitive/net8.0/_._`, with dependencies pruned. Tests/net10 has no UserSecrets node. Host and Tests × net9/net8 select the corresponding real DLL and net8 props/targets; the other 31 graphs have no UserSecrets node.
Verified **four distinct selected real files + two companion XML files** exist; generated lower-TFM imports select 10.0.13. Four copied host/test lower-TFM DLLs exactly match package bytes; their deps select 10.0.13. Host/test net10 deps omit the package.
All pruning maps, feeds (nuget.org / Visual Studio Offline Packages), package folders and assets diagnostics are unchanged. Host prune ranges remain `(,10.0.32767]`, `(,9.0.32767]`, `(,8.0.32767]`.
Only existing Pkcs **NU1902** [GHSA-2fmv-rv2c-59x7](https://github.com/advisories/GHSA-2fmv-rv2c-59x7) and **NU1903** [GHSA-846f-f764-76mx](https://github.com/advisories/GHSA-846f-f764-76mx): unchanged MailKit/MimeKit closure resolves Pkcs 10.0.0 on net10, 8.0.1 on net9/net8. No new compiler/analyzer diagnostic or hidden warning.

## Exact Windows verification

Authority: [AGENTS.md](../../../AGENTS.md), [related-filter form](../building-and-testing.md), phase-7 B/T-config/V-accept and the documented all-target focused form. `dotnet --info`: Windows **10.0.26200 x64**, SDK **10.0.400**, .NET/ASP.NET Core **10.0.11 / 9.0.19 / 8.0.30**; xUnit VSTest adapter **3.1.5+1b188a7b0a**.
Differing lower-TFM DLLs/build assets warranted the same focused filter without `--framework`; this is not a full-suite claim.
```powershell
dotnet test --configuration Release --no-restore --verbosity normal --filter "FullyQualifiedName~fhir.candle.Tests.ConfigTests.OptionalUserSecretsConfigurationPreservesMissingAttributeBehavior"
dotnet build --configuration Release --framework net10.0
dotnet test --configuration Release --framework net10.0 --no-restore --verbosity normal --filter "FullyQualifiedName~fhir.candle.Tests.ConfigTests|FullyQualifiedName~fhir.candle.Tests.StrictModeStartupTests"
dotnet test --configuration Release --no-restore --verbosity normal --filter "FullyQualifiedName~fhir.candle.Tests.ConfigTests|FullyQualifiedName~fhir.candle.Tests.StrictModeStartupTests"
```
| Gate, chronological | Executions / result | MSBuild elapsed |
|-|-|-|
| New absence fact, pre-pin | Once; exit 0; **1 × 3 passed**, 24 existing warnings | 13.63 s |
| Candidate B | Once; exit 0; **8 existing warnings, zero errors** | 4.70 s |
| Candidate net10 T-config | Once; exit 0; **108/108 passed**, 8 existing warnings | 5.83 s |
| Candidate all-target focused | Twice; each command exit 0, **108 × 3 passed**, 24 existing warnings; first inventory wrapper failed | 12.08 s / 10.92 s |
All test runs report zero failed/skipped cases. Final inventory is **99 Config + nine Strict = 108** names, SHA-256 **`7683bac4a3ce03256961200bedb65b356d397bb3dd8ebbecc39c6542bc12c92d`**.
Preserved 107-name hash **`5702bd8dfdc008b90d151abfad40af56d938ae3d272768e3e5b4bdddfd10a146`** matches the predecessor. New one-name hash **`4c94d4ba246e697e71d08d9fe8219d8bb844a5b66895f6dbd6b3352dc581ca6e`** matches pre-pin.
Final all-target stdout has three 108-pass summaries and each exact name occurs three times: **324-occurrence hash `9e22d7193da7c7c5dfff85ce90e085587df0bfb4f262462ff68dfa31b31457fc`**; retained 321 hash **`b539daf763344e369f9a59e7d8be10b1d9649bec38b851c23f841f9e70916b1d`**; new three-occurrence hash **`f5451400b7690c2ecaf394359f358f38f83dad26b23ef2430839ba44e7834f46`** matches pre-pin.
Name hashes: complete names/parameters, ordinal-sorted, LF-joined, UTF-8 without terminal LF. Framework output interleaves; no unsupported per-framework case hash/time attribution or numeric discovery-total claim. No new owned JSON-test TEMP directories remained.

## Actual Development startup and cleanup

The optional default-builder absence boundary warranted the [existing isolated startup procedure](../sdk6-compatibility.md#isolated-startup). Each launch used a fresh owned TEMP root, empty source/cache, no package/resource inputs and no real settings edits.
Child-only synthetic environment: `DOTNET_ENVIRONMENT=Development`, `CandleTenants_R4__0=prefixed-r4`, `Tenants_R4__0=external-r4`, `CandleTenants_R4B__0=prefixed-r4b`, `CandleTenants_R5__0=prefixed-r5`, `Tenants_R5__0=external-r5`, `CandleListen_Port=5828`, `Listen_Port=5827`. Unrelated configuration was excluded from the child copy; repository appsettings were screened by key/type without copying values.
```powershell
dotnet run --project src\fhir-candle\fhir-candle.csproj --framework net10.0 -- --r5 cli-r5 --fhir-source "$root\source" --fhir-package-cache "$root\cache" --use-official-registries false --open-browser false --port 5826
```
Final actual launch: wrapper **exit 0**, readiness observed at **15.881 s** (includes PID inspection), Development confirmed. GET `http://localhost:5826/fhir/{tenant}/metadata`, `Accept: application/fhir+json`, explicitly UTF-8-decoded bytes: **external-r4 200/4.0.1; prefixed-r4b 200/4.3.0; cli-r5 200/5.0.0**. Each exact canonical is `http://localhost:5826/fhir/{tenant}/CapabilityStatement/metadata`.
`prefixed-r4` and `external-r5` each returned 404, with exactly their two `GetMetadata <<< no tenant at {tenant}!` failure logs. No unexpected runtime warning/error/URL-parse diagnostic; four service-start messages each occurred once, SMTP/Zulip absent, source/cache remained empty. Only the four existing Pkcs build diagnostic variants repeated across restore/build.
Both launches actually loaded UserSecrets from shared ASP.NET Core **10.0.11**, file version **10.0.1126.37416**, not package 10.0.13. Lower-TFM candidate DLL execution is supplied by the focused absence fact, not this net10 startup.
Final run parent **134000**, application **134224**: `Stop-Process -Id <explicit observed PID list> -Force` terminated **47 live owned descendants**, leaves first. Parent exit **-1** captured; all **48 tracked PIDs** absent, TEMP root removed, port 5826 bindable, parent environment unchanged. Attached helper completed. **Not graceful shutdown or hosted-service lifecycle proof.**

## Failures, limits and rollback

All-target run 1 emitted three successful 108-case summaries, but its strict-line stdout wrapper extracted **323**, not 324, and exited 1. That attempt's incomplete inventory is not accepted; raw capture was not retained after wrapper exit, so the missing record's cause is unproven. The unchanged command ran once more; both strict-line and position-based extraction produced the exact 324-name inventory above. No assertion, source, filter or diagnostic flag was weakened.
Startup-wrapper attempts **three total / two launches**: attempt 1 rejected before execution because dynamic PID cleanup was not accepted by the tool; attempt 2 passed all five HTTP checks (readiness observed 87.096 s) but wrapper exit 1 at final port bind, so runtime diagnostic/parent-exit results were not recovered. Its parent **118312** / application **128360** and all **62 tracked PIDs** were independently confirmed absent after 47 live descendants were explicitly stopped; TEMP removed and port subsequently bindable. Attempt 3 is the complete passing result above.
Other probe failures: wrong `configuration-environment.md` lookup; gzip registration initially decoded as UTF-8 (exit 1), corrected with decompression; one read-only grep found no matches (exit 1); a smart-apostrophe PowerShell parse error prevented a cleanup/metadata probe from executing, then corrected. No build/test command failed and no second package candidate was tried.
Validation stdout/stderr was captured in memory. Four tool-managed oversized-response TEMP spools were removed; no authored log/output files, installations, publishing, plan/skills edits, staging or commits. No real user-secret file or scratch diagnostic worktree was searched.
Rollback boundary: revert this complete unit, restore with B and repeat graph/focused/absence-startup verification. Retaining this candidate requires Json/Abstractions/FileProviders >=10.0.13; do not independently lower those prerequisites. Unlocked resolution is not an arbitrary rollback guarantee.
Windows focused execution covers all three TFMs; startup is net10 only. Final full-suite acceptance and post-pass Linux/macOS validation remain pending. No browser, packaged-asset, real-secret-store, graceful-lifecycle or cross-platform certification. Only the three owned files changed.
