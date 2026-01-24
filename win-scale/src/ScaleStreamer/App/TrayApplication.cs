using ScaleStreamer.Config;
using ScaleStreamer.Core;
using System.Diagnostics;

namespace ScaleStreamer.App;

/// <summary>
/// System tray application with context menu.
/// </summary>
public class TrayApplication : IDisposable
{
    private readonly AppSettings _settings;
    private readonly ConfigManager _configManager;
    private readonly NotifyIcon _trayIcon;
    private readonly ContextMenuStrip _contextMenu;

    private IScaleReader? _scaleReader;
    private StreamManager? _streamManager;
    private bool _disposed;

    private ToolStripMenuItem _statusItem = null!;
    private ToolStripMenuItem _startStopItem = null!;
    private ToolStripMenuItem _weightItem = null!;

    public TrayApplication(AppSettings settings, ConfigManager configManager)
    {
        _settings = settings;
        _configManager = configManager;

        Program.LogMessage("Initializing tray application...");

        _contextMenu = CreateContextMenu();

        // Load custom icon from Resources folder
        Icon? customIcon = null;
        try
        {
            var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "icon.ico");
            Program.LogMessage($"Loading icon from: {iconPath}");

            if (File.Exists(iconPath))
            {
                customIcon = new Icon(iconPath);
                Program.LogMessage("Custom icon loaded successfully.");
            }
            else
            {
                Program.LogMessage($"WARNING: Icon file not found at {iconPath}, using default icon.");
            }
        }
        catch (Exception ex)
        {
            Program.LogMessage($"ERROR loading custom icon: {ex.Message}");
        }

        _trayIcon = new NotifyIcon
        {
            Icon = customIcon ?? SystemIcons.Application,
            Text = "Scale Streamer",
            ContextMenuStrip = _contextMenu,
            Visible = true
        };

        Program.LogMessage("Tray icon created and made visible.");

        _trayIcon.DoubleClick += (s, e) => OpenConfiguration();

