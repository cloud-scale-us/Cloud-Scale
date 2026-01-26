using ScaleStreamer.Common.IPC;
using ScaleStreamer.Common.Models;
using System.Text.Json;
using System.Net;
using System.Net.Sockets;
using Serilog;

namespace ScaleStreamer.Config;

/// <summary>
/// Real-time monitoring dashboard showing live weight readings
/// Rebuilt for reliability - direct IPC message handling
/// </summary>
public partial class MonitoringTab : UserControl
{
    private static readonly ILogger _log = Log.ForContext<MonitoringTab>();

    private readonly IpcClient _ipcClient;

    // Main display controls
    private Label _currentWeightLabel = null!;
    private Label _unitLabel = null!;
    private Label _statusLabel = null!;
    private Label _lastUpdateLabel = null!;
    private Label _connectionStatusLabel = null!;
    private Label _readingRateLabel = null!;
    private TextBox _rawDataText = null!;
    private ListView _historyListView = null!;
    private Panel _statusBarPanel = null!;

    private int _readingCount = 0;
    private DateTime _startTime = DateTime.Now;
    private DateTime _lastReadingTime = DateTime.MinValue;
    private DateTime _lastUiUpdate = DateTime.MinValue;
    private const int UI_UPDATE_INTERVAL_MS = 250; // Throttle UI updates to 4/second max
    private WeightReading? _latestReading = null;

    public MonitoringTab(IpcClient ipcClient)
    {
        _ipcClient = ipcClient;
        _log.Information("MonitoringTab constructor starting");
        InitializeComponent();
        _log.Information("MonitoringTab initialized");
    }

