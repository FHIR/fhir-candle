# MCP protocol / ASP.NET Core pair

**2026-10-16, Windows x64 — accept 2.2.0/2.2.0 for the phase-17 local checkpoint.**
Final Windows full-suite and Linux/macOS validation remain pending. No commit was made.

## Scope and identity

Authority: [AGENTS.md](../../../AGENTS.md), phase17, unit contract and V-accept.
Clean predecessor reverified: `a1bf503c44bc5c48bde72faedc3a6f941219c3fc`.
Only direct changes: **ModelContextProtocol and ModelContextProtocol.AspNetCore 1.4.0 -> 2.2.0**.
Namespace-aware XML comparison covered all **46 declarations / 36 IDs**, including imported UI props:
every other direct version, condition, alias and asset attribute is unchanged.
Program's existing registration explicitly selects `HttpServerSessionMode.Stateful`; mapping and enablement are unchanged.
FhirMcpTools, FhirStoreMcp, CommonCandleMcp and every individual tool implementation are unchanged.
No new library/project, protocol feature, credentials, authentication relaxation or pre-existing repair.

Raw source SHA-256 (CRLF):

| File | SHA-256 |
|---|---|
| Host csproj | `02e6c4fffbf3e89b07ae87d87966062c85e6f6b1999831e825025d2198d00f1b` |
| Program.cs | `19837d15316446c7b765668c9178b6f33c94ae6c964fb1b372b21aaf70578654` |
| McpBasicTests.cs | `6017ca13cfe8e58d74965e7f42c82850db571b95909f761c786a243ec950deec` |
| McpTransportCompatibilityTests.cs | `ce6bcae577ef519e9400e5119d7d39cc0ec97a2dfceec1014746ae3d66b26ecf` |

Executable patch: **26,756 bytes**, SHA-256 `0c1eb0d0c0a9719d24a381f7875a6b5a3cce22dcf841bd6ef65043c1f3499bf0`.
Identity concatenates native `git diff --no-ext-diff --binary -- <path>` outputs in sorted path order;
the new transport file uses `git diff --no-ext-diff --binary --no-index -- NUL <path>`. Documentation excluded.
Both test files were identical for the retained-pair and candidate all-target executions.

## Fresh supplier assessment and major decision

