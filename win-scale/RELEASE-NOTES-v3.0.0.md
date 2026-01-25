# Scale Streamer v3.0.0 - Smart Launcher & Streamlined GUI 🚀

**Major release with intelligent service launcher and redesigned interface!**

## 🚀 What's New in v3.0

### ⚡ Smart Launcher
- **One-Click Start**: Desktop shortcut now uses smart launcher that automatically ensures service is running
- **Service Auto-Start**: Launcher checks service status and starts it if needed
- **Error Handling**: Graceful messages if service not installed or fails to start
- **Seamless Experience**: No more manual service management for most users

### 🎨 Streamlined Interface
- **Cleaner GUI**: Removed cluttered service status bar from bottom
- **All Tabs Intact**: Connection, Protocol, Monitoring, Status, and Logs tabs all preserved
- **Professional Look**: Simplified, modern interface
- **System Tray**: Full service control still available via tray icon

### 📥 Enhanced Updates
- **Direct Downloads**: MSI files download directly in app (no browser redirect)
- **Progress Dialog**: Real-time download progress with percentage
- **Instant Install**: Option to install immediately after download
- **Cancellable**: Stop download at any time

## 📦 Installation

1. Download `ScaleStreamer-v3.0.0-YYYYMMDD-HHMMSS.msi` below
2. Run installer (requires Administrator rights)
3. **Upgrading from v2.x?** Install directly - all settings preserved!
4. Click desktop shortcut "Scale Streamer" - service starts automatically!

**File Size:** ~58 MB (self-contained with .NET 8.0)

## 🎯 Key Improvements

### Smart Launcher Benefits:
- No need to manually start service
- Desktop shortcut handles everything
- Service status checked before GUI opens
- Automatic service startup if stopped

### Interface Cleanup:
- Removed redundant service status bar
- More screen space for configuration tabs
- Cleaner, professional appearance
- System tray provides full service control

### Update Experience:
- No more browser redirects
- Download progress visible
- Install immediately or later
- Much more convenient!

## 🔧 Technical Details

### New Components:
- **ScaleStreamer.Launcher.exe** - Smart service launcher
- System.ServiceProcess.ServiceController integration
- Enhanced build system for 3-component architecture

### Versions:
- Service: 3.0.0
- Config GUI: 3.0.0
- Launcher: 3.0.0
- All components updated to .NET 8.0

### Build Stats:
- Service DLLs: 226
- Config DLLs: 248
- Launcher DLLs: 239
- Installer Size: 58.42 MB
- Build Time: ~4 minutes

## 🎯 Upgrade Path

### From v2.7 → v3.0
- ✅ Direct upgrade
- ✅ All settings preserved
- ✅ **New**: Smart launcher on desktop
- ✅ **New**: Direct MSI downloads
- ✅ Cleaner GUI without status bar

### From v2.6 or earlier → v3.0
- ✅ Major upgrade recommended
- ✅ All settings preserved
- ✅ Gain all v2.7 + v3.0 features

### From v2.0.1 → v3.0
- ✅ Massive upgrade!
- ✅ All settings preserved
- ✅ Auto-update system (v2.5)
- ✅ Enhanced UI with logo (v2.6)
- ✅ System tray + direct downloads (v2.7)
- ✅ Smart launcher + streamlined GUI (v3.0)

## 📋 System Requirements

- Windows 10/11 or Windows Server 2016+ (64-bit)
- Administrator rights for installation
- ~200 MB disk space

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

## 🚀 What's Next (v3.1+)

- **Minimize on Startup**: Option to start minimized to tray
- **Auto-Update Installation**: Automatic silent updates
- **Enhanced Launcher**: More startup options and diagnostics
- **RTSP Streaming**: Video stream of weight display
- **REST API**: HTTP endpoints for weight data
- **Multi-Scale Support**: Manage multiple scales
- **Cloud Sync**: Synchronize data to cloud
- **Mobile App**: iOS/Android companion app

---

**Full Changelog:** https://github.com/cloud-scale-us/Cloud-Scale/compare/v2.7.0...v3.0.0

**Tested On:** Windows 10, Windows 11
