using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AccessibleMediaController.Core.Sonos;

/// <summary>
/// WLASNE RADIO W SONOSIE - transport DWOCH operacji sesji odtwarzania.
///
/// Zrodlo: definicja OpenAPI 3.0.3 "Sonos Control API (cloud)"
/// v1.56.0-alpha.1-1-gc264f93f-production-cloud:
///  * PlaybackSession-CreateSession-GroupId: POST /groups/{groupId}/playbackSession,
///    cialo PlaybackSession-CreateSessionBody - appId i appContext WYMAGANE,
///    accountId i customData opcjonalne; odpowiedz sessionStatus,
///  * PlaybackSession-LoadStreamUrl-SessionId: POST
///    /playbackSessions/{sessionId}/playbackSession/loadStreamUrl, cialo
///    PlaybackSession-LoadStreamUrlBody - streamUrl WYMAGANY, playOnCompletion,
///    stationMetadata i itemId opcjonalne.
///
/// Caly silnik HTTP (HttpClient, polityka hosta, Bearer, X-Sonos-Api-Key,
/// kontrola koncowego adresu, deadline, budzet tresci, scisle UTF-8) jest
/// WSPOLNY z istniejacymi poleceniami i odczytami: createSession wola ten sam
/// <c>WriteCoreAsync</c> z wlaczonym odbiorem ciala, ktore czyta TEN SAM
/// ograniczony czytnik co odczyty. Zaden drugi HttpClient, zadna kopia polityki.
///
/// TO SA ZAPISY, w szczegolnosci createSession: wg definicji moze WYPRZEC
/// istniejace sesje w grupie, czyli przerwac to, co uzytkownik sluchal. Dlatego:
///  * ZADNEGO ponowienia POST (ani przy 401, ani przy zerwaniu),
///  * zadnego automatycznego tworzenia sesji po eviction czy bledzie,
///  * zadnego dodatkowego Play po autostarcie (playOnCompletion to zalatwia),
///  * zadnego odswiezania tokenu ani kasowania konta - to warstwa koordynatora.
///
/// Czego tu NIE MA: serwera kolejki w chmurze, odtwarzania na zadanie, SMAPI,
/// audioClip, subskrypcji, stationMetadata (pominiete w minimum), accountId
/// uslugi (nie zgadujemy go) i customData.
///
/// NIC z tych operacji nie trafia do logu: ani sessionId, ani adres strumienia,
/// ani appId/appContext, ani tresc odpowiedzi.
/// </summary>
public sealed partial class SonosControlApiClient : ISonosSessionCreateApi, ISonosStreamUrlLoadApi
{
    /// <summary>
    /// POST /groups/{groupId}/playbackSession - UTWORZENIE sesji odtwarzania.
    ///
    /// UWAGA: to ZAPIS, ktory moze przejac grupe. Wolno go wywolac TYLKO z jawnego
    /// zadania uzytkownika; nigdy w sciezce odczytu, wejscia do widoku, listowania
    /// ani w ponowieniu po 401.
    ///
    /// <paramref name="request"/> niesie appId i appContext - JAWNIE od wolajacego,
    /// bez wartosci domyslnych w transporcie. accountId i customData sa celowo
    /// pominiete (patrz <see cref="SonosSessionRequest"/>).
    ///
    /// Odpowiedz jest parsowana scisle: HTTP 200 bez uzytecznego sessionId daje
    /// wynik z Accepted = true, ale Ready = false, bo dalszego polecenia nie ma
    /// gdzie wyslac.
    /// </summary>
    public async Task<SonosSessionOutcome> CreateSessionAsync(
        string? accessToken,
        string? groupId,
        SonosSessionRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null || !TryGroupUri(groupId, "playbackSession", out var uri))
        {
            return SonosSessionOutcome.Failure(SonosControlApiStatus.InvalidConfiguration, false);
        }

        var reply = await WriteCoreAsync(uri!, accessToken, CreateSessionBody(request), readBody: true, cancellationToken)
            .ConfigureAwait(false);
        if (reply.Status != SonosControlApiStatus.Success || reply.Body is null)
        {
            return SonosSessionOutcome.Failure(reply.Status, reply.Sent);
        }

