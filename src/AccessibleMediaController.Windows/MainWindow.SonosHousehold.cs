using System.Windows;
using AccessibleMediaController.Core.Sonos;

namespace AccessibleMediaController.Windows;

/// <summary>
/// B2c2: DOSTEPNY WYBOR DOMU Sonos. Trzy niezalezne obietnice:
///
/// 1) SWIEZOSC: lista domow pochodzi z ISTNIEJACEGO zaplecza sesji
///    (<c>ReadHouseholdsAsync</c> na tym samym wlascicielu konta) - zadnego
///    wlasnego HTTP, zadnego cache i zadnego drugiego magazynu.
/// 2) ANULOWANIE JEST BEZSKUTKOWE: samo pokazanie okna i ruch zaznaczeniem nie
///    dotyka ani stanu, ani pliku. Mutacja zdarza sie WYLACZNIE po
///    POTWIERDZENIU, i tylko dla identyfikatora ze swiezej listy.
/// 3) PRZELACZENIE CELU JEST BEZPIECZNE: przed odczytem grup nowego domu
///    uniewazniamy loty starego celu i czyscimy JEGO dane oraz WIERSZE, wiec
///    spozniony GET domu A nie ma czego nadpisac, a lista nie pokazuje grup A
///    pod nazwa domu B. Zaden POST nie idzie - muzyki nie zatrzymujemy.
/// </summary>
public partial class MainWindow
{
    /// <summary>
    /// Bramka WYBORU DOMU. Wlasna, bo wybor domu i odswiezanie grup to dwie
    /// rozne drogi uzytkownika; wspolna bramka blokowalaby jedna przez druga.
    /// </summary>
    private bool _sonosHouseholdChoiceInFlight;

    /// <summary>
    /// Wlasciciel bramki wyboru. TA SAMA regula co przy odswiezaniu:
    /// <c>CancelSonosPendingWork</c> podnosi bilet, wiec porzucony przelot A
    /// widzi w finally CUDZY bilet i NIE odblokuje trwajacego B.
    /// </summary>
    private int _sonosHouseholdChoiceGateTicket;

    /// <summary>Otwarte okno wyboru domu albo null. Jedno naraz.</summary>
    private SonosHouseholdSelectionWindow? _sonosHouseholdWindow;

    internal bool SonosHouseholdChoiceInFlightForTests => _sonosHouseholdChoiceInFlight;

    internal SonosHouseholdSelectionWindow? OpenSonosHouseholdWindowForTests => _sonosHouseholdWindow;

    /// <summary>Ile okien wyboru POWSTALO. Guard nie ma prawa tego podniesc.</summary>
    internal int SonosHouseholdWindowsCreatedForTests { get; private set; }

    /// <summary>
    /// TESTOWY punkt podstawienia POKAZANIA okna. Produkcyjnie null, czyli
    /// prawdziwe modalne <c>ShowDialog</c> na oknie glownym.
    /// </summary>
    internal Action<SonosHouseholdSelectionWindow>? PresentSonosHouseholdOverrideForTests { get; set; }

    internal Task ChooseSonosHouseholdForTests() => ChooseSonosHouseholdAsync();

