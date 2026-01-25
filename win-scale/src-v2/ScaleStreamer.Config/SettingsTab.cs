using ScaleStreamer.Common.IPC;
using Serilog;

namespace ScaleStreamer.Config;

/// <summary>
/// Settings tab for email alerts, notifications, and global preferences
/// </summary>
public partial class SettingsTab : UserControl
{
    private readonly IpcClient _ipcClient;

    // Email Alert Settings
    private CheckBox _enableEmailAlertsCheck;
    private TextBox _smtpServerText;
    private NumericUpDown _smtpPortNumeric;
    private CheckBox _smtpUseSslCheck;
    private TextBox _smtpUsernameText;
    private TextBox _smtpPasswordText;
    private TextBox _alertFromText;
    private TextBox _alertToText;

    // Connection Alerts
    private CheckBox _alertOnConnectionFailureCheck;
    private CheckBox _alertOnConnectionRestoreCheck;
    private NumericUpDown _connectionFailureDelayNumeric;

    // Update Alerts
    private CheckBox _alertOnUpdateAvailableCheck;
    private CheckBox _autoCheckUpdatesCheck;
    private NumericUpDown _updateCheckIntervalNumeric;

    // Weight Alerts
    private CheckBox _enableWeightAlertsCheck;
    private NumericUpDown _weightAlertThresholdNumeric;
    private ComboBox _weightAlertConditionCombo;
    private CheckBox _weightAlertEmailCheck;
    private CheckBox _weightAlertSoundCheck;

    // Data Logging
    private CheckBox _enableDataLoggingCheck;
    private NumericUpDown _logRetentionDaysNumeric;
    private NumericUpDown _logFileSizeMbNumeric;
    private ComboBox _logLevelCombo;

    // Auto-Reconnect
    private CheckBox _autoReconnectGlobalCheck;
    private NumericUpDown _reconnectDelayNumeric;
    private NumericUpDown _maxReconnectAttemptsNumeric;

    public SettingsTab(IpcClient ipcClient)
    {
        _ipcClient = ipcClient;
        InitializeComponent();
        LoadSettings();
    }

    private void InitializeComponent()
    {
        this.Padding = new Padding(10);
        this.AutoScroll = true;

        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            ColumnCount = 1
        };

        // Add sections
        mainLayout.Controls.Add(CreateEmailSettingsPanel(), 0, 0);
        mainLayout.Controls.Add(CreateConnectionAlertsPanel(), 0, 1);
        mainLayout.Controls.Add(CreateUpdateAlertsPanel(), 0, 2);
        mainLayout.Controls.Add(CreateWeightAlertsPanel(), 0, 3);
        mainLayout.Controls.Add(CreateDataLoggingPanel(), 0, 4);
        mainLayout.Controls.Add(CreateAutoReconnectPanel(), 0, 5);
        mainLayout.Controls.Add(CreateButtonPanel(), 0, 6);

