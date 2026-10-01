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
/// ZMIANA SKLADU GRUP SONOSA - createGroup + setGroupMembers w warstwie Core.
/// Tylko Core: zero GUI, zero prawdziwego konta, zero sieci, zero muzyki i zero
/// przestawiania czyichkolwiek glosnikow. Dane syntetyczne, atrapa bramki
/// logowania, atrapa magazynu w pamieci i PRAWDZIWY
/// <see cref="SonosControlApiClient"/> na kontrolowanym HttpMessageHandler.
/// Wywolania ida PRZEZ publiczny <see cref="ISonosGroupMembershipApi"/> i
/// PRZEZ koordynator, wiec mierzony jest caly tor konto -> klient -> HTTP.
///
/// Zakres CELOWO WASKI i nie powielajacy odebranych suit (sesja odtwarzania,
/// polecenia grupy, listy): macierz zabezpieczen HTTP i OAuth - przekierowania,
/// budzet tresci, kodowanie, naglowki, 403/404/429/5xx, odnawianie, kasowanie
/// konta - zostala zmierzona na TYM SAMYM wspolnym transporcie
/// (<c>WriteCoreAsync</c>) i TYM SAMYM torze koordynatora
/// (<c>RunSessionWriteAsync</c>). Tutaj mierzymy to, co dla skladu grup jest
/// NOWE albo INNE:
///
///   * zestaw glosnikow: limity 32/24 z definicji, LITERALNE identyfikatory
///     (spacje i znaki spoza ASCII dopuszczone, bo to wartosc JSON, nie segment
///     adresu) i odrzucenie duplikatow oraz samotnego surogatu,
///   * createGroup: wlasciwy URI DOMU, cialo z playerIds i musicContextGroupId,
///     oraz cialo BEZ musicContextGroupId jako grupa bez dzwieku,
///   * setGroupMembers: wlasciwy URI GRUPY i cialo z pelnym zestawem,
///   * odczyt INNEGO groupId niz zadany - createGroup moze oddac identyfikator
///     grupy istniejacej,
///   * HTTP 200 bez obiektu grupy to przyjecie, ale NIE potwierdzenie skladu,
///   * zle wejscie daje ZERO POST,
///   * 401 na zapisie: jeden POST, zero odnowien, zero powtorzen,
///   * zmiana konta podczas wstrzymanej odpowiedzi NIE publikuje groupId,
///   * komunikaty i ToString nie ujawniaja groupId ani identyfikatorow glosnikow.
/// </summary>
internal static class SonosGroupMembershipTests
{
    private const string Key = "00000000-0000-0000-0000-000000000042";
    private const string Household = "Sonos_synthetic.household-42";
    private const string Group = "RINCON_0001:1";
    private const string PlayerA = "RINCON_SYNTH_A";
    private const string PlayerB = "RINCON_SYNTH_B";
    private const string MusicContext = "RINCON_0001:7";
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(5);

    public static void Run()
    {
        var tests = new List<(string Name, Action Test)>
        {
            ("zestaw głośników: limity 32/24, literalność, duplikaty", PlayerSetEnforcesContract),
            ("createGroup: URI domu, ciało z playerIds i musicContext", CreateSendsHouseholdBody),
            ("createGroup: brak musicContext to grupa BEZ dźwięku", CreateWithoutMusicContextOmitsField),
            ("createGroup: oddany INNY groupId niż żądany", CreateReturnsServerChosenGroupId),
            ("setGroupMembers: URI grupy, ciało z pełnym zestawem", SetSendsGroupBody),
            ("200 bez obiektu grupy to NIE potwierdzenie składu", AcceptedWithoutGroupIsNotConfirmed),
            ("niezgodna odpowiedź nie daje identyfikatora grupy", InvalidResponseGivesNoGroupId),
            ("złe wejście: zero POST na obu operacjach", InvalidInputNeverPosts),
            ("401 na zapisie: jeden POST, zero odnowień i powtórzeń", UnauthorizedNeverRetriesWrite),
            ("zmiana konta w trakcie: groupId NIE jest publikowany", AccountChangeNeverPublishesGroupId),
            ("koordynator: dwie jawne metody, bez wartości domyślnych", CoordinatorExposesBothWrites),
            ("komunikaty i ToString bez groupId i bez głośników", MessagesHideIdentifiers)
        };

        var failures = 0;
        foreach (var (name, test) in tests)
        {
            try { test(); Console.WriteLine("[OK] " + name); }
            catch (Exception ex) { failures++; Console.WriteLine("[FAIL] " + name + " (" + ex.GetType().Name + ": " + ex.Message + ")"); }
        }

        Console.WriteLine($"Zmiana składu grup Sonos w Core: {tests.Count - failures}/{tests.Count}");
        if (failures != 0)
        {
            throw new InvalidOperationException("Zmiana składu grup Sonos w Core: " + failures + " nieudanych testow.");
        }
    }

