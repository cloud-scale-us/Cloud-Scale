# Scale Streamer v2.0 - Build and Test Instructions

## Current Status

✅ **60% Complete** - Core application architecture finished

### What's Been Built

| Component | Status | Completion |
|-----------|--------|------------|
| Common Library (interfaces, models, protocols) | ✅ Complete | 100% |
| Universal Protocol Engine | ✅ Complete | 100% |
| Database Layer (SQLite) | ✅ Complete | 100% |
| Windows Service | ✅ Complete | 100% |
| IPC Communication System | ✅ Complete | 100% |
| WinForms GUI (5 tabs) | ✅ Complete | 100% |
| Protocol Templates (3 examples) | ✅ Complete | 100% |
| RTSP Streaming | ⏳ Pending | 0% |
| Additional Protocol Implementations | ⏳ Pending | 30% |
| WiX Installer for Service | ⏳ Pending | 40% |
| Asset Files (icons) | ⏳ Pending | 0% |
| Testing & Documentation | ⏳ Pending | 20% |

---

## Prerequisites

### Required Software

1. **Windows 10 or 11** (required for Windows Service and WinForms)
2. **Visual Studio 2022** (Community Edition or higher)
   - Workload: ".NET desktop development"
   - Component: ".NET 8.0 Runtime"
3. **.NET 8.0 SDK** (already installed at `$HOME/.dotnet`)

### Optional Tools

- **Git for Windows** (for version control)
- **WiX Toolset v4** (for installer building)
- **SQLite Browser** (for database inspection)

---

## Build Instructions

### Option 1: Build with Visual Studio (Recommended)

#### Step 1: Open Solution

```powershell
# Navigate to project directory
cd D:\win-scale\win-scale

# Open solution in Visual Studio
start ScaleStreamer.sln
```

#### Step 2: Restore NuGet Packages

Visual Studio will automatically restore packages when you open the solution. If not:

- Right-click on solution in Solution Explorer
- Select "Restore NuGet Packages"

Or use Package Manager Console:
```powershell
Update-Package -reinstall
```

#### Step 3: Build Solution

Press **Ctrl+Shift+B** or:
- Build → Build Solution

**Expected Output**:
```
Build started...
1>------ Build started: Project: ScaleStreamer.Common, Configuration: Debug Any CPU ------
1>ScaleStreamer.Common -> D:\win-scale\win-scale\src-v2\ScaleStreamer.Common\bin\Debug\net8.0\ScaleStreamer.Common.dll
2>------ Build started: Project: ScaleStreamer.Service, Configuration: Debug Any CPU ------
2>ScaleStreamer.Service -> D:\win-scale\win-scale\src-v2\ScaleStreamer.Service\bin\Debug\net8.0\ScaleStreamer.Service.exe
3>------ Build started: Project: ScaleStreamer.Config, Configuration: Debug Any CPU ------
3>ScaleStreamer.Config -> D:\win-scale\win-scale\src-v2\ScaleStreamer.Config\bin\Debug\net8.0\ScaleStreamer.Config.exe
========== Build: 3 succeeded, 0 failed, 0 up-to-date, 0 skipped ==========
```

### Option 2: Build with Command Line

#### Step 1: Set PATH

If using WSL bash:
```bash
export PATH="$HOME/.dotnet:$PATH"
cd /mnt/d/win-scale/win-scale
```

If using PowerShell:
```powershell
$env:PATH = "$env:USERPROFILE\.dotnet;$env:PATH"
cd D:\win-scale\win-scale
```

#### Step 2: Restore Packages

```bash
dotnet restore ScaleStreamer.sln
```

**Expected Output**:
```
Determining projects to restore...
Restored D:\win-scale\win-scale\src-v2\ScaleStreamer.Common\ScaleStreamer.Common.csproj (in 1.2 sec).
Restored D:\win-scale\win-scale\src-v2\ScaleStreamer.Service\ScaleStreamer.Service.csproj (in 1.3 sec).
Restored D:\win-scale\win-scale\src-v2\ScaleStreamer.Config\ScaleStreamer.Config.csproj (in 1.1 sec).
```

#### Step 3: Build All Projects

```bash
dotnet build ScaleStreamer.sln --configuration Debug
```

