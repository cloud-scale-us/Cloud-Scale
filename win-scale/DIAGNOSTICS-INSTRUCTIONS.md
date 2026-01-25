# Scale Streamer Diagnostics Instructions

## Quick Log Collection

### Option 1: Automated Script (RECOMMENDED)
1. Open PowerShell as Administrator
2. Navigate to install directory:
   ```powershell
   cd "C:\Program Files\Scale Streamer"
   ```
   Or if running from source:
   ```powershell
   cd "D:\win-scale\win-scale"
   ```
3. Run the diagnostic collector:
   ```powershell
   .\collect-diagnostics.ps1
   ```
4. **COPY ALL OUTPUT** from PowerShell window (Ctrl+A, then Ctrl+C)
5. Paste into Claude for analysis

### Option 2: Manual Log Locations

**Service Logs:**
```
C:\ProgramData\ScaleStreamer\logs\service-YYYYMMDD.log
```

**Database:**
```
C:\ProgramData\ScaleStreamer\scalestreamer.db
```

**Protocol Files:**
```
C:\Program Files\Scale Streamer\protocols\manufacturers\fairbanks-6011.json
C:\Program Files\Scale Streamer\protocols\generic\*.json
```

**Installation Directory:**
```
C:\Program Files\Scale Streamer\
├── Service\           (Service executable and runtime)
├── Config\            (Configuration GUI)
├── Launcher\          (Launcher executable)
├── protocols\         (Protocol definitions)
└── docs\              (Documentation)
```

## Common Issues & Quick Checks

### Issue: No TCP Data from Scale

**Check 1: Network Connectivity**
```powershell
# Ping the scale
ping 10.1.10.210

# Test TCP port
Test-NetConnection -ComputerName 10.1.10.210 -Port 5001
```

**Check 2: Service Running**
```powershell
Get-Service ScaleStreamerService
```

**Check 3: Scale Configured**
1. Open Scale Streamer Configuration (Desktop icon)
2. Go to **Connection** tab
3. Verify scale is configured with:
   - Scale ID: `scale1`
   - Host: `10.1.10.210`
   - Port: `5001`
   - Protocol: `Fairbanks 6011`
4. Click **Save**
5. Click **Test Connection**

### Issue: GUI Tabs Empty / No Logs

The **Diagnostics** tab shows real-time data. If it's empty:

1. Ensure service is running:
   ```powershell
   Restart-Service ScaleStreamerService
   ```

2. Ensure a scale is configured and enabled (see above)

3. Check service logs manually:
   ```powershell
   notepad "C:\ProgramData\ScaleStreamer\logs\service-$(Get-Date -Format 'yyyyMMdd').log"
   ```

### Issue: Protocols Not Loading

Check service log for:
```
[WRN] Protocols directory not found: C:\Program Files\Scale Streamer\Service\protocols
```

If you see this, the protocols path bug is present. Install v3.3.3 to fix.

Expected log after v3.3.3:
```
[INF] Protocols path resolved to: C:\Program Files\Scale Streamer\protocols
[INF] Found 3 protocol template files in C:\Program Files\Scale Streamer\protocols
[INF] Loaded protocol template: Fairbanks 6011 v1.1
```

## Manual Scale Configuration

If you need to manually add the scale at 10.1.10.210:5001:

1. Open Scale Streamer Configuration
2. Go to **Connection** tab
3. Fill in:
   ```
   Scale ID: scale1
   Scale Name: Main Fairbanks Scale
   Location: Production Floor

   Market Type: Industrial
   Manufacturer: Fairbanks
   Protocol: Fairbanks 6011

   Connection Type: TCP/IP
   Host: 10.1.10.210
   Port: 5001
   Timeout: 5000

   ☑ Auto-reconnect
   Reconnect Interval: 10 seconds
   ```
4. Click **Save**
5. Click **Test Connection**
6. Go to **Diagnostics** tab → **Live TCP Data** to see raw data

## Expected Data Format

From Fairbanks 6011 at 10.1.10.210:5001:

```
1     960    00    (= 960.0 LB, stable)
2     965    00    (= 965.0 LB, motion)
1"   -500    00    (= -500.0 LB, stable negative)
```

Status codes:
- `1` = Stable
- `2` = Motion
- `1"` = Stable negative

## Getting Help

1. Run `collect-diagnostics.ps1` and copy ALL output
2. Paste into Claude with description of issue
3. Include any error messages from GUI or logs
