using System.Text.Json;
using AccessibleMediaController.LiteHost.Protocol;

namespace AccessibleMediaController.LiteHost.ProtocolTests;

internal static class ArgumentReadingTests
{
    public static void Run()
    {
        MissingRequiredTextIsRequestError();
        NumbersAreClampedToSafeRange();
        ControlCharactersInPathAreRejected();
        AbsentOptionalArgumentFallsBack();
        Console.WriteLine("ArgumentReadingTests: OK");
    }

    private static JsonElement Parse(string json) =>
        JsonDocument.Parse(json).RootElement.Clone();

    private static void MissingRequiredTextIsRequestError()
    {
        try
        {
            LiteArgs.RequireText(Parse("""{"inne":"x"}"""), "path");
            throw new InvalidOperationException("ZALOZENIE NIESPELNIONE: brak argumentu musi dac blad.");
        }
        catch (LiteRequestException exception)
        {
            Assert.True(
                exception.Message.Contains("path", StringComparison.Ordinal),
                "komunikat musi nazwac BRAKUJACY argument");
        }
    }

    private static void NumbersAreClampedToSafeRange()
    {
        // Frontend moze przyslac bledna wartosc. Host nie ma prawa podac
        // silnikowi glosnosci 5000 ani tempa 0.
        Assert.True(
            LiteArgs.ReadInt(Parse("""{"volume":5000}"""), "volume", 35, 0, 100) == 100,
            "glosnosc przycieta do 100");
        Assert.True(
            Math.Abs(LiteArgs.ReadDouble(Parse("""{"rate":0}"""), "rate", 1d, 0.5d, 2d) - 0.5d) < 1e-9,
            "tempo przyciete do dolnej granicy 0.5");
    }

    private static void ControlCharactersInPathAreRejected()
    {
        try
        {
            LiteArgs.RequirePath(Parse("""{"path":"C:\\muzyka\u0007\\plik.mp3"}"""), "path");
            throw new InvalidOperationException("ZALOZENIE NIESPELNIONE: znak sterujacy musi byc odrzucony.");
        }
        catch (LiteRequestException)
        {
        }
    }

    private static void AbsentOptionalArgumentFallsBack()
    {
        Assert.True(
            LiteArgs.ReadBool(Parse("""{}"""), "audible", true),
            "brak argumentu daje wartosc zapasowa");
        Assert.True(
            LiteArgs.ReadText(Parse("""{"name":"   "}"""), "name") is null,
            "same biale znaki to brak wartosci");
    }
}
