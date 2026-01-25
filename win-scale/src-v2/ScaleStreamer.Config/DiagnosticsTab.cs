using ScaleStreamer.Common.IPC;
using System.Text;

namespace ScaleStreamer.Config;

/// <summary>
/// Diagnostics tab showing live TCP data, connection status, and debug information
/// </summary>
public partial class DiagnosticsTab : UserControl
{
    private readonly IpcClient _ipcClient;
    private TextBox _liveDataText;
    private TextBox _connectionLogText;
    private TextBox _ipcLogText;
    private TextBox _errorLogText;
    private Label _connectionStatusLabel;
    private Label _lastDataLabel;
    private Label _dataRateLabel;
    private Label _totalLinesLabel;
    private Button _clearButton;
    private Button _exportButton;
    private CheckBox _autoScrollCheck;
    private CheckBox _timestampCheck;

    private int _lineCount = 0;
    private int _dataCount = 0;
    private DateTime _startTime = DateTime.Now;
    private DateTime _lastDataTime = DateTime.MinValue;

    public DiagnosticsTab(IpcClient ipcClient)
    {
        _ipcClient = ipcClient;
        InitializeComponent();
        _ipcClient.MessageReceived += OnIpcMessage;
    }

    private void InitializeComponent()
    {
        this.Padding = new Padding(10);

        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 2
        };
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

        // Status Panel (top-left)
        mainLayout.Controls.Add(CreateStatusPanel(), 0, 0);

        // Control Panel (top-right)
        mainLayout.Controls.Add(CreateControlPanel(), 1, 0);

