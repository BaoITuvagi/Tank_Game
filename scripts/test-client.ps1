param([string]$Unity=$env:UNITY_EDITOR_PATH)
$ErrorActionPreference='Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)
$Unity=& ./scripts/resolve-unity-editor.ps1 -Unity $Unity
New-Item -ItemType Directory -Force runtime | Out-Null
$project=(Resolve-Path source/client).Path
$log=Join-Path (Get-Location) 'runtime/unity-tests.log'
$result=Join-Path (Get-Location) 'runtime/unity-tests.xml'
$test=Start-Process -FilePath $Unity -ArgumentList "-batchmode -nographics -projectPath `"$project`" -runTests -testPlatform EditMode -testResults `"$result`" -logFile `"$log`"" -WindowStyle Hidden -PassThru
$test.WaitForExit()
if($test.ExitCode -ne 0){throw "Unity tests failed: inspect $log and $result"}
[xml]$report=Get-Content -LiteralPath $result
if([int]$report.'test-run'.total -eq 0 -or [int]$report.'test-run'.failed -ne 0 -or [int]$report.'test-run'.passed -ne [int]$report.'test-run'.total){throw 'Unity tests failed or incomplete; inspect result XML'}
Write-Output "Tests: $result"


