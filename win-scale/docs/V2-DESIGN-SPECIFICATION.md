# Scale RTSP Streamer v2.0.0 - Design Specification

**Cloud-Scale Industrial IoT Platform**
**Comprehensive Scale Integration System**

---

## Executive Summary

Version 2.0.0 represents a complete architectural redesign to support enterprise-grade industrial weighing operations across floor scales, truck scales, train scales, and WIM (Weigh-in-Motion) systems. This version provides comprehensive protocol support for all major manufacturers and deployment scenarios.

---

## Supported Scale Manufacturers & Models

### Fairbanks Scales
- **Models**: FB2255, FB6000, 70-2453-4, WIM systems
- **Protocols**: Fairbanks 6011, Custom ASCII, Modbus RTU/TCP
- **Connections**: RS232, RS422, RS485, TCP/IP
- **Features**: Bidirectional communication, remote configuration

### Rice Lake Weighing Systems
- **Indicators**: 720i, 820i, 920i, 880 Plus, 1280
- **Protocols**: Lantronix TCP/IP, RS232/RS485, Modbus TCP, EtherNet/IP
- **Connections**: TCP/IP (port 10001), RS232, RS485, USB
- **Features**: Serial tunneling, streaming data, PLC integration

### Cardinal Scale
- **Models**: 201, 204, 205, 210, 225, 825
- **Protocols**: ASCII, Modbus RTU, Modbus TCP, EtherNet/IP
- **Connections**: RS232, RS485, TCP/IP, USB, 4-20mA analog, 0-10V
- **Features**: 200 samples/sec, wireless (Zigbee), Wi-Fi modules

### Additional Manufacturers
- **Avery Weigh-Tronix**: ZM Series, BSQ, ZK830
- **Mettler Toledo**: IND560, IND780, IND990
- **TRANSCELL**: TI-500E Plus
- **Arlyn Scales**: SAW Series, 6200 Series

---

## Protocol Support Matrix

### Serial Protocols (RS232/RS485/RS422)

| Protocol | Format | Mode | Baud Rates | Data Bits | Parity | Stop Bits |
|----------|--------|------|------------|-----------|--------|-----------|
| Fairbanks 6011 | `STATUS WEIGHT TARE` | Continuous/Demand | 1200-115200 | 7,8 | N,E,O | 1,2 |
| Generic ASCII | Configurable | Continuous/Demand | 1200-115200 | 7,8 | N,E,O | 1,2 |
| Modbus RTU | Binary | Polling | 9600-115200 | 8 | N,E,O | 1,2 |
| NTEP Continuous | ASCII | Continuous | 9600-38400 | 7,8 | N,E,O | 1,2 |
| NTEP Demand | ASCII | On-Request | 9600-38400 | 7,8 | N,E,O | 1,2 |

### Network Protocols (TCP/IP/Ethernet)

| Protocol | Port | Format | Features |
|----------|------|--------|----------|
| Raw TCP Socket | Configurable (10001 default) | ASCII | Direct scale connection |
| Modbus TCP | 502 | Binary | Industrial PLC integration |
| EtherNet/IP | 44818 | Binary | Allen-Bradley PLCs |
| HTTP REST API | 80/443 | JSON | Web integration |
| Lantronix Device Server | 10001 | ASCII | Rice Lake compatibility |

### Data Formats

```
// Fairbanks 6011 Format
STATUS   WEIGHT    TARE
1        00044140  00000

// Generic ASCII Formats
+00123.45 LB<CR><LF>
S 00123.4 KG<CR><LF>
123.45<CR><LF>

// NTEP Continuous Mode
STX GS WEIGHT NET TARE GROSS UNIT STATUS ETX

// Modbus RTU/TCP
Function Code: 03 (Read Holding Registers)
Starting Address: 0x0000
Quantity: 2 registers (weight = register 0-1)
```

---

## Architecture Overview

### Three-Tier System Design

