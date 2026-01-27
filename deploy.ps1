# deploy.ps1 - Deploy updated DLLs to Scale Streamer Service
# This script can run WITHOUT admin elevation after running grant-service-control.ps1

$ErrorActionPreference = "Stop"

Write-Host "=== Scale Streamer Deployment ===" -ForegroundColor Cyan
Write-Host ""

# Build first
Write-Host "[1/4] Building solution..." -ForegroundColor Yellow
Push-Location "C:\Users\Windfield\Cloud-Scale\win-scale\src-v2"
try {
    $buildResult = & dotnet build -c Release 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Build failed!" -ForegroundColor Red
        Write-Host $buildResult
        exit 1
    }
    Write-Host "Build successful" -ForegroundColor Green
}
finally {
    Pop-Location
}

# Stop service
Write-Host ""
Write-Host "[2/4] Stopping ScaleStreamerService..." -ForegroundColor Yellow
$stopResult = net stop ScaleStreamerService 2>&1
if ($LASTEXITCODE -ne 0 -and $stopResult -notmatch "is not started") {
    Write-Host "Warning: Could not stop service (may not be running)" -ForegroundColor DarkYellow
}
else {
    Write-Host "Service stopped" -ForegroundColor Green
}

# Wait a moment
Start-Sleep -Seconds 2

# Copy files
Write-Host ""
Write-Host "[3/4] Copying updated DLLs..." -ForegroundColor Yellow

$sourcePath = "C:\Users\Windfield\Cloud-Scale\win-scale\src-v2"
$servicePath = "C:\Program Files\Scale Streamer\Service"
$configPath = "C:\Program Files\Scale Streamer\Config"

# Copy to Service folder
Copy-Item "$sourcePath\ScaleStreamer.Common\bin\Release\net8.0\ScaleStreamer.Common.dll" "$servicePath\" -Force
Copy-Item "$sourcePath\ScaleStreamer.Service\bin\Release\net8.0\win-x64\ScaleStreamer.Service.dll" "$servicePath\" -Force
Write-Host "  Copied to Service folder" -ForegroundColor Gray

# Copy to Config folder
Copy-Item "$sourcePath\ScaleStreamer.Common\bin\Release\net8.0\ScaleStreamer.Common.dll" "$configPath\" -Force
Copy-Item "$sourcePath\ScaleStreamer.Config\bin\Release\net8.0-windows\ScaleStreamer.Config.dll" "$configPath\" -Force
Write-Host "  Copied to Config folder" -ForegroundColor Gray

Write-Host "Files copied" -ForegroundColor Green

# Start service
Write-Host ""
Write-Host "[4/4] Starting ScaleStreamerService..." -ForegroundColor Yellow
$startResult = net start ScaleStreamerService 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "Failed to start service!" -ForegroundColor Red
    Write-Host $startResult
    exit 1
}
Write-Host "Service started" -ForegroundColor Green

Write-Host ""
Write-Host "=== Deployment Complete ===" -ForegroundColor Cyan

# Show service status
Write-Host ""
Get-Service ScaleStreamerService | Format-Table Name, Status, StartType -AutoSize
