# Scale Streamer v2.0 - Installer

This directory contains the WiX installer configuration for Scale Streamer v2.0.

## Prerequisites

### Required Software

1. **.NET 8.0 SDK** (already installed)
2. **WiX Toolset v4** (install via dotnet tool)
3. **PowerShell 5.1+** (for build script)

### Install WiX Toolset

```powershell
# Install WiX v4 as global tool
dotnet tool install --global wix

# Or update if already installed
dotnet tool update --global wix

# Verify installation
wix --version
```

## Building the Installer

### Quick Build

```powershell
cd installer
.\build-installer-v2.ps1
```

### Build Options

```powershell
# Build with specific configuration
.\build-installer-v2.ps1 -Configuration Release

# Skip rebuild (use existing binaries)
.\build-installer-v2.ps1 -SkipBuild

# Debug configuration
.\build-installer-v2.ps1 -Configuration Debug
```

## Installer Features

### Components Installed

1. **Windows Service** (`ScaleStreamerService`)
   - Installs to: `C:\Program Files\Scale Streamer\Service\`
   - Auto-starts on boot
   - Runs as LocalSystem
   - Service recovery configured (restart on failure)

2. **Configuration GUI** (`ScaleStreamer.Config.exe`)
   - Installs to: `C:\Program Files\Scale Streamer\Config\`
   - Desktop shortcut created
   - Start menu shortcuts

3. **Protocol Templates**
   - Fairbanks 6011
   - Generic ASCII
   - Modbus TCP
   - Installed to: `C:\Program Files\Scale Streamer\protocols\`

4. **Database and Logs**
   - Database: `C:\ProgramData\ScaleStreamer\scalestreamer.db`
   - Logs: `C:\ProgramData\ScaleStreamer\logs\`
   - Backups: `C:\ProgramData\ScaleStreamer\backups\`

5. **Documentation**
   - Quick Start Guide
   - Build and Test Instructions
   - Architecture Documentation

### Automatic Configuration

- ✅ Windows Service installation and auto-start
- ✅ Firewall rules (ports 8554, 8888)
- ✅ Registry keys for paths and settings
- ✅ Database initialization
- ✅ Start menu shortcuts
- ✅ Desktop shortcut

## Installation

### Interactive Installation

```powershell
# Run installer
msiexec /i ScaleStreamer-v2.0.0.msi

# Or double-click the MSI file
```

### Silent Installation

```powershell
# Silent install with logging
msiexec /i ScaleStreamer-v2.0.0.msi /quiet /l*v install.log

# Verify installation
sc.exe query ScaleStreamerService
```

### Custom Installation Path

```powershell
# Install to custom directory
msiexec /i ScaleStreamer-v2.0.0.msi INSTALLFOLDER="D:\ScaleStreamer"
```

## Uninstallation

### Interactive Uninstall

```powershell
# Via Programs and Features
# Or via Start Menu shortcut
```

### Silent Uninstall

```powershell
# Stop service first
sc.exe stop ScaleStreamerService

# Uninstall
msiexec /x ScaleStreamer-v2.0.0.msi /quiet /l*v uninstall.log
```

## Testing the Installation

### 1. Verify Service

```powershell
# Check service status
sc.exe query ScaleStreamerService

# Expected output:
#   STATE: 4 RUNNING
```

### 2. Check Files

```powershell
# Service files
dir "C:\Program Files\Scale Streamer\Service"

# Config GUI
dir "C:\Program Files\Scale Streamer\Config"

# Protocol templates
dir "C:\Program Files\Scale Streamer\protocols"

# Database
dir "C:\ProgramData\ScaleStreamer"
```

### 3. Check Firewall Rules

```powershell
netsh advfirewall firewall show rule name="Scale Streamer - RTSP"
netsh advfirewall firewall show rule name="Scale Streamer - HLS"
```

### 4. Launch Configuration GUI

```powershell
# From Start Menu or Desktop
# Or directly:
& "C:\Program Files\Scale Streamer\Config\ScaleStreamer.Config.exe"
```

### 5. View Service Logs

```powershell
# Service logs
Get-Content "C:\ProgramData\ScaleStreamer\logs\service-*.log" -Tail 50

# Installation log
Get-Content install.log -Tail 100
```

## Installer Structure

```
installer/
├── ScaleStreamerV2.wxs          # WiX source file (main installer definition)
├── build-installer-v2.ps1       # Build script
├── license.rtf                  # License agreement (auto-generated)
├── banner.bmp                   # Installer banner (493x58 pixels)
├── dialog.bmp                   # Installer dialog (493x312 pixels)
├── icon.ico                     # Application icon
└── bin/
    └── ScaleStreamer-v2.0.0.msi # Output MSI file
