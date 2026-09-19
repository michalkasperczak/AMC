using AccessibleMediaController.Windows.Services;

internal static class YouTubePremiereTests
{
    internal const string Notice = "Ten materiał oczekuje na premierę. Nie można go jeszcze odtworzyć ani pobrać.";
    private const string Url = "https://www.youtube.com/watch?v=Wo9LmGPN6ko";
    internal static void Run()
    {
        var observed = "ERROR: [youtube] Wo9LmGPN6ko: Premieres in 18 hours\n";
        if (YouTubeErrorTranslator.Describe(observed) != Notice)
            throw new Exception("Premiera jest opisana jako ogólna niedostępność zamiast jasnego komunikatu o oczekiwaniu.");
        foreach (var polish in new[] { "Premiera za 18 godzin", "Premiera za\u00a018 godzin", "Premiera za\ufffd18 godzin" })
        {
            if (YouTubeErrorTranslator.Describe("ERROR: [youtube] Wo9LmGPN6ko: " + polish) != Notice)
                throw new Exception("Polska odpowiedź używana przy Ctrl+D nie rozpoznaje premiery.");
        }
        foreach (var other in new[] { "", "ERROR: Private video", "ERROR: This live event has ended", "Premiered 2 hours ago" })
        {
            if (YouTubeErrorTranslator.DescribePremiere(other) is not null)
                throw new Exception("Inny stan został błędnie uznany za oczekiwanie na premierę.");
        }
        if (YouTubeErrorTranslator.DescribePremiere(null) is not null)
            throw new Exception("Brak błędu został uznany za premierę.");
        if (YouTubeErrorTranslator.Describe("ERROR: Private video") != "To nagranie jest prywatne.")
            throw new Exception("Zmieniono obsługę filmu prywatnego.");
        if (YouTubeErrorTranslator.Describe("ERROR: This live event has ended") != "Ta transmisja już się zakończyła. Nagranie nie zostało udostępnione.")
            throw new Exception("Zmieniono obsługę zakończonej transmisji.");
        if (WindowsMediaOutput.FriendlyPlaybackError(new InvalidDataException(Notice)) != Notice)
            throw new Exception("Odtwarzacz zastępuje informację o premierze ogólnym błędem formatu dźwięku.");
        if (WindowsMediaOutput.FriendlyPlaybackError(new InvalidDataException("bad audio"))
            != "Plik ma nieobsługiwany albo uszkodzony format dźwięku.")
            throw new Exception("Zmieniono komunikat rzeczywiście uszkodzonego dźwięku.");
        Console.WriteLine("OK: komunikat premiery YouTube po angielsku i polsku, także przez obsługę błędów odtwarzacza, bez zmiany innych przyczyn");
    }

    // Explicit, time-dependent live probe; never part of the normal suite.
    internal static void RunLive(bool includeDownload)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        ExpectNotice(() => YouTubeSourceResolver.ResolveAudioAsync(Url, timeout.Token).GetAwaiter().GetResult(), "odtwarzanie");
        if (!includeDownload) return;
        var root = Path.Combine(Path.GetTempPath(), "amc-premiere-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var destination = Path.Combine(root, "premiere.mp3");
            ExpectNotice(() => YouTubeMediaDownloader.DownloadMp3Async(Url, destination, timeout.Token, overwrite: false).GetAwaiter().GetResult(), "pobieranie MP3");
            if (File.Exists(destination)) throw new Exception("Nieudana premiera utworzyła gotowy plik.");
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    internal static void ShowForNvda()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var app = new System.Windows.Application();
                var root = Path.Combine(Path.GetTempPath(), "amc-premiere-gui-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(root);
                DiagnosticLog.Initialize(Path.Combine(root, "logs"));
                SynchronizationContext.SetSynchronizationContext(
                    new System.Windows.Threading.DispatcherSynchronizationContext(app.Dispatcher));
                var store = new AccessibleMediaController.Core.Configuration.ConfigurationStore(Path.Combine(root, "state.json"));
                var state = store.LoadOrCreate();
                state.Settings.Updates.CheckAutomatically = false;
                state.Settings.LastSessionId = "podcasts";
                state.Radio.RecordingSchedules.Clear();
                state.Podcasts.Volume = 0;
                state.Podcasts.DownloadsFolder = Path.Combine(root, "downloads");
                state.Podcasts.Subscriptions.Add(new()
                {
                    Id = "premiere-probe", Title = "Próba premiery YouTube",
                    SourceKind = AccessibleMediaController.Core.Configuration.PodcastSourceKind.PublicInternetMedia,
                    FeedUrl = Url, LastRefreshUtcTicks = DateTime.UtcNow.Ticks, IsInLibrary = true
                });
                state.Podcasts.Episodes.Add(new()
                {
                    Id = "premiere-probe-item", SubscriptionId = "premiere-probe", SourceIdentifier = Url,
                    Title = "Próba premiery YouTube — Henryk Kuźniak", MediaUrl = Url, PageUrl = Url,
                    MediaType = "video/youtube", IsNew = true, PublishedUtcTicks = DateTime.UtcNow.Ticks
                });
                var window = new AccessibleMediaController.Windows.MainWindow(state, store);
                Console.WriteLine("GUI_PRIVATE_ROOT=" + root);
                app.Run(window);
            }
            catch (Exception e) { failure = e; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) throw new Exception("Próba NVDA premiery", failure);
    }

    private static void ExpectNotice(Action action, string label)
    {
        try { action(); }
        catch (InvalidDataException e)
        {
            if (e.Message != Notice) throw new Exception(label + ": otrzymano: " + e.Message, e);
            Console.WriteLine("OK: prawdziwe yt-dlp, " + label + ": " + e.Message);
            return;
        }
        throw new Exception(label + ": adres nie jest już premierą; pomiar nie potwierdza badanego przypadku.");
    }
}
