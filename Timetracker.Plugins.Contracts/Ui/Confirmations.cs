using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Timetracker.Plugins.Contracts.Localization;

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
        var message = string.Format(Strings.Confirm_Delete, summary);
        return await ConfirmAsync(ownerControl, message, acceptLabel: Strings.Confirm_Yes, cancelLabel: Strings.Confirm_No);
    }

    /// <summary>
    /// Shows a modal yes/no dialog with a single message and custom labels. Returns
    /// false when the component is not hosted in a window (nothing can be shown).
    /// </summary>
    public static async Task<bool> ConfirmAsync(
        Control ownerControl,
        string message,
        string acceptLabel,
        // Default parameter values cannot be resources; callers pass Strings.Confirm_Cancel.
        string cancelLabel = "Cancel")
    {
        if (TopLevel.GetTopLevel(ownerControl) is not Window owner)
        {
            return false;
        }

        var dialog = new Window
        {
            Title = Strings.Confirm_Title,
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
            Title = Strings.Confirm_Title,
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

        var apply = new Button { Content = Strings.Confirm_Apply, IsDefault = true };
        apply.Click += (_, _) => dialog.Close(true);
        var cancel = new Button { Content = Strings.Confirm_Cancel, IsCancel = true };
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
                    Text = Strings.Confirm_Rewritten,
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
