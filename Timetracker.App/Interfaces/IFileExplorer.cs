namespace Timetracker.App.Interfaces;

/// <summary>Opens the platform's file manager, so the options view stays testable.</summary>
public interface IFileExplorer
{
    /// <summary>
    /// Shows the path in the file manager: a file is selected in its folder, a
    /// folder is opened. A path that does not exist yet opens its nearest existing
    /// parent folder.
    /// </summary>
    void Show(string path);
}