    // ================= kontrakt zestawu glosnikow =================

    private static void PlayerSetEnforcesContract()
    {
        Check(SonosPlayerSet.TryCreate(new[] { PlayerA, PlayerB }, out var ok) && ok is not null, "poprawny zestaw");
        Check(ok!.Count == 2 && ok.PlayerIds[0] == PlayerA && ok.PlayerIds[1] == PlayerB,
            "kolejnosc i wartosci zachowane literalnie");

        // Pusty zestaw: NASZA polityka jawnego, pelnego zestawu. W definicji
        // playerIds jest nullable, ale puste wejscie nie jest poleceniem.
        Check(!SonosPlayerSet.TryCreate(System.Array.Empty<string>(), out _), "pusty zestaw odrzucony");
        Check(!SonosPlayerSet.TryCreate(null, out _), "brak zestawu odrzucony");

        // maxItems 32 z definicji - granica wchodzi, 33 nie.
        var atLimit = Enumerable.Range(0, 32).Select(i => "P" + i).ToArray();
        Check(SonosPlayerSet.TryCreate(atLimit, out _), "32 glosniki wchodza");
        var overLimit = Enumerable.Range(0, 33).Select(i => "P" + i).ToArray();
        Check(!SonosPlayerSet.TryCreate(overLimit, out _), "33 glosniki odrzucone");

        // items.maxLength 24 z definicji - granica wchodzi, 25 nie.
        Check(SonosPlayerSet.TryCreate(new[] { new string('p', 24) }, out _), "playerId 24 wchodzi");
        Check(!SonosPlayerSet.TryCreate(new[] { new string('p', 25) }, out _), "playerId 25 odrzucony");

        // playerId idzie w CIELE JSON, nie w sciezce: spacja i znaki spoza ASCII
        // sa dopuszczone, bo serializator je escapuje. Nie stosujemy tu polityki
        // segmentu adresu, ktora odrzucilaby takie wartosci.
        Check(SonosPlayerSet.TryCreate(new[] { "gło śnik ąćż" }, out _), "spacje i polskie znaki dopuszczone");
        Check(SonosPlayerSet.TryCreate(new[] { "salon \U0001F3B5" }, out _), "para surogatow wchodzi");

        // Pusty napis nie wskazuje glosnika; duplikat to blad wolajacego.
        Check(!SonosPlayerSet.TryCreate(new[] { PlayerA, "" }, out _), "pusty playerId odrzucony");
        Check(!SonosPlayerSet.TryCreate(new[] { PlayerA, PlayerA }, out _), "duplikat odrzucony");

        // Samotny surogat: poszlaby INNA wartosc niz podal wolajacy.
        Check(!SonosPlayerSet.TryCreate(new[] { "zly\ud800gracz" }, out _), "samotny surogat odrzucony");

        // musicContextGroupId: maxLength 35, null legalny, pusty napis nie.
        Check(SonosCreateGroupRequest.TryCreate(new[] { PlayerA }, null, out var noMusic)
            && noMusic!.CarriesMusicContext == false, "brak musicContext legalny");
        Check(SonosCreateGroupRequest.TryCreate(new[] { PlayerA }, new string('g', 35), out _),
            "musicContext 35 wchodzi");
        Check(!SonosCreateGroupRequest.TryCreate(new[] { PlayerA }, new string('g', 36), out _),
            "musicContext 36 odrzucony");
        Check(!SonosCreateGroupRequest.TryCreate(new[] { PlayerA }, "", out _), "pusty musicContext odrzucony");

        // ToString nie ujawnia identyfikatorow.
        Check(!ok.ToString().Contains(PlayerA, StringComparison.Ordinal), "ToString zestawu bez identyfikatorow");
        Check(!noMusic!.ToString().Contains(PlayerA, StringComparison.Ordinal), "ToString zadania bez identyfikatorow");
    }

