# Scale Streamer v2.0 - Configuration GUI Specification

**Universal Scale Configuration Interface**

---

## Connection Configuration Tab

### Layout

```
┌────────────────────────────────────────────────────────────────────┐
│ Connection Configuration                                      [?][X]│
├────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  Scale Information                                                  │
│  ┌───────────────────────────────────────────────────────────────┐ │
│  │ Scale Name:     [Shipping Scale #1                         ] │ │
│  │ Location:       [Warehouse - Dock A                        ] │ │
│  │ Scale ID:       [SCALE-001                                 ] │ │
│  │ Market Type:    [Floor Scales                         ▼]     │ │
│  └───────────────────────────────────────────────────────────────┘ │
│                                                                     │
│  Protocol Selection                                                 │
│  ┌───────────────────────────────────────────────────────────────┐ │
│  │ Manufacturer:   [Generic                              ▼]     │ │
│  │ Model:          [Custom Configuration                 ▼]     │ │
│  │ Protocol:       [ASCII - Continuous Stream            ▼]     │ │
│  │                                                               │ │
│  │ [Load from Template...] [Save as Template...] [Advanced...] │ │
│  └───────────────────────────────────────────────────────────────┘ │
│                                                                     │
│  Connection Type                                                    │
│  ┌───────────────────────────────────────────────────────────────┐ │
│  │ ● TCP/IP  ○ RS232  ○ RS485  ○ USB  ○ HTTP API  ○ Modbus     │ │
│  └───────────────────────────────────────────────────────────────┘ │
│                                                                     │
│  TCP/IP Settings                                                    │
│  ┌───────────────────────────────────────────────────────────────┐ │
│  │ IP Address:     [192.168.1.100                            ]   │ │
│  │ Port:           [10001        ]                               │ │
│  │ Timeout (ms):   [5000         ]                               │ │
│  │ Keepalive:      [☑] Send keepalive every [30] seconds        │ │
│  │ Auto-Reconnect: [☑] Retry every [10] seconds                 │ │
│  └───────────────────────────────────────────────────────────────┘ │
│                                                                     │
│  [< Back]  [Test Connection]  [Save Configuration]  [Next >]      │
│                                                                     │
└────────────────────────────────────────────────────────────────────┘
```

### Field Specifications

#### Market Type Dropdown
```
- Floor Scales (Warehouse, Shipping/Receiving)
- Truck Scales (Vehicle Weighing, WIM)
- Train Scales (Rail Car Weighing)
- Hopper Scales (Batch/Bulk Material)
- Conveyor Scales (Belt Scales)
- Checkweighers (Production Line)
- Medical Scales (Patient Weighing)
- Retail Scales (Point-of-Sale)
- Laboratory Balances (Precision)
- Livestock Scales (Animal Weighing)
- Agriculture Scales (Grain, Feed)
- Waste/Recycling Scales
- Custom/Other
```

#### Manufacturer Dropdown
```
- Generic (No specific manufacturer)
───────────────────────────
- Fairbanks Scales
- Rice Lake Weighing Systems
- Cardinal Scale
- Avery Weigh-Tronix
- Mettler Toledo
- TRANSCELL
- Arlyn Scales
- Ohaus
- Doran Scales
- Brecknell Scales
───────────────────────────
- Custom/Other Manufacturer
```

#### Protocol Dropdown (changes based on manufacturer)

**When Manufacturer = "Generic":**
```
- ASCII - Continuous Stream
- ASCII - Demand/Request Response
- ASCII - Custom Format
- Modbus RTU (Serial)
- Modbus TCP (Ethernet)
- NTEP Continuous Mode
- NTEP Demand Mode
- HTTP REST API (JSON)
- HTTP REST API (XML)
- SOAP Web Service
- Custom Binary Protocol
```

**When Manufacturer = "Fairbanks":**
```
- Fairbanks 6011 (Standard ASCII)
- Fairbanks Modbus RTU
- Fairbanks Modbus TCP
- Fairbanks Custom Protocol
```

