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
$mcpProject = Join-Path $root "src\InventorModel.Mcp\InventorModel.Mcp.csproj"
$tests = Join-Path $root "tests\InventorModel.Core.Tests\InventorModel.Core.Tests.csproj"
$bin = Join-Path $root "bin"
$obj = Join-Path $root "obj"
$artifacts = Join-Path $root "artifacts"
$output = Join-Path $bin "x64\$Configuration"
$publishStaging = Join-Path $artifacts "publish\win-x64\$Configuration"

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

if (-not (Test-Path -LiteralPath $mcpProject)) {
    throw "MCP project was not found: $mcpProject"
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
Write-Host "Framework     : .NET 8 (self-contained publish)"
Write-Host "Runtime       : win-x64"
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

Invoke-DotNet -Step "build" -Arguments (@(
    "build",
    $solution,
    "-c", $Configuration,
    "--no-restore"
) + $commonProperties)

if (-not $SkipTests) {
    Invoke-DotNet -Step "test" -Arguments (@(
        "test",
        $tests,
        "-c", $Configuration,
        "--no-build",
        "--no-restore"
    ) + $commonProperties)
}

if (Test-Path -LiteralPath $publishStaging) {
    Remove-Item -LiteralPath $publishStaging -Recurse -Force
}
New-Item -ItemType Directory -Path $publishStaging -Force | Out-Null

Invoke-DotNet -Step "publish" -Arguments (@(
    "publish",
    $mcpProject,
    "-c", $Configuration,
    "-r", "win-x64",
    "--self-contained", "true",
    "--no-restore",
    "-o", $publishStaging,
    "-p:PublishSingleFile=true",
    "-p:PublishTrimmed=false",
    "-p:DebugType=None",
    "-p:DebugSymbols=false"
) + $commonProperties)

$publishedExe = Join-Path $publishStaging "InventorModel.exe"
if (-not (Test-Path -LiteralPath $publishedExe)) {
    throw "Single-file publish did not produce InventorModel.exe."
}

if (Test-Path -LiteralPath $output) {
    Remove-Item -LiteralPath $output -Recurse -Force
}
New-Item -ItemType Directory -Path $output -Force | Out-Null

Copy-Item -LiteralPath $publishedExe -Destination (Join-Path $output "InventorModel.exe") -Force

$mcpManifestSource = Join-Path $root "mcp.manifest.json"
$mcpManifestTarget = Join-Path $output "mcp.manifest.json"
if (-not (Test-Path -LiteralPath $mcpManifestSource)) {
    throw "MCP package manifest was not found: $mcpManifestSource"
}
Copy-Item -LiteralPath $mcpManifestSource -Destination $mcpManifestTarget -Force

$skillsSource = Join-Path $root "Skills"
$skillsOutput = Join-Path $output "Skills"
if (Test-Path -LiteralPath $skillsSource) {
    New-Item -ItemType Directory -Path $skillsOutput -Force | Out-Null
    Copy-Item -Path (Join-Path $skillsSource "*") -Destination $skillsOutput -Recurse -Force
}

$skillManifest = Join-Path $skillsOutput "inventor-model\skill.manifest.json"
if (-not (Test-Path -LiteralPath $skillManifest)) {
    throw "InventorModel Skill manifest was not packaged: $skillManifest"
}

$topLevelNames = @(Get-ChildItem -LiteralPath $output -File | ForEach-Object { $_.Name } | Sort-Object)
$expectedTopLevelNames = @("InventorModel.exe", "mcp.manifest.json") | Sort-Object
if (($topLevelNames -join "|") -ne ($expectedTopLevelNames -join "|")) {
    throw "Final output must contain InventorModel.exe and mcp.manifest.json as the only top-level files."
}

Remove-Item -LiteralPath $publishStaging -Recurse -Force

Write-Host ""
Write-Host "Build completed successfully." -ForegroundColor Green
Write-Host "Executable : $(Join-Path $output 'InventorModel.exe')"
Write-Host "Manifest   : $mcpManifestTarget"
Write-Host "Skills     : $skillsOutput"
Write-Host "Runtime    : bundled into InventorModel.exe"
Write-Host "Output     : $output"
