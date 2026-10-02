using AccessibleMediaController.Core.Configuration;

namespace AccessibleMediaController.Core.Sonos;

/// <summary>
/// ZASADY POWROTU DO PODLISTY SONOSA po przelaczeniu sesji (Ctrl+cyfra tam,
/// Ctrl+cyfra z powrotem).
///
/// DLACZEGO OSOBNA KLASA: decyzja "czy wolno wrocic do TEJ podlisty" jest czysta
/// i dalo sie ja zmierzyc bez okna, czytnika i sieci. Okno Windows tylko WYKONUJE
/// te decyzje; nie powtarza jej po swojemu.
///
/// CO TO NIE JEST: to NIE jest nowy mechanizm nawigacji. Zapamietane miejsce
/// odtwarzamy ISTNIEJACA droga Biblioteki (ten sam kod, co Enter na kategorii) -
/// tu rozstrzygamy wylacznie, czy wolno ja uruchomic.
/// </summary>
public static class SonosSublistReturnPolicy
{
    /// <summary>
    /// KATEGORIE, ktore maja wlasna podliste. Nieznany identyfikator nie wraca
    /// nigdzie: cisza jest lepsza od otwarcia losowego okna.
    /// </summary>
    public static bool IsKnownCategory(string? categoryId) =>
        string.Equals(categoryId, SonosLibraryPresentation.OwnStreamsCategoryId, StringComparison.Ordinal)
        || string.Equals(categoryId, SonosLibraryPresentation.FavoritesCategoryId, StringComparison.Ordinal)
        || string.Equals(categoryId, SonosLibraryPresentation.PlaylistsCategoryId, StringComparison.Ordinal);

    /// <summary>
    /// ZAPAMIETANIE miejsca. Zwraca <c>null</c>, gdy nie ma czego pamietac - wtedy
    /// wolajacy CZYSCI pole, a nie zostawia nieaktualnej wartosci.
    /// </summary>
    public static SonosSublistReturnState? Capture(
        string? categoryId,
        string? selectedRowId,
        string? householdId,
        string? groupId)
    {
        if (!IsKnownCategory(categoryId)) return null;
        return new SonosSublistReturnState
        {
            CategoryId = categoryId!,
            SelectedRowId = selectedRowId,
            HouseholdId = householdId,
            GroupId = groupId
        };
    }

    /// <summary>
    /// Czy WOLNO otworzyc zapamietana podliste TERAZ.
    ///
    /// Kolejnosc warunkow jest cala trescia bezpieczenstwa:
    ///  1) nie ma zapamietanego miejsca - nie ma powrotu,
    ///  2) nieznana kategoria - nie zgadujemy okna,
    ///  3) ZMIENIONY DOM albo ZMIENIONY CEL - miejsce przestalo istniec w tym
    ///     sensie, w jakim je zapamietano; wracamy do korzenia sesji zamiast
    ///     otwierac liste dla innego domu/celu.
    ///
    /// Punkt 3 jest tu po to, zeby powrot NIE obchodzil granic konta, domu i celu,
    /// ktorych pilnuje reszta sciezki Sonosa.
    /// </summary>
    public static bool CanReopen(
        SonosSublistReturnState? remembered,
        string? currentHouseholdId,
        string? currentGroupId)
    {
        if (remembered is null) return false;
        if (!IsKnownCategory(remembered.CategoryId)) return false;
        if (!string.Equals(remembered.HouseholdId, currentHouseholdId, StringComparison.Ordinal)) return false;
        if (!string.Equals(remembered.GroupId, currentGroupId, StringComparison.Ordinal)) return false;
        return true;
    }

    /// <summary>
    /// WIERSZ do zaznaczenia po otwarciu podlisty: zapamietany identyfikator, gdy
    /// NADAL jest na liscie, inaczej pierwszy wiersz (indeks 0) albo brak
    /// zaznaczenia przy pustej liscie.
    ///
    /// SWIADOMIE po identyfikatorze, nie po indeksie: lista mogla sie w tym czasie
    /// zmienic, a indeks wskazywalby wtedy CZYJS INNY material.
    /// </summary>
    public static int ResolveRowIndex(IReadOnlyList<string> rowIds, string? rememberedRowId)
    {
        ArgumentNullException.ThrowIfNull(rowIds);
        if (rowIds.Count == 0) return -1;
        if (rememberedRowId is null) return 0;
        for (var index = 0; index < rowIds.Count; index++)
        {
            if (string.Equals(rowIds[index], rememberedRowId, StringComparison.Ordinal)) return index;
        }

        return 0;
    }
}
