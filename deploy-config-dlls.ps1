# Deploy Config from self-contained publish output
$publishDir = 'C:\Users\Windfield\Cloud-Scale\win-scale\src-v2\ScaleStreamer.Config\bin\Release\net8.0-windows\win-x64\publish'
$cfgDir = 'C:\Program Files\Scale Streamer\Config'

# Kill Config app if running
Get-Process ScaleStreamer.Config -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 1

# Copy all files recursively, clearing read-only attributes
Get-ChildItem $publishDir -Recurse | ForEach-Object {
    $dest = $_.FullName.Replace($publishDir, $cfgDir)
    if ($_.PSIsContainer) {
        New-Item -ItemType Directory -Path $dest -Force -ErrorAction SilentlyContinue | Out-Null
    } else {
        if (Test-Path $dest) {
            Set-ItemProperty $dest -Name IsReadOnly -Value $false -ErrorAction SilentlyContinue
        }
        Copy-Item $_.FullName $dest -Force
    }
}

Write-Host "All Config files deployed from self-contained publish" -ForegroundColor Green

# Relaunch Config
Start-Process "$cfgDir\ScaleStreamer.Config.exe"
Write-Host "Config app relaunched." -ForegroundColor Green
