# Scale Streamer v2.7.0 - Enhanced User Experience 🚀

**Major UI improvements: System tray icon, direct MSI downloads, and logo integration!**

## ✨ What's New

### 🖼️ Logo Integration
- **GUI Header Logo**: Beautiful 48x48 Cloud Scale logo displayed in application header
- **System Tray Icon**: Logo appears in Windows system tray
- **Window Icon**: Logo shown in titlebar and taskbar
- **Fully Deployed**: Logo files now properly included in installer

### 🔔 System Tray Functionality
- **Minimize to Tray**: Click X to minimize to system tray instead of closing
- **Rich Context Menu**: Right-click tray icon for quick actions:
  - Open Configuration
  - Check for Updates
  - Start/Stop/Restart Service
  - Exit Application
- **Double-Click to Restore**: Quick access to main window
- **Balloon Notifications**: Informative tooltips when minimizing

### 📥 Direct MSI Downloads
- **No Browser Required**: Updates download directly in the app
- **Progress Dialog**: Real-time download progress with percentage
- **Automatic Installation**: Launch installer immediately after download
- **Cancellable Downloads**: Stop download at any time
- **Error Handling**: Clear error messages if download fails

### 🛠️ Service Management
- **Tray Menu Controls**: Start, stop, and restart service from tray
- **Status Confirmations**: Success/error messages for all operations
- **Graceful Shutdown**: Service continues running when GUI closes
- **Quick Restart**: Restart service with automatic reconnection

### 🐛 Bug Fixes
- **Service Connection**: IPC server properly initialized (code was correct, just needed reinstall)
- **Logo Deployment**: Logo files now correctly included in MSI installer
- **Build Process**: Added logo copy step to build-self-contained.ps1

## 📥 Installation

1. Download `ScaleStreamer-v2.7.0-YYYYMMDD-HHMMSS.msi` below
2. Run installer (requires Administrator rights)
3. **Upgrading from v2.5.0 or v2.6.0?** Install directly - settings preserved!
4. Find desktop shortcut or Start Menu → "Scale Streamer"
5. **Look for system tray icon** - Scale Streamer is always accessible!

**File Size:** ~57 MB (self-contained with .NET 8.0)

## 🎯 Key Features

### System Tray Menu:
```
┌────────────────────────────┐
│ ● Open Configuration      │ (bold, default)
│   Check for Updates        │
│ ──────────────────────────│
│   Start Service            │
│   Stop Service             │
│   Restart Service          │
│ ──────────────────────────│
│   Exit                     │
└────────────────────────────┘
```

### Direct Download Flow:
1. Click "Download Update" → Progress dialog opens
2. Download MSI with real-time progress (0-100%)
3. Prompt: "Install now?" → Yes/No
4. If Yes: Launch installer, close configuration app
5. If No: MSI saved to temp folder for later

### Minimize Behavior:
- **Click X**: App minimizes to system tray (not closed!)
- **Balloon Tip**: "Application minimized to system tray"
- **To Truly Exit**: Right-click tray icon → Exit

## 📋 System Requirements

- Windows 10/11 or Windows Server 2016+ (64-bit)
- Administrator rights for installation
- ~200 MB disk space

## 🔧 Technical Changes

### New Features
- Added `NotifyIcon` with context menu to MainForm.cs
- Implemented `DownloadInstallerAsync` in UpdateChecker.cs
- Added direct download with `IProgress<DownloadProgressInfo>`
- Modified `OnFormClosing` to minimize to tray on user close

### Build Process
- Updated build-self-contained.ps1 to copy logo files
- Logo files now deployed via heat.exe (automatic inclusion)
- Removed manual logo components from WiX (prevented duplicates)

### Version Updates
- All .csproj files: 2.7.0
- WiX Package: Scale Streamer v2.7
- MainForm.cs APP_VERSION: "2.7.0"
- Installer MSI name: ScaleStreamer-v2.7.0

## 🎯 Upgrade Path

### From v2.6.0 → v2.7.0
- ✅ Direct upgrade (in-place)
- ✅ All settings preserved
- ✅ Service automatically restarted
- ✅ **New**: System tray icon appears
- ✅ **New**: Direct download capability

### From v2.5.0 → v2.7.0
- ✅ Major upgrade (skip v2.6)
- ✅ All settings preserved
- ✅ Service reconfigured
- ✅ Gain all v2.6 + v2.7 features

### From v2.0.1 → v2.7.0
- ✅ Massive upgrade (recommended!)
- ✅ All settings preserved
- ✅ Auto-update system added (v2.5)
- ✅ Enhanced UI with logo (v2.6)
- ✅ System tray + direct downloads (v2.7)

## 📖 Documentation

- [Auto-Update Architecture](AUTO-UPDATE-ARCHITECTURE.md) - How updates work
- [Quick Start Guide](QUICK-START-V2.md) - Get started in 5 minutes
- [Build Instructions](BUILD-AND-TEST-V2.md) - Build from source
- [Complete Documentation](CLAUDE.md) - Master guide

## 🐛 Known Issues

**Windows SmartScreen Warning:**
- Installer shows "Windows protected your PC" warning
- This is normal for unsigned installers
- Click "More info" → "Run anyway"
- To remove: Purchase code signing certificate ($200-500/year)

**Service Connection on First Launch:**
- If upgrading from v2.0.1 or earlier, service may not connect
- **Solution**: Restart service from system tray menu
- This is because old service version doesn't have IPC server
- After restart, v2.7 service will be running and connect properly

## 💬 Support

- **Issues**: [Report bugs](../../issues)
- **Email**: support@cloud-scale.us

## 🚀 What's Next (v2.8+)

- **Minimize on Startup**: Option to start minimized to tray
- **Auto-Update Installation**: Automatic silent updates
- **Notification Settings**: Configure balloon tip behavior
- **RTSP Streaming**: Video stream of weight display
- **REST API**: HTTP endpoints for weight data
- **Multi-Scale Support**: Manage multiple scales
- **Cloud Sync**: Synchronize data to cloud
- **Mobile App**: iOS/Android companion app

---

**Full Changelog:** https://github.com/cloud-scale-us/Cloud-Scale/compare/v2.6.0...v2.7.0

**Build Stats:**
- Service DLLs: 226
- Config DLLs: 248 (+ logo.png, logo.ico)
- Installer Size: 57.1 MB
- Build Time: ~3 minutes

**New in v2.7:**
- System tray icon with context menu
- Direct MSI download with progress
- Minimize to tray behavior
- Logo files properly deployed
- Enhanced service management

**Tested On:** Windows 10, Windows 11