**When Manufacturer = "Rice Lake":**
```
- Rice Lake Lantronix (TCP/IP Serial)
- Rice Lake 920i Indicator
- Rice Lake Modbus TCP
- Rice Lake EtherNet/IP
```

**When Manufacturer = "Cardinal":**
```
- Cardinal ASCII (Standard)
- Cardinal 201 Transmitter
- Cardinal Modbus RTU
- Cardinal EtherNet/IP
```

#### Connection Type (Radio Buttons)

**TCP/IP** - Shows:
```
IP Address:      [___.___.___.___ ]
Port:            [________]
Timeout (ms):    [________]
Keepalive:       [☐] Every [__] seconds
Auto-Reconnect:  [☐] Every [__] seconds
```

**RS232** - Shows:
```
COM Port:        [COM1 ▼] (Auto-detected: COM1, COM3, COM4)
Baud Rate:       [9600 ▼] (300, 1200, 2400, 4800, 9600, 19200, 38400, 57600, 115200)
Data Bits:       [8 ▼] (7, 8)
Parity:          [None ▼] (None, Even, Odd, Mark, Space)
Stop Bits:       [1 ▼] (1, 1.5, 2)
Flow Control:    [None ▼] (None, Hardware, Software)
```

**RS485** - Shows (extends RS232):
```
COM Port:        [COM1 ▼]
Baud Rate:       [9600 ▼]
Data Bits:       [8 ▼]
Parity:          [None ▼]
Stop Bits:       [1 ▼]
Device Address:  [1   ]
Mode:            [Half Duplex ▼] (Half Duplex, Full Duplex)
Termination:     [☐] Enable 120Ω termination resistor
```

**USB** - Shows:
```
USB Device:      [Detect Devices]
                 ┌─────────────────────────────────────────┐
                 │ Found:                                  │
                 │ ○ USB Serial Port (COM5) - FTDI         │
                 │ ○ USB HID Device - Scale Interface      │
                 └─────────────────────────────────────────┘
Vendor ID:       [0x0403]
Product ID:      [0x6001]
Interface:       [Serial Emulation ▼] (Serial, HID, Custom)
```

**HTTP API** - Shows:
```
URL:             [https://api.example.com/scales/123/weight]
Method:          [GET ▼] (GET, POST, PUT)
Authentication:  [API Key ▼] (None, API Key, Bearer Token, Basic Auth, OAuth2)
  API Key Header: [X-API-Key]
  API Key Value:  [********************]
Poll Interval:   [1000] ms
Timeout:         [5000] ms
```

**Modbus** - Shows:
```
Connection:      [Modbus TCP ▼] (Modbus RTU, Modbus TCP)

[If Modbus TCP]
  IP Address:    [192.168.1.100]
  Port:          [502]

[If Modbus RTU]
  COM Port:      [COM1 ▼]
  Baud Rate:     [9600 ▼]

Unit ID:         [1   ]
Register Type:   [Holding Registers ▼] (Coils, Discrete Inputs, Input Registers, Holding Registers)
Start Address:   [0    ]
Register Count:  [2    ]
Data Format:     [Float32 Big-Endian ▼]
  └─ Int16, UInt16, Int32, UInt32, Float32 BE/LE, Float64 BE/LE
Poll Interval:   [100] ms
```

---

## Protocol Configuration Tab

