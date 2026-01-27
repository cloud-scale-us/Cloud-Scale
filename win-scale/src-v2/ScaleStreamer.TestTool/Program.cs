using ScaleStreamer.Common.Models;
using ScaleStreamer.Common.Protocols;
using System.Text;
using System.Text.Json;

namespace ScaleStreamer.TestTool;

/// <summary>
/// Standalone tool to test scale TCP connections and view raw data
/// </summary>
class Program
{
    static async Task<int> Main(string[] args)
    {
        Console.WriteLine("═══════════════════════════════════════════════════════");
        Console.WriteLine("  Scale Connection Test Tool v5.1.1");
        Console.WriteLine("  Cloud-Scale - https://cloud-scale.us");
        Console.WriteLine("═══════════════════════════════════════════════════════\n");

        if (args.Length == 0 || args.Contains("--help") || args.Contains("-h"))
        {
            ShowHelp();
            return 0;
        }

        // Parse arguments
        string? host = null;
        int port = 5001;
        int timeout = 10;
        string? protocolFile = null;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--host":
                case "-h":
                    if (i + 1 < args.Length) host = args[++i];
                    break;
                case "--port":
                case "-p":
                    if (i + 1 < args.Length) int.TryParse(args[++i], out port);
                    break;
                case "--timeout":
                case "-t":
                    if (i + 1 < args.Length) int.TryParse(args[++i], out timeout);
                    break;
                case "--protocol":
                    if (i + 1 < args.Length) protocolFile = args[++i];
                    break;
                default:
                    if (!args[i].StartsWith("-"))
                    {
                        // Assume first positional arg is host
                        if (host == null) host = args[i];
                    }
                    break;
            }
        }

        if (string.IsNullOrEmpty(host))
        {
            Console.WriteLine("❌ Error: Host is required");
            Console.WriteLine("\nUse --help for usage information\n");
            return 1;
        }

        // Load protocol if specified
        ProtocolDefinition? protocol = null;
        if (!string.IsNullOrEmpty(protocolFile))
        {
            try
            {
                var json = await File.ReadAllTextAsync(protocolFile);
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Skip
                };
                protocol = JsonSerializer.Deserialize<ProtocolDefinition>(json, options);
                Console.WriteLine($"📄 Loaded protocol: {protocol?.ProtocolName ?? "Unknown"}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️  Warning: Failed to load protocol file: {ex.Message}");
                Console.WriteLine("   Continuing with raw TCP test...\n");
            }
        }

        // Test connection
        Console.WriteLine($"🔌 Testing connection to {host}:{port}");
        Console.WriteLine($"⏱️  Timeout: {timeout} seconds");
        Console.WriteLine($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n");

        if (protocol != null)
        {
            return await TestWithProtocol(host, port, timeout, protocol);
        }
        else
        {
            return await TestRawTcp(host, port, timeout);
        }
    }

    static async Task<int> TestRawTcp(string host, int port, int timeoutSeconds)
    {
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        var client = new System.Net.Sockets.TcpClient();

        try
        {
            // Connect
            Console.Write($"[{DateTime.Now:HH:mm:ss}] Connecting... ");
            await client.ConnectAsync(host, port, cts.Token);
            Console.WriteLine("✅ Connected!");

            // Read data
            var stream = client.GetStream();
            var buffer = new byte[4096];
            var totalBytes = 0;
            var lineCount = 0;
            var startTime = DateTime.Now;

            Console.WriteLine($"\n📡 Reading data (Press Ctrl+C to stop)...\n");

            var readCts = new CancellationTokenSource();
            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                readCts.Cancel();
            };

            while (!readCts.Token.IsCancellationRequested)
            {
                stream.ReadTimeout = 1000; // 1 second chunks
                try
                {
                    var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, readCts.Token);

                    if (bytesRead == 0)
                    {
                        Console.WriteLine($"\n[{DateTime.Now:HH:mm:ss}] Connection closed by server");
                        break;
                    }

                    totalBytes += bytesRead;
                    lineCount++;

                    // Show timestamp
                    Console.Write($"[{DateTime.Now:HH:mm:ss.fff}] ");

                    // Show byte count
                    Console.Write($"{bytesRead,4} bytes | ");

                    // Show HEX dump (first 32 bytes)
                    var hexLen = Math.Min(bytesRead, 32);
                    var hexDump = string.Join(" ", buffer.Take(hexLen).Select(b => $"{b:X2}"));
                    Console.Write($"HEX: {hexDump,-96} | ");

                    // Show ASCII (replace non-printable)
                    var ascii = Encoding.ASCII.GetString(buffer, 0, bytesRead)
                        .Replace("\r", "\\r")
                        .Replace("\n", "\\n")
                        .Replace("\t", "\\t");
                    var asciiPreview = ascii.Length > 50 ? ascii.Substring(0, 50) + "..." : ascii;
                    Console.WriteLine($"ASCII: {asciiPreview}");
                }
                catch (System.IO.IOException)
                {
                    // Timeout, continue
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            var duration = DateTime.Now - startTime;
            Console.WriteLine($"\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Console.WriteLine($"📊 Test Complete");
            Console.WriteLine($"   Duration: {duration.TotalSeconds:F1} seconds");
            Console.WriteLine($"   Total bytes: {totalBytes}");
            Console.WriteLine($"   Chunks received: {lineCount}");
            if (duration.TotalSeconds > 0)
            {
                Console.WriteLine($"   Avg rate: {totalBytes / duration.TotalSeconds:F1} bytes/sec");
            }
            Console.WriteLine();

            return totalBytes > 0 ? 0 : 1;
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine($"❌ Connection timeout after {timeoutSeconds} seconds");
            return 1;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
            return 1;
        }
        finally
        {
            client?.Close();
        }
    }

    static async Task<int> TestWithProtocol(string host, int port, int timeoutSeconds, ProtocolDefinition protocol)
    {
        Console.WriteLine($"🔧 Using protocol: {protocol.ProtocolName}");
        Console.WriteLine($"   Manufacturer: {protocol.Manufacturer}");
        Console.WriteLine($"   Mode: {protocol.Mode}");
        Console.WriteLine($"   Delimiter: {protocol.Parsing?.LineDelimiter?.Replace("\r", "\\r").Replace("\n", "\\n") ?? "\\r\\n"}");
        Console.WriteLine();

        var adapter = new UniversalProtocolAdapter(protocol);
        var readingCount = 0;

        // Subscribe to events
        adapter.RawDataReceived += (sender, rawData) =>
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] 📄 Raw: '{rawData}'");
        };

        adapter.WeightReceived += (sender, reading) =>
        {
            readingCount++;
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] ⚖️  Weight: {reading.Weight} {reading.Unit} | Status: {reading.Status}");
        };

        adapter.ErrorOccurred += (sender, error) =>
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] ⚠️  {error.Message}");
        };

        try
        {
            var config = new ConnectionConfig
            {
                Type = ConnectionType.TcpIp,
                Host = host,
                Port = port,
                TimeoutMs = timeoutSeconds * 1000,
                AutoReconnect = false,
                ReconnectIntervalSeconds = 0
            };

            Console.Write($"[{DateTime.Now:HH:mm:ss}] Connecting... ");
            var connected = await adapter.ConnectAsync(config);

            if (!connected)
            {
                Console.WriteLine("❌ Failed to connect");
                return 1;
            }

            Console.WriteLine("✅ Connected!");
            Console.WriteLine($"\n📡 Starting continuous reading (Press Ctrl+C to stop)...\n");

            var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            await adapter.StartContinuousReadingAsync(cts.Token);

            // Wait for cancellation
            try
            {
                await Task.Delay(Timeout.Infinite, cts.Token);
            }
            catch (OperationCanceledException) { }

            await adapter.StopContinuousReadingAsync();
            await adapter.DisconnectAsync();

            Console.WriteLine($"\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Console.WriteLine($"📊 Test Complete");
            Console.WriteLine($"   Weight readings: {readingCount}");
            Console.WriteLine();

            return readingCount > 0 ? 0 : 1;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
            return 1;
        }
    }

    static void ShowHelp()
    {
        Console.WriteLine("USAGE:");
        Console.WriteLine("  ScaleStreamer.TestTool.exe <host> [options]");
        Console.WriteLine();
        Console.WriteLine("ARGUMENTS:");
        Console.WriteLine("  <host>              IP address or hostname of scale");
        Console.WriteLine();
        Console.WriteLine("OPTIONS:");
        Console.WriteLine("  --port, -p <port>   TCP port (default: 5001)");
        Console.WriteLine("  --timeout, -t <sec> Timeout in seconds (default: 10)");
        Console.WriteLine("  --protocol <file>   Path to protocol JSON file (optional)");
        Console.WriteLine("  --help, -h          Show this help");
        Console.WriteLine();
        Console.WriteLine("EXAMPLES:");
        Console.WriteLine("  # Raw TCP test");
        Console.WriteLine("  ScaleStreamer.TestTool.exe 10.1.10.210");
        Console.WriteLine();
        Console.WriteLine("  # Specify port");
        Console.WriteLine("  ScaleStreamer.TestTool.exe 10.1.10.210 --port 5001");
        Console.WriteLine();
        Console.WriteLine("  # Test with protocol parsing");
        Console.WriteLine("  ScaleStreamer.TestTool.exe 10.1.10.210 --protocol fairbanks-6011.json");
        Console.WriteLine();
        Console.WriteLine("  # Longer timeout");
        Console.WriteLine("  ScaleStreamer.TestTool.exe 10.1.10.210 --timeout 30");
        Console.WriteLine();
        Console.WriteLine("OUTPUT:");
        Console.WriteLine("  Without --protocol: Shows raw HEX and ASCII data");
        Console.WriteLine("  With --protocol:    Shows parsed weight readings");
        Console.WriteLine();
    }
}
