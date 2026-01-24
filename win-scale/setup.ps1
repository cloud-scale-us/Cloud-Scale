<#
.SYNOPSIS
    One-click setup script for Scale RTSP Streamer.
.DESCRIPTION
    This script automatically:
    1. Checks for .NET SDK
    2. Downloads all dependencies (ffmpeg, MediaMTX)
    3. Builds the application
    4. Creates the MSI installer (if WiX is available)
    5. Launches the application

    Run this script once to set up everything!
.EXAMPLE
    .\setup.ps1
    .\setup.ps1 -SkipRun
    .\setup.ps1 -CreateInstaller
#>

param(
    [switch]$SkipRun,          # Don't launch the app after building
    [switch]$CreateInstaller,  # Also create MSI installer
    [switch]$Force             # Force re-download of dependencies
)

$ErrorActionPreference = "Stop"
$ProjectRoot = $PSScriptRoot

Write-Host ""
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host "  Scale RTSP Streamer - Automatic Setup" -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host ""

# Check for .NET SDK
Write-Host "[1/4] Checking .NET SDK..." -ForegroundColor Yellow
$dotnet = Get-Command "dotnet" -ErrorAction SilentlyContinue

if (-not $dotnet) {
    Write-Host "ERROR: .NET SDK not found!" -ForegroundColor Red
    Write-Host "Please install .NET 8.0 SDK from: https://dotnet.microsoft.com/download/dotnet/8.0" -ForegroundColor Yellow
    exit 1
}

$dotnetVersion = dotnet --version
Write-Host "  Found .NET SDK: $dotnetVersion" -ForegroundColor Green

# Download dependencies
Write-Host ""
Write-Host "[2/4] Downloading dependencies..." -ForegroundColor Yellow

$depsDir = Join-Path $ProjectRoot "deps"
$ffmpegDir = Join-Path $depsDir "ffmpeg"
$mediamtxDir = Join-Path $depsDir "mediamtx"

# ffmpeg
$ffmpegExe = Join-Path $ffmpegDir "ffmpeg.exe"
if ($Force -or -not (Test-Path $ffmpegExe)) {
    Write-Host "  Downloading ffmpeg..." -ForegroundColor Gray

    New-Item -ItemType Directory -Force -Path $ffmpegDir | Out-Null
    $ffmpegUrl = "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip"
    $ffmpegZip = Join-Path $env:TEMP "ffmpeg.zip"

    try {
        Invoke-WebRequest -Uri $ffmpegUrl -OutFile $ffmpegZip -UseBasicParsing

        $extractDir = Join-Path $env:TEMP "ffmpeg-extract"
        Remove-Item -Path $extractDir -Recurse -Force -ErrorAction SilentlyContinue
        Expand-Archive -Path $ffmpegZip -DestinationPath $extractDir -Force

        $binDir = Get-ChildItem -Path $extractDir -Recurse -Directory -Filter "bin" | Select-Object -First 1
        if ($binDir) {
            Copy-Item -Path (Join-Path $binDir.FullName "ffmpeg.exe") -Destination $ffmpegDir -Force
            Copy-Item -Path (Join-Path $binDir.FullName "ffplay.exe") -Destination $ffmpegDir -Force
            Write-Host "  ffmpeg downloaded successfully." -ForegroundColor Green
        }

        Remove-Item -Path $ffmpegZip -Force -ErrorAction SilentlyContinue
        Remove-Item -Path $extractDir -Recurse -Force -ErrorAction SilentlyContinue
    } catch {
        Write-Host "  WARNING: Failed to download ffmpeg: $_" -ForegroundColor Yellow
        Write-Host "  You may need to download it manually from https://www.gyan.dev/ffmpeg/builds/" -ForegroundColor Yellow
    }
} else {
    Write-Host "  ffmpeg already exists." -ForegroundColor Gray
}

