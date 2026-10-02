using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using AccessibleMediaController.Core.Commands;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Core.Sonos;

namespace AccessibleMediaController.Windows;

/// <summary>
/// BIBLIOTEKA MATERIALU SONOSA (Ctrl+L) i WYBOR CELU STEROWANIA (Ctrl+F5).
///
/// Decyzja, ktora ten plik wykonuje:
///  * Ctrl+L w sesji Sonos otwiera BIBLIOTEKE TRESCI z kategoriami "Ulubione
///    Sonos" i "Playlisty Sonos". Dotad Ctrl+L odmawial zdaniem "Biblioteka
///    Sonos pokazuje wszystkie odczytane głośniki i grupy" - to byla odmowa, nie
///    biblioteka.
///  * Ctrl+U nadal prowadzi BEZPOSREDNIO do ulubionych - istniejacy skrot zostaje
///    bez zmian, bo jest odebrany i dziala.
///  * Ctrl+F5 jest MIEJSCEM WYBORU I INFORMACJI O CELU (dom, grupy). Konto Sonos
///    pozostaje dostepne Z TEGO SAMEGO miejsca, zeby zadna odebrana droga nie
///    zginela.
///
/// ZADNYCH NOWYCH SKROTOW: uzywamy wylacznie Ctrl+L, Ctrl+U i Ctrl+F5, ktore w
/// AMC juz istnialy.
///
/// DLACZEGO OKNA, A NIE WIERSZE W LISCIE: lista sesji Sonos jest MODELEM
/// STEROWANIA i jest cyklicznie publikowana przez <c>ApplySonosGroupRows</c> z
/// istniejacego timera odczytow topologii. Kategorie wstawione do tej listy
/// zginelyby albo przestawilyby zaznaczenie przy najblizszym odswiezeniu - w
/// trakcie czytania przez czytnik ekranu. Okna sa od tamtego timera niezalezne,
/// wiec kategorie nie gina, pozycja nie skacze, a lista glosnikow pozostaje
/// modelem sterowania, nie pozorna biblioteka.
/// </summary>
public partial class MainWindow
{
    private SonosLibraryWindow? _sonosLibraryWindow;
    private SonosTargetSelectionWindow? _sonosTargetWindow;

    internal Task? LastSonosLibraryTaskForTests { get; private set; }

    /// <summary>ODCZYT zlecony przez OTWARTE okno celu (Ctrl+F5 / F5 w oknie).</summary>
    internal Task? LastSonosTargetRefreshTaskForTests { get; private set; }


    internal int SonosLibraryWindowsCreatedForTests { get; private set; }

    internal int SonosTargetWindowsCreatedForTests { get; private set; }

    /// <summary>Ile razy wybor celu ZMIENIL aktywna grupe. Anulowanie nie liczy sie.</summary>
    internal int SonosTargetSelectionsAppliedForTests { get; private set; }

    internal SonosLibraryWindow? OpenSonosLibraryWindowForTests => _sonosLibraryWindow;

    internal SonosTargetSelectionWindow? OpenSonosTargetWindowForTests => _sonosTargetWindow;

    /// <summary>TESTOWY punkt podstawienia POKAZANIA Biblioteki (produkcyjnie modal).</summary>
    internal Action<SonosLibraryWindow>? PresentSonosLibraryOverrideForTests { get; set; }

    /// <summary>TESTOWY punkt podstawienia POKAZANIA wyboru celu (produkcyjnie modal).</summary>
    internal Action<SonosTargetSelectionWindow>? PresentSonosTargetOverrideForTests { get; set; }

    internal void ShowSonosLibraryForTests() => ShowSonosLibrary();

    /// <summary>
    /// TESTOWE ustawienie NAZWY WIDOKU przegladania tej sesji. Pomiar powrotu do
    /// sesji musi opuscic Sonosa z widoku INNEGO niz domyslny, a produkcyjny
    /// Ctrl+L otwiera Biblioteke jako MODAL, nie jako widok listy.
    /// </summary>
    internal void SetCurrentViewForTests(string viewName)
    {
        _currentView = viewName;
        CaptureCurrentSessionNavigationStateForTests();
    }

