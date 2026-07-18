param(
    [Parameter(Mandatory = $true)]
    [string]$PackageDirectory,
    [Parameter(Mandatory = $true)]
    [string]$SmartProblemsVersion,
    [Parameter(Mandatory = $true)]
    [string]$SmartSelectorVersion,
    [switch]$KeepWorkDirectory
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw $Message }
}

function Get-PackageVersion {
    param([string]$PackageId)

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    foreach ($file in Get-ChildItem -LiteralPath $PackageDirectory -Filter '*.nupkg' -File) {
        $archive = [System.IO.Compression.ZipFile]::OpenRead($file.FullName)
        try {
            $entry = $archive.Entries | Where-Object { $_.FullName -like '*.nuspec' } | Select-Object -First 1
            if ($null -eq $entry) { continue }
            $reader = [System.IO.StreamReader]::new($entry.Open())
            try { [xml]$nuspec = $reader.ReadToEnd() } finally { $reader.Dispose() }
            if ($nuspec.package.metadata.id -eq $PackageId) { return [string]$nuspec.package.metadata.version }
        }
        finally { $archive.Dispose() }
    }
    throw "Package '$PackageId' was not found."
}

function Write-Utf8File {
    param([string]$Path, [string]$Content)
    $utf8 = [System.Text.UTF8Encoding]::new($false)
    [System.IO.File]::WriteAllText($Path, $Content, $utf8)
}

function Invoke-ConsumerBuild {
    param([string]$Project, [bool]$ExpectSuccess, [string]$ExpectedDiagnostic = '')

    $output = & dotnet build $Project -c Release --nologo --configfile $nugetConfig 2>&1 | Out-String
    $exitCode = $LASTEXITCODE
    Write-Host $output

    foreach ($crashCode in @('CS8032', 'CS8785', 'AD0001')) {
        Assert-True ($output.IndexOf($crashCode, [StringComparison]::Ordinal) -lt 0) `
            "Consumer build emitted $crashCode."
    }

    if ($ExpectSuccess) {
        Assert-True ($exitCode -eq 0) "Consumer '$Project' failed unexpectedly."
    }
    else {
        Assert-True ($exitCode -ne 0) "Invalid consumer '$Project' compiled unexpectedly."
        Assert-True ($output.IndexOf($ExpectedDiagnostic, [StringComparison]::Ordinal) -ge 0) `
            "Invalid consumer did not emit $ExpectedDiagnostic."
    }
}

