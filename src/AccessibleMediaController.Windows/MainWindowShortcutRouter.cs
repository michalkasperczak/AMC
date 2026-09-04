using System.Windows.Input;
using AccessibleMediaController.Core.Commands;
using AccessibleMediaController.Core.Playback;

namespace AccessibleMediaController.Windows;

internal enum MainWindowDigitShortcutKind
{
    None,
    SessionList,
    SessionSlot,
    Preset
}

internal readonly record struct MainWindowDigitShortcut(
    MainWindowDigitShortcutKind Kind,
    int Slot);

internal static class MainWindowShortcutRouter
{
    public static string? ResolveNumberedView(
        Key key,
        ModifierKeys modifiers,
        string sessionId,
        string viewName)
    {
        if (modifiers != ModifierKeys.Alt) return null;
        if (string.Equals(viewName, "Ulubione", StringComparison.Ordinal)
            || string.Equals(sessionId, "podcasts", StringComparison.Ordinal)
               && string.Equals(viewName, "Nowe odcinki", StringComparison.Ordinal)
            || string.Equals(viewName, "Biblioteka", StringComparison.Ordinal)
               && !string.Equals(sessionId, "local", StringComparison.Ordinal))
        {
            return key switch
            {
                Key.D1 => CommandIds.SortCollectionByAdded,
                Key.D2 => CommandIds.SortCollectionAlphabetically,
                Key.D3 => CommandIds.SortCollectionCustom,
                _ => null
            };
        }
        return (sessionId, key) switch
        {
            ("local", Key.D1) => CommandIds.ViewFolders,
            ("local", Key.D2) => CommandIds.ViewAllLocalFiles,
            ("local", Key.D3) => CommandIds.ViewCustomLocalOrder,
            _ => null
        };
    }

    public static string? ResolveTransientRadioView(
        Key key,
        ModifierKeys modifiers,
        string sessionId) =>
        modifiers == ModifierKeys.Alt
        && key == Key.R
        && string.Equals(sessionId, "radio", StringComparison.Ordinal)
            ? CommandIds.ViewActiveRadioRecordings
            : null;

    public static string? ResolvePlayerAudioProcessing(
        Key key,
        ModifierKeys modifiers,
        bool playerActive,
        PlaybackAudioProcessingCapabilities capabilities)
    {
        if (!playerActive || modifiers != ModifierKeys.Shift) return null;
        return key switch
        {
            Key.N when capabilities.HasFlag(PlaybackAudioProcessingCapabilities.LoudnessNormalization) =>
                CommandIds.ToggleLoudnessNormalization,
            Key.T when capabilities.HasFlag(PlaybackAudioProcessingCapabilities.SmoothTrackTransitions) =>
                CommandIds.ToggleSmoothTrackTransitions,
            Key.C when capabilities.HasFlag(PlaybackAudioProcessingCapabilities.InterTrackSilence) =>
                CommandIds.CycleInterTrackSilence,
            _ => null
        };
    }

    public static string? ResolvePlayerAudioProcessingFromVirtualKey(
        int virtualKey,
        ModifierKeys modifiers,
        bool playerActive,
        PlaybackAudioProcessingCapabilities capabilities) =>
        ResolvePlayerAudioProcessing(
            KeyInterop.KeyFromVirtualKey(virtualKey),
            modifiers,
            playerActive,
            capabilities);

    public static string? ResolvePlayerChapterList(
        Key key,
        ModifierKeys modifiers,
        bool playerActive) =>
        playerActive && key == Key.C && modifiers == ModifierKeys.None
            ? CommandIds.ViewChapters
            : null;

    public static bool IsSessionListShortcut(Key key, ModifierKeys modifiers) =>
        key == Key.S && modifiers == (ModifierKeys.Control | ModifierKeys.Shift);

    public static string? ResolveAudioOutputSelection(Key key, ModifierKeys modifiers) =>
        key == Key.A && modifiers == ModifierKeys.Shift
            ? CommandIds.SelectAudioOutput
            : null;

    public static string? ResolvePodcastEpisodeFileAction(
        Key key,
        ModifierKeys modifiers,
        string sessionId,
        bool itemContext)
    {
        if (!itemContext
            || !string.Equals(sessionId, "podcasts", StringComparison.Ordinal)
            || modifiers != ModifierKeys.Control)
        {
            return null;
        }

        return key switch
        {
            Key.D => CommandIds.DownloadInService,
            Key.S => CommandIds.SavePodcastAs,
            _ => null
        };
    }

    public static string? ResolveRadioRecordingBookmark(
        Key key,
        ModifierKeys modifiers,
        bool recordingContext)
    {
        if (!recordingContext || key != Key.B) return null;
        return modifiers switch
        {
            ModifierKeys.None => CommandIds.AddBookmark,
            ModifierKeys.Shift => CommandIds.AddNamedBookmark,
            _ => null
        };
    }

    public static MainWindowDigitShortcut ResolveDigit(
        Key key,
        ModifierKeys modifiers,
        bool presetsAvailable)
    {
        if (presetsAvailable
            && modifiers == (ModifierKeys.Control | ModifierKeys.Shift)
            && RadioPresetKeyMap.TryGetSlot(key, out var presetSlot))
        {
            return new MainWindowDigitShortcut(MainWindowDigitShortcutKind.Preset, presetSlot);
        }

        if (modifiers == ModifierKeys.Control && TryGetDigit(key, out var digit))
        {
            return digit == 0
                ? new MainWindowDigitShortcut(MainWindowDigitShortcutKind.SessionList, 0)
                : new MainWindowDigitShortcut(MainWindowDigitShortcutKind.SessionSlot, digit);
        }
        return new MainWindowDigitShortcut(MainWindowDigitShortcutKind.None, 0);
    }

    private static bool TryGetDigit(Key key, out int digit)
    {
        if (key is >= Key.D0 and <= Key.D9)
        {
            digit = (int)key - (int)Key.D0;
            return true;
        }
        if (key is >= Key.NumPad0 and <= Key.NumPad9)
        {
            digit = (int)key - (int)Key.NumPad0;
            return true;
        }
        digit = 0;
        return false;
    }
}
