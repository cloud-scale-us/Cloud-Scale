# Deploy updated DLLs to installed service
$ErrorActionPreference = "Continue"

Write-Host "=== Deploying Updated ScaleStreamer ===" -ForegroundColor Cyan

# Stop service first
Write-Host "[1/3] Stopping service..." -ForegroundColor Yellow
net stop ScaleStreamerService 2>&1 | Out-Null
Start-Sleep -Seconds 3

# Copy files
Write-Host "[2/3] Copying updated DLLs..." -ForegroundColor Yellow
$source = "C:\Users\Windfield\Cloud-Scale\win-scale\src-v2"
$dest = "C:\Program Files\Scale Streamer\Service"

Copy-Item "$source\ScaleStreamer.Common\bin\Release\net8.0\ScaleStreamer.Common.dll" "$dest\" -Force
Write-Host "  Common.dll copied" -ForegroundColor Gray
Copy-Item "$source\ScaleStreamer.Service\bin\Release\net8.0\ScaleStreamer.Service.dll" "$dest\" -Force
Write-Host "  Service.dll copied" -ForegroundColor Gray

# Start service
Write-Host "[3/3] Starting service..." -ForegroundColor Yellow
net start ScaleStreamerService

Write-Host ""
Write-Host "=== Done ===" -ForegroundColor Green
Get-Service ScaleStreamerService | Format-Table Name, Status -AutoSize