    // ================= createGroup =================

    private static void CreateSendsHouseholdBody()
    {
        var seen = new List<Sent>();
        using var fixture = Fixture.Connected(request =>
        {
            seen.Add(Sent.From(request));
            return GroupJson(Group, PlayerA, PlayerB);
        });

        var result = fixture.Create(MusicContext);

        Check(fixture.Requests == 1, "dokladnie jeden POST");
        Check(seen.Count == 1 && seen[0].Method == "POST", "metoda POST");
        // Sciezka DOMU - createGroup wisi na /households/{householdId}/groups/createGroup,
        // a NIE na zasobie grupy.
        Check(seen[0].Path!.EndsWith("/households/" + Household + "/groups/createGroup", StringComparison.Ordinal),
            "sciezka domu literalna");
        Check(seen[0].Token == Fixture.Access, "bilet biezacego konta");

        // Cialo DOKLADNIE wg definicji: playerIds + musicContextGroupId, BEZ areaIds.
        Check(seen[0].Body == $"{{\"playerIds\":[\"{PlayerA}\",\"{PlayerB}\"],\"musicContextGroupId\":\"{MusicContext}\"}}",
            "cialo createGroup doslowne");
        Check(!seen[0].HasAreaIds, "bez areaIds");
        // playerIds poszly LITERALNIE, w podanej kolejnosci.
        Check(seen[0].PlayerIds!.SequenceEqual(new[] { PlayerA, PlayerB }), "playerIds literalne i w kolejnosci");

        Check(result.Status == SonosGroupOperationStatus.Attempted, "proba na biezacym koncie");
        Check(result.Accepted && result.HasGroupId, "przyjete i z identyfikatorem");
        // Przyjecie NIE jest dowodem skladu - to pokaze tylko swiezy odczyt.
        Check(!result.EffectConfirmed, "przyjecie to NIE potwierdzenie skladu");
        Check(result.TryGetGroupId(out var id) && id == Group, "groupId oddany literalnie");
        Check(result.Outcome!.Info!.Group!.PlayerIds.SequenceEqual(new[] { PlayerA, PlayerB }),
            "odczytany sklad z odpowiedzi");
    }

    private static void CreateWithoutMusicContextOmitsField()
    {
        // Wg definicji brak musicContextGroupId znaczy grupa BEZ audio. Pole musi
        // wiec byc POMINIETE, a nie wyslane jako null albo pusty napis.
        var seen = new List<Sent>();
        using var fixture = Fixture.Connected(request =>
        {
            seen.Add(Sent.From(request));
            return GroupJson(Group, PlayerA);
        });

        var result = fixture.Create(musicContextGroupId: null, PlayerA);

        Check(fixture.Requests == 1, "jeden POST");
        Check(seen[0].Body == $"{{\"playerIds\":[\"{PlayerA}\"]}}", "cialo bez musicContextGroupId");
        Check(!seen[0].HasMusicContext, "pole POMINIETE, nie null");
        Check(result.Accepted && result.HasGroupId, "grupa bez dzwieku to poprawne zlecenie");
    }

    private static void CreateReturnsServerChosenGroupId()
    {
        // Wg definicji zwrocony identyfikator MOZE byc identyfikatorem grupy
        // ISTNIEJACEJ, gdy jest ona podzbiorem zadanej. Wolajacy musi uzyc
        // wartosci Z ODPOWIEDZI, nie tej, ktora wyslal.
        const string Other = "RINCON_0009:9";
        using var fixture = Fixture.Connected(_ => GroupJson(Other, PlayerA, PlayerB));

        var result = fixture.Create(MusicContext);

        Check(result.TryGetGroupId(out var id) && id == Other, "oddany groupId Z ODPOWIEDZI");
        Check(id != Group && id != MusicContext, "to INNY identyfikator niz zadany");

        // Ten sam odczyt dziala dla setGroupMembers: grupa mogla zostac scalona.
        using var set = Fixture.Connected(_ => GroupJson(Other, PlayerA));
        Check(set.Set().TryGetGroupId(out var setId) && setId == Other, "setGroupMembers tez oddaje groupId z odpowiedzi");
    }

