using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using AccessibleMediaController.Core.Spotify;

namespace AccessibleMediaController.Windows.Services;

/// <summary>
/// Zadanie sparowania natywnej sesji: to, co UI ma pokazac czlowiekowi.
/// KOD URZADZENIA (device_code) jest sekretem rownym tokenowi, wiec nie ma
/// publicznej wlasciwosci, nie trafia do ToString ani do JSON.
/// </summary>
internal sealed class SpotifyLibrespotPairingRequest
{
    internal SpotifyLibrespotPairingRequest(
        string deviceCode,
        string userCode,
        Uri verificationUri,
        DateTimeOffset expiresAtUtc,
        TimeSpan pollInterval)
    {
        DeviceCode = deviceCode;
        UserCode = userCode;
        VerificationUri = verificationUri;
        ExpiresAtUtc = expiresAtUtc;
        PollInterval = pollInterval;
    }

    /// <summary>Kod do przepisania na stronie Spotify. Jawny celowo.</summary>
    public string UserCode { get; }

    /// <summary>
    /// Pelny adres weryfikacji, juz z wpisanym kodem, gdy Spotify go poda.
    /// NIE otwieramy przegladarki sami - adres podaje UI.
    /// </summary>
    public Uri VerificationUri { get; }

    public DateTimeOffset ExpiresAtUtc { get; }

    internal TimeSpan PollInterval { get; }

    /// <summary>Sekret. Tylko usluga logowania.</summary>
    internal string DeviceCode { get; }

    /// <summary>
    /// Zadna reprezentacja tekstowa nie moze wyniesc sekretu do dziennika.
    /// </summary>
    public override string ToString() => "Sparowanie natywnej sesji Spotify";
}

/// <summary>
/// ODDZIELNE, bezpieczne logowanie natywnej sesji Spotify (Librespot).
///
/// ZMIERZONY FAKT (18.09.2026): token wydany dla wlasnego ClientID AMC NIE
/// loguje sie do Librespota - AP inicjalizuje sie, a Login konczy sie
/// INVALID_CREDENTIALS. Dziala wylacznie standardowe OAuth Device
/// Authorization z ClientID klienta desktop, takim jakiego uzywa upstream
/// librespot (SessionConfig). Dlatego ta usluga NIGDY nie siega po token Web
/// API/SDK i NIE MA zadnego cichego zapasowego przejscia na niego: brak
/// sparowania to czytelny wyjatek, nie podmiana zrodla logowania.
/// </summary>
internal sealed class SpotifyLibrespotAuthenticationService : IDisposable
{
    /// <summary>
    /// Publiczny identyfikator klienta desktop, ten sam co w upstream
    /// librespot. Nie jest sekretem (device flow nie uzywa client_secret) i
    /// jest jawny w zrodlach librespota - dlatego stoi tu wprost.
    /// </summary>
    internal const string DefaultClientId = "65b708073fc0480ea92a077233ca87bd";

    internal const string DeviceScope = "streaming";
    internal const string DeviceAuthorizeEndpoint = "https://accounts.spotify.com/oauth2/device/authorize";
    internal const string TokenEndpoint = "https://accounts.spotify.com/api/token";
    internal const string DeviceCodeGrantType = "urn:ietf:params:oauth:grant-type:device_code";

    /// <summary>
    /// Kod odmowy: konto natywne NIE jest sparowane. Warstwa UI ma poprosic o
    /// sparowanie, a nie sciagac token z innego logowania.
    /// </summary>
    internal const string NativePairingRequiredCode = "native_pairing_required";

    internal const string PairingDeniedCode = "native_pairing_denied";
    internal const string PairingExpiredCode = "native_pairing_expired";
    internal const string PairingFailedCode = "native_pairing_failed";

    /// <summary>Token odswiezamy z zapasem, zeby nie wygasl w trakcie logowania hosta.</summary>
    internal static readonly TimeSpan RefreshMargin = TimeSpan.FromMinutes(5);

    /// <summary>Minimum wymuszane przez serwer; nigdy nie pytamy czesciej.</summary>
    internal static readonly TimeSpan MinimumPollInterval = TimeSpan.FromSeconds(5);

