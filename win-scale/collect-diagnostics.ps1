# ========================================
# Scale Streamer Comprehensive Diagnostics Collector
# Version: 3.3.3
# ========================================
# This script collects ALL diagnostic information for troubleshooting
# Copy and paste the entire output to Claude for analysis

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Scale Streamer Diagnostics Collection" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# Function to add section headers
function Write-Section {
    param([string]$Title)
    Write-Host "`n==================== $Title ====================" -ForegroundColor Yellow
}

# System Information
Write-Section "SYSTEM INFORMATION"
Write-Host "Date/Time: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
Write-Host "Computer Name: $env:COMPUTERNAME"
Write-Host "User: $env:USERNAME"
Write-Host "OS: $(Get-CimInstance Win32_OperatingSystem | Select-Object -ExpandProperty Caption)"
Write-Host "OS Version: $(Get-CimInstance Win32_OperatingSystem | Select-Object -ExpandProperty Version)"

# .NET Runtime
Write-Section ".NET RUNTIME"
dotnet --version
dotnet --list-runtimes

# Network Configuration
Write-Section "NETWORK CONFIGURATION"
Get-NetIPAddress | Where-Object {$_.AddressFamily -eq 'IPv4'} | Format-Table -AutoSize

# Test Connectivity to Scale
Write-Section "SCALE CONNECTIVITY TEST"
$scaleIP = "10.1.10.210"
$scalePort = 5001

Write-Host "Testing ping to $scaleIP..."
Test-Connection -ComputerName $scaleIP -Count 3 -ErrorAction SilentlyContinue | Format-Table

Write-Host "`nTesting TCP port $scalePort on $scaleIP..."
try {
    $tcpClient = New-Object System.Net.Sockets.TcpClient
    $connect = $tcpClient.BeginConnect($scaleIP, $scalePort, $null, $null)
    $wait = $connect.AsyncWaitHandle.WaitOne(3000, $false)

    if ($wait) {
        $tcpClient.EndConnect($connect)
        Write-Host "SUCCESS: Port $scalePort is OPEN" -ForegroundColor Green

        # Try to read data
        Write-Host "`nAttempting to read raw data from scale..."
        $stream = $tcpClient.GetStream()
        $buffer = New-Object byte[] 1024
        $stream.ReadTimeout = 5000

        try {
            $bytesRead = $stream.Read($buffer, 0, 1024)
            if ($bytesRead -gt 0) {
                $data = [System.Text.Encoding]::ASCII.GetString($buffer, 0, $bytesRead)
                Write-Host "RAW DATA RECEIVED:" -ForegroundColor Green
                Write-Host $data -ForegroundColor Cyan
            }
        } catch {
            Write-Host "No data received within timeout (this may be normal)" -ForegroundColor Yellow
        }

        $tcpClient.Close()
    } else {
        Write-Host "FAILED: Port $scalePort is CLOSED or FILTERED" -ForegroundColor Red
        $tcpClient.Close()
    }
} catch {
    Write-Host "ERROR: Could not connect to $scaleIP`:$scalePort" -ForegroundColor Red
    Write-Host "Error: $_" -ForegroundColor Red
}

# Service Status
Write-Section "SERVICE STATUS"
$service = Get-Service -Name "ScaleStreamerService" -ErrorAction SilentlyContinue
if ($service) {
    Write-Host "Service Status: $($service.Status)" -ForegroundColor $(if ($service.Status -eq 'Running') {'Green'} else {'Red'})
    Write-Host "Start Type: $($service.StartType)"
    Write-Host "Display Name: $($service.DisplayName)"
} else {
    Write-Host "ERROR: ScaleStreamerService not found!" -ForegroundColor Red
}

# Installation Paths
Write-Section "INSTALLATION PATHS"
$installPath = "C:\Program Files\Scale Streamer"
$dataPath = "C:\ProgramData\ScaleStreamer"

Write-Host "Install Directory:"
if (Test-Path $installPath) {
    Get-ChildItem $installPath -Recurse -Directory | Select-Object FullName | Format-Table -AutoSize
} else {
    Write-Host "ERROR: Install directory not found!" -ForegroundColor Red
}

Write-Host "`nData Directory:"
if (Test-Path $dataPath) {
    Get-ChildItem $dataPath -Recurse | Select-Object FullName, Length, LastWriteTime | Format-Table -AutoSize
} else {
    Write-Host "ERROR: Data directory not found!" -ForegroundColor Red
}

# Protocol Files
Write-Section "PROTOCOL FILES"
$protocolsPath = "$installPath\protocols"
if (Test-Path $protocolsPath) {
    Write-Host "Protocols directory exists: $protocolsPath" -ForegroundColor Green
    Get-ChildItem $protocolsPath -Recurse -Filter "*.json" | ForEach-Object {
        Write-Host "`n$($_.FullName):" -ForegroundColor Cyan
        Get-Content $_.FullName
    }
} else {
    Write-Host "ERROR: Protocols directory not found!" -ForegroundColor Red
}

