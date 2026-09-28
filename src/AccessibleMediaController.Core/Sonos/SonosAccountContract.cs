using System;
using System.Collections.Generic;
using System.Globalization;

namespace AccessibleMediaController.Core.Sonos;

/// <summary>
/// STANY konta Sonos widoczne na zewnatrz koordynatora. Sa ROZDZIELNE: inny stan
/// znaczy inne dzialanie uzytkownika, a nie inny odcien tego samego bledu.
/// </summary>
public enum SonosAccountState
{
    /// <summary>Nie ma zapisanego logowania. To NIE jest blad (Missing z magazynu).</summary>
    NoAccount,

    /// <summary>
    /// Rozpoczeto logowanie i czekamy, az uzytkownik skonczy je w przegladarce.
    /// Koordynator NIE uruchamia przegladarki - to zadanie warstwy UI.
    /// </summary>
    AwaitingBrowser,

    /// <summary>
    /// Mamy uzyteczny zestaw poswiadczen w pamieci. Uwaga: to NIE znaczy, ze
    /// zestaw jest utrwalony - o tym mowi <see cref="SonosAccountSnapshot.IsPersisted"/>.
    /// </summary>
    Connected,

    /// <summary>Trzeba przejsc logowanie od nowa (401 reauthorization, nieczytelny/obcy zapis).</summary>
    NeedsLogin,

    /// <summary>Blad MAGAZYNU, ktory nie jest wyrokiem o waznosci tokenow (odczyt/usuniecie).</summary>
    StoreFailure
}

/// <summary>
/// Rozpoznana PRZYCZYNA biezacego stanu. Osobno od stanu, bo ten sam stan moze
/// wynikac z bledu dysku albo z decyzji dostawcy, a komunikat i dalsze kroki sa inne.
/// </summary>
public enum SonosAccountIssue
{
    None,

    /// <summary>Nie udalo sie odczytac zapisu (blokada, uprawnienia, I/O). Plik NIE zostal ruszony.</summary>
    ReadFailure,

    /// <summary>Zapis istnieje, ale nie jest naszym poprawnym rekordem. NIE kasujemy go przy odczycie.</summary>
    InvalidStoredRecord,

    /// <summary>Zapis nalezy do INNEGO skonfigurowanego brokera. Nie przelaczamy adresu.</summary>
    BrokerMismatch,

    /// <summary>
    /// Trwaly zapis NOWEGO, poprawnego zestawu sie nie udal. Zestaw zostaje W PAMIECI
    /// jako niezapisany; NIE wracamy do starego refresh tokenu, bo mogl juz stracic waznosc.
    /// </summary>
    WriteFailure,

    /// <summary>
    /// Rekord nie przeszedl ISTNIEJACEJ polityki zapisu. To NIE awaria dysku: takiego
    /// rekordu nie wolno zainstalowac jako dzialajacego konta.
    /// </summary>
    InvalidRecord,

    /// <summary>Dokladne 401 reauthorization_required od brokera dla BIEZACEJ generacji.</summary>
    Reauthorization,

    /// <summary>Usuniecie zapisu sie nie udalo - nie wolno udawac udanego wylogowania.</summary>
    DeleteFailure
}

/// <summary>Stale, bezpieczne komunikaty konta. Nigdy nie cytuja tokenu, scope, origin ani sciezki.</summary>
public static class SonosAccountMessages
{
    private static readonly IReadOnlyDictionary<SonosAccountState, string> StateTexts =
        new Dictionary<SonosAccountState, string>
        {
            [SonosAccountState.NoAccount] = "Konto Sonos nie jest połączone.",
            [SonosAccountState.AwaitingBrowser] = "Dokończ logowanie Sonos w przeglądarce.",
            [SonosAccountState.Connected] = "Konto Sonos jest połączone.",
            [SonosAccountState.NeedsLogin] = "Konto Sonos wymaga ponownego zalogowania.",
            [SonosAccountState.StoreFailure] = "Zapisane logowanie Sonos jest niedostępne."
        };

    private static readonly IReadOnlyDictionary<SonosAccountIssue, string> IssueTexts =
        new Dictionary<SonosAccountIssue, string>
        {
            [SonosAccountIssue.None] = "",
            [SonosAccountIssue.ReadFailure] = "Nie udało się odczytać zapisanego logowania Sonos.",
            [SonosAccountIssue.InvalidStoredRecord] =
                "Zapisane logowanie Sonos jest nieczytelne lub w nieobsługiwanym formacie; nie zostało usunięte.",
            [SonosAccountIssue.BrokerMismatch] =
                "Zapisane logowanie Sonos należy do innego serwera logowania; nie zostało usunięte.",
            [SonosAccountIssue.WriteFailure] =
                "Nowe logowanie Sonos działa, ale nie udało się go zapisać. Możesz ponowić sam zapis.",
            [SonosAccountIssue.InvalidRecord] =
                "Otrzymane logowanie Sonos nie spełnia wymagań zapisu; nie zostało przyjęte jako konto.",
            [SonosAccountIssue.Reauthorization] = "Sonos wymaga ponownego zalogowania.",
            [SonosAccountIssue.DeleteFailure] =
                "Nie udało się usunąć zapisanego logowania Sonos; wylogowanie nie zostało potwierdzone."
        };

