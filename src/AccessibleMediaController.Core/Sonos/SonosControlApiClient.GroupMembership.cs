using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AccessibleMediaController.Core.Sonos;

/// <summary>
/// ZMIANA SKLADU GRUP SONOSA - transport DWOCH operacji zapisu.
///
/// Zrodlo: definicja OpenAPI 3.0.3 "Sonos Control API (cloud)"
/// v1.56.0-alpha.1-1-gc264f93f-production-cloud:
///  * Groups-CreateGroup-HouseholdId: POST /households/{householdId}/groups/createGroup,
///  * Groups-SetGroupMembers-GroupId: POST /groups/{groupId}/groups/setGroupMembers.
/// Obie odpowiadaja obiektem groupInfo (jedno NULLABLE pole group).
///
/// Caly silnik HTTP jest WSPOLNY z istniejacymi zapisami: ten sam HttpClient, ta
/// sama polityka hosta, Bearer, X-Sonos-Api-Key, kontrola koncowego adresu,
/// deadline obejmujacy naglowki i cialo, budzet tresci, scisle UTF-8 i
/// odrzucanie duplikatow pol. Oba wywolania ida przez <c>WriteCoreAsync</c> z
/// wlaczonym odbiorem ciala, ktore czyta TEN SAM ograniczony czytnik
/// (<c>ReadLimitedJsonAsync</c>) co odczyty. Zaden drugi HttpClient, zaden drugi
/// OAuth, zadna kopia polityki.
///
/// TO SA ZAPISY ZMIENIAJACE SKLAD GRUP U UZYTKOWNIKA. Dlatego:
///  * ZADNEGO ponowienia POST (ani przy 401, ani przy zerwaniu) - drugi POST
///    przestawilby glosniki po raz drugi, juz z innego stanu wyjsciowego,
///  * zadnego Play, Stop ani dosylanego polecenia odtwarzania,
///  * zadnego odczytu topologii tutaj - swiezy odczyt przed i po nalezy do
///    wolajacego,
///  * zadnego odswiezania tokenu ani kasowania konta - to warstwa koordynatora,
///  * HTTP 200 to PRZYJECIE zlecenia, NIE dowod skladu grupy.
///
/// Czego tu NIE MA: areaIds i "Everywhere" (UI poda konkretne logiczne
/// glosniki), nazywania grup, algorytmu planowania grup, odczytu konfliktow,
/// rozdzielania zestawow zbondowanych i jakiegokolwiek UI.
///
/// Do logu nie trafia ani identyfikator grupy, ani glosnika, ani tresc odpowiedzi.
/// </summary>
public sealed partial class SonosControlApiClient : ISonosGroupMembershipApi
{
    /// <summary>
    /// POST /households/{householdId}/groups/createGroup - UTWORZENIE grupy z
    /// podanego zestawu logicznych glosnikow.
    ///
    /// Adres jest zasobem DOMU, nie grupy, wiec identyfikator przechodzi
    /// ISTNIEJACA polityke <see cref="SonosHouseholdIdPolicy"/> - nie
    /// <c>TryGroupUri</c>, ktora trafilaby w inny zasob.
    ///
    /// Zwrocony identyfikator grupy moze byc INNY albo ISTNIEJACY: wg definicji
    /// "This may be an existing group ID if an existing group is a subset of the
    /// new group". Oddajemy FAKTYCZNA wartosc z odpowiedzi, a nie zadana.
    /// </summary>
    public async Task<SonosGroupMembershipOutcome> CreateGroupAsync(
        string? accessToken,
        string? householdId,
        SonosCreateGroupRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null || !SonosHouseholdIdPolicy.TryEncode(householdId, out var encoded))
        {
            return SonosGroupMembershipOutcome.Failure(
                SonosGroupMembershipOperation.CreateGroup, SonosControlApiStatus.InvalidConfiguration, false);
        }

