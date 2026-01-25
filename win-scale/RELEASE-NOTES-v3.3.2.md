# Scale Streamer v3.3.2 - GUARANTEED Tab Visibility Fix

**Release Date:** January 25, 2026

## 🔧 Critical GUI Fixes

### Tab Visibility - Comprehensive Solution
This release implements the most comprehensive fix yet for the recurring tab visibility issue:

- **Layout Management:** Added proper `SuspendLayout()`/`ResumeLayout()` calls during control initialization
- **Explicit Display:** Added `Show()` and `BringToFront()` calls to TabControl
- **Initialization Safety:** Added null checking with exceptions in tab initialization
- **Debug Logging:** Added tab count logging for diagnostics
- **Control Selection:** Automatically selects first tab on load

### Technical Implementation
```csharp
// Proper layout suspension during initialization
this.SuspendLayout();
this.Controls.Add(headerPanel);
this.Controls.Add(_mainTabControl);
this.ResumeLayout(false);
this.PerformLayout();

// Explicit visibility enforcement
_mainTabControl.Show();
_mainTabControl.BringToFront();
```

## 📋 All 7 Tabs Included

1. **Connection** - Scale connection configuration
2. **Protocol** - Protocol selection and parameters
3. **Monitoring** - Live weight display (48pt digital readout) with RTSP URL
4. **Status** - Service and connection status
5. **Logs** - Real-time log viewer
6. **Settings** - Comprehensive configuration (email alerts, thresholds, auto-reconnect)
7. **Diagnostics** - Live TCP data, connection log, IPC messages, errors

## 🔍 Diagnostics Features

The Diagnostics tab provides 4 real-time monitoring views:
- **Live TCP Data:** Black terminal with lime green text showing raw scale data
- **Connection Log:** Connection events and state changes
- **IPC Messages:** Service-GUI communication messages
- **Errors:** Error tracking with timestamps

Perfect for debugging Fairbanks 6011 scale connections at 10.1.10.210:5001

## 📦 Installation

1. Download `ScaleStreamer-v3.3.2-20260125-170419.msi`
2. Run installer (requires admin rights)
3. Launch from Desktop shortcut
4. All 7 tabs should be immediately visible

## ⚙️ Configuration Preserved

Existing scale configurations are preserved during upgrade.

## 🐛 Known Issues

If tabs are still not visible after installation, please check:
- Windows display scaling settings
- .NET 8.0 runtime (included in self-contained build)
- Check logs in `C:\ProgramData\ScaleStreamer\logs`

## 📞 Support

For issues, contact admin@cloud-scale.us or visit https://cloud-scale.us/support
