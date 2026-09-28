using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AccessibleMediaController.Core.Sonos;

/// <summary>
/// WASKI szew AUTORYZOWANYCH operacji GRUPY dla koordynatora konta. Analogicznie
/// do <see cref="ISonosDeviceApi"/>: celowo NIE jest to HttpClient ani caly
/// <see cref="SonosControlApiClient"/>. Sa tu DOKLADNIE trzy odczyty i polecenia
/// grupy - nic wiecej: zadnego pollingu, sesji odtwarzania, kolejki, ulubionych
/// ani EQ.
///
/// Token jest ARGUMENTEM pojedynczego wywolania; implementacja go nie zapisuje
/// i nie oddaje na zewnatrz.
/// </summary>
public interface ISonosGroupApi
{
    Task<SonosGroupPlaybackOutcome> GetGroupPlaybackAsync(
        string? accessToken, string? groupId, CancellationToken cancellationToken);

    Task<SonosGroupMetadataOutcome> GetGroupMetadataAsync(
        string? accessToken, string? groupId, CancellationToken cancellationToken);

    Task<SonosGroupVolumeOutcome> GetGroupVolumeAsync(
        string? accessToken, string? groupId, CancellationToken cancellationToken);

    Task<SonosGroupCommandOutcome> SendGroupCommandAsync(
        string? accessToken, string? groupId, SonosGroupCommand command, CancellationToken cancellationToken);

    Task<SonosGroupCommandOutcome> SeekAsync(
        string? accessToken, string? groupId, int positionMillis, string? itemId, CancellationToken cancellationToken);

    Task<SonosGroupCommandOutcome> SeekRelativeAsync(
        string? accessToken, string? groupId, int deltaMillis, string? itemId, CancellationToken cancellationToken);

    Task<SonosGroupCommandOutcome> SetGroupVolumeAsync(
        string? accessToken, string? groupId, int volume, CancellationToken cancellationToken);

    Task<SonosGroupCommandOutcome> SetGroupMuteAsync(
        string? accessToken, string? groupId, bool muted, CancellationToken cancellationToken);

    Task<SonosGroupCommandOutcome> SetRelativeGroupVolumeAsync(
        string? accessToken, string? groupId, int volumeDelta, CancellationToken cancellationToken);
}

/// <summary>
/// CIENKI adapter na ODEBRANY <see cref="SonosControlApiClient"/> - bez wlasnej
/// logiki, bez zapamietywania tokenu i bez ponowien. Nie przejmuje wlasnosci
/// klienta: zwalnia go ten, kto go utworzyl.
/// </summary>
public sealed class SonosControlApiGroupApi : ISonosGroupApi
{
    private readonly SonosControlApiClient _client;

    public SonosControlApiGroupApi(SonosControlApiClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
    }

    public Task<SonosGroupPlaybackOutcome> GetGroupPlaybackAsync(
        string? accessToken, string? groupId, CancellationToken cancellationToken) =>
        _client.GetGroupPlaybackAsync(accessToken, groupId, cancellationToken);

    public Task<SonosGroupMetadataOutcome> GetGroupMetadataAsync(
        string? accessToken, string? groupId, CancellationToken cancellationToken) =>
        _client.GetGroupMetadataAsync(accessToken, groupId, cancellationToken);

    public Task<SonosGroupVolumeOutcome> GetGroupVolumeAsync(
        string? accessToken, string? groupId, CancellationToken cancellationToken) =>
        _client.GetGroupVolumeAsync(accessToken, groupId, cancellationToken);

    public Task<SonosGroupCommandOutcome> SendGroupCommandAsync(
        string? accessToken, string? groupId, SonosGroupCommand command, CancellationToken cancellationToken) =>
        _client.SendGroupCommandAsync(accessToken, groupId, command, cancellationToken);

    public Task<SonosGroupCommandOutcome> SeekAsync(
        string? accessToken, string? groupId, int positionMillis, string? itemId, CancellationToken cancellationToken) =>
        _client.SeekAsync(accessToken, groupId, positionMillis, itemId, cancellationToken);

    public Task<SonosGroupCommandOutcome> SeekRelativeAsync(
        string? accessToken, string? groupId, int deltaMillis, string? itemId, CancellationToken cancellationToken) =>
        _client.SeekRelativeAsync(accessToken, groupId, deltaMillis, itemId, cancellationToken);

    public Task<SonosGroupCommandOutcome> SetGroupVolumeAsync(
        string? accessToken, string? groupId, int volume, CancellationToken cancellationToken) =>
        _client.SetGroupVolumeAsync(accessToken, groupId, volume, cancellationToken);

    public Task<SonosGroupCommandOutcome> SetGroupMuteAsync(
        string? accessToken, string? groupId, bool muted, CancellationToken cancellationToken) =>
        _client.SetGroupMuteAsync(accessToken, groupId, muted, cancellationToken);

    public Task<SonosGroupCommandOutcome> SetRelativeGroupVolumeAsync(
        string? accessToken, string? groupId, int volumeDelta, CancellationToken cancellationToken) =>
        _client.SetRelativeGroupVolumeAsync(accessToken, groupId, volumeDelta, cancellationToken);
}

