# <copyright file="Test-ReleaseScripts.ps1" company="Microsoft Corporation">
#     Copyright (c) Microsoft Corporation. All rights reserved.
#     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
# </copyright>

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$scripts = Split-Path $PSScriptRoot
$identity = Join-Path $scripts 'Test-ReleaseIdentity.ps1'
$packageValidator = Join-Path $scripts 'Test-ReleasePackage.ps1'
$version = '2026.1113.900'
$tag = "v$version"
$commit = 'a' * 40
$testState = @{
    TagCommit = $commit
    HeadCommit = $commit
    OnMain = $true
    Changelog = @('* Work in Progress', '', "* $tag")
    Versions = @('2026.1112.1700')
    Draft = $false
    Tag = $tag
    Fetches = 0
    Passed = 0
}

function Assert-Fails
{
    param([scriptblock] $Action, [string] $Message)
    try
    {
        & $Action | Out-Null
    }
    catch
    {
        if (!$_.Exception.Message.Contains($Message))
        {
            throw "Expected '$Message', got '$($_.Exception.Message)'."
        }
        $testState.Passed++
        return
    }
    throw "Expected failure containing '$Message'."
}

function git
{
    $global:LASTEXITCODE = 0
    switch ($args[0])
    {
        'fetch' { $testState.Fetches++ }
        'rev-parse' { if ($args[-1] -ceq 'HEAD') { $testState.HeadCommit } else { $testState.TagCommit } }
        'merge-base' { if (!$testState.OnMain) { $global:LASTEXITCODE = 1 } }
        'show' { $testState.Changelog }
        default { throw "Unexpected git invocation: $args" }
    }
}

function Invoke-RestMethod
{
    param([string] $Uri)
    if ($Uri -cne 'https://api.nuget.org/v3-flatcontainer/fhir-candle/index.json') { throw "Unexpected URI: $Uri" }
    @{ versions = $testState.Versions }
}

function gh
{
    $global:LASTEXITCODE = 0
    @{ draft = $testState.Draft; published_at = '2026-11-13T15:00:00Z'; tag_name = $testState.Tag } | ConvertTo-Json
}

