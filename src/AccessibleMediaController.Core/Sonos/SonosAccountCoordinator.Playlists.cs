using System;
using System.Threading;
using System.Threading.Tasks;

namespace AccessibleMediaController.Core.Sonos;

/// <summary>
/// ODCZYT PLAYLIST SONOSA domu oraz ich URUCHOMIENIE przez ISTNIEJACEGO
/// wlasciciela konta.
///
/// Ta czesc koordynatora jest CIENKA z zamyslu. Cala droga poswiadczen - bilet
/// pod blokada, HTTP poza blokada, jedno odnowienie przy znanej minionej
/// waznosci, po 401 najwyzej jedno odnowienie i jedno powtorzenie przy ODCZYCIE,
/// kontrola ORYGINALNEJ generacji po kazdym await - jest JUZ rozstrzygnieta we
/// wspolnych <see cref="RunGroupReadAsync{TValue}"/> i
/// <see cref="RunGroupCommandAsync"/>. Nie ma tu ani kopii biletu, ani kopii
/// odnawiania, ani wlasnej blokady, ani wlasnej polityki identyfikatorow.
///
/// Stad bierze sie zachowanie obowiazujace bez wyjatku:
///   * brak konta: ZERO zapytan i ZERO odnowien,
///   * znana MINIONA waznosc: DOKLADNIE JEDNO odnowienie przed zapytaniem,
///   * uruchomienie playlisty to DOKLADNIE JEDEN POST - takze po 401, 403, 404,
///     429, 5xx i po utraconej odpowiedzi nie ma ani powtorzenia, ani kasowania
///     konta, bo polecenie zmienia wspolna kolejke uzytkownika,
///   * HTTP 200 to PRZYJECIE zlecenia, nie dowod, ze muzyka zagrala,
///   * wynik SPOZNIONY (zmiana konta w trakcie) jest PORZUCANY i nie przenosi
///     starej listy pod nowa tozsamosc.
///
/// Czego tu celowo NIE MA: walidacji domu, grupy, identyfikatora playlisty i
/// akcji - ma ja ODEBRANY klient i to on odrzuca zle wejscie PRZED HTTP. Nie ma
/// tez cache, pollingu, subskrypcji playlistsVersionChange, presetow ani UI.
/// </summary>
public sealed partial class SonosAccountCoordinator
{
    /// <summary>
    /// GET /households/{householdId}/playlists przez bilet biezacego konta.
    ///
    /// <paramref name="householdId"/> idzie do klienta LITERALNIE - koordynator
    /// nie trimuje, nie podmienia i nie zgaduje domu.
    /// </summary>
    public async Task<SonosPlaylistsReadResult> ReadPlaylistsAsync(
        ISonosPlaylistsApi api,
        string? householdId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(api);
        var read = await RunGroupReadAsync<SonosPlaylistsList>(
            async (token, ct) =>
            {
                var outcome = await api.GetPlaylistsAsync(token, householdId, ct).ConfigureAwait(false);
                return (outcome.Status, outcome.Playlists);
            },
            cancellationToken).ConfigureAwait(false);

        return SonosPlaylistsReadResult.From(read);
    }

