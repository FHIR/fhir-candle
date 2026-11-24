# <copyright file="Test-ReleasePackage.ps1" company="Microsoft Corporation">
#     Copyright (c) Microsoft Corporation. All rights reserved.
#     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
# </copyright>

param(
    [Parameter(Mandatory)][string] $CandidateDirectory,
    [Parameter(Mandatory)][string] $Version,
    [Parameter(Mandatory)][ValidatePattern('^[0-9a-f]{40}$')][string] $Commit,
    [string] $ExpectedSha512
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$packageName = "fhir-candle.$Version.nupkg"
$packagePath = Join-Path $CandidateDirectory $packageName
$packages = @(Get-ChildItem -LiteralPath $CandidateDirectory -File -Filter '*.nupkg')
if ($packages.Count -ne 1 -or $packages[0].Name -cne $packageName)
{
    throw "Expected exactly one package: '$packageName'."
}
$hash = (Get-FileHash -LiteralPath $packagePath -Algorithm SHA512).Hash.ToLowerInvariant()
if ($ExpectedSha512 -and $hash -cne $ExpectedSha512)
{
    throw 'Candidate bytes do not match the qualified SHA-512.'
}

$archive = [System.IO.Compression.ZipFile]::OpenRead($packagePath)
try
{
    $names = @($archive.Entries.FullName)
    if (@($names | Group-Object -CaseSensitive | Where-Object Count -gt 1).Count -ne 0)
    {
        throw 'Package contains duplicate archive entries.'
    }
    foreach ($required in @('fhir-candle.nuspec', 'README.md', 'LICENSE'))
    {
        if ($names -cnotcontains $required) { throw "Package is missing '$required'." }
    }
    $reader = [System.IO.StreamReader]::new($archive.GetEntry('fhir-candle.nuspec').Open())
    try { [xml] $nuspec = $reader.ReadToEnd() }
    finally { $reader.Dispose() }
    $metadata = $nuspec.SelectSingleNode("/*[local-name()='package']/*[local-name()='metadata']")
    if ($metadata.id -cne 'fhir-candle' -or $metadata.version -cne $Version)
    {
        throw 'Nuspec package identity does not match the release.'
    }
    if ($metadata.repository.commit -cne $Commit -or
        $metadata.repository.url -ine 'https://github.com/FHIR/fhir-candle')
    {
        throw 'Nuspec repository provenance does not match the release.'
    }
    if ($metadata.packageTypes.packageType.name -cne 'DotnetTool')
    {
        throw 'Release package must be a DotnetTool.'
    }
    $frameworks = @($names | Where-Object { $_ -cmatch '^tools/([^/]+)/' } |
        ForEach-Object { $_.Split('/')[1] } | Sort-Object -Unique)
    if (($frameworks -join ',') -cne 'net10.0,net11.0')
    {
        throw 'Tool package must contain exactly net10.0 and net11.0 payloads.'
    }
    foreach ($framework in $frameworks)
    {
        $prefix = "tools/$framework/any"
        foreach ($required in @(
            "$prefix/fhir-candle.dll",
            "$prefix/fhir-candle.deps.json",
            "$prefix/fhir-candle.runtimeconfig.json",
            "$prefix/DotnetToolSettings.xml",
            "$prefix/wwwroot/js/candle-theme.js",
            "$prefix/wwwroot/css/candle-theme.css",
            "$prefix/wwwroot/css/app.css",
            "$prefix/wwwroot/fhir-candle.styles.css",
            "$prefix/wwwroot/_content/Microsoft.FluentUI.AspNetCore.Components/css/default-fuib.css",
            "$prefix/wwwroot/_content/Microsoft.FluentUI.AspNetCore.Components/Microsoft.FluentUI.AspNetCore.Components.bundle.scp.css",
            "$prefix/wwwroot/_content/Microsoft.FluentUI.AspNetCore.Components/Microsoft.FluentUI.AspNetCore.Components.lib.module.js",
            "$prefix/wwwroot/_framework/blazor.web.js",
            "$prefix/wwwroot/_content/BlazorMonaco/jsInterop.js"
        ))
        {
            if ($names -cnotcontains $required) { throw "Package is missing '$required'." }
        }
        $reader = [System.IO.StreamReader]::new($archive.GetEntry("$prefix/DotnetToolSettings.xml").Open())
        try { [xml] $settings = $reader.ReadToEnd() }
        finally { $reader.Dispose() }
        $command = $settings.DotNetCliTool.Commands.Command
        if ($command.Name -cne 'fhir-candle' -or $command.EntryPoint -cne 'fhir-candle.dll' -or $command.Runner -cne 'dotnet')
        {
            throw "Tool command metadata is incorrect for '$framework'."
        }
        $reader = [System.IO.StreamReader]::new($archive.GetEntry("$prefix/fhir-candle.runtimeconfig.json").Open())
        try { $runtime = $reader.ReadToEnd() | ConvertFrom-Json }
        finally { $reader.Dispose() }
        if ($runtime.runtimeOptions.tfm -cne $framework)
        {
            throw "Runtime configuration does not match '$framework'."
        }
        $assemblyPath = [System.IO.Path]::GetTempFileName()
        try
        {
            [System.IO.Compression.ZipFileExtensions]::ExtractToFile(
                $archive.GetEntry("$prefix/fhir-candle.dll"), $assemblyPath, $true)
            $assembly = [System.Reflection.AssemblyName]::GetAssemblyName($assemblyPath)
            $fileVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($assemblyPath).FileVersion
            if ($assembly.Name -cne 'fhir-candle' -or
                $assembly.Version.ToString() -cne "0.$Version" -or $fileVersion -cne "0.$Version")
            {
                throw "Assembly and file versions must match the release for '$framework'."
            }
        }
        finally
        {
            Remove-Item -LiteralPath $assemblyPath
        }
    }
}
finally
{
    $archive.Dispose()
}

$hash
