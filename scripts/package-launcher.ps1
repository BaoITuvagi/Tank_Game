param(
    [Parameter(Mandatory=$true)][string]$ExecutablePath,
    [string]$ConfigurationPath,
    [string]$OutputPath=(Join-Path (Split-Path $PSScriptRoot -Parent) 'build/TankBattle-launcher-win64.zip')
)
$ErrorActionPreference='Stop'
$executable=Get-Item -LiteralPath $ExecutablePath
if($executable.PSIsContainer -or $executable.Extension -ne '.exe'){
    throw 'ExecutablePath must point to a launcher .exe file.'
}
$files=@($executable.FullName)
if($ConfigurationPath){
    $configuration=Get-Item -LiteralPath $ConfigurationPath
    if($configuration.PSIsContainer -or $configuration.Name -ne 'launcher.json'){
        throw 'ConfigurationPath must point to launcher.json.'
    }
    $files+=$configuration.FullName
}
$destination=$ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputPath)
if([IO.Path]::GetExtension($destination) -ne '.zip'){throw 'OutputPath must end in .zip.'}
$null=New-Item -ItemType Directory -Path (Split-Path $destination -Parent) -Force
# Include only the requested player files, never stale binaries or build output.
Compress-Archive -LiteralPath $files -DestinationPath $destination -Force
Write-Output "Player download: $destination"
