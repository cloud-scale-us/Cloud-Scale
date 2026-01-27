using System.Text.Json;
using System.Text.Json.Serialization;
using Serilog;

namespace ScaleStreamer.Common.Settings;

/// <summary>
/// Application settings that are persisted to JSON and shared between Service and Config
/// Settings auto-save when modified and can be watched for changes
/// </summary>
public class AppSettings
{
    private static readonly ILogger _log = Log.ForContext<AppSettings>();
    private static readonly object _lock = new();
    private static AppSettings? _instance;
    private static string? _settingsPath;
    private static FileSystemWatcher? _watcher;
    private static bool _saving;

    public event EventHandler? SettingsChanged;

    // Scale Connection Settings
    public ScaleConnectionSettings ScaleConnection { get; set; } = new();

    // RTSP Streaming Settings
    public RtspStreamSettings RtspStream { get; set; } = new();

    // Service Settings
    public ServiceSettings Service { get; set; } = new();

    // Logging Settings
    public LoggingSettings Logging { get; set; } = new();

    // ONVIF Settings
    public OnvifSettings Onvif { get; set; } = new();

    // Last modified timestamp
    [JsonIgnore]
    public DateTime LastModified { get; private set; } = DateTime.Now;

    /// <summary>
    /// Get the singleton instance, loading from file if necessary
    /// </summary>
    public static AppSettings Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= Load();
                }
            }
            return _instance;
        }
    }

    /// <summary>
    /// Get the settings file path
    /// </summary>
    public static string SettingsFilePath
    {
        get
        {
            if (_settingsPath == null)
            {
                var appDataPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "ScaleStreamer");
                Directory.CreateDirectory(appDataPath);
                _settingsPath = Path.Combine(appDataPath, "settings.json");
            }
            return _settingsPath;
        }
    }

    /// <summary>
    /// Load settings from file, or create defaults if not found
    /// </summary>
    public static AppSettings Load()
    {
        try
        {
            var path = SettingsFilePath;
            _log.Information("Loading settings from {Path}", path);

            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var settings = JsonSerializer.Deserialize<AppSettings>(json, GetJsonOptions());
                if (settings != null)
                {
                    settings.LastModified = File.GetLastWriteTime(path);
                    _log.Information("Settings loaded successfully. ScaleHost={Host}, ScalePort={Port}",
                        settings.ScaleConnection.Host, settings.ScaleConnection.Port);
                    return settings;
                }
            }

            _log.Information("No settings file found, using defaults");
            var defaultSettings = new AppSettings();
            defaultSettings.Save(); // Create the file with defaults
            return defaultSettings;
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Failed to load settings, using defaults");
            return new AppSettings();
        }
    }

    /// <summary>
    /// Save settings to file
    /// </summary>
    public void Save()
    {
        lock (_lock)
        {
            try
            {
                _saving = true;
                var path = SettingsFilePath;
                var json = JsonSerializer.Serialize(this, GetJsonOptions());
                File.WriteAllText(path, json);
                LastModified = DateTime.Now;
                _log.Information("Settings saved to {Path}", path);
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Failed to save settings");
            }
            finally
            {
                _saving = false;
            }
        }
    }

    /// <summary>
    /// Reload settings from file
    /// </summary>
    public void Reload()
    {
        lock (_lock)
        {
            try
            {
                var path = SettingsFilePath;
                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path);
                    var newSettings = JsonSerializer.Deserialize<AppSettings>(json, GetJsonOptions());
                    if (newSettings != null)
                    {
                        // Copy values from loaded settings
                        ScaleConnection = newSettings.ScaleConnection;
                        RtspStream = newSettings.RtspStream;
                        Service = newSettings.Service;
                        Logging = newSettings.Logging;
                        Onvif = newSettings.Onvif;
                        LastModified = File.GetLastWriteTime(path);

                        _log.Information("Settings reloaded. ScaleHost={Host}, ScalePort={Port}",
                            ScaleConnection.Host, ScaleConnection.Port);

                        SettingsChanged?.Invoke(this, EventArgs.Empty);
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Failed to reload settings");
            }
        }
    }

    /// <summary>
    /// Start watching for settings file changes
    /// </summary>
    public void StartWatching()
    {
        if (_watcher != null) return;

        try
        {
            var dir = Path.GetDirectoryName(SettingsFilePath)!;
            var file = Path.GetFileName(SettingsFilePath);

            _watcher = new FileSystemWatcher(dir, file)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size
            };

            _watcher.Changed += OnSettingsFileChanged;
            _watcher.EnableRaisingEvents = true;

            _log.Information("Started watching settings file for changes");
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Failed to start settings file watcher");
        }
    }

    /// <summary>
    /// Stop watching for settings file changes
    /// </summary>
    public void StopWatching()
    {
        if (_watcher != null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
            _watcher = null;
            _log.Information("Stopped watching settings file");
        }
    }

    private void OnSettingsFileChanged(object sender, FileSystemEventArgs e)
    {
        // Skip if we're the one saving
        if (_saving) return;

        // Debounce - wait a bit for file to be fully written
        Task.Delay(100).ContinueWith(_ =>
        {
            _log.Information("Settings file changed externally, reloading...");
            Reload();
        });
    }

    private static JsonSerializerOptions GetJsonOptions()
    {
        return new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }
}

