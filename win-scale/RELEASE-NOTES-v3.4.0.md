# Scale Streamer v3.4.0 - Built-in Diagnostics & Real-Time Logging

**Release Date:** January 25, 2026

## 🎉 Major New Features

### 1. Built-in Diagnostic Tools
The diagnostic collector script is now **included in the installer** at:
```
C:\Program Files\Scale Streamer\collect-diagnostics.ps1
```

**Usage:**
```powershell
cd "C:\Program Files\Scale Streamer"
.\collect-diagnostics.ps1
```

The script automatically collects:
- ✅ System and .NET runtime information
- ✅ Network configuration and routing
- ✅ **Live TCP connectivity test** to scale (10.1.10.210:5001)
- ✅ Actual TCP data from scale (if connected)
- ✅ Service status and uptime
- ✅ Protocol files and content
- ✅ Database structure and configured scales
- ✅ Service logs (last 100 lines)
- ✅ Registry settings and firewall rules

**Just copy ALL output and paste to support/Claude for instant analysis!**

### 2. Real-Time Logging Tab (FIXED!)

The **Logs** tab now **actually works**! 🎊

**Before (v3.3.3):**
- Showed only 3 hardcoded sample events
- No connection to real service logs

**After (v3.4.0):**
- ✅ Reads actual service logs from `C:\ProgramData\ScaleStreamer\logs\`
- ✅ Automatically loads last 100 log entries on startup
- ✅ Parses Serilog format with timestamp, level, and message
- ✅ Color-coded by severity (Gray=Debug, Black=Info, Orange=Warning, Red=Error, Dark Red=Fatal)
- ✅ Auto-categorizes by content (Service, ScaleConnection, Database, IPC)
- ✅ **Refresh button** reloads latest logs
- ✅ Shows which log file is being displayed
- ✅ Works even if service logs don't exist yet (shows warning)

**Log Format Support:**
```
2026-01-24 21:12:07.159 -05:00 [INF] Scale Streamer Service starting...
                                 ^^^^^
                                 Serilog levels: DBG, INF, WRN, ERR, FTL
```

### 3. Comprehensive Documentation

**Included in installer at** `C:\Program Files\Scale Streamer\docs\`:
- `DIAGNOSTICS-INSTRUCTIONS.md` - Complete troubleshooting guide
- `QUICK-START-V2.md` - Getting started guide
- `BUILD-AND-TEST-V2.md` - Developer guide
- `V2-UNIVERSAL-ARCHITECTURE.md` - Technical architecture

## 🔧 Technical Improvements

### Logging Tab Implementation
- Added `LoadServiceLogs()` method to read log files from ProgramData
- Added `ParseLogLine()` method to parse Serilog format
- Auto-finds today's log file, falls back to most recent if needed
- Runs in background thread to avoid UI blocking
- Handles missing directories and files gracefully

### Installer Enhancements
- Added `DiagnosticComponents` component group
- Diagnostic script installed to main folder for easy access
- Documentation updated with diagnostics instructions

## 📋 All 7 Tabs - ALL FUNCTIONAL

1. **Connection** - Scale connection configuration
2. **Protocol** - Protocol selection and parsing rules
3. **Monitoring** - Live 48pt weight display + RTSP URL
4. **Status** - Service and connection status with controls
5. **Logs** - **NOW SHOWS REAL SERVICE LOGS!** ⭐
6. **Settings** - Comprehensive configuration options
7. **Diagnostics** - Live TCP data, connection log, IPC messages, errors

## 🚀 Quick Start

### First-Time Installation

1. **Install v3.4.0:**
   ```cmd
   msiexec /i ScaleStreamer-v3.4.0-20260125-180725.msi
   ```

2. **View Service Logs:**
   - Open Scale Streamer Configuration
   - Go to **Logs** tab
   - See last 100 service log entries
   - Click **Refresh** to reload

3. **Run Diagnostics:**
   ```powershell
   cd "C:\Program Files\Scale Streamer"
   .\collect-diagnostics.ps1
   # Copy ALL output for analysis
   ```

4. **Configure Scale:**
   - Go to **Connection** tab
   - Add scale: `10.1.10.210:5001`, protocol `Fairbanks 6011`
   - Click **Save** → **Test Connection**
   - Go to **Diagnostics** → **Live TCP Data** to see stream

### Upgrading from v3.3.3

Your configurations are preserved. After installing v3.4.0:

1. **Check Logs Tab** - Should now show real service logs
2. **Run Diagnostics** - Script is now in `C:\Program Files\Scale Streamer\`
3. **Verify Protocols** - Check service log shows:
   ```
   [INF] Protocols path resolved to: C:\Program Files\Scale Streamer\protocols
   [INF] Found 3 protocol template files
   [INF] Loaded protocol template: Fairbanks 6011 v1.1
   ```

## 🐛 Bug Fixes from v3.3.3

- ✅ **Fixed:** Logging tab now reads actual service logs instead of sample data
- ✅ **Fixed:** Diagnostic script now included in installer (no more "file not found")
- ✅ **Fixed:** Better error handling when log directories don't exist
- ✅ **Fixed:** Log parsing handles malformed lines gracefully

## 📦 Installation Details

- **Self-Contained:** Includes .NET 8.0 runtime (59 MB)
- **Upgrade:** Automatically upgrades from v3.0.0-3.3.3
- **Firewall:** Auto-creates rules for RTSP (8554) and HLS (8888)
- **Service:** Auto-starts as LocalSystem with restart recovery
- **Diagnostics:** Script and docs included in installation

## 🔍 Troubleshooting

### Logs Tab Shows "No service log files found"
1. Service hasn't started yet - start the service
2. Check `C:\ProgramData\ScaleStreamer\logs\` exists
3. Run diagnostics script to verify installation

### Diagnostic Script Not Found
1. Verify installation: `dir "C:\Program Files\Scale Streamer\collect-diagnostics.ps1"`
2. Reinstall v3.4.0 if missing
3. Script should be in main install folder, not Service subfolder

### Scale Not Connecting
1. Open **Logs** tab - check for connection errors
2. Run `collect-diagnostics.ps1` - includes network tests
3. Go to **Diagnostics** → **Live TCP Data** for raw stream
4. Check service log shows protocols loaded correctly

## 📞 Support

- **Logs:** `C:\ProgramData\ScaleStreamer\logs\`
- **Database:** `C:\ProgramData\ScaleStreamer\scalestreamer.db`
- **Diagnostics:** `C:\Program Files\Scale Streamer\collect-diagnostics.ps1`
- **Docs:** `C:\Program Files\Scale Streamer\docs\`
- **Email:** admin@cloud-scale.us
- **Web:** https://cloud-scale.us/support

## 🔄 Version History

- **v3.4.0** - Built-in diagnostics, real-time logging tab
- **v3.3.3** - Protocols path fix, enhanced diagnostics
- **v3.3.2** - Tab visibility improvements
- **v3.3.1** - Added Diagnostics tab with live TCP monitoring
- **v3.3.0** - GUI stability improvements
- **v3.2.0** - Unified launcher and tab fixes
- **v3.1.0** - Settings tab, monitoring enhancements

## 🎁 What's Next?

Future enhancements being considered:
- Auto-refresh for Logs tab (currently manual refresh)
- Filtering and search in Logs tab
- Export logs to CSV/JSON
- Log level filtering (currently shows all levels)
- Multi-file log viewing (currently shows one file)

**Your feedback helps shape the roadmap!**
