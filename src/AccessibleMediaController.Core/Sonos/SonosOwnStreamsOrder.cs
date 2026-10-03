using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.LocalMedia;

namespace AccessibleMediaController.Core.Sonos;

/// <summary>Co stalo sie z kolejnoscia - werdykt dla KROTKIEGO komunikatu.</summary>
public enum SonosOwnStreamsOrderResult
{
    Moved,
    Boundary,
    NonContiguousSelection,
    InvalidSelection,
    TargetInSelection,
    TargetMissing,
    Unchanged
}

/// <summary>
/// SORTOWANIE I KOLEJNOSC WLASNA "Moich stacji Sonosa" - czysta logika, zero UI
/// i zero sieci.
///
/// WZOR: dokladnie to, co robi radio/Biblioteka w oknie glownym
/// (<c>CollectionSortMode</c>, <c>LocalLibraryManualOrder</c>). Algorytmu
/// przenoszenia NIE przepisujemy: Alt+strzalki i Ctrl+X/Ctrl+V wolaja TE SAME
/// <see cref="LocalLibraryManualOrder.MoveVisibleBlock"/> i
/// <see cref="LocalLibraryManualOrder.PlaceItemsBefore"/>, wiec dwa systemy nie
/// rozjada sie na brzegach ani na wielozaznaczeniu.
///
/// JEDNO ZRODLO PRAWDY FORMATU: kolejnosc to lista IDENTYFIKATOROW stacji w
/// ISTNIEJACYM <see cref="CollectionOrderSettings"/>, pod kluczem
/// <see cref="StorageKey"/> - dokladnie tak, jak Biblioteka i Ulubione
/// (<c>LibraryItemIdsBySession</c> dla kolejnosci wlasnej,
/// <c>LibraryAddedItemIdsBySession</c> dla chronologii dodania). ZERO nowych pol
/// konfiguracji: nie powstaje drugi system zapisu kolejnosci, ktory moglby sie
/// rozjechac z pierwszym.
///
/// MIGRACJA STARYCH USTAWIEN: brak zapisanej kolejnosci znaczy "zostaw tak, jak
/// bylo". Chronologia dodania startuje jako DOTYCHCZASOWA kolejnosc listy, wiec
/// pierwsze uruchomienie po aktualizacji NIE odwraca wszystkich stacji - dopiero
/// jawny Alt+1 pokazuje najnowsze na poczatku.
/// </summary>
public static class SonosOwnStreamsOrder
{
    /// <summary>Klucz kolejnosci w <see cref="CollectionOrderSettings"/>.</summary>
    public const string StorageKey = "sonos|Moje stacje";

    /// <summary>Nazwa widoku dla <c>CollectionSortModes</c> - jak w oknie glownym.</summary>
    public const string ViewName = "Moje stacje";

    /// <summary>Etykiety trybu - KROTKIE, tak jak w oknie glownym.</summary>
    public static string DescribeMode(CollectionSortMode mode) => mode switch
    {
        CollectionSortMode.Alphabetical => "Alfabetycznie",
        CollectionSortMode.Custom => "Kolejność własna",
        _ => "Według dodania, najnowsze na początku"
    };

    /// <summary>
    /// CHRONOLOGIA DODANIA. Pierwsze wywolanie przyjmuje biezaca kolejnosc listy
    /// jako "tak byly dodane", a kazde nastepne tylko DOPISUJE nowe stacje na
    /// koniec i usuwa te, ktorych juz nie ma. Dzieki temu Add/Edit/Import/Remove
    /// nie gubia chronologii i nie przestawiaja istniejacych wpisow.
    /// </summary>
    public static List<string> EnsureAddedOrder(
        CollectionOrderSettings orders,
        IReadOnlyList<SonosOwnStreamSettings> stations)
    {
        ArgumentNullException.ThrowIfNull(orders);
        ArgumentNullException.ThrowIfNull(stations);
        var normalized = NormalizeIds(
            orders.LibraryAddedItemIdsBySession.GetValueOrDefault(StorageKey), stations);
        orders.LibraryAddedItemIdsBySession[StorageKey] = normalized;
        return normalized;
    }

    /// <summary>
    /// KOLEJNOSC WLASNA. Startuje od DOTYCHCZASOWEJ kolejnosci listy - inaczej
    /// pierwsze Alt+3 po aktualizacji przetasowaloby stacje bez zadnego gestu
    /// uzytkownika.
    /// </summary>
    public static List<string> EnsureCustomOrder(
        CollectionOrderSettings orders,
        IReadOnlyList<SonosOwnStreamSettings> stations)
    {
        ArgumentNullException.ThrowIfNull(orders);
        ArgumentNullException.ThrowIfNull(stations);
        var normalized = NormalizeIds(
            orders.LibraryItemIdsBySession.GetValueOrDefault(StorageKey), stations);
        orders.LibraryItemIdsBySession[StorageKey] = normalized;
        return normalized;
    }

