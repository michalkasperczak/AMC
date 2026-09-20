using System.Globalization;
using System.Net.Http;
using AccessibleMediaController.Core.Presentation;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Core.Spotify;
using AccessibleMediaController.Windows.Services;

namespace AccessibleMediaController.Windows;

/// <summary>
/// Otwieranie zawartosci albumu, playlisty i wykonawcy Spotify (strzalka w prawo).
///
/// Zasady, ktorych trzeba sie tu trzymac:
/// - Widok zawartosci jest CACHE z sieci. Po ponownym uruchomieniu AMC nie moze
///   zostac przywrocony jako zapamietane miejsce, bo lista bylaby pusta pod
///   techniczna nazwa widoku.
/// - Wolna odpowiedz sieci NIE MA prawa przestawic listy, gdy uzytkownik w tym
///   czasie zmienil sesje, widok albo zaznaczenie. Czytnik przeczytalby wtedy
///   cos innego, niz uzytkownik wybral.
/// - Playlisty redakcyjne Spotify i playlisty innych osob sa zamkniete przez
///   Spotify (listopad 2024). Trzeba to powiedziec SPOKOJNIE i konkretnie,
///   a nie jako blad programu.
/// </summary>
public partial class MainWindow
{
    private const string SpotifyContentsViewPrefix = "Spotify:";

    /// <summary>
    /// Prefiks wiersza "Wczytaj więcej utworów". WŁASNY, nie podcastowy: wiersz
    /// doładowania podcastu ma inne zachowanie (lokalna lista odcinków), a
    /// podszycie się pod niego sprawiłoby, że Enter szukałby odcinków, których
    /// tu nie ma.
    /// </summary>
    private const string SpotifyTracksLoadMoreRowPrefix = "spotify-more-tracks:";

    private readonly Dictionary<string, SpotifyContainerViewState> _spotifyContainerViews =
        new(StringComparer.Ordinal);

    /// <summary>Widoki, w ktorych doladowanie wlasnie trwa - drugi Enter nie dubluje zapytania.</summary>
    private readonly HashSet<string> _spotifyTracksLoading = new(StringComparer.Ordinal);

    private long _spotifyNavigationVersion;

    /// <summary>
    /// Anulowanie biezacej pracy Spotify. Odczyt katalogu z doladowywaniem stron
    /// jest DLUGA sciezka; przy nowej nawigacji albo zamykaniu okna nie moze
    /// zostac wieczna praca w tle, ktora dalej wisi na sieci. Poprzedni token
    /// anulujemy JAWNIE, bez spekulacyjnych wyscigow.
    /// </summary>
    private CancellationTokenSource _spotifyWorkCancellation = new();

    private CancellationToken BeginSpotifyWork()
    {
        var previous = _spotifyWorkCancellation;
        _spotifyWorkCancellation = new CancellationTokenSource();
        previous.Cancel();
        previous.Dispose();
        return _spotifyWorkCancellation.Token;
    }

    private void CancelSpotifyWork()
    {
        try { _spotifyWorkCancellation.Cancel(); }
        catch (ObjectDisposedException) { }
    }

    /// <summary>
    /// Transport HTTP wstrzykiwany przez TESTY. Produkcja zostawia null i klient
    /// tworzy wlasny transport. Dzieki temu test UI moze przejsc CALA droga -
    /// wiersz doladowania, zapytanie, dolozenie pozycji - bez sieci i bez konta.
    /// </summary>
    internal HttpClient? SpotifyHttpClientForTests { get; set; }

    // Token podany wprost przez test UI: zadnego czytania poswiadczen konta.
    internal string? SpotifyAccessTokenForTests { get; set; }

    private SpotifyApiClient CreateSpotifyApiClient() => new(SpotifyHttpClientForTests);

