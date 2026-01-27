# Add Git to PATH
$env:Path = "C:\Program Files\Git\bin;$env:Path"

$releaseNotes = @"
## v4.2.0 - GUI Optimization and Stability Release

### Major Improvements

**UI Stability**
- Fixed GUI freeze issue when receiving continuous scale data
- Limited UI updates to 4/second max (throttling)
- Non-blocking UI updates using BeginInvoke
- Batch UI updates with SuspendLayout/ResumeLayout
- ListView optimization with BeginUpdate/EndUpdate
- Reduced log buffer from 500 to 200 lines

**Status Tab Rewrite**
- Real Windows service status monitoring via ServiceController
- Shows actual IPC connection status
- Shows scale connection with data flow indicator
- Working Start/Stop/Restart service buttons with UAC elevation
- Open Logs button for quick access to service logs

**Connection Improvements**
- Removed hardcoded default IP address (requires explicit configuration)
- Settings changes now trigger actual scale reconnection
- Added debouncing to prevent reconnection floods (500ms delay)

**IPC Improvements**
- Reduced verbose logging to prevent log floods
- Better error handling in client connections

### Installation
- Self-contained installer (58 MB) - no .NET runtime required
- Supports Windows 10/11 and Windows Server 2019+

### Upgrade Notes
This release supersedes v4.1.2. Simply run the new installer to upgrade.
"@

$msiFile = Get-ChildItem "C:\Users\Windfield\Cloud-Scale\win-scale\installer\bin\ScaleStreamer-v4.2.0-*.msi" | Sort-Object LastWriteTime -Descending | Select-Object -First 1

Write-Host "Creating GitHub release v4.2.0..." -ForegroundColor Cyan
Write-Host "MSI file: $($msiFile.FullName)" -ForegroundColor Yellow

cd "C:\Users\Windfield\Cloud-Scale"

& "C:\Program Files\GitHub CLI\gh.exe" release create v4.2.0 --title "Scale Streamer v4.2.0 - GUI Optimization and Stability" --notes $releaseNotes $msiFile.FullName

if ($LASTEXITCODE -eq 0) {
    Write-Host "Release created successfully!" -ForegroundColor Green
} else {
    Write-Host "Release creation failed with exit code $LASTEXITCODE" -ForegroundColor Red
}
