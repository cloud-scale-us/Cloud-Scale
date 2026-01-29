using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using ScaleStreamer.Common.Models;
using Serilog;

namespace ScaleStreamer.Common.Streaming;

/// <summary>
/// Native RTSP server for streaming weight display video.
/// Supports H.264 encoding via FFmpeg (preferred for NVR compatibility)
/// with MJPEG fallback.
/// </summary>
public class WeightRtspServer : IDisposable
{
    private static readonly ILogger _log = Log.ForContext<WeightRtspServer>();

    private readonly RtspStreamConfig _config;
    private TcpListener? _listener;
    private readonly List<RtspClientConnection> _clients = new();
    private readonly object _clientsLock = new();
    private CancellationTokenSource? _cts;
    private Task? _acceptTask;
    private Task? _streamTask;
    private Task? _h264ForwardTask;
    private WeightReading? _currentReading;
    private readonly object _readingLock = new();
    private bool _isRunning;
    private int _sessionCounter = 1000;
    private int _frameCount;

    // FFmpeg H.264 encoding
    private Process? _ffmpegProcess;
    private byte[]? _spsNalu;
    private byte[]? _ppsNalu;
    private string? _spsBase64;
    private string? _ppsBase64;
    private string? _profileLevelId;
    private bool _useH264;
    private readonly List<byte[]> _pendingNalus = new();
    private readonly object _naluLock = new();

    // Pre-allocated rendering objects (reused every frame to avoid GC pressure)
    private Bitmap? _frameBitmap;
    private Graphics? _frameGraphics;
    private byte[]? _rawFrameBuffer;
    private Font? _weightFont;
    private Font? _statusFont;
    private Font? _labelFont;
    private Font? _liveFont;
    private SolidBrush? _redBrush;
    private SolidBrush? _dimBrush;
    private SolidBrush? _liveBrush;
    private StringFormat? _centerFormat;
    private StringFormat? _rightFormat;

    public event EventHandler<string>? StatusChanged;
    public event EventHandler<string>? ErrorOccurred;

    public bool IsRunning => _isRunning;
    public string StreamUrl => $"rtsp://{LocalIpAddress}:{_config.RtspPort}/{_config.StreamName}";