    // ================= setGroupMembers =================

    private static void SetSendsGroupBody()
    {
        var seen = new List<Sent>();
        using var fixture = Fixture.Connected(request =>
        {
            seen.Add(Sent.From(request));
            return GroupJson(Group, PlayerA, PlayerB);
        });

        var result = fixture.Set(PlayerA, PlayerB);

        Check(fixture.Requests == 1, "dokladnie jeden POST");
        // Sciezka GRUPY - setGroupMembers wisi na /groups/{groupId}/groups/setGroupMembers.
        Check(seen[0].Path!.EndsWith("/groups/" + Group + "/groups/setGroupMembers", StringComparison.Ordinal),
            "sciezka grupy literalna");

        // Cialo: TYLKO playerIds. musicContextGroupId w tym schemacie NIE ISTNIEJE,
        // wiec nie wolno go dosylac.
        Check(seen[0].Body == $"{{\"playerIds\":[\"{PlayerA}\",\"{PlayerB}\"]}}", "cialo setGroupMembers doslowne");
        Check(!seen[0].HasMusicContext && !seen[0].HasAreaIds, "bez musicContext i bez areaIds");
        Check(result.Accepted && result.HasGroupId, "przyjete");
        Check(!result.EffectConfirmed, "przyjecie to NIE potwierdzenie skladu");
    }

    // ================= odpowiedzi, ktorych nie wolno uznac =================

    private static void AcceptedWithoutGroupIsNotConfirmed()
    {
        // group jest w definicji NULLABLE i NIE jest required, wiec oba ponizsze
        // ciala to POPRAWNE odpowiedzi - ale skladu nie potwierdzaja.
        foreach (var body in new[] { """{ "group": null }""", "{}" })
        {
            using var fixture = Fixture.Connected(_ => Json(body));
            var result = fixture.Create(MusicContext);
            Check(result.Accepted, "200 to przyjecie");
            Check(!result.HasGroupId, "brak obiektu grupy to NIE potwierdzenie");
            Check(!result.TryGetGroupId(out var leaked) && leaked is null, "nic nie wychodzi");
            Check(result.Outcome!.Info!.Group is null, "brak grupy przeniesiony jako brak");
            // Komunikat musi UCZCIWIE mowic, ze skladu nie potwierdzamy.
            Check(result.Message == SonosGroupMembershipMessages.CreateAcceptedWithoutIdText,
                "wlasny komunikat dla przyjecia bez identyfikatora");
        }

        // 200 z obiektem grupy, ale identyfikatorem, ktorego nie da sie wstawic w
        // sciezke kolejnego polecenia - tez nie jest uzyteczny.
        using var unsafeId = Fixture.Connected(_ => GroupJson("zla/grupa", PlayerA));
        var withUnsafe = unsafeId.Create(MusicContext);
        Check(withUnsafe.Accepted && !withUnsafe.HasGroupId, "groupId ze slashem nie jest uzyteczny");
        Check(!withUnsafe.TryGetGroupId(out _), "niebezpieczny nie wychodzi");
    }

    private static void InvalidResponseGivesNoGroupId()
    {
        // Jeden przypadek na KAZDY rozny powod odrzucenia: nie JSON, zly korzen,
        // zly typ pola group, brak pola required w obiekcie grupy, zly typ
        // elementu playerIds, przekroczony limit, duplikat pola, zly UTF-16,
        // przekroczona glebokosc.
        foreach (var body in new[]
                 {
                     "to nie jest JSON",
                     "[]",
                     """{ "group": 42 }""",
                     """{ "group": { "name": "Salon", "coordinatorId": "A", "playerIds": ["A"] } }""",
                     """{ "group": { "id": "G", "name": "S", "coordinatorId": "A", "playerIds": [7] } }""",
                     """{ "group": { "id": "G", "name": "S", "coordinatorId": "A", "playerIds": ["ĄĆĘŁŃÓŚŹŻabcdefghijklmnop"] } }""",
                     """{ "group": { "id": "first" }, "group": { "id": "second" } }""",
                     """{ "group": { "id": "\uD800", "name": "S", "coordinatorId": "A", "playerIds": ["A"] } }""",
                     "{\"group\":{\"id\":\"G\",\"name\":\"S\",\"coordinatorId\":\"A\",\"playerIds\":[\"A\"],\"extra\":"
                         + new string('[', 20) + "0" + new string(']', 20) + "}}"
                 })
        {
            using var fixture = Fixture.Connected(_ => Json(body));
            var result = fixture.Create(MusicContext);
            Check(fixture.Requests == 1, "jeden POST nawet przy zlej odpowiedzi");
            Check(result.Outcome!.Status == SonosControlApiStatus.InvalidResponse, "status InvalidResponse");
            Check(!result.Accepted && !result.HasGroupId, "nic nie uznane");
            Check(!result.TryGetGroupId(out _), "zero identyfikatora");
        }
    }

