@echo off
setlocal enabledelayedexpansion
cd /d "%~dp0"

echo ========================================
echo   MyTodo Build Script
echo ========================================
echo.

REM ==================================================================
REM Step 1: Set environment variables (China mirror - npmmirror)
REM ==================================================================
set "ELECTRON_MIRROR=https://npmmirror.com/mirrors/electron/"
set "ELECTRON_BUILDER_BINARIES_MIRROR=https://registry.npmmirror.com/-/binary/electron-builder-binaries/"
set "CSC_IDENTITY_AUTO_DISCOVERY=false"

REM ==================================================================
REM Step 2: Install dependencies if needed
REM ==================================================================
if not exist "node_modules\electron\dist\electron.exe" (
    echo [1/4] Installing dependencies...
    call npm install
    if errorlevel 1 (
        echo [ERROR] npm install failed!
        pause
        exit /b 1
    )
) else (
    echo [1/4] Dependencies already installed.
)

REM ==================================================================
REM Step 3: Verify icon exists
REM ==================================================================
set "SRC_ICON=src\icon.ico"
if not exist "%SRC_ICON%" (
    echo [ERROR] %SRC_ICON% not found! Please place the app icon there before building.
    pause
    exit /b 1
)
echo [2/4] Using icon: %SRC_ICON%

REM ==================================================================
REM Step 4: Build with electron-builder (unpacked + portable + nsis)
REM ==================================================================
echo [3/4] Building executables...
call npx electron-builder --win portable nsis --publish never
if errorlevel 1 (
    echo [ERROR] Build failed!
    pause
    exit /b 1
)

REM ==================================================================
REM Step 5: Fix icon on win-unpacked\MyTodo.exe using rcedit
REM electron-builder sets icon for portable/installer but not unpacked exe.
REM ==================================================================
echo [4/4] Setting icon on unpacked executable...

set "RCEDIT="
for /f "delims=" %%i in ('dir /s /b "%LOCALAPPDATA%\electron-builder\Cache\winCodeSign\rcedit-x64.exe" 2^>nul') do (
    set "RCEDIT=%%i"
)

set "UNPACKED_EXE=dist\win-unpacked\MyTodo.exe"

if defined RCEDIT (
    if exist "%SRC_ICON%" (
        if exist "%UNPACKED_EXE%" (
            "%RCEDIT%" "%UNPACKED_EXE%" --set-icon "%~dp0%SRC_ICON%"
            if errorlevel 1 (
                echo   WARNING: rcedit failed, icon may not be set on unpacked exe.
            ) else (
                echo   Icon set successfully.
            )
        ) else (
            echo   Skipped: %UNPACKED_EXE% not found.
        )
    ) else (
        echo   Skipped: icon file not found.
    )
) else (
    echo   Skipped: rcedit not found in cache.
)

REM ==================================================================
REM Show results
REM ==================================================================
echo.
echo Build complete!
echo.
echo Output files:
for %%f in (dist\*.exe) do (
    for %%A in ("%%f") do (
        set "size=%%~zA"
        set /a "sizeMB=size / 1048576"
        echo   %%~nxA  (!sizeMB! MB^)
    )
)
echo.
echo Unpacked app: dist\win-unpacked\MyTodo.exe
echo.
pause
