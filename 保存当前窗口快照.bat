@echo off
setlocal
cd /d "%~dp0"

rem Save a snapshot of all currently opened CodeBuddy windows
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Save-CodeBuddyWindows.ps1"
if not "%ERRORLEVEL%"=="0" (
    echo.
    pause
) else (
    ping -n 5 127.0.0.1 >nul
)
endlocal