```
┌────────────────────────────────────────────────────────────────────┐
│ Protocol Configuration                                        [?][X]│
├────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  Live Data Preview                                                  │
│  ┌───────────────────────────────────────────────────────────────┐ │
│  │ ┌─────────────────────────────────────────────────────────────┴┐ │
│  │ │ Status: [●] Connected  |  Data Rate: 10 readings/sec       │ │
│  │ └─────────────────────────────────────────────────────────────┬┘ │
│  │                                                               │ │
│  │  Raw Data Stream:                                             │ │
│  │  ┌─────────────────────────────────────────────────────────┐ │ │
│  │  │ 1   00123450  00000                                     │ │ │
│  │  │ 1   00123460  00000                                     │ │ │
│  │  │ 1   00123470  00000    ← Scrolling live data            │ │ │
│  │  └─────────────────────────────────────────────────────────┘ │ │
│  │                                                               │ │
│  │  [Pause] [Clear] [Export to File...]                         │ │
│  └───────────────────────────────────────────────────────────────┘ │
│                                                                     │
│  Data Format                                                        │
│  ┌───────────────────────────────────────────────────────────────┐ │
│  │ Encoding:       [ASCII ▼]                                     │ │
│  │                  └─ ASCII, UTF-8, UTF-16, Binary, Hex         │ │
│  │                                                                │ │
│  │ Data Mode:      [Continuous Stream ▼]                         │ │
│  │                  └─ Continuous, Demand/Request, Event-Driven  │ │
│  │                                                                │ │
│  │ Line Delimiter: [CR+LF (\\r\\n) ▼]                              │ │
│  │                  └─ CR+LF, LF, CR, Custom...                  │ │
│  │                                                                │ │
│  │ Field Separator: [Whitespace ▼]                               │ │
│  │                   └─ Whitespace, Tab, Comma, Semicolon,       │ │
│  │                      Fixed Width, Custom Regex...             │ │
│  └───────────────────────────────────────────────────────────────┘ │
│                                                                     │
│  Field Mapping                                                      │
│  ┌───────────────────────────────────────────────────────────────┐ │
│  │ ┌─────────────────────────────────────────────────────────┐   │ │
│  │ │ Field       │ Position │ Type    │ Unit │ Multiplier   │   │ │
│  │ ├─────────────────────────────────────────────────────────┤   │ │
│  │ │ ☑ Status    │ 0        │ Integer │ -    │ 1.0          │   │ │
│  │ │ ☑ Weight    │ 1        │ Float   │ lb   │ 0.01         │   │ │
│  │ │ ☑ Tare      │ 2        │ Float   │ lb   │ 0.01         │   │ │
│  │ │ ☐ Gross     │ -        │ -       │ -    │ -            │   │ │
│  │ │ ☐ Timestamp │ -        │ -       │ -    │ -            │   │ │
│  │ └─────────────────────────────────────────────────────────┘   │ │
│  │                                                                │ │
│  │  [Add Field] [Remove] [Edit Mapping...] [Use Regex Parser]   │ │
│  └───────────────────────────────────────────────────────────────┘ │
│                                                                     │
│  Parsed Values (Live):                                              │
│  ┌───────────────────────────────────────────────────────────────┐ │
│  │ Weight:  1234.70 lb          Tare: 0.00 lb                    │ │
│  │ Status:  Stable (1)          Gross: 1234.70 lb (calculated)   │ │
│  └───────────────────────────────────────────────────────────────┘ │
│                                                                     │
│  [< Back]  [Test Parsing]  [Save Configuration]  [Next >]         │
│                                                                     │
└────────────────────────────────────────────────────────────────────┘
```

### Field Specifications

#### Encoding Dropdown
```
- ASCII (7-bit, US-ASCII)
- UTF-8 (8-bit Unicode)
- UTF-16 LE (Little-Endian)
- UTF-16 BE (Big-Endian)
- Windows-1252 (Latin-1)
- ISO-8859-1
- Binary (Raw bytes)
- Hexadecimal (Hex string)
- Base64 Encoded
```

#### Data Mode Dropdown
```
- Continuous Stream (scale constantly sends data)
- Demand/Request (send request, receive response)
- Event-Driven (scale sends on weight change)
- Polled (periodic request at interval)
```

#### Line Delimiter Dropdown
```
- CR+LF (\\r\\n) - Windows standard
- LF (\\n) - Unix standard
- CR (\\r) - Mac Classic
- NULL (\\0) - C-style string
- Custom Character... (opens input dialog)
- Fixed Length (no delimiter, X bytes per message)
- Start/End Markers (STX/ETX, custom bytes)
```

#### Field Separator Dropdown
```
- Whitespace (any spaces/tabs)
- Tab (\\t)
- Comma (,)
- Semicolon (;)
- Pipe (|)
- Fixed Width (specify column positions)
- Regex Pattern (advanced users)
- JSON (use JSON path)
- XML (use XPath)
```

