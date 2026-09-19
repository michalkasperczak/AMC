using System.Reflection;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Core.Spotify;
using AccessibleMediaController.Windows;
using AccessibleMediaController.Windows.Services;

/// <summary>
/// RZECZYWISTE okno glowne, nie atrapa. Dowodzi trzech rzeczy przy starcie:
/// jest DOKLADNIE JEDNA sesja Spotify, aktywny silnik pochodzi z zapisu, a
/// migracja starej sesji wykonuje sie PRZED zbudowaniem sesji i nie jest
/// nadpisywana przez pozniejsze zapisy stanu.
/// </summary>
internal static class SpotifyStartupEngineTests
{
    internal static void Run()
    {
        PustaBibliotekaNieTworzyDemonstracji();
        JednaSesjaISilnikZZapisu(SpotifyPlaybackEngine.Librespot);
        JednaSesjaISilnikZZapisu(SpotifyPlaybackEngine.Sdk);
        MigracjaPrzedSesjamiINieNadpisana();
        ZywaKolejkaPoMigracji();
        Console.WriteLine("OK: jedna sesja Spotify w rzeczywistym MainWindow, silnik wg zapisu, migracja przed sesjami");
    }

    /// <summary>
    /// Migracja przenosi kolejke starej sesji DO PLIKU. Ta kolejka musi wrocic
    /// takze do ZYWEJ sesji przy starcie, inaczej uzytkownik po restarcie widzi
    /// pusta kolejke mimo poprawnego zapisu. Sprawdzamy pieciu przypadkach:
    /// brak katalogu, katalog z innym utworem, katalog surowy i kopia legacy
    /// tego samego ID, katalog prefiksowany i kopia legacy tego samego ID oraz
    /// zakladka legacy wskazujaca element katalogu BEZ kolejki.
    /// </summary>
    private static void ZywaKolejkaPoMigracji()
    {
        // 1. Brak katalogu (pierwszy start po czyszczeniu cache) + kolejka legacy.
        ZywaKolejka("brak katalogu", katalog: [], (session, _) =>
        {
            var wKolejce = session.Items.Where(item => item.IsInQueue || item.IsPlayNext).ToArray();
            if (wKolejce.Length != 1 || wKolejce[0].ExternalId != "OLD")
            {
                throw new Exception(
                    $"Bez katalogu zywa sesja ma {wKolejce.Length} pozycji w kolejce zamiast przeniesionego OLD "
                    + $"(pozycje sesji: {session.Items.Count}).");
            }
        });

        // 2. Katalog z INNYM utworem + kolejka legacy: obie pozycje w sesji.
        ZywaKolejka("katalog z innym utworem", katalog: [Utwor("spotify:track:INNY", "INNY")], (session, _) =>
        {
            if (session.Items.All(item => item.ExternalId != "INNY"))
                throw new Exception("Zywa sesja zgubila zapamietany katalog.");
            var wKolejce = session.Items.Where(item => item.IsInQueue || item.IsPlayNext).ToArray();
            if (wKolejce.Length != 1 || wKolejce[0].ExternalId != "OLD")
                throw new Exception($"Kolejka legacy nie wrocila obok katalogu ({wKolejce.Length} pozycji).");
        });

        // 3. Katalog SUROWY o tym samym ID co kopia legacy: jeden wiersz, w kolejce.
        ZywaKolejka("katalog surowy = kopia legacy", katalog: [Utwor("spotify:track:OLD", "OLD")], (session, _) =>
        {
            var old = session.Items.Where(item => item.ExternalId == "OLD").ToArray();
            if (old.Length != 1)
                throw new Exception($"Ten sam utwor wystapil {old.Length} razy zamiast raz (surowy katalog).");
            if (!old[0].IsInQueue && !old[0].IsPlayNext)
                throw new Exception("Pozycja z katalogu nie zostala oznaczona jako kolejka po migracji.");
        });

        // 4. Katalog PREFIKSOWANY (kopia sesyjna zapisana do cache) + kopia legacy.
        ZywaKolejka("katalog prefiksowany = kopia legacy",
            katalog: [Utwor("spotify:spotify:track:OLD", "OLD")], (session, _) =>
        {
            var old = session.Items.Where(item => item.ExternalId == "OLD").ToArray();
            if (old.Length != 1)
                throw new Exception($"Ten sam utwor wystapil {old.Length} razy zamiast raz (prefiksowany katalog).");
            if (!old[0].IsInQueue && !old[0].IsPlayNext)
                throw new Exception("Prefiksowana pozycja katalogu nie trafila do kolejki po migracji.");
        });

        // 5. Zakladka legacy do elementu katalogu, BEZ kolejki: nic nie wymyslamy.
        WOknie(state =>
        {
            StanLegacy(state, kolejka: false);
            state.Spotify.CachedCollectionItems.Add(
                TidalCachedCollectionItemSettings.FromMediaItem(Utwor("spotify:track:OLD", "OLD")));
            state.SessionNavigation.Sessions["spotifyLibrespot"] = new SessionNavigationState
            {
                SelectedItemIds = { ["Biblioteka"] = "spotifyLibrespot:spotify:track:OLD" }
            };
            state.Bookmarks.Entries.Add(new BookmarkEntry
            {
                Id = "known", SessionId = "spotifyLibrespot", ItemId = "spotifyLibrespot:spotify:track:OLD",
                Name = "Znany element", PositionTicks = 10000000
            });
            state.Bookmarks.Entries.Add(new BookmarkEntry
            {
                Id = "orphan", SessionId = "spotifyLibrespot", ItemId = "spotifyLibrespot:search-only-id",
                Name = "Utwór OLD", PositionTicks = 20000000
            });
        },
        (window, state) =>
        {
            var session = Sesje(window).FindSession("spotify")
                ?? throw new Exception("Brak kanonicznej sesji Spotify.");
            if (session.Items.Count(item => item.ExternalId == "OLD") != 1)
                throw new Exception("Zakladka legacy zdublowala element katalogu.");
            if (session.Items.Any(item => item.IsInQueue || item.IsPlayNext))
                throw new Exception("Sama zakladka legacy wymyslila pozycje w kolejce.");
            if (state.SessionNavigation.Sessions.ContainsKey("spotifyLibrespot"))
                throw new Exception("Stary wpis nawigacji przetrwal migracje.");
            var known = state.Bookmarks.Entries.Single(entry => entry.Id == "known");
            if (known.SessionId != "spotify" || !session.Items.Any(item => item.Id == known.ItemId))
                throw new Exception("Zakladka poza kolejka nie wskazuje rzeczywistego elementu katalogu.");
            if (state.SessionNavigation.Sessions["spotify"].SelectedItemIds["Biblioteka"] != known.ItemId)
                throw new Exception("Zapamietane zaznaczenie nie wskazuje elementu katalogu.");
            var orphan = state.Bookmarks.Entries.Single(entry => entry.Id == "orphan");
            if (session.Items.Any(item => item.Id == orphan.ItemId) || orphan.PositionTicks != 20000000)
                throw new Exception("Osierocona zakladka zostala dopasowana po tytule albo zgubila pozycje.");
            var archived = state.Settings.SpotifyLegacySession?.Bookmarks.Single(entry => entry.Id == "orphan");
            if (archived?.ItemId != "spotifyLibrespot:search-only-id" || archived.PositionTicks != 20000000)
                throw new Exception("Archiwum nie zachowalo osieroconej zakladki.");
        });
    }