/// <summary>
/// Wynik ODCZYTU grupy WIDZIANY PRZEZ KONTO. Status transportu zostaje
/// nietkniety (<see cref="SonosDeviceReadStatus"/> mapuje go 1:1); konto dodaje
/// wlasne stany, ktorych transport nie zna: brak konta i wynik PORZUCONY.
///
/// Tokenow tu nie ma i byc nie moze: na zewnatrz wychodza wylacznie dane
/// odczytu, flaga <see cref="Renewed"/> i bezpieczna migawka konta.
/// </summary>
public sealed class SonosGroupReadResult<TValue>
    where TValue : class
{
    internal SonosGroupReadResult(
        SonosDeviceReadStatus status,
        TValue? value,
        bool renewed,
        SonosAccountSnapshot snapshot)
    {
        Status = status;
        Value = value;
        Renewed = renewed;
        Snapshot = snapshot;
    }

    public SonosDeviceReadStatus Status { get; }

    /// <summary>Dane TYLKO przy sukcesie. Wynik porzucony niczego nie oddaje.</summary>
    public TValue? Value { get; }

    /// <summary>Czy po drodze uzyto ISTNIEJACEGO odnowienia dostepu (najwyzej raz).</summary>
    public bool Renewed { get; }

    public SonosAccountSnapshot Snapshot { get; }

    public bool Succeeded => Status == SonosDeviceReadStatus.Success && Value is not null;

    /// <summary>Wynik SPOZNIONY: nie podmienia tego, co widzi uzytkownik.</summary>
    public bool Discarded => Status == SonosDeviceReadStatus.Discarded;

    public string Message => SonosDeviceReadMessages.Describe(Status);

    public override string ToString() =>
        "Odczyt grupy Sonos: " + Status
        + (Value is null ? ", brak danych" : ", dane odczytane")
        + (Renewed ? ", po odnowieniu dostępu" : string.Empty);
}

/// <summary>
/// Co KONTO zrobilo z poleceniem grupy. To NIE jest status HTTP i nie jest
/// skutek polecenia - te trzy rzeczy sa tu celowo rozdzielone.
/// </summary>
public enum SonosGroupOperationStatus
{
    /// <summary>Brak poswiadczen w pamieci: zadne zadanie NIE poszlo.</summary>
    NoAccount,

    /// <summary>
    /// Polecenie zostalo PODJETE: jest wynik warstwy transportu
    /// (<see cref="SonosGroupCommandResult.Outcome"/>). Nadal nie mowi to nic o
    /// SKUTKU - HTTP 200 jest tylko przyjeciem zlecenia.
    /// </summary>
    Attempted,

    /// <summary>
    /// Konto zmienilo sie w trakcie (Disconnect, nowe logowanie, zwolnienie
    /// wlasciciela). Dane i komunikat sukcesu NIE ida do uzytkownika. Jesli
    /// zadanie JUZ poszlo, jego skutek pozostaje NIEZNANY - porzucenie nie jest
    /// cofnieciem.
    /// </summary>
    Discarded,

    /// <summary>Anulowano PRZED wyslaniem zadania: nic nie poszlo.</summary>
    Canceled,

    /// <summary>
    /// 401 NA WEJSCIU, gdy konta nie da sie odnowic (odnowienie skonczylo sie
    /// wylogowaniem). Nie kasuje konta tutaj - o tym decyduje wylacznie
    /// istniejaca sciezka odnawiania.
    /// </summary>
    Unauthorized
}

/// <summary>
/// Stale komunikaty PL dla WYNIKU KONTA wobec polecenia grupy. Osobne od
/// slownika transportu (<see cref="SonosGroupCommandMessages"/>) i od slownika
/// odczytu: opisuja INNE zdarzenie - co konto zrobilo ze zleceniem.
/// Zaden tekst nie zawiera tokenu, klucza integracji ani ciala odpowiedzi.
/// </summary>
public static class SonosGroupOperationMessages
{
    /// <summary>
    /// Porzucenie PO wyslaniu zadania. Nie oglasza cofniecia i nie twierdzi,
    /// ze polecenia nie bylo: zadanie moglo sie wykonac, tylko wynik jest
    /// nieaktualny wobec biezacego konta.
    /// </summary>
    public const string DiscardedAfterSendText =
        "Konto Sonos zmieniło się po wysłaniu polecenia: skutek nieznany i niepotwierdzony. "
        + "Sprawdź stan odtwarzania.";

    private static readonly IReadOnlyDictionary<SonosGroupOperationStatus, string> Texts =
        new Dictionary<SonosGroupOperationStatus, string>
        {
            [SonosGroupOperationStatus.NoAccount] =
                "Nie ma połączonego konta Sonos, więc polecenie nie zostało wysłane.",
            [SonosGroupOperationStatus.Attempted] =
                "Polecenie Sonos zostało podjęte; wynik opisuje odpowiedź usługi.",
            [SonosGroupOperationStatus.Discarded] =
                "Konto Sonos zmieniło się w trakcie: skutek polecenia nieznany i niepotwierdzony.",
            [SonosGroupOperationStatus.Canceled] =
                "Polecenie Sonos nie zostało wysłane: anulowano przed wysłaniem.",
            [SonosGroupOperationStatus.Unauthorized] =
                "Sonos nie przyjął dostępu do konta, więc polecenie nie zostało wysłane. Zaloguj się ponownie."
        };

