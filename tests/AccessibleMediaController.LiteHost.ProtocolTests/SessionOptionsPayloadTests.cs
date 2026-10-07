using System.Text.Json;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.LiteHost.Protocol;
namespace AccessibleMediaController.LiteHost.ProtocolTests;

/// <summary>
/// Opcje sesji z portu Python czytane PRAWDZIWYM parserem protokolu.
///
/// Testy po stronie Pythona uzywaja atrapy klienta, wiec nie dowodza, ze
/// host przyjmie te pola. Ten zestaw bierze payload dokladnie w takiej
/// postaci, jaka buduje <c>session_options.engine_payload</c>, i podaje go
/// do <see cref="LiteAudioSettings.Read"/> -- tego samego kodu, ktorego
/// uzywa dispatch <c>audio.configure</c>.
/// </summary>
internal static class SessionOptionsPayloadTests
{
    public static void Run()
    {
        // Pliki lokalne: wszystkie cztery pola, ktore port wysyla.
        using var files = JsonDocument.Parse(
            "{\"tempoAlgorithm\":1,\"loudnessNormalization\":true,"
            + "\"smoothTrackTransitions\":false,\"interTrackSilenceMs\":2000}");
        var parsed = LiteAudioSettings.Read(files.RootElement);
        Assert.True(parsed.LoudnessNormalizationEnabled,
            "wybrana normalizacja dochodzi do modelu silnika");
        Assert.True(!parsed.SmoothTrackTransitionsEnabled,
            "niewybrane przejscia zostaja wylaczone, nie 'jakies'");
        Assert.True(parsed.InterTrackSilenceMilliseconds == 2000,
            "wybrana dlugosc ciszy dochodzi bez przyciecia");
        Assert.True(parsed.TempoAlgorithm == PlaybackTempoAlgorithm.Speech,
            "algorytm tempa z sesji nie gubi sie po drodze");

        // Radio: port NIE wysyla pol przetwarzania, bo silnik ich nie wykonuje.
        // Taki payload musi byc dla hosta poprawny, a nie 'brakujacy'.
        using var radio = JsonDocument.Parse("{\"tempoAlgorithm\":2}");
        var radioParsed = LiteAudioSettings.Read(radio.RootElement);
        Assert.True(radioParsed.TempoAlgorithm == PlaybackTempoAlgorithm.Music,
            "payload Radia bez pol przetwarzania jest poprawny dla hosta");
        Assert.True(radioParsed.InterTrackSilenceMilliseconds == 0,
            "brak pola ciszy nie wymysla ciszy w silniku");

        // Kazda dlugosc, ktora dialog proponuje, musi przejsc parser. Gdyby
        // port zaoferowal inna, uzytkownik dostalby odmowe po 'Zapisz'.
        foreach (var ms in PlaybackAudioSettingsRules.SupportedInterTrackSilenceMilliseconds)
        {
            using var input = JsonDocument.Parse("{\"interTrackSilenceMs\":" + ms + "}");
            Assert.True(LiteAudioSettings.Read(input.RootElement).InterTrackSilenceMilliseconds == ms,
                "dlugosc ciszy oferowana w dialogu jest przyjmowana przez host");
        }

        ReadPayloadsProducedByThePythonPort();

        Console.WriteLine("SessionOptionsPayloadTests: OK");
    }

    /// <summary>
    /// Czyta payloady WYGENEROWANE przez port Python i podaje je parserowi.
    ///
    /// Powyzsze przypadki maja JSON wpisany recznie, wiec dowodza tylko
    /// tego, ze host rozumie oczekiwany ksztalt. Tutaj plik powstaje z
    /// <c>session_options.engine_payload</c>, wiec rozjazd miedzy portem a
    /// protokolem wychodzi jako czerwony test, a nie jako cicha odmowa u
    /// uzytkownika po "Zapisz".
    ///
    /// Gdy pliku nie ma (uruchomienie bez kroku Pythona), zestaw NIE udaje
    /// zaliczenia -- mowi wprost, ze pominal sprawdzenie.
    /// </summary>
    private static void ReadPayloadsProducedByThePythonPort()
    {
        var path = Environment.GetEnvironmentVariable("AMC_SESSION_OPTIONS_PAYLOADS");
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            Console.WriteLine("  (pominieto: brak payloadow z portu Python -- "
                + "ustaw AMC_SESSION_OPTIONS_PAYLOADS na plik z wxlite)");
            return;
        }

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var count = 0;
        foreach (var entry in document.RootElement.EnumerateArray())
        {
            var label = entry.GetProperty("label").GetString() ?? "(bez nazwy)";
            var payload = entry.GetProperty("payload");
            // Parser rzuca LiteRequestException na kazde pole, ktorego nie
            // przyjmuje. Brak wyjatku = port nie wysyla nic nieobslugiwanego.
            var settings = LiteAudioSettings.Read(payload);
            Assert.True(
                PlaybackAudioSettingsRules.IsSupportedSilence(
                    settings.InterTrackSilenceMilliseconds),
                $"payload portu '{label}' daje dlugosc ciszy obslugiwana przez silnik");
            count++;
        }
        Assert.True(count > 0, "plik payloadow z portu nie moze byc pusty");
        Console.WriteLine($"  (payloady z portu Python przyjete parserem: {count})");
    }
}
