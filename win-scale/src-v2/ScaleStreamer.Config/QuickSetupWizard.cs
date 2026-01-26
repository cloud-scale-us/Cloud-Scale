using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ScaleStreamer.Config;

/// <summary>
/// Quick setup wizard for auto-detecting scale connections and protocols
/// </summary>
public partial class QuickSetupWizard : Form
{
    private TextBox _ipAddressText;
    private NumericUpDown _portNumeric;
    private Button _scanButton;
    private Button _acceptButton;
    private Button _cancelButton;
    private TextBox _logTextBox;
    private Label _statusLabel;
    private ProgressBar _progressBar;
    private GroupBox _resultsGroup;
    private Label _detectedProtocolLabel;
    private TextBox _sampleDataText;

    private string? _detectedProtocol;
    private Dictionary<string, object>? _detectedConfig;

    public string? DetectedProtocol => _detectedProtocol;
    public Dictionary<string, object>? DetectedConfig => _detectedConfig;
    public string? IpAddress => _ipAddressText.Text;
    public int Port => (int)_portNumeric.Value;

    public QuickSetupWizard()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        this.Text = "Quick Setup Wizard";
        this.Size = new Size(700, 650);
        this.StartPosition = FormStartPosition.CenterParent;
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;

        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(15),
            RowCount = 6,
            ColumnCount = 1
        };
        mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        // Header
        var headerLabel = new Label
        {
            Text = "Auto-Detect Scale Configuration",
            Font = new Font("Segoe UI", 14F, FontStyle.Bold),
            AutoSize = true,
            Padding = new Padding(0, 0, 0, 10)
        };
        mainLayout.Controls.Add(headerLabel, 0, 0);

        // Connection Info Panel
        var connectionPanel = CreateConnectionPanel();
        mainLayout.Controls.Add(connectionPanel, 0, 1);

        // Status
        _statusLabel = new Label
        {
            Text = "Enter IP address and port, then click Scan",
            AutoSize = true,
            Font = new Font("Segoe UI", 10F),
            ForeColor = Color.Gray,
            Padding = new Padding(0, 10, 0, 5)
        };
        mainLayout.Controls.Add(_statusLabel, 0, 2);

        // Progress
        _progressBar = new ProgressBar
        {
            Dock = DockStyle.Fill,
            Style = ProgressBarStyle.Continuous,
            Height = 25,
            Visible = false
        };
        mainLayout.Controls.Add(_progressBar, 0, 3);

        // Results Panel
        _resultsGroup = new GroupBox
        {
            Text = "Detection Results",
            Dock = DockStyle.Fill,
            Padding = new Padding(10),
            Visible = false
        };

        var resultsLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            ColumnCount = 1
        };
        resultsLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        resultsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
        resultsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));

        _detectedProtocolLabel = new Label
        {
            Text = "Detected Protocol: Unknown",
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            AutoSize = true,
            Padding = new Padding(0, 0, 0, 10)
        };
        resultsLayout.Controls.Add(_detectedProtocolLabel, 0, 0);

        var sampleLabel = new Label
        {
            Text = "Sample Data:",
            AutoSize = true,
            Padding = new Padding(0, 5, 0, 3)
        };
        resultsLayout.Controls.Add(sampleLabel, 0, 1);

        _sampleDataText = new TextBox
        {
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            ReadOnly = true,
            Font = new Font("Consolas", 9F),
            Dock = DockStyle.Fill
        };
        resultsLayout.Controls.Add(_sampleDataText, 0, 2);

        _resultsGroup.Controls.Add(resultsLayout);
        mainLayout.Controls.Add(_resultsGroup, 0, 4);

        // Log
        var logLabel = new Label
        {
            Text = "Detection Log:",
            AutoSize = true,
            Padding = new Padding(0, 10, 0, 3)
        };

        _logTextBox = new TextBox
        {
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            ReadOnly = true,
            Font = new Font("Consolas", 8F),
            Height = 120,
            Dock = DockStyle.Fill
        };

        var logPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Height = 150
        };
        logPanel.Controls.Add(_logTextBox);
        logPanel.Controls.Add(logLabel);
        _logTextBox.Dock = DockStyle.Fill;
        logLabel.Dock = DockStyle.Top;

        mainLayout.Controls.Add(logPanel, 0, 4);

        // Buttons
        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(0, 10, 0, 0)
        };

        _cancelButton = new Button
        {
            Text = "Cancel",
            Width = 100,
            Height = 35,
            DialogResult = DialogResult.Cancel
        };

        _acceptButton = new Button
        {
            Text = "Use This Config",
            Width = 130,
            Height = 35,
            DialogResult = DialogResult.OK,
            Enabled = false
        };

        _scanButton = new Button
        {
            Text = "Scan",
            Width = 100,
            Height = 35
        };
        _scanButton.Click += Scan_Click;

        buttonPanel.Controls.Add(_cancelButton);
        buttonPanel.Controls.Add(_acceptButton);
        buttonPanel.Controls.Add(_scanButton);

        mainLayout.Controls.Add(buttonPanel, 0, 5);

        this.Controls.Add(mainLayout);
        this.AcceptButton = _scanButton;
        this.CancelButton = _cancelButton;
    }

    private Control CreateConnectionPanel()
    {
        var panel = new GroupBox
        {
            Text = "Scale Connection",
            Dock = DockStyle.Fill,
            Padding = new Padding(10),
            AutoSize = true
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            AutoSize = true
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

        // IP Address
        var ipLabel = new Label
        {
            Text = "IP Address:",
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(0, 5, 0, 5)
        };
        layout.Controls.Add(ipLabel, 0, 0);

        _ipAddressText = new TextBox
        {
            Width = 200,
            Text = "10.1.10.210"
        };
        layout.Controls.Add(_ipAddressText, 1, 0);

        // Port
        var portLabel = new Label
        {
            Text = "Port:",
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(0, 5, 0, 5)
        };
        layout.Controls.Add(portLabel, 0, 1);

        _portNumeric = new NumericUpDown
        {
            Width = 100,
            Minimum = 1,
            Maximum = 65535,
            Value = 5001
        };
        layout.Controls.Add(_portNumeric, 1, 1);

        panel.Controls.Add(layout);
        return panel;
    }

    private async void Scan_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_ipAddressText.Text))
        {
            MessageBox.Show("Please enter an IP address.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _scanButton.Enabled = false;
        _acceptButton.Enabled = false;
        _resultsGroup.Visible = false;
        _progressBar.Visible = true;
        _progressBar.Style = ProgressBarStyle.Marquee;
        _logTextBox.Clear();
        _sampleDataText.Clear();

        try
        {
            await Task.Run(() => PerformScan());
        }
        catch (Exception ex)
        {
            LogMessage($"ERROR: {ex.Message}");
            _statusLabel.Text = "Scan failed";
            _statusLabel.ForeColor = Color.Red;
        }
        finally
        {
            _progressBar.Visible = false;
            _scanButton.Enabled = true;
        }
    }

    private void PerformScan()
    {
        var ipAddress = _ipAddressText.Text;
        var port = (int)_portNumeric.Value;

        UpdateStatus("Checking IP connectivity...", Color.Orange);
        LogMessage($"Starting scan for {ipAddress}:{port}");

        // Step 1: Test TCP connectivity
        if (!TestTcpConnection(ipAddress, port))
        {
            UpdateStatus("Connection failed - IP or port not reachable", Color.Red);
            return;
        }

        LogMessage("✓ TCP connection successful");

        // Step 2: Read data stream
        UpdateStatus("Reading data stream...", Color.Orange);
        var dataLines = ReadDataStream(ipAddress, port, timeoutSeconds: 10);

        if (dataLines == null || dataLines.Count == 0)
        {
            UpdateStatus("No data received from scale", Color.Red);
            LogMessage("✗ No data received within 10 seconds");
            return;
        }

        LogMessage($"✓ Received {dataLines.Count} lines of data");

        // Step 3: Analyze and detect protocol
        UpdateStatus("Analyzing data format...", Color.Orange);
        var protocol = AnalyzeDataFormat(dataLines);

        if (protocol == null)
        {
            UpdateStatus("Could not auto-detect protocol format", Color.Red);
            LogMessage("✗ Data format does not match known protocols");
            ShowSampleData(dataLines);
            return;
        }

        // Success!
        _detectedProtocol = protocol;
        _detectedConfig = new Dictionary<string, object>
        {
            ["host"] = ipAddress,
            ["port"] = port,
            ["protocol"] = protocol
        };

        UpdateStatus($"✓ Detected: {protocol}", Color.Green);
        LogMessage($"✓ Successfully detected protocol: {protocol}");
        ShowResults(protocol, dataLines);
    }

    private bool TestTcpConnection(string ipAddress, int port)
    {
        try
        {
            using var client = new TcpClient();
            var connectTask = client.ConnectAsync(ipAddress, port);

            if (!connectTask.Wait(TimeSpan.FromSeconds(5)))
            {
                LogMessage($"✗ Connection timeout after 5 seconds");
                return false;
            }

            return client.Connected;
        }
        catch (Exception ex)
        {
            LogMessage($"✗ Connection failed: {ex.Message}");
            return false;
        }
    }

    private List<string>? ReadDataStream(string ipAddress, int port, int timeoutSeconds)
    {
        var lines = new List<string>();
        var startTime = DateTime.Now;

        try
        {
            using var client = new TcpClient();
            client.Connect(ipAddress, port);
            using var stream = client.GetStream();
            stream.ReadTimeout = timeoutSeconds * 1000;

            var buffer = new byte[1024];
            var dataBuilder = new StringBuilder();

            LogMessage($"Reading data for up to {timeoutSeconds} seconds...");

            while ((DateTime.Now - startTime).TotalSeconds < timeoutSeconds)
            {
                if (stream.DataAvailable)
                {
                    int bytesRead = stream.Read(buffer, 0, buffer.Length);
                    if (bytesRead > 0)
                    {
                        var data = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                        dataBuilder.Append(data);

                        // Split by common line delimiters
                        var currentLines = dataBuilder.ToString()
                            .Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);

                        foreach (var line in currentLines)
                        {
                            if (!string.IsNullOrWhiteSpace(line) && !lines.Contains(line))
                            {
                                lines.Add(line);
                                LogMessage($"  << {line}");
                            }
                        }

                        // If we have enough samples, we can stop early
                        if (lines.Count >= 5)
                        {
                            LogMessage($"Collected {lines.Count} samples, stopping early");
                            break;
                        }
                    }
                }
                else
                {
                    Thread.Sleep(100);
                }
            }

            return lines;
        }
        catch (Exception ex)
        {
            LogMessage($"✗ Error reading data: {ex.Message}");
            return null;
        }
    }

    private string? AnalyzeDataFormat(List<string> dataLines)
    {
        if (dataLines.Count == 0)
            return null;

        // Analyze patterns
        var sampleLine = dataLines[0];

        // Test for Fairbanks 6011 format: "STATUS  WEIGHT  TARE"
        // Example: "1     960    00" or "2     965    00"
        if (IsFairbanks6011Format(dataLines))
        {
            LogMessage("Pattern matches: Fairbanks 6011");
            return "Fairbanks 6011";
        }

        // Test for Generic ASCII format
        // Typically: weight value with optional unit
        if (IsGenericAsciiFormat(dataLines))
        {
            LogMessage("Pattern matches: Generic ASCII");
            return "Generic ASCII";
        }

        // Test for Modbus TCP (binary)
        if (IsModbusTcpFormat(dataLines))
        {
            LogMessage("Pattern matches: Modbus TCP");
            return "Modbus TCP";
        }

        LogMessage("No matching protocol pattern found");
        return null;
    }

    private bool IsFairbanks6011Format(List<string> lines)
    {
        // Fairbanks 6011 format: STATUS(1-2 chars) + spaces + WEIGHT(digits) + spaces + TARE(digits)
        // Example: "1     960    00" or "1\"   -500    00" or "2     965    00"

        var pattern = @"^[12]\""?\s+\-?\d+\s+\d+";
        var matchCount = 0;

        foreach (var line in lines.Take(5))
        {
            if (Regex.IsMatch(line.Trim(), pattern))
            {
                matchCount++;
            }
        }

        // If most lines match, it's likely Fairbanks 6011
        return matchCount >= Math.Min(3, lines.Count / 2);
    }

    private bool IsGenericAsciiFormat(List<string> lines)
    {
        // Generic ASCII: Just weight values, possibly with units
        // Example: "1234.5" or "1234.5 lb" or "WT: 1234.5"

        var pattern = @"\d+\.?\d*\s*(lb|kg|g|oz)?";

        var matchCount = 0;
        foreach (var line in lines.Take(5))
        {
            if (Regex.IsMatch(line.Trim(), pattern, RegexOptions.IgnoreCase))
            {
                matchCount++;
            }
        }

        return matchCount >= Math.Min(3, lines.Count / 2);
    }

    private bool IsModbusTcpFormat(List<string> lines)
    {
        // Modbus TCP is binary, so ASCII representation would look odd
        // Check for non-printable characters or hex-like patterns

        foreach (var line in lines.Take(3))
        {
            if (line.Any(c => c < 32 && c != '\r' && c != '\n' && c != '\t'))
            {
                return true;
            }
        }

        return false;
    }

    private void ShowResults(string protocol, List<string> dataLines)
    {
        Invoke(() =>
        {
            _resultsGroup.Visible = true;
            _detectedProtocolLabel.Text = $"Detected Protocol: {protocol}";
            _detectedProtocolLabel.ForeColor = Color.Green;
            _acceptButton.Enabled = true;

            ShowSampleData(dataLines);
        });
    }

    private void ShowSampleData(List<string> dataLines)
    {
        Invoke(() =>
        {
            _sampleDataText.Text = string.Join(Environment.NewLine, dataLines.Take(10));
        });
    }

    private void UpdateStatus(string message, Color color)
    {
        Invoke(() =>
        {
            _statusLabel.Text = message;
            _statusLabel.ForeColor = color;
        });
    }

    private void LogMessage(string message)
    {
        Invoke(() =>
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            _logTextBox.AppendText($"[{timestamp}] {message}\r\n");
        });
    }
}
