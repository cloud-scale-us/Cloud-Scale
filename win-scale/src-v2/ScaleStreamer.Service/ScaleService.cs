using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ScaleStreamer.Common.Database;
using ScaleStreamer.Common.Models;
using ScaleStreamer.Common.IPC;
using System.Text.Json;

namespace ScaleStreamer.Service;

/// <summary>
/// Main Windows Service that manages scale connections and data streaming
/// </summary>
public class ScaleService : BackgroundService
{
    private readonly ILogger<ScaleService> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ScaleConnectionManager _connectionManager;
    private DatabaseService? _database;
    private IpcServer? _ipcServer;
    private IpcCommandHandler? _commandHandler;
    private readonly string _databasePath;
    private readonly string _protocolsPath;

    public ScaleService(
        ILogger<ScaleService> logger,
        ILoggerFactory loggerFactory,
        ScaleConnectionManager connectionManager)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
        _connectionManager = connectionManager;

        var dataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "ScaleStreamer");

        Directory.CreateDirectory(dataPath);
        _databasePath = Path.Combine(dataPath, "scalestreamer.db");

        // Protocols are in the parent directory (Scale Streamer\protocols)
        // Service runs from (Scale Streamer\Service\)
        var installRoot = Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory)?.FullName
            ?? AppDomain.CurrentDomain.BaseDirectory;
        _protocolsPath = Path.Combine(installRoot, "protocols");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Scale Service starting execution...");

        try
        {
            // Initialize database
            _database = new DatabaseService(_databasePath);
            await _database.InitializeAsync();
            _logger.LogInformation("Database initialized at: {Path}", _databasePath);

            // Load protocol templates
            await LoadProtocolTemplatesAsync();

            // Initialize IPC command handler
            _commandHandler = new IpcCommandHandler(
                _loggerFactory.CreateLogger<IpcCommandHandler>(),
                _connectionManager,
                _database);

            // Start IPC server for GUI communication
            _ipcServer = new IpcServer("ScaleStreamerPipe");
            _ipcServer.MessageReceived += OnIpcMessageReceived;
            _ipcServer.ErrorOccurred += OnIpcError;
            _ipcServer.Start();
            _logger.LogInformation("IPC Server started on pipe: ScaleStreamerPipe");

            // Load scale configurations from database
            // TODO: Implement scale configuration loading

            // Subscribe to weight readings
            _connectionManager.WeightReceived += OnWeightReceived;
            _connectionManager.ErrorOccurred += OnError;

            _logger.LogInformation("Scale Service running and ready for connections.");

            // Keep service running
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Scale Service stopping...");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in Scale Service");
            throw;
        }
    }

    /// <summary>
    /// Load protocol templates from JSON files and store in database
    /// </summary>
    private async Task LoadProtocolTemplatesAsync()
    {
        if (!Directory.Exists(_protocolsPath))
        {
            _logger.LogWarning("Protocols directory not found: {Path}", _protocolsPath);
            return;
        }

        var jsonFiles = Directory.GetFiles(_protocolsPath, "*.json", SearchOption.AllDirectories);
        _logger.LogInformation("Loading {Count} protocol templates...", jsonFiles.Length);

        foreach (var file in jsonFiles)
        {
            try
            {
                var json = await File.ReadAllTextAsync(file);
                var protocol = JsonSerializer.Deserialize<ProtocolDefinition>(json);

                if (protocol != null && _database != null)
                {
                    await _database.SaveProtocolTemplateAsync(protocol, isBuiltin: true);
                    _logger.LogInformation("Loaded protocol: {Name}", protocol.ProtocolName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load protocol from: {File}", file);
            }
        }
    }

    /// <summary>
    /// Create test connection for development/testing
    /// TODO: Remove this once GUI is implemented
    /// </summary>
    private async Task CreateTestConnectionIfNeeded()
    {
        // Check if any scales are configured
        // For now, just log that we're ready for configuration
        _logger.LogInformation("Service ready for scale configuration via GUI");
        await Task.CompletedTask;
    }

    /// <summary>
    /// Handle weight reading events
    /// </summary>
    private async void OnWeightReceived(object? sender, (string ScaleId, WeightReading Reading) e)
    {
        try
        {
            _logger.LogDebug("Weight received from {ScaleId}: {Weight} {Unit}",
                e.ScaleId, e.Reading.Weight, e.Reading.Unit);

            // Store in database
            if (_database != null)
            {
                await _database.InsertWeightReadingAsync(e.Reading);
            }

            // TODO: Send to RTSP streaming pipeline

            // Send notification to GUI via IPC
            if (_ipcServer != null)
            {
                var message = new IpcEvent
                {
                    MessageType = IpcMessageType.WeightReading,
                    ScaleId = e.ScaleId,
                    EventData = JsonSerializer.Serialize(e.Reading),
                    Timestamp = DateTime.UtcNow
                };

                await _ipcServer.SendResponseAsync(new IpcResponse
                {
                    MessageType = IpcMessageType.WeightReading,
                    Success = true,
                    Payload = JsonSerializer.Serialize(e.Reading)
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing weight reading from {ScaleId}", e.ScaleId);
        }
    }

    /// <summary>
    /// Handle error events
    /// </summary>
    private async void OnError(object? sender, (string ScaleId, string Error) e)
    {
        _logger.LogError("Error from {ScaleId}: {Error}", e.ScaleId, e.Error);

        // Log to database
        if (_database != null)
        {
            await _database.LogEventAsync("ERROR", "ScaleConnection", e.Error, scaleId: e.ScaleId);
        }

    }

    /// <summary>
    /// Handle IPC message from GUI
    /// </summary>
    private async void OnIpcMessageReceived(object? sender, IpcMessage message)
    {
        try
        {
            if (message is IpcCommand command && _commandHandler != null)
            {
                _logger.LogDebug("Processing IPC command: {MessageType}", command.MessageType);

                var response = await _commandHandler.HandleCommandAsync(command);

                // Send response back to GUI
                if (_ipcServer != null)
                {
                    await _ipcServer.SendResponseAsync(response);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling IPC message");
        }
    }

    /// <summary>
    /// Handle IPC errors
    /// </summary>
    private void OnIpcError(object? sender, string error)
    {
        _logger.LogError("IPC Error: {Error}", error);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Scale Service stopping...");

        // Stop IPC server
        if (_ipcServer != null)
        {
            _ipcServer.MessageReceived -= OnIpcMessageReceived;
            _ipcServer.ErrorOccurred -= OnIpcError;
            await _ipcServer.StopAsync();
            _ipcServer.Dispose();
        }

        // Unsubscribe from events
        _connectionManager.WeightReceived -= OnWeightReceived;
        _connectionManager.ErrorOccurred -= OnError;

        // Disconnect all scales
        await _connectionManager.DisconnectAllAsync();

        // Close database
        _database?.Dispose();

        await base.StopAsync(cancellationToken);
        _logger.LogInformation("Scale Service stopped.");
    }
}
