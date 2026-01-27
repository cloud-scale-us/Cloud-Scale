# Download and setup MediaMTX
$url = 'https://github.com/bluenviron/mediamtx/releases/download/v1.5.1/mediamtx_v1.5.1_windows_amd64.zip'
$outPath = 'C:\Users\Windfield\Cloud-Scale\mediamtx.zip'
$extractPath = 'C:\Users\Windfield\Cloud-Scale\mediamtx'

Write-Host "Downloading MediaMTX..." -ForegroundColor Yellow
Invoke-WebRequest -Uri $url -OutFile $outPath

Write-Host "Extracting..." -ForegroundColor Yellow
if (Test-Path $extractPath) { Remove-Item $extractPath -Recurse -Force }
Expand-Archive -Path $outPath -DestinationPath $extractPath -Force

Write-Host "Done! MediaMTX installed at: $extractPath" -ForegroundColor Green
Get-ChildItem $extractPath
