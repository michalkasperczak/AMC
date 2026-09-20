namespace AccessibleMediaController.Core.Podcasts;

/// <summary>
/// Dwie kolekcje publicznych materialow internetowych (YouTube bez logowania).
/// Michal ustalil 20.09.2026: Enter na wyniku ma OTWORZYC i zagrac, bez zapisu
/// do Biblioteki. Jedna wspolna kolekcja tego nie unosi: gdy raz znalazla sie w
/// Bibliotece, KAZDY nastepny podglad ladowal w zapisanej kolekcji.
///
/// Dlatego material tymczasowy trzyma <see cref="PreviewId"/> (nigdy w
/// Bibliotece), a jawne dodanie PRZENOSI odcinek do <see cref="SavedId"/>.
/// Przenosiny zmieniaja WYLACZNIE przynaleznosc odcinka - jego identyfikator
/// zostaje, wiec odtwarzanie, powrot fokusu, historia, kolejka i zakladki
/// (wszystkie kluczowane identyfikatorem odcinka) przezywaja promocje.
///
/// Obie kolekcje sa zwyklymi subskrypcjami, wiec ida przez PodcastLibraryDatabase,
/// klonowanie stanu oraz import i eksport bez zadnego nowego pola.
/// </summary>
public static class PublicInternetMediaCollections
{
    /// <summary>Kolekcja jawnie zapisana przez uzytkownika.</summary>
    public const string SavedId = "internet-media:public";

    /// <summary>Kolekcja podgladow: material otwarty bez zapisu do Biblioteki.</summary>
    public const string PreviewId = "internet-media:preview";

    public const string SavedTitle = "Media internetowe";
    public const string PreviewTitle = "Podglądy internetowe";

    /// <summary>Kolekcja, do ktorej trafia nowo otwarty material publiczny.</summary>
    public static string ResolveId(bool addToLibrary) => addToLibrary ? SavedId : PreviewId;

    /// <summary>Nazwa kolekcji zgodna z <see cref="ResolveId"/>.</summary>
    public static string ResolveTitle(bool addToLibrary) => addToLibrary ? SavedTitle : PreviewTitle;

    public static bool IsCollection(string? subscriptionId) =>
        string.Equals(subscriptionId, SavedId, StringComparison.Ordinal)
        || string.Equals(subscriptionId, PreviewId, StringComparison.Ordinal);

    public static bool IsPreview(string? subscriptionId) =>
        string.Equals(subscriptionId, PreviewId, StringComparison.Ordinal);

    public static bool IsSaved(string? subscriptionId) =>
        string.Equals(subscriptionId, SavedId, StringComparison.Ordinal);
}
