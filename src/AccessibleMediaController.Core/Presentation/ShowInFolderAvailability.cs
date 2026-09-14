namespace AccessibleMediaController.Core.Presentation;

using AccessibleMediaController.Core.Sessions;

/// <summary>
/// Rozstrzyga, czy dla elementu ma sens pozycja "Pokaż w folderze".
/// Zadanie 7 z listy 15.09.2026: pozycja ma byc wszedzie, gdzie ma sens - a
/// sens ma wtedy i tylko wtedy, gdy element wskazuje PLIK ALBO FOLDER na dysku.
///
/// Regula siedzi w Core, bo pytaja o nia dwa niezalezne miejsca w interfejsie
/// (menu listy i menu odtwarzacza) i wczesniej kazde liczylo ja po swojemu -
/// menu listy wymagalo, by KAZDY zaznaczony element byl plikiem, wiec dla
/// folderu z nagraniami pozycja w ogole sie nie pokazywala.
/// </summary>
public static class ShowInFolderAvailability
{
    /// <summary>
    /// Zwraca prawde, gdy sciezka elementu jest pelna sciezka lokalna. Istnienia
    /// pliku NIE sprawdzamy tutaj - dotkniecie dysku przy kazdym otwarciu menu
    /// zawiesza interfejs na niedostepnym dysku sieciowym, a brak pliku i tak
    /// jest zglaszany komunikatem w chwili wywolania pozycji.
    /// </summary>
    public static bool IsAvailableFor(MediaItem? item) =>
        item is not null && HasLocalPath(item.Source);

    public static bool HasLocalPath(string? source)
    {
        if (string.IsNullOrWhiteSpace(source)) return false;
        // Adresy sieciowe (radio, YouTube, podcast) odpadaja: nie ma czego
        // pokazac w Eksploratorze.
        if (Uri.TryCreate(source, UriKind.Absolute, out var uri) && !uri.IsFile) return false;
        return Path.IsPathFullyQualified(source);
    }
}
