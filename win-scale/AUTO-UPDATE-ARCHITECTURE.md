# Auto-Update System Architecture

## How It Works

The Scale Streamer auto-update system provides automatic notifications when new versions are available, allowing users to download and install updates with minimal friction.

### Architecture Overview

```
┌─────────────────────┐
│   Configuration GUI │
│   (User's PC)       │
└──────────┬──────────┘
           │
           │ 1. Check for updates (HTTP GET)
           ▼
┌─────────────────────────────────────────┐
│   GitHub Releases API                   │
│   https://api.github.com/repos/         │
│   cloud-scale-us/Cloud-Scale/releases   │
└──────────┬──────────────────────────────┘
           │
           │ 2. Returns latest release info (JSON)
           │    - version (tag_name: "v2.5.0")
           │    - download URL
           │    - release notes
           ▼
┌─────────────────────┐
│   Version Compare   │
│   (Client-side)     │
└──────────┬──────────┘
           │
           │ 3. If newer version available
           ▼
┌─────────────────────┐
│  Notification UI    │
│  (Update Banner)    │
└─────────────────────┘
```

## Implementation Details

### 1. Version Checking Service

**Location:** `ScaleStreamer.Common/Services/UpdateChecker.cs`

**Features:**
- Checks GitHub Releases API for latest version
- Compares semantic versions (2.5.0 vs 2.0.1)
- Retrieves release notes and download URL
- Configurable check interval (default: once per day)
- Caches last check to avoid API rate limits

**API Endpoint:**
```
GET https://api.github.com/repos/cloud-scale-us/Cloud-Scale/releases/latest

Response:
{
  "tag_name": "v2.5.0",
  "name": "Scale Streamer v2.5.0 - Auto-Update Support",
  "body": "Release notes...",
  "assets": [
    {
      "name": "ScaleStreamer-v2.5.0-YYYYMMDD-HHMMSS.msi",
      "browser_download_url": "https://github.com/..."
    }
  ]
}
```

### 2. GUI Update Notification

**Location:** `ScaleStreamer.Config/MainForm.cs`

**UI Elements:**
- **Update banner** at top of window (when update available)
- **Version label** in status bar shows "v2.5.0 (Update available)"
- **Update dialog** with release notes and download button

**User Workflow:**
1. User opens Configuration GUI
2. GUI checks for updates on startup (async, non-blocking)
3. If newer version found, show notification banner
4. User clicks "Download Update" button
5. Browser opens to GitHub release page
6. User downloads and runs MSI installer
7. Installer upgrades in-place, preserving configuration

### 3. Configuration Storage

**Location:** SQLite database or registry

**Settings:**
- `LastUpdateCheck`: DateTime of last check
- `UpdateCheckEnabled`: bool (allow user to disable)
- `UpdateCheckInterval`: TimeSpan (default: 24 hours)
- `SkippedVersion`: string (if user clicks "Skip this version")

### 4. Installation/Upgrade Process

**MSI Installer Behavior:**
- Detects existing installation via Product Code
- Stops service gracefully
- Replaces binaries (service + GUI + common DLLs)
- Preserves configuration in `C:\ProgramData\ScaleStreamer\`
- Restarts service automatically
- Shows "What's New" on first launch after upgrade

## Security & Privacy

### No Data Collection
- Update check only sends HTTP GET to public GitHub API
- No telemetry or analytics
- No user identification
- No usage tracking

### No Automatic Downloads
- System only **notifies** user of updates
- User must manually download and install
- No background downloads
- No forced updates

### API Rate Limits
- GitHub API: 60 requests/hour for unauthenticated
- Client caches responses for 24 hours
- Last check time stored locally
- Respects `Cache-Control` headers

## Version Numbering

**Semantic Versioning:** `MAJOR.MINOR.PATCH`

- **MAJOR** (2.x.x): Breaking changes, major features
- **MINOR** (x.5.x): New features, backward-compatible
- **PATCH** (x.x.1): Bug fixes only

**Examples:**
- `2.0.1` → `2.0.2`: Bug fix
- `2.0.1` → `2.1.0`: New feature (auto-update)
- `2.0.1` → `3.0.0`: Breaking changes

## User Experience

### First Launch After Install
```
┌─────────────────────────────────────────────┐
│ Scale Streamer Configuration         [_][□][X] │
├─────────────────────────────────────────────┤
│  Connection  │ Protocol │ Monitoring │ ... │
├─────────────────────────────────────────────┤
│                                             │
│  Normal interface...                        │
│                                             │
└─────────────────────────────────────────────┘
Status: Service: Connected     v2.5.0
```

### When Update Available
```
┌─────────────────────────────────────────────┐
│ Scale Streamer Configuration         [_][□][X] │
├─────────────────────────────────────────────┤
│ ⚠ New version available: v2.6.0             │
│   [View Release Notes] [Download] [Skip]   │
├─────────────────────────────────────────────┤
│  Connection  │ Protocol │ Monitoring │ ... │
├─────────────────────────────────────────────┤
│                                             │
│  Normal interface...                        │
│                                             │
└─────────────────────────────────────────────┘
Status: Service: Connected     v2.5.0 (Update available)
```

### Release Notes Dialog
```
┌──────────────────────────────────────────┐
│  Update Available - v2.6.0          [X]  │
├──────────────────────────────────────────┤
│                                          │
│  What's New:                             │
│  ✨ New REST API for weight data         │
│  🔧 Fixed serial port auto-reconnect     │
│  📊 Added data export feature            │
│                                          │
│  Release Date: 2026-02-15                │
│  Size: ~55 MB                            │
│                                          │
├──────────────────────────────────────────┤
│     [Download & Install]  [Skip]  [Cancel] │
└──────────────────────────────────────────┘
```

## Implementation Checklist

- [ ] Create `UpdateChecker.cs` service
- [ ] Add update notification banner to `MainForm.cs`
- [ ] Create update dialog with release notes
- [ ] Add settings for update checks
- [ ] Implement version comparison logic
- [ ] Add "Check for Updates" menu item
- [ ] Test with mock GitHub API responses
- [ ] Update installer to preserve config during upgrade
- [ ] Add "What's New" dialog on first launch after upgrade
- [ ] Document update process in README

## Future Enhancements

### Phase 2 (v2.6+)
- In-app MSI download (download to temp folder)
- One-click upgrade (launch MSI from GUI)
- Background download with progress bar
- Delta updates (only changed files)

### Phase 3 (v3.0+)
- Automatic silent updates (opt-in)
- Rollback capability
- Beta/Stable channel selection
- Update scheduling (install during low-usage hours)

## Testing Strategy

### Manual Testing
1. Set current version to 2.0.1
2. Publish v2.5.0 release to GitHub
3. Launch GUI, verify update notification appears
4. Click "Download", verify browser opens to release page
5. Install update, verify config preserved

### Automated Testing
- Unit tests for version comparison
- Mock GitHub API responses
- Test rate limiting behavior
- Test offline/network error handling

## Fallback & Error Handling

**Network Errors:**
- Silently fail if GitHub API unreachable
- Show update check failed in settings
- Don't block GUI startup

**Invalid Responses:**
- Log error details
- Skip update notification
- Continue normal operation

**Rate Limit Exceeded:**
- Respect GitHub's rate limit headers
- Extend check interval to 48 hours
- Show message in settings

---

**Result:** Users get notified of updates without any invasive behavior, data collection, or forced installations. Simple, transparent, and user-friendly.
