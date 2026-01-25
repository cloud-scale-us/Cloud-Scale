# Scale Streamer v2.0 Installer Build Script
# Builds MSI installer using WiX Toolset v4

param(
    [string]$Configuration = "Release",
    [switch]$SkipBuild = $false
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Scale Streamer v2.0 Installer Build" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Paths
$RootDir = Split-Path $PSScriptRoot -Parent
$InstallerDir = $PSScriptRoot
$ServiceProjectDir = Join-Path $RootDir "src-v2\ScaleStreamer.Service"
$ConfigProjectDir = Join-Path $RootDir "src-v2\ScaleStreamer.Config"
$ServicePublishDir = Join-Path $ServiceProjectDir "bin\$Configuration\net8.0-windows\publish"
$ConfigPublishDir = Join-Path $ConfigProjectDir "bin\$Configuration\net8.0-windows\publish"
$OutputDir = Join-Path $InstallerDir "bin"

# Create output directory
if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir | Out-Null
}

# Step 1: Build and publish projects
if (-not $SkipBuild) {
    Write-Host "[1/5] Building Service project..." -ForegroundColor Yellow

    dotnet publish $ServiceProjectDir `
        -c $Configuration `
        -o $ServicePublishDir `
        --self-contained false `
        -p:PublishSingleFile=false `
        -p:PublishReadyToRun=false `
        -p:CopyOutputSymbolsToPublishDirectory=false

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Service build failed!"
        exit 1
    }

    Write-Host "[2/5] Building Configuration GUI project..." -ForegroundColor Yellow

    dotnet publish $ConfigProjectDir `
        -c $Configuration `
        -o $ConfigPublishDir `
        --self-contained false `
        -p:PublishSingleFile=false `
        -p:PublishReadyToRun=false `
        -p:CopyOutputSymbolsToPublishDirectory=false

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Config GUI build failed!"
        exit 1
    }

    Write-Host "Build complete!" -ForegroundColor Green
    Write-Host ""
} else {
    Write-Host "Skipping build (using existing binaries)" -ForegroundColor Yellow
    Write-Host ""
}

# Step 2: Verify WiX Toolset
Write-Host "[3/5] Verifying WiX Toolset..." -ForegroundColor Yellow

$wixPath = Get-Command wix -ErrorAction SilentlyContinue

if (-not $wixPath) {
    Write-Error @"
WiX Toolset v4 not found!

Please install WiX Toolset v4:
    dotnet tool install --global wix

Or update:
    dotnet tool update --global wix

Documentation: https://wixtoolset.org/
"@
    exit 1
}

Write-Host "WiX Toolset found: $($wixPath.Source)" -ForegroundColor Green
Write-Host ""

# Step 3: Create license file if it doesn't exist
$licenseFile = Join-Path $InstallerDir "license.rtf"
if (-not (Test-Path $licenseFile)) {
    Write-Host "[4/5] Creating license file..." -ForegroundColor Yellow

    $licenseContent = @"
{\rtf1\ansi\deff0
{\fonttbl{\f0\fnil\fcharset0 Courier New;}}
\viewkind4\uc1\pard\lang1033\f0\fs20

Scale Streamer v2.0 - End User License Agreement

Copyright (c) 2026 Cloud-Scale IoT Solutions

Permission is hereby granted to use this software for commercial and
non-commercial purposes subject to the following conditions:

1. This software is provided "as is" without warranty of any kind.

2. Cloud-Scale shall not be liable for any damages arising from the
   use of this software.

3. Redistribution of this software requires written permission from
   Cloud-Scale.

For licensing inquiries, contact: admin@cloud-scale.us

}
"@

    Set-Content -Path $licenseFile -Value $licenseContent -Encoding ASCII
    Write-Host "License file created" -ForegroundColor Green
} else {
    Write-Host "[4/5] License file exists" -ForegroundColor Green
}
Write-Host ""

# Step 4: Build installer
Write-Host "[5/5] Building MSI installer..." -ForegroundColor Yellow

$wixFile = Join-Path $InstallerDir "ScaleStreamerV2.wxs"
$outputMsi = Join-Path $OutputDir "ScaleStreamer-v2.0.0.msi"

# Build with WiX
# Install extensions if not already present (WiX v4.0.5 requires v4.0.5 extensions)
& wix extension add -g WixToolset.UI.wixext/4.0.5 2>$null
& wix extension add -g WixToolset.Util.wixext/4.0.5 2>$null

wix build $wixFile `
    -o $outputMsi `
    -ext WixToolset.UI.wixext/4.0.5 `
    -ext WixToolset.Util.wixext/4.0.5 `
    -d ServicePublishDir=$ServicePublishDir `
    -d ConfigPublishDir=$ConfigPublishDir `
    -arch x64

if ($LASTEXITCODE -ne 0) {
    Write-Error "WiX build failed!"
    exit 1
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "BUILD SUCCESSFUL!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Installer created at:" -ForegroundColor Cyan
Write-Host "  $outputMsi" -ForegroundColor White
Write-Host ""
Write-Host "File size: $((Get-Item $outputMsi).Length / 1MB) MB" -ForegroundColor Cyan
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  1. Test installation: " -ForegroundColor Gray -NoNewline
Write-Host "msiexec /i `"$outputMsi`" /l*v install.log" -ForegroundColor White
Write-Host "  2. Silent install: " -ForegroundColor Gray -NoNewline
Write-Host "msiexec /i `"$outputMsi`" /quiet" -ForegroundColor White
Write-Host "  3. Uninstall: " -ForegroundColor Gray -NoNewline
Write-Host "msiexec /x `"$outputMsi`" /quiet" -ForegroundColor White
Write-Host ""
