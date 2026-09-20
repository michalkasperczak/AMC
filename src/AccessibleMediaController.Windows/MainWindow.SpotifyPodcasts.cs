using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Core.Spotify;
using AccessibleMediaController.Windows.Services;

namespace AccessibleMediaController.Windows;

/// <summary>
/// Widok zapisanych podcastow Spotify.
///
/// Dlaczego osobno od sesji "Podcasty" AMC: tamta czyta kanaly RSS i nic nie
/// wie o koncie Spotify. Odcinek Spotify NIE MA adresu RSS - ma
/// "spotify:episode:...". Probowanie otwierac go tamta droga konczylo sie
/// widokiem bez odcinkow, wiec podcasty Spotify chodza wlasna sciezka.
///
/// Zasady:
/// - Podcast jest KONTENEREM: Enter go nie odtwarza, strzalka w prawo wchodzi
///   w odcinki (to zalatwia juz <c>OpenSpotifyContainerAsync</c>).
/// - Odcinek jest nagraniem: Enter odtwarza, Escape wraca do widoku podcastow.
/// - Kazda sesja Spotify ma WLASNY widok. Dwie sesje wspoldziela konto, ale nie
///   kolejke ani zaznaczenie, wiec wspolna nazwa widoku mieszalaby je.
/// </summary>
public partial class MainWindow
{
    private const string SpotifyPodcastsViewSuffix = "podcasty";

    // Opisy podcastow i odcinkow po adresie spotify:. Trzymane obok modelu, bo
    // opis ma czesto kilka tysiecy znakow i nie moze trafic do etykiety wiersza
    // ani do zapisywanych ustawien.
    //
    // Slownik wspolbiezny, bo zapis idzie z watku puli (ConfigureAwait(false) w
    // pobieraniu), a odczyt z watku GUI przy Alt+D. Zwykly Dictionary psul by
    // sie przy pobieraniu w tle i jednoczesnym czytaniu opisu.
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, string>
        _spotifyPodcastDescriptions = new(StringComparer.Ordinal);

    /// <summary>Nazwa widoku podcastow danej sesji Spotify.</summary>
    internal static string SpotifyPodcastsViewName(string sessionId) => sessionId == "spotify"
        ? $"{SpotifyContentsViewPrefix}{SpotifyPodcastsViewSuffix}"
        : $"{SpotifyContentsViewPrefix}{sessionId}:{SpotifyPodcastsViewSuffix}";

    internal static bool IsSpotifyPodcastsView(string viewName) =>
        viewName.StartsWith(SpotifyContentsViewPrefix, StringComparison.Ordinal)
        && viewName.EndsWith(SpotifyPodcastsViewSuffix, StringComparison.Ordinal);

