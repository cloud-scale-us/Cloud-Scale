# Scale RTSP Streamer

A Windows system tray application that reads weight data from a Fairbanks 6011 scale (via TCP/IP or RS232) and streams it as an RTSP video feed.

## Features

- **TCP/IP and RS232 Support**: Connect to your scale via network or serial port
- **Real-time RTSP Streaming**: Stream weight display as H.264 video
- **System Tray Application**: Runs quietly in the background
- **Configurable Display**: Customize title, unit, colors, and overlays
- **Date/Time Overlay**: Real-time timestamp (toggle on/off)
- **Stream Rate Display**: Shows current bitrate in KB/s
- **Transmit Indicator**: Green blinking [TX] indicator
- **Custom Label**: Add your own on-screen label
- **Auto-reconnect**: Automatically reconnects if connection is lost
- **HLS Support**: View stream in web browser

## Requirements

- Windows 10/11 (64-bit)
- .NET 8.0 Runtime (included in installer)
- Fairbanks 6011 scale (or compatible)

## Installation

### Option 1: MSI Installer (Recommended)
1. Download `ScaleStreamer-Setup.msi`
2. Run the installer
3. Launch from Start Menu

### Option 2: One-Click Setup (Recommended for Development)
1. Install .NET 8.0 SDK
2. Run the setup script (auto-downloads dependencies and builds):
   ```powershell
   .\setup.ps1
   ```
   The application will launch automatically after setup.

### Option 3: Manual Build
1. Install .NET 8.0 SDK
2. Run the build script:
   ```powershell
   cd scripts
   .\build.ps1 -Release
   ```

## Configuration

Right-click the system tray icon and select "Configure..." to open the settings dialog.

### Connection Settings

#### TCP/IP Mode
- **Host**: IP address of the scale (e.g., `10.1.10.210`)
- **Port**: TCP port (default: `5001`)

#### Serial Mode
- **Port**: COM port (e.g., `COM1`)
- **Baud Rate**: Communication speed (default: `9600`)

### Stream Settings
- **RTSP Port**: Port for RTSP streaming (default: `8554`)
- **Resolution**: Video resolution (default: `640x480`)
- **Frame Rate**: Frames per second (default: `30`)

### Display Settings
- **Title**: Text shown at top of stream (default: `FAIRBANKS 6011`)
- **Unit**: Weight unit label (LB, KG, OZ, G)
- **Custom Label**: Optional text shown at bottom of stream
- **Show Date/Time**: Enable/disable timestamp overlay (top right)
- **Show Stream Rate**: Enable/disable bitrate display in KB/s (top left)
- **Show Transmit Indicator**: Enable/disable blinking [TX] indicator (bottom right)

## Usage

### Starting the Stream
1. Right-click the tray icon
2. Select "Start Stream"

### Viewing the Stream

#### In VLC Player
```
rtsp://127.0.0.1:8554/scale
```

#### In Web Browser (HLS)
```
http://127.0.0.1:8888/scale/
```

#### Copy URL
Right-click tray icon → "Copy RTSP URL"

### Connecting NVR
Use the RTSP URL in your NVR software:
```
rtsp://<computer-ip>:8554/scale
```

## Scale Protocol

The application supports the Fairbanks 6011 scale protocol:
- Data format: `STATUS  WEIGHT  TARE`
- Example: `1   44140    00`
- Line endings: CR, LF, or CRLF
- May include STX/ETX control characters

## Troubleshooting

### "Connection failed"
- Verify scale IP address and port
- Check firewall settings
- Ensure scale is powered on and connected

### "No weight data received"
- Check scale is transmitting data
- Verify baud rate (for serial connection)
- Try the "Test Connection" button in settings

### Stream not visible in VLC
- Ensure RTSP port is not blocked by firewall
- Try TCP transport: `rtsp-tcp://127.0.0.1:8554/scale`

## Building the Installer

1. Install WiX Toolset:
   ```powershell
   dotnet tool install -g wix
   ```

2. Build with installer:
   ```powershell
   .\scripts\build.ps1 -Release -CreateInstaller
   ```

## Project Structure

```
win-scale/
├── src/ScaleStreamer/     # Main application source
│   ├── App/               # UI components
│   ├── Core/              # Business logic
│   └── Config/            # Configuration
├── deps/                  # Dependencies (ffmpeg, MediaMTX)
├── installer/             # WiX installer files
├── scripts/               # Build scripts
└── publish/               # Build output
```

## License

MIT License - See LICENSE file

## Credits

- [ffmpeg](https://ffmpeg.org/) - Video encoding
- [MediaMTX](https://github.com/bluenviron/mediamtx) - RTSP server
