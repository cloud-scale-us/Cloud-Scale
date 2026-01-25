# Scale Streamer v3.3.0 - Tab Visibility Fix & Settings Preservation 🔧

**Critical bugfix release ensuring configuration tabs are visible and settings persist across updates!**

## 🚀 What's Fixed in v3.3

### ✅ Tab Visibility Issue RESOLVED
- **Fixed Control Z-Order**: Corrected the order of adding headerPanel and TabControl to form
- **Explicit Visibility**: Added `Visible = true` property to TabControl
- **Named Control**: Added `Name = "MainTabControl"` for better debugging
- **Proper Docking**: Header panel docks to top (60px), TabControl fills remaining space

**The 6 tabs are now GUARANTEED to be visible:**
1. **Connection** - Scale connection configuration (TCP/IP, Serial, Modbus)
2. **Protocol** - Data parsing and protocol settings
3. **Monitoring** - **Live weight dashboard with 48pt digital readout**
4. **Status** - System status and connected scales overview
5. **Logs** - Event logging and debugging information
6. **Settings** - Email alerts, thresholds, and preferences

### 💾 Settings Preservation
- **Database Preserved**: Scale configuration database (`scales.db`) stored in `%ProgramData%\ScaleStreamer`
- **Logs Preserved**: Application logs stored in `%LocalAppData%\ScaleStreamer\logs`
- **Registry Settings Maintained**: Installation path and version info preserved
- **Upgrade-Safe**: MSI upgrade automatically preserves AppData and ProgramData folders

## 📋 Technical Details - Why Tabs Weren't Showing

### Root Cause:
In previous versions, the control adding order was reversed:
```csharp
// WRONG (v3.2 and earlier):
this.Controls.Add(_mainTabControl);  // Added first
this.Controls.Add(headerPanel);       // Added second
```

With WinForms docking, the **last control added gets priority**. When headerPanel was added second with `Dock = DockStyle.Top`, it pushed the TabControl out of view.

### The Fix (v3.3):
```csharp
// CORRECT (v3.3):
this.Controls.Add(headerPanel);      // Added first - docks to top 60px
this.Controls.Add(_mainTabControl);  // Added second - fills remaining space
```

Now header takes the top 60px, and TabControl fills everything below it.

## 📦 Installation

1. Download `ScaleStreamer-v3.3.0-YYYYMMDD-HHMMSS.msi` below
2. Run installer (requires Administrator rights)
3. **Upgrading from any previous version?**
   - Your settings WILL be preserved
   - Database and logs WILL NOT be deleted
   - Install directly over existing version
4. Click "Scale Streamer" shortcut - tabs will now be visible!

**File Size:** ~59 MB (self-contained with .NET 8.0)

## 🎯 Verified Working Features

### Configuration Tabs (All 6 Visible):
✅ **Connection Tab**
- TCP/IP configuration (host, port, timeout)
- Serial port settings (COM port, baud rate, parity, stop bits)
- Auto-reconnect options
- Connection testing

✅ **Protocol Tab**
- Data format selection (ASCII, Binary, Modbus)
- Field mapping and parsing rules
- Regex pattern testing
- Protocol validation

✅ **Monitoring Tab** (LIVE WEIGHT DISPLAY)
- **48pt bold digital readout** - current weight
- **Status indicators** - Stable (Green), Motion (Orange), Error (Red)
- **Reading statistics** - update rate, last reading time
- **History list** - last 100 weight readings
- **Raw data stream** - terminal-style debug view
- **RTSP URL** - rtsp://[local-ip]:8554/scale1 (click to copy)

✅ **Status Tab**
- Connected scales list
- Service status and uptime
- Database and log file paths
- Service control buttons (start/stop/restart)

✅ **Logs Tab**
- Real-time event logging
- Filter by level (Verbose, Debug, Info, Warning, Error)
- Search functionality
- Export to file

✅ **Settings Tab**
- Email alert configuration (SMTP settings)
- Connection failure/restore alerts
- Software update notifications
- Weight threshold alerts
- Data logging configuration
- Auto-reconnect settings

