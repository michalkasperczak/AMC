using AccessibleMediaController.Core.Commands;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Input;
using AccessibleMediaController.Core.Presentation;

/// <summary>
/// Wspolne podglady Alt+R, Alt+Shift+R i Ctrl+I maja byc dostepne ze WSZYSTKICH
/// sesji AMC, a Escape ma wracac dokladnie tam, skad je wywolano. Pomiar dotyczy
/// modelu polityki i pamieci powrotu, nie tekstu zrodel.
/// </summary>
internal static class TransientPreviewNavigationTests
{
    internal static void Run()
    {
        TestDomyslnieWszystkieSesje();
        TestWylaczonyPrzelacznikZostawiaSesjeMacierzyste();
        TestEscapeWracaDoMiejscaWywolania();
        TestEscapeZOdtwarzaczaZrodla();
        TestPrzelaczanieMiedzyPodgladamiNieGubiZrodla();
        TestSwiadomyEnterNieCofaDoDawnegoSluchania();
        TestJawnaNawigacjaUsuwaCelPowrotu();
        TestPrzelacznikTrwalyPrzezZapisIOdczyt();
        TestOpisyIPaletaZgodneZeSkrotami();
    }

    private static void True(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static void Equal<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new Exception($"{message} Oczekiwano „{expected}”, otrzymano „{actual}”.");
    }

    private static readonly TransientPreviewKind[] AllPreviews =
    [
        TransientPreviewKind.ActiveRadioRecordings,
        TransientPreviewKind.RecordedRadioFiles,
        TransientPreviewKind.PodcastInbox
    ];

    private static readonly string[] AllSessions =
        ["radio", "local", "podcasts", "tidal", "spotify", "wiim", "youtube"];

    private static void TestDomyslnieWszystkieSesje()
    {
        True(
            new AppSettings().GlobalTransientPreviews,
            "Wspólne podglądy mają być domyślnie włączone dla wszystkich sesji.");
        True(
            ConfigurationStore.CreateDefaultState().Settings.GlobalTransientPreviews,
            "Domyślny stan konfiguracji musi mieć włączone wspólne podglądy.");

        foreach (var preview in AllPreviews)
        {
            foreach (var session in AllSessions)
            {
                True(
                    TransientPreviewPolicy.IsAvailable(preview, session, globalPreviews: true),
                    $"Podgląd {preview} musi być dostępny w sesji {session} przy włączonym przełączniku.");
            }
        }
    }

    private static void TestWylaczonyPrzelacznikZostawiaSesjeMacierzyste()
    {
        True(
            TransientPreviewPolicy.IsAvailable(TransientPreviewKind.ActiveRadioRecordings, "radio", false),
            "Nagrywane stacje muszą działać w sesji Radio nawet przy wyłączonym przełączniku.");
        True(
            !TransientPreviewPolicy.IsAvailable(TransientPreviewKind.ActiveRadioRecordings, "tidal", false),
            "Wyłączony przełącznik nie może udostępniać nagrywanych stacji w TIDAL-u.");

        True(
            TransientPreviewPolicy.IsAvailable(TransientPreviewKind.RecordedRadioFiles, "radio", false)
            && TransientPreviewPolicy.IsAvailable(TransientPreviewKind.RecordedRadioFiles, "local", false),
            "Historia nagrywania ma dwie sesje macierzyste: Radio i Pliki lokalne.");
        True(
            !TransientPreviewPolicy.IsAvailable(TransientPreviewKind.RecordedRadioFiles, "podcasts", false),
            "Wyłączony przełącznik nie może udostępniać historii nagrywania w Podcastach.");

        True(
            TransientPreviewPolicy.IsAvailable(TransientPreviewKind.PodcastInbox, "podcasts", false),
            "Nowe odcinki muszą działać w sesji Podcasty przy wyłączonym przełączniku.");
        True(
            !TransientPreviewPolicy.IsAvailable(TransientPreviewKind.PodcastInbox, "local", false),
            "Wyłączony przełącznik nie może udostępniać nowych odcinków w Plikach lokalnych.");

        foreach (var preview in AllPreviews)
        {
            var message = TransientPreviewPolicy.DescribeUnavailable(preview);
            True(
                message.Contains("Ustawieniach", StringComparison.Ordinal),
                $"Komunikat o niedostępnym podglądzie {preview} ma wskazywać przełącznik w Ustawieniach.");
        }
    }

