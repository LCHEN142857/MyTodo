$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  MyTodo Build Script" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Set environment variables for faster downloads (China mirror - npmmirror)
$env:ELECTRON_MIRROR = "https://npmmirror.com/mirrors/electron/"
$env:ELECTRON_BUILDER_BINARIES_MIRROR = "https://registry.npmmirror.com/-/binary/electron-builder-binaries/"
$env:CSC_IDENTITY_AUTO_DISCOVERY = "false"

# Step 1: Install dependencies if needed
if (-not (Test-Path "node_modules\electron\dist\electron.exe")) {
    Write-Host "[1/4] Installing dependencies..." -ForegroundColor Yellow
    npm install
    if ($LASTEXITCODE -ne 0) { Write-Host "npm install failed!" -ForegroundColor Red; exit 1 }
} else {
    Write-Host "[1/4] Dependencies already installed." -ForegroundColor Green
}

# Step 2: Verify icon exists
$srcIconIco = "src\icon.ico"
if (-not (Test-Path $srcIconIco)) {
    Write-Host "ERROR: $srcIconIco not found! Please place the app icon there before building." -ForegroundColor Red
    exit 1
}
Write-Host "[2/4] Using icon: $srcIconIco" -ForegroundColor Green

# Step 3: Build with electron-builder (unpacked + portable + nsis)
# --publish never: skip auto-publish (avoids extra network requests)
# Note: electron-builder sets the icon for portable/installer exes, but win-unpacked\MyTodo.exe
# keeps the default Electron icon. We fix that in Step 4 with rcedit.
Write-Host "[3/4] Building executables..." -ForegroundColor Yellow
npx electron-builder --win portable nsis --publish never
if ($LASTEXITCODE -ne 0) { Write-Host "Build failed!" -ForegroundColor Red; exit 1 }

# Step 4: Fix icon on win-unpacked\MyTodo.exe using rcedit
# electron-builder only applies the icon during the packaging step for installers/portable.
# The unpacked exe retains the default Electron icon, so we use rcedit to fix it.
$rcedit = "$env:LOCALAPPDATA\electron-builder\Cache\winCodeSign\*\rcedit-x64.exe"
$rceditPath = (Get-Item $rcedit -ErrorAction SilentlyContinue | Select-Object -First 1).FullName
$exePath = "dist\win-unpacked\MyTodo.exe"

if ($rceditPath -and (Test-Path $srcIconIco) -and (Test-Path $exePath)) {
    Write-Host "[4/4] Setting icon on unpacked executable..." -ForegroundColor Yellow
    & $rceditPath $exePath --set-icon (Resolve-Path $srcIconIco).Path
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  Icon set successfully." -ForegroundColor Green
    } else {
        Write-Host "  WARNING: rcedit failed, icon may not be set on unpacked exe." -ForegroundColor DarkYellow
    }
} else {
    Write-Host "[4/4] Skipping unpacked icon fix (rcedit or icon not found)." -ForegroundColor DarkGray
}

Write-Host ""
Write-Host "Build complete!" -ForegroundColor Green
Write-Host ""
Write-Host "Output files:" -ForegroundColor Cyan
Get-ChildItem "dist\*.exe" | ForEach-Object {
    $sizeMB = [math]::Round($_.Length / 1MB, 1)
    Write-Host "  $($_.Name)  ($sizeMB MB)" -ForegroundColor White
}
Write-Host ""
Write-Host "Unpacked app: dist\win-unpacked\MyTodo.exe" -ForegroundColor DarkGray
