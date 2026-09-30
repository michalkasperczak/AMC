using System.Threading;
using System.Threading.Tasks;

namespace AccessibleMediaController.Core.Sonos;

/// <summary>
/// F3c: OPCJONALNA granica URUCHOMIENIA ULUBIONEGO dla zaplecza sesji Sonos.
///
/// Jest OSOBNA od <see cref="ISonosGroupSessionBackend"/> i od
/// <see cref="ISonosFavoritesSessionBackend"/> z tego samego rozmyslu, ktory
/// stworzyl granice odczytu ulubionych: dodanie metody ZAPISU do obowiazkowego
/// interfejsu grup albo do granicy ODCZYTU ulubionych zlamaloby wszystkie
/// istniejace atrapy, ktore o ladowaniu ulubionego nie wiedza i wiedziec nie
/// musza. Tutaj zaplecze DOKLADA jedna umiejetnosc, a wolajacy sprawdza ja
/// zwyklym rzutowaniem: brak tej granicy to uczciwe "to zaplecze nie umie
/// uruchamiac ulubionych" - nie awaria i nie udawane uruchomienie.
///
/// Dokladnie JEDNA operacja i dokladnie JEDEN POST. <paramref name="action"/>
/// oraz <paramref name="playOnCompletion"/> sa OBOWIAZKOWE i bez wartosci
/// domyslnych - tak samo jak w kliencie (F3a) i koordynatorze (F3b), bo roznica
/// miedzy dopisaniem i zastapieniem kolejki uzytkownika jest nieodwracalna i nie
/// moze zalezec od milczenia wolajacego.
///
/// Zadnego presetu, zadnej trwalosci, zadnego drugiego polecenia odtwarzania.
/// </summary>
public interface ISonosFavoriteLoadSessionBackend
{
    /// <summary>
    /// POST /groups/{groupId}/favorites dla WSKAZANEJ grupy i WSKAZANEGO
    /// ulubionego. Oba identyfikatory ida LITERALNIE - to warstwa transportu je
    /// waliduje PRZED HTTP, a wolajacy nie ma prawa podstawic tu nazwy zamiast
    /// identyfikatora ani identyfikatora domu zamiast grupy.
    /// </summary>
    Task<SonosGroupCommandResult> LoadFavoriteAsync(
        string? groupId,
        string? favoriteId,
        SonosFavoriteQueueAction action,
        bool playOnCompletion,
        CancellationToken cancellationToken);
}
