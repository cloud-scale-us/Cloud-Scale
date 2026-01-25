using System.Text.Json.Serialization;

namespace ScaleStreamer.Common.Models;

/// <summary>
/// Connection configuration for a scale
/// </summary>
public class ConnectionConfig
{
    /// <summary>
    /// Connection type
    /// </summary>
    [JsonPropertyName("type")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ConnectionType Type { get; set; } = ConnectionType.TcpIp;

    // TCP/IP Settings
    [JsonPropertyName("host")]
    public string? Host { get; set; }

    [JsonPropertyName("port")]
    public int? Port { get; set; }

    [JsonPropertyName("timeout_ms")]
    public int TimeoutMs { get; set; } = 5000;

    [JsonPropertyName("keepalive_enabled")]
    public bool KeepaliveEnabled { get; set; }

    [JsonPropertyName("keepalive_interval_seconds")]
    public int KeepaliveIntervalSeconds { get; set; } = 30;

    [JsonPropertyName("auto_reconnect")]
    public bool AutoReconnect { get; set; } = true;

    [JsonPropertyName("reconnect_interval_seconds")]
    public int ReconnectIntervalSeconds { get; set; } = 10;

    // Serial Settings (RS232/RS485)
    [JsonPropertyName("com_port")]
    public string? ComPort { get; set; }

    [JsonPropertyName("baud_rate")]
    public int BaudRate { get; set; } = 9600;

    [JsonPropertyName("data_bits")]
    public int DataBits { get; set; } = 8;

    [JsonPropertyName("parity")]
    public string Parity { get; set; } = "None"; // None, Even, Odd, Mark, Space

    [JsonPropertyName("stop_bits")]
    public string StopBits { get; set; } = "1"; // 1, 1.5, 2

    [JsonPropertyName("flow_control")]
    public string FlowControl { get; set; } = "None"; // None, Hardware, Software

    [JsonPropertyName("device_address")]
    public int? DeviceAddress { get; set; } // For RS485

    // HTTP Settings
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("method")]
    public string HttpMethod { get; set; } = "GET";

    [JsonPropertyName("headers")]
    public Dictionary<string, string>? Headers { get; set; }

    [JsonPropertyName("authentication_type")]
    public string? AuthenticationType { get; set; } // None, ApiKey, Bearer, Basic, OAuth2

    [JsonPropertyName("api_key")]
    public string? ApiKey { get; set; }

    [JsonPropertyName("api_key_header")]
    public string? ApiKeyHeader { get; set; } = "X-API-Key";

    // Modbus Settings
    [JsonPropertyName("unit_id")]
    public byte? UnitId { get; set; } = 1;

    [JsonPropertyName("register_type")]
    public string? RegisterType { get; set; } // Coils, DiscreteInputs, InputRegisters, HoldingRegisters

    [JsonPropertyName("start_address")]
    public int? StartAddress { get; set; }

    [JsonPropertyName("register_count")]
    public int? RegisterCount { get; set; }

    // USB Settings
    [JsonPropertyName("vendor_id")]
    public string? VendorId { get; set; }

    [JsonPropertyName("product_id")]
    public string? ProductId { get; set; }

    // Encoding
    [JsonPropertyName("encoding")]
    public string Encoding { get; set; } = "ASCII"; // ASCII, UTF-8, UTF-16, Binary, Hex

    /// <summary>
    /// Get a connection string representation
    /// </summary>
    public string GetConnectionString()
    {
        return Type switch
        {
            ConnectionType.TcpIp => $"{Host}:{Port}",
            ConnectionType.RS232 or ConnectionType.RS485 => $"{ComPort}:{BaudRate}",
            ConnectionType.Http => Url ?? "",
            ConnectionType.ModbusTCP => $"{Host}:{Port} (Unit {UnitId})",
            ConnectionType.ModbusRTU => $"{ComPort}:{BaudRate} (Unit {UnitId})",
            ConnectionType.USB => $"USB VID:{VendorId} PID:{ProductId}",
            _ => "Unknown"
        };
    }

    public override string ToString() => GetConnectionString();
}
