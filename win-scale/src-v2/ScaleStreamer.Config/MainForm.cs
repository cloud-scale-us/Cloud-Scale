using ScaleStreamer.Common.IPC;
using Serilog;

namespace ScaleStreamer.Config;

/// <summary>
/// Main configuration window with tabbed interface
/// </summary>
public partial class MainForm : Form
{
    private readonly IpcClient _ipcClient;
    private System.Windows.Forms.Timer _statusTimer;
    private bool _serviceConnected = false;

    // Tab controls
    private TabControl _mainTabControl;
    private ConnectionTab? _connectionTab;
    private ProtocolTab? _protocolTab;
    private MonitoringTab? _monitoringTab;
    private StatusTab? _statusTab;
    private LoggingTab? _loggingTab;

    public MainForm()
    {
        InitializeComponent();

        _ipcClient = new IpcClient("ScaleStreamerPipe");
        _ipcClient.MessageReceived += OnServiceMessageReceived;
        _ipcClient.ErrorOccurred += OnIpcError;

        InitializeTabs();
        InitializeStatusBar();

        // Timer to check service connection
        _statusTimer = new System.Windows.Forms.Timer
        {
            Interval = 5000 // Check every 5 seconds
        };
        _statusTimer.Tick += StatusTimer_Tick;
        _statusTimer.Start();

        // Defer initial connection until form is shown (avoid blocking UI on startup)
        this.Shown += OnFormShown;
    }

    private async void OnFormShown(object? sender, EventArgs e)
    {
        // Give service a moment to fully start if app launched during installation
        await Task.Delay(500);

        // Try initial connection after form is visible
        await ConnectToServiceAsync();
    }

    private void InitializeComponent()
    {
        this.Text = "Scale Streamer Configuration";
        this.Size = new Size(1200, 800);
        this.StartPosition = FormStartPosition.CenterScreen;
        this.MinimumSize = new Size(1000, 600);

        // Create main tab control
        _mainTabControl = new TabControl
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 9F)
        };

        this.Controls.Add(_mainTabControl);
    }

    private void InitializeTabs()
    {
        // Connection Configuration Tab
        var connectionPage = new TabPage("Connection")
        {
            UseVisualStyleBackColor = true
        };
        _connectionTab = new ConnectionTab(_ipcClient);
        _connectionTab.Dock = DockStyle.Fill;
        connectionPage.Controls.Add(_connectionTab);
        _mainTabControl.TabPages.Add(connectionPage);

        // Protocol Configuration Tab
        var protocolPage = new TabPage("Protocol")
        {
            UseVisualStyleBackColor = true
        };
        _protocolTab = new ProtocolTab(_ipcClient);
        _protocolTab.Dock = DockStyle.Fill;
        protocolPage.Controls.Add(_protocolTab);
        _mainTabControl.TabPages.Add(protocolPage);

        // Monitoring Dashboard Tab
        var monitoringPage = new TabPage("Monitoring")
        {
            UseVisualStyleBackColor = true
        };
        _monitoringTab = new MonitoringTab(_ipcClient);
        _monitoringTab.Dock = DockStyle.Fill;
        monitoringPage.Controls.Add(_monitoringTab);
        _mainTabControl.TabPages.Add(monitoringPage);

        // System Status Tab
        var statusPage = new TabPage("Status")
        {
            UseVisualStyleBackColor = true
        };
        _statusTab = new StatusTab(_ipcClient);
        _statusTab.Dock = DockStyle.Fill;
        statusPage.Controls.Add(_statusTab);
        _mainTabControl.TabPages.Add(statusPage);

        // Logging Tab
        var loggingPage = new TabPage("Logs")
        {
            UseVisualStyleBackColor = true
        };
        _loggingTab = new LoggingTab(_ipcClient);
        _loggingTab.Dock = DockStyle.Fill;
        loggingPage.Controls.Add(_loggingTab);
        _mainTabControl.TabPages.Add(loggingPage);
    }

    private void InitializeStatusBar()
    {
        var statusStrip = new StatusStrip();

        var serviceStatusLabel = new ToolStripStatusLabel
        {
            Name = "serviceStatus",
            Text = "Service: Connecting...",
            ForeColor = Color.DarkOrange,
            BorderSides = ToolStripStatusLabelBorderSides.Right
        };

        var versionLabel = new ToolStripStatusLabel
        {
            Text = "v2.0.0",
            Spring = true,
            TextAlign = ContentAlignment.MiddleRight
        };

        statusStrip.Items.Add(serviceStatusLabel);
        statusStrip.Items.Add(versionLabel);

        this.Controls.Add(statusStrip);
    }

    private async Task ConnectToServiceAsync()
    {
        try
        {
            Log.Information("Attempting to connect to Scale Streamer Service...");

            // Use shorter timeout to avoid hanging UI (service should respond quickly if running)
            var connected = await _ipcClient.ConnectAsync(timeoutMs: 1000);

            if (connected)
            {
                _serviceConnected = true;
                UpdateServiceStatus("Service: Connected", true);
                Log.Information("Connected to Scale Streamer Service");
            }
            else
            {
                _serviceConnected = false;
                UpdateServiceStatus("Service: Not Running (retrying...)", false);
                Log.Warning("Failed to connect to Scale Streamer Service - will retry");
            }
        }
        catch (Exception ex)
        {
            _serviceConnected = false;
            UpdateServiceStatus("Service: Error", false);
            Log.Error(ex, "Error connecting to service");
        }
    }

    private void UpdateServiceStatus(string text, bool connected)
    {
        if (InvokeRequired)
        {
            Invoke(() => UpdateServiceStatus(text, connected));
            return;
        }

        var statusStrip = this.Controls.OfType<StatusStrip>().FirstOrDefault();
        if (statusStrip != null)
        {
            var statusLabel = statusStrip.Items["serviceStatus"] as ToolStripStatusLabel;
            if (statusLabel != null)
            {
                statusLabel.Text = text;
                statusLabel.ForeColor = connected ? Color.Green : Color.Red;
            }
        }
    }

    private async void StatusTimer_Tick(object? sender, EventArgs e)
    {
        if (!_serviceConnected || !_ipcClient.IsConnected)
        {
            await ConnectToServiceAsync();
        }
    }

    private void OnServiceMessageReceived(object? sender, IpcMessage message)
    {
        Log.Debug("Received message from service: {MessageType}", message.MessageType);

        // Route message to appropriate tab
        switch (message.MessageType)
        {
            case IpcMessageType.WeightReading:
                _monitoringTab?.HandleWeightReading(message);
                break;

            case IpcMessageType.ConnectionStatus:
                _statusTab?.HandleConnectionStatus(message);
                break;

            case IpcMessageType.Error:
                _loggingTab?.HandleError(message);
                break;
        }
    }

    private void OnIpcError(object? sender, string error)
    {
        Log.Error("IPC Error: {Error}", error);
        _serviceConnected = false;
        UpdateServiceStatus("Service: Error", false);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _statusTimer.Stop();
        _statusTimer.Dispose();

        _ipcClient.Disconnect();
        _ipcClient.Dispose();

        base.OnFormClosing(e);
    }
}
