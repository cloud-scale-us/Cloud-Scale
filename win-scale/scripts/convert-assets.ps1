# Convert SVG assets to PNG and ICO formats
# Requires: Inkscape (free) - https://inkscape.org/release/

param(
    [string]$InkscapePath = "C:\Program Files\Inkscape\bin\inkscape.exe"
)

Write-Host "Cloud-Scale Asset Converter" -ForegroundColor Cyan
Write-Host "===========================" -ForegroundColor Cyan
Write-Host ""

# Check if Inkscape is installed
if (-not (Test-Path $InkscapePath)) {
    Write-Host "ERROR: Inkscape not found at: $InkscapePath" -ForegroundColor Red
    Write-Host ""
    Write-Host "Please install Inkscape from: https://inkscape.org/release/" -ForegroundColor Yellow
    Write-Host "Or specify custom path with: -InkscapePath 'C:\path\to\inkscape.exe'" -ForegroundColor Yellow
    exit 1
}

$sourceDir = "$PSScriptRoot\..\assetts\cloudscale_windows_svg_assets"
$targetIconsDir = "$PSScriptRoot\..\assets\icons"
$targetInstallerDir = "$PSScriptRoot\..\assets\installer"
$targetBrandingDir = "$PSScriptRoot\..\assets\branding"

# Create target directories
New-Item -ItemType Directory -Force -Path $targetIconsDir | Out-Null
New-Item -ItemType Directory -Force -Path $targetInstallerDir | Out-Null
New-Item -ItemType Directory -Force -Path $targetBrandingDir | Out-Null

function Convert-SVGtoPNG {
    param(
        [string]$InputSVG,
        [string]$OutputPNG,
        [int]$Width,
        [int]$Height = $Width
    )

    Write-Host "  Converting: $(Split-Path $OutputPNG -Leaf) ($Width x $Height)" -ForegroundColor Gray

    & $InkscapePath `
        --export-filename="$OutputPNG" `
        --export-width=$Width `
        --export-height=$Height `
        --export-background-opacity=0 `
        "$InputSVG" 2>$null

    if ($LASTEXITCODE -eq 0) {
        Write-Host "    ✓ Success" -ForegroundColor Green
    } else {
        Write-Host "    ✗ Failed" -ForegroundColor Red
    }
}

# 1. Main App Icon (multi-size PNG for ICO conversion)
Write-Host "[1/8] Converting main app icon..." -ForegroundColor Yellow
$iconSizes = @(256, 128, 64, 48, 32, 16)
$tempDir = "$env:TEMP\scale-icon-temp"
New-Item -ItemType Directory -Force -Path $tempDir | Out-Null

foreach ($size in $iconSizes) {
    Convert-SVGtoPNG `
        -InputSVG "$sourceDir\cloudscale_icon.svg" `
        -OutputPNG "$tempDir\icon_$size.png" `
        -Width $size
}

# Combine PNGs into ICO (requires ImageMagick or online tool)
Write-Host "  Creating multi-size ICO file..." -ForegroundColor Gray
Write-Host "    NOTE: Use online converter at https://convertico.com/" -ForegroundColor Yellow
Write-Host "    Upload: $tempDir\icon_*.png" -ForegroundColor Yellow
Write-Host "    Save to: $targetIconsDir\app-icon.ico" -ForegroundColor Yellow

# 2. System Tray Icons
Write-Host ""
Write-Host "[2/8] Converting system tray icons..." -ForegroundColor Yellow

# Connected (blue)
Convert-SVGtoPNG `
    -InputSVG "$sourceDir\cloudscale_tray_blue_64.svg" `
    -OutputPNG "$tempDir\tray_connected_64.png" `
    -Width 64

# Disconnected (monochrome)
Convert-SVGtoPNG `
    -InputSVG "$sourceDir\cloudscale_tray_monochrome_64.svg" `
    -OutputPNG "$tempDir\tray_disconnected_64.png" `
    -Width 64

# Error (create red version - manual step or use ImageMagick)
Write-Host "    Creating error icon (red variant)..." -ForegroundColor Gray
Write-Host "      NOTE: Manually colorize to red and save to:" -ForegroundColor Yellow
Write-Host "      $tempDir\tray_error_64.png" -ForegroundColor Yellow

# 3. Desktop Shortcut Icon
Write-Host ""
Write-Host "[3/8] Desktop shortcut icon (same as app icon)..." -ForegroundColor Yellow
Write-Host "  Will use app-icon.ico" -ForegroundColor Gray

