using ScaleStreamer.Common.IPC;
using ScaleStreamer.Common.Models;
using System.Text.Json;
using System.Net;
using System.Net.Sockets;

namespace ScaleStreamer.Config;

/// <summary>
/// Real-time monitoring dashboard showing live weight readings
/// </summary>
public partial class MonitoringTab : UserControl
{
    private readonly IpcClient _ipcClient;
    private Label _currentWeightLabel;
    private Label _tareLabel;
    private Label _grossLabel;
    private Label _netLabel;
    private Label _statusLabel;
    private Label _unitLabel;
    private Label _lastUpdateLabel;
    private Label _readingRateLabel;
    private TextBox _rawDataText;
    private ListView _historyListView;

    private int _readingCount = 0;
    private DateTime _startTime = DateTime.Now;

    public MonitoringTab(IpcClient ipcClient)
    {
        _ipcClient = ipcClient;
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        this.Padding = new Padding(10);

        // Create main layout
        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 2
        };
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
        mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60F));
        mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F));

        // Current Weight Display (top-left)
        var weightPanel = CreateWeightDisplayPanel();
        mainLayout.Controls.Add(weightPanel, 0, 0);

        // Status Panel (top-right)
        var statusPanel = CreateStatusPanel();
        mainLayout.Controls.Add(statusPanel, 1, 0);

        // History List (bottom-left)
        var historyPanel = CreateHistoryPanel();
        mainLayout.Controls.Add(historyPanel, 0, 1);

        // Raw Data Panel (bottom-right)
        var rawDataPanel = CreateRawDataPanel();
        mainLayout.Controls.Add(rawDataPanel, 1, 1);

        this.Controls.Add(mainLayout);
    }

    private Control CreateWeightDisplayPanel()
    {
        var panel = new GroupBox
        {
            Text = "Current Weight",
            Dock = DockStyle.Fill,
            Padding = new Padding(20)
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2
        };

        // Current Weight (large display)
        _currentWeightLabel = new Label
        {
            Text = "0.00",
            Font = new Font("Segoe UI", 48F, FontStyle.Bold),
            AutoSize = true,
            ForeColor = Color.DarkBlue,
            TextAlign = ContentAlignment.MiddleCenter
        };

        _unitLabel = new Label
        {
            Text = "lb",
            Font = new Font("Segoe UI", 24F),
            AutoSize = true,
            ForeColor = Color.Gray,
            TextAlign = ContentAlignment.MiddleCenter
        };

        var weightContainer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            Anchor = AnchorStyles.None
        };
        weightContainer.Controls.Add(_currentWeightLabel);
        weightContainer.Controls.Add(_unitLabel);

        layout.Controls.Add(weightContainer);
        layout.SetColumnSpan(weightContainer, 2);
        layout.SetRow(weightContainer, 0);

        // Status
        int row = 1;
        AddWeightField(layout, "Status:", out _statusLabel, ref row, Color.Gray);
        AddWeightField(layout, "Tare:", out _tareLabel, ref row, Color.Black);
        AddWeightField(layout, "Gross:", out _grossLabel, ref row, Color.Black);
        AddWeightField(layout, "Net:", out _netLabel, ref row, Color.Black);

        panel.Controls.Add(layout);
        return panel;
    }

    private void AddWeightField(TableLayoutPanel layout, string label, out Label valueLabel, ref int row, Color color)
    {
        var labelControl = new Label
        {
            Text = label,
            AutoSize = true,
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleRight
        };

        valueLabel = new Label
        {
            Text = "-",
            AutoSize = true,
            Font = new Font("Segoe UI", 11F),
            ForeColor = color,
            TextAlign = ContentAlignment.MiddleLeft
        };

        layout.Controls.Add(labelControl);
        layout.SetColumn(labelControl, 0);
        layout.SetRow(labelControl, row);

        layout.Controls.Add(valueLabel);
        layout.SetColumn(valueLabel, 1);
        layout.SetRow(valueLabel, row);

        row++;
    }

    private Control CreateStatusPanel()
    {
        var panel = new GroupBox
        {
            Text = "Statistics",
            Dock = DockStyle.Fill,
            Padding = new Padding(10)
        };

        var layout = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown
        };

        _lastUpdateLabel = CreateStatLabel("Last Update: Never");
        _readingRateLabel = CreateStatLabel("Reading Rate: 0.0/sec");

        layout.Controls.Add(_lastUpdateLabel);
        layout.Controls.Add(_readingRateLabel);

        // RTSP URL
        var rtspUrl = $"rtsp://{GetLocalIPv4Address()}:8554/scale1";
        var rtspUrlLabel = CreateStatLabel($"RTSP URL: {rtspUrl}");
        rtspUrlLabel.Cursor = Cursors.Hand;
        rtspUrlLabel.ForeColor = Color.Blue;
        rtspUrlLabel.Click += (s, e) => Clipboard.SetText(rtspUrl);
        rtspUrlLabel.MouseEnter += (s, e) => rtspUrlLabel.Font = new Font(rtspUrlLabel.Font, FontStyle.Underline);
        rtspUrlLabel.MouseLeave += (s, e) => rtspUrlLabel.Font = new Font(rtspUrlLabel.Font, FontStyle.Regular);
        layout.Controls.Add(rtspUrlLabel);

        var clearButton = new Button
        {
            Text = "Clear History",
            Width = 120,
            Margin = new Padding(0, 20, 0, 0)
        };
        clearButton.Click += (s, e) => ClearHistory();
        layout.Controls.Add(clearButton);

        panel.Controls.Add(layout);
        return panel;
    }

    private Label CreateStatLabel(string text)
    {
        return new Label
        {
            Text = text,
            AutoSize = true,
            Font = new Font("Segoe UI", 10F),
            Padding = new Padding(0, 5, 0, 5)
        };
    }

    private Control CreateHistoryPanel()
    {
        var panel = new GroupBox
        {
            Text = "Reading History (Last 100)",
            Dock = DockStyle.Fill,
            Padding = new Padding(5)
        };

        _historyListView = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            Font = new Font("Segoe UI", 9F)
        };

        _historyListView.Columns.Add("Time", 100);
        _historyListView.Columns.Add("Weight", 100);
        _historyListView.Columns.Add("Unit", 60);
        _historyListView.Columns.Add("Status", 100);
        _historyListView.Columns.Add("Tare", 80);
        _historyListView.Columns.Add("Gross", 80);

        panel.Controls.Add(_historyListView);
        return panel;
    }

    private Control CreateRawDataPanel()
    {
        var panel = new GroupBox
        {
            Text = "Raw Data Stream",
            Dock = DockStyle.Fill,
            Padding = new Padding(5)
        };

        _rawDataText = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            ReadOnly = true,
            Font = new Font("Consolas", 9F),
            BackColor = Color.Black,
            ForeColor = Color.LimeGreen
        };

        panel.Controls.Add(_rawDataText);
        return panel;
    }

    public void HandleWeightReading(IpcMessage message)
    {
        try
        {
            if (string.IsNullOrEmpty(message.Payload))
                return;

            var reading = JsonSerializer.Deserialize<WeightReading>(message.Payload);
            if (reading == null)
                return;

            UpdateDisplay(reading);
        }
        catch (Exception ex)
        {
            AppendRawData($"Error: {ex.Message}");
        }
    }

    private void UpdateDisplay(WeightReading reading)
    {
        if (InvokeRequired)
        {
            Invoke(() => UpdateDisplay(reading));
            return;
        }

        // Update current weight display
        _currentWeightLabel.Text = reading.Weight.ToString("F2");
        _unitLabel.Text = reading.Unit;

        // Update status color
        _statusLabel.Text = reading.Status.ToString();
        _statusLabel.ForeColor = reading.Status switch
        {
            ScaleStatus.Stable => Color.Green,
            ScaleStatus.Motion => Color.Orange,
            ScaleStatus.Overload => Color.Red,
            ScaleStatus.Underload => Color.Red,
            ScaleStatus.Error => Color.Red,
            _ => Color.Gray
        };

        // Update other fields
        _tareLabel.Text = reading.Tare?.ToString("F2") ?? "-";
        _grossLabel.Text = reading.Gross?.ToString("F2") ?? "-";
        _netLabel.Text = reading.Net?.ToString("F2") ?? "-";

        // Update statistics
        _readingCount++;
        var elapsed = (DateTime.Now - _startTime).TotalSeconds;
        var rate = elapsed > 0 ? _readingCount / elapsed : 0;
        _readingRateLabel.Text = $"Reading Rate: {rate:F1}/sec";
        _lastUpdateLabel.Text = $"Last Update: {DateTime.Now:HH:mm:ss.fff}";

        // Add to history (keep last 100)
        var item = new ListViewItem(reading.Timestamp.ToLocalTime().ToString("HH:mm:ss.fff"));
        item.SubItems.Add(reading.Weight.ToString("F2"));
        item.SubItems.Add(reading.Unit);
        item.SubItems.Add(reading.Status.ToString());
        item.SubItems.Add(reading.Tare?.ToString("F2") ?? "-");
        item.SubItems.Add(reading.Gross?.ToString("F2") ?? "-");

        _historyListView.Items.Insert(0, item);

        if (_historyListView.Items.Count > 100)
        {
            _historyListView.Items.RemoveAt(100);
        }

        // Show raw data
        if (!string.IsNullOrEmpty(reading.RawData))
        {
            AppendRawData(reading.RawData);
        }
    }

    private void AppendRawData(string data)
    {
        if (InvokeRequired)
        {
            Invoke(() => AppendRawData(data));
            return;
        }

        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        _rawDataText.AppendText($"[{timestamp}] {data}\r\n");

        // Keep only last 1000 lines
        var lines = _rawDataText.Lines;
        if (lines.Length > 1000)
        {
            _rawDataText.Lines = lines.Skip(lines.Length - 1000).ToArray();
        }
    }

    private void ClearHistory()
    {
        _historyListView.Items.Clear();
        _rawDataText.Clear();
        _readingCount = 0;
        _startTime = DateTime.Now;
        _readingRateLabel.Text = "Reading Rate: 0.0/sec";
    }

    private string GetLocalIPv4Address()
    {
        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
            socket.Connect("8.8.8.8", 65530);
            var endPoint = socket.LocalEndPoint as IPEndPoint;
            return endPoint?.Address.ToString() ?? "127.0.0.1";
        }
        catch
        {
            return "127.0.0.1";
        }
    }
}
