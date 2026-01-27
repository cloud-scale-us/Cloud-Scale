using ScaleStreamer.Common.IPC;
using ScaleStreamer.Common.Settings;
using Serilog;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace ScaleStreamer.Config;

/// <summary>
/// ONVIF Server Monitor tab - full diagnostics for the ONVIF server
/// </summary>
public partial class OnvifMonitorTab : UserControl
{
    private static readonly ILogger _log = Log.ForContext<OnvifMonitorTab>();
    private readonly IpcClient _ipcClient;

    // Server status
    private Label _serverStatusLabel;
    private Label _localIpLabel;
    private Label _httpPortLabel;
    private Label _rtspPortLabel;
    private Label _discoveryLabel;
    private Label _streamUrlLabel;
    private Label _onvifUrlLabel;
    private Label _machineNameLabel;
    private Label _uptimeLabel;

    // Network interfaces
    private ListView _networkListView;

    // Log
    private TextBox _logTextBox;

    // Timer
    private System.Windows.Forms.Timer _refreshTimer;
    private DateTime _startTime = DateTime.Now;

    public OnvifMonitorTab(IpcClient ipcClient)
    {
        _ipcClient = ipcClient;
        InitializeComponent();
        RefreshStatus();

        _refreshTimer = new System.Windows.Forms.Timer { Interval = 5000 };
        _refreshTimer.Tick += (s, e) => RefreshStatus();
        _refreshTimer.Start();
    }

