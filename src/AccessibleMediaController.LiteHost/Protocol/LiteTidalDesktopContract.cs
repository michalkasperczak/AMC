using System.Text.Json;
using AccessibleMediaController.Core.Tidal;

namespace AccessibleMediaController.LiteHost.Protocol;

/// <summary>
/// Waski kontrakt sterowania ORYGINALNA aplikacja TIDAL. Nie przyjmuje tokenu,
/// adresu wykonywalnego ani kodu JavaScript: frontend przekazuje jedynie
/// tozsamosc katalogowa wybranego utworu lub kontenera, a sprawdzony plan
/// sklada Core AMC.
/// </summary>
public static class LiteTidalDesktopContract
{
    public const string PlayOperation = "tidal.desktopPlay";
    public const string TransportOperation = "tidal.externalTransport";

    public static LiteTidalDesktopPlayCommand ReadPlayRequest(JsonElement args)
    {
        var itemId = LiteArgs.RequireText(args, "itemId").Trim();
        var externalId = LiteArgs.RequireText(args, "externalId").Trim();
        var title = LiteArgs.RequireText(args, "title").Trim();
        var kind = LiteArgs.ReadText(args, "kind")?.Trim() ?? "track";

        if (kind is "album" or "playlist")
        {
            var prefix = kind == "playlist" ? "playlists:" : "albums:";
            if (!HasTypedId(externalId, prefix))
                throw new LiteRequestException(
                    "Ten element ma nieprawidłową tożsamość katalogową TIDAL.");

            if (!TidalDesktopPlaybackPlan.TryBuildRequest(
                    TidalDesktopPlayKind.Container,
                    trackTitle: null,
                    albumExternalId: null,
                    containerExternalId: externalId,
                    containerIsPlaylist: kind == "playlist",
                    displayName: title,
                    out var containerRequest,
                    out var containerReason))
            {
                throw new LiteRequestException(
                    containerReason ?? "Nie da się przekazać tego elementu oryginalnemu TIDALowi.");
            }

            return new LiteTidalDesktopPlayCommand(
                itemId,
                externalId,
                containerRequest!,
                LiteArgs.ReadBool(args, "restartConsent", false));
        }
        if (kind != "track")
            throw new LiteRequestException("Nieznany rodzaj elementu TIDAL.");

        var albumExternalId = LiteArgs.RequireText(
            args, "relatedAlbumExternalId").Trim();

        if (!HasTypedId(externalId, "tracks:"))
            throw new LiteRequestException(
                "Ten utwór ma nieprawidłową tożsamość katalogową TIDAL.");
        if (!HasTypedId(albumExternalId, "albums:"))
            throw new LiteRequestException(
                "Ten utwór nie ma prawidłowo przypisanego albumu TIDAL.");

        if (!TidalDesktopPlaybackPlan.TryBuildRequest(
                TidalDesktopPlayKind.Track,
                title,
                albumExternalId,
                containerExternalId: null,
                containerIsPlaylist: false,
                displayName: title,
                out var request,
                out var reason))
        {
            throw new LiteRequestException(
                reason ?? "Nie da się przekazać tego utworu oryginalnemu TIDALowi.");
        }

        return new LiteTidalDesktopPlayCommand(
            itemId,
            externalId,
            request!,
            LiteArgs.ReadBool(args, "restartConsent", false));
    }

    public static string ReadTransportCommand(JsonElement args)
    {
        var command = LiteArgs.RequireText(args, "command").Trim();
        return command switch
        {
            "toggle" or "next" or "previous" or "state" => command,
            _ => throw new LiteRequestException(
                "Nieznane polecenie sterowania oryginalnym TIDALem.")
        };
    }

    private static bool HasTypedId(string value, string prefix) =>
        value.StartsWith(prefix, StringComparison.Ordinal)
        && value.Length > prefix.Length;
}

public sealed record LiteTidalDesktopPlayCommand(
    string ItemId,
    string ExternalId,
    TidalDesktopPlayRequest Request,
    bool RestartConsent);