    /// <summary>
    /// POST /groups/{groupId}/playlists przez bilet biezacego konta.
    ///
    /// <paramref name="action"/> i <paramref name="playOnCompletion"/> sa
    /// OBOWIAZKOWE i BEZ wartosci domyslnych - dokladnie jak przy ulubionych.
    /// Roznica miedzy dopisaniem i zastapieniem kolejki uzytkownika jest
    /// nieodwracalna i nie moze zalezec od milczenia wolajacego.
    /// </summary>
    public async Task<SonosGroupCommandResult> LoadPlaylistAsync(
        ISonosPlaylistLoadApi api,
        string? groupId,
        string? playlistId,
        SonosFavoriteQueueAction action,
        bool playOnCompletion,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(api);
        return await RunGroupCommandAsync(
            SonosGroupCommand.LoadPlaylist,
            (token, ct) => api.LoadPlaylistAsync(token, groupId, playlistId, action, playOnCompletion, ct),
            cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// Stale komunikaty PL odczytu PLAYLIST. Osobne od komunikatow ulubionych i
/// urzadzen, bo uzytkownik ma uslyszec, czego naprawde dotyczyl odczyt.
///
/// Zaden tekst nie zawiera tokenu, klucza integracji, identyfikatora domu, nazwy
/// ani identyfikatora playlisty, ani surowego globalError.reason.
/// </summary>
public static class SonosPlaylistsReadMessages
{
    public static string Describe(SonosDeviceReadStatus status) => status switch
    {
        SonosDeviceReadStatus.Success => "Lista playlist Sonos została odczytana.",
        SonosDeviceReadStatus.NoAccount =>
            "Nie ma połączonego konta Sonos, więc nie ma skąd odczytać playlist.",
        // PORZUCENIE nie orzeka JEDNEJ przyczyny: mogla to byc zmiana konta,
        // wylogowanie albo zakonczenie pracy wlasciciela.
        SonosDeviceReadStatus.Discarded =>
            "Odczyt playlist Sonos został pominięty: kontekst konta zmienił się w trakcie. Spróbuj odświeżyć.",
        SonosDeviceReadStatus.InvalidConfiguration =>
            "Odczyt playlist nie został wysłany: brak klucza integracji albo niepoprawny dom.",
        SonosDeviceReadStatus.Unauthorized =>
            "Sonos nie przyjął dostępu do konta. Odnów dostęp albo zaloguj się ponownie.",
        SonosDeviceReadStatus.Forbidden => "Konto Sonos nie ma uprawnień do odczytu playlist.",
        SonosDeviceReadStatus.NotFound => "Sonos nie znalazł wskazanego domu.",
        SonosDeviceReadStatus.RequestRejected => "Sonos odrzucił zapytanie o playlisty.",
        SonosDeviceReadStatus.RateLimited => "Sonos chwilowo ogranicza liczbę zapytań. Spróbuj później.",
        SonosDeviceReadStatus.CommandFailed => "Sonos nie wykonał zapytania o playlisty.",
        SonosDeviceReadStatus.ServiceError => "Usługa Sonos zgłosiła błąd.",
        SonosDeviceReadStatus.Unreachable => "Nie udało się połączyć z usługą Sonos.",
        SonosDeviceReadStatus.Canceled => "Odczyt playlist Sonos został anulowany.",
        SonosDeviceReadStatus.RedirectRefused =>
            "Sonos próbował przekierować zapytanie; zostało zatrzymane.",
        _ => "Odpowiedź Sonos o playlistach była niezgodna z oczekiwaną."
    };
}

/// <summary>
/// Wynik ODCZYTU PLAYLIST widziany przez konto. Jest to WYLACZNIE mapowanie juz
/// rozstrzygnietego <see cref="SonosGroupReadResult{TValue}"/>: zadnego nowego
/// algorytmu konta, zadnej nowej migawki, zadnej zmiany statusu.
///
/// Dane wychodza TYLKO przy sukcesie. Wynik PORZUCONY albo bledny nie przenosi
/// starej listy - stare playlisty nie moga pojawic sie pod nowa tozsamoscia.
/// </summary>
public sealed class SonosPlaylistsReadResult
{
    internal SonosPlaylistsReadResult(
        SonosDeviceReadStatus status,
        SonosPlaylistsList? playlists,
        bool renewed,
        SonosAccountSnapshot snapshot)
    {
        Status = status;
        // Brak danych przy KAZDYM niepowodzeniu i przy porzuceniu.
        Playlists = status == SonosDeviceReadStatus.Success ? playlists : null;
        Renewed = renewed;
        Snapshot = snapshot;
    }

    public SonosDeviceReadStatus Status { get; }

    /// <summary>
    /// Lista playlist TYLKO przy sukcesie. Niesie LITERALNY identyfikator domu z
    /// zapytania - wstawia go klient, nie koordynator.
    /// </summary>
    public SonosPlaylistsList? Playlists { get; }

    /// <summary>Czy po drodze uzyto ISTNIEJACEGO odnowienia dostepu (najwyzej raz).</summary>
    public bool Renewed { get; }

    /// <summary>Migawka konta Z WYNIKU wspolnego odczytu, nie pobrana ponownie po await.</summary>
    public SonosAccountSnapshot Snapshot { get; }

    /// <summary>Pusta lista playlist NADAL jest sukcesem.</summary>
    public bool Succeeded => Status == SonosDeviceReadStatus.Success && Playlists is not null;

    /// <summary>Wynik SPOZNIONY: nie podmienia tego, co widzi uzytkownik.</summary>
    public bool Discarded => Status == SonosDeviceReadStatus.Discarded;

    public string Message => SonosPlaylistsReadMessages.Describe(Status);

    /// <summary>
    /// Mapowanie 1:1 wyniku wspolnego odczytu. Status zostaje nietkniety, w tym
    /// NoAccount i Discarded; migawka jest przeniesiona, nie odczytana na nowo.
    /// </summary>
    internal static SonosPlaylistsReadResult From(SonosGroupReadResult<SonosPlaylistsList> read) =>
        new(read.Status, read.Value, read.Renewed, read.Snapshot);

    /// <summary>
    /// WYLACZNIE DO POMIARU: pozwala atrapie zaplecza sesji oddac gotowy wynik
    /// bez konta, bez tokenu i bez transportu. Ta sama regula co w produkcji -
    /// dane wychodza TYLKO przy sukcesie, a migawka jest PUSTA, wiec nikt nie
    /// pomyli tego z prawdziwym kontem.
    /// </summary>
    public static SonosPlaylistsReadResult CreateForMeasurement(
        SonosDeviceReadStatus status, SonosPlaylistsList? playlists) =>
        new(status, playlists, renewed: false, SonosAccountSnapshots.Empty);

    /// <summary>
    /// Kontrolowane ToString: status, obecnosc danych i liczba pozycji. Bez
    /// tokenu, klucza, identyfikatora domu, nazw i identyfikatorow playlist.
    /// </summary>
    public override string ToString() =>
        "Odczyt playlist Sonos przez konto: " + Status
        + (Playlists is null ? ", brak danych" : ", " + Playlists.ToString())
        + (Renewed ? ", po odnowieniu dostępu" : string.Empty);
}
