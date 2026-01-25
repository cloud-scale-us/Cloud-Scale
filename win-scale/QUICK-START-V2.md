# Scale Streamer v2.0 - Quick Start Guide

## Foundation Status

✅ **Core foundation is COMPLETE** (~30% of total project)

The following have been implemented:
- Universal protocol engine (JSON-based, no hardcoded protocols)
- Multi-scale connection manager
- SQLite database with automatic data retention
- Windows Service architecture
- Named Pipe IPC for Service ↔ GUI communication
- Base protocol implementations (TCP/IP complete)
- Three example protocol templates

**See V2-FOUNDATION-COMPLETE.md for full details**

---

## Quick Build & Test (Windows Only)

### Prerequisites

You need:
- **Windows 10/11**
- **.NET 8.0 SDK** (already installed at `$HOME/.dotnet`)
- **Visual Studio 2022** (recommended but optional for testing)

### Option 1: Build from Command Line

```bash
# Navigate to project directory
cd /mnt/d/win-scale/win-scale

# Restore NuGet packages
export PATH="$HOME/.dotnet:$PATH"
dotnet restore ScaleStreamer.sln

# Build all projects
dotnet build ScaleStreamer.sln --configuration Release

# Output will be in:
# src-v2/ScaleStreamer.Common/bin/Release/net8.0/
# src-v2/ScaleStreamer.Service/bin/Release/net8.0/
```

### Option 2: Build in Visual Studio

```bash
# On Windows, open solution
start ScaleStreamer.sln
```

