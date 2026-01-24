using ScaleStreamer.Config;
using ScaleStreamer.Core;
using System.IO.Ports;

namespace ScaleStreamer.App;

/// <summary>
/// Configuration dialog form.
/// </summary>
public class ConfigForm : Form
{
    private readonly AppSettings _settings;
    private readonly ConfigManager _configManager;

    // Connection tab controls
    private RadioButton _tcpRadio = null!;
    private RadioButton _serialRadio = null!;
    private TextBox _tcpHostBox = null!;
    private NumericUpDown _tcpPortBox = null!;
    private ComboBox _serialPortCombo = null!;
    private ComboBox _baudRateCombo = null!;

    // Stream tab controls
    private NumericUpDown _rtspPortBox = null!;
    private ComboBox _resolutionCombo = null!;
    private ComboBox _frameRateCombo = null!;

    // Display tab controls
    private TextBox _titleBox = null!;
    private ComboBox _unitCombo = null!;
    private CheckBox _showTimestampCheck = null!;
    private CheckBox _showStreamRateCheck = null!;
    private CheckBox _showTransmitIndicatorCheck = null!;
    private TextBox _customLabelBox = null!;

    // Other controls
    private CheckBox _autoStartCheck = null!;
    private Button _testButton = null!;
    private Label _statusLabel = null!;

    public ConfigForm(AppSettings settings, ConfigManager configManager)
    {
        _settings = settings;
        _configManager = configManager;

        InitializeComponent();
        LoadSettings();
    }

    private void InitializeComponent()
    {
        Text = "Scale Streamer Configuration";
        Size = new Size(450, 400);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;

        var tabControl = new TabControl
        {
            Dock = DockStyle.Top,
            Height = 280
        };

        tabControl.TabPages.Add(CreateConnectionTab());
        tabControl.TabPages.Add(CreateStreamTab());
        tabControl.TabPages.Add(CreateDisplayTab());

        Controls.Add(tabControl);

        // Auto-start checkbox
        _autoStartCheck = new CheckBox
        {
            Text = "Start streaming automatically",
            Location = new Point(20, 295),
            AutoSize = true
        };
        Controls.Add(_autoStartCheck);

        // Status label
        _statusLabel = new Label
        {
            Location = new Point(20, 325),
            Size = new Size(300, 20),
            ForeColor = Color.Gray
        };
        Controls.Add(_statusLabel);

        // Buttons
        _testButton = new Button
        {
            Text = "Test Connection",
            Location = new Point(120, 320),
            Size = new Size(100, 28)
        };
        _testButton.Click += TestConnection;
        Controls.Add(_testButton);

        var saveButton = new Button
        {
            Text = "Save",
            Location = new Point(240, 320),
            Size = new Size(80, 28),
            DialogResult = DialogResult.OK
        };
        saveButton.Click += SaveSettings;
        Controls.Add(saveButton);

        var cancelButton = new Button
        {
            Text = "Cancel",
            Location = new Point(330, 320),
            Size = new Size(80, 28),
            DialogResult = DialogResult.Cancel
        };
        Controls.Add(cancelButton);

        AcceptButton = saveButton;
        CancelButton = cancelButton;
    }

    private TabPage CreateConnectionTab()
    {
        var page = new TabPage("Connection");

        // Connection type
        var typeLabel = new Label { Text = "Connection Type:", Location = new Point(20, 20), AutoSize = true };
        page.Controls.Add(typeLabel);

        _tcpRadio = new RadioButton { Text = "TCP/IP", Location = new Point(20, 45), AutoSize = true };
        _tcpRadio.CheckedChanged += OnConnectionTypeChanged;
        page.Controls.Add(_tcpRadio);

        _serialRadio = new RadioButton { Text = "Serial (RS232)", Location = new Point(120, 45), AutoSize = true };
        _serialRadio.CheckedChanged += OnConnectionTypeChanged;
        page.Controls.Add(_serialRadio);

        // TCP settings
        var tcpGroup = new GroupBox { Text = "TCP/IP Settings", Location = new Point(20, 75), Size = new Size(380, 70) };

        var hostLabel = new Label { Text = "Host:", Location = new Point(10, 25), AutoSize = true };
        tcpGroup.Controls.Add(hostLabel);
        _tcpHostBox = new TextBox { Location = new Point(80, 22), Size = new Size(150, 23) };
        tcpGroup.Controls.Add(_tcpHostBox);

        var portLabel = new Label { Text = "Port:", Location = new Point(250, 25), AutoSize = true };
        tcpGroup.Controls.Add(portLabel);
        _tcpPortBox = new NumericUpDown { Location = new Point(290, 22), Size = new Size(70, 23), Minimum = 1, Maximum = 65535 };
        tcpGroup.Controls.Add(_tcpPortBox);

        page.Controls.Add(tcpGroup);

        // Serial settings
        var serialGroup = new GroupBox { Text = "Serial Settings", Location = new Point(20, 150), Size = new Size(380, 70) };

        var comLabel = new Label { Text = "Port:", Location = new Point(10, 25), AutoSize = true };
        serialGroup.Controls.Add(comLabel);
        _serialPortCombo = new ComboBox { Location = new Point(80, 22), Size = new Size(100, 23), DropDownStyle = ComboBoxStyle.DropDownList };
        _serialPortCombo.Items.AddRange(SerialScaleReader.GetAvailablePorts());
        serialGroup.Controls.Add(_serialPortCombo);

        var baudLabel = new Label { Text = "Baud:", Location = new Point(200, 25), AutoSize = true };
        serialGroup.Controls.Add(baudLabel);
        _baudRateCombo = new ComboBox { Location = new Point(250, 22), Size = new Size(100, 23), DropDownStyle = ComboBoxStyle.DropDownList };
        _baudRateCombo.Items.AddRange(new object[] { 9600, 19200, 38400, 57600, 115200 });
        serialGroup.Controls.Add(_baudRateCombo);

        page.Controls.Add(serialGroup);

        return page;
    }

