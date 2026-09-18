using System.Net;
using System.Net.Http;
using System.Text;
using AccessibleMediaController.Core.Spotify;
using AccessibleMediaController.Windows.Services;

/// <summary>
/// Logowanie NATYWNEJ sesji Spotify (Librespot) - testy bez sieci i bez konta.
///
/// Zadne zapytanie nie wychodzi z komputera: HttpMessageHandler jest atrapa,
/// a tokeny w odpowiedziach sa jawnie FAKE. Klucz Menedzera poswiadczen jest
/// LOSOWY i tymczasowy, kasowany w finally - produkcyjne klucze
/// "AccessibleMediaController/SpotifyLibrespot" i ".../Spotify" nie sa nigdy
/// dotykane.
/// </summary>
internal static class SpotifyLibrespotAuthenticationTests
{
    internal static void Run()
    {
        // Peln zestaw WPF moze pozostawic DispatcherSynchronizationContext na
        // watku STA; async w nim zakleszcza test. Dlatego wlasny watek puli.
        Task.Run(async () =>
        {
            await VerifyStoredTokenIsReusedWithoutNetwork();
            await VerifyExpiredTokenIsRefreshedAndPersisted();
            await VerifyMissingPairingIsHonestRefusal();
            await VerifyRevokedRefreshAsksForNewPairing();
            await VerifyDeviceFlowPairingWritesNativeKeyOnly();
            await VerifyPollingRespectsIntervalSlowDownAndRefusals();
            await VerifyDisconnectDuringRefreshDoesNotResurrectLogin();
            await VerifyDisposeDuringRefreshDoesNotMaskRealFailure();
            await VerifyPairingDoesNotWriteAfterDisconnectOrCancellation();
            await VerifyServerErrorTextNeverReachesMessage();
            VerifyVerificationUriValidation();
            VerifySecretsNeverAppearInText();
        }).GetAwaiter().GetResult();

        VerifyIsolatedWindowsCredentialRoundtrip();
        Console.WriteLine("OK: oddzielne logowanie natywnej sesji Spotify — odczyt, odswiezenie, device flow i klucz Windows");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    // ---------- atrapy ----------

    private sealed class FakeStore : ISpotifyLibrespotCredentialStore
    {
        private SpotifyTokenSet? _tokens;
        internal int Writes { get; private set; }
        internal int Deletes { get; private set; }
        internal SpotifyTokenSet? Current => _tokens;

        internal FakeStore(SpotifyTokenSet? initial = null) => _tokens = initial;

        public bool TryRead(out SpotifyTokenSet? tokens)
        {
            tokens = _tokens;
            return tokens is not null;
        }

        public void Write(SpotifyTokenSet tokens)
        {
            Writes++;
            _tokens = tokens;
        }

        public void Delete()
        {
            Deletes++;
            _tokens = null;
        }
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Queue<(HttpStatusCode Status, string Body)> _responses = new();
        internal List<(string Url, string Body)> Requests { get; } = new();

        internal FakeHandler Enqueue(HttpStatusCode status, string body)
        {
            _responses.Enqueue((status, body));
            return this;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add((request.RequestUri!.ToString(), body));
            if (_responses.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Test nie przewidzial kolejnego zapytania HTTP: {request.RequestUri}");
            }
            var (status, responseBody) = _responses.Dequeue();
            return new HttpResponseMessage(status)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            };
        }
    }

    /// <summary>
    /// Atrapa z bariera: wstrzymuje zapytanie HTTP, zeby test mogl w TRAKCIE
    /// odpowiedzi serwera wykonac Disconnect, Dispose albo anulowanie. Token
    /// anulowania jest celowo ignorowany — sprawdzamy zachowanie uslugi PO
    /// przeczytaniu odpowiedzi, a nie przerwanie samego zapytania.
    /// </summary>
    private sealed class BarrierHandler : HttpMessageHandler
    {
        private readonly TaskCompletionSource _entered =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _release =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly HttpStatusCode _status;
        private readonly string _body;

        internal BarrierHandler(HttpStatusCode status, string body)
        {
            _status = status;
            _body = body;
        }

        internal Task Entered => _entered.Task;

