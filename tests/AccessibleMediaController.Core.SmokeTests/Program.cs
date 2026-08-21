using System.Globalization;
using System.Text.Json.Nodes;
using AccessibleMediaController.Core.Commands;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Input;
using AccessibleMediaController.Core.LocalMedia;
using AccessibleMediaController.Core.Playback;
using AccessibleMediaController.Core.Presentation;
using AccessibleMediaController.Core.Sessions;

var tests = new (string Name, Action Test)[]
{
    ("Normalizacja skrótów", TestKeyChords),
    ("Domyślny profil", TestDefaultProfile),
    ("Odświeżanie profilu wbudowanego", TestBuiltInProfileRefresh),
    ("Czytelne nazwy poleceń", TestCommandCatalog),
    ("Konfigurowana kolejność odczytu", TestMediaItemFormatting),
    ("Zwięzłe parametry audio", TestAudioParametersFormatting),
    ("Migracja starszych ustawień", TestLegacyStateMigration),
    ("Migracja ustawień alpha.4", TestVersion2StateMigration),
    ("Migracja komunikatów alpha.5", TestVersion3MessageMigration),
    ("Migracja krótkich komunikatów alpha.7", TestVersion4MessageMigration),
    ("Migracja komunikatów z nazwą elementu alpha.8", TestVersion5MessageMigration),
    ("Migracja nazw w komunikatach Ulubionych alpha.18", TestVersion6FavoriteMessageMigration),
    ("Migracja wspólnego wyciszenia alpha.40", TestVersion9PlayerMessageMigration),
    ("Migracja kategorii komunikatów alpha.41", TestVersion10PlayerMessageMigration),
    ("Przełączanie sesji", TestSessions),
    ("Konfigurowana kolejność sesji", TestSessionOrder),
    ("Pusta sesja lokalna", TestEmptyLocalSession),
    ("Oddzielony tor lokalnego odtwarzania", TestLocalPlaybackBoundary),
    ("Odkrywanie lokalnych plików audio", TestLocalAudioFileDiscovery),
    ("Ponowne włączanie folderu do biblioteki", TestLocalLibraryImport),
    ("Synchronizacja źródeł lokalnej biblioteki", TestLocalLibrarySynchronization),
    ("Integracyjny cykl zmian folderu", TestLocalFolderSynchronizationCycle),
    ("Migracja biblioteki alpha.79", TestVersion17LocalLibraryMigration),
    ("Naprawa pustego źródła po alpha.80", TestVersion18EmptySourceMigration),
    ("Wyszukiwanie w katalogu", TestCatalogSearch),
    ("Historia wyszukiwania", TestSearchHistory),
    ("Historia odtwarzania", TestPlaybackHistory),
    ("Trwałe zakładki", TestBookmarks),
    ("Pamięć widoków sesji", TestSessionNavigationPersistence),
    ("Pamięć lokalnej biblioteki", TestLocalMediaPersistence),
    ("Paleta poleceń", TestCommandPalette),
    ("Cofanie zmian przynależności", TestMembershipHistory),
    ("Zbiorowe zmiany przynależności", TestBatchMembershipCommands),
    ("Krótkie komunikaty czasu", TestTimeCommands),
    ("Skok wpisanym czasem i procentem", TestSeekInputParser),
    ("Trzy rodzaje eksportu", TestExports)
};

var failures = new List<string>();
foreach (var (name, test) in tests)
{
    try
    {
        test();
        Console.WriteLine($"OK: {name}");
    }
    catch (Exception exception)
    {
        failures.Add($"BŁĄD: {name}: {exception.Message}");
    }
}

foreach (var failure in failures) Console.Error.WriteLine(failure);
return failures.Count == 0 ? 0 : 1;

static void TestKeyChords()
{
    Equal("Ctrl+Shift+F", KeyChord.Parse("shift+ctrl+f").Canonical);
    Equal("Ctrl+Alt+Space", KeyChord.Parse("Control+Alt+Spacja").Canonical);
    Equal("Ctrl+Alt+Windows+Enter", KeyChord.Parse("Alt+Win+Control+Enter").Canonical);
    Equal("Ctrl+Alt+Windows+F12", KeyChord.Parse("Windows+Alt+Control+F12").Canonical);
    Equal("PageDown", KeyChord.Parse("PgDn").Canonical);
}

static void TestDefaultProfile()
{
    var settings = new AppSettings();
    Equal("Ctrl+Alt+Windows+F12", settings.PrefixChord);
    Equal(true, settings.Messages.Enabled);
    Equal(false, settings.Messages.DetailedHints);
    Equal(true, settings.Messages.SeekMessages);
    Equal(true, settings.Messages.ArrowSeekMessages);
    Equal(true, settings.Messages.PercentageSeekMessages);
    Equal(true, settings.Messages.BookmarkNavigationMessages);
    Equal(true, settings.Messages.VolumeMessages);
    Equal(true, settings.Messages.PlaybackMessages);
    Equal(PercentageSeekAnnouncementMode.Percent, settings.Messages.PercentageSeekAnnouncement);
    Equal(StartupTarget.MediaList, settings.StartupTarget);

    var profile = KeyboardProfile.CreateDefault();
    Equal(CommandIds.SessionSlot(1), profile.Resolve(KeyChord.Parse("1")));
    True(profile.Resolve(KeyChord.Parse("Ctrl+1")) is null, "Po prefiksie cyfra nie powinna wymagać Control.");
    Equal(CommandIds.ViewFavorites, profile.Resolve(KeyChord.Parse("U")));
    Equal(CommandIds.ToggleFavorite, profile.Resolve(KeyChord.Parse("Shift+U")));
    Equal(CommandIds.ViewAlbums, profile.Resolve(KeyChord.Parse("A")));
    Equal(CommandIds.ViewBookmarks, profile.Resolve(KeyChord.Parse("B")));
    Equal(CommandIds.AddBookmark, profile.Resolve(KeyChord.Parse("Shift+B")));
    True(profile.Resolve(KeyChord.Parse("Shift+A")) is null, "Shift+A pozostaje nieprzypisane.");
    True(profile.Resolve(KeyChord.Parse("Shift+N")) is null, "Skrót oficjalnej aplikacji pozostaje do ustalenia.");
    Equal(CommandIds.FilterCurrent, profile.Resolve(KeyChord.Parse("K")));
    Equal(CommandIds.CommandPalette, profile.Resolve(KeyChord.Parse("Shift+K")));
    Equal(CommandIds.SearchCurrent, profile.Resolve(KeyChord.Parse("F")));
    Equal(CommandIds.SearchAll, profile.Resolve(KeyChord.Parse("Shift+F")));
    Equal(CommandIds.DownloadInService, profile.Resolve(KeyChord.Parse("D")));
    Equal(CommandIds.DownloadToDisk, profile.Resolve(KeyChord.Parse("Shift+D")));
    True(profile.Resolve(KeyChord.Parse("I")) is null, "I nie powinno mieć polecenia informacyjnego po prefiksie.");
    True(profile.Resolve(KeyChord.Parse("Shift+I")) is null, "Shift+I nie powinno mieć polecenia informacyjnego po prefiksie.");
    Equal(CommandIds.TimeElapsed, profile.Resolve(KeyChord.Parse("Ctrl+E")));
    Equal(CommandIds.TimeRemaining, profile.Resolve(KeyChord.Parse("Ctrl+R")));
    Equal(CommandIds.TimeTotal, profile.Resolve(KeyChord.Parse("Ctrl+T")));
}

