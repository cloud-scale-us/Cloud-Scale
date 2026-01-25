# GitHub Release Guide

Follow these steps to create a GitHub Release with the installer download.

## Step 1: Create GitHub Release

1. **Go to your repository:** https://github.com/CNesbitt2025/Cloud-Scale

2. **Navigate to Releases:**
   - Click on "Releases" in the right sidebar
   - Or go directly to: https://github.com/CNesbitt2025/Cloud-Scale/releases

3. **Click "Draft a new release"**

4. **Fill in Release Details:**

   **Tag version:** `v2.0.1`

   **Release title:** `Scale Streamer v2.0.1 - Universal Scale Platform`

   **Description:**
   ```markdown
   # Scale Streamer v2.0.1 🎉

   **First official release of the universal industrial scale data acquisition platform!**

   ## ✨ What's New

   - **Universal Protocol Engine**: Connect to any scale brand (Fairbanks, Toledo, Mettler Toledo, Rice Lake, and more)
   - **Windows Service Architecture**: Professional background service with auto-start
   - **Modern Configuration GUI**: Easy-to-use WinForms interface
   - **Self-Contained Installer**: No .NET installation required - everything included!
   - **Protocol Templates**: XML-based protocol definitions for easy customization
   - **Multiple Connection Types**: TCP/IP, Serial (RS232), UDP
   - **Real-Time Monitoring**: Live weight data display
   - **Robust Error Handling**: Auto-reconnect and comprehensive logging

   ## 📥 Installation

   1. Download `ScaleStreamer-v2.0.1-YYYYMMDD-HHMMSS.msi` below
   2. Run the installer (requires Administrator rights)
   3. Launch "Scale Streamer Configuration" from Start Menu
   4. Configure your scale connection and protocol
   5. Start streaming!

   **No .NET Runtime required** - The installer includes all dependencies (~55 MB)

   ## 📋 System Requirements

   - Windows 10/11 or Windows Server 2016+ (64-bit)
   - Administrator rights for installation
   - ~200 MB disk space

   ## 🎯 Supported Scales

   Pre-configured templates included for:
   - Fairbanks scales (all models)
   - Toledo scales
   - Mettler Toledo scales
   - Rice Lake scales
   - Avery Weigh-Tronix scales
   - Generic ASCII weight output

   Custom protocol templates can be created in minutes using XML.

   ## 📖 Documentation

   - [Quick Start Guide](QUICK-START-V2.md)
   - [Build Instructions](BUILD-AND-TEST-V2.md)
   - [Architecture Overview](V2-UNIVERSAL-ARCHITECTURE.md)
   - [Protocol Templates](protocols/)
   - [Complete Documentation](CLAUDE.md)

   ## 🐛 Known Issues

   None reported yet! This is the first stable release.

   ## 💬 Support

   - **Issues**: [Report bugs or request features](../../issues)
   - **Email**: support@cloud-scale.us

   ---

   **Full Changelog:** https://github.com/CNesbitt2025/Cloud-Scale/commits/main
   ```

5. **Attach the Installer File:**
   - Scroll down to the "Attach binaries" section at the bottom
   - Click "Attach files by dragging & dropping, selecting or pasting them"
   - Upload: `installer/bin/ScaleStreamer-v2.0.1-20260124-213324.msi`

   **IMPORTANT:** The file will be uploaded and attached to the release

6. **Publish Options:**
   - ✅ Check "Set as the latest release" (so the download link works)
   - ⬜ Leave "Set as a pre-release" unchecked
   - ⬜ Leave "Create a discussion for this release" unchecked (or check if you want community discussion)

7. **Click "Publish release"**

## Step 2: Verify the Release

After publishing:

1. Go to https://github.com/CNesbitt2025/Cloud-Scale/releases
2. Verify your release appears with the "Latest" badge
3. Click on the MSI file to verify it downloads correctly
4. Test the download link in the README: https://github.com/CNesbitt2025/Cloud-Scale/releases/latest

## Step 3: Update README (if needed)

The README has already been updated with the download section. Once you create the release, the download link will automatically work.

## Alternative: Using GitHub CLI

If you prefer command-line, you can create the release using `gh` CLI:

```powershell
# Navigate to repository
cd /mnt/d/win-scale/win-scale

# Create release with MSI attached
gh release create v2.0.1 `
  --title "Scale Streamer v2.0.1 - Universal Scale Platform" `
  --notes-file RELEASE-NOTES.md `
  installer/bin/ScaleStreamer-v2.0.1-20260124-213324.msi
```

## Next Steps

After creating the release:

1. ✅ Test the download link
2. ✅ Share the release on social media / with users
3. ✅ Monitor GitHub Issues for bug reports
4. 🔄 Plan next release (v2.0.2 or v2.1.0)

## File Details

**Installer File:** `ScaleStreamer-v2.0.1-20260124-213324.msi`
- **Size:** ~55 MB
- **Type:** Windows Installer Package
- **Location:** `installer/bin/`
- **Self-Contained:** Yes (includes .NET 8.0 runtime)
- **Architecture:** x64

---

**Note:** Once the release is published, the README download link will automatically point to the latest release!
