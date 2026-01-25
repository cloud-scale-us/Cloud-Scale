using ScaleStreamer.Common.Models;
using System.IO.Ports;
using System.Text;

namespace ScaleStreamer.Common.Protocols;

/// <summary>
/// Base class for RS232/RS485 serial port-based scale protocols
/// </summary>
public abstract class SerialProtocolBase : BaseScaleProtocol
{
    protected SerialPort? _serialPort;
    protected StringBuilder _buffer = new();
    protected string _lineDelimiter = "\r\n";

    public override async Task<bool> ConnectAsync(ConnectionConfig config, CancellationToken cancellationToken = default)
    {
        ValidateConfig(config);

        if (config.Type != ConnectionType.RS232 && config.Type != ConnectionType.RS485)
            throw new ArgumentException("Configuration must be for RS232 or RS485 connection");

        if (string.IsNullOrEmpty(config.ComPort))
            throw new ArgumentException("COM port is required for serial connection");

        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            _config = config;
            UpdateStatus(ConnectionStatus.Connecting);

            _serialPort = new SerialPort
            {
                PortName = config.ComPort!,
                BaudRate = config.BaudRate,
                DataBits = config.DataBits,
                Parity = ParseParity(config.Parity),
                StopBits = ParseStopBits(config.StopBits),
                Handshake = ParseHandshake(config.FlowControl),
                ReadTimeout = config.TimeoutMs,
                WriteTimeout = config.TimeoutMs
            };

            _serialPort.DataReceived += SerialPort_DataReceived;
            _serialPort.ErrorReceived += SerialPort_ErrorReceived;

            _serialPort.Open();

            if (_serialPort.IsOpen)
            {
                UpdateStatus(ConnectionStatus.Connected);
                OnErrorOccurred($"Connected to {config.ComPort}");
                return true;
            }

            UpdateStatus(ConnectionStatus.Error);
            return false;
        }
        catch (Exception ex)
        {
            UpdateStatus(ConnectionStatus.Error);
            OnErrorOccurred($"Connection failed: {ex.Message}");
            return false;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public override async Task DisconnectAsync()
    {
        await _connectionLock.WaitAsync();
        try
        {
            await StopContinuousReadingAsync();

            if (_serialPort != null)
            {
                if (_serialPort.IsOpen)
                {
                    _serialPort.Close();
                }

                _serialPort.DataReceived -= SerialPort_DataReceived;
                _serialPort.ErrorReceived -= SerialPort_ErrorReceived;
                _serialPort.Dispose();
                _serialPort = null;
            }

            _buffer.Clear();
            UpdateStatus(ConnectionStatus.Disconnected);
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    /// <summary>
    /// Handle incoming serial data
    /// </summary>
    private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        try
        {
            if (_serialPort == null || !_serialPort.IsOpen)
                return;

            // Read available data
            var bytesToRead = _serialPort.BytesToRead;
            if (bytesToRead == 0)
                return;

            var buffer = new byte[bytesToRead];
            var bytesRead = _serialPort.Read(buffer, 0, bytesToRead);

            if (bytesRead > 0)
            {
                var data = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                ProcessReceivedData(data);
            }
        }
        catch (Exception ex)
        {
            OnErrorOccurred($"Data receive error: {ex.Message}");
        }
    }

    /// <summary>
    /// Handle serial port errors
    /// </summary>
    private void SerialPort_ErrorReceived(object sender, SerialErrorReceivedEventArgs e)
    {
        OnErrorOccurred($"Serial port error: {e.EventType}");
    }

    /// <summary>
    /// Process received data and extract complete lines
    /// </summary>
    protected virtual void ProcessReceivedData(string data)
    {
        _buffer.Append(data);

        // Extract complete lines
        while (true)
        {
            var delimiterIndex = _buffer.ToString().IndexOf(_lineDelimiter);
            if (delimiterIndex < 0)
                break;

            var line = _buffer.ToString(0, delimiterIndex);
            _buffer.Remove(0, delimiterIndex + _lineDelimiter.Length);

            if (!string.IsNullOrWhiteSpace(line))
            {
                OnRawDataReceived(line);
                ProcessLine(line);
            }
        }
    }

    /// <summary>
    /// Process a complete line of data (must be implemented by derived class)
    /// </summary>
    protected abstract void ProcessLine(string line);

    /// <summary>
    /// Send command to serial port
    /// </summary>
    protected async Task<bool> SendCommandAsync(string command, CancellationToken cancellationToken = default)
    {
        if (_serialPort == null || !_serialPort.IsOpen)
            return false;

        try
        {
            var data = Encoding.ASCII.GetBytes(command + _lineDelimiter);
            await _serialPort.BaseStream.WriteAsync(data, 0, data.Length, cancellationToken);
            await _serialPort.BaseStream.FlushAsync(cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            OnErrorOccurred($"Send command error: {ex.Message}");
            return false;
        }
    }

    protected override void ValidateConfig(ConnectionConfig config)
    {
        base.ValidateConfig(config);

        if (string.IsNullOrEmpty(config.ComPort))
            throw new ArgumentException("COM port is required for serial connection");

        if (config.BaudRate <= 0)
            throw new ArgumentException("Baud rate must be greater than 0");

        if (config.DataBits < 5 || config.DataBits > 8)
            throw new ArgumentException("Data bits must be between 5 and 8");
    }

    private static Parity ParseParity(string? parity)
    {
        return parity?.ToLowerInvariant() switch
        {
            "none" => Parity.None,
            "odd" => Parity.Odd,
            "even" => Parity.Even,
            "mark" => Parity.Mark,
            "space" => Parity.Space,
            _ => Parity.None
        };
    }

    private static StopBits ParseStopBits(string? stopBits)
    {
        return stopBits?.ToLowerInvariant() switch
        {
            "none" => StopBits.None,
            "one" => StopBits.One,
            "two" => StopBits.Two,
            "onepointfive" => StopBits.OnePointFive,
            _ => StopBits.One
        };
    }

    private static Handshake ParseHandshake(string? flowControl)
    {
        return flowControl?.ToLowerInvariant() switch
        {
            "none" => Handshake.None,
            "hardware" => Handshake.RequestToSend,
            "software" => Handshake.XOnXOff,
            "both" => Handshake.RequestToSendXOnXOff,
            _ => Handshake.None
        };
    }

    public override void Dispose()
    {
        DisconnectAsync().GetAwaiter().GetResult();
        base.Dispose();
    }
}
