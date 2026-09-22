param([ValidateSet("Debug","Release")][string]$Configuration="Debug",[switch]$Clean)
$ErrorActionPreference="Stop"
$root=Split-Path -Parent $MyInvocation.MyCommand.Path
if($Clean -and (Test-Path "$root\bin")){Remove-Item "$root\bin" -Recurse -Force}
dotnet restore "$root\InventorModel.sln"
dotnet build "$root\InventorModel.sln" -c $Configuration -p:Platform=x64 --no-restore
dotnet test "$root\tests\InventorModel.Core.Tests\InventorModel.Core.Tests.csproj" -c $Configuration --no-build
