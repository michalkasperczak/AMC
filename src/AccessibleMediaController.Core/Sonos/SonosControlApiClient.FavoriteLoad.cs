using System;
using System.Threading;
using System.Threading.Tasks;

namespace AccessibleMediaController.Core.Sonos;

/// <summary>
/// ZALADOWANIE ULUBIONEGO do wspolnej kolejki grupy (F3a).
/// POST /groups/{groupId}/favorites, operacja Favorites-LoadFavorite-GroupId.
///
/// ETAP RED: sygnatura istnieje, zeby test poprawnego wywolania sie KOMPILOWAL,
/// ale zadnego zadania jeszcze nie wysyla. Test jednego prawidlowego POST musi
/// wiec ZAWIESC zachowaniem (brak POST), a nie bledem kompilacji.
/// </summary>
public sealed partial class SonosControlApiClient : ISonosFavoriteLoadApi
{
    public Task<SonosGroupCommandOutcome> LoadFavoriteAsync(
        string? accessToken,
        string? groupId,
        string? favoriteId,
        SonosFavoriteQueueAction action,
        bool playOnCompletion,
        CancellationToken cancellationToken) =>
        Task.FromResult(SonosGroupCommandOutcome.FromStatus(
            SonosGroupCommand.LoadFavorite, SonosControlApiStatus.InvalidConfiguration, false));
}