    private static void TestEscapeWracaDoMiejscaWywolania()
    {
        var navigator = new TransientPreviewNavigator();
        var origin = new TransientPreviewLocation(
            SessionId: "tidal",
            ViewName: "TIDAL: Moja kolekcja",
            Filter: "koncert",
            SelectedItemId: "tidal-track-7",
            PlayerActive: false);

        navigator.BeginPreview(TransientPreviewKind.RecordedRadioFiles, origin);
        True(navigator.HasPendingReturn, "Po wejściu w podgląd musi istnieć cel powrotu.");

        var escape = navigator.HandleEscape();
        Equal(TransientPreviewEscapeKind.ReturnToOrigin, escape.Kind, "Escape z podglądu ma wracać do źródła.");
        Equal("tidal", escape.Origin!.SessionId, "Escape zgubił sesję źródłową.");
        Equal("TIDAL: Moja kolekcja", escape.Origin!.ViewName, "Escape zgubił widok źródłowy.");
        Equal("koncert", escape.Origin!.Filter, "Escape zgubił filtr źródłowy.");
        Equal("tidal-track-7", escape.Origin!.SelectedItemId, "Escape zgubił zaznaczony element źródłowy.");
        True(!escape.RestorePlayer, "Podgląd wywołany z listy nie może wracać do odtwarzacza.");
        True(!navigator.HasPendingReturn, "Po powrocie cel Escape musi zniknąć.");

        Equal(
            TransientPreviewEscapeKind.NotHandled,
            navigator.HandleEscape().Kind,
            "Drugi Escape nie może być przechwycony przez podglądy.");
    }

    private static void TestEscapeZOdtwarzaczaZrodla()
    {
        var navigator = new TransientPreviewNavigator();
        navigator.BeginPreview(
            TransientPreviewKind.ActiveRadioRecordings,
            new TransientPreviewLocation("spotify", "Biblioteka", PlayerActive: true));

        var escape = navigator.HandleEscape();
        Equal(TransientPreviewEscapeKind.ReturnToOrigin, escape.Kind, "Escape ma wracać do źródła.");
        True(escape.RestorePlayer, "Podgląd wywołany z odtwarzacza musi wracać do odtwarzacza.");
        Equal("spotify", escape.Origin!.SessionId, "Powrót do odtwarzacza zgubił sesję.");
    }

    private static void TestPrzelaczanieMiedzyPodgladamiNieGubiZrodla()
    {
        var navigator = new TransientPreviewNavigator();
        var origin = new TransientPreviewLocation("wiim", "Ulubione", SelectedItemId: "wiim-3");

        navigator.BeginPreview(TransientPreviewKind.PodcastInbox, origin);
        navigator.BeginPreview(TransientPreviewKind.ActiveRadioRecordings, new TransientPreviewLocation("radio", "Nagrywane"));
        navigator.BeginPreview(TransientPreviewKind.RecordedRadioFiles, new TransientPreviewLocation("local", "Historia nagrywania"));
        // Powtorny ten sam skrot tez nie moze przesunac celu powrotu.
        navigator.BeginPreview(TransientPreviewKind.RecordedRadioFiles, new TransientPreviewLocation("local", "Historia nagrywania"));

        var escape = navigator.HandleEscape();
        Equal("wiim", escape.Origin!.SessionId, "Przełączanie między podglądami zgubiło pierwotną sesję.");
        Equal("Ulubione", escape.Origin!.ViewName, "Przełączanie między podglądami zgubiło pierwotny widok.");
        Equal("wiim-3", escape.Origin!.SelectedItemId, "Przełączanie między podglądami zgubiło pierwotny element.");
    }

