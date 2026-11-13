using System.Text.Json;
using System.Text.RegularExpressions;

namespace AccessibleMediaController.AppleMusicWebHost;

internal static class WebCommand
{
    private static readonly Regex SongId = new("^[0-9]{1,20}$", RegexOptions.Compiled);
    private static readonly HashSet<string> Commands = new(StringComparer.Ordinal)
    {
        "play", "state", "toggle", "stop", "next", "previous", "seek", "show"
    };

    public static Request Parse(string line)
    {
        if (line.Length > 4096)
            throw new ArgumentException("Zbyt długie polecenie Apple Music.");
        using var document = JsonDocument.Parse(line);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.Number ||
            !id.TryGetInt32(out var number) || number < 0 ||
            !root.TryGetProperty("command", out var commandValue) ||
            commandValue.ValueKind != JsonValueKind.String)
            throw new ArgumentException("Nieprawidłowe polecenie Apple Music.");
        var command = commandValue.GetString() ?? "";
        if (!Commands.Contains(command))
            throw new ArgumentException("Nieznane polecenie Apple Music.");
        string? songId = null;
        if (command == "play")
        {
            if (!root.TryGetProperty("songId", out var song) ||
                song.ValueKind != JsonValueKind.String ||
                !SongId.IsMatch(song.GetString() ?? ""))
                throw new ArgumentException("Nieprawidłowy utwór Apple Music.");
            songId = song.GetString();
        }
        double? seconds = null;
        if (command == "seek")
        {
            if (!root.TryGetProperty("seconds", out var value) ||
                value.ValueKind != JsonValueKind.Number ||
                !value.TryGetDouble(out var parsed) ||
                !double.IsFinite(parsed) || parsed < 0 || parsed > 86400)
                throw new ArgumentException("Nieprawidłowa pozycja utworu.");
            seconds = parsed;
        }
        return new Request(number, command, songId, seconds);
    }
}

internal sealed record Request(int Id, string Command, string? SongId, double? Seconds);
