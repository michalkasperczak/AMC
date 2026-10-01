using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using AccessibleMediaController.Core.Sonos;

namespace AccessibleMediaController.Windows;

/// <summary>
/// PLAYLISTY SONOSA w oknie glownym. SWIADOMA kopia dyscypliny odebranego
/// <see cref="MainWindow.ShowSonosFavoritesAsync"/>, nie nowy framework: te same
/// bramki, te same bilety, ta sama kolejnosc granic i te same drogi odzyskania.
///
/// Dlaczego osobny plik, a nie rozszerzenie ulubionych: playlista ma WLASNY
/// endpoint, WLASNY typ i WLASNA akcje kolejki. Udawanie ulubionego, zeby
/// skorzystac z gotowej drogi, konczy sie POST na zly endpoint.
/// </summary>
public partial class MainWindow
{
    private SonosPlaylistsWindow? _sonosPlaylistsWindow;

    /// <summary>Czy JAWNY odczyt playlist wlasnie trwa. Powtorka odmawia bez GET.</summary>
    private bool _sonosPlaylistsInFlight;

    /// <summary>
    /// WLASNY bilet bramki odczytu playlist: spozniony przelot A nie zwalnia
    /// trwajacego B i nie pokazuje swojego okna.
    /// </summary>
    private int _sonosPlaylistsGateTicket;

    /// <summary>ZERO POST: polecenie nie poszlo, bo konto sie zmienilo.</summary>
    internal const string PlaylistPlayNotSentAccountChanged =
        "Nie wysłałem polecenia uruchomienia playlisty, bo konto Sonos się zmieniło. "
        + "Otwórz playlisty jeszcze raz";

    /// <summary>ZERO POST: polecenie nie poszlo, bo zmienil sie cel.</summary>
    internal const string PlaylistPlayNotSentTargetChanged =
        "Nie wysłałem polecenia uruchomienia playlisty, bo cel sterowania Sonos się zmienił. "
        + "Sprawdź cel skrótem Control F5 i otwórz playlisty jeszcze raz";

    /// <summary>
    /// PROBA POSZLA, ale wyniku nie znamy. Uczciwie: nie obiecujemy cofniecia i
    /// nie twierdzimy, ze muzyka gra.
    /// </summary>
    internal const string PlaylistPlayAttemptedOutcomeUnknown =
        "Polecenie uruchomienia playlisty poszło do Sonosa, ale kontekst zmienił się w trakcie, "
        + "więc nie ma potwierdzenia wyniku; sprawdź stan grupy";

    internal const string PlaylistPlayAbandonedOutcomeUnknown =
        "Uruchamianie playlisty zostało przerwane. Polecenie mogło już pójść do Sonosa, "
        + "więc nie ma potwierdzenia wyniku i nie obiecuję cofnięcia; sprawdź stan grupy";

    internal bool SonosPlaylistsInFlightForTests => _sonosPlaylistsInFlight;

    internal SonosPlaylistsWindow? OpenSonosPlaylistsWindowForTests => _sonosPlaylistsWindow;

    /// <summary>Ile okien playlist POWSTALO. Guard nie ma prawa tego podniesc.</summary>
    internal int SonosPlaylistsWindowsCreatedForTests { get; private set; }

    /// <summary>Ile razy poszedl JAWNY odczyt playlist. Odmowa nie liczy sie.</summary>
    internal int SonosPlaylistsReadsStartedForTests { get; private set; }

    /// <summary>
    /// TESTOWY punkt podstawienia POKAZANIA okna. Produkcyjnie null, czyli
    /// prawdziwe modalne <c>ShowDialog</c> z wlascicielem na oknie glownym.
    /// </summary>
    internal Action<SonosPlaylistsWindow>? PresentSonosPlaylistsOverrideForTests { get; set; }

    internal Task? LastSonosPlaylistsTaskForTests { get; private set; }

    internal Task ShowSonosPlaylistsForTests() => ShowSonosPlaylistsAsync();

    /// <summary>
    /// Wejscie z Biblioteki: Enter na kategorii "Playlisty Sonos". Zadanie
    /// trzymamy, zeby pomiar mial na czym czekac; nie czekamy w watku UI.
    /// </summary>
    private void StartSonosPlaylistsView() =>
        LastSonosPlaylistsTaskForTests = ShowSonosPlaylistsAsync();