# MediaMTX
$mediamtxExe = Join-Path $mediamtxDir "mediamtx.exe"
if ($Force -or -not (Test-Path $mediamtxExe)) {
    Write-Host "  Downloading MediaMTX..." -ForegroundColor Gray

    New-Item -ItemType Directory -Force -Path $mediamtxDir | Out-Null
    $mediamtxVersion = "1.9.3"
    $mediamtxUrl = "https://github.com/bluenviron/mediamtx/releases/download/v$mediamtxVersion/mediamtx_v${mediamtxVersion}_windows_amd64.zip"
    $mediamtxZip = Join-Path $env:TEMP "mediamtx.zip"

    try {
        Invoke-WebRequest -Uri $mediamtxUrl -OutFile $mediamtxZip -UseBasicParsing
        Expand-Archive -Path $mediamtxZip -DestinationPath $mediamtxDir -Force
        Write-Host "  MediaMTX downloaded successfully." -ForegroundColor Green

        Remove-Item -Path $mediamtxZip -Force -ErrorAction SilentlyContinue
    } catch {
        Write-Host "  WARNING: Failed to download MediaMTX: $_" -ForegroundColor Yellow
        Write-Host "  You may need to download it manually from https://github.com/bluenviron/mediamtx/releases" -ForegroundColor Yellow
    }
} else {
    Write-Host "  MediaMTX already exists." -ForegroundColor Gray
}

# Build the application
Write-Host ""
Write-Host "[3/4] Building application..." -ForegroundColor Yellow

$srcDir = Join-Path $ProjectRoot "src\ScaleStreamer"
$publishDir = Join-Path $ProjectRoot "publish\Release"

Push-Location $srcDir
try {
    Write-Host "  Restoring packages..." -ForegroundColor Gray
    dotnet restore --verbosity quiet

    Write-Host "  Publishing for Windows x64..." -ForegroundColor Gray
    dotnet publish -c Release -r win-x64 --self-contained true -o $publishDir --verbosity quiet

    # Copy dependencies
    if (Test-Path $depsDir) {
        $publishDepsDir = Join-Path $publishDir "deps"
        Copy-Item -Path $depsDir -Destination $publishDepsDir -Recurse -Force
    }

    Write-Host "  Build completed successfully!" -ForegroundColor Green
} finally {
    Pop-Location
}

# Create installer (optional)
if ($CreateInstaller) {
    Write-Host ""
    Write-Host "[3.5] Creating MSI installer..." -ForegroundColor Yellow

    $wixPath = Get-Command "wix" -ErrorAction SilentlyContinue
    if ($wixPath) {
        $installerDir = Join-Path $ProjectRoot "installer"
        $msiOutput = Join-Path $ProjectRoot "publish\ScaleStreamer-Setup.msi"

        Push-Location $installerDir
        try {
            wix build ScaleStreamer.wxs -o $msiOutput -d PublishDir=$publishDir
            Write-Host "  Installer created: $msiOutput" -ForegroundColor Green
        } catch {
            Write-Host "  WARNING: Failed to create installer: $_" -ForegroundColor Yellow
        } finally {
            Pop-Location
        }
    } else {
        Write-Host "  WiX Toolset not found. Install with: dotnet tool install -g wix" -ForegroundColor Yellow
    }
}

# Done!
Write-Host ""
Write-Host "[4/4] Setup complete!" -ForegroundColor Yellow
Write-Host ""
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host "  Setup Complete!" -ForegroundColor Green
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Output folder: $publishDir" -ForegroundColor White
Write-Host ""
Write-Host "To run manually:" -ForegroundColor White
Write-Host "  cd `"$publishDir`"" -ForegroundColor Gray
Write-Host "  .\ScaleStreamer.exe" -ForegroundColor Gray
Write-Host ""

# Launch the application
if (-not $SkipRun) {
    $exePath = Join-Path $publishDir "ScaleStreamer.exe"
    if (Test-Path $exePath) {
        Write-Host "Launching Scale Streamer..." -ForegroundColor Green
        Start-Process -FilePath $exePath -WorkingDirectory $publishDir
    }
}