    internal void CaptureCurrentSessionNavigationStateForTests() =>
        CaptureCurrentSessionNavigationState();

    internal void ShowSonosTargetSelectionForTests() => ShowSonosTargetSelection();

    /// <summary>
    /// WIERSZE KORZENIA TRESCI sesji Sonos: KATEGORIE Biblioteki, nie glosniki.
    ///
    /// Wiersze zyja WYLACZNIE w widoku - do <c>Session.Items</c> nie wchodzi ani
    /// jeden, wiec zaden ogolny tor odtwarzania, presetu ani ulubionych AMC nie
    /// ma czego zlapac, a lista grup zostaje nietknietym modelem sterowania.
    /// <c>MediaItemKind.Folder</c> jest tu swiadomy: to KONTENER do wejscia, nie
    /// material, i istniejacy <c>RejectFolderContainer</c> w Core juz odmawia
    /// dodawania takiego wiersza do kolejki i Biblioteki.
    /// </summary>
    private List<MediaItemRow> CreateSonosLibraryCategoryRows() =>
        SonosLibraryPresentation.DescribeCategories()
            .Select(category => new MediaItemRow(
                new MediaItem
                {
                    Id = category.CategoryId,
                    Title = category.Name,
                    Kind = MediaItemKind.Folder
                },
                category.Name,
                category.Name))
            .ToList();

    /// <summary>
    /// Enter na WIERSZU KORZENIA Biblioteki Sonosa. Wchodzi w te same, odebrane
    /// drogi kategorii, ktore otwiera modal Ctrl+L - zero drugiego toru.
    /// </summary>
    /// <returns>
    /// Prawda, gdy wiersz BYL kategoria i zostal obsluzony; wtedy wolajacy nie
    /// moze juz probowac aktywacji grupy ani ogolnej nawigacji.
    /// </returns>
    private bool TryOpenSonosLibraryCategoryRow(string? itemId)
    {
        if (!IsSonosSession(_sessions?.Current.Id)) return false;
        if (!SonosLibraryPresentation.IsCategoryId(itemId)) return false;
        var row = SonosLibraryPresentation.DescribeCategories()
            .FirstOrDefault(category =>
                string.Equals(category.CategoryId, itemId, StringComparison.Ordinal));
        if (row is null) return false;
        OpenSonosLibraryCategory(row);
        return true;
    }

    /// <summary>
    /// Ctrl+L w sesji Sonos: BIBLIOTEKA MATERIALU. Zero sieci przy samym otwarciu
    /// - kategorie sa znane z Core, a odczyt leci dopiero po Enter na kategorii.
    /// </summary>
    private void ShowSonosLibrary()
    {
        // Callback kategorii moze otworzyc kolejny modal przed powrotem ze
        // ShowDialog Biblioteki. Pole jest wtedy jeszcze niepuste, chociaz
        // okno kategorii juz zamknieto. Nie traktuj takiej referencji jak
        // zywego okna; sama jej obecnosc nie dowodzi usterki klawiatury.
        if (_sonosLibraryWindow is { IsLiveOwnerTarget: true })
        {
            Announce("Biblioteka Sonos jest już otwarta");
            try
            {
                _sonosLibraryWindow.Activate();
            }
            catch (InvalidOperationException)
            {
            }

            return;
        }

        if (!CanPresentSonosChildWindow())
        {
            Announce("Biblioteka Sonos nie została otwarta, bo okno AMC nie jest aktywne. "
                + "Wróć do AMC i ponów otwarcie Biblioteki");
            return;
        }

        var window = new SonosLibraryWindow(OpenSonosLibraryCategory);
        SonosLibraryWindowsCreatedForTests++;
        _sonosLibraryWindow = window;
        Announce(SonosLibraryPresentation.ViewIntroduction);
        try
        {
            window.Owner = this;
            if (PresentSonosLibraryOverrideForTests is { } present) present(window);
            else window.ShowDialog();
        }
        finally
        {
            _sonosLibraryWindow = null;
        }
    }

