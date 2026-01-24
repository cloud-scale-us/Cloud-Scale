# Cloud-Scale Setup Summary

## ✅ Completed Tasks

### 1. Git Repository Setup
- **Repository:** https://github.com/CNesbitt2025/Cloud-Scale.git
- **Branch:** main
- **Structure:** Cloud-Scale parent repo with win-scale product subdirectory
- **Logo:** Added to README with transparent background (requires manual removal at remove.bg)

### 2. Application Build
- **.NET 8 SDK:** Installed (v8.0.417)
- **Build Output:** `/mnt/d/win-scale/win-scale/publish/Release/ScaleStreamer.exe`
- **Dependencies:** FFmpeg and MediaMTX copied to publish folder

### 3. Logging & Debugging

#### Runtime Application Log
- **Location:** `%LOCALAPPDATA%\ScaleStreamer\app.log`
- **Contents:** Startup, errors, tray icon loading, stream status
- **View:** `.\scripts\view-logs.ps1` (Option 1)

#### Installation Log
- **Location:** `%TEMP%\ScaleStreamer-Install\install-*.log`
- **Install with logging:** `.\scripts\install-with-logging.ps1`
- **View:** `.\scripts\view-logs.ps1` (Option 2)

#### MSI Logging
- **Enabled:** Automatic verbose logging (`MsiLogging=voicewarmup`)
- **Format:** Full installation trace with all actions

### 4. System Integration

#### Registry Entries
```
HKCU\Software\Microsoft\Windows\CurrentVersion\Run
  ScaleStreamer = "[INSTALLFOLDER]ScaleStreamer.exe"  (Auto-start)

HKLM\Software\Cloud-Scale\ScaleStreamer
  InstallPath = "C:\Program Files\Scale RTSP Streamer"
  Version = "1.0.0"
  Vendor = "Cloud-Scale"
  SupportURL = "https://cloud-scale.us/support"

HKLM\Software\Cloud-Scale\ScaleStreamer\Firewall
  RTSP = 8554
  HLS = 8888
```

#### Firewall Rules
Automatically added during installation:
- **Scale Streamer - RTSP:** TCP port 8554 (inbound)
- **Scale Streamer - HLS:** TCP port 8888 (inbound)

Automatically removed during uninstallation.

### 5. Fixed Issues

#### System Tray Icon
- ✅ Fixed: Now loads from `Resources\icon.ico`
- ✅ Fallback: Uses default Windows icon if not found
- ✅ Logging: Reports icon loading status

#### Application Launch
- ✅ Enhanced error handling with user-friendly messages
- ✅ Logs all startup steps for debugging
- ✅ Shows error dialog with log file location on failure

---

## 🚀 Next Steps

### 1. Remove Logo Background
Visit https://www.remove.bg/ and upload:
```
/mnt/d/win-scale/win-scale/installer/logo.png
```
Download the transparent version and replace the original file.

### 2. Rebuild Application
From WSL (Linux):
```bash
cd /mnt/d/win-scale/win-scale
export PATH="$HOME/.dotnet:$PATH"
dotnet publish -c Release -r win-x64 --self-contained true -o publish/Release
cp -r deps publish/Release/
cp appsettings.json publish/Release/
```

### 3. Build MSI Installer
From Windows PowerShell:
```powershell
cd D:\win-scale\win-scale
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
.\build-installer.ps1
```

The MSI will be created at:
```
D:\win-scale\win-scale\installer\ScaleStreamerSetup.msi
```

### 4. Test Installation with Logging
```powershell
.\scripts\install-with-logging.ps1
```

This will:
- Install the application
- Create verbose installation log
- Show log location
- Offer to open the log

### 5. Verify Application Launch
After installation:
1. **Check system tray** for Scale Streamer icon (bottom-right)
2. **View runtime log:**
   ```powershell
   notepad $env:LOCALAPPDATA\ScaleStreamer\app.log
   ```
3. **Test features:**
   - Right-click tray icon → Configure
   - Test scale connection
   - Start streaming

### 6. Troubleshooting
If issues occur, use the log viewer:
```powershell
.\scripts\view-logs.ps1
```

Options:
- [1] View application runtime log
- [2] View installation log
- [3] Open Windows Event Viewer
- [4] Tail application log (live monitoring)

See `TROUBLESHOOTING.md` for detailed help.

