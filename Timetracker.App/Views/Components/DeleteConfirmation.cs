using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Timetracker.Views.Components;

/// <summary>
/// Modal confirmations shared by the tracker views. Kept out of the components so
/// the same dialog style is used wherever a destructive action is confirmed.
/// </summary>
internal static class DeleteConfirmation
{
    /// <summary>
    /// Asks the user to confirm deleting the described entries. Returns false when
    /// the component is not hosted in a window (nothing can be shown).
    /// </summary>
    public static async Task<bool> ConfirmAsync(Control ownerControl, string summary)
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
}
