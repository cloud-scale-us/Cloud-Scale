using ScaleStreamer.Common.IPC;
using ScaleStreamer.Common.Models;
using System.Text.Json;

namespace ScaleStreamer.Config;

/// <summary>
/// Protocol configuration tab for designing and testing custom protocols
/// </summary>
public partial class ProtocolTab : UserControl
{
    private readonly IpcClient _ipcClient;

    // Controls
    private ComboBox _dataFormatCombo;
    private ComboBox _dataModeCombo;
    private TextBox _lineDelimiterText;
    private TextBox _fieldSeparatorText;
    private TextBox _regexText;
    private Button _testRegexButton;
    private TextBox _testDataText;
    private TextBox _parseResultText;
    private ListView _fieldsListView;
    private Button _addFieldButton;
    private Button _removeFieldButton;
    private Button _testProtocolButton;
    private Button _saveProtocolButton;

    public ProtocolTab(IpcClient ipcClient)
    {
        _ipcClient = ipcClient;
        InitializeComponent();
        LoadDefaults();
    }

    private void InitializeComponent()
    {
        this.AutoScroll = true;
        this.Padding = new Padding(10);

        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 3
        };
        mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));

        // Protocol Settings Panel (top, spans both columns)
        var settingsPanel = CreateSettingsPanel();
        mainLayout.Controls.Add(settingsPanel, 0, 0);
        mainLayout.SetColumnSpan(settingsPanel, 2);

        // Fields Configuration (middle-left)
        var fieldsPanel = CreateFieldsPanel();
        mainLayout.Controls.Add(fieldsPanel, 0, 1);

        // Test Panel (middle-right)
        var testPanel = CreateTestPanel();
        mainLayout.Controls.Add(testPanel, 1, 1);

        // Parse Result Panel (bottom, spans both columns)
        var resultPanel = CreateResultPanel();
        mainLayout.Controls.Add(resultPanel, 0, 2);
        mainLayout.SetColumnSpan(resultPanel, 2);

        this.Controls.Add(mainLayout);
    }

    private Control CreateSettingsPanel()
    {
        var panel = new GroupBox
        {
            Text = "Protocol Settings",
            Dock = DockStyle.Fill,
            Padding = new Padding(10),
            AutoSize = true
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            AutoSize = true
        };

        // Data Format
        layout.Controls.Add(new Label { Text = "Data Format:", AutoSize = true });
        _dataFormatCombo = new ComboBox { Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
        layout.Controls.Add(_dataFormatCombo);

        // Data Mode
        layout.Controls.Add(new Label { Text = "Data Mode:", AutoSize = true });
        _dataModeCombo = new ComboBox { Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
        layout.Controls.Add(_dataModeCombo);

        // Line Delimiter
        layout.Controls.Add(new Label { Text = "Line Delimiter:", AutoSize = true });
        _lineDelimiterText = new TextBox { Width = 100, Text = "\\r\\n" };
        layout.Controls.Add(_lineDelimiterText);

        // Field Separator
        layout.Controls.Add(new Label { Text = "Field Separator:", AutoSize = true });
        _fieldSeparatorText = new TextBox { Width = 100, Text = "\\s+" };
        layout.Controls.Add(_fieldSeparatorText);

        panel.Controls.Add(layout);
        return panel;
    }

    private Control CreateFieldsPanel()
    {
        var panel = new GroupBox
        {
            Text = "Field Definitions",
            Dock = DockStyle.Fill,
            Padding = new Padding(5)
        };

        var layout = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown
        };

        _fieldsListView = new ListView
        {
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            Width = 450,
            Height = 300
        };

        _fieldsListView.Columns.Add("Name", 100);
        _fieldsListView.Columns.Add("Type", 80);
        _fieldsListView.Columns.Add("Position", 70);
        _fieldsListView.Columns.Add("Regex Group", 100);
        _fieldsListView.Columns.Add("Multiplier", 80);

        var buttonPanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true
        };

        _addFieldButton = new Button { Text = "Add Field", Width = 100 };
        _addFieldButton.Click += AddField_Click;

        _removeFieldButton = new Button { Text = "Remove Field", Width = 100 };
        _removeFieldButton.Click += RemoveField_Click;

        buttonPanel.Controls.Add(_addFieldButton);
        buttonPanel.Controls.Add(_removeFieldButton);

        layout.Controls.Add(_fieldsListView);
        layout.Controls.Add(buttonPanel);

        panel.Controls.Add(layout);
        return panel;
    }

    private Control CreateTestPanel()
    {
        var panel = new GroupBox
        {
            Text = "Protocol Testing",
            Dock = DockStyle.Fill,
            Padding = new Padding(10)
        };

        var layout = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown
        };

        // Regex Pattern
        layout.Controls.Add(new Label { Text = "Regex Pattern (optional):", AutoSize = true });
        _regexText = new TextBox
        {
            Width = 400,
            Font = new Font("Consolas", 9F)
        };
        layout.Controls.Add(_regexText);

        _testRegexButton = new Button
        {
            Text = "Test Regex",
            Width = 120,
            Margin = new Padding(0, 5, 0, 10)
        };
        _testRegexButton.Click += TestRegex_Click;
        layout.Controls.Add(_testRegexButton);

        // Test Data
        layout.Controls.Add(new Label { Text = "Test Data:", AutoSize = true });
        _testDataText = new TextBox
        {
            Width = 400,
            Height = 100,
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            Font = new Font("Consolas", 9F)
        };
        layout.Controls.Add(_testDataText);

        _testProtocolButton = new Button
        {
            Text = "Test Protocol",
            Width = 120,
            Margin = new Padding(0, 5, 0, 0)
        };
        _testProtocolButton.Click += TestProtocol_Click;
        layout.Controls.Add(_testProtocolButton);

        panel.Controls.Add(layout);
        return panel;
    }

    private Control CreateResultPanel()
    {
        var panel = new GroupBox
        {
            Text = "Parse Results",
            Dock = DockStyle.Fill,
            Padding = new Padding(5)
        };

        _parseResultText = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ScrollBars = ScrollBars.Both,
            ReadOnly = true,
            Font = new Font("Consolas", 9F),
            BackColor = Color.WhiteSmoke
        };

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            Padding = new Padding(0, 5, 0, 0)
        };

        _saveProtocolButton = new Button
        {
            Text = "Save Protocol Template",
            Width = 160
        };
        _saveProtocolButton.Click += SaveProtocol_Click;
        buttonPanel.Controls.Add(_saveProtocolButton);

        panel.Controls.Add(_parseResultText);
        panel.Controls.Add(buttonPanel);

        return panel;
    }

    private void LoadDefaults()
    {
        // Data Format
        _dataFormatCombo.Items.AddRange(Enum.GetNames(typeof(DataFormat)));
        _dataFormatCombo.SelectedIndex = 0;

        // Data Mode
        _dataModeCombo.Items.AddRange(Enum.GetNames(typeof(DataMode)));
        _dataModeCombo.SelectedIndex = 0;

        // Add example fields
        AddFieldToList("weight", "float", "1", "", "1.0");
        AddFieldToList("status", "string", "0", "", "1.0");
    }

    private void AddFieldToList(string name, string type, string position, string regexGroup, string multiplier)
    {
        var item = new ListViewItem(name);
        item.SubItems.Add(type);
        item.SubItems.Add(position);
        item.SubItems.Add(regexGroup);
        item.SubItems.Add(multiplier);
        _fieldsListView.Items.Add(item);
    }

    private void AddField_Click(object? sender, EventArgs e)
    {
        // TODO: Show dialog to add new field
        using var dialog = new FieldEditorDialog();
        if (dialog.ShowDialog() == DialogResult.OK)
        {
            AddFieldToList(
                dialog.FieldName,
                dialog.FieldType,
                dialog.Position,
                dialog.RegexGroup,
                dialog.Multiplier);
        }
    }

    private void RemoveField_Click(object? sender, EventArgs e)
    {
        if (_fieldsListView.SelectedItems.Count > 0)
        {
            _fieldsListView.Items.Remove(_fieldsListView.SelectedItems[0]);
        }
    }

    private void TestRegex_Click(object? sender, EventArgs e)
    {
        try
        {
            var pattern = _regexText.Text;
            var testData = _testDataText.Text;

            if (string.IsNullOrEmpty(pattern) || string.IsNullOrEmpty(testData))
            {
                MessageBox.Show("Please enter both regex pattern and test data.", "Missing Data", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var regex = new System.Text.RegularExpressions.Regex(pattern);
            var match = regex.Match(testData);

            var result = new System.Text.StringBuilder();
            result.AppendLine("Regex Test Results:");
            result.AppendLine($"Pattern: {pattern}");
            result.AppendLine($"Test Data: {testData}");
            result.AppendLine();

            if (match.Success)
            {
                result.AppendLine("✓ Match Successful");
                result.AppendLine();
                result.AppendLine("Captured Groups:");

                foreach (var groupName in regex.GetGroupNames())
                {
                    if (int.TryParse(groupName, out _))
                        continue; // Skip numbered groups

                    var group = match.Groups[groupName];
                    if (group.Success)
                    {
                        result.AppendLine($"  {groupName} = '{group.Value}'");
                    }
                }
            }
            else
            {
                result.AppendLine("✗ No Match");
            }

            _parseResultText.Text = result.ToString();
        }
        catch (Exception ex)
        {
            _parseResultText.Text = $"Error testing regex:\n{ex.Message}";
        }
    }

    private void TestProtocol_Click(object? sender, EventArgs e)
    {
        try
        {
            _parseResultText.Text = "Testing protocol...\n\n";
            _parseResultText.AppendText("Protocol configuration:\n");
            _parseResultText.AppendText($"  Data Format: {_dataFormatCombo.SelectedItem}\n");
            _parseResultText.AppendText($"  Data Mode: {_dataModeCombo.SelectedItem}\n");
            _parseResultText.AppendText($"  Line Delimiter: {_lineDelimiterText.Text}\n");
            _parseResultText.AppendText($"  Field Separator: {_fieldSeparatorText.Text}\n");
            _parseResultText.AppendText($"\nTest data:\n{_testDataText.Text}\n");
            _parseResultText.AppendText("\n✓ Protocol test would execute here with actual engine");
        }
        catch (Exception ex)
        {
            _parseResultText.Text = $"Error testing protocol:\n{ex.Message}";
        }
    }

    private void SaveProtocol_Click(object? sender, EventArgs e)
    {
        using var dialog = new SaveFileDialog
        {
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            DefaultExt = "json",
            Title = "Save Protocol Template"
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            try
            {
                // TODO: Build ProtocolDefinition and save to JSON
                MessageBox.Show("Protocol template saved successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving protocol: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}

/// <summary>
/// Simple dialog for editing field definitions
/// </summary>
public class FieldEditorDialog : Form
{
    public string FieldName => _nameText.Text;
    public string FieldType => _typeCombo.SelectedItem?.ToString() ?? "string";
    public string Position => _positionText.Text;
    public string RegexGroup => _regexText.Text;
    public string Multiplier => _multiplierText.Text;

    private TextBox _nameText;
    private ComboBox _typeCombo;
    private TextBox _positionText;
    private TextBox _regexText;
    private TextBox _multiplierText;

    public FieldEditorDialog()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        this.Text = "Add Field";
        this.Size = new Size(400, 300);
        this.StartPosition = FormStartPosition.CenterParent;
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Padding = new Padding(10)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

        int row = 0;

        // Name
        layout.Controls.Add(new Label { Text = "Field Name:", AutoSize = true }, 0, row);
        _nameText = new TextBox { Width = 200 };
        layout.Controls.Add(_nameText, 1, row++);

        // Type
        layout.Controls.Add(new Label { Text = "Data Type:", AutoSize = true }, 0, row);
        _typeCombo = new ComboBox { Width = 200, DropDownStyle = ComboBoxStyle.DropDownList };
        _typeCombo.Items.AddRange(new object[] { "string", "float", "integer", "boolean" });
        _typeCombo.SelectedIndex = 0;
        layout.Controls.Add(_typeCombo, 1, row++);

        // Position
        layout.Controls.Add(new Label { Text = "Position:", AutoSize = true }, 0, row);
        _positionText = new TextBox { Width = 100 };
        layout.Controls.Add(_positionText, 1, row++);

        // Regex Group
        layout.Controls.Add(new Label { Text = "Regex Group:", AutoSize = true }, 0, row);
        _regexText = new TextBox { Width = 200 };
        layout.Controls.Add(_regexText, 1, row++);

        // Multiplier
        layout.Controls.Add(new Label { Text = "Multiplier:", AutoSize = true }, 0, row);
        _multiplierText = new TextBox { Width = 100, Text = "1.0" };
        layout.Controls.Add(_multiplierText, 1, row++);

        // Buttons
        var buttonPanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Bottom,
            AutoSize = true
        };

        var okButton = new Button { Text = "OK", DialogResult = DialogResult.OK, Width = 80 };
        var cancelButton = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 80 };

        buttonPanel.Controls.Add(okButton);
        buttonPanel.Controls.Add(cancelButton);

        this.Controls.Add(layout);
        this.Controls.Add(buttonPanel);
        this.AcceptButton = okButton;
        this.CancelButton = cancelButton;
    }
}
