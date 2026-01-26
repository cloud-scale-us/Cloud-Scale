Write-Host "===== QUICK STATUS CHECK =====" -ForegroundColor Cyan

# Service Status
Write-Host "`nSERVICE:" -ForegroundColor Yellow
$svc = Get-Service ScaleStreamerService -ErrorAction SilentlyContinue
if ($svc) {
    Write-Host "  Status: $($svc.Status)" -ForegroundColor $(if ($svc.Status -eq 'Running') {'Green'} else {'Red'})
} else {
    Write-Host "  NOT INSTALLED" -ForegroundColor Red
}

# Database Check
Write-Host "`nDATABASE:" -ForegroundColor Yellow
$dbPath = "C:\ProgramData\ScaleStreamer\scalestreamer.db"
if (Test-Path $dbPath) {
    Write-Host "  Exists: YES ($((Get-Item $dbPath).Length) bytes)" -ForegroundColor Green

    # Check for scales (need sqlite3)
    Write-Host "`n  Attempting to read scales..." -ForegroundColor Yellow
    try {
        Add-Type -Path "C:\Program Files\Scale Streamer\Service\Microsoft.Data.Sqlite.dll" -ErrorAction SilentlyContinue
        $conn = New-Object Microsoft.Data.Sqlite.SqliteConnection("Data Source=$dbPath")
        $conn.Open()

        $cmd = $conn.CreateCommand()
        $cmd.CommandText = "SELECT COUNT(*) FROM scales WHERE enabled=1"
        $count = $cmd.ExecuteScalar()

        if ($count -gt 0) {
            Write-Host "  Enabled Scales: $count" -ForegroundColor Green

            $cmd.CommandText = "SELECT id, name, host, port, protocol_name FROM scales WHERE enabled=1"
            $reader = $cmd.ExecuteReader()
            while ($reader.Read()) {
                Write-Host "    - $($reader.GetString(1)): $($reader.GetString(2)):$($reader.GetInt32(3)) ($($reader.GetString(4)))" -ForegroundColor Cyan
            }
            $reader.Close()
        } else {
            Write-Host "  Enabled Scales: 0" -ForegroundColor Red
            Write-Host "  ** NO SCALES CONFIGURED **" -ForegroundColor Red
        }

        $conn.Close()
    } catch {
        Write-Host "  Could not read database: $_" -ForegroundColor Yellow
    }
} else {
    Write-Host "  Exists: NO" -ForegroundColor Red
}

# Log Check
Write-Host "`nLATEST LOG:" -ForegroundColor Yellow
$logPath = "C:\ProgramData\ScaleStreamer\logs\service-$(Get-Date -Format 'yyyyMMdd').log"
if (Test-Path $logPath) {
    Write-Host "  File: $logPath" -ForegroundColor Green
    Write-Host "`n  Last 10 lines:" -ForegroundColor Cyan
    Get-Content $logPath -Tail 10 | ForEach-Object { Write-Host "    $_" }
} else {
    Write-Host "  Today's log not found" -ForegroundColor Red
}

Write-Host "`n===== QUICK FIX =====" -ForegroundColor Cyan
Write-Host "If NO SCALES CONFIGURED, run in PowerShell:" -ForegroundColor Yellow
Write-Host '  $scale = @{id="scale1"; name="Fairbanks Scale"; host="10.1.10.210"; port=5001; protocol="Fairbanks 6011"; enabled=1}' -ForegroundColor White
Write-Host '  # Then add via GUI Connection tab' -ForegroundColor Gray
