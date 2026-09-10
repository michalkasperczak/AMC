using System.Globalization;

namespace AccessibleMediaController.Windows.Services;

// Deliberately contains no credentials, account IDs, URLs or arbitrary SDK
// error text. A report is an observation, not a claim of playback entitlement.
internal sealed class TidalPlaybackDiagnostics
{
    private readonly object gate = new();
    private string title = string.Empty;
    private DateTimeOffset? started;
    private bool resumed, prepared, readBySdk, playing;
    private string presentation = string.Empty, previewReason = string.Empty, failure = string.Empty;
    private double duration;

    internal void Begin(string itemTitle, bool isResume)
    {
        lock (gate)
        {
            title = itemTitle;
            started = DateTimeOffset.Now;
            resumed = isResume;
            prepared = readBySdk = playing = false;
            presentation = previewReason = failure = string.Empty;
            duration = 0;
        }
    }

    internal void CredentialsPrepared() { lock (gate) prepared = true; }
    internal void CredentialsRead() { lock (gate) readBySdk = true; }
    internal void PlaybackStarted() { lock (gate) playing = true; }
    internal void Failure(string friendlyMessage) { lock (gate) failure = friendlyMessage; }

    internal void Transition(string assetPresentation, string reason, double seconds)
    {
        lock (gate)
        {
            presentation = assetPresentation is "FULL" or "PREVIEW" ? assetPresentation : string.Empty;
            previewReason = TidalMediaOutput.NormalizePreviewReason(reason);
            duration = double.IsFinite(seconds) ? Math.Clamp(seconds, 0, 604800) : 0;
        }
    }

    internal string Report
    {
        get
        {
            lock (gate)
            {
                if (started is null)
                    return "Nie wykonano jeszcze próby odtwarzania TIDAL w tym uruchomieniu AMC.\n\n" +
                        "Zamknij to okno i ustawienia konta klawiszem Escape, następnie odtwórz jeden utwór. " +
                        "Wróć do Ctrl+F5 i wybierz Diagnostyka odtwarzania. Nie trzeba ponownie się logować.";
                var material = presentation switch
                {
                    "FULL" => "pełny materiał według odpowiedzi TIDAL; wymaga sprawdzenia odsłuchem",
                    "PREVIEW" => "próbka",
                    _ => "brak potwierdzonej odpowiedzi"
                };
                var reason = presentation == "PREVIEW" ? TidalMediaOutput.PreviewNotice(previewReason) : "nie dotyczy lub brak danych";
                return $"Diagnostyka ostatniej próby TIDAL\n" +
                    $"Utwór: {title}\nPróba: {started:dd.MM.yyyy HH:mm:ss}\n" +
                    $"Działanie: {(resumed ? "wznowienie po pauzie" : "otwarcie utworu")}\n" +
                    $"Aktualne logowanie przygotowane dla silnika: {(prepared ? "tak" : "nie potwierdzono")}\n" +
                    $"Logowanie użytkownika odczytane przez SDK: {(readBySdk ? "tak" : "nie potwierdzono")}\n" +
                    $"Rozpoczęcie odtwarzania potwierdzone przez SDK: {(playing ? "tak" : "nie potwierdzono")}\n" +
                    $"Udostępniony materiał: {material}\nPowód ograniczenia: {reason}\n" +
                    $"Długość udostępnionego materiału: {(duration > 0 ? duration.ToString("0.###", CultureInfo.GetCultureInfo("pl-PL")) + " s" : "brak danych")}\n" +
                    $"Wynik błędu: {(failure.Length > 0 ? failure : "nie zgłoszono")}\n\n" +
                    "Potwierdzenie logowania nie oznacza prawa do pełnego odtwarzania. Brak odpowiedzi nie oznacza próbki. " +
                    "Raport dotyczy ostatniej próby w tym uruchomieniu, nie sprawdza obecnej ważności konta i nie zawiera tokenów. " +
                    "Kopiuj wszystko kopiuje raport, nie zamykając okna. Escape wraca do ustawień konta.";
            }
        }
    }
}
