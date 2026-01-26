# Deploy the scale registration and CR delimiter fix
Write-Host "Stopping ScaleStreamerService..."
net stop ScaleStreamerService
Start-Sleep 3

Write-Host "Copying updated DLLs..."
Copy-Item "C:\Users\Windfield\Cloud-Scale\win-scale\src-v2\ScaleStreamer.Service\bin\Release\net8.0\ScaleStreamer.Service.dll" "C:\Program Files\Scale Streamer\Service\" -Force
Copy-Item "C:\Users\Windfield\Cloud-Scale\win-scale\src-v2\ScaleStreamer.Common\bin\Release\net8.0\ScaleStreamer.Common.dll" "C:\Program Files\Scale Streamer\Service\" -Force

Write-Host "Verifying..."
Get-Item "C:\Program Files\Scale Streamer\Service\ScaleStreamer.Service.dll" | Select-Object Name, LastWriteTime
Get-Item "C:\Program Files\Scale Streamer\Service\ScaleStreamer.Common.dll" | Select-Object Name, LastWriteTime

Write-Host "Starting ScaleStreamerService..."
net start ScaleStreamerService
Start-Sleep 2

Get-Service ScaleStreamerService | Select-Object Name, Status
Write-Host "Done!"
