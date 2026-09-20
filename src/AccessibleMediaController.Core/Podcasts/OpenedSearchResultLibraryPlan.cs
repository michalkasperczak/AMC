using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Core.Spotify;
using AccessibleMediaController.Core.Tidal;

namespace AccessibleMediaController.Core.Podcasts;

/// <summary>
/// Co trzeba zrobic, zeby OTWARTY wynik wyszukiwania trafil do Biblioteki danej
/// uslugi. Jeden zestaw decyzji dla wszystkich sesji: nowa usluga bez wlasnego
/// zapisu dostaje <see cref="None"/>, a nie ciche pominiecie w pieciu miejscach.
/// </summary>
public enum OpenedSearchResultLibraryPlan
{
    /// <summary>Nie dopisujemy nic: ustawienie OFF, element juz zapisany albo usluga bez zapisu.</summary>
    None = 0,

    /// <summary>Stacja radiowa: czlonkostwo zyje w stanie AMC, bez wywolania sieciowego.</summary>
    RadioStation,

    /// <summary>Kolekcja TIDAL: zapis na koncie przez pisarza TIDAL.</summary>
    TidalCollection,

    /// <summary>Kolekcja Spotify: zapis na koncie przez pisarza Spotify.</summary>
    SpotifyCollection
}

/// <summary>
/// Jeden punkt decyzji dla „Enter dodaje otwarty wynik do Biblioteki”.
///
/// Zasady ustalone z Michalem 20.09.2026:
/// - Domyslnie (OpenWithoutLibrary) Enter TYLKO otwiera; zadnej mutacji, zadnego HTTP.
/// - Przy AddToLibrary Enter otwiera I dodaje — tak samo w radiu, TIDAL i Spotify.
/// - To jest intencja DODANIA, nie przelacznik: element, ktory JUZ jest zapisany,
///   nie moze zostac usuniety powtornym Enter, wiec plan jest wtedy <see cref="OpenedSearchResultLibraryPlan.None"/>.
/// - Usluga, ktorej AMC nie umie zapisac, nie blokuje otwarcia: plan None.
/// - Nowa usluga trafia TUTAJ, nie do piatej kopii warunku w oknie glownym.
/// </summary>
public static class OpenedSearchResultLibraryPolicy
{
    public const string RadioSessionId = "radio";
    public const string TidalSessionId = "tidal";

    /// <summary>
    /// Wejscie produkcyjne: cale rozstrzygniecie dla otwartego wyniku. Okno
    /// glowne i testy licza to samo, tym samym kodem.
    /// </summary>
    public static OpenedSearchResultLibraryPlan ResolveForOpenedResult(
        SearchResultEnterBehavior behavior,
        string? sessionId,
        MediaItem? item,
        bool explicitLibraryRequest = false)
    {
        if (item is null) return OpenedSearchResultLibraryPlan.None;
        var spotify = SpotifyPlaybackSettingsResolver.IsSpotifySession(sessionId);
        var tidal = string.Equals(sessionId, TidalSessionId, StringComparison.OrdinalIgnoreCase);
        var hasExternalId = item.ExternalId is { Length: > 0 };
        var alreadyMember = spotify
            ? SpotifyCollectionSemantics.IsMember(item)
            : tidal
                ? TidalCollectionSemantics.IsMember(item)
                : item.IsInLibrary;
        var writableKind = spotify
            ? SpotifyCollectionSemantics.IsCollectionKind(item.Kind) && hasExternalId
            : tidal
                ? TidalCollectionSemantics.IsCollectionKind(item.Kind) && hasExternalId
                : item.Kind == MediaItemKind.Station;
        return Resolve(
            behavior,
            sessionId,
            item,
            spotify,
            alreadyMember,
            writableKind,
            explicitLibraryRequest);
    }

    /// <param name="behavior">Globalne ustawienie uzytkownika.</param>
    /// <param name="sessionId">Sesja, do ktorej nalezy otwarty wynik.</param>
    /// <param name="item">Otwarty wynik.</param>
    /// <param name="isSpotifySession">
    /// Czy identyfikator sesji nalezy do Spotify (dwa identyfikatory silnikow).
    /// </param>
    /// <param name="isAlreadyMember">
    /// Czy element jest JUZ czlonkiem wlasciwej kolekcji uslugi.
    /// </param>
    /// <param name="isWritableKind">Czy usluga potrafi zapisac ten rodzaj elementu.</param>
    public static OpenedSearchResultLibraryPlan Resolve(
        SearchResultEnterBehavior behavior,
        string? sessionId,
        MediaItem? item,
        bool isSpotifySession,
        bool isAlreadyMember,
        bool isWritableKind,
        bool explicitLibraryRequest = false)
    {
        if (item is null) return OpenedSearchResultLibraryPlan.None;
        if (!SearchResultEnterPolicy.ShouldAddToLibrary(behavior, explicitLibraryRequest))
            return OpenedSearchResultLibraryPlan.None;
        // Intencja to DODANIE. Zapisany element zostaje zapisany.
        if (isAlreadyMember) return OpenedSearchResultLibraryPlan.None;
        if (!isWritableKind) return OpenedSearchResultLibraryPlan.None;

        if (isSpotifySession) return OpenedSearchResultLibraryPlan.SpotifyCollection;
        if (string.Equals(sessionId, TidalSessionId, StringComparison.OrdinalIgnoreCase))
            return OpenedSearchResultLibraryPlan.TidalCollection;
        if (string.Equals(sessionId, RadioSessionId, StringComparison.OrdinalIgnoreCase))
            return item.Kind == MediaItemKind.Station
                ? OpenedSearchResultLibraryPlan.RadioStation
                : OpenedSearchResultLibraryPlan.None;
        // Przyszla usluga: brak wlasnego zapisu nie moze przeszkodzic w otwarciu.
        return OpenedSearchResultLibraryPlan.None;
    }

    /// <summary>
    /// Wykonuje plan. Wywolanie jest LENIWE: przy planie None nie rusza zadnego
    /// pisarza, wiec domyslne ustawienie nie wysyla zadnego zapytania HTTP.
    /// Zawsze deklarujemy intencje DODANIA — nigdy przelacznika.
    /// </summary>
    public static Task ExecuteAsync(
        OpenedSearchResultLibraryPlan plan,
        Action addRadioStation,
        Func<Task> addTidalCollectionItem,
        Func<Task> addSpotifyCollectionItem)
    {
        ArgumentNullException.ThrowIfNull(addRadioStation);
        ArgumentNullException.ThrowIfNull(addTidalCollectionItem);
        ArgumentNullException.ThrowIfNull(addSpotifyCollectionItem);
        switch (plan)
        {
            case OpenedSearchResultLibraryPlan.RadioStation:
                addRadioStation();
                return Task.CompletedTask;
            case OpenedSearchResultLibraryPlan.TidalCollection:
                return addTidalCollectionItem();
            case OpenedSearchResultLibraryPlan.SpotifyCollection:
                return addSpotifyCollectionItem();
            default:
                return Task.CompletedTask;
        }
    }
}