    private sealed record SpotifyContainerViewState(
        MediaItem Container,
        IReadOnlyList<MediaItem> Items,
        ArtistBrowseSection? ArtistSection = null,
        bool IsArtistOverview = false,
        // Adres "next" od Spotify dla kategorii "Utwory". Niepusty znaczy:
        // katalog ma WIECEJ pozycji, niz widac - i wiersz doladowania ma sens.
        string? NextTracksUrl = null);

    /// <summary>Wynik jednego odczytu kontenera: pozycje plus to, co wiadomo o dalszych stronach.</summary>
    private sealed record SpotifyContainerLoad(
        IReadOnlyList<MediaItem>? Items,
        string? NextTracksUrl = null,
        SpotifyArtistTracksFailure? TracksFailure = null,
        string? ResolvedArtistName = null);

    private static string SpotifyContentsView(MediaItem container) =>
        $"{SpotifyContentsViewPrefix}{container.ExternalId}";

    /// <summary>
    /// Nazwa widoku kontenera dla KONKRETNEJ sesji Spotify. Druga sesja
    /// (Librespot) ma wlasny prefiks, inaczej nadpisalaby widok pierwszej.
    /// </summary>
    private static string SpotifyContainerViewName(string sessionId, MediaItem container) =>
        string.Equals(sessionId, "spotify", StringComparison.Ordinal)
            ? SpotifyContentsView(container)
            : $"{SpotifyContentsViewPrefix}{sessionId}:{container.ExternalId}";

    private static bool IsSpotifyContentsView(string viewName) =>
        viewName.StartsWith(SpotifyContentsViewPrefix, StringComparison.Ordinal);

    private static string SpotifyContainerLabel(MediaItemKind kind) => kind switch
    {
        MediaItemKind.Album => "Album Spotify",
        MediaItemKind.Playlist => "Playlista Spotify",
        MediaItemKind.Artist => "Wykonawca Spotify",
        MediaItemKind.Podcast => "Podcast Spotify",
        _ => "Spotify"
    };

    /// <summary>
    /// Czy strzalka w prawo ma co otworzyc. Utwor i odcinek nie sa kontenerami.
    /// </summary>
    private static bool CanOpenSpotifyContainer(MediaItem? item) =>
        item is { Kind: MediaItemKind.Album or MediaItemKind.Playlist or MediaItemKind.Artist
            or MediaItemKind.Podcast }
        && item.ExternalId is { Length: > 0 };

