param([switch]$IncludeUiSmoke, [switch]$KeepUi)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
Set-Location $root
$env:DOTNET_CLI_HOME=Join-Path $root 'runtime/dotnet-home'
$env:NUGET_PACKAGES=Join-Path $root 'runtime/nuget-packages'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$project='source/launcher/TankLauncher.Checks/TankLauncher.Checks.csproj'
foreach($fixture in @(@{name='old';version='1.2.0'},@{name='new';version='1.3.0'})) {
    $output=Join-Path $root ('build/launcher-checks-'+$fixture.name)
    & dotnet publish $project -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:DebugType=None -p:DebugSymbols=false -o $output "-p:Version=$($fixture.version)"
    if($LASTEXITCODE -ne 0){throw "Failed to publish $($fixture.name) launcher fixture."}
}
$artifacts=Join-Path $root ('runtime/launcher-checks/'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'-'+[guid]::NewGuid().ToString('N').Substring(0,8))
New-Item -ItemType Directory -Path $artifacts -Force | Out-Null
$old=Join-Path $root 'build/launcher-checks-old/TankLauncher.Checks.exe'
$new=Join-Path $root 'build/launcher-checks-new/TankLauncher.Checks.exe'
$checkArgs=@($old,$new,$artifacts)
if($KeepUi -and !$IncludeUiSmoke){throw 'KeepUi requires IncludeUiSmoke.'}
if($IncludeUiSmoke){
    foreach($fixture in @(@{name='old';version='1.2.0'},@{name='new';version='1.3.0'})) {
        $output=Join-Path $root ('build/launcher-ui-'+$fixture.name)
        & dotnet publish source/launcher/TankLauncher/TankLauncher.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -p:DefineConstants=LAUNCHER_TEST -o $output "-p:Version=$($fixture.version)"
        if($LASTEXITCODE -ne 0){throw "Failed to publish $($fixture.name) UI fixture."}
        $checkArgs+=Join-Path $output 'Launcher.exe'
    }
    if($KeepUi){$checkArgs+='--keep-ui'}
}
& $old @checkArgs | Tee-Object -FilePath (Join-Path $artifacts 'checks.log')
if($LASTEXITCODE -ne 0){throw "Launcher checks failed. Artifacts: $artifacts"}
