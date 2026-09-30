using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AccessibleMediaController.Core.Sonos;

/// <summary>
/// F3b: ZALADOWANIE ULUBIONEGO do wspolnej kolejki grupy przez ISTNIEJACEGO
/// koordynatora konta. Tylko Core: zero GUI, zero presetow, zero prawdziwego
/// konta, zero sieci - dane syntetyczne, atrapa bramki logowania, atrapa
/// magazynu i PRAWDZIWY <see cref="SonosControlApiClient"/> na sztucznym
/// HttpMessageHandler. Wywolanie idzie PRZEZ <see cref="ISonosFavoriteLoadApi"/>
/// do produkcyjnego klienta, wiec mierzony jest caly tor
/// koordynator -> klient -> HTTP.
///
/// Co jest mierzone PRAWDZIWYM kodem produkcyjnym:
///   * brak konta: ZERO POST i ZERO odnowien,
///   * aktualna i NIEZNANA waznosc: DOKLADNIE JEDEN POST wlasciwym tokenem, z
///     literalna grupa, literalnym favoriteId, literalna akcja i playOnCompletion
///     w ciele zadania,
///   * znana MINIONA waznosc: JEDNO odnowienie PRZED jedynym POST, nowym tokenem,
///   * 401/403/404/429/499/5xx i utracona odpowiedz: JEDEN POST, zero odnowien,
///     zero powtorzen, zero kasowania konta,
///   * HTTP 200 to PRZYJECIE, nie potwierdzony skutek; RequestSent mowi o PROBIE,
///   * niepoprawne wejscie (favoriteId, grupa, akcja): ZERO POST - bramke ma
///     klient, a wczesniejsze odnowienie jest dozwolone,
///   * anulowanie przed wejsciem: zero prob; po wejsciu: uczciwy nieznany skutek,
///   * Disconnect i Dispose w trakcie trwajacego POST: Discarded bez sukcesu i
///     bez obietnicy cofniecia,
///   * BARIERA GENERACJI przez PRAWDZIWE logowanie B (BeginLoginAsync +
///     CheckLoginAsync): stare A jest porzucone, nie odnawia i nie pyta biletem B,
///     a konto B zostaje zdrowe (sprawdzone NASTEPNYM publicznym wywolaniem),
///   * nowe konto w trakcie PRZEDPOSTOWEGO odnowienia: ZERO POST i tekst
///     mowiacy wprost o niewyslaniu,
///   * HTTP leci POZA blokada koordynatora (Snapshot z DRUGIEGO watku w trakcie),
///   * WSPOLDZIELONE odnowienie: dwa wywolania faktycznie osiagaja nieukonczony
///     wspolny await; anulowanie pierwszego nie konczy cudzego odnowienia -
///     pierwszy jest Canceled BEZ POST, drugi po zwolnieniu dostaje JEDNO
///     odnowienie i JEDEN POST.
///
/// Jak mierzone jest WSPOLDZIELONE odnowienie (bez zadnego szwu w produkcji):
///   * oba wywolania ida BEZPOSREDNIO na watku testu do publicznego
///     <see cref="SonosAccountCoordinator.LoadFavoriteAsync"/>; atrapa bramki
///     odnowienia awaituje PRAWDZIWIE, wiec metoda oddaje NIEUKONCZONY Task
///     dopiero po wykonaniu calej swojej czesci synchronicznej,
///   * dowodem wejscia w odnowienie jest wiec sam ZWROCONY Task
///     (<c>!IsCompleted</c>) razem z licznikiem odnowien bramki, nie uplyw czasu
///     i nie sygnal ustawiany obok wywolania,
///   * atrapa bramki odnowienia jest TOKEN-AWARE (czeka z tokenem, ktory dostala
///     od produkcji), wiec anulowanie wspolnego odnowienia byloby WIDOCZNE;
///     osobny, JAWNY wariant ignorujacy token sluzy wylacznie badaniu spoznionej
///     odpowiedzi przy zmianie generacji konta.
///
/// Sprzatanie APARATURY (nie zachowanie produkcji): kazdy test trzymajacy bramke
/// zwalnia w <c>finally</c> WSZYSTKIE bramki, a potem OGRANICZONYM czasem domyka
/// wystartowane zadania - takze wtedy, gdy asercja padla przed zwolnieniem.
/// Przekroczony limit nie jest cichym zaliczeniem, a pierwotna asercja nigdy nie
/// jest maskowana wyjatkiem sprzatania. Osobny przypadek
/// (<see cref="CleanupSurvivesFailedAssertion"/>) dowodzi tego na CELOWO
/// nieudanej asercji.
/// </summary>
internal static class SonosFavoriteLoadAccountTests
{
    private const string Key = "00000000-0000-0000-0000-000000000042";
    private const string Group = "RINCON_0001:1";
    private const string FavoriteId = "SYNTHETIC-FAV-42";
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(5);

    public static void Run()
    {
        var tests = new List<(string Name, Action Test)>
        {
            ("koordynator konta wystawia ladowanie ulubionego", CoordinatorExposesFavoriteLoad),
            ("brak konta: zero POST i zero odnowien", NoAccountSendsNothing),
            ("aktualny bilet: jeden POST z literalnym ciałem i grupą", CurrentTicketSendsExactlyOnePost),
            ("nieznana waznosc nie odnawia przed POST", UnknownExpiryDoesNotRenew),
            ("kazda akcja i playOnCompletion ida doslownie", EveryActionGoesLiterally),
            ("znana miniona waznosc: jedno odnowienie PRZED jednym POST", ExpiredRenewsOnceBeforeSinglePost),
            ("odmowy uslugi: jeden POST, zero odnowien i kasowania", RefusalsNeverRetryOrDelete),
            ("przyjecie nie jest potwierdzonym skutkiem", AcceptedIsNotEffect),
            ("niepoprawne wejscie nie wysyla POST", InvalidInputNeverPosts),
            ("anulowanie przed i po wejsciu nigdy nie udaje sukcesu", CancellationIsNeverSuccess),
            ("Disconnect w trakcie POST: porzucone bez obietnicy cofniecia", DiscardedDuringPostDoesNotPromiseUndo),
            ("Dispose w trakcie POST: porzucone", DiscardedAfterDispose),
            ("bariera: logowanie B w trakcie POST A porzuca A i nie tyka B", NewAccountDuringPostDiscardsOld),
            ("bariera: logowanie B w trakcie odnowienia A: zero POST", NewAccountDuringRenewalSendsNothing),
            ("HTTP poza blokada: Snapshot z drugiego watku w trakcie POST", SnapshotDuringPost),
            ("aparatura: sprzatanie domyka zadania takze po padnietej asercji", CleanupSurvivesFailedAssertion),
            ("wspoldzielone odnowienie: anulowanie jednego nie konczy cudzego", SharedRenewalSurvivesOneCancel),
            ("komunikaty i ToString bez tokenow i identyfikatorow", MessagesHideSecrets)
        };

        var failures = 0;
        foreach (var (name, test) in tests)
        {
            try { test(); Console.WriteLine("[OK] " + name); }
            catch (Exception ex) { failures++; Console.WriteLine("[FAIL] " + name + " (" + ex.GetType().Name + ": " + ex.Message + ")"); }
        }

        Console.WriteLine($"Ladowanie ulubionego Sonos przez konto: {tests.Count - failures}/{tests.Count}");
        if (failures != 0)
        {
            throw new InvalidOperationException(
                "Ladowanie ulubionego Sonos przez konto: " + failures + " nieudanych testow.");
        }
    }