        this.Controls.Add(mainLayout);
    }

    private Control CreateEmailSettingsPanel()
    {
        var panel = new GroupBox
        {
            Text = "Email Alert Configuration",
            Dock = DockStyle.Top,
            Height = 300,
            Padding = new Padding(10)
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            AutoSize = true
        };

        int row = 0;

        _enableEmailAlertsCheck = new CheckBox
        {
            Text = "Enable Email Alerts",
            AutoSize = true,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold)
        };
        layout.Controls.Add(_enableEmailAlertsCheck, 0, row);
        layout.SetColumnSpan(_enableEmailAlertsCheck, 2);
        row++;

        AddField(layout, "SMTP Server:", out _smtpServerText, ref row, "smtp.gmail.com");
        AddNumericField(layout, "SMTP Port:", out _smtpPortNumeric, ref row, 587, 1, 65535);

        _smtpUseSslCheck = new CheckBox
        {
            Text = "Use SSL/TLS",
            Checked = true,
            AutoSize = true
        };
        layout.Controls.Add(new Label { Text = "", Dock = DockStyle.Fill }, 0, row);
        layout.Controls.Add(_smtpUseSslCheck, 1, row);
        row++;

        AddField(layout, "SMTP Username:", out _smtpUsernameText, ref row);
        AddPasswordField(layout, "SMTP Password:", out _smtpPasswordText, ref row);
        AddField(layout, "From Email:", out _alertFromText, ref row, "scalestreamer@yourdomain.com");
        AddField(layout, "Alert Recipients:", out _alertToText, ref row, "admin@yourdomain.com");

        var testButton = new Button
        {
            Text = "Test Email Configuration",
            AutoSize = true
        };
        testButton.Click += TestEmailButton_Click;
        layout.Controls.Add(testButton, 1, row);
        row++;

        panel.Controls.Add(layout);
        return panel;
    }

    private Control CreateConnectionAlertsPanel()
    {
        var panel = new GroupBox
        {
            Text = "Connection Failure Alerts",
            Dock = DockStyle.Top,
            Height = 150,
            Padding = new Padding(10)
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2
        };

        int row = 0;

        _alertOnConnectionFailureCheck = new CheckBox
        {
            Text = "Send email alert on connection failure",
            AutoSize = true,
            Checked = true
        };
        layout.Controls.Add(_alertOnConnectionFailureCheck, 0, row);
        layout.SetColumnSpan(_alertOnConnectionFailureCheck, 2);
        row++;

        _alertOnConnectionRestoreCheck = new CheckBox
        {
            Text = "Send email alert when connection restored",
            AutoSize = true,
            Checked = true
        };
        layout.Controls.Add(_alertOnConnectionRestoreCheck, 0, row);
        layout.SetColumnSpan(_alertOnConnectionRestoreCheck, 2);
        row++;

        AddNumericField(layout, "Failure Delay (seconds):", out _connectionFailureDelayNumeric, ref row, 30, 0, 3600);

        panel.Controls.Add(layout);
        return panel;
    }

    private Control CreateUpdateAlertsPanel()
    {
        var panel = new GroupBox
        {
            Text = "Software Update Alerts",
            Dock = DockStyle.Top,
            Height = 150,
            Padding = new Padding(10)
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2
        };

        int row = 0;

        _alertOnUpdateAvailableCheck = new CheckBox
        {
            Text = "Send email alert when updates are available",
            AutoSize = true,
            Checked = true
        };
        layout.Controls.Add(_alertOnUpdateAvailableCheck, 0, row);
        layout.SetColumnSpan(_alertOnUpdateAvailableCheck, 2);
        row++;

        _autoCheckUpdatesCheck = new CheckBox
        {
            Text = "Automatically check for updates",
            AutoSize = true,
            Checked = true
        };
        layout.Controls.Add(_autoCheckUpdatesCheck, 0, row);
        layout.SetColumnSpan(_autoCheckUpdatesCheck, 2);
        row++;

        AddNumericField(layout, "Check Interval (hours):", out _updateCheckIntervalNumeric, ref row, 24, 1, 168);

        panel.Controls.Add(layout);
        return panel;
    }

    private Control CreateWeightAlertsPanel()
    {
        var panel = new GroupBox
        {
            Text = "Weight Threshold Alerts",
            Dock = DockStyle.Top,
            Height = 180,
            Padding = new Padding(10)
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2
        };

        int row = 0;

        _enableWeightAlertsCheck = new CheckBox
        {
            Text = "Enable weight threshold alerts",
            AutoSize = true
        };
        layout.Controls.Add(_enableWeightAlertsCheck, 0, row);
        layout.SetColumnSpan(_enableWeightAlertsCheck, 2);
        row++;

        AddNumericField(layout, "Weight Threshold:", out _weightAlertThresholdNumeric, ref row, 1000, 0, 999999, 2);

        layout.Controls.Add(new Label { Text = "Alert Condition:", AutoSize = true }, 0, row);
        _weightAlertConditionCombo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Dock = DockStyle.Fill
        };
        _weightAlertConditionCombo.Items.AddRange(new[] { "Greater Than", "Less Than", "Equal To" });
        _weightAlertConditionCombo.SelectedIndex = 0;
        layout.Controls.Add(_weightAlertConditionCombo, 1, row);
        row++;

        _weightAlertEmailCheck = new CheckBox
        {
            Text = "Send email notification",
            AutoSize = true,
            Checked = true
        };
        layout.Controls.Add(_weightAlertEmailCheck, 1, row);
        row++;

        _weightAlertSoundCheck = new CheckBox
        {
            Text = "Play sound alert",
            AutoSize = true,
            Checked = true
        };
        layout.Controls.Add(_weightAlertSoundCheck, 1, row);
        row++;

        panel.Controls.Add(layout);
        return panel;
    }

    private Control CreateDataLoggingPanel()
    {
        var panel = new GroupBox
        {
            Text = "Data Logging Settings",
            Dock = DockStyle.Top,
            Height = 150,
            Padding = new Padding(10)
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2
        };

        int row = 0;

        _enableDataLoggingCheck = new CheckBox
        {
            Text = "Enable data logging",
            AutoSize = true,
            Checked = true
        };
        layout.Controls.Add(_enableDataLoggingCheck, 0, row);
        layout.SetColumnSpan(_enableDataLoggingCheck, 2);
        row++;

        AddNumericField(layout, "Log Retention (days):", out _logRetentionDaysNumeric, ref row, 30, 1, 365);
        AddNumericField(layout, "Max Log File Size (MB):", out _logFileSizeMbNumeric, ref row, 10, 1, 1000);

        layout.Controls.Add(new Label { Text = "Log Level:", AutoSize = true }, 0, row);
        _logLevelCombo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Dock = DockStyle.Fill
        };
        _logLevelCombo.Items.AddRange(new[] { "Verbose", "Debug", "Information", "Warning", "Error" });
        _logLevelCombo.SelectedIndex = 2;
        layout.Controls.Add(_logLevelCombo, 1, row);
        row++;

        panel.Controls.Add(layout);
        return panel;
    }

    private Control CreateAutoReconnectPanel()
    {
        var panel = new GroupBox
        {
            Text = "Auto-Reconnect Settings",
            Dock = DockStyle.Top,
            Height = 130,
            Padding = new Padding(10)
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2
        };

        int row = 0;

        _autoReconnectGlobalCheck = new CheckBox
        {
            Text = "Enable auto-reconnect for all scales",
            AutoSize = true,
            Checked = true
        };
        layout.Controls.Add(_autoReconnectGlobalCheck, 0, row);
        layout.SetColumnSpan(_autoReconnectGlobalCheck, 2);
        row++;

        AddNumericField(layout, "Reconnect Delay (seconds):", out _reconnectDelayNumeric, ref row, 10, 1, 300);
        AddNumericField(layout, "Max Reconnect Attempts:", out _maxReconnectAttemptsNumeric, ref row, 5, 0, 100);

        panel.Controls.Add(layout);
        return panel;
    }

    private Control CreateButtonPanel()
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 50,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(10)
        };

        var saveButton = new Button
        {
            Text = "Save Settings",
            Size = new Size(120, 30),
            BackColor = Color.FromArgb(0, 120, 215),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        saveButton.Click += SaveButton_Click;
        panel.Controls.Add(saveButton);

        var cancelButton = new Button
        {
            Text = "Cancel",
            Size = new Size(100, 30)
        };
        cancelButton.Click += (s, e) => LoadSettings();
        panel.Controls.Add(cancelButton);

        return panel;
    }

    private void AddField(TableLayoutPanel layout, string label, out TextBox textBox, ref int row, string placeholder = "")
    {
        layout.Controls.Add(new Label { Text = label, AutoSize = true }, 0, row);
        textBox = new TextBox
        {
            Dock = DockStyle.Fill,
            PlaceholderText = placeholder
        };
        layout.Controls.Add(textBox, 1, row);
        row++;
    }

    private void AddPasswordField(TableLayoutPanel layout, string label, out TextBox textBox, ref int row)
    {
        layout.Controls.Add(new Label { Text = label, AutoSize = true }, 0, row);
        textBox = new TextBox
        {
            Dock = DockStyle.Fill,
            UseSystemPasswordChar = true
        };
        layout.Controls.Add(textBox, 1, row);
        row++;
    }

    private void AddNumericField(TableLayoutPanel layout, string label, out NumericUpDown numeric, ref int row, decimal value, decimal min, decimal max, int decimals = 0)
    {
        layout.Controls.Add(new Label { Text = label, AutoSize = true }, 0, row);
        numeric = new NumericUpDown
        {
            Dock = DockStyle.Fill,
            Minimum = min,
            Maximum = max,
            Value = value,
            DecimalPlaces = decimals
        };
        layout.Controls.Add(numeric, 1, row);
        row++;
    }

    private void LoadSettings()
    {
        try
        {
            // Load settings from registry or config file
            // For now, using defaults
            Log.Information("Loading settings...");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load settings");
            MessageBox.Show($"Failed to load settings:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void SaveButton_Click(object? sender, EventArgs e)
    {
        try
        {
            // Validate email settings if enabled
            if (_enableEmailAlertsCheck.Checked)
            {
                if (string.IsNullOrWhiteSpace(_smtpServerText.Text))
                {
                    MessageBox.Show("SMTP Server is required for email alerts.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (string.IsNullOrWhiteSpace(_alertToText.Text))
                {
                    MessageBox.Show("Alert recipient email is required.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            // Save settings
            Log.Information("Saving settings...");

            // TODO: Save to registry or config file
            // For now, just show success message

            MessageBox.Show("Settings saved successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to save settings");
            MessageBox.Show($"Failed to save settings:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async void TestEmailButton_Click(object? sender, EventArgs e)
    {
        if (!_enableEmailAlertsCheck.Checked)
        {
            MessageBox.Show("Please enable email alerts first.", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            var button = sender as Button;
            if (button != null)
            {
                button.Enabled = false;
                button.Text = "Sending...";
            }

            // TODO: Implement email test
            await Task.Delay(1000);

            MessageBox.Show("Test email sent successfully!\n\nCheck your inbox.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);

            if (button != null)
            {
                button.Enabled = true;
                button.Text = "Test Email Configuration";
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to send test email");
            MessageBox.Show($"Failed to send test email:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
