using System.Threading;
using System.Threading.Tasks;

namespace AccessibleMediaController.Core.Sonos;

/// <summary>
/// F2: OPCJONALNA granica ODCZYTU ULUBIONYCH dla zaplecza sesji Sonos.
///
/// Jest OSOBNA od <see cref="ISonosGroupSessionBackend"/> z rozmyslu: dodanie
/// metody do obowiazkowego interfejsu grup zlamaloby WSZYSTKIE istniejace
/// atrapy zaplecza w pomiarach sesji, ktore o ulubionych nie wiedza i wiedziec
/// nie musza. Tutaj zaplecze DOKLADA jedna umiejetnosc, a wolajacy sprawdza ja
/// zwyklym rzutowaniem: brak tej granicy to uczciwe "to zaplecze nie umie
/// ulubionych", nie awaria.
///
/// Dokladnie JEDNA operacja i tylko GET: zadnego loadFavorite, zadnego POST,
/// zadnego presetu - to dopiero F3 z osobnym kontraktem.
/// </summary>
public interface ISonosFavoritesSessionBackend
{
    /// <summary>
    /// ODCZYT ulubionych WSKAZANEGO domu. <paramref name="householdId"/> idzie
    /// LITERALNIE - to warstwa transportu waliduje segment sciezki. Wolajacy
    /// NIE ma prawa podstawic tu identyfikatora grupy.
    /// </summary>
    Task<SonosFavoritesReadResult> ReadFavoritesAsync(
        string? householdId, CancellationToken cancellationToken);
}