    // ================= przypadki =================

    private static void CoordinatorExposesFavoriteLoad()
    {
        var method = typeof(SonosAccountCoordinator)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(candidate =>
                candidate.Name == "LoadFavoriteAsync"
                && candidate.ReturnType == typeof(Task<SonosGroupCommandResult>)
                && candidate.GetParameters().Select(p => p.ParameterType).SequenceEqual(
                    new[]
                    {
                        typeof(ISonosFavoriteLoadApi), typeof(string), typeof(string),
                        typeof(SonosFavoriteQueueAction), typeof(bool), typeof(CancellationToken)
                    }));

        Check(method is not null);
        // Akcja i playOnCompletion sa JAWNE: zadna z nich nie ma wartosci domyslnej.
        Check(method!.GetParameters().All(p => !p.HasDefaultValue));

        // Null api jest bledem WOLAJACEGO, nie cichym brakiem konta.
        using var fixture = Fixture.Connected(_ => Json("{}"));
        var threw = false;
        try
        {
            fixture.Coordinator
                .LoadFavoriteAsync(null!, Group, FavoriteId, SonosFavoriteQueueAction.Append, false, CancellationToken.None)
                .WaitAsync(Deadline).GetAwaiter().GetResult();
        }
        catch (ArgumentNullException) { threw = true; }

        Check(threw && fixture.Requests == 0);
    }

    private static void NoAccountSendsNothing()
    {
        using var fixture = Fixture.WithoutAccount(_ => Json("{}"));
        var result = fixture.Load();

        Check(result.Status == SonosGroupOperationStatus.NoAccount);
        Check(result.Command == SonosGroupCommand.LoadFavorite);
        Check(!result.RequestSent && !result.Accepted && !result.EffectConfirmed);
        Check(result.Outcome is null);
        Check(fixture.Requests == 0 && fixture.Refreshes == 0 && fixture.Deletes == 0);
    }

    private static void CurrentTicketSendsExactlyOnePost()
    {
        var seen = new List<Sent>();
        using var fixture = Fixture.Connected(request => { seen.Add(Sent.From(request)); return Json("{}"); });

        var result = fixture.Load(action: SonosFavoriteQueueAction.Replace, playOnCompletion: true);

        Check(result.Status == SonosGroupOperationStatus.Attempted && result.RequestSent && result.Accepted);
        Check(result.Command == SonosGroupCommand.LoadFavorite);
        Check(fixture.Requests == 1 && fixture.Refreshes == 0 && !result.Renewed);

        // DOKLADNIE jeden POST, wlasciwym biletem, na sciezke grupy z zapytania.
        Check(seen.Count == 1);
        Check(seen[0].Method == "POST" && seen[0].Token == Fixture.Access);
        // Dwukropek jest legalnym znakiem segmentu sciezki i zostaje LITERALNIE.
        Check(seen[0].Path!.EndsWith("/groups/" + Group + "/favorites", StringComparison.Ordinal), "sciezka grupy literalna");

        // Cialo niesie DOKLADNIE to, co podal wolajacy.
        Check(seen[0].Favorite == FavoriteId);
        Check(seen[0].Action == "REPLACE");
        Check(seen[0].PlayOnCompletion == true);
    }

    private static void UnknownExpiryDoesNotRenew()
    {
        var seen = new List<Sent>();
        using var fixture = Fixture.Connected(
            request => { seen.Add(Sent.From(request)); return Json("{}"); },
            expiresInSeconds: null);

        var result = fixture.Load();

        Check(result.Accepted && !result.Renewed);
        Check(fixture.Refreshes == 0 && fixture.Requests == 1);
        Check(seen.Count == 1 && seen[0].Token == Fixture.Access);
        Check(!result.Snapshot.IsExpiryKnown);
    }

    private static void EveryActionGoesLiterally()
    {
        var expected = new (SonosFavoriteQueueAction Action, string Wire)[]
        {
            (SonosFavoriteQueueAction.Replace, "REPLACE"),
            (SonosFavoriteQueueAction.Append, "APPEND"),
            (SonosFavoriteQueueAction.Insert, "INSERT"),
            (SonosFavoriteQueueAction.InsertNext, "INSERT_NEXT"),
            (SonosFavoriteQueueAction.PlayNow, "PLAY_NOW")
        };

        foreach (var (action, wire) in expected)
        {
            foreach (var play in new[] { false, true })
            {
                var seen = new List<Sent>();
                using var fixture = Fixture.Connected(request => { seen.Add(Sent.From(request)); return Json("{}"); });
                var result = fixture.Load(action: action, playOnCompletion: play);

                Check(result.Accepted && fixture.Requests == 1);
                Check(seen.Count == 1 && seen[0].Action == wire && seen[0].PlayOnCompletion == play);
                // Transport NIE dokleja playModes: brak pola zachowuje tryby gloshnika.
                Check(seen[0].HasPlayModes == false);
            }
        }
    }

    private static void ExpiredRenewsOnceBeforeSinglePost()
    {
        var seen = new List<Sent>();
        using var fixture = Fixture.Connected(
            request => { seen.Add(Sent.From(request)); return Json("{}"); },
            expiresInSeconds: 1,
            receivedShift: TimeSpan.FromMinutes(-10));

        var result = fixture.Load(action: SonosFavoriteQueueAction.InsertNext, playOnCompletion: true);

        Check(result.Accepted && result.Renewed);
        Check(fixture.Refreshes == 1 && fixture.Requests == 1);
        // POST poszedl JUZ ODNOWIONYM biletem, nie starym.
        Check(seen.Count == 1 && seen[0].Token == Fixture.Renewed);
        Check(seen[0].Action == "INSERT_NEXT" && seen[0].PlayOnCompletion == true);
    }

