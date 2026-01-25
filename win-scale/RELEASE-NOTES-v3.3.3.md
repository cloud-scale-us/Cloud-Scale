# Scale Streamer v3.3.3 - Protocols Path Fix & Diagnostics

**Release Date:** January 25, 2026

## 🔧 Critical Bug Fixes

### Protocols Path Resolution (CRITICAL FIX)
Fixed service failing to load protocol templates due to incorrect path calculation:

**Before (BROKEN):**
```
[WRN] Protocols directory not found: C:\Program Files\Scale Streamer\Service\protocols
```

**After (FIXED):**
```
[INF] Protocols path resolved to: C:\Program Files\Scale Streamer\protocols
[INF] Found 3 protocol template files in C:\Program Files\Scale Streamer\protocols
[INF] Loaded protocol template: Fairbanks 6011 v1.1
[INF] Loaded protocol template: Generic ASCII v1.0
[INF] Loaded protocol template: Modbus TCP v1.0
```

**Technical Details:**
- Replaced `Directory.GetParent()` with `Path.GetDirectoryName()` for more reliable parent directory resolution
- Added path trimming to handle trailing separators correctly
- Added debug logging to show resolved protocols path on startup

## 📊 Enhanced Diagnostics

### New Diagnostic Tools

**1. Comprehensive Diagnostics Collector** (`collect-diagnostics.ps1`)
Automated PowerShell script that collects ALL diagnostic information:
- System information and .NET runtime
- Network configuration and connectivity tests
- Live TCP connection test to scale (10.1.10.210:5001)
- Service status and uptime
- Installation paths and file structure
- Protocol files and content
- Database structure and configured scales
- Service logs (last 100 lines)
- Registry settings and firewall rules
- Running processes

**Usage:**
```powershell
cd "C:\Program Files\Scale Streamer"
.\collect-diagnostics.ps1
# Copy ALL output and paste to Claude for analysis
```

**2. Diagnostics Instructions** (`DIAGNOSTICS-INSTRUCTIONS.md`)
Complete guide for troubleshooting including:
- Manual log locations
- Common issues and quick checks
- Network connectivity testing
- Scale configuration steps
- Expected data formats for Fairbanks 6011

### Service Logging Improvements
- Added logging for protocols path resolution
- Added logging for each protocol template loaded
- Added debug logging for protocol file discovery
- Enhanced startup logging for troubleshooting

## 🐛 Known Issues

### Logging Tab Shows Only Sample Data
The **Logging** tab currently shows only 3 hardcoded sample events and does not read actual service logs.

**Workarounds:**
1. Use the **Diagnostics** tab for live TCP data, connection logs, and IPC messages
2. Manually view logs at: `C:\ProgramData\ScaleStreamer\logs\service-YYYYMMDD.log`
3. Run `collect-diagnostics.ps1` for comprehensive log collection

**Will be fixed in:** v3.3.4

## 📋 All 7 Tabs Included

1. **Connection** - Scale connection configuration (TCP/IP, Serial)
2. **Protocol** - Protocol selection and parsing rules
3. **Monitoring** - Live weight display (48pt digital readout) with RTSP URL
4. **Status** - Service and connection status with controls
5. **Logs** - Event log viewer *(shows sample data only in v3.3.3)*
6. **Settings** - Comprehensive configuration (email alerts, thresholds, auto-reconnect)
7. **Diagnostics** - **LIVE** TCP data, connection log, IPC messages, errors

## 🔗 Fairbanks 6011 Support

Fully supports Fairbanks 6011 scales via TCP/IP:

**Protocol File:** `C:\Program Files\Scale Streamer\protocols\manufacturers\fairbanks-6011.json`

**Data Format:**
```
1     960    00    → 960.0 LB (stable)
2     965    00    → 965.0 LB (motion)
1"   -500    00    → -500.0 LB (stable negative)
```

**Status Codes:**
- `1` = Stable weight
- `2` = Weight in motion
- `1"` = Stable negative weight

## 🚀 Quick Start

### For First-Time Setup

1. **Install v3.3.3:**
   ```cmd
   msiexec /i ScaleStreamer-v3.3.3-20260125-173236.msi
   ```

2. **Configure Scale at 10.1.10.210:5001:**
   - Open Scale Streamer Configuration (Desktop icon)
   - Go to **Connection** tab
   - Fill in:
     ```
     Scale ID: scale1
     Scale Name: Main Fairbanks Scale
     Host: 10.1.10.210
     Port: 5001
     Protocol: Fairbanks 6011
     ☑ Auto-reconnect
     ```
   - Click **Save**
   - Click **Test Connection**

3. **View Live Data:**
   - Go to **Diagnostics** tab → **Live TCP Data**
   - You should see: `1     960    00` format data

4. **Troubleshoot Issues:**
   ```powershell
   cd "C:\Program Files\Scale Streamer"
   .\collect-diagnostics.ps1
   ```
   Copy all output and paste to Claude

### For Upgrades from v3.3.2

Your scale configurations are preserved. Just install and verify:
- Service starts automatically
- Protocols load successfully (check service log)
- Scale reconnects automatically

## 📦 Installation Notes

- **Self-Contained:** Includes .NET 8.0 runtime, no prerequisites needed
- **File Size:** 59 MB
- **Upgrade:** Automatically upgrades from v3.0.0-3.3.2
- **Firewall:** Automatically adds rules for RTSP (8554) and HLS (8888)
- **Service:** Auto-starts as LocalSystem with restart recovery

## 📞 Support

- **Logs:** `C:\ProgramData\ScaleStreamer\logs\`
- **Database:** `C:\ProgramData\ScaleStreamer\scalestreamer.db`
- **Diagnostics:** `collect-diagnostics.ps1` in install directory
- **Email:** admin@cloud-scale.us
- **Web:** https://cloud-scale.us/support

## 🔄 Version History

- **v3.3.3** - Protocols path fix, enhanced diagnostics
- **v3.3.2** - Tab visibility improvements
- **v3.3.1** - Added Diagnostics tab with live TCP monitoring
- **v3.3.0** - GUI stability improvements
- **v3.2.0** - Unified launcher and tab fixes
- **v3.1.0** - Settings tab, monitoring enhancements, Fairbanks protocol update
