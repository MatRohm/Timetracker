namespace Timetracker.AzureDevOps;

/// <summary>
/// Add-in control for the tracker tab: a single button with the Azure DevOps
/// favicon. Clicking it opens a small popup where the user types the issue
/// number; fetching runs asynchronously and results are pushed into the tracker
/// inputs and the shared status line through <see cref="ITrackerUiHost"/>.
/// View-only.
/// </summary>
public sealed class AzureDevOpsPanel : UserControl
{
    private readonly AzureDevOpsService _service;
    private readonly ITrackerUiHost _host;

    private readonly Button _importButton = new();

    /// <summary>Suppresses concurrent apply attempts while a request is running.</summary>
    private bool _busy;

    /// <summary>The 16x16 Azure DevOps icon for the button.</summary>
    private static Icon AzureIcon => LoadIcon(16);

    /// <summary>The 32x32 icon for the popup's title bar.</summary>
    private static Icon PopupIcon => LoadIcon(32);

    private static Icon LoadIcon(int size)
    {
        var stream = typeof(AzureDevOpsPanel).Assembly.GetManifestResourceStream(
            "Timetracker.AzureDevOps.azure-favicon.ico");
        if (stream is null)
        {
            return SystemIcons.Application;
        }

        using (stream)
        {
            return new Icon(stream, size, size);
        }
    }

    public AzureDevOpsPanel(AzureDevOpsService service, ITrackerUiHost host)
    {
        _service = service;
        _host = host;
        // Size the panel to its button; the default UserControl size (200x100)
        // would clip the caption and inflate the button row's height.
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Margin = new Padding(0);
        Padding = new Padding(0);

        BuildUi();
    }

    private void BuildUi()
    {
        _importButton.Image = AzureIcon.ToBitmap();
        _importButton.ImageAlign = ContentAlignment.MiddleLeft;
        _importButton.TextImageRelation = TextImageRelation.ImageBeforeText;
        _importButton.Text = "Azure DevOps import";
        _importButton.AutoSize = true;
        // Same height as Start/Stop; width grows with the caption.
        _importButton.MinimumSize = new Size(170, 36);
        _importButton.Margin = new Padding(0);

        // Config hint goes to the tooltip; status messages live in the host's
        // shared status line at the bottom of the tab.
        UpdateToolTip();

        _importButton.Click += async (_, _) => await ShowImportPopupAsync();

        Controls.Add(_importButton);
    }

    private void UpdateToolTip()
    {
        var toolTip = new ToolTip();
        toolTip.SetToolTip(_importButton, _service.IsConfigured
            ? "Fetches issue number, title and AZE-Element into the fields."
            : "Not configured - create " + _service.ConfigFilePath
                + " (url, project, pat) and restart.");
    }

    private async Task ShowImportPopupAsync()
    {
        if (_busy)
        {
            return;
        }

        // Without a usable config the popup cannot succeed; explain instead.
        if (!_service.IsConfigured)
        {
            _host.ShowStatus("Azure DevOps is not configured - create "
                + _service.ConfigFilePath + " (url, project, pat).",
                TrackerStatusKind.Error);
            MessageBox.Show(this, _service.ConfigurationHint,
                "Azure DevOps import", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        using var popup = new ImportPopup(PopupIcon);
        if (popup.ShowDialog(FindForm()) == DialogResult.OK)
        {
            await ApplyAsync(popup.IssueNumber);
        }
    }

    private async Task ApplyAsync(string issueNumber)
    {
        if (_busy)
        {
            return;
        }
        _busy = true;
        _importButton.Enabled = false;
        try
        {
            var result = await _service.ApplyIssueAsync(issueNumber, _host);
            if (!result.Success)
            {
                _host.ShowStatus("✗ " + result.Error, TrackerStatusKind.Error);
            }
        }
        finally
        {
            _busy = false;
            _importButton.Enabled = true;
        }
    }

    // Popup for entering the issue number: OK (Enter or Apply button) starts the
    // import, Esc cancels.
    private sealed class ImportPopup : Form
    {
        private readonly TextBox _issueBox = new();
        private readonly Button _applyButton = new();
        private readonly Button _cancelButton = new();

        /// <summary>Typed issue number; only meaningful when DialogResult is OK.</summary>
        public string IssueNumber => _issueBox.Text;

        public ImportPopup(Icon icon)
        {
            Text = "Azure DevOps import";
            Icon = icon;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(300, 110);

            var label = new Label
            {
                Text = "Issue number:",
                AutoSize = true,
                Location = new Point(12, 12),
            };

            _issueBox.Location = new Point(15, 32);
            _issueBox.Size = new Size(270, 27);

            _applyButton.Text = "Import";
            _applyButton.Location = new Point(127, 70);
            _applyButton.Size = new Size(75, 28);

            _cancelButton.Text = "Cancel";
            _cancelButton.Location = new Point(210, 70);
            _cancelButton.Size = new Size(75, 28);
            _cancelButton.DialogResult = DialogResult.Cancel;

            _applyButton.Click += (_, _) =>
            {
                if (ValidateInput())
                {
                    DialogResult = DialogResult.OK;
                }
            };

            Controls.Add(label);
            Controls.Add(_issueBox);
            Controls.Add(_applyButton);
            Controls.Add(_cancelButton);

            AcceptButton = _applyButton;
            CancelButton = _cancelButton;
        }

        private bool ValidateInput()
        {
            var text = _issueBox.Text.Trim();
            if (text.Length == 0 || !int.TryParse(text, out var id) || id <= 0)
            {
                MessageBox.Show(this, "Please enter a numeric issue number.", "Azure DevOps import",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            return true;
        }
    }
}
