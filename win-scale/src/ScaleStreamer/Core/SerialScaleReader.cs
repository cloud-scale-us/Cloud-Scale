using System.IO.Ports;
using System.Text;

namespace ScaleStreamer.Core;

/// <summary>
/// Reads scale data over RS232 serial connection.
/// </summary>
public class SerialScaleReader : IScaleReader
{
    private readonly string _portName;
    private readonly int _baudRate;
    private readonly int _dataBits;
    private readonly Parity _parity;
    private readonly StopBits _stopBits;

    private SerialPort? _serialPort;
    private CancellationTokenSource? _cts;
    private Task? _readTask;
    private bool _disposed;

    public event EventHandler<WeightReceivedEventArgs>? WeightReceived;
    public event EventHandler<ConnectionStatusEventArgs>? ConnectionStatusChanged;

    public bool IsConnected => _serialPort?.IsOpen ?? false;
    public string CurrentWeight { get; private set; } = "------";

    public SerialScaleReader(string portName, int baudRate = 9600,
        int dataBits = 8, Parity parity = Parity.None, StopBits stopBits = StopBits.One)
    {
        _portName = portName;
        _baudRate = baudRate;
        _dataBits = dataBits;
        _parity = parity;
        _stopBits = stopBits;
    }

    public static string[] GetAvailablePorts()
    {
        return SerialPort.GetPortNames();
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _readTask = Task.Run(() => ReadLoop(_cts.Token), _cts.Token);
        await Task.CompletedTask;
    }

    public void Stop()
    {
        _cts?.Cancel();
        Disconnect();
    }

    private async Task ReadLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                Connect();
                await ReadDataAsync(ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                CurrentWeight = "------";
                OnConnectionStatusChanged(false, ex.Message);

                if (!ct.IsCancellationRequested)
                {
                    await Task.Delay(2000, ct);
                }
            }
            finally
            {
                Disconnect();
            }
        }
    }

    private void Connect()
    {
        _serialPort = new SerialPort(_portName, _baudRate, _parity, _dataBits, _stopBits)
        {
            ReadTimeout = 5000,
            WriteTimeout = 5000,
            Handshake = Handshake.None,
            DtrEnable = true,
            RtsEnable = true
        };

        _serialPort.Open();
        OnConnectionStatusChanged(true, $"Connected to {_portName}");
    }

    private async Task ReadDataAsync(CancellationToken ct)
    {
        var lineBuffer = new StringBuilder();

        while (!ct.IsCancellationRequested && _serialPort?.IsOpen == true)
        {
            try
            {
                // Read available data
                if (_serialPort.BytesToRead > 0)
                {
                    var data = _serialPort.ReadExisting();
                    lineBuffer.Append(data);

                    // Process complete lines
                    var content = lineBuffer.ToString();
                    int lastNewline;

                    while ((lastNewline = content.IndexOfAny(new[] { '\r', '\n' })) >= 0)
                    {
                        var line = content.Substring(0, lastNewline);
                        content = content.Substring(lastNewline + 1).TrimStart('\r', '\n');

                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            ProcessLine(line);
                        }
                    }

                    lineBuffer.Clear();
                    lineBuffer.Append(content);
                }

                await Task.Delay(50, ct);
            }
            catch (TimeoutException)
            {
                // Continue reading
            }
            catch (IOException) when (!ct.IsCancellationRequested)
            {
                throw;
            }
        }
    }

    private void ProcessLine(string line)
    {
        var weight = WeightParser.Parse(line);
        if (weight != null)
        {
            CurrentWeight = weight;
            OnWeightReceived(weight);
        }
    }

    private void Disconnect()
    {
        try
        {
            if (_serialPort?.IsOpen == true)
            {
                _serialPort.Close();
            }
            _serialPort?.Dispose();
        }
        catch { }
        finally
        {
            _serialPort = null;
        }
    }

    protected virtual void OnWeightReceived(string weight)
    {
        WeightReceived?.Invoke(this, new WeightReceivedEventArgs(weight));
    }

    protected virtual void OnConnectionStatusChanged(bool isConnected, string? message = null)
    {
        ConnectionStatusChanged?.Invoke(this, new ConnectionStatusEventArgs(isConnected, message));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Stop();
        _cts?.Dispose();
    }
}
