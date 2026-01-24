# Scale Streamer Troubleshooting Guide

## Application Won't Start After Installation

### Check Application Log
The application creates a detailed log file at:
```
%LOCALAPPDATA%\ScaleStreamer\app.log
```

**Quick access:**
```powershell
notepad $env:LOCALAPPDATA\ScaleStreamer\app.log
```

Or use the log viewer script:
```powershell
.\scripts\view-logs.ps1
```

### Common Issues

#### 1. No System Tray Icon Appears

**Symptoms:** Application installs but no tray icon shows up.

**Causes:**
- Application crashed during startup
- Windows Explorer needs restart
- Icon file missing from Resources folder

**Solutions:**

1. **Check if application is running:**
   ```powershell
   Get-Process ScaleStreamer
   ```

2. **Restart Windows Explorer:**
   - Press `Ctrl+Shift+Esc` to open Task Manager
   - Find "Windows Explorer" in Processes
   - Right-click → Restart

3. **Check the log file:**
   ```powershell
   notepad $env:LOCALAPPDATA\ScaleStreamer\app.log
   ```
   Look for errors like "Icon file not found" or exceptions.

4. **Verify installation:**
   ```powershell
   ls "C:\Program Files\Scale RTSP Streamer\Resources\icon.ico"
   ```

#### 2. Application Crashes on Startup

**Check the log for:**
- `FATAL ERROR` messages
- Missing dependencies (FFmpeg, MediaMTX)
- Configuration file errors

**Common fixes:**
1. Verify all dependencies are in the `deps` folder
2. Delete corrupted config: `%APPDATA%\ScaleStreamer\config.json`
3. Reinstall with logging enabled (see below)

#### 3. Missing Dependencies

**Verify FFmpeg and MediaMTX exist:**
```powershell
ls "C:\Program Files\Scale RTSP Streamer\deps\ffmpeg\ffmpeg.exe"
ls "C:\Program Files\Scale RTSP Streamer\deps\mediamtx\mediamtx.exe"
```

If missing, reinstall or manually download dependencies.

---

## Installation Issues

### Install with Verbose Logging

Use the installation script with logging:
```powershell
.\scripts\install-with-logging.ps1
```

This creates a detailed installation log at:
```
%TEMP%\ScaleStreamer-Install\install-YYYYMMDD-HHMMSS.log
```

### Common Installation Exit Codes

| Code | Meaning | Solution |
|------|---------|----------|
| 0 | Success | - |
| 1602 | User cancelled | Reinstall and don't cancel |
| 1603 | Fatal error | Check install log for details |
| 1618 | Another install running | Wait and retry |
| 1625 | Forbidden by policy | Run as Administrator |

### Manual Installation

If MSI installer fails:
1. Extract the `publish/Release` folder
2. Copy to `C:\Program Files\Scale RTSP Streamer`
3. Run `ScaleStreamer.exe` manually
4. Add to Windows startup registry if needed

---

## Stream Not Working

### Check Stream Manager Status

The application log will show:
```
Starting FFmpeg...
Starting MediaMTX...
Stream started successfully
```

### Test RTSP Stream

**Using VLC:**
```
Media → Open Network Stream → rtsp://localhost:8554/scale
```

**Using FFplay:**
```cmd
"C:\Program Files\Scale RTSP Streamer\deps\ffmpeg\ffplay.exe" rtsp://localhost:8554/scale
```

### Check Ports

Ensure ports aren't blocked:
```powershell
netstat -ano | findstr "8554"  # RTSP port
netstat -ano | findstr "8888"  # HLS port
```

---

## Scale Connection Issues

### Test Connection

1. Right-click tray icon → **Configure**
2. Go to **Connection** tab
3. Click **Test Connection**

### TCP/IP Connection

**Verify network connectivity:**
```powershell
Test-NetConnection -ComputerName 192.168.1.100 -Port 5001
```

**Test with telnet:**
```cmd
telnet 192.168.1.100 5001
```

### Serial (RS232) Connection

**Check COM ports:**
```powershell
Get-WmiObject Win32_SerialPort | Select Name, DeviceID
```

**Verify baud rate** matches your scale (default: 9600)

---

## Log Files Reference

### Application Runtime Log
- **Location:** `%LOCALAPPDATA%\ScaleStreamer\app.log`
- **Contains:** Startup, errors, stream status, scale connection
- **View:** `.\scripts\view-logs.ps1` → Option 1

### Installation Log
- **Location:** `%TEMP%\ScaleStreamer-Install\install-*.log`
- **Contains:** MSI installation details, file operations, registry changes
- **View:** `.\scripts\view-logs.ps1` → Option 2

### Windows Event Log
- **Location:** Event Viewer → Windows Logs → Application
- **Filter by:** Source = MsiInstaller
- **View:** `.\scripts\view-logs.ps1` → Option 3

---

## Reset Application

### Complete Reset

1. **Uninstall:**
   ```powershell
   Start-Process "appwiz.cpl"  # Find "Scale RTSP Streamer" and uninstall
   ```

2. **Delete user data:**
   ```powershell
   Remove-Item -Recurse "$env:LOCALAPPDATA\ScaleStreamer"
   Remove-Item -Recurse "$env:APPDATA\ScaleStreamer"
   ```

3. **Remove registry auto-start:**
   ```powershell
   Remove-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "ScaleStreamer" -ErrorAction SilentlyContinue
   ```

4. **Reinstall**

---

## Getting Help

### Before Reporting Issues

1. **Check logs:**
   ```powershell
   .\scripts\view-logs.ps1
   ```

2. **Gather system info:**
   ```powershell
   Get-ComputerInfo | Select WindowsProductName, OsArchitecture, WindowsVersion
   dotnet --version  # If running from source
   ```

3. **Test with defaults:**
   - Reset configuration to defaults
   - Test without custom settings

### Report Issues

Include in your report:
- Application log (`%LOCALAPPDATA%\ScaleStreamer\app.log`)
- Installation log (if install issue)
- Steps to reproduce
- Windows version
- Scale model and connection type

**Submit to:** https://github.com/CNesbitt2025/Cloud-Scale/issues

---

## Advanced: Running as Windows Service

For persistent background operation, convert to a Windows service:

```powershell
# Install as service (requires admin)
sc.exe create ScaleStreamer binPath= "C:\Program Files\Scale RTSP Streamer\ScaleStreamer.exe" start= auto

# Start service
sc.exe start ScaleStreamer

# Stop service
sc.exe stop ScaleStreamer

# Remove service
sc.exe delete ScaleStreamer
```

**Note:** The current version is designed as a user application. Service mode may require modifications to support background operation without user session.
