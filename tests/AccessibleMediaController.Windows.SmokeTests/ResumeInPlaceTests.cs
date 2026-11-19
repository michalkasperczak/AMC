using AccessibleMediaController.Windows.Services;

/// <summary>
/// Wznowienie po pauzie w tym samym miejscu nie przewija toru. Przewiniecie
/// czysci silnik tempa i dla zrodla sieciowego restartuje ffmpeg; przy tempie
/// 2x i strumieniu YouTube oddawanym ok. 2x szybciej niz odtwarzanie dawalo to
/// krotkie przerwy. Czysta decyzja, bez okien i bez dzwieku.
/// </summary>
internal static class ResumeInPlaceTests
{
    internal static void Run()
    {
        var paused = TimeSpan.FromMinutes(12) + TimeSpan.FromMilliseconds(345);
        var shortPause = TimeSpan.FromSeconds(30);
        var longPause = TimeSpan.FromMinutes(30);

        Check(Resumes(paused, true, paused, shortPause, remote: false),
            "Wznowienie z miejsca pauzy przewija tor.");
        Check(Resumes(paused, true, paused + TimeSpan.FromMilliseconds(1), shortPause, remote: false),
            "Różnica zaokrąglenia pozycji wymusza przewinięcie.");
        Check(Resumes(paused, true, paused + TimeSpan.FromMilliseconds(250), shortPause, remote: false),
            "Pozycja na granicy tolerancji (250 ms) wymusza przewinięcie.");
        Check(!Resumes(paused, true, paused + TimeSpan.FromMilliseconds(300), shortPause, remote: false),
            "Wznowienie poza tolerancją nie przewija toru.");
        Check(!Resumes(paused, true, paused + TimeSpan.FromSeconds(10), shortPause, remote: false),
            "Wznowienie z innego miejsca nie przewija toru.");
        Check(!Resumes(paused, true, paused - TimeSpan.FromSeconds(10), shortPause, remote: false),
            "Wznowienie wcześniej niż pauza nie przewija toru.");
        Check(!Resumes(paused, true, TimeSpan.Zero, shortPause, remote: false),
            "Start od początku wstrzymanego materiału gra dalej z miejsca pauzy.");
        Check(!Resumes(null, true, paused, shortPause, remote: false),
            "Play bez wcześniejszej pauzy nie przewija toru.");
        Check(!Resumes(paused, false, paused, shortPause, remote: false),
            "Grające albo zatrzymane wyjście nie przewija toru.");
        Check(Resumes(paused, true, paused, shortPause, remote: true),
            "Krótka pauza materiału sieciowego nadal restartuje ffmpeg.");
        Check(!Resumes(paused, true, paused, longPause, remote: true),
            "Po długiej pauzie materiał sieciowy gra dalej starym, być może martwym połączeniem.");
        Check(Resumes(paused, true, paused, longPause, remote: false),
            "Długa pauza pliku lokalnego wymusza przewinięcie.");
        CheckPlayCoreWiring();
        Console.WriteLine("OK: wznowienie po pauzie w tym samym miejscu nie przewija toru");
    }

    private static bool Resumes(
        TimeSpan? pausedAt,
        bool outputPaused,
        TimeSpan requested,
        TimeSpan pausedFor,
        bool remote) =>
        WindowsMediaOutput.ResumesInPlace(pausedAt, outputPaused, requested, pausedFor, remote);

    /// <summary>
    /// Pozycje pauzy trzeba odczytac, ZANIM PlayCore nadpisze ja zadana
    /// pozycja i zdejmie znacznik pauzy. Odwrotna kolejnosc po cichu wylacza
    /// poprawke albo kaze kazdemu Play po pauzie grac dalej z miejsca pauzy.
    /// Bez prawdziwego toru WASAPI nie da sie tego wywolac, wiec sprawdzamy
    /// zrodlo, tak jak test wyciszania przy zmianie pliku.
    /// </summary>
    private static void CheckPlayCoreWiring()
    {
        var source = ReadRepositoryFile(Path.Combine(
            "src", "AccessibleMediaController.Windows", "Services", "WindowsMediaOutput.cs"));
        var start = source.IndexOf("private void PlayCore(", StringComparison.Ordinal);
        Check(start >= 0, "Nie znaleziono PlayCore.");
        var end = source.IndexOf("private async Task WatchPreparationTimeoutAsync", start, StringComparison.Ordinal);
        Check(end > start, "Nie znaleziono końca PlayCore.");
        var body = source[start..end];

        var capture = body.IndexOf("pausedAt = _pauseRequested ? _pendingPosition : null;", StringComparison.Ordinal);
        var overwrite = body.IndexOf("_pendingPosition = resolvedPosition;", StringComparison.Ordinal);
        var cleared = body.IndexOf("_pauseRequested = false;", StringComparison.Ordinal);
        Check(capture >= 0, "PlayCore nie zapamiętuje pozycji pauzy.");
        Check(capture < overwrite && capture < cleared,
            "PlayCore czyta pozycję pauzy po nadpisaniu jej żądaną pozycją albo po zdjęciu pauzy.");

        var decision = body.IndexOf("if (!ResumesInPlace(", StringComparison.Ordinal);
        var seek = body.IndexOf("SeekPipeline(reusable, resolvedPosition);", StringComparison.Ordinal);
        Check(decision >= 0 && seek > decision,
            "Wznowienie toru wielokrotnego użytku przewija bez decyzji o wznowieniu w miejscu.");
    }

    private static string ReadRepositoryFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, relativePath);
            if (File.Exists(candidate)) return File.ReadAllText(candidate);
            directory = directory.Parent;
        }
        throw new FileNotFoundException($"Nie znaleziono pliku repozytorium: {relativePath}");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
