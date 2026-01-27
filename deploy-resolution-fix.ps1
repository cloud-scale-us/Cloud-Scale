# Deploy 1920x1080 resolution fix
$ErrorActionPreference = "Continue"

Write-Host "=== Deploying Full HD Resolution (1920x1080) ===" -ForegroundColor Cyan
Write-Host ""

# Stop service
Write-Host "[1/3] Stopping ScaleStreamerService..." -ForegroundColor Yellow
$result = net stop ScaleStreamerService 2>&1
if ($LASTEXITCODE -eq 0 -or $result -match "is not started") {
    Write-Host "  Service stopped" -ForegroundColor Green
} else {
    Write-Host "  Service may not be running (continuing anyway)" -ForegroundColor DarkYellow
}

Start-Sleep -Seconds 2

# Copy files
Write-Host ""
Write-Host "[2/3] Copying updated DLLs..." -ForegroundColor Yellow
$sourcePath = "C:\Users\Windfield\Cloud-Scale\win-scale\src-v2"
$servicePath = "C:\Program Files\Scale Streamer\Service"

Copy-Item "$sourcePath\ScaleStreamer.Common\bin\Release\net8.0\ScaleStreamer.Common.dll" "$servicePath\" -Force
if ($?) {
    Write-Host "  ScaleStreamer.Common.dll copied" -ForegroundColor Gray
} else {
    Write-Host "  Failed to copy Common DLL" -ForegroundColor Red
}

Copy-Item "$sourcePath\ScaleStreamer.Service\bin\Release\net8.0\win-x64\ScaleStreamer.Service.dll" "$servicePath\" -Force
if ($?) {
    Write-Host "  ScaleStreamer.Service.dll copied" -ForegroundColor Gray
} else {
    Write-Host "  Failed to copy Service DLL" -ForegroundColor Red
}

# Start service
Write-Host ""
Write-Host "[3/3] Starting ScaleStreamerService..." -ForegroundColor Yellow
net start ScaleStreamerService
if ($LASTEXITCODE -eq 0) {
    Write-Host "  Service started" -ForegroundColor Green
} else {
    Write-Host "  Failed to start service" -ForegroundColor Red
}

Write-Host ""
Write-Host "=== Deployment Complete ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "Resolution changed from 1280x720 to 1920x1080" -ForegroundColor White
Write-Host "Font size increased from 72 to 120" -ForegroundColor White
Write-Host "Q value = 95 for high quality JPEG" -ForegroundColor White
Write-Host ""

# Show service status
Get-Service ScaleStreamerService | Format-Table Name, Status -AutoSize

Read-Host "Press Enter to exit"
