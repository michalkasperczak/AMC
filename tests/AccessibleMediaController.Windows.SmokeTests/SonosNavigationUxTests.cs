using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using AccessibleMediaController.Core.Commands;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Core.Sonos;
using AccessibleMediaController.Windows;
using AccessibleMediaController.Windows.Services;

/// <summary>
/// UKLAD interfejsu sesji Sonos mierzony RZECZYWISTA droga uzytkownika.
///
///   * U1 BIBLIOTEKA: istniejace Ctrl+L (CommandIds.ViewLibrary) pokazuje grupy
///     jako CELE sterowania. Mierzymy zawartosc KONTROLKI MediaList, nie flagi.
///   * U2 ULUBIONE: zaden tor nie ma prawa wstawic glosnika/grupy do Ulubionych.
///     ToggleFavorite na wierszu grupy (takze przy PUSTEJ liscie z CurrentItem -
///     przypadek z logu Michala) musi ODMOWIC, a sterowanie zostaje.
///   * U3 Ctrl+F5: PRAWDZIWY handler klawiatury w sesji Sonos otwiera ISTNIEJACE
///     okno Konta Sonos. Modyfikator bierzemy ze stanu klawiatury WLASNEGO watku.
///   * U4 KROTKI ODCZYT: wiersz grupy w liscie to krotka nazwa, bez powtorzonej
///     nazwy i technicznych liczb - szczegoly sa w oknie Glosniki i grupy.
///
/// Pomiar ma WLASNE okno (przelacznik --sonos-navigation-ux). Zero HTTP, zero
/// DPAPI, zero konta, zero NVDA, zero audio, zero cudzych okien.
///
/// GRANICA KONTA (uzupelnienie po przegladzie SEC1): U3 prowadzi PRAWDZIWA
/// sciezke Ctrl+F5 -> ShowSonosAccountManager -> EnsureCoordinator ->
/// RestoreOnce. Samo podstawienie prezentera NIE omija tego odtworzenia, wiec
/// TEN SAM wlasciciel konta, ktorego uzywa prawdziwe okno, dostaje syntetyczny
/// magazyn i syntetyczna bramke (wzor: SonosSessionAccountUiTests.Harness).
/// Bez tego RestoreOnce poszedlby do DOMYSLNEGO SonosDpapiCredentialStore na
/// prawdziwej sciezce uzytkownika. Asercja <see cref="Harness.AssertAccountBoundariesAreSynthetic"/>
/// jest sprawdzana PRZED klawiszem, wiec niezastapione fabryki zatrzymuja
/// pomiar BEZ zadnego I/O.
/// </summary>
internal static class SonosNavigationUxTests
{
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    internal static void Run()
    {
        var checks = 0;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                checks += MeasureLibraryShowsContentCategories();
                checks += MeasureFavoritesRefuseSpeakers();
                checks += MeasureCtrlF5ChoosesControlTarget();
                // KOLEJNOSC: kontekst odtwarzacza mierzony PRZED kontekstem pola,
                // zeby zaden z tych dwoch przypadkow nie chowal sie za padnieciem
                // drugiego (oba byly zglaszane osobno przez zywy NVDA).
                checks += MeasureCtrlF5FromPlayerOpensTargetWindow();
                checks += MeasureCtrlF5FromFilterBoxOpensTargetWindow();
                checks += MeasureShortGroupRowLabel();
                checks += MeasureReturnToSonosRestoresMaterialNotSpeakers();
                checks += MeasureOwnForegroundDoesNotBlockChildWindows();
                checks += MeasureOwnStationsRespondToF2AndDelete();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
            finally
            {
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        thread.Join();
        if (failure is not null) throw failure;

        Console.WriteLine(
            "OK: uklad interfejsu Sonos - Ctrl+L otwiera Biblioteke materialu (Ulubione, Playlisty), "
            + "Ulubione odmawiaja glosnika, Ctrl+F5 wybiera cel sterowania z listy, z pola "
            + "filtrowania i z odtwarzacza z powrotem fokusu, wiersz grupy ma krotka nazwe "
            + $"({checks} sprawdzeń, WLASNE pokazane okno)");
    }

    /// <summary>
    /// POKAZ KORZENIA TRESCI dla ZYWEGO NVDA. Stawia PRAWDZIWE okno glowne w
    /// sesji Sonos przy uzyciu TEJ SAMEJ izolacji, ktorej uzywaja pomiary wyzej:
    /// atrapa zaplecza w pamieci, magazyn konta w pamieci, wartownik na Control
    /// API i brak integracji z pulpitem. ZERO sieci, konta, audio i sprzetu.
    ///
    /// Czytnik ma tu przeczytac to, co zglosil Michal: strzalki po KATEGORIACH
    /// (Ulubione Sonos, Moje stacje, ...), a nie po glosnikach - i to samo po
    /// POWROCIE do sesji z innej sesji.
    /// </summary>
    internal static void ShowContentRootForNvda(int seconds)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            Harness? harness = null;
            try
            {
                harness = Harness.Create();
                harness.EnterSonosSession();
                harness.Window.ShowInTaskbar = true;
                harness.Window.Title += "  [POMIAR NVDA: korzen tresci Sonos]";
                harness.ShowOwnWindow();
                // ShowOwnWindow celowo schowal okno z paska zadan (pomiary nie
                // zasmiecaja pulpitu). Dla ZYWEGO czytnika wracamy do paska i na
                // wierzch, inaczej NVDA nie ma czego przeczytac.
                harness.Window.ShowInTaskbar = true;
                harness.Window.Topmost = true;
                harness.Window.Activate();
                harness.Window.Focus();
                harness.PumpQuietly(TimeSpan.FromMilliseconds(300));

                Console.WriteLine("POKAZANO korzen tresci sesji Sonos. Widok: " + harness.CurrentView);
                Console.WriteLine("WIERSZE LISTY: " + string.Join(" | ", harness.RowLabels()));
                Console.WriteLine("CELE STEROWANIA (model, NIE na liscie): "
                    + string.Join(" | ", harness.Window.SonosGroupRows.Select(row => row.Name)));
                Console.WriteLine($"Limit pokazu: {seconds} s.");

                var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(seconds);
                while (DateTime.UtcNow < deadline && harness.Window.IsVisible)
                {
                    harness.PumpQuietly(TimeSpan.FromMilliseconds(200));
                }
            }
            catch (Exception exception)
            {
                failure = exception;
            }
            finally
            {
                harness?.Dispose();
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) throw failure;
        Console.WriteLine("KONIEC POKAZU korzenia tresci Sonos.");
    }

    // ===== U1: Ctrl+L otwiera BIBLIOTEKE MATERIALU, nie liste glosnikow =====

    /// <summary>
    /// ZMIENIONE OCZEKIWANIE. Ten pomiar zadal wczesniej, by Ctrl+L przestawialo
    /// widok listy na "Biblioteka" wypelniona GRUPAMI - czyli dokladnie uklad,
    /// ktory zostal odrzucony: glosniki i grupy NIE SA biblioteka muzyczna.
    ///
    /// Nowe oczekiwanie jest MOCNIEJSZE, nie slabsze: Ctrl+L musi otworzyc
    /// Biblioteke MATERIALU z kategoriami, lista sesji musi POZOSTAC modelem
    /// sterowania z nietknietymi grupami, a sama nawigacja nadal nie ma prawa
    /// wyslac polecenia do Sonosa.
    /// </summary>
    private static int MeasureLibraryShowsContentCategories()
    {
        using var harness = Harness.Create();
        var window = harness.Window;
        harness.EnterSonosSession();
        // OKNO MUSI BYC AKTYWNE: produkcyjna brama okien potomnych Sonosa odmawia
        // otwarcia, gdy AMC nie jest aktywne (zeby modal nie wyskoczyl pod reka
        // uzytkownika pracujacego w innej aplikacji). Pomiar respektuje te brame,
        // zamiast ja obchodzic.
        harness.ShowOwnWindow();

        // ZMIENIONE OCZEKIWANIE SASIADA (wymaganie korzenia tresci): widoczna
        // lista w korzeniu sesji Sonos pokazuje KATEGORIE, a model sterowania
        // (wiersze grup) zyje OSOBNO w SonosGroupRows i Session.Items. Dawniej
        // ten pomiar zadal 2 wierszy GRUP w liscie - to byl dokladnie uklad,
        // ktory Michal odrzucil.
        var rowsBefore = harness.RowLabels();
        if (harness.Window.SonosGroupRows.Count != 2)
        {
            throw new Exception(
                $"Sesja Sonos ma {harness.Window.SonosGroupRows.Count} odczytanych grup zamiast 2 "
                + "- model sterowania zginal.");
        }
        if (rowsBefore.Any(label => label.Contains("Salon", StringComparison.Ordinal)))
        {
            throw new Exception(
                "Korzen sesji Sonos pokazuje GLOSNIKI jako tresc: " + string.Join(" | ", rowsBefore));
        }

        // ISTNIEJACE polecenie Ctrl+L. Pokazanie podstawione, zeby nie stawiac
        // modalnego okna na pulpicie w pomiarze bez GUI.
        SonosLibraryWindow? opened = null;
        window.PresentSonosLibraryOverrideForTests = libraryWindow => opened = libraryWindow;
        harness.ExecuteCommand(CommandIds.ViewLibrary);
        harness.PumpQuietly(TimeSpan.FromMilliseconds(120));

        if (opened is null || window.SonosLibraryWindowsCreatedForTests != 1)
        {
            throw new Exception(
                "Ctrl+L w sesji Sonos nie otworzyło Biblioteki materiału (okien: "
                + $"{window.SonosLibraryWindowsCreatedForTests}; komunikaty: "
                + string.Join(" | ", harness.Announcements) + ").");
        }

        // KATEGORIE MATERIALU, nie glosniki.
        var categories = opened.CategoryNamesForTests;
        if (categories.Count != 3)
        {
            throw new Exception(
                $"Biblioteka ma {categories.Count} kategorii zamiast Ulubionych, Playlist i Moich stacji: "
                + string.Join(" | ", categories));
        }

        var surface = string.Join(" | ", categories);
        if (!surface.Contains("Ulubione", StringComparison.Ordinal)
            || !surface.Contains("Playlisty", StringComparison.Ordinal)
            || !surface.Contains("Moje stacje", StringComparison.Ordinal))
        {
            throw new Exception("Biblioteka nie pokazuje Ulubionych i Playlist: " + surface);
        }
        if (surface.Contains("Salon", StringComparison.Ordinal)
            || surface.Contains("Kuchnia", StringComparison.Ordinal))
        {
            throw new Exception("Biblioteka materiału znów podaje głośniki/grupy jako treść: " + surface);
        }

        // STEROWANIE: model sterowania nadal ma te same grupy. Biblioteka nie
        // przejela listy; lista widoku nadal pokazuje te same KATEGORIE.
        var rowsAfter = harness.RowLabels();
        if (!rowsAfter.SequenceEqual(rowsBefore, StringComparer.Ordinal))
        {
            throw new Exception(
                "Otwarcie Biblioteki zmieniło listę celów sterowania: " + string.Join(" | ", rowsAfter));
        }

        // Zaden glosnik nie zostal RECZNIE dodany do Biblioteki.
        if (harness.SonosSession.Items.Any(item => item.IsInLibrary))
        {
            throw new Exception("Grupy Sonos dostały trwałą flagę IsInLibrary - to ręczne dodanie do Biblioteki.");
        }

        // ODZIEDZICZONY widok Ulubione nie ma prawa zostac fałszywą lista grup.
        harness.ExecuteCommand(CommandIds.ViewFavorites);
        harness.PumpQuietly(TimeSpan.FromMilliseconds(120));
        if (string.Equals(harness.CurrentView, "Ulubione", StringComparison.Ordinal)
            && harness.RowLabels().Any(label => label.Contains("Salon", StringComparison.Ordinal)))
        {
            throw new Exception("Widok Ulubione pokazuje głośniki Sonos jako materiał.");
        }
        if (harness.Backend.Commands.Count != 0)
        {
            throw new Exception("Sama nawigacja po widokach wysłała polecenie do Sonosa.");
        }
        return 7;
    }

