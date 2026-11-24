# <copyright file="Test-ToolConsumer.ps1" company="Microsoft Corporation">
#     Copyright (c) Microsoft Corporation. All rights reserved.
#     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
# </copyright>

param(
    [Parameter(Mandatory)][string] $CandidateDirectory,
    [Parameter(Mandatory)][string] $Version,
    [Parameter(Mandatory)][ValidateSet('net11.0', 'net10.0')][string] $Framework,
    [Parameter(Mandatory)][string] $WorkingDirectory
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$CandidateDirectory = (Resolve-Path -LiteralPath $CandidateDirectory).Path
$WorkingDirectory = [System.IO.Path]::GetFullPath($WorkingDirectory)
if (Test-Path -LiteralPath $WorkingDirectory)
{
    throw "Consumer working directory '$WorkingDirectory' must not already exist."
}
New-Item -ItemType Directory -Path $WorkingDirectory | Out-Null
$toolPath = Join-Path $WorkingDirectory 'tool'
$sourcePath = Join-Path $WorkingDirectory 'source'
$cachePath = Join-Path $WorkingDirectory 'fhir-cache'
New-Item -ItemType Directory -Path $sourcePath, $cachePath | Out-Null
[xml] $configuration = '<configuration><packageSources><clear /><add key="candidate" value="" /></packageSources></configuration>'
$configuration.configuration.packageSources.add.SetAttribute('value', $CandidateDirectory)
$configurationPath = Join-Path $WorkingDirectory 'NuGet.Config'
$configuration.Save($configurationPath)
$previousPackages = $env:NUGET_PACKAGES
$env:NUGET_PACKAGES = Join-Path $WorkingDirectory 'packages'
$process = $null
$client = $null

try
{
    dotnet tool install fhir-candle --tool-path $toolPath --version $Version --framework $Framework `
        --configfile $configurationPath --no-cache
    if ($LASTEXITCODE -ne 0) { throw 'Candidate tool installation failed.' }
    $installedRoot = Join-Path (Join-Path (Join-Path (Join-Path (Join-Path $toolPath '.store') 'fhir-candle') $Version) 'fhir-candle') $Version
    $packageName = "fhir-candle.$Version.nupkg"
    $candidateHash = (Get-FileHash (Join-Path $CandidateDirectory $packageName) -Algorithm SHA512).Hash
    $installedHash = (Get-FileHash (Join-Path $installedRoot $packageName) -Algorithm SHA512).Hash
    if ($candidateHash -cne $installedHash)
    {
        throw 'Tool installation did not consume the exact candidate bytes.'
    }
    $payload = Join-Path (Join-Path (Join-Path $installedRoot 'tools') $Framework) 'any'
    if (!(Test-Path -LiteralPath (Join-Path $payload 'fhir-candle.dll')))
    {
        throw "Tool did not install the requested '$Framework' payload."
    }

    $assetManifest = @(
        @{ Path = '/js/candle-theme.js'; MediaTypes = @('text/javascript', 'application/javascript') },
        @{ Path = '/css/candle-theme.css'; MediaTypes = @('text/css') },
        @{ Path = '/css/app.css'; MediaTypes = @('text/css') },
        @{ Path = '/fhir-candle.styles.css'; MediaTypes = @('text/css') },
        @{ Path = '/_content/Microsoft.FluentUI.AspNetCore.Components/css/default-fuib.css'; MediaTypes = @('text/css') },
        @{ Path = '/_content/Microsoft.FluentUI.AspNetCore.Components/Microsoft.FluentUI.AspNetCore.Components.bundle.scp.css'; MediaTypes = @('text/css') },
        @{ Path = '/_content/Microsoft.FluentUI.AspNetCore.Components/Microsoft.FluentUI.AspNetCore.Components.lib.module.js'; MediaTypes = @('text/javascript', 'application/javascript') },
        @{ Path = '/_framework/blazor.web.js'; MediaTypes = @('text/javascript', 'application/javascript') },
        @{ Path = '/_content/BlazorMonaco/jsInterop.js'; MediaTypes = @('text/javascript', 'application/javascript') }
    )
    # Bind HTTP identities to the exact candidate's per-framework archive entries.
    # Include default-fuib.css explicitly: the initializer fetches it dynamically.
    $archive = [System.IO.Compression.ZipFile]::OpenRead((Join-Path $CandidateDirectory $packageName))
    try
    {
        foreach ($asset in $assetManifest)
        {
            $entryName = "tools/$Framework/any/wwwroot$($asset.Path)"
            $entry = $archive.GetEntry($entryName)
            if (!$entry) { throw "Candidate package is missing '$entryName'." }
            $stream = $entry.Open()
            try { $asset.Sha512 = (Get-FileHash -InputStream $stream -Algorithm SHA512).Hash }
            finally { $stream.Dispose() }
        }
    }
    finally { $archive.Dispose() }

    $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
    $listener.Start()
    $port = $listener.LocalEndpoint.Port
    $listener.Stop()
    $baseUrl = "http://127.0.0.1:$port"
    $shim = Join-Path $toolPath $(if ($IsWindows) { 'fhir-candle.exe' } else { 'fhir-candle' })
    $start = [System.Diagnostics.ProcessStartInfo]::new()
    $start.FileName = $shim
    $start.WorkingDirectory = $WorkingDirectory
    $start.UseShellExecute = $false
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start.Environment['ASPNETCORE_ENVIRONMENT'] = 'Production'
    foreach ($argument in @(
        '--port', "$port", '--url', $baseUrl,
        '--fhir-source', $sourcePath, '--fhir-package-cache', $cachePath,
        '--r4', 'r4', '--r4b', 'r4b', '--r5', 'r5'
    ))
    {
        $start.ArgumentList.Add($argument)
    }
    $process = [System.Diagnostics.Process]::Start($start)
    $stdout = $process.StandardOutput.ReadToEndAsync()
    $stderr = $process.StandardError.ReadToEndAsync()
    $handler = [System.Net.Http.HttpClientHandler]::new()
    $handler.UseProxy = $false
    $client = [System.Net.Http.HttpClient]::new($handler)
    $client.Timeout = [TimeSpan]::FromSeconds(5)
    $client.DefaultRequestHeaders.Accept.ParseAdd('application/fhir+json')
    $deadline = [DateTime]::UtcNow.AddSeconds(90)
    $ready = $false
    $lastError = 'No successful metadata response.'
    while ([DateTime]::UtcNow -lt $deadline)
    {
        if ($process.HasExited) { throw "Installed tool exited with code $($process.ExitCode) before becoming ready." }
        try
        {
            $response = $client.GetAsync("$baseUrl/fhir/r4/metadata").GetAwaiter().GetResult()
            $ready = $response.IsSuccessStatusCode
            $lastError = "Metadata returned HTTP $([int] $response.StatusCode)."
            $response.Dispose()
        }
        catch [System.Net.Http.HttpRequestException]
        {
            $lastError = $_.Exception.Message
        }
        if ($ready) { break }
        Start-Sleep -Milliseconds 250
    }
    if (!$ready) { throw "Installed tool did not become ready: $lastError" }

    foreach ($tenant in @(@('r4', '4.0.1'), @('r4b', '4.3.0'), @('r5', '5.0.0')))
    {
        $url = "$baseUrl/fhir/$($tenant[0])"
        $response = $client.GetAsync("$url/metadata").GetAwaiter().GetResult()
        try
        {
            $response.EnsureSuccessStatusCode() | Out-Null
            $metadata = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult() | ConvertFrom-Json
            if ($metadata.resourceType -cne 'CapabilityStatement' -or $metadata.fhirVersion -cne $tenant[1])
            {
                throw "Incorrect FHIR metadata for '$($tenant[0])'."
            }
        }
        finally { $response.Dispose() }
        $content = [System.Net.Http.StringContent]::new(
            '{"resourceType":"Patient","identifier":[{"system":"urn:fhir-candle:release","value":"candidate"}]}',
            [System.Text.Encoding]::UTF8, 'application/fhir+json')
        try { $response = $client.PostAsync("$url/Patient", $content).GetAwaiter().GetResult() }
        finally { $content.Dispose() }
        try
        {
            if ([int] $response.StatusCode -ne 201) { throw "Patient creation failed for '$($tenant[0])': $($response.StatusCode)." }
            $patient = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult() | ConvertFrom-Json
            if ($patient.resourceType -cne 'Patient' -or [string]::IsNullOrEmpty($patient.id))
            {
                throw 'Patient creation did not return a resource with an id.'
            }
        }
        finally { $response.Dispose() }
        $response = $client.GetAsync("$url/Patient?identifier=urn%3Afhir-candle%3Arelease%7Ccandidate").GetAwaiter().GetResult()
        try
        {
            $response.EnsureSuccessStatusCode() | Out-Null
            $bundle = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult() | ConvertFrom-Json
            if ($bundle.resourceType -cne 'Bundle' -or $bundle.total -ne 1 -or $bundle.entry[0].resource.id -cne $patient.id)
            {
                throw "Patient search did not find the created resource for '$($tenant[0])'."
            }
        }
        finally { $response.Dispose() }
        $response = $client.DeleteAsync("$url/Patient/$($patient.id)").GetAwaiter().GetResult()
        try
        {
            if ([int] $response.StatusCode -ne 200) { throw "Patient deletion failed for '$($tenant[0])': $($response.StatusCode)." }
        }
        finally { $response.Dispose() }
        $response = $client.GetAsync("$url/Patient/$($patient.id)").GetAwaiter().GetResult()
        try
        {
            if ([int] $response.StatusCode -ne 404) { throw "Deleted Patient is still readable for '$($tenant[0])'." }
        }
        finally { $response.Dispose() }
    }
    $client.DefaultRequestHeaders.Accept.Clear()
    $response = $client.GetAsync("$baseUrl/").GetAwaiter().GetResult()
    try
    {
        $response.EnsureSuccessStatusCode() | Out-Null
        $contentType = $response.Content.Headers.ContentType
        $body = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        if (!$contentType -or $contentType.MediaType -ine 'text/html' -or [string]::IsNullOrWhiteSpace($body))
        {
            throw 'Installed tool did not serve the expected UI document.'
        }
        foreach ($marker in @(
            'js/candle-theme.js', 'css/candle-theme.css', 'css/app.css',
            'fhir-candle.styles.css', '_framework/blazor.web.js', '_content/BlazorMonaco/jsInterop.js'
        ))
        {
            if (!$body.Contains($marker)) { throw "UI document is missing '$marker'." }
        }
        if ($body -imatch 'loading-theme\.js|<loading-theme\b')
        {
            throw 'UI document still references the retired theme loader.'
        }
    }
    finally { $response.Dispose() }
    foreach ($asset in $assetManifest)
    {
        $response = $client.GetAsync("$baseUrl$($asset.Path)").GetAwaiter().GetResult()
        try
        {
            $response.EnsureSuccessStatusCode() | Out-Null
            $contentType = $response.Content.Headers.ContentType
            if (!$contentType -or $asset.MediaTypes -inotcontains $contentType.MediaType)
            {
                throw "Incorrect content type for UI asset '$($asset.Path)': '$contentType'."
            }
            $bytes = $response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult()
            $body = [System.Text.Encoding]::UTF8.GetString($bytes)
            if ([string]::IsNullOrWhiteSpace($body) -or
                $body -imatch '^\s*(?:\uFEFF\s*)?(?:<!doctype\s+html\b|<html\b)')
            {
                throw "Installed tool served an empty or HTML fallback UI asset '$($asset.Path)'."
            }
            $actualHash = [Convert]::ToHexString([System.Security.Cryptography.SHA512]::HashData($bytes))
            if ($actualHash -cne $asset.Sha512)
            {
                throw "UI asset '$($asset.Path)' does not match the candidate's '$Framework' asset manifest."
            }
        }
        finally { $response.Dispose() }
    }
    "PASS: exact $Version/$Framework tool installation, all FHIR versions, Patient create/search/delete, and UI assets." |
        Tee-Object -FilePath (Join-Path $WorkingDirectory 'qualification.log')
}
finally
{
    if ($client) { $client.Dispose() }
    if ($process)
    {
        if (!$process.HasExited) { Stop-Process -Id $process.Id }
        $process.WaitForExit()
        $stdout.GetAwaiter().GetResult() | Set-Content (Join-Path $WorkingDirectory 'stdout.log')
        $stderr.GetAwaiter().GetResult() | Set-Content (Join-Path $WorkingDirectory 'stderr.log')
        $process.Dispose()
    }
    $env:NUGET_PACKAGES = $previousPackages
}
