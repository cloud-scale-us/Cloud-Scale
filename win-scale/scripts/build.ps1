<#
.SYNOPSIS
    Builds the Scale Streamer application.
.DESCRIPTION
    This script:
    1. Downloads dependencies (ffmpeg, MediaMTX)
    2. Builds the .NET application
    3. Optionally creates an MSI installer
#>

param(
    [switch]$Release,
    [switch]$CreateInstaller,
    [switch]$SkipDeps
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path $PSScriptRoot -Parent
$SrcDir = Join-Path $ProjectRoot "src\ScaleStreamer"
$OutputDir = Join-Path $ProjectRoot "publish"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Scale Streamer Build Script" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

# Step 1: Download dependencies
if (-not $SkipDeps) {
    Write-Host "`n[1/3] Downloading dependencies..." -ForegroundColor Yellow
    & (Join-Path $PSScriptRoot "download-deps.ps1")
}

# Step 2: Build the application
Write-Host "`n[2/3] Building application..." -ForegroundColor Yellow

$config = if ($Release) { "Release" } else { "Debug" }
$publishDir = Join-Path $OutputDir $config

Push-Location $SrcDir
try {
    dotnet restore
    dotnet publish -c $config -r win-x64 --self-contained true -o $publishDir

    # Copy dependencies to publish folder
    $depsDir = Join-Path $ProjectRoot "deps"
    $publishDepsDir = Join-Path $publishDir "deps"

    if (Test-Path $depsDir) {
        Write-Host "Copying dependencies to publish folder..." -ForegroundColor Yellow
        Copy-Item -Path $depsDir -Destination $publishDepsDir -Recurse -Force
    }

    Write-Host "Build completed successfully!" -ForegroundColor Green
    Write-Host "Output: $publishDir"
} finally {
    Pop-Location
}

# Step 3: Create installer (optional)
if ($CreateInstaller) {
    Write-Host "`n[3/3] Creating installer..." -ForegroundColor Yellow

    $wixDir = Join-Path $ProjectRoot "installer"
    $msiOutput = Join-Path $OutputDir "ScaleStreamer-Setup.msi"

    # Check if WiX is installed
    $wixPath = Get-Command "wix" -ErrorAction SilentlyContinue

    if ($wixPath) {
        Push-Location $wixDir
        try {
            wix build ScaleStreamer.wxs -o $msiOutput -d PublishDir=$publishDir
            Write-Host "Installer created: $msiOutput" -ForegroundColor Green
        } finally {
            Pop-Location
        }
    } else {
        Write-Host "WiX Toolset not found. Install with: dotnet tool install -g wix" -ForegroundColor Yellow
        Write-Host "Skipping installer creation." -ForegroundColor Yellow
    }
}

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  Build Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "To run the application:" -ForegroundColor White
Write-Host "  cd $publishDir" -ForegroundColor Gray
Write-Host "  .\ScaleStreamer.exe" -ForegroundColor Gray
