using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using Serilog;

namespace ScaleStreamer.Common.IPC;

/// <summary>
/// Named Pipe server for Service to receive commands from GUI
/// </summary>
public class IpcServer : IDisposable
{
    private static readonly ILogger _log = Log.ForContext<IpcServer>();
    private readonly string _pipeName;
    private NamedPipeServerStream? _pipeServer;
    private CancellationTokenSource? _cts;
    private Task? _listenTask;
    private int _connectionCount;

    // Track connected clients for broadcasting messages
    private readonly List<ConnectedClient> _connectedClients = new();
    private readonly object _clientsLock = new();

    public event EventHandler<IpcMessage>? MessageReceived;
    public event EventHandler<string>? ErrorOccurred;

    private class ConnectedClient
    {
        public int ConnectionId { get; set; }
        public StreamWriter Writer { get; set; } = null!;
        public bool IsConnected { get; set; } = true;
        public SemaphoreSlim WriteLock { get; } = new(1, 1);
        public int ConsecutiveWriteErrors { get; set; }
        public const int MaxWriteErrors = 3;
    }

    public IpcServer(string pipeName = "ScaleStreamerPipe")
    {
        _pipeName = pipeName;
        _log.Information("IpcServer created with pipe name: {PipeName}", pipeName);
    }

    /// <summary>
    /// Create pipe security settings that allow all users to connect
    /// </summary>
    private static PipeSecurity CreatePipeSecurity()
    {
        var pipeSecurity = new PipeSecurity();

        // Allow Everyone to read/write to the pipe
        var everyone = new SecurityIdentifier(WellKnownSidType.WorldSid, null);
        pipeSecurity.AddAccessRule(new PipeAccessRule(
            everyone,
            PipeAccessRights.ReadWrite,
            AccessControlType.Allow));

        // Allow Authenticated Users to read/write
        var authenticatedUsers = new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null);
        pipeSecurity.AddAccessRule(new PipeAccessRule(
            authenticatedUsers,
            PipeAccessRights.ReadWrite,
            AccessControlType.Allow));

        // Allow the current user (LocalSystem when running as service)
        var currentUser = WindowsIdentity.GetCurrent().User;
        if (currentUser != null)
        {
            pipeSecurity.AddAccessRule(new PipeAccessRule(
                currentUser,
                PipeAccessRights.FullControl,
                AccessControlType.Allow));
        }

