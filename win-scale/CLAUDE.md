# Scale Streamer v2.0 - Master Claude Prompt & Project Restoration Guide

## Project Identity

**Name:** Scale Streamer v2.0
**Company:** Cloud-Scale IoT Solutions
**Purpose:** Universal industrial scale data acquisition, streaming, and analytics platform
**Status:** Production-ready with self-contained installer
**Last Updated:** January 24, 2026

---

## Executive Summary

Scale Streamer v2.0 is a **universal scale integration platform** that connects ANY industrial scale (regardless of manufacturer) to modern cloud infrastructure. It solves the fundamental problem that every scale manufacturer uses proprietary protocols, making integration expensive and complex.

### The Problem We Solve
- Industrial facilities have scales from multiple manufacturers (Fairbanks, Mettler Toledo, Rice Lake, etc.)
- Each manufacturer uses different communication protocols
- Traditional solutions require custom integration for each scale type
- No standardized way to stream scale data to cloud/analytics platforms

### Our Solution
- **Universal protocol engine** - Define any scale protocol in JSON
- **Plugin architecture** - Support ANY scale manufacturer
- **Windows Service** - Reliable background operation
- **Self-contained installer** - No dependencies, works anywhere
- **RTSP/HLS streaming** - Stream weight data like video
- **Cloud-ready** - Built for IoT and cloud analytics

---

## Architecture Philosophy

### Design Principles

1. **Universal, Not Specific**
   - Don't hardcode protocols
   - JSON-defined protocol templates
   - Regex and positional parsing engines
   - Easy to add new scale types

2. **Industrial-Grade Reliability**
   - Windows Service architecture
   - Auto-restart on failure
   - Connection retry logic
   - Comprehensive logging

3. **Zero-Touch Deployment**
   - Self-contained MSI installer
   - All .NET runtime included (55MB)
   - No prerequisites needed
   - Auto-start service

4. **Cloud-First Design**
   - RTSP streaming foundation
   - REST API ready
   - Named Pipe IPC
   - Database for persistence

---

## Technical Architecture

### Component Hierarchy

```
ScaleStreamer v2.0
├── ScaleStreamer.Service (Windows Service)
│   ├── ScaleConnectionManager
│   ├── IpcCommandHandler
│   ├── Protocol template loader
│   └── Weight streaming engine
│
├── ScaleStreamer.Config (WinForms GUI)
│   ├── Connection configuration
│   ├── Protocol designer/tester
│   ├── Real-time monitoring
│   ├── Service control
│   └── Event logging
│
├── ScaleStreamer.Common (Shared Library)
│   ├── Universal protocol engine
│   ├── IPC infrastructure
│   ├── Database service (SQLite)
│   ├── RTSP streaming (foundation)
│   └── Models and utilities
│
└── Protocols (JSON Templates)
    ├── Fairbanks 6011
    ├── Generic ASCII
    └── Modbus TCP
```

### Key Technologies

- **.NET 8.0** - Modern, cross-platform framework
- **Windows Services** - Background operation
- **Named Pipes** - IPC between service and GUI
- **SQLite** - Embedded database
- **Serilog** - Structured logging
- **WiX Toolset v4** - Professional installer
- **Windows Forms** - Configuration GUI

---

## Protocol Engine - The Core Innovation

### Universal Protocol Definition (JSON)

```json
{
  "protocolName": "Fairbanks 6011",
  "manufacturer": "Fairbanks",
  "version": "1.0",
  "connectionType": "TcpIp",
  "dataFormat": "Ascii",
  "dataMode": "Streaming",
  "lineDelimiter": "\r\n",
  "fieldSeparator": "\\s+",
  "fields": [
    {
      "name": "weight",
      "dataType": "float",
      "position": 1,
      "multiplier": 1.0
    },
    {
      "name": "status",
      "dataType": "string",
      "position": 0
    }
  ]
}
```

### Parser Types

1. **Positional Parser** - Field at specific position (delimited)
2. **Regex Parser** - Named capture groups
3. **Fixed-Width Parser** - Column positions
4. **Custom Parser** - C# plugin for complex protocols

