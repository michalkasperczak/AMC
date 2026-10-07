using AccessibleMediaController.Core.Sonos;

/// <summary>
/// POMIAR ZASAD POWROTU DO PODLISTY SONOSA. Bez okna, bez czytnika, bez sieci:
/// mierzy DECYZJE, ktora wykonuje potem okno Windows.
/// </summary>
internal static class SonosSublistReturnPolicyTests
{
    internal static void Run()
    {
        MeasuresKnownCategories();
        MeasuresCaptureRejectsUnknown();
        MeasuresContextGuards();
        MeasuresRowResolution();
        MeasuresSlotLeaveGate();
        Console.WriteLine("OK: zasady powrotu do podlisty Sonosa - kategorie, granice domu i celu, wybor wiersza, "
            + "brama wyjscia dla slotu");
    }

    /// <summary>
    /// BRAMA WYJSCIA Z PODLISTY. Mierzy DOKLADNIE te dwa przypadki, w ktorych
    /// stare zachowanie bezwarunkowo zamykalo liste, mimo ze sesja sie nie
    /// zmieniala: slot NIEPRZYPISANY i slot BIEZACEJ sesji.
    /// </summary>
    private static void MeasuresSlotLeaveGate()
    {
        var slots = new Dictionary<int, string>
        {
            [1] = "local",
            [5] = "sonos",
            [8] = "radio"
        };

        // SLOT INNEJ SESJI: jedyny przypadek, w ktorym wolno zwinac liste.
        if (!SonosSublistReturnPolicy.ShouldLeaveSublistForSlot(slots, 8, "sonos"))
            throw new Exception("Slot innej sesji nie pozwolil wyjsc z podlisty.");

        // SLOT BIEZACEJ SESJI: zadnej zmiany, wiec zadnego zamykania listy.
        if (SonosSublistReturnPolicy.ShouldLeaveSublistForSlot(slots, 5, "sonos"))
            throw new Exception("Slot JUZ AKTYWNEJ sesji wyrzucil uzytkownika z podlisty.");

        // SLOT NIEPRZYPISANY: router powie tylko "nieprzypisana" - lista zostaje.
        if (SonosSublistReturnPolicy.ShouldLeaveSublistForSlot(slots, 4, "sonos"))
            throw new Exception("Slot NIEPRZYPISANY wyrzucil uzytkownika z podlisty.");

        // PUSTY wpis w mapie nie jest celem.
        if (SonosSublistReturnPolicy.ShouldLeaveSublistForSlot(
                new Dictionary<int, string> { [3] = string.Empty }, 3, "sonos"))
            throw new Exception("Puste przypisanie slotu zostalo uznane za cel.");

        // BRAK MAPY to brak decyzji na "tak".
        if (SonosSublistReturnPolicy.ShouldLeaveSublistForSlot(null, 8, "sonos"))
            throw new Exception("Brak mapy slotow pozwolil wyjsc z podlisty.");
    }

    private static void MeasuresKnownCategories()
    {
        foreach (var id in new[]
                 {
                     SonosLibraryPresentation.OwnStreamsCategoryId,
                     SonosLibraryPresentation.FavoritesCategoryId,
                     SonosLibraryPresentation.PlaylistsCategoryId
                 })
        {
            if (!SonosSublistReturnPolicy.IsKnownCategory(id))
                throw new Exception("Rzeczywista podlista nie jest rozpoznana: " + id);
        }

        // NIEZNANY identyfikator NIE MA prawa otwierac zadnego okna.
        if (SonosSublistReturnPolicy.IsKnownCategory("sonos.library.cos-nowego"))
            throw new Exception("Nieznana kategoria zostala przyjeta do powrotu.");
        if (SonosSublistReturnPolicy.IsKnownCategory(null))
            throw new Exception("Brak kategorii zostal przyjety do powrotu.");
    }

    private static void MeasuresCaptureRejectsUnknown()
    {
        var captured = SonosSublistReturnPolicy.Capture(
            SonosLibraryPresentation.OwnStreamsCategoryId, "stacja-7", "DOM-1", "GRUPA-1");
        if (captured is null) throw new Exception("Nie zapamietano prawdziwej podlisty.");
        if (captured.SelectedRowId != "stacja-7" || captured.HouseholdId != "DOM-1"
            || captured.GroupId != "GRUPA-1")
            throw new Exception("Zapamietane miejsce zgubilo wiersz albo kontekst celu.");

        if (SonosSublistReturnPolicy.Capture("nie-ma-takiej", "x", "DOM-1", "GRUPA-1") is not null)
            throw new Exception("Zapamietano nieznana kategorie.");
    }

    private static void MeasuresContextGuards()
    {
        var remembered = SonosSublistReturnPolicy.Capture(
            SonosLibraryPresentation.FavoritesCategoryId, "fav-3", "DOM-1", "GRUPA-1")!;

        if (!SonosSublistReturnPolicy.CanReopen(remembered, "DOM-1", "GRUPA-1"))
            throw new Exception("Ten sam dom i cel nie pozwolily wrocic do podlisty.");

        // ZMIANA DOMU: zapamietane miejsce opisuje material, ktorego tu nie ma.
        if (SonosSublistReturnPolicy.CanReopen(remembered, "DOM-2", "GRUPA-1"))
            throw new Exception("Powrot przeszedl po zmianie domu.");

        // ZMIANA CELU: lista dotyczyla innej grupy.
        if (SonosSublistReturnPolicy.CanReopen(remembered, "DOM-1", "GRUPA-2"))
            throw new Exception("Powrot przeszedl po zmianie celu.");

        // ZDJETY CEL tez jest zmiana, nie "czymkolwiek".
        if (SonosSublistReturnPolicy.CanReopen(remembered, "DOM-1", null))
            throw new Exception("Powrot przeszedl po zdjeciu celu.");

        // BRAK ZADANIA to brak powrotu - zwykle wejscie w sesje nic nie otwiera.
        if (SonosSublistReturnPolicy.CanReopen(null, "DOM-1", "GRUPA-1"))
            throw new Exception("Powrot zadzialal bez zapamietanego miejsca.");
    }

    private static void MeasuresRowResolution()
    {
        string[] rows = ["a", "b", "c"];

        if (SonosSublistReturnPolicy.ResolveRowIndex(rows, "b") != 1)
            throw new Exception("Nie wrocono na zapamietany wiersz.");

        // ZNIKNIETY wiersz: pierwszy, a nie cudzy material pod starym indeksem.
        if (SonosSublistReturnPolicy.ResolveRowIndex(rows, "znikl") != 0)
            throw new Exception("Zniknięty wiersz nie spadl na pierwszy.");

        if (SonosSublistReturnPolicy.ResolveRowIndex(rows, null) != 0)
            throw new Exception("Brak zapamietanego wiersza nie dal pierwszego.");

        // PUSTA lista nie ma czego zaznaczyc.
        if (SonosSublistReturnPolicy.ResolveRowIndex([], "a") != -1)
            throw new Exception("Pusta lista udala, ze ma wiersz.");
    }
}
