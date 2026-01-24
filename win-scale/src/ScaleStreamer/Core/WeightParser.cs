using System.Text.RegularExpressions;

namespace ScaleStreamer.Core;

/// <summary>
/// Parses weight data from Fairbanks 6011 scale format.
/// Format: STATUS  WEIGHT  TARE
/// Example: "1   44140    00" - weight is 44140
/// May contain STX (\x02) and ETX (\x03) markers
/// </summary>
public static class WeightParser
{
    private static readonly Regex WeightPattern = new(@"^[01][\s""]*\s*([-+]?\d+\.?\d*)\s*\d*$",
        RegexOptions.Compiled);

    /// <summary>
    /// Parse raw scale data and extract weight value.
    /// </summary>
    /// <param name="data">Raw data from scale</param>
    /// <returns>Formatted weight string or null if parsing fails</returns>
    public static string? Parse(string data)
    {
        if (string.IsNullOrWhiteSpace(data))
            return null;

        try
        {
            // Remove control characters (STX, ETX)
            var cleaned = data
                .Replace("\x02", "")
                .Replace("\x03", "")
                .Trim();

            if (string.IsNullOrEmpty(cleaned))
                return null;

            // Split by whitespace
            var parts = cleaned.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length >= 2)
            {
                // Check for negative indicator (1" prefix means negative)
                bool isNegative = parts[0].Contains("\"") && parts[0].StartsWith("1");

                // Second part is the weight
                if (double.TryParse(parts[1], out double value))
                {
                    if (isNegative)
                        value = -Math.Abs(value);

                    // Format with one decimal place
                    return value == Math.Floor(value)
                        ? $"{value:F1}"
                        : $"{value:F1}";
                }
            }

            // Alternative: try regex match
            var match = WeightPattern.Match(cleaned);
            if (match.Success && double.TryParse(match.Groups[1].Value, out double regexValue))
            {
                return $"{regexValue:F1}";
            }
        }
        catch
        {
            // Parsing failed
        }

        return null;
    }

    /// <summary>
    /// Parse raw bytes from scale.
    /// </summary>
    public static string? Parse(byte[] data)
    {
        if (data == null || data.Length == 0)
            return null;

        var text = System.Text.Encoding.ASCII.GetString(data);
        return Parse(text);
    }
}
