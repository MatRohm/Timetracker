namespace Timetracker.AzureDevOps;

/// <summary>
/// Add-in panel for the tracker tab: an issue-number box with an "Apply" button.
/// Fetching the work item runs asynchronously; results are pushed into the
/// tracker inputs through <see cref="ITrackerUiHost"/>. View-only.
/// </summary>
public sealed class AzureDevOpsPanel : UserControl
{
    private readonly AzureDevOpsService _service;
    private readonly ITrackerUiHost _host;

    private readonly TextBox _issueBox = new();
    private readonly Button _applyButton = new();
    private readonly Label _statusLabel = new();

    /// <summary>Suppresses concurrent apply attempts while a request is running.</summary>
    private bool _busy;

    public AzureDevOpsPanel(AzureDevOpsService service, ITrackerUiHost host)
    {
        _service = service;
        _host = host;
        Dock = DockStyle.Fill;

        BuildUi();
        UpdateStatusHint();
    }

    private void BuildUi()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 3,
            RowCount = 1,
            Margin = new Padding(0),
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var label = new Label
        {
            Text = "Azure DevOps issue",
            AutoSize = true,
            Margin = new Padding(0, 5, 8, 0),
        };

        _issueBox.Width = 90;
        _issueBox.Margin = new Padding(0, 3, 8, 0);
        _issueBox.KeyDown += OnIssueBoxKeyDown;

        _applyButton.Text = "⇩ Apply";
        _applyButton.AutoSize = true;
        _applyButton.Margin = new Padding(0, 2, 8, 0);

        _statusLabel.AutoSize = true;
        _statusLabel.ForeColor = Color.DimGray;
        _statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.Margin = new Padding(0, 5, 0, 0);

        layout.Controls.Add(label, 0, 0);
        layout.Controls.Add(_issueBox, 1, 0);
        layout.Controls.Add(_applyButton, 2, 0);
        layout.Controls.Add(_statusLabel, 3, 0);

        _applyButton.Click += async (_, _) => await ApplyAsync();

        Controls.Add(layout);
    }

    private void UpdateStatusHint()
    {
        _statusLabel.ForeColor = Color.DimGray;
        _statusLabel.Text = _service.IsConfigured
            ? "Fetches title and AZE-Element into the fields below."
            : "Not configured - create " + _service.ConfigFilePath
                + " (url, project, pat) and restart.";
    }

    private async Task ApplyAsync()
    {
        if (_busy)
        {
            return;
        }
        _busy = true;
        _applyButton.Enabled = false;
        try
        {
            var result = await _service.ApplyIssueAsync(_issueBox.Text, _host);
            ShowResult(result);
        }
        finally
        {
            _busy = false;
            _applyButton.Enabled = true;
        }
    }

    private void ShowResult(AzureDevOpsService.ApplyResult result)
    {
        if (result.Success)
        {
            _statusLabel.ForeColor = Color.ForestGreen;
            _statusLabel.Text = "✓ Applied \"" + result.TaskName + "\"";
        }
        else
        {
            _statusLabel.ForeColor = Color.Firebrick;
            _statusLabel.Text = "✗ " + result.Error;
        }
    }

    private void OnIssueBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            _ = ApplyAsync();
        }
    }
}
