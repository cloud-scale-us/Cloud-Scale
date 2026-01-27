$env:PATH = "C:\Program Files\Git\bin;C:\Program Files\Git\cmd;" + $env:PATH
cd "C:\Users\Windfield\Cloud-Scale\win-scale"

$notes = @"
## Scale Streamer v5.1.2

Version bump release with all v5.1.1 features:

- **Full RS232/RS485 serial support** - Service correctly maps connection types, includes FlowControl, loads protocol templates
- **Real serial Test Connection** - Opens COM port to verify connectivity before saving
- **RS232 Diagnostics tab** - Live serial terminal with raw data, parsed readings, hex view, send capability, and export
- **Auto-start from saved config** - Service reconnects automatically on startup using saved settings
- **All versions unified to 5.1.2** across all assemblies and installer

### Installer
Self-contained MSI (72 MB) - no .NET runtime required on target system.
"@

& "C:\Program Files\GitHub CLI\gh.exe" release create v5.1.2 "C:\Users\Windfield\Cloud-Scale\win-scale\installer\bin\ScaleStreamer-v5.1.2-20260127-163206.msi" --title "Scale Streamer v5.1.2" --notes $notes
