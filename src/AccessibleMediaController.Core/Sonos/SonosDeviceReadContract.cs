using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace AccessibleMediaController.Core.Sonos;

/// <summary>
/// WASKI szew odczytu urzadzen dla koordynatora konta. Celowo NIE jest to
/// HttpClient ani caly <see cref="SonosControlApiClient"/>: koordynator ma
/// wolac dokladnie dwa GET-y i nic wiecej. Zadnej mutacji, zadnego playbacku.
///
/// Token jest ARGUMENTEM pojedynczego wywolania - implementacja nie zapisuje go
/// i nie oddaje na zewnatrz.
/// </summary>
public interface ISonosDeviceApi
{
    Task<SonosHouseholdsOutcome> GetHouseholdsAsync(string? accessToken, CancellationToken cancellationToken);

    Task<SonosGroupsOutcome> GetGroupsAsync(string? accessToken, string? householdId, CancellationToken cancellationToken);
}

/// <summary>
/// Produkcyjny adapter na ODEBRANY <see cref="SonosControlApiClient"/>. Nie
/// przejmuje wlasnosci klienta - zwalnia go ten, kto go utworzyl (wlasciciel
/// konta w aplikacji, nie okno).
/// </summary>
public sealed class SonosControlApiDeviceApi : ISonosDeviceApi
{
    private readonly SonosControlApiClient _client;

    public SonosControlApiDeviceApi(SonosControlApiClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
    }

    public Task<SonosHouseholdsOutcome> GetHouseholdsAsync(string? accessToken, CancellationToken cancellationToken) =>
        _client.GetHouseholdsAsync(accessToken, cancellationToken);

    public Task<SonosGroupsOutcome> GetGroupsAsync(string? accessToken, string? householdId, CancellationToken cancellationToken) =>
        _client.GetGroupsAsync(accessToken, householdId, cancellationToken);
}

/// <summary>
/// Wynik odczytu urzadzen WIDZIANY PRZEZ KONTO. Oddzielony od
/// <see cref="SonosControlApiStatus"/>, bo konto zna stany, ktorych transport
/// nie zna: brak konta i wynik PORZUCONY (konto zmienilo sie w trakcie).
/// </summary>
public enum SonosDeviceReadStatus
{
    /// <summary>Odpowiedz zgodna z kontraktem. Pusta lista tez jest sukcesem.</summary>
    Success,

    /// <summary>Nie ma z czym pytac: brak poswiadczen w pamieci. Zadne zapytanie nie poszlo.</summary>
    NoAccount,

    /// <summary>
    /// Wynik SPOZNIONY wobec biezacego konta (wylogowanie, nowe logowanie,
    /// zakonczenie wlasciciela). Danych NIE oddajemy - to nie jest blad uslugi.
    /// </summary>
    Discarded,

    /// <summary>Brak klucza integracji albo niepoprawny identyfikator domu - zapytanie NIE poszlo.</summary>
    InvalidConfiguration,

    /// <summary>401 takze po jednej dozwolonej probie odnowienia. NIE kasuje konta tutaj.</summary>
    Unauthorized,

    Forbidden,
    NotFound,
    RequestRejected,
    RateLimited,
    CommandFailed,
    ServiceError,
    Unreachable,
    Canceled,
    InvalidResponse,
    RedirectRefused
}

