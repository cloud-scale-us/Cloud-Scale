using ScaleStreamer.Common.IPC;
using System.Text.Json;

namespace ScaleStreamer.Config;

/// <summary>
/// System status tab showing service and scale connection status
/// </summary>
public partial class StatusTab : UserControl
{
    private readonly IpcClient _ipcClient;
    private ListView _scalesListView;
    private Label _serviceStatusLabel;
    private Label _uptimeLabel;
    private Label _databasePathLabel;
    private Label _logPathLabel;
    private Button _refreshButton;
    private Button _startServiceButton;
    private Button _stopServiceButton;
    private Button _restartServiceButton;
    private System.Windows.Forms.Timer _updateTimer;

    private DateTime _serviceStartTime = DateTime.Now;

    public StatusTab(IpcClient ipcClient)
    {
        _ipcClient = ipcClient;
        InitializeComponent();

        _updateTimer = new System.Windows.Forms.Timer
        {
            Interval = 1000 // Update every second
        };
        _updateTimer.Tick += UpdateTimer_Tick;
        _updateTimer.Start();
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
        mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        // Service Status Panel (top)
        var servicePanel = CreateServicePanel();
        mainLayout.Controls.Add(servicePanel, 0, 0);

        // Scales Status Panel (bottom)
        var scalesPanel = CreateScalesPanel();
        mainLayout.Controls.Add(scalesPanel, 0, 1);

        this.Controls.Add(mainLayout);
    }

    private Control CreateServicePanel()
    {
        var panel = new GroupBox
        {
            Text = "Service Status",
            Dock = DockStyle.Fill,
            Padding = new Padding(10),
            AutoSize = true
        };

        var layout = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown
        };

        // Service Status
        _serviceStatusLabel = new Label
        {
            Text = "Service: Running",
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            ForeColor = Color.Green,
            AutoSize = true,
            Padding = new Padding(0, 0, 0, 10)
        };
        layout.Controls.Add(_serviceStatusLabel);

        // Uptime
        _uptimeLabel = new Label
        {
            Text = "Uptime: 00:00:00",
            Font = new Font("Segoe UI", 10F),
            AutoSize = true
        };
        layout.Controls.Add(_uptimeLabel);

        // Database Path
        _databasePathLabel = new Label
        {
            Text = "Database: C:\\ProgramData\\ScaleStreamer\\scalestreamer.db",
            Font = new Font("Segoe UI", 9F),
            AutoSize = true,
            Padding = new Padding(0, 5, 0, 0)
        };
        layout.Controls.Add(_databasePathLabel);

        // Log Path
        _logPathLabel = new Label
        {
            Text = "Logs: C:\\ProgramData\\ScaleStreamer\\logs\\",
            Font = new Font("Segoe UI", 9F),
            AutoSize = true
        };
        layout.Controls.Add(_logPathLabel);

        // Service Control Buttons
        var buttonPanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            Padding = new Padding(0, 10, 0, 0)
        };

        _startServiceButton = new Button
        {
            Text = "Start Service",
            Width = 110,
            Enabled = false
        };
        _startServiceButton.Click += StartService_Click;

        _stopServiceButton = new Button
        {
            Text = "Stop Service",
            Width = 110
        };
        _stopServiceButton.Click += StopService_Click;

        _restartServiceButton = new Button
        {
            Text = "Restart Service",
            Width = 110
        };
        _restartServiceButton.Click += RestartService_Click;

        buttonPanel.Controls.Add(_startServiceButton);
        buttonPanel.Controls.Add(_stopServiceButton);
        buttonPanel.Controls.Add(_restartServiceButton);

        layout.Controls.Add(buttonPanel);

        panel.Controls.Add(layout);
        return panel;
    }

    private Control CreateScalesPanel()
    {
        var panel = new GroupBox
        {
            Text = "Connected Scales",
            Dock = DockStyle.Fill,
            Padding = new Padding(5)
        };

        var layout = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown
        };

        _scalesListView = new ListView
        {
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            Width = 800,
            Height = 300
        };

        _scalesListView.Columns.Add("Scale ID", 150);
        _scalesListView.Columns.Add("Name", 150);
        _scalesListView.Columns.Add("Protocol", 120);
        _scalesListView.Columns.Add("Connection", 120);
        _scalesListView.Columns.Add("Status", 100);
        _scalesListView.Columns.Add("Last Reading", 150);

        _refreshButton = new Button
        {
            Text = "Refresh Status",
            Width = 120,
            Margin = new Padding(0, 10, 0, 0)
        };
        _refreshButton.Click += Refresh_Click;

        layout.Controls.Add(_scalesListView);
        layout.Controls.Add(_refreshButton);

        panel.Controls.Add(layout);
        return panel;
    }

    public void HandleConnectionStatus(IpcMessage message)
    {
        try
        {
            if (string.IsNullOrEmpty(message.Payload))
                return;

            // Parse status update and update UI
            RefreshScalesList();
        }
        catch (Exception ex)
        {
            // Log error
        }
    }

    private void UpdateTimer_Tick(object? sender, EventArgs e)
    {
        // Update uptime
        var uptime = DateTime.Now - _serviceStartTime;
        _uptimeLabel.Text = $"Uptime: {uptime:hh\\:mm\\:ss}";
    }

    private async void Refresh_Click(object? sender, EventArgs e)
    {
        _refreshButton.Enabled = false;
        try
        {
            await RefreshScalesList();
        }
        finally
        {
            _refreshButton.Enabled = true;
        }
    }

    private async Task RefreshScalesList()
    {
        if (InvokeRequired)
        {
            Invoke(async () => await RefreshScalesList());
            return;
        }

        _scalesListView.Items.Clear();

        // TODO: Request scale list from service via IPC
        // For now, show placeholder
        var item = new ListViewItem("scale-1");
        item.SubItems.Add("Test Scale");
        item.SubItems.Add("Fairbanks 6011");
        item.SubItems.Add("TCP/IP");
        item.SubItems.Add("Connected");
        item.SubItems.Add(DateTime.Now.ToString("HH:mm:ss"));
        item.ForeColor = Color.Green;

        _scalesListView.Items.Add(item);

        await Task.CompletedTask;
    }

    private void StartService_Click(object? sender, EventArgs e)
    {
        try
        {
            // TODO: Start service via sc.exe or ServiceController
            MessageBox.Show("Service start command sent.", "Service Control", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error starting service: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void StopService_Click(object? sender, EventArgs e)
    {
        var result = MessageBox.Show(
            "Are you sure you want to stop the Scale Streamer Service?\nAll scale connections will be terminated.",
            "Confirm Stop Service",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (result == DialogResult.Yes)
        {
            try
            {
                // TODO: Stop service via sc.exe or ServiceController
                MessageBox.Show("Service stop command sent.", "Service Control", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error stopping service: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void RestartService_Click(object? sender, EventArgs e)
    {
        var result = MessageBox.Show(
            "Are you sure you want to restart the Scale Streamer Service?\nAll scale connections will be temporarily interrupted.",
            "Confirm Restart Service",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (result == DialogResult.Yes)
        {
            try
            {
                // TODO: Restart service via sc.exe or ServiceController
                MessageBox.Show("Service restart command sent.", "Service Control", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error restarting service: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _updateTimer?.Stop();
            _updateTimer?.Dispose();
        }
        base.Dispose(disposing);
    }
}
