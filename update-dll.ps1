Stop-Process -Name ScaleStreamer.Config -Force -ErrorAction SilentlyContinue
Start-Sleep 1
Copy-Item -Path 'C:\Users\Windfield\Cloud-Scale\win-scale\src-v2\ScaleStreamer.Common\bin\Release\net8.0\ScaleStreamer.Common.dll' -Destination 'C:\Program Files\Scale Streamer\Config\' -Force
Write-Host "Copied!"
Get-Item 'C:\Program Files\Scale Streamer\Config\ScaleStreamer.Common.dll'
Start-Process 'C:\Program Files\Scale Streamer\Config\ScaleStreamer.Config.exe'
