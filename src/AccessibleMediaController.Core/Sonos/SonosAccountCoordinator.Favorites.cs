using System;
using System.Threading;
using System.Threading.Tasks;

namespace AccessibleMediaController.Core.Sonos;

/// <summary>
/// F1b: ODCZYT ULUBIONYCH domu przez ISTNIEJACEGO wlasciciela konta.
///
/// Ta czesc koordynatora jest CIENKA z zamyslu. Cala droga poswiadczen -
/// bilet pod blokada, HTTP poza blokada, jedno odnowienie przy znanej minionej
/// waznosci, po 401 najwyzej jedno odnowienie i jedno powtorzenie, kontrola
/// ORYGINALNEJ generacji po kazdym await - jest JUZ rozstrzygnieta we
/// wspolnym <see cref="RunGroupReadAsync{TValue}"/>. Nie ma tu ani kopii
/// biletu, ani kopii odnawiania, ani wlasnej blokady, ani wlasnej polityki
/// identyfikatora domu.
///
/// Czego tu NIE MA i byc nie moze na tym etapie:
///  * ZADNEGO POST (loadFavorite i odtwarzanie ulubionego to osobny etap),
///  * zadnego cache, pollingu ani subskrypcji favoritesVersionChange,
///  * zadnego wlasciciela Windows, UI, XAML, skrotow, presetow ani zapisu
///    ustawien - to dopiero F2,
///  * zadnego pobierania NOWEJ migawki konta po await w celu nadania starej
///    odpowiedzi nowej tozsamosci: migawke niesie wynik wspolnego odczytu.
/// </summary>
public sealed partial class SonosAccountCoordinator
{
    /// <summary>
    /// GET /households/{householdId}/favorites przez bilet biezacego konta.
    ///
    /// <paramref name="householdId"/> idzie do klienta LITERALNIE - to on
    /// waliduje segment sciezki PRZED wyslaniem HTTP i sam zwraca
    /// InvalidConfiguration, dokladnie jak przy odczycie grup. Koordynator nie
    /// trimuje, nie podmienia i nie zgaduje domu.
    /// </summary>
    public async Task<SonosFavoritesReadResult> ReadFavoritesAsync(
        ISonosFavoritesApi api,
        string? householdId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(api);
        var read = await RunGroupReadAsync<SonosFavoritesList>(
            async (token, ct) =>
            {
                var outcome = await api.GetFavoritesAsync(token, householdId, ct).ConfigureAwait(false);
                return (outcome.Status, outcome.Favorites);
            },
            cancellationToken).ConfigureAwait(false);

        return SonosFavoritesReadResult.From(read);
    }
}

/// <summary>
/// Stale komunikaty PL odczytu ULUBIONYCH. Osobne od komunikatow odczytu
/// urzadzen, bo uzytkownik ma uslyszec, czego naprawde dotyczyl odczyt - a nie
/// odziedziczonej "listy urzadzen".
///
/// Zaden tekst nie zawiera tokenu, klucza integracji, identyfikatora domu,
/// nazwy ani identyfikatora ulubionego, ani surowego globalError.reason.
/// </summary>
public static class SonosFavoritesReadMessages
{
    public static string Describe(SonosDeviceReadStatus status) => status switch
    {
        SonosDeviceReadStatus.Success => "Lista ulubionych Sonos została odczytana.",
        SonosDeviceReadStatus.NoAccount =>
            "Nie ma połączonego konta Sonos, więc nie ma skąd odczytać ulubionych.",
        // PORZUCENIE nie orzeka JEDNEJ przyczyny: mogla to byc zmiana konta,
        // wylogowanie albo zakonczenie pracy wlasciciela.
        SonosDeviceReadStatus.Discarded =>
            "Odczyt ulubionych Sonos został pominięty: kontekst konta zmienił się w trakcie. Spróbuj odświeżyć.",
        SonosDeviceReadStatus.InvalidConfiguration =>
            "Odczyt ulubionych nie został wysłany: brak klucza integracji albo niepoprawny dom.",
        SonosDeviceReadStatus.Unauthorized =>
            "Sonos nie przyjął dostępu do konta. Odnów dostęp albo zaloguj się ponownie.",
        SonosDeviceReadStatus.Forbidden => "Konto Sonos nie ma uprawnień do odczytu ulubionych.",
        SonosDeviceReadStatus.NotFound => "Sonos nie znalazł wskazanego domu.",
        SonosDeviceReadStatus.RequestRejected => "Sonos odrzucił zapytanie o ulubione.",
        SonosDeviceReadStatus.RateLimited => "Sonos chwilowo ogranicza liczbę zapytań. Spróbuj później.",
        SonosDeviceReadStatus.CommandFailed => "Sonos nie wykonał zapytania o ulubione.",
        SonosDeviceReadStatus.ServiceError => "Usługa Sonos zgłosiła błąd.",
        SonosDeviceReadStatus.Unreachable => "Nie udało się połączyć z usługą Sonos.",
        SonosDeviceReadStatus.Canceled => "Odczyt ulubionych Sonos został anulowany.",
        SonosDeviceReadStatus.RedirectRefused =>
            "Sonos próbował przekierować zapytanie; zostało zatrzymane.",
        _ => "Odpowiedź Sonos o ulubionych była niezgodna z oczekiwaną."
    };
}