/// <summary>
/// Scale connection settings
/// </summary>
public class ScaleConnectionSettings
{
    public string ScaleId { get; set; } = "";
    public string ScaleName { get; set; } = "";
    public string Location { get; set; } = "";
    public string ConnectionType { get; set; } = "TcpIp";
    public string Host { get; set; } = "";
    public int Port { get; set; } = 5001;
    public string Protocol { get; set; } = "Generic ASCII";
    public string Manufacturer { get; set; } = "Generic";
    public string MarketType { get; set; } = "Industrial";
    public int TimeoutMs { get; set; } = 5000;
    public bool AutoReconnect { get; set; } = true;
    public int ReconnectIntervalSeconds { get; set; } = 10;

    // Serial port settings (for RS-232 connections)
    public string? ComPort { get; set; }
    public int BaudRate { get; set; } = 9600;
    public int DataBits { get; set; } = 8;
    public string Parity { get; set; } = "None";
    public string StopBits { get; set; } = "One";
    public string FlowControl { get; set; } = "None";
}

/// <summary>
/// Service settings
/// </summary>
public class ServiceSettings
{
    public bool AutoStart { get; set; } = true;
    public int StatusCheckIntervalSeconds { get; set; } = 5;
    public bool EnableIpc { get; set; } = true;
    public int IpcPort { get; set; } = 0; // 0 = use named pipe, >0 = use TCP
}

/// <summary>
/// Logging settings
/// </summary>
public class LoggingSettings
{
    public string LogLevel { get; set; } = "Information";
    public int RetentionDays { get; set; } = 7;
    public int MaxFileSizeMb { get; set; } = 10;
    public bool EnableConsoleLogging { get; set; } = false;
}

/// <summary>
/// ONVIF settings
/// </summary>
public class OnvifSettings
{
    public bool Enabled { get; set; } = true;
    public int HttpPort { get; set; } = 8080;
    public bool DiscoveryEnabled { get; set; } = true;
}

/// <summary>
/// RTSP streaming settings
/// </summary>
public class RtspStreamSettings
{
    public bool Enabled { get; set; } = true;
    public int Port { get; set; } = 8554;
    public int VideoWidth { get; set; } = 1920;
    public int VideoHeight { get; set; } = 1080;
    public int FrameRate { get; set; } = 10;
    public int FontSize { get; set; } = 120;

    // Authentication settings for NVR compatibility
    public bool RequireAuth { get; set; } = false;
    public string Username { get; set; } = "admin";
    public string Password { get; set; } = "scale123";
}
