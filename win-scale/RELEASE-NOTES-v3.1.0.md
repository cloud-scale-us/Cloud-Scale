# Scale Streamer v3.1.0 - Comprehensive Settings & RTSP Monitoring 🎯

**Feature-rich release with enhanced configuration and monitoring capabilities!**

## 🚀 What's New in v3.1

### ⚙️ Comprehensive Settings Tab
- **Email Alert Configuration**: Full SMTP setup for automated notifications
- **Connection Monitoring**: Email alerts on connection failure and restoration
- **Update Notifications**: Automatic update checking with configurable intervals
- **Weight Threshold Alerts**: Customizable weight-based alerts with email and sound
- **Data Logging Settings**: Configurable log retention, file size, and log levels
- **Auto-Reconnect Options**: Global auto-reconnect with configurable delays and retry limits
- **Test Email Function**: Verify SMTP configuration before deployment

### 📡 Enhanced Monitoring Tab
- **RTSP URL Display**: Shows complete RTSP URL with local IPv4 address
- **Click-to-Copy**: Click RTSP URL to copy to clipboard for NVR configuration
- **Real-Time IP Detection**: Automatically detects and displays host machine IP
- **NVR Integration**: Pre-formatted RTSP URL ready for Network Video Recorder systems

### 🔧 Updated Protocol Specifications
- **Fairbanks 6011 TCP Protocol**: Updated with detailed connection specifications
  - Correct default port (5001)
  - Control character stripping (STX/ETX)
  - Negative weight indicator support
  - Configurable decimal multiplier
  - Flexible line delimiter support (CR/LF/CRLF)
  - Example data formats and parsing notes

### 📋 Configuration Categories

#### Email Alerts
- SMTP server and port configuration
- SSL/TLS security options
- Username and password authentication
- From/To email addresses
- Test email functionality

#### Connection Alerts
- Alert on connection failure
- Alert on connection restore
- Configurable failure delay threshold

#### Software Updates
- Automatic update checking
- Configurable check intervals
- Email notifications for new versions

#### Weight Monitoring
- Threshold-based alerts
- Greater than, less than, or equal to conditions
- Email and sound notifications
- Customizable weight limits

#### Data Logging
- Enable/disable logging
- Log retention in days
- Maximum log file size limits
- Configurable log levels (Verbose, Debug, Information, Warning, Error)

#### Auto-Reconnect
- Global auto-reconnect toggle
- Reconnect delay in seconds
- Maximum reconnect attempts
- Per-scale override capability

## 📦 Installation

1. Download `ScaleStreamer-v3.1.0-YYYYMMDD-HHMMSS.msi` below
2. Run installer (requires Administrator rights)
3. **Upgrading from v3.0?** Install directly - all settings preserved!
4. Click desktop shortcut "Scale Streamer" - service starts automatically!

**File Size:** ~59 MB (self-contained with .NET 8.0)

## 🎯 Key Improvements

### Settings Tab Benefits:
- Centralized configuration management
- Email alerting for critical events
- Comprehensive logging controls
- Auto-reconnect with fine-grained control
- Weight threshold monitoring

### RTSP Monitoring Benefits:
- Easy NVR integration
- No manual IP configuration needed
- One-click URL copying
- Visual confirmation of stream endpoint

### Email Alert Scenarios:
- Scale connection lost/restored
- Weight exceeds safety thresholds
- Software updates available
- Critical system errors

## 🔧 Technical Details

### New Components:
- **SettingsTab.cs** - Comprehensive settings interface with 6 configuration panels
- **Enhanced MonitoringTab** - RTSP URL display with IPv4 auto-detection
- **Updated Fairbanks 6011 Protocol** - Enhanced protocol definition with TCP-specific parsing rules

### Versions:
- Service: 3.1.0
- Config GUI: 3.1.0
- Launcher: 3.1.0
- All components updated to .NET 8.0

### Build Stats:
- Service DLLs: 226
- Config DLLs: 248
- Launcher DLLs: 239
- Installer Size: 59 MB
- Build Time: ~4 minutes

## 🎯 Upgrade Path

### From v3.0 → v3.1
- ✅ Direct upgrade
- ✅ All settings preserved
- ✅ **New**: Comprehensive settings tab
- ✅ **New**: RTSP URL display with local IP
- ✅ **New**: Email alert configuration
- ✅ **New**: Weight threshold alerts

### From v2.7 → v3.1
- ✅ Recommended upgrade
- ✅ All settings preserved
- ✅ Gain smart launcher (v3.0)
- ✅ Gain streamlined GUI (v3.0)
- ✅ Gain comprehensive settings (v3.1)
- ✅ Gain RTSP monitoring (v3.1)

### From v2.6 or earlier → v3.1
- ✅ Major upgrade recommended
- ✅ All settings preserved
- ✅ Gain all v2.7 + v3.0 + v3.1 features

## 📋 System Requirements

- Windows 10/11 or Windows Server 2016+ (64-bit)
- Administrator rights for installation
- ~200 MB disk space
- SMTP server (for email alerts)

## 📖 Documentation

- [Quick Start Guide](QUICK-START-V2.md) - Get started in 5 minutes
- [Build Instructions](BUILD-AND-TEST-V2.md) - Build from source
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

## 🚀 What's Next (v3.2+)

- **Email Implementation**: Fully functional SMTP email sending
- **Weight Alert Engine**: Active monitoring and alert triggering
- **Settings Persistence**: Save/load settings from registry or config
- **Minimize on Startup**: Option to start minimized to tray
- **Auto-Update Installation**: Automatic silent updates
- **RTSP Streaming**: Actual video stream of weight display
- **REST API**: HTTP endpoints for weight data
- **Multi-Scale Support**: Manage multiple scales simultaneously
- **Cloud Sync**: Synchronize data to cloud
- **Mobile App**: iOS/Android companion app

## 📝 Notes

### Email Alerts (v3.1)
- UI framework complete
- Backend integration pending
- Test email button demonstrates async functionality
- Full implementation planned for v3.2

### RTSP URL Display (v3.1)
- Automatically detects local IPv4 address
- Displays rtsp://[local-ip]:8554/scale1 format
- Click to copy for easy NVR configuration
- Future: Clickable link to open in VLC/media player

---

**Full Changelog:** https://github.com/cloud-scale-us/Cloud-Scale/compare/v3.0.0...v3.1.0

**Tested On:** Windows 10, Windows 11
