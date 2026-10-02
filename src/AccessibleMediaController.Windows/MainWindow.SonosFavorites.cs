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

    /// <summary>
    /// ODMOWA PRZED WYSLANIEM, gdy konto zostalo RZECZYWISCIE zmienione: ZERO
    /// POST, wiec mowimy WPROST, ze polecenia NIE WYSLANO - zadnego "odrzucono"
    /// i zadnego "cofnieto".
    ///
    /// DROGA POWROTU JEST ZMIERZONA FIZYCZNIE (parent 905, after-recovery-ctrlL.txt,
    /// after-session-reentry.txt, recovery-choose-household.txt). Po rzeczywistej
    /// zmianie konta lista glowna jest PUSTA, a "wybierz grupę na nowo" nie mialo
    /// czego wybrac:
    ///  * Ctrl+L nie przywraca grup,
    ///  * wyjscie i powrot do sesji Sonos przy DWOCH domach daje nowy odczyt domu,
    ///    ale ZERO grup,
    ///  * Ctrl+F5 (Głośniki i grupy) jest PODGLADEM, nie wyborem grupy.
    /// DZIALA dopiero ISTNIEJACE polecenie "Wybierz dom Sonos" - i dopiero po nim
    /// lista grup wraca, wiec grupe wybiera sie PO domu. Nie dodajemy tu nowej
    /// funkcji odswiezania: wskazujemy akcje, ktora w aplikacji JEST.
    /// </summary>
    internal const string PlayNotSentAccountChanged =
        "Polecenie uruchomienia ulubionego nie zostało wysłane, bo konto Sonos się zmieniło. "
        + "Zamknij Ulubione, użyj polecenia Wybierz dom Sonos z menu Plik albo z palety poleceń "
        + "Control Shift K, wskaż dom, potem grupę, i otwórz listę jeszcze raz";

    /// <summary>
    /// ODMOWA PRZED WYSLANIEM, gdy CEL (bilet, sesja albo dom) przestal byc ten
    /// sam, ktory okno dostalo przy otwarciu: ZERO POST i jawne niewyslanie.
    /// DROGA POWROTU jak wyzej - zmierzona, nie zgadnieta.
    /// </summary>
    internal const string PlayNotSentTargetChanged =
        "Polecenie uruchomienia ulubionego nie zostało wysłane, bo cel Sonos się zmienił. "
        + "Zamknij Ulubione, użyj polecenia Wybierz dom Sonos z menu Plik albo z palety poleceń "
        + "Control Shift K, wskaż dom, potem grupę, i otwórz listę jeszcze raz";

    /// <summary>
    /// PO WYSLANIU, gdy konto albo cel zmienily sie w trakcie: PROBA BYLA
    /// (<c>RequestSent</c> to tylko proba), ale NIE MA potwierdzenia wyniku.
    /// Nie twierdzimy ani wykonania, ani odrzucenia, ani cofniecia - i niczego
    /// nie wysylamy, zeby to odkrecic.
    ///
    /// WARUNEK UZYCIA JEST SPRAWDZANY: ten komunikat idzie WYLACZNIE gdy
    /// <c>result.RequestSent</c>. Gdy zadanie NIE poszlo (np. odnowienie biletu
    /// padlo przed POST), mowimy niewyslanie stalymi <see cref="PlayNotSentAccountChanged"/>
    /// / <see cref="PlayNotSentTargetChanged"/>, bo "podjeto probe" byloby klamstwem.
    /// </summary>
    internal const string PlayAttemptedOutcomeUnknown =
        "Podjęto próbę uruchomienia ulubionego, ale nie ma potwierdzenia jej wyniku, "
        + "bo cel Sonos zmienił się w trakcie. Zamknij Ulubione i sprawdź stan grupy";

    /// <summary>
    /// PORZUCENIE JUZ PODJETEJ proby (anulowanie oczekiwania): polecenie moglo
    /// opuscic maszyne, wiec NIE obiecujemy cofniecia i NIE mowimy o sukcesie.
    /// </summary>
    internal const string PlayAbandonedOutcomeUnknown =
        "Uruchamianie ulubionego zostało przerwane. Polecenie mogło już pójść do Sonosa, "
        + "więc nie ma potwierdzenia wyniku i nie obiecuję cofnięcia; sprawdź stan grupy";

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
                    request => LoadSonosFavoriteAsync(
                        loadBackend, householdId, group?.Id, ticket, request),
                    // PRZYPISANIE: okno oddaje ZAZNACZONY ulubiony, a my - jako
                    // ZYWY wlasciciel - pokazujemy dialog miejsca i zapisujemy.
                    favorite => AssignSonosFavoritePreset(
                        favorite, _sonosFavoritesWindow!, householdId, ticket));
            SonosFavoritesWindowsCreatedForTests++;
            _sonosFavoritesWindow = window;
            // POWROT Z INNEJ SESJI: wiersz sprzed Ctrl+cyfra. Przy zwyklym
            // otwarciu pole jest puste i lista zostaje na pierwszym wierszu.
            if (ConsumeSonosSublistPendingRowId(SonosLibraryPresentation.FavoritesCategoryId)
                is { } pendingFavorite)
            {
                window.Loaded += (_, _) => window.RestoreSelectedRow(pendingFavorite);
            }
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
    /// ZYCIE ZLECENIA JEST PRZYWIAZANE DO MODALU, KTORY JE ZLECIL. Zlecenie
    /// niesie NIEZMIENNA tozsamosc tej instancji i jej TOKEN ZYCIA:
    ///  * token lokalny jest POWIAZANY z tokenem sesji, wiec zamkniecie modalu
    ///    anuluje WYLACZNIE swoje oczekiwanie - nie cala sesje Sonos, nie konto i
    ///    nie polecenie, ktore JUZ poszlo (cofniecia nie obiecujemy),
    ///  * granice tozsamosci sprawdzamy PRZED dzialaniem i PONOWNIE po KAZDYM
    ///    await, PRZED mowa i PRZED odczytem,
    ///  * status idzie DO TEJ instancji albo NIGDZIE. Zadnego zapasowego
    ///    ogloszenia w oknie glownym i zadnego wejscia w nowo otwarty modal:
    ///    porzucona proba nie ma prawa odezwac sie w cudzym widoku.
    ///
    /// Sam token nie wystarczy: zaplecze moze SWIADOMIE zignorowac anulowanie i
    /// oddac spozniony sukces, dlatego rozstrzyga granica tozsamosci, a token
    /// jest uprzejmoscia wobec klienta, ktory go honoruje.
    /// </summary>
    private async Task LoadSonosFavoriteAsync(
        ISonosFavoriteLoadSessionBackend backend,
        string householdId,
        string? groupId,
        int targetTicket,
        SonosFavoritesWindow.PlayRequest request)
    {
        ArgumentNullException.ThrowIfNull(backend);
        ArgumentNullException.ThrowIfNull(request);
        var origin = request.Origin;
        var favorite = request.Favorite;
        SonosFavoriteLoadRequestsForTests++;

        // ZLECAJACY MUSI ZYC JUZ TERAZ: zamkniety modal nie dostaje odpowiedzi,
        // a my nie szukamy zastepczego adresata.
        if (!IsLiveFavoritesOrigin(origin)) return;
        NextSonosPlaybackIntent();

        // GRANICA konta PRZED czymkolwiek: po RZECZYWISTEJ zmianie konta stary
        // identyfikator ulubionego nie ma prawa pojsc przez NOWE konto.
        if (ApplySonosAccountBinding())
        {
            // ZERO POST: mowimy WPROST, ze nie wyslano, i dopiero potem droge
            // odzyskania. Sam SonosAccountChangedInstruction tego nie mowil, a
            // zywy modal ma prawo wiedziec, ze jego Enter nic nie wyslal.
            AnnounceInFavoritesOrigin(origin, PlayNotSentAccountChanged);
            return;
        }

        // CEL musi byc TEN SAM, ktory okno dostalo przy otwarciu: ten sam bilet,
        // ten sam dom, ta sama grupa, ta sama sesja. Zmiana czegokolwiek konczy
        // droge BEZ POST - nie przekierowujemy materialu do innej grupy.
        //
        // ZAMKNIETE okno jest CICHE: _isClosing to nasze wlasne zamykanie AMC,
        // wiec nie ma komu i po co mowic. Pozostale zmiany celu dotycza ZYWEGO
        // modalu, ktory wlasnie nacisnal Odtworz - i on MUSI dostac koniec, bo
        // inaczej zostaje na "Wysyłam polecenie uruchomienia. Czekaj." na zawsze.
        if (_isClosing) return;
        if (targetTicket != _sonosTargetTicket
            || !IsSonosSession(_sessions?.Current.Id)
            || !string.Equals(_state.Sonos.SelectedHouseholdId, householdId, StringComparison.Ordinal))
        {
            AnnounceInFavoritesOrigin(origin, PlayNotSentTargetChanged);
            return;
        }

        if (string.IsNullOrWhiteSpace(groupId)
            || SonosActiveGroup is not { } group
            || !string.Equals(group.Id, groupId, StringComparison.Ordinal))
        {
            // BRAK grupy albo ZMIENIONY cel: uczciwe wyjasnienie i ISTNIEJACA
            // droga odzyskania. ZERO POST.
            AnnounceInFavoritesOrigin(origin, SonosFavoritesLabels.PlayNeedsGroup);
            return;
        }

        // TA SAMA bramka jednego polecenia Sonos, co reszta sterowania: jawna
        // odmowa zamiast cichej kolejki i zamiast drugiego POST.
        if (_sonosCommandInFlight)
        {
            AnnounceInFavoritesOrigin(origin, SonosFavoritesLabels.PlayAlreadyInFlight);
            return;
        }

        var gateTicket = ++_sonosCommandGateTicket;
        // TOKEN LOKALNY POWIAZANY Z TOKENEM SESJI: anuluje go albo zamkniecie
        // TEGO modalu, albo istniejace porzucenie pracy sesji. Zamkniecie modalu
        // NIE wola CancelSonosPendingWork i nie rusza cudzych oczekiwan.
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(
            EnsureSonosCancellation().Token, request.Lifetime);
        var token = lifetime.Token;
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

            // GRANICE PO AWAIT: te same co przed, a NAJPIERW tozsamosc zlecajacego.
            // Spozniony wynik nie mowi w cudzym widoku, nie rusza cudzego fokusu i
            // nie odczytuje stanu dla okna, ktorego juz nie ma.
            //
            // ROZNICA WOBEC PRE-POST: tutaj await JUZ SIE SKONCZYL, ale to NIE
            // znaczy, ze POST poszedl. Prawdziwy producent (SonosAccountCoordinator
            // .GroupOperations RunGroupCommandAsync) przy ZNANEJ MINIONEJ waznosci
            // biletu czeka na RenewForReadAsync PRZED wyslaniem; gdy konto zmieni
            // sie w trakcie TEGO odnowienia, CommandFromRenewal oddaje ZWYKLYM
            // returnem wynik z RequestSent=false - i "zadnego POST nie bylo".
            // Taki przelot NIE rzuca OperationCanceledException, wiec wpada tutaj.
            //
            // Dlatego o "probie" mowimy WYLACZNIE gdy result.RequestSent. Przy
            // RequestSent=false uzywamy tych samych stalych niewyslania co przed
            // POST - ta sama regula, ktora projekt trzyma juz w MainWindow.Sonos.cs
            // (RequestSent=false to ZERO prob wyslania, wiec "wyslano" byloby
            // klamstwem). ZYWY zlecajacy modal w KAZDEJ galezi dostaje koniec:
            // zostawienie go na "Czekaj" to L1.
            if (!IsLiveFavoritesOrigin(origin)) return;
            if (_isClosing) return;
            var accountChanged = ApplySonosAccountBinding();
            if (accountChanged
                || targetTicket != _sonosTargetTicket
                || !IsSonosSession(_sessions?.Current.Id))
            {
                AnnounceInFavoritesOrigin(origin, result.RequestSent
                    ? PlayAttemptedOutcomeUnknown
                    : accountChanged
                        ? PlayNotSentAccountChanged
                        : PlayNotSentTargetChanged);
                return;
            }

            var accepted = result.Status == SonosGroupOperationStatus.Attempted
                && result.Outcome?.Status == SonosControlApiStatus.Success;
            // HTTP 200 to PRZYJECIE ZLECENIA, nie dowod, ze muzyka gra.
            // Tozsamosc pozycji bierzemy z NASZEJ listy - to my wyslalismy ten
            // identyfikator. Tytul z metadanych NIE jest dowodem tozsamosci.
            AnnounceInFavoritesOrigin(origin, accepted
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
            // Zamkniecie TEGO modalu albo wlasne zamykanie AMC: CISZA - nie ma
            // komu odpowiedziec i nie szukamy zastepczego adresata.
            // PORZUCONY POST mogl sie mimo wszystko wykonac - nie obiecujemy
            // cofniecia i nie wysylamy niczego, zeby to odkrecic.
            //
            // ZYWY zlecajacy to jednak INNY przypadek: anulowanie przyszlo z
            // porzucenia pracy sesji (np. rzeczywista zmiana konta), a modal
            // stoi otwarty na "Czekaj". On MUSI dostac uczciwy koniec.
            if (_isClosing) return;
            AnnounceInFavoritesOrigin(origin, PlayAbandonedOutcomeUnknown);
        }
        catch (Exception exception)
        {
            LogSonosFavoriteLoadFailure("wysłanie uruchomienia", exception);
            if (!IsLiveFavoritesOrigin(origin)) return;
            if (_isClosing || targetTicket != _sonosTargetTicket) return;
            // UCZCIWIE, BEZ surowego wyjatku, identyfikatora i tokenu.
            AnnounceInFavoritesOrigin(origin,
                "Nie udało się wykonać polecenia uruchomienia ulubionego. Spróbuj ponownie.");
        }
        finally
        {
            // SPOZNIONY przelot A nie uwalnia trwajacego B: bramke zwalnia tylko
            // jej WLASCICIEL - ta SAMA produkcyjna droga co reszta polecen. Idzie
            // to BEZWARUNKOWO, takze po zamknieciu modalu, zeby zamkniecie i
            // ponowne otwarcie nie zostawilo bramki zajetej na zawsze.
            ReleaseSonosCommandGate(gateTicket);
        }
    }

    /// <summary>
    /// Czy ZLECAJACA instancja modalu nadal jest ZYWYM adresatem. Kryterium jest
    /// TOZSAMOSC, nie "jakikolwiek otwarty modal": okno B nie jest nastepca okna
    /// A, a okno glowne nie jest jego zapasowym glosnikiem.
    /// </summary>
    private bool IsLiveFavoritesOrigin(SonosFavoritesWindow origin) =>
        ReferenceEquals(_sonosFavoritesWindow, origin) && origin.IsLiveOwnerTarget;

    /// <summary>
    /// STATUS DO TEJ INSTANCJI albo NIGDZIE. Gdy zlecajacy modal nie zyje, wynik
    /// jest CICHY: nie przenosi sie do okna glownego i nie wchodzi w nowy modal.
    /// </summary>
    private void AnnounceInFavoritesOrigin(SonosFavoritesWindow origin, string message)
    {
        if (!IsLiveFavoritesOrigin(origin)) return;
        origin.AnnounceForOwner(message);
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
        SonosFavoritesWindow.PlayRequest request) =>
        LoadSonosFavoriteAsync(backend, householdId, groupId, targetTicket, request);


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
