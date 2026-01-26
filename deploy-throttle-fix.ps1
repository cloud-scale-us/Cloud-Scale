# Deploy UI Throttling Fix - Run as Administrator
# This script stops the service, copies new DLLs, and restarts the service

Write-Host "Deploying UI Throttling Fix..." -ForegroundColor Cyan

# Stop service
Write-Host "Stopping ScaleStreamerService..." -ForegroundColor Yellow
net stop ScaleStreamerService 2>$null
Start-Sleep -Seconds 2

# Kill any running Config app
Write-Host "Closing Config app if running..." -ForegroundColor Yellow
Get-Process -Name "ScaleStreamer.Config" -ErrorAction SilentlyContinue | Stop-Process -Force

# Copy new DLLs to Config directory
Write-Host "Copying Config DLLs..." -ForegroundColor Yellow
Copy-Item 'C:\Users\Windfield\Cloud-Scale\win-scale\src-v2\ScaleStreamer.Common\bin\Release\net8.0\ScaleStreamer.Common.dll' 'C:\Program Files\Scale Streamer\Config\' -Force
Copy-Item 'C:\Users\Windfield\Cloud-Scale\win-scale\src-v2\ScaleStreamer.Config\bin\Release\net8.0-windows\ScaleStreamer.Config.dll' 'C:\Program Files\Scale Streamer\Config\' -Force

# Copy new DLLs to Service directory
Write-Host "Copying Service DLLs..." -ForegroundColor Yellow
Copy-Item 'C:\Users\Windfield\Cloud-Scale\win-scale\src-v2\ScaleStreamer.Common\bin\Release\net8.0\ScaleStreamer.Common.dll' 'C:\Program Files\Scale Streamer\Service\' -Force
Copy-Item 'C:\Users\Windfield\Cloud-Scale\win-scale\src-v2\ScaleStreamer.Service\bin\Release\net8.0\ScaleStreamer.Service.dll' 'C:\Program Files\Scale Streamer\Service\' -Force

# Start service
Write-Host "Starting ScaleStreamerService..." -ForegroundColor Yellow
net start ScaleStreamerService

# Show status
Write-Host ""
Write-Host "Deployment complete!" -ForegroundColor Green
Get-Service ScaleStreamerService | Format-Table Name, Status -AutoSize

Write-Host ""
Write-Host "Changes deployed:" -ForegroundColor Cyan
Write-Host "  - UI throttling: Max 4 updates/second (was ~5)" -ForegroundColor White
Write-Host "  - BeginInvoke instead of Invoke (non-blocking)" -ForegroundColor White
Write-Host "  - SuspendLayout/ResumeLayout for batch updates" -ForegroundColor White
Write-Host "  - ListView BeginUpdate/EndUpdate for history" -ForegroundColor White
Write-Host "  - Reduced log buffer from 500 to 200 lines" -ForegroundColor White
Write-Host ""
Write-Host "Press any key to exit..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
