# Download FFmpeg
$ffmpegDir = "C:\Users\Windfield\Cloud-Scale\ffmpeg"
if (-not (Test-Path $ffmpegDir)) {
    New-Item -ItemType Directory -Path $ffmpegDir -Force | Out-Null
}

$ffmpegZip = "$ffmpegDir\ffmpeg.zip"
$ffmpegUrl = "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip"

Write-Host "Downloading FFmpeg..." -ForegroundColor Yellow
Invoke-WebRequest -Uri $ffmpegUrl -OutFile $ffmpegZip -UseBasicParsing

Write-Host "Extracting FFmpeg..." -ForegroundColor Yellow
Expand-Archive -Path $ffmpegZip -DestinationPath $ffmpegDir -Force

# Find and display the bin folder
$binPath = Get-ChildItem -Path $ffmpegDir -Recurse -Filter "ffmpeg.exe" | Select-Object -First 1 -ExpandProperty DirectoryName
Write-Host "FFmpeg installed at: $binPath" -ForegroundColor Green

# Test ffmpeg
& "$binPath\ffmpeg.exe" -version | Select-Object -First 2
