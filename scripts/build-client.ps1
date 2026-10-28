param([string]$Unity=$env:UNITY_EDITOR_PATH)
$ErrorActionPreference='Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)
& python scripts/check-admin-integration.py --client
if($LASTEXITCODE -ne 0){throw 'Admin notification integration is missing; see check output before rebuilding the client'}
$Unity=& ./scripts/resolve-unity-editor.ps1 -Unity $Unity
New-Item -ItemType Directory -Force runtime | Out-Null
$project=(Resolve-Path source/client).Path
$log=Join-Path (Get-Location) 'runtime/unity-build.log'
$build=Start-Process -FilePath $Unity -ArgumentList "-batchmode -nographics -quit -projectPath `"$project`" -executeMethod BuildClient.Build -logFile `"$log`"" -WindowStyle Hidden -PassThru
$build.WaitForExit()
if($build.ExitCode -ne 0){throw "Unity build failed: inspect $log"}
Write-Output 'Built build/client/TankBattle.exe'


