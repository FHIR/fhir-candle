# MailKit — deferred, original retained

**2026-10-16, Windows-local assessment: retain MailKit 4.17.0; candidate 4.18.1 not trialled.**
This is the phase-18 documentation-only retention route, not an accepted upgrade, compatibility cap,
failed candidate, security approval, or completed Windows mail gate. Final Windows and Linux/macOS validation remain pending.
Repository guidance: [AGENTS.md](../../../AGENTS.md); phase18, unit contract and V-accept were read.

## Material local prerequisite

Before changing pins or writing tests, inspected tracked source/tests, fixture/project declarations,
README, contributor/user/technical/manual documentation, Dockerfile and workflow files for SMTP/TLS procedures.
No applicable controlled local SMTP facility, trust fixture or procedure was found in that inspected scope;
the user supplied no local service/credentials. This does not assert that no SMTP service exists anywhere on the machine.
No personal files, credential/certificate stores or arbitrary listeners were scanned.

- [NotificationManager.cs:118–133, 196–200](../../../src/fhir-candle/Services/NotificationManager.cs#L118) reads SMTP configuration and dispatches the email channel.
- [Lines 485–512](../../../src/fhir-candle/Services/NotificationManager.cs#L485): private mail method rejects missing destination/host/user/port; `example.org` returns true **without delivery**.
- [Lines 429–473, 516–574, 616–628](../../../src/fhir-candle/Services/NotificationManager.cs#L429): MIME selection, text/HTML body, notification/event-range subjects and JSON/XML attachment names/content.
- [Lines 576–607](../../../src/fhir-candle/Services/NotificationManager.cs#L576): internally constructed `SmtpClient`; implicit TLS (`ConnectAsync(..., true, token)`), username/password authentication, send and graceful disconnect; ordinary certificate validation, no injected client seam.
- Success updates communication time only after that sequence; requested cancellation is rethrown; other exceptions retain logging and subscription error registration. [Lines 738–759, 883–994](../../../src/fhir-candle/Services/NotificationManager.cs#L738) track delivery, cancel transports and join shutdown.
- [LocalPackageFixture.cs:33–57](../../../src/fhir-candle.Tests/Extensions/LocalPackageFixture.cs#L33) explicitly clears SMTP host/user/password and sets port zero.
- [Sdk6NotificationTests.cs:101–120, 515–550](../../../src/fhir-candle.Tests/Sdk6NotificationTests.cs#L101) uses that configuration and an HTTP receiver; [LocalHttpFixture.cs:50–76](../../../src/fhir-candle.Tests/Controllers/LocalHttpFixture.cs#L50) starts loopback Kestrel, not trusted SMTP.
- [Existing compatibility record](../sdk6-compatibility.md), line 115, explicitly excludes SMTP-account evidence. The existing runner is xUnit v2/VSTest, not a mail facility.

A bounded peer alone cannot supply a genuinely trusted server certificate/private key and matching hostname.
No applicable existing trust material was identified through the permitted repository inspection.
No server/helper was started, certificate/tool/package installed, trust callback bypassed, or external account contacted.
Neither an invalid self-signed handshake, isolated MIME assertion, pre-canceled token nor REST-hook pass would prove successful authenticated SMTP delivery.
`MailKitCompatibilityTests.cs` remains absent; no speculative tests or source adaptations were created.

## Fresh supplier and migration review

Public NuGet [MailKit index](https://api.nuget.org/v3-flatcontainer/mailkit/index.json) and
[MimeKit index](https://api.nuget.org/v3-flatcontainer/mimekit/index.json) both identify **4.18.1** as highest stable.
Fresh registration/catalog entries mark both listed: [MailKit](https://api.nuget.org/v3/registration5-gz-semver2/mailkit/4.18.1.json), published **2026-09-27**;
[MimeKit](https://api.nuget.org/v3/registration5-gz-semver2/mimekit/4.18.1.json), published **2026-09-19**.
Both original 4.17.0 entries are also listed, published 2026-05-26. These four entries contain no deprecation/vulnerability records; that is not a closure security audit.
Tagged security policies for [MailKit](https://raw.githubusercontent.com/jstedfast/MailKit/4.18.1/SECURITY.md) and
[MimeKit](https://raw.githubusercontent.com/jstedfast/MimeKit/4.18.1/SECURITY.md) reserve security fixes for the **latest version** despite their broad 4.x support tables; retention is not a support guarantee.
Tagged READMEs describe cross-platform libraries but retain older solution-TFM prose; actual nuspec/archive assets govern this assessment.

Reviewed tagged [MailKit release notes](https://raw.githubusercontent.com/jstedfast/MailKit/4.18.1/ReleaseNotes.md):
4.18.0 fixes native NTLM multi-stage authentication, changes IMAP behavior/performance and raises ASN.1/MimeKit minima;
4.18.1 fixes Linux/macOS telemetry durations and raises MimeKit to 4.18.1. No SMTP call-site break is identified by these notes.
Reviewed tagged [MimeKit release notes](https://raw.githubusercontent.com/jstedfast/MimeKit/4.18.1/ReleaseNotes.md):
HTML unsafe-tag handling, empty address conversion, SubjectKeyIdentifier null handling, >2GB MIME reading,
international Message-Id parsing, TNEF overflow protection and raised crypto dependencies.
The documented BouncyCastle 2.7.0 behavior break rejects S/MIME decryption using Blowfish/CAST5/RC2 variants;
this outbound body/attachment path performs no such decryption. The suggested lenient-RSA switch was **not** applied.
No release-note finding proves this consumer incompatible or removes any supported Candle TFM; real transport evidence is still missing.
GitHub release-object API lookups returned 404; the successfully fetched version-tagged release-note files above supplied the review.

## All-target metadata closure, not a candidate restore

Compared complete original/candidate nuspecs: MailKit [4.17.0](https://api.nuget.org/v3-flatcontainer/mailkit/4.17.0/mailkit.nuspec) / [4.18.1](https://api.nuget.org/v3-flatcontainer/mailkit/4.18.1/mailkit.nuspec),
MimeKit [4.17.0](https://api.nuget.org/v3-flatcontainer/mimekit/4.17.0/mimekit.nuspec) / [4.18.1](https://api.nuget.org/v3-flatcontainer/mimekit/4.18.1/mimekit.nuspec).
Actual archives for all four contain DLLs for net10.0, net8.0, net462/net47/net48 and netstandard2.0/2.1; **no native net9.0**.
Existing host/test net9 selects net8 MailKit/MimeKit assets; candidate layout retains that compatibility path, not a framework drop.
MailKit 4.18.1 requires MimeKit >=4.18.1 and System.Formats.Asn1 >=10.0.0 in every group.
MimeKit 4.18.1 requires Pkcs >=10.0.0 and BouncyCastle.Cryptography >=2.7.0 in both applicable net8/net10 groups.
These are minimum ranges, **not equality constraints**; no separate direct transitive pin was added.

| Host and tests | Existing resolved closure/assets | Candidate metadata implications (unresolved) |
|---|---|---|
| net10.0 | MailKit/MimeKit 4.17.0, net10 DLLs; Pkcs 10.0.0, net10 + Windows runtime asset | MailKit/MimeKit >=4.18.1; Pkcs minimum unchanged; BouncyCastle minimum rises |
| net9.0 | MailKit/MimeKit 4.17.0 and Pkcs 8.0.1, net8 DLLs + Pkcs Windows runtime asset | Pkcs >=10.0.0 offers native net9 + Windows runtime; requires ASN.1/Bcl.Cryptography >=10.0.0 |
| net8.0 | MailKit/MimeKit 4.17.0 and Pkcs 8.0.1, net8 DLLs + Pkcs Windows runtime asset | Pkcs >=10.0.0 retains net8 + Windows runtime; requires ASN.1/Bcl.Cryptography >=10.0.0 |

Recursive nuspec/archive review: Pkcs [8.0.1](https://api.nuget.org/v3-flatcontainer/system.security.cryptography.pkcs/8.0.1/system.security.cryptography.pkcs.nuspec) has no net8 dependencies;
[10.0.0](https://api.nuget.org/v3-flatcontainer/system.security.cryptography.pkcs/10.0.0/system.security.cryptography.pkcs.nuspec) has no net10 dependencies, with the net8/net9 edges shown above.
BouncyCastle [2.6.2](https://api.nuget.org/v3-flatcontainer/bouncycastle.cryptography/2.6.2/bouncycastle.cryptography.nuspec) / [2.7.0](https://api.nuget.org/v3-flatcontainer/bouncycastle.cryptography/2.7.0/bouncycastle.cryptography.nuspec) have dependency-free net6 assets applicable to all three targets; existing graphs select 2.6.2.
Existing Bcl.Cryptography 10.0.2 satisfies that candidate minimum on all TFMs; its [nuspec](https://api.nuget.org/v3-flatcontainer/microsoft.bcl.cryptography/10.0.2/microsoft.bcl.cryptography.nuspec) requires ASN.1 >=10.0.2 only on net8.
Existing ASN.1 is 10.0.2 on net8, absent as a package on net9/net10; candidate net9 introduces a >=10.0.0 requirement.
ASN.1 [10.0.0](https://api.nuget.org/v3-flatcontainer/system.formats.asn1/10.0.0/system.formats.asn1.nuspec) / [10.0.2](https://api.nuget.org/v3-flatcontainer/system.formats.asn1/10.0.2/system.formats.asn1.nuspec) have dependency-free net8/net9/net10 groups. Legacy-only dependencies are not selected here.
Fresh Pkcs 8.0.1/10.0.0 catalogs both report [high GHSA-846f-f764-76mx](https://github.com/advisories/GHSA-846f-f764-76mx) and [moderate GHSA-2fmv-rv2c-59x7](https://github.com/advisories/GHSA-2fmv-rv2c-59x7).
Retention leaves those known baseline warnings unresolved; the candidate's 10.0.0 minimum does not establish remediation. No warning suppression or unrelated security pin change.

## Identity and verification performed

Clean predecessor reverified: `3502b0474f3e30eaf3c68ab9f3dba214c5b1f0f9`; index empty.
Namespace-aware XML comparison: **46 direct declarations / 36 IDs / seven owners unchanged**, including conditions/aliases/assets.
Read-only existing assets reproduce **12 projects / 36 targets / 946 package nodes, 387,301 bytes**:
SHA-256 `55bdad44a44eac1ad97e18493d6da1a944581ff267b73ad585cd84808fefd1e1`.
Normalization matches [predecessor MCP record](mcp.md): full package nodes, sorted compact JSON, slash `csproj|target` keys, UTF-8, `ensure_ascii=False`, no terminal LF.
Raw unchanged file SHA-256:
- Host csproj: `02e6c4fffbf3e89b07ae87d87966062c85e6f6b1999831e825025d2198d00f1b`.
- NotificationManager.cs: `85ac36940a3f19cb9072fde715ba11542734cf897ad6cf342eda68ccf74b47d3`.
- Sdk6NotificationTests.cs: `5d8435f9772bd60aa18b07a57f0259870d444ed5219601e056fc64d3140a502a`.

Performed: Git status/identity/diff checks, read-only source/procedure searches, XML/graph/hash checks and public supplier GETs; package ZIPs inspected in memory only. Documentation links/claims checked.
**Not performed:** restore, build, tests, SMTP/TLS/auth/send/disconnect/cancellation/error execution, candidate graph resolution, or platform validation. No runtime result or case count is claimed.
B (`dotnet build --configuration Release --framework net10.0`) and the plan's exact T-mail filter were deliberately not run for documentation-only retention.

## Reconsideration conditions

Provide an available controlled local implicit-TLS SMTP peer with usable synthetic authentication, a matching-hostname certificate/private key valid under existing standard trust, bounded capture/failure/cancellation controls and cleanup, without installation, policy relaxation or external-account delivery.
Then revalidate newest stable/support/closure; add the owned real-mail regressions and run baseline/candidate B and exact T-mail with nonempty intended cases, all-TFM graphs/assets and applicable Windows lifecycle checks.
Prove unchanged subjects, text/HTML bodies, JSON/XML attachments, diagnostics, successful AUTH/DATA/QUIT and real in-flight cancellation boundaries; synthetic success shortcuts cannot satisfy this gate.
Any required extra owner/direct pin/trust/architecture change or pre-existing product defect must be reported separately. Linux/macOS mail validation remains post-pass work, not the reason for this local deferral.