    // ================= odmowa wejscia =================

    private static void InvalidInputNeverPosts()
    {
        using var fixture = Fixture.Connected(_ => GroupJson(Group, PlayerA));

        // createGroup: brak zadania i zly dom.
        Check(!fixture.CreateRaw(Household, null).RequestSent, "brak zadania: zero POST");
        Check(!fixture.CreateRaw("zly/dom", Request(MusicContext, PlayerA)).RequestSent, "zly dom: zero POST");
        Check(!fixture.CreateRaw(null, Request(MusicContext, PlayerA)).RequestSent, "brak domu: zero POST");

        // setGroupMembers: brak zestawu i zla grupa.
        Check(!fixture.SetRaw(Group, null).RequestSent, "brak zestawu: zero POST");
        Check(!fixture.SetRaw("zla/grupa", Set(PlayerA)).RequestSent, "zla grupa: zero POST");
        Check(!fixture.SetRaw(null, Set(PlayerA)).RequestSent, "brak grupy: zero POST");

        Check(fixture.Requests == 0, "LACZNIE zero zapytan dla calego zlego wejscia");
        Check(fixture.Refreshes == 0 && fixture.Deletes == 0, "zero odnowien i kasowania");

        // Odmowa wejscia ma status transportu InvalidConfiguration i Sent == false.
        var refused = fixture.SetRaw(null, Set(PlayerA));
        Check(refused.Outcome!.Status == SonosControlApiStatus.InvalidConfiguration, "InvalidConfiguration");
        Check(!refused.Outcome.Sent && !refused.Outcome.EffectAmbiguous, "nic nie poszlo, skutek NIE jest nieznany");

        // Brak konta: zero zapytan na obu operacjach.
        using var noAccount = Fixture.WithoutAccount(_ => GroupJson(Group, PlayerA));
        Check(noAccount.Create(MusicContext).Status == SonosGroupOperationStatus.NoAccount, "createGroup: brak konta");
        Check(noAccount.Set(PlayerA).Status == SonosGroupOperationStatus.NoAccount, "setGroupMembers: brak konta");
        Check(noAccount.Requests == 0, "brak konta: zero zapytan");
    }

    // ================= ochrona konta i brak ponowien =================

    private static void UnauthorizedNeverRetriesWrite()
    {
        // 401 na ZAPISIE: zadna z dwoch operacji nie odnawia dostepu po fakcie i
        // nie powtarza POST. Drugi POST przestawilby glosniki po raz drugi, juz
        // z innego stanu wyjsciowego.
        using var create = Fixture.Connected(_ => GroupJsonWithCode(Group, PlayerA, HttpStatusCode.Unauthorized));
        var createResult = create.Create(MusicContext);
        Check(create.Requests == 1, "createGroup: dokladnie jeden POST po 401");
        Check(create.Refreshes == 0, "createGroup: zero odnowien PO wyslaniu");
        Check(create.Deletes == 0, "createGroup: konto NIE kasowane");
        Check(createResult.Outcome!.Status == SonosControlApiStatus.Unauthorized, "status Unauthorized");
        Check(!createResult.Accepted && !createResult.HasGroupId, "zadnej grupy");
        // Cialo odpowiedzi BLEDU nie jest czytane, wiec zaden identyfikator z
        // niego nie wychodzi.
        Check(!createResult.TryGetGroupId(out _), "zero ID z odpowiedzi bledu");

        using var set = Fixture.Connected(_ => Json("{}", HttpStatusCode.Unauthorized));
        var setResult = set.Set(PlayerA);
        Check(set.Requests == 1, "setGroupMembers: dokladnie jeden POST po 401");
        Check(set.Refreshes == 0 && set.Deletes == 0, "setGroupMembers: zero odnowien i kasowania");
        Check(setResult.Outcome!.Status == SonosControlApiStatus.Unauthorized, "status Unauthorized");

        // Zerwane polaczenie: skutek NIEZNANY, zero ponowien - sklad grup moze
        // byc juz zmieniony.
        using var broken = Fixture.Connected(_ => throw new HttpRequestException("atrapa: zerwane polaczenie"));
        var brokenResult = broken.Set(PlayerA);
        Check(broken.Requests == 1, "zerwanie: jeden POST, zero ponowien");
        Check(brokenResult.Outcome!.Status == SonosControlApiStatus.Unreachable, "Unreachable");
        Check(brokenResult.EffectAmbiguous, "skutek NIEZNANY, nie 'skladu nie zmieniono'");
        Check(!brokenResult.TryGetGroupId(out _), "zero ID przy nieznanym skutku");
    }

