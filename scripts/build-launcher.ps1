param(
    [string]$ManifestUrl='https://github.com/BaoITuvagi/Tank_Game/releases/latest/download/manifest.json',
    [string]$LauncherManifestUrl='',
    [string]$ReleaseBaseUrl='https://github.com/BaoITuvagi/Tank_Game/releases/latest/download',
    [string]$LauncherVersion='',
    [switch]$FrameworkDependent,
    [switch]$AllowLoopbackHttp
)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
Set-Location $root
function Assert-UpdateUrl([string]$value) {
    $uri=[uri]$value
    if(!$uri.IsAbsoluteUri -or $uri.UserInfo -or ($uri.Scheme -ne 'https' -and !($AllowLoopbackHttp -and $uri.Scheme -eq 'http' -and $uri.IsLoopback))){throw 'Update URLs must be HTTPS; use -AllowLoopbackHttp only for localhost testing.'}
}
Assert-UpdateUrl $ManifestUrl
if(!$LauncherManifestUrl){$LauncherManifestUrl=([uri]::new([uri]$ManifestUrl,'launcher-manifest.json')).AbsoluteUri}
Assert-UpdateUrl $LauncherManifestUrl
Assert-UpdateUrl $ReleaseBaseUrl
$releaseUri=[uri]$ReleaseBaseUrl
if($releaseUri.Query -or $releaseUri.Fragment){throw 'ReleaseBaseUrl must be a directory URL without query or fragment.'}
if(!$LauncherVersion){
    [xml]$project=Get-Content -LiteralPath 'source/launcher/TankLauncher/TankLauncher.csproj'
    $LauncherVersion=[string]$project.Project.PropertyGroup.Version
}
if($LauncherVersion -notmatch '^\d{1,6}\.\d{1,6}\.\d{1,6}$'){throw 'LauncherVersion must be MAJOR.MINOR.PATCH.'}
$env:DOTNET_CLI_HOME=Join-Path $root 'runtime/dotnet-home'
$env:NUGET_PACKAGES=Join-Path $root 'runtime/nuget-packages'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
# Use separate output directories so framework-dependent files cannot contaminate a standalone release.
$flavor=if($FrameworkDependent){'launcher-framework-dependent'}else{'launcher'}
$output=Join-Path $root "build/$flavor"
$selfContained=if($FrameworkDependent){'false'}else{'true'}
& dotnet publish source/launcher/TankLauncher/TankLauncher.csproj -c Release -r win-x64 --self-contained $selfContained -o $output -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false "-p:Version=$LauncherVersion"
if($LASTEXITCODE -ne 0){throw 'Launcher publish failed. A standalone build needs network access on its first build to restore .NET runtime packs.'}
$config=[ordered]@{manifestUrl=$ManifestUrl;allowLoopbackHttp=[bool]$AllowLoopbackHttp;launcherManifestUrl=$LauncherManifestUrl}
$json=$config | ConvertTo-Json
[IO.File]::WriteAllText((Join-Path $output 'launcher.json'),$json,(New-Object Text.UTF8Encoding($false)))
$bundle=Join-Path $root "build/$flavor-win64.zip"
Compress-Archive -LiteralPath (Join-Path $output 'Launcher.exe'),(Join-Path $output 'launcher.json') -DestinationPath $bundle -Force
Write-Output "Built $output/Launcher.exe. Distribute the entire folder, including launcher.json."
Write-Output "Player download: $bundle"
if($FrameworkDependent){Write-Warning 'This development build requires the .NET 9 Desktop Runtime on the player machine. Use the default standalone build for distribution.'}
else {
    $asset="TankBattle-Launcher-$LauncherVersion-win64.exe"
    $assetPath=Join-Path $output $asset
    Copy-Item -LiteralPath (Join-Path $output 'Launcher.exe') -Destination $assetPath -Force
    $manifest=[ordered]@{schemaVersion=1;version=$LauncherVersion;packageUrl=$ReleaseBaseUrl.TrimEnd('/')+'/'+$asset;sha256=(Get-FileHash -LiteralPath $assetPath -Algorithm SHA256).Hash.ToLowerInvariant();size=(Get-Item -LiteralPath $assetPath).Length}
    [IO.File]::WriteAllText((Join-Path $output 'launcher-manifest.json'),($manifest | ConvertTo-Json),(New-Object Text.UTF8Encoding($false)))
    Write-Output "Upload $asset, launcher-manifest.json and $bundle to GitHub Releases; retain the game manifest/assets on latest."
}
