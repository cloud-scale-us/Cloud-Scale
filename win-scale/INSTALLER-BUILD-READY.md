# Scale Streamer v2.0 - Installer Build Ready ✅

**Date**: 2026-01-24
**Status**: Ready for Windows build
**Verification**: Complete

---

## Pre-Build Verification Summary

All installer prerequisites have been verified and are in place. The installer is ready to build on a Windows system with .NET 8.0 SDK and WiX Toolset v4.

### ✅ Installer Assets Verified

| Asset | Status | Location | Size |
|-------|--------|----------|------|
| **banner.bmp** | ✅ Present | `/installer/banner.bmp` | 84 KB |
| **dialog.bmp** | ✅ Present | `/installer/dialog.bmp` | 451 KB |
| **icon.ico** | ✅ Present | `/installer/icon.ico` | 91 KB |
| **license.rtf** | ✅ Present | `/installer/license.rtf` | 3.6 KB |
| **ScaleStreamerV2.wxs** | ✅ Present | `/installer/ScaleStreamerV2.wxs` | 19 KB |
| **build-installer-v2.ps1** | ✅ Present | `/installer/build-installer-v2.ps1` | 5.0 KB |

### ✅ Documentation Files Verified

| Document | Status | Location | Size |
|----------|--------|----------|------|
| **QUICK-START-V2.md** | ✅ Present | `/QUICK-START-V2.md` | 12 KB |
| **BUILD-AND-TEST-V2.md** | ✅ Present | `/BUILD-AND-TEST-V2.md` | 16 KB |
| **V2-UNIVERSAL-ARCHITECTURE.md** | ✅ Present | `/V2-UNIVERSAL-ARCHITECTURE.md` | 21 KB |

### ✅ Protocol Template Files Verified

| Protocol | Status | Location |
|----------|--------|----------|
| **fairbanks-6011.json** | ✅ Present | `/protocols/manufacturers/fairbanks-6011.json` |
| **generic-ascii.json** | ✅ Present | `/protocols/generic/generic-ascii.json` |
| **modbus-tcp.json** | ✅ Present | `/protocols/generic/modbus-tcp.json` |

### ✅ Database Schema Verified

| File | Status | Location |
|------|--------|----------|
| **schema.sql** | ✅ Present | `/src-v2/ScaleStreamer.Common/Database/schema.sql` |

### ✅ Solution and Projects Verified

| Component | Status | Location |
|-----------|--------|----------|
| **Solution File** | ✅ Present | `/ScaleStreamer.sln` |
| **ScaleStreamer.Common** | ✅ Present | `/src-v2/ScaleStreamer.Common/ScaleStreamer.Common.csproj` |
| **ScaleStreamer.Service** | ✅ Present | `/src-v2/ScaleStreamer.Service/ScaleStreamer.Service.csproj` |
| **ScaleStreamer.Config** | ✅ Present | `/src-v2/ScaleStreamer.Config/ScaleStreamer.Config.csproj` |

---

## Build Instructions

### Prerequisites

The following must be installed on Windows:

1. **.NET 8.0 SDK**
   ```powershell
   # Verify installation
   dotnet --version
   # Should show 8.0.x
   ```

2. **WiX Toolset v4**
   ```powershell
   # Install globally
   dotnet tool install --global wix

   # Verify installation
   wix --version
   ```

3. **PowerShell 5.1 or later**
   ```powershell
   # Check version
   $PSVersionTable.PSVersion
   ```

### Build Process

```powershell
# 1. Open PowerShell as Administrator (recommended)
# 2. Navigate to installer directory
cd D:\win-scale\win-scale\installer

# 3. Run build script
.\build-installer-v2.ps1

# Optional: Build with specific configuration
.\build-installer-v2.ps1 -Configuration Release

# Optional: Skip project rebuild (use existing binaries)
.\build-installer-v2.ps1 -SkipBuild
```