    /// <summary>
    /// Otwiera kontener Spotify. Dla wykonawcy bez wskazanej sekcji pokazuje
    /// PRZEGLAD (kategorie) tym samym mechanizmem co TIDAL - nie od razu albumy,
    /// bo uzytkownik ma wybrac, czy chce wydania czy utwory.
    /// </summary>
    private async Task OpenSpotifyContainerAsync(
        MediaItem container, ArtistBrowseSection? artistSection = null)
    {
        if (!CanOpenSpotifyContainer(container))
        {
            Announce(container.Kind is MediaItemKind.Track or MediaItemKind.Episode
                ? "Enter odtwarza tę pozycję"
                : "Tego elementu Spotify nie można otworzyć");
            return;
        }

        // Spotify nie udostepnia related-artists od listopada 2024. Kategoria
        // musi zostac JAWNIE odrzucona - nie wolno po cichu podstawic albumow,
        // bo uzytkownik dostalby cos innego, niz wybral. Dlatego ta galaz jest
        // PRZED rozpoznaniem przegladu i przed jakimkolwiek zapytaniem.
        if (artistSection == ArtistBrowseSection.SimilarArtists)
        {
            Announce("Spotify nie udostępnia podobnych wykonawców. "
                + "Wybierz Albumy albo Utwory");
            return;
        }

        if (container.Kind == MediaItemKind.Artist && artistSection is null)
        {
            OpenSpotifyArtistOverview(container);
            return;
        }

        var label = artistSection?.Label() ?? SpotifyContainerLabel(container.Kind);
        _spotifyNavigationVersion++;
        var startContext = CaptureServiceInteractionContext(_spotifyNavigationVersion);
        var sessionAtStart = startContext.SessionId;
        var cancellation = BeginSpotifyWork();

        try
        {
            var loadTask = LoadSpotifyContainerItemsAsync(container, artistSection, null, cancellation);
            var progressDelay = Task.Delay(TimeSpan.FromMilliseconds(1400), cancellation);
            if (await Task.WhenAny(loadTask, progressDelay).ConfigureAwait(true) == progressDelay
                && CanPresentSpotifyResponse(startContext))
            {
                Announce(
                    $"Wczytywanie {label.ToLower(CultureInfo.CurrentCulture)}: {container.Title}");
            }
            var load = await loadTask.ConfigureAwait(true);
            if (_isClosing) return;

            // Brak NAZWY wykonawcy nie jest awaria sieci ani pustym katalogiem -
            // powiedz dokladnie, czego brakuje, zamiast szukac po surowym ID.
            if (load.TracksFailure == SpotifyArtistTracksFailure.UnknownArtistName)
            {
                if (CanPresentSpotifyResponse(startContext))
                {
                    Announce("Nie udało się ustalić nazwy tego wykonawcy w Spotify, "
                        + "a wyszukiwanie utworów jej wymaga. Otwórz Albumy wykonawcy");
                }
                return;
            }

            // Spotify odmawia zawartosci playlist redakcyjnych i playlist innych
            // osob. To nie awaria - uzytkownik ma wiedziec, ZE tak jest i DLACZEGO.
            if (load.Items is null)
            {
                if (CanPresentSpotifyResponse(startContext))
                {
                    Announce(container.Kind == MediaItemKind.Playlist
                        ? $"Spotify nie udostępnia zawartości tej playlisty: {container.Title}. "
                            + "Dotyczy playlist redakcyjnych Spotify i playlist innych osób"
                        : artistSection is not null
                            ? $"Spotify nie udostępnia kategorii {label} wykonawcy {container.Title}"
                            : $"Spotify nie udostępnia zawartości: {label}, {container.Title}");
                }
                return;
            }

            var items = load.Items;
            foreach (var item in items)
            {
                if (container.Kind == MediaItemKind.Album && item.Kind == MediaItemKind.Track)
                {
                    item.RelatedAlbumExternalId = container.ExternalId;
                    item.RelatedAlbumTitle = container.Title;
                }
                if (container.Kind == MediaItemKind.Artist
                    && string.IsNullOrWhiteSpace(item.RelatedArtistExternalId))
                {
                    item.RelatedArtistExternalId = container.ExternalId;
                    item.RelatedArtistName = container.Title;
                }
            }

            var viewName = StoreSpotifyContainerForSession(
                sessionAtStart, container, items, artistSection, load.NextTracksUrl);

            if (!CanPresentSpotifyResponse(startContext))
            {
                DiagnosticLog.Info("spotify-navigation",
                    $"Zachowano dane bez zmiany widoku; żądanie: {startContext.NavigationVersion}; elementów: {items.Count}.");
                return;
            }

            if (items.Count == 0)
            {
                NavigateTo(viewName);
                FocusMediaList();
                Announce(artistSection is not null
                    ? $"Kategoria {label}: Spotify nie zwrócił elementów dla wykonawcy {container.Title}"
                    : $"{label} {container.Title} nie zawiera dostępnych elementów");
                return;
            }

            NavigateTo(viewName);
            PrepareViewFocusContext($"{label}, {container.Title}");
            FocusMediaList();
            // Kategoria "Utwory" idzie przez wyszukiwanie katalogu (top-tracks
            // usuniete w lutym 2026), wiec to PARTIA. Komunikat mowi, ILE
            // POBRANO, i czy w katalogu jest wiecej - nie przedstawia liczby
            // jako wszystkich utworow wykonawcy i nie przypisuje Spotify
            // NASZEGO limitu partii.
            if (artistSection == ArtistBrowseSection.Tracks)
                Announce(SpotifyTracksBatchMessage(container.Title, items.Count, load.NextTracksUrl));
            DiagnosticLog.Info("spotify-navigation",
                $"Otwarto {container.ExternalId}; elementów: {items.Count}; "
                + $"dalsze strony: {(string.IsNullOrEmpty(load.NextTracksUrl) ? "nie" : "tak")}.");
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            DiagnosticLog.Error(
                "spotify-container",
                $"Nie otwarto elementu {container.ExternalId}.",
                exception);
            if (CanPresentSpotifyResponse(startContext))
                Announce($"Nie udało się otworzyć: {label}, {container.Title}. {exception.Message}");
        }
    }

