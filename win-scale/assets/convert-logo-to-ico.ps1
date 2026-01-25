# Convert logo.png to .ico file with multiple sizes for Windows
param(
    [string]$InputPng = "logo.png",
    [string]$OutputIco = "logo.ico"
)

$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Drawing

Write-Host "Converting $InputPng to $OutputIco..." -ForegroundColor Cyan

# Load the source image
$sourceImage = [System.Drawing.Image]::FromFile((Resolve-Path $InputPng).Path)

# Create icon sizes
$sizes = @(16, 32, 48, 64, 128, 256)

# Create a memory stream for the icon
$iconStream = New-Object System.IO.MemoryStream

# Icon header
$iconWriter = New-Object System.IO.BinaryWriter($iconStream)
$iconWriter.Write([uint16]0)        # Reserved
$iconWriter.Write([uint16]1)        # Type (1 = ICO)
$iconWriter.Write([uint16]$sizes.Count) # Number of images

# Calculate offset for first image
$imageDataOffset = 6 + ($sizes.Count * 16)

$imageStreams = @()

foreach ($size in $sizes) {
    Write-Host "  Creating ${size}x${size} icon..." -ForegroundColor Gray

    # Create bitmap of specified size
    $bitmap = New-Object System.Drawing.Bitmap($size, $size)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality

    # Draw the source image scaled to this size
    $graphics.DrawImage($sourceImage, 0, 0, $size, $size)
    $graphics.Dispose()

    # Save to PNG in memory
    $pngStream = New-Object System.IO.MemoryStream
    $bitmap.Save($pngStream, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngBytes = $pngStream.ToArray()
    $pngStream.Dispose()
    $bitmap.Dispose()

    # Write icon directory entry
    # Note: 256 is represented as 0 in ICO format (byte max is 255)
    $width = if ($size -eq 256) { 0 } else { $size }
    $height = if ($size -eq 256) { 0 } else { $size }
    $iconWriter.Write([byte]$width)          # Width (0 means 256)
    $iconWriter.Write([byte]$height)         # Height (0 means 256)
    $iconWriter.Write([byte]0)               # Color palette
    $iconWriter.Write([byte]0)               # Reserved
    $iconWriter.Write([uint16]1)             # Color planes
    $iconWriter.Write([uint16]32)            # Bits per pixel
    $iconWriter.Write([uint32]$pngBytes.Length) # Size of image data
    $iconWriter.Write([uint32]$imageDataOffset) # Offset to image data

    $imageDataOffset += $pngBytes.Length
    $imageStreams += $pngBytes
}

# Write all image data
foreach ($imageBytes in $imageStreams) {
    $iconWriter.Write($imageBytes)
}

# Save to file
$iconBytes = $iconStream.ToArray()
[System.IO.File]::WriteAllBytes((Join-Path (Get-Location) $OutputIco), $iconBytes)

$iconWriter.Dispose()
$iconStream.Dispose()
$sourceImage.Dispose()

Write-Host "Icon created: $OutputIco" -ForegroundColor Green
Write-Host "  Sizes: $($sizes -join ', ')" -ForegroundColor Gray
$sizeKb = [math]::Round($iconBytes.Length / 1KB, 2)
Write-Host "  File size: $sizeKb KB" -ForegroundColor Gray
