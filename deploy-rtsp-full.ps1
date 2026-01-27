# Full Deploy RTSP-enabled Scale Streamer
# Run as Administrator

$ErrorActionPreference = "Stop"

Write-Host "=== Full Deploy of RTSP-Enabled Scale Streamer ===" -ForegroundColor Cyan

# Stop service
Write-Host "Stopping service..." -ForegroundColor Yellow
net stop ScaleStreamerService 2>&1 | Out-Null
Start-Sleep 3

# Source - self-contained publish output
$ServicePublish = "C:\Users\Windfield\Cloud-Scale\win-scale\src-v2\ScaleStreamer.Service\bin\Release\net8.0\win-x64\publish"

# Target directory
$ServiceDir = "C:\Program Files\Scale Streamer\Service"

Write-Host "Copying all published files to Service folder..." -ForegroundColor Yellow

# Copy ALL files from publish directory
Get-ChildItem $ServicePublish -File | ForEach-Object {
    Copy-Item $_.FullName $ServiceDir -Force
}

Write-Host "Files copied: $((Get-ChildItem $ServiceDir -File).Count)" -ForegroundColor Cyan

# Check for System.Drawing.Common
$drawingDll = Join-Path $ServiceDir "System.Drawing.Common.dll"
if (Test-Path $drawingDll) {
    Write-Host "System.Drawing.Common.dll: OK" -ForegroundColor Green
} else {
    Write-Host "System.Drawing.Common.dll: MISSING" -ForegroundColor Red
}

Write-Host "Starting service..." -ForegroundColor Yellow
net start ScaleStreamerService

Start-Sleep 2

# Check if service is running
$svc = Get-Service ScaleStreamerService
Write-Host "Service status: $($svc.Status)" -ForegroundColor $(if ($svc.Status -eq 'Running') { 'Green' } else { 'Red' })

Write-Host ""
Write-Host "=== Deployment Complete ===" -ForegroundColor Green
Write-Host ""
Write-Host "RTSP Stream URL: rtsp://localhost:8554/scale" -ForegroundColor Cyan
Write-Host ""
Write-Host "To test with VLC:" -ForegroundColor Yellow
Write-Host "  vlc rtsp://localhost:8554/scale" -ForegroundColor White
Write-Host ""