    // ===== U2: Ulubione odmawiaja glosnika =====

    private static int MeasureFavoritesRefuseSpeakers()
    {
        using var harness = Harness.Create();
        var window = harness.Window;
        harness.EnterSonosSession();
        // Grupy sa w liscie z samego wejscia w sesje. Ctrl+L prowadzi teraz do
        // Biblioteki MATERIALU i nie jest zrodlem wierszy grup.

        // Przypadek A: uzytkownik ma ZAZNACZONY wiersz grupy.
        harness.SelectRow(0);
        harness.Announcements.Clear();
        harness.ExecuteCommand(CommandIds.ToggleFavorite);
        harness.PumpQuietly(TimeSpan.FromMilliseconds(120));
        if (harness.SonosSession.Items.Any(item => item.IsFavorite))
        {
            throw new Exception("ToggleFavorite dodał głośnik/grupę Sonos do Ulubionych.");
        }
        if (harness.Announcements.Count == 0)
        {
            throw new Exception("Odmowa dodania głośnika do Ulubionych przeszła w ciszy.");
        }

        // Przypadek B z LOGU: lista PUSTA, ale sesja ma CurrentItem. Wtedy
        // ActionItems bierze CurrentItem i guard po zaznaczeniu by nie zadzialal.
        harness.ClearListSelection();
        harness.Announcements.Clear();
        harness.ExecuteCommand(CommandIds.ToggleFavorite);
        harness.PumpQuietly(TimeSpan.FromMilliseconds(120));
        if (harness.SonosSession.Items.Any(item => item.IsFavorite))
        {
            throw new Exception(
                "ToggleFavorite bez zaznaczenia (pusta lista, obecny CurrentItem) dodał głośnik do Ulubionych.");
        }

        // ToggleLibrary nie moze UKRYC celu sterowania. Cel zyje w modelu
        // (SonosGroupRows), nie na widocznej liscie - ta pokazuje kategorie.
        harness.SelectRow(0);
        harness.ExecuteCommand(CommandIds.ToggleLibrary);
        harness.PumpQuietly(TimeSpan.FromMilliseconds(120));
        if (harness.Window.SonosGroupRows.Count != 2)
        {
            throw new Exception("ToggleLibrary ukrył cel sterowania Sonos z modelu grup.");
        }

        // STEROWANIE zostaje nietkniete.
        harness.SelectRow(0);
        harness.Pump(window.ActivateSonosGroupForTests("GRUPA-SALON"));
        harness.Pump(window.ExecuteSonosCommandForTests(CommandIds.PlayPause));
        harness.PumpQuietly(TimeSpan.FromMilliseconds(120));
        if (harness.Backend.Commands.Count == 0)
        {
            throw new Exception("Guard Ulubionych zablokował sterowanie odtwarzaniem Sonos.");
        }
        return 5;
    }

    // ===== U3: Ctrl+F5 wybiera CEL STEROWANIA, a konto zostaje dostepne =====

    /// <summary>
    /// ZMIENIONE OCZEKIWANIE. Ten pomiar zadal wczesniej, by Ctrl+F5 otwieralo
    /// okno Konto Sonos. Ctrl+F5 jest teraz MIEJSCEM WYBORU CELU (dom, grupy,
    /// glosniki) - taka byla decyzja. Pomiar nie slabnie: nadal pilnuje, ze
    /// zadne poswiadczenia nie sa ruszane, zaden klient Control API nie powstaje
    /// i ze SAM WYBOR nie wysyla polecenia sterujacego.
    ///
    /// DOSTEPNOSC KONTA, dawniej dowodzona przyciskiem "Głośniki i grupy",
    /// mierzymy teraz slowem: po zamknieciu wyboru AMC mowi, gdzie sa konto i dom.
    /// </summary>
    private static int MeasureCtrlF5ChoosesControlTarget()
    {
        using var harness = Harness.Create();
        var window = harness.Window;
        harness.EnterSonosSession();
        harness.ShowOwnWindow();
        harness.SelectRow(0);

        // PRODUKCYJNY punkt podstawienia POKAZANIA okna: mierzymy, ze powstalo
        // okno wyboru celu, bez ShowDialog na pulpicie. Anulujemy je (brak
        // potwierdzenia), bo ten przypadek ma dowiesc, ze sam Ctrl+F5 niczego
        // nie zmienia i niczego nie gra.
        SonosTargetSelectionWindow? opened = null;
        window.PresentSonosTargetOverrideForTests = targetWindow => opened = targetWindow;

        harness.Announcements.Clear();
        // GRANICA PRZED KLAWISZEM: fixture MUSI mieć odcięty prawdziwy magazyn
        // konta. Ta asercja nie robi zadnego I/O - pada, gdy fabryki nie sa
        // zastapione.
        harness.AssertAccountBoundariesAreSynthetic();
        harness.PressCtrl(Key.F5);
        harness.PumpQuietly(TimeSpan.FromMilliseconds(200));

        if (opened is null || window.SonosTargetWindowsCreatedForTests != 1)
        {
            throw new Exception(
                "Ctrl+F5 w sesji Sonos nie otworzyło wyboru celu sterowania (okien: "
                + $"{window.SonosTargetWindowsCreatedForTests}; komunikaty: "
                + string.Join(" | ", harness.Announcements) + ").");
        }

        // PELNE NAZWY grup, nie identyfikatory i nie skroty w rodzaju "Biuro +1".
        var targets = opened.RowLabelsForTests;
        if (targets.Count == 0)
        {
            throw new Exception("Wybór celu nie pokazał żadnej odczytanej grupy.");
        }
        var targetSurface = string.Join(" | ", targets);
        if (!targetSurface.Contains("Salon", StringComparison.Ordinal))
        {
            throw new Exception("Wybór celu nie pokazuje odczytanej grupy Salon: " + targetSurface);
        }
        if (targetSurface.Contains("GRUPA-", StringComparison.Ordinal))
        {
            throw new Exception("Wybór celu pokazuje identyfikatory zamiast nazw: " + targetSurface);
        }

        // KONTROLKA ODCIECIA: zaden prawdziwy klient Control API nie powstal i
        // zadne logowanie nie ruszylo.
        if (harness.AccountStore.Writes != 0 || harness.AccountStore.Deletes != 0)
        {
            throw new Exception("Ctrl+F5 zapisał albo skasował poświadczenia Sonos.");
        }
        if (harness.Gateway.Calls != 0)
        {
            throw new Exception("Ctrl+F5 ruszył bramkę logowania Sonos bez polecenia użytkownika.");
        }
        if (harness.AccountOwner.ControlApiCreations != 0)
        {
            throw new Exception("Ctrl+F5 utworzył klienta Control API Sonos - to droga do prawdziwego HTTP.");
        }

        if (harness.Announcements.Any(m => m.Contains("nie ma polecenia", StringComparison.OrdinalIgnoreCase)))
        {
            throw new Exception("Ctrl+F5 nadal mówi, że nie ma polecenia w bieżącej sesji.");
        }

        // KONTO I DOM NADAL DOSTEPNE: po anulowaniu AMC mowi, gdzie ich szukac.
        if (!harness.Announcements.Any(m => m.Contains("konta Sonos", StringComparison.OrdinalIgnoreCase)))
        {
            throw new Exception(
                "Po wyborze celu AMC nie mówi, gdzie są konto i dom Sonos: "
                + string.Join(" | ", harness.Announcements));
        }

        // SAM WYBOR bez potwierdzenia NIE zmienia celu.
        if (window.SonosTargetSelectionsAppliedForTests != 0)
        {
            throw new Exception("Anulowany wybór celu mimo to zmienił cel sterowania.");
        }

        // Powrot zachowuje miejsce i wybor.
        if (harness.MediaList.SelectedIndex != 0)
        {
            throw new Exception("Powrót z wyboru celu zgubił zaznaczenie w liście.");
        }
        if (harness.Backend.Commands.Count != 0)
        {
            throw new Exception("Ctrl+F5 wysłał polecenie sterujące do Sonosa.");
        }
        return 11;
    }

