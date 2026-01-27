using Serilog;

namespace ScaleStreamer.Config;

static class Program
{
    /// <summary>
    /// The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main()
    {
        // Configure Serilog with detailed logging
        var logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ScaleStreamer", "logs", "config-.log");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()  // Capture everything
            .WriteTo.File(
                logPath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: "{Timestamp:HH:mm:ss.fff} [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        Log.Information("=== Scale Streamer Config Starting ===");
        Log.Information("Log file: {LogPath}", logPath);
        Log.Information("Version: {Version}", "5.0.0");

        try
        {
            Log.Information("Scale Streamer Configuration starting...");

            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());

            Log.Information("Scale Streamer Configuration closed normally.");
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Scale Streamer Configuration crashed");
            MessageBox.Show(
                $"Fatal error: {ex.Message}\n\nSee log file for details:\n{Log.Logger}",
                "Scale Streamer Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}
