using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AccessibleMediaController.Core.Tidal;

namespace AccessibleMediaController.Windows.Services;

/// <summary>
/// Powod, dla ktorego nie da sie sterowac oryginalnym TIDALem.
/// </summary>
public enum TidalDesktopReadiness
{
    /// <summary>Port sterowania odpowiada, mozna grac.</summary>
    Ready,

    /// <summary>TIDAL nie jest w ogole uruchomiony - wolno go uruchomic samemu.</summary>
    NotRunning,

    /// <summary>TIDAL dziala, ale bez portu sterowania. NIE zamykac go bez zgody uzytkownika.</summary>
    RunningWithoutControlPort,

    /// <summary>Nie znaleziono zainstalowanego TIDALa.</summary>
    NotInstalled
}

public sealed class TidalDesktopPlayResult
{
    public bool Success { get; init; }
    public bool WasAlreadyCurrent { get; init; }

    /// <summary>Zdanie dla uzytkownika. Nigdy puste - operacja nie moze konczyc sie cisza.</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>Ustawione, gdy trzeba spytac uzytkownika o ponowne uruchomienie TIDALa.</summary>
    public bool NeedsRestartConsent { get; init; }
}

/// <summary>
/// Uruchamia wskazany utwor, album albo playliste w ORYGINALNYM TIDAL desktop.
///
/// Dlaczego tak: TIDAL nie rozwiazuje odnosnikow tidal://track/&lt;id&gt; w
/// odtwarzanie (zmierzone na 6 utworach, 6/6 wraca do poprzedniej kolejki), a
/// jego SDK nie pozwala odtwarzac calych utworow w cudzym programie. Sam TIDAL
/// desktop jest jednak aplikacja Electron i uruchomiony z przelacznikiem
/// --remote-debugging-port oddaje sterowanie wlasnym interfejsem. Dzwiek robi w
/// calosci silnik TIDALa, wiec jakosc jest ta, ktora uzytkownik ma ustawiona
/// (zmierzone: zadanie playbackinfo z audioquality=HI_RES_LOSSLESS).
///
/// Czego ta klasa NIE robi celowo: nie zamyka dzialajacego TIDALa na wlasna
/// reke. TIDAL wstaje ok. 20 sekund, a uzytkownik moze wlasnie czegos sluchac -
/// dlatego brak portu sterowania konczy sie pytaniem, nie restartem.
/// </summary>
public interface ITidalDesktopPlayback
{
    Task<TidalDesktopPlayResult> PlayAsync(
        TidalDesktopPlayRequest request,
        bool restartConsent = false,
        CancellationToken token = default);
}

public sealed class TidalDesktopController : ITidalDesktopPlayback
{
    private const string ProcessName = "TIDAL";
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan PageLoadWait = TimeSpan.FromSeconds(12);

    private readonly Action<string>? _log;
    private readonly int _port;

    public TidalDesktopController(Action<string>? log = null, int port = TidalDesktopPlaybackPlan.DefaultDebuggingPort)
    {
        _log = log;
        _port = port;
    }

    /// <summary>
    /// Sprawdza, czy da sie sterowac, NIC nie zmieniajac. Wolne od skutkow
    /// ubocznych, zeby dalo sie tym odpowiedziec na pytanie uzytkownika.
    /// </summary>
    public async Task<TidalDesktopReadiness> CheckReadinessAsync(CancellationToken token = default)
    {
        if (await IsControlPortOpenAsync(token).ConfigureAwait(false))
        {
            return TidalDesktopReadiness.Ready;
        }

        if (IsTidalRunning())
        {
            return TidalDesktopReadiness.RunningWithoutControlPort;
        }

        return FindExecutable() is null
            ? TidalDesktopReadiness.NotInstalled
            : TidalDesktopReadiness.NotRunning;
    }

