@echo off
setlocal
cd /d "%~dp0"

rem One-click restore: reopen all previously opened CodeBuddy windows
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Open-CodeBuddyWindows.ps1" -DelayMs 1500
if not "%ERRORLEVEL%"=="0" (
    echo.
    pause
) else (
    ping -n 5 127.0.0.1 >nul
)
endlocal
