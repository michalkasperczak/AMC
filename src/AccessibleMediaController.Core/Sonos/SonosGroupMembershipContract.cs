using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

namespace AccessibleMediaController.Core.Sonos;

/// <summary>
/// ZMIANA SKLADU GRUP SONOSA - kontrakt DWOCH operacji zapisu: createGroup i
/// setGroupMembers.
///
/// Zrodlo: oficjalna definicja OpenAPI 3.0.3 "Sonos Control API (cloud)"
/// v1.56.0-alpha.1-1-gc264f93f-production-cloud:
///  * Groups-CreateGroup-HouseholdId: POST /households/{householdId}/groups/createGroup,
///    cialo Groups-CreateGroupBody - playerIds WYMAGANE (maxItems 32, element
///    maxLength 24), musicContextGroupId OPCJONALNE (maxLength 35, nullable),
///    areaIds opcjonalne i POMINIETE,
///  * Groups-SetGroupMembers-GroupId: POST /groups/{groupId}/groups/setGroupMembers,
///    cialo Groups-SetGroupMembersBody - playerIds w definicji OPCJONALNE i
///    NULLABLE, areaIds opcjonalne i POMINIETE.
///
/// Obie operacje zwracaja groupInfo, czyli obiekt z JEDNYM nullable polem group.
///
/// To NIE jest zaden RESTowy PUT /groupMembers - takiej operacji w definicji nie
/// ma i nie wolno jej domyslac.
///
/// PLAYERID TO LOGICZNY GLOSNIK, nie deviceId. Zestaw zbondowany (para stereo,
/// kino domowe z satelitami) jest w tym API JEDNA jednostka i my go NIE
/// rozdzielamy ani tego nie obiecujemy.
///
/// Czego tu NIE MA i na tym etapie byc nie moze: areaIds i "Everywhere" (UI poda
/// konkretne logiczne glosniki), algorytmu planowania grup, odczytu konfliktow,
/// nazywania grup, autostartu, Play/Stop, drugiego POST ani UI.
/// </summary>
public static class SonosGroupMembershipLimits
{
    /// <summary>playerIds: maxItems 32 w OBU cialach.</summary>
    public const int MaxPlayers = 32;

    /// <summary>
    /// playerIds.items: maxLength 24. To limit DLUGOSCI NAPISU (UTF-16) z pola
    /// maxLength definicji, sprawdzony w zapisanym zrodle - nie odziedziczona
    /// hipoteza "24 znaki ASCII". Znaki spoza ASCII sa dozwolone, bo to wartosc
    /// JSON, a serializator je zapisze poprawnie.
    /// </summary>
    public const int MaxPlayerIdLength = 24;

    /// <summary>
    /// musicContextGroupId: maxLength 35 - tyle samo co group.id, bo to JEST
    /// identyfikator grupy, z ktorej bierzemy zrodlo dzwieku.
    /// </summary>
    public const int MaxMusicContextGroupIdLength = 35;
}

/// <summary>
/// JAWNY, PELNY zestaw glosnikow dla jednej operacji skladu grupy.
///
/// playerId idzie w CIELE JSON, nie w sciezce adresu, wiec NIE stosujemy do
/// niego polityki segmentu adresu (<see cref="SonosGroupIdPolicy"/>): zaden
/// regex tozsamosci, zadna whitelista ASCII. Wartosc zachowujemy LITERALNIE
/// dokladnie tak, jak podal ja odczyt getPlayers - takze ze spacjami i znakami
/// spoza ASCII, ktore producent dopuszcza. Serializator je escapuje.
///
/// Odrzucamy natomiast to, czego wyslac NIE DA SIE uczciwie:
///  * puste zestawy - nasza polityka nowego przeplywu "Wybierz glosniki"
///    wymaga PELNEGO, jawnego zestawu; pusty zestaw nie jest poleceniem,
///  * pusty napis jako playerId - nie wskazuje zadnego glosnika,
///  * dlugosc powyzej 24 znakow i liczbe powyzej 32 - limity definicji,
///  * duplikaty - ten sam glosnik dwa razy to blad wolajacego, a nie zestaw,
///  * niepoprawny UTF-16 (samotny surogat) - serializator podmienilby go na
///    znak zastepczy i poszlaby po cichu INNA wartosc niz podal wolajacy.
///
/// Zestaw jest NIEZMIENNY i zachowuje KOLEJNOSC podana przez wolajacego.
/// </summary>
public sealed class SonosPlayerSet
{
    private SonosPlayerSet(IReadOnlyList<string> playerIds) => PlayerIds = playerIds;