    private static MediaItem Utwor(string id, string externalId) => new()
    {
        Id = id, ExternalId = externalId, Source = "spotify:track:" + externalId,
        Title = "Utwór " + externalId, Kind = MediaItemKind.Track
    };

    private static void StanLegacy(PersistedState state, bool kolejka)
    {
        state.Settings.SpotifyEngine = SpotifyPlaybackEngine.Librespot;
        state.Settings.SpotifySessionUnificationVersion = 0;
        state.Settings.LastSessionId = "spotifyLibrespot";
        if (!kolejka) return;
        var legacy = SpotifySessionItemCopies.ForSession(Utwor("spotify:track:OLD", "OLD"), "spotifyLibrespot");
        legacy.IsInQueue = true;
        state.RemoteQueues.ItemsBySession["spotifyLibrespot"] =
            [RemoteQueueItemSettings.FromMediaItem("spotifyLibrespot", legacy)];
        state.CollectionOrders.QueueItemIdsBySession["spotifyLibrespot"] =
            ["spotifyLibrespot:spotify:track:OLD"];
    }

    private static void ZywaKolejka(
        string przypadek,
        MediaItem[] katalog,
        Action<DemoMediaSession, PersistedState> sprawdz)
    {
        try
        {
            WOknie(state =>
            {
                StanLegacy(state, kolejka: true);
                foreach (var item in katalog)
                    state.Spotify.CachedCollectionItems.Add(TidalCachedCollectionItemSettings.FromMediaItem(item));
            },
            (window, state) =>
            {
                // Plik musi miec kolejke - to juz dzialalo przed poprawka.
                if (!state.RemoteQueues.ItemsBySession.TryGetValue("spotify", out var zapis)
                    || zapis.All(item => item.ExternalId != "OLD"))
                {
                    throw new Exception("Migracja nie przeniosla kolejki do zapisu kanonicznej sesji.");
                }
                var session = Sesje(window).FindSession("spotify")
                    ?? throw new Exception("Brak kanonicznej sesji Spotify.");
                sprawdz(session, state);
            });
        }
        catch (Exception exception)
        {
            throw new Exception($"Zywa kolejka po migracji - przypadek '{przypadek}'.", exception);
        }
    }

