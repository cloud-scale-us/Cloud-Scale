# Scale Streamer Build Instructions

This document provides step-by-step instructions for building Scale Streamer from source on any workstation.

## Prerequisites

### Required Software

1. **.NET 8.0 SDK**
   - Download: https://dotnet.microsoft.com/download/dotnet/8.0
   - Verify installation: `dotnet --version` (should show 8.0.x)

2. **WiX Toolset v5.0 or later**
   - Install via dotnet tool:
     ```powershell
     dotnet tool install --global wix
     ```
   - Verify installation: `wix --version`

3. **Git** (for version control)
   - Download: https://git-scm.com/downloads
   - Verify: `git --version`

4. **PowerShell 5.1 or later**
   - Built into Windows 10/11
   - Verify: `$PSVersionTable.PSVersion`

### Optional Tools

- **GitHub CLI** (for creating releases)
  ```powershell
  winget install GitHub.cli
  gh auth login
  ```

## Repository Structure

```
win-scale/
├── src-v2/                          # Source code
│   ├── ScaleStreamer.Common/        # Shared library
│   ├── ScaleStreamer.Service/       # Windows Service
│   ├── ScaleStreamer.Config/        # GUI Configuration
│   ├── ScaleStreamer.Launcher/      # Launcher app
│   └── ScaleStreamer.TestTool/      # TCP test utility
├── installer/                       # WiX installer project
│   ├── ScaleStreamerV2-SelfContained.wxs
│   ├── build-self-contained.ps1     # Build binaries
│   ├── build-installer-selfcontained.ps1  # Build MSI
│   └── generate-wix-components.ps1  # Generate component files
└── protocols/                       # Protocol definitions
    └── manufacturers/               # Scale protocol JSON files
```

## Building from Source

### Step 1: Clone Repository

```bash
git clone https://github.com/cloud-scale-us/Cloud-Scale.git
cd Cloud-Scale/win-scale
```

### Step 2: Update Version Numbers

Before building a new version, update version numbers in these files:

1. **Project Files** (*.csproj):
   - `src-v2/ScaleStreamer.Common/ScaleStreamer.Common.csproj`
   - `src-v2/ScaleStreamer.Service/ScaleStreamer.Service.csproj`
   - `src-v2/ScaleStreamer.Config/ScaleStreamer.Config.csproj`
   - `src-v2/ScaleStreamer.Launcher/ScaleStreamer.Launcher.csproj`
   - `src-v2/ScaleStreamer.TestTool/ScaleStreamer.TestTool.csproj`

   Update the `<Version>` tag:
   ```xml
   <Version>4.1.0</Version>
   ```

2. **MainForm.cs**:
   ```csharp
   // File: src-v2/ScaleStreamer.Config/MainForm.cs
   private const string APP_VERSION = "4.1.0";
   ```

3. **Program.cs**:
   ```csharp
   // File: src-v2/ScaleStreamer.Config/Program.cs
   Log.Information("Version: {Version}", "4.1.0");
   ```

4. **Installer WXS**:
   ```xml
   <!-- File: installer/ScaleStreamerV2-SelfContained.wxs -->
   <Package Name="Scale Streamer v4.1.0 (Self-Contained)"
            Manufacturer="Cloud-Scale"
            Version="4.1.0"
   ```

### Step 3: Build Self-Contained Binaries

Run the build script from PowerShell:

```powershell
cd installer
.\build-self-contained.ps1
```

This script will:
- Restore NuGet packages
- Compile all projects in Release mode
- Publish self-contained executables (includes .NET runtime)
- Output binaries to `src-v2/*/bin/Release/net8.0*/win-x64/publish/`

**Output locations:**
- Service: `src-v2/ScaleStreamer.Service/bin/Release/net8.0/win-x64/publish/`
- Config GUI: `src-v2/ScaleStreamer.Config/bin/Release/net8.0-windows/win-x64/publish/`
- Launcher: `src-v2/ScaleStreamer.Launcher/bin/Release/net8.0-windows/win-x64/publish/`
- TestTool: `src-v2/ScaleStreamer.TestTool/bin/Release/net8.0/win-x64/publish/`

### Step 4: Build MSI Installer

After building binaries, create the installer:

```powershell
cd installer
.\build-installer-selfcontained.ps1
```

This script will:
1. Verify published binaries exist
2. Generate WiX component definitions (GeneratedComponents.wxs)
3. Compile WiX project
4. Create MSI installer in `installer/bin/`

**Output:**
- Installer: `installer/bin/ScaleStreamer-v4.1.0-YYYYMMDD-HHMMSS.msi`
- Size: ~82 MB (includes .NET runtime)

## Build Scripts Reference

### build-self-contained.ps1

```powershell
# Full build from scratch
.\build-self-contained.ps1

# Build only specific components
dotnet publish src-v2/ScaleStreamer.Service/ScaleStreamer.Service.csproj `
    -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=false
