$ErrorActionPreference='Stop'
$command=Get-Command mvn -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
if($command){return $command.Source}
foreach($base in @($env:MAVEN_HOME,$env:M2_HOME)){
    if($base){
        $candidate=Join-Path $base 'bin/mvn.cmd'
        if(Test-Path -LiteralPath $candidate -PathType Leaf){return (Resolve-Path -LiteralPath $candidate).Path}
    }
}
if($env:USERPROFILE){
    $cache=Join-Path $env:USERPROFILE '.m2/wrapper/dists'
    if(Test-Path -LiteralPath $cache){
        $candidates=Get-ChildItem -LiteralPath $cache -Filter mvn.cmd -Recurse -File | ForEach-Object {
            if($_.FullName -match 'apache-maven-(\d+\.\d+\.\d+)'){
                [PSCustomObject]@{Version=[version]$Matches[1];Path=$_.FullName}
            }
        } | Sort-Object Version -Descending
        if($candidates){return ($candidates | Select-Object -First 1).Path}
    }
}
throw 'Maven not found. Install Maven 3.9.x and add its bin directory to PATH or set MAVEN_HOME.'
