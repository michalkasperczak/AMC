using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AccessibleMediaController.Core.Spotify;

/// <summary>
/// PRAWDZIWY proces-atrapa hosta Librespot. Ten sam plik wykonywalny testow
/// uruchamia sie ponownie z argumentem "--librespot-host-fixture &lt;tryb&gt;" i
/// zachowuje sie jak host: czyta linie JSON ze stdin i odpowiada liniami JSON
/// na stdout.
///
/// Dzieki temu transport jest mierzony na realnym procesie z realnymi
/// przekierowanymi strumieniami - a nie tylko na parsowaniu tekstu w pamieci.
/// Zadnego konta i zadnego prawdziwego Spotify tu nie ma: token jest jawnie
/// fikcyjny, a "odtwarzanie" to tylko zdarzenia protokolu.
/// </summary>
internal static class LibrespotHostFixture
{
    internal const string ModeArgument = "--librespot-host-fixture";

    /// <summary>Normalny host: gotowosc, potwierdzenia, urzadzenia, stany.</summary>
    internal const string ModeNormal = "normal";

    /// <summary>Host zglasza wersje protokolu, ktorej AMC nie zna.</summary>
    internal const string ModeBadVersion = "badversion";

    /// <summary>Host wstaje i nigdy nie odpowiada na polecenia.</summary>
    internal const string ModeSilent = "silent";

    /// <summary>Host konczy sie po pierwszym poleceniu, bez odpowiedzi.</summary>
    internal const string ModeExitAfterFirst = "exitafterfirst";

    /// <summary>Host wysyla jedna gigantyczna linie bez znaku konca wiersza.</summary>
    internal const string ModeLongLine = "longline";

    /// <summary>Host wysyla tekst, ktory nie jest JSON-em.</summary>
    internal const string ModeGarbage = "garbage";

    /// <summary>Host wcale nie zglasza gotowosci.</summary>
    internal const string ModeNoReady = "noready";

    /// <summary>Zmienna z opoznieniem odpowiedzi na "play" (ms).</summary>
    internal const string PlayDelayVariable = "AMC_FIXTURE_PLAY_DELAY_MS";

    /// <summary>Zmienna ze sciezka dziennika odebranych polecen.</summary>
    internal const string CommandLogVariable = "AMC_FIXTURE_COMMAND_LOG";

    /// <summary>
    /// Zmienna z opoznieniem (ms) miedzy odebraniem polecenia i zakonczeniem
    /// procesu w trybie <see cref="ModeExitAfterFirst"/>. Bez opoznienia zapis
    /// AMC do stdin padalby na przerwanym potoku, a test mierzylby inna droge
    /// niz koniec strumienia.
    /// </summary>
    internal const string ExitDelayVariable = "AMC_FIXTURE_EXIT_DELAY_MS";

