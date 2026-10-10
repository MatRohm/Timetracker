namespace Timetracker.Plugins.Contracts;

/// <summary>How an option is edited and shown.</summary>
public enum OptionKind
{
    /// <summary>Free text.</summary>
    Text,

    /// <summary>A file or folder path; the options view can show it in the file explorer.</summary>
    Path,

    /// <summary>Text that is masked on screen, such as a token.</summary>
    Secret,
}
