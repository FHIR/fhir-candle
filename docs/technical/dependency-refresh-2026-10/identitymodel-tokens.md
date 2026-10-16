# Microsoft.IdentityModel.Tokens — 2026-10-16

**Phase 9 stopped before pins: supplier-alignment gate unresolved.** Attempt 1 only; zero package trials. Tokens and JsonWebTokens remain **8.19.1**. This is a regrouping hand-back, not an accepted upgrade or a demonstrated runtime incompatibility.

## Identity and scope

Predecessor: **`afb8d90651ed7e7556148947754ffeb556fd9fae`**; initial worktree and index clean. Read `AGENTS.md`, `docs/technical/building-and-testing.md`, and the plan's unit contract, phase 9 and final verification requirements.
Only this evidence document is added. Common's Tokens pin, `SmartClientRegistration.cs`, `SmartAuthManager.cs`, `AuthTests.cs`, and phase 10's JsonWebTokens pin are untouched.
Raw production/test patch excluding this document: **0 bytes**, SHA-256 **`e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855`**, from the literal command:
```powershell
git --no-pager diff --no-ext-diff --binary afb8d90651ed7e7556148947754ffeb556fd9fae -- src/FhirStore.Common/FhirCandle.Common.csproj src/FhirStore.Common/Smart/SmartClientRegistration.cs src/fhir-candle/Services/SmartAuthManager.cs src/fhir-candle.Tests/AuthTests.cs
```

## Availability is not mixed-version eligibility

Revalidated the public [Tokens version index][versions], [8.23.0 registration][registration] and catalog: **8.23.0 remains newest stable/listed**, published 2026-09-18. Retained Tokens/JsonWebTokens 8.19.1 are listed; queried catalog entries had no deprecation or vulnerability fields. This is metadata, not a security certification.
The version-tagged [support policy][support] lists 8.0.1 through latest 8.x as supported. It does **not** establish support for arbitrary combinations within that line.
The supplier's official [IdentityModel 8.x migration guidance][alignment], read on 2026-10-16, states:

> All the IdentityModel libraries must have the same version 8.0.0 in your project and including the recursive dependencies.

Raw wiki document: **2,503 bytes**, SHA-256 **`ac32bce4a60c18dd235842ac58956782b3ec24b2494165ffdae5cf1bef9ef289`**. Unlike the support-policy link, this is a mutable wiki, not an 8.23.0-tagged document.
The reminder uses the introductory release number 8.0.0; it is supplier alignment guidance, **not a nuspec equality constraint**. No consulted supplier source establishes an exception permitting this proposed 8.23.0/8.19.1 checkpoint. The phase-1 range/8.x dossier therefore does not settle actual eligibility.
**Parent action:** review/regroup phases 9 and 10 before implementation, including ownership of `src/fhir-candle/fhir-candle.csproj`, their evidence and verification requirements; alternatively obtain authoritative clarification permitting this exact mix. No phase-10 pin was moved and the plan was not edited.
Even a successful mixed-runtime test would not resolve this supplier gate by itself. Conversely, this finding does not prove that the binaries fail to load or that SMART rejects valid tokens.

## Dependency closure and assets reviewed before the stop

The original/candidate nuspecs have native **net10.0, net9.0 and net8.0** groups. Bare dependency versions below are minimums `[version, )`, without equality or upper bounds.

| Edge, all three targets unless noted | Retained / proposed minimum |
|-|-|
| JsonWebTokens 8.19.1 → Tokens | >=8.19.1; 8.23.0 is range-compatible only |
| Tokens 8.19.1 / 8.23.0 → IdentityModel.Logging | >=8.19.1 / >=8.23.0 |
| IdentityModel.Logging 8.19.1 / 8.23.0 → IdentityModel.Abstractions | >=8.19.1 / >=8.23.0 |
| Tokens, both versions → Microsoft.Bcl.Cryptography | >=10.0.2, unchanged |
| Tokens, both versions → Microsoft.Extensions.Logging.Abstractions | >=8.0.0, unchanged |
| Bcl.Cryptography 10.0.2 → System.Formats.Asn1 | >=10.0.2 on net8 only; no package dependency on net9/net10 |
| JsonWebTokens 8.23.0 → Tokens, future grouped trial | >=8.23.0; no authorization to move this direct pin here |

IdentityModel.Abstractions has no package dependencies on these targets. No other direct-ID movement was required by the inspected ranges; supplier alignment, not a new NuGet minimum, causes the hand-back.
Read original/candidate Tokens, Logging and Abstractions archives plus retained JsonWebTokens 8.19.1 **in memory**, without installation or extraction. Each supplies `lib/net10.0`, `lib/net9.0`, `lib/net8.0` DLL/XML assets; none has native/RID payloads.
Tokens candidate archive: **1,407,878 bytes**, SHA-256 **`4e491ce07152fc5c3a67a8ea50ab88f1348b198bad4ecfabd6602f5ee250e9f7`**. Its three target DLLs differ; any authorized trial warrants the documented focused all-target test form, not net10-only inference.

## Release changes and actual SMART boundary

Reviewed published releases **8.19.2, 8.21.0, 8.22.0, 8.23.0** and the [8.19.1→8.23.0 source comparison][comparison] (17 commits / 220 files). These cover serializer/JWE handling, JWT header/claims allocation, optional audience case matching, SHR path matching, WS-Trust reinstatement, decryption diagnostics and ML-DSA/AKP/composite key support.
`IgnoreCaseWhenValidatingAudience` is new and defaults **false**; the default trailing-slash tolerance remains **true**. The constructor's signed-token/expiration requirements and five-minute clock skew remain. Composite ML-DSA is gated; no new algorithm or switch was enabled here.
Read `SmartAuthManager` and its controller/interface callers. Registration explicitly processes RS384/ES384; client assertions use the retained `JsonWebTokenHandler`, registered keys, and four validation passes whose failures prevent issuance. No validation flags, algorithm policy, key handling, audience/lifetime check or expected rejection changed.
Existing `TestSmartAuthorize` exercises actual service authorization success/failure; `TestTokenRequest` exchanges an authorization code. Positive key registration generates/exchanges an RS384 assertion; the negative registration case expects audience and lifetime diagnostics. These are source observations, **not executions in this phase**, and are not claimed as invalid-signature or ES384 runtime coverage.
An eventual authorized trial should reuse these paths and assess the narrow audience-default gap before pins. No speculative tests or production adaptation were added after the supplier stop. The unrelated SMART CLI-array constructor behavior remains untouched.

