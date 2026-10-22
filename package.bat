@echo off
setlocal
cd /d "%~dp0"

tasklist /FI "IMAGENAME eq CodeBuddyWindowMaster.exe" 2>nul | find /I "CodeBuddyWindowMaster.exe" >nul
if not errorlevel 1 (
    echo [WARN] CodeBuddyWindowMaster.exe is running - close it first, then run this again.
    if not defined CI pause
    exit /b 1
)

echo [1/3] Building exe ...
call build.bat
if errorlevel 1 goto fail

echo [2/3] Assembling dist ...
if exist dist rmdir /s /q dist
mkdir dist
copy /y "CodeBuddyWindowMaster.exe" "dist\" >nul
copy /y "docs\README.txt" "dist\" >nul
copy /y "docs\bookmarks.empty.json" "dist\bookmarks.json" >nul

echo [3/3] Zipping ...
if exist "CodeBuddyWindowMaster-portable.zip" del /q "CodeBuddyWindowMaster-portable.zip"
powershell -NoProfile -Command "Compress-Archive -Path 'dist\*' -DestinationPath 'CodeBuddyWindowMaster-portable.zip' -Force"
if errorlevel 1 goto fail

echo.
echo [OK] dist\  and  CodeBuddyWindowMaster-portable.zip
dir /b dist
exit /b 0

:fail
echo.
echo [FAILED]
if not defined CI pause
exit /b 1
