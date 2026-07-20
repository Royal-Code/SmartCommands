param(
    [Parameter(Mandatory = $true)]
    [string]$PackageDirectory
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

Add-Type -AssemblyName System.IO.Compression.FileSystem

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw $Message }
}

function Read-Package {
    param([string]$PackageId)

    foreach ($file in Get-ChildItem -LiteralPath $PackageDirectory -Filter '*.nupkg' -File) {
        $archive = [System.IO.Compression.ZipFile]::OpenRead($file.FullName)
        try {
            $nuspecEntry = $archive.Entries | Where-Object { $_.FullName -like '*.nuspec' } | Select-Object -First 1
            if ($null -eq $nuspecEntry) { continue }

            $reader = [System.IO.StreamReader]::new($nuspecEntry.Open())
            try { [xml]$nuspec = $reader.ReadToEnd() } finally { $reader.Dispose() }

            if ($nuspec.package.metadata.id -ne $PackageId) { continue }

            return [pscustomobject]@{
                Id = $PackageId
                File = $file.FullName
                Metadata = $nuspec.package.metadata
                Entries = @($archive.Entries | ForEach-Object { $_.FullName.Replace('\', '/') })
            }
        }
        finally {
            $archive.Dispose()
        }
    }

    throw "Package '$PackageId' was not found in '$PackageDirectory'."
}

function Assert-Entry {
    param($Package, [string]$Entry)
    Assert-True ($Package.Entries -contains $Entry) "Package '$($Package.Id)' is missing '$Entry'."
}

function Assert-Metadata {
    param($Package)

    $metadata = $Package.Metadata
    Assert-True ($metadata.repository.url -eq 'https://github.com/Royal-Code/SmartCommands') `
        "Package '$($Package.Id)' has an invalid repository URL."
    Assert-True ($metadata.repository.type -eq 'Git') "Package '$($Package.Id)' has an invalid repository type."
    Assert-True ($metadata.license.type -eq 'expression' -and $metadata.license.'#text' -eq 'AGPL-3.0-only') `
        "Package '$($Package.Id)' has an invalid license expression."
    Assert-True ($metadata.readme -eq 'README.md') "Package '$($Package.Id)' does not declare README.md."
    Assert-True ($metadata.icon -eq 'icon2.png') "Package '$($Package.Id)' does not declare icon2.png."
    Assert-True (-not [string]::IsNullOrWhiteSpace([string]$metadata.description)) `
        "Package '$($Package.Id)' has no description."
    Assert-True (-not [string]::IsNullOrWhiteSpace([string]$metadata.version)) `
        "Package '$($Package.Id)' has no version."
    Assert-Entry $Package 'README.md'
    Assert-Entry $Package 'icon2.png'
}

function Assert-Dependency {
    param($Package, [string]$DependencyId)

    $dependencies = @($Package.Metadata.SelectNodes(".//*[local-name()='dependency']"))
    $dependencyIds = @($dependencies | ForEach-Object { $_.GetAttribute('id') })
    Assert-True ($dependencyIds -contains $DependencyId) `
        "Package '$($Package.Id)' is missing dependency '$DependencyId'."
}

function Assert-RuntimePackage {
    param($Package, [string[]]$Dependencies)

    Assert-Metadata $Package
    foreach ($tfm in @('net8.0', 'net9.0', 'net10.0')) {
        Assert-Entry $Package "lib/$tfm/$($Package.Id).dll"
        Assert-Entry $Package "lib/$tfm/$($Package.Id).xml"
    }
    foreach ($dependency in $Dependencies) { Assert-Dependency $Package $dependency }
}

$core = Read-Package 'RoyalCode.SmartCommands'
$entityFramework = Read-Package 'RoyalCode.SmartCommands.EntityFramework'
$workContext = Read-Package 'RoyalCode.SmartCommands.WorkContext'
$generator = Read-Package 'RoyalCode.SmartCommands.Generators'

Assert-RuntimePackage $core @(
    'RoyalCode.SmartProblems',
    'RoyalCode.SmartProblems.ApiResults',
    'RoyalCode.SmartValidations'
)
Assert-RuntimePackage $entityFramework @('RoyalCode.SmartCommands', 'Microsoft.EntityFrameworkCore')
Assert-RuntimePackage $workContext @('RoyalCode.SmartCommands', 'RoyalCode.WorkContext.EntityFramework')

Assert-Metadata $generator
Assert-Entry $generator 'analyzers/dotnet/cs/RoyalCode.SmartCommands.Generators.dll'
Assert-Entry $generator 'analyzers/dotnet/cs/RoyalCode.Extensions.SourceGenerator.dll'
Assert-Entry $generator 'build/RoyalCode.SmartCommands.Generators.props'
Assert-True (-not ($generator.Entries | Where-Object { $_ -like 'lib/*/RoyalCode.SmartCommands.Generators.dll' })) `
    'The generator assembly must not be exposed as a runtime library.'

$analyzerAssemblies = @($generator.Entries | Where-Object { $_ -like 'analyzers/dotnet/cs/*.dll' })
$expectedAnalyzerAssemblies = @(
    'analyzers/dotnet/cs/RoyalCode.Extensions.SourceGenerator.dll',
    'analyzers/dotnet/cs/RoyalCode.SmartCommands.Generators.dll'
)
Assert-True (@(Compare-Object $expectedAnalyzerAssemblies $analyzerAssemblies).Count -eq 0) `
    "Unexpected analyzer assemblies: $($analyzerAssemblies -join ', ')."

Write-Host 'Package metadata .......... passed'
Write-Host 'Generator package layout .. passed'
Write-Host 'Runtime package layouts ... passed'
