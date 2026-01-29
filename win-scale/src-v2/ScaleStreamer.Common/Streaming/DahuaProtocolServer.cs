using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ScaleStreamer.Common.Models;
using ScaleStreamer.Common.Settings;
using Serilog;

namespace ScaleStreamer.Common.Streaming;

/// <summary>
/// Emulates a Dahua IP camera using the proprietary "Private" protocol on TCP port 37777.
/// Handles authentication, system info queries, and streams H.264 video wrapped in DHAV containers.
/// </summary>
public class DahuaProtocolServer : IDisposable
{
    private static readonly ILogger _log = Log.ForContext<DahuaProtocolServer>();

    private readonly int _port;
    private readonly RtspStreamConfig _config;
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _acceptTask;
    private WeightReading? _currentReading;
    private readonly object _readingLock = new();

    // FFmpeg H.264 encoding (independent pipeline)
    private Process? _ffmpegProcess;
    private Task? _h264ReaderTask;
    private readonly List<byte[]> _pendingNalus = new();
    private readonly object _naluLock = new();
    private bool _ffmpegReady;

    // Pre-allocated rendering objects
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

    // Protocol state
    private uint _sessionId;
    private uint _frameNumber;
    private int _objectIdCounter;
    private readonly string _realm;
    private readonly string _random;
    private readonly string _serialNumber;

    // Per-connection state wrapper
    private class ConnectionState
    {
        public bool ChallengeSent;
        public byte RequestedChannel = 1; // Default to channel 1 (Dahua is 1-indexed)
    }

    public DahuaProtocolServer(int port, RtspStreamConfig config)
    {
        _port = port;
        _config = config;
        _sessionId = (uint)Random.Shared.Next(0x10000, int.MaxValue);
        _random = Random.Shared.Next(100000000, 999999999).ToString();
        _serialNumber = AppSettings.Instance.ScaleConnection.ScaleId;
        if (string.IsNullOrEmpty(_serialNumber))
            _serialNumber = "3G0" + Random.Shared.Next(10000, 99999) + "PAF" + Random.Shared.Next(10000, 99999);
        _realm = "Login to " + _serialNumber;
    }

    public void UpdateWeight(WeightReading reading)
    {
        lock (_readingLock)
        {
            _currentReading = reading;
        }
    }

    public async Task<bool> StartAsync()
    {
        try
        {
            _cts = new CancellationTokenSource();
            _listener = new TcpListener(IPAddress.Any, _port);
            _listener.Start();

            // Start FFmpeg H.264 pipeline
            if (!await StartFfmpegAsync())
            {
                _log.Error("Failed to start FFmpeg for Dahua server");
                return false;
            }

            // Accept client connections
            _acceptTask = Task.Run(() => AcceptClientsAsync(_cts.Token));

            _log.Information("Dahua protocol server listening on port {Port}", _port);
            return true;
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Failed to start Dahua protocol server");
            return false;
        }
    }

    public async Task StopAsync()
    {
        _cts?.Cancel();
        _listener?.Stop();
        StopFfmpeg();

        if (_acceptTask != null)
        {
            try { await _acceptTask.WaitAsync(TimeSpan.FromSeconds(5)); }
            catch { /* timeout ok */ }
        }

        _log.Information("Dahua protocol server stopped");
    }

