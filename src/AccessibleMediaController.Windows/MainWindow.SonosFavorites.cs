using System.Windows;
using AccessibleMediaController.Core.Sonos;

namespace AccessibleMediaController.Windows;

/// <summary>
/// F2: DOSTEPNY PODGLAD ULUBIONYCH Sonos pod ISTNIEJACYM "Pokaż ulubione"
/// (Ctrl+U). Zadnego nowego skrotu, zadnej kopii pozycji w menu, zadnej zmiany
/// innych sesji - w sesji nie-Sonos polecenie robi dokladnie to, co robilo.
///
/// Trzy obietnice, wzorowane na juz zmierzonym wyborze domu:
///  1) SWIEZOSC: kazde JAWNE otwarcie robi NOWY odczyt przez to samo zaplecze
///     sesji. Zero cache, zero pollingu, zero subskrypcji, zero obrazow.
///  2) TYLKO PODGLAD: jedyna operacja to GET ulubionych. Zaden POST, zadne
///     odtwarzanie, zadne przypisanie presetu - to F3.
///  3) SPOZNIONY WYNIK NIC NIE PSUJE: granice sprawdzamy PRZED I/O i PONOWNIE
///     po await. Zmiana konta, domu, sesji, zamkniecie okna, odejscie fokusu
///     albo cudzy przelot koncza TEN przelot - nie otwieraja starego okna, nie
///     ruszaja grup i nie kradna fokusu.
/// </summary>
public partial class MainWindow
{
    /// <summary>
    /// Bramka PODGLADU ULUBIONYCH. WLASNA, nie wspolna z wyborem domu ani
    /// odswiezaniem grup: to trzy rozne drogi uzytkownika i jedna nie ma prawa
    /// blokowac drugiej.
    /// </summary>
    private bool _sonosFavoritesInFlight;

    /// <summary>
    /// Wlasciciel bramki. TA SAMA regula co przy wyborze domu: porzucony przelot
    /// A widzi w finally CUDZY bilet i NIE odblokuje trwajacego B.
    /// </summary>
    private int _sonosFavoritesGateTicket;

    /// <summary>Otwarte okno podgladu albo null. Jedno naraz.</summary>
    private SonosFavoritesWindow? _sonosFavoritesWindow;

    internal bool SonosFavoritesInFlightForTests => _sonosFavoritesInFlight;

    internal SonosFavoritesWindow? OpenSonosFavoritesWindowForTests => _sonosFavoritesWindow;

    /// <summary>Ile okien podgladu POWSTALO. Guard nie ma prawa tego podniesc.</summary>
    internal int SonosFavoritesWindowsCreatedForTests { get; private set; }

    /// <summary>Ile razy poszedl JAWNY odczyt ulubionych. Odmowa nie liczy sie.</summary>
    internal int SonosFavoritesReadsStartedForTests { get; private set; }

    /// <summary>
    /// TESTOWY punkt podstawienia POKAZANIA okna. Produkcyjnie null, czyli
    /// prawdziwe modalne <c>ShowDialog</c> z wlascicielem na oknie glownym.
    /// </summary>
    internal Action<SonosFavoritesWindow>? PresentSonosFavoritesOverrideForTests { get; set; }

    internal Task? LastSonosFavoritesTaskForTests { get; private set; }

    internal Task ShowSonosFavoritesForTests() => ShowSonosFavoritesAsync();

    /// <summary>
    /// Wejscie z routera polecen: istniejace <c>CommandIds.ViewFavorites</c> w
    /// sesji Sonos. Zadanie trzymamy, zeby pomiar mial na czym czekac; nie
    /// czekamy na nie w watku UI.
    /// </summary>
    private void StartSonosFavoritesView() => LastSonosFavoritesTaskForTests = ShowSonosFavoritesAsync();

