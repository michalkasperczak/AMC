using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Threading;
using AccessibleMediaController.Core.Commands;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Playback;
using AccessibleMediaController.Core.Presentation;
using AccessibleMediaController.Core.Input;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Windows;

/// <summary>
/// ZAKRES uzgodniony z Michalem 27.09.2026: opcja „Pozostawaj na liście po
/// uruchomieniu stacji Enterem”. Domyslnie WYLACZONA — bez niej Enter na
/// stacji radiowej dziala dokladnie tak jak dotad (gra i otwiera odtwarzacz).
/// Po wlaczeniu Enter na liscie RADIA faktycznie ROZPOCZYNA odtwarzanie, ale
/// NIE otwiera widoku odtwarzacza i nie rusza listy, zaznaczenia ani widoku.
/// Dopiero F6 przechodzi do grajacego odtwarzacza.
///
/// Opcja jest osobna od „Po uruchomieniu presetu otwieraj odtwarzacz”
/// (OpenPlayerWhenActivatingPreset) i nie dotyczy innych sesji ani Spacji.
///
/// Pomiar idzie przez PRAWDZIWY <see cref="AppSettings"/>, prawdziwy
/// <see cref="ConfigurationStore"/> w katalogu tymczasowym, prawdziwe okno
/// <see cref="SettingsWindow"/> oraz prawdziwe <see cref="MainWindow"/> z
/// produkcyjna metoda ActivateSelected (to jedyna rzecz, ktora wykonuje Enter
/// bez modyfikatorow na liscie) i produkcyjnym Window_PreviewKeyDown dla F6.
/// Bez kont, sieci, dzwieku i bez pokazywania okna.
///
/// Tryby:
/// * <see cref="RunModel"/> — model, zapis i katalog polecen, BEZ okien.
/// * <see cref="RunControls"/> — okno Ustawien BEZ ShowDialog.
/// * <see cref="Run"/> — komplet: Zapisz/Anuluj oraz rzeczywiste MainWindow.
/// </summary>
internal static class RadioEnterStaysOnListTests
{
    /// <summary>Etykieta zatwierdzona w zleceniu. Zmiana slowa to zmiana umowy.</summary>
    private const string Etykieta = "Pozostawaj na liście po uruchomieniu stacji Enterem";

    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    private static readonly Dictionary<ConfigurationStore, string> _storePaths = new();

    internal static void Run() => RunCore(0);
    internal static void RunModel() => RunCore(1);
    internal static void RunControls() => RunCore(2);

    private static void RunCore(int mode)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            var root = Path.Combine(
                Path.GetTempPath(),
                "amc-radio-enter-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());

                Console.WriteLine("ETAP: TestDomyslnieWylaczoneRowniezPrzyStarymZapisie");
                TestDomyslnieWylaczoneRowniezPrzyStarymZapisie(root);
                Console.WriteLine("ETAP: TestZapisIOdczytObuWartosci");
                TestZapisIOdczytObuWartosci(root);
                Console.WriteLine("ETAP: TestCloneStatePrzenosiWybor");
                TestCloneStatePrzenosiWybor(root);
                Console.WriteLine("ETAP: TestUstawienieJestWyszukiwalne");
                TestUstawienieJestWyszukiwalne();
                Console.WriteLine("ETAP: TestOpcjaPresetuPozostajeOsobna");
                TestOpcjaPresetuPozostajeOsobna();
                if (mode == 1) return;

                Console.WriteLine("ETAP: TestKontrolkaOdtwarzaWartosc");
                TestKontrolkaOdtwarzaWartosc(root);
                Console.WriteLine("ETAP: TestKontrolkaJestDostepnaIOpisujeZakres");
                TestKontrolkaJestDostepnaIOpisujeZakres(root);
                Console.WriteLine("ETAP: TestKolejnoscTabulacji");
                TestKolejnoscTabulacji(root);
                if (mode == 2) return;

                Console.WriteLine("ETAP: TestZapisPrzenosiWybor");
                TestZapisPrzenosiWybor(root);
                Console.WriteLine("ETAP: TestAnulowanieNieZmieniaOryginalu");
                TestAnulowanieNieZmieniaOryginalu(root);
                Console.WriteLine("ETAP: TestEnterRadiaWObuWartosciach");
                TestEnterRadiaWObuWartosciach();
                Console.WriteLine("ETAP: TestF6PrzechodziDoGrajacegoOdtwarzacza");
                TestF6PrzechodziDoGrajacegoOdtwarzacza();
                Console.WriteLine("ETAP: TestInneSesjeBezZmiany");
                TestInneSesjeBezZmiany();
                Console.WriteLine("ETAP: TestPresetRadiaBezZmiany");
                TestPresetRadiaBezZmiany();
            }
            catch (Exception exception) { failure = exception; }
            finally
            {
                Dispatcher.CurrentDispatcher.InvokeShutdown();
                try { Directory.Delete(root, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(120)))
            throw new Exception("Pomiar opcji „Enter bez odtwarzacza” przekroczył czas.");
        if (failure is not null)
            throw new Exception("Enter radia bez odtwarzacza: " + failure.Message, failure);
        Console.WriteLine(mode switch
        {
            1 => "OK: model opcji „" + Etykieta + "” bez okien",
            2 => "OK: kontrolka opcji „" + Etykieta + "” w oknie Ustawień",
            _ => "OK: opcja „" + Etykieta + "”: domyślna wartość, zapis, Enter radia, F6, inne sesje i presety"
        });
    }