    private static string LocalIpAddress
    {
        get
        {
            try
            {
                var nic = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
                    .FirstOrDefault(i =>
                        i.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Loopback &&
                        i.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Tunnel &&
                        i.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up);
                if (nic != null)
                {
                    var addr = nic.GetIPProperties().UnicastAddresses
                        .FirstOrDefault(a => a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
                    if (addr != null) return addr.Address.ToString();
                }
            }
            catch { }
            return "0.0.0.0";
        }
    }

    public WeightRtspServer(RtspStreamConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public async Task<bool> StartAsync()
    {
        if (_isRunning)
        {
            OnError("Server already running");
            return false;
        }

        try
        {
            _cts = new CancellationTokenSource();
            _listener = new TcpListener(IPAddress.Any, _config.RtspPort);
            _listener.Start();

            _isRunning = true;

            // Try to start FFmpeg for H.264 encoding
            try
            {
                _useH264 = await StartFfmpegH264Async();
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Failed to start FFmpeg H.264");
                _useH264 = false;
            }
            if (_useH264)
            {
                OnStatus("Using H.264 encoding via FFmpeg");
            }
            else
            {
                OnStatus("FFmpeg not available, falling back to MJPEG");
            }

            OnStatus($"RTSP server started on port {_config.RtspPort}");
            OnStatus($"Stream URL: {StreamUrl}");

            // Start accepting connections
            _acceptTask = AcceptConnectionsAsync(_cts.Token);

            // Start frame streaming loop (generates frames and pipes to FFmpeg when H.264)
            _streamTask = StreamFramesAsync(_cts.Token);

            return true;
        }
        catch (Exception ex)
        {
            OnError($"Failed to start RTSP server: {ex.Message}");
            _isRunning = false;
            return false;
        }
    }

    public async Task StopAsync()
    {
        if (!_isRunning)
            return;

        OnStatus("Stopping RTSP server...");

        _cts?.Cancel();
        _listener?.Stop();

        // Stop FFmpeg
        StopFfmpeg();

        // Disconnect all clients
        lock (_clientsLock)
        {
            foreach (var client in _clients)
            {
                client.Dispose();
            }
            _clients.Clear();
        }

        try
        {
            if (_acceptTask != null)
                await _acceptTask.WaitAsync(TimeSpan.FromSeconds(2));
            if (_streamTask != null)
                await _streamTask.WaitAsync(TimeSpan.FromSeconds(2));
            if (_h264ForwardTask != null)
                await _h264ForwardTask.WaitAsync(TimeSpan.FromSeconds(2));
        }
        catch (TimeoutException) { }
        catch (OperationCanceledException) { }

        _isRunning = false;
        OnStatus("RTSP server stopped");
    }

    private async Task<bool> StartFfmpegH264Async()
    {
        try
        {
            var ffmpegPath = FindFfmpeg();
            if (ffmpegPath == null)
            {
                _log.Warning("FFmpeg not found, will use MJPEG");
                return false;
            }
            _log.Information("Found FFmpeg at {Path}", ffmpegPath);

            // FFmpeg: read raw BGR24 from stdin, encode H.264 Annex B to stdout
            // Using baseline profile with zerolatency for fast startup (required for RTSP)
            var args = $"-f rawvideo -pix_fmt bgr24 -s {_config.VideoWidth}x{_config.VideoHeight} -r {_config.FrameRate} -i pipe:0 " +
                       $"-c:v libx264 -preset ultrafast -tune zerolatency -profile:v baseline -level 3.1 " +
                       $"-b:v 1024k -maxrate 1024k -bufsize 1024k -g 1 -keyint_min 1 -intra " +
                       $"-pix_fmt yuv420p -bsf:v dump_extra -f h264 pipe:1";

            _log.Information("Starting FFmpeg: {Args}", args);

            _ffmpegProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                }
            };

            _ffmpegProcess.ErrorDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    _log.Debug("FFmpeg: {Line}", e.Data);
            };

            _ffmpegProcess.Start();
            _ffmpegProcess.BeginErrorReadLine();

            // Start reading H.264 NAL units from FFmpeg stdout in background
            _h264ForwardTask = ReadH264StreamAsync(_cts!.Token);

            // Write primer frames so FFmpeg initializes and outputs SPS/PPS
            _log.Information("Writing primer frames to FFmpeg...");
            for (int i = 0; i < 3; i++)
            {
                var frame = GenerateRawFrame();
                await _ffmpegProcess.StandardInput.BaseStream.WriteAsync(frame, 0, frame.Length);
                await _ffmpegProcess.StandardInput.BaseStream.FlushAsync();
            }

            // Wait for SPS/PPS to be extracted from the H.264 stream
            for (int i = 0; i < 30; i++)
            {
                await Task.Delay(100);
                if (_spsBase64 != null && _ppsBase64 != null) break;
            }

            if (_spsBase64 == null)
            {
                _log.Warning("Could not extract SPS/PPS from H.264 stream, using defaults");
                _profileLevelId = "42C01F";
            }

            _log.Information("FFmpeg H.264 encoder started, profile-level-id={Profile}, SPS={Sps}",
                _profileLevelId ?? "42C01F", _spsBase64 ?? "none");

            return true;
        }
        catch (Exception ex)
        {
            _log.Warning("Failed to start FFmpeg H.264: {Error}", ex.Message);
            StopFfmpeg();
            return false;
        }
    }

    /// <summary>
    /// Read H.264 Annex B stream from FFmpeg stdout, parse NAL units,
    /// and send them as RTP packets to all playing clients.
    /// </summary>
    private async Task ReadH264StreamAsync(CancellationToken ct)
    {
        _log.Information("H.264 stream reader started");
        var buffer = new byte[1024 * 1024]; // 1MB read buffer
        var annexBStream = new MemoryStream();
        uint rtpTimestamp = (uint)Random.Shared.Next();
        uint timestampIncrement = 90000 / (uint)_config.FrameRate;
        long naluCount = 0;

        try
        {
            var stdout = _ffmpegProcess!.StandardOutput.BaseStream;

            while (!ct.IsCancellationRequested)
            {
                int bytesRead;
                try
                {
                    bytesRead = await stdout.ReadAsync(buffer, 0, buffer.Length, ct);
                }
                catch (OperationCanceledException) { break; }
                catch { break; }

                if (bytesRead == 0) break; // EOF

                // Append to our Annex B buffer
                annexBStream.Write(buffer, 0, bytesRead);

                // Parse complete NAL units from the buffer
                var data = annexBStream.ToArray();
                int lastNaluEnd = 0;

                var nalus = new List<(int offset, int length)>();
                int i = 0;
                while (i < data.Length - 3)
                {
                    // Look for start codes: 00 00 00 01 or 00 00 01
                    bool found3 = (data[i] == 0 && data[i + 1] == 0 && data[i + 2] == 1);
                    bool found4 = (i < data.Length - 3 && data[i] == 0 && data[i + 1] == 0 && data[i + 2] == 0 && data[i + 3] == 1);

                    if (found4 || found3)
                    {
                        int startCodeLen = found4 ? 4 : 3;
                        int naluStart = i + startCodeLen;

                        if (nalus.Count > 0)
                        {
                            // Previous NALU ends here
                            var prev = nalus[nalus.Count - 1];
                            nalus[nalus.Count - 1] = (prev.offset, i - prev.offset);
                        }

                        nalus.Add((naluStart, 0)); // length unknown yet
                        lastNaluEnd = i;
                        i = naluStart;
                    }
                    else
                    {
                        i++;
                    }
                }

                // Process all complete NAL units (all except the last which may be incomplete)
                if (nalus.Count > 1)
                {
                    for (int n = 0; n < nalus.Count - 1; n++)
                    {
                        var (offset, length) = nalus[n];
                        if (length <= 0) continue;

                        var nalu = new byte[length];
                        Array.Copy(data, offset, nalu, 0, length);

                        int naluType = nalu[0] & 0x1F;

                        // Extract SPS/PPS
                        if (naluType == 7) // SPS
                        {
                            _spsNalu = nalu;
                            _spsBase64 = Convert.ToBase64String(nalu);
                            if (nalu.Length >= 4)
                                _profileLevelId = $"{nalu[1]:X2}{nalu[2]:X2}{nalu[3]:X2}";
                            if (naluCount == 0)
                                _log.Information("H.264 SPS: {Sps}, profile={Profile}", _spsBase64, _profileLevelId);
                        }
                        else if (naluType == 8) // PPS
                        {
                            if (_ppsNalu == null)
                                _log.Information("H.264 PPS: {Pps}", Convert.ToBase64String(nalu));
                            _ppsNalu = nalu;
                            _ppsBase64 = Convert.ToBase64String(nalu);
                        }

                        // Send NAL unit as RTP to all playing clients
                        bool isLastNaluInFrame = (naluType == 5 || naluType == 1); // IDR or non-IDR slice
                        await SendNaluToClients(nalu, rtpTimestamp, isLastNaluInFrame, ct);

                        if (isLastNaluInFrame)
                            rtpTimestamp += timestampIncrement;

                        naluCount++;
                        if (naluCount % 300 == 0)
                        {
                            List<RtspClientConnection> pc;
                            lock (_clientsLock) { pc = _clients.Where(c => c.IsPlaying).ToList(); }
                            if (pc.Count > 0)
                                _log.Information("H.264: Sent {Count} NALUs, {Clients} client(s)", naluCount, pc.Count);
                        }
                    }

                    // Keep only the incomplete last NALU in the buffer
                    var lastNalu = nalus[nalus.Count - 1];
                    var remaining = data.Length - lastNaluEnd;
                    annexBStream = new MemoryStream();
                    annexBStream.Write(data, lastNaluEnd, remaining);
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _log.Error("H.264 stream reader error: {Error}", ex.Message);
        }

        _log.Information("H.264 stream reader stopped after {Count} NALUs", naluCount);
    }

    /// <summary>
    /// Send a single H.264 NAL unit to all playing clients via RTP (RFC 6184)
    /// </summary>
    private async Task SendNaluToClients(byte[] nalu, uint timestamp, bool marker, CancellationToken ct)
    {
        const int MAX_RTP_PAYLOAD = 1400;
        int naluType = nalu[0] & 0x1F;
        bool isIdr = naluType == 5;

        List<RtspClientConnection> playingClients;
        lock (_clientsLock)
        {
            playingClients = _clients.Where(c => c.IsPlaying && c.TcpClient.Connected).ToList();
        }

        if (playingClients.Count == 0) return;

        foreach (var client in playingClients)
        {
            try
            {
                // New clients must wait for an IDR frame before receiving anything
                if (!client.SentSps)
                {
                    if (!isIdr) continue; // Skip non-IDR NALUs until we get a keyframe

                    // Send SPS/PPS immediately before the IDR frame
                    if (_spsNalu != null && _ppsNalu != null)
                    {
                        _log.Information("H.264: Sending SPS ({SpsLen} bytes) + PPS ({PpsLen} bytes) + IDR ({IdrLen} bytes) to {Client}",
                            _spsNalu.Length, _ppsNalu.Length, nalu.Length, client.TcpClient.Client.RemoteEndPoint);
                        await SendSingleNaluRtp(client, _spsNalu, timestamp, false, ct);
                        await SendSingleNaluRtp(client, _ppsNalu, timestamp, false, ct);
                        client.SentSps = true;
                    }
                    else
                    {
                        continue; // No SPS/PPS available yet
                    }
                }

                if (nalu.Length <= MAX_RTP_PAYLOAD)
                {
                    await SendSingleNaluRtp(client, nalu, timestamp, marker, ct);
                }
                else
                {
                    await SendFuaNaluRtp(client, nalu, timestamp, marker, ct);
                }

                client.LastActivity = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                _log.Debug("Error sending H.264 to {Client}: {Error}",
                    client.TcpClient.Client.RemoteEndPoint, ex.Message);
                client.IsPlaying = false;
            }
        }
    }

    private async Task SendSingleNaluRtp(RtspClientConnection client, byte[] nalu, uint timestamp, bool marker, CancellationToken ct)
    {
        // RTP header (12 bytes) + NAL unit
        var packet = new byte[12 + nalu.Length];

        // RTP header
        packet[0] = 0x80; // V=2
        packet[1] = (byte)((marker ? 0x80 : 0) | 96); // PT=96
        packet[2] = (byte)(client.RtpSequenceNumber >> 8);
        packet[3] = (byte)(client.RtpSequenceNumber);
        client.RtpSequenceNumber++;
        packet[4] = (byte)(timestamp >> 24);
        packet[5] = (byte)(timestamp >> 16);
        packet[6] = (byte)(timestamp >> 8);
        packet[7] = (byte)(timestamp);
        packet[8] = (byte)(client.RtpSsrc >> 24);
        packet[9] = (byte)(client.RtpSsrc >> 16);
        packet[10] = (byte)(client.RtpSsrc >> 8);
        packet[11] = (byte)(client.RtpSsrc);

        Array.Copy(nalu, 0, packet, 12, nalu.Length);

        await WriteRtpToClient(client, packet, ct);
    }

    private async Task SendFuaNaluRtp(RtspClientConnection client, byte[] nalu, uint timestamp, bool marker, CancellationToken ct)
    {
        const int MAX_RTP_PAYLOAD = 1400;
        byte naluHeader = nalu[0];
        byte fnri = (byte)(naluHeader & 0xE0); // F + NRI bits
        byte naluType = (byte)(naluHeader & 0x1F);

        int offset = 1; // skip original NAL header
        bool first = true;

        while (offset < nalu.Length)
        {
            int payloadSize = Math.Min(MAX_RTP_PAYLOAD - 2, nalu.Length - offset); // -2 for FU indicator + FU header
            bool last = (offset + payloadSize >= nalu.Length);

            // RTP header (12) + FU indicator (1) + FU header (1) + payload
            var packet = new byte[12 + 2 + payloadSize];

            // RTP header
            packet[0] = 0x80;
            packet[1] = (byte)(((last && marker) ? 0x80 : 0) | 96);
            packet[2] = (byte)(client.RtpSequenceNumber >> 8);
            packet[3] = (byte)(client.RtpSequenceNumber);
            client.RtpSequenceNumber++;
            packet[4] = (byte)(timestamp >> 24);
            packet[5] = (byte)(timestamp >> 16);
            packet[6] = (byte)(timestamp >> 8);
            packet[7] = (byte)(timestamp);
            packet[8] = (byte)(client.RtpSsrc >> 24);
            packet[9] = (byte)(client.RtpSsrc >> 16);
            packet[10] = (byte)(client.RtpSsrc >> 8);
            packet[11] = (byte)(client.RtpSsrc);

            // FU indicator: F+NRI from original header, type=28 (FU-A)
            packet[12] = (byte)(fnri | 28);
            // FU header: S/E/R bits + original NAL type
            packet[13] = (byte)((first ? 0x80 : 0) | (last ? 0x40 : 0) | naluType);

            Array.Copy(nalu, offset, packet, 14, payloadSize);

            await WriteRtpToClient(client, packet, ct);

            offset += payloadSize;
            first = false;
        }
    }

    private async Task WriteRtpToClient(RtspClientConnection client, byte[] rtpPacket, CancellationToken ct)
    {
        if (client.UseTcp)
        {
            var interleaved = new byte[4 + rtpPacket.Length];
            interleaved[0] = 0x24; // '$'
            interleaved[1] = (byte)client.RtpChannel;
            interleaved[2] = (byte)(rtpPacket.Length >> 8);
            interleaved[3] = (byte)(rtpPacket.Length & 0xFF);
            Array.Copy(rtpPacket, 0, interleaved, 4, rtpPacket.Length);

            await client.WriteLock.WaitAsync(ct);
            try
            {
                await client.TcpClient.GetStream().WriteAsync(interleaved, ct);
            }
            finally
            {
                client.WriteLock.Release();
            }
        }
        else if (client.UdpClient != null && client.ClientRtpEndpoint != null)
        {
            await client.UdpClient.SendAsync(rtpPacket, rtpPacket.Length, client.ClientRtpEndpoint);
        }
    }

    private static string? FindFfmpeg()
    {
        // Check common Windows paths
        var candidates = new[]
        {
            @"C:\ffmpeg\bin\ffmpeg.exe",
            @"C:\Program Files\FFmpeg\bin\ffmpeg.exe",
            @"C:\ProgramData\chocolatey\bin\ffmpeg.exe",
        };

        foreach (var path in candidates)
        {
            if (File.Exists(path)) return path;
        }

        // Search winget packages in all user profiles and current user
        var searchRoots = new List<string>();

        // Current user's AppData
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrEmpty(localAppData))
            searchRoots.Add(Path.Combine(localAppData, @"Microsoft\WinGet\Packages"));

        // All user profiles (service runs as SYSTEM, so check all users)
        try
        {
            var usersDir = @"C:\Users";
            if (Directory.Exists(usersDir))
            {
                foreach (var userDir in Directory.GetDirectories(usersDir))
                {
                    searchRoots.Add(Path.Combine(userDir, @"AppData\Local\Microsoft\WinGet\Packages"));
                }
            }
        }
        catch { }

        foreach (var wingetPkgs in searchRoots)
        {
            try
            {
                if (!Directory.Exists(wingetPkgs)) continue;
                foreach (var dir in Directory.GetDirectories(wingetPkgs, "Gyan.FFmpeg*"))
                {
                    var binDirs = Directory.GetDirectories(dir, "bin", SearchOption.AllDirectories);
                    foreach (var binDir in binDirs)
                    {
                        var ffmpeg = Path.Combine(binDir, "ffmpeg.exe");
                        if (File.Exists(ffmpeg)) return ffmpeg;
                    }
                }
            }
            catch { }
        }

        // Try PATH
        try
        {
            var proc = Process.Start(new ProcessStartInfo
            {
                FileName = "where",
                Arguments = "ffmpeg",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
            });
            if (proc != null)
            {
                var output = proc.StandardOutput.ReadToEnd().Trim();
                proc.WaitForExit(3000);
                if (proc.ExitCode == 0 && !string.IsNullOrEmpty(output))
                {
                    return output.Split('\n')[0].Trim();
                }
            }
        }
        catch { }

        return null;
    }

    private void StopFfmpeg()
    {
        try
        {
            if (_ffmpegProcess != null && !_ffmpegProcess.HasExited)
            {
                try { _ffmpegProcess.StandardInput.Close(); } catch { }
                if (!_ffmpegProcess.WaitForExit(3000))
                    _ffmpegProcess.Kill();
                _ffmpegProcess.Dispose();
            }
        }
        catch { }
        _ffmpegProcess = null;
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
        while (!ct.IsCancellationRequested && _listener != null)
        {
            try
            {
                var tcpClient = await _listener.AcceptTcpClientAsync(ct);
                var clientConnection = new RtspClientConnection(tcpClient, ++_sessionCounter);

                lock (_clientsLock)
                {
                    _clients.Add(clientConnection);
                }

                OnStatus($"Client connected: {tcpClient.Client.RemoteEndPoint}");

                // Handle client in background
                _ = HandleClientAsync(clientConnection, ct);
            }
            catch (OperationCanceledException) { break; }
            catch (SocketException) { break; }
            catch (Exception ex)
            {
                OnError($"Accept error: {ex.Message}");
            }
        }
    }

    private async Task HandleClientAsync(RtspClientConnection client, CancellationToken ct)
    {
        try
        {
            var stream = client.TcpClient.GetStream();
            var buffer = new byte[4096];

            while (!ct.IsCancellationRequested && client.TcpClient.Connected)
            {
                // Check for incoming data
                if (stream.DataAvailable)
                {
                    // Peek at first byte to detect interleaved RTP/RTCP ($-framed)
                    var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, ct);
                    if (bytesRead == 0) break;

                    int pos = 0;
                    while (pos < bytesRead)
                    {
                        if (buffer[pos] == 0x24 && pos + 4 <= bytesRead) // '$' interleaved frame
                        {
                            // Skip interleaved RTP/RTCP data from NVR (e.g. RTCP receiver reports)
                            int channel = buffer[pos + 1];
                            int frameLen = (buffer[pos + 2] << 8) | buffer[pos + 3];
                            int totalLen = 4 + frameLen;
                            if (pos + totalLen <= bytesRead)
                            {
                                pos += totalLen;
                            }
                            else
                            {
                                // Frame spans beyond current read — discard remaining bytes
                                int remaining = totalLen - (bytesRead - pos);
                                pos = bytesRead;
                                // Drain the rest of the interleaved frame
                                while (remaining > 0)
                                {
                                    var drain = new byte[Math.Min(remaining, 4096)];
                                    var read = await stream.ReadAsync(drain, 0, drain.Length, ct);
                                    if (read == 0) break;
                                    remaining -= read;
                                }
                            }
                        }
                        else
                        {
                            // RTSP text request — find the end (\r\n\r\n)
                            var request = Encoding.ASCII.GetString(buffer, pos, bytesRead - pos);
                            await ProcessRtspRequestAsync(client, request, ct);
                            break;
                        }
                    }
                }
                else
                {
                    await Task.Delay(10, ct);
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _log.Debug("Client handler error: {Error}", ex.Message);
        }
        finally
        {
            RemoveClient(client);
        }
    }

    private async Task ProcessRtspRequestAsync(RtspClientConnection client, string request, CancellationToken ct)
    {
        var lines = request.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0) return;

        var requestLine = lines[0].Split(' ');
        if (requestLine.Length < 2) return;

        var method = requestLine[0];
        var cseq = ExtractCSeq(lines);

        _log.Information("RTSP Request: {Method} CSeq={CSeq} from {Client}", method, cseq, client.TcpClient.Client.RemoteEndPoint);

        // Check authentication if required (except for OPTIONS which is used for discovery)
        if (_config.RequireAuth && method != "OPTIONS")
        {
            if (!CheckRtspAuthentication(lines, out string? authError))
            {
                var authResponse = BuildUnauthorizedResponse(cseq, authError);
                var authBytes = Encoding.ASCII.GetBytes(authResponse);
                await client.WriteLock.WaitAsync(ct);
                try
                {
                    await client.TcpClient.GetStream().WriteAsync(authBytes, 0, authBytes.Length, ct);
                }
                finally
                {
                    client.WriteLock.Release();
                }
                _log.Information("RTSP auth failed from {Client}: {Error}", client.TcpClient.Client.RemoteEndPoint, authError);
                return;
            }
            if (!client.IsAuthenticated)
            {
                client.IsAuthenticated = true;
                _log.Information("RTSP auth successful from {Client}", client.TcpClient.Client.RemoteEndPoint);
            }
        }

        string response;
        switch (method)
        {
            case "OPTIONS":
                response = BuildOptionsResponse(cseq);
                break;
            case "DESCRIBE":
                response = BuildDescribeResponse(cseq);
                _log.Information("RTSP DESCRIBE response SDP (H264={H264}, SPS={Sps}, PPS={Pps})",
                    _useH264, _spsBase64 != null, _ppsBase64 != null);
                break;
            case "SETUP":
                response = BuildSetupResponse(client, cseq, lines);
                break;
            case "PLAY":
                client.IsPlaying = true;
                response = BuildPlayResponse(client, cseq);
                OnStatus($"Client started playback: {client.TcpClient.Client.RemoteEndPoint}");
                break;
            case "TEARDOWN":
                response = BuildTeardownResponse(cseq);
                client.IsPlaying = false;
                break;
            default:
                response = BuildNotImplementedResponse(cseq);
                break;
        }

        var responseBytes = Encoding.ASCII.GetBytes(response);
        await client.WriteLock.WaitAsync(ct);
        try
        {
            await client.TcpClient.GetStream().WriteAsync(responseBytes, 0, responseBytes.Length, ct);
        }
        finally
        {
            client.WriteLock.Release();
        }
    }

    private bool CheckRtspAuthentication(string[] lines, out string? error)
    {
        error = null;

        // Look for Authorization header
        var authLine = lines.FirstOrDefault(l => l.StartsWith("Authorization:", StringComparison.OrdinalIgnoreCase));

        if (string.IsNullOrEmpty(authLine))
        {
            error = "No credentials provided";
            return false;
        }

        // Support Basic authentication
        if (authLine.Contains("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var base64 = authLine.Substring(authLine.IndexOf("Basic ", StringComparison.OrdinalIgnoreCase) + 6).Trim();
                var credentials = Encoding.UTF8.GetString(Convert.FromBase64String(base64));
                var parts = credentials.Split(':', 2);

                if (parts.Length == 2 && parts[0] == _config.Username && parts[1] == _config.Password)
                {
                    return true;
                }
                error = "Invalid credentials";
                return false;
            }
            catch
            {
                error = "Malformed credentials";
                return false;
            }
        }

        // Support Digest authentication (simplified - just check username/password from uri)
        if (authLine.Contains("Digest ", StringComparison.OrdinalIgnoreCase))
        {
            // For Dahua compatibility, we'll accept Digest auth with the username field
            if (authLine.Contains($"username=\"{_config.Username}\""))
            {
                // In a full implementation, we'd verify the digest response
                // For now, accept if username matches (Dahua uses Digest)
                return true;
            }
            error = "Invalid username";
            return false;
        }

        error = "Unsupported auth method";
        return false;
    }

    private string BuildUnauthorizedResponse(string cseq, string? error)
    {
        // Support both Basic and Digest authentication for maximum NVR compatibility
        return $"RTSP/1.0 401 Unauthorized\r\n" +
               $"CSeq: {cseq}\r\n" +
               $"WWW-Authenticate: Basic realm=\"Scale Streamer\"\r\n" +
               $"WWW-Authenticate: Digest realm=\"Scale Streamer\", nonce=\"{Guid.NewGuid():N}\"\r\n\r\n";
    }

    private string ExtractCSeq(string[] lines)
    {
        foreach (var line in lines)
        {
            if (line.StartsWith("CSeq:", StringComparison.OrdinalIgnoreCase))
            {
                return line.Substring(5).Trim();
            }
        }
        return "1";
    }

    private string BuildOptionsResponse(string cseq)
    {
        return $"RTSP/1.0 200 OK\r\n" +
               $"CSeq: {cseq}\r\n" +
               "Public: OPTIONS, DESCRIBE, SETUP, PLAY, TEARDOWN\r\n\r\n";
    }

    private string BuildDescribeResponse(string cseq)
    {
        var ip = LocalIpAddress;
        string sdp;

        if (_useH264)
        {
            // H.264 SDP
            var profileLevelId = _profileLevelId ?? "42C01F";
            var spropParams = (_spsBase64 != null && _ppsBase64 != null)
                ? $"{_spsBase64},{_ppsBase64}" : "";

            sdp = $"v=0\r\n" +
                  $"o=- {DateTimeOffset.UtcNow.ToUnixTimeSeconds()} 1 IN IP4 {ip}\r\n" +
                  $"s=Scale Streamer\r\n" +
                  $"i=Weight Display Stream\r\n" +
                  $"c=IN IP4 {ip}\r\n" +
                  $"t=0 0\r\n" +
                  $"a=tool:ScaleStreamer\r\n" +
                  $"a=type:broadcast\r\n" +
                  $"a=range:npt=now-\r\n" +
                  $"m=video 0 RTP/AVP 96\r\n" +
                  $"a=rtpmap:96 H264/90000\r\n" +
                  $"a=fmtp:96 packetization-mode=1;profile-level-id={profileLevelId}" +
                  (spropParams.Length > 0 ? $";sprop-parameter-sets={spropParams}" : "") + "\r\n" +
                  $"a=framerate:{_config.FrameRate}\r\n" +
                  $"a=control:trackID=0\r\n" +
                  $"a=recvonly\r\n";
        }
        else
        {
            // MJPEG SDP
            sdp = $"v=0\r\n" +
                  $"o=- {DateTimeOffset.UtcNow.ToUnixTimeSeconds()} 1 IN IP4 {ip}\r\n" +
                  $"s=Scale Streamer\r\n" +
                  $"i=Weight Display Stream\r\n" +
                  $"c=IN IP4 {ip}\r\n" +
                  $"t=0 0\r\n" +
                  $"a=tool:ScaleStreamer\r\n" +
                  $"a=type:broadcast\r\n" +
                  $"a=range:npt=now-\r\n" +
                  $"a=x-qt-text-nam:Scale Weight Stream\r\n" +
                  $"m=video 0 RTP/AVP 26\r\n" +
                  $"a=rtpmap:26 JPEG/90000\r\n" +
                  $"a=framerate:{_config.FrameRate}\r\n" +
                  $"a=framesize:26 {_config.VideoWidth}-{_config.VideoHeight}\r\n" +
                  $"a=control:trackID=0\r\n" +
                  $"a=recvonly\r\n";
        }

        return $"RTSP/1.0 200 OK\r\n" +
               $"CSeq: {cseq}\r\n" +
               "Content-Type: application/sdp\r\n" +
               $"Content-Base: rtsp://{ip}:{_config.RtspPort}/{_config.StreamName}/\r\n" +
               $"Content-Length: {sdp.Length}\r\n\r\n" +
               sdp;
    }

    private string BuildSetupResponse(RtspClientConnection client, string cseq, string[] lines)
    {
        // Parse transport line
        var transportLine = lines.FirstOrDefault(l => l.StartsWith("Transport:", StringComparison.OrdinalIgnoreCase));
        _log.Information("RTSP SETUP transport: {Transport}", transportLine ?? "(none)");

        string transportResponse;

        if (transportLine != null)
        {
            if (transportLine.Contains("RTP/AVP/TCP") || transportLine.Contains("interleaved"))
            {
                // Client wants TCP interleaved
                client.UseTcp = true;
                var match = System.Text.RegularExpressions.Regex.Match(transportLine, @"interleaved=(\d+)-(\d+)");
                if (match.Success)
                {
                    client.RtpChannel = int.Parse(match.Groups[1].Value);
                    client.RtcpChannel = int.Parse(match.Groups[2].Value);
                }
                else
                {
                    client.RtpChannel = 0;
                    client.RtcpChannel = 1;
                }
                transportResponse = $"RTP/AVP/TCP;unicast;interleaved={client.RtpChannel}-{client.RtcpChannel}";
            }
            else if (transportLine.Contains("RTP/AVP") && transportLine.Contains("client_port"))
            {
                // Client wants UDP - set up UDP transport
                client.UseTcp = false;
                var portMatch = System.Text.RegularExpressions.Regex.Match(transportLine, @"client_port=(\d+)-(\d+)");
                if (portMatch.Success)
                {
                    client.ClientRtpPort = int.Parse(portMatch.Groups[1].Value);
                    client.ClientRtcpPort = int.Parse(portMatch.Groups[2].Value);
                }
                else
                {
                    client.ClientRtpPort = 5000;
                    client.ClientRtcpPort = 5001;
                }

                // Create UDP client for sending RTP
                try
                {
                    client.UdpClient = new UdpClient(0); // Bind to any available port
                    var localEndpoint = (IPEndPoint)client.UdpClient.Client.LocalEndPoint!;
                    client.ServerRtpPort = localEndpoint.Port;
                    client.ServerRtcpPort = client.ServerRtpPort + 1;

                    // Get client IP from TCP connection
                    var clientEndpoint = (IPEndPoint)client.TcpClient.Client.RemoteEndPoint!;
                    client.ClientRtpEndpoint = new IPEndPoint(clientEndpoint.Address, client.ClientRtpPort);

                    _log.Information("UDP transport configured: client={ClientIP}:{ClientPort}, server_port={ServerPort}",
                        clientEndpoint.Address, client.ClientRtpPort, client.ServerRtpPort);

                    transportResponse = $"RTP/AVP;unicast;client_port={client.ClientRtpPort}-{client.ClientRtcpPort};server_port={client.ServerRtpPort}-{client.ServerRtcpPort}";
                }
                catch (Exception ex)
                {
                    _log.Warning("Failed to create UDP transport, falling back to TCP: {Error}", ex.Message);
                    client.UseTcp = true;
                    client.RtpChannel = 0;
                    client.RtcpChannel = 1;
                    transportResponse = $"RTP/AVP/TCP;unicast;interleaved={client.RtpChannel}-{client.RtcpChannel}";
                }
            }
            else
            {
                client.UseTcp = true;
                client.RtpChannel = 0;
                client.RtcpChannel = 1;
                transportResponse = $"RTP/AVP/TCP;unicast;interleaved={client.RtpChannel}-{client.RtcpChannel}";
            }
        }
        else
        {
            client.UseTcp = true;
            client.RtpChannel = 0;
            client.RtcpChannel = 1;
            transportResponse = $"RTP/AVP/TCP;unicast;interleaved={client.RtpChannel}-{client.RtcpChannel}";
        }

        return $"RTSP/1.0 200 OK\r\n" +
               $"CSeq: {cseq}\r\n" +
               $"Session: {client.SessionId};timeout=60\r\n" +
               $"Transport: {transportResponse}\r\n\r\n";
    }

    private string BuildPlayResponse(RtspClientConnection client, string cseq)
    {
        // Note: RTP-Info omitted because we can't know the exact timestamp until first packet is sent
        // Most decoders handle this fine; some servers include placeholder values but that can confuse strict decoders
        return $"RTSP/1.0 200 OK\r\n" +
               $"CSeq: {cseq}\r\n" +
               $"Session: {client.SessionId}\r\n" +
               "Range: npt=now-\r\n\r\n";
    }

    private string BuildTeardownResponse(string cseq)
    {
        return $"RTSP/1.0 200 OK\r\n" +
               $"CSeq: {cseq}\r\n\r\n";
    }

    private string BuildNotImplementedResponse(string cseq)
    {
        return $"RTSP/1.0 501 Not Implemented\r\n" +
               $"CSeq: {cseq}\r\n\r\n";
    }

    private async Task StreamFramesAsync(CancellationToken ct)
    {
        var frameInterval = TimeSpan.FromMilliseconds(1000.0 / _config.FrameRate);
        uint timestampIncrement = (uint)(90000 / _config.FrameRate); // RTP timestamp units (90kHz clock)
        var rawFrameSize = _config.VideoWidth * _config.VideoHeight * 3; // BGR24

        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (_useH264)
                {
                    // H.264 mode: generate raw frame and pipe to FFmpeg
                    byte[] rawFrame;
                    try
                    {
                        rawFrame = GenerateRawFrame();
                    }
                    catch (Exception ex)
                    {
                        _log.Error(ex, "Raw frame generation failed");
                        await Task.Delay(1000, ct);
                        continue;
                    }

                    if (_ffmpegProcess != null && !_ffmpegProcess.HasExited)
                    {
                        try
                        {
                            await _ffmpegProcess.StandardInput.BaseStream.WriteAsync(rawFrame, 0, rawFrame.Length, ct);
                            await _ffmpegProcess.StandardInput.BaseStream.FlushAsync(ct);
                        }
                        catch (Exception ex)
                        {
                            _log.Error("Error writing to FFmpeg: {Error}", ex.Message);
                            _useH264 = false;
                            StopFfmpeg();
                            OnStatus("FFmpeg crashed, falling back to MJPEG");
                            continue;
                        }
                    }

                    if (_frameCount % 300 == 0)
                    {
                        List<RtspClientConnection> pc;
                        lock (_clientsLock) { pc = _clients.Where(c => c.IsPlaying).ToList(); }
                        if (pc.Count > 0)
                            _log.Information("H.264: Frame {Frame}, {Count} client(s)", _frameCount, pc.Count);
                    }
                    _frameCount++;
                }
                else
                {
                    // MJPEG mode: generate JPEG and send directly
                    byte[] jpegData;
                    try
                    {
                        jpegData = GenerateWeightFrame();
                    }
                    catch (Exception ex)
                    {
                        _log.Error("Frame generation failed: {Error}", ex.Message);
                        await Task.Delay(1000, ct);
                        continue;
                    }

                    List<RtspClientConnection> playingClients;
                    lock (_clientsLock)
                    {
                        playingClients = _clients.Where(c => c.IsPlaying && c.TcpClient.Connected).ToList();
                    }

                    if (playingClients.Count > 0 && _frameCount % 100 == 0)
                    {
                        _log.Information("MJPEG: Sending frame {Frame} ({Size} bytes) to {Count} client(s)",
                            _frameCount, jpegData.Length, playingClients.Count);
                    }
                    _frameCount++;

                    foreach (var client in playingClients)
                    {
                        try
                        {
                            await SendJpegFrameAsync(client, jpegData, ct);
                            client.RtpTimestamp += timestampIncrement;
                        }
                        catch (Exception ex)
                        {
                            _log.Error("Error sending frame to client {Client}: {Error}",
                                client.TcpClient.Client.RemoteEndPoint, ex.Message);
                            client.IsPlaying = false;
                        }
                    }
                }

                await Task.Delay(frameInterval, ct);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                OnError($"Stream error: {ex.Message}");
                await Task.Delay(100, ct);
            }
        }
    }

    private void EnsureRenderingResources()
    {
        if (_frameBitmap == null || _frameBitmap.Width != _config.VideoWidth || _frameBitmap.Height != _config.VideoHeight)
        {
            _frameGraphics?.Dispose();
            _frameBitmap?.Dispose();
            _frameBitmap = new Bitmap(_config.VideoWidth, _config.VideoHeight, PixelFormat.Format24bppRgb);
            _frameGraphics = Graphics.FromImage(_frameBitmap);
            _frameGraphics.SmoothingMode = SmoothingMode.HighSpeed;
            _frameGraphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;
            _frameGraphics.InterpolationMode = InterpolationMode.Low;
            _frameGraphics.PixelOffsetMode = PixelOffsetMode.HighSpeed;
            _frameGraphics.CompositingQuality = CompositingQuality.HighSpeed;
            _rawFrameBuffer = new byte[_config.VideoWidth * _config.VideoHeight * 3];
        }

        if (_weightFont == null)
        {
            _weightFont = new Font("Consolas", _config.FontSize, FontStyle.Bold);
            _statusFont = new Font("Arial", _config.FontSize / 3, FontStyle.Bold);
            _labelFont = new Font("Arial", 24, FontStyle.Regular);
            _liveFont = new Font("Arial", 16, FontStyle.Bold);
            _redBrush = new SolidBrush(Color.FromArgb(255, 0, 0));
            _dimBrush = new SolidBrush(Color.FromArgb(100, 100, 100));
            _liveBrush = new SolidBrush(Color.FromArgb(255, 0, 0));
            _centerFormat = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };
            _rightFormat = new StringFormat { Alignment = StringAlignment.Far };
        }
    }

    /// <summary>
    /// Generate a raw BGR24 frame (no JPEG encoding) for FFmpeg input.
    /// Reuses pre-allocated bitmap, graphics, fonts and pixel buffer.
    /// </summary>
    private byte[] GenerateRawFrame()
    {
        EnsureRenderingResources();

        _frameGraphics!.Clear(Color.Black);
        DrawWeightContent(_frameGraphics);

        // Extract raw BGR24 pixel data
        var rect = new Rectangle(0, 0, _frameBitmap!.Width, _frameBitmap.Height);
        var bmpData = _frameBitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
        try
        {
            var stride = bmpData.Stride;
            var rawSize = _config.VideoWidth * 3;

            if (stride == rawSize)
            {
                Marshal.Copy(bmpData.Scan0, _rawFrameBuffer!, 0, _rawFrameBuffer!.Length);
            }
            else
            {
                for (int y = 0; y < _config.VideoHeight; y++)
                {
                    Marshal.Copy(bmpData.Scan0 + y * stride, _rawFrameBuffer!, y * rawSize, rawSize);
                }
            }
            return _rawFrameBuffer!;
        }
        finally
        {
            _frameBitmap.UnlockBits(bmpData);
        }
    }

    /// <summary>
    /// Forward H.264 RTP packets from FFmpeg's UDP output to all playing RTSP clients
    /// </summary>
    private async Task SendJpegFrameAsync(RtspClientConnection client, byte[] jpegData, CancellationToken ct)
    {
        // JPEG RTP packetization (RFC 2435)
        const int MAX_PAYLOAD = 1400;
        int offset = 0;

        // Skip JPEG header to find image data
        int dataOffset = FindJpegDataOffset(jpegData);
        if (dataOffset < 0) return;

        // Extract quantization tables
        var (qTables, qtableOffset) = ExtractQuantizationTables(jpegData);

        // Check if client is still connected before writing
        if (!client.TcpClient.Connected || !client.IsPlaying)
        {
            return;
        }

        if (client.UseTcp)
        {
            // TCP interleaved transport
            await client.WriteLock.WaitAsync(ct);
            try
            {
                var stream = client.TcpClient.GetStream();

                while (offset < jpegData.Length - dataOffset)
                {
                    var payloadSize = Math.Min(MAX_PAYLOAD, jpegData.Length - dataOffset - offset);
                    var isLastPacket = (offset + payloadSize >= jpegData.Length - dataOffset);

                    // Build RTP packet using client's own sequence number and SSRC
                    var rtpPacket = BuildJpegRtpPacket(
                        jpegData, dataOffset + offset, payloadSize,
                        offset, // fragment offset
                        isLastPacket,
                        client.RtpTimestamp, client.RtpSequenceNumber++, client.RtpSsrc,
                        qTables, offset == 0);

                    // For TCP interleaved, wrap in RTSP interleaved frame
                    var interleavedPacket = new byte[4 + rtpPacket.Length];
                    interleavedPacket[0] = 0x24; // '$'
                    interleavedPacket[1] = (byte)client.RtpChannel;
                    interleavedPacket[2] = (byte)(rtpPacket.Length >> 8);
                    interleavedPacket[3] = (byte)(rtpPacket.Length & 0xFF);
                    Array.Copy(rtpPacket, 0, interleavedPacket, 4, rtpPacket.Length);

                    await stream.WriteAsync(interleavedPacket, ct);

                    offset += payloadSize;
                }
            }
            finally
            {
                client.WriteLock.Release();
            }
        }
        else
        {
            // UDP transport
            if (client.UdpClient == null || client.ClientRtpEndpoint == null)
            {
                return;
            }

            while (offset < jpegData.Length - dataOffset)
            {
                var payloadSize = Math.Min(MAX_PAYLOAD, jpegData.Length - dataOffset - offset);
                var isLastPacket = (offset + payloadSize >= jpegData.Length - dataOffset);

                // Build RTP packet using client's own sequence number and SSRC
                var rtpPacket = BuildJpegRtpPacket(
                    jpegData, dataOffset + offset, payloadSize,
                    offset, // fragment offset
                    isLastPacket,
                    client.RtpTimestamp, client.RtpSequenceNumber++, client.RtpSsrc,
                    qTables, offset == 0);

                // Send via UDP
                await client.UdpClient.SendAsync(rtpPacket, rtpPacket.Length, client.ClientRtpEndpoint);

                offset += payloadSize;
            }
        }

        // Update last activity
        client.LastActivity = DateTime.UtcNow;
    }

    private int FindJpegDataOffset(byte[] jpeg)
    {
        // Find SOS (Start of Scan) marker
        for (int i = 0; i < jpeg.Length - 1; i++)
        {
            if (jpeg[i] == 0xFF && jpeg[i + 1] == 0xDA)
            {
                // Skip SOS header
                int sosLength = (jpeg[i + 2] << 8) | jpeg[i + 3];
                return i + 2 + sosLength;
            }
        }
        return -1;
    }

    private (byte[]? tables, int offset) ExtractQuantizationTables(byte[] jpeg)
    {
        // RFC 2435 quantization table header needs raw 64-byte tables (no JPEG DQT overhead).
        // JPEG DQT format: [marker 0xFFDB][length 2 bytes][id_byte][64 values]...
        // The id_byte (precision nibble + table ID nibble) must be stripped for RFC 2435.
        var allTables = new List<byte>();
        int firstOffset = -1;

        for (int i = 0; i < jpeg.Length - 3; i++)
        {
            if (jpeg[i] == 0xFF && jpeg[i + 1] == 0xDB)
            {
                if (firstOffset == -1) firstOffset = i;

                int length = (jpeg[i + 2] << 8) | jpeg[i + 3];
                int pos = i + 4; // Start of DQT payload
                int end = i + 2 + length;

                while (pos < end && pos < jpeg.Length)
                {
                    byte idByte = jpeg[pos];
                    int precision = (idByte >> 4) & 0x0F; // 0 = 8-bit, 1 = 16-bit
                    int tableSize = precision == 0 ? 64 : 128;
                    pos++; // Skip the id byte

                    // Copy just the raw table values (no id byte)
                    for (int j = 0; j < tableSize && pos + j < jpeg.Length; j++)
                    {
                        allTables.Add(jpeg[pos + j]);
                    }
                    pos += tableSize;
                }

                i = end - 1; // Move past this DQT segment
            }
        }

        if (allTables.Count > 0)
        {
            return (allTables.ToArray(), firstOffset);
        }
        return (null, -1);
    }

    private byte[] BuildJpegRtpPacket(byte[] jpeg, int dataOffset, int payloadSize,
        int fragmentOffset, bool marker, uint timestamp, ushort seq, uint ssrc,
        byte[]? qTables, bool includeQTables)
    {
        // RFC 2435 JPEG RTP header (8 bytes)
        var jpegHeader = new byte[8];
        jpegHeader[0] = 0; // Type-specific
        jpegHeader[1] = (byte)(fragmentOffset >> 16);
        jpegHeader[2] = (byte)(fragmentOffset >> 8);
        jpegHeader[3] = (byte)(fragmentOffset);
        jpegHeader[4] = 1; // Type 1 = JPEG baseline, YUV 4:2:0

        // Q=255 signals dynamic quantization tables (RFC 2435 section 3.1.8).
        // Tables from the actual JPEG are included in the first RTP packet,
        // giving the decoder full quality instead of standard Q=80 tables.
        byte qValue = 255;
        jpegHeader[5] = qValue;
        jpegHeader[6] = (byte)(_config.VideoWidth / 8);
        jpegHeader[7] = (byte)(_config.VideoHeight / 8);

        // RTP header (12 bytes)
        var rtpHeader = new byte[12];
        rtpHeader[0] = 0x80; // Version 2, no padding, no extension
        rtpHeader[1] = (byte)((marker ? 0x80 : 0) | 26); // Marker + PT=26 (JPEG)
        rtpHeader[2] = (byte)(seq >> 8);
        rtpHeader[3] = (byte)(seq);
        rtpHeader[4] = (byte)(timestamp >> 24);
        rtpHeader[5] = (byte)(timestamp >> 16);
        rtpHeader[6] = (byte)(timestamp >> 8);
        rtpHeader[7] = (byte)(timestamp);
        rtpHeader[8] = (byte)(ssrc >> 24);
        rtpHeader[9] = (byte)(ssrc >> 16);
        rtpHeader[10] = (byte)(ssrc >> 8);
        rtpHeader[11] = (byte)(ssrc);

        // Build quantization table header if needed (RFC 2435 section 3.1.8)
        byte[]? qTableHeader = null;
        if (includeQTables && qTables != null && qValue >= 128)
        {
            // Q table header format: MBZ (1), Precision (1), Length (2), Data (Length)
            qTableHeader = new byte[4 + qTables.Length];
            qTableHeader[0] = 0; // MBZ
            qTableHeader[1] = 0; // Precision: 0 = 8-bit values
            int tableLen = qTables.Length;
            qTableHeader[2] = (byte)(tableLen >> 8);
            qTableHeader[3] = (byte)(tableLen & 0xFF);
            Array.Copy(qTables, 0, qTableHeader, 4, qTables.Length);
        }

        // Combine headers and payload
        int qTableSize = qTableHeader?.Length ?? 0;
        var packetSize = rtpHeader.Length + jpegHeader.Length + qTableSize + payloadSize;
        var packet = new byte[packetSize];

        int pos = 0;
        Array.Copy(rtpHeader, 0, packet, pos, rtpHeader.Length);
        pos += rtpHeader.Length;

        Array.Copy(jpegHeader, 0, packet, pos, jpegHeader.Length);
        pos += jpegHeader.Length;

        if (qTableHeader != null)
        {
            Array.Copy(qTableHeader, 0, packet, pos, qTableHeader.Length);
            pos += qTableHeader.Length;
        }

        Array.Copy(jpeg, dataOffset, packet, pos, payloadSize);

        return packet;
    }

    private void DrawWeightContent(Graphics graphics)
    {
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

        string unit = _currentReading?.Unit ?? "lb";
        string displayText = $"{weightText} {unit}";

        var centerRect = new RectangleF(0, 0, _config.VideoWidth, _config.VideoHeight);
        graphics.DrawString(displayText, _weightFont!, _redBrush!, centerRect, _centerFormat!);

        var statusY = (_config.VideoHeight / 2) + (_config.FontSize / 2) + 30;
        using var statusBrush = new SolidBrush(statusColor);
        var statusRect = new RectangleF(0, statusY, _config.VideoWidth, 50);
        graphics.DrawString(statusText, _statusFont!, statusBrush, statusRect, _centerFormat!);

        string scaleLabel = $"Scale: {_config.ScaleId ?? "Default"}";
        graphics.DrawString(scaleLabel, _labelFont!, _dimBrush!, 20, 20);

        string timeLabel = DateTime.Now.ToString("HH:mm:ss");
        graphics.DrawString(timeLabel, _labelFont!, _dimBrush!, _config.VideoWidth - 20, 20, _rightFormat!);

        graphics.FillEllipse(_liveBrush!, _config.VideoWidth - 95, _config.VideoHeight - 40, 16, 16);
        graphics.DrawString("LIVE", _liveFont!, Brushes.White, _config.VideoWidth - 75, _config.VideoHeight - 42);

        graphics.DrawString("Cloud-Scale", _labelFont!, _dimBrush!, 20, _config.VideoHeight - 40);
    }

    private byte[] GenerateWeightFrame()
    {
        using var bitmap = new Bitmap(_config.VideoWidth, _config.VideoHeight, PixelFormat.Format24bppRgb);
        using var graphics = Graphics.FromImage(bitmap);

        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.CompositingQuality = CompositingQuality.HighQuality;
        graphics.Clear(Color.Black);

        DrawWeightContent(graphics);

        using var ms = new MemoryStream();
        var encoder = ImageCodecInfo.GetImageEncoders().First(e => e.FormatID == ImageFormat.Jpeg.Guid);
        var encoderParams = new EncoderParameters(1);
        encoderParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 100L);
        bitmap.Save(ms, encoder, encoderParams);

        return ms.ToArray();
    }

    private void RemoveClient(RtspClientConnection client)
    {
        lock (_clientsLock)
        {
            _clients.Remove(client);
        }
        OnStatus($"Client disconnected: {client.TcpClient.Client.RemoteEndPoint}");
        client.Dispose();
    }

    private void OnStatus(string message)
    {
        _log.Information("RTSP: {Message}", message);
        StatusChanged?.Invoke(this, message);
    }

    private void OnError(string message)
    {
        _log.Error("RTSP Error: {Message}", message);
        ErrorOccurred?.Invoke(this, message);
    }

    public void Dispose()
    {
        StopAsync().GetAwaiter().GetResult();
        _cts?.Dispose();
        _frameGraphics?.Dispose();
        _frameBitmap?.Dispose();
        _weightFont?.Dispose();
        _statusFont?.Dispose();
        _labelFont?.Dispose();
        _liveFont?.Dispose();
        _redBrush?.Dispose();
        _dimBrush?.Dispose();
        _liveBrush?.Dispose();
        _centerFormat?.Dispose();
        _rightFormat?.Dispose();
        GC.SuppressFinalize(this);
    }

    private class RtspClientConnection : IDisposable
    {
        public TcpClient TcpClient { get; }
        public int SessionId { get; }
        public bool IsPlaying { get; set; }
        public bool IsAuthenticated { get; set; }
        public bool UseTcp { get; set; } = true;
        public int RtpChannel { get; set; }
        public int RtcpChannel { get; set; }
        public SemaphoreSlim WriteLock { get; } = new SemaphoreSlim(1, 1);
        public DateTime LastActivity { get; set; } = DateTime.UtcNow;

        // RTP state - each client needs its own sequence number and SSRC
        public ushort RtpSequenceNumber { get; set; }
        public uint RtpSsrc { get; set; }
        public uint RtpTimestamp { get; set; }

        // UDP transport
        public UdpClient? UdpClient { get; set; }
        public IPEndPoint? ClientRtpEndpoint { get; set; }
        public int ClientRtpPort { get; set; }
        public int ClientRtcpPort { get; set; }
        public int ServerRtpPort { get; set; }
        public int ServerRtcpPort { get; set; }
        public bool SentSps { get; set; }

        public RtspClientConnection(TcpClient tcpClient, int sessionId)
        {
            TcpClient = tcpClient;
            SessionId = sessionId;
            // Initialize per-client RTP state
            RtpSequenceNumber = (ushort)Random.Shared.Next(0, 65535);
            RtpSsrc = (uint)Random.Shared.Next();
            RtpTimestamp = (uint)Random.Shared.Next();
        }

        public void Dispose()
        {
            try { TcpClient.Close(); } catch { }
            TcpClient.Dispose();
            WriteLock.Dispose();
            UdpClient?.Dispose();
        }
    }
}
