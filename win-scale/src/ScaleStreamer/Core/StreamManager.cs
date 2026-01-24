using System.Diagnostics;
using ScaleStreamer.Config;

namespace ScaleStreamer.Core;

/// <summary>
/// Manages ffmpeg and MediaMTX processes for RTSP streaming.
/// </summary>
public class StreamManager : IDisposable
{
    private readonly AppSettings _settings;
    private readonly string _appDir;
    private readonly string _weightFile;
    private readonly string _indicatorFile;
    private readonly string _rateFile;

    private Process? _mediaMtxProcess;
    private Process? _ffmpegProcess;
    private CancellationTokenSource? _cts;
    private Task? _weightWriterTask;
    private Task? _indicatorTask;
    private string _currentWeight = "------";
    private readonly object _weightLock = new();
    private bool _disposed;
    private bool _indicatorVisible = true;

    public event EventHandler<string>? StatusChanged;
    public bool IsStreaming => _ffmpegProcess?.HasExited == false;

    public StreamManager(AppSettings settings)
    {
        _settings = settings;
        _appDir = AppContext.BaseDirectory;
        _weightFile = Path.Combine(Path.GetTempPath(), "scale_weight.txt");
        _indicatorFile = Path.Combine(Path.GetTempPath(), "scale_indicator.txt");
        _rateFile = Path.Combine(Path.GetTempPath(), "scale_rate.txt");
    }

    public void UpdateWeight(string weight)
    {
        lock (_weightLock)
        {
            _currentWeight = weight;
        }
    }

    public async Task StartAsync()
    {
        _cts = new CancellationTokenSource();

        // Initialize files
        await File.WriteAllTextAsync(_weightFile, "------");
        await File.WriteAllTextAsync(_indicatorFile, "[TX]");
        await File.WriteAllTextAsync(_rateFile, $"{_settings.Stream.Bitrate} KB/s");

        // Start weight writer task
        _weightWriterTask = Task.Run(() => WeightWriterLoop(_cts.Token));

        // Start indicator blink task
        if (_settings.Display.ShowTransmitIndicator)
        {
            _indicatorTask = Task.Run(() => IndicatorBlinkLoop(_cts.Token));
        }

        // Start MediaMTX
        StartMediaMtx();
        await Task.Delay(1000); // Wait for MediaMTX to start

        // Start ffmpeg
        StartFfmpeg();

        OnStatusChanged("Stream started");
    }

    public void Stop()
    {
        _cts?.Cancel();

        StopProcess(_ffmpegProcess, "ffmpeg");
        StopProcess(_mediaMtxProcess, "MediaMTX");

        _ffmpegProcess = null;
        _mediaMtxProcess = null;

        OnStatusChanged("Stream stopped");
    }

