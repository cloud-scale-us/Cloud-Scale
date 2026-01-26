# Scale Streamer v3.4.2 - Quick Setup Wizard & UX Improvements

**Release Date:** January 25, 2026

## 🚀 Major New Feature: Auto-Detection Quick Setup Wizard

### Quick Setup Wizard
**Finally - truly automatic scale configuration!** No more guessing at settings or manual protocol configuration.

**What it does:**
1. **Tests Network Connectivity** - Verifies IP address is reachable
2. **Tests TCP Port** - Confirms port is open and accepting connections
3. **Reads Live Data Stream** - Captures actual data from your scale
4. **Auto-Detects Protocol** - Analyzes data format and identifies protocol automatically
5. **Populates All Settings** - Fills in all configuration fields for you

**Supported Auto-Detection:**
- ✅ **Fairbanks 6011** - Detects STATUS + WEIGHT + TARE format
- ✅ **Generic ASCII** - Detects simple weight value formats
- ✅ **Modbus TCP** - Detects binary Modbus protocol

**How to use:**
1. Open **Connection** tab
2. Click **"Auto-Detect Scale..."** button
3. Enter IP address (e.g., `10.1.10.210`) and port (e.g., `5001`)
4. Click **Scan**
5. Wait 10 seconds while wizard:
   - Tests connection
   - Reads data
   - Detects protocol
6. Review detected configuration
7. Click **"Use This Config"**
8. Save configuration

**No more trial and error!** 🎉

## ✨ Protocol Template Auto-Loading

When you manually select a protocol (e.g., "Fairbanks 6011"), the GUI now automatically:
- ✅ Loads default port (5001 for Fairbanks 6011)
- ✅ Loads timeout settings
- ✅ Sets connection type (TCP/IP)
- ✅ Configures auto-reconnect settings

**Before v3.4.2:**
```
User selects "Fairbanks 6011"
Port field stays at default: 502
User has to manually change to 5001
```

**After v3.4.2:**
```
User selects "Fairbanks 6011"
Port automatically changes to: 5001 ✓
Timeout set to: 5000ms ✓
Auto-reconnect enabled ✓
```

## 🐛 Bug Fixes

### Fixed: Log File Access Denied (CRITICAL)

**Problem:**
```
[ERROR] Could not load service logs: The process cannot access the file because it is being used by another process
```

Logging tab couldn't read log files while service was running due to exclusive file locks.

**Fix:**
Changed log file reader to use `FileShare.ReadWrite`, allowing read access while service writes:

```csharp
// OLD (failed):
var lines = File.ReadLines(logFile).TakeLast(100).ToList();

// NEW (works):
using (var fileStream = new FileStream(logFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
using (var reader = new StreamReader(fileStream))
{
    // Read log file while service has it open
}
```

**Result:** ✅ Logging tab now shows real-time logs even while service is running!

## 📋 What's Included in v3.4.2

All features from previous versions, plus new additions:

**New in v3.4.2:**
- ✅ Quick Setup Wizard with auto-detection
- ✅ Protocol template auto-loading
- ✅ Log file sharing fix

**From v3.4.1:**
- ✅ Embedded database schema (tables always created)
- ✅ Protocol templates load correctly

**From v3.4.0:**
- ✅ Built-in diagnostic script in install folder
- ✅ Real-time logging tab (reads actual service logs)
- ✅ Comprehensive diagnostics instructions

**From v3.3.3:**
- ✅ Protocols path resolution fix
- ✅ All 7 tabs fully functional

## 🎯 User Experience Improvements

### For New Users:
1. Install Scale Streamer
2. Open Connection tab
3. Click "Auto-Detect Scale..."
4. Enter IP and port
5. Click Scan
6. Done! ✓

### For Advanced Users:
- Manual protocol selection now pre-fills all settings
- No more looking up default port numbers
- Protocol templates loaded from install folder

## 🔍 How to Verify v3.4.2 Features

