namespace AccessibleMediaController.Core.Presentation;

/// <summary>
/// Obserwacje plików nagrań używane wyłącznie w wątku UI. Pamięć jest
/// unieważniana po zdarzeniu pliku, powrocie do okna lub otwarciu historii;
/// zwykłe filtrowanie i poruszanie po liście nie pytają ponownie dysku.
/// Brak dostępu do folderu nie jest potwierdzeniem usunięcia pliku.
/// </summary>
public sealed class RecordingPathProbe(Func<string, bool> fileExists, Func<string, bool>? directoryExists = null)
{
    private readonly Func<string, bool> _fileExists =
        fileExists ?? throw new ArgumentNullException(nameof(fileExists));
    private enum Availability { Present, Missing, Unavailable }
    private readonly Dictionary<string, Availability> _known = new(StringComparer.OrdinalIgnoreCase);
    public int HitCount { get; private set; }
    public int KnownCount => _known.Count;

    public bool Exists(string? path)
    {
        path = Normalize(path);
        if (path.Length == 0) return false;
        if (_known.TryGetValue(path, out var known))
        {
            HitCount++;
            return known == Availability.Present;
        }
        var result = _fileExists(path);
        _known[path] = result ? Availability.Present
            : directoryExists is not null && !directoryExists(Path.GetDirectoryName(path) ?? string.Empty)
                ? Availability.Unavailable : Availability.Missing;
        return result;
    }

    public bool IsMissing(string? path)
    {
        path = Normalize(path);
        return path.Length != 0 && !Exists(path) && _known[path] == Availability.Missing;
    }

    public bool IsUnavailable(string? path)
    {
        path = Normalize(path);
        return path.Length != 0 && !Exists(path) && _known[path] == Availability.Unavailable;
    }

    public void Invalidate(string? path) => _known.Remove(Normalize(path));

    public void MarkMissing(string? path)
    {
        path = Normalize(path);
        if (path.Length != 0) _known[path] = Availability.Missing;
    }

    public void MarkPresent(string? path)
    {
        path = Normalize(path);
        if (path.Length != 0) _known[path] = Availability.Present;
    }

    public void Clear() => _known.Clear();

    private static string Normalize(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;
        try { return Path.GetFullPath(path.Trim()); }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException)
        { return path.Trim(); }
    }
}
