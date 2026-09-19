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
    private readonly Dictionary<string, string> _spotifyPodcastDescriptions =
        new(StringComparer.Ordinal);

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
    private void ShowSpotifyPodcasts()
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

        var requestVersion = ++_spotifyNavigationVersion;
        var viewAtStart = _currentView;
        var itemAtStart = SelectedItem?.Id;

        try
        {
            var loadTask = LoadSpotifyPodcastLibraryAsync();
            var progressDelay = Task.Delay(TimeSpan.FromMilliseconds(1400));
            if (await Task.WhenAny(loadTask, progressDelay).ConfigureAwait(true) == progressDelay
                && CanPresentSpotifyResponse(requestVersion, sessionAtStart, viewAtStart, itemAtStart))
            {
                Announce("Wczytywanie zapisanych podcastów Spotify");
            }

            var library = await loadTask.ConfigureAwait(true);
            if (_isClosing) return;

            if (library is null)
            {
                if (CanPresentSpotifyResponse(requestVersion, sessionAtStart, viewAtStart, itemAtStart))
                    Announce("Spotify nie udostępnia zapisanych podcastów tego konta");
                return;
            }

            var widok = BuildSpotifyPodcastsView([.. library.Shows, .. library.Episodes]);
            var viewName = StoreSpotifyPodcastsForSession(sessionAtStart, widok);

            if (!CanPresentSpotifyResponse(requestVersion, sessionAtStart, viewAtStart, itemAtStart))
            {
                DiagnosticLog.Info("spotify-navigation",
                    $"Zachowano podcasty bez zmiany widoku; żądanie: {requestVersion}; pozycji: {widok.Count}.");
                return;
            }

            NavigateTo(viewName);
            if (widok.Count == 0)
            {
                FocusMediaList();
                Announce("Nie masz zapisanych podcastów ani odcinków w Spotify");
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
            if (CanPresentSpotifyResponse(requestVersion, sessionAtStart, viewAtStart, itemAtStart))
                Announce($"Nie udało się wczytać podcastów Spotify. {exception.Message}");
        }
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
            owned);
        RestoreSpotifyRememberedPositions();
        return viewName;
    }
}