static void TestBuiltInProfileRefresh()
{
    var directory = Path.Combine(Path.GetTempPath(), $"amc-profile-tests-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    try
    {
        var statePath = Path.Combine(directory, "state.json");
        var store = new ConfigurationStore(statePath);
        var state = ConfigurationStore.CreateDefaultState();
        var oldBuiltIn = state.KeyboardProfiles.Single(profile => profile.Id == "default");
        oldBuiltIn.Bindings.Clear();
        oldBuiltIn.Bindings[KeyChord.Parse("Ctrl+1").Canonical] = CommandIds.SessionSlot(1);

        var custom = oldBuiltIn.CreateEditableCopy("Własny stary profil");
        custom.Bindings[KeyChord.Parse("Ctrl+Enter").Canonical] = "transport.playSelected";
        custom.Bindings[KeyChord.Parse("I").Canonical] = "view.itemInformation";
        custom.Bindings[KeyChord.Parse("Shift+I").Canonical] = "information.playbackStatus";
        state.KeyboardProfiles.Add(custom);
        state.Settings.ActiveKeyboardProfileId = custom.Id;
        store.Save(state);

        var loaded = store.LoadOrCreate();
        var refreshedBuiltIn = loaded.KeyboardProfiles.Single(profile => profile.Id == "default");
        var retainedCustom = loaded.KeyboardProfiles.Single(profile => profile.Id == custom.Id);
        Equal(CommandIds.SessionSlot(1), refreshedBuiltIn.Resolve(KeyChord.Parse("1")));
        True(refreshedBuiltIn.Resolve(KeyChord.Parse("Ctrl+1")) is null, "Profil wbudowany powinien otrzymać nową mapę.");
        Equal(CommandIds.SessionSlot(1), retainedCustom.Resolve(KeyChord.Parse("Ctrl+1")));
        Equal(CommandIds.ActivateSelected, retainedCustom.Resolve(KeyChord.Parse("Ctrl+Enter")));
        True(retainedCustom.Resolve(KeyChord.Parse("I")) is null, "Usunięte polecenie informacji nie może pozostać w profilu.");
        True(retainedCustom.Resolve(KeyChord.Parse("Shift+I")) is null, "Usunięty odczyt stanu nie może pozostać w profilu.");
        Equal(custom.Id, loaded.Settings.ActiveKeyboardProfileId);
    }
    finally
    {
        Directory.Delete(directory, true);
    }
}

static void TestCommandCatalog()
{
    Equal("Odtwórz lub wstrzymaj", CommandCatalog.GetDisplayName(CommandIds.ActivateSelected));
    Equal("Dodaj lub usuń z ulubionych", CommandCatalog.GetDisplayName(CommandIds.ToggleFavorite));
    Equal("Dodaj lub usuń z kolejki", CommandCatalog.GetDisplayName(CommandIds.AddQueue));
    Equal("Ustawienia: szablony komunikatów", CommandCatalog.GetDisplayName(CommandIds.SettingsMessageTemplates));
    Equal("Ustawienia: komunikat po skoku cyfrą", CommandCatalog.GetDisplayName(CommandIds.SettingsPercentageSeekAnnouncement));
    Equal("Przełącz automatyczne komunikaty odtwarzacza", CommandCatalog.GetDisplayName(CommandIds.SettingsToggleSeekMessages));
    Equal("Otwórz lokalne pliki audio", CommandCatalog.GetDisplayName(CommandIds.OpenLocalFiles));
    Equal("Otwórz folder z plikami audio", CommandCatalog.GetDisplayName(CommandIds.OpenLocalFolder));
    Equal("Biblioteka lokalna: pokaż foldery", CommandCatalog.GetDisplayName(CommandIds.ViewFolders));
    Equal("Biblioteka lokalna: pokaż wszystkie pliki", CommandCatalog.GetDisplayName(CommandIds.ViewAllLocalFiles));
    Equal("Odśwież źródła biblioteki lokalnej", CommandCatalog.GetDisplayName(CommandIds.RefreshLocalLibrary));
    Equal("Ustawienia: kolejność sesji i skrótów Ctrl+1–9", CommandCatalog.GetDisplayName(CommandIds.SettingsSessionOrder));
    Equal("Skocz do czasu", CommandCatalog.GetDisplayName(CommandIds.SeekToTime));
    Equal("Skocz do procentu", CommandCatalog.GetDisplayName(CommandIds.SeekToPercentage));
    Equal("Właściwości i informacje", CommandCatalog.GetDisplayName(CommandIds.ItemProperties));
    Equal("Zwiększ prędkość odtwarzania", CommandCatalog.GetDisplayName(CommandIds.PlaybackRateUp));
    Equal("Przywróć normalną prędkość odtwarzania", CommandCatalog.GetDisplayName(CommandIds.PlaybackRateReset));
    Equal("Wybierz sesję 7", CommandCatalog.GetDisplayName(CommandIds.SessionSlot(7)));
    Equal("Przejdź do 50% utworu", CommandCatalog.GetDisplayName(CommandIds.SeekPercent(50)));
    True(CommandIds.TryParseSeekPercent(CommandIds.SeekPercent(90), out var percent), "Identyfikator skoku procentowego powinien być rozpoznawany.");
    Equal(90, percent);
    Equal(10, CommandCatalog.GetAllCommandIds().Count(commandId => CommandIds.TryParseSeekPercent(commandId, out _)));
    Equal("nieznane.polecenie", CommandCatalog.GetDisplayName("nieznane.polecenie"));
}

static void TestMediaItemFormatting()
{
    var item = new MediaItem
    {
        Title = "Przykładowy utwór",
        Artist = "Przykładowy wykonawca",
        Duration = TimeSpan.FromSeconds(222),
        Kind = MediaItemKind.Track
    };
    var artistFirst = new[]
    {
        MediaItemField.Artist,
        MediaItemField.Title,
        MediaItemField.Duration,
        MediaItemField.Kind
    };
    Equal(
        "Przykładowy wykonawca, Przykładowy utwór, 3:42, utwór",
        MediaItemFormatter.Format(item, artistFirst));

    var withoutArtist = new MediaItem
    {
        Title = "Do odsłuchu",
        Duration = TimeSpan.FromMinutes(166),
        Kind = MediaItemKind.Playlist
    };
    Equal(
        "Do odsłuchu, 2:46:00, playlista",
        MediaItemFormatter.Format(withoutArtist, ListDisplaySettings.CreateDefaultFieldOrder()));

    var homogeneousFields = ListDisplaySettings.CreateDefaultFieldOrder()
        .Where(field => field != MediaItemField.Kind);
    Equal(
        "Do odsłuchu, 2:46:00",
        MediaItemFormatter.Format(withoutArtist, homogeneousFields));
}

static void TestLocalLibraryImport()
{
    var existingPath = @"D:\Nagrania\istniejący.mp3";
    var newPath = @"D:\Nagrania\nowy.ogg";
    var existing = new MediaItem
    {
        Id = "local-existing",
        Title = "Istniejący",
        Source = existingPath,
        IsInLibrary = false
    };
    var catalog = new List<MediaItem> { existing };

    var first = LocalLibraryImporter.Import(catalog, [existingPath, newPath, existingPath]);
    Equal(2, first.ImportedItems.Count);
    Equal(1, first.AddedItems.Count);
    Equal(1, first.RestoredItems.Count);
    Equal(true, existing.IsInLibrary);
    Equal(2, catalog.Count);
    Equal(true, catalog.Single(item => item.Source == newPath).IsInLibrary);

    var second = LocalLibraryImporter.Import(catalog, [existingPath, newPath]);
    Equal(2, second.ImportedItems.Count);
    Equal(0, second.AddedItems.Count);
    Equal(0, second.RestoredItems.Count);
    Equal(2, catalog.Count);
}

static void TestLocalLibrarySynchronization()
{
    var root = Path.Combine(Path.GetTempPath(), $"amc-sync-root-{Guid.NewGuid():N}");
    var unavailableRoot = Path.Combine(Path.GetTempPath(), $"amc-sync-offline-{Guid.NewGuid():N}");
    var keepPath = Path.Combine(root, "Album", "zostaje.mp3");
    var missingPath = Path.Combine(root, "znika.flac");
    var excludedPath = Path.Combine(root, "pomijany.ogg");
    var newPath = Path.Combine(root, "nowy.wav");
    var manualPath = Path.Combine(Path.GetTempPath(), "pojedynczy.aac");
    var offlinePath = Path.Combine(unavailableRoot, "offline.mp3");
    var keep = new MediaItem { Id = "keep", Title = "Zostaje", Source = keepPath, IsInLibrary = true };
    var missing = new MediaItem { Id = "missing", Title = "Znika", Source = missingPath, IsInLibrary = true };
    var excluded = new MediaItem { Id = "excluded", Title = "Pomijany", Source = excludedPath, IsInLibrary = true };
    var manual = new MediaItem { Id = "manual", Title = "Pojedynczy", Source = manualPath, IsInLibrary = true };
    var offline = new MediaItem { Id = "offline", Title = "Offline", Source = offlinePath, IsInLibrary = true };
    var catalog = new List<MediaItem> { keep, missing, excluded, manual, offline };

    var first = LocalLibrarySynchronizer.Synchronize(
        catalog,
        [root],
        [keepPath, excludedPath, newPath],
        [excludedPath]);
    Equal(1, first.AddedItems.Count);
    Equal(false, missing.IsAvailable);
    Equal(false, excluded.IsInLibrary);
    Equal(true, excluded.IsAvailable);
    Equal(true, manual.IsAvailable);
    Equal(true, offline.IsAvailable);
    Equal(6, catalog.Count);
    Equal(true, catalog.Single(item => item.Source == newPath).IsInLibrary);

    var second = LocalLibrarySynchronizer.Synchronize(
        catalog,
        [root],
        [keepPath, missingPath, excludedPath, newPath],
        [excludedPath]);
    Equal(0, second.AddedItems.Count);
    Equal(true, missing.IsAvailable);
    Equal(true, missing.IsInLibrary);
    Equal(false, excluded.IsInLibrary);
    Equal(6, catalog.Count);
}

static void TestLocalFolderSynchronizationCycle()
{
    var root = Path.Combine(Path.GetTempPath(), $"amc-sync-cycle-{Guid.NewGuid():N}");
    var nested = Path.Combine(root, "Audycje");
    Directory.CreateDirectory(nested);
    try
    {
        var firstPath = Path.Combine(root, "pierwszy.mp3");
        var secondPath = Path.Combine(nested, "drugi.flac");
        File.WriteAllBytes(firstPath, [1, 2, 3]);
        var catalog = new List<MediaItem>();

        var firstScan = LocalAudioFileDiscovery.FindFiles(root);
        var first = LocalLibrarySynchronizer.Synchronize(catalog, [root], firstScan, []);
        Equal(1, first.AddedItems.Count);
        Equal(true, catalog.Single().IsAvailable);

        File.WriteAllBytes(secondPath, [4, 5, 6]);
        var secondScan = LocalAudioFileDiscovery.FindFiles(root);
        var second = LocalLibrarySynchronizer.Synchronize(catalog, [root], secondScan, []);
        Equal(1, second.AddedItems.Count);
        Equal(2, catalog.Count(item => item.IsAvailable && item.IsInLibrary));

        File.Delete(firstPath);
        var thirdScan = LocalAudioFileDiscovery.FindFiles(root);
        var third = LocalLibrarySynchronizer.Synchronize(catalog, [root], thirdScan, []);
        Equal(1, third.BecameUnavailableItems.Count);
        Equal(false, catalog.Single(item => item.Source == firstPath).IsAvailable);
        Equal(true, catalog.Single(item => item.Source == secondPath).IsAvailable);
    }
    finally
    {
        Directory.Delete(root, true);
    }
}

static void TestVersion17LocalLibraryMigration()
{
    var directory = Path.Combine(Path.GetTempPath(), $"amc-v17-library-tests-{Guid.NewGuid():N}");
    var source = Path.Combine(directory, "Muzyka");
    Directory.CreateDirectory(source);
    try
    {
        var statePath = Path.Combine(directory, "state.json");
        var store = new ConfigurationStore(statePath);
        var state = ConfigurationStore.CreateDefaultState();
        var path = Path.Combine(source, "wykluczony.mp3");
        state.LocalMedia.FolderSources.Add(new LocalFolderSourceSettings
        {
            Id = "source",
            Path = source,
            DisplayName = "Muzyka"
        });
        state.LocalMedia.Items.Add(new LocalMediaItemSettings
        {
            Id = "excluded",
            Title = "Wykluczony",
            Path = path,
            IsInLibrary = false
        });
        state.LocalMedia.Items.Add(new LocalMediaItemSettings
        {
            Id = "included",
            Title = "Pozostawiony",
            Path = Path.Combine(source, "pozostawiony.mp3"),
            IsInLibrary = true
        });
        state.SessionNavigation.Sessions["local"] = new SessionNavigationState
        {
            CurrentView = "Biblioteka"
        };
        store.Save(state);

        var document = JsonNode.Parse(File.ReadAllText(statePath))!.AsObject();
        document["schemaVersion"] = 17;
        var localMedia = document["localMedia"]!.AsObject();
        localMedia.Remove("excludedPaths");
        localMedia.Remove("libraryView");
        foreach (var item in localMedia["items"]!.AsArray()) item!.AsObject().Remove("isAvailable");
        File.WriteAllText(statePath, document.ToJsonString());

        var loaded = store.LoadOrCreate();
        Equal(ConfigurationStore.CurrentSchemaVersion, loaded.SchemaVersion);
        Equal("Wszystkie pliki", loaded.LocalMedia.LibraryView);
        Equal("Wszystkie pliki", loaded.SessionNavigation.Sessions["local"].CurrentView);
        Equal(1, loaded.LocalMedia.ExcludedPaths.Count);
        Equal(Path.GetFullPath(path), loaded.LocalMedia.ExcludedPaths[0]);
        Equal(true, loaded.LocalMedia.Items[0].IsAvailable);
    }
    finally
    {
        Directory.Delete(directory, true);
    }
}

static void TestVersion18EmptySourceMigration()
{
    var directory = Path.Combine(Path.GetTempPath(), $"amc-v18-empty-source-{Guid.NewGuid():N}");
    var source = Path.Combine(directory, "iCloud");
    Directory.CreateDirectory(source);
    try
    {
        var statePath = Path.Combine(directory, "state.json");
        var store = new ConfigurationStore(statePath);
        var state = ConfigurationStore.CreateDefaultState();
        state.LocalMedia.FolderSources.Add(new LocalFolderSourceSettings
        {
            Id = "cloud-source",
            Path = source,
            DisplayName = "iCloud"
        });
        foreach (var name in new[] { "pierwszy.mp3", "drugi.m4a" })
        {
            var path = Path.Combine(source, name);
            state.LocalMedia.Items.Add(new LocalMediaItemSettings
            {
                Id = name,
                Title = Path.GetFileNameWithoutExtension(name),
                Path = path,
                IsInLibrary = false,
                IsAvailable = true
            });
            state.LocalMedia.ExcludedPaths.Add(path);
        }
        store.Save(state);

        var document = JsonNode.Parse(File.ReadAllText(statePath))!.AsObject();
        document["schemaVersion"] = 18;
        File.WriteAllText(statePath, document.ToJsonString());

        var loaded = store.LoadOrCreate();
        Equal(ConfigurationStore.CurrentSchemaVersion, loaded.SchemaVersion);
        Equal(0, loaded.LocalMedia.ExcludedPaths.Count);
        Equal(true, loaded.LocalMedia.Items.All(item => item.IsInLibrary));
    }
    finally
    {
        Directory.Delete(directory, true);
    }
}

static void TestAudioParametersFormatting()
{
    var polish = CultureInfo.GetCultureInfo("pl-PL");
    var exact = new MediaItem
    {
        BitrateKbps = 192,
        SampleRateHz = 48_000
    };
    Equal("192 kb/s, 48 kHz", AudioParametersFormatter.Format(exact, polish));

    var estimated = new MediaItem
    {
        BitrateKbps = 322,
        IsBitrateEstimated = true,
        SampleRateHz = 44_100
    };
    Equal("około 322 kb/s, 44,1 kHz", AudioParametersFormatter.Format(estimated, polish));
    Equal("322 kb/s, 44,1 kHz", AudioParametersFormatter.FormatCompact(estimated, polish));

    Equal(
        "96 kHz",
        AudioParametersFormatter.Format(new MediaItem { SampleRateHz = 96_000 }, polish));
    Equal("brak danych audio", AudioParametersFormatter.Format(new MediaItem(), polish));
    Equal(string.Empty, AudioParametersFormatter.FormatCompact(new MediaItem(), polish));
}

static void TestLegacyStateMigration()
{
    var directory = Path.Combine(Path.GetTempPath(), $"amc-legacy-tests-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    try
    {
        var statePath = Path.Combine(directory, "state.json");
        var store = new ConfigurationStore(statePath);
        store.Save(ConfigurationStore.CreateDefaultState());

        var document = JsonNode.Parse(File.ReadAllText(statePath))?.AsObject()
            ?? throw new InvalidOperationException("Nie udało się utworzyć testowej konfiguracji.");
        document["schemaVersion"] = 1;
        var settings = document["settings"]?.AsObject()
            ?? throw new InvalidOperationException("Brak ustawień w testowej konfiguracji.");
        settings["prefixChord"] = "Ctrl+Alt+Space";
        settings["messages"]!["enabled"] = false;
        settings.Remove("lists");
        settings.Remove("startupTarget");
        File.WriteAllText(statePath, document.ToJsonString());

        var loaded = store.LoadOrCreate();
        Equal(ConfigurationStore.CurrentSchemaVersion, loaded.SchemaVersion);
        Equal("Ctrl+Alt+Windows+F12", loaded.Settings.PrefixChord);
        Equal(true, loaded.Settings.Messages.Enabled);
        Equal(StartupTarget.MediaList, loaded.Settings.StartupTarget);
        Equal(MediaItemField.Title, loaded.Settings.Lists.FieldOrder[0]);
        Equal(4, loaded.Settings.Lists.FieldOrder.Count);
    }
    finally
    {
        Directory.Delete(directory, true);
    }
}

static void TestVersion2StateMigration()
{
    var directory = Path.Combine(Path.GetTempPath(), $"amc-v2-tests-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    try
    {
        var statePath = Path.Combine(directory, "state.json");
        var store = new ConfigurationStore(statePath);
        store.Save(ConfigurationStore.CreateDefaultState());

        var document = JsonNode.Parse(File.ReadAllText(statePath))?.AsObject()
            ?? throw new InvalidOperationException("Nie udało się utworzyć testowej konfiguracji alpha.4.");
        document["schemaVersion"] = 2;
        var settings = document["settings"]?.AsObject()
            ?? throw new InvalidOperationException("Brak ustawień w testowej konfiguracji alpha.4.");
        settings["prefixChord"] = "Ctrl+Alt+Windows+Enter";
        settings["messages"]!["enabled"] = false;
        File.WriteAllText(statePath, document.ToJsonString());

        var loaded = store.LoadOrCreate();
        Equal(ConfigurationStore.CurrentSchemaVersion, loaded.SchemaVersion);
        Equal("Ctrl+Alt+Windows+F12", loaded.Settings.PrefixChord);
        Equal(true, loaded.Settings.Messages.Enabled);

        settings["prefixChord"] = "Ctrl+Alt+Shift+F11";
        File.WriteAllText(statePath, document.ToJsonString());
        loaded = store.LoadOrCreate();
        Equal("Ctrl+Alt+Shift+F11", loaded.Settings.PrefixChord);
    }
    finally
    {
        Directory.Delete(directory, true);
    }
}

static void TestVersion3MessageMigration()
{
    var directory = Path.Combine(Path.GetTempPath(), $"amc-v3-tests-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    try
    {
        var statePath = Path.Combine(directory, "state.json");
        var store = new ConfigurationStore(statePath);
        store.Save(ConfigurationStore.CreateDefaultState());

        var document = JsonNode.Parse(File.ReadAllText(statePath))?.AsObject()
            ?? throw new InvalidOperationException("Nie udało się utworzyć testowej konfiguracji alpha.5.");
        document["schemaVersion"] = 3;
        var templates = document["settings"]?["messages"]?["templates"]?.AsObject()
            ?? throw new InvalidOperationException("Brak szablonów komunikatów w konfiguracji alpha.5.");
        templates.Remove("queue.added");
        templates.Remove("queue.removed");
        templates.Remove("playNext.added");
        templates.Remove("playNext.removed");
        File.WriteAllText(statePath, document.ToJsonString());

        var loaded = store.LoadOrCreate();
        Equal(ConfigurationStore.CurrentSchemaVersion, loaded.SchemaVersion);
        Equal("Dodano do kolejki: {item}", loaded.Settings.Messages.Templates["queue.added"]);
        Equal("Usunięto z następnych: {item}", loaded.Settings.Messages.Templates["playNext.removed"]);
    }
    finally
    {
        Directory.Delete(directory, true);
    }
}

static void TestVersion4MessageMigration()
{
    var directory = Path.Combine(Path.GetTempPath(), $"amc-v4-tests-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    try
    {
        var statePath = Path.Combine(directory, "state.json");
        var store = new ConfigurationStore(statePath);
        store.Save(ConfigurationStore.CreateDefaultState());

        var document = JsonNode.Parse(File.ReadAllText(statePath))?.AsObject()
            ?? throw new InvalidOperationException("Nie udało się utworzyć testowej konfiguracji alpha.6.");
        document["schemaVersion"] = 4;
        var templates = document["settings"]?["messages"]?["templates"]?.AsObject()
            ?? throw new InvalidOperationException("Brak szablonów komunikatów w konfiguracji alpha.6.");
        templates["queue.added"] = "Dodano do kolejki — demonstracja";
        templates["queue.removed"] = "Usunięto z kolejki — demonstracja";
        templates["playNext.added"] = "Ustawiono do odtworzenia jako następne — demonstracja";
        templates["playNext.removed"] = "Usunięto z odtwarzanych jako następne — demonstracja";
        File.WriteAllText(statePath, document.ToJsonString());

        var loaded = store.LoadOrCreate();
        Equal(ConfigurationStore.CurrentSchemaVersion, loaded.SchemaVersion);
        Equal("Dodano do kolejki: {item}", loaded.Settings.Messages.Templates["queue.added"]);
        Equal("Usunięto z kolejki: {item}", loaded.Settings.Messages.Templates["queue.removed"]);
        Equal("Odtwarzaj jako następne: {item}", loaded.Settings.Messages.Templates["playNext.added"]);
        Equal("Usunięto z następnych: {item}", loaded.Settings.Messages.Templates["playNext.removed"]);

        document["schemaVersion"] = 4;
        templates["queue.added"] = "Mój własny komunikat";
        File.WriteAllText(statePath, document.ToJsonString());
        loaded = store.LoadOrCreate();
        Equal("Mój własny komunikat", loaded.Settings.Messages.Templates["queue.added"]);
    }
    finally
    {
        Directory.Delete(directory, true);
    }
}

static void TestVersion5MessageMigration()
{
    var directory = Path.Combine(Path.GetTempPath(), $"amc-v5-tests-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    try
    {
        var statePath = Path.Combine(directory, "state.json");
        var store = new ConfigurationStore(statePath);
        store.Save(ConfigurationStore.CreateDefaultState());

        var document = JsonNode.Parse(File.ReadAllText(statePath))?.AsObject()
            ?? throw new InvalidOperationException("Nie udało się utworzyć testowej konfiguracji alpha.7.");
        document["schemaVersion"] = 5;
        var templates = document["settings"]?["messages"]?["templates"]?.AsObject()
            ?? throw new InvalidOperationException("Brak szablonów komunikatów w konfiguracji alpha.7.");
        templates["queue.added"] = "Dodano do kolejki";
        templates["queue.removed"] = "Usunięto z kolejki";
        templates["playNext.added"] = "Odtwarzaj jako następne";
        templates["playNext.removed"] = "Usunięto z następnych";
        File.WriteAllText(statePath, document.ToJsonString());

        var loaded = store.LoadOrCreate();
        Equal(ConfigurationStore.CurrentSchemaVersion, loaded.SchemaVersion);
        Equal("Dodano do kolejki: {item}", loaded.Settings.Messages.Templates["queue.added"]);
        Equal("Usunięto z następnych: {item}", loaded.Settings.Messages.Templates["playNext.removed"]);

        document["schemaVersion"] = 5;
        templates["queue.added"] = "Własny tekst kolejki";
        File.WriteAllText(statePath, document.ToJsonString());
        loaded = store.LoadOrCreate();
        Equal("Własny tekst kolejki", loaded.Settings.Messages.Templates["queue.added"]);
    }
    finally
    {
        Directory.Delete(directory, true);
    }
}

static void TestSessions()
{
    var settings = new AppSettings();
    var manager = new SessionManager(settings);
    Equal("TIDAL", manager.Current.DisplayName);
    Equal("WiiM", manager.SelectSlot(2)?.DisplayName);
    Equal("Apple Music", manager.MoveSession(-1).DisplayName);
    Equal("TIDAL", manager.SelectSession("tidal")?.DisplayName);
    Equal("tidal", settings.LastSessionId);
    Equal(17, manager.Current.Items.Count);
    Equal(2, manager.Current.Items.Count(item => item.IsInLibrary));
    True(manager.Current.Items.Count(item => item.Title.StartsWith('B')) >= 2, "Dane demonstracyjne powinny umożliwiać powtarzanie litery B.");
    True(manager.Current.Items.Count(item => item.Title.StartsWith('C')) >= 2, "Dane demonstracyjne powinny umożliwiać powtarzanie litery C.");
    True(manager.Current.Items.Any(item => item.IsInQueue), "Kolejka demonstracyjna nie powinna być pusta.");
    var tidalQueueIds = manager.Current.Items
        .Where(item => item.IsInQueue || item.IsPlayNext)
        .Select(item => item.Id)
        .ToArray();
    Equal("Apple Music", manager.SelectSession("appleMusic")?.DisplayName);
    Equal("TIDAL", manager.SelectSession("tidal")?.DisplayName);
    Equal(
        string.Join('|', tidalQueueIds),
        string.Join('|', manager.Current.Items
            .Where(item => item.IsInQueue || item.IsPlayNext)
            .Select(item => item.Id)));
    Equal(true, manager.Current.ToggleQueue(manager.Current.CurrentItem));
    Equal(false, manager.Current.ToggleQueue(manager.Current.CurrentItem));
    Equal(true, manager.Current.TogglePlayNext(manager.Current.CurrentItem));
    Equal(false, manager.Current.TogglePlayNext(manager.Current.CurrentItem));
    Equal(true, manager.Current.TogglePlayNext(manager.Current.CurrentItem));
    Equal(false, manager.Current.ToggleQueue(manager.Current.CurrentItem));
    Equal(false, manager.Current.CurrentItem.IsPlayNext);
    var selected = manager.Current.Items.First(item => item.Title == "Brzeg ciszy");
    True(manager.Current.Play(selected), "Wybrany element powinien dać się odtworzyć.");
    Equal(selected, manager.Current.CurrentItem);
    True(manager.Current.IsPlaying, "Odtwarzanie wybranego elementu powinno uruchomić sesję.");
    True(manager.Current.Play(selected), "Ponowne polecenie odtwarzania nie powinno przełączać na pauzę.");
    True(manager.Current.IsPlaying, "Polecenie odtwarzania ma pozostać jednoznaczne także dla bieżącego elementu.");
    True(manager.Current.Activate(selected), "Ponowne otwarcie bieżącego elementu powinno być obsłużone.");
    Equal(false, manager.Current.IsPlaying);
    True(manager.Current.Activate(selected), "Kolejne otwarcie bieżącego elementu powinno wznowić odtwarzanie.");
    Equal(true, manager.Current.IsPlaying);
    var album = manager.Current.Items.First(item => item.Kind == MediaItemKind.Album);
    Equal("Album demonstracyjny", album.PrimaryText);
    var playlist = manager.Current.Items.First(item => item.Kind == MediaItemKind.Playlist);
    Equal("Do odsłuchu", playlist.PrimaryText);

    settings.LastSessionId = "nieistniejąca";
    manager = new SessionManager(settings);
    Equal("wiim", settings.LastSessionId);
}

static void TestSessionOrder()
{
    var defaults = SessionSlotOrder.CreateDefault();
    Equal("local", defaults[1]);
    Equal("wiim", defaults[2]);
    Equal("tidal", defaults[3]);
    Equal("appleMusic", defaults[4]);

    var settings = new AppSettings
    {
        LastSessionId = "tidal",
        SessionSlots = new Dictionary<int, string>
        {
            [1] = "appleMusic",
            [2] = "tidal",
            [3] = "wiim",
            [4] = "local"
        }
    };
    var manager = new SessionManager(settings);
    Equal("TIDAL", manager.Current.DisplayName);
    Equal("Apple Music", manager.MoveSession(-1).DisplayName);
    Equal("TIDAL", manager.MoveSession(1).DisplayName);

    var local = new MediaItem { Id = "local-order", Title = "Lokalny", Source = @"C:\Muzyka\lokalny.mp3" };
    var (localSession, slot) = manager.AddOrUpdateTransientSession(
        "local", "Pliki lokalne", [local], new FakeMediaOutput(), 1);
    Equal(4, slot);
    Equal(localSession, manager.SelectSlot(4));
    Equal("Pliki lokalne", manager.Sessions[^1].DisplayName);
}

static void TestLocalPlaybackBoundary()
{
    var output = new FakeMediaOutput();
    var item = new MediaItem
    {
        Id = "local-1",
        Title = "Plik testowy",
        Source = @"C:\Muzyka\plik-testowy.mp3"
    };
    var manager = new SessionManager(new AppSettings());
    var (session, slot) = manager.AddOrUpdateTransientSession(
        "local",
        "Pliki lokalne",
        [item],
        output,
        4);

    Equal(1, slot);
    Equal(session, manager.SelectSlot(1));
    Equal(TimeSpan.Zero, session.Position);
    True(session.Activate(item), "Lokalny element powinien uruchamiać wyjście dźwięku.");
    Equal(1, output.PlayCount);
    Equal(item, output.LastItem);
    Equal(35, output.Volume);
    Equal(1d, output.PlaybackRate);
    True(session.SupportsPlaybackRate, "Lokalne wyjście powinno udostępniać regulację prędkości.");

    session.TogglePlayback();
    Equal(1, output.PauseCount);
    Equal(false, session.IsPlaying);
    session.TogglePlayback();
    Equal(2, output.PlayCount);
    Equal(true, session.IsPlaying);
    session.StopPlayback();
    Equal(1, output.StopCount);
    Equal(false, session.IsPlaying);
    session.TogglePlayback();

    output.Position = TimeSpan.FromSeconds(30);
    session.Seek(TimeSpan.FromSeconds(10));
    Equal(TimeSpan.FromSeconds(40), output.Position);
    session.ChangeVolume(5);
    Equal(40, output.Volume);
    True(session.ChangePlaybackRate(1), "Przyspieszenie powinno zostać przekazane do wyjścia audio.");
    Equal(1.25d, session.PlaybackRate);
    Equal(1.25d, output.PlaybackRate);
    True(session.ChangePlaybackRate(-1), "Zwolnienie powinno zostać przekazane do wyjścia audio.");
    Equal(1d, output.PlaybackRate);
    True(session.SetPlaybackRate(2d), "Ustawienie najwyższej prędkości powinno być obsłużone.");
    Equal(2d, output.PlaybackRate);
    True(session.SetPlaybackRate(1d), "Przywrócenie normalnej prędkości powinno być obsłużone.");

    session.AddItems([item]);
    Equal(1, session.Items.Count);
    var nextItem = new MediaItem
    {
        Id = "local-2",
        Title = "Następny plik",
        Source = @"C:\Muzyka\następny-plik.mp3"
    };
    session.AddItems([nextItem]);
    Equal(2, session.Items.Count);
    var removed = session.RemoveItems([item.Id]);
    Equal(1, removed.Count);
    Equal(nextItem, session.CurrentItem);
    Equal(1, session.Items.Count);
    Equal(0, session.RemoveItems([nextItem.Id]).Count);
    session.RestoreItems(removed);
    Equal(2, session.Items.Count);
    Equal(item, session.Items[0]);
    True(session.SelectItem(item), "Przywrócony plik powinien dać się ponownie wybrać.");
    True(session.PlayRelative(1), "Page Down powinien uruchomić następny plik.");
    Equal(nextItem, session.CurrentItem);
    Equal(true, session.IsPlaying);
    True(!session.PlayRelative(1), "Następny plik nie powinien zapętlać końca listy.");
    True(session.PlayRelative(-1), "Page Up powinien uruchomić poprzedni plik.");
    Equal(item, session.CurrentItem);
    True(!session.PlayRelative(-1), "Poprzedni plik nie powinien zapętlać początku listy.");
    True(session.Play(item), "Pierwszy plik powinien ponownie rozpocząć odtwarzanie.");
    Equal(nextItem, session.ContinueAfterPlaybackEnded(item));
    Equal(nextItem, session.CurrentItem);
    Equal(true, session.IsPlaying);
    Equal(nextItem, output.LastItem);
    True(session.ContinueAfterPlaybackEnded(nextItem) is null, "Ostatni plik nie powinien zapętlać listy.");
    Equal(false, session.IsPlaying);
    Equal(TimeSpan.Zero, session.Position);

    var natural = new MediaItem { Id = "natural", Title = "Naturalny następny", Source = @"C:\Muzyka\naturalny.mp3" };
    var queued = new MediaItem { Id = "queued", Title = "Z kolejki", Source = @"C:\Muzyka\kolejka.mp3", IsInQueue = true };
    var playNext = new MediaItem { Id = "play-next", Title = "Jako następny", Source = @"C:\Muzyka\jako-następny.mp3", IsPlayNext = true };
    var prioritySession = new DemoMediaSession(
        "priority",
        "Priorytety",
        [item, natural, queued, playNext],
        output);
    True(prioritySession.Play(item), "Test priorytetów powinien rozpocząć pierwszy element.");
    Equal(playNext, prioritySession.ContinueAfterPlaybackEnded(item));
    Equal(queued, prioritySession.ContinueAfterPlaybackEnded(playNext));
    Equal(natural, prioritySession.ContinueAfterPlaybackEnded(queued));
    True(prioritySession.ContinueAfterPlaybackEnded(natural) is null, "Po powrocie do naturalnej listy wykorzystana kolejka nie powinna zagrać drugi raz.");
    True(!playNext.IsPlayNext && !queued.IsInQueue, "Wykorzystane stany kolejki powinny zostać wyczyszczone.");

    var detached = manager.RemoveTransientSession("local");
    True(detached is not null, "Pusta lokalna sesja powinna dać się odłączyć od menedżera.");
    True(manager.FindSession("local") is null, "Odłączona sesja nie może pozostać na liście.");
    Equal(session, manager.RestoreTransientSession(detached!, makeCurrent: true));
    Equal(session, manager.Current);
}

static void TestEmptyLocalSession()
{
    var output = new FakeMediaOutput();
    var manager = new SessionManager(new AppSettings());
    var (session, slot) = manager.AddOrUpdateTransientSession(
        "local",
        "Pliki lokalne",
        [],
        output,
        1);

    Equal(1, slot);
    Equal(session, manager.SelectSlot(1));
    Equal(false, session.HasItems);
    Equal("Brak elementów w sesji Pliki lokalne", session.CurrentItem.Title);
    session.TogglePlayback();
    session.Move(1);
    session.Seek(TimeSpan.FromSeconds(10));
    Equal(0, output.PlayCount);
    Equal(false, session.IsPlaying);

    var item = new MediaItem
    {
        Id = "local-after-empty",
        Title = "Dodany po uruchomieniu",
        Source = @"C:\Muzyka\dodany.mp3"
    };
    session.AddItems([item]);
    Equal(true, session.HasItems);
    Equal(item, session.CurrentItem);
    True(session.Activate(item), "Plik dodany do pustej sesji powinien dać się odtworzyć.");
    Equal(1, output.PlayCount);
    session.ReplaceItems([]);
    Equal(false, session.HasItems);
    Equal(false, session.IsPlaying);
    Equal(1, output.StopCount);
    session.ReplaceItems([item]);
    Equal(true, session.HasItems);
    Equal(item, session.CurrentItem);
}

static void TestLocalAudioFileDiscovery()
{
    var directory = Path.Combine(Path.GetTempPath(), $"amc-local-folder-tests-{Guid.NewGuid():N}");
    var nested = Path.Combine(directory, "Z Album 2");
    Directory.CreateDirectory(nested);
    try
    {
        File.WriteAllText(Path.Combine(directory, "Utwór 10.mp3"), string.Empty);
        File.WriteAllText(Path.Combine(directory, "Utwór 2.FLAC"), string.Empty);
        File.WriteAllText(Path.Combine(directory, "okładka.jpg"), string.Empty);
        File.WriteAllText(Path.Combine(nested, "01 Intro.opus"), string.Empty);

        var files = LocalAudioFileDiscovery.FindFiles(directory);
        Equal(3, files.Count);
        Equal("Utwór 2.FLAC", Path.GetFileName(files[0]));
        Equal("Utwór 10.mp3", Path.GetFileName(files[1]));
        Equal("01 Intro.opus", Path.GetFileName(files[2]));
    True(LocalAudioFileDiscovery.IsAudioFile("nagranie.aiff"), "AIFF powinien być rozpoznawany.");
    True(!LocalAudioFileDiscovery.IsAudioFile("okładka.jpg"), "Obraz nie może trafić na listę audio.");
    Equal(320, LocalAudioFileDiscovery.EstimateBitrateKbps(4_000_000, TimeSpan.FromSeconds(100)));
    True(LocalAudioFileDiscovery.EstimateBitrateKbps(0, TimeSpan.FromSeconds(100)) is null, "Pusty plik nie ma wiarygodnej przepływności.");
    }
    finally
    {
        Directory.Delete(directory, true);
    }
}

static void TestVersion6FavoriteMessageMigration()
{
    var directory = Path.Combine(Path.GetTempPath(), $"amc-v6-tests-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    try
    {
        var statePath = Path.Combine(directory, "state.json");
        var store = new ConfigurationStore(statePath);
        store.Save(ConfigurationStore.CreateDefaultState());

        var document = JsonNode.Parse(File.ReadAllText(statePath))?.AsObject()
            ?? throw new InvalidOperationException("Nie udało się utworzyć testowej konfiguracji alpha.17.");
        document["schemaVersion"] = 6;
        var templates = document["settings"]?["messages"]?["templates"]?.AsObject()
            ?? throw new InvalidOperationException("Brak szablonów komunikatów w konfiguracji alpha.17.");
        templates["favorite.added"] = "Dodano do ulubionych";
        templates["favorite.removed"] = "Usunięto z ulubionych";
        File.WriteAllText(statePath, document.ToJsonString());

        var loaded = store.LoadOrCreate();
        Equal(ConfigurationStore.CurrentSchemaVersion, loaded.SchemaVersion);
        Equal("Dodano do ulubionych: {item}", loaded.Settings.Messages.Templates["favorite.added"]);
        Equal("Usunięto z ulubionych: {item}", loaded.Settings.Messages.Templates["favorite.removed"]);

        document["schemaVersion"] = 6;
        templates["favorite.added"] = "Moje ulubione: {item}";
        File.WriteAllText(statePath, document.ToJsonString());
        loaded = store.LoadOrCreate();
        Equal("Moje ulubione: {item}", loaded.Settings.Messages.Templates["favorite.added"]);
    }
    finally
    {
        Directory.Delete(directory, true);
    }
}

static void TestVersion9PlayerMessageMigration()
{
    var directory = Path.Combine(Path.GetTempPath(), $"amc-v9-tests-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    try
    {
        var statePath = Path.Combine(directory, "state.json");
        var store = new ConfigurationStore(statePath);
        var state = ConfigurationStore.CreateDefaultState();
        state.SchemaVersion = 8;
        state.Settings.Messages.SeekMessages = false;
        state.Settings.Messages.VolumeMessages = true;
        store.Save(state);

        var loaded = store.LoadOrCreate();
        Equal(ConfigurationStore.CurrentSchemaVersion, loaded.SchemaVersion);
        Equal(false, loaded.Settings.Messages.SeekMessages);
        Equal(false, loaded.Settings.Messages.VolumeMessages);
        Equal(PercentageSeekAnnouncementMode.Percent, loaded.Settings.Messages.PercentageSeekAnnouncement);
    }
    finally
    {
        Directory.Delete(directory, true);
    }
}

static void TestVersion10PlayerMessageMigration()
{
    var directory = Path.Combine(Path.GetTempPath(), $"amc-v10-tests-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    try
    {
        var statePath = Path.Combine(directory, "state.json");
        var store = new ConfigurationStore(statePath);
        var state = ConfigurationStore.CreateDefaultState();
        state.SchemaVersion = 9;
        state.Settings.Messages.SeekMessages = false;
        state.Settings.Messages.ArrowSeekMessages = false;
        state.Settings.Messages.PercentageSeekMessages = false;
        store.Save(state);

        var loaded = store.LoadOrCreate();
        Equal(ConfigurationStore.CurrentSchemaVersion, loaded.SchemaVersion);
        Equal(false, loaded.Settings.Messages.SeekMessages);
        Equal(true, loaded.Settings.Messages.ArrowSeekMessages);
        Equal(true, loaded.Settings.Messages.PercentageSeekMessages);
    }
    finally
    {
        Directory.Delete(directory, true);
    }
}

static void TestCatalogSearch()
{
    var manager = new SessionManager(new AppSettings());
    var currentResults = MediaCatalogSearch.Search([manager.Current], "brzeg ciszy");
    Equal(1, currentResults.Count);
    Equal("Brzeg ciszy", currentResults[0].Item.Title);
    Equal("TIDAL", currentResults[0].Session.DisplayName);

    var globalResults = MediaCatalogSearch.Search(manager.Sessions, "zielony horyzont");
    Equal(3, globalResults.Count);
    True(globalResults.Any(result => result.Session.DisplayName == "Apple Music"), "Wyniki globalne powinny zawierać Apple Music.");
    Equal(0, MediaCatalogSearch.Search(manager.Sessions, "nieistniejący wynik").Count);
    Equal(0, MediaCatalogSearch.Search(manager.Sessions, "   ").Count);
}

static void TestSearchHistory()
{
    var settings = new SearchHistorySettings();
    var history = new SearchQueryHistory(settings);

    True(history.Record("tidal", "  Brzeg ciszy  "), "Pierwsze zapytanie powinno zostać zapisane.");
    True(history.Record(SearchQueryHistory.GlobalScope, "zielony horyzont"), "Zakres globalny powinien mieć osobną historię.");
    Equal("Brzeg ciszy", history.GetEntries("TIDAL")[0]);
    Equal("zielony horyzont", history.GetEntries(SearchQueryHistory.GlobalScope)[0]);
    Equal(1, history.GetEntries("tidal").Count);

    True(history.Record("tidal", "BRZEG CISZY"), "Nowszy zapis powinien zaktualizować pisownię duplikatu.");
    Equal(1, history.GetEntries("tidal").Count);
    Equal("BRZEG CISZY", history.GetEntries("tidal")[0]);
    True(!history.Record("tidal", "BRZEG CISZY"), "Identyczne najnowsze zapytanie nie powinno zmieniać historii.");

    for (var index = 0; index < 25; index++)
    {
        history.Record("tidal", $"Zapytanie {index}");
    }
    Equal(SearchQueryHistory.MaxEntriesPerScope, history.GetEntries("tidal").Count);
    Equal("Zapytanie 24", history.GetEntries("tidal")[0]);
    Equal("Zapytanie 5", history.GetEntries("tidal")[^1]);

    var duplicateScopes = new SearchHistorySettings
    {
        Entries = new Dictionary<string, List<string>>
        {
            ["tidal"] = Enumerable.Range(0, 15).Select(index => $"Pierwsza {index}").ToList(),
            ["TIDAL"] = Enumerable.Range(0, 15).Select(index => $"Druga {index}").ToList()
        }
    };
    var normalizedHistory = new SearchQueryHistory(duplicateScopes);
    Equal(SearchQueryHistory.MaxEntriesPerScope, normalizedHistory.GetEntries("tidal").Count);

    var directory = Path.Combine(Path.GetTempPath(), $"amc-search-history-tests-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    try
    {
        var statePath = Path.Combine(directory, "state.json");
        var store = new ConfigurationStore(statePath);
        var state = ConfigurationStore.CreateDefaultState();
        state.SearchHistory = settings;
        store.Save(state);

        var loaded = store.LoadOrCreate();
        var loadedHistory = new SearchQueryHistory(loaded.SearchHistory);
        Equal("Zapytanie 24", loadedHistory.GetEntries("tidal")[0]);
        Equal("zielony horyzont", loadedHistory.GetEntries(SearchQueryHistory.GlobalScope)[0]);

        var document = JsonNode.Parse(File.ReadAllText(statePath))?.AsObject()
            ?? throw new InvalidOperationException("Nie udało się odczytać testowej historii.");
        document["schemaVersion"] = 7;
        document.Remove("searchHistory");
        File.WriteAllText(statePath, document.ToJsonString());
        loaded = store.LoadOrCreate();
        Equal(ConfigurationStore.CurrentSchemaVersion, loaded.SchemaVersion);
        Equal(0, new SearchQueryHistory(loaded.SearchHistory).GetEntries("tidal").Count);
    }
    finally
    {
        Directory.Delete(directory, true);
    }
}

static void TestPlaybackHistory()
{
    var settings = new PlaybackHistorySettings();
    var history = new PlaybackHistory(settings);
    True(history.Record("local", "a"), "Pierwszy plik powinien trafić do historii.");
    True(history.Record("local", "b"), "Nowszy plik powinien trafić na początek.");
    True(history.Record("local", "a"), "Ponowne odtworzenie powinno przenieść plik na początek.");
    Equal("a", history.GetItemIds("local")[0]);
    Equal("b", history.GetItemIds("local")[1]);
    Equal(2, history.GetItemIds("local").Count);
    True(!history.Record("local", "a"), "Powtórzenie najnowszego wpisu nie powinno zmieniać historii.");

    history.Remove("local", ["a"]);
    Equal(1, history.GetItemIds("local").Count);
    Equal("b", history.GetItemIds("local")[0]);

    var directory = Path.Combine(Path.GetTempPath(), $"amc-playback-history-tests-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    try
    {
        var store = new ConfigurationStore(Path.Combine(directory, "state.json"));
        var state = ConfigurationStore.CreateDefaultState();
        state.LocalMedia.Items =
        [
            new LocalMediaItemSettings { Id = "b", Title = "B", Path = "B.mp3" }
        ];
        history.Record("local", "usunięty");
        state.PlaybackHistory = settings;
        store.Save(state);
        var loaded = store.LoadOrCreate();
        var loadedHistory = new PlaybackHistory(loaded.PlaybackHistory).GetItemIds("local");
        Equal(1, loadedHistory.Count);
        Equal("b", loadedHistory[0]);
    }
    finally
    {
        Directory.Delete(directory, true);
    }
}

static void TestBookmarks()
{
    var settings = new BookmarkSettings();
    var index = new BookmarkIndex(settings);
    var item = new MediaItem
    {
        Id = "local-1",
        Title = "Długie nagranie",
        Duration = TimeSpan.FromMinutes(90)
    };
    var now = new DateTime(2026, 8, 20, 12, 0, 0, DateTimeKind.Utc);

    var first = index.Add("local", "Pliki lokalne", item, TimeSpan.FromMinutes(10), now);
    True(first.Added, "Pierwsza zakładka powinna zostać dodana.");
    var duplicate = index.Add("local", "Pliki lokalne", item, TimeSpan.FromMinutes(10).Add(TimeSpan.FromMilliseconds(400)), now.AddSeconds(1));
    True(!duplicate.Added, "Druga zakładka w tej samej sekundzie nie powinna tworzyć duplikatu.");
    var second = index.Add("local", "Pliki lokalne", item, TimeSpan.FromMinutes(25), now.AddMinutes(1));
    True(second.Added, "Zakładka w innym miejscu powinna zostać dodana.");
    var third = index.Add("local", "Pliki lokalne", item, TimeSpan.FromMinutes(40), now.AddMinutes(2));
    True(third.Added, "Trzecia zakładka powinna zostać dodana.");
    var named = index.Add("local", "Pliki lokalne", item, TimeSpan.FromMinutes(25), now.AddMinutes(3), "  Ważny   fragment  ");
    True(!named.Added, "Nazwanie istniejącej pozycji nie powinno tworzyć duplikatu.");
    True(named.NameChanged, "Istniejąca szybka zakładka powinna otrzymać nazwę.");
    Equal("Ważny fragment", named.Entry.Name);
    var sameName = index.Add("local", "Pliki lokalne", item, TimeSpan.FromMinutes(25), now.AddMinutes(4), "Ważny fragment");
    True(!sameName.NameChanged, "Ponowne zapisanie tej samej nazwy nie powinno zgłaszać zmiany.");
    Equal(3, index.GetForItem("local", item.Id).Count);
    Equal(first.Entry.Id, index.FindRelative("local", item.Id, TimeSpan.FromMinutes(20), -1)?.Id);
    Equal(second.Entry.Id, index.FindRelative("local", item.Id, TimeSpan.FromMinutes(20), 1)?.Id);
    Equal(first.Entry.Id, index.FindRelative("local", item.Id, TimeSpan.FromMinutes(25).Add(TimeSpan.FromMilliseconds(400)), -1)?.Id);
    Equal(second.Entry.Id, index.FindAdjacent("local", item.Id, third.Entry.Id, -1)?.Id);
    Equal(first.Entry.Id, index.FindAdjacent("local", item.Id, second.Entry.Id, -1)?.Id);
    Equal(third.Entry.Id, index.FindAdjacent("local", item.Id, second.Entry.Id, 1)?.Id);
    Equal(null, index.FindAdjacent("local", item.Id, first.Entry.Id, -1)?.Id);
    Equal(null, index.FindAdjacent("local", item.Id, third.Entry.Id, 1)?.Id);
    Equal(third.Entry.Id, index.GetAll()[0].Id);

    var early = index.Add("local", "Pliki lokalne", item, TimeSpan.FromMinutes(5), now.AddMinutes(5));
    var otherItem = new MediaItem
    {
        Id = "apple-1",
        Title = "Inny materiał",
        Duration = TimeSpan.FromMinutes(30)
    };
    var other = index.Add("appleMusic", "Apple Music", otherItem, TimeSpan.FromMinutes(1), now.AddMinutes(6));
    var display = index.GetForDisplay("local", item.Id);
    Equal(early.Entry.Id, display[0].Id);
    Equal(first.Entry.Id, display[1].Id);
    Equal(second.Entry.Id, display[2].Id);
    Equal(third.Entry.Id, display[3].Id);
    Equal(other.Entry.Id, display[4].Id);

    var directory = Path.Combine(Path.GetTempPath(), $"amc-bookmark-tests-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    try
    {
        var store = new ConfigurationStore(Path.Combine(directory, "state.json"));
        var state = ConfigurationStore.CreateDefaultState();
        state.Bookmarks = settings;
        store.Save(state);
        var loaded = store.LoadOrCreate();
        Equal(5, new BookmarkIndex(loaded.Bookmarks).GetAll().Count);
        Equal("Długie nagranie", loaded.Bookmarks.Entries.Single(entry => entry.Id == first.Entry.Id).ItemTitle);
        Equal("Ważny fragment", loaded.Bookmarks.Entries.Single(entry => entry.Id == second.Entry.Id).Name);
    }
    finally
    {
        Directory.Delete(directory, true);
    }

    Equal(1, index.Remove([first.Entry.Id]));
    Equal(4, index.GetAll().Count);
}

static void TestSessionNavigationPersistence()
{
    var directory = Path.Combine(Path.GetTempPath(), $"amc-navigation-tests-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    try
    {
        var store = new ConfigurationStore(Path.Combine(directory, "state.json"));
        var state = ConfigurationStore.CreateDefaultState();
        state.SessionNavigation.Sessions["tidal"] = new SessionNavigationState
        {
            CurrentView = "Ulubione",
            PlayerActive = true,
            SelectedItemIds = new Dictionary<string, string?>
            {
                ["Ulubione"] = "tidal-14"
            },
            Filters = new Dictionary<string, string>
            {
                ["Ulubione"] = "północ"
            }
        };
        state.SessionNavigation.Sessions["appleMusic"] = new SessionNavigationState
        {
            CurrentView = "Albumy",
            PlayerActive = false
        };

        store.Save(state);
        var loaded = store.LoadOrCreate();
        var tidal = loaded.SessionNavigation.Sessions["TIDAL"];
        Equal("Ulubione", tidal.CurrentView);
        Equal(true, tidal.PlayerActive);
        Equal("tidal-14", tidal.SelectedItemIds["ulubione"]);
        Equal("północ", tidal.Filters["ULUBIONE"]);
        Equal("Albumy", loaded.SessionNavigation.Sessions["appleMusic"].CurrentView);
        Equal(false, loaded.SessionNavigation.Sessions["appleMusic"].PlayerActive);
    }
    finally
    {
        Directory.Delete(directory, true);
    }
}

static void TestLocalMediaPersistence()
{
    var directory = Path.Combine(Path.GetTempPath(), $"amc-local-state-tests-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    try
    {
        var store = new ConfigurationStore(Path.Combine(directory, "state.json"));
        var state = ConfigurationStore.CreateDefaultState();
        state.LocalMedia.CurrentItemId = "local-1";
        state.LocalMedia.Volume = 47;
        state.LocalMedia.PlaybackRate = 1.50d;
        state.LocalMedia.FolderSources.Add(new LocalFolderSourceSettings
        {
            Id = "folder-1",
            Path = directory,
            DisplayName = "Nagrania"
        });
        state.LocalMedia.CurrentFolderPath = directory;
        state.LocalMedia.LibraryView = "Foldery";
        state.LocalMedia.ExcludedPaths.Add(@"C:\Muzyka\pomijany.mp3");
        state.LocalMedia.Items.Add(new LocalMediaItemSettings
        {
            Id = "local-1",
            Title = "Długie nagranie",
            Path = @"C:\Muzyka\długie.aac",
            DurationTicks = TimeSpan.FromMinutes(90).Ticks,
            ResumePositionTicks = TimeSpan.FromMinutes(17).Ticks,
            FileLength = 123456,
            LastWriteUtcTicks = 987654,
            IsFavorite = true,
            IsInLibrary = true,
            IsInQueue = true
        });

        store.Save(state);
        var loaded = store.LoadOrCreate();
        Equal(ConfigurationStore.CurrentSchemaVersion, loaded.SchemaVersion);
        Equal("local-1", loaded.LocalMedia.CurrentItemId);
        Equal(47, loaded.LocalMedia.Volume);
        Equal(1.50d, loaded.LocalMedia.PlaybackRate);
        Equal(1, loaded.LocalMedia.Items.Count);
        Equal(1, loaded.LocalMedia.FolderSources.Count);
        Equal("Foldery", loaded.LocalMedia.LibraryView);
        Equal(Path.GetFullPath(@"C:\Muzyka\pomijany.mp3"), loaded.LocalMedia.ExcludedPaths[0]);
        Equal("Nagrania", loaded.LocalMedia.FolderSources[0].DisplayName);
        Equal(Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory)), loaded.LocalMedia.CurrentFolderPath);
        Equal("local-1", new PlaybackHistory(loaded.PlaybackHistory).GetItemIds("local")[0]);
        var item = loaded.LocalMedia.Items[0];
        Equal("Długie nagranie", item.Title);
        Equal(TimeSpan.FromMinutes(17).Ticks, item.ResumePositionTicks);
        Equal(true, item.IsFavorite);
        Equal(true, item.IsInQueue);
        Equal(true, item.IsAvailable);

        var output = new FakeMediaOutput();
        var media = new MediaItem { Id = item.Id, Title = item.Title, Source = item.Path };
        var session = new DemoMediaSession("local", "Pliki lokalne", [media], output);
        session.SetRememberedPosition(item.Id, TimeSpan.FromTicks(item.ResumePositionTicks));
        Equal(TimeSpan.FromMinutes(17), session.Position);
        session.TogglePlayback();
        Equal(TimeSpan.FromMinutes(17), output.Position);
    }
    finally
    {
        Directory.Delete(directory, true);
    }
}

static void TestCommandPalette()
{
    var profile = KeyboardProfile.CreateDefault();
    var settings = new AppSettings();
    settings.Messages.Enabled = true;
    settings.Messages.DetailedHints = false;
    var entries = CommandPaletteSearch.CreateEntries(profile, settings);

    True(entries.Count >= 40, "Paleta powinna zawierać pełny katalog poleceń.");
    True(entries.All(entry => entry.CommandId != CommandIds.CommandPalette), "Paleta nie powinna uruchamiać samej siebie.");
    Equal(9, entries.Count(entry => entry.CommandId.StartsWith("session.slot.", StringComparison.Ordinal)));
    Equal(
        "Wybierz sesję 1: Pliki lokalne",
        entries.Single(entry => entry.CommandId == CommandIds.SessionSlot(1)).DisplayName);

    var favorites = entries.Single(entry => entry.CommandId == CommandIds.ViewFavorites);
    Equal("Ctrl+U", favorites.LocalShortcut);
    Equal("U", favorites.PrefixShortcut);
    True(favorites.Label.Contains("Ctrl+U", StringComparison.Ordinal), "Etykieta powinna podawać skrót działający w oknie.");
    True(favorites.Label.Contains("prefiks U", StringComparison.Ordinal), "Etykieta powinna podawać aktywny skrót po prefiksie.");
    Equal(favorites.Label, favorites.ToString());
    True(!favorites.ToString().Contains("CommandId", StringComparison.Ordinal), "Lista nie może ujawniać technicznych nazw pól obiektu.");
    Equal("Ctrl+O", entries.Single(entry => entry.CommandId == CommandIds.OpenLocalFiles).LocalShortcut);
    Equal("Ctrl+Shift+O", entries.Single(entry => entry.CommandId == CommandIds.OpenLocalFolder).LocalShortcut);
    True(entries.Any(entry => entry.CommandId == CommandIds.ViewFolders), "Paleta powinna zawierać widok folderów.");
    True(entries.Any(entry => entry.CommandId == CommandIds.SettingsSessionOrder), "Paleta powinna zawierać ustawienia kolejności sesji.");
    Equal("Ctrl+H", entries.Single(entry => entry.CommandId == CommandIds.ViewHistory).LocalShortcut);
    var bookmarks = entries.Single(entry => entry.CommandId == CommandIds.ViewBookmarks);
    Equal("Ctrl+B", bookmarks.LocalShortcut);
    Equal("B", bookmarks.PrefixShortcut);
    Equal("B (odtwarzacz)", entries.Single(entry => entry.CommandId == CommandIds.AddBookmark).LocalShortcut);
    Equal("Ctrl+Shift+B (odtwarzacz)", entries.Single(entry => entry.CommandId == CommandIds.AddNamedBookmark).LocalShortcut);
    Equal("Shift+PageUp (odtwarzacz)", entries.Single(entry => entry.CommandId == CommandIds.PreviousBookmark).LocalShortcut);
    Equal("Shift+PageDown (odtwarzacz)", entries.Single(entry => entry.CommandId == CommandIds.NextBookmark).LocalShortcut);
    Equal("Ctrl+J (odtwarzacz)", entries.Single(entry => entry.CommandId == CommandIds.SeekToTime).LocalShortcut);
    Equal("Ctrl+Shift+J (odtwarzacz)", entries.Single(entry => entry.CommandId == CommandIds.SeekToPercentage).LocalShortcut);
    Equal("PageUp (odtwarzacz)", entries.Single(entry => entry.CommandId == CommandIds.Previous).LocalShortcut);
    Equal("PageDown (odtwarzacz)", entries.Single(entry => entry.CommandId == CommandIds.Next).LocalShortcut);
    var itemProperties = entries.Single(entry => entry.CommandId == CommandIds.ItemProperties);
    Equal("Alt+Enter", itemProperties.LocalShortcut);
    True(itemProperties.PrefixShortcut is null, "Właściwości nie mają skrótu prefiksowego.");
    True(entries.All(entry => entry.CommandId != "view.itemInformation"), "Stare polecenie informacji nie może być w palecie.");
    True(entries.All(entry => entry.CommandId != "information.playbackStatus"), "Stary odczyt stanu nie może być w palecie.");
    Equal("Left (odtwarzacz)", entries.Single(entry => entry.CommandId == CommandIds.SeekBackward10).LocalShortcut);
    Equal("Shift+Left (odtwarzacz)", entries.Single(entry => entry.CommandId == CommandIds.SeekBackward30).LocalShortcut);
    Equal("Ctrl+Left (odtwarzacz)", entries.Single(entry => entry.CommandId == CommandIds.SeekBackward60).LocalShortcut);
    Equal("Up (odtwarzacz)", entries.Single(entry => entry.CommandId == CommandIds.VolumeUp5).LocalShortcut);
    Equal("Shift+, (odtwarzacz)", entries.Single(entry => entry.CommandId == CommandIds.PlaybackRateDown).LocalShortcut);
    Equal("Shift+. (odtwarzacz)", entries.Single(entry => entry.CommandId == CommandIds.PlaybackRateUp).LocalShortcut);
    Equal("Ctrl+. (odtwarzacz)", entries.Single(entry => entry.CommandId == CommandIds.PlaybackRateReset).LocalShortcut);
    Equal("Ctrl+Shift+E", entries.Single(entry => entry.CommandId == CommandIds.TimeElapsed).LocalShortcut);
    Equal("F6", entries.Single(entry => entry.CommandId == CommandIds.ViewNowPlaying).LocalShortcut);
    Equal("0 (odtwarzacz)", entries.Single(entry => entry.CommandId == CommandIds.SeekPercent(0)).LocalShortcut);
    Equal("9 (odtwarzacz)", entries.Single(entry => entry.CommandId == CommandIds.SeekPercent(90)).LocalShortcut);
    Equal(10, entries.Count(entry => CommandIds.TryParseSeekPercent(entry.CommandId, out _)));
    True(
        entries.Single(entry => entry.CommandId == CommandIds.OpenOfficialApp).LocalShortcut is null,
        "Otwieranie w oficjalnej aplikacji nie powinno kolidować ze skrótem folderu.");
    Equal("Alt+1 (lista lokalna)", entries.Single(entry => entry.CommandId == CommandIds.ViewFolders).LocalShortcut);
    Equal("Alt+2 (lista lokalna)", entries.Single(entry => entry.CommandId == CommandIds.ViewAllLocalFiles).LocalShortcut);
    Equal("F5 (lista lokalna)", entries.Single(entry => entry.CommandId == CommandIds.RefreshLocalLibrary).LocalShortcut);

    var remaining = CommandPaletteSearch.Filter(entries, "czas pozostaly");
    Equal(1, remaining.Count);
    Equal(CommandIds.TimeRemaining, remaining[0].CommandId);

    var shiftedFavorite = CommandPaletteSearch.Filter(entries, "shift u");
    True(shiftedFavorite.Any(entry => entry.CommandId == CommandIds.ToggleFavorite), "Powinno dać się filtrować także po skrócie.");
    Equal(3, CommandPaletteSearch.Filter(entries, "predkosc").Count);
    Equal(0, CommandPaletteSearch.Filter(entries, "polecenie-którego-nie-ma").Count);

    Equal("p", CommandPaletteSearch.ContinueOrRestartListQuery(entries, "sesja", "p"));
    Equal(string.Empty, CommandPaletteSearch.ContinueOrRestartListQuery(entries, "sesja", "§"));

    var messages = entries.Single(entry => entry.CommandId == CommandIds.SettingsToggleMessages);
    Equal("Komunikaty dostępności: włączone. Enter: wyłącz", messages.DisplayName);
    var hints = entries.Single(entry => entry.CommandId == CommandIds.SettingsToggleDetailedHints);
    Equal("Szczegółowe podpowiedzi klawiatury: wyłączone. Enter: włącz", hints.DisplayName);
    var seekMessages = entries.Single(entry => entry.CommandId == CommandIds.SettingsToggleSeekMessages);
    Equal("Automatyczne komunikaty odtwarzacza: włączone. Enter: wyłącz", seekMessages.DisplayName);
    Equal("Ctrl+Shift+G", seekMessages.LocalShortcut);
    Equal(
        "Komunikaty przewijania strzałkami: włączone. Enter: ustawienia",
        entries.Single(entry => entry.CommandId == CommandIds.SettingsArrowSeekMessages).DisplayName);
    Equal(
        "Komunikaty skoków cyframi: włączone. Enter: ustawienia",
        entries.Single(entry => entry.CommandId == CommandIds.SettingsPercentageSeekMessages).DisplayName);
    Equal(
        "Komunikaty nawigacji po zakładkach: włączone. Enter: ustawienia",
        entries.Single(entry => entry.CommandId == CommandIds.SettingsBookmarkNavigationMessages).DisplayName);
    Equal(
        "Komunikaty zmian głośności: włączone. Enter: ustawienia",
        entries.Single(entry => entry.CommandId == CommandIds.SettingsVolumeMessages).DisplayName);
    Equal(
        "Komunikaty odtwarzania i pauzy: włączone. Enter: ustawienia",
        entries.Single(entry => entry.CommandId == CommandIds.SettingsPlaybackMessages).DisplayName);
    Equal(
        "Komunikat po skoku cyfrą: tylko procent",
        entries.Single(entry => entry.CommandId == CommandIds.SettingsPercentageSeekAnnouncement).DisplayName);
    Equal("Ctrl+,", entries.Single(entry => entry.CommandId == CommandIds.SettingsGeneral).LocalShortcut);
    True(
        entries.Any(entry => entry.CommandId == CommandIds.SettingsImportFullBackup),
        "Paleta powinna udostępniać wszystkie bezpieczne wejścia do ustawień.");
    True(
        CommandPaletteSearch.Filter(entries, "szablony komunikatow")
            .Any(entry => entry.CommandId == CommandIds.SettingsMessageTemplates),
        "Ustawienia powinny być wyszukiwalne bez polskich znaków.");
    True(
        CommandPaletteSearch.Filter(entries, "ustawienia")
            .Any(entry => entry.CommandId == CommandIds.SettingsToggleMessages),
        "Wspólne wyszukiwanie ustawień powinno obejmować także bezpośrednie przełączniki.");

    settings.Messages.Enabled = false;
    settings.Messages.DetailedHints = true;
    settings.Messages.SeekMessages = false;
    settings.Messages.ArrowSeekMessages = false;
    settings.Messages.PercentageSeekMessages = false;
    settings.Messages.BookmarkNavigationMessages = false;
    settings.Messages.VolumeMessages = false;
    settings.Messages.PlaybackMessages = false;
    settings.Messages.PercentageSeekAnnouncement = PercentageSeekAnnouncementMode.PercentAndTime;
    var changedEntries = CommandPaletteSearch.CreateEntries(profile, settings);
    Equal(
        "Komunikaty dostępności: wyłączone. Enter: włącz",
        changedEntries.Single(entry => entry.CommandId == CommandIds.SettingsToggleMessages).DisplayName);
    Equal(
        "Szczegółowe podpowiedzi klawiatury: włączone. Enter: wyłącz",
        changedEntries.Single(entry => entry.CommandId == CommandIds.SettingsToggleDetailedHints).DisplayName);
    Equal(
        "Automatyczne komunikaty odtwarzacza: wyłączone. Enter: włącz",
        changedEntries.Single(entry => entry.CommandId == CommandIds.SettingsToggleSeekMessages).DisplayName);
    Equal(
        "Komunikaty przewijania strzałkami: wyłączone. Enter: ustawienia",
        changedEntries.Single(entry => entry.CommandId == CommandIds.SettingsArrowSeekMessages).DisplayName);
    Equal(
        "Komunikaty nawigacji po zakładkach: wyłączone. Enter: ustawienia",
        changedEntries.Single(entry => entry.CommandId == CommandIds.SettingsBookmarkNavigationMessages).DisplayName);
    Equal(
        "Komunikat po skoku cyfrą: procent i czas",
        changedEntries.Single(entry => entry.CommandId == CommandIds.SettingsPercentageSeekAnnouncement).DisplayName);
}

static void TestMembershipHistory()
{
    var item = new MediaItem
    {
        Title = "Element do przywrócenia",
        IsFavorite = true,
        IsInLibrary = true,
        IsInQueue = true,
        IsPlayNext = true
    };
    var history = new MediaMembershipHistory();
    var originalState = MediaMembershipState.From(item);

    item.IsInLibrary = false;
    item.IsInQueue = false;
    item.IsPlayNext = false;
    history.Record("tidal", item, originalState, "Przywrócono element");
    Equal(1, history.Count);

    var undo = history.Undo();
    True(undo is not null, "Historia powinna zwrócić ostatnią zmianę.");
    Equal("tidal", undo!.SessionId);
    Equal("Przywrócono element", undo.Announcement);
    Equal(originalState, MediaMembershipState.From(item));
    Equal(0, history.Count);
    True(history.Undo() is null, "Pusta historia nie powinna zwracać zmiany.");

    history.Record("tidal", item, MediaMembershipState.From(item), "Bez zmiany");
    Equal(0, history.Count);

    var second = new MediaItem { Title = "Drugi element", IsFavorite = true };
    var firstBeforeBatch = MediaMembershipState.From(item);
    var secondBeforeBatch = MediaMembershipState.From(second);
    item.IsFavorite = false;
    second.IsFavorite = false;
    history.RecordBatch(
        "tidal",
        [(item, firstBeforeBatch), (second, secondBeforeBatch)],
        "Przywrócono dwa elementy");
    Equal(1, history.Count);
    var batchUndo = history.Undo();
    Equal(2, batchUndo!.Items.Count);
    True(item.IsFavorite && second.IsFavorite, "Jedno cofnięcie powinno przywrócić całą zmianę zbiorową.");
}

static void TestBatchMembershipCommands()
{
    var settings = new AppSettings();
    var sessions = new SessionManager(settings);
    var first = sessions.Current.Items[0];
    var second = sessions.Current.Items[1];
    first.IsFavorite = false;
    second.IsFavorite = false;
    first.IsInQueue = false;
    second.IsInQueue = false;
    first.IsPlayNext = false;
    second.IsPlayNext = false;
    var sink = new FakeSink();
    var actions = new FakeActions(first, [first, second]);
    var router = new CommandRouter(sessions, settings, sink, actions);

    router.Execute(CommandIds.ToggleFavorite);
    Equal(true, first.IsFavorite);
    Equal(true, second.IsFavorite);
    True(sink.LastMessage.Contains("2 elementy", StringComparison.Ordinal), "Komunikat powinien podawać liczbę elementów.");
    router.Execute(CommandIds.ToggleFavorite);
    Equal(false, first.IsFavorite);
    Equal(false, second.IsFavorite);

    router.Execute(CommandIds.AddQueue);
    Equal(true, first.IsInQueue);
    Equal(true, second.IsInQueue);
    router.Execute(CommandIds.AddQueue);
    Equal(false, first.IsInQueue);
    Equal(false, second.IsInQueue);
}

static void TestTimeCommands()
{
    var settings = new AppSettings();
    var sessions = new SessionManager(settings);
    var sink = new FakeSink();
    var actions = new FakeActions(sessions.Current.CurrentItem);
    var router = new CommandRouter(sessions, settings, sink, actions);

    router.Execute(CommandIds.TimeElapsed);
    Equal("1:23", sink.LastMessage);
    router.Execute(CommandIds.TimeTotal);
    True(!sink.LastMessage.Contains("czas", StringComparison.OrdinalIgnoreCase), "Komunikat czasu powinien zawierać tylko wartość.");
    settings.Messages.SeekMessages = false;
    settings.Messages.VolumeMessages = false;
    var messageBeforeSilentSeek = sink.LastMessage;
    router.Execute(CommandIds.VolumeUp5);
    Equal(40, sessions.Current.Volume);
    Equal(messageBeforeSilentSeek, sink.LastMessage);
    router.Execute(CommandIds.SeekForward30);
    Equal(TimeSpan.FromSeconds(113), sessions.Current.Position);
    Equal(messageBeforeSilentSeek, sink.LastMessage);
    router.Execute(CommandIds.SeekBackward30);
    Equal(TimeSpan.FromSeconds(83), sessions.Current.Position);
    router.Execute(CommandIds.TrackEnd);
    Equal(sessions.Current.CurrentItem.Duration - TimeSpan.FromSeconds(10), sessions.Current.Position);
    Equal(messageBeforeSilentSeek, sink.LastMessage);
    router.Execute(CommandIds.TimeElapsed);
    Equal(CommandRouter.FormatTime(sessions.Current.Position), sink.LastMessage);
    var messageBeforePercentSeek = sink.LastMessage;
    router.Execute(CommandIds.SeekPercent(50));
    Equal(
        TimeSpan.FromTicks((long)Math.Round(sessions.Current.CurrentItem.Duration.Ticks * 0.5d)),
        sessions.Current.Position);
    Equal(messageBeforePercentSeek, sink.LastMessage);
    router.Execute(CommandIds.TimeElapsed);
    Equal(CommandRouter.FormatTime(sessions.Current.Position), sink.LastMessage);
    settings.Messages.SeekMessages = true;
    settings.Messages.VolumeMessages = true;
    router.Execute(CommandIds.SeekPercent(90));
    Equal(
        TimeSpan.FromTicks((long)Math.Round(sessions.Current.CurrentItem.Duration.Ticks * 0.9d)),
        sessions.Current.Position);
    Equal("90%", sink.LastMessage);
    router.Execute(CommandIds.SeekPercent(0));
    Equal(TimeSpan.Zero, sessions.Current.Position);
    Equal("0%", sink.LastMessage);
    settings.Messages.PercentageSeekAnnouncement = PercentageSeekAnnouncementMode.Time;
    router.Execute(CommandIds.SeekPercent(50));
    Equal(CommandRouter.FormatTime(sessions.Current.Position), sink.LastMessage);
    settings.Messages.PercentageSeekAnnouncement = PercentageSeekAnnouncementMode.PercentAndTime;
    router.Execute(CommandIds.SeekPercent(20));
    Equal($"20%, {CommandRouter.FormatTime(sessions.Current.Position)}", sink.LastMessage);
    settings.Messages.ArrowSeekMessages = false;
    var messageBeforeSilentArrowSeek = sink.LastMessage;
    router.Execute(CommandIds.SeekForward10);
    Equal(messageBeforeSilentArrowSeek, sink.LastMessage);
    settings.Messages.ArrowSeekMessages = true;
    settings.Messages.PercentageSeekMessages = false;
    var messageBeforeSilentPercentageSeek = sink.LastMessage;
    router.Execute(CommandIds.SeekPercent(30));
    Equal(messageBeforeSilentPercentageSeek, sink.LastMessage);
    settings.Messages.PercentageSeekMessages = true;
    settings.Messages.VolumeMessages = false;
    var messageBeforeSilentVolume = sink.LastMessage;
    router.Execute(CommandIds.VolumeUp5);
    Equal(messageBeforeSilentVolume, sink.LastMessage);
    settings.Messages.VolumeMessages = true;
    router.Execute(CommandIds.VolumeDown5);
    Equal("40%", sink.LastMessage);
    settings.Messages.PlaybackMessages = false;
    var messageBeforeSilentPlayback = sink.LastMessage;
    router.Execute(CommandIds.PlayPause);
    Equal(messageBeforeSilentPlayback, sink.LastMessage);
    router.Execute(CommandIds.PlayPause);
    Equal(messageBeforeSilentPlayback, sink.LastMessage);
    settings.Messages.PlaybackMessages = true;
    router.Execute(CommandIds.ActivateSelected);
    Equal("Odtwarzanie: Pierwszy utwór demonstracyjny", sink.LastMessage);
    router.Execute(CommandIds.ActivateSelected);
    Equal("Pauza: Pierwszy utwór demonstracyjny", sink.LastMessage);
    router.Execute(CommandIds.ActivateSelected);
    Equal("Odtwarzanie: Pierwszy utwór demonstracyjny", sink.LastMessage);
    router.Execute(CommandIds.PlayPause);
    Equal("Pauza: Pierwszy utwór demonstracyjny", sink.LastMessage);
    router.Execute(CommandIds.PlayPause);
    Equal("Odtwarzanie: Pierwszy utwór demonstracyjny", sink.LastMessage);
    router.Execute(CommandIds.ToggleFavorite);
    Equal("Usunięto z ulubionych: Pierwszy utwór demonstracyjny", sink.LastMessage);
    router.Execute(CommandIds.ToggleFavorite);
    Equal("Dodano do ulubionych: Pierwszy utwór demonstracyjny", sink.LastMessage);
    router.Execute(CommandIds.AddQueue);
    Equal("Dodano do kolejki: Pierwszy utwór demonstracyjny", sink.LastMessage);
    router.Execute(CommandIds.AddQueue);
    Equal("Usunięto z kolejki: Pierwszy utwór demonstracyjny", sink.LastMessage);
    router.Execute(CommandIds.TogglePlayNext);
    Equal("Odtwarzaj jako następne: Pierwszy utwór demonstracyjny", sink.LastMessage);
    router.Execute(CommandIds.TogglePlayNext);
    Equal("Usunięto z następnych: Pierwszy utwór demonstracyjny", sink.LastMessage);
    router.Execute(CommandIds.CommandPalette);
    True(actions.CommandPaletteShown, "Router powinien otworzyć paletę poleceń przez interfejs aplikacji.");
    router.Execute(CommandIds.RefreshLocalLibrary);
    True(actions.LocalLibraryRefreshed, "Router powinien przekazać ręczne odświeżenie lokalnej biblioteki.");
    router.Execute(CommandIds.SeekToTime);
    True(actions.SeekToTimeShown, "Router powinien otworzyć okno skoku do czasu.");
    router.Execute(CommandIds.SeekToPercentage);
    True(actions.SeekToPercentageShown, "Router powinien otworzyć okno skoku do procentu.");
    router.Execute(CommandIds.ItemProperties);
    True(actions.ItemPropertiesShown, "Router powinien otworzyć jedno okno właściwości i informacji.");
    router.Execute(CommandIds.AddNamedBookmark);
    True(actions.NamedBookmarkAdded, "Router powinien przekazać dodanie nazwanej zakładki do aplikacji.");
    router.Execute(CommandIds.SettingsMessageTemplates);
    Equal(SettingsTarget.MessageTemplates, actions.LastSettingsTarget);
    router.Execute(CommandIds.SettingsPercentageSeekAnnouncement);
    Equal(SettingsTarget.PercentageSeekAnnouncement, actions.LastSettingsTarget);
    router.Execute(CommandIds.SettingsArrowSeekMessages);
    Equal(SettingsTarget.ArrowSeekMessages, actions.LastSettingsTarget);
    router.Execute(CommandIds.SettingsPercentageSeekMessages);
    Equal(SettingsTarget.PercentageSeekMessages, actions.LastSettingsTarget);
    router.Execute(CommandIds.SettingsBookmarkNavigationMessages);
    Equal(SettingsTarget.BookmarkNavigationMessages, actions.LastSettingsTarget);
    router.Execute(CommandIds.SettingsVolumeMessages);
    Equal(SettingsTarget.VolumeMessages, actions.LastSettingsTarget);
    router.Execute(CommandIds.SettingsPlaybackMessages);
    Equal(SettingsTarget.PlaybackMessages, actions.LastSettingsTarget);
    router.Execute(CommandIds.SettingsToggleMessages);
    True(actions.MessagesToggled, "Router powinien przekazać przełączenie komunikatów do aplikacji.");
    router.Execute(CommandIds.SettingsToggleDetailedHints);
    True(actions.DetailedHintsToggled, "Router powinien przekazać przełączenie szczegółowych podpowiedzi do aplikacji.");
    router.Execute(CommandIds.SettingsToggleSeekMessages);
    True(actions.SeekMessagesToggled, "Router powinien przekazać przełączenie odczytu przewijania do aplikacji.");
}

static void TestSeekInputParser()
{
    True(SeekInputParser.TryParseTime("35", out var minutes, out _), "Sama liczba powinna oznaczać minuty.");
    Equal(TimeSpan.FromMinutes(35), minutes);
    True(SeekInputParser.TryParseTime("1:35", out var minuteSeconds, out _), "Format minuty:sekundy powinien działać.");
    Equal(TimeSpan.FromSeconds(95), minuteSeconds);
    True(SeekInputParser.TryParseTime("1:02:30", out var hourTime, out _), "Format godziny:minuty:sekundy powinien działać.");
    Equal(new TimeSpan(1, 2, 30), hourTime);
    True(!SeekInputParser.TryParseTime("1:60", out _, out _), "Sekundy 60 nie mogą być przyjęte.");
    True(!SeekInputParser.TryParseTime("-1", out _, out _), "Czas ujemny nie może być przyjęty.");

    True(SeekInputParser.TryParsePercentage("35", out var percent, out _), "Procent bez znaku powinien działać.");
    Equal(35, percent);
    True(SeekInputParser.TryParsePercentage("100%", out percent, out _), "Procent ze znakiem powinien działać.");
    Equal(100, percent);
    True(!SeekInputParser.TryParsePercentage("101", out _, out _), "Procent ponad 100 nie może być przyjęty.");
}

static void TestExports()
{
    var directory = Path.Combine(Path.GetTempPath(), $"amc-tests-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    try
    {
        var store = new ConfigurationStore(Path.Combine(directory, "state.json"));
        var state = ConfigurationStore.CreateDefaultState();
        state.Settings.Messages.DetailedHints = true;
        state.Settings.Messages.SeekMessages = false;
        state.Settings.Messages.ArrowSeekMessages = false;
        state.Settings.Messages.PercentageSeekMessages = true;
        state.Settings.Messages.BookmarkNavigationMessages = false;
        state.Settings.Messages.VolumeMessages = false;
        state.Settings.Messages.PlaybackMessages = false;
        state.Settings.Messages.PercentageSeekAnnouncement = PercentageSeekAnnouncementMode.PercentAndTime;
        state.Bookmarks.Entries.Add(new BookmarkEntry
        {
            Id = "bookmark-1",
            SessionId = "tidal",
            SessionName = "TIDAL",
            ItemId = "tidal-1",
            ItemTitle = "Pierwszy utwór demonstracyjny",
            PositionTicks = TimeSpan.FromMinutes(2).Ticks,
            CreatedUtcTicks = DateTime.UtcNow.Ticks
        });
        var mapPath = Path.Combine(directory, "map.amckeys.json");
        var settingsPath = Path.Combine(directory, "settings.amcsettings.json");
        var backupPath = Path.Combine(directory, "all.amcbackup.json");

        store.Save(state);
        Equal(false, store.LoadOrCreate().Settings.Messages.SeekMessages);
        Equal(false, store.LoadOrCreate().Settings.Messages.ArrowSeekMessages);
        Equal(true, store.LoadOrCreate().Settings.Messages.PercentageSeekMessages);
        Equal(false, store.LoadOrCreate().Settings.Messages.BookmarkNavigationMessages);
        Equal(false, store.LoadOrCreate().Settings.Messages.VolumeMessages);
        Equal(false, store.LoadOrCreate().Settings.Messages.PlaybackMessages);
        Equal(PercentageSeekAnnouncementMode.PercentAndTime, store.LoadOrCreate().Settings.Messages.PercentageSeekAnnouncement);

        store.ExportKeyboardMap(mapPath, state.KeyboardProfiles[0]);
        var importedProfile = store.ImportKeyboardMap(mapPath);
        True(!importedProfile.IsBuiltIn, "Importowana mapa musi być edytowalna.");

        store.ExportConfiguration(settingsPath, state.Settings);
        var importedSettings = store.ImportConfiguration(settingsPath, "default");
        Equal("default", importedSettings.ActiveKeyboardProfileId);
        Equal(MediaItemField.Title, importedSettings.Lists.FieldOrder[0]);
        Equal(true, importedSettings.Messages.DetailedHints);
        Equal(false, importedSettings.Messages.SeekMessages);
        Equal(false, importedSettings.Messages.ArrowSeekMessages);
        Equal(true, importedSettings.Messages.PercentageSeekMessages);
        Equal(false, importedSettings.Messages.BookmarkNavigationMessages);
        Equal(false, importedSettings.Messages.VolumeMessages);
        Equal(false, importedSettings.Messages.PlaybackMessages);
        Equal(PercentageSeekAnnouncementMode.PercentAndTime, importedSettings.Messages.PercentageSeekAnnouncement);

        store.ExportFullBackup(backupPath, state);
        var importedBackup = store.ImportFullBackup(backupPath);
        Equal(4, importedBackup.Settings.SessionSlots.Count);
        Equal(1, importedBackup.KeyboardProfiles.Count);
        Equal(false, importedBackup.Settings.Messages.SeekMessages);
        Equal(false, importedBackup.Settings.Messages.ArrowSeekMessages);
        Equal(true, importedBackup.Settings.Messages.PercentageSeekMessages);
        Equal(false, importedBackup.Settings.Messages.BookmarkNavigationMessages);
        Equal(false, importedBackup.Settings.Messages.VolumeMessages);
        Equal(false, importedBackup.Settings.Messages.PlaybackMessages);
        Equal(PercentageSeekAnnouncementMode.PercentAndTime, importedBackup.Settings.Messages.PercentageSeekAnnouncement);
        Equal(1, importedBackup.Bookmarks.Entries.Count);
        Equal("tidal-1", importedBackup.Bookmarks.Entries[0].ItemId);
    }
    finally
    {
        Directory.Delete(directory, true);
    }
}

static void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"Oczekiwano „{expected}”, otrzymano „{actual}”.");
    }
}

static void True(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

sealed class FakeSink : IAnnouncementSink
{
    public string LastMessage { get; private set; } = string.Empty;
    public void Announce(string message) => LastMessage = message;
}

sealed class FakeActions(MediaItem selectedItem, IReadOnlyList<MediaItem>? actionItems = null) : IApplicationActions
{
    public MediaItem? SelectedItem { get; } = selectedItem;
    public MediaItem? ActionItem => SelectedItem;
    public IReadOnlyList<MediaItem> ActionItems => actionItems ?? (SelectedItem is null ? [] : [SelectedItem]);
    public bool CommandPaletteShown { get; private set; }
    public SettingsTarget? LastSettingsTarget { get; private set; }
    public bool MessagesToggled { get; private set; }
    public bool DetailedHintsToggled { get; private set; }
    public bool SeekMessagesToggled { get; private set; }
    public bool SeekToTimeShown { get; private set; }
    public bool SeekToPercentageShown { get; private set; }
    public bool ItemPropertiesShown { get; private set; }
    public bool BookmarkAdded { get; private set; }
    public bool NamedBookmarkAdded { get; private set; }
    public bool LocalLibraryRefreshed { get; private set; }
    public int BookmarkNavigationDirection { get; private set; }
    public void ShowCurrentSession(string viewName) { }
    public void ShowFilter() { }
    public void ShowSessionList() { }
    public void ShowPlaylistManager() { }
    public void ShowCommandPalette() => CommandPaletteShown = true;
    public void ShowItemProperties() => ItemPropertiesShown = true;
    public void OpenOfficialApplication() { }
    public void ShowHelp() { }
    public void ShowSettings(SettingsTarget target) => LastSettingsTarget = target;
    public void ToggleAccessibilityMessages() => MessagesToggled = true;
    public void ToggleDetailedHints() => DetailedHintsToggled = true;
    public void ToggleSeekMessages() => SeekMessagesToggled = true;
    public void OpenLocalFiles() { }
    public void OpenLocalFolder() { }
    public void RefreshLocalLibrary() => LocalLibraryRefreshed = true;
    public void ShowSeekToTime() => SeekToTimeShown = true;
    public void ShowSeekToPercentage() => SeekToPercentageShown = true;
    public void AddBookmark() => BookmarkAdded = true;
    public void AddNamedBookmark() => NamedBookmarkAdded = true;
    public void NavigateBookmark(int direction) => BookmarkNavigationDirection = direction;
}

sealed class FakeMediaOutput : IMediaOutput
{
    public string? LoadedItemId => LastItem?.Id;
    public TimeSpan Position { get; set; }
    public bool SupportsPlaybackRate => true;
    public int PlayCount { get; private set; }
    public int PauseCount { get; private set; }
    public int StopCount { get; private set; }
    public int Volume { get; private set; }
    public double PlaybackRate { get; private set; } = 1d;
    public MediaItem? LastItem { get; private set; }

    public void Play(MediaItem item, TimeSpan position, int volume, double playbackRate)
    {
        LastItem = item;
        Position = position;
        Volume = volume;
        PlaybackRate = playbackRate;
        PlayCount++;
    }

    public void Pause() => PauseCount++;
    public void Stop() => StopCount++;
    public void Seek(TimeSpan position) => Position = position;
    public void SetVolume(int volume) => Volume = volume;
    public void SetPlaybackRate(double playbackRate) => PlaybackRate = playbackRate;
}
