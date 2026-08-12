using System.Text.Json;
using System.Text.Json.Serialization;
using AccessibleMediaController.Core.Input;
using AccessibleMediaController.Core.Presentation;

namespace AccessibleMediaController.Core.Configuration;

public sealed class ConfigurationStore(string statePath)
{
    public const int CurrentSchemaVersion = 6;
    private const string Version1DefaultPrefix = "Ctrl+Alt+Space";
    private const string Version2DefaultPrefix = "Ctrl+Alt+Windows+Enter";
    private const string CurrentDefaultPrefix = "Ctrl+Alt+Windows+F12";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public PersistedState LoadOrCreate()
    {
        if (!File.Exists(statePath))
        {
            return CreateDefaultState();
        }

        var state = JsonSerializer.Deserialize<PersistedState>(File.ReadAllText(statePath), JsonOptions)
            ?? throw new InvalidDataException("Nie udało się odczytać konfiguracji.");
        MigrateState(state);
        ValidateState(state);
        EnsureBuiltInProfile(state);
        return state;
    }

    public void Save(PersistedState state)
    {
        ValidateState(state);
        var directory = Path.GetDirectoryName(statePath);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        File.WriteAllText(statePath, JsonSerializer.Serialize(state, JsonOptions));
    }

    public void ExportKeyboardMap(string path, KeyboardProfile profile)
    {
        var export = new KeyboardMapExport(CurrentSchemaVersion, "keyboardMap", profile.CreateEditableCopy(profile.Name));
        Write(path, export);
    }

    public KeyboardProfile ImportKeyboardMap(string path)
    {
        var export = Read<KeyboardMapExport>(path, "keyboardMap");
        return export.Profile.CreateEditableCopy($"{export.Profile.Name} — import");
    }

    public void ExportConfiguration(string path, AppSettings settings)
    {
        var copy = Clone(settings);
        copy.ActiveKeyboardProfileId = string.Empty;
        Write(path, new ConfigurationExport(CurrentSchemaVersion, "configuration", copy));
    }

    public AppSettings ImportConfiguration(string path, string activeKeyboardProfileId)
    {
        var export = Read<ConfigurationExport>(path, "configuration");
        export.Settings.ActiveKeyboardProfileId = activeKeyboardProfileId;
        MigrateSettings(export.Settings, export.SchemaVersion);
        ValidateSettings(export.Settings);
        return export.Settings;
    }

    public void ExportFullBackup(string path, PersistedState state)
    {
        ValidateState(state);
        Write(path, new FullBackupExport(CurrentSchemaVersion, "fullBackup", state));
    }

    public PersistedState ImportFullBackup(string path)
    {
        var export = Read<FullBackupExport>(path, "fullBackup");
        MigrateState(export.State);
        ValidateState(export.State);
        EnsureBuiltInProfile(export.State);
        return export.State;
    }

    public PersistedState CloneState(PersistedState state)
    {
        return JsonSerializer.Deserialize<PersistedState>(JsonSerializer.Serialize(state, JsonOptions), JsonOptions)
            ?? throw new InvalidOperationException("Nie udało się skopiować konfiguracji.");
    }

    public static PersistedState CreateDefaultState() => new()
    {
        SchemaVersion = CurrentSchemaVersion,
        Settings = new AppSettings(),
        KeyboardProfiles = [KeyboardProfile.CreateDefault()]
    };

    private static void EnsureBuiltInProfile(PersistedState state)
    {
        if (state.KeyboardProfiles.All(profile => profile.Id != "default"))
        {
            state.KeyboardProfiles.Insert(0, KeyboardProfile.CreateDefault());
        }

        if (state.KeyboardProfiles.All(profile => profile.Id != state.Settings.ActiveKeyboardProfileId))
        {
            state.Settings.ActiveKeyboardProfileId = "default";
        }
    }

    private static void MigrateState(PersistedState state)
    {
        MigrateSettings(state.Settings, state.SchemaVersion);
        state.SchemaVersion = CurrentSchemaVersion;
    }

