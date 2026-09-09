using System.Reflection;
using System.Text.Json;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Windows.Services;
using Microsoft.Web.WebView2.Wpf;

internal static class TidalPlaybackSmokeTests
{
    internal static void Run()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { TestBridge(); }
            catch (Exception exception) { failure = exception; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(15))) throw new Exception("TIDAL: test mostka przekroczył limit czasu.");
        if (failure is not null) throw new Exception("TIDAL: test mostka", failure);
        Console.WriteLine("OK: TIDAL — gest odtwarzania, wersjonowanie zdarzeń, błąd/koniec/próbka i kolejka");
    }

    private static void TestBridge()
    {
        using var integration = new TidalIntegrationService(new TidalSettings());
        using var output = new TidalMediaOutput(integration, new WebView2());
        var item = new MediaItem { Id = "tidal-one", ExternalId = "tracks:one", Title = "One", Kind = MediaItemKind.Track, IsInQueue = true };
        var session = new DemoMediaSession("tidal", "TIDAL", [item]);
        int ended = 0, failed = 0, started = 0;
        string? lastNotice = null;
        output.PlaybackNotice += (_, args) => lastNotice = args.Message;
        output.PlaybackEnded += (_, args) => { ended++; session.ContinueAfterPlaybackEnded(args.Item); };
        output.PlaybackFailed += (_, _) => { failed++; session.MarkPlaybackFailed(); };
        output.PlaybackStarted += (_, _) => started++;
        void Seed(int version)
        {
            Set("currentItem", item); Set("loadedItemId", item.Id);
            Set("playbackRequestVersion", version); Set("playbackStarted", false);
            Set("isPreparing", true); Set("wasPlaying", false); Set("isPreview", false);
        }
        void Set(string name, object value) => typeof(TidalMediaOutput)
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(output, value);
        void Message(string type, int version, string id = "one", string state = "PLAYING", string reason = "completed") =>
            output.ProcessBridgeMessage(JsonSerializer.Serialize(new
            {
                type, requestVersion = version, productId = id, state, reason, position = 60, duration = 60,
                message = "play() failed because the user didn't interact with the document first.", id = "NotAllowedError"
            }));

        Seed(2);
        Message("state", 1); Message("error", 1); Message("ended", 1);
        Message("state", 2, "other"); Message("ended", 2, "");
        Check(started == 0 && failed == 0 && ended == 0, "Spóźnione/obce zdarzenie zmieniło nowy utwór.");
        Message("ended", 2);
        Check(ended == 0, "Koniec przed rozpoczęciem zużył kolejkę.");
        Message("error", 2); Message("state", 2); Message("ended", 2); Message("error", 2);
        Check(failed == 1 && started == 0 && ended == 0 && item.IsInQueue, "Błąd uruchomił następny utwór lub usunął kolejkę.");

        Seed(3); Message("state", 3); Message("ended", 3, reason: "error"); Message("ended", 3, reason: "skip");
        Check(ended == 0, "Błąd lub skip SDK jest mylony z naturalnym końcem.");
        output.Pause(); Message("ended", 3);
        Check(ended == 0, "Pauza uruchomiła następny utwór.");
        Seed(5);
        output.ProcessBridgeMessage("{\"type\":\"transition\",\"requestVersion\":5,\"productId\":\"one\",\"assetPresentation\":\"PREVIEW\",\"previewReason\":\"FULL_REQUIRES_HIGHER_ACCESS_TIER\",\"duration\":29.953}");
        Check(lastNotice?.Contains("dostępu aplikacji") == true && Math.Abs(item.Duration.TotalSeconds - 29.953) < 0.001,
            "Mostek nie przekazał użytkowego powodu lub rzeczywistego czasu próbki.");
        Message("state", 5); Message("ended", 5);
        Check(ended == 0 && failed == 2 && item.IsInQueue, "Próbka zużyła pełny utwór z kolejki.");
        Seed(6); Message("state", 6); Message("ended", 6); Message("ended", 6);
        Check(ended == 1 && !item.IsInQueue, "Naturalny koniec powinien wystąpić dokładnie raz.");

        var policy = TidalWebViewPolicy.CreateOptions().AdditionalBrowserArguments;
        Check(policy == "--autoplay-policy=no-user-gesture-required", "Brak polityki odtwarzania dla poleceń natywnych.");
        var friendly = TidalMediaOutput.FriendlyFailure("NotAllowedError: user didn't interact");
        Check(friendly.Contains("integracji AMC") && !friendly.Contains("zaloguj"), "Błąd gestu mylony z autoryzacją.");
        var safe = TidalMediaOutput.SafeDiagnostic("https://private.test/media?token=secret Bearer abc access_token=def eyJabc.xyz.sig");
        Check(!safe.Contains("secret") && !safe.Contains("abc") && !safe.Contains("def") && !safe.Contains("xyz"), "Log ujawnia dane wrażliwe.");
        Check(TidalMediaOutput.NormalizePreviewReason("FULL_REQUIRES_HIGHER_ACCESS_TIER") == "FULL_REQUIRES_HIGHER_ACCESS_TIER",
            "Zgubiono rzeczywisty powód próbki.");
        Check(TidalMediaOutput.NormalizePreviewReason("") == "NOT_PROVIDED"
              && TidalMediaOutput.NormalizePreviewReason("https://private.test/?token=secret\nInjected") == "UNKNOWN",
            "Diagnostyka powodu próbki przyjęła dowolne dane.");
        Check(!TidalMediaOutput.PreviewNotice("UNKNOWN").Contains("wyższego poziomu")
              && TidalMediaOutput.PreviewNotice("FULL_REQUIRES_HIGHER_ACCESS_TIER").Contains("dostępu aplikacji")
              && TidalMediaOutput.PreviewNotice("FULL_REQUIRES_SUBSCRIPTION").Contains("subskrypcji")
              && TidalMediaOutput.PreviewNotice("FULL_REQUIRES_PURCHASE").Contains("zakupu"),
            "Komunikat zgaduje przyczynę próbki lub myli konto z aplikacją.");
        Check(!TidalMediaOutput.FriendlyFailure("S3016 EUnexpected").Contains("wyższego poziomu"),
            "Ogólny błąd SDK fałszywie diagnozuje poziom dostępu.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
