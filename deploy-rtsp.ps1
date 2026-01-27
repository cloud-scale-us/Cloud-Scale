# Deploy RTSP-enabled Scale Streamer
# Run as Administrator

$ErrorActionPreference = "Stop"

Write-Host "=== Deploying RTSP-Enabled Scale Streamer ===" -ForegroundColor Cyan

# Stop service
Write-Host "Stopping service..." -ForegroundColor Yellow
net stop ScaleStreamerService 2>&1 | Out-Null
Start-Sleep 2

# Source directories
$ServiceBin = "C:\Users\Windfield\Cloud-Scale\win-scale\src-v2\ScaleStreamer.Service\bin\Release\net8.0"
$CommonBin = "C:\Users\Windfield\Cloud-Scale\win-scale\src-v2\ScaleStreamer.Common\bin\Release\net8.0"

# Target directories
$ServiceDir = "C:\Program Files\Scale Streamer\Service"
$ConfigDir = "C:\Program Files\Scale Streamer\Config"

Write-Host "Copying new files..." -ForegroundColor Yellow

# Copy Common DLL to both Service and Config
Copy-Item "$CommonBin\ScaleStreamer.Common.dll" $ServiceDir -Force
Copy-Item "$CommonBin\ScaleStreamer.Common.dll" $ConfigDir -Force

# Copy new packages to Service (SharpRTSP and System.Drawing.Common)
if (Test-Path "$ServiceBin\SharpRTSP.dll") {
    Copy-Item "$ServiceBin\SharpRTSP.dll" $ServiceDir -Force
}
if (Test-Path "$ServiceBin\System.Drawing.Common.dll") {
    Copy-Item "$ServiceBin\System.Drawing.Common.dll" $ServiceDir -Force
}
if (Test-Path "$ServiceBin\Microsoft.Win32.SystemEvents.dll") {
    Copy-Item "$ServiceBin\Microsoft.Win32.SystemEvents.dll" $ServiceDir -Force
}

# Copy Service DLL
Copy-Item "$ServiceBin\ScaleStreamer.Service.dll" $ServiceDir -Force

Write-Host "Starting service..." -ForegroundColor Yellow
net start ScaleStreamerService

Write-Host ""
Write-Host "=== Deployment Complete ===" -ForegroundColor Green
Write-Host ""
Write-Host "RTSP Stream should be available at: rtsp://localhost:8554/scale" -ForegroundColor Cyan
Write-Host ""
Write-Host "To test with VLC:" -ForegroundColor Yellow
Write-Host "  1. Open VLC" -ForegroundColor White
Write-Host "  2. Media -> Open Network Stream" -ForegroundColor White
Write-Host "  3. Enter: rtsp://localhost:8554/scale" -ForegroundColor White
Write-Host ""
