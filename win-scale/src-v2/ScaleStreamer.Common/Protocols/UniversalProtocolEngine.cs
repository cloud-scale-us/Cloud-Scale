using ScaleStreamer.Common.Models;
using System.Text.RegularExpressions;
using System.Text.Json;

namespace ScaleStreamer.Common.Protocols;

/// <summary>
/// Universal protocol parser that can handle any scale protocol
/// based on JSON configuration
/// </summary>
public class UniversalProtocolEngine
{
    private readonly ProtocolDefinition _protocol;

    public UniversalProtocolEngine(ProtocolDefinition protocol)
    {
        _protocol = protocol ?? throw new ArgumentNullException(nameof(protocol));
    }

    /// <summary>
    /// Parse raw data from scale into WeightReading
    /// </summary>
    public WeightReading? Parse(string rawData)
    {
        if (string.IsNullOrWhiteSpace(rawData))
            return null;

        try
        {
            var reading = new WeightReading
            {
                RawData = rawData,
                Timestamp = DateTime.UtcNow,
                ScaleId = _protocol.ProtocolName
            };

            // Parse based on data format
            switch (_protocol.DataFormat)
            {
                case DataFormat.ASCII:
                    return ParseAscii(rawData, reading);

                case DataFormat.JSON:
                    return ParseJson(rawData, reading);

                case DataFormat.Binary:
                    // TODO: Implement binary parsing
                    throw new NotImplementedException("Binary parsing not yet implemented");

                case DataFormat.ModbusRegisters:
                    // TODO: Implement Modbus register parsing
                    throw new NotImplementedException("Modbus parsing not yet implemented");

                default:
                    throw new NotSupportedException($"Data format {_protocol.DataFormat} not supported");
            }
        }
        catch (Exception ex)
        {
            // Return reading with error
            return new WeightReading
            {
                RawData = rawData,
                IsValid = false,
                ValidationErrors = new List<string> { ex.Message }
            };
        }
    }

    /// <summary>
    /// Parse ASCII format data
    /// </summary>
    private WeightReading? ParseAscii(string rawData, WeightReading reading)
    {
        if (_protocol.Parsing == null)
            throw new InvalidOperationException("Parsing configuration is required");

        // Use regex if specified
        if (!string.IsNullOrEmpty(_protocol.Parsing.Regex))
        {
            return ParseWithRegex(rawData, reading);
        }

        // Use field separator if specified
        if (!string.IsNullOrEmpty(_protocol.Parsing.FieldSeparator))
        {
            return ParseWithSeparator(rawData, reading);
        }

        // Use position-based parsing
        return ParseWithPositions(rawData, reading);
    }

    /// <summary>
    /// Parse using regular expression
    /// </summary>
    private WeightReading? ParseWithRegex(string rawData, WeightReading reading)
    {
        var regex = new Regex(_protocol.Parsing.Regex!, RegexOptions.Compiled);
        var match = regex.Match(rawData);

        if (!match.Success)
        {
            reading.IsValid = false;
            reading.ValidationErrors = new List<string> { "Regex pattern did not match" };
            return reading;
        }

        // Extract fields based on named groups
        foreach (var field in _protocol.Parsing.Fields)
        {
            if (string.IsNullOrEmpty(field.RegexGroup))
                continue;

            var group = match.Groups[field.RegexGroup];
            if (!group.Success)
                continue;

            var value = group.Value;
            ApplyFieldValue(reading, field, value);
        }

        return reading;
    }

    /// <summary>
    /// Parse using field separator (e.g., whitespace, comma, tab)
    /// </summary>
    private WeightReading? ParseWithSeparator(string rawData, WeightReading reading)
    {
        // Split by separator (regex pattern)
        var separator = _protocol.Parsing.FieldSeparator!;
        var parts = Regex.Split(rawData.Trim(), separator);

        foreach (var field in _protocol.Parsing.Fields.Where(f => f.Position.HasValue))
        {
            if (field.Position!.Value >= parts.Length)
                continue;

            var value = parts[field.Position.Value];
            ApplyFieldValue(reading, field, value);
        }

        return reading;
    }

