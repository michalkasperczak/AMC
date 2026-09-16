using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using AccessibleMediaController.Core.Tidal;
using Windows.Media.Control;

namespace AccessibleMediaController.Windows.Services;

/// <summary>Co wiadomo o utworze grajacym w cudzej aplikacji.</summary>
public sealed record ExternalPlaybackState
{
    public bool HasSession { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Artist { get; init; } = string.Empty;
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Ile utworu juz minelo. Windows podaje to jako migawke ze znacznikiem
    /// czasu (LastUpdatedTime) i NIE odliczajac dalej, wiec przy grajacym
    /// utworze trzeba doliczyc czas, ktory uplynal od tej migawki - inaczej
    /// czas od poczatku stoi w miejscu.
    /// </summary>
    public TimeSpan Position { get; init; }

    /// <summary>
    /// Czy program w ogole wystawia polozenie na zewnatrz. Gdy nie - lepiej
    /// powiedziec "nieznany" niz podac zero, ktore brzmi jak poczatek utworu.
    /// </summary>
    public bool HasPosition { get; init; }
    public bool IsPlaying { get; init; }

    public static ExternalPlaybackState None => new();
}

/// <summary>
/// Steruje utworem, ktory GRA w oryginalnej aplikacji TIDAL.
///
/// Powod istnienia: TidalDesktopController potrafi WSKAZAC utwor (otwiera strone
/// i klika przycisk odtwarzania), ale po starcie odtwarzania AMC tracilo z TIDALem
/// kontakt. Spacja, nastepny i poprzedni szly do wbudowanego odtwarzacza w
/// WebView2, czyli tam, gdzie sa tylko 30-sekundowe probki. Uzytkownik zglaszal
/// to slowami: "sterowanie przekazuje tylko pojedyncze nagranie".
///
/// Droga: Windows.Media.Control (SMTC) - oficjalne API Microsoftu do programow
/// sterujacych odtwarzaniem w innych aplikacjach. Nie lamie regulaminu TIDALa,
/// bo dzwiek w calosci odtwarza TIDAL; wysylamy te same polecenia co klawisze
/// multimedialne klawiatury.
///
/// WSZYSTKO PONIZEJ ZMIERZONE 11.09.2026 na maszynie uzytkownika, sondy 5-10:
/// - TIDAL zglasza sie jako SourceAppUserModelId "com.squirrel.TIDAL.TIDAL";
/// - odtwarzanie jest PELNE, nie probka (po 105 s nadal Playing, dlugosc 4:09);
/// - sterowanie dziala przy UKRYTYM oknie TIDALa, muzyka sie nie urywa;
/// - TIDAL nie zglasza POZYCJI odtwarzania (zawsze zero), dlugosc owszem.
///
/// DWIE PULAPKI, ktore musza tu zostac:
/// 1. AMC SAMO publikuje sesje SMTC (jego WebView2 tez). Bez filtra program
///    sterowalby soba - petla. Dlatego wymagamy "tidal" w identyfikatorze
///    i odrzucamy wszystko z "webview" oraz wlasny proces.
/// 2. TIDAL ma IsPauseEnabled=False, ale IsPlayPauseToggleEnabled=True.
///    Do pauzy TRZEBA uzyc TryTogglePlayPauseAsync; TryPauseAsync nic nie robi.
/// </summary>
public sealed class ExternalMediaController
{
    private const string TidalMarker = "tidal";
    private const string WebViewMarker = "webview";

    private readonly Action<string>? _log;

    public ExternalMediaController(Action<string>? log = null) => _log = log;

    private async Task<GlobalSystemMediaTransportControlsSession?> FindTidalSessionAsync(
        CancellationToken token)
    {
        try
        {
            var manager = await GlobalSystemMediaTransportControlsSessionManager
                .RequestAsync()
                .AsTask(token)
                .ConfigureAwait(false);

            foreach (var session in manager.GetSessions())
            {
                var id = session.SourceAppUserModelId ?? string.Empty;
                if (id.Contains(TidalMarker, StringComparison.OrdinalIgnoreCase)
                    && !id.Contains(WebViewMarker, StringComparison.OrdinalIgnoreCase))
                {
                    return session;
                }
            }
        }
        catch (Exception exception)
        {
            // Sesja bez pulpitu (np. uruchomienie przez usluge) rzuca
            // "Okreslona usluga nie istnieje jako usluga zainstalowana."
            // To nie blad kodu - po prostu nie ma czym sterowac.
            _log?.Invoke($"Sterowanie zewnetrzne niedostepne: {exception.Message}");
        }
        return null;
    }

