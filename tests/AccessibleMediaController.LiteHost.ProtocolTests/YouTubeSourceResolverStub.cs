namespace AccessibleMediaController.Windows.Services;

/// <summary>
/// Minimalna atrapa zależności procesu yt-dlp dla testów samego protokołu.
/// Testy protokołu nie uruchamiają narzędzi zewnętrznych ani kodu Windows.
/// </summary>
internal static class YouTubeSourceResolver
{
    internal static bool IsYouTubeUrl(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri.Scheme is "http" or "https"
        && (uri.Host.Equals("youtube.com", StringComparison.OrdinalIgnoreCase)
            || uri.Host.EndsWith(".youtube.com", StringComparison.OrdinalIgnoreCase)
            || uri.Host.Equals("youtu.be", StringComparison.OrdinalIgnoreCase));

    internal static string? TryBuildChannelLiveUrl(string? channelId) =>
        !string.IsNullOrWhiteSpace(channelId)
        && channelId.Length == 24
        && channelId.StartsWith("UC", StringComparison.Ordinal)
            ? $"https://www.youtube.com/channel/{channelId}/live"
            : null;

    internal static Task<string?> TryResolveChannelLiveUrlAsync(
        string executable,
        string expiredVideoUrl,
        CancellationToken cancellationToken) => Task.FromResult<string?>(null);

    internal static string? FindExecutable() => null;
}