## Graph, commands and results

Inspected existing generated assets for **12 projects / 36 TFMs / 946 package nodes**; no restore was run, so these are the predecessor graph, not a fresh candidate restore.
Normalization: `<repository-relative csproj>|<TFM>` → full package-only target nodes; recursively sorted compact JSON, UTF-8, no terminal LF.
Before and at hand-back: **386,811 bytes**, SHA-256 **`06c0659d2a30e4e33ad9b359c97a3395450d9c79cd0af1c8f3e7b84c196c209b`**. **Zero node or selected-asset deltas**, because no candidate was applied. Direct inventory remains **36 IDs / 46 declarations / seven files**.
Tokens/IdentityModel.Logging/IdentityModel.Abstractions 8.19.1 each occur in 30 graphs; retained JsonWebTokens 8.19.1 in six. Bcl.Cryptography 10.0.2 occurs in 30, Asn1 10.0.2 in ten net8 graphs. Logging.Abstractions stays 10.0.13 in 25; Tokens' Logging.Abstractions edge is pruned in host/UI graphs.
Verified **16 distinct selected closure DLLs and two build placeholders** exist across all TFMs. Selected compile/runtime paths remain target-native. No candidate output, candidate diagnostics or runtime assembly-load evidence exists.
Executed read-only checks: `git --no-pager status --short`, `git --no-pager rev-parse HEAD`, `git --no-pager diff --cached --name-only`, the raw diff above, tracked-file/XML/JSON/hash inspections, and public HTTPS metadata/source/archive reads. HEAD and cleanliness gates matched; no index entries were introduced.
`dotnet --version` returned **10.0.400**; `dotnet --list-runtimes` confirmed .NET/ASP.NET Core **10.0.11 / 9.0.19 / 8.0.30**. OS inspection: Windows **10.0.26200 x64**.
**B and T-auth were not executed**: supplier eligibility stopped the trial first. Baseline/candidate executed cases **0 / 0**; executed-name/parameter hashes **not applicable**. No pass, discovered/skipped-case comparison or warning comparison is claimed.
Required commands remain `dotnet build --configuration Release --framework net10.0` and `dotnet test --configuration Release --framework net10.0 --no-restore --verbosity normal --filter "FullyQualifiedName~fhir.candle.Tests.AuthTests"` after an authorized unit is frozen. Final Windows full-suite and Linux/macOS validation remain pending.

## Failures, cleanup and rollback

No product/build/test failure occurred because no trial began. Three read-only probe failures are retained: registration JSON needed gzip decoding; Python's default cp1252 could not print a supplier checkmark; an assumed 8.20.0 release endpoint returned HTTP 404. Corrected decoding/UTF-8 and the actual release index supplied the evidence above; these were not package retries.
No helper process, endpoint, credentials, temporary fixture/cache, authored log or extracted archive was created. Tool-managed response spools were retained; all inspection sessions completed. No installation, staging, commit, publication, skill/plan edit or diagnostic-worktree search.
No package rollback is necessary: production and tests remain exactly at the predecessor. This document may be removed/revised by the parent during integration; an eventual grouped upgrade must preserve the original expectations and earn its own baseline/candidate case inventories, diagnostics and all-target evidence.

[versions]: https://api.nuget.org/v3-flatcontainer/microsoft.identitymodel.tokens/index.json
[registration]: https://api.nuget.org/v3/registration5-gz-semver2/microsoft.identitymodel.tokens/8.23.0.json
[support]: https://raw.githubusercontent.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet/8.23.0/supportPolicy.md
[alignment]: https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet/wiki/IdentityModel-8x
[comparison]: https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet/compare/8.19.1...8.23.0

## Joint phases 9-10 recovery — 2026-10-16

**BLOCKED at the pre-pin baseline: a pre-existing ES384 registration defect requires separate authorization.** Both direct pins remain **8.19.1**, with no candidate restore, build or test. The freshly revalidated aligned **8.23.0/8.23.0** pair is the intended evaluation candidate, not an accepted upgrade and not a demonstrated dependency regression. The joint authorization supersedes only the historical grouping/ownership barrier above; the entire original 75-line assessment remains the historical prefix.

### Recovery identity and explicit disposition

- Rechecked predecessor **`afb8d90651ed7e7556148947754ffeb556fd9fae`**, empty index, clean tracked source at entry, both direct pins and the nine completed predecessor commits. Only the known assessment was untracked.
- Original prefix: **10,283 bytes / 75 lines**, SHA-256 **`7e3a5684b90886026f088c208c5f80ef029ed84d19956f668e22ac7b56637fcb`**. Appended evidence does not rewrite its stopped-before-pins or zero-execution history.
- Read `AGENTS.md`, `.editorconfig`, `docs\technical\building-and-testing.md`, phase 9, the unit contract, command catalogue and final verification gates. Inspected registration/JWT consumers, `ISmartAuthManager`, `SmartController`, the existing test fixture and project metadata. No source request, plan, skill, workflow, unrelated source or package declaration was edited.
- Only `src\fhir-candle.Tests\AuthTests.cs` has a source patch: three named regressions, six new rows, fresh per-case service/tenant/client/key state, BCL-generated in-memory RSA/P-384 material and loaded-assembly diagnostics. All six existing cases, including every negative audience/lifetime assertion, and the shared legacy fixture remain unchanged. Existing bodies/fixture were compared after normalizing Git LF versus working-tree CRLF; the test file retains its BOM and CRLF.
- **The new tests are explicitly retained as unaccepted recovery/reproducer work for the parent, not an accepted independent test change or upgrade.** No candidate was attempted or rejected. Do not commit this patch as a green dependency checkpoint. Parent must authorize a separate bounded pre-existing ES384 repair and disposition of these regressions before resuming, or direct withdrawal of all new test work for a documentation-only joint retention. No assertions were relaxed, no ES384 row was skipped, and the product was not repaired under dependency-compatibility authority.
- Required authorization: correct the existing `SmartAuthManager.TryProcessKey` handling of the standard JWK curve name `P-384` at the existing owner, retaining the advertised RS384/ES384 algorithm policy and rejection behavior; establish the corrected aligned-8.19.1 baseline before any joint upgrade trial. This is a pre-existing behavior correction, not permission to use 8.23.0 as a repair or to change unrelated SMART CLI arrays.