Then:
1. Press **Ctrl+Shift+B** to build all projects
2. Check Output window for build results
3. Fix any missing references (shouldn't be any)

---

## Test the Service (Console Mode)

Before installing as Windows Service, test in console mode:

```bash
# Navigate to service directory
cd src-v2/ScaleStreamer.Service

# Run service in console mode
dotnet run
```

**Expected output**:
```
[12:34:56 INF] Scale Streamer Service starting...
[12:34:56 INF] Database initialized at: C:\ProgramData\ScaleStreamer\scalestreamer.db
[12:34:56 INF] Loading 3 protocol templates...
[12:34:56 INF] Loaded protocol: Fairbanks 6011
[12:34:56 INF] Loaded protocol: Generic ASCII
[12:34:56 INF] Loaded protocol: Modbus TCP Generic
[12:34:56 INF] Service ready for scale configuration via GUI
```

Press **Ctrl+C** to stop.

---

## Test Protocol Loading

Create a test file `test-protocol.cs`:

```csharp
using ScaleStreamer.Common.Models;
using ScaleStreamer.Common.Protocols;
using System.Text.Json;

// Load protocol from JSON
var json = File.ReadAllText("protocols/manufacturers/fairbanks-6011.json");
var protocol = JsonSerializer.Deserialize<ProtocolDefinition>(json);

Console.WriteLine($"Protocol: {protocol.ProtocolName}");
Console.WriteLine($"Manufacturer: {protocol.Manufacturer}");
Console.WriteLine($"Connection: {protocol.Connection.Type}");
Console.WriteLine($"Fields: {protocol.Parsing.Fields.Count}");

// Test parsing
var engine = new UniversalProtocolEngine(protocol);
var testData = "1 123456 0"; // Fairbanks format: STATUS WEIGHT TARE

var reading = engine.Parse(testData);
Console.WriteLine($"\nParsed weight: {reading.Weight} {reading.Unit}");
Console.WriteLine($"Status: {reading.Status}");
Console.WriteLine($"Tare: {reading.Tare}");
```

Run with:
```bash
dotnet script test-protocol.cs
```

---

## Test TCP Connection (Requires Hardware)

If you have a Fairbanks 6011 scale on network:

```csharp
using ScaleStreamer.Common.Protocols;
using System.Text.Json;

// Load protocol
var json = File.ReadAllText("protocols/manufacturers/fairbanks-6011.json");
var protocol = JsonSerializer.Deserialize<ProtocolDefinition>(json);

// Update IP address
protocol.Connection.Host = "192.168.1.100"; // Your scale IP
protocol.Connection.Port = 10001;

// Create adapter
var adapter = new UniversalProtocolAdapter(protocol);

// Subscribe to weight readings
adapter.WeightReceived += (sender, reading) =>
{
    Console.WriteLine($"{DateTime.Now:HH:mm:ss} - {reading.Weight} {reading.Unit} - {reading.Status}");
};

adapter.ErrorOccurred += (sender, error) =>
{
    Console.WriteLine($"ERROR: {error}");
};

// Connect
var connected = await adapter.ConnectAsync(protocol.Connection);
if (connected)
{
    Console.WriteLine("Connected! Reading weights for 30 seconds...");
    await adapter.StartContinuousReadingAsync();
    await Task.Delay(30000);
    await adapter.StopContinuousReadingAsync();
    Console.WriteLine("Disconnected.");
}
else
{
    Console.WriteLine("Connection failed!");
}
```

---

## Test Database Operations

```csharp
using ScaleStreamer.Common.Database;
using ScaleStreamer.Common.Models;

// Initialize database
var db = new DatabaseService("test.db");
await db.InitializeAsync();

// Insert test weight reading
var reading = new WeightReading
{
    ScaleId = "test-scale-1",
    Timestamp = DateTime.UtcNow,
    Weight = 1234.56,
    Unit = "lb",
    Status = ScaleStatus.Stable,
    IsValid = true
};

var id = await db.InsertWeightReadingAsync(reading);
Console.WriteLine($"Inserted reading with ID: {id}");

// Get statistics
var stats = await db.GetWeightStatisticsAsync(
    "test-scale-1",
    DateTime.UtcNow.AddHours(-1),
    DateTime.UtcNow);

Console.WriteLine($"Min: {stats.MinWeight}");
Console.WriteLine($"Max: {stats.MaxWeight}");
Console.WriteLine($"Avg: {stats.AverageWeight}");
Console.WriteLine($"Count: {stats.ReadingCount}");

// Log an event
await db.LogEventAsync("INFO", "Testing", "This is a test event");

// Get recent events
var events = await db.GetRecentEventsAsync(10);
foreach (var evt in events)
{
    Console.WriteLine($"[{evt.Timestamp}] {evt.Level}: {evt.Message}");
}
```

---

## Test IPC Communication

### Server Side (Service):

```csharp
using ScaleStreamer.Common.IPC;

var server = new IpcServer("TestPipe");

server.MessageReceived += (sender, message) =>
{
    Console.WriteLine($"Received: {message.MessageType}");

    // Send response
    var response = new IpcResponse
    {
        MessageType = IpcMessageType.StatusUpdate,
        Success = true,
        Payload = "Command received!"
    };
    server.SendResponseAsync(response);
};

server.Start();
Console.WriteLine("IPC Server started. Press Enter to stop...");
Console.ReadLine();
await server.StopAsync();
```

### Client Side (GUI):

```csharp
using ScaleStreamer.Common.IPC;

var client = new IpcClient("TestPipe");

client.MessageReceived += (sender, message) =>
{
    Console.WriteLine($"Response: {message.MessageType} - {message.Payload}");
};

var connected = await client.ConnectAsync();
if (connected)
{
    var command = new IpcCommand
    {
        MessageType = IpcMessageType.GetScaleStatus,
        Payload = "scale-1"
    };

    await client.SendCommandAsync(command);
    await Task.Delay(1000); // Wait for response
}
```

---

## Install as Windows Service

After testing in console mode, install as Windows Service:

```powershell
# Build in Release mode
dotnet publish src-v2/ScaleStreamer.Service/ScaleStreamer.Service.csproj -c Release -o publish/service

# Create Windows Service
sc.exe create ScaleStreamerService binPath="C:\path\to\publish\service\ScaleStreamer.Service.exe" start=auto

# Start service
sc.exe start ScaleStreamerService

# Check status
sc.exe query ScaleStreamerService

# View logs
Get-Content "$env:ProgramData\ScaleStreamer\logs\service-*.log" -Tail 50
```

To uninstall:
```powershell
sc.exe stop ScaleStreamerService
sc.exe delete ScaleStreamerService
```

---

## Project Structure Reference

```
win-scale/
├── ScaleStreamer.sln                    # Visual Studio solution
├── src-v2/
│   ├── ScaleStreamer.Common/            # ✅ Shared library
│   │   ├── Interfaces/                  # IScaleProtocol
│   │   ├── Models/                      # WeightReading, ProtocolDefinition, etc.
│   │   ├── Protocols/                   # Universal engine + adapters
│   │   ├── Database/                    # SQLite schema + service
│   │   ├── IPC/                         # Named Pipe communication
│   │   └── Utilities/                   # WeightConverter
│   ├── ScaleStreamer.Service/           # ✅ Windows Service
│   │   ├── Program.cs                   # Service host
│   │   ├── ScaleService.cs              # Main background service
│   │   └── ScaleConnectionManager.cs    # Multi-scale manager
│   └── ScaleStreamer.Config/            # ⏳ WinForms GUI (pending)
├── protocols/                           # ✅ JSON protocol templates
│   ├── manufacturers/
│   │   └── fairbanks-6011.json
│   └── generic/
│       ├── generic-ascii.json
│       └── modbus-tcp.json
└── docs/                                # ✅ Complete specifications
    ├── V2-EXECUTIVE-SUMMARY.md
    ├── V2-UNIVERSAL-ARCHITECTURE.md
    ├── V2-GUI-SPECIFICATION.md
    ├── V2-DEVELOPMENT-PLAN.md
    └── V2-FOUNDATION-COMPLETE.md
```

---

## Common Build Errors & Solutions

### Error: "The type or namespace name 'Microsoft' could not be found"

**Solution**: Restore NuGet packages:
```bash
dotnet restore
```

### Error: "Project file does not exist"

**Solution**: Ensure you're in the correct directory:
```bash
cd /mnt/d/win-scale/win-scale
```

### Error: "Access to path denied" (database)

**Solution**: Create directory with permissions:
```powershell
New-Item -ItemType Directory -Force -Path "$env:ProgramData\ScaleStreamer"
```

### Error: "Named pipe connection failed"

**Solution**: Ensure service is running:
```powershell
sc.exe query ScaleStreamerService
```

---

## Next Steps

### Immediate (Weeks 5-7): Build WinForms GUI

Create configuration interface with:
1. **Connection Tab** - Scale setup wizard
2. **Protocol Tab** - Visual protocol designer
3. **Monitoring Tab** - Real-time dashboard
4. **Logging Tab** - Event viewer

**Reference**: See `V2-GUI-SPECIFICATION.md` for complete field specifications

### Medium Term (Week 8): Add RTSP Streaming

Integrate FFmpeg and MediaMTX for video streaming with weight overlay

### Long Term (Weeks 9-12): Testing & Deployment

- Hardware testing with real scales
- Stress testing (7-day continuous run)
- Installer updates
- Documentation

---

## Getting Help

### Documentation

- **V2-FOUNDATION-COMPLETE.md** - What's been built
- **V2-UNIVERSAL-ARCHITECTURE.md** - Technical details (25 pages)
- **V2-GUI-SPECIFICATION.md** - GUI requirements (15 pages)
- **V2-DEVELOPMENT-PLAN.md** - 12-week roadmap

### Support Resources

- GitHub Issues: https://github.com/CNesbitt2025/Cloud-Scale/issues
- .NET Documentation: https://docs.microsoft.com/en-us/dotnet/
- SQLite Documentation: https://www.sqlite.org/docs.html

---

## Summary

✅ **Foundation is complete and ready for use**
- All core interfaces implemented
- Universal protocol engine working
- Database layer complete
- Windows Service architecture ready
- IPC communication system functional

⏳ **Next major milestone: WinForms GUI**
- Estimated 2-3 weeks of development
- All backend infrastructure ready to support GUI
- See V2-GUI-SPECIFICATION.md for requirements

🎯 **Target: 8-10 weeks to production-ready v2.0**

---

*Last updated: 2026-01-24*
*Foundation completion: ~30%*
