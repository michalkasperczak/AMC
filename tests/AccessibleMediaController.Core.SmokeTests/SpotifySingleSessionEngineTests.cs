using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Playback;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Core.Spotify;

/// <summary>
/// Spotify jest JEDNA sesja z dwoma silnikami. Ten test zastapil dawny test
/// "dwie niezalezne sesje": zamiast dowodzic istnienia drugiej sesji, dowodzi ze
/// druga sesja NIE MOZE powstac, a oba silniki sa nadal osiagalne - wyborem
/// zapisanym w ustawieniach, a nie osobna sesja.
/// </summary>
internal static class SpotifySingleSessionEngineTests
{
    internal static void Run()
    {
        DowodzeBrakuDrugiejSesji();
        DowodzeWyboruSilnikaZZapisu();
        DowodzeStalegoSilnikaWTrakcieGrania();
        DowodzeZachowaniaSlotowInnychSesji();
        DowodzeJednychDanychKanonicznych();
        Console.WriteLine("OK: jedna sesja Spotify, silnik z zapisu (Librespot/Sdk), sloty innych sesji bez zmian");
    }

    /// <summary>
    /// Zadna droga nie wolno dorobic drugiej sesji Spotify. Dawna metoda
    /// rejestrujaca sesje Librespot musi byc usunieta, nie tylko nieuzywana:
    /// dopoki istnieje, kolejny kod moze ja zawolac i uzytkownik znow dostanie
    /// dwie listy tego samego konta.
    /// </summary>
    private static void DowodzeBrakuDrugiejSesji()
    {
        var register = typeof(SessionManager).GetMethod(
            "RegisterSpotifyLibrespotSession",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        if (register is not null)
            throw new Exception("Nadal istnieje rejestracja drugiej sesji Spotify Librespot.");

        var settings = new AppSettings();
        var manager = new SessionManager(
            settings,
            spotifyOutput: new ProbeOutput(),
            spotifyLibrespotOutput: new ProbeOutput());
        var spotifySessions = manager.Sessions
            .Where(session => SpotifyPlaybackSettingsResolver.IsSpotifySession(session.Id))
            .ToArray();
        if (spotifySessions.Length != 1)
            throw new Exception($"Sesji Spotify jest {spotifySessions.Length}, a musi byc dokladnie jedna.");
        if (spotifySessions[0].Id != "spotify")
            throw new Exception("Jedyna sesja Spotify nie ma kanonicznego identyfikatora.");
        if (manager.Sessions.Any(session => session.Id == SpotifySessionMigration.LegacySessionId))
            throw new Exception("Stara sesja spotifyLibrespot nadal jest rejestrowana.");
        if (manager.SessionSlots.Values.Any(id =>
                string.Equals(id, SpotifySessionMigration.LegacySessionId, StringComparison.OrdinalIgnoreCase)))
            throw new Exception("Numer sesji nadal wskazuje stara sesje Librespot.");
    }

    /// <summary>
    /// Wybor silnika jest REALNY: sesja gra tym wyjsciem, ktore odpowiada
    /// zapisanej wartosci. Sama obecnosc pola w ustawieniach niczego nie dowodzi.
    /// </summary>
    private static void DowodzeWyboruSilnikaZZapisu()
    {
        foreach (var (engine, opis) in new[]
                 {
                     (SpotifyPlaybackEngine.Librespot, "Librespot"),
                     (SpotifyPlaybackEngine.Sdk, "Sdk")
                 })
        {
            var settings = new AppSettings { SpotifyEngine = engine };
            var sdkOutput = new ProbeOutput();
            var librespotOutput = new ProbeOutput();
            var manager = new SessionManager(
                settings,
                spotifyOutput: sdkOutput,
                spotifyLibrespotOutput: librespotOutput);
            if (manager.ActiveSpotifyEngine != engine)
                throw new Exception($"SessionManager nie zglasza aktywnego silnika {opis}.");
            var session = manager.FindSession("spotify")!;
            var track = new MediaItem
            {
                Id = "spotify:track:one",
                ExternalId = "one",
                Source = "spotify:track:one",
                Duration = TimeSpan.FromMinutes(3)
            };
            session.ReplaceItems([track]);
            session.Play(track);
            var oczekiwane = engine == SpotifyPlaybackEngine.Librespot ? librespotOutput : sdkOutput;
            var niechciane = engine == SpotifyPlaybackEngine.Librespot ? sdkOutput : librespotOutput;
            if (oczekiwane.Plays != 1)
                throw new Exception($"Zapis {opis} nie skierowal odtwarzania na wlasciwe wyjscie.");
            if (niechciane.Plays != 0)
                throw new Exception($"Przy zapisie {opis} zagralo rowniez drugie wyjscie.");
        }

        // Nieznana wartosc w pliku nie moze zostawic sesji bez toru: Librespot
        // jest domyslny i awaryjny jednoczesnie.
        var uszkodzone = new AppSettings { SpotifyEngine = (SpotifyPlaybackEngine)77 };
        var awaryjny = new SessionManager(
            uszkodzone,
            spotifyOutput: new ProbeOutput(),
            spotifyLibrespotOutput: new ProbeOutput());
        if (awaryjny.ActiveSpotifyEngine != SpotifyPlaybackEngine.Librespot)
            throw new Exception("Uszkodzona nazwa silnika nie wraca na Librespot.");

        // Brak skladnika Librespot nie moze uciszyc sesji - zostaje SDK.
        var bezLibrespot = new AppSettings { SpotifyEngine = SpotifyPlaybackEngine.Librespot };
        var sdkZapas = new ProbeOutput();
        var zapasowy = new SessionManager(bezLibrespot, spotifyOutput: sdkZapas);
        var sesjaZapas = zapasowy.FindSession("spotify")!;
        var utwor = new MediaItem { Id = "spotify:track:two", Source = "spotify:track:two" };
        sesjaZapas.ReplaceItems([utwor]);
        sesjaZapas.Play(utwor);
        if (sdkZapas.Plays != 1)
            throw new Exception("Brak wyjscia Librespot uciszyl sesje zamiast spasc na SDK.");
    }

    /// <summary>
    /// Zapis ustawien w czasie sluchania NIE MOZE przelaczyc toru pod grajacym
    /// utworem. Silnik jest utrwalony przy tworzeniu sesji.
    /// </summary>
    private static void DowodzeStalegoSilnikaWTrakcieGrania()
    {
        var settings = new AppSettings { SpotifyEngine = SpotifyPlaybackEngine.Librespot };
        var sdkOutput = new ProbeOutput();
        var librespotOutput = new ProbeOutput();
        var manager = new SessionManager(
            settings,
            spotifyOutput: sdkOutput,
            spotifyLibrespotOutput: librespotOutput);
        var session = manager.FindSession("spotify")!;
        var track = new MediaItem { Id = "spotify:track:live", Source = "spotify:track:live" };
        session.ReplaceItems([track]);
        session.Play(track);
        var stops = librespotOutput.Stops;

        settings.SpotifyEngine = SpotifyPlaybackEngine.Sdk;
        session.SetPosition(TimeSpan.FromSeconds(42));
        if (manager.ActiveSpotifyEngine != SpotifyPlaybackEngine.Librespot)
            throw new Exception("Zapis ustawien zmienil aktywny silnik grajacej sesji.");
        if (sdkOutput.Plays != 0 || librespotOutput.Stops != stops)
            throw new Exception("Zapis ustawien przerwal odtwarzanie albo przelaczyl tor w locie.");
        if (librespotOutput.Position != TimeSpan.FromSeconds(42))
            throw new Exception("Przewijanie po zapisie ustawien poszlo na inny tor.");
    }

    /// <summary>Numery pozostalych sesji sa wyuczone na pamiec - nie wolno ich ruszac.</summary>
    private static void DowodzeZachowaniaSlotowInnychSesji()
    {
        // Najpierw sama normalizacja: luka po zwolnionym numerze NIE MOZE
        // przesunac dalszych sesji. Stary kod zageszczal numery "po kolei", wiec
        // scalenie Spotify zmienialoby Alt+cyfra dla sesji, ktorych nikt nie tknal.
        var zLuka = SessionSlotOrder.Normalize(new Dictionary<int, string>
        {
            [1] = "radio", [2] = "local", [4] = "tidal", [5] = "spotify",
            [6] = "podcasts", [7] = "wiim", [8] = "appleMusic"
        });
        foreach (var pair in new Dictionary<int, string>
                 {
                     [1] = "radio", [2] = "local", [4] = "tidal", [5] = "spotify",
                     [6] = "podcasts", [7] = "wiim", [8] = "appleMusic"
                 })
        {
            if (!zLuka.TryGetValue(pair.Key, out var occupant)
                || !string.Equals(occupant, pair.Value, StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception(
                    $"Normalizacja przesunela numer sesji {pair.Key} po zwolnieniu numeru 3.");
            }
        }
        if (zLuka.ContainsKey(3))
            throw new Exception("Normalizacja zapchala zwolniony numer 3 obca sesja.");

        // Zapis i odczyt stanu tez nie moze przenumerowac slotow: ConfigurationStore
        // wola Normalize po obu stronach.
        var folderSlotow = Path.Combine(Path.GetTempPath(), "amc-spotify-slots-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folderSlotow);
        try
        {
            var zapisywane = new PersistedState();
            zapisywane.Settings.SessionSlots = new Dictionary<int, string>(zLuka);
            var store = new ConfigurationStore(Path.Combine(folderSlotow, "state.json"));
            store.Save(zapisywane);
            var odczytane = store.LoadOrCreate().Settings.SessionSlots;
            foreach (var pair in zLuka)
            {
                if (!odczytane.TryGetValue(pair.Key, out var occupant)
                    || !string.Equals(occupant, pair.Value, StringComparison.OrdinalIgnoreCase))
                {
                    throw new Exception($"Zapis i odczyt stanu przestawil numer sesji {pair.Key}.");
                }
            }
        }
        finally
        {
            Directory.Delete(folderSlotow, true);
        }

        var settings = new AppSettings
        {
            SessionSlots = new Dictionary<int, string>
            {
                [1] = "radio", [2] = "local", [3] = "wiim", [4] = "tidal",
                [5] = "appleMusic", [6] = "podcasts", [7] = "spotify"
            }
        };
        var oczekiwane = new Dictionary<int, string>(settings.SessionSlots);
        var manager = new SessionManager(
            settings,
            spotifyOutput: new ProbeOutput(),
            spotifyLibrespotOutput: new ProbeOutput());
        foreach (var pair in oczekiwane)
        {
            if (!manager.SessionSlots.TryGetValue(pair.Key, out var occupant)
                || !string.Equals(occupant, pair.Value, StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception($"Scalenie Spotify przestawilo numer sesji {pair.Key}.");
            }
        }
        if (manager.SessionSlots.Count != oczekiwane.Count)
            throw new Exception("Scalenie Spotify dopisalo albo usunelo numer sesji.");
    }

    /// <summary>
    /// Po scaleniu WSZYSTKIE dane Spotify (pozycje, wyciszenie, urzadzenie) maja
    /// jeden kanoniczny klucz. To zastapilo dawny dowod na "niezalezne zapisy
    /// dwoch sesji".
    /// </summary>
    private static void DowodzeJednychDanychKanonicznych()
    {
        var settings = new AppSettings();
        var track = new MediaItem
        {
            Id = "same-item",
            Source = "spotify:track:same",
            Duration = TimeSpan.FromMinutes(4)
        };
        SpotifyPlaybackSettingsResolver.SetItemMode(settings, track, ResumePositionMode.Remember, "spotify");
        SpotifyPlaybackSettingsResolver.StorePosition(settings, track, TimeSpan.FromSeconds(71), "spotify");
        // Ten sam klucz dla starego identyfikatora: po scaleniu nie ma osobnej
        // pamieci Librespot, wiec oba zapytania musza dac TO SAMO.
        var kanoniczna = SpotifyPlaybackSettingsResolver.ResolvePosition(settings, track, "spotify");
        var legacy = SpotifyPlaybackSettingsResolver.ResolvePosition(
            settings, track, SpotifySessionMigration.LegacySessionId);
        if (kanoniczna != TimeSpan.FromSeconds(71))
            throw new Exception("Kanoniczna sesja Spotify zgubila zapamietany czas.");
        if (legacy != kanoniczna)
            throw new Exception("Stary identyfikator Librespot ma nadal wlasna, osobna pamiec czasu.");

        var folder = Path.Combine(Path.GetTempPath(), "amc-spotify-unify-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            settings.SpotifyLibrespotDeviceName = "Wyjście USB testowe";
            settings.SpotifyEngine = SpotifyPlaybackEngine.Sdk;
            var config = new ConfigurationStore(Path.Combine(folder, "state.json"));
            config.Save(new PersistedState { Settings = settings });
            var restored = config.LoadOrCreate().Settings;
            if (restored.SpotifyEngine != SpotifyPlaybackEngine.Sdk)
                throw new Exception("Wybor silnika nie przezyl zapisu i odczytu stanu.");
            if (restored.SpotifyLibrespotDeviceName != "Wyjście USB testowe")
                throw new Exception("Zapis zgubil wybrane wyjscie Librespot.");
            if (SpotifyPlaybackSettingsResolver.ResolvePosition(restored, track, "spotify") != TimeSpan.FromSeconds(71))
                throw new Exception("Zapis zgubil zapamietany czas sesji Spotify.");
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    private sealed class ProbeOutput : IMediaOutput
    {
        public string? LoadedItemId { get; private set; }
        public TimeSpan Position { get; private set; }
        public bool SupportsPlaybackRate => false;
        public int Plays;
        public int Stops;
        public void Play(MediaItem item, TimeSpan position, int volume, double playbackRate)
        {
            Plays++;
            LoadedItemId = item.Id;
            Position = position;
        }
        public void Pause() { }
        public void Stop() { Stops++; }
        public void Seek(TimeSpan position) { Position = position; }
        public void SetVolume(int volume) { }
        public void SetPlaybackRate(double playbackRate) { }
    }
}
