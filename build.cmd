@echo off
REM Build wrapper for MyTodo
REM Bypasses PowerShell execution policy restrictions for downloaded/untrusted scripts.
REM This is safe because it only affects this single script invocation.

cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1"
if %errorlevel% neq 0 (
    echo.
    echo Build failed with exit code %errorlevel%.
    pause
    exit /b %errorlevel%
)
echo.
echo Build complete.
pause
