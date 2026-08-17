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
    void ShowCurrentSession(string viewName);
    void ShowFilter();
    void ShowSessionList();
    void ShowPlaylistManager();
    void ShowCommandPalette();
    void ShowItemInformation(bool extended);
    void OpenOfficialApplication();
    void ShowHelp();
    void ShowSettings(SettingsTarget target);
    void ToggleAccessibilityMessages();
    void ToggleDetailedHints();
    void OpenLocalFiles();
    void OpenLocalFolder();
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

        var current = sessions.Current;
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
            case CommandIds.PlayPause:
                current.TogglePlayback();
                announcements.Announce(current.IsPlaying
                    ? $"Odtwarzanie: {current.CurrentItem.Title}"
                    : $"Pauza: {current.CurrentItem.Title}");
                return new(true);
            case CommandIds.ActivateSelected:
                var selectedToActivate = application.SelectedItem ?? current.CurrentItem;
                if (!current.Activate(selectedToActivate))
                {
                    announcements.Announce("Nie można otworzyć wybranego elementu w tej sesji");
                    return new(false);
                }
                announcements.Announce(current.IsPlaying
                    ? $"Odtwarzanie: {selectedToActivate.Title}"
                    : $"Pauza: {selectedToActivate.Title}");
                return new(true);
            case CommandIds.Previous:
                current.Move(-1);
                announcements.Announce(FormatItem(current.CurrentItem));
                return new(true);
            case CommandIds.Next:
                current.Move(1);
                announcements.Announce(FormatItem(current.CurrentItem));
                return new(true);
            case CommandIds.SeekBackward10: return Seek(current, -10);
            case CommandIds.SeekForward10: return Seek(current, 10);
            case CommandIds.SeekBackward60: return Seek(current, -60);
            case CommandIds.SeekForward60: return Seek(current, 60);
            case CommandIds.VolumeUp5: return Volume(current, 5);
            case CommandIds.VolumeDown5: return Volume(current, -5);
            case CommandIds.VolumeUp1: return Volume(current, 1);
            case CommandIds.VolumeDown1: return Volume(current, -1);
            case CommandIds.TrackStart:
                current.SetPosition(TimeSpan.Zero);
                announcements.Announce("0:00");
                return new(true);
            case CommandIds.TrackEnd:
                var nearEnd = Max(TimeSpan.Zero, current.CurrentItem.Duration - TimeSpan.FromSeconds(10));
                current.SetPosition(nearEnd);
                announcements.Announce(FormatTime(nearEnd));
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
                var favoriteItem = application.SelectedItem ?? current.CurrentItem;
                var favorite = current.ToggleFavorite(favoriteItem);
                AnnounceTemplate(
                    favorite ? "favorite.added" : "favorite.removed",
                    favorite ? "Dodano do ulubionych: {item}" : "Usunięto z ulubionych: {item}",
                    ("item", favoriteItem.Title));
                return new(true);
            case CommandIds.ToggleLibrary:
                var library = current.ToggleLibrary(application.SelectedItem ?? current.CurrentItem);
                announcements.Announce(library ? "Dodano do biblioteki" : "Usunięto z biblioteki");
                return new(true);
            case CommandIds.ManagePlaylists:
                application.ShowPlaylistManager();
                return new(true);
            case CommandIds.ItemInformation:
                application.ShowItemInformation(false);
                return new(true);
            case CommandIds.ExtendedInformation:
                application.ShowItemInformation(true);
                return new(true);
            case CommandIds.OpenOfficialApp:
                application.OpenOfficialApplication();
                return new(true);
            case CommandIds.Help:
                application.ShowHelp();
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
            case CommandIds.ViewQueue: return ShowView("Kolejka");
            case CommandIds.ViewAlbums: return ShowView("Albumy");
            case CommandIds.ViewRadio: return ShowView("Radio i rekomendacje");
            case CommandIds.ViewMixes: return ShowView("Miksy");
            case CommandIds.ViewHistory: return ShowView("Historia");
            case CommandIds.ViewNowPlaying: return ShowView("Teraz odtwarzane");
            case CommandIds.ViewOutputs: return ShowView("Wyjścia i urządzenia");
            case CommandIds.ViewDownloads: return ShowView("Pobrane");
            case CommandIds.AddQueue:
                var queueItem = application.SelectedItem ?? current.CurrentItem;
                var queued = current.ToggleQueue(queueItem);
                AnnounceTemplate(
                    queued ? "queue.added" : "queue.removed",
                    queued ? "Dodano do kolejki: {item}" : "Usunięto z kolejki: {item}",
                    ("item", queueItem.Title));
                return new(true);
            case CommandIds.TogglePlayNext:
                var playNextItem = application.SelectedItem ?? current.CurrentItem;
                var playNext = current.TogglePlayNext(playNextItem);
                AnnounceTemplate(
                    playNext ? "playNext.added" : "playNext.removed",
                    playNext
                        ? "Odtwarzaj jako następne: {item}"
                        : "Usunięto z następnych: {item}",
                    ("item", playNextItem.Title));
                return new(true);
            case CommandIds.StartRadio:
                announcements.Announce("Uruchamianie radia — demonstracja");
                return new(true);
            default:
                announcements.Announce("Nieprzypisane polecenie");
                return new(false);
        }
    }

    private static bool TryGetSettingsTarget(string commandId, out SettingsTarget target)
    {
        SettingsTarget? resolved = commandId switch
        {
            CommandIds.SettingsGeneral => SettingsTarget.General,
            CommandIds.SettingsLanguage => SettingsTarget.Language,
            CommandIds.SettingsStartupTarget => SettingsTarget.StartupTarget,
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
        announcements.Announce(FormatTime(session.Position));
        return new(true);
    }

    private CommandExecutionResult Volume(DemoMediaSession session, int delta)
    {
        session.ChangeVolume(delta);
        AnnounceTemplate("volume.changed", "{value}%", ("value", session.Volume.ToString()));
        return new(true);
    }

    private CommandExecutionResult ShowView(string name)
    {
        application.ShowCurrentSession(name);
        return new(true);
    }

    private static TimeSpan Max(TimeSpan first, TimeSpan second) => first >= second ? first : second;

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