    /// <summary>
    /// Enter na kategorii Biblioteki. Rozpoznanie idzie po IDENTYFIKATORZE wiersza,
    /// NIGDY po polskiej nazwie. Kazda kategoria wchodzi w ISTNIEJACA, odebrana
    /// droge odczytu - nie powstaje tu drugi, rownolegly tor.
    /// </summary>
    private void OpenSonosLibraryCategory(SonosLibraryPresentation.SonosLibraryCategoryRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        if (string.Equals(row.CategoryId, SonosLibraryPresentation.FavoritesCategoryId,
            StringComparison.Ordinal))
        {
            // DOKLADNIE ta sama droga, co Ctrl+U - zero kopii logiki ulubionych.
            StartSonosFavoritesView();
            return;
        }

        if (string.Equals(row.CategoryId, SonosLibraryPresentation.PlaylistsCategoryId,
            StringComparison.Ordinal))
        {
            LastSonosPlaylistsTaskForTests = ShowSonosPlaylistsAsync();
            return;
        }

        if (string.Equals(row.CategoryId, SonosLibraryPresentation.OwnStreamsCategoryId,
            StringComparison.Ordinal))
        {
            ShowSonosOwnStreams();
            return;
        }

        // NIEZNANY identyfikator: cisza byla by najgorsza odpowiedzia.
        Announce("Ta kategoria Biblioteki Sonos nie ma jeszcze własnej listy.");
    }

