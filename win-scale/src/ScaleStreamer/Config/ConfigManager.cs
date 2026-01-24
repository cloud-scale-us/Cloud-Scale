using System.Text.Json;
using System.Text.Json.Serialization;

namespace ScaleStreamer.Config;

public class ConfigManager
{
    private readonly string _configPath;
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public ConfigManager()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var configDir = Path.Combine(appData, "ScaleStreamer");
        Directory.CreateDirectory(configDir);
        _configPath = Path.Combine(configDir, "appsettings.json");
    }

    public AppSettings Load()
    {
        try
        {
            if (File.Exists(_configPath))
            {
                var json = File.ReadAllText(_configPath);
                return JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions) ?? new AppSettings();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading config: {ex.Message}");
        }

        var settings = new AppSettings();
        Save(settings);
        return settings;
    }

    public void Save(AppSettings settings)
    {
        try
        {
            var json = JsonSerializer.Serialize(settings, _jsonOptions);
            File.WriteAllText(_configPath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving config: {ex.Message}");
        }
    }

    public string ConfigPath => _configPath;
}