    private static void RefusalsNeverRetryOrDelete()
    {
        var codes = new[]
        {
            HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.NotFound,
            (HttpStatusCode)429, (HttpStatusCode)499, HttpStatusCode.InternalServerError,
            HttpStatusCode.ServiceUnavailable
        };

        foreach (var code in codes)
        {
            using var fixture = Fixture.Connected(_ => Json("{\"globalError\":{\"reason\":\"SYNTHETIC-REASON\"}}", code));
            var result = fixture.Load();

            Check(result.Status == SonosGroupOperationStatus.Attempted && result.RequestSent);
            Check(!result.Accepted && !result.EffectConfirmed);
            // JEDEN POST. Zero odnowien, zero powtorzen, zero kasowania konta.
            Check(fixture.Requests == 1 && fixture.Refreshes == 0 && fixture.Deletes == 0);
            Check(result.Snapshot.HasCredentials);
            Check(!result.Message.Contains("SYNTHETIC-REASON", StringComparison.Ordinal));
        }

        // UTRACONA odpowiedz: polecenie moglo sie wykonac, wiec ani go nie
        // ponawiamy, ani nie oglaszamy, ze nie doszlo.
        using var lost = Fixture.Connected(_ => throw new HttpRequestException("brak odpowiedzi", new IOException()));
        var lostResult = lost.Load();
        Check(lost.Requests == 1 && lost.Refreshes == 0 && lost.Deletes == 0);
        Check(lostResult.Status == SonosGroupOperationStatus.Attempted && lostResult.RequestSent);
        Check(!lostResult.Accepted && lostResult.EffectAmbiguous && !lostResult.EffectConfirmed);
    }

    private static void AcceptedIsNotEffect()
    {
        using var fixture = Fixture.Connected(_ => Json("{}"));
        var result = fixture.Load();

        // HTTP 200 to PRZYJECIE zlecenia, nie dowod, ze kolejka sie zmienila.
        Check(result.Accepted && !result.EffectConfirmed && !result.EffectAmbiguous);
        Check(result.Outcome!.Command == SonosGroupCommand.LoadFavorite && result.Outcome.Sent);
        Check(result.StateReadRecommended);
        Check(result.Message.Contains("niepotwierdzone", StringComparison.Ordinal));
        Check(result.ToString().Contains("podjęto próbę wysłania", StringComparison.Ordinal));
    }

    private static void InvalidInputNeverPosts()
    {
        // (a) puste favoriteId - bramke ma ODEBRANY klient, przed HTTP.
        using var empty = Fixture.Connected(_ => Json("{}"));
        var emptyResult = empty.Load(favoriteId: string.Empty);
        Check(emptyResult.Status == SonosGroupOperationStatus.Attempted);
        Check(emptyResult.Outcome!.Status == SonosControlApiStatus.InvalidConfiguration);
        Check(!emptyResult.Accepted && empty.Requests == 0);

        // (b) grupa z ukosnikami nie moze podmienic sciezki.
        using var group = Fixture.Connected(_ => Json("{}"));
        var groupResult = group.Load(groupId: "grupa/z/ukosnikami");
        Check(groupResult.Outcome!.Status == SonosControlApiStatus.InvalidConfiguration);
        Check(!groupResult.Accepted && group.Requests == 0);

        // (c) akcja spoza definicji (legalne rzutowanie w C#).
        using var action = Fixture.Connected(_ => Json("{}"));
        var actionResult = action.Load(action: (SonosFavoriteQueueAction)777);
        Check(actionResult.Outcome!.Status == SonosControlApiStatus.InvalidConfiguration);
        Check(!actionResult.Accepted && action.Requests == 0);
        Check(actionResult.Snapshot.HasCredentials && action.Deletes == 0);

        // (d) NIEPOPRAWNE wejscie przy MINIONEJ waznosci: koordynator nie wie z
        // gory, co klient odrzuci, wiec wczesniejsze JEDNO odnowienie jest
        // dozwolone. Twarda regula dotyczy POST: jego nie ma.
        using var expired = Fixture.Connected(
            _ => Json("{}"),
            expiresInSeconds: 1,
            receivedShift: TimeSpan.FromMinutes(-10));
        var expiredResult = expired.Load(favoriteId: string.Empty);
        Check(expired.Requests == 0 && expired.Deletes == 0);
        Check(expiredResult.Outcome!.Status == SonosControlApiStatus.InvalidConfiguration);
    }

    private static void CancellationIsNeverSuccess()
    {
        // (a) anulowanie PRZED wejsciem: ZERO prob wyslania. Bramke anulowania na
        // tym etapie ma ODEBRANY klient (WriteAsync sprawdza token wolajacego
        // przed zbudowaniem zadania), wiec konto widzi zwykly wynik transportu
        // Canceled z Sent=false - i to jest uczciwe: zadnego POST nie bylo.
        using var beforeSource = new CancellationTokenSource();
        beforeSource.Cancel();
        using var before = Fixture.Connected(_ => Json("{}"));
        var beforeResult = before.Load(cancellationToken: beforeSource.Token);
        Check(before.Requests == 0, "anulowanie przed wejsciem: zero zapytan HTTP");
        Check(!beforeResult.RequestSent, "anulowanie przed wejsciem: zero prob wyslania");
        Check(!beforeResult.Accepted && !beforeResult.EffectConfirmed, "anulowanie nie jest sukcesem");
        Check(!beforeResult.EffectAmbiguous, "brak proby to brak nieznanego skutku");
        Check(beforeResult.Outcome!.Status == SonosControlApiStatus.Canceled, "status transportu Canceled");
        Check(before.Refreshes == 0 && before.Deletes == 0, "anulowanie nie odnawia i nie kasuje konta");

        // (b) anulowanie PO wejsciu w HTTP: proba byla, skutek NIEZNANY.
        WithApparatus(apparatus =>
        {
            var gate = apparatus.NewGate();
            var during = apparatus.Own(new CancellationTokenSource());
            // Handler blokuje WATEK, wiec to wywolanie musi isc obok watku testu;
            // referencja zadania zyje w aparaturze, nie w ciele try.
            var fixture = apparatus.Own(Fixture.Connected(_ =>
            {
                gate.EnterAndWait();
                throw new OperationCanceledException();
            }));

            var task = apparatus.Track(Task.Run(() => fixture.Load(cancellationToken: during.Token)));
            Check(gate.WaitEntered(), "handler wszedl w POST");
            during.Cancel();
            gate.Release();

            var result = task.WaitAsync(Deadline).GetAwaiter().GetResult();
            // DOKLADNIE Attempted, nie "Canceled albo cokolwiek". Wg kontraktu
            // Canceled znaczy "anulowano PRZED wyslaniem, nic nie poszlo", a tu
            // zadanie JUZ weszlo w transport - wiec skutek jest NIEZNANY.
            Check(result.Status == SonosGroupOperationStatus.Attempted, "status konta Attempted, jest " + result.Status);
            Check(!result.Accepted && !result.EffectConfirmed, "anulowanie po wejsciu nie jest sukcesem");
            Check(result.RequestSent, "proba wyslania odnotowana");
            Check(result.EffectAmbiguous, "skutek po anulowaniu w locie NIEZNANY");
            Check(result.Outcome!.Status == SonosControlApiStatus.Canceled, "transport Canceled");
            // Transport BYL wolany, wiec zaden tekst nie obiecuje, ze nic sie nie stalo.
            Check(!result.Message.Contains("nie zostało wysłane", StringComparison.Ordinal), "brak obietnicy niewyslania");
            Check(fixture.Requests == 1, "dokladnie jedno zapytanie HTTP");
            Check(fixture.Refreshes == 0 && fixture.Deletes == 0, "anulowanie nie odnawia i nie kasuje konta");
        });
    }