    /// <summary>
    /// Ctrl+F5 w sesji Sonos: WYBOR I INFORMACJA O CELU STEROWANIA.
    ///
    /// Okno NIE GRA, nie tworzy i nie rozwiazuje grup: zmienia wylacznie to, do
    /// czego AMC adresuje polecenia. Topologie bierzemy z JUZ ODCZYTANEJ migawki -
    /// zadnego nowego pollingu przy samym otwarciu.
    ///
    /// KONTO SONOS POZOSTAJE DOSTEPNE: po zamknieciu wyboru celu mowimy wprost, ze
    /// konto i dom siedza w oknie konta Sonos, zeby dotychczasowa droga Ctrl+F5
    /// (zarzadzanie poleczeniem) nie zginela bez slowa.
    /// </summary>
    private void ShowSonosTargetSelection()
    {
        if (_sonosTargetWindow is not null)
        {
            Announce("Wybór celu sterowania Sonos jest już otwarty");
            try
            {
                _sonosTargetWindow.Activate();
            }
            catch (InvalidOperationException)
            {
            }

            return;
        }

        if (!CanPresentSonosChildWindow())
        {
            Announce("Wybór celu Sonos nie został otwarty, bo okno AMC nie jest aktywne. "
                + "Wróć do AMC i ponów skrót Control F5");
            return;
        }

        // GRANICA konta PRZED uzyciem zapamietanej topologii: po rzeczywistej
        // zmianie konta stare grupy nie maja prawa wrocic na liste.
        ApplySonosAccountBinding();

        // PUSTA TOPOLOGIA: 4.1.7 otwieralo puste okno i odsylalo do kolejnego
        // Control F5 - petla odmow. Teraz Control F5 OTWIERA okno OD RAZU, mowi
        // "Wczytuję grupy" i SAM zleca odczyt tym samym istniejacym torem, co
        // menu Plik; wynik wpada do TEGO SAMEGO, juz otwartego okna. Dzieki temu
        // jedno otwarcie daje uzywalny wybor bez powtarzania skrotu, a zadne
        // spoznione okno nie wyskakuje po wyjsciu - okno istnieje PRZED odczytem.
        if (_sonosTopology is null)
        {
            ExecuteCommand(CommandIds.RefreshSonosGroups);
        }

        var topology = _sonosTopology;
        var window = topology is null
            ? new SonosTargetSelectionWindow(DescribeSonosTargetUnavailable())
            : new SonosTargetSelectionWindow(topology, _state.Sonos.SelectedGroupId);
        SonosTargetWindowsCreatedForTests++;
        _sonosTargetWindow = window;
        var ticket = _sonosTargetTicket;
        try
        {
            window.Owner = this;
            if (PresentSonosTargetOverrideForTests is { } present) present(window);
            else window.ShowDialog();
        }
        finally
        {
            _sonosTargetWindow = null;
        }

        // WYBOR GLOSNIKOW: INTENCJA czytana PRZED galezia potwierdzenia celu, inaczej
        // uslyszelibysmy "Cel bez zmian" o czyms, co wlasnie sie zaczyna. Modal
        // otwieramy PO zamknieciu tego okna, nie z jego callbacku.
        if (window.SpeakersRequested)
        {
            if (_isClosing) return;
            StartSonosSpeakerSelection();
            return;
        }

        // ANULOWANIE NIE ZMIENIA NICZEGO: bez potwierdzenia nie ruszamy celu.
        if (!window.Confirmed || window.SelectedGroupId is not { } groupId)
        {
            Announce("Cel sterowania Sonos bez zmian. Konto i dom Sonos znajdziesz "
                + "w oknie konta Sonos.");
            return;
        }

        // GRANICE PO MODALU: zmiana konta, wyjscie z sesji albo zamykanie AMC
        // koncza droge. Nie wybieramy wtedy celu "na slepo".
        if (_isClosing) return;
        if (ApplySonosAccountBinding() || ticket != _sonosTargetTicket)
        {
            Announce("Cel sterowania Sonos nie został zmieniony, bo konto Sonos się zmieniło.");
            return;
        }

        if (!IsSonosSession(_sessions?.Current.Id)) return;

        // WYBOR CELU, nie odtwarzanie: zadnego POST materialu. Aktywacja idzie
        // ISTNIEJACA, odebrana droga ActivateSonosGroupAsync - nie powstaje tu
        // drugi tor zmiany celu. Komunikat mowi "Wybrano cel", nigdy "gra".
        LastSonosLibraryTaskForTests = ApplySonosTargetSelectionAsync(groupId, window.SelectedGroupLabel);
    }

    /// <summary>
    /// ODCZYT GRUP NA RZECZ OTWARTEGO OKNA CELU. Zlecany przy otwarciu z pusta
    /// topologia oraz przez F5 w samym oknie.
    ///
    /// GRANICE, ktorych ten tor NIE wolno zdjac:
    ///  * ISTNIEJACA bramka "jedno odswiezenie naraz" siedzi w
    ///    <see cref="RefreshSonosTopologyAsync"/> - kilkukrotne F5/Ctrl+F5 w
    ///    trakcie odczytu NIE mnozy GET ani okien.
    ///  * Wynik wpuszczamy do okna TYLKO gdy to NADAL TO SAMO, ZYWE okno
    ///    (<c>_sonosTargetWindow</c>), bilet konta/sesji sie nie zmienil i AMC
    ///    sie nie zamyka. Po Cancel / zmianie konta / wyjsciu z sesji spozniony
    ///    odczyt NIE przywraca starej listy i NIE otwiera nowego okna.
    ///  * ZERO POST: to wylacznie odczyt.
    ///
    /// GRANICA TEGO TORU (swiadoma, nie przeoczona): gdy W TYM MOMENCIE trwa JUZ
    /// inne odswiezenie, istniejaca bramka <c>RefreshSonosTopologyAsync</c>
    /// wraca od razu ze "Odświeżanie grup Sonos już trwa" i okno opublikuje
    /// jeszcze nieznana topologie z uczciwa przyczyna; F5 w oknie ponawia. Nie
    /// oslabiamy tu tej bramki, bo mnozylaby GET-y.
    /// </summary>
    private void BeginSonosTargetRefresh(SonosTargetSelectionWindow window)
    {
        if (_isClosing) return;
        if (!ReferenceEquals(_sonosTargetWindow, window)) return;
        window.ShowLoadingForTarget();
        LastSonosTargetRefreshTaskForTests = RunSonosTargetRefreshAsync(window);
    }

