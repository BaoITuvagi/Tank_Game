@echo off
setlocal
title Tank Battle Server
cd /d "%~dp0"
if errorlevel 1 (
    echo Cannot open the Tank_Game folder.
    pause
    exit /b 1
)

echo Starting Tank Battle server...
echo Keep this window open while playing. Press Ctrl+C to stop.
echo.

if exist "%ProgramFiles%\Java\jdk-21.0.12.1\bin\java.exe" (
    powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\start-server.ps1" -Java "%ProgramFiles%\Java\jdk-21.0.12.1\bin\java.exe"
) else (
    powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\start-server.ps1"
)
set "serverExitCode=%errorlevel%"

echo.
if not "%serverExitCode%"=="0" echo Server failed to start or stopped with an error. See the message above.
pause
exit /b %serverExitCode%
