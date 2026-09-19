using System.Text.Json;
using System.Text.Json.Nodes;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Core.Spotify;

/// <summary>
/// Scalenie dwoch sesji Spotify w jedna i trwaly wybor silnika.
/// Kazda proba uzywa DANYCH PROBNYCH tworzonych w tym pliku i katalogu
/// tymczasowego - nigdy stanu uzytkownika.
/// </summary>
internal static class SpotifySessionUnificationTests
{
    internal static void Run()
    {
        VerifyEngineContract();
        VerifyMergeOfBothQueuesAndPositions();
        VerifyIdempotence();
        VerifyIdAndPositionConflictRule();
        VerifyPersistenceThroughConfigurationStore();
        VerifyOldFileWithoutNewField();
        VerifyInvalidEngineFallsBackSafely();
        VerifyOtherSessionsAndSlotsSurvive();
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    // ---- kontrakt silnika -------------------------------------------------

    private static void VerifyEngineContract()
    {
        Check(Enum.GetNames<SpotifyPlaybackEngine>().SequenceEqual(["Librespot", "Sdk"]),
            "Kontrakt silnika Spotify zmienil nazwy albo kolejnosc wartosci.");
        Check(new AppSettings().SpotifyEngine == SpotifyPlaybackEngine.Librespot,
            "Domyslnym silnikiem Spotify nie jest Librespot.");
        Check(SpotifyPlaybackEngineRules.Describe(SpotifyPlaybackEngine.Sdk).Contains("SDK", StringComparison.Ordinal)
            && !SpotifyPlaybackEngineRules.Describe(SpotifyPlaybackEngine.Librespot).Equals("Librespot", StringComparison.Ordinal),
            "Etykieta silnika dla czytnika ekranu jest sama nazwa elementu enum.");
    }

    // ---- scalenie obu kolejek i pozycji ----------------------------------

    private static void VerifyMergeOfBothQueuesAndPositions()
    {
        var state = CreateProbeState();
        var result = SpotifySessionMigration.Apply(state);
        Check(result.Applied, "Migracja nie wykonala sie na stanie sprzed scalenia.");

        var queue = state.RemoteQueues.ItemsBySession["spotify"];
        Check(!state.RemoteQueues.ItemsBySession.ContainsKey("spotifyLibrespot"),
            "Kolejka starej sesji Librespot zostala jako druga, osobna kolejka.");
        // Utwor wspolny obu kolejkom musi zostac JEDNYM wierszem.
        Check(queue.Count == 3, $"Scalenie kolejek dalo {queue.Count} wierszy zamiast trzech.");
        Check(queue.Count(item => item.ExternalId == "wspolny") == 1,
            "Utwor obecny w obu kolejkach zostal zdublowany zamiast scalony po tozsamosci.");
        Check(queue.Any(item => item.ExternalId == "tylko-librespot"),
            "Utwor z kolejki Librespot zaginal przy scalaniu.");
        Check(queue.Any(item => item.ExternalId == "tylko-sdk"),
            "Utwor z kolejki SDK zaginal przy scalaniu.");

        // Identyfikator kopii Librespot dopasowany do kanonicznej sesji.
        var przeniesiony = queue.Single(item => item.ExternalId == "tylko-librespot");
        Check(!przeniesiony.Id.StartsWith("spotifyLibrespot:", StringComparison.Ordinal),
            "Przeniesiony wiersz nadal ma identyfikator starej sesji.");
        Check(przeniesiony.Id.StartsWith("spotify:", StringComparison.Ordinal),
            "Przeniesiony wiersz nie dostal kanonicznego identyfikatora Spotify.");

        // Pozycja obecna tylko w Librespot przechodzi zawsze (regula c).
        var tylkoLibrespot = new MediaItem { Source = "spotify:track:tylko-librespot" };
        Check(SpotifyPlaybackSettingsResolver.ResolvePosition(state.Settings, tylkoLibrespot, "spotify")
                == TimeSpan.FromSeconds(95),
            "Bezkonfliktowa pozycja z Librespot nie trafila do sesji kanonicznej.");

        // Archiwum ma pelne zrodlo, nic nie zostalo skasowane.
        var archive = state.Settings.SpotifyLegacySession;
        Check(archive is not null && archive.SessionId == "spotifyLibrespot",
            "Brak archiwum starej sesji Librespot.");
        Check(archive!.RemoteQueue.Count == 2, "Archiwum nie zachowalo zrodlowej kolejki Librespot.");
        Check(archive.Playback.ItemsByKey.Count == 2, "Archiwum nie zachowalo zrodlowych pozycji Librespot.");
        Check(archive.CollectionOrders.ContainsKey("queue"), "Archiwum nie zachowalo kolejnosci list Librespot.");
        Check(archive.SearchHistory.Count == 1 && archive.PlaybackHistoryItemIds.Count == 1,
            "Archiwum nie zachowalo historii starej sesji.");

        // Listy i sortowania.
        var nav = state.SessionNavigation.Sessions["spotify"];
        Check(!state.SessionNavigation.Sessions.ContainsKey("spotifyLibrespot"),
            "Stan nawigacji starej sesji zostal osobno.");
        // "Biblioteka" ma sortowanie w OBU sesjach - to konflikt, wiec wygrywa
        // sesja aktywna wczesniej (w probie: Librespot).
        Check(nav.CollectionSortModes["Biblioteka"] == CollectionSortMode.Custom,
            "Konflikt sortowania listy nie zostal rozstrzygniety regula pierwszenstwa.");
        Check(nav.CollectionSortModes["Kolejka"] == CollectionSortMode.AddedNewest,
            "Sortowanie znane tylko Librespot nie zostalo wniesione.");
        Check(state.Settings.SpotifyLegacySession!.ConflictNotes.Any(note =>
                note.Contains("Sortowanie listy Biblioteka", StringComparison.Ordinal)),
            "Konflikt sortowania nie zostal opisany w archiwum.");

        var queueOrder = state.CollectionOrders.QueueItemIdsBySession["spotify"];
        Check(queueOrder.Count == 3 && queueOrder[0] == "spotify:tylko-sdk",
            "Scalenie kolejnosci listy przestawilo wiersze sesji kanonicznej.");
        Check(queueOrder.All(id => !id.StartsWith("spotifyLibrespot:", StringComparison.Ordinal)),
            "Kolejnosc listy nadal wskazuje identyfikatory starej sesji.");

        // Wyciszenie i wyjscie audio.
        Check(!state.Settings.Audio.SessionMutedById.ContainsKey("spotifyLibrespot")
            && !state.Settings.Audio.OutputDeviceIdsBySession.ContainsKey("spotifyLibrespot"),
            "Wyciszenie albo wyjscie starej sesji zostalo jako osobny wpis.");
        Check(state.Settings.Audio.OutputDeviceIdsBySession["spotify"] == "urzadzenie-librespot",
            "Wyjscie audio aktywnej wczesniej sesji nie wygralo rozstrzygania.");

        // Playlisty, zakladki, presety i glosnosci przepiete na kanoniczna sesje.
        Check(state.Playlists.Entries.All(entry => entry.SessionId == "spotify"),
            "Playlista starej sesji nadal wskazuje sesje Librespot.");
        Check(state.Bookmarks.Entries.All(entry => entry.SessionId == "spotify"),
            "Zakladka starej sesji nadal wskazuje sesje Librespot.");
        Check(state.PlaybackVolumes.Entries.All(entry => entry.SessionId == "spotify"),
            "Zapamietana glosnosc starej sesji nadal wskazuje sesje Librespot.");
        Check(state.SessionPresets.EntriesBySession["spotify"].Count == 2,
            "Preset Librespot z wolnym numerem nie zostal przeniesiony.");
    }

    // ---- idempotencja -----------------------------------------------------

    private static void VerifyIdempotence()
    {
        var state = CreateProbeState();
        var first = SpotifySessionMigration.Apply(state);
        var afterFirst = Snapshot(state);

        var second = SpotifySessionMigration.Apply(state);
        Check(!second.Applied, "Druga migracja tego samego stanu zglosila sie jako wykonana.");
        Check(Snapshot(state) == afterFirst, "Powtorzona migracja zmienila stan (kolejka albo prefsy zdublowane).");

        var third = SpotifySessionMigration.Apply(state);
        Check(!third.Applied && Snapshot(state) == afterFirst, "Trzecia migracja zmienila stan.");
        Check(state.Settings.SpotifySessionUnificationVersion == SpotifySessionMigration.Version,
            "Brak znacznika wersji scalenia w ustawieniach.");
        Check(first.MergedQueueItems > 0, "Pierwsza migracja nic nie przeniosla z kolejki proby.");
    }

    // ---- deterministyczna regula konfliktu -------------------------------

    private static void VerifyIdAndPositionConflictRule()
    {
        // (a) Ostatnio aktywna byla sesja SDK: to ona wygrywa konflikt.
        var sdkAktywna = CreateProbeState();
        sdkAktywna.Settings.LastSessionId = "spotify";
        SpotifySessionMigration.Apply(sdkAktywna);
        var wspolny = new MediaItem { Source = "spotify:track:wspolny" };
        Check(SpotifyPlaybackSettingsResolver.ResolvePosition(sdkAktywna.Settings, wspolny, "spotify")
                == TimeSpan.FromSeconds(71),
            "Przy aktywnej wczesniej sesji SDK konflikt pozycji rozstrzygnieto na rzecz Librespot.");
        Check(sdkAktywna.Settings.SpotifyLegacySession!.ConflictNotes.Any(note => note.Contains("wspolny", StringComparison.Ordinal)),
            "Konflikt pozycji nie zostal opisany w archiwum.");

        // (a) Ostatnio aktywny byl Librespot: wygrywa Librespot.
        var librespotAktywny = CreateProbeState();
        librespotAktywny.Settings.LastSessionId = "spotifyLibrespot";
        SpotifySessionMigration.Apply(librespotAktywny);
        Check(SpotifyPlaybackSettingsResolver.ResolvePosition(librespotAktywny.Settings, wspolny, "spotify")
                == TimeSpan.FromSeconds(23),
            "Przy aktywnym wczesniej Librespot konflikt pozycji rozstrzygnieto na rzecz SDK.");
        Check(librespotAktywny.Settings.LastSessionId == "spotify",
            "Ostatnia sesja nadal wskazuje identyfikator, ktorego po scaleniu nie ma.");

        // (b) Zadna z dwoch nie byla ostatnia aktywna: wygrywa Librespot.
        var obca = CreateProbeState();
        obca.Settings.LastSessionId = "radio";
        SpotifySessionMigration.Apply(obca);
        Check(SpotifyPlaybackSettingsResolver.ResolvePosition(obca.Settings, wspolny, "spotify")
                == TimeSpan.FromSeconds(23),
            "Bez ostatniej aktywnej sesji Spotify nie wygral domyslny silnik Librespot.");
        Check(obca.Settings.LastSessionId == "radio", "Migracja przestawila ostatnia sesje niezwiazana ze Spotify.");

        // Konflikt flag kolejki tego samego utworu.
        Check(obca.RemoteQueues.ItemsBySession["spotify"].Single(item => item.ExternalId == "wspolny").IsPlayNext,
            "Konflikt flag kolejki nie zostal rozstrzygniety regula pierwszenstwa.");

        // Przegrana wartosc jest do odczytania w archiwum, nie ginie.
        var archiwum = sdkAktywna.Settings.SpotifyLegacySession!;
        Check(archiwum.Playback.ItemsByKey["spotify:track:wspolny"].PositionTicks == TimeSpan.FromSeconds(23).Ticks,
            "Przegrana pozycja Librespot nie zostala zachowana w archiwum.");

        // Ten sam stan wejsciowy daje ten sam wynik - regula jest deterministyczna.
        var powtorka = CreateProbeState();
        powtorka.Settings.LastSessionId = "radio";
        SpotifySessionMigration.Apply(powtorka);
        var a = Snapshot(obca);
        var b = Snapshot(powtorka);
        Check(a == b, "Dwa przebiegi na tym samym wejsciu daly rozny wynik. " + FirstDifference(a, b));
    }

    // ---- trwalosc przez rzeczywisty ConfigurationStore -------------------

    private static void VerifyPersistenceThroughConfigurationStore()
    {
        var folder = NewTempFolder();
        try
        {
            var store = new ConfigurationStore(Path.Combine(folder, "state.json"));
            var state = CreateProbeState();
            SpotifySessionMigration.Apply(state);
            state.Settings.SpotifyEngine = SpotifyPlaybackEngine.Sdk;
            store.Save(state);

            var restored = store.LoadOrCreate();
            Check(restored.Settings.SpotifyEngine == SpotifyPlaybackEngine.Sdk,
                "Wybor silnika Spotify nie przezyl zapisu i ponownego odczytu.");
            Check(restored.Settings.SpotifySessionUnificationVersion == SpotifySessionMigration.Version,
                "Znacznik wersji scalenia nie przezyl zapisu.");
            Check(restored.Settings.SpotifyLegacySession is { RemoteQueue.Count: 2 },
                "Archiwum starej sesji nie przezylo zapisu i odczytu.");
            Check(restored.RemoteQueues.ItemsBySession["spotify"].Count == 3,
                "Scalona kolejka nie przezyla zapisu i odczytu.");

            // Ponowna migracja po odczycie z dysku nadal nic nie zmienia.
            var przed = Snapshot(restored);
            Check(!SpotifySessionMigration.Apply(restored).Applied && Snapshot(restored) == przed,
                "Migracja wykonala sie ponownie po odczycie stanu z dysku.");

            // Zapis MUSI byc w camelCase - kontrakt pliku ustawien.
            var json = JsonNode.Parse(File.ReadAllText(Path.Combine(folder, "state.json")))!.AsObject();
            var settingsNode = json["settings"]!.AsObject();
            Check(settingsNode.ContainsKey("spotifyEngine")
                && settingsNode.ContainsKey("spotifySessionUnificationVersion")
                && settingsNode.ContainsKey("spotifyLegacySession"),
                "Nowe pola Spotify nie sa zapisane w camelCase w pliku ustawien.");
            Check(settingsNode["spotifyEngine"]!.GetValue<string>() == "Sdk",
                "Silnik zapisano inaczej niz jako czytelna nazwe wartosci.");
        }
        finally { Directory.Delete(folder, true); }
    }

    // ---- stary plik bez nowego pola --------------------------------------

    private static void VerifyOldFileWithoutNewField()
    {
        var folder = NewTempFolder();
        try
        {
            var path = Path.Combine(folder, "state.json");
            // Plik sprzed scalenia: bez spotifyEngine, bez znacznika wersji,
            // za to z pelna druga sesja Librespot. Tak wyglada realny plik
            // po aktualizacji programu.
            File.WriteAllText(path, """
            {
              "schemaVersion": 54,
              "settings": {
                "lastSessionId": "spotifyLibrespot",
                "sessionSlots": { "1": "local", "2": "wiim", "3": "tidal", "4": "appleMusic", "5": "radio", "6": "podcasts", "7": "spotify", "8": "spotifyLibrespot" },
                "spotifyLibrespotPlayback": {
                  "itemsByKey": { "spotify:track:stary": { "resumePositionMode": "Remember", "positionTicks": 230000000 } }
                }
              },
              "remoteQueues": {
                "itemsBySession": {
                  "spotifyLibrespot": [ { "id": "spotifyLibrespot:stary", "externalId": "stary", "title": "Proba", "kind": "Track", "isInQueue": true } ]
                }
              }
            }
            """);

            var store = new ConfigurationStore(path);
            var state = store.LoadOrCreate();
            Check(state.Settings.SpotifyEngine == SpotifyPlaybackEngine.Librespot,
                "Stary plik bez pola silnika nie dostal domyslnego Librespot.");
            Check(state.Settings.SpotifySessionUnificationVersion == 0,
                "Stary plik zostal uznany za juz scalony.");

            var result = SpotifySessionMigration.Apply(state);
            Check(result.Applied, "Migracja pominela stary plik bez nowego pola.");
            Check(state.RemoteQueues.ItemsBySession["spotify"].Single().Id == "spotify:stary",
                "Wiersz kolejki ze starego pliku nie dostal kanonicznego identyfikatora.");
            Check(SpotifyPlaybackSettingsResolver.ResolvePosition(
                    state.Settings, new MediaItem { Source = "spotify:track:stary" }, "spotify").Ticks == 230000000,
                "Pozycja ze starego pliku nie trafila do sesji kanonicznej.");
            Check(state.Settings.LastSessionId == "spotify",
                "Ostatnia sesja ze starego pliku nadal wskazuje usunieta sesje Librespot.");

            store.Save(state);
            Check(store.LoadOrCreate().Settings.SpotifySessionUnificationVersion == SpotifySessionMigration.Version,
                "Scalenie starego pliku nie zostalo zapisane.");
        }
        finally { Directory.Delete(folder, true); }
    }

    // ---- niedozwolony silnik: bezpieczny powrot --------------------------

    private static void VerifyInvalidEngineFallsBackSafely()
    {
        Check(SpotifyPlaybackEngineRules.Parse("WebPlayer") == SpotifyPlaybackEngine.Librespot,
            "Nieznana nazwa silnika nie wrocila do Librespot.");
        Check(SpotifyPlaybackEngineRules.Parse(null) == SpotifyPlaybackEngine.Librespot
            && SpotifyPlaybackEngineRules.Parse("  ") == SpotifyPlaybackEngine.Librespot,
            "Pusta wartosc silnika nie wrocila do Librespot.");
        Check(SpotifyPlaybackEngineRules.Parse("sdk") == SpotifyPlaybackEngine.Sdk,
            "Poprawna nazwa silnika zapisana mala litera nie zostala odczytana.");
        Check(SpotifyPlaybackEngineRules.Normalize((SpotifyPlaybackEngine)77) == SpotifyPlaybackEngine.Librespot,
            "Wartosc spoza zakresu enum nie zostala znormalizowana.");

        var folder = NewTempFolder();
        try
        {
            var path = Path.Combine(folder, "state.json");
            File.WriteAllText(path, """
            {
              "schemaVersion": 54,
              "settings": { "spotifyEngine": "NieIstniejacySilnik", "spotifySessionUnificationVersion": 1 }
            }
            """);
            var state = new ConfigurationStore(path).LoadOrCreate();
            Check(state.Settings.SpotifyEngine == SpotifyPlaybackEngine.Librespot,
                "Niedozwolony silnik w pliku nie dal bezpiecznego powrotu do Librespot.");

            // Bezpieczny powrot NIE moze polegac na rzuceniu wyjatku: caly plik
            // ustawien uzytkownika przepadlby przez jedno zle slowo.
            File.WriteAllText(path, """
            {
              "schemaVersion": 54,
              "settings": { "spotifyEngine": 99, "lastSessionId": "radio", "spotifySessionUnificationVersion": 1 }
            }
            """);
            var liczbowy = new ConfigurationStore(path).LoadOrCreate();
            Check(liczbowy.Settings.SpotifyEngine == SpotifyPlaybackEngine.Librespot
                && liczbowy.Settings.LastSessionId == "radio",
                "Uszkodzona wartosc silnika przerwala odczyt reszty ustawien.");

            // Normalizacja dziala takze na stanie juz scalonym.
            liczbowy.Settings.SpotifyEngine = (SpotifyPlaybackEngine)42;
            SpotifySessionMigration.Apply(liczbowy);
            Check(liczbowy.Settings.SpotifyEngine == SpotifyPlaybackEngine.Librespot,
                "Migracja nie znormalizowala niedozwolonego silnika w juz scalonym stanie.");
        }
        finally { Directory.Delete(folder, true); }
    }

    // ---- inne sesje, numery i poswiadczenia ------------------------------

    private static void VerifyOtherSessionsAndSlotsSurvive()
    {
        var state = CreateProbeState();
        var przedSloty = state.Settings.SessionSlots
            .Where(pair => pair.Value is not "spotifyLibrespot")
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        var przedRadio = state.RemoteQueues.ItemsBySession["radio"].Count;
        var przedKonto = state.Spotify.ClientId;
        var przedCache = state.Spotify.CachedCollectionItems.Count;

        SpotifySessionMigration.Apply(state);

        foreach (var pair in przedSloty)
        {
            Check(state.Settings.SessionSlots.TryGetValue(pair.Key, out var teraz) && teraz == pair.Value,
                $"Scalenie przestawilo numer sesji {pair.Key} ({pair.Value}).");
        }
        Check(!state.Settings.SessionSlots.ContainsValue("spotifyLibrespot"),
            "Stara sesja Librespot nadal zajmuje numer.");
        Check(state.RemoteQueues.ItemsBySession["radio"].Count == przedRadio,
            "Scalenie ruszylo kolejke innej sesji.");
        Check(state.SessionNavigation.Sessions.ContainsKey("tidal"),
            "Scalenie usunelo stan nawigacji innej sesji.");
        Check(state.Settings.Audio.SessionMutedById["radio"],
            "Scalenie ruszylo wyciszenie innej sesji.");

        // Poswiadczenia i cache biblioteki zostaja nietkniete.
        Check(state.Spotify.ClientId == przedKonto && state.Spotify.ClientId.Length > 0,
            "Scalenie ruszylo dane logowania Spotify.");
        Check(state.Spotify.CachedCollectionItems.Count == przedCache && przedCache > 0,
            "Scalenie skasowalo cache biblioteki Spotify.");
        Check(state.Tidal.CachedCollectionItems.Count > 0 && state.Tidal.ClientId.Length > 0,
            "Scalenie ruszylo dane innego serwisu.");

        // Po scaleniu numeracja zostaje domknieta bez przestawiania sesji 1-6.
        var znormalizowane = SessionSlotOrder.Normalize(state.Settings.SessionSlots);
        foreach (var pair in przedSloty.Where(pair => pair.Key <= 6))
        {
            Check(znormalizowane[pair.Key] == pair.Value,
                $"Normalizacja po scaleniu przestawila wyuczony numer {pair.Key}.");
        }
    }

    // ---- dane probne ------------------------------------------------------

    private static string NewTempFolder()
    {
        var folder = Path.Combine(Path.GetTempPath(), "amc-spotify-unify-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        return folder;
    }

    private static string Snapshot(PersistedState state)
    {
        var json = JsonSerializer.Serialize(state, new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        });
        // Znacznik czasu archiwizacji jest jedynym polem, ktore Z ZALOZENIA
        // rozni sie miedzy przebiegami. Porownanie determinizmu dotyczy tresci
        // danych uzytkownika, wiec ten jeden zapis zrownujemy jawnie - zamiast
        // uznawac caly wynik za niedeterministyczny albo wyrzucac datowanie.
        var marker = "\"archivedUtcTicks\":";
        var start = json.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0) return json;
        var end = json.IndexOf(',', start + marker.Length);
        if (end < 0) end = json.IndexOf('}', start + marker.Length);
        return json[..start] + marker + "0" + json[end..];
    }

