# coverlet.collector — deferred; retain 10.0.1

**2026-10-16, phase 30: evaluate 10.1.0; retain 10.0.1 without a collector trial.**
Inspected predecessor: **`839613a4e08bc39ce9fcf689db5233177c5b206f`**; initial worktree and index clean.
Authority: [AGENTS.md](../../../AGENTS.md), phase 30, and the preceding [Test SDK](test-sdk.md) / [adapter](xunit-vs-adapter.md) dossiers.
Only this document changes. Deferral is a missing sanctioned collection-verification procedure, **not a demonstrated runtime incompatibility**.

## Fresh supplier evidence (accessed 2026-10-16)

[NuGet's index][index] still identifies **10.1.0** as highest stable; [registration][candidate] marks it listed, published **2026-09-27 21:36:27.42 UTC**.
[Retained 10.0.1][retained] is also listed, published **2026-05-18 08:04:46.64 UTC**. Neither listing establishes collection safety.
The [10.0.1][nuspec-old] / [10.1.0 nuspecs][nuspec] declare `developmentDependency=true` and **no package dependency edges**; no declared edge forces a Test SDK/adapter co-update.
Their repository commits are **`d21b5b6a08d48f51405ba2c5c5660f91a565776d`** (10.0.1) and **`10a8051216eeaebb661624eef348ee768693fed9`** (10.1.0).
The registration records link the inspected package archives.
Archives were inspected in memory, not installed/restored: SHA-256 10.0.1 **`ea7679a96bf2d523d833d49f61b6447461ab12b76738f96e65780927f8626abc`**;
10.1.0 **`e466ffb46fcf15b88de183f214baccceccc624205c6ec6040848f90ecc962fcb`**.

| Boundary | Supplier declaration and current repository |
|---|---|
| Runtime / SDK | Both packaged and tagged [10.0.1][guide-old] / [10.1.0][guide] integration guides support .NET Core **>=8.0** (.NET Framework >=4.7.2 separately) and require SDK **>=8.0.414**. |
| Test SDK | Guides identify **18.4.0** as minimum; 10.1.0's example references **18.5.1**. Actual **18.11.0** exceeds both. The [old][readme-old] / [candidate README][readme] minima (SDK 8.0.112 / Test SDK 17.12.0) are weaker; use the stricter integration guide. |
| Existing runner | **VSTest**, xunit **2.9.3**, xunit.runner.visualstudio **4.0.1**. [Adapter support][adapter] covers .NET 8+ and xUnit 1.9.2+. SDK/TestPlatform.TestHost/ObjectModel/CodeCoverage remain **18.11.0**. |
| Target assets | Both archives contain matching **net10.0/net9.0/net8.0** collector build targets; 10.1.0 also adds netstandard2.0 targets. No `lib/` assets or package runtimeconfig. Existing restored 10.0.1 nodes select each matching-TFM target. |
| Local environment | Prior adapter execution used SDK **10.0.400**, .NET/ASP.NET Core **10.0.11 / 9.0.19 / 8.0.30**; installed directories reconfirmed read-only. Generated runtimeconfigs still request **10.0.0 / 9.0.0 / 8.0.0**, without patch overrides. |

The 10.1.0 guide explicitly rejects **MTP v2**, not VSTest on .NET 10. Its example only targets net8/net9; that is not proof that net10 collection passed or a target-removal decision.
The README's broad .NET 10/default-MTP wording must be read against [Microsoft's CLI guidance][cli]: VSTest remains the default unless MTP is selected.
No checked-in global.json, MTP package or runner opt-in was found. No runner switch, `TestingPlatformDotnetTestSupport` override or replacement collector is proposed.
The stated SDK/runtime envelope is met; no higher servicing-patch minimum was found in these collector sources. This is eligibility evidence, **not tested collector compatibility**, nor a claim that installed patches are current.
[10.1.0 release notes][release] include record/auto-property and branch instrumentation fixes, process-exit hit-file races and empty-coverage fixes; MTP-specific changes do not migrate this project.

## Preserved source, graph and privacy

