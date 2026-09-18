using AccessibleMediaController.Core.Sessions;

namespace AccessibleMediaController.Core.Spotify;

/// <summary>
/// Odwzorowanie rodzajów pozycji AMC na adresy URI biblioteki Spotify oraz
/// rozdzielenie dwóch widoków AMC: pojedyncze pozycje grywalne to Ulubione,
/// kontenery to Biblioteka. Ta sama umowa co w TIDAL
/// (<see cref="Tidal.TidalCollectionSemantics"/>), żeby oba serwisy
/// zachowywały się pod tymi samymi skrótami identycznie.
///
/// Adres URI składamy z rodzaju i <see cref="MediaItem.ExternalId"/>, bo klient
/// biblioteki Spotify zapisuje w tym polu goły identyfikator, nie pełny adres.
/// Gdy jednak w polu jest już pełny adres "spotify:...", bierzemy go bez zmian -
/// zdublowany prefiks daje 400 i wyglądałby jak odmowa uprawnień.
/// </summary>
public static class SpotifyCollectionSemantics
{
    public static bool UsesFavorites(MediaItemKind kind) =>
        kind is MediaItemKind.Track or MediaItemKind.Episode;

    public static bool UsesLibrary(MediaItemKind kind) =>
        kind is MediaItemKind.Album or MediaItemKind.Artist or MediaItemKind.Playlist or MediaItemKind.Podcast;

    public static bool IsCollectionKind(MediaItemKind kind) =>
        UsesFavorites(kind) || UsesLibrary(kind);

    public static bool IsMember(MediaItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return UsesFavorites(item.Kind) ? item.IsFavorite
            : UsesLibrary(item.Kind) && item.IsInLibrary;
    }

    public static void ApplyMembership(MediaItem item, bool isMember)
    {
        ArgumentNullException.ThrowIfNull(item);
        item.IsFavorite = UsesFavorites(item.Kind) && isMember;
        item.IsInLibrary = UsesLibrary(item.Kind) && isMember;
    }

    /// <summary>Nazwa typu adresu URI Spotify dla rodzaju pozycji AMC.</summary>
    public static string? UriType(MediaItemKind kind) => kind switch
    {
        MediaItemKind.Track => "track",
        MediaItemKind.Album => "album",
        MediaItemKind.Artist => "artist",
        MediaItemKind.Playlist => "playlist",
        MediaItemKind.Podcast => "show",
        MediaItemKind.Episode => "episode",
        _ => null
    };

    public static bool TryBuildUri(MediaItem item, out string uri)
    {
        ArgumentNullException.ThrowIfNull(item);
        uri = string.Empty;
        var id = item.ExternalId;
        if (string.IsNullOrWhiteSpace(id)) return false;
        id = id.Trim();
        if (id.StartsWith("spotify:", StringComparison.Ordinal))
        {
            var czesci = id.Split(':', StringSplitOptions.RemoveEmptyEntries);
            if (czesci.Length < 3) return false;
            uri = id;
            return true;
        }
        var type = UriType(item.Kind);
        if (type is null) return false;
        // Identyfikatory Spotify to base62. Cokolwiek innego oznacza pozycję z
        // innego źródła (plik lokalny, radio) i nie ma czego zapisywać.
        if (!id.All(char.IsLetterOrDigit)) return false;
        uri = $"spotify:{type}:{id}";
        return true;
    }

    /// <summary>
    /// Jawny stan docelowy. Gdy użytkownik nie wskazał kierunku (jedno
    /// naciśnięcie skrótu odwraca stan), kierunek liczymy ze stanu POTWIERDZONEGO
    /// przez API, nie z lokalnych flag: pozycje z wyszukiwania i z kontenerów
    /// noszą flagi starsze niż ostatni zapis i odwracanie po nich raz po raz
    /// wysyłało to samo żądanie.
    /// </summary>
    public static bool ResolveAddition(
        IReadOnlyList<string> uris,
        IReadOnlyDictionary<string, bool> confirmedMembership,
        bool? requestedAddition)
    {
        ArgumentNullException.ThrowIfNull(uris);
        ArgumentNullException.ThrowIfNull(confirmedMembership);
        if (requestedAddition is { } requested) return requested;
        return !uris.All(uri => confirmedMembership.TryGetValue(uri, out var member) && member);
    }

    /// <summary>Komunikat o niezgodnym rodzaju pozycji dla danego skrótu.</summary>
    public static string IncompatibleMessage(bool favorites) => favorites
        ? "Ctrl+Shift+U zmienia Ulubione dla utworów i odcinków Spotify"
        : "Ctrl+Shift+L zmienia Bibliotekę dla albumów, wykonawców, playlist i podcastów Spotify";
}