    private void InitializeComponent()
    {
        this.Padding = new Padding(10);

        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            ColumnCount = 1
        };
        mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 40F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 60F));

        // Server Status Panel
        mainLayout.Controls.Add(CreateServerPanel(), 0, 0);

        // Network Interfaces Panel
        mainLayout.Controls.Add(CreateNetworkPanel(), 0, 1);

        // Log Panel
        mainLayout.Controls.Add(CreateLogPanel(), 0, 2);

        this.Controls.Add(mainLayout);
    }

    private Control CreateServerPanel()
    {
        var panel = new GroupBox
        {
            Text = "ONVIF Server Status",
            Dock = DockStyle.Fill,
            Padding = new Padding(15),
            AutoSize = true
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            AutoSize = true
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

        int row = 0;

        // Row 0: Server Status + Machine Name
        layout.Controls.Add(MakeLabel("Server Status:"), 0, row);
        _serverStatusLabel = MakeValueLabel("Checking...", Color.Gray);
        layout.Controls.Add(_serverStatusLabel, 1, row);
        layout.Controls.Add(MakeLabel("Machine Name:"), 2, row);
        _machineNameLabel = MakeValueLabel(Environment.MachineName, Color.Black);
        layout.Controls.Add(_machineNameLabel, 3, row);
        row++;

        // Row 1: Local IP + HTTP Port
        layout.Controls.Add(MakeLabel("Local IPv4:"), 0, row);
        _localIpLabel = MakeValueLabel("Detecting...", Color.Gray);
        layout.Controls.Add(_localIpLabel, 1, row);
        layout.Controls.Add(MakeLabel("HTTP Port:"), 2, row);
        _httpPortLabel = MakeValueLabel("--", Color.Gray);
        layout.Controls.Add(_httpPortLabel, 3, row);
        row++;

        // Row 2: RTSP Port + Discovery
        layout.Controls.Add(MakeLabel("RTSP Port:"), 0, row);
        _rtspPortLabel = MakeValueLabel("--", Color.Gray);
        layout.Controls.Add(_rtspPortLabel, 1, row);
        layout.Controls.Add(MakeLabel("WS-Discovery:"), 2, row);
        _discoveryLabel = MakeValueLabel("--", Color.Gray);
        layout.Controls.Add(_discoveryLabel, 3, row);
        row++;

        // Row 3: Stream URL
        layout.Controls.Add(MakeLabel("Stream URL:"), 0, row);
        _streamUrlLabel = MakeValueLabel("--", Color.Blue);
        _streamUrlLabel.Cursor = Cursors.Hand;
        _streamUrlLabel.Click += (s, e) =>
        {
            if (_streamUrlLabel.Text != "--")
            {
                Clipboard.SetText(_streamUrlLabel.Text);
                LogMessage("Stream URL copied to clipboard.");
            }
        };
        layout.SetColumnSpan(_streamUrlLabel, 3);
        layout.Controls.Add(_streamUrlLabel, 1, row);
        row++;

        // Row 4: ONVIF URL
        layout.Controls.Add(MakeLabel("ONVIF URL:"), 0, row);
        _onvifUrlLabel = MakeValueLabel("--", Color.Blue);
        _onvifUrlLabel.Cursor = Cursors.Hand;
        _onvifUrlLabel.Click += (s, e) =>
        {
            if (_onvifUrlLabel.Text != "--")
            {
                Clipboard.SetText(_onvifUrlLabel.Text);
                LogMessage("ONVIF URL copied to clipboard.");
            }
        };
        layout.SetColumnSpan(_onvifUrlLabel, 3);
        layout.Controls.Add(_onvifUrlLabel, 1, row);
        row++;

        // Row 5: Uptime + Refresh button
        layout.Controls.Add(MakeLabel("Uptime:"), 0, row);
        _uptimeLabel = MakeValueLabel("--", Color.Gray);
        layout.Controls.Add(_uptimeLabel, 1, row);

        var refreshBtn = new Button { Text = "Refresh", Width = 80 };
        refreshBtn.Click += (s, e) => RefreshStatus();
        layout.Controls.Add(refreshBtn, 2, row);
        row++;

        panel.Controls.Add(layout);
        return panel;
    }

    private Control CreateNetworkPanel()
    {
        var panel = new GroupBox
        {
            Text = "Network Interfaces",
            Dock = DockStyle.Fill,
            Padding = new Padding(5)
        };

        _networkListView = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            Font = new Font("Segoe UI", 9F)
        };

        _networkListView.Columns.Add("Name", 180);
        _networkListView.Columns.Add("IPv4 Address", 140);
        _networkListView.Columns.Add("MAC Address", 150);
        _networkListView.Columns.Add("Type", 120);
        _networkListView.Columns.Add("Status", 80);
        _networkListView.Columns.Add("Speed", 100);

        panel.Controls.Add(_networkListView);
        return panel;
    }

    private Control CreateLogPanel()
    {
        var panel = new GroupBox
        {
            Text = "ONVIF Server Log",
            Dock = DockStyle.Fill,
            Padding = new Padding(5)
        };

        _logTextBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            ReadOnly = true,
            Font = new Font("Consolas", 9F),
            BackColor = Color.Black,
            ForeColor = Color.LimeGreen
        };

        panel.Controls.Add(_logTextBox);
        return panel;
    }

    private void RefreshStatus()
    {
        try
        {
            var settings = AppSettings.Instance;
            var onvifSettings = settings.Onvif;
            var rtspSettings = settings.RtspStream;

            // Get primary NIC
            var localIp = GetLocalIPv4();

            // Server status
            var enabled = onvifSettings.Enabled;
            _serverStatusLabel.Text = enabled ? "Running" : "Disabled";
            _serverStatusLabel.ForeColor = enabled ? Color.Green : Color.Red;

            // IP and ports
            _localIpLabel.Text = localIp;
            _localIpLabel.ForeColor = localIp == "127.0.0.1" ? Color.Orange : Color.Green;

            _httpPortLabel.Text = onvifSettings.HttpPort.ToString();
            _httpPortLabel.ForeColor = Color.Black;

            _rtspPortLabel.Text = rtspSettings.Port.ToString();
            _rtspPortLabel.ForeColor = Color.Black;

            _discoveryLabel.Text = onvifSettings.DiscoveryEnabled ? "Enabled" : "Disabled";
            _discoveryLabel.ForeColor = onvifSettings.DiscoveryEnabled ? Color.Green : Color.Orange;

            // URLs
            var streamUser = rtspSettings.Username;
            var streamPass = rtspSettings.Password;
            var authPart = !string.IsNullOrEmpty(streamUser) ? $"{streamUser}:{streamPass}@" : "";

            _streamUrlLabel.Text = $"rtsp://{authPart}{localIp}:{rtspSettings.Port}/scale1";
            _onvifUrlLabel.Text = $"http://{localIp}:{onvifSettings.HttpPort}/onvif/device_service";

            // Uptime
            var uptime = DateTime.Now - _startTime;
            _uptimeLabel.Text = $"{uptime.Hours:D2}:{uptime.Minutes:D2}:{uptime.Seconds:D2}";

            // Refresh network interfaces
            RefreshNetworkInterfaces();
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "Error refreshing ONVIF status");
            LogMessage($"Error: {ex.Message}");
        }
    }

    private void RefreshNetworkInterfaces()
    {
        _networkListView.BeginUpdate();
        _networkListView.Items.Clear();

        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                    continue;

                var ipProps = nic.GetIPProperties();
                var ipv4 = ipProps.UnicastAddresses
                    .FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork);

                var mac = BitConverter.ToString(nic.GetPhysicalAddress().GetAddressBytes()).Replace('-', ':');

                var item = new ListViewItem(nic.Name);
                item.SubItems.Add(ipv4?.Address.ToString() ?? "N/A");
                item.SubItems.Add(mac.Length > 2 ? mac : "N/A");
                item.SubItems.Add(nic.NetworkInterfaceType.ToString());
                item.SubItems.Add(nic.OperationalStatus.ToString());
                item.SubItems.Add(nic.Speed > 0 ? $"{nic.Speed / 1_000_000} Mbps" : "N/A");

                if (nic.OperationalStatus == OperationalStatus.Up && ipv4 != null)
                {
                    item.ForeColor = Color.Green;
                }
                else
                {
                    item.ForeColor = Color.Gray;
                }

                _networkListView.Items.Add(item);
            }
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "Error enumerating network interfaces");
        }

        _networkListView.EndUpdate();
    }

    private void LogMessage(string message)
    {
        if (InvokeRequired)
        {
            Invoke(() => LogMessage(message));
            return;
        }

        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        _logTextBox.AppendText($"[{timestamp}] {message}\r\n");
    }

    private string GetLocalIPv4()
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
            // Fallback: scan NICs
            try
            {
                var nic = NetworkInterface.GetAllNetworkInterfaces().FirstOrDefault(
                    i => i.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                         i.NetworkInterfaceType != NetworkInterfaceType.Tunnel &&
                         i.OperationalStatus == OperationalStatus.Up);

                if (nic != null)
                {
                    var addr = nic.GetIPProperties().UnicastAddresses
                        .FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork);
                    return addr?.Address.ToString() ?? "127.0.0.1";
                }
            }
            catch { }
            return "127.0.0.1";
        }
    }

    private Label MakeLabel(string text)
    {
        return new Label
        {
            Text = text,
            AutoSize = true,
            Font = new Font("Segoe UI", 9F),
            Padding = new Padding(0, 5, 0, 5)
        };
    }

    private Label MakeValueLabel(string text, Color color)
    {
        return new Label
        {
            Text = text,
            AutoSize = true,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = color,
            Padding = new Padding(0, 5, 0, 5)
        };
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _refreshTimer?.Stop();
            _refreshTimer?.Dispose();
        }
        base.Dispose(disposing);
    }
}
