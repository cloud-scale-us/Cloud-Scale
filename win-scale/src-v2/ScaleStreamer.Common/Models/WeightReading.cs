namespace ScaleStreamer.Common.Models;

/// <summary>
/// Represents a single weight reading from a scale
/// </summary>
public class WeightReading
{
    /// <summary>
    /// Timestamp when the reading was taken
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Weight value (in the unit specified by Unit property)
    /// </summary>
    public double Weight { get; set; }

    /// <summary>
    /// Tare weight (if applicable)
    /// </summary>
    public double? Tare { get; set; }

    /// <summary>
    /// Gross weight (if applicable)
    /// </summary>
    public double? Gross { get; set; }

    /// <summary>
    /// Net weight (calculated or from scale)
    /// </summary>
    public double? Net { get; set; }

    /// <summary>
    /// Unit of measurement (lb, kg, oz, g, t, etc.)
    /// </summary>
    public string Unit { get; set; } = "lb";

    /// <summary>
    /// Scale status (Stable, Motion, Overload, Underload, etc.)
    /// </summary>
    public ScaleStatus Status { get; set; } = ScaleStatus.Stable;

    /// <summary>
    /// Quality score (0.0 to 1.0) - confidence in the reading
    /// </summary>
    public double QualityScore { get; set; } = 1.0;

    /// <summary>
    /// Raw data string received from scale (for diagnostics)
    /// </summary>
    public string? RawData { get; set; }

    /// <summary>
    /// Scale ID (for multi-scale deployments)
    /// </summary>
    public string? ScaleId { get; set; }

    /// <summary>
    /// Extended data (protocol-specific, stored as key-value pairs)
    /// Example: VehicleId, AxleWeights, LicensePlate, etc.
    /// </summary>
    public Dictionary<string, object>? ExtendedData { get; set; }

    /// <summary>
    /// Whether this reading passed validation rules
    /// </summary>
    public bool IsValid { get; set; } = true;

    /// <summary>
    /// Validation errors (if any)
    /// </summary>
    public List<string>? ValidationErrors { get; set; }

    /// <summary>
    /// Calculate net weight from gross and tare
    /// </summary>
    public double CalculateNet()
    {
        if (Gross.HasValue && Tare.HasValue)
        {
            return Gross.Value - Tare.Value;
        }
        return Weight;
    }

    /// <summary>
    /// Convert weight to a different unit
    /// </summary>
    public double ConvertTo(string targetUnit)
    {
        return WeightConverter.Convert(Weight, Unit, targetUnit);
    }

    public override string ToString()
    {
        return $"{Weight:F2} {Unit} [{Status}] @ {Timestamp:HH:mm:ss.fff}";
    }
}
