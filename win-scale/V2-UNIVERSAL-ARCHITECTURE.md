# Universal Scale Integration Platform - Architecture v2.0

**Cloud-Scale: Platform-Agnostic Industrial Weighing System**

---

## Philosophy

Version 2.0 is a **blank canvas** - a universal platform for industrial scale integration, not tied to any specific:
- Manufacturer (Fairbanks, Rice Lake, Cardinal, etc.)
- Protocol (6011, Modbus, ASCII, etc.)
- Market (floor scales, truck scales, train scales, etc.)
- Deployment (single-scale, multi-scale, enterprise)

### Design Principles

1. **Protocol Independence**: No hardcoded assumptions about data formats
2. **Manufacturer Neutrality**: Equal support for all vendors
3. **Configuration-Driven**: Everything defined by user/admin, not code
4. **Extensibility**: Easy to add new protocols without code changes
5. **Commercial Ready**: Multi-tenant, licensing, white-label capable

---

## Universal Protocol Engine

### Instead of hardcoded parsers, use:

```csharp
public class UniversalScaleProtocol
{
    // User configures these via GUI:
    private ProtocolDefinition _definition;

    public class ProtocolDefinition
    {
        // Connection
        public ConnectionType Type { get; set; } // TCP, Serial, USB, HTTP
        public Dictionary<string, string> ConnectionParams { get; set; }

        // Data Extraction
        public DataFormat Format { get; set; } // ASCII, Binary, JSON, XML
        public List<DataField> Fields { get; set; }

        // Parsing Rules
        public string StartDelimiter { get; set; }
        public string EndDelimiter { get; set; }
        public string FieldSeparator { get; set; }
        public Dictionary<string, string> RegexPatterns { get; set; }

        // Validation
        public List<ValidationRule> ValidationRules { get; set; }
    }

    public class DataField
    {
        public string Name { get; set; } // "Weight", "Tare", "Unit", "Status"
        public int Position { get; set; } // For fixed-width formats
        public string Regex { get; set; } // For regex extraction
        public string JSONPath { get; set; } // For JSON formats
        public string XPath { get; set; } // For XML formats
        public DataType Type { get; set; } // Float, Integer, String, Boolean
        public string Unit { get; set; } // LB, KG, OZ, G, T
        public double? Multiplier { get; set; } // Convert internal units
    }
}
```

### Example Configuration (JSON)

**Fairbanks 6011:**
```json
{
  "protocol_name": "Fairbanks 6011",
  "connection": {
    "type": "tcp",
    "host": "192.168.1.100",
    "port": 10001
  },
  "data_format": "ascii",
  "parsing": {
    "line_delimiter": "\r\n",
    "field_separator": "\\s+",
    "fields": [
      {
        "name": "status",
        "position": 0,
        "type": "integer",
        "mapping": {
          "1": "stable",
          "2": "motion",
          "3": "overload"
        }
      },
      {
        "name": "weight",
        "position": 1,
        "type": "float",
        "multiplier": 0.01,
        "unit": "lb"
      },
      {
        "name": "tare",
        "position": 2,
        "type": "float",
        "multiplier": 0.01,
        "unit": "lb"
      }
    ]
  },
  "validation": {
    "min_weight": 0,
    "max_weight": 50000,
    "require_stable": false
  }
}
```

**Generic Modbus TCP:**
```json
{
  "protocol_name": "Modbus TCP Generic",
  "connection": {
    "type": "modbus_tcp",
    "host": "192.168.1.50",
    "port": 502,
    "unit_id": 1
  },
  "data_format": "modbus_registers",
  "parsing": {
    "fields": [
      {
        "name": "weight",
        "register_address": 0,
        "register_count": 2,
        "data_type": "float32_be",
        "unit": "kg"
      },
      {
        "name": "status",
        "register_address": 2,
        "register_count": 1,
        "data_type": "uint16",
        "bit_masks": {
          "stable": 0x01,
          "overload": 0x02,
          "underload": 0x04
        }
      }
    ]
  }
}
```

