# TCP Diagnostics Enhancements - v3.4.4

## Problem

The Scale Streamer service was not showing weight readings in logs or GUI, even when connected to a scale. This made it impossible to diagnose whether:
- The scale was actually sending data
- Data was being received but not parsed correctly
- The delimiter configuration was wrong
- The protocol configuration was incorrect

## Root Causes

1. **No raw TCP data logging** - The code never logged what bytes were actually received from the TCP stream
2. **Silent null returns** - When ReadLineAsync didn't find a complete line with the expected delimiter, it returned null and the continuous reading loop silently skipped it
3. **Debug-level logging only** - Weight readings were logged at DEBUG level, which doesn't show in production logs
4. **No diagnostic visibility** - No way to see if data was accumulating in the buffer without the right delimiter

## Enhancements Made

### 1. Enhanced TCP Data Logging (TcpProtocolBase.cs)

Added comprehensive logging to show exactly what's happening with the TCP stream:

**First Data Reception:**
```
[TCP] First data received: 18 bytes from 10.1.10.210:5001
```

**Raw Byte Logging:**
```
[TCP] Received 18 bytes | HEX: 31 20 20 20 20 20 39 36 30 20 20 20 20 30 30 0D 0A | ASCII: 1     960    00\r\n
```

**Complete Line Extraction:**
```
[TCP] Complete line extracted: '1     960    00'
```

**Buffer Diagnostics (when no complete line found):**
```
[TCP] No complete line yet. Buffer size: 15 chars. Content: '1     960    00'
[TCP] Expected delimiter: '\r\n'
```

This allows you to:
- See if data is being received at all
- See exactly what bytes are coming through (hex and ASCII)
- See if the delimiter matches what's expected
- See what's accumulating in the buffer

### 2. Weight Reading Logging Upgraded (ScaleService.cs)

Changed from DEBUG to INFO level:

**Before:**
```csharp
_logger.LogDebug("Weight received from {ScaleId}: {Weight} {Unit}", ...);
// Would NOT show in production logs
```

**After:**
```csharp
_logger.LogInformation("Weight received from {ScaleId}: {Weight} {Unit}", ...);
// WILL show in production logs
```

### 3. Raw Data Event Logging (ScaleConnectionManager.cs)

Added subscription to RawDataReceived events:

```csharp
protocolAdapter.RawDataReceived += (sender, rawData) =>
{
    _logger?.LogInformation("[{ScaleId}] Raw data: {RawData}", scaleId, rawData);
};
```

This logs every line of raw data received from the scale before parsing.

### 4. Error Logging Visibility

Changed ErrorOccurred logging from silent to WARNING level:

```csharp
protocolAdapter.ErrorOccurred += (sender, error) =>
{
    _logger?.LogWarning("[{ScaleId}] {Error}", scaleId, error.Message);
    ErrorOccurred?.Invoke(this, (scaleId, error.Message));
};
```

## What You'll See Now

### If Scale is NOT Sending Data:

```
[INF] Adding scale: Scale1 with protocol: Fairbanks 6011
[INF] Scale Scale1 status changed to: Connected
[INF] Scale Scale1 connected and reading started
[WRN] [Scale1] Read timeout after 5 seconds
```

### If Scale IS Sending Data (Correct Delimiter):

```
[INF] Adding scale: Scale1 with protocol: Fairbanks 6011
[WRN] [Scale1] [TCP] First data received: 18 bytes from 10.1.10.210:5001
[WRN] [Scale1] [TCP] Received 18 bytes | HEX: 31 20 20 20 20 20 39 36 30 20 20 20 20 30 30 0D 0A | ASCII: 1     960    00\r\n
[WRN] [Scale1] [TCP] Complete line extracted: '1     960    00'
[INF] [Scale1] Raw data: 1     960    00
[INF] Weight received from Scale1: 960 lbs
```

### If Scale IS Sending Data (WRONG Delimiter):

```
[INF] Adding scale: Scale1 with protocol: Fairbanks 6011
[WRN] [Scale1] [TCP] First data received: 15 bytes from 10.1.10.210:5001
[WRN] [Scale1] [TCP] Received 15 bytes | HEX: 31 20 20 20 20 20 39 36 30 20 20 20 20 30 30 | ASCII: 1     960    00
[WRN] [Scale1] [TCP] No complete line yet. Buffer size: 15 chars. Content: '1     960    00'
[WRN] [Scale1] [TCP] Expected delimiter: '\r\n'
```

This immediately shows you that data is being received but the delimiter doesn't match.

## Using the Diagnostics

1. **Check service logs** at `C:\ProgramData\ScaleStreamer\logs\service-YYYYMMDD.log`

2. **Look for TCP messages** - If you see `[TCP] First data received`, the scale is sending data

3. **Check the HEX dump** - See exactly what bytes are coming through:
   - `0D 0A` = `\r\n` (Windows/network standard)
   - `0A` = `\n` (Unix standard)
   - `0D` = `\r` (Old Mac standard)

4. **Check the buffer messages** - If you see "No complete line yet", your delimiter is wrong

5. **Check for "Complete line extracted"** - If you see this but no "Weight received", the parsing/regex is failing

6. **Check for "Raw data:"** - Shows the extracted line before parsing

7. **Check for "Weight received"** - Final confirmation that everything is working

## Network Connectivity Note

Remember that the scale must be network-reachable from the Windows host where the service runs. If you're testing from WSL and the scale is on a different network segment, you won't be able to connect.

To test network connectivity from Windows PowerShell:
```powershell
Test-NetConnection -ComputerName 10.1.10.210 -Port 5001
```

## Next Steps

With these enhanced diagnostics, you can now:
1. Confirm if data is being received
2. See exactly what format the data is in
3. Identify delimiter mismatches
4. Debug parsing/regex issues
5. Monitor data flow in real-time

All of this information will now appear in the service logs at INFO/WARN level, making troubleshooting much easier.
