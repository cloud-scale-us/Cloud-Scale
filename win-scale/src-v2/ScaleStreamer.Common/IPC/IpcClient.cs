using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using Serilog;

namespace ScaleStreamer.Common.IPC;

/// <summary>
/// Named Pipe client for GUI to send commands to Service
/// </summary>
public class IpcClient : IDisposable
{
    private static readonly ILogger _log = Log.ForContext<IpcClient>();
    private readonly string _pipeName;
    private NamedPipeClientStream? _pipeClient;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private bool _isConnected;
    private CancellationTokenSource? _listenerCts;

    public event EventHandler<IpcMessage>? MessageReceived;
    public event EventHandler<string>? ErrorOccurred;

    public bool IsConnected => _isConnected && _pipeClient?.IsConnected == true;

    public IpcClient(string pipeName = "ScaleStreamerPipe")
    {
        _pipeName = pipeName;
        _log.Debug("IpcClient created with pipe name: {PipeName}", pipeName);
    }

    /// <summary>
    /// Connect to the service
    /// </summary>
    public async Task<bool> ConnectAsync(int timeoutMs = 5000, CancellationToken cancellationToken = default)
    {
        _log.Information("ConnectAsync starting: PipeName={PipeName}, Timeout={TimeoutMs}ms, CurrentIsConnected={IsConnected}",
            _pipeName, timeoutMs, IsConnected);

        // If already connected, return immediately
        if (IsConnected)
        {
            _log.Debug("Already connected, skipping connection attempt");
            return true;
        }

        try
        {
            // Dispose any existing (broken) connection
            if (_pipeClient != null)
            {
                _log.Debug("Disposing existing pipe connection");
                try { _pipeClient.Dispose(); } catch (Exception ex) { _log.Debug(ex, "Error disposing old pipe"); }
                _pipeClient = null;
                _reader?.Dispose();
                _reader = null;
                _writer?.Dispose();
                _writer = null;
            }

            _log.Debug("Creating new NamedPipeClientStream");
            _pipeClient = new NamedPipeClientStream(
                ".",
                _pipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous);

            _log.Debug("Starting pipe connection with timeout...");

            // Use Task.WhenAny with a delay to enforce timeout
            var connectTask = _pipeClient.ConnectAsync(cancellationToken);
            var timeoutTask = Task.Delay(timeoutMs, cancellationToken);

            var completedTask = await Task.WhenAny(connectTask, timeoutTask).ConfigureAwait(false);

            if (completedTask == timeoutTask)
            {
                _log.Warning("Connection timed out after {TimeoutMs}ms", timeoutMs);
                // Timeout occurred - dispose the pipe and return
                try { _pipeClient.Dispose(); } catch { }
                _pipeClient = null;
                ErrorOccurred?.Invoke(this, "Connection timed out");
                return false;
            }

            // Make sure the connect task completed successfully
            await connectTask.ConfigureAwait(false);
            _log.Information("Pipe connected successfully! PipeIsConnected={PipeConnected}", _pipeClient.IsConnected);

            _log.Debug("Creating StreamReader first");

            // Create reader first - this should never block
            _reader = new StreamReader(_pipeClient, Encoding.UTF8, leaveOpen: true);
            _log.Debug("StreamReader created successfully");

            // Cancel any previous listener
            if (_listenerCts != null)
            {
                _log.Debug("Cancelling previous listener task");
                _listenerCts.Cancel();
                _listenerCts.Dispose();
            }

            // Create a new CTS for the listener - this lives until Disconnect is called
            _listenerCts = new CancellationTokenSource();
            _log.Debug("Created new CancellationTokenSource for listener");

            // Set connected flag BEFORE starting listener (listener checks this flag)
            _isConnected = true;
            _log.Debug("Set _isConnected = true (early - before listener and writer)");

            // IMPORTANT: Start listener BEFORE creating StreamWriter
            // This ensures any pending data from server (welcome message) is consumed
            // and prevents potential deadlock
            _log.Debug("Starting listener task BEFORE creating StreamWriter");
            Task.Factory.StartNew(
                () => ListenForResponsesAsync(_listenerCts.Token),
                _listenerCts.Token,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);

            // Small delay to let listener start consuming the welcome message
            await Task.Delay(100).ConfigureAwait(false);
            _log.Debug("About to create StreamWriter...");

            // Create writer - now should not block since listener is reading
            _writer = new StreamWriter(_pipeClient, Encoding.UTF8, leaveOpen: true);
            _writer.AutoFlush = true;
            _log.Debug("StreamWriter created with AutoFlush=true");

            _log.Information("ConnectAsync completed successfully. IsConnected={IsConnected}", IsConnected);
            return true;
        }
        catch (OperationCanceledException)
        {
            _log.Warning("Connection was cancelled by caller");
            ErrorOccurred?.Invoke(this, "Connection was cancelled");
            return false;
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Connection error: {ErrorMessage}", ex.Message);
            ErrorOccurred?.Invoke(this, $"Connection error: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Disconnect from the service
    /// </summary>
    public void Disconnect()
    {
        _log.Information("Disconnect called. CurrentIsConnected={IsConnected}", _isConnected);
        _isConnected = false;

        // Cancel the listener task
        if (_listenerCts != null)
        {
            _log.Debug("Cancelling listener CancellationTokenSource");
            _listenerCts.Cancel();
            _listenerCts.Dispose();
            _listenerCts = null;
        }

        if (_reader != null)
        {
            _log.Debug("Disposing StreamReader");
            _reader.Dispose();
            _reader = null;
        }

        if (_writer != null)
        {
            _log.Debug("Disposing StreamWriter");
            _writer.Dispose();
            _writer = null;
        }

        if (_pipeClient != null)
        {
            _log.Debug("Disposing NamedPipeClientStream");
            _pipeClient.Dispose();
            _pipeClient = null;
        }

        _log.Information("Disconnect completed");
    }

    /// <summary>
    /// Send command to service
    /// </summary>
    public async Task<bool> SendCommandAsync(IpcCommand command, CancellationToken cancellationToken = default)
    {
        _log.Debug("SendCommandAsync called: MessageType={MessageType}, CommandId={CommandId}, IsConnected={IsConnected}",
            command.MessageType, command.CommandId, IsConnected);

        if (!IsConnected || _writer == null)
        {
            _log.Warning("SendCommandAsync failed - not connected. IsConnected={IsConnected}, WriterNull={WriterNull}",
                IsConnected, _writer == null);
            ErrorOccurred?.Invoke(this, "Not connected to service");
            return false;
        }

        try
        {
            var json = JsonSerializer.Serialize(command);
            _log.Verbose("Sending command JSON: {Json}", json);
            await _writer.WriteLineAsync(json).ConfigureAwait(false);
            await _writer.FlushAsync().ConfigureAwait(false);
            _log.Debug("Command sent successfully: {MessageType}", command.MessageType);
            return true;
        }
        catch (Exception ex)
        {
            _log.Error(ex, "SendCommandAsync error: {ErrorMessage}", ex.Message);
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
        _log.Information("ListenForResponsesAsync started. Thread={ThreadId}", Environment.CurrentManagedThreadId);
        int messageCount = 0;

        while (_isConnected && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (_reader == null)
                {
                    _log.Warning("Listener: Reader is null, breaking out of loop");
                    break;
                }

                _log.Verbose("Listener: Waiting for ReadLineAsync (message #{MessageCount})...", messageCount + 1);
                var messageJson = await _reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);

                if (string.IsNullOrEmpty(messageJson))
                {
                    _log.Warning("Listener: Received empty/null message - pipe broken. Setting _isConnected=false");
                    _isConnected = false;
                    ErrorOccurred?.Invoke(this, "Pipe broken - received empty message");
                    break;
                }

                messageCount++;
                _log.Debug("Listener: Received message #{Count}, Length={Length} chars", messageCount, messageJson.Length);
                _log.Verbose("Listener: Message content: {Json}", messageJson);

                // Deserialize message
                var message = JsonSerializer.Deserialize<IpcMessage>(messageJson);
                if (message != null)
                {
                    _log.Information("Listener: Deserialized message type: {MessageType}", message.MessageType);
                    MessageReceived?.Invoke(this, message);
                }
                else
                {
                    _log.Warning("Listener: Failed to deserialize message (returned null)");
                }
            }
            catch (OperationCanceledException)
            {
                _log.Debug("Listener: Cancelled via CancellationToken");
                break;
            }
            catch (IOException ioEx)
            {
                _log.Warning(ioEx, "Listener: IOException (pipe likely closed): {ErrorMessage}", ioEx.Message);
                ErrorOccurred?.Invoke(this, $"Pipe error: {ioEx.Message}");
                _isConnected = false;
                break;
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Listener: Unexpected error: {ErrorMessage}", ex.Message);
                ErrorOccurred?.Invoke(this, $"Listen error: {ex.Message}");
                _isConnected = false;
                break;
            }
        }

        _log.Information("ListenForResponsesAsync ended. TotalMessagesReceived={Count}, IsConnected={IsConnected}, Cancelled={Cancelled}",
            messageCount, _isConnected, cancellationToken.IsCancellationRequested);
    }

    public void Dispose()
    {
        Disconnect();
        GC.SuppressFinalize(this);
    }
}
