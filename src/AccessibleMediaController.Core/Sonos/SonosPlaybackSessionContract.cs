using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AccessibleMediaController.Core.Sonos;

/// <summary>
/// WLASNE RADIO W SONOSIE - kontrakt dwoch operacji sesji odtwarzania:
/// createSession i loadStreamUrl.
///
/// Zrodlo: oficjalna definicja OpenAPI 3.0.3 "Sonos Control API (cloud)"
/// v1.56.0-alpha.1-1-gc264f93f-production-cloud, operacje
/// PlaybackSession-CreateSession-GroupId (POST /groups/{groupId}/playbackSession)
/// i PlaybackSession-LoadStreamUrl-SessionId
/// (POST /playbackSessions/{sessionId}/playbackSession/loadStreamUrl).
/// Wszystkie limity (appId 127, appContext 127, accountId 13, customData 1023,
/// streamUrl 1024, itemId 128, sessionId 46) pochodza z pol maxLength tych
/// definicji, a nie z nazw ani z domyslow.
///
/// To jest droga WLASNEGO adresu radia zapisanego w AMC - NIE zaladowanie
/// ulubionego Sonosa. Ulubione (F3a/F3b) i playlisty zostaja nietkniete.
///
/// Czego tu NIE MA i na tym etapie byc nie moze: serwera kolejki w chmurze
/// (cloudQueue), odtwarzania utworow na zadanie, SMAPI, audioClip, subskrypcji
/// playbackStatus, presetow, grupowania ani UI. Dokumentacja wymaga OTWARTEJ
/// sesji dla loadStreamUrl; czy radio zagra bez wlasnego serwera kolejki,
/// rozstrzygnie dopiero proba na prawdziwym koncie - tego nie udajemy atrapa.
/// </summary>
public static class SonosPlaybackSessionLimits
{
    /// <summary>appId - maxLength 127. Odwrotna nazwa DNS identyfikujaca APLIKACJE.</summary>
    public const int MaxAppIdLength = 127;

    /// <summary>appContext - maxLength 127. Nieprzezroczysta instancja aplikacji.</summary>
    public const int MaxAppContextLength = 127;

    /// <summary>accountId - maxLength 13. Konto USLUGI MUZYCZNEJ na gloshniku.</summary>
    public const int MaxAccountIdLength = 13;

    /// <summary>customData - maxLength 1023 (gloshnik dluzsze OBCINA i zwraca obciete).</summary>
    public const int MaxCustomDataLength = 1023;

    /// <summary>
    /// SUMA bajtow UTF-8 appId i appContext musi byc MNIEJSZA niz 255 - inaczej
    /// gloshnik zwroci blad. To warunek definicji, nie nasza ostroznosc, i nie
    /// wynika z samych maxLength (127 + 127 = 254 znakow, ale bajtow moze byc
    /// wiecej).
    /// </summary>
    public const int MaxAppIdAndContextBytes = 255;

    /// <summary>sessionStatus.sessionId - maxLength 46, NULLABLE w definicji.</summary>
    public const int MaxSessionIdLength = 46;

    /// <summary>streamUrl - maxLength 1024. Adres strumienia radia NA ZYWO.</summary>
    public const int MaxStreamUrlLength = 1024;

    /// <summary>itemId - maxLength 128. Korelacja przyszlych zdarzen playbackStatus.</summary>
    public const int MaxItemIdLength = 128;
}

/// <summary>
/// Polityka segmentu adresu dla sessionId. To INNY ZASOB niz groupId: sessionId
/// jest identyfikatorem SESJI, nie grupy, i wchodzi w sciezke
/// /playbackSessions/{sessionId}/... . Nie wolno go sprawdzac polityka grupy ani
/// odwrotnie, mimo ze oba sa napisami.
///
/// Definicja podaje dla parametru sciezki wylacznie type:string - ZADNEGO
/// wzorca. Nie wymyslamy wiec regexa tozsamosci: zachowujemy wartosc
/// LITERALNIE, a bezpieczenstwo daje biala lista znakow dozwolonych w segmencie
/// (pchar z RFC 3986 bez znakow, ktore zmienialyby strukture adresu). Wszystko
/// poza nia jest ODRZUCANE, nie naprawiane - poprawiony identyfikator nie
/// wskazalby tej samej sesji.
/// </summary>
public static class SonosSessionIdPolicy
{
    public static bool IsAcceptable(string? sessionId) => TryEncode(sessionId, out _);