    /// <summary>
    /// JAWNE "Pokaż ulubione" w sesji Sonos.
    ///
    /// Kolejnosc jest cala trescia bezpieczenstwa i jest TA SAMA co przy wyborze
    /// domu: warunek prezentacji na WEJSCIU (zeby nie poszedl zaden GET), potem
    /// granica konta PRZED wzieciem biletu bramki (bo <c>ApplySonosAccountBinding</c>
    /// sam wola <c>CancelSonosPendingWork</c>, ktory bilet PODNOSI), potem dom,
    /// potem swiezy odczyt, potem TE SAME granice PONOWNIE po await i PRZED
    /// utworzeniem okna.
    /// </summary>
    internal async Task ShowSonosFavoritesAsync()
    {
        if (_sonosFavoritesInFlight)
        {
            // SWIADOMA powtorka: krotka informacja, ZERO dodatkowych GET.
            Announce("Odczyt ulubionych Sonos już trwa");
            return;
        }

        if (_sonosFavoritesWindow is not null)
        {
            Announce("Okno ulubionych Sonos jest już otwarte");
            try
            {
                _sonosFavoritesWindow.Activate();
            }
            catch (InvalidOperationException)
            {
            }

            return;
        }

        // GRANICA FOKUSU NA WEJSCIU, PRZED JAKIMKOLWIEK GET: niewidoczne albo
        // nieaktywne okno glowne, cudzy widoczny modal AMC albo wyjscie z sesji
        // odmawiaja ZANIM cokolwiek wyjdzie w siec.
        if (!CanPresentSonosFavorites())
        {
            Announce("Ulubione Sonos nie zostały otwarte, bo okno AMC nie jest aktywne. "
                + "Wróć do AMC i ponów Pokaż ulubione");
            return;
        }

        var backend = EnsureSonosBackend();
        // OPCJONALNA granica: zaplecze, ktore nie umie ulubionych, mowi to
        // uczciwie i NIE udaje pustej listy.
        if (backend is not ISonosFavoritesSessionBackend favoritesBackend)
        {
            Announce("To zaplecze Sonos nie udostępnia odczytu ulubionych");
            return;
        }

        ApplySonosAccountBinding();

        // DOM bierzemy z WYBRANEGO domu sesji. NIGDY z identyfikatora grupy i
        // NIGDY zgadniety: aktywna grupa nie jest tu w ogole wymagana.
        var householdId = _state.Sonos.SelectedHouseholdId;
        if (string.IsNullOrWhiteSpace(householdId))
        {
            // UCZCIWE wyjasnienie + ISTNIEJACA droga odzyskania. Jeden dom wybiera
            // sie sam istniejaca regula przy wejsciu do sesji; tutaj nie
            // wybieramy za uzytkownika.
            Announce("Nie wiadomo, z którego domu Sonos czytać ulubione. "
                + "Użyj polecenia Wybierz dom Sonos, a jeśli nie ma konta, "
                + "otwórz połączenie z Sonos skrótem Control F5");
            return;
        }

        var gate = ++_sonosFavoritesGateTicket;
        _sonosFavoritesInFlight = true;
        var ticket = _sonosTargetTicket;
        var token = EnsureSonosCancellation().Token;
        Announce(SonosFavoritesLabels.Loading);
        try
        {
            SonosFavoritesReadsStartedForTests++;
            var favorites = await favoritesBackend
                .ReadFavoritesAsync(householdId, token).ConfigureAwait(true);

            // GRANICE PO AWAIT: zmiana konta, wyjscie z sesji, zamkniecie okna,
            // zmiana celu/domu albo cudzy przelot koncza TEN przelot. ZADNE z
            // nich nie pokazuje spoznionego okna ze stara lista.
            if (ApplySonosAccountBinding()) return;
            if (ticket != _sonosTargetTicket || _isClosing || gate != _sonosFavoritesGateTicket) return;
            if (!IsSonosSession(_sessions?.Current.Id)) return;
            // DOM MUSI byc ten sam, o ktory pytalismy: lista domu A nie ma prawa
            // pokazac sie pod nazwa domu B.
            if (!string.Equals(_state.Sonos.SelectedHouseholdId, householdId, StringComparison.Ordinal)) return;

            if (!favorites.Succeeded || favorites.Favorites is null)
            {
                // ODMOWA, LIMIT, TIMEOUT, BLAD i PORZUCENIE: STALY komunikat ze
                // wspolnego zestawu. NIGDY nie udajemy swiezej pustej listy i
                // NIGDY nie otwieramy okna z pustka.
                Announce(favorites.Message);
                return;
            }

            var items = favorites.Favorites.Items;

            // GRANICA FOKUSU PONOWNIE, PRZED UTWORZENIEM okna: przez czas odczytu
            // uzytkownik mogl przejsc do innego okna albo otworzyc inny modal AMC.
            // Odmowa NIE MOZE podniesc licznika utworzonych okien.
            if (!CanPresentSonosFavorites())
            {
                Announce("Ulubione Sonos nie zostały otwarte, bo okno AMC nie jest aktywne. "
                    + "Wróć do AMC i ponów Pokaż ulubione");
                return;
            }

            // PUSTA lista to POPRAWNY wynik: okno otwiera sie z dostepnym pustym
            // stanem, bez bledu i bez udawanej pozycji.
            Announce(SonosFavoritesLabels.SummarizeCount(items.Count));

            // F3c: URUCHAMIANIE podajemy TYLKO wtedy, gdy zaplecze ma OPCJONALNA
            // granice ladowania. Brak granicy to uczciwy podglad, nie awaria.
            //
            // CEL jest CAPTUROWANY TERAZ, z niezmiennych danych: dom, o ktory
            // pytalismy, aktywna grupa i BILET celu. Dzieki temu stara lista A
            // nie wysle identyfikatora przez konto B ani do grupy B.
            var loadBackend = backend as ISonosFavoriteLoadSessionBackend;
            var group = SonosActiveGroup;
            var window = loadBackend is null
                ? new SonosFavoritesWindow(items)
                : new SonosFavoritesWindow(
                    items,
                    group?.Name,
                    favorite => LoadSonosFavoriteAsync(
                        loadBackend, householdId, group?.Id, ticket, favorite));
            SonosFavoritesWindowsCreatedForTests++;
            _sonosFavoritesWindow = window;
            try
            {
                PresentSonosFavorites(window);
            }
            finally
            {
                _sonosFavoritesWindow = null;
            }

            // BEZPIECZNY POWROT: tylko gdy okno glowne nadal jest aktywne i
            // kontekst sie nie zmienil. Nie kradniemy fokusu obcemu oknu.
            if (_isClosing || !IsActive) return;
            if (ticket != _sonosTargetTicket || gate != _sonosFavoritesGateTicket) return;
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
            if (gate == _sonosFavoritesGateTicket) _sonosFavoritesInFlight = false;
        }
    }

