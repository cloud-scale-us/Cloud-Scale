using ScaleStreamer.Common.IPC;
using ScaleStreamer.Common.Services;
using Serilog;
using System.Diagnostics;

namespace ScaleStreamer.Config;

/// <summary>
/// Main configuration window with tabbed interface
/// </summary>
public partial class MainForm : Form
{
    private const string APP_VERSION = "2.5.0";

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

    // Update notification
    private Panel? _updateNotificationPanel;
    private UpdateChecker? _updateChecker;
    private UpdateInfo? _availableUpdate;

    public MainForm()
    {
        InitializeComponent();

        _ipcClient = new IpcClient("ScaleStreamerPipe");
        _ipcClient.MessageReceived += OnServiceMessageReceived;
        _ipcClient.ErrorOccurred += OnIpcError;

        InitializeTabs();
        InitializeStatusBar();
        InitializeUpdateNotification();

        // Timer to check service connection
        _statusTimer = new System.Windows.Forms.Timer
        {
            Interval = 5000 // Check every 5 seconds
        };
        _statusTimer.Tick += StatusTimer_Tick;
        _statusTimer.Start();

        // Defer initial connection and update check until form is shown
        this.Shown += OnFormShown;
    }

    private async void OnFormShown(object? sender, EventArgs e)
    {
        // Give service a moment to fully start if app launched during installation
        await Task.Delay(500);

        // Try initial connection after form is visible
        await ConnectToServiceAsync();

        // Check for updates (non-blocking)
        _ = Task.Run(async () =>
        {
            _updateChecker = new UpdateChecker(APP_VERSION);
            var update = await _updateChecker.CheckForUpdatesAsync();
            if (update != null)
            {
                _availableUpdate = update;
                ShowUpdateNotification(update);
            }
        });
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
            Name = "versionLabel",
            Text = $"v{APP_VERSION}",
            Spring = true,
            TextAlign = ContentAlignment.MiddleRight
        };

        statusStrip.Items.Add(serviceStatusLabel);
        statusStrip.Items.Add(versionLabel);

        this.Controls.Add(statusStrip);
    }

    private void InitializeUpdateNotification()
    {
        _updateNotificationPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 40,
            BackColor = Color.FromArgb(255, 243, 205), // Light yellow
            Visible = false,
            Padding = new Padding(10, 8, 10, 8)
        };

        var messageLabel = new Label
        {
            Name = "updateMessage",
            AutoSize = true,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = Color.FromArgb(133, 100, 4),
            Text = "⚠ New version available!",
            Location = new Point(10, 11)
        };

        var downloadButton = new Button
        {
            Text = "Download Update",
            AutoSize = true,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(0, 120, 215),
            ForeColor = Color.White,
            Location = new Point(200, 7),
            Cursor = Cursors.Hand
        };
        downloadButton.FlatAppearance.BorderSize = 0;
        downloadButton.Click += DownloadButton_Click;

        var viewNotesButton = new Button
        {
            Text = "Release Notes",
            AutoSize = true,
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.FromArgb(0, 120, 215),
            Location = new Point(330, 7),
            Cursor = Cursors.Hand
        };
        viewNotesButton.FlatAppearance.BorderSize = 1;
        viewNotesButton.FlatAppearance.BorderColor = Color.FromArgb(0, 120, 215);
        viewNotesButton.Click += ViewNotesButton_Click;

        var closeButton = new Button
        {
            Text = "×",
            Size = new Size(25, 25),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            ForeColor = Color.FromArgb(133, 100, 4),
            Cursor = Cursors.Hand,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        closeButton.FlatAppearance.BorderSize = 0;
        closeButton.Click += (s, e) => { if (_updateNotificationPanel != null) _updateNotificationPanel.Visible = false; };
        closeButton.Location = new Point(_updateNotificationPanel.Width - 35, 7);

        _updateNotificationPanel.Controls.Add(messageLabel);
        _updateNotificationPanel.Controls.Add(downloadButton);
        _updateNotificationPanel.Controls.Add(viewNotesButton);
        _updateNotificationPanel.Controls.Add(closeButton);

        this.Controls.Add(_updateNotificationPanel);
        _updateNotificationPanel.BringToFront();
    }

    private async Task ConnectToServiceAsync()
    {
        try
        {
            Log.Information("Attempting to connect to Scale Streamer Service...");

            // Increase timeout to 3 seconds for more reliable connection
            var connected = await _ipcClient.ConnectAsync(timeoutMs: 3000);

            if (connected && _ipcClient.IsConnected)
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

    private void ShowUpdateNotification(UpdateInfo update)
    {
        if (InvokeRequired)
        {
            Invoke(() => ShowUpdateNotification(update));
            return;
        }

        if (_updateNotificationPanel != null)
        {
            var messageLabel = _updateNotificationPanel.Controls["updateMessage"] as Label;
            if (messageLabel != null)
            {
                messageLabel.Text = $"⚠ New version available: v{update.LatestVersion}";
            }

            _updateNotificationPanel.Visible = true;

            // Update version label in status bar
            var statusStrip = this.Controls.OfType<StatusStrip>().FirstOrDefault();
            if (statusStrip != null)
            {
                var versionLabel = statusStrip.Items["versionLabel"] as ToolStripStatusLabel;
                if (versionLabel != null)
                {
                    versionLabel.Text = $"v{APP_VERSION} (Update available)";
                    versionLabel.ForeColor = Color.DarkOrange;
                }
            }
        }
    }

    private void DownloadButton_Click(object? sender, EventArgs e)
    {
        if (_availableUpdate != null && !string.IsNullOrEmpty(_availableUpdate.DownloadUrl))
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = _availableUpdate.DownloadUrl,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to open download page:\n{ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
    }

    private void ViewNotesButton_Click(object? sender, EventArgs e)
    {
        if (_availableUpdate != null)
        {
            var notesForm = new Form
            {
                Text = $"Release Notes - v{_availableUpdate.LatestVersion}",
                Size = new Size(600, 500),
                StartPosition = FormStartPosition.CenterParent,
                MinimumSize = new Size(500, 400)
            };

            var textBox = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Dock = DockStyle.Fill,
                Font = new Font("Consolas", 9F),
                ReadOnly = true,
                Text = $"{_availableUpdate.ReleaseName}\n\n" +
                       $"Version: {_availableUpdate.LatestVersion}\n" +
                       $"Published: {_availableUpdate.PublishedAt:yyyy-MM-dd}\n" +
                       $"Size: {_availableUpdate.FileSizeMB}\n\n" +
                       $"{new string('-', 60)}\n\n" +
                       $"{_availableUpdate.ReleaseNotes}"
            };

            var buttonPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 50,
                Padding = new Padding(10)
            };

            var downloadBtn = new Button
            {
                Text = "Download Update",
                Size = new Size(150, 30),
                Location = new Point(10, 10),
                DialogResult = DialogResult.OK
            };
            downloadBtn.Click += (s, ev) =>
            {
                DownloadButton_Click(s, ev);
                notesForm.Close();
            };

            var closeBtn = new Button
            {
                Text = "Close",
                Size = new Size(100, 30),
                Location = new Point(170, 10),
                DialogResult = DialogResult.Cancel
            };

            buttonPanel.Controls.Add(downloadBtn);
            buttonPanel.Controls.Add(closeBtn);

            notesForm.Controls.Add(textBox);
            notesForm.Controls.Add(buttonPanel);
            notesForm.ShowDialog(this);
        }
    }
}