    private void InitializeComponent()
    {
        this.SuspendLayout();
        this.Padding = new Padding(10);

        // Main container - split into top status bar and content below
        var mainContainer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1
        };
        mainContainer.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F)); // Status bar
        mainContainer.RowStyles.Add(new RowStyle(SizeType.Percent, 100F)); // Content

        // === STATUS BAR (TOP) ===
        _statusBarPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(240, 240, 240),
            Padding = new Padding(10, 5, 10, 5)
        };

        _connectionStatusLabel = new Label
        {
            Text = "● Disconnected",
            ForeColor = Color.Red,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(10, 10)
        };

        _lastUpdateLabel = new Label
        {
            Text = "Last Update: Never",
            ForeColor = Color.Gray,
            Font = new Font("Segoe UI", 9F),
            AutoSize = true,
            Location = new Point(180, 12)
        };

        _readingRateLabel = new Label
        {
            Text = "Rate: 0.0/sec",
            ForeColor = Color.Gray,
            Font = new Font("Segoe UI", 9F),
            AutoSize = true,
            Location = new Point(400, 12)
        };

        _statusBarPanel.Controls.Add(_connectionStatusLabel);
        _statusBarPanel.Controls.Add(_lastUpdateLabel);
        _statusBarPanel.Controls.Add(_readingRateLabel);

        mainContainer.Controls.Add(_statusBarPanel, 0, 0);

        // === CONTENT AREA ===
        var contentLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 2
        };
        contentLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
        contentLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
        contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60F));
        contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F));

        // Current Weight Display (top-left)
        var weightPanel = CreateWeightDisplayPanel();
        contentLayout.Controls.Add(weightPanel, 0, 0);

        // Status Panel (top-right)
        var statusPanel = CreateInfoPanel();
        contentLayout.Controls.Add(statusPanel, 1, 0);

        // History List (bottom-left)
        var historyPanel = CreateHistoryPanel();
        contentLayout.Controls.Add(historyPanel, 0, 1);

        // Raw Data Panel (bottom-right)
        var rawDataPanel = CreateRawDataPanel();
        contentLayout.Controls.Add(rawDataPanel, 1, 1);

        mainContainer.Controls.Add(contentLayout, 0, 1);

        this.Controls.Add(mainContainer);
        this.ResumeLayout(true);
    }

    private Control CreateWeightDisplayPanel()
    {
        var panel = new GroupBox
        {
            Text = "Current Weight",
            Dock = DockStyle.Fill,
            Padding = new Padding(20),
            Font = new Font("Segoe UI", 10F)
        };

        var innerPanel = new Panel { Dock = DockStyle.Fill };

        // Current Weight (large display)
        _currentWeightLabel = new Label
        {
            Text = "---",
            Font = new Font("Segoe UI", 72F, FontStyle.Bold),
            ForeColor = Color.DarkBlue,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Top,
            Height = 120
        };

        _unitLabel = new Label
        {
            Text = "lb",
            Font = new Font("Segoe UI", 28F),
            ForeColor = Color.Gray,
            AutoSize = false,
            TextAlign = ContentAlignment.TopCenter,
            Dock = DockStyle.Top,
            Height = 50
        };

        _statusLabel = new Label
        {
            Text = "Status: Unknown",
            Font = new Font("Segoe UI", 14F, FontStyle.Bold),
            ForeColor = Color.Gray,
            AutoSize = false,
            TextAlign = ContentAlignment.TopCenter,
            Dock = DockStyle.Top,
            Height = 40
        };

        innerPanel.Controls.Add(_statusLabel);
        innerPanel.Controls.Add(_unitLabel);
        innerPanel.Controls.Add(_currentWeightLabel);

        panel.Controls.Add(innerPanel);
        return panel;
    }

    private Control CreateInfoPanel()
    {
        var panel = new GroupBox
        {
            Text = "Information",
            Dock = DockStyle.Fill,
            Padding = new Padding(10),
            Font = new Font("Segoe UI", 10F)
        };

        var layout = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false
        };

        // RTSP URL
        var rtspUrl = $"rtsp://{GetLocalIPv4Address()}:8554/scale1";
        var rtspLabel = new Label
        {
            Text = $"RTSP: {rtspUrl}",
            Font = new Font("Segoe UI", 9F),
            AutoSize = true,
            Padding = new Padding(0, 5, 0, 5),
            ForeColor = Color.Blue,
            Cursor = Cursors.Hand
        };
        rtspLabel.Click += (s, e) =>
        {
            Clipboard.SetText(rtspUrl);
            MessageBox.Show("RTSP URL copied to clipboard!", "Copied", MessageBoxButtons.OK, MessageBoxIcon.Information);
        };

        var clearButton = new Button
        {
            Text = "Clear History",
            Width = 120,
            Height = 30,
            Margin = new Padding(0, 20, 0, 0)
        };
        clearButton.Click += (s, e) => ClearHistory();

        var testButton = new Button
        {
            Text = "Test Display",
            Width = 120,
            Height = 30,
            Margin = new Padding(0, 5, 0, 0)
        };
        testButton.Click += (s, e) => TestDisplay();

        layout.Controls.Add(rtspLabel);
        layout.Controls.Add(clearButton);
        layout.Controls.Add(testButton);

        panel.Controls.Add(layout);
        return panel;
    }

    private Control CreateHistoryPanel()
    {
        var panel = new GroupBox
        {
            Text = "Reading History (Last 100)",
            Dock = DockStyle.Fill,
            Padding = new Padding(5),
            Font = new Font("Segoe UI", 10F)
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
        _historyListView.Columns.Add("Status", 80);

        panel.Controls.Add(_historyListView);
        return panel;
    }

    private Control CreateRawDataPanel()
    {
        var panel = new GroupBox
        {
            Text = "Debug Log",
            Dock = DockStyle.Fill,
            Padding = new Padding(5),
            Font = new Font("Segoe UI", 10F)
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

    /// <summary>
    /// Handle incoming weight reading from IPC - called by MainForm
    /// Throttled to prevent GUI freeze from high-frequency updates
    /// </summary>
    public void HandleWeightReading(IpcMessage message)
    {
        try
        {
            if (string.IsNullOrEmpty(message.Payload))
            {
                return;
            }

            // Deserialize the WeightReading from the payload
            var reading = JsonSerializer.Deserialize<WeightReading>(message.Payload);
            if (reading == null)
            {
                return;
            }

            // Always count readings for rate calculation
            _readingCount++;
            _lastReadingTime = DateTime.Now;
            _latestReading = reading;

            // Throttle UI updates to prevent freeze
            var now = DateTime.Now;
            if ((now - _lastUiUpdate).TotalMilliseconds < UI_UPDATE_INTERVAL_MS)
            {
                // Skip UI update, but we've stored the latest reading
                return;
            }
            _lastUiUpdate = now;

            // Now update the display with the latest reading
            UpdateDisplay(reading);
        }
        catch (JsonException)
        {
            // Silently ignore JSON errors to prevent log spam
        }
        catch (Exception ex)
        {
            // Only log unexpected errors occasionally
            if (_readingCount % 100 == 0)
            {
                AppendLog($"ERROR: {ex.GetType().Name}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Update connection status - called by MainForm
    /// </summary>
    public void UpdateConnectionStatus(bool connected)
    {
        if (InvokeRequired)
        {
            Invoke(() => UpdateConnectionStatus(connected));
            return;
        }

        if (connected)
        {
            _connectionStatusLabel.Text = "● Connected to Service";
            _connectionStatusLabel.ForeColor = Color.Green;
        }
        else
        {
            _connectionStatusLabel.Text = "● Disconnected";
            _connectionStatusLabel.ForeColor = Color.Red;
        }
    }

    private void UpdateDisplay(WeightReading reading)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => UpdateDisplay(reading));
            return;
        }

        // Batch UI updates with SuspendLayout/ResumeLayout to reduce flicker
        this.SuspendLayout();
        try
        {
            // Update current weight display
            _currentWeightLabel.Text = reading.Weight.ToString("F0");
            _unitLabel.Text = reading.Unit;

            // Update status with color
            _statusLabel.Text = $"Status: {reading.Status}";
            _statusLabel.ForeColor = reading.Status switch
            {
                ScaleStatus.Stable => Color.Green,
                ScaleStatus.Motion => Color.Orange,
                ScaleStatus.Overload => Color.Red,
                ScaleStatus.Underload => Color.Red,
                ScaleStatus.Error => Color.Red,
                _ => Color.Gray
            };

            // Update statistics (use cached values from HandleWeightReading)
            var elapsed = (_lastReadingTime - _startTime).TotalSeconds;
            var rate = elapsed > 0 ? _readingCount / elapsed : 0;
            _readingRateLabel.Text = $"Rate: {rate:F1}/sec";
            _lastUpdateLabel.Text = $"Last Update: {_lastReadingTime:HH:mm:ss.fff}";

            // Mark as connected
            _connectionStatusLabel.Text = "● Receiving Data";
            _connectionStatusLabel.ForeColor = Color.Green;

            // Add to history (keep last 100) - use BeginUpdate/EndUpdate for ListView
            _historyListView.BeginUpdate();
            try
            {
                var item = new ListViewItem(reading.Timestamp.ToLocalTime().ToString("HH:mm:ss.fff"));
                item.SubItems.Add(reading.Weight.ToString("F0"));
                item.SubItems.Add(reading.Unit);
                item.SubItems.Add(reading.Status.ToString());

                _historyListView.Items.Insert(0, item);

                if (_historyListView.Items.Count > 100)
                {
                    _historyListView.Items.RemoveAt(100);
                }
            }
            finally
            {
                _historyListView.EndUpdate();
            }
        }
        finally
        {
            this.ResumeLayout(false);
        }
    }

    private void AppendLog(string message)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => AppendLog(message));
            return;
        }

        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        _rawDataText.AppendText($"[{timestamp}] {message}\r\n");

        // Keep only last 200 lines (reduced from 500 for better performance)
        var lines = _rawDataText.Lines;
        if (lines.Length > 200)
        {
            _rawDataText.Lines = lines.Skip(lines.Length - 200).ToArray();
        }
    }

    /// <summary>
    /// Public method for MainForm to log debug messages
    /// </summary>
    public void AppendDebugLog(string message)
    {
        AppendLog(message);
    }

    private void ClearHistory()
    {
        _historyListView.Items.Clear();
        _rawDataText.Clear();
        _readingCount = 0;
        _startTime = DateTime.Now;
        _readingRateLabel.Text = "Rate: 0.0/sec";
        _currentWeightLabel.Text = "---";
        _statusLabel.Text = "Status: Unknown";
        _statusLabel.ForeColor = Color.Gray;
        AppendLog("History cleared");
    }

    /// <summary>
    /// Test button to verify display is working
    /// </summary>
    private void TestDisplay()
    {
        var testReading = new WeightReading
        {
            Weight = 1234.5,
            Unit = "lb",
            Status = ScaleStatus.Stable,
            Timestamp = DateTime.UtcNow,
            RawData = "TEST DATA"
        };

        AppendLog("Testing display with fake reading...");
        UpdateDisplay(testReading);
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