[Test project](../../../src/fhir-candle.Tests/fhir-candle.Tests.csproj): collector stays **10.0.1** with `PrivateAssets=all` and `IncludeAssets=runtime; build; native; contentfiles; analyzers; buildtransitive`.
The adapter retains the same privacy metadata. Test-only assets do not become public dependencies; target conditions, aliases, fixture copies, runner configuration and all code remain untouched.
Read-only XML inventory remains **46 direct references / 36 IDs / seven owners**. Existing full package-only graph remains **12 projects / 36 targets / 946 nodes / 387,338 bytes**:
**SHA-256 `1069cacc596aab8d664278b593c251fe746fbfea494d6af5c55876d45eb4a464`**.
Normalization: sorted compact JSON, slash-separated git-relative `csproj|target` to full package-only nodes, all metadata retained, `ensure_ascii=False`, UTF-8, no final LF.
Source map remains **384 files / 47,054 bytes**, SHA-256 **`49d74c79a2e1418a52ebc6303cae01f35ffbfb35d62ebff6dcd7f8d896ec5cf3`**.
Source normalization: the same compact JSON encoding of sorted tracked git-relative `src/` paths to raw-file SHA-256. Test csproj remains **4,952 bytes**, SHA-256 **`b416f20955580cd4cc1e279379046381a75046459325369d7744b6061193a1cd`**.
These reproduce the adapter checkpoint, not a new restore or candidate graph.

## Missing procedure, outcome and reconsideration

Fresh inspection of AGENTS.md, README, all checked-in workflows and docs, including [building/testing](../building-and-testing.md), found **no sanctioned collector-enabled invocation**.
The existing workflow runs ordinary VSTest tests. Supplier examples are not repository authorization; no collection argument, runsettings, runner flag, tool installation or pin edit was introduced.
Missing evidence: an authorized invocation on this test project across **net10.0/net9.0/net8.0**, plus observable collector activation, nonempty expected coverage/report contents, instrumentation and cleanup behavior, and preserved case/result inventories.
Neither 10.0.1 nor 10.1.0 has collection execution evidence in this assessment. **Deferred; retain 10.0.1**, rather than label 10.1.0 incompatible or an untested upgrade safe.
To unblock, obtain an explicitly sanctioned procedure and revise the plan **before any version edit**, specifying exact commands, prerequisites, project ownership and acceptance cases.
Then revalidate availability/support/assets and verify retained/candidate collection under that procedure, preserving VSTest, xUnit v2, all three targets and asset privacy; record actual reports/results and resolve any failures.
The [adapter dossier](xunit-vs-adapter.md) attributes the final implementation's ordinary Windows suite: **4,454 passed per TFM, zero failed/skipped; 13,362 aggregate occurrences**.
Those prior results are **not collection proof** and were not rerun here. Linux/macOS × all three TFMs remain **pending post-pass validation**, not this local deferral's reason.
Phase 30 ran only read-only metadata/source/procedure/hash/link checks and document whitespace validation; **no build, restore, test, collection, stage or commit**. Final roll-up remains separate.

[index]: https://api.nuget.org/v3-flatcontainer/coverlet.collector/index.json
[candidate]: https://api.nuget.org/v3/registration5-gz-semver2/coverlet.collector/10.1.0.json
[retained]: https://api.nuget.org/v3/registration5-gz-semver2/coverlet.collector/10.0.1.json
[nuspec]: https://api.nuget.org/v3-flatcontainer/coverlet.collector/10.1.0/coverlet.collector.nuspec
[nuspec-old]: https://api.nuget.org/v3-flatcontainer/coverlet.collector/10.0.1/coverlet.collector.nuspec
[guide]: https://raw.githubusercontent.com/coverlet-coverage/coverlet/v10.1.0/Documentation/VSTestIntegration.md
[guide-old]: https://raw.githubusercontent.com/coverlet-coverage/coverlet/v10.0.1/Documentation/VSTestIntegration.md
[readme]: https://raw.githubusercontent.com/coverlet-coverage/coverlet/v10.1.0/README.md
[readme-old]: https://raw.githubusercontent.com/coverlet-coverage/coverlet/v10.0.1/README.md
[adapter]: https://raw.githubusercontent.com/xunit/visualstudio.xunit/4.0.1/README.md
[cli]: https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-test
[release]: https://github.com/coverlet-coverage/coverlet/releases/tag/v10.1.0
