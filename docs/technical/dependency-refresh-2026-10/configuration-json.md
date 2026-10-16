# Microsoft.Extensions.Configuration.Json — 2026-10-16

**Phase 6 candidate gates passed; parent acceptance/commit pending.** One package trial; startup passed on its second check. No production adaptation.

## Identity and preserved contract

Predecessor **`a5bb1cd743f55d8781bc3c50fd06ff16d395c894`**; initial worktree/index clean. Reused its accepted 103-case baseline without rerunning it before the pin.
Only the host Json declaration changes **10.0.9 → 10.0.13**. Core/Binder/environment remain 10.0.13; UserSecrets remains 10.0.9. No other pin changes.
`ConfigTests.cs` adds 64 lines: `OptionalJsonConfigurationPreservesMissingFileBehavior` (one fact) and `OptionalJsonConfigurationPreservesParseErrors` (three rows). All previous expectations remain unchanged.
Missing optional JSON produces no keys or bound model. Existing-file errors retain `InvalidDataException` → `FormatException`: malformed `{"Listen_Port":` additionally contains a `JsonException`; root `[]` and duplicate `{"Listen_Port":8090,"listen_port":8091}` have no nested exception.
Each case owns a fresh TEMP directory and disposes its physical file provider and successfully built configuration. The error case explicitly disposes its JSON provider when root construction throws; directory removal is in `finally`. No real appsettings or user data is a fixture; reload remains disabled.
Raw code patch: **4,015 bytes**, SHA-256 **`96e68a9f9541019b3b46574480372d159d5031b757f694d936ea019513685597`**, excluding this document:
```powershell
git --no-pager diff --no-ext-diff --binary a5bb1cd743f55d8781bc3c50fd06ff16d395c894 -- src\fhir-candle\fhir-candle.csproj src\fhir-candle.Tests\ConfigTests.cs
```

## Supplier metadata and unit decision