    private readonly HttpClient _http;
    private readonly bool _ownsHttp;
    private readonly ISpotifyLibrespotCredentialStore _store;
    private readonly string _clientId;
    private readonly Func<DateTimeOffset> _now;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>
    /// KROTKI lock wylacznie na odczyt/zapis/kasowanie poswiadczen i na numer
    /// generacji. Disconnect i Dispose NIE MOGA czekac na _gate, bo ten jest
    /// trzymany przez caly czas zapytania HTTP - okno konta w UI zawieszaloby
    /// sie na czas odswiezania tokenu. Dlatego stan poswiadczen chroni ten
    /// lock, a nie semafor operacji.
    /// </summary>
    private readonly object _commitLock = new();

    /// <summary>
    /// Numer generacji logowania. Kazde Disconnect (i Dispose) go zwieksza.
    /// Operacja, ktora zaczela sie w starszej generacji, NIE MOZE zapisac
    /// swojego wyniku: spozniona odpowiedz serwera wskrzeszalaby logowanie,
    /// ktore uzytkownik wlasnie odlaczyl.
    /// </summary>
    private int _generation;

    private volatile bool _disposed;

    public SpotifyLibrespotAuthenticationService()
        : this(new HttpClient(), new SpotifyLibrespotCredentialStore(), ownsHttpClient: true)
    {
    }

    /// <summary>
    /// Wstrzykiwany HttpClient i magazyn sluza testom: test NIGDY nie dotyka
    /// ani sieci Spotify, ani produkcyjnego klucza poswiadczen.
    /// </summary>
    internal SpotifyLibrespotAuthenticationService(
        HttpClient httpClient,
        ISpotifyLibrespotCredentialStore store,
        bool ownsHttpClient = false,
        string? clientId = null,
        Func<DateTimeOffset>? now = null,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        _http = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _ownsHttp = ownsHttpClient;
        _clientId = string.IsNullOrWhiteSpace(clientId) ? DefaultClientId : clientId;
        _now = now ?? (() => DateTimeOffset.UtcNow);
        _delay = delay ?? ((wait, token) => Task.Delay(wait, token));
    }

    /// <summary>Czy natywne konto jest sparowane (bez siegania do sieci).</summary>
    public bool HasStoredLogin
    {
        get
        {
            lock (_commitLock) return _store.TryRead(out _);
        }
    }

    /// <summary>
    /// Odczyt poswiadczen razem z numerem generacji, w ktorej ich odczytano.
    /// Wynik operacji wolno zapisac tylko wtedy, gdy generacja sie nie zmienila.
    /// </summary>
    private (SpotifyTokenSet? Tokens, int Generation) ReadWithGeneration()
    {
        lock (_commitLock)
        {
            _store.TryRead(out var tokens);
            return (tokens, _generation);
        }
    }

    /// <summary>
    /// Zapisuje poswiadczenia TYLKO gdy trwa nadal ta sama generacja logowania.
    /// Zwraca potwierdzony odczytem zestaw albo null, gdy w trakcie operacji
    /// wykonano Disconnect/Dispose - wtedy wolno wylacznie odmowic.
    /// </summary>
    private SpotifyTokenSet? TryCommit(SpotifyTokenSet tokens, int generation)
    {
        lock (_commitLock)
        {
            if (_disposed || _generation != generation) return null;
            _store.Write(tokens);

            // Zapis sprawdzamy ODCZYTEM: token zyjacy tylko w pamieci procesu
            // znikalby po restarcie, a uzytkownik uslyszalby, ze jest
            // zalogowany.
            if (!_store.TryRead(out var confirmed) || confirmed is null) return null;
            return confirmed;
        }
    }

    /// <summary>
    /// Zwraca wazny token natywnej sesji. Odczyt z Menedzera poswiadczen,
    /// odswiezenie z zapasem czasu, bezpieczny zapis i ponowny odczyt.
    /// Brak sparowania = czytelny wyjatek. NIGDY token Web API/SDK.
    /// </summary>
    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var (tokens, generation) = ReadWithGeneration();
            if (tokens is null)
            {
                throw new LibrespotHostException(
                    NativePairingRequiredCode,
                    "Natywna sesja Spotify nie jest sparowana. Zaloguj ja osobno w oknie konta natywnej sesji.");
            }

