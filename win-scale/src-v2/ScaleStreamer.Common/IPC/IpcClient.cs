using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace ScaleStreamer.Common.IPC;

/// <summary>
/// Named Pipe client for GUI to send commands to Service
/// </summary>
public class IpcClient : IDisposable
{
    private readonly string _pipeName;
    private NamedPipeClientStream? _pipeClient;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private bool _isConnected;

    public event EventHandler<IpcMessage>? MessageReceived;
    public event EventHandler<string>? ErrorOccurred;

    public bool IsConnected => _isConnected && _pipeClient?.IsConnected == true;

    public IpcClient(string pipeName = "ScaleStreamerPipe")
    {
        _pipeName = pipeName;
    }

    /// <summary>
    /// Connect to the service
    /// </summary>
    public async Task<bool> ConnectAsync(int timeoutMs = 5000, CancellationToken cancellationToken = default)
    {
        try
        {
            _pipeClient = new NamedPipeClientStream(
                ".",
                _pipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous);

            await _pipeClient.ConnectAsync(timeoutMs, cancellationToken);

            _reader = new StreamReader(_pipeClient, Encoding.UTF8, leaveOpen: true);
            _writer = new StreamWriter(_pipeClient, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };

            _isConnected = true;

            // Start listening for responses
            _ = Task.Run(() => ListenForResponsesAsync(cancellationToken), cancellationToken);

            return true;
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Connection error: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Disconnect from the service
    /// </summary>
    public void Disconnect()
    {
        _isConnected = false;

        _reader?.Dispose();
        _reader = null;

        _writer?.Dispose();
        _writer = null;

        _pipeClient?.Dispose();
        _pipeClient = null;
    }

    /// <summary>
    /// Send command to service
    /// </summary>
    public async Task<bool> SendCommandAsync(IpcCommand command, CancellationToken cancellationToken = default)
    {
        if (!IsConnected || _writer == null)
        {
            ErrorOccurred?.Invoke(this, "Not connected to service");
            return false;
        }

        try
        {
            var json = JsonSerializer.Serialize(command);
            await _writer.WriteLineAsync(json);
            await _writer.FlushAsync();
            return true;
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Send error: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Send command and wait for response
    /// </summary>
    public async Task<IpcResponse?> SendCommandWithResponseAsync(
        IpcCommand command,
        int timeoutMs = 5000,
        CancellationToken cancellationToken = default)
    {
        var tcs = new TaskCompletionSource<IpcResponse?>();
        var commandId = command.CommandId;

        // Set up response handler
        EventHandler<IpcMessage> responseHandler = (sender, message) =>
        {
            if (message is IpcResponse response && response.CommandId == commandId)
            {
                tcs.TrySetResult(response);
            }
        };

        MessageReceived += responseHandler;

        try
        {
            // Send command
            var sent = await SendCommandAsync(command, cancellationToken);
            if (!sent)
                return null;

            // Wait for response with timeout
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeoutMs);

            var timeoutTask = Task.Delay(timeoutMs, cts.Token);
            var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

            if (completedTask == timeoutTask)
            {
                ErrorOccurred?.Invoke(this, "Response timeout");
                return null;
            }

            return await tcs.Task;
        }
        finally
        {
            MessageReceived -= responseHandler;
        }
    }

    /// <summary>
    /// Listen for responses from service
    /// </summary>
    private async Task ListenForResponsesAsync(CancellationToken cancellationToken)
    {
        while (_isConnected && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (_reader == null)
                    break;

                var messageJson = await _reader.ReadLineAsync(cancellationToken);
                if (string.IsNullOrEmpty(messageJson))
                {
                    _isConnected = false;
                    break;
                }

                // Deserialize message
                var message = JsonSerializer.Deserialize<IpcMessage>(messageJson);
                if (message != null)
                {
                    MessageReceived?.Invoke(this, message);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"Listen error: {ex.Message}");
                _isConnected = false;
                break;
            }
        }
    }

    public void Dispose()
    {
        Disconnect();
        GC.SuppressFinalize(this);
    }
}