    private async Task RunSonosTargetRefreshAsync(SonosTargetSelectionWindow window)
    {
        var ticket = _sonosTargetTicket;
        var session = _sessions?.Current.Id;

        // ISTNIEJACY tor odczytu - ten sam, co menu Plik i F5 na liscie. Zadnego
        // nowego brokera ani drugiej drogi do zaplecza.
        await RefreshSonosTopologyAsync().ConfigureAwait(true);

        // SPOZNIONY WYNIK NIE WRACA: okno musi zyc i byc TYM SAMYM, konto i
        // sesja bez zmian, AMC nie w zamykaniu.
        if (_isClosing) return;
        if (!ReferenceEquals(_sonosTargetWindow, window)) return;
        if (ApplySonosAccountBinding() || ticket != _sonosTargetTicket) return;
        if (!IsSonosSession(_sessions?.Current.Id)) return;
        if (!string.Equals(session, _sessions?.Current.Id, StringComparison.Ordinal)) return;

        var topology = _sonosTopology;
        window.ApplyRefreshedTopology(
            topology,
            _state.Sonos.SelectedGroupId,
            topology is null ? DescribeSonosTargetUnavailable() : null);
    }

    /// <summary>
    /// ZMIANA CELU przez odebrana droge aktywacji grupy. Mowimy "Wybrano", bo
    /// wybor celu NIE URUCHAMIA muzyki - to ustawienie adresata polecen.
    /// </summary>
    private async Task ApplySonosTargetSelectionAsync(string groupId, string? label)
    {
        if (await ActivateSonosGroupAsync(groupId).ConfigureAwait(true) is null)
        {
            // ActivateSonosGroupAsync juz powiedzial, ze grupy nie ma.
            return;
        }

        SonosTargetSelectionsAppliedForTests++;
        Announce(SonosTargetSelectionLabels.DescribeSelected(
            label ?? SonosTargetSelectionLabels.UnnamedGroup));
    }

    /// <summary>
    /// UCZCIWA przyczyna braku listy grup. Rozrozniamy brak konta, brak domu i
    /// nieodczytana topologie - zamiast jednej pustej listy dla wszystkiego.
    /// </summary>
    private string DescribeSonosTargetUnavailable()
    {
        var backend = _sonosBackend as ISonosAccountBoundBackend
            ?? SonosBackendOverride as ISonosAccountBoundBackend;
        if (backend?.AccountSnapshot is { } snapshot
            && snapshot.State != SonosAccountState.Connected)
        {
            return SonosTargetSelectionLabels.NotSignedIn;
        }

        return string.IsNullOrWhiteSpace(_state.Sonos.SelectedHouseholdId)
            ? SonosTargetSelectionLabels.NoHousehold
            : SonosTargetSelectionLabels.TopologyUnknown;
    }