    private void StartMediaMtx()
    {
        var mediaMtxPath = Path.Combine(_appDir, "deps", "mediamtx", "mediamtx.exe");

        if (!File.Exists(mediaMtxPath))
        {
            throw new FileNotFoundException("MediaMTX not found. Please run the dependency download script.", mediaMtxPath);
        }

        var configPath = CreateMediaMtxConfig();

        _mediaMtxProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = mediaMtxPath,
                Arguments = configPath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = Path.GetDirectoryName(mediaMtxPath)
            },
            EnableRaisingEvents = true
        };

        _mediaMtxProcess.Start();
        OnStatusChanged("MediaMTX started");
    }

    private string CreateMediaMtxConfig()
    {
        var configPath = Path.Combine(Path.GetTempPath(), "mediamtx.yml");
        var config = $@"
rtspAddress: :{_settings.Stream.RtspPort}
hlsAddress: :8888
webrtcAddress: :8889
paths:
  all:
";
        File.WriteAllText(configPath, config);
        return configPath;
    }

    private void StartFfmpeg()
    {
        var ffmpegPath = Path.Combine(_appDir, "deps", "ffmpeg", "ffmpeg.exe");

        if (!File.Exists(ffmpegPath))
        {
            throw new FileNotFoundException("ffmpeg not found. Please run the dependency download script.", ffmpegPath);
        }

        var (width, height) = ParseResolution(_settings.Stream.Resolution);
        var rtspUrl = $"rtsp://127.0.0.1:{_settings.Stream.RtspPort}/scale";

        // Build drawtext filter
        var filter = BuildVideoFilter(width, height);

        var arguments = $"-y -re " +
            $"-f lavfi -i \"color=c=black:s={width}x{height}:r={_settings.Stream.FrameRate}\" " +
            $"-vf \"{filter}\" " +
            $"-c:v libx264 -preset ultrafast -tune zerolatency " +
            $"-pix_fmt yuv420p -g 10 -keyint_min 10 " +
            $"-b:v {_settings.Stream.Bitrate}k -maxrate {_settings.Stream.Bitrate}k -bufsize {_settings.Stream.Bitrate / 2}k " +
            $"-f rtsp -rtsp_transport tcp \"{rtspUrl}\"";

        _ffmpegProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            },
            EnableRaisingEvents = true
        };

        _ffmpegProcess.Exited += (s, e) =>
        {
            if (_cts?.IsCancellationRequested == false)
            {
                OnStatusChanged("ffmpeg exited, restarting...");
                Task.Delay(2000).ContinueWith(_ => StartFfmpeg());
            }
        };

        _ffmpegProcess.Start();
        OnStatusChanged("ffmpeg started");
    }

    private string BuildVideoFilter(int width, int height)
    {
        var fontPath = "C:/Windows/Fonts/consola.ttf";
        var fontPathBold = "C:/Windows/Fonts/consolab.ttf";
        var weightFilePath = _weightFile.Replace("\\", "/");
        var indicatorFilePath = _indicatorFile.Replace("\\", "/");
        var rateFilePath = _rateFile.Replace("\\", "/");

        var filters = new List<string>();

        // Title
        filters.Add($"drawtext=fontfile='{fontPath}':" +
            $"text='{EscapeText(_settings.Display.Title)}':" +
            $"fontcolor=gray:fontsize=24:" +
            $"x=(w-text_w)/2:y=20");

        // Timestamp (if enabled) - top right
        if (_settings.Display.ShowTimestamp)
        {
            filters.Add($"drawtext=fontfile='{fontPath}':" +
                $"text='%{{localtime\\:%Y-%m-%d %H\\:%M\\:%S}}':" +
                $"fontcolor=white:fontsize=16:" +
                $"x=w-text_w-10:y=5");
        }

        // Stream rate (if enabled) - top left
        if (_settings.Display.ShowStreamRate)
        {
            filters.Add($"drawtext=fontfile='{fontPath}':" +
                $"textfile='{rateFilePath}':reload=1:" +
                $"fontcolor=cyan:fontsize=14:" +
                $"x=10:y=5");
        }

        // Weight (center)
        filters.Add($"drawtext=fontfile='{fontPathBold}':" +
            $"textfile='{weightFilePath}':reload=0.1:" +
            $"fontcolor={_settings.Display.FontColor}:fontsize=80:" +
            $"x=(w-text_w)/2:y=(h-text_h)/2-20");

        // Unit
        filters.Add($"drawtext=fontfile='{fontPath}':" +
            $"text='{_settings.Display.Unit}':" +
            $"fontcolor={_settings.Display.FontColor}:fontsize=40:" +
            $"x=(w-text_w)/2:y=(h-text_h)/2+50");

        // Custom label (if set) - below unit
        if (!string.IsNullOrWhiteSpace(_settings.Display.CustomLabel))
        {
            filters.Add($"drawtext=fontfile='{fontPath}':" +
                $"text='{EscapeText(_settings.Display.CustomLabel)}':" +
                $"fontcolor=white:fontsize=18:" +
                $"x=(w-text_w)/2:y=h-40");
        }

        // Transmit indicator (if enabled) - bottom right, green blinking
        if (_settings.Display.ShowTransmitIndicator)
        {
            filters.Add($"drawtext=fontfile='{fontPathBold}':" +
                $"textfile='{indicatorFilePath}':reload=0.1:" +
                $"fontcolor=lime:fontsize=14:" +
                $"x=w-text_w-10:y=h-25");
        }

        return string.Join(",", filters);
    }

    private static string EscapeText(string text)
    {
        // Escape special characters for ffmpeg drawtext filter
        return text.Replace("'", "\\'")
                   .Replace(":", "\\:")
                   .Replace("\\", "\\\\");
    }

    private async Task WeightWriterLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                string weight;
                lock (_weightLock)
                {
                    weight = _currentWeight;
                }

                await File.WriteAllTextAsync(_weightFile, weight, ct);
                await Task.Delay(50, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                // Ignore write errors
            }
        }
    }

    private async Task IndicatorBlinkLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                _indicatorVisible = !_indicatorVisible;
                var indicator = _indicatorVisible ? "[TX]" : "    ";
                await File.WriteAllTextAsync(_indicatorFile, indicator, ct);
                await Task.Delay(500, ct); // Blink every 500ms
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                // Ignore write errors
            }
        }
    }

    private static (int width, int height) ParseResolution(string resolution)
    {
        var parts = resolution.Split('x');
        if (parts.Length == 2 &&
            int.TryParse(parts[0], out int w) &&
            int.TryParse(parts[1], out int h))
        {
            return (w, h);
        }
        return (640, 480);
    }

    private void StopProcess(Process? process, string name)
    {
        if (process == null || process.HasExited) return;

        try
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit(3000);
        }
        catch (Exception ex)
        {
            OnStatusChanged($"Error stopping {name}: {ex.Message}");
        }
    }

    protected virtual void OnStatusChanged(string status)
    {
        StatusChanged?.Invoke(this, status);
    }

    public string GetRtspUrl() => $"rtsp://127.0.0.1:{_settings.Stream.RtspPort}/scale";
    public string GetHlsUrl() => $"http://127.0.0.1:8888/scale/";

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Stop();
        _cts?.Dispose();
    }
}
