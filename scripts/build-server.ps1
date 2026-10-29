param([string]$JavaHome=$env:JAVA_HOME,[string]$BuildDirectory='target')
$ErrorActionPreference='Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)
& python scripts/check-admin-integration.py --server
if($LASTEXITCODE -ne 0){throw 'Admin integration is missing; see check output before rebuilding the server'}
if($JavaHome){$env:JAVA_HOME=$JavaHome}
$env:MAVEN_SKIP_RC='1'
$maven=& ./scripts/resolve-maven.ps1
$root=(Get-Location).Path
$output=[IO.Path]::GetFullPath((Join-Path "$root/source/server" $BuildDirectory))
if(!$output.StartsWith($root+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Build directory must stay inside the workspace'}
$standard=Join-Path $root 'source/server/target'
$generatedRoot=($output.StartsWith((Join-Path $root 'runtime')+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase) -or $output.StartsWith((Join-Path $root 'build')+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase))
if($output -ne $standard -and (!$generatedRoot -or (Split-Path $output -Leaf) -notmatch '^server-')){throw 'Refusing to clean source/data: use target or a generated server-* build directory'}
& python scripts/sync-contracts.py --check
if($LASTEXITCODE -ne 0){throw 'Shared gameplay constants are out of date'}
& $maven -f source/server/pom.xml "-Dtank.build.directory=$output" clean package
if($LASTEXITCODE -ne 0){throw 'Maven failed'}
New-Item -ItemType Directory -Force build/server | Out-Null
$publish=Join-Path "$root/build/server" ([guid]::NewGuid().ToString('N')+'.tmp')
Copy-Item -LiteralPath "$output/tank-server-1.0.0.jar" -Destination $publish
Move-Item -LiteralPath $publish -Destination build/server/tank-server-1.0.0.jar -Force
Write-Output 'Built build/server/tank-server-1.0.0.jar'
