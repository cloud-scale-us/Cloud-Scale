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
    private const string APP_VERSION = "5.2.0";

    private readonly IpcClient _ipcClient;
    private System.Windows.Forms.Timer _statusTimer;
    private bool _serviceConnected = false;
    private bool _connectionInProgress = false;

    // Tab controls
    private TabControl _mainTabControl;
    private ConnectionTab? _connectionTab;
    private ProtocolTab? _protocolTab;
    private MonitoringTab? _monitoringTab;
    private StatusTab? _statusTab;
    private LoggingTab? _loggingTab;
    private SettingsTab? _settingsTab;
    private DiagnosticsTab? _diagnosticsTab;
    private OnvifMonitorTab? _onvifMonitorTab;

    // Update notification
    private Panel? _updateNotificationPanel;
    private UpdateChecker? _updateChecker;
    private UpdateInfo? _availableUpdate;

    // System tray
    private NotifyIcon? _notifyIcon;

    public MainForm()
    {
        Log.Information("MainForm constructor starting");
        InitializeComponent();

        Log.Debug("Creating IpcClient");
        _ipcClient = new IpcClient("ScaleStreamerPipe");
        _ipcClient.MessageReceived += OnServiceMessageReceived;
        _ipcClient.ErrorOccurred += OnIpcError;

        InitializeTabs();
        InitializeUpdateNotification();
        InitializeSystemTray();

        // Timer to check service connection
        _statusTimer = new System.Windows.Forms.Timer
        {
            Interval = 5000 // Check every 5 seconds
        };
        _statusTimer.Tick += StatusTimer_Tick;
        _statusTimer.Start();
        Log.Debug("Status timer started with 5s interval");

        // Defer initial connection and update check until form is shown
        this.Shown += OnFormShown;
        Log.Information("MainForm constructor completed");
    }

    private async void OnFormShown(object? sender, EventArgs e)
    {
        Log.Information("OnFormShown event fired - form is now visible");

        // Give service a moment to fully start if app launched during installation
        Log.Debug("Delaying 500ms before initial connection attempt");
        await Task.Delay(500);

        // Try initial connection after form is visible
        Log.Information("Starting initial connection to service");
        await ConnectToServiceAsync();

        // Check for updates (non-blocking)
        Log.Debug("Starting background update check");
        _ = Task.Run(async () =>
        {
            _updateChecker = new UpdateChecker(APP_VERSION);
            var update = await _updateChecker.CheckForUpdatesAsync();
            if (update != null)
            {
                Log.Information("Update available: v{Version}", update.LatestVersion);
                _availableUpdate = update;
                ShowUpdateNotification(update);
            }
            else
            {
                Log.Debug("No updates available");
            }
        });
    }

    private void InitializeComponent()
    {
        this.Text = $"Scale Streamer Configuration - v{APP_VERSION}";
        this.Size = new Size(1200, 800);
        this.StartPosition = FormStartPosition.CenterScreen;
        this.MinimumSize = new Size(1000, 600);
        this.Icon = LoadAppIcon();

        // Create header panel with logo
        var headerPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 60,
            BackColor = Color.FromArgb(240, 240, 240),
            Padding = new Padding(10)
        };

        // Logo box
        var logoPictureBox = new PictureBox
        {
            Size = new Size(48, 48),
            Location = new Point(10, 6),
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };

        try
        {
            var logoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logo.png");
            if (File.Exists(logoPath))
            {
                logoPictureBox.Image = Image.FromFile(logoPath);
            }
        }
        catch { /* Logo not critical */ }

        var titleLabel = new Label
        {
            Text = "Scale Streamer",
            Font = new Font("Segoe UI", 16F, FontStyle.Bold),
            Location = new Point(68, 8),
            AutoSize = true,
            ForeColor = Color.FromArgb(0, 120, 215)
        };

        var versionLabel = new Label
        {
            Text = $"Version {APP_VERSION}",
            Font = new Font("Segoe UI", 9F),
            Location = new Point(68, 35),
            AutoSize = true,
            ForeColor = Color.Gray
        };

        headerPanel.Controls.Add(logoPictureBox);
        headerPanel.Controls.Add(titleLabel);
        headerPanel.Controls.Add(versionLabel);

        // Create main tab control - MUST be added after header for proper docking
        _mainTabControl = new TabControl
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 9F),
            Visible = true,
            Name = "MainTabControl"
        };

        // Add header first (docks to top), then tab control (fills remaining space)
        this.SuspendLayout();
        this.Controls.Add(headerPanel);
        this.Controls.Add(_mainTabControl);
        this.ResumeLayout(false);
        this.PerformLayout();

        // Explicitly show the tab control
        _mainTabControl.Show();
        _mainTabControl.BringToFront();
    }

    private Icon? LoadAppIcon()
    {
        try
        {
            var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logo.ico");
            if (File.Exists(iconPath))
            {
                return new Icon(iconPath);
            }
        }
        catch { /* Icon not critical */ }
        return null;
    }

    private void InitializeTabs()
    {
        if (_mainTabControl == null)
        {
            throw new InvalidOperationException("TabControl not initialized. InitializeComponent must be called first.");
        }

        _mainTabControl.SuspendLayout();

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

        // Settings Tab
        var settingsPage = new TabPage("Settings")
        {
            UseVisualStyleBackColor = true
        };
        _settingsTab = new SettingsTab(_ipcClient);
        _settingsTab.Dock = DockStyle.Fill;
        settingsPage.Controls.Add(_settingsTab);
        _mainTabControl.TabPages.Add(settingsPage);

        // RS232 Diagnostics Tab
        var diagnosticsPage = new TabPage("RS232 Diagnostics")
        {
            UseVisualStyleBackColor = true
        };
        _diagnosticsTab = new DiagnosticsTab(_ipcClient);
        _diagnosticsTab.Dock = DockStyle.Fill;
        diagnosticsPage.Controls.Add(_diagnosticsTab);
        _mainTabControl.TabPages.Add(diagnosticsPage);

        // ONVIF Server Monitor Tab
        var onvifPage = new TabPage("ONVIF Server")
        {
            UseVisualStyleBackColor = true
        };
        _onvifMonitorTab = new OnvifMonitorTab(_ipcClient);
        _onvifMonitorTab.Dock = DockStyle.Fill;
        onvifPage.Controls.Add(_onvifMonitorTab);
        _mainTabControl.TabPages.Add(onvifPage);

        _mainTabControl.ResumeLayout(true);
        _mainTabControl.SelectedIndex = 0; // Select first tab

        Log.Information("Initialized {TabCount} tabs", _mainTabControl.TabPages.Count);
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

    private void InitializeSystemTray()
    {
        _notifyIcon = new NotifyIcon
        {
            Visible = true,
            Text = $"Scale Streamer v{APP_VERSION}"
        };

        // Try to load icon from file
        try
        {
            var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logo.ico");
            if (File.Exists(iconPath))
            {
                _notifyIcon.Icon = new Icon(iconPath);
            }
            else
            {
                // Use default application icon
                _notifyIcon.Icon = this.Icon ?? SystemIcons.Application;
            }
        }
        catch
        {
            _notifyIcon.Icon = SystemIcons.Application;
        }

        // Create context menu
        var contextMenu = new ContextMenuStrip();

        var openConfigItem = new ToolStripMenuItem("Open Configuration");
        openConfigItem.Font = new Font(openConfigItem.Font, FontStyle.Bold);
        openConfigItem.Click += (s, e) =>
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
            this.BringToFront();
        };

        var checkUpdatesItem = new ToolStripMenuItem("Check for Updates");
        checkUpdatesItem.Click += async (s, e) =>
        {
            if (_updateChecker != null)
            {
                var update = await _updateChecker.CheckForUpdatesAsync(forceCheck: true);
                if (update != null)
                {
                    _availableUpdate = update;
                    ShowUpdateNotification(update);
                    this.Show();
                    this.WindowState = FormWindowState.Normal;
                }
                else
                {
                    MessageBox.Show(
                        $"You are running the latest version (v{APP_VERSION}).",
                        "No Updates Available",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
            }
        };

        var separator1 = new ToolStripSeparator();

        var stopServiceItem = new ToolStripMenuItem("Stop Service");
        stopServiceItem.Click += async (s, e) =>
        {
            var result = MessageBox.Show(
                "Are you sure you want to stop the Scale Streamer Service?\n\nThis will disconnect all scales.",
                "Stop Service",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result == DialogResult.Yes)
            {
                try
                {
                    var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "sc.exe",
                            Arguments = "stop ScaleStreamerService",
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            RedirectStandardOutput = true
                        }
                    };
                    process.Start();
                    await process.WaitForExitAsync();

                    MessageBox.Show("Service stopped successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    _serviceConnected = false;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to stop service:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        };

        var startServiceItem = new ToolStripMenuItem("Start Service");
        startServiceItem.Click += async (s, e) =>
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "sc.exe",
                        Arguments = "start ScaleStreamerService",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true
                    }
                };
                process.Start();
                await process.WaitForExitAsync();

                MessageBox.Show("Service started successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                await Task.Delay(1000);
                await ConnectToServiceAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to start service:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };

        var restartServiceItem = new ToolStripMenuItem("Restart Service");
        restartServiceItem.Click += async (s, e) =>
        {
            var result = MessageBox.Show(
                "Are you sure you want to restart the Scale Streamer Service?",
                "Restart Service",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                try
                {
                    // Stop service
                    var stopProcess = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "sc.exe",
                            Arguments = "stop ScaleStreamerService",
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            RedirectStandardOutput = true
                        }
                    };
                    stopProcess.Start();
                    await stopProcess.WaitForExitAsync();
                    await Task.Delay(2000);

                    // Start service
                    var startProcess = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "sc.exe",
                            Arguments = "start ScaleStreamerService",
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            RedirectStandardOutput = true
                        }
                    };
                    startProcess.Start();
                    await startProcess.WaitForExitAsync();

                    MessageBox.Show("Service restarted successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    await Task.Delay(1000);
                    await ConnectToServiceAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to restart service:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        };

        var separator2 = new ToolStripSeparator();

        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (s, e) =>
        {
            var result = MessageBox.Show(
                "Are you sure you want to exit the configuration app?\n\nNote: The service will continue running in the background.",
                "Exit",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                Application.Exit();
            }
        };

        contextMenu.Items.Add(openConfigItem);
        contextMenu.Items.Add(checkUpdatesItem);
        contextMenu.Items.Add(separator1);
        contextMenu.Items.Add(startServiceItem);
        contextMenu.Items.Add(stopServiceItem);
        contextMenu.Items.Add(restartServiceItem);
        contextMenu.Items.Add(separator2);
        contextMenu.Items.Add(exitItem);

        _notifyIcon.ContextMenuStrip = contextMenu;

        // Double-click to open main window
        _notifyIcon.DoubleClick += (s, e) =>
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
            this.BringToFront();
        };
    }

    private async Task ConnectToServiceAsync()
    {
        // Skip if already connected
        if (_serviceConnected && _ipcClient.IsConnected)
        {
            Log.Verbose("Already connected, skipping connection attempt");
            return;
        }

        if (_connectionInProgress)
            return;

        _connectionInProgress = true;
        try
        {
            Log.Information("Attempting to connect to Scale Streamer Service...");
            Log.Debug("Starting ConnectAsync with 3s timeout...");

            // Run the entire connection on a background thread to avoid any UI thread capture
            var connected = await Task.Run(async () =>
            {
                return await _ipcClient.ConnectAsync(timeoutMs: 3000).ConfigureAwait(false);
            }).ConfigureAwait(false);

            Log.Debug("ConnectAsync returned: {Connected}", connected);

            if (connected && _ipcClient.IsConnected)
            {
                _serviceConnected = true;
                Log.Information("Connected to Scale Streamer Service");
            }
            else
            {
                _serviceConnected = false;
                Log.Warning("Failed to connect to Scale Streamer Service - will retry");
            }
        }
        catch (Exception ex)
        {
            _serviceConnected = false;
            Log.Error(ex, "Error connecting to service");
        }
        finally
        {
            _connectionInProgress = false;
            Log.Debug("ConnectToServiceAsync completed, _connectionInProgress = false");
        }
    }


    private async void StatusTimer_Tick(object? sender, EventArgs e)
    {
        Log.Verbose("StatusTimer_Tick: _serviceConnected={ServiceConnected}, IpcClient.IsConnected={IpcConnected}, _connectionInProgress={ConnectionInProgress}",
            _serviceConnected, _ipcClient.IsConnected, _connectionInProgress);

        if (!_serviceConnected || !_ipcClient.IsConnected)
        {
            if (!_connectionInProgress)
            {
                Log.Debug("StatusTimer: Not connected, attempting reconnection");
                await ConnectToServiceAsync();
            }
            else
            {
                Log.Verbose("StatusTimer: Connection attempt already in progress, skipping");
            }
        }
    }

    private void OnServiceMessageReceived(object? sender, IpcMessage message)
    {
        Log.Information("OnServiceMessageReceived: Type={MessageType} (int={TypeInt}), PayloadLen={Length}",
            message.MessageType, (int)message.MessageType, message.Payload?.Length ?? 0);

        // Always log to monitoring tab for debugging
        _monitoringTab?.AppendDebugLog($"IPC: {message.MessageType}");

        // Route message to appropriate tab
        switch (message.MessageType)
        {
            case IpcMessageType.ServiceStarted:
                // Welcome message received - connection is fully established
                _serviceConnected = true;
                Log.Information("Connected to Scale Streamer Service: {Payload}", message.Payload);
                _monitoringTab?.UpdateConnectionStatus(true);
                break;

            case IpcMessageType.WeightReading:
                Log.Information("Routing WeightReading to MonitoringTab");
                _monitoringTab?.HandleWeightReading(message);
                break;

            case IpcMessageType.ConnectionStatus:
                _statusTab?.HandleConnectionStatus(message);
                break;

            case IpcMessageType.Error:
                _loggingTab?.HandleError(message);
                break;

            case IpcMessageType.RawData:
                // Route raw TCP data to diagnostics
                _diagnosticsTab?.LogDebug($"Raw data received: {message.Payload}");
                break;

            default:
                Log.Warning("Unknown message type: {MessageType}", message.MessageType);
                _monitoringTab?.AppendDebugLog($"UNKNOWN: {message.MessageType}");
                break;
        }
    }

    private void OnIpcError(object? sender, string error)
    {
        Log.Error("IPC Error: {Error}", error);
        _serviceConnected = false;
        _monitoringTab?.UpdateConnectionStatus(false);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // If user clicks X, minimize to tray instead of closing
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            this.Hide();
            if (_notifyIcon != null)
            {
                _notifyIcon.ShowBalloonTip(2000, "Scale Streamer", "Application minimized to system tray", ToolTipIcon.Info);
            }
            return;
        }

        // Cleanup on actual exit
        _statusTimer.Stop();
        _statusTimer.Dispose();

        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }

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
        }
    }

    private async void DownloadButton_Click(object? sender, EventArgs e)
    {
        if (_availableUpdate != null && !string.IsNullOrEmpty(_availableUpdate.DownloadUrl) && _updateChecker != null)
        {
            // Create progress form
            var progressForm = new Form
            {
                Text = "Downloading Update",
                Size = new Size(500, 150),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                ControlBox = false
            };

            var progressBar = new ProgressBar
            {
                Location = new Point(20, 20),
                Size = new Size(440, 30),
                Style = ProgressBarStyle.Continuous
            };

            var statusLabel = new Label
            {
                Location = new Point(20, 60),
                Size = new Size(440, 20),
                Text = "Starting download..."
            };

            var cancelButton = new Button
            {
                Text = "Cancel",
                Location = new Point(200, 90),
                Size = new Size(100, 30)
            };

            var cts = new CancellationTokenSource();
            cancelButton.Click += (s, ev) => { cts.Cancel(); progressForm.Close(); };

            progressForm.Controls.Add(progressBar);
            progressForm.Controls.Add(statusLabel);
            progressForm.Controls.Add(cancelButton);

            progressForm.Show(this);

            try
            {
                var progress = new Progress<DownloadProgressInfo>(info =>
                {
                    if (progressForm.IsDisposed) return;
                    progressBar.Value = info.ProgressPercentage;
                    statusLabel.Text = info.Status;
                });

                var tempPath = await _updateChecker.DownloadInstallerAsync(
                    _availableUpdate.DownloadUrl,
                    progress,
                    cts.Token);

                progressForm.Close();

                if (tempPath != null && File.Exists(tempPath))
                {
                    var result = MessageBox.Show(
                        $"Update downloaded successfully!\n\nWould you like to install it now?\n\nNote: This will close the configuration app.",
                        "Update Ready",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);

                    if (result == DialogResult.Yes)
                    {
                        // Launch installer
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = "msiexec.exe",
                            Arguments = $"/i \"{tempPath}\" /passive",
                            UseShellExecute = true
                        });

                        // Close the app
                        Application.Exit();
                    }
                }
                else
                {
                    MessageBox.Show(
                        "Download failed. Please try again or download manually from GitHub.",
                        "Download Failed",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }
            catch (OperationCanceledException)
            {
                progressForm.Close();
                MessageBox.Show("Download cancelled.", "Cancelled", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                progressForm.Close();
                MessageBox.Show(
                    $"Failed to download update:\n{ex.Message}",
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