    // ===== U3b: Ctrl+F5 z POLA FILTROWANIA =====

    /// <summary>
    /// ZGLOSZENIE (zywy NVDA, 657): Ctrl+F5 w sesji Sonos NIC nie otwieralo, gdy
    /// fokus byl w polu "Filtruj listę". Pomiar prowadzi PRAWDZIWY
    /// Window_PreviewKeyDown przy RZECZYWISTYM Keyboard.FocusedElement bedacym
    /// tym polem, a okno konta pokazuje sie PRAWDZIWYM modalnym ShowDialog
    /// (zamykanym wlasnym zegarem), zeby powrot fokusu byl mierzony na
    /// prawdziwej drodze WPF, nie na podstawionym pokazaniu.
    /// </summary>
    private static int MeasureCtrlF5FromFilterBoxOpensTargetWindow()
    {
        using var harness = Harness.Create();
        harness.EnterSonosSession();
        harness.ShowOwnWindow();
        harness.SelectRow(0);

        var filter = harness.FilterBox;
        filter.Text = "Sal";
        filter.CaretIndex = 2;
        harness.PumpQuietly(TimeSpan.FromMilliseconds(120));
        var viewBefore = harness.CurrentView;
        var selectedBefore = harness.MediaList.SelectedIndex;
        var groupBefore = harness.Window.SonosSelectedGroupId;

        harness.FocusFilterBox();
        if (!ReferenceEquals(Keyboard.FocusedElement, filter))
        {
            throw new Exception(
                "Pomiar nie postawil fokusu klawiatury w polu filtrowania: "
                + (Keyboard.FocusedElement?.GetType().Name ?? "brak") + ".");
        }

        // PRAWDZIWY modal: wlasny zegar zamyka okno wyboru celu, ktore powstalo
        // produkcyjnym ShowDialog. Fokus mierzymy na prawdziwej drodze WPF.
        var shown = 0;
        harness.Window.PresentSonosTargetOverrideForTests = targetWindow =>
        {
            shown++;
            CloseWhenShown(targetWindow);
            targetWindow.ShowDialog();
        };

        harness.Announcements.Clear();
        harness.AssertAccountBoundariesAreSynthetic();
        harness.PressCtrl(Key.F5);
        harness.PumpQuietly(TimeSpan.FromMilliseconds(350));

        if (shown != 1)
        {
            throw new Exception(
                $"Ctrl+F5 z pola filtrowania nie otworzylo wyboru celu Sonos (okien: {shown}; "
                + "komunikaty: " + string.Join(" | ", harness.Announcements) + ").");
        }
        if (harness.AccountStore.Writes != 0 || harness.AccountStore.Deletes != 0)
        {
            throw new Exception("Ctrl+F5 z pola filtrowania zapisal albo skasowal poswiadczenia Sonos.");
        }
        if (harness.Gateway.Calls != 0 || harness.AccountOwner.ControlApiCreations != 0)
        {
            throw new Exception("Ctrl+F5 z pola filtrowania ruszyl logowanie albo Control API Sonos.");
        }
        if (harness.Backend.Commands.Count != 0)
        {
            throw new Exception("Ctrl+F5 z pola filtrowania wyslal polecenie sterujace do Sonosa.");
        }

        // POWROT FOKUSU: pole filtrowania z tym samym tekstem i karetka.
        if (!ReferenceEquals(Keyboard.FocusedElement, filter))
        {
            throw new Exception(
                "Po zamknieciu wyboru celu fokus NIE wrocil do pola filtrowania, a do "
                + (Keyboard.FocusedElement?.GetType().Name ?? "brak") + ".");
        }
        if (filter.Text != "Sal" || filter.CaretIndex != 2)
        {
            throw new Exception(
                $"Powrot do pola filtrowania zmienil tekst albo karetke: \"{filter.Text}\" / {filter.CaretIndex}.");
        }
        if (harness.CurrentView != viewBefore
            || harness.MediaList.SelectedIndex != selectedBefore
            || harness.Window.SonosSelectedGroupId != groupBefore)
        {
            throw new Exception("Ctrl+F5 z pola filtrowania zmienil widok, zaznaczenie albo wybrana grupe.");
        }
        return 9;
    }