    /// <summary>
    /// Bezpieczna postac sessionId do wstawienia jako JEDEN segment sciezki.
    /// Kodowanie NIE zmienia tozsamosci: dozwolone znaki ida bez zmian, a
    /// znakow wymagajacych procent-kodowania po prostu nie wpuszczamy.
    /// </summary>
    public static bool TryEncode(string? sessionId, out string? encoded)
    {
        encoded = null;
        if (string.IsNullOrEmpty(sessionId) || sessionId.Length > SonosPlaybackSessionLimits.MaxSessionIdLength)
        {
            return false;
        }

        var onlyDots = true;
        foreach (var character in sessionId)
        {
            var allowed = character is >= 'A' and <= 'Z'
                or >= 'a' and <= 'z'
                or >= '0' and <= '9'
                or '_'
                or '-'
                or '.'
                or ':'
                // Observed in sessionId returned by Sonos; safe inside a path segment.
                or '@'
                or '~';
            if (!allowed)
            {
                return false;
            }

            if (character != '.')
            {
                onlyDots = false;
            }
        }

        // Segment z samych kropek to przejscie po katalogach.
        if (onlyDots)
        {
            return false;
        }

        encoded = sessionId;
        return true;
    }
}

/// <summary>
/// Adres strumienia RADIA NA ZYWO dla loadStreamUrl.
///
/// Wymagamy POPRAWNEGO absolutnego http albo https, bo pole opisane jest wprost
/// jako "HTTP URL for the radio station stream". Nie obiecujemy przy tym, ze
/// KAZDY format audio albo KAZDY adres zagra: lista typow tresci nalezy do
/// gloshnika, a adresy lokalne moga byc dla chmury Sonos nieosiagalne. Bramka
/// odrzuca tylko to, co pewnie nie jest poprawnym adresem HTTP.
///
/// Adresu NIE POBIERAMY: zadnego GET pod wskazany URL, nawet "zeby sprawdzic".
/// Zadnej normalizacji, trimowania ani dopisywania schematu - wyslemy DOKLADNIE
/// to, co podal wolajacy.
/// </summary>
public static class SonosStreamUrlPolicy
{
    public static bool IsAcceptable(string? streamUrl) =>
        !string.IsNullOrEmpty(streamUrl)
        && streamUrl.Length <= SonosPlaybackSessionLimits.MaxStreamUrlLength
        && !HasForbiddenCharacter(streamUrl)
        && Uri.TryCreate(streamUrl, UriKind.Absolute, out var uri)
        && (string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal)
            || string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal))
        && !string.IsNullOrEmpty(uri.Host);

    /// <summary>
    /// Znaki, ktorych w adresie byc nie moze: sterujace, spacje i cudzyslow.
    /// Uri.TryCreate czesc z nich toleruje, a my nie chcemy wyslac adresu, ktory
    /// po drodze mozna rozlamac.
    /// </summary>
    private static bool HasForbiddenCharacter(string value)
    {
        foreach (var character in value)
        {
            if (character <= ' ' || character == '"' || character == '\\' || character == '<' || character == '>'
                || character == '^' || character == '`' || character == '{' || character == '}' || character == '|'
                || character == (char)0x7f)
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>
/// Tozsamosc aplikacji dla createSession. appId identyfikuje APLIKACJE i NIE
/// JEST ani OAuth clientId, ani tokenem, ani kluczem integracji - pomylenie tych
/// rzeczy wyslaloby poswiadczenie w ciele polecenia.
///
/// appContext podaje WOLAJACY i nie ma tu wartosci domyslnej: od niego zalezy,
/// czy dwie instancje aplikacji moga sterowac ta sama sesja. Transport nie ma
/// prawa tej polityki wybrac za uzytkownika.
///
/// accountId uslugi muzycznej CELOWO pomijamy w minimalnym radiu: zly accountId
/// daje ERROR_INVALID_PARAMETER, a zgadywanie go nie ma zadnej podstawy.
/// customData tez pomijamy - nie mamy czego w sesji przechowywac.
/// </summary>
public sealed class SonosSessionRequest
{
    private SonosSessionRequest(string appId, string appContext)
    {
        AppId = appId;
        AppContext = appContext;
    }

    /// <summary>appId - odwrotna nazwa DNS aplikacji, maxLength 127.</summary>
    public string AppId { get; }

    /// <summary>appContext - nieprzezroczysta instancja aplikacji, maxLength 127.</summary>
    public string AppContext { get; }

    /// <summary>
    /// Buduje zadanie sesji albo zwraca false. Sprawdzane jest DOKLADNIE to, co
    /// stawia definicja: oba pola WYMAGANE i niepuste, kazde do 127 znakow,
    /// SUMA bajtow UTF-8 mniejsza niz 255. Odrzucamy tez niepoprawny UTF-16
    /// (samotny surogat), bo serializator podmienilby go na znak zastepczy i
    /// poszlaby INNA wartosc niz podal wolajacy.
    /// </summary>
    public static bool TryCreate(string? appId, string? appContext, out SonosSessionRequest? request)
    {
        request = null;
        if (!IsAcceptableField(appId, SonosPlaybackSessionLimits.MaxAppIdLength)
            || !IsAcceptableField(appContext, SonosPlaybackSessionLimits.MaxAppContextLength))
        {
            return false;
        }

        var bytes = System.Text.Encoding.UTF8.GetByteCount(appId!)
            + System.Text.Encoding.UTF8.GetByteCount(appContext!);
        if (bytes >= SonosPlaybackSessionLimits.MaxAppIdAndContextBytes)
        {
            return false;
        }

        request = new SonosSessionRequest(appId!, appContext!);
        return true;
    }

    private static bool IsAcceptableField(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length > maxLength)
        {
            return false;
        }

        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (character < ' ' || character == (char)0x7f)
            {
                return false;
            }

            if (!char.IsSurrogate(character))
            {
                continue;
            }

            if (!char.IsHighSurrogate(character)
                || index + 1 >= value.Length
                || !char.IsLowSurrogate(value[index + 1]))
            {
                return false;
            }

            index++;
        }

        return true;
    }

    /// <summary>
    /// Kontrolowane ToString: NIE pokazuje ani appId, ani appContext. appContext
    /// moze nosic zakodowana tozsamosc uzytkownika, a appId nie ma powodu
    /// trafiac do logu.
    /// </summary>
    public override string ToString() => "Żądanie sesji odtwarzania Sonos (bez ujawniania identyfikatorów).";
}

