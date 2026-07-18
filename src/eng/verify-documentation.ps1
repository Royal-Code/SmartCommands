param([string]$RootDirectory = '')

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ([string]::IsNullOrWhiteSpace($RootDirectory)) {
    $RootDirectory = Split-Path -Parent $PSScriptRoot
}
$root = (Resolve-Path -LiteralPath $RootDirectory).Path
$files = @(
    Get-Item -LiteralPath (Join-Path $root 'README.md')
    Get-ChildItem -LiteralPath (Join-Path $root '.docs') -Recurse -Filter '*.md' -File
)
$broken = [System.Collections.Generic.List[string]]::new()

foreach ($file in $files) {
    $content = Get-Content -LiteralPath $file.FullName -Raw
    foreach ($match in [regex]::Matches($content, '!?(?:\[[^\]]*\])\(([^)]+)\)')) {
        $target = $match.Groups[1].Value.Trim()
        if ($target -match '^(?:https?://|mailto:|#)' -or $target.Contains('<') -or $target.Contains('>')) {
            continue
        }

        $pathPart = ($target -split '#', 2)[0]
        if ([string]::IsNullOrWhiteSpace($pathPart)) { continue }
        $decoded = [Uri]::UnescapeDataString($pathPart)
        $resolved = [System.IO.Path]::GetFullPath((Join-Path $file.DirectoryName $decoded))
        if (-not (Test-Path -LiteralPath $resolved)) {
            $broken.Add("$($file.FullName) -> $target")
        }
    }
}

if ($broken.Count -gt 0) {
    throw "Broken local Markdown links:`n$($broken -join "`n")"
}

Write-Host "Documentation links ...... passed ($($files.Count) files)"
