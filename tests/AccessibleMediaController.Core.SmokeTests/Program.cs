using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
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
    ("Kontekst listy odtwarzania", TestPlaybackContext),
    ("Trwała kolejność Kolejki", TestQueueOrder),
    ("Nawigacja Page Up i Page Down w Kolejce", TestQueuePlaybackNavigation),
    ("Zniknięcie bieżącego pliku zachowuje kontekst odtwarzania", TestMissingCurrentItemRecovery),
    ("Polityka pamiętania pozycji", TestResumePositionPolicy),
    ("Odkrywanie lokalnych plików audio", TestLocalAudioFileDiscovery),
    ("Ponowne włączanie folderu do biblioteki", TestLocalLibraryImport),
    ("Synchronizacja źródeł lokalnej biblioteki", TestLocalLibrarySynchronization),
    ("Bezpieczna zmiana nazwy lokalnego pliku", TestLocalFileRenamePolicy),
    ("Integracyjny cykl zmian folderu", TestLocalFolderSynchronizationCycle),
    ("Bezpieczne zarządzanie źródłami Biblioteki", TestLocalFolderSourcePolicy),
    ("Trwała kolejność własna Biblioteki", TestLocalLibraryManualOrder),
    ("Albumy rozpoznawane ze struktury folderów", TestLocalAlbumInference),
    ("Migracja i trwałość Biblioteki SQLite", TestSqliteLibraryMigration),
    ("Migracja biblioteki alpha.79", TestVersion17LocalLibraryMigration),
    ("Naprawa pustego źródła po alpha.80", TestVersion18EmptySourceMigration),
    ("Wyszukiwanie w katalogu", TestCatalogSearch),
    ("Historia wyszukiwania", TestSearchHistory),
    ("Historia odtwarzania", TestPlaybackHistory),
    ("Trwałe zakładki", TestBookmarks),
    ("Pamięć widoków sesji", TestSessionNavigationPersistence),
    ("Pamięć lokalnej biblioteki", TestLocalMediaPersistence),
    ("Trwałe playlisty", TestPlaylists),
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
    Equal(true, settings.PausePlaybackWhenLeavingPlayer);
    Equal(true, settings.RememberLocalPlaybackPositions);

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
    Equal("Biblioteka lokalna: pokaż kolejność własną", CommandCatalog.GetDisplayName(CommandIds.ViewCustomLocalOrder));
    Equal("Odśwież źródła biblioteki lokalnej", CommandCatalog.GetDisplayName(CommandIds.RefreshLocalLibrary));
    Equal("Zarządzaj źródłami biblioteki lokalnej", CommandCatalog.GetDisplayName(CommandIds.ManageLocalSources));
    Equal("Zmień nazwę w Bibliotece", CommandCatalog.GetDisplayName(CommandIds.RenameLibraryItem));
    Equal("Zmień nazwę pliku na dysku", CommandCatalog.GetDisplayName(CommandIds.RenameLocalFile));
    Equal("Przenieś wyżej na bieżącej liście", CommandCatalog.GetDisplayName(CommandIds.MoveLocalLibraryItemUp));
    Equal("Przenieś niżej na bieżącej liście", CommandCatalog.GetDisplayName(CommandIds.MoveLocalLibraryItemDown));
    Equal("Ustawienia: kolejność sesji i skrótów Ctrl+1–9", CommandCatalog.GetDisplayName(CommandIds.SettingsSessionOrder));
    Equal("Ustawienia: wstrzymuj po wyjściu z odtwarzacza", CommandCatalog.GetDisplayName(CommandIds.SettingsPausePlaybackWhenLeavingPlayer));
    Equal("Ustawienia: domyślnie pamiętaj pozycje lokalnych plików", CommandCatalog.GetDisplayName(CommandIds.SettingsRememberLocalPlaybackPositions));
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
    var keep = new MediaItem
    {
        Id = "keep",
        Title = "Własna nazwa AMC",
        HasCustomTitle = true,
        Source = keepPath,
        IsInLibrary = true
    };
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
    Equal("Własna nazwa AMC", keep.Title);
    Equal(true, keep.HasCustomTitle);

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