    // ---------------------------------------------------------------- model

    private static void TestDomyslnieWylaczoneRowniezPrzyStarymZapisie(string root)
    {
        Check(!new AppSettings().StayOnListAfterRadioEnter,
            "Świeże ustawienia pozostają na liście po Enterze, choć domyślnie nie powinny.");

        var (state, store) = Prepare(root, "domyslnie");
        store.Save(state);
        var path = StorePath(store);
        var json = JsonNode.Parse(File.ReadAllText(path))!.AsObject();

        var settings = FindSettingsObject(json)
            ?? throw new Exception("Zapis stanu nie zawiera obiektu ustawień ogólnych.");
        var usuniete = settings.Remove(NameOfSetting());
        Check(usuniete, "Zapis stanu nie zawiera nowej własności — nie ma czego migrować.");
        File.WriteAllText(path, json.ToJsonString());

        var wczytane = store.LoadOrCreate();
        Check(!wczytane.Settings.StayOnListAfterRadioEnter,
            "Stary zapis bez tej własności włączył pozostawanie na liście po Enterze.");
    }

    private static void TestZapisIOdczytObuWartosci(string root)
    {
        foreach (var wybor in new[] { true, false })
        {
            var (state, store) = Prepare(root, "zapis-" + wybor);
            state.Settings.StayOnListAfterRadioEnter = wybor;
            store.Save(state);
            var wczytane = store.LoadOrCreate();
            Check(wczytane.Settings.StayOnListAfterRadioEnter == wybor,
                $"Zapis {wybor} nie wrócił z dysku: odczytano {wczytane.Settings.StayOnListAfterRadioEnter}.");

            store.Save(wczytane);
            Check(store.LoadOrCreate().Settings.StayOnListAfterRadioEnter == wybor,
                $"Powtórny zapis zgubił wybór {wybor}.");
        }
    }

    private static void TestCloneStatePrzenosiWybor(string root)
    {
        var (state, store) = Prepare(root, "clone");
        state.Settings.StayOnListAfterRadioEnter = true;
        var kopia = store.CloneState(state);
        Check(kopia.Settings.StayOnListAfterRadioEnter,
            "Migawka stanu zgubiła włączone pozostawanie na liście po Enterze.");

        kopia.Settings.StayOnListAfterRadioEnter = false;
        Check(state.Settings.StayOnListAfterRadioEnter,
            "Migawka nie jest odłączona: zmiana w kopii ruszyła oryginał.");
    }

