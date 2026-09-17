using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Devices.WiiM;

/// <summary>
/// Testy do zgloszen Michala z 17.09.2026:
/// 1) preset TIDAL na WiiM nie moze konczyc sie cisza,
/// 2) harmonogram nagrania musi POKAZAC folder stacji jako wybor.
/// </summary>
internal static class RadioFolderAndWiiMPresetTests
{
    public static void Run()
    {
        TestTidalPresetIsRecognizedAsUnplayable();
        TestOrdinaryPresetStaysPlayable();
        TestPresetWithUriPlaysEvenWhenSourceLooksLikeTidal();
        TestUnknownSourceDoesNotBlockPreset();
        TestStationFolderAppearsWithPath();
        TestStationWithoutFolderGivesTwoChoices();
        TestCustomFolderIsListedSeparately();
        TestCustomChoiceExistsForNewSchedule();
        TestSavedFolderEqualToStationSelectsStationChoice();
        TestInitialChoiceFollowsUserSetting();
        TestSavedCustomFolderWinsOverSetting();
    }

    // --- WiiM: preset TIDAL ---------------------------------------------------

    private static void TestTidalPresetIsRecognizedAsUnplayable()
    {
        // Dokladnie to, co zwrocilo urzadzenie Michala: zrodlo TIDAL, brak adresu.
        var preset = new WiiMPresetInformation(3, "Moja playlista", "TIDAL Connect", null);
        var reason = WiiMPresetPlayability.DescribeUnplayableReason(preset);
        Assert(
            reason is not null,
            "Preset TIDAL bez adresu musi byc rozpoznany jako nieuruchamialny, "
            + "inaczej AMC milczy, a nic nie gra.");
        Assert(
            reason!.Contains("TIDAL Connect", StringComparison.Ordinal),
            "Komunikat musi nazwac zrodlo, zeby uzytkownik wiedzial, czego dotyczy.");
        Assert(
            !WiiMPresetPlayability.CanActivate(preset),
            "CanActivate musi byc zgodne z DescribeUnplayableReason.");
    }

    private static void TestOrdinaryPresetStaysPlayable()
    {
        var preset = new WiiMPresetInformation(
            1, "Radio Poznan", "network", "http://stream.example/poznan");
        Assert(
            WiiMPresetPlayability.CanActivate(preset),
            "Zwykly preset radiowy musi dzialac tak jak dotad.");
    }

    private static void TestPresetWithUriPlaysEvenWhenSourceLooksLikeTidal()
    {
        // Adres jest mocniejszym dowodem niz nazwa zrodla - inaczej zablokowalibysmy
        // stacje, ktora ma "tidal" w nazwie albo w adresie.
        var preset = new WiiMPresetInformation(
            5, "Stacja", "TIDAL", "http://stream.example/tidal-radio");
        Assert(
            WiiMPresetPlayability.CanActivate(preset),
            "Preset z prawdziwym adresem nie moze byc blokowany po nazwie zrodla.");
    }

    private static void TestUnknownSourceDoesNotBlockPreset()
    {
        // "unknown" znaczy "urzadzenie nie podalo zrodla", a nie "nie zagra".
        var preset = new WiiMPresetInformation(7, "Preset", "unknown", null);
        Assert(
            WiiMPresetPlayability.CanActivate(preset),
            "Nieznane zrodlo nie jest dowodem, ze preset nie zagra - nie blokujemy go.");
    }

    // --- Harmonogram: trzy miejsca zapisu ------------------------------------
    //
    // ZGLOSZENIE Michala 17.09.2026 (drugie): pozycja listy NIE otwiera juz okna
    // wyboru folderu. Sciezke pokazuje osobne pole edycyjne, a dialog otwiera
    // osobny przycisk. Dlatego pozycje maja krotkie etykiety BEZ sciezki, a rodzaj
    // "Browse" przestal istniec.

    private static void TestStationFolderAppearsWithPath()
    {
        var choices = RadioScheduleFolderChoices.Build(@"D:\Nagrania\Dominikanie", null);
        var station = choices.FirstOrDefault(choice =>
            choice.Kind == RadioScheduleFolderKind.Station);
        Assert(
            station is not null,
            "Gdy stacja ma wlasny folder, harmonogram musi go POKAZAC jako wybor.");
        Assert(
            station!.Path == @"D:\Nagrania\Dominikanie",
            "Pozycja folderu stacji musi niesc sciezke, bo pole edycyjne ja z niej bierze.");
        Assert(
            !station.Label.Contains('\\', StringComparison.Ordinal),
            "Etykieta pozycji nie moze zawierac sciezki - czytnik czyta ja z pola obok.");
        Assert(
            choices.All(choice => choice.Label.Length <= 40),
            "Etykiety pozycji musza byc krotkie - dluga litania utrudnia sluchanie listy.");
    }

