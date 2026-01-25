using ScaleStreamer.Common.Models;

namespace ScaleStreamer.Common.Protocols;

/// <summary>
/// Universal protocol adapter that combines TcpProtocolBase with UniversalProtocolEngine
/// Handles any TCP/IP scale protocol defined in JSON configuration
/// </summary>
public class UniversalProtocolAdapter : TcpProtocolBase
{
    private readonly ProtocolDefinition _protocolDefinition;
    private readonly UniversalProtocolEngine _engine;

    public UniversalProtocolAdapter(ProtocolDefinition protocolDefinition)
    {
        _protocolDefinition = protocolDefinition ?? throw new ArgumentNullException(nameof(protocolDefinition));
        _engine = new UniversalProtocolEngine(protocolDefinition);

        ProtocolName = protocolDefinition.ProtocolName;
        Manufacturer = protocolDefinition.Manufacturer;

        // Set line delimiter from protocol definition
        if (!string.IsNullOrEmpty(protocolDefinition.Parsing?.LineDelimiter))
        {
            _lineDelimiter = protocolDefinition.Parsing.LineDelimiter
                .Replace("\\r", "\r")
                .Replace("\\n", "\n")
                .Replace("\\t", "\t");
        }
    }

    public override async Task<WeightReading?> ReadWeightAsync(CancellationToken cancellationToken = default)
    {
        if (_status != ConnectionStatus.Connected || _stream == null)
            return null;

        try
        {
            // Handle different data modes
            switch (_protocolDefinition.Mode)
            {
                case DataMode.Continuous:
                    return await ReadContinuousAsync(cancellationToken);

                case DataMode.Demand:
                    return await ReadDemandAsync(cancellationToken);

                case DataMode.Polled:
                    return await ReadPolledAsync(cancellationToken);

                default:
                    OnErrorOccurred($"Unsupported data mode: {_protocolDefinition.Mode}");
                    return null;
            }
        }
        catch (Exception ex)
        {
            OnErrorOccurred($"Read error: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Read from continuous data stream
    /// </summary>
    private async Task<WeightReading?> ReadContinuousAsync(CancellationToken cancellationToken)
    {
        var rawData = await ReadLineAsync(cancellationToken);
        if (string.IsNullOrEmpty(rawData))
            return null;

        OnRawDataReceived(rawData);

        var reading = _engine.Parse(rawData);
        if (reading != null)
        {
            _engine.Validate(reading);
        }

        return reading;
    }

    /// <summary>
    /// Read from demand mode (send request, wait for response)
    /// </summary>
    private async Task<WeightReading?> ReadDemandAsync(CancellationToken cancellationToken)
    {
        // Send demand command if specified
        if (!string.IsNullOrEmpty(_protocolDefinition.Commands?.DemandWeight))
        {
            var success = await SendCommandAsync(_protocolDefinition.Commands.DemandWeight, cancellationToken);
            if (!success)
                return null;
        }

        // Wait for response
        var rawData = await ReadLineAsync(cancellationToken);
        if (string.IsNullOrEmpty(rawData))
            return null;

        OnRawDataReceived(rawData);

        var reading = _engine.Parse(rawData);
        if (reading != null)
        {
            _engine.Validate(reading);
        }

        return reading;
    }

    /// <summary>
    /// Read from polled mode (periodic request)
    /// </summary>
    private async Task<WeightReading?> ReadPolledAsync(CancellationToken cancellationToken)
    {
        // Check polling interval
        var interval = _protocolDefinition.PollingIntervalMs ?? 1000;
        if (_lastReadTime.HasValue)
        {
            var elapsed = (DateTime.UtcNow - _lastReadTime.Value).TotalMilliseconds;
            if (elapsed < interval)
            {
                await Task.Delay((int)(interval - elapsed), cancellationToken);
            }
        }

        return await ReadDemandAsync(cancellationToken);
    }

    /// <summary>
    /// Send zero/tare command to scale
    /// </summary>
    public async Task<bool> SendZeroCommandAsync()
    {
        if (!string.IsNullOrEmpty(_protocolDefinition.Commands?.Zero))
        {
            return await SendCommandAsync(_protocolDefinition.Commands.Zero);
        }
        return false;
    }

    /// <summary>
    /// Send tare command to scale
    /// </summary>
    public async Task<bool> SendTareCommandAsync()
    {
        if (!string.IsNullOrEmpty(_protocolDefinition.Commands?.Tare))
        {
            return await SendCommandAsync(_protocolDefinition.Commands.Tare);
        }
        return false;
    }

    /// <summary>
    /// Send print command to scale
    /// </summary>
    public async Task<bool> SendPrintCommandAsync()
    {
        if (!string.IsNullOrEmpty(_protocolDefinition.Commands?.Print))
        {
            return await SendCommandAsync(_protocolDefinition.Commands.Print);
        }
        return false;
    }
}
