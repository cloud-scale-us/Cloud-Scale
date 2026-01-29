# Scale Streamer v2.0 Self-Contained Installer Build Script
# Builds MSI installer with all .NET runtime dependencies included

param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

# Ensure tools are in PATH (check multiple locations)
$dotnetPaths = @("C:\Program Files\dotnet", "C:\dotnet", "$env:USERPROFILE\.dotnet")
$toolsPaths = @("$env:USERPROFILE\.dotnet\tools", "C:\Users\Charlie\.dotnet\tools")
foreach ($path in $dotnetPaths) {
    if (Test-Path "$path\dotnet.exe") {
        $env:Path = "$path;$env:Path"
        break
    }
}
foreach ($path in $toolsPaths) {
    if (Test-Path $path) {
        $env:Path = "$path;$env:Path"
        break
    }
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Scale Streamer v2.0 Self-Contained Installer" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Paths
$RootDir = Split-Path $PSScriptRoot -Parent
$InstallerDir = $PSScriptRoot
$ServiceProjectDir = Join-Path $RootDir "src-v2\ScaleStreamer.Service"
$ConfigProjectDir = Join-Path $RootDir "src-v2\ScaleStreamer.Config"
$LauncherProjectDir = Join-Path $RootDir "src-v2\ScaleStreamer.Launcher"
# Service targets net8.0 (not net8.0-windows), Config and Launcher target net8.0-windows
$ServicePublishDir = Join-Path $ServiceProjectDir "bin\$Configuration\net8.0\win-x64\publish"
$ConfigPublishDir = Join-Path $ConfigProjectDir "bin\$Configuration\net8.0-windows\win-x64\publish"
$LauncherPublishDir = Join-Path $LauncherProjectDir "bin\$Configuration\net8.0-windows\win-x64\publish"
$OutputDir = Join-Path $InstallerDir "bin"

# Create output directory
if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir | Out-Null
}

Write-Host "[1/4] Using self-contained binaries:" -ForegroundColor Yellow
Write-Host "  Service:  $ServicePublishDir" -ForegroundColor White
Write-Host "  Config:   $ConfigPublishDir" -ForegroundColor White
Write-Host "  Launcher: $LauncherPublishDir" -ForegroundColor White
Write-Host ""

if (-not (Test-Path $ServicePublishDir)) {
    Write-Error "Service publish directory not found! Run build-self-contained.ps1 first."
    exit 1
}

if (-not (Test-Path $ConfigPublishDir)) {
    Write-Error "Config publish directory not found! Run build-self-contained.ps1 first."
    exit 1
}

if (-not (Test-Path $LauncherPublishDir)) {
    Write-Error "Launcher publish directory not found! Run build-self-contained.ps1 first."
    exit 1
}

# Count files
$serviceDllCount = (Get-ChildItem $ServicePublishDir -Filter *.dll).Count
$configDllCount = (Get-ChildItem $ConfigPublishDir -Filter *.dll).Count
$launcherDllCount = (Get-ChildItem $LauncherPublishDir -Filter *.dll).Count

Write-Host "  Service DLLs:  $serviceDllCount" -ForegroundColor Cyan
Write-Host "  Config DLLs:   $configDllCount" -ForegroundColor Cyan
Write-Host "  Launcher DLLs: $launcherDllCount" -ForegroundColor Cyan
Write-Host ""

# Step 2: Generate component definitions
Write-Host "[2/4] Generating WiX component definitions..." -ForegroundColor Yellow

& powershell.exe -File "$InstallerDir\generate-components.ps1" `
    -ServicePublishDir $ServicePublishDir `
    -ConfigPublishDir $ConfigPublishDir `
    -LauncherPublishDir $LauncherPublishDir `
    -OutputFile "GeneratedComponents.wxs"

if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to generate component definitions!"
    exit 1
}

Write-Host ""
Write-Host "Component definitions generated!" -ForegroundColor Green
Write-Host ""

# Step 3: Build installer
Write-Host "[3/4] Building MSI installer..." -ForegroundColor Yellow

$wixFile = Join-Path $InstallerDir "ScaleStreamerV2-SelfContained.wxs"
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$outputMsi = Join-Path $OutputDir "ScaleStreamer-v6.0.0-$timestamp.msi"

# Install extensions if not already present
& wix extension add -g WixToolset.UI.wixext 2>$null
& wix extension add -g WixToolset.Util.wixext 2>$null

Push-Location $InstallerDir
try {
    wix build "ScaleStreamerV2-SelfContained.wxs" `
        "GeneratedComponents.wxs" `
        -o $outputMsi `
        -ext WixToolset.UI.wixext `
        -ext WixToolset.Util.wixext `
        -d ServicePublishDir=$ServicePublishDir `
        -d ConfigPublishDir=$ConfigPublishDir `
        -d LauncherPublishDir=$LauncherPublishDir `
        -arch x64
} finally {
    Pop-Location
}

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
Write-Host "File size: $([math]::Round((Get-Item $outputMsi).Length / 1MB, 2)) MB" -ForegroundColor Cyan
Write-Host ""
Write-Host "This installer includes ALL .NET dependencies!" -ForegroundColor Green
Write-Host "No .NET Runtime installation required on target system." -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  1. Test installation: " -ForegroundColor Gray -NoNewline
Write-Host "msiexec /i `"$outputMsi`" /l*v install.log" -ForegroundColor White
Write-Host "  2. Silent install: " -ForegroundColor Gray -NoNewline
Write-Host "msiexec /i `"$outputMsi`" /quiet" -ForegroundColor White
Write-Host ""
