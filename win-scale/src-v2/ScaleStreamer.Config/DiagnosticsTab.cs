using ScaleStreamer.Common.IPC;
using ScaleStreamer.Common.Settings;
using System.IO.Ports;
using System.Text;

namespace ScaleStreamer.Config;

/// <summary>
/// RS232 Diagnostics tab - direct serial port monitor, test, and debug tool
/// </summary>
public partial class DiagnosticsTab : UserControl
{
    private readonly IpcClient _ipcClient;

    // Serial port controls
    private ComboBox _comPortCombo;
    private ComboBox _baudRateCombo;
    private ComboBox _dataBitsCombo;
    private ComboBox _parityCombo;
    private ComboBox _stopBitsCombo;
    private Button _openPortButton;
    private Button _closePortButton;
    private Button _refreshPortsButton;
    private Button _sendButton;
    private TextBox _sendText;
    private Label _portStatusLabel;

    // Display controls
    private TextBox _rawDataText;
    private TextBox _parsedDataText;
    private TextBox _hexDataText;
    private CheckBox _autoScrollCheck;
    private CheckBox _timestampCheck;
    private CheckBox _showHexCheck;
    private Button _clearButton;
    private Button _exportButton;

    // Statistics
    private Label _bytesReceivedLabel;
    private Label _linesReceivedLabel;
    private Label _dataRateLabel;
    private Label _lastDataLabel;

    private SerialPort? _serialPort;
    private int _bytesReceived;
    private int _linesReceived;
    private DateTime _startTime = DateTime.Now;
    private DateTime _lastDataTime = DateTime.MinValue;
    private StringBuilder _lineBuffer = new();

    public DiagnosticsTab(IpcClient ipcClient)
    {
        _ipcClient = ipcClient;
        InitializeComponent();
        _ipcClient.MessageReceived += OnIpcMessage;
        RefreshPorts();
        LoadSettingsDefaults();
    }

    private void InitializeComponent()
    {
        this.Padding = new Padding(10);

        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1
        };
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 140F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        // Top panel: Serial port config + controls
        mainLayout.Controls.Add(CreateSerialConfigPanel(), 0, 0);

