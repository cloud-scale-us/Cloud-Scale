# Scale Streamer v2.0 - Complete Backup and Restore Script
# This script creates a comprehensive backup of the entire project

param(
    [string]$Action = "backup",  # backup, restore, or verify
    [string]$BackupPath = "..\scale-streamer-backup-$(Get-Date -Format 'yyyyMMdd-HHmmss')",
    [switch]$SkipZip = $false  # Skip ZIP creation (useful to avoid timeouts)
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Scale Streamer v2.0 - Backup Manager" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$ProjectRoot = $PSScriptRoot

# Critical files and directories to backup
$CriticalPaths = @{
    "Source Code" = @(
        "src-v2"
    )
    "Build System" = @(
        "installer\*.ps1",
        "installer\*.wxs",
        "installer\README.md",
        "installer\icon.ico",
        "installer\banner.bmp",
        "installer\dialog.bmp",
        "installer\license.rtf"
    )
    "Protocol Templates" = @(
        "protocols"
    )
    "Branding Assets" = @(
        "assets\logo.png",
        "assets\logo.ico",
        "assets\cloudscale_logo.svg",
        "assets\*.ps1",
        "assets\icons"
    )
    "Documentation" = @(
        "CLAUDE.md",
        "BUILD-AND-TEST-V2.md",
        "BUILD-INSTRUCTIONS.md",
        "QUICK-START-V2.md",
        "V2-UNIVERSAL-ARCHITECTURE.md",
        "INSTALLER-BUILD-READY.md",
        "*.md"
    )
    "Solution Files" = @(
        "ScaleStreamer.sln",
        ".gitignore"
    )
    "This Backup Script" = @(
        "BACKUP-RESTORE.ps1"
    )
}

function Create-Backup {
    Write-Host "Creating backup at: $BackupPath" -ForegroundColor Yellow
    Write-Host ""

    if (-not (Test-Path $BackupPath)) {
        New-Item -ItemType Directory -Path $BackupPath | Out-Null
    }

    $totalFiles = 0
    $totalSize = 0

    foreach ($category in $CriticalPaths.Keys) {
        Write-Host "Backing up: $category" -ForegroundColor Cyan

        $categoryPath = Join-Path $BackupPath $category.Replace(" ", "_")
        New-Item -ItemType Directory -Path $categoryPath -Force | Out-Null

        foreach ($pattern in $CriticalPaths[$category]) {
            $fullPath = Join-Path $ProjectRoot $pattern

            if (Test-Path $fullPath) {
                $items = Get-Item $fullPath -ErrorAction SilentlyContinue

                foreach ($item in $items) {
                    if ($item.PSIsContainer) {
                        # Directory - copy recursively
                        $destPath = Join-Path $categoryPath $item.Name
                        Copy-Item -Path $item.FullName -Destination $destPath -Recurse -Force
                        $fileCount = (Get-ChildItem $destPath -Recurse -File).Count
                        $totalFiles += $fileCount
                        Write-Host "  Copied directory: $($item.Name) ($fileCount files)" -ForegroundColor Gray
                    } else {
                        # File - copy individual file
                        Copy-Item -Path $item.FullName -Destination $categoryPath -Force
                        $totalFiles++
                        $totalSize += $item.Length
                        Write-Host "  Copied file: $($item.Name)" -ForegroundColor Gray
                    }
                }
            } else {
                Write-Host "  Warning: Not found: $pattern" -ForegroundColor Yellow
            }
        }
    }

    # Create manifest file
    $manifest = @{
        BackupDate = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
        Version = "2.0.1"
        TotalFiles = $totalFiles
        TotalSizeMB = [math]::Round($totalSize / 1MB, 2)
        Categories = $CriticalPaths.Keys
    }

    $manifestPath = Join-Path $BackupPath "BACKUP-MANIFEST.json"
    $manifest | ConvertTo-Json -Depth 10 | Set-Content $manifestPath

    # Copy CLAUDE.md to root of backup for quick reference
    Copy-Item (Join-Path $ProjectRoot "CLAUDE.md") -Destination (Join-Path $BackupPath "CLAUDE.md") -Force

    Write-Host ""
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "BACKUP COMPLETE!" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "Backup location: $BackupPath" -ForegroundColor Cyan
    Write-Host "Total files: $totalFiles" -ForegroundColor White
    Write-Host "Total size: $([math]::Round($totalSize / 1MB, 2)) MB" -ForegroundColor White
    Write-Host ""
    Write-Host "To restore from this backup:" -ForegroundColor Yellow
    Write-Host "  .\BACKUP-RESTORE.ps1 -Action restore -BackupPath `"$BackupPath`"" -ForegroundColor White
    Write-Host ""

    # Create ZIP archive (optional)
    if (-not $SkipZip) {
        $zipPath = "$BackupPath.zip"
        Write-Host "Creating ZIP archive..." -ForegroundColor Yellow
        Compress-Archive -Path $BackupPath -DestinationPath $zipPath -Force
        $zipSize = (Get-Item $zipPath).Length
        Write-Host "ZIP created: $zipPath" -ForegroundColor Green
        Write-Host "ZIP size: $([math]::Round($zipSize / 1MB, 2)) MB" -ForegroundColor White
        Write-Host ""
    } else {
        Write-Host "Skipping ZIP creation (use without -SkipZip to create archive)" -ForegroundColor Yellow
        Write-Host ""
    }
}

function Restore-Backup {
    Write-Host "Restoring from backup: $BackupPath" -ForegroundColor Yellow
    Write-Host ""

    if (-not (Test-Path $BackupPath)) {
        # Check if it's a ZIP file
        if (Test-Path "$BackupPath.zip") {
            Write-Host "Found ZIP archive, extracting..." -ForegroundColor Yellow
            Expand-Archive -Path "$BackupPath.zip" -DestinationPath (Split-Path $BackupPath) -Force
        } else {
            Write-Error "Backup not found: $BackupPath"
            return
        }
    }

    # Read manifest
    $manifestPath = Join-Path $BackupPath "BACKUP-MANIFEST.json"
    if (Test-Path $manifestPath) {
        $manifest = Get-Content $manifestPath | ConvertFrom-Json
        Write-Host "Backup information:" -ForegroundColor Cyan
        Write-Host "  Date: $($manifest.BackupDate)" -ForegroundColor White
        Write-Host "  Version: $($manifest.Version)" -ForegroundColor White
        Write-Host "  Files: $($manifest.TotalFiles)" -ForegroundColor White
        Write-Host "  Size: $($manifest.TotalSizeMB) MB" -ForegroundColor White
        Write-Host ""
    }

    $response = Read-Host "This will overwrite existing files. Continue? (y/n)"
    if ($response -ne 'y') {
        Write-Host "Restore cancelled." -ForegroundColor Yellow
        return
    }

    Write-Host ""
    Write-Host "Restoring files..." -ForegroundColor Yellow

    foreach ($category in (Get-ChildItem $BackupPath -Directory)) {
        Write-Host "Restoring: $($category.Name)" -ForegroundColor Cyan

        # Copy contents back to project root
        $items = Get-ChildItem $category.FullName -Recurse
        foreach ($item in $items) {
            if (-not $item.PSIsContainer) {
                $relativePath = $item.FullName.Substring($category.FullName.Length + 1)
                $destPath = Join-Path $ProjectRoot $relativePath

                $destDir = Split-Path $destPath -Parent
                if (-not (Test-Path $destDir)) {
                    New-Item -ItemType Directory -Path $destDir -Force | Out-Null
                }

                Copy-Item -Path $item.FullName -Destination $destPath -Force
                Write-Host "  Restored: $relativePath" -ForegroundColor Gray
            }
        }
    }

    Write-Host ""
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "RESTORE COMPLETE!" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Yellow
    Write-Host "  1. Read CLAUDE.md for project overview" -ForegroundColor White
    Write-Host "  2. Run installer\build-self-contained.ps1" -ForegroundColor White
    Write-Host "  3. Run installer\build-installer-selfcontained.ps1" -ForegroundColor White
    Write-Host ""
}

function Verify-Backup {
    Write-Host "Verifying backup: $BackupPath" -ForegroundColor Yellow
    Write-Host ""

    if (-not (Test-Path $BackupPath)) {
        Write-Error "Backup not found: $BackupPath"
        return
    }

    $manifestPath = Join-Path $BackupPath "BACKUP-MANIFEST.json"
    if (Test-Path $manifestPath) {
        $manifest = Get-Content $manifestPath | ConvertFrom-Json
        Write-Host "Backup valid:" -ForegroundColor Green
        Write-Host "  Date: $($manifest.BackupDate)" -ForegroundColor White
        Write-Host "  Version: $($manifest.Version)" -ForegroundColor White
        Write-Host "  Files: $($manifest.TotalFiles)" -ForegroundColor White
        Write-Host "  Size: $($manifest.TotalSizeMB) MB" -ForegroundColor White
        Write-Host ""
        Write-Host "Categories:" -ForegroundColor Cyan
        foreach ($cat in $manifest.Categories) {
            Write-Host "  - $cat" -ForegroundColor White
        }
    } else {
        Write-Host "Warning: No manifest found" -ForegroundColor Yellow
    }

    Write-Host ""
    Write-Host "Directory structure:" -ForegroundColor Cyan
    Get-ChildItem $BackupPath -Directory | ForEach-Object {
        $fileCount = (Get-ChildItem $_.FullName -Recurse -File).Count
        Write-Host "  $($_.Name): $fileCount files" -ForegroundColor White
    }
}

# Execute requested action
switch ($Action.ToLower()) {
    "backup" {
        Create-Backup
    }
    "restore" {
        Restore-Backup
    }
    "verify" {
        Verify-Backup
    }
    default {
        Write-Host "Invalid action: $Action" -ForegroundColor Red
        Write-Host "Valid actions: backup, restore, verify" -ForegroundColor Yellow
    }
}