# Database Check
Write-Section "DATABASE CONTENT"
$dbPath = "$dataPath\scalestreamer.db"
if (Test-Path $dbPath) {
    Write-Host "Database found: $dbPath" -ForegroundColor Green
    Write-Host "Size: $((Get-Item $dbPath).Length) bytes"
    Write-Host "Last Modified: $((Get-Item $dbPath).LastWriteTime)"

    # Try to query database using .NET SQLite
    try {
        Add-Type -Path "C:\Program Files\Scale Streamer\Service\Microsoft.Data.Sqlite.dll"
        $connString = "Data Source=$dbPath"
        $conn = New-Object Microsoft.Data.Sqlite.SqliteConnection($connString)
        $conn.Open()

        Write-Host "`nDatabase Tables:"
        $cmd = $conn.CreateCommand()
        $cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table'"
        $reader = $cmd.ExecuteReader()
        while ($reader.Read()) {
            Write-Host "  - $($reader.GetString(0))"
        }
        $reader.Close()

        Write-Host "`nConfigured Scales:"
        $cmd.CommandText = "SELECT id, name, connection_type, host, port, enabled FROM scales"
        $reader = $cmd.ExecuteReader()
        $scaleCount = 0
        while ($reader.Read()) {
            $scaleCount++
            Write-Host "  Scale ID: $($reader.GetString(0))"
            Write-Host "    Name: $($reader.GetString(1))"
            Write-Host "    Type: $($reader.GetString(2))"
            Write-Host "    Host: $($reader.GetString(3))"
            Write-Host "    Port: $($reader.GetInt32(4))"
            Write-Host "    Enabled: $($reader.GetBoolean(5))"
            Write-Host ""
        }
        $reader.Close()

        if ($scaleCount -eq 0) {
            Write-Host "  *** NO SCALES CONFIGURED ***" -ForegroundColor Red
        }

        $conn.Close()
    } catch {
        Write-Host "Could not query database: $_" -ForegroundColor Yellow
    }
} else {
    Write-Host "ERROR: Database not found!" -ForegroundColor Red
}

# Service Logs (Last 100 lines)
Write-Section "SERVICE LOGS (Last 100 lines)"
$logsPath = "$dataPath\logs"
if (Test-Path $logsPath) {
    $logFiles = Get-ChildItem $logsPath -Filter "*.log" | Sort-Object LastWriteTime -Descending
    if ($logFiles.Count -gt 0) {
        $latestLog = $logFiles[0]
        Write-Host "Latest log file: $($latestLog.FullName)" -ForegroundColor Cyan
        Write-Host "Size: $($latestLog.Length) bytes"
        Write-Host "Last Modified: $($latestLog.LastWriteTime)"
        Write-Host "`nLog Contents (last 100 lines):" -ForegroundColor Cyan
        Get-Content $latestLog.FullName -Tail 100
    } else {
        Write-Host "No log files found!" -ForegroundColor Red
    }
} else {
    Write-Host "ERROR: Logs directory not found!" -ForegroundColor Red
}

# Registry Settings
Write-Section "REGISTRY SETTINGS"
$regPath = "HKLM:\Software\Cloud-Scale\ScaleStreamer"
if (Test-Path $regPath) {
    Get-ItemProperty $regPath | Format-List
} else {
    Write-Host "ERROR: Registry keys not found!" -ForegroundColor Red
}

# Firewall Rules
Write-Section "FIREWALL RULES"
Get-NetFirewallRule | Where-Object {$_.DisplayName -like "*Scale*"} | Format-Table -AutoSize

# Running Processes
Write-Section "SCALE STREAMER PROCESSES"
Get-Process | Where-Object {$_.ProcessName -like "*Scale*"} | Format-Table -AutoSize

# Summary
Write-Section "DIAGNOSTIC SUMMARY"
Write-Host "✓ System Information: Collected" -ForegroundColor Green
Write-Host "✓ Network Configuration: Collected" -ForegroundColor Green
Write-Host "$(if (Test-Connection -ComputerName $scaleIP -Count 1 -Quiet) {'✓'} else {'✗'}) Scale Connectivity: $(if (Test-Connection -ComputerName $scaleIP -Count 1 -Quiet) {'REACHABLE'} else {'UNREACHABLE'})" -ForegroundColor $(if (Test-Connection -ComputerName $scaleIP -Count 1 -Quiet) {'Green'} else {'Red'})
Write-Host "$(if ($service -and $service.Status -eq 'Running') {'✓'} else {'✗'}) Service Status: $(if ($service) {$service.Status} else {'NOT INSTALLED'})" -ForegroundColor $(if ($service -and $service.Status -eq 'Running') {'Green'} else {'Red'})
Write-Host "$(if (Test-Path $protocolsPath) {'✓'} else {'✗'}) Protocols: $(if (Test-Path $protocolsPath) {'FOUND'} else {'MISSING'})" -ForegroundColor $(if (Test-Path $protocolsPath) {'Green'} else {'Red'})
Write-Host "$(if (Test-Path $dbPath) {'✓'} else {'✗'}) Database: $(if (Test-Path $dbPath) {'FOUND'} else {'MISSING'})" -ForegroundColor $(if (Test-Path $dbPath) {'Green'} else {'Red'})

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Diagnostics collection complete!" -ForegroundColor Cyan
Write-Host "Copy ALL output above and paste to Claude" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan
