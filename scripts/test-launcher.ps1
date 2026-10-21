param([string]$ReleaseDirectory)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
Set-Location $root
$env:DOTNET_CLI_HOME=Join-Path $root 'runtime/dotnet-home'
$env:NUGET_PACKAGES=Join-Path $root 'runtime/nuget-packages'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$testArgs=@((Join-Path $root 'runtime/launcher-tests'))
if($ReleaseDirectory){$testArgs += (Resolve-Path -LiteralPath $ReleaseDirectory).Path}
& dotnet run --project source/launcher/TankLauncher.Tests/TankLauncher.Tests.csproj --configuration Release -- @testArgs
if($LASTEXITCODE -ne 0){throw 'Launcher regression tests failed'}