    /// <summary>
    /// Komunikat partii utworow. Mowi POBRANO/WYSWIETLONO N i czy jest wiecej.
    /// Celowo NIE mowi "Spotify udostepnia wybor z katalogu" bez zastrzezenia:
    /// obciecie do jednej partii jest NASZE, nie Spotify.
    /// </summary>
    private static string SpotifyTracksBatchMessage(string artistTitle, int count, string? next) =>
        string.IsNullOrEmpty(next)
            ? $"Utwory wykonawcy {artistTitle}: wyświetlono {count}. "
                + "To wszystko, co Spotify zwróciło dla tego zapytania"
            : $"Utwory wykonawcy {artistTitle}: pobrano {count}. "
                + "W katalogu Spotify jest więcej — wybierz Wczytaj więcej utworów";

    /// <summary>
    /// Pokazuje kategorie wykonawcy Spotify (przeglad). Widok przegladu jest
    /// SPISEM kategorii - nie ma w nim elementow multimedialnych, wiec lista
    /// pozycji zostaje pusta i wiersze buduje wspolna fabryka kategorii.
    /// </summary>
    private void OpenSpotifyArtistOverview(MediaItem artist)
    {
        var sessionId = _sessions.Current.Id;
        if (!SpotifyPlaybackSettingsResolver.IsSpotifySession(sessionId)) return;
        var view = SpotifyContainerViewName(sessionId, artist);
        _spotifyContainerViews[view] = new SpotifyContainerViewState(artist, [], IsArtistOverview: true);
        NavigateTo(view);
        PrepareViewFocusContext($"Wykonawca, {artist.Title}");
        FocusMediaList();
    }

    private static string SpotifyTracksLoadMoreRowId(string viewName) =>
        $"{SpotifyTracksLoadMoreRowPrefix}{viewName}";

    private static bool IsSpotifyTracksLoadMoreRow(MediaItem? item) =>
        item?.Id.StartsWith(SpotifyTracksLoadMoreRowPrefix, StringComparison.Ordinal) == true;

    /// <summary>
    /// Wiersz "Wczytaj więcej utworów" dopisany na koniec kategorii Utwory.
    ///
    /// Wiersz jest DOSTĘPNY tak samo, jak podcastowy: zwykła pozycja listy,
    /// osiągalna strzałkami, z etykietą mówiącą, co się stanie po Enterze.
    /// Nie jest natomiast elementem multimedialnym — nie wchodzi do kolejki,
    /// biblioteki, presetów, playlist ani zapisu, bo nie ma czego odtworzyć.
    /// </summary>
    private List<MediaItemRow> AppendSpotifyTracksLoadMoreRow(
        List<MediaItemRow> rows, SpotifyContainerViewState state, string viewName)
    {
        if (state.ArtistSection != ArtistBrowseSection.Tracks
            || string.IsNullOrEmpty(state.NextTracksUrl))
        {
            return rows;
        }
        var loadMoreItem = new MediaItem
        {
            Id = SpotifyTracksLoadMoreRowId(viewName),
            Title = "Wczytaj więcej utworów",
            Kind = MediaItemKind.Folder,
            IsAvailable = true
        };
        rows.Add(new MediaItemRow(
            loadMoreItem,
            $"Wczytaj więcej utworów wykonawcy {state.Container.Title}, "
                + $"wczytano {state.Items.Count}",
            "Wczytaj więcej utworów",
            loadMoreSpotifyTracksViewName: viewName));
        return rows;
    }