```
┌─────────────────────────────────────────────────────┐
│  Presentation Layer (WinForms GUI)                  │
│  - Configuration UI                                  │
│  - Real-time monitoring dashboard                   │
│  - System tray integration                          │
└─────────────────────────────────────────────────────┘
                        ↕
┌─────────────────────────────────────────────────────┐
│  Business Logic Layer (Windows Service)             │
│  - Scale communication manager                      │
│  - Protocol parsers & adapters                      │
│  - Data validation & buffering                      │
│  - Stream generation                                │
└─────────────────────────────────────────────────────┘
                        ↕
┌─────────────────────────────────────────────────────┐
│  Data Layer                                         │
│  - SQLite embedded database                         │
│  - File-based logging (structured JSON)            │
│  - Configuration persistence                        │
└─────────────────────────────────────────────────────┘
```

---

## Component Architecture

### 1. Windows Service (ScaleStreamerService.exe)

**Purpose**: Background service for 24/7 unattended operation

**Responsibilities**:
- Scale connection management
- Data acquisition and buffering
- FFmpeg/MediaMTX process supervision
- Automatic reconnection
- Watchdog monitoring
- Performance metrics collection

**Service Configuration**:
```
Service Name: ScaleStreamerService
Display Name: Cloud-Scale RTSP Streamer
Startup Type: Automatic (Delayed Start)
Recovery: Restart on failure (3 attempts)
Dependencies: EventLog, Tcpip
```

### 2. Configuration GUI (ScaleStreamerConfig.exe)

**Purpose**: Full-featured configuration and monitoring application

**Features**:

#### Connection Manager Tab
- **Scale Selection**:
  - Manufacturer dropdown (Fairbanks, Rice Lake, Cardinal, etc.)
  - Model selection with auto-configuration
  - Protocol selection (Auto-detect available)

- **Connection Types**:
  - TCP/IP: IP address, port, timeout, keepalive
  - RS232: COM port, baud, data bits, parity, stop bits
  - RS485: Address, half/full duplex
  - USB: Auto-detect and enumerate
  - Analog: 4-20mA, 0-10V (via ADC module)

- **Connection Testing**:
  - Live data preview window
  - Signal strength indicator
  - Latency measurement
  - Packet loss tracking

#### Protocol Configuration Tab
- **Data Format**:
  - Weight format: Regex pattern builder
  - Unit extraction: Auto-detect LB/KG/OZ/G/T
  - Status codes: Mapping table
  - Tare handling

- **Polling Configuration**:
  - Continuous mode: Stream rate
  - Demand mode: Poll interval
  - Modbus registers: Custom mapping

- **Validation Rules**:
  - Min/max weight thresholds
  - Stability detection
  - Motion detection
  - Outlier filtering

#### Video Settings Tab
- **Stream Configuration**:
  - Resolution: 640x480 to 1920x1080
  - Frame rate: 1-60 FPS
  - Bitrate: 200-5000 kbps
  - Codec: H.264 (main/high profile)

- **Overlay Designer**:
  - Drag-and-drop layout editor
  - Live preview
  - Font selection
  - Color picker
  - Background images/gradients

- **Display Elements**:
  - Weight (primary, large format)
  - Tare weight
  - Gross weight
  - Net weight
  - Unit label
  - Scale ID
  - Timestamp (configurable format)
  - Company logo
  - Custom text fields (up to 10)
  - Stream rate KB/s
  - TX indicator
  - QR code (for mobile access)

#### Monitoring Dashboard Tab
- **Real-time Metrics**:
  - Current weight with trending graph
  - Connection status indicator
  - Uptime counter
  - Data rate (samples/sec)
  - Stream viewers count
  - Error rate

- **Historical Data**:
  - Weight history chart (last 24hrs)
  - Min/max/avg statistics
  - Transaction log
  - Event timeline

- **Alerts**:
  - Connection loss
  - Weight threshold exceeded
  - Stream failure
  - Disk space low
  - Custom alert rules