    internal Task? LastSonosHouseholdChoiceTaskForTests { get; private set; }
    /// <summary>
    /// JAWNE polecenie "Wybierz dom Sonos".
    ///
    /// Kolejnosc jest istotna: TEN SAM warunek prezentacji
    /// (<c>CanPresentSonosHouseholdChoice</c>) na WEJSCIU, zeby niewidoczny lub
    /// nieaktywny wlasciciel nie wyslal w ogole zadnego GET; potem granica konta
    /// PRZED wzieciem biletu bramki (bo <c>ApplySonosAccountBinding</c> sam wola
    /// <c>CancelSonosPendingWork</c>, ktory bilet PODNOSI), potem swiezy odczyt,
    /// potem TEN SAM warunek PONOWNIE po await i PRZED Show (bo w czasie odczytu
    /// fokus mogl odejsc), potem dopiero zapis i odczyt grup.
    /// </summary>
    internal async Task ChooseSonosHouseholdAsync()
    {
        if (_sonosHouseholdChoiceInFlight)
        {
            // SWIADOMA powtorka: krotka informacja, ZERO dodatkowych GET.
            Announce("Wybieranie domu Sonos już trwa");
            return;
        }

        if (_sonosHouseholdWindow is not null)
        {
            // Okno JUZ jest otwarte: nie mnozymy modali nad modalem.
            Announce("Okno wyboru domu Sonos jest już otwarte");
            try
            {
                _sonosHouseholdWindow.Activate();
            }
            catch (InvalidOperationException)
            {
            }

            return;
        }

        // GRANICA FOKUSU NA WEJSCIU, PRZED JAKIMKOLWIEK GET: dokladnie ten sam
        // warunek co przed pokazaniem okna. Bez tego polecenie wywolane przy
        // niewidocznym/nieaktywnym oknie glownym albo nad innym widocznym modalem
        // AMC i tak ruszalo siec (EnsureSonosBackend + ReadHouseholdsAsync), a
        // odmawialo dopiero po odpowiedzi. Tutaj odmawiamy ZANIM cokolwiek
        // wyjdzie: zero GET, zero biletu bramki, zero przejmowania fokusu.
        if (!CanPresentSonosHouseholdChoice())
        {
            Announce("Wybór domu Sonos nie został otwarty, bo okno AMC nie jest aktywne. "
                + "Wróć do AMC i ponów wybór domu");
            return;
        }

        var backend = EnsureSonosBackend();
        ApplySonosAccountBinding();
        var gate = ++_sonosHouseholdChoiceGateTicket;
        _sonosHouseholdChoiceInFlight = true;
        var ticket = _sonosTargetTicket;
        var token = EnsureSonosCancellation().Token;
        // JAWNY komunikat ladowania: uzytkownik wie, ze czekamy na siec.
        Announce("Czytam domy Sonos");
        try
        {
            var households = await backend.ReadHouseholdsAsync(token).ConfigureAwait(true);
            // GRANICE PO AWAIT: zmiana konta, wyjscie z sesji, zamkniecie okna
            // albo cudzy przelot - kazde z nich konczy TEN przelot i ZADNE nie
            // pokazuje spoznionego okna ze stara lista.
            if (ApplySonosAccountBinding()) return;
            if (ticket != _sonosTargetTicket || _isClosing || gate != _sonosHouseholdChoiceGateTicket) return;
            if (!IsSonosSession(_sessions?.Current.Id)) return;

            if (!households.Succeeded || households.Households is null)
            {
                // BRAK SWIEZOSCI, nie "brak domow": nie otwieramy okna z pustka
                // ani ze stara lista, bo wybor musi byc z AKTUALNYCH danych.
                Announce(households.Status == SonosDeviceReadStatus.NoAccount
                    ? "Nie można wybrać domu Sonos: brak podłączonego konta"
                    : "Nie udało się odczytać domów Sonos. Wybór domu nie został otwarty");
                return;
            }

            var fresh = households.Households;
            if (fresh.Count == 0)
            {
                // NoHouseholds to NIE NoAccount: konto jest, domow nie ma.
                _sonosHouseholds = fresh;
                Announce("Konto Sonos nie udostępnia żadnego domu, więc nie ma z czego wybierać");
                return;
            }

            // SWIEZA lista staje sie ta, ktora zna sesja; okno dostaje DOKLADNIE ja.
            _sonosHouseholds = fresh;

            // GRANICA FOKUSU, PONOWNIE PO AWAIT i PRZED UTWORZENIEM okna: przez
            // czas odczytu uzytkownik mogl przejsc do innego okna albo otworzyc
            // inny modal AMC. Spozniony wynik NIE ma prawa wtedy ukrasc fokusu,
            // a odmowa NIE MOZE podniesc licznika utworzonych okien.
            if (!CanPresentSonosHouseholdChoice())
            {
                Announce("Wybór domu Sonos nie został otwarty, bo okno AMC nie jest aktywne. "
                    + "Wróć do AMC i ponów wybór domu");
                return;
            }

            var currentId = _state.Sonos.SelectedHouseholdId;
            var window = new SonosHouseholdSelectionWindow(fresh, currentId);
            SonosHouseholdWindowsCreatedForTests++;
            _sonosHouseholdWindow = window;
            try
            {
                PresentSonosHouseholdChoice(window);
            }
            finally
            {
                _sonosHouseholdWindow = null;
            }

            // GRANICE PO MODALU: podczas otwartego okna konto albo sesja mogly
            // sie zmienic. Wynik starego domu NIE moze wtedy nic zapisac.
            if (ApplySonosAccountBinding()) return;
            if (ticket != _sonosTargetTicket || _isClosing || gate != _sonosHouseholdChoiceGateTicket) return;
            if (!IsSonosSession(_sessions?.Current.Id)) return;

            if (!window.Confirmed || window.SelectedHouseholdId is not { } chosenId)
            {
                // ANULOWANIE: zero mutacji, zero zapisu, zero POST.
                Announce("Anulowano wybór domu Sonos. Nic nie zmieniono");
                RestoreFocusAfterSonosHouseholdChoice();
                return;
            }

            // POTWIERDZENIE: identyfikator MUSI pochodzic z tej SWIEZEJ listy
            // (i tym samym z tego samego konta), inaczej nie jest wyborem.
            var chosen = SonosActiveGroupPolicy.ResolveHousehold(chosenId, fresh);
            if (chosen is null)
            {
                Announce("Wybrany dom Sonos nie jest na aktualnej liście. Nic nie zmieniono");
                RestoreFocusAfterSonosHouseholdChoice();
                return;
            }

            if (string.Equals(chosen.Id, currentId, StringComparison.Ordinal))
            {
                // TEN SAM dom: cel, aktywna grupa i metadane ZOSTAJA. Zadnego
                // restartu celu, zadnego czyszczenia, zadnego POST.
                Announce("Dom Sonos bez zmian: " + SonosDeviceLabels.DescribeHousehold(chosen, 1));
                RestoreFocusAfterSonosHouseholdChoice();
                return;
            }

            await SwitchSonosHouseholdAsync(backend, chosen, ticket, gate).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (gate == _sonosHouseholdChoiceGateTicket) _sonosHouseholdChoiceInFlight = false;
        }
    }

