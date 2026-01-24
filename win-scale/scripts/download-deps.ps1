<#
.SYNOPSIS
    Downloads ffmpeg and MediaMTX dependencies for Scale Streamer.
.DESCRIPTION
    This script downloads and extracts:
    - ffmpeg (Windows build from gyan.dev)
    - MediaMTX RTSP server (from GitHub releases)
#>

param(
    [string]$TargetDir = (Join-Path $PSScriptRoot "..\deps")
)

$ErrorActionPreference = "Stop"

# Versions
$ffmpegVersion = "7.0"
$mediamtxVersion = "1.9.3"

# URLs
$ffmpegUrl = "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip"
$mediamtxUrl = "https://github.com/bluenviron/mediamtx/releases/download/v$mediamtxVersion/mediamtx_v${mediamtxVersion}_windows_amd64.zip"

# Create directories
$ffmpegDir = Join-Path $TargetDir "ffmpeg"
$mediamtxDir = Join-Path $TargetDir "mediamtx"
$tempDir = Join-Path $env:TEMP "scale-streamer-deps"

New-Item -ItemType Directory -Force -Path $ffmpegDir | Out-Null
New-Item -ItemType Directory -Force -Path $mediamtxDir | Out-Null
New-Item -ItemType Directory -Force -Path $tempDir | Out-Null

Write-Host "Downloading dependencies to $TargetDir..." -ForegroundColor Cyan

# Download ffmpeg
$ffmpegZip = Join-Path $tempDir "ffmpeg.zip"
if (-not (Test-Path (Join-Path $ffmpegDir "ffmpeg.exe"))) {
    Write-Host "Downloading ffmpeg..." -ForegroundColor Yellow
    Invoke-WebRequest -Uri $ffmpegUrl -OutFile $ffmpegZip -UseBasicParsing

    Write-Host "Extracting ffmpeg..." -ForegroundColor Yellow
    $extractDir = Join-Path $tempDir "ffmpeg-extract"
    Expand-Archive -Path $ffmpegZip -DestinationPath $extractDir -Force

    # Find the bin folder (it's in a versioned subdirectory)
    $binDir = Get-ChildItem -Path $extractDir -Recurse -Directory -Filter "bin" | Select-Object -First 1

    if ($binDir) {
        Copy-Item -Path (Join-Path $binDir.FullName "ffmpeg.exe") -Destination $ffmpegDir -Force
        Copy-Item -Path (Join-Path $binDir.FullName "ffplay.exe") -Destination $ffmpegDir -Force
        Copy-Item -Path (Join-Path $binDir.FullName "ffprobe.exe") -Destination $ffmpegDir -Force
        Write-Host "ffmpeg installed successfully." -ForegroundColor Green
    } else {
        Write-Error "Could not find ffmpeg binaries in extracted archive."
    }
} else {
    Write-Host "ffmpeg already exists, skipping." -ForegroundColor Gray
}

# Download MediaMTX
$mediamtxZip = Join-Path $tempDir "mediamtx.zip"
if (-not (Test-Path (Join-Path $mediamtxDir "mediamtx.exe"))) {
    Write-Host "Downloading MediaMTX v$mediamtxVersion..." -ForegroundColor Yellow
    Invoke-WebRequest -Uri $mediamtxUrl -OutFile $mediamtxZip -UseBasicParsing

    Write-Host "Extracting MediaMTX..." -ForegroundColor Yellow
    Expand-Archive -Path $mediamtxZip -DestinationPath $mediamtxDir -Force

    Write-Host "MediaMTX installed successfully." -ForegroundColor Green
} else {
    Write-Host "MediaMTX already exists, skipping." -ForegroundColor Gray
}

# Cleanup temp files
Remove-Item -Path $tempDir -Recurse -Force -ErrorAction SilentlyContinue

Write-Host ""
Write-Host "Dependencies downloaded successfully!" -ForegroundColor Green
Write-Host "ffmpeg: $ffmpegDir"
Write-Host "MediaMTX: $mediamtxDir"
