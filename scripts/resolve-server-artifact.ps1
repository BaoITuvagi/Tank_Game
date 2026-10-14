param([string]$Path='build/server/tank-server-1.0.0.jar')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$source=(Resolve-Path -LiteralPath (Join-Path $root $Path)).Path
$snapshots=Join-Path $root 'runtime/server-artifacts'
New-Item -ItemType Directory -Force $snapshots | Out-Null
$temporary=Join-Path $snapshots ([guid]::NewGuid().ToString('N')+'.tmp')
try {
 Copy-Item -LiteralPath $source -Destination $temporary
 $hash=(Get-FileHash -LiteralPath $temporary -Algorithm SHA256).Hash
 $directory=Join-Path $snapshots $hash
 New-Item -ItemType Directory -Force $directory | Out-Null
 $target=Join-Path $directory 'tank-server-1.0.0.jar'
 if(Test-Path -LiteralPath $target){
  if((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ne $hash){throw 'Immutable server artifact was modified'}
 }else{
  try{[IO.File]::Move($temporary,$target)}catch{
   if(!(Test-Path -LiteralPath $target) -or (Get-FileHash -LiteralPath $target).Hash -ne $hash){throw}
  }
 }
 # Every JVM uses an immutable copy; publishing a later build cannot alter lazy class loading.
 Write-Output $target
}finally{
 if(Test-Path -LiteralPath $temporary){Remove-Item -LiteralPath $temporary}
}