    /// <summary>
    /// Czy WOLNO pokazac podglad ulubionych TERAZ. Ten sam wzorzec, co brama
    /// wyboru domu: okno widoczne i AKTYWNE, nie zamykane, w sesji Sonos i bez
    /// innego WIDOCZNEGO okna potomnego. Guard jest PRODUKCYJNY - punkt
    /// podstawienia pokazania siedzi ZA nim, nie przed nim.
    /// </summary>
    private bool CanPresentSonosFavorites()
    {
        if (_isClosing) return false;
        if (!IsVisible || !IsActive || !IsEnabled) return false;
        if (!IsSonosSession(_sessions?.Current.Id)) return false;
        return !OwnedWindows.OfType<Window>().Any(window => window.IsVisible);
    }

    /// <summary>Ile razy okno ulubionych POPROSILO o uruchomienie. Odmowa liczy sie tutaj.</summary>
    internal int SonosFavoriteLoadRequestsForTests { get; private set; }

    /// <summary>Ile razy uruchomienie DOSZLO do zaplecza (czyli do POST).</summary>
    internal int SonosFavoriteLoadsSentForTests { get; private set; }

    /// <summary>
    /// JAWNA akcja "Odtwórz"/Enter z okna ulubionych: DOKLADNIE JEDEN POST
    /// loadFavorite dla WSKAZANEGO ulubionego w AKTYWNEJ grupie.
    ///
    /// Duch istniejacego <c>ExecuteSonosCommandAsync</c> zachowany co do joty:
    /// ta SAMA bramka jednego polecenia (<c>_sonosCommandInFlight</c>), ten SAM
    /// wlasny bilet bramki (<c>_sonosCommandGateTicket</c>) i ta SAMA droga
    /// zwolnienia (<c>ReleaseSonosCommandGate</c>) - zadnej drugiej, sprzecznej
    /// kolejki polecen.
    ///
    /// AKCJA KOLEJKI JEST JAWNA i wynika z oficjalnej dokumentacji Sonosa
    /// (queue-action): INSERT to jedyna wartosc, o ktorej dokumentacja mowi
    /// wprost, ze "Sonos moves the playback head to the first enqueued content".
    /// APPEND samo GLOWICY NIE PRZENOSI, REPLACE KASUJE kolejke uzytkownika,
    /// a PLAY_NOW wystepuje w definicji OpenAPI BEZ opisu - wiec go nie
    /// zgadujemy. <c>playOnCompletion: true</c> dopelnia zamiar "zagraj teraz".
    /// To kontrakt ZAMIARU, nie pomiar fizyczny.
    ///
    /// GRANICE sprawdzamy PRZED POST i PONOWNIE po KAZDYM await: konto, dom,
    /// grupa, sesja, bilet celu, zamykanie i stan modala. Spozniony wynik NIE
    /// mowi w cudzym widoku i nie rusza cudzego fokusu.
    /// </summary>
    private async Task LoadSonosFavoriteAsync(
        ISonosFavoriteLoadSessionBackend backend,
        string householdId,
        string? groupId,
        int targetTicket,
        SonosFavorite favorite)
    {
        ArgumentNullException.ThrowIfNull(backend);
        ArgumentNullException.ThrowIfNull(favorite);
        SonosFavoriteLoadRequestsForTests++;

        // GRANICA konta PRZED czymkolwiek: po RZECZYWISTEJ zmianie konta stary
        // identyfikator ulubionego nie ma prawa pojsc przez NOWE konto.
        if (ApplySonosAccountBinding())
        {
            AnnounceInSonosFavorites(SonosAccountChangedInstruction);
            return;
        }

        // CEL musi byc TEN SAM, ktory okno dostalo przy otwarciu: ten sam bilet,
        // ten sam dom, ta sama grupa, ta sama sesja. Zmiana czegokolwiek konczy
        // droge BEZ POST - nie przekierowujemy materialu do innej grupy.
        if (_isClosing
            || targetTicket != _sonosTargetTicket
            || !IsSonosSession(_sessions?.Current.Id)
            || !string.Equals(_state.Sonos.SelectedHouseholdId, householdId, StringComparison.Ordinal))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(groupId)
            || SonosActiveGroup is not { } group
            || !string.Equals(group.Id, groupId, StringComparison.Ordinal))
        {
            // BRAK grupy albo ZMIENIONY cel: uczciwe wyjasnienie i ISTNIEJACA
            // droga odzyskania. ZERO POST.
            AnnounceInSonosFavorites(SonosFavoritesLabels.PlayNeedsGroup);
            return;
        }