/// <summary>Stale komunikaty PL. Nigdy nie zawieraja tokenu, klucza ani ciala odpowiedzi.</summary>
public static class SonosDeviceReadMessages
{
    private static readonly IReadOnlyDictionary<SonosDeviceReadStatus, string> Texts =
        new Dictionary<SonosDeviceReadStatus, string>
        {
            [SonosDeviceReadStatus.Success] = "Lista urządzeń Sonos została odczytana.",
            [SonosDeviceReadStatus.NoAccount] =
                "Nie ma połączonego konta Sonos, więc nie ma czego odczytać.",
            [SonosDeviceReadStatus.Discarded] =
                "Odczyt urządzeń Sonos został pominięty: konto zmieniło się w trakcie. Spróbuj odświeżyć.",
            [SonosDeviceReadStatus.InvalidConfiguration] =
                "Odczyt urządzeń nie został wysłany: brak klucza integracji albo niepoprawny dom.",
            [SonosDeviceReadStatus.Unauthorized] =
                "Sonos nie przyjął dostępu do konta. Odnów dostęp albo zaloguj się ponownie.",
            [SonosDeviceReadStatus.Forbidden] = "Konto Sonos nie ma uprawnień do odczytu urządzeń.",
            [SonosDeviceReadStatus.NotFound] = "Sonos nie znalazł wskazanego domu lub grupy.",
            [SonosDeviceReadStatus.RequestRejected] = "Sonos odrzucił zapytanie o urządzenia.",
            [SonosDeviceReadStatus.RateLimited] = "Sonos chwilowo ogranicza liczbę zapytań. Spróbuj później.",
            [SonosDeviceReadStatus.CommandFailed] = "Sonos nie wykonał zapytania o urządzenia.",
            [SonosDeviceReadStatus.ServiceError] = "Usługa Sonos zgłosiła błąd.",
            [SonosDeviceReadStatus.Unreachable] = "Nie udało się połączyć z usługą Sonos.",
            [SonosDeviceReadStatus.Canceled] = "Odczyt urządzeń Sonos został anulowany.",
            [SonosDeviceReadStatus.InvalidResponse] = "Odpowiedź Sonos była niezgodna z oczekiwaną.",
            [SonosDeviceReadStatus.RedirectRefused] =
                "Sonos próbował przekierować zapytanie; zostało zatrzymane."
        };

    public static string Describe(SonosDeviceReadStatus status) =>
        Texts.TryGetValue(status, out var text) ? text : Texts[SonosDeviceReadStatus.InvalidResponse];

    /// <summary>Mapowanie 1:1 statusow transportu; konto nie zmienia ich znaczenia.</summary>
    public static SonosDeviceReadStatus From(SonosControlApiStatus status) => status switch
    {
        SonosControlApiStatus.Success => SonosDeviceReadStatus.Success,
        SonosControlApiStatus.InvalidConfiguration => SonosDeviceReadStatus.InvalidConfiguration,
        SonosControlApiStatus.Unauthorized => SonosDeviceReadStatus.Unauthorized,
        SonosControlApiStatus.Forbidden => SonosDeviceReadStatus.Forbidden,
        SonosControlApiStatus.NotFound => SonosDeviceReadStatus.NotFound,
        SonosControlApiStatus.RequestRejected => SonosDeviceReadStatus.RequestRejected,
        SonosControlApiStatus.RateLimited => SonosDeviceReadStatus.RateLimited,
        SonosControlApiStatus.CommandFailed => SonosDeviceReadStatus.CommandFailed,
        SonosControlApiStatus.ServiceError => SonosDeviceReadStatus.ServiceError,
        SonosControlApiStatus.Unreachable => SonosDeviceReadStatus.Unreachable,
        SonosControlApiStatus.Canceled => SonosDeviceReadStatus.Canceled,
        SonosControlApiStatus.RedirectRefused => SonosDeviceReadStatus.RedirectRefused,
        _ => SonosDeviceReadStatus.InvalidResponse
    };
}

/// <summary>
/// PUSTA migawka konta: stan "brak konta" bez zadnego tokenu. Jedno zrodlo dla
/// wynikow porzuconych i dla wynikow budowanych poza koordynatorem, zeby dwie
/// warstwy nie utrzymywaly dwoch kopii tej samej wartosci.
/// </summary>
public static class SonosAccountSnapshots
{
    public static SonosAccountSnapshot Empty { get; } = new(
        SonosAccountState.NoAccount,
        SonosAccountIssue.None,
        hasCredentials: false,
        hasRefreshToken: false,
        isPersisted: false,
        isAwaitingBrowser: false,
        persistedRecordMayRemain: false,
        receivedAtUtc: null,
        expiresAtUtc: null,
        isExpiryKnown: false,
        credentialGeneration: 0,
        loginGeneration: 0,
        accountBindingGeneration: 0);
}