    /// <summary>playerIds w kolejnosci podanej przez wolajacego, literalnie.</summary>
    public IReadOnlyList<string> PlayerIds { get; }

    public int Count => PlayerIds.Count;

    /// <summary>
    /// Buduje zestaw albo zwraca false. Nie naprawia wejscia: nie trimuje, nie
    /// odsiewa duplikatow i nie obcina listy - poprawiony zestaw nie jest tym,
    /// o ktory prosil uzytkownik.
    /// </summary>
    public static bool TryCreate(IReadOnlyList<string?>? playerIds, out SonosPlayerSet? set)
    {
        set = null;
        if (playerIds is null || playerIds.Count == 0 || playerIds.Count > SonosGroupMembershipLimits.MaxPlayers)
        {
            return false;
        }

        var accepted = new List<string>(playerIds.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var playerId in playerIds)
        {
            if (!IsAcceptablePlayerId(playerId) || !seen.Add(playerId!))
            {
                return false;
            }

            accepted.Add(playerId!);
        }

        set = new SonosPlayerSet(new ReadOnlyCollection<string>(accepted));
        return true;
    }

    private static bool IsAcceptablePlayerId(string? playerId)
    {
        if (string.IsNullOrEmpty(playerId) || playerId.Length > SonosGroupMembershipLimits.MaxPlayerIdLength)
        {
            return false;
        }

        for (var index = 0; index < playerId.Length; index++)
        {
            var character = playerId[index];
            if (!char.IsSurrogate(character))
            {
                continue;
            }

            if (!char.IsHighSurrogate(character)
                || index + 1 >= playerId.Length
                || !char.IsLowSurrogate(playerId[index + 1]))
            {
                return false;
            }

            index++;
        }

        return true;
    }

    /// <summary>Kontrolowane ToString: LICZBA glosnikow, bez identyfikatorow.</summary>
    public override string ToString() =>
        "Zestaw głośników Sonos: " + Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

/// <summary>
/// Zadanie UTWORZENIA grupy (createGroup).
///
/// musicContextGroupId jest OPCJONALNE: wg definicji "If empty or not provided,
/// the new group will not contain any audio". Dlatego null znaczy u nas
/// DOKLADNIE to samo - grupa BEZ muzyki - i jest to swiadomy, legalny wybor
/// wolajacego, a nie brak danych do uzupelnienia. Pustego napisu natomiast nie
/// przyjmujemy: wolajacy ma podac null, zeby jednoznacznie powiedziec "bez
/// zrodla", zamiast wysylac puste pole.
/// </summary>
public sealed class SonosCreateGroupRequest
{
    private SonosCreateGroupRequest(SonosPlayerSet players, string? musicContextGroupId)
    {
        Players = players;
        MusicContextGroupId = musicContextGroupId;
    }

    public SonosPlayerSet Players { get; }

    /// <summary>
    /// musicContextGroupId - grupa, z ktorej nowa grupa bierze dzwiek.
    /// null = pole POMINIETE = nowa grupa bez zadnego audio.
    /// </summary>
    public string? MusicContextGroupId { get; }

    /// <summary>Czy nowa grupa ma przejac zrodlo dzwieku z istniejacej grupy.</summary>
    public bool CarriesMusicContext => MusicContextGroupId is not null;

