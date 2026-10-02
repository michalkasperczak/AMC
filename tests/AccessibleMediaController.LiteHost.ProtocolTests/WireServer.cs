using System.Text.Json;
using AccessibleMediaController.LiteHost.Protocol;

namespace AccessibleMediaController.LiteHost.ProtocolTests;

/// <summary>
/// Uruchamia PRAWDZIWA petle dyspozytora hosta na stdin/stdout, ale z
/// handlerami bez dzwieku. Sluzy do zmierzenia ZGODNOSCI PROTOKOLU z klientem
/// Python w WSL: ten sam kod <see cref="LiteDispatchLoop"/>, ktory idzie na
/// Windows, odpowiada tu na prawdziwe zadania frontendu.
///
/// To NIE jest atrapa odtwarzania i nie udaje silnika - mierzy wylacznie
/// warstwe komunikacji, ktorej na Linuksie da sie dotknac.
/// </summary>
internal static class WireServer
{
    public static int Run()
    {
        var standardOutput = Console.Out;
        Console.SetOut(Console.Error);

        var handlers = new Dictionary<string, Func<LiteRequest, LiteEventSink, object?>>(StringComparer.Ordinal)
        {
            ["host.hello"] = (_, _) => new { host = "amc-lite-host", protocol = 1, wire = true },
            ["echo"] = (request, _) => new { op = request.Op, args = request.Args },
            ["transport.status"] = (_, _) => new
            {
                engine = "files",
                paused = false,
                positionSeconds = 12.5,
                durationSeconds = 100.0,
                volume = 35,
                rate = 1.0
            },
            // Sprawdza, ze argumenty czytane sa z granicami (LiteArgs).
            ["transport.setVolume"] = (request, _) =>
                new { volume = LiteArgs.ReadInt(request.Args, "volume", 35, 0, 100) },
            ["transport.setRate"] = (request, _) =>
                new { rate = LiteArgs.ReadDouble(request.Args, "rate", 1d, 0.5d, 2d) },
            ["files.listFolder"] = (request, _) =>
            {
                var path = LiteArgs.RequirePath(request.Args, "path");
                return new
                {
                    path,
                    parent = (string?)null,
                    items = new object[]
                    {
                        new { kind = "folder", id = "dir:" + path + "/album", title = "album", path = path + "/album" },
                        new { kind = "track", id = "file:" + path + "/a.mp3", title = "a", path = path + "/a.mp3" }
                    }
                };
            },
            // Blad TRESCI: host odpowiada bledem i ZYJE dalej.
            ["fail"] = (_, _) => throw new LiteRequestException("nie znalazlem pliku"),
            // Nieoczekiwany wyjatek tez nie moze zabic hosta.
            ["crash"] = (_, _) => throw new InvalidOperationException("wewnetrzny blad"),
            ["emit"] = (request, events) =>
            {
                events.Publish("playback.started", new { title = LiteArgs.ReadText(request.Args, "title") ?? "utwor" });
                return new { ok = true };
            },
            ["bigPayload"] = (_, _) => new { text = new string('x', 100_000) },
            ["utf8"] = (request, _) => new { text = LiteArgs.ReadText(request.Args, "text") }
        };

        new LiteDispatchLoop(handlers).Run(Console.In, standardOutput);
        return 0;
    }
}
