using System.Text.Json;

namespace AccessibleMediaController.LiteHost.Protocol;

/// <summary>Only commands for the official Apple Music Windows media session.</summary>
public static class LiteAppleMusicExternalContract
{
    public const string TransportOperation = "appleMusic.externalTransport";

    public static bool IsEnabledForProfile(string? profileId) =>
        string.Equals(profileId, "test", StringComparison.OrdinalIgnoreCase);

    public static string ReadTransportCommand(JsonElement args)
    {
        var command = LiteArgs.RequireText(args, "command").Trim();
        return command switch
        {
            "state" or "toggle" or "next" or "previous" or "stop" => command,
            _ => throw new LiteRequestException(
                "Nieznane polecenie sterowania oryginalnym Apple Music.")
        };
    }
}