    public static bool TryCreate(
        IReadOnlyList<string?>? playerIds, string? musicContextGroupId, out SonosCreateGroupRequest? request)
    {
        request = null;
        if (!SonosPlayerSet.TryCreate(playerIds, out var players))
        {
            return false;
        }

        // musicContextGroupId to identyfikator grupy w CIELE, nie w sciezce -
        // wiec limit dlugosci i poprawny UTF-16, bez polityki segmentu adresu.
        if (musicContextGroupId is not null
            && (musicContextGroupId.Length == 0
                || musicContextGroupId.Length > SonosGroupMembershipLimits.MaxMusicContextGroupIdLength
                || HasLoneSurrogate(musicContextGroupId)))
        {
            return false;
        }

        request = new SonosCreateGroupRequest(players!, musicContextGroupId);
        return true;
    }

    private static bool HasLoneSurrogate(string value)
    {
        for (var index = 0; index < value.Length; index++)
        {
            if (!char.IsSurrogate(value[index]))
            {
                continue;
            }

            if (!char.IsHighSurrogate(value[index])
                || index + 1 >= value.Length
                || !char.IsLowSurrogate(value[index + 1]))
            {
                return true;
            }

            index++;
        }

        return false;
    }

    /// <summary>Kontrolowane ToString: bez identyfikatorow glosnikow i grupy.</summary>
    public override string ToString() =>
        "Żądanie utworzenia grupy Sonos: " + Players
        + (CarriesMusicContext ? ", ze źródłem dźwięku istniejącej grupy" : ", bez dźwięku");
}

/// <summary>
/// Odpowiedz obu operacji: schema groupInfo. Definicja ma TYLKO jedno pole
/// group, NULLABLE i BEZ listy required.
///
/// Stad najwazniejsza regula: HTTP 200 BEZ obiektu grupy jest POPRAWNA
/// odpowiedzia, ale NIE jest potwierdzeniem skladu - nie ma czego oddac
/// wolajacemu i nie ma jak nazwac wyniku sukcesem skladu.
///
/// Sam obiekt grupy czytamy ISTNIEJACYM typem <see cref="SonosGroup"/> - to ten
/// SAM format co w odebranym getGroups, wiec nie ma tu drugiego modelu grupy ani
/// drugiej tabeli limitow.
/// </summary>
public sealed class SonosGroupInfo
{
    internal SonosGroupInfo(SonosGroup? group) => Group = group;

    /// <summary>groupInfo.group - NULLABLE w definicji.</summary>
    public SonosGroup? Group { get; }

    /// <summary>
    /// Czy odpowiedz niesie uzyteczny identyfikator grupy. Moze to byc grupa
    /// ISTNIEJACA albo INNA niz zadana: wg definicji createGroup "may be an
    /// existing group ID if an existing group is a subset of the new group".
    /// </summary>
    public bool HasGroupId => !string.IsNullOrEmpty(Group?.Id);

    /// <summary>ToString bez identyfikatorow glosnikow.</summary>
    public override string ToString() =>
        HasGroupId ? "Sonos zwrócił obiekt grupy" : "Sonos nie podał obiektu grupy";
}

/// <summary>
/// Ktora z DWOCH operacji skladu zostala wywolana. Potrzebne, bo komunikat
/// uzytkownika i granice sa dla nich inne: createGroup moze oddac INNY
/// identyfikator grupy, a setGroupMembers zmienia sklad grupy ISTNIEJACEJ.
/// </summary>
public enum SonosGroupMembershipOperation
{
    /// <summary>POST /households/{householdId}/groups/createGroup.</summary>
    CreateGroup,

    /// <summary>POST /groups/{groupId}/groups/setGroupMembers.</summary>
    SetGroupMembers
}

/// <summary>
/// Wynik TRANSPORTU jednej operacji skladu grupy.
///
/// Rozdziela te same trzy rzeczy co pozostale zapisy: zadanie nie poszlo /
/// Sonos PRZYJAL (HTTP 200 - NIE dowod skladu ani dzwieku) / skutek NIEZNANY.
/// Do tego czwarta, wlasna dla tych operacji: przyjete, ale BEZ zwroconego
/// identyfikatora grupy.
/// </summary>
public sealed class SonosGroupMembershipOutcome
{
    private SonosGroupMembershipOutcome(
        SonosGroupMembershipOperation operation, SonosControlApiStatus status, SonosGroupInfo? info, bool sent)
    {
        Operation = operation;
        Status = status;
        Info = status == SonosControlApiStatus.Success ? info : null;
        Sent = sent;
    }

