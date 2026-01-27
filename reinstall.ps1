# Force reinstall - uninstall then install fresh
$ErrorActionPreference = "Stop"

Write-Host "Force reinstalling Scale Streamer v4.2.0..." -ForegroundColor Cyan

# Kill Config app
Write-Host "Stopping Config app..." -ForegroundColor Yellow
Get-Process -Name "ScaleStreamer.Config" -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep 2

# Stop service
Write-Host "Stopping service..." -ForegroundColor Yellow
net stop ScaleStreamerService 2>$null

# Uninstall using msiexec
Write-Host "Uninstalling previous version..." -ForegroundColor Yellow
$msi = "C:\Users\Windfield\ScaleStreamer.msi"
Start-Process msiexec -ArgumentList "/x `"$msi`" /passive" -Wait

Start-Sleep 3

# Install fresh
Write-Host "Installing fresh..." -ForegroundColor Yellow
Start-Process msiexec -ArgumentList "/i `"$msi`" /passive" -Wait

Write-Host ""
Write-Host "Reinstall complete!" -ForegroundColor Green

# Start Config app
Write-Host "Starting Config app..." -ForegroundColor Cyan
Start-Process "C:\Program Files\Scale Streamer\Config\ScaleStreamer.Config.exe"
