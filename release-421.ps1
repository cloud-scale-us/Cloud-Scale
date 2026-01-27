$env:Path = "C:\Program Files\Git\bin;$env:Path"
cd 'C:\Users\Windfield\Cloud-Scale\win-scale'

$notes = @"
## Scale Streamer v4.2.1 - Version String Fix

**Release Date:** January 26, 2026

### Fixed
- Fixed hardcoded version strings showing 4.1.2 in GUI instead of correct version
- APP_VERSION constant in MainForm.cs now correctly shows 4.2.1
- Version logging in Program.cs updated to 4.2.1

### Unchanged from 4.2.0
- GUI Performance: UI throttling prevents freeze during rapid weight updates
- Self-contained installer includes all .NET 8 runtime dependencies

### Installation
Download ScaleStreamer-v4.2.1-20260126-193232.msi (~58MB)

Silent install: msiexec /i ScaleStreamer-v4.2.1-20260126-193232.msi /quiet
"@

& 'C:\Program Files\GitHub CLI\gh.exe' release create v4.2.1 `
    'installer\bin\ScaleStreamer-v4.2.1-20260126-193232.msi' `
    --title 'Scale Streamer v4.2.1 - Version String Fix' `
    --notes $notes
