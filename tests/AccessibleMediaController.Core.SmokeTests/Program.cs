using System.Text.Json.Nodes;
using AccessibleMediaController.Core.Commands;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Input;
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
    ("Przełączanie sesji", TestSessions),
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
    Equal(StartupTarget.MediaList, settings.StartupTarget);

    var profile = KeyboardProfile.CreateDefault();
    Equal(CommandIds.SessionSlot(1), profile.Resolve(KeyChord.Parse("1")));
    True(profile.Resolve(KeyChord.Parse("Ctrl+1")) is null, "Po prefiksie cyfra nie powinna wymagać Control.");
    Equal(CommandIds.ViewFavorites, profile.Resolve(KeyChord.Parse("U")));
    Equal(CommandIds.ToggleFavorite, profile.Resolve(KeyChord.Parse("Shift+U")));
    Equal(CommandIds.ViewAlbums, profile.Resolve(KeyChord.Parse("A")));
    True(profile.Resolve(KeyChord.Parse("Shift+A")) is null, "Shift+A pozostaje nieprzypisane.");
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
        state.KeyboardProfiles.Add(custom);
        state.Settings.ActiveKeyboardProfileId = custom.Id;
        store.Save(state);

        var loaded = store.LoadOrCreate();
        var refreshedBuiltIn = loaded.KeyboardProfiles.Single(profile => profile.Id == "default");
        var retainedCustom = loaded.KeyboardProfiles.Single(profile => profile.Id == custom.Id);
        Equal(CommandIds.SessionSlot(1), refreshedBuiltIn.Resolve(KeyChord.Parse("1")));
        True(refreshedBuiltIn.Resolve(KeyChord.Parse("Ctrl+1")) is null, "Profil wbudowany powinien otrzymać nową mapę.");
        Equal(CommandIds.SessionSlot(1), retainedCustom.Resolve(KeyChord.Parse("Ctrl+1")));
        Equal(custom.Id, loaded.Settings.ActiveKeyboardProfileId);
    }
    finally
    {
        Directory.Delete(directory, true);
    }
}

static void TestCommandCatalog()
{
    Equal("Dodaj lub usuń z ulubionych", CommandCatalog.GetDisplayName(CommandIds.ToggleFavorite));
    Equal("Dodaj lub usuń z kolejki", CommandCatalog.GetDisplayName(CommandIds.AddQueue));
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
    Equal(2, manager.Current.Items.Count(item => item.IsInLibrary));
    Equal(true, manager.Current.ToggleQueue(manager.Current.CurrentItem));
    Equal(false, manager.Current.ToggleQueue(manager.Current.CurrentItem));
    Equal(true, manager.Current.TogglePlayNext(manager.Current.CurrentItem));
    Equal(false, manager.Current.TogglePlayNext(manager.Current.CurrentItem));
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
    router.Execute(CommandIds.AddQueue);
    Equal("Dodano do kolejki: Pierwszy utwór demonstracyjny", sink.LastMessage);
    router.Execute(CommandIds.AddQueue);
    Equal("Usunięto z kolejki: Pierwszy utwór demonstracyjny", sink.LastMessage);
    router.Execute(CommandIds.TogglePlayNext);
    Equal("Odtwarzaj jako następne: Pierwszy utwór demonstracyjny", sink.LastMessage);
    router.Execute(CommandIds.TogglePlayNext);
    Equal("Usunięto z następnych: Pierwszy utwór demonstracyjny", sink.LastMessage);
}

static void TestExports()
{
    var directory = Path.Combine(Path.GetTempPath(), $"amc-tests-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    try
    {
        var store = new ConfigurationStore(Path.Combine(directory, "state.json"));
        var state = ConfigurationStore.CreateDefaultState();
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
    public void ShowCurrentSession(string viewName) { }
    public void ShowFilter() { }
    public void ShowSessionList() { }
    public void ShowPlaylistManager() { }
    public void ShowItemInformation(bool extended) { }
    public void OpenOfficialApplication() { }
    public void ShowHelp() { }
}
