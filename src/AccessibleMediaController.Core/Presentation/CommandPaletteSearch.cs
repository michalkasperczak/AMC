using System.Globalization;
using System.Text;
using AccessibleMediaController.Core.Commands;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Input;

namespace AccessibleMediaController.Core.Presentation;

public sealed record CommandPaletteEntry(
    string CommandId,
    string DisplayName,
    string? LocalShortcut,
    string? PrefixShortcut)
{
    public string Label
    {
        get
        {
            var parts = new List<string> { DisplayName };
            if (!string.IsNullOrWhiteSpace(LocalShortcut)) parts.Add(LocalShortcut);
            if (!string.IsNullOrWhiteSpace(PrefixShortcut)
                && !string.Equals(LocalShortcut, PrefixShortcut, StringComparison.OrdinalIgnoreCase))
            {
                parts.Add($"prefiks {PrefixShortcut}");
            }
            return string.Join(", ", parts);
        }
    }

    public override string ToString() => Label;
}

public static class CommandPaletteSearch
{
    public static IReadOnlyList<CommandPaletteEntry> CreateEntries(
        KeyboardProfile profile,
        AppSettings settings,
        bool includeCommandPalette = false,
        RadioSettings? radioSettings = null)
    {
        var shortcuts = profile.Bindings
            .GroupBy(pair => pair.Value, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => string.Join(", ", group
                    .Select(pair => pair.Key)
                    .OrderBy(value => value, StringComparer.CurrentCultureIgnoreCase)),
                StringComparer.Ordinal);

        return CommandCatalog.GetAllCommandIds()
            .Where(commandId => includeCommandPalette || commandId != CommandIds.CommandPalette)
            .Select(commandId => new CommandPaletteEntry(
                commandId,
                GetDisplayName(commandId, settings, radioSettings),
                GetLocalShortcut(commandId),
                shortcuts.GetValueOrDefault(commandId)))
            .OrderBy(entry => entry.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    private static string GetDisplayName(
        string commandId,
        AppSettings settings,
        RadioSettings? radioSettings)
    {
        const string sessionSlotPrefix = "session.slot.";
        if (commandId.StartsWith(sessionSlotPrefix, StringComparison.Ordinal)
            && int.TryParse(commandId.AsSpan(sessionSlotPrefix.Length), out var slot)
            && settings.SessionSlots.TryGetValue(slot, out var sessionId))
        {
            return $"Wybierz sesję {slot}: {SessionSlotOrder.GetDisplayName(sessionId)}";
        }

        return commandId switch
        {
            CommandIds.SettingsToggleMessages => settings.Messages.Enabled
                ? "Komunikaty dostępności: włączone. Enter: wyłącz"
                : "Komunikaty dostępności: wyłączone. Enter: włącz",
            CommandIds.SettingsToggleDetailedHints => settings.Messages.DetailedHints
                ? "Szczegółowe podpowiedzi klawiatury: włączone. Enter: wyłącz"
                : "Szczegółowe podpowiedzi klawiatury: wyłączone. Enter: włącz",
            CommandIds.SettingsToggleSeekMessages => settings.Messages.SeekMessages
                ? "Automatyczne komunikaty odtwarzacza: włączone. Enter: wyłącz"
                : "Automatyczne komunikaty odtwarzacza: wyłączone. Enter: włącz",
            CommandIds.SettingsHistoryMessages =>
                $"Komunikaty historii widoków: {OnOff(settings.Messages.HistoryMessages)}. Enter: ustawienia",
            CommandIds.SettingsArrowSeekMessages =>
                $"Komunikaty przewijania strzałkami: {OnOff(settings.Messages.ArrowSeekMessages)}. Enter: ustawienia",
            CommandIds.SettingsPercentageSeekMessages =>
                $"Komunikaty skoków cyframi: {OnOff(settings.Messages.PercentageSeekMessages)}. Enter: ustawienia",
            CommandIds.SettingsBookmarkNavigationMessages =>
                $"Komunikaty nawigacji po zakładkach: {OnOff(settings.Messages.BookmarkNavigationMessages)}. Enter: ustawienia",
            CommandIds.SettingsVolumeMessages =>
                $"Komunikaty zmian głośności: {OnOff(settings.Messages.VolumeMessages)}. Enter: ustawienia",
            CommandIds.SettingsPlaybackMessages =>
                $"Komunikaty odtwarzania i pauzy: {OnOff(settings.Messages.PlaybackMessages)}. Enter: ustawienia",
            CommandIds.SettingsAutomaticRecognitionMessages =>
                $"Oznajmianie automatycznie rozpoznanych utworów: {OnOff(settings.Messages.AutomaticRecognitionMessages)}. Enter: ustawienia",
            CommandIds.ToggleRadioRecognitionAnnouncements =>
                $"Oznajmianie rozpoznanych utworów: {OnOff(settings.Messages.AutomaticRecognitionMessages)}. Enter: przełącz",
            CommandIds.SettingsRadioRecognitionScope =>
                $"Zakres automatycznego rozpoznawania radia: {RadioRecognitionScopeRules.GetLabel(radioSettings?.AutomaticTrackRecognitionScope ?? RadioRecognitionScope.CurrentStation)}. Enter: ustawienia",
            CommandIds.SettingsLoudnessNormalization =>
                $"Globalna normalizacja głośności: {OnOff(settings.Audio.LoudnessNormalizationEnabled)}. Enter: ustawienia",
            CommandIds.SettingsSmoothTrackTransitions =>
                $"Globalne łagodne przejścia między utworami: {OnOff(settings.Audio.SmoothTrackTransitionsEnabled)}. Enter: ustawienia",
            CommandIds.SettingsInterTrackSilence =>
                $"Globalna cisza między utworami: {PlaybackAudioSettingsRules.GetInterTrackSilenceLabel(settings.Audio.InterTrackSilenceMilliseconds)}. Enter: ustawienia",
            CommandIds.ToggleLoudnessNormalization =>
                $"Globalna normalizacja głośności: {OnOff(settings.Audio.LoudnessNormalizationEnabled)}. Enter: przełącz",
            CommandIds.ToggleSmoothTrackTransitions =>
                $"Globalne łagodne przejścia między utworami: {OnOff(settings.Audio.SmoothTrackTransitionsEnabled)}. Enter: przełącz",
            CommandIds.CycleInterTrackSilence =>
                $"Globalna cisza między utworami: {PlaybackAudioSettingsRules.GetInterTrackSilenceLabel(settings.Audio.InterTrackSilenceMilliseconds)}. Enter: następna wartość",
            CommandIds.SettingsPercentageSeekAnnouncement =>
                $"Komunikat po skoku cyfrą: {GetPercentageSeekAnnouncementName(settings.Messages.PercentageSeekAnnouncement)}",
            _ => CommandCatalog.GetDisplayName(commandId)
        };
    }

    private static string GetPercentageSeekAnnouncementName(PercentageSeekAnnouncementMode mode) => mode switch
    {
        PercentageSeekAnnouncementMode.Time => "tylko czas",
        PercentageSeekAnnouncementMode.PercentAndTime => "procent i czas",
        _ => "tylko procent"
    };

    private static string OnOff(bool enabled) => enabled ? "włączone" : "wyłączone";

    public static string ContinueOrRestartListQuery(
        IReadOnlyList<CommandPaletteEntry> entries,
        string currentQuery,
        string input)
    {
        if (string.IsNullOrEmpty(input)) return currentQuery;

        var continued = currentQuery + input;
        if (Filter(entries, continued).Count > 0) return continued;
        if (Filter(entries, input).Count > 0) return input;
        return string.Empty;
    }

    private static string? GetLocalShortcut(string commandId)
    {
        if (CommandIds.TryParseSeekPercent(commandId, out var percent))
        {
            return $"{percent / 10} (odtwarzacz)";
        }

        if (CommandIds.TryParseRadioPreset(commandId, out var radioPresetSlot))
        {
            return $"Ctrl+Shift+{RadioPresetSlots.ShortcutLabel(radioPresetSlot)} (sesja obsługująca presety)";
        }

        const string sessionSlotPrefix = "session.slot.";
        if (commandId.StartsWith(sessionSlotPrefix, StringComparison.Ordinal)
            && int.TryParse(commandId.AsSpan(sessionSlotPrefix.Length), out var slot)
            && slot is >= 1 and <= 9)
        {
            return $"Ctrl+{slot}";
        }

        return commandId switch
        {
            CommandIds.PlayPause => "Space",
            CommandIds.ActivateSelected => "Ctrl+Enter",
            CommandIds.Previous => "PageUp (odtwarzacz)",
            CommandIds.Next => "PageDown (odtwarzacz)",
            CommandIds.SeekBackward10 => "Left (odtwarzacz)",
            CommandIds.SeekForward10 => "Right (odtwarzacz)",
            CommandIds.SeekBackward30 => "Shift+Left (odtwarzacz)",
            CommandIds.SeekForward30 => "Shift+Right (odtwarzacz)",
            CommandIds.SeekBackward60 => "Ctrl+Left (odtwarzacz)",
            CommandIds.SeekForward60 => "Ctrl+Right (odtwarzacz)",
            CommandIds.VolumeUp5 => "Up (odtwarzacz)",
            CommandIds.VolumeDown5 => "Down (odtwarzacz)",
            CommandIds.VolumeUp1 => "Shift+Up (odtwarzacz)",
            CommandIds.VolumeDown1 => "Shift+Down (odtwarzacz)",
            CommandIds.ToggleMuteCurrentSession => "Ctrl+M",
            CommandIds.ToggleMuteAllSessions => "Ctrl+Shift+M",
            CommandIds.ToggleLoudnessNormalization => "Shift+N (odtwarzacz obsługujący przetwarzanie dźwięku)",
            CommandIds.ToggleSmoothTrackTransitions => "Shift+T (odtwarzacz obsługujący przetwarzanie dźwięku)",
            CommandIds.CycleInterTrackSilence => "Shift+C (odtwarzacz obsługujący przetwarzanie dźwięku)",
            CommandIds.PlaybackRateDown => "Shift+, (odtwarzacz)",
            CommandIds.PlaybackRateUp => "Shift+. (odtwarzacz)",
            CommandIds.PlaybackRateReset => "Ctrl+. (odtwarzacz)",
            CommandIds.TrackStart => "Home (odtwarzacz)",
            CommandIds.TrackEnd => "End (odtwarzacz)",
            CommandIds.SeekToTime => "Ctrl+J (odtwarzacz)",
            CommandIds.SeekToPercentage => "Ctrl+Shift+J (odtwarzacz)",
            CommandIds.MarkClipStart => "I (odtwarzacz lokalnego pliku)",
            CommandIds.MarkClipEnd => "O (odtwarzacz lokalnego pliku)",
            CommandIds.JumpClipStart => "Shift+I (odtwarzacz lokalnego pliku)",
            CommandIds.JumpClipEnd => "Shift+O (odtwarzacz lokalnego pliku)",
            CommandIds.PreviousClipBoundary => "Alt+PageUp (odtwarzacz lokalnego pliku)",
            CommandIds.NextClipBoundary => "Alt+PageDown (odtwarzacz lokalnego pliku)",
            CommandIds.ExportClip => "X (odtwarzacz lokalnego pliku)",
            CommandIds.RemoveClipFromOriginal => "Ctrl+X (odtwarzacz lokalnego pliku)",
            CommandIds.ClearClipSelection => "Shift+X (odtwarzacz lokalnego pliku)",
            CommandIds.TimeElapsed => "Ctrl+Shift+E",
            CommandIds.TimeRemaining => "Ctrl+Shift+R",
            CommandIds.TimeTotal => "Ctrl+Shift+T",
            CommandIds.SessionList => "Ctrl+Shift+S lub Ctrl+0",
            CommandIds.SessionPrevious => "Ctrl+PageUp",
            CommandIds.SessionNext => "Ctrl+PageDown",
            CommandIds.ViewFavorites => "Ctrl+U",
            CommandIds.ToggleFavorite => "Ctrl+Shift+U",
            CommandIds.ViewPlaylists => "Ctrl+P",
            CommandIds.ManagePlaylists => "Ctrl+Shift+P",
            CommandIds.ViewLibrary => "Ctrl+L",
            CommandIds.SortCollectionByAdded => "Alt+1 (Biblioteka lub Ulubione)",
            CommandIds.SortCollectionAlphabetically => "Alt+2 (Biblioteka lub Ulubione)",
            CommandIds.SortCollectionCustom => "Alt+3 (Biblioteka lub Ulubione)",
            CommandIds.ViewFolders => "Alt+1 (lista lokalna)",
            CommandIds.ViewAllLocalFiles => "Alt+2 (lista lokalna)",
            CommandIds.ViewCustomLocalOrder => "Alt+3 (lista lokalna)",
            CommandIds.RefreshLocalLibrary => "F5 (lista lokalna)",
            CommandIds.ManageLocalSources => "Ctrl+F5",
            CommandIds.ManageWiiMDevices => "Ctrl+F5 (WiiM)",
            CommandIds.RefreshWiiMDevices => "F5 (WiiM)",
            CommandIds.OpenOnWiiM => "Ctrl+Alt+W",
            CommandIds.AddWiiMNetworkStream => "Ctrl+N (WiiM)",
            CommandIds.ImportWiiMNetworkStreams => "Ctrl+O (WiiM)",
            CommandIds.ExportWiiMNetworkStreams => "Ctrl+Shift+O (WiiM)",
            CommandIds.ViewWiiMDevicePresets => "Ctrl+Alt+P (WiiM)",
            CommandIds.PreviousWiiMDevicePreset => "Alt+PageUp (odtwarzacz WiiM)",
            CommandIds.NextWiiMDevicePreset => "Alt+PageDown (odtwarzacz WiiM)",
            CommandIds.SelectWiiMInput => "I (odtwarzacz WiiM)",
            CommandIds.SelectWiiMOutput => "O (odtwarzacz WiiM)",
            CommandIds.SelectWiiMEqualizer => "E (odtwarzacz WiiM)",
            CommandIds.SelectWiiMRepeatMode => "R (odtwarzacz WiiM)",
            CommandIds.ToggleWiiMShuffle => "S (odtwarzacz WiiM)",
            CommandIds.SetWiiMSleepTimer => "T (odtwarzacz WiiM)",
            CommandIds.RenameLibraryItem => "F2 (Biblioteka lokalna, Radio lub Podcasty)",
            CommandIds.RenameLocalFile => "Shift+F2 (lista lokalna)",
            CommandIds.MoveLocalLibraryItemUp => "Alt+Up (kolejność własna lub Ulubione)",
            CommandIds.MoveLocalLibraryItemDown => "Alt+Down (kolejność własna lub Ulubione)",
            CommandIds.ToggleLibrary => "Ctrl+Shift+L",
            CommandIds.ViewQueue => "Ctrl+Q",
            CommandIds.ViewHistory => "Ctrl+H",
            CommandIds.ViewBookmarks => "Ctrl+B",
            CommandIds.AddBookmark => "B (odtwarzacz)",
            CommandIds.AddNamedBookmark => "Ctrl+Shift+B (odtwarzacz)",
            CommandIds.PreviousBookmark => "Shift+PageUp (odtwarzacz)",
            CommandIds.NextBookmark => "Shift+PageDown (odtwarzacz)",
            CommandIds.ViewChapters => "Ctrl+Alt+B",
            CommandIds.AddNamedChapter => "Ctrl+Alt+Shift+B (odtwarzacz)",
            CommandIds.PreviousChapter => "Ctrl+Shift+strzałka w lewo (odtwarzacz)",
            CommandIds.NextChapter => "Ctrl+Shift+strzałka w prawo (odtwarzacz)",
            CommandIds.AddQueue => "Shift+Enter",
            CommandIds.TogglePlayNext => "Ctrl+Shift+Enter",
            CommandIds.ViewAlbums => "Ctrl+Shift+A",
            CommandIds.ViewNowPlaying => "F6",
            CommandIds.SelectAudioOutput => "Shift+A",
            CommandIds.FilterCurrent => "Ctrl+K",
            CommandIds.SearchCurrent => "Ctrl+F",
            CommandIds.SearchAll => "Ctrl+Shift+F",
            CommandIds.ItemProperties => "Alt+Enter",
            CommandIds.PodcastDescription => "Alt+D (Podcasty)",
            CommandIds.CurrentBroadcastInformation => "Alt+D (Radio internetowe i WiiM)",
            CommandIds.ItemPlaybackOptions => "Alt+Shift+Enter",
            CommandIds.Help => "F1",
            CommandIds.KeyboardHelp => "Ctrl+F1",
            CommandIds.OpenLocalFiles => "Ctrl+O",
            CommandIds.OpenLocalFolder => "Ctrl+Shift+O",
            CommandIds.ImportRadioPlaylist => "Ctrl+O (Radio internetowe)",
            CommandIds.ExportRadioFavorites => "Ctrl+E (Radio internetowe)",
            CommandIds.AddPodcast => "Ctrl+N (Podcasty)",
            CommandIds.ImportPodcastOpml => "Ctrl+O (Podcasty)",
            CommandIds.RefreshPodcast => "F5 (Podcasty)",
            CommandIds.RefreshPodcastLibrary => "Ctrl+F5 (Podcasty)",
            CommandIds.ViewPodcastInbox => "Ctrl+I (Podcasty)",
            CommandIds.ViewPodcastInProgress => "Ctrl+Shift+I (Podcasty)",
            CommandIds.DownloadInService => "Ctrl+D (odcinki Podcastów)",
            CommandIds.SavePodcastAs => "Ctrl+S (jeden odcinek Podcastów)",
            CommandIds.AddRadioStation => "Ctrl+N (Radio internetowe)",
            CommandIds.ToggleRadioRecording => "R (odtwarzacz radia) lub Ctrl+Alt+R (lista radia)",
            CommandIds.ToggleRadioRecordingPause => "Shift+Spacja (Radio internetowe)",
            CommandIds.SplitRadioRecording => "T (odtwarzacz radia lub widok Nagrywane)",
            CommandIds.StopAllRadioRecordings => "Ctrl+Alt+Shift+R",
            CommandIds.AddRadioSchedule => "Shift+R (Radio internetowe)",
            CommandIds.ManageRadioSchedules => "Ctrl+Shift+H (Radio internetowe)",
            CommandIds.ViewActiveRadioRecordings => "Alt+R (Radio internetowe)",
            CommandIds.ViewRecordedRadioFiles => "Alt+Shift+R",
            CommandIds.RadioJumpLive => "End (odtwarzacz radia)",
            CommandIds.RecognizeRadioTrack => "S (odtwarzacz radia)",
            CommandIds.ToggleRadioRecognitionMonitoring => "Shift+S (odtwarzacz radia)",
            CommandIds.ToggleRadioRecognitionAnnouncements => "Ctrl+Alt+Shift+S (Radio internetowe)",
            CommandIds.ViewRadioRecognitionHistory => "Ctrl+Alt+S (Radio internetowe)",
            CommandIds.ViewRadioPresets => "Ctrl+Alt+P (Pliki lokalne lub Radio internetowe)",
            CommandIds.AssignRadioPreset => "Ctrl+Alt+Shift+P (sesja obsługująca presety)",
            CommandIds.SettingsGeneral => "Ctrl+,",
            CommandIds.SettingsToggleSeekMessages => "Ctrl+Shift+G",
            _ => null
        };
    }

    public static IReadOnlyList<CommandPaletteEntry> Filter(
        IEnumerable<CommandPaletteEntry> entries,
        string query)
    {
        var tokens = FoldForSearch(query)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0) return entries.ToArray();

        return entries
            .Where(entry =>
            {
                var searchableText = entry.CommandId.StartsWith("settings.", StringComparison.Ordinal)
                    ? $"{entry.Label} ustawienia"
                    : entry.Label;
                var searchableLabel = FoldForSearch(searchableText);
                return tokens.All(token => searchableLabel.Contains(token, StringComparison.Ordinal));
            })
            .ToArray();
    }

    private static string FoldForSearch(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark) continue;
            builder.Append(character switch
            {
                'ł' or 'Ł' => 'L',
                _ => char.ToUpperInvariant(character)
            });
        }
        return builder.ToString();
    }
}
