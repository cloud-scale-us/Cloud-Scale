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
        // Configure Serilog
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "ScaleStreamer", "logs", "config-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7)
            .CreateLogger();

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
