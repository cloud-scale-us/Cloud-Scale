# Scale Streamer Quick Start Guide

Fast reference for building and releasing Scale Streamer.

## TL;DR - New Release in 5 Commands

```powershell
# 1. Update version numbers (see BUILD.md for file locations)
# Edit: *.csproj, MainForm.cs, Program.cs, ScaleStreamerV2-SelfContained.wxs

# 2. Build binaries
cd win-scale/installer
.\build-self-contained.ps1

# 3. Build installer
.\build-installer-selfcontained.ps1

# 4. Commit and tag
git add -A
git commit -m "Release v4.2.0 - Description"
git tag -a v4.2.0 -m "v4.2.0 - Description"
git push origin main --tags

# 5. Create GitHub release
gh release create v4.2.0 ./bin/ScaleStreamer-v4.2.0-*.msi \
  --title "v4.2.0 - Feature Name" \
  --notes "Release notes here"
```

## Prerequisites (One-Time Setup)

```powershell
# Install .NET SDK 8.0
winget install Microsoft.DotNet.SDK.8

# Install WiX Toolset
dotnet tool install --global wix

# Install GitHub CLI (optional)
winget install GitHub.cli
gh auth login
```

## Version Number Locations

Quick checklist of files to update:

- [ ] `src-v2/ScaleStreamer.Common/ScaleStreamer.Common.csproj` → `<Version>X.X.X</Version>`
- [ ] `src-v2/ScaleStreamer.Service/ScaleStreamer.Service.csproj` → `<Version>X.X.X</Version>`
- [ ] `src-v2/ScaleStreamer.Config/ScaleStreamer.Config.csproj` → `<Version>X.X.X</Version>`
- [ ] `src-v2/ScaleStreamer.Launcher/ScaleStreamer.Launcher.csproj` → `<Version>X.X.X</Version>`
- [ ] `src-v2/ScaleStreamer.TestTool/ScaleStreamer.TestTool.csproj` → `<Version>X.X.X</Version>`
- [ ] `src-v2/ScaleStreamer.Config/MainForm.cs` → `APP_VERSION = "X.X.X"`
- [ ] `src-v2/ScaleStreamer.Config/Program.cs` → `Log.Information("Version: {Version}", "X.X.X")`
- [ ] `installer/ScaleStreamerV2-SelfContained.wxs` → `Version="X.X.X"` (2 places)

**Pro tip**: Use Find & Replace in your editor to update all at once!

## Build Commands

```powershell
# Full clean build
cd win-scale
dotnet clean -c Release src-v2/
cd installer
.\build-self-contained.ps1
.\build-installer-selfcontained.ps1
```

## Common Tasks

### Check Current Version

```powershell
# From source
grep -r "APP_VERSION" src-v2/ScaleStreamer.Config/MainForm.cs

# From installed app
(Get-Item "C:\Program Files\Scale Streamer\Config\ScaleStreamer.Config.exe").VersionInfo
```

### Test Build Locally

```powershell
# Install
msiexec /i "installer\bin\ScaleStreamer-v4.X.X-*.msi" /l*v install.log

# Test
Get-Service ScaleStreamerService
& "C:\Program Files\Scale Streamer\Scale Streamer.lnk"

# Uninstall
msiexec /x "installer\bin\ScaleStreamer-v4.X.X-*.msi" /quiet
```

### Create Release on GitHub

```bash
# With GitHub CLI
gh release create v4.2.0 \
  ./installer/bin/ScaleStreamer-v4.2.0-*.msi \
  --title "v4.2.0 - Feature Name" \
  --notes "See RELEASE_NOTES.md"

# Verify
gh release view v4.2.0
```

### Fix Version Mismatch

```powershell
# If MSI shows wrong version:
1. Update all 8 version locations (see checklist above)
2. dotnet clean -c Release
3. .\build-self-contained.ps1
4. .\build-installer-selfcontained.ps1
```

## Directory Structure Quick Reference

```
win-scale/
├── src-v2/              # Source code
│   ├── ScaleStreamer.Common/
│   ├── ScaleStreamer.Service/
│   └── ScaleStreamer.Config/
├── installer/           # Build scripts
│   ├── build-self-contained.ps1
│   ├── build-installer-selfcontained.ps1
│   └── bin/            # Output MSI files
└── protocols/          # Scale protocol definitions
```

## Troubleshooting Quick Fixes

| Problem | Solution |
|---------|----------|
| "wix: command not found" | `dotnet tool install --global wix` |
| "Cannot find published binaries" | Run `build-self-contained.ps1` first |
| "Tag already exists" | `git tag -d v4.X.X && git push origin :refs/tags/v4.X.X` |
| Build warnings (CS8618) | Safe to ignore - nullable warnings |
| Build warnings (CA1416) | Safe to ignore - Windows-only APIs |
| "Access denied" | Close all Scale Streamer instances first |

## Release Checklist

Pre-release:
- [ ] All changes committed
- [ ] Version numbers updated (8 files)
- [ ] Build succeeds without errors
- [ ] Tested on clean Windows machine

Create release:
- [ ] Build binaries: `build-self-contained.ps1`
- [ ] Build installer: `build-installer-selfcontained.ps1`
- [ ] Commit: `git commit -m "Release v4.X.X"`
- [ ] Tag: `git tag -a v4.X.X -m "Description"`
- [ ] Push: `git push origin main --tags`
- [ ] Release: `gh release create v4.X.X ./bin/*.msi`

Post-release:
- [ ] Verify download link works
- [ ] Test update checker (if applicable)
- [ ] Update documentation

## Version Numbering Quick Guide

- **Major (X.0.0)**: Breaking changes (e.g., 3.x → 4.0)
- **Minor (x.X.0)**: New features (e.g., 4.0 → 4.1)
- **Patch (x.x.X)**: Bug fixes (e.g., 4.1.0 → 4.1.1)

## Git Workflow Quick Reference

```bash
# Start new feature
git checkout -b feature/update-checker

# Commit changes
git add -A
git commit -m "Add update checker"

# Merge to main
git checkout main
git merge feature/update-checker

# Tag release
git tag -a v4.1.0 -m "v4.1.0 - Update checker"
git push origin main --tags

# Hotfix
git checkout -b hotfix/v4.1.1 v4.1.0
# ... fix bug ...
git checkout main
git merge hotfix/v4.1.1
git tag -a v4.1.1 -m "Hotfix: Critical bug"
git push origin main --tags
```

## Output File Naming Convention

```
ScaleStreamer-v{VERSION}-{DATE}-{TIME}.msi

Example:
ScaleStreamer-v4.1.0-20260126-150748.msi
                │      │        └─ HHMMSS (time)
                │      └─ YYYYMMDD (date)
                └─ Semantic version
```

## Links

- **Full Build Guide**: [BUILD.md](BUILD.md)
- **Release Process**: [RELEASE.md](RELEASE.md)
- **GitHub Repo**: https://github.com/cloud-scale-us/Cloud-Scale
- **Issues**: https://github.com/cloud-scale-us/Cloud-Scale/issues
- **Releases**: https://github.com/cloud-scale-us/Cloud-Scale/releases

## Support

Questions? Check:
1. This quick start guide
2. [BUILD.md](BUILD.md) for detailed instructions
3. [RELEASE.md](RELEASE.md) for release process
4. GitHub Issues for known problems
