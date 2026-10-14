param([string]$Config='',[string]$Java='')
$ErrorActionPreference='Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)
$Java=& ./scripts/resolve-java.ps1 -Java $Java
if(!$Config){$Config=if(Test-Path -LiteralPath 'config/server.local.properties'){'config/server.local.properties'}else{'config/server.properties'}}
$serverJar=& ./scripts/resolve-server-artifact.ps1
& $Java -jar $serverJar $Config
if($LASTEXITCODE -ne 0){throw "Server exited with code $LASTEXITCODE"}