    private static void TestUstawienieJestWyszukiwalne()
    {
        Check(CommandCatalog.GetAllCommandIds().Contains(CommandIds.SettingsStayOnListAfterRadioEnter),
            "Polecenia nowej opcji nie ma w katalogu poleceń.");
        var nazwa = CommandCatalog.GetDisplayName(CommandIds.SettingsStayOnListAfterRadioEnter);
        Check(!string.Equals(nazwa, CommandIds.SettingsStayOnListAfterRadioEnter, StringComparison.Ordinal),
            "Polecenie nie ma własnej nazwy — paleta pokazałaby identyfikator.");
        Check(nazwa.Contains("liście", StringComparison.OrdinalIgnoreCase)
              && nazwa.Contains("Enter", StringComparison.OrdinalIgnoreCase),
            $"Nazwa polecenia „{nazwa}” nie mówi o pozostawaniu na liście po Enterze.");

        var router = typeof(CommandRouter)
            .GetMethod("TryGetSettingsTarget", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new Exception("CommandRouter nie ma prywatnej reguły celu ustawień.");
        object?[] argumenty = [CommandIds.SettingsStayOnListAfterRadioEnter, null];
        Check((bool)router.Invoke(null, argumenty)!,
            "Polecenie nowej opcji nie prowadzi do żadnego pola Ustawień.");
        Check((SettingsTarget)argumenty[1]! == SettingsTarget.StayOnListAfterRadioEnter,
            $"Polecenie prowadzi do {argumenty[1]} zamiast do własnego pola.");

        foreach (var wybor in new[] { true, false })
        {
            var settings = new AppSettings { StayOnListAfterRadioEnter = wybor };
            var wpis = CommandPaletteSearch
                .CreateEntries(new KeyboardProfile(), settings)
                .SingleOrDefault(entry => string.Equals(
                    entry.CommandId, CommandIds.SettingsStayOnListAfterRadioEnter, StringComparison.Ordinal))
                ?? throw new Exception("Palety nie da się użyć do tej opcji — brak wpisu.");
            Check(wpis.DisplayName.Contains(wybor ? "włączone" : "wyłączone", StringComparison.OrdinalIgnoreCase),
                $"Opis w palecie nie pokazuje bieżącego stanu {wybor}: „{wpis.DisplayName}”.");
        }
    }

    /// <summary>
    /// Opcja presetu jest OSOBNA. Nowa opcja nie moze jej podmieniac ani
    /// wskazywac tego samego pola Ustawien.
    /// </summary>
    private static void TestOpcjaPresetuPozostajeOsobna()
    {
        Check(!string.Equals(
                CommandIds.SettingsStayOnListAfterRadioEnter,
                CommandIds.SettingsOpenPlayerWhenActivatingPreset,
                StringComparison.Ordinal),
            "Nowa opcja przejęła identyfikator polecenia opcji presetu.");
        Check(SettingsTarget.StayOnListAfterRadioEnter != SettingsTarget.OpenPlayerWhenActivatingPreset,
            "Nowa opcja prowadzi do pola opcji presetu.");

        var settings = new AppSettings { StayOnListAfterRadioEnter = true };
        Check(!settings.OpenPlayerWhenActivatingPreset,
            "Włączenie nowej opcji zmieniło opcję presetu.");
    }

    // -------------------------------------------------------------- kontrolka

    private static void TestKontrolkaOdtwarzaWartosc(string root)
    {
        foreach (var wybor in new[] { true, false })
        {
            var (state, store) = Prepare(root, "kontrolka-" + wybor);
            state.Settings.StayOnListAfterRadioEnter = wybor;
            var window = new SettingsWindow(state, store);
            try
            {
                Check(Checkbox(window).IsChecked == wybor,
                    $"Kontrolka pokazuje {Checkbox(window).IsChecked} zamiast zapisanego {wybor}.");
            }
            finally { window.Close(); }
        }
    }

    private static void TestKontrolkaJestDostepnaIOpisujeZakres(string root)
    {
        var (state, store) = Prepare(root, "dostepnosc");
        var window = new SettingsWindow(state, store);
        try
        {
            var box = Checkbox(window);
            Check(string.Equals(AutomationProperties.GetName(box), Etykieta, StringComparison.Ordinal),
                $"Nazwa dla czytnika to „{AutomationProperties.GetName(box)}”, a nie „{Etykieta}”.");

            var pomoc = AutomationProperties.GetHelpText(box);
            Check(!string.IsNullOrWhiteSpace(pomoc), "Kontrolka nie ma opisu pomocniczego.");
            foreach (var (fragment, czego) in new[]
            {
                ("radi", "sesji radia"),
                ("Enter", "klawisza Enter"),
                ("F6", "przejścia do odtwarzacza klawiszem F6"),
                ("odtwarzanie", "faktycznego rozpoczęcia odtwarzania"),
                ("preset", "braku wpływu na presety")
            })
            {
                Check(pomoc.Contains(fragment, StringComparison.OrdinalIgnoreCase),
                    $"Opis pomocniczy nie mówi o {czego} (brak „{fragment}”).");
            }

            Check(box.IsTabStop, "Kontrolki nie da się osiągnąć tabulatorem.");
            Check(!box.IsChecked!.Value, "Okno pokazuje włączoną opcję dla świeżych ustawień.");
        }
        finally { window.Close(); }
    }

    private static void TestKolejnoscTabulacji(string root)
    {
        var (state, store) = Prepare(root, "kolejnosc");
        var window = new SettingsWindow(state, store);
        try
        {
            var zakladka = (TabItem)window.FindName("GeneralTab")
                ?? throw new Exception("Okno Ustawień nie ma zakładki Ogólne.");
            var kolejnosc = Walk(zakladka).OfType<Control>().ToList();
            var nowe = kolejnosc.IndexOf(Checkbox(window));
            Check(nowe >= 0, "Nowe pole nie leży w zakładce Ogólne.");

            var preset = kolejnosc.IndexOf((Control)window.FindName("OpenPlayerWhenActivatingPresetCheck"));
            var pamiec = kolejnosc.IndexOf((Control)window.FindName("RememberLocalPlaybackPositionsCheck"));
            Check(preset >= 0 && pamiec >= 0, "Nie znaleziono sąsiadujących pól zakładki Ogólne.");
            Check(preset < nowe && nowe < pamiec,
                $"Pole stoi w kolejności {nowe}, poza grupą opcji odtwarzania ({preset}…{pamiec}).");
        }
        finally { window.Close(); }
    }

    // ------------------------------------------------- prawdziwy Zapisz/Anuluj

    private static void TestZapisPrzenosiWybor(string root)
    {
        var (state, store) = Prepare(root, "zapisz");
        var result = WithSettings(state, store, save: true, body: window =>
        {
            var box = Checkbox(window);
            Check(box.IsChecked == false, "Test zaczyna od stanu, którego nie zamierzał zmienić.");
            box.IsChecked = true;
        });

        var zapisane = result ?? throw new Exception("Zapisz nie zwrócił stanu ustawień.");
        Check(zapisane.Settings.StayOnListAfterRadioEnter,
            "Przycisk Zapisz nie przeniósł włączonej opcji.");
        Check(!zapisane.Settings.OpenPlayerWhenActivatingPreset,
            "Zapis nowej opcji ruszył opcję presetu.");

        store.Save(zapisane);
        var wczytane = store.LoadOrCreate();
        Check(wczytane.Settings.StayOnListAfterRadioEnter, "Zapis na dysk zgubił wybór.");
        WithSettings(wczytane, store, save: false, body: window =>
            Check(Checkbox(window).IsChecked == true,
                "Po ponownym otwarciu okno nie pokazuje zapisanego wyboru."));

        var wylaczone = WithSettings(wczytane, store, save: true, body: window =>
            Checkbox(window).IsChecked = false);
        Check(wylaczone is not null && !wylaczone.Settings.StayOnListAfterRadioEnter,
            "Przycisk Zapisz nie przeniósł wyłączenia opcji.");
    }

    private static void TestAnulowanieNieZmieniaOryginalu(string root)
    {
        var (state, store) = Prepare(root, "anuluj");
        store.Save(state);

        var result = WithSettings(state, store, save: false, body: window =>
            Checkbox(window).IsChecked = true);

        Check(result is null, "Anuluj zwrócił stan do zapisania.");
        Check(!state.Settings.StayOnListAfterRadioEnter,
            "Anulowanie mimo wszystko włączyło opcję w stanie okna wywołującego.");
        Check(!store.LoadOrCreate().Settings.StayOnListAfterRadioEnter,
            "Anulowanie zapisało zmianę na dysk.");
    }

    // --------------------------------------------- rzeczywiste okno główne

    /// <summary>
    /// Sedno zlecenia. Dla OBU wartosci opcji: Enter na stacji radiowej ma
    /// FAKTYCZNIE rozpoczac odtwarzanie (prawdziwe wywolanie wyjscia audio,
    /// podstawionego atrapa bez dzwieku), a widok odtwarzacza ma sie otworzyc
    /// TYLKO przy wylaczonej opcji. Przy wlaczonej lista, zaznaczenie i widok
    /// zostaja nietkniete.
    /// </summary>
    private static void TestEnterRadiaWObuWartosciach()
    {
        foreach (var opcja in new[] { false, true })
        {
            WOknie(opcja, "radio", (window, sessions) =>
            {
                var radio = sessions.Current;
                var output = Podstaw(radio);
                var wiersze = window.MediaList.Items.Cast<object>().ToArray();
                Check(wiersze.Length >= 2, "Aparatura: lista radia nie ma wierszy do pomiaru.");
                var zaznaczony = Zaznacz(window, "station-b");
                var indeks = window.MediaList.SelectedIndex;
                var widok = Widok(window);

                Call(window, "ActivateSelected");

                Check(radio.IsPlaying, $"opcja={opcja}: Enter nie rozpoczął odtwarzania stacji.");
                Check(radio.CurrentItem.Id == "station-b",
                    $"opcja={opcja}: Enter uruchomił nie tę stację ({radio.CurrentItem.Id}).");
                Check(output.LoadedItemId == "station-b",
                    $"opcja={opcja}: wyjście audio nie dostało żądania odtwarzania stacji.");

                var odtwarzacz = PlayerActive(window);
                Check(odtwarzacz == !opcja,
                    opcja
                        ? "Przy włączonej opcji Enter mimo wszystko otworzył widok odtwarzacza."
                        : "Przy wyłączonej opcji Enter przestał otwierać widok odtwarzacza (regresja).");

                if (!opcja) return;

                Check(wiersze.SequenceEqual(window.MediaList.Items.Cast<object>()),
                    "Enter przy włączonej opcji przebudował listę.");
                Check(ReferenceEquals(zaznaczony, window.MediaList.SelectedItem)
                      && window.MediaList.SelectedIndex == indeks,
                    "Enter przy włączonej opcji zmienił zaznaczenie.");
                Check(string.Equals(widok, Widok(window), StringComparison.Ordinal),
                    $"Enter przy włączonej opcji zmienił widok z „{widok}” na „{Widok(window)}”.");
                Check(window.MediaList.Visibility == Visibility.Visible,
                    "Enter przy włączonej opcji ukrył listę.");
            });
        }
    }

    /// <summary>
    /// F6 pozostaje droga do grajacego odtwarzacza takze wtedy, gdy Enter
    /// celowo go nie otworzyl. Mierzymy PRODUKCYJNY Window_PreviewKeyDown.
    /// </summary>
    private static void TestF6PrzechodziDoGrajacegoOdtwarzacza()
    {
        WOknie(true, "radio", (window, sessions) =>
        {
            var radio = sessions.Current;
            Podstaw(radio);
            Zaznacz(window, "station-b");
            Call(window, "ActivateSelected");
            Check(!PlayerActive(window), "Aparatura: Enter jednak otworzył odtwarzacz.");

            var input = new System.Windows.Input.KeyEventArgs(
                System.Windows.Input.Keyboard.PrimaryDevice,
                new InputSource(),
                Environment.TickCount,
                System.Windows.Input.Key.F6)
            {
                RoutedEvent = System.Windows.Input.Keyboard.PreviewKeyDownEvent,
                Source = window.MediaList
            };
            try { Call(window, "Window_PreviewKeyDown", window, input); }
            catch (TargetInvocationException exception)
                when (exception.InnerException is InvalidOperationException)
            {
                // Window.Activate() wymaga pokazanego okna. Przelaczenie widoku
                // dzieje sie PRZED nim, wiec mierzymy je mimo to; samej
                // aktywacji okna w trybie bez GUI nie sprawdzamy.
            }

            Check(PlayerActive(window), "F6 nie przeszło do grającego odtwarzacza.");
            Check(radio.IsPlaying && radio.CurrentItem.Id == "station-b",
                "F6 zmieniło odtwarzanie zamiast tylko pokazać odtwarzacz.");
        });
    }

    /// <summary>
    /// Opcja jest radiowa. Podcasty i pliki lokalne maja po Enterze nadal
    /// otwierac odtwarzacz, nawet gdy opcja jest WLACZONA.
    /// </summary>
    private static void TestInneSesjeBezZmiany()
    {
        // Widoku biblioteki podcastow/plikow nie da sie zasilic w tym trybie bez
        // rozbudowy aparatury, wiec granice mierzymy dwoma prawdziwymi drogami:
        // (1) inny RODZAJ elementu w tej samej sesji radia - przez produkcyjny
        //     ActivateSelected, czyli pelna droga Entera;
        // (2) inna SESJA z prawdziwym obiektem sesji - przez produkcyjny predykat
        //     bramkujacy jedyna zmieniona linie.
        WOknie(true, "radio", (window, sessions) =>
        {
            var radio = sessions.Current;
            Podstaw(radio);
            radio.ReplaceItems(
            [
                new MediaItem { Id = "station-b", Title = "Stacja B", Kind = MediaItemKind.Station, IsInLibrary = true },
                new MediaItem { Id = "utwor", Title = "Utwór", Kind = MediaItemKind.Track, IsInLibrary = true }
            ]);
            Call(window, "NavigateTo", "Biblioteka");
            Pump();

            Zaznacz(window, "utwor");
            Call(window, "ActivateSelected");
            Check(radio.IsPlaying && radio.CurrentItem.Id == "utwor",
                "Enter na utworze nie rozpoczął odtwarzania.");
            Check(PlayerActive(window),
                "Włączona opcja radiowa zabrała odtwarzacz zwykłemu utworowi.");

            var predykat = typeof(MainWindow).GetMethod("ShouldStayOnListAfterRadioEnter", Private)
                ?? throw new Exception("Brak produkcyjnego predykatu bramkującego.");
            var stacja = new MediaItem { Id = "station-b", Title = "Stacja B", Kind = MediaItemKind.Station };
            Check((bool)predykat.Invoke(window, [radio, stacja])!,
                "Predykat nie działa dla stacji w sesji radia.");
            foreach (var obca in new[] { "podcasts", "local" })
            {
                var session = sessions.SelectSession(obca)
                    ?? throw new Exception($"Nie ma sesji {obca}.");
                Check(!(bool)predykat.Invoke(window, [session, stacja])!,
                    $"Opcja radiowa zabrałaby odtwarzacz sesji {obca}.");
            }
            sessions.SelectSession("radio");
        });
    }

    /// <summary>
    /// Preset radia nadal slucha WYLACZNIE swojej opcji. Nowa opcja go nie rusza.
    /// </summary>
    private static void TestPresetRadiaBezZmiany()
    {
        WOknie(true, "radio", (window, sessions) =>
        {
            var state = (PersistedState)typeof(MainWindow).GetField("_state", Private)!.GetValue(window)!;
            state.Settings.OpenPlayerWhenActivatingPreset = true;
            Podstaw(sessions.Current);

            typeof(MainWindow)
                .GetMethod("ActivatePreset", Private, null, [typeof(int), typeof(bool), typeof(bool)], null)!
                .Invoke(window, [2, true, false]);

            Check(sessions.Current.IsPlaying && sessions.Current.CurrentItem.Id == "station-b",
                "Preset radia nie uruchomił przypisanej stacji.");
            Check(PlayerActive(window),
                "Nowa opcja zabrała presetowi jego własne otwieranie odtwarzacza.");
        });
    }

    // ------------------------------------------------------------- narzedzia

    private static CheckBox Checkbox(SettingsWindow window) =>
        (CheckBox)window.FindName("StayOnListAfterRadioEnterCheck")
        ?? throw new Exception("Okno Ustawień nie ma pola „" + Etykieta + "”.");

    private static (PersistedState State, ConfigurationStore Store) Prepare(string root, string name)
    {
        var folder = Path.Combine(root, name);
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "state.json");
        var store = new ConfigurationStore(path);
        _storePaths[store] = path;
        var state = new PersistedState();
        state.Podcasts.DownloadsFolder = Path.Combine(folder, "podcasts");
        state.Radio.RecordingsFolder = Path.Combine(folder, "recordings");
        return (state, store);
    }

