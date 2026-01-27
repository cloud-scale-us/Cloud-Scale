# Deploy ONVIF-enabled service (self-contained)
Write-Host "Stopping ScaleStreamerService..." -ForegroundColor Yellow
net stop ScaleStreamerService 2>&1 | Out-Null
Start-Sleep -Seconds 2

$src = 'C:\Users\Windfield\Cloud-Scale\win-scale\src-v2\ScaleStreamer.Service\bin\publish'
$dst = 'C:\Program Files\Scale Streamer\Service'

Write-Host "Deploying self-contained service from $src to $dst..." -ForegroundColor Yellow
Get-ChildItem $src -Filter "*.dll" | ForEach-Object { Copy-Item $_.FullName $dst -Force }
Copy-Item "$src\ScaleStreamer.Service.exe" $dst -Force

# Copy all remaining files (json configs, etc)
Get-ChildItem $src -Exclude "*.pdb" | ForEach-Object {
    if (!(Test-Path "$dst\$($_.Name)") -or $_.Extension -ne '') {
        Copy-Item $_.FullName $dst -Force -ErrorAction SilentlyContinue
    }
}

Write-Host "Starting ScaleStreamerService..." -ForegroundColor Yellow
net start ScaleStreamerService
Start-Sleep -Seconds 3

$svc = Get-Service ScaleStreamerService
Write-Host "Service status: $($svc.Status)" -ForegroundColor $(if ($svc.Status -eq 'Running') { 'Green' } else { 'Red' })

# Open firewall for ONVIF
Write-Host "Opening firewall ports..." -ForegroundColor Yellow
netsh advfirewall firewall delete rule name="ScaleStreamer ONVIF" 2>&1 | Out-Null
netsh advfirewall firewall add rule name="ScaleStreamer ONVIF" dir=in action=allow protocol=tcp localport=8080 | Out-Null
netsh advfirewall firewall delete rule name="ScaleStreamer WS-Discovery" 2>&1 | Out-Null
netsh advfirewall firewall add rule name="ScaleStreamer WS-Discovery" dir=in action=allow protocol=udp localport=3702 | Out-Null
Write-Host "Firewall rules added for ONVIF (TCP 8080) and WS-Discovery (UDP 3702)" -ForegroundColor Green

Write-Host "`nDone! Service deployed with ONVIF Profile S support." -ForegroundColor Green