    /// <summary>
    /// Uklad widoku: najpierw zapisane podcasty alfabetycznie, potem zapisane
    /// odcinki. Rozdzielenie jest celowe - czytnik czyta liste po kolei, a
    /// przemieszanie kontenerow z nagraniami kazaloby sluchac kazdego wiersza,
    /// zeby wiedziec, czy Enter odtworzy, czy nie zrobi nic.
    ///
    /// Odcinki NIEzapisane (pobrane tylko do podgladu podcastu) tu nie wchodza:
    /// widok nazywa sie "zapisane", wiec ma pokazywac to, co uzytkownik zapisal.
    /// </summary>
    internal static IReadOnlyList<MediaItem> BuildSpotifyPodcastsView(
        IEnumerable<MediaItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        var wszystkie = items.ToArray();
        var podcasty = wszystkie
            .Where(item => item.Kind == MediaItemKind.Podcast)
            .OrderBy(item => item.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
        var odcinki = wszystkie
            .Where(item => item.Kind == MediaItemKind.Episode && item.IsFavorite)
            .OrderBy(item => item.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
        return [.. podcasty, .. odcinki];
    }

    /// <summary>
    /// Opis podcastu albo odcinka do odczytania na zadanie. Dla innych rodzajow
    /// zwraca null - "opis podcastu" utworu nie istnieje i nie wolno podstawiac
    /// pod niego czegokolwiek innego.
    /// </summary>
    internal static string? SpotifyPodcastDescription(
        IReadOnlyDictionary<string, string> descriptions,
        MediaItem? item)
    {
        ArgumentNullException.ThrowIfNull(descriptions);
        if (item?.ExternalId is not { Length: > 0 } id) return null;
        var uri = item.Kind switch
        {
            MediaItemKind.Podcast => $"spotify:show:{id}",
            MediaItemKind.Episode => $"spotify:episode:{id}",
            _ => null
        };
        if (uri is null) return null;
        return descriptions.TryGetValue(uri, out var opis) ? opis : null;
    }

    /// <summary>
    /// HOOK DLA MENU I SKROTU: otwiera widok zapisanych podcastow biezacej
    /// sesji Spotify. Skrotu ani pozycji menu ten plik NIE rejestruje - robi to
    /// okno glowne, zeby kolizje klawiszy byly rozstrzygane w jednym miejscu.
    /// </summary>
    public void ShowSpotifyPodcasts()
    {
        _ = ShowSpotifyPodcastsAsync();
    }

    private async Task ShowSpotifyPodcastsAsync()
    {
        var sessionAtStart = _sessions.Current.Id;
        if (!SpotifyPlaybackSettingsResolver.IsSpotifySession(sessionAtStart))
        {
            Announce("Podcasty Spotify są dostępne w sesji Spotify");
            return;
        }

        _spotifyNavigationVersion++;
        var startContext = CaptureServiceInteractionContext(_spotifyNavigationVersion);

        try
        {
            var loadTask = LoadSpotifyPodcastLibraryAsync();
            var progressDelay = Task.Delay(TimeSpan.FromMilliseconds(1400));
            if (await Task.WhenAny(loadTask, progressDelay).ConfigureAwait(true) == progressDelay
                && CanPresentSpotifyResponse(startContext))
            {
                Announce("Wczytywanie zapisanych podcastów Spotify");
            }

            var library = await loadTask.ConfigureAwait(true);
            if (_isClosing) return;

            if (library is null)
            {
                if (CanPresentSpotifyResponse(startContext))
                    Announce("Spotify nie udostępnia zapisanych podcastów tego konta");
                return;
            }

            var widok = BuildSpotifyPodcastsView([.. library.Shows, .. library.Episodes]);
            var viewName = StoreSpotifyPodcastsForSession(sessionAtStart, widok);

            if (!CanPresentSpotifyResponse(startContext))
            {
                DiagnosticLog.Info("spotify-navigation",
                    $"Zachowano podcasty bez zmiany widoku; żądanie: {startContext.NavigationVersion}; pozycji: {widok.Count}.");
                return;
            }

            NavigateTo(viewName);
            if (widok.Count == 0)
            {
                FocusMediaList();
                // Ostrzezenia PRZED werdyktem o pustym koncie. Spotify potrafi
                // odmowic jednej z dwoch list (podcasty albo zapisane odcinki) -
                // wtedy "nie masz zapisanych podcastow" byloby nieprawda, a
                // uzytkownik nie mialby zadnego sladu po odmowie.
                foreach (var warning in library.Warnings) Announce(warning);
                Announce(library.Warnings.Count > 0
                    ? "Spotify nie podało pełnej listy. Nie wiadomo, czy masz zapisane podcasty"
                    : "Nie masz zapisanych podcastów ani odcinków w Spotify");
                return;
            }

            PrepareViewFocusContext(
                $"Podcasty Spotify, podcastów: {library.Shows.Count}, zapisanych odcinków: "
                + widok.Count(item => item.Kind == MediaItemKind.Episode));
            FocusMediaList();
            foreach (var warning in library.Warnings) Announce(warning);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            DiagnosticLog.Error("spotify-podcasts", "Nie otwarto widoku podcastów Spotify.", exception);
            if (CanPresentSpotifyResponse(startContext))
                Announce($"Nie udało się wczytać podcastów Spotify. {exception.Message}");
        }
    }

    /// <summary>
    /// Alt+D w sesji Spotify. Opis pochodzi z katalogu Spotify, nie z kanalu RSS
    /// - odcinek Spotify nie ma adresu RSS, wiec sciezka podcastow AMC nie ma
    /// czego szukac.
    /// </summary>
    private void ShowSpotifyPodcastDescription(MediaItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var description = SpotifyPodcastDescription(_spotifyPodcastDescriptions, item);
        if (string.IsNullOrWhiteSpace(description))
        {
            Announce(item.Kind == MediaItemKind.Podcast
                ? "Spotify nie podało opisu tego podcastu"
                : "Spotify nie podało opisu tego odcinka");
            return;
        }
        var links = new List<InformationLink>();
        if (!string.IsNullOrWhiteSpace(item.PublicUri))
        {
            links.Add(new InformationLink(
                item.Kind == MediaItemKind.Podcast ? "Otwórz podcast w Spotify" : "Otwórz odcinek w Spotify",
                item.PublicUri));
        }
        var dialog = new InformationWindow(description, links, preferTextMode: true) { Owner = this };
        dialog.ShowDialog();
        Activate();
        if (_playerViewActive) FocusPlayerView();
        else RestoreMediaListFocusAfterRefresh();
    }

    private async Task<SpotifyPodcastLibrary?> LoadSpotifyPodcastLibraryAsync()
    {
        var credentials = await _spotifyIntegration
            .GetPlaybackCredentialsAsync(CancellationToken.None)
            .ConfigureAwait(false);
        using var client = new SpotifyApiClient();
        var library = await client
            .GetSavedPodcastsAsync(credentials.AccessToken, CancellationToken.None)
            .ConfigureAwait(false);
        foreach (var pair in client.PodcastDescriptions)
            _spotifyPodcastDescriptions[pair.Key] = pair.Value;
        return library;
    }

    private string StoreSpotifyPodcastsForSession(string sessionId, IReadOnlyList<MediaItem> items)
    {
        var session = _sessions.FindSession(sessionId)
            ?? throw new InvalidOperationException("Sesja Spotify nie jest zarejestrowana.");
        var owned = items.Select(item => sessionId == "spotify"
            ? item : SpotifySessionItemCopies.ForSession(item, sessionId)).ToArray();
        session.AddItemsById(owned);
        // Ta sama regula, co przy albumach i playlistach: jesli pozycja o tym ID
        // jest JUZ w sesji, do widoku wchodzi obiekt z sesji, nie swiezy z API.
        // Bez tego wiersz w widoku podcastow bylby innym obiektem niz ten w
        // kolejce - flaga "w kolejce" i pamiec czasu nie odczytywalyby sie.
        var registered = session.Items
            .GroupBy(item => item.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var widokPozycje = owned
            .Select(item => registered.TryGetValue(item.Id, out var istniejacy) ? istniejacy : item)
            .ToArray();
        var viewName = SpotifyPodcastsViewName(sessionId);
        // Widok podcastow jest pojemnikiem samym w sobie, wiec zapisujemy go
        // tym samym mechanizmem co album czy playliste - dzieki temu Escape,
        // pamiec pozycji i odswiezanie dzialaja bez osobnej sciezki.
        _spotifyContainerViews[viewName] = new SpotifyContainerViewState(
            new MediaItem
            {
                Kind = MediaItemKind.Folder,
                Title = "Podcasty Spotify",
                ExternalId = SpotifyPodcastsViewSuffix
            },
            widokPozycje);
        RestoreSpotifyRememberedPositions();
        return viewName;
    }
}
