using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Threading;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Playback;
using AccessibleMediaController.Windows;

/// <summary>
/// ZGLOSZENIE Michala 20.09.2026: opcje odtwarzania KAZDEJ sesji maja byc
/// dostepne z glownych Ustawien, a nie tylko pod Ctrl+Alt+Enter dla sesji
/// BIEZACEJ. Wczesniej zmiana ustawienia innej sesji wymagala przelaczenia sie
/// na nia.
///
/// Pomiar idzie przez RZECZYWISTE okno Ustawien: prawdziwy przycisk, prawdziwe
/// podokno <see cref="ItemPlaybackOptionsWindow"/>, prawdziwy przycisk Zapisz
/// albo Anuluj. Nie ma tu kont, sieci ani dzwieku - tylko stan konfiguracji w
/// osobnym katalogu tymczasowym.
/// </summary>
internal static class SessionOptionsInSettingsTests
{
    /// <summary>
    /// Handler klasowy WPF nie da sie odrejestrowac, wiec rejestrujemy go RAZ i
    /// kierujemy do tego pola. Kazdy przypadek podstawia wlasna obsluge
    /// otwartego podokna opcji sesji.
    /// </summary>
    private static Action<ItemPlaybackOptionsWindow>? _onOptionsDialog;
    private static bool _classHandlerRegistered;

    internal static void Run() => RunCore(0);
    internal static void RunHeadless() => RunCore(1);
    internal static void RunConsumer() => RunCore(2);