    public static string Describe(SonosAccountState state) =>
        StateTexts.TryGetValue(state, out var text) ? text : StateTexts[SonosAccountState.StoreFailure];

    public static string Describe(SonosAccountIssue issue) =>
        IssueTexts.TryGetValue(issue, out var text) ? text : IssueTexts[SonosAccountIssue.None];
}

/// <summary>
/// BEZPIECZNA, niemutowalna migawka stanu konta. Zamiast zdarzenia frameworka
/// koordynator udostepnia wlasnie migawke: mozna ja odczytac poza blokada i
/// odrzucic jako stara po <see cref="CredentialGeneration"/>.
///
/// Migawka NIE zawiera tokenow, scope, origin ani identyfikatora sesji - ani w
/// polach, ani w <see cref="ToString"/>.
/// </summary>
public sealed class SonosAccountSnapshot
{
    internal SonosAccountSnapshot(
        SonosAccountState state,
        SonosAccountIssue issue,
        bool hasCredentials,
        bool hasRefreshToken,
        bool isPersisted,
        bool isAwaitingBrowser,
        bool persistedRecordMayRemain,
        DateTimeOffset? receivedAtUtc,
        DateTimeOffset? expiresAtUtc,
        bool isExpiryKnown,
        long credentialGeneration,
        long loginGeneration,
        long accountBindingGeneration)
    {
        State = state;
        Issue = issue;
        HasCredentials = hasCredentials;
        HasRefreshToken = hasRefreshToken;
        IsPersisted = isPersisted;
        IsAwaitingBrowser = isAwaitingBrowser;
        PersistedRecordMayRemain = persistedRecordMayRemain;
        ReceivedAtUtc = receivedAtUtc;
        ExpiresAtUtc = expiresAtUtc;
        IsExpiryKnown = isExpiryKnown;
        CredentialGeneration = credentialGeneration;
        LoginGeneration = loginGeneration;
        AccountBindingGeneration = accountBindingGeneration;
    }

    public SonosAccountState State { get; }

    public SonosAccountIssue Issue { get; }

    public bool HasCredentials { get; }

    public bool HasRefreshToken { get; }

    /// <summary>Czy biezacy zestaw jest UTRWALONY. Fałsz przy zachowanym, niezapisanym zestawie.</summary>
    public bool IsPersisted { get; }

    /// <summary>
    /// Czy trwa proba logowania oczekujaca na przegladarke. NIEZALEZNE od
    /// <see cref="State"/>: mozna byc polaczonym i jednoczesnie logowac sie na nowo.
    /// </summary>
    public bool IsAwaitingBrowser { get; }

    /// <summary>
    /// Czy trwaly zapis MOZE nadal istniec na dysku wbrew naszej decyzji (nieudane
    /// Delete). Jawny stan zamiast cichego zalozenia, ze plik zniknal.
    /// </summary>
    public bool PersistedRecordMayRemain { get; }

    /// <summary>Moment OTRZYMANIA biezacego zestawu w UTC, dokladnie jak zapisany/odebrany.</summary>
    public DateTimeOffset? ReceivedAtUtc { get; }

    /// <summary>Wyliczony termin waznosci albo null, gdy broker nie podal expires_in.</summary>
    public DateTimeOffset? ExpiresAtUtc { get; }

    /// <summary>Czy w ogole da sie wyliczyc termin waznosci. Nieznany zostaje nieznany.</summary>
    public bool IsExpiryKnown { get; }

    /// <summary>Generacja ZESTAWU POSWIADCZEN. Rozna od generacji proby logowania.</summary>
    public long CredentialGeneration { get; }

    /// <summary>Generacja PROBY LOGOWANIA. Rosnie tylko przy rozpoczeciu/anulowaniu proby.</summary>
    public long LoginGeneration { get; }

