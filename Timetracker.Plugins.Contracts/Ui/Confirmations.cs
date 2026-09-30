using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Timetracker.Plugins.Contracts.Ui;

/// <summary>
/// Modal confirmations shared by the app and the week view, so the same dialog
/// style is used wherever stored sessions are deleted or rewritten.
/// </summary>
public static class Confirmations
{
    /// <summary>
    /// Asks the user to confirm deleting the described entries. Returns false when
    /// the component is not hosted in a window (nothing can be shown).
    /// </summary>
    public static async Task<bool> ConfirmDeleteAsync(Control ownerControl, string summary)
    {
        var message = $"Delete {summary}?\n\nThis removes the sessions from the JSON file and cannot be undone.";
        return await ConfirmAsync(ownerControl, message, acceptLabel: "Yes", cancelLabel: "No");
    }

    /// <summary>
    /// Shows a modal yes/no dialog with a single message and custom labels. Returns
    /// false when the component is not hosted in a window (nothing can be shown).
    /// </summary>
    public static async Task<bool> ConfirmAsync(
        Control ownerControl,
        string message,
        string acceptLabel,
        string cancelLabel = "Cancel")
    {
        if (TopLevel.GetTopLevel(ownerControl) is not Window owner)
        {
            return false;
        }

        var dialog = new Window
        {
            Title = "Timetracker",
            Width = 400,
            Height = 180,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };

        var text = new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
        };

        var yes = new Button { Content = acceptLabel };
        yes.Click += (_, _) => dialog.Close(true);
        var no = new Button { Content = cancelLabel, IsCancel = true };
        no.Click += (_, _) => dialog.Close(false);

        dialog.Content = new StackPanel
        {
            Margin = new Thickness(16),
            Spacing = 12,
            Children =
            {
                text,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8,
                    Children = { no, yes },
                },
            },
        };

        return await dialog.ShowDialog<bool>(owner);
    }

    /// <summary>
    /// Asks the user to confirm rewriting stored sessions, listing each change (e.g.
    /// "Report 09:00–10:15 → 09:00–10:44"). Returns false when the component is not
    /// hosted in a window (nothing can be shown).
    /// </summary>
    public static async Task<bool> ConfirmChangesAsync(Control ownerControl, string question, IReadOnlyList<string> changes)
    {
        if (TopLevel.GetTopLevel(ownerControl) is not Window owner)
        {
            return false;
        }

        var dialog = new Window
        {
            Title = "Timetracker",
            Width = 440,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };

        var lines = new StackPanel { Spacing = 2, Margin = new Thickness(8, 0, 0, 0) };
        foreach (var change in changes)
        {
            lines.Children.Add(new TextBlock { Text = "• " + change, TextWrapping = TextWrapping.Wrap });
        }

        var apply = new Button { Content = "Apply", IsDefault = true };
        apply.Click += (_, _) => dialog.Close(true);
        var cancel = new Button { Content = "Cancel", IsCancel = true };
        cancel.Click += (_, _) => dialog.Close(false);

        dialog.Content = new StackPanel
        {
            Margin = new Thickness(16),
            Spacing = 12,
            Children =
            {
                new TextBlock { Text = question, TextWrapping = TextWrapping.Wrap },
                lines,
                new TextBlock
                {
                    Text = "The sessions are rewritten in the JSON file.",
                    Foreground = Brushes.Gray,
                    TextWrapping = TextWrapping.Wrap,
                },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8,
                    Children = { cancel, apply },
                },
            },
        };

        return await dialog.ShowDialog<bool>(owner);
    }
}