    private static void DiscardedDuringPostDoesNotPromiseUndo()
    {
        WithApparatus(apparatus =>
        {
            var gate = apparatus.NewGate();
            var fixture = apparatus.Own(Fixture.Connected(_ => { gate.EnterAndWait(); return Json("{}"); }));

            var task = apparatus.Track(Task.Run(() => fixture.Load()));
            Check(gate.WaitEntered());
            fixture.Coordinator.Disconnect();
            gate.Release();

            var result = task.WaitAsync(Deadline).GetAwaiter().GetResult();
            Check(result.Status == SonosGroupOperationStatus.Discarded);
            // Zadanie JUZ poszlo: proba i NIEZNANY skutek, nigdy cofniecie.
            Check(result.RequestSent && !result.Accepted && !result.EffectConfirmed);
            Check(result.EffectAmbiguous);
            Check(result.Message.Contains("nieznany", StringComparison.Ordinal));
            Check(!result.Message.Contains("cofni", StringComparison.Ordinal));
            Check(!result.Message.Contains("nie zostało wysłane", StringComparison.Ordinal));
            Check(result.Message.Contains("próbie wysłania", StringComparison.Ordinal));
            Check(fixture.Requests == 1);
        });
    }

    private static void DiscardedAfterDispose()
    {
        WithApparatus(apparatus =>
        {
            var gate = apparatus.NewGate();
            var fixture = Fixture.Connected(_ => { gate.EnterAndWait(); return Json("{}"); });
            // Koordynator jest zwalniany W TRAKCIE testu, wiec sprzatanie dotyka
            // TYLKO transportu - i dopiero PO domknieciu zadania.
            apparatus.Then(fixture.DisposeTransportOnly);

            var task = apparatus.Track(Task.Run(() => fixture.Load()));
            Check(gate.WaitEntered());
            fixture.Coordinator.Dispose();
            gate.Release();

            var result = task.WaitAsync(Deadline).GetAwaiter().GetResult();
            Check(result.Status == SonosGroupOperationStatus.Discarded);
            Check(result.RequestSent && !result.Accepted && !result.EffectConfirmed);
            Check(fixture.Requests == 1);
        });
    }

    private static void NewAccountDuringPostDiscardsOld()
    {
        var gate = new Gate();
        var seen = new List<Sent>();
        using var fixture = Fixture.Connected(request =>
        {
            seen.Add(Sent.From(request));
            gate.EnterAndWait();
            // Stare A dostaje nawet 401 - i to NIE moze dotknac konta B.
            return Json("{}", HttpStatusCode.Unauthorized);
        });
        fixture.Gateway.AllowLogin(Fixture.AccessB);

        // Fixture zyje DLUZEJ niz czesc z bramka: konto B sprawdzamy nastepnym
        // publicznym wywolaniem, wiec aparatura dozoruje tylko bramke i zadanie.
        WithApparatus(apparatus =>
        {
            apparatus.Adopt(gate);
            var task = apparatus.Track(Task.Run(() => fixture.Load()));
            Check(gate.WaitEntered());

            // PRAWDZIWE nowe logowanie przez istniejacy tor publiczny.
            fixture.LogInAsNewAccount();
            var refreshesAfterLogin = fixture.Refreshes;
            var writesAfterLogin = fixture.Writes;
            var deletesAfterLogin = fixture.Deletes;
            gate.Release();

            var result = task.WaitAsync(Deadline).GetAwaiter().GetResult();
            Check(result.Status == SonosGroupOperationStatus.Discarded);
            // Proba ZOSTAJE zachowana: skutek na koncie A jest nieznany.
            Check(result.RequestSent && !result.Accepted && result.EffectAmbiguous);
            Check(result.Outcome is null);
            // Stare A nie odnowilo sie, nie nadpisalo i nie skasowalo konta B.
            Check(fixture.Refreshes == refreshesAfterLogin);
            Check(fixture.Writes == writesAfterLogin && fixture.Deletes == deletesAfterLogin);
            Check(fixture.StoredAccessToken == Fixture.AccessB);
            // Jeden POST, TYLKO biletem A.
            Check(fixture.Requests == 1 && seen.Count == 1 && seen[0].Token == Fixture.Access);
        });

        // Konto B jest ZDROWE: sprawdzamy to NASTEPNYM publicznym wywolaniem.
        fixture.Reply = _ => Json("{}");
        var after = fixture.Load(action: SonosFavoriteQueueAction.Append, playOnCompletion: false);
        Check(after.Status == SonosGroupOperationStatus.Attempted && after.Accepted);
        Check(fixture.Requests == 2 && fixture.LastToken == Fixture.AccessB);
    }

    private static void NewAccountDuringRenewalSendsNothing()
    {
        WithApparatus(apparatus =>
        {
            var refreshGate = apparatus.NewGate();
            var fixture = apparatus.Own(Fixture.Connected(
                _ => Json("{}"),
                expiresInSeconds: 1,
                receivedShift: TimeSpan.FromMinutes(-10)));
            fixture.Gateway.AllowLogin(Fixture.AccessB);
            // Tu badana jest SPOZNIONA odpowiedz po zmianie generacji konta, nie
            // anulowanie - wiec atrapa CELOWO i JAWNIE ignoruje token.
            fixture.Gateway.HoldRefreshIgnoringToken(refreshGate);

            var task = apparatus.Track(Task.Run(() => fixture.Load()));
            Check(refreshGate.WaitEntered());
            fixture.LogInAsNewAccount();
            refreshGate.Release();

            var result = task.WaitAsync(Deadline).GetAwaiter().GetResult();
            Check(result.Status == SonosGroupOperationStatus.Discarded);
            // ZERO POST - ani starym, ani nowym biletem. Tekst mowi to WPROST.
            Check(!result.RequestSent && fixture.Requests == 0);
            Check(!result.EffectAmbiguous);
            Check(result.Message.Contains("nie zostało wysłane", StringComparison.Ordinal));
            Check(!result.Message.Contains("nieznany", StringComparison.Ordinal));
            Check(result.ToString().Contains("żądania nie wysłano", StringComparison.Ordinal));
            Check(fixture.StoredAccessToken == Fixture.AccessB && fixture.Deletes == 0);
        });
    }

