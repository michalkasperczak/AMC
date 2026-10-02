using AccessibleMediaController.LiteHost.Protocol;

namespace AccessibleMediaController.LiteHost.ProtocolTests;

internal static class Assert
{
    public static void True(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("ZALOZENIE NIESPELNIONE: " + message);
    }

    public static void Equal(string? expected, string? actual, string message)
    {
        if (!string.Equals(expected, actual, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"ZALOZENIE NIESPELNIONE: {message}. Oczekiwano \"{expected}\", jest \"{actual}\".");
        }
    }
}

internal static class RequestParsingTests
{
    public static void Run()
    {
        ParsesValidRequest();
        BadJsonYieldsErrorNotException();
        MissingOpKeepsRequestId();
        OverlongLineIsRejectedBeforeParsing();
        Console.WriteLine("RequestParsingTests: OK");
    }

    private static void BadJsonYieldsErrorNotException()
    {
        var outcome = LiteRequestReader.Read("{to nie jest JSON");

        Assert.True(!outcome.IsRequest, "zepsuty wiersz nie moze dac zadania");
        Assert.Equal("bad_json", outcome.ErrorCode, "kod bledu zlego JSON-a");
        Assert.True(
            !string.IsNullOrWhiteSpace(outcome.ErrorMessage),
            "blad musi miec tresc komunikatu, nie sam typ");
    }

    private static void MissingOpKeepsRequestId()
    {
        // Frontend czeka na odpowiedz o tym samym id. Brak operacji nie moze
        // zgubic identyfikatora, bo zadanie wisialoby w nieskonczonosc.
        var outcome = LiteRequestReader.Read("""{"id":"r7"}""");

        Assert.True(!outcome.IsRequest, "wiersz bez op nie jest zadaniem");
        Assert.Equal("bad_request", outcome.ErrorCode, "kod bledu braku op");
        Assert.Equal("r7", outcome.RequestId, "identyfikator zachowany mimo bledu");
    }

    private static void OverlongLineIsRejectedBeforeParsing()
    {
        var line = "{\"id\":\"r1\",\"op\":\"x\",\"pad\":\""
            + new string('a', LiteRequestReader.MaximumLineLength)
            + "\"}";

        var outcome = LiteRequestReader.Read(line);

        Assert.Equal("line_too_long", outcome.ErrorCode, "zbyt dlugi wiersz odrzucony");
    }

    private static void ParsesValidRequest()
    {
        var outcome = LiteRequestReader.Read("""{"id":"r1","op":"transport.pause"}""");

        Assert.True(outcome.IsRequest, "poprawny wiersz ma dac zadanie");
        Assert.Equal("r1", outcome.Request!.Id, "identyfikator zadania");
        Assert.Equal("transport.pause", outcome.Request!.Op, "nazwa operacji");
    }
}
