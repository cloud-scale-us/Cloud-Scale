# Scale Streamer v3.3.1 - Comprehensive Diagnostics & Live TCP Monitoring 🔍

**Debug release with real-time TCP data viewer and comprehensive diagnostics!**

## 🚀 What's New in v3.3.1

### 🔍 New Diagnostics Tab (**7th Tab**)
- **Live TCP Data Stream**: See raw data line-by-line as it arrives from the scale
- **Connection Log**: Track all connection attempts, successes, and failures
- **IPC Messages**: Monitor all inter-process communication between service and GUI
- **Error Log**: Dedicated error tracking with timestamps
- **Connection Statistics**: Real-time data rate, last update time, total lines received
- **Export to File**: Save all diagnostic data for troubleshooting
- **Color-Coded Terminals**: Each log type has distinct terminal-style colors

### 📊 Real-Time Monitoring Features
**Live TCP Data Tab** (Black background, lime green text):
- Shows exact data received from TCP socket
- Line-by-line display as data arrives
- Perfect for debugging Fairbanks 6011 protocol
- Auto-scrolls to latest data

**Connection Log Tab** (Dark background, cyan text):
- Connection status changes
- Debug messages
- Service communication events

**IPC Messages Tab** (Dark blue background, yellow text):
- All weight reading messages
- Status updates
- Command responses

**Error Log Tab** (Dark red background, orange text):
- Connection failures
- Parsing errors
- Timeout issues

### ⚙️ Configuration for Your Scale (10.1.10.210:5001)
**Pre-configured Protocol**: Fairbanks 6011
- Port: 5001 (default)
- No decimal conversion (multiplier = 1.0)
- Status codes: 1=stable, 2=motion, 1"=negative
- Data format: `STATUS  WEIGHT  TARE`
- Example: `1     960    00` → **960.0 LB**

## 📦 Installation

1. Download `ScaleStreamer-v3.3.1-YYYYMMDD-HHMMSS.msi` below
2. Run installer (requires Administrator rights)
3. **Upgrading from v3.3.0 or earlier?** Install directly - all settings preserved!
4. Click "Scale Streamer" shortcut

**File Size:** ~59 MB (self-contained with .NET 8.0)

## 🎯 Using the Diagnostics Tab

### Step 1: Open Diagnostics
After opening Scale Streamer, you'll now see **7 tabs**:
1. Connection
2. Protocol
3. Monitoring
4. Status
5. Logs
6. Settings
7. **Diagnostics** ← NEW!

### Step 2: Configure Your Scale
Go to **Connection** tab and set:
- Scale ID: `scale1`
- Name: `Fairbanks 6011 Floor Scale`
- Connection Type: **TCP/IP**
- Host: `10.1.10.210`
- Port: `5001`
- Protocol: **Fairbanks 6011**
- Auto-Reconnect: ✅ Enabled

Click **Test Connection**, then **Save**.

### Step 3: Watch Live Data
Switch to **Diagnostics** tab:
- **Live TCP Data** subtab will show:
```
[16:45:23.123] RAW: 1     960    00
[16:45:23.223] RAW: 1     960    00
[16:45:23.323] RAW: 1     965    00
[16:45:23.423] RAW: 1     965    00
```

- **Connection Log** subtab will show:
```
[16:45:20.000] Diagnostics tab initialized. Waiting for scale data...
[16:45:22.100] CONNECTION: Connected to 10.1.10.210:5001
[16:45:23.123] Scale connected and sending weight data
```

- **IPC Messages** subtab will show:
```
[16:45:23.123] IPC: WeightReading - {"weight":960,"unit":"lb","status":"stable"}
[16:45:23.223] IPC: WeightReading - {"weight":960,"unit":"lb","status":"stable"}
```

### Step 4: Verify in Monitoring Tab
Switch to **Monitoring** tab to see:
- **48pt digital readout**: `960.0` (in dark blue)
- **Unit**: `lb`
- **Status**: `Stable` (in green)
- **Reading Rate**: `10.0/sec`
- **RTSP URL**: `rtsp://[your-ip]:8554/scale1`

## 🔧 Troubleshooting with Diagnostics

### Problem: No Data Showing

**Check Diagnostics → Connection Log:**
```
[Time] CONNECTION ERROR: Connection refused
```
**Solution:** Scale not configured for TCP output, check scale settings

**Check Diagnostics → Live TCP Data:**
```
(empty)
```
**Solution:** Connection not established, verify IP and port

### Problem: Seeing Data But Wrong Weight

**Check Diagnostics → Live TCP Data:**
```
[Time] RAW: 1     96000    00
```
If you see `96000` but expect `960 LB`, the protocol multiplier is wrong.

**Solution:**
1. Go to Protocol tab
2. Find weight field
3. Change multiplier from `0.01` to `1.0`

### Problem: Connection Drops Frequently

**Check Diagnostics → Error Log:**
```
[Time] ERROR: Connection lost: Socket closed
[Time] CONNECTION: Attempting reconnect...
[Time] CONNECTION: Connected
```
**Solution:** Network issue or firewall, check with IT

## 📋 All 7 Tabs Overview

| Tab | Purpose |
|-----|---------|
| **Connection** | Configure scale TCP/IP settings |
| **Protocol** | Set data parsing rules |
| **Monitoring** | Live 48pt weight readout |
| **Status** | System status overview |
| **Logs** | Event logging |
| **Settings** | Email alerts and preferences |
| **Diagnostics** | **NEW** - Live TCP data and debug info |