#### Field Type Dropdown
```
- String (text)
- Integer (whole number)
- Float (decimal number)
- Double (high-precision decimal)
- Boolean (true/false, 0/1)
- Datetime (timestamp)
- Hex String (0xFF format)
- Custom...
```

#### Unit Dropdown
```
Weight Units:
- lb (pounds)
- kg (kilograms)
- oz (ounces)
- g (grams)
- t (metric tons)
- ton (US tons)
- mg (milligrams)
- μg (micrograms)
- ct (carats)
- dwt (pennyweight)

Other:
- % (percentage)
- count (piece count)
- custom... (enter unit)
```

---

## Advanced Protocol Editor Dialog

```
┌────────────────────────────────────────────────────────────────────┐
│ Advanced Protocol Configuration                               [?][X]│
├────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  Parsing Method: ● Field Position  ○ Regular Expression  ○ JSON    │
│                                                                     │
│  ┌─ Field Position Parser ────────────────────────────────────────┐ │
│  │                                                                 │ │
│  │ Sample Data: [1   00123450  00000                          ]   │ │
│  │                                                                 │ │
│  │ Field: [Weight ▼]                                              │ │
│  │                                                                 │ │
│  │ Extraction:                                                     │ │
│  │   Start Position: [4    ] (0-indexed)                          │ │
│  │   Length:         [8    ] characters                           │ │
│  │                                                                 │ │
│  │ Processing:                                                     │ │
│  │   Trim Whitespace: [☑]                                         │ │
│  │   Data Type:       [Integer ▼]                                 │ │
│  │   Multiplier:      [0.01    ] (scale factor)                   │ │
│  │   Offset:          [0.00    ] (add after multiply)             │ │
│  │                                                                 │ │
│  │ Result: [1234.50]                                              │ │
│  │                                                                 │ │
│  └─────────────────────────────────────────────────────────────────┘ │
│                                                                     │
│  ┌─ Regular Expression Parser ────────────────────────────────────┐ │
│  │                                                                 │ │
│  │ Pattern: [(?<status>\\d)\\s+(?<weight>\\d+)\\s+(?<tare>\\d+)    ] │ │
│  │                                                                 │ │
│  │ Named Groups:                                                   │ │
│  │   ☑ status  → Status field                                     │ │
│  │   ☑ weight  → Weight field (multiply by 0.01)                  │ │
│  │   ☑ tare    → Tare field (multiply by 0.01)                    │ │
│  │                                                                 │ │
│  │ [Test Regex] [Regex Help] [Common Patterns...]                 │ │
│  │                                                                 │ │
│  └─────────────────────────────────────────────────────────────────┘ │
│                                                                     │
│  ┌─ JSON Parser ──────────────────────────────────────────────────┐ │
│  │                                                                 │ │
│  │ Sample JSON:                                                    │ │
│  │ ┌───────────────────────────────────────────────────────────┐ │ │
│  │ │ {                                                         │ │ │
│  │ │   "weight": {"value": 1234.5, "unit": "lb"},             │ │ │
│  │ │   "status": "stable"                                      │ │ │
│  │ │ }                                                         │ │ │
│  │ └───────────────────────────────────────────────────────────┘ │ │
│  │                                                                 │ │
│  │ Field Mappings:                                                 │ │
│  │   Weight:  [$.weight.value    ] → 1234.5                       │ │
│  │   Unit:    [$.weight.unit     ] → "lb"                         │ │
│  │   Status:  [$.status          ] → "stable"                     │ │
│  │                                                                 │ │
│  │ [Validate JSON] [JSONPath Help]                                │ │
│  │                                                                 │ │
│  └─────────────────────────────────────────────────────────────────┘ │
│                                                                     │
│  [OK] [Cancel] [Test with Live Data]                               │
│                                                                     │
└────────────────────────────────────────────────────────────────────┘
```

---

## Validation Rules Tab

