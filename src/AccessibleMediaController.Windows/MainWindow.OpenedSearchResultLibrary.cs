using AccessibleMediaController.Core.Podcasts;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Windows.Services;

namespace AccessibleMediaController.Windows;

/// <summary>
/// Jeden punkt wykonania decyzji „Enter dodaje otwarty wynik do Biblioteki”.
///
/// Wolane WYLACZNIE ze sciezek, ktore faktycznie OTWIERAJA wynik: Enter (Open)
/// i Ctrl+Enter (TogglePlayback). NIE z <c>SelectSearchResultBrowserItem</c> —
/// ten sam wybor robia Informacje, kopiowanie i Kolejka, ktore niczego nie
/// otwieraja i nie moga dopisywac do Biblioteki.
///
/// Decyzje podejmuje <see cref="OpenedSearchResultLibraryPolicy"/>; tutaj zostaja
/// tylko trzy sposoby zapisu. Przyszla usluga dodaje przypadek W POLITYCE, a nie
/// piaty warunek w oknie glownym.
/// </summary>
public partial class MainWindow
{
    /// <summary>
    /// Dopisuje otwarty wynik do Biblioteki wlasciwej uslugi, gdy ustawienie tak
    /// mowi. Nigdy nie jest przelacznikiem: deklarujemy intencje DODANIA
    /// (<c>add: true</c>), zeby powtorny Enter nie usunal juz zapisanej pozycji.
    /// Blad zgody/sieci nie przerywa otwarcia — mowi o nim sciezka zapisu.
    /// </summary>
    private Task MaybeAddOpenedSearchResultToLibrary(SearchWindow.SearchResult result)
    {
        var item = result.Item;
        // Publiczny material internetowy (YouTube) trzyma czlonkostwo we WLASNEJ
        // parze kolekcji, nie na koncie uslugi. Awans robimy TUTAJ, bo tylko ta
        // sciezka wie, ze otwarcie zostalo ZAAKCEPTOWANE: materializacja wyniku
        // przygotowuje wylacznie podglad, a pauza i odmowa wcale tu nie trafiaja.
        if (string.Equals(result.SessionId, "podcasts", StringComparison.OrdinalIgnoreCase)
            && TryAddOpenedPublicInternetMediaToLibrary(item)) return Task.CompletedTask;
        var plan = OpenedSearchResultLibraryPolicy.ResolveForOpenedResult(
            _state.Settings.SearchResultEnterBehavior,
            result.SessionId,
            item);
        DiagnosticLog.Info(
            "search-enter-library",
            $"Otwarty wynik {result.SessionId}/{item.Kind}; "
            + $"ustawienie: {_state.Settings.SearchResultEnterBehavior}; plan: {plan}.");
        return OpenedSearchResultLibraryPolicy.ExecuteAsync(
            plan,
            () => AddOpenedRadioStationToLibrary(item),
            () => ChangeTidalCollectionMembershipAsync([item], add: true),
            () => ChangeSpotifyCollectionMembershipAsync([item], add: true));
    }

    /// <summary>
    /// Awansuje OTWARTY publiczny material internetowy z podgladow do kolekcji
    /// zapisanej, gdy globalne ustawienie tak mowi. Wylacznie w GORE: uzywa tego
    /// samego add-only mechanizmu co jawne Ctrl+Shift+L, wiec identyfikator
    /// odcinka, historia, kolejka, zakladki i metadane kolekcji zostaja
    /// nietkniete, a zadne czlonkostwo nie jest zdejmowane.
    ///
    /// Zwraca true, gdy element JEST publicznym materialem internetowym — wtedy
    /// sciezki radia, TIDALa i Spotify nie maja tu nic do zrobienia.
    /// </summary>
    private bool TryAddOpenedPublicInternetMediaToLibrary(MediaItem item)
    {
        if (item.Kind != MediaItemKind.Episode) return false;
        var episode = _state.Podcasts.Episodes.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, item.Id, StringComparison.Ordinal));
        if (episode is null
            || !PublicInternetMediaCollections.IsCollection(episode.SubscriptionId))
        {
            return false;
        }
        if (!SearchResultEnterPolicy.ShouldAddToLibrary(
                _state.Settings.SearchResultEnterBehavior,
                explicitLibraryRequest: false))
        {
            DiagnosticLog.Info(
                "search-enter-library",
                $"Otwarty materiał internetowy {episode.Id}; ustawienie: "
                + $"{_state.Settings.SearchResultEnterBehavior}; plan: podgląd bez zapisu.");
            return true;
        }
        // Juz zapisany zostaje zapisany: intencja to DODANIE, nie przelacznik.
        if (PublicInternetMediaCollections.IsSaved(episode.SubscriptionId)) return true;
        CapturePodcastState();
        if (!TryMovePublicInternetMediaMembership(episode, addToLibrary: true)) return true;
        ReloadPodcastSessionItems();
        QueueStateSave(announceFailure: true);
        DiagnosticLog.Info(
            "search-enter-library",
            $"Otwarty materiał internetowy {episode.Id} awansowany do kolekcji zapisanej.");
        if (string.Equals(_sessions.Current.Id, "podcasts", StringComparison.Ordinal))
            RefreshCurrentView(preferredItemId: episode.Id);
        return true;
    }

    /// <summary>
    /// Radio trzyma czlonkostwo w stanie AMC, nie na koncie uslugi: zadnego
    /// zapytania HTTP. Nie dotyka odtwarzania — otwarcie zrobila sciezka wyzej.
    /// </summary>
    private void AddOpenedRadioStationToLibrary(MediaItem item)
    {
        var station = _radioItems.FirstOrDefault(candidate => string.Equals(
                candidate.Id,
                item.Id,
                StringComparison.Ordinal))
            ?? _radioItems.FirstOrDefault(candidate =>
                candidate.Source is { Length: > 0 }
                && string.Equals(candidate.Source, item.Source, StringComparison.OrdinalIgnoreCase));
        if (station is null)
        {
            station = item;
            _radioItems.Add(station);
        }
        station.IsInLibrary = true;
        station.IsAvailable = true;
        station.PublicUri ??= station.Source;
        CaptureRadioState();
        QueueStateSave();
        // Bez skoku fokusu: odswiezamy tylko wtedy, gdy uzytkownik patrzy na
        // radio, i celujemy w ten sam wiersz.
        if (string.Equals(
                _sessions.Current.Id,
                OpenedSearchResultLibraryPolicy.RadioSessionId,
                StringComparison.Ordinal))
        {
            RefreshCurrentView(preferredItemId: station.Id);
        }
    }
}