        // Create tab control for different log views (bottom, spanning both columns)
        var logTabControl = new TabControl
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 9F)
        };

        // Live TCP Data Tab
        var liveDataPage = new TabPage("Live TCP Data")
        {
            UseVisualStyleBackColor = true
        };
        _liveDataText = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ScrollBars = ScrollBars.Both,
            ReadOnly = true,
            Font = new Font("Consolas", 9F),
            BackColor = Color.Black,
            ForeColor = Color.LimeGreen,
            WordWrap = false
        };
        liveDataPage.Controls.Add(_liveDataText);
        logTabControl.TabPages.Add(liveDataPage);

        // Connection Log Tab
        var connectionLogPage = new TabPage("Connection Log")
        {
            UseVisualStyleBackColor = true
        };
        _connectionLogText = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ScrollBars = ScrollBars.Both,
            ReadOnly = true,
            Font = new Font("Consolas", 9F),
            BackColor = Color.FromArgb(30, 30, 30),
            ForeColor = Color.Cyan
        };
        connectionLogPage.Controls.Add(_connectionLogText);
        logTabControl.TabPages.Add(connectionLogPage);

        // IPC Messages Tab
        var ipcLogPage = new TabPage("IPC Messages")
        {
            UseVisualStyleBackColor = true
        };
        _ipcLogText = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ScrollBars = ScrollBars.Both,
            ReadOnly = true,
            Font = new Font("Consolas", 9F),
            BackColor = Color.FromArgb(20, 20, 40),
            ForeColor = Color.Yellow
        };
        ipcLogPage.Controls.Add(_ipcLogText);
        logTabControl.TabPages.Add(ipcLogPage);

        // Error Log Tab
        var errorLogPage = new TabPage("Errors")
        {
            UseVisualStyleBackColor = true
        };
        _errorLogText = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ScrollBars = ScrollBars.Both,
            ReadOnly = true,
            Font = new Font("Consolas", 9F),
            BackColor = Color.FromArgb(40, 20, 20),
            ForeColor = Color.OrangeRed
        };
        errorLogPage.Controls.Add(_errorLogText);
        logTabControl.TabPages.Add(errorLogPage);

        mainLayout.Controls.Add(logTabControl, 0, 1);
        mainLayout.SetColumnSpan(logTabControl, 2);

        this.Controls.Add(mainLayout);

        // Start logging connection status
        LogConnection("Diagnostics tab initialized. Waiting for scale data...");
    }

    private Control CreateStatusPanel()
    {
        var panel = new GroupBox
        {
            Text = "Connection Status",
            Dock = DockStyle.Fill,
            Padding = new Padding(10)
        };

        var layout = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown
        };

        _connectionStatusLabel = new Label
        {
            Text = "Status: Waiting for connection...",
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            ForeColor = Color.Gray,
            AutoSize = true,
            Padding = new Padding(0, 5, 0, 5)
        };

        _lastDataLabel = new Label
        {
            Text = "Last Data: Never",
            Font = new Font("Segoe UI", 9F),
            AutoSize = true,
            Padding = new Padding(0, 3, 0, 3)
        };

        _dataRateLabel = new Label
        {
            Text = "Data Rate: 0.0 lines/sec",
            Font = new Font("Segoe UI", 9F),
            AutoSize = true,
            Padding = new Padding(0, 3, 0, 3)
        };

        _totalLinesLabel = new Label
        {
            Text = "Total Lines: 0",
            Font = new Font("Segoe UI", 9F),
            AutoSize = true,
            Padding = new Padding(0, 3, 0, 3)
        };

        layout.Controls.Add(_connectionStatusLabel);
        layout.Controls.Add(_lastDataLabel);
        layout.Controls.Add(_dataRateLabel);
        layout.Controls.Add(_totalLinesLabel);

        panel.Controls.Add(layout);
        return panel;
    }

    private Control CreateControlPanel()
    {
        var panel = new GroupBox
        {
            Text = "Controls",
            Dock = DockStyle.Fill,
            Padding = new Padding(10)
        };

        var layout = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown
        };

        _autoScrollCheck = new CheckBox
        {
            Text = "Auto-scroll",
            Checked = true,
            AutoSize = true
        };

        _timestampCheck = new CheckBox
        {
            Text = "Show timestamps",
            Checked = true,
            AutoSize = true
        };

        _clearButton = new Button
        {
            Text = "Clear All Logs",
            Width = 120,
            Margin = new Padding(0, 10, 0, 0)
        };
        _clearButton.Click += (s, e) => ClearLogs();

        _exportButton = new Button
        {
            Text = "Export to File",
            Width = 120,
            Margin = new Padding(0, 5, 0, 0)
        };
        _exportButton.Click += ExportButton_Click;

        layout.Controls.Add(_autoScrollCheck);
        layout.Controls.Add(_timestampCheck);
        layout.Controls.Add(_clearButton);
        layout.Controls.Add(_exportButton);

        panel.Controls.Add(layout);
        return panel;
    }

    private void OnIpcMessage(object? sender, IpcMessage message)
    {
        if (InvokeRequired)
        {
            Invoke(() => OnIpcMessage(sender, message));
            return;
        }

        var timestamp = _timestampCheck.Checked ? $"[{DateTime.Now:HH:mm:ss.fff}] " : "";

        // Log IPC message
        LogIpc($"{timestamp}IPC: {message.MessageType} - {message.Payload?.Substring(0, Math.Min(100, message.Payload?.Length ?? 0))}");

        // Route to appropriate handler
        switch (message.MessageType)
        {
            case IpcMessageType.WeightReading:
                HandleWeightReading(message, timestamp);
                break;

            case IpcMessageType.ConnectionStatus:
                HandleConnectionStatus(message, timestamp);
                break;

            case IpcMessageType.Error:
                HandleError(message, timestamp);
                break;

            case IpcMessageType.RawData:
                HandleRawData(message, timestamp);
                break;
        }
    }

    private void HandleWeightReading(IpcMessage message, string timestamp)
    {
        _dataCount++;
        _lastDataTime = DateTime.Now;
        _lineCount++;

        UpdateStatistics();

        // Log to live data view
        LogLiveData($"{timestamp}WEIGHT: {message.Payload}");

        // Update connection status
        if (_connectionStatusLabel.ForeColor != Color.Green)
        {
            _connectionStatusLabel.Text = "Status: Connected - Receiving Data";
            _connectionStatusLabel.ForeColor = Color.Green;
            LogConnection($"{timestamp}Scale connected and sending weight data");
        }
    }

    private void HandleConnectionStatus(IpcMessage message, string timestamp)
    {
        LogConnection($"{timestamp}CONNECTION: {message.Payload}");

        if (message.Payload?.Contains("Connected") == true)
        {
            _connectionStatusLabel.Text = "Status: Connected";
            _connectionStatusLabel.ForeColor = Color.Blue;
        }
        else if (message.Payload?.Contains("Disconnect") == true)
        {
            _connectionStatusLabel.Text = "Status: Disconnected";
            _connectionStatusLabel.ForeColor = Color.Orange;
        }
        else if (message.Payload?.Contains("Error") == true || message.Payload?.Contains("Failed") == true)
        {
            _connectionStatusLabel.Text = "Status: Connection Error";
            _connectionStatusLabel.ForeColor = Color.Red;
            LogError($"{timestamp}CONNECTION ERROR: {message.Payload}");
        }
    }

    private void HandleError(IpcMessage message, string timestamp)
    {
        LogError($"{timestamp}ERROR: {message.Payload}");
        _connectionStatusLabel.Text = "Status: Error";
        _connectionStatusLabel.ForeColor = Color.Red;
    }

    private void HandleRawData(IpcMessage message, string timestamp)
    {
        _lineCount++;
        _lastDataTime = DateTime.Now;
        UpdateStatistics();

        // Log raw TCP data
        LogLiveData($"{timestamp}RAW: {message.Payload}");
    }

    private void UpdateStatistics()
    {
        _lastDataLabel.Text = $"Last Data: {_lastDataTime:HH:mm:ss.fff}";

        var elapsed = (DateTime.Now - _startTime).TotalSeconds;
        var rate = elapsed > 0 ? _lineCount / elapsed : 0;
        _dataRateLabel.Text = $"Data Rate: {rate:F1} lines/sec";

        _totalLinesLabel.Text = $"Total Lines: {_lineCount}";
    }

    private void LogLiveData(string message)
    {
        AppendToTextBox(_liveDataText, message);
    }

    private void LogConnection(string message)
    {
        AppendToTextBox(_connectionLogText, message);
    }

    private void LogIpc(string message)
    {
        AppendToTextBox(_ipcLogText, message);
    }

    private void LogError(string message)
    {
        AppendToTextBox(_errorLogText, message);
    }

    private void AppendToTextBox(TextBox textBox, string message)
    {
        if (textBox.InvokeRequired)
        {
            textBox.Invoke(() => AppendToTextBox(textBox, message));
            return;
        }

        textBox.AppendText(message + Environment.NewLine);

        // Auto-scroll to bottom if enabled
        if (_autoScrollCheck.Checked)
        {
            textBox.SelectionStart = textBox.Text.Length;
            textBox.ScrollToCaret();
        }

        // Keep only last 1000 lines
        var lines = textBox.Lines;
        if (lines.Length > 1000)
        {
            textBox.Lines = lines.Skip(lines.Length - 1000).ToArray();
        }
    }

    private void ClearLogs()
    {
        _liveDataText.Clear();
        _connectionLogText.Clear();
        _ipcLogText.Clear();
        _errorLogText.Clear();
        _lineCount = 0;
        _dataCount = 0;
        _startTime = DateTime.Now;
        _lastDataTime = DateTime.MinValue;
        UpdateStatistics();
        LogConnection("Logs cleared. Waiting for new data...");
    }

    private void ExportButton_Click(object? sender, EventArgs e)
    {
        using var dialog = new SaveFileDialog
        {
            Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
            DefaultExt = "txt",
            FileName = $"diagnostics-{DateTime.Now:yyyyMMdd-HHmmss}.txt"
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("=== Scale Streamer Diagnostics Export ===");
                sb.AppendLine($"Exported: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"Total Lines: {_lineCount}");
                sb.AppendLine($"Data Rate: {(_lineCount / (DateTime.Now - _startTime).TotalSeconds):F1} lines/sec");
                sb.AppendLine();
                sb.AppendLine("=== LIVE TCP DATA ===");
                sb.AppendLine(_liveDataText.Text);
                sb.AppendLine();
                sb.AppendLine("=== CONNECTION LOG ===");
                sb.AppendLine(_connectionLogText.Text);
                sb.AppendLine();
                sb.AppendLine("=== IPC MESSAGES ===");
                sb.AppendLine(_ipcLogText.Text);
                sb.AppendLine();
                sb.AppendLine("=== ERRORS ===");
                sb.AppendLine(_errorLogText.Text);

                File.WriteAllText(dialog.FileName, sb.ToString());

                MessageBox.Show($"Diagnostics exported to:\n{dialog.FileName}", "Export Successful", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to export diagnostics:\n{ex.Message}", "Export Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    public void LogDebug(string message)
    {
        var timestamp = _timestampCheck?.Checked == true ? $"[{DateTime.Now:HH:mm:ss.fff}] " : "";
        LogConnection($"{timestamp}DEBUG: {message}");
    }
}
