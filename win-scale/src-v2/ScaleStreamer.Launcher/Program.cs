using System.Diagnostics;
using System.ServiceProcess;

namespace ScaleStreamer.Launcher;

/// <summary>
/// Launcher application that ensures the service is running before opening the configuration GUI
/// </summary>
static class Program
{
    private const string SERVICE_NAME = "ScaleStreamerService";
    private const string CONFIG_EXE = "ScaleStreamer.Config.exe";

    [STAThread]
    static void Main()
    {
        try
        {
            // Check if service is running, start if needed
            EnsureServiceRunning();

            // Launch configuration GUI
            LaunchConfigurationGUI();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Failed to start Scale Streamer:\n\n{ex.Message}",
                "Scale Streamer Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private static void EnsureServiceRunning()
    {
        try
        {
            using var service = new ServiceController(SERVICE_NAME);

            // Check current status
            var status = service.Status;

            if (status == ServiceControllerStatus.Running)
            {
                // Service is already running
                return;
            }

            if (status == ServiceControllerStatus.Stopped || status == ServiceControllerStatus.Paused)
            {
                // Try to start the service
                service.Start();
                service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(10));
            }
            else if (status == ServiceControllerStatus.StartPending)
            {
                // Wait for service to start
                service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(10));
            }
        }
        catch (InvalidOperationException)
        {
            // Service not found - show error
            MessageBox.Show(
                "Scale Streamer Service is not installed.\n\nPlease run the installer to install the service.",
                "Service Not Found",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            throw;
        }
        catch (System.ServiceProcess.TimeoutException)
        {
            // Service didn't start in time - show warning but continue
            var result = MessageBox.Show(
                "Scale Streamer Service is taking longer than expected to start.\n\nDo you want to open the configuration anyway?",
                "Service Starting",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result == DialogResult.No)
            {
                throw new Exception("Service startup cancelled by user");
            }
        }
    }

    private static void LaunchConfigurationGUI()
    {
        // Get the directory where this launcher is running from
        var launcherDir = AppDomain.CurrentDomain.BaseDirectory;

        // Config GUI is in the parent directory's Config folder
        var installRoot = Directory.GetParent(launcherDir)?.FullName;
        if (installRoot == null)
        {
            throw new Exception("Could not determine installation directory");
        }

        var configPath = Path.Combine(installRoot, "Config", CONFIG_EXE);

        if (!File.Exists(configPath))
        {
            throw new Exception($"Configuration GUI not found at: {configPath}");
        }

        // Launch the configuration GUI
        var startInfo = new ProcessStartInfo
        {
            FileName = configPath,
            WorkingDirectory = Path.GetDirectoryName(configPath),
            UseShellExecute = true
        };

        Process.Start(startInfo);
    }
}