Fresh NuGet indexes for [protocol](https://api.nuget.org/v3-flatcontainer/modelcontextprotocol/index.json),
[ASP.NET Core](https://api.nuget.org/v3-flatcontainer/modelcontextprotocol.aspnetcore/index.json) and
[Core](https://api.nuget.org/v3-flatcontainer/modelcontextprotocol.core/index.json) identify **2.2.0** as highest stable,
and **1.4.1** as newest original-major fallback. All three registration/catalog entries are listed,
non-prerelease, published **2026-08-13** (2.2.0), **2026-07-09** (1.4.1), **2026-06-04** (1.4.0).
No deprecation/vulnerability entries were present in those catalog responses; this is not a security audit.
Exact metadata URLs: `https://api.nuget.org/v3/registration5-gz-semver2/{lowercase-id}/{version}.json`
and `https://api.nuget.org/v3-flatcontainer/{lowercase-id}/{version}/{lowercase-id}.nuspec`.
The full metadata spool below includes catalog URLs, source hashes and complete nuspecs for all nine combinations.

**Coupling frozen before trial:** 2.2.0 ASP.NET Core -> protocol **[2.2.0]** -> Core **[2.2.0]**.
The 1.4.x edges are minima, not equality constraints. The candidate's exact ranges justify one pair/unit.
Protocol also requires Caching.Abstractions and Hosting.Abstractions >=10.0.10 on all supported TFMs.
The already accepted [Hosting.Abstractions](hosting-abstractions.md) direct **10.0.13** satisfies this prerequisite;
no neighbor pin moved, and arbitrary rollback of that prerequisite is not certified.
Core requires AI.Abstractions >=10.8.3 and Logging.Abstractions >=10.0.10; net8 additionally needs
Pipelines >=10.0.10, and net8/net9 need ServerSentEvents >=10.0.10.
All three packages' actual 1.4.0/1.4.1/2.2.0 archives contain native **net10/net9/net8** DLLs.
Protocol/Core also contain netstandard2.0, not selected here; ASP.NET Core retains Microsoft.AspNetCore.App.
Cached 1.4.0 metadata records `https://packagefeedproxy.microsoft.io/nuget/v3/index.json`;
2.2.0 records `https://api.nuget.org/v3/index.json`. All six cached archive SHA-256 values match freshly fetched public NuGet archives.

Reviewed tagged [support/versioning](https://github.com/modelcontextprotocol/csharp-sdk/blob/v2.2.0/docs/versioning.md),
[2.0 migration/release notes](https://github.com/modelcontextprotocol/csharp-sdk/releases/tag/v2.0.0),
[2.1](https://github.com/modelcontextprotocol/csharp-sdk/releases/tag/v2.1.0),
[2.2](https://github.com/modelcontextprotocol/csharp-sdk/releases/tag/v2.2.0) and
[1.4.1](https://github.com/modelcontextprotocol/csharp-sdk/releases/tag/v1.4.1).
The support policy reserves regular fixes for the latest major and critical/blocking fixes for one previous major.
It explicitly retains stable non-deprecated 1.x APIs for peers negotiating **2025-11-25 and earlier**;
experimental Tasks are the documented exception and are not used by Candle.
2.0 changes the default to stateless/discovery-first HTTP. Accepting that default would remove Candle's session/GET/DELETE behavior.
Tagged [transport options](https://github.com/modelcontextprotocol/csharp-sdk/blob/v2.2.0/src/ModelContextProtocol.AspNetCore/HttpServerTransportOptions.cs)
and [session modes](https://github.com/modelcontextprotocol/csharp-sdk/blob/v2.2.0/src/ModelContextProtocol.AspNetCore/HttpServerSessionMode.cs)
provide a stable, non-obsolete Stateful option; this one registration adaptation preserves the existing handshake.
It intentionally does not enable hybrid/stateless 2026-07-28 serving, MRTR, Apps, Tasks, caching hints or new tool semantics.
Structured-content and OAuth-client breaking changes do not apply to these custom text-result handlers.
Required `inputSchema` deserialization is compatible with the existing serialized tool definitions.
2.1 fixes HTTP fallback/status handling across TFMs; 2.2 adds optional hybrid serving/header decoding fixes.
1.4.1 backports SSE response-stream release on GET disconnect. It remains the researched fallback,
**not a trialled or failed candidate**: 2.2.0 passed without broad migration or suppressions, so no cap is justified.

## Fresh graph and actual assets

Normalization: sorted compact JSON, git-relative slash `csproj|target` keys, full package-only nodes,
`ensure_ascii=False`, UTF-8, no terminal LF. Original graph captured before edits and reproduced after retained build.
Both snapshots: **12 projects / 36 targets / 946 nodes**, with no added or removed package IDs.
Original: **387,238 bytes**, SHA-256 `95b71592e5a36a41a90264e94e25065ba77684d90889138aa2dd4d9a1f55943a`.
Candidate: **387,301 bytes**, SHA-256 `55bdad44a44eac1ad97e18493d6da1a944581ff267b73ad585cd84808fefd1e1`.
Full node/asset comparison: **30 target graphs unchanged**; only host/tests' six targets differ:

| Resolved change | Occurrences / explanation |
|---|---|
| Protocol, ASP.NET Core, Core 1.4.0 -> 2.2.0 | Six each; exact candidate closure, native matching-TFM compile/runtime assets |
| AI.Abstractions 10.5.2 -> 10.8.3 | Six; Core minimum; net8/net9 Json minimum rises 10.0.6 -> 10.0.10 |
| Caching.Abstractions 10.0.7 -> 10.0.10 | Five; host net10 uses framework pruning; Primitives minimum rises to 10.0.10 |
| ServerSentEvents 10.0.7 -> 10.0.10 | Four, host/tests net8/net9; framework supplies net10 |

These are **all 33 changed nodes**, including dependency-edge changes, not merely a version-list comparison.
Hosting/Logging.Abstractions and Primitives remain 10.0.13; net8/net9 Json/Pipelines remain 10.0.13.
Raised minima are already satisfied; no unrelated resolved node or asset metadata changes.
All 18 host/test output MCP DLLs byte-match the restored package's matching-TFM DLLs.
Live production startup logs load all three MCP assemblies from each test target's output:
**1.4.0.0 -> 2.2.0.0**; Hosting.Abstractions remains file **10.0.1326.50504**.

## Commands, cases and observed transport behavior

Windows **10.0.26200 x64**, SDK **10.0.400**, MSBuild **18.9.6**; .NET/ASP.NET runtimes
**10.0.11 / 9.0.19 / 8.0.30**. Commands from the repository root:

```powershell
dotnet build --configuration Release --framework net10.0
dotnet test --configuration Release --framework net10.0 --no-restore --verbosity normal --filter "FullyQualifiedName~fhir.candle.Tests.McpTests"
dotnet test --configuration Release --no-restore --verbosity normal --filter "FullyQualifiedName~fhir.candle.Tests.McpTests"
```

| Execution | Exit / result |
|---|---|
| Original B, session `phase17-original-build` | 0; 8 existing warnings, 0 errors |
| Original T-mcp, `phase17-original-tests` | 0; 2/2, no failures/skips; 8 existing warnings |
| Added-tests B on 1.4.0, `phase17-retained-build` | 0; 8 existing + one authoring xUnit2031 warning; corrected before testing |
| Retained all-target T-mcp, `phase17-retained-alltests` | 0; 9/9 per target, 27/27 total; 24 existing warnings |
| Candidate B, `phase17-candidate-build` | 0; 8 existing warnings, 0 errors, no new diagnostics |
| Candidate all-target T-mcp, `phase17-candidate-alltests` | 0; identical 27/27; 24 existing warnings, 0 errors |

Escalated once for native target-specific MCP assemblies, lower-TFM SSE dependencies and supplier HTTP-status changes.
Each all-target run supplies its net10 T-mcp gate: no duplicate net10 candidate test run.
Warnings are only Pkcs **NU1902/NU1903**, versions 10.0.0/8.0.1, host/tests; no suppression.
The xUnit2031 authoring diagnostic was fixed with Assert.Single's predicate overload, not attributed to the candidate.
Initial NuGet registration reading needed gzip decoding; corrected research probe, not restore/test failure.
CRLF-aware `git -c core.whitespace=cr-at-eol diff --check` passed; default Git misidentifies new Program CRs as trailing whitespace.

Full intended case occurrence comparison: namespace `fhir.candle.Tests.McpTests`; each row below occurs **three times**
in both all-target native logs, once per target. Zero missing/new cases, failures or skips.
VSTest truncates the long `expected` display value; the full unchanged InlineData value is recorded below,
and both distinct parameterized case identities and their occurrences were compared, not just aggregate totals.

| Class / method | Parameters |
|---|---|
| McpBasicTests.FhirMcpTools_CanBeConstructed | none |
| McpBasicTests.FhirMcpTools_ListTools_ReturnsExpectedTools | none; retains all original names, now requires exactly nine |
| McpTransportCompatibilityTests.HttpTransportInitializesAndListsTools | none |
| McpTransportCompatibilityTests.HttpTransportCallsStoreList | none |
| McpTransportCompatibilityTests.HttpTransportInvalidToolRequestPreservesResponse | `missingTool`, `Unknown tool: missingTool.` |
| Same method | `getResourceList`, `Store name is missing or not provided and is required` |
| McpTransportCompatibilityTests.HttpTransportUnsupportedMethodReturnsProtocolError | none |
| McpTransportCompatibilityTests.DisabledMcpDoesNotMapTransport | `null` (omitted) |
| Same method | `false` |

Tests call actual **Program.RunServer**, observe HostBuilding/HostBuilt as in HostingCompatibilityTests, and use real loopback Kestrel.
No copied MCP registration or controller-only factory; production handlers, singleton store and mapping run unchanged.
Observed: initialize 200, negotiated 2025-11-25, session header and tools capability; initialized notification 202;
tools/list returns the nine exact names, descriptions and input schemas; store-list returns exact R4/R4B/R5 URL/version text.
Unknown-tool/missing-store requests preserve text content (not invented `isError` results); unsupported RPC returns **-32601**,
then the same session still calls tools. GET SSE closes cleanly; DELETE 200; deleted-session request 404.
Omitted/false enablement gives no FhirMcpTools singleton and POST/GET `/mcp` **404**.
Each run owns empty source/cache and three synthetic tenants, disables registries/browser, isolates host config and key storage,
and restores environment, service statics and Program return state in CompatibilityHostCollection.
All **21 hosts per all-target run** complete cancellation/stop/disposal; clients/listeners/stores and temporary roots are removed.
No external runtime calls or real user cache/key-ring reads; no helper survives. These checks do not measure memory-leak size,
exercise arbitrary tools, certify new-protocol features, or replace final Windows full-suite/Linux/macOS follow-up.

## Raw evidence handoff

No redirected/manual transcript files were created. Full native test and graph tool spools are under
`C:\Users\ginoc\AppData\Local\Temp\`; parent should archive the files, not transcribe the summaries.
Build commands' full native stdout is in the named tool sessions above (no separate filesystem spool exposed).
Spool basenames and SHA-256:

| Evidence | Native spool basename | SHA-256 |
|---|---|---|
| Original graph | `1792192338179-copilot-tool-output-33128-a8ae1e4e-e99c-4cf9-83b7-ae2a3c43c04b.txt` | `d6e3c4d19fca97e0d04595e23790380bfb9a0f5e44e5789ba844c53a75d56729` |
| Original tests | `1792192355539-copilot-tool-output-33128-2c658f34-5a42-4625-916a-57a8762b3e7d.txt` | `2542b29c4ce9da602f6a180404dcb60fae0178fb724d067832962a31d29294e1` |
| Retained all-target tests | `1792192506113-copilot-tool-output-33128-408957e6-05fe-49f3-a9a2-d5bbd8bc3fce.txt` | `1f50e2ec4bca1886fc63afb06f007bf8eba6ee501f90b95bab0e97a4db31b82f` |
| Candidate all-target tests | `1792192560870-copilot-tool-output-33128-fe921339-1cd4-47fd-b004-e5786011ec31.txt` | `f2e14fa6b544d8cf4d5c902a4bd9662aba0dad989b374130df68b578e1ca05a4` |
| Candidate graph + full deltas | `1792192558693-copilot-tool-output-33128-42a6e5f0-404d-42a4-b307-5642d673022f.txt` | `bc56e64c7bbd10c0120d98935c85c8f41d06b843272daebf36ed61c9dcc6686c` |
| NuGet metadata | `1792192354747-copilot-tool-output-33128-c242c0d8-0a65-4100-aef4-0236756516e3.txt` | `d29123007242ff178d6cca9ece2ce9a6dcb257f2d25366b810c6e8c243057625` |
| Releases | `1792192370417-copilot-tool-output-33128-1d6fc22b-b6f0-48eb-9b5f-df041e1774d4.txt` | `d2fd607e206015cb280ef033ba6d9b62202c726ebd1cc10004b458da6df7b55e` |
| Support / transport options | `1792192380504-copilot-tool-output-33128-68babc56-75ec-424f-b296-c67c9ac831f0.txt` | `ea7c642ea229ca3e0a39ddd40dbb8a607d414288f9dbb6e7198b4217d56c84e8` |