    /// <summary>Czy jest czym sterowac. Nie zmienia niczego.</summary>
    public async Task<bool> IsAvailableAsync(CancellationToken token = default) =>
        await FindTidalSessionAsync(token).ConfigureAwait(false) is not null;

    /// <summary>Co gra w oryginalnym TIDALu.</summary>
    public async Task<ExternalPlaybackState> GetStateAsync(CancellationToken token = default)
    {
        var session = await FindTidalSessionAsync(token).ConfigureAwait(false);
        if (session is null) return ExternalPlaybackState.None;

        try
        {
            var media = await session.TryGetMediaPropertiesAsync().AsTask(token).ConfigureAwait(false);
            var timeline = session.GetTimelineProperties();
            var playback = session.GetPlaybackInfo();
            var isPlaying = playback?.PlaybackStatus
                == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;

            // Rachunek siedzi w czesci wspolnej (ExternalPlaybackTime), bo tylko
            // tam da sie go sprawdzic testami bez zywej sesji multimediow.
            var czas = ExternalPlaybackTime.Compute(
                position: timeline?.Position ?? TimeSpan.Zero,
                endTime: timeline?.EndTime ?? TimeSpan.Zero,
                startTime: timeline?.StartTime ?? TimeSpan.Zero,
                lastUpdated: timeline?.LastUpdatedTime ?? default,
                isPlaying: isPlaying,
                now: DateTimeOffset.UtcNow);

            return new ExternalPlaybackState
            {
                HasSession = true,
                Title = media?.Title ?? string.Empty,
                Artist = media?.Artist ?? string.Empty,
                Duration = czas.Duration,
                Position = czas.Position,
                HasPosition = czas.HasPosition,
                IsPlaying = isPlaying
            };
        }
        catch (Exception exception)
        {
            _log?.Invoke($"Nie udalo sie odczytac stanu TIDALa: {exception.Message}");
            return ExternalPlaybackState.None;
        }
    }

    private async Task<bool> RunAsync(
        string name,
        Func<GlobalSystemMediaTransportControlsSession, IAsyncOperation<bool>> action,
        CancellationToken token)
    {
        var session = await FindTidalSessionAsync(token).ConfigureAwait(false);
        if (session is null)
        {
            _log?.Invoke($"{name}: brak sesji oryginalnego TIDALa.");
            return false;
        }
        try
        {
            var done = await action(session).AsTask(token).ConfigureAwait(false);
            _log?.Invoke($"{name}: {(done ? "wykonane" : "odrzucone przez TIDALa")}.");
            return done;
        }
        catch (Exception exception)
        {
            _log?.Invoke($"{name} nie przeszlo: {exception.Message}");
            return false;
        }
    }

    /// <summary>
    /// Gra albo pauza. ZAWSZE przez przelacznik - TIDAL zglasza IsPauseEnabled=False,
    /// wiec osobne TryPauseAsync nie ma skutku (zmierzone 11.09.2026).
    /// </summary>
    public Task<bool> TogglePlayPauseAsync(CancellationToken token = default) =>
        RunAsync("Gra/pauza", s => s.TryTogglePlayPauseAsync(), token);

    public Task<bool> NextAsync(CancellationToken token = default) =>
        RunAsync("Nastepny", s => s.TrySkipNextAsync(), token);

    public Task<bool> PreviousAsync(CancellationToken token = default) =>
        RunAsync("Poprzedni", s => s.TrySkipPreviousAsync(), token);

    public Task<bool> StopAsync(CancellationToken token = default) =>
        RunAsync("Zatrzymaj", s => s.TryStopAsync(), token);
}