        var session = ParseSessionStatus(reply.Body);
        return session is null
            ? SonosSessionOutcome.Failure(SonosControlApiStatus.InvalidResponse, true)
            : SonosSessionOutcome.Ok(session);
    }

    /// <summary>
    /// POST /playbackSessions/{sessionId}/playbackSession/loadStreamUrl -
    /// WCZYTANIE wlasnego adresu radia do OTWARTEJ sesji.
    ///
    /// To osobna, JAWNA operacja: nie tworzy sesji, nie szuka jej i nie tworzy
    /// ponownie po zamknieciu. Gdy Sonos odpowie NotFound (sesja zamknieta albo
    /// przejeta), wynik to blad - nie cicha proba utworzenia nowej sesji, bo ta
    /// przerwalaby to, co wlasnie gra.
    ///
    /// <paramref name="playOnCompletion"/> jest OBOWIAZKOWY i bez domyslnej
    /// wartosci: autostart zmienia zachowanie u uzytkownika. Gdy jest true, NIE
    /// wolno dosylac osobnego Play.
    ///
    /// <paramref name="itemId"/> jest opcjonalny (null = pole pominiete) i sluzy
    /// korelacji przyszlych zdarzen playbackStatus. stationMetadata pomijamy.
    ///
    /// Adres strumienia NIE jest przez nas pobierany: zadnego GET, nawet
    /// sprawdzajacego.
    /// </summary>
    public async Task<SonosStreamUrlOutcome> LoadStreamUrlAsync(
        string? accessToken,
        string? sessionId,
        string? streamUrl,
        bool playOnCompletion,
        string? itemId,
        CancellationToken cancellationToken)
    {
        if (!SonosStreamUrlPolicy.IsAcceptable(streamUrl)
            || !IsAcceptableStreamItemId(itemId)
            || !TrySessionUri(sessionId, "playbackSession/loadStreamUrl", out var uri))
        {
            return SonosStreamUrlOutcome.FromStatus(SonosControlApiStatus.InvalidConfiguration, false);
        }

        var reply = await WriteAsync(uri!, accessToken, LoadStreamUrlBody(streamUrl!, playOnCompletion, itemId),
            cancellationToken).ConfigureAwait(false);
        return SonosStreamUrlOutcome.FromStatus(reply.Status, reply.Sent);
    }

    /// <summary>
    /// Adres zasobu SESJI. Osobna metoda od <c>TryGroupUri</c>, bo to INNY zasob:
    /// sciezka /playbackSessions/{sessionId}/..., a identyfikator przechodzi
    /// polityke sessionId, nie polityke groupId. Przepuszczenie tu groupId albo
    /// odwrotnie trafiloby w nieistniejacy zasob.
    /// </summary>
    private bool TrySessionUri(string? sessionId, string suffix, out Uri? uri)
    {
        uri = null;
        if (!SonosSessionIdPolicy.TryEncode(sessionId, out var encoded))
        {
            return false;
        }

        uri = new Uri(_configuration.Origin, "playbackSessions/" + encoded + "/" + suffix);
        return true;
    }

    /// <summary>
    /// itemId w ciele loadStreamUrl: null znaczy POMIN POLE (to nie to samo co
    /// pusty napis). Gdy podany, obowiazuje ta sama bramka co dla innych
    /// identyfikatorow w ciele, z limitem 128 z definicji.
    /// </summary>
    private static bool IsAcceptableStreamItemId(string? itemId) =>
        itemId is null || IsAcceptableBodyId(itemId, SonosPlaybackSessionLimits.MaxItemIdLength);

    /// <summary>
    /// Cialo createSession budowane SCISLYM serializatorem: appContext pochodzi
    /// od wolajacego i moze zawierac cudzyslow, ukosnik albo znaki spoza ASCII,
    /// ktore reczne sklejanie uszkodziloby. Kolejnosc pol ustalona (appId,
    /// appContext), bo testy porownuja cialo doslownie.
    ///
    /// accountId i customData sa POMINIETE - nie wysylamy pol, ktorych wartosci
    /// nie znamy.
    /// </summary>
    private static string CreateSessionBody(SonosSessionRequest request)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("appId", request.AppId);
            writer.WriteString("appContext", request.AppContext);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    /// <summary>
    /// Cialo loadStreamUrl: streamUrl, playOnCompletion zawsze JAWNIE, itemId
    /// tylko gdy podany. stationMetadata CELOWO pominiete - minimalne radio nie
    /// ma czym opisac stacji, a wyslanie pustych metadanych nadpisaloby to, co
    /// gloshnik pokazuje.
    /// </summary>
    private static string LoadStreamUrlBody(string streamUrl, bool playOnCompletion, string? itemId)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("streamUrl", streamUrl);
            writer.WriteBoolean("playOnCompletion", playOnCompletion);
            if (itemId is not null)
            {
                writer.WriteString("itemId", itemId);
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    /// <summary>
    /// Scisly odczyt sessionStatus. Zwraca null, gdy ciala nie da sie uznac za
    /// te odpowiedz (nie JSON, nie obiekt, zle typy pol, sessionId dluzszy niz
    /// 46 znakow) - wolajacy zamienia to na InvalidResponse.
    ///
    /// NIE zwraca natomiast nulla dla samego BRAKU sessionId: definicja ma to
    /// pole jako nullable, wiec jego brak jest poprawna odpowiedzia, tylko NIE
    /// JEST gotowa sesja. Te dwie rzeczy rozdziela <see cref="SonosSessionOutcome.Ready"/>.
    /// </summary>
    private static SonosSessionStatus? ParseSessionStatus(string body)
    {
        try
        {
            using var document = Parse(body);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            string? sessionId = null;
            if (root.TryGetProperty("sessionId", out var sessionIdElement)
                && sessionIdElement.ValueKind != JsonValueKind.Null)
            {
                if (sessionIdElement.ValueKind != JsonValueKind.String)
                {
                    return null;
                }

                sessionId = sessionIdElement.GetString();
                if (sessionId is not null && sessionId.Length > SonosPlaybackSessionLimits.MaxSessionIdLength)
                {
                    // Dluzszy niz dopuszcza definicja - nie przyjmujemy go, bo
                    // obciecie dalo by identyfikator INNEJ (albo zadnej) sesji.
                    return null;
                }
            }

            var state = SonosSessionState.Unknown;
            if (root.TryGetProperty("sessionState", out var stateElement)
                && stateElement.ValueKind != JsonValueKind.Null)
            {
                if (stateElement.ValueKind != JsonValueKind.String)
                {
                    return null;
                }

                // Nieznana wartosc zostaje Unknown: definicja zapowiada zmiany
                // tego pola, a jego brak i tak nie jest warunkiem gotowosci.
                if (string.Equals(stateElement.GetString(), "SESSION_STATE_CONNECTED", StringComparison.Ordinal))
                {
                    state = SonosSessionState.Connected;
                }
            }

            bool? created = null;
            if (root.TryGetProperty("sessionCreated", out var createdElement)
                && createdElement.ValueKind != JsonValueKind.Null)
            {
                if (createdElement.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                {
                    return null;
                }

                created = createdElement.GetBoolean();
            }

            return new SonosSessionStatus(sessionId, state, created);
        }
        catch (Exception ex) when (IsInvalidData(ex))
        {
            return null;
        }
    }
}
