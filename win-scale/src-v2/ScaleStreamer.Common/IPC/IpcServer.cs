using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;

namespace ScaleStreamer.Common.IPC;

/// <summary>
/// Named Pipe server for Service to receive commands from GUI
/// </summary>
public class IpcServer : IDisposable
{
    private readonly string _pipeName;
    private NamedPipeServerStream? _pipeServer;
    private CancellationTokenSource? _cts;
    private Task? _listenTask;

    public event EventHandler<IpcMessage>? MessageReceived;
    public event EventHandler<string>? ErrorOccurred;

    public IpcServer(string pipeName = "ScaleStreamerPipe")
    {
        _pipeName = pipeName;
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
        if (_listenTask != null)
            throw new InvalidOperationException("Server already started");

        _cts = new CancellationTokenSource();
        _listenTask = Task.Run(() => ListenAsync(_cts.Token));
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
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Create new pipe server for each connection with security allowing all users
                var pipeSecurity = CreatePipeSecurity();
                _pipeServer = NamedPipeServerStreamAcl.Create(
                    _pipeName,
                    PipeDirection.InOut,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Message,
                    PipeOptions.Asynchronous,
                    0, // inBufferSize (0 = default)
                    0, // outBufferSize (0 = default)
                    pipeSecurity);

                // Wait for client connection
                await _pipeServer.WaitForConnectionAsync(cancellationToken);

                // Handle the connection in a separate task
                _ = Task.Run(() => HandleClientAsync(_pipeServer, cancellationToken), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"Listen error: {ex.Message}");
                await Task.Delay(1000, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Handle a client connection
    /// </summary>
    private async Task HandleClientAsync(NamedPipeServerStream pipeServer, CancellationToken cancellationToken)
    {
        try
        {
            using var reader = new StreamReader(pipeServer, Encoding.UTF8, leaveOpen: true);
            using var writer = new StreamWriter(pipeServer, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };

            while (pipeServer.IsConnected && !cancellationToken.IsCancellationRequested)
            {
                var messageJson = await reader.ReadLineAsync(cancellationToken);
                if (string.IsNullOrEmpty(messageJson))
                    break;

                // Deserialize message
                var message = JsonSerializer.Deserialize<IpcMessage>(messageJson);
                if (message != null)
                {
                    MessageReceived?.Invoke(this, message);
                }
            }
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Client handler error: {ex.Message}");
        }
        finally
        {
            try
            {
                pipeServer.Disconnect();
                pipeServer.Dispose();
            }
            catch { }
        }
    }

    /// <summary>
    /// Send response back to client
    /// </summary>
    public async Task SendResponseAsync(IpcResponse response)
    {
        if (_pipeServer == null || !_pipeServer.IsConnected)
            return;

        try
        {
            var json = JsonSerializer.Serialize(response) + "\n";
            var bytes = Encoding.UTF8.GetBytes(json);
            await _pipeServer.WriteAsync(bytes, 0, bytes.Length);
            await _pipeServer.FlushAsync();
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Send error: {ex.Message}");
        }
    }

    public void Dispose()
    {
        StopAsync().GetAwaiter().GetResult();
        _cts?.Dispose();
        GC.SuppressFinalize(this);
    }
}
