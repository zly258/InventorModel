[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",

    [switch]$Clean,

    [switch]$SkipTests,

    [switch]$RunInventorTests
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root = $PSScriptRoot
$solution = Join-Path $root "InventorModel.sln"
$coreTests = Join-Path $root "tests\InventorModel.Core.Tests\InventorModel.Core.Tests.csproj"
$inventorTests = Join-Path $root "tests\InventorModel.Inventor.Tests\InventorModel.Inventor.Tests.csproj"
$bin = Join-Path $root "bin"
$obj = Join-Path $root "obj"
$artifacts = Join-Path $root "artifacts"

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

$inventorExe = Join-Path $inventorRoot "Bin\Inventor.exe"

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

Install Autodesk Inventor 2023, or set:
  InventorInstallRoot
  InventorInteropPath
"@
}

Write-Host "InventorModel build" -ForegroundColor Green
Write-Host "Configuration : $Configuration"
Write-Host "Inventor      : $inventorRoot"

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

Invoke-DotNet -Step "build" -Arguments (@(
    "build",
    $solution,
    "-c", $Configuration,
    "--no-restore"
) + $commonProperties)

if (-not $SkipTests) {
    Invoke-DotNet -Step "test" -Arguments (@(
        "test",
        $coreTests,
        "-c", $Configuration,
        "--no-build",
        "--no-restore"
    ) + $commonProperties)

    if ($RunInventorTests) {
        if (-not (Test-Path -LiteralPath $inventorExe)) {
            throw "Autodesk Inventor executable was not found: $inventorExe"
        }

        $previousInventorExe = $env:INVENTORMODEL_INVENTOR_EXE
        $env:INVENTORMODEL_INVENTOR_EXE = $inventorExe

        try {
            Invoke-DotNet -Step "inventor-test" -Arguments (@(
                "test",
                $inventorTests,
                "-c", $Configuration,
                "--no-build",
                "--no-restore"
            ) + $commonProperties)
        }
        finally {
            $env:INVENTORMODEL_INVENTOR_EXE = $previousInventorExe
        }
    }
}

Write-Host ""
Write-Host "Build completed successfully." -ForegroundColor Green
Write-Host "Output : $(Join-Path $bin "x64\$Configuration")"