    public SonosGroupMembershipOperation Operation { get; }

    public SonosControlApiStatus Status { get; }

    /// <summary>Odczytane groupInfo TYLKO przy sukcesie.</summary>
    public SonosGroupInfo? Info { get; }

    /// <summary>Czy zadanie HTTP w ogole opuscilo aplikacje.</summary>
    public bool Sent { get; }

    /// <summary>Sonos PRZYJAL zlecenie (HTTP 200). NIE znaczy, ze sklad jest taki.</summary>
    public bool Accepted => Status == SonosControlApiStatus.Success;

    /// <summary>
    /// Przyjete ORAZ z uzytecznym identyfikatorem grupy, ktory da sie wstawic w
    /// sciezke kolejnego polecenia. Samo 200 nie wystarcza - group jest w
    /// definicji nullable.
    /// </summary>
    public bool HasGroupId => Accepted && SonosGroupIdPolicy.IsAcceptable(Info?.Group?.Id);

    /// <summary>
    /// Zawsze false: przyjecie zlecenia nie dowodzi skladu grupy. Sklad pokaze
    /// dopiero SWIEZY odczyt topologii, ktory nalezy do wolajacego.
    /// </summary>
    public bool EffectConfirmed => false;

    /// <summary>
    /// Skutek NIEROZSTRZYGNIETY: zapis poszedl w siec, a odpowiedz nie wrocila.
    /// Sklad grup mogl sie JUZ zmienic. Nie wolno tego ponawiac automatycznie.
    /// </summary>
    public bool EffectAmbiguous =>
        Sent && Status is SonosControlApiStatus.Unreachable or SonosControlApiStatus.Canceled;

    public string Message =>
        SonosGroupMembershipMessages.Describe(Operation, Status, Sent, EffectAmbiguous, HasGroupId);

    internal static SonosGroupMembershipOutcome Ok(SonosGroupMembershipOperation operation, SonosGroupInfo info) =>
        new(operation, SonosControlApiStatus.Success, info, true);

    internal static SonosGroupMembershipOutcome Failure(
        SonosGroupMembershipOperation operation, SonosControlApiStatus status, bool sent) =>
        new(operation,
            status == SonosControlApiStatus.Success ? SonosControlApiStatus.InvalidResponse : status,
            null, sent);

    /// <summary>ToString bez identyfikatorow grup i glosnikow.</summary>
    public override string ToString() =>
        "Zmiana składu grupy Sonos (" + Operation + "): " + Status
        + (Sent ? ", podjęto próbę wysłania" : ", żądania nie wysłano")
        + (HasGroupId ? ", z identyfikatorem grupy" : ", bez identyfikatora grupy")
        + (EffectAmbiguous ? ", skutek nieznany" : string.Empty);
}

/// <summary>
/// Stale komunikaty PL dla DWOCH operacji skladu grupy.
///
/// Zaden tekst nie zawiera identyfikatora grupy, identyfikatora glosnika,
/// tokenu, klucza integracji, surowej tresci odpowiedzi ani pola reason z bledu
/// Sonosa (reason jest wg definicji napisem diagnostycznym producenta, wprost
/// NIE przeznaczonym do pokazywania uzytkownikowi).
///
/// Zaden komunikat sukcesu nie oglasza, ze sklad grupy JEST taki, jak zadano -
/// najwyzej, ze Sonos zlecenie PRZYJAL.
/// </summary>
public static class SonosGroupMembershipMessages
{
    public const string CreateAcceptedText =
        "Sonos przyjął utworzenie grupy; sam skład potwierdzi dopiero odczyt głośników.";