**Rice Lake Lantronix (Serial over TCP):**
```json
{
  "protocol_name": "Rice Lake 920i",
  "connection": {
    "type": "tcp",
    "host": "192.168.1.75",
    "port": 10001
  },
  "data_format": "ascii",
  "mode": "continuous",
  "parsing": {
    "regex": "(?<status>[A-Z])\\s+(?<weight>[0-9.]+)\\s+(?<unit>[A-Z]+)",
    "fields": [
      {
        "name": "status",
        "regex_group": "status",
        "type": "string"
      },
      {
        "name": "weight",
        "regex_group": "weight",
        "type": "float"
      },
      {
        "name": "unit",
        "regex_group": "unit",
        "type": "string"
      }
    ]
  }
}
```

**REST API Scale:**
```json
{
  "protocol_name": "Cloud Scale API",
  "connection": {
    "type": "http",
    "url": "https://api.example.com/scales/123/weight",
    "method": "GET",
    "headers": {
      "Authorization": "Bearer {api_key}"
    }
  },
  "data_format": "json",
  "polling_interval_ms": 1000,
  "parsing": {
    "fields": [
      {
        "name": "weight",
        "json_path": "$.data.weight.value",
        "type": "float"
      },
      {
        "name": "unit",
        "json_path": "$.data.weight.unit",
        "type": "string"
      },
      {
        "name": "timestamp",
        "json_path": "$.data.timestamp",
        "type": "datetime"
      }
    ]
  }
}
```

---

## Protocol Library System

### Built-in Protocol Templates

**Shipping with v2.0:**

```
win-scale/protocols/
├── manufacturers/
│   ├── fairbanks.json           # Fairbanks 6011
│   ├── rice-lake.json           # Lantronix
│   ├── cardinal.json            # Cardinal ASCII
│   ├── mettler-toledo.json      # MT-SICS
│   ├── avery-weigh-tronix.json  # AWI
│   └── arlyn.json               # Arlyn SAW
│
├── generic/
│   ├── modbus-rtu.json          # Generic Modbus RTU
│   ├── modbus-tcp.json          # Generic Modbus TCP
│   ├── ntep-continuous.json     # NTEP continuous mode
│   ├── ntep-demand.json         # NTEP demand mode
│   ├── generic-ascii.json       # Configurable ASCII
│   └── rest-api.json            # HTTP REST API template
│
└── industry/
    ├── truck-scale-wim.json     # Weigh-in-Motion
    ├── train-scale.json         # Rail scales
    ├── hopper-scale.json        # Batch weighing
    └── conveyor-scale.json      # Belt scales
```

### User-Created Protocols

Users can create custom protocols:

1. **Via GUI**: Protocol Designer wizard
2. **Via JSON**: Edit template and import
3. **Via Marketplace**: Download from community/vendor

**Custom protocols stored in:**
```
%ProgramData%\Cloud-Scale\ScaleStreamer\protocols\custom\
```

---

## Multi-Market Configuration

### Market Profiles

Instead of one configuration, support **market-specific deployments**:

```csharp
public enum Market
{
    FloorScales,        // Warehouse, shipping/receiving
    TruckScales,        // Vehicle weighing, WIM
    TrainScales,        // Rail car weighing
    Hoppers,            // Batch/bulk material
    Conveyors,          // Belt scales
    Checkweighers,      // Production line
    Medical,            // Patient weighing
    Retail,             // Point-of-sale
    Laboratory,         // Precision balances
    Livestock,          // Animal weighing
    Agriculture,        // Grain, feed
    Waste,              // Refuse, recycling
    Custom              // User-defined
}

public class MarketProfile
{
    public Market Type { get; set; }
    public DisplayTemplate DefaultDisplay { get; set; }
    public List<string> RequiredFields { get; set; }
    public List<AlertRule> DefaultAlerts { get; set; }
    public ReportingTemplate DefaultReports { get; set; }
}
```

### Market-Specific Features

