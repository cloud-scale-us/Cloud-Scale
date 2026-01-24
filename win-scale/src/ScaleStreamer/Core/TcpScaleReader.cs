using System.Net.Sockets;
using System.Text;

namespace ScaleStreamer.Core;

/// <summary>
/// Reads scale data over TCP/IP connection.
/// </summary>
public class TcpScaleReader : IScaleReader
{
    private readonly string _host;
    private readonly int _port;
    private TcpClient? _client;
    private NetworkStream? _stream;
    private CancellationTokenSource? _cts;
    private Task? _readTask;
    private bool _disposed;

    public event EventHandler<WeightReceivedEventArgs>? WeightReceived;
    public event EventHandler<ConnectionStatusEventArgs>? ConnectionStatusChanged;

    public bool IsConnected => _client?.Connected ?? false;
    public string CurrentWeight { get; private set; } = "------";

    public TcpScaleReader(string host, int port)
    {
        _host = host;
        _port = port;
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
                await ConnectAsync(ct);
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

    private async Task ConnectAsync(CancellationToken ct)
    {
        _client = new TcpClient();
        _client.ReceiveTimeout = 5000;
        _client.SendTimeout = 5000;

        using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        connectCts.CancelAfter(5000);

        await _client.ConnectAsync(_host, _port, connectCts.Token);
        _stream = _client.GetStream();

        OnConnectionStatusChanged(true, $"Connected to {_host}:{_port}");
    }

    private async Task ReadDataAsync(CancellationToken ct)
    {
        var buffer = new byte[1024];
        var lineBuffer = new StringBuilder();

        while (!ct.IsCancellationRequested && _client?.Connected == true)
        {
            try
            {
                var bytesRead = await _stream!.ReadAsync(buffer, 0, buffer.Length, ct);

                if (bytesRead == 0)
                {
                    throw new IOException("Connection closed by remote host");
                }

                var text = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                lineBuffer.Append(text);

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
            _stream?.Dispose();
            _client?.Dispose();
        }
        catch { }
        finally
        {
            _stream = null;
            _client = null;
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
