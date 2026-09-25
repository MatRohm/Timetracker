using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Timetracker.ViewModels;

namespace Timetracker.Views.Components;

/// <summary>
/// A funnel button for one grid column's header that toggles a filter box for that
/// column. Showing the box focuses it; hiding it clears the column's filter. The
/// filtering itself lives in <see cref="TrackerViewModel.SetColumnFilter"/>.
/// </summary>
public sealed class ColumnFilterHeader : UserControl
{
    private readonly TrackerViewModel _viewModel;
    private readonly string _column;
    private readonly TextBlock _caption = new();
    private readonly ToggleButton _toggle = new();
    private readonly TextBox _filterBox = new();

    /// <summary>The column property (<see cref="EntryRow"/> name) this header filters.</summary>
    public string Column => _column;

    /// <summary>The caption, including the sort indicator when set.</summary>
    public TextBlock Caption => _caption;

    /// <summary>The funnel toggle; exposed for tests.</summary>
    public ToggleButton Toggle => _toggle;

    /// <summary>The per-column filter box; exposed for tests.</summary>
    public TextBox FilterBox => _filterBox;

    public ColumnFilterHeader(TrackerViewModel viewModel, string column, string caption)
    {
        _viewModel = viewModel;
        _column = column;

        _caption.Text = caption;
        _caption.VerticalAlignment = VerticalAlignment.Center;

        _toggle.Content = BuildFunnelIcon();
        _toggle.Padding = new Thickness(4, 1);
        _toggle.VerticalAlignment = VerticalAlignment.Center;
        ToolTip.SetTip(_toggle, $"Filter by {caption}");
        _toggle.IsCheckedChanged += (_, _) => OnToggleChanged();

        _filterBox.Watermark = caption;
        _filterBox.MinWidth = 120;
        _filterBox.Margin = new Thickness(0, 2, 0, 2);
        _filterBox.IsVisible = false;

        var captionRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Children = { _caption, _toggle },
        };

        Content = new StackPanel
        {
            Children = { captionRow, _filterBox },
        };

        // Filter changes are pushed to the view model as the user types.
        _filterBox.TextChanged += (_, _) => _viewModel.SetColumnFilter(_column, _filterBox.Text ?? "");
    }

    /// <summary>Updates the caption text (used for the sort glyph).</summary>
    public void SetCaption(string caption) => _caption.Text = caption;

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
            // Hiding the box drops this column's filter.
            _filterBox.Text = "";
            _viewModel.SetColumnFilter(_column, "");
        }
    }
}
