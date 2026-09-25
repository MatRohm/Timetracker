using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Timetracker.ViewModels;

namespace Timetracker.Views.Components;

/// <summary>
/// The history filter: a funnel button that toggles a text box filtering the
/// history by task name or booking element. Showing the box focuses it; hiding it
/// clears the filter, so the full history comes back. Filtering itself lives in
/// <see cref="TrackerViewModel.FilterText"/>.
/// </summary>
public sealed class FilterField : UserControl
{
    private readonly TrackerViewModel _viewModel;
    private readonly ToggleButton _toggle = new();
    private readonly TextBox _filterBox = new();

    /// <summary>The filter text box; exposed for tests.</summary>
    public TextBox FilterBox => _filterBox;

    /// <summary>The funnel toggle; exposed for tests.</summary>
    public ToggleButton Toggle => _toggle;

    public FilterField(TrackerViewModel viewModel)
    {
        _viewModel = viewModel;

        _toggle.Content = BuildFunnelIcon();
        _toggle.Padding = new Thickness(6, 2);
        _toggle.VerticalAlignment = VerticalAlignment.Center;
        ToolTip.SetTip(_toggle, "Filter by task name or booking element");
        _toggle.IsCheckedChanged += (_, _) => OnToggleChanged();

        _filterBox.Watermark = "Filter task / booking element";
        _filterBox.Margin = new Thickness(4, 0, 0, 0);
        _filterBox.MinWidth = 220;
        _filterBox.VerticalAlignment = VerticalAlignment.Center;
        _filterBox.IsVisible = false;
        _filterBox.Bind(TextBox.TextProperty, new Binding
        {
            Source = _viewModel,
            Path = nameof(TrackerViewModel.FilterText),
            Mode = BindingMode.TwoWay,
        });

        Content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Children = { _toggle, _filterBox },
        };
    }

    /// <summary>A small drawn funnel, so the icon needs no icon font.</summary>
    private static Control BuildFunnelIcon() => new Avalonia.Controls.Shapes.Path
    {
        Data = Geometry.Parse("M0,0 L12,0 L7.5,5.5 L7.5,10 L4.5,11.5 L4.5,5.5 Z"),
        Fill = Brushes.Black,
        Width = 12,
        Height = 12,
        VerticalAlignment = VerticalAlignment.Center,
    };

    private void OnToggleChanged()
    {
        var show = _toggle.IsChecked == true;
        _filterBox.IsVisible = show;

        if (show)
        {
            _filterBox.Focus();
        }
        else
        {
            // Hiding the box drops the filter, so the full history comes back.
            _viewModel.FilterText = "";
        }
    }
}
