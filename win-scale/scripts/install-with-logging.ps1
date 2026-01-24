# Install Scale Streamer with verbose logging
# Usage: .\install-with-logging.ps1

param(
    [string]$MsiPath = "$PSScriptRoot\..\installer\ScaleStreamerSetup.msi"
)

$logDir = "$env:TEMP\ScaleStreamer-Install"
$logFile = Join-Path $logDir "install-$(Get-Date -Format 'yyyyMMdd-HHmmss').log"

# Create log directory
New-Item -ItemType Directory -Force -Path $logDir | Out-Null

Write-Host "Installing Scale Streamer with verbose logging..." -ForegroundColor Cyan
Write-Host "MSI: $MsiPath" -ForegroundColor Gray
Write-Host "Log: $logFile" -ForegroundColor Gray
Write-Host ""

# Install with full logging
$arguments = @(
    "/i"
    "`"$MsiPath`""
    "/l*vx"
    "`"$logFile`""
)

Write-Host "Running: msiexec $($arguments -join ' ')" -ForegroundColor Gray
Write-Host ""

$process = Start-Process -FilePath "msiexec.exe" -ArgumentList $arguments -Wait -PassThru

Write-Host ""
if ($process.ExitCode -eq 0) {
    Write-Host "Installation completed successfully!" -ForegroundColor Green
} else {
    Write-Host "Installation failed with exit code: $($process.ExitCode)" -ForegroundColor Red
    Write-Host ""
    Write-Host "Common exit codes:" -ForegroundColor Yellow
    Write-Host "  0    = Success"
    Write-Host "  1602 = User cancelled"
    Write-Host "  1603 = Fatal error during installation"
    Write-Host "  1618 = Another installation is in progress"
    Write-Host "  1625 = Installation forbidden by system policy"
    Write-Host ""
}

Write-Host "Full installation log saved to:" -ForegroundColor Cyan
Write-Host "  $logFile" -ForegroundColor White
Write-Host ""

# Check application log location
$appLogPath = "$env:LOCALAPPDATA\ScaleStreamer\app.log"
Write-Host "Application runtime log will be at:" -ForegroundColor Cyan
Write-Host "  $appLogPath" -ForegroundColor White
Write-Host ""

# Offer to open log
$response = Read-Host "Open installation log? (y/n)"
if ($response -eq 'y') {
    notepad $logFile
}
