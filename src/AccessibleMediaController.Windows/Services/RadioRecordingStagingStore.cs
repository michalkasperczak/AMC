using System.IO;

namespace AccessibleMediaController.Windows.Services;

/// <summary>
/// Keeps an encoder-owned file away from cloud placeholder providers.  Only a
/// complete, closed recording is copied to the selected destination and then
/// published under its final name.
/// </summary>
internal static class RadioRecordingStagingStore
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(150);
    private const int PublishAttempts = 8;

    public static string CreatePath(string finalPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(finalPath);
        var directory = CreateStagingDirectory();
        CleanupOldFiles(directory);
        var extension = Path.GetExtension(finalPath);
        return Path.Combine(directory, $"{Guid.NewGuid():N}{extension}.amc-partial");
    }

    private static string CreateStagingDirectory()
    {
        var candidates = new[]
        {
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AccessibleMediaController",
                "recording-staging"),
            Path.Combine(Path.GetTempPath(), "AccessibleMediaController", "recording-staging")
        }.Where(path => !string.IsNullOrWhiteSpace(path))
         .Distinct(StringComparer.OrdinalIgnoreCase);
        Exception? lastFailure = null;
        foreach (var candidate in candidates)
        {
            string? probePath = null;
            try
            {
                Directory.CreateDirectory(candidate);
                // Directory.CreateDirectory also succeeds when the directory
                // already exists but the current process cannot create files
                // in it. Verify the permission now so downloads and recordings
                // can fall back to the system temporary directory instead of
                // failing only after the network stream has started.
                probePath = Path.Combine(candidate, $".{Guid.NewGuid():N}.amc-write-test");
                using (new FileStream(
                           probePath,
                           FileMode.CreateNew,
                           FileAccess.Write,
                           FileShare.None,
                           bufferSize: 1,
                           FileOptions.DeleteOnClose))
                {
                }
                return candidate;
            }
            catch (Exception exception) when (exception is IOException
                or UnauthorizedAccessException)
            {
                lastFailure = exception;
            }
            finally
            {
                if (probePath is not null) TryDelete(probePath);
            }
        }
        throw new IOException(
            "Nie można utworzyć lokalnego, bezpiecznego folderu roboczego nagrań.",
            lastFailure);
    }

    public static void Publish(string stagingPath, string finalPath, bool overwrite = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stagingPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(finalPath);
        if (!File.Exists(stagingPath) || new FileInfo(stagingPath).Length == 0)
            throw new InvalidDataException("Koder nie utworzył danych nagrania.");

        var destinationDirectory = Path.GetDirectoryName(finalPath)
            ?? throw new InvalidOperationException("Nie rozpoznano folderu docelowego nagrania.");
        Directory.CreateDirectory(destinationDirectory);
        // A unique name prevents a stale file from an earlier cloud-provider
        // failure from being mistaken for the newly encoded recording merely
        // because both happen to have the same byte length.
        var publishingPath = finalPath + $".{Guid.NewGuid():N}.amc-publishing";
        Exception? lastFailure = null;
        for (var attempt = 1; attempt <= PublishAttempts; attempt++)
        {
            try
            {
                if (!overwrite && File.Exists(finalPath))
                    throw new IOException("Plik o tej nazwie już istnieje.");
                if (!File.Exists(publishingPath))
                    File.Copy(stagingPath, publishingPath, overwrite: false);
                File.Move(publishingPath, finalPath, overwrite);
                File.Delete(stagingPath);
                return;
            }
            catch (Exception exception) when (exception is IOException
                or UnauthorizedAccessException)
            {
                lastFailure = exception;
                if (attempt < PublishAttempts) Thread.Sleep(RetryDelay);
            }
        }

        TryDelete(publishingPath);
        throw new IOException(
            $"Nie można zapisać gotowego pliku w wybranym folderze. "
            + $"Bezpieczna kopia pozostała w: {stagingPath}",
            lastFailure);
    }

    public static void TryDelete(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            DiagnosticLog.Warning(
                "radio-recording",
                $"Nie można usunąć lokalnego pliku roboczego; błąd {exception.GetType().Name}.");
        }
    }

    private static void CleanupOldFiles(string directory)
    {
        try
        {
            var threshold = DateTime.UtcNow.AddDays(-1);
            foreach (var path in Directory.EnumerateFiles(directory, "*.amc-partial"))
            {
                try
                {
                    // A non-empty file can be the only recoverable copy after
                    // an encoder crash or a failed cloud publication. Never
                    // discard it automatically.
                    var file = new FileInfo(path);
                    if (file.Length == 0 && file.LastWriteTimeUtc < threshold) file.Delete();
                }
                catch (Exception exception) when (exception is IOException
                    or UnauthorizedAccessException)
                {
                    // A retained recovery file must never prevent a new recording.
                }
            }
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or DirectoryNotFoundException)
        {
        }
    }
}