        if (_settings.AutoStart)
        {
            Program.LogMessage("Auto-start enabled, starting stream...");
            Task.Run(StartStreamingAsync);
        }
        else
        {
            Program.LogMessage("Auto-start disabled.");
        }
    }

    private ContextMenuStrip CreateContextMenu()
    {
        var menu = new ContextMenuStrip();

        _weightItem = new ToolStripMenuItem("Weight: ------")
        {
            Enabled = false,
            Font = new Font(menu.Font, FontStyle.Bold)
        };
        menu.Items.Add(_weightItem);

        menu.Items.Add(new ToolStripSeparator());

        _statusItem = new ToolStripMenuItem("Status: Stopped") { Enabled = false };
        menu.Items.Add(_statusItem);

        _startStopItem = new ToolStripMenuItem("Start Stream", null, (s, e) => ToggleStreaming());
        menu.Items.Add(_startStopItem);

        menu.Items.Add(new ToolStripSeparator());

        var viewRtspItem = new ToolStripMenuItem("View RTSP Stream", null, (s, e) => ViewRtspStream());
        menu.Items.Add(viewRtspItem);

        var viewHlsItem = new ToolStripMenuItem("View in Browser", null, (s, e) => ViewHlsStream());
        menu.Items.Add(viewHlsItem);

        var copyUrlItem = new ToolStripMenuItem("Copy RTSP URL", null, (s, e) => CopyRtspUrl());
        menu.Items.Add(copyUrlItem);

        menu.Items.Add(new ToolStripSeparator());

        var configureItem = new ToolStripMenuItem("Configure...", null, (s, e) => OpenConfiguration());
        menu.Items.Add(configureItem);

        var openConfigFolderItem = new ToolStripMenuItem("Open Config Folder", null, (s, e) => OpenConfigFolder());
        menu.Items.Add(openConfigFolderItem);

        menu.Items.Add(new ToolStripSeparator());

        var exitItem = new ToolStripMenuItem("Exit", null, (s, e) => Exit());
        menu.Items.Add(exitItem);

        return menu;
    }

    private async void ToggleStreaming()
    {
        if (_streamManager?.IsStreaming == true)
        {
            StopStreaming();
        }
        else
        {
            await StartStreamingAsync();
        }
    }

    private async Task StartStreamingAsync()
    {
        try
        {
            UpdateStatus("Starting...");
            _startStopItem.Enabled = false;

            // Create scale reader
            _scaleReader?.Dispose();
            _scaleReader = CreateScaleReader();
            _scaleReader.WeightReceived += OnWeightReceived;
            _scaleReader.ConnectionStatusChanged += OnConnectionStatusChanged;

            // Create stream manager
            _streamManager?.Dispose();
            _streamManager = new StreamManager(_settings);
            _streamManager.StatusChanged += (s, msg) => UpdateStatus(msg);

            // Start streaming
            await _streamManager.StartAsync();
            await _scaleReader.StartAsync();

            _startStopItem.Text = "Stop Stream";
            _startStopItem.Enabled = true;
            UpdateStatus("Streaming");

            ShowBalloon("Stream Started", $"RTSP: {_streamManager.GetRtspUrl()}");
        }
        catch (Exception ex)
        {
            UpdateStatus($"Error: {ex.Message}");
            _startStopItem.Text = "Start Stream";
            _startStopItem.Enabled = true;
            ShowBalloon("Error", ex.Message, ToolTipIcon.Error);
        }
    }

    private void StopStreaming()
    {
        try
        {
            _scaleReader?.Stop();
            _streamManager?.Stop();

            _startStopItem.Text = "Start Stream";
            UpdateStatus("Stopped");
            UpdateWeight("------");

            ShowBalloon("Stream Stopped", "Scale streaming has been stopped.");
        }
        catch (Exception ex)
        {
            ShowBalloon("Error", ex.Message, ToolTipIcon.Error);
        }
    }

    private IScaleReader CreateScaleReader()
    {
        if (_settings.Connection.Type.Equals("Serial", StringComparison.OrdinalIgnoreCase))
        {
            var parity = Enum.Parse<System.IO.Ports.Parity>(_settings.Connection.Parity);
            var stopBits = Enum.Parse<System.IO.Ports.StopBits>(_settings.Connection.StopBits);

            return new SerialScaleReader(
                _settings.Connection.SerialPort,
                _settings.Connection.BaudRate,
                _settings.Connection.DataBits,
                parity,
                stopBits
            );
        }
        else
        {
            return new TcpScaleReader(
                _settings.Connection.TcpHost,
                _settings.Connection.TcpPort
            );
        }
    }

    private void OnWeightReceived(object? sender, WeightReceivedEventArgs e)
    {
        _streamManager?.UpdateWeight(e.Weight);
        UpdateWeight(e.Weight);
    }

    private void OnConnectionStatusChanged(object? sender, ConnectionStatusEventArgs e)
    {
        var status = e.IsConnected ? "Connected" : "Disconnected";
        if (!string.IsNullOrEmpty(e.Message))
        {
            status += $": {e.Message}";
        }
        UpdateStatus(status);
    }

    private void UpdateStatus(string status)
    {
        if (_trayIcon.ContextMenuStrip?.InvokeRequired == true)
        {
            _trayIcon.ContextMenuStrip.Invoke(() => UpdateStatus(status));
            return;
        }

        _statusItem.Text = $"Status: {status}";
        _trayIcon.Text = $"Scale Streamer - {status}";
    }

    private void UpdateWeight(string weight)
    {
        if (_trayIcon.ContextMenuStrip?.InvokeRequired == true)
        {
            _trayIcon.ContextMenuStrip.Invoke(() => UpdateWeight(weight));
            return;
        }

        _weightItem.Text = $"Weight: {weight} {_settings.Display.Unit}";
    }

    private void ViewRtspStream()
    {
        if (_streamManager == null) return;

        var url = _streamManager.GetRtspUrl();
        try
        {
            // Try VLC first
            var vlcPaths = new[]
            {
                @"C:\Program Files\VideoLAN\VLC\vlc.exe",
                @"C:\Program Files (x86)\VideoLAN\VLC\vlc.exe"
            };

            var vlcPath = vlcPaths.FirstOrDefault(File.Exists);
            if (vlcPath != null)
            {
                Process.Start(vlcPath, url);
            }
            else
            {
                // Fallback to ffplay if bundled
                var ffplayPath = Path.Combine(AppContext.BaseDirectory, "deps", "ffmpeg", "ffplay.exe");
                if (File.Exists(ffplayPath))
                {
                    Process.Start(ffplayPath, $"-rtsp_transport tcp \"{url}\"");
                }
                else
                {
                    Clipboard.SetText(url);
                    ShowBalloon("RTSP URL Copied", "Open in VLC: " + url);
                }
            }
        }
        catch (Exception ex)
        {
            ShowBalloon("Error", ex.Message, ToolTipIcon.Error);
        }
    }

    private void ViewHlsStream()
    {
        if (_streamManager == null) return;

        var url = _streamManager.GetHlsUrl();
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            ShowBalloon("Error", ex.Message, ToolTipIcon.Error);
        }
    }

    private void CopyRtspUrl()
    {
        if (_streamManager == null)
        {
            Clipboard.SetText($"rtsp://127.0.0.1:{_settings.Stream.RtspPort}/scale");
        }
        else
        {
            Clipboard.SetText(_streamManager.GetRtspUrl());
        }
        ShowBalloon("URL Copied", "RTSP URL copied to clipboard.");
    }

    private void OpenConfiguration()
    {
        var wasStreaming = _streamManager?.IsStreaming == true;

        using var form = new ConfigForm(_settings, _configManager);
        if (form.ShowDialog() == DialogResult.OK)
        {
            // Reload settings
            if (wasStreaming)
            {
                StopStreaming();
                Task.Run(StartStreamingAsync);
            }
        }
    }

    private void OpenConfigFolder()
    {
        var folder = Path.GetDirectoryName(_configManager.ConfigPath);
        if (folder != null && Directory.Exists(folder))
        {
            Process.Start("explorer.exe", folder);
        }
    }

    private void ShowBalloon(string title, string text, ToolTipIcon icon = ToolTipIcon.Info)
    {
        _trayIcon.ShowBalloonTip(3000, title, text, icon);
    }

    private void Exit()
    {
        StopStreaming();
        _trayIcon.Visible = false;
        Application.Exit();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _scaleReader?.Dispose();
        _streamManager?.Dispose();
        _trayIcon.Dispose();
        _contextMenu.Dispose();
    }
}
