<#
.SYNOPSIS
    Complete automated build script for Scale RTSP Streamer.
.DESCRIPTION
    This script performs a complete build including:
    1. Downloads all dependencies (ffmpeg, MediaMTX)
    2. Builds the .NET application
    3. Creates the MSI installer

    Run this script to produce a ready-to-distribute installer.
.EXAMPLE
    .\build-installer.ps1
    .\build-installer.ps1 -Version "1.0.1"
    .\build-installer.ps1 -SkipDeps
#>

param(
    [string]$Version = "1.0.0",
    [switch]$SkipDeps,
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"
$ProjectRoot = $PSScriptRoot

# Versions for dependencies
$ffmpegVersion = "7.0"
$mediamtxVersion = "1.9.3"

Write-Host ""
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "  Scale RTSP Streamer - Automated Installer Build" -ForegroundColor Cyan
Write-Host "  Version: $Version" -ForegroundColor Cyan
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host ""

# ============================================================================
# STEP 1: Check Prerequisites
# ============================================================================
Write-Host "[1/5] Checking prerequisites..." -ForegroundColor Yellow

# Check .NET SDK
$dotnet = Get-Command "dotnet" -ErrorAction SilentlyContinue
if (-not $dotnet) {
    Write-Host "ERROR: .NET SDK not found!" -ForegroundColor Red
    Write-Host "Download from: https://dotnet.microsoft.com/download/dotnet/8.0" -ForegroundColor Yellow
    exit 1
}
$dotnetVersion = dotnet --version
Write-Host "  .NET SDK: $dotnetVersion" -ForegroundColor Green

# Check/Install WiX
$wix = Get-Command "wix" -ErrorAction SilentlyContinue
if (-not $wix) {
    Write-Host "  Installing WiX Toolset..." -ForegroundColor Gray
    dotnet tool install -g wix
    $env:Path = [System.Environment]::GetEnvironmentVariable("Path","Machine") + ";" + [System.Environment]::GetEnvironmentVariable("Path","User")
    $wix = Get-Command "wix" -ErrorAction SilentlyContinue
    if (-not $wix) {
        Write-Host "ERROR: Failed to install WiX Toolset" -ForegroundColor Red
        exit 1
    }
}
Write-Host "  WiX Toolset: Installed" -ForegroundColor Green

# Add WiX UI extension if needed
Write-Host "  Ensuring WiX UI extension..." -ForegroundColor Gray
wix extension add WixToolset.UI.wixext -g 2>$null

Write-Host ""

# ============================================================================
# STEP 2: Download Dependencies
# ============================================================================
if (-not $SkipDeps) {
    Write-Host "[2/5] Downloading dependencies..." -ForegroundColor Yellow

    $depsDir = Join-Path $ProjectRoot "deps"
    $ffmpegDir = Join-Path $depsDir "ffmpeg"
    $mediamtxDir = Join-Path $depsDir "mediamtx"
    $tempDir = Join-Path $env:TEMP "scale-build-temp"

    New-Item -ItemType Directory -Force -Path $ffmpegDir | Out-Null
    New-Item -ItemType Directory -Force -Path $mediamtxDir | Out-Null
    New-Item -ItemType Directory -Force -Path $tempDir | Out-Null

    # Download ffmpeg
    $ffmpegExe = Join-Path $ffmpegDir "ffmpeg.exe"
    if (-not (Test-Path $ffmpegExe)) {
        Write-Host "  Downloading ffmpeg..." -ForegroundColor Gray
        $ffmpegUrl = "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip"
        $ffmpegZip = Join-Path $tempDir "ffmpeg.zip"

        try {
            [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
            Invoke-WebRequest -Uri $ffmpegUrl -OutFile $ffmpegZip -UseBasicParsing

            $extractDir = Join-Path $tempDir "ffmpeg-extract"
            Expand-Archive -Path $ffmpegZip -DestinationPath $extractDir -Force

            $binDir = Get-ChildItem -Path $extractDir -Recurse -Directory -Filter "bin" | Select-Object -First 1
            if ($binDir) {
                Copy-Item -Path (Join-Path $binDir.FullName "ffmpeg.exe") -Destination $ffmpegDir -Force
                Copy-Item -Path (Join-Path $binDir.FullName "ffplay.exe") -Destination $ffmpegDir -Force
                Write-Host "  ffmpeg: Downloaded" -ForegroundColor Green
            }
        } catch {
            Write-Host "  ERROR: Failed to download ffmpeg: $_" -ForegroundColor Red
            exit 1
        }
    } else {
        Write-Host "  ffmpeg: Already exists" -ForegroundColor Gray
    }

    # Download MediaMTX
    $mediamtxExe = Join-Path $mediamtxDir "mediamtx.exe"
    if (-not (Test-Path $mediamtxExe)) {
        Write-Host "  Downloading MediaMTX v$mediamtxVersion..." -ForegroundColor Gray
        $mediamtxUrl = "https://github.com/bluenviron/mediamtx/releases/download/v$mediamtxVersion/mediamtx_v${mediamtxVersion}_windows_amd64.zip"
        $mediamtxZip = Join-Path $tempDir "mediamtx.zip"

        try {
            Invoke-WebRequest -Uri $mediamtxUrl -OutFile $mediamtxZip -UseBasicParsing
            Expand-Archive -Path $mediamtxZip -DestinationPath $mediamtxDir -Force
            Write-Host "  MediaMTX: Downloaded" -ForegroundColor Green
        } catch {
            Write-Host "  ERROR: Failed to download MediaMTX: $_" -ForegroundColor Red
            exit 1
        }
    } else {
        Write-Host "  MediaMTX: Already exists" -ForegroundColor Gray
    }

    # Cleanup temp
    Remove-Item -Path $tempDir -Recurse -Force -ErrorAction SilentlyContinue
} else {
    Write-Host "[2/5] Skipping dependency download" -ForegroundColor Gray
}

Write-Host ""

# ============================================================================
# STEP 3: Build Application
# ============================================================================
if (-not $SkipBuild) {
    Write-Host "[3/5] Building application..." -ForegroundColor Yellow

    $srcDir = Join-Path $ProjectRoot "src\ScaleStreamer"
    $publishDir = Join-Path $ProjectRoot "publish\Release"

    Push-Location $srcDir
    try {
        Write-Host "  Restoring NuGet packages..." -ForegroundColor Gray
        dotnet restore --verbosity quiet

        Write-Host "  Building Release for Windows x64..." -ForegroundColor Gray
        dotnet publish -c Release -r win-x64 --self-contained true -o $publishDir -p:Version=$Version --verbosity quiet

        # Copy dependencies to publish folder
        $depsDir = Join-Path $ProjectRoot "deps"
        if (Test-Path $depsDir) {
            Write-Host "  Copying dependencies to output..." -ForegroundColor Gray
            $publishDepsDir = Join-Path $publishDir "deps"
            if (Test-Path $publishDepsDir) {
                Remove-Item -Path $publishDepsDir -Recurse -Force
            }
            Copy-Item -Path $depsDir -Destination $publishDepsDir -Recurse -Force
        }

        # Copy default config
        $configFile = Join-Path $ProjectRoot "appsettings.json"
        if (Test-Path $configFile) {
            Copy-Item -Path $configFile -Destination $publishDir -Force
        }

        Write-Host "  Build: Complete" -ForegroundColor Green
    } finally {
        Pop-Location
    }
} else {
    Write-Host "[3/5] Skipping build" -ForegroundColor Gray
}

Write-Host ""

# ============================================================================
# STEP 4: Update Installer Version
# ============================================================================
Write-Host "[4/5] Preparing installer..." -ForegroundColor Yellow

$installerDir = Join-Path $ProjectRoot "installer"
$wxsFile = Join-Path $installerDir "ScaleStreamer.wxs"

# Update version in WXS file
$wxsContent = Get-Content $wxsFile -Raw
$wxsContent = $wxsContent -replace 'Version="[0-9]+\.[0-9]+\.[0-9]+"', "Version=`"$Version`""
Set-Content -Path $wxsFile -Value $wxsContent

Write-Host "  Version set to: $Version" -ForegroundColor Green

Write-Host ""

# ============================================================================
# STEP 5: Build MSI Installer
# ============================================================================
Write-Host "[5/5] Building MSI installer..." -ForegroundColor Yellow

$publishDir = Join-Path $ProjectRoot "publish\Release"
$outputDir = Join-Path $ProjectRoot "publish"
$msiOutput = Join-Path $outputDir "ScaleStreamer-$Version-Setup.msi"

Push-Location $installerDir
try {
    Write-Host "  Compiling installer..." -ForegroundColor Gray

    # Build with WiX v4
    wix build ScaleStreamer.wxs `
        -ext WixToolset.UI.wixext `
        -d PublishDir="$publishDir" `
        -o "$msiOutput"

    if (Test-Path $msiOutput) {
        $msiSize = [math]::Round((Get-Item $msiOutput).Length / 1MB, 2)
        Write-Host "  MSI Created: $msiOutput ($msiSize MB)" -ForegroundColor Green
    } else {
        Write-Host "  ERROR: MSI was not created" -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Host "  ERROR: Failed to build MSI: $_" -ForegroundColor Red
    exit 1
} finally {
    Pop-Location
}

Write-Host ""

# ============================================================================
# DONE
# ============================================================================
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "  BUILD COMPLETE!" -ForegroundColor Green
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Output files:" -ForegroundColor White
Write-Host "  Installer: $msiOutput" -ForegroundColor Gray
Write-Host "  App folder: $publishDir" -ForegroundColor Gray
Write-Host ""
Write-Host "The MSI installer includes:" -ForegroundColor White
Write-Host "  - Scale RTSP Streamer application" -ForegroundColor Gray
Write-Host "  - FFmpeg video encoder" -ForegroundColor Gray
Write-Host "  - MediaMTX RTSP server" -ForegroundColor Gray
Write-Host "  - .NET 8.0 runtime (self-contained)" -ForegroundColor Gray
Write-Host ""
Write-Host "Installation will show:" -ForegroundColor White
Write-Host "  1. Welcome screen" -ForegroundColor Gray
Write-Host "  2. License agreement (must accept)" -ForegroundColor Gray
Write-Host "  3. Installation folder selection" -ForegroundColor Gray
Write-Host "  4. Install progress" -ForegroundColor Gray
Write-Host "  5. Completion" -ForegroundColor Gray
Write-Host ""
