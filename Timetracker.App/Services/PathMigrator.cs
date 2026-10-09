using System.Text.Json;
using Microsoft.Extensions.Logging;
using Timetracker.Plugins.Contracts;

namespace Timetracker.App.Services;

/// <summary>
/// Moves the five config/data/log artifacts from their legacy (pre-unification)
/// locations into the unified <c>~/.timetracker</c> layout, once, at startup. The
/// options file is the fixed anchor and moves first; the other four move to the
/// effective folder (the configured <c>General.ConfigFolder</c>, or the default).
/// A failure on one item does not abort the others. Runs before the service
/// container is built so the options store reads the migrated file.
/// </summary>
public static class PathMigrator
{
    public static void MigrateIfNeeded(ILogger logger)
    {
        // The options file is the fixed anchor; move it before resolving the folder.
        MoveFile(TimetrackerPaths.LegacyOptionsFile, TimetrackerPaths.OptionsFile, logger);

        // Resolve the effective folder now that the options file is in place.
        var folder = TimetrackerPaths.ResolveFolder(ReadConfigFolder());

        // Ensure the folders exist so the first write never fails on a fresh install
        // (with no legacy files to move).
        Directory.CreateDirectory(TimetrackerPaths.RootFolder);
        Directory.CreateDirectory(folder);

        MoveFile(TimetrackerPaths.LegacyTrackingFile, Path.Combine(folder, "timetracker.json"), logger);
        MoveFile(TimetrackerPaths.LegacyActivityFile, Path.Combine(folder, "timetracker-activity.json"), logger);
        MoveFile(
            TimetrackerPaths.LegacyActivityStateFile,
            Path.Combine(folder, "timetracker-activity.state.json"),
            logger);
        MoveFolder(TimetrackerPaths.LegacyLogFolder, Path.Combine(folder, "logs"), logger);
    }

    /// <summary>
    /// Reads <see cref="TimetrackerPaths.ConfigFolderKey"/> straight from the options
    /// file. The migration runs before the options store is built, so it cannot use
    /// <see cref="IOptionQuery"/>; this small bootstrap read is the exception.
    /// </summary>
    private static string? ReadConfigFolder()
    {
        try
        {
            if (!File.Exists(TimetrackerPaths.OptionsFile))
            {
                return null;
            }

            using var document = JsonDocument.Parse(File.ReadAllText(TimetrackerPaths.OptionsFile));
            if (document.RootElement.TryGetProperty(TimetrackerPaths.ConfigFolderKey, out var element) &&
                element.ValueKind == JsonValueKind.String)
            {
                return element.GetString();
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            // A broken options file yields no configured folder; fall back to the default.
        }

        return null;
    }

    /// <summary>Moves one file from its legacy location to the effective one, if warranted.</summary>
    internal static void MoveFile(string oldPath, string newPath, ILogger logger)
    {
        if (oldPath == newPath)
        {
            return; // The effective path matches the legacy one (entered old-default value); nothing to move.
        }

        if (!File.Exists(oldPath))
        {
            return; // Nothing at the legacy location.
        }

        if (File.Exists(newPath))
        {
            logger.LogWarning("Both {OldPath} and {NewPath} exist; leaving both.", oldPath, newPath);
            return;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(newPath)!);
            File.Move(oldPath, newPath);
            logger.LogInformation("Moved {OldPath} to {NewPath}", oldPath, newPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogError(ex, "Could not move {OldPath} to {NewPath}", oldPath, newPath);
        }
    }

    /// <summary>Moves every file in the legacy log folder to the effective one, if warranted.</summary>
    internal static void MoveFolder(string oldFolder, string newFolder, ILogger logger)
    {
        if (oldFolder == newFolder || !Directory.Exists(oldFolder))
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(newFolder);
            foreach (var file in Directory.EnumerateFiles(oldFolder))
            {
                var target = Path.Combine(newFolder, Path.GetFileName(file));
                if (!File.Exists(target))
                {
                    File.Move(file, target);
                }
            }

            logger.LogInformation("Moved logs from {OldFolder} to {NewFolder}", oldFolder, newFolder);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogError(ex, "Could not move logs from {OldFolder} to {NewFolder}", oldFolder, newFolder);
        }
    }
}