    private static void SnapshotDuringPost()
    {
        WithApparatus(apparatus =>
        {
            var gate = apparatus.NewGate();
            var generation = -1L;
            var fixture = apparatus.Own(Fixture.Connected(_ => { gate.EnterAndWait(); return Json("{}"); }));

            var task = apparatus.Track(Task.Run(() => fixture.Load()));
            Check(gate.WaitEntered());

            // Gdyby HTTP bieglo pod blokada koordynatora, ten odczyt z DRUGIEGO
            // watku nie wrocilby przed zwolnieniem bramki. Sonda tez idzie pod
            // dozor: jej zawieszenie nie moze zostac po tescie.
            var probe = apparatus.Track(
                Task.Run(() => generation = fixture.Coordinator.Snapshot.CredentialGeneration));
            Check(probe.Wait(TimeSpan.FromSeconds(3)));
            Check(generation >= 0);

            gate.Release();
            var result = task.WaitAsync(Deadline).GetAwaiter().GetResult();
            Check(result.Accepted);
        });
    }

    /// <summary>
    /// APARATURA, nie produkcja: dowod, ze sprzatanie dziala takze wtedy, gdy
    /// asercja pada PRZED zwolnieniem bramki. Dwa zadania wisza na bramce, cialo
    /// testu przerywa CELOWO nieudana asercja - a sprzatanie i tak zwalnia bramke
    /// i domyka OBA zadania. Pierwotna asercja nie jest maskowana.
    /// </summary>
    private static void CleanupSurvivesFailedAssertion()
    {
        const string Label = "CELOWO nieudana asercja aparatury";
        Task<SonosGroupCommandResult>? first = null;
        Task? probe = null;

        var apparatus = RunGuarded(
            inner =>
            {
                var gate = inner.NewGate();
                var fixture = inner.Own(Fixture.Connected(_ => { gate.EnterAndWait(); return Json("{}"); }));

                first = inner.Track(Task.Run(() => fixture.Load()));
                Check(gate.WaitEntered(), "handler wszedl w POST");
                probe = inner.Track(Task.Run(() => gate.EnterAndWait()));

                // OBA zadania wisza na bramce. Tu test sie wywala.
                Check(false, Label);
            },
            out var primary);

        // Pierwotna asercja WYGRALA, dokladnie ta z etykieta - zaden wyjatek
        // sprzatania jej nie podmienil.
        Check(primary is InvalidOperationException, "pierwotny wyjatek zachowany");
        Check(primary!.Message.Contains(Label, StringComparison.Ordinal), "pierwotna etykieta zachowana");

        // Sprzatanie i tak zwolnilo bramke i domknelo OBA wiszace zadania.
        Check(apparatus.Released, "bramki zwolnione po padnietej asercji");
        Check(apparatus.TrackedTasks == 2, "oba zadania byly pod dozorem");
        Check(apparatus.AllTasksSettled, "oba zadania stanely");
        Check(apparatus.CleanupFailure is null, "sprzatanie bez timeoutu");

        // Nic nie zostalo w tle: stan zadan jest KONCOWY, nie "jeszcze biegnie".
        Check(first is { IsCompleted: true }, "zadanie POST zakonczone");
        Check(probe is { IsCompleted: true }, "zadanie sondy zakonczone");
        Check(first!.Status == TaskStatus.RanToCompletion && first.Result.Accepted, "POST domkniety zwyczajnie");
    }

    private static void SharedRenewalSurvivesOneCancel()
    {
        WithApparatus(apparatus =>
        {
            var refreshGate = apparatus.NewGate();
            var seen = new List<Sent>();
            var canceled = apparatus.Own(new CancellationTokenSource());
            var fixture = apparatus.Own(Fixture.Connected(
                request => { seen.Add(Sent.From(request)); return Json("{}"); },
                expiresInSeconds: 1,
                receivedShift: TimeSpan.FromMinutes(-10)));
            // Atrapa odnowienia czeka TOKENEM, ktory dostala od produkcji.
            fixture.Gateway.HoldRefresh(refreshGate);

            // PIERWSZY: wywolanie idzie BEZPOSREDNIO na watku testu do publicznej
            // metody koordynatora. Ze sterowanie wrocilo z NIEUKONCZONYM Taskiem
            // wynika, ze wolajacy wykonal cala swoja czesc synchroniczna i siedzi
            // w PRAWDZIWYM awaicie wspolnego odnowienia.
            var first = apparatus.Track(fixture.LoadAsync(cancellationToken: canceled.Token));
            Check(!first.IsCompleted, "pierwszy wrocil NIEUKONCZONY z metody produkcyjnej");
            Check(refreshGate.WaitEntered(), "pierwszy dotarl do odnowienia w bramce");
            Check(fixture.Refreshes == 1, "jedno zapytanie odnowienia");
            // Token wspolnego odnowienia DA SIE anulowac, wiec brak anulowania
            // nizej jest pomiarem, nie skutkiem tokenu-atrapy.
            Check(fixture.Gateway.SharedRefreshTokenCanBeCanceled, "token wspolnego odnowienia anulowalny");

            // DRUGI: TAKIE SAMO bezposrednie wywolanie na watku testu. Znow liczy
            // sie sam ZWROCONY Task: nieukonczony PO POWROCIE z metody dowodzi,
            // ze drugi wolajacy DOSZEDL do wspolnego awaitu. Licznik bramki
            // pokazuje, ze DOLACZYL do tego samego odnowienia, a nie zaczal drugie.
            var second = apparatus.Track(fixture.LoadAsync());
            Check(!second.IsCompleted, "drugi wrocil NIEUKONCZONY z metody produkcyjnej");
            Check(fixture.Refreshes == 1, "drugi DOLACZYL: nadal jedno zapytanie odnowienia");
            Check(fixture.Requests == 0, "przed zwolnieniem zero POST");

            canceled.Cancel();

            // PIERWSZY: anulowany PRZED wyslaniem - zadnego POST.
            var firstResult = first.WaitAsync(Deadline).GetAwaiter().GetResult();
            Check(firstResult.Status == SonosGroupOperationStatus.Canceled, "pierwszy DOKLADNIE Canceled");
            Check(!firstResult.RequestSent && !firstResult.Accepted, "pierwszy bez proby wyslania");
            Check(fixture.Requests == 0, "anulowanie pierwszego nie wyslalo POST");

            // Wspolne odnowienie NIE zostalo anulowane wraz z pierwszym: token
            // jest czysty, a DRUGI nadal wisi w tym samym, nieukonczonym awaicie.
            Check(!fixture.Gateway.RefreshCanceled, "token wspolnego odnowienia NIE anulowany");
            Check(!second.IsCompleted, "drugi nadal czeka po anulowaniu pierwszego");
            Check(fixture.Refreshes == 1 && fixture.Requests == 0, "nadal jedno odnowienie i zero POST");

            refreshGate.Release();
            var secondResult = second.WaitAsync(Deadline).GetAwaiter().GetResult();

            // DRUGI: JEDNO wspolne odnowienie i DOKLADNIE JEDEN POST nowym biletem.
            Check(secondResult.Accepted && secondResult.Renewed, "drugi przyjety po wspolnym odnowieniu");
            Check(fixture.Refreshes == 1 && fixture.Requests == 1, "jedno odnowienie, jeden POST");
            Check(seen.Count == 1 && seen[0].Token == Fixture.Renewed, "POST poszedl NOWYM biletem");
            Check(fixture.Coordinator.Snapshot.HasCredentials, "konto zostalo zdrowe");
        });
    }