    public static string Describe(SonosGroupOperationStatus status) =>
        Texts.TryGetValue(status, out var text) ? text : Texts[SonosGroupOperationStatus.Discarded];
}

/// <summary>
/// Wynik POLECENIA grupy WIDZIANY PRZEZ KONTO. Rozdziela trzy rozne rzeczy,
/// ktorych mylenie jest zwyklym zrodlem falszywych komunikatow:
///  * co zrobilo KONTO (<see cref="Status"/>): brak konta, proba, porzucenie,
///  * czy zadanie w ogole POSZLO (<see cref="RequestSent"/>),
///  * co odpowiedziala USLUGA (<see cref="Outcome"/>) - i to nadal NIE jest
///    dowod skutku (<see cref="EffectConfirmed"/> jest zawsze false).
///
/// Weryfikacja skutku to OSOBNA, jawna operacja odczytu. Tu jej nie ma i zaden
/// tekst jej nie udaje.
/// </summary>
public sealed class SonosGroupCommandResult
{
    internal SonosGroupCommandResult(
        SonosGroupOperationStatus status,
        SonosGroupCommand command,
        SonosGroupCommandOutcome? outcome,
        bool requestSent,
        bool renewed,
        SonosAccountSnapshot snapshot)
    {
        Status = status;
        Command = command;
        Outcome = outcome;
        RequestSent = requestSent;
        Renewed = renewed;
        Snapshot = snapshot;
    }

    public SonosGroupOperationStatus Status { get; }

    public SonosGroupCommand Command { get; }

    /// <summary>
    /// Wynik warstwy transportu, gdy polecenie zostalo podjete. Null znaczy, ze
    /// konto odrzucilo zlecenie przed wyslaniem albo wynik zostal porzucony.
    /// </summary>
    public SonosGroupCommandOutcome? Outcome { get; }

    /// <summary>
    /// Czy zadanie HTTP opuscilo aplikacje. True takze przy porzuceniu - i
    /// wlasnie dlatego porzucenie nie oglasza cofniecia.
    /// </summary>
    public bool RequestSent { get; }

    /// <summary>Czy PRZED wyslaniem uzyto jednego odnowienia dostepu.</summary>
    public bool Renewed { get; }

    public SonosAccountSnapshot Snapshot { get; }

    /// <summary>Sonos PRZYJAL polecenie. NIE jest to potwierdzenie skutku.</summary>
    public bool Accepted => Status == SonosGroupOperationStatus.Attempted && Outcome?.Accepted == true;

    /// <summary>
    /// Zawsze false: ta warstwa nigdy nie potwierdza skutku polecenia. Nazwane
    /// wprost, zeby zaden wolajacy nie ogloszil wykonania po HTTP 200.
    /// </summary>
    public bool EffectConfirmed => false;

    /// <summary>
    /// Skutek NIEROZSTRZYGNIETY: zadanie poszlo, a wynik nie jest rozstrzygajacy
    /// - albo transport zglosil to wprost, albo konto porzucilo JUZ WYSLANE
    /// polecenie. W obu razach nie wolno ponawiac ani oglaszac wykonania.
    /// </summary>
    public bool EffectAmbiguous =>
        (Status == SonosGroupOperationStatus.Discarded && RequestSent)
        || Outcome?.EffectAmbiguous == true;

    /// <summary>Czy warto, by warstwa wyzej zrobila PÓZNIEJ jawny odczyt stanu.</summary>
    public bool StateReadRecommended => Accepted || EffectAmbiguous;

    /// <summary>
    /// Tekst dla uzytkownika. Porzucenie PO wyslaniu ma wlasne zdanie o
    /// nieznanym skutku; porzucenie PRZED wyslaniem mowi o niewyslanym
    /// poleceniu. Przy zwyklej probie glos ma slownik transportu, bo to on zna
    /// odpowiedz uslugi.
    /// </summary>
    public string Message => Status switch
    {
        SonosGroupOperationStatus.Discarded when RequestSent => SonosGroupOperationMessages.DiscardedAfterSendText,
        SonosGroupOperationStatus.Attempted when Outcome is not null => Outcome.Message,
        _ => SonosGroupOperationMessages.Describe(Status)
    };

    public override string ToString() =>
        "Polecenie grupy Sonos przez konto: " + Command + ", " + Status
        + (RequestSent ? ", żądanie wysłane" : ", żądania nie wysłano")
        + (Accepted ? ", przyjęte (skutek niepotwierdzony)" : string.Empty)
        + (EffectAmbiguous ? ", skutek nieznany" : string.Empty)
        + (Renewed ? ", po odnowieniu dostępu" : string.Empty);
}
