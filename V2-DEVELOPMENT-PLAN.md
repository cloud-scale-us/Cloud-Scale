# Scale RTSP Streamer v2.0.0 - Development Plan

**Cloud-Scale Industrial IoT Platform**

---

## Current Status (v1.1.0)

### What Works
✅ Basic Fairbanks 6011 protocol parsing
✅ TCP/IP connectivity
✅ System tray application
✅ Configuration dialog (basic)
✅ FFmpeg video generation
✅ MediaMTX RTSP streaming
✅ Runtime logging
✅ Firewall rules
✅ MSI installer

### Known Issues
❌ Application doesn't launch reliably after installation
❌ System tray icon may not appear
❌ No Windows Service (runs as user application)
❌ Limited to single scale manufacturer
❌ No RS232 support (code exists but untested)
❌ No monitoring dashboard
❌ No historical data
❌ Basic error handling only

---

## V2.0.0 Objectives

Transform the application from a **proof-of-concept** into an **enterprise-grade industrial monitoring system**.

### Key Goals

1. **Reliability**: 99.9% uptime via Windows Service architecture
2. **Compatibility**: Support 9 protocols across 4+ manufacturers
3. **Usability**: Professional GUI with real-time monitoring
4. **Diagnostics**: Comprehensive logging and troubleshooting tools
5. **Scalability**: Multi-scale support, database storage
6. **Professionalism**: Polished UI with proper branding

---

## Research Findings

### Supported Scale Manufacturers

| Manufacturer | Models | Protocols | Connections |
|--------------|--------|-----------|-------------|
| **Fairbanks** | FB2255, FB6000, 70-2453 | 6011, ASCII, Modbus | RS232/422/485, TCP/IP |
| **Rice Lake** | 720i, 820i, 920i, 880+ | Lantronix, Modbus, EtherNet/IP | TCP:10001, RS232/485, USB |
| **Cardinal** | 201, 204, 205, 210, 225 | ASCII, Modbus, EtherNet/IP | RS232/485, TCP/IP, 4-20mA |
| **Avery Weigh-Tronix** | ZM, BSQ, ZK830 | ASCII, Modbus | RS232, TCP/IP |
| **Mettler Toledo** | IND560, IND780 | MT-SICS, Modbus | RS232, TCP/IP |

### Protocol Details

**ASCII-Based:**
- Fairbanks 6011: `STATUS WEIGHT TARE` format
- Generic ASCII: Configurable regex parsing
- NTEP Continuous: Continuous stream with STX/ETX
- NTEP Demand: Request/response mode

**Binary:**
- Modbus RTU: Serial binary protocol (9600-115200 baud)
- Modbus TCP: Ethernet binary (port 502)
- EtherNet/IP: Industrial protocol (port 44818)

**Analog:**
- 4-20mA current loop
- 0-10V voltage (via USB ADC module)

### Common Data Formats

```
Fairbanks 6011:  1   00044140  00000
Generic ASCII:   +00123.45 LB<CR><LF>
NTEP:            STX GS WEIGHT NET TARE GROSS UNIT STATUS ETX
Modbus:          Function 03, Register 0x0000-0x0001 (32-bit float)
```

---

## Architecture Changes

### V1.x Architecture (Current)
```
User launches ScaleStreamer.exe
  ↓
Creates NotifyIcon (system tray)
  ↓
User opens config, clicks "Start"
  ↓
App spawns FFmpeg + MediaMTX processes
  ↓
App reads scale data, feeds to FFmpeg stdin
```

**Problems:**
- User must stay logged in
- No auto-restart on crash
- No Windows Service support
- Single scale only

### V2.0 Architecture (New)
```
Windows boots
  ↓
ScaleStreamerService.exe starts automatically (Windows Service)
  ↓
Service loads config, connects to scale(s)
  ↓
Service spawns FFmpeg + MediaMTX (supervised)
  ↓
Service logs to SQLite + file logs
  ↓
User can open ScaleStreamerConfig.exe anytime
  ↓
Config GUI connects to Service via Named Pipes
  ↓
GUI shows real-time dashboard, allows configuration changes
```

