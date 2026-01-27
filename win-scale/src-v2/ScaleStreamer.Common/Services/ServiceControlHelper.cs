using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Serilog;

namespace ScaleStreamer.Common.Services;

/// <summary>
/// Helper class for controlling Windows services with optional elevation
/// </summary>
public static class ServiceControlHelper
{
    private static readonly ILogger _log = Log.ForContext(typeof(ServiceControlHelper));
    private const string SERVICE_NAME = "ScaleStreamerService";

    /// <summary>
    /// Check if current user has permission to control the service without elevation
    /// </summary>
    public static bool HasServiceControlPermission()
    {
        try
        {
            // Try to query the service - if we can't even query, we definitely can't control it
            var result = RunSc("query", captureOutput: true, elevated: false);
            return result.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Grant the current user permission to control the service (requires elevation)
    /// </summary>
    public static async Task<bool> GrantServiceControlPermissionAsync()
    {
        try
        {
            // Get current user SID
            var identity = WindowsIdentity.GetCurrent();
            var userSid = identity.User?.Value;

            if (string.IsNullOrEmpty(userSid))
            {
                _log.Error("Could not get current user SID");
                return false;
            }

            _log.Information("Granting service control permission to SID: {UserSid}", userSid);

            // Get current SDDL
            var sddlResult = RunSc("sdshow", captureOutput: true, elevated: true);
            if (sddlResult.ExitCode != 0 || string.IsNullOrEmpty(sddlResult.Output))
            {
                _log.Error("Failed to get current SDDL: {Output}", sddlResult.Output);
                return false;
            }

            var currentSddl = sddlResult.Output.Trim();
            _log.Debug("Current SDDL: {Sddl}", currentSddl);

            // Create ACE for user: Allow Start (RP), Stop (WP), Query Status (LC), Query Config (CC)
            var newAce = $"(A;;RPWPLCCC;;;{userSid})";

            // Insert new ACE after D:
            string newSddl;
            if (currentSddl.StartsWith("D:"))
            {
                newSddl = "D:" + newAce + currentSddl.Substring(2);
            }
            else
            {
                _log.Error("SDDL format unexpected: {Sddl}", currentSddl);
                return false;
            }

            _log.Debug("New SDDL: {Sddl}", newSddl);

            // Apply new SDDL
            var setResult = RunSc($"sdset {SERVICE_NAME} {newSddl}", captureOutput: true, elevated: true);

            if (setResult.ExitCode == 0)
            {
                _log.Information("Successfully granted service control permission");
                return true;
            }
            else
            {
                _log.Error("Failed to set SDDL: {Output}", setResult.Output);
                return false;
            }
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Error granting service control permission");
            return false;
        }
    }

    /// <summary>
    /// Start the service, attempting without elevation first
    /// </summary>
    public static async Task<ServiceControlResult> StartServiceAsync()
    {
        _log.Information("Starting service...");

        // First try without elevation
        var result = RunSc("start", captureOutput: true, elevated: false);

        if (result.ExitCode == 0 || result.Output?.Contains("RUNNING") == true)
        {
            _log.Information("Service started (no elevation needed)");
            return new ServiceControlResult(true, false, "Service started successfully");
        }

        // Check if it's an access denied error
        if (result.Output?.Contains("Access is denied") == true || result.ExitCode == 5)
        {
            _log.Warning("Access denied without elevation, trying with elevation...");
            result = RunSc("start", captureOutput: true, elevated: true);

            if (result.ExitCode == 0 || result.Output?.Contains("RUNNING") == true)
            {
                _log.Information("Service started (with elevation)");
                return new ServiceControlResult(true, true, "Service started successfully (elevated)");
            }
        }

        _log.Error("Failed to start service: {Output}", result.Output);
        return new ServiceControlResult(false, result.WasElevated, $"Failed to start service: {result.Output}");
    }

    /// <summary>
    /// Stop the service, attempting without elevation first
    /// </summary>
    public static async Task<ServiceControlResult> StopServiceAsync()
    {
        _log.Information("Stopping service...");

        // First try without elevation
        var result = RunSc("stop", captureOutput: true, elevated: false);

        if (result.ExitCode == 0 || result.Output?.Contains("STOPPED") == true)
        {
            _log.Information("Service stopped (no elevation needed)");
            return new ServiceControlResult(true, false, "Service stopped successfully");
        }

        // Check if it's an access denied error
        if (result.Output?.Contains("Access is denied") == true || result.ExitCode == 5)
        {
            _log.Warning("Access denied without elevation, trying with elevation...");
            result = RunSc("stop", captureOutput: true, elevated: true);

            if (result.ExitCode == 0 || result.Output?.Contains("STOPPED") == true)
            {
                _log.Information("Service stopped (with elevation)");
                return new ServiceControlResult(true, true, "Service stopped successfully (elevated)");
            }
        }

        _log.Error("Failed to stop service: {Output}", result.Output);
        return new ServiceControlResult(false, result.WasElevated, $"Failed to stop service: {result.Output}");
    }

    /// <summary>
    /// Restart the service
    /// </summary>
    public static async Task<ServiceControlResult> RestartServiceAsync()
    {
        _log.Information("Restarting service...");

        var stopResult = await StopServiceAsync();
        if (!stopResult.Success)
        {
            return stopResult;
        }

        // Wait for service to fully stop
        await Task.Delay(2000);

        var startResult = await StartServiceAsync();
        return new ServiceControlResult(
            startResult.Success,
            stopResult.RequiredElevation || startResult.RequiredElevation,
            startResult.Success ? "Service restarted successfully" : startResult.Message);
    }

    private static ScResult RunSc(string arguments, bool captureOutput, bool elevated)
    {
        try
        {
            var fullArgs = arguments.StartsWith("start") || arguments.StartsWith("stop") || arguments.StartsWith("query")
                ? $"{arguments.Split(' ')[0]} {SERVICE_NAME}"
                : arguments;

            var psi = new ProcessStartInfo("sc.exe", fullArgs)
            {
                UseShellExecute = elevated,
                CreateNoWindow = !elevated,
                RedirectStandardOutput = !elevated && captureOutput,
                RedirectStandardError = !elevated && captureOutput
            };

            if (elevated)
            {
                psi.Verb = "runas";
            }

            using var process = Process.Start(psi);
            if (process == null)
            {
                return new ScResult(-1, "Failed to start sc.exe", elevated);
            }

            string output = "";
            if (!elevated && captureOutput)
            {
                output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
            }

            process.WaitForExit(30000);
            return new ScResult(process.ExitCode, output, elevated);
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            // User cancelled UAC
            return new ScResult(-1, "Operation cancelled by user", elevated);
        }
        catch (Exception ex)
        {
            return new ScResult(-1, ex.Message, elevated);
        }
    }

    private record ScResult(int ExitCode, string Output, bool WasElevated);
}

public record ServiceControlResult(bool Success, bool RequiredElevation, string Message);