/// <summary>
/// sessionState z definicji (schema sessionStateEnum). Jedyna wartosc to
/// SESSION_STATE_CONNECTED, pole jest OPCJONALNE i NULLABLE, a definicja wprost
/// zapowiada mozliwe wycofanie. Dlatego jego BRAK nie jest bledem i nie wolno z
/// niego robic warunku gotowosci sesji.
/// </summary>
public enum SonosSessionState
{
    /// <summary>Pola nie bylo albo mialo nieznana wartosc - NIE jest to blad.</summary>
    Unknown,

    /// <summary>SESSION_STATE_CONNECTED.</summary>
    Connected
}

/// <summary>
/// Odpowiedz createSession (schema sessionStatus). WSZYSTKIE pola sa w definicji
/// opcjonalne albo nullable, w szczegolnosci sessionId.
///
/// Dlatego kluczowa regula brzmi: HTTP 200 BEZ niepustego sessionId NIE JEST
/// gotowa sesja. Dalsze polecenie (loadStreamUrl) potrzebuje poprawnego,
/// niepustego identyfikatora - bez niego nie ma czego zaladowac i nie wolno
/// udawac, ze sesja istnieje.
/// </summary>
public sealed class SonosSessionStatus
{
    internal SonosSessionStatus(string? sessionId, SonosSessionState sessionState, bool? sessionCreated)
    {
        SessionId = sessionId;
        SessionState = sessionState;
        SessionCreated = sessionCreated;
    }

