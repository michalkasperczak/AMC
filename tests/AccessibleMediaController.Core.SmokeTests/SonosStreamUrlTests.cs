using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AccessibleMediaController.Core.Sonos;

/// <summary>
/// WLASNE RADIO W SONOSIE - createSession + loadStreamUrl w warstwie Core.
/// Tylko Core: zero GUI, zero presetow, zero prawdziwego konta, zero sieci, zero
/// muzyki i ZERO pobierania adresu strumienia. Dane syntetyczne, atrapa bramki
/// logowania, atrapa magazynu w pamieci i PRAWDZIWY
/// <see cref="SonosControlApiClient"/> na kontrolowanym HttpMessageHandler.
/// Wywolania ida PRZEZ publiczne <see cref="ISonosSessionCreateApi"/> i
/// <see cref="ISonosStreamUrlLoadApi"/>, wiec mierzony jest caly tor
/// koordynator -> klient -> HTTP.
///
/// Zakres jest CELOWO WASKI i nie powiela odebranych suit F1/F3/playlist:
/// macierz zabezpieczen HTTP i OAuth (przekierowania, limit tresci, kodowanie,
/// naglowki, 403/404/429/5xx, odnawianie, kasowanie konta) zostala zmierzona na
/// TYM SAMYM wspolnym transporcie i TYCH SAMYCH torach koordynatora. Tutaj
/// mierzymy to, co dla sesji jest NOWE albo INNE:
///
///   * createSession: wlasciwe cialo (appId + appContext, BEZ accountId i
///     customData) i ODEBRANY sessionId - to pierwsza operacja, ktorej POST
///     parsuje odpowiedz,
///   * HTTP 200 BEZ sessionId oraz z niepoprawnym sessionId nie daje gotowej
///     sesji i nie wypuszcza identyfikatora,
///   * loadStreamUrl: wlasciwy URI /playbackSessions/{id}/..., wlasciwe cialo
///     (streamUrl + jawny playOnCompletion, bez stationMetadata) i DOKLADNIE
///     JEDEN POST,
///   * zle wejscie (adres, sessionId, appId/appContext) daje ZERO POST,
///   * 401 na ZAPISIE nie powoduje ani odnowienia, ani drugiego POST,
///   * zmiana konta podczas wstrzymanej odpowiedzi NIE publikuje sessionId,
///   * typowe anulowanie i eviction NIE tworza sesji automatycznie,
///   * komunikaty i ToString nie ujawniaja sessionId, adresu radia ani appContext.
///
/// Rejestr pelnej tabeli operacji jest sprawdzany tam, gdzie ma sens: dla
/// sessionState (jedyna wartosc z definicji + brak + nieznana) i dla pelnego
/// zestawu statusow w slownikach komunikatow.
/// </summary>
internal static class SonosStreamUrlTests
{
    private const string Key = "00000000-0000-0000-0000-000000000042";
    private const string Group = "RINCON_0001:1";
    private const string SessionId = "SYNTHETIC-SESSION-42";
    private const string AppId = "pl.synthetic.amc.test";
    private const string AppContext = "SYNTHETIC-CONTEXT-7";
    private const string StreamUrl = "https://stream.invalid/radio.mp3";
    private const string ItemId = "SYNTHETIC-ITEM-9";
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(5);

    public static void Run()
    {
        var tests = new List<(string Name, Action Test)>
        {
            ("zadanie sesji: tabela limitow appId/appContext i sumy UTF-8", SessionRequestEnforcesContract),
            ("createSession: jeden POST, wlasciwe cialo, odebrany sessionId", CreateSendsBodyAndReadsSessionId),
            ("createSession: 200 bez sessionId to NIE gotowa sesja", AcceptedWithoutSessionIdIsNotReady),
            ("createSession: niepoprawna odpowiedz nie daje sesji", InvalidResponseGivesNoSession),
            ("createSession: tabela sessionState (brak, znana, nieznana)", SessionStateTableIsParsed),
            ("loadStreamUrl: wlasciwy URI, wlasciwe cialo, jeden POST", LoadSendsExactlyOnePost),
            ("zle wejscie: zero POST (adres, sesja, tozsamosc aplikacji)", InvalidInputNeverPosts),
            ("401 na zapisie: zero odnowien, zero powtorzen, zero kasowania", UnauthorizedNeverRetriesWrite),
            ("zmiana konta w trakcie: sessionId NIE jest publikowany", AccountChangeNeverPublishesSessionId),
            ("anulowanie i eviction: zadna sesja nie powstaje sama", CancelAndEvictionNeverRecreateSession),
            ("koordynator: cienkie podlaczenie dwoch jawnych metod", CoordinatorExposesBothWrites),
            ("komunikaty i ToString bez sessionId, adresu i kontekstu", MessagesHideSecrets)
        };

        var failures = 0;
        foreach (var (name, test) in tests)
        {
            try { test(); Console.WriteLine("[OK] " + name); }
            catch (Exception ex) { failures++; Console.WriteLine("[FAIL] " + name + " (" + ex.GetType().Name + ": " + ex.Message + ")"); }
        }

        Console.WriteLine($"Własne radio Sonos w Core: {tests.Count - failures}/{tests.Count}");
        if (failures != 0)
        {
            throw new InvalidOperationException("Własne radio Sonos w Core: " + failures + " nieudanych testow.");
        }
    }

    // ================= kontrakt zadania sesji =================

