using System.IO;
using System.Security.Cryptography;

namespace AccessibleMediaController.Windows.Services;

/// <summary>
/// Retention of the backup that ONE audio edit creates for ITS OWN operation.
///
/// The user asked for the default to be: after the saved file has really been
/// checked, that single backup goes away. Everything uncertain keeps it. This
/// type therefore does three things in one place, for cutting and appending
/// alike:
///
/// 1. It measures the verified result BEFORE the file is committed and the
///    final file AFTER it, so the decision is about the file that now exists on
///    disk - not about a temporary file that was only correct a moment earlier.
/// 2. On any doubt after the commit (different content, unreadable result) it
///    keeps the backup and says plainly that the destination HAS been replaced.
///    Promising an untouched original after a commit would be a lie.
/// 3. It never searches, globs or schedules anything. Only the one path it was
///    given for this operation can be removed, so older backups from earlier
///    edits stay exactly where the user left them.
/// </summary>
internal static class AudioEditBackupRetention
{
    /// <summary>
    /// Commits <paramref name="verifiedResultPath"/> over
    /// <paramref name="destinationPath"/> through <paramref name="replace"/>,
    /// confirms the committed bytes and then applies the retention policy to
    /// <paramref name="backupPath"/> only.
    /// </summary>
    /// <returns>
    /// An empty string when this operation's backup has really been removed
    /// after a confirmed success, or the path of the backup that was kept.
    /// </returns>
    internal static async Task<string> CommitAsync(
        string verifiedResultPath,
        string destinationPath,
        string backupPath,
        bool keepBackup,
        Action<string, string, string> replace,
        string logArea,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(verifiedResultPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(backupPath);
        ArgumentNullException.ThrowIfNull(replace);

        // Last cancellation point: everything below either happens or has to be
        // reported as done. A cancelled commit cannot be undone silently.
        cancellationToken.ThrowIfCancellationRequested();
        var expected = await ReadDigestAsync(verifiedResultPath, cancellationToken).ConfigureAwait(false);

        replace(verifiedResultPath, destinationPath, backupPath);

        string actual;
        try
        {
            actual = await ReadDigestAsync(destinationPath, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or FileNotFoundException
            or DirectoryNotFoundException)
        {
            DiagnosticLog.Error(
                logArea,
                $"Plik {Path.GetFileName(destinationPath)} został podmieniony, ale nie udało się go "
                + $"odczytać do sprawdzenia. Kopia {Path.GetFileName(backupPath)} zostaje zachowana.",
                exception);
            throw new IOException(
                $"Plik {Path.GetFileName(destinationPath)} został już zapisany, ale nie udało się go teraz "
                + "odczytać, żeby to sprawdzić. Poprzednia wersja jest w kopii zapasowej "
                + $"{Path.GetFileName(backupPath)} - sprawdź oba pliki przed dalszą pracą.",
                exception);
        }

        if (!string.Equals(expected, actual, StringComparison.Ordinal))
        {
            DiagnosticLog.Error(
                logArea,
                $"Zapisany plik {Path.GetFileName(destinationPath)} różni się od sprawdzonego wyniku. "
                + $"Kopia {Path.GetFileName(backupPath)} zostaje zachowana.");
            throw new InvalidDataException(
                $"Plik {Path.GetFileName(destinationPath)} został zapisany, ale jego zawartość różni się od "
                + "sprawdzonego wyniku. Poprzednia wersja jest w kopii zapasowej "
                + $"{Path.GetFileName(backupPath)} - sprawdź oba pliki przed dalszą pracą.");
        }

        if (keepBackup)
        {
            DiagnosticLog.Info(
                logArea,
                $"Zachowano kopię {Path.GetFileName(backupPath)} zgodnie z wybranym ustawieniem.");
            return backupPath;
        }

        return TryRemoveBackup(backupPath, destinationPath, logArea);
    }

    /// <summary>
    /// Removes exactly one backup file. A failure here is not a failure of the
    /// edit itself: the caller keeps the path, so the interface can still offer
    /// the copy it can see.
    /// </summary>
    /// <returns>An empty string when the file is gone, otherwise its path.</returns>
    internal static string TryRemoveBackup(string backupPath, string destinationPath, string logArea)
    {
        try
        {
            File.Delete(backupPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            DiagnosticLog.Warning(
                logArea,
                $"Nie udało się usunąć kopii {Path.GetFileName(backupPath)} po sprawdzonej zmianie pliku "
                + $"{Path.GetFileName(destinationPath)}: {exception.Message} Kopia zostaje na dysku.");
            return backupPath;
        }

        if (File.Exists(backupPath))
        {
            DiagnosticLog.Warning(
                logArea,
                $"Kopia {Path.GetFileName(backupPath)} nadal jest na dysku po próbie usunięcia.");
            return backupPath;
        }

        DiagnosticLog.Info(
            logArea,
            $"Usunięto kopię {Path.GetFileName(backupPath)} po sprawdzeniu zapisanego pliku "
            + $"{Path.GetFileName(destinationPath)}.");
        return string.Empty;
    }

    private static async Task<string> ReadDigestAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            128 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash);
    }
}