    public const string CreateAcceptedWithoutIdText =
        "Sonos przyjął utworzenie grupy, ale nie podał identyfikatora grupy, "
        + "więc składu nie potwierdzamy. Odczytaj głośniki ponownie.";

    public const string SetAcceptedText =
        "Sonos przyjął nowy skład grupy; sam skład potwierdzi dopiero odczyt głośników.";

    public const string SetAcceptedWithoutIdText =
        "Sonos przyjął nowy skład grupy, ale nie podał identyfikatora grupy, "
        + "więc składu nie potwierdzamy. Odczytaj głośniki ponownie.";

    public const string CreateAmbiguousText =
        "Nie otrzymano odpowiedzi na utworzenie grupy Sonos: nie wiadomo, czy grupa powstała. "
        + "Odczytaj głośniki ponownie przed kolejną próbą.";

    public const string SetAmbiguousText =
        "Nie otrzymano odpowiedzi na zmianę składu grupy Sonos: skutek nieznany. "
        + "Odczytaj głośniki ponownie przed kolejną próbą.";

    private static readonly IReadOnlyDictionary<SonosControlApiStatus, string> CreateTexts =
        new Dictionary<SonosControlApiStatus, string>
        {
            [SonosControlApiStatus.InvalidConfiguration] =
                "Utworzenia grupy nie wysłano: brak klucza integracji, brak domu albo niepoprawny zestaw głośników.",
            [SonosControlApiStatus.Unauthorized] =
                "Sonos nie przyjął dostępu do konta, więc grupy nie utworzono. Odśwież dostęp albo zaloguj się ponownie.",
            [SonosControlApiStatus.Forbidden] = "Konto Sonos nie ma uprawnień do tworzenia grup.",
            [SonosControlApiStatus.NotFound] =
                "Sonos nie znalazł wskazanego domu albo głośnika; grupy nie utworzono. "
                + "Lista głośników mogła się zmienić.",
            [SonosControlApiStatus.RequestRejected] =
                "Sonos odrzucił utworzenie grupy jako niepoprawne (np. któregoś głośnika nie da się dodać).",
            [SonosControlApiStatus.RateLimited] =
                "Sonos chwilowo ogranicza liczbę żądań; grupy nie utworzono. Spróbuj później.",
            [SonosControlApiStatus.CommandFailed] = "Sonos zgłosił, że grupy nie utworzył.",
            [SonosControlApiStatus.ServiceError] = "Usługa Sonos zgłosiła błąd; stan grup niepotwierdzony.",
            [SonosControlApiStatus.Unreachable] =
                "Nie otrzymano potwierdzenia utworzenia grupy Sonos; stan grup niepotwierdzony.",
            [SonosControlApiStatus.Canceled] = "Utworzenie grupy anulowano.",
            [SonosControlApiStatus.InvalidResponse] =
                "Odpowiedź Sonos o grupie była niezgodna z oczekiwaną; utworzenia nie uznajemy za potwierdzone.",
            [SonosControlApiStatus.RedirectRefused] =
                "Odrzucono nieoczekiwaną odpowiedź na utworzenie grupy Sonos; stan grup niepotwierdzony."
        };