    /// <summary>
    /// Parse using fixed positions (substring)
    /// </summary>
    private WeightReading? ParseWithPositions(string rawData, WeightReading reading)
    {
        foreach (var field in _protocol.Parsing.Fields)
        {
            if (!field.StartPosition.HasValue || !field.Length.HasValue)
                continue;

            if (field.StartPosition.Value + field.Length.Value > rawData.Length)
                continue;

            var value = rawData.Substring(field.StartPosition.Value, field.Length.Value);
            ApplyFieldValue(reading, field, value);
        }

        return reading;
    }

    /// <summary>
    /// Parse JSON format data
    /// </summary>
    private WeightReading? ParseJson(string rawData, WeightReading reading)
    {
        var json = JsonDocument.Parse(rawData);

        foreach (var field in _protocol.Parsing.Fields)
        {
            if (string.IsNullOrEmpty(field.JsonPath))
                continue;

            // TODO: Implement JSON path evaluation
            // For now, simple property access
            var value = GetJsonValue(json.RootElement, field.JsonPath);
            if (value != null)
            {
                ApplyFieldValue(reading, field, value);
            }
        }

        return reading;
    }

    /// <summary>
    /// Apply extracted value to WeightReading based on field definition
    /// </summary>
    private void ApplyFieldValue(WeightReading reading, FieldDefinition field, string value)
    {
        // Trim whitespace
        value = value.Trim();

        // Apply mapping if specified
        if (field.Mapping != null && field.Mapping.ContainsKey(value))
        {
            value = field.Mapping[value];
        }

        // Convert to appropriate type and apply to reading
        switch (field.Name.ToLowerInvariant())
        {
            case "weight":
                if (double.TryParse(value, out var weight))
                {
                    weight = weight * (field.Multiplier ?? 1.0) + (field.Offset ?? 0.0);
                    reading.Weight = weight;
                    reading.Unit = field.Unit ?? "lb";
                }
                break;

            case "tare":
                if (double.TryParse(value, out var tare))
                {
                    tare = tare * (field.Multiplier ?? 1.0) + (field.Offset ?? 0.0);
                    reading.Tare = tare;
                }
                break;

            case "gross":
                if (double.TryParse(value, out var gross))
                {
                    gross = gross * (field.Multiplier ?? 1.0) + (field.Offset ?? 0.0);
                    reading.Gross = gross;
                }
                break;

            case "net":
                if (double.TryParse(value, out var net))
                {
                    net = net * (field.Multiplier ?? 1.0) + (field.Offset ?? 0.0);
                    reading.Net = net;
                }
                break;

            case "unit":
                reading.Unit = value;
                break;

            case "status":
                if (Enum.TryParse<ScaleStatus>(value, true, out var status))
                {
                    reading.Status = status;
                }
                break;

            default:
                // Store in extended data
                reading.ExtendedData ??= new Dictionary<string, object>();
                reading.ExtendedData[field.Name] = value;
                break;
        }
    }

    /// <summary>
    /// Simple JSON path evaluation (supports basic $.property syntax)
    /// </summary>
    private string? GetJsonValue(JsonElement element, string jsonPath)
    {
        // TODO: Implement full JSONPath support
        // For now, simple property access: $.weight.value
        var path = jsonPath.TrimStart('$', '.');
        var parts = path.Split('.');

        var current = element;
        foreach (var part in parts)
        {
            if (current.TryGetProperty(part, out var next))
            {
                current = next;
            }
            else
            {
                return null;
            }
        }

        return current.ToString();
    }

    /// <summary>
    /// Validate weight reading against protocol validation rules
    /// </summary>
    public bool Validate(WeightReading reading)
    {
        if (_protocol.Validation == null)
            return true; // No validation rules = always valid

        var errors = new List<string>();

        // Min/max weight
        if (_protocol.Validation.MinWeight.HasValue && reading.Weight < _protocol.Validation.MinWeight.Value)
        {
            errors.Add($"Weight {reading.Weight} below minimum {_protocol.Validation.MinWeight}");
        }

        if (_protocol.Validation.MaxWeight.HasValue && reading.Weight > _protocol.Validation.MaxWeight.Value)
        {
            errors.Add($"Weight {reading.Weight} above maximum {_protocol.Validation.MaxWeight}");
        }

        // Require stable
        if (_protocol.Validation.RequireStable == true && reading.Status != ScaleStatus.Stable)
        {
            errors.Add($"Weight not stable (status: {reading.Status})");
        }

        reading.IsValid = errors.Count == 0;
        reading.ValidationErrors = errors.Count > 0 ? errors : null;

        return reading.IsValid;
    }
}