static void TestLocalFileRenamePolicy()
{
    var directory = Path.Combine(Path.GetTempPath(), $"amc-rename-tests-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    try
    {
        var current = Path.Combine(directory, "stara nazwa.mp3");
        File.WriteAllBytes(current, [1, 2, 3]);
        True(
            LocalFileRenamePolicy.TryBuildTargetPath(current, "nowa nazwa", out var target, out var error),
            $"Poprawna nazwa powinna zostać przyjęta: {error}");
        Equal(Path.Combine(directory, "nowa nazwa.mp3"), target);
        True(
            LocalFileRenamePolicy.TryBuildTargetPath(current, "nowa nazwa.mp3", out var targetWithExtension, out _),
            "Wpisanie dotychczasowego rozszerzenia nie powinno go dublować.");
        Equal(target, targetWithExtension);
        True(!LocalFileRenamePolicy.TryBuildTargetPath(current, "CON", out _, out _),
            "Nazwa zarezerwowana przez Windows musi zostać odrzucona.");
        True(!LocalFileRenamePolicy.TryBuildTargetPath(current, "folder\\plik", out _, out _),
            "Nazwa nie może zawierać ścieżki.");

        File.WriteAllBytes(target, [4, 5, 6]);
        True(!LocalFileRenamePolicy.TryBuildTargetPath(current, "nowa nazwa", out _, out _),
            "Istniejący plik docelowy musi zostać ochroniony przed nadpisaniem.");
    }
    finally
    {
        Directory.Delete(directory, true);
    }
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
        state.SchemaVersion = 17;
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
        WriteLegacyState(statePath, state);
        var document = JsonNode.Parse(File.ReadAllText(statePath))!.AsObject();
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
        True(File.Exists(Path.Combine(directory, "library.db")), "Migracja powinna utworzyć bazę SQLite.");
        True(
            File.Exists(Path.Combine(directory, "state.pre-sqlite-migration.json")),
            "Migracja powinna zachować źródłowy JSON.");
        var settingsOnly = JsonNode.Parse(File.ReadAllText(statePath))!.AsObject();
        Equal(0, settingsOnly["localMedia"]!["items"]!.AsArray().Count);
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
        state.SchemaVersion = 18;
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
        WriteLegacyState(statePath, state);

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

static void WriteLegacyState(string path, PersistedState state)
{
    var options = new JsonSerializerOptions
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };
    File.WriteAllText(path, JsonSerializer.Serialize(state, options));
}

static void TestSqliteLibraryMigration()
{
    var directory = Path.Combine(Path.GetTempPath(), $"amc-sqlite-tests-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    try
    {
        var statePath = Path.Combine(directory, "state.json");
        var databasePath = Path.Combine(directory, "library.db");
        var state = ConfigurationStore.CreateDefaultState();
        state.SchemaVersion = 24;
        state.LocalMedia.FolderSources.Add(new LocalFolderSourceSettings
        {
            Id = "source",
            DisplayName = "Duża biblioteka",
            Path = Path.Combine(directory, "Muzyka")
        });
        for (var index = 0; index < 1500; index++)
        {
            state.LocalMedia.Items.Add(new LocalMediaItemSettings
            {
                Id = $"item-{index}",
                Title = $"Utwór {index}",
                Path = Path.Combine(directory, "Muzyka", $"Utwór {index}.mp3"),
                IsFavorite = index % 10 == 0,
                IsInLibrary = true,
                IsAvailable = true,
                ResumePositionTicks = index
            });
        }
        state.LocalMedia.CurrentItemId = "item-1499";
        state.LocalMedia.Items[1498].IsInQueue = true;
        state.LocalMedia.Items[1499].IsInQueue = true;
        state.CollectionOrders.QueueItemIdsBySession["local"] = ["item-1499", "item-1498"];
        state.PlaybackHistory.ItemIdsBySession["local"] = ["item-1499", "item-1498"];
        state.Bookmarks.Entries.Add(new BookmarkEntry
        {
            Id = "bookmark",
            SessionId = "local",
            SessionName = "Pliki lokalne",
            ItemId = "item-1499",
            ItemTitle = "Utwór 1499",
            PositionTicks = 1234
        });
        WriteLegacyState(statePath, state);

        var store = new ConfigurationStore(statePath, databasePath);
        var migrated = store.LoadOrCreate();
        Equal(1500, migrated.LocalMedia.Items.Count);
        Equal("item-1499", migrated.LocalMedia.CurrentItemId);
        Equal(2, migrated.PlaybackHistory.ItemIdsBySession["local"].Count);
        Equal(1, migrated.Bookmarks.Entries.Count);
        True(migrated.CollectionOrders.QueueItemIdsBySession["LOCAL"].SequenceEqual(
                ["item-1499", "item-1498"]),
            "Migracja SQLite powinna zachować ręczny porządek Kolejki.");
        True(File.Exists(databasePath), "Brak pliku Biblioteki SQLite.");

        migrated.LocalMedia.Items[1499].Title = "Zmieniony tytuł";
        migrated.LocalMedia.Items[1499].HasCustomTitle = true;
        store.Save(migrated);
        var reloaded = new ConfigurationStore(statePath, databasePath).LoadOrCreate();
        Equal(1500, reloaded.LocalMedia.Items.Count);
        Equal("Zmieniony tytuł", reloaded.LocalMedia.Items.Single(item => item.Id == "item-1499").Title);
        Equal(true, reloaded.LocalMedia.Items.Single(item => item.Id == "item-1499").HasCustomTitle);
        True(reloaded.CollectionOrders.QueueItemIdsBySession["local"].SequenceEqual(
                ["item-1499", "item-1498"]),
            "Ponowny odczyt SQLite powinien zachować ręczny porządek Kolejki.");
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
        Equal(true, loaded.Settings.PausePlaybackWhenLeavingPlayer);
        Equal(true, loaded.Settings.RememberLocalPlaybackPositions);
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

static void TestPlaybackContext()
{
    var output = new FakeMediaOutput();
    var first = new MediaItem { Id = "first", Title = "Pierwszy" };
    var second = new MediaItem { Id = "second", Title = "Drugi" };
    var third = new MediaItem { Id = "third", Title = "Trzeci" };
    var fourth = new MediaItem { Id = "fourth", Title = "Czwarty" };
    var rateOverrides = new Dictionary<string, double?>
    {
        [third.Id] = 1.50d
    };
    var session = new DemoMediaSession(
        "context",
        "Kontekst",
        [first, second, third, fourth],
        output,
        playbackRateOverride: item => rateOverrides.GetValueOrDefault(item.Id));
    session.SetDefaultPlaybackRate(1.25d);
    session.SetPlaybackContext([second.Id, fourth.Id]);

    True(session.Play(second), "Element kontekstu powinien się uruchomić.");
    Equal(1.25d, session.PlaybackRate);
    True(session.PlayRelative(1), "Page Down powinien użyć kolejności bieżącego kontekstu.");
    Equal(fourth, session.CurrentItem);
    True(!session.PlayRelative(1), "Kontekst nie może przejść do elementu spoza listy.");
    True(session.PlayRelative(-1), "Page Up powinien wrócić w tym samym kontekście.");
    Equal(second, session.CurrentItem);
    Equal(fourth, session.ContinueAfterPlaybackEnded(second));
    True(session.ContinueAfterPlaybackEnded(fourth) is null,
        "Automatyczna kontynuacja powinna zakończyć się wraz z kontekstem.");

    second.IsInQueue = true;
    session.SetPlaybackContext([first.Id, third.Id, fourth.Id]);
    True(session.Play(first), "Pierwszy element nowego kontekstu powinien się uruchomić.");
    Equal(second, session.ContinueAfterPlaybackEnded(first));
    Equal(third, session.ContinueAfterPlaybackEnded(second));
    Equal(1.50d, session.PlaybackRate);
    Equal(fourth, session.ContinueAfterPlaybackEnded(third));
    Equal(1.25d, session.PlaybackRate);
}

static void TestQueueOrder()
{
    var output = new FakeMediaOutput();
    var start = new MediaItem { Id = "start", Title = "Początek" };
    var firstQueued = new MediaItem { Id = "queue-1", Title = "Kolejka pierwsza", IsInQueue = true };
    var secondQueued = new MediaItem { Id = "queue-2", Title = "Kolejka druga", IsInQueue = true };
    var firstNext = new MediaItem { Id = "next-1", Title = "Następny pierwszy", IsPlayNext = true };
    var secondNext = new MediaItem { Id = "next-2", Title = "Następny drugi", IsPlayNext = true };
    var session = new DemoMediaSession(
        "queue-order",
        "Kolejność kolejki",
        [start, firstQueued, secondQueued, firstNext, secondNext],
        output);
    session.SetQueueOrder([secondQueued.Id, "missing", secondNext.Id, firstQueued.Id, firstNext.Id]);
    True(session.QueueItemIds.SequenceEqual(
            [secondQueued.Id, secondNext.Id, firstQueued.Id, firstNext.Id]),
        "Kolejność powinna odrzucić brakujące identyfikatory i zachować zapisane pozycje.");

    True(session.Play(start), "Test kolejki powinien rozpocząć element źródłowy.");
    Equal(secondNext, session.ContinueAfterPlaybackEnded(start));
    Equal(firstNext, session.ContinueAfterPlaybackEnded(secondNext));
    Equal(secondQueued, session.ContinueAfterPlaybackEnded(firstNext));
    Equal(firstQueued, session.ContinueAfterPlaybackEnded(secondQueued));
    True(session.ContinueAfterPlaybackEnded(firstQueued) is null,
        "Po wykorzystaniu uporządkowanej kolejki odtwarzanie nie może powtarzać jej elementów.");
    Equal(0, session.QueueItemIds.Count);
}

static void TestQueuePlaybackNavigation()
{
    var output = new FakeMediaOutput();
    var first = new MediaItem { Id = "queue-a", Title = "Kolejka A", IsPlayNext = true };
    var second = new MediaItem { Id = "queue-b", Title = "Kolejka B", IsInQueue = true };
    var third = new MediaItem { Id = "queue-c", Title = "Kolejka C", IsInQueue = true };
    var explicitQueue = new DemoMediaSession(
        "explicit-queue",
        "Jawna Kolejka",
        [first, second, third],
        output);
    explicitQueue.SetQueueOrder([first.Id, second.Id, third.Id]);
    explicitQueue.SetPlaybackContext([first.Id, second.Id, third.Id], isQueueContext: true);

    True(explicitQueue.Play(first), "Pierwsza pozycja jawnej Kolejki powinna się uruchomić.");
    True(!first.IsPlayNext && !first.IsInQueue,
        "Bieżący element nie może pozostać jednocześnie na liście oczekujących.");
    True(explicitQueue.QueueNavigationActive, "Odtwarzacz powinien pamiętać aktywny kontekst Kolejki.");
    True(explicitQueue.PlayRelative(1), "Page Down powinien przejść do następnej pozycji Kolejki.");
    Equal(second, explicitQueue.CurrentItem);
    True(explicitQueue.PlayRelative(-1), "Page Up powinien wrócić do poprzedniej pozycji tej samej Kolejki.");
    Equal(first, explicitQueue.CurrentItem);
    True(explicitQueue.PlayRelative(1), "Ponowny Page Down powinien wrócić do drugiej pozycji.");
    Equal(second, explicitQueue.CurrentItem);
    Equal(third, explicitQueue.ContinueAfterPlaybackEnded(second));
    True(explicitQueue.ContinueAfterPlaybackEnded(third) is null,
        "Jawna Kolejka nie może po wyczerpaniu odtworzyć zużytej pozycji ponownie.");
    True(explicitQueue.PlayRelative(-1),
        "Po dojściu do końca Page Up powinien nadal pozwolić wrócić w historii Kolejki.");
    Equal(second, explicitQueue.CurrentItem);

    var source = new MediaItem { Id = "source", Title = "Źródło" };
    var natural = new MediaItem { Id = "natural", Title = "Dalszy element Biblioteki" };
    var next = new MediaItem { Id = "next", Title = "Następny", IsPlayNext = true };
    var queued = new MediaItem { Id = "queued", Title = "Zwykła Kolejka", IsInQueue = true };
    var diversion = new DemoMediaSession(
        "queue-diversion",
        "Wejście automatyczne",
        [source, natural, next, queued],
        output);
    diversion.SetPlaybackContext([source.Id, next.Id, queued.Id, natural.Id]);
    diversion.SetQueueOrder([queued.Id, next.Id]);
    True(diversion.Play(source), "Źródłowy plik powinien się uruchomić.");
    Equal(next, diversion.ContinueAfterPlaybackEnded(source));
    True(diversion.PlayRelative(1),
        "Page Down po automatycznym wejściu do Kolejki powinien wybrać jej kolejną pozycję.");
    Equal(queued, diversion.CurrentItem);
    True(diversion.PlayRelative(-1),
        "Page Up po automatycznym wejściu powinien wrócić w Kolejce, a nie w Bibliotece.");
    Equal(next, diversion.CurrentItem);
    Equal(natural, diversion.ContinueAfterPlaybackEnded(next));

    var adopted = new MediaItem { Id = "adopted", Title = "Już odtwarzany", IsPlayNext = true };
    var adoptedLater = new MediaItem { Id = "adopted-later", Title = "Później", IsInQueue = true };
    var adoption = new DemoMediaSession(
        "queue-adoption",
        "Przejęcie bieżącego",
        [adopted, adoptedLater],
        output);
    True(adoption.Play(adopted), "Element powinien najpierw grać poza kontekstem Kolejki.");
    True(adopted.IsPlayNext, "Samo odtworzenie poza widokiem Kolejki nie powinno zmienić przynależności.");
    adoption.SetPlaybackContext([adopted.Id, adoptedLater.Id], isQueueContext: true);
    True(!adopted.IsPlayNext && adoption.QueueNavigationActive,
        "Otwarcie już odtwarzanego elementu z Kolejki powinno przejąć go bez ponownego uruchamiania.");
}

static void TestMissingCurrentItemRecovery()
{
    var output = new FakeMediaOutput();
    var unrelated = new MediaItem { Id = "unrelated", Title = "Pierwszy w całej Bibliotece" };
    var first = new MediaItem { Id = "first", Title = "Bieżący" };
    var second = new MediaItem { Id = "second", Title = "Następny z folderu" };
    var folderSession = new DemoMediaSession(
        "folder-recovery",
        "Folder",
        [unrelated, first, second],
        output);
    folderSession.SetPlaybackContext([first.Id, second.Id]);
    True(folderSession.Play(first), "Bieżący plik folderu powinien się uruchomić.");

    var folderResult = folderSession.ReplaceItems([unrelated, second]);
    True(folderResult.CurrentItemRemoved, "Odświeżenie powinno rozpoznać zniknięcie bieżącego pliku.");
    Equal(second, folderResult.SelectedSuccessor);
    Equal(second, folderSession.CurrentItem);
    Equal(false, folderSession.IsPlaying);
    folderSession.TogglePlayback();
    Equal(second, output.LastItem);

    var queuedFirst = new MediaItem { Id = "queue-first", Title = "Pierwszy z Kolejki", IsInQueue = true };
    var queuedSecond = new MediaItem { Id = "queue-second", Title = "Drugi z Kolejki", IsInQueue = true };
    var queueSession = new DemoMediaSession(
        "queue-recovery",
        "Kolejka",
        [unrelated, queuedFirst, queuedSecond],
        output);
    queueSession.SetQueueOrder([queuedFirst.Id, queuedSecond.Id]);
    queueSession.SetPlaybackContext([queuedFirst.Id, queuedSecond.Id], isQueueContext: true);
    True(queueSession.Play(queuedFirst), "Pierwszy element Kolejki powinien się uruchomić.");

    var queueResult = queueSession.ReplaceItems([unrelated, queuedSecond]);
    Equal(queuedSecond, queueResult.SelectedSuccessor);
    Equal(queuedSecond, queueSession.CurrentItem);
    Equal(false, queueSession.IsPlaying);
    Equal(true, queuedSecond.IsInQueue);
    queueSession.TogglePlayback();
    Equal(queuedSecond, output.LastItem);
    Equal(false, queuedSecond.IsInQueue);

    var onlyQueued = new MediaItem { Id = "queue-only", Title = "Jedyny z Kolejki", IsInQueue = true };
    var exhaustedQueue = new DemoMediaSession(
        "queue-exhausted",
        "Pusta Kolejka",
        [unrelated, onlyQueued],
        output);
    exhaustedQueue.SetPlaybackContext([onlyQueued.Id], isQueueContext: true);
    True(exhaustedQueue.Play(onlyQueued), "Jedyny element Kolejki powinien się uruchomić.");

    var exhaustedResult = exhaustedQueue.ReplaceItems([unrelated]);
    True(exhaustedResult.CurrentItemRemoved, "Usunięcie jedynego elementu powinno zostać rozpoznane.");
    Equal<MediaItem?>(null, exhaustedResult.SelectedSuccessor);
    Equal(false, exhaustedQueue.HasCurrentItem);
    exhaustedQueue.TogglePlayback();
    Equal(false, exhaustedQueue.IsPlaying);

    var source = new MediaItem { Id = "source", Title = "Źródło" };
    var queued = new MediaItem { Id = "diverted", Title = "Pozycja z Kolejki", IsPlayNext = true };
    var natural = new MediaItem { Id = "natural", Title = "Dalszy plik folderu" };
    var divertedSession = new DemoMediaSession(
        "diversion-recovery",
        "Powrót z Kolejki",
        [source, queued, natural],
        output);
    divertedSession.SetPlaybackContext([source.Id, natural.Id]);
    True(divertedSession.Play(source), "Plik źródłowy powinien się uruchomić.");
    Equal(queued, divertedSession.ContinueAfterPlaybackEnded(source));

    var diversionResult = divertedSession.ReplaceItems([source, natural]);
    Equal(natural, diversionResult.SelectedSuccessor);
    Equal(natural, divertedSession.CurrentItem);
    Equal(false, divertedSession.QueueNavigationActive);
}

static void TestResumePositionPolicy()
{
    var output = new FakeMediaOutput();
    var music = new MediaItem
    {
        Id = "music",
        Title = "Muzyka od początku",
        Source = @"C:\Muzyka\utwor.mp3"
    };
    var podcast = new MediaItem
    {
        Id = "podcast",
        Title = "Podcast ze wznowieniem",
        Source = @"C:\Podcasty\odcinek.mp3"
    };
    var session = new DemoMediaSession(
        "local",
        "Pliki lokalne",
        [music, podcast],
        output,
        item => string.Equals(item.Id, podcast.Id, StringComparison.Ordinal));

    True(session.Play(music), "Plik muzyczny powinien się uruchomić.");
    output.Position = TimeSpan.FromSeconds(25);
    session.TogglePlayback();
    Equal(false, session.IsPlaying);
    session.TogglePlayback();
    Equal(TimeSpan.FromSeconds(25), output.Position);
    True(!session.RememberedPositions.ContainsKey(music.Id),
        "Pauza ma zachować bieżące miejsce tylko w sesji, bez trwałego zapisu muzyki.");

    True(session.Play(podcast), "Podcast powinien dać się wybrać.");
    session.SetPosition(TimeSpan.FromMinutes(12));
    True(session.Play(music), "Powrót do muzyki powinien być możliwy.");
    Equal(TimeSpan.Zero, session.Position);
    True(session.Play(podcast), "Powrót do podcastu powinien być możliwy.");
    Equal(TimeSpan.FromMinutes(12), session.Position);
    Equal(TimeSpan.FromMinutes(12), session.RememberedPositions[podcast.Id]);
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
        var lockedPath = Path.Combine(directory, "Zablokowany.mp3");
        File.WriteAllBytes(lockedPath, [1, 2, 3]);
        using (File.Open(lockedPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var indexedWithoutOpeningPayload = LocalAudioFileDiscovery.FindFiles(directory);
            True(
                indexedWithoutOpeningPayload.Contains(lockedPath, StringComparer.OrdinalIgnoreCase),
                "Indeksowanie folderu nie może wymagać otwarcia danych pliku.");
        }
        Equal(CloudFileState.Local, CloudFileAvailability.GetState(lockedPath));
        var placeholderPath = Path.Combine(directory, "Tylko online.mp3");
        File.WriteAllBytes(placeholderPath, [1]);
        try
        {
            File.SetAttributes(
                placeholderPath,
                File.GetAttributes(placeholderPath) | FileAttributes.Offline);
            Equal(CloudFileState.Placeholder, CloudFileAvailability.GetState(placeholderPath));
            True(
                CloudFileAvailability.RequiresHydration(placeholderPath),
                "Plik oznaczony jako Offline powinien zostać rozpoznany bez otwierania zawartości.");
        }
        finally
        {
            File.SetAttributes(placeholderPath, FileAttributes.Normal);
        }
        Equal(
            CloudFileState.Unavailable,
            CloudFileAvailability.GetState(Path.Combine(directory, "brak.mp3")));
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
            },
            PlaybackContextView = "Ulubione",
            PlaybackContextItemIds = ["tidal-1", "tidal-14"]
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
        Equal("Ulubione", tidal.PlaybackContextView);
        True(tidal.PlaybackContextItemIds.SequenceEqual(["tidal-1", "tidal-14"]),
            "Kontekst odtwarzania powinien przetrwać ponowne uruchomienie.");
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
        state.LocalMedia.FolderPlaybackOptions.Add(new LocalFolderPlaybackSettings
        {
            Path = Path.Combine(directory, "Podcasty"),
            ResumePositionMode = ResumePositionMode.Remember,
            PlaybackRateOverride = 1.75d
        });
        state.LocalMedia.CurrentFolderPath = directory;
        state.LocalMedia.LibraryView = "Foldery";
        state.LocalMedia.CustomOrderItemIds.Add("local-1");
        state.LocalMedia.ExcludedPaths.Add(@"C:\Muzyka\pomijany.mp3");
        state.CollectionOrders.FavoriteItemIdsBySession["local"] = ["local-1"];
        state.CollectionOrders.QueueItemIdsBySession["local"] = ["local-1"];
        state.LocalMedia.Items.Add(new LocalMediaItemSettings
        {
            Id = "local-1",
            Title = "Długie nagranie",
            HasCustomTitle = true,
            Path = @"C:\Muzyka\długie.aac",
            DurationTicks = TimeSpan.FromMinutes(90).Ticks,
            ResumePositionTicks = TimeSpan.FromMinutes(17).Ticks,
            FileLength = 123456,
            LastWriteUtcTicks = 987654,
            IsFavorite = true,
            IsInLibrary = true,
            IsInQueue = true,
            ResumePositionMode = ResumePositionMode.Remember,
            PlaybackRateOverride = 1.75d,
            OutputDeviceId = "default"
        });

        store.Save(state);
        var loaded = store.LoadOrCreate();
        Equal(ConfigurationStore.CurrentSchemaVersion, loaded.SchemaVersion);
        Equal("local-1", loaded.LocalMedia.CurrentItemId);
        Equal(47, loaded.LocalMedia.Volume);
        Equal(1.50d, loaded.LocalMedia.PlaybackRate);
        Equal(1, loaded.LocalMedia.Items.Count);
        Equal(1, loaded.LocalMedia.FolderSources.Count);
        Equal(1, loaded.LocalMedia.FolderPlaybackOptions.Count);
        Equal("Foldery", loaded.LocalMedia.LibraryView);
        Equal("local-1", loaded.LocalMedia.CustomOrderItemIds.Single());
        Equal(Path.GetFullPath(@"C:\Muzyka\pomijany.mp3"), loaded.LocalMedia.ExcludedPaths[0]);
        Equal("Nagrania", loaded.LocalMedia.FolderSources[0].DisplayName);
        var folderOptions = loaded.LocalMedia.FolderPlaybackOptions[0];
        Equal(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.Combine(directory, "Podcasty"))),
            folderOptions.Path);
        Equal(ResumePositionMode.Remember, folderOptions.ResumePositionMode);
        Equal(1.75d, folderOptions.PlaybackRateOverride);
        Equal(Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory)), loaded.LocalMedia.CurrentFolderPath);
        Equal("local-1", new PlaybackHistory(loaded.PlaybackHistory).GetItemIds("local")[0]);
        var item = loaded.LocalMedia.Items[0];
        Equal("Długie nagranie", item.Title);
        Equal(true, item.HasCustomTitle);
        Equal(TimeSpan.FromMinutes(17).Ticks, item.ResumePositionTicks);
        Equal(true, item.IsFavorite);
        Equal(true, item.IsInQueue);
        Equal(true, item.IsAvailable);
        Equal(ResumePositionMode.Remember, item.ResumePositionMode);
        Equal(1.75d, item.PlaybackRateOverride);
        Equal("default", item.OutputDeviceId);
        Equal("local-1", loaded.CollectionOrders.FavoriteItemIdsBySession["LOCAL"].Single());
        Equal("local-1", loaded.CollectionOrders.QueueItemIdsBySession["LOCAL"].Single());

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

static void TestPlaylists()
{
    var directory = Path.Combine(Path.GetTempPath(), $"amc-playlist-tests-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    try
    {
        var state = ConfigurationStore.CreateDefaultState();
        state.LocalMedia.Items.AddRange(
        [
            new LocalMediaItemSettings
            {
                Id = "track-a",
                Title = "Alfa",
                Path = Path.Combine(directory, "a.mp3"),
                IsInLibrary = true,
                IsAvailable = true
            },
            new LocalMediaItemSettings
            {
                Id = "track-b",
                Title = "Bravo",
                Path = Path.Combine(directory, "b.mp3"),
                IsInLibrary = true,
                IsAvailable = true
            },
            new LocalMediaItemSettings
            {
                Id = "track-c",
                Title = "Charlie",
                Path = Path.Combine(directory, "c.mp3"),
                IsInLibrary = true,
                IsAvailable = true
            }
        ]);
        var playlists = new PlaylistIndex(state.Playlists);
        var first = playlists.Create("local", "Do odsłuchu");
        playlists.SetMembership(first.Id, ["track-a", "track-b"], true);
        Equal(PlaylistMembershipState.Some, playlists.GetMembership(first.Id, ["track-b", "track-c"]));
        playlists.SetMembership(first.Id, ["track-b", "track-c"], true);
        True(first.ItemIds.SequenceEqual(["track-a", "track-b", "track-c"]),
            "Dodanie zbiorowe powinno zachować dotychczasową kolejność i dopisać tylko brakujący element.");
        Equal(
            ManualOrderMoveResult.Moved,
            playlists.MoveItems(first.Id, ["track-a", "track-b", "track-c"], ["track-c"], -1));
        True(first.ItemIds.SequenceEqual(["track-a", "track-c", "track-b"]),
            "Playlista powinna pozwalać przenieść element bez zmiany katalogu.");
        playlists.SetMembership(first.Id, ["track-c"], false);
        True(first.ItemIds.SequenceEqual(["track-a", "track-b"]),
            "Usunięcie z playlisty nie powinno naruszyć pozostałych pozycji.");
        playlists.Rename(first.Id, "Audycje");
        var second = playlists.Create("local", "Muzyka");
        playlists.SetMembership(second.Id, ["track-c"], true);
        var duplicateRejected = false;
        try
        {
            playlists.Create("LOCAL", "muzyka");
        }
        catch (InvalidOperationException)
        {
            duplicateRejected = true;
        }
        True(duplicateRejected, "Nazwy playlist powinny być unikatowe w obrębie sesji.");

        var statePath = Path.Combine(directory, "state.json");
        var databasePath = Path.Combine(directory, "library.db");
        var store = new ConfigurationStore(statePath, databasePath);
        store.Save(state);
        var loaded = new ConfigurationStore(statePath, databasePath).LoadOrCreate();
        var loadedPlaylists = new PlaylistIndex(loaded.Playlists).GetForSession("LOCAL");
        Equal(2, loadedPlaylists.Count);
        Equal("Audycje", loadedPlaylists[0].Name);
        True(loadedPlaylists[0].ItemIds.SequenceEqual(["track-a", "track-b"]),
            "SQLite powinien zachować kolejność elementów pierwszej playlisty.");
        Equal("track-c", loadedPlaylists[1].ItemIds.Single());

        var clone = new PlaylistIndex(loaded.Playlists).CloneSettings();
        new PlaylistIndex(loaded.Playlists).Remove(loadedPlaylists[0].Id);
        new PlaylistIndex(loaded.Playlists).ReplaceSession("local", clone.Entries);
        Equal(2, new PlaylistIndex(loaded.Playlists).GetForSession("local").Count);
    }
    finally
    {
        Directory.Delete(directory, true);
    }
}

static void TestLocalLibraryManualOrder()
{
    var alpha = new MediaItem { Id = "a", Title = "Alfa", Source = @"C:\Muzyka\a.mp3" };
    var bravo = new MediaItem { Id = "b", Title = "Brawo", Source = @"C:\Muzyka\b.mp3" };
    var charlie = new MediaItem { Id = "c", Title = "Charlie", Source = @"C:\Muzyka\c.mp3" };
    var delta = new MediaItem { Id = "d", Title = "Delta", Source = @"C:\Muzyka\d.mp3" };

    var initialized = LocalLibraryManualOrder.Normalize(
        [],
        [charlie, alpha, bravo],
        initializeAlphabetically: true);
    True(initialized.SequenceEqual(["a", "b", "c"]), "Pierwsze otwarcie powinno utworzyć porządek alfabetyczny.");

    var normalized = LocalLibraryManualOrder.Normalize(
        ["c", "brak", "c"],
        [alpha, bravo, charlie, delta]);
    True(normalized.SequenceEqual(["c", "a", "b", "d"]), "Należy zachować znaną kolejność, usunąć duplikaty i dopisać nowe elementy.");
    True(
        LocalLibraryManualOrder.Order([alpha, bravo, charlie, delta], normalized)
            .Select(item => item.Id)
            .SequenceEqual(["c", "a", "b", "d"]),
        "Widok powinien respektować zapisaną kolejność.");

    var singleMove = new List<string> { "a", "b", "c", "d" };
    Equal(
        ManualOrderMoveResult.Moved,
        LocalLibraryManualOrder.MoveVisibleBlock(singleMove, ["a", "b", "c", "d"], ["b"], 1));
    True(singleMove.SequenceEqual(["a", "c", "b", "d"]), "Pojedynczy element powinien przesunąć się o jeden wiersz.");

    var blockMove = new List<string> { "a", "b", "c", "d" };
    Equal(
        ManualOrderMoveResult.Moved,
        LocalLibraryManualOrder.MoveVisibleBlock(blockMove, ["a", "b", "c", "d"], ["b", "c"], -1));
    True(blockMove.SequenceEqual(["b", "c", "a", "d"]), "Ciągłe zaznaczenie powinno przenieść się jako jeden blok.");
    Equal(
        ManualOrderMoveResult.Boundary,
        LocalLibraryManualOrder.MoveVisibleBlock(blockMove, ["b", "c", "a", "d"], ["b", "c"], -1));
    Equal(
        ManualOrderMoveResult.NonContiguousSelection,
        LocalLibraryManualOrder.MoveVisibleBlock(blockMove, ["b", "c", "a", "d"], ["b", "a"], 1));

    var orderWithHiddenItems = new List<string> { "a", "ukryty-1", "b", "ukryty-2", "c" };
    Equal(
        ManualOrderMoveResult.Moved,
        LocalLibraryManualOrder.MoveVisibleBlock(orderWithHiddenItems, ["a", "b", "c"], ["b"], 1));
    True(
        orderWithHiddenItems.SequenceEqual(["a", "ukryty-1", "c", "ukryty-2", "b"]),
        "Przenoszenie nie powinno gubić pozycji chwilowo niewidocznych plików.");

    var orderBeforeRemoval = new List<string> { "a", "b", "c", "d", "e" };
    var removedPositions = LocalLibraryManualOrder.CapturePositions(
        orderBeforeRemoval,
        ["b", "d"]);
    var orderAfterSaveWithoutRemovedItems = LocalLibraryManualOrder.Normalize(
        orderBeforeRemoval,
        [alpha, charlie, new MediaItem { Id = "e", Title = "Echo" }]);
    True(
        orderAfterSaveWithoutRemovedItems.SequenceEqual(["a", "c", "e"]),
        "Trwałe usunięcie rekordów powinno usunąć ich identyfikatory z bieżącego porządku.");
    LocalLibraryManualOrder.RestorePositions(orderAfterSaveWithoutRemovedItems, removedPositions);
    True(
        orderAfterSaveWithoutRemovedItems.SequenceEqual(["a", "b", "c", "d", "e"]),
        "Ctrl+Z powinno odtworzyć dokładne pozycje kilku usuniętych elementów.");

    var withGenuinelyNewItem = LocalLibraryManualOrder.Normalize(
        orderAfterSaveWithoutRemovedItems,
        [alpha, bravo, charlie, delta, new MediaItem { Id = "e", Title = "Echo" }, new MediaItem { Id = "f", Title = "Foxtrot" }]);
    True(
        withGenuinelyNewItem.SequenceEqual(["a", "b", "c", "d", "e", "f"]),
        "Rzeczywiście nowy plik powinien nadal trafić na koniec kolejności własnej.");
}

static void TestLocalAlbumInference()
{
    var root = Path.GetFullPath(@"C:\Muzyka");
    var albumFolder = Path.Combine(root, "Anna Kowalska", "Pierwszy album");
    var first = new MediaItem
    {
        Id = "track-1",
        Title = "01 Początek",
        Source = Path.Combine(albumFolder, "01 - Początek.mp3"),
        Duration = TimeSpan.FromMinutes(2)
    };
    var second = new MediaItem
    {
        Id = "track-2",
        Title = "02 Środek",
        Source = Path.Combine(albumFolder, "02. Środek.flac"),
        Duration = TimeSpan.FromMinutes(3)
    };
    var tenth = new MediaItem
    {
        Id = "track-10",
        Title = "10 Koniec",
        Source = Path.Combine(albumFolder, "10_Koniec.ogg"),
        Duration = TimeSpan.FromMinutes(4)
    };

    var albums = LocalAlbumInference.Infer([tenth, second, first], [root]);
    Equal(1, albums.Count);
    var album = albums[0];
    Equal("Pierwszy album", album.Title);
    Equal("Anna Kowalska", album.Artist);
    Equal(albumFolder, album.FolderPath);
    Equal(TimeSpan.FromMinutes(9), album.Duration);
    True(
        album.Tracks.Select(track => track.Id).SequenceEqual(["track-1", "track-2", "track-10"]),
        "Ścieżki powinny być uporządkowane według numerów z nazw.");

    True(LocalAlbumInference.TryGetTrackNumber(first.Source, out var firstNumber) && firstNumber == 1,
        "Należy rozpoznać numer z początku nazwy.");
    True(!LocalAlbumInference.TryGetTrackNumber(@"C:\Muzyka\2026-08-24 nagranie.mp3", out _),
        "Rok i data nie mogą udawać numeru ścieżki.");
    True(!LocalAlbumInference.TryGetTrackNumber(@"C:\Muzyka\01Początek.mp3", out _),
        "Cyfry bez separatora nie powinny klasyfikować zwykłej nazwy.");

    var looseFolder = Path.Combine(root, "Nagrania");
    var loose = LocalAlbumInference.Infer(
        [
            new MediaItem { Id = "loose-1", Title = "Poranek", Source = Path.Combine(looseFolder, "Poranek.mp3") },
            new MediaItem { Id = "loose-2", Title = "Wieczór", Source = Path.Combine(looseFolder, "Wieczór.mp3") }
        ],
        [root]);
    Equal(0, loose.Count);

    var single = LocalAlbumInference.Infer([first], [root]);
    Equal(0, single.Count);
    var directAlbum = LocalAlbumInference.Infer([first, second], [albumFolder]);
    Equal(string.Empty, directAlbum.Single().Artist);
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
    Equal("Alt+3 (lista lokalna)", entries.Single(entry => entry.CommandId == CommandIds.ViewCustomLocalOrder).LocalShortcut);
    Equal("Alt+Up (kolejność własna lub Ulubione)", entries.Single(entry => entry.CommandId == CommandIds.MoveLocalLibraryItemUp).LocalShortcut);
    Equal("Alt+Down (kolejność własna lub Ulubione)", entries.Single(entry => entry.CommandId == CommandIds.MoveLocalLibraryItemDown).LocalShortcut);
    Equal("F5 (lista lokalna)", entries.Single(entry => entry.CommandId == CommandIds.RefreshLocalLibrary).LocalShortcut);
    Equal("Ctrl+F5", entries.Single(entry => entry.CommandId == CommandIds.ManageLocalSources).LocalShortcut);
    Equal("F2 (lista lokalna)", entries.Single(entry => entry.CommandId == CommandIds.RenameLibraryItem).LocalShortcut);
    Equal("Shift+F2 (lista lokalna)", entries.Single(entry => entry.CommandId == CommandIds.RenameLocalFile).LocalShortcut);

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

    var firstFavorite = new MediaItem { Id = "favorite-a", Title = "Pierwszy", IsFavorite = true };
    var middleFavorite = new MediaItem { Id = "favorite-b", Title = "Środkowy", IsFavorite = true };
    var lastFavorite = new MediaItem { Id = "favorite-c", Title = "Ostatni", IsFavorite = true };
    var favoriteOrder = new List<string> { firstFavorite.Id, middleFavorite.Id, lastFavorite.Id };
    var middlePosition = LocalLibraryManualOrder.CapturePositions(favoriteOrder, [middleFavorite.Id])
        .Select(entry => new MediaMembershipOrderPosition(entry.ItemId, entry.Index))
        .ToArray();
    var middleBeforeRemoval = MediaMembershipState.From(middleFavorite);
    middleFavorite.IsFavorite = false;
    history.Record(
        "local",
        middleFavorite,
        middleBeforeRemoval,
        "Przywrócono ulubiony",
        orderSnapshot: new MediaMembershipOrderSnapshot("favorites", middlePosition));
    favoriteOrder = LocalLibraryManualOrder.Normalize(
        favoriteOrder,
        [firstFavorite, lastFavorite]);
    True(
        favoriteOrder.SequenceEqual([firstFavorite.Id, lastFavorite.Id]),
        "Usunięty ulubiony znika z bieżącego porządku kolekcji.");
    var favoriteUndo = history.Undo();
    True(favoriteUndo?.OrderSnapshot is not null, "Historia powinna zachować pozycję w kolekcji.");
    LocalLibraryManualOrder.RestorePositions(
        favoriteOrder,
        favoriteUndo!.OrderSnapshot!.Positions.Select(entry =>
            new ManualOrderPosition(entry.ItemId, entry.Index)));
    True(
        favoriteOrder.SequenceEqual([firstFavorite.Id, middleFavorite.Id, lastFavorite.Id]),
        "Ctrl+Z powinno przywrócić ulubiony dokładnie w środkowym miejscu, a nie na końcu.");
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
    router.Execute(CommandIds.ManageLocalSources);
    True(actions.LocalSourceManagerShown, "Router powinien otworzyć menedżer źródeł Biblioteki.");
    router.Execute(CommandIds.RenameLibraryItem);
    True(actions.LibraryItemRenameShown, "Router powinien otworzyć zmianę nazwy w Bibliotece.");
    router.Execute(CommandIds.RenameLocalFile);
    True(actions.LocalFileRenameShown, "Router powinien otworzyć zmianę nazwy pliku na dysku.");
    router.Execute(CommandIds.MoveLocalLibraryItemUp);
    Equal(-1, actions.LocalLibraryMoveDirection);
    router.Execute(CommandIds.MoveLocalLibraryItemDown);
    Equal(1, actions.LocalLibraryMoveDirection);
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
    router.Execute(CommandIds.SettingsPausePlaybackWhenLeavingPlayer);
    Equal(SettingsTarget.PausePlaybackWhenLeavingPlayer, actions.LastSettingsTarget);
    router.Execute(CommandIds.SettingsRememberLocalPlaybackPositions);
    Equal(SettingsTarget.RememberLocalPlaybackPositions, actions.LastSettingsTarget);
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

static void TestLocalFolderSourcePolicy()
{
    var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "AMC-library"));
    var child = Path.Combine(root, "Podcasty");
    var parent = Directory.GetParent(root)?.FullName ?? Path.GetPathRoot(root)!;
    var sources = new List<LocalFolderSourceSettings>
    {
        new()
        {
            Id = "source-1",
            DisplayName = "Biblioteka",
            Path = root,
            ResumePositionMode = ResumePositionMode.StartFromBeginning
        }
    };

    Equal(
        LocalFolderSourceConflictKind.SameSource,
        LocalFolderSourcePolicy.FindConflict(sources, root)!.Kind);
    Equal(
        LocalFolderSourceConflictKind.CoveredByExistingSource,
        LocalFolderSourcePolicy.FindConflict(sources, child)!.Kind);
    Equal(
        LocalFolderSourceConflictKind.ContainsExistingSource,
        LocalFolderSourcePolicy.FindConflict(sources, parent)!.Kind);

    var items = new List<LocalMediaItemSettings>
    {
        new() { Id = "one", Path = Path.Combine(root, "one.mp3"), IsInLibrary = true, IsAvailable = true },
        new() { Id = "two", Path = Path.Combine(root, "two.ogg"), IsInLibrary = true, IsAvailable = false },
        new() { Id = "three", Path = Path.Combine(root, "three.wav"), IsInLibrary = false, IsAvailable = true }
    };
    var statuses = LocalFolderSourcePolicy.BuildStatuses(
        sources,
        items,
        [items[2].Path],
        _ => false);
    Equal(1, statuses.Count);
    Equal(false, statuses[0].IsReachable);
    Equal(1, statuses[0].ActiveItemCount);
    Equal(1, statuses[0].UnavailableItemCount);
    Equal(1, statuses[0].ExcludedItemCount);
    Equal(ResumePositionMode.StartFromBeginning, statuses[0].ResumePositionMode);
    True(statuses[0].Label.Contains("zawsze od początku", StringComparison.Ordinal),
        "Lista źródeł powinna podawać politykę pamiętania pozycji.");
    Equal(statuses[0].Label, statuses[0].ToString());
    True(!statuses[0].ToString().Contains(nameof(LocalFolderSourceStatus), StringComparison.Ordinal),
        "Nazwa dostępnościowa źródła nie może ujawniać technicznego zapisu rekordu.");

    True(LocalFolderSourcePolicy.DetachSource(sources, "source-1"), "Źródło powinno dać się odłączyć.");
    Equal(0, sources.Count);
    Equal(3, items.Count);
    Equal(true, items[0].IsInLibrary);
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
        state.Settings.PausePlaybackWhenLeavingPlayer = false;
        state.Settings.RememberLocalPlaybackPositions = false;
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
        state.LocalMedia.FolderSources.Add(new LocalFolderSourceSettings
        {
            Id = "local-source",
            DisplayName = "Nagrania",
            Path = Path.Combine(directory, "Nagrania"),
            ResumePositionMode = ResumePositionMode.Remember
        });
        state.LocalMedia.Items.Add(new LocalMediaItemSettings
        {
            Id = "local-item",
            Title = "Audycja",
            Path = Path.Combine(directory, "Nagrania", "audycja.mp3"),
            IsInLibrary = true,
            IsAvailable = false,
            ResumePositionTicks = TimeSpan.FromMinutes(12).Ticks
        });
        state.Playlists.Entries.Add(new PlaylistEntry
        {
            Id = "playlist-1",
            SessionId = "local",
            Name = "Do odsłuchu",
            CreatedUtcTicks = DateTime.UtcNow.Ticks,
            ItemIds = ["local-item"]
        });
        state.CollectionOrders.QueueItemIdsBySession["local"] = ["local-item"];
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
        Equal(false, store.LoadOrCreate().Settings.PausePlaybackWhenLeavingPlayer);
        Equal(false, store.LoadOrCreate().Settings.RememberLocalPlaybackPositions);

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
        Equal(false, importedSettings.PausePlaybackWhenLeavingPlayer);
        Equal(false, importedSettings.RememberLocalPlaybackPositions);

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
        Equal(false, importedBackup.Settings.PausePlaybackWhenLeavingPlayer);
        Equal(false, importedBackup.Settings.RememberLocalPlaybackPositions);
        Equal(ResumePositionMode.Remember, importedBackup.LocalMedia.FolderSources[0].ResumePositionMode);
        Equal(PercentageSeekAnnouncementMode.PercentAndTime, importedBackup.Settings.Messages.PercentageSeekAnnouncement);
        Equal(1, importedBackup.Bookmarks.Entries.Count);
        Equal("tidal-1", importedBackup.Bookmarks.Entries[0].ItemId);
        Equal(1, importedBackup.LocalMedia.FolderSources.Count);
        Equal("local-source", importedBackup.LocalMedia.FolderSources[0].Id);
        Equal(1, importedBackup.LocalMedia.Items.Count);
        Equal(TimeSpan.FromMinutes(12).Ticks, importedBackup.LocalMedia.Items[0].ResumePositionTicks);
        Equal(1, importedBackup.Playlists.Entries.Count);
        Equal("Do odsłuchu", importedBackup.Playlists.Entries[0].Name);
        Equal("local-item", importedBackup.Playlists.Entries[0].ItemIds.Single());
        Equal("local-item", importedBackup.CollectionOrders.QueueItemIdsBySession["LOCAL"].Single());
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
    public bool ItemPlaybackOptionsShown { get; private set; }
    public bool BookmarkAdded { get; private set; }
    public bool NamedBookmarkAdded { get; private set; }
    public bool LocalLibraryRefreshed { get; private set; }
    public bool LocalSourceManagerShown { get; private set; }
    public bool LibraryItemRenameShown { get; private set; }
    public bool LocalFileRenameShown { get; private set; }
    public int LocalLibraryMoveDirection { get; private set; }
    public int BookmarkNavigationDirection { get; private set; }
    public void ShowCurrentSession(string viewName) { }
    public void ShowFilter() { }
    public void ShowSessionList() { }
    public void ShowPlaylistManager() { }
    public void ShowCommandPalette() => CommandPaletteShown = true;
    public void ShowItemProperties() => ItemPropertiesShown = true;
    public void ShowItemPlaybackOptions() => ItemPlaybackOptionsShown = true;
    public void OpenOfficialApplication() { }
    public void ShowHelp() { }
    public void ShowSettings(SettingsTarget target) => LastSettingsTarget = target;
    public void ToggleAccessibilityMessages() => MessagesToggled = true;
    public void ToggleDetailedHints() => DetailedHintsToggled = true;
    public void ToggleSeekMessages() => SeekMessagesToggled = true;
    public void OpenLocalFiles() { }
    public void OpenLocalFolder() { }
    public void RefreshLocalLibrary() => LocalLibraryRefreshed = true;
    public void ShowLocalSourceManager() => LocalSourceManagerShown = true;
    public void RenameLibraryItem() => LibraryItemRenameShown = true;
    public void RenameLocalFile() => LocalFileRenameShown = true;
    public void MoveLocalLibrarySelection(int direction) => LocalLibraryMoveDirection = direction;
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