# 4. Installer Banner
Write-Host ""
Write-Host "[4/8] Converting installer banner..." -ForegroundColor Yellow
Convert-SVGtoPNG `
    -InputSVG "$sourceDir\installer_banner_493x58.svg" `
    -OutputPNG "$targetInstallerDir\banner.png" `
    -Width 493 `
    -Height 58

# 5. Installer Dialog Background
Write-Host ""
Write-Host "[5/8] Converting installer dialog..." -ForegroundColor Yellow
Convert-SVGtoPNG `
    -InputSVG "$sourceDir\installer_dialog_493x312.svg" `
    -OutputPNG "$targetInstallerDir\dialog.png" `
    -Width 493 `
    -Height 312

# 6. Branding Logo
Write-Host ""
Write-Host "[6/8] Converting branding logos..." -ForegroundColor Yellow

# Main icon
Convert-SVGtoPNG `
    -InputSVG "$sourceDir\cloudscale_icon.svg" `
    -OutputPNG "$targetBrandingDir\cloud-scale-logo.png" `
    -Width 512

# Horizontal wordmark
Convert-SVGtoPNG `
    -InputSVG "$sourceDir\cloudscale_logo_horizontal.svg" `
    -OutputPNG "$targetBrandingDir\cloud-scale-wordmark.png" `
    -Width 800 `
    -Height 200

# 7. Splash Screen
Write-Host ""
Write-Host "[7/8] Creating splash screen..." -ForegroundColor Yellow
Convert-SVGtoPNG `
    -InputSVG "$sourceDir\cloudscale_logo_horizontal.svg" `
    -OutputPNG "$targetBrandingDir\splash-screen.png" `
    -Width 800 `
    -Height 600

# 8. Favicon
Write-Host ""
Write-Host "[8/8] Creating favicon..." -ForegroundColor Yellow
Convert-SVGtoPNG `
    -InputSVG "$sourceDir\cloudscale_icon.svg" `
    -OutputPNG "$tempDir\favicon_32.png" `
    -Width 32

Convert-SVGtoPNG `
    -InputSVG "$sourceDir\cloudscale_icon.svg" `
    -OutputPNG "$tempDir\favicon_16.png" `
    -Width 16

Write-Host ""
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "  PNG Conversion Complete!" -ForegroundColor Green
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Temporary PNG files created in: $tempDir" -ForegroundColor White
Write-Host ""
Write-Host "NEXT STEPS:" -ForegroundColor Yellow
Write-Host ""
Write-Host "1. Create ICO files (requires online converter or ImageMagick):" -ForegroundColor White
Write-Host ""
Write-Host "   app-icon.ico:" -ForegroundColor Cyan
Write-Host "   - Visit: https://convertico.com/" -ForegroundColor Gray
Write-Host "   - Upload all: $tempDir\icon_*.png" -ForegroundColor Gray
Write-Host "   - Download multi-size ICO" -ForegroundColor Gray
Write-Host "   - Save to: $targetIconsDir\app-icon.ico" -ForegroundColor Gray
Write-Host ""
Write-Host "   tray icons:" -ForegroundColor Cyan
Write-Host "   - Convert: tray_connected_64.png → tray-icon-connected.ico" -ForegroundColor Gray
Write-Host "   - Convert: tray_disconnected_64.png → tray-icon-disconnected.ico" -ForegroundColor Gray
Write-Host "   - Create red variant → tray-icon-error.ico" -ForegroundColor Gray
Write-Host "   - Save to: $targetIconsDir\" -ForegroundColor Gray
Write-Host ""
Write-Host "   favicon.ico:" -ForegroundColor Cyan
Write-Host "   - Convert: favicon_16.png + favicon_32.png → favicon.ico" -ForegroundColor Gray
Write-Host "   - Save to: $targetBrandingDir\favicon.ico" -ForegroundColor Gray
Write-Host ""
Write-Host "2. Copy license file:" -ForegroundColor White
Write-Host "   Copy-Item installer\license.rtf -Destination assets\installer\" -ForegroundColor Gray
Write-Host ""
Write-Host "3. Verify all assets created:" -ForegroundColor White
Write-Host "   dir assets\icons\" -ForegroundColor Gray
Write-Host "   dir assets\installer\" -ForegroundColor Gray
Write-Host "   dir assets\branding\" -ForegroundColor Gray
Write-Host ""
Write-Host "4. Update installer to use new assets" -ForegroundColor White
Write-Host ""
Write-Host "Conversion log saved to: asset-conversion.log" -ForegroundColor White
