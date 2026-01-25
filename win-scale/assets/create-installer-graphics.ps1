# Create installer graphics (banner and dialog) from logo
param(
    [string]$LogoPng = "logo.png",
    [string]$OutputDir = "..\installer"
)

$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Drawing

Write-Host "Creating WiX installer graphics..." -ForegroundColor Cyan
Write-Host ""

# Load the source logo
$logo = [System.Drawing.Image]::FromFile((Resolve-Path $LogoPng).Path)

# Create banner (493 x 58 pixels)
Write-Host "Creating banner.bmp (493x58)..." -ForegroundColor Yellow
$banner = New-Object System.Drawing.Bitmap(493, 58)
$graphics = [System.Drawing.Graphics]::FromImage($banner)
$graphics.Clear([System.Drawing.Color]::White)
$graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality

# Draw logo smaller and positioned to the right to avoid text overlap
$logoHeight = 45
$logoWidth = [int]($logo.Width * ($logoHeight / $logo.Height))
$logoX = 493 - $logoWidth - 10  # 10px margin from right
$logoY = 6  # 6px from top
$graphics.DrawImage($logo, $logoX, $logoY, $logoWidth, $logoHeight)
$graphics.Dispose()

$bannerPath = Join-Path $OutputDir "banner.bmp"
$banner.Save($bannerPath, [System.Drawing.Imaging.ImageFormat]::Bmp)
$banner.Dispose()
Write-Host "  Created: $bannerPath" -ForegroundColor Green

# Create dialog (493 x 312 pixels)
Write-Host "Creating dialog.bmp (493x312)..." -ForegroundColor Yellow
$dialog = New-Object System.Drawing.Bitmap(493, 312)
$graphics = [System.Drawing.Graphics]::FromImage($dialog)

# Gradient background (light blue to white)
$brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
    (New-Object System.Drawing.Point(0, 0)),
    (New-Object System.Drawing.Point(0, 312)),
    [System.Drawing.Color]::FromArgb(240, 248, 255),  # AliceBlue
    [System.Drawing.Color]::White
)
$graphics.FillRectangle($brush, 0, 0, 493, 312)
$brush.Dispose()

$graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality

# Draw logo at bottom to avoid text overlap (text is usually at top)
$logoHeight = 150
$logoWidth = [int]($logo.Width * ($logoHeight / $logo.Height))
$logoX = (493 - $logoWidth) / 2
$logoY = 312 - $logoHeight - 20  # 20px margin from bottom
$graphics.DrawImage($logo, $logoX, $logoY, $logoWidth, $logoHeight)
$graphics.Dispose()

$dialogPath = Join-Path $OutputDir "dialog.bmp"
$dialog.Save($dialogPath, [System.Drawing.Imaging.ImageFormat]::Bmp)
$dialog.Dispose()
Write-Host "  Created: $dialogPath" -ForegroundColor Green

$logo.Dispose()

Write-Host ""
Write-Host "Installer graphics created successfully!" -ForegroundColor Green