$root = Join-Path ([System.IO.Path]::GetTempPath()) "fhir-candle-release-tests-$([guid]::NewGuid().ToString('N'))"
$previousRepository = $env:GITHUB_REPOSITORY
New-Item -ItemType Directory -Path $root | Out-Null
try
{
    foreach ($invalid in @('2026.0113.900', '2026.1113.0900', '2026.1113', '2026.1113.900-preview', "2026.1113.900`n"))
    {
        Assert-Fails { & $identity -Version $invalid -Tag "v$invalid" } 'three numeric components'
    }
    Assert-Fails { & $identity -Version '2026.1113.65535' -Tag 'v2026.1113.65535' } 'assembly version limits'
    Assert-Fails { & $identity -Version $version -Tag "V$version" } "Tag must be exactly"
    Assert-Fails { & $identity -Version $version -Tag $tag -ExpectedCommit ('A' * 40) } 'full lowercase commit SHA'
    if ($testState.Fetches -ne 0) { throw 'Invalid release input reached git fetch.' }
    if ((& $identity -Version $version -Tag $tag -ExpectedCommit $commit -RequireNewVersion) -cne $commit)
    {
        throw 'Valid release identity did not resolve the exact commit.'
    }
    $testState.Passed++
    $testState.TagCommit = 'b' * 40
    Assert-Fails { & $identity -Version $version -Tag $tag -ExpectedCommit $commit } 'no longer matches'
    $testState.TagCommit = $commit
    $testState.HeadCommit = 'b' * 40
    Assert-Fails { & $identity -Version $version -Tag $tag -ExpectedCommit $commit } 'no longer matches'
    $testState.HeadCommit = $commit
    $testState.OnMain = $false
    Assert-Fails { & $identity -Version $version -Tag $tag } 'contained in origin/main'
    $testState.OnMain = $true
    $testState.Changelog = @("* $tag", "* $tag")
    Assert-Fails { & $identity -Version $version -Tag $tag } 'exactly one changelog.md release entry'
    $testState.Changelog = @('* Work in Progress')
    Assert-Fails { & $identity -Version $version -Tag $tag } 'exactly one changelog.md release entry'
    $testState.Changelog = @("* $tag")
    $testState.Versions = @($version)
    Assert-Fails { & $identity -Version $version -Tag $tag -RequireNewVersion } 'already published'
    $testState.Versions = @('2026.1114.900')
    Assert-Fails { & $identity -Version $version -Tag $tag -RequireNewVersion } 'must be newer'
    $env:GITHUB_REPOSITORY = 'FHIR/fhir-candle'
    $testState.Draft = $true
    Assert-Fails { & $identity -Version $version -Tag $tag -RequirePublishedRelease } 'must still be published'
    $testState.Draft = $false
    if ((& $identity -Version $version -Tag $tag -RequirePublishedRelease) -cne $commit)
    {
        throw 'Published release identity did not resolve the exact commit.'
    }
    $testState.Passed++

    $assemblyPath = Join-Path $root 'fhir-candle.dll'
    $fixtureProject = Join-Path $root 'fixture.csproj'
    @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net11.0</TargetFramework>
    <AssemblyName>fhir-candle</AssemblyName>
    <AssemblyVersion>0.$version</AssemblyVersion>
    <FileVersion>0.$version</FileVersion>
  </PropertyGroup>
</Project>
"@
        | Set-Content -LiteralPath $fixtureProject
    dotnet build $fixtureProject --configuration Release --output $root --verbosity quiet "-p:RestoreSources=$root"
    if ($LASTEXITCODE -ne 0) { throw 'Release test fixture compilation failed.' }
    $candidate = Join-Path $root 'candidate'
    New-Item -ItemType Directory -Path $candidate | Out-Null
    $packagePath = Join-Path $candidate "fhir-candle.$version.nupkg"
    $uiAssets = @(
        'js/candle-theme.js',
        'css/candle-theme.css',
        'css/app.css',
        'fhir-candle.styles.css',
        '_content/Microsoft.FluentUI.AspNetCore.Components/css/default-fuib.css',
        '_content/Microsoft.FluentUI.AspNetCore.Components/Microsoft.FluentUI.AspNetCore.Components.bundle.scp.css',
        '_content/Microsoft.FluentUI.AspNetCore.Components/Microsoft.FluentUI.AspNetCore.Components.lib.module.js',
        '_framework/blazor.web.js',
        '_content/BlazorMonaco/jsInterop.js'
    )
    $archive = [System.IO.Compression.ZipFile]::Open($packagePath, [System.IO.Compression.ZipArchiveMode]::Create)
    try
    {
        $entries = @{
            'fhir-candle.nuspec' = @"
<package><metadata><id>fhir-candle</id><version>$version</version><repository url="https://github.com/FHIR/fhir-candle" commit="$commit" /><packageTypes><packageType name="DotnetTool" /></packageTypes></metadata></package>
"@
            'README.md' = 'Release fixture'
            'LICENSE' = 'MIT'
        }
        foreach ($framework in @('net11.0', 'net10.0'))
        {
            $prefix = "tools/$framework/any"
            [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $assemblyPath, "$prefix/fhir-candle.dll") | Out-Null
            $entries["$prefix/fhir-candle.deps.json"] = '{}'
            $entries["$prefix/fhir-candle.runtimeconfig.json"] = "{`"runtimeOptions`":{`"tfm`":`"$framework`"}}"
            $entries["$prefix/DotnetToolSettings.xml"] = '<DotNetCliTool><Commands><Command Name="fhir-candle" EntryPoint="fhir-candle.dll" Runner="dotnet" /></Commands></DotNetCliTool>'
            foreach ($asset in $uiAssets)
            {
                $entries["$prefix/wwwroot/$asset"] = if ($asset.EndsWith('.css')) { '/* Release fixture */ .fixture { display: block; }' } else { '// Release fixture' }
            }
        }
        foreach ($name in $entries.Keys)
        {
            $writer = [System.IO.StreamWriter]::new($archive.CreateEntry($name).Open())
            try { $writer.Write($entries[$name]) }
            finally { $writer.Dispose() }
        }
    }
    finally { $archive.Dispose() }
    $archive = [System.IO.Compression.ZipFile]::OpenRead($packagePath)
    try
    {
        foreach ($framework in @('net11.0', 'net10.0'))
        {
            if ($archive.GetEntry("tools/$framework/any/wwwroot/_content/Microsoft.FluentUI.AspNetCore.Components/js/loading-theme.js"))
            {
                throw "The '$framework' fixture must not depend on the retired theme loader."
            }
            $testState.Passed++
        }
    }
    finally { $archive.Dispose() }
    $expectedHash = (Get-FileHash $packagePath -Algorithm SHA512).Hash.ToLowerInvariant()
    if ((& $packageValidator -CandidateDirectory $candidate -Version $version -Commit $commit -ExpectedSha512 $expectedHash) -cne $expectedHash)
    {
        throw 'Valid package without the retired theme loader did not return its exact hash.'
    }
    $testState.Passed++
    Assert-Fails { & $packageValidator -CandidateDirectory $candidate -Version $version -Commit ('b' * 40) } 'repository provenance'
    Assert-Fails { & $packageValidator -CandidateDirectory $candidate -Version $version -Commit $commit -ExpectedSha512 ('0' * 128) } 'qualified SHA-512'
    $backup = Join-Path $root 'original.nupkg'
    Copy-Item -LiteralPath $packagePath -Destination $backup
    Copy-Item -LiteralPath $packagePath -Destination (Join-Path $candidate 'unexpected.nupkg')
    Assert-Fails { & $packageValidator -CandidateDirectory $candidate -Version $version -Commit $commit } 'exactly one package'
    Remove-Item -LiteralPath (Join-Path $candidate 'unexpected.nupkg')
    foreach ($framework in @('net11.0', 'net10.0'))
    {
        foreach ($asset in $uiAssets)
        {
            $missingEntry = "tools/$framework/any/wwwroot/$asset"
            try
            {
                $archive = [System.IO.Compression.ZipFile]::Open($packagePath, [System.IO.Compression.ZipArchiveMode]::Update)
                try { $archive.GetEntry($missingEntry).Delete() }
                finally { $archive.Dispose() }
                Assert-Fails { & $packageValidator -CandidateDirectory $candidate -Version $version -Commit $commit } "Package is missing '$missingEntry'."
            }
            finally { Copy-Item -LiteralPath $backup -Destination $packagePath }
        }
    }
    $archive = [System.IO.Compression.ZipFile]::Open($packagePath, [System.IO.Compression.ZipArchiveMode]::Update)
    try { $archive.GetEntry('tools/net10.0/any/wwwroot/_framework/blazor.web.js').Delete() }
    finally { $archive.Dispose() }
    Assert-Fails { & $packageValidator -CandidateDirectory $candidate -Version $version -Commit $commit -ExpectedSha512 $expectedHash } 'qualified SHA-512'
    Assert-Fails { & $packageValidator -CandidateDirectory $candidate -Version $version -Commit $commit } 'Package is missing'
    Copy-Item -LiteralPath $backup -Destination $packagePath
    $archive = [System.IO.Compression.ZipFile]::Open($packagePath, [System.IO.Compression.ZipArchiveMode]::Update)
    try { $archive.CreateEntry('README.md') | Out-Null }
    finally { $archive.Dispose() }
    Assert-Fails { & $packageValidator -CandidateDirectory $candidate -Version $version -Commit $commit } 'duplicate archive entries'
    "PASS: $($testState.Passed) release identity and immutable package checks."
}
finally
{
    $env:GITHUB_REPOSITORY = $previousRepository
    Remove-Item -LiteralPath $root -Recurse
}
