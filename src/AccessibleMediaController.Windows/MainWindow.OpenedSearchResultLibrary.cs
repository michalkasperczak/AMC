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
