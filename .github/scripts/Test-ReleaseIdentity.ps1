# <copyright file="Test-ReleaseIdentity.ps1" company="Microsoft Corporation">
#     Copyright (c) Microsoft Corporation. All rights reserved.
#     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
# </copyright>

param(
    [Parameter(Mandatory)][string] $Version,
    [Parameter(Mandatory)][string] $Tag,
    [string] $ExpectedCommit,
    [switch] $RequireNewVersion,
    [switch] $RequirePublishedRelease
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ($Version -cnotmatch '^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\z')
{
    throw 'Version must have three numeric components with no leading zeros.'
}
foreach ($component in $Version.Split('.'))
{
    [int] $number = 0
    if (![int]::TryParse($component, [ref] $number) -or $number -gt 65534)
    {
        throw 'Version components must fit the .NET assembly version limits (0-65534).'
    }
}
if ($Tag -cne "v$Version")
{
    throw "Tag must be exactly 'v$Version'."
}
if ($ExpectedCommit -and $ExpectedCommit -cnotmatch '^[0-9a-f]{40}\z')
{
    throw 'ExpectedCommit must be a full lowercase commit SHA.'
}

git fetch --no-tags origin '+refs/heads/main:refs/remotes/origin/main' "+refs/tags/${Tag}:refs/tags/${Tag}" | Out-Host
if ($LASTEXITCODE -ne 0) { throw 'Cannot fetch the release tag and main branch.' }
$commit = git rev-parse --verify "refs/tags/${Tag}^{commit}"
if ($LASTEXITCODE -ne 0) { throw 'Release tag must resolve to a commit.' }
$commit = $commit.Trim()
if ($ExpectedCommit)
{
    $headCommit = (git rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0 -or $headCommit -cne $ExpectedCommit -or $commit -cne $ExpectedCommit)
    {
        throw 'The checkout or release tag no longer matches the validated commit.'
    }
}
git merge-base --is-ancestor $commit refs/remotes/origin/main
if ($LASTEXITCODE -ne 0) { throw 'Release commit must be contained in origin/main.' }

$changelog = git show "${commit}:changelog.md"
if ($LASTEXITCODE -ne 0) { throw 'Cannot read the release changelog.' }
if (@($changelog | Where-Object { $_ -cmatch "^\* v$([regex]::Escape($Version))(?:\s.*)?$" }).Count -ne 1)
{
    throw "Prepare exactly one changelog.md release entry for 'v$Version' before tagging."
}

if ($RequireNewVersion)
{
    $index = Invoke-RestMethod 'https://api.nuget.org/v3-flatcontainer/fhir-candle/index.json'
    if ($index.versions -contains $Version)
    {
        throw "NuGet version '$Version' is already published. Never rebuild or overwrite it."
    }
    foreach ($published in $index.versions)
    {
        if ($published -cmatch '^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\z' -and
            [version] $published -ge [version] $Version)
        {
            throw "Release version '$Version' must be newer than published version '$published'."
        }
    }
}

if ($RequirePublishedRelease)
{
    if ([string]::IsNullOrEmpty($env:GITHUB_REPOSITORY))
    {
        throw 'GITHUB_REPOSITORY is required to verify the published release.'
    }
    $releaseJson = gh api "repos/$env:GITHUB_REPOSITORY/releases/tags/$Tag"
    if ($LASTEXITCODE -ne 0) { throw 'Cannot verify the published GitHub Release.' }
    $release = ($releaseJson -join [Environment]::NewLine) | ConvertFrom-Json
    if ($release.draft -or !$release.published_at -or $release.tag_name -cne $Tag)
    {
        throw 'The matching GitHub Release must still be published.'
    }
}

$commit
