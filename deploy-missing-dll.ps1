# Deploy missing ServiceController DLL - Run as Administrator

Write-Host "Deploying missing System.ServiceProcess.ServiceController.dll..." -ForegroundColor Cyan

# Kill any running Config app
Write-Host "Closing Config app if running..." -ForegroundColor Yellow
Get-Process -Name "ScaleStreamer.Config" -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 1

# Copy missing DLL
Write-Host "Copying ServiceController DLL..." -ForegroundColor Yellow
Copy-Item 'C:\Users\Windfield\Cloud-Scale\win-scale\src-v2\ScaleStreamer.Config\bin\Release\net8.0-windows\System.ServiceProcess.ServiceController.dll' 'C:\Program Files\Scale Streamer\Config\' -Force

Write-Host ""
Write-Host "Done! Starting Config app..." -ForegroundColor Green
Start-Process 'C:\Program Files\Scale Streamer\Config\ScaleStreamer.Config.exe'
