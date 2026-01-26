# Must run as admin - deploys updated Config app
Write-Host "Stopping Scale Streamer Config..."
Get-Process | Where-Object { $_.Name -like '*ScaleStreamer.Config*' } | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep 2

Write-Host "Copying updated DLLs..."
Copy-Item "C:\Users\Windfield\Cloud-Scale\win-scale\src-v2\ScaleStreamer.Common\bin\Release\net8.0\ScaleStreamer.Common.dll" "C:\Program Files\Scale Streamer\Config\" -Force
Copy-Item "C:\Users\Windfield\Cloud-Scale\win-scale\src-v2\ScaleStreamer.Config\bin\Release\net8.0-windows\ScaleStreamer.Config.dll" "C:\Program Files\Scale Streamer\Config\" -Force

Write-Host "Verifying..."
Get-Item "C:\Program Files\Scale Streamer\Config\ScaleStreamer.Config.dll" | Select-Object Name, Length, LastWriteTime
Get-Item "C:\Program Files\Scale Streamer\Config\ScaleStreamer.Common.dll" | Select-Object Name, Length, LastWriteTime

Write-Host "Starting Scale Streamer Config..."
Start-Process "C:\Program Files\Scale Streamer\Config\ScaleStreamer.Config.exe"

Write-Host "Done!"
