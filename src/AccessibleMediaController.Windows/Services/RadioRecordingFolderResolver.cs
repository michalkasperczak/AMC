using System.IO;

namespace AccessibleMediaController.Windows.Services;

internal sealed record RadioRecordingFolderResolution(
    string Path,
    bool UsedFallback,
    string? RejectedPath,
    string? ErrorType);

internal static class RadioRecordingFolderResolver
{
    public static RadioRecordingFolderResolution Resolve(params string?[] candidates)
    {
        var usableCandidates = candidates
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => path!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (usableCandidates.Length == 0)
            throw new ArgumentException("Nie wskazano folderu nagrań.", nameof(candidates));

        string? firstRejected = null;
        string? firstErrorType = null;
        foreach (var rawCandidate in usableCandidates)
        {
            try
            {
                var candidate = Path.GetFullPath(rawCandidate);
                EnsureWritable(candidate);
                return new RadioRecordingFolderResolution(
                    candidate,
                    firstRejected is not null,
                    firstRejected,
                    firstErrorType);
            }
            catch (Exception exception) when (exception is IOException
                or UnauthorizedAccessException
                or ArgumentException
                or NotSupportedException)
            {
                firstRejected ??= rawCandidate;
                firstErrorType ??= exception.GetType().Name;
            }
        }

        throw new IOException("Żaden skonfigurowany folder nagrań nie jest dostępny do zapisu.");
    }

    private static void EnsureWritable(string folder)
    {
        Directory.CreateDirectory(folder);
        var probe = Path.Combine(folder, $".amc-write-test-{Guid.NewGuid():N}.tmp");
        try
        {
            using var stream = new FileStream(
                probe,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                1,
                FileOptions.WriteThrough);
            stream.WriteByte(0);
            stream.Flush(true);
        }
        finally
        {
            try
            {
                if (File.Exists(probe)) File.Delete(probe);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                DiagnosticLog.Warning(
                    "radio-recording",
                    $"Nie można usunąć pliku próby zapisu; błąd {exception.GetType().Name}.");
            }
        }
    }
}
