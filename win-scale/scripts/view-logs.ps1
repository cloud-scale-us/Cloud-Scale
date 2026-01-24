# View Scale Streamer logs
# Usage: .\view-logs.ps1

Write-Host "Scale Streamer Log Viewer" -ForegroundColor Cyan
Write-Host "=========================" -ForegroundColor Cyan
Write-Host ""

# Application runtime log
$appLog = "$env:LOCALAPPDATA\ScaleStreamer\app.log"
Write-Host "[1] Application Runtime Log" -ForegroundColor Yellow
Write-Host "    Location: $appLog" -ForegroundColor Gray

if (Test-Path $appLog) {
    $fileInfo = Get-Item $appLog
    Write-Host "    Size: $([math]::Round($fileInfo.Length / 1KB, 2)) KB" -ForegroundColor Gray
    Write-Host "    Modified: $($fileInfo.LastWriteTime)" -ForegroundColor Gray
    Write-Host "    Status: EXISTS" -ForegroundColor Green
} else {
    Write-Host "    Status: NOT FOUND (app hasn't run yet)" -ForegroundColor Red
}
Write-Host ""

# Installation logs
$installLogDir = "$env:TEMP\ScaleStreamer-Install"
Write-Host "[2] Installation Logs" -ForegroundColor Yellow
Write-Host "    Location: $installLogDir" -ForegroundColor Gray

if (Test-Path $installLogDir) {
    $logs = Get-ChildItem -Path $installLogDir -Filter "install-*.log" | Sort-Object LastWriteTime -Descending
    if ($logs.Count -gt 0) {
        Write-Host "    Found $($logs.Count) installation log(s)" -ForegroundColor Green
        foreach ($log in $logs) {
            Write-Host "      - $($log.Name) ($([math]::Round($log.Length / 1KB, 2)) KB)" -ForegroundColor Gray
        }
    } else {
        Write-Host "    Status: NO LOGS FOUND" -ForegroundColor Red
    }
} else {
    Write-Host "    Status: DIRECTORY NOT FOUND" -ForegroundColor Red
}
Write-Host ""

# Windows Event Log
Write-Host "[3] Windows Installer Event Log" -ForegroundColor Yellow
Write-Host "    Checking for recent MsiInstaller events..." -ForegroundColor Gray

try {
    $events = Get-WinEvent -LogName Application -MaxEvents 50 -ErrorAction SilentlyContinue |
              Where-Object { $_.ProviderName -eq "MsiInstaller" }

    if ($events) {
        $recentEvents = $events | Select-Object -First 5
        Write-Host "    Found $($events.Count) MsiInstaller events (showing last 5):" -ForegroundColor Green
        foreach ($event in $recentEvents) {
            $level = switch ($event.Level) {
                1 { "CRITICAL" }
                2 { "ERROR" }
                3 { "WARNING" }
                4 { "INFO" }
                default { "UNKNOWN" }
            }
            Write-Host "      [$level] $($event.TimeCreated): $($event.Message.Split("`n")[0])" -ForegroundColor Gray
        }
    } else {
        Write-Host "    Status: NO RECENT EVENTS" -ForegroundColor Yellow
    }
} catch {
    Write-Host "    Status: UNABLE TO READ EVENT LOG" -ForegroundColor Red
}
Write-Host ""

# Menu
Write-Host "Options:" -ForegroundColor Cyan
Write-Host "  [1] Open application log in Notepad"
Write-Host "  [2] Open latest installation log in Notepad"
Write-Host "  [3] Open Event Viewer (Application log)"
Write-Host "  [4] Tail application log (live monitoring)"
Write-Host "  [Q] Quit"
Write-Host ""

$choice = Read-Host "Select option"

switch ($choice) {
    "1" {
        if (Test-Path $appLog) {
            notepad $appLog
        } else {
            Write-Host "Application log not found!" -ForegroundColor Red
        }
    }
    "2" {
        if (Test-Path $installLogDir) {
            $latestLog = Get-ChildItem -Path $installLogDir -Filter "install-*.log" |
                         Sort-Object LastWriteTime -Descending |
                         Select-Object -First 1
            if ($latestLog) {
                notepad $latestLog.FullName
            } else {
                Write-Host "No installation logs found!" -ForegroundColor Red
            }
        } else {
            Write-Host "Installation log directory not found!" -ForegroundColor Red
        }
    }
    "3" {
        eventvwr /c:Application
    }
    "4" {
        if (Test-Path $appLog) {
            Write-Host "Monitoring $appLog (Ctrl+C to stop)..." -ForegroundColor Green
            Get-Content -Path $appLog -Wait -Tail 50
        } else {
            Write-Host "Application log not found!" -ForegroundColor Red
        }
    }
    default {
        Write-Host "Exiting..." -ForegroundColor Gray
    }
}
