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
    ("Migracja starszych ustawień", TestLegacyStateMigration),
    ("Migracja ustawień alpha.4", TestVersion2StateMigration),
    ("Migracja komunikatów alpha.5", TestVersion3MessageMigration),
    ("Migracja krótkich komunikatów alpha.7", TestVersion4MessageMigration),
    ("Migracja komunikatów z nazwą elementu alpha.8", TestVersion5MessageMigration),
    ("Migracja nazw w komunikatach Ulubionych alpha.18", TestVersion6FavoriteMessageMigration),
    ("Przełączanie sesji", TestSessions),
    ("Oddzielony tor lokalnego odtwarzania", TestLocalPlaybackBoundary),
    ("Odkrywanie lokalnych plików audio", TestLocalAudioFileDiscovery),
    ("Wyszukiwanie w katalogu", TestCatalogSearch),
    ("Historia wyszukiwania", TestSearchHistory),
    ("Paleta poleceń", TestCommandPalette),
    ("Cofanie zmian przynależności", TestMembershipHistory),
    ("Krótkie komunikaty czasu", TestTimeCommands),
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
    Equal(StartupTarget.MediaList, settings.StartupTarget);

    var profile = KeyboardProfile.CreateDefault();
    Equal(CommandIds.SessionSlot(1), profile.Resolve(KeyChord.Parse("1")));
    True(profile.Resolve(KeyChord.Parse("Ctrl+1")) is null, "Po prefiksie cyfra nie powinna wymagać Control.");
    Equal(CommandIds.ViewFavorites, profile.Resolve(KeyChord.Parse("U")));
    Equal(CommandIds.ToggleFavorite, profile.Resolve(KeyChord.Parse("Shift+U")));
    Equal(CommandIds.ViewAlbums, profile.Resolve(KeyChord.Parse("A")));
    True(profile.Resolve(KeyChord.Parse("Shift+A")) is null, "Shift+A pozostaje nieprzypisane.");
    True(profile.Resolve(KeyChord.Parse("Shift+N")) is null, "Skrót oficjalnej aplikacji pozostaje do ustalenia.");
    Equal(CommandIds.FilterCurrent, profile.Resolve(KeyChord.Parse("K")));
    Equal(CommandIds.CommandPalette, profile.Resolve(KeyChord.Parse("Shift+K")));
    Equal(CommandIds.SearchCurrent, profile.Resolve(KeyChord.Parse("F")));
    Equal(CommandIds.SearchAll, profile.Resolve(KeyChord.Parse("Shift+F")));
    Equal(CommandIds.DownloadInService, profile.Resolve(KeyChord.Parse("D")));
    Equal(CommandIds.DownloadToDisk, profile.Resolve(KeyChord.Parse("Shift+D")));
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
    Equal("Otwórz lokalne pliki audio", CommandCatalog.GetDisplayName(CommandIds.OpenLocalFiles));
    Equal("Otwórz folder z plikami audio", CommandCatalog.GetDisplayName(CommandIds.OpenLocalFolder));
    Equal("Wybierz sesję 7", CommandCatalog.GetDisplayName(CommandIds.SessionSlot(7)));
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
    Equal("WiiM", manager.SelectSlot(3)?.DisplayName);
    Equal("Apple Music", manager.MoveSession(-1).DisplayName);
    Equal("TIDAL", manager.SelectSession("tidal")?.DisplayName);
    Equal("tidal", settings.LastSessionId);
    Equal(17, manager.Current.Items.Count);
    Equal(2, manager.Current.Items.Count(item => item.IsInLibrary));
    True(manager.Current.Items.Count(item => item.Title.StartsWith('B')) >= 2, "Dane demonstracyjne powinny umożliwiać powtarzanie litery B.");
    True(manager.Current.Items.Count(item => item.Title.StartsWith('C')) >= 2, "Dane demonstracyjne powinny umożliwiać powtarzanie litery C.");
    True(manager.Current.Items.Any(item => item.IsInQueue), "Kolejka demonstracyjna nie powinna być pusta.");
    Equal(true, manager.Current.ToggleQueue(manager.Current.CurrentItem));
    Equal(false, manager.Current.ToggleQueue(manager.Current.CurrentItem));
    Equal(true, manager.Current.TogglePlayNext(manager.Current.CurrentItem));
    Equal(false, manager.Current.TogglePlayNext(manager.Current.CurrentItem));
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
    Equal("tidal", settings.LastSessionId);
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
        "Lokalne multimedia",
        [item],
        output,
        4);

    Equal(4, slot);
    Equal(session, manager.SelectSlot(4));
    Equal(TimeSpan.Zero, session.Position);
    True(session.Activate(item), "Lokalny element powinien uruchamiać wyjście dźwięku.");
    Equal(1, output.PlayCount);
    Equal(item, output.LastItem);
    Equal(35, output.Volume);

    session.TogglePlayback();
    Equal(1, output.PauseCount);
    Equal(false, session.IsPlaying);
    session.TogglePlayback();
    Equal(2, output.PlayCount);
    Equal(true, session.IsPlaying);

    output.Position = TimeSpan.FromSeconds(30);
    session.Seek(TimeSpan.FromSeconds(10));
    Equal(TimeSpan.FromSeconds(40), output.Position);
    session.ChangeVolume(5);
    Equal(40, output.Volume);

    session.AddItems([item]);
    Equal(1, session.Items.Count);
    session.MarkPlaybackEnded();
    Equal(false, session.IsPlaying);
    Equal(TimeSpan.Zero, session.Position);
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

    var favorites = entries.Single(entry => entry.CommandId == CommandIds.ViewFavorites);
    Equal("Ctrl+U", favorites.LocalShortcut);
    Equal("U", favorites.PrefixShortcut);
    True(favorites.Label.Contains("Ctrl+U", StringComparison.Ordinal), "Etykieta powinna podawać skrót działający w oknie.");
    True(favorites.Label.Contains("prefiks U", StringComparison.Ordinal), "Etykieta powinna podawać aktywny skrót po prefiksie.");
    Equal(favorites.Label, favorites.ToString());
    True(!favorites.ToString().Contains("CommandId", StringComparison.Ordinal), "Lista nie może ujawniać technicznych nazw pól obiektu.");
    Equal("Ctrl+O", entries.Single(entry => entry.CommandId == CommandIds.OpenLocalFiles).LocalShortcut);
    Equal("Ctrl+Shift+O", entries.Single(entry => entry.CommandId == CommandIds.OpenLocalFolder).LocalShortcut);
    Equal("Left (odtwarzacz)", entries.Single(entry => entry.CommandId == CommandIds.SeekBackward10).LocalShortcut);
    Equal("Shift+Left (odtwarzacz)", entries.Single(entry => entry.CommandId == CommandIds.SeekBackward30).LocalShortcut);
    Equal("Ctrl+Left (odtwarzacz)", entries.Single(entry => entry.CommandId == CommandIds.SeekBackward60).LocalShortcut);
    Equal("Up (odtwarzacz)", entries.Single(entry => entry.CommandId == CommandIds.VolumeUp5).LocalShortcut);
    Equal("Ctrl+Shift+E", entries.Single(entry => entry.CommandId == CommandIds.TimeElapsed).LocalShortcut);
    Equal("F6", entries.Single(entry => entry.CommandId == CommandIds.ViewNowPlaying).LocalShortcut);
    True(
        entries.Single(entry => entry.CommandId == CommandIds.OpenOfficialApp).LocalShortcut is null,
        "Otwieranie w oficjalnej aplikacji nie powinno kolidować ze skrótem folderu.");

    var remaining = CommandPaletteSearch.Filter(entries, "czas pozostaly");
    Equal(1, remaining.Count);
    Equal(CommandIds.TimeRemaining, remaining[0].CommandId);

    var shiftedFavorite = CommandPaletteSearch.Filter(entries, "shift u");
    True(shiftedFavorite.Any(entry => entry.CommandId == CommandIds.ToggleFavorite), "Powinno dać się filtrować także po skrócie.");
    Equal(0, CommandPaletteSearch.Filter(entries, "polecenie-którego-nie-ma").Count);

    Equal("p", CommandPaletteSearch.ContinueOrRestartListQuery(entries, "sesja", "p"));
    Equal(string.Empty, CommandPaletteSearch.ContinueOrRestartListQuery(entries, "sesja", "§"));

    var messages = entries.Single(entry => entry.CommandId == CommandIds.SettingsToggleMessages);
    Equal("Komunikaty dostępności: włączone. Enter: wyłącz", messages.DisplayName);
    var hints = entries.Single(entry => entry.CommandId == CommandIds.SettingsToggleDetailedHints);
    Equal("Szczegółowe podpowiedzi klawiatury: wyłączone. Enter: włącz", hints.DisplayName);
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
    var changedEntries = CommandPaletteSearch.CreateEntries(profile, settings);
    Equal(
        "Komunikaty dostępności: wyłączone. Enter: włącz",
        changedEntries.Single(entry => entry.CommandId == CommandIds.SettingsToggleMessages).DisplayName);
    Equal(
        "Szczegółowe podpowiedzi klawiatury: włączone. Enter: wyłącz",
        changedEntries.Single(entry => entry.CommandId == CommandIds.SettingsToggleDetailedHints).DisplayName);
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
    router.Execute(CommandIds.SeekForward30);
    Equal(TimeSpan.FromSeconds(113), sessions.Current.Position);
    router.Execute(CommandIds.SeekBackward30);
    Equal(TimeSpan.FromSeconds(83), sessions.Current.Position);
    router.Execute(CommandIds.TrackEnd);
    Equal(sessions.Current.CurrentItem.Duration - TimeSpan.FromSeconds(10), sessions.Current.Position);
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
    router.Execute(CommandIds.SettingsMessageTemplates);
    Equal(SettingsTarget.MessageTemplates, actions.LastSettingsTarget);
    router.Execute(CommandIds.SettingsToggleMessages);
    True(actions.MessagesToggled, "Router powinien przekazać przełączenie komunikatów do aplikacji.");
    router.Execute(CommandIds.SettingsToggleDetailedHints);
    True(actions.DetailedHintsToggled, "Router powinien przekazać przełączenie szczegółowych podpowiedzi do aplikacji.");
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
        var mapPath = Path.Combine(directory, "map.amckeys.json");
        var settingsPath = Path.Combine(directory, "settings.amcsettings.json");
        var backupPath = Path.Combine(directory, "all.amcbackup.json");

        store.ExportKeyboardMap(mapPath, state.KeyboardProfiles[0]);
        var importedProfile = store.ImportKeyboardMap(mapPath);
        True(!importedProfile.IsBuiltIn, "Importowana mapa musi być edytowalna.");

        store.ExportConfiguration(settingsPath, state.Settings);
        var importedSettings = store.ImportConfiguration(settingsPath, "default");
        Equal("default", importedSettings.ActiveKeyboardProfileId);
        Equal(MediaItemField.Title, importedSettings.Lists.FieldOrder[0]);
        Equal(true, importedSettings.Messages.DetailedHints);

        store.ExportFullBackup(backupPath, state);
        var importedBackup = store.ImportFullBackup(backupPath);
        Equal(3, importedBackup.Settings.SessionSlots.Count);
        Equal(1, importedBackup.KeyboardProfiles.Count);
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

