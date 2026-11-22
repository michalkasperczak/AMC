namespace AccessibleMediaController.LiteHost.ProtocolTests;

internal static class Program
{
    private static readonly (string Switch, Action Run)[] Suites =
    [
        ("--requests", RequestParsingTests.Run),
        ("--dispatch", DispatchLoopTests.Run),
        ("--nvda-relay", NvdaRelayTests.Run),
        ("--args", ArgumentReadingTests.Run),
        ("--component-updates", ComponentUpdatePolicyTests.Run),
        ("--audio", AudioConfigurationTests.Run),
        ("--session-options", SessionOptionsPayloadTests.Run),
        ("--queue", QueueCoordinatorTests.Run),
        ("--transport", QueueTransportTests.Run),
        ("--transport-routing", TransportRoutingTests.Run),
        ("--tidal-native", TidalNativePlayRequestTests.Run),
        ("--remote-media-bitrate", RemoteMediaBitrateTests.Run),
        ("--persistence", QueuePersistenceTests.Run),
        ("--quickinfo", QuickInformationTests.Run),
        ("--bookmarks", BookmarkStoreTests.Run),
        ("--playback-history", PlaybackHistoryMutationTests.Run),
        ("--radio-library", RadioLibraryMutationTests.Run),
        ("--podcast-progress", PodcastProgressStoreTests.Run),
        ("--youtube-rss-first", YouTubeRssFirstTests.Run),
        ("--podcast-file-import", PodcastFileImportTests.Run),
        ("--podcast-chapters", PodcastChapterCoordinatorTests.Run),
        ("--facebook-page", FacebookPageTests.Run),
        ("--fresh-profile", FreshProfileInitializationTests.Run),
        ("--tidal-catalog", TidalCatalogContractTests.Run),
        ("--apple-music-external", AppleMusicExternalContractTests.Run),
        ("--apple-music-web", AppleMusicWebCommandTests.Run),
        ("--tidal-playback-probe", TidalPlaybackProbeTests.Run),
        ("--spotify-catalog", SpotifyCatalogContractTests.Run),
        ("--spotify-collection-completion", SpotifyCollectionCompletionTests.Run),
        ("--spotify-profile", SpotifyReleaseProfileTests.Run),
        ("--youtube-account", YouTubeAccountContractTests.Run),
        ("--youtube-public-broadcasts", YouTubePublicBroadcastCoordinatorTests.Run),
        ("--account-settings", AccountSettingsStoreTests.Run),
        ("--wiim", WiiMContractTests.Run),
        ("--wiim-profile", WiiMProfileMutationTests.Run),
        ("--frontier-smart", FrontierSmartContractTests.Run),
        ("--sonos", SonosContractTests.Run),
        ("--sonos-observed-material", SonosObservedMaterialCoordinatorTests.Run),
        ("--observed-material-fixture-contract", ObservedMaterialFixtureExportTests.Run),
        ("--sonos-profile", SonosOwnStreamsMutationTests.Run),
    ];

    public static int Main(string[] args)
    {
        if (args.Contains("--facebook-live", StringComparer.Ordinal))
        {
            try
            {
                FacebookPageTests.RunLive();
                Console.WriteLine("Publiczna strona Facebooka została odczytana.");
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine("Nie odczytano publicznej strony: " + exception);
                return 1;
            }
        }
        // Tryb SERWERA PROTOKOLU: prawdziwa petla hosta na stdin/stdout.
        // Uzywa go test zgodnosci klienta Python w WSL.
        if (args.Contains("--wire-server", StringComparer.Ordinal))
        {
            return WireServer.Run();
        }

        var selectionArgs = args.ToList();
        string? observedFixturePath = null;
        var fixtureOptionIndex = selectionArgs.FindIndex(argument => string.Equals(
            argument, "--observed-material-fixtures", StringComparison.Ordinal));
        if (fixtureOptionIndex >= 0)
        {
            if (selectionArgs.Count(argument => string.Equals(
                    argument, "--observed-material-fixtures", StringComparison.Ordinal)) != 1
                || fixtureOptionIndex == selectionArgs.Count - 1
                || string.IsNullOrWhiteSpace(selectionArgs[fixtureOptionIndex + 1]))
            {
                Console.Error.WriteLine(
                    "--observed-material-fixtures wymaga jednej niepustej ścieżki pliku.");
                return 2;
            }
            observedFixturePath = selectionArgs[fixtureOptionIndex + 1];
            selectionArgs.RemoveRange(fixtureOptionIndex, 2);
            var requiredSuites = new[]
            {
                "--wiim", "--sonos", "--sonos-observed-material",
                "--observed-material-fixture-contract"
            };
            if (selectionArgs.Count == 0
                || requiredSuites.Any(required => !selectionArgs.Contains(required, StringComparer.Ordinal)))
            {
                Console.Error.WriteLine(
                    "Eksport fixture wymaga jawnie: --wiim --sonos --sonos-observed-material --observed-material-fixture-contract.");
                return 2;
            }
        }

        var selected = selectionArgs.Count == 0
            ? Suites
            : Suites.Where(suite => selectionArgs.Contains(suite.Switch, StringComparer.Ordinal)).ToArray();

        if (selected.Length == 0)
        {
            Console.Error.WriteLine("Nie wskazano zadnego znanego zestawu testow.");
            Console.Error.WriteLine("Dostepne: " + string.Join(", ", Suites.Select(suite => suite.Switch)));
            return 2;
        }

        var failures = 0;
        foreach (var (name, run) in selected)
        {
            // Kazdy zestaw ma WLASNY blok bledow: awaria jednego nie ukrywa
            // wynikow pozostalych, a kod wyjscia pozostaje niezerowy.
            try
            {
                run();
            }
            catch (Exception exception)
            {
                failures++;
                Console.Error.WriteLine($"NIEPOWODZENIE {name}: {exception.Message}");
                Console.Error.WriteLine(exception.StackTrace);
            }
        }

        if (failures != 0)
        {
            Console.WriteLine($"Niepowodzenia: {failures} z {selected.Length}.");
            return 1;
        }

        if (observedFixturePath is not null)
        {
            try
            {
                ObservedMaterialFixtureExport.Write(observedFixturePath);
                Console.WriteLine("Wyeksportowano fixture obserwowanego materiału: "
                    + Path.GetFullPath(observedFixturePath));
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine("Nie wyeksportowano fixture obserwowanego materiału: "
                    + exception.Message);
                return 1;
            }
        }

        Console.WriteLine($"Wszystkie zestawy przeszly ({selected.Length}).");
        return 0;
    }
}
