namespace ScaleStreamer.Core;

/// <summary>
/// Event args for weight received events.
/// </summary>
public class WeightReceivedEventArgs : EventArgs
{
    public string Weight { get; }
    public DateTime Timestamp { get; }

    public WeightReceivedEventArgs(string weight)
    {
        Weight = weight;
        Timestamp = DateTime.Now;
    }
}

/// <summary>
/// Event args for connection status changes.
/// </summary>
public class ConnectionStatusEventArgs : EventArgs
{
    public bool IsConnected { get; }
    public string? Message { get; }

    public ConnectionStatusEventArgs(bool isConnected, string? message = null)
    {
        IsConnected = isConnected;
        Message = message;
    }
}

/// <summary>
/// Interface for scale readers (TCP/IP or Serial).
/// </summary>
public interface IScaleReader : IDisposable
{
    /// <summary>
    /// Fired when a new weight reading is received.
    /// </summary>
    event EventHandler<WeightReceivedEventArgs>? WeightReceived;

    /// <summary>
    /// Fired when connection status changes.
    /// </summary>
    event EventHandler<ConnectionStatusEventArgs>? ConnectionStatusChanged;

    /// <summary>
    /// Whether the reader is currently connected.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Most recent weight reading.
    /// </summary>
    string CurrentWeight { get; }

    /// <summary>
    /// Start reading from the scale.
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stop reading from the scale.
    /// </summary>
    void Stop();
}
