param(
    [string]$Configuration = 'Release',
    [string]$ArtifactsDirectory = '',
    [switch]$KeepConsumerWorkDirectory
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($ArtifactsDirectory)) {
    $ArtifactsDirectory = Join-Path $root 'artifacts/distribution-validation'
}
$packages = Join-Path $ArtifactsDirectory 'packages'

function Invoke-DotNet {
    param([string[]]$Arguments)

    Write-Host "dotnet $($Arguments -join ' ')"
    $output = & dotnet @Arguments 2>&1 | Out-String
    $exitCode = $LASTEXITCODE
    Write-Host $output
    if ($exitCode -ne 0) { throw "dotnet exited with code $exitCode." }

    $warningCodes = @([regex]::Matches($output, 'warning\s+([A-Z]+\d+)', 'IgnoreCase') |
        ForEach-Object { $_.Groups[1].Value.ToUpperInvariant() } | Sort-Object -Unique)
    $unexpectedWarnings = @($warningCodes | Where-Object { $_ -ne 'NU5104' })
    if ($unexpectedWarnings.Count -gt 0) {
        throw "Unexpected warning codes: $($unexpectedWarnings -join ', ')."
    }
}

$resolvedRoot = [System.IO.Path]::GetFullPath($root).TrimEnd('\', '/')
$resolvedArtifacts = [System.IO.Path]::GetFullPath($ArtifactsDirectory).TrimEnd('\', '/')
if (-not $resolvedArtifacts.StartsWith($resolvedRoot + [System.IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Artifacts directory must be inside '$resolvedRoot'."
}
if (Test-Path -LiteralPath $resolvedArtifacts) {
    Remove-Item -LiteralPath $resolvedArtifacts -Recurse -Force
}
New-Item -ItemType Directory -Path $packages -Force | Out-Null

[xml]$props = Get-Content -LiteralPath (Join-Path $root 'Directory.Build.props') -Raw
$versionProperties = $props.Project.PropertyGroup |
    Where-Object { $null -ne $_.PSObject.Properties['SmartProblemsVer'] } |
    Select-Object -First 1
$smartProblemsVersion = [string]$versionProperties.SmartProblemsVer
$smartSelectorVersion = [string]$versionProperties.SmartSelectVer

Push-Location $root
try {
    Invoke-DotNet @('restore', 'SmartCommands.sln', '--nologo')
    foreach ($project in @(
        'RoyalCode.SmartCommands/RoyalCode.SmartCommands.csproj',
        'RoyalCode.SmartCommands.EntityFramework/RoyalCode.SmartCommands.EntityFramework.csproj',
        'RoyalCode.SmartCommands.WorkContext/RoyalCode.SmartCommands.WorkContext.csproj',
        'RoyalCode.SmartCommands.Generators/RoyalCode.SmartCommands.Generators.csproj'
    )) {
        Invoke-DotNet @('pack', $project, '-c', $Configuration, '--no-restore', '--output', $packages, '--nologo')
    }

    & (Join-Path $PSScriptRoot 'verify-package-layout.ps1') -PackageDirectory $packages
    if ($LASTEXITCODE -ne 0) { throw 'Package layout verification failed.' }

    & (Join-Path $PSScriptRoot 'consumer-smoke.ps1') `
        -PackageDirectory $packages `
        -SmartProblemsVersion $smartProblemsVersion `
        -SmartSelectorVersion $smartSelectorVersion `
        -KeepWorkDirectory:$KeepConsumerWorkDirectory
    if ($LASTEXITCODE -ne 0) { throw 'Consumer smoke failed.' }

    & (Join-Path $PSScriptRoot 'verify-documentation.ps1') -RootDirectory $root

    Write-Host 'Warnings allowlist ........ NU5104 only'
    Write-Host 'Distribution verification . passed'
}
finally {
    Pop-Location
}
