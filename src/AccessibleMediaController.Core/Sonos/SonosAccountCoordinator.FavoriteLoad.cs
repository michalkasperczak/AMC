using System;
using System.Threading;
using System.Threading.Tasks;

namespace AccessibleMediaController.Core.Sonos;

/// <summary>
/// ZALADOWANIE ULUBIONEGO do wspolnej kolejki grupy przez ISTNIEJACEGO
/// wlasciciela konta (F3b). To CIENKIE podlaczenie jednej operacji zapisu do
/// JUZ ODEBRANEGO przebiegu polecen grupy
/// (<see cref="RunGroupCommandAsync"/>) - zadnej wlasnej polityki, zadnej kopii
/// logiki biletu, odnawiania, generacji ani transportu.
///
/// Z tego bierze sie CALE zachowanie, ktore obowiazuje tu bez wyjatku:
///   * brak konta: ZERO zapytan i ZERO odnowien,
///   * znana MINIONA waznosc: DOKLADNIE JEDNO odnowienie PRZED jedynym POST,
///     nieznana waznosc nie odnawia niczego,
///   * potem DOKLADNIE JEDEN POST - takze po 401, 403, 404, 429, 5xx i po
///     utraconej odpowiedzi nie ma ani powtorzenia, ani odnowienia, ani
///     kasowania konta. Zaladowanie ulubionego dopisuje albo ZASTEPUJE kolejke
///     uzytkownika, wiec drugi POST dalby inny skutek niz pierwszy,
///   * HTTP 200 to PRZYJECIE zlecenia, nie dowod, ze muzyka zagrala
///     (<see cref="SonosGroupCommandResult.EffectConfirmed"/> zawsze false),
///   * porzucenie JUZ WYSLANEGO polecenia nie udaje cofniecia: proba zostaje
///     rozdzielona od nieznanego skutku.
///
/// Czego tu celowo NIE MA: walidacji identyfikatora ulubionego, grupy i akcji -
/// ma ja ODEBRANY klient (<see cref="SonosControlApiClient.LoadFavoriteAsync"/>)
/// i to on odrzuca zle wejscie PRZED HTTP. Druga bramka w koordynatorze byla by
/// tylko drugim, rozjezdzajacym sie zrodlem prawdy.
/// </summary>
public sealed partial class SonosAccountCoordinator
{
    /// <summary>
    /// POST /groups/{groupId}/favorites przez bilet biezacego konta.
    ///
    /// <paramref name="action"/> i <paramref name="playOnCompletion"/> sa
    /// OBOWIAZKOWE i BEZ wartosci domyslnych - dokladnie jak w kliencie. Roznica
    /// miedzy dopisaniem i zastapieniem kolejki uzytkownika jest nieodwracalna i
    /// nie moze zalezec od milczenia wolajacego.
    /// </summary>
    public async Task<SonosGroupCommandResult> LoadFavoriteAsync(
        ISonosFavoriteLoadApi api,
        string? groupId,
        string? favoriteId,
        SonosFavoriteQueueAction action,
        bool playOnCompletion,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(api);
        return await RunGroupCommandAsync(
            SonosGroupCommand.LoadFavorite,
            (token, ct) => api.LoadFavoriteAsync(token, groupId, favoriteId, action, playOnCompletion, ct),
            cancellationToken).ConfigureAwait(false);
    }
}