    private static void MigrateSettings(AppSettings settings, int schemaVersion)
    {
        if (schemaVersion < 2
            && string.Equals(
                KeyChord.Parse(settings.PrefixChord).Canonical,
                Version1DefaultPrefix,
                StringComparison.OrdinalIgnoreCase))
        {
            settings.PrefixChord = Version2DefaultPrefix;
        }

        if (schemaVersion < 3)
        {
            if (string.Equals(
                    KeyChord.Parse(settings.PrefixChord).Canonical,
                    Version2DefaultPrefix,
                    StringComparison.OrdinalIgnoreCase))
            {
                settings.PrefixChord = CurrentDefaultPrefix;
            }

            // Alpha.5 restores announcements once for configurations created by the prototype.
            // They can still be disabled explicitly after the migration.
            settings.Messages.Enabled = true;
        }

        if (schemaVersion < 4)
        {
            foreach (var pair in MessageSettings.CreateDefault().Templates)
            {
                settings.Messages.Templates.TryAdd(pair.Key, pair.Value);
            }
        }

        if (schemaVersion < 5)
        {
            ReplaceTemplateIfDefault(settings, "queue.added", "Dodano do kolejki — demonstracja", "Dodano do kolejki");
            ReplaceTemplateIfDefault(settings, "queue.removed", "Usunięto z kolejki — demonstracja", "Usunięto z kolejki");
            ReplaceTemplateIfDefault(settings, "playNext.added", "Ustawiono do odtworzenia jako następne — demonstracja", "Odtwarzaj jako następne");
            ReplaceTemplateIfDefault(settings, "playNext.removed", "Usunięto z odtwarzanych jako następne — demonstracja", "Usunięto z następnych");
        }

        if (schemaVersion < 6)
        {
            ReplaceTemplateIfDefault(settings, "queue.added", "Dodano do kolejki", "Dodano do kolejki: {item}");
            ReplaceTemplateIfDefault(settings, "queue.removed", "Usunięto z kolejki", "Usunięto z kolejki: {item}");
            ReplaceTemplateIfDefault(settings, "playNext.added", "Odtwarzaj jako następne", "Odtwarzaj jako następne: {item}");
            ReplaceTemplateIfDefault(settings, "playNext.removed", "Usunięto z następnych", "Usunięto z następnych: {item}");
        }
    }

    private static void ReplaceTemplateIfDefault(
        AppSettings settings,
        string eventId,
        string previousDefault,
        string currentDefault)
    {
        if (settings.Messages.Templates.TryGetValue(eventId, out var value)
            && string.Equals(value, previousDefault, StringComparison.Ordinal))
        {
            settings.Messages.Templates[eventId] = currentDefault;
        }
    }

    private static void ValidateState(PersistedState state)
    {
        if (state.SchemaVersion > CurrentSchemaVersion)
        {
            throw new InvalidDataException("Plik został utworzony przez nowszą wersję programu.");
        }

        ValidateSettings(state.Settings);

        if (state.KeyboardProfiles.Count == 0)
        {
            throw new InvalidDataException("Konfiguracja nie zawiera profilu klawiatury.");
        }

        foreach (var profile in state.KeyboardProfiles)
        {
            foreach (var chord in profile.Bindings.Keys) _ = KeyChord.Parse(chord);
        }
    }

    private static void ValidateSettings(AppSettings settings)
    {
        if (settings.PrefixTimeoutMilliseconds is < 250 or > 30000)
        {
            throw new InvalidDataException("Czas prefiksu musi mieścić się między 250 a 30000 ms.");
        }

        var expectedFields = Enum.GetValues<MediaItemField>();
        var fieldOrder = settings.Lists?.FieldOrder;
        if (fieldOrder is null
            || fieldOrder.Count != expectedFields.Length
            || fieldOrder.Distinct().Count() != expectedFields.Length
            || expectedFields.Any(field => !fieldOrder.Contains(field)))
        {
            throw new InvalidDataException("Kolejność informacji na listach jest nieprawidłowa.");
        }
    }

    private static T Read<T>(string path, string expectedKind) where T : IExportEnvelope
    {
        var value = JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonOptions)
            ?? throw new InvalidDataException("Plik eksportu jest pusty lub uszkodzony.");
        if (!string.Equals(value.Kind, expectedKind, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Oczekiwano pliku typu {expectedKind}, otrzymano {value.Kind}.");
        }
        if (value.SchemaVersion > CurrentSchemaVersion)
        {
            throw new InvalidDataException("Plik eksportu pochodzi z nowszej wersji programu.");
        }
        return value;
    }

    private static void Write<T>(string path, T value)
    {
        File.WriteAllText(path, JsonSerializer.Serialize(value, JsonOptions));
    }

    private static AppSettings Clone(AppSettings settings) =>
        JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(settings, JsonOptions), JsonOptions)
        ?? throw new InvalidOperationException("Nie udało się skopiować ustawień.");
}

public interface IExportEnvelope
{
    int SchemaVersion { get; }
    string Kind { get; }
}

public sealed record KeyboardMapExport(int SchemaVersion, string Kind, KeyboardProfile Profile) : IExportEnvelope;
public sealed record ConfigurationExport(int SchemaVersion, string Kind, AppSettings Settings) : IExportEnvelope;
public sealed record FullBackupExport(int SchemaVersion, string Kind, PersistedState State) : IExportEnvelope;
