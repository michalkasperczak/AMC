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
    /// Kolejnosc jest istotna: granica konta PRZED wzieciem biletu bramki (bo
    /// <c>ApplySonosAccountBinding</c> sam wola <c>CancelSonosPendingWork</c>,
    /// ktory bilet PODNOSI), potem swiezy odczyt, potem guardy PRZED Show,
    /// potem dopiero zapis i odczyt grup.
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
        // Bramke wyboru trzymamy dalej MY: Cancel ja zwolnil, a ten przelot
        // jeszcze pracuje. Bez tego drugie polecenie weszloby w polowie zamiany.
        _sonosHouseholdChoiceInFlight = true;
        _ = ticket;
        _ = gate;

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
    /// POKAZANIE okna. Guardy kontekstu sa juz za nami; tutaj zostaje samo
    /// powiazanie wlasciciela i modalnosc. Brak wlasciciela nie moze wywrocic
    /// drogi do wyboru, wiec nieudane powiazanie jest pomijane.
    /// </summary>
    private void PresentSonosHouseholdChoice(SonosHouseholdSelectionWindow window)
    {
        if (PresentSonosHouseholdOverrideForTests is { } present)
        {
            present(window);
            return;
        }

        // Okno glowne musi BYC widoczne: WPF przyjmuje Owner tylko dla okna,
        // ktore juz pokazano, a modal nad nieistniejacym oknem nie ma sensu.
        if (IsVisible)
        {
            try
            {
                window.Owner = this;
            }
            catch (InvalidOperationException)
            {
            }
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