        var uri = new Uri(_configuration.Origin, "households/" + encoded + "/groups/createGroup");
        return await SendMembershipAsync(
            SonosGroupMembershipOperation.CreateGroup, uri, accessToken, CreateGroupBody(request), cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// POST /groups/{groupId}/groups/setGroupMembers - ZASTAPIENIE skladu
    /// istniejacej grupy PELNYM, jawnym zestawem glosnikow.
    ///
    /// Definicja ma playerIds jako opcjonalne i nullable, ale NASZ przeplyw
    /// zawsze zna caly nowy zestaw, wiec brak zestawu jest u nas bledem
    /// wolajacego i NIE wysylamy wtedy niczego. Nie dopisujemy przy tym Sonosowi
    /// wymagalnosci - to NASZA polityka aplikacyjna.
    ///
    /// musicContextGroupId w tym ciele NIE ISTNIEJE - nie wysylamy pola, ktorego
    /// definicja tu nie ma.
    /// </summary>
    public async Task<SonosGroupMembershipOutcome> SetGroupMembersAsync(
        string? accessToken,
        string? groupId,
        SonosPlayerSet? players,
        CancellationToken cancellationToken)
    {
        if (players is null || !TryGroupUri(groupId, "groups/setGroupMembers", out var uri))
        {
            return SonosGroupMembershipOutcome.Failure(
                SonosGroupMembershipOperation.SetGroupMembers, SonosControlApiStatus.InvalidConfiguration, false);
        }

        return await SendMembershipAsync(
            SonosGroupMembershipOperation.SetGroupMembers, uri!, accessToken, PlayerIdsBody(players, null),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// WSPOLNE wyslanie obu operacji: JEDEN POST przez istniejacy silnik, potem
    /// scisly odczyt groupInfo. Zadnego ponowienia i zadnego drugiego zapytania.
    /// </summary>
    private async Task<SonosGroupMembershipOutcome> SendMembershipAsync(
        SonosGroupMembershipOperation operation,
        Uri uri,
        string? accessToken,
        string body,
        CancellationToken cancellationToken)
    {
        var reply = await WriteCoreAsync(uri, accessToken, body, readBody: true, cancellationToken)
            .ConfigureAwait(false);
        if (reply.Status != SonosControlApiStatus.Success || reply.Body is null)
        {
            return SonosGroupMembershipOutcome.Failure(operation, reply.Status, reply.Sent);
        }

        var info = ParseGroupInfo(reply.Body);
        return info is null
            ? SonosGroupMembershipOutcome.Failure(operation, SonosControlApiStatus.InvalidResponse, true)
            : SonosGroupMembershipOutcome.Ok(operation, info);
    }

    /// <summary>
    /// Cialo createGroup: playerIds (wymagane) i musicContextGroupId TYLKO gdy
    /// podane - brak pola znaczy wg definicji grupe BEZ audio. areaIds celowo
    /// pominiete. Kolejnosc pol ustalona, bo testy porownuja cialo doslownie.
    /// </summary>
    private static string CreateGroupBody(SonosCreateGroupRequest request) =>
        PlayerIdsBody(request.Players, request.MusicContextGroupId);

    /// <summary>
    /// WSPOLNY serializator ciala obu operacji. playerIds to WARTOSCI JSON, nie
    /// segmenty adresu: ida LITERALNIE tak, jak podal je odczyt glosnikow - takze
    /// ze spacjami i znakami spoza ASCII - a ucieczki robi serializator. Zadnego
    /// trimowania, normalizacji ani whitelisty, bo poprawiony identyfikator nie
    /// wskazalby tego samego glosnika.
    /// </summary>
    private static string PlayerIdsBody(SonosPlayerSet players, string? musicContextGroupId)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteStartArray("playerIds");
            foreach (var playerId in players.PlayerIds)
            {
                writer.WriteStringValue(playerId);
            }

            writer.WriteEndArray();
            if (musicContextGroupId is not null)
            {
                writer.WriteString("musicContextGroupId", musicContextGroupId);
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    /// <summary>
    /// Scisly odczyt groupInfo. Zwraca null dla KAZDEJ odpowiedzi, ktorej nie
    /// wolno przyjac (nie JSON, nie obiekt, zly typ pola group, niezgodny obiekt
    /// grupy) - wolajacy zamienia to na InvalidResponse.
    ///
    /// NIE zwraca natomiast nulla dla samego BRAKU pola group: w definicji jest
    /// ono NULLABLE i bez listy required, wiec jego brak jest POPRAWNA
    /// odpowiedzia - tylko nie potwierdza skladu. Te dwie rzeczy rozdziela
    /// <see cref="SonosGroupMembershipOutcome.HasGroupId"/>.
    ///
    /// Obiekt grupy jest czytany TYMI SAMYMI pomocnikami i limitami co odebrany
    /// getGroups, bo to DOKLADNIE ten sam schemat "group".
    /// </summary>
    private static SonosGroupInfo? ParseGroupInfo(string body)
    {
        try
        {
            using var document = Parse(body);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (!root.TryGetProperty("group", out var group) || group.ValueKind == JsonValueKind.Null)
            {
                // Pole nullable i nie-required: poprawna odpowiedz BEZ grupy.
                return new SonosGroupInfo(null);
            }

            if (group.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var playerIds = new List<string>();
            foreach (var id in Array(group, "playerIds", SonosGroupMembershipLimits.MaxPlayers, true))
            {
                if (id.ValueKind != JsonValueKind.String)
                {
                    throw new JsonException();
                }

                var value = id.GetString();
                if (string.IsNullOrEmpty(value) || value.Length > SonosGroupMembershipLimits.MaxPlayerIdLength)
                {
                    throw new JsonException();
                }

                playerIds.Add(value);
            }

            // playbackState jest wg definicji oddawany tylko w getGroups, wiec
            // jego brak tutaj jest normalny i daje Unknown, nie blad.
            SonosPlaybackStates.TryParse(Text(group, "playbackState", 128), out var state);
            return new SonosGroupInfo(new SonosGroup(
                Text(group, "id", SonosGroupIdPolicy.MaxLength, true)!,
                Text(group, "name", 69, true)!,
                Text(group, "coordinatorId", SonosGroupMembershipLimits.MaxPlayerIdLength, true)!,
                playerIds,
                state));
        }
        catch (Exception ex) when (IsInvalidData(ex))
        {
            return null;
        }
    }
}
