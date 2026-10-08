using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Timetracker.App.Localization;
using Timetracker.App.ViewModels;
using Timetracker.Plugins.Contracts.Ui;

namespace Timetracker.App.Views;

/// <summary>
/// Options tab: one block per section with a label, a value box and, for paths, a
/// button that shows the path in the file explorer; Save stores the edited values.
/// View-only; state lives in <see cref="OptionsViewModel"/>.
/// </summary>
public sealed class OptionsTabView : UserControl
{
    private readonly OptionsViewModel _options;

    public OptionsTabView(
        OptionsViewModel options,
        IReadOnlyList<(string Section, Control Control)> optionUiContributions)
    {
        _options = options;

        var contributionsBySection = optionUiContributions
            .GroupBy(c => c.Section)
            .ToDictionary(g => g.Key, g => g.Select(c => c.Control).ToList());

        var sections = new StackPanel { Spacing = 14 };
        var renderedSections = new HashSet<string>(StringComparer.Ordinal);
        foreach (var section in _options.Sections)
        {
            renderedSections.Add(section.Title);
            contributionsBySection.TryGetValue(section.Title, out var controls);
            sections.Children.Add(BuildSection(section, controls));
        }

        // A contribution whose section has no option rows renders as its own section.
        foreach (var (section, control) in optionUiContributions)
        {
            if (!renderedSections.Add(section))
            {
                continue;
            }

            sections.Children.Add(BuildCustomSection(section, control));
        }

        var saveButton = new Button
        {
            Content = Strings.Options_Save,
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

    private static Control BuildSection(OptionSection section, IReadOnlyList<Control>? controls)
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
            Control labelContent = row.HasHint
                ? new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 4,
                    VerticalAlignment = VerticalAlignment.Center,
                    Children = { label, BuildHintIcon(row.HintText, row.Label) },
                }
                : label;
            Grid.SetRow(labelContent, i);
            grid.Children.Add(labelContent);

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
                ToolTip.SetTip(explorerButton, Strings.Options_ExplorerToolTip);
                AutomationProperties.SetName(explorerButton, Strings.Options_ExplorerToolTip);
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
        AppendControls(result, controls);
        return result;
    }

    private static Control BuildCustomSection(string title, Control control) =>
        new StackPanel
        {
            Spacing = 6,
            Children =
            {
                new TextBlock { Text = title, FontSize = 15, FontWeight = FontWeight.Bold },
                control,
            },
        };

    private static void AppendControls(StackPanel section, IReadOnlyList<Control>? controls)
    {
        if (controls is null)
        {
            return;
        }

        foreach (var control in controls)
        {
            section.Children.Add(control);
        }
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

    /// <summary>
    /// A small flat (i) symbol carrying the option's hint as its tooltip; drawn so it
    /// needs no icon font. A solid disc with the dot and stem knocked out (even-odd
    /// fill): a bare dot without the stem reads as a radio button, not an (i).
    /// </summary>
    private static Control BuildHintIcon(string hintText, string labelText)
    {
        var icon = new Viewbox
        {
            Width = 16,
            Height = 16,
            VerticalAlignment = VerticalAlignment.Center,
            Child = new Avalonia.Controls.Shapes.Path
            {
                Data = Geometry.Parse(
                    "M8,1.5 A6.5,6.5 0 1 1 8,14.5 A6.5,6.5 0 1 1 8,1.5 Z "
                    + "M8,4.9 A1.05,1.05 0 1 1 8,7.0 A1.05,1.05 0 1 1 8,4.9 Z "
                    + "M7.15,7.6 L8.85,7.6 L8.85,11.4 L7.15,11.4 Z"),
                Fill = ViewBrushes.Info,
            },
        };
        ToolTip.SetTip(icon, hintText);
        AutomationProperties.SetName(icon, string.Format(Strings.Options_HintAutomationName, labelText));
        var result = icon;
        return result;
    }
}