### Test Quick Setup Wizard:
1. Open Scale Streamer Configuration
2. Go to **Connection** tab
3. Click **"Auto-Detect Scale..."** button
4. Enter scale IP: `10.1.10.210`, Port: `5001`
5. Click **Scan**
6. Should detect "Fairbanks 6011" protocol
7. Click "Use This Config" to apply

### Test Protocol Auto-Loading:
1. Go to **Connection** tab
2. Select Manufacturer: **"Fairbanks Scales"**
3. Select Protocol: **"Fairbanks 6011"**
4. Watch port field automatically change to **5001** ✓

### Test Logging Tab:
1. Go to **Logs** tab
2. Should see real service log entries (not sample data)
3. Click **Refresh** button
4. Should load latest 100 log lines without errors

## 📦 Installation

### Clean Install:
```powershell
msiexec /i ScaleStreamer-v3.4.2-20260125-185106.msi
```

### Upgrade from v3.4.0 or v3.4.1:
Just install v3.4.2 - it will auto-upgrade and preserve your database.

### Silent Install:
```powershell
msiexec /i ScaleStreamer-v3.4.2-20260125-185106.msi /quiet
```

## 🔄 Version History

- **v3.4.2** - Quick setup wizard, protocol auto-loading, log file fixes
- **v3.4.1** - **CRITICAL** database schema fix
- **v3.4.0** - Built-in diagnostics, real-time logging (broken database)
- **v3.3.3** - Protocols path fix
- **v3.3.2** - Tab visibility improvements
- **v3.3.1** - Diagnostics tab
- **v3.2.0** - Unified launcher

## 📞 Support

- **Quick Check:** Run `C:\Program Files\Scale Streamer\collect-diagnostics.ps1`
- **Diagnostics:** `C:\Program Files\Scale Streamer\collect-diagnostics.ps1`
- **Logs:** `C:\ProgramData\ScaleStreamer\logs\`
- **Database:** `C:\ProgramData\ScaleStreamer\scalestreamer.db`
- **Email:** admin@cloud-scale.us
- **Web:** https://cloud-scale.us/support

## 💡 Technical Details

### QuickSetupWizard.cs
New 700+ line WinForms dialog that:
- Tests TCP connectivity with 5-second timeout
- Reads data stream for up to 10 seconds
- Analyzes patterns using regex to detect protocol
- Supports early exit when 5+ samples collected
- Returns detected configuration to ConnectionTab

**Protocol Detection Logic:**
```csharp
// Fairbanks 6011: "STATUS  WEIGHT  TARE"
Pattern: ^[12]\""?\s+\-?\d+\s+\d+

// Generic ASCII: "1234.5" or "1234.5 lb"
Pattern: \d+\.?\d*\s*(lb|kg|g|oz)?

// Modbus TCP: Binary data with non-printable characters
Detection: Check for characters < ASCII 32
```

### Protocol Template Loading
When user selects a protocol, GUI now:
1. Looks up template file in `C:\Program Files\Scale Streamer\protocols\`
2. Loads JSON template
3. Parses `connection` section
4. Applies defaults to GUI fields:
   - Port number
   - Timeout
   - Auto-reconnect
   - Connection type

### Log File Sharing
Changed from:
```csharp
File.ReadLines(logFile)  // Requires exclusive access
```

To:
```csharp
new FileStream(logFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
// Allows reading while service writes
```

## ⚠️ Known Issues

- Monitoring tab still requires scale to be configured and connected
- Diagnostics tab shows live data only when scale is actively sending
- Connection status not updated in real-time (shows "Not Connected" until manual test)

These will be addressed in future releases.

## 🎉 Summary

**v3.4.2 is the most user-friendly release yet!**

- **For beginners:** Auto-detection wizard makes setup effortless
- **For advanced users:** Protocol templates auto-populate settings
- **For troubleshooting:** Logging tab now works correctly with live logs

No more guessing at protocol settings. No more trial and error. Just enter your scale's IP and port, click Scan, and you're done! ✨
