using ScaleStreamer.Common.IPC;
using ScaleStreamer.Common.Services;
using ScaleStreamer.Common.Settings;
using System.Diagnostics;
using System.ServiceProcess;
using System.Text.Json;
using Serilog;

namespace ScaleStreamer.Config;

/// <summary>
/// System status tab showing service and scale connection status
/// </summary>
public partial class StatusTab : UserControl
{
    private static readonly ILogger _log = Log.ForContext<StatusTab>();
    private readonly IpcClient _ipcClient;

    // Service status controls
    private Label _serviceStatusLabel;
    private Label _serviceUptimeLabel;
    private Label _ipcConnectionLabel;
    private Label _databasePathLabel;
    private Label _logPathLabel;

    // Scale status controls
    private Label _scaleConnectionLabel;
    private Label _scaleHostLabel;
    private Label _scaleProtocolLabel;
    private Label _lastWeightLabel;
    private Label _lastUpdateLabel;
    private ProgressBar _dataFlowIndicator;

    // Buttons
    private Button _refreshButton;
    private Button _startServiceButton;
    private Button _stopServiceButton;
    private Button _restartServiceButton;
    private Button _openLogsButton;
    private Button _grantPermissionsButton;
    private Label _permissionStatusLabel;

    // Timers
    private System.Windows.Forms.Timer _updateTimer;
    private DateTime _lastDataReceived = DateTime.MinValue;
    private int _messageCount = 0;

