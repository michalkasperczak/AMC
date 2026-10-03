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

        // Zmiana WYLACZNIE wielkosci liter jest dozwolona. Na NTFS (Windows)
        // „No cześć.mp3” i „No Cześć.mp3” to ten SAM wpis katalogu, wiec goly
        // File.Exists(targetPath) zwraca true i wygladalby jak kolizja z obcym
        // plikiem. Dlatego o zajetosc nazwy pytamy katalog i porownujemy nazwy
        // DOKLADNIE (Ordinal): tylko wpis o identycznej pisowni, inny niz nasz
        // plik, jest prawdziwa kolizja. Ta sama reguła dziala na systemach
        // rozrozniajacych wielkosc liter (ext4), gdzie taki wpis moze istniec.
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
    /// <paramref name="exactFileName"/> (porownanie Ordinal), ktory NIE jest
    /// plikiem <paramref name="currentFullPath"/>. Enumeracja z wzorcem nazwy
    /// jest na Windows niewrazliwa na wielkosc liter, dlatego pisownie
    /// sprawdzamy sami.
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

    /// <summary>
    /// Wykonuje zmiane nazwy na sciezke wyliczona przez
    /// <see cref="TryBuildTargetPath"/>. Nie nadpisuje innego pliku: zadne
    /// wywolanie nie uzywa trybu overwrite.
    ///
    /// Zmiana samej wielkosci liter na NTFS jest zwyklym przemianowaniem i
    /// <see cref="File.Move(string,string)"/> ja wykonuje. Czesc systemow plikow
    /// (udzialy sieciowe, FAT, warstwy chmurowe) odrzuca taka pare nazw jako
    /// „plik juz istnieje”; wtedy robimy to samo w dwoch krokach przez nazwe
    /// tymczasowa w TYM SAMYM folderze. Gdy drugi krok padnie, plik wraca pod
    /// pierwotna nazwe, zeby nie zostawic go pod nazwa techniczna.
    /// </summary>
    public static void MoveFile(string currentPath, string targetPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currentPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);

        var isCaseOnlyChange =
            !string.Equals(currentPath, targetPath, StringComparison.Ordinal)
            && string.Equals(currentPath, targetPath, StringComparison.OrdinalIgnoreCase);

        try
        {
            File.Move(currentPath, targetPath);
            return;
        }
        catch (IOException) when (isCaseOnlyChange)
        {
            // Jedyny przypadek, w ktorym ponawiamy: ten sam plik, inna pisownia.
        }

        var directory = Path.GetDirectoryName(Path.GetFullPath(currentPath))
            ?? throw new IOException("Nie można ustalić folderu pliku.");
        var staging = Path.Combine(directory, ".amc-rename-" + Guid.NewGuid().ToString("N"));
        File.Move(currentPath, staging);
        try
        {
            File.Move(staging, targetPath);
        }
        catch
        {
            File.Move(staging, currentPath);
            throw;
        }
    }
}