**Benefits:**
- Runs 24/7 even when no user logged in
- Auto-restart on crash (Windows Service Recovery)
- Multi-scale support
- Remote monitoring capability
- Full diagnostics

---

## Component Breakdown

### 1. ScaleStreamerService.exe (NEW)

**Type**: Windows Service
**Language**: C# .NET 8
**Framework**: Microsoft.Extensions.Hosting

**Responsibilities**:
- Scale connection management (all protocols)
- Data buffering and validation
- FFmpeg/MediaMTX process supervision
- Watchdog monitoring
- SQLite database writes
- Named Pipe server (for GUI communication)

**Key Classes**:
```csharp
- ScaleServiceWorker : BackgroundService
- ScaleProtocolFactory : Factory for IScaleProtocol
- StreamManagerService : Manages FFmpeg/MediaMTX
- DatabaseService : SQLite operations
- ConfigurationService : Load/save settings
- IPCServer : Named Pipe server for GUI
```

### 2. ScaleStreamerConfig.exe (ENHANCED)

**Type**: WinForms Application
**Language**: C# .NET 8
**UI Framework**: WinForms with custom controls

**Tabs**:
1. **Connection** - Scale selection, protocol config
2. **Protocol** - Data format, polling, validation
3. **Video** - Stream settings, overlay designer
4. **Monitoring** - Real-time dashboard
5. **System** - Service control, performance
6. **Logging** - Log viewer, diagnostics
7. **Advanced** - Security, retention, network

**Communication**:
- Named Pipe client to Service
- Commands: Start/Stop/GetStatus/UpdateConfig
- Real-time updates via callback interface

### 3. ScaleStreamer.Common.dll (NEW)

**Type**: Class Library
**Purpose**: Shared code between Service and GUI

**Contents**:
- Protocol interfaces (`IScaleProtocol`)
- Data models (`WeightReading`, `ScaleStatus`)
- Configuration classes
- IPC message contracts
- Database schema

---

## Database Schema

### SQLite Database: `scalestreamer.db`