### Supported Connection Types

- **TCP/IP** - Network scales (most common)
- **Serial (RS-232)** - Legacy scales
- **Modbus TCP** - Industrial protocols
- **Custom** - Extensible for any transport

---

## Installation & Deployment

### Self-Contained Installer

**File:** `ScaleStreamer-v2.0.1-YYYYMMDD-HHMMSS.msi` (55 MB)

**Includes:**
- ScaleStreamer.Service.exe + 226 DLLs
- ScaleStreamer.Config.exe + 248 DLLs
- All .NET 8 runtime libraries
- Protocol templates (JSON)
- Database schema
- Cloud Scale branding

**Installation:**
```powershell
msiexec /i ScaleStreamer-v2.0.1-YYYYMMDD-HHMMSS.msi /l*v install.log
```

**Silent Install:**
```powershell
msiexec /i ScaleStreamer-v2.0.1-YYYYMMDD-HHMMSS.msi /quiet
```

### Installation Layout

```
C:\Program Files\Scale Streamer\
├── Service\
│   ├── ScaleStreamer.Service.exe
│   └── [226 DLLs - .NET runtime + dependencies]
├── Config\
│   ├── ScaleStreamer.Config.exe
│   └── [248 DLLs - .NET runtime + WinForms]
├── protocols\
│   ├── manufacturers\
│   │   └── fairbanks-6011.json
│   └── generic\
│       ├── generic-ascii.json
│       └── modbus-tcp.json
└── docs\
    ├── QUICK-START-V2.md
    ├── BUILD-AND-TEST-V2.md
    └── V2-UNIVERSAL-ARCHITECTURE.md

C:\ProgramData\ScaleStreamer\
├── scalestreamer.db (SQLite)
├── logs\
│   └── service-YYYYMMDD.log
└── backups\
```

---

## Building From Source

### Prerequisites

- Windows 10/11 or Windows Server 2019+
- .NET 8 SDK
- WiX Toolset v4 (for installer)
- Git

### Build Commands

```powershell
# 1. Clone repository
git clone https://github.com/CNesbitt2025/Cloud-Scale.git
cd Cloud-Scale/win-scale

# 2. Build self-contained binaries (includes all .NET runtime)
cd installer
.\build-self-contained.ps1

# 3. Build MSI installer
.\build-installer-selfcontained.ps1

# Output: installer/bin/ScaleStreamer-v2.0.1-YYYYMMDD-HHMMSS.msi
```

### Development Build

```powershell
# Open solution in Visual Studio 2022
start ScaleStreamer.sln

# Or build from command line
dotnet build -c Release
```

---

## Configuration & Usage

### 1. Configure Scale Connection (GUI)

1. Launch "Scale Streamer Configuration"
2. Go to **Connection** tab
3. Configure:
   - Scale ID (unique identifier)
   - Connection type (TCP/IP or Serial)
   - Host/Port or COM port
   - Select protocol template
4. Click "Test Connection"
5. Click "Save Configuration"

### 2. Monitor Real-Time Data

1. Go to **Monitoring** tab
2. View:
   - Current weight (large display)
   - Status (stable, motion, overload)
   - Reading rate (readings/sec)
   - History (last 100 readings)
   - Raw data stream

### 3. Service Control

1. Go to **Status** tab
2. View service status and uptime
3. Control service:
   - Start Service
   - Stop Service
   - Restart Service

### 4. Design Custom Protocols

1. Go to **Protocol** tab
2. Configure:
   - Data format (ASCII, Binary)
   - Data mode (Streaming, Polled)
   - Delimiters and separators
   - Field definitions
3. Test with sample data
4. Save as JSON template

---

## Future Roadmap

### Phase 1: RTSP Streaming (Q1 2026)
- Real-time weight streaming over RTSP
- Integration with MediaMTX
- FFmpeg overlay for weight display
- Network monitoring clients

### Phase 2: REST API (Q2 2026)
- RESTful API for scale management
- WebSocket for real-time data
- Swagger/OpenAPI documentation
- Authentication and authorization