#### System Status Tab
- **Service Control**:
  - Start/Stop/Restart service
  - Service status indicator
  - Process CPU/Memory usage

- **Component Status**:
  - FFmpeg: Running/PID/CPU/Mem
  - MediaMTX: Running/PID/CPU/Mem
  - Scale connection: Connected/Disconnected
  - Stream status: Active/Inactive/Error

- **Performance Metrics**:
  - Data throughput
  - Frame generation rate
  - Network bandwidth usage
  - Buffer status

#### Logging & Diagnostics Tab
- **Log Viewer**:
  - Real-time log tail
  - Filter by level (Debug/Info/Warn/Error)
  - Search functionality
  - Export to file

- **Diagnostic Tools**:
  - Packet capture (raw scale data)
  - Protocol analyzer
  - Connection tester
  - Stream validator

- **Export Options**:
  - Export logs (CSV/JSON/TXT)
  - Generate support bundle
  - Email support request

#### Advanced Settings Tab
- **Scale-Specific Settings**:
  - Manufacturer-specific features
  - Custom command sequences
  - Calibration data

- **Network Settings**:
  - RTSP port (default: 8554)
  - HLS port (default: 8888)
  - WebRTC port
  - Firewall auto-configuration

- **Data Retention**:
  - Log retention period
  - Database purge schedule
  - Backup configuration

- **Security**:
  - RTSP authentication
  - SSL/TLS for web interface
  - User access control

### 3. System Tray Application

**Purpose**: Quick access and status monitoring

**Features**:
- Icon changes based on status (connected/disconnected/error)
- Tooltip shows current weight
- Context menu:
  - Open Configuration
  - Start/Stop Service
  - Quick Connect/Disconnect
  - View Stream (VLC)
  - Copy Stream URL
  - Recent Weights
  - About/Help
  - Exit

---

## Data Storage

### SQLite Database Schema

```sql
-- Configuration
CREATE TABLE config (
    key TEXT PRIMARY KEY,
    value TEXT,
    modified TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Weight Transactions
CREATE TABLE weight_log (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    timestamp DATETIME DEFAULT CURRENT_TIMESTAMP,
    weight_value REAL,
    weight_unit TEXT,
    tare REAL,
    gross REAL,
    status TEXT,
    scale_id TEXT,
    source_ip TEXT
);

-- Events
CREATE TABLE events (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    timestamp DATETIME DEFAULT CURRENT_TIMESTAMP,
    level TEXT CHECK(level IN ('DEBUG','INFO','WARN','ERROR','CRITICAL')),
    category TEXT,
    message TEXT,
    details TEXT
);

-- Performance Metrics
CREATE TABLE metrics (
    timestamp DATETIME DEFAULT CURRENT_TIMESTAMP,
    cpu_percent REAL,
    memory_mb REAL,
    data_rate REAL,
    frame_rate REAL,
    stream_bitrate REAL,
    viewer_count INTEGER
);

-- Indexes for performance
CREATE INDEX idx_weight_timestamp ON weight_log(timestamp);
CREATE INDEX idx_events_timestamp ON events(timestamp);
CREATE INDEX idx_events_level ON events(level);
CREATE INDEX idx_metrics_timestamp ON metrics(timestamp);
```

### Logging System

**Structured JSON Logging**:
```json
{
  "timestamp": "2026-01-24T14:32:15.123Z",
  "level": "INFO",
  "category": "ScaleConnection",
  "message": "Connected to Fairbanks scale",
  "details": {
    "scale_type": "Fairbanks FB2255",
    "connection": "TCP/IP",
    "endpoint": "192.168.1.100:10001",
    "protocol": "Fairbanks 6011",
    "response_time_ms": 45
  },
  "correlation_id": "abc-123-xyz"
}
```