        internal void Release() => _release.TrySetResult();

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            _entered.TrySetResult();
            await _release.Task.ConfigureAwait(false);
            return new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json")
            };
        }
    }

    private static SpotifyLibrespotAuthenticationService BarrierService(
        BarrierHandler handler,
        ISpotifyLibrespotCredentialStore store,
        DateTimeOffset now,
        bool ownsHttpClient = true)
        => new(
            new HttpClient(handler),
            store,
            ownsHttpClient: ownsHttpClient,
            clientId: null,
            now: () => now,
            delay: (_, _) => Task.CompletedTask);

    private static async Task WithinWatchdog(Task task, string message)
    {
        var finished = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(10))).ConfigureAwait(false);
        Assert(ReferenceEquals(finished, task), message);
        await task.ConfigureAwait(false);
    }

    private static SpotifyLibrespotAuthenticationService Service(
        FakeHandler handler,
        ISpotifyLibrespotCredentialStore store,
        DateTimeOffset now,
        List<TimeSpan>? waits = null)
        => new(
            new HttpClient(handler),
            store,
            ownsHttpClient: true,
            clientId: null,
            now: () => now,
            delay: (wait, _) =>
            {
                waits?.Add(wait);
                return Task.CompletedTask;
            });

    // ---------- odczyt i odswiezenie ----------

    private static async Task VerifyStoredTokenIsReusedWithoutNetwork()
    {
        var now = new DateTimeOffset(2026, 9, 18, 12, 0, 0, TimeSpan.Zero);
        var store = new FakeStore(new SpotifyTokenSet(
            "FAKE-access-waznY",
            "FAKE-refresh",
            now.AddHours(1),
            "streaming",
            SpotifyLibrespotAuthenticationService.DefaultClientId));
        var handler = new FakeHandler();
        using var service = Service(handler, store, now);

        Assert(service.HasStoredLogin, "Zapisane logowanie natywnej sesji nie zostalo rozpoznane.");
        var token = await service.GetAccessTokenAsync(CancellationToken.None);
        Assert(token == "FAKE-access-waznY", "Wazny token natywnej sesji nie zostal uzyty.");
        Assert(handler.Requests.Count == 0, "Wazny token niepotrzebnie wywolal zapytanie do Spotify.");
        Assert(store.Writes == 0, "Wazny token niepotrzebnie nadpisal Menedzer poswiadczen.");
    }

    private static async Task VerifyExpiredTokenIsRefreshedAndPersisted()
    {
        var now = new DateTimeOffset(2026, 9, 18, 12, 0, 0, TimeSpan.Zero);
        var store = new FakeStore(new SpotifyTokenSet(
            "FAKE-access-stary",
            "FAKE-refresh",
            // Mniej niz margines 5 minut: token trzeba odswiezyc ZANIM wygasnie,
            // inaczej host Librespot dostanie token umierajacy w trakcie logowania.
            now.AddMinutes(2),
            "streaming",
            SpotifyLibrespotAuthenticationService.DefaultClientId));
        var handler = new FakeHandler().Enqueue(
            HttpStatusCode.OK,
            """{"access_token":"FAKE-access-nowy","expires_in":3600,"scope":"streaming"}""");
        using var service = Service(handler, store, now);

        var token = await service.GetAccessTokenAsync(CancellationToken.None);
        Assert(token == "FAKE-access-nowy", "Token blisko wygasniecia nie zostal odswiezony.");
        Assert(store.Writes == 1, "Odswiezony token nie zostal zapisany dokladnie raz.");
        Assert(store.Current!.RefreshToken == "FAKE-refresh",
            "Brak nowego refresh_token skasowal dotychczasowe logowanie natywnej sesji.");
        Assert(store.Current!.ExpiresAtUtc > now.AddMinutes(50),
            "Zapisany token nie ma nowego czasu waznosci.");

        var (url, body) = handler.Requests.Single();
        Assert(url == SpotifyLibrespotAuthenticationService.TokenEndpoint,
            $"Odswiezenie poszlo na zly adres: {url}.");
        Assert(body.Contains("grant_type=refresh_token", StringComparison.Ordinal)
               && body.Contains("refresh_token=FAKE-refresh", StringComparison.Ordinal)
               && body.Contains("client_id=" + SpotifyLibrespotAuthenticationService.DefaultClientId, StringComparison.Ordinal),
            "Zapytanie odswiezenia nie ma wymaganych pol standardu OAuth.");
    }

    private static async Task VerifyMissingPairingIsHonestRefusal()
    {
        var now = DateTimeOffset.UtcNow;
        var store = new FakeStore();
        var handler = new FakeHandler();
        using var service = Service(handler, store, now);

        Assert(!service.HasStoredLogin, "Brak parowania zostal zglaszony jako zalogowane konto.");
        LibrespotHostException? failure = null;
        try { await service.GetAccessTokenAsync(CancellationToken.None); }
        catch (LibrespotHostException exception) { failure = exception; }

        Assert(failure is not null,
            "Brak sparowanej natywnej sesji nie zglosil bledu — grozi cichym zapasowym logowaniem tokenem Web API.");
        Assert(failure!.Code == SpotifyLibrespotAuthenticationService.NativePairingRequiredCode,
            $"Brak parowania ma zly kod bledu: {failure.Code}.");
        Assert(!string.IsNullOrWhiteSpace(failure.Message) && failure.Message.Contains("sparowana", StringComparison.OrdinalIgnoreCase),
            "Komunikat braku parowania nie jest czytelny dla czlowieka.");
        Assert(handler.Requests.Count == 0, "Brak parowania poszedl mimo to do sieci Spotify.");
    }

    private static async Task VerifyRevokedRefreshAsksForNewPairing()
    {
        var now = new DateTimeOffset(2026, 9, 18, 12, 0, 0, TimeSpan.Zero);
        var store = new FakeStore(new SpotifyTokenSet(
            "FAKE-access-stary", "FAKE-refresh-uniewazniony", now.AddMinutes(1), "streaming",
            SpotifyLibrespotAuthenticationService.DefaultClientId));
        var handler = new FakeHandler().Enqueue(
            HttpStatusCode.BadRequest,
            """{"error":"invalid_grant","error_description":"Refresh token revoked"}""");
        using var service = Service(handler, store, now);

        LibrespotHostException? failure = null;
        try { await service.GetAccessTokenAsync(CancellationToken.None); }
        catch (LibrespotHostException exception) { failure = exception; }

        Assert(failure?.Code == SpotifyLibrespotAuthenticationService.NativePairingRequiredCode,
            "Uniewaznione logowanie natywnej sesji nie prosi o ponowne sparowanie.");
    }

    // ---------- device flow ----------

    private static async Task VerifyDeviceFlowPairingWritesNativeKeyOnly()
    {
        var now = new DateTimeOffset(2026, 9, 18, 12, 0, 0, TimeSpan.Zero);
        var store = new FakeStore();
        var waits = new List<TimeSpan>();
        var handler = new FakeHandler()
            .Enqueue(HttpStatusCode.OK, """
                {"device_code":"FAKE-device-code-SEKRET","user_code":"ABCD-1234",
                 "verification_uri":"https://www.spotify.com/pair",
                 "verification_uri_complete":"https://www.spotify.com/pair?code=ABCD-1234",
                 "expires_in":600,"interval":5}
                """)
            .Enqueue(HttpStatusCode.OK, """
                {"access_token":"FAKE-access-po-parowaniu","refresh_token":"FAKE-refresh-nowy",
                 "expires_in":3600,"scope":"streaming"}
                """);
        using var service = Service(handler, store, now, waits);

        var request = await service.RequestPairingAsync(CancellationToken.None);
        Assert(request.UserCode == "ABCD-1234", "Kod do przepisania nie zostal odczytany.");
        Assert(request.VerificationUri.ToString() == "https://www.spotify.com/pair?code=ABCD-1234",
            "UI nie dostal pelnego adresu z juz wpisanym kodem.");
        Assert(request.ExpiresAtUtc == now.AddMinutes(10), "Czas waznosci parowania jest bledny.");

        var (authorizeUrl, authorizeBody) = handler.Requests[0];
        Assert(authorizeUrl == SpotifyLibrespotAuthenticationService.DeviceAuthorizeEndpoint,
            $"Parowanie poszlo na zly adres: {authorizeUrl}.");
        Assert(authorizeBody.Contains("client_id=" + SpotifyLibrespotAuthenticationService.DefaultClientId, StringComparison.Ordinal)
               && authorizeBody.Contains("scope=streaming", StringComparison.Ordinal),
            "Zapytanie parowania nie uzywa ClientID klienta desktop ani zakresu streaming.");

        await service.CompletePairingAsync(request, CancellationToken.None);
        Assert(store.Writes == 1 && store.Current is not null,
            "Zatwierdzone parowanie nie zapisalo logowania natywnej sesji.");
        Assert(store.Current!.AccessToken == "FAKE-access-po-parowaniu"
               && store.Current.RefreshToken == "FAKE-refresh-nowy"
               && store.Current.ClientId == SpotifyLibrespotAuthenticationService.DefaultClientId,
            "Zapisane logowanie natywnej sesji nie zawiera danych z device flow.");

        var (tokenUrl, tokenBody) = handler.Requests[1];
        Assert(tokenUrl == SpotifyLibrespotAuthenticationService.TokenEndpoint,
            $"Odpytywanie o token poszlo na zly adres: {tokenUrl}.");
        Assert(tokenBody.Contains("grant_type=urn%3Aietf%3Aparams%3Aoauth%3Agrant-type%3Adevice_code", StringComparison.Ordinal)
               && tokenBody.Contains("device_code=FAKE-device-code-SEKRET", StringComparison.Ordinal),
            "Odpytywanie nie uzywa standardowego grantu device_code.");
        Assert(waits.All(wait => wait >= SpotifyLibrespotAuthenticationService.MinimumPollInterval),
            "Odpytywanie nie szanuje minimalnego odstepu podanego przez serwer.");

        service.Disconnect();
        Assert(store.Deletes == 1 && store.Current is null,
            "Odlaczenie nie usunelo logowania natywnej sesji.");
    }

    private static async Task VerifyPollingRespectsIntervalSlowDownAndRefusals()
    {
        var now = new DateTimeOffset(2026, 9, 18, 12, 0, 0, TimeSpan.Zero);
        var waits = new List<TimeSpan>();
        var store = new FakeStore();
        var handler = new FakeHandler()
            .Enqueue(HttpStatusCode.BadRequest, """{"error":"authorization_pending"}""")
            .Enqueue(HttpStatusCode.BadRequest, """{"error":"slow_down"}""")
            .Enqueue(HttpStatusCode.OK, """
                {"access_token":"FAKE-a","refresh_token":"FAKE-r","expires_in":3600,"scope":"streaming"}
                """);
        using var service = Service(handler, store, now, waits);
        var request = Pairing(now, TimeSpan.FromSeconds(5));

        await service.CompletePairingAsync(request, CancellationToken.None);
        Assert(waits.Count == 3, $"Liczba odstepow odpytywania jest bledna: {waits.Count}.");
        Assert(waits[2] > waits[1],
            "Serwerowe slow_down nie wydluzylo odstepu — grozi zbanowaniem logowania.");

        await VerifyRefusal("access_denied", SpotifyLibrespotAuthenticationService.PairingDeniedCode, now);
        await VerifyRefusal("expired_token", SpotifyLibrespotAuthenticationService.PairingExpiredCode, now);
        await VerifyRefusal("nieznany_blad", SpotifyLibrespotAuthenticationService.PairingFailedCode, now);

        // Wygasnieciem rzadzi rowniez zegar: przeterminowane zadanie nie moze
        // pytac serwera w nieskonczonosc.
        var expiredHandler = new FakeHandler();
        using var expiredService = Service(expiredHandler, new FakeStore(), now);
        LibrespotHostException? expired = null;
        try
        {
            await expiredService.CompletePairingAsync(Pairing(now.AddMinutes(-20), TimeSpan.FromSeconds(5)), CancellationToken.None);
        }
        catch (LibrespotHostException exception) { expired = exception; }
        Assert(expired?.Code == SpotifyLibrespotAuthenticationService.PairingExpiredCode,
            "Przeterminowane parowanie nie zostalo zatrzymane przed zapytaniem.");
        Assert(expiredHandler.Requests.Count == 0, "Przeterminowane parowanie mimo to pytalo serwer.");

        // Anulowanie w oknie UI musi natychmiast przerwac odpytywanie.
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var cancelHandler = new FakeHandler();
        using var cancelService = Service(cancelHandler, new FakeStore(), now);
        var wasCancelled = false;
        try { await cancelService.CompletePairingAsync(Pairing(now, TimeSpan.FromSeconds(5)), cancelled.Token); }
        catch (OperationCanceledException) { wasCancelled = true; }
        Assert(wasCancelled, "Anulowanie logowania natywnej sesji nie przerwalo odpytywania.");
        Assert(cancelHandler.Requests.Count == 0, "Anulowane logowanie mimo to pytalo serwer.");
    }

    // ---------- wyscigi: Disconnect, Dispose, anulowanie ----------

    /// <summary>
    /// WYSCIG 1: Disconnect w trakcie oczekiwania na odswiezenie. Odpowiedz
    /// serwera przychodzi PO odlaczeniu — spozniony zapis nie moze wskrzesic
    /// skasowanego logowania natywnej sesji.
    /// </summary>
    private static async Task VerifyDisconnectDuringRefreshDoesNotResurrectLogin()
    {
        var now = new DateTimeOffset(2026, 9, 18, 12, 0, 0, TimeSpan.Zero);
        var store = new FakeStore(new SpotifyTokenSet(
            "FAKE-access-stary", "FAKE-refresh", now.AddMinutes(2), "streaming",
            SpotifyLibrespotAuthenticationService.DefaultClientId));
        var handler = new BarrierHandler(
            HttpStatusCode.OK,
            """{"access_token":"FAKE-access-spozniony","expires_in":3600,"scope":"streaming"}""");
        using var service = BarrierService(handler, store, now);

        var refresh = service.GetAccessTokenAsync(CancellationToken.None);
        await handler.Entered.ConfigureAwait(false);

        // Disconnect NIE MOZE czekac na zapytanie HTTP: w UI wisialoby okno.
        var disconnect = Task.Run(() => service.Disconnect());
        await WithinWatchdog(disconnect,
            "Disconnect czekal na zapytanie HTTP — okno konta natywnej sesji zawieszaloby sie na czas odswiezania.");
        Assert(store.Current is null && store.Deletes == 1,
            "Disconnect nie usunal logowania natywnej sesji z Menedzera poswiadczen.");

        handler.Release();
        LibrespotHostException? failure = null;
        try { await refresh.ConfigureAwait(false); }
        catch (LibrespotHostException exception) { failure = exception; }

        Assert(store.Current is null,
            "Spozniony zapis odswiezenia wskrzesil skasowane logowanie natywnej sesji po Disconnect.");
        Assert(store.Writes == 0,
            "Odswiezenie zapisalo token po Disconnect — Menedzer poswiadczen dostal uniewazniona sesje.");
        Assert(failure is not null,
            "Odswiezenie przerwane przez Disconnect zwrocilo token jak gdyby konto bylo nadal sparowane.");
        Assert(failure!.Code == SpotifyLibrespotAuthenticationService.NativePairingRequiredCode,
            $"Odswiezenie przerwane przez Disconnect ma zly kod bledu: {failure.Code}.");
    }

    /// <summary>
    /// Dispose w trakcie odswiezania konczy sie grzecznie i NIE zaslania
    /// wlasciwego bledu przypadkowym ObjectDisposedException z Release.
    /// </summary>
    private static async Task VerifyDisposeDuringRefreshDoesNotMaskRealFailure()
    {
        var now = new DateTimeOffset(2026, 9, 18, 12, 0, 0, TimeSpan.Zero);
        var store = new FakeStore(new SpotifyTokenSet(
            "FAKE-access-stary", "FAKE-refresh", now.AddMinutes(2), "streaming",
            SpotifyLibrespotAuthenticationService.DefaultClientId));
        var handler = new BarrierHandler(
            HttpStatusCode.OK,
            """{"access_token":"FAKE-access-po-dispose","expires_in":3600,"scope":"streaming"}""");
        var service = BarrierService(handler, store, now, ownsHttpClient: false);

        var refresh = service.GetAccessTokenAsync(CancellationToken.None);
        await handler.Entered.ConfigureAwait(false);

        var dispose = Task.Run(() => service.Dispose());
        await WithinWatchdog(dispose, "Dispose zawisl na trwajacym odswiezaniu natywnej sesji.");

        handler.Release();
        Exception? failure = null;
        try { await refresh.ConfigureAwait(false); }
        catch (Exception exception) { failure = exception; }

        Assert(failure is null or LibrespotHostException or OperationCanceledException,
            $"Dispose w trakcie odswiezania zaslonil wynik wyjatkiem {failure?.GetType().Name}: {failure?.Message}.");
        Assert(store.Writes == 0,
            "Odswiezenie zapisalo token po Dispose uslugi.");
    }

    /// <summary>
    /// WYSCIG 2: Disconnect albo anulowanie okna UI w fazie miedzy udana
    /// odpowiedzia serwera i zapisem. Sukces parowania NIE MOZE wtedy zapisac
    /// logowania, ktore uzytkownik wlasnie odlaczyl albo porzucil.
    /// </summary>
    private static async Task VerifyPairingDoesNotWriteAfterDisconnectOrCancellation()
    {
        var now = new DateTimeOffset(2026, 9, 18, 12, 0, 0, TimeSpan.Zero);
        const string success =
            """{"access_token":"FAKE-a-po-zamknieciu","refresh_token":"FAKE-r-po-zamknieciu","expires_in":3600,"scope":"streaming"}""";

        // (a) anulowanie okna UI po odpowiedzi serwera, przed zapisem.
        var cancelStore = new FakeStore();
        var cancelHandler = new BarrierHandler(HttpStatusCode.OK, success);
        using var cancelService = BarrierService(cancelHandler, cancelStore, now);
        using var cts = new CancellationTokenSource();
        var pairing = cancelService.CompletePairingAsync(Pairing(now, TimeSpan.FromSeconds(5)), cts.Token);
        await cancelHandler.Entered.ConfigureAwait(false);
        cts.Cancel();
        cancelHandler.Release();

        var wasCancelled = false;
        try { await pairing.ConfigureAwait(false); }
        catch (OperationCanceledException) { wasCancelled = true; }
        Assert(wasCancelled,
            "Zamkniecie okna parowania po odpowiedzi serwera nie zglosilo anulowania.");
        Assert(cancelStore.Writes == 0 && cancelStore.Current is null,
            "Parowanie zapisalo logowanie natywnej sesji po anulowaniu okna UI.");

        // (b) Disconnect w tej samej fazie: zapis nie moze wskrzesic sesji.
        var disconnectStore = new FakeStore();
        var disconnectHandler = new BarrierHandler(HttpStatusCode.OK, success);
        using var disconnectService = BarrierService(disconnectHandler, disconnectStore, now);
        var second = disconnectService.CompletePairingAsync(Pairing(now, TimeSpan.FromSeconds(5)), CancellationToken.None);
        await disconnectHandler.Entered.ConfigureAwait(false);
        var disconnect = Task.Run(() => disconnectService.Disconnect());
        await WithinWatchdog(disconnect, "Disconnect czekal na trwajace parowanie — UI zawieszaloby sie.");
        disconnectHandler.Release();

        LibrespotHostException? failure = null;
        try { await second.ConfigureAwait(false); }
        catch (LibrespotHostException exception) { failure = exception; }
        Assert(disconnectStore.Writes == 0 && disconnectStore.Current is null,
            "Parowanie zapisalo logowanie natywnej sesji po Disconnect — odlaczone konto wrocilo.");
        Assert(failure is not null,
            "Parowanie przerwane przez Disconnect udalo, ze sie powiodlo.");
        // Petla odpytywania nie moze sie zapetlic po przerwaniu.
        Assert(failure!.Code is SpotifyLibrespotAuthenticationService.PairingFailedCode
               or SpotifyLibrespotAuthenticationService.NativePairingRequiredCode,
            $"Przerwane parowanie ma zly kod bledu: {failure.Code}.");
    }

    /// <summary>
    /// WYSCIG/WYCIEK 3: tresc bledu serwera (moze zawierac sekrety, adresy,
    /// identyfikatory sesji) NIE MOZE wejsc do komunikatu dla czlowieka ani do
    /// dziennika. Rozpoznane kody dostaja stale zdania, nieznane — sam HTTP.
    /// </summary>
    private static async Task VerifyServerErrorTextNeverReachesMessage()
    {
        var now = new DateTimeOffset(2026, 9, 18, 12, 0, 0, TimeSpan.Zero);
        const string secret = "FAKE-SEKRET-Z-SERWERA-c0ffee";
        var handler = new FakeHandler().Enqueue(
            HttpStatusCode.BadRequest,
            $$"""{"error":"{{secret}}","error_description":"{{secret}} opis","state":"{{secret}}"}""");
        var store = new FakeStore();
        using var service = Service(handler, store, now);

        LibrespotHostException? failure = null;
        try { await service.CompletePairingAsync(Pairing(now, TimeSpan.FromSeconds(5)), CancellationToken.None); }
        catch (LibrespotHostException exception) { failure = exception; }

        Assert(failure?.Code == SpotifyLibrespotAuthenticationService.PairingFailedCode,
            $"Nieznany kod bledu serwera ma zly kod odmowy: {failure?.Code ?? "brak wyjatku"}.");
        Assert(!failure!.Message.Contains(secret, StringComparison.OrdinalIgnoreCase),
            "Tresc bledu serwera wyciekla do komunikatu dla czlowieka (a wiec i do dziennika).");
        Assert(!failure.ToString().Contains(secret, StringComparison.OrdinalIgnoreCase),
            "Tresc bledu serwera wyciekla do ToString wyjatku razem z wyjatkiem wewnetrznym.");
        Assert(failure.Message.Contains("400", StringComparison.Ordinal),
            "Komunikat nieznanej odmowy nie podaje kodu HTTP, wiec nie da sie jej zdiagnozowac.");
        Assert(store.Writes == 0, "Nieznana odmowa zapisala logowanie natywnej sesji.");

        // Nieprawidlowy JSON tez nie moze wnosic surowej tresci odpowiedzi.
        var rawHandler = new FakeHandler().Enqueue(HttpStatusCode.BadRequest, secret + " to nie JSON");
        using var rawService = Service(rawHandler, new FakeStore(), now);
        LibrespotHostException? raw = null;
        try { await rawService.CompletePairingAsync(Pairing(now, TimeSpan.FromSeconds(5)), CancellationToken.None); }
        catch (LibrespotHostException exception) { raw = exception; }
        Assert(raw is not null && !raw.ToString().Contains(secret, StringComparison.OrdinalIgnoreCase),
            "Surowa odpowiedz serwera wyciekla do wyjatku przy nieprawidlowym JSON-ie.");
    }

    private static async Task VerifyRefusal(string error, string expectedCode, DateTimeOffset now)
    {
        var handler = new FakeHandler().Enqueue(HttpStatusCode.BadRequest, $"{{\"error\":\"{error}\"}}");
        var store = new FakeStore();
        using var service = Service(handler, store, now);
        LibrespotHostException? failure = null;
        try { await service.CompletePairingAsync(Pairing(now, TimeSpan.FromSeconds(5)), CancellationToken.None); }
        catch (LibrespotHostException exception) { failure = exception; }
        Assert(failure?.Code == expectedCode,
            $"Odmowa {error} ma zly kod bledu: {failure?.Code ?? "brak wyjatku"}.");
        Assert(store.Writes == 0, $"Odmowa {error} zapisala logowanie natywnej sesji.");
    }

    private static SpotifyLibrespotPairingRequest Pairing(DateTimeOffset now, TimeSpan interval)
        => new(
            "FAKE-device-code-SEKRET",
            "ABCD-1234",
            new Uri("https://www.spotify.com/pair?code=ABCD-1234"),
            now.AddMinutes(10),
            interval);

    private static void VerifyVerificationUriValidation()
    {
        Assert(SpotifyLibrespotAuthenticationService
                   .ResolveVerificationUri("https://accounts.spotify.com/pair?code=X", "https://www.spotify.com/pair")
                   .Host == "accounts.spotify.com",
            "Pelny adres z kodem nie ma pierwszenstwa.");
        Assert(SpotifyLibrespotAuthenticationService
                   .ResolveVerificationUri(null, "https://www.spotify.com/pair").Host == "www.spotify.com",
            "Zapasowy adres weryfikacji Spotify nie zostal przyjety.");

        foreach (var hostile in new[]
                 {
                     "http://www.spotify.com/pair",
                     "https://spotify.com.zly.example/pair",
                     "https://evil.example/pair",
                     "nie-adres",
                     // Czesc przed @ to NIE host: przegladarka poszlaby na
                     // spotify.com, ale UserInfo w adresie sluzy podszywaniu
                     // sie i nie ma go w prawdziwych adresach Spotify.
                     "https://user@spotify.com/pair",
                     "https://accounts.spotify.com@evil.example/pair",
                     // Niestandardowy port to nie strona logowania Spotify.
                     "https://accounts.spotify.com:8443/pair"
                 })
        {
            var refused = false;
            try { SpotifyLibrespotAuthenticationService.ResolveVerificationUri(hostile, null); }
            catch (LibrespotHostException) { refused = true; }
            Assert(refused, $"Obcy albo nieszyfrowany adres weryfikacji zostal przyjety: {hostile}.");
        }
    }

    private static void VerifySecretsNeverAppearInText()
    {
        var request = Pairing(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
        Assert(!request.ToString().Contains("FAKE-device-code-SEKRET", StringComparison.Ordinal),
            "Kod urzadzenia wyciekl do reprezentacji tekstowej (a wiec i do dziennika).");
        var serialized = System.Text.Json.JsonSerializer.Serialize(request);
        Assert(!serialized.Contains("FAKE-device-code-SEKRET", StringComparison.Ordinal),
            "Kod urzadzenia wyciekl do JSON-a zadania parowania.");

        var deviceCodeProperty = typeof(SpotifyLibrespotPairingRequest)
            .GetProperty("DeviceCode", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        Assert(deviceCodeProperty is null, "Kod urzadzenia jest publiczna wlasciwoscia zadania parowania.");
    }

    // ---------- klucz Windows, osamotniony i losowy ----------

    private static void VerifyIsolatedWindowsCredentialRoundtrip()
    {
        if (!OperatingSystem.IsWindows())
        {
            Console.WriteLine("POMINIETO: zapis w Menedzerze poswiadczen wymaga Windows");
            return;
        }

        // LOSOWY klucz tymczasowy. Test NIGDY nie czyta i nie kasuje kluczy
        // produkcyjnych natywnej sesji ani Web API.
        var target = $"AccessibleMediaController/TEST-SpotifyLibrespot-{Guid.NewGuid():N}";
        Assert(target != SpotifyLibrespotCredentialStore.NativeTargetName
               && !target.Equals("AccessibleMediaController/Spotify", StringComparison.OrdinalIgnoreCase),
            "Test uzylby produkcyjnego klucza poswiadczen.");
        var store = new SpotifyLibrespotCredentialStore(target);
        try
        {
            Assert(!store.TryRead(out _), "Losowy klucz testowy istnial przed zapisem.");

            var expires = new DateTimeOffset(2026, 9, 18, 12, 34, 56, TimeSpan.Zero);
            store.Write(new SpotifyTokenSet(
                "FAKE-access-roundtrip", "FAKE-refresh-roundtrip", expires, "streaming", "FAKE-client"));
            Assert(store.TryRead(out var read) && read is not null, "Zapisanego klucza testowego nie udalo sie odczytac.");
            Assert(read!.AccessToken == "FAKE-access-roundtrip"
                   && read.RefreshToken == "FAKE-refresh-roundtrip"
                   && read.Scope == "streaming"
                   && read.ClientId == "FAKE-client"
                   && read.ExpiresAtUtc == expires,
                "Odczyt z Menedzera poswiadczen zgubil albo przeklamal pola logowania.");

            store.Delete();
            Assert(!store.TryRead(out _), "Odlaczenie nie usunelo klucza testowego.");
            store.Delete(); // brak wpisu nie jest bledem
        }
        finally
        {
            try { store.Delete(); } catch { /* sprzatanie nie moze zaslonic wyniku testu */ }
        }
        Console.WriteLine("OK: typowany zapis i odczyt logowania natywnej sesji w losowym kluczu Windows");
    }
}
