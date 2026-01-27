# Deploy RTP sequence number fix
Write-Host "Stopping ScaleStreamerService..." -ForegroundColor Yellow
net stop ScaleStreamerService 2>&1 | Out-Null
Start-Sleep -Seconds 2

Write-Host "Copying updated DLLs..." -ForegroundColor Yellow
Copy-Item 'C:\Users\Windfield\Cloud-Scale\win-scale\src-v2\ScaleStreamer.Common\bin\Release\net8.0\ScaleStreamer.Common.dll' 'C:\Program Files\Scale Streamer\Service\' -Force

Write-Host "Starting ScaleStreamerService..." -ForegroundColor Yellow
net start ScaleStreamerService

Write-Host "Done! RTP fix deployed." -ForegroundColor Green
