using System.Text;
using System.Text.Json;
using AccessibleMediaController.LiteHost.Protocol;

namespace AccessibleMediaController.LiteHost.ProtocolTests;

internal static class DispatchLoopTests
{
    public static void Run()
    {
        BadJsonDoesNotStopTheLoop();
        UnknownOperationAnswersWithError();
        EndOfInputEndsTheLoop();
        StdoutCarriesOnlyProtocolLines();
        EventsAreWrittenAsOwnLines();
        HandlerFailureBecomesResponseNotCrash();
        Console.WriteLine("DispatchLoopTests: OK");
    }

    private static (IReadOnlyList<JsonDocument> Lines, int Exit) RunLoop(
        string input,
        IReadOnlyDictionary<string, Func<LiteRequest, LiteEventSink, object?>> handlers)
    {
        using var reader = new StringReader(input);
        var output = new StringWriter();
        var loop = new LiteDispatchLoop(handlers);
        var exit = loop.Run(reader, output);
        var lines = output.ToString()
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => JsonDocument.Parse(line.TrimEnd('\r')))
            .ToArray();
        return (lines, exit);
    }

    private static readonly Dictionary<string, Func<LiteRequest, LiteEventSink, object?>> Echo =
        new(StringComparer.Ordinal)
        {
            ["echo"] = static (request, _) => new { ok = true, op = request.Op }
        };

    private static void BadJsonDoesNotStopTheLoop()
    {
        var (lines, exit) = RunLoop(
            "{zepsuty\n{\"id\":\"a\",\"op\":\"echo\"}\n",
            Echo);

        Assert.True(lines.Count == 2, $"oczekiwano 2 wierszy wyjscia, jest {lines.Count}");
        Assert.Equal("bad_json", lines[0].RootElement.GetProperty("error").GetProperty("code").GetString(),
            "pierwszy wiersz to blad zlego JSON-a");
        Assert.Equal("a", lines[1].RootElement.GetProperty("id").GetString(),
            "drugie zadanie obsluzone PO bledzie");
        Assert.True(lines[1].RootElement.TryGetProperty("result", out _),
            "drugie zadanie ma wynik");
        Assert.True(exit == 0, "zly JSON nie konczy procesu bledem");
    }

    private static void UnknownOperationAnswersWithError()
    {
        var (lines, _) = RunLoop("""{"id":"b","op":"nie.ma.takiej"}""" + "\n", Echo);

        Assert.Equal("b", lines[0].RootElement.GetProperty("id").GetString(), "id w odpowiedzi");
        Assert.Equal("unknown_op",
            lines[0].RootElement.GetProperty("error").GetProperty("code").GetString(),
            "kod nieznanej operacji");
    }

    private static void EndOfInputEndsTheLoop()
    {
        // EOF na wejsciu zamyka WLASNY host. Bez tego proces backendu zostaje
        // po zamknieciu frontendu i trzyma urzadzenie audio.
        var (lines, exit) = RunLoop("""{"id":"c","op":"echo"}""" + "\n", Echo);

        Assert.True(lines.Count == 1, "po EOF nie ma dodatkowych wierszy");
        Assert.True(exit == 0, "EOF to normalne zakonczenie");
    }

    private static void StdoutCarriesOnlyProtocolLines()
    {
        // Handler, ktory pisze po stdout, zepsulby strumien protokolu. Host
        // podstawia wlasne stdout, wiec taki zapis idzie do diagnostyki.
        var handlers = new Dictionary<string, Func<LiteRequest, LiteEventSink, object?>>(StringComparer.Ordinal)
        {
            ["gada"] = static (_, _) =>
            {
                Console.Out.Write("to nie jest protokol\n");
                return new { ok = true };
            }
        };

        var (lines, _) = RunLoop("""{"id":"d","op":"gada"}""" + "\n", handlers);

        Assert.True(lines.Count == 1,
            $"stdout protokolu musi miec dokladnie 1 wiersz, ma {lines.Count}");
        Assert.Equal("d", lines[0].RootElement.GetProperty("id").GetString(), "id odpowiedzi");
    }

    private static void EventsAreWrittenAsOwnLines()
    {
        var handlers = new Dictionary<string, Func<LiteRequest, LiteEventSink, object?>>(StringComparer.Ordinal)
        {
            ["zglos"] = static (_, events) =>
            {
                events.Publish("playback.started", new { title = "Próba" });
                return new { ok = true };
            }
        };

        var (lines, _) = RunLoop("""{"id":"e","op":"zglos"}""" + "\n", handlers);

        Assert.True(lines.Count == 2, $"zdarzenie plus odpowiedz = 2 wiersze, jest {lines.Count}");
        Assert.Equal("playback.started",
            lines[0].RootElement.GetProperty("event").GetString(), "nazwa zdarzenia");
        Assert.Equal("Próba",
            lines[0].RootElement.GetProperty("data").GetProperty("title").GetString(),
            "polskie znaki w zdarzeniu bez ucieczek mojibake");
        Assert.True(!lines[0].RootElement.TryGetProperty("id", out _),
            "zdarzenie nie udaje odpowiedzi na zadanie");
        Assert.Equal("e", lines[1].RootElement.GetProperty("id").GetString(), "odpowiedz po zdarzeniu");
    }

    private static void HandlerFailureBecomesResponseNotCrash()
    {
        var handlers = new Dictionary<string, Func<LiteRequest, LiteEventSink, object?>>(StringComparer.Ordinal)
        {
            ["pada"] = static (_, _) => throw new InvalidOperationException("Nie znaleziono pliku."),
            ["echo"] = static (_, _) => new { ok = true }
        };

        var (lines, exit) = RunLoop(
            "{\"id\":\"f\",\"op\":\"pada\"}\n{\"id\":\"g\",\"op\":\"echo\"}\n",
            handlers);

        Assert.Equal("handler_failed",
            lines[0].RootElement.GetProperty("error").GetProperty("code").GetString(),
            "awaria handlera to odpowiedz bledu");
        Assert.True(
            lines[0].RootElement.GetProperty("error").GetProperty("message").GetString()
                ?.Contains("Nie znaleziono pliku.", StringComparison.Ordinal) == true,
            "komunikat bledu przenosi TRESC, nie tylko typ wyjatku");
        Assert.Equal("g", lines[1].RootElement.GetProperty("id").GetString(),
            "po awarii handlera petla dziala dalej");
        Assert.True(exit == 0, "awaria jednego polecenia nie konczy hosta");
    }
}