    /// <summary>
    /// Znacznik LOKALNEGO CYKLU PODLACZENIA konta. Rosnie tylko wtedy, gdy konto
    /// zostalo ZASTAPIONE (udane nowe logowanie instalujace zestaw) albo REALNIE
    /// ODLACZONE (jawne wylogowanie, usuniecie po dokladnym 401 biezacej generacji,
    /// odrzucenie rekordu kasujace dotychczasowe konto). ZWYKLE odnowienie dostepu
    /// i rotacja zestawu go NIE zmieniaja, choc
    /// <see cref="CredentialGeneration"/> zgodnie ze starym kontraktem rosnie.
    ///
    /// To NIE identyfikator uzytkownika ani niczego po stronie Sonosa: sam licznik,
    /// bez tokenow, scope, origin i proof. Nie jest zapisywany w AppSettings, DPAPI
    /// ani pliku konta, wiec jego zakres zycia to JEDNA instancja koordynatora -
    /// miedzy procesami nie jest stabilny. Konsument porownuje PIERWSZA odczytana
    /// migawke jako punkt odniesienia: pierwszy odczyt po starcie nie jest zmiana
    /// wczesniejszego konta.
    /// </summary>
    public long AccountBindingGeneration { get; }

    /// <summary>Czy ma sens ponowienie SAMEGO zapisu, bez zadnego zapytania HTTP.</summary>
    public bool CanRetryPersist => HasCredentials && !IsPersisted;

    public string Message => SonosAccountMessages.Describe(State);

    public string IssueMessage => SonosAccountMessages.Describe(Issue);

    public override string ToString() =>
        "Konto Sonos (wartości ukryte): stan "
        + State
        + ", przyczyna "
        + Issue
        + ", poświadczenia "
        + (HasCredentials ? "w pamięci" : "brak")
        + ", zapisane "
        + (IsPersisted ? "tak" : "nie")
        + ", token odświeżania "
        + (HasRefreshToken ? "obecny" : "brak")
        + ", ważność "
        + (IsExpiryKnown ? "wyliczalna" : "nieznana")
        + ", generacja zestawu "
        + CredentialGeneration.ToString(CultureInfo.InvariantCulture)
        + ".";
}

/// <summary>Wynik JEDNORAZOWEGO odtworzenia konta z trwalego magazynu.</summary>
public sealed class SonosAccountRestoreResult
{
    internal SonosAccountRestoreResult(bool performed, SonosCredentialReadStatus storeStatus, SonosAccountSnapshot snapshot)
    {
        Performed = performed;
        StoreStatus = storeStatus;
        Snapshot = snapshot;
    }

    /// <summary>Czy TO wywolanie faktycznie czytalo magazyn. Odtworzenie jest jednorazowe.</summary>
    public bool Performed { get; }

    public SonosCredentialReadStatus StoreStatus { get; }

    public SonosAccountSnapshot Snapshot { get; }

    public override string ToString() =>
        "Odtworzenie konta Sonos: " + (Performed ? "wykonane" : "pominięte") + ", " + StoreStatus + ", " + Snapshot;
}

/// <summary>Wynik ROZPOCZECIA logowania. Nie uruchamia przegladarki i nic nie zapisuje.</summary>
public sealed class SonosAccountLoginStartResult
{
    internal SonosAccountLoginStartResult(
        SonosLoginStatus loginStatus,
        bool started,
        bool discarded,
        Uri? authorizeUri,
        SonosAccountSnapshot snapshot)
    {
        LoginStatus = loginStatus;
        Started = started;
        Discarded = discarded;
        AuthorizeUri = authorizeUri;
        Snapshot = snapshot;
    }

    public SonosLoginStatus LoginStatus { get; }

    public bool Started { get; }

    /// <summary>Odpowiedz SPOZNIONA wobec nowszej proby albo anulowania - nic nie zainstalowano.</summary>
    public bool Discarded { get; }

    /// <summary>ZAUFANY adres autoryzacji Sonos do otwarcia przez WARSTWE UI.</summary>
    public Uri? AuthorizeUri { get; }

    public SonosAccountSnapshot Snapshot { get; }

    public string Message => SonosLoginMessages.Describe(LoginStatus);

    public override string ToString() =>
        "Rozpoczęcie logowania Sonos: " + LoginStatus + (Discarded ? ", odrzucone jako spóźnione" : "") + ", " + Snapshot;
}

/// <summary>Wynik JEDNORAZOWEGO sprawdzenia logowania (jedno wywolanie Fetch).</summary>
public sealed class SonosAccountLoginCheckResult
{
    internal SonosAccountLoginCheckResult(
        SonosLoginStatus loginStatus,
        bool connected,
        bool stillWaiting,
        bool discarded,
        bool hadPendingLogin,
        SonosCredentialWriteStatus? writeStatus,
        SonosAccountSnapshot snapshot)
    {
        LoginStatus = loginStatus;
        Connected = connected;
        StillWaiting = stillWaiting;
        Discarded = discarded;
        HadPendingLogin = hadPendingLogin;
        WriteStatus = writeStatus;
        Snapshot = snapshot;
    }

