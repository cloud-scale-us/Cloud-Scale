$env:Path = 'C:\Program Files\Git\bin;' + $env:Path
cd 'C:\Users\Windfield\Cloud-Scale'
$gh = 'C:\Program Files\GitHub CLI\gh.exe'
$msi = Get-ChildItem 'win-scale\installer\bin\*.msi' | Sort-Object LastWriteTime -Descending | Select-Object -First 1
Write-Host "MSI: $($msi.FullName)"

$notes = @"
v5.2.0: Fix RS232 serial ingestion bugs, version bump, auto-restore on power loss

Changes:
- Fixed RS232 serial port data ingestion (3 bugs)
- Auto-reconnect to last data stream on service restart
- Auto-start ONVIF service on startup
- System restores previous state after power loss
- Version bumped to 5.2.0 across all components
"@

& $gh release create v5.2.0 $msi.FullName --title 'Scale Streamer v5.2.0' --notes $notes
