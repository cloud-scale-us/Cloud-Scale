using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Net;
using System.Text;
using ScaleStreamer.Common.Models;
using Serilog;

namespace ScaleStreamer.Common.Streaming;

/// <summary>
/// HTTP MJPEG streaming server for weight display
/// Much simpler than RTSP and works in VLC, browsers, and most IP camera software
/// </summary>
public class WeightHttpStream : IDisposable
{
    private static readonly ILogger _log = Log.ForContext<WeightHttpStream>();

    private readonly RtspStreamConfig _config;
    private HttpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _listenerTask;
    private WeightReading? _currentReading;
    private readonly object _readingLock = new();
    private bool _isRunning;
    private int _activeConnections = 0;

    public event EventHandler<string>? StatusChanged;
    public event EventHandler<string>? ErrorOccurred;

    public bool IsRunning => _isRunning;
    public string StreamUrl => $"http://localhost:{_config.RtspPort}/stream";

    public WeightHttpStream(RtspStreamConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public Task<bool> StartAsync()
    {
        if (_isRunning)
        {
            OnError("Server already running");
            return Task.FromResult(false);
        }

        try
        {
            _cts = new CancellationTokenSource();
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://+:{_config.RtspPort}/");
            _listener.Start();

            _isRunning = true;
            OnStatus($"HTTP MJPEG server started on port {_config.RtspPort}");
            OnStatus($"Stream URL: {StreamUrl}");
            OnStatus($"Also try: http://localhost:{_config.RtspPort}/snapshot for single frame");

            _listenerTask = AcceptConnectionsAsync(_cts.Token);

            return Task.FromResult(true);
        }
        catch (HttpListenerException ex) when (ex.ErrorCode == 5)
        {
            OnError($"Access denied. Run as admin or: netsh http add urlacl url=http://+:{_config.RtspPort}/ user=Everyone");
            _isRunning = false;
            return Task.FromResult(false);
        }
        catch (Exception ex)
        {
            OnError($"Failed to start HTTP server: {ex.Message}");
            _isRunning = false;
            return Task.FromResult(false);
        }
    }

    public async Task StopAsync()
    {
        if (!_isRunning)
            return;

        OnStatus("Stopping HTTP stream server...");

        _cts?.Cancel();
        _listener?.Stop();

        try
        {
            if (_listenerTask != null)
                await _listenerTask.WaitAsync(TimeSpan.FromSeconds(2));
        }
        catch { }

        _isRunning = false;
        OnStatus("HTTP stream server stopped");
    }

    public void UpdateWeight(WeightReading reading)
    {
        lock (_readingLock)
        {
            _currentReading = reading;
        }
    }

    private async Task AcceptConnectionsAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _listener != null && _listener.IsListening)
        {
            try
            {
                var context = await _listener.GetContextAsync();
                _ = HandleRequestAsync(context, ct);
            }
            catch (HttpListenerException) { break; }
            catch (ObjectDisposedException) { break; }
            catch (Exception ex)
            {
                OnError($"Accept error: {ex.Message}");
            }
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context, CancellationToken ct)
    {
        var request = context.Request;
        var response = context.Response;
        var path = request.Url?.AbsolutePath ?? "/";

        OnStatus($"HTTP request: {path} from {request.RemoteEndPoint}");

        try
        {
            // Check authentication if required
            if (_config.RequireAuth && !CheckAuthentication(request, response))
            {
                return; // Response already sent (401)
            }

            if (path == "/snapshot" || path == "/snapshot.jpg")
            {
                await SendSnapshotAsync(response, ct);
            }
            else if (path == "/stream" || path == "/mjpeg" || path == "/")
            {
                await SendMjpegStreamAsync(response, ct);
            }
            else
            {
                response.StatusCode = 404;
                var msg = Encoding.UTF8.GetBytes("Not Found. Try /stream or /snapshot");
                await response.OutputStream.WriteAsync(msg, ct);
            }
        }
        catch (Exception ex)
        {
            _log.Debug("HTTP handler error: {Error}", ex.Message);
        }
        finally
        {
            try { response.Close(); } catch { }
        }
    }

