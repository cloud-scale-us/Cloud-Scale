# Scale Streamer v2.6.0 - Enhanced UI & Installation 🎨

**Major UI improvements, desktop shortcut, and smooth installation experience!**

## ✨ What's New

### 🎨 Professional User Interface
- **Version in Title Bar**: Window title now shows "Scale Streamer Configuration - v2.6.0"
- **Header Panel with Logo**: Beautiful header with 48x48 Cloud Scale logo and version display
- **Window Icon**: Application icon in titlebar and taskbar
- **Modern Design**: Clean, professional look with proper branding

**New GUI Header:**
```
┌────────────────────────────────────────┐
│ [Logo]  Scale Streamer                 │
│         Version 2.6.0                   │
├────────────────────────────────────────┤
│  Connection │ Protocol │ Monitoring... │
```

### 🖥️ Desktop Shortcut
- **Automatic Creation**: Desktop shortcut created during installation
- **Custom Icon**: Uses Cloud Scale logo for easy identification
- **Name**: "Scale Streamer" (clean and simple)
- **Quick Access**: Double-click from desktop to launch configuration

### 🔧 Installation Improvements

**Smooth Installation - No More Hangs!**
- ✅ Removed database initialization that caused cmd window
- ✅ Service starts silently in background
- ✅ No manual window closes required
- ✅ Installation completes automatically

**Better Version Display:**
- Installer screens show "Scale Streamer v2.6"
- Start Menu shortcuts have Cloud Scale icon
- Add/Remove Programs shows correct version

### 🐛 Critical Bug Fixes

**Service Connection - FINALLY FIXED!**
- Increased IPC timeout to 3000ms (was 1000ms)
- Service connection now reliable on first launch
- GUI correctly shows "Service: Connected" status
- No more "Service: Not Running" false alarms

**Database Auto-Creation:**
- Service automatically creates database on first run
- No manual initialization needed
- Smoother startup experience

## 📥 Installation

1. Download `ScaleStreamer-v2.6.0-YYYYMMDD-HHMMSS.msi` below
2. Run installer (requires Administrator rights)
3. **Upgrading from v2.5.0?** Install directly - settings preserved!
4. Find desktop shortcut or Start Menu → "Scale Streamer"

**File Size:** ~55 MB (self-contained with .NET 8.0)

## 🎯 Testing Auto-Update Notifications

**This release tests the update notification system!**

If you have v2.5.0 installed:
1. Install v2.6.0 (upgrade)
2. Launch Configuration GUI
3. You should see update notification: "⚠ New version available: v2.6.0"

This confirms the auto-update detection is working correctly!

## 📋 System Requirements

- Windows 10/11 or Windows Server 2016+ (64-bit)
- Administrator rights for installation
- ~200 MB disk space

## 🔧 Technical Changes

### UI Components
- Added header panel (60px height, gray background)
- Logo PictureBox (48x48, bordered)
- Title label (Segoe UI, 16pt, bold, blue)
- Version label (Segoe UI, 9pt, gray)

### Installer Changes
- Added DesktopShortcutComponents group
- Removed InitializeDatabase custom action
- Package name: "Scale Streamer v2.6 (Self-Contained)"
- All shortcuts reference ScaleStreamerIcon

### Service Improvements
- Auto-creates SQLite database on first run
- IPC timeout: 3000ms (reliable connection)
- Starts silently without console window

## 🎯 Upgrade Path

### From v2.5.0 → v2.6.0
- ✅ Direct upgrade (in-place)
- ✅ All settings preserved
- ✅ Service automatically restarted
- ✅ Desktop shortcut added
- ✅ Update notification tested

### From v2.0.1 → v2.6.0
- ✅ Major upgrade (skip v2.5)
- ✅ All settings preserved
- ✅ Service reconfigured
- ✅ Auto-update system added
- ✅ New UI features

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

## 💬 Support

- **Issues**: [Report bugs](../../issues)
- **Email**: support@cloud-scale.us

## 🚀 What's Next (v2.7+)

- RTSP video streaming (weight display as video)
- REST API for weight data
- Multi-scale support
- Cloud synchronization
- Mobile app

---

**Full Changelog:** https://github.com/cloud-scale-us/Cloud-Scale/compare/v2.5.0...v2.6.0

**Build Stats:**
- Service DLLs: 226
- Config DLLs: 248 (+ logo.png, logo.ico)
- Installer Size: 54.95 MB
- Build Time: ~2 minutes

**Tested On:** Windows 10, Windows 11
