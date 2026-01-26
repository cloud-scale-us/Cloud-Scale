# Deploy full self-contained Config app - Run as Administrator
# This replaces all files in the Config folder with the new self-contained build

Write-Host "Deploying full self-contained Config app..." -ForegroundColor Cyan

# Kill any running Config app
Write-Host "Closing Config app if running..." -ForegroundColor Yellow
Get-Process -Name "ScaleStreamer.Config" -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 2

# Copy all files from publish to installed location
Write-Host "Copying all files from publish folder..." -ForegroundColor Yellow
$source = 'C:\Users\Windfield\Cloud-Scale\win-scale\src-v2\ScaleStreamer.Config\bin\Publish\*'
$dest = 'C:\Program Files\Scale Streamer\Config\'

# Count files
$fileCount = (Get-ChildItem $source -File).Count
Write-Host "Copying $fileCount files..." -ForegroundColor Yellow

Copy-Item $source $dest -Recurse -Force

Write-Host ""
Write-Host "Done! Starting Config app..." -ForegroundColor Green
Start-Process 'C:\Program Files\Scale Streamer\Config\ScaleStreamer.Config.exe'
