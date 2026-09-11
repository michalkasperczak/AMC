using System.Reflection;
using System.Text.Json;
using System.Net;
using System.Net.Http;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using AccessibleMediaController.Windows;
using AccessibleMediaController.Core.Tidal;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Windows.Services;
using Microsoft.Web.WebView2.Wpf;

internal static class TidalPlaybackSmokeTests
{
    internal static void Run()
    {
        Task.Run(TestPlaybackCredentials).WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
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
        output.Diagnostics.Begin(item.Title, false);
        output.Diagnostics.CredentialsPrepared();
        output.ProcessBridgeMessage("{\"type\":\"credentials\",\"requestVersion\":4,\"productId\":\"one\",\"authenticatedUser\":true}");
        Check(output.Diagnostics.Report.Contains("odczytane przez SDK: nie potwierdzono"), "Obcy raport logowania potwierdził nową próbę.");
        output.ProcessBridgeMessage("{\"type\":\"credentials\",\"requestVersion\":5,\"productId\":\"one\",\"authenticatedUser\":true}");
        output.ProcessBridgeMessage("{\"type\":\"transition\",\"requestVersion\":5,\"productId\":\"one\",\"assetPresentation\":\"PREVIEW\",\"previewReason\":\"FULL_REQUIRES_HIGHER_ACCESS_TIER\",\"duration\":29.953}");
        Check(output.Diagnostics.Report.Contains("odczytane przez SDK: tak")
              && output.Diagnostics.Report.Contains("Udostępniony materiał: próbka")
              && output.Diagnostics.Report.Contains("dostępu do pełnego odtwarzania")
              && output.Diagnostics.Report.Contains("29,953 s"), "Raport nie rozróżnia logowania i próbki.");
        Check(lastNotice?.Contains("dostępu do pełnego odtwarzania") == true && Math.Abs(item.Duration.TotalSeconds - 29.953) < 0.001,
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
              && TidalMediaOutput.PreviewNotice("FULL_REQUIRES_HIGHER_ACCESS_TIER").Contains("dostępu do pełnego odtwarzania")
              && TidalMediaOutput.PreviewNotice("FULL_REQUIRES_SUBSCRIPTION").Contains("subskrypcji")
              && TidalMediaOutput.PreviewNotice("FULL_REQUIRES_PURCHASE").Contains("zakupu"),
            "Komunikat zgaduje przyczynę próbki lub myli konto z aplikacją.");
        // Komunikat o próbce nie może sugerować, że winna jest subskrypcja
        // użytkownika: TIDAL udostępnia aplikacjom zewnętrznym tylko próbki.
        Check(TidalMediaOutput.PreviewNotice("FULL_REQUIRES_SUBSCRIPTION").Contains("aplikacjom zewnętrznym")
              && TidalMediaOutput.PreviewNotice("FULL_REQUIRES_SUBSCRIPTION").Contains("niezależnie od Twojej subskrypcji"),
            "Komunikat o próbce zrzuca winę na subskrypcję użytkownika.");
        // Każdy komunikat o próbce podaje jej długość, żeby użytkownik czytnika
        // ekranu wiedział, czego się spodziewać, zanim dźwięk się urwie.
        Check(TidalMediaOutput.PreviewNotice("UNKNOWN").Contains("30 sekund")
              && TidalMediaOutput.PreviewNotice("FULL_REQUIRES_SUBSCRIPTION").Contains("30 sekund")
              && TidalMediaOutput.PreviewNotice("FULL_REQUIRES_HIGHER_ACCESS_TIER").Contains("30 sekund")
              && TidalMediaOutput.PreviewNotice("FULL_REQUIRES_PURCHASE").Contains("30 sekund"),
            "Komunikat o próbce nie podaje jej długości.");
        Check(!TidalMediaOutput.FriendlyFailure("S3016 EUnexpected").Contains("wyższego poziomu"),
            "Ogólny błąd SDK fałszywie diagnozuje poziom dostępu.");
        var diagnostics = new TidalPlaybackDiagnostics();
        Check(diagnostics.Report.Contains("Nie wykonano jeszcze"), "Pusty raport sugeruje wynik testu.");
        diagnostics.Begin("Drugi utwór", true);
        diagnostics.Transition("SECRET_PRESENTATION", "https://private/?token=secret", double.NaN);
        Check(!diagnostics.Report.Contains("secret") && !diagnostics.Report.Contains("SECRET_PRESENTATION")
              && diagnostics.Report.Contains("brak potwierdzonej odpowiedzi"), "Raport ujawnia dowolne dane SDK.");
        diagnostics.Transition("FULL", "", 240);
        Check(diagnostics.Report.Contains("wymaga sprawdzenia odsłuchem"), "Sam FULL deklaruje potwierdzenie odsłuchu.");
        diagnostics.Begin("Trzeci utwór", false);
        Check(!diagnostics.Report.Contains("240 s") && diagnostics.Report.Contains("odczytane przez SDK: nie potwierdzono"),
            "Raport nowego utworu zachował wynik poprzedniego.");
        var accountWindow = new TidalAccountWindow(new TidalSettings(), integration, () => diagnostics.Report);
        var diagnosticsButton = (Button)accountWindow.FindName("DiagnosticsButton");
        Check(UIElementAutomationPeer.CreatePeerForElement(diagnosticsButton)!.GetName() == "Diagnostyka odtwarzania",
            "Przycisk diagnostyki nie ma jawnej nazwy dostępnościowej.");
        accountWindow.Close();
        // Okno konta nie może zamknąć się w trakcie logowania lub synchronizacji:
        // wcześniej zamknięcie anulowało operację w ciszy, użytkownik zostawał
        // z pustą Biblioteką TIDAL i bez informacji, że nic nie zostało pobrane.
        var busyWindow = new TidalAccountWindow(new TidalSettings(), integration, () => diagnostics.Report);
        var busyField = typeof(TidalAccountWindow).GetField("busy", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var closing = typeof(TidalAccountWindow).GetMethod("Window_Closing", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var wolne = new System.ComponentModel.CancelEventArgs();
        busyField.SetValue(busyWindow, false);
        closing.Invoke(busyWindow, [null, wolne]);
        Check(!wolne.Cancel, "Wolne okno konta TIDAL nie daje się zamknąć.");
        var zajete = new System.ComponentModel.CancelEventArgs();
        busyField.SetValue(busyWindow, true);
        closing.Invoke(busyWindow, [null, zajete]);
        Check(zajete.Cancel, "Okno konta TIDAL zamyka się w trakcie operacji i po cichu ją przerywa.");
        busyField.SetValue(busyWindow, false);
        busyWindow.Close();
        var information = new InformationWindow(diagnostics.Report, windowTitle: "Diagnostyka odtwarzania TIDAL");
        var text = (System.Windows.Forms.RichTextBox)typeof(InformationWindow)
            .GetField("_informationBox", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(information)!;
        Check(text.ReadOnly && text.Multiline && text.ShortcutsEnabled && text.Text.Contains("Trzeci utwór")
              && text.AccessibleName == "Diagnostyka odtwarzania TIDAL", "Raport nie jest czytelnym, zaznaczalnym tekstem.");
        information.Close();
        text.Dispose();
    }

    private static async Task TestPlaybackCredentials()
    {
        using var handler = new AccountHandler();
        using var http = new HttpClient(handler);
        var reads = 0;
        var tokens = new TidalTokenSet("first-test-secret", "test-refresh", DateTimeOffset.UtcNow.AddHours(1), "user.read collection.read", "test-client");
        using var integration = new TidalIntegrationService(new TidalSettings(), new TidalApiClient(http),
            _ => { reads++; return Task.FromResult(tokens); });
        var first = await integration.GetPlaybackCredentialsAsync(CancellationToken.None);
        Check(first.UserId == "test-user" && first.ClientId == "test-client"
              && first.AccessToken == tokens.AccessToken && first.ExpiresAtUtc == tokens.ExpiresAtUtc
              && first.Scopes.SequenceEqual(["user.read", "collection.read"]), "Niepełne przekazanie poświadczenia do silnika.");
        tokens = tokens with { AccessToken = "second-test-secret", ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(2) };
        var second = await integration.GetPlaybackCredentialsAsync(CancellationToken.None);
        Check(reads == 2 && handler.Count == 1 && second.AccessToken == tokens.AccessToken,
            "Ponowne przygotowanie nie odczytało nowego tokenu albo ponownie pobierało ten sam profil.");
        tokens = tokens with { ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1) };
        try
        {
            await integration.GetPlaybackCredentialsAsync(CancellationToken.None);
            throw new Exception("Wygasłe poświadczenie przekazano do silnika.");
        }
        catch (InvalidOperationException exception) when (exception.Message.Contains("invalid_token")) { }
    }

    private sealed class AccountHandler : HttpMessageHandler
    {
        internal int Count;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Count++;
            Check(request.RequestUri?.AbsolutePath.EndsWith("/users/me") == true
                  && request.Headers.Authorization?.Parameter == "first-test-secret", "Żądanie profilu nie używa tokenu użytkownika.");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"data\":{\"type\":\"users\",\"id\":\"test-user\"}}")
            });
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