    /// <summary>
    /// Check HTTP Basic Authentication credentials
    /// </summary>
    private bool CheckAuthentication(HttpListenerRequest request, HttpListenerResponse response)
    {
        var authHeader = request.Headers["Authorization"];

        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            // No credentials - request authentication
            response.StatusCode = 401;
            response.AddHeader("WWW-Authenticate", "Basic realm=\"Scale Streamer\"");
            response.Close();
            OnStatus($"Auth required from {request.RemoteEndPoint}");
            return false;
        }

        try
        {
            // Decode Base64 credentials
            var encodedCredentials = authHeader.Substring(6);
            var credentialBytes = Convert.FromBase64String(encodedCredentials);
            var credentials = Encoding.UTF8.GetString(credentialBytes);
            var parts = credentials.Split(':', 2);

            if (parts.Length == 2)
            {
                var username = parts[0];
                var password = parts[1];

                if (username == _config.Username && password == _config.Password)
                {
                    OnStatus($"Auth successful for {username} from {request.RemoteEndPoint}");
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            _log.Warning("Auth decode error: {Error}", ex.Message);
        }

        // Invalid credentials
        response.StatusCode = 401;
        response.AddHeader("WWW-Authenticate", "Basic realm=\"Scale Streamer\"");
        response.Close();
        OnStatus($"Auth failed from {request.RemoteEndPoint}");
        return false;
    }

    private async Task SendSnapshotAsync(HttpListenerResponse response, CancellationToken ct)
    {
        var jpeg = GenerateWeightFrame();
        response.ContentType = "image/jpeg";
        response.ContentLength64 = jpeg.Length;
        await response.OutputStream.WriteAsync(jpeg, ct);
    }

    private async Task SendMjpegStreamAsync(HttpListenerResponse response, CancellationToken ct)
    {
        // Use standard boundary format compatible with Dahua and other NVRs
        const string boundary = "myboundary";
        response.ContentType = $"multipart/x-mixed-replace; boundary={boundary}";
        response.SendChunked = true;

        // Add headers for better NVR compatibility
        response.AddHeader("Cache-Control", "no-cache, no-store, must-revalidate");
        response.AddHeader("Pragma", "no-cache");
        response.AddHeader("Expires", "0");
        response.AddHeader("Connection", "keep-alive");

        var frameInterval = TimeSpan.FromMilliseconds(1000.0 / _config.FrameRate);
        var stream = response.OutputStream;

        var connectionId = Interlocked.Increment(ref _activeConnections);
        OnStatus($"MJPEG stream #{connectionId} started (active: {_activeConnections})");

        try
        {
            while (!ct.IsCancellationRequested)
            {
                var jpeg = GenerateWeightFrame();

                // Standard MJPEG boundary format for NVR compatibility
                var header = $"--{boundary}\r\nContent-Type: image/jpeg\r\nContent-Length: {jpeg.Length}\r\n\r\n";
                var headerBytes = Encoding.ASCII.GetBytes(header);

                await stream.WriteAsync(headerBytes, ct);
                await stream.WriteAsync(jpeg, ct);
                await stream.WriteAsync(Encoding.ASCII.GetBytes("\r\n"), ct);
                await stream.FlushAsync(ct);

                await Task.Delay(frameInterval, ct);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _log.Debug("MJPEG stream ended: {Reason}", ex.Message);
        }

        Interlocked.Decrement(ref _activeConnections);
        OnStatus($"MJPEG stream #{connectionId} disconnected (active: {_activeConnections})");
    }

    private byte[] GenerateWeightFrame()
    {
        using var bitmap = new Bitmap(_config.VideoWidth, _config.VideoHeight, PixelFormat.Format24bppRgb);
        using var graphics = Graphics.FromImage(bitmap);

        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

        // Background - dark blue-gray gradient look
        graphics.Clear(Color.FromArgb(25, 35, 50));

        // Get current weight reading
        string weightText;
        string statusText;
        Color statusColor;

        lock (_readingLock)
        {
            if (_currentReading == null)
            {
                weightText = "----.--";
                statusText = "NO DATA";
                statusColor = Color.Gray;
            }
            else
            {
                weightText = $"{_currentReading.Weight:F2}";
                statusText = _currentReading.Status.ToString().ToUpper();
                statusColor = _currentReading.Status switch
                {
                    ScaleStatus.Stable => Color.LimeGreen,
                    ScaleStatus.Motion => Color.Yellow,
                    ScaleStatus.Zero => Color.Cyan,
                    ScaleStatus.Overload => Color.Red,
                    ScaleStatus.Error => Color.Red,
                    _ => Color.White
                };
            }
        }

        // Draw weight display
        using var weightFont = new Font("Consolas", _config.FontSize, FontStyle.Bold);
        using var unitFont = new Font("Arial", _config.FontSize / 2, FontStyle.Regular);
        using var statusFont = new Font("Arial", _config.FontSize / 3, FontStyle.Bold);
        using var labelFont = new Font("Arial", 20, FontStyle.Regular);

        // Center the weight
        var weightSize = graphics.MeasureString(weightText, weightFont);
        var centerX = (_config.VideoWidth - weightSize.Width) / 2;
        var centerY = (_config.VideoHeight - weightSize.Height) / 2 - 40;

        // Weight value with shadow
        graphics.DrawString(weightText, weightFont, Brushes.Black, centerX + 3, centerY + 3);
        graphics.DrawString(weightText, weightFont, Brushes.White, centerX, centerY);

        // Unit
        string unit = _currentReading?.Unit ?? "lb";
        var unitX = centerX + weightSize.Width + 15;
        graphics.DrawString(unit, unitFont, Brushes.LightGray, unitX, centerY + weightSize.Height / 2 - 15);

        // Status indicator with background box
        var statusSize = graphics.MeasureString(statusText, statusFont);
        var statusX = (_config.VideoWidth - statusSize.Width) / 2;
        var statusY = centerY + weightSize.Height + 20;

        using var statusBrush = new SolidBrush(statusColor);
        using var statusBgBrush = new SolidBrush(Color.FromArgb(60, 60, 70));
        graphics.FillRectangle(statusBgBrush, statusX - 15, statusY - 5, statusSize.Width + 30, statusSize.Height + 10);
        graphics.DrawString(statusText, statusFont, statusBrush, statusX, statusY);

        // Scale ID label
        string scaleLabel = $"Scale: {_config.ScaleId ?? "Default"}";
        graphics.DrawString(scaleLabel, labelFont, Brushes.Gray, 15, 15);

        // Timestamp
        string timeLabel = DateTime.Now.ToString("HH:mm:ss");
        var timeSize = graphics.MeasureString(timeLabel, labelFont);
        graphics.DrawString(timeLabel, labelFont, Brushes.Gray,
            _config.VideoWidth - timeSize.Width - 15, 15);

        // Bottom branding bar
        using var barBrush = new SolidBrush(Color.FromArgb(35, 45, 60));
        graphics.FillRectangle(barBrush, 0, _config.VideoHeight - 45, _config.VideoWidth, 45);

        using var brandFont = new Font("Arial", 16, FontStyle.Bold);
        graphics.DrawString("Cloud-Scale Weight Stream", brandFont, Brushes.DarkGray, 15, _config.VideoHeight - 35);

        // Live indicator
        using var liveBrush = new SolidBrush(Color.Red);
        graphics.FillEllipse(liveBrush, _config.VideoWidth - 80, _config.VideoHeight - 32, 12, 12);
        graphics.DrawString("LIVE", labelFont, Brushes.White, _config.VideoWidth - 60, _config.VideoHeight - 35);

        // Encode to JPEG
        using var ms = new MemoryStream();
        var encoder = ImageCodecInfo.GetImageEncoders().First(e => e.FormatID == ImageFormat.Jpeg.Guid);
        var encoderParams = new EncoderParameters(1);
        encoderParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 85L);
        bitmap.Save(ms, encoder, encoderParams);

        return ms.ToArray();
    }

    private void OnStatus(string message)
    {
        _log.Information("HTTP Stream: {Message}", message);
        StatusChanged?.Invoke(this, message);
    }

    private void OnError(string message)
    {
        _log.Error("HTTP Stream Error: {Message}", message);
        ErrorOccurred?.Invoke(this, message);
    }

    public void Dispose()
    {
        StopAsync().GetAwaiter().GetResult();
        _cts?.Dispose();
        GC.SuppressFinalize(this);
    }
}
