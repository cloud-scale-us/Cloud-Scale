using ScaleStreamer.Common.Models;
using System.Diagnostics;
using System.Text;

namespace ScaleStreamer.Common.Streaming;

/// <summary>
/// Manages RTSP streaming with FFmpeg for weight overlay
/// </summary>
public class RtspStreamManager : IDisposable
{
    private readonly RtspStreamConfig _config;
    private Process? _ffmpegProcess;
    private Process? _mediaMtxProcess;
    private WeightReading? _currentReading;
    private readonly object _readingLock = new();
    private bool _isRunning = false;

    public event EventHandler<string>? ErrorOccurred;
    public event EventHandler<string>? StatusChanged;

    public bool IsRunning => _isRunning;

    public RtspStreamManager(RtspStreamConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    /// <summary>
    /// Start RTSP streaming
    /// </summary>
    public async Task<bool> StartStreamingAsync()
    {
        if (_isRunning)
        {
            OnError("Streaming already running");
            return false;
        }

        try
        {
            OnStatusChanged("Starting MediaMTX server...");

            // Start MediaMTX RTSP server
            if (!await StartMediaMtxAsync())
            {
                OnError("Failed to start MediaMTX server");
                return false;
            }

            // Wait for MediaMTX to be ready
            await Task.Delay(2000);

            OnStatusChanged("Starting FFmpeg encoder...");

            // Start FFmpeg with weight overlay
            if (!await StartFfmpegAsync())
            {
                OnError("Failed to start FFmpeg");
                await StopMediaMtxAsync();
                return false;
            }

            _isRunning = true;
            OnStatusChanged($"Streaming started: rtsp://localhost:{_config.RtspPort}/{_config.StreamName}");
            return true;
        }
        catch (Exception ex)
        {
            OnError($"Failed to start streaming: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Stop RTSP streaming
    /// </summary>
    public async Task StopStreamingAsync()
    {
        if (!_isRunning)
            return;

        OnStatusChanged("Stopping streaming...");

        await StopFfmpegAsync();
        await StopMediaMtxAsync();

        _isRunning = false;
        OnStatusChanged("Streaming stopped");
    }

    /// <summary>
    /// Update weight reading for overlay
    /// </summary>
    public void UpdateWeight(WeightReading reading)
    {
        lock (_readingLock)
        {
            _currentReading = reading;
        }
    }

    /// <summary>
    /// Get current weight text for overlay
    /// </summary>
    private string GetWeightText()
    {
        lock (_readingLock)
        {
            if (_currentReading == null)
                return "No Weight Data";

            var status = _currentReading.Status switch
            {
                ScaleStatus.Stable => "✓",
                ScaleStatus.Motion => "~",
                ScaleStatus.Overload => "!",
                ScaleStatus.Error => "X",
                _ => "?"
            };

            return $"{status} {_currentReading.Weight:F2} {_currentReading.Unit}";
        }
    }

    private async Task<bool> StartMediaMtxAsync()
    {
        try
        {
            var mediaMtxConfig = GenerateMediaMtxConfig();
            var configPath = Path.Combine(Path.GetTempPath(), "mediamtx.yml");
            await File.WriteAllTextAsync(configPath, mediaMtxConfig);

            _mediaMtxProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _config.MediaMtxPath,
                    Arguments = configPath,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            _mediaMtxProcess.OutputDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    OnStatusChanged($"MediaMTX: {e.Data}");
            };

            _mediaMtxProcess.ErrorDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    OnError($"MediaMTX Error: {e.Data}");
            };

            _mediaMtxProcess.Start();
            _mediaMtxProcess.BeginOutputReadLine();
            _mediaMtxProcess.BeginErrorReadLine();

            return true;
        }
        catch (Exception ex)
        {
            OnError($"MediaMTX start error: {ex.Message}");
            return false;
        }
    }

    private async Task<bool> StartFfmpegAsync()
    {
        try
        {
            var ffmpegArgs = BuildFfmpegArguments();

            _ffmpegProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _config.FfmpegPath,
                    Arguments = ffmpegArgs,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    RedirectStandardInput = true,
                    CreateNoWindow = true
                }
            };

            _ffmpegProcess.OutputDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    OnStatusChanged($"FFmpeg: {e.Data}");
            };

            _ffmpegProcess.ErrorDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data) && e.Data.Contains("error", StringComparison.OrdinalIgnoreCase))
                    OnError($"FFmpeg: {e.Data}");
            };

            _ffmpegProcess.Start();
            _ffmpegProcess.BeginOutputReadLine();
            _ffmpegProcess.BeginErrorReadLine();

            // Start weight overlay updater
            _ = Task.Run(() => UpdateOverlayLoop());

            return true;
        }
        catch (Exception ex)
        {
            OnError($"FFmpeg start error: {ex.Message}");
            return false;
        }
    }

    private string BuildFfmpegArguments()
    {
        var args = new StringBuilder();

        // Input: Generate test pattern or blank video
        args.Append($"-f lavfi -i color=c=black:s={_config.VideoWidth}x{_config.VideoHeight}:r={_config.FrameRate} ");

        // Hardware acceleration (if enabled)
        if (_config.UseHardwareAcceleration)
        {
            args.Append($"-hwaccel {_config.HardwareAccelerationMethod} ");
        }

        // Video codec
        args.Append($"-c:v {_config.VideoCodec} ");

        // Bitrate
        args.Append($"-b:v {_config.VideoBitrate} ");

        // Preset for x264
        if (_config.VideoCodec == "libx264")
        {
            args.Append("-preset ultrafast ");
        }

        // Framerate
        args.Append($"-r {_config.FrameRate} ");

        // Text overlay filter (drawtext)
        // Note: Dynamic text update requires reinitializing the filter
        // For continuous update, we'll use a simpler approach with periodic restarts
        var textFilter = $"drawtext=fontfile=/Windows/Fonts/arial.ttf:" +
                        $"text='{GetWeightText()}':" +
                        $"fontsize={_config.FontSize}:" +
                        $"fontcolor={_config.FontColor}:" +
                        $"box=1:" +
                        $"boxcolor={_config.BackgroundColor}:" +
                        $"boxborderw=10:" +
                        $"x={_config.TextPositionX}:" +
                        $"y={_config.TextPositionY}";

        args.Append($"-vf \"{textFilter}\" ");

        // Output format for RTSP
        args.Append("-f rtsp ");
        args.Append($"-rtsp_transport tcp ");
        args.Append($"rtsp://localhost:{_config.RtspPort}/{_config.StreamName}");

        return args.ToString();
    }

    private string GenerateMediaMtxConfig()
    {
        return $@"
logLevel: info
logDestinations: [stdout]

rtspAddress: :{_config.RtspPort}
rtpAddress: :8000
rtcpAddress: :8001

hlsAddress: :{_config.HlsPort}
hlsAlwaysRemux: yes

paths:
  {_config.StreamName}:
    source: publisher
";
    }

    private async Task UpdateOverlayLoop()
    {
        while (_isRunning && _ffmpegProcess != null && !_ffmpegProcess.HasExited)
        {
            try
            {
                // TODO: Implement dynamic text overlay update
                // This requires either:
                // 1. Periodic FFmpeg restart (simple but causes brief interruption)
                // 2. Using zmq filter for dynamic text updates (complex)
                // 3. Using separate image overlay that updates (moderate complexity)

                await Task.Delay(_config.UpdateIntervalMs);
            }
            catch (Exception ex)
            {
                OnError($"Overlay update error: {ex.Message}");
            }
        }
    }

    private async Task StopFfmpegAsync()
    {
        if (_ffmpegProcess != null && !_ffmpegProcess.HasExited)
        {
            try
            {
                // Send 'q' to gracefully stop FFmpeg
                await _ffmpegProcess.StandardInput.WriteLineAsync("q");
                await _ffmpegProcess.StandardInput.FlushAsync();

                // Wait up to 5 seconds for graceful shutdown
                if (!_ffmpegProcess.WaitForExit(5000))
                {
                    _ffmpegProcess.Kill();
                }

                _ffmpegProcess.Dispose();
                _ffmpegProcess = null;
            }
            catch (Exception ex)
            {
                OnError($"Error stopping FFmpeg: {ex.Message}");
            }
        }
    }

    private async Task StopMediaMtxAsync()
    {
        if (_mediaMtxProcess != null && !_mediaMtxProcess.HasExited)
        {
            try
            {
                _mediaMtxProcess.Kill();

                await Task.Delay(1000);

                _mediaMtxProcess.Dispose();
                _mediaMtxProcess = null;
            }
            catch (Exception ex)
            {
                OnError($"Error stopping MediaMTX: {ex.Message}");
            }
        }
    }

    private void OnError(string error)
    {
        ErrorOccurred?.Invoke(this, error);
    }

    private void OnStatusChanged(string status)
    {
        StatusChanged?.Invoke(this, status);
    }

    public void Dispose()
    {
        StopStreamingAsync().GetAwaiter().GetResult();
        GC.SuppressFinalize(this);
    }
}
