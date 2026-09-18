using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Windows.Services;

namespace AccessibleMediaController.Windows;

/// <summary>
/// Zdalne wyszukiwanie w katalogu Spotify (Ctrl+F w sesji Spotify), na wzor
/// TIDAL.
///
/// Zasady, ktorych trzeba sie tu trzymac:
/// - Wynik z sieci DOPELNIA wyszukiwanie lokalne, nie zastepuje go. Gdy Spotify
///   nie odpowie (brak sieci, wygasle logowanie, 429), okno musi dalej pokazac
///   zapisane pozycje sesji i powiedziec, DLACZEGO katalogu nie ma. Zwrocenie
///   pustej listy albo wyczyszczenie wynikow czytaloby sie jak awaria programu.
/// - Pozycja z katalogu to NIE biblioteka konta. Gdy ta sama pozycja jest juz w
///   sesji, oddajemy egzemplarz z sesji (z jego flagami i pamiecia pozycji);
///   nowe pozycje dostaja stabilny identyfikator, zeby fokus mial do czego
///   wrocic po zamknieciu okna.
/// - Rodzaje wynikow sa te, ktore Spotify oddaje i ktore AMC umie pokazac:
///   wykonawca, album, utwor, playlista, podcast i odcinek.
/// </summary>
public partial class MainWindow
{
    /// <summary>
    /// Czy sesja ma wlasne zdalne wyszukiwanie katalogu. Trzymane osobno, zeby
    /// warunek w ShowSearch i opis katalogu nie rozjechaly sie ze soba.
    /// </summary>
    internal static bool SessionHasRemoteSearch(string sessionId, bool allServices) =>
        allServices
        || string.Equals(sessionId, "radio", StringComparison.Ordinal)
        || string.Equals(sessionId, "podcasts", StringComparison.Ordinal)
        || string.Equals(sessionId, "tidal", StringComparison.Ordinal)
        || string.Equals(sessionId, "spotify", StringComparison.Ordinal);

    /// <summary>
    /// Nazwa katalogu czytana przez czytnik ekranu ("Wyszukiwanie w ...").
    /// </summary>
    internal static string RemoteSearchLabel(string sessionId, bool allServices) => allServices
        ? "katalogach radia, podcastów i YouTube"
        : sessionId switch
        {
            "podcasts" => "katalogach Apple Podcasts, Spreaker i YouTube",
            "tidal" => "katalogu TIDAL",
            "spotify" => "katalogu Spotify",
            _ => "katalogu radia"
        };

    /// <summary>
    /// Scala wynik z katalogu Spotify z tym, co sesja juz zna.
    ///
    /// Pozycja rozpoznana po parze rodzaj-ExternalId wraca jako egzemplarz z
    /// sesji: ma juz wlasciwy identyfikator, flagi kolekcji i zapamietana
    /// pozycje odtwarzania. Bez tego ta sama plyta stalaby na liscie dwa razy -
    /// raz z biblioteki, raz z katalogu - a czytnik przeczytalby duplikat.
    /// Nowa pozycja dostaje identyfikator wyliczony z adresu uslugi, wiec po
    /// zamknieciu okna fokus wraca na to samo miejsce.
    /// </summary>
    internal static IReadOnlyList<MediaItem> MergeSpotifySearchResults(
        IReadOnlyList<MediaItem> remoteItems,
        IEnumerable<MediaItem> sessionItems)
    {
        var znane = new Dictionary<string, MediaItem>(StringComparer.Ordinal);
        foreach (var item in sessionItems)
        {
            if (string.IsNullOrWhiteSpace(item.ExternalId)) continue;
            znane.TryAdd(SpotifySearchKey(item.Kind, item.ExternalId), item);
        }

        var wynik = new List<MediaItem>();
        var uzyte = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in remoteItems)
        {
            if (string.IsNullOrWhiteSpace(item.ExternalId)) continue;
            var klucz = SpotifySearchKey(item.Kind, item.ExternalId);
            if (!uzyte.Add(klucz)) continue;
            if (znane.TryGetValue(klucz, out var znany))
            {
                wynik.Add(znany);
                continue;
            }
            wynik.Add(new MediaItem
            {
                Id = $"spotify:{klucz}",
                Kind = item.Kind,
                Title = item.Title,
                Artist = item.Artist,
                ExternalId = item.ExternalId,
                Source = item.Source,
                PublicUri = item.PublicUri,
                Duration = item.Duration,
                IsAvailable = item.IsAvailable,
                RelatedAlbumExternalId = item.RelatedAlbumExternalId,
                RelatedAlbumTitle = item.RelatedAlbumTitle,
                RelatedArtistExternalId = item.RelatedArtistExternalId,
                RelatedArtistName = item.RelatedArtistName
            });
        }
        return wynik;
    }

    private static string SpotifySearchKey(MediaItemKind kind, string? externalId) =>
        $"{kind}:{externalId}";

    /// <summary>
    /// Zdalne wyszukiwanie Spotify dla okna wyszukiwania. Wyjatek celowo idzie
    /// wyzej: okno wyszukiwania zamienia go na komunikat i ZOSTAWIA wyniki
    /// lokalne. Ciche zwrocenie pustej listy ukryloby przyczyne.
    /// </summary>
    private async Task<IReadOnlyList<SearchWindow.SearchResult>> PrepareSpotifySearchAsync(
        string query,
        CancellationToken cancellationToken)
    {
        var remote = await _spotifyIntegration.SearchAsync(query, cancellationToken).ConfigureAwait(true);
        var session = _sessions.FindSession("spotify");
        var scalone = MergeSpotifySearchResults(
            remote,
            session?.Items ?? (IEnumerable<MediaItem>)_spotifyItems);
        DiagnosticLog.Info(
            "spotify-search",
            $"Katalog Spotify zwrócił {remote.Count} pozycji; po scaleniu z sesją: {scalone.Count}.");
        return scalone
            .Select(item => new SearchWindow.SearchResult("spotify", item))
            .ToArray();
    }
}
