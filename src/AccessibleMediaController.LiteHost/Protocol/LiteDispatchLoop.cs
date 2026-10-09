using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace AccessibleMediaController.LiteHost.Protocol;

/// <summary>
/// Kanal zdarzen jednokierunkowych (odtwarzanie zaczelo sie, skonczylo,
/// strumien zmienil tytul). Zdarzenie NIE ma pola <c>id</c>, zeby frontend
/// nie mogl pomylic go z odpowiedzia na swoje zadanie.
/// </summary>
public sealed class LiteEventSink(Action<string> writeLine)
{
    public void Publish(string name, object? data) =>
        writeLine(LiteJson.Serialize(new LiteEventEnvelope(name, data)));
}

internal sealed record LiteEventEnvelope(string Name, object? Data);

internal sealed record LiteResultEnvelope(string Id, object? Result);

internal sealed record LiteErrorEnvelope(string? Id, string Code, string Message);

public static class LiteJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        // Polskie znaki musza wyjsc jako UTF-8, nie jako \uXXXX: frontend
        // wyswietla je prosto w kontrolkach dla czytnika ekranu.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false
    };

    public static string Serialize(object value) => value switch
    {
        LiteEventEnvelope envelope => WriteObject(writer =>
        {
            writer.WriteString("event", envelope.Name);
            writer.WritePropertyName("data");
            JsonSerializer.Serialize(writer, envelope.Data, Options);
        }),
        LiteResultEnvelope envelope => WriteObject(writer =>
        {
            writer.WriteString("id", envelope.Id);
            writer.WritePropertyName("result");
            JsonSerializer.Serialize(writer, envelope.Result, Options);
        }),
        LiteErrorEnvelope envelope => WriteObject(writer =>
        {
            if (envelope.Id is not null) writer.WriteString("id", envelope.Id);
            writer.WriteStartObject("error");
            writer.WriteString("code", envelope.Code);
            writer.WriteString("message", envelope.Message);
            writer.WriteEndObject();
        }),
        _ => JsonSerializer.Serialize(value, Options)
    };

    private static string WriteObject(Action<Utf8JsonWriter> body)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions
        {
            Encoder = Options.Encoder,
            Indented = false
        }))
        {
            writer.WriteStartObject();
            body(writer);
            writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(buffer.ToArray());
    }
}

