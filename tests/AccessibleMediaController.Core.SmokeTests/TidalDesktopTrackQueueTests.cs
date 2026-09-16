using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Core.Tidal;

/// <summary>
/// Testy kolejki utworow TIDALa prowadzonej PRZEZ AMC.
///
/// Zgloszenie uzytkownika 16.09.2026: nastepny i poprzedni szly kolejka
/// oryginalnego TIDALa, nie lista widoczna w AMC. Te testy pilnuja, ze
/// kolejnosc bierze sie z listy AMC i ze koniec listy jest powiedziany, a nie
/// przemilczany.
/// </summary>
internal static class TidalDesktopTrackQueueTests
{
    internal static void Run()
    {
        QueueFollowsListOrderNotTidal();
        StartingInMiddleKeepsPosition();
        TracksWithoutAlbumAreSkipped();
        EndOfListIsReportedNotSilent();
        TrackOutsideListBecomesSingleItemQueue();
        UnplayableTrackGivesNoQueue();
        ClearForgetsQueue();
        EndMessageNamesTheList();
    }

    private static MediaItem Track(string title, string albumId = "111", string? externalId = null) =>
        new()
        {
            Title = title,
            Kind = MediaItemKind.Track,
            RelatedAlbumExternalId = albumId,
            ExternalId = externalId ?? $"tidal:tracks:{title}"
        };

    private static void QueueFollowsListOrderNotTidal()
    {
        var a = Track("Pierwszy");
        var b = Track("Drugi");
        var c = Track("Trzeci");
        var queue = new TidalDesktopTrackQueue();
        queue.Capture([a, b, c], a, "Moja playlista");

        Check(queue.HasQueue, "Kolejka z trzech utworow musi istniec");
        Check(queue.Count == 3, "Kolejka musi miec trzy utwory");
        Check(queue.HumanPosition == 1, "Start na pierwszym utworze");

        Check(queue.TryMove(true, out var next) && next!.Title == "Drugi",
            "Nastepny musi byc drugi utwor Z LISTY AMC");
        Check(queue.TryMove(true, out next) && next!.Title == "Trzeci",
            "Kolejny nastepny musi byc trzeci utwor z listy");
        Check(queue.TryMove(false, out var prev) && prev!.Title == "Drugi",
            "Poprzedni musi wrocic do drugiego utworu z listy");
    }

    private static void StartingInMiddleKeepsPosition()
    {
        var a = Track("A");
        var b = Track("B");
        var c = Track("C");
        var queue = new TidalDesktopTrackQueue();
        queue.Capture([a, b, c], b, "Album");

        Check(queue.HumanPosition == 2, "Start w srodku listy musi dac pozycje druga");
        Check(queue.Current!.Title == "B", "Grajacy utwor to ten, ktory uruchomiono");
        Check(queue.TryMove(false, out var prev) && prev!.Title == "A",
            "Poprzedni ze srodka listy musi dac utwor wczesniejszy");
    }

    private static void TracksWithoutAlbumAreSkipped()
    {
        var a = Track("Z albumem", "555");
        var bezAlbumu = Track("Bez albumu", albumId: "");
        var c = Track("Tez z albumem", "777");
        var queue = new TidalDesktopTrackQueue();
        queue.Capture([a, bezAlbumu, c], a, "Ulubione");

        Check(queue.Count == 2,
            "Utwor bez albumu nie moze wejsc do kolejki - TIDAL nie ma jak go wskazac");
        Check(queue.TryMove(true, out var next) && next!.Title == "Tez z albumem",
            "Nastepny musi przeskoczyc utwor, ktorego nie da sie zagrac");
    }

    private static void EndOfListIsReportedNotSilent()
    {
        var a = Track("Jedyny");
        var queue = new TidalDesktopTrackQueue();
        queue.Capture([a], a, "Lista");

        Check(!queue.TryMove(true, out _), "Za ostatnim utworem nie ma nastepnego");
        Check(!queue.TryMove(false, out _), "Przed pierwszym utworem nie ma poprzedniego");
        Check(queue.HumanPosition == 1, "Nieudany ruch nie moze przesunac pozycji");
    }

    private static void TrackOutsideListBecomesSingleItemQueue()
    {
        var naLiscie = Track("Na liscie", "1", "tidal:tracks:1");
        var zWyszukiwania = Track("Z wyszukiwania", "2", "tidal:tracks:2");
        var queue = new TidalDesktopTrackQueue();
        queue.Capture([naLiscie], zWyszukiwania, "Wyniki");

        Check(queue.Count == 1 && queue.Current!.Title == "Z wyszukiwania",
            "Utwor poza lista daje kolejke jednoelementowa, nie udawana wieksza");
        Check(!queue.TryMove(true, out _), "Kolejka jednoelementowa nie ma nastepnego");
    }

    private static void UnplayableTrackGivesNoQueue()
    {
        var bezAlbumu = Track("Bez albumu", albumId: "");
        var queue = new TidalDesktopTrackQueue();
        queue.Capture([bezAlbumu], bezAlbumu, "Lista");

        Check(!queue.HasQueue,
            "Utwor, ktorego TIDAL nie przyjmie, nie moze udawac kolejki");
        Check(!TidalDesktopTrackQueue.CanQueue(bezAlbumu),
            "Utwor bez albumu musi byc odrzucony przez CanQueue");
        Check(!TidalDesktopTrackQueue.CanQueue(null), "Brak elementu to brak kolejki");
        Check(!TidalDesktopTrackQueue.CanQueue(new MediaItem { Title = "Album", Kind = MediaItemKind.Album, RelatedAlbumExternalId = "9" }),
            "Do kolejki utworow wchodza tylko utwory");
    }

    private static void ClearForgetsQueue()
    {
        var a = Track("A");
        var queue = new TidalDesktopTrackQueue();
        queue.Capture([a, Track("B")], a, "Lista");
        queue.Clear();

        Check(!queue.HasQueue, "Po wyczyszczeniu kolejki nie ma");
        Check(queue.Current is null, "Po wyczyszczeniu nie ma grajacego utworu");
        Check(!queue.TryMove(true, out _), "Po wyczyszczeniu nastepny nie dziala");
    }

    private static void EndMessageNamesTheList()
    {
        var a = Track("A");
        var queue = new TidalDesktopTrackQueue();
        queue.Capture([a], a, "Moja playlista");

        var koniec = queue.EndOfQueueMessage(true);
        var poczatek = queue.EndOfQueueMessage(false);
        Check(koniec.Contains("Moja playlista", StringComparison.Ordinal),
            "Komunikat konca musi nazwac liste, bo uzytkownik moze patrzec na inna");
        Check(koniec.Contains("ostatni", StringComparison.Ordinal), "Koniec listy to ostatni utwor");
        Check(poczatek.Contains("pierwszy", StringComparison.Ordinal), "Poczatek listy to pierwszy utwor");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