Baseline test/hand-back source identity excluding this document:

```powershell
git --no-pager diff --no-ext-diff --binary afb8d90651ed7e7556148947754ffeb556fd9fae -- src/FhirStore.Common/FhirCandle.Common.csproj src/fhir-candle/fhir-candle.csproj src/FhirStore.Common/Smart/SmartClientRegistration.cs src/fhir-candle/Services/SmartAuthManager.cs src/fhir-candle.Tests/AuthTests.cs
```

Raw patch: **12,267 bytes**, SHA-256 **`4aabbd3e36f5dc10c01241517021e7e63ee74191aabdd858022c9fc6086413b5`**. `AuthTests.cs`: **25,971 bytes**, SHA-256 **`5eb616a9ecd8be5ef4bb58e89de7b4d94466aa180fca54c1009b4820239159d2`**. No production/package patch exists.

### Fresh candidate, supplier and closure assessment

Read-only HTTPS checks on 2026-10-16, before selecting the exact joint evaluation pair:

- `dotnet nuget list source` reported enabled public NuGet (`https://api.nuget.org/v3/index.json`) and `C:\Program Files (x86)\Microsoft SDKs\NuGetPackages\`. The offline directory contained no IdentityModel packages. No private feed credentials were read or printed.
- Both public flat-container indexes still identify **8.23.0** as highest stable. Both [Tokens registration][registration] and [JsonWebTokens registration][joint-jwt-registration], and their linked catalogs, mark it listed/published **2026-09-18**. Both retained 8.19.1 entries are also listed. No deprecation/vulnerability fields were present in the queried catalogs; that is metadata, not a security certification.
- Freshly read the official [8.x alignment reminder][alignment] and [8.23.0 support policy][support]. Reminder bytes/hash remain **2,503 / `ac32bce4a60c18dd235842ac58956782b3ec24b2494165ffdae5cf1bef9ef289`**. Support policy: **5,812 / `cf97724e29625383f5da03dbad477998660ed4c9618cffb7becac3a075d0dc93`**. The active 8.x line and recursive alignment justify the authorized joint evaluation, not arbitrary mixed versions. No alignment exception was requested or trialled.
- Reviewed the current official [release index][joint-releases], including 8.19.2, 8.21.0, 8.22.0 and 8.23.0. Relevant changes remain serializer/JWE handling, JWT allocation, optional audience case matching, SHR path matching, WS-Trust, decryption diagnostics and gated ML-DSA additions. No new switch or algorithm was enabled.
- Read candidate [TokenValidationParameters source][joint-defaults]: `IgnoreCaseWhenValidatingAudience` defaults false, trailing-slash tolerance true, signed-token/expiration requirements true, clock skew 300 seconds. Source inspection is not execution of every default.
- Read both versions of all four IdentityModel archives in memory, plus Bcl.Cryptography 10.0.2, Asn1 10.0.2 and Logging.Abstractions 10.0.13; no installation/extraction. Native `lib/net10.0`, `lib/net9.0`, `lib/net8.0` DLLs differ by target, so lower-TFM execution cannot be inferred from net10. Candidate Tokens archive hash remains `4e491ce07152fc5c3a67a8ea50ab88f1348b198bad4ecfabd6602f5ee250e9f7`; candidate JsonWebTokens is **562,939 bytes**, hash **`47e2203de8ea687eb56a05411ce10fd9e8a27956f2accef04ef2aa2c0db85d9a`**. No native/RID payloads occur in the inspected archives.

All three target nuspec groups preserve the chain JsonWebTokens → Tokens → IdentityModel.Logging → IdentityModel.Abstractions; edges advance from minimum **8.19.1** to minimum **8.23.0** together. Abstractions has no package dependencies. Tokens also requires Bcl.Cryptography >=10.0.2 and Extensions.Logging.Abstractions >=8.0.0, unchanged. Bcl.Cryptography requires Asn1 >=10.0.2 only on net8. Logging.Abstractions 10.0.13 requires DependencyInjection.Abstractions >=10.0.13 and, on net8/net9, DiagnosticSource >=10.0.13. These are minimum ranges, not equality constraints; no additional direct-pin movement was identified.

### Actual baseline failure and SMART boundaries

The new `ClientAssertionPreservesRegisteredSigningAlgorithms("ES384")` generates a BCL NIST P-384 key, exports its public portion and uses the existing IdentityModel converter to produce standard `crv: "P-384"` JWKs. It fails during **public-key registration**, before that case reaches `GenerateSignedJwt` or `TryClientAssertionExchange`:

```text
System.PlatformNotSupportedException:
  The specified curve 'P-384' or its parameters are not valid for this platform.