    /// <summary>
    /// Czy WOLNO pokazac okno potomne Sonosa TERAZ. Ten sam wzorzec, co brama
    /// ulubionych i playlist: okno widoczne i AKTYWNE, w sesji Sonos i bez innego
    /// WIDOCZNEGO okna potomnego.
    /// </summary>
    private bool CanPresentSonosChildWindow()
    {
        if (_isClosing) return false;
        if (!IsVisible || !IsEnabled) return false;
        // AKTYWNOSC liczymy dla CALEJ APLIKACJI, nie dla samego okna glownego.
        //
        // ZGLOSZENIE: "Biblioteka Sonos nie została otwarta, bo okno AMC nie jest
        // aktywne" lecialo przy Ctrl+L / Ctrl+F5 wywolanym Z NASZEGO WLASNEGO
        // okna potomnego (lancuch dialogow: SonosLibraryWindow.Close() -> otwarcie
        // kolejnego okna). W tym momencie MainWindow.IsActive jest JESZCZE false,
        // bo WPF nie oddal aktywacji wlascicielowi - mimo ze pierwszy plan nalezy
        // do NAS. Stary warunek czytal to jako "uzytkownik pracuje w innej
        // aplikacji" i odmawial, choc nic obcego nie bylo na wierzchu.
        if (!IsApplicationForeground()) return false;
        if (!IsSonosSession(_sessions?.Current.Id)) return false;
        return !OwnedWindows.OfType<Window>().Any(window => window.IsVisible);
    }

    /// <summary>
    /// Czy PIERWSZY PLAN nalezy do TEGO PROCESU. Zastepuje <c>Window.IsActive</c>
    /// w bramie okien potomnych Sonosa.
    ///
    /// CO TO ZACHOWUJE: obca aplikacja na pierwszym planie nadal ODMAWIA - modal
    /// nie wyskoczy pod reka uzytkownika pracujacego gdzie indziej. Porownujemy
    /// PID wlasciciela okna pierwszego planu z wlasnym, wiec KAZDE nasze okno
    /// (glowne, Biblioteka, wybor celu, Podglad mowy) liczy sie jako "jestesmy na
    /// wierzchu".
    ///
    /// CO TO NAPRAWIA: lancuch dialogow. <c>SonosLibraryWindow</c> zamyka sie i
    /// natychmiast kaze wlascicielowi otworzyc kolejne okno; WPF jeszcze nie
    /// przywrocil <c>IsActive</c> na <c>MainWindow</c>, a <c>OwnedWindows</c> jest
    /// juz puste. Stary warunek trafial dokladnie w te luke.
    ///
    /// NIEZNANY PIERWSZY PLAN NIE JEST POTWIERDZENIEM. Brak HWND, PID rowny zero
    /// albo wyjatek z Win32 oznacza, ze NIE WIEMY, czyje jest wierzch - a nie, ze
    /// nasze. Zwracamy wtedy FALSZ, bo przeciwny wybor otwieral modal pod reka
    /// uzytkownika pracujacego w obcej aplikacji. Lancuch WLASNYCH dialogow nie
    /// cierpi: wyzej odpowiadaja na niego <c>IsActive</c> okna glownego i okien
    /// potomnych, ktore nie potrzebuja Win32.
    /// </summary>
    private bool IsApplicationForeground()
    {
        // Okno glowne aktywne to juz dowod - bez wchodzenia w Win32.
        if (IsActive) return true;
        if (OwnedWindows.OfType<Window>().Any(window => window.IsActive)) return true;

        if (ForegroundProcessIdOverrideForTests is { } injected)
        {
            return injected == Environment.ProcessId;
        }

        try
        {
            var foreground = NativeForeground.GetForegroundWindow();
            if (foreground == IntPtr.Zero) return false;
            _ = NativeForeground.GetWindowThreadProcessId(foreground, out var processId);
            if (processId == 0) return false;
            return processId == Environment.ProcessId;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
    }

    /// <summary>
    /// TESTOWE podstawienie PID wlasciciela pierwszego planu. Pomiar nie moze
    /// polegac na tym, ktore okno pulpitu jest akurat na wierzchu, a OBCY
    /// pierwszy plan musi dac sie zmierzyc bez kradziezy fokusu uzytkownikowi.
    /// </summary>
    internal int? ForegroundProcessIdOverrideForTests { get; set; }

    private static class NativeForeground
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        internal static extern IntPtr GetForegroundWindow();

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        internal static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    }
}