    private async Task AcceptClientsAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var client = await _listener!.AcceptTcpClientAsync(ct);
                _log.Information("Dahua: Client connected from {Remote}", client.Client.RemoteEndPoint);
                _ = Task.Run(() => HandleClientAsync(client, ct), ct);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _log.Error(ex, "Dahua: Error accepting client");
                await Task.Delay(1000, ct);
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
    {
        using var _ = client;
        var stream = client.GetStream();
        var buffer = new byte[64 * 1024];
        bool authenticated = false;
        var connState = new ConnectionState(); // Per-connection login phase tracking
        var pendingData = new MemoryStream();
        Task? streamingTask = null;
        var streamingCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var writeLock = new SemaphoreSlim(1, 1); // Protect writes from streaming vs command responses

        try
        {
            while (!ct.IsCancellationRequested && client.Connected)
            {
                // Read data from NVR
                int bytesRead;
                try
                {
                    bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, ct);
                }
                catch (OperationCanceledException) { break; }
                catch (IOException) { break; }

                if (bytesRead == 0) break;

                _log.Information("Dahua RX [{Len}]: {Hex}", bytesRead,
                    BitConverter.ToString(buffer, 0, Math.Min(64, bytesRead)));

                // Append to pending data buffer for proper framing
                pendingData.Write(buffer, 0, bytesRead);

                // Process all complete messages in the buffer
                while (pendingData.Length >= 32)
                {
                    var allData = pendingData.ToArray();
                    byte firstByte = allData[0];

                    // Check if this looks like a Dahua binary message:
                    // All binary message types use 32-byte header + payload (length at bytes 4-7)
                    // Known types: 0x0A, 0x24, 0x68, 0xA0-AF, 0xF4, 0xF6
                    // Exclude: 0x20+DHIP (has "DHIP" at bytes 4-7), JSON ({), XML (<)
                    int payloadLenCheck = allData.Length >= 8 ? BitConverter.ToInt32(allData, 4) : -1;
                    bool isDhip = allData.Length >= 8 && allData[0] == 0x20 &&
                                  allData[4] == 'D' && allData[5] == 'H' && allData[6] == 'I' && allData[7] == 'P';
                    bool isBinaryMsg = !isDhip && firstByte != '{' && firstByte != '<' &&
                                       payloadLenCheck >= 0 && payloadLenCheck < 10 * 1024 * 1024;

                    if (isBinaryMsg)
                    {
                        // Binary DVRIP message: 32-byte header + payload
                        int payloadLen = payloadLenCheck;
                        int totalMsgLen = 32 + payloadLen;

                        if (allData.Length < totalMsgLen)
                            break; // Need more data

                        var msg = new byte[totalMsgLen];
                        Array.Copy(allData, msg, totalMsgLen);

                        _log.Information("Dahua: Processing msg type=0x{Type:X2} sub=0x{Sub:X2} payload={PLen} total={TLen}",
                            msg[0], msg[1], payloadLen, totalMsgLen);

                        // Consume this message from the buffer
                        var remaining = allData.Length - totalMsgLen;
                        pendingData = new MemoryStream();
                        if (remaining > 0)
                            pendingData.Write(allData, totalMsgLen, remaining);

                        if (firstByte == 0xf6)
                        {
                            // JSON command
                            var (response, startStream) = await HandleJsonCommand(msg, totalMsgLen, ct);
                            if (response != null)
                            {
                                await writeLock.WaitAsync(ct);
                                try { await stream.WriteAsync(response, ct); }
                                finally { writeLock.Release(); }
                            }

                            if (startStream && streamingTask == null)
                            {
                                _log.Information("Dahua: Starting DHAV video stream to client (background)");
                                streamingTask = Task.Run(() => StreamDhavFramesAsync(stream, writeLock, connState.RequestedChannel, streamingCts.Token), ct);
                            }
                        }
                        else if (firstByte == 0xf4)
                        {
                            // Legacy text command (TransactionID/Method format)
                            await writeLock.WaitAsync(ct);
                            try { await HandleTextCommand(stream, msg, totalMsgLen, ct); }
                            finally { writeLock.Release(); }
                        }
                        else
                        {
                            // Binary message (A0-AF, 0x68 stream, 0x24 time, etc.)
                            bool startStream = await HandleBinaryMessageWithLock(stream, msg, connState, writeLock, ct);
                            if (msg[0] == 0xa0 && (msg[1] == 0x00 || msg[1] == 0x05))
                                authenticated = true;

                            if (startStream && streamingTask == null)
                            {
                                _log.Information("Dahua: Starting DHAV video stream to client (binary trigger, background)");
                                streamingTask = Task.Run(() => StreamDhavFramesAsync(stream, writeLock, connState.RequestedChannel, streamingCts.Token), ct);
                            }
                        }
                    }
                    else if (firstByte == 0x20 || (allData.Length >= 8 && Encoding.ASCII.GetString(allData, 4, 4) == "DHIP"))
                    {
                        // DHIP: consume all pending data as one message for now
                        var data = allData;
                        pendingData = new MemoryStream();

                        var response = await HandleDhipMessage(data, data.Length, ct);
                        if (response != null)
                        {
                            await writeLock.WaitAsync(ct);
                            try { await stream.WriteAsync(response, ct); }
                            finally { writeLock.Release(); }
                            if (!authenticated)
                            {
                                var json = TryExtractDhipJson(data, data.Length);
                                if (json != null && json.Contains("global.login"))
                                    authenticated = true;
                            }
                        }
                        break;
                    }
                    else
                    {
                        // Unknown - try to parse as JSON
                        var data = allData;
                        pendingData = new MemoryStream();

                        var jsonStr = TryExtractJson(data, data.Length);
                        if (jsonStr != null)
                        {
                            _log.Information("Dahua: Received JSON: {Json}", jsonStr.Length > 500 ? jsonStr[..500] : jsonStr);
                            var (response, startStream) = await HandleJsonFromString(jsonStr, ct);
                            if (response != null)
                            {
                                await writeLock.WaitAsync(ct);
                                try { await stream.WriteAsync(response, ct); }
                                finally { writeLock.Release(); }
                            }

                            if (startStream && streamingTask == null)
                            {
                                _log.Information("Dahua: Starting DHAV video stream to client (background)");
                                streamingTask = Task.Run(() => StreamDhavFramesAsync(stream, writeLock, connState.RequestedChannel, streamingCts.Token), ct);
                            }
                        }
                        else
                        {
                            _log.Warning("Dahua: Unknown data, first bytes: {Hex}",
                                BitConverter.ToString(data, 0, Math.Min(32, data.Length)));
                        }
                        break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Dahua: Client handler error");
        }
        finally
        {
            // Stop streaming when client disconnects
            streamingCts.Cancel();
            if (streamingTask != null)
            {
                try { await streamingTask.WaitAsync(TimeSpan.FromSeconds(3)); }
                catch { /* timeout ok */ }
            }
            streamingCts.Dispose();
            writeLock.Dispose();
        }

        _log.Information("Dahua: Client disconnected");
    }

    #region Binary DVRIP Protocol (0xa0/0xb0)

    /// <returns>True if video streaming should be started</returns>
    private async Task<bool> HandleBinaryMessageWithLock(NetworkStream stream, byte[] data, ConnectionState connState, SemaphoreSlim writeLock, CancellationToken ct)
    {
        await writeLock.WaitAsync(ct);
        try
        {
            return await HandleBinaryMessage(stream, data, connState, ct);
        }
        finally
        {
            writeLock.Release();
        }
    }

    /// <returns>True if video streaming should be started</returns>
    private async Task<bool> HandleBinaryMessage(NetworkStream stream, byte[] data, ConnectionState connState, CancellationToken ct)
    {
        byte msgType = data[0];
        byte subType = data.Length > 1 ? data[1] : (byte)0;

        _log.Information("Dahua Binary: type=0x{Type:X2} sub=0x{Sub:X2}, len={Len}",
            msgType, subType, data.Length);

        if (msgType == 0x68)
        {
            // Real-time stream request (CYCL_START_REALPLAY)
            // byte[8] = stream type: 02=main, 03=sub1, 05=sub2
            // byte[12] = channel number (1-indexed for Dahua)
            byte streamType = data.Length > 8 ? data[8] : (byte)0x02;
            byte channelNum = data.Length > 12 ? data[12] : (byte)0x01;
            _log.Information("Dahua Binary: Stream request 0x68, streamType=0x{Type:X2} channel={Channel}", streamType, channelNum);

            // Store the requested channel for DHAV frames (use channel 0 if 1 requested, since we only have 1 channel)
            // Dahua channel numbers are 1-indexed, DHAV frame channel field is 0-indexed
            connState.RequestedChannel = channelNum > 0 ? (byte)(channelNum - 1) : (byte)0;

            // Only accept main stream (0x02) for channel 1
            if (streamType == 0x02 && channelNum == 0x01)
            {
                // Send stream start acknowledgement
                var response = new byte[32];
                response[0] = 0x69; // response to 0x68
                response[2] = 0x00;
                response[3] = 0x68;
                response[8] = 0x00;
                response[9] = 0x08;
                response[10] = 0x01;
                response[11] = 0x00;
                BitConverter.GetBytes(_sessionId).CopyTo(response, 16);
                response[20] = 0x01;
                response[24] = 0x06;
                response[25] = 0x00;
                response[26] = 0xf9;
                response[27] = 0x00;
                response[28] = 0x00;
                response[29] = 0x07;
                response[30] = 0x00;
                response[31] = 0x02;
                _log.Information("Dahua TX [{Len}]: {Hex}", response.Length, BitConverter.ToString(response));
                await stream.WriteAsync(response, ct);
                return true; // Signal to start streaming
            }
            else
            {
                _log.Information("Dahua Binary: Ignoring stream request for streamType=0x{Type:X2} channel={Channel} (we only have main stream on channel 1)", streamType, channelNum);
                // Send acknowledgement but don't start streaming for other channels
                var response = new byte[32];
                response[0] = 0x69;
                response[2] = 0x00;
                response[3] = 0x68;
                response[8] = 0x00;
                response[9] = 0x08;
                response[10] = 0x01;
                response[11] = 0x00;
                BitConverter.GetBytes(_sessionId).CopyTo(response, 16);
                await stream.WriteAsync(response, ct);
                return false;
            }
        }

        if (msgType == 0x24)
        {
            // Time sync or similar — acknowledge
            _log.Information("Dahua Binary: Time/sync cmd 0x24");
            var response = new byte[32];
            response[0] = 0x25; // response
            response[2] = 0x00;
            response[3] = 0x68;
            response[8] = 0x00;
            response[9] = 0x08;
            response[10] = 0x01;
            response[11] = 0x00;
            BitConverter.GetBytes(_sessionId).CopyTo(response, 16);
            response[20] = 0x01;
            response[24] = 0x06;
            response[25] = 0x00;
            response[26] = 0xf9;
            response[27] = 0x00;
            response[28] = 0x00;
            response[29] = 0x07;
            response[30] = 0x00;
            response[31] = 0x02;
            await stream.WriteAsync(response, ct);
            return false;
        }

        if (msgType <= 0x0f)
        {
            // Low-byte message (e.g., 0x0A = channel/session management)
            _log.Information("Dahua Binary: Low-byte cmd 0x{Type:X2}, acknowledging", msgType);
            var response = new byte[32];
            response[0] = (byte)(msgType + 0x10);
            response[2] = 0x00;
            response[3] = 0x68;
            response[8] = 0x00;
            response[9] = 0x08;
            response[10] = 0x01;
            response[11] = 0x00;
            BitConverter.GetBytes(_sessionId).CopyTo(response, 16);
            response[20] = 0x01;
            response[24] = 0x06;
            response[25] = 0x00;
            response[26] = 0xf9;
            response[27] = 0x00;
            response[28] = 0x00;
            response[29] = 0x07;
            response[30] = 0x00;
            response[31] = 0x02;
            _log.Information("Dahua TX [{Len}]: {Hex}", response.Length, BitConverter.ToString(response));
            await stream.WriteAsync(response, ct);
            return false;
        }

        if (msgType == 0xa1 && subType == 0x00)
        {
            // Connection probe — respond with probe ack
            var response = new byte[32];
            response[0] = 0xb1;
            response[1] = 0x00;
            response[2] = 0x00;
            response[3] = 0x58;
            _log.Information("Dahua TX [{Len}]: {Hex}", response.Length, BitConverter.ToString(response));
            await stream.WriteAsync(response, ct);
        }
        else if (msgType == 0xa0 && subType == 0x01)
        {
            // REALM request — send realm + random
            var realmPayload = Encoding.UTF8.GetBytes(
                $"{{\"encryption\":\"Default\",\"mac\":\"\",\"random\":\"{_random}\",\"realm\":\"{_realm}\"}}");

            var response = new byte[32 + realmPayload.Length];
            response[0] = 0xb0;
            response[1] = 0x01;
            response[8] = 0x01;
            response[9] = 0x00;
            response[10] = 0x00;
            response[11] = 0x10;
            BitConverter.GetBytes(realmPayload.Length).CopyTo(response, 12);
            response[28] = 0x05;
            response[29] = 0x02;
            response[30] = 0x01;
            response[31] = 0x01;
            Array.Copy(realmPayload, 0, response, 32, realmPayload.Length);

            _log.Information("Dahua TX [{Len}]: {Hex}", response.Length,
                BitConverter.ToString(response, 0, Math.Min(64, response.Length)));
            await stream.WriteAsync(response, ct);
        }
        else if (msgType == 0xa0 && (subType == 0x05 || subType == 0x00))
        {
            // Real Dahua camera login flow (captured from 10.172.80.245:37777):
            // 1. NVR sends A0-05 (encrypted creds) → Camera responds with CHALLENGE (realm+random)
            // 2. NVR sends A0-05 (hashed creds)    → Camera responds with SUCCESS (session ID)
            //
            // Challenge response: B0 00 00 68 + payload_len + error(01 0E 01 00) + realm text
            // Success response:   B0 00 00 68 + 0 + error(00 08 01 00) + session at [16-19]
            // Trailer always:     06 00 F9 00 00 07 00 02

            int payloadLen = data.Length > 7 ? BitConverter.ToInt32(data, 4) : 0;

            if (!connState.ChallengeSent)
            {
                // Phase 1: Send challenge with realm + random
                connState.ChallengeSent = true;
                var realmText = $"Realm:{_realm}\r\nRandom:{_random}\r\n\r\n";
                var realmBytes = Encoding.UTF8.GetBytes(realmText);

                var response = new byte[32 + realmBytes.Length];
                response[0] = 0xb0;
                response[1] = 0x00;
                response[2] = 0x00;
                response[3] = 0x68;
                // Payload length
                BitConverter.GetBytes(realmBytes.Length).CopyTo(response, 4);
                // Error code: 01 0E 01 00 = challenge needed
                response[8] = 0x01;
                response[9] = 0x0e;
                response[10] = 0x01;
                response[11] = 0x00;
                // Bytes 12-19: zeros (no session yet)
                // Bytes 20-23: 01 00 00 00
                response[20] = 0x01;
                // Trailer: 06 00 F9 00 00 00 00 02
                response[24] = 0x06;
                response[25] = 0x00;
                response[26] = 0xf9;
                response[27] = 0x00;
                response[28] = 0x00;
                response[29] = 0x00;
                response[30] = 0x00;
                response[31] = 0x02;
                // Copy realm payload
                Array.Copy(realmBytes, 0, response, 32, realmBytes.Length);

                _log.Information("Dahua TX [{Len}]: {Hex}", response.Length,
                    BitConverter.ToString(response, 0, Math.Min(64, response.Length)));
                await stream.WriteAsync(response, ct);
                _log.Information("Dahua Binary: Challenge sent (realm={Realm}, random={Random})", _realm, _random);
            }
            else
            {
                // Phase 2: Accept credentials, send success with session ID
                if (payloadLen > 0 && data.Length > 32)
                {
                    var credsText = Encoding.UTF8.GetString(data, 32, Math.Min(payloadLen, data.Length - 32));
                    _log.Information("Dahua Binary: Received credentials: {Creds}", credsText);
                }

                var response = new byte[32];
                response[0] = 0xb0;
                response[1] = 0x00;
                response[2] = 0x00;
                response[3] = 0x68;
                // Bytes 4-7: zero (no payload)
                // Error code: 00 08 01 00 = success
                response[8] = 0x00;
                response[9] = 0x08;
                response[10] = 0x01;
                response[11] = 0x00;
                // Bytes 12-15: 00 00 00 00
                // Session ID at bytes 16-19
                BitConverter.GetBytes(_sessionId).CopyTo(response, 16);
                // Bytes 20-23: 01 00 00 00
                response[20] = 0x01;
                // Trailer: 06 00 F9 00 00 07 00 02
                response[24] = 0x06;
                response[25] = 0x00;
                response[26] = 0xf9;
                response[27] = 0x00;
                response[28] = 0x00;
                response[29] = 0x07;
                response[30] = 0x00;
                response[31] = 0x02;

                _log.Information("Dahua TX [{Len}]: {Hex}", response.Length, BitConverter.ToString(response));
                await stream.WriteAsync(response, ct);
                _log.Information("Dahua Binary: Login SUCCESS sent, session=0x{Session:X8}", _sessionId);
            }
        }
        else if (msgType == 0xa4)
        {
            // System info query — subcmd at byte[8] determines what info is requested
            // A4 responses use BINARY payloads, not JSON!
            // Header: B4 00 00 68, payload len at [4-7], subcmd echoed at [8-11], rest zeros
            byte subCmd = data.Length > 8 ? data[8] : (byte)0;
            _log.Information("Dahua Binary: A4 subcmd=0x{SubCmd:X2}", subCmd);

            byte[] payload = GetA4BinaryPayload(subCmd);

            var response = new byte[32 + payload.Length];
            response[0] = 0xb4;
            response[1] = 0x00;
            response[2] = 0x00;
            response[3] = 0x68;
            BitConverter.GetBytes(payload.Length).CopyTo(response, 4);
            // Echo subcmd at bytes 8-11 (NOT success code — matches real camera)
            response[8] = subCmd;
            response[9] = 0x00;
            response[10] = 0x00;
            response[11] = 0x00;
            // Bytes 12-19: some have 0x01 at byte 18 for subcmd 0x02
            if (subCmd == 0x02) response[18] = 0x01;
            // Bytes 20-31: zeros (real camera has all zeros)
            Array.Copy(payload, 0, response, 32, payload.Length);
            await stream.WriteAsync(response, ct);
            _log.Information("Dahua Binary: Sent A4 binary response for subcmd=0x{SubCmd:X2}, payload={Len} bytes", subCmd, payload.Length);
        }
        else if (msgType == 0xa8)
        {
            // Channel info query
            var channelJson = GetChannelInfoJson();
            var payload = Encoding.UTF8.GetBytes(channelJson);
            var response = new byte[32 + payload.Length];
            response[0] = 0xb8;
            response[1] = subType;
            response[2] = 0x00;
            response[3] = 0x68;
            BitConverter.GetBytes(payload.Length).CopyTo(response, 4);
            response[8] = 0x00;
            response[9] = 0x08;
            response[10] = 0x01;
            response[11] = 0x00;
            BitConverter.GetBytes(_sessionId).CopyTo(response, 16);
            response[20] = 0x01;
            response[24] = 0x06;
            response[25] = 0x00;
            response[26] = 0xf9;
            response[27] = 0x00;
            response[28] = 0x00;
            response[29] = 0x07;
            response[30] = 0x00;
            response[31] = 0x02;
            Array.Copy(payload, 0, response, 32, payload.Length);
            await stream.WriteAsync(response, ct);
            _log.Information("Dahua Binary: Sent channel info");
        }
        else
        {
            // Unknown binary command — send generic success (matching real camera format)
            var response = new byte[32];
            response[0] = (byte)(msgType + 0x10); // response = request + 0x10
            response[1] = subType;
            response[2] = 0x00;
            response[3] = 0x68;
            // Success error code
            response[8] = 0x00;
            response[9] = 0x08;
            response[10] = 0x01;
            response[11] = 0x00;
            // Session ID at bytes 16-19
            BitConverter.GetBytes(_sessionId).CopyTo(response, 16);
            response[20] = 0x01;
            // Trailer
            response[24] = 0x06;
            response[25] = 0x00;
            response[26] = 0xf9;
            response[27] = 0x00;
            response[28] = 0x00;
            response[29] = 0x07;
            response[30] = 0x00;
            response[31] = 0x02;
            _log.Information("Dahua TX [{Len}]: {Hex}", response.Length, BitConverter.ToString(response));
            await stream.WriteAsync(response, ct);
            _log.Information("Dahua Binary: Sent generic OK for 0x{Type:X2} 0x{Sub:X2}", msgType, subType);
        }

        return false;
    }

    private async Task HandleTextCommand(NetworkStream stream, byte[] data, int totalLen, CancellationToken ct)
    {
        // F4 text-based command protocol: 32-byte header + text payload
        // Payload format: "TransactionID:NNNNN\r\nMethod:SomeMethod\r\n...\r\n\r\n"
        int payloadLen = totalLen > 32 ? BitConverter.ToInt32(data, 4) : 0;
        var payloadText = payloadLen > 0 && totalLen > 32
            ? Encoding.UTF8.GetString(data, 32, Math.Min(payloadLen, totalLen - 32)).TrimEnd('\0')
            : "";

        _log.Information("Dahua Text Cmd: {Text}", payloadText.Length > 300 ? payloadText[..300] : payloadText);

        // Parse TransactionID, Method, and ParameterName
        string transactionId = "";
        string method = "";
        string parameterName = "";
        foreach (var line in payloadText.Split("\r\n", StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.StartsWith("TransactionID:"))
                transactionId = line["TransactionID:".Length..];
            else if (line.StartsWith("Method:"))
                method = line["Method:".Length..];
            else if (line.StartsWith("ParameterName:"))
                parameterName = line["ParameterName:".Length..];
        }

        _log.Information("Dahua Text: TransactionID={TxId} Method={Method} Param={Param}", transactionId, method, parameterName);

        // Build F5 text response based on method
        string responseText;
        if (method == "AddObject")
        {
            // AddObject requires returning an ObjectID
            var objectId = ++_objectIdCounter;
            responseText = $"TransactionID:{transactionId}\r\nAction:Response\r\nCode:200\r\nDescription:OK\r\nObjectID:{objectId}\r\n\r\n";
            _log.Information("Dahua Text: Created object {ObjectId} for {Param}", objectId, parameterName);
        }
        else if (method == "QueryObject" || method == "Query")
        {
            // Query responses need to include requested parameters
            responseText = $"TransactionID:{transactionId}\r\nAction:Response\r\nCode:200\r\nDescription:OK\r\n\r\n";
        }
        else if (method == "QueryDevState")
        {
            // Device state query - return online/connected state
            responseText = $"TransactionID:{transactionId}\r\nAction:Response\r\nCode:200\r\nDescription:OK\r\nState:Online\r\n\r\n";
        }
        else
        {
            // Generic success response
            responseText = $"TransactionID:{transactionId}\r\nAction:Response\r\nCode:200\r\nDescription:OK\r\n\r\n";
        }

        var responsePayload = Encoding.UTF8.GetBytes(responseText);

        var response = new byte[32 + responsePayload.Length];
        response[0] = 0xf5; // F5 = text response
        BitConverter.GetBytes(responsePayload.Length).CopyTo(response, 4);
        // Session ID
        BitConverter.GetBytes(_sessionId).CopyTo(response, 24);
        Array.Copy(responsePayload, 0, response, 32, responsePayload.Length);

        _log.Information("Dahua TX Text [{Len}]: {Text}", response.Length, responseText.TrimEnd());
        await stream.WriteAsync(response, ct);
    }

    #endregion

    #region DHIP Protocol (modern JSON-over-binary)

    private async Task<byte[]?> HandleDhipMessage(byte[] data, int length, CancellationToken ct)
    {
        // DHIP header: 8 bytes magic + 4 session + 4 reqId + 4 payloadLen + 4 reserved + 4 payloadLen2 + 4 reserved
        if (length < 32) return null;

        var payloadLen = BitConverter.ToInt32(data, 16);
        if (payloadLen <= 0 || 32 + payloadLen > length) payloadLen = length - 32;

        var jsonStr = Encoding.UTF8.GetString(data, 32, Math.Min(payloadLen, length - 32)).TrimEnd('\0');
        _log.Information("Dahua DHIP: {Json}", jsonStr.Length > 500 ? jsonStr[..500] : jsonStr);

        JsonNode? json;
        try { json = JsonNode.Parse(jsonStr); }
        catch { return null; }

        if (json == null) return null;

        var method = json["method"]?.GetValue<string>() ?? "";
        var id = json["id"]?.GetValue<int>() ?? 1;
        var sessionHex = json["session"]?.GetValue<string>() ?? "0x00000000";

        string responseJson;

        if (method == "global.login")
        {
            var password = json["params"]?["password"]?.GetValue<string>() ?? "";
            if (string.IsNullOrEmpty(password))
            {
                // First login attempt — return challenge
                responseJson = JsonSerializer.Serialize(new
                {
                    error = new { code = 268632079, message = "Component error: login challenge" },
                    id,
                    @params = new
                    {
                        encryption = "Default",
                        mac = "00:00:00:00:00:00",
                        random = _random,
                        realm = _realm
                    },
                    result = false,
                    session = $"0x{_sessionId:X8}"
                });
            }
            else
            {
                // Second attempt with credentials — accept
                responseJson = JsonSerializer.Serialize(new
                {
                    id,
                    @params = new { keepAliveInterval = 60 },
                    result = true,
                    session = $"0x{_sessionId:X8}"
                });
                _log.Information("Dahua DHIP: Login accepted");
            }
        }
        else
        {
            // Route to JSON command handler
            var (respBytes, _) = await HandleJsonFromString(jsonStr, ct);
            if (respBytes != null) return respBytes;

            responseJson = JsonSerializer.Serialize(new
            {
                id,
                result = true,
                session = $"0x{_sessionId:X8}"
            });
        }

        return BuildDhipPacket(responseJson, id);
    }

    private byte[] BuildDhipPacket(string json, int requestId)
    {
        var payload = Encoding.UTF8.GetBytes(json);
        var packet = new byte[32 + payload.Length];

        // DHIP magic: 0x20 0x00 0x00 0x00 "DHIP"
        packet[0] = 0x20;
        packet[4] = (byte)'D';
        packet[5] = (byte)'H';
        packet[6] = (byte)'I';
        packet[7] = (byte)'P';
        // Session ID
        BitConverter.GetBytes(_sessionId).CopyTo(packet, 8);
        // Request ID
        BitConverter.GetBytes(requestId).CopyTo(packet, 12);
        // Payload length (twice, per protocol)
        BitConverter.GetBytes(payload.Length).CopyTo(packet, 16);
        BitConverter.GetBytes(payload.Length).CopyTo(packet, 24);
        // Copy JSON payload
        Array.Copy(payload, 0, packet, 32, payload.Length);

        return packet;
    }

    private string? TryExtractDhipJson(byte[] data, int length)
    {
        if (length <= 32) return null;
        try
        {
            return Encoding.UTF8.GetString(data, 32, length - 32).TrimEnd('\0');
        }
        catch { return null; }
    }

    #endregion

    #region JSON Command Handler (0xf6 protocol)

    private async Task<(byte[]? response, bool startStream)> HandleJsonCommand(byte[] data, int length, CancellationToken ct)
    {
        // 0xf6 header: 4 magic + 4 payloadLen + 4 reqId + 4 reserved + 4 payloadLen2 + 4 reserved + 4 session + 4 reserved
        if (length < 32) return (null, false);

        var payloadLen = BitConverter.ToInt32(data, 4);
        if (payloadLen <= 0 || 32 + payloadLen > length) payloadLen = length - 32;

        var jsonStr = Encoding.UTF8.GetString(data, 32, Math.Min(payloadLen, length - 32)).TrimEnd('\0');
        return await HandleJsonFromString(jsonStr, ct);
    }

    private async Task<(byte[]? response, bool startStream)> HandleJsonFromString(string jsonStr, CancellationToken ct)
    {
        _log.Information("Dahua JSON: {Json}", jsonStr.Length > 500 ? jsonStr[..500] : jsonStr);

        JsonNode? json;
        try { json = JsonNode.Parse(jsonStr); }
        catch
        {
            _log.Warning("Dahua: Failed to parse JSON: {Json}", jsonStr[..Math.Min(200, jsonStr.Length)]);
            return (null, false);
        }
        if (json == null) return (null, false);

        var method = json["method"]?.GetValue<string>() ?? "";
        var id = json["id"]?.GetValue<int>() ?? 1;
        bool startStream = false;
        string responseJson;

        switch (method)
        {
            case "global.login":
            {
                var password = json["params"]?["password"]?.GetValue<string>() ?? "";
                if (string.IsNullOrEmpty(password))
                {
                    responseJson = JsonSerializer.Serialize(new
                    {
                        error = new { code = 268632079, message = "Component error: login challenge" },
                        id,
                        @params = new
                        {
                            encryption = "Default",
                            mac = "00:00:00:00:00:00",
                            random = _random,
                            realm = _realm
                        },
                        result = false,
                        session = $"0x{_sessionId:X8}"
                    });
                }
                else
                {
                    responseJson = JsonSerializer.Serialize(new
                    {
                        id,
                        @params = new { keepAliveInterval = 60 },
                        result = true,
                        session = $"0x{_sessionId:X8}"
                    });
                    _log.Information("Dahua JSON: Login accepted");
                }
                break;
            }

            case "global.keepAlive":
                responseJson = JsonSerializer.Serialize(new
                {
                    id,
                    @params = new { timeout = 300 },
                    result = true,
                    session = $"0x{_sessionId:X8}"
                });
                break;

            case "magicBox.getSystemInfo":
                responseJson = GetSystemInfoJson();
                break;

            case "magicBox.getSoftwareVersion":
                responseJson = JsonSerializer.Serialize(new
                {
                    id,
                    @params = new
                    {
                        version = new
                        {
                            BuildDate = "2024-01-15",
                            SecurityBaseLineVersion = "2.0",
                            Version = "2.800.0000000.1",
                            WebVersion = "3.2.1"
                        }
                    },
                    result = true,
                    session = $"0x{_sessionId:X8}"
                });
                break;

            case "magicBox.getDeviceType":
                responseJson = JsonSerializer.Serialize(new
                {
                    id,
                    @params = new { type = "IPC-HFW2431T-ZS" },
                    result = true,
                    session = $"0x{_sessionId:X8}"
                });
                break;

            case "magicBox.getSerialNo":
                responseJson = JsonSerializer.Serialize(new
                {
                    id,
                    @params = new { sn = _serialNumber },
                    result = true,
                    session = $"0x{_sessionId:X8}"
                });
                break;

            case "magicBox.getDeviceClass":
                responseJson = JsonSerializer.Serialize(new
                {
                    id,
                    @params = new { type = "IPC" },
                    result = true,
                    session = $"0x{_sessionId:X8}"
                });
                break;

            case "magicBox.getProductDefinition":
                responseJson = JsonSerializer.Serialize(new
                {
                    id,
                    @params = new { definition = "IPC-HFW2431T-ZS" },
                    result = true,
                    session = $"0x{_sessionId:X8}"
                });
                break;

            case "magicBox.getMarketArea":
                responseJson = JsonSerializer.Serialize(new
                {
                    id,
                    @params = new { area = "International" },
                    result = true,
                    session = $"0x{_sessionId:X8}"
                });
                break;

            case "configManager.getConfig":
            {
                var configName = json["params"]?["name"]?.GetValue<string>() ?? "";
                responseJson = HandleConfigQuery(configName, id);
                break;
            }

            case "configManager.getDefault":
            {
                var configName = json["params"]?["name"]?.GetValue<string>() ?? "";
                responseJson = HandleConfigQuery(configName, id);
                break;
            }

            case "mediaStream.factory.instance":
            {
                var objectId = ++_objectIdCounter;
                responseJson = JsonSerializer.Serialize(new
                {
                    id,
                    @params = new { },
                    result = objectId,
                    session = $"0x{_sessionId:X8}"
                });
                _log.Information("Dahua: mediaStream factory created, objectId={Id}", objectId);
                break;
            }

            case "mediaStream.attach":
            {
                responseJson = JsonSerializer.Serialize(new
                {
                    id,
                    result = true,
                    session = $"0x{_sessionId:X8}"
                });
                startStream = true;
                _log.Information("Dahua: mediaStream.attach — will start video streaming");
                break;
            }

            case "mediaStream.detach":
            case "mediaStream.destroy":
            case "mediaStream.factory.close":
                responseJson = JsonSerializer.Serialize(new
                {
                    id,
                    result = true,
                    session = $"0x{_sessionId:X8}"
                });
                break;

            case "system.listMethod":
            case "magicBox.listMethod":
                responseJson = JsonSerializer.Serialize(new
                {
                    id,
                    @params = new
                    {
                        method = new[]
                        {
                            "configManager.getConfig", "configManager.getDefault",
                            "magicBox.getSystemInfo", "magicBox.getSoftwareVersion",
                            "magicBox.getDeviceType", "magicBox.getSerialNo",
                            "magicBox.getDeviceClass", "magicBox.getProductDefinition",
                            "magicBox.getMarketArea", "magicBox.listMethod",
                            "system.listMethod", "global.login", "global.keepAlive",
                            "mediaStream.factory.instance", "mediaStream.attach",
                            "mediaStream.detach", "mediaStream.destroy",
                            "mediaStream.factory.close",
                            "devVideoInput.factory.instance", "devVideoInput.factory.getCollect",
                            "alarm.getAllInSlots", "alarm.getAllOutSlots"
                        }
                    },
                    result = true,
                    session = $"0x{_sessionId:X8}"
                });
                break;

            case "devVideoInput.factory.instance":
            case "devVideoEncode.factory.instance":
            case "devAudioEncode.factory.instance":
            case "devAudioInput.factory.instance":
            {
                var objectId = ++_objectIdCounter;
                responseJson = JsonSerializer.Serialize(new
                {
                    id,
                    result = objectId,
                    session = $"0x{_sessionId:X8}"
                });
                _log.Information("Dahua: Factory instance created for {Method}, objectId={Id}", method, objectId);
                break;
            }

            case "devVideoInput.factory.getCollect":
            case "devVideoEncode.factory.getCollect":
                responseJson = JsonSerializer.Serialize(new
                {
                    id,
                    @params = new
                    {
                        videoInputChannels = 1,
                        videoOutputChannels = 0
                    },
                    result = true,
                    session = $"0x{_sessionId:X8}"
                });
                break;

            case "devAudioEncode.factory.getCollect":
            case "devAudioInput.factory.getCollect":
                responseJson = JsonSerializer.Serialize(new
                {
                    id,
                    @params = new
                    {
                        audioInputChannels = 0,
                        audioOutputChannels = 0
                    },
                    result = true,
                    session = $"0x{_sessionId:X8}"
                });
                break;

            case "MediaEncrypt.getCaps":
            case "MediaEncrypt.listMethod":
                responseJson = JsonSerializer.Serialize(new
                {
                    id,
                    @params = new
                    {
                        caps = new { encrypt = false, encryptVersion = "None" }
                    },
                    result = true,
                    session = $"0x{_sessionId:X8}"
                });
                break;

            case "alarm.getAllInSlots":
            case "alarm.getAllOutSlots":
                responseJson = JsonSerializer.Serialize(new
                {
                    id,
                    @params = new { slots = Array.Empty<object>() },
                    result = true,
                    session = $"0x{_sessionId:X8}"
                });
                break;

            case "ChannelManager.getVirtualChannels":
                // Tell clients this is a simple camera with no virtual channels
                responseJson = JsonSerializer.Serialize(new
                {
                    id,
                    @params = new { channels = Array.Empty<object>() },
                    result = true,
                    session = $"0x{_sessionId:X8}"
                });
                break;

            case "magicBox.getHardwareVersion":
                responseJson = JsonSerializer.Serialize(new
                {
                    id,
                    @params = new { version = "1.0" },
                    result = true,
                    session = $"0x{_sessionId:X8}"
                });
                break;

            case "magicBox.factory.instance":
                responseJson = JsonSerializer.Serialize(new
                {
                    id,
                    result = 1,  // Return object ID
                    session = $"0x{_sessionId:X8}"
                });
                break;

            default:
                _log.Information("Dahua: Unknown method '{Method}' — returning success", method);
                responseJson = JsonSerializer.Serialize(new
                {
                    id,
                    result = true,
                    session = $"0x{_sessionId:X8}"
                });
                break;
        }

        return (BuildF6Packet(responseJson, id), startStream);
    }

    private string HandleConfigQuery(string configName, int id)
    {
        _log.Information("Dahua: configManager query for '{Name}'", configName);

        return configName switch
        {
            "Encode" => JsonSerializer.Serialize(new
            {
                id,
                @params = new
                {
                    table = new[]
                    {
                        new
                        {
                            MainFormat = new[]
                            {
                                new
                                {
                                    Audio = new { AudioEnable = false },
                                    Video = new
                                    {
                                        BitRate = 512,
                                        BitRateControl = "CBR",
                                        Compression = "H.264",
                                        FPS = _config.FrameRate,
                                        GOP = _config.FrameRate,
                                        Height = _config.VideoHeight,
                                        Width = _config.VideoWidth,
                                        Quality = 4,
                                        Profile = "Baseline"
                                    },
                                    VideoEnable = true
                                }
                            },
                            ExtraFormat = new[]
                            {
                                new
                                {
                                    Audio = new { AudioEnable = false },
                                    Video = new
                                    {
                                        BitRate = 256,
                                        BitRateControl = "CBR",
                                        Compression = "H.264",
                                        FPS = _config.FrameRate,
                                        GOP = _config.FrameRate,
                                        Height = _config.VideoHeight,
                                        Width = _config.VideoWidth,
                                        Quality = 4,
                                        Profile = "Baseline"
                                    },
                                    VideoEnable = true
                                }
                            }
                        }
                    }
                },
                result = true,
                session = $"0x{_sessionId:X8}"
            }),

            "ChannelTitle" => JsonSerializer.Serialize(new
            {
                id,
                @params = new
                {
                    table = new[] { new { Name = "Scale Camera" } }
                },
                result = true,
                session = $"0x{_sessionId:X8}"
            }),

            "VideoColor" => JsonSerializer.Serialize(new
            {
                id,
                @params = new
                {
                    table = new[]
                    {
                        new
                        {
                            Brightness = 50,
                            Contrast = 50,
                            Hue = 50,
                            Saturation = 50,
                            Gain = 50,
                            GainBlue = 50,
                            GainGreen = 50,
                            GainRed = 50
                        }
                    }
                },
                result = true,
                session = $"0x{_sessionId:X8}"
            }),

            _ => JsonSerializer.Serialize(new
            {
                id,
                @params = new { table = Array.Empty<object>() },
                result = true,
                session = $"0x{_sessionId:X8}"
            })
        };
    }

    private byte[] GetA4BinaryPayload(byte subCmd)
    {
        switch (subCmd)
        {
            case 0x01:
            {
                // System info binary struct (32 bytes) - exact match to real Dahua IPC-HFW4431R-Z
                // Real: 02 2A 01 00 00 00 02 01 02 01 00 00 E0 07 0C 09
                //       00 00 33 2E 32 2E 00 00 00 00 01 00 00 00 00 00
                var payload = new byte[32];
                payload[0] = 0x02; payload[1] = 0x2A; payload[2] = 0x01; payload[3] = 0x00;
                payload[4] = 0x00; payload[5] = 0x00; payload[6] = 0x02; payload[7] = 0x01;
                payload[8] = 0x02; payload[9] = 0x01;
                // Build date: 2024-01-15 (year=0x07E8 LE, month=01, day=0F)
                payload[12] = 0xE8; payload[13] = 0x07; payload[14] = 0x01; payload[15] = 0x0F;
                // Version string at offset 18: "2.8." (matching real camera format "X.Y.")
                payload[18] = 0x32; // '2'
                payload[19] = 0x2E; // '.'
                payload[20] = 0x38; // '8'
                payload[21] = 0x2E; // '.'
                // Video input channels = 1 at offset 26
                payload[26] = 0x01;
                return payload;
            }

            case 0x07:
            {
                // Serial number as plain ASCII text (no null terminator)
                return Encoding.ASCII.GetBytes(_serialNumber);
            }

            case 0x08:
            {
                // Firmware version string, null-terminated, 16 bytes
                // Real camera: "2.420.0000.22.R\0"
                var versionBytes = new byte[16];
                var vstr = Encoding.ASCII.GetBytes("2.800.0000.0.R");
                Array.Copy(vstr, versionBytes, Math.Min(vstr.Length, 15));
                // Last byte is already 0x00 (null terminator)
                return versionBytes;
            }

            case 0x0B:
            {
                // Device class/model string, plain ASCII (no null terminator)
                // Real camera: "IPC-HFW4431R-Z"
                return Encoding.ASCII.GetBytes("IPC-HFW2431T-ZS");
            }

            case 0x02:
            {
                // Device capabilities binary struct (288 bytes, mostly zeros)
                // Real camera: byte 8 = 0xFF
                var payload = new byte[288];
                payload[8] = 0xFF;
                return payload;
            }

            case 0x1A:
            {
                // Capabilities text (&&-separated) - match real camera format
                var caps = "FTP:1:Record,Snap" +
                           "&&NTP:2:AdjustSysTime" +
                           "&&VideoCover:1:MutiCover" +
                           "&&AutoRegister:1:Login" +
                           "&&AutoMaintain:1:Reboot,DeleteFiles,ShutDown" +
                           "&&UPNP:1:SearchDevice" +
                           "&&DHCP:1:RequestIP" +
                           "&&DefaultQuery:1:DQuery" +
                           "&&DavinciModule:1:WorkSheetCFGApart,StandardGOP" +
                           "&&Dahua.a4.9:1:Login" +
                           "&&Log:1:PageForPageLog" +
                           "&&QueryURL:1:CONFIG" +
                           "&&SearchRecord:1:V3" +
                           "&&BackupVideoExtFormat:1:DAV,ASF" +
                           "&&Dahua_Config:1:Json,V3,MotionDetect_F6" +
                           "&&ProtocolFramework:1:V3_1";
                return Encoding.ASCII.GetBytes(caps);
            }

            default:
            {
                // Unknown subcmd — return empty payload
                return Array.Empty<byte>();
            }
        }
    }

    private string GetSystemInfoJson()
    {
        return JsonSerializer.Serialize(new
        {
            id = 1,
            @params = new
            {
                deviceType = "IPC-HFW2431T-ZS",
                hardwareVersion = "1.00",
                processor = "SSC337DE",
                serialNumber = _serialNumber,
                updateSerial = new
                {
                    Builtin = "Unknown",
                    Serial = _serialNumber
                }
            },
            result = true,
            session = $"0x{_sessionId:X8}"
        });
    }

    private string GetSoftwareVersionJson()
    {
        return JsonSerializer.Serialize(new
        {
            id = 1,
            @params = new
            {
                version = new
                {
                    BuildDate = "2024-01-15",
                    SecurityBaseLineVersion = "2.0",
                    Version = "2.800.0000000.1",
                    WebVersion = "3.2.1"
                }
            },
            result = true,
            session = $"0x{_sessionId:X8}"
        });
    }

    private string GetDeviceTypeJson()
    {
        return JsonSerializer.Serialize(new
        {
            id = 1,
            @params = new
            {
                type = "IPC-HFW2431T-ZS",
                deviceClass = "IPC",
                serialNumber = _serialNumber
            },
            result = true,
            session = $"0x{_sessionId:X8}"
        });
    }

    private string GetChannelInfoJson()
    {
        return JsonSerializer.Serialize(new
        {
            id = 1,
            @params = new
            {
                table = new[]
                {
                    new
                    {
                        ChannelName = "Scale Camera",
                        Detail = new
                        {
                            Compression = "H.264",
                            Height = _config.VideoHeight,
                            Width = _config.VideoWidth,
                            FPS = _config.FrameRate
                        }
                    }
                }
            },
            result = true,
            session = $"0x{_sessionId:X8}"
        });
    }

    private byte[] BuildF6Packet(string json, int requestId)
    {
        var payload = Encoding.UTF8.GetBytes(json);
        var packet = new byte[32 + payload.Length];

        // Magic: 0xf6000000
        packet[0] = 0xf6;
        // Payload length
        BitConverter.GetBytes(payload.Length).CopyTo(packet, 4);
        // Request ID
        BitConverter.GetBytes(requestId).CopyTo(packet, 8);
        // Payload length (duplicate)
        BitConverter.GetBytes(payload.Length).CopyTo(packet, 16);
        // Session ID
        BitConverter.GetBytes(_sessionId).CopyTo(packet, 24);
        // Copy JSON payload
        Array.Copy(payload, 0, packet, 32, payload.Length);

        return packet;
    }

    #endregion

    #region DHAV Video Streaming

    private async Task StreamDhavFramesAsync(NetworkStream stream, SemaphoreSlim writeLock, byte channel, CancellationToken ct)
    {
        _log.Information("Dahua: DHAV streaming started (background) for channel {Channel}, ffmpegReady={Ready}, ffmpegExited={Exited}, pendingNalus={Count}",
            channel, _ffmpegReady, _ffmpegProcess?.HasExited ?? true, _pendingNalus.Count);

        // Frame pacing - send frames at configured rate
        var frameInterval = TimeSpan.FromMilliseconds(1000.0 / _config.FrameRate);
        var lastFrameTime = DateTime.UtcNow;

        try
        {
            int emptyLoops = 0;
            while (!ct.IsCancellationRequested && stream.CanWrite)
            {
                // Check FFmpeg health
                if (_ffmpegProcess == null || _ffmpegProcess.HasExited)
                {
                    _log.Warning("Dahua: FFmpeg process is not running during streaming, restarting...");
                    if (!await StartFfmpegAsync())
                    {
                        _log.Error("Dahua: Failed to restart FFmpeg, aborting streaming");
                        break;
                    }
                    await Task.Delay(1000, ct); // Wait for FFmpeg to produce output
                    continue;
                }

                // Collect pending NAL units
                List<byte[]> nalus;
                lock (_naluLock)
                {
                    if (_pendingNalus.Count == 0)
                    {
                        // No data yet, wait
                        Monitor.Wait(_naluLock, 500);
                        if (_pendingNalus.Count == 0)
                        {
                            emptyLoops++;
                            if (emptyLoops % 10 == 1)
                                _log.Information("Dahua: Waiting for NAL units... (empty loops={Count}, ffmpegExited={Exited})",
                                    emptyLoops, _ffmpegProcess?.HasExited ?? true);
                            continue;
                        }
                    }
                    nalus = new List<byte[]>(_pendingNalus);
                    _pendingNalus.Clear();
                    emptyLoops = 0;
                }

                // Group NALUs into a single frame
                // Concatenate all NALUs with Annex B start codes
                int totalSize = 0;
                bool isKeyframe = false;
                foreach (var nalu in nalus)
                {
                    totalSize += 4 + nalu.Length; // 4-byte start code + NALU
                    var naluType = nalu[0] & 0x1F;
                    if (naluType == 5 || naluType == 7) isKeyframe = true;
                }

                var frameData = new byte[totalSize];
                int offset = 0;
                foreach (var nalu in nalus)
                {
                    // Annex B start code
                    frameData[offset++] = 0x00;
                    frameData[offset++] = 0x00;
                    frameData[offset++] = 0x00;
                    frameData[offset++] = 0x01;
                    Array.Copy(nalu, 0, frameData, offset, nalu.Length);
                    offset += nalu.Length;
                }

                // Build DHAV packet
                var dhavPacket = BuildDhavFrame(frameData, isKeyframe, channel);

                try
                {
                    await writeLock.WaitAsync(ct);
                    try
                    {
                        await stream.WriteAsync(dhavPacket, ct);
                        await stream.FlushAsync(ct);
                    }
                    finally
                    {
                        writeLock.Release();
                    }

                    if (_frameNumber <= 3 || _frameNumber % 30 == 0)
                        _log.Information("Dahua: Sent DHAV frame #{N}, size={Size}, keyframe={Key}, nalus={NaluCount}",
                            _frameNumber, dhavPacket.Length, isKeyframe, nalus.Count);

                    // Frame pacing - wait until next frame time
                    var now = DateTime.UtcNow;
                    var nextFrameTime = lastFrameTime + frameInterval;
                    var waitTime = nextFrameTime - now;
                    if (waitTime > TimeSpan.Zero)
                    {
                        await Task.Delay(waitTime, ct);
                    }
                    lastFrameTime = DateTime.UtcNow;
                }
                catch (IOException)
                {
                    _log.Information("Dahua: Client disconnected during streaming");
                    break;
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _log.Error(ex, "Dahua: DHAV streaming error");
        }

        _log.Information("Dahua: DHAV streaming stopped");
    }

    private byte[] BuildDhavFrame(byte[] h264Data, bool isKeyframe, byte channel)
    {
        _frameNumber++;

        // Extension tags
        // Tag 0x82: width/height (6 bytes: tag(1) + len(1) + width(2) + height(2))
        // Tag 0x81: codec + framerate (6 bytes: tag(1) + len(1) + codec(1) + framerate(1) + reserved(2))
        var extensionLen = 12; // 6 + 6

        // Total size = header(24) + extensions + data + footer(4)
        var totalSize = 24 + extensionLen + h264Data.Length + 4;
        var packet = new byte[totalSize];
        int pos = 0;

        // Header
        // Magic "DHAV"
        packet[pos++] = 0x44; // D
        packet[pos++] = 0x48; // H
        packet[pos++] = 0x41; // A
        packet[pos++] = 0x56; // V

        // Frame type
        packet[pos++] = isKeyframe ? (byte)0xFD : (byte)0xFC;
        // Subtype
        packet[pos++] = 0x00;
        // Channel (0-indexed in DHAV frame)
        packet[pos++] = channel;
        // Frame sub-number
        packet[pos++] = 0x00;

        // Frame number (LE uint32)
        BitConverter.GetBytes(_frameNumber).CopyTo(packet, pos);
        pos += 4;

        // Frame data length (extensions + h264 data, NOT including header/footer)
        var dataLen = extensionLen + h264Data.Length;
        BitConverter.GetBytes(dataLen).CopyTo(packet, pos);
        pos += 4;

        // Timestamp (packed date)
        var now = DateTime.Now;
        uint timestamp = PackDahuaTimestamp(now);
        BitConverter.GetBytes(timestamp).CopyTo(packet, pos);
        pos += 4;

        // Milliseconds
        BitConverter.GetBytes((ushort)now.Millisecond).CopyTo(packet, pos);
        pos += 2;

        // Extension data length (in bytes)
        packet[pos++] = (byte)extensionLen;

        // Checksum (simple sum of header bytes mod 256)
        byte checksum = 0;
        for (int i = 0; i < pos; i++) checksum += packet[i];
        packet[pos++] = checksum;

        // Extension tag 0x82: Resolution (width/height in 16-bit LE)
        packet[pos++] = 0x82;
        packet[pos++] = 0x04; // payload size
        BitConverter.GetBytes((ushort)_config.VideoWidth).CopyTo(packet, pos);
        pos += 2;
        BitConverter.GetBytes((ushort)_config.VideoHeight).CopyTo(packet, pos);
        pos += 2;

        // Extension tag 0x81: Codec + framerate
        packet[pos++] = 0x81;
        packet[pos++] = 0x04; // payload size
        packet[pos++] = 0x02; // H.264
        packet[pos++] = (byte)_config.FrameRate;
        packet[pos++] = 0x00; // reserved
        packet[pos++] = 0x00; // reserved

        // H.264 data
        Array.Copy(h264Data, 0, packet, pos, h264Data.Length);
        pos += h264Data.Length;

        // Footer "dhav"
        packet[pos++] = 0x64; // d
        packet[pos++] = 0x68; // h
        packet[pos++] = 0x61; // a
        packet[pos++] = 0x76; // v

        return packet;
    }

    private static uint PackDahuaTimestamp(DateTime dt)
    {
        // Packed bitfield:
        // Bits 0-5: Seconds
        // Bits 6-11: Minutes
        // Bits 12-16: Hours
        // Bits 17-21: Day
        // Bits 22-25: Month
        // Bits 26-31: Year (offset from 2000)
        uint packed = 0;
        packed |= (uint)(dt.Second & 0x3F);
        packed |= (uint)((dt.Minute & 0x3F) << 6);
        packed |= (uint)((dt.Hour & 0x1F) << 12);
        packed |= (uint)((dt.Day & 0x1F) << 17);
        packed |= (uint)((dt.Month & 0x0F) << 22);
        packed |= (uint)(((dt.Year - 2000) & 0x3F) << 26);
        return packed;
    }

    #endregion

    #region FFmpeg H.264 Pipeline

    private async Task<bool> StartFfmpegAsync()
    {
        try
        {
            var ffmpegPath = FindFfmpeg();
            if (ffmpegPath == null)
            {
                _log.Warning("FFmpeg not found for Dahua server");
                return false;
            }
            _log.Information("Dahua: Found FFmpeg at {Path}", ffmpegPath);

            // Use lavfi input to generate frames internally (avoids stdin pipe deadlock)
            // FFmpeg generates black frames with text overlay showing weight
            var weightText = "SCALE";
            lock (_readingLock)
            {
                if (_currentReading != null)
                    weightText = $"{_currentReading.Weight:F0} {_currentReading.Unit ?? "lb"}";
            }

            // Use drawtext filter for weight display - FFmpeg handles both generation and encoding
            var args = $"-re -f lavfi -i color=black:s={_config.VideoWidth}x{_config.VideoHeight}:r={_config.FrameRate}:d=86400 " +
                       $"-vf \"drawtext=text='{weightText}':fontsize=72:fontcolor=red:x=(w-tw)/2:y=(h-th)/2," +
                       $"drawtext=text='Cloud-Scale':fontsize=24:fontcolor=gray:x=20:y=h-40\" " +
                       $"-c:v libx264 -preset ultrafast -tune zerolatency -profile:v baseline -level 3.1 " +
                       $"-b:v 1024k -maxrate 1024k -bufsize 1024k -g 1 -keyint_min 1 -intra " +
                       $"-pix_fmt yuv420p -f h264 pipe:1";

            _log.Information("Dahua FFmpeg: {Args}", args);

            _ffmpegProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardInput = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                }
            };

            _ffmpegProcess.ErrorDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    _log.Debug("Dahua FFmpeg: {Line}", e.Data);
            };

            _ffmpegProcess.Start();
            _ffmpegProcess.BeginErrorReadLine();

            // Start reading H.264 output
            _h264ReaderTask = Task.Run(() => ReadH264OutputAsync(_cts!.Token));

            // Wait for first NALUs to be produced
            _log.Information("Dahua: Waiting for FFmpeg to produce H.264 output...");
            for (int i = 0; i < 20; i++)
            {
                await Task.Delay(250);
                lock (_naluLock)
                {
                    if (_pendingNalus.Count > 0)
                    {
                        _log.Information("Dahua: FFmpeg producing output, {Count} NALUs ready", _pendingNalus.Count);
                        break;
                    }
                }
            }

            _ffmpegReady = true;
            _log.Information("Dahua: FFmpeg encoder ready");
            return true;
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Dahua: Failed to start FFmpeg");
            return false;
        }
    }

    /// <summary>
    /// Restart FFmpeg with updated weight text overlay.
    /// </summary>
    private async Task RestartFfmpegWithTextAsync(string text)
    {
        StopFfmpeg();
        await Task.Delay(500);

        var ffmpegPath = FindFfmpeg();
        if (ffmpegPath == null) return;

        // Escape single quotes for FFmpeg filter
        var safeText = text.Replace("'", "'\\''").Replace(":", "\\:");

        var args = $"-re -f lavfi -i color=black:s={_config.VideoWidth}x{_config.VideoHeight}:r={_config.FrameRate}:d=86400 " +
                   $"-vf \"drawtext=text='{safeText}':fontsize=72:fontcolor=red:x=(w-tw)/2:y=(h-th)/2," +
                   $"drawtext=text='Cloud-Scale':fontsize=24:fontcolor=gray:x=20:y=h-40\" " +
                   $"-c:v libx264 -preset ultrafast -tune zerolatency -profile:v baseline -level 3.1 " +
                   $"-b:v 1024k -maxrate 1024k -bufsize 1024k -g 1 -keyint_min 1 -intra " +
                   $"-pix_fmt yuv420p -f h264 pipe:1";

        _ffmpegProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardInput = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            }
        };

