namespace ScaleStreamer.Common.Models;

/// <summary>
/// Scale status enumeration
/// </summary>
public enum ScaleStatus
{
    /// <summary>
    /// Unknown or uninitialized status
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Weight is stable and ready to read
    /// </summary>
    Stable = 1,

    /// <summary>
    /// Weight is in motion (changing)
    /// </summary>
    Motion = 2,

    /// <summary>
    /// Scale is overloaded (above capacity)
    /// </summary>
    Overload = 3,

    /// <summary>
    /// Scale is underloaded (below minimum)
    /// </summary>
    Underload = 4,

    /// <summary>
    /// Scale is at zero / tared
    /// </summary>
    Zero = 5,

    /// <summary>
    /// Scale error condition
    /// </summary>
    Error = 6,

    /// <summary>
    /// Scale is calibrating
    /// </summary>
    Calibrating = 7,

    /// <summary>
    /// Scale is in setup mode
    /// </summary>
    Setup = 8
}

/// <summary>
/// Connection type enumeration
/// </summary>
public enum ConnectionType
{
    /// <summary>
    /// TCP/IP network connection
    /// </summary>
    TcpIp,

    /// <summary>
    /// RS232 serial port
    /// </summary>
    RS232,

    /// <summary>
    /// RS485 serial port
    /// </summary>
    RS485,

    /// <summary>
    /// USB connection
    /// </summary>
    USB,

    /// <summary>
    /// HTTP REST API
    /// </summary>
    Http,

    /// <summary>
    /// Modbus RTU (serial)
    /// </summary>
    ModbusRTU,

    /// <summary>
    /// Modbus TCP (network)
    /// </summary>
    ModbusTCP,

    /// <summary>
    /// EtherNet/IP
    /// </summary>
    EtherNetIP,

    /// <summary>
    /// Custom/other connection type
    /// </summary>
    Custom
}

/// <summary>
/// Connection status enumeration
/// </summary>
public enum ConnectionStatus
{
    /// <summary>
    /// Not connected
    /// </summary>
    Disconnected,

    /// <summary>
    /// Attempting to connect
    /// </summary>
    Connecting,

    /// <summary>
    /// Successfully connected
    /// </summary>
    Connected,

    /// <summary>
    /// Connection error
    /// </summary>
    Error,

    /// <summary>
    /// Reconnecting after disconnection
    /// </summary>
    Reconnecting
}

/// <summary>
/// Data format enumeration
/// </summary>
public enum DataFormat
{
    /// <summary>
    /// ASCII text format
    /// </summary>
    ASCII,

    /// <summary>
    /// Binary data format
    /// </summary>
    Binary,

    /// <summary>
    /// JSON format
    /// </summary>
    JSON,

    /// <summary>
    /// XML format
    /// </summary>
    XML,

    /// <summary>
    /// Modbus registers
    /// </summary>
    ModbusRegisters,

    /// <summary>
    /// Custom format
    /// </summary>
    Custom
}

/// <summary>
/// Data mode (how data is received)
/// </summary>
public enum DataMode
{
    /// <summary>
    /// Scale continuously sends data
    /// </summary>
    Continuous,

    /// <summary>
    /// Request/response (demand) mode
    /// </summary>
    Demand,

    /// <summary>
    /// Event-driven (scale sends on change)
    /// </summary>
    EventDriven,

    /// <summary>
    /// Polled at regular intervals
    /// </summary>
    Polled
}

/// <summary>
/// Market type enumeration
/// </summary>
public enum MarketType
{
    FloorScales,
    TruckScales,
    TrainScales,
    HopperScales,
    ConveyorScales,
    Checkweighers,
    MedicalScales,
    RetailScales,
    LaboratoryBalances,
    LivestockScales,
    AgricultureScales,
    WasteScales,
    Custom
}