    private string StoreSpotifyContainerForSession(
        string sessionId,
        MediaItem container,
        IReadOnlyList<MediaItem> items,
        ArtistBrowseSection? artistSection = null,
        string? nextTracksUrl = null)
    {
        if (!SpotifyPlaybackSettingsResolver.IsSpotifySession(sessionId))
            throw new ArgumentException("Nieznana sesja Spotify.", nameof(sessionId));
        var session = _sessions.FindSession(sessionId)
            ?? throw new InvalidOperationException("Sesja Spotify nie jest zarejestrowana.");
        var owned = items.Select(item => sessionId == "spotify"
            ? item : SpotifySessionItemCopies.ForSession(item, sessionId)).ToArray();
        var existing = session.Items.GroupBy(item => item.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        for (var index = 0; index < owned.Length; index++)
        {
            if (!existing.TryGetValue(owned[index].Id, out var registered)) continue;
            var incoming = owned[index];
            // Zachowaj obiekt z jego kolejką; dopełnij jedynie pobrane relacje.
            if (!string.IsNullOrWhiteSpace(incoming.RelatedAlbumExternalId))
            {
                registered.RelatedAlbumExternalId = incoming.RelatedAlbumExternalId;
                registered.RelatedAlbumTitle = incoming.RelatedAlbumTitle;
            }
            if (!string.IsNullOrWhiteSpace(incoming.RelatedArtistExternalId))
            {
                registered.RelatedArtistExternalId = incoming.RelatedArtistExternalId;
                registered.RelatedArtistName = incoming.RelatedArtistName;
            }
            owned[index] = registered;
        }
        session.AddItemsById(owned);
        var viewName = SpotifySectionViewName(sessionId, container, artistSection);
        _spotifyContainerViews[viewName] =
            new SpotifyContainerViewState(container, owned, artistSection, NextTracksUrl: nextTracksUrl);
        RestoreSpotifyRememberedPositions();
        return viewName;
    }

    /// <summary>
    /// Doklada NASTEPNA partie utworow wykonawcy do widoku kategorii "Utwory".
    ///
    /// Trzy rzeczy, ktorych nie wolno tu zepsuc:
    /// - kolejna strona idzie DOKLADNIE pod adres "next" od Spotify; nie budujemy
    ///   offsetu na wlasna reke,
    /// - pozycje juz obecne w widoku odsiewamy po ExternalId, ale WYLACZNIE w
    ///   ramach tego katalogu - nie ruszamy katalogu sesji ani przypisan,
    /// - drugi Enter na wierszu doladowania nie moze wystrzelic drugiego
    ///   zapytania; stad <see cref="_spotifyTracksLoading"/>.
    /// </summary>
    private async Task LoadMoreSpotifyArtistTracksAsync(string viewName)
    {
        if (!_spotifyContainerViews.TryGetValue(viewName, out var state)
            || state.ArtistSection != ArtistBrowseSection.Tracks)
        {
            return;
        }
        if (string.IsNullOrEmpty(state.NextTracksUrl))
        {
            // Bez adresu nastepnej strony wiersz nie ma czego pobrac. Martwy
            // "wczytaj wiecej" czyta sie jak zepsuty program, wiec mowimy wprost.
            Announce("To już wszystkie utwory, które Spotify zwróciło dla tego wykonawcy");
            return;
        }
        if (!_spotifyTracksLoading.Add(viewName))
        {
            Announce("Wczytywanie kolejnych utworów już trwa");
            return;
        }

        var container = state.Container;
        _spotifyNavigationVersion++;
        var startContext = CaptureServiceInteractionContext(_spotifyNavigationVersion);
        var sessionAtStart = startContext.SessionId;
        var cancellation = BeginSpotifyWork();
        try
        {
            Announce($"Wczytywanie kolejnych utworów wykonawcy {container.Title}");
            var load = await LoadSpotifyContainerItemsAsync(
                container, ArtistBrowseSection.Tracks, state.NextTracksUrl, cancellation)
                .ConfigureAwait(true);
            if (_isClosing) return;

            if (load.Items is null)
            {
                if (CanPresentSpotifyLoadMore(startContext, viewName))
                    Announce($"Spotify nie zwróciło dalszych utworów wykonawcy {container.Title}");
                return;
            }

            // Pod nazwa widoku moze juz stac NOWSZY stan (np. po odswiezeniu F5),
            // wiec dokladamy do tego, co jest TERAZ, a nie do kopii z poczatku.
            if (!_spotifyContainerViews.TryGetValue(viewName, out var current)) return;
            var znane = new HashSet<string>(
                current.Items.Select(item => item.ExternalId ?? string.Empty),
                StringComparer.OrdinalIgnoreCase);
            var nowe = load.Items
                .Where(item => item.ExternalId is { Length: > 0 } && znane.Add(item.ExternalId))
                .ToArray();
            foreach (var item in nowe)
            {
                if (string.IsNullOrWhiteSpace(item.RelatedArtistExternalId))
                {
                    item.RelatedArtistExternalId = container.ExternalId;
                    item.RelatedArtistName = container.Title;
                }
            }

            // Kolejne strony przechodza tym samym produkcyjnym torem, co pierwsza:
            // rejestracja w sesji, zachowanie istniejacych obiektow z kolejka i
            // przywrocenie zapamietanych pozycji.
            StoreSpotifyContainerForSession(
                sessionAtStart,
                container,
                [.. current.Items, .. nowe],
                ArtistBrowseSection.Tracks,
                load.NextTracksUrl);

            if (!CanPresentSpotifyLoadMore(startContext, viewName))
            {
                DiagnosticLog.Info("spotify-navigation",
                    $"Doładowano {nowe.Length} utworów bez odświeżenia widoku {viewName}.");
                return;
            }

            var total = current.Items.Count + nowe.Length;
            // Widok odbudowuje sie PRODUKCYJNA sciezka i wraca na TEN SAM wiersz
            // doladowania, ktory przesunal sie w dol o dolozone pozycje.
            RefreshCurrentView(preferredItemId: SpotifyTracksLoadMoreRowId(viewName));
            Announce(nowe.Length == 0
                ? "Spotify nie zwróciło nowych utworów; " + SpotifyTracksBatchMessage(
                    container.Title, total, load.NextTracksUrl)
                : $"Dodano {nowe.Length}. " + SpotifyTracksBatchMessage(
                    container.Title, total, load.NextTracksUrl));
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            DiagnosticLog.Error("spotify-container",
                $"Nie doładowano utworów wykonawcy {container.ExternalId}.", exception);
            if (CanPresentSpotifyLoadMore(startContext, viewName))
                Announce($"Nie udało się wczytać kolejnych utworów. {exception.Message}");
        }
        finally
        {
            _spotifyTracksLoading.Remove(viewName);
        }
    }

    /// <summary>
    /// Czy doladowanie wolno jeszcze POKAZAC. Nie porownujemy tu zaznaczenia:
    /// doladowanie startuje Z wiersza doladowania, a po dolozeniu pozycji ten
    /// wiersz zmienia miejsce. Liczy sie, ze uzytkownik nie wyszedl z widoku.
    /// </summary>
    private bool CanPresentSpotifyLoadMore(
        ServiceInteractionContext startContext, string viewName)
    {
        var current = CaptureServiceInteractionContext(_spotifyNavigationVersion);
        return CanPresentServiceResponse
            && current.NavigationVersion == startContext.NavigationVersion
            && string.Equals(current.SessionId, startContext.SessionId, StringComparison.Ordinal)
            && string.Equals(current.View, viewName, StringComparison.Ordinal)
            && current.PlayerActive == startContext.PlayerActive;
    }

    private async Task<SpotifyContainerLoad> LoadSpotifyContainerItemsAsync(
        MediaItem container,
        ArtistBrowseSection? artistSection = null,
        string? continuationUrl = null,
        CancellationToken cancellationToken = default)
    {
        // Test UI podaje token JAWNIE, zeby nie dotykac danych konta ani sieci
        // autoryzacji. Produkcyjnie ta wlasciwosc jest null i token pochodzi z
        // normalnej sciezki poswiadczen.
        var accessToken = SpotifyAccessTokenForTests;
        if (accessToken is null)
        {
            var credentials = await _spotifyIntegration
                .GetPlaybackCredentialsAsync(cancellationToken)
                .ConfigureAwait(false);
            accessToken = credentials.AccessToken;
        }
        using var client = CreateSpotifyApiClient();
        var id = container.ExternalId ?? string.Empty;
        // Kategoria "Utwory" ma wlasna droge, bo Get Artist's Top Tracks zostalo
        // usuniete (luty 2026) - szczegoly w GetArtistTracksAsync.
        if (container.Kind == MediaItemKind.Artist && artistSection == ArtistBrowseSection.Tracks)
        {
            var batch = await client.GetArtistTracksAsync(
                accessToken,
                _spotifyIntegration.CountryCode,
                id,
                container.Title ?? string.Empty,
                cancellationToken,
                continuationUrl).ConfigureAwait(false);
            return batch.Failure is { } failure
                ? new SpotifyContainerLoad(null, TracksFailure: failure)
                : new SpotifyContainerLoad(
                    batch.Items, batch.Next, ResolvedArtistName: batch.ArtistName);
        }
        var items = container.Kind switch
        {
            MediaItemKind.Album => await client
                .GetAlbumTracksAsync(accessToken, id, cancellationToken)
                .ConfigureAwait(false),
            MediaItemKind.Playlist => await client
                .GetPlaylistTracksAsync(accessToken, id, cancellationToken)
                .ConfigureAwait(false),
            MediaItemKind.Artist => await client
                .GetArtistAlbumsAsync(accessToken, id, cancellationToken)
                .ConfigureAwait(false),
            MediaItemKind.Podcast => await client
                .GetShowEpisodesAsync(accessToken, id, cancellationToken)
                .ConfigureAwait(false),
            _ => null
        };
        // Opisy przychodza TYM SAMYM zapytaniem co odcinki. Bez przeniesienia
        // ich z klienta Alt+D na odcinku otwartym z podcastu nie mialby czego
        // przeczytac, mimo ze opis wlasnie przyszedl z Spotify.
        foreach (var pair in client.PodcastDescriptions)
            _spotifyPodcastDescriptions[pair.Key] = pair.Value;
        return new SpotifyContainerLoad(items);
    }

    /// <summary>
    /// Czy wolno jeszcze przestawic liste. Odpowiedz z sieci, ktora przyszla po
    /// tym, jak uzytkownik zmienil sesje, widok albo zaznaczenie, nie ma prawa
    /// przeniesc fokusu - czytnik przeczytalby nie to, co wybrano.
    ///
    /// Bramka jest WSPOLNA z TIDAL-em (<see cref="ServiceInteractionContext"/>);
    /// Spotify ma wlasny licznik nawigacji, ale nie wlasna kopie regul.
    /// </summary>
    private bool CanPresentSpotifyResponse(ServiceInteractionContext startContext) =>
        startContext.CanPresent(
            CaptureServiceInteractionContext(_spotifyNavigationVersion), CanPresentServiceResponse);
}