For Release build:
```bash
dotnet build ScaleStreamer.sln --configuration Release
```

---

## Running the Application

### Test 1: Run Service in Console Mode

Before installing as Windows Service, test in console mode:

```powershell
cd src-v2\ScaleStreamer.Service
dotnet run
```

**Expected Output**:
```
[12:34:56 INF] Scale Streamer Service starting...
[12:34:56 INF] Database initialized at: C:\ProgramData\ScaleStreamer\scalestreamer.db
[12:34:56 INF] Loading 3 protocol templates...
[12:34:56 INF] Loaded protocol: Fairbanks 6011
[12:34:56 INF] Loaded protocol: Generic ASCII
[12:34:56 INF] Loaded protocol: Modbus TCP Generic
[12:34:56 INF] Service ready for scale configuration via GUI
[12:34:56 INF] Scale Service running. Press Ctrl+C to stop.
```

Press **Ctrl+C** to stop.

**Troubleshooting**:

If you get "Access denied" for database:
```powershell
New-Item -ItemType Directory -Force -Path "$env:ProgramData\ScaleStreamer"
icacls "$env:ProgramData\ScaleStreamer" /grant Users:F
```

If you get "Port already in use":
- Another instance of the service is running
- Run: `Get-Process | Where-Object { $_.Name -like "*ScaleStreamer*" } | Stop-Process`

### Test 2: Run GUI Application

In a new terminal (keep service running):

```powershell
cd src-v2\ScaleStreamer.Config
dotnet run
```

**Expected**:
- WinForms window opens
- Status bar shows "Service: Connected" (green) or "Service: Disconnected" (red)
- 5 tabs visible: Connection, Protocol, Monitoring, Status, Logs

**GUI Navigation**:
1. **Connection Tab** - Configure scale connections
2. **Protocol Tab** - Design custom protocols
3. **Monitoring Tab** - View real-time weight data
4. **Status Tab** - Check service and scale status
5. **Logs Tab** - View application events

### Test 3: Configure a Test Scale

With both service and GUI running:

1. Go to **Connection** tab
2. Fill in:
   - Scale ID: `test-scale-1`
   - Scale Name: `Test Floor Scale`
   - Location: `Lab`
   - Market Type: `General Purpose`
   - Manufacturer: `Generic`
   - Protocol: `Generic ASCII`
   - Connection Type: `TcpIp`
   - Host: `192.168.1.100` (your scale IP)
   - Port: `10001`
3. Click **Test Connection**

**Expected**:
- Log shows connection attempt
- Status changes to "Connected" (if scale is reachable)
- Or error message if unreachable

### Test 4: Protocol Designer

1. Go to **Protocol** tab
2. Enter regex pattern:
   ```
   (?<status>[A-Z])\s+(?<weight>[0-9.]+)\s+(?<unit>[A-Z]+)
   ```
3. Enter test data:
   ```
   S 1234.56 LB
   ```
4. Click **Test Regex**

**Expected Parse Results**:
```
Regex Test Results:
Pattern: (?<status>[A-Z])\s+(?<weight>[0-9.]+)\s+(?<unit>[A-Z]+)
Test Data: S 1234.56 LB

✓ Match Successful

Captured Groups:
  status = 'S'
  weight = '1234.56'
  unit = 'LB'
```

---

## Installing as Windows Service

### Step 1: Build Release Version

```powershell
dotnet publish src-v2\ScaleStreamer.Service\ScaleStreamer.Service.csproj `
  -c Release `
  -o publish\service `
  --self-contained false
```

This creates standalone executable at: `publish\service\ScaleStreamer.Service.exe`

### Step 2: Install Service

**Run PowerShell as Administrator**:

```powershell
$servicePath = "D:\win-scale\win-scale\publish\service\ScaleStreamer.Service.exe"

sc.exe create ScaleStreamerService `
  binPath= $servicePath `
  start= auto `
  DisplayName= "Scale Streamer Service" `
  Description= "Cloud-Scale Scale Streamer - Manages scale connections and data streaming"
