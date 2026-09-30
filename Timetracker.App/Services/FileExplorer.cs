using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Timetracker.App.Interfaces;

namespace Timetracker.App.Services;

/// <summary>
/// Opens Windows Explorer (<c>explorer /select,</c> for a file), the macOS Finder
/// (<c>open -R</c>) or, elsewhere, the desktop's file manager via <c>xdg-open</c> on
/// the folder.
/// </summary>
public sealed class FileExplorer(ILogger<FileExplorer> logger) : IFileExplorer
{
    public void Show(string path)
    {
        var command = CommandFor(
            path,
            OperatingSystem.IsWindows() ? Platform.Windows
                : OperatingSystem.IsMacOS() ? Platform.MacOS
                : Platform.Linux,
            File.Exists,
            Directory.Exists);
        if (command is null)
        {
            logger.LogWarning("Nothing to show in the file explorer for {Path}", path);
            return;
        }

        try
        {
            using var process = Process.Start(
                new ProcessStartInfo(command.FileName, command.Arguments) { UseShellExecute = false });
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            logger.LogError(ex, "Could not open the file explorer for {Path}", path);
        }
    }

    /// <summary>The platforms with a distinct way of showing a path.</summary>
    public enum Platform
    {
        Windows,
        MacOS,
        Linux,
    }

    /// <summary>A program and its command line; paths are quoted.</summary>
    public sealed record Command(string FileName, string Arguments);

    /// <summary>
    /// The command that shows <paramref name="path"/> on the given platform; null
    /// when the path is empty or none of its folders exist. The file system checks
    /// are passed in, so every platform can be tested on any machine.
    /// </summary>
    public static Command? CommandFor(
        string path, Platform platform, Func<string, bool> fileExists, Func<string, bool> directoryExists)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var fullPath = Path.GetFullPath(path.Trim());
        if (fileExists(fullPath))
        {
            Command? fileCommand = platform switch
            {
                // explorer parses its own command line and expects the quotes after the comma.
                Platform.Windows => new Command("explorer.exe", $"/select,\"{fullPath}\""),
                Platform.MacOS => new Command("open", $"-R \"{fullPath}\""),
                _ => FolderCommand(Path.GetDirectoryName(fullPath), platform),
            };
            return fileCommand;
        }

        var folder = fullPath;
        while (folder is not null && !directoryExists(folder))
        {
            folder = Path.GetDirectoryName(folder);
        }

        var result = FolderCommand(folder, platform);
        return result;
    }

    private static Command? FolderCommand(string? folder, Platform platform) =>
        folder is null
            ? null
            : platform switch
            {
                Platform.Windows => new Command("explorer.exe", $"\"{folder}\""),
                Platform.MacOS => new Command("open", $"\"{folder}\""),
                _ => new Command("xdg-open", $"\"{folder}\""),
            };
}