/// <summary>Wynik odczytu DOMOW. ToString bez tokenu i klucza - tylko status i liczniki.</summary>
public sealed class SonosHouseholdsReadResult
{
    private static readonly IReadOnlyList<SonosHousehold> None =
        new ReadOnlyCollection<SonosHousehold>(new List<SonosHousehold>());

    internal SonosHouseholdsReadResult(
        SonosDeviceReadStatus status,
        IReadOnlyList<SonosHousehold>? households,
        bool renewed,
        SonosAccountSnapshot snapshot,
        string? message = null)
    {
        Status = status;
        Households = households ?? None;
        Renewed = renewed;
        Snapshot = snapshot;
        ownMessage = message;
    }

    private readonly string? ownMessage;

    public SonosDeviceReadStatus Status { get; }

    public IReadOnlyList<SonosHousehold> Households { get; }

    /// <summary>Czy po drodze uzyto ISTNIEJACEGO odnowienia dostepu (najwyzej raz).</summary>
    public bool Renewed { get; }

    public SonosAccountSnapshot Snapshot { get; }

    public bool Succeeded => Status == SonosDeviceReadStatus.Success;

    /// <summary>Wynik PORZUCONY nie podmienia listy w oknie.</summary>
    public bool Discarded => Status == SonosDeviceReadStatus.Discarded;

    public string Message => ownMessage ?? SonosDeviceReadMessages.Describe(Status);

    /// <summary>
    /// Wynik UDANY. Publiczna fabryka, zeby warstwa okna i jej pomiary mogly
    /// budowac wynik bez dostepu do wnetrza koordynatora.
    /// </summary>
    public static SonosHouseholdsReadResult Success(IReadOnlyList<SonosHousehold> households) =>
        new(SonosDeviceReadStatus.Success, households, false, SonosAccountSnapshots.Empty);

    /// <summary>Wynik NIEUDANY z wlasnym, jawnym komunikatem PL.</summary>
    public static SonosHouseholdsReadResult Failure(SonosDeviceReadStatus status, string? message = null) =>
        new(status, null, false, SonosAccountSnapshots.Empty, message);

    public override string ToString() =>
        "Odczyt domów Sonos: " + Status
        + ", domów " + Households.Count.ToString(CultureInfo.InvariantCulture)
        + (Renewed ? ", po odnowieniu dostępu" : string.Empty);
}

/// <summary>Wynik odczytu GRUP i GLOSNIKOW jednego domu.</summary>
public sealed class SonosGroupsReadResult
{
    internal SonosGroupsReadResult(
        SonosDeviceReadStatus status,
        SonosHouseholdTopology? topology,
        bool renewed,
        SonosAccountSnapshot snapshot,
        string? message = null)
    {
        Status = status;
        Topology = topology;
        Renewed = renewed;
        Snapshot = snapshot;
        ownMessage = message;
    }

    private readonly string? ownMessage;

    public SonosDeviceReadStatus Status { get; }

    public SonosHouseholdTopology? Topology { get; }

    public bool Renewed { get; }

    public SonosAccountSnapshot Snapshot { get; }

    public bool Succeeded => Status == SonosDeviceReadStatus.Success && Topology is not null;

    public bool Discarded => Status == SonosDeviceReadStatus.Discarded;

    public string Message => ownMessage ?? SonosDeviceReadMessages.Describe(Status);

    public static SonosGroupsReadResult Success(SonosHouseholdTopology topology) =>
        new(SonosDeviceReadStatus.Success, topology, false, SonosAccountSnapshots.Empty);

    public static SonosGroupsReadResult Failure(SonosDeviceReadStatus status, string? message = null) =>
        new(status, null, false, SonosAccountSnapshots.Empty, message);

    public override string ToString() =>
        "Odczyt grup Sonos: " + Status
        + ", grup " + (Topology?.Groups.Count ?? 0).ToString(CultureInfo.InvariantCulture)
        + ", głośników " + (Topology?.Players.Count ?? 0).ToString(CultureInfo.InvariantCulture)
        + (Topology?.Partial == true ? ", lista NIEPEŁNA" : string.Empty);
}
