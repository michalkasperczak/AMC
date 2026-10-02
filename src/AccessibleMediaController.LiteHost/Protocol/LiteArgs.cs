using System.Text.Json;

namespace AccessibleMediaController.LiteHost.Protocol;

/// <summary>
/// Argumenty zadania czytane JAWNIE, z granicami. Nic tu nie siega do
/// systemu: ta klasa jest w czesci protokolu, zeby dala sie przetestowac
/// w WSL bez Windows.
/// </summary>
public static class LiteArgs
{
    public const int MaximumPathLength = 4_096;
    public const int MaximumTextLength = 4_096;

    public static string RequireText(JsonElement args, string name)
    {
        var value = ReadText(args, name);
        if (value is null) throw new LiteRequestException($"Brak wymaganego argumentu \"{name}\".");
        return value;
    }

    public static string? ReadText(JsonElement args, string name)
    {
        if (args.ValueKind != JsonValueKind.Object) return null;
        if (!args.TryGetProperty(name, out var property)) return null;
        if (property.ValueKind != JsonValueKind.String) return null;
        var text = property.GetString();
        if (string.IsNullOrWhiteSpace(text)) return null;
        if (text.Length > MaximumTextLength)
        {
            throw new LiteRequestException($"Argument \"{name}\" jest zbyt dlugi.");
        }
        return text;
    }

    public static int ReadInt(JsonElement args, string name, int fallback, int minimum, int maximum)
    {
        if (args.ValueKind != JsonValueKind.Object) return fallback;
        if (!args.TryGetProperty(name, out var property)) return fallback;
        if (property.ValueKind != JsonValueKind.Number || !property.TryGetInt32(out var value)) return fallback;
        return Math.Clamp(value, minimum, maximum);
    }

    public static double ReadDouble(JsonElement args, string name, double fallback, double minimum, double maximum)
    {
        if (args.ValueKind != JsonValueKind.Object) return fallback;
        if (!args.TryGetProperty(name, out var property)) return fallback;
        if (property.ValueKind != JsonValueKind.Number || !property.TryGetDouble(out var value)) return fallback;
        return Math.Clamp(value, minimum, maximum);
    }

    public static bool ReadBool(JsonElement args, string name, bool fallback)
    {
        if (args.ValueKind != JsonValueKind.Object) return fallback;
        if (!args.TryGetProperty(name, out var property)) return fallback;
        return property.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => fallback
        };
    }

    /// <summary>
    /// Sciezka systemu plikow. Odrzucamy puste, zbyt dlugie i zawierajace
    /// znaki sterujace; NIE pozwalamy na przekazanie dowolnej komendy.
    /// </summary>
    public static string RequirePath(JsonElement args, string name)
    {
        var value = RequireText(args, name);
        if (value.Length > MaximumPathLength)
        {
            throw new LiteRequestException($"Sciezka w \"{name}\" jest zbyt dluga.");
        }
        if (value.Any(char.IsControl))
        {
            throw new LiteRequestException($"Sciezka w \"{name}\" zawiera znaki sterujace.");
        }
        return value;
    }
}

/// <summary>
/// Blad TRESCI zadania (zly argument, brak pliku). Daje odpowiedz bledu
/// z komunikatem dla uzytkownika, nie awarie hosta.
/// </summary>
public sealed class LiteRequestException(string message) : Exception(message);
