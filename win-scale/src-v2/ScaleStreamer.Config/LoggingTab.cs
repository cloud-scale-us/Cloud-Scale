using ScaleStreamer.Common.IPC;

namespace ScaleStreamer.Config;

/// <summary>
/// Logging tab for viewing application events and errors
/// </summary>
public partial class LoggingTab : UserControl
{
    private readonly IpcClient _ipcClient;
    private ListView _eventsListView;
    private ComboBox _levelFilterCombo;
    private ComboBox _categoryFilterCombo;
    private TextBox _searchText;
    private Button _refreshButton;
    private Button _clearButton;
    private Button _exportButton;
    private CheckBox _autoScrollCheck;
    private Label _eventCountLabel;

    public LoggingTab(IpcClient ipcClient)
    {
        _ipcClient = ipcClient;
        InitializeComponent();
        LoadDefaults();
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
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        // Filter Panel (top)
        var filterPanel = CreateFilterPanel();
        mainLayout.Controls.Add(filterPanel, 0, 0);

        // Events List (middle)
        var eventsPanel = CreateEventsPanel();
        mainLayout.Controls.Add(eventsPanel, 0, 1);

        // Status Panel (bottom)
        var statusPanel = CreateStatusPanel();
        mainLayout.Controls.Add(statusPanel, 0, 2);

        this.Controls.Add(mainLayout);
    }

    private Control CreateFilterPanel()
    {
        var panel = new GroupBox
        {
            Text = "Filters",
            Dock = DockStyle.Fill,
            Padding = new Padding(10),
            AutoSize = true
        };

        var layout = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true
        };

        // Level Filter
        layout.Controls.Add(new Label { Text = "Level:", AutoSize = true, Padding = new Padding(0, 5, 5, 0) });
        _levelFilterCombo = new ComboBox { Width = 120, DropDownStyle = ComboBoxStyle.DropDownList };
        _levelFilterCombo.SelectedIndexChanged += Filter_Changed;
        layout.Controls.Add(_levelFilterCombo);

        // Category Filter
        layout.Controls.Add(new Label { Text = "Category:", AutoSize = true, Padding = new Padding(10, 5, 5, 0) });
        _categoryFilterCombo = new ComboBox { Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
        _categoryFilterCombo.SelectedIndexChanged += Filter_Changed;
        layout.Controls.Add(_categoryFilterCombo);

        // Search
        layout.Controls.Add(new Label { Text = "Search:", AutoSize = true, Padding = new Padding(10, 5, 5, 0) });
        _searchText = new TextBox { Width = 200 };
        _searchText.TextChanged += Filter_Changed;
        layout.Controls.Add(_searchText);

        // Buttons
        _refreshButton = new Button { Text = "Refresh", Width = 80, Margin = new Padding(10, 0, 0, 0) };
        _refreshButton.Click += Refresh_Click;
        layout.Controls.Add(_refreshButton);

        _clearButton = new Button { Text = "Clear", Width = 80 };
        _clearButton.Click += Clear_Click;
        layout.Controls.Add(_clearButton);

        _exportButton = new Button { Text = "Export...", Width = 80 };
        _exportButton.Click += Export_Click;
        layout.Controls.Add(_exportButton);

        panel.Controls.Add(layout);
        return panel;
    }

    private Control CreateEventsPanel()
    {
        var panel = new GroupBox
        {
            Text = "Event Log",
            Dock = DockStyle.Fill,
            Padding = new Padding(5)
        };

        _eventsListView = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            Font = new Font("Consolas", 9F)
        };

        _eventsListView.Columns.Add("Timestamp", 150);
        _eventsListView.Columns.Add("Level", 80);
        _eventsListView.Columns.Add("Category", 120);
        _eventsListView.Columns.Add("Message", 500);

        // Double-click to show details
        _eventsListView.DoubleClick += EventsList_DoubleClick;

