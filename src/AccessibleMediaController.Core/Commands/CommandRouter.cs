using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Presentation;
using AccessibleMediaController.Core.Sessions;

namespace AccessibleMediaController.Core.Commands;

public interface IAnnouncementSink
{
    void Announce(string message);
}

public interface IApplicationActions
{
    MediaItem? SelectedItem { get; }
    MediaItem? ActionItem { get; }
    IReadOnlyList<MediaItem> ActionItems { get; }
    void ShowCurrentSession(string viewName);
    void ShowFilter();
    void ShowSessionList();
    void ShowPlaylistManager();
    void ShowCommandPalette();
    void ShowItemProperties();
    void ShowItemPlaybackOptions();
    void OpenOfficialApplication();
    void ShowHelp();
    void ToggleKeyboardHelp();
    void ShowSettings(SettingsTarget target);
    void ToggleAccessibilityMessages();
    void ToggleDetailedHints();
    void ToggleSeekMessages();
    void OpenLocalFiles();
    void OpenLocalFolder();
    void RefreshLocalLibrary();
    void ShowLocalSourceManager();
    void RenameLibraryItem();
    void RenameLocalFile();
    void MoveLocalLibrarySelection(int direction);
    void ShowSeekToTime();
    void ShowSeekToPercentage();
    void AddBookmark();
    void AddNamedBookmark();
    void NavigateBookmark(int direction);
}

public readonly record struct CommandExecutionResult(bool Handled, bool KeepPrefixActive = false);

