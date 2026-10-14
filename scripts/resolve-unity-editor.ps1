param([string]$Unity=$env:UNITY_EDITOR_PATH)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$versionLine=Get-Content -LiteralPath (Join-Path $root 'source/client/ProjectSettings/ProjectVersion.txt') |
    Where-Object { $_ -match '^m_EditorVersion:' } | Select-Object -First 1
$version=($versionLine -split ':',2)[1].Trim()
if($Unity){
    if(!(Test-Path -LiteralPath $Unity -PathType Leaf)){throw "Unity Editor not found: $Unity. Set UNITY_EDITOR_PATH or pass -Unity pointing to the Editor executable."}
    return (Resolve-Path -LiteralPath $Unity).Path
}
$candidates=@()
foreach($base in @($env:ProgramFiles,${env:ProgramFiles(x86)},$env:LOCALAPPDATA)){
    if($base){$candidates += Join-Path $base "Unity/Hub/Editor/$version/Editor/Unity.exe"}
}
foreach($path in $candidates){if(Test-Path -LiteralPath $path -PathType Leaf){return (Resolve-Path -LiteralPath $path).Path}}
throw "Unity Editor $version not found. Install it with Windows Build Support, set UNITY_EDITOR_PATH, or pass -Unity with the Editor executable path."
