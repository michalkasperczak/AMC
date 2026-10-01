using System.Threading;
using System.Threading.Tasks;

namespace AccessibleMediaController.Core.Sonos;

/// <summary>
/// OPCJONALNA granica ODCZYTU PLAYLIST dla zaplecza sesji Sonos.
///
/// Rodzenstwo <see cref="ISonosFavoritesSessionBackend"/> i ten sam rozmysl:
/// granica jest OSOBNA od obowiazkowego <see cref="ISonosGroupSessionBackend"/>,
/// bo dolozenie metody do interfejsu grup zlamaloby wszystkie istniejace atrapy
/// zaplecza, ktore o playlistach nie wiedza i wiedziec nie musza. Zaplecze
/// DOKLADA jedna umiejetnosc, a wolajacy sprawdza ja zwyklym rzutowaniem: brak
/// tej granicy to uczciwe "to zaplecze nie umie playlist", nie awaria.
///
/// Dokladnie JEDNA operacja i tylko GET: zadnego loadPlaylist, zadnego POST,
/// zadnego presetu. Uruchamianie ma WLASNA granice
/// <see cref="ISonosPlaylistLoadSessionBackend"/>.
///
/// Tu NIE MA zadnego wlasnego HTTP, OAuth ani generowania naglowkow - caly ten
/// kod JUZ ISTNIEJE w kliencie i koordynatorze; to tylko waska granica sesji.
/// </summary>
public interface ISonosPlaylistsSessionBackend
{
    /// <summary>
    /// ODCZYT playlist WSKAZANEGO domu. <paramref name="householdId"/> idzie
    /// LITERALNIE - to warstwa transportu waliduje segment sciezki. Wolajacy NIE
    /// ma prawa podstawic tu identyfikatora grupy.
    ///
    /// PUSTA lista jest POPRAWNYM sukcesem: definicja mowi, ze pole playlists
    /// jest opcjonalne, wiec dom bez playlist to nie blad.
    /// </summary>
    Task<SonosPlaylistsReadResult> ReadPlaylistsAsync(
        string? householdId, CancellationToken cancellationToken);
}