Inner System.Security.Cryptography.CryptographicException: The parameter is incorrect.
SmartAuthManager.TryProcessKey                  SmartAuthManager.cs:774
SmartAuthManager.ProcessKeys                    SmartAuthManager.cs:564
SmartAuthManager.TryRegisterClient              SmartAuthManager.cs:521
AuthTests.RegisterAssertionClient               AuthTests.cs:518
AuthTests.ClientAssertionPreservesRegisteredSigningAlgorithms AuthTests.cs:427
```

The existing code passes the JWK name into `ECCurve.CreateFromFriendlyName`. Its fallback surrounds only that name creation, whereas CNG rejects the unresolved name later at import outside the catch. The BCL key generation and JWK conversion have already succeeded. This is not an expired fixture, malformed coordinate setup, shared-client collision, candidate behavior or authorization to replace the standard JWK name with a platform-specific fixture workaround. Production and both pins are unchanged from the predecessor.

Executed service observations:

- Existing `RequestAuth` success/wrong-audience rejection, authorization lookup/update and authorization-code `TryCreateSmartResponse` exchange all passed.
- Existing RS384 fixture registration, `GenerateSignedJwt` and assertion exchange passed. Existing negative registration/assertion case retained exactly two messages including **IDX10214 audience** and **IDX10223 lifetime**.
- Fresh RS384 registrations and assertions passed exact audience and trailing slash; path-only `r4` → `R4` mismatch failed with one IDX10214 message, null response and no new authorization.
- The recognized-key-ID/wrong-RSA-signer case failed with signature diagnostics **IDX10511**, null response and unchanged authorization count. A subsequent correctly signed assertion naming an unregistered key ID also failed with the existing key-not-found message and no issuance.
- The RS384 algorithm row verified JWT algorithm, key ID, issuer, subject, JTI and audience; accepted responses retained bearer type, client ID, `system/*.rs`, access/refresh/ID tokens, tenant routing and usable authorization lookup.
- **ES384 registration was executed and failed; ES384 signing/exchange were not executed.** No SMART HTTP controller, listener, external JWKS retrieval, browser flow, new algorithm, clock-skew boundary or cross-platform behavior is claimed. Tests directly exercised the real service; controller callers were inspected read-only.

### Commands, execution inventory and warnings

Windows **10.0.26200 x64**, SDK **10.0.400**; executed testhost runtimes **.NET 10.0.11 / 9.0.19 / 8.0.30**. ASP.NET Core versions installed match those three patches. The xUnit VSTest adapter reports **3.1.5+1b188a7b0a**; runner/dependency pins were not changed.

| Command from repository root | Result / evidence |
|-|-|
| `dotnet build --configuration Release --framework net10.0` | Exit **0**, 10.62 seconds, zero errors; initial test draft built/restored aligned 8.19.1. Eight existing Pkcs warnings plus one new CS8604 in the new nullable advertised-algorithms assertion. Complete output retained in tool session `phase9-baseline-build`; no standalone build log was saved. |
| `dotnet test --configuration Release --no-restore --verbosity normal --filter "FullyQualifiedName~fhir.candle.Tests.AuthTests"` | Exit **1**; all three target DLLs executed. **11 passed / 1 failed / 0 skipped of 12 each**, 36 case occurrences total. Compilation produced zero errors and **24 existing Pkcs warnings** (eight per TFM); the new CS8604 was absent. The sole failed row per target is ES384 registration above. |
| Candidate B / focused tests | **Not run**: required retained-pair baseline failed. No separate net10 T-auth rerun; the all-target command contains the attributable net10 baseline execution, but it is red, not an acceptance result. |

Between B and the baseline test command, the test-only CS8604 was fixed by assigning the nullable advertised algorithm collection to an explicit local and asserting non-null before its contents. No suppression or expectation weakening. Assembly diagnostics were changed from captured test output to `Console.WriteLine` so successful cases' loaded assemblies appear under the sanctioned normal verbosity. The test command compiled the final identified patch on all targets. B's earlier draft is not falsely identified as that exact patch: its reconstructed `AuthTests.cs` hash is `3a201d01eea74369ed99c72506d5407d3983fcf90a7e68243e5cb8d6add7f3d8` (25,876 bytes).

Existing warnings: **NU1902 / NU1903**, `System.Security.Cryptography.Pkcs` **10.0.0 / 8.0.1**, advisories **GHSA-2fmv-rv2c-59x7 / GHSA-846f-f764-76mx**, Host and Tests. The all-target log repeats each warning in its summary (48 printed occurrences, 24 reported warnings); no other compiler/analyzer warning remained. VSTest's failed-task **MSB4181** is a consequence of the red test run, not a compiler error. No candidate warning comparison exists.

Every row below appears **three times** in the all-target executed-result lines, one occurrence for each runner. Three runner banners and three identical 12/11/1 summaries account for net10/net9/net8. Output interleaves and does not tag each result line with TFM; no per-TFM duration/order attribution is claimed.

| Case / parameters | Result on each Windows target |
|-|-|
| `TestSmartConfig` | Pass |
| `TestSmartAuthorize(true, "http://localhost:5826/fhir/r4", "openid fhirUser profile launch/patient patient/*.read")` | Pass |
| `TestSmartAuthorize(false, "http://localhost:5826/fhir/notAnEndpoint", same scope)` | Pass |
| `TestTokenRequest("Patient/1", "")` | Pass |
| `TestSmartClientRegistrationWithKeys(privateJson, publicJson)` | Pass |
| `TestSmartClientRegistrationWithKeysFails(publicJson)` | Pass |
| `ClientAssertionAudienceMatchingPreservesCurrentDefaults("http://localhost:5826/fhir/r4", true)` | Pass |
| `ClientAssertionAudienceMatchingPreservesCurrentDefaults("http://localhost:5826/fhir/R4", false)` | Pass |
| `ClientAssertionAudienceMatchingPreservesCurrentDefaults("http://localhost:5826/fhir/r4/", true)` | Pass |
| `ClientAssertionRejectsInvalidSignature()` | Pass, including unrecognized-key control |
| `ClientAssertionPreservesRegisteredSigningAlgorithms("RS384")` | Pass |
| `ClientAssertionPreservesRegisteredSigningAlgorithms("ES384")` | Fail at registration |

Six legacy + six new rows; no missing occurrences or skips, **not** a green baseline. Original phase-9 executed cases were zero and are not reused. Candidate inventory is zero/not executed; no baseline/candidate name equality claim is possible.

Executed display inventory: strip duration from `  Passed/Failed/Skipped fhir.candle.Tests.AuthTests... [duration]` lines, count `(status, display name)`, serialize sorted `[status,name,count]` rows with compact JSON/UTF-8/no LF. **12 distinct / 36 occurrences / 1,829 bytes**, SHA-256 **`3b643940d36a3b786a3b9825633443fdba6061dcc2ef6ebd1aa68db9fd5ce319`**. Runner display names truncate long scope/file parameters; this hash is explicitly the displayed inventory, not full raw parameters. The table and unchanged source supply inline parameters. Existing parameter fixture identities: `data\smart\smart.rs384.private.json` **2,423 bytes / `6b0c6762a4e1f131c0b9ea2dcd83179536fcce98b0c3f168d01da10a7ccd3d0a`**; public file **575 bytes / `331e3d72c85a9eab254c3f864ce9f0de1aa241ee7c6a3485bb49c44c2451702e`**. No fixture contents or generated private material are added to this record.

### Restored graphs and actually loaded assemblies

Before and after B/test restore state: **12 projects / 36 targets / 946 package nodes**, normalized **386,811 bytes**, SHA-256 **`06c0659d2a30e4e33ad9b359c97a3395450d9c79cd0af1c8f3e7b84c196c209b`**. Same normalization as the original prefix: Git-relative slash project path plus `|<target>` → full package-only target nodes; recursive sorted compact JSON, UTF-8/no LF. **Zero package-node, edge or selected-asset deltas.** No candidate graph exists.

| Resolved closure member | net10 / net9 / net8 graph occurrences | Version throughout |
|-|-|-|
| Tokens, IdentityModel.Logging, IdentityModel.Abstractions (each) | 10 / 10 / 10 | 8.19.1 |
| JsonWebTokens | 2 / 2 / 2, Host and Tests | 8.19.1 |
| Bcl.Cryptography | 10 / 10 / 10 | 10.0.2 |
| System.Formats.Asn1 | 0 / 0 / 10 | 10.0.2 |
| Extensions.Logging.Abstractions | 5 / 10 / 10 | 10.0.13 |

Both performance projects' six graphs contain none of this closure. No extra recursive IdentityModel member was found. All selected closure compile/runtime/build paths were checked to exist across the 36 targets: **19 distinct DLL paths**, two `_._` build placeholders and Logging.Abstractions' build target. DLLs are target-native; Asn1 selects net8 only. Net10 Host/UI prune the package Logging.Abstractions edge. Direct inventory remains **36 IDs / 46 declarations / seven files**, all declarations unchanged, including both authorized pins. The first namespace-naive XML probe undercounted imported UI props; the corrected namespace-aware probe produced this inventory.

Observed loaded assembly names/locations in successful RS384 cases:

- **All three TFMs:** Tokens, JsonWebTokens, IdentityModel.Logging and IdentityModel.Abstractions **8.19.1.0**, loaded from the respective `src\fhir-candle.Tests\bin\Release\<TFM>\` output. Extensions.Logging.Abstractions **10.0.0.0** loaded from the same target output (resolved package 10.0.13).
- **net9/net8:** Microsoft.Bcl.Cryptography **10.0.0.2** observed loaded. **net10:** not observed in these snapshots; do not infer loading from its restored node.
- **System.Formats.Asn1:** not observed loaded on any target in these snapshots; restored net8 assets are not execution evidence. No candidate assembly loaded.

### Evidence locations, inspection diagnostics and cleanup

Authorized session evidence directory:
`C:\Users\ginoc\.copilot\session-state\085617bb-4d9a-475b-99ad-a7708b939326\files`

- `phase9-joint-prepin-auth-all-targets.log`: copied byte-for-byte from the generated tool spool; **3,020,884 bytes**, SHA-256 **`4883027c8e7387357293d0ed4acec5f533ea8597bae44583dd5f49f6e13b801c`**.
- `phase9-joint-revalidation.log`: supplier/listing/archive inspection output, SHA-256 **`e1c07f19381b2ee0514762050a33bb7e5e2ed0e50d79ab28a0e7b969722fa6dc`**. Includes public URLs, nuspec target groups and individual DLL/archive hashes; no installed tools or extracted packages.

Other executed commands were read-only `git --no-pager status --short`, `rev-parse HEAD`, `diff --cached --name-only`, `log -10 --oneline`, `diff --check`, `diff --stat`, the raw patch command above, `git ls-files`/`git show` via inline Python, `dotnet nuget list source`, `dotnet --version`, `dotnet --list-runtimes`, `Select-String`/file/XML/JSON/hash inspections, public HTTPS reads, and process inspections. They completed successfully except the explicitly diagnosed read-only byte-comparison probe: it assumed CRLF in Git's LF blob, raised `ValueError`, and was corrected with newline-normalized comparison (both legacy-body/fixture checks true). An attempted standalone `AuthTestFixture.cs` read found no file; the fixture is at the bottom of the owned `AuthTests.cs`. These are inspection errors, not candidate trials.

`Copy-Item`/`Get-FileHash` preserved the two generated logs successfully without writing scratch outputs. Build-owned reusable MSBuild workers were identified by parent PID 95332, creation time and command line, then stopped with exact `Stop-Process -Id` calls: **102164, 139328, 15364, 62772, 35688, 37980, 35540, 79112, 32176, 23300, 67172, 123884, 94740, 36704, 15628, 137272, 67204**. Their compiler child **40328** had exited before the subsequent guarded stop/check. No owned testhost, compiler server, build worker, service or shell command remains running. Unrelated pre-existing processes were not stopped.

No real credentials, external JWKS calls, persistent key fixtures, listeners, dependency/tool installs, staging, commits, pushes or PRs. B refreshed ignored assets/output only; no generated assets/logs are added to Git. **Final Windows full-suite and all Linux/macOS cells remain pending.** The parent's next decision is separate repair authorization versus explicit documentation-only retention/test withdrawal, not acceptance of the untried pair.

[joint-jwt-registration]: https://api.nuget.org/v3/registration5-gz-semver2/microsoft.identitymodel.jsonwebtokens/8.23.0.json
[joint-releases]: https://api.github.com/repos/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet/releases?per_page=15
[joint-defaults]: https://raw.githubusercontent.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet/8.23.0/src/Microsoft.IdentityModel.Tokens/TokenValidationParameters.cs

## Phase 9 code-only ES384 prerequisite repair — 2026-10-16

**Corrected retained-pair baseline verified on Windows; awaiting parent review/integration, not a dependency upgrade.** The user selected **"Authorize a bounded ES384 prerequisite repair and revise the plan to commit it separately"**. Revised phase 9 authorizes the existing recovery tests and the correction below; phase 33 alone owns the subsequent joint package trial. Tokens, JsonWebTokens, IdentityModel.Logging and IdentityModel.Abstractions remain **8.19.1**. No candidate was restored, built, tested or accepted in this repair.

The historical results above remain unchanged: the original assessment executed no tests, and joint recovery executed **12 cases / 11 passes / one ES384 registration failure / zero skips per Windows target** before any candidate. This append does not turn that failed execution into a pass.

### Recovery identity and exact bounded change

- Reconciled HEAD **`afb8d90651ed7e7556148947754ffeb556fd9fae`**, empty index, the nine completed predecessor commits, unchanged production/project files, retained test hash **`5eb616a9ecd8be5ef4bb58e89de7b4d94466aa180fca54c1009b4820239159d2`**, and the full retained dossier hash below. The known modified tests/untracked dossier were authorized recovery inputs, not discarded or reset.
- Read `AGENTS.md` for conventions and sanctioned commands, `.editorconfig`, `docs\technical\building-and-testing.md`, revised phase 9 and the authorized ES384 section. Read the retained tests/dossier, `TryProcessKey`, both helper callers (`ProcessKeys` and `GenerateSignedJwt`), registration/assertion validation, `ISmartAuthManager` and the SMART controller callers.
- The sole production change is the existing ES384 branch's curve selection: exact case-sensitive **`webKey.Crv == "P-384" ? ECCurve.NamedCurves.nistP384 : ECCurve.CreateFromFriendlyName(webKey.Crv)`** before import. All other names retain the pre-existing friendly-name path and catch behavior. Guard checks, diagnostics, key IDs, algorithms, CNG import, caller returns and validation policy are unchanged. No catch expansion, unknown-name repair, provider redesign, public API, new algorithm or invalid-private-key behavior change.
- Retained all six legacy cases, all six recovery rows, their assertions and the existing fixture. Added the two explicit `Crv.ShouldBe("P-384")` assertions to the ES384 algorithm row, `Es384ClientAssertionRejectsInvalidSignature` (one case), and `Es384RegistrationPreservesMissingKeyFieldDiagnostics` (three cases). These are now explicitly authorized prerequisite regressions, not speculative candidate tests. New cases use isolated service/client state and fresh BCL-generated in-memory keys; no added fixtures, secrets, listeners or external JWKS traffic.
- New invalid-registration controls preserve **true-with-diagnostics/no-usable-key** semantics. Each missing public field yields the exact existing three registration messages and an empty resolved-key collection; a JWT signed with the untouched valid private key is then rejected with `client has NO keys.`, null response and unchanged authorization count. No malformed private key is submitted to `GenerateSignedJwt`.
- Removing only this repair's test additions reconstructs the retained test bytes exactly: **25,971 bytes / `5eb616a9ecd8be5ef4bb58e89de7b4d94466aa180fca54c1009b4820239159d2`**. A separate newline-normalized HEAD comparison confirmed unchanged legacy method bodies and fixture. Both C# files retain their BOM and CRLF.

Preserved evidence prefixes, checked before and after the append:

| Prefix | Bytes | SHA-256 |
|-|-:|-|
| Original 75-line assessment | 10,283 | `7e3a5684b90886026f088c208c5f80ef029ed84d19956f668e22ac7b56637fcb` |
| Entire recovery dossier, including all joint failed-baseline text | 30,344 | `e0f9a353b3bc92d9f60db5bb0d870e71f76bcd603744f349fec417de463fbb78` |

Verified production/test patch, excluding this append, is **17,709 bytes**, SHA-256 **`e1d7b21bc6e1f2c6166bb87e6714026fb58ebb40f32758ee72f205e29c9c153a`**, using the same predecessor/path set as the joint record:

```powershell
git --no-pager diff --no-ext-diff --binary afb8d90651ed7e7556148947754ffeb556fd9fae -- src/FhirStore.Common/FhirCandle.Common.csproj src/fhir-candle/fhir-candle.csproj src/FhirStore.Common/Smart/SmartClientRegistration.cs src/fhir-candle/Services/SmartAuthManager.cs src/fhir-candle.Tests/AuthTests.cs
```

`SmartAuthManager.cs`: **80,763 bytes / `478be218775768185f308cdeb326af84067ec52576e0e03d015ba150065d1c8c`**. `AuthTests.cs`: **30,542 bytes / `80b59e9d65bf5c27100f1fc5a07f974c4c24ae4352d76aa5d8487988fcc258e1`**. These are the exact sources used by both commands below; no code changed between or after them.

### Commands, environment and executed inventory

Repository root: `C:\ai\git\fhir-candle`. Windows **10.0.26200 x64**, SDK **10.0.400**, PowerShell **7.6.6**. Installed .NET/ASP.NET Core patches are **10.0.11 / 9.0.19 / 8.0.30**; the three testhost banners actually report those .NET runtime versions and xUnit VSTest adapter **3.1.5+1b188a7b0a**, 64-bit. No tool/runner/package pin was changed.

| Exact command | Actual result |
|-|-|
| `dotnet build src\fhir-candle\fhir-candle.csproj --configuration Release --framework net10.0` | Exit **0**, **9.04 s** reported; **4 warnings / 0 errors**. Shell interval **16:37:24.493–16:37:35.386 -05:00**. Host/dependencies built and existing assets refreshed. |
| `dotnet test --configuration Release --no-restore --verbosity normal --filter "FullyQualifiedName~fhir.candle.Tests.AuthTests"` | Exit **0**, **15.11 s** reported; **24 warnings / 0 errors**. Shell interval **16:37:50.638–16:38:06.138 -05:00**. Three successful runs, **16/16 each**, no failures/skips. The attributable net10 execution also supplies T-auth; no duplicate focused invocation. |

Existing restored test graphs were present; no missing-dependency diagnostic occurred, so solution build B was unnecessary. Both commands ran synchronously with a 180-second initial wait and completed. The unchanged red baseline was not rerun, nor was any candidate command.

| Windows target / actual testhost runtime | Executed | Passed | Failed | Skipped |
|-|-:|-:|-:|-:|
| net10.0 / .NET 10.0.11 | 16 | 16 | 0 | 0 |
| net9.0 / .NET 9.0.19 | 16 | 16 | 0 | 0 |
| net8.0 / .NET 8.0.30 | 16 | 16 | 0 | 0 |
| Total | 48 | 48 | 0 | 0 |

Each case below has **three actual Passed display occurrences**, one per runner's identical inventory. Three target banners and three 16/16 summaries support the table above; interleaved result lines are not individually TFM-tagged, so no per-target duration/order attribution is claimed.

| Case / parameters | Repair result on each Windows target |
|-|-|
| `TestSmartConfig` | Pass |
| `TestSmartAuthorize(true, "http://localhost:5826/fhir/r4", "openid fhirUser profile launch/patient patient/*.read")` | Pass |
| `TestSmartAuthorize(false, "http://localhost:5826/fhir/notAnEndpoint", same scope)` | Pass |
| `TestTokenRequest("Patient/1", "")` | Pass |
| `TestSmartClientRegistrationWithKeys(privateJson, publicJson)` | Pass |
| `TestSmartClientRegistrationWithKeysFails(publicJson)` | Pass |
| `ClientAssertionAudienceMatchingPreservesCurrentDefaults("http://localhost:5826/fhir/r4", true)` | Pass |
| `ClientAssertionAudienceMatchingPreservesCurrentDefaults("http://localhost:5826/fhir/R4", false)` | Pass |
| `ClientAssertionAudienceMatchingPreservesCurrentDefaults("http://localhost:5826/fhir/r4/", true)` | Pass |
| `ClientAssertionRejectsInvalidSignature()` | Pass, including unknown-kid control |
| `ClientAssertionPreservesRegisteredSigningAlgorithms("RS384")` | Pass |
| `ClientAssertionPreservesRegisteredSigningAlgorithms("ES384")` | Pass through registration, signing and usable exchange |
| `Es384ClientAssertionRejectsInvalidSignature()` | Pass |
| `Es384RegistrationPreservesMissingKeyFieldDiagnostics("crv", "ECDSA Curve (crv)")` | Pass |
| `Es384RegistrationPreservesMissingKeyFieldDiagnostics("x", "ECDSA X coordinate (x)")` | Pass |
| `Es384RegistrationPreservesMissingKeyFieldDiagnostics("y", "ECDSA Y coordinate (y)")` | Pass |

Executed display normalization: remove only the terminal duration from `Passed/Failed/Skipped fhir.candle.Tests.AuthTests... [duration]`, count `(status, display name)`, serialize sorted `[status,name,count]` rows as compact JSON, `ensure_ascii=False`, UTF-8, no LF. Re-reading the historical baseline reproduces **12 distinct / 36 occurrences / 1,829 bytes / `3b643940d36a3b786a3b9825633443fdba6061dcc2ef6ebd1aa68db9fd5ce319`**. Repair inventory: **16 distinct / 48 occurrences / 2,399 bytes / `0dc30667ea211803b0d51723d8b56e98af64f9e4367cadbf9d4d561d20c1fac6`**.

Ignoring status, all 12 retained names/parameter displays still occur exactly three times; no occurrence is missing. Exactly four new rows contribute 12 occurrences. ES384 changes from Failed to Passed; the other retained rows remain Passed. Long fixture/scope displays remain runner-truncated (including the capture's replacement characters), so these hashes identify displayed inventories, not complete raw parameters. The unchanged source/fixture identities above supply the underlying parameters. Candidate inventory remains zero/not executed.

### Actual positive and negative service observations

- **Both RS384 and ES384** now register their generated public keys, sign via the shared helper with their private keys and exchange assertions successfully. The ES384 row verifies that both converter-produced JWKs retain standard **P-384**, JWT `alg`/`kid`/issuer/subject/JTI/audience, advertised algorithm, empty diagnostics, bearer response, client/scopes, access/refresh/ID tokens and usable intended-tenant authorization lookup with future expiration.
- Fresh wrong-RSA and wrong-EC signers using recognized registered key IDs are rejected with **IDX10511 signature diagnostics**, null response and no new authorization. The new EC case asserts different public X and private D values, so it cannot accidentally reuse the registered pair. The retained unknown-kid assertion is rejected with the existing key-not-found message and no issuance.
- Missing `crv`, `x` and `y` each preserve the exact field-specific diagnostic, `could not be processed and will not be available.`, and `has no keys.` registration message. Registration still returns true and records the client, but no usable key is added; valid-private-key assertions cannot obtain tokens from those registrations.
- Exact and trailing-slash audiences succeed; path-only `r4` to `R4` mismatch is rejected with one **IDX10214** message, null response and unchanged authorization count. The legacy invalid assertion still requires exactly two messages containing **IDX10214 audience** and **IDX10223 lifetime** diagnostics. Existing `RequestAuth`, authorization lookup/update and authorization-code exchange also pass.

No assertion was weakened or skipped. No different product defect appeared. These tests directly invoke the real SMART service, not HTTP controllers; controller contracts were inspected only.

### Unchanged dependency graph, warnings and observed assembly loads

Before edits and after both commands: **12 Git-tracked projects / 36 targets / 946 package-only full target nodes / 386,811 bytes**, SHA-256 **`06c0659d2a30e4e33ad9b359c97a3395450d9c79cd0af1c8f3e7b84c196c209b`**. Normalization is `<Git-relative slash csproj>|<TFM>` to all package-only full nodes in adjacent `obj\project.assets.json`, recursively sorted compact JSON, `ensure_ascii=False`, UTF-8 without LF. **Zero node, edge or selected-asset deltas.** All project/props declarations compare unchanged to HEAD: **36 direct IDs / 46 declarations / seven files**; no unrelated direct movement.

The retained closure occurrence counts remain those in the joint record: each Tokens/Logging/Abstractions 8.19.1 in 30 graphs; JsonWebTokens 8.19.1 in six; Bcl.Cryptography 10.0.2 in 30; Asn1 10.0.2 in ten net8 graphs; Logging.Abstractions 10.0.13 in 25. Rechecked all selected closure paths: **19 DLLs, two `_._` placeholders and one build target exist**. Restored assets are not by themselves execution evidence.

Only existing **NU1902/NU1903** warnings appeared: Pkcs **10.0.0 / 8.0.1**, advisories **GHSA-2fmv-rv2c-59x7 / GHSA-846f-f764-76mx**. Host-only build prints 12 warning occurrences across restore/build/summary and reports four target warnings; its narrower scope excludes Tests, unlike the earlier solution build. All-target tests print **48 warning occurrences / 24 reported warnings**, exactly matching the historical baseline's warning-code/text multiset. No new compiler/analyzer warning, suppression, missing-dependency error or MSB4181 occurred in the repaired run.

Actual snapshots contain five RS384 and five ES384 runtime markers per TFM. Observed assembly names/locations, each appearing in ten snapshots per applicable target:

| Loaded member | Actual assembly version / targets |
|-|-|
| Tokens, JsonWebTokens, IdentityModel.Logging, IdentityModel.Abstractions | **8.19.1.0**, all three TFMs |
| Extensions.Logging.Abstractions | **10.0.0.0**, all three TFMs (resolved package remains 10.0.13) |
| Microsoft.Bcl.Cryptography | **10.0.0.2**, net9/net8 only; not observed on net10 |
| System.Formats.Asn1 | Not observed on any target; no loading claim from its net8 restored node |

Observed DLL locations are the corresponding `C:\ai\git\fhir-candle\src\fhir-candle.Tests\bin\Release\<TFM>\` outputs. No candidate assembly was loaded.

### Durable evidence, inspection diagnostics and cleanup

All repair logs are under:
`C:\Users\ginoc\.copilot\session-state\085617bb-4d9a-475b-99ad-a7708b939326\files`

| Log | Bytes / SHA-256 |
|-|-|
| `phase9-repair-host-build-output.log` | 4,712 / `951cf652203230a2505e718c28f1a73bc755e68b9ca747e9efeaa906e3daca9e` |
| `phase9-repair-auth-all-targets-output.log` | 2,299,170 / `dc7ccf7cb98fdb19493a8f462f146be36f55ab45923e92a338faa3dc29301b57` |
| `phase9-repair-host-build.log` | 8,887 / `ddb81d19840f177448989a6eeece650848403d41e6c9c8e8e7e4b8e145462d03` |
| `phase9-repair-auth-all-targets.log` | 2,479 / `a6478e71005923764141f40cad36ef3c478b42bd5bbfb041438e4daf09012d93` |
| `phase9-repair-verification.log` | 9,149 / `fd73f076b7d9028663dff0d4e81298a6d43615cb06027787c6ea7c8d76d50982` |
| `phase9-repair-inventory.log` | 14,831 / `8cbe4d2a853a2f93142f5e79b9bda6f9aad004800a84967b762b5a385a5bbe11` |
| `phase9-repair-cleanup.log` | 4,340 / `a4b153555d27d00150172b4411f838b2a75c8047e4c52aa19d9d0578c56032de` |

**Logging limitation:** `Start-Transcript` retained shell command/environment/timing/exit information and the build-worker list, but this execution host did not transcribe native `dotnet` stdout. Those two transcript files alone are not full build/test logs. The complete test tool-output spool was therefore copied byte-for-byte to the `-output.log` above. The complete native host-build output was transcribed from its returned tool result and then verified equal to that original result's native segment after newline normalization; the companion transcript retains the surrounding metadata and process listing. Thus full native results are preserved in the evidence directory, but the requested avoidance of tool spooling was not achieved. No build/test was repeated merely for logging. Verification/inventory logging explicitly routed inspection output through the PowerShell host and includes the real graph, display inventories, warnings and assembly observations.

Other commands/checks: `git --no-pager status --short`, `rev-parse HEAD`, `log -10 --oneline`, `diff --cached --name-only`, `diff --check`, `diff --stat`, the raw patch command above, `git ls-files`/`git show`, `dotnet --version`, `dotnet --list-runtimes`, read-only XML/JSON/hash and selected-file inspections, `Get-CimInstance Win32_Process`, `Get-FileHash`, `Copy-Item`, and transcript commands. No new tool/package was installed.

Inspection-only errors were corrected without product/test changes: an assumed Common-project interface path did not exist (the interface is beside the host service); two initial preservation comparisons included class-boundary/retained-using differences rather than just legacy bodies; an overly broad display-duration regex removed JSON-array text from fixture parameters. The corrected body comparison and exact retained-test reconstruction passed, and the duration-only parser reproduces the original baseline inventory hash above. The incomplete native transcripts initially yielded zero parsed case lines; they are not zero-test results, and all final counts come from the complete native output.

Cleanup reconciled creation time, command line and parent **7320** for 22 build-owned reusable MSBuild workers, then stopped only exact PIDs **99432, 4920, 99040, 137456, 117280, 114420, 69716, 133620, 82572, 14080, 48544, 131952, 30408, 84692, 138676, 9456, 104888, 31984, 113604, 105172, 56056, 123448**, plus their compiler **25136** (parent 69716), using guarded `Stop-Process -Id`. The all-target command reused those workers and left no new worker list. Final process inspection found no owned worker/compiler/testhost or descendants remaining; unrelated pre-existing processes were untouched.

No plan/skill/project/package/fixture edit, staging, commit, push, PR, new listener or external test traffic. Only the two owned C# files and this append change in the repository; ignored build outputs and authorized session evidence are not Git deliverables. **This is the focused Windows prerequisite baseline only: candidate evaluation, final Windows full-suite verification and all Linux/macOS validation remain unexecuted/pending.** Parent integration and a separate repair commit must precede phase 33.
