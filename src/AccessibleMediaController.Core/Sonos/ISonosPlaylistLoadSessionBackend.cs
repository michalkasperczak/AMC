using System.Threading;
using System.Threading.Tasks;

namespace AccessibleMediaController.Core.Sonos;

/// <summary>
/// OPCJONALNA granica URUCHOMIENIA PLAYLISTY dla zaplecza sesji Sonos.
///
/// Rodzenstwo <see cref="ISonosFavoriteLoadSessionBackend"/> i ten sam rozmysl:
/// granica ZAPISU jest OSOBNA od granicy ODCZYTU playlist i od obowiazkowego
/// interfejsu grup, bo dolozenie metody POST do nich zlamaloby wszystkie
/// istniejace atrapy. Brak tej granicy to uczciwe "to zaplecze nie umie
/// uruchamiac playlist" - nie awaria i nie udawane uruchomienie.
///
/// Dokladnie JEDNA operacja i dokladnie JEDEN POST. <paramref name="action"/>
/// oraz <paramref name="playOnCompletion"/> sa OBOWIAZKOWE i bez wartosci
/// domyslnych - tak samo jak w kliencie i koordynatorze, bo roznica miedzy
/// dopisaniem i zastapieniem kolejki uzytkownika jest nieodwracalna i nie moze
/// zalezec od milczenia wolajacego.
///
/// Zadnego presetu, zadnej trwalosci, zadnego drugiego polecenia odtwarzania i
/// zadnego wlasnego HTTP/OAuth - uzywamy ISTNIEJACEGO Core.
/// </summary>
public interface ISonosPlaylistLoadSessionBackend
{
    /// <summary>
    /// POST /groups/{groupId}/playlists dla WSKAZANEJ grupy i WSKAZANEJ
    /// playlisty. Oba identyfikatory ida LITERALNIE - to warstwa transportu je
    /// waliduje PRZED HTTP, a wolajacy nie ma prawa podstawic tu nazwy zamiast
    /// identyfikatora ani identyfikatora domu zamiast grupy.
    /// </summary>
    Task<SonosGroupCommandResult> LoadPlaylistAsync(
        string? groupId,
        string? playlistId,
        SonosFavoriteQueueAction action,
        bool playOnCompletion,
        CancellationToken cancellationToken);
}
