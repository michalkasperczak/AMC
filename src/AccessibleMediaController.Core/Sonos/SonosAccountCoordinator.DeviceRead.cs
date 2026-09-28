using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AccessibleMediaController.Core.Sonos;

/// <summary>
/// ODCZYT domow, grup i glosnikow przez konto Sonos. Dopisane do ISTNIEJACEGO
/// koordynatora, bo tylko on wie, ktory zestaw poswiadczen jest biezacy.
///
/// Granice, swiadome i sprawdzane testami:
///   * WASKIE operacje odczytu. Zaden getter tokenu ani callback dowolnego
///     tokenu nie wychodzi na zewnatrz; UI dostaje wylacznie nazwy i stany,
///   * token i generacja kopiowane POD blokada, prawdziwy HTTP zawsze POZA nia,
///   * po powrocie z HTTP wynik jest PORZUCANY, gdy generacja zestawu sie
///     zmienila (Disconnect, nowe logowanie) albo wlasciciel zakonczyl prace,
///   * odnowienie korzysta z ISTNIEJACEGO <see cref="RefreshAsync"/>. Najwyzej
///     JEDNA proba odnowienia i JEDNO powtorzenie GET. Zero petli reauth,
///   * NIEZNANY termin waznosci NIE jest wylogowaniem i nie wywoluje odnowienia
///     "na wszelki wypadek",
///   * 401 po powtorzeniu, 403, 429 i 5xx NIE kasuja konta - o kasowaniu
///     decyduje wylacznie istniejaca sciezka odnawiania (dokladne 401 brokera).
/// </summary>
public sealed partial class SonosAccountCoordinator
{
    /// <summary>
    /// Czy odczyt urzadzen ma prawo skorzystac z jednej proby odnowienia dostepu.
    /// Wymaga tokenu odswiezania w biezacym zestawie.
    /// </summary>
    private bool CanRenewLocked() => current is not null && current.HasRefreshToken;

    /// <summary>
    /// Bezpieczna kopia do wykonania zapytania POZA blokada. Null, gdy nie ma
    /// z czym pytac.
    /// </summary>
    private bool TryTakeReadTicketLocked(out string? accessToken, out long generation, out bool expired, out bool canRenew)
    {
        accessToken = null;
        generation = 0;
        expired = false;
        canRenew = false;
        if (current is null)
        {
            return false;
        }

        accessToken = current.Tokens.AccessToken;
        generation = credentialGeneration;
        canRenew = CanRenewLocked();

        // ZNANY i MINIONY termin waznosci. Nieznany zostaje nieznany: wtedy
        // probujemy zwyczajnie i ewentualnie reagujemy na 401.
        expired = current.IsExpiryKnown
            && current.ExpiresAtUtc is { } expiresAt
            && expiresAt <= clock().ToUniversalTime();
        return true;
    }

    /// <summary>
    /// Lista DOMOW konta. Pusta lista jest sukcesem (konto bez podlaczonych
    /// urzadzen), nie bledem.
    /// </summary>
    public async Task<SonosHouseholdsReadResult> ReadHouseholdsAsync(
        ISonosDeviceApi api,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(api);
        var attempt = await RunReadAsync(
            (token, ct) => ReadHouseholdsOnceAsync(api, token, ct),
            cancellationToken).ConfigureAwait(false);

        return new SonosHouseholdsReadResult(
            attempt.Status,
            attempt.Status == SonosDeviceReadStatus.Success ? attempt.Value?.Households : null,
            attempt.Renewed,
            attempt.Snapshot);
    }

    /// <summary>
    /// GRUPY i GLOSNIKI jednego domu. Pusty dom jest sukcesem, a
    /// <see cref="SonosHouseholdTopology.Partial"/> zostaje jawne - nie udajemy
    /// pelnej listy.
    /// </summary>
    public async Task<SonosGroupsReadResult> ReadGroupsAsync(
        ISonosDeviceApi api,
        string? householdId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(api);
        var attempt = await RunReadAsync(
            (token, ct) => ReadGroupsOnceAsync(api, token, householdId, ct),
            cancellationToken).ConfigureAwait(false);

        return new SonosGroupsReadResult(
            attempt.Status,
            attempt.Status == SonosDeviceReadStatus.Success ? attempt.Topology : null,
            attempt.Renewed,
            attempt.Snapshot);
    }