    private static void TestStationWithoutFolderGivesTwoChoices()
    {
        var choices = RadioScheduleFolderChoices.Build(null, null);
        Assert(
            choices.Count == 2,
            "Stacja bez wlasnego folderu daje dwie pozycje: domyslna i osobny folder.");
        Assert(
            choices.All(choice => choice.Kind != RadioScheduleFolderKind.Station),
            "Nie wolno pokazywac pustej pozycji folderu stacji.");
    }

    private static void TestCustomFolderIsListedSeparately()
    {
        var choices = RadioScheduleFolderChoices.Build(
            @"D:\Nagrania\Dominikanie",
            @"E:\Inny folder");
        Assert(
            choices.Count == 3,
            "Stacja z folderem daje trzy pozycje: domyslna, folder stacji, osobny folder.");
        var custom = choices.First(choice => choice.Kind == RadioScheduleFolderKind.Custom);
        Assert(
            custom.Path == @"E:\Inny folder",
            "Folder tego planu musi zachowac wskazana sciezke.");
    }

    private static void TestCustomChoiceExistsForNewSchedule()
    {
        // Bez tej pozycji nowy harmonogram nie mialby jak zglosic zamiaru wskazania
        // wlasnego folderu - wczesniej te role pelnila pozycja otwierajaca dialog.
        var choices = RadioScheduleFolderChoices.Build(@"D:\Nagrania\Dominikanie", null);
        var custom = choices.FirstOrDefault(choice =>
            choice.Kind == RadioScheduleFolderKind.Custom);
        Assert(
            custom is not null,
            "Pozycja osobnego folderu musi istniec takze przy NOWYM harmonogramie.");
        Assert(
            custom!.Path is null,
            "Przy nowym harmonogramie osobny folder nie ma jeszcze sciezki.");
    }

    private static void TestSavedFolderEqualToStationSelectsStationChoice()
    {
        var choices = RadioScheduleFolderChoices.Build(
            @"D:\Nagrania\Dominikanie",
            @"d:\nagrania\dominikanie");
        var initial = RadioScheduleFolderChoices.ResolveInitial(
            choices, savedCustomFolder: @"d:\nagrania\dominikanie", preferStationFolder: false);
        Assert(
            initial.Kind == RadioScheduleFolderKind.Station,
            "Gdy zapisana sciezka to folder stacji, uczciwiej zaznaczyc pozycje stacji - "
            + "uzytkownik slyszy wtedy, skad ta sciezka sie bierze.");
    }

    private static void TestInitialChoiceFollowsUserSetting()
    {
        var choices = RadioScheduleFolderChoices.Build(@"D:\Nagrania\Dominikanie", null);
        var preferStation = RadioScheduleFolderChoices.ResolveInitial(
            choices, savedCustomFolder: null, preferStationFolder: true);
        Assert(
            preferStation.Kind == RadioScheduleFolderKind.Station,
            "Przy wlaczonym ustawieniu nowy harmonogram zaczyna od folderu stacji.");

        var preferDefault = RadioScheduleFolderChoices.ResolveInitial(
            choices, savedCustomFolder: null, preferStationFolder: false);
        Assert(
            preferDefault.Kind == RadioScheduleFolderKind.Default,
            "Przy wylaczonym ustawieniu nowy harmonogram zaczyna od folderu domyslnego.");
    }

    private static void TestSavedCustomFolderWinsOverSetting()
    {
        var choices = RadioScheduleFolderChoices.Build(
            @"D:\Nagrania\Dominikanie",
            @"E:\Inny folder");
        var initial = RadioScheduleFolderChoices.ResolveInitial(
            choices, savedCustomFolder: @"E:\Inny folder", preferStationFolder: true);
        Assert(
            initial.Path == @"E:\Inny folder",
            "Folder zapisany w istniejacym harmonogramie to jawna decyzja uzytkownika "
            + "i nie moze byc nadpisany ustawieniem.");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
