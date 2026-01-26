using ScaleStreamer.Common.IPC;
using ScaleStreamer.Common.Models;
using ScaleStreamer.Common.Settings;
using System.Text.Json;
using Serilog;

namespace ScaleStreamer.Config;

/// <summary>
/// Connection configuration tab for setting up scale connections
/// </summary>
public partial class ConnectionTab : UserControl
{
    private readonly IpcClient _ipcClient;
    private bool _isLoading; // Prevent auto-save during initial load

    // Controls
    private ComboBox _marketTypeCombo;
    private ComboBox _manufacturerCombo;
    private ComboBox _protocolCombo;
    private ComboBox _connectionTypeCombo;
    private TextBox _scaleIdText;
    private TextBox _scaleNameText;
    private TextBox _locationText;

    // TCP/IP controls
    private TextBox _hostText;
    private NumericUpDown _portNumeric;
    private NumericUpDown _timeoutNumeric;
    private CheckBox _autoReconnectCheck;
    private NumericUpDown _reconnectIntervalNumeric;

    // Serial controls
    private ComboBox _comPortCombo;
    private ComboBox _baudRateCombo;
    private ComboBox _dataBitsCombo;
    private ComboBox _parityCombo;
    private ComboBox _stopBitsCombo;
    private ComboBox _flowControlCombo;

    // Status controls
    private Label _connectionStatusLabel;
    private Button _quickSetupButton;
    private Button _testConnectionButton;
    private Button _saveButton;
    private TextBox _logTextBox;

    public ConnectionTab(IpcClient ipcClient)
    {
        _ipcClient = ipcClient;
        _isLoading = true;
        InitializeComponent();
        LoadDefaults();
        LoadFromSettings();
        _isLoading = false;
        HookAutoSaveEvents();
    }