    /// <summary>
    /// PRZELACZENIE CELU na INNY dom. Kolejnosc jest cala trescia bezpieczenstwa:
    /// najpierw uniewazniamy loty i dane starego domu ORAZ WIERSZE, zapisujemy
    /// swiadomy wybor, i tylko potem czytamy grupy nowego domu.
    /// </summary>
    private async Task SwitchSonosHouseholdAsync(
        ISonosGroupSessionBackend backend,
        SonosHousehold chosen,
        int ticket,
        int gate)
    {
        // 1) Uniewaznienie starego celu istniejaca droga. CancelSonosPendingWork
        // podnosi _sonosTargetTicket i bilet TEJ bramki, wiec od tego miejsca
        // pracujemy na NOWYCH biletach.
        CancelSonosPendingWork();
        var switchTicket = _sonosTargetTicket;
        var switchGate = _sonosHouseholdChoiceGateTicket;
        // Bramke wyboru trzymamy dalej MY, ale to jest JUZ NOWY bilet: Cancel ja
        // zwolnil i PODNIOSL. Outer finally w ChooseSonosHouseholdAsync ma bilet
        // STARY, wiec tej bramki NIE zwolni - musimy zrobic to TUTAJ, we wszystkich
        // zakonczeniach, i tylko gdy NADAL jesteśmy jej wlascicielem.
        _sonosHouseholdChoiceInFlight = true;
        _ = ticket;
        _ = gate;
        try
        {
            await SwitchSonosHouseholdCoreAsync(backend, chosen, switchTicket, switchGate)
                .ConfigureAwait(true);
        }
        finally
        {
            if (switchGate == _sonosHouseholdChoiceGateTicket) _sonosHouseholdChoiceInFlight = false;
        }
    }

    /// <summary>
    /// TRESC przelaczenia. Wydzielona, bo bramke nowego biletu trzeba zwolnic w
    /// KAZDYM zakonczeniu tej pracy, takze przy wczesnych <c>return</c> granic.
    /// </summary>
    private async Task SwitchSonosHouseholdCoreAsync(
        ISonosGroupSessionBackend backend,
        SonosHousehold chosen,
        int switchTicket,
        int switchGate)
    {
        // 2) Stary dom przestaje istniec dla widoku: topologia, cel, dane i
        // RZECZYWISTE WIERSZE ida do zera PRZED odczytem grup nowego domu.
        _sonosTopology = null;
        ClearSonosTargetState();
        _state.Sonos.SelectedHouseholdId = chosen.Id;
        if (_playerViewActive && IsSonosSession(_sessions?.Current.Id)) ReturnFromPlayerToList();
        _sonosEmptyReason = SonosSessionEmptyReason.NotRead;
        ApplySonosGroupRows();

        // 3) SWIADOMY wybor zapisujemy ISTNIEJACA droga, z uczciwa skarga gdy
        // zapis padnie. Wybor jest juz w stanie, wiec zapis nie jest warunkiem
        // dalszego odczytu.
        QueueStateSave(announceFailure: true);
        Announce("Wybrano dom Sonos: " + SonosDeviceLabels.DescribeHousehold(chosen, 1)
            + ". Czytam grupy");

        var token = EnsureSonosCancellation().Token;
        var groups = await backend.ReadGroupsAsync(chosen.Id, token).ConfigureAwait(true);
        if (ApplySonosAccountBinding()) return;
        if (switchTicket != _sonosTargetTicket
            || _isClosing
            || switchGate != _sonosHouseholdChoiceGateTicket)
        {
            return;
        }

        if (!groups.Succeeded || groups.Topology is null)
        {
            // Wybor domu B JEST SWIADOMY i ZOSTAJE zapisany. Grup nie znamy -
            // mowimy to wprost i NIE pokazujemy starych grup domu A jako B.
            _sonosEmptyReason = SonosSessionEmptyReason.NotRead;
            ApplySonosGroupRows();
            Announce(groups.Status == SonosDeviceReadStatus.NoAccount
                ? "Wybrano dom Sonos, ale konto się rozłączyło: grup nie znamy"
                : "Wybrano dom Sonos, ale nie udało się odczytać jego grup. Lista jest pusta, nie nieaktualna");
            RestoreFocusAfterSonosHouseholdChoice();
            return;
        }

        // 4) Publikacja grup NOWEGO domu. ApplySonosTopology nie ma czego
        // przenosic (cel byl wyczyszczony), wiec zadna grupa nie staje sie
        // aktywna po cichu i odtwarzacz sam sie nie otwiera.
        ApplySonosTopology(groups.Topology);
        Announce(SonosHouseholdGroupsSummary(groups.Topology.Groups.Count));
        RestoreFocusAfterSonosHouseholdChoice();
    }