[Version index](https://api.nuget.org/v3-flatcontainer/microsoft.extensions.configuration.json/index.json) and [registration](https://api.nuget.org/v3/registration5-gz-semver2/microsoft.extensions.configuration.json/10.0.13.json) revalidated **10.0.13**, highest stable/listed, published 2026-10-13.
Reviewed [October servicing notes](https://raw.githubusercontent.com/dotnet/core/main/release-notes/10.0/10.0.13/10.0.13.md), package documentation, and original/candidate nuspecs. These are availability/support inputs, not execution evidence.

| Applicable supplier constraint | Minimum at candidate |
|-|-|
| [Json](https://api.nuget.org/v3-flatcontainer/microsoft.extensions.configuration.json/10.0.13/microsoft.extensions.configuration.json.nuspec), net8/net9/net10 | Configuration, Configuration.Abstractions, Configuration.FileExtensions, FileProviders.Abstractions **>=10.0.13** |
| Json, net8/net9 only | System.Text.Json **>=10.0.13** |
| [FileExtensions](https://api.nuget.org/v3-flatcontainer/microsoft.extensions.configuration.fileextensions/10.0.13/microsoft.extensions.configuration.fileextensions.nuspec), all three | Configuration, Configuration.Abstractions, FileProviders.Abstractions, FileProviders.Physical, Primitives **>=10.0.13** |
| FileProviders.Abstractions / Physical | Primitives >=10.0.13; Physical also FileProviders.Abstractions/FileSystemGlobbing >=10.0.13 |
| [System.Text.Json](https://api.nuget.org/v3-flatcontainer/system.text.json/10.0.13/system.text.json.nuspec), net8/net9 | System.IO.Pipelines and System.Text.Encodings.Web **>=10.0.13**; their applicable groups have no further dependencies |

These were >=10.0.9 at the original versions, not equality constraints. Retained UserSecrets/Hosting.Abstractions 10.0.9 impose compatible >=10.0.9 minima; all restored reverse constraints, including MCP/AI/TestHost, remain satisfied. Accepted core/Abstractions/Primitives supply >=10.0.13. **Single direct-ID unit; no required co-update.**
Compared the four JSON source files and FileExtensions' `FileConfigurationProvider.cs` at nuspec commits `901ca941248413c79832d2fdbd709da0c4386353` / `4099f83977f4d29604a9aeb971fb0a7bc1d5f699` in [supplier source](https://github.com/dotnet/dotnet/tree/4099f83977f4d29604a9aeb971fb0a7bc1d5f699/src/runtime/src/libraries): byte-identical optional/error implementation.
All eight affected archives retain real net8/net9/net10 managed assets, with no native payload/path change. Json candidate archive SHA-256: `a8dadfeb0c4f0548dcf510a072ce8df5a30dfa32ab08d561f4f25afafebcfecb`. Archive path differences are only generated `.psmdcp` metadata names.

## Complete graph and asset comparison

Inspected all **12 assets files / 36 targets / 946 package nodes** before restore, after B, and after execution/startups. Direct inventory remains **36 IDs / 46 declarations / seven files**.
Normalization: `<repository-relative csproj>|<TFM>` → full package-only target nodes; recursively sorted compact JSON, UTF-8, no terminal LF.
Before: **386,216 bytes**, SHA-256 **`fcb8930daea43c64a0e1cd061d4becd8cd74951e8a06854679cdb85b00698a79`**.
After: **386,313 bytes**, SHA-256 **`df3a9ce6e1b65310c8145315886775c2d01606019459252ddb062f4e254adad3`**; unchanged after both startups.

| Package advancing 10.0.9 → 10.0.13 | Changed graph occurrences |
|-|-|
| Microsoft.Extensions.Configuration.Json | 5: Host net10/net9/net8; Tests net9/net8 |
| Microsoft.Extensions.Configuration.FileExtensions | 4: Host/Tests × net9/net8 |
| Microsoft.Extensions.FileProviders.Abstractions | Same 4; its other 21 nodes retain 10.0.9 |
| Microsoft.Extensions.FileProviders.Physical | Same 4 |
| Microsoft.Extensions.FileSystemGlobbing | Same 4 |
| System.Text.Json | Same 4 |
| System.Text.Encodings.Web | Same 4 |
| System.IO.Pipelines | Same 4 |

Exactly **33 version advances, 64 dependency-minimum advances, 913 identical full nodes**, zero added/removed package IDs. All selected compile/runtime/build/runtimeTargets/native/content fields unchanged; package-library version paths/content hashes change as expected.
Verified **22 distinct selected non-placeholder files plus 19 companion XML files** exist. Lower TFMs select `lib/{net8.0,net9.0}/*.dll`; System.Text.Json retains `buildTransitive/net8.0/System.Text.Json.targets`. Other affected build selections remain `_._`.
System.Text.Encodings.Web retains browser/net8 and win/net9 runtimeTargets; no browser/WASI execution is claimed. Host prune ranges remain `(,10.0.32767]`, `(,9.0.32767]`, `(,8.0.32767]` for this closure.
Host/net10 Json is `lib/net10.0/_._`, with dependencies pruned; Tests/net10 has no Json node. Net10 host/test deps omit this closure. Startup actually loaded Json/FileExtensions/System.Text.Json from shared framework **10.0.11**, file version **10.0.1126.37416**, not package 10.0.13.
Lower-TFM host/test deps select all eight candidate IDs; **32 copied DLL comparisons** match selected package bytes. Focused lower-TFM execution below, not net10 placeholders, supplies the corresponding test evidence.
Feeds unchanged: nuget.org and Visual Studio Offline Packages. Pruning and all assets diagnostics unchanged; only existing Pkcs NU1902/NU1903.

## Exact Windows commands and executed inventory

Authority: [AGENTS.md](../../../AGENTS.md), [related VSTest filters](../building-and-testing.md), phase-6 B/T-config/V-accept. Windows **10.0.26200 x64**, SDK **10.0.400**, runtimes **10.0.11 / 9.0.19 / 8.0.30**, xUnit VSTest adapter **3.1.5+1b188a7b0a**.
Distinct lower-TFM DLLs/System.Text.Json closure warranted V-accept escalation: the same sanctioned focused filter without `--framework`, not a new runner, option or full-suite claim.
```powershell
dotnet test --configuration Release --framework net10.0 --no-restore --verbosity normal --filter "FullyQualifiedName~fhir.candle.Tests.ConfigTests.OptionalJsonConfigurationPreservesMissingFileBehavior|FullyQualifiedName~fhir.candle.Tests.ConfigTests.OptionalJsonConfigurationPreservesParseErrors"
dotnet build --configuration Release --framework net10.0
dotnet test --configuration Release --framework net10.0 --no-restore --verbosity normal --filter "FullyQualifiedName~fhir.candle.Tests.ConfigTests|FullyQualifiedName~fhir.candle.Tests.StrictModeStartupTests"
dotnet test --configuration Release --no-restore --verbosity normal --filter "FullyQualifiedName~fhir.candle.Tests.ConfigTests|FullyQualifiedName~fhir.candle.Tests.StrictModeStartupTests"
```

| Gate, executed once in this order | Exit / result | Command elapsed |
|-|-|-|
| New rows, original Json 10.0.9 | 0; **4/4 passed**, zero failed/skipped | 8.01 s |
| Candidate B | 0; **8 existing warnings, zero errors** | 5.26 s |
| Candidate net10 T-config | 0; **107/107 passed**, zero failed/skipped | 6.19 s |
| Candidate all-target focused T-config | 0; **107 × 3 = 321 passed**, zero failed/skipped | 13.46 s |

Net10 test builds also report eight warnings; all-target build reports **24**, matching the documented baseline variants. Unchanged MailKit/MimeKit closure selects Pkcs 10.0.0 (net10), 8.0.1 (net9/net8): NU1902 [GHSA-2fmv-rv2c-59x7](https://github.com/advisories/GHSA-2fmv-rv2c-59x7), NU1903 [GHSA-846f-f764-76mx](https://github.com/advisories/GHSA-846f-f764-76mx). No new compiler/analyzer diagnostic or suppression.
Net10 inventory: **98 Config + nine Strict = 107** unique executed names. Preserved 103-row hash **`b583d4fbe19a9c74ee39cc9ca66ca64ad7e6c1dcea0d56506c0fcd41950807e1`** matches [predecessor inventory](configuration-environmentvariables.md).
New four-row hash **`df3ed188f3e28e9a574fb4bc1fb0f524147f6f7302c6ae95194f781481cce358`** matches pre-pin; full 107-row hash **`5702bd8dfdc008b90d151abfad40af56d938ae3d272768e3e5b4bdddfd10a146`**.
All-target stdout interleaves frameworks: three 107-pass summaries, each of the same 107 names occurs exactly three times; aggregate 321-occurrence hash **`b539daf763344e369f9a59e7d8be10b1d9649bec38b851c23f841f9e70916b1d`**. No unsupported per-framework case-hash/time attribution.
Hashes use full executed names/parameters, ordinal-sorted, LF-joined, UTF-8, no terminal LF. No numeric discovery total was emitted. Captured normal output in memory; no authored logs. Each test command verified no leftover owned TEMP test directories.

## Isolated startup, failures and cleanup

Used the [documented startup form](../sdk6-compatibility.md#isolated-startup), twice with distinct previously absent TEMP roots and empty `source`/`cache`; no real settings edits, secret fixtures, FHIR packages or resource inputs. Inert repository defaults were screened without copying values; unrelated child configuration was removed.
Child-only synthetic environment: `DOTNET_ENVIRONMENT=Production`, `CandleTenants_R4__0=prefixed-r4`, `Tenants_R4__0=external-r4`, `CandleTenants_R4B__0=prefixed-r4b`, `CandleTenants_R5__0=prefixed-r5`, `Tenants_R5__0=external-r5`, `CandleListen_Port=5828`, `Listen_Port=5827`.
```powershell
dotnet run --project src\fhir-candle\fhir-candle.csproj --framework net10.0 -- --r5 cli-r5 --fhir-source "$root\source" --fhir-package-cache "$root\cache" --use-official-registries false --open-browser false --port 5826
```
**Check 1 failed, wrapper exit 1:** ready in 8.522 s; first metadata GET returned 200, but the probe piped byte content into `ConvertFrom-Json`, producing scalar numbers and a false identity mismatch. Reproduced that behavior with synthetic bytes; no application change or weakened expectation.
**Check 2 passed, wrapper exit 0:** explicit UTF-8 decoding; confirmed actual `System.Byte[]` / `application/fhir+json`. Ready in **9.401 s**, Production, child-owned `http://[::]:5826`. GET `http://localhost:5826/fhir/{tenant}/metadata`: **external-r4 200/4.0.1; prefixed-r4b 200/4.3.0; cli-r5 200/5.0.0**; each exact CapabilityStatement canonical is `http://localhost:5826/fhir/{tenant}/CapabilityStatement/metadata`.
`prefixed-r4` / `external-r5` each returned 404, with exactly `GetMetadata <<< no tenant at {tenant}!` for those routes. Rejected unexpected fail/warn/error/URL-parse logs; none remained. Four service-start messages each occurred once; SMTP/Zulip absent; source/cache stayed empty. Only the four existing Pkcs diagnostic variants repeated across restore/build.
Cleanup used specific `Stop-Process -Id <PID> -Force`: check 1 child **74696**, parent **123396**; check 2 children **57720, 56144, 68076**, parent **51396** exited after child termination. Both run parents exited **-1**. Both checks verified all owned PIDs absent, TEMP root removed, parent environment unchanged, and port 5826 bindable; final reinspection confirmed cleanup. **Not graceful shutdown/lifecycle proof.**

## Limits, prerequisites and rollback

Other actual failure: public FileProviders.Physical 10.0.13 archive read timed out with WinError 10060 (inspection exit 1); retry of remaining reads succeeded (exit 0). No failed build/test or package-trial retry. Source/graph identities stayed unchanged through startup retry.
Prerequisites remain the accepted tenant repair and core/Binder/environment checkpoints; core >=10.0.13 is required. Revert this complete unit, restore with B and repeat graph/focused/startup checks; reassess later UserSecrets minima before rollback. Do not lower core under retained Binder/environment providers. Unlocked resolution is not an arbitrary rollback guarantee.
Windows focused tests cover all three TFMs; startup is net10 only. Final full-suite/all-target acceptance and post-pass Linux/macOS validation remain pending. No browser/RI, R6 hosting, graceful lifecycle or cross-platform certification. Only the three owned files changed; no plan/skills/other pins, installs, staging, commits or publication.