### Phase 3: Cloud Integration (Q2-Q3 2026)
- Azure IoT Hub integration
- AWS IoT Core support
- MQTT publishing
- Cloud dashboards

### Phase 4: Analytics & ML (Q3-Q4 2026)
- Historical data analysis
- Predictive maintenance
- Anomaly detection
- Trend analysis

### Phase 5: Mobile Apps (Q4 2026)
- iOS/Android monitoring apps
- Remote configuration
- Push notifications
- QR code scale pairing

### Phase 6: Advanced Features (2027)
- Multi-scale aggregation
- Load balancing across scales
- Batch processing
- Integration with ERP systems

---

## Master Prompts for Claude

### Prompt 1: Understanding the Project

```
I'm working on Scale Streamer v2.0, a universal industrial scale integration platform for Cloud-Scale IoT Solutions.

The project consists of:
1. ScaleStreamer.Service - Windows Service for background scale management
2. ScaleStreamer.Config - WinForms GUI for configuration
3. ScaleStreamer.Common - Shared library with universal protocol engine
4. Self-contained MSI installer built with WiX Toolset v4

Key features:
- Universal protocol engine (JSON-defined protocols)
- Supports ANY scale manufacturer via plugins
- TCP/IP and Serial (RS-232) connections
- RTSP streaming foundation
- Self-contained installer (55MB, includes all .NET 8 runtime)
- Cloud Scale branding

Read CLAUDE.md for complete architecture and context.
```

### Prompt 2: Building/Rebuilding

```
I need to rebuild the Scale Streamer v2.0 installer.

Location: D:\win-scale\win-scale
Repository: https://github.com/CNesbitt2025/Cloud-Scale.git

Steps:
1. Run installer/build-self-contained.ps1 (creates self-contained binaries)
2. Run installer/build-installer-selfcontained.ps1 (creates MSI)

The installer must include:
- All .NET 8 runtime dependencies (226 DLLs for Service, 248 for Config)
- Protocol templates (Fairbanks, Generic ASCII, Modbus TCP)
- Cloud Scale branding (logo.png in assets/)
- Windows Service auto-start configuration

Known issues to avoid:
- Service publish dir is net8.0\win-x64 (NOT net8.0-windows)
- Config publish dir is net8.0-windows\win-x64
- Protocols path: parent directory of Service folder
- GUI connection: deferred until form shown (avoid startup hang)

Read BUILD-AND-TEST-V2.md for detailed build instructions.
```

### Prompt 3: Adding New Scale Protocol

```
I need to add support for a new scale protocol to Scale Streamer v2.0.

Create a JSON protocol template in protocols/manufacturers/ with:
- Protocol name and manufacturer
- Connection type (TcpIp or Serial)
- Data format (Ascii or Binary)
- Data mode (Streaming or Polled)
- Line delimiter and field separator
- Field definitions (name, type, position, multiplier)

Example data from the scale: [provide sample output]

The universal protocol engine (UniversalProtocolEngine.cs) will automatically parse it using either:
- Positional parsing (by field position with separator)
- Regex parsing (named capture groups)

Read existing protocols in protocols/ for examples.
```

### Prompt 4: Troubleshooting

```
Scale Streamer v2.0 is experiencing [describe issue].

Architecture:
- Windows Service runs in background (LocalSystem account)
- Config GUI connects via Named Pipe (ScaleStreamerPipe)
- Logs: C:\ProgramData\ScaleStreamer\logs\service-YYYYMMDD.log
- Database: C:\ProgramData\ScaleStreamer\scalestreamer.db

Common issues:
1. Service won't start - Check .NET 8 runtime (if not self-contained build)
2. GUI won't connect - Check service is running, Named Pipe permissions
3. Protocol not loading - Check protocols/ directory path
4. Scale not connecting - Check TCP/IP host/port or COM port settings

Check service logs first for detailed error messages.
```

### Prompt 5: Future Development

