using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ScaleStreamer.Common.Database;
using ScaleStreamer.Common.Models;
using ScaleStreamer.Common.IPC;
using ScaleStreamer.Common.Settings;
using ScaleStreamer.Common.Streaming;
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
    private WeightRtspServer? _rtspServer;
    private readonly string _databasePath;
    private readonly string _protocolsPath;
    private CancellationTokenSource? _settingsDebounce;
    private readonly object _settingsLock = new();

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
        // First try registry, then use BaseDirectory parent
        var baseDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        var installRoot = Path.GetDirectoryName(baseDir);
        if (string.IsNullOrEmpty(installRoot))
        {
            _logger.LogWarning("Could not determine install root from BaseDirectory: {BaseDir}", baseDir);
            installRoot = baseDir;
        }
        _protocolsPath = Path.Combine(installRoot, "protocols");
        _logger.LogInformation("Protocols path resolved to: {Path}", _protocolsPath);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Scale Service starting execution...");

        try
        {
            // Load and watch settings file
            var settings = AppSettings.Instance;
            settings.SettingsChanged += OnSettingsChanged;
            settings.StartWatching();
            _logger.LogInformation("Settings loaded from: {Path}. ScaleHost={Host}, ScalePort={Port}",
                AppSettings.SettingsFilePath, settings.ScaleConnection.Host, settings.ScaleConnection.Port);

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

            // Subscribe to weight readings and status changes
            _connectionManager.WeightReceived += OnWeightReceived;
            _connectionManager.ErrorOccurred += OnError;
            _connectionManager.StatusChanged += OnStatusChanged;

            // Start RTSP streaming server
            var rtspSettings = AppSettings.Instance.RtspStream;
            if (rtspSettings.Enabled)
            {
                await StartRtspServerAsync(rtspSettings);
            }
            else
            {
                _logger.LogInformation("RTSP streaming is disabled in settings");
            }

            _logger.LogInformation("Scale Service running and ready for connections.");

            // Auto-connect to scale from settings
            await ConnectFromSettingsAsync();

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
        _logger.LogInformation("Found {Count} protocol template files in {Path}", jsonFiles.Length, _protocolsPath);

        foreach (var file in jsonFiles)
        {
            try
            {
                _logger.LogDebug("Loading protocol from: {File}", file);
                var json = await File.ReadAllTextAsync(file);

                // Configure JSON options to ignore unknown properties (allows extra documentation fields)
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Skip
                };

                var protocol = JsonSerializer.Deserialize<ProtocolDefinition>(json, options);

                if (protocol != null && _database != null)
                {
                    await _database.SaveProtocolTemplateAsync(protocol, isBuiltin: true);
                    _logger.LogInformation("Loaded protocol template: {Name} v{Version}", protocol.ProtocolName, protocol.Version);
                }
                else if (protocol == null)
                {
                    _logger.LogWarning("Protocol deserialized to null from: {File}", file);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load protocol from: {File}. Error: {Message}", file, ex.Message);
            }
        }
    }

    /// <summary>
    /// Connect to scale using settings from AppSettings
    /// </summary>
    private async Task ConnectFromSettingsAsync()
    {
        try
        {
            var scaleSettings = AppSettings.Instance.ScaleConnection;

            // Determine if this is a serial or TCP connection
            var isSerial = scaleSettings.ConnectionType == "RS232" || scaleSettings.ConnectionType == "RS485";

            // Check if we have valid connection configured
            if (isSerial)
            {
                if (string.IsNullOrEmpty(scaleSettings.ComPort))
                {
                    _logger.LogInformation("No serial COM port configured in settings. Waiting for GUI configuration.");
                    return;
                }
                _logger.LogInformation("Auto-connecting to scale via {Type}: {ComPort} (Protocol: {Protocol})",
                    scaleSettings.ConnectionType, scaleSettings.ComPort, scaleSettings.Protocol);
            }
            else
            {
                if (string.IsNullOrEmpty(scaleSettings.Host) || scaleSettings.Port <= 0)
                {
                    _logger.LogInformation("No scale connection configured in settings. Waiting for GUI configuration.");
                    return;
                }
                _logger.LogInformation("Auto-connecting to scale: {Host}:{Port} (Protocol: {Protocol})",
                    scaleSettings.Host, scaleSettings.Port, scaleSettings.Protocol);
            }

            // Register the scale in the database first (required for foreign key constraint)
            var connectionInfo = isSerial
                ? $"{scaleSettings.ConnectionType}:{scaleSettings.ComPort}"
                : $"{scaleSettings.Host}:{scaleSettings.Port}";

            if (_database != null)
            {
                await _database.RegisterScaleAsync(
                    scaleSettings.ScaleId,
                    scaleSettings.ScaleId,
                    connectionInfo,
                    scaleSettings.Protocol);
                _logger.LogInformation("Scale registered in database: {ScaleId}", scaleSettings.ScaleId);
            }

            // Map connection type
            var connectionType = scaleSettings.ConnectionType switch
            {
                "RS232" => ConnectionType.RS232,
                "RS485" => ConnectionType.RS485,
                _ => ConnectionType.TcpIp
            };

            // Try to load parsing config from protocol template
            var parsing = LoadParsingFromProtocol(scaleSettings.Protocol);

            // Create a ProtocolDefinition from settings
            var protocol = new ProtocolDefinition
            {
                ProtocolName = scaleSettings.Protocol ?? "Generic ASCII",
                Manufacturer = scaleSettings.Manufacturer ?? "Generic",
                DataFormat = DataFormat.ASCII,
                Mode = DataMode.Continuous,
                Connection = new ConnectionConfig
                {
                    Type = connectionType,
                    Host = scaleSettings.Host,
                    Port = scaleSettings.Port,
                    TimeoutMs = scaleSettings.TimeoutMs,
                    AutoReconnect = scaleSettings.AutoReconnect,
                    ReconnectIntervalSeconds = scaleSettings.ReconnectIntervalSeconds,
                    ComPort = scaleSettings.ComPort,
                    BaudRate = scaleSettings.BaudRate,
                    DataBits = scaleSettings.DataBits,
                    Parity = scaleSettings.Parity,
                    StopBits = scaleSettings.StopBits,
                    FlowControl = scaleSettings.FlowControl
                },
                Parsing = parsing
            };

            var connected = await _connectionManager.AddScaleAsync(scaleSettings.ScaleId, protocol);

            if (connected)
            {
                _logger.LogInformation("Successfully connected to scale: {ScaleId} via {Connection}",
                    scaleSettings.ScaleId, connectionInfo);
            }
            else
            {
                _logger.LogWarning("Failed to connect to scale: {ScaleId} at {Connection}",
                    scaleSettings.ScaleId, connectionInfo);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error auto-connecting to scale from settings");
        }
    }

    /// <summary>
    /// Load parsing configuration from protocol template JSON file
    /// </summary>
    private ParsingConfig LoadParsingFromProtocol(string? protocolName)
    {
        // Default fallback parsing
        var defaultParsing = new ParsingConfig
        {
            LineDelimiter = "\r\n",
            FieldSeparator = "\\s+",
            Fields = new List<FieldDefinition>
            {
                new FieldDefinition { Name = "status", DataType = "string", Position = 0 },
                new FieldDefinition { Name = "weight", DataType = "float", Position = 1 }
            }
        };

        if (string.IsNullOrEmpty(protocolName))
            return defaultParsing;

        try
        {
            // Look for protocol templates in the protocols directory
            var basePath = AppContext.BaseDirectory;
            var protocolDirs = new[]
            {
                Path.Combine(basePath, "..", "protocols"),
                Path.Combine(basePath, "protocols"),
                @"C:\Program Files\Scale Streamer\protocols"
            };

            foreach (var protocolDir in protocolDirs)
            {
                if (!Directory.Exists(protocolDir)) continue;

                // Search all JSON files for matching protocol name
                foreach (var file in Directory.GetFiles(protocolDir, "*.json", SearchOption.AllDirectories))
                {
                    try
                    {
                        var json = File.ReadAllText(file);
                        var def = System.Text.Json.JsonSerializer.Deserialize<ProtocolDefinition>(json);
                        if (def?.ProtocolName?.Equals(protocolName, StringComparison.OrdinalIgnoreCase) == true)
                        {
                            _logger.LogInformation("Loaded protocol template from {File}", file);
                            return def.Parsing ?? defaultParsing;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to parse protocol file: {File}", file);
                    }
                }
            }

            _logger.LogInformation("No protocol template found for '{Protocol}', using default parsing", protocolName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error loading protocol templates");
        }

        return defaultParsing;
    }

    /// <summary>
    /// Handle weight reading events
    /// </summary>
    private async void OnWeightReceived(object? sender, (string ScaleId, WeightReading Reading) e)
    {
        try
        {
            _logger.LogInformation("Weight received from {ScaleId}: {Weight} {Unit}",
                e.ScaleId, e.Reading.Weight, e.Reading.Unit);

            // Store in database
            if (_database != null)
            {
                await _database.InsertWeightReadingAsync(e.Reading);
            }

            // Send to RTSP streaming pipeline
            _rtspServer?.UpdateWeight(e.Reading);

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
    /// Handle scale connection status changes and broadcast to GUI
    /// </summary>
    private async void OnStatusChanged(object? sender, (string ScaleId, ConnectionStatus Status) e)
    {
        try
        {
            _logger.LogInformation("Scale {ScaleId} connection status changed to: {Status}", e.ScaleId, e.Status);

            if (_ipcServer != null)
            {
                await _ipcServer.SendResponseAsync(new IpcResponse
                {
                    MessageType = IpcMessageType.ConnectionStatus,
                    Success = true,
                    Payload = JsonSerializer.Serialize(new
                    {
                        ScaleId = e.ScaleId,
                        Status = e.Status.ToString(),
                        Connected = e.Status == ConnectionStatus.Connected,
                        Timestamp = DateTime.UtcNow
                    })
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting status change for {ScaleId}", e.ScaleId);
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

    /// <summary>
    /// Start the RTSP streaming server
    /// </summary>
    private async Task StartRtspServerAsync(RtspStreamSettings settings)
    {
        try
        {
            var config = new RtspStreamConfig
            {
                RtspPort = settings.Port,
                VideoWidth = settings.VideoWidth,
                VideoHeight = settings.VideoHeight,
                FrameRate = settings.FrameRate,
                FontSize = settings.FontSize,
                ScaleId = AppSettings.Instance.ScaleConnection.ScaleId,
                RequireAuth = settings.RequireAuth,
                Username = settings.Username,
                Password = settings.Password
            };

            _rtspServer = new WeightRtspServer(config);
            _rtspServer.StatusChanged += (s, msg) => _logger.LogInformation("RTSP: {Message}", msg);
            _rtspServer.ErrorOccurred += (s, err) => _logger.LogError("RTSP Error: {Error}", err);

            var started = await _rtspServer.StartAsync();
            if (started)
            {
                _logger.LogInformation("RTSP streaming started: {Url}", _rtspServer.StreamUrl);
            }
            else
            {
                _logger.LogError("Failed to start RTSP streaming server");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting RTSP stream server");
        }
    }

    /// <summary>
    /// Handle settings file changes (auto-reload when Config app saves)
    /// Uses debouncing to prevent multiple reconnection attempts
    /// </summary>
    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        // Debounce settings changes - wait 500ms after last change before applying
        lock (_settingsLock)
        {
            _settingsDebounce?.Cancel();
            _settingsDebounce = new CancellationTokenSource();
        }

        var token = _settingsDebounce!.Token;
        var self = this;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(500, token);

                var settings = AppSettings.Instance.ScaleConnection;
                var isSerial = settings.ConnectionType == "RS232" || settings.ConnectionType == "RS485";

                self._logger.LogInformation("Settings changed! ConnectionType={Type}, Host={Host}, Port={Port}, ComPort={ComPort}, AutoReconnect={AutoReconnect}",
                    settings.ConnectionType, settings.Host, settings.Port, settings.ComPort, settings.AutoReconnect);

                // Skip if no connection configured
                if (isSerial)
                {
                    if (string.IsNullOrEmpty(settings.ComPort))
                    {
                        self._logger.LogWarning("No COM port configured, skipping reconnection");
                        return;
                    }
                }
                else
                {
                    if (string.IsNullOrEmpty(settings.Host))
                    {
                        self._logger.LogWarning("No scale host configured, skipping reconnection");
                        return;
                    }
                }

                // Disconnect existing scale if any
                var existingScale = self._connectionManager.GetScale(settings.ScaleId);
                if (existingScale != null)
                {
                    self._logger.LogInformation("Disconnecting existing scale: {ScaleId}", settings.ScaleId);
                    await self._connectionManager.RemoveScaleAsync(settings.ScaleId);
                }

                // Reconnect with new settings
                self._logger.LogInformation("Reconnecting to scale with new settings...");
                await self.ConnectFromSettingsAsync();
            }
            catch (OperationCanceledException)
            {
                // Debounced - another change came in
            }
            catch (Exception ex)
            {
                self._logger.LogError(ex, "Error handling settings change");
            }
        });
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Scale Service stopping...");

        // Stop watching settings
        AppSettings.Instance.SettingsChanged -= OnSettingsChanged;
        AppSettings.Instance.StopWatching();

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

        // Stop RTSP stream server
        if (_rtspServer != null)
        {
            await _rtspServer.StopAsync();
            _rtspServer.Dispose();
        }

        // Disconnect all scales
        await _connectionManager.DisconnectAllAsync();

        // Close database
        _database?.Dispose();

        await base.StopAsync(cancellationToken);
        _logger.LogInformation("Scale Service stopped.");
    }
}
