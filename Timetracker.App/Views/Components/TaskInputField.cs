using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Timetracker.ViewModels;

namespace Timetracker.Views.Components;

/// <summary>
/// The task-name input with its autocomplete list. Typing shows matching known
/// tasks below the field; picking one fills the field. View-only: the text,
/// suggestions and acceptance logic live in <see cref="TrackerViewModel"/>.
/// </summary>
public sealed class TaskInputField : UserControl
{
    private readonly TrackerViewModel _viewModel;
    private readonly TextBox _taskBox = new();
    private readonly ListBox _suggestionList = new();

    /// <summary>Suppresses suggestion handling while a suggestion is being applied.</summary>
    private bool _pickingSuggestion;

    /// <summary>The task-name text box; the host focuses it and sets its text.</summary>
    public TextBox TaskBox => _taskBox;

    public TaskInputField(TrackerViewModel viewModel)
    {
        _viewModel = viewModel;

        BuildUi();
        BindViewModel();
    }

    private void BuildUi()
    {
        var label = new TextBlock
        {
            Text = "Task name",
            Margin = new Avalonia.Thickness(0, 0, 0, 4),
        };

        _taskBox.Margin = new Avalonia.Thickness(0, 0, 0, 4);
        _taskBox.KeyDown += OnTaskBoxKeyDown;

        _suggestionList.IsVisible = false;
        _suggestionList.MaxHeight = 140;
        _suggestionList.ItemTemplate = new FuncDataTemplate<SuggestionItem>(
            (item, _) => new TextBlock { Text = item?.DisplayText ?? "" }, true);
        _suggestionList.DoubleTapped += OnSuggestionChosen;
        _suggestionList.KeyDown += OnSuggestionKeyDown;

        Content = new StackPanel
        {
            Children =
            {
                label,
                _taskBox,
                _suggestionList,
            },
        };
    }

    private void BindViewModel()
    {
        _taskBox.Bind(TextBox.TextProperty, new Binding
        {
            Source = _viewModel,
            Path = nameof(TrackerViewModel.TaskName),
            Mode = BindingMode.TwoWay,
        });
        _taskBox.Bind(TextBox.IsReadOnlyProperty, new Binding
        {
            Source = _viewModel,
            Path = nameof(TrackerViewModel.IsRunning),
        });
        _taskBox.TextChanged += OnTaskBoxTextChanged;

        _suggestionList.ItemsSource = _viewModel.Suggestions;
        _viewModel.Suggestions.CollectionChanged += (_, _) => UpdateSuggestions();

        UpdateSuggestions();
    }

    private void OnTaskBoxTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_pickingSuggestion)
        {
            return;
        }

        // The binding pushes the text into the view model; suggestions update with it.
        UpdateSuggestions();
    }

    private void OnTaskBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (!_suggestionList.IsVisible || _viewModel.Suggestions.Count == 0)
        {
            return;
        }

        if (e.Key == Key.Down)
        {
            // Hand focus to the list so Arrow/Enter can pick a suggestion.
            _suggestionList.SelectedIndex = 0;
            _suggestionList.Focus();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            HideSuggestions();
            e.Handled = true;
        }
    }

    private void OnSuggestionChosen(object? sender, TappedEventArgs e) => ApplySelectedSuggestion();

    private void OnSuggestionKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ApplySelectedSuggestion();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            HideSuggestions();
            _taskBox.Focus();
            e.Handled = true;
        }
    }

    private void ApplySelectedSuggestion()
    {
        if (_suggestionList.SelectedItem is not SuggestionItem suggestion)
        {
            return;
        }

        _pickingSuggestion = true;
        try
        {
            _viewModel.AcceptSuggestion(suggestion);
            HideSuggestions();
        }
        finally
        {
            _pickingSuggestion = false;
        }

        UpdateSuggestions();
    }

    private void UpdateSuggestions()
    {
        var show = !_pickingSuggestion && _viewModel.ShowSuggestions;
        _suggestionList.IsVisible = show;
    }

    private void HideSuggestions() => _suggestionList.IsVisible = false;
}
