namespace Timetracker.Plugins.Calendar.ViewModels;

/// <summary>
/// The visible time window of the Workweek/Week grid, measured from midnight.
/// <see cref="End"/> can be 24:00 when the window reaches the end of the day.
/// </summary>
public sealed record CalendarAxis(TimeSpan Start, TimeSpan End)
{
    /// <summary>Default window shown when the week has neither sessions nor activity.</summary>
    public static readonly CalendarAxis Default =
        new(TimeSpan.FromHours(8), TimeSpan.FromHours(18));

    /// <summary>Length of the visible window.</summary>
    public TimeSpan Duration => End - Start;

    /// <summary>Body height in pixels at the layout's scale.</summary>
    public double Height => Duration.TotalHours * CalendarLayout.PixelsPerHour;

    /// <summary>True when <paramref name="time"/> lies inside the window.</summary>
    public bool Contains(TimeSpan time) => time >= Start && time <= End;
}