    private static void AccountChangeNeverPublishesGroupId()
    {
        // Ta sama WSPOLNA granica generacji co sesja odtwarzania: nowe logowanie
        // w trakcie wstrzymanej odpowiedzi porzuca wynik, zeby groupId konta A
        // nie trafil do kontekstu konta B. Konto NIE jest przy tym kasowane.
        var gate = new Gate();
        using var fixture = Fixture.Connected(_ =>
        {
            gate.EnterAndWait();
            return GroupJson(Group, PlayerA, PlayerB);
        });

        var pending = Task.Run(() => fixture.Create(MusicContext));
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
        // NAJWAZNIEJSZE: identyfikator grupy konta A nie wychodzi do konta B.
        Check(!result.TryGetGroupId(out var leaked) && leaked is null, "groupId NIE opublikowany");
        Check(!result.Accepted && !result.HasGroupId, "nic nie uznane");
        // Zapis JUZ poszedl, wiec uczciwie: skutek NIEZNANY, a nie "cofnieto".
        Check(result.RequestSent && result.EffectAmbiguous, "proba i nieznany skutek, bez udawania cofniecia");
        Check(fixture.Requests == 1, "zero powtorzen po zmianie konta");
        Check(fixture.Deletes == 0, "konto NIE kasowane przy porzuceniu");
    }

    // ================= koordynator =================

    private static void CoordinatorExposesBothWrites()
    {
        // Dwie JAWNE, ROZDZIELONE metody - zadnej jednej metody "ustaw glosniki",
        // ktora po cichu wybieralaby miedzy tworzeniem i przestawianiem grupy.
        var create = typeof(SonosAccountCoordinator)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(candidate =>
                candidate.Name == "CreateGroupAsync"
                && candidate.ReturnType == typeof(Task<SonosGroupMembershipResult>));
        var set = typeof(SonosAccountCoordinator)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(candidate =>
                candidate.Name == "SetGroupMembersAsync"
                && candidate.ReturnType == typeof(Task<SonosGroupMembershipResult>));

        Check(create is not null && set is not null, "obie metody obecne");

        // ZERO wartosci domyslnych: dom, grupa i zestaw glosnikow musza byc
        // wybrane jawnie przez wolajacego.
        foreach (var parameter in create!.GetParameters().Concat(set!.GetParameters()))
        {
            Check(!parameter.HasDefaultValue, "brak wartosci domyslnej: " + parameter.Name);
        }

        // Waski kontrakt ma DOKLADNIE dwie metody - zadnego odczytu topologii.
        Check(typeof(ISonosGroupMembershipApi).GetMethods().Length == 2, "kontrakt ma dwie operacje");
        // PRAWDZIWY klient JUZ go implementuje - zadnej osobnej atrapy produkcyjnej.
        Check(typeof(ISonosGroupMembershipApi).IsAssignableFrom(typeof(SonosControlApiClient)),
            "klient implementuje kontrakt");

        // Null api to blad WOLAJACEGO, nie ciche zero.
        using var fixture = Fixture.Connected(_ => GroupJson(Group, PlayerA));
        var threwCreate = false;
        var threwSet = false;
        try
        {
            fixture.Coordinator.CreateGroupAsync(null!, Household, Request(MusicContext, PlayerA), CancellationToken.None)
                .WaitAsync(Deadline).GetAwaiter().GetResult();
        }
        catch (ArgumentNullException) { threwCreate = true; }

        try
        {
            fixture.Coordinator.SetGroupMembersAsync(null!, Group, Set(PlayerA), CancellationToken.None)
                .WaitAsync(Deadline).GetAwaiter().GetResult();
        }
        catch (ArgumentNullException) { threwSet = true; }

        Check(threwCreate && threwSet, "null api to ArgumentNullException");

        // Anulowanie PRZED wyslaniem: zadnego POST na obu operacjach.
        using var source = new CancellationTokenSource();
        source.Cancel();
        var canceledCreate = fixture.Coordinator
            .CreateGroupAsync(fixture.Api, Household, Request(MusicContext, PlayerA), source.Token)
            .WaitAsync(Deadline).GetAwaiter().GetResult();
        var canceledSet = fixture.Coordinator
            .SetGroupMembersAsync(fixture.Api, Group, Set(PlayerA), source.Token)
            .WaitAsync(Deadline).GetAwaiter().GetResult();
        Check(fixture.Requests == 0, "anulowanie przed wyslaniem: zero POST");
        Check(!canceledCreate.Accepted && !canceledSet.Accepted, "zadnej grupy");
        Check(!canceledCreate.TryGetGroupId(out _) && !canceledSet.TryGetGroupId(out _), "zero ID");
    }

