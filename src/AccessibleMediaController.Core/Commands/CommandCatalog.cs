using AccessibleMediaController.Core.Configuration;

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
            .Concat(Enumerable.Range(1, RadioPresetSlots.Count).Select(CommandIds.RadioPreset))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    public static string GetDisplayName(string commandId)
    {
        if (CommandIds.TryParseSeekPercent(commandId, out var percent))
        {
            return $"Przejdź do {percent}% utworu";
        }

        if (CommandIds.TryParseRadioPreset(commandId, out var radioPresetSlot))
        {
            return $"Uruchom preset {RadioPresetSlots.Label(radioPresetSlot)} aktywnej sesji";
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
            CommandIds.ToggleMuteCurrentSession => "Wycisz lub przywróć dźwięk bieżącej sesji",
            CommandIds.ToggleMuteAllSessions => "Wycisz lub przywróć dźwięk wszystkich sesji AMC",
            CommandIds.ToggleLoudnessNormalization => "Przełącz globalną normalizację głośności",
            CommandIds.ToggleSmoothTrackTransitions => "Przełącz łagodne przejścia między utworami",
            CommandIds.CycleInterTrackSilence => "Wybierz następną długość ciszy między utworami",
            CommandIds.PlaybackRateDown => "Zmniejsz prędkość odtwarzania",
            CommandIds.PlaybackRateUp => "Zwiększ prędkość odtwarzania",
            CommandIds.PlaybackRateReset => "Przywróć normalną prędkość odtwarzania",
            CommandIds.TrackStart => "Początek utworu",
            CommandIds.TrackEnd => "Koniec utworu",
            CommandIds.SeekToTime => "Skocz do czasu",
            CommandIds.SeekToPercentage => "Skocz do procentu",
            CommandIds.MarkClipStart => "Ustaw początek fragmentu",
            CommandIds.MarkClipEnd => "Ustaw koniec fragmentu",
            CommandIds.JumpClipStart => "Przejdź do początku fragmentu",
            CommandIds.JumpClipEnd => "Przejdź do końca fragmentu",
            CommandIds.PreviousClipBoundary => "Przejdź do poprzedniej granicy fragmentu",
            CommandIds.NextClipBoundary => "Przejdź do następnej granicy fragmentu",
            CommandIds.ExportClip => "Zapisz zaznaczony fragment do nowego pliku",
            CommandIds.RemoveClipFromOriginal => "Usuń zaznaczony fragment z oryginalnego pliku",
            CommandIds.ClearClipSelection => "Wyczyść zaznaczenie fragmentu",
            CommandIds.TimeElapsed => "Czas od początku",
            CommandIds.TimeRemaining => "Czas pozostały",
            CommandIds.TimeTotal => "Czas całkowity",
            CommandIds.ItemProperties => "Właściwości i informacje",
            CommandIds.PodcastDescription => "Pokaż pełny opis podcastu lub odcinka",
            CommandIds.GoToPodcast => "Przejdź do podcastu tego odcinka",
            CommandIds.ItemPlaybackOptions => "Opcje odtwarzania elementu",
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
            CommandIds.SortCollectionByAdded => "Uporządkuj według dodania, najnowsze na początku",
            CommandIds.SortCollectionAlphabetically => "Uporządkuj alfabetycznie",
            CommandIds.SortCollectionCustom => "Pokaż kolejność własną",
            CommandIds.ViewFolders => "Biblioteka lokalna: pokaż foldery",
            CommandIds.ViewAllLocalFiles => "Biblioteka lokalna: pokaż wszystkie pliki",
            CommandIds.ViewCustomLocalOrder => "Biblioteka lokalna: pokaż kolejność własną",
            CommandIds.RefreshLocalLibrary => "Odśwież foldery Biblioteki",
            CommandIds.ManageLocalSources => "Foldery Biblioteki",
            CommandIds.RenameLibraryItem => "Zmień nazwę w Bibliotece",
            CommandIds.RenameLocalFile => "Zmień nazwę pliku na dysku",
            CommandIds.MoveLocalLibraryItemUp => "Przenieś wyżej na bieżącej liście",
            CommandIds.MoveLocalLibraryItemDown => "Przenieś niżej na bieżącej liście",
            CommandIds.ToggleLibrary => "Dodaj lub usuń z biblioteki",
            CommandIds.ViewQueue => "Pokaż kolejkę",
            CommandIds.AddQueue => "Dodaj lub usuń z kolejki",
            CommandIds.TogglePlayNext => "Ustaw lub usuń odtwarzanie jako następne",
            CommandIds.ViewAlbums => "Pokaż albumy",
            CommandIds.ViewRadio => "Pokaż radio internetowe",
            CommandIds.StartRadio => "Uruchom radio na podstawie elementu",
            CommandIds.AddRadioStation => "Dodaj stację radiową",
            CommandIds.ImportRadioPlaylist => "Importuj stacje radiowe z playlisty",
            CommandIds.AddPodcast => "Dodaj podcast przez RSS lub Atom",
            CommandIds.ImportPodcastOpml => "Importuj podcasty z OPML",
            CommandIds.RefreshPodcast => "Odśwież bieżący podcast",
            CommandIds.RefreshPodcastLibrary => "Odśwież wszystkie podcasty",
            CommandIds.ViewPodcastInbox => "Pokaż nowe odcinki podcastów",
            CommandIds.ViewPodcastInProgress => "Pokaż rozpoczęte odcinki podcastów",
            CommandIds.ToggleRadioRecording => "Rozpocznij lub zakończ nagrywanie radia",
            CommandIds.ToggleRadioRecordingPause => "Wstrzymaj lub wznów wybrane nagranie radia",
            CommandIds.SplitRadioRecording => "Rozpocznij nową część ręcznego nagrania radia",
            CommandIds.StopAllRadioRecordings => "Zatrzymaj wszystkie trwające nagrania",
            CommandIds.AddRadioSchedule => "Zaplanuj nagranie wybranej stacji",
            CommandIds.ManageRadioSchedules => "Harmonogram nagrywania radia",
            CommandIds.ViewActiveRadioRecordings => "Pokaż aktualnie nagrywane stacje",
            CommandIds.RadioJumpLive => "Radio: wróć na żywo",
            CommandIds.RecognizeRadioTrack => "Rozpoznaj teraz odtwarzany utwór",
            CommandIds.ToggleRadioRecognitionMonitoring => "Włącz lub wyłącz obserwowanie rozpoznawania utworów",
            CommandIds.ToggleRadioRecognitionAnnouncements => "Włącz lub wyłącz oznajmianie rozpoznanych utworów",
            CommandIds.ViewRadioRecognitionHistory => "Pokaż rozpoznane utwory",
            CommandIds.ViewRadioPresets => "Pokaż presety aktywnej sesji",
            CommandIds.AssignRadioPreset => "Utwórz lub przypisz preset aktywnej sesji",
            CommandIds.ViewMixes => "Pokaż miksy",
            CommandIds.ViewHistory => "Pokaż historię odtwarzania",
            CommandIds.ViewBookmarks => "Pokaż zakładki",
            CommandIds.AddBookmark => "Dodaj zakładkę w bieżącym miejscu",
            CommandIds.AddNamedBookmark => "Dodaj nazwaną zakładkę w bieżącym miejscu",
            CommandIds.PreviousBookmark => "Poprzednia zakładka w bieżącym materiale",
            CommandIds.NextBookmark => "Następna zakładka w bieżącym materiale",
            CommandIds.ViewNowPlaying => "Pokaż teraz odtwarzane",
            CommandIds.OpenOfficialApp => "Otwórz stronę elementu",
            CommandIds.SelectAudioOutput => "Wybierz urządzenie audio bieżącej sesji",
            CommandIds.ViewOutputs => "Pokaż wyjścia i urządzenia",
            CommandIds.ViewDownloads => "Pokaż pobrane",
            CommandIds.DownloadInService => "Pobierz wewnątrz usługi",
            CommandIds.DownloadToDisk => "Pobierz na dysk",
            CommandIds.Help => "Skróty klawiszowe",
            CommandIds.KeyboardHelp => "Włącz lub wyłącz pomoc klawiatury",
            CommandIds.OpenLocalFiles => "Otwórz lokalne pliki multimedialne",
            CommandIds.OpenLocalFolder => "Otwórz folder z plikami multimedialnymi",
            CommandIds.SettingsGeneral => "Ustawienia: ogólne",
            CommandIds.SettingsLanguage => "Ustawienia: język interfejsu",
            CommandIds.SettingsStartupTarget => "Ustawienia: widok po uruchomieniu",
            CommandIds.SettingsSessionOrder => "Ustawienia: kolejność sesji i skrótów Ctrl+1–9",
            CommandIds.SettingsPausePlaybackWhenLeavingPlayer => "Ustawienia: wstrzymuj po wyjściu z odtwarzacza",
            CommandIds.SettingsFollowPlaybackOnPlayerExit => "Ustawienia: fokus podąża za odtwarzaniem",
            CommandIds.SettingsOpenPlayerWhenActivatingPreset => "Ustawienia: otwieraj odtwarzacz po uruchomieniu presetu",
            CommandIds.SettingsRememberLocalPlaybackPositions => "Ustawienia: pamiętaj pozycję odtwarzania lokalnych plików",
            CommandIds.SettingsLoudnessNormalization => "Ustawienia: globalna normalizacja głośności",
            CommandIds.SettingsSmoothTrackTransitions => "Ustawienia: łagodne przejścia między utworami",
            CommandIds.SettingsInterTrackSilence => "Ustawienia: cisza między utworami",
            CommandIds.SettingsPrefix => "Ustawienia: globalny prefiks",
            CommandIds.SettingsPrefixTimeout => "Ustawienia: czas oczekiwania po prefiksie",
            CommandIds.SettingsRadioRecognitionScope => "Ustawienia: zakres automatycznego rozpoznawania radia",
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
            CommandIds.SettingsHistoryMessages => "Ustawienia: komunikaty historii widoków",
            CommandIds.SettingsToggleMessages => "Przełącz komunikaty dostępności",
            CommandIds.SettingsToggleDetailedHints => "Przełącz szczegółowe podpowiedzi klawiatury",
            CommandIds.SettingsToggleSeekMessages => "Przełącz automatyczne komunikaty odtwarzacza",
            CommandIds.SettingsArrowSeekMessages => "Ustawienia: komunikaty przewijania strzałkami",
            CommandIds.SettingsPercentageSeekMessages => "Ustawienia: komunikaty skoków cyframi",
            CommandIds.SettingsBookmarkNavigationMessages => "Ustawienia: komunikaty nawigacji po zakładkach",
            CommandIds.SettingsVolumeMessages => "Ustawienia: komunikaty zmian głośności",
            CommandIds.SettingsPlaybackMessages => "Ustawienia: komunikaty odtwarzania i pauzy",
            CommandIds.SettingsAutomaticRecognitionMessages => "Ustawienia: oznajmianie automatycznie rozpoznanych utworów",
            CommandIds.SettingsPercentageSeekAnnouncement => "Ustawienia: komunikat po skoku cyfrą",
            CommandIds.SettingsMessageTemplates => "Ustawienia: szablony komunikatów",
            CommandIds.SettingsUpdates => "Ustawienia: aktualizacje, planowane",
            _ => commandId
        };
    }
}
