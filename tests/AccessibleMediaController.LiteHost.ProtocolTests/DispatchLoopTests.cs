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
        SlowQuickInformationDoesNotBlockTransport();
        SlowQuickInformationDoesNotBlockTransport(LiteDispatchLoop.MaxConcurrentOperations + 1);
        OrdinaryOperationsStayInOrder();
        ConcurrentOperationFinishesBeforeRunReturns();
        ConcurrencyIsBoundedNotOnePerRequest();
        Console.WriteLine("DispatchLoopTests: OK");
    }

    /// <summary>
    /// Czas oczekiwania sondy. Test NIE mierzy wydajnosci: uzywa bariery,
    /// wiec limit sluzy tylko temu, zeby niepowodzenie bylo niepowodzeniem,
    /// a nie zawieszonym procesem testow.
    /// </summary>
    private static readonly TimeSpan ProbeWait = TimeSpan.FromSeconds(10);

    /// <summary>
    /// RYZYKO LEWEJ STRZALKI: <c>media.quickInformation</c> czyta metadane
    /// pliku/strumienia synchronicznie (<c>GetAwaiter().GetResult()</c> z
    /// limitem kilku sekund). W petli serialnej takie zadanie wstrzymuje
    /// KAZDE nastepne polecenie, takze <c>transport.status</c> i pauze, ktore
    /// uzytkownik wywoluje w trakcie czytania informacji.
    ///
    /// Pomiar jest BARIERA, nie uspieniem: handler informacji wchodzi i czeka,
    /// a test wymaga, zeby transport zostal obsluzony PRZED jej zwolnieniem.
    /// Bariere zwalnia blok <c>finally</c>, zeby niepowodzenie nie zostawilo
    /// zakleszczonego watku petli.
    /// </summary>
    private static void SlowQuickInformationDoesNotBlockTransport(int informationRequests = 1)
    {
        using var quickInfoEntered = new ManualResetEventSlim(false);
        using var releaseQuickInfo = new ManualResetEventSlim(false);
        using var transportHandled = new ManualResetEventSlim(false);
        using var pauseHandled = new ManualResetEventSlim(false);

        var handlers = new Dictionary<string, Func<LiteRequest, LiteEventSink, object?>>(StringComparer.Ordinal)
        {
            ["media.quickInformation"] = (_, _) =>
            {
                quickInfoEntered.Set();
                // Zwolnienie WYŁĄCZNIE przez finally testu. Timeout samego
                // handlera mógłby pozornie odblokować transport przed asercją.
                releaseQuickInfo.Wait();
                return new { text = "Informacje" };
            },
            ["transport.status"] = (_, _) =>
            {
                transportHandled.Set();
                return new { playing = true };
            },
            ["transport.pause"] = (_, _) =>
            {
                pauseHandled.Set();
                return new { paused = true };
            }
        };

        var input = string.Join('\n', Enumerable.Range(0, informationRequests)
            .Select(index => $"{{\"id\":\"info{index}\",\"op\":\"media.quickInformation\"}}")
            .Concat([
                """{"id":"stan","op":"transport.status"}""",
                """{"id":"pauza","op":"transport.pause"}"""
            ])) + "\n";

        var output = new StringWriter();
        var loop = new LiteDispatchLoop(handlers, concurrentOperations: ["media.quickInformation"]);
        var worker = new Thread(() =>
        {
            using var reader = new StringReader(input);
            loop.Run(reader, output);
        })
        { IsBackground = true };

        try
        {
            worker.Start();

            Assert.True(quickInfoEntered.Wait(ProbeWait),
                "handler informacji musi w ogole wejsc (inaczej sonda nic nie mierzy)");
            Assert.True(transportHandled.Wait(ProbeWait),
                "transport.status obsluzony W TRAKCIE wolnej informacji, PRZED zwolnieniem bariery");
            Assert.True(pauseHandled.Wait(ProbeWait),
                "pauza obsluzona W TRAKCIE wolnej informacji: uzytkownik moze zatrzymac odtwarzanie");
        }
        finally
        {
            releaseQuickInfo.Set();
            worker.Join(ProbeWait);
        }

        var lines = output.ToString()
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => JsonDocument.Parse(line.TrimEnd('\r')))
            .ToArray();

        Assert.True(lines.Length == informationRequests + 2, $"odpowiedź na każde zadanie, jest {lines.Length}");
        var identifiers = lines.Select(line => line.RootElement.GetProperty("id").GetString()).ToArray();
        Assert.True(identifiers.Contains("info0"), "odpowiedz informacji dotarla");
        Assert.True(identifiers.Contains("stan"), "odpowiedz transport.status dotarla");
        Assert.True(identifiers.Contains("pauza"), "odpowiedz pauzy dotarla");
        if (informationRequests > LiteDispatchLoop.MaxConcurrentOperations)
        {
            var busy = lines.Where(line => line.RootElement.TryGetProperty("error", out _)).ToArray();
            Assert.True(busy.Length == informationRequests - LiteDispatchLoop.MaxConcurrentOperations,
                "nadmiar ma natychmiastową odpowiedź, nie znika i nie tworzy dodatkowych zadań");
            Assert.True(busy.All(line => line.RootElement.GetProperty("error").GetProperty("code").GetString() == "operation_busy"),
                "odmowa zachowuje kod operation_busy dla krótkiego komunikatu interfejsu");
        }
        // Wolna informacja konczy sie PO transporcie: dowod, ze transport
        // nie czekal na nia w kolejce.
        Assert.True(Array.IndexOf(identifiers, "info0") > Array.IndexOf(identifiers, "stan"),
            "odpowiedz informacji wraca po transporcie, bo transport jej nie czekal");
    }

    /// <summary>
    /// Zwykle polecenia zostaja SERIALNE. Zmiana dotyczy tylko jawnie
    /// wskazanej operacji, wiec kolejnosc pozostalych musi byc nadal
    /// deterministyczna (zmiana glosnosci po pauzie, nie odwrotnie).
    /// </summary>
    private static void OrdinaryOperationsStayInOrder()
    {
        var order = new List<string>();
        var handlers = new Dictionary<string, Func<LiteRequest, LiteEventSink, object?>>(StringComparer.Ordinal)
        {
            ["pierwsze"] = (_, _) =>
            {
                Thread.Sleep(30);
                lock (order) order.Add("pierwsze");
                return new { ok = true };
            },
            ["drugie"] = (_, _) =>
            {
                lock (order) order.Add("drugie");
                return new { ok = true };
            }
        };

        using var reader = new StringReader(
            "{\"id\":\"1\",\"op\":\"pierwsze\"}\n{\"id\":\"2\",\"op\":\"drugie\"}\n");
        var output = new StringWriter();
        // Operacja wspolbiezna jest zadeklarowana, ale ZADNE z tych zadan nia
        // nie jest: obie musza przejsc po kolei.
        new LiteDispatchLoop(handlers, concurrentOperations: ["media.quickInformation"])
            .Run(reader, output);

        Assert.True(order.Count == 2, $"oba zadania wykonane, jest {order.Count}");
        Assert.Equal("pierwsze", order[0], "wolniejsze zwykle polecenie nadal idzie pierwsze");
        Assert.Equal("drugie", order[1], "zwykle polecenia pozostaja uporzadkowane serialnie");

        var identifiers = output.ToString()
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => JsonDocument.Parse(line.TrimEnd('\r')).RootElement.GetProperty("id").GetString())
            .ToArray();
        Assert.Equal("1", identifiers[0], "odpowiedzi zwyklych polecen w kolejnosci zadan");
        Assert.Equal("2", identifiers[1], "odpowiedzi zwyklych polecen w kolejnosci zadan");
    }

    /// <summary>
    /// EOF nie moze zostawic wlasnego zadania, ktore dopisze wiersz do JUZ
    /// zamknietego strumienia protokolu. <c>Run</c> wraca dopiero wtedy, gdy
    /// wszystkie rozpoczete zadania skoncza pisac.
    /// </summary>
    private static void ConcurrentOperationFinishesBeforeRunReturns()
    {
        var handlers = new Dictionary<string, Func<LiteRequest, LiteEventSink, object?>>(StringComparer.Ordinal)
        {
            ["media.quickInformation"] = (_, _) =>
            {
                Thread.Sleep(60);
                return new { text = "Informacje" };
            }
        };

        using var reader = new StringReader("""{"id":"info","op":"media.quickInformation"}""" + "\n");
        var output = new StringWriter();
        var exit = new LiteDispatchLoop(handlers, concurrentOperations: ["media.quickInformation"])
            .Run(reader, output);

        // Odczyt NATYCHMIAST po powrocie Run: brak odpowiedzi oznaczalby zapis
        // po zamknieciu strumienia u prawdziwego hosta.
        var text = output.ToString();
        Assert.True(exit == 0, "EOF to normalne zakonczenie takze przy operacji wspolbieznej");
        Assert.True(text.Contains("\"info\"", StringComparison.Ordinal),
            "odpowiedz zadania wspolbieznego zapisana PRZED powrotem z Run");
        Assert.True(text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length == 1,
            "dokladnie jedna odpowiedz, bez duplikatu z dwoch torow");
    }

    /// <summary>
    /// Przytrzymany skrot (auto-powtarzanie klawisza) nie moze wyprodukowac
    /// dowolnej liczby równoległych pomiarów. Nadmiar dostaje szybką odmowę,
    /// zamiast blokować transport na serialnym pomiarze. Każde ID ma odpowiedź.
    /// </summary>
    private static void ConcurrencyIsBoundedNotOnePerRequest()
    {
        const int requests = 40;
        var active = 0;
        var peak = 0;
        var peakGate = new object();
        var handlers = new Dictionary<string, Func<LiteRequest, LiteEventSink, object?>>(StringComparer.Ordinal)
        {
            ["media.quickInformation"] = (_, _) =>
            {
                var now = Interlocked.Increment(ref active);
                lock (peakGate) peak = Math.Max(peak, now);
                Thread.Sleep(5);
                Interlocked.Decrement(ref active);
                return new { text = "Informacje" };
            }
        };

        var input = string.Join('\n', Enumerable.Range(0, requests)
            .Select(index => $"{{\"id\":\"i{index}\",\"op\":\"media.quickInformation\"}}")) + "\n";

        using var reader = new StringReader(input);
        var output = new StringWriter();
        new LiteDispatchLoop(handlers, concurrentOperations: ["media.quickInformation"])
            .Run(reader, output);

        var lines = output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.True(lines.Length == requests,
            $"kazde zadanie ma odpowiedz, oczekiwano {requests}, jest {lines.Length}");
        // Nie ma dodatkowego pomiaru na wątku pętli: on musi obsługiwać pauzę.
        const int bound = LiteDispatchLoop.MaxConcurrentOperations;
        Assert.True(peak <= bound,
            $"liczba jednoczesnych pomiarow ograniczona do {bound} (bez blokowania transportu), szczyt {peak}");
        Assert.True(peak > 1,
            $"pomiar musi w ogole zobaczyc wspolbieznosc, szczyt {peak}");
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