    /// <summary>
    /// Odtwarza wskazany element. <paramref name="restartConsent"/> ustaw na
    /// true tylko wtedy, gdy uzytkownik zgodzil sie na ponowne uruchomienie
    /// dzialajacego TIDALa.
    /// </summary>
    public async Task<TidalDesktopPlayResult> PlayAsync(
        TidalDesktopPlayRequest request,
        bool restartConsent = false,
        CancellationToken token = default)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));

        var readiness = await CheckReadinessAsync(token).ConfigureAwait(false);
        _log?.Invoke($"TIDAL desktop: stan przed odtworzeniem = {readiness}, element = {request.DisplayName}");

        switch (readiness)
        {
            case TidalDesktopReadiness.NotInstalled:
                return Failure("Nie znaleziono zainstalowanego oryginalnego programu TIDAL. Odtwarzanie w nim jest niemożliwe");

            case TidalDesktopReadiness.RunningWithoutControlPort when !restartConsent:
                _log?.Invoke("TIDAL desktop: dziala bez portu sterowania, pytam o zgode na ponowne uruchomienie");
                return new TidalDesktopPlayResult
                {
                    Success = false,
                    NeedsRestartConsent = true,
                    Message = "Oryginalny TIDAL jest już uruchomiony, ale bez możliwości sterowania. Aby AMC mogło przekazywać mu utwory, trzeba go zamknąć i uruchomić ponownie"
                };

            case TidalDesktopReadiness.RunningWithoutControlPort:
                _log?.Invoke("TIDAL desktop: uzytkownik zgodzil sie na ponowne uruchomienie");
                if (!await RestartWithControlPortAsync(token).ConfigureAwait(false))
                {
                    return Failure("Nie udało się uruchomić oryginalnego TIDALa z możliwością sterowania");
                }
                break;

            case TidalDesktopReadiness.NotRunning:
                _log?.Invoke("TIDAL desktop: nie dziala, uruchamiam z portem sterowania");
                if (!await StartWithControlPortAsync(token).ConfigureAwait(false))
                {
                    return Failure("Nie udało się uruchomić oryginalnego TIDALa");
                }
                break;
        }

        try
        {
            return await SendPlayAsync(request, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Cisza po anulowaniu jest przy czytniku ekranu nieodroznialna od
            // sukcesu, wiec anulowanie tez ma swoj komunikat i wpis w dzienniku.
            _log?.Invoke("TIDAL desktop: odtwarzanie przerwane przed potwierdzeniem");
            return Failure("Przekazywanie do oryginalnego TIDALa zostało przerwane");
        }
        catch (Exception ex)
        {
            _log?.Invoke($"TIDAL desktop: błąd sterowania: {ex.GetType().Name}: {ex.Message}");
            return Failure("Nie udało się przekazać elementu oryginalnemu TIDALowi");
        }
    }

    private async Task<TidalDesktopPlayResult> SendPlayAsync(TidalDesktopPlayRequest request, CancellationToken token)
    {
        var socketUri = await FindPageSocketAsync(token).ConfigureAwait(false);
        if (socketUri is null)
        {
            return Failure("Oryginalny TIDAL nie oddał sterowania swoim oknem");
        }

        using var socket = new ClientWebSocket();
        await socket.ConnectAsync(socketUri, token).ConfigureAwait(false);
        var session = new CdpSession(socket);

        await session.SendAsync("Runtime.enable", null, token).ConfigureAwait(false);
        await session.SendAsync("Page.navigate", new { url = request.PageUri }, token).ConfigureAwait(false);

        // Czekamy, az pojawi sie lista utworow, zamiast spac na sztywno.
        // Klikniecie w pusta jeszcze strone nic by nie dalo, a stale czekanie
        // kazalo by uzytkownikowi czekac tez wtedy, gdy strona jest gotowa.
        if (!await WaitForTrackListAsync(session, token).ConfigureAwait(false))
        {
            _log?.Invoke($"TIDAL desktop: strona {request.PageUri} nie pokazała listy utworów");
            return Failure($"Oryginalny TIDAL nie zdążył wczytać {request.DisplayName}");
        }

        // Zmierzone na TIDAL 2.43.2: utwor uruchamia klik przycisku odtwarzania
        // WEWNATRZ jego wiersza, a calosc - przycisk "odtwarzaj wszystko".
        // Klikamy metoda .click() na samym elemencie, nie wspolrzednymi: przy
        // przewinietej stronie wspolrzedne wychodza poza ekran i klik nie trafia.
        var expression = request.Kind == TidalDesktopPlayKind.Track
            ? TidalDesktopPlaybackPlan.PlayTrackExpression(request.TrackTitle ?? string.Empty)
            : TidalDesktopPlaybackPlan.PlayAllExpression();

        var answer = await session.EvaluateAsync(expression, token).ConfigureAwait(false);

        if (!TidalDesktopPlaybackPlan.IsSuccess(answer, request.DisplayName, out var problem))
        {
            _log?.Invoke($"TIDAL desktop: nie odtworzono \"{request.DisplayName}\", odpowiedź = {answer ?? "brak"}");
            return Failure(problem ?? $"Nie udało się odtworzyć {request.DisplayName} w oryginalnym TIDALu");
        }

        _log?.Invoke($"TIDAL desktop: przekazano \"{request.DisplayName}\" ({request.PageUri}, {request.Kind})");
        return new TidalDesktopPlayResult
        {
            Success = true,
            WasAlreadyCurrent = TidalDesktopPlaybackPlan.IsCurrentItemSuccess(answer),
            Message = request.Kind == TidalDesktopPlayKind.Track
                ? $"{request.DisplayName}: odtwarzanie w oryginalnym TIDALu"
                : $"{request.DisplayName}: odtwarzanie całości w oryginalnym TIDALu"
        };
    }

    /// <summary>
    /// Czeka, az strona wystawi wiersze utworow. Zwraca false, gdy w wyznaczonym
    /// czasie lista sie nie pojawila.
    /// </summary>
    private static async Task<bool> WaitForTrackListAsync(CdpSession session, CancellationToken token)
    {
        var deadline = DateTime.UtcNow + PageLoadWait;
        while (DateTime.UtcNow < deadline)
        {
            token.ThrowIfCancellationRequested();
            var count = await session.EvaluateAsync(
                TidalDesktopPlaybackPlan.TrackRowCountExpression(), token).ConfigureAwait(false);
            if (int.TryParse(count, out var rows) && rows > 0) return true;
            await Task.Delay(500, token).ConfigureAwait(false);
        }
        return false;
    }

    private static TidalDesktopPlayResult Failure(string message) => new()
    {
        Success = false,
        Message = message
    };

    private async Task<bool> IsControlPortOpenAsync(CancellationToken token)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            var body = await http.GetStringAsync($"http://127.0.0.1:{_port}/json/version", token).ConfigureAwait(false);
            return body.Contains("Browser", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private async Task<Uri?> FindPageSocketAsync(CancellationToken token)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var body = await http.GetStringAsync($"http://127.0.0.1:{_port}/json/list", token).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(body);
            foreach (var entry in doc.RootElement.EnumerateArray())
            {
                if (!entry.TryGetProperty("type", out var type)) continue;
                if (!string.Equals(type.GetString(), "page", StringComparison.Ordinal)) continue;
                if (!entry.TryGetProperty("webSocketDebuggerUrl", out var url)) continue;
                var text = url.GetString();
                if (!string.IsNullOrWhiteSpace(text)) return new Uri(text);
            }
        }
        catch (Exception ex)
        {
            _log?.Invoke($"TIDAL desktop: nie odczytano listy okien: {ex.Message}");
        }
        return null;
    }

    private static bool IsTidalRunning()
    {
        try
        {
            return Process.GetProcessesByName(ProcessName).Length > 0;
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> RestartWithControlPortAsync(CancellationToken token)
    {
        try
        {
            foreach (var process in Process.GetProcessesByName(ProcessName))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(8000);
                }
                catch (Exception ex)
                {
                    _log?.Invoke($"TIDAL desktop: nie zamknięto procesu: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            _log?.Invoke($"TIDAL desktop: błąd zamykania: {ex.Message}");
        }

        await Task.Delay(TimeSpan.FromSeconds(3), token).ConfigureAwait(false);
        return await StartWithControlPortAsync(token).ConfigureAwait(false);
    }

    private async Task<bool> StartWithControlPortAsync(CancellationToken token)
    {
        var exe = FindExecutable();
        if (exe is null)
        {
            _log?.Invoke("TIDAL desktop: nie znaleziono pliku wykonywalnego");
            return false;
        }

        try
        {
            var info = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = TidalDesktopPlaybackPlan.RemoteDebuggingSwitch + _port.ToString(System.Globalization.CultureInfo.InvariantCulture),
                UseShellExecute = true
            };
            Process.Start(info);
        }
        catch (Exception ex)
        {
            _log?.Invoke($"TIDAL desktop: nie uruchomiono programu: {ex.Message}");
            return false;
        }

        var deadline = DateTime.UtcNow + StartupTimeout;
        while (DateTime.UtcNow < deadline)
        {
            token.ThrowIfCancellationRequested();
            if (await IsControlPortOpenAsync(token).ConfigureAwait(false)) return true;
            await Task.Delay(TimeSpan.FromSeconds(2), token).ConfigureAwait(false);
        }

        _log?.Invoke("TIDAL desktop: port sterowania nie odpowiedział w wyznaczonym czasie");
        return false;
    }

    /// <summary>
    /// Szuka TIDALa w miejscu, w ktore instaluje go Squirrel. Numer wersji jest
    /// w nazwie katalogu, wiec bierzemy najnowszy - inaczej po kazdej
    /// aktualizacji TIDALa nasza sciezka przestawalaby istniec.
    /// </summary>
    public static string? FindExecutable()
    {
        try
        {
            var root = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TIDAL");
            if (!Directory.Exists(root)) return null;

            var candidates = Directory.GetDirectories(root, "app-*")
                .Select(dir => Path.Combine(dir, "TIDAL.exe"))
                .Where(File.Exists)
                .OrderByDescending(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (candidates.Count > 0) return candidates[0];

            var direct = Path.Combine(root, "TIDAL.exe");
            return File.Exists(direct) ? direct : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Najciensza mozliwa obsluga protokolu debugowania: numerowanie polecen,
    /// czekanie na odpowiedz o tym numerze i trzy potrzebne nam czynnosci.
    /// </summary>
    private sealed class CdpSession
    {
        private readonly ClientWebSocket _socket;
        private int _id;

        public CdpSession(ClientWebSocket socket) => _socket = socket;

        public async Task<JsonDocument?> SendAsync(string method, object? parameters, CancellationToken token)
        {
            var id = ++_id;
            var payload = parameters is null
                ? JsonSerializer.Serialize(new { id, method })
                : JsonSerializer.Serialize(new { id, method, @params = parameters });

            var bytes = Encoding.UTF8.GetBytes(payload);
            await _socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, token).ConfigureAwait(false);

            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
            while (DateTime.UtcNow < deadline)
            {
                var text = await ReceiveAsync(token).ConfigureAwait(false);
                if (text is null) return null;
                JsonDocument document;
                try
                {
                    document = JsonDocument.Parse(text);
                }
                catch (JsonException)
                {
                    continue;
                }

                if (document.RootElement.TryGetProperty("id", out var idElement)
                    && idElement.TryGetInt32(out var received)
                    && received == id)
                {
                    return document;
                }

                document.Dispose();
            }

            return null;
        }

        public async Task<string?> EvaluateAsync(string expression, CancellationToken token)
        {
            using var response = await SendAsync("Runtime.evaluate", new
            {
                expression,
                returnByValue = true,
                awaitPromise = true
            }, token).ConfigureAwait(false);

            if (response is null) return null;
            if (!response.RootElement.TryGetProperty("result", out var outer)) return null;
            if (!outer.TryGetProperty("result", out var inner)) return null;
            return inner.TryGetProperty("value", out var value) ? value.ToString() : null;
        }

        private async Task<string?> ReceiveAsync(CancellationToken token)
        {
            var buffer = new byte[262144];
            var builder = new StringBuilder();
            WebSocketReceiveResult result;
            do
            {
                result = await _socket.ReceiveAsync(new ArraySegment<byte>(buffer), token).ConfigureAwait(false);
                if (result.MessageType == WebSocketMessageType.Close) return null;
                builder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
            }
            while (!result.EndOfMessage);

            return builder.ToString();
        }
    }
}