    private static void MessagesHideSecrets()
    {
        using var fixture = Fixture.Connected(_ => Json("{}"));
        var success = fixture.Load();
        foreach (var text in new[] { success.Message, success.ToString(), success.Snapshot.ToString()! })
        {
            Check(!text.Contains(Fixture.Access, StringComparison.Ordinal));
            Check(!text.Contains(Fixture.Renewed, StringComparison.Ordinal));
            Check(!text.Contains(Key, StringComparison.Ordinal));
            Check(!text.Contains(FavoriteId, StringComparison.Ordinal));
            Check(!text.Contains(Group, StringComparison.Ordinal));
        }

        using var refused = Fixture.Connected(_ => Json("{\"globalError\":{\"reason\":\"SYNTHETIC-REASON\"}}", HttpStatusCode.Forbidden));
        var refusedResult = refused.Load();
        Check(!refusedResult.Message.Contains("SYNTHETIC-REASON", StringComparison.Ordinal));
        Check(!refusedResult.ToString().Contains("SYNTHETIC-REASON", StringComparison.Ordinal));
        Check(!refusedResult.ToString().Contains(FavoriteId, StringComparison.Ordinal));
    }

    // ================= aparatura =================

    /// <summary>
    /// Asercja z ETYKIETA: log wskazuje, ktory warunek padl, a nie tylko ktory
    /// test. Etykiety sa opisami regul, nigdy danymi - zaden token, klucz ani
    /// identyfikator do nich nie trafia.
    /// </summary>
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
    /// APARATURA testu trzymajacego bramki: rejestr bramek, wystartowanych zadan
    /// i sprzatania. Referencje zyja POZA cialem testu, wiec padnieta asercja w
    /// srodku nie gubi zadania wiszacego na bramce.
    ///
    /// To WYLACZNIE obsluga aparatury testowej - zadnego zachowania produkcji tu
    /// nie ma i nie jest ono tu mierzone.
    /// </summary>
    private sealed class Apparatus
    {
        private static readonly TimeSpan DrainDeadline = TimeSpan.FromSeconds(10);

        private readonly List<Gate> gates = new();
        private readonly List<Task> tasks = new();
        private readonly List<Action> cleanups = new();

        /// <summary>Bramki zostaly zwolnione (nawet po padnietej asercji).</summary>
        internal bool Released { get; private set; }

        /// <summary>Liczba zadan, ktore test wystartowal i oddal pod dozor.</summary>
        internal int TrackedTasks => tasks.Count;

        /// <summary>WSZYSTKIE dozorowane zadania STANELY - wynikiem, bledem albo anulowaniem.</summary>
        internal bool AllTasksSettled { get; private set; }

        /// <summary>Blad SAMEGO sprzatania; nigdy nie zastepuje pierwotnej asercji.</summary>
        internal Exception? CleanupFailure { get; private set; }

        internal Gate NewGate()
        {
            var gate = new Gate();
            gates.Add(gate);
            return gate;
        }

        /// <summary>Bierze pod dozor bramke utworzona poza aparatura.</summary>
        internal Gate Adopt(Gate gate)
        {
            gates.Add(gate);
            return gate;
        }

        /// <summary>Oddaje zadanie pod dozor: zostanie domkniete po zwolnieniu bramek.</summary>
        internal T Track<T>(T task)
            where T : Task
        {
            tasks.Add(task);
            return task;
        }

        /// <summary>Aparatura do zwolnienia PO domknieciu zadan, nie przed.</summary>
        internal T Own<T>(T disposable)
            where T : IDisposable
        {
            cleanups.Add(disposable.Dispose);
            return disposable;
        }

        internal void Then(Action cleanup) => cleanups.Add(cleanup);

        /// <summary>
        /// Zwalnia WSZYSTKIE bramki, potem OGRANICZONYM czasem domyka zadania i
        /// tylko na koniec zwalnia aparature. Przekroczony limit jest BLEDEM
        /// zapisanym w <see cref="CleanupFailure"/>, nie cichym przejsciem.
        /// Wyjatki samych zadan sa OBSERWOWANE (zadnych nieodczytanych bledow),
        /// ale nie staja sie wynikiem testu.
        /// </summary>
        internal void ReleaseAndDrain()
        {
            try
            {
                foreach (var gate in gates)
                {
                    gate.Release();
                }

                Released = true;

                var stuck = 0;
                foreach (var task in tasks)
                {
                    bool settled;
                    try { settled = task.Wait(DrainDeadline); }
                    catch (Exception) { settled = task.IsCompleted; }

                    if (!settled)
                    {
                        stuck++;
                    }
                }

                AllTasksSettled = stuck == 0;
                if (stuck != 0)
                {
                    throw new TimeoutException(
                        "Sprzatanie aparatury: " + stuck + " zadan testu nie stanelo po zwolnieniu bramek.");
                }
            }
            catch (Exception exception)
            {
                CleanupFailure = exception;
            }

            for (var index = cleanups.Count - 1; index >= 0; index--)
            {
                try { cleanups[index](); }
                catch (Exception exception) { CleanupFailure ??= exception; }
            }
        }
    }

    /// <summary>
    /// Uruchamia cialo testu i ZAWSZE sprzata aparature. Zwraca aparature oraz
    /// pierwotny wyjatek ciala BEZ rzucania, zeby dalo sie zmierzyc samo
    /// sprzatanie przy CELOWO nieudanej asercji.
    /// </summary>
    private static Apparatus RunGuarded(Action<Apparatus> body, out Exception? primary)
    {
        var apparatus = new Apparatus();
        primary = null;
        try { body(apparatus); }
        catch (Exception exception) { primary = exception; }

        apparatus.ReleaseAndDrain();
        return apparatus;
    }