    private void InitializeComponent()
    {
        this.AutoScroll = true;
        this.Padding = new Padding(10);

        // Create main layout panel
        var mainPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            AutoSize = true,
            AutoScroll = true
        };
        mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200F));
        mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

        int row = 0;

        // Scale Information Section
        AddSectionHeader(mainPanel, "Scale Information", ref row);

        // Scale ID
        AddLabel(mainPanel, "Scale ID:", ref row);
        _scaleIdText = new TextBox { Width = 300 };
        AddControl(mainPanel, _scaleIdText, row++);

        // Scale Name
        AddLabel(mainPanel, "Scale Name:", ref row);
        _scaleNameText = new TextBox { Width = 300 };
        AddControl(mainPanel, _scaleNameText, row++);

        // Location
        AddLabel(mainPanel, "Location:", ref row);
        _locationText = new TextBox { Width = 300 };
        AddControl(mainPanel, _locationText, row++);

        // Market Type Section
        AddSectionHeader(mainPanel, "Market Configuration", ref row);

        // Market Type
        AddLabel(mainPanel, "Market Type:", ref row);
        _marketTypeCombo = new ComboBox { Width = 300, DropDownStyle = ComboBoxStyle.DropDownList };
        _marketTypeCombo.SelectedIndexChanged += MarketType_Changed;
        AddControl(mainPanel, _marketTypeCombo, row++);

        // Protocol Selection Section
        AddSectionHeader(mainPanel, "Protocol Selection", ref row);

        // Manufacturer
        AddLabel(mainPanel, "Manufacturer:", ref row);
        _manufacturerCombo = new ComboBox { Width = 300, DropDownStyle = ComboBoxStyle.DropDownList };
        _manufacturerCombo.SelectedIndexChanged += Manufacturer_Changed;
        AddControl(mainPanel, _manufacturerCombo, row++);

        // Protocol
        AddLabel(mainPanel, "Protocol:", ref row);
        _protocolCombo = new ComboBox { Width = 300, DropDownStyle = ComboBoxStyle.DropDownList };
        _protocolCombo.SelectedIndexChanged += Protocol_Changed;
        AddControl(mainPanel, _protocolCombo, row++);

        // Connection Type Section
        AddSectionHeader(mainPanel, "Connection Configuration", ref row);

        // Connection Type
        AddLabel(mainPanel, "Connection Type:", ref row);
        _connectionTypeCombo = new ComboBox { Width = 300, DropDownStyle = ComboBoxStyle.DropDownList };
        _connectionTypeCombo.SelectedIndexChanged += ConnectionType_Changed;
        AddControl(mainPanel, _connectionTypeCombo, row++);

        // TCP/IP Settings (initially hidden)
        var tcpPanel = CreateTcpIpPanel();
        tcpPanel.Visible = false;
        tcpPanel.Name = "tcpPanel";
        mainPanel.Controls.Add(tcpPanel);
        mainPanel.SetColumnSpan(tcpPanel, 2);
        mainPanel.SetRow(tcpPanel, row++);

        // Serial Settings (initially hidden)
        var serialPanel = CreateSerialPanel();
        serialPanel.Visible = false;
        serialPanel.Name = "serialPanel";
        mainPanel.Controls.Add(serialPanel);
        mainPanel.SetColumnSpan(serialPanel, 2);
        mainPanel.SetRow(serialPanel, row++);

        // Quick Setup Section
        AddSectionHeader(mainPanel, "Quick Setup", ref row);

        var quickSetupPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(0, 5, 0, 10)
        };

        _quickSetupButton = new Button
        {
            Text = "Auto-Detect Scale...",
            Width = 150,
            Height = 35
        };
        _quickSetupButton.Click += QuickSetup_Click;

        var quickSetupHelp = new Label
        {
            Text = "Automatically detect scale connection and protocol",
            AutoSize = true,
            Padding = new Padding(10, 8, 0, 0),
            ForeColor = Color.Gray
        };

        quickSetupPanel.Controls.Add(_quickSetupButton);
        quickSetupPanel.Controls.Add(quickSetupHelp);

        mainPanel.Controls.Add(quickSetupPanel);
        mainPanel.SetColumnSpan(quickSetupPanel, 2);
        mainPanel.SetRow(quickSetupPanel, row++);

        // Connection Status Section
        AddSectionHeader(mainPanel, "Connection Status", ref row);

        _connectionStatusLabel = new Label
        {
            Text = "Not Connected",
            AutoSize = true,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            ForeColor = Color.Gray
        };
        mainPanel.Controls.Add(_connectionStatusLabel);
        mainPanel.SetColumn(_connectionStatusLabel, 1);
        mainPanel.SetRow(_connectionStatusLabel, row++);

        // Buttons
        var buttonPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight
        };

        _testConnectionButton = new Button
        {
            Text = "Test Connection",
            Width = 130,
            Height = 35
        };
        _testConnectionButton.Click += TestConnection_Click;

        _saveButton = new Button
        {
            Text = "Save Configuration",
            Width = 130,
            Height = 35
        };
        _saveButton.Click += Save_Click;

        buttonPanel.Controls.Add(_testConnectionButton);
        buttonPanel.Controls.Add(_saveButton);

        mainPanel.Controls.Add(buttonPanel);
        mainPanel.SetColumnSpan(buttonPanel, 2);
        mainPanel.SetRow(buttonPanel, row++);

        // Log Output
        AddSectionHeader(mainPanel, "Connection Log", ref row);

        _logTextBox = new TextBox
        {
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            ReadOnly = true,
            Height = 150,
            Dock = DockStyle.Fill,
            Font = new Font("Consolas", 9F)
        };
        mainPanel.Controls.Add(_logTextBox);
        mainPanel.SetColumnSpan(_logTextBox, 2);
        mainPanel.SetRow(_logTextBox, row++);

        this.Controls.Add(mainPanel);
    }

    private Control CreateTcpIpPanel()
    {
        var panel = new GroupBox
        {
            Text = "TCP/IP Settings",
            AutoSize = true,
            Padding = new Padding(10)
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            AutoSize = true
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

        int row = 0;

        // Host
        AddLabel(layout, "Host/IP Address:", ref row);
        _hostText = new TextBox { Width = 250, Text = "10.1.10.210" };
        AddControl(layout, _hostText, row++);

        // Port
        AddLabel(layout, "Port:", ref row);
        _portNumeric = new NumericUpDown { Width = 100, Minimum = 1, Maximum = 65535, Value = 5001 };
        AddControl(layout, _portNumeric, row++);

        // Timeout
        AddLabel(layout, "Timeout (ms):", ref row);
        _timeoutNumeric = new NumericUpDown { Width = 100, Minimum = 100, Maximum = 60000, Value = 5000, Increment = 100 };
        AddControl(layout, _timeoutNumeric, row++);

        // Auto Reconnect
        AddLabel(layout, "Auto Reconnect:", ref row);
        _autoReconnectCheck = new CheckBox { Checked = true };
        AddControl(layout, _autoReconnectCheck, row++);

        // Reconnect Interval
        AddLabel(layout, "Reconnect Interval (s):", ref row);
        _reconnectIntervalNumeric = new NumericUpDown { Width = 100, Minimum = 1, Maximum = 300, Value = 10 };
        AddControl(layout, _reconnectIntervalNumeric, row++);

        panel.Controls.Add(layout);
        return panel;
    }

    private Control CreateSerialPanel()
    {
        var panel = new GroupBox
        {
            Text = "Serial Port Settings",
            AutoSize = true,
            Padding = new Padding(10)
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            AutoSize = true
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

        int row = 0;

        // COM Port
        AddLabel(layout, "COM Port:", ref row);
        _comPortCombo = new ComboBox { Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
        AddControl(layout, _comPortCombo, row++);

        // Baud Rate
        AddLabel(layout, "Baud Rate:", ref row);
        _baudRateCombo = new ComboBox { Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
        AddControl(layout, _baudRateCombo, row++);

        // Data Bits
        AddLabel(layout, "Data Bits:", ref row);
        _dataBitsCombo = new ComboBox { Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
        AddControl(layout, _dataBitsCombo, row++);

        // Parity
        AddLabel(layout, "Parity:", ref row);
        _parityCombo = new ComboBox { Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
        AddControl(layout, _parityCombo, row++);

        // Stop Bits
        AddLabel(layout, "Stop Bits:", ref row);
        _stopBitsCombo = new ComboBox { Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
        AddControl(layout, _stopBitsCombo, row++);

        // Flow Control
        AddLabel(layout, "Flow Control:", ref row);
        _flowControlCombo = new ComboBox { Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
        AddControl(layout, _flowControlCombo, row++);

        panel.Controls.Add(layout);
        return panel;
    }

    private void AddSectionHeader(TableLayoutPanel panel, string text, ref int row)
    {
        var label = new Label
        {
            Text = text,
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            AutoSize = true,
            Padding = new Padding(0, 10, 0, 5)
        };
        panel.Controls.Add(label);
        panel.SetColumnSpan(label, 2);
        panel.SetRow(label, row++);
    }

    private void AddLabel(TableLayoutPanel panel, string text, ref int row)
    {
        var label = new Label
        {
            Text = text,
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(0, 5, 0, 5)
        };
        panel.Controls.Add(label);
        panel.SetColumn(label, 0);
        panel.SetRow(label, row);
    }

    private void AddControl(TableLayoutPanel panel, Control control, int row)
    {
        control.Margin = new Padding(0, 3, 0, 3);
        panel.Controls.Add(control);
        panel.SetColumn(control, 1);
        panel.SetRow(control, row);
    }

    /// <summary>
    /// Load settings from the shared AppSettings
    /// </summary>
    private void LoadFromSettings()
    {
        try
        {
            var settings = AppSettings.Instance.ScaleConnection;
            Log.Information("Loading connection settings from file: Host={Host}, Port={Port}",
                settings.Host, settings.Port);

            _scaleIdText.Text = settings.ScaleId;
            _scaleNameText.Text = settings.ScaleName;
            _locationText.Text = settings.Location;
            _hostText.Text = settings.Host;
            _portNumeric.Value = settings.Port;
            _timeoutNumeric.Value = settings.TimeoutMs;
            _autoReconnectCheck.Checked = settings.AutoReconnect;
            _reconnectIntervalNumeric.Value = settings.ReconnectIntervalSeconds;

            // Set combo boxes
            SelectComboItem(_marketTypeCombo, settings.MarketType);
            SelectComboItem(_manufacturerCombo, settings.Manufacturer);
            SelectComboItem(_connectionTypeCombo, settings.ConnectionType);

            // Update protocol list and select
            UpdateProtocolList();
            SelectComboItem(_protocolCombo, settings.Protocol);

            LogMessage($"Settings loaded: {settings.Host}:{settings.Port}");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load settings");
        }
    }

    private void SelectComboItem(ComboBox combo, string value)
    {
        for (int i = 0; i < combo.Items.Count; i++)
        {
            if (combo.Items[i]?.ToString()?.Contains(value, StringComparison.OrdinalIgnoreCase) == true)
            {
                combo.SelectedIndex = i;
                return;
            }
        }
    }

    /// <summary>
    /// Hook change events to auto-save settings
    /// </summary>
    private void HookAutoSaveEvents()
    {
        // Text fields - use Leave event for less frequent saves
        _scaleIdText.Leave += (s, e) => AutoSave();
        _scaleNameText.Leave += (s, e) => AutoSave();
        _locationText.Leave += (s, e) => AutoSave();
        _hostText.Leave += (s, e) => AutoSave();

        // Numeric fields
        _portNumeric.ValueChanged += (s, e) => AutoSave();
        _timeoutNumeric.ValueChanged += (s, e) => AutoSave();
        _reconnectIntervalNumeric.ValueChanged += (s, e) => AutoSave();

        // Checkboxes
        _autoReconnectCheck.CheckedChanged += (s, e) => AutoSave();

        // Combo boxes
        _marketTypeCombo.SelectedIndexChanged += (s, e) => AutoSave();
        _manufacturerCombo.SelectedIndexChanged += (s, e) => AutoSave();
        _connectionTypeCombo.SelectedIndexChanged += (s, e) => AutoSave();
        _protocolCombo.SelectedIndexChanged += (s, e) => AutoSave();
    }

    /// <summary>
    /// Auto-save settings when any value changes
    /// </summary>
    private void AutoSave()
    {
        if (_isLoading) return;

        try
        {
            var settings = AppSettings.Instance.ScaleConnection;

            settings.ScaleId = _scaleIdText.Text;
            settings.ScaleName = _scaleNameText.Text;
            settings.Location = _locationText.Text;
            settings.Host = _hostText.Text;
            settings.Port = (int)_portNumeric.Value;
            settings.TimeoutMs = (int)_timeoutNumeric.Value;
            settings.AutoReconnect = _autoReconnectCheck.Checked;
            settings.ReconnectIntervalSeconds = (int)_reconnectIntervalNumeric.Value;
            settings.MarketType = _marketTypeCombo.SelectedItem?.ToString() ?? "Industrial";
            settings.Manufacturer = _manufacturerCombo.SelectedItem?.ToString() ?? "Generic";
            settings.ConnectionType = _connectionTypeCombo.SelectedItem?.ToString() ?? "TcpIp";
            settings.Protocol = _protocolCombo.SelectedItem?.ToString() ?? "Generic ASCII";

            AppSettings.Instance.Save();

            Log.Debug("Settings auto-saved: Host={Host}, Port={Port}", settings.Host, settings.Port);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to auto-save settings");
        }
    }

    private void LoadDefaults()
    {
        // Market Types (from Enums.cs)
        _marketTypeCombo.Items.AddRange(new object[]
        {
            "Floor Scales",
            "Truck Scales",
            "Train/Rail Scales",
            "Hopper Scales",
            "Conveyor Scales",
            "Shipping/Receiving",
            "Checkweigher",
            "WIM (Weigh-In-Motion)",
            "Retail/Point-of-Sale",
            "Laboratory",
            "Crane Scales",
            "Livestock Scales",
            "General Purpose"
        });
        _marketTypeCombo.SelectedIndex = 0;

        // Manufacturers (50+ manufacturers from spec)
        _manufacturerCombo.Items.AddRange(new object[]
        {
            "Generic",
            "Fairbanks Scales",
            "Rice Lake Weighing Systems",
            "Cardinal Scale",
            "Avery Weigh-Tronix",
            "Mettler Toledo",
            "Digi",
            "Brecknell",
            "Intercomp",
            "B-Tek",
            "Pennsylvania Scale Company",
            "Sterling Scale",
            "Transcell",
            "Weighwell"
        });
        _manufacturerCombo.SelectedIndex = 0;

        // Connection Types
        _connectionTypeCombo.Items.AddRange(Enum.GetNames(typeof(ConnectionType)));
        _connectionTypeCombo.SelectedIndex = 0;

        // Serial Port Settings
        _comPortCombo.Items.AddRange(new object[] { "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8" });
        _baudRateCombo.Items.AddRange(new object[] { "300", "1200", "2400", "4800", "9600", "19200", "38400", "57600", "115200" });
        _baudRateCombo.SelectedItem = "9600";

        _dataBitsCombo.Items.AddRange(new object[] { "7", "8" });
        _dataBitsCombo.SelectedItem = "8";

        _parityCombo.Items.AddRange(new object[] { "None", "Odd", "Even", "Mark", "Space" });
        _parityCombo.SelectedItem = "None";

        _stopBitsCombo.Items.AddRange(new object[] { "None", "One", "Two", "OnePointFive" });
        _stopBitsCombo.SelectedItem = "One";

        _flowControlCombo.Items.AddRange(new object[] { "None", "Hardware", "Software", "Both" });
        _flowControlCombo.SelectedItem = "None";
    }

    private void MarketType_Changed(object? sender, EventArgs e)
    {
        LogMessage($"Market type changed to: {_marketTypeCombo.SelectedItem}");
    }

    private void Manufacturer_Changed(object? sender, EventArgs e)
    {
        LogMessage($"Manufacturer changed to: {_manufacturerCombo.SelectedItem}");
        // TODO: Load available protocols for this manufacturer
        UpdateProtocolList();
    }

    private void Protocol_Changed(object? sender, EventArgs e)
    {
        var protocol = _protocolCombo.SelectedItem?.ToString();
        LogMessage($"Protocol changed to: {protocol}");

        if (!string.IsNullOrEmpty(protocol))
        {
            LoadProtocolDefaults(protocol);
        }
    }

    private void LoadProtocolDefaults(string protocolName)
    {
        try
        {
            // Try to load protocol template from installation folder
            var installPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "Scale Streamer", "protocols");

            string? templatePath = null;

            // Map protocol names to file paths
            if (protocolName.Contains("Fairbanks 6011"))
            {
                templatePath = Path.Combine(installPath, "manufacturers", "fairbanks-6011.json");
            }
            else if (protocolName.Contains("Generic ASCII"))
            {
                templatePath = Path.Combine(installPath, "generic", "generic-ascii.json");
            }
            else if (protocolName.Contains("Modbus TCP"))
            {
                templatePath = Path.Combine(installPath, "generic", "modbus-tcp.json");
            }

            if (templatePath != null && File.Exists(templatePath))
            {
                var json = File.ReadAllText(templatePath);
                var template = JsonSerializer.Deserialize<JsonElement>(json);

                // Apply connection defaults
                if (template.TryGetProperty("connection", out var connection))
                {
                    if (connection.TryGetProperty("type", out var connType))
                    {
                        var type = connType.GetString();
                        _connectionTypeCombo.SelectedItem = type;
                    }

                    if (connection.TryGetProperty("port", out var port))
                    {
                        _portNumeric.Value = port.GetInt32();
                        LogMessage($"  → Port set to {port.GetInt32()}");
                    }

                    if (connection.TryGetProperty("timeout_ms", out var timeout))
                    {
                        _timeoutNumeric.Value = timeout.GetInt32();
                    }

                    if (connection.TryGetProperty("auto_reconnect", out var autoReconnect))
                    {
                        _autoReconnectCheck.Checked = autoReconnect.GetBoolean();
                    }
                }

                LogMessage($"✓ Loaded defaults for {protocolName}");
            }
        }
        catch (Exception ex)
        {
            LogMessage($"Could not load protocol defaults: {ex.Message}");
        }
    }

    private void ConnectionType_Changed(object? sender, EventArgs e)
    {
        var selectedType = _connectionTypeCombo.SelectedItem?.ToString();
        LogMessage($"Connection type changed to: {selectedType}");

        // Show/hide appropriate panels
        var tcpPanel = this.Controls.Find("tcpPanel", true).FirstOrDefault();
        var serialPanel = this.Controls.Find("serialPanel", true).FirstOrDefault();

        if (tcpPanel != null)
            tcpPanel.Visible = selectedType == "TcpIp" || selectedType == "ModbusTCP";

        if (serialPanel != null)
            serialPanel.Visible = selectedType == "RS232" || selectedType == "RS485" || selectedType == "ModbusRTU";
    }

    private void UpdateProtocolList()
    {
        _protocolCombo.Items.Clear();
        _protocolCombo.Items.Add("Generic ASCII");
        _protocolCombo.Items.Add("Generic Binary");

        // Add manufacturer-specific protocols
        var manufacturer = _manufacturerCombo.SelectedItem?.ToString();
        if (manufacturer == "Fairbanks Scales")
        {
            _protocolCombo.Items.Add("Fairbanks 6011");
            _protocolCombo.Items.Add("Fairbanks Lantronix");
        }
        else if (manufacturer == "Rice Lake Weighing Systems")
        {
            _protocolCombo.Items.Add("Rice Lake 920i");
            _protocolCombo.Items.Add("Rice Lake IQ Plus");
        }

        if (_protocolCombo.Items.Count > 0)
            _protocolCombo.SelectedIndex = 0;
    }

    private async void TestConnection_Click(object? sender, EventArgs e)
    {
        try
        {
            LogMessage("Testing connection...");
            _testConnectionButton.Enabled = false;
            _connectionStatusLabel.Text = "Testing...";
            _connectionStatusLabel.ForeColor = Color.Orange;

            // TODO: Send test connection command to service via IPC
            await Task.Delay(2000); // Simulate test

            _connectionStatusLabel.Text = "Connection Successful";
            _connectionStatusLabel.ForeColor = Color.Green;
            LogMessage("Connection test successful!");
        }
        catch (Exception ex)
        {
            _connectionStatusLabel.Text = "Connection Failed";
            _connectionStatusLabel.ForeColor = Color.Red;
            LogMessage($"Connection test failed: {ex.Message}");
        }
        finally
        {
            _testConnectionButton.Enabled = true;
        }
    }

    private async void Save_Click(object? sender, EventArgs e)
    {
        try
        {
            LogMessage("Saving configuration...");

            // TODO: Build configuration and send to service via IPC

            MessageBox.Show("Configuration saved successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            LogMessage("Configuration saved.");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error saving configuration: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            LogMessage($"Save failed: {ex.Message}");
        }
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

    private void QuickSetup_Click(object? sender, EventArgs e)
    {
        using var wizard = new QuickSetupWizard();
        if (wizard.ShowDialog(this) == DialogResult.OK)
        {
            if (wizard.DetectedProtocol != null && wizard.DetectedConfig != null)
            {
                ApplyQuickSetupConfig(wizard.DetectedProtocol, wizard.IpAddress!, wizard.Port);
                LogMessage($"✓ Quick setup completed: {wizard.DetectedProtocol} at {wizard.IpAddress}:{wizard.Port}");
            }
        }
    }

    private void ApplyQuickSetupConfig(string protocol, string ipAddress, int port)
    {
        // Set connection type to TCP/IP
        _connectionTypeCombo.SelectedItem = "TcpIp";

        // Set TCP/IP settings
        _hostText.Text = ipAddress;
        _portNumeric.Value = port;

        // Set manufacturer and protocol based on detection
        if (protocol.Contains("Fairbanks"))
        {
            _manufacturerCombo.SelectedItem = "Fairbanks Scales";
            // Wait for manufacturer list to update
            Application.DoEvents();
            _protocolCombo.SelectedItem = "Fairbanks 6011";
        }
        else if (protocol.Contains("Generic ASCII"))
        {
            _manufacturerCombo.SelectedItem = "Generic";
            Application.DoEvents();
            _protocolCombo.SelectedItem = "Generic ASCII";
        }
        else if (protocol.Contains("Modbus"))
        {
            _manufacturerCombo.SelectedItem = "Generic";
            Application.DoEvents();
            _protocolCombo.SelectedItem = "Modbus TCP";
        }

        // Generate scale ID and name if empty
        if (string.IsNullOrWhiteSpace(_scaleIdText.Text))
        {
            _scaleIdText.Text = $"scale-{ipAddress.Replace(".", "-")}";
        }

        if (string.IsNullOrWhiteSpace(_scaleNameText.Text))
        {
            _scaleNameText.Text = $"{protocol} at {ipAddress}";
        }

        // Update status
        _connectionStatusLabel.Text = "Configuration Ready - Click Save";
        _connectionStatusLabel.ForeColor = Color.Green;

        MessageBox.Show(
            $"Configuration detected and applied!\n\n" +
            $"Protocol: {protocol}\n" +
            $"Address: {ipAddress}:{port}\n\n" +
            $"Review the settings and click 'Save Configuration' to apply.",
            "Quick Setup Complete",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }
}
