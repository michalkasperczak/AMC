using AccessibleMediaController.Windows.Services;
using NAudio.CoreAudioApi;

/// <summary>
/// Wyjście bez wybranego urządzenia idzie za zmianą domyślnego urządzenia
/// Windows (np. podłączenie słuchawek). Decyzje są czyste; jedyny kontakt z
/// systemem to otwarcie wyjścia bez Init i Play oraz rejestracja
/// powiadomień, więc test nie gra dźwięku i nie otwiera okien.
/// </summary>
internal static class DefaultOutputFollowTests
{
    private const string Speakers = "{0.0.0.00000000}.{speakers}";
    private const string Headphones = "{0.0.0.00000000}.{headphones}";

    internal static void Run()
    {
        // Podłączenie słuchawek: głośniki dalej działają, zmienia się domyślne.
        Check(DefaultAudioOutputWatcher.ShouldFollow(null, Speakers, true, Headphones),
            "Wyjście na domyślnym urządzeniu nie przechodzi na nowe domyślne.");
        Check(DefaultAudioOutputWatcher.ShouldFollow("", Speakers, true, Headphones),
            "Puste ustawienie urządzenia nie jest traktowane jak urządzenie domyślne.");
        Check(!DefaultAudioOutputWatcher.ShouldFollow(Speakers, Speakers, true, Headphones),
            "Sesja z wybranym urządzeniem przechodzi na domyślne.");
        // Odłączenie słuchawek: zostawiamy odzyskiwaniu po utracie urządzenia,
        // które zna ustawienie wstrzymania po odłączeniu.
        Check(!DefaultAudioOutputWatcher.ShouldFollow(null, Headphones, false, Speakers),
            "Odłączone urządzenie jest przenoszone obok odzyskiwania po jego utracie.");
        Check(!DefaultAudioOutputWatcher.ShouldFollow(null, Headphones, true, Headphones),
            "Tor otwarty już na nowym urządzeniu (po odłączeniu słuchawek) jest otwierany drugi raz.");
        Check(!DefaultAudioOutputWatcher.ShouldFollow(null, Headphones, true, Headphones.ToUpperInvariant()),
            "Ta sama nazwa urządzenia w innej wielkości liter wymusza przełączenie.");
        Check(!DefaultAudioOutputWatcher.ShouldFollow(null, null, false, Headphones),
            "Brak otwartego wyjścia (przygotowanie, zatrzymanie) wymusza przełączenie.");
        Check(!DefaultAudioOutputWatcher.ShouldFollow(null, Speakers, true, null),
            "Brak urządzenia domyślnego wymusza przełączenie.");

        Check(DefaultAudioOutputWatcher.IsRelevant(DataFlow.Render, Role.Console),
            "Zmiana domyślnego urządzenia odtwarzania jest pomijana.");
        Check(!DefaultAudioOutputWatcher.IsRelevant(DataFlow.Render, Role.Communications),
            "Zmiana urządzenia komunikacyjnego przełącza odtwarzanie.");
        Check(!DefaultAudioOutputWatcher.IsRelevant(DataFlow.Render, Role.Multimedia),
            "Powiadomienie roli Multimedia jest liczone drugi raz obok Console.");
        Check(!DefaultAudioOutputWatcher.IsRelevant(DataFlow.Capture, Role.Console),
            "Zmiana mikrofonu przełącza odtwarzanie.");
        Check(DefaultAudioOutputWatcher.SettleDelay >= TimeSpan.FromMilliseconds(200)
                && DefaultAudioOutputWatcher.SettleDelay <= TimeSpan.FromSeconds(2),
            "Opóźnienie zbierania powiadomień jest poza rozsądnym zakresem.");

        CheckLiveDefaultEndpoint();
        CheckHostWiring();
    }

    /// <summary>
    /// Na komputerze z urządzeniem audio: wyjście domyślne zna swoje
    /// urządzenie, a obserwator rejestruje się i wyrejestrowuje bez błędu.
    /// Bez urządzeń audio test pomija tę część.
    /// </summary>
    private static void CheckLiveDefaultEndpoint()
    {
        string? defaultId;
        using (var enumerator = new MMDeviceEnumerator())
        {
            if (!enumerator.HasDefaultAudioEndpoint(DataFlow.Render, Role.Console)) return;
            using var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Console);
            defaultId = device.ID;
        }

        using (var lease = AudioOutputDeviceCatalog.CreateOutput(null, 120))
        {
            Check(lease.UsesDefaultDevice, "Wyjście bez urządzenia nie jest oznaczone jako domyślne.");
            Check(string.Equals(lease.EndpointId, defaultId, StringComparison.OrdinalIgnoreCase),
                "Wyjście domyślne nie zapamiętało urządzenia, na którym gra.");
        }

        Check(AudioOutputDeviceCatalog.IsEndpointActive(defaultId),
            "Aktywne urządzenie domyślne jest uznane za nieaktywne.");
        Check(!AudioOutputDeviceCatalog.IsEndpointActive("{0.0.0.00000000}.{nie-ma-takiego}"),
            "Nieistniejące urządzenie jest uznane za aktywne.");
        Check(!AudioOutputDeviceCatalog.IsEndpointActive(null),
            "Brak identyfikatora jest uznany za aktywne urządzenie.");

        using (var lease = AudioOutputDeviceCatalog.CreateOutput(defaultId, 120))
        {
            Check(!lease.UsesDefaultDevice, "Wskazane urządzenie jest oznaczone jako domyślne.");
            Check(string.Equals(lease.EndpointId, defaultId, StringComparison.Ordinal),
                "Wyjście na wskazanym urządzeniu nie zna swojego identyfikatora.");
        }