    private static void SessionRequestEnforcesContract()
    {
        Check(SonosSessionRequest.TryCreate(AppId, AppContext, out var ok) && ok is not null, "poprawne zadanie");
        Check(ok!.AppId == AppId && ok.AppContext == AppContext, "wartosci literalne");

        // Oba pola WYMAGANE.
        Check(!SonosSessionRequest.TryCreate(null, AppContext, out _), "brak appId odrzucony");
        Check(!SonosSessionRequest.TryCreate(AppId, null, out _), "brak appContext odrzucony");
        Check(!SonosSessionRequest.TryCreate("", AppContext, out _), "pusty appId odrzucony");
        Check(!SonosSessionRequest.TryCreate(AppId, "", out _), "pusty appContext odrzucony");

        // maxLength 127 z definicji - granica wchodzi, 128 nie.
        Check(SonosSessionRequest.TryCreate(new string('a', 127), "x", out _), "appId 127 wchodzi");
        Check(!SonosSessionRequest.TryCreate(new string('a', 128), "x", out _), "appId 128 odrzucony");
        Check(SonosSessionRequest.TryCreate("x", new string('b', 127), out _), "appContext 127 wchodzi");
        Check(!SonosSessionRequest.TryCreate("x", new string('b', 128), out _), "appContext 128 odrzucony");

        // SUMA bajtow UTF-8 musi byc MNIEJSZA niz 255. 127 + 127 ASCII = 254 < 255
        // wchodzi, ale te same dlugosci w znakach dwubajtowych NIE.
        Check(SonosSessionRequest.TryCreate(new string('a', 127), new string('b', 127), out _),
            "suma 254 bajtow wchodzi");
        Check(!SonosSessionRequest.TryCreate(new string('ą', 100), new string('b', 60), out _),
            "suma 260 bajtow odrzucona");

        // Samotny surogat: serializator podmienilby go na znak zastepczy, wiec
        // poszlaby INNA wartosc niz podal wolajacy.
        Check(!SonosSessionRequest.TryCreate(AppId, "zly\ud800kontekst", out _), "samotny surogat odrzucony");
        Check(!SonosSessionRequest.TryCreate(AppId, "zly\nkontekst", out _), "znak sterujacy odrzucony");

        // Para surogatow (emoji) jest POPRAWNA i musi przejsc.
        Check(SonosSessionRequest.TryCreate(AppId, "kontekst \U0001F3B5", out _), "para surogatow wchodzi");

        // ToString nie ujawnia ani appId, ani appContext.
        var text = ok.ToString();
        Check(!text.Contains(AppId, StringComparison.Ordinal) && !text.Contains(AppContext, StringComparison.Ordinal),
            "ToString zadania bez identyfikatorow");
    }

    // ================= createSession =================

    private static void CreateSendsBodyAndReadsSessionId()
    {
        var seen = new List<Sent>();
        using var fixture = Fixture.Connected(request =>
        {
            seen.Add(Sent.From(request));
            return Json($$"""
                { "sessionId": "{{SessionId}}", "sessionState": "SESSION_STATE_CONNECTED", "sessionCreated": true }
                """);
        });

        var result = fixture.Create();

        Check(fixture.Requests == 1, "dokladnie jeden POST");
        Check(seen.Count == 1 && seen[0].Method == "POST", "metoda POST");
        // Sciezka GRUPY - createSession wisi na /groups/{groupId}/playbackSession.
        Check(seen[0].Path!.EndsWith("/groups/" + Group + "/playbackSession", StringComparison.Ordinal),
            "sciezka grupy literalna");
        Check(seen[0].Token == Fixture.Access, "bilet biezacego konta");

        // Cialo DOKLADNIE wg definicji: appId i appContext, BEZ accountId i customData.
        Check(seen[0].Body == $"{{\"appId\":\"{AppId}\",\"appContext\":\"{AppContext}\"}}",
            "cialo createSession doslowne");
        Check(!seen[0].HasAccountId && !seen[0].HasCustomData, "bez accountId i customData");

        Check(result.Status == SonosGroupOperationStatus.Attempted, "proba na biezacym koncie");
        Check(result.Accepted && result.Ready, "przyjete i gotowe");
        Check(result.Outcome!.Session!.SessionState == SonosSessionState.Connected, "stan sesji odczytany");
        Check(result.Outcome.Session.SessionCreated == true, "sessionCreated odczytane");

        // sessionId wychodzi TYLKO ta droga i LITERALNIE.
        Check(result.TryGetSessionId(out var id) && id == SessionId, "sessionId oddany literalnie");
    }

