@echo off
rem Copy to config\tools.local.cmd and replace paths for your machine.
rem Load into the current CMD window with: call config\tools.local.cmd
rem Do not add setlocal: these variables must remain in the calling CMD.
set "JAVA_HOME=REPLACE_WITH_JDK_21_DIRECTORY"
set "UNITY_EDITOR_PATH=REPLACE_WITH_UNITY_EDITOR_EXECUTABLE"
set "PATH=%JAVA_HOME%\bin;%PATH%"
