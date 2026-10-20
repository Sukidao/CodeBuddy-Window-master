@echo off
setlocal
cd /d "%~dp0"

set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
    echo [ERROR] csc.exe not found.
    pause
    exit /b 1
)

set ICON=%LOCALAPPDATA%\Programs\CodeBuddy CN\resources\app\extensions\genie\out\webviews\favicon.ico
set ICONARG=
if exist "%ICON%" set ICONARG=/win32icon:"%ICON%"

"%CSC%" /nologo /target:winexe /platform:anycpu /optimize+ /codepage:65001 ^
    /out:"%~dp0CodeBuddyWindowMaster.exe" ^
    %ICONARG% ^
    /win32manifest:"%~dp0src\app.manifest" ^
    /r:System.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll ^
    "%~dp0src\CodeBuddyBookmarks.cs"

if errorlevel 1 (
    echo.
    echo [FAILED] compile error
    pause
    exit /b 1
)

echo.
echo [OK] CodeBuddyWindowMaster.exe
exit /b 0
