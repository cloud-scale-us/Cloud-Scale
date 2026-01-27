# Deploy IPC stability fix - requires admin
$ErrorActionPreference = 'Continue'

# Stop service
Write-Host 'Stopping service...' -ForegroundColor Yellow
net stop ScaleStreamerService 2>&1 | Out-Null
Start-Sleep -Seconds 3

# Kill Config
Get-Process ScaleStreamer.Config -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 2

# Deploy Service
$svcPub = 'C:\Users\Windfield\Cloud-Scale\win-scale\src-v2\ScaleStreamer.Service\bin\Release\net8.0\win-x64\publish'
$svcDir = 'C:\Program Files\Scale Streamer\Service'
Write-Host "Deploying Service from $svcPub..." -ForegroundColor Yellow
Get-ChildItem $svcPub -Recurse | ForEach-Object {
    $dest = $_.FullName.Replace($svcPub, $svcDir)
    if ($_.PSIsContainer) {
        New-Item -ItemType Directory -Path $dest -Force -ErrorAction SilentlyContinue | Out-Null
    } else {
        if (Test-Path $dest) { Set-ItemProperty $dest -Name IsReadOnly -Value $false -ErrorAction SilentlyContinue }
        Copy-Item $_.FullName $dest -Force -ErrorAction SilentlyContinue
    }
}
Write-Host 'Service deployed.' -ForegroundColor Green

# Deploy Config
$cfgPub = 'C:\Users\Windfield\Cloud-Scale\win-scale\src-v2\ScaleStreamer.Config\bin\Release\net8.0-windows\win-x64\publish'
$cfgDir = 'C:\Program Files\Scale Streamer\Config'
Write-Host "Deploying Config from $cfgPub..." -ForegroundColor Yellow
Get-ChildItem $cfgPub -Recurse | ForEach-Object {
    $dest = $_.FullName.Replace($cfgPub, $cfgDir)
    if ($_.PSIsContainer) {
        New-Item -ItemType Directory -Path $dest -Force -ErrorAction SilentlyContinue | Out-Null
    } else {
        if (Test-Path $dest) { Set-ItemProperty $dest -Name IsReadOnly -Value $false -ErrorAction SilentlyContinue }
        Copy-Item $_.FullName $dest -Force -ErrorAction SilentlyContinue
    }
}
Write-Host 'Config deployed.' -ForegroundColor Green

# Start service
Write-Host 'Starting service...' -ForegroundColor Yellow
net start ScaleStreamerService
Start-Sleep -Seconds 2
Get-Service ScaleStreamerService | Select-Object Name, Status

# Launch Config
Start-Process "$cfgDir\ScaleStreamer.Config.exe"
Write-Host 'Config launched.' -ForegroundColor Green
Write-Host 'IPC stability fix deployed!' -ForegroundColor Cyan