        _ffmpegProcess.ErrorDataReceived += (s, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                _log.Debug("Dahua FFmpeg: {Line}", e.Data);
        };

        _ffmpegProcess.Start();
        _ffmpegProcess.BeginErrorReadLine();
        _h264ReaderTask = Task.Run(() => ReadH264OutputAsync(_cts!.Token));

        // Wait for output
        for (int i = 0; i < 10; i++)
        {
            await Task.Delay(250);
            lock (_naluLock)
            {
                if (_pendingNalus.Count > 0) break;
            }
        }

        _ffmpegReady = true;
    }

    private async Task ReadH264OutputAsync(CancellationToken ct)
    {
        var buffer = new byte[512 * 1024];
        var annexBStream = new MemoryStream();
        long totalBytesRead = 0;
        int totalNalusProduced = 0;

        _log.Information("Dahua: H.264 reader started");

        try
        {
            var stdout = _ffmpegProcess!.StandardOutput.BaseStream;
            while (!ct.IsCancellationRequested)
            {
                int bytesRead;
                try { bytesRead = await stdout.ReadAsync(buffer, 0, buffer.Length, ct); }
                catch (Exception ex)
                {
                    _log.Warning(ex, "Dahua: H.264 reader read error");
                    break;
                }
                if (bytesRead == 0)
                {
                    _log.Warning("Dahua: H.264 reader got EOF (FFmpeg stdout closed), totalBytesRead={Total}", totalBytesRead);
                    break;
                }

                totalBytesRead += bytesRead;
                annexBStream.Write(buffer, 0, bytesRead);
                var data = annexBStream.ToArray();

                // Parse NAL units from Annex B stream
                var nalus = ParseNalUnits(data, out int consumed);

                if (consumed > 0)
                {
                    // Keep unconsumed data
                    var remaining = new byte[data.Length - consumed];
                    Array.Copy(data, consumed, remaining, 0, remaining.Length);
                    annexBStream = new MemoryStream();
                    annexBStream.Write(remaining, 0, remaining.Length);
                }

                if (nalus.Count > 0)
                {
                    totalNalusProduced += nalus.Count;
                    if (totalNalusProduced <= 5 || totalNalusProduced % 50 == 0)
                        _log.Information("Dahua: H.264 reader produced {Count} NALUs (total={Total}, bytesRead={Bytes})",
                            nalus.Count, totalNalusProduced, totalBytesRead);

                    lock (_naluLock)
                    {
                        _pendingNalus.AddRange(nalus);
                        Monitor.PulseAll(_naluLock);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Dahua: H.264 reader error");
        }

        _log.Information("Dahua: H.264 reader stopped, totalBytesRead={Bytes}, totalNalus={Nalus}",
            totalBytesRead, totalNalusProduced);
    }

    private static List<byte[]> ParseNalUnits(byte[] data, out int consumed)
    {
        var nalus = new List<byte[]>();
        var startPositions = new List<(int pos, int scLen)>();
        consumed = 0;

        // Find all start codes
        for (int i = 0; i < data.Length - 3; i++)
        {
            if (data[i] == 0 && data[i + 1] == 0)
            {
                if (i + 3 < data.Length && data[i + 2] == 0 && data[i + 3] == 1)
                {
                    startPositions.Add((i, 4));
                    i += 3;
                }
                else if (data[i + 2] == 1)
                {
                    startPositions.Add((i, 3));
                    i += 2;
                }
            }
        }

        // Extract NAL units between start codes
        for (int i = 0; i < startPositions.Count - 1; i++)
        {
            var naluStart = startPositions[i].pos + startPositions[i].scLen;
            var naluEnd = startPositions[i + 1].pos;
            if (naluEnd > naluStart)
            {
                var nalu = new byte[naluEnd - naluStart];
                Array.Copy(data, naluStart, nalu, 0, nalu.Length);
                nalus.Add(nalu);
                consumed = naluEnd;
            }
        }

        return nalus;
    }

    private async Task GenerateFramesAsync(CancellationToken ct)
    {
        var frameInterval = TimeSpan.FromMilliseconds(1000.0 / _config.FrameRate);
        int frameCount = 0;

        _log.Information("Dahua: Frame generation loop started, interval={Interval}ms", frameInterval.TotalMilliseconds);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (_ffmpegProcess == null || _ffmpegProcess.HasExited)
                {
                    _log.Warning("Dahua: FFmpeg exited in frame generation loop, stopping");
                    break;
                }

                var rawFrame = GenerateRawFrame();
                await _ffmpegProcess.StandardInput.BaseStream.WriteAsync(rawFrame, 0, rawFrame.Length, ct);
                await _ffmpegProcess.StandardInput.BaseStream.FlushAsync(ct);
                frameCount++;

                if (frameCount <= 3 || frameCount % 100 == 0)
                    _log.Information("Dahua: Fed raw frame #{Count} to FFmpeg ({Size} bytes)",
                        frameCount, rawFrame.Length);

                await Task.Delay(frameInterval, ct);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _log.Error(ex, "Dahua: Frame generation error at frame {Count}", frameCount);
                await Task.Delay(1000, ct);
            }
        }

        _log.Information("Dahua: Frame generation loop stopped after {Count} frames", frameCount);
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
            }
        }
        catch { }
        _ffmpegProcess = null;
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
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrEmpty(localAppData))
            searchRoots.Add(Path.Combine(localAppData, @"Microsoft\WinGet\Packages"));

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
                    return output.Split('\n')[0].Trim();
            }
        }
        catch { }

        return null;
    }

    #endregion

    #region Frame Generation (reused from WeightRtspServer pattern)

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

    private byte[] GenerateRawFrame()
    {
        EnsureRenderingResources();

        _frameGraphics!.Clear(Color.Black);
        DrawWeightContent(_frameGraphics);

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

    #endregion

    #region Helpers

    private static string? TryExtractJson(byte[] data, int length)
    {
        // Try to find JSON anywhere in the data
        for (int i = 0; i < length; i++)
        {
            if (data[i] == '{')
            {
                try
                {
                    var str = Encoding.UTF8.GetString(data, i, length - i).TrimEnd('\0');
                    JsonNode.Parse(str); // validate
                    return str;
                }
                catch { }
            }
        }
        return null;
    }

    #endregion

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
}