    /// <summary>
    /// sessionId - maxLength 46, NULLABLE. Null znaczy, ze gloshnik go nie
    /// podal; nie podstawiamy pustego napisu.
    /// </summary>
    public string? SessionId { get; }

    /// <summary>sessionState - opcjonalne. Brak pola daje Unknown, nie blad.</summary>
    public SonosSessionState SessionState { get; }

    /// <summary>
    /// sessionCreated - czy sesja jest NOWA (true) czy dolaczono do istniejacej
    /// (false). Definicja ma default false, ale BRAK pola zostaje nullem: "nie
    /// wiadomo" i "dolaczono do istniejacej" to rozne informacje.
    /// </summary>
    public bool? SessionCreated { get; }

    /// <summary>
    /// Czy odpowiedz nadaje sie do WYSLANIA DALSZEGO polecenia. Wymaga
    /// identyfikatora, ktory przejdzie polityke segmentu adresu - sam niepusty
    /// napis nie wystarczy, bo nie kazdy napis da sie wstawic w sciezke.
    /// </summary>
    public bool HasUsableSessionId => SonosSessionIdPolicy.IsAcceptable(SessionId);

    /// <summary>
    /// Kontrolowane ToString: BEZ sessionId. Identyfikator sesji pozwala sterowac
    /// odtwarzaniem u uzytkownika, wiec nie trafia do logu ani do komunikatu.
    /// </summary>
    public override string ToString() =>
        "Sesja odtwarzania Sonos: "
        + (HasUsableSessionId ? "identyfikator otrzymany" : "bez użytecznego identyfikatora")
        + ", stan " + SessionState
        + (SessionCreated switch
        {
            true => ", nowo utworzona",
            false => ", dołączono do istniejącej",
            _ => ", nie wiadomo czy nowa"
        });
}

/// <summary>
/// Wynik transportu dla POST /groups/{groupId}/playbackSession.
///
/// To ZAPIS: polecenie moze WYPRZEC cudze odtwarzanie w grupie ("clobber any
/// existing sessions" wg definicji). Dlatego - jak przy innych poleceniach -
/// HTTP 200 znaczy PRZYJECIE, a dane wychodza tylko przy sukcesie.
/// </summary>
public sealed class SonosSessionOutcome
{
    private SonosSessionOutcome(SonosControlApiStatus status, SonosSessionStatus? session, bool sent)
    {
        Status = status;
        Session = status == SonosControlApiStatus.Success ? session : null;
        Sent = sent;
    }

    public SonosControlApiStatus Status { get; }

    /// <summary>Odczytana odpowiedz TYLKO przy sukcesie.</summary>
    public SonosSessionStatus? Session { get; }

    /// <summary>Czy zadanie HTTP w ogole opuscilo aplikacje.</summary>
    public bool Sent { get; }

    /// <summary>Sonos PRZYJAL polecenie utworzenia sesji (HTTP 200).</summary>
    public bool Accepted => Status == SonosControlApiStatus.Success;

    /// <summary>
    /// Sesja jest GOTOWA do dalszego polecenia: przyjeta ORAZ z uzytecznym
    /// sessionId. Samo 200 nie wystarcza - sessionId jest w definicji nullable.
    /// </summary>
    public bool Ready => Accepted && Session?.HasUsableSessionId == true;

    /// <summary>
    /// Skutek NIEROZSTRZYGNIETY: zapis poszedl w siec, a odpowiedz nie wrocila.
    /// Sesja mogla zostac utworzona i wyprzec cudze odtwarzanie. Nie wolno tego
    /// ponawiac automatycznie.
    /// </summary>
    public bool EffectAmbiguous =>
        Sent && Status is SonosControlApiStatus.Unreachable or SonosControlApiStatus.Canceled;