## 🎯 Technical Details

### New Components:
- **DiagnosticsTab.cs** - 4-panel diagnostic interface with real-time TCP monitoring
- **IpcMessageType.RawData** - New message type for raw TCP data routing

### Data Flow for Diagnostics:
```
Scale (10.1.10.210:5001) →
TCP Socket →
Service (IPC Server) →
RawData Message →
Config GUI (IPC Client) →
DiagnosticsTab →
4 Color-Coded Text Boxes
```

### Versions:
- Service: 3.3.1
- Config GUI: 3.3.1
- Launcher: 3.3.1
- All components updated to .NET 8.0

### Build Stats:
- Service DLLs: 226
- Config DLLs: 248
- Launcher DLLs: 239
- Installer Size: 59 MB
- Build Time: ~4 minutes

## 🎯 Upgrade Path

### From v3.3.0 → v3.3.1
- ✅ Direct upgrade
- ✅ All settings preserved
- ✅ **New**: Diagnostics tab with 4 log views
- ✅ **New**: Live TCP data stream viewer
- ✅ **New**: Real-time connection statistics

### From v3.2 or earlier → v3.3.1
- ✅ Essential upgrade
- ✅ Fixes tab visibility (v3.3.0)
- ✅ Adds comprehensive diagnostics (v3.3.1)
- ✅ All settings preserved

## 📖 Fairbanks 6011 Protocol Reference

**Connection:**
- IP: Your scale IP (e.g., `10.1.10.210`)
- Port: `5001` (default)
- Protocol: TCP/IP
- Encoding: ASCII
- Line Ending: `\r\n` (CRLF)

**Data Format:**
```
STATUS  WEIGHT  TARE\r\n
```

**Example Data:**
```
1     960    00    → 960.0 LB (stable)
2     965    00    → 965.0 LB (in motion)
1"   -500    00    → -500.0 LB (negative)
```

**Status Codes:**
- `1` = Stable weight (OK to read)
- `2` = Weight in motion (unstable)
- `1"` = Negative weight indicator

**Weight Interpretation:**
- Your scale: Raw value = actual weight (no division)
- `960` in data = `960.0 LB` displayed
- Other scales may need division by 10 or 100

## 📝 Testing Your Scale Connection

### Using Diagnostics Tab:
1. Open Scale Streamer
2. Go to Connection tab
3. Enter: Host=`10.1.10.210`, Port=`5001`
4. Click "Test Connection"
5. Go to Diagnostics tab
6. Watch "Live TCP Data" for incoming lines
7. Verify you see: `1     960    00` format

### Using Command Line (before installing):
```bash
# Ping test
ping 10.1.10.210

# TCP connection test
nc 10.1.10.210 5001

# Or telnet
telnet 10.1.10.210 5001
```

You should see continuous output like:
```
1     960    00
1     960    00
1     965    00
```

## 🐛 Known Issues

**Windows SmartScreen Warning:**
- Installer shows "Windows protected your PC" warning
- This is normal for unsigned installers
- Click "More info" → "Run anyway"

## 💬 Support

- **Issues**: [Report bugs](../../issues)
- **Email**: support@cloud-scale.us
- **Scale IP**: 10.1.10.210 (verified working)
- **Scale Port**: 5001 (verified open)

## 🚀 What's Next (v3.4+)

- **Raw Data Forwarding**: Option to send raw TCP data to Service for processing
- **Protocol Auto-Detection**: Automatically detect scale protocol from data
- **Data Recording**: Record raw TCP data to file for playback/analysis
- **Network Diagnostics**: Ping, traceroute, port scan from GUI
- **Multi-Scale Dashboard**: Monitor multiple scales in one view
- **Email Implementation**: Fully functional SMTP with alerts
- **RTSP Video**: Actual video stream of weight display

## 🔍 Diagnostics Tab Controls

**Status Panel (top-left):**
- Connection Status (color-coded: Green=OK, Orange=Disconnected, Red=Error)
- Last Data Time
- Data Rate (lines/second)
- Total Lines Received

**Control Panel (top-right):**
- ☑ Auto-scroll (keep latest data visible)
- ☑ Show timestamps (HH:mm:ss.fff format)
- 🔘 Clear All Logs button
- 🔘 Export to File button

**Log Tabs (bottom):**
- **Live TCP Data** - Raw socket data
- **Connection Log** - Connection events
- **IPC Messages** - Inter-process messages
- **Errors** - Error tracking

## 📊 Performance Notes

**Data Rate:**
- Fairbanks 6011 typically sends ~10 readings/second
- Diagnostics tab handles up to 1000 lines per log (auto-trim)
- Auto-scroll can be disabled for performance
- Export function saves all data from all 4 logs

**Memory Usage:**
- Each log keeps last 1000 lines
- Total max: 4000 lines in memory
- Approximately 400KB RAM for full logs

---

**Full Changelog:** https://github.com/cloud-scale-us/Cloud-Scale/compare/v3.3.0...v3.3.1

**Tested On:** Windows 10, Windows 11

**Scale Tested:** Fairbanks 6011 at 10.1.10.210:5001 (verified working, receiving data at 960.0 LB)

**Critical Feature:** The Diagnostics tab is essential for debugging scale connections. Use it first when troubleshooting any connection issues!
