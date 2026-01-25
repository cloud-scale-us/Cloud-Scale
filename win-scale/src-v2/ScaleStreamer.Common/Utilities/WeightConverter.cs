namespace ScaleStreamer.Common.Models;

/// <summary>
/// Utility class for weight unit conversions
/// </summary>
public static class WeightConverter
{
    private static readonly Dictionary<string, double> ToKilogramsConversion = new()
    {
        { "kg", 1.0 },
        { "lb", 0.45359237 },
        { "oz", 0.028349523125 },
        { "g", 0.001 },
        { "mg", 0.000001 },
        { "t", 1000.0 },          // metric ton
        { "ton", 907.18474 },     // US ton
        { "ct", 0.0002 },         // carat
        { "dwt", 0.00155517384 }  // pennyweight
    };

    /// <summary>
    /// Convert weight from one unit to another
    /// </summary>
    public static double Convert(double value, string fromUnit, string toUnit)
    {
        var normalizedFrom = fromUnit.ToLowerInvariant().Trim();
        var normalizedTo = toUnit.ToLowerInvariant().Trim();

        if (normalizedFrom == normalizedTo)
            return value;

        if (!ToKilogramsConversion.ContainsKey(normalizedFrom))
            throw new ArgumentException($"Unknown weight unit: {fromUnit}", nameof(fromUnit));

        if (!ToKilogramsConversion.ContainsKey(normalizedTo))
            throw new ArgumentException($"Unknown weight unit: {toUnit}", nameof(toUnit));

        // Convert to kg, then to target unit
        var kilograms = value * ToKilogramsConversion[normalizedFrom];
        return kilograms / ToKilogramsConversion[normalizedTo];
    }

    /// <summary>
    /// Get list of supported units
    /// </summary>
    public static IEnumerable<string> GetSupportedUnits() => ToKilogramsConversion.Keys;

    /// <summary>
    /// Check if unit is supported
    /// </summary>
    public static bool IsUnitSupported(string unit)
    {
        return ToKilogramsConversion.ContainsKey(unit.ToLowerInvariant().Trim());
    }
}
