using System.Text.Json;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.LiteHost.Protocol;
namespace AccessibleMediaController.LiteHost.ProtocolTests;

internal static class AudioConfigurationTests
{
    public static void Run()
    {
        foreach (var value in new[] { 0, 1, 2 })
        {
            using var input = JsonDocument.Parse("{\"tempoAlgorithm\":" + value + "}");
            var parsed = LiteAudioSettings.Read(input.RootElement);
            Assert.True((int)parsed.TempoAlgorithm == value, "wybrany rzeczywisty algorytm przechodzi do modelu Core");
        }
        using var empty = JsonDocument.Parse("{}");
        Assert.True(LiteAudioSettings.Read(empty.RootElement).TempoAlgorithm == PlaybackTempoAlgorithm.SoundTouch,
            "brak wyboru nie zmienia dotychczasowego ustawienia protokolu");
        foreach (var value in new[] { "3", "-1", "true", "null", "\"Speech\"", "1.5" })
        {
            using var input = JsonDocument.Parse("{\"tempoAlgorithm\":" + value + "}");
            var refused = false;
            try { LiteAudioSettings.Read(input.RootElement); }
            catch (LiteRequestException) { refused = true; }
            Assert.True(refused, "nieprawidłowy algorytm odrzucony, nie przycinany do innego");
        }
        Console.WriteLine("AudioConfigurationTests: OK");
    }
}
