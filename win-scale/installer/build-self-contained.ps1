# Build self-contained installer with all dependencies
# This creates a larger MSI but doesn't require .NET Runtime installation

param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

# Ensure dotnet is in PATH (check multiple locations)
$dotnetPaths = @("C:\Program Files\dotnet", "C:\dotnet", "$env:USERPROFILE\.dotnet")
foreach ($path in $dotnetPaths) {
    if (Test-Path "$path\dotnet.exe") {
        $env:Path = "$path;$env:Path"
        break
    }
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Self-Contained Installer Build" -ForegroundColor Cyan
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

Write-Host "[1/3] Publishing Service as self-contained..." -ForegroundColor Yellow
dotnet publish $ServiceProjectDir `
    -c $Configuration `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=false `
    -p:PublishReadyToRun=false `
    -p:IncludeNativeLibrariesForSelfExtract=true

if ($LASTEXITCODE -ne 0) {
    Write-Error "Service publish failed!"
    exit 1
}

Write-Host "[2/4] Publishing Config GUI as self-contained..." -ForegroundColor Yellow
dotnet publish $ConfigProjectDir `
    -c $Configuration `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=false `
    -p:PublishReadyToRun=false `
    -p:IncludeNativeLibrariesForSelfExtract=true

if ($LASTEXITCODE -ne 0) {
    Write-Error "Config publish failed!"
    exit 1
}

Write-Host "[3/4] Publishing Launcher as self-contained..." -ForegroundColor Yellow
dotnet publish $LauncherProjectDir `
    -c $Configuration `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=false `
    -p:PublishReadyToRun=false `
    -p:IncludeNativeLibrariesForSelfExtract=true

if ($LASTEXITCODE -ne 0) {
    Write-Error "Launcher publish failed!"
    exit 1
}

Write-Host "[4/4] Copying logo files to Config and Launcher publish directories..." -ForegroundColor Yellow
$AssetsDir = Join-Path $RootDir "assets"
$LogoPng = Join-Path $AssetsDir "logo.png"
$LogoIco = Join-Path $AssetsDir "logo.ico"

if (Test-Path $LogoPng) {
    Copy-Item $LogoPng -Destination $ConfigPublishDir -Force
    Write-Host "  Copied logo.png" -ForegroundColor White
} else {
    Write-Warning "  logo.png not found in assets directory"
}

if (Test-Path $LogoIco) {
    Copy-Item $LogoIco -Destination $ConfigPublishDir -Force
    Copy-Item $LogoIco -Destination $LauncherPublishDir -Force
    Write-Host "  Copied logo.ico to Config and Launcher" -ForegroundColor White
} else {
    Write-Warning "  logo.ico not found in assets directory"
}

if (Test-Path $LogoPng) {
    Copy-Item $LogoPng -Destination $LauncherPublishDir -Force
    Write-Host "  Copied logo.png to Launcher" -ForegroundColor White
}

Write-Host ""
Write-Host "Self-contained binaries created!" -ForegroundColor Green
Write-Host "  Service:  $ServicePublishDir" -ForegroundColor Cyan
Write-Host "  Config:   $ConfigPublishDir" -ForegroundColor Cyan
Write-Host "  Launcher: $LauncherPublishDir" -ForegroundColor Cyan
Write-Host ""
Write-Host "File counts:" -ForegroundColor Yellow
Write-Host "  Service DLLs:  $((Get-ChildItem $ServicePublishDir -Filter *.dll).Count)" -ForegroundColor White
Write-Host "  Config DLLs:   $((Get-ChildItem $ConfigPublishDir -Filter *.dll).Count)" -ForegroundColor White
Write-Host "  Launcher DLLs: $((Get-ChildItem $LauncherPublishDir -Filter *.dll).Count)" -ForegroundColor White
Write-Host ""
Write-Host "Next: Run .\build-installer-selfcontained.ps1" -ForegroundColor Yellow
Write-Host "This will create an installer with ALL dependencies included." -ForegroundColor Green
Write-Host ""