public sealed class CommandRouter(
    SessionManager sessions,
    AppSettings settings,
    IAnnouncementSink announcements,
    IApplicationActions application)
{
    public CommandExecutionResult Execute(string commandId)
    {
        if (commandId.StartsWith("session.slot.", StringComparison.Ordinal))
        {
            return SelectSessionSlot(commandId);
        }

        if (TryGetSettingsTarget(commandId, out var settingsTarget))
        {
            application.ShowSettings(settingsTarget);
            return new(true);
        }

        if (commandId == CommandIds.SettingsToggleMessages)
        {
            application.ToggleAccessibilityMessages();
            return new(true);
        }

        if (commandId == CommandIds.SettingsToggleDetailedHints)
        {
            application.ToggleDetailedHints();
            return new(true);
        }

        if (commandId == CommandIds.SettingsToggleSeekMessages)
        {
            application.ToggleSeekMessages();
            return new(true);
        }

        var current = sessions.Current;
        if (CommandIds.TryParseSeekPercent(commandId, out var percent))
        {
            if (!current.HasCurrentItem) return MissingCurrentMediaItem();
            return SeekPercent(current, percent);
        }
        if (!current.HasItems && RequiresMediaItem(commandId)) return MissingMediaItem(current);

        switch (commandId)
        {
            case CommandIds.SessionList:
                application.ShowSessionList();
                return new(true);
            case CommandIds.SessionPrevious:
                AnnounceSession(sessions.MoveSession(-1));
                return new(true, true);
            case CommandIds.SessionNext:
                AnnounceSession(sessions.MoveSession(1));
                return new(true, true);
            case CommandIds.OpenLocalFiles:
                application.OpenLocalFiles();
                return new(true);
            case CommandIds.OpenLocalFolder:
                application.OpenLocalFolder();
                return new(true);
            case CommandIds.RefreshLocalLibrary:
                application.RefreshLocalLibrary();
                return new(true);
            case CommandIds.ManageLocalSources:
                application.ShowLocalSourceManager();
                return new(true);
            case CommandIds.RenameLibraryItem:
                application.RenameLibraryItem();
                return new(true);
            case CommandIds.RenameLocalFile:
                application.RenameLocalFile();
                return new(true);
            case CommandIds.MoveLocalLibraryItemUp:
                application.MoveLocalLibrarySelection(-1);
                return new(true);
            case CommandIds.MoveLocalLibraryItemDown:
                application.MoveLocalLibrarySelection(1);
                return new(true);
            case CommandIds.SeekToTime:
                application.ShowSeekToTime();
                return new(true);
            case CommandIds.SeekToPercentage:
                application.ShowSeekToPercentage();
                return new(true);
            case CommandIds.AddBookmark:
                application.AddBookmark();
                return new(true);
            case CommandIds.AddNamedBookmark:
                application.AddNamedBookmark();
                return new(true);
            case CommandIds.PreviousBookmark:
                application.NavigateBookmark(-1);
                return new(true);
            case CommandIds.NextBookmark:
                application.NavigateBookmark(1);
                return new(true);
            case CommandIds.ItemProperties:
                application.ShowItemProperties();
                return new(true);
            case CommandIds.ItemPlaybackOptions:
                application.ShowItemPlaybackOptions();
                return new(true);
            case CommandIds.PlayPause:
                if (!current.HasCurrentItem) return MissingCurrentMediaItem();
                current.TogglePlayback();
                if (settings.Messages.SeekMessages && settings.Messages.PlaybackMessages)
                {
                    announcements.Announce(current.IsPlaying
                        ? $"Odtwarzanie: {current.CurrentItem.Title}"
                        : $"Pauza: {current.CurrentItem.Title}");
                }
                return new(true);
            case CommandIds.ActivateSelected:
                var selectedToActivate = application.ActionItem ?? current.CurrentItem;
                if (!current.Activate(selectedToActivate))
                {
                    announcements.Announce("Nie można otworzyć wybranego elementu w tej sesji");
                    return new(false);
                }
                if (settings.Messages.SeekMessages && settings.Messages.PlaybackMessages)
                {
                    announcements.Announce(current.IsPlaying
                        ? $"Odtwarzanie: {selectedToActivate.Title}"
                        : $"Pauza: {selectedToActivate.Title}");
                }
                return new(true);
            case CommandIds.Previous:
                if (!current.HasCurrentItem) return MissingCurrentMediaItem();
                if (!current.PlayRelative(-1))
                {
                    announcements.Announce("To pierwszy element");
                    return new(false);
                }
                announcements.Announce($"Odtwarzanie: {FormatItem(current.CurrentItem)}");
                return new(true);
            case CommandIds.Next:
                if (!current.HasCurrentItem) return MissingCurrentMediaItem();
                if (!current.PlayRelative(1))
                {
                    announcements.Announce("To ostatni element");
                    return new(false);
                }
                announcements.Announce($"Odtwarzanie: {FormatItem(current.CurrentItem)}");
                return new(true);
            case CommandIds.SeekBackward10: return Seek(current, -10);
            case CommandIds.SeekForward10: return Seek(current, 10);
            case CommandIds.SeekBackward30: return Seek(current, -30);
            case CommandIds.SeekForward30: return Seek(current, 30);
            case CommandIds.SeekBackward60: return Seek(current, -60);
            case CommandIds.SeekForward60: return Seek(current, 60);
            case CommandIds.VolumeUp5: return Volume(current, 5);
            case CommandIds.VolumeDown5: return Volume(current, -5);
            case CommandIds.VolumeUp1: return Volume(current, 1);
            case CommandIds.VolumeDown1: return Volume(current, -1);
            case CommandIds.PlaybackRateDown: return PlaybackRate(current, -1);
            case CommandIds.PlaybackRateUp: return PlaybackRate(current, 1);
            case CommandIds.PlaybackRateReset: return ResetPlaybackRate(current);
            case CommandIds.TrackStart:
                current.SetPosition(TimeSpan.Zero);
                if (settings.Messages.SeekMessages && settings.Messages.ArrowSeekMessages) announcements.Announce("0:00");
                return new(true);
            case CommandIds.TrackEnd:
                var nearEnd = Max(TimeSpan.Zero, current.CurrentItem.Duration - TimeSpan.FromSeconds(10));
                current.SetPosition(nearEnd);
                if (settings.Messages.SeekMessages && settings.Messages.ArrowSeekMessages) announcements.Announce(FormatTime(nearEnd));
                return new(true);
            case CommandIds.TimeElapsed:
                AnnounceTemplate("time.elapsed", "{elapsed}", ("elapsed", FormatTime(current.Position)));
                return new(true);
            case CommandIds.TimeRemaining:
                AnnounceTemplate(
                    "time.remaining",
                    "{remaining}",
                    ("remaining", FormatTime(Max(TimeSpan.Zero, current.CurrentItem.Duration - current.Position))));
                return new(true);
            case CommandIds.TimeTotal:
                AnnounceTemplate("time.total", "{total}", ("total", FormatTime(current.CurrentItem.Duration)));
                return new(true);
            case CommandIds.ToggleFavorite:
                var favoriteItems = ResolveActionItems(current);
                var favorite = !favoriteItems.All(item => item.IsFavorite);
                foreach (var item in favoriteItems) item.IsFavorite = favorite;
                AnnounceTemplate(
                    favorite ? "favorite.added" : "favorite.removed",
                    favorite ? "Dodano do ulubionych: {item}" : "Usunięto z ulubionych: {item}",
                    ("item", FormatActionItems(favoriteItems)));
                return new(true);
            case CommandIds.ToggleLibrary:
                var libraryItems = ResolveActionItems(current);
                var library = !libraryItems.All(item => item.IsInLibrary);
                foreach (var item in libraryItems) item.IsInLibrary = library;
                announcements.Announce(library
                    ? $"Dodano do biblioteki: {FormatActionItems(libraryItems)}"
                    : $"Usunięto z biblioteki: {FormatActionItems(libraryItems)}");
                return new(true);
            case CommandIds.ManagePlaylists:
                application.ShowPlaylistManager();
                return new(true);
            case CommandIds.OpenOfficialApp:
                application.OpenOfficialApplication();
                return new(true);
            case CommandIds.Help:
                application.ShowHelp();
                return new(true);
            case CommandIds.KeyboardHelp:
                application.ToggleKeyboardHelp();
                return new(true);
            case CommandIds.FilterCurrent:
                application.ShowFilter();
                return new(true);
            case CommandIds.CommandPalette:
                application.ShowCommandPalette();
                return new(true);
            case CommandIds.DownloadInService:
                announcements.Announce("Pobieranie wewnątrz usługi nie jest jeszcze dostępne w tym prototypie");
                return new(true);
            case CommandIds.DownloadToDisk:
                announcements.Announce("Pobieranie na dysk nie jest jeszcze dostępne w tym prototypie");
                return new(true);
            case CommandIds.ViewFavorites: return ShowView("Ulubione");
            case CommandIds.ViewPlaylists: return ShowView("Playlisty");
            case CommandIds.SearchCurrent: return ShowView("Szukaj w bieżącej usłudze");
            case CommandIds.SearchAll: return ShowView("Szukaj we wszystkich usługach");
            case CommandIds.ViewLibrary: return ShowView("Biblioteka");
            case CommandIds.ViewFolders: return ShowView("Foldery");
            case CommandIds.ViewAllLocalFiles: return ShowView("Wszystkie pliki");
            case CommandIds.ViewCustomLocalOrder: return ShowView("Kolejność własna");
            case CommandIds.ViewQueue: return ShowView("Kolejka");
            case CommandIds.ViewAlbums: return ShowView("Albumy");
            case CommandIds.ViewRadio: return ShowView("Radio i rekomendacje");
            case CommandIds.ViewMixes: return ShowView("Miksy");
            case CommandIds.ViewHistory: return ShowView("Historia odtwarzania");
            case CommandIds.ViewBookmarks: return ShowView("Zakładki");
            case CommandIds.ViewNowPlaying: return ShowView("Teraz odtwarzane");
            case CommandIds.ViewOutputs: return ShowView("Wyjścia i urządzenia");
            case CommandIds.ViewDownloads: return ShowView("Pobrane");
            case CommandIds.AddQueue:
                var queueItems = ResolveActionItems(current);
                var queued = !queueItems.All(item => item.IsInQueue || item.IsPlayNext);
                foreach (var item in queueItems)
                {
                    item.IsInQueue = queued;
                    if (!queued) item.IsPlayNext = false;
                }
                AnnounceTemplate(
                    queued ? "queue.added" : "queue.removed",
                    queued ? "Dodano do kolejki: {item}" : "Usunięto z kolejki: {item}",
                    ("item", FormatActionItems(queueItems)));
                return new(true);
            case CommandIds.TogglePlayNext:
                var playNextItems = ResolveActionItems(current);
                var playNext = !playNextItems.All(item => item.IsPlayNext);
                foreach (var item in playNextItems) item.IsPlayNext = playNext;
                AnnounceTemplate(
                    playNext ? "playNext.added" : "playNext.removed",
                    playNext
                        ? "Odtwarzaj jako następne: {item}"
                        : "Usunięto z następnych: {item}",
                    ("item", FormatActionItems(playNextItems)));
                return new(true);
            case CommandIds.StartRadio:
                announcements.Announce("Uruchamianie radia — demonstracja");
                return new(true);
            default:
                announcements.Announce("Nieprzypisane polecenie");
                return new(false);
        }
    }

    private CommandExecutionResult MissingCurrentMediaItem()
    {
        announcements.Announce("Brak następnego dostępnego elementu do odtworzenia");
        return new(false);
    }

    private static bool TryGetSettingsTarget(string commandId, out SettingsTarget target)
    {
        SettingsTarget? resolved = commandId switch
        {
            CommandIds.SettingsGeneral => SettingsTarget.General,
            CommandIds.SettingsLanguage => SettingsTarget.Language,
            CommandIds.SettingsStartupTarget => SettingsTarget.StartupTarget,
            CommandIds.SettingsSessionOrder => SettingsTarget.SessionOrder,
            CommandIds.SettingsPausePlaybackWhenLeavingPlayer => SettingsTarget.PausePlaybackWhenLeavingPlayer,
            CommandIds.SettingsRememberLocalPlaybackPositions => SettingsTarget.RememberLocalPlaybackPositions,
            CommandIds.SettingsPrefix => SettingsTarget.Prefix,
            CommandIds.SettingsPrefixTimeout => SettingsTarget.PrefixTimeout,
            CommandIds.SettingsKeyboardProfile => SettingsTarget.KeyboardProfile,
            CommandIds.SettingsActivateKeyboardProfile => SettingsTarget.ActivateKeyboardProfile,
            CommandIds.SettingsDuplicateKeyboardProfile => SettingsTarget.DuplicateKeyboardProfile,
            CommandIds.SettingsRenameKeyboardProfile => SettingsTarget.RenameKeyboardProfile,
            CommandIds.SettingsDeleteKeyboardProfile => SettingsTarget.DeleteKeyboardProfile,
            CommandIds.SettingsImportKeyboardMap => SettingsTarget.ImportKeyboardMap,
            CommandIds.SettingsExportKeyboardMap => SettingsTarget.ExportKeyboardMap,
            CommandIds.SettingsKeyboardBindings => SettingsTarget.KeyboardBindings,
            CommandIds.SettingsChangeKeyboardBinding => SettingsTarget.ChangeKeyboardBinding,
            CommandIds.SettingsRemoveKeyboardBinding => SettingsTarget.RemoveKeyboardBinding,
            CommandIds.SettingsListFieldOrder => SettingsTarget.ListFieldOrder,
            CommandIds.SettingsImportExport => SettingsTarget.ImportExport,
            CommandIds.SettingsImportConfiguration => SettingsTarget.ImportConfiguration,
            CommandIds.SettingsExportConfiguration => SettingsTarget.ExportConfiguration,
            CommandIds.SettingsImportFullBackup => SettingsTarget.ImportFullBackup,
            CommandIds.SettingsExportFullBackup => SettingsTarget.ExportFullBackup,
            CommandIds.SettingsMessages => SettingsTarget.Messages,
            CommandIds.SettingsHistoryMessages => SettingsTarget.HistoryMessages,
            CommandIds.SettingsArrowSeekMessages => SettingsTarget.ArrowSeekMessages,
            CommandIds.SettingsPercentageSeekMessages => SettingsTarget.PercentageSeekMessages,
            CommandIds.SettingsBookmarkNavigationMessages => SettingsTarget.BookmarkNavigationMessages,
            CommandIds.SettingsVolumeMessages => SettingsTarget.VolumeMessages,
            CommandIds.SettingsPlaybackMessages => SettingsTarget.PlaybackMessages,
            CommandIds.SettingsPercentageSeekAnnouncement => SettingsTarget.PercentageSeekAnnouncement,
            CommandIds.SettingsMessageTemplates => SettingsTarget.MessageTemplates,
            CommandIds.SettingsUpdates => SettingsTarget.Updates,
            _ => null
        };

        target = resolved ?? default;
        return resolved.HasValue;
    }

    private CommandExecutionResult SelectSessionSlot(string commandId)
    {
        if (!int.TryParse(commandId.AsSpan("session.slot.".Length), out var slot)) return new(false);
        var session = sessions.SelectSlot(slot);
        if (session is null)
        {
            AnnounceTemplate("session.unassigned", "Sesja {slot} nieprzypisana", ("slot", slot.ToString()));
            return new(true, true);
        }

        AnnounceSession(session, slot);
        return new(true, true);
    }

    private void AnnounceSession(DemoMediaSession session, int? slot = null)
    {
        var resolvedSlot = slot ?? sessions.FindSlot(session.Id);
        if (resolvedSlot is > 0)
        {
            AnnounceTemplate(
                "session.changed",
                "{slot}, {service}",
                ("slot", resolvedSlot.Value.ToString()),
                ("service", session.DisplayName));
        }
        else
        {
            announcements.Announce(session.DisplayName);
        }
    }

    private CommandExecutionResult Seek(DemoMediaSession session, int seconds)
    {
        session.Seek(TimeSpan.FromSeconds(seconds));
        if (settings.Messages.SeekMessages && settings.Messages.ArrowSeekMessages)
        {
            announcements.Announce(FormatTime(session.Position));
        }
        return new(true);
    }

    private CommandExecutionResult SeekPercent(DemoMediaSession session, int percent)
    {
        var duration = session.CurrentItem.Duration;
        if (duration <= TimeSpan.Zero)
        {
            announcements.Announce("Skok procentowy niedostępny: czas trwania jest nieznany");
            return new(true);
        }

        var position = TimeSpan.FromTicks((long)Math.Round(duration.Ticks * (percent / 100d)));
        session.SetPosition(position);
        if (settings.Messages.SeekMessages && settings.Messages.PercentageSeekMessages)
        {
            announcements.Announce(settings.Messages.PercentageSeekAnnouncement switch
            {
                PercentageSeekAnnouncementMode.Time => FormatTime(position),
                PercentageSeekAnnouncementMode.PercentAndTime => $"{percent}%, {FormatTime(position)}",
                _ => $"{percent}%"
            });
        }
        return new(true);
    }

    private CommandExecutionResult Volume(DemoMediaSession session, int delta)
    {
        session.ChangeVolume(delta);
        if (settings.Messages.SeekMessages && settings.Messages.VolumeMessages)
        {
            AnnounceTemplate("volume.changed", "{value}%", ("value", session.Volume.ToString()));
        }
        return new(true);
    }

    private CommandExecutionResult PlaybackRate(DemoMediaSession session, int direction)
    {
        if (!session.ChangePlaybackRate(direction))
        {
            announcements.Announce("Regulacja prędkości jest niedostępna w tej sesji");
            return new(true);
        }
        announcements.Announce(FormatPlaybackRate(session.PlaybackRate));
        return new(true);
    }

    private CommandExecutionResult ResetPlaybackRate(DemoMediaSession session)
    {
        if (!session.SetPlaybackRate(1d))
        {
            announcements.Announce("Regulacja prędkości jest niedostępna w tej sesji");
            return new(true);
        }
        announcements.Announce("Prędkość normalna");
        return new(true);
    }

    public static string FormatPlaybackRate(double playbackRate)
    {
        if (Math.Abs(playbackRate - 1d) < 0.001d) return "Prędkość normalna";
        return $"Prędkość {playbackRate.ToString("0.##", System.Globalization.CultureInfo.CurrentCulture)} razy";
    }

    private CommandExecutionResult ShowView(string name)
    {
        application.ShowCurrentSession(name);
        return new(true);
    }

    private static TimeSpan Max(TimeSpan first, TimeSpan second) => first >= second ? first : second;

    private CommandExecutionResult MissingMediaItem(DemoMediaSession session)
    {
        announcements.Announce($"Brak elementów w sesji {session.DisplayName}");
        return new(false);
    }

    private static bool RequiresMediaItem(string commandId) => commandId is
        CommandIds.PlayPause or CommandIds.ActivateSelected
        or CommandIds.Previous or CommandIds.Next
        or CommandIds.SeekBackward10 or CommandIds.SeekForward10
        or CommandIds.SeekBackward30 or CommandIds.SeekForward30
        or CommandIds.SeekBackward60 or CommandIds.SeekForward60
        or CommandIds.TrackStart or CommandIds.TrackEnd
        or CommandIds.TimeElapsed or CommandIds.TimeRemaining or CommandIds.TimeTotal
        or CommandIds.ItemProperties or CommandIds.ItemPlaybackOptions
        or CommandIds.ToggleFavorite or CommandIds.ToggleLibrary
        or CommandIds.AddQueue or CommandIds.TogglePlayNext
        or CommandIds.PlaybackRateDown or CommandIds.PlaybackRateUp or CommandIds.PlaybackRateReset;

    private IReadOnlyList<MediaItem> ResolveActionItems(DemoMediaSession session) =>
        application.ActionItems.Count > 0
            ? application.ActionItems
            : [application.ActionItem ?? session.CurrentItem];

    private static string FormatActionItems(IReadOnlyList<MediaItem> items)
    {
        if (items.Count == 1) return items[0].Title;
        var lastTwoDigits = items.Count % 100;
        var lastDigit = items.Count % 10;
        return lastDigit is >= 2 and <= 4 && lastTwoDigits is not (>= 12 and <= 14)
            ? $"{items.Count} elementy"
            : $"{items.Count} elementów";
    }

    private string FormatItem(MediaItem item) =>
        MediaItemFormatter.Format(item, settings.Lists.FieldOrder);

    private void AnnounceTemplate(string key, string fallback, params (string Name, string Value)[] values)
    {
        var template = settings.Messages.Templates.TryGetValue(key, out var configured) ? configured : fallback;
        if (string.IsNullOrEmpty(template)) return;
        foreach (var (name, value) in values)
        {
            template = template.Replace($"{{{name}}}", value, StringComparison.OrdinalIgnoreCase);
        }
        announcements.Announce(template);
    }

    public static string FormatTime(TimeSpan value)
    {
        return MediaItemFormatter.FormatDuration(value);
    }
}
