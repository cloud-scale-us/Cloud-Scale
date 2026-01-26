# Deploy updated ScaleStreamer.Common.dll with debug logging
Write-Host "Stopping Config app..."
Get-Process | Where-Object { $_.Name -like '*ScaleStreamer.Config*' } | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep 2

Write-Host "Copying updated DLL..."
Copy-Item 'C:\Users\Windfield\Cloud-Scale\win-scale\src-v2\ScaleStreamer.Common\bin\Release\net8.0\ScaleStreamer.Common.dll' 'C:\Program Files\Scale Streamer\Config\' -Force

Write-Host "Verifying..."
Get-Item 'C:\Program Files\Scale Streamer\Config\ScaleStreamer.Common.dll' | Select-Object Name, Length, LastWriteTime

Write-Host "Starting Config app..."
Start-Process 'C:\Program Files\Scale Streamer\Config\ScaleStreamer.Config.exe'

Write-Host "Done!"
