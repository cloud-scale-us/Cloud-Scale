# Scale Streamer v3.4.1 - CRITICAL Database Schema Fix

**Release Date:** January 25, 2026

## 🚨 CRITICAL BUG FIX

### Database Schema Not Being Created

**The Problem (v3.4.0):**
```
[ERR] Failed to load protocol from: ...fairbanks-6011.json
SQLite Error 1: 'no such table: protocol_templates'.
```

v3.4.0 had a critical bug where the database schema file (`schema.sql`) was not being deployed with the published application. This caused:
- ❌ Database tables never created
- ❌ Protocol templates failed to load
- ❌ Scales could not be configured
- ❌ Application completely non-functional

### The Fix (v3.4.1):

**Embedded the entire database schema directly in C# code** instead of relying on external file deployment.

**Before:**
```csharp
// Read schema from file (file not deployed!)
var schemaPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Database", "schema.sql");
if (File.Exists(schemaPath))
{
    var schema = await File.ReadAllTextAsync(schemaPath);
    // ...
}
```

**After:**
```csharp
// Embedded schema - always works
private static string GetDatabaseSchema()
{
    return @"
    CREATE TABLE IF NOT EXISTS config (...);
    CREATE TABLE IF NOT EXISTS scales (...);
    CREATE TABLE IF NOT EXISTS protocol_templates (...);
    // ... full schema embedded
    ";
}
```

## ✅ What Now Works

After installing v3.4.1, you should see in the service log:

```
[INF] Protocols path resolved to: C:\Program Files\Scale Streamer\protocols
[INF] Found 3 protocol template files in C:\Program Files\Scale Streamer\protocols
[INF] Loaded protocol template: Fairbanks 6011 v1.1         ← SUCCESS!
[INF] Loaded protocol template: Generic ASCII v1.0          ← SUCCESS!
[INF] Loaded protocol template: Modbus TCP v1.0              ← SUCCESS!
[INF] IPC Server started on pipe: ScaleStreamerPipe
[INF] Scale Service running and ready for connections.
```

**No more database errors!** ✅

## 📋 Database Tables Created

The embedded schema creates all necessary tables:
- ✅ `config` - Application configuration
- ✅ `scales` - Scale configurations
- ✅ `protocol_templates` - Protocol definitions (Fairbanks, Generic ASCII, Modbus)
- ✅ `weight_readings` - Weight data storage
- ✅ `transactions` - Transaction records
- ✅ `events` - Application events and errors
- ✅ `metrics` - Performance metrics
- ✅ `alert_rules` - Alert configuration
- ✅ `alert_history` - Alert history

Plus indexes, triggers for auto-purging old data, and default configuration values.

## 🚀 Upgrade from v3.4.0

**IMPORTANT:** If you installed v3.4.0, you need to delete the broken database:

### Option 1: Automatic (Recommended)
Just install v3.4.1 - it will auto-upgrade and recreate the database correctly on next service start.

### Option 2: Manual (if you want clean slate)
```powershell
# Stop service
Stop-Service ScaleStreamerService

# Delete broken database
Remove-Item "C:\ProgramData\ScaleStreamer\scalestreamer.db" -Force

# Install v3.4.1
msiexec /i ScaleStreamer-v3.4.1-20260125-182219.msi

# Service will auto-create proper database
```

## 🔍 How to Verify Fix

After installing v3.4.1:

1. **Check Service Log (should have NO errors):**
   ```powershell
   Get-Content "C:\ProgramData\ScaleStreamer\logs\service-$(Get-Date -Format 'yyyyMMdd').log" -Tail 20
   ```

2. **Run Diagnostics:**
   ```powershell
   cd "C:\Program Files\Scale Streamer"
   .\collect-diagnostics.ps1
   ```
   Look for: "Database found", "Loaded protocol template: Fairbanks 6011"

3. **Open Logs Tab in GUI:**
   - Should show real service logs
   - No database errors

4. **Configure Scale:**
   - Connection tab → Add scale
   - Host: `10.1.10.210`, Port: `5001`
   - Protocol: `Fairbanks 6011` ← Should be available now!

## 📦 All Features from v3.4.0

Everything from v3.4.0 is included and now **actually works**:

- ✅ Diagnostic script in install folder
- ✅ Real-time logging tab (reads actual service logs)
- ✅ 7 fully functional tabs
- ✅ Protocol templates (Fairbanks 6011, Generic ASCII, Modbus TCP)
- ✅ Complete documentation

## 🐛 Bug Fixes

- ✅ **CRITICAL:** Database schema now embedded, tables always created
- ✅ **CRITICAL:** Protocol templates can now be saved to database
- ✅ **CRITICAL:** Scales can now be configured
- ✅ **CRITICAL:** Application is now functional

## 🔄 Version History

- **v3.4.1** - **CRITICAL** database schema fix
- **v3.4.0** - Built-in diagnostics, real-time logging (broken database)
- **v3.3.3** - Protocols path fix
- **v3.3.2** - Tab visibility improvements
- **v3.3.1** - Diagnostics tab
- **v3.2.0** - Unified launcher

## 📞 Support

- **Diagnostics:** `C:\Program Files\Scale Streamer\collect-diagnostics.ps1`
- **Logs:** `C:\ProgramData\ScaleStreamer\logs\`
- **Database:** `C:\ProgramData\ScaleStreamer\scalestreamer.db`
- **Email:** admin@cloud-scale.us
- **Web:** https://cloud-scale.us/support

## 💡 Technical Details

The database schema is now a 200+ line embedded string in `DatabaseService.cs:GetDatabaseSchema()`. This ensures:
- Schema is always available (no file deployment issues)
- Atomic initialization (all tables created in one transaction)
- Consistent across all installations
- No dependency on file system paths

The embedded schema is identical to the original `schema.sql` file, but compiled into the DLL so it can never be "missing".

## ⚠️ Apology

Sorry for the v3.4.0 release being broken! The database schema file was missing from the build output, making the app unusable. v3.4.1 fixes this completely by embedding the schema directly in code.

**v3.4.1 is the first truly functional release** of the v3.4.x series. 🎉
