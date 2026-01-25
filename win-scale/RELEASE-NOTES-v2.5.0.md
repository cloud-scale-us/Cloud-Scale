# Scale Streamer v2.5.0 - Auto-Update System 🎉

**Major update with automatic update notifications and critical bug fixes!**

## ✨ What's New

### 🔄 Automatic Update Notifications
- **Smart Update Checker**: GUI automatically checks GitHub for new versions on startup
- **Non-Intrusive Notifications**: Update banner appears only when new version is available
- **Release Notes Viewer**: View what's new before downloading
- **One-Click Download**: Opens release page in browser with single click
- **No Telemetry**: Zero data collection, tracking, or forced updates
- **Smart Caching**: Respects GitHub API limits with 24-hour check interval

**How it works:**
1. Configuration GUI checks GitHub Releases API on startup (non-blocking)
2. If newer version available, shows notification banner at top of window
3. Click "Download Update" to open release page in browser
4. Download and run MSI installer (upgrades in-place, keeps your settings)

### 🐛 Critical Bug Fixes

**Service Connection Issue - FIXED**
- **Problem**: GUI incorrectly showed "Service: Not Running" even when service was running
- **Root Cause**: Named pipe connection timeout was too short (1 second)
- **Solution**: Increased timeout to 3 seconds + improved error handling
- **Result**: GUI now correctly shows connection status

## 📥 Installation

1. Download `ScaleStreamer-v2.5.0-YYYYMMDD-HHMMSS.msi` below
2. Run the installer (requires Administrator rights)
3. **Upgrading from v2.0.1?** Your configuration will be preserved automatically!
4. Launch "Scale Streamer Configuration" from Start Menu

**No .NET Runtime required** - Installer includes all dependencies (~55 MB)

## 📋 System Requirements

- Windows 10/11 or Windows Server 2016+ (64-bit)
- Administrator rights for installation
- ~200 MB disk space

## 🔧 Technical Changes

### New Files
- `UpdateChecker.cs`: GitHub API integration for version checking
- `AUTO-UPDATE-ARCHITECTURE.md`: Complete documentation of update system

### Updated Components
- **MainForm.cs**: Added update notification UI and handlers
- **IPC Connection**: Increased timeout from 1000ms to 3000ms
- **Version Numbers**: Updated all assemblies to 2.5.0
- **Installer**: Now generates timestamped MSI filenames

### Build Stats
- Service: 226 DLLs (self-contained)
- Config GUI: 248 DLLs (self-contained)
- Installer Size: ~55 MB

## 🎯 Upgrade Notes

### From v2.0.1 to v2.5.0

**What's Preserved:**
- ✅ All scale configurations
- ✅ Connection settings (TCP/IP, Serial)
- ✅ Protocol templates
- ✅ Custom protocols
- ✅ Database (`C:\ProgramData\ScaleStreamer\`)

**What Happens:**
1. Installer detects existing v2.0.1 installation
2. Stops service gracefully
3. Replaces binaries with v2.5.0 versions
4. Preserves all configuration files
5. Restarts service automatically
6. First launch shows new update notification feature

**No manual steps required!**

## 📖 Documentation

- [Auto-Update Architecture](AUTO-UPDATE-ARCHITECTURE.md) - How the update system works
- [Quick Start Guide](QUICK-START-V2.md) - Get started in 5 minutes
- [Build Instructions](BUILD-AND-TEST-V2.md) - Build from source
- [Architecture Overview](V2-UNIVERSAL-ARCHITECTURE.md) - Technical architecture
- [Complete Documentation](CLAUDE.md) - Master restoration guide

## 🐛 Known Issues

None reported yet! This is a stable release.

If you encounter issues:
1. Check service logs: `C:\ProgramData\ScaleStreamer\logs\`
2. Verify service is running: `services.msc` → "Scale Streamer Service"
3. [Report bug on GitHub](../../issues)

## 💬 Support

- **Issues**: [Report bugs or request features](../../issues)
- **Email**: support@cloud-scale.us
- **Documentation**: See repository docs/

## 🚀 What's Next?

Future releases will include:
- RTSP video streaming (weight display as video feed)
- REST API for weight data access
- Cloud synchronization
- Data analytics and reporting
- Multi-scale support

---

**Full Changelog:** https://github.com/cloud-scale-us/Cloud-Scale/compare/v2.0.1...v2.5.0

**Tested On:** Windows 10, Windows 11, Windows Server 2019

**Self-Contained:** Yes - includes .NET 8.0 runtime
