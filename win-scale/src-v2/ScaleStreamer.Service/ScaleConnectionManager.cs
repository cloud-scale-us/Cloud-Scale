using Microsoft.Extensions.Logging;
using ScaleStreamer.Common.Interfaces;
using ScaleStreamer.Common.Models;
using ScaleStreamer.Common.Protocols;
using System.Collections.Concurrent;

namespace ScaleStreamer.Service;

/// <summary>
/// Manages multiple scale connections
/// </summary>
public class ScaleConnectionManager
{
    private readonly ILogger<ScaleConnectionManager>? _logger;
    private readonly ConcurrentDictionary<string, IScaleProtocol> _connections = new();

    public event EventHandler<(string ScaleId, WeightReading Reading)>? WeightReceived;
    public event EventHandler<(string ScaleId, string Error)>? ErrorOccurred;
    public event EventHandler<(string ScaleId, ConnectionStatus Status)>? StatusChanged;

    public ScaleConnectionManager(ILogger<ScaleConnectionManager>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Add and connect a new scale
    /// </summary>
    public async Task<bool> AddScaleAsync(string scaleId, ProtocolDefinition protocol)
    {
        try
        {
            _logger?.LogInformation("Adding scale: {ScaleId} with protocol: {Protocol}",
                scaleId, protocol.ProtocolName);

            // Create protocol adapter based on connection type
            IScaleProtocol? protocolAdapter = protocol.Connection.Type switch
            {
                ConnectionType.TcpIp => new UniversalProtocolAdapter(protocol),
                ConnectionType.RS232 => throw new NotImplementedException("RS232 not yet implemented"),
                ConnectionType.RS485 => throw new NotImplementedException("RS485 not yet implemented"),
                ConnectionType.USB => throw new NotImplementedException("USB not yet implemented"),
                ConnectionType.Http => throw new NotImplementedException("HTTP not yet implemented"),
                ConnectionType.ModbusRTU => throw new NotImplementedException("Modbus RTU not yet implemented"),
                ConnectionType.ModbusTCP => throw new NotImplementedException("Modbus TCP not yet implemented"),
                _ => throw new NotSupportedException($"Connection type {protocol.Connection.Type} not supported")
            };

            if (protocolAdapter == null)
                return false;

            // Subscribe to events
            protocolAdapter.WeightReceived += (sender, reading) =>
            {
                reading.ScaleId = scaleId;
                WeightReceived?.Invoke(this, (scaleId, reading));
            };

            protocolAdapter.RawDataReceived += (sender, rawData) =>
            {
                _logger?.LogInformation("[{ScaleId}] Raw data: {RawData}", scaleId, rawData);
            };

            protocolAdapter.ErrorOccurred += (sender, error) =>
            {
                _logger?.LogWarning("[{ScaleId}] {Error}", scaleId, error.Message);
                ErrorOccurred?.Invoke(this, (scaleId, error.Message));
            };

            protocolAdapter.StatusChanged += (sender, status) =>
            {
                _logger?.LogInformation("Scale {ScaleId} status changed to: {Status}", scaleId, status);
                StatusChanged?.Invoke(this, (scaleId, status));
            };

            // Connect to scale
            var connected = await protocolAdapter.ConnectAsync(protocol.Connection);
            if (!connected)
            {
                _logger?.LogError("Failed to connect to scale: {ScaleId}", scaleId);
                return false;
            }

            // Add to dictionary
            _connections.TryAdd(scaleId, protocolAdapter);

            // Start continuous reading
            await protocolAdapter.StartContinuousReadingAsync();

            _logger?.LogInformation("Scale {ScaleId} connected and reading started", scaleId);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error adding scale: {ScaleId}", scaleId);
            ErrorOccurred?.Invoke(this, (scaleId, ex.Message));
            return false;
        }
    }

    /// <summary>
    /// Remove and disconnect a scale
    /// </summary>
    public async Task<bool> RemoveScaleAsync(string scaleId)
    {
        try
        {
            if (_connections.TryRemove(scaleId, out var protocol))
            {
                _logger?.LogInformation("Removing scale: {ScaleId}", scaleId);

                await protocol.StopContinuousReadingAsync();
                await protocol.DisconnectAsync();
                protocol.Dispose();

                _logger?.LogInformation("Scale {ScaleId} removed", scaleId);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error removing scale: {ScaleId}", scaleId);
            return false;
        }
    }

    /// <summary>
    /// Disconnect all scales
    /// </summary>
    public async Task DisconnectAllAsync()
    {
        _logger?.LogInformation("Disconnecting all scales...");

        var tasks = _connections.Keys.Select(scaleId => RemoveScaleAsync(scaleId));
        await Task.WhenAll(tasks);

        _logger?.LogInformation("All scales disconnected");
    }

    /// <summary>
    /// Get scale by ID
    /// </summary>
    public IScaleProtocol? GetScale(string scaleId)
    {
        _connections.TryGetValue(scaleId, out var protocol);
        return protocol;
    }

    /// <summary>
    /// Get all connected scale IDs
    /// </summary>
    public IEnumerable<string> GetConnectedScaleIds()
    {
        return _connections.Keys;
    }

    /// <summary>
    /// Get status of all scales
    /// </summary>
    public Dictionary<string, ConnectionStatus> GetAllStatuses()
    {
        return _connections.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.Status);
    }

    /// <summary>
    /// Request weight reading from specific scale
    /// </summary>
    public async Task<WeightReading?> RequestWeightAsync(string scaleId, CancellationToken cancellationToken = default)
    {
        if (_connections.TryGetValue(scaleId, out var protocol))
        {
            return await protocol.ReadWeightAsync(cancellationToken);
        }

        return null;
    }
}