    /// <summary>
    /// WIDOCZNA kolejnosc wierszy dla danego trybu. Alt+1 to chronologia odwrotnie
    /// (najnowsze na poczatku), Alt+2 alfabetycznie po nazwie, Alt+3 kolejnosc
    /// wlasna. ID, nazwy i DOSLOWNE adresy zostaja nietkniete - zmienia sie
    /// WYLACZNIE porzadek.
    /// </summary>
    public static IReadOnlyList<SonosOwnStreamSettings> Arrange(
        CollectionOrderSettings orders,
        IReadOnlyList<SonosOwnStreamSettings> stations,
        CollectionSortMode mode)
    {
        ArgumentNullException.ThrowIfNull(orders);
        ArgumentNullException.ThrowIfNull(stations);
        return mode switch
        {
            CollectionSortMode.Alphabetical => stations
                .OrderBy(station => Label(station), StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(station => station.Id, StringComparer.Ordinal)
                .ToArray(),
            CollectionSortMode.Custom => ByIds(stations, EnsureCustomOrder(orders, stations)),
            _ => ByIds(stations, EnsureAddedOrder(orders, stations)).Reverse().ToArray()
        };
    }

    /// <summary>
    /// ALT+STRZALKI: przesuniecie pojedynczej stacji albo CIAGLEGO bloku w
    /// kolejnosci wlasnej. Pusta lista, brzeg i dziurawe zaznaczenie maja WLASNE
    /// werdykty, zeby komunikat nie klamal o skutku.
    /// </summary>
    public static SonosOwnStreamsOrderResult Move(
        CollectionOrderSettings orders,
        IReadOnlyList<SonosOwnStreamSettings> stations,
        IReadOnlyCollection<string> selectedIds,
        int direction)
    {
        ArgumentNullException.ThrowIfNull(orders);
        ArgumentNullException.ThrowIfNull(stations);
        ArgumentNullException.ThrowIfNull(selectedIds);
        if (stations.Count == 0 || selectedIds.Count == 0 || direction == 0)
            return SonosOwnStreamsOrderResult.InvalidSelection;
        var order = EnsureCustomOrder(orders, stations);
        var visible = ByIds(stations, order).Select(station => station.Id).ToArray();
        return LocalLibraryManualOrder.MoveVisibleBlock(order, visible, selectedIds, direction) switch
        {
            ManualOrderMoveResult.Moved => SonosOwnStreamsOrderResult.Moved,
            ManualOrderMoveResult.Boundary => SonosOwnStreamsOrderResult.Boundary,
            ManualOrderMoveResult.NonContiguousSelection => SonosOwnStreamsOrderResult.NonContiguousSelection,
            _ => SonosOwnStreamsOrderResult.InvalidSelection
        };
    }

    /// <summary>
    /// CTRL+V: wstawienie WYCIETYCH stacji PRZED wierszem docelowym. Ctrl+X sam
    /// niczego nie rusza i niczego nie usuwa - zapamietuje tylko identyfikatory,
    /// wiec anulowanie i zamkniecie okna nie przenosza nic.
    /// </summary>
    public static SonosOwnStreamsOrderResult PlaceBefore(
        CollectionOrderSettings orders,
        IReadOnlyList<SonosOwnStreamSettings> stations,
        IReadOnlyCollection<string> selectedIds,
        string? targetId)
    {
        ArgumentNullException.ThrowIfNull(orders);
        ArgumentNullException.ThrowIfNull(stations);
        ArgumentNullException.ThrowIfNull(selectedIds);
        if (stations.Count == 0 || selectedIds.Count == 0)
            return SonosOwnStreamsOrderResult.InvalidSelection;
        // Stacja WYCIETA, a potem USUNIETA albo zmieniona na inne ID nie ma czego
        // przenosic: brakujace zrodlo to nieprawidlowe zaznaczenie, nie "przeniesiono".
        var known = stations.Select(station => station.Id).ToHashSet(StringComparer.Ordinal);
        if (selectedIds.Any(id => !known.Contains(id)))
            return SonosOwnStreamsOrderResult.InvalidSelection;
        var order = EnsureCustomOrder(orders, stations);
        return LocalLibraryManualOrder.PlaceItemsBefore(order, selectedIds, targetId ?? string.Empty) switch
        {
            ManualOrderPlacementResult.Moved => SonosOwnStreamsOrderResult.Moved,
            ManualOrderPlacementResult.TargetInSelection => SonosOwnStreamsOrderResult.TargetInSelection,
            ManualOrderPlacementResult.TargetMissing => SonosOwnStreamsOrderResult.TargetMissing,
            ManualOrderPlacementResult.Unchanged => SonosOwnStreamsOrderResult.Unchanged,
            _ => SonosOwnStreamsOrderResult.InvalidSelection
        };
    }

    /// <summary>Etykieta wiersza dla CZYTNIKA: nazwa, a gdy puste - adres.</summary>
    public static string Label(SonosOwnStreamSettings station) =>
        string.IsNullOrWhiteSpace(station?.Name) ? station?.StreamUrl ?? string.Empty : station.Name;

    private static List<string> NormalizeIds(
        IEnumerable<string>? stored,
        IReadOnlyList<SonosOwnStreamSettings> stations)
    {
        var known = stations
            .Select(station => station.Id)
            .Where(id => !string.IsNullOrEmpty(id))
            .ToList();
        var knownSet = known.ToHashSet(StringComparer.Ordinal);
        var result = (stored ?? [])
            .Where(id => knownSet.Contains(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var present = result.ToHashSet(StringComparer.Ordinal);
        result.AddRange(known.Where(id => present.Add(id)));
        return result;
    }

    private static IReadOnlyList<SonosOwnStreamSettings> ByIds(
        IReadOnlyList<SonosOwnStreamSettings> stations,
        IReadOnlyList<string> order)
    {
        var indices = order
            .Select((id, index) => (id, index))
            .GroupBy(pair => pair.id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().index, StringComparer.Ordinal);
        return stations
            .Select((station, original) => (station, original))
            .OrderBy(pair => indices.GetValueOrDefault(pair.station.Id, int.MaxValue))
            .ThenBy(pair => pair.original)
            .Select(pair => pair.station)
            .ToArray();
    }
}
