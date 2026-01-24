using ScaleStreamer.App;
using ScaleStreamer.Config;
using System.Diagnostics;

namespace ScaleStreamer;

static class Program
{
    private static string LogFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ScaleStreamer", "app.log");

    [STAThread]
    static void Main(string[] args)
    {
        try
        {
            // Ensure log directory exists
            Directory.CreateDirectory(Path.GetDirectoryName(LogFilePath)!);

            LogMessage("Application starting...");
            LogMessage($"Working directory: {Environment.CurrentDirectory}");
            LogMessage($"Executable path: {AppDomain.CurrentDomain.BaseDirectory}");

            // Ensure single instance
            using var mutex = new Mutex(true, "ScaleStreamer_SingleInstance", out bool createdNew);
            if (!createdNew)
            {
                LogMessage("Another instance is already running. Exiting.");
                MessageBox.Show("Scale Streamer is already running.", "Scale Streamer",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetHighDpiMode(HighDpiMode.SystemAware);

            LogMessage("Loading configuration...");
            // Load configuration
            var configManager = new ConfigManager();
            var settings = configManager.Load();
            LogMessage("Configuration loaded successfully.");

            LogMessage("Creating tray application...");
            // Create and run tray application
            using var trayApp = new TrayApplication(settings, configManager);
            LogMessage("Application running. Entering message loop...");
            Application.Run();

            LogMessage("Application shutting down normally.");
        }
        catch (Exception ex)
        {
            var errorMsg = $"FATAL ERROR: {ex.Message}\n{ex.StackTrace}";
            LogMessage(errorMsg);
            MessageBox.Show($"Scale Streamer failed to start:\n\n{ex.Message}\n\nCheck log at:\n{LogFilePath}",
                "Scale Streamer Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    public static void LogMessage(string message)
    {
        try
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var logEntry = $"[{timestamp}] {message}";
            File.AppendAllText(LogFilePath, logEntry + Environment.NewLine);
            Debug.WriteLine(logEntry);
        }
        catch
        {
            // Ignore logging errors
        }
    }
}