**Location**: `%ProgramData%\Cloud-Scale\ScaleStreamer\`

**Tables**:

1. **config** - Application settings (key-value)
2. **weight_log** - Weight transaction history
3. **events** - Application events and errors
4. **metrics** - Performance metrics (CPU, memory, throughput)
5. **scales** - Multi-scale configuration (v2.1+)

**Data Retention**:
- Weight logs: 30 days (configurable)
- Events: 90 days
- Metrics: 7 days (1-minute granularity)

---

## Development Phases

### Phase 1: Core Service Architecture (Week 1-2)

**Tasks**:
1. Create `ScaleStreamerService` project
2. Implement Windows Service hosting
3. Create protocol interface (`IScaleProtocol`)
4. Implement Fairbanks protocol (port from v1.x)
5. Add Named Pipe IPC server
6. Basic SQLite integration
7. Service installer in WiX

**Deliverable**: Service can connect to Fairbanks scale, log to database

### Phase 2: Multi-Protocol Support (Week 3-4)

**Tasks**:
1. Implement Rice Lake protocol (Lantronix)
2. Implement Cardinal protocol
3. Implement Modbus RTU
4. Implement Modbus TCP
5. Implement NTEP Continuous/Demand
6. Add protocol auto-detection
7. Unit tests for all protocols

**Deliverable**: Service supports all 9 protocols

### Phase 3: Configuration GUI - Part 1 (Week 5-6)

**Tasks**:
1. Create `ScaleStreamerConfig` WinForms project
2. Implement Named Pipe client
3. Build Connection Manager tab
4. Build Protocol Configuration tab
5. Build Video Settings tab
6. Add overlay designer (basic)
7. Service control (Start/Stop)

**Deliverable**: GUI can configure and control service

### Phase 4: Configuration GUI - Part 2 (Week 7)

**Tasks**:
1. Build Monitoring Dashboard tab
   - Real-time weight graph
   - Connection status
   - Statistics widgets
2. Build System Status tab
   - Process monitoring
   - Performance metrics
3. Build Logging tab
   - Real-time log viewer
   - Filter/search

**Deliverable**: Full-featured configuration application

### Phase 5: Assets & Installer (Week 8-9)

**Tasks**:
1. Convert SVG assets to ICO/PNG
   - Generate multi-size ICO files
   - Create system tray icon variations
   - Resize installer images
2. Update WiX installer
   - Add service installation
   - Include all assets
   - Desktop/Start menu shortcuts
3. Create installer banner/dialog images
4. Update license agreement
5. Test installation flow

**Deliverable**: Professional installer with branding

### Phase 6: Advanced Features (Week 10)

**Tasks**:
1. Advanced logging
   - Packet capture
   - Protocol analyzer
   - Export support bundle
2. Alerts and notifications
   - Email alerts
   - Windows notifications
   - Custom alert rules
3. Security
   - RTSP authentication
   - User access control
4. Multi-scale support (if time permits)

**Deliverable**: Enterprise-grade features

### Phase 7: Testing (Week 11)

**Tasks**:
1. Unit tests (90% coverage)
2. Integration tests
3. Hardware testing (if scales available)
   - Fairbanks scale
   - Generic NTEP scale
   - Modbus simulator
4. Stress testing
   - 7-day continuous run
   - 100 readings/sec sustained
   - Memory leak detection
5. Installation testing
   - Fresh Windows 10
   - Fresh Windows 11
   - Upgrade from v1.x

**Deliverable**: Stable, tested release

### Phase 8: Documentation & Release (Week 12)

**Tasks**:
1. User manual (PDF)
2. Administrator guide
3. API documentation
4. Troubleshooting guide
5. Video tutorials (screen recordings)
6. Release notes
7. GitHub README update
8. Website landing page

**Deliverable**: v2.0.0 released on GitHub

---

## Asset Preparation

### Immediate Actions

1. **Convert SVG to ICO**:
   ```bash
   # Use Inkscape or online converter
   # Required sizes: 16, 32, 48, 64, 128, 256

   # app-icon.ico (from cloudscale_icon.svg)
   # tray-icon-connected.ico (from cloudscale_tray_blue_64.svg)
   # tray-icon-disconnected.ico (from cloudscale_tray_monochrome_64.svg)
   # tray-icon-error.ico (create red version)
   ```

2. **Convert SVG to PNG**:
   ```bash
   # installer_banner_493x58.svg → banner.png
   # installer_dialog_493x312.svg → dialog.png
   # cloudscale_logo_horizontal.svg → cloud-scale-wordmark.png
   ```

3. **Download Fonts**:
   - Roboto Mono Bold: https://fonts.google.com/specimen/Roboto+Mono
   - Inter Regular: https://fonts.google.com/specimen/Inter

4. **Organize Assets**:
   ```
   Move to win-scale/assets/:
   - icons/*.ico (converted)
   - installer/*.png (converted)
   - branding/*.png (converted from SVG)
   - overlays/fonts/*.ttf (downloaded)
   ```

---

## Testing Strategy

### Test Environments

**Required**:
- Windows 10 22H2 (VM)
- Windows 11 23H2 (VM)
- Windows Server 2022 (if targeting server deployments)

**Hardware** (if available):
- Fairbanks scale with TCP/IP
- Generic NTEP scale with RS232
- USB-to-RS232 adapter
- Modbus simulator software

### Test Scenarios

1. **Installation**:
   - Clean install
   - Upgrade from v1.1.0
   - Uninstall/reinstall
   - Install as non-admin (should fail gracefully)

2. **Service**:
   - Auto-start on boot
   - Survives user logoff
   - Auto-restart on crash
   - Handles missing dependencies

3. **Protocols**:
   - Each protocol connects successfully
   - Handles network interruptions
   - Auto-reconnect works
   - Invalid data rejected

4. **GUI**:
   - Opens without service running (shows warning)
   - Connects to running service
   - Real-time updates work
   - Configuration changes applied

5. **Streaming**:
   - RTSP stream accessible
   - HLS stream works
   - Multiple viewers supported
   - Overlays render correctly

6. **Logging**:
   - Logs rotate properly
   - Database doesn't grow unbounded
   - Export functions work
   - No sensitive data logged

---

## Version Numbering

**Semantic Versioning**: MAJOR.MINOR.PATCH

- **v1.0.0** - Initial release
- **v1.1.0** - Logging and firewall improvements
- **v2.0.0** - Complete rewrite (this version)
- **v2.1.0** - Multi-scale support (future)
- **v2.2.0** - Cloud integration (future)
- **v3.0.0** - Cross-platform (Linux support) (far future)

---

## Git Workflow

### Branching Strategy

```
main (protected)
  ├── develop (v2.0.0 work)
  │   ├── feature/windows-service
  │   ├── feature/protocols
  │   ├── feature/gui-dashboard
  │   ├── feature/assets
  │   └── feature/installer
  └── hotfix/* (for v1.x critical bugs)
```

### Commit Messages

```
feat: Add Modbus TCP protocol support
fix: System tray icon not appearing
docs: Update v2.0.0 design specification
chore: Reorganize assets folder
test: Add unit tests for Rice Lake protocol
refactor: Extract IPC logic to separate class
```

### Tags

```bash
git tag -a v2.0.0-alpha.1 -m "Alpha release 1"
git tag -a v2.0.0-beta.1 -m "Beta release 1"
git tag -a v2.0.0 -m "Version 2.0.0 - Enterprise Release"
```

---

## Success Criteria

Version 2.0.0 is ready for release when:

✅ All 9 protocols tested and working
✅ Windows Service installs and runs reliably
✅ GUI connects to service and displays real-time data
✅ Installer creates proper shortcuts and service
✅ All assets integrated and displaying correctly
✅ Passes 7-day continuous operation test
✅ No memory leaks detected
✅ Database operates within size limits
✅ Documentation complete
✅ Zero critical bugs
✅ Less than 5 known minor bugs

---

## Risk Assessment

| Risk | Impact | Probability | Mitigation |
|------|--------|-------------|------------|
| No access to real scales for testing | High | High | Use Modbus simulators, mock data generators |
| Windows Service complexity | Medium | Medium | Use Microsoft.Extensions.Hosting framework |
| WiX installer issues | Medium | Low | Test early, use v1.x installer as template |
| Asset quality | Low | Low | SVG assets already provided by user |
| Timeline overrun | Medium | Medium | Prioritize core features, defer nice-to-haves |
| Protocol documentation gaps | Medium | Medium | Implement generic configurable parser |

---

## Post-Release Roadmap (v2.x)

**v2.1.0** - Multi-Scale Support
- Connect to multiple scales simultaneously
- Aggregate view
- Per-scale configuration

**v2.2.0** - Cloud Integration
- Upload weight data to cloud database
- Remote monitoring dashboard (web)
- Mobile app for iOS/Android

**v2.3.0** - Advanced Analytics
- Weight trending
- Predictive maintenance
- Anomaly detection
- Export to BI tools

**v3.0.0** - Cross-Platform
- Linux support (systemd service)
- Docker containerization
- Kubernetes deployment

---

## Resources

### Documentation
- [WiX Toolset v4 Docs](https://wixtoolset.org/docs/)
- [Windows Service Hosting](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services)
- [Modbus Protocol](https://modbus.org/docs/Modbus_Application_Protocol_V1_1b3.pdf)
- [SQLite Documentation](https://www.sqlite.org/docs.html)

### Tools
- Visual Studio 2022 Community (free)
- WiX Toolset 4.0
- Inkscape (SVG to PNG conversion)
- VLC Media Player (stream testing)
- Postman (protocol testing)

### Libraries
- `Microsoft.Extensions.Hosting` - Service hosting
- `System.IO.Ports` - RS232 communication
- `System.Data.SQLite` - Database
- `NModbus` - Modbus protocol
- `Serilog` - Structured logging

---

**Document Version**: 1.0
**Date**: 2026-01-24
**Author**: Cloud-Scale Engineering
**Status**: Ready for Implementation

**Next Steps**:
1. Set up development branch
2. Create Visual Studio solution structure
3. Begin Phase 1 (Windows Service)
4. Convert SVG assets to required formats