**Truck Scales:**
- Vehicle ID capture (license plate, RFID)
- Axle weight distribution
- WIM continuous monitoring
- Overload alerts
- DOT compliance reporting

**Floor Scales:**
- Piece counting
- Checkweighing (min/max)
- Totalization
- SKU tracking

**Train Scales:**
- Car number tracking
- Cumulative train weight
- Per-car reports
- Slow-speed weighing

**Laboratory:**
- High precision display (0.0001g)
- Calibration tracking
- Environmental compensation
- GLP compliance

---

## White-Label & Multi-Tenant Architecture

### White-Label Support

Allow system integrators to rebrand:

```csharp
public class BrandingConfiguration
{
    public string CompanyName { get; set; }
    public string ProductName { get; set; }
    public string LogoPath { get; set; }
    public ColorScheme Colors { get; set; }
    public string SupportURL { get; set; }
    public string SupportEmail { get; set; }
    public LicenseInfo License { get; set; }
}
```

**Customizable:**
- Company name/logo
- Color scheme
- Support links
- About dialog
- Installer branding
- Video overlays

### Multi-Tenant Licensing

```csharp
public class LicenseManager
{
    public enum LicenseType
    {
        Trial,              // 30-day trial, 1 scale
        Standard,           // 1-5 scales, basic features
        Professional,       // 6-50 scales, advanced features
        Enterprise,         // Unlimited scales, all features
        OEM                 // White-label, no branding
    }

    public class License
    {
        public LicenseType Type { get; set; }
        public int MaxScales { get; set; }
        public DateTime ExpirationDate { get; set; }
        public List<Feature> EnabledFeatures { get; set; }
        public string LicensedTo { get; set; }
        public string LicenseKey { get; set; }
    }
}
```

**Feature Flags:**
```csharp
public enum Feature
{
    BasicStreaming,
    MultipleScales,
    CloudSync,
    AdvancedAnalytics,
    CustomProtocols,
    RestAPI,
    MobileApp,
    RemoteMonitoring,
    EmailAlerts,
    DatabaseExport,
    CustomReports,
    WhiteLabel
}
```

---

## Deployment Scenarios

### Scenario 1: Single Floor Scale (Basic)

**License**: Standard
**Hardware**: 1 floor scale, 1 Windows PC
**Protocol**: Generic ASCII via RS232
**Use Case**: Warehouse shipping/receiving

**Configuration**:
```json
{
  "deployment": "single_scale",
  "scale": {
    "name": "Shipping Scale",
    "location": "Dock A",
    "protocol": "generic-ascii",
    "connection": "COM1:9600,8,N,1"
  },
  "video": {
    "resolution": "1280x720",
    "overlay": "simple"
  }
}
```

### Scenario 2: Truck Scale with WIM (Professional)

**License**: Professional
**Hardware**: 1 truck scale, 1 industrial PC
**Protocol**: Fairbanks WIM via TCP/IP
**Use Case**: Aggregate quarry, continuous truck monitoring

**Configuration**:
```json
{
  "deployment": "truck_scale",
  "scale": {
    "name": "Quarry Truck Scale",
    "type": "wim",
    "protocol": "fairbanks-wim",
    "connection": "tcp://192.168.1.100:10001"
  },
  "capture": {
    "vehicle_id": true,
    "axle_weights": true,
    "license_plate_camera": "rtsp://192.168.1.200/stream"
  },
  "alerts": [
    {
      "type": "overload",
      "threshold": 80000,
      "action": "email"
    }
  ]
}
```

### Scenario 3: Multi-Scale Enterprise (Enterprise)

**License**: Enterprise
**Hardware**: 20 scales across 3 facilities
**Protocols**: Mixed (Modbus TCP, Rice Lake, Cardinal)
**Use Case**: Distribution center network