```

### build-installer-selfcontained.ps1

```powershell
# Build installer (requires binaries already built)
.\build-installer-selfcontained.ps1

# Manual WiX build (from installer directory)
wix build ScaleStreamerV2-SelfContained.wxs `
    -arch x64 `
    -out bin/ScaleStreamer-v4.1.0.msi `
    -ext WixToolset.UI.wixext `
    -ext WixToolset.Util.wixext
```

## Troubleshooting

### Error: "wix: command not found"

Install WiX as global tool:
```powershell
dotnet tool install --global wix
```

Verify installation:
```powershell
dotnet tool list --global
```

### Error: "Cannot find published binaries"

Run `build-self-contained.ps1` first before building installer.

### Error: "Access to the path is denied"

Close any running instances of Scale Streamer:
```powershell
Stop-Service ScaleStreamerService
Get-Process | Where-Object {$_.Name -like "*ScaleStreamer*"} | Stop-Process
```

### WiX Component Generation Issues

If WiX can't find files, regenerate components:
```powershell
cd installer
.\generate-wix-components.ps1
```

### Build Warnings (CS8618, CA1416)

These are expected warnings:
- **CS8618**: Nullable reference warnings (safe to ignore - fields initialized in InitializeComponent)
- **CA1416**: Windows-specific APIs (expected - this is Windows-only software)

## Version Numbering Scheme

Scale Streamer uses semantic versioning: `MAJOR.MINOR.PATCH`

- **MAJOR**: Breaking changes, major architecture changes (e.g., 4.0.0 → 5.0.0)
- **MINOR**: New features, backwards compatible (e.g., 4.0.0 → 4.1.0)
- **PATCH**: Bug fixes, minor improvements (e.g., 4.1.0 → 4.1.1)

## Testing the Build

### Test Installation

```powershell
# Install with logging
msiexec /i "installer\bin\ScaleStreamer-v4.1.0-*.msi" /l*v install.log

# Silent install
msiexec /i "installer\bin\ScaleStreamer-v4.1.0-*.msi" /quiet

# Uninstall
msiexec /x "installer\bin\ScaleStreamer-v4.1.0-*.msi" /quiet
```

### Verify Installation

```powershell
# Check service installed
Get-Service ScaleStreamerService

# Check files installed
dir "C:\Program Files\Scale Streamer"

# Check GUI version
& "C:\Program Files\Scale Streamer\Config\ScaleStreamer.Config.exe"
```

### Test Scale Connection

```powershell
# Use test tool
cd "C:\Program Files\Scale Streamer"
.\ScaleStreamer.TestTool.exe 10.1.10.210 --port 5001
```

## Clean Build

To perform a clean build from scratch:

```powershell
# Clean all build artifacts
cd src-v2
dotnet clean -c Release
Remove-Item -Recurse -Force */bin, */obj

# Clean installer
cd ..\installer
Remove-Item -Force bin/*.msi, GeneratedComponents.wxs

# Rebuild
.\build-self-contained.ps1
.\build-installer-selfcontained.ps1
```

## Building on Different Workstations

### First-Time Setup

1. Install prerequisites (see above)
2. Clone repository
3. Run first build:
   ```powershell
   cd win-scale/installer
   .\build-self-contained.ps1
   .\build-installer-selfcontained.ps1
   ```

### Subsequent Builds

1. Pull latest changes:
   ```bash
   git pull origin main
   ```

2. Update version numbers (if new release)

3. Build:
   ```powershell
   cd installer
   .\build-self-contained.ps1
   .\build-installer-selfcontained.ps1
   ```

## Build Environment Variables

These can be set to customize builds:

```powershell
# Custom output directory
$env:BUILD_OUTPUT = "C:\Builds\ScaleStreamer"

# Skip tests (if applicable)
$env:SKIP_TESTS = "true"

# Verbose logging
$env:BUILD_VERBOSE = "true"
```

## Continuous Integration

For automated builds, use this workflow:

```yaml
# Example GitHub Actions workflow
name: Build Scale Streamer

on: [push, pull_request]

jobs:
  build:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v3

      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '8.0.x'

      - name: Install WiX
        run: dotnet tool install --global wix

      - name: Build Binaries
        run: |
          cd win-scale/installer
          .\build-self-contained.ps1

      - name: Build Installer
        run: |
          cd win-scale/installer
          .\build-installer-selfcontained.ps1

      - name: Upload Artifact
        uses: actions/upload-artifact@v3
        with:
          name: installer
          path: win-scale/installer/bin/*.msi
```

## Support

For build issues:
1. Check this documentation
2. Review build logs in `installer/build.log`
3. Open an issue on GitHub: https://github.com/cloud-scale-us/Cloud-Scale/issues

## Related Documentation

- [RELEASE.md](RELEASE.md) - Creating GitHub releases
- [README.md](README.md) - General project information
- [CONTRIBUTING.md](CONTRIBUTING.md) - Contributing guidelines