```
I want to implement [feature] for Scale Streamer v2.0.

Current architecture:
- Service: Background scale management
- Common: Shared library with protocol engine
- Config: WinForms GUI

Planned features (see CLAUDE.md roadmap):
- RTSP streaming (in progress)
- REST API
- Cloud integration (Azure IoT, AWS IoT)
- Analytics and ML
- Mobile apps

Design principles:
- Universal, not manufacturer-specific
- Industrial-grade reliability
- Zero-touch deployment
- Cloud-first design

Where does [feature] fit in this architecture?
```

---

## Critical Files & Locations

### Source Code
```
src-v2/
├── ScaleStreamer.Common/      # Shared library
├── ScaleStreamer.Service/     # Windows Service
└── ScaleStreamer.Config/      # WinForms GUI
```

### Build System
```
installer/
├── build-self-contained.ps1           # Build self-contained binaries
├── build-installer-selfcontained.ps1  # Build MSI installer
├── generate-components.ps1            # Auto-generate WiX components
├── ScaleStreamerV2-SelfContained.wxs  # WiX installer definition
└── bin/                               # Output MSI files
```

### Branding
```
assets/
├── logo.png                    # Source logo (2.2MB)
├── logo.ico                    # Multi-resolution icon (36KB)
├── convert-logo-to-ico.ps1    # Icon converter
└── create-installer-graphics.ps1  # Banner/dialog creator
```

### Configuration
```
protocols/
├── manufacturers/
│   └── fairbanks-6011.json
└── generic/
    ├── generic-ascii.json
    └── modbus-tcp.json
```

### Documentation
```
CLAUDE.md                      # This file - Master prompt guide
BUILD-AND-TEST-V2.md          # Build and test instructions
QUICK-START-V2.md             # User quick start guide
V2-UNIVERSAL-ARCHITECTURE.md  # Technical architecture
INSTALLER-BUILD-READY.md      # Installer build guide
```

---

## Key Design Decisions

### Why Windows Service?
- Runs in background without user login
- Automatic startup on boot
- System-level permissions for hardware access
- Professional deployment model

### Why Self-Contained Installer?
- No .NET installation required on target systems
- Guaranteed compatibility (no version conflicts)
- Single MSI deployment artifact
- Works on air-gapped networks

### Why JSON Protocol Definitions?
- Non-programmers can add scale types
- Field engineers can customize protocols
- Version control friendly
- Easy to share and document

### Why Named Pipes for IPC?
- Fast local communication
- Windows security integration
- Bi-directional messaging
- No network port conflicts

### Why SQLite?
- Zero administration
- Embedded (no separate database server)
- Reliable and proven
- Excellent .NET support

### Why WiX Toolset?
- Industry standard MSI creation
- Windows Installer integration
- Professional installation experience
- Upgrade/uninstall support

---

## Success Metrics

### Technical Metrics
- ✅ Supports 3+ protocol types out of box
- ✅ Self-contained installer (no prerequisites)
- ✅ Service auto-starts and auto-recovers
- ✅ GUI connects within 1 second
- ✅ Can add new protocol in < 15 minutes

### Business Metrics
- 🎯 Reduce scale integration time from weeks to hours
- 🎯 Support 90%+ of industrial scales with JSON configs
- 🎯 Zero-touch deployment on any Windows system
- 🎯 < 5 minute installation time
- 🎯 Enable real-time weight streaming to cloud

### User Experience Metrics
- ✅ Installer size < 60MB
- ✅ Installation completes in < 2 minutes
- ✅ GUI launches without hang
- ✅ Logo and branding throughout
- 🎯 Protocol designer usable by non-programmers

---

## Known Limitations & Future Improvements

### Current Limitations

1. **Single Machine Only**
   - Service runs on one Windows machine
   - Future: Distributed architecture, scale clustering

2. **Windows Only**
   - Requires Windows OS
   - Future: Linux version, Docker containers

3. **Limited Streaming**
   - RTSP foundation only
   - Future: Complete RTSP/HLS implementation

4. **No Cloud Integration**
   - Local only
   - Future: Azure IoT Hub, AWS IoT Core, MQTT

