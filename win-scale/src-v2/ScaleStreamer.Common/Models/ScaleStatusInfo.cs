namespace ScaleStreamer.Common.Models;

/// <summary>
/// Represents the current status information of a scale connection
/// </summary>
public class ScaleStatusInfo
{
    /// <summary>
    /// Whether the scale is currently connected
    /// </summary>
    public bool IsConnected { get; set; }

    /// <summary>
    /// Current connection status
    /// </summary>
    public ConnectionStatus ConnectionStatus { get; set; }

    /// <summary>
    /// Last time a weight reading was received
    /// </summary>
    public DateTime? LastReadTime { get; set; }

    /// <summary>
    /// Number of consecutive errors encountered
    /// </summary>
    public int ConsecutiveErrors { get; set; }

    /// <summary>
    /// Additional status information (protocol-specific)
    /// </summary>
    public Dictionary<string, object>? ExtendedInfo { get; set; }
}
