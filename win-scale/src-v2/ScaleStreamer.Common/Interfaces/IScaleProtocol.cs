using ScaleStreamer.Common.Models;

namespace ScaleStreamer.Common.Interfaces;

/// <summary>
/// Universal interface for all scale communication protocols.
/// Implementations can be TCP/IP, Serial, USB, HTTP, Modbus, etc.
/// </summary>
public interface IScaleProtocol : IDisposable
{
    /// <summary>
    /// Protocol name (e.g., "Fairbanks 6011", "Modbus TCP", "Generic ASCII")
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Manufacturer name (e.g., "Fairbanks", "Rice Lake", "Generic")
    /// </summary>
    string Manufacturer { get; }

    /// <summary>
    /// Protocol version
    /// </summary>
    string Version { get; }

    /// <summary>
    /// Connection type (TCP, Serial, USB, HTTP, Modbus)
    /// </summary>
    ConnectionType ConnectionType { get; }

    /// <summary>
    /// Current connection status
    /// </summary>
    ConnectionStatus Status { get; }

    /// <summary>
    /// Connect to the scale using provided configuration
    /// </summary>
    Task<bool> ConnectAsync(ConnectionConfig config, CancellationToken cancellationToken = default);

    /// <summary>
    /// Disconnect from the scale
    /// </summary>
    Task DisconnectAsync();

    /// <summary>
    /// Read a single weight value from the scale
    /// </summary>
    Task<WeightReading?> ReadWeightAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Start continuous reading (for streaming protocols)
    /// </summary>
    Task StartContinuousReadingAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stop continuous reading
    /// </summary>
    Task StopContinuousReadingAsync();

    /// <summary>
    /// Get current scale status information
    /// </summary>
    Task<ScaleStatusInfo> GetStatusAsync();

    /// <summary>
    /// Send a command to the scale (protocol-specific)
    /// </summary>
    Task<bool> SendCommandAsync(string command);

    /// <summary>
    /// Test the connection without starting continuous reading
    /// </summary>
    Task<bool> TestConnectionAsync(ConnectionConfig config);

    /// <summary>
    /// Event raised when new weight data is received
    /// </summary>
    event EventHandler<WeightReading> WeightReceived;

    /// <summary>
    /// Event raised when connection status changes
    /// </summary>
    event EventHandler<ConnectionStatus> StatusChanged;

    /// <summary>
    /// Event raised when an error occurs
    /// </summary>
    event EventHandler<Exception> ErrorOccurred;

    /// <summary>
    /// Event raised when raw data is received (for diagnostics)
    /// </summary>
    event EventHandler<string> RawDataReceived;
}