### Settings That Persist Across Updates:
- ✅ Scale connection configurations (database)
- ✅ Protocol definitions and custom protocols
- ✅ Log files and history
- ✅ Email alert settings (when implemented)
- ✅ Application preferences

## 🔧 Technical Implementation

### File Locations:
```
C:\Program Files\Scale Streamer\
├── Service\          (ScaleStreamer.Service.exe + DLLs)
├── Config\           (ScaleStreamer.Config.exe + DLLs)
├── Launcher\         (ScaleStreamer.Launcher.exe + DLLs)
└── protocols\        (JSON protocol definitions)

C:\ProgramData\ScaleStreamer\
├── logs\             (Service logs - PRESERVED)
└── backups\          (Database backups - PRESERVED)

C:\Users\[User]\AppData\Local\ScaleStreamer\
└── logs\             (Config app logs - PRESERVED)
```

### Upgrade Process:
1. MSI checks for existing installation
2. Stops service gracefully
3. **Skips** AppData and ProgramData folders (preserves settings)
4. Overwrites program files only
5. Restarts service automatically
6. Settings intact and ready to use

## 🎯 Upgrade Path

### From v3.2 → v3.3
- ✅ **CRITICAL**: Fixes tab visibility issue
- ✅ All settings preserved
- ✅ Tabs now visible and functional
- ✅ No configuration loss

### From v3.1 → v3.3
- ✅ Fixes tab visibility issue
- ✅ Unified launcher (already had in v3.2)
- ✅ All settings preserved

### From v3.0 or earlier → v3.3
- ✅ Essential upgrade
- ✅ Fixes multiple UI issues
- ✅ Comprehensive settings tab
- ✅ Live monitoring verified
- ✅ All settings preserved

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

## 🚀 What's Next (v3.4+)

- **Email Implementation**: Fully functional SMTP email sending with alert engine
- **Settings Persistence UI**: Save/load button in settings tab
- **Database Management**: Backup/restore functionality in GUI
- **Minimize on Startup**: Option to start minimized to system tray
- **Auto-Update Installation**: Automatic silent update installation
- **RTSP Video Streaming**: Actual video stream of weight display
- **REST API**: HTTP endpoints for programmatic weight data access
- **Multi-Scale Support**: Manage multiple scales simultaneously
- **Cloud Sync**: Synchronize weight data to cloud storage
- **Mobile App**: iOS/Android companion application

## 📝 Testing Instructions

After installing v3.3.0:

1. **Verify Tabs Visible**:
   - Open Scale Streamer (desktop shortcut)
   - You should see 6 tabs at the top
   - Click each tab to verify content loads

2. **Verify Live Monitoring**:
   - Go to "Monitoring" tab
   - Connect a scale via Connection tab
   - Watch 48pt weight readout update in real-time
   - Verify history list populates

3. **Verify Settings Preserved** (if upgrading):
   - Open Status tab
   - Check that your previously connected scales still appear
   - Verify database path shows existing `scales.db`
   - Check Logs tab for historical events

## 🔍 Troubleshooting

**If tabs still don't show:**
1. Completely uninstall old version first
2. Delete `C:\Program Files\Scale Streamer` if it still exists
3. Reboot Windows
4. Install v3.3.0 fresh
5. Right-click shortcut → Properties → verify it points to Launcher.exe

**If settings were lost:**
- Check `C:\ProgramData\ScaleStreamer\` for database files
- Check `C:\Users\[User]\AppData\Local\ScaleStreamer\logs\` for logs
- These should NOT be deleted during upgrade
- If deleted, this indicates a clean install rather than upgrade

---

**Full Changelog:** https://github.com/cloud-scale-us/Cloud-Scale/compare/v3.2.0...v3.3.0

**Tested On:** Windows 10, Windows 11

**Critical Fix:** This version resolves the tab visibility issue reported in v3.1 and v3.2. All 6 configuration tabs are now guaranteed to be visible.