### Expected Output

```
========================================
Scale Streamer v2.0 Installer Build
========================================

[1/5] Building Service project...
[2/5] Building Configuration GUI project...
[3/5] Verifying WiX Toolset...
[4/5] License file exists
[5/5] Building MSI installer...

========================================
BUILD SUCCESSFUL!
========================================

Installer created at:
  D:\win-scale\win-scale\installer\bin\ScaleStreamer-v2.0.0.msi

File size: ~70-80 MB

Next steps:
  1. Test installation: msiexec /i "..." /l*v install.log
  2. Silent install: msiexec /i "..." /quiet
  3. Uninstall: msiexec /x "..." /quiet
```

---

## What the Installer Includes

### Components Installed

#### 1. Windows Service
- **Path**: `C:\Program Files\Scale Streamer\Service\`
- **Executable**: `ScaleStreamer.Service.exe`
- **Service Name**: `ScaleStreamerService`
- **Display Name**: `Scale Streamer Service`
- **Start Type**: Automatic
- **Account**: LocalSystem
- **Recovery**: Restart on failure (3 attempts)

#### 2. Configuration GUI
- **Path**: `C:\Program Files\Scale Streamer\Config\`
- **Executable**: `ScaleStreamer.Config.exe`
- **Shortcuts**: Desktop + Start Menu

#### 3. Protocol Templates
- **Path**: `C:\Program Files\Scale Streamer\protocols\`
- **Manufacturers**: Fairbanks 6011
- **Generic**: ASCII, Modbus TCP

#### 4. Documentation
- **Path**: `C:\Program Files\Scale Streamer\docs\`
- **Files**: Quick Start, Build & Test, Architecture

#### 5. Application Data
- **Database**: `C:\ProgramData\ScaleStreamer\scalestreamer.db`
- **Logs**: `C:\ProgramData\ScaleStreamer\logs\`
- **Backups**: `C:\ProgramData\ScaleStreamer\backups\`

### Automatic Configuration

The installer automatically:

✅ **Installs Windows Service** with auto-start and recovery
✅ **Creates Firewall Rules** for ports 8554 (RTSP) and 8888 (HLS)
✅ **Sets Registry Keys** for all paths and configuration
✅ **Initializes Database** with schema on first run
✅ **Creates Shortcuts** in Start Menu and Desktop
✅ **Launches Config GUI** after installation (optional)

### Registry Keys Created

**Location**: `HKLM\Software\Cloud-Scale\ScaleStreamer`

| Key | Value |
|-----|-------|
| `InstallPath` | `C:\Program Files\Scale Streamer\` |
| `Version` | `2.0.0` |
| `Vendor` | `Cloud-Scale` |
| `SupportURL` | `https://cloud-scale.us/support` |
| `ServicePath` | `C:\Program Files\Scale Streamer\Service\` |
| `ConfigPath` | `C:\Program Files\Scale Streamer\Config\` |
| `ProtocolsPath` | `C:\Program Files\Scale Streamer\protocols\` |
| `LogsPath` | `C:\ProgramData\ScaleStreamer\logs\` |
| `BackupsPath` | `C:\ProgramData\ScaleStreamer\backups\` |

### Firewall Rules Created

| Rule Name | Direction | Protocol | Port | Action |
|-----------|-----------|----------|------|--------|
| `Scale Streamer - RTSP` | Inbound | TCP | 8554 | Allow |
| `Scale Streamer - HLS` | Inbound | TCP | 8888 | Allow |

---

## Testing the Installer

### 1. Build Verification

After running `build-installer-v2.ps1`, verify:

```powershell
# Check MSI file exists
Test-Path "D:\win-scale\win-scale\installer\bin\ScaleStreamer-v2.0.0.msi"

# Check file size (should be ~70-80 MB with .NET runtime)
Get-Item "D:\win-scale\win-scale\installer\bin\ScaleStreamer-v2.0.0.msi" | Select-Object Length

