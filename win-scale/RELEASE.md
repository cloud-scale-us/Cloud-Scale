# Scale Streamer Release Process

This document describes how to create and publish new releases of Scale Streamer.

## Pre-Release Checklist

Before creating a new release:

- [ ] All code changes committed and pushed to `main` branch
- [ ] Version numbers updated in all files (see BUILD.md)
- [ ] Build succeeds without errors
- [ ] Installer tested on clean Windows machine
- [ ] Release notes drafted
- [ ] All features documented

## Version Number Guidelines

### When to Increment

- **MAJOR (X.0.0)**: Breaking changes, major architecture overhaul
  - Example: v3.0.0 → v4.0.0 (IPC overhaul, auto-connect)

- **MINOR (x.X.0)**: New features, backwards compatible
  - Example: v4.0.0 → v4.1.0 (update checker added)

- **PATCH (x.x.X)**: Bug fixes, minor improvements
  - Example: v4.1.0 → v4.1.1 (fix crash, typo corrections)

### Version History

| Version | Date | Description |
|---------|------|-------------|
| 4.1.0 | 2026-01-26 | Automatic update checker |
| 4.0.0 | 2026-01-26 | Major IPC overhaul, auto-connect |
| 3.4.5 | 2026-01-25 | IPC permissions fix |
| 3.4.4 | 2026-01-25 | TCP diagnostics (HEX dumps) |
| 3.4.3 | 2026-01-25 | CR delimiter fix |
| 3.4.2 | 2026-01-25 | Initial stable release |

## Release Process

### Step 1: Update Version Numbers

Update version in these files (use Find & Replace):

1. **All .csproj files** (5 files):
   ```xml
   <Version>4.1.0</Version>
   ```

2. **MainForm.cs**:
   ```csharp
   private const string APP_VERSION = "4.1.0";
   ```

3. **Program.cs**:
   ```csharp
   Log.Information("Version: {Version}", "4.1.0");
   ```

4. **ScaleStreamerV2-SelfContained.wxs**:
   ```xml
   <Package Name="Scale Streamer v4.1.0 (Self-Contained)"
            Manufacturer="Cloud-Scale"
            Version="4.1.0"
            ...>
   <SummaryInformation Description="Scale Streamer v4.1.0 - Brief Description"
   ```

### Step 2: Build Release Binaries

```powershell
cd win-scale/installer
.\build-self-contained.ps1
```

**Verify build output:**
- Check for compilation warnings/errors
- Confirm DLL versions: `(Get-Item "src-v2\ScaleStreamer.Service\bin\Release\net8.0\win-x64\publish\ScaleStreamer.Service.exe").VersionInfo`

### Step 3: Build Installer

```powershell
.\build-installer-selfcontained.ps1
```

**Output:**
- MSI file in `installer/bin/ScaleStreamer-v4.1.0-YYYYMMDD-HHMMSS.msi`
- Size should be ~82 MB

### Step 4: Test Installation

Test on a clean Windows machine (or VM):

```powershell
# Install
msiexec /i "ScaleStreamer-v4.1.0-*.msi" /l*v install.log

# Verify service
Get-Service ScaleStreamerService

# Launch GUI
& "C:\Program Files\Scale Streamer\Scale Streamer.lnk"

# Test scale connection
cd "C:\Program Files\Scale Streamer"
.\ScaleStreamer.TestTool.exe <scale-ip> --port 5001

# Uninstall
msiexec /x "ScaleStreamer-v4.1.0-*.msi" /quiet
```

### Step 5: Commit Changes

```bash
cd win-scale
git add -A
git commit -m "Release v4.1.0 - Automatic Update Checker

- Added UpdateChecker class for GitHub API integration
- Update notification banner in GUI
- One-click download and install
- All v4.0 features included
- Version numbers updated to 4.1.0"
```

### Step 6: Create Git Tag

```bash
git tag -a v4.1.0 -m "v4.1.0 - Automatic Update Checker"
git push origin main
git push origin v4.1.0
```

### Step 7: Create GitHub Release

#### Option A: Using GitHub CLI (Recommended)

```bash
gh release create v4.1.0 \
  ./installer/bin/ScaleStreamer-v4.1.0-*.msi \
  --title "v4.1.0 - Automatic Update Checker" \
  --notes-file RELEASE_NOTES.md
```

#### Option B: Manual Upload

1. Go to: https://github.com/cloud-scale-us/Cloud-Scale/releases/new
2. Choose tag: `v4.1.0`
3. Release title: `v4.1.0 - Automatic Update Checker`
4. Upload MSI file: `ScaleStreamer-v4.1.0-*.msi`
5. Add release notes (see template below)
6. Click "Publish release"

### Step 8: Verify Release

```bash
# Check release exists
gh release view v4.1.0

# Verify asset uploaded
gh release view v4.1.0 --json assets --jq '.assets[].name'

# Test download
gh release download v4.1.0 --pattern "*.msi" --dir ./test-download
```

## Release Notes Template

```markdown
## Scale Streamer v4.X.X

Brief one-line description of the release.

### New Features
- **Feature Name**: Description of new feature
- **Another Feature**: What it does and why it matters

### Improvements
- Improvement 1
- Improvement 2
- Performance optimization in X

### Bug Fixes
- Fixed issue where X would Y (#123)
- Resolved crash when Z happened
- Corrected typo in error message

### Technical Details
- Technical change 1
- API modification 2
- Dependency update 3

### Breaking Changes (if any)
- What changed that might affect users
- Migration steps required

### All vX.0 Features Included (for minor/patch releases)
- ✅ Major feature from v4.0
- ✅ Another feature from v4.0
- ✅ Core functionality

### Installation
This is a self-contained installer including all .NET dependencies. No .NET Runtime installation required.

**File**: ScaleStreamer-v4.X.X-YYYYMMDD-HHMMSS.msi (82 MB)

### Upgrade Notes
This version can upgrade from any previous version. The MajorUpgrade element will automatically uninstall older versions during installation.

### System Requirements
- Windows 10/11 (64-bit)
- 200 MB disk space
- Network connectivity for scale communication

🤖 Generated with [Claude Code](https://claude.com/claude-code)
```

