# Scale Streamer v3.4.4 - Enhanced TCP Diagnostics

**Release Date:** January 25, 2026

## 🔍 DIAGNOSTIC ENHANCEMENTS

This release dramatically improves troubleshooting capabilities by adding comprehensive TCP-level diagnostics and logging. Now you can see exactly what's happening with scale connections!

**Use case:** When your scale connection isn't working, you can now see:
- If data is actually being received from the scale
- What the raw data looks like (HEX + ASCII)
- If the delimiter configuration matches what the scale sends
- Where parsing is failing

---

## ✨ What's New

### 1. Enhanced TCP Data Logging

Every TCP read operation now logs detailed diagnostic information:

**First data reception:**
```
[TCP] First data received: 18 bytes from 10.1.10.210:5001
```

**Raw byte logging with HEX and ASCII:**
```
[TCP] Received 18 bytes | HEX: 31 20 20 20 20 20 39 36 30... | ASCII: 1     960    00\r\n
```

**Complete line extraction:**
```
[TCP] Complete line extracted: '1     960    00'
```

**Buffer diagnostics (when delimiter doesn't match):**
```
[TCP] No complete line yet. Buffer size: 15 chars. Content: '1     960    00'
[TCP] Expected delimiter: '\r\n'
```

This immediately shows you if data is being received but the delimiter configuration is wrong!

### 2. Upgraded Log Levels

**Before v3.4.4:**
- Weight readings: DEBUG level (invisible in production logs)
- Raw data: Not logged at all
- Connection errors: Silent

**After v3.4.4:**
- Weight readings: **INFO** level ✅
- Raw data: **INFO** level ✅
- TCP diagnostics: **WARNING** level ✅
- Connection errors: **WARNING** level ✅

All scale activity now visible in service logs!

### 3. New Standalone Test Tool

**`ScaleStreamer.TestTool.exe`** - Portable diagnostic utility included with installation!

**Features:**
- Standalone single-file executable (no installation required)
- Test any TCP scale connection
- View raw HEX and ASCII data in real-time
- Optional protocol parsing mode
- Command-line interface for easy scripting

**Usage Examples:**

```powershell
# Raw TCP test
ScaleStreamer.TestTool.exe 10.1.10.210

# Specify port
ScaleStreamer.TestTool.exe 10.1.10.210 --port 5001

# Test with protocol parsing
ScaleStreamer.TestTool.exe 10.1.10.210 --protocol fairbanks-6011.json

# Longer timeout
ScaleStreamer.TestTool.exe 10.1.10.210 --timeout 30
```

**Output example (raw mode):**
```
[20:15:33.123]   18 bytes | HEX: 31 20 20 20 20 20 39 36 30... | ASCII: 1     960    00\r\n
[20:15:33.623]   18 bytes | HEX: 31 20 20 20 20 20 39 36 31... | ASCII: 1     961    00\r\n
```

**Output example (protocol mode):**
```
[20:15:33.123] 📄 Raw: '1     960    00'
[20:15:33.124] ⚖️  Weight: 960 lbs | Status: Stable
[20:15:33.623] 📄 Raw: '1     961    00'
[20:15:33.624] ⚖️  Weight: 961 lbs | Status: Stable
```

---

## 🔧 Technical Changes

### Files Modified:

**TcpProtocolBase.cs**
- Added `_totalBytesReceived` and `_firstDataReceivedTime` tracking
- Enhanced `ReadLineAsync()` with comprehensive logging:
  - Log first data reception
  - Log every TCP read with HEX dump (first 32 bytes)
  - Log ASCII preview with escaped control characters
  - Log complete line extraction
  - Log buffer diagnostics when no delimiter found
  - Show expected vs actual delimiter

**ScaleService.cs**
- Changed `OnWeightReceived` logging from `LogDebug` → `LogInformation`
- Weight readings now appear in production logs

**ScaleConnectionManager.cs**
- Added subscription to `RawDataReceived` events
- Log all raw data lines at INFO level
- Changed `ErrorOccurred` logging to WARNING level

**New Project: ScaleStreamer.TestTool**
- Standalone console application for testing scale connections
- Single-file executable with .NET runtime included
- Supports raw TCP mode and protocol parsing mode
- Real-time HEX and ASCII visualization

---

## 📊 What You'll See in Logs Now

### Scenario 1: Scale NOT Sending Data
```
[INF] Adding scale: Scale1 with protocol: Fairbanks 6011
[INF] Scale Scale1 status changed to: Connected
[INF] Scale Scale1 connected and reading started
[WRN] [Scale1] Read timeout after 5 seconds
```
**Diagnosis:** Connection successful but no data received. Check if scale is powered on and configured to send continuous data.

### Scenario 2: Scale Sending Data (Correct Delimiter)
```
[INF] Adding scale: Scale1 with protocol: Fairbanks 6011
[WRN] [Scale1] [TCP] First data received: 18 bytes from 10.1.10.210:5001
[WRN] [Scale1] [TCP] Received 18 bytes | HEX: 31 20 20 20 ... | ASCII: 1     960    00\r\n
[WRN] [Scale1] [TCP] Complete line extracted: '1     960    00'
[INF] [Scale1] Raw data: 1     960    00
[INF] Weight received from Scale1: 960 lbs
```
**Diagnosis:** Everything working! Data received, parsed, and logged.

### Scenario 3: Scale Sending Data (WRONG Delimiter)
```
[INF] Adding scale: Scale1 with protocol: Fairbanks 6011
[WRN] [Scale1] [TCP] First data received: 15 bytes from 10.1.10.210:5001
[WRN] [Scale1] [TCP] Received 15 bytes | HEX: 31 20 20 ... | ASCII: 1     960    00
[WRN] [Scale1] [TCP] No complete line yet. Buffer size: 15 chars. Content: '1     960    00'
[WRN] [Scale1] [TCP] Expected delimiter: '\r\n'
```
**Diagnosis:** Data received but delimiter mismatch! Scale might be sending `\n` only but protocol expects `\r\n`. Update protocol configuration.

---

## 🛠️ Troubleshooting Guide

### Use the Test Tool First!

Before configuring the service, test your connection:

```powershell
cd "C:\Program Files\Scale Streamer"
.\ScaleStreamer.TestTool.exe 10.1.10.210 --port 5001
```

This will show you immediately if:
1. The scale is reachable on the network
2. Data is being sent
3. What format the data is in

### Check Service Logs

View logs at: `C:\ProgramData\ScaleStreamer\logs\service-YYYYMMDD.log`

**Look for:**
- `[TCP] First data received` - Data is coming through ✅
- `[TCP] Complete line extracted` - Delimiter is correct ✅
- `Raw data:` - Line extracted before parsing ✅
- `Weight received` - Parsing successful ✅

### Diagnose Delimiter Issues

**Check the HEX dump to identify line endings:**
- `0D 0A` = `\r\n` (Windows/network standard)
- `0A` = `\n` (Unix standard)
- `0D` = `\r` (Old Mac standard)

**Update protocol JSON if needed:**
```json
{
  "parsing": {
    "line_delimiter": "\r\n"   ← Change to match actual data
  }
}
```

### Test Network Connectivity

From Windows PowerShell:
```powershell
Test-NetConnection -ComputerName 10.1.10.210 -Port 5001
```

---

## 📦 Installation

### Upgrade from v3.4.3:

```powershell
msiexec /i ScaleStreamer-v3.4.4-TIMESTAMP.msi
```

The installer will:
- Upgrade all components
- Preserve your existing configuration
- Add the new test tool to `C:\Program Files\Scale Streamer\`

### Clean Install:

```powershell
msiexec /i ScaleStreamer-v3.4.4-TIMESTAMP.msi
```

### Silent Install:

```powershell
msiexec /i ScaleStreamer-v3.4.4-TIMESTAMP.msi /quiet
```

---

## 🔄 All Features from Previous Versions

**v3.4.4** - Enhanced TCP diagnostics + test tool (this release)
**v3.4.3** - CRITICAL: Fixed protocol loading and WinForms threading bugs
**v3.4.2** - Quick setup wizard (broken - do not use)
**v3.4.1** - CRITICAL: Database schema fix
**v3.4.0** - Built-in diagnostics, real-time logging
**v3.3.3** - Protocols path resolution fix

---

## 📄 New Documentation

**TCP-DIAGNOSTICS-ENHANCEMENTS.md** - Complete guide to the new diagnostic features and how to use them for troubleshooting.

---

## 💡 Use Cases

### Deploying to New Site
1. Run `ScaleStreamer.TestTool.exe <scale-ip>` first
2. Verify data format matches protocol
3. Configure service with confidence

### Scale Not Reporting Data
1. Check service logs for TCP diagnostics
2. Look for "No complete line yet" → delimiter issue
3. Look for "Read timeout" → scale not sending
4. Update protocol or check scale configuration

### Custom Protocol Development
1. Use test tool to capture raw data
2. Analyze HEX dump to understand format
3. Create protocol JSON
4. Test with `--protocol` option
5. Deploy to service

---

## 📞 Support

- **Test Tool:** `C:\Program Files\Scale Streamer\ScaleStreamer.TestTool.exe`
- **Diagnostics Script:** `C:\Program Files\Scale Streamer\collect-diagnostics.ps1`
- **Logs:** `C:\ProgramData\ScaleStreamer\logs\`
- **Database:** `C:\ProgramData\ScaleStreamer\scalestreamer.db`
- **Email:** admin@cloud-scale.us
- **Web:** https://cloud-scale.us/support

---

## 🎉 Summary

v3.4.4 makes troubleshooting scale connections **dramatically easier**:

✅ See raw TCP data in real-time
✅ Identify delimiter mismatches instantly
✅ Diagnose parsing failures with precision
✅ Portable test tool for field diagnostics
✅ All activity visible in production logs

**No more guessing why a scale isn't working!**

The enhanced diagnostics give you complete visibility into what's happening at the TCP level, making it easy to identify and fix connection issues.