    private static string SonosHouseholdGroupsSummary(int groupCount) => groupCount switch
    {
        0 => "Ten dom Sonos nie ma żadnych grup",
        1 => "Grupy nowego domu Sonos: 1 grupa. Wybierz grupę z listy",
        _ => $"Grupy nowego domu Sonos: {groupCount}. Wybierz grupę z listy"
    };

    /// <summary>
    /// Czy WOLNO pokazac modal wyboru domu TERAZ. Ten sam wzorzec, co brama
    /// widokow dla NVDA (MainWindow.Nvda.cs): okno musi byc widoczne i AKTYWNE,
    /// nie zamykane, w sesji Sonos i bez innego WIDOCZNEGO okna potomnego.
    ///
    /// Guard jest PRODUKCYJNY i testy go NIE obchodza - override pokazania siedzi
    /// za nim, nie przed nim. Wolany DWA razy z <c>ChooseSonosHouseholdAsync</c>:
    /// na WEJSCIU (zeby nie robic zadnego GET) i PONOWNIE po await, PRZED
    /// utworzeniem okna.
    /// </summary>
    private bool CanPresentSonosHouseholdChoice()
    {
        if (_isClosing) return false;
        if (!IsVisible || !IsActive || !IsEnabled) return false;
        if (!IsSonosSession(_sessions?.Current.Id)) return false;
        // Inny WIDOCZNY modal AMC ma pierwszenstwo: nie przykrywamy go spoznionym
        // wynikiem. _sonosHouseholdWindow jest tu zawsze null (sprawdzone na
        // wejsciu), wiec liczy sie kazde inne wlasne okno.
        return !OwnedWindows.OfType<Window>().Any(window => window.IsVisible);
    }

    /// <summary>
    /// POKAZANIE okna. Guardy kontekstu i fokusu sa juz za nami; tutaj zostaje
    /// samo powiazanie wlasciciela i modalnosc. Wlasciciel jest WYMAGANY - bez
    /// niego modal bylby osobnym oknem na pasku zadan i moglby zostac za AMC.
    /// </summary>
    private void PresentSonosHouseholdChoice(SonosHouseholdSelectionWindow window)
    {
        // WLASCICIEL jest czescia POPRAWKI, nie szczegolem pokazania: modal bez
        // wlasciciela moze zostac za AMC. Wiazemy go PRZED punktem podstawienia,
        // zeby testowa droga mierzyla TO SAMO powiazanie co produkcyjna.
        // IsVisible sprawdzil juz CanPresentSonosHouseholdChoice, a WPF przyjmuje
        // Owner wlasnie dla pokazanego okna. Wyjatku NIE tlumimy: pokazanie modalu
        // bez wlasciciela byloby dokladnie ta wada, ktora tu naprawiamy.
        window.Owner = this;

        if (PresentSonosHouseholdOverrideForTests is { } present)
        {
            present(window);
            return;
        }

        window.ShowDialog();
    }

    /// <summary>
    /// Powrot fokusu tam, gdzie uzytkownik byl: lista grup albo odtwarzacz.
    /// Tylko gdy okno glowne NADAL jest aktywne - nie kradniemy fokusu obcemu.
    /// </summary>
    private void RestoreFocusAfterSonosHouseholdChoice()
    {
        if (_isClosing || !IsActive) return;
        if (_playerViewActive) return;
        RestoreMediaListFocusAfterRefresh();
    }
}
