using AccessibleMediaController.Core.Configuration;

namespace AccessibleMediaController.Core.Sonos;

/// <summary>
/// JEDEN wpis odczytany przez ISTNIEJACY importer playlist radia/WiiM
/// (<c>RadioPlaylistImporter</c> w warstwie Windows): nazwa i adres, tak jak je
/// oddal parser. Celowo NIE powtarzamy tu parsowania M3U/PLS/XSPF/VRadio JSON -
/// drugi parser tych samych formatow rozjechalby sie z pierwszym.
///
/// <paramref name="StreamUrl"/> jest nullable, bo wolajacy moze przekazac wpis
/// bez adresu; bramka niżej odrzuci go jako pominiety, zamiast wywalic import.
/// </summary>
public sealed record SonosOwnStreamsImportEntry(string? Name, string? StreamUrl);

/// <summary>
/// Wynik scalenia. <see cref="Stations"/> to PELNA nowa lista Moich stacji
/// (stare + dodane), gotowa do zapisania przez warstwe Windows. Liczniki sa
/// rozdzielone, bo uzytkownik slyszy inny komunikat dla "adres nie nadaje sie
/// do Sonosa" niz dla "ta stacja juz jest zapisana".
/// </summary>
public sealed record SonosOwnStreamsImportResult(
    IReadOnlyList<SonosOwnStreamSettings> Stations,
    int Added,
    int SkippedDuplicates,
    int SkippedInvalidAddresses);

/// <summary>
/// IMPORT PLAYLISTY DO LOKALNYCH "Moich stacji Sonosa" - czysta logika scalania.
///
/// Granice, swiadomie wezsze niz moze sie wydawac z nazwy:
/// - ZERO wejscia/wyjscia: nie czytamy pliku, nie pobieramy adresu, nie
///   sprawdzamy, czy strumien gra. Zadnego GET ani POST, zadnego createSession,
///   zadnego autoodtwarzania.
/// - ZERO zmian w Ulubionych Sonosa i w playlistach domu: te stacje zyja TYLKO
///   w AppSettings AMC.
/// - Import DOPISUJE. Nigdy nie czysci wczesniej zapisanych stacji, takze gdy
///   cala playlista okaze sie nieprzydatna.
/// - Zapisanej stacji NIE NADPISUJEMY: zduplikowany adres zostawia jej ID,
///   nazwe i doslowny adres. Nazwa z pliku nie wygrywa z nazwa nadana rucznie.
/// - Adres idzie DOSLOWNIE do <see cref="SonosOwnStreamSettings.StreamUrl"/>:
///   bez trimowania, dopisywania ukosnika, gubienia zapytania i fragmentu.
///   Decyduje ta sama bramka, co przy odtwarzaniu - <see cref="SonosStreamUrlPolicy"/>,
///   wiec lista nie przyjmie adresu, ktorego loadStreamUrl potem odrzuci.
/// - Wejscie zostaje NIETKNIETE, a wynik jest ODLACZONY: obie listy sa
///   mutowalne, wiec aliasowanie obiektow po cichu zmienialoby stan drugiej
///   strony.
///
/// Zapis na dysk, okno i anulowanie okna wyboru pliku naleza do warstwy
/// Windows i NIE sa tu zmierzone.
/// </summary>
public static class SonosOwnStreamsImport
{
    public static SonosOwnStreamsImportResult Merge(
        IEnumerable<SonosOwnStreamSettings> existingStations,
        IEnumerable<SonosOwnStreamsImportEntry> importedEntries,
        Func<string>? newIdFactory = null)
    {
        ArgumentNullException.ThrowIfNull(existingStations);
        ArgumentNullException.ThrowIfNull(importedEntries);

        // Kopia od razu: dalej pracujemy wylacznie na wlasnych obiektach, wiec
        // ani my nie dotkniemy wejscia, ani wolajacy nie zmieni nam wyniku.
        var stations = existingStations.Select(Copy).ToList();
        var takenAddresses = stations
            .Select(station => station.StreamUrl)
            .Where(address => !string.IsNullOrEmpty(address))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var takenIds = stations
            .Select(station => station.Id)
            .Where(id => !string.IsNullOrEmpty(id))
            .ToHashSet(StringComparer.Ordinal);

        var added = 0;
        var duplicates = 0;
        var invalid = 0;
        foreach (var entry in importedEntries)
        {
            var address = entry?.StreamUrl;

            // Ta sama bramka, co przy odtwarzaniu: tylko poprawne, bezpieczne
            // http/https w limicie Sonosa. Lokalne sciezki i obce schematy
            // (file:, spotify:, tidal:) nie maja tu czego robic.
            if (!SonosStreamUrlPolicy.IsAcceptable(address)) { invalid++; continue; }
            if (!takenAddresses.Add(address!)) { duplicates++; continue; }

            stations.Add(new SonosOwnStreamSettings
            {
                Id = NextId(takenIds, newIdFactory),
                Name = DisplayName(entry!.Name, address!),
                StreamUrl = address!
            });
            added++;
        }

        return new SonosOwnStreamsImportResult(stations, added, duplicates, invalid);
    }

    private static SonosOwnStreamSettings Copy(SonosOwnStreamSettings station) =>
        new() { Id = station.Id, Name = station.Name, StreamUrl = station.StreamUrl };

    /// <summary>
    /// ID musi byc UNIKALNE takze wtedy, gdy wolajacy poda zly generator:
    /// powtorzone ID zlalyby dwie stacje w jedna przy zapisie i w presetach.
    /// </summary>
    private static string NextId(HashSet<string> takenIds, Func<string>? newIdFactory)
    {
        for (var attempt = 0; attempt < 8; attempt++)
        {
            var candidate = newIdFactory?.Invoke();
            if (!string.IsNullOrWhiteSpace(candidate) && takenIds.Add(candidate)) return candidate;
            if (newIdFactory is null) break;
        }

        while (true)
        {
            var candidate = Guid.NewGuid().ToString("N");
            if (takenIds.Add(candidate)) return candidate;
        }
    }

    /// <summary>
    /// Nazwa to etykieta dla CZYTNIKA EKRANU, wiec pusty wiersz jest bledem.
    /// Parser radia zwykle podstawia juz host; gdy jednak nazwa przyjdzie pusta,
    /// robimy to samo tutaj. Adresu to nie dotyczy - on zostaje doslowny.
    /// </summary>
    private static string DisplayName(string? name, string address)
    {
        var trimmed = name?.Trim();
        if (!string.IsNullOrEmpty(trimmed)) return trimmed;
        return Uri.TryCreate(address, UriKind.Absolute, out var uri) && uri.Host.Length > 0
            ? uri.Host
            : "Stacja radiowa";
    }
}
