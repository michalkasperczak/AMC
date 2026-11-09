using AccessibleMediaController.LiteHost.Protocol;

namespace AccessibleMediaController.LiteHost.ProtocolTests;

internal static class SpotifyReleaseProfileTests
{
    internal static void Run()
    {
        if (LiteSpotifyReleaseProfile.CredentialTarget("test") !=
            "AccessibleMediaController/Spotify")
            throw new Exception("Test lost its existing Spotify credential target.");
        if (LiteSpotifyReleaseProfile.CredentialTarget("public") ==
            LiteSpotifyReleaseProfile.CredentialTarget("test"))
            throw new Exception("Public and Test share Spotify credentials.");
        if (LiteSpotifyReleaseProfile.ChromeDirectory("public") ==
            LiteSpotifyReleaseProfile.ChromeDirectory("test"))
            throw new Exception("Public and Test share their Chrome profile.");
        if (LiteSpotifyReleaseProfile.ChromeDirectory(null) != "AMC Test")
            throw new Exception("Legacy Test default changed.");
        try
        {
            LiteSpotifyReleaseProfile.CredentialTarget("other");
            throw new Exception("Unknown release profile was accepted.");
        }
        catch (ArgumentOutOfRangeException) { }
    }
}