    /// <summary>
    /// JAWNE otwarcie PLAYLIST Sonos (Enter na kategorii Biblioteki).
    ///
    /// Kolejnosc jest cala trescia bezpieczenstwa i jest TA SAMA co w ulubionych:
    /// warunek prezentacji na WEJSCIU (zeby nie poszedl zaden GET), granica konta
    /// PRZED wzieciem biletu bramki, dom, SWIEZY odczyt, a potem TE SAME granice
    /// PONOWNIE po await i PRZED utworzeniem okna.
    /// </summary>
    internal async Task ShowSonosPlaylistsAsync()
    {
        if (_sonosPlaylistsInFlight)
        {
            // SWIADOMA powtorka: krotka informacja, ZERO dodatkowych GET.
            Announce("Odczyt playlist Sonos już trwa");
            return;
        }

        if (_sonosPlaylistsWindow is not null)
        {
            Announce("Okno playlist Sonos jest już otwarte");
            try
            {
                _sonosPlaylistsWindow.Activate();
            }
            catch (InvalidOperationException)
            {
            }

            return;
        }

        // GRANICA FOKUSU NA WEJSCIU, PRZED JAKIMKOLWIEK GET.
        if (!CanPresentSonosPlaylists())
        {
            Announce("Playlisty Sonos nie zostały otwarte, bo okno AMC nie jest aktywne. "
                + "Wróć do AMC i ponów otwarcie playlist");
            return;
        }

        var backend = EnsureSonosBackend();
        // OPCJONALNA granica: zaplecze, ktore nie umie playlist, mowi to uczciwie
        // i NIE udaje pustej listy.
        if (backend is not ISonosPlaylistsSessionBackend playlistsBackend)
        {
            Announce("To zaplecze Sonos nie udostępnia odczytu playlist");
            return;
        }

        ApplySonosAccountBinding();

        // DOM z WYBRANEGO domu sesji. NIGDY z identyfikatora grupy i NIGDY
        // zgadniety: aktywna grupa nie jest tu wymagana do samego odczytu.
        var householdId = _state.Sonos.SelectedHouseholdId;
        if (string.IsNullOrWhiteSpace(householdId))
        {
            Announce("Nie wiadomo, z którego domu Sonos czytać playlisty. "
                + "Użyj polecenia Wybierz dom Sonos, a jeśli nie ma konta, "
                + "otwórz połączenie z Sonos w oknie konta Sonos");
            return;
        }

        var gate = ++_sonosPlaylistsGateTicket;
        _sonosPlaylistsInFlight = true;
        var ticket = _sonosTargetTicket;
        var token = EnsureSonosCancellation().Token;
        Announce(SonosPlaylistsLabels.Loading);
        try
        {
            SonosPlaylistsReadsStartedForTests++;
            var playlists = await playlistsBackend
                .ReadPlaylistsAsync(householdId, token).ConfigureAwait(true);

            // GRANICE PO AWAIT: zmiana konta, wyjscie z sesji, zamkniecie okna,
            // zmiana celu/domu albo cudzy przelot koncza TEN przelot. ZADNE z nich
            // nie pokazuje spoznionego okna ze stara lista.
            if (ApplySonosAccountBinding()) return;
            if (ticket != _sonosTargetTicket || _isClosing || gate != _sonosPlaylistsGateTicket) return;
            if (!IsSonosSession(_sessions?.Current.Id)) return;
            // DOM MUSI byc ten sam, o ktory pytalismy.
            if (!string.Equals(_state.Sonos.SelectedHouseholdId, householdId, StringComparison.Ordinal)) return;

            if (!playlists.Succeeded || playlists.Playlists is null)
            {
                // ODMOWA, LIMIT, TIMEOUT, BLAD i PORZUCENIE: STALY komunikat ze
                // wspolnego zestawu. NIGDY nie udajemy swiezej pustej listy.
                Announce(playlists.Message);
                return;
            }

            var items = playlists.Playlists.Items;

            // GRANICA FOKUSU PONOWNIE, PRZED UTWORZENIEM okna. Odmowa NIE MOZE
            // podniesc licznika utworzonych okien.
            if (!CanPresentSonosPlaylists())
            {
                Announce("Playlisty Sonos nie zostały otwarte, bo okno AMC nie jest aktywne. "
                    + "Wróć do AMC i ponów otwarcie playlist");
                return;
            }

            // PUSTA lista to POPRAWNY wynik: okno otwiera sie z dostepnym pustym
            // stanem, bez bledu i bez udawanej pozycji.
            Announce(SonosPlaylistsLabels.SummarizeCount(items.Count));

            // URUCHAMIANIE podajemy TYLKO gdy zaplecze ma OPCJONALNA granice
            // ladowania. CEL jest CAPTUROWANY TERAZ, z niezmiennych danych: dom,
            // aktywna grupa i BILET celu - stara lista nie wysle identyfikatora
            // przez inne konto ani do innej grupy.
            var loadBackend = backend as ISonosPlaylistLoadSessionBackend;
            var group = SonosActiveGroup;
            var window = loadBackend is null
                ? new SonosPlaylistsWindow(items)
                : new SonosPlaylistsWindow(
                    items,
                    group?.Name,
                    request => LoadSonosPlaylistAsync(
                        loadBackend, householdId, group?.Id, ticket, request),
                    playlist => AssignSonosPlaylistPreset(
                        playlist, _sonosPlaylistsWindow!, householdId, ticket));
            SonosPlaylistsWindowsCreatedForTests++;
            _sonosPlaylistsWindow = window;
            try
            {
                PresentSonosPlaylists(window);
            }
            finally
            {
                _sonosPlaylistsWindow = null;
            }

            // BEZPIECZNY POWROT FOKUSU: tylko gdy okno glowne nadal jest aktywne i
            // kontekst sie nie zmienil. Nie kradniemy fokusu obcemu oknu.
            if (_isClosing || !IsActive) return;
            if (ticket != _sonosTargetTicket || gate != _sonosPlaylistsGateTicket) return;
            if (!IsSonosSession(_sessions?.Current.Id)) return;
            if (_playerViewActive) return;
            RestoreMediaListFocusAfterRefresh();
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            // SPOZNIONY A nie uwalnia trwajacego B: bramke zwalnia tylko jej
            // WLASCICIEL.
            if (gate == _sonosPlaylistsGateTicket) _sonosPlaylistsInFlight = false;
        }
    }

