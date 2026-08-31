namespace AccessibleMediaController.Core.Input;

public readonly record struct KeyChord(string Key, KeyModifiers Modifiers = KeyModifiers.None)
{
    public string Canonical => BuildCanonical(NormalizeKey(Key), Modifiers);

    public override string ToString() => Canonical;

    public static KeyChord Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new FormatException("Skrót nie może być pusty.");
        }

        var parts = value.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            throw new FormatException($"Nieprawidłowy skrót: {value}");
        }

        var modifiers = KeyModifiers.None;
        for (var index = 0; index < parts.Length - 1; index++)
        {
            modifiers |= parts[index].ToLowerInvariant() switch
            {
                "ctrl" or "control" => KeyModifiers.Ctrl,
                "alt" => KeyModifiers.Alt,
                "shift" => KeyModifiers.Shift,
                "win" or "windows" => KeyModifiers.Windows,
                _ => throw new FormatException($"Nieznany modyfikator: {parts[index]}")
            };
        }

        return new KeyChord(NormalizeKey(parts[^1]), modifiers);
    }

    public static string NormalizeKey(string key)
    {
        var trimmed = key.Trim();
        if (trimmed.Length == 1)
        {
            return trimmed.ToUpperInvariant();
        }

        var lower = trimmed.ToLowerInvariant();
        if (lower.StartsWith("numpad", StringComparison.Ordinal)
            && lower.Length == "numpad0".Length
            && char.IsAsciiDigit(lower[^1]))
        {
            return $"Numpad{lower[^1]}";
        }
        if (lower.Length == "0 numeryczny".Length
            && char.IsAsciiDigit(lower[0])
            && lower.EndsWith(" numeryczny", StringComparison.Ordinal))
        {
            return $"Numpad{lower[0]}";
        }

        return lower switch
        {
            "space" or "spacja" => "Space",
            "left" or "leftarrow" => "Left",
            "right" or "rightarrow" => "Right",
            "up" or "uparrow" => "Up",
            "down" or "downarrow" => "Down",
            "pageup" or "pgup" => "PageUp",
            "pagedown" or "pgdn" => "PageDown",
            "home" => "Home",
            "end" => "End",
            "enter" or "return" => "Enter",
            "numpadenter" or "numpad enter" or "numericenter" or "numeric enter"
                or "enter numeryczny" => "NumpadEnter",
            "numpadadd" or "numpad add" or "numericadd" or "numeric add"
                or "plus numeryczny" => "NumpadAdd",
            "numpadsubtract" or "numpad subtract" or "numericsubtract" or "numeric subtract"
                or "minus numeryczny" => "NumpadSubtract",
            "numpadmultiply" or "numpad multiply" or "numericmultiply" or "numeric multiply"
                or "gwiazdka numeryczna" or "mnożenie numeryczne" => "NumpadMultiply",
            "numpaddivide" or "numpad divide" or "numericdivide" or "numeric divide"
                or "ukośnik numeryczny" or "dzielenie numeryczne" => "NumpadDivide",
            "numpaddecimal" or "numpad decimal" or "numericdecimal" or "numeric decimal"
                or "kropka numeryczna" or "przecinek numeryczny" => "NumpadDecimal",
            "numpadseparator" or "numpad separator" or "separator numeryczny" => "NumpadSeparator",
            "numpadnumlock" or "numpad numlock" or "numlock" or "num lock"
                or "num lock numeryczny" => "NumpadNumLock",
            "numpadinsert" or "numpad insert" or "insert numeryczny" => "NumpadInsert",
            "numpaddelete" or "numpad delete" or "delete numeryczny" => "NumpadDelete",
            "numpadhome" or "numpad home" or "home numeryczny" => "NumpadHome",
            "numpadend" or "numpad end" or "end numeryczny" => "NumpadEnd",
            "numpadpageup" or "numpad pageup" or "page up numeryczny" => "NumpadPageUp",
            "numpadpagedown" or "numpad pagedown" or "page down numeryczny" => "NumpadPageDown",
            "numpadleft" or "numpad left" or "lewo numeryczne" => "NumpadLeft",
            "numpadright" or "numpad right" or "prawo numeryczne" => "NumpadRight",
            "numpadup" or "numpad up" or "góra numeryczna" => "NumpadUp",
            "numpaddown" or "numpad down" or "dół numeryczny" => "NumpadDown",
            "numpadclear" or "numpad clear" or "clear numeryczny" => "NumpadClear",
            "escape" or "esc" => "Escape",
            "backspace" => "Backspace",
            "delete" or "del" => "Delete",
            _ when trimmed.StartsWith('F') && int.TryParse(trimmed[1..], out _) => trimmed.ToUpperInvariant(),
            _ => char.ToUpperInvariant(trimmed[0]) + trimmed[1..]
        };
    }

    private static string BuildCanonical(string key, KeyModifiers modifiers)
    {
        var parts = new List<string>(5);
        if (modifiers.HasFlag(KeyModifiers.Ctrl)) parts.Add("Ctrl");
        if (modifiers.HasFlag(KeyModifiers.Alt)) parts.Add("Alt");
        if (modifiers.HasFlag(KeyModifiers.Shift)) parts.Add("Shift");
        if (modifiers.HasFlag(KeyModifiers.Windows)) parts.Add("Windows");
        parts.Add(key);
        return string.Join('+', parts);
    }
}
