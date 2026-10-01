using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using AccessibleMediaController.Core.Configuration;

namespace AccessibleMediaController.Core.Sonos;

/// <summary>
/// RODZAJE materialu Sonos, ktory wolno zapisac w presecie sesji.
///
/// Sa TRZY i tylko trzy - dokladnie te, ktore maja wlasna liste i wlasna droge
/// uruchomienia. GRUPA GLOSNIKOW nie jest tu wymieniona ROZMYSLNIE: grupa to
/// MIEJSCE, nie material, i zapisanie jej jako presetu zrobiloby z niej
/// fikcyjny "preset katalogu", ktorego nie da sie uruchomic.
/// </summary>
public static class SonosPresetKinds
{
    /// <summary>Ulubiony Sonos. Identyfikator OPAQUE, wazny w SWOIM domu.</summary>
    public const string Favorite = "sonos-favorite";

    /// <summary>Playlista Sonos. Identyfikator OPAQUE, wazny w SWOIM domu.</summary>
    public const string Playlist = "sonos-playlist";

    /// <summary>Wlasna stacja AMC. Identyfikator LOKALNY, adres z aktualnego wpisu.</summary>
    public const string OwnStream = "sonos-own-stream";

    public static bool IsSonosMaterial(string? kind) =>
        IsFavorite(kind) || IsPlaylist(kind) || IsOwnStream(kind);

    public static bool IsFavorite(string? kind) =>
        string.Equals(kind, Favorite, StringComparison.OrdinalIgnoreCase);

    public static bool IsPlaylist(string? kind) =>
        string.Equals(kind, Playlist, StringComparison.OrdinalIgnoreCase);

    public static bool IsOwnStream(string? kind) =>
        string.Equals(kind, OwnStream, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Czy identyfikator materialu jest wazny TYLKO w jednym domu. Ulubione i
    /// playlisty - TAK: ich identyfikatory przychodza z katalogu KONKRETNEGO
    /// domu, wiec puszczenie starego identyfikatora przez inny dom to strzal w
    /// cudzy katalog. Wlasna stacja - NIE: jej identyfikator jest nasz, lokalny.
    /// </summary>
    public static bool IsHouseholdBound(string? kind) =>
        IsFavorite(kind) || IsPlaylist(kind);

    /// <summary>
    /// Czy identyfikator jest OPAQUE, czyli NIE WOLNO go obcinac ani normalizowac.
    /// To WASKI wyjatek dla dwoch rodzajow Sonosa, a nie zmiana zasad dla calego
    /// magazynu presetow.
    /// </summary>
    public static bool HasOpaqueId(string? kind) => IsFavorite(kind) || IsPlaylist(kind);
}

/// <summary>Wynik szukania grupy o DOKLADNIE zapisanym skladzie.</summary>
public enum SonosFixedTargetResolution
{
    /// <summary>Jest dokladnie JEDNA taka grupa - mozna dzialac.</summary>
    Resolved,

    /// <summary>Preset nie ma stalego zestawu; cel bierze sie z biezacego wyboru.</summary>
    NotFixed,

    /// <summary>Topologii nie znamy TERAZ. To NIE znaczy, ze grupy nie ma.</summary>
    TopologyUnavailable,

    /// <summary>Jestesmy w INNYM domu niz ten, dla ktorego zapisano zestaw.</summary>
    DifferentHousehold,

    /// <summary>Zadna grupa nie ma dokladnie tego skladu.</summary>
    NoGroupWithExactSet,

    /// <summary>Kilka grup ma ten sklad - wybor nie jest jednoznaczny.</summary>
    Ambiguous
}

/// <summary>
/// STALY CEL presetu ("Zawsze w tym miejscu").
///
/// Wiazemy sie z LOGICZNYMI identyfikatorami odtwarzaczy i domem, bo to one
/// przezywaja przegrupowanie. <c>groupId</c> i nazwa grupy sa KRUCHE: pierwszy
/// zmienia sie przy kazdej zmianie skladu, druga przy zmianie nazwy pokoju -
/// zaden z nich nie jest tozsamoscia miejsca.
///
/// Dopasowanie jest DOKLADNE (zbior rowny, nie podzbior). Grupa szersza ma
/// INNYCH sluchaczy, grupa wezsza nie obejmuje tych, ktorych uzytkownik wskazal
/// - w obu wypadkach "prawie to samo miejsce" to po prostu INNE miejsce.
/// </summary>
public static class SonosPresetFixedTarget
{
    /// <summary>
    /// Porzadkujemy sklad do postaci KANONICZNEJ: bez pustych, bez duplikatow,
    /// posortowany. Dzieki temu kolejnosc z API nie decyduje o dopasowaniu.
    /// </summary>
    public static IReadOnlyList<string> NormalizePlayerIds(IEnumerable<string?>? playerIds)
    {
        if (playerIds is null) return Array.Empty<string>();
        var unique = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var playerId in playerIds)
        {
            if (string.IsNullOrWhiteSpace(playerId)) continue;
            unique.Add(playerId.Trim());
        }
        return unique.Count == 0 ? Array.Empty<string>() : unique.ToArray();
    }