**Log Files**:
- `app-YYYYMMDD.log` - Application logs (daily rotation)
- `scale-data-YYYYMMDD.log` - Raw scale data (for diagnostics)
- `stream-YYYYMMDD.log` - FFmpeg/MediaMTX logs
- `performance-YYYYMMDD.log` - Performance metrics

**Log Levels**:
- DEBUG: Verbose diagnostic information
- INFO: Normal operations
- WARN: Potential issues
- ERROR: Errors that don't stop service
- CRITICAL: Fatal errors requiring restart

---

## Protocol Adapters

### Scale Protocol Interface

```csharp
public interface IScaleProtocol
{
    string Name { get; }
    string Manufacturer { get; }

    Task<bool> ConnectAsync(ConnectionConfig config);
    Task<bool> DisconnectAsync();
    Task<WeightReading> ReadWeightAsync();
    Task<ScaleStatus> GetStatusAsync();
    Task SendCommandAsync(string command);

    event EventHandler<WeightReading> WeightReceived;
    event EventHandler<ScaleStatus> StatusChanged;
    event EventHandler<Exception> ErrorOccurred;
}
```

### Implemented Protocols

1. **FairbanksProtocol** - Fairbanks 6011 ASCII format
2. **RiceLakeProtocol** - Lantronix TCP/IP serial tunneling
3. **CardinalProtocol** - Standard Cardinal ASCII
4. **ModbusRTUProtocol** - Modbus RTU over serial
5. **ModbusTCPProtocol** - Modbus TCP over Ethernet
6. **GenericASCIIProtocol** - Configurable regex parser
7. **NTEPContinuousProtocol** - NTEP continuous mode
8. **NTEPDemandProtocol** - NTEP demand mode
9. **AnalogProtocol** - 4-20mA / 0-10V via USB ADC

---

## Assets Folder Structure

```
win-scale/assets/
├── icons/
│   ├── app-icon.ico              # 256x256, 128x128, 64x64, 48x48, 32x32, 16x16
│   ├── tray-icon-connected.ico   # Green indicator
│   ├── tray-icon-disconnected.ico# Gray indicator
│   ├── tray-icon-error.ico       # Red indicator
│   ├── desktop-shortcut.ico      # Desktop icon
│   └── file-association.ico      # .scaleconfig file icon
│
├── installer/
│   ├── banner.png                # 493x58 WiX banner (top)
│   ├── dialog.png                # 493x312 WiX dialog background
│   ├── license.rtf               # EULA
│   ├── welcome.png               # 164x164 welcome screen
│   └── finished.png              # 164x164 completion screen
│
├── branding/
│   ├── cloud-scale-logo.png      # Transparent PNG
│   ├── cloud-scale-wordmark.png  # Logo with text
│   ├── favicon.ico               # Web interface
│   └── splash-screen.png         # Application startup (800x600)
│
└── overlays/
    ├── default-background.png    # Video stream background
    ├── corporate-template.png    # Corporate branding template
    └── fonts/
        ├── RobotoMono-Bold.ttf   # Monospace for weight
        └── Inter-Regular.ttf     # UI font
```

---

## Installation Package Changes

### WiX Installer Updates (v2.0.0)

**Assets Integration**:
```xml
<Component Id="AppIcons" Guid="...">
    <File Source="$(var.AssetsDir)\icons\app-icon.ico" KeyPath="yes" />
    <File Source="$(var.AssetsDir)\icons\tray-icon-connected.ico" />
    <File Source="$(var.AssetsDir)\icons\tray-icon-disconnected.ico" />
    <File Source="$(var.AssetsDir)\icons\tray-icon-error.ico" />
</Component>

<Component Id="InstallerBranding" Guid="...">
    <File Source="$(var.AssetsDir)\branding\cloud-scale-logo.png" />
    <File Source="$(var.AssetsDir)\branding\splash-screen.png" />
</Component>
```