    /// <summary>
    /// Zwykle uruchomienie testu z bramkami: pierwotna asercja WYGRYWA i nie jest
    /// maskowana bledem sprzatania, a gdy cialo przeszlo - nieudane sprzatanie
    /// (np. wiszace zadanie) jest bledem testu.
    /// </summary>
    private static void WithApparatus(Action<Apparatus> body)
    {
        var apparatus = RunGuarded(body, out var primary);
        if (primary is not null)
        {
            ExceptionDispatchInfo.Throw(primary);
        }

        if (apparatus.CleanupFailure is { } failure)
        {
            throw new InvalidOperationException(
                "Sprzatanie aparatury testu: " + failure.Message, failure);
        }
    }

    /// <summary>Co NAPRAWDE poszlo w zadaniu - odczytane z prawdziwego HttpRequestMessage.</summary>
    private sealed record Sent(
        string? Method, string? Token, string? Path,
        string? Favorite, string? Action, bool? PlayOnCompletion, bool HasPlayModes)
    {
        internal static Sent From(HttpRequestMessage request)
        {
            string? favorite = null;
            string? action = null;
            bool? play = null;
            var hasPlayModes = false;
            if (request.Content is { } content)
            {
                var body = content.ReadAsStringAsync().GetAwaiter().GetResult();
                using var document = JsonDocument.Parse(body);
                var root = document.RootElement;
                favorite = root.TryGetProperty("favoriteId", out var id) ? id.GetString() : null;
                action = root.TryGetProperty("action", out var value) ? value.GetString() : null;
                play = root.TryGetProperty("playOnCompletion", out var flag) ? flag.GetBoolean() : null;
                hasPlayModes = root.TryGetProperty("playModes", out _);
            }

            return new Sent(
                request.Method.Method,
                request.Headers.Authorization?.Parameter,
                request.RequestUri?.AbsolutePath,
                favorite, action, play, hasPlayModes);
        }
    }

    /// <summary>
    /// Bramka zdarzeniowa na TaskCompletionSource z RunContinuationsAsynchronously:
    /// wznowienie NIE biegnie na watku zwalniajacym. Kazde czekanie ma TWARDY
    /// limit, a jego niedotrzymanie jest BLEDEM testu, nie cichym przejsciem -
    /// inaczej niz w starszym <c>Gate.EnterAndWait</c>, ktory ignorowal wynik.
    /// </summary>
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

