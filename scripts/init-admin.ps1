param([string]$Name='admin',[string]$Config='config/server.properties')
$ErrorActionPreference='Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)
$java=& "$PSScriptRoot/resolve-java.ps1"
$artifact=& "$PSScriptRoot/resolve-server-artifact.ps1"
& $java -cp $artifact tank.AdminSetup $Config $Name
if($LASTEXITCODE -ne 0){throw 'Admin initialization failed'}