    private static void RunCore(int mode)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            var root = Path.Combine(
                Path.GetTempPath(),
                "amc-session-options-settings-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
                RegisterOptionsDialogHandler();
                if (mode != 0)
                {
                    if (mode == 1) TestPolaOdpowiadajaOdbiorcom();
                    else TestOpcjePodcastowDocierajaDoOdtwarzania(root);
                    return;
                }
                Console.WriteLine("ETAP: TestPrzyciskOpcjiSesjiJestDostepny");
                TestPrzyciskOpcjiSesjiJestDostepny(root);
                Console.WriteLine("ETAP: TestDwieSesjeZapisaneIWidocznePoPonownymOtwarciu");
                TestDwieSesjeZapisaneIWidocznePoPonownymOtwarciu(root);
                Console.WriteLine("ETAP: TestAnulowanieUstawienNieZmieniaStanu");
                TestAnulowanieUstawienNieZmieniaStanu(root);
                Console.WriteLine("ETAP: TestBiezaceDziedziczenieIAnulowaniePodokna");
                TestBiezaceDziedziczenieIAnulowaniePodokna(root);
                TestPolaOdpowiadajaOdbiorcom();
                TestOpcjePodcastowDocierajaDoOdtwarzania(root);
                Console.WriteLine("ETAP: TestOknoPokazujeTylkoObslugiwaneOpcje");
                TestOknoPokazujeTylkoObslugiwaneOpcje(root);
                Console.WriteLine("ETAP: TestUkryteWartosciZostajaNietkniete");
                TestUkryteWartosciZostajaNietkniete(root);
                Console.WriteLine("ETAP: TestSkrotDajeTenSamWynikCoUstawienia");
                TestSkrotDajeTenSamWynikCoUstawienia(root);
            }
            catch (Exception exception) { failure = exception; }
            finally
            {
                _onOptionsDialog = null;
                Dispatcher.CurrentDispatcher.InvokeShutdown();
                try { Directory.Delete(root, true); } catch (IOException) { }
            }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(90)))
            throw new Exception("Pomiar opcji sesji w Ustawieniach przekroczył czas.");
        if (failure is not null)
            throw new Exception("Opcje sesji w Ustawieniach: " + failure.Message, failure);
        Console.WriteLine(
            mode != 0 ? "OK: wybrana kontrola możliwości lub odbiorcy opcji sesji"
                : "OK: opcje odtwarzania sesji z Ustawień: zapis, anulowanie i obsługiwane pola");
    }

    private static void RegisterOptionsDialogHandler()
    {
        if (_classHandlerRegistered) return;
        _classHandlerRegistered = true;
        EventManager.RegisterClassHandler(
            typeof(ItemPlaybackOptionsWindow),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler((sender, _) =>
            {
                if (sender is not ItemPlaybackOptionsWindow dialog) return;
                var handler = _onOptionsDialog;
                if (handler is null) return;
                dialog.Dispatcher.BeginInvoke(
                    new Action(() => handler(dialog)),
                    DispatcherPriority.ApplicationIdle);
            }));
    }

    // ---------------------------------------------------------------- przypadki

    private static void TestPolaOdpowiadajaOdbiorcom()
    {
        var type = typeof(MainWindow).Assembly.GetType("AccessibleMediaController.Windows.SessionPlaybackOptionsEditor")!;
        var create = type.GetMethod("CreateDialog", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        foreach (var (id, resume, dsp, pause) in new[]
        {
            ("local", true, true, true), ("podcasts", true, true, true),
            ("spotify", true, false, true), ("radio", false, false, true),
            ("tidal", false, false, true), ("wiim", false, false, false),
            ("appleMusic", false, false, true)
        })
        {
            var dialog = (ItemPlaybackOptionsWindow)create.Invoke(null, [new AppSettings(), id, id])!;
            try
            {
                Check(((ComboBox)dialog.FindName("ResumeModeBox")).Visibility == (resume ? Visibility.Visible : Visibility.Collapsed), "Brak zgodnej obsługi pamięci pozycji: " + id);
                foreach (var name in new[] { "LoudnessNormalizationBox", "SmoothTransitionsBox", "InterTrackSilenceBox" })
                    Check(((ComboBox)dialog.FindName(name)).Visibility == (dsp ? Visibility.Visible : Visibility.Collapsed), "Okno obiecuje nieobsługiwane DSP: " + id + " / " + name);
                Check(((StackPanel)dialog.FindName("SessionSettingsPanel")).Visibility == (pause ? Visibility.Visible : Visibility.Collapsed), "Nieprawidłowe pole pauzy: " + id);
                Check(((ComboBox)dialog.FindName("PlaybackRateBox")).Visibility == Visibility.Collapsed, "Nieobsługiwany zapis prędkości sesji: " + id);
                Check(((ComboBox)dialog.FindName("OutputDeviceBox")).Visibility == Visibility.Collapsed, "Okno sesji pokazuje pozorny wybór wyjścia: " + id);
            }
            finally { dialog.Close(); }
        }
    }

    private static void TestOpcjePodcastowDocierajaDoOdtwarzania(string root)
    {
        var (state, store) = Prepare(root, "odbiorca");
        state.Settings.RememberLocalPlaybackPositions = true;
        state.Settings.Audio.LoudnessNormalizationEnabled = false;
        ResumePositionPolicy.SetSessionMode(state.Settings, "podcasts", ResumePositionMode.StartFromBeginning);
        state.Settings.Audio.OverridesBySession["podcasts"] = new SessionPlaybackAudioOverrides
        { LoudnessNormalizationOverride = true, InterTrackSilenceMillisecondsOverride = 2000 };
        var window = new MainWindow(state, store);
        try
        {
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var item = new AccessibleMediaController.Core.Sessions.MediaItem
            { Id = "próba-odcinka", Title = "Próba", Kind = AccessibleMediaController.Core.Sessions.MediaItemKind.Episode };
            var remember = (bool)typeof(MainWindow).GetMethod("ShouldRememberPodcastPosition", flags)!.Invoke(window, [item])!;
            Check(!remember, "Odtwarzacz Podcastów nie odczytał ustawienia całej sesji: zawsze od początku.");
            var audio = (PlaybackAudioSettings)typeof(MainWindow).GetMethod("GetEffectivePodcastAudioSettings", flags)!.Invoke(window, [item])!;
            Check(audio.LoudnessNormalizationEnabled && audio.InterTrackSilenceMilliseconds == 2000,
                "Odtwarzacz Podcastów pominął przetwarzanie dźwięku ustawione dla sesji.");
        }
        finally { window.Close(); }
    }

    private static void TestPrzyciskOpcjiSesjiJestDostepny(string root)
    {
        var (state, store) = Prepare(root, "dostep");
        using var _ = new NoDialog();
        var window = new SettingsWindow(state, store);
        try
        {
            var button = window.FindName("SessionPlaybackOptionsButton") as Button;
            Check(button is not null,
                "W Ustawieniach nie ma drogi do opcji odtwarzania wybranej sesji.");
            Check(button!.Content?.ToString() == "Opcje odtwarzania sesji…",
                "Przycisk opcji sesji nie ma czytelnej etykiety z klawiszem dostępu.");
            Check(AutomationProperties.GetName(button) == "Opcje odtwarzania zaznaczonej sesji",
                "Przycisk opcji sesji nie ma nazwy dla czytnika ekranu.");
            var help = AutomationProperties.GetHelpText(button) ?? string.Empty;
            Check(help.Contains("Ctrl+Alt+Enter", StringComparison.Ordinal),
                "Podpowiedź nie mówi, że to te same opcje co pod skrótem sesji bieżącej.");
            Check(help.Contains("Zapisz", StringComparison.Ordinal)
                  && help.Contains("Anuluj", StringComparison.Ordinal),
                "Podpowiedź nie mówi, że zmiana czeka na Zapisz i że Anuluj ją cofa.");

            var list = (ListBox)window.FindName("SessionOrderList");
            Check((AutomationProperties.GetName(list) ?? string.Empty)
                    .Contains("opcje odtwarzania", StringComparison.OrdinalIgnoreCase),
                "Nazwa listy sesji nie mówi, że jej zaznaczenie wyznacza edytowane opcje.");
            Check(list.SelectedIndex == 0,
                "Lista sesji nie ma zaznaczonej sesji, więc przycisk nie miałby na czym działać.");
        }
        finally { window.Close(); }
    }

    private static void TestDwieSesjeZapisaneIWidocznePoPonownymOtwarciu(string root)
    {
        var (state, store) = Prepare(root, "zapis");

        // Przerwa w numerach skrotow Ctrl+cyfra: zapis opcji sesji NIE MOZE jej
        // zageszczac, bo to zmienilo by klawisze, ktorych uzytkownik nie ruszal.
        state.Settings.SessionSlots = new Dictionary<int, string>
        {
            [1] = "local",
            [3] = "radio",
            [4] = "spotify",
            [6] = "wiim", [7] = "tidal", [8] = "appleMusic", [9] = "podcasts"
        };
        var oczekiwaneSloty = new Dictionary<int, string>(state.Settings.SessionSlots);

        var result = WithSettings(state, store, save: true, body: window =>
        {
            EditSession(window, "local", dialog =>
            {
                Select(dialog, "ResumeModeBox", "Zawsze od początku");
                Select(dialog, "LoudnessNormalizationBox", "Włączone");
                Select(dialog, "SmoothTransitionsBox", "Wyłączone");
                Select(dialog, "InterTrackSilenceBox", "Cisza: 2 sekundy");
                Select(dialog, "PlayerExitPauseBox", "Odtwarzaj dalej");
            });

            // Druga sesja - BEZ przelaczania sesji aktywnej i bez zamykania okna.
            EditSession(window, "podcasts", dialog =>
            {
                Select(dialog, "ResumeModeBox", "Pamiętaj pozycję odtwarzania");
                Select(dialog, "LoudnessNormalizationBox", "Wyłączone");
                Select(dialog, "SmoothTransitionsBox", "Włączone");
                Select(dialog, "InterTrackSilenceBox", "Cisza: pół sekundy");
                Select(dialog, "PlayerExitPauseBox", "Wstrzymuj odtwarzanie");
            });

            // Zanim padnie Zapisz, RZECZYWISTY stan musi byc nietkniety.
            Check(state.Settings.Audio.OverridesBySession.Count == 0,
                "Podokno opcji sesji zapisało do rzeczywistego stanu przed naciśnięciem Zapisz.");
            Check(state.Settings.ResumePositionModeBySession.Count == 0,
                "Podokno opcji sesji zmieniło pamięć pozycji w rzeczywistym stanie przed Zapisz.");

            var status = (TextBlock)window.FindName("SessionOrderStatus");
            Check(status.Text.Contains("Podcasty i YouTube", StringComparison.Ordinal)
                  && status.Text.Contains("Zapisz", StringComparison.Ordinal),
                "Komunikat nie mówi, której sesji dotyczy zmiana ani że czeka na Zapisz.");

            var list = (ListBox)window.FindName("SessionOrderList");
            Check(SessionIdOf(list.SelectedItem!) == "podcasts",
                "Zaznaczenie sesji zmieniło się po edycji jej opcji.");
        });

        var saved = result ?? throw new Exception("Zapisz nie zwrócił stanu ustawień.");
        AssertOverrides(saved.Settings, "local",
            loudness: true, smooth: false, silence: 2000, exitPause: false,
            resume: ResumePositionMode.StartFromBeginning);
        AssertOverrides(saved.Settings, "podcasts",
            loudness: false, smooth: true, silence: 500, exitPause: true,
            resume: ResumePositionMode.Remember);
        Check(saved.Settings.SessionSlots.OrderBy(pair => pair.Key)
                .SequenceEqual(oczekiwaneSloty.OrderBy(pair => pair.Key)),
            "Zapis opcji sesji przenumerował skróty Ctrl+cyfra innych sesji.");

        // Trwalosc: zapis na dysk, odczyt przez produkcyjny LoadOrCreate i
        // ponowne otwarcie okna pokazuje zapisane wartosci.
        store.Save(saved);
        var reloaded = store.LoadOrCreate();
        AssertOverrides(reloaded.Settings, "local",
            loudness: true, smooth: false, silence: 2000, exitPause: false,
            resume: ResumePositionMode.StartFromBeginning);
        AssertOverrides(reloaded.Settings, "podcasts",
            loudness: false, smooth: true, silence: 500, exitPause: true,
            resume: ResumePositionMode.Remember);

        WithSettings(reloaded, store, save: false, body: window =>
        {
            EditSession(window, "local", dialog =>
            {
                CheckSelected(dialog, "ResumeModeBox", "Zawsze od początku");
                CheckSelected(dialog, "LoudnessNormalizationBox", "Włączone");
                CheckSelected(dialog, "SmoothTransitionsBox", "Wyłączone");
                CheckSelected(dialog, "InterTrackSilenceBox", "Cisza: 2 sekundy");
                CheckSelected(dialog, "PlayerExitPauseBox", "Odtwarzaj dalej");
            });
            EditSession(window, "podcasts", dialog =>
            {
                CheckSelected(dialog, "ResumeModeBox", "Pamiętaj pozycję odtwarzania");
                CheckSelected(dialog, "InterTrackSilenceBox", "Cisza: pół sekundy");
                CheckSelected(dialog, "PlayerExitPauseBox", "Wstrzymuj odtwarzanie");
            });
        });
    }

    private static void TestAnulowanieUstawienNieZmieniaStanu(string root)
    {
        var (state, store) = Prepare(root, "anuluj");
        state.Settings.Audio.OverridesBySession["local"] = new SessionPlaybackAudioOverrides
        {
            LoudnessNormalizationOverride = true
        };
        ResumePositionPolicy.SetSessionMode(state.Settings, "local", ResumePositionMode.Remember);

        var result = WithSettings(state, store, save: false, body: window =>
        {
            EditSession(window, "local", dialog =>
            {
                Select(dialog, "ResumeModeBox", "Zawsze od początku");
                Select(dialog, "LoudnessNormalizationBox", "Wyłączone");
                Select(dialog, "InterTrackSilenceBox", "Cisza: 3 sekundy");
            });
        });

        Check(result is null, "Anulowanie ustawień zwróciło stan do zapisania.");
        Check(state.Settings.Audio.OverridesBySession["local"].LoudnessNormalizationOverride == true,
            "Anulowanie Ustawień nie cofnęło zmiany zatwierdzonej w podoknie opcji sesji.");
        Check(state.Settings.Audio.OverridesBySession["local"].InterTrackSilenceMillisecondsOverride is null,
            "Anulowanie Ustawień zostawiło ciszę ustawioną tylko w podoknie.");
        Check(ResumePositionPolicy.GetSessionMode(state.Settings, "local") == ResumePositionMode.Remember,
            "Anulowanie Ustawień nie cofnęło zmiany pamięci pozycji sesji.");

        var onDisk = store.LoadOrCreate();
        Check(!onDisk.Settings.Audio.OverridesBySession.ContainsKey("local")
              || onDisk.Settings.Audio.OverridesBySession["local"].InterTrackSilenceMillisecondsOverride is null,
            "Anulowane Ustawienia trafiły na dysk.");
    }

    private static void TestBiezaceDziedziczenieIAnulowaniePodokna(string root)
    {
        var (state, store) = Prepare(root, "dziedziczenie");
        state.Settings.PausePlaybackWhenLeavingPlayer = true;
        WithSettings(state, store, save: false, body: window =>
        {
            ((CheckBox)window.FindName("PausePlaybackWhenLeavingPlayerCheck")).IsChecked = false;
            EditSession(window, "local", dialog =>
                CheckSelected(dialog, "PlayerExitPauseBox", "Jak ustawienie ogólne — odtwarzaj dalej"));
        });
        Check(state.Settings.PausePlaybackWhenLeavingPlayer, "Zewnętrzne Anuluj zmieniło opcję ogólną.");
        ResumePositionPolicy.SetSessionMode(state.Settings, "local", ResumePositionMode.Remember);
        var saved = WithSettings(state, store, save: true, body: window =>
            EditSession(window, "local", dialog => Select(dialog, "ResumeModeBox", "Zawsze od początku"), accept: false))!;
        Check(ResumePositionPolicy.GetSessionMode(saved.Settings, "local") == ResumePositionMode.Remember,
            "Zewnętrzne Zapisz utrwaliło zmianę anulowaną w podoknie.");
    }

    private static void TestOknoPokazujeTylkoObslugiwaneOpcje(string root)
    {
        var (state, store) = Prepare(root, "zdolnosci");
        WithSettings(state, store, save: false, body: window =>
        {
            // Spotify gra przez wlasny odtwarzacz uslugi: pola DSP nie maja tam
            // odbiorcy, wiec okno NIE MOZE ich pokazywac.
            EditSession(window, "spotify", dialog =>
            {
                foreach (var martwa in new[]
                         {
                             "LoudnessNormalizationBox", "SmoothTransitionsBox",
                             "InterTrackSilenceBox", "OutputDeviceBox"
                         })
                {
                    Check(((ComboBox)dialog.FindName(martwa)).Visibility == Visibility.Collapsed,
                        $"Opcje sesji Spotify pokazują nieobsługiwane {martwa}.");
                }
                Check(((ComboBox)dialog.FindName("ResumeModeBox")).Visibility == Visibility.Visible,
                    "Sesja Spotify musi nadal pozwalać ustawić pamięć pozycji.");
                Check(((ComboBox)dialog.FindName("PlayerExitPauseBox")).Visibility == Visibility.Visible,
                    "Sesja Spotify musi pozwalać ustawić wstrzymywanie po wyjściu z odtwarzacza.");
            });

            // WiiM to autonomiczny odtwarzacz sieciowy - wyjscie z kontrolera
            // NIGDY nie zatrzymuje muzyki w pokoju.
            var list = (ListBox)window.FindName("SessionOrderList");
            list.SelectedItem = list.Items.Cast<object>().Single(row => SessionIdOf(row) == "wiim");
            Check(!((Button)window.FindName("SessionPlaybackOptionsButton")).IsEnabled,
                "WiiM oferuje pusty edytor nieobsługiwanych opcji.");

            // ZMIERZONY BLOKER: predkosc odtwarzania nie ma zapisu na poziomie
            // sesji (brak slownika w AppSettings i brak resolvera), a dotad
            // okno i tak pokazywalo te liste dla KAZDEJ sesji.
            foreach (var sessionId in new[] { "local", "radio", "spotify", "podcasts", "tidal", "appleMusic" })
            {
                EditSession(window, sessionId, dialog =>
                    Check(((ComboBox)dialog.FindName("PlaybackRateBox")).Visibility == Visibility.Collapsed,
                        $"Okno opcji sesji {sessionId} pokazuje prędkość, której nikt dla sesji nie odczytuje."));
            }
        });
    }

    private static void TestUkryteWartosciZostajaNietkniete(string root)
    {
        var (state, store) = Prepare(root, "ukryte");

        // Wartosci zapisane wczesniej (np. przez inna wersje albo recznie w
        // pliku) dla pol, ktorych okno danej sesji NIE POKAZUJE.
        state.Settings.Audio.OverridesBySession["spotify"] = new SessionPlaybackAudioOverrides
        {
            LoudnessNormalizationOverride = true,
            SmoothTrackTransitionsOverride = false,
            InterTrackSilenceMillisecondsOverride = 5000
        };
        state.Settings.Audio.OverridesBySession["wiim"] = new SessionPlaybackAudioOverrides
        {
            PausePlaybackWhenLeavingPlayerOverride = true,
            LoudnessNormalizationOverride = true
        };

        var saved = WithSettings(state, store, save: true, body: window =>
        {
            EditSession(window, "spotify", dialog =>
                Select(dialog, "ResumeModeBox", "Zawsze od początku"));
            var sessions = (ListBox)window.FindName("SessionOrderList");
            sessions.SelectedItem = sessions.Items.Cast<object>().Single(row => SessionIdOf(row) == "wiim");
            Check(!((Button)window.FindName("SessionPlaybackOptionsButton")).IsEnabled,
                "WiiM pokazuje edycję opcji bez odbiorcy.");
        }) ?? throw new Exception("Zapisz nie zwrócił stanu ustawień.");

        var spotify = saved.Settings.Audio.OverridesBySession["spotify"];
        Check(spotify.LoudnessNormalizationOverride == true
              && spotify.SmoothTrackTransitionsOverride == false
              && spotify.InterTrackSilenceMillisecondsOverride == 5000,
            "Zapis opcji sesji Spotify wymazał wartości pól, których okno nie pokazało.");
        Check(ResumePositionPolicy.GetSessionMode(saved.Settings, "spotify")
              == ResumePositionMode.StartFromBeginning,
            "Wybór pamięci pozycji sesji Spotify nie został zapisany.");

        var wiim = saved.Settings.Audio.OverridesBySession["wiim"];
        Check(wiim.PausePlaybackWhenLeavingPlayerOverride == true,
            "Zapis opcji sesji WiiM wymazał wstrzymywanie po wyjściu, którego okno nie pokazało.");
        Check(wiim.LoudnessNormalizationOverride == true,
            "Zewnętrzny zapis wymazał niewidoczną dawną wartość WiiM.");
    }

    private static void TestSkrotDajeTenSamWynikCoUstawienia(string root)
    {
        // Ctrl+Alt+Enter i Ustawienia MUSZA dawac identyczny zapis, bo obie
        // drogi wolaja te sama regule. Tutaj mierzymy wynik wspolnej logiki na
        // osobnych ustawieniach i porownujemy z droga przez okno Ustawien.
        var (state, store) = Prepare(root, "skrot");
        var saved = WithSettings(state, store, save: true, body: window =>
            EditSession(window, "local", dialog =>
            {
                Select(dialog, "ResumeModeBox", "Zawsze od początku");
                Select(dialog, "LoudnessNormalizationBox", "Włączone");
                Select(dialog, "InterTrackSilenceBox", "Cisza: 1 sekunda");
                Select(dialog, "PlayerExitPauseBox", "Odtwarzaj dalej");
            })) ?? throw new Exception("Zapisz nie zwrócił stanu ustawień.");

        var direct = new AppSettings();
        var editor = typeof(MainWindow).Assembly.GetType("AccessibleMediaController.Windows.SessionPlaybackOptionsEditor")
            ?? throw new Exception("Brak wspólnej obsługi opcji sesji.");
        const System.Reflection.BindingFlags editorFlags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
        var dialogDirect = (ItemPlaybackOptionsWindow)editor.GetMethod("CreateDialog", editorFlags)!
            .Invoke(null, [direct, "local", "Pliki lokalne"])!;
        try
        {
            dialogDirect.Show();
            dialogDirect.UpdateLayout();
            Select(dialogDirect, "ResumeModeBox", "Zawsze od początku");
            Select(dialogDirect, "LoudnessNormalizationBox", "Włączone");
            Select(dialogDirect, "InterTrackSilenceBox", "Cisza: 1 sekunda");
            Select(dialogDirect, "PlayerExitPauseBox", "Odtwarzaj dalej");
            editor.GetMethod("Apply", editorFlags)!.Invoke(null, [direct, "local", dialogDirect]);
        }
        finally { dialogDirect.Close(); }

        var zUstawien = saved.Settings.Audio.OverridesBySession["local"];
        var zeSkrotu = direct.Audio.OverridesBySession["local"];
        Check(zUstawien.LoudnessNormalizationOverride == zeSkrotu.LoudnessNormalizationOverride
              && zUstawien.SmoothTrackTransitionsOverride == zeSkrotu.SmoothTrackTransitionsOverride
              && zUstawien.InterTrackSilenceMillisecondsOverride == zeSkrotu.InterTrackSilenceMillisecondsOverride
              && zUstawien.PausePlaybackWhenLeavingPlayerOverride == zeSkrotu.PausePlaybackWhenLeavingPlayerOverride,
            "Ustawienia i wspólna logika skrótu zapisują różne wyjątki dźwięku tej samej sesji.");
        Check(ResumePositionPolicy.GetSessionMode(saved.Settings, "local")
              == ResumePositionPolicy.GetSessionMode(direct, "local"),
            "Ustawienia i wspólna logika skrótu zapisują różną pamięć pozycji tej samej sesji.");

        // Skrot Ctrl+Alt+Enter MUSI przechodzic przez te sama regule, a nie
        // przez wlasna kopie kodu, ktora rozjedzie sie przy nastepnej poprawce.
        var main = File.ReadAllText(FindSource("MainWindow.xaml.cs"));
        var start = main.IndexOf("public void ShowSessionPlaybackOptions()", StringComparison.Ordinal);
        Check(start > 0, "Nie znaleziono obsługi skrótu opcji sesji.");
        var end = main.IndexOf("private SessionPlaybackAudioOverrides? FindSessionPlaybackOverrides", start, StringComparison.Ordinal);
        Check(end > start, "Nie znaleziono końca obsługi opcji sesji.");
        var body = main[start..end];
        Check(body.Contains("SessionPlaybackOptionsEditor.CreateDialog", StringComparison.Ordinal)
              && body.Contains("SessionPlaybackOptionsEditor.Apply", StringComparison.Ordinal),
            "Skrót opcji sesji nie korzysta ze wspólnej reguły, której używają Ustawienia.");
        var settingsSource = string.Join("\n", File.ReadAllLines(FindSource("SettingsWindow.xaml.cs"))
            .Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal)));
        Check(!settingsSource.Contains("new ItemPlaybackOptionsWindow", StringComparison.Ordinal),
            "Ustawienia budują okno opcji sesji własną kopią kodu zamiast wspólnej reguły.");
        Check(!settingsSource.Contains("ShowSessionPlaybackOptions", StringComparison.Ordinal),
            "Ustawienia wołają obsługę sesji bieżącej, która zapisuje od razu i łamie Anuluj.");
    }

    // ------------------------------------------------------------- narzedzia

    /// <summary>Świeży, izolowany stan: brak kont, sieci i cudzych danych.</summary>
    private static (PersistedState State, ConfigurationStore Store) Prepare(string root, string name)
    {
        var folder = Path.Combine(root, name);
        Directory.CreateDirectory(folder);
        var store = new ConfigurationStore(Path.Combine(folder, "state.json"));
        var state = new PersistedState();
        state.Podcasts.DownloadsFolder = Path.Combine(folder, "podcasts");
        state.Radio.RecordingsFolder = Path.Combine(folder, "recordings");
        return (state, store);
    }

    /// <summary>
    /// Otwiera RZECZYWISTE okno Ustawień, wykonuje próbę i kończy prawdziwym
    /// przyciskiem Zapisz albo Anuluj. Zwraca stan przekazany do zapisania
    /// (null po Anuluj).
    /// </summary>
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

    // RaiseEvent pomija Button.OnClick i wbudowane IsCancel. Wywołujemy prawdziwe kliknięcie WPF.
    private static void ClickButton(Button button) => typeof(Button)
        .GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
        .Invoke(button, null);

    /// <summary>
    /// Zaznacza sesję na liście i otwiera jej opcje PRAWDZIWYM przyciskiem
    /// Ustawień. <paramref name="inside"/> dostaje otwarte podokno; po nim
    /// podokno jest zatwierdzane jak przyciskiem Zapisz.
    /// </summary>
    private static void EditSession(SettingsWindow window, string sessionId, Action<ItemPlaybackOptionsWindow> inside, bool accept = true)
    {
        var list = (ListBox)window.FindName("SessionOrderList");
        var row = list.Items.Cast<object>().FirstOrDefault(item => SessionIdOf(item) == sessionId)
            ?? throw new Exception($"Na liście sesji nie ma {sessionId}.");
        list.SelectedItem = row;

        Exception? inner = null;
        var opened = false;
        _onOptionsDialog = dialog =>
        {
            opened = true;
            try
            {
                inside(dialog);
                ClickButton(Walk(dialog).OfType<Button>().Single(button => accept ? button.IsDefault : button.IsCancel));
            }
            catch (Exception exception)
            {
                inner = exception;
                try { dialog.DialogResult = false; } catch (InvalidOperationException) { dialog.Close(); }
            }
        };
        try
        {
            var button = (Button)window.FindName("SessionPlaybackOptionsButton");
            ClickButton(button);
        }
        finally { _onOptionsDialog = null; }

        if (inner is not null) throw inner;
        if (!opened) throw new Exception($"Przycisk Ustawień nie otworzył opcji sesji {sessionId}.");
    }

    /// <summary>Blokada przypadkowego otwarcia podokna w przypadkach, które go nie używają.</summary>
    private sealed class NoDialog : IDisposable
    {
        public NoDialog() => _onOptionsDialog = _ => throw new Exception("Nieoczekiwane podokno opcji sesji.");
        public void Dispose() => _onOptionsDialog = null;
    }

    private static string SessionIdOf(object row) =>
        row.GetType().GetProperty("SessionId")?.GetValue(row)?.ToString() ?? string.Empty;

    private static void Select(Window dialog, string boxName, string label)
    {
        var box = (ComboBox)dialog.FindName(boxName)
            ?? throw new Exception($"Okno opcji nie ma pola {boxName}.");
        var choice = box.Items.Cast<object>()
            .FirstOrDefault(item => string.Equals(item.ToString(), label, StringComparison.Ordinal))
            ?? throw new Exception($"Pole {boxName} nie ma wyboru \"{label}\".");
        box.SelectedItem = choice;
    }

    private static void CheckSelected(Window dialog, string boxName, string label)
    {
        var box = (ComboBox)dialog.FindName(boxName);
        Check(string.Equals(box.SelectedItem?.ToString(), label, StringComparison.Ordinal),
            $"Pole {boxName} pokazuje \"{box.SelectedItem}\" zamiast zapisanego \"{label}\".");
    }

    private static void AssertOverrides(
        AppSettings settings,
        string sessionId,
        bool? loudness,
        bool? smooth,
        int? silence,
        bool? exitPause,
        ResumePositionMode resume)
    {
        var saved = settings.Audio.OverridesBySession.GetValueOrDefault(sessionId)
            ?? throw new Exception($"Brak zapisanych opcji sesji {sessionId}.");
        Check(saved.LoudnessNormalizationOverride == loudness,
            $"Sesja {sessionId}: normalizacja {saved.LoudnessNormalizationOverride} zamiast {loudness}.");
        Check(saved.SmoothTrackTransitionsOverride == smooth,
            $"Sesja {sessionId}: przejścia {saved.SmoothTrackTransitionsOverride} zamiast {smooth}.");
        Check(saved.InterTrackSilenceMillisecondsOverride == silence,
            $"Sesja {sessionId}: cisza {saved.InterTrackSilenceMillisecondsOverride} zamiast {silence}.");
        Check(saved.PausePlaybackWhenLeavingPlayerOverride == exitPause,
            $"Sesja {sessionId}: wstrzymywanie {saved.PausePlaybackWhenLeavingPlayerOverride} zamiast {exitPause}.");
        Check(ResumePositionPolicy.GetSessionMode(settings, sessionId) == resume,
            $"Sesja {sessionId}: pamięć pozycji nie jest {resume}.");
    }

    private static string FindSource(string name)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var src = Path.Combine(directory.FullName, "src");
            if (Directory.Exists(src))
            {
                var found = Directory.GetFiles(src, name, SearchOption.AllDirectories);
                if (found.Length > 0) return found[0];
            }
            directory = directory.Parent;
        }
        throw new Exception($"Nie znaleziono pliku źródłowego {name}.");
    }

    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }

    private static IEnumerable<DependencyObject> Walk(DependencyObject root)
    {
        yield return root;
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
            foreach (var descendant in Walk(child)) yield return descendant;
    }
}
