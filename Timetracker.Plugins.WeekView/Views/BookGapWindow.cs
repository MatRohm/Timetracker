using Avalonia;
using Timetracker.Plugins.WeekView.Localization;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Timetracker.Plugins.Contracts.ViewModels;
using Timetracker.Plugins.WeekView.ViewModels;

namespace Timetracker.Plugins.WeekView.Views;

/// <summary>
/// Dialog that books an untracked gap from the week view as a session: editable
/// start and end, a task name with suggestions and an optional booking element.
/// View-only; the state lives in <see cref="BookGapViewModel"/>. Closes with true
/// when the user books, false when cancelled; the caller saves.
/// </summary>
public sealed class BookGapWindow : Window
{
    private readonly ListBox _suggestionList = new() { MaxHeight = 140 };

    public BookGapWindow(BookGapViewModel viewModel)
    {
        ViewModel = viewModel;

        Title = Strings.BookGap_Title;
        Width = 460;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        Content = BuildContent();
    }

    public BookGapViewModel ViewModel { get; }

    private Control BuildContent()
    {
        var hint = new TextBlock
        {
            Text = Strings.BookGap_Hint,
            Foreground = Brushes.Gray,
            TextWrapping = TextWrapping.Wrap,
        };

        var fields = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,Auto,Auto"),
            ColumnSpacing = 8,
            RowSpacing = 6,
        };
        AddRow(fields, 0, Strings.BookGap_Start, BoundTextBox(ViewModel.Times, nameof(SessionEditRow.StartText)));
        AddRow(fields, 1, Strings.BookGap_End, BoundTextBox(ViewModel.Times, nameof(SessionEditRow.EndText)));
        AddRow(fields, 2, Strings.BookGap_Duration, BoundText(ViewModel.Times, nameof(SessionEditRow.DurationText)));
        AddRow(fields, 3, Strings.BookGap_Task, BoundTextBox(ViewModel, nameof(BookGapViewModel.TaskName)));
        fields.Children.Add(BuildSuggestionList());
        AddRow(fields, 5, Strings.BookGap_BookingElement, BoundTextBox(ViewModel, nameof(BookGapViewModel.BookingElement)));

        var book = new Button { Content = Strings.BookGap_Book, IsDefault = true };
        book.Bind(IsEnabledProperty, new Binding { Source = ViewModel, Path = nameof(BookGapViewModel.CanBook) });
        book.Click += (_, _) => Close(true);

        var cancel = new Button { Content = Strings.BookGap_Cancel, IsCancel = true };
        cancel.Click += (_, _) => Close(false);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Children = { cancel, book },
        };

        return new StackPanel
        {
            Margin = new Thickness(16),
            Spacing = 12,
            Children = { hint, fields, actions },
        };
    }

    /// <summary>Suggestion list below the task field; a pick fills the task name.</summary>
    private ListBox BuildSuggestionList()
    {
        _suggestionList.ItemsSource = ViewModel.Suggestions.Suggestions;
        _suggestionList.ItemTemplate = new FuncDataTemplate<SuggestionItem>(
            (item, _) => new TextBlock { Text = item?.DisplayText });
        _suggestionList.Bind(IsVisibleProperty, new Binding
        {
            Source = ViewModel.Suggestions,
            Path = nameof(SuggestionListViewModel.ShowSuggestions),
        });
        _suggestionList.DoubleTapped += (_, _) => AcceptSelectedSuggestion();
        _suggestionList.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                AcceptSelectedSuggestion();
                e.Handled = true;
            }
        };

        Grid.SetRow(_suggestionList, 4);
        Grid.SetColumn(_suggestionList, 1);
        return _suggestionList;
    }

    private void AcceptSelectedSuggestion()
    {
        if (_suggestionList.SelectedItem is SuggestionItem suggestion)
        {
            ViewModel.AcceptSuggestion(suggestion);
        }
    }

    private static void AddRow(Grid grid, int row, string label, Control field)
    {
        var caption = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetRow(caption, row);
        Grid.SetRow(field, row);
        Grid.SetColumn(field, 1);
        grid.Children.Add(caption);
        grid.Children.Add(field);
    }

    private static TextBox BoundTextBox(object source, string path)
    {
        var box = new TextBox();
        box.Bind(TextBox.TextProperty, new Binding { Source = source, Path = path, Mode = BindingMode.TwoWay });
        return box;
    }

    private static TextBlock BoundText(object source, string path)
    {
        var text = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
        text.Bind(TextBlock.TextProperty, new Binding { Source = source, Path = path });
        return text;
    }
}