    private static PersistedState? WithSettings(
        PersistedState state,
        ConfigurationStore store,
        bool save,
        Action<SettingsWindow> body)
    {
        var window = new SettingsWindow(state, store);
        Exception? failure = null;
        window.ContentRendered += (_, _) => window.Dispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                body(window);
                var label = save ? "_Zapisz" : "_Anuluj";
                var button = Walk(window).OfType<Button>()
                    .Single(candidate => candidate.Content?.ToString() == label);
                ClickButton(button);
            }
            catch (Exception exception)
            {
                failure = exception;
                try { window.DialogResult = false; } catch (InvalidOperationException) { window.Close(); }
            }
        }), DispatcherPriority.ApplicationIdle);

        var accepted = window.ShowDialog();
        if (failure is not null) throw failure;
        if (save && accepted != true) throw new Exception("Przycisk Zapisz nie zatwierdził ustawień.");
        if (!save && accepted == true) throw new Exception("Przycisk Anuluj zatwierdził ustawienia.");
        return window.ResultState;
    }

    private static void ClickButton(Button button) => typeof(Button)
        .GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic)!
        .Invoke(button, null);

    private static string StorePath(ConfigurationStore store) =>
        _storePaths.TryGetValue(store, out var path)
            ? path
            : throw new Exception("Nie udało się ustalić pliku stanu magazynu konfiguracji.");

    private static string NameOfSetting() =>
        JsonNamingPolicy.CamelCase.ConvertName(nameof(AppSettings.StayOnListAfterRadioEnter));

    private static JsonObject? FindSettingsObject(JsonObject json)
    {
        var key = NameOfSetting();
        foreach (var (_, value) in json)
        {
            if (value is not JsonObject nested) continue;
            if (nested.ContainsKey(key)) return nested;
            var found = FindSettingsObject(nested);
            if (found is not null) return found;
        }
        return null;
    }

    private static bool PlayerActive(MainWindow window) =>
        (bool)typeof(MainWindow).GetField("_playerViewActive", Private)!.GetValue(window)!;

    private static string Widok(MainWindow window) =>
        (string)typeof(MainWindow).GetField("_currentView", Private)!.GetValue(window)!;

    private static object? Call(MainWindow window, string name, params object?[] args) =>
        typeof(MainWindow).GetMethod(name, Private)!.Invoke(window, args.Length == 0 ? null : args);

    /// <summary>Atrapa wyjscia audio: rejestruje zadania, nie odtwarza dzwieku.</summary>
    private static ObservedOutput Podstaw(DemoMediaSession session)
    {
        var output = new ObservedOutput();
        typeof(DemoMediaSession).GetField("_output", Private)!.SetValue(session, output);
        return output;
    }

    private sealed class ObservedOutput : IMediaOutput
    {
        public string? LoadedItemId { get; private set; }
        public TimeSpan Position { get; private set; }
        public bool SupportsPlaybackRate => false;
        public int Calls { get; private set; }
        public void Play(MediaItem item, TimeSpan position, int volume, double rate)
        { LoadedItemId = item.Id; Position = position; Calls++; }
        public void Pause() { Calls++; }
        public void Stop() { LoadedItemId = null; Calls++; }
        public void Seek(TimeSpan position) { Position = position; Calls++; }
        public void SetVolume(int volume) { Calls++; }
        public void SetPlaybackRate(double rate) { Calls++; }
    }

    private sealed class InputSource : PresentationSource
    {
        public override System.Windows.Media.Visual RootVisual { get; set; } = null!;
        public override bool IsDisposed => false;
        protected override System.Windows.Media.CompositionTarget GetCompositionTargetCore() => null!;
    }

    /// <summary>
    /// Prawdziwe MainWindow na wlasnym watku STA, wlasny katalog tymczasowy,
    /// izolowany magazyn konfiguracji. Bez Show, bez kont, sieci, dzwieku,
    /// aktualizacji i integracji z pulpitem.
    /// </summary>
    private static void WOknie(bool opcja, string sessionId, Action<MainWindow, SessionManager> sprawdz)
    {
        var root = Path.Combine(Path.GetTempPath(), "amc-radio-enter-okno-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        MainWindow? window = null;
        try
        {
            var state = new PersistedState();
            state.Settings.Updates.CheckAutomatically = false;
            state.Settings.Updates.InstallOnExit = false;
            state.Settings.StayOnListAfterRadioEnter = opcja;
            state.Settings.LastSessionId = sessionId;
            state.Podcasts.DownloadsFolder = Path.Combine(root, "podcasts");
            state.Radio.RecordingsFolder = Path.Combine(root, "recordings");
            state.SessionPresets.EntriesBySession["radio"] =
            [
                new SessionPresetEntry
                {
                    Slot = 2,
                    TargetId = "station-b",
                    TargetKind = "station",
                    TargetTitle = "Stacja B"
                }
            ];

            var store = new ConfigurationStore(Path.Combine(root, "state.json"));
            window = new MainWindow(state, store) { SuppressDesktopIntegrationForTests = true };
            typeof(MainWindow).GetField("_applicationUpdateStartOverride", Private)!.SetValue(window,
                (Func<bool, bool>)(_ => throw new Exception("Test zabrania instalacji aktualizacji")));

            var sessions = (SessionManager)typeof(MainWindow).GetField("_sessions", Private)!.GetValue(window)!;
            var session = sessions.SelectSession(sessionId)!;
            var kind = sessionId switch
            {
                "radio" => MediaItemKind.Station,
                "podcasts" => MediaItemKind.Episode,
                _ => MediaItemKind.Track
            };
            session.ReplaceItems(
            [
                new MediaItem { Id = "station-a", Title = "Stacja A", Kind = kind, IsInLibrary = true },
                new MediaItem { Id = "station-b", Title = "Stacja B", Kind = kind, IsInLibrary = true }
            ]);
            Call(window, "NavigateTo", "Biblioteka");
            Pump();
            sprawdz(window, sessions);
        }
        finally
        {
            window?.Close();
            Pump();
            try { Directory.Delete(root, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    /// <summary>Zaznacza wiersz konkretnego elementu; indeks zalezy od widoku.</summary>
    private static object Zaznacz(MainWindow window, string itemId)
    {
        for (var index = 0; index < window.MediaList.Items.Count; index++)
        {
            var row = window.MediaList.Items[index];
            var item = row?.GetType().GetProperty("Item")?.GetValue(row) as MediaItem;
            if (string.Equals(item?.Id, itemId, StringComparison.Ordinal))
            {
                window.MediaList.SelectedIndex = index;
                return row!;
            }
        }
        throw new Exception($"Aparatura: na liście nie ma wiersza elementu {itemId}.");
    }

    private static void Pump() =>
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);

    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }

    private static IEnumerable<DependencyObject> Walk(DependencyObject root)
    {
        yield return root;
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
            foreach (var descendant in Walk(child)) yield return descendant;
    }
}