        panel.Controls.Add(_eventsListView);
        return panel;
    }

    private Control CreateStatusPanel()
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            Padding = new Padding(0, 5, 0, 0)
        };

        _eventCountLabel = new Label
        {
            Text = "Events: 0",
            AutoSize = true,
            Padding = new Padding(0, 0, 20, 0)
        };
        panel.Controls.Add(_eventCountLabel);

        _autoScrollCheck = new CheckBox
        {
            Text = "Auto-scroll to new events",
            Checked = true,
            AutoSize = true
        };
        panel.Controls.Add(_autoScrollCheck);

        return panel;
    }

    private void LoadDefaults()
    {
        // Level Filter
        _levelFilterCombo.Items.Add("All");
        _levelFilterCombo.Items.Add("DEBUG");
        _levelFilterCombo.Items.Add("INFO");
        _levelFilterCombo.Items.Add("WARN");
        _levelFilterCombo.Items.Add("ERROR");
        _levelFilterCombo.Items.Add("CRITICAL");
        _levelFilterCombo.SelectedIndex = 0;

        // Category Filter
        _categoryFilterCombo.Items.Add("All");
        _categoryFilterCombo.Items.Add("ScaleConnection");
        _categoryFilterCombo.Items.Add("Service");
        _categoryFilterCombo.Items.Add("GUI");
        _categoryFilterCombo.Items.Add("FFmpeg");
        _categoryFilterCombo.Items.Add("MediaMTX");
        _categoryFilterCombo.Items.Add("Database");
        _categoryFilterCombo.SelectedIndex = 0;

        // Add sample events
        AddEvent(DateTime.Now, "INFO", "Service", "Scale Streamer Service started");
        AddEvent(DateTime.Now, "INFO", "Database", "Database initialized successfully");
        AddEvent(DateTime.Now, "INFO", "ScaleConnection", "Protocol templates loaded: 3");
    }

    private void AddEvent(DateTime timestamp, string level, string category, string message)
    {
        if (InvokeRequired)
        {
            Invoke(() => AddEvent(timestamp, level, category, message));
            return;
        }

        var item = new ListViewItem(timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff"));
        item.SubItems.Add(level);
        item.SubItems.Add(category);
        item.SubItems.Add(message);

        // Color code by level
        item.ForeColor = level switch
        {
            "DEBUG" => Color.Gray,
            "INFO" => Color.Black,
            "WARN" => Color.Orange,
            "ERROR" => Color.Red,
            "CRITICAL" => Color.DarkRed,
            _ => Color.Black
        };

        if (level == "CRITICAL")
            item.BackColor = Color.LightYellow;

        _eventsListView.Items.Insert(0, item);

        // Auto-scroll if enabled
        if (_autoScrollCheck.Checked && _eventsListView.Items.Count > 0)
        {
            _eventsListView.Items[0].EnsureVisible();
        }

        // Update count
        _eventCountLabel.Text = $"Events: {_eventsListView.Items.Count}";
    }

    public void HandleError(IpcMessage message)
    {
        try
        {
            if (string.IsNullOrEmpty(message.Payload))
                return;

            AddEvent(message.Timestamp, "ERROR", "Service", message.Payload);
        }
        catch (Exception ex)
        {
            // Silently fail to avoid infinite loop
        }
    }

    private void Filter_Changed(object? sender, EventArgs e)
    {
        // TODO: Apply filters to event list
    }

    private async void Refresh_Click(object? sender, EventArgs e)
    {
        _refreshButton.Enabled = false;
        try
        {
            // TODO: Request recent events from service via IPC
            await Task.Delay(500); // Simulate refresh
        }
        finally
        {
            _refreshButton.Enabled = true;
        }
    }

    private void Clear_Click(object? sender, EventArgs e)
    {
        var result = MessageBox.Show(
            "Are you sure you want to clear the event log display?\nThis will not delete events from the database.",
            "Confirm Clear",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (result == DialogResult.Yes)
        {
            _eventsListView.Items.Clear();
            _eventCountLabel.Text = "Events: 0";
        }
    }

    private void Export_Click(object? sender, EventArgs e)
    {
        using var dialog = new SaveFileDialog
        {
            Filter = "CSV files (*.csv)|*.csv|Text files (*.txt)|*.txt|All files (*.*)|*.*",
            DefaultExt = "csv",
            FileName = $"scalestreamer_events_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            try
            {
                using var writer = new System.IO.StreamWriter(dialog.FileName);

                // Write header
                writer.WriteLine("Timestamp,Level,Category,Message");

                // Write events
                foreach (ListViewItem item in _eventsListView.Items)
                {
                    var timestamp = item.SubItems[0].Text;
                    var level = item.SubItems[1].Text;
                    var category = item.SubItems[2].Text;
                    var message = item.SubItems[3].Text.Replace("\"", "\"\""); // Escape quotes

                    writer.WriteLine($"\"{timestamp}\",\"{level}\",\"{category}\",\"{message}\"");
                }

                MessageBox.Show(
                    $"Events exported successfully to:\n{dialog.FileName}",
                    "Export Complete",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error exporting events: {ex.Message}",
                    "Export Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
    }

    private void EventsList_DoubleClick(object? sender, EventArgs e)
    {
        if (_eventsListView.SelectedItems.Count == 0)
            return;

        var item = _eventsListView.SelectedItems[0];
        var details = $"Timestamp: {item.SubItems[0].Text}\n" +
                     $"Level: {item.SubItems[1].Text}\n" +
                     $"Category: {item.SubItems[2].Text}\n" +
                     $"Message: {item.SubItems[3].Text}";

        MessageBox.Show(details, "Event Details", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
}