    // ================= komunikaty =================

    private static void MessagesHideIdentifiers()
    {
        // Pelna tabela statusow transportu dla OBU operacji: kazdy ma wlasny,
        // niepusty tekst PL i zaden nie ujawnia identyfikatorow.
        foreach (var operation in new[]
                 {
                     SonosGroupMembershipOperation.CreateGroup,
                     SonosGroupMembershipOperation.SetGroupMembers
                 })
        {
            foreach (var status in Enum.GetValues<SonosControlApiStatus>())
            {
                var text = SonosGroupMembershipMessages.Describe(operation, status, sent: true,
                    effectAmbiguous: false, hasGroupId: true);
                Check(!string.IsNullOrWhiteSpace(text), "tekst dla " + operation + "/" + status);
                Check(!text.Contains(Group, StringComparison.Ordinal)
                    && !text.Contains(PlayerA, StringComparison.Ordinal)
                    && !text.Contains(Household, StringComparison.Ordinal),
                    "tekst bez identyfikatorow: " + status);
            }
        }

        // Sukces NIGDY nie oglasza, ze sklad JEST taki, jak zadano.
        Check(SonosGroupMembershipMessages.CreateAcceptedText.Contains("potwierdzi", StringComparison.Ordinal),
            "sukces mowi o potwierdzeniu dopiero przez odczyt");
        Check(SonosGroupMembershipMessages.SetAcceptedText.Contains("potwierdzi", StringComparison.Ordinal),
            "sukces setGroupMembers tez");

        // Nieznany skutek ma WLASNY tekst, inny niz zwykly blad.
        var ambiguous = SonosGroupMembershipMessages.Describe(
            SonosGroupMembershipOperation.SetGroupMembers, SonosControlApiStatus.Unreachable,
            sent: true, effectAmbiguous: true, hasGroupId: false);
        Check(ambiguous == SonosGroupMembershipMessages.SetAmbiguousText, "wlasny tekst nieznanego skutku");

        // ToString obu warstw bez groupId, bez glosnikow i bez domu.
        using var fixture = Fixture.Connected(_ => GroupJson(Group, PlayerA, PlayerB));
        var result = fixture.Create(MusicContext);
        foreach (var text in new[] { result.ToString(), result.Outcome!.ToString(), result.Message })
        {
            Check(!text.Contains(Group, StringComparison.Ordinal), "ToString bez groupId");
            Check(!text.Contains(PlayerA, StringComparison.Ordinal), "ToString bez playerId");
            Check(!text.Contains(Household, StringComparison.Ordinal), "ToString bez householdId");
            Check(!text.Contains(Fixture.Access, StringComparison.Ordinal), "ToString bez tokenu");
        }
    }

    // ================= aparatura =================

    private static SonosCreateGroupRequest Request(string? musicContextGroupId, params string[] playerIds)
    {
        Check(SonosCreateGroupRequest.TryCreate(playerIds, musicContextGroupId, out var request),
            "zadanie createGroup zbudowane");
        return request!;
    }

