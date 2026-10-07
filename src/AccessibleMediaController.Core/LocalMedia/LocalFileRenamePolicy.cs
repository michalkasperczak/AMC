namespace AccessibleMediaController.Core.LocalMedia;

public static class LocalFileRenamePolicy
{
    private static readonly HashSet<string> ReservedWindowsNames = new(
        ["CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
         "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"],
        StringComparer.OrdinalIgnoreCase);

    public static bool TryBuildTargetPath(
        string currentPath,
        string requestedName,
        out string targetPath,
        out string error)
    {
        targetPath = string.Empty;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(currentPath))
        {
            error = "Brak ścieżki do pliku.";
            return false;
        }

        string fullCurrentPath;
        try
        {
            fullCurrentPath = Path.GetFullPath(currentPath);
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            error = "Ścieżka do pliku jest nieprawidłowa.";
            return false;
        }

        var name = requestedName.Trim();
        var extension = Path.GetExtension(fullCurrentPath);
        if (extension.Length > 0 && name.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
        {
            name = name[..^extension.Length].TrimEnd();
        }

        if (name.Length == 0)
        {
            error = "Nazwa pliku nie może być pusta.";
            return false;
        }
        if (name is "." or ".."
            || name.EndsWith(' ') || name.EndsWith('.')
            || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || name.Contains(Path.DirectorySeparatorChar)
            || name.Contains(Path.AltDirectorySeparatorChar))
        {
            error = "Nazwa zawiera znak niedozwolony w nazwie pliku.";
            return false;
        }

        var firstPart = name.Split('.')[0];
        if (ReservedWindowsNames.Contains(firstPart))
        {
            error = "Ta nazwa jest zarezerwowana przez system Windows.";
            return false;
        }

        var directory = Path.GetDirectoryName(fullCurrentPath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            error = "Nie można ustalić folderu pliku.";
            return false;
        }

        targetPath = Path.Combine(directory, name + extension);
        if (string.Equals(targetPath, fullCurrentPath, StringComparison.Ordinal))
        {
            error = "Nazwa pliku nie została zmieniona.";
            return false;
        }

        // Zmiana WYLACZNIE wielkosci liter jest dozwolona. Zmierzone na NTFS:
        // File.Exists(targetPath) zwraca wtedy true (ten sam wpis katalogu), wiec
        // wygladalaby jak kolizja. O zajetosc nazwy pytamy katalog i porownujemy
        // pisownie DOKLADNIE (Ordinal) — tylko wpis innego pliku jest kolizja.
        if (string.Equals(targetPath, fullCurrentPath, StringComparison.OrdinalIgnoreCase))
        {
            if (ExistsWithExactName(directory, Path.GetFileName(targetPath), fullCurrentPath))
            {
                error = "W tym folderze istnieje już plik albo folder o takiej nazwie.";
                return false;
            }
            return true;
        }

        if (File.Exists(targetPath) || Directory.Exists(targetPath))
        {
            error = "W tym folderze istnieje już plik albo folder o takiej nazwie.";
            return false;
        }
        return true;
    }

    /// <summary>
    /// Czy w <paramref name="directory"/> istnieje wpis o nazwie dokladnie
    /// <paramref name="exactFileName"/> (Ordinal), ktory NIE jest plikiem
    /// <paramref name="currentFullPath"/>. Zmierzone: enumeracja z wzorcem nazwy
    /// jest na NTFS niewrazliwa na wielkosc liter, wiec pisownie sprawdzamy sami.
    /// </summary>
    private static bool ExistsWithExactName(
        string directory,
        string exactFileName,
        string currentFullPath)
    {
        try
        {
            foreach (var entry in Directory.EnumerateFileSystemEntries(directory, exactFileName))
            {
                if (!string.Equals(Path.GetFileName(entry), exactFileName, StringComparison.Ordinal))
                {
                    continue;
                }
                if (string.Equals(Path.GetFullPath(entry), currentFullPath, StringComparison.Ordinal))
                {
                    continue;
                }
                return true;
            }
            return false;
        }
        catch (Exception exception) when (
            exception is IOException
                or UnauthorizedAccessException
                or ArgumentException
                or DirectoryNotFoundException)
        {
            // Nie potwierdzono kolizji. Samo przemianowanie i tak nie nadpisze
            // obcego pliku: File.Move bez overwrite odmawia, a komunikat bledu
            // trafia do uzytkownika z warstwy wykonania.
            return false;
        }
    }
}