```
┌────────────────────────────────────────────────────────────────────┐
│ Validation & Processing Rules                                 [?][X]│
├────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  Weight Validation                                                  │
│  ┌───────────────────────────────────────────────────────────────┐ │
│  │ Minimum Weight: [0.00      ] lb  (reject below)               │ │
│  │ Maximum Weight: [50000.00  ] lb  (reject above)               │ │
│  │                                                                │ │
│  │ Zero Detection:                                                │ │
│  │   ☑ Treat values below [0.10] lb as zero                      │ │
│  │                                                                │ │
│  │ Stability:                                                     │ │
│  │   ☑ Require stable reading                                    │ │
│  │   Definition: Weight unchanged for [0.5] seconds              │ │
│  │   Tolerance: ± [0.05] lb                                      │ │
│  └───────────────────────────────────────────────────────────────┘ │
│                                                                     │
│  Data Filtering                                                     │
│  ┌───────────────────────────────────────────────────────────────┐ │
│  │ Outlier Detection:                                             │ │
│  │   ☑ Reject sudden changes > [10.0] lb/second                  │ │
│  │                                                                │ │
│  │ Smoothing:                                                     │ │
│  │   ☑ Apply moving average filter                               │ │
│  │   Window size: [5] readings                                   │ │
│  │                                                                │ │
│  │ Rate Limiting:                                                 │ │
│  │   ☑ Maximum update rate: [10] readings/second                 │ │
│  └───────────────────────────────────────────────────────────────┘ │
│                                                                     │
│  Unit Conversion                                                    │
│  ┌───────────────────────────────────────────────────────────────┐ │
│  │ Input Unit:  [lb (from scale) ▼]                              │ │
│  │ Output Unit: [lb (display)    ▼]                              │ │
│  │                                                                │ │
│  │ Conversion: 1.0000 (no conversion)                            │ │
│  │                                                                │ │
│  │ Decimal Places: [2 ▼] (0, 1, 2, 3, 4, 5, 6)                  │ │
│  └───────────────────────────────────────────────────────────────┘ │
│                                                                     │
│  [< Back]  [Reset to Defaults]  [Save Rules]  [Next >]            │
│                                                                     │
└────────────────────────────────────────────────────────────────────┘
```

---

## Summary of All Dropdowns

### Connection Tab
1. **Market Type**: 13 options (Floor, Truck, Train, etc.)
2. **Manufacturer**: 15+ manufacturers + Generic
3. **Protocol**: Dynamic based on manufacturer (5-15 options)
4. **Connection Type**: 6 radio options (TCP/IP, RS232, RS485, USB, HTTP, Modbus)
5. **COM Port**: Auto-detected (COM1-COM20+)
6. **Baud Rate**: 9 standard rates (300-115200)
7. **Data Bits**: 2 options (7, 8)
8. **Parity**: 5 options (None, Even, Odd, Mark, Space)
9. **Stop Bits**: 3 options (1, 1.5, 2)
10. **Flow Control**: 3 options (None, Hardware, Software)
11. **HTTP Method**: 3 options (GET, POST, PUT)
12. **Authentication**: 5 options (None, API Key, Bearer, Basic, OAuth2)

### Protocol Tab
13. **Encoding**: 9 options (ASCII, UTF-8, Binary, Hex, etc.)
14. **Data Mode**: 4 options (Continuous, Demand, Event-Driven, Polled)
15. **Line Delimiter**: 7+ options (CR+LF, LF, CR, NULL, Custom, etc.)
16. **Field Separator**: 9 options (Whitespace, Tab, Comma, Regex, JSON, etc.)
17. **Field Type**: 8 options (String, Integer, Float, Boolean, etc.)
18. **Unit**: 15+ weight units + custom

### Validation Tab
19. **Output Unit**: Same as input unit dropdown
20. **Decimal Places**: 7 options (0-6)

---

**Total Interactive Elements**: 100+ fields, dropdowns, checkboxes, and buttons

**Next Document**: Will create the Windows Service implementation specification.

---

**Document Version**: 1.0
**Date**: 2026-01-24
**Status**: UI/UX Specification
