using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Core.Presentation;

/// <summary>
/// Testy dostepnosci pozycji "Pokaż w folderze" (zadanie 7 z listy 15.09.2026).
///
/// Wczesniej regula istniala w DWOCH kopiach w interfejsie, a wersja z menu
/// listy wymagala, zeby element byl PLIKIEM - folder z nagraniami nie dostawal
/// tej pozycji w ogole. Tu mierzymy wspolna regule z Core.
/// </summary>
internal static class ShowInFolderTests
{
    internal static void Run()
    {
        LocalFileIsAvailable();
        LocalFolderIsAvailable();
        NetworkSourcesAreNotAvailable();
        EmptyAndRelativeSourcesAreNotAvailable();
        MissingItemIsNotAvailable();
    }

    private static void LocalFileIsAvailable()
    {
        Check(ShowInFolderAvailability.IsAvailableFor(
            new MediaItem { Id = "1", Title = "Nagranie", Source = @"C:\Nagrania\radio.mp3" }),
            "Plik na dysku ma dostac pozycje Pokaz w folderze");
    }

    private static void LocalFolderIsAvailable()
    {
        // To jest wlasciwa naprawa zadania 7: folder tez ma sens.
        Check(ShowInFolderAvailability.IsAvailableFor(
            new MediaItem
            {
                Id = "2",
                Title = "Nagrania",
                Kind = MediaItemKind.Folder,
                Source = @"C:\Nagrania"
            }),
            "Folder na dysku ma dostac pozycje Pokaz w folderze");
    }

    private static void NetworkSourcesAreNotAvailable()
    {
        foreach (var source in new[]
                 {
                     "https://stream.example.test/radio.mp3",
                     "http://example.test/odcinek.mp3",
                     "tidal:track:81326519"
                 })
        {
            Check(!ShowInFolderAvailability.HasLocalPath(source),
                $"Zrodlo sieciowe nie moze dostac pozycji Pokaz w folderze: {source}");
        }
    }

    private static void EmptyAndRelativeSourcesAreNotAvailable()
    {
        Check(!ShowInFolderAvailability.HasLocalPath(null), "Brak sciezki");
        Check(!ShowInFolderAvailability.HasLocalPath(""), "Pusta sciezka");
        Check(!ShowInFolderAvailability.HasLocalPath("   "), "Same odstepy");
        Check(!ShowInFolderAvailability.HasLocalPath(@"Nagrania\radio.mp3"),
            "Sciezka wzgledna nie wskazuje jednoznacznie miejsca na dysku");
    }

    private static void MissingItemIsNotAvailable()
    {
        Check(!ShowInFolderAvailability.IsAvailableFor(null),
            "Brak elementu nie moze dawac pozycji w menu");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