5. **Basic Analytics**
   - Raw data only
   - Future: ML-based analytics, predictive maintenance

### Improvement Roadmap

**Short Term (Q1 2026)**
- Complete RTSP streaming implementation
- Add more protocol templates (10+ manufacturers)
- Performance testing with 50+ concurrent scales
- Memory optimization

**Medium Term (Q2-Q3 2026)**
- REST API and WebSocket support
- Cloud connector modules
- Web-based configuration UI
- Mobile apps (iOS/Android)

**Long Term (Q4 2026+)**
- Machine learning integration
- Multi-site deployment
- Enterprise features (LDAP, SSO)
- SaaS offering

---

## Disaster Recovery

### Backup Strategy

**Critical Files to Backup:**
1. Source code: `src-v2/`
2. Protocol templates: `protocols/`
3. Build scripts: `installer/*.ps1`
4. Branding assets: `assets/`
5. Documentation: `*.md`
6. Installer definition: `installer/*.wxs`

**Backup Commands:**
```powershell
# Full repository backup
git bundle create scale-streamer-backup.bundle --all

# Or just archive critical files
Compress-Archive -Path src-v2,protocols,installer,assets,*.md -DestinationPath scale-streamer-backup.zip
```

### Restore from Backup

```powershell
# From git bundle
git clone scale-streamer-backup.bundle Cloud-Scale

# Or from zip
Expand-Archive scale-streamer-backup.zip -DestinationPath Cloud-Scale

# Rebuild
cd Cloud-Scale/win-scale/installer
.\build-self-contained.ps1
.\build-installer-selfcontained.ps1
```

### Database Backup

```powershell
# Service automatically backs up database to:
# C:\ProgramData\ScaleStreamer\backups\

# Manual backup
Copy-Item "C:\ProgramData\ScaleStreamer\scalestreamer.db" `
          "C:\ProgramData\ScaleStreamer\backups\scalestreamer-$(Get-Date -Format 'yyyyMMdd-HHmmss').db"
```

---

## Contact & Support

**Company:** Cloud-Scale IoT Solutions
**Website:** https://cloud-scale.us
**Support:** admin@cloud-scale.us
**Repository:** https://github.com/CNesbitt2025/Cloud-Scale

**Key Personnel:**
- Project Lead: [Your Name]
- Development: Claude Code (Anthropic)

---

## License

Scale Streamer v2.0 - End User License Agreement

Copyright (c) 2026 Cloud-Scale IoT Solutions

Permission is hereby granted to use this software for commercial and
non-commercial purposes subject to the following conditions:

1. This software is provided "as is" without warranty of any kind.

2. Cloud-Scale shall not be liable for any damages arising from the
   use of this software.

3. Redistribution of this software requires written permission from
   Cloud-Scale.

For licensing inquiries, contact: admin@cloud-scale.us

---

## Version History

**v2.0.1** (January 24, 2026)
- Self-contained installer with all .NET runtime
- Cloud Scale branding (logo, icons, installer graphics)
- Fixed protocols directory path lookup
- Fixed GUI startup hang (deferred connection)
- Production-ready release

**v2.0.0** (January 2026)
- Complete rewrite with universal protocol engine
- Windows Service architecture
- WinForms configuration GUI
- SQLite database
- Named Pipe IPC
- RTSP streaming foundation
- WiX v4 installer

**v1.x** (Legacy)
- Fairbanks 6011 specific
- Single-purpose application
- No installer

---

## Glossary

**Scale** - Industrial weighing equipment that measures mass/weight
**Protocol** - Communication format used by a scale (manufacturer-specific)
**RTSP** - Real-Time Streaming Protocol (for video/data streaming)
**IPC** - Inter-Process Communication (Named Pipes)
**WiX** - Windows Installer XML (MSI creation toolkit)
**Self-Contained** - Includes all runtime dependencies
**Universal Protocol Engine** - Parser that can handle any protocol via JSON config

---

*Last Updated: January 24, 2026*
*This is the master restoration guide for Scale Streamer v2.0*
*Keep this file updated as the project evolves*
