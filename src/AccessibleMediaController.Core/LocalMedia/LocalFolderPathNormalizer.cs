namespace AccessibleMediaController.Core.LocalMedia;

/// <summary>
/// Jawny normalizator ścieżek folderów o zasięgu JEDNEGO przebiegu wywołującego.
/// Nie jest trwałym cache'em globalnym: instancję tworzy wywołujący, trzyma ją w
/// zmiennej lokalnej i porzuca po zakończeniu przebiegu, więc zmiana stanu systemu
/// plików między przebiegami nigdy nie jest maskowana starym wynikiem.
///
/// WARUNEK POPRAWNOŚCI: <see cref="Normalize"/> zwraca dokładnie to samo, co
/// <c>Path.TrimEndingDirectorySeparator(Path.GetFullPath(path))</c>, a klucz pamięci
/// jest porównywany <see cref="StringComparer.Ordinal"/>, czyli dokładnie tym, co
/// rozróżnia wejścia tej funkcji. Wyjątki propagują się bez zmian i NIE są pamiętane.
/// </summary>
public sealed class LocalFolderPathNormalizer
{
    private readonly Dictionary<string, string> _memo = new(StringComparer.Ordinal);

    /// <summary>Liczba rzeczywistych normalizacji (diagnostyka i testy regresji kosztu).</summary>
    public int ComputeCount { get; private set; }

    /// <summary>Liczba odpowiedzi z pamięci przebiegu (diagnostyka i testy regresji kosztu).</summary>
    public int HitCount { get; private set; }

    public string Normalize(string path)
    {
        if (_memo.TryGetValue(path, out var cached))
        {
            HitCount++;
            return cached;
        }

        // Wyjątek z GetFullPath leci do wywołującego i nie zostaje zapamiętany:
        // kolejne wywołanie zachowa się identycznie jak bez normalizatora.
        var value = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        ComputeCount++;
        _memo[path] = value;
        return value;
    }
}