    /// <summary>
    /// Czy WOLNO pokazac playlisty TERAZ. Ten sam wzorzec, co brama ulubionych:
    /// okno widoczne i AKTYWNE, nie zamykane, w sesji Sonos i bez innego
    /// WIDOCZNEGO okna potomnego. Guard jest PRODUKCYJNY - punkt podstawienia
    /// pokazania siedzi ZA nim, nie przed nim.
    /// </summary>
    private bool CanPresentSonosPlaylists()
    {
        if (_isClosing) return false;
        if (!IsVisible || !IsActive || !IsEnabled) return false;
        if (!IsSonosSession(_sessions?.Current.Id)) return false;
        return !OwnedWindows.OfType<Window>().Any(window => window.IsVisible);
    }

    /// <summary>Ile razy okno playlist POPROSILO o uruchomienie. Odmowa liczy sie tutaj.</summary>
    internal int SonosPlaylistLoadRequestsForTests { get; private set; }

    /// <summary>Ile razy uruchomienie playlisty DOSZLO do zaplecza (czyli do POST).</summary>
    internal int SonosPlaylistLoadsSentForTests { get; private set; }

    /// <summary>
    /// JAWNA akcja "Odtwórz"/Enter z okna playlist: DOKLADNIE JEDEN POST
    /// loadPlaylist dla WSKAZANEJ playlisty w AKTYWNEJ grupie.
    ///
    /// Duch odebranego <c>LoadSonosFavoriteAsync</c> zachowany co do joty: ta SAMA
    /// bramka jednego polecenia (<c>_sonosCommandInFlight</c>), ten SAM wlasny
    /// bilet bramki i ta SAMA droga zwolnienia - zadnej drugiej, sprzecznej
    /// kolejki polecen.
    ///
    /// AKCJA KOLEJKI JEST JAWNA: INSERT + playOnCompletion true, dokladnie jak w
    /// ulubionych i z tego samego powodu (dokumentacja queue-action mowi wprost,
    /// ze INSERT przenosi glowice na pierwsza wstawiona pozycje). Bez playModes,
    /// bez drugiego Play i bez retry.
    ///
    /// ZYCIE ZLECENIA JEST PRZYWIAZANE DO OKNA, KTORE JE ZLECILO: status idzie DO
    /// TEJ instancji albo NIGDZIE - zadnego zapasowego ogloszenia w oknie glownym
    /// i zadnego wejscia w nowo otwarte okno.
    /// </summary>
    private async Task LoadSonosPlaylistAsync(
        ISonosPlaylistLoadSessionBackend backend,
        string householdId,
        string? groupId,
        int targetTicket,
        SonosPlaylistsWindow.PlayRequest request)
    {
        ArgumentNullException.ThrowIfNull(backend);
        ArgumentNullException.ThrowIfNull(request);
        var origin = request.Origin;
        var playlist = request.Playlist;
        SonosPlaylistLoadRequestsForTests++;

        // ZLECAJACY MUSI ZYC JUZ TERAZ: zamkniete okno nie dostaje odpowiedzi, a
        // my nie szukamy zastepczego adresata.
        if (!IsLivePlaylistsOrigin(origin)) return;
        NextSonosPlaybackIntent();

        // GRANICA konta PRZED czymkolwiek: po RZECZYWISTEJ zmianie konta stary
        // identyfikator playlisty nie ma prawa pojsc przez NOWE konto.
        if (ApplySonosAccountBinding())
        {
            AnnounceInPlaylistsOrigin(origin, PlaylistPlayNotSentAccountChanged);
            return;
        }

        // CEL musi byc TEN SAM, ktory okno dostalo przy otwarciu. Zmiana
        // czegokolwiek konczy droge BEZ POST - nie przekierowujemy materialu do
        // innej grupy.
        if (_isClosing) return;
        if (targetTicket != _sonosTargetTicket
            || !IsSonosSession(_sessions?.Current.Id)
            || !string.Equals(_state.Sonos.SelectedHouseholdId, householdId, StringComparison.Ordinal))
        {
            AnnounceInPlaylistsOrigin(origin, PlaylistPlayNotSentTargetChanged);
            return;
        }

        if (string.IsNullOrWhiteSpace(groupId)
            || SonosActiveGroup is not { } group
            || !string.Equals(group.Id, groupId, StringComparison.Ordinal))
        {
            // BRAK grupy albo ZMIENIONY cel: uczciwe wyjasnienie wskazujace
            // PRAWDZIWA droge (Ctrl+F5). ZERO POST.
            AnnounceInPlaylistsOrigin(origin, SonosPlaylistsLabels.PlayNeedsGroup);
            return;
        }

        // TA SAMA bramka jednego polecenia Sonos, co reszta sterowania.
        if (_sonosCommandInFlight)
        {
            AnnounceInPlaylistsOrigin(origin, SonosPlaylistsLabels.PlayAlreadyInFlight);
            return;
        }

        var gateTicket = ++_sonosCommandGateTicket;
        // TOKEN LOKALNY POWIAZANY Z TOKENEM SESJI: anuluje go albo zamkniecie TEGO
        // okna, albo istniejace porzucenie pracy sesji. Zamkniecie okna NIE wola
        // CancelSonosPendingWork i nie rusza cudzych oczekiwan.
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(
            EnsureSonosCancellation().Token, request.Lifetime);
        var token = lifetime.Token;
        _sonosCommandInFlight = true;
        try
        {
            SonosPlaylistLoadsSentForTests++;
            // JEDEN POST. Bez presetu, bez zapisu, bez drugiego Play/Toggle.
            var result = await backend.LoadPlaylistAsync(
                groupId,
                playlist.Id,
                SonosFavoriteQueueAction.Insert,
                playOnCompletion: true,
                token).ConfigureAwait(true);

            // GRANICE PO AWAIT, a NAJPIERW tozsamosc zlecajacego. O "probie"
            // mowimy WYLACZNIE gdy result.RequestSent: RequestSent=false to ZERO
            // prob wyslania, wiec "wyslano" byloby klamstwem. ZYWE okno w KAZDEJ
            // galezi dostaje koniec - zostawienie go na "Czekaj" to blad.
            if (!IsLivePlaylistsOrigin(origin)) return;
            if (_isClosing) return;
            var accountChanged = ApplySonosAccountBinding();
            if (accountChanged
                || targetTicket != _sonosTargetTicket
                || !IsSonosSession(_sessions?.Current.Id))
            {
                AnnounceInPlaylistsOrigin(origin, result.RequestSent
                    ? PlaylistPlayAttemptedOutcomeUnknown
                    : accountChanged
                        ? PlaylistPlayNotSentAccountChanged
                        : PlaylistPlayNotSentTargetChanged);
                return;
            }

            var accepted = result.Status == SonosGroupOperationStatus.Attempted
                && result.Outcome?.Status == SonosControlApiStatus.Success;
            // HTTP 200 to PRZYJECIE ZLECENIA, nie dowod, ze muzyka gra. Tozsamosc
            // pozycji bierzemy z NASZEJ listy - to my wyslalismy ten identyfikator.
            AnnounceInPlaylistsOrigin(origin, accepted
                ? SonosPlaylistsLabels.DescribePlayAccepted(SonosPlaylistsLabels.Describe(playlist))
                : result.Message);

            // ISTNIEJACY jawny odczyt stanu tego SAMEGO celu - zeby odtwarzacz i
            // bramki polecen nie zostaly ze starym stanem. Bez drugiego POST i bez
            // nowego pollingu.
            try
            {
                await ReadSonosGroupStateAsync().ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                LogSonosPlaylistLoadFailure("odczyt po uruchomieniu", exception);
            }
        }
        catch (OperationCanceledException)
        {
            // Zamkniecie TEGO okna albo wlasne zamykanie AMC: CISZA. PORZUCONY
            // POST mogl sie mimo wszystko wykonac - nie obiecujemy cofniecia.
            // ZYWE okno to jednak INNY przypadek: stoi na "Czekaj" i MUSI dostac
            // uczciwy koniec.
            if (_isClosing) return;
            AnnounceInPlaylistsOrigin(origin, PlaylistPlayAbandonedOutcomeUnknown);
        }
        catch (Exception exception)
        {
            LogSonosPlaylistLoadFailure("wysłanie uruchomienia", exception);
            if (!IsLivePlaylistsOrigin(origin)) return;
            if (_isClosing || targetTicket != _sonosTargetTicket) return;
            AnnounceInPlaylistsOrigin(origin,
                "Nie udało się wykonać polecenia uruchomienia playlisty. Spróbuj ponownie.");
        }
        finally
        {
            // SPOZNIONY przelot A nie uwalnia trwajacego B: bramke zwalnia tylko
            // jej WLASCICIEL - ta SAMA produkcyjna droga co reszta polecen.
            ReleaseSonosCommandGate(gateTicket);
        }
    }