### 7. Push to GitHub
```bash
cd /mnt/d/win-scale
git push -u origin main
```

---

## 📦 Ports & Network Requirements

### Required Ports (Automatically Configured)
- **8554/TCP** - RTSP streaming (MediaMTX)
- **8888/TCP** - HLS web streaming (MediaMTX)

### Firewall Configuration
Firewall rules are automatically:
- ✅ **Added** during installation (requires admin)
- ✅ **Removed** during uninstallation

### Manual Firewall Commands (if needed)
```powershell
# Add rules
netsh advfirewall firewall add rule name="Scale Streamer - RTSP" dir=in action=allow protocol=TCP localport=8554
netsh advfirewall firewall add rule name="Scale Streamer - HLS" dir=in action=allow protocol=TCP localport=8888

# Remove rules
netsh advfirewall firewall delete rule name="Scale Streamer - RTSP"
netsh advfirewall firewall delete rule name="Scale Streamer - HLS"

# List rules
netsh advfirewall firewall show rule name="Scale Streamer - RTSP"
netsh advfirewall firewall show rule name="Scale Streamer - HLS"
```

---

## 🔧 Helper Scripts

### `scripts/install-with-logging.ps1`
Install MSI with full logging enabled. Creates timestamped log in `%TEMP%\ScaleStreamer-Install\`.

### `scripts/view-logs.ps1`
Interactive log viewer:
- Shows application runtime log status
- Lists all installation logs
- Shows recent Windows Installer events
- Open logs in Notepad or tail live

### `scripts/build.ps1`
Build application from source (called by `build-installer.ps1`).

### `scripts/download-deps.ps1`
Download FFmpeg and MediaMTX dependencies.

---

## 📊 Project Status

| Component | Status | Notes |
|-----------|--------|-------|
| Git Repository | ✅ Complete | Ready to push |
| .NET Build | ✅ Complete | v1.0.0 built |
| Runtime Logging | ✅ Complete | Detailed startup/error logs |
| Installer Logging | ✅ Complete | MSI verbose logging enabled |
| System Tray Icon | ✅ Fixed | Loads from Resources folder |
| Firewall Rules | ✅ Complete | Auto-add/remove on install |
| Registry Keys | ✅ Complete | Cloud-Scale tracking entries |
| Logo Background | ⚠️ Needs Action | Use remove.bg to fix |
| MSI Installer | 🔨 Ready to Build | Run build-installer.ps1 |
| GitHub Push | ⏳ Pending | Ready when you are |

---

## 📝 Files Modified/Created

### New Files
- `TROUBLESHOOTING.md` - Comprehensive troubleshooting guide
- `SETUP-SUMMARY.md` - This file
- `scripts/install-with-logging.ps1` - Installation with logging
- `scripts/view-logs.ps1` - Interactive log viewer
- `.github-logo.png` - Cloud-Scale logo for GitHub
- `README.md` - Cloud-Scale main README

### Modified Files
- `src/ScaleStreamer/Program.cs` - Added runtime logging
- `src/ScaleStreamer/App/TrayApplication.cs` - Fixed icon loading
- `installer/ScaleStreamer.wxs` - Added firewall, registry, logging
- `win-scale/.gitignore` - Exclude build artifacts

---

## 🌐 Cloud-Scale URLs

- **Website:** https://cloud-scale.us
- **Support:** https://cloud-scale.us/support
- **GitHub:** https://github.com/CNesbitt2025/Cloud-Scale
- **Issues:** https://github.com/CNesbitt2025/Cloud-Scale/issues

---

## 📞 Support Information

### Runtime Log Location
```
%LOCALAPPDATA%\ScaleStreamer\app.log
```
Equivalent to:
```
C:\Users\<Username>\AppData\Local\ScaleStreamer\app.log
```

### Installation Log Location
```
%TEMP%\ScaleStreamer-Install\install-YYYYMMDD-HHMMSS.log
```
Equivalent to:
```
C:\Users\<Username>\AppData\Local\Temp\ScaleStreamer-Install\
```

### When Reporting Issues
Include:
1. Runtime log contents
2. Installation log (if install issue)
3. Windows version
4. Scale model and connection type
5. Steps to reproduce

---

**Generated:** 2026-01-24
**Version:** 1.0.0
**Repository:** Cloud-Scale
**Product:** win-scale