        /// <summary>
        /// Czekanie ASYNCHRONICZNE i TOKEN-AWARE. Blokada synchroniczna w atrapie
        /// bramki trzyma WATEK wolajacego jeszcze PRZED pierwszym awaitem
        /// odnowienia, wiec wolajacy nigdy nie dociera do miejsca, w ktorym
        /// obserwuje wlasne anulowanie. Tylko prawdziwy await pozwala to zmierzyc,
        /// zamiast dopuszczac "Canceled ALBO Success".
        ///
        /// <paramref name="cancellationToken"/> to token, ktory atrapa dostala OD
        /// PRODUKCJI dla wspolnego odnowienia. Jest tu naprawde honorowany, wiec
        /// anulowanie tego odnowienia PRZERWALO by oczekiwanie - i to jest
        /// widoczne w pomiarze, a nie tylko zapisane obok.
        /// </summary>
        internal async Task EnterAndWaitAsync(CancellationToken cancellationToken)
        {
            entered.TrySetResult();
            await released.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken).ConfigureAwait(false);
        }

        internal bool WaitEntered() => entered.Task.Wait(Deadline);

        /// <summary>
        /// Czekanie ASYNCHRONICZNE, ale CELOWO GLUCHE na token - wylacznie dla
        /// badania SPOZNIONEJ odpowiedzi odnowienia po zmianie generacji konta,
        /// gdzie przedmiotem pomiaru nie jest anulowanie. Trzymane osobno, zeby
        /// nie mieszac go z dowodem wspolnego anulowania.
        /// </summary>
        internal async Task EnterAndWaitIgnoringTokenAsync()
        {
            entered.TrySetResult();
            await released.Task.WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
        }

        internal void Release() => released.TrySetResult();
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> reply) : HttpMessageHandler
    {
        private int count;

        internal Func<HttpRequestMessage, HttpResponseMessage> Reply { get; set; } = reply;

        public int Count => Volatile.Read(ref count);

        internal string? LastToken { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref count);
            LastToken = request.Headers.Authorization?.Parameter;
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
        private bool coordinatorDisposed;

        private Fixture(SonosAccountCoordinator coordinator, Handler handler, SonosControlApiClient client, Gateway gateway, Store store)
        {
            Coordinator = coordinator;
            this.handler = handler;
            this.client = client;
            this.store = store;
            Gateway = gateway;
            // PRAWDZIWY klient Control API JUZ implementuje ISonosFavoriteLoadApi -
            // zaden adapter nie jest potrzebny. Caly tor HTTP jest produkcyjny.
            Api = client;
        }

        internal SonosAccountCoordinator Coordinator { get; }

        internal ISonosFavoriteLoadApi Api { get; }

        internal Gateway Gateway { get; }

        internal int Requests => handler.Count;

        internal string? LastToken => handler.LastToken;

        internal Func<HttpRequestMessage, HttpResponseMessage> Reply
        {
            set => handler.Reply = value;
        }

        internal int Refreshes => Gateway.RefreshCalls;

        internal string? StoredAccessToken => store.AccessToken;

        internal int Deletes => store.Deletes;

        internal int Writes => store.Writes;

        internal static Fixture Connected(
            Func<HttpRequestMessage, HttpResponseMessage> reply,
            int? expiresInSeconds = 3600,
            string? refreshToken = "SYNTHETIC-REFRESH",
            TimeSpan? receivedShift = null,
            bool failWrites = false) =>
            Create(reply, connected: true, expiresInSeconds, refreshToken, receivedShift, failWrites);

        internal static Fixture WithoutAccount(Func<HttpRequestMessage, HttpResponseMessage> reply) =>
            Create(reply, connected: false, 3600, "SYNTHETIC-REFRESH", null, false);

        private static Fixture Create(
            Func<HttpRequestMessage, HttpResponseMessage> reply,
            bool connected,
            int? expiresInSeconds,
            string? refreshToken,
            TimeSpan? receivedShift,
            bool failWrites)
        {
            var now = new DateTimeOffset(2026, 9, 27, 12, 0, 0, TimeSpan.Zero);
            var store = new Store();
            if (connected)
            {
                store.Seed(new SonosStoredCredentials(
                    Broker,
                    new SonosTokens(Access, "Bearer", expiresInSeconds, refreshToken, "playback-control-all"),
                    now + (receivedShift ?? TimeSpan.Zero)));
            }

            store.FailWrites = failWrites;
            var gateway = new Gateway(expiresInSeconds, refreshToken);
            var coordinator = new SonosAccountCoordinator(gateway, store, Broker, () => now);
            coordinator.RestoreOnce();

            var handler = new Handler(reply);
            var client = new SonosControlApiClient(SonosControlApiConfiguration.CreateDefault(Key), handler);
            return new Fixture(coordinator, handler, client, gateway, store);
        }

        /// <summary>PRAWDZIWA droga nowego logowania, bez podstawiania pol i bez refleksji.</summary>
        internal void LogInAsNewAccount(bool expectStored = true)
        {
            Coordinator.BeginLoginAsync(CancellationToken.None)
                .WaitAsync(Deadline).GetAwaiter().GetResult();
            var login = Coordinator.CheckLoginAsync(CancellationToken.None)
                .WaitAsync(Deadline).GetAwaiter().GetResult();
            Check(login.Snapshot.HasCredentials);
            Check(!expectStored || StoredAccessToken == AccessB);
        }

        internal SonosGroupCommandResult Load(
            string? groupId = Group,
            string? favoriteId = FavoriteId,
            SonosFavoriteQueueAction action = SonosFavoriteQueueAction.Append,
            bool playOnCompletion = false,
            CancellationToken cancellationToken = default) =>
            LoadAsync(groupId, favoriteId, action, playOnCompletion, cancellationToken)
                .WaitAsync(TimeSpan.FromSeconds(15)).GetAwaiter().GetResult();

        /// <summary>
        /// WASKIE wejscie asynchroniczne: DOKLADNIE to samo publiczne wywolanie
        /// koordynatora, tylko BEZ blokujacego GetResult i bez Task.Run. Zwrocony
        /// Task jest oddawany taki, jaki wyszedl z produkcji, wiec jego
        /// nieukonczenie dowodzi, ze wolajacy wykonal cala swoja czesc
        /// synchroniczna i siedzi w PRAWDZIWYM awaicie - a nie, ze zyje jeszcze
        /// jakies opakowanie testu.
        /// </summary>
        internal Task<SonosGroupCommandResult> LoadAsync(
            string? groupId = Group,
            string? favoriteId = FavoriteId,
            SonosFavoriteQueueAction action = SonosFavoriteQueueAction.Append,
            bool playOnCompletion = false,
            CancellationToken cancellationToken = default) =>
            Coordinator.LoadFavoriteAsync(Api, groupId, favoriteId, action, playOnCompletion, cancellationToken);

        internal void DisposeTransportOnly()
        {
            coordinatorDisposed = true;
            client.Dispose();
            handler.Dispose();
        }

        public void Dispose()
        {
            if (!coordinatorDisposed)
            {
                Coordinator.Dispose();
            }

            client.Dispose();
            handler.Dispose();
        }
    }

    /// <summary>
    /// Atrapa bramki logowania: zero sieci, policzone odnowienia. Zapamietuje
    /// token, ktory produkcja podala dla PIERWSZEGO (wspolnego) odnowienia, i -
    /// w trybie token-aware - naprawde na nim czeka, wiec jego anulowanie jest
    /// mierzalne, a nie tylko zapisane.
    ///
    /// Atrapa NIE liczy, ilu wolajacych czeka na wspolne odnowienie: takiej
    /// wiedzy nie ma i nigdy nie miala. Liczbe ZAPYTAN do bramki mowi
    /// <see cref="RefreshCalls"/>, a to, ze drugi wolajacy siedzi w TYM SAMYM
    /// odnowieniu, test pokazuje nieukonczonym Taskiem zwroconym z produkcji.
    /// </summary>
    private sealed class Gateway(int? expiresInSeconds, string? refreshToken) : ISonosLoginGateway
    {
        private string? loginAccessToken;
        private Gate? refreshGate;
        private bool refreshGateIgnoresToken;
        private int refreshCalls;
        private CancellationToken sharedRefreshToken;

        public int RefreshCalls => Volatile.Read(ref refreshCalls);

        /// <summary>Czy token WSPOLNEGO odnowienia zostal anulowany (nie powinien).</summary>
        internal bool RefreshCanceled => sharedRefreshToken.IsCancellationRequested;

        /// <summary>Czy token wspolnego odnowienia w ogole DA SIE anulowac.</summary>
        internal bool SharedRefreshTokenCanBeCanceled => sharedRefreshToken.CanBeCanceled;

        internal void AllowLogin(string accessToken) => loginAccessToken = accessToken;

        /// <summary>Trzyma odnowienie awaitem HONORUJACYM token wspolnego odnowienia.</summary>
        internal void HoldRefresh(Gate gate)
        {
            refreshGate = gate;
            refreshGateIgnoresToken = false;
        }

        /// <summary>
        /// Trzyma odnowienie awaitem CELOWO gluchym na token - tylko dla badania
        /// spoznionej odpowiedzi przy zmianie generacji konta.
        /// </summary>
        internal void HoldRefreshIgnoringToken(Gate gate)
        {
            refreshGate = gate;
            refreshGateIgnoresToken = true;
        }

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

        public async Task<SonosRefreshOutcome> RefreshAsync(string? token, CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref refreshCalls) == 1)
            {
                sharedRefreshToken = cancellationToken;
            }

            if (refreshGate is { } gate)
            {
                // PRAWDZIWY await, nie blokada watku: wolajacy moze dojsc do
                // miejsca, w ktorym widzi wlasne anulowanie. Domyslnie await
                // HONORUJE token podany przez produkcje.
                if (refreshGateIgnoresToken)
                {
                    await gate.EnterAndWaitIgnoringTokenAsync().ConfigureAwait(false);
                }
                else
                {
                    await gate.EnterAndWaitAsync(cancellationToken).ConfigureAwait(false);
                }
            }

            return SonosRefreshOutcome.Ok(new SonosTokens(
                Fixture.Renewed, "Bearer", expiresInSeconds ?? 3600, refreshToken, "playback-control-all"));
        }
    }

    /// <summary>Atrapa magazynu w PAMIECI: bez DPAPI i bez pliku. Liczy zapisy i usuniecia.</summary>
    private sealed class Store : ISonosCredentialStore
    {
        private SonosStoredCredentials? record;

        internal void Seed(SonosStoredCredentials value) => record = value;

        internal bool FailWrites { get; set; }

        internal string? AccessToken => record?.Tokens.AccessToken;

        internal int Deletes { get; private set; }

        internal int Writes { get; private set; }

        public SonosCredentialReadOutcome Read() => record is null
            ? SonosCredentialReadOutcome.Failure(SonosCredentialReadStatus.Missing)
            : SonosCredentialReadOutcome.Ok(record);

        public SonosCredentialWriteOutcome Write(SonosStoredCredentials credentials)
        {
            Writes++;
            if (FailWrites)
            {
                // Kontrakt magazynu: nieudany zapis NIE zmienia zawartosci.
                return SonosCredentialWriteOutcome.Failure(SonosCredentialWriteStatus.WriteFailure);
            }

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