    private static void AcceptedWithoutSessionIdIsNotReady()
    {
        // HTTP 200 z sessionId: null - w definicji pole jest NULLABLE, wiec to
        // POPRAWNA odpowiedz, ale NIE gotowa sesja: nie ma czego dalej wyslac.
        using var nullId = Fixture.Connected(_ => Json("""{ "sessionId": null, "sessionCreated": false }"""));
        var withNull = nullId.Create();
        Check(withNull.Accepted, "200 to przyjecie");
        Check(!withNull.Ready, "brak sessionId to NIE gotowa sesja");
        Check(!withNull.TryGetSessionId(out var fromNull) && fromNull is null, "nic nie wychodzi");
        Check(withNull.Outcome!.Session!.SessionCreated == false, "sessionCreated false przeniesione");

        // Pole w ogole pominiete - tak samo.
        using var missing = Fixture.Connected(_ => Json("{}"));
        var withoutField = missing.Create();
        Check(withoutField.Accepted && !withoutField.Ready, "pominiete pole to NIE gotowa sesja");
        Check(withoutField.Outcome!.Session!.SessionCreated is null,
            "brak sessionCreated to NIE WIADOMO, nie false");

        // Pusty napis tez nie jest uzytecznym identyfikatorem.
        using var empty = Fixture.Connected(_ => Json("""{ "sessionId": "" }"""));
        var withEmpty = empty.Create();
        Check(withEmpty.Accepted && !withEmpty.Ready, "pusty sessionId to NIE gotowa sesja");
        Check(!withEmpty.TryGetSessionId(out _), "pusty nie wychodzi");

        // Identyfikator, ktorego nie da sie wstawic w segment sciezki, tez nie.
        using var unsafeId = Fixture.Connected(_ => Json("""{ "sessionId": "zly/segment" }"""));
        var withUnsafe = unsafeId.Create();
        Check(withUnsafe.Accepted && !withUnsafe.Ready, "sessionId ze slashem nie jest uzyteczny");
        Check(!withUnsafe.TryGetSessionId(out _), "niebezpieczny nie wychodzi");

        // 46 znakow z definicji wchodzi, 47 jest odrzucone jako niezgodna odpowiedz.
        var longest = new string('s', 46);
        using var atLimit = Fixture.Connected(_ => Json($$"""{ "sessionId": "{{longest}}" }"""));
        Check(atLimit.Create().TryGetSessionId(out var limitId) && limitId == longest, "sessionId 46 wchodzi");

        var tooLong = new string('s', 47);
        using var overLimit = Fixture.Connected(_ => Json($$"""{ "sessionId": "{{tooLong}}" }"""));
        var over = overLimit.Create();
        Check(over.Outcome!.Status == SonosControlApiStatus.InvalidResponse, "sessionId 47 to niezgodna odpowiedz");
        Check(!over.Ready && !over.TryGetSessionId(out _), "za dlugi nie wychodzi");
    }

