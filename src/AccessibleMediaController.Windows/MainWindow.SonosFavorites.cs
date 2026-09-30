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
            var window = new SonosFavoritesWindow(items);
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
