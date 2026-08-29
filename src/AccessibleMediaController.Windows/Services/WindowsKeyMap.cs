using System.Windows.Input;
using AccessibleMediaController.Core.Input;

namespace AccessibleMediaController.Windows.Services;

internal static class WindowsKeyMap
{
    public static bool TryGetVirtualKey(string keyName, out uint virtualKey)
    {
        var key = KeyChord.NormalizeKey(keyName);
        if (key.Length == 1)
        {
            var character = key[0];
            if (character is >= 'A' and <= 'Z')
            {
                virtualKey = character;
                return true;
            }
            if (character is >= '0' and <= '9')
            {
                virtualKey = character;
                return true;
            }
        }

        virtualKey = key switch
        {
            "Space" => 0x20,
            "Tab" => 0x09,
            "PageUp" => 0x21,
            "PageDown" => 0x22,
            "End" => 0x23,
            "Home" => 0x24,
            "Left" => 0x25,
            "Up" => 0x26,
            "Right" => 0x27,
            "Down" => 0x28,
            "Enter" => 0x0D,
            "Escape" => 0x1B,
            "Backspace" => 0x08,
            "Delete" => 0x2E,
            "Insert" => 0x2D,
            _ when key.StartsWith('F') && int.TryParse(key.AsSpan(1), out var number) && number is >= 1 and <= 24 => (uint)(0x6F + number),
            _ => 0
        };
        return virtualKey != 0;
    }

    public static string? FromVirtualKey(uint virtualKey)
    {
        if (virtualKey is >= 0x30 and <= 0x39) return ((char)virtualKey).ToString();
        if (virtualKey is >= 0x41 and <= 0x5A) return ((char)virtualKey).ToString();
        if (virtualKey is >= 0x70 and <= 0x87) return $"F{virtualKey - 0x6F}";
        return virtualKey switch
        {
            0x20 => "Space",
            0x09 => "Tab",
            0x21 => "PageUp",
            0x22 => "PageDown",
            0x23 => "End",
            0x24 => "Home",
            0x25 => "Left",
            0x26 => "Up",
            0x27 => "Right",
            0x28 => "Down",
            0x0D => "Enter",
            0x1B => "Escape",
            0x08 => "Backspace",
            0x2E => "Delete",
            0x2D => "Insert",
            _ => null
        };
    }

    public static KeyChord FromKeyEvent(KeyEventArgs eventArgs)
        => FromKeyEvent(eventArgs, Keyboard.Modifiers);

    public static KeyChord FromKeyEvent(KeyEventArgs eventArgs, ModifierKeys effectiveModifiers)
    {
        var key = eventArgs.Key == Key.System ? eventArgs.SystemKey : eventArgs.Key;
        var keyName = key switch
        {
            Key.Space => "Space",
            Key.Tab => "Tab",
            Key.Left => "Left",
            Key.Right => "Right",
            Key.Up => "Up",
            Key.Down => "Down",
            Key.PageUp => "PageUp",
            Key.PageDown => "PageDown",
            Key.Home => "Home",
            Key.End => "End",
            Key.Enter => "Enter",
            Key.Escape => "Escape",
            Key.Back => "Backspace",
            Key.Delete => "Delete",
            Key.Insert => "Insert",
            _ when key is >= Key.D0 and <= Key.D9 => ((int)key - (int)Key.D0).ToString(),
            _ when key is >= Key.NumPad0 and <= Key.NumPad9 => ((int)key - (int)Key.NumPad0).ToString(),
            _ => key.ToString()
        };

        var modifiers = KeyModifiers.None;
        if (effectiveModifiers.HasFlag(ModifierKeys.Control)) modifiers |= KeyModifiers.Ctrl;
        if (effectiveModifiers.HasFlag(ModifierKeys.Alt)) modifiers |= KeyModifiers.Alt;
        if (effectiveModifiers.HasFlag(ModifierKeys.Shift)) modifiers |= KeyModifiers.Shift;
        if (effectiveModifiers.HasFlag(ModifierKeys.Windows)) modifiers |= KeyModifiers.Windows;
        return new KeyChord(keyName, modifiers);
    }
}