    internal static int Run(string[] args)
    {
        var mode = args.Length > 1 ? args[1] : ModeNormal;
        var output = Console.Out;
        var logPath = Environment.GetEnvironmentVariable(CommandLogVariable);
        var playDelay = int.TryParse(
            Environment.GetEnvironmentVariable(PlayDelayVariable), out var parsed) ? parsed : 0;

        switch (mode)
        {
            case ModeBadVersion:
                WriteLine(output, new JsonObject { ["type"] = "ready", ["protocolVersion"] = 7 });
                Thread.Sleep(2000);
                return 0;
            case ModeNoReady:
                Thread.Sleep(2000);
                return 0;
            case ModeLongLine:
                WriteReady(output);
                output.Write(new string('x', 200_000));
                output.Flush();
                Thread.Sleep(2000);
                return 0;
            case ModeGarbage:
                WriteReady(output);
                output.WriteLine("to zdecydowanie nie jest JSON");
                output.Flush();
                Thread.Sleep(2000);
                return 0;
        }

        WriteReady(output);
        var commandCount = 0;
        while (Console.In.ReadLine() is { } line)
        {
            commandCount++;
            if (logPath is not null) AppendLog(logPath, line);
            if (mode == ModeExitAfterFirst)
            {
                var exitDelay = int.TryParse(
                    Environment.GetEnvironmentVariable(ExitDelayVariable), out var delay) ? delay : 0;
                if (exitDelay > 0) Thread.Sleep(exitDelay);
                return 3;
            }
            if (mode == ModeSilent) continue;

            JsonObject request;
            try
            {
                request = JsonNode.Parse(line) as JsonObject ?? new JsonObject();
            }
            catch (JsonException)
            {
                // Zlepione linie oznaczaja, ze pisarze nie byli serializowani.
                if (logPath is not null) AppendLog(logPath, "{\"fixture\":\"nieparsowalna linia\"}");
                continue;
            }

            var command = request["command"]?.GetValue<string>() ?? string.Empty;
            var requestId = request["requestId"] is JsonValue value
                && value.TryGetValue(out long identifier) ? identifier : 0L;

            switch (command)
            {
                case LibrespotHostContract.CommandDevices:
                    WriteLine(output, new JsonObject
                    {
                        ["type"] = "devices",
                        ["requestId"] = requestId,
                        ["devices"] = new JsonArray
                        {
                            new JsonObject { ["name"] = "Głośniki testowe", ["isDefault"] = true },
                            new JsonObject { ["name"] = "Słuchawki testowe", ["isDefault"] = false }
                        }
                    });
                    break;

                case LibrespotHostContract.CommandPlay:
                    if (playDelay > 0) Thread.Sleep(playDelay);
                    WriteAck(output, requestId);
                    var playId = request["playId"] is JsonValue identifierValue
                        && identifierValue.TryGetValue(out long play) ? play : 0L;
                    WriteLine(output, new JsonObject
                    {
                        ["type"] = "state",
                        ["sessionId"] = LibrespotHostContract.SessionId,
                        ["playId"] = playId,
                        ["uri"] = request["uri"]?.GetValue<string>() ?? string.Empty,
                        ["positionMs"] = request["positionMs"]?.GetValue<long>() ?? 0L,
                        ["durationMs"] = 180_000L,
                        ["isPlaying"] = true,
                        ["isPaused"] = false
                    });
                    break;

                case LibrespotHostContract.CommandShutdown:
                    WriteAck(output, requestId);
                    return 0;

                default:
                    WriteAck(output, requestId);
                    break;
            }
        }

        return commandCount >= 0 ? 0 : 1;
    }

    /// <summary>Uruchamia ten sam plik wykonywalny testow w trybie hosta.</summary>
    internal static LibrespotHostProcess Start(string mode)
    {
        var assembly = typeof(LibrespotHostFixture).Assembly.Location;
        var apphost = OperatingSystem.IsWindows()
            ? Path.ChangeExtension(assembly, ".exe")
            : Path.Combine(
                Path.GetDirectoryName(assembly) ?? ".",
                Path.GetFileNameWithoutExtension(assembly));
        return File.Exists(apphost)
            ? LibrespotHostProcess.Start(apphost, [ModeArgument, mode])
            : LibrespotHostProcess.Start(
                Environment.ProcessPath ?? "dotnet",
                [assembly, ModeArgument, mode]);
    }

    private static void WriteReady(TextWriter output) => WriteLine(output, new JsonObject
    {
        ["type"] = "ready",
        ["protocolVersion"] = LibrespotHostContract.ProtocolVersion
    });

    private static void WriteAck(TextWriter output, long requestId) => WriteLine(output, new JsonObject
    {
        ["type"] = "ack",
        ["requestId"] = requestId
    });

    private static void WriteLine(TextWriter output, JsonObject payload)
    {
        output.WriteLine(payload.ToJsonString());
        output.Flush();
    }

    private static void AppendLog(string path, string line)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            try
            {
                File.AppendAllText(path, line + Environment.NewLine, new UTF8Encoding(false));
                return;
            }
            catch (IOException)
            {
                Thread.Sleep(10);
            }
        }
    }
}
