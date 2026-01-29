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
    /// Grant the current user permission to control the service (requires elevation).
    /// Uses a single elevated PowerShell process to read the current SDDL, modify it,
    /// and apply it — avoiding the issue where elevated processes can't capture output
    /// through UseShellExecute + runas.
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

            // ACE: Allow Start (RP), Stop (WP), Query Status (LC), Query Config (CC), Interrogate (CR)
            var newAce = $"(A;;RPWPLCCCCR;;;{userSid})";

            // Build a PowerShell script that runs elevated to:
            // 1. Read current SDDL via sc.exe sdshow
            // 2. Insert the new ACE
            // 3. Apply via sc.exe sdset
            // Using a temp file to pass the result back since elevated process stdout isn't capturable
            var resultFile = Path.Combine(Path.GetTempPath(), $"ss_perm_{Guid.NewGuid():N}.txt");

            var psScript = $@"
$ErrorActionPreference = 'Stop'
try {{
    $output = & sc.exe sdshow {SERVICE_NAME} 2>&1
    $sddl = ($output | Where-Object {{ $_ -match '^D:' }}) -join ''
    $sddl = $sddl.Trim()
    if (-not $sddl.StartsWith('D:')) {{
        'FAIL:Could not read SDDL: ' + $sddl | Out-File '{resultFile}'
        exit 1
    }}
    $newSddl = 'D:{newAce}' + $sddl.Substring(2)
    $setOutput = & sc.exe sdset {SERVICE_NAME} $newSddl 2>&1
    if ($LASTEXITCODE -eq 0) {{
        'OK' | Out-File '{resultFile}'
    }} else {{
        ('FAIL:' + ($setOutput -join ' ')) | Out-File '{resultFile}'
    }}
}} catch {{
    ('FAIL:' + $_.Exception.Message) | Out-File '{resultFile}'
}}";

            // Encode script as base64 for safe passing
            var encodedScript = Convert.ToBase64String(
                System.Text.Encoding.Unicode.GetBytes(psScript));

            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -EncodedCommand {encodedScript}",
                UseShellExecute = true,
                Verb = "runas",
                CreateNoWindow = false,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            using var process = Process.Start(psi);
            if (process == null)
            {
                _log.Error("Failed to start elevated PowerShell process");
                return false;
            }

            process.WaitForExit(30000);

            // Read result from temp file
            if (File.Exists(resultFile))
            {
                var result = File.ReadAllText(resultFile).Trim();
                File.Delete(resultFile);

                if (result == "OK")
                {
                    _log.Information("Successfully granted service control permission");
                    return true;
                }
                else
                {
                    _log.Error("Failed to grant permission: {Result}", result);
                    return false;
                }
            }
            else
            {
                _log.Error("Permission grant: no result file found (user may have cancelled UAC)");
                return false;
            }
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            _log.Warning("User cancelled UAC prompt");
            return false;
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
            // Commands that need the service name appended
            var cmd = arguments.Split(' ')[0];
            var needsServiceName = cmd is "start" or "stop" or "query" or "sdshow";
            var fullArgs = needsServiceName
                ? $"{cmd} {SERVICE_NAME}"
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
