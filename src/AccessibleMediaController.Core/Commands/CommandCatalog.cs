namespace AccessibleMediaController.Core.Commands;

public static class CommandCatalog
{
    private static readonly string[] DefinedCommandIds = typeof(CommandIds)
        .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
        .Where(field => field.IsLiteral && field.FieldType == typeof(string))
        .Select(field => field.GetRawConstantValue() as string)
        .Where(commandId => !string.IsNullOrWhiteSpace(commandId))
        .Cast<string>()
        .ToArray();

    public static IReadOnlyList<string> GetAllCommandIds()
    {
        return DefinedCommandIds
            .Concat(Enumerable.Range(1, 9).Select(CommandIds.SessionSlot))
            .Concat(Enumerable.Range(0, 10).Select(digit => CommandIds.SeekPercent(digit * 10)))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    public static string GetDisplayName(string commandId)
    {
        if (CommandIds.TryParseSeekPercent(commandId, out var percent))
        {
            return $"Przejdź do {percent}% utworu";
        }

        const string sessionSlotPrefix = "session.slot.";
        if (commandId.StartsWith(sessionSlotPrefix, StringComparison.Ordinal)
            && int.TryParse(commandId.AsSpan(sessionSlotPrefix.Length), out var slot))
        {
            return $"Wybierz sesję {slot}";
        }

        return commandId switch
        {
            CommandIds.PlayPause => "Odtwarzaj lub wstrzymaj",
            CommandIds.ActivateSelected => "Odtwórz lub wstrzymaj",
            CommandIds.Previous => "Poprzedni element",
            CommandIds.Next => "Następny element",
            CommandIds.SeekBackward10 => "Cofnij o 10 sekund",
            CommandIds.SeekForward10 => "Przewiń o 10 sekund",
            CommandIds.SeekBackward30 => "Cofnij o 30 sekund",
            CommandIds.SeekForward30 => "Przewiń o 30 sekund",
            CommandIds.SeekBackward60 => "Cofnij o minutę",
            CommandIds.SeekForward60 => "Przewiń o minutę",
            CommandIds.VolumeUp5 => "Głośniej o 5%",
            CommandIds.VolumeDown5 => "Ciszej o 5%",
            CommandIds.VolumeUp1 => "Głośniej o 1%",
            CommandIds.VolumeDown1 => "Ciszej o 1%",
            CommandIds.TrackStart => "Początek utworu",
            CommandIds.TrackEnd => "Koniec utworu",
            CommandIds.TimeElapsed => "Czas od początku",
            CommandIds.TimeRemaining => "Czas pozostały",
            CommandIds.TimeTotal => "Czas całkowity",
            CommandIds.SessionList => "Lista sesji",
            CommandIds.SessionPrevious => "Poprzednia sesja",
            CommandIds.SessionNext => "Następna sesja",
            CommandIds.ViewFavorites => "Pokaż ulubione",
            CommandIds.ToggleFavorite => "Dodaj lub usuń z ulubionych",
            CommandIds.ViewPlaylists => "Pokaż playlisty",
            CommandIds.ManagePlaylists => "Zmień przynależność do playlist",
            CommandIds.SearchCurrent => "Szukaj w bieżącej usłudze",
            CommandIds.SearchAll => "Szukaj we wszystkich usługach",
            CommandIds.FilterCurrent => "Filtruj bieżącą listę",
            CommandIds.CommandPalette => "Paleta poleceń",
            CommandIds.ViewLibrary => "Pokaż bibliotekę",
            CommandIds.ToggleLibrary => "Dodaj lub usuń z biblioteki",
            CommandIds.ViewQueue => "Pokaż kolejkę",
            CommandIds.AddQueue => "Dodaj lub usuń z kolejki",
            CommandIds.TogglePlayNext => "Ustaw lub usuń odtwarzanie jako następne",
            CommandIds.ViewAlbums => "Pokaż albumy",
            CommandIds.ViewRadio => "Pokaż radio i rekomendacje",
            CommandIds.StartRadio => "Uruchom radio na podstawie elementu",
            CommandIds.ViewMixes => "Pokaż miksy",
            CommandIds.ViewHistory => "Pokaż historię",
            CommandIds.ViewNowPlaying => "Pokaż teraz odtwarzane",
            CommandIds.OpenOfficialApp => "Otwórz w oficjalnej aplikacji",
            CommandIds.ItemInformation => "Informacje o elemencie",
            CommandIds.ExtendedInformation => "Rozszerzone informacje o elemencie",
            CommandIds.ViewOutputs => "Pokaż wyjścia i urządzenia",
            CommandIds.ViewDownloads => "Pokaż pobrane",
            CommandIds.DownloadInService => "Pobierz wewnątrz usługi",
            CommandIds.DownloadToDisk => "Pobierz na dysk",
            CommandIds.Help => "Pomoc dotycząca skrótów",
            CommandIds.OpenLocalFiles => "Otwórz lokalne pliki audio",
            CommandIds.OpenLocalFolder => "Otwórz folder z plikami audio",
            CommandIds.SettingsGeneral => "Ustawienia: ogólne",
            CommandIds.SettingsLanguage => "Ustawienia: język interfejsu",
            CommandIds.SettingsStartupTarget => "Ustawienia: widok po uruchomieniu",
            CommandIds.SettingsPrefix => "Ustawienia: globalny prefiks",
            CommandIds.SettingsPrefixTimeout => "Ustawienia: czas oczekiwania po prefiksie",
            CommandIds.SettingsKeyboardProfile => "Ustawienia: profil klawiatury",
            CommandIds.SettingsActivateKeyboardProfile => "Ustawienia: aktywuj profil klawiatury",
            CommandIds.SettingsDuplicateKeyboardProfile => "Ustawienia: utwórz kopię profilu klawiatury",
            CommandIds.SettingsRenameKeyboardProfile => "Ustawienia: zmień nazwę profilu klawiatury",
            CommandIds.SettingsDeleteKeyboardProfile => "Ustawienia: usuń profil klawiatury",
            CommandIds.SettingsImportKeyboardMap => "Ustawienia: importuj mapę klawiatury",
            CommandIds.SettingsExportKeyboardMap => "Ustawienia: eksportuj mapę klawiatury",
            CommandIds.SettingsKeyboardBindings => "Ustawienia: przypisania klawiszy",
            CommandIds.SettingsChangeKeyboardBinding => "Ustawienia: zmień skrót klawiszowy",
            CommandIds.SettingsRemoveKeyboardBinding => "Ustawienia: usuń przypisanie klawisza",
            CommandIds.SettingsListFieldOrder => "Ustawienia: kolejność informacji na listach",
            CommandIds.SettingsImportExport => "Ustawienia: import i eksport",
            CommandIds.SettingsImportConfiguration => "Ustawienia: importuj konfigurację",
            CommandIds.SettingsExportConfiguration => "Ustawienia: eksportuj konfigurację",
            CommandIds.SettingsImportFullBackup => "Ustawienia: importuj pełną kopię",
            CommandIds.SettingsExportFullBackup => "Ustawienia: eksportuj pełną kopię",
            CommandIds.SettingsMessages => "Ustawienia: komunikaty",
            CommandIds.SettingsToggleMessages => "Przełącz komunikaty dostępności",
            CommandIds.SettingsToggleDetailedHints => "Przełącz szczegółowe podpowiedzi klawiatury",
            CommandIds.SettingsToggleSeekMessages => "Przełącz odczyt pozycji po przewijaniu",
            CommandIds.SettingsMessageTemplates => "Ustawienia: szablony komunikatów",
            CommandIds.SettingsUpdates => "Ustawienia: aktualizacje, planowane",
            _ => commandId
        };
    }
}
