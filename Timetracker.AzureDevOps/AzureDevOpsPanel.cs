using Timetracker.Plugins.Interfaces;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Timetracker.Plugins;

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

    /// <summary>Exposed so tests can assert the busy state.</summary>
    public Button ImportButton => _importButton;

    public AzureDevOpsPanel(AzureDevOpsService service, ITrackerUiHost host)
    {
        _service = service;
        _host = host;

        _importButton.Content = "Azure DevOps import";
        _importButton.Click += async (_, _) => await ShowImportPopupAsync();
        ToolTip.SetTip(_importButton, _service.IsConfigured
            ? "Fetches issue number, title and AZE-Element into the fields."
            : "Not configured - create " + _service.ConfigFilePath
                + " (url, project, pat) and restart.");

        var icon = LoadBitmap("Timetracker.AzureDevOps.azure-favicon.png", 16);
        if (icon is not null)
        {
            _importButton.Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                Children =
                {
                    new Image { Source = icon, Width = 16, Height = 16 },
                    new TextBlock
                    {
                        Text = "Azure DevOps import",
                        VerticalAlignment = VerticalAlignment.Center,
                    },
                },
            };
        }

        Content = _importButton;
    }

    /// <summary>Loads an embedded icon resource; null when it is missing.</summary>
    internal static Bitmap? LoadBitmap(string resourceName, int decodeWidth)
    {
        var stream = typeof(AzureDevOpsPanel).Assembly
            .GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            return null;
        }

        using (stream)
        {
            return Bitmap.DecodeToWidth(stream, decodeWidth);
        }
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
            return;
        }

        if (TopLevel.GetTopLevel(this) is not Window owner)
        {
            return;
        }

        var popup = new ImportWindow(LoadBitmap("Timetracker.AzureDevOps.azure-favicon.png", 32));
        var issueNumber = await popup.ShowDialog<string?>(owner);
        if (!string.IsNullOrWhiteSpace(issueNumber))
        {
            await ApplyAsync(issueNumber);
        }
    }

    private async Task ApplyAsync(string issueNumber)
    {
        if (_busy)
        {
            return;
        }
        _busy = true;
        _importButton.IsEnabled = false;
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
            _importButton.IsEnabled = true;
        }
    }

    /// <summary>
    /// Popup for entering the issue number: Enter (or the Import button) starts
    /// the import, Esc cancels. Closes with the trimmed number, or null on cancel.
    /// </summary>
    private sealed class ImportWindow : Window
    {
        private readonly TextBox _issueBox = new();
        private readonly TextBlock _errorText = new();

        public ImportWindow(Bitmap? icon)
        {
            Title = "Azure DevOps import";
            Width = 320;
            Height = 180;
            CanResize = false;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            if (icon is not null)
            {
                Icon = new WindowIcon(icon);
            }

            _issueBox.Watermark = "Issue number";

            var applyButton = new Button { Content = "Import" };
            applyButton.Click += (_, _) => Submit();

            var cancelButton = new Button { Content = "Cancel" };
            cancelButton.Click += (_, _) => Close(null);

            _errorText.Foreground = Brushes.Firebrick;

            Content = new StackPanel
            {
                Margin = new Thickness(16),
                Spacing = 8,
                Children =
                {
                    new TextBlock { Text = "Issue number:" },
                    _issueBox,
                    _errorText,
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Spacing = 8,
                        Children = { cancelButton, applyButton },
                    },
                },
            };

            // Enter submits, Esc cancels.
            _issueBox.KeyDown += (_, e) =>
            {
                if (e.Key == Avalonia.Input.Key.Enter)
                {
                    Submit();
                    e.Handled = true;
                }
            };
            KeyDown += (_, e) =>
            {
                if (e.Key == Avalonia.Input.Key.Escape)
                {
                    Close(null);
                    e.Handled = true;
                }
            };
        }

        /// <summary>Closes with the entered number, or keeps the dialog open when invalid.</summary>
        private void Submit()
        {
            var text = _issueBox.Text?.Trim() ?? "";
            if (text.Length == 0 || !int.TryParse(text, out var id) || id <= 0)
            {
                _errorText.Text = "Please enter a numeric issue number.";
                return;
            }
            Close(text);
        }
    }
}