    private static readonly IReadOnlyDictionary<SonosControlApiStatus, string> SetTexts =
        new Dictionary<SonosControlApiStatus, string>
        {
            [SonosControlApiStatus.InvalidConfiguration] =
                "Zmiany składu nie wysłano: brak klucza integracji, niepoprawna grupa albo niepoprawny zestaw głośników.",
            [SonosControlApiStatus.Unauthorized] =
                "Sonos nie przyjął dostępu do konta, więc składu grupy nie zmieniono. "
                + "Odśwież dostęp albo zaloguj się ponownie.",
            [SonosControlApiStatus.Forbidden] = "Konto Sonos nie ma uprawnień do zmiany składu tej grupy.",
            [SonosControlApiStatus.NotFound] =
                "Sonos nie znalazł tej grupy; składu nie zmieniono. Grupa mogła zostać w tym czasie rozwiązana.",
            [SonosControlApiStatus.RequestRejected] =
                "Sonos odrzucił nowy skład grupy jako niepoprawny (np. któregoś głośnika nie da się dodać).",
            [SonosControlApiStatus.RateLimited] =
                "Sonos chwilowo ogranicza liczbę żądań; składu nie zmieniono. Spróbuj później.",
            [SonosControlApiStatus.CommandFailed] = "Sonos zgłosił, że składu grupy nie zmienił.",
            [SonosControlApiStatus.ServiceError] = "Usługa Sonos zgłosiła błąd; skład grupy niepotwierdzony.",
            [SonosControlApiStatus.Unreachable] =
                "Nie otrzymano potwierdzenia zmiany składu grupy Sonos; skład niepotwierdzony.",
            [SonosControlApiStatus.Canceled] = "Zmianę składu grupy anulowano.",
            [SonosControlApiStatus.InvalidResponse] =
                "Odpowiedź Sonos o grupie była niezgodna z oczekiwaną; zmiany składu nie uznajemy za potwierdzoną.",
            [SonosControlApiStatus.RedirectRefused] =
                "Odrzucono nieoczekiwaną odpowiedź na zmianę składu grupy Sonos; skład niepotwierdzony."
        };

    /// <summary>
    /// Tekst wyniku transportu. Przypadek "przyjete, ale BEZ identyfikatora
    /// grupy" ma WLASNE zdanie, bo nazwanie go sukcesem skladu byloby klamstwem.
    /// </summary>
    public static string Describe(
        SonosGroupMembershipOperation operation,
        SonosControlApiStatus status,
        bool sent,
        bool effectAmbiguous,
        bool hasGroupId)
    {
        var create = operation == SonosGroupMembershipOperation.CreateGroup;
        if (effectAmbiguous)
        {
            return create ? CreateAmbiguousText : SetAmbiguousText;
        }

        if (status == SonosControlApiStatus.Success)
        {
            return create
                ? hasGroupId ? CreateAcceptedText : CreateAcceptedWithoutIdText
                : hasGroupId ? SetAcceptedText : SetAcceptedWithoutIdText;
        }

        var texts = create ? CreateTexts : SetTexts;
        var text = texts.TryGetValue(status, out var found) ? found : texts[SonosControlApiStatus.InvalidResponse];
        return sent || status == SonosControlApiStatus.InvalidConfiguration
            ? text
            : (create ? "Utworzenia grupy nie wysłano. " : "Zmiany składu grupy nie wysłano. ") + text;
    }
}

/// <summary>
/// WASKA granica zmiany skladu grup - DOKLADNIE dwie operacje zapisu, zadnego
/// odczytu. Osobna od granic odczytu topologii i od granic polecen odtwarzania,
/// zeby istniejace atrapy nie musialy nagle umiec przestawiac cudzych grup.
///
/// Token jest ARGUMENTEM jednego wywolania; implementacja go nie zapisuje.
/// </summary>
public interface ISonosGroupMembershipApi
{
    /// <summary>POST /households/{householdId}/groups/createGroup.</summary>
    Task<SonosGroupMembershipOutcome> CreateGroupAsync(
        string? accessToken,
        string? householdId,
        SonosCreateGroupRequest? request,
        CancellationToken cancellationToken);

    /// <summary>
    /// POST /groups/{groupId}/groups/setGroupMembers z PELNYM, jawnym zestawem.
    ///
    /// playerIds jest w definicji opcjonalne i nullable, ale NASZ przeplyw
    /// zawsze podaje caly nowy zestaw - dlatego tutaj jest wymagany typ
    /// <see cref="SonosPlayerSet"/>, a nie lista, ktora mogla by byc pusta.
    /// </summary>
    Task<SonosGroupMembershipOutcome> SetGroupMembersAsync(
        string? accessToken,
        string? groupId,
        SonosPlayerSet? players,
        CancellationToken cancellationToken);
}
