using System.Diagnostics;
using System.Text;
using System.Text.Json;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Podcasts;
using AccessibleMediaController.Windows.Services;

internal static class YouTubeChapterProcessTests
{
    private const string FixtureModeVariable = "AMC_YOUTUBE_CHAPTER_FIXTURE_MODE";
    private const string FixtureReadyPathVariable = "AMC_YOUTUBE_CHAPTER_FIXTURE_READY_PATH";

    internal static void Run()
    {
        var root = Path.Combine(Path.GetTempPath(), "amc-youtube-chapters-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var oldExecutable = Environment.GetEnvironmentVariable("YTDLP_PATH");
        var oldMode = Environment.GetEnvironmentVariable(FixtureModeVariable);
        var oldReady = Environment.GetEnvironmentVariable(FixtureReadyPathVariable);
        try
        {
            var apphost = Path.ChangeExtension(typeof(YouTubeChapterProcessTests).Assembly.Location, ".exe");
            Ensure(File.Exists(apphost), "Brak prywatnego apphosta fixture testów rozdziałów.");
            Environment.SetEnvironmentVariable("YTDLP_PATH", apphost);

            var chapters = ReadFixture(root, "normal");
            Ensure(chapters.Count == 2
                && chapters[0].Name == "Wstęp"
                && chapters[1].Start == TimeSpan.FromSeconds(62),
                $"fixture yt-dlp zwraca prawidłowe rozdziały twórcy (count={chapters.Count}, first={chapters.FirstOrDefault()?.Name ?? "brak"})");
            ExpectFailure(root, "malformed", typeof(JsonException));
            ExpectFailure(root, "nonzero", typeof(InvalidDataException));
            ExpectFailure(root, "stdout-overflow", typeof(InvalidDataException));
            ExpectFailure(root, "stderr-overflow", typeof(InvalidDataException));

            using var cancelled = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));
            var ready = Path.Combine(root, "cancel.ready");
            Environment.SetEnvironmentVariable(FixtureModeVariable, "wait");
            Environment.SetEnvironmentVariable(FixtureReadyPathVariable, ready);
            var cancelledFailure = false;
            try
            {
                YouTubeSourceResolver.ReadChaptersAsync(
                    new Uri("https://www.youtube.com/watch?v=fixture-cancel"),
                    TimeSpan.FromMinutes(10),
                    cancelled.Token,
                    Path.ChangeExtension(typeof(YouTubeChapterProcessTests).Assembly.Location, ".exe")).GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
                cancelledFailure = true;
            }
            Ensure(cancelledFailure, "anulowanie zatrzymuje wyłącznie własny proces fixture yt-dlp");
            AssertTerminated(ready);
        }
        finally
        {
            Environment.SetEnvironmentVariable("YTDLP_PATH", oldExecutable);
            Environment.SetEnvironmentVariable(FixtureModeVariable, oldMode);
            Environment.SetEnvironmentVariable(FixtureReadyPathVariable, oldReady);
            Directory.Delete(root, recursive: true);
        }
    }

    internal static int RunFixture()
    {
        var mode = Environment.GetEnvironmentVariable(FixtureModeVariable);
        if (string.IsNullOrWhiteSpace(mode)) return -1;
        Console.OutputEncoding = new UTF8Encoding(false);
        var ready = Environment.GetEnvironmentVariable(FixtureReadyPathVariable);
        if (!string.IsNullOrWhiteSpace(ready))
            File.WriteAllText(ready, $"READY {Environment.ProcessId}", new UTF8Encoding(false));
        switch (mode)
        {
            case "normal":
                Console.Out.Write("[{\"start_time\":0,\"title\":\"  Wstęp  \"},{\"start_time\":62,\"title\":\"Temat\"}]");
                Console.Out.Flush();
                return WaitForOwnerInput();
            case "malformed":
                Console.Out.Write("[{\"start_time\":0");
                Console.Out.Flush();
                return WaitForOwnerInput();
            case "nonzero":
                Console.Error.Write("fixture-nonzero");
                Console.Error.Flush();
                return 7;
            case "stdout-overflow":
                Console.Out.Write(new string('x', YouTubeChapterParser.MaximumOutputCharacters + 8192));
                Console.Out.Flush();
                return WaitForOwnerInput();
            case "stderr-overflow":
                Console.Out.Write("NA");
                Console.Out.Flush();
                Console.Error.Write(new string('e', 64 * 1024 + 8192));
                Console.Error.Flush();
                return WaitForOwnerInput();
            case "wait":
                Thread.Sleep(TimeSpan.FromSeconds(10));
                return 0;
            default:
                return 2;
        }
    }

    private static IReadOnlyList<ProviderChapterPoint> ReadFixture(string root, string mode)
    {
        var ready = Path.Combine(root, mode + ".ready");
        Environment.SetEnvironmentVariable(FixtureModeVariable, mode);
        Environment.SetEnvironmentVariable(FixtureReadyPathVariable, ready);
        try
        {
            return YouTubeSourceResolver.ReadChaptersAsync(
                new Uri("https://www.youtube.com/watch?v=fixture-" + mode),
                TimeSpan.FromMinutes(10),
                CancellationToken.None,
                Path.ChangeExtension(typeof(YouTubeChapterProcessTests).Assembly.Location, ".exe")).GetAwaiter().GetResult();
        }
        finally
        {
            AssertTerminated(ready);
        }
    }

    private static void ExpectFailure(string root, string mode, Type expectedType)
    {
        var failed = false;
        try
        {
            _ = ReadFixture(root, mode);
        }
        catch (Exception exception) when (expectedType.IsAssignableFrom(exception.GetType()))
        {
            failed = true;
        }
        Ensure(failed, $"fixture {mode} ma zgłosić {expectedType.Name}");
    }

    private static int WaitForOwnerInput()
    {
        // The parent owns redirected stdin. It closes this pipe on every path;
        // this fixture never uses a shell sleep or an unrelated system process.
        _ = Console.In.ReadToEnd();
        return 0;
    }

    private static void AssertTerminated(string readyPath)
    {
        Ensure(File.Exists(readyPath), "fixture nie zgłosił READY z własnym PID");
        var fields = File.ReadAllText(readyPath).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length != 2 || fields[0] != "READY" || !int.TryParse(fields[1], out var pid))
            throw new InvalidOperationException("fixture zgłosił nieprawidłowy READY");
        for (var attempt = 0; attempt < 20; attempt++)
        {
            try
            {
                using var process = Process.GetProcessById(pid);
                Thread.Sleep(50);
            }
            catch (ArgumentException)
            {
                return;
            }
        }
        throw new InvalidOperationException($"fixture yt-dlp PID {pid} pozostał uruchomiony po teście.");
    }

    private static void Ensure(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
