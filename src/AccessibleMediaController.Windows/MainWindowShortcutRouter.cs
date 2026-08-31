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

    public static bool IsSessionListShortcut(Key key, ModifierKeys modifiers) =>
        key == Key.S && modifiers == (ModifierKeys.Control | ModifierKeys.Shift);

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
