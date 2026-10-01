using System;
using System.Threading;
using System.Threading.Tasks;

namespace AccessibleMediaController.Core.Sonos;

/// <summary>
/// WLASNE RADIO W SONOSIE przez ISTNIEJACEGO wlasciciela konta: utworzenie sesji
/// odtwarzania i wczytanie do niej adresu strumienia.
///
/// Ta czesc koordynatora jest CIENKA z zamyslu. Cala droga poswiadczen - bilet
/// pod blokada, HTTP poza blokada, jedno odnowienie TYLKO przed wyslaniem i
/// tylko przy znanej minionej waznosci, potem DOKLADNIE JEDEN POST, kontrola
/// ORYGINALNEJ generacji po kazdym await - jest JUZ rozstrzygnieta we WSPOLNYM
/// <see cref="RunSessionWriteAsync{TOutcome,TResult}"/>, ktory jest tym samym
/// przebiegiem co <see cref="RunGroupCommandAsync"/>, tylko potrafi oddac wynik
/// Z DANYMI. Nie ma tu ani kopii biletu, ani kopii odnawiania, ani wlasnej
/// blokady, ani wlasnej polityki identyfikatorow.
///
/// TWORZENIE SESJI TO ZAPIS, KTORY MOZE PRZEJAC GRUPE. Stad reguly bez wyjatku:
///   * NIGDY nie wola tego zaden odczyt, zadne wejscie do widoku, zadne
///     listowanie ani zadne ponowienie po 401 - sciezka odczytu
///     (<see cref="RunGroupReadAsync{TValue}"/>) nie ma do tej metody dostepu,
///   * ZERO automatycznego tworzenia sesji po eviction, po ERROR_SESSION_EVICTED,
///     po NotFound i po jakimkolwiek bledzie. Nowa sesja powstaje WYLACZNIE z
///     jawnego, nowego zadania uzytkownika,
///   * ZERO powtorzen POST - takze po 401, bo drugie utworzenie sesji moze
///     przerwac to, co w tym czasie zaczelo grac,
///   * anulowanie PO wyslaniu nie cofa przejecia sesji: wynik mowi wtedy o
///     PROBIE i NIEZNANYM skutku,
///   * sessionId wychodzi TYLKO z waznego wyniku BIEZACEGO konta. Wynik
///     SPOZNIONY (zmiana konta w trakcie) jest PORZUCANY i identyfikatora nie
///     publikuje - stara sesja konta A nie moze trafic do kontekstu konta B.
///
/// Tworzenie sesji i wczytanie radia sa ROZDZIELONE na dwie jawne metody. Nie ma
/// tu zadnej maszyny stanow, przechowywania sessionId, odtwarzania sesji ani
/// orkiestracji UI - to kolejny etap.
/// </summary>
public sealed partial class SonosAccountCoordinator
{
    /// <summary>
    /// POST /groups/{groupId}/playbackSession przez bilet biezacego konta -
    /// JAWNE utworzenie sesji odtwarzania.
    ///
    /// Wolno wywolac TYLKO w odpowiedzi na jawne zadanie uzytkownika. Wg definicji
    /// Sonosa to polecenie moze wyprzec istniejace sesje w grupie, czyli przerwac
    /// to, czego uzytkownik sluchal.
    ///
    /// <paramref name="request"/> (appId + appContext) podaje WOLAJACY - tu nie ma
    /// zadnych wartosci domyslnych.
    /// </summary>
    public async Task<SonosSessionCreateResult> CreateSessionAsync(
        ISonosSessionCreateApi api,
        string? groupId,
        SonosSessionRequest? request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(api);
        return await RunSessionWriteAsync(
            (token, ct) => api.CreateSessionAsync(token, groupId, request, ct),
            static outcome => outcome.Sent,
            static (status, outcome, sent, renewed, snapshot) =>
                new SonosSessionCreateResult(status, outcome, sent, renewed, snapshot),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// POST /playbackSessions/{sessionId}/playbackSession/loadStreamUrl przez
    /// bilet biezacego konta - wczytanie WLASNEGO adresu radia do OTWARTEJ sesji.
    ///
    /// <paramref name="sessionId"/> podaje wolajacy: koordynator go NIE
    /// przechowuje i nie podstawia zapamietanego. Gdy sesja zostala zamknieta
    /// albo przejeta, wynikiem jest blad - nie ciche utworzenie nowej sesji.
    ///
    /// <paramref name="playOnCompletion"/> jest OBOWIAZKOWY. Gdy jest true, NIE
    /// wolno dosylac osobnego polecenia Play.
    /// </summary>
    public async Task<SonosStreamUrlLoadResult> LoadStreamUrlAsync(
        ISonosStreamUrlLoadApi api,
        string? sessionId,
        string? streamUrl,
        bool playOnCompletion,
        string? itemId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(api);
        return await RunSessionWriteAsync(
            (token, ct) => api.LoadStreamUrlAsync(token, sessionId, streamUrl, playOnCompletion, itemId, ct),
            static outcome => outcome.Sent,
            static (status, outcome, sent, renewed, snapshot) =>
                new SonosStreamUrlLoadResult(status, outcome, sent, renewed, snapshot),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// WSPOLNY przebieg ZAPISU sesji. Jest to DOKLADNIE ten sam kontrakt
    /// poswiadczen co <see cref="RunGroupCommandAsync"/> - bilet pod blokada,
    /// odnowienie tylko PRZED wyslaniem i tylko przy znanej minionej waznosci,
    /// potem JEDEN POST, zero powtorzen, kontrola generacji na zamkniecie. Jedyna
    /// roznica: wynik moze niesc DANE odpowiedzi (sessionId), dlatego przebieg
    /// jest generyczny po typie wyniku transportu.
    ///
    /// Brak jakiegokolwiek ponowienia po 401 jest tu tym bardziej konieczny niz
    /// przy poleceniach: drugi POST utworzylby DRUGA sesje, znow przejmujac grupe.
    /// </summary>
    private async Task<TResult> RunSessionWriteAsync<TOutcome, TResult>(
        Func<string?, CancellationToken, Task<TOutcome>> send,
        Func<TOutcome, bool> sentOf,
        Func<SonosGroupOperationStatus, TOutcome?, bool, bool, SonosAccountSnapshot, TResult> build,
        CancellationToken cancellationToken)
        where TOutcome : class
    {
        string? accessToken;
        long generation;
        bool expired;
        bool canRenew;
        lock (gate)
        {
            ThrowIfDisposed();
            if (!TryTakeReadTicketLocked(out accessToken, out generation, out expired, out canRenew))
            {
                // Brak poswiadczen: ZERO zapytan, ZERO odnowien, zadnej sesji.
                return build(SonosGroupOperationStatus.NoAccount, null, false, false, CreateSnapshot());
            }
        }

        var renewed = false;
        if (expired && canRenew)
        {
            var renew = await RenewForReadAsync(generation, cancellationToken).ConfigureAwait(false);
            if (renew.Terminal is { } before)
            {
                // Zadnego POST nie bylo: zadna sesja nie mogla powstac.
                var status = before.Status switch
                {
                    SonosDeviceReadStatus.Discarded => SonosGroupOperationStatus.Discarded,
                    SonosDeviceReadStatus.Canceled => SonosGroupOperationStatus.Canceled,
                    SonosDeviceReadStatus.NoAccount => SonosGroupOperationStatus.NoAccount,
                    _ => SonosGroupOperationStatus.Unauthorized
                };
                return build(status, null, false, false, before.Snapshot);
            }

            renewed = renew.Renewed;
            accessToken = renew.AccessToken;
            generation = renew.Generation;
        }

        using var linked = Link(cancellationToken);
        TOutcome outcome;
        try
        {
            // JEDEN POST. Koniec drogi autoryzacji dla tego zapisu - nawet 401 nie
            // powoduje ponowienia, bo powtorzony zapis moglby przejac grupe dwa razy.
            outcome = await send(accessToken, linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Anulowano PRZED wyslaniem (transport nie zdazyl zaczac).
            return FinishSessionWrite<TOutcome, TResult>(build, null, false, renewed, generation);
        }

        return FinishSessionWrite(build, outcome, sentOf(outcome), renewed, generation);
    }

    /// <summary>
    /// Zamkniecie ZAPISU sesji pod blokada. Tu rozstrzyga sie najwazniejsza
    /// ochrona konta: gdy generacja poswiadczen zmienila sie w trakcie (nowe
    /// logowanie, Disconnect) albo wlasciciel zakonczyl prace, wynik jest
    /// PORZUCONY i <b>nie oddaje danych odpowiedzi</b>. Dzieki temu sessionId
    /// nalezacy do konta A NIGDY nie wychodzi do kontekstu konta B.
    ///
    /// Porzucenie zachowuje przy tym uczciwe rozroznienie: jesli zadanie poszlo,
    /// mowimy o PROBIE i nieznanym skutku, a nie o cofnieciu - sesja moze juz
    /// istniec i moze juz byla przejela grupe.
    /// </summary>
    private TResult FinishSessionWrite<TOutcome, TResult>(
        Func<SonosGroupOperationStatus, TOutcome?, bool, bool, SonosAccountSnapshot, TResult> build,
        TOutcome? outcome,
        bool requestSent,
        bool renewed,
        long generation)
        where TOutcome : class
    {
        lock (gate)
        {
            if (disposed)
            {
                return build(SonosGroupOperationStatus.Discarded, null, requestSent, renewed, EmptySnapshot());
            }

            if (credentialGeneration != generation)
            {
                return build(SonosGroupOperationStatus.Discarded, null, requestSent, renewed, CreateSnapshot());
            }

            if (outcome is null)
            {
                return build(SonosGroupOperationStatus.Canceled, null, false, renewed, CreateSnapshot());
            }

            return build(SonosGroupOperationStatus.Attempted, outcome, requestSent, renewed, CreateSnapshot());
        }
    }
}

/// <summary>
/// Wynik UTWORZENIA SESJI widziany przez konto. Trzyma te same granice co wynik
/// polecenia grupy (proba / przyjecie / nieznany skutek) i dodaje jedna wlasna,
/// kluczowa: sessionId wychodzi TYLKO z waznego wyniku BIEZACEGO konta.
/// </summary>
public sealed class SonosSessionCreateResult
{
    internal SonosSessionCreateResult(
        SonosGroupOperationStatus status,
        SonosSessionOutcome? outcome,
        bool requestSent,
        bool renewed,
        SonosAccountSnapshot snapshot)
    {
        Status = status;
        // Przy PORZUCENIU i przy braku konta nie ma zadnego wyniku transportu,
        // wiec nie ma tez zadnego sessionId do oddania.
        Outcome = status == SonosGroupOperationStatus.Attempted ? outcome : null;
        RequestSent = requestSent;
        Renewed = renewed;
        Snapshot = snapshot;
    }

    public SonosGroupOperationStatus Status { get; }

    /// <summary>Wynik transportu TYLKO gdy proba doszla do skutku na biezacym koncie.</summary>
    public SonosSessionOutcome? Outcome { get; }

    /// <summary>Czy zadanie HTTP w ogole opuscilo aplikacje.</summary>
    public bool RequestSent { get; }

    /// <summary>Czy po drodze uzyto ISTNIEJACEGO odnowienia dostepu (najwyzej raz).</summary>
    public bool Renewed { get; }

    public SonosAccountSnapshot Snapshot { get; }

    /// <summary>Sonos PRZYJAL zadanie utworzenia sesji. NIE znaczy, ze cokolwiek gra.</summary>
    public bool Accepted => Outcome?.Accepted == true;

    /// <summary>
    /// Sesja GOTOWA do dalszego polecenia: przyjeta i z uzytecznym sessionId.
    /// HTTP 200 bez identyfikatora gotowa sesja NIE JEST.
    /// </summary>
    public bool Ready => Outcome?.Ready == true;

    /// <summary>Wynik SPOZNIONY: konto zmienilo sie w trakcie, danych nie publikujemy.</summary>
    public bool Discarded => Status == SonosGroupOperationStatus.Discarded;

    /// <summary>
    /// Skutek NIEZNANY: zapis poszedl w siec bez odpowiedzi albo wynik zostal
    /// porzucony PO wyslaniu. Sesja mogla powstac i przejac grupe. Nie wolno
    /// tego ponawiac automatycznie.
    /// </summary>
    public bool EffectAmbiguous =>
        Outcome?.EffectAmbiguous == true || (Discarded && RequestSent);

    public string Message => Status switch
    {
        SonosGroupOperationStatus.NoAccount =>
            "Nie ma połączonego konta Sonos, więc nie ma gdzie utworzyć sesji odtwarzania.",
        SonosGroupOperationStatus.Unauthorized =>
            "Nie udało się odnowić dostępu do konta Sonos, więc żądania sesji nie wysłano.",
        SonosGroupOperationStatus.Canceled => "Żądanie sesji Sonos anulowano przed wysłaniem.",
        SonosGroupOperationStatus.Discarded => RequestSent
            ? "Kontekst konta Sonos zmienił się po wysłaniu żądania sesji: wyniku nie używamy, "
              + "a skutek pozostaje nieznany. Sprawdź stan odtwarzania."
            : "Żądanie sesji Sonos pominięto: kontekst konta zmienił się w trakcie.",
        _ => Outcome?.Message ?? "Stan żądania sesji Sonos jest nieznany."
    };

    /// <summary>
    /// Oddaje sessionId do dalszego polecenia - ale TYLKO z waznego wyniku
    /// biezacego konta i tylko gdy identyfikator naprawde da sie uzyc.
    ///
    /// Zwraca false dla porzuconego wyniku (zmiana konta), dla braku konta, dla
    /// bledu i dla HTTP 200 bez identyfikatora. To JEDYNA droga wyjscia sessionId
    /// z tej warstwy; wlasciwosci publicznej z samym napisem celowo nie ma.
    /// </summary>
    public bool TryGetSessionId(out string? sessionId)
    {
        sessionId = null;
        if (Status != SonosGroupOperationStatus.Attempted || Outcome?.Ready != true)
        {
            return false;
        }

        sessionId = Outcome.Session!.SessionId;
        return true;
    }

    /// <summary>
    /// WYLACZNIE DO POMIARU: gotowy wynik bez konta, tokenu i transportu. Migawka
    /// jest PUSTA, wiec nikt nie pomyli tego z prawdziwym kontem.
    /// </summary>
    public static SonosSessionCreateResult CreateForMeasurement(
        SonosGroupOperationStatus status, SonosSessionOutcome? outcome, bool requestSent) =>
        new(status, outcome, requestSent, renewed: false, SonosAccountSnapshots.Empty);

    /// <summary>
    /// Kontrolowane ToString: BEZ sessionId, bez appId, bez appContext i bez
    /// tokenu. Odczytany identyfikator sesji nie moze trafic do logu.
    /// </summary>
    public override string ToString() =>
        "Utworzenie sesji Sonos przez konto: " + Status
        + (RequestSent ? ", żądanie wysłane" : ", żądania nie wysłano")
        + (Ready ? ", sesja gotowa" : ", brak gotowej sesji")
        + (EffectAmbiguous ? ", skutek nieznany" : string.Empty)
        + (Renewed ? ", po odnowieniu dostępu" : string.Empty);
}

/// <summary>
/// Wynik WCZYTANIA RADIA widziany przez konto. Te same granice: proba /
/// przyjecie / nieznany skutek, zadnych danych z porzuconego wyniku.
/// </summary>
public sealed class SonosStreamUrlLoadResult
{
    internal SonosStreamUrlLoadResult(
        SonosGroupOperationStatus status,
        SonosStreamUrlOutcome? outcome,
        bool requestSent,
        bool renewed,
        SonosAccountSnapshot snapshot)
    {
        Status = status;
        Outcome = status == SonosGroupOperationStatus.Attempted ? outcome : null;
        RequestSent = requestSent;
        Renewed = renewed;
        Snapshot = snapshot;
    }

    public SonosGroupOperationStatus Status { get; }

    public SonosStreamUrlOutcome? Outcome { get; }

    public bool RequestSent { get; }

    public bool Renewed { get; }

    public SonosAccountSnapshot Snapshot { get; }

    /// <summary>Sonos PRZYJAL adres radia. NIE znaczy, ze radio gra.</summary>
    public bool Accepted => Outcome?.Accepted == true;

    /// <summary>Zawsze false: przyjecie adresu nie dowodzi odtwarzania.</summary>
    public bool EffectConfirmed => false;

    public bool Discarded => Status == SonosGroupOperationStatus.Discarded;

    public bool EffectAmbiguous =>
        Outcome?.EffectAmbiguous == true || (Discarded && RequestSent);

    public string Message => Status switch
    {
        SonosGroupOperationStatus.NoAccount =>
            "Nie ma połączonego konta Sonos, więc nie ma gdzie wczytać radia.",
        SonosGroupOperationStatus.Unauthorized =>
            "Nie udało się odnowić dostępu do konta Sonos, więc radia nie wysłano.",
        SonosGroupOperationStatus.Canceled => "Wczytanie radia anulowano przed wysłaniem.",
        SonosGroupOperationStatus.Discarded => RequestSent
            ? "Kontekst konta Sonos zmienił się po wysłaniu radia: wyniku nie używamy, "
              + "a skutek pozostaje nieznany. Sprawdź stan odtwarzania."
            : "Wczytanie radia pominięto: kontekst konta zmienił się w trakcie.",
        _ => Outcome?.Message ?? "Stan wczytania radia w Sonosie jest nieznany."
    };

    /// <summary>WYLACZNIE DO POMIARU - jak w wyniku sesji, z PUSTA migawka.</summary>
    public static SonosStreamUrlLoadResult CreateForMeasurement(
        SonosGroupOperationStatus status, SonosStreamUrlOutcome? outcome, bool requestSent) =>
        new(status, outcome, requestSent, renewed: false, SonosAccountSnapshots.Empty);

    /// <summary>Kontrolowane ToString: bez adresu strumienia i bez sessionId.</summary>
    public override string ToString() =>
        "Wczytanie radia w Sonosie przez konto: " + Status
        + (RequestSent ? ", żądanie wysłane" : ", żądania nie wysłano")
        + (Accepted ? ", przyjęte (odtwarzanie niepotwierdzone)" : string.Empty)
        + (EffectAmbiguous ? ", skutek nieznany" : string.Empty)
        + (Renewed ? ", po odnowieniu dostępu" : string.Empty);
}