    /// <summary>
    /// ZAMKNIJ okno, GDY naprawde sie pokaze. Zegar na "mniej wiecej teraz"
    /// zostawialby modal na pulpicie, gdyby pokazanie sie opoznilo.
    /// </summary>
    private static void CloseWhenShown(Window window)
    {
        void OnLoaded(object? sender, RoutedEventArgs args)
        {
            window.Loaded -= OnLoaded;
            window.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                if (window.IsVisible) window.Close();
            }));
        }

        window.Loaded += OnLoaded;
    }

    // ===== U3c: Ctrl+F5 z ODTWARZACZA =====

    /// <summary>
    /// ZGLOSZENIE (zywy NVDA, 657): Ctrl+F5 nie otwieralo konta, gdy fokus byl na
    /// przycisku odtwarzania. Odtwarzacz otwieramy PRODUKCYJNA droga (Enter na
    /// wierszu grupy -> ActivateSonosGroupThenShowPlayer) i stawiamy fokus na
    /// RZECZYWISTYM PlayerPlayPauseButton, nie ustawiamy prywatnej flagi.
    /// </summary>
    private static int MeasureCtrlF5FromPlayerOpensTargetWindow()
    {
        using var harness = Harness.Create();
        harness.EnterSonosSession();
        harness.ShowOwnWindow();
        harness.SelectRow(0);

        // PRODUKCYJNE wejscie w odtwarzacz z listy, tak jak Enter uzytkownika.
        harness.Pump(harness.Window.ActivateSonosGroupThenShowPlayerForTests("GRUPA-SALON"));
        harness.PumpQuietly(TimeSpan.FromMilliseconds(150));
        var playButton = harness.PlayerPlayPauseButton;
        if (!playButton.IsVisible)
        {
            throw new Exception("Produkcyjne wejscie z listy nie pokazalo odtwarzacza Sonos.");
        }
        playButton.Focus();
        Keyboard.Focus(playButton);
        harness.PumpQuietly(TimeSpan.FromMilliseconds(80));
        if (!ReferenceEquals(Keyboard.FocusedElement, playButton))
        {
            throw new Exception(
                "Pomiar nie postawil fokusu na prawdziwym przycisku odtwarzania: "
                + (Keyboard.FocusedElement?.GetType().Name ?? "brak") + ".");
        }

        // Ctrl+F5 prowadzi teraz do WYBORU CELU. Fokus musi wrocic na przycisk
        // odtwarzacza tak samo, jak wracal z okna konta.
        var shown = 0;
        harness.Window.PresentSonosTargetOverrideForTests = targetWindow => shown++;

        var groupBefore = harness.Window.SonosSelectedGroupId;
        var commandsBefore = harness.Backend.Commands.Count;
        var contentBefore = playButton.Content as string;
        harness.Announcements.Clear();
        harness.AssertAccountBoundariesAreSynthetic();
        harness.PressCtrl(Key.F5);
        harness.PumpQuietly(TimeSpan.FromMilliseconds(250));

        if (shown != 1)
        {
            throw new Exception(
                $"Ctrl+F5 w odtwarzaczu Sonos nie otworzylo wyboru celu (otwarc: {shown}; "
                + "komunikaty: " + string.Join(" | ", harness.Announcements) + ").");
        }
        if (harness.AccountStore.Writes != 0 || harness.AccountStore.Deletes != 0)
        {
            throw new Exception("Ctrl+F5 z odtwarzacza zapisal albo skasowal poswiadczenia Sonos.");
        }
        if (harness.Gateway.Calls != 0 || harness.AccountOwner.ControlApiCreations != 0)
        {
            throw new Exception("Ctrl+F5 z odtwarzacza ruszyl logowanie albo Control API Sonos.");
        }
        if (harness.Backend.Commands.Count != commandsBefore)
        {
            throw new Exception("Ctrl+F5 z odtwarzacza wyslal polecenie sterujace do Sonosa.");
        }

        // POWROT FOKUSU: ten sam przycisk odtwarzacza i ten sam stan.
        if (!ReferenceEquals(Keyboard.FocusedElement, playButton))
        {
            throw new Exception(
                "Po zamknieciu okna konta fokus NIE wrocil na przycisk odtwarzania, a do "
                + (Keyboard.FocusedElement?.GetType().Name ?? "brak") + ".");
        }
        if ((playButton.Content as string) != contentBefore
            || harness.Window.SonosSelectedGroupId != groupBefore)
        {
            throw new Exception("Ctrl+F5 z odtwarzacza zmienil stan przycisku albo wybrana grupe.");
        }
        return 8;
    }

    // ===== U4: krotki wiersz grupy (CEL STEROWANIA, nie korzen tresci) =====

    /// <summary>
    /// Krotka nazwa grupy. Wiersze grup NIE SA juz na widocznej liscie korzenia
    /// (ta pokazuje tresc), wiec mierzymy MODEL sterowania: Name, czyli dokladnie
    /// to, co ApplySonosGroupRows wklada w MediaItem.Title celu sterowania.
    /// Dlugi Text z liczba glosnikow nalezy do okna Ctrl+F5 i ma tam zostac.
    /// </summary>
    private static int MeasureShortGroupRowLabel()
    {
        using var harness = Harness.Create();
        harness.EnterSonosSession();

        var labels = harness.Window.SonosGroupRows.Select(row => row.Name).ToList();
        var salon = labels.FirstOrDefault(label => label.Contains("Salon", StringComparison.Ordinal))
            ?? throw new Exception("Brak wiersza grupy Salon: " + string.Join(" | ", labels));

        var occurrences = salon.Split("Salon", StringSplitOptions.None).Length - 1;
        if (occurrences > 1)
        {
            throw new Exception($"Wiersz grupy POWTARZA nazwę {occurrences} razy: \"{salon}\".");
        }
        if (salon.Contains("głośników:", StringComparison.OrdinalIgnoreCase))
        {
            throw new Exception($"Wiersz grupy niesie techniczne liczby zamiast krótkiej nazwy: \"{salon}\".");
        }
        if (salon.Length > 40)
        {
            throw new Exception($"Wiersz grupy jest długi ({salon.Length} znaków): \"{salon}\".");
        }
        return 3;
    }

    // ===== U5: powrot do sesji Sonos wraca do MATERIALU, nie do glosnikow =====

    /// <summary>
    /// ZGLOSZENIE MICHALA (doslownie): "po przelaczeniu i powrocie do sesji byla
    /// biblioteka albo otwarty element a nie grupa glosnikow i inne rzeczy
    /// techniczne" oraz "wymaga klikania glosnika by aktywowac".
    ///
    /// MIERZYMY DWA PRZEJSCIA przez PRAWDZIWE polecenia slotow sesji:
    ///   (a) Sonos w BIBLIOTECE -> inna sesja -> Sonos  = znow biblioteka,
    ///   (b) Sonos w OTWARTYM elemencie -> inna sesja -> Sonos = znow ten element.
    /// W obu wypadkach wybrany CEL (grupa) musi przezyc bez ponownego Enter,
    /// a lista glosnikow NIE MA prawa sie pokazac - te sa tylko pod Ctrl+F5.
    /// </summary>
    private static int MeasureReturnToSonosRestoresMaterialNotSpeakers()
    {
        using var harness = Harness.Create();
        harness.EnterSonosSession();
        harness.ShowOwnWindow();

        var spotifySlot = harness.Window.SessionsForTests.SessionSlots
            .First(pair => !string.Equals(pair.Value, "sonos", StringComparison.Ordinal)).Key;
        var sonosSlot = harness.Window.SessionsForTests.SessionSlots
            .First(pair => string.Equals(pair.Value, "sonos", StringComparison.Ordinal)).Key;

        // CEL STEROWANIA wybrany PRODUKCYJNA droga (Enter na grupie). To jest
        // wlasnie ten wybor, ktory po powrocie nie moze wymagac drugiego Enter.
        harness.Pump(harness.Window.ActivateSonosGroupForTests("GRUPA-SALON"));
        harness.PumpQuietly(TimeSpan.FromMilliseconds(150));

        // --- (a) OPUSZCZAMY sesje z widoku BIBLIOTEKI ---
        // PRODUKCYJNA droga: Ctrl+L. Widok i stan nawigacji sa prawdziwe;
        // podstawione jest WYLACZNIE pokazanie modala (pomiar bez pulpitu).
        harness.Window.PresentSonosLibraryOverrideForTests = _ => { };
        harness.ExecuteCommand(CommandIds.ViewLibrary);
        harness.PumpQuietly(TimeSpan.FromMilliseconds(150));
        var libraryView = harness.CurrentView;
        var libraryLabels = harness.RowLabels();

        // DYSKRYMINUJACA ASERCJA ZGLOSZENIA: widoczna lista korzenia to TRESC.
        // Bez zmiany korzenia lista mialaby tu wiersze "Salon"/"Biuro" z
        // Session.Items i ten warunek by padl.
        if (libraryLabels.Any(label => label.Contains("Salon", StringComparison.Ordinal)
            || label.Contains("Biuro", StringComparison.Ordinal)))
        {
            throw new Exception(
                "Biblioteka Sonos pokazuje GLOSNIKI zamiast tresci: " + string.Join(" | ", libraryLabels));
        }
        foreach (var expected in new[] { "Ulubione Sonos", "Moje stacje" })
        {
            if (!libraryLabels.Any(label => label.Contains(expected, StringComparison.Ordinal)))
            {
                throw new Exception(
                    $"Biblioteka Sonos nie ma realnego wejscia \"{expected}\": "
                    + string.Join(" | ", libraryLabels));
            }
        }

        // ZAZNACZENIE I FOKUS na KONKRETNYM elemencie tresci - to ten element ma
        // przezyc powrot. Bierzemy "Moje stacje", zeby pomiar nie przeszedl
        // przypadkiem na pierwszym wierszu.
        var ownStationsIndex = libraryLabels.FindIndex(label =>
            label.Contains("Moje stacje", StringComparison.Ordinal));
        harness.SelectRow(ownStationsIndex);
        harness.FocusSelectedRow();
        var selectedIdBefore = harness.SelectedRowId;
        var groupBefore = harness.Window.SonosSelectedGroupId;
        if (string.IsNullOrEmpty(groupBefore))
        {
            throw new Exception("Pomiar nie ma wybranego celu Sonos przed opuszczeniem sesji.");
        }

        harness.ExecuteCommand(CommandIds.SessionSlot(spotifySlot));
        harness.PumpQuietly(TimeSpan.FromMilliseconds(150));
        harness.Announcements.Clear();
        harness.ExecuteCommand(CommandIds.SessionSlot(sonosSlot));
        harness.PumpQuietly(TimeSpan.FromMilliseconds(250));

        var afterLabels = harness.RowLabels();
        if (harness.CurrentView != libraryView)
        {
            throw new Exception(
                $"Powrot do Sonosa NIE wrocil do biblioteki: widok \"{libraryView}\" -> \"{harness.CurrentView}\".");
        }
        // TRESC PO POWROCIE: lista znow pokazuje kategorie materialu, nie glosniki.
        if (afterLabels.Any(label => label.Contains("Salon", StringComparison.Ordinal)
            || label.Contains("Biuro", StringComparison.Ordinal)))
        {
            throw new Exception(
                "Powrot do Sonosa dal LISTE GLOSNIKOW zamiast tresci: " + string.Join(" | ", afterLabels));
        }
        if (!afterLabels.SequenceEqual(libraryLabels, StringComparer.Ordinal))
        {
            throw new Exception(
                "Powrot do Sonosa przestawil tresc korzenia: \""
                + string.Join(" | ", afterLabels) + "\" zamiast \"" + string.Join(" | ", libraryLabels) + "\".");
        }
        // ZACHOWANY ELEMENT I FOKUS: ten sam wiersz tresci, fokus na liscie.
        if (harness.SelectedRowId != selectedIdBefore)
        {
            throw new Exception(
                "Powrot do Sonosa zgubil zaznaczony element tresci: \"" + (selectedIdBefore ?? "brak")
                + "\" -> \"" + (harness.SelectedRowId ?? "brak") + "\".");
        }
        if (!harness.IsListFocused)
        {
            throw new Exception(
                "Powrot do Sonosa nie zostawil fokusu na liscie tresci, a na "
                + (Keyboard.FocusedElement?.GetType().Name ?? "brak") + ".");
        }
        if (harness.Window.SonosSelectedGroupId != groupBefore)
        {
            throw new Exception(
                "Powrot do Sonosa ZGUBIL wybrany cel: \"" + groupBefore
                + "\" -> \"" + (harness.Window.SonosSelectedGroupId ?? "brak") + "\".");
        }
        AssertNoSpeakerListOnReturn(harness, "biblioteki");

        // --- (a2) SPOZNIONY ODCZYT TOPOLOGII W ODTWORZONYM WIDOKU ---
        // TERAZ JEST DYSKRYMINUJACY: ApplySonosGroupRows robi ReplaceItems na
        // Session.Items i konczy RefreshCurrentView. Dopoki korzen brał tresc z
        // Session.Items, kazdy odczyt topologii wracal GLOSNIKAMI na liste. Ta
        // asercja pilnuje ZAWARTOSCI, a nie tylko nazwy widoku.
        var viewBeforeLateRead = harness.CurrentView;
        harness.Window.ApplySonosGroupRowsForTests();
        harness.PumpQuietly(TimeSpan.FromMilliseconds(150));
        if (harness.CurrentView != viewBeforeLateRead)
        {
            throw new Exception(
                "SPOZNIONY odczyt topologii PRZESTAWIL odtworzony widok uzytkownika: \""
                + viewBeforeLateRead + "\" -> \"" + harness.CurrentView + "\".");
        }
        var afterLateRead = harness.RowLabels();
        if (!afterLateRead.SequenceEqual(libraryLabels, StringComparer.Ordinal))
        {
            throw new Exception(
                "SPOZNIONY odczyt topologii WROCIL GLOSNIKAMI na liste tresci: "
                + string.Join(" | ", afterLateRead));
        }
        if (harness.SelectedRowId != selectedIdBefore)
        {
            throw new Exception("Spozniony odczyt topologii przestawil zaznaczony element tresci.");
        }
        if (harness.Window.SonosSelectedGroupId != groupBefore)
        {
            throw new Exception("Spozniony odczyt topologii zgubil wybrany cel Sonos.");
        }

        // --- (a3) ENTER NA KATEGORII WCHODZI W REALNA LISTE ---
        // Korzen bez wejscia bylby atrapa: Enter na "Moje stacje" musi otworzyc
        // ISTNIEJACA droge kategorii, a nie przestawic nazwe widoku ani wybrac
        // celu sterowania.
        var ownStreamWindows = 0;
        harness.Window.PresentSonosOwnStreamsOverrideForTests = _ => ownStreamWindows++;
        var targetWindowsBeforeEnter = harness.Window.SonosTargetWindowsCreatedForTests;
        harness.ExecuteCommand(CommandIds.ActivateSelected);
        harness.PumpQuietly(TimeSpan.FromMilliseconds(200));
        if (ownStreamWindows != 1)
        {
            throw new Exception(
                $"Enter na kategorii \"Moje stacje\" nie otworzyl jej listy (okien: {ownStreamWindows}).");
        }
        if (harness.Window.SonosTargetWindowsCreatedForTests != targetWindowsBeforeEnter)
        {
            throw new Exception("Enter na kategorii tresci otworzyl wybor celu sterowania.");
        }
        if (harness.Backend.Commands.Count != 0)
        {
            throw new Exception("Enter na kategorii tresci wyslal polecenie sterujace do Sonosa.");
        }

        // --- (b) OPUSZCZAMY sesje z OTWARTEGO elementu (odtwarzacz) ---
        harness.Pump(harness.Window.ActivateSonosGroupThenShowPlayerForTests("GRUPA-SALON"));
        harness.PumpQuietly(TimeSpan.FromMilliseconds(200));
        if (!harness.PlayerPlayPauseButton.IsVisible)
        {
            throw new Exception("Pomiar nie otworzyl odtwarzacza Sonos przed opuszczeniem sesji.");
        }
        var playerView = harness.CurrentView;

        harness.ExecuteCommand(CommandIds.SessionSlot(spotifySlot));
        harness.PumpQuietly(TimeSpan.FromMilliseconds(150));
        harness.Announcements.Clear();
        harness.ExecuteCommand(CommandIds.SessionSlot(sonosSlot));
        harness.PumpQuietly(TimeSpan.FromMilliseconds(250));

        if (harness.CurrentView != playerView)
        {
            throw new Exception(
                $"Powrot do Sonosa NIE wrocil do otwartego elementu: \"{playerView}\" -> \"{harness.CurrentView}\".");
        }
        if (!harness.PlayerPlayPauseButton.IsVisible)
        {
            throw new Exception("Powrot do Sonosa zgubil otwarty odtwarzacz: przycisk odtwarzania niewidoczny.");
        }
        if (harness.Window.SonosSelectedGroupId != groupBefore)
        {
            throw new Exception("Powrot do otwartego elementu zgubil wybrany cel Sonos.");
        }
        AssertNoSpeakerListOnReturn(harness, "otwartego elementu");

        // ZADEN powrot nie ma prawa ruszyc konta ani Control API.
        if (harness.Gateway.Calls != 0 || harness.AccountOwner.ControlApiCreations != 0)
        {
            throw new Exception("Powrot do sesji Sonos ruszyl logowanie albo Control API.");
        }
        return 12;
    }

    /// <summary>
    /// Lista GLOSNIKOW/GRUP jest CELEM STEROWANIA i zyje w Session.Items, ale
    /// PO ZWYKLYM POWROCIE do sesji nie ma prawa stac sie WIDOKIEM, ani byc
    /// oglaszana technikaliami. Jawne Ctrl+F5 to inna, osobna droga.
    /// </summary>
    private static void AssertNoSpeakerListOnReturn(Harness harness, string skad)
    {
        if (harness.Window.SonosTargetWindowsCreatedForTests != 0)
        {
            throw new Exception($"Powrot z {skad} otworzyl okno wyboru celu BEZ Ctrl+F5.");
        }

        var technical = harness.Announcements.FirstOrDefault(message =>
            message.Contains("głośnik", StringComparison.OrdinalIgnoreCase)
            || message.Contains("grupa głośników", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Control F5", StringComparison.OrdinalIgnoreCase));
        if (technical is not null)
        {
            throw new Exception(
                $"Powrot z {skad} mowi technikaliami o glosnikach: \"{technical}\".");
        }
    }

    /// <summary>
    /// ZGLOSZENIE MICHALA (doslowne cytaty): "Biblioteka Sonos nie została
    /// otwarta, bo okno AMC nie jest aktywne. Wróć do AMC i ponów otwarcie
    /// Biblioteki" oraz to samo dla wyboru celu i Control F5 - mimo ze AMC BYLO
    /// na wierzchu.
    ///
    /// PRZYCZYNA: brama okien potomnych pytala o <c>Window.IsActive</c> SAMEGO
    /// okna glownego. W lancuchu dialogow (nasze okno potomne zamyka sie i kaze
    /// wlascicielowi otworzyc kolejne) WPF jeszcze nie oddal aktywacji
    /// wlascicielowi, wiec warunek byl falszywie NEGATYWNY.
    ///
    /// MIERZYMY OBIE STRONY na PRAWDZIWEJ bramie:
    ///   * NASZ proces na pierwszym planie, okno glowne NIEaktywne = WOLNO,
    ///   * OBCY proces na pierwszym planie = NADAL ODMOWA (ochrona zostaje).
    /// </summary>
    private static int MeasureOwnForegroundDoesNotBlockChildWindows()
    {
        using var harness = Harness.Create();
        harness.EnterSonosSession();
        harness.ShowOwnWindow();

        var shown = 0;
        harness.Window.PresentSonosLibraryOverrideForTests = _ => shown++;

        // --- NASZ pierwszy plan, ale okno glowne NIE jest aktywne ---
        // Dokladnie stan lancucha dialogow. Deaktywacje wymuszamy przez oddanie
        // aktywacji WLASNEMU drugiemu oknu, nie przez prywatna flage.
        harness.DeactivateMainWindowWithinOwnProcess();
        if (harness.Window.IsActive)
        {
            throw new Exception("Pomiar nie zdjal aktywacji z okna glownego - nie mierzy zglaszanego stanu.");
        }
        harness.Window.ForegroundProcessIdOverrideForTests = Environment.ProcessId;

        harness.Announcements.Clear();
        harness.Window.ShowSonosLibraryForTests();
        harness.PumpQuietly(TimeSpan.FromMilliseconds(150));

        if (shown != 1)
        {
            throw new Exception(
                "Brama ODMOWILA otwarcia Biblioteki, choc pierwszy plan nalezy do NASZEGO procesu "
                + "(komunikaty: " + string.Join(" | ", harness.Announcements) + ").");
        }
        var falseRefusal = harness.Announcements.FirstOrDefault(message =>
            message.Contains("nie jest aktywne", StringComparison.OrdinalIgnoreCase));
        if (falseRefusal is not null)
        {
            throw new Exception($"Brama powiedziala FALSZYWA odmowe: \"{falseRefusal}\".");
        }

        // --- OBCY pierwszy plan: ochrona MUSI zostac ---
        // Nie kradniemy fokusu zadnemu prawdziwemu oknu pulpitu: podstawiamy
        // CUDZY PID, zeby zmierzyc te sama brame od drugiej strony.
        shown = 0;
        harness.Window.ForegroundProcessIdOverrideForTests = Environment.ProcessId + 1;
        harness.Announcements.Clear();
        harness.Window.ShowSonosLibraryForTests();
        harness.PumpQuietly(TimeSpan.FromMilliseconds(150));

        if (shown != 0)
        {
            throw new Exception(
                "Brama otworzyla modal, choc na pierwszym planie jest OBCA aplikacja - "
                + "ochrona obcego pierwszego planu zostala usunieta.");
        }
        var honestRefusal = harness.Announcements.FirstOrDefault(message =>
            message.Contains("nie jest aktywne", StringComparison.OrdinalIgnoreCase));
        if (honestRefusal is null)
        {
            throw new Exception(
                "Przy OBCYM pierwszym planie brama nie powiedziala, dlaczego nie otworzyla okna "
                + "(komunikaty: " + string.Join(" | ", harness.Announcements) + ").");
        }

        // --- NIEZNANY pierwszy plan: NIE jest potwierdzeniem "to my" ---
        // Brak HWND / PID zero / wyjatek z Win32 to BRAK WIEDZY. Poprzednia wersja
        // bramy zwracala wtedy PRAWDE ("awaria odczytu = aktywni"), wiec modal
        // mogl wyskoczyc pod reka uzytkownika pracujacego w obcej aplikacji.
        // PID zero jest dokladnie tym nieznanym stanem (zaden proces go nie ma).
        shown = 0;
        harness.Window.ForegroundProcessIdOverrideForTests = 0;
        harness.Announcements.Clear();
        harness.Window.ShowSonosLibraryForTests();
        harness.PumpQuietly(TimeSpan.FromMilliseconds(150));

        if (shown != 0)
        {
            throw new Exception(
                "Brama otworzyla modal, choc pierwszy plan jest NIEZNANY - nieznane "
                + "zostalo potraktowane jak wlasny proces.");
        }

        harness.Window.ForegroundProcessIdOverrideForTests = null;
        harness.CloseSiblingWindow();
        return 8;
    }

    /// <summary>
    /// ZGLOSZENIE MICHALA: "Ulubione stacje graja, ale F2 nie edytuje i Delete nie
    /// usuwa". Mierzymy OBA klawisze na PRAWDZIWYM oknie Moich stacji: F2 ma
    /// wejsc w edytor, Delete ma wejsc w potwierdzenie usuniecia. Potwierdzenie i
    /// edytor sa modalne, wiec pomiar tylko SPRAWDZA, ze okno sie pojawilo,
    /// i je zamyka - nie przeklikuje zapisu.
    /// </summary>
    private static int MeasureOwnStationsRespondToF2AndDelete()
    {
        var checks = 0;
        var saved = new List<IReadOnlyList<SonosOwnStreamSettings>>();
        var stations = new[]
        {
            new SonosOwnStreamSettings { Id = "s1", Name = "Stacja pierwsza", StreamUrl = "https://example.invalid/1" },
            new SonosOwnStreamSettings { Id = "s2", Name = "Stacja druga", StreamUrl = "https://example.invalid/2" }
        };

        var window = new SonosOwnStreamsWindow(stations, "Salon", rows => saved.Add(rows), play: null)
        {
            ShowInTaskbar = false
        };
        window.Show();
        Pump(TimeSpan.FromMilliseconds(250));

        var list = (ListBox)window.FindName("StationsList")!;
        list.SelectedIndex = 0;
        list.UpdateLayout();
        if (list.ItemContainerGenerator.ContainerFromIndex(0) is ListBoxItem row)
        {
            row.Focus();
            Keyboard.Focus(row);
        }
        Pump(TimeSpan.FromMilliseconds(150));
        if (!list.IsKeyboardFocusWithin)
        {
            throw new Exception("Pomiar nie ustawil fokusu na liscie wlasnych stacji.");
        }
        checks++;

        // Modalne okna (edytor, potwierdzenie) zamykamy, jak tylko sie pokaza.
        var seenModals = new List<string>();
        var watchdog = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
        watchdog.Tick += (_, _) =>
        {
            // W hoscie pomiarowym NIE MA Application.Current, a potwierdzenie
            // usuniecia idzie przez AccessibleDialog BEZ wlasciciela - wiec ani
            // Application.Current.Windows, ani OwnedWindows nie wystarcza.
            // Patrzymy na WSZYSTKIE zrodla prezentacji tego watku STA.
            foreach (var source in PresentationSource.CurrentSources.OfType<HwndSource>().ToList())
            {
                if (source.RootVisual is not Window other) continue;
                if (ReferenceEquals(other, window) || !other.IsVisible) continue;
                seenModals.Add(other.GetType().Name);
                other.Close();
            }
        };
        watchdog.Start();

        // Klawisz wysylamy PRZEZ KOLEJKE: oba handlery otwieraja MODAL (ShowDialog /
        // AccessibleDialog), ktory kreci wlasna petle komunikatow. Wywolany wprost
        // zablokowalby ten watek, a watchdog nigdy by nie tyknal.
        window.Dispatcher.BeginInvoke(new Action(() => SendKey(window, list, Key.F2)));
        Pump(TimeSpan.FromMilliseconds(1200));
        if (!seenModals.Any(name => name.Contains("RadioStation", StringComparison.Ordinal)))
        {
            throw new Exception(
                "F2 na liscie wlasnych stacji NIE otworzylo edytora stacji (zobaczone okna: "
                + (seenModals.Count == 0 ? "zadnego" : string.Join(", ", seenModals)) + ").");
        }
        checks++;

        seenModals.Clear();
        window.Dispatcher.BeginInvoke(new Action(() => SendKey(window, list, Key.Delete)));
        Pump(TimeSpan.FromMilliseconds(1200));
        if (seenModals.Count == 0)
        {
            throw new Exception("Delete na liscie wlasnych stacji NIE otworzylo potwierdzenia usuniecia.");
        }
        checks++;

        watchdog.Stop();
        window.Close();
        Pump(TimeSpan.FromMilliseconds(150));
        return checks;
    }

    private static void SendKey(Window window, IInputElement target, Key key)
    {
        var source = PresentationSource.FromVisual(window)!;
        var args = new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, key)
        {
            RoutedEvent = Keyboard.PreviewKeyDownEvent
        };
        target.RaiseEvent(args);
    }

    private static void Pump(TimeSpan duration)
    {
        // Pompujemy PRAWDZIWIE: gdy modal kreci wlasna petle, Dispatcher.Invoke z
        // tego watku nie wroci. DoEvents przez zagniezdzona ramke przepuszcza
        // zarowno nasze BeginInvoke, jak i tyknięcia watchdoga zamykajacego modal.
        var deadline = DateTime.UtcNow + duration;
        while (DateTime.UtcNow < deadline)
        {
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(
                DispatcherPriority.Background,
                new Action(() => frame.Continue = false));
            Dispatcher.PushFrame(frame);
            Thread.Sleep(10);
        }
    }

    // ===== aparatura =====

    private sealed class Harness : IDisposable
    {
        private static readonly TimeSpan Limit = TimeSpan.FromSeconds(15);

        private readonly string _directory;
        private readonly Dispatcher _dispatcher;

        private Harness(string directory, MainWindow window, FakeBackend backend, List<string> announcements,
            SonosAccountOwner accountOwner, PamieciowyMagazynKonta accountStore,
            NieuzywanaBramkaLogowania gateway)
        {
            _directory = directory;
            _dispatcher = Dispatcher.CurrentDispatcher;
            Window = window;
            Backend = backend;
            Announcements = announcements;
            AccountOwner = accountOwner;
            AccountStore = accountStore;
            Gateway = gateway;
        }

        internal MainWindow Window { get; }

        internal FakeBackend Backend { get; }

        internal List<string> Announcements { get; }

        /// <summary>TEN SAM wlasciciel konta, ktorego uzywa prawdziwe okno.</summary>
        internal SonosAccountOwner AccountOwner { get; }

        /// <summary>Syntetyczny magazyn w PAMIECI - kwit, ze DPAPI nie bylo czytane.</summary>
        internal PamieciowyMagazynKonta AccountStore { get; }

        internal NieuzywanaBramkaLogowania Gateway { get; }

        /// <summary>
        /// ASERCJA PRZED JAKIMKOLWIEK I/O: oba punkty podstawienia wlasciciela
        /// konta MUSZA byc zastapione, a konto nie moze byc jeszcze obudzone.
        /// Fixture bez tego prowadzilby RestoreOnce do PRAWDZIWEGO magazynu
        /// DPAPI uzytkownika; ta asercja pada BEZ dotkniecia dysku.
        /// </summary>
        internal void AssertAccountBoundariesAreSynthetic()
        {
            if (AccountOwner.StoreFactory is null)
            {
                throw new Exception(
                    "Fixture NIE odciął magazynu konta: StoreFactory właściciela jest pusty, więc "
                    + "produkcyjne RestoreOnce poszłoby do domyślnego SonosDpapiCredentialStore.");
            }
            if (AccountOwner.GatewayFactory is null)
            {
                throw new Exception(
                    "Fixture NIE odciął transportu logowania: GatewayFactory właściciela jest pusty.");
            }
            if (AccountOwner.HasCoordinator)
            {
                throw new Exception("Konto Sonos zostało obudzone przed mierzonym Ctrl+F5.");
            }
            if (AccountStore.Reads != 0 || AccountStore.Writes != 0 || AccountStore.Deletes != 0)
            {
                throw new Exception("Syntetyczny magazyn był już użyty przed mierzonym Ctrl+F5.");
            }
        }

        internal ListBox MediaList => (ListBox)Window.FindName("MediaList")!;

        internal TextBox FilterBox => (TextBox)Window.FindName("FilterBox")!;

        internal Button PlayerPlayPauseButton => (Button)Window.FindName("PlayerPlayPauseButton")!;

        /// <summary>
        /// RZECZYWISTY fokus klawiatury w polu filtrowania: to samo, co zrobi
        /// uzytkownik Tabem. Bez tego KeyEventArgs nie mierzylby kontekstu pola.
        /// </summary>
        internal void FocusFilterBox()
        {
            var filter = FilterBox;
            filter.Focus();
            Keyboard.Focus(filter);
            PumpQuietly(TimeSpan.FromMilliseconds(80));
        }

        /// <summary>
        /// PRAWDZIWY modal bez zawieszenia pomiaru: zegar czeka na okno konta
        /// otwarte produkcyjnym ShowDialog, odnotowuje ISTNIEJACY przycisk
        /// Glosniki i grupy i zamyka je. Zadnego kliku w logowanie, odnowienie
        /// ani rozlaczenie - tylko zamkniecie okna.
        /// </summary>
        internal AccountWindowAutoClose StartAccountWindowAutoClose(SonosAccountPresenter presenter) =>
            new(presenter, _dispatcher);

        internal sealed class AccountWindowAutoClose : IDisposable
        {
            private readonly DispatcherTimer _timer;

            internal AccountWindowAutoClose(SonosAccountPresenter presenter, Dispatcher dispatcher)
            {
                _timer = new DispatcherTimer(DispatcherPriority.Normal, dispatcher)
                {
                    Interval = TimeSpan.FromMilliseconds(30)
                };
                _timer.Tick += (_, _) =>
                {
                    if (presenter.OpenWindow is not { } window) return;
                    if (!window.IsLoaded) return;
                    SawDevicesButton |= window.FindName("DevicesButton") is Button;
                    Closed++;
                    window.Close();
                };
                _timer.Start();
            }

            internal bool SawDevicesButton { get; private set; }

            internal int Closed { get; private set; }

            public void Dispose() => _timer.Stop();
        }

        internal string CurrentView => (string)Field("_currentView")!;

        internal AccessibleMediaController.Core.Sessions.DemoMediaSession SonosSession =>
            Window.SessionsForTests.FindSession("sonos")
            ?? throw new Exception("Nie ma sesji Sonos w prawdziwym oknie.");

        internal static Harness Create()
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));

            var directory = Path.Combine(Path.GetTempPath(), "amc-sonos-nav-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var store = new ConfigurationStore(Path.Combine(directory, "settings.json"));
            var state = store.LoadOrCreate();
            state.Podcasts.Subscriptions.Clear();
            state.Podcasts.Episodes.Clear();
            state.WiiM.Devices.Clear();
            state.Radio.RecordingSchedules.Clear();
            state.Settings.Updates.CheckAutomatically = false;
            state.Settings.Updates.InstallOnExit = false;

            var backend = new FakeBackend();
            var announcements = new List<string>();
            var window = new MainWindow(state, store)
            {
                SuppressDesktopIntegrationForTests = true,
                SonosBackendOverride = backend,
                AnnouncementSinkForTests = announcements.Add
            };
            window.DenyApplicationUpdateStartForTests();

            // ODCIECIE PRAWDZIWEGO MAGAZYNU KONTA. Ctrl+F5 prowadzi produkcyjne
            // ShowSonosAccountManager -> EnsureCoordinator -> RestoreOnce, ktore
            // BEZ tego poszloby do domyslnego SonosDpapiCredentialStore na
            // prawdziwej sciezce uzytkownika. Bierzemy TEGO SAMEGO wlasciciela,
            // ktorego uzywa prawdziwe okno (wzor: SonosSessionAccountUiTests),
            // i podstawiamy magazyn w PAMIECI oraz bramke, ktora ma nie ruszyc.
            var owner = (SonosAccountOwner)(window.GetType().GetField("_sonosAccount", Instance)
                ?? throw new Exception("Nie ma pola _sonosAccount w prawdziwym MainWindow."))
                .GetValue(window)!;
            if (owner.HasCoordinator)
            {
                throw new Exception("Konstrukcja okna zainicjowała konto Sonos przed jawnym wejściem.");
            }

            var accountStore = new PamieciowyMagazynKonta();
            var gateway = new NieuzywanaBramkaLogowania();
            owner.StoreFactory = _ => accountStore;
            owner.GatewayFactory = _ => gateway;
            // Gdyby ktorakolwiek sciezka tego pomiaru dotknela Control API,
            // wartownik ma to ZATRZYMAC zamiast wypuscic prawdziwy HTTP.
            owner.ControlApiConfigurationFactory = () =>
                throw new Exception(
                    "Pomiar układu nawigacji nie ma prawa tworzyć klienta Control API Sonos.");

            return new Harness(directory, window, backend, announcements, owner, accountStore, gateway);
        }

        internal void EnterSonosSession()
        {
            var slot = Window.SessionsForTests.SessionSlots
                .First(pair => string.Equals(pair.Value, "sonos", StringComparison.Ordinal)).Key;
            ExecuteCommand(CommandIds.SessionSlot(slot));
            PumpUntil(() => Window.SonosGroupRows.Count == 2, "wejście do sesji Sonos nie odczytało grup");
            PumpQuietly(TimeSpan.FromMilliseconds(120));
        }

        internal void ShowOwnWindow()
        {
            var rendered = (EventHandler)Delegate.CreateDelegate(
                typeof(EventHandler),
                Window,
                Window.GetType().GetMethod("Window_ContentRendered", Instance)!);
            Window.ContentRendered -= rendered;
            Window.ShowInTaskbar = false;
            Window.Show();
            PumpUntil(() => Window.IsLoaded && PresentationSource.FromVisual(Window) is not null,
                "własne okno się nie pokazało");
            MediaList.Focus();
            PumpQuietly(TimeSpan.FromMilliseconds(100));
        }

        internal List<string> RowLabels() => MediaList.Items.Cast<object>()
            .Select(row => row.GetType().GetProperty("Label", Instance)?.GetValue(row) as string ?? string.Empty)
            .ToList();

        /// <summary>IDENTYFIKATOR zaznaczonego wiersza - zachowanie elementu po powrocie.</summary>
        internal string? SelectedRowId =>
            (MediaList.SelectedItem?.GetType().GetProperty("Item", Instance)?.GetValue(MediaList.SelectedItem)
                as AccessibleMediaController.Core.Sessions.MediaItem)?.Id;

        /// <summary>Daje fokus KONTENEROWI zaznaczonego wiersza, jak Tab uzytkownika.</summary>
        internal void FocusSelectedRow()
        {
            var list = MediaList;
            list.UpdateLayout();
            if (list.ItemContainerGenerator.ContainerFromIndex(list.SelectedIndex) is ListBoxItem container)
                container.Focus();
            else list.Focus();
            PumpQuietly(TimeSpan.FromMilliseconds(80));
        }

        /// <summary>Czy fokus klawiatury jest na LISCIE (sama lista albo jej wiersz).</summary>
        internal bool IsListFocused => Keyboard.FocusedElement switch
        {
            ListBox box => ReferenceEquals(box, MediaList),
            ListBoxItem item => ItemsControl.ItemsControlFromItemContainer(item) is ListBox owner
                && ReferenceEquals(owner, MediaList),
            _ => false
        };

        /// <summary>
        /// Zdejmuje aktywacje z okna glownego BEZ oddawania pierwszego planu obcej
        /// aplikacji: aktywujemy WLASNE drugie okno. Dokladnie tak wyglada lancuch
        /// dialogow Sonos, w ktorym brama falszywie odmawiala.
        /// </summary>
        internal void DeactivateMainWindowWithinOwnProcess()
        {
            _siblingWindow = new Window
            {
                Width = 120,
                Height = 90,
                ShowInTaskbar = false,
                Title = "AMC pomiar - wlasne okno pomocnicze"
                // BEZ Owner: brama okien potomnych odmawia, gdy jakies WLASNE okno
                // POTOMNE jest widoczne (i slusznie). Mierzymy luke aktywacji, a nie
                // ten warunek, wiec okno pomocnicze jest osobnym oknem najwyzszego
                // poziomu TEGO SAMEGO procesu.
            };
            _siblingWindow.Show();
            _siblingWindow.Activate();
            PumpQuietly(TimeSpan.FromMilliseconds(200));
        }

        private Window? _siblingWindow;

        /// <summary>Zamyka wlasne okno pomocnicze - pomiar nie zostawia okien na pulpicie.</summary>
        internal void CloseSiblingWindow()
        {
            if (_siblingWindow is null) return;
            _siblingWindow.Close();
            _siblingWindow = null;
            PumpQuietly(TimeSpan.FromMilliseconds(120));
        }

        internal void SelectRow(int index)
        {
            var list = MediaList;
            if (list.Items.Count <= index) throw new Exception($"Lista nie ma wiersza {index}.");
            list.SelectedIndex = index;
            list.UpdateLayout();
            PumpQuietly(TimeSpan.FromMilliseconds(50));
        }

        internal void ClearListSelection()
        {
            MediaList.SelectedIndex = -1;
            MediaList.UpdateLayout();
            PumpQuietly(TimeSpan.FromMilliseconds(50));
        }

        internal void ExecuteCommand(string commandId)
        {
            var method = Window.GetType().GetMethod(
                "ExecuteCommand", Instance, binder: null, types: [typeof(string)], modifiers: null)
                ?? throw new Exception("Nie ma prawdziwej metody ExecuteCommand(string).");
            try
            {
                method.Invoke(Window, [commandId]);
            }
            catch (TargetInvocationException exception) when (exception.InnerException is not null)
            {
                throw exception.InnerException;
            }
        }

        /// <summary>
        /// PRAWDZIWY handler klawiatury okna z MODYFIKATOREM. Stan klawiatury
        /// WLASNEGO watku ustawiamy jawnie: KeyEventArgs bez tego nie jest Ctrl+F5.
        /// </summary>
        internal void PressCtrl(Key key)
        {
            var source = PresentationSource.FromVisual(Window)
                ?? throw new Exception("Okno nie ma powierzchni prezentacji; pokaż je przed klawiszem.");
            var previous = new byte[256];
            if (!GetKeyboardState(previous)) throw new Exception("Nie da się odczytać stanu klawiatury wątku.");
            var keys = new byte[256];
            keys[0x11] = 0x80;
            keys[0xA2] = 0x80;
            try
            {
                if (!SetKeyboardState(keys)) throw new Exception("Nie da się ustawić stanu klawiatury wątku.");
                if (Keyboard.Modifiers != ModifierKeys.Control)
                {
                    throw new Exception("Stan wątku nie dał modyfikatora Control; pomiar nie byłby Ctrl+F5.");
                }
                var args = new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, key)
                {
                    RoutedEvent = Keyboard.PreviewKeyDownEvent
                };
                var handler = Window.GetType().GetMethod("Window_PreviewKeyDown", Instance)
                    ?? throw new Exception("Nie ma prawdziwego handlera Window_PreviewKeyDown.");
                try
                {
                    handler.Invoke(Window, [Window, args]);
                }
                catch (TargetInvocationException exception) when (exception.InnerException is not null)
                {
                    throw exception.InnerException;
                }
            }
            finally
            {
                SetKeyboardState(previous);
            }
            PumpQuietly(TimeSpan.FromMilliseconds(80));
        }

        internal void PumpUntil(Func<bool> condition, string what)
        {
            var deadline = DateTime.UtcNow + Limit;
            while (!condition())
            {
                if (DateTime.UtcNow > deadline) throw new Exception("Limit czasu: " + what + ".");
                DoEvents();
            }
        }

        internal void PumpQuietly(TimeSpan duration)
        {
            var deadline = DateTime.UtcNow + duration;
            while (DateTime.UtcNow < deadline) DoEvents();
        }

        internal void Pump(Task task)
        {
            var deadline = DateTime.UtcNow + Limit;
            while (!task.IsCompleted)
            {
                if (DateTime.UtcNow > deadline)
                {
                    throw new Exception("Limit czasu: zadanie sesji Sonos się nie zakończyło.");
                }
                DoEvents();
            }
            task.GetAwaiter().GetResult();
        }

        internal void Pump<T>(Task<T> task) => Pump((Task)task);

        private void DoEvents()
        {
            var frame = new DispatcherFrame();
            _dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => frame.Continue = false));
            Dispatcher.PushFrame(frame);
            Thread.Sleep(1);
        }

        private object? Field(string name) =>
            Window.GetType().GetField(name, Instance)?.GetValue(Window)
            ?? throw new Exception("Nie ma pola " + name + " w prawdziwym MainWindow.");

        public void Dispose()
        {
            Window.CancelSonosPendingWork();
            var closed = false;
            void OnClosed(object? sender, EventArgs e) => closed = true;
            Window.Closed += OnClosed;
            try
            {
                Window.Close();
            }
            catch (InvalidOperationException)
            {
                closed = true;
            }

            var deadline = DateTime.UtcNow + Limit;
            while (!closed)
            {
                if (DateTime.UtcNow > deadline)
                {
                    Window.Closed -= OnClosed;
                    throw new Exception("Limit czasu: własne okno pomiaru się nie zamknęło.");
                }
                DoEvents();
            }
            Window.Closed -= OnClosed;
            PumpQuietly(TimeSpan.FromMilliseconds(200));
            try
            {
                Directory.Delete(_directory, true);
            }
            catch (IOException)
            {
            }
        }

        [DllImport("user32.dll")] private static extern bool GetKeyboardState(byte[] keys);

        [DllImport("user32.dll")] private static extern bool SetKeyboardState(byte[] keys);
    }

    /// <summary>SYNTETYCZNA granica API: zero HTTP, zero tokenu, zero magazynu.</summary>
    private sealed class FakeBackend : ISonosGroupSessionBackend
    {
        internal List<SonosGroupCommand> Commands { get; } = [];

        internal List<string?> CommandGroupIds { get; } = [];

        private readonly SonosPlaybackActions _actions = new(
            canPlay: true, canSkip: true, canSkipBack: true, canSkipToPrevious: true,
            canSeek: true, canPause: true, canStop: null, canRepeat: null, canRepeatOne: null,
            canCrossfade: null, canShuffle: null);

        public Task<SonosGroupReadResult<SonosGroupPlaybackStatus>> ReadGroupPlaybackAsync(
            string? groupId, CancellationToken cancellationToken)
        {
            var status = new SonosGroupPlaybackStatus(
                SonosPlaybackState.Playing, null, null, "UTWOR-1", 12_000, null, null, null, _actions);
            return Task.FromResult(SonosGroupReadResult<SonosGroupPlaybackStatus>.Success(status));
        }

        public Task<SonosGroupReadResult<SonosGroupMetadata>> ReadGroupMetadataAsync(
            string? groupId, CancellationToken cancellationToken)
        {
            var track = new SonosTrackMetadata(
                "track", "Preludium", "Chopin", "Nokturny", null,
                new SonosMetadataService("Sonos Radio", "9"), 180_000);
            var metadata = new SonosGroupMetadata(
                null, new SonosQueueItem("UTWOR-1", track, null), null, null, null);
            return Task.FromResult(SonosGroupReadResult<SonosGroupMetadata>.Success(metadata));
        }

        public Task<SonosGroupReadResult<SonosGroupVolume>> ReadGroupVolumeAsync(
            string? groupId, CancellationToken cancellationToken) =>
            Task.FromResult(
                SonosGroupReadResult<SonosGroupVolume>.Success(new SonosGroupVolume(30, false, false)));

        public Task<SonosGroupCommandResult> SendGroupCommandAsync(
            string? groupId, SonosGroupCommand command, CancellationToken cancellationToken)
        {
            Commands.Add(command);
            CommandGroupIds.Add(groupId);
            return Task.FromResult(SonosGroupCommandResult.CreateAcceptedForMeasurement(command));
        }

        public Task<SonosGroupCommandResult> SeekRelativeAsync(
            string? groupId, int deltaMillis, string? itemId, CancellationToken cancellationToken)
        {
            Commands.Add(SonosGroupCommand.SeekRelative);
            CommandGroupIds.Add(groupId);
            return Task.FromResult(
                SonosGroupCommandResult.CreateAcceptedForMeasurement(SonosGroupCommand.SeekRelative));
        }

        public Task<SonosGroupCommandResult> SetGroupVolumeAsync(
            string? groupId, int volume, CancellationToken cancellationToken)
        {
            Commands.Add(SonosGroupCommand.SetVolume);
            CommandGroupIds.Add(groupId);
            return Task.FromResult(
                SonosGroupCommandResult.CreateAcceptedForMeasurement(SonosGroupCommand.SetVolume));
        }

        public Task<SonosGroupCommandResult> SetGroupMuteAsync(
            string? groupId, bool muted, CancellationToken cancellationToken)
        {
            Commands.Add(SonosGroupCommand.SetMute);
            CommandGroupIds.Add(groupId);
            return Task.FromResult(
                SonosGroupCommandResult.CreateAcceptedForMeasurement(SonosGroupCommand.SetMute));
        }

        public Task<SonosHouseholdsReadResult> ReadHouseholdsAsync(CancellationToken cancellationToken) =>
            Task.FromResult(SonosHouseholdsReadResult.Success([new SonosHousehold("DOM-1", "Dom", null)]));

        public Task<SonosGroupsReadResult> ReadGroupsAsync(
            string householdId, CancellationToken cancellationToken) =>
            Task.FromResult(SonosGroupsReadResult.Success(new SonosHouseholdTopology(
                [
                    new SonosGroup("GRUPA-SALON", "Salon", "P1", ["P1"], SonosPlaybackState.Playing),
                    new SonosGroup("GRUPA-KUCHNIA", "Kuchnia", "P2", ["P2"], SonosPlaybackState.Idle)
                ],
                [
                    new SonosPlayer("P1", "Salon", null, null, null),
                    new SonosPlayer("P2", "Kuchnia", null, null, null)
                ],
                false)));
    }

    /// <summary>
    /// Magazyn konta w PAMIECI: oddaje jeden syntetyczny zestaw i liczy
    /// operacje. Nic nie dotyka DPAPI ani dysku uzytkownika, wiec produkcyjne
    /// RestoreOnce z Ctrl+F5 nie ma jak przeczytac prawdziwego sekretu.
    /// Tokeny sa SYNTETYCZNE i zyja tylko w pamieci procesu pomiaru.
    /// </summary>
    private sealed class PamieciowyMagazynKonta : ISonosCredentialStore
    {
        internal int Reads;
        internal int Writes;
        internal int Deletes;

        public SonosCredentialReadOutcome Read()
        {
            Reads++;
            var tokens = new SonosTokens(
                "ACCESS-SYNTETYCZNY", "Bearer", 3600, "RT-SYNTETYCZNY", "playback-control-all");
            return SonosCredentialReadOutcome.Ok(
                new SonosStoredCredentials("https://broker-testowy.invalid/", tokens, DateTimeOffset.UtcNow));
        }

        public SonosCredentialWriteOutcome Write(SonosStoredCredentials credentials)
        {
            Writes++;
            return SonosCredentialWriteOutcome.Ok();
        }

        public bool Delete()
        {
            Deletes++;
            return true;
        }
    }

    /// <summary>
    /// Bramka logowania, ktora ma NIE zostac zawolana: ten pomiar nie loguje sie
    /// i nie otwiera przegladarki. Kazde wywolanie zatrzymuje pomiar.
    /// </summary>
    private sealed class NieuzywanaBramkaLogowania : ISonosLoginGateway
    {
        internal int Calls;

        public Task<SonosLoginStartOutcome> StartAsync(CancellationToken cancellationToken)
        {
            Calls++;
            throw new Exception("Pomiar układu nawigacji nie ma prawa rozpoczynać logowania Sonos.");
        }

        public Task<SonosLoginResultOutcome> FetchResultAsync(
            SonosLoginSession session, CancellationToken cancellationToken)
        {
            Calls++;
            throw new Exception("Pomiar układu nawigacji nie ma prawa odbierać wyniku logowania Sonos.");
        }

        public Task<SonosRefreshOutcome> RefreshAsync(string? refreshToken, CancellationToken cancellationToken)
        {
            Calls++;
            throw new Exception("Pomiar układu nawigacji nie ma prawa odnawiać dostępu Sonos.");
        }
    }
}