    public string Message => SonosPlaybackSessionMessages.DescribeCreate(Status, Sent, EffectAmbiguous, Ready);

    internal static SonosSessionOutcome Ok(SonosSessionStatus session) =>
        new(SonosControlApiStatus.Success, session, true);

    internal static SonosSessionOutcome Failure(SonosControlApiStatus status, bool sent) =>
        new(status == SonosControlApiStatus.Success ? SonosControlApiStatus.InvalidResponse : status, null, sent);

    /// <summary>ToString bez sessionId i bez tresci odpowiedzi.</summary>
    public override string ToString() =>
        "Utworzenie sesji Sonos: " + Status
        + (Sent ? ", podjęto próbę wysłania" : ", żądania nie wysłano")
        + (Ready ? ", sesja gotowa" : ", brak gotowej sesji")
        + (EffectAmbiguous ? ", skutek nieznany" : string.Empty);
}

/// <summary>
/// Wynik WCZYTANIA RADIA do sesji. Osobny typ od wyniku polecenia grupy, bo
/// loadStreamUrl NIE jest poleceniem grupy: dziala na innym zasobie
/// (/playbackSessions/{sessionId}/...), nie ma go w tabeli polecen grupy i nie
/// wolno mu wejsc do tamtej enumeracji ani do jej tabeli sciezek.
///
/// Rozdziela te same trzy rzeczy co wynik polecenia: zadanie nie poszlo /
/// Sonos przyjal (HTTP 200, NIE dowod ze radio gra) / skutek nieznany.
/// </summary>
public sealed class SonosStreamUrlOutcome
{
    private SonosStreamUrlOutcome(SonosControlApiStatus status, bool sent)
    {
        Status = status;
        Sent = sent;
    }

    public SonosControlApiStatus Status { get; }

    /// <summary>Czy zadanie HTTP w ogole opuscilo aplikacje.</summary>
    public bool Sent { get; }

    /// <summary>Sonos PRZYJAL adres strumienia (HTTP 200).</summary>
    public bool Accepted => Status == SonosControlApiStatus.Success;

    /// <summary>
    /// Zawsze false: przyjecie adresu nie dowodzi, ze radio gra. Strumien moze
    /// byc nieosiagalny albo w formacie, ktorego gloshnik nie odtworzy, a tego
    /// transport nie sprawdza (i celowo nie pobiera adresu).
    /// </summary>
    public bool EffectConfirmed => false;

    /// <summary>
    /// Skutek NIEROZSTRZYGNIETY: zapis poszedl w siec bez odpowiedzi. Wczytanie
    /// radia zmienia to, co gra, wiec nie wolno go ponawiac automatycznie.
    /// </summary>
    public bool EffectAmbiguous =>
        Sent && Status is SonosControlApiStatus.Unreachable or SonosControlApiStatus.Canceled;

    public string Message => SonosPlaybackSessionMessages.DescribeLoad(Status, Sent, EffectAmbiguous);

    internal static SonosStreamUrlOutcome FromStatus(SonosControlApiStatus status, bool sent) => new(status, sent);

    /// <summary>ToString bez adresu strumienia i bez sessionId.</summary>
    public override string ToString() =>
        "Wczytanie radia w Sonosie: " + Status
        + (Accepted ? ", przyjęte (odtwarzanie niepotwierdzone)" : string.Empty)
        + (EffectAmbiguous ? ", skutek nierozstrzygnięty" : string.Empty)
        + ", " + Message;
}

/// <summary>
/// Stale komunikaty PL dla DWOCH operacji sesji. Osobne od slownika polecen
/// grupy, bo opisuja inne zdarzenia - w szczegolnosci przypadek "HTTP 200, ale
/// bez identyfikatora sesji", ktorego przy poleceniach grupy nie ma.
///
/// Zaden tekst nie zawiera sessionId, adresu strumienia, appId, appContext,
/// tokenu, klucza integracji ani surowej tresci odpowiedzi.
/// </summary>
public static class SonosPlaybackSessionMessages
{
    public const string CreatedText =
        "Sonos utworzył sesję odtwarzania; samo utworzenie nie znaczy, że radio zagrało.";

