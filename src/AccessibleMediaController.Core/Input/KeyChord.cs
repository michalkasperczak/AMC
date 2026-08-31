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

        return trimmed.ToLowerInvariant() switch
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