    /// <summary>Wspolny, rozdzielny wynik jednego przebiegu odczytu.</summary>
    private readonly struct ReadAttempt
    {
        internal ReadAttempt(
            SonosDeviceReadStatus status,
            SonosHouseholdsOutcome? value,
            SonosHouseholdTopology? topology,
            bool renewed,
            SonosAccountSnapshot snapshot)
        {
            Status = status;
            Value = value;
            Topology = topology;
            Renewed = renewed;
            Snapshot = snapshot;
        }

        internal SonosDeviceReadStatus Status { get; }

        internal SonosHouseholdsOutcome? Value { get; }

        internal SonosHouseholdTopology? Topology { get; }

        internal bool Renewed { get; }

        internal SonosAccountSnapshot Snapshot { get; }
    }

    /// <summary>Surowy wynik JEDNEGO zapytania: status transportu plus dane.</summary>
    private readonly struct RawRead
    {
        internal RawRead(SonosControlApiStatus status, SonosHouseholdsOutcome? households, SonosHouseholdTopology? topology)
        {
            Status = status;
            Households = households;
            Topology = topology;
        }

        internal SonosControlApiStatus Status { get; }

        internal SonosHouseholdsOutcome? Households { get; }

        internal SonosHouseholdTopology? Topology { get; }
    }

    private static async Task<RawRead> ReadHouseholdsOnceAsync(
        ISonosDeviceApi api,
        string? accessToken,
        CancellationToken cancellationToken)
    {
        var outcome = await api.GetHouseholdsAsync(accessToken, cancellationToken).ConfigureAwait(false);
        return new RawRead(outcome.Status, outcome, null);
    }

    private static async Task<RawRead> ReadGroupsOnceAsync(
        ISonosDeviceApi api,
        string? accessToken,
        string? householdId,
        CancellationToken cancellationToken)
    {
        var outcome = await api.GetGroupsAsync(accessToken, householdId, cancellationToken).ConfigureAwait(false);
        return new RawRead(outcome.Status, null, outcome.Topology);
    }

