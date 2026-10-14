param([string]$Java='')
$ErrorActionPreference='Stop'
if(!$Java){
    if($env:JAVA_HOME){$Java=Join-Path $env:JAVA_HOME 'bin/java.exe'}else{$Java='java'}
}
$command=Get-Command $Java -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
if(!$command){throw 'Java not found. Install JDK 21 and set JAVA_HOME/PATH, or pass -Java with the Java executable path.'}
return $command.Source
