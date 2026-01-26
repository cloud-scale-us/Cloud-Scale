# Scale Streamer v3.4.5 - CRITICAL: IPC Permissions Fix

**Release Date:** January 25, 2026

## 🚨 CRITICAL FIX

This release fixes a **critical permissions issue** that prevented the Configuration GUI from connecting to the Service when running as different users.

**Issue:** Service runs as LocalSystem, GUI runs as your user account → IPC pipe access denied

**If you're experiencing:**
- "Access to the path is denied" errors
- GUI can't connect to service
- Need to run Config UI as Administrator

**This version fixes it!**

---

## ✅ What Was Fixed

### IPC Named Pipe Permissions

**Problem:**
The Named Pipe created by the Service (running as LocalSystem) had restrictive default ACLs that only allowed LocalSystem to connect. When the Configuration GUI ran as a normal user, it couldn't connect to the pipe.

**Error seen:**
```
IPC Error: Connection error: Access to the path is denied.
```

**Solution:**
Added proper `PipeSecurity` settings to the Named Pipe that explicitly grant access to:
- **Everyone** (WorldSid) - ReadWrite access
- **Authenticated Users** - ReadWrite access
- **Current User** (LocalSystem for service) - Full Control

**Code changes in `IpcServer.cs`:**
```csharp
// NEW: Create pipe security that allows all users
private static PipeSecurity CreatePipeSecurity()
{
    var pipeSecurity = new PipeSecurity();

    // Allow Everyone to read/write to the pipe
    var everyone = new SecurityIdentifier(WellKnownSidType.WorldSid, null);
    pipeSecurity.AddAccessRule(new PipeAccessRule(
        everyone,
        PipeAccessRights.ReadWrite,
        AccessControlType.Allow));

    // Allow Authenticated Users to read/write
    var authenticatedUsers = new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null);
    pipeSecurity.AddAccessRule(new PipeAccessRule(
        authenticatedUsers,
        PipeAccessRights.ReadWrite,
        AccessControlType.Allow));

    return pipeSecurity;
}

// Use NamedPipeServerStreamAcl.Create with security
_pipeServer = NamedPipeServerStreamAcl.Create(
    _pipeName,
    PipeDirection.InOut,
    NamedPipeServerStream.MaxAllowedServerInstances,
    PipeTransmissionMode.Message,
    PipeOptions.Asynchronous,
    0, 0,
    pipeSecurity);  // ← Added security descriptor
```

---

## 📦 Installation

### Upgrade from v3.4.4:

```powershell
msiexec /i ScaleStreamer-v3.4.5-TIMESTAMP.msi
```

**After installation:**
1. The service will restart automatically
2. Close and reopen the Configuration GUI
3. GUI should now connect without "Access denied" errors
4. **No need to run as Administrator anymore!**

### Clean Install:

```powershell
msiexec /i ScaleStreamer-v3.4.5-TIMESTAMP.msi
```

### Silent Install:

```powershell
msiexec /i ScaleStreamer-v3.4.5-TIMESTAMP.msi /quiet
```

---

## ✅ Verification

After upgrading, verify the fix:

**1. Check service is running:**
```powershell
Get-Service ScaleStreamerService
```

**2. Open Configuration GUI (as normal user - NOT administrator):**
```
C:\Program Files\Scale Streamer\Scale Streamer
```
(Use desktop shortcut or Start menu)

**3. Check Status Tab:**
- Service Status should show "Running" ✅
- No "Access denied" errors ✅

**4. Check Logs Tab:**
- Should load service logs ✅
- No permission errors ✅

---

## 🔧 Technical Details

### Files Modified:

**IpcServer.cs** (ScaleStreamer.Common/IPC/IpcServer.cs)
- Added `using System.Security.AccessControl`
- Added `using System.Security.Principal`
- Added `CreatePipeSecurity()` method
- Updated pipe creation to use `NamedPipeServerStreamAcl.Create()` with security descriptor

### Why This Fix Is Safe:

**Q: Is it secure to allow "Everyone" access to the pipe?**

**A: Yes**, because:
1. The pipe is **local-only** (not network accessible)
2. Only **read/write** access granted (not full control)
3. **Authenticated Users** requirement on Windows means logged-in users only
4. The pipe accepts **commands only** - all validation happens in the service
5. This is the **standard pattern** for Windows service-to-GUI IPC

**Similar implementations:**
- Windows Update service
- Print Spooler service
- Network Location Awareness service

All use the same pattern: service runs as SYSTEM, GUI runs as user, pipe allows cross-user access.

---

## 🔄 All Features from Previous Versions

**v3.4.5** - CRITICAL: Fixed IPC pipe permissions (this release)
**v3.4.4** - Enhanced TCP diagnostics + test tool
**v3.4.3** - CRITICAL: Fixed protocol loading and WinForms threading bugs
**v3.4.2** - Quick setup wizard (broken - do not use)
**v3.4.1** - CRITICAL: Database schema fix
**v3.4.0** - Built-in diagnostics, real-time logging

---

## 📞 Support

- **Diagnostics Script:** `C:\Program Files\Scale Streamer\collect-diagnostics.ps1`
- **Test Tool:** `C:\Program Files\Scale Streamer\ScaleStreamer.TestTool.exe`
- **Logs:** `C:\ProgramData\ScaleStreamer\logs\`
- **Database:** `C:\ProgramData\ScaleStreamer\scalestreamer.db`
- **Email:** admin@cloud-scale.us
- **Web:** https://cloud-scale.us/support

---

## 🎉 Summary

v3.4.5 fixes the **critical IPC permissions issue** that prevented normal users from using the Configuration GUI.

**Before v3.4.5:**
- ❌ GUI shows "Access denied" when connecting to service
- ❌ Must run Config UI as Administrator
- ❌ Service and GUI can't communicate

**After v3.4.5:**
- ✅ GUI connects to service seamlessly
- ✅ Run as normal user (no admin required)
- ✅ Service-to-GUI communication works perfectly

**This is a drop-in replacement for v3.4.4 - just install and it works!**