        // TA SAMA bramka jednego polecenia Sonos, co reszta sterowania: jawna
        // odmowa zamiast cichej kolejki i zamiast drugiego POST.
        if (_sonosCommandInFlight)
        {
            AnnounceInSonosFavorites(SonosFavoritesLabels.PlayAlreadyInFlight);
            return;
        }

        var gateTicket = ++_sonosCommandGateTicket;
        var token = EnsureSonosCancellation().Token;
        _sonosCommandInFlight = true;
        try
        {
            SonosFavoriteLoadsSentForTests++;
            // JEDEN POST. Bez presetu, bez zapisu, bez drugiego Play/Toggle.
            var result = await backend.LoadFavoriteAsync(
                groupId,
                favorite.Id,
                // JAWNY INSERT: dokumentacja queue-action mowi wprost, ze INSERT
                // przenosi glowice na pierwsza wstawiona pozycje.
                SonosFavoriteQueueAction.Insert,
                playOnCompletion: true,
                token).ConfigureAwait(true);

            // GRANICE PO AWAIT: te same co przed. Spozniony wynik nie mowi w
            // cudzym widoku i nie przypisuje sie do innego celu.
            if (ApplySonosAccountBinding()) return;
            if (_isClosing || targetTicket != _sonosTargetTicket) return;
            if (!IsSonosSession(_sessions?.Current.Id)) return;

            var accepted = result.Status == SonosGroupOperationStatus.Attempted
                && result.Outcome?.Status == SonosControlApiStatus.Success;
            // HTTP 200 to PRZYJECIE ZLECENIA, nie dowod, ze muzyka gra.
            // Tozsamosc pozycji bierzemy z NASZEJ listy - to my wyslalismy ten
            // identyfikator. Tytul z metadanych NIE jest dowodem tozsamosci.
            AnnounceInSonosFavorites(accepted
                ? SonosFavoritesLabels.DescribePlayAccepted(SonosFavoritesLabels.Describe(favorite))
                : result.Message);

            // ISTNIEJACY jawny odczyt stanu tego SAMEGO celu - zeby odtwarzacz i
            // bramki polecen nie zostaly ze starym stanem. Bez drugiego POST i
            // bez nowego pollingu. Wyjatek odczytu NIE moze udawac, ze proba
            // uruchomienia sie nie odbyla.
            try
            {
                await ReadSonosGroupStateAsync().ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                // Tresc wyjatku (adres, naglowek autoryzacji, identyfikatory) NIE
                // idzie do mowy ani do statusu.
                LogSonosFavoriteLoadFailure("odczyt po uruchomieniu", exception);
            }
        }
        catch (OperationCanceledException)
        {
            // Wlasne zamykanie albo zmiana celu: CISZA. PORZUCONY POST mogl sie
            // mimo wszystko wykonac - nie obiecujemy cofniecia.
        }
        catch (Exception exception)
        {
            LogSonosFavoriteLoadFailure("wysłanie uruchomienia", exception);
            if (_isClosing || targetTicket != _sonosTargetTicket) return;
            // UCZCIWIE, BEZ surowego wyjatku, identyfikatora i tokenu.
            AnnounceInSonosFavorites(
                "Nie udało się wykonać polecenia uruchomienia ulubionego. Spróbuj ponownie.");
        }
        finally
        {
            // SPOZNIONY przelot A nie uwalnia trwajacego B: bramke zwalnia tylko
            // jej WLASCICIEL - ta SAMA produkcyjna droga co reszta polecen.
            ReleaseSonosCommandGate(gateTicket);
        }
    }

    /// <summary>
    /// KOMUNIKAT dla uzytkownika stojacego w oknie ulubionych: gdy okno jest
    /// otwarte, mowi ONO (wlasny dostepny status), a nie okno glowne za modalem.
    /// Po zamknieciu wraca ISTNIEJACY mechanizm okna glownego.
    /// </summary>
    private void AnnounceInSonosFavorites(string message)
    {
        if (_sonosFavoritesWindow is { } window && window.IsVisible)
        {
            window.AnnounceForOwner(message);
            return;
        }

        Announce(message);
    }

    /// <summary>
    /// LOG diagnostyczny bez sekretu: RODZAJ wyjatku, zero tresci, zero adresu,
    /// zero identyfikatorow.
    /// </summary>
    private static void LogSonosFavoriteLoadFailure(string stage, Exception exception) =>
        System.Diagnostics.Debug.WriteLine(
            $"Sonos: uruchomienie ulubionego - {stage} nie udalo sie ({exception.GetType().Name}).");

    /// <summary>WASKI hook pomiarowy: PRODUKCYJNA droga uruchomienia z okna.</summary>
    internal Task LoadSonosFavoriteForTests(
        ISonosFavoriteLoadSessionBackend backend,
        string householdId,
        string? groupId,
        int targetTicket,
        SonosFavorite favorite) =>
        LoadSonosFavoriteAsync(backend, householdId, groupId, targetTicket, favorite);


    /// <summary>
    /// POKAZANIE okna. WLASCICIEL jest WYMAGANY - modal bez wlasciciela moze
    /// zostac za AMC. Wiazemy go PRZED punktem podstawienia, zeby pomiar mierzyl
    /// TO SAMO powiazanie co droga produkcyjna.
    /// </summary>
    private void PresentSonosFavorites(SonosFavoritesWindow window)
    {
        window.Owner = this;

        if (PresentSonosFavoritesOverrideForTests is { } present)
        {
            present(window);
            return;
        }

        window.ShowDialog();
    }
}
