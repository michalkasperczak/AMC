using AccessibleMediaController.Windows.Services;

internal static class DiagnosticLogRedactionTests
{
    internal static void Run()
    {
        const string remote = "https://user:path-secret@audio.example.test/signed-path-secret/track.flac?Signature=query-secret#fragment-secret";
        var local = @"C:\Muzyka\Koncert.flac";
        var redacted = DiagnosticLog.RedactSensitiveData($"Źródło: {remote}; lokalny: {local}");
        Assert(redacted.Contains("https://audio.example.test/[adres sieciowy]", StringComparison.Ordinal),
            "Adres sieciowy powinien zachować wyłącznie host i etykietę.");
        Assert(redacted.Contains(local, StringComparison.Ordinal),
            "Redakcja nie powinna zmieniać lokalnej ścieżki pliku.");
        AssertNoSecrets(redacted, "path-secret", "signed-path-secret", "query-secret", "fragment-secret");

        var credentials = DiagnosticLog.RedactSensitiveData(
            "Authorization: Bearer header-secret\n" +
            "refresh_token=refresh-secret; x-tidal-token: client-secret; " +
            "{\"access_token\":\"json-secret\"}");
        AssertNoSecrets(credentials, "header-secret", "refresh-secret", "client-secret", "json-secret");
        Assert(credentials.Contains("refresh_token=[ukryto]", StringComparison.Ordinal),
            "W diagnostyce powinien pozostać rodzaj zredagowanego parametru.");

        var root = Path.Combine(Path.GetTempPath(), "amc-diagnostic-redaction-" + Guid.NewGuid().ToString("N"));
        try
        {
            DiagnosticLog.Initialize(root);
            DiagnosticLog.Info("playback", $"Źródło: {remote}; lokalny: {local}");
            DiagnosticLog.Error("tidal", "Nie otwarto utworu.",
                new HttpRequestException($"Żądanie {remote} odrzucone. token=exception-secret"));
            DiagnosticLog.Shutdown();

            var contents = File.ReadAllText(Path.Combine(root, "amc.log"));
            Assert(contents.Contains("https://audio.example.test/[adres sieciowy]", StringComparison.Ordinal),
                "Redakcja nie dotarła do zapisanego logu.");
            Assert(contents.Contains(local, StringComparison.Ordinal),
                "Zapisany log utracił lokalną ścieżkę.");
            AssertNoSecrets(contents, "path-secret", "signed-path-secret", "query-secret",
                "fragment-secret", "exception-secret");
        }
        finally
        {
            DiagnosticLog.Shutdown();
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }

        Console.WriteLine("OK: diagnostyka redaguje zdalne adresy i poświadczenia, zachowuje lokalne ścieżki");
    }

    private static void AssertNoSecrets(string actual, params string[] secrets)
    {
        foreach (var secret in secrets)
            Assert(!actual.Contains(secret, StringComparison.Ordinal),
                $"Diagnostyka ujawniła syntetyczny sekret: {secret}.");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
