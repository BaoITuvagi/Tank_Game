# Copy to config/tools.local.ps1 and set paths for your own machine.
# Dot-source the local file before running build/test scripts:
# . ./config/tools.local.ps1
$env:JAVA_HOME='REPLACE_WITH_JDK_21_DIRECTORY'
$env:UNITY_EDITOR_PATH='REPLACE_WITH_UNITY_EDITOR_EXECUTABLE'
$env:Path=(Join-Path $env:JAVA_HOME 'bin')+[IO.Path]::PathSeparator+$env:Path
