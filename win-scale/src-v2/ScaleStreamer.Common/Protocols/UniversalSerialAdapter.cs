using ScaleStreamer.Common.Models;

namespace ScaleStreamer.Common.Protocols;

/// <summary>
/// Universal serial protocol adapter that combines SerialProtocolBase with UniversalProtocolEngine
/// Handles any RS232/RS485 scale protocol defined in JSON configuration
/// </summary>
public class UniversalSerialAdapter : SerialProtocolBase
{
    private readonly ProtocolDefinition _protocolDefinition;
    private readonly UniversalProtocolEngine _engine;

    public UniversalSerialAdapter(ProtocolDefinition protocolDefinition)
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

        // Set alternate delimiters (e.g., some scales send CR-only instead of CRLF)
        if (protocolDefinition.Parsing?.AlternateDelimiters is { Count: > 0 })
        {
            _alternateDelimiters = protocolDefinition.Parsing.AlternateDelimiters
                .Select(d => d.Replace("\\r", "\r").Replace("\\n", "\n").Replace("\\t", "\t"))
                .ToArray();
        }
    }

    public override async Task<WeightReading?> ReadWeightAsync(CancellationToken cancellationToken = default)
    {
        if (_status != ConnectionStatus.Connected)
            return null;

        // For serial, we rely on the DataReceived event
        // This method is mainly for demand mode
        if (_protocolDefinition.Mode == DataMode.Demand &&
            !string.IsNullOrEmpty(_protocolDefinition.Commands?.DemandWeight))
        {
            await SendCommandAsync(_protocolDefinition.Commands.DemandWeight, cancellationToken);

            // Wait for response (simple implementation)
            await Task.Delay(100, cancellationToken);
        }

        // The actual reading will come through ProcessLine
        return null;
    }

    protected override void ProcessLine(string line)
    {
        try
        {
            var reading = _engine.Parse(line);
            if (reading != null)
            {
                _engine.Validate(reading);
                _lastReadTime = DateTime.UtcNow;
                _consecutiveErrors = 0;
                OnWeightReceived(reading);
            }
        }
        catch (Exception ex)
        {
            _consecutiveErrors++;
            OnErrorOccurred($"Parse error: {ex.Message}");
        }
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