**Configuration**:
```json
{
  "deployment": "enterprise",
  "facilities": [
    {
      "name": "Warehouse East",
      "scales": [
        {
          "id": "WE-01",
          "name": "Receiving Scale 1",
          "protocol": "modbus-tcp",
          "connection": "tcp://10.1.1.50:502"
        },
        {
          "id": "WE-02",
          "name": "Shipping Scale 1",
          "protocol": "cardinal",
          "connection": "tcp://10.1.1.51:10001"
        }
      ]
    }
  ],
  "central_database": {
    "enabled": true,
    "type": "mssql",
    "connection_string": "..."
  },
  "cloud_sync": {
    "enabled": true,
    "endpoint": "https://api.cloud-scale.us/sync"
  }
}
```

### Scenario 4: OEM Integration (OEM License)

**License**: OEM (White-Label)
**Hardware**: Integrated with custom industrial equipment
**Protocol**: Custom proprietary
**Use Case**: System integrator embedding in larger system

**Configuration**:
```json
{
  "deployment": "oem_embedded",
  "branding": {
    "company": "Acme Industrial Systems",
    "product": "Acme Process Controller",
    "hide_cloud_scale_branding": true
  },
  "scale": {
    "protocol": "custom",
    "protocol_definition": "file://protocols/acme-custom.json"
  },
  "integration": {
    "api_mode": true,
    "rest_api_port": 8080,
    "webhook_url": "http://localhost:9000/weight-update"
  }
}
```

---

## Configuration GUI - Universal Approach

### Protocol Configuration Wizard

**Step 1: Choose Scale Type**
- Select manufacturer (or "Generic")
- Select model (loads protocol template)
- OR: "Create custom protocol"

**Step 2: Connection Settings**
- Auto-detected based on protocol type
- TCP/IP: Host, Port
- Serial: COM port, Baud, Parity, etc.
- USB: Auto-enumerate
- HTTP: URL, Auth

**Step 3: Data Mapping**
- Live data preview window
- Highlight fields in raw data
- Click to map: Weight, Tare, Unit, Status
- Auto-detect common patterns

**Step 4: Validation Rules**
- Min/max weight
- Stability detection
- Unit conversion
- Custom formulas

**Step 5: Test & Save**
- Live connection test
- Save as protocol template
- Name and describe for reuse

### Visual Protocol Designer

```
┌─────────────────────────────────────────────────────────┐
│ Protocol Designer                                  [?][X]│
├─────────────────────────────────────────────────────────┤
│                                                          │
│  Raw Data Preview:                                       │
│  ┌────────────────────────────────────────────────────┐ │
│  │ 1   00123450  00000                                │ │
│  │ 1   00123460  00000                                │ │
│  │ 1   00123470  00000         ← Live data from scale │
│  └────────────────────────────────────────────────────┘ │
│                                                          │
│  Field Mapping:                                          │
│  ┌────────────────────────────────────────────────────┐ │
│  │ Field: [Weight      ▼]                             │ │
│  │                                                     │ │
│  │ Extract Method:                                     │ │
│  │ ○ Position (column):  [1] to [8]                   │ │
│  │ ● Regex:  [(\d{8})]                                │ │
│  │ ○ JSON Path:  [$.weight.value]                     │ │
│  │                                                     │ │
│  │ Data Type: [Float ▼]                                │ │
│  │ Multiplier: [0.01] (internal to display)           │ │
│  │ Unit: [lb ▼]                                        │ │
│  │                                                     │ │
│  │ Preview: [1234.50 lb]            ← Parsed value   │ │
│  └────────────────────────────────────────────────────┘ │
│                                                          │
│  [< Back]  [Test Connection]  [Save Template]  [Next >] │
└─────────────────────────────────────────────────────────┘
```

---

## Database Schema - Universal

### Flexible Schema for Any Scale Type

