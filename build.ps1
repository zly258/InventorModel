[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",

    [switch]$Clean,

    [switch]$SkipTests
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root = $PSScriptRoot
$solution = Join-Path $root "InventorModel.sln"
$tests = Join-Path $root "tests\InventorModel.Core.Tests\InventorModel.Core.Tests.csproj"
$bin = Join-Path $root "bin"
$obj = Join-Path $root "obj"
$artifacts = Join-Path $root "artifacts"
$output = Join-Path $bin "x64\$Configuration"

$inventorRoot = if ($env:InventorInstallRoot) {
    $env:InventorInstallRoot
}
else {
    "C:\Program Files\Autodesk\Inventor 2023"
}

$interop = if ($env:InventorInteropPath) {
    $env:InventorInteropPath
}
else {
    Join-Path $inventorRoot "Bin\Public Assemblies\Autodesk.Inventor.Interop.dll"
}

function Invoke-DotNet {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,
        [Parameter(Mandatory = $true)]
        [string]$Step
    )

    Write-Host ""
    Write-Host "[$Step]" -ForegroundColor Cyan
    Write-Host "dotnet $($Arguments -join ' ')" -ForegroundColor DarkGray

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$Step failed with exit code $LASTEXITCODE."
    }
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw ".NET SDK was not found in PATH."
}

if (-not (Test-Path -LiteralPath $solution)) {
    throw "Solution was not found: $solution"
}

if (-not (Test-Path -LiteralPath $interop)) {
    throw @"
Autodesk Inventor Interop assembly was not found:
$interop

Install Autodesk Inventor 2023, or set one of these environment variables:
  InventorInstallRoot
  InventorInteropPath
"@
}

Write-Host "InventorModel build" -ForegroundColor Green
Write-Host "Configuration : $Configuration"
Write-Host "Framework     : .NET Framework 4.8"
Write-Host "Platform      : x64"
Write-Host "Inventor      : $inventorRoot"
Write-Host "Interop       : $interop"

if ($Clean) {
    Write-Host ""
    Write-Host "[clean]" -ForegroundColor Cyan

    foreach ($path in @($bin, $obj, $artifacts)) {
        if (Test-Path -LiteralPath $path) {
            Remove-Item -LiteralPath $path -Recurse -Force
        }
    }
}

$commonProperties = @(
    "-p:Platform=x64",
    "-p:InventorInstallRoot=$inventorRoot",
    "-p:InventorInteropPath=$interop"
)

Invoke-DotNet -Step "restore" -Arguments @(
    "restore",
    $solution
)

$buildArguments = @(
    "build",
    $solution,
    "-c", $Configuration,
    "--no-restore"
) + $commonProperties

Invoke-DotNet -Step "build" -Arguments $buildArguments

# Remove stale outputs from the former Addin / CLI products when this script
# is run without -Clean after upgrading an existing working copy.
foreach ($pattern in @(
    "InventorModel.Addin*",
    "InventorModel.Cli*"
)) {
    Get-ChildItem -Path $output -Filter $pattern -ErrorAction SilentlyContinue |
        Remove-Item -Force -Recurse -ErrorAction SilentlyContinue
}

$skillsSource = Join-Path $root "Skills"
$skillsOutput = Join-Path $output "Skills"
if (Test-Path -LiteralPath $skillsSource) {
    if (Test-Path -LiteralPath $skillsOutput) {
        Remove-Item -LiteralPath $skillsOutput -Recurse -Force
    }

    New-Item -ItemType Directory -Path $skillsOutput -Force | Out-Null
    Copy-Item -Path (Join-Path $skillsSource "*") -Destination $skillsOutput -Recurse -Force
}

if (-not $SkipTests) {
    $testArguments = @(
        "test",
        $tests,
        "-c", $Configuration,
        "--no-build",
        "--no-restore"
    ) + $commonProperties

    Invoke-DotNet -Step "test" -Arguments $testArguments
}

Write-Host ""
Write-Host "Build completed successfully." -ForegroundColor Green
Write-Host "MCP     : $(Join-Path $output 'InventorModel.Mcp.exe')"
Write-Host "Skills  : $(Join-Path $output 'Skills')"
Write-Host "Output  : $output"
