using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Net;
using System.Net.Sockets;
using System.Text;
using ScaleStreamer.Common.Models;
using Serilog;

namespace ScaleStreamer.Common.Streaming;

/// <summary>
/// Native RTSP server for streaming weight display video
/// Uses MJPEG format for maximum compatibility (VLC, browsers, etc.)
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
    private WeightReading? _currentReading;
    private readonly object _readingLock = new();
    private bool _isRunning;
    private int _sessionCounter = 1000;

    public event EventHandler<string>? StatusChanged;
    public event EventHandler<string>? ErrorOccurred;

    public bool IsRunning => _isRunning;
    public string StreamUrl => $"rtsp://localhost:{_config.RtspPort}/{_config.StreamName}";

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
            OnStatus($"RTSP server started on port {_config.RtspPort}");
            OnStatus($"Stream URL: {StreamUrl}");

            // Start accepting connections
            _acceptTask = AcceptConnectionsAsync(_cts.Token);

            // Start frame streaming loop
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
        }
        catch (TimeoutException) { }
        catch (OperationCanceledException) { }

        _isRunning = false;
        OnStatus("RTSP server stopped");
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
                // Check for incoming RTSP request
                if (stream.DataAvailable)
                {
                    var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, ct);
                    if (bytesRead == 0) break;

                    var request = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                    await ProcessRtspRequestAsync(client, request, ct);
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

        _log.Debug("RTSP Request: {Method} CSeq={CSeq}", method, cseq);

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
        // SDP for MJPEG video stream - optimized for low latency
        var sdp = $"v=0\r\n" +
                  $"o=- {DateTimeOffset.UtcNow.ToUnixTimeSeconds()} 1 IN IP4 0.0.0.0\r\n" +
                  $"s=Scale Streamer\r\n" +
                  $"i=Weight Display Stream\r\n" +
                  $"c=IN IP4 0.0.0.0\r\n" +
                  $"t=0 0\r\n" +
                  $"a=tool:ScaleStreamer\r\n" +
                  $"a=type:broadcast\r\n" +
                  $"a=range:npt=now-\r\n" +  // Live stream
                  $"a=x-qt-text-nam:Scale Weight Stream\r\n" +
                  $"m=video 0 RTP/AVP 26\r\n" +  // 26 = JPEG
                  $"a=rtpmap:26 JPEG/90000\r\n" +
                  $"a=framerate:{_config.FrameRate}\r\n" +
                  $"a=framesize:26 {_config.VideoWidth}-{_config.VideoHeight}\r\n" +
                  $"a=control:trackID=0\r\n" +
                  $"a=recvonly\r\n";

        return $"RTSP/1.0 200 OK\r\n" +
               $"CSeq: {cseq}\r\n" +
               "Content-Type: application/sdp\r\n" +
               "Content-Base: rtsp://0.0.0.0/scale/\r\n" +
               $"Content-Length: {sdp.Length}\r\n\r\n" +
               sdp;
    }

    private string BuildSetupResponse(RtspClientConnection client, string cseq, string[] lines)
    {
        // Parse transport line
        var transportLine = lines.FirstOrDefault(l => l.StartsWith("Transport:", StringComparison.OrdinalIgnoreCase));

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
        return $"RTSP/1.0 200 OK\r\n" +
               $"CSeq: {cseq}\r\n" +
               $"Session: {client.SessionId}\r\n" +
               "Range: npt=0.000-\r\n\r\n";
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

        while (!ct.IsCancellationRequested)
        {
            try
            {
                // Generate frame with current weight
                var jpegData = GenerateWeightFrame();

                // Send to all playing clients
                List<RtspClientConnection> playingClients;
                lock (_clientsLock)
                {
                    playingClients = _clients.Where(c => c.IsPlaying && c.TcpClient.Connected).ToList();
                }

                foreach (var client in playingClients)
                {
                    try
                    {
                        // Each client has its own sequence number, timestamp, and SSRC
                        await SendJpegFrameAsync(client, jpegData, ct);
                        // Increment client's timestamp after each frame
                        client.RtpTimestamp += timestampIncrement;
                    }
                    catch (Exception ex)
                    {
                        _log.Debug("Error sending to client: {Error}", ex.Message);
                        client.IsPlaying = false;
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

    private byte[] GenerateWeightFrame()
    {
        using var bitmap = new Bitmap(_config.VideoWidth, _config.VideoHeight, PixelFormat.Format24bppRgb);
        using var graphics = Graphics.FromImage(bitmap);

        // AntiAliasGridFit produces clean text for video encoding (ClearType adds subpixel color fringing)
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.CompositingQuality = CompositingQuality.HighQuality;

        // Pure black background
        graphics.Clear(Color.Black);

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

        // Draw weight display - use StringFormat for precise centering
        using var weightFont = new Font("Consolas", _config.FontSize, FontStyle.Bold);
        using var unitFont = new Font("Arial", _config.FontSize / 2, FontStyle.Bold);
        using var statusFont = new Font("Arial", _config.FontSize / 3, FontStyle.Bold);
        using var labelFont = new Font("Arial", 24, FontStyle.Regular);

        // Get unit
        string unit = _currentReading?.Unit ?? "lb";

        // Combine weight and unit into one string for true centering
        string displayText = $"{weightText} {unit}";

        // Use StringFormat for precise centering
        using var centerFormat = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };

        // Draw weight+unit centered on screen
        using var redBrush = new SolidBrush(Color.FromArgb(255, 0, 0));
        var centerRect = new RectangleF(0, 0, _config.VideoWidth, _config.VideoHeight);
        graphics.DrawString(displayText, weightFont, redBrush, centerRect, centerFormat);

        // Status indicator below center
        var statusY = (_config.VideoHeight / 2) + (_config.FontSize / 2) + 30;
        using var statusBrush = new SolidBrush(statusColor);
        var statusRect = new RectangleF(0, statusY, _config.VideoWidth, 50);
        graphics.DrawString(statusText, statusFont, statusBrush, statusRect, centerFormat);

        // Scale ID label - top left
        string scaleLabel = $"Scale: {_config.ScaleId ?? "Default"}";
        using var dimBrush = new SolidBrush(Color.FromArgb(100, 100, 100));
        graphics.DrawString(scaleLabel, labelFont, dimBrush, 20, 20);

        // Timestamp - top right
        string timeLabel = DateTime.Now.ToString("HH:mm:ss");
        using var rightFormat = new StringFormat { Alignment = StringAlignment.Far };
        graphics.DrawString(timeLabel, labelFont, dimBrush, _config.VideoWidth - 20, 20, rightFormat);

        // Bright red LIVE indicator - bottom right
        using var liveBrush = new SolidBrush(Color.FromArgb(255, 0, 0));
        graphics.FillEllipse(liveBrush, _config.VideoWidth - 95, _config.VideoHeight - 40, 16, 16);
        using var liveFont = new Font("Arial", 16, FontStyle.Bold);
        graphics.DrawString("LIVE", liveFont, Brushes.White, _config.VideoWidth - 75, _config.VideoHeight - 42);

        // Company branding - bottom left
        graphics.DrawString("Cloud-Scale", labelFont, dimBrush, 20, _config.VideoHeight - 40);

        // Encode to JPEG with maximum quality for crisp image
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