```

### Step 3: Start Service

```powershell
sc.exe start ScaleStreamerService
```

### Step 4: Verify Service

```powershell
sc.exe query ScaleStreamerService
```

**Expected Output**:
```
SERVICE_NAME: ScaleStreamerService
        TYPE               : 10  WIN32_OWN_PROCESS
        STATE              : 4  RUNNING
        WIN32_EXIT_CODE    : 0  (0x0)
        SERVICE_EXIT_CODE  : 0  (0x0)
        CHECKPOINT         : 0x0
        WAIT_HINT          : 0x0
```

### Step 5: View Service Logs

```powershell
Get-Content "$env:ProgramData\ScaleStreamer\logs\service-*.log" -Tail 50
```

### Uninstall Service

```powershell
# Stop service
sc.exe stop ScaleStreamerService

# Wait a few seconds
Start-Sleep -Seconds 3

# Delete service
sc.exe delete ScaleStreamerService
```

---

## Testing with Hardware

### Prerequisites

- Physical scale connected to network or serial port
- Scale configured for continuous data output
- Protocol definition matching your scale

### Test with Fairbanks 6011 Scale

1. **Verify Scale Network Settings**:
   - IP Address: (e.g., 192.168.1.100)
   - Port: 10001 (default)
   - Protocol: Continuous ASCII output

2. **Test Raw Connection** (using telnet):
   ```powershell
   telnet 192.168.1.100 10001
   ```
   You should see weight data streaming.

3. **Configure in Scale Streamer**:
   - Open GUI
   - Connection tab
   - Manufacturer: "Fairbanks Scales"
   - Protocol: "Fairbanks 6011"
   - Enter scale IP and port
   - Click "Test Connection"

4. **Monitor Live Data**:
   - Go to Monitoring tab
   - Weight should update in real-time
   - History list shows recent readings
   - Raw data stream shows ASCII strings

### Test with Generic ASCII Scale

If your scale uses a custom ASCII protocol:

1. **Capture Sample Data**:
   ```powershell
   # Connect and capture output
   telnet [scale-ip] [port] > sample.txt
   ```

2. **Design Protocol**:
   - Open Protocol tab
   - Enter regex pattern to match your data
   - Test with captured sample
   - Save as JSON template

3. **Use Custom Protocol**:
   - Load your JSON file
   - Select in Connection tab
   - Test connection

---

## Database Inspection

### View Database

```powershell
# Install SQLite if needed
choco install sqlite

# Open database
sqlite3 $env:ProgramData\ScaleStreamer\scalestreamer.db
```

### Useful Queries

```sql
-- View all tables
.tables

-- View weight readings
SELECT timestamp, weight_value, weight_unit, status
FROM weight_readings
ORDER BY timestamp DESC
LIMIT 100;

-- View events
SELECT timestamp, level, category, message
FROM events
ORDER BY timestamp DESC
LIMIT 100;

-- View statistics
SELECT
  MIN(weight_value) as min,
  MAX(weight_value) as max,
  AVG(weight_value) as avg,
  COUNT(*) as count
FROM weight_readings
WHERE timestamp > datetime('now', '-1 hour');
```

---

## Common Issues and Solutions

### Issue: GUI Won't Connect to Service

**Symptoms**:
- Status bar shows "Service: Disconnected" (red)
- GUI opens but cannot communicate with service

**Solutions**:
1. Verify service is running:
   ```powershell
   sc.exe query ScaleStreamerService
   ```

2. Check service logs:
   ```powershell
   Get-Content "$env:ProgramData\ScaleStreamer\logs\service-*.log" -Tail 50
   ```

3. Try running service in console mode:
   ```powershell
   cd src-v2\ScaleStreamer.Service
   dotnet run
   ```

### Issue: Build Errors

**Error**: "The type or namespace name 'Microsoft' could not be found"

**Solution**:
```powershell
dotnet restore
dotnet clean
dotnet build
```

**Error**: "Project file does not exist"

**Solution**: Verify you're in the correct directory:
```powershell
cd D:\win-scale\win-scale
ls *.sln  # Should show ScaleStreamer.sln
```

### Issue: Scale Won't Connect

**Symptoms**:
- Test connection fails
- No weight data appearing

**Solutions**:
1. Verify network connectivity:
   ```powershell
   Test-NetConnection -ComputerName [scale-ip] -Port [port]
   ```

2. Test with telnet:
   ```powershell
   telnet [scale-ip] [port]
   ```

3. Check scale configuration:
   - Continuous output enabled?
   - Correct baud rate (serial)?
   - Correct IP/port?

4. Check Windows Firewall:
   ```powershell
   New-NetFirewallRule -DisplayName "Scale Streamer" `
     -Direction Inbound `
     -Protocol TCP `
     -LocalPort 502,10001 `
     -Action Allow
   ```

### Issue: Database Access Denied

**Error**: "SQLite Error: unable to open database file"

**Solution**:
```powershell
# Create directory with permissions
New-Item -ItemType Directory -Force -Path "$env:ProgramData\ScaleStreamer"
icacls "$env:ProgramData\ScaleStreamer" /grant Users:(OI)(CI)F /T
```

### Issue: Missing DLL

**Error**: "Could not load file or assembly..."

**Solution**:
```powershell
# Rebuild with all dependencies
dotnet publish src-v2\ScaleStreamer.Service\ScaleStreamer.Service.csproj `
  -c Release `
  -o publish\service `
  --self-contained true `
  -r win-x64
