using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Timetracker.App.ViewModels;

namespace Timetracker.App.Views;

/// <summary>
/// Options tab: one block per section with a label, a value box and, for paths, a
/// button that shows the path in the file explorer; Save stores the edited values.
/// View-only; state lives in <see cref="OptionsViewModel"/>.
/// </summary>
public sealed class OptionsTabView : UserControl
{
    private readonly OptionsViewModel _options;

    public OptionsTabView(OptionsViewModel options)
    {
        _options = options;

        var sections = new StackPanel { Spacing = 14 };
        foreach (var section in _options.Sections)
        {
            sections.Children.Add(BuildSection(section));
        }

        var saveButton = new Button
        {
            Content = "Save",
            Command = _options.SaveCommand,
        };
        var statusLabel = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
        statusLabel.Bind(TextBlock.TextProperty, new Binding
        {
            Source = _options,
            Path = nameof(OptionsViewModel.StatusText),
        });

        var footer = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            Margin = new Thickness(0, 8, 0, 0),
            Children = { saveButton, statusLabel },
        };
        DockPanel.SetDock(footer, Dock.Bottom);

        Content = new DockPanel
        {
            Margin = new Thickness(10),
            Children =
            {
                footer,
                new ScrollViewer { Content = sections },
            },
        };
    }

    private static Control BuildSection(OptionSection section)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            ColumnSpacing = 8,
            RowSpacing = 6,
        };

        for (var i = 0; i < section.Rows.Count; i++)
        {
            var row = section.Rows[i];
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            var label = new TextBlock
            {
                Text = row.Label,
                VerticalAlignment = VerticalAlignment.Center,
                MinWidth = 140,
            };
            Grid.SetRow(label, i);
            grid.Children.Add(label);

            var valueBox = new TextBox
            {
                IsReadOnly = row.IsReadOnly,
                PasswordChar = row.IsSecret ? '•' : default,
            };
            valueBox.Bind(TextBox.TextProperty, new Binding
            {
                Source = row,
                Path = nameof(OptionRowViewModel.Value),
                Mode = BindingMode.TwoWay,
            });
            Grid.SetRow(valueBox, i);
            Grid.SetColumn(valueBox, 1);
            grid.Children.Add(valueBox);

            if (row.IsPath)
            {
                var explorerButton = new Button
                {
                    Content = BuildExplorerIcon(),
                    Command = row.ShowInExplorerCommand,
                    Name = "ShowInExplorerButton",
                };
                ToolTip.SetTip(explorerButton, "Show in explorer");
                AutomationProperties.SetName(explorerButton, "Show in explorer");
                Grid.SetRow(explorerButton, i);
                Grid.SetColumn(explorerButton, 2);
                grid.Children.Add(explorerButton);
            }
        }

        var result = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                new TextBlock { Text = section.Title, FontSize = 15, FontWeight = FontWeight.Bold },
                grid,
            },
        };
        return result;
    }

    /// <summary>A small flat amber folder, the file-explorer symbol; drawn so it needs no icon font.</summary>
    private static Control BuildExplorerIcon() => new Avalonia.Controls.Shapes.Path
    {
        Data = Geometry.Parse("M2,4 L6,4 L8,6 L14,6 L14,13 L2,13 Z"),
        Fill = new SolidColorBrush(Color.FromRgb(0xFF, 0xC1, 0x07)),
        Width = 16,
        Height = 16,
        VerticalAlignment = VerticalAlignment.Center,
    };
}
