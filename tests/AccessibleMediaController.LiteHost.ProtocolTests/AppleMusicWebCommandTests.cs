using AccessibleMediaController.AppleMusicWebHost;

namespace AccessibleMediaController.LiteHost.ProtocolTests;

internal static class AppleMusicWebCommandTests
{
    public static void Run()
    {
        var play = WebCommand.Parse("""
            {"id":1,"command":"play","songId":"1804690076"}
            """);
        if (play.Id != 1 || play.Command != "play" || play.SongId != "1804690076")
            throw new InvalidOperationException("Utracono identyfikator utworu Apple Music.");

        var seek = WebCommand.Parse("""
            {"id":2,"command":"seek","seconds":75.5}
            """);
        if (seek.Seconds != 75.5)
            throw new InvalidOperationException("Odrzucono prawidłowe przewijanie.");

        foreach (var invalid in new[]
        {
            """{"id":1,"command":"executeJavaScript","script":"alert(1)"}""",
            """{"id":1,"command":"play","songId":"library-private"}""",
            """{"id":1,"command":"play","songId":"https://music.apple.com"}""",
            """{"id":1,"command":"seek","seconds":-1}""",
            """{"id":1,"command":"seek","seconds":1e999}""",
            """{"id":-1,"command":"state"}""",
        })
        {
            try
            {
                WebCommand.Parse(invalid);
                throw new InvalidOperationException("Przyjęto niedozwolone polecenie Apple Music.");
            }
            catch (ArgumentException) { }
            catch (System.Text.Json.JsonException) { }
        }
    }
}
