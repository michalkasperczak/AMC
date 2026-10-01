using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AccessibleMediaController.Core.Sonos;

/// <summary>
/// ZALADOWANIE ULUBIONEGO do wspolnej kolejki grupy (F3a).
///
/// Zrodlo: definicja OpenAPI 3.0.3 "Sonos Control API (cloud)"
/// v1.56.0-alpha.1-1-gc264f93f-production-cloud, operacja
/// Favorites-LoadFavorite-GroupId: POST /groups/{groupId}/favorites, cialo
/// Favorites-LoadFavoriteBody (favoriteId wymagane, maxLength 36; action z
/// enuma queueAction; playOnCompletion bool; playModes opcjonalne).
///
/// Ta czesc klienta korzysta DOKLADNIE z tego samego transportu co pozostale
/// polecenia grupy - prywatne SendAsync, TryGroupUri i WriteAsync: staly host
/// HTTPS, brak przekierowan i ciasteczek, naglowki na POJEDYNCZYM zadaniu,
/// deadline obejmujacy naglowki i cialo, sprawdzenie koncowego adresu. Zaden
/// nowy HttpClient tu nie powstaje i zadna polityka transportu nie jest
/// kopiowana ani liberalizowana.
///
/// Czego tu NIE MA i byc nie moze:
///  * ZADNEGO ponowienia POST - polecenie dopisuje albo zastepuje kolejke
///    uzytkownika, wiec dwa razy da inny skutek niz raz,
///  * zadnej domyslnej akcji - definicja milczaco przyjmuje "append", ale AMC
///    zada JAWNEGO wyboru, bo REPLACE niszczy kolejke uzytkownika,
///  * zadnego playModes - pominiecie pola ZACHOWUJE tryby gloshnika, a jawne
///    false by je wylaczylo; transport nie ma prawa ich ruszac,
///  * zadnej weryfikacji SKUTKU - HTTP 200 ({} wg definicji) to PRZYJECIE
///    zlecenia, nie dowod, ze muzyka zagrala,
///  * zadnego odswiezania tokenu ani kasowania konta przy 401 - to warstwa
///    koordynatora, nie transport.
/// </summary>
public sealed partial class SonosControlApiClient : ISonosFavoriteLoadApi
{
    /// <summary>
    /// POST /groups/{groupId}/favorites - zaladowanie ulubionego.
    ///
    /// <paramref name="action"/> i <paramref name="playOnCompletion"/> sa
    /// OBOWIAZKOWE i nie maja wartosci domyslnych: wybor polityki nalezy do
    /// wolajacego, a nie do transportu.
    /// </summary>
    public Task<SonosGroupCommandOutcome> LoadFavoriteAsync(
        string? accessToken,
        string? groupId,
        string? favoriteId,
        SonosFavoriteQueueAction action,
        bool playOnCompletion,
        CancellationToken cancellationToken)
    {
        if (!IsAcceptableFavoriteId(favoriteId) || !SonosFavoriteQueueActions.IsDefined(action))
        {
            return Task.FromResult(SonosGroupCommandOutcome.FromStatus(
                SonosGroupCommand.LoadFavorite, SonosControlApiStatus.InvalidConfiguration, false));
        }

        return SendAsync(accessToken, groupId, SonosGroupCommand.LoadFavorite,
            LoadFavoriteBody(favoriteId!, action, playOnCompletion), cancellationToken);
    }

    /// <summary>
    /// Cialo budowane SCISLYM serializatorem JSON, nie sklejane recznie:
    /// favoriteId pochodzi z odpowiedzi Sonosa i moze zawierac cudzyslow,
    /// odwrotny ukosnik, spacje i znaki spoza ASCII. Reczne sklejanie wyslaloby
    /// dla nich uszkodzony JSON.
    ///
    /// Kolejnosc pol jest ustalona (favoriteId, action, playOnCompletion), a
    /// playModes CELOWO NIE MA - brak pola zachowuje tryby odtwarzania
    /// gloshnika, jawne false by je wylaczylo.
    /// </summary>
    private static string LoadFavoriteBody(string favoriteId, SonosFavoriteQueueAction action, bool playOnCompletion) =>
        QueueLoadBody("favoriteId", favoriteId, action, playOnCompletion);

    /// <summary>
    /// WSPOLNE cialo polecenia kolejki (ulubione: favoriteId, playlisty:
    /// playlistId). Nazwa pola jest JAWNYM argumentem, bo oba zasoby maja
    /// wlasna - pomylenie ich dalaby Sonosowi ERROR_MISSING_PARAMETERS.
    ///
    /// Kolejnosc pol jest ustalona (identyfikator, action, playOnCompletion), a
    /// playModes CELOWO NIE MA w zadnym z przypadkow.
    /// </summary>
    private static string QueueLoadBody(
        string idField, string id, SonosFavoriteQueueAction action, bool playOnCompletion)
    {
        using var buffer = new MemoryStream();
        // Domyslny koder ucieka znaki spoza ASCII; wartosc po odkodowaniu jest
        // ta sama, wiec Sonos dostaje DOKLADNIE to, co przyszlo z odczytu.
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString(idField, id);
            writer.WriteString("action", SonosFavoriteQueueActions.WireValue(action));
            writer.WriteBoolean("playOnCompletion", playOnCompletion);
            writer.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(buffer.ToArray());
    }

    /// <summary>
    /// favoriteId w ciele polecenia. Dokladnie ta sama bramka co przy ODCZYCIE
    /// (F1): niepuste i do <see cref="SonosFavoritesLimits.MaxFavoriteIdLength"/>
    /// znakow. Caly sprawdzian siedzi we WSPOLNYM
    /// <see cref="IsAcceptableBodyId"/>, zeby ulubione i playlisty nie mialy
    /// dwoch rozjezdzajacych sie kopii tej samej reguly.
    /// </summary>
    private static bool IsAcceptableFavoriteId(string? favoriteId) =>
        IsAcceptableBodyId(favoriteId, SonosFavoritesLimits.MaxFavoriteIdLength);

    /// <summary>
    /// WSPOLNA bramka identyfikatora przenoszonego w CIELE polecenia (ulubione,
    /// playlisty). To NIE jest walidacja segmentu adresu: identyfikator idzie w
    /// JSON, wiec nie ma tu ani kodowania procentowego, ani zakazu ukosnika.
    ///
    /// IsNullOrEmpty, NIE IsNullOrWhiteSpace - jesli Sonos zwrocil identyfikator
    /// ze samych spacji, model odczytu go przyjmuje, wiec zapis tez musi umiec
    /// go odeslac.
    ///
    /// Zadnego trim, obcinania, normalizacji ani whitelisty ASCII: nie
    /// naprawiamy cudzych identyfikatorow, bo poprawiony nie wskaze niczego.
    ///
    /// Odrzucamy natomiast NIEPOPRAWNY UTF-16 (samotny surogat) - serializator
    /// zastapilby go znakiem zastepczym i wyslalibysmy po cichu INNA wartosc
    /// niz podal wolajacy.
    /// </summary>
    private static bool IsAcceptableBodyId(string? id, int maxLength)
    {
        if (string.IsNullOrEmpty(id) || id.Length > maxLength)
        {
            return false;
        }

        for (var index = 0; index < id.Length; index++)
        {
            var character = id[index];
            if (!char.IsSurrogate(character))
            {
                continue;
            }

            if (!char.IsHighSurrogate(character)
                || index + 1 >= id.Length
                || !char.IsLowSurrogate(id[index + 1]))
            {
                return false;
            }

            index++;
        }

        return true;
    }
}