/// <summary>
/// Wynik ODCZYTU ULUBIONYCH widziany przez konto. Jest to WYLACZNIE mapowanie
/// juz rozstrzygnietego <see cref="SonosGroupReadResult{TValue}"/>: zadnego
/// nowego algorytmu konta, zadnej nowej migawki, zadnej zmiany statusu.
///
/// Dane wychodza TYLKO przy sukcesie. Wynik PORZUCONY albo bledny nie przenosi
/// starej listy - stare ulubione nie moga pojawic sie pod nowa tozsamoscia.
/// </summary>
public sealed class SonosFavoritesReadResult
{
    internal SonosFavoritesReadResult(
        SonosDeviceReadStatus status,
        SonosFavoritesList? favorites,
        bool renewed,
        SonosAccountSnapshot snapshot)
    {
        Status = status;
        // Brak danych przy KAZDYM niepowodzeniu i przy porzuceniu.
        Favorites = status == SonosDeviceReadStatus.Success ? favorites : null;
        Renewed = renewed;
        Snapshot = snapshot;
    }

    public SonosDeviceReadStatus Status { get; }

    /// <summary>
    /// Lista ulubionych TYLKO przy sukcesie. Niesie LITERALNY identyfikator
    /// domu z zapytania - wstawia go klient, nie koordynator.
    /// </summary>
    public SonosFavoritesList? Favorites { get; }

    /// <summary>Czy po drodze uzyto ISTNIEJACEGO odnowienia dostepu (najwyzej raz).</summary>
    public bool Renewed { get; }

    /// <summary>Migawka konta Z WYNIKU wspolnego odczytu, nie pobrana ponownie po await.</summary>
    public SonosAccountSnapshot Snapshot { get; }

    /// <summary>Pusta lista ulubionych NADAL jest sukcesem.</summary>
    public bool Succeeded => Status == SonosDeviceReadStatus.Success && Favorites is not null;

    /// <summary>Wynik SPOZNIONY: nie podmienia tego, co widzi uzytkownik.</summary>
    public bool Discarded => Status == SonosDeviceReadStatus.Discarded;

    public string Message => SonosFavoritesReadMessages.Describe(Status);

    /// <summary>
    /// Mapowanie 1:1 wyniku wspolnego odczytu. Status zostaje nietkniety, w tym
    /// NoAccount i Discarded; migawka jest przeniesiona, nie odczytana na nowo.
    /// </summary>
    internal static SonosFavoritesReadResult From(SonosGroupReadResult<SonosFavoritesList> read) =>
        new(read.Status, read.Value, read.Renewed, read.Snapshot);

    /// <summary>
    /// WYLACZNIE DO POMIARU: pozwala atrapie zaplecza sesji oddac gotowy wynik
    /// bez konta, bez tokenu i bez transportu. Ta sama regula co w produkcji -
    /// dane wychodza TYLKO przy sukcesie, a migawka jest PUSTA, wiec nikt nie
    /// pomyli tego z prawdziwym kontem.
    /// </summary>
    public static SonosFavoritesReadResult CreateForMeasurement(
        SonosDeviceReadStatus status, SonosFavoritesList? favorites) =>
        new(status, favorites, renewed: false, SonosAccountSnapshots.Empty);

    /// <summary>
    /// Kontrolowane ToString: status, obecnosc danych i liczba pozycji. Bez
    /// tokenu, klucza, identyfikatora domu, nazw i identyfikatorow ulubionych.
    /// </summary>
    public override string ToString() =>
        "Odczyt ulubionych Sonos przez konto: " + Status
        + (Favorites is null ? ", brak danych" : ", " + Favorites.ToString())
        + (Renewed ? ", po odnowieniu dostępu" : string.Empty);
}