    private static void PustaBibliotekaNieTworzyDemonstracji()
    {
        foreach (var engine in new[] { SpotifyPlaybackEngine.Librespot, SpotifyPlaybackEngine.Sdk })
        {
            WOknie(state =>
            {
                state.Settings.SpotifyEngine = engine;
                state.Settings.LastSessionId = "spotifyLibrespot";
                state.Spotify.CachedCollectionItems.Clear();
                state.RemoteQueues.ItemsBySession.Clear();
            }, (window, _) =>
            {
                var spotify = Sesje(window).FindSession("spotify")!;
                if (spotify.Items.Count != 0 || spotify.HasCurrentItem)
                    throw new Exception("Pusta sesja Spotify pokazuje utwory demonstracyjne zamiast pustej biblioteki.");
            });
        }
    }

    private static void JednaSesjaISilnikZZapisu(SpotifyPlaybackEngine engine)
    {
        WOknie(state =>
        {
            state.Settings.SpotifyEngine = engine;
            state.Settings.LastSessionId = "spotify";
            var track = new MediaItem
            {
                Id = "spotify:track:TEST", ExternalId = "TEST", Source = "spotify:track:TEST",
                Title = "Utwór testowy", Kind = MediaItemKind.Track, IsFavorite = true
            };
            state.Spotify.CachedCollectionItems.Add(TidalCachedCollectionItemSettings.FromMediaItem(track));
        },
        (window, state) =>
        {
            var manager = Sesje(window);
            var spotify = manager.Sessions
                .Where(session => SpotifyPlaybackSettingsResolver.IsSpotifySession(session.Id))
                .ToArray();
            if (spotify.Length != 1)
                throw new Exception($"Okno glowne utworzylo {spotify.Length} sesji Spotify zamiast jednej.");
            if (spotify[0].Id != "spotify")
                throw new Exception("Sesja Spotify w oknie nie ma kanonicznego identyfikatora.");
            if (manager.FindSession("spotifyLibrespot") is not null)
                throw new Exception("Okno glowne nadal tworzy osobna sesje spotifyLibrespot.");

            var session = spotify[0];
            if (session.Items.Count != 1 || session.Items[0].ExternalId != "TEST" || !session.Items[0].IsFavorite)
                throw new Exception("Jedyna sesja Spotify nie wczytala zapamietanej kolekcji.");

            // Aktywny silnik okna MUSI odpowiadac zapisowi.
            var aktywny = (SpotifyPlaybackEngine)typeof(MainWindow)
                .GetField("_activeSpotifyEngine", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(window)!;
            if (aktywny != engine)
                throw new Exception($"Okno glowne gra silnikiem {aktywny}, a zapis mowi {engine}.");
            if (manager.ActiveSpotifyEngine != engine)
                throw new Exception("SessionManager okna nie zglasza silnika z zapisu.");

            // Host Librespot nie startuje od samego otwarcia okna, w zadnym trybie.
            var output = (SpotifyLibrespotMediaOutput)typeof(MainWindow)
                .GetField("_spotifyLibrespotOutput", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(window)!;
            if (output.IsHostRunning)
                throw new Exception("Samo otwarcie okna uruchomilo hosta Librespot.");

            // Wybor wyjscia zalezy od AKTYWNEGO SILNIKA, nie od identyfikatora sesji.
            manager.SelectSession("spotify");
            var wyborWyjscia = (bool)typeof(MainWindow)
                .GetMethod("CurrentSessionSupportsAudioOutputSelection", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(window, null)!;
            var oczekiwany = engine == SpotifyPlaybackEngine.Librespot;
            if (wyborWyjscia != oczekiwany)
            {
                throw new Exception(oczekiwany
                    ? "Sesja Spotify na Librespot nie udostepnia wyboru wyjscia."
                    : "Sesja Spotify na SDK udostepnia wybor wyjscia, choc nie ma wlasnego toru.");
            }

            // Zmiana ustawien (RebuildCore) nie wolno zgubic sesji ani kolejki.
            session.Items[0].IsInQueue = true;
            typeof(MainWindow).GetMethod("RebuildCore", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(window, null);
            var po = Sesje(window);
            if (!ReferenceEquals(po.FindSession("spotify"), session) || !session.Items[0].IsInQueue)
                throw new Exception("Zmiana ustawien przebudowala grajaca sesje Spotify albo zgubila kolejke.");
            if (po.Sessions.Count(s => SpotifyPlaybackSettingsResolver.IsSpotifySession(s.Id)) != 1)
                throw new Exception("Po zmianie ustawien pojawila sie druga sesja Spotify.");

            // Polecenie podcastow Ctrl+Alt+O jest dostepne w tej JEDNEJ sesji.
            var dostepne = (bool)typeof(MainWindow)
                .GetMethod("CommandVisibleInPalette", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(window, [AccessibleMediaController.Core.Commands.CommandIds.ViewSpotifyPodcasts])!;
            if (!dostepne)
                throw new Exception("Polecenie zapisanych podcastow Spotify jest niedostepne w sesji Spotify.");
        });
    }

    /// <summary>
    /// Migracja idzie PRZED sesjami i nie moze byc nadpisana. Stan wejsciowy ma
    /// stara sesje Librespot w numerze 8 z wlasna pozycja - po starcie numer musi
    /// byc wolny, pozycja przeniesiona na kanoniczna sesje, a KOLEJNY zapis stanu
    /// (mirroring, przechwytywanie pozycji) nie moze przywrocic starego wpisu.
    /// </summary>
    private static void MigracjaPrzedSesjamiINieNadpisana()
    {
        WOknie(state =>
        {
            state.Settings.SpotifyEngine = SpotifyPlaybackEngine.Librespot;
            state.Settings.SpotifySessionUnificationVersion = 0;
            state.Settings.LastSessionId = "spotifyLibrespot";
            state.Settings.SessionSlots = new Dictionary<int, string>
            {
                [1] = "radio", [2] = "local", [3] = "wiim", [4] = "tidal",
                [5] = "appleMusic", [6] = "podcasts", [7] = "spotify", [8] = "spotifyLibrespot"
            };
            state.Settings.Audio.OutputDeviceIdsBySession["spotifyLibrespot"] = "Stare wyjście";
            state.Settings.Audio.SessionMutedById["spotifyLibrespot"] = true;
            state.RemoteQueues.ItemsBySession["spotifyLibrespot"] =
            [
                new RemoteQueueItemSettings
                {
                    Id = "spotifyLibrespot:spotify:track:OLD",
                    ExternalId = "OLD",
                    PublicUri = "spotify:track:OLD",
                    Title = "Stary utwór z kolejki Librespot"
                }
            ];
        },
        (window, state) =>
        {
            var settings = state.Settings;
            if (settings.SpotifySessionUnificationVersion < 1)
                throw new Exception("Migracja nie wykonala sie przy starcie okna.");
            if (settings.SessionSlots.ContainsKey(8))
                throw new Exception("Numer 8 nadal trzyma stara sesje Librespot.");
            foreach (var pair in new Dictionary<int, string>
                     {
                         [1] = "radio", [2] = "local", [3] = "wiim", [4] = "tidal",
                         [5] = "appleMusic", [6] = "podcasts", [7] = "spotify"
                     })
            {
                if (!settings.SessionSlots.TryGetValue(pair.Key, out var occupant)
                    || !string.Equals(occupant, pair.Value, StringComparison.OrdinalIgnoreCase))
                {
                    throw new Exception($"Migracja przestawila numer sesji {pair.Key}.");
                }
            }
            if (!string.Equals(settings.LastSessionId, "spotify", StringComparison.OrdinalIgnoreCase))
                throw new Exception("Ostatnia sesja nadal wskazuje nieistniejaca sesje Librespot.");

            var manager = Sesje(window);
            if (manager.FindSession("spotifyLibrespot") is not null)
                throw new Exception("Sesje zbudowano przed migracja: stara sesja Librespot zyje.");
            if (manager.Sessions.Count(s => SpotifyPlaybackSettingsResolver.IsSpotifySession(s.Id)) != 1)
                throw new Exception("Po migracji sesji Spotify jest wiecej niz jedna.");

            // Kolejka i wyjscie przeniesione na kanoniczna sesje.
            if (!state.RemoteQueues.ItemsBySession.TryGetValue("spotify", out var kolejka)
                || kolejka.All(item => item.ExternalId != "OLD"))
            {
                throw new Exception("Migracja nie przeniosla kolejki starej sesji Librespot.");
            }
            if (!settings.Audio.OutputDeviceIdsBySession.TryGetValue("spotify", out var wyjscie)
                || wyjscie != "Stare wyjście")
            {
                throw new Exception("Migracja nie przeniosla wybranego wyjscia dzwieku.");
            }

            if (!settings.Audio.SessionMutedById.GetValueOrDefault("spotify")
                || settings.SpotifyLegacySession?.Muted != true
                || settings.SpotifyLegacySession.Slot != 8
                || settings.SpotifyLegacySession.OutputDeviceId != "Stare wyjście")
                throw new Exception("Odczyt przed migracja zgubil wyciszenie, numer lub archiwum wyjscia.");

            // NAJWAZNIEJSZE: pozniejsze zapisy/mirroring/capture nie moga odtworzyc
            // starego wpisu. Wolamy realne sciezki zapisu okna.
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(MainWindow).GetMethod("CaptureSpotifyPlaybackPosition", flags)!.Invoke(window, null);
            typeof(MainWindow).GetMethod("CaptureCurrentSessionNavigationState", flags)!.Invoke(window, null);
            typeof(MainWindow).GetMethod("RebuildCore", flags)!.Invoke(window, null);

            if (state.RemoteQueues.ItemsBySession.ContainsKey("spotifyLibrespot"))
                throw new Exception("Zapis po migracji odtworzyl kolejke starej sesji Librespot.");
            if (settings.Audio.OutputDeviceIdsBySession.ContainsKey("spotifyLibrespot"))
                throw new Exception("Zapis po migracji odtworzyl wyjscie starej sesji Librespot.");
            if (settings.SessionSlots.ContainsKey(8))
                throw new Exception("Zapis po migracji odtworzyl numer 8 dla starej sesji.");
            if (state.SessionNavigation.Sessions.ContainsKey("spotifyLibrespot"))
                throw new Exception("Zapis nawigacji odtworzyl stan starej sesji Librespot.");
            if (state.CollectionOrders.QueueItemIdsBySession.ContainsKey("spotifyLibrespot"))
                throw new Exception("Zapis kolejnosci kolejki odtworzyl stara sesje Librespot.");
            if (settings.SpotifySessionUnificationVersion < 1)
                throw new Exception("Zapis stanu cofnal znacznik wykonanej migracji.");
        });
    }

    private static SessionManager Sesje(MainWindow window) =>
        (SessionManager)typeof(MainWindow)
            .GetField("_sessions", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(window)!;

    /// <summary>
    /// Uruchamia sprawdzenie na prawdziwym MainWindow w watku STA. Bez ShowDialog
    /// i bez interakcji z pulpitem - okno tylko powstaje i jest zamykane.
    /// </summary>
    private static void WOknie(Action<PersistedState> przygotuj, Action<MainWindow, PersistedState> sprawdz)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            var root = Path.Combine(Path.GetTempPath(), "amc-spotify-startup-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            MainWindow? window = null;
            try
            {
                var state = new PersistedState();
                state.Settings.Updates.CheckAutomatically = false;
                przygotuj(state);
                var store = new ConfigurationStore(Path.Combine(root, "state.json"));
                // Plik starszej wersji: nowy Save odrzuca juz identyfikator sesji legacy.
                // Uzyj rzeczywistego formatu JSON; wczytanie przechodzi pelna sciezke migracji.
                var options = (System.Text.Json.JsonSerializerOptions)typeof(ConfigurationStore)
                    .GetField("JsonOptions", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
                File.WriteAllText(Path.Combine(root, "state.json"), System.Text.Json.JsonSerializer.Serialize(state, options));
                state = store.LoadOrCreate();
                // Pierwszy odczyt inicjalizuje bazy; drugi bierze dane z SQLite,
                // tak jak aktualizacja istniejacej instalacji ze stara sesja.
                state = store.LoadOrCreate();
                for (var restart = 0; restart < 2; restart++)
                {
                    window = new MainWindow(state, store);
                    sprawdz(window, state);
                    window.Close();
                    window = null;
                    if (restart == 0)
                    {
                        store.Save(state);
                        state = store.LoadOrCreate();
                    }
                }
            }
            catch (Exception exception)
            {
                failure = exception;
            }
            finally
            {
                window?.Close();
                try { Directory.Delete(root, true); } catch (IOException) { }
            }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(40)))
            throw new Exception("Start okna nie zakonczyl testu w 40 s.");
        if (failure is not null)
            throw new Exception("Jedna sesja Spotify i silnik przy starcie.", failure);
    }
}
