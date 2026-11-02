using System.Text.Json;
using AccessibleMediaController.Core.Sessions;

namespace AccessibleMediaController.LiteHost.Protocol;

/// <summary>
/// Wąski kontrakt przekazania jednego utworu lub odcinka do odtwarzacza Spotify.
/// Tokeny, prywatne źródła i dane procesu nie są częścią tego kontraktu.
/// </summary>
internal static class LiteSpotifyPlaybackContract
{
    internal const string PlayOperation = "spotify.play";

    internal static LiteSpotifyPlayRequest ReadPlay(JsonElement args)
    {
        var itemId = LiteArgs.RequireText(args, "itemId").Trim();
        if (itemId.Length == 0 || itemId.Length > 512 || itemId.Any(char.IsControl))
            throw new LiteRequestException("Element Spotify ma nieprawidłową tożsamość widoku.");

        var title = LiteSpotifyCatalogContract.CleanText(
            LiteArgs.RequireText(args, "title"));
        if (title.Length == 0)
            throw new LiteRequestException("Element Spotify nie ma nazwy do odtworzenia.");

        var kind = LiteArgs.RequireText(args, "kind").Trim().ToLowerInvariant() switch
        {
            "track" => MediaItemKind.Track,
            "episode" => MediaItemKind.Episode,
            _ => throw new LiteRequestException(
                "W Spotify można odtworzyć utwór albo odcinek podcastu.")
        };
        if (!LiteSpotifyCatalogContract.TryNormalizeExternalId(
                LiteArgs.RequireText(args, "externalId"), kind, out var externalId))
        {
            throw new LiteRequestException(
                "Element Spotify ma nieprawidłową tożsamość katalogową.");
        }

        var position = LiteArgs.ReadDouble(
            args, "positionSeconds", 0d, 0d, 86_400d);
        var volume = LiteArgs.ReadInt(args, "volume", 35, 0, 100);
        var playbackRate = LiteArgs.ReadDouble(
            args, "playbackRate", 1d, 0.5d, 2d);
        var serviceType = kind == MediaItemKind.Episode ? "episode" : "track";
        return new LiteSpotifyPlayRequest(
            new MediaItem
            {
                Id = itemId,
                Title = title,
                Artist = LiteSpotifyCatalogContract.CleanText(
                    LiteArgs.ReadText(args, "artist")),
                Kind = kind,
                ExternalId = externalId,
                Source = $"spotify:{serviceType}:{externalId}",
                IsAvailable = true
            },
            TimeSpan.FromSeconds(position),
            volume,
            playbackRate);
    }
}

internal sealed record LiteSpotifyPlayRequest(
    MediaItem Item,
    TimeSpan Position,
    int Volume,
    double PlaybackRate);
