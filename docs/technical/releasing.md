# Release process

The **Publish dotnet tool** workflow is the release entry point for NuGet and
both GHCR images. It runs when a GitHub Release for **`v<version>`** is
published. Merges to `main`, tag pushes, and draft releases do not publish.
This follows the process in `dotnet-fhir-packages` and `CSharpLightDbGen`,
adapted to one server tool rather than paired packages or an analyzer.

Manual runs require an existing tag and its matching version and are
**validation-only**. They run exact-source CI, pack and qualify the tool, and
build both container variants without logging into GHCR, pushing packages or
images, generating registry attestations, or restarting the deployment.
Maintainers dispatch and publish; agents must not dispatch the release
workflow or publish packages locally.

## Administrator preflight

Before the first release using this workflow:

1. Configure the protected GitHub environment **`nuget.org`** with required
   reviewers or equivalent deployment protection. Allow `v*` release tags.
2. Set **`NUGET_PUBLISH_ENVIRONMENT_READY=true`** in that environment only
   after configuring its protection.
3. Store **`NUGET_USER`** as an environment secret, using the nuget.org profile
   username, not an email address.
4. Configure a [NuGet trusted-publishing policy](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing)
   for owner **`FHIR`**, repository **`fhir-candle`**, workflow filename
   **`nuget-tool.yml`** (without the directory), and environment **`nuget.org`**.
   The selected user or organization must own and permit new versions of
   **`fhir-candle`**.
5. Confirm GitHub Actions may write the repository's GHCR package and that
   **`KUBE_CONFIG`** remains available for the deployment workflow.

Only `publish-nuget` uses `NuGet/login@v1` to exchange its GitHub OIDC token
for a temporary NuGet API key. Source CI, packing, and tool qualification have
read-only repository permissions and no publishing secrets. The container
jobs use `GITHUB_TOKEN` for GHCR; their separate OIDC permission is for image
attestations. Committing these workflows does not configure external settings.

## Version and source identity

Keep the three-component date-based version convention without leading zeros.
For example, 09:00 on November 13, 2026 normalizes to **`2026.1113.900`**,
with tag **`v2026.1113.900`**. Each component must fit `0-65534` for .NET
assembly metadata. Prerelease suffixes and unprefixed tags are rejected.

Local builds retain their clock-based versions. Release build and pack
commands override `VersionPrefix`, `Version`, and `PackageVersion` with the
approved version, `AssemblyVersion` and `FileVersion` with `0.<version>`,
and `RepositoryCommit` with the full source SHA. Container builds receive the
same identity through Docker build arguments. Do not rename or rewrite an
archive, edit project versions, move an existing release tag, or change the
machine clock.

The validator freshly fetches the tag and `origin/main`, requires the tag
commit to be contained in `main`, and rejects versions already published to
NuGet or older than any existing canonical three-component version. Release
events must still match their original event SHA. Reusable source CI builds
and tests that exact commit on Windows, Linux, and macOS for both target
frameworks, independently of earlier CI runs on `main`.

## Prepare and publish

1. Choose the version and prepare its entry in `changelog.md`: insert
   **`* v<version>`** beneath `* Work in Progress`, move the accumulated
   indented entries under the new version, and leave the work-in-progress
   section empty. Preserve historical entries. The tagged commit must contain
   exactly one matching release entry.
2. Merge the release records and source to `main`, and require normal source
   CI to pass.
3. Create an annotated `v<version>` tag on that release commit and push it
   with explicit publishing authorization.
4. Optionally run validation first. For maintainers:

   ```powershell
   gh workflow run nuget-tool.yml --ref main -f version=<version> -f tag=v<version>
   ```

   Require all exact-source, package-consumer, and container-build jobs to
   pass. This does not reserve the version or publish; the release run
   creates and qualifies its own package.
5. Publish a GitHub Release for the existing tag. Link its notes to the
   changelog entry and describe publication as pending until the workflow
   completes.
6. Compare the candidate's source SHA and SHA-512 in the pack job summary
   with the intended release, review all six tool-consumer results, and
   approve `publish-nuget` in the protected environment.
7. Confirm the exact version appears on NuGet and both versioned GHCR images
   exist. Record the source SHA and package hash in the release notes.

Runs for the same tag are serialized without cancelling an active run.
NuGet publishes first; image publication waits for that protected job to
succeed. Manual validation still builds both images after tool qualification,
despite skipping the NuGet publisher. A later image failure does not roll back
the accepted NuGet package. The deployment restarts only after the complete
published-release workflow succeeds. Its separate manual restart remains
available.

## One qualified tool package

After exact-source CI, the pack job builds once with the release identity
and packs once with `--no-build`. Its immutable artifact contains:

```text
fhir-candle.<version>.nupkg
package.sha512
```

Validation checks the nuspec identity and repository commit, `DotnetTool`
command settings, exactly the `net11.0` and `net10.0` payloads, assembly/file
versions, framework runtime configuration, and framework-specific Blazor,
FluentUI, and Monaco assets. Duplicate ZIP entries or additional packages are
rejected.

Each Windows/Linux/macOS and .NET 11/10 consumer downloads the same artifact
by ID, fails on an artifact-digest mismatch, and verifies its SHA-512. It
installs the requested framework from the candidate-only NuGet source with
an isolated cache, checks the installed archive's hash, and starts the actual
tool shim in Production mode with empty data and a private FHIR package cache.
It verifies R4/R4B/R5 capability metadata, Patient create/search/delete, and
the UI and shipped JavaScript assets. These are artifact smoke checks, not
browser interaction or a substitute for the full source-test matrix.

The NuGet publisher downloads and rechecks the same bytes, tag, ancestry,
checkout, and still-published GitHub Release. **It never builds or packs.**
There is no duplicate-skipping push that could hide a conflicting version.
Candidate artifacts are retained for 90 days; archive them for longer-term
evidence if needed.

## Containers and deployment

The two container workflows are reusable release jobs, not independent
publish entry points. They build the validated commit for Linux amd64 and
arm64 and attest published images. The standard image gets **`<version>`**
and **`latest`** tags; the chiseled-extra image gets **`<version>-minimal`**
and **`minimal`** tags. Neither alias depends on the release tag being the
default branch. Runtime images, ports, and Kubernetes manifests are unchanged.

Container publication requires the original release SHA and a still-published
matching GitHub Release. Manual validation builds without pushing; it does
not claim runtime qualification of either container image.

The **Restart Deployment** workflow listens for successful **Publish dotnet
tool** runs whose original event was `release` in this repository. There is
no `main` branch filter that would discard tagged releases, and successful
validation-only runs cannot trigger an automatic restart.

## Failure and retry

If validation, source CI, packing, or tool qualification fails, nothing is
published. A source fix needs a new commit, version, and tag; never move an
existing release tag.

If external environment, registry, or deployment configuration fails, correct
the external problem and **re-run failed jobs** in the original run. Do not
re-run all jobs to rebuild an already qualified NuGet candidate or dispatch
another publishing run. If NuGet already accepted the version, stop and
inspect it rather than treating a duplicate as success. There is no
paired-package recovery mechanism.

Keep `GINOC_NUGET` only while outstanding releases tagged with the old
API-key workflow still need it, then revoke that key and delete the secret.
Old tags keep their original workflow; never move a release tag to adopt
trusted publishing.
