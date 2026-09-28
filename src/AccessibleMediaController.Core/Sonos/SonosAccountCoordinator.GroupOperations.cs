using System;
using System.Threading;
using System.Threading.Tasks;

namespace AccessibleMediaController.Core.Sonos;

/// <summary>
/// AUTORYZOWANE operacje GRUPY przez ISTNIEJACEGO wlasciciela konta: trzy
/// odczyty (stan odtwarzania, metadane, glosnosc) i podstawowe polecenia.
/// Dopisane do TEGO SAMEGO koordynatora, bo tylko on wie, ktory zestaw
/// poswiadczen jest biezacy.
///
/// Granice, swiadome i sprawdzane testami:
///   * token i generacja kopiowane POD blokada (jeden bilet, jedno czytanie),
///     prawdziwy HTTP i cudze callbacki ZAWSZE poza nia,
///   * po KAZDYM await weryfikowana jest ORYGINALNA generacja. Dawna operacja
///     po nowym logowaniu albo Disconnect jest PORZUCANA: nie odnawia konta B,
///     nie uzywa jego biletu i nie publikuje spoznionych danych,
///   * ODCZYT: znana miniona waznosc -> jedno odnowienie PRZED zapytaniem;
///     401 -> najwyzej JEDNO odnowienie i JEDNO powtorzenie, dokladnie jak w
///     <see cref="RunReadAsync"/>. Zero petli reauth,
///   * POLECENIE: odnowienie TYLKO PRZED wyslaniem i tylko przy ZNANEJ minionej
///     waznosci, potem DOKLADNIE JEDEN POST. Po 401, 429, 5xx ani po utraconej
///     odpowiedzi NIE MA zadnego powtorzenia - polecenie przelaczajace albo
///     wzgledne wykonane dwa razy da inny skutek niz raz,
///   * samo polecenie NIE kasuje konta i nie niesie w sobie kolejnej
///     autoryzacji. Jawne pozniejsze odnowienie to OSOBNA operacja,
///   * porzucenie JUZ WYSLANEGO polecenia nie udaje cofniecia: proba zostaje
///     rozdzielona od nieznanego skutku,
///   * NIE MA tu weryfikacji skutku, pollingu ani parsera kodow bledu - to
///     kolejny etap. Nie ma tez zadnego gettera tokenu dla UI.
///
/// Wspolne z odczytem urzadzen jest WSZYSTKO, co dotyczy poswiadczen: bilet
/// (<see cref="TryTakeReadTicketLocked"/>) i JEDNA centralna logika odnawiania
/// (<see cref="RenewForReadAsync"/> nad <see cref="RefreshCoreAsync"/>). Zadnej
/// kopii refresh, tokenow ani blokady.
/// </summary>
public sealed partial class SonosAccountCoordinator
{
    /// <summary>
    /// GET /groups/{groupId}/playback przez bilet biezacego konta.
    /// </summary>
    public async Task<SonosGroupReadResult<SonosGroupPlaybackStatus>> ReadGroupPlaybackAsync(
        ISonosGroupApi api,
        string? groupId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(api);
        return await RunGroupReadAsync<SonosGroupPlaybackStatus>(
            async (token, ct) =>
            {
                var outcome = await api.GetGroupPlaybackAsync(token, groupId, ct).ConfigureAwait(false);
                return (outcome.Status, outcome.Playback);
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>GET /groups/{groupId}/playbackMetadata przez bilet biezacego konta.</summary>
    public async Task<SonosGroupReadResult<SonosGroupMetadata>> ReadGroupMetadataAsync(
        ISonosGroupApi api,
        string? groupId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(api);
        return await RunGroupReadAsync<SonosGroupMetadata>(
            async (token, ct) =>
            {
                var outcome = await api.GetGroupMetadataAsync(token, groupId, ct).ConfigureAwait(false);
                return (outcome.Status, outcome.Metadata);
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>GET /groups/{groupId}/groupVolume przez bilet biezacego konta.</summary>
    public async Task<SonosGroupReadResult<SonosGroupVolume>> ReadGroupVolumeAsync(
        ISonosGroupApi api,
        string? groupId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(api);
        return await RunGroupReadAsync<SonosGroupVolume>(
            async (token, ct) =>
            {
                var outcome = await api.GetGroupVolumeAsync(token, groupId, ct).ConfigureAwait(false);
                return (outcome.Status, outcome.Volume);
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// POLECENIE grupy BEZ parametrow: play, pause, togglePlayPause,
    /// skipToNextTrack, skipToPreviousTrack. JEDEN POST, zero powtorzen.
    /// </summary>
    public async Task<SonosGroupCommandResult> SendGroupCommandAsync(
        ISonosGroupApi api,
        string? groupId,
        SonosGroupCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(api);
        return await RunGroupCommandAsync(
            command,
            (token, ct) => api.SendGroupCommandAsync(token, groupId, command, ct),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>POST playback/seek - przeskok BEZWZGLEDNY. JEDEN POST.</summary>
    public async Task<SonosGroupCommandResult> SeekAsync(
        ISonosGroupApi api,
        string? groupId,
        int positionMillis,
        string? itemId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(api);
        return await RunGroupCommandAsync(
            SonosGroupCommand.Seek,
            (token, ct) => api.SeekAsync(token, groupId, positionMillis, itemId, ct),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// POST playback/seekRelative - przeskok WZGLEDNY. Zalezy od stanu, wiec tym
    /// bardziej bez zadnego powtorzenia.
    /// </summary>
    public async Task<SonosGroupCommandResult> SeekRelativeAsync(
        ISonosGroupApi api,
        string? groupId,
        int deltaMillis,
        string? itemId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(api);
        return await RunGroupCommandAsync(
            SonosGroupCommand.SeekRelative,
            (token, ct) => api.SeekRelativeAsync(token, groupId, deltaMillis, itemId, ct),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>POST groupVolume - glosnosc BEZWZGLEDNA 0..100. JEDEN POST.</summary>
    public async Task<SonosGroupCommandResult> SetGroupVolumeAsync(
        ISonosGroupApi api,
        string? groupId,
        int volume,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(api);
        return await RunGroupCommandAsync(
            SonosGroupCommand.SetVolume,
            (token, ct) => api.SetGroupVolumeAsync(token, groupId, volume, ct),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>POST groupVolume/mute - wyciszenie JAWNE, nie przelaczane. JEDEN POST.</summary>
    public async Task<SonosGroupCommandResult> SetGroupMuteAsync(
        ISonosGroupApi api,
        string? groupId,
        bool muted,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(api);
        return await RunGroupCommandAsync(
            SonosGroupCommand.SetMute,
            (token, ct) => api.SetGroupMuteAsync(token, groupId, muted, ct),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>POST groupVolume/relative - zmiana WZGLEDNA. Bez powtorzen.</summary>
    public async Task<SonosGroupCommandResult> SetRelativeGroupVolumeAsync(
        ISonosGroupApi api,
        string? groupId,
        int volumeDelta,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(api);
        return await RunGroupCommandAsync(
            SonosGroupCommand.SetRelativeVolume,
            (token, ct) => api.SetRelativeGroupVolumeAsync(token, groupId, volumeDelta, ct),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Wspolny przebieg ODCZYTU grupy. Ten sam kontrakt co
    /// <see cref="RunReadAsync"/>: bilet pod blokada, HTTP poza blokada, jedno
    /// odnowienie przy znanej minionej waznosci, po 401 najwyzej jedno
    /// odnowienie i jedno powtorzenie, kontrola generacji po kazdym powrocie.
    /// </summary>
    private async Task<SonosGroupReadResult<TValue>> RunGroupReadAsync<TValue>(
        Func<string?, CancellationToken, Task<(SonosControlApiStatus Status, TValue? Value)>> read,
        CancellationToken cancellationToken)
        where TValue : class
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
                return new SonosGroupReadResult<TValue>(
                    SonosDeviceReadStatus.NoAccount, null, false, CreateSnapshot());
            }
        }

        var renewed = false;
        if (expired && canRenew)
        {
            var renew = await RenewForReadAsync(generation, cancellationToken).ConfigureAwait(false);
            if (renew.Terminal is { } before)
            {
                return GroupRead<TValue>(before);
            }

            renewed = renew.Renewed;
            accessToken = renew.AccessToken;
            generation = renew.Generation;
            canRenew = renew.CanRenew;
        }

        using var linked = Link(cancellationToken);
        SonosControlApiStatus status;
        TValue? value;
        try
        {
            (status, value) = await read(accessToken, linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return FinishGroupRead<TValue>(SonosDeviceReadStatus.Canceled, null, renewed, generation);
        }

        if (status != SonosControlApiStatus.Unauthorized || renewed || !canRenew)
        {
            return FinishGroupRead(SonosDeviceReadMessages.From(status), value, renewed, generation);
        }

        // DOKLADNIE JEDNO odnowienie po 401 i DOKLADNIE JEDNO powtorzenie GET.
        var after = await RenewForReadAsync(generation, cancellationToken).ConfigureAwait(false);
        if (after.Terminal is { } terminal)
        {
            return GroupRead<TValue>(terminal);
        }

        if (!after.Renewed)
        {
            return FinishGroupRead<TValue>(SonosDeviceReadStatus.Unauthorized, null, false, generation);
        }

        using var retryLink = Link(cancellationToken);
        try
        {
            (status, value) = await read(after.AccessToken, retryLink.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return FinishGroupRead<TValue>(SonosDeviceReadStatus.Canceled, null, true, after.Generation);
        }

        return FinishGroupRead(SonosDeviceReadMessages.From(status), value, true, after.Generation);
    }

    /// <summary>
    /// Wspolny przebieg POLECENIA grupy. Rozni sie od odczytu w JEDNEJ, celowej
    /// rzeczy: po wyslaniu NIE MA odnowienia ani powtorzenia. Nawet po 401
    /// zostaje dokladnie jeden POST, bo drugi POST moglby zadzialac po raz
    /// drugi, a przy poleceniu przelaczajacym albo wzglednym da inny skutek.
    /// </summary>
    private async Task<SonosGroupCommandResult> RunGroupCommandAsync(
        SonosGroupCommand command,
        Func<string?, CancellationToken, Task<SonosGroupCommandOutcome>> send,
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
                return new SonosGroupCommandResult(
                    SonosGroupOperationStatus.NoAccount, command, null,
                    requestSent: false, renewed: false, CreateSnapshot());
            }
        }

        var renewed = false;

        // ODNOWIENIE TYLKO PRZED wyslaniem i tylko przy ZNANEJ minionej waznosci.
        // Nieznana waznosc nie odnawia niczego "na wszelki wypadek".
        if (expired && canRenew)
        {
            var renew = await RenewForReadAsync(generation, cancellationToken).ConfigureAwait(false);
            if (renew.Terminal is { } before)
            {
                // Zadnego POST nie bylo: porzucenie PRZED wyslaniem.
                return CommandFromRenewal(command, before);
            }

            renewed = renew.Renewed;
            accessToken = renew.AccessToken;
            generation = renew.Generation;
        }

        using var linked = Link(cancellationToken);
        SonosGroupCommandOutcome outcome;
        try
        {
            // JEDEN POST. Koniec drogi autoryzacji dla tego polecenia.
            outcome = await send(accessToken, linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return FinishGroupCommand(command, null, requestSent: false, renewed, generation);
        }

        return FinishGroupCommand(command, outcome, outcome.Sent, renewed, generation);
    }

    /// <summary>
    /// Przelozenie terminalnego wyniku ODNOWIENIA na wynik odczytu grupy. Sam
    /// <see cref="ReadAttempt"/> nosi juz rozstrzygniecie i migawke, wiec nie
    /// powtarzamy tu jego logiki.
    /// </summary>
    private static SonosGroupReadResult<TValue> GroupRead<TValue>(ReadAttempt attempt)
        where TValue : class =>
        new(attempt.Status, null, attempt.Renewed, attempt.Snapshot);

    /// <summary>
    /// Terminalny wynik odnowienia PRZED wyslaniem polecenia: zadnego POST nie
    /// bylo, wiec RequestSent jest false i nic nie sugeruje wykonania.
    /// </summary>
    private static SonosGroupCommandResult CommandFromRenewal(SonosGroupCommand command, ReadAttempt attempt)
    {
        var status = attempt.Status switch
        {
            SonosDeviceReadStatus.Discarded => SonosGroupOperationStatus.Discarded,
            SonosDeviceReadStatus.Canceled => SonosGroupOperationStatus.Canceled,
            SonosDeviceReadStatus.NoAccount => SonosGroupOperationStatus.NoAccount,
            _ => SonosGroupOperationStatus.Unauthorized
        };

        return new SonosGroupCommandResult(
            status, command, null, requestSent: false, renewed: false, attempt.Snapshot);
    }

    /// <summary>
    /// Zamkniecie ODCZYTU grupy: wynik PORZUCONY, gdy zestaw zmienil sie w
    /// trakcie albo wlasciciel zakonczyl prace. Dane spoznione nie podmieniaja
    /// tego, co widzi uzytkownik.
    /// </summary>
    private SonosGroupReadResult<TValue> FinishGroupRead<TValue>(
        SonosDeviceReadStatus status,
        TValue? value,
        bool renewed,
        long generation)
        where TValue : class
    {
        lock (gate)
        {
            if (disposed)
            {
                return new SonosGroupReadResult<TValue>(
                    SonosDeviceReadStatus.Discarded, null, renewed, EmptySnapshot());
            }

            if (credentialGeneration != generation)
            {
                return new SonosGroupReadResult<TValue>(
                    SonosDeviceReadStatus.Discarded, null, renewed, CreateSnapshot());
            }

            return new SonosGroupReadResult<TValue>(status, value, renewed, CreateSnapshot());
        }
    }

    /// <summary>
    /// Zamkniecie POLECENIA. Porzucenie zachowuje UCZCIWE rozroznienie: jesli
    /// zadanie poszlo, mowimy o probie i NIEZNANYM skutku, a nie o cofnieciu ani
    /// o niewyslanym poleceniu. Wynik uslugi z nieaktualnego konta nie jest
    /// publikowany jako sukces.
    /// </summary>
    private SonosGroupCommandResult FinishGroupCommand(
        SonosGroupCommand command,
        SonosGroupCommandOutcome? outcome,
        bool requestSent,
        bool renewed,
        long generation)
    {
        lock (gate)
        {
            if (disposed)
            {
                return new SonosGroupCommandResult(
                    SonosGroupOperationStatus.Discarded, command, null, requestSent, renewed, EmptySnapshot());
            }

            if (credentialGeneration != generation)
            {
                return new SonosGroupCommandResult(
                    SonosGroupOperationStatus.Discarded, command, null, requestSent, renewed, CreateSnapshot());
            }

            if (outcome is null)
            {
                // Anulowano PRZED wyslaniem: zadnego zadania nie bylo.
                return new SonosGroupCommandResult(
                    SonosGroupOperationStatus.Canceled, command, null, false, renewed, CreateSnapshot());
            }

            return new SonosGroupCommandResult(
                SonosGroupOperationStatus.Attempted, command, outcome, requestSent, renewed, CreateSnapshot());
        }
    }
}