    public SonosLoginStatus LoginStatus { get; }

    public bool Connected { get; }

    /// <summary>Proba pozostaje czynna: wynik jest Pending albo jego odbior mozna ponowic po bledzie przejsciowym.</summary>
    public bool StillWaiting { get; }

    public bool Discarded { get; }

    public bool HadPendingLogin { get; }

    /// <summary>Wynik trwalego zapisu, gdy logowanie sie udalo. Null, gdy zapisu nie bylo.</summary>
    public SonosCredentialWriteStatus? WriteStatus { get; }

    public SonosAccountSnapshot Snapshot { get; }

    public string Message => SonosLoginMessages.Describe(LoginStatus);

    public override string ToString() =>
        "Sprawdzenie logowania Sonos: " + LoginStatus + (Discarded ? ", odrzucone jako spóźnione" : "") + ", " + Snapshot;
}

/// <summary>Wynik JEDNEJ proby odnowienia dostepu widziany przez WOLAJACEGO.</summary>
public sealed class SonosAccountRefreshResult
{
    internal SonosAccountRefreshResult(
        SonosRefreshStatus refreshStatus,
        bool renewed,
        bool discarded,
        bool joined,
        bool waiterCanceled,
        SonosCredentialWriteStatus? writeStatus,
        SonosAccountSnapshot snapshot)
    {
        RefreshStatus = refreshStatus;
        Renewed = renewed;
        Discarded = discarded;
        Joined = joined;
        WaiterCanceled = waiterCanceled;
        WriteStatus = writeStatus;
        Snapshot = snapshot;
    }

    public SonosRefreshStatus RefreshStatus { get; }

    /// <summary>Czy w pamieci stoi NOWY zestaw z tej proby.</summary>
    public bool Renewed { get; }

    /// <summary>
    /// Odpowiedz dotyczyla STARSZEJ generacji zestawu - nic nie zapisano i NICZEGO
    /// nie skasowano.
    /// </summary>
    public bool Discarded { get; }

    /// <summary>Ten wolajacy DOLACZYL do trwajacego zapytania, zamiast wysylac drugie.</summary>
    public bool Joined { get; }

    /// <summary>
    /// TEN wolajacy zrezygnowal z czekania. Wspolne zapytanie idzie dalej dla
    /// pozostalych - anulowanie jednego nie anuluje operacji innym.
    /// </summary>
    public bool WaiterCanceled { get; }

    public SonosCredentialWriteStatus? WriteStatus { get; }

    public SonosAccountSnapshot Snapshot { get; }

    public string Message => SonosRefreshMessages.Describe(RefreshStatus);

    public override string ToString() =>
        "Odnowienie konta Sonos: " + RefreshStatus
        + (Discarded ? ", odrzucone jako spóźnione" : "")
        + (Joined ? ", dołączone do trwającego" : "")
        + ", " + Snapshot;
}

/// <summary>Wynik PONOWIENIA samego zapisu. Bez zadnego zapytania HTTP.</summary>
public sealed class SonosAccountPersistRetryResult
{
    internal SonosAccountPersistRetryResult(
        bool attempted,
        SonosCredentialWriteStatus? writeStatus,
        SonosAccountSnapshot snapshot)
    {
        Attempted = attempted;
        WriteStatus = writeStatus;
        Snapshot = snapshot;
    }

    public bool Attempted { get; }

    public SonosCredentialWriteStatus? WriteStatus { get; }

    public bool Succeeded => WriteStatus == SonosCredentialWriteStatus.Success;

    public SonosAccountSnapshot Snapshot { get; }

    public override string ToString() =>
        "Ponowienie zapisu konta Sonos: " + (Attempted ? WriteStatus?.ToString() ?? "brak" : "niepotrzebne") + ", " + Snapshot;
}

/// <summary>Wynik JAWNEGO wylogowania.</summary>
public sealed class SonosAccountDisconnectResult
{
    internal SonosAccountDisconnectResult(bool disconnected, bool deleteFailed, SonosAccountSnapshot snapshot)
    {
        Disconnected = disconnected;
        DeleteFailed = deleteFailed;
        Snapshot = snapshot;
    }

    /// <summary>Prawda TYLKO wtedy, gdy trwaly zapis rzeczywiscie zniknal.</summary>
    public bool Disconnected { get; }

    public bool DeleteFailed { get; }

    public SonosAccountSnapshot Snapshot { get; }

    public override string ToString() =>
        "Wylogowanie Sonos: " + (Disconnected ? "wykonane" : "NIEPOTWIERDZONE") + ", " + Snapshot;
}