    private static void TestSwiadomyEnterNieCofaDoDawnegoSluchania()
    {
        var navigator = new TransientPreviewNavigator();
        navigator.BeginPreview(
            TransientPreviewKind.RecordedRadioFiles,
            new TransientPreviewLocation("radio", "Ulubione", PlayerActive: true));

        // Uzytkownik swiadomie wlaczyl Enterem nagranie z podgladu.
        navigator.NotePlaybackStartedFromPreview();

        var first = navigator.HandleEscape();
        Equal(
            TransientPreviewEscapeKind.ReturnToPreview,
            first.Kind,
            "Escape z odtwarzacza otwartego z podglądu ma najpierw wracać do podglądu.");
        Equal(
            TransientPreviewKind.RecordedRadioFiles,
            first.Preview,
            "Powrót z odtwarzacza wskazał zły podgląd.");

        var second = navigator.HandleEscape();
        Equal(TransientPreviewEscapeKind.ReturnToOrigin, second.Kind, "Drugi Escape ma wracać do źródła.");
        True(
            second.RestorePlayer,
            "Powrót ma przywrócić widok odtwarzacza, bez polecenia wznowienia dawnego słuchania.");
        Equal("radio", second.Origin!.SessionId, "Powrót do źródła zgubił sesję.");
    }

    private static void TestJawnaNawigacjaUsuwaCelPowrotu()
    {
        var navigator = new TransientPreviewNavigator();
        navigator.BeginPreview(
            TransientPreviewKind.PodcastInbox,
            new TransientPreviewLocation("local", "Biblioteka"));

        navigator.NoteExplicitNavigation();

        True(!navigator.HasPendingReturn, "Jawna nawigacja musi unieważnić cel powrotu.");
        Equal(
            TransientPreviewEscapeKind.NotHandled,
            navigator.HandleEscape().Kind,
            "Po jawnej nawigacji Escape nie może wracać do nieaktualnego źródła.");
    }

    private static void TestPrzelacznikTrwalyPrzezZapisIOdczyt()
    {
        var root = Path.Combine(Path.GetTempPath(), "amc-global-previews-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "state.json");
            var state = ConfigurationStore.CreateDefaultState();
            state.Settings.GlobalTransientPreviews = false;
            new ConfigurationStore(path).Save(state);

            var loaded = new ConfigurationStore(path).LoadOrCreate();
            True(
                !loaded.Settings.GlobalTransientPreviews,
                "Wyłączony przełącznik wspólnych podglądów musi przetrwać zapis i restart.");

            // Klon roboczy okna Ustawien nie moze dzielic wartosci z zywym stanem.
            var working = new ConfigurationStore(path).CloneState(loaded);
            working.Settings.GlobalTransientPreviews = true;
            True(
                !loaded.Settings.GlobalTransientPreviews,
                "Anulowanie Ustawień nie może zmieniać żywego stanu: klon musi być niezależny.");

            new ConfigurationStore(path).Save(working);
            True(
                new ConfigurationStore(path).LoadOrCreate().Settings.GlobalTransientPreviews,
                "Zapis Ustawień nie utrwalił włączonego przełącznika wspólnych podglądów.");
        }
        finally { Directory.Delete(root, true); }
    }

    private static void TestOpisyIPaletaZgodneZeSkrotami()
    {
        var entries = CommandPaletteSearch.CreateEntries(
            KeyboardProfile.CreateDefault(),
            new AppSettings()).ToArray();

        Equal(
            "Alt+R",
            entries.Single(entry => entry.CommandId == CommandIds.ViewActiveRadioRecordings).LocalShortcut,
            "Paleta nadal ogranicza Alt+R do jednej sesji.");
        Equal(
            "Alt+Shift+R",
            entries.Single(entry => entry.CommandId == CommandIds.ViewRecordedRadioFiles).LocalShortcut,
            "Paleta podaje zły skrót historii nagrywania.");
        Equal(
            "Ctrl+I",
            entries.Single(entry => entry.CommandId == CommandIds.ViewPodcastInbox).LocalShortcut,
            "Paleta nadal ogranicza Ctrl+I do jednej sesji.");

        True(
            CommandCatalog.GetDisplayName(CommandIds.SettingsGlobalTransientPreviews).Length > 0,
            "Przełącznik wspólnych podglądów musi mieć nazwę w katalogu poleceń.");
        Equal(
            TransientPreviewKind.ActiveRadioRecordings,
            TransientPreviewPolicy.FromCommandId(CommandIds.ViewActiveRadioRecordings),
            "Odwzorowanie polecenia na podgląd nagrywanych stacji jest błędne.");
        Equal(
            CommandIds.ViewPodcastInbox,
            TransientPreviewPolicy.ToCommandId(TransientPreviewKind.PodcastInbox),
            "Odwzorowanie podglądu nowych odcinków na polecenie jest błędne.");
    }
}