    public static bool IsExactSameSet(IEnumerable<string?>? left, IEnumerable<string?>? right)
    {
        var a = NormalizePlayerIds(left);
        var b = NormalizePlayerIds(right);
        if (a.Count == 0 || a.Count != b.Count) return false;
        for (var index = 0; index < a.Count; index++)
        {
            if (!string.Equals(a[index], b[index], StringComparison.Ordinal)) return false;
        }
        return true;
    }

    /// <summary>
    /// Szukamy grupy o DOKLADNIE zapisanym skladzie w SWIEZEJ topologii.
    /// Wynik niejednoznaczny albo nieznany NIE jest zgoda na granie gdziekolwiek.
    /// </summary>
    public static SonosFixedTargetResolution Resolve(
        string? presetHouseholdId,
        IEnumerable<string?>? presetPlayerIds,
        string? currentHouseholdId,
        SonosHouseholdTopology? topology,
        out SonosGroup? group)
    {
        group = null;
        var wanted = NormalizePlayerIds(presetPlayerIds);
        if (wanted.Count == 0) return SonosFixedTargetResolution.NotFixed;

        if (!string.IsNullOrWhiteSpace(presetHouseholdId)
            && !string.Equals(presetHouseholdId, currentHouseholdId, StringComparison.Ordinal))
        {
            return SonosFixedTargetResolution.DifferentHousehold;
        }

        if (topology is null || topology.Partial) return SonosFixedTargetResolution.TopologyUnavailable;

        SonosGroup? found = null;
        var matches = 0;
        foreach (var candidate in topology.Groups)
        {
            if (!IsExactSameSet(wanted, candidate.PlayerIds)) continue;
            matches++;
            found ??= candidate;
        }

        if (matches == 0) return SonosFixedTargetResolution.NoGroupWithExactSet;
        if (matches > 1) return SonosFixedTargetResolution.Ambiguous;
        group = found;
        return SonosFixedTargetResolution.Resolved;
    }
}

/// <summary>Co zrobic z presetem wobec tego, co grupa gra TERAZ.</summary>
public enum SonosPresetRepeatDecision
{
    /// <summary>Nie udało się ustalić stanu; nie wolno przejmować grupy.</summary>
    Unavailable,
    /// <summary>Zwykle uruchomienie: inny material albo nieznana tozsamosc.</summary>
    Load,

    /// <summary>TEN material GRA: tylko nazwa. Zero load, zero restartu.</summary>
    AnnounceOnly,

    /// <summary>TEN material jest SPAUZOWANY: zwykle wznowienie, bez przeladowania.</summary>
    Resume
}

/// <summary>
/// POWTORZENIE: drugie nacisniecie tego samego presetu.
///
/// Warunek jest JEDEN i twardy: musimy miec STABILNY klucz, ktory NAPRAWDE
/// pochodzi od materialu. Tytul odpada (dwie stacje moga nazywac sie tak samo, a
/// tytul utworu zmienia sie w trakcie), "ostatni uzyty slot" odpada (nie mowi
/// nic o tym, co grupa gra teraz, zwlaszcza po cudzym sterowaniu z appki Sonos),
/// a HTTP 200 odpada, bo przyjecie polecenia to nie dowod odtwarzania.
///
/// BRAK lokalnego cache sam z siebie NIE wymusza przeladowania: jesli SWIEZY
/// odczyt da nasz zgodny klucz i stan PLAYING, to powtorzenie jest uzasadnione
/// mimo pustej pamieci po restarcie programu.
/// </summary>
public static class SonosPresetRepeat
{
    public static SonosPresetRepeatDecision Decide(
        string? expectedItemId,
        string? currentItemId,
        SonosPlaybackState playbackState,
        bool hasContainer)
    {
        // Bez klucza po ktorejkolwiek stronie nie ma tozsamosci. Nie maskujemy
        // tego ryzyka cichym "pewnie to to samo" - idzie zwykly load.
        if (string.IsNullOrEmpty(expectedItemId) || string.IsNullOrEmpty(currentItemId))
            return SonosPresetRepeatDecision.Load;
        if (!string.Equals(expectedItemId, currentItemId, StringComparison.Ordinal))
            return SonosPresetRepeatDecision.Load;

        // IDLE może oznaczać spauzowane radio. Wznawiamy tylko zgodny klucz
        // z nadal obecnym kontenerem, po świeżym odczycie wykonanym przez callera.
        return playbackState switch
        {
            // BUFFERING to material juz w drodze: przeladowanie przerwaloby je bez powodu.
            SonosPlaybackState.Playing or SonosPlaybackState.Buffering
                => SonosPresetRepeatDecision.AnnounceOnly,
            SonosPlaybackState.Paused or SonosPlaybackState.Idle when hasContainer => SonosPresetRepeatDecision.Resume,
            _ => SonosPresetRepeatDecision.Load
        };
    }
}