sealed class FakeActions(MediaItem selectedItem) : IApplicationActions
{
    public MediaItem? SelectedItem { get; } = selectedItem;
    public bool CommandPaletteShown { get; private set; }
    public SettingsTarget? LastSettingsTarget { get; private set; }
    public bool MessagesToggled { get; private set; }
    public bool DetailedHintsToggled { get; private set; }
    public void ShowCurrentSession(string viewName) { }
    public void ShowFilter() { }
    public void ShowSessionList() { }
    public void ShowPlaylistManager() { }
    public void ShowCommandPalette() => CommandPaletteShown = true;
    public void ShowItemInformation(bool extended) { }
    public void OpenOfficialApplication() { }
    public void ShowHelp() { }
    public void ShowSettings(SettingsTarget target) => LastSettingsTarget = target;
    public void ToggleAccessibilityMessages() => MessagesToggled = true;
    public void ToggleDetailedHints() => DetailedHintsToggled = true;
    public void OpenLocalFiles() { }
    public void OpenLocalFolder() { }
}

sealed class FakeMediaOutput : IMediaOutput
{
    public TimeSpan Position { get; set; }
    public int PlayCount { get; private set; }
    public int PauseCount { get; private set; }
    public int Volume { get; private set; }
    public MediaItem? LastItem { get; private set; }

    public void Play(MediaItem item, TimeSpan position, int volume)
    {
        LastItem = item;
        Position = position;
        Volume = volume;
        PlayCount++;
    }

    public void Pause() => PauseCount++;
    public void Seek(TimeSpan position) => Position = position;
    public void SetVolume(int volume) => Volume = volume;
}
