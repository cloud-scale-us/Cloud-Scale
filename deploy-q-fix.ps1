# Deploy Q value fix for JPEG RTP streaming
$ErrorActionPreference = "Continue"

Write-Host "=== Deploying Q Value Fix (Q=80) ===" -ForegroundColor Cyan
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
Write-Host "[2/3] Copying updated ScaleStreamer.Common.dll..." -ForegroundColor Yellow
$sourcePath = "C:\Users\Windfield\Cloud-Scale\win-scale\src-v2"
$servicePath = "C:\Program Files\Scale Streamer\Service"

Copy-Item "$sourcePath\ScaleStreamer.Common\bin\Release\net8.0\ScaleStreamer.Common.dll" "$servicePath\" -Force
if ($?) {
    Write-Host "  DLL copied successfully" -ForegroundColor Green
} else {
    Write-Host "  Failed to copy DLL" -ForegroundColor Red
    exit 1
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
Write-Host "The JPEG RTP Q value has been changed from 255 to 80." -ForegroundColor White
Write-Host "This uses standard quantization tables without embedding them in packets." -ForegroundColor White
Write-Host ""

# Show service status
Get-Service ScaleStreamerService | Format-Table Name, Status -AutoSize

Read-Host "Press Enter to exit"
