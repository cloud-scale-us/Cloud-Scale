# Run this script as Administrator to install the logging update
# Right-click and select "Run with PowerShell" or run from elevated PowerShell

param([switch]$NoPrompt)

Write-Host "Installing comprehensive logging update..." -ForegroundColor Cyan

# Kill Config apps
Write-Host "Stopping Config apps..." -ForegroundColor Yellow
taskkill /f /im ScaleStreamer.Config.exe 2>$null

# Stop service
Write-Host "Stopping Scale Streamer Service..." -ForegroundColor Yellow
net stop ScaleStreamerService 2>$null
Start-Sleep -Seconds 2

# Copy updated DLLs to Service directory
Write-Host "Copying updated DLLs to Service directory..." -ForegroundColor Yellow
Copy-Item 'C:\Users\Windfield\Cloud-Scale\win-scale\src-v2\ScaleStreamer.Common\bin\Release\net8.0\ScaleStreamer.Common.dll' 'C:\Program Files\Scale Streamer\Service\' -Force
Copy-Item 'C:\Users\Windfield\Cloud-Scale\win-scale\src-v2\ScaleStreamer.Service\bin\Release\net8.0\ScaleStreamer.Service.dll' 'C:\Program Files\Scale Streamer\Service\' -Force

# Copy updated DLLs to Config directory
Write-Host "Copying updated DLLs to Config directory..." -ForegroundColor Yellow
Copy-Item 'C:\Users\Windfield\Cloud-Scale\win-scale\src-v2\ScaleStreamer.Common\bin\Release\net8.0\ScaleStreamer.Common.dll' 'C:\Program Files\Scale Streamer\Config\' -Force
Copy-Item 'C:\Users\Windfield\Cloud-Scale\win-scale\src-v2\ScaleStreamer.Config\bin\Release\net8.0-windows\ScaleStreamer.Config.dll' 'C:\Program Files\Scale Streamer\Config\' -Force

# Verify copies
Write-Host ""
Write-Host "Verifying DLLs..." -ForegroundColor Yellow
Get-Item 'C:\Program Files\Scale Streamer\Service\ScaleStreamer.Common.dll' | Select-Object Name, LastWriteTime
Get-Item 'C:\Program Files\Scale Streamer\Config\ScaleStreamer.Common.dll' | Select-Object Name, LastWriteTime

# Start service
Write-Host ""
Write-Host "Starting Scale Streamer Service..." -ForegroundColor Yellow
net start ScaleStreamerService
Start-Sleep -Seconds 2

# Check service status
Write-Host ""
Get-Service ScaleStreamerService | Select-Object Name, Status

Write-Host ""
Write-Host "Done! The logging update has been installed." -ForegroundColor Green
Write-Host "Launching Scale Streamer Config..." -ForegroundColor Green

# Launch the Config GUI
Start-Process 'C:\Program Files\Scale Streamer\Config\ScaleStreamer.Config.exe'

if (-not $NoPrompt) {
    Write-Host ""
    Write-Host "Press Enter to close this window"
    Read-Host
}