## Release Notes Examples

### Major Release (4.0.0)

```markdown
## Scale Streamer v4.0.0

This is a major feature release that includes significant improvements to IPC communication, auto-connect functionality, and TCP diagnostics.

### Major Features
- **IPC Overhaul**: Fixed Named Pipe permissions to allow GUI access without admin privileges
- **Auto-Connect**: Service automatically connects to scale on startup using AppSettings
- **Fire-and-Forget Welcome**: Eliminated IPC deadlock with non-blocking welcome messages
- **AppSettings**: File-based persistent configuration with auto-reload

### Improvements
- **TCP Diagnostics**: Added HEX dumps and buffer analysis for debugging scale connections
- **CR Delimiter Fix**: Proper support for Fairbanks 6011 scales (CR only, not CRLF)
- **MonitoringTab Rewrite**: Complete overhaul of GUI monitoring with direct IPC handling
- **Comprehensive Logging**: Serilog integration throughout all components
```

### Minor Release (4.1.0)

```markdown
## Scale Streamer v4.1.0

This release adds automatic update checking and notification functionality to the GUI.

### New Features
- **Automatic Update Checker**: GUI automatically checks for new versions on GitHub
- **Update Notifications**: Yellow notification banner appears when new version is available
- **One-Click Download**: Download and install updates directly from the notification
- **Release Notes Viewer**: View what's new in each release before updating

### All v4.0 Features Included
- ✅ IPC Overhaul: Named Pipe permissions fixed for non-admin GUI access
- ✅ Auto-Connect: Service automatically connects to scale on startup
```

### Patch Release (4.1.1)

```markdown
## Scale Streamer v4.1.1

This is a maintenance release with bug fixes and minor improvements.

### Bug Fixes
- Fixed crash when update checker times out (#45)
- Resolved memory leak in continuous TCP reading (#47)
- Corrected version display in About dialog

### Improvements
- Increased update check timeout from 5s to 10s
- Better error messages for network failures
```

## Post-Release Tasks

After publishing a release:

1. **Announce Release**
   - Update internal documentation
   - Notify users via email/Slack
   - Post on social media if applicable

2. **Update Documentation**
   - Update README.md with new version number
   - Add changelog entry
   - Update screenshots if UI changed

3. **Monitor for Issues**
   - Watch GitHub Issues for bug reports
   - Monitor download metrics
   - Check for update checker working correctly

4. **Plan Next Release**
   - Review feature requests
   - Prioritize bug fixes
   - Update project roadmap

## Hotfix Process

For critical bugs requiring immediate release:

1. Create hotfix branch:
   ```bash
   git checkout -b hotfix/v4.1.2 v4.1.1
   ```

2. Fix the bug and commit

3. Update version to patch level (4.1.1 → 4.1.2)

4. Build and test

5. Merge to main:
   ```bash
   git checkout main
   git merge --no-ff hotfix/v4.1.2
   git tag -a v4.1.2 -m "Hotfix: Critical bug fix"
   git push origin main --tags
   ```

6. Create GitHub release immediately

## Rollback Process

If a release has critical issues:

1. **Mark Release as Pre-release** (not latest):
   ```bash
   gh release edit v4.1.0 --prerelease
   ```

2. **Create Rollback Release**:
   - Re-release previous stable version as v4.1.1
   - Add notes explaining the rollback

3. **Fix Issues**:
   - Address critical bugs
   - Release fixed version as v4.2.0

## Release Automation

### Future GitHub Actions Workflow

```yaml
# .github/workflows/release.yml
name: Create Release

on:
  push:
    tags:
      - 'v*.*.*'

jobs:
  build-and-release:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v3

      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '8.0.x'

      - name: Install WiX
        run: dotnet tool install --global wix

      - name: Build
        run: |
          cd win-scale/installer
          .\build-self-contained.ps1
          .\build-installer-selfcontained.ps1

      - name: Create Release
        uses: softprops/action-gh-release@v1
        with:
          files: win-scale/installer/bin/*.msi
          generate_release_notes: true
        env:
          GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
```

## Troubleshooting

### "Tag already exists"

```bash
# Delete local and remote tag
git tag -d v4.1.0
git push origin :refs/tags/v4.1.0

# Recreate tag
git tag -a v4.1.0 -m "v4.1.0 - Description"
git push origin v4.1.0
```

### "Release already exists"

```bash
# Delete release
gh release delete v4.1.0 --yes

# Recreate
gh release create v4.1.0 ./installer/bin/*.msi --title "..." --notes "..."
```

### Installer Version Mismatch

If the MSI shows wrong version:
1. Verify all version numbers updated (Step 1)
2. Clean build: `dotnet clean -c Release`
3. Rebuild: `.\build-self-contained.ps1`
4. Verify DLL version: `(Get-Item "path\to\exe").VersionInfo.FileVersion`

## Support

For release process questions:
- Review this documentation
- Check GitHub Actions logs (when available)
- Contact: support@cloud-scale.us
- GitHub Issues: https://github.com/cloud-scale-us/Cloud-Scale/issues

## Related Documentation

- [BUILD.md](BUILD.md) - Build instructions
- [CONTRIBUTING.md](CONTRIBUTING.md) - Contributing guidelines
- [README.md](README.md) - Project overview