        return pipeSecurity;
    }

    /// <summary>
    /// Start the IPC server
    /// </summary>
    public void Start()
    {
        _log.Information("IpcServer.Start() called");
        if (_listenTask != null)
        {
            _log.Warning("Server already started - throwing exception");
            throw new InvalidOperationException("Server already started");
        }

        _cts = new CancellationTokenSource();
        _log.Debug("Starting listener task");
        _listenTask = Task.Run(() => ListenAsync(_cts.Token));
        _log.Information("IpcServer started successfully");
    }

    /// <summary>
    /// Stop the IPC server
    /// </summary>
    public async Task StopAsync()
    {
        _cts?.Cancel();

        if (_pipeServer != null)
        {
            try
            {
                _pipeServer.Disconnect();
                _pipeServer.Dispose();
            }
            catch { }
            _pipeServer = null;
        }

        if (_listenTask != null)
        {
            await _listenTask;
            _listenTask = null;
        }
    }

    /// <summary>
    /// Listen for incoming connections
    /// </summary>
    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        _log.Information("ListenAsync started - waiting for client connections on pipe: {PipeName}", _pipeName);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                _log.Debug("Creating new NamedPipeServerStream with Byte mode");
                // Create new pipe server for each connection with security allowing all users
                // Use Byte mode (not Message mode) to match how StreamReader/StreamWriter work
                var pipeSecurity = CreatePipeSecurity();
                _pipeServer = NamedPipeServerStreamAcl.Create(
                    _pipeName,
                    PipeDirection.InOut,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous,
                    65536, // inBufferSize - 64KB for incoming commands
                    262144, // outBufferSize - 256KB for high-frequency weight broadcasts
                    pipeSecurity);

                _log.Debug("Waiting for client connection...");
                // Wait for client connection
                await _pipeServer.WaitForConnectionAsync(cancellationToken);

                _connectionCount++;
                _log.Information("Client connected! Connection #{ConnectionCount}", _connectionCount);

                // Capture the connected pipe in a local variable before the loop creates a new one
                var connectedPipe = _pipeServer;
                var connectionId = _connectionCount;

                // Handle the connection in a separate task
                _log.Debug("Starting client handler task for connection #{ConnectionId}", connectionId);
                _ = Task.Run(() => HandleClientAsync(connectedPipe, connectionId, cancellationToken), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _log.Debug("ListenAsync cancelled via CancellationToken");
                break;
            }
            catch (Exception ex)
            {
                _log.Error(ex, "ListenAsync error: {ErrorMessage}", ex.Message);
                ErrorOccurred?.Invoke(this, $"Listen error: {ex.Message}");
                await Task.Delay(1000, cancellationToken);
            }
        }

        _log.Information("ListenAsync ended. TotalConnections={ConnectionCount}", _connectionCount);
    }

    /// <summary>
    /// Handle a client connection
    /// </summary>
    private async Task HandleClientAsync(NamedPipeServerStream pipeServer, int connectionId, CancellationToken cancellationToken)
    {
        _log.Information("[Conn#{ConnectionId}] HandleClientAsync started", connectionId);
        int messageCount = 0;
        ConnectedClient? client = null;

        try
        {
            using var reader = new StreamReader(pipeServer, Encoding.UTF8, leaveOpen: true);
            var writer = new StreamWriter(pipeServer, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };

            // Register this client for broadcasting
            client = new ConnectedClient
            {
                ConnectionId = connectionId,
                Writer = writer,
                IsConnected = true
            };
            lock (_clientsLock)
            {
                _connectedClients.Add(client);
            }
            _log.Debug("[Conn#{ConnectionId}] Client registered for broadcasting. Total clients: {Count}", connectionId, _connectedClients.Count);

            // Send welcome message in a fire-and-forget manner to avoid blocking
            // The client may not be ready to read immediately, so we use ConfigureAwait(false)
            // and a timeout to prevent deadlock
            _log.Debug("[Conn#{ConnectionId}] Sending welcome message (fire-and-forget)", connectionId);
            var welcomeMessage = new IpcEvent
            {
                MessageType = IpcMessageType.ServiceStarted,
                Payload = "Connected to Scale Streamer Service"
            };
            var welcomeJson = JsonSerializer.Serialize<IpcMessage>(welcomeMessage);
            _log.Verbose("[Conn#{ConnectionId}] Welcome JSON: {Json}", connectionId, welcomeJson);

            // Fire-and-forget the welcome message - don't await to avoid blocking
            // If the client isn't ready, the write will complete when they start reading
            _ = Task.Run(async () =>
            {
                try
                {
                    await writer.WriteLineAsync(welcomeJson);
                    await writer.FlushAsync();
                    _log.Information("[Conn#{ConnectionId}] Welcome message sent successfully", connectionId);
                }
                catch (Exception ex)
                {
                    _log.Warning(ex, "[Conn#{ConnectionId}] Failed to send welcome message: {Error}", connectionId, ex.Message);
                }
            });

            while (pipeServer.IsConnected && !cancellationToken.IsCancellationRequested)
            {
                _log.Verbose("[Conn#{ConnectionId}] Waiting for message from client...", connectionId);
                var messageJson = await reader.ReadLineAsync(cancellationToken);
                if (string.IsNullOrEmpty(messageJson))
                {
                    _log.Warning("[Conn#{ConnectionId}] Received empty/null message - client disconnected", connectionId);
                    break;
                }

                messageCount++;
                _log.Debug("[Conn#{ConnectionId}] Received message #{Count}, Length={Length}", connectionId, messageCount, messageJson.Length);
                _log.Verbose("[Conn#{ConnectionId}] Message content: {Json}", connectionId, messageJson);

                // Deserialize message
                var message = JsonSerializer.Deserialize<IpcMessage>(messageJson);
                if (message != null)
                {
                    _log.Information("[Conn#{ConnectionId}] Deserialized message type: {MessageType}", connectionId, message.MessageType);
                    MessageReceived?.Invoke(this, message);
                }
                else
                {
                    _log.Warning("[Conn#{ConnectionId}] Failed to deserialize message", connectionId);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _log.Debug("[Conn#{ConnectionId}] Handler cancelled via CancellationToken", connectionId);
        }
        catch (IOException ioEx)
        {
            _log.Warning(ioEx, "[Conn#{ConnectionId}] IOException (pipe likely closed): {ErrorMessage}", connectionId, ioEx.Message);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "[Conn#{ConnectionId}] Handler error: {ErrorMessage}", connectionId, ex.Message);
            ErrorOccurred?.Invoke(this, $"Client handler error: {ex.Message}");
        }
        finally
        {
            // Unregister this client
            if (client != null)
            {
                client.IsConnected = false;
                lock (_clientsLock)
                {
                    _connectedClients.Remove(client);
                }
                _log.Debug("[Conn#{ConnectionId}] Client unregistered. Remaining clients: {Count}", connectionId, _connectedClients.Count);
            }

            _log.Debug("[Conn#{ConnectionId}] Cleaning up connection", connectionId);
            try
            {
                pipeServer.Disconnect();
                pipeServer.Dispose();
            }
            catch (Exception ex)
            {
                _log.Debug(ex, "[Conn#{ConnectionId}] Error during cleanup", connectionId);
            }
            _log.Information("[Conn#{ConnectionId}] HandleClientAsync ended. TotalMessagesReceived={Count}", connectionId, messageCount);
        }
    }

    /// <summary>
    /// Send response/event to all connected clients (broadcast)
    /// Uses fire-and-forget to prevent slow clients from blocking broadcasts
    /// </summary>
    public Task SendResponseAsync(IpcResponse response)
    {
        List<ConnectedClient> clientsCopy;
        lock (_clientsLock)
        {
            clientsCopy = _connectedClients.Where(c => c.IsConnected).ToList();
        }

        if (clientsCopy.Count == 0)
        {
            _log.Verbose("SendResponseAsync: No connected clients to send to");
            return Task.CompletedTask;
        }

        var json = JsonSerializer.Serialize<IpcMessage>(response);
        _log.Debug("Broadcasting {MessageType} to {ClientCount} clients", response.MessageType, clientsCopy.Count);

        // Fire-and-forget broadcast to each client with write serialization
        // WriteLock prevents concurrent writes from corrupting the pipe stream
        foreach (var client in clientsCopy)
        {
            var capturedClient = client; // Capture for closure
            _ = Task.Run(async () =>
            {
                // Skip if write lock is busy (another message is being written)
                // This prevents message backlog when client is slow
                if (!await capturedClient.WriteLock.WaitAsync(100))
                {
                    _log.Verbose("[Conn#{ConnectionId}] Skipping broadcast - write lock busy", capturedClient.ConnectionId);
                    return;
                }

                try
                {
                    await capturedClient.Writer.WriteLineAsync(json);
                    await capturedClient.Writer.FlushAsync();
                    capturedClient.ConsecutiveWriteErrors = 0;
                }
                catch (Exception ex)
                {
                    capturedClient.ConsecutiveWriteErrors++;
                    _log.Warning("[Conn#{ConnectionId}] Write error ({ErrorCount}/{MaxErrors}): {Error}",
                        capturedClient.ConnectionId, capturedClient.ConsecutiveWriteErrors, ConnectedClient.MaxWriteErrors, ex.Message);

                    if (capturedClient.ConsecutiveWriteErrors >= ConnectedClient.MaxWriteErrors)
                    {
                        _log.Warning("[Conn#{ConnectionId}] Max write errors reached, disconnecting client", capturedClient.ConnectionId);
                        capturedClient.IsConnected = false;
                        lock (_clientsLock)
                        {
                            _connectedClients.Remove(capturedClient);
                        }
                    }
                }
                finally
                {
                    capturedClient.WriteLock.Release();
                }
            });
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Get the number of connected clients
    /// </summary>
    public int ConnectedClientCount
    {
        get
        {
            lock (_clientsLock)
            {
                return _connectedClients.Count(c => c.IsConnected);
            }
        }
    }

    public void Dispose()
    {
        StopAsync().GetAwaiter().GetResult();
        _cts?.Dispose();
        GC.SuppressFinalize(this);
    }
}
