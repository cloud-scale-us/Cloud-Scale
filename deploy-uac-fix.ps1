# Deploy UAC-free service control update
# Run this script as Administrator

$ErrorActionPreference = "Continue"

Write-Host "=== Deploying UAC-Free Service Control ===" -ForegroundColor Cyan
Write-Host ""

# Stop service
Write-Host "[1/5] Stopping ScaleStreamerService..." -ForegroundColor Yellow
$result = net stop ScaleStreamerService 2>&1
if ($LASTEXITCODE -eq 0 -or $result -match "is not started") {
    Write-Host "Service stopped" -ForegroundColor Green
} else {
    Write-Host "Service may not be running (continuing anyway)" -ForegroundColor DarkYellow
}

Start-Sleep -Seconds 2

# Copy files
Write-Host ""
Write-Host "[2/5] Copying updated Service DLLs..." -ForegroundColor Yellow
$sourcePath = "C:\Users\Windfield\Cloud-Scale\win-scale\src-v2"
$servicePath = "C:\Program Files\Scale Streamer\Service"
$configPath = "C:\Program Files\Scale Streamer\Config"

Copy-Item "$sourcePath\ScaleStreamer.Common\bin\Release\net8.0\ScaleStreamer.Common.dll" "$servicePath\" -Force
Copy-Item "$sourcePath\ScaleStreamer.Service\bin\Release\net8.0\win-x64\ScaleStreamer.Service.dll" "$servicePath\" -Force
Write-Host "  Service DLLs copied" -ForegroundColor Gray

Write-Host ""
Write-Host "[3/5] Copying updated Config DLLs..." -ForegroundColor Yellow
Copy-Item "$sourcePath\ScaleStreamer.Common\bin\Release\net8.0\ScaleStreamer.Common.dll" "$configPath\" -Force
Copy-Item "$sourcePath\ScaleStreamer.Config\bin\Release\net8.0-windows\ScaleStreamer.Config.dll" "$configPath\" -Force
Write-Host "  Config DLLs copied" -ForegroundColor Gray

# Grant service control permissions to current user
Write-Host ""
Write-Host "[4/5] Granting service control permissions to current user..." -ForegroundColor Yellow

$userSid = ([System.Security.Principal.WindowsIdentity]::GetCurrent()).User.Value
Write-Host "  User SID: $userSid" -ForegroundColor Gray

# Get current SDDL
$currentSddl = (sc.exe sdshow ScaleStreamerService | Where-Object { $_ -match "^D:" }) -join ""

if ($currentSddl) {
    # Check if permission already granted
    if ($currentSddl -match $userSid) {
        Write-Host "  Permissions already granted" -ForegroundColor Green
    } else {
        # Add user permission: Start (RP), Stop (WP), Query Status (LC), Query Config (CC)
        $newAce = "(A;;RPWPLCCC;;;$userSid)"
        $newSddl = "D:" + $newAce + $currentSddl.Substring(2)

        $setResult = sc.exe sdset ScaleStreamerService $newSddl 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Host "  Permissions granted successfully" -ForegroundColor Green
        } else {
            Write-Host "  Failed to set permissions: $setResult" -ForegroundColor Red
        }
    }
} else {
    Write-Host "  Warning: Could not get current SDDL" -ForegroundColor DarkYellow
}

# Start service
Write-Host ""
Write-Host "[5/5] Starting ScaleStreamerService..." -ForegroundColor Yellow
net start ScaleStreamerService
if ($LASTEXITCODE -eq 0) {
    Write-Host "Service started" -ForegroundColor Green
} else {
    Write-Host "Failed to start service" -ForegroundColor Red
}

Write-Host ""
Write-Host "=== Deployment Complete ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "You can now control the service from the Config app WITHOUT UAC prompts!" -ForegroundColor Green
Write-Host ""

# Show service status
Get-Service ScaleStreamerService | Format-Table Name, Status -AutoSize

Read-Host "Press Enter to exit"