/// <summary>
/// Petla zadan hosta. Jeden wiersz wejscia = jedno zadanie, jeden wiersz
/// wyjscia = jedna odpowiedz albo zdarzenie. Zasady, ktore wynikaja z testow:
/// zly JSON i awaria handlera daja odpowiedz bledu i petla idzie dalej, EOF
/// konczy petle, a zapis handlera do <c>Console.Out</c> nie trafia do
/// strumienia protokolu.
/// </summary>
public sealed class LiteDispatchLoop(
    IReadOnlyDictionary<string, Func<LiteRequest, LiteEventSink, object?>> handlers,
    IReadOnlyCollection<string>? concurrentOperations = null)
{
    /// <summary>
    /// Gorna granica jednoczesnych zadan wspolbieznych. Przytrzymany klawisz
    /// nie może tworzyć nieograniczonej liczby pomiarów. Przy zajętych
    /// miejscach odpowiadamy odmową, nigdy pomiarem na wątku transportu.
    /// </summary>
    public const int MaxConcurrentOperations = 3;

    private readonly object _writeGate = new();

    /// <summary>
    /// Operacje jawnie zgloszone jako wspolbiezne. Pusty zbior = zachowanie
    /// sprzed zmiany, czyli pelna serializacja.
    /// </summary>
    private readonly HashSet<string> _concurrent =
        new(concurrentOperations ?? [], StringComparer.Ordinal);

    public int Run(TextReader input, TextWriter protocolOutput)
    {
        // Jeden zamek na zapis: zdarzenia moga przychodzic z watkow silnika
        // audio, a przeplatany wiersz zepsulby strumien JSON-lines.
        void WriteLine(string line)
        {
            lock (_writeGate)
            {
                protocolOutput.Write(line);
                protocolOutput.Write('\n');
                protocolOutput.Flush();
            }
        }

        var events = new LiteEventSink(WriteLine);
        var originalStdout = Console.Out;
        // Handler, ktory pisze po Console.Out (albo zrobi to biblioteka
        // ponizej), nie moze wejsc w strumien protokolu. Przekierowujemy go
        // na diagnostyke.
        Console.SetOut(Console.Error);

        // Zadania JAWNIE zgloszonych operacji wspolbieznych. Lista istnieje
        // po to, zeby po EOF poczekac na ich koniec: zapis do zamknietego
        // juz strumienia protokolu byloby bledem hosta, nie frontendu.
        var inFlight = new List<Task>();
        using var slots = new SemaphoreSlim(MaxConcurrentOperations, MaxConcurrentOperations);
        try
        {
            while (input.ReadLine() is { } line)
            {
                if (line.Trim().Length == 0) continue;

                // Tylko jawnie wskazane, dlugie operacje moga ominac kolejke.
                // Pozostale polecenia nadal ida SERIALNIE, wiec ich wzajemna
                // kolejnosc sie nie zmienia.
                var outcome = LiteRequestReader.Read(line);
                if (!TryBeginConcurrent(outcome, events, WriteLine, slots, inFlight))
                {
                    Handle(outcome, events, WriteLine);
                }
            }
        }
        finally
        {
            // Najpierw dokoncz wlasne zadania, dopiero potem oddaj stdout i
            // wroc: inaczej spozniony wiersz trafilby w pustke.
            try
            {
                Task.WaitAll([.. inFlight]);
            }
            catch (AggregateException)
            {
                // Wyjatki handlerow sa juz zamienione na odpowiedzi bledu
                // wewnatrz zadania; tutaj nie ma czego raportowac.
            }
            Console.SetOut(originalStdout);
        }
        return 0;
    }

    /// <summary>
    /// Probuje obsluzyc zadanie poza kolejka. Zwraca <c>false</c>, gdy
    /// operacja nie jest zgłoszona jako współbieżna. Przy zajętych miejscach
    /// od razu odpowiada błędem; spadnięcie na tor serialny znowu blokowałoby
    /// pauzę właśnie przy szybkim powtarzaniu klawisza.
    /// </summary>
    private bool TryBeginConcurrent(
        LiteReadOutcome outcome,
        LiteEventSink events,
        Action<string> writeLine,
        SemaphoreSlim slots,
        List<Task> inFlight)
    {
        if (_concurrent.Count == 0) return false;
        // Zle zadanie i nieznana operacja zostaja na torze serialnym: ich
        // odpowiedz bledu i tak jest natychmiastowa.
        if (!outcome.IsRequest || !_concurrent.Contains(outcome.Request!.Op)) return false;
        if (!slots.Wait(0))
        {
            writeLine(LiteJson.Serialize(new LiteErrorEnvelope(
                outcome.Request.Id,
                "operation_busy",
                "Trwa odczyt informacji. Powtórz skrót za chwilę.")));
            return true;
        }

        inFlight.RemoveAll(static task => task.IsCompleted);
        inFlight.Add(Task.Run(() =>
        {
            try
            {
                Handle(outcome, events, writeLine);
            }
            finally
            {
                slots.Release();
            }
        }));
        return true;
    }

    private void Handle(LiteReadOutcome outcome, LiteEventSink events, Action<string> writeLine)
    {
        if (!outcome.IsRequest)
        {
            writeLine(LiteJson.Serialize(new LiteErrorEnvelope(
                outcome.RequestId,
                outcome.ErrorCode ?? "bad_request",
                outcome.ErrorMessage ?? "Niepoprawne zadanie.")));
            return;
        }

        var request = outcome.Request!;
        if (!handlers.TryGetValue(request.Op, out var handler))
        {
            writeLine(LiteJson.Serialize(new LiteErrorEnvelope(
                request.Id,
                "unknown_op",
                $"Nieznana operacja \"{request.Op}\".")));
            return;
        }

        try
        {
            var result = handler(request, events);
            writeLine(LiteJson.Serialize(new LiteResultEnvelope(request.Id, result)));
        }
        catch (Exception exception)
        {
            // Tresc komunikatu, nie sam typ: frontend czyta ja uzytkownikowi.
            writeLine(LiteJson.Serialize(new LiteErrorEnvelope(
                request.Id,
                "handler_failed",
                exception.Message)));
            Console.Error.WriteLine($"[lite-host] {request.Op}: {exception}");
        }
    }
}