    public const string AcceptedWithoutIdText =
        "Sonos przyjął żądanie sesji, ale nie podał użytecznego identyfikatora sesji, "
        + "więc nie ma czego dalej wysłać.";

    public const string CreateAmbiguousText =
        "Nie otrzymano odpowiedzi na żądanie sesji Sonos: nie wiadomo, czy sesja powstała. "
        + "Sprawdź stan odtwarzania; nowa sesja mogła przerwać to, co grało.";

    public const string LoadAcceptedText =
        "Sonos przyjął adres radia; odtwarzanie niepotwierdzone.";

    public const string LoadAmbiguousText =
        "Nie otrzymano odpowiedzi na wczytanie radia w Sonosie: skutek nieznany. Sprawdź stan odtwarzania.";

    private static readonly IReadOnlyDictionary<SonosControlApiStatus, string> CreateTexts =
        new Dictionary<SonosControlApiStatus, string>
        {
            [SonosControlApiStatus.InvalidConfiguration] =
                "Żądanie sesji Sonos nie zostało wysłane: brak klucza integracji albo niepoprawne dane żądania.",
            [SonosControlApiStatus.Unauthorized] =
                "Sonos nie przyjął dostępu do konta, więc sesja nie powstała. Odśwież dostęp albo zaloguj się ponownie.",
            [SonosControlApiStatus.Forbidden] = "Konto Sonos nie ma uprawnień do tworzenia sesji odtwarzania.",
            [SonosControlApiStatus.NotFound] = "Sonos nie znalazł wskazanej grupy; sesja nie powstała.",
            [SonosControlApiStatus.RequestRejected] = "Sonos odrzucił żądanie sesji jako niepoprawne.",
            [SonosControlApiStatus.RateLimited] =
                "Sonos chwilowo ogranicza liczbę żądań; sesja nie powstała. Spróbuj później.",
            [SonosControlApiStatus.CommandFailed] =
                "Sonos nie utworzył sesji odtwarzania (zgłosił niewykonanie polecenia).",
            [SonosControlApiStatus.ServiceError] = "Usługa Sonos zgłosiła błąd; stan sesji niepotwierdzony.",
            [SonosControlApiStatus.Unreachable] =
                "Nie otrzymano potwierdzenia utworzenia sesji Sonos; stan niepotwierdzony.",
            [SonosControlApiStatus.Canceled] = "Żądanie sesji anulowano.",
            [SonosControlApiStatus.InvalidResponse] =
                "Odpowiedź Sonos o sesji była niezgodna z oczekiwaną; sesji nie uznajemy za utworzoną.",
            [SonosControlApiStatus.RedirectRefused] =
                "Odrzucono nieoczekiwaną odpowiedź na żądanie sesji Sonos; stan niepotwierdzony."
        };

    /// <summary>
    /// Tekst wyniku createSession. Przypadek "przyjete, ale BEZ identyfikatora"
    /// ma WLASNE zdanie, bo milczace nazwanie go sukcesem byloby klamstwem -
    /// dalsze polecenie nie ma wtedy adresu.
    /// </summary>
    public static string DescribeCreate(SonosControlApiStatus status, bool sent, bool effectAmbiguous, bool ready)
    {
        if (effectAmbiguous)
        {
            return CreateAmbiguousText;
        }

        if (status == SonosControlApiStatus.Success)
        {
            return ready ? CreatedText : AcceptedWithoutIdText;
        }

        var text = CreateTexts.TryGetValue(status, out var found)
            ? found
            : CreateTexts[SonosControlApiStatus.InvalidResponse];
        return sent || status == SonosControlApiStatus.InvalidConfiguration
            ? text
            : "Żądanie sesji Sonos nie zostało wysłane. " + text;
    }