    /// <summary>
    /// Czy ZLECAJACA instancja okna nadal jest ZYWYM adresatem. Kryterium jest
    /// TOZSAMOSC, nie "jakiekolwiek otwarte okno": okno B nie jest nastepca okna
    /// A, a okno glowne nie jest jego zapasowym glosnikiem.
    /// </summary>
    private bool IsLivePlaylistsOrigin(SonosPlaylistsWindow origin) =>
        ReferenceEquals(_sonosPlaylistsWindow, origin) && origin.IsLiveOwnerTarget;

    /// <summary>
    /// STATUS DO TEJ INSTANCJI albo NIGDZIE. Gdy zlecajace okno nie zyje, wynik
    /// jest CICHY: nie przenosi sie do okna glownego i nie wchodzi w nowe okno.
    /// </summary>
    private void AnnounceInPlaylistsOrigin(SonosPlaylistsWindow origin, string message)
    {
        if (!IsLivePlaylistsOrigin(origin)) return;
        origin.AnnounceForOwner(message);
    }

    /// <summary>
    /// LOG diagnostyczny bez sekretu: RODZAJ wyjatku, zero tresci, zero adresu,
    /// zero identyfikatorow.
    /// </summary>
    private static void LogSonosPlaylistLoadFailure(string stage, Exception exception) =>
        System.Diagnostics.Debug.WriteLine(
            $"Sonos: uruchomienie playlisty - {stage} nie udalo sie ({exception.GetType().Name}).");

    /// <summary>WASKI hook pomiarowy: PRODUKCYJNA droga uruchomienia z okna.</summary>
    internal Task LoadSonosPlaylistForTests(
        ISonosPlaylistLoadSessionBackend backend,
        string householdId,
        string? groupId,
        int targetTicket,
        SonosPlaylistsWindow.PlayRequest request) =>
        LoadSonosPlaylistAsync(backend, householdId, groupId, targetTicket, request);

    /// <summary>
    /// POKAZANIE okna. WLASCICIEL jest WYMAGANY - modal bez wlasciciela moze
    /// zostac za AMC. Wiazemy go PRZED punktem podstawienia, zeby pomiar mierzyl
    /// TO SAMO powiazanie co droga produkcyjna.
    /// </summary>
    private void PresentSonosPlaylists(SonosPlaylistsWindow window)
    {
        window.Owner = this;

        if (PresentSonosPlaylistsOverrideForTests is { } present)
        {
            present(window);
            return;
        }

        window.ShowDialog();
    }
}