    /// <summary>
    /// Wspolny przebieg: bilet pod blokada, HTTP poza blokada, najwyzej jedno
    /// odnowienie i jedno powtorzenie, kontrola generacji po kazdym powrocie.
    /// </summary>
    private async Task<ReadAttempt> RunReadAsync(
        Func<string?, CancellationToken, Task<RawRead>> read,
        CancellationToken cancellationToken)
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
                return new ReadAttempt(SonosDeviceReadStatus.NoAccount, null, null, false, CreateSnapshot());
            }
        }

        var renewed = false;

        // ZNANA, miniona waznosc: jedna proba odnowienia PRZED zapytaniem.
        // Nieznana waznosc nie uruchamia tego kroku.
        if (expired && canRenew)
        {
            var renew = await RenewForReadAsync(generation, cancellationToken).ConfigureAwait(false);
            if (renew.Terminal is { } terminalBefore)
            {
                return terminalBefore;
            }

            renewed = renew.Renewed;
            accessToken = renew.AccessToken;
            generation = renew.Generation;
            canRenew = renew.CanRenew;
        }

        using var linked = Link(cancellationToken);
        RawRead reply;
        try
        {
            reply = await read(accessToken, linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return Finish(SonosDeviceReadStatus.Canceled, null, null, renewed, generation);
        }

        if (reply.Status != SonosControlApiStatus.Unauthorized || renewed || !canRenew)
        {
            return Finish(
                SonosDeviceReadMessages.From(reply.Status),
                reply.Households,
                reply.Topology,
                renewed,
                generation);
        }

        // DOKLADNIE JEDNO odnowienie po 401 i DOKLADNIE JEDNO powtorzenie GET.
        var afterUnauthorized = await RenewForReadAsync(generation, cancellationToken).ConfigureAwait(false);
        if (afterUnauthorized.Terminal is { } terminal)
        {
            return terminal;
        }

        if (!afterUnauthorized.Renewed)
        {
            // Odnowienie nie dalo nowego zestawu: zostaje pierwotne 401.
            return Finish(SonosDeviceReadStatus.Unauthorized, null, null, false, generation);
        }

        using var retryLink = Link(cancellationToken);
        RawRead retry;
        try
        {
            retry = await read(afterUnauthorized.AccessToken, retryLink.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return Finish(SonosDeviceReadStatus.Canceled, null, null, true, afterUnauthorized.Generation);
        }

        return Finish(
            SonosDeviceReadMessages.From(retry.Status),
            retry.Households,
            retry.Topology,
            true,
            afterUnauthorized.Generation);
    }

    /// <summary>Wynik proby odnowienia w sluzbie odczytu.</summary>
    private readonly struct RenewOutcome
    {
        internal RenewOutcome(ReadAttempt? terminal, bool renewed, string? accessToken, long generation, bool canRenew)
        {
            Terminal = terminal;
            Renewed = renewed;
            AccessToken = accessToken;
            Generation = generation;
            CanRenew = canRenew;
        }

        /// <summary>Gdy ustawione, odczyt konczy sie TYM wynikiem (porzucenie, anulowanie, 401).</summary>
        internal ReadAttempt? Terminal { get; }

        internal bool Renewed { get; }

        internal string? AccessToken { get; }

        internal long Generation { get; }

        internal bool CanRenew { get; }
    }

    /// <summary>
    /// Uzywa ISTNIEJACEJ, centralnej logiki odnawiania (<see cref="RefreshCoreAsync"/>)
    /// - bez wlasnej kopii i bez dodatkowego zewnetrznego locka na I/O.
    ///
    /// <paramref name="generation"/> jest WARUNKIEM WSTEPNYM sprawdzanym ATOMOWO
    /// pod ta sama blokada, pod ktora odnowienie startuje albo dolacza. Jesli
    /// konto zmienilo sie wczesniej (nowe logowanie, Disconnect), NIE leci zadne
    /// zapytanie odnowienia i odczyt konczy sie PORZUCENIEM. Po powrocie wynik
    /// jest kotwiczony w DOKLADNYM zestawie zainstalowanym TYM odnowieniem, nie
    /// w dowolnej biezacej migawce - swieza generacja nie legalizuje starej
    /// operacji.
    /// </summary>
    private async Task<RenewOutcome> RenewForReadAsync(long generation, CancellationToken cancellationToken)
    {
        RefreshRun run;
        try
        {
            run = await RefreshCoreAsync(generation, cancellationToken).ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
            // Wlasciciel zakonczyl prace w trakcie: wynik jest PORZUCONY, nie bledem uslugi.
            return new RenewOutcome(
                new ReadAttempt(SonosDeviceReadStatus.Discarded, null, null, false, EmptySnapshot()),
                false, null, generation, false);
        }
        catch (OperationCanceledException)
        {
            return new RenewOutcome(
                new ReadAttempt(SonosDeviceReadStatus.Canceled, null, null, false, SafeSnapshot()),
                false, null, generation, false);
        }

        if (run.GenerationMismatch)
        {
            // Konto zmienilo sie PRZED naszym odnowieniem: zero zapytan do bramki,
            // zero odczytu nowego konta.
            return new RenewOutcome(
                new ReadAttempt(SonosDeviceReadStatus.Discarded, null, null, false, run.Result.Snapshot),
                false, null, generation, false);
        }

        var refresh = run.Result;
        if (refresh.RefreshStatus == SonosRefreshStatus.Canceled)
        {
            return new RenewOutcome(
                new ReadAttempt(SonosDeviceReadStatus.Canceled, null, null, false, refresh.Snapshot),
                false, null, generation, false);
        }

        if (refresh.Discarded)
        {
            // Odpowiedz odnowienia byla SPOZNIONA wobec nowszego zestawu: nasza
            // operacja tez jest stara.
            return new RenewOutcome(
                new ReadAttempt(SonosDeviceReadStatus.Discarded, null, null, false, refresh.Snapshot),
                false, null, generation, false);
        }

        lock (gate)
        {
            if (disposed)
            {
                return new RenewOutcome(
                    new ReadAttempt(SonosDeviceReadStatus.Discarded, null, null, false, EmptySnapshot()),
                    false, null, generation, false);
            }

            if (run.Invalidated || current is null)
            {
                // Odnowienie skonczylo sie wylogowaniem (dokladne 401 brokera)
                // albo konto zniklo w trakcie.
                var status = run.Invalidated
                    || refresh.RefreshStatus == SonosRefreshStatus.ReauthorizationRequired
                    ? SonosDeviceReadStatus.Unauthorized
                    : SonosDeviceReadStatus.NoAccount;
                return new RenewOutcome(
                    new ReadAttempt(status, null, null, false, CreateSnapshot()),
                    false, null, generation, false);
            }

            if (run.InstalledCredentials is null)
            {
                // Nie udalo sie odnowic, ale konto zyje i to NADAL nasz zestaw:
                // wolajacy zdecyduje, czy to koniec (brak powtorzenia).
                if (credentialGeneration != generation)
                {
                    return new RenewOutcome(
                        new ReadAttempt(SonosDeviceReadStatus.Discarded, null, null, false, CreateSnapshot()),
                        false, null, generation, false);
                }

                return new RenewOutcome(null, false, current.Tokens.AccessToken, generation, false);
            }

            // KOTWICA: legalny jest WYLACZNIE zestaw zainstalowany TYM odnowieniem
            // i tylko dopoki nadal obowiazuje. Cokolwiek innego (nowe logowanie w
            // czasie odnawiania) oznacza porzucenie starej operacji.
            if (!ReferenceEquals(current, run.InstalledCredentials)
                || credentialGeneration != run.InstalledGeneration)
            {
                return new RenewOutcome(
                    new ReadAttempt(SonosDeviceReadStatus.Discarded, null, null, false, CreateSnapshot()),
                    false, null, generation, false);
            }

            return new RenewOutcome(
                null, true, run.InstalledCredentials.Tokens.AccessToken, run.InstalledGeneration, CanRenewLocked());
        }
    }

    /// <summary>
    /// PUNKT WSTAWIENIA poswiadczen WYLACZNIE do pomiaru: pozwala zbudowac stan
    /// "konto dziala, ale zapis do magazynu sie nie udal" bez logowania i bez
    /// dotykania prawdziwego magazynu. Nie oddaje tokenow na zewnatrz i nie ma
    /// zadnego wywolania produkcyjnego.
    /// </summary>
    internal void ApplyCredentialsForMeasurement(SonosTokens tokens, SonosCredentialWriteStatus writeStatus)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        lock (gate)
        {
            current = new SonosStoredCredentials(brokerOrigin, tokens, DateTimeOffset.UtcNow);
            credentialGeneration++;
            ApplyWriteLocked(writeStatus);
        }
    }

    /// <summary>Migawka bezpieczna takze po zwolnieniu wlasciciela.</summary>
    private SonosAccountSnapshot SafeSnapshot()
    {
        lock (gate)
        {
            return disposed ? EmptySnapshot() : CreateSnapshot();
        }
    }

    /// <summary>
    /// Zamkniecie odczytu: wynik jest PORZUCANY, gdy zestaw zmienil sie w
    /// trakcie albo wlasciciel zakonczyl prace. Dane spoznione NIE podmieniaja
    /// listy w oknie.
    /// </summary>
    private ReadAttempt Finish(
        SonosDeviceReadStatus status,
        SonosHouseholdsOutcome? households,
        SonosHouseholdTopology? topology,
        bool renewed,
        long generation)
    {
        lock (gate)
        {
            if (disposed)
            {
                return new ReadAttempt(SonosDeviceReadStatus.Discarded, null, null, renewed, EmptySnapshot());
            }

            if (credentialGeneration != generation)
            {
                return new ReadAttempt(SonosDeviceReadStatus.Discarded, null, null, renewed, CreateSnapshot());
            }

            return new ReadAttempt(status, households, topology, renewed, CreateSnapshot());
        }
    }

    /// <summary>
    /// Migawka dla przypadku, w ktorym wlasciciel juz zakonczyl prace: bez
    /// czytania stanu pod blokada zwolnionego obiektu i bez tokenow.
    /// </summary>
    private static SonosAccountSnapshot EmptySnapshot() => SonosAccountSnapshots.Empty;
}
