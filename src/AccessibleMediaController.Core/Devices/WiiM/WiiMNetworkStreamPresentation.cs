using AccessibleMediaController.Core.Configuration;

namespace AccessibleMediaController.Core.Devices.WiiM;

public static class WiiMNetworkStreamPresentation
{
    public static string? ResolveDisplayName(
        IEnumerable<WiiMNetworkStreamSettings> streams,
        string? rememberedStreamId,
        string? snapshotContentUri)
    {
        ArgumentNullException.ThrowIfNull(streams);
        var available = streams.ToArray();
        if (!string.IsNullOrWhiteSpace(rememberedStreamId))
        {
            var remembered = available.FirstOrDefault(stream => string.Equals(
                stream.Id,
                rememberedStreamId,
                StringComparison.OrdinalIgnoreCase));
            if (remembered is not null) return UserFacingText(remembered.Name);
        }

        if (!WiiMPlaybackUriPolicy.TryNormalize(snapshotContentUri, out var normalizedUri)) return null;
        var matched = available.FirstOrDefault(stream =>
            WiiMPlaybackUriPolicy.TryNormalize(stream.StreamUrl, out var candidateUri)
            && string.Equals(candidateUri, normalizedUri, StringComparison.OrdinalIgnoreCase));
        return matched is null ? null : UserFacingText(matched.Name);
    }

    public static string? UsefulMetadataText(string? value)
    {
        var text = UserFacingText(value);
        if (text is null) return null;
        if (Uri.TryCreate(text, UriKind.Absolute, out var uri)
            && (text.Contains("://", StringComparison.Ordinal)
                || uri.IsFile
                || uri.Scheme.Equals("mailto", StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }
        if (text.StartsWith("www.", StringComparison.OrdinalIgnoreCase)) return null;

        var path = text.Split(['?', '#'], 2)[0];
        var extension = Path.GetExtension(path);
        return extension.Equals(".m3u", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".m3u8", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".pls", StringComparison.OrdinalIgnoreCase)
                ? null
                : text;
    }

    private static string? UserFacingText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
