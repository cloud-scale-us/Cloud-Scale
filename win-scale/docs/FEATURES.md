# Scale RTSP Streamer - Feature List

## Product Overview

**Scale RTSP Streamer** is a professional-grade Windows application that converts industrial scale weight data into a real-time video stream. Perfect for surveillance systems, remote monitoring, quality control, and compliance documentation.

---

## Core Features

### Multi-Protocol Scale Connectivity

- **TCP/IP Network Connection**
  - Connect to network-enabled scales over Ethernet
  - Configurable IP address and port settings
  - Auto-reconnect on connection loss
  - Connection health monitoring

- **RS232 Serial Connection**
  - Direct COM port connectivity
  - Configurable baud rate (9600-115200)
  - Support for standard serial parameters (data bits, parity, stop bits)
  - Automatic port detection

- **Fairbanks 6011 Protocol Support**
  - Native parsing of Fairbanks scale data format
  - Handles positive and negative weight values
  - Tare weight support
  - Compatible with other ASCII-based scale protocols

### Real-Time RTSP Video Streaming

- **H.264 Video Encoding**
  - Industry-standard codec for maximum compatibility
  - Ultra-low latency streaming (sub-second delay)
  - Configurable bitrate (200-2000 kbps)
  - Hardware-accelerated encoding support

- **Multiple Resolution Options**
  - 640x480 (SD) - Low bandwidth
  - 800x600 (SVGA)
  - 1280x720 (HD) - High clarity
  - 1920x1080 (Full HD) - Maximum detail

- **Configurable Frame Rates**
  - 15, 25, 30, or 60 FPS options
  - Optimized for real-time weight updates

### Professional Video Overlay

- **Weight Display**
  - Large, easy-to-read weight numbers
  - Configurable font color
  - Automatic decimal formatting
  - Real-time updates (10 updates per second)

- **Customizable Branding**
  - Custom title text (company name, scale ID)
  - Unit label (LB, KG, OZ, G)
  - Custom on-screen label field
  - Professional appearance

- **Live Timestamp**
  - Date and time overlay (toggle on/off)
  - Automatic timezone handling
  - Ideal for compliance and audit trails

- **Stream Status Indicators**
  - Real-time bitrate display (KB/s)
  - Green blinking transmit indicator [TX]
  - Visual confirmation of active streaming

### NVR & Surveillance Integration

- **Standard RTSP Protocol**
  - Compatible with all major NVR systems
  - Works with Hikvision, Dahua, Milestone, Blue Iris
  - ONVIF-compatible stream structure

- **HLS Web Streaming**
  - Browser-based viewing
  - No plugins required
  - Cross-platform compatibility

- **Multi-Client Support**
  - Unlimited simultaneous viewers
  - Efficient multicast distribution
  - No performance degradation

### System Tray Application

- **Minimal Footprint**
  - Runs quietly in system tray
  - Low memory usage (<50 MB)
  - Minimal CPU overhead

- **Quick Access Menu**
  - Start/Stop streaming
  - View stream directly
  - Copy RTSP URL
  - Access configuration

- **Status Notifications**
  - Connection status alerts
  - Stream health monitoring
  - Real-time weight display in menu

### Configuration & Management

- **Intuitive Settings Dialog**
  - Tabbed interface for easy navigation
  - Real-time connection testing
  - Settings validation

- **Overlay Controls**
  - Toggle date/time display on/off
  - Toggle stream rate display on/off
  - Toggle transmit indicator on/off
  - Custom label text field

- **Persistent Configuration**
  - Settings saved automatically
  - Survives system restarts
  - JSON-based for easy backup

- **Auto-Start Option**
  - Start streaming on Windows boot
  - Headless operation support
  - Service mode available

- **One-Click Setup**
  - Automatic dependency download
  - Self-configuring build system
  - No manual installation steps

### Reliability Features

- **Automatic Recovery**
  - Reconnects after scale disconnection
  - Restarts stream on encoder failure
  - Graceful error handling

- **Watchdog Monitoring**
  - Continuous health checks
  - Automatic process restart
  - Logging for diagnostics

- **Stable Operation**
  - 24/7 continuous operation
  - Memory leak prevention
  - Tested for extended runtime

---

## Technical Specifications

| Feature | Specification |
|---------|---------------|
| Platform | Windows 10/11 (64-bit) |
| Runtime | .NET 8.0 |
| Video Codec | H.264 (AVC) |
| Streaming Protocol | RTSP, HLS |
| Scale Protocol | Fairbanks 6011, ASCII-based |
| Connection Types | TCP/IP, RS232 Serial |
| Max Resolution | 1920x1080 |
| Max Frame Rate | 60 FPS |
| Latency | < 500ms typical |
| Memory Usage | < 50 MB |
| Disk Space | ~100 MB installed |

---

## Use Cases

### Quality Control
- Real-time weight verification
- Integration with production line cameras
- Audit trail documentation

### Shipping & Logistics
- Package weight verification
- Remote scale monitoring
- Multi-location weight tracking

### Agriculture
- Livestock weighing records
- Grain scale monitoring
- Harvest weight documentation

### Manufacturing
- Component weight verification
- Batch weight recording
- Process control integration

### Compliance & Legal
- Timestamped weight records
- Video evidence for disputes
- Regulatory compliance documentation

---

## Included Components

- Scale RTSP Streamer application
- FFmpeg video encoder (bundled)
- MediaMTX RTSP server (bundled)
- Configuration utility
- User documentation
- MSI installer package

---

## Support & Licensing

- One-year software updates included
- Email technical support
- Volume licensing available
- Enterprise customization options

---

*Scale RTSP Streamer - Transform Your Scale into a Smart Streaming Device*
