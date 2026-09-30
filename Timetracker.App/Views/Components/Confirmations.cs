using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Timetracker.App.Views.Components;

/// <summary>
/// Modal confirmations shared by the tracker views. Kept out of the components so
/// the same dialog style is used wherever stored sessions are deleted or rewritten.
/// </summary>
internal static class Confirmations
{
    /// <summary>
    /// Asks the user to confirm deleting the described entries. Returns false when
    /// the component is not hosted in a window (nothing can be shown).
    /// </summary>
    public static async Task<bool> ConfirmDeleteAsync(Control ownerControl, string summary)
    {
        if (TopLevel.GetTopLevel(ownerControl) is not Window owner)
        {
            return false;
        }

        var dialog = new Window
        {
            Title = "Timetracker",
            Width = 380,
            Height = 180,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };

        var message = new TextBlock
        {
            Text = $"Delete {summary}?\n\nThis removes the sessions from the JSON file and cannot be undone.",
            TextWrapping = TextWrapping.Wrap,
        };

        var yes = new Button { Content = "Yes" };
        yes.Click += (_, _) => dialog.Close(true);
        var no = new Button { Content = "No", IsCancel = true };
        no.Click += (_, _) => dialog.Close(false);

        dialog.Content = new StackPanel
        {
            Margin = new Thickness(16),
            Spacing = 12,
            Children =
            {
                message,
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