# Generate checksum
Get-FileHash "D:\win-scale\win-scale\installer\bin\ScaleStreamer-v2.0.0.msi" -Algorithm SHA256
```

### 2. Installation Test

```powershell
# Interactive installation with logging
msiexec /i "D:\win-scale\win-scale\installer\bin\ScaleStreamer-v2.0.0.msi" /l*v install.log

# Verify service installed
sc.exe query ScaleStreamerService

# Expected output:
#   STATE: 4 RUNNING

# Check files
Get-ChildItem "C:\Program Files\Scale Streamer" -Recurse

# Verify firewall rules
netsh advfirewall firewall show rule name="Scale Streamer - RTSP"
netsh advfirewall firewall show rule name="Scale Streamer - HLS"

# Check registry keys
Get-ItemProperty "HKLM:\Software\Cloud-Scale\ScaleStreamer"

# View service log
Get-Content "C:\ProgramData\ScaleStreamer\logs\service-*.log" -Tail 50
```

### 3. Configuration GUI Test

```powershell
# Launch from Start Menu or Desktop
# Or directly:
& "C:\Program Files\Scale Streamer\Config\ScaleStreamer.Config.exe"
```

Expected behavior:
- GUI launches successfully
- Connects to Windows Service via Named Pipe
- Status tab shows "Service Running"
- All tabs are accessible

### 4. Uninstallation Test

```powershell
# Stop service first (optional)
sc.exe stop ScaleStreamerService

# Uninstall with logging
msiexec /x "D:\win-scale\win-scale\installer\bin\ScaleStreamer-v2.0.0.msi" /l*v uninstall.log

# Verify service removed
sc.exe query ScaleStreamerService
# Expected: "The specified service does not exist as an installed service."

# Verify firewall rules removed
netsh advfirewall firewall show rule name="Scale Streamer - RTSP"
# Expected: "No rules match the specified criteria."

# Check registry keys removed
Test-Path "HKLM:\Software\Cloud-Scale\ScaleStreamer"
# Expected: False

# Note: AppData (database, logs) may remain for user data preservation
```

---

## Troubleshooting

### Build Errors

**Error: "WiX Toolset not found"**
```powershell
# Install WiX globally
dotnet tool install --global wix

# Add to PATH if needed (restart PowerShell after)
$env:PATH += ";$env:USERPROFILE\.dotnet\tools"
```

**Error: "Project build failed"**
```powershell
# Clean and restore
dotnet clean D:\win-scale\win-scale\ScaleStreamer.sln
dotnet restore D:\win-scale\win-scale\ScaleStreamer.sln

# Rebuild
.\build-installer-v2.ps1
```

**Error: "Cannot find source file"**
- Verify all protocol JSON files exist in `/protocols/`
- Verify documentation files exist in root directory
- Verify schema.sql exists in `src-v2/ScaleStreamer.Common/Database/`

### Installation Errors

**Error: "Service installation failed"**
```powershell
# Check if service already exists
sc.exe query ScaleStreamerService

# If exists, delete it first
sc.exe delete ScaleStreamerService