**Installer UI Customization**:
```xml
<WixVariable Id="WixUIBannerBmp" Value="$(var.AssetsDir)\installer\banner.png" />
<WixVariable Id="WixUIDialogBmp" Value="$(var.AssetsDir)\installer\dialog.png" />
<WixVariable Id="WixUILicenseRtf" Value="$(var.AssetsDir)\installer\license.rtf" />
```

**Service Installation**:
```xml
<Component Id="WindowsService" Guid="...">
    <File Id="ServiceExe" Source="$(var.PublishDir)\ScaleStreamerService.exe" KeyPath="yes" />
    <ServiceInstall
        Id="ScaleStreamerService"
        Name="ScaleStreamerService"
        DisplayName="Cloud-Scale RTSP Streamer"
        Description="Industrial scale weight streaming service"
        Type="ownProcess"
        Start="auto"
        ErrorControl="normal"
        Account="LocalSystem"
        Interactive="no"
        DelayedAutoStart="yes">
        <ServiceDependency Id="EventLog" />
        <ServiceDependency Id="Tcpip" />
    </ServiceInstall>
    <ServiceControl Id="StartService" Name="ScaleStreamerService" Start="install" Stop="both" Remove="uninstall" Wait="yes" />
</Component>
```

**Desktop Icon**:
```xml
<Component Id="DesktopIcon" Guid="...">
    <Shortcut Id="DesktopShortcut"
              Directory="DesktopFolder"
              Name="Scale Streamer Config"
              Target="[INSTALLFOLDER]ScaleStreamerConfig.exe"
              WorkingDirectory="INSTALLFOLDER"
              Icon="DesktopIcon.ico" />
    <RegistryValue Root="HKCU" Key="Software\Cloud-Scale\ScaleStreamer" Name="desktop_shortcut" Type="integer" Value="1" KeyPath="yes" />
</Component>
```

---

## Testing Requirements

### Unit Tests
- Protocol parsers (all 9 protocols)
- Data validation
- Configuration management
- Database operations

### Integration Tests
- Service installation/uninstallation
- Service start/stop
- GUI ↔ Service communication
- FFmpeg/MediaMTX integration

### Hardware Tests
| Manufacturer | Model | Connection | Protocol | Status |
|--------------|-------|------------|----------|--------|
| Fairbanks | FB2255 | TCP/IP | Fairbanks 6011 | ⏳ Pending |
| Rice Lake | 920i | TCP/IP | Lantronix | ⏳ Pending |
| Cardinal | 201 | RS232 | ASCII | ⏳ Pending |
| Generic | NTEP | RS232 | Continuous | ⏳ Pending |

### Performance Tests
- Continuous operation (7+ days)
- 100 weight readings/second sustained
- Memory leak detection
- Auto-reconnection stress test

---

## Deployment Plan

### Phase 1: Core Development (4 weeks)
- Week 1-2: Protocol adapters
- Week 3: Windows Service architecture
- Week 4: Basic GUI framework

### Phase 2: GUI Development (3 weeks)
- Week 5: Connection configuration
- Week 6: Monitoring dashboard
- Week 7: Advanced settings

### Phase 3: Integration (2 weeks)
- Week 8: Service ↔ GUI integration
- Week 9: Asset integration, installer updates

### Phase 4: Testing (2 weeks)
- Week 10: Unit/integration tests
- Week 11: Hardware testing, bug fixes

### Phase 5: Documentation & Release (1 week)
- Week 12: User manual, release v2.0.0

---

## Version History

- **v1.0.0** - Initial release (basic Fairbanks 6011, TCP/IP only)
- **v1.1.0** - Added logging, firewall rules, Cloud-Scale branding
- **v2.0.0** - Complete rewrite:
  - Windows Service architecture
  - Multi-manufacturer support (9 protocols)
  - Full-featured GUI
  - Comprehensive logging
  - SQLite database
  - Asset integration
  - Enterprise features

---

**Document Version**: 1.0
**Date**: 2026-01-24
**Author**: Cloud-Scale Engineering Team
**Status**: Design Specification - Ready for Implementation