```

## WiX Configuration Details

### Component Groups

| Group | Description |
|-------|-------------|
| ServiceComponents | Windows Service executables and configs |
| ServiceRuntimeComponents | .NET runtime DLLs for service |
| ConfigComponents | Configuration GUI executables |
| ConfigRuntimeComponents | .NET runtime DLLs for GUI |
| ProtocolComponents | JSON protocol templates |
| DocumentationComponents | User guides |
| AppDataComponents | Database and log directories |
| RegistryComponents | Registry keys |
| ShortcutComponents | Start menu and desktop shortcuts |

### Custom Actions

| Action | Timing | Description |
|--------|--------|-------------|
| AddFirewallRules | After InstallFiles | Adds Windows Firewall rules for RTSP/HLS |
| RemoveFirewallRules | Before RemoveFiles | Removes firewall rules on uninstall |
| InitializeDatabase | After AddFirewallRules | Creates initial database schema |
| LaunchConfigApp | After InstallFinalize | Opens config GUI after installation |

### Registry Keys

**Location**: `HKLM\Software\Cloud-Scale\ScaleStreamer`

| Key | Value |
|-----|-------|
| InstallPath | Installation directory |
| Version | 2.0.0 |
| Vendor | Cloud-Scale |
| SupportURL | https://cloud-scale.us/support |
| ServicePath | Service directory |
| ConfigPath | Config GUI directory |
| ProtocolsPath | Protocol templates directory |
| LogsPath | Logs directory |
| BackupsPath | Backups directory |

## Troubleshooting

### Installer Won't Build

**Error**: "WiX Toolset not found"
```powershell
# Install WiX
dotnet tool install --global wix

# Add to PATH if needed
$env:PATH += ";$env:USERPROFILE\.dotnet\tools"
```

**Error**: "Project build failed"
```powershell
# Clean and rebuild
dotnet clean
dotnet restore
.\build-installer-v2.ps1
```

### Installation Fails

**Error**: "Service installation failed"
```powershell
# Check if service already exists
sc.exe query ScaleStreamerService

# If exists, uninstall old version first
sc.exe delete ScaleStreamerService
```

**Error**: "Access denied"
```powershell
# Run installer as administrator
# Right-click MSI → Run as administrator
```

### Service Won't Start

**Error**: "Service failed to start"
```powershell
# Check event log
Get-WinEvent -LogName Application -MaxEvents 20 |
    Where-Object { $_.Message -like "*ScaleStreamer*" }

# Check service log
Get-Content "C:\ProgramData\ScaleStreamer\logs\service-*.log" -Tail 50

# Try starting manually
sc.exe start ScaleStreamerService
```

### Firewall Rules Not Added

```powershell
# Manually add rules
netsh advfirewall firewall add rule name="Scale Streamer - RTSP" dir=in action=allow protocol=TCP localport=8554

netsh advfirewall firewall add rule name="Scale Streamer - HLS" dir=in action=allow protocol=TCP localport=8888
```

## Upgrading

### From v1.x to v2.0

v2.0 uses a different UpgradeCode, so it will install side-by-side with v1.x.

**Recommended upgrade procedure**:

1. Stop v1.x application (if running)
2. Install v2.0 (v1.x remains installed)
3. Test v2.0 functionality
4. Uninstall v1.x when satisfied

**Migration**:
- Database schemas are different (v1.x and v2.0 are separate)
- Protocol configurations must be reconfigured in v2.0

### From v2.0.0 to v2.x.x

Future v2.x versions will use the same UpgradeCode and will automatically upgrade.

## Asset Requirements

For a complete branded installer, you need:

### Image Files

1. **banner.bmp** (493 x 58 pixels)
   - Top banner in installer
   - Use Cloud-Scale branding

2. **dialog.bmp** (493 x 312 pixels)
   - Left side of installer dialogs
   - Use Cloud-Scale logo/imagery

3. **icon.ico** (multi-resolution)
   - 16x16, 32x32, 48x48, 256x256
   - Used in Add/Remove Programs
   - Used for shortcuts

### Creating Assets

See `../assets/README.md` for instructions on converting SVG assets to required formats.

## Distribution

### MSI Package

The built MSI can be distributed:
- Via download from website
- On USB/physical media
- Through software deployment tools (SCCM, Intune, etc.)
- Via package managers (Chocolatey, winget)

### Digital Signing (Recommended)

For production distribution, sign the MSI:

```powershell
# Sign with code signing certificate
signtool sign /f certificate.pfx /p password /t http://timestamp.digicert.com ScaleStreamer-v2.0.0.msi
```

## Support

**Documentation**: See `../docs/` directory
**Website**: https://cloud-scale.us
**Email**: admin@cloud-scale.us
**GitHub**: https://github.com/CNesbitt2025/Cloud-Scale

---

*Last updated: 2026-01-24*
*Installer version: 2.0.0*
*WiX Toolset version: 4.x*