# Retry installation
```

**Error: "Access denied"**
```powershell
# Run installer as Administrator
# Right-click MSI → Run as administrator
```

**Error: "Firewall rules not added"**
```powershell
# Manually add rules
netsh advfirewall firewall add rule name="Scale Streamer - RTSP" dir=in action=allow protocol=TCP localport=8554
netsh advfirewall firewall add rule name="Scale Streamer - HLS" dir=in action=allow protocol=TCP localport=8888
```

---

## Next Steps

### After Successful Build

1. **Test Installation**
   - Install on clean Windows VM or test system
   - Verify all components install correctly
   - Test service starts automatically

2. **Test Configuration GUI**
   - Launch Config application
   - Test adding a scale connection
   - Verify IPC communication works

3. **Test with Hardware**
   - Connect to real scale hardware
   - Configure protocol settings
   - Verify weight readings

4. **Stability Testing**
   - Run service for 24+ hours
   - Monitor for memory leaks
   - Check log files for errors

5. **Documentation Review**
   - Update screenshots
   - Add troubleshooting examples
   - Create video tutorials

### Production Checklist

Before production distribution:

- [ ] Code signing certificate acquired
- [ ] MSI digitally signed with certificate
- [ ] SHA-256 checksum generated and published
- [ ] User manual with screenshots complete
- [ ] Video tutorials recorded
- [ ] Support email/website updated
- [ ] Tested on Windows 10 (1809+)
- [ ] Tested on Windows 11
- [ ] Tested on Windows Server 2019/2022
- [ ] Upgrade path from v1.x tested
- [ ] Silent installation tested (for enterprise)
- [ ] Chocolatey package created (optional)

---

## File Inventory

### Installer Directory (`/installer/`)

```
installer/
├── ScaleStreamerV2.wxs          # WiX installer definition (v4)
├── build-installer-v2.ps1       # Build automation script
├── banner.bmp                   # Installer banner (493x58)
├── dialog.bmp                   # Installer dialog (493x312)
├── icon.ico                     # Application icon (multi-res)
├── license.rtf                  # End-user license agreement
├── README.md                    # Installer documentation
└── bin/
    └── ScaleStreamer-v2.0.0.msi # Output MSI (after build)
```

### Source Projects (`/src-v2/`)

```
src-v2/
├── ScaleStreamer.Common/        # Shared library
│   ├── Database/
│   │   └── schema.sql           # SQLite database schema
│   ├── Interfaces/
│   ├── Models/
│   ├── Protocols/
│   └── ...
├── ScaleStreamer.Service/       # Windows Service
│   ├── Program.cs
│   ├── ScaleService.cs
│   ├── appsettings.json
│   └── ...
└── ScaleStreamer.Config/        # Configuration GUI
    ├── Program.cs
    ├── MainForm.cs
    ├── Tabs/
    └── ...
```

### Protocol Templates (`/protocols/`)

```
protocols/
├── manufacturers/
│   └── fairbanks-6011.json      # Fairbanks 6011 protocol
└── generic/
    ├── generic-ascii.json       # Generic ASCII protocol
    └── modbus-tcp.json          # Modbus TCP protocol
```

### Documentation (`/` root)

```
/
├── QUICK-START-V2.md            # Quick start guide
├── BUILD-AND-TEST-V2.md         # Build and test instructions
├── V2-UNIVERSAL-ARCHITECTURE.md # Architecture documentation
├── INSTALLER-UPDATE-SUMMARY.md  # Installer redesign summary
├── INSTALLER-BUILD-READY.md     # This file
└── ...
```

---

## Verification Checklist

### ✅ All Checks Passed

- [x] Installer assets present (banner, dialog, icon, license)
- [x] WiX source file present (ScaleStreamerV2.wxs)
- [x] Build script present (build-installer-v2.ps1)
- [x] Documentation files present (3 files)
- [x] Protocol templates present (3 files)
- [x] Database schema present (schema.sql)
- [x] Solution file present (ScaleStreamer.sln)
- [x] Service project present (ScaleStreamer.Service.csproj)
- [x] Config project present (ScaleStreamer.Config.csproj)
- [x] Common library present (ScaleStreamer.Common.csproj)

**Status**: Ready to build on Windows with .NET 8.0 SDK and WiX Toolset v4

---

## Support

**Documentation**: `/docs/` directory
**Website**: https://cloud-scale.us
**Email**: admin@cloud-scale.us
**GitHub**: https://github.com/CNesbitt2025/Cloud-Scale

---

*Verification completed: 2026-01-24*
*Installer version: 2.0.0*
*All prerequisites: ✅ VERIFIED*
*Build status: 🟢 READY*
