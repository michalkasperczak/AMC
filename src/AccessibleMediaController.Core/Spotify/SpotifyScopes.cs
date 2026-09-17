namespace AccessibleMediaController.Core.Spotify;

/// <summary>
/// Zakresy uprawnień Spotify, o które prosi AMC.
///
/// Rozdzielone celowo na trzy grupy, bo mają różne uzasadnienie wobec
/// użytkownika i różne skutki przy odmowie:
///
/// PLAYBACK - bez nich wbudowany odtwarzacz (Web Playback SDK) nie wstanie
/// w ogóle. "streaming" wymaga konta Premium; "user-read-email" oraz
/// "user-read-private" są wymagane przez sam SDK do potwierdzenia rodzaju
/// konta, nie przez AMC do czegokolwiek innego.
///
/// LIBRARY - czytanie kolekcji. Brak tych zakresów daje puste listy, ale
/// odtwarzanie działa.
///
/// CONTROL - sterowanie i stan odtwarzania przez Web API (przenoszenie
/// odtwarzania na urządzenie AMC, czytanie co gra). Bez nich zostaje
/// sterowanie wyłącznie wewnątrz SDK.
///
/// NIE DODAWAJ tu zakresów zapisu ("user-library-modify",
/// "playlist-modify-*") dopóki AMC nie ma funkcji, która ich naprawdę używa.
/// Zakres bez funkcji to prośba o zgodę bez powodu, a użytkownik czytnikiem
/// ekranu musi wysłuchać całej listy uprawnień na stronie Spotify.
/// </summary>
public static class SpotifyScopes
{
    public static readonly string[] Playback =
    [
        "streaming",
        "user-read-email",
        "user-read-private"
    ];

    public static readonly string[] Library =
    [
        "user-library-read",
        "user-follow-read",
        "playlist-read-private",
        "playlist-read-collaborative"
    ];

    public static readonly string[] Control =
    [
        "user-read-playback-state",
        "user-modify-playback-state",
        "user-read-currently-playing"
    ];

    /// <summary>Pełny zestaw w kolejności grup, rozdzielony spacjami.</summary>
    public static string Requested { get; } =
        string.Join(' ', Playback.Concat(Library).Concat(Control));

    /// <summary>
    /// Czy przyznany zakres pozwala uruchomić wbudowany odtwarzacz.
    /// Spotify potrafi przyznać część zakresów, a "streaming" pomija dla
    /// konta bez Premium - wtedy trzeba to powiedzieć wprost, zamiast
    /// pokazywać odtwarzacz, który nigdy nie zagra.
    /// </summary>
    public static bool AllowsPlayback(string? grantedScope) =>
        Contains(grantedScope, "streaming");

    public static bool AllowsLibrary(string? grantedScope) =>
        Contains(grantedScope, "user-library-read");

    public static bool AllowsRemoteControl(string? grantedScope) =>
        Contains(grantedScope, "user-modify-playback-state");

    public static IReadOnlyList<string> Missing(string? grantedScope)
    {
        var granted = Split(grantedScope);
        return Split(Requested)
            .Where(scope => !granted.Contains(scope, StringComparer.Ordinal))
            .ToArray();
    }

    private static bool Contains(string? scope, string expected) =>
        Split(scope).Contains(expected, StringComparer.Ordinal);

    private static string[] Split(string? scope) =>
        string.IsNullOrWhiteSpace(scope)
            ? []
            : scope.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
