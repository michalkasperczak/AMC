using AccessibleMediaController.Core.Configuration;

namespace AccessibleMediaController.Core.Podcasts;

/// <summary>
/// Jedna bramka decydujaca, czy otwarcie wyniku wyszukiwania ma dopisac go do
/// Biblioteki. Michal ustalil 20.09.2026: domyslnie NIE - Enter otwiera i gra,
/// swiadome dodanie zostaje pod Ctrl+Shift+L.
///
/// Bramka jest jedna dla wszystkich serwisow (radio, podcasty, YouTube, TIDAL,
/// Spotify i przyszle sesje), zeby dwie sciezki nie kopiowaly tego samego
/// warunku i nie rozjechaly sie po kolejnej zmianie.
/// </summary>
public static class SearchResultEnterPolicy
{
    /// <param name="behavior">Globalne ustawienie uzytkownika.</param>
    /// <param name="explicitLibraryRequest">
    /// Czy uzytkownik wprost zazadal Biblioteki (Ctrl+Shift+L albo menu
    /// „Biblioteka”). Taka prosba dodaje NIEZALEZNIE od ustawienia.
    /// </param>
    public static bool ShouldAddToLibrary(
        SearchResultEnterBehavior behavior,
        bool explicitLibraryRequest) =>
        explicitLibraryRequest || behavior == SearchResultEnterBehavior.AddToLibrary;
}
