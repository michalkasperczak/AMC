using System.Windows.Input;
using AccessibleMediaController.Core.Input;

namespace AccessibleMediaController.Windows.Services;

internal static class WindowsKeyMap
{
    public const string NumpadEnterKey = "NumpadEnter";

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
            "Numpad0" => 0x60,
            "Numpad1" => 0x61,
            "Numpad2" => 0x62,
            "Numpad3" => 0x63,
            "Numpad4" => 0x64,
            "Numpad5" => 0x65,
            "Numpad6" => 0x66,
            "Numpad7" => 0x67,
            "Numpad8" => 0x68,
            "Numpad9" => 0x69,
            "NumpadMultiply" => 0x6A,
            "NumpadAdd" => 0x6B,
            "NumpadSeparator" => 0x6C,
            "NumpadSubtract" => 0x6D,
            "NumpadDecimal" => 0x6E,
            "NumpadDivide" => 0x6F,
            "NumpadNumLock" => 0x90,
            "NumpadEnter" => 0x0D,
            "NumpadInsert" => 0x2D,
            "NumpadDelete" => 0x2E,
            "NumpadHome" => 0x24,
            "NumpadEnd" => 0x23,
            "NumpadPageUp" => 0x21,
            "NumpadPageDown" => 0x22,
            "NumpadLeft" => 0x25,
            "NumpadRight" => 0x27,
            "NumpadUp" => 0x26,
            "NumpadDown" => 0x28,
            "NumpadClear" => 0x0C,
            _ when key.StartsWith('F') && int.TryParse(key.AsSpan(1), out var number) && number is >= 1 and <= 24 => (uint)(0x6F + number),
            _ => 0
        };
        return virtualKey != 0;
    }

    public static string? FromVirtualKey(uint virtualKey)
    {
        if (virtualKey is >= 0x30 and <= 0x39) return ((char)virtualKey).ToString();
        if (virtualKey is >= 0x41 and <= 0x5A) return ((char)virtualKey).ToString();
        if (virtualKey is >= 0x60 and <= 0x69) return $"Numpad{virtualKey - 0x60}";
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
            0x6A => "NumpadMultiply",
            0x6B => "NumpadAdd",
            0x6C => "NumpadSeparator",
            0x6D => "NumpadSubtract",
            0x6E => "NumpadDecimal",
            0x6F => "NumpadDivide",
            0x90 => "NumpadNumLock",
            _ => null
        };
    }

    public static string? FromKeyboardInput(uint virtualKey, bool extended)
    {
        if (virtualKey == 0x0D && extended) return NumpadEnterKey;
        if (!extended)
        {
            var navigationKey = virtualKey switch
            {
                0x2D => "NumpadInsert",
                0x2E => "NumpadDelete",
                0x24 => "NumpadHome",
                0x23 => "NumpadEnd",
                0x21 => "NumpadPageUp",
                0x22 => "NumpadPageDown",
                0x25 => "NumpadLeft",
                0x27 => "NumpadRight",
                0x26 => "NumpadUp",
                0x28 => "NumpadDown",
                0x0C => "NumpadClear",
                _ => null
            };
            if (navigationKey is not null) return navigationKey;
        }
        return FromVirtualKey(virtualKey);
    }

    public static bool TryGetExactNumpadKey(
        uint virtualKey,
        bool extended,
        out string keyName)
    {
        var candidate = FromKeyboardInput(virtualKey, extended);
        if (candidate?.StartsWith("Numpad", StringComparison.Ordinal) == true)
        {
            keyName = candidate;
            return true;
        }
        keyName = string.Empty;
        return false;
    }

    public static bool RequiresExactNumpadHook(string keyName) =>
        KeyChord.NormalizeKey(keyName) is
            "NumpadEnter" or
            "NumpadInsert" or
            "NumpadDelete" or
            "NumpadHome" or
            "NumpadEnd" or
            "NumpadPageUp" or
            "NumpadPageDown" or
            "NumpadLeft" or
            "NumpadRight" or
            "NumpadUp" or
            "NumpadDown" or
            "NumpadClear";

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
            _ when key is >= Key.NumPad0 and <= Key.NumPad9 => $"Numpad{(int)key - (int)Key.NumPad0}",
            Key.Multiply => "NumpadMultiply",
            Key.Add => "NumpadAdd",
            Key.Separator => "NumpadSeparator",
            Key.Subtract => "NumpadSubtract",
            Key.Decimal => "NumpadDecimal",
            Key.Divide => "NumpadDivide",
            Key.NumLock => "NumpadNumLock",
            _ => key.ToString()
        };

        return new KeyChord(keyName, FromModifierKeys(effectiveModifiers));
    }

    public static KeyModifiers FromModifierKeys(ModifierKeys modifiers)
    {
        var result = KeyModifiers.None;
        if (modifiers.HasFlag(ModifierKeys.Control)) result |= KeyModifiers.Ctrl;
        if (modifiers.HasFlag(ModifierKeys.Alt)) result |= KeyModifiers.Alt;
        if (modifiers.HasFlag(ModifierKeys.Shift)) result |= KeyModifiers.Shift;
        if (modifiers.HasFlag(ModifierKeys.Windows)) result |= KeyModifiers.Windows;
        return result;
    }

    public static string ToDisplayText(KeyChord chord)
    {
        var parts = new List<string>(5);
        if (chord.Modifiers.HasFlag(KeyModifiers.Ctrl)) parts.Add("Ctrl");
        if (chord.Modifiers.HasFlag(KeyModifiers.Alt)) parts.Add("Alt");
        if (chord.Modifiers.HasFlag(KeyModifiers.Shift)) parts.Add("Shift");
        if (chord.Modifiers.HasFlag(KeyModifiers.Windows)) parts.Add("Windows");
        var key = KeyChord.NormalizeKey(chord.Key);
        parts.Add(NumpadDisplayText(key));
        return string.Join('+', parts);
    }

    private static string NumpadDisplayText(string key)
    {
        if (key.StartsWith("Numpad", StringComparison.Ordinal)
            && key.Length == "Numpad0".Length
            && char.IsAsciiDigit(key[^1]))
        {
            return $"{key[^1]} numeryczny";
        }
        return key switch
        {
            "NumpadEnter" => "Enter numeryczny",
            "NumpadMultiply" => "Gwiazdka numeryczna",
            "NumpadAdd" => "Plus numeryczny",
            "NumpadSeparator" => "Separator numeryczny",
            "NumpadSubtract" => "Minus numeryczny",
            "NumpadDecimal" => "Kropka numeryczna",
            "NumpadDivide" => "Ukośnik numeryczny",
            "NumpadNumLock" => "Num Lock",
            "NumpadInsert" => "Insert numeryczny",
            "NumpadDelete" => "Delete numeryczny",
            "NumpadHome" => "Home numeryczny",
            "NumpadEnd" => "End numeryczny",
            "NumpadPageUp" => "Page Up numeryczny",
            "NumpadPageDown" => "Page Down numeryczny",
            "NumpadLeft" => "Strzałka w lewo numeryczna",
            "NumpadRight" => "Strzałka w prawo numeryczna",
            "NumpadUp" => "Strzałka w górę numeryczna",
            "NumpadDown" => "Strzałka w dół numeryczna",
            "NumpadClear" => "5 numeryczny przy wyłączonym Num Lock",
            _ => key
        };
    }
}