    private TabPage CreateStreamTab()
    {
        var page = new TabPage("Stream");

        var rtspLabel = new Label { Text = "RTSP Port:", Location = new Point(20, 30), AutoSize = true };
        page.Controls.Add(rtspLabel);
        _rtspPortBox = new NumericUpDown { Location = new Point(120, 27), Size = new Size(80, 23), Minimum = 1, Maximum = 65535 };
        page.Controls.Add(_rtspPortBox);

        var resLabel = new Label { Text = "Resolution:", Location = new Point(20, 65), AutoSize = true };
        page.Controls.Add(resLabel);
        _resolutionCombo = new ComboBox { Location = new Point(120, 62), Size = new Size(120, 23), DropDownStyle = ComboBoxStyle.DropDownList };
        _resolutionCombo.Items.AddRange(new object[] { "640x480", "800x600", "1280x720", "1920x1080" });
        page.Controls.Add(_resolutionCombo);

        var fpsLabel = new Label { Text = "Frame Rate:", Location = new Point(20, 100), AutoSize = true };
        page.Controls.Add(fpsLabel);
        _frameRateCombo = new ComboBox { Location = new Point(120, 97), Size = new Size(80, 23), DropDownStyle = ComboBoxStyle.DropDownList };
        _frameRateCombo.Items.AddRange(new object[] { 15, 25, 30, 60 });
        page.Controls.Add(_frameRateCombo);

        var infoLabel = new Label
        {
            Text = "Stream URL will be:\nrtsp://127.0.0.1:<port>/scale\nhttp://127.0.0.1:8888/scale/ (HLS)",
            Location = new Point(20, 150),
            Size = new Size(350, 60),
            ForeColor = Color.Gray
        };
        page.Controls.Add(infoLabel);

        return page;
    }

    private TabPage CreateDisplayTab()
    {
        var page = new TabPage("Display");

        var titleLabel = new Label { Text = "Title:", Location = new Point(20, 25), AutoSize = true };
        page.Controls.Add(titleLabel);
        _titleBox = new TextBox { Location = new Point(120, 22), Size = new Size(200, 23) };
        page.Controls.Add(_titleBox);

        var unitLabel = new Label { Text = "Unit:", Location = new Point(20, 55), AutoSize = true };
        page.Controls.Add(unitLabel);
        _unitCombo = new ComboBox { Location = new Point(120, 52), Size = new Size(80, 23), DropDownStyle = ComboBoxStyle.DropDownList };
        _unitCombo.Items.AddRange(new object[] { "LB", "KG", "OZ", "G" });
        page.Controls.Add(_unitCombo);

        var customLabelLabel = new Label { Text = "Custom Label:", Location = new Point(20, 85), AutoSize = true };
        page.Controls.Add(customLabelLabel);
        _customLabelBox = new TextBox { Location = new Point(120, 82), Size = new Size(200, 23) };
        page.Controls.Add(_customLabelBox);

        var overlayGroup = new GroupBox { Text = "Overlay Options", Location = new Point(20, 115), Size = new Size(380, 100) };

        _showTimestampCheck = new CheckBox { Text = "Show date/time (top right)", Location = new Point(15, 22), AutoSize = true };
        overlayGroup.Controls.Add(_showTimestampCheck);

        _showStreamRateCheck = new CheckBox { Text = "Show stream rate KB/s (top left)", Location = new Point(15, 47), AutoSize = true };
        overlayGroup.Controls.Add(_showStreamRateCheck);

        _showTransmitIndicatorCheck = new CheckBox { Text = "Show transmit indicator [TX] (bottom right, green blink)", Location = new Point(15, 72), AutoSize = true };
        overlayGroup.Controls.Add(_showTransmitIndicatorCheck);

        page.Controls.Add(overlayGroup);

        return page;
    }