            if (tokens.ExpiresAtUtc - RefreshMargin > _now())
            {
                return tokens.AccessToken;
            }

            var refreshed = await RefreshAsync(tokens, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            var confirmed = TryCommit(refreshed, generation);
            if (confirmed is null)
            {
                // Disconnect/Dispose w trakcie odswiezania: uczciwa odmowa,
                // nigdy wskrzeszenie odlaczonego logowania.
                throw new LibrespotHostException(
                    NativePairingRequiredCode,
                    "Natywna sesja Spotify zostala odlaczona w trakcie odswiezania logowania. Zaloguj ja ponownie.");
            }

            if (!string.Equals(confirmed.AccessToken, refreshed.AccessToken, StringComparison.Ordinal))
            {
                throw new LibrespotHostException(
                    PairingFailedCode,
                    "Odswiezone logowanie natywnej sesji Spotify nie zostalo zapisane w Menedzerze poswiadczen Windows.");
            }

            return confirmed.AccessToken;
        }
        finally
        {
            // Dispose w trakcie operacji zwalnia semafor; Release na juz
            // zwolnionym semaforze nie moze zaslonic wlasciwego bledu.
            try { _gate.Release(); }
            catch (ObjectDisposedException) { }
        }
    }

    /// <summary>
    /// Rozpoczyna standardowe OAuth Device Authorization. Nie otwiera
    /// przegladarki i nie pokazuje zadnego okna - zwraca dane dla osobnego UI.
    /// </summary>
    public async Task<SpotifyLibrespotPairingRequest> RequestPairingAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = _clientId,
            ["scope"] = DeviceScope
        });

        using var response = await _http
            .PostAsync(DeviceAuthorizeEndpoint, content, cancellationToken)
            .ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new LibrespotHostException(
                PairingFailedCode,
                $"Spotify odmowil rozpoczecia parowania natywnej sesji (HTTP {(int)response.StatusCode}).");
        }

        using var document = ParseJson(body);
        var root = document.RootElement;
        var deviceCode = ReadString(root, "device_code");
        var userCode = ReadString(root, "user_code");
        if (string.IsNullOrWhiteSpace(deviceCode) || string.IsNullOrWhiteSpace(userCode))
        {
            throw new LibrespotHostException(
                PairingFailedCode,
                "Odpowiedz Spotify nie zawiera kodu urzadzenia ani kodu do przepisania.");
        }

        var verificationUri = ResolveVerificationUri(
            ReadString(root, "verification_uri_complete"),
            ReadString(root, "verification_uri"));
        var expiresIn = ReadSeconds(root, "expires_in") ?? TimeSpan.FromMinutes(5);
        var interval = ReadSeconds(root, "interval") ?? MinimumPollInterval;
        if (interval < MinimumPollInterval) interval = MinimumPollInterval;

        return new SpotifyLibrespotPairingRequest(
            deviceCode!,
            userCode!,
            verificationUri,
            _now() + expiresIn,
            interval);
    }

    /// <summary>
    /// Czeka na zatwierdzenie w przegladarce i zapisuje logowanie w kluczu
    /// NATYWNYM. Szanuje interval, slow_down, wygasniecie, odmowe i anulowanie.
    /// </summary>
    public async Task CompletePairingAsync(
        SpotifyLibrespotPairingRequest request,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);

        var interval = request.PollInterval < MinimumPollInterval ? MinimumPollInterval : request.PollInterval;
        var generation = ReadWithGeneration().Generation;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_now() >= request.ExpiresAtUtc)
            {
                throw new LibrespotHostException(
                    PairingExpiredCode,
                    "Czas na zatwierdzenie natywnej sesji Spotify minal. Rozpocznij logowanie od nowa.");
            }

            await _delay(interval, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            using var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = _clientId,
                ["grant_type"] = DeviceCodeGrantType,
                ["device_code"] = request.DeviceCode
            });
            using var response = await _http
                .PostAsync(TokenEndpoint, content, cancellationToken)
                .ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                var tokens = ReadTokenSet(body, previousRefreshToken: null);

                // Zamkniecie okna UI albo Disconnect/Dispose W TEJ FAZIE - juz
                // po odpowiedzi serwera, przed zapisem - NIE MOZE zapisac
                // logowania, ktorego uzytkownik wlasnie sie pozbyl.
                cancellationToken.ThrowIfCancellationRequested();

                var confirmed = TryCommit(tokens, generation);
                if (confirmed is null)
                {
                    throw new LibrespotHostException(
                        NativePairingRequiredCode,
                        "Logowanie natywnej sesji Spotify zostalo przerwane przed zapisaniem. Zaloguj ja ponownie.");
                }

                if (!string.Equals(confirmed.RefreshToken, tokens.RefreshToken, StringComparison.Ordinal))
                {
                    throw new LibrespotHostException(
                        PairingFailedCode,
                        "Logowanie natywnej sesji Spotify nie zostalo zapisane w Menedzerze poswiadczen Windows.");
                }
                return;
            }

            // Tresc bledu serwera moze zawierac sekrety, identyfikatory sesji i
            // adresy. Do komunikatu dla czlowieka (a wiec i do dziennika)
            // wchodza WYLACZNIE rozpoznane kody jako stale zdania; nierozpoznany
            // kod nie jest w ogole wplatany w tekst.
            var error = ReadErrorCode(body);
            switch (error)
            {
                case "authorization_pending":
                    continue;
                case "slow_down":
                    interval += TimeSpan.FromSeconds(5);
                    continue;
                case "access_denied":
                    throw new LibrespotHostException(
                        PairingDeniedCode,
                        "Logowanie natywnej sesji Spotify zostalo odrzucone w przegladarce.");
                case "expired_token":
                    throw new LibrespotHostException(
                        PairingExpiredCode,
                        "Kod logowania natywnej sesji Spotify wygasl. Rozpocznij logowanie od nowa.");
                default:
                    throw new LibrespotHostException(
                        PairingFailedCode,
                        $"Spotify odrzucil logowanie natywnej sesji (HTTP {(int)response.StatusCode}).");
            }
        }
    }

    /// <summary>
    /// Odlacza TYLKO natywna sesje. Logowanie Web API/SDK zostaje nietkniete.
    ///
    /// NIE czeka na trwajace zapytanie HTTP (semafor _gate): okno konta w UI
    /// zawieszaloby sie na czas odswiezania. Zwiekszona generacja sprawia, ze
    /// spozniona odpowiedz serwera nie zapisze skasowanego logowania.
    /// </summary>
    public void Disconnect()
    {
        ThrowIfDisposed();
        lock (_commitLock)
        {
            _generation++;
            _store.Delete();
        }
    }

    private async Task<SpotifyTokenSet> RefreshAsync(SpotifyTokenSet tokens, CancellationToken cancellationToken)
    {
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = string.IsNullOrWhiteSpace(tokens.ClientId) ? _clientId : tokens.ClientId,
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = tokens.RefreshToken
        });
        using var response = await _http
            .PostAsync(TokenEndpoint, content, cancellationToken)
            .ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized)
        {
            // Spotify uniewaznil logowanie. Uczciwa odmowa i prosba o nowe
            // parowanie; NIGDY podmiana na token innej sesji.
            throw new LibrespotHostException(
                NativePairingRequiredCode,
                "Logowanie natywnej sesji Spotify straci\u0142o waznosc. Zaloguj ja ponownie.");
        }
        if (!response.IsSuccessStatusCode)
        {
            throw new LibrespotHostException(
                PairingFailedCode,
                $"Nie udalo sie odswiezyc logowania natywnej sesji Spotify (HTTP {(int)response.StatusCode}).");
        }

        return ReadTokenSet(body, tokens.RefreshToken);
    }

    private SpotifyTokenSet ReadTokenSet(string body, string? previousRefreshToken)
    {
        using var document = ParseJson(body);
        var root = document.RootElement;
        var accessToken = ReadString(root, "access_token");
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new LibrespotHostException(
                PairingFailedCode,
                "Odpowiedz Spotify nie zawiera tokenu natywnej sesji.");
        }

        // Spotify nie musi przyslac nowego refresh_token przy odswiezeniu -
        // wtedy zachowujemy dotychczasowy, inaczej skasowalibysmy logowanie.
        var refreshToken = ReadString(root, "refresh_token") ?? previousRefreshToken;
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new LibrespotHostException(
                PairingFailedCode,
                "Odpowiedz Spotify nie zawiera tokenu odnowienia natywnej sesji.");
        }

        var expiresIn = ReadSeconds(root, "expires_in") ?? TimeSpan.FromHours(1);
        var scope = ReadString(root, "scope") ?? DeviceScope;
        return new SpotifyTokenSet(accessToken!, refreshToken!, _now() + expiresIn, scope, _clientId);
    }

    /// <summary>
    /// Adres weryfikacji musi byc pelnym HTTPS w domenie Spotify - inaczej
    /// odeslalibysmy uzytkownika na obca strone z jego kodem. Odrzucamy takze
    /// czesc uzytkownika (`https://accounts.spotify.com@evil.example` prowadzi
    /// na evil.example, a `https://user@spotify.com` sluzy podszywaniu sie) i
    /// niestandardowy port, ktorego prawdziwa strona logowania nie uzywa.
    /// </summary>
    internal static Uri ResolveVerificationUri(string? complete, string? basic)
    {
        foreach (var candidate in new[] { complete, basic })
        {
            if (string.IsNullOrWhiteSpace(candidate)) continue;
            if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri)) continue;
            if (uri.Scheme != Uri.UriSchemeHttps) continue;
            if (!string.IsNullOrEmpty(uri.UserInfo)) continue;
            if (!uri.IsDefaultPort) continue;
            var host = uri.Host;
            if (!host.Equals("spotify.com", StringComparison.OrdinalIgnoreCase)
                && !host.EndsWith(".spotify.com", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            return uri;
        }

        throw new LibrespotHostException(
            PairingFailedCode,
            "Spotify nie podal prawidlowego adresu HTTPS do zatwierdzenia natywnej sesji.");
    }

    private static JsonDocument ParseJson(string body)
    {
        try
        {
            return JsonDocument.Parse(body);
        }
        catch (JsonException exception)
        {
            // Komunikat JsonException zawiera fragment surowej odpowiedzi, wiec
            // wyjatek wewnetrzny NIE MOZE do nas trafic - inaczej ToString
            // wyniosloby tresc serwera (i ewentualne sekrety) do dziennika.
            _ = exception;
            throw new LibrespotHostException(
                PairingFailedCode,
                "Odpowiedz logowania Spotify nie jest prawidlowym JSON-em.");
        }
    }

    internal static string? ReadErrorCode(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            return ReadString(document.RootElement, "error");
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ReadString(JsonElement root, string name)
        => root.ValueKind == JsonValueKind.Object
           && root.TryGetProperty(name, out var value)
           && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static TimeSpan? ReadSeconds(JsonElement root, string name)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty(name, out var value)) return null;
        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetDouble(out var seconds) && seconds > 0
                => TimeSpan.FromSeconds(seconds),
            JsonValueKind.String when double.TryParse(
                value.GetString(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var parsed) && parsed > 0 => TimeSpan.FromSeconds(parsed),
            _ => null
        };
    }

    private void ThrowIfDisposed()
        => ObjectDisposedException.ThrowIf(_disposed, this);

    public void Dispose()
    {
        lock (_commitLock)
        {
            if (_disposed) return;
            _disposed = true;
            // Generacja rosnie takze przy Dispose: operacja w locie nie zapisze
            // juz swojego wyniku do Menedzera poswiadczen.
            _generation++;
        }

        _gate.Dispose();
        if (_ownsHttp) _http.Dispose();
    }
}
