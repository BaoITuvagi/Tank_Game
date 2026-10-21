param(
    [string]$ManifestUrl='https://github.com/BaoITuvagi/Tank_Game/releases/latest/download/manifest.json',
    [switch]$FrameworkDependent,
    [switch]$AllowLoopbackHttp
)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
Set-Location $root
$uri=[uri]$ManifestUrl
if(!$uri.IsAbsoluteUri -or ($uri.Scheme -ne 'https' -and !($AllowLoopbackHttp -and $uri.Scheme -eq 'http' -and $uri.IsLoopback))){throw 'ManifestUrl must be HTTPS; use -AllowLoopbackHttp only for localhost testing.'}
$env:DOTNET_CLI_HOME=Join-Path $root 'runtime/dotnet-home'
$env:NUGET_PACKAGES=Join-Path $root 'runtime/nuget-packages'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
# Use separate output directories so framework-dependent files cannot contaminate a standalone release.
$flavor=if($FrameworkDependent){'launcher-framework-dependent'}else{'launcher'}
$output=Join-Path $root "build/$flavor"
$selfContained=if($FrameworkDependent){'false'}else{'true'}
& dotnet publish source/launcher/TankLauncher/TankLauncher.csproj -c Release -r win-x64 --self-contained $selfContained -o $output -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false
if($LASTEXITCODE -ne 0){throw 'Launcher publish failed. A standalone build needs network access on its first build to restore .NET runtime packs.'}
$config=[ordered]@{manifestUrl=$ManifestUrl;allowLoopbackHttp=[bool]$AllowLoopbackHttp}
$json=$config | ConvertTo-Json
[IO.File]::WriteAllText((Join-Path $output 'launcher.json'),$json,(New-Object Text.UTF8Encoding($false)))
$bundle=Join-Path $root "build/$flavor-win64.zip"
Compress-Archive -LiteralPath (Join-Path $output 'Launcher.exe'),(Join-Path $output 'launcher.json') -DestinationPath $bundle -Force
Write-Output "Built $output/Launcher.exe. Distribute the entire folder, including launcher.json."
Write-Output "Player download: $bundle"
if($FrameworkDependent){Write-Warning 'This development build requires the .NET 9 Desktop Runtime on the player machine. Use the default standalone build for distribution.'}
