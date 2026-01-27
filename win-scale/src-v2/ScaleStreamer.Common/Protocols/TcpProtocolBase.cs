using ScaleStreamer.Common.Models;
using System.Net.Sockets;
using System.Text;
using Serilog;

namespace ScaleStreamer.Common.Protocols;

/// <summary>
/// Base class for TCP/IP-based scale protocols
/// Handles socket management, buffering, and line-based reading
/// </summary>
public abstract class TcpProtocolBase : BaseScaleProtocol
{
    private static readonly ILogger _log = Log.ForContext<TcpProtocolBase>();

    protected TcpClient? _client;
    protected NetworkStream? _stream;
    protected StringBuilder _buffer = new();
    protected readonly byte[] _readBuffer = new byte[8192];
    protected string _lineDelimiter = "\r\n";
    protected int _totalBytesReceived = 0;
    protected DateTime? _firstDataReceivedTime = null;

    public override async Task<bool> ConnectAsync(ConnectionConfig config, CancellationToken cancellationToken = default)
    {
        ValidateConfig(config);

        if (config.Type != ConnectionType.TcpIp)
            throw new ArgumentException("Configuration must be for TCP/IP connection");

        if (string.IsNullOrEmpty(config.Host))
            throw new ArgumentException("Host is required for TCP/IP connection");

        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            _config = config;
            UpdateStatus(ConnectionStatus.Connecting);

            _client = new TcpClient
            {
                SendTimeout = config.TimeoutMs,
                ReceiveTimeout = config.TimeoutMs
            };

            await _client.ConnectAsync(config.Host!, config.Port!.Value, cancellationToken);

            if (_client.Connected)
            {
                _stream = _client.GetStream();
                UpdateStatus(ConnectionStatus.Connected);
                _log.Information("Connected to {Host}:{Port}", config.Host, config.Port);
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
            DisconnectInternal();
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    /// <summary>
    /// Internal disconnect without lock (for use when lock is already held, e.g. reconnect)
    /// </summary>
    protected void DisconnectInternal()
    {
        StopContinuousReadingAsync().GetAwaiter().GetResult();

        if (_stream != null)
        {
            _stream.Close();
            _stream.Dispose();
            _stream = null;
        }

        if (_client != null)
        {
            _client.Close();
            _client.Dispose();
            _client = null;
        }

        _buffer.Clear();
        UpdateStatus(ConnectionStatus.Disconnected);
    }

    /// <summary>
    /// Read a single line from the TCP stream
    /// </summary>
    protected async Task<string?> ReadLineAsync(CancellationToken cancellationToken = default)
    {
        if (_stream == null || !_stream.CanRead)
            return null;

        try
        {
            // Check if we already have a complete line in the buffer
            var delimiterIndex = _buffer.ToString().IndexOf(_lineDelimiter);
            if (delimiterIndex >= 0)
            {
                var line = _buffer.ToString(0, delimiterIndex);
                _buffer.Remove(0, delimiterIndex + _lineDelimiter.Length);
                return line;
            }

            // Read more data from stream with timeout
            var timeout = _config?.TimeoutMs ?? 5000;
            using var readCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            readCts.CancelAfter(timeout);

            int bytesRead;
            try
            {
                bytesRead = await _stream.ReadAsync(_readBuffer, 0, _readBuffer.Length, readCts.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // Timeout - no data available (this is normal for polled connections)
                _log.Debug("[TCP] Read timeout after {TimeoutMs}ms waiting for data", timeout);
                return null;
            }

            if (bytesRead == 0)
            {
                // Connection closed
                OnErrorOccurred("Connection closed by remote host");
                UpdateStatus(ConnectionStatus.Error);
                return null;
            }

            // LOG: Raw data received
            _totalBytesReceived += bytesRead;
            if (_firstDataReceivedTime == null)
            {
                _firstDataReceivedTime = DateTime.UtcNow;
                _log.Debug("[TCP] First data received: {BytesRead} bytes from {Host}:{Port}", bytesRead, _config?.Host, _config?.Port);
            }

            // Log raw bytes as hex and ASCII (verbose level to avoid log spam)
            var hexDump = string.Join(" ", _readBuffer.Take(Math.Min(bytesRead, 32)).Select(b => $"{b:X2}"));
            var asciiPreview = Encoding.ASCII.GetString(_readBuffer, 0, Math.Min(bytesRead, 32))
                .Replace("\r", "\\r")
                .Replace("\n", "\\n")
                .Replace("\t", "\\t");
            _log.Verbose("[TCP] Received {BytesRead} bytes | HEX: {HexDump} | ASCII: {AsciiPreview}", bytesRead, hexDump, asciiPreview);

            // Append to buffer
            var data = Encoding.ASCII.GetString(_readBuffer, 0, bytesRead);
            _buffer.Append(data);

            // Check again for complete line
            delimiterIndex = _buffer.ToString().IndexOf(_lineDelimiter);
            if (delimiterIndex >= 0)
            {
                var line = _buffer.ToString(0, delimiterIndex);
                _buffer.Remove(0, delimiterIndex + _lineDelimiter.Length);
                _log.Verbose("[TCP] Complete line extracted: {Line}", line);
                return line;
            }

            // No complete line yet, but data was received (verbose to avoid log spam)
            var bufferPreview = _buffer.ToString().Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");
            _log.Verbose("[TCP] No complete line yet. Buffer size: {BufferLength} chars", _buffer.Length);
            return null;
        }
        catch (IOException ex)
        {
            OnErrorOccurred($"Read error: {ex.Message}");
            return null;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    /// <summary>
    /// Send a command to the scale
    /// </summary>
    protected async Task<bool> SendCommandAsync(string command, CancellationToken cancellationToken = default)
    {
        if (_stream == null || !_stream.CanWrite)
            return false;

        try
        {
            var data = Encoding.ASCII.GetBytes(command + _lineDelimiter);
            await _stream.WriteAsync(data, 0, data.Length, cancellationToken);
            await _stream.FlushAsync(cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            OnErrorOccurred($"Send command error: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Read raw bytes from the stream
    /// </summary>
    protected async Task<byte[]?> ReadBytesAsync(int count, CancellationToken cancellationToken = default)
    {
        if (_stream == null || !_stream.CanRead)
            return null;

        try
        {
            var buffer = new byte[count];
            var totalRead = 0;

            while (totalRead < count)
            {
                var bytesRead = await _stream.ReadAsync(buffer, totalRead, count - totalRead, cancellationToken);
                if (bytesRead == 0)
                {
                    // Connection closed
                    OnErrorOccurred("Connection closed while reading bytes");
                    return null;
                }
                totalRead += bytesRead;
            }

            return buffer;
        }
        catch (Exception ex)
        {
            OnErrorOccurred($"Read bytes error: {ex.Message}");
            return null;
        }
    }

    protected override void ValidateConfig(ConnectionConfig config)
    {
        base.ValidateConfig(config);

        if (string.IsNullOrEmpty(config.Host))
            throw new ArgumentException("Host is required for TCP/IP connection");

        if (!config.Port.HasValue || config.Port.Value <= 0 || config.Port.Value > 65535)
            throw new ArgumentException("Port must be between 1 and 65535");
    }

    public override void Dispose()
    {
        DisconnectAsync().GetAwaiter().GetResult();
        base.Dispose();
    }
}
