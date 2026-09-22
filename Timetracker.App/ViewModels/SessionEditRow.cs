using System.Globalization;
using Timetracker.Models;

namespace Timetracker.ViewModels;

/// <summary>
/// One editable session inside the per-item editor. The start and end are staged
/// as text in the application's usual format ("yyyy-MM-dd HH:mm"); the duration is
/// always derived from them. Edits are only written back to the
/// <see cref="TrackerEntry"/> on commit, so cancelling leaves the stored data
/// untouched.
///
/// Text entry is used instead of the framework's date/time pickers because those
/// are fixed-width controls that ignore Width and cannot be sized to fit a cell.
/// </summary>
public sealed class SessionEditRow : ObservableObject
{
    private const string StampFormat = "yyyy-MM-dd HH:mm";

    private readonly TrackerEntry _entry;
    private DateTimeOffset _start;
    private DateTimeOffset _end;

    private string _startText;
    private string _endText;
    private bool _startValid = true;
    private bool _endValid = true;

    public SessionEditRow(TrackerEntry entry)
    {
        _entry = entry;
        _start = entry.Start;
        _end = entry.End;
        _startText = Format(_start);
        _endText = Format(_end);
    }

    /// <summary>The session this row edits; unchanged until <see cref="Commit"/>.</summary>
    public TrackerEntry Entry => _entry;

    /// <summary>Task name, shown for context (not editable here).</summary>
    public string Task => _entry.Task;

    /// <summary>Booking element, shown for context (not editable here).</summary>
    public string BookingElement => _entry.BookingElement;

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

    /// <summary>Writes the staged times to the underlying session.</summary>
    public void Commit() => _entry.Reschedule(_start, _end);

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
