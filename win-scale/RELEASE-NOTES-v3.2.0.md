# Scale Streamer v3.2.0 - Unified Launcher & Live Monitoring 📊

**Streamlined release with single launcher entry point and verified live scale monitoring!**

## 🚀 What's New in v3.2

### 🎯 Unified Launcher Experience
- **Single Entry Point**: Both Desktop and Start Menu shortcuts now use the smart launcher
- **Consistent Behavior**: Service always starts automatically before GUI opens
- **No Confusion**: Eliminated duplicate shortcuts that bypassed service startup
- **One-Click Access**: Single "Scale Streamer" shortcut in both locations

### 📡 Verified Live Monitoring
- **Real-Time Weight Display**: 48pt bold digital readout updates live from scale
- **Connection Status**: Visual indicators for scale connection state
- **Reading Statistics**: Live update rate and last reading timestamp
- **History Tracking**: Last 100 weight readings with full details
- **Raw Data Stream**: Terminal-style raw data view for debugging
- **RTSP URL Display**: Local IPv4 with click-to-copy for NVR integration

### 🏗️ Enhanced Architecture
- **Launcher-First Design**: All user entry points route through launcher
- **Service Auto-Start**: Guaranteed service availability before GUI access
- **Proper IPC Routing**: Weight readings routed to MonitoringTab via IPC
- **Connection Management**: Automatic reconnection with 5-second retry intervals

## 📦 Installation

1. Download `ScaleStreamer-v3.2.0-YYYYMMDD-HHMMSS.msi` below
2. Run installer (requires Administrator rights)
3. **Upgrading from v3.1 or earlier?** Install directly - all settings preserved!
4. Click "Scale Streamer" shortcut (Desktop or Start Menu) - service starts automatically!

**File Size:** ~59 MB (self-contained with .NET 8.0)

## 🎯 Key Improvements

### Launcher Benefits:
- **Unified Experience**: No more confusion about which shortcut to use
- **Guaranteed Service**: Service always running before GUI opens
- **Single Icon**: Desktop has only one "Scale Streamer" icon
- **Start Menu Consistency**: Start Menu also uses launcher

### Live Monitoring Benefits:
- **Real-Time Updates**: See weight changes as they happen (~10 readings/second)
- **Large Readout**: 48pt font for easy visibility from distance
- **Status Indicators**: Color-coded status (Green=Stable, Orange=Motion, Red=Error)
- **Historical View**: Track weight changes over time
- **Debug Support**: Raw data stream for troubleshooting protocol issues

## 🔧 Technical Details

### Changes from v3.1:
- **Start Menu Shortcut**: Now points to Launcher instead of Config.exe
- **Verified IPC Flow**: WeightReading messages properly routed to MonitoringTab
- **Connection Timer**: 5-second service connection check interval
- **Auto-Reconnect**: Automatic IPC reconnection if service restarts

### Versions:
- Service: 3.2.0
- Config GUI: 3.2.0
- Launcher: 3.2.0
- All components updated to .NET 8.0

### Build Stats:
- Service DLLs: 226
- Config DLLs: 248
- Launcher DLLs: 239
- Installer Size: 59 MB
- Build Time: ~4 minutes

## 🎯 Upgrade Path

### From v3.1 → v3.2
- ✅ Direct upgrade
- ✅ All settings preserved
- ✅ **Fixed**: Start Menu now uses launcher
- ✅ **Verified**: Live weight monitoring working
- ✅ **Improved**: Consistent user experience

### From v3.0 → v3.2
- ✅ Recommended upgrade
- ✅ All settings preserved
- ✅ Gain comprehensive settings tab (v3.1)
- ✅ Gain RTSP URL display (v3.1)
- ✅ Gain unified launcher (v3.2)

### From v2.x → v3.2
- ✅ Major upgrade recommended
- ✅ All settings preserved
- ✅ Gain smart launcher with auto-service-start
- ✅ Gain streamlined GUI
- ✅ Gain comprehensive settings
- ✅ Gain live monitoring dashboard

## 📋 System Requirements

- Windows 10/11 or Windows Server 2016+ (64-bit)
- Administrator rights for installation
- ~200 MB disk space
- Active scale connection for live monitoring

## 📖 Documentation

- [Quick Start Guide](QUICK-START-V2.md) - Get started in 5 minutes
- [Build Instructions](BUILD-AND-TEST-V2.md) - Build from source
- [Fairbanks 6011 TCP Protocol](protocols/manufacturers/fairbanks-6011.json) - Connection specs
- [Complete Documentation](CLAUDE.md) - Master guide

## 🐛 Known Issues

**Windows SmartScreen Warning:**
- Installer shows "Windows protected your PC" warning
- This is normal for unsigned installers
- Click "More info" → "Run anyway"
- To remove: Purchase code signing certificate ($200-500/year)

## 💬 Support

- **Issues**: [Report bugs](../../issues)
- **Email**: support@cloud-scale.us

## 🚀 What's Next (v3.3+)

- **Email Implementation**: Fully functional SMTP email sending
- **Weight Alert Engine**: Active monitoring and alert triggering with notifications
- **Settings Persistence**: Save/load settings from registry or config file
- **Minimize on Startup**: Option to start minimized to system tray
- **Auto-Update Installation**: Automatic silent update installation
- **RTSP Video Streaming**: Actual video stream of weight display
- **REST API**: HTTP endpoints for programmatic weight data access
- **Multi-Scale Support**: Manage multiple scales simultaneously
- **Cloud Sync**: Synchronize weight data to cloud storage
- **Mobile App**: iOS/Android companion application

## 📝 Technical Notes

### GUI Tab Structure (v3.2):
The configuration GUI includes 6 tabs:
1. **Connection** - Scale connection settings (TCP/IP, Serial, etc.)
2. **Protocol** - Data format and parsing configuration
3. **Monitoring** - Live weight dashboard with 48pt readout
4. **Status** - System status and scale list overview
5. **Logs** - Event logging and debugging information
6. **Settings** - Email alerts, thresholds, logging config

### Live Weight Data Flow:
```
Scale Device → Service (IPC Server) → Weight Reading Message →
Config GUI (IPC Client) → MonitoringTab.HandleWeightReading() →
UI Update (48pt label, history list, raw data text)
```

### Launcher Behavior:
```
User clicks shortcut → Launcher.exe starts →
Check service status → Start service if stopped →
Wait for service (10s timeout) → Launch Config.exe →
Config connects to service via IPC → Live data flows
```

---

**Full Changelog:** https://github.com/cloud-scale-us/Cloud-Scale/compare/v3.1.0...v3.2.0

**Tested On:** Windows 10, Windows 11