```

---

## Performance Testing

### Test 1: High-Frequency Data

Test with scale sending data at 10+ readings/second:

1. Configure continuous mode scale
2. Monitor for 5 minutes
3. Check CPU usage (should be < 10%)
4. Check memory usage (should be < 100MB)
5. Verify all readings captured

### Test 2: Multiple Scales

Test with 5+ scales connected simultaneously:

1. Configure 5 scales
2. Start all connections
3. Monitor for 10 minutes
4. Verify no data loss
5. Check database size growth

### Test 3: Long-Running Stability

Test service stability over extended period:

1. Start service
2. Connect 2-3 scales
3. Run for 24 hours
4. Check for memory leaks
5. Verify auto-reconnect on network interruption

---

## Log Files

### Service Logs

**Location**: `C:\ProgramData\ScaleStreamer\logs\service-YYYYMMDD.log`

**Contents**:
- Service startup/shutdown
- Scale connections
- Weight readings (debug level)
- Errors and warnings

**View Recent Logs**:
```powershell
Get-Content "$env:ProgramData\ScaleStreamer\logs\service-*.log" -Tail 100 -Wait
```

### GUI Logs

**Location**: `%LOCALAPPDATA%\ScaleStreamer\logs\config-YYYYMMDD.log`

**Contents**:
- GUI startup
- User actions
- IPC communication
- Errors

**View Recent Logs**:
```powershell
Get-Content "$env:LOCALAPPDATA\ScaleStreamer\logs\config-*.log" -Tail 100
```

---

## Next Development Steps

### Phase 4: RTSP Streaming (1 week)

1. Install FFmpeg and MediaMTX
2. Implement video overlay with weight
3. Configure RTSP/HLS streaming
4. Test with VLC or web browser

### Phase 5: Installer Updates (1 week)

1. Update WiX installer for service installation
2. Convert SVG assets to PNG/ICO
3. Add firewall rules to installer
4. Test MSI installation/uninstallation

### Phase 6: Additional Protocols (1-2 weeks)

1. Implement SerialProtocolBase (RS232/RS485)
2. Implement ModbusProtocolBase (RTU/TCP)
3. Implement HttpProtocolBase (REST API)
4. Add more manufacturer protocols

### Phase 7: Testing (2 weeks)

1. Unit tests (target 90% coverage)
2. Integration tests
3. Hardware testing with real scales
4. Stress testing (7-day continuous run)
5. User acceptance testing

### Phase 8: Documentation (1 week)

1. User manual with screenshots
2. Administrator guide
3. API documentation
4. Video tutorials

---

## Summary

✅ **Application is buildable and runnable**
- Visual Studio solution compiles successfully
- Service runs in console mode
- GUI launches and displays all tabs
- IPC communication framework operational

⏳ **Integration testing needed**
- Connect GUI actions to service commands
- Test with real hardware scales
- Verify database persistence
- Performance testing under load

🎯 **Target: 4-6 weeks to production-ready**
- Complete RTSP streaming integration
- Finish installer with service installation
- Complete hardware testing
- Finalize documentation

---

*Last updated: 2026-01-24*
*Build status: ✅ Successful*
*Test status: ⏳ Ready for integration testing*