    /// <summary>Krotki opis pierwszej roznicy - diagnostyka po niepowodzeniu.</summary>
    private static string FirstDifference(string a, string b)
    {
        var limit = Math.Min(a.Length, b.Length);
        for (var index = 0; index < limit; index++)
        {
            if (a[index] == b[index]) continue;
            var from = Math.Max(0, index - 60);
            return $"Pierwsza roznica na pozycji {index}: [{a[from..Math.Min(a.Length, index + 60)]}] vs [{b[from..Math.Min(b.Length, index + 60)]}]";
        }
        return $"Rozne dlugosci: {a.Length} vs {b.Length}";
    }

    /// <summary>
    /// Stan PROBNY z dwiema sesjami Spotify. Zadne pole nie pochodzi z pliku
    /// uzytkownika: wszystkie identyfikatory i tytuly sa wymyslone na potrzeby
    /// tego testu.
    /// </summary>
    private static PersistedState CreateProbeState()
    {
        var state = new PersistedState();
        var settings = state.Settings;
        settings.LastSessionId = "spotifyLibrespot";
        settings.SessionSlots = new Dictionary<int, string>
        {
            [1] = "local", [2] = "wiim", [3] = "tidal", [4] = "appleMusic",
            [5] = "radio", [6] = "podcasts", [7] = "spotify", [8] = "spotifyLibrespot"
        };
        settings.SpotifyLibrespotDeviceName = "Proba wyjscia Librespot";

        // Pozycje: jedna wspolna (konflikt), jedna tylko w Librespot.
        settings.SpotifyPlayback.ItemsByKey["spotify:track:wspolny"] =
            new SpotifyItemPlaybackSettings { ResumePositionMode = ResumePositionMode.Remember, PositionTicks = TimeSpan.FromSeconds(71).Ticks };
        settings.SpotifyLibrespotPlayback.ItemsByKey["spotify:track:wspolny"] =
            new SpotifyItemPlaybackSettings { ResumePositionMode = ResumePositionMode.Remember, PositionTicks = TimeSpan.FromSeconds(23).Ticks };
        settings.SpotifyLibrespotPlayback.ItemsByKey["spotify:track:tylko-librespot"] =
            new SpotifyItemPlaybackSettings { ResumePositionMode = ResumePositionMode.Remember, PositionTicks = TimeSpan.FromSeconds(95).Ticks };
        settings.SpotifyPlayback.ContainersByKey["spotify:album:proba"] = ResumePositionMode.Remember;
        settings.SpotifyLibrespotPlayback.ContainersByKey["spotify:album:proba"] = ResumePositionMode.StartFromBeginning;

        settings.ResumePositionModeBySession["spotify"] = ResumePositionMode.StartFromBeginning;
        settings.ResumePositionModeBySession["spotifyLibrespot"] = ResumePositionMode.Remember;
        settings.Audio.SessionMutedById["radio"] = true;
        settings.Audio.SessionMutedById["spotifyLibrespot"] = true;
        settings.Audio.OutputDeviceIdsBySession["spotify"] = "urzadzenie-sdk";
        settings.Audio.OutputDeviceIdsBySession["spotifyLibrespot"] = "urzadzenie-librespot";
        settings.Audio.OverridesBySession["spotifyLibrespot"] =
            new SessionPlaybackAudioOverrides { LoudnessNormalizationOverride = true };

        // Poswiadczenia i cache - MUSZA przetrwac nietkniete.
        state.Spotify.ClientId = "probny-identyfikator-aplikacji";
        state.Spotify.AccountDisplayName = "Konto probne";
        state.Spotify.CachedCollectionItems.Add(new TidalCachedCollectionItemSettings
        { Id = "cache-1", ExternalId = "wspolny", Title = "Utwor probny", Source = "spotify:track:wspolny" });
        state.Tidal.ClientId = "probny-tidal";
        state.Tidal.CachedCollectionItems.Add(new TidalCachedCollectionItemSettings { Id = "tidal-cache-1", Title = "Inny serwis" });

        // Obie kolejki: jeden utwor wspolny, po jednym wlasnym.
        state.RemoteQueues.ItemsBySession["spotify"] =
        [
            new RemoteQueueItemSettings { Id = "spotify:tylko-sdk", ExternalId = "tylko-sdk", Title = "Tylko SDK", PublicUri = "spotify:track:tylko-sdk", IsInQueue = true },
            new RemoteQueueItemSettings { Id = "spotify:wspolny", ExternalId = "wspolny", Title = "Wspolny", PublicUri = "spotify:track:wspolny", IsInQueue = true }
        ];
        state.RemoteQueues.ItemsBySession["spotifyLibrespot"] =
        [
            new RemoteQueueItemSettings { Id = "spotifyLibrespot:wspolny", ExternalId = "wspolny", Title = "Wspolny", PublicUri = "spotify:track:wspolny", IsInQueue = true, IsPlayNext = true },
            new RemoteQueueItemSettings { Id = "spotifyLibrespot:tylko-librespot", ExternalId = "tylko-librespot", Title = "Tylko Librespot", PublicUri = "spotify:track:tylko-librespot", IsInQueue = true }
        ];
        state.RemoteQueues.ItemsBySession["radio"] =
        [
            new RemoteQueueItemSettings { Id = "radio:1", Title = "Stacja probna", Kind = MediaItemKind.Station }
        ];

        // Widoki i sortowania.
        state.SessionNavigation.Sessions["spotify"] = new SessionNavigationState
        {
            CurrentView = "Biblioteka",
            CollectionSortModes = { ["Biblioteka"] = CollectionSortMode.Alphabetical },
            SelectedItemIds = { ["Biblioteka"] = "spotify:tylko-sdk" }
        };
        state.SessionNavigation.Sessions["spotifyLibrespot"] = new SessionNavigationState
        {
            CurrentView = "Kolejka",
            CollectionSortModes = { ["Biblioteka"] = CollectionSortMode.Custom, ["Kolejka"] = CollectionSortMode.AddedNewest },
            SelectedItemIds = { ["Kolejka"] = "spotifyLibrespot:tylko-librespot" },
            PlaybackContextItemIds = { "spotifyLibrespot:tylko-librespot" }
        };
        state.SessionNavigation.Sessions["tidal"] = new SessionNavigationState { CurrentView = "Biblioteka" };

        state.CollectionOrders.QueueItemIdsBySession["spotify"] = ["spotify:tylko-sdk", "spotify:wspolny"];
        state.CollectionOrders.QueueItemIdsBySession["spotifyLibrespot"] =
            ["spotifyLibrespot:wspolny", "spotifyLibrespot:tylko-librespot"];
        state.CollectionOrders.LibraryItemIdsBySession["spotifyLibrespot"] = ["spotifyLibrespot:tylko-librespot"];

        state.PlaybackHistory.ItemIdsBySession["spotifyLibrespot"] = ["spotifyLibrespot:tylko-librespot"];
        state.SearchHistory.Entries["spotifyLibrespot"] = ["zapytanie probne"];

        state.SessionPresets.EntriesBySession["spotify"] =
        [
            new SessionPresetEntry { Slot = 1, TargetId = "spotify:tylko-sdk", TargetTitle = "Preset SDK" }
        ];
        state.SessionPresets.EntriesBySession["spotifyLibrespot"] =
        [
            new SessionPresetEntry { Slot = 2, TargetId = "spotifyLibrespot:tylko-librespot", TargetTitle = "Preset Librespot" }
        ];

        // Daty tworzenia PINUJEMY. Domyslnie sa brane z zegara, wiec dwa
        // przebiegi proby nigdy nie bylyby porownywalne - a to zamaskowaloby
        // prawdziwa niedeterministycznosc samej migracji.
        state.Playlists.Entries.Add(new PlaylistEntry
        { Id = "pl-1", SessionId = "spotifyLibrespot", Name = "Lista probna", CreatedUtcTicks = 1,
          ItemIds = { "spotifyLibrespot:tylko-librespot" } });
        state.Bookmarks.Entries.Add(new BookmarkEntry
        { Id = "bm-1", SessionId = "spotifyLibrespot", ItemId = "spotifyLibrespot:tylko-librespot",
          Name = "Zakladka probna", CreatedUtcTicks = 2 });
        state.PlaybackVolumes.Entries.Add(new PlaybackVolumeMemoryEntry
        { SessionId = "spotifyLibrespot", ContextId = "spotifyLibrespot:tylko-librespot", OutputDeviceId = "urzadzenie-librespot", Volume = 42 });

        return state;
    }
}