    private static SonosPlayerSet Set(params string[] playerIds)
    {
        Check(SonosPlayerSet.TryCreate(playerIds, out var set), "zestaw zbudowany");
        return set!;
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

    /// <summary>
    /// Odpowiedz groupInfo z obiektem group - ten SAM schemat co getGroups.
    /// Koordynatorem jest PIERWSZY glosnik listy, bo wg definicji playerIds
    /// "This list includes the coordinatorId" - osobny parametr koordynatora
    /// wczesniej zjadal pierwszy element listy czlonkow (blad APARATURY testu,
    /// nie produktu).
    /// </summary>
    private static HttpResponseMessage GroupJson(string groupId, params string[] members)
    {
        Check(members.Length > 0, "atrapa odpowiedzi potrzebuje co najmniej jednego glosnika");
        var list = string.Join(",", members.Select(id => JsonSerializer.Serialize(id)));
        return Json($"{{\"group\":{{\"id\":{JsonSerializer.Serialize(groupId)},\"name\":\"Salon\","
            + $"\"coordinatorId\":{JsonSerializer.Serialize(members[0])},\"playerIds\":[{list}]}}}}");
    }

    /// <summary>Ta sama tresc groupInfo, ale z WYBRANYM kodem HTTP (np. 401).</summary>
    private static HttpResponseMessage GroupJsonWithCode(string groupId, string player, HttpStatusCode code) =>
        Json($"{{\"group\":{{\"id\":{JsonSerializer.Serialize(groupId)},\"name\":\"Salon\","
            + $"\"coordinatorId\":{JsonSerializer.Serialize(player)},"
            + $"\"playerIds\":[{JsonSerializer.Serialize(player)}]}}}}", code);

    /// <summary>Co NAPRAWDE poszlo w zadaniu - odczytane z prawdziwego HttpRequestMessage.</summary>
    private sealed record Sent(
        string? Method, string? Token, string? Path, string? Body,
        IReadOnlyList<string>? PlayerIds, bool HasMusicContext, bool HasAreaIds)
    {
        internal static Sent From(HttpRequestMessage request)
        {
            string? body = null;
            List<string>? playerIds = null;
            var hasMusicContext = false;
            var hasAreaIds = false;
            if (request.Content is { } content)
            {
                body = content.ReadAsStringAsync().GetAwaiter().GetResult();
                using var document = JsonDocument.Parse(body);
                var root = document.RootElement;
                if (root.TryGetProperty("playerIds", out var ids) && ids.ValueKind == JsonValueKind.Array)
                {
                    playerIds = ids.EnumerateArray().Select(item => item.GetString()!).ToList();
                }

                hasMusicContext = root.TryGetProperty("musicContextGroupId", out _);
                hasAreaIds = root.TryGetProperty("areaIds", out _);
            }

            return new Sent(
                request.Method.Method,
                request.Headers.Authorization?.Parameter,
                request.RequestUri?.AbsolutePath,
                body, playerIds, hasMusicContext, hasAreaIds);
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
            // PRAWDZIWY klient Control API JUZ implementuje waski kontrakt skladu.
            Api = client;
        }

        internal SonosAccountCoordinator Coordinator { get; }

        internal ISonosGroupMembershipApi Api { get; }

        internal Gateway Gateway { get; }

        internal int Requests => handler.Count;

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

        internal SonosGroupMembershipResult Create(string? musicContextGroupId, params string[] playerIds) =>
            CreateRaw(Household, SonosGroupMembershipTests.Request(
                musicContextGroupId, playerIds.Length == 0 ? new[] { PlayerA, PlayerB } : playerIds));

        internal SonosGroupMembershipResult CreateRaw(string? householdId, SonosCreateGroupRequest? request) =>
            Coordinator.CreateGroupAsync(Api, householdId, request, CancellationToken.None)
                .WaitAsync(TimeSpan.FromSeconds(15)).GetAwaiter().GetResult();

        internal SonosGroupMembershipResult Set(params string[] playerIds) =>
            SetRaw(Group, SonosGroupMembershipTests.Set(playerIds.Length == 0 ? new[] { PlayerA } : playerIds));

        internal SonosGroupMembershipResult SetRaw(string? groupId, SonosPlayerSet? players) =>
            Coordinator.SetGroupMembersAsync(Api, groupId, players, CancellationToken.None)
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