        var watcher = DefaultAudioOutputWatcher.TryStart(_ => { });
        Check(watcher is not null, "Nie udało się zarejestrować powiadomień o urządzeniu domyślnym.");
        watcher!.Dispose();
        watcher.Dispose();
    }

    /// <summary>
    /// Host uruchamia obserwatora, zamyka go przed wyjściami i przenosi
    /// wyjście tą samą drogą co ręczny wybór urządzenia.
    /// </summary>
    private static void CheckHostWiring()
    {
        var host = ReadRepositoryFile(Path.Combine(
            "src", "AccessibleMediaController.LiteHost", "LiteEngineHandlers.cs"));
        Check(host.Contains("DefaultAudioOutputWatcher.TryStart(FollowDefaultAudioOutput)", StringComparison.Ordinal),
            "Host nie uruchamia obserwatora urządzenia domyślnego.");
        var dispose = host.IndexOf("_defaultOutputWatcher?.Dispose();", StringComparison.Ordinal);
        var filesDispose = host.IndexOf("_files.Dispose();", StringComparison.Ordinal);
        Check(dispose >= 0 && filesDispose > dispose,
            "Obserwator nie jest zamykany przed wyjściami odtwarzania.");
        var follow = host.IndexOf("private void FollowDefaultAudioOutput", StringComparison.Ordinal);
        Check(follow >= 0, "Brak obsługi zmiany urządzenia domyślnego w hoście.");
        var body = host[follow..host.IndexOf("    private object ListOutputs(JsonElement args)", follow, StringComparison.Ordinal)];
        var filesActive = body.IndexOf("AudioOutputDeviceCatalog.IsEndpointActive(filesEndpoint)", StringComparison.Ordinal);
        var move = body.IndexOf("_files.MoveOutputToConfiguredDevice()", StringComparison.Ordinal);
        Check(filesActive >= 0 && move > filesActive,
            "Wyjście plików przechodzi na nowe urządzenie także po odłączeniu starego.");
        Check(body.Contains("AudioOutputDeviceCatalog.IsEndpointActive(radioEndpoint)", StringComparison.Ordinal),
            "Radio przechodzi na nowe urządzenie także po odłączeniu starego.");
        Check(body.IndexOf("Task.Run(", StringComparison.Ordinal) is var background
                && background >= 0 && move > background,
            "Przeniesienie wyjścia plików blokuje wątek obserwatora urządzeń.");
        Check(!body.Contains("RunLocalPlaybackStart", StringComparison.Ordinal)
                && !body.Contains("_files.Stop()", StringComparison.Ordinal),
            "Przejście za urządzeniem domyślnym otwiera materiał od nowa albo czeka pod blokadą edycji klipów.");

        var media = ReadRepositoryFile(Path.Combine(
            "src", "AccessibleMediaController.Windows", "Services", "WindowsMediaOutput.cs"));
        var recovery = media.IndexOf("private bool TryBeginOutputDeviceRecovery", StringComparison.Ordinal);
        Check(recovery >= 0, "Brak odzyskiwania po utracie urządzenia.");
        var recoveryBody = media[recovery..media.IndexOf("private bool ReplacePlaybackOutput", recovery, StringComparison.Ordinal)];
        var failed = recoveryBody.IndexOf("var failedLease = pipeline.OutputLease;", StringComparison.Ordinal);
        var replaceFirst = recoveryBody.IndexOf("if (ReplacePlaybackOutput(pipeline, outputDeviceId, failedLease)) return;", StringComparison.Ordinal);
        var reopenAfter = recoveryBody.IndexOf("RestartPipelineAfterResumeFailure(pipeline, position, volume, rate, exception);", StringComparison.Ordinal);
        Check(recoveryBody.Contains("Task.Run(", StringComparison.Ordinal)
                && failed >= 0 && replaceFirst > failed && reopenAfter > replaceFirst,
            "Po utracie urządzenia AMC nie przenosi najpierw samego wyjścia (poza wątkiem starego, z jego identyfikacją).");

        var replace = media.IndexOf("private bool ReplacePlaybackOutput", StringComparison.Ordinal);
        var replaceBody = media[replace..media.IndexOf("private bool RestartPipelineAfterResumeFailure", replace, StringComparison.Ordinal)];
        var serialized = replaceBody.IndexOf("lock (pipeline.OutputSwapGate)", StringComparison.Ordinal);
        var initNew = replaceBody.IndexOf("output.Init(pipeline.OutputReadMonitor.ToWaveProvider());", StringComparison.Ordinal);
        var decidePlay = replaceBody.IndexOf("play = !_pauseRequested && previous.Output.PlaybackState == PlaybackState.Playing;", StringComparison.Ordinal);
        var swapLease = replaceBody.IndexOf("pipeline.ReplaceOutputLease(replacement);", StringComparison.Ordinal);
        var closeOld = replaceBody.IndexOf("previous.Dispose();", StringComparison.Ordinal);
        var playNew = replaceBody.IndexOf("if (play)", StringComparison.Ordinal);
        Check(serialized >= 0 && initNew > serialized && decidePlay > initNew && swapLease > decidePlay
                && closeOld > swapLease && playNew > closeOld,
            "Podmiana wyjścia nie trzyma kolejności: nowe otwarte, decyzja i podmiana pod blokadą, stare zamknięte, dopiero potem gra nowe.");
        var disposePipeline = media.IndexOf("private static void DisposePipeline", StringComparison.Ordinal);
        Check(disposePipeline >= 0
                && media.IndexOf("lock (pipeline.OutputSwapGate)", disposePipeline, StringComparison.Ordinal) > disposePipeline,
            "Zamykanie toru może działać równolegle z podmianą wyjścia.");
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
