# Run as Administrator to update Service DLLs
Write-Host "Updating Scale Streamer Service..." -ForegroundColor Cyan

Write-Host "Stopping service..." -ForegroundColor Yellow
net stop ScaleStreamerService 2>$null
Start-Sleep 2

Write-Host "Copying new DLLs..." -ForegroundColor Yellow
Copy-Item "C:\Users\Windfield\Cloud-Scale\win-scale\src-v2\ScaleStreamer.Common\bin\Release\net8.0\ScaleStreamer.Common.dll" "C:\Program Files\Scale Streamer\Service\" -Force
Copy-Item "C:\Users\Windfield\Cloud-Scale\win-scale\src-v2\ScaleStreamer.Service\bin\Release\net8.0\ScaleStreamer.Service.dll" "C:\Program Files\Scale Streamer\Service\" -Force

Write-Host "Starting service..." -ForegroundColor Yellow
net start ScaleStreamerService
Start-Sleep 2

Write-Host ""
Get-Service ScaleStreamerService | Select-Object Name, Status
Write-Host ""
Write-Host "Done!" -ForegroundColor Green
