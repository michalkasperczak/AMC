using System.Threading;
using System.Threading.Tasks;

namespace AccessibleMediaController.Core.Sonos;

/// <summary>
/// URUCHOMIENIE PLAYLISTY SONOSA we wspolnej kolejce grupy.
///
/// Zrodlo: definicja OpenAPI 3.0.3 "Sonos Control API (cloud)"
/// v1.56.0-alpha.1-1-gc264f93f-production-cloud, operacja
/// Playlists-LoadPlaylist-GroupId: POST /groups/{groupId}/playlists, cialo
/// Playlists-LoadPlaylistBody - playlistId WYMAGANE (maxLength 36), action z
/// enuma queueAction (REPLACE, APPEND, INSERT, INSERT_NEXT, PLAY_NOW)
/// opcjonalne, playOnCompletion opcjonalne, playModes opcjonalne.
///
/// Pole nazywa sie playlistId, NIE favoriteId - to inny zasob i wyslanie cudzej
/// nazwy pola dalo by ERROR_MISSING_PARAMETERS. Caly transport (SendAsync,
/// TryGroupUri, deadline, kontrola koncowego adresu) i caly sprawdzian
/// identyfikatora w ciele oraz budowanie JSON sa WSPOLNE z ulubionymi - zaden
/// drugi silnik, zaden nowy HttpClient, zadna kopia polityki.
///
/// Czego tu NIE MA i byc nie moze:
///  * ZADNEGO ponowienia POST - polecenie zmienia kolejke uzytkownika,
///  * zadnej domyslnej akcji - mimo ze definicja pozwala pominac action, AMC
///    zada JAWNEGO wyboru, bo REPLACE niszczy kolejke uzytkownika,
///  * zadnego playModes - pominiecie pola ZACHOWUJE tryby gloshnika,
///  * zadnego dodatkowego Play po tym poleceniu,
///  * zadnej weryfikacji SKUTKU - HTTP 200 to PRZYJECIE zlecenia, nie dowod,
///    ze muzyka zagrala,
///  * zadnego odswiezania tokenu ani kasowania konta przy 401 - to warstwa
///    koordynatora, nie transport.
/// </summary>
public sealed partial class SonosControlApiClient : ISonosPlaylistLoadApi
{
    /// <summary>
    /// POST /groups/{groupId}/playlists - uruchomienie playlisty.
    ///
    /// <paramref name="action"/> i <paramref name="playOnCompletion"/> sa
    /// OBOWIAZKOWE i nie maja wartosci domyslnych: wybor polityki nalezy do
    /// wolajacego, a nie do transportu. Tak samo jak przy ulubionych.
    ///
    /// playlistId idzie LITERALNIE z odczytu listy - bez trim, bez zmiany
    /// wielkosci liter i bez walidacji jak segment adresu, bo jedzie w ciele
    /// JSON, a nie w URL.
    /// </summary>
    public Task<SonosGroupCommandOutcome> LoadPlaylistAsync(
        string? accessToken,
        string? groupId,
        string? playlistId,
        SonosFavoriteQueueAction action,
        bool playOnCompletion,
        CancellationToken cancellationToken)
    {
        if (!IsAcceptablePlaylistId(playlistId) || !SonosFavoriteQueueActions.IsDefined(action))
        {
            return Task.FromResult(SonosGroupCommandOutcome.FromStatus(
                SonosGroupCommand.LoadPlaylist, SonosControlApiStatus.InvalidConfiguration, false));
        }

        return SendAsync(accessToken, groupId, SonosGroupCommand.LoadPlaylist,
            QueueLoadBody("playlistId", playlistId!, action, playOnCompletion), cancellationToken);
    }

    /// <summary>
    /// playlistId w ciele polecenia: ta sama bramka co przy ODCZYCIE listy,
    /// tylko z limitem playlist (<see cref="SonosPlaylistsLimits.MaxPlaylistIdLength"/>).
    /// Logika siedzi we WSPOLNYM IsAcceptableBodyId - jedna regula, dwa zasoby.
    /// </summary>
    private static bool IsAcceptablePlaylistId(string? playlistId) =>
        IsAcceptableBodyId(playlistId, SonosPlaylistsLimits.MaxPlaylistIdLength);
}
