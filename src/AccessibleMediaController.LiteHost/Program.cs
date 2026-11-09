using System.Text;
using AccessibleMediaController.Core.Sonos;
using AccessibleMediaController.LiteHost.Protocol;
using AccessibleMediaController.Windows.Services;

namespace AccessibleMediaController.LiteHost;

/// <summary>
/// Punkt wejscia BEZOKIENNEGO hosta AMC-wx-Lite.
///
/// Uruchamia go frontend wxPython jako proces potomny i rozmawia z nim
/// przez stdin/stdout. Host NIE otwiera zadnego okna, NIE nasluchuje na
/// zadnym porcie i NIE wykonuje dowolnych polecen systemu.
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        // stdout jest kanalem PROTOKOLU: nic poza wierszami JSON nie moze
        // sie tam znalezc, inaczej frontend straci synchronizacje.
        // Diagnostyka i wyjatki ida na stderr.
        var standardOutput = Console.Out;

        // Protokol jest w UTF-8 i MUSI byc czytany jako UTF-8.
        //
        // Domyslnie Console.In na Windows dekoduje przekierowany stdin
        // kodowaniem strony kodowej konsoli (u Michala CP852/CP1250), wiec
        // polskie znaki docieraly uszkodzone. Zmierzone na zywym hoscie:
        // ten sam tytul wyslany surowym UTF-8 i jako \uXXXX dawal ROZNE
        // klucze sortowania, a zgadzaly sie tylko tytuly czysto ASCII
        // (1333 z 2596). Dotyczy to kazdej operacji z polskim tekstem --
        // takze sciezek plikow do odtwarzania, nie tylko kolejnosci listy.
        var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        // stderr tez jest czytany jako UTF-8 przez klienta. Domyslna strona
        // kodowa konsoli Windows (CP852/CP1250) psula polskie komunikaty, a
        // przy anulowaniu potrafila nawet zakonczyc watek czytajacy bledem
        // UnicodeDecodeError. Jawny writer utrzymuje jeden kontrakt kodowania
        // dla calego procesu, rowniez dla stack trace i bibliotek ponizej.
        var diagnosticOutput = new StreamWriter(Console.OpenStandardError(), utf8)
        {
            AutoFlush = true
        };
        Console.SetError(diagnosticOutput);
        Console.SetOut(diagnosticOutput);

        // LiteHost jest silnikiem tej samej aplikacji, ale nie uruchamia WPF,
        // wiec App.xaml.cs nie inicjalizuje dla niego dziennika. Bez tego
        // stderr znikal po zakonczeniu procesu i pojedynczej awarii nagrania
        // nie dalo sie rzetelnie odtworzyc. Osobny katalog nie miesza logow
        // AMC Python z logiem starego interfejsu.
        var localDataDirectory =
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localDataDirectory))
            localDataDirectory = Environment.GetEnvironmentVariable("LOCALAPPDATA")
                ?? AppContext.BaseDirectory;
        var logDirectory = Path.Combine(localDataDirectory, "AMC-wx-Lite", "logs");
        DiagnosticLog.Initialize(logDirectory);
        Console.Error.WriteLine("[amc-lite-host] dziennik: " + DiagnosticLog.CurrentLogPath);
        AppDomain.CurrentDomain.ProcessExit += (_, _) => DiagnosticLog.Shutdown();
        var protocolInput = new StreamReader(
            Console.OpenStandardInput(), utf8, detectEncodingFromByteOrderMarks: false);
        var protocolOutput = new StreamWriter(Console.OpenStandardOutput(), utf8)
        {
            AutoFlush = false // petla zadan flushuje sama, po calym wierszu
        };

        var timeshiftMinutes = ReadTimeshiftMinutes(args);
        var timeshiftBufferFolder = ReadArgumentText(args, "--timeshift-buffer-folder");

        // Swiezy publiczny AMC dostaje od frontendu poprawne, lecz jeszcze
        // nieistniejace sciezki. Zanim zarejestrujemy operacje edycji,
        // przygotowujemy caly profil aktualnym ConfigurationStore. Bez tego
        // Ctrl+Shift+L i import playlisty byly dostepne, ale pierwszy zapis
        // konczyl sie komunikatem o brakujacym profilu albo bazie.
        try
        {
            EnsureFreshProfileInitialized(args);
        }
        catch (Exception exception)
        {
            DiagnosticLog.Error("profile", "Nie udało się przygotować profilu AMC.", exception);
            Console.Error.WriteLine(
                "[amc-lite-host] Nie udało się przygotować profilu AMC: "
                + exception.Message);
            Console.SetOut(standardOutput);
            return 2;
        }

        // TRWALOSC kolejki. Dwie jawne decyzje, obie z wiersza polecen:
        //   --profile-dir <sciezka>  ktora KOPIA profilu,
        //   --queue-write            czy ten host jest wlascicielem zapisu.
        // Bez --queue-write host tylko ODCZYTUJE zapisana kolejke. Domyslnie
        // (bez --profile-dir) nie dotyka profilu w ogole.
        LiteQueueStore? store;
        string? queueStartupError = null;
        try
        {
            store = OpenQueueStore(args);
        }
        catch (LiteQueueStoreDenied denied)
        {
            if (args.Contains("--queue-data-dir", StringComparer.Ordinal)
                && !args.Contains("--profile-dir", StringComparer.Ordinal)
                && !args.Contains("--queue-write", StringComparer.Ordinal))
            {
                // Niedostępny własny magazyn nie może wyłączyć odtwarzacza.
                // Status powie prawdę, a interfejs przeczyta ją tylko raz.
                Console.Error.WriteLine("[amc-lite-host] kolejka tymczasowa: " + denied.Message);
                store = null;
                queueStartupError =
                    "Kolejka działa tylko do zamknięcia AMC. Nie można otworzyć miejsca zapisu.";
            }
            else
            {
                Console.Error.WriteLine("[amc-lite-host] ODMOWA magazynu kolejki: " + denied.Message);
                Console.SetOut(standardOutput);
                return 2;
            }
        }

        LitePodcastProgressStore? podcastStore;
        try
        {
            podcastStore = OpenPodcastProgressStore(args);
        }
        catch (LitePodcastProgressStoreDenied denied)
        {
            Console.Error.WriteLine("[amc-lite-host] ODMOWA zapisu postępu podcastów: " + denied.Message);
            store?.Dispose();
            Console.SetOut(standardOutput);
            return 2;
        }

        var profileMutations = OpenProfileMutationStore(args);
        var commonDataSnapshot = OpenCommonDataSnapshot(args);
        using var tidalCatalog = OpenTidalCatalog(args);
        using var spotifyCatalog = OpenSpotifyCatalog(args);
        using var spotifyPlayback = new LiteSpotifyPlaybackCoordinator(
            spotifyCatalog is null ? null : spotifyCatalog.GetPlaybackCredentialsAsync,
            ReadArgumentText(args, "--release-profile"),
            ReadArgumentPath(args, "--soloist-settings"),
            ReadArgumentPath(args, "--soloist-data-dir"));
        using var wiim = OpenWiiM(args);
        using var frontierSmart = OpenFrontierSmart(args);
        var componentUpdates = new LiteComponentUpdateCoordinator(
            ReadArgumentPath(args, "--state-json"));
        var shellIntegration = new LiteShellIntegrationCoordinator(
            ReadArgumentText(args, "--release-profile"));
        using var sonos = new LiteSonosCoordinator(
            new LiteSonosAccountOwner(),
            ReadArgumentPath(args, "--state-json"),
            loginBrokerAvailable: SonosIntegrationDefaults.BrokerAvailable);
        using var youtubeAccount = OpenYouTubeAccount(args);

        var bookmarkStore = OpenBookmarkStore(args);
        using var handlers = new LiteEngineHandlers(
            timeshiftMinutes,
            store,
            bookmarkStore,
            podcastStore,
            profileMutations,
            tidalCatalog,
            wiim,
            frontierSmart,
            sonos,
            spotifyCatalog,
            spotifyPlayback,
            commonDataSnapshot,
            enableTidalNativePlayback: LiteTidalNativePlayRequest.IsEnabledForProfile(
                ReadArgumentText(args, "--release-profile")),
            timeshiftBufferFolder: timeshiftBufferFolder,
            queueStartupError: queueStartupError);
        using var nvdaRelay = new LiteNvdaRelay();
        using var nvdaBridge = new LiteNvdaBridge(nvdaRelay);
        var registeredHandlers = new Dictionary<string, Func<LiteRequest, LiteEventSink, object?>>(
            handlers.Build(), StringComparer.Ordinal);
        foreach (var entry in nvdaRelay.Handlers()) registeredHandlers.Add(entry.Key, entry.Value);
        foreach (var entry in nvdaBridge.Handlers()) registeredHandlers.Add(entry.Key, entry.Value);
        foreach (var entry in componentUpdates.Handlers()) registeredHandlers.Add(entry.Key, entry.Value);
        foreach (var entry in shellIntegration.Handlers()) registeredHandlers.Add(entry.Key, entry.Value);
        foreach (var entry in LiteGeneralSettingsCoordinator.Handlers())
            registeredHandlers.Add(entry.Key, entry.Value);
        if (youtubeAccount is not null)
        {
            foreach (var entry in youtubeAccount.Handlers())
                registeredHandlers.Add(entry.Key, entry.Value);
        }
        // JAWNY opt-in: poza kolejke wychodza tylko operacje, ktore moga dlugo
        // czytac/dekodowac plik: informacja pod lewa strzalka oraz eksport
        // zaznaczonego fragmentu. Transport nadal pozostaje responsywny, a
        // wszystkie pozostale polecenia sa uporzadkowane serialnie.
        var loop = new LiteDispatchLoop(
            registeredHandlers,
            concurrentOperations:
            [
                LiteQuickInformation.Operation,
                // Aktualizacja FFmpeg może pobrać i zweryfikować duży pakiet.
                // Nie wolno jej stawiać przed pauzą, nagrywaniem ani statusem.
                LiteComponentUpdateCoordinator.UpdateOperation,
                LiteComponentUpdateCoordinator.AutomaticOperation,
                // Eksport moze trwac dlugo. Odtwarzanie, pauza i status nie
                // moga na ten czas utknac za dekoderem/FFmpeg.
                LiteAudioClipOperations.ExportOperation,
                // Usuwanie tez uzywa FFmpeg i po potwierdzeniu moze trwac
                // wiele minut. Osobna komenda anulowania musi w tym czasie
                // pozostac osiagalna przez zwykla, serialna sciezke.
                LiteAudioClipOperations.RemoveOperation,
                // Dopisywanie buduje i weryfikuje nowy plik obok celu. Takze
                // musi pozostawic status i anulowanie responsywne.
                LiteAudioClipOperations.AppendOperation,
                // RSS i yt-dlp nie moga blokowac pauzy, statusu ani nagrywania.
                // Zapis bazy na koncu pozostaje chroniony przez jednego pisarza.
                LitePodcastRefreshCoordinator.Operation,
                LitePodcastRefreshCoordinator.AutomaticOperation,
                // Pobieranie RSS/YouTube jest dlugie, ale nie moze blokowac
                // transportu ani zapisu postepu odtwarzania.
                LitePodcastDownloadCoordinator.Operation,
                LitePodcastDownloadCoordinator.SaveAsOperation,
                LitePodcastDownloadCoordinator.VideoOperation,
                LitePodcastDownloadCoordinator.VideoSaveAsOperation,
                // Sprawdzenie nowego źródła również wykonuje sieć lub yt-dlp.
                LitePodcastAddCoordinator.Operation,
                // Alt+D pobiera brakujący opis publicznego materiału lub
                // kolekcji bez logowania Google i bez blokowania transportu.
                LiteYouTubeDescriptionCoordinator.Operation,
                // Pobranie Podcasting 2.0 JSON albo strony odcinka może trwać
                // kilkanaście sekund. Samo przejście po gotowym indeksie
                // pozostaje w szeregowej kolejce transportu.
                LitePodcastChapterCoordinator.Operation,
                // Publiczne katalogi Apple Podcasts, Spreaker i YouTube są
                // niezależne od transportu; wolna odpowiedź nie zatrzymuje
                // radia, nagrywania ani pauzy.
                LitePodcastDirectorySearchCoordinator.Operation,
                // Skan Folderów Biblioteki może objąć tysiące plików. Nie
                // blokuje transportu; zapis pozostaje pod jedną bramą profilu.
                "library.folderSource.add",
                "library.folderSource.refresh",
                // Radio Browser używa sieci, ale nie może blokować aktywnego
                // radia, nagrywania ani statusu transportu.
                LiteRadioDirectorySearchCoordinator.Operation,
                // Import pobiera maksymalnie cztery RSS równolegle. Nie może
                // zatrzymać transportu ani komunikatów o nagrywaniu.
                LitePodcastOpmlCoordinator.ImportOperation,
                // Podpis akustyczny i odpowiedź usługi mogą zająć kilkanaście
                // sekund. Radio, nagrywanie i klawiatura pozostają w tym
                // czasie na zwykłej, responsywnej ścieżce protokołu.
                LiteRadioRecognitionCoordinator.Operation,
                // Oficjalny katalog TIDAL wykonuje siec i odswiezenie tokenu,
                // ale nie moze blokowac transportu ani nagrywania radia.
                LiteTidalCatalogContract.SearchOperation,
                LiteTidalPlaybackProbe.Operation,
                LiteTidalCatalogContract.LoginOperation,
                LiteTidalCatalogContract.ContainerItemsOperation,
                LiteTidalCatalogContract.CollectionViewOperation,
                LiteTidalCatalogContract.MembershipOperation,
                LiteTidalCatalogContract.PlaylistRemoveItemsOperation,
                // Spotify uses the same authenticated Web API pattern, but
                // this first layer contains reads only and never starts OAuth.
                LiteSpotifyCatalogContract.SearchOperation,
                LiteSpotifyCatalogContract.LoginOperation,
                LiteSpotifyCatalogContract.ContainerItemsOperation,
                LiteSpotifyCatalogContract.CollectionViewOperation,
                LiteSpotifyCatalogContract.MembershipOperation,
                LiteSpotifyCatalogContract.PlaylistTargetsOperation,
                LiteSpotifyCatalogContract.PlaylistCreateOperation,
                LiteSpotifyCatalogContract.PlaylistAddItemsOperation,
                LiteSpotifyCatalogContract.PlaylistRemoveItemsOperation,
                // OAuth i odczyt konta YouTube są jawne, a synchronizacja
                // używa świeżego podglądu i jednej transakcji bazy Podcastów.
                LiteYouTubeAccountContract.LoginOperation,
                LiteYouTubeAccountContract.SyncPreviewOperation,
                LiteYouTubeAccountContract.SyncApplyOperation,
                // Uruchomienie TIDALa i wczytanie strony albumu trwa nawet
                // kilkadziesiat sekund. Radio, nagrywanie i status lokalnego
                // odtwarzacza musza w tym czasie pozostac responsywne.
                LiteTidalDesktopContract.PlayOperation,
                LiteTidalDesktopContract.TransportOperation,
                // Urzadzenie sieciowe moze odpowiadac kilka sekund. Nie moze
                // w tym czasie blokowac lokalnego radia ani nagrywania.
                LiteWiiMContract.SnapshotOperation,
                LiteWiiMContract.TransportOperation,
                LiteWiiMContract.PresetsOperation,
                LiteWiiMContract.ActivatePresetOperation,
                LiteWiiMContract.OptionsOperation,
                LiteWiiMContract.SetOptionOperation,
                LiteWiiMContract.ToggleShuffleOperation,
                LiteWiiMContract.NavigateSourceOperation,
                LiteWiiMContract.NetworkStreamsOperation,
                LiteWiiMContract.PlayNetworkStreamOperation,
                LiteWiiMContract.NetworkStreamEditInfoOperation,
                // Sonos korzysta z chmury i odnowienia konta. Wszystkie jego
                // operacje muszą pozostawić radio i nagrywanie responsywne.
                LiteSonosContract.AccountLoginStartOperation,
                LiteSonosContract.AccountLoginCheckOperation,
                LiteSonosContract.AccountRefreshOperation,
                LiteSonosContract.TargetsOperation,
                LiteSonosContract.SnapshotOperation,
                 LiteSonosContract.TransportOperation,
                 LiteSonosContract.MaterialsOperation,
                 LiteSonosContract.LoadMaterialOperation
            ],
            onReady: nvdaBridge.Start);

        Console.Error.WriteLine(
            $"[amc-lite-host] start, bufor transmisji {timeshiftMinutes} min, protokol 1");
        Console.Error.WriteLine(store is null
            ? "[amc-lite-host] kolejka BEZ trwalosci (brak --profile-dir)"
            : $"[amc-lite-host] kolejka: profil {store.ProfileDirectory}, tryb {store.Mode}");
        Console.Error.WriteLine(podcastStore is null
            ? "[amc-lite-host] postęp podcastów BEZ zapisu (brak --podcasts-db)"
            : "[amc-lite-host] postęp podcastów: wąski zapis C#");

        try
        {
            loop.Run(protocolInput, protocolOutput);
        }
        catch (Exception exception)
        {
            DiagnosticLog.Error("lite-host", "Awaria petli polecen.", exception);
            Console.Error.WriteLine("[amc-lite-host] awaria petli: " + exception);
            return 1;
        }
        finally
        {
            protocolOutput.Flush();
            Console.SetOut(standardOutput);
        }

        Console.Error.WriteLine("[amc-lite-host] koniec wejscia, zamykam sie");
        DiagnosticLog.Info("application", "Zamykanie silnika AMC Python.");
        DiagnosticLog.Shutdown();
        return 0;
    }

    private static int ReadTimeshiftMinutes(string[] args)
    {
        for (var index = 0; index < args.Length - 1; index++)
        {
            if (!string.Equals(args[index], "--timeshift-minutes", StringComparison.Ordinal)) continue;
            if (int.TryParse(args[index + 1], out var value)) return Math.Clamp(value, 1, 720);
        }
        return 30;
    }

    /// <summary>
    /// Otwiera magazyn trwalosci kolejki wedlug argumentow.
    ///
    /// Zasada bezpieczenstwa: zapis jest WYLACZONY domyslnie i wlaczany
    /// wylacznie jawnym <c>--queue-write</c> razem z <c>--profile-dir</c>
    /// wskazujacym konkretna kopie. Odmowa (zajeta kopia, profil produkcyjny,
    /// brak prawa zapisu) konczy START HOSTA bledem -- host NIE startuje cicho
    /// bez trwalosci, o ktora go poproszono.
    /// </summary>
    private static LiteQueueStore? OpenQueueStore(string[] args)
    {
        string? profileDirectory = null;
        string? standaloneDirectory = null;
        for (var index = 0; index < args.Length - 1; index++)
        {
            if (string.Equals(args[index], "--queue-data-dir", StringComparison.Ordinal))
                standaloneDirectory = args[index + 1];
            if (!string.Equals(args[index], "--profile-dir", StringComparison.Ordinal)) continue;
            profileDirectory = args[index + 1];
        }
        var writable = args.Contains("--queue-write", StringComparer.Ordinal);

        if (standaloneDirectory is not null)
        {
            if (profileDirectory is not null || writable)
                throw new LiteQueueStoreDenied("Wybierz jeden magazyn kolejki.");
            try
            {
                return LiteQueueStore.OpenStandalone(standaloneDirectory);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                Console.Error.WriteLine("[amc-lite-host] ODMOWA magazynu kolejki: " + exception.Message);
                throw new LiteQueueStoreDenied("Nie udało się otworzyć magazynu kolejki: " + exception.Message);
            }
        }

        if (profileDirectory is null)
        {
            if (writable)
            {
                // Zadanie zapisu BEZ wskazania kopii to blad wywolania, nie
                // powod do cichego trybu odczytu.
                Console.Error.WriteLine(
                    "[amc-lite-host] --queue-write wymaga --profile-dir wskazujacego wlasna kopie profilu");
                throw new LiteQueueStoreDenied("--queue-write bez --profile-dir");
            }
            return null;
        }

        try
        {
            return LiteQueueStore.Open(profileDirectory, writable);
        }
        catch (LiteQueueStoreDenied denied)
        {
            Console.Error.WriteLine("[amc-lite-host] ODMOWA magazynu kolejki: " + denied.Message);
            throw;
        }
    }

    private static LiteBookmarkStore? OpenBookmarkStore(string[] args)
    {
        for (var index = 0; index < args.Length - 1; index++)
        {
            if (!string.Equals(args[index], "--library-db", StringComparison.Ordinal)) continue;
            var path = Path.GetFullPath(args[index + 1]);
            return File.Exists(path) ? new LiteBookmarkStore(path) : null;
        }
        return null;
    }

    private static LitePodcastProgressStore? OpenPodcastProgressStore(string[] args)
    {
        for (var index = 0; index < args.Length - 1; index++)
        {
            if (!string.Equals(args[index], "--podcasts-db", StringComparison.Ordinal)) continue;
            return LitePodcastProgressStore.Open(Path.GetFullPath(args[index + 1]));
        }
        return null;
    }

    private static LiteProfileMutationStore? OpenProfileMutationStore(string[] args)
    {
        var library = ReadArgumentPath(args, "--library-db");
        var podcasts = ReadArgumentPath(args, "--podcasts-db");
        var state = ReadArgumentPath(args, "--state-json");
        return library is not null && podcasts is not null && state is not null
            ? new LiteProfileMutationStore(library, podcasts, state)
            : null;
    }

    private static void EnsureFreshProfileInitialized(string[] args)
    {
        var library = ReadArgumentPath(args, "--library-db");
        var podcasts = ReadArgumentPath(args, "--podcasts-db");
        var state = ReadArgumentPath(args, "--state-json");
        if (library is null || podcasts is null || state is null) return;
        LiteFreshProfileInitializer.EnsureInitialized(library, podcasts, state);
    }

    private static LiteCommonDataSnapshotCoordinator? OpenCommonDataSnapshot(string[] args)
    {
        var profile = ReadArgumentText(args, "--release-profile");
        var snapshot = ReadArgumentPath(args, "--common-data-snapshot");
        var library = ReadArgumentPath(args, "--library-db");
        var podcasts = ReadArgumentPath(args, "--podcasts-db");
        var state = ReadArgumentPath(args, "--state-json");
        return profile is not null
            && snapshot is not null
            && library is not null
            && podcasts is not null
            && state is not null
                ? new LiteCommonDataSnapshotCoordinator(
                    profile,
                    snapshot,
                    state,
                    library,
                    podcasts)
                : null;
    }

    private static LiteTidalCatalogCoordinator? OpenTidalCatalog(string[] args)
    {
        var state = ReadArgumentPath(args, "--state-json");
        var profile = ReadArgumentText(args, "--release-profile") ?? "test";
        return state is null ? null : new LiteTidalCatalogCoordinator(state, profile);
    }

    private static LiteSpotifyCatalogCoordinator? OpenSpotifyCatalog(string[] args)
    {
        var state = ReadArgumentPath(args, "--state-json");
        return state is null
            ? null
            : new LiteSpotifyCatalogCoordinator(
                new LiteSpotifyWindowsCatalogBackend(
                    state, ReadArgumentText(args, "--release-profile")));
    }

    private static LiteWiiMCoordinator? OpenWiiM(string[] args)
    {
        var state = ReadArgumentPath(args, "--state-json");
        return state is null ? null : new LiteWiiMCoordinator(state);
    }

    private static LiteFrontierSmartCoordinator? OpenFrontierSmart(string[] args)
    {
        var state = ReadArgumentPath(args, "--state-json");
        var profile = ReadArgumentText(args, "--release-profile") ?? "test";
        return state is null ? null : new LiteFrontierSmartCoordinator(state, profile);
    }

    private static LiteYouTubeAccountCoordinator? OpenYouTubeAccount(string[] args)
    {
        var profile = ReadArgumentText(args, "--release-profile")?.ToLowerInvariant();
        var clientId = ReadArgumentText(args, "--youtube-client-id");
        var clientSecretFile = ReadArgumentPath(args, "--youtube-client-secret-file");
        var clientSecret = ReadYouTubeClientSecret(clientSecretFile);
        var tokenBrokerText = ReadArgumentText(args, "--youtube-token-broker");
        var podcasts = ReadArgumentPath(args, "--podcasts-db");
        if (clientId is null || podcasts is null)
            return null;
        if (profile == "test" && clientSecret is not null && tokenBrokerText is null)
        {
            return new LiteYouTubeAccountCoordinator(
                new LiteYouTubeWindowsAccountBackend(
                    clientId, clientSecret, profile, podcasts));
        }
        if (profile == "public"
            && clientSecret is null
            && Uri.TryCreate(tokenBrokerText, UriKind.Absolute, out var tokenBroker))
        {
            return new LiteYouTubeAccountCoordinator(
                new LiteYouTubeWindowsAccountBackend(
                    clientId, tokenBroker, profile, podcasts));
        }
        return null;
    }

    private static string? ReadYouTubeClientSecret(string? path)
    {
        if (path is null) return null;
        try
        {
            var file = new FileInfo(path);
            if (!file.Exists || file.Length is < 8 or > 2048) return null;
            var value = File.ReadAllText(file.FullName).Trim();
            if (value.Length is < 8 or > 512
                || value.Any(character => char.IsWhiteSpace(character) || char.IsControl(character)))
                return null;
            return value;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or System.Security.SecurityException)
        {
            return null;
        }
    }

    private static string? ReadArgumentPath(string[] values, string name)
    {
        for (var index = 0; index < values.Length - 1; index++)
        {
            if (string.Equals(values[index], name, StringComparison.Ordinal))
                return Path.GetFullPath(values[index + 1]);
        }
        return null;
    }

    private static string? ReadArgumentText(string[] values, string name)
    {
        for (var index = 0; index < values.Length - 1; index++)
        {
            if (string.Equals(values[index], name, StringComparison.Ordinal))
                return values[index + 1].Trim();
        }
        return null;
    }
}
