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
        if (string.Equals(targetPath, fullCurrentPath, StringComparison.OrdinalIgnoreCase))
        {
            error = "Zmiana wyłącznie wielkości liter nie jest jeszcze obsługiwana.";
            return false;
        }
        if (File.Exists(targetPath) || Directory.Exists(targetPath))
        {
            error = "W tym folderze istnieje już plik albo folder o takiej nazwie.";
            return false;
        }
        return true;
    }
}
