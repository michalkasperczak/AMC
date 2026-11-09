namespace AccessibleMediaController.LiteHost.Protocol;

/// <summary>Names of local Spotify resources owned by each release profile.</summary>
internal static class LiteSpotifyReleaseProfile
{
    internal const string TestCredentialTarget = "AccessibleMediaController/Spotify";
    internal const string PublicCredentialTarget = "AccessibleMediaController.Public/Spotify";

    internal static string CredentialTarget(string? profile) => Normalize(profile) switch
    {
        "test" => TestCredentialTarget,
        "public" => PublicCredentialTarget,
        _ => throw new ArgumentOutOfRangeException(nameof(profile))
    };

    internal static string ChromeDirectory(string? profile) => Normalize(profile) switch
    {
        "test" => "AMC Test",
        "public" => "AMC Public",
        _ => throw new ArgumentOutOfRangeException(nameof(profile))
    };

    private static string Normalize(string? profile) =>
        string.IsNullOrWhiteSpace(profile) ? "test" : profile.Trim().ToLowerInvariant();
}