$packageDirectoryPath = (Resolve-Path -LiteralPath $PackageDirectory).Path
$smartCommandsVersion = Get-PackageVersion 'RoyalCode.SmartCommands'
$workRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("smartcommands-smoke-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $workRoot | Out-Null

try {
    $escapedFeed = [System.Security.SecurityElement]::Escape($packageDirectoryPath)
    $nugetConfig = Join-Path $workRoot 'NuGet.Config'
    Write-Utf8File $nugetConfig @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="SmartCommandsLocal" value="$escapedFeed" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
"@

    foreach ($tfm in @('net8.0', 'net9.0', 'net10.0')) {
        $directory = Join-Path $workRoot "valid-$tfm"
        New-Item -ItemType Directory -Path $directory | Out-Null
        $project = Join-Path $directory 'ValidConsumer.csproj'
        Write-Utf8File $project @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><TargetFramework>$tfm</TargetFramework><Nullable>enable</Nullable></PropertyGroup>
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
    <PackageReference Include="RoyalCode.SmartCommands" Version="$smartCommandsVersion" />
    <PackageReference Include="RoyalCode.SmartCommands.Generators" Version="$smartCommandsVersion" PrivateAssets="all" />
    <PackageReference Include="RoyalCode.SmartProblems.ApiResults" Version="$SmartProblemsVersion" />
  </ItemGroup>
</Project>
"@
        Write-Utf8File (Join-Path $directory 'Command.cs') @'
using RoyalCode.SmartCommands;
using RoyalCode.SmartProblems;

namespace DistributionSmoke;

[MapGroup("things")]
[MapPost("/", "create-thing")]
public class CreateThing
{
    [Command]
    public Result Execute() => Result.Ok();
}

[MapApiHandlers]
public static partial class Endpoints { }

public static class GeneratedContractCheck
{
    public static System.Type HandlerType => typeof(ICreateThingHandler);
}
'@
        Invoke-ConsumerBuild $project $true
        Write-Host "Consumer $tfm ........... passed"
    }

    $invalidDirectory = Join-Path $workRoot 'invalid'
    New-Item -ItemType Directory -Path $invalidDirectory | Out-Null
    $invalidProject = Join-Path $invalidDirectory 'InvalidConsumer.csproj'
    Write-Utf8File $invalidProject @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><TargetFramework>net10.0</TargetFramework><Nullable>enable</Nullable></PropertyGroup>
  <ItemGroup>
    <PackageReference Include="RoyalCode.SmartCommands" Version="$smartCommandsVersion" />
    <PackageReference Include="RoyalCode.SmartCommands.Generators" Version="$smartCommandsVersion" PrivateAssets="all" />
  </ItemGroup>
</Project>
"@
    Write-Utf8File (Join-Path $invalidDirectory 'InvalidCommand.cs') @'
using RoyalCode.SmartCommands;
using RoyalCode.SmartProblems;

public class InvalidCommand
{
    [Command] public Result First() => Result.Ok();
    [Command] public Result Second() => Result.Ok();
}
'@
    Invoke-ConsumerBuild $invalidProject $false 'RCCMD026'
    Write-Host 'Invalid consumer RCCMD026 . passed'

    $combinedDirectory = Join-Path $workRoot 'combined'
    New-Item -ItemType Directory -Path $combinedDirectory | Out-Null
    $combinedProject = Join-Path $combinedDirectory 'CombinedConsumer.csproj'
    Write-Utf8File $combinedProject @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework><Nullable>enable</Nullable>
    <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
    <CompilerGeneratedFilesOutputPath>Generated</CompilerGeneratedFilesOutputPath>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="RoyalCode.SmartCommands" Version="$smartCommandsVersion" />
    <PackageReference Include="RoyalCode.SmartCommands.Generators" Version="$smartCommandsVersion" PrivateAssets="all" />
    <PackageReference Include="RoyalCode.SmartSelector" Version="$SmartSelectorVersion" />
    <PackageReference Include="RoyalCode.SmartSelector.Generators" Version="$SmartSelectorVersion" PrivateAssets="all" />
  </ItemGroup>
</Project>
"@
    Write-Utf8File (Join-Path $combinedDirectory 'Combined.cs') @'
using RoyalCode.SmartCommands;
using RoyalCode.SmartProblems;
using RoyalCode.SmartSelector;

namespace DistributionSmoke;

public class Product
{
    public int Id { get; set; }
    public string? Name { get; set; }
}

[AutoSelect<Product>, AutoProperties]
public partial class ProductDetails { }

public class CombinedCommand
{
    [Command]
    public Result Execute() => Result.Ok();
}

public static class GeneratedContractCheck
{
    public static System.Type HandlerType => typeof(ICombinedCommandHandler);
}
'@
    Invoke-ConsumerBuild $combinedProject $true
    $generated = @(Get-ChildItem -LiteralPath (Join-Path $combinedDirectory 'Generated') -Recurse -Filter '*.cs' -File)
    Assert-True ($generated.Count -gt 0) 'The combined consumer produced no generated files.'
    $generatedText = ($generated | Get-Content -Raw) -join "`n"
    Assert-True ($generatedText.IndexOf('ICombinedCommandHandler', [StringComparison]::Ordinal) -ge 0) `
        'SmartCommands did not generate the combined consumer handler.'
    Assert-True ($generatedText.IndexOf('ProductDetails', [StringComparison]::Ordinal) -ge 0) `
        'SmartSelector did not generate the combined consumer projection.'
    Write-Host 'SmartCommands+Selector .... passed'
}
finally {
    if ($KeepWorkDirectory) {
        Write-Host "Consumer work directory retained at '$workRoot'."
    }
    elseif (Test-Path -LiteralPath $workRoot) {
        Remove-Item -LiteralPath $workRoot -Recurse -Force
    }
}