    public StatusTab(IpcClient ipcClient)
    {
        _ipcClient = ipcClient;
        InitializeComponent();

        // Subscribe to IPC messages to track data flow
        _ipcClient.MessageReceived += OnMessageReceived;

        _updateTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _updateTimer.Tick += UpdateTimer_Tick;
        _updateTimer.Start();
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
        mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Service panel
        mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Scale panel
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F)); // Spacer

        // Service Status Panel
        mainLayout.Controls.Add(CreateServicePanel(), 0, 0);

        // Scale Connection Panel
        mainLayout.Controls.Add(CreateScalePanel(), 0, 1);

        this.Controls.Add(mainLayout);
    }

    private Control CreateServicePanel()
    {
        var panel = new GroupBox
        {
            Text = "Windows Service Status",
            Dock = DockStyle.Fill,
            Padding = new Padding(15),
            AutoSize = true
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            AutoSize = true
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

        int row = 0;

        // Service Status
        layout.Controls.Add(CreateLabel("Service Status:"), 0, row);
        _serviceStatusLabel = CreateValueLabel("Checking...", Color.Gray);
        layout.Controls.Add(_serviceStatusLabel, 1, row++);

        // IPC Connection
        layout.Controls.Add(CreateLabel("IPC Connection:"), 0, row);
        _ipcConnectionLabel = CreateValueLabel("Not Connected", Color.Red);
        layout.Controls.Add(_ipcConnectionLabel, 1, row++);

        // Service Uptime
        layout.Controls.Add(CreateLabel("Service Uptime:"), 0, row);
        _serviceUptimeLabel = CreateValueLabel("--:--:--", Color.Gray);
        layout.Controls.Add(_serviceUptimeLabel, 1, row++);

        // Database Path
        layout.Controls.Add(CreateLabel("Database:"), 0, row);
        _databasePathLabel = CreateValueLabel("C:\\ProgramData\\ScaleStreamer\\scalestreamer.db", Color.Gray);
        layout.Controls.Add(_databasePathLabel, 1, row++);

        // Log Path
        layout.Controls.Add(CreateLabel("Logs:"), 0, row);
        _logPathLabel = CreateValueLabel("C:\\ProgramData\\ScaleStreamer\\logs\\", Color.Gray);
        layout.Controls.Add(_logPathLabel, 1, row++);

        // Buttons
        var buttonPanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            Margin = new Padding(0, 10, 0, 0)
        };

        _startServiceButton = new Button { Text = "Start Service", Width = 100, Enabled = false };
        _startServiceButton.Click += StartService_Click;

        _stopServiceButton = new Button { Text = "Stop Service", Width = 100 };
        _stopServiceButton.Click += StopService_Click;

        _restartServiceButton = new Button { Text = "Restart", Width = 80 };
        _restartServiceButton.Click += RestartService_Click;

        _openLogsButton = new Button { Text = "Open Logs", Width = 80 };
        _openLogsButton.Click += OpenLogs_Click;

        buttonPanel.Controls.Add(_startServiceButton);
        buttonPanel.Controls.Add(_stopServiceButton);
        buttonPanel.Controls.Add(_restartServiceButton);
        buttonPanel.Controls.Add(_openLogsButton);

        layout.Controls.Add(buttonPanel, 0, row);
        layout.SetColumnSpan(buttonPanel, 2);
        row++;

        // Permission status and grant button
        var permissionPanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            Margin = new Padding(0, 5, 0, 0)
        };

        _permissionStatusLabel = new Label
        {
            Text = "Checking permissions...",
            AutoSize = true,
            Font = new Font("Segoe UI", 8F),
            ForeColor = Color.Gray,
            Padding = new Padding(0, 5, 10, 0)
        };

        _grantPermissionsButton = new Button
        {
            Text = "Grant Permissions (One-Time)",
            Width = 180,
            Visible = false
        };
        _grantPermissionsButton.Click += GrantPermissions_Click;

        permissionPanel.Controls.Add(_permissionStatusLabel);
        permissionPanel.Controls.Add(_grantPermissionsButton);

        layout.Controls.Add(permissionPanel, 0, row);
        layout.SetColumnSpan(permissionPanel, 2);

        // Check permissions on load
        CheckServicePermissions();

        panel.Controls.Add(layout);
        return panel;
    }

    private Control CreateScalePanel()
    {
        var panel = new GroupBox
        {
            Text = "Scale Connection Status",
            Dock = DockStyle.Fill,
            Padding = new Padding(15),
            AutoSize = true
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            AutoSize = true
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

        int row = 0;

        // Scale Connection Status
        layout.Controls.Add(CreateLabel("Connection:"), 0, row);
        _scaleConnectionLabel = CreateValueLabel("Not Configured", Color.Gray);
        layout.Controls.Add(_scaleConnectionLabel, 1, row++);

        // Scale Host
        layout.Controls.Add(CreateLabel("Scale Address:"), 0, row);
        _scaleHostLabel = CreateValueLabel("Not configured", Color.Gray);
        layout.Controls.Add(_scaleHostLabel, 1, row++);

        // Protocol
        layout.Controls.Add(CreateLabel("Protocol:"), 0, row);
        _scaleProtocolLabel = CreateValueLabel("Not configured", Color.Gray);
        layout.Controls.Add(_scaleProtocolLabel, 1, row++);

        // Last Weight
        layout.Controls.Add(CreateLabel("Last Weight:"), 0, row);
        _lastWeightLabel = CreateValueLabel("--- lb", Color.Gray);
        _lastWeightLabel.Font = new Font("Segoe UI", 14F, FontStyle.Bold);
        layout.Controls.Add(_lastWeightLabel, 1, row++);

        // Last Update
        layout.Controls.Add(CreateLabel("Last Update:"), 0, row);
        _lastUpdateLabel = CreateValueLabel("Never", Color.Gray);
        layout.Controls.Add(_lastUpdateLabel, 1, row++);

        // Data Flow Indicator
        layout.Controls.Add(CreateLabel("Data Flow:"), 0, row);
        var flowPanel = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        _dataFlowIndicator = new ProgressBar
        {
            Width = 200,
            Height = 20,
            Style = ProgressBarStyle.Continuous,
            Maximum = 100,
            Value = 0
        };
        var flowLabel = new Label { Text = "No data", AutoSize = true, Padding = new Padding(5, 3, 0, 0) };
        flowLabel.Name = "flowLabel";
        flowPanel.Controls.Add(_dataFlowIndicator);
        flowPanel.Controls.Add(flowLabel);
        layout.Controls.Add(flowPanel, 1, row++);

        // Refresh button
        _refreshButton = new Button
        {
            Text = "Refresh Status",
            Width = 120,
            Margin = new Padding(0, 10, 0, 0)
        };
        _refreshButton.Click += Refresh_Click;
        layout.Controls.Add(_refreshButton, 0, row);
        layout.SetColumnSpan(_refreshButton, 2);

        panel.Controls.Add(layout);
        return panel;
    }

    private Label CreateLabel(string text)
    {
        return new Label
        {
            Text = text,
            AutoSize = true,
            Font = new Font("Segoe UI", 9F),
            Padding = new Padding(0, 5, 0, 5)
        };
    }

    private Label CreateValueLabel(string text, Color color)
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

    private void OnMessageReceived(object? sender, IpcMessage message)
    {
        if (InvokeRequired)
        {
            Invoke(() => OnMessageReceived(sender, message));
            return;
        }

        _messageCount++;
        _lastDataReceived = DateTime.Now;

        // Update data flow indicator
        _dataFlowIndicator.Value = Math.Min(100, _dataFlowIndicator.Value + 20);

        // Handle weight readings
        if (message.MessageType == IpcMessageType.WeightReading && !string.IsNullOrEmpty(message.Payload))
        {
            try
            {
                var reading = JsonSerializer.Deserialize<WeightReadingData>(message.Payload);
                if (reading != null)
                {
                    _lastWeightLabel.Text = $"{reading.Weight:F0} {reading.Unit}";
                    _lastWeightLabel.ForeColor = Color.Green;
                    _lastUpdateLabel.Text = DateTime.Now.ToString("HH:mm:ss.fff");
                    _lastUpdateLabel.ForeColor = Color.Green;

                    _scaleConnectionLabel.Text = "● Receiving Data";
                    _scaleConnectionLabel.ForeColor = Color.Green;
                }
            }
            catch { }
        }
    }

    private void UpdateTimer_Tick(object? sender, EventArgs e)
    {
        // Check Windows service status
        UpdateServiceStatus();

        // Check IPC connection
        UpdateIpcStatus();

        // Update scale config display
        UpdateScaleConfig();

        // Decay data flow indicator
        if (_dataFlowIndicator.Value > 0)
        {
            _dataFlowIndicator.Value = Math.Max(0, _dataFlowIndicator.Value - 5);
        }

        // Update flow label
        var flowLabel = this.Controls.Find("flowLabel", true).FirstOrDefault() as Label;
        if (flowLabel != null)
        {
            var timeSinceData = DateTime.Now - _lastDataReceived;
            if (_lastDataReceived == DateTime.MinValue)
            {
                flowLabel.Text = "No data received";
            }
            else if (timeSinceData.TotalSeconds < 2)
            {
                flowLabel.Text = $"Active ({_messageCount} msgs)";
            }
            else
            {
                flowLabel.Text = $"Idle ({timeSinceData.TotalSeconds:F0}s ago)";
            }
        }
    }

    private void UpdateServiceStatus()
    {
        try
        {
            using var sc = new ServiceController("ScaleStreamerService");
            switch (sc.Status)
            {
                case ServiceControllerStatus.Running:
                    _serviceStatusLabel.Text = "● Running";
                    _serviceStatusLabel.ForeColor = Color.Green;
                    _startServiceButton.Enabled = false;
                    _stopServiceButton.Enabled = true;
                    break;
                case ServiceControllerStatus.Stopped:
                    _serviceStatusLabel.Text = "● Stopped";
                    _serviceStatusLabel.ForeColor = Color.Red;
                    _startServiceButton.Enabled = true;
                    _stopServiceButton.Enabled = false;
                    break;
                case ServiceControllerStatus.StartPending:
                    _serviceStatusLabel.Text = "● Starting...";
                    _serviceStatusLabel.ForeColor = Color.Orange;
                    break;
                case ServiceControllerStatus.StopPending:
                    _serviceStatusLabel.Text = "● Stopping...";
                    _serviceStatusLabel.ForeColor = Color.Orange;
                    break;
                default:
                    _serviceStatusLabel.Text = $"● {sc.Status}";
                    _serviceStatusLabel.ForeColor = Color.Orange;
                    break;
            }
        }
        catch (Exception ex)
        {
            _serviceStatusLabel.Text = "● Not Installed";
            _serviceStatusLabel.ForeColor = Color.Red;
            _log.Debug("Service check error: {Message}", ex.Message);
        }
    }

    private void UpdateIpcStatus()
    {
        if (_ipcClient.IsConnected)
        {
            _ipcConnectionLabel.Text = "● Connected";
            _ipcConnectionLabel.ForeColor = Color.Green;
        }
        else
        {
            _ipcConnectionLabel.Text = "● Disconnected";
            _ipcConnectionLabel.ForeColor = Color.Red;
        }
    }

    private void UpdateScaleConfig()
    {
        var settings = AppSettings.Instance.ScaleConnection;

        var isSerial = settings.ConnectionType == "RS232" || settings.ConnectionType == "RS485" || settings.ConnectionType == "ModbusRTU";
        var isConfigured = isSerial
            ? !string.IsNullOrEmpty(settings.ComPort)
            : !string.IsNullOrEmpty(settings.Host);

        if (!isConfigured)
        {
            _scaleHostLabel.Text = "Not configured - use Connection tab";
            _scaleHostLabel.ForeColor = Color.Orange;
            _scaleConnectionLabel.Text = "● Not Configured";
            _scaleConnectionLabel.ForeColor = Color.Gray;
        }
        else
        {
            _scaleHostLabel.Text = isSerial
                ? $"{settings.ComPort} @ {settings.BaudRate} baud ({settings.ConnectionType})"
                : $"{settings.Host}:{settings.Port} ({settings.ConnectionType})";
            _scaleHostLabel.ForeColor = Color.Black;

            // If we haven't received data recently, show as waiting
            if (_lastDataReceived == DateTime.MinValue || (DateTime.Now - _lastDataReceived).TotalSeconds > 10)
            {
                _scaleConnectionLabel.Text = "● Waiting for data...";
                _scaleConnectionLabel.ForeColor = Color.Orange;
            }
        }

        _scaleProtocolLabel.Text = settings.Protocol;
        _scaleProtocolLabel.ForeColor = string.IsNullOrEmpty(settings.Protocol) ? Color.Gray : Color.Black;
    }

    public void HandleConnectionStatus(IpcMessage message)
    {
        if (InvokeRequired)
        {
            Invoke(() => HandleConnectionStatus(message));
            return;
        }

        _log.Debug("StatusTab received connection status: {Payload}", message.Payload);

        if (message.Payload?.Contains("Connected") == true)
        {
            _scaleConnectionLabel.Text = "● Connected";
            _scaleConnectionLabel.ForeColor = Color.Blue;
        }
        else if (message.Payload?.Contains("Disconnected") == true)
        {
            _scaleConnectionLabel.Text = "● Disconnected";
            _scaleConnectionLabel.ForeColor = Color.Red;
        }
        else if (message.Payload?.Contains("Error") == true)
        {
            _scaleConnectionLabel.Text = "● Connection Error";
            _scaleConnectionLabel.ForeColor = Color.Red;
        }
    }

    private void Refresh_Click(object? sender, EventArgs e)
    {
        _refreshButton.Enabled = false;
        try
        {
            UpdateServiceStatus();
            UpdateIpcStatus();
            UpdateScaleConfig();

            // Request scale status from service via IPC
            if (_ipcClient.IsConnected)
            {
                _ = RequestScaleStatusAsync();
            }
        }
        finally
        {
            _refreshButton.Enabled = true;
        }
    }

    private async Task RequestScaleStatusAsync()
    {
        try
        {
            var command = new IpcCommand
            {
                MessageType = IpcMessageType.GetAllStatuses
            };

            var response = await _ipcClient.SendCommandWithResponseAsync(command, timeoutMs: 3000);
            if (response != null && response.Success && !string.IsNullOrEmpty(response.Payload))
            {
                _log.Debug("Received scale statuses: {Payload}", response.Payload);
                // Parse and display scale statuses
            }
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "Failed to request scale status");
        }
    }

    private void CheckServicePermissions()
    {
        Task.Run(() =>
        {
            var hasPermission = ServiceControlHelper.HasServiceControlPermission();
            Invoke(() =>
            {
                if (hasPermission)
                {
                    _permissionStatusLabel.Text = "You can control the service without elevation";
                    _permissionStatusLabel.ForeColor = Color.Green;
                    _grantPermissionsButton.Visible = false;
                }
                else
                {
                    _permissionStatusLabel.Text = "UAC required - click to grant permissions once:";
                    _permissionStatusLabel.ForeColor = Color.Orange;
                    _grantPermissionsButton.Visible = true;
                }
            });
        });
    }

    private async void GrantPermissions_Click(object? sender, EventArgs e)
    {
        _grantPermissionsButton.Enabled = false;
        _permissionStatusLabel.Text = "Granting permissions (UAC prompt)...";

        try
        {
            var success = await ServiceControlHelper.GrantServiceControlPermissionAsync();
            if (success)
            {
                _permissionStatusLabel.Text = "Permissions granted! No more UAC prompts needed.";
                _permissionStatusLabel.ForeColor = Color.Green;
                _grantPermissionsButton.Visible = false;

                MessageBox.Show(
                    "Service control permissions granted!\n\nYou can now start, stop, and restart the service without UAC prompts.",
                    "Success",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            else
            {
                _permissionStatusLabel.Text = "Failed to grant permissions";
                _permissionStatusLabel.ForeColor = Color.Red;
                _grantPermissionsButton.Enabled = true;
            }
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Failed to grant permissions");
            _permissionStatusLabel.Text = "Error granting permissions";
            _permissionStatusLabel.ForeColor = Color.Red;
            _grantPermissionsButton.Enabled = true;
        }
    }

    private async void StartService_Click(object? sender, EventArgs e)
    {
        try
        {
            _startServiceButton.Enabled = false;
            _startServiceButton.Text = "Starting...";

            var result = await ServiceControlHelper.StartServiceAsync();

            if (!result.Success)
            {
                MessageBox.Show(result.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            else if (result.RequiredElevation)
            {
                // Offer to grant permissions
                var answer = MessageBox.Show(
                    "The service was started, but UAC was required.\n\nWould you like to grant permissions so UAC isn't needed next time?",
                    "Grant Permissions?",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (answer == DialogResult.Yes)
                {
                    GrantPermissions_Click(null, EventArgs.Empty);
                }
            }

            await Task.Delay(1000);
            UpdateServiceStatus();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to start service: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _startServiceButton.Text = "Start Service";
            _startServiceButton.Enabled = true;
        }
    }

    private async void StopService_Click(object? sender, EventArgs e)
    {
        var confirm = MessageBox.Show(
            "Stop the Scale Streamer Service?\nAll scale connections will be terminated.",
            "Confirm",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (confirm == DialogResult.Yes)
        {
            try
            {
                _stopServiceButton.Enabled = false;
                _stopServiceButton.Text = "Stopping...";

                var result = await ServiceControlHelper.StopServiceAsync();

                if (!result.Success)
                {
                    MessageBox.Show(result.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                else if (result.RequiredElevation)
                {
                    var answer = MessageBox.Show(
                        "The service was stopped, but UAC was required.\n\nWould you like to grant permissions so UAC isn't needed next time?",
                        "Grant Permissions?",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);

                    if (answer == DialogResult.Yes)
                    {
                        GrantPermissions_Click(null, EventArgs.Empty);
                    }
                }

                await Task.Delay(1000);
                UpdateServiceStatus();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to stop service: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _stopServiceButton.Text = "Stop Service";
                _stopServiceButton.Enabled = true;
            }
        }
    }

    private async void RestartService_Click(object? sender, EventArgs e)
    {
        var confirm = MessageBox.Show(
            "Restart the Scale Streamer Service?",
            "Confirm",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (confirm == DialogResult.Yes)
        {
            try
            {
                _restartServiceButton.Enabled = false;
                _restartServiceButton.Text = "Restarting...";

                var result = await ServiceControlHelper.RestartServiceAsync();

                if (!result.Success)
                {
                    MessageBox.Show(result.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                else if (result.RequiredElevation)
                {
                    var answer = MessageBox.Show(
                        "The service was restarted, but UAC was required.\n\nWould you like to grant permissions so UAC isn't needed next time?",
                        "Grant Permissions?",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);

                    if (answer == DialogResult.Yes)
                    {
                        GrantPermissions_Click(null, EventArgs.Empty);
                    }
                }

                await Task.Delay(1000);
                UpdateServiceStatus();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to restart service: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _restartServiceButton.Text = "Restart";
                _restartServiceButton.Enabled = true;
            }
        }
    }

    private void OpenLogs_Click(object? sender, EventArgs e)
    {
        try
        {
            var logPath = @"C:\ProgramData\ScaleStreamer\logs";
            if (Directory.Exists(logPath))
            {
                Process.Start("explorer.exe", logPath);
            }
            else
            {
                MessageBox.Show("Log folder not found.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to open logs folder: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _updateTimer?.Stop();
            _updateTimer?.Dispose();
            _ipcClient.MessageReceived -= OnMessageReceived;
        }
        base.Dispose(disposing);
    }

    // Helper class for deserializing weight readings
    private class WeightReadingData
    {
        public double Weight { get; set; }
        public string Unit { get; set; } = "lb";
    }
}