```sql
-- Scales (supports multiple scales)
CREATE TABLE scales (
    id TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    location TEXT,
    market_type TEXT,
    protocol_name TEXT,
    protocol_definition TEXT, -- JSON
    connection_config TEXT,   -- JSON
    enabled INTEGER DEFAULT 1,
    created TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    modified TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Weight Readings (universal format)
CREATE TABLE weight_readings (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    scale_id TEXT REFERENCES scales(id),
    timestamp DATETIME DEFAULT CURRENT_TIMESTAMP,

    -- Core fields (always present)
    weight_value REAL NOT NULL,
    weight_unit TEXT NOT NULL,

    -- Optional fields (protocol-dependent)
    tare REAL,
    gross REAL,
    net REAL,
    status TEXT,

    -- Extended fields (market-specific, stored as JSON)
    extended_data TEXT, -- JSON: {"vehicle_id": "ABC123", "axle_weights": [...]}

    -- Metadata
    raw_data TEXT, -- Original data string for debugging
    quality_score REAL, -- 0-1, confidence in reading

    INDEX idx_scale_timestamp (scale_id, timestamp),
    INDEX idx_timestamp (timestamp)
);

-- Protocol Templates (user and built-in)
CREATE TABLE protocol_templates (
    id TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    category TEXT, -- manufacturer, generic, industry, custom
    definition TEXT, -- JSON
    is_builtin INTEGER DEFAULT 0,
    created_by TEXT,
    created TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    modified TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Transactions (optional, for markets needing it)
CREATE TABLE transactions (
    id TEXT PRIMARY KEY,
    scale_id TEXT REFERENCES scales(id),
    transaction_type TEXT, -- shipping, receiving, wim, batch, etc.
    start_time DATETIME,
    end_time DATETIME,
    total_weight REAL,
    item_count INTEGER,
    customer_id TEXT,
    vehicle_id TEXT,
    metadata TEXT, -- JSON for flexible data

    INDEX idx_scale_transaction (scale_id, start_time)
);
```

---

## Plugin Architecture (Future v2.x)

Allow third-party developers to extend:

```csharp
public interface IScalePlugin
{
    string Name { get; }
    string Version { get; }
    string Author { get; }

    // Protocol plugins
    IScaleProtocol CreateProtocol();

    // Display plugins
    IOverlayRenderer CreateOverlay();

    // Export plugins
    IDataExporter CreateExporter();

    // Alert plugins
    IAlertHandler CreateAlertHandler();
}
```

**Example Plugins:**
- SAP integration
- QuickBooks export
- Custom overlay designer
- SMS alert handler
- Barcode scanner integration
- RFID reader support

---

## Commercial Licensing Model

### Tiered Pricing

| Edition | Price | Max Scales | Features |
|---------|-------|------------|----------|
| **Trial** | Free (30 days) | 1 | Basic streaming, limited overlays |
| **Standard** | $299/year | 1-5 | All protocols, basic analytics |
| **Professional** | $999/year | 6-50 | Multi-scale, cloud sync, advanced analytics |
| **Enterprise** | $2,499/year | Unlimited | All features, white-label, priority support |
| **OEM** | Custom | Unlimited | Embedding rights, no branding, redistribution |

### Add-Ons (à la carte)

- **Cloud Storage**: $50/month (unlimited weight data)
- **Mobile App**: $200/year (iOS + Android)
- **Premium Support**: $500/year (24/7 phone/email)
- **Custom Development**: $150/hour
- **Training**: $1,000/day on-site

---

## Summary

Version 2.0 is a **complete platform rewrite** with:

✅ **Zero hardcoded protocols** - Everything user-configurable
✅ **Manufacturer agnostic** - Equal support for all vendors
✅ **Market independent** - Floor, truck, train, hopper, etc.
✅ **White-label ready** - OEM embedding support
✅ **Commercial licensing** - Multiple tiers, feature flags
✅ **Extensible** - Plugin architecture for 3rd party
✅ **Multi-tenant** - Enterprise deployments
✅ **Protocol library** - Pre-built templates, custom creation
✅ **Visual designer** - No coding required for new protocols

**This is not a "Fairbanks 6011 streamer" anymore. This is a universal industrial scale integration platform.**

---

**Document Version**: 1.0
**Date**: 2026-01-24
**Status**: Architectural Blueprint
