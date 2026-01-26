# Deploy IPC fix - fire-and-forget welcome message
Write-Host "=== Deploying IPC Fix ===" -ForegroundColor Cyan

# 1. Stop Config app
Write-Host "Stopping Config app..."
Get-Process | Where-Object { $_.Name -like '*ScaleStreamer.Config*' } | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep 2

# 2. Stop service
Write-Host "Stopping ScaleStreamerService..."
net stop ScaleStreamerService 2>&1 | Out-Null
Start-Sleep 3

# 3. Copy updated DLLs
Write-Host "Copying updated ScaleStreamer.Common.dll to Service and Config directories..."
$sourceDll = 'C:\Users\Windfield\Cloud-Scale\win-scale\src-v2\ScaleStreamer.Common\bin\Release\net8.0\ScaleStreamer.Common.dll'

# Copy to Service directory
Copy-Item $sourceDll 'C:\Program Files\Scale Streamer\Service\' -Force
Write-Host "  -> Service directory: OK"

# Copy to Config directory
Copy-Item $sourceDll 'C:\Program Files\Scale Streamer\Config\' -Force
Write-Host "  -> Config directory: OK"

# Verify DLL timestamps
Write-Host "`nVerifying DLL timestamps:"
Get-Item 'C:\Program Files\Scale Streamer\Service\ScaleStreamer.Common.dll' | Select-Object Name, Length, LastWriteTime | Format-Table -AutoSize
Get-Item 'C:\Program Files\Scale Streamer\Config\ScaleStreamer.Common.dll' | Select-Object Name, Length, LastWriteTime | Format-Table -AutoSize

# 4. Start service
Write-Host "Starting ScaleStreamerService..."
net start ScaleStreamerService
Start-Sleep 3

# 5. Start Config app
Write-Host "Starting Config app..."
Start-Process 'C:\Program Files\Scale Streamer\Config\ScaleStreamer.Config.exe'
Start-Sleep 3

# 6. Verify
Write-Host "`nRunning processes:"
Get-Process | Where-Object { $_.Name -like '*ScaleStreamer*' } | Select-Object Name, Id | Format-Table -AutoSize

Write-Host "`nService status:"
Get-Service ScaleStreamerService | Select-Object Name, Status | Format-Table -AutoSize

Write-Host "`n=== Deployment Complete ===" -ForegroundColor Green