    private void LoadSettings()
    {
        // Connection
        _tcpRadio.Checked = _settings.Connection.Type.Equals("TCP", StringComparison.OrdinalIgnoreCase);
        _serialRadio.Checked = !_tcpRadio.Checked;
        _tcpHostBox.Text = _settings.Connection.TcpHost;
        _tcpPortBox.Value = _settings.Connection.TcpPort;

        if (_serialPortCombo.Items.Contains(_settings.Connection.SerialPort))
            _serialPortCombo.SelectedItem = _settings.Connection.SerialPort;
        else if (_serialPortCombo.Items.Count > 0)
            _serialPortCombo.SelectedIndex = 0;

        if (_baudRateCombo.Items.Contains(_settings.Connection.BaudRate))
            _baudRateCombo.SelectedItem = _settings.Connection.BaudRate;
        else
            _baudRateCombo.SelectedItem = 9600;

        // Stream
        _rtspPortBox.Value = _settings.Stream.RtspPort;

        if (_resolutionCombo.Items.Contains(_settings.Stream.Resolution))
            _resolutionCombo.SelectedItem = _settings.Stream.Resolution;
        else
            _resolutionCombo.SelectedItem = "640x480";

        if (_frameRateCombo.Items.Contains(_settings.Stream.FrameRate))
            _frameRateCombo.SelectedItem = _settings.Stream.FrameRate;
        else
            _frameRateCombo.SelectedItem = 30;

        // Display
        _titleBox.Text = _settings.Display.Title;

        if (_unitCombo.Items.Contains(_settings.Display.Unit))
            _unitCombo.SelectedItem = _settings.Display.Unit;
        else
            _unitCombo.SelectedItem = "LB";

        _customLabelBox.Text = _settings.Display.CustomLabel;
        _showTimestampCheck.Checked = _settings.Display.ShowTimestamp;
        _showStreamRateCheck.Checked = _settings.Display.ShowStreamRate;
        _showTransmitIndicatorCheck.Checked = _settings.Display.ShowTransmitIndicator;

        // Auto-start
        _autoStartCheck.Checked = _settings.AutoStart;

        OnConnectionTypeChanged(this, EventArgs.Empty);
    }

    private void SaveSettings(object? sender, EventArgs e)
    {
        // Connection
        _settings.Connection.Type = _tcpRadio.Checked ? "TCP" : "Serial";
        _settings.Connection.TcpHost = _tcpHostBox.Text;
        _settings.Connection.TcpPort = (int)_tcpPortBox.Value;
        _settings.Connection.SerialPort = _serialPortCombo.SelectedItem?.ToString() ?? "COM1";
        _settings.Connection.BaudRate = (int?)_baudRateCombo.SelectedItem ?? 9600;

        // Stream
        _settings.Stream.RtspPort = (int)_rtspPortBox.Value;
        _settings.Stream.Resolution = _resolutionCombo.SelectedItem?.ToString() ?? "640x480";
        _settings.Stream.FrameRate = (int?)_frameRateCombo.SelectedItem ?? 30;

        // Display
        _settings.Display.Title = _titleBox.Text;
        _settings.Display.Unit = _unitCombo.SelectedItem?.ToString() ?? "LB";
        _settings.Display.CustomLabel = _customLabelBox.Text;
        _settings.Display.ShowTimestamp = _showTimestampCheck.Checked;
        _settings.Display.ShowStreamRate = _showStreamRateCheck.Checked;
        _settings.Display.ShowTransmitIndicator = _showTransmitIndicatorCheck.Checked;

        // Auto-start
        _settings.AutoStart = _autoStartCheck.Checked;

        _configManager.Save(_settings);
    }

    private void OnConnectionTypeChanged(object? sender, EventArgs e)
    {
        var isTcp = _tcpRadio.Checked;
        _tcpHostBox.Enabled = isTcp;
        _tcpPortBox.Enabled = isTcp;
        _serialPortCombo.Enabled = !isTcp;
        _baudRateCombo.Enabled = !isTcp;
    }

    private async void TestConnection(object? sender, EventArgs e)
    {
        _testButton.Enabled = false;
        _statusLabel.Text = "Testing connection...";
        _statusLabel.ForeColor = Color.Blue;

        try
        {
            IScaleReader reader;
            if (_tcpRadio.Checked)
            {
                reader = new TcpScaleReader(_tcpHostBox.Text, (int)_tcpPortBox.Value);
            }
            else
            {
                var port = _serialPortCombo.SelectedItem?.ToString() ?? "COM1";
                var baud = (int?)_baudRateCombo.SelectedItem ?? 9600;
                reader = new SerialScaleReader(port, baud);
            }

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            string? receivedWeight = null;

            reader.WeightReceived += (s, e) => receivedWeight = e.Weight;

            await reader.StartAsync(cts.Token);
            await Task.Delay(3000, cts.Token);

            reader.Stop();
            reader.Dispose();

            if (receivedWeight != null)
            {
                _statusLabel.Text = $"Success! Weight: {receivedWeight}";
                _statusLabel.ForeColor = Color.Green;
            }
            else if (reader.IsConnected)
            {
                _statusLabel.Text = "Connected but no weight data received";
                _statusLabel.ForeColor = Color.Orange;
            }
            else
            {
                _statusLabel.Text = "Connection failed";
                _statusLabel.ForeColor = Color.Red;
            }
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Error: {ex.Message}";
            _statusLabel.ForeColor = Color.Red;
        }
        finally
        {
            _testButton.Enabled = true;
        }
    }
}