    private static void InvalidResponseGivesNoSession()
    {
        foreach (var body in new[]
                 {
                     "to nie jest JSON",
                     "[]",
                     """{ "sessionId": 42 }""",
                     """{ "sessionId": "ok", "sessionCreated": "owszem" }""",
                     """{ "sessionId": "ok", "sessionState": 7 }""",
                     """{ "sessionId": "first", "sessionId": "second" }""",
                     """{ "sessionId": "\uD800" }""",
                     """{ "sessionId": "ok", "sessionState": "\uD800" }""",
                     "{\"sessionId\":\"ok\",\"extra\":" + new string('[', 20)
                         + "0" + new string(']', 20) + "}"
                 })
        {
            using var fixture = Fixture.Connected(_ => Json(body));
            var result = fixture.Create();
            Check(fixture.Requests == 1, "jeden POST nawet przy zlej odpowiedzi");
            Check(result.Outcome!.Status == SonosControlApiStatus.InvalidResponse, "status InvalidResponse");
            Check(!result.Accepted && !result.Ready, "zadna sesja nie uznana");
            Check(!result.TryGetSessionId(out _), "zero identyfikatora");
        }

        // Zly typ tresci - wspolna bramka transportu, nie nowa polityka.
        using var wrongType = Fixture.Connected(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{ "sessionId": "ok" }""", Encoding.UTF8, "text/plain")
        });
        var typed = wrongType.Create();
        Check(typed.Outcome!.Status == SonosControlApiStatus.InvalidResponse, "text/plain odrzucone");
        Check(!typed.TryGetSessionId(out _), "zly typ nie wypuszcza ID");
    }

    private static void SessionStateTableIsParsed()
    {
        // PELNA tabela sessionState: jedyna wartosc z definicji, brak pola i
        // wartosc nieznana (definicja zapowiada zmiany tego pola).
        var table = new (string Body, SonosSessionState Expected)[]
        {
            ("""{ "sessionId": "s1", "sessionState": "SESSION_STATE_CONNECTED" }""", SonosSessionState.Connected),
            ("""{ "sessionId": "s1" }""", SonosSessionState.Unknown),
            ("""{ "sessionId": "s1", "sessionState": null }""", SonosSessionState.Unknown),
            ("""{ "sessionId": "s1", "sessionState": "SESSION_STATE_PRZYSZLY" }""", SonosSessionState.Unknown)
        };

        foreach (var (body, expected) in table)
        {
            using var fixture = Fixture.Connected(_ => Json(body));
            var result = fixture.Create();
            Check(result.Outcome!.Session!.SessionState == expected, "sessionState: " + expected);
            // Nieznany albo brakujacy stan NIE blokuje gotowosci sesji - warunkiem
            // jest sessionId, nie sessionState.
            Check(result.Ready, "gotowosc zalezy od sessionId, nie od sessionState");
        }

        Check(Enum.GetValues<SonosSessionState>().Length == 2, "enum stanu nie urosl bez pomiaru");
    }

    // ================= loadStreamUrl =================

    private static void LoadSendsExactlyOnePost()
    {
        var seen = new List<Sent>();
        using var fixture = Fixture.Connected(request =>
        {
            seen.Add(Sent.From(request));
            return Json("{}");
        });

        var result = fixture.Load();

        Check(fixture.Requests == 1, "dokladnie jeden POST");
        Check(seen.Count == 1 && seen[0].Method == "POST", "metoda POST");
        // INNY zasob niz grupa: /playbackSessions/{sessionId}/...
        // Origin niesie wlasna sciezke bazowa bramki, wiec porownujemy KONIEC.
        Check(seen[0].Path!.EndsWith("/playbackSessions/" + SessionId + "/playbackSession/loadStreamUrl",
            StringComparison.Ordinal), "sciezka sesji literalna");
        Check(!seen[0].Path!.Contains("/groups/", StringComparison.Ordinal), "nie sciezka grupy");

        // Cialo: streamUrl + JAWNY playOnCompletion, BEZ stationMetadata.
        Check(seen[0].StreamUrl == StreamUrl, "adres radia literalny");
        Check(seen[0].PlayOnCompletion == true, "playOnCompletion jawne");
        Check(!seen[0].HasStationMetadata, "bez stationMetadata");
        Check(seen[0].ItemId is null, "itemId pominiety gdy nie podany");

        Check(result.Status == SonosGroupOperationStatus.Attempted && result.Accepted, "przyjete");
        // Przyjecie adresu NIE jest dowodem, ze radio gra.
        Check(!result.EffectConfirmed, "odtwarzanie niepotwierdzone");

        // itemId jako korelacja przyszlego odczytu - podany, wchodzi literalnie.
        seen.Clear();
        var withItem = fixture.Load(itemId: ItemId, playOnCompletion: false);
        Check(seen.Count == 1 && seen[0].ItemId == ItemId, "itemId literalny gdy podany");
        Check(seen[0].PlayOnCompletion == false, "playOnCompletion false jawne, nie pominiete");
        Check(withItem.Accepted, "drugie zadanie przyjete");
        Check(fixture.Requests == 2, "dwa jawne zadania to dwa POST, bez powtorzen");
    }

    private static void InvalidInputNeverPosts()
    {
        using var fixture = Fixture.Connected(_ => Json("{}"));

        // Adres radia: wymagany bezpieczny, poprawny http/https.
        foreach (var bad in new[]
                 {
                     null, "", "   ", "nie-adres", "ftp://stream.invalid/x", "file:///C:/muzyka.mp3",
                     "javascript:alert(1)", "//stream.invalid/x", "http://", "https://zly host/x",
                     "https://stream.invalid/\nzly", "https://stream.invalid/\"zly"
                 })
        {
            var result = fixture.Load(streamUrl: bad);
            Check(result.Status == SonosGroupOperationStatus.Attempted, "zle wejscie rozstrzygniete lokalnie");
            Check(result.Outcome!.Status == SonosControlApiStatus.InvalidConfiguration, "InvalidConfiguration");
            Check(!result.RequestSent && !result.Accepted, "nic nie wyszlo");
        }

        // 1024 znaki z definicji wchodza, 1025 nie.
        var prefix = "https://stream.invalid/";
        var atLimit = prefix + new string('a', 1024 - prefix.Length);
        var overLimit = prefix + new string('a', 1025 - prefix.Length);
        Check(SonosStreamUrlPolicy.IsAcceptable(atLimit), "adres 1024 wchodzi");
        Check(!SonosStreamUrlPolicy.IsAcceptable(overLimit), "adres 1025 odrzucony");

        // sessionId: inny zasob niz groupId, wlasna polityka segmentu.
        foreach (var bad in new[] { null, "", "zly/segment", "zly?segment", "zly#segment", "..", "zly segment" })
        {
            var result = fixture.Load(sessionId: bad);
            Check(result.Outcome!.Status == SonosControlApiStatus.InvalidConfiguration, "zly sessionId odrzucony");
            Check(!result.RequestSent, "zero POST dla zlego sessionId");
        }

        Check(!SonosSessionIdPolicy.IsAcceptable(new string('s', 47)), "sessionId 47 odrzucony");
        Check(SonosSessionIdPolicy.IsAcceptable(new string('s', 46)), "sessionId 46 wchodzi");

        // Zle itemId (gdy podane) tez nie wysyla POST; null to POMINIECIE POLA.
        Check(fixture.Load(itemId: new string('i', 129)).RequestSent == false, "itemId 129 odrzucony");
        Check(fixture.Load(itemId: "").RequestSent == false, "puste itemId odrzucone");
        // Samotny surogat: serializator wyslalby po cichu INNA wartosc, wiec odpada.
        Check(fixture.Load(itemId: "zle\ud800itemId").RequestSent == false, "itemId z samotnym surogatem odrzucony");
        // Znak sterujacy NIE jest odrzucany z rozmyslu: wspolna bramka ciala nie
        // ma whitelisty, bo serializator go escapuje i TOZSAMOSC zostaje ta sama.
        // Nie poprawiamy cudzych identyfikatorow - poprawiony nie wskaze niczego.
        // Ten JEDEN przypadek swiadomie wysyla POST, wiec liczymy go osobno.
        var escaped = fixture.Load(itemId: "znak\nsterujacy");
        Check(escaped.RequestSent, "itemId ze znakiem sterujacym przechodzi (escapowany)");
        Check(fixture.Requests == 1, "tylko ten jeden poprawny przypadek wyslal POST");
        fixture.ResetCounters();

        // Brak zadania sesji i zla grupa: createSession tez nie wysyla POST.
        Check(!fixture.CreateRaw(Group, null).RequestSent, "brak zadania sesji: zero POST");
        Check(!fixture.Create(groupId: "zla/grupa").RequestSent, "zla grupa: zero POST");
        Check(!fixture.Create(groupId: null).RequestSent, "brak grupy: zero POST");

        Check(fixture.Requests == 0, "LACZNIE zero zapytan dla calego zlego wejscia");
        Check(fixture.Refreshes == 0 && fixture.Deletes == 0, "zero odnowien i kasowania");
    }

    // ================= ochrona konta i brak ponowien =================

    private static void UnauthorizedNeverRetriesWrite()
    {
        // 401 na ZAPISIE: zaden z dwoch endpointow nie odnawia dostepu po fakcie
        // i nie powtarza POST. Drugi POST createSession utworzylby DRUGA sesje,
        // znow przejmujac grupe.
        using var create = Fixture.Connected(_ => Json("""{ "sessionId": "nie-uznane" }""", HttpStatusCode.Unauthorized));
        var createResult = create.Create();
        Check(create.Requests == 1, "createSession: dokladnie jeden POST po 401");
        Check(create.Refreshes == 0, "createSession: zero odnowien");
        Check(create.Deletes == 0, "createSession: konto NIE kasowane");
        Check(createResult.Outcome!.Status == SonosControlApiStatus.Unauthorized, "status Unauthorized");
        Check(!createResult.Accepted && !createResult.Ready, "brak sesji");
        // Cialo odpowiedzi BLEDU nie jest czytane, wiec zaden identyfikator z
        // niego nie wychodzi.
        Check(!createResult.TryGetSessionId(out _), "zero ID z odpowiedzi bledu");

        using var load = Fixture.Connected(_ => Json("{}", HttpStatusCode.Unauthorized));
        var loadResult = load.Load();
        Check(load.Requests == 1 && load.Refreshes == 0 && load.Deletes == 0,
            "loadStreamUrl: jeden POST, zero odnowien, zero kasowania");
        Check(loadResult.Outcome!.Status == SonosControlApiStatus.Unauthorized, "load: Unauthorized");

        // NotFound (sesja zamknieta albo przejeta) tez NIE tworzy nowej sesji.
        using var gone = Fixture.Connected(_ => Json("{}", HttpStatusCode.NotFound));
        var goneResult = gone.Load();
        Check(gone.Requests == 1, "NotFound: dokladnie jeden POST, zadnego tworzenia sesji");
        Check(goneResult.Outcome!.Status == SonosControlApiStatus.NotFound, "load: NotFound");
    }

    private static void AccountChangeNeverPublishesSessionId()
    {
        // Ta sama WSPOLNA granica generacji co F3: nowe logowanie w trakcie
        // wstrzymanej odpowiedzi porzuca wynik. Tu stawka jest wyzsza niz lista -
        // sessionId konta A nie moze trafic do kontekstu konta B.
        var gate = new Gate();
        using var fixture = Fixture.Connected(_ =>
        {
            gate.EnterAndWait();
            return Json($$"""{ "sessionId": "{{SessionId}}", "sessionCreated": true }""");
        });

        var pending = Task.Run(() => fixture.Create());
        Check(gate.WaitEntered(), "zapytanie weszlo w transport");

        try
        {
            fixture.LogInAsNewAccount();
        }
        finally
        {
            gate.Release();
        }

        var result = pending.WaitAsync(TimeSpan.FromSeconds(15)).GetAwaiter().GetResult();

        Check(result.Discarded && result.Status == SonosGroupOperationStatus.Discarded, "wynik porzucony");
        Check(result.Outcome is null, "zero danych transportu starego konta");
        // NAJWAZNIEJSZE: identyfikator sesji konta A nie wychodzi do konta B.
        Check(!result.TryGetSessionId(out var leaked) && leaked is null, "sessionId NIE opublikowany");
        Check(!result.Ready && !result.Accepted, "nic nie uznane za gotowe");
        // Zapis JUZ poszedl, wiec uczciwie: skutek NIEZNANY, a nie "cofnieto".
        Check(result.RequestSent && result.EffectAmbiguous, "proba i nieznany skutek, bez udawania cofniecia");
        Check(fixture.Requests == 1, "zero powtorzen po zmianie konta");
    }

    private static void CancelAndEvictionNeverRecreateSession()
    {
        // 1. Anulowanie PRZED wyslaniem: zadnego POST.
        using var canceled = Fixture.Connected(_ => Json("{}"));
        using var source = new CancellationTokenSource();
        source.Cancel();
        var createCanceled = canceled.Coordinator
            .CreateSessionAsync(canceled.CreateApi, Group, Request(), source.Token)
            .WaitAsync(Deadline).GetAwaiter().GetResult();
        Check(canceled.Requests == 0, "anulowanie przed wyslaniem: zero POST");
        Check(!createCanceled.Accepted && !createCanceled.Ready, "zadnej sesji");
        Check(!createCanceled.TryGetSessionId(out _), "zero ID");

        var loadCanceled = canceled.Coordinator
            .LoadStreamUrlAsync(canceled.LoadApi, SessionId, StreamUrl, true, null, source.Token)
            .WaitAsync(Deadline).GetAwaiter().GetResult();
        Check(canceled.Requests == 0, "anulowany load: zero POST");
        Check(!loadCanceled.Accepted, "load nieprzyjety");

        // 2. ERROR_SESSION_EVICTED na loadStreamUrl: sesje przejela inna
        // aplikacja. Wynik to BLAD - zadnego automatycznego createSession, bo
        // nowa sesja przerwalaby to, co wlasnie gra u uzytkownika.
        using var evicted = Fixture.Connected(_ => Json(
            """{ "errorCode": "ERROR_SESSION_EVICTED", "reason": "session evicted" }""",
            HttpStatusCode.BadRequest));
        var evictedResult = evicted.Load();
        Check(evicted.Requests == 1, "eviction: DOKLADNIE jeden POST, zadnego tworzenia sesji");
        Check(evictedResult.Outcome!.Status == SonosControlApiStatus.RequestRejected, "eviction odrzucone");
        Check(!evictedResult.Accepted, "nic nie przyjete");

        // 3. Zerwane polaczenie na zapisie: skutek NIEZNANY, zero ponowien.
        using var broken = Fixture.Connected(_ => throw new HttpRequestException("atrapa: zerwane polaczenie"));
        var brokenResult = broken.Create();
        Check(broken.Requests == 1, "zerwanie: jeden POST, zero ponowien");
        Check(brokenResult.Outcome!.Status == SonosControlApiStatus.Unreachable, "Unreachable");
        Check(brokenResult.EffectAmbiguous, "skutek NIEZNANY, nie 'sesji nie ma'");
        Check(!brokenResult.TryGetSessionId(out _), "zero ID przy nieznanym skutku");
    }

    // ================= koordynator =================

    private static void CoordinatorExposesBothWrites()
    {
        // Dwie JAWNE, ROZDZIELONE metody - zadnej jednej metody "zagraj radio",
        // ktora po cichu tworzylaby sesje przy kazdym wczytaniu.
        var create = typeof(SonosAccountCoordinator)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(candidate =>
                candidate.Name == "CreateSessionAsync"
                && candidate.ReturnType == typeof(Task<SonosSessionCreateResult>));
        var load = typeof(SonosAccountCoordinator)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(candidate =>
                candidate.Name == "LoadStreamUrlAsync"
                && candidate.ReturnType == typeof(Task<SonosStreamUrlLoadResult>));

        Check(create is not null && load is not null, "obie metody obecne");

        // ZERO wartosci domyslnych: autostart i tozsamosc aplikacji musza byc
        // wybrane jawnie przez wolajacego.
        foreach (var parameter in create!.GetParameters().Concat(load!.GetParameters()))
        {
            Check(!parameter.HasDefaultValue, "brak wartosci domyslnej: " + parameter.Name);
        }

        Check(load.GetParameters().Any(p => p.Name == "playOnCompletion" && p.ParameterType == typeof(bool)),
            "playOnCompletion jawny bool");

        // Null api to blad WOLAJACEGO, nie ciche zero.
        using var fixture = Fixture.Connected(_ => Json("{}"));
        var threwCreate = false;
        var threwLoad = false;
        try
        {
            fixture.Coordinator.CreateSessionAsync(null!, Group, Request(), CancellationToken.None)
                .WaitAsync(Deadline).GetAwaiter().GetResult();
        }
        catch (ArgumentNullException) { threwCreate = true; }

        try
        {
            fixture.Coordinator.LoadStreamUrlAsync(null!, SessionId, StreamUrl, true, null, CancellationToken.None)
                .WaitAsync(Deadline).GetAwaiter().GetResult();
        }
        catch (ArgumentNullException) { threwLoad = true; }

        Check(threwCreate && threwLoad && fixture.Requests == 0, "null api: wyjatek i zero zapytan");

        // Brak konta: ZERO zapytan, ZERO odnowien, ZERO kasowania i zadnej sesji.
        using var noAccount = Fixture.WithoutAccount(_ => Json("""{ "sessionId": "nie-powinno-byc" }"""));
        var createNoAccount = noAccount.Create();
        Check(createNoAccount.Status == SonosGroupOperationStatus.NoAccount, "createSession bez konta");
        Check(!createNoAccount.RequestSent && !createNoAccount.TryGetSessionId(out _), "zero ID bez konta");
        var loadNoAccount = noAccount.Load();
        Check(loadNoAccount.Status == SonosGroupOperationStatus.NoAccount, "load bez konta");
        Check(noAccount.Requests == 0 && noAccount.Refreshes == 0 && noAccount.Deletes == 0,
            "zero zapytan, odnowien i kasowania bez konta");
    }

    private static void MessagesHideSecrets()
    {
        var secrets = new[] { Fixture.Access, Fixture.AccessB, Key, SessionId, StreamUrl, AppContext, AppId, ItemId };

        // PELNA tabela statusow w OBU slownikach sesji.
        foreach (SonosControlApiStatus status in Enum.GetValues<SonosControlApiStatus>())
        {
            foreach (var sent in new[] { true, false })
            {
                foreach (var message in new[]
                         {
                             SonosPlaybackSessionMessages.DescribeCreate(status, sent, false, false),
                             SonosPlaybackSessionMessages.DescribeCreate(status, sent, false, true),
                             SonosPlaybackSessionMessages.DescribeCreate(status, sent, true, false),
                             SonosPlaybackSessionMessages.DescribeLoad(status, sent, false),
                             SonosPlaybackSessionMessages.DescribeLoad(status, sent, true)
                         })
                {
                    Check(!string.IsNullOrWhiteSpace(message), "komunikat niepusty");
                    foreach (var secret in secrets)
                    {
                        Check(!message.Contains(secret, StringComparison.OrdinalIgnoreCase),
                            "komunikat bez tajemnic: " + status);
                    }
                }
            }
        }

        // "Przyjete bez identyfikatora" ma WLASNE zdanie - nie udaje sukcesu.
        var withoutId = SonosPlaybackSessionMessages.DescribeCreate(SonosControlApiStatus.Success, true, false, false);
        var ready = SonosPlaybackSessionMessages.DescribeCreate(SonosControlApiStatus.Success, true, false, true);
        Check(withoutId != ready, "brak ID mowi inaczej niz gotowa sesja");
        Check(withoutId.Contains("identyfikator", StringComparison.OrdinalIgnoreCase), "nazywa brak identyfikatora");

        // Zaden komunikat sukcesu nie oglasza, ze muzyka gra.
        Check(!SonosPlaybackSessionMessages.DescribeLoad(SonosControlApiStatus.Success, true, false)
            .Contains("gra.", StringComparison.OrdinalIgnoreCase), "nie oglasza grania");

        // ToString na WSZYSTKICH typach wyniku: zero sessionId i zero adresu.
        using var fixture = Fixture.Connected(_ => Json($$"""
            { "sessionId": "{{SessionId}}", "sessionState": "SESSION_STATE_CONNECTED", "sessionCreated": true }
            """));
        var created = fixture.Create();
        Check(created.Ready, "sesja gotowa do pomiaru ToString");

        using var loader = Fixture.Connected(_ => Json("{}"));
        var loaded = loader.Load(itemId: ItemId);

        foreach (var text in new[]
                 {
                     created.ToString(), created.Outcome!.ToString(), created.Outcome.Session!.ToString(),
                     created.Message, loaded.ToString(), loaded.Outcome!.ToString(), loaded.Message
                 })
        {
            foreach (var secret in secrets)
            {
                Check(!text.Contains(secret, StringComparison.OrdinalIgnoreCase), "ToString bez tajemnic");
            }
        }

        // Wyniki DO POMIARU maja PUSTA migawke, zeby nikt nie pomylil ich z kontem.
        Check(!SonosSessionCreateResult.CreateForMeasurement(SonosGroupOperationStatus.NoAccount, null, false)
            .Snapshot.HasCredentials, "pomiarowy wynik sesji bez poswiadczen");
        Check(!SonosStreamUrlLoadResult.CreateForMeasurement(SonosGroupOperationStatus.NoAccount, null, false)
            .Snapshot.HasCredentials, "pomiarowy wynik radia bez poswiadczen");
    }

    // ================= aparatura =================

    private static SonosSessionRequest Request()
    {
        Check(SonosSessionRequest.TryCreate(AppId, AppContext, out var request), "zadanie sesji zbudowane");
        return request!;
    }

    private static void Check(bool ok, string label = "bez etykiety")
    {
        if (!ok)
        {
            throw new InvalidOperationException("Niespelniona asercja: " + label);
        }
    }

    private static HttpResponseMessage Json(string body, HttpStatusCode code = HttpStatusCode.OK) =>
        new(code) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    /// <summary>Co NAPRAWDE poszlo w zadaniu - odczytane z prawdziwego HttpRequestMessage.</summary>
    private sealed record Sent(
        string? Method, string? Token, string? Path, string? Body,
        string? StreamUrl, bool? PlayOnCompletion, string? ItemId,
        bool HasStationMetadata, bool HasAccountId, bool HasCustomData)
    {
        internal static Sent From(HttpRequestMessage request)
        {
            string? body = null;
            string? streamUrl = null;
            bool? play = null;
            string? itemId = null;
            var hasStationMetadata = false;
            var hasAccountId = false;
            var hasCustomData = false;
            if (request.Content is { } content)
            {
                body = content.ReadAsStringAsync().GetAwaiter().GetResult();
                using var document = JsonDocument.Parse(body);
                var root = document.RootElement;
                streamUrl = root.TryGetProperty("streamUrl", out var url) ? url.GetString() : null;
                play = root.TryGetProperty("playOnCompletion", out var flag) ? flag.GetBoolean() : null;
                itemId = root.TryGetProperty("itemId", out var item) ? item.GetString() : null;
                hasStationMetadata = root.TryGetProperty("stationMetadata", out _);
                hasAccountId = root.TryGetProperty("accountId", out _);
                hasCustomData = root.TryGetProperty("customData", out _);
            }

            return new Sent(
                request.Method.Method,
                request.Headers.Authorization?.Parameter,
                request.RequestUri?.AbsolutePath,
                body, streamUrl, play, itemId,
                hasStationMetadata, hasAccountId, hasCustomData);
        }
    }

    /// <summary>Bramka zdarzeniowa z TWARDYM limitem: niedotrzymanie to BLAD, nie ciche przejscie.</summary>
    private sealed class Gate
    {
        private readonly TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource released = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal void EnterAndWait()
        {
            entered.TrySetResult();
            if (!released.Task.Wait(TimeSpan.FromSeconds(10)))
            {
                throw new TimeoutException("Bramka testu nie zostala zwolniona w terminie.");
            }
        }

        internal bool WaitEntered() => entered.Task.Wait(Deadline);

        internal void Release() => released.TrySetResult();
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> reply) : HttpMessageHandler
    {
        private int count;

        internal Func<HttpRequestMessage, HttpResponseMessage> Reply { get; set; } = reply;

        public int Count => Volatile.Read(ref count);

        internal void Reset() => Volatile.Write(ref count, 0);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref count);
            var response = Reply(request);
            // Bez tego klient nie widzi, na ktory adres przyszla odpowiedz.
            response.RequestMessage ??= request;
            return Task.FromResult(response);
        }
    }

    private sealed class Fixture : IDisposable
    {
        internal const string Access = "SYNTHETIC-ACCESS-ONLY";
        internal const string AccessB = "SYNTHETIC-ACCESS-B";
        internal const string Renewed = "SYNTHETIC-ACCESS-RENEWED";
        private const string Broker = "https://broker.invalid/";

        private readonly Handler handler;
        private readonly SonosControlApiClient client;
        private readonly Store store;

        private Fixture(SonosAccountCoordinator coordinator, Handler handler, SonosControlApiClient client, Gateway gateway, Store store)
        {
            Coordinator = coordinator;
            this.handler = handler;
            this.client = client;
            this.store = store;
            Gateway = gateway;
            // PRAWDZIWY klient Control API JUZ implementuje oba waskie kontrakty.
            CreateApi = client;
            LoadApi = client;
        }

        internal SonosAccountCoordinator Coordinator { get; }

        internal ISonosSessionCreateApi CreateApi { get; }

        internal ISonosStreamUrlLoadApi LoadApi { get; }

        internal Gateway Gateway { get; }

        internal int Requests => handler.Count;

        /// <summary>Zeruje licznik zapytan, gdy jeden przypadek w tescie
        /// SWIADOMIE wysyla POST, a reszta ma wysylac zero.</summary>
        internal void ResetCounters() => handler.Reset();

        internal int Refreshes => Gateway.RefreshCalls;

        internal string? StoredAccessToken => store.AccessToken;

        internal int Deletes => store.Deletes;

        internal static Fixture Connected(Func<HttpRequestMessage, HttpResponseMessage> reply) =>
            Create(reply, connected: true);

        internal static Fixture WithoutAccount(Func<HttpRequestMessage, HttpResponseMessage> reply) =>
            Create(reply, connected: false);

        private static Fixture Create(Func<HttpRequestMessage, HttpResponseMessage> reply, bool connected)
        {
            var now = new DateTimeOffset(2026, 9, 27, 12, 0, 0, TimeSpan.Zero);
            var store = new Store();
            if (connected)
            {
                store.Seed(new SonosStoredCredentials(
                    Broker,
                    new SonosTokens(Access, "Bearer", 3600, "SYNTHETIC-REFRESH", "playback-control-all"),
                    now));
            }

            var gateway = new Gateway();
            var coordinator = new SonosAccountCoordinator(gateway, store, Broker, () => now);
            coordinator.RestoreOnce();

            var handler = new Handler(reply);
            var client = new SonosControlApiClient(SonosControlApiConfiguration.CreateDefault(Key), handler);
            return new Fixture(coordinator, handler, client, gateway, store);
        }

        /// <summary>PRAWDZIWA droga nowego logowania, bez podstawiania pol i bez refleksji.</summary>
        internal void LogInAsNewAccount()
        {
            Gateway.AllowLogin(AccessB);
            Coordinator.BeginLoginAsync(CancellationToken.None)
                .WaitAsync(Deadline).GetAwaiter().GetResult();
            var login = Coordinator.CheckLoginAsync(CancellationToken.None)
                .WaitAsync(Deadline).GetAwaiter().GetResult();
            Check(login.Snapshot.HasCredentials, "nowe konto ma poswiadczenia");
            Check(StoredAccessToken == AccessB, "nowe konto zapisane");
        }

        internal SonosSessionCreateResult Create(string? groupId = Group) =>
            CreateRaw(groupId, SonosStreamUrlTests.Request());

        internal SonosSessionCreateResult CreateRaw(string? groupId, SonosSessionRequest? request) =>
            Coordinator.CreateSessionAsync(CreateApi, groupId, request, CancellationToken.None)
                .WaitAsync(TimeSpan.FromSeconds(15)).GetAwaiter().GetResult();

        internal SonosStreamUrlLoadResult Load(
            string? sessionId = SessionId,
            string? streamUrl = StreamUrl,
            bool playOnCompletion = true,
            string? itemId = null) =>
            Coordinator.LoadStreamUrlAsync(LoadApi, sessionId, streamUrl, playOnCompletion, itemId, CancellationToken.None)
                .WaitAsync(TimeSpan.FromSeconds(15)).GetAwaiter().GetResult();

        public void Dispose()
        {
            Coordinator.Dispose();
            client.Dispose();
            handler.Dispose();
        }
    }

    /// <summary>Atrapa bramki logowania: zero sieci, policzone odnowienia.</summary>
    private sealed class Gateway : ISonosLoginGateway
    {
        private string? loginAccessToken;
        private int refreshCalls;

        public int RefreshCalls => Volatile.Read(ref refreshCalls);

        internal void AllowLogin(string accessToken) => loginAccessToken = accessToken;

        public Task<SonosLoginStartOutcome> StartAsync(CancellationToken cancellationToken) =>
            Task.FromResult(loginAccessToken is null
                ? SonosLoginStartOutcome.Failure(SonosLoginStatus.BrokerUnreachable)
                : SonosLoginStartOutcome.Ok(new SonosLoginSession(
                    "synthetic-session",
                    new string('a', 48),
                    new Uri("https://api.sonos.com/login/v3/oauth"),
                    DateTimeOffset.UtcNow.AddMinutes(5))));

        public Task<SonosLoginResultOutcome> FetchResultAsync(SonosLoginSession session, CancellationToken cancellationToken) =>
            Task.FromResult(loginAccessToken is null
                ? SonosLoginResultOutcome.Failure(SonosLoginStatus.BrokerUnreachable)
                : SonosLoginResultOutcome.Ok(new SonosTokens(
                    loginAccessToken, "Bearer", 3600, "SYNTHETIC-REFRESH-B", "playback-control-all")));

        public Task<SonosRefreshOutcome> RefreshAsync(string? token, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref refreshCalls);
            return Task.FromResult(SonosRefreshOutcome.Ok(new SonosTokens(
                Fixture.Renewed, "Bearer", 3600, "SYNTHETIC-REFRESH", "playback-control-all")));
        }
    }

    /// <summary>Atrapa magazynu w PAMIECI: bez DPAPI i bez pliku. Liczy zapisy i usuniecia.</summary>
    private sealed class Store : ISonosCredentialStore
    {
        private SonosStoredCredentials? record;

        internal void Seed(SonosStoredCredentials value) => record = value;

        internal string? AccessToken => record?.Tokens.AccessToken;

        internal int Deletes { get; private set; }

        public SonosCredentialReadOutcome Read() => record is null
            ? SonosCredentialReadOutcome.Failure(SonosCredentialReadStatus.Missing)
            : SonosCredentialReadOutcome.Ok(record);

        public SonosCredentialWriteOutcome Write(SonosStoredCredentials credentials)
        {
            record = credentials;
            return SonosCredentialWriteOutcome.Ok();
        }

        public bool Delete()
        {
            Deletes++;
            record = null;
            return true;
        }
    }
}
