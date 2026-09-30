using System.Globalization;
using Timetracker.Plugins.Contracts.ViewModels.Mvvm;

namespace Timetracker.Plugins.Contracts.ViewModels;

/// <summary>
/// One editable session time, shared by the tracker's per-item editor and the week
/// view's booking dialog. The start and end are staged as text in the application's
/// usual format ("yyyy-MM-dd HH:mm"); the duration is always derived from them.
/// Staging never touches the stored data — the caller writes the values back.
///
/// Text entry is used instead of the framework's date/time pickers because those
/// are fixed-width controls that ignore Width and cannot be sized to fit a cell.
/// </summary>
public sealed class SessionEditRow : ObservableObject
{
    private const string StampFormat = "yyyy-MM-dd HH:mm";

    private DateTimeOffset _start;
    private DateTimeOffset _end;

    private string _startText;
    private string _endText;
    private bool _startValid = true;
    private bool _endValid = true;

    public SessionEditRow(string task, DateTimeOffset start, DateTimeOffset end)
    {
        Task = task;
        _start = start;
        _end = end;
        _startText = Format(_start);
        _endText = Format(_end);
    }

    /// <summary>Task name, shown for context (not editable here).</summary>
    public string Task { get; }

    public DateTimeOffset Start => _start;

    public DateTimeOffset End => _end;

    /// <summary>Editable start, "yyyy-MM-dd HH:mm".</summary>
    public string StartText
    {
        get => _startText;
        set
        {
            if (!SetProperty(ref _startText, value))
            {
                return;
            }

            _startValid = TryParse(value, _start.Offset, out var start);
            if (_startValid)
            {
                // Update the value without rewriting the text the user is typing.
                _start = start;
                OnPropertyChanged(nameof(Start));
            }
            RefreshDependent();
        }
    }

    /// <summary>Editable end, "yyyy-MM-dd HH:mm".</summary>
    public string EndText
    {
        get => _endText;
        set
        {
            if (!SetProperty(ref _endText, value))
            {
                return;
            }

            _endValid = TryParse(value, _end.Offset, out var end);
            if (_endValid)
            {
                _end = end;
                OnPropertyChanged(nameof(End));
            }
            RefreshDependent();
        }
    }

    /// <summary>Duration derived from the staged times, e.g. "01:30:00".</summary>
    public string DurationText => Elapsed.ToString(@"hh\:mm\:ss");

    /// <summary>Duration in seconds; 0 when the end lies before the start.</summary>
    public double DurationSeconds => Math.Round(Elapsed.TotalSeconds, 1);

    /// <summary>False when a timestamp does not parse or the end lies before the start.</summary>
    public bool IsValid => _startValid && _endValid && _end >= _start;

    /// <summary>Sets the staged start (and its editable text).</summary>
    public void SetStart(DateTimeOffset start)
    {
        if (SetProperty(ref _start, start, nameof(Start)))
        {
            SetText(ref _startText, Format(start), nameof(StartText));
            _startValid = true;
            RefreshDependent();
        }
    }

    /// <summary>Sets the staged end (and its editable text).</summary>
    public void SetEnd(DateTimeOffset end)
    {
        if (SetProperty(ref _end, end, nameof(End)))
        {
            SetText(ref _endText, Format(end), nameof(EndText));
            _endValid = true;
            RefreshDependent();
        }
    }

    private TimeSpan Elapsed => _end > _start ? _end - _start : TimeSpan.Zero;

    private void RefreshDependent()
    {
        OnPropertyChanged(nameof(DurationText));
        OnPropertyChanged(nameof(DurationSeconds));
        OnPropertyChanged(nameof(IsValid));
    }

    private void SetText(ref string field, string value, string propertyName)
    {
        if (field == value)
        {
            return;
        }
        field = value;
        OnPropertyChanged(propertyName);
    }

    private static string Format(DateTimeOffset moment) =>
        moment.ToString(StampFormat, CultureInfo.InvariantCulture);

    /// <summary>
    /// Parses typed wall-clock text ("yyyy-MM-dd HH:mm") in the session's own
    /// <paramref name="offset"/>. Using the stored offset instead of the machine's
    /// current local offset keeps an edit from shifting the recorded instant when
    /// the app happens to run in another timezone.
    /// </summary>
    private static bool TryParse(string text, TimeSpan offset, out DateTimeOffset moment)
    {
        if (!DateTime.TryParseExact(
                text.Trim(), StampFormat, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var wallClock))
        {
            moment = default;
            return false;
        }

        moment = new DateTimeOffset(wallClock, offset);
        return true;
    }
}
