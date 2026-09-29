using System.Text;

namespace Timetracker.App.Services;

/// <summary>
/// Crash-safe file replacement: the new content goes to a uniquely named temp file
/// in the target's directory, is flushed to disk and then renamed over the target.
/// A rename within one directory is atomic (MoveFileEx on NTFS, rename(2) on
/// Linux), so the target always holds either the old or the new content, never a
/// partial write. A rename blocked by a transient lock (antivirus, sync client) is
/// retried; if it keeps failing the exception propagates and the target is left
/// untouched. The target is never written directly.
/// </summary>
internal static class AtomicFile
{
    /// <summary>Rename attempts before the failure is reported.</summary>
    private const int MoveAttempts = 5;

    /// <summary>First retry delay; doubled per attempt (100, 200, 400, 800 ms).</summary>
    private static readonly TimeSpan BaseRetryDelay = TimeSpan.FromMilliseconds(50);

    /// <summary>Same encoding as File.WriteAllText: UTF-8 without a byte-order mark.</summary>
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>Replaces <paramref name="path"/> with <paramref name="contents"/> atomically.</summary>
    public static async Task WriteAllTextAsync(string path, string contents)
    {
        var fullPath = Path.GetFullPath(path);
        var tempPath = TempPathFor(fullPath);
        try
        {
            await using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, bufferSize: 4096, FileOptions.Asynchronous))
            {
                await stream.WriteAsync(Utf8NoBom.GetBytes(contents));
                // Data must be on disk before the rename, or a power loss can leave
                // the renamed file empty.
                stream.Flush(flushToDisk: true);
            }

            for (var attempt = 1; !TryMove(tempPath, fullPath, attempt); attempt++)
            {
                await Task.Delay(RetryDelay(attempt));
            }
        }
        catch
        {
            TryDelete(tempPath);
            throw;
        }
    }

    /// <summary>
    /// Synchronous variant for callers that run before the UI is up (the startup
    /// migration); blocking on the async variant there could deadlock the UI thread.
    /// </summary>
    public static void WriteAllText(string path, string contents)
    {
        var fullPath = Path.GetFullPath(path);
        var tempPath = TempPathFor(fullPath);
        try
        {
            using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(Utf8NoBom.GetBytes(contents));
                stream.Flush(flushToDisk: true);
            }

            for (var attempt = 1; !TryMove(tempPath, fullPath, attempt); attempt++)
            {
                Thread.Sleep(RetryDelay(attempt));
            }
        }
        catch
        {
            TryDelete(tempPath);
            throw;
        }
    }

    /// <summary>
    /// A unique name next to the target: the rename is only atomic within one
    /// volume, and a fixed name could collide with a leftover from a crash.
    /// </summary>
    private static string TempPathFor(string fullPath) => Path.Combine(
        Path.GetDirectoryName(fullPath)!,
        $"{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");

    /// <summary>
    /// Renames the temp file over the target. Returns false on a failure worth
    /// retrying; the last attempt's failure is thrown to the caller.
    /// </summary>
    private static bool TryMove(string tempPath, string fullPath, int attempt)
    {
        try
        {
            File.Move(tempPath, fullPath, overwrite: true);
            return true;
        }
        catch (Exception ex) when (attempt < MoveAttempts
            && ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static TimeSpan RetryDelay(int attempt) => BaseRetryDelay * (1 << attempt);

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception)
        {
            // Best effort: a stray temp file is harmless, the original error matters.
        }
    }
}