/// <summary>
/// STABILNY klucz WLASNEJ stacji, podawany jako <c>itemId</c> przy
/// loadStreamUrl i porownywany przy nastepnym nacisnieciu.
///
/// Liczymy go z LOKALNEGO identyfikatora stacji ORAZ z adresu: ten sam wpis po
/// zmianie adresu to INNY material, wiec klucz ma sie zmienic. API ogranicza
/// <c>itemId</c> do 128 znakow, dlatego bierzemy skrot o STALEJ dlugosci -
/// dlugi adres nie moze wypchnac nas za limit.
///
/// Ten klucz NIE ISTNIEJE dla ulubionych i playlist: <c>loadFavorite</c> i
/// <c>loadPlaylist</c> nie przyjmuja naszego <c>itemId</c>, a <c>container.id</c>
/// (optional universalMusicObjectId) nie jest w obecnym modelu/parserze AMC.
/// </summary>
public static class SonosOwnStreamIdentity
{
    private const string Prefix = "amc-own-";

    public static string? TryComputeItemId(string? stationId, string? streamUrl)
    {
        if (string.IsNullOrWhiteSpace(stationId) || string.IsNullOrWhiteSpace(streamUrl))
            return null;

        var material = stationId.Trim() + "\n" + streamUrl.Trim();
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(material));
        var builder = new StringBuilder(Prefix, Prefix.Length + 32);
        // 16 bajtow = 32 znaki hex; z prefiksem 40 znakow, grubo pod limitem 128.
        for (var index = 0; index < 16; index++)
        {
            builder.Append(digest[index].ToString("x2", CultureInfo.InvariantCulture));
        }
        return builder.ToString();
    }
}

/// <summary>Napisy drogi presetow Sonosa. Nazwa materialu, nie numer slotu.</summary>
public static class SonosPresetLabels
{
    public const string AssignNeedsMaterial =
        "Presety Sonos zapisują materiał: ulubiony, playlistę albo własną stację. "
        + "Otwórz jedną z tych list, wybierz pozycję i naciśnij Ctrl+Alt+Shift+P";

    public const string NeedsGroup =
        "Nie wybrano głośników Sonos. Wybierz grupę i spróbuj ponownie. "
        + "Nic nie uruchomiłem";

    public const string AlreadyInFlight =
        "Trwa inne polecenie Sonos. Poczekaj i spróbuj ponownie";

    public const string AccountChanged =
        "Konto Sonos zostało zmienione. Nic nie uruchomiłem";

    public static string DescribeAssigned(string slotLabel, string title) =>
        $"Preset {slotLabel}: {title}. Nic nie uruchomiłem";

    public static string DescribeAssignedFixed(string slotLabel, string title, int playerCount) =>
        $"Preset {slotLabel}: {title}, zawsze na tym zestawie "
        + $"{DescribePlayerCount(playerCount)}. Nic nie uruchomiłem";

    /// <summary>
    /// Material, ktorego TERAZ nie ma: usunieta stacja albo zapis z innego domu.
    /// Tu numer slotu i instrukcja sa na miejscu - nie ma nazwy, ktora by grala.
    /// </summary>
    public static string DescribeMaterialGone(string slotLabel, string? kind) => kind switch
    {
        _ when SonosPresetKinds.IsOwnStream(kind) =>
            $"Preset {slotLabel}: tej stacji już nie ma. Przypisz preset ponownie "
            + "w Moje stacje. Nic nie uruchomiłem",
        _ when SonosPresetKinds.IsHouseholdBound(kind) =>
            $"Preset {slotLabel} zapisano dla innego domu Sonos. Przypisz go ponownie "
            + "w tym domu. Nic nie uruchomiłem",
        _ => $"Preset {slotLabel} jest nieaktualny. Przypisz go ponownie. Nic nie uruchomiłem"
    };

    /// <summary>UCZCIWA ODMOWA stalego celu: bez przegrupowania i bez grania obok.</summary>
    public static string DescribeFixedRefusal(SonosFixedTargetResolution resolution) => resolution switch
    {
        SonosFixedTargetResolution.NoGroupWithExactSet =>
            "Nie ma teraz grupy Sonos o dokładnie tym zestawie głośników. "
            + "Zgrupuj je tak jak przy zapisie presetu. Nic nie uruchomiłem i nic nie przegrupowałem",
        SonosFixedTargetResolution.Ambiguous =>
            "Kilka grup Sonos ma ten sam zestaw głośników, więc wybór nie jest jednoznaczny. "
            + "Nic nie uruchomiłem",
        SonosFixedTargetResolution.DifferentHousehold =>
            "Ten preset zapisano dla innego domu Sonos. Nic nie uruchomiłem",
        SonosFixedTargetResolution.TopologyUnavailable =>
            "Nie udało się odczytać grup Sonos, więc nie wiem, czy zapisany zestaw istnieje. "
            + "Nic nie uruchomiłem",
        _ => "Nie znalazłem zapisanego zestawu głośników. Nic nie uruchomiłem"
    };

    private static string DescribePlayerCount(int count) => count switch
    {
        1 => "1 głośnik",
        >= 2 and <= 4 => $"{count} głośniki",
        _ => $"{count} głośników"
    };
}
