using System.Text.Json.Serialization;

namespace ScaleStreamer.Common.Models;

/// <summary>
/// Defines a scale protocol configuration (loaded from JSON templates)
/// This allows users to create custom protocols without coding
/// </summary>
public class ProtocolDefinition
{
    /// <summary>
    /// Protocol name (e.g., "Fairbanks 6011")
    /// </summary>
    [JsonPropertyName("protocol_name")]
    public string ProtocolName { get; set; } = string.Empty;

    /// <summary>
    /// Manufacturer name
    /// </summary>
    [JsonPropertyName("manufacturer")]
    public string Manufacturer { get; set; } = "Generic";

    /// <summary>
    /// Protocol version
    /// </summary>
    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0";

    /// <summary>
    /// Description
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Connection configuration
    /// </summary>
    [JsonPropertyName("connection")]
    public ConnectionConfig Connection { get; set; } = new();

    /// <summary>
    /// Data format (ASCII, Binary, JSON, XML, Modbus)
    /// </summary>
    [JsonPropertyName("data_format")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public DataFormat DataFormat { get; set; } = DataFormat.ASCII;

    /// <summary>
    /// Data mode (Continuous, Demand, Event-Driven, Polled)
    /// </summary>
    [JsonPropertyName("mode")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public DataMode Mode { get; set; } = DataMode.Continuous;

    /// <summary>
    /// Parsing configuration
    /// </summary>
    [JsonPropertyName("parsing")]
    public ParsingConfig Parsing { get; set; } = new();

    /// <summary>
    /// Validation rules
    /// </summary>
    [JsonPropertyName("validation")]
    public ValidationConfig? Validation { get; set; }

    /// <summary>
    /// Polling interval in milliseconds (for polled mode)
    /// </summary>
    [JsonPropertyName("polling_interval_ms")]
    public int? PollingIntervalMs { get; set; }

    /// <summary>
    /// Scale commands (for demand mode and control)
    /// </summary>
    [JsonPropertyName("commands")]
    public CommandsConfig? Commands { get; set; }
}

/// <summary>
/// Parsing configuration
/// </summary>
public class ParsingConfig
{
    /// <summary>
    /// Line delimiter (e.g., "\r\n", "\n", "\r")
    /// </summary>
    [JsonPropertyName("line_delimiter")]
    public string? LineDelimiter { get; set; } = "\r\n";

    /// <summary>
    /// Field separator (e.g., ",", "\t", "\\s+")
    /// </summary>
    [JsonPropertyName("field_separator")]
    public string? FieldSeparator { get; set; }

    /// <summary>
    /// Start delimiter (e.g., STX, custom byte)
    /// </summary>
    [JsonPropertyName("start_delimiter")]
    public string? StartDelimiter { get; set; }

    /// <summary>
    /// End delimiter (e.g., ETX, custom byte)
    /// </summary>
    [JsonPropertyName("end_delimiter")]
    public string? EndDelimiter { get; set; }

    /// <summary>
    /// Regular expression pattern for extraction
    /// </summary>
    [JsonPropertyName("regex")]
    public string? Regex { get; set; }

    /// <summary>
    /// Field definitions
    /// </summary>
    [JsonPropertyName("fields")]
    public List<FieldDefinition> Fields { get; set; } = new();
}

/// <summary>
/// Field definition for extracting data
/// </summary>
public class FieldDefinition
{
    /// <summary>
    /// Field name (e.g., "weight", "tare", "status", "unit")
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Field position (for fixed-width or delimited formats)
    /// </summary>
    [JsonPropertyName("position")]
    public int? Position { get; set; }

    /// <summary>
    /// Start position (for substring extraction)
    /// </summary>
    [JsonPropertyName("start_position")]
    public int? StartPosition { get; set; }

    /// <summary>
    /// Field length (for fixed-width formats)
    /// </summary>
    [JsonPropertyName("length")]
    public int? Length { get; set; }

    /// <summary>
    /// Regular expression group name
    /// </summary>
    [JsonPropertyName("regex_group")]
    public string? RegexGroup { get; set; }

    /// <summary>
    /// JSON path (e.g., "$.weight.value")
    /// </summary>
    [JsonPropertyName("json_path")]
    public string? JsonPath { get; set; }

    /// <summary>
    /// XPath (for XML formats)
    /// </summary>
    [JsonPropertyName("xpath")]
    public string? XPath { get; set; }

    /// <summary>
    /// Modbus register address
    /// </summary>
    [JsonPropertyName("register_address")]
    public int? RegisterAddress { get; set; }

    /// <summary>
    /// Number of Modbus registers to read
    /// </summary>
    [JsonPropertyName("register_count")]
    public int? RegisterCount { get; set; }

    /// <summary>
    /// Data type (Float, Integer, String, Boolean, etc.)
    /// </summary>
    [JsonPropertyName("data_type")]
    public string DataType { get; set; } = "string";

    /// <summary>
    /// Unit of measurement
    /// </summary>
    [JsonPropertyName("unit")]
    public string? Unit { get; set; }

    /// <summary>
    /// Multiplier to apply after extraction
    /// </summary>
    [JsonPropertyName("multiplier")]
    public double? Multiplier { get; set; } = 1.0;

    /// <summary>
    /// Offset to add after multiplying
    /// </summary>
    [JsonPropertyName("offset")]
    public double? Offset { get; set; } = 0.0;

    /// <summary>
    /// Value mapping (e.g., status codes: "1" -> "stable", "2" -> "motion")
    /// </summary>
    [JsonPropertyName("mapping")]
    public Dictionary<string, string>? Mapping { get; set; }

    /// <summary>
    /// Bit masks (for binary status fields)
    /// </summary>
    [JsonPropertyName("bit_masks")]
    public Dictionary<string, int>? BitMasks { get; set; }
}

/// <summary>
/// Validation configuration
/// </summary>
public class ValidationConfig
{
    /// <summary>
    /// Minimum acceptable weight
    /// </summary>
    [JsonPropertyName("min_weight")]
    public double? MinWeight { get; set; }

    /// <summary>
    /// Maximum acceptable weight
    /// </summary>
    [JsonPropertyName("max_weight")]
    public double? MaxWeight { get; set; }

    /// <summary>
    /// Require stable status
    /// </summary>
    [JsonPropertyName("require_stable")]
    public bool? RequireStable { get; set; }

    /// <summary>
    /// Tolerance for stability detection
    /// </summary>
    [JsonPropertyName("stability_tolerance")]
    public double? StabilityTolerance { get; set; }

    /// <summary>
    /// Stability duration in seconds
    /// </summary>
    [JsonPropertyName("stability_duration_seconds")]
    public double? StabilityDurationSeconds { get; set; }

    /// <summary>
    /// Maximum rate of change (for outlier detection)
    /// </summary>
    [JsonPropertyName("max_rate_of_change")]
    public double? MaxRateOfChange { get; set; }

    /// <summary>
    /// Moving average window size
    /// </summary>
    [JsonPropertyName("moving_average_window")]
    public int? MovingAverageWindow { get; set; }

    /// <summary>
    /// Maximum update rate (readings per second)
    /// </summary>
    [JsonPropertyName("max_update_rate")]
    public int? MaxUpdateRate { get; set; }
}

/// <summary>
/// Scale commands configuration
/// </summary>
public class CommandsConfig
{
    /// <summary>
    /// Command to request weight (demand mode)
    /// </summary>
    [JsonPropertyName("demand_weight")]
    public string? DemandWeight { get; set; }

    /// <summary>
    /// Command to zero the scale
    /// </summary>
    [JsonPropertyName("zero")]
    public string? Zero { get; set; }

    /// <summary>
    /// Command to tare the scale
    /// </summary>
    [JsonPropertyName("tare")]
    public string? Tare { get; set; }

    /// <summary>
    /// Command to print/output weight
    /// </summary>
    [JsonPropertyName("print")]
    public string? Print { get; set; }

    /// <summary>
    /// Command to clear tare
    /// </summary>
    [JsonPropertyName("clear_tare")]
    public string? ClearTare { get; set; }

    /// <summary>
    /// Command to enter setup/config mode
    /// </summary>
    [JsonPropertyName("enter_setup")]
    public string? EnterSetup { get; set; }

    /// <summary>
    /// Command to exit setup/config mode
    /// </summary>
    [JsonPropertyName("exit_setup")]
    public string? ExitSetup { get; set; }
}