    private static readonly IReadOnlyDictionary<SonosControlApiStatus, string> LoadTexts =
        new Dictionary<SonosControlApiStatus, string>
        {
            [SonosControlApiStatus.InvalidConfiguration] =
                "Radia nie wysłano do Sonosa: brak klucza integracji, niepoprawny adres strumienia "
                + "albo niepoprawny identyfikator sesji.",
            [SonosControlApiStatus.Unauthorized] =
                "Sonos nie przyjął dostępu do konta, więc radia nie wczytano. Odśwież dostęp albo zaloguj się ponownie.",
            [SonosControlApiStatus.Forbidden] = "Konto Sonos nie ma uprawnień do wczytania radia w tej sesji.",
            [SonosControlApiStatus.NotFound] =
                "Sonos nie znalazł tej sesji odtwarzania; radia nie wczytano. Sesja mogła zostać zamknięta "
                + "albo przejęta przez inną aplikację.",
            [SonosControlApiStatus.RequestRejected] =
                "Sonos odrzucił wczytanie radia jako niepoprawne (np. adres strumienia nie został przyjęty).",
            [SonosControlApiStatus.RateLimited] =
                "Sonos chwilowo ogranicza liczbę żądań; radia nie wczytano. Spróbuj później.",
            [SonosControlApiStatus.CommandFailed] =
                "Sonos przyjął żądanie, ale zgłosił, że radia nie wczytał.",
            [SonosControlApiStatus.ServiceError] = "Usługa Sonos zgłosiła błąd; wczytanie radia niepotwierdzone.",
            [SonosControlApiStatus.Unreachable] =
                "Nie otrzymano potwierdzenia wczytania radia w Sonosie; wynik niepotwierdzony.",
            [SonosControlApiStatus.Canceled] = "Wczytanie radia anulowano.",
            [SonosControlApiStatus.InvalidResponse] =
                "Odpowiedź Sonos na wczytanie radia była niezgodna z oczekiwaną; przyjęcie niepotwierdzone.",
            [SonosControlApiStatus.RedirectRefused] =
                "Odrzucono nieoczekiwaną odpowiedź na wczytanie radia w Sonosie; wynik niepotwierdzony."
        };

    /// <summary>
    /// Tekst wyniku loadStreamUrl. Nigdy nie oglasza, ze radio GRA - najwyzej, ze
    /// Sonos adres PRZYJAL. Nie zawiera adresu strumienia.
    /// </summary>
    public static string DescribeLoad(SonosControlApiStatus status, bool sent, bool effectAmbiguous)
    {
        if (effectAmbiguous)
        {
            return LoadAmbiguousText;
        }

        if (status == SonosControlApiStatus.Success)
        {
            return LoadAcceptedText;
        }

        var text = LoadTexts.TryGetValue(status, out var found)
            ? found
            : LoadTexts[SonosControlApiStatus.InvalidResponse];
        return sent || status == SonosControlApiStatus.InvalidConfiguration
            ? text
            : "Żądania wczytania radia nie wysłano. " + text;
    }
}

/// <summary>
/// WASKA granica TWORZENIA sesji - dokladnie jedna operacja ZAPISU. Osobna od
/// granic odczytu i od granic polecen grupy, zeby istniejace atrapy odczytu nie
/// musialy nagle umiec przejmowac cudzego odtwarzania.
///
/// Token jest ARGUMENTEM jednego wywolania; implementacja go nie zapisuje.
/// </summary>
public interface ISonosSessionCreateApi
{
    Task<SonosSessionOutcome> CreateSessionAsync(
        string? accessToken,
        string? groupId,
        SonosSessionRequest? request,
        CancellationToken cancellationToken);
}

/// <summary>
/// WASKA granica WCZYTANIA RADIA do istniejacej sesji - takze dokladnie jedna
/// operacja zapisu, na INNYM zasobie (sessionId, nie groupId).
/// </summary>
public interface ISonosStreamUrlLoadApi
{
    Task<SonosStreamUrlOutcome> LoadStreamUrlAsync(
        string? accessToken,
        string? sessionId,
        string? streamUrl,
        bool playOnCompletion,
        string? itemId,
        CancellationToken cancellationToken);
}