        // Bottom: Tabbed data views
        var dataTabControl = new TabControl
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 9F)
        };

        // Raw ASCII Data
        var rawPage = new TabPage("Raw Serial Data") { UseVisualStyleBackColor = true };
        _rawDataText = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ScrollBars = ScrollBars.Both,
            ReadOnly = true,
            Font = new Font("Consolas", 10F),
            BackColor = Color.Black,
            ForeColor = Color.LimeGreen,
            WordWrap = false
        };
        rawPage.Controls.Add(_rawDataText);
        dataTabControl.TabPages.Add(rawPage);

        // Parsed Data
        var parsedPage = new TabPage("Parsed Readings") { UseVisualStyleBackColor = true };
        _parsedDataText = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ScrollBars = ScrollBars.Both,
            ReadOnly = true,
            Font = new Font("Consolas", 10F),
            BackColor = Color.FromArgb(20, 20, 40),
            ForeColor = Color.Cyan,
            WordWrap = false
        };
        parsedPage.Controls.Add(_parsedDataText);
        dataTabControl.TabPages.Add(parsedPage);

        // Hex View
        var hexPage = new TabPage("Hex View") { UseVisualStyleBackColor = true };
        _hexDataText = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ScrollBars = ScrollBars.Both,
            ReadOnly = true,
            Font = new Font("Consolas", 10F),
            BackColor = Color.FromArgb(30, 30, 30),
            ForeColor = Color.Yellow,
            WordWrap = false
        };
        hexPage.Controls.Add(_hexDataText);
        dataTabControl.TabPages.Add(hexPage);

        mainLayout.Controls.Add(dataTabControl, 0, 1);
        this.Controls.Add(mainLayout);
    }

    private Control CreateSerialConfigPanel()
    {
        var outerPanel = new Panel { Dock = DockStyle.Fill };

        var leftGroup = new GroupBox
        {
            Text = "RS232 Port Configuration",
            Location = new Point(0, 0),
            Size = new Size(460, 130),
            Padding = new Padding(8)
        };

        var y = 20;
        // Row 1: COM Port + Baud Rate + Refresh
        leftGroup.Controls.Add(new Label { Text = "Port:", Location = new Point(10, y + 3), AutoSize = true });
        _comPortCombo = new ComboBox { Location = new Point(60, y), Width = 80, DropDownStyle = ComboBoxStyle.DropDownList };
        leftGroup.Controls.Add(_comPortCombo);

        leftGroup.Controls.Add(new Label { Text = "Baud:", Location = new Point(150, y + 3), AutoSize = true });
        _baudRateCombo = new ComboBox { Location = new Point(195, y), Width = 80, DropDownStyle = ComboBoxStyle.DropDownList };
        _baudRateCombo.Items.AddRange(new object[] { "1200", "2400", "4800", "9600", "19200", "38400", "57600", "115200" });
        _baudRateCombo.SelectedItem = "9600";
        leftGroup.Controls.Add(_baudRateCombo);

        _refreshPortsButton = new Button { Text = "Refresh", Location = new Point(285, y - 1), Width = 65 };
        _refreshPortsButton.Click += (s, e) => RefreshPorts();
        leftGroup.Controls.Add(_refreshPortsButton);

        _portStatusLabel = new Label
        {
            Text = "Closed",
            Location = new Point(360, y + 3),
            AutoSize = true,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = Color.Gray
        };
        leftGroup.Controls.Add(_portStatusLabel);

        // Row 2: Data Bits, Parity, Stop Bits
        y += 30;
        leftGroup.Controls.Add(new Label { Text = "Data:", Location = new Point(10, y + 3), AutoSize = true });
        _dataBitsCombo = new ComboBox { Location = new Point(60, y), Width = 50, DropDownStyle = ComboBoxStyle.DropDownList };
        _dataBitsCombo.Items.AddRange(new object[] { "7", "8" });
        _dataBitsCombo.SelectedItem = "8";
        leftGroup.Controls.Add(_dataBitsCombo);

        leftGroup.Controls.Add(new Label { Text = "Parity:", Location = new Point(120, y + 3), AutoSize = true });
        _parityCombo = new ComboBox { Location = new Point(170, y), Width = 70, DropDownStyle = ComboBoxStyle.DropDownList };
        _parityCombo.Items.AddRange(new object[] { "None", "Odd", "Even" });
        _parityCombo.SelectedItem = "None";
        leftGroup.Controls.Add(_parityCombo);

        leftGroup.Controls.Add(new Label { Text = "Stop:", Location = new Point(250, y + 3), AutoSize = true });
        _stopBitsCombo = new ComboBox { Location = new Point(290, y), Width = 55, DropDownStyle = ComboBoxStyle.DropDownList };
        _stopBitsCombo.Items.AddRange(new object[] { "One", "Two" });
        _stopBitsCombo.SelectedItem = "One";
        leftGroup.Controls.Add(_stopBitsCombo);

        // Row 3: Open/Close buttons + Send
        y += 30;
        _openPortButton = new Button { Text = "Open Port", Location = new Point(10, y), Width = 85, BackColor = Color.FromArgb(40, 167, 69), ForeColor = Color.White };
        _openPortButton.Click += OpenPort_Click;
        leftGroup.Controls.Add(_openPortButton);

        _closePortButton = new Button { Text = "Close Port", Location = new Point(100, y), Width = 85, Enabled = false };
        _closePortButton.Click += ClosePort_Click;
        leftGroup.Controls.Add(_closePortButton);

        leftGroup.Controls.Add(new Label { Text = "Send:", Location = new Point(195, y + 3), AutoSize = true });
        _sendText = new TextBox { Location = new Point(235, y), Width = 120, Text = "" };
        leftGroup.Controls.Add(_sendText);

        _sendButton = new Button { Text = "Send", Location = new Point(360, y), Width = 55, Enabled = false };
        _sendButton.Click += SendData_Click;
        leftGroup.Controls.Add(_sendButton);

        outerPanel.Controls.Add(leftGroup);

        // Right side: Statistics + Controls
        var rightGroup = new GroupBox
        {
            Text = "Statistics & Controls",
            Location = new Point(470, 0),
            Size = new Size(350, 130),
            Padding = new Padding(8)
        };

        var ry = 20;
        _bytesReceivedLabel = new Label { Text = "Bytes: 0", Location = new Point(10, ry), AutoSize = true };
        rightGroup.Controls.Add(_bytesReceivedLabel);
        _linesReceivedLabel = new Label { Text = "Lines: 0", Location = new Point(120, ry), AutoSize = true };
        rightGroup.Controls.Add(_linesReceivedLabel);

        ry += 22;
        _dataRateLabel = new Label { Text = "Rate: 0.0 lines/sec", Location = new Point(10, ry), AutoSize = true };
        rightGroup.Controls.Add(_dataRateLabel);
        _lastDataLabel = new Label { Text = "Last: Never", Location = new Point(170, ry), AutoSize = true };
        rightGroup.Controls.Add(_lastDataLabel);

        ry += 25;
        _autoScrollCheck = new CheckBox { Text = "Auto-scroll", Checked = true, Location = new Point(10, ry), AutoSize = true };
        rightGroup.Controls.Add(_autoScrollCheck);
        _timestampCheck = new CheckBox { Text = "Timestamps", Checked = true, Location = new Point(110, ry), AutoSize = true };
        rightGroup.Controls.Add(_timestampCheck);
        _showHexCheck = new CheckBox { Text = "Log Hex", Checked = true, Location = new Point(220, ry), AutoSize = true };
        rightGroup.Controls.Add(_showHexCheck);

        ry += 25;
        _clearButton = new Button { Text = "Clear", Location = new Point(10, ry), Width = 70 };
        _clearButton.Click += (s, e) => ClearAll();
        rightGroup.Controls.Add(_clearButton);

        _exportButton = new Button { Text = "Export", Location = new Point(90, ry), Width = 70 };
        _exportButton.Click += ExportButton_Click;
        rightGroup.Controls.Add(_exportButton);

        outerPanel.Controls.Add(rightGroup);
        return outerPanel;
    }

    private void LoadSettingsDefaults()
    {
        try
        {
            var settings = AppSettings.Instance.ScaleConnection;
            if (!string.IsNullOrEmpty(settings.ComPort) && _comPortCombo.Items.Contains(settings.ComPort))
                _comPortCombo.SelectedItem = settings.ComPort;
            if (_baudRateCombo.Items.Contains(settings.BaudRate.ToString()))
                _baudRateCombo.SelectedItem = settings.BaudRate.ToString();
            if (_dataBitsCombo.Items.Contains(settings.DataBits.ToString()))
                _dataBitsCombo.SelectedItem = settings.DataBits.ToString();
            if (_parityCombo.Items.Contains(settings.Parity))
                _parityCombo.SelectedItem = settings.Parity;
            if (_stopBitsCombo.Items.Contains(settings.StopBits))
                _stopBitsCombo.SelectedItem = settings.StopBits;
        }
        catch { }
    }

    private void RefreshPorts()
    {
        _comPortCombo.Items.Clear();
        try
        {
            var ports = SerialPort.GetPortNames();
            foreach (var port in ports.OrderBy(p => p))
                _comPortCombo.Items.Add(port);
            if (_comPortCombo.Items.Count > 0)
                _comPortCombo.SelectedIndex = 0;
        }
        catch { }
    }

    private void OpenPort_Click(object? sender, EventArgs e)
    {
        try
        {
            var comPort = _comPortCombo.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(comPort))
            {
                LogRaw("ERROR: No COM port selected.");
                return;
            }

            var baudRate = int.Parse(_baudRateCombo.SelectedItem?.ToString() ?? "9600");
            var dataBits = int.Parse(_dataBitsCombo.SelectedItem?.ToString() ?? "8");
            var parity = Enum.Parse<Parity>(_parityCombo.SelectedItem?.ToString() ?? "None");
            var stopBits = _stopBitsCombo.SelectedItem?.ToString() == "Two" ? StopBits.Two : StopBits.One;

            _serialPort = new SerialPort(comPort, baudRate, parity, dataBits, stopBits);
            _serialPort.ReadTimeout = 1000;
            _serialPort.DataReceived += SerialPort_DataReceived;
            _serialPort.ErrorReceived += SerialPort_ErrorReceived;
            _serialPort.Open();

            _portStatusLabel.Text = $"Open: {comPort}";
            _portStatusLabel.ForeColor = Color.Green;
            _openPortButton.Enabled = false;
            _closePortButton.Enabled = true;
            _sendButton.Enabled = true;

            _bytesReceived = 0;
            _linesReceived = 0;
            _startTime = DateTime.Now;

            LogRaw($"=== Port {comPort} opened: {baudRate} baud, {dataBits}{parity.ToString()[0]}{(stopBits == StopBits.Two ? "2" : "1")} ===");
            LogParsed("Waiting for serial data...");
        }
        catch (Exception ex)
        {
            _portStatusLabel.Text = "Error";
            _portStatusLabel.ForeColor = Color.Red;
            LogRaw($"ERROR opening port: {ex.Message}");
        }
    }

    private void ClosePort_Click(object? sender, EventArgs e)
    {
        CloseSerialPort();
    }

    private void CloseSerialPort()
    {
        try
        {
            if (_serialPort?.IsOpen == true)
            {
                _serialPort.DataReceived -= SerialPort_DataReceived;
                _serialPort.ErrorReceived -= SerialPort_ErrorReceived;
                _serialPort.Close();
                _serialPort.Dispose();
            }
        }
        catch { }

        _serialPort = null;
        _portStatusLabel.Text = "Closed";
        _portStatusLabel.ForeColor = Color.Gray;
        _openPortButton.Enabled = true;
        _closePortButton.Enabled = false;
        _sendButton.Enabled = false;
        LogRaw("=== Port closed ===");
    }

    private void SendData_Click(object? sender, EventArgs e)
    {
        if (_serialPort?.IsOpen != true) return;
        try
        {
            var text = _sendText.Text;
            // Replace escape sequences
            text = text.Replace("\\r", "\r").Replace("\\n", "\n");
            _serialPort.Write(text);
            LogRaw($"SENT: {_sendText.Text}");
        }
        catch (Exception ex)
        {
            LogRaw($"SEND ERROR: {ex.Message}");
        }
    }

    private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        try
        {
            if (_serialPort == null || !_serialPort.IsOpen) return;

            var bytes = new byte[_serialPort.BytesToRead];
            var count = _serialPort.Read(bytes, 0, bytes.Length);
            if (count == 0) return;

            _bytesReceived += count;
            _lastDataTime = DateTime.Now;

            var text = Encoding.ASCII.GetString(bytes, 0, count);
            var hex = BitConverter.ToString(bytes, 0, count).Replace("-", " ");

            Invoke(() =>
            {
                var ts = _timestampCheck.Checked ? $"[{DateTime.Now:HH:mm:ss.fff}] " : "";

                // Raw data display
                LogRaw($"{ts}{text.Replace("\r", "\\r").Replace("\n", "\\n")}");

                // Hex display
                if (_showHexCheck.Checked)
                    LogHex($"{ts}{hex}  |  {text.Replace("\r", "<CR>").Replace("\n", "<LF>")}");

                // Line buffering for parsed data
                _lineBuffer.Append(text);
                var bufferStr = _lineBuffer.ToString();
                int idx;
                while ((idx = bufferStr.IndexOfAny(new[] { '\r', '\n' })) >= 0)
                {
                    var line = bufferStr.Substring(0, idx).Trim();
                    // Skip past delimiter(s)
                    if (idx + 1 < bufferStr.Length && bufferStr[idx] == '\r' && bufferStr[idx + 1] == '\n')
                        bufferStr = bufferStr.Substring(idx + 2);
                    else
                        bufferStr = bufferStr.Substring(idx + 1);

                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        _linesReceived++;
                        LogParsed($"{ts}LINE {_linesReceived}: {line}");
                    }
                }
                _lineBuffer.Clear();
                _lineBuffer.Append(bufferStr);

                UpdateStatistics();
            });
        }
        catch { }
    }

    private void SerialPort_ErrorReceived(object sender, SerialErrorReceivedEventArgs e)
    {
        try
        {
            Invoke(() => LogRaw($"SERIAL ERROR: {e.EventType}"));
        }
        catch { }
    }

    private void OnIpcMessage(object? sender, IpcMessage message)
    {
        if (InvokeRequired)
        {
            Invoke(() => OnIpcMessage(sender, message));
            return;
        }

        var ts = _timestampCheck?.Checked == true ? $"[{DateTime.Now:HH:mm:ss.fff}] " : "";

        switch (message.MessageType)
        {
            case IpcMessageType.WeightReading:
                LogParsed($"{ts}SERVICE WEIGHT: {message.Payload}");
                break;
            case IpcMessageType.RawData:
                LogRaw($"{ts}SERVICE RAW: {message.Payload}");
                break;
            case IpcMessageType.Error:
                LogRaw($"{ts}SERVICE ERROR: {message.Payload}");
                break;
        }
    }

    private void UpdateStatistics()
    {
        _bytesReceivedLabel.Text = $"Bytes: {_bytesReceived:N0}";
        _linesReceivedLabel.Text = $"Lines: {_linesReceived}";
        _lastDataLabel.Text = $"Last: {_lastDataTime:HH:mm:ss.fff}";

        var elapsed = (DateTime.Now - _startTime).TotalSeconds;
        _dataRateLabel.Text = elapsed > 0 ? $"Rate: {_linesReceived / elapsed:F1} lines/sec" : "Rate: 0.0 lines/sec";
    }

    private void LogRaw(string message) => AppendToTextBox(_rawDataText, message);
    private void LogParsed(string message) => AppendToTextBox(_parsedDataText, message);
    private void LogHex(string message) => AppendToTextBox(_hexDataText, message);

    private void AppendToTextBox(TextBox textBox, string message)
    {
        if (textBox.InvokeRequired)
        {
            textBox.Invoke(() => AppendToTextBox(textBox, message));
            return;
        }

        textBox.AppendText(message + Environment.NewLine);

        if (_autoScrollCheck.Checked)
        {
            textBox.SelectionStart = textBox.Text.Length;
            textBox.ScrollToCaret();
        }

        var lines = textBox.Lines;
        if (lines.Length > 2000)
        {
            textBox.Lines = lines.Skip(lines.Length - 2000).ToArray();
        }
    }

    private void ClearAll()
    {
        _rawDataText.Clear();
        _parsedDataText.Clear();
        _hexDataText.Clear();
        _bytesReceived = 0;
        _linesReceived = 0;
        _startTime = DateTime.Now;
        UpdateStatistics();
    }

    private void ExportButton_Click(object? sender, EventArgs e)
    {
        using var dialog = new SaveFileDialog
        {
            Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
            DefaultExt = "txt",
            FileName = $"rs232-diagnostics-{DateTime.Now:yyyyMMdd-HHmmss}.txt"
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("=== Scale Streamer RS232 Diagnostics Export ===");
                sb.AppendLine($"Exported: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"Bytes Received: {_bytesReceived:N0}");
                sb.AppendLine($"Lines Received: {_linesReceived}");
                sb.AppendLine();
                sb.AppendLine("=== RAW SERIAL DATA ===");
                sb.AppendLine(_rawDataText.Text);
                sb.AppendLine();
                sb.AppendLine("=== PARSED READINGS ===");
                sb.AppendLine(_parsedDataText.Text);
                sb.AppendLine();
                sb.AppendLine("=== HEX VIEW ===");
                sb.AppendLine(_hexDataText.Text);

                File.WriteAllText(dialog.FileName, sb.ToString());
                MessageBox.Show($"Exported to:\n{dialog.FileName}", "Export Successful", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    public void LogDebug(string message)
    {
        var ts = _timestampCheck?.Checked == true ? $"[{DateTime.Now:HH:mm:ss.fff}] " : "";
        LogRaw($"{ts}DEBUG: {message}");
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            CloseSerialPort();
        }
        base.Dispose(disposing);
    }
}
