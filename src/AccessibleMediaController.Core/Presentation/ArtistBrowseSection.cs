namespace AccessibleMediaController.Core.Presentation;

/// <summary>Navigation categories, never media items or library membership.</summary>
public enum ArtistBrowseSection
{
    Albums,
    Tracks,
    SimilarArtists
}

public static class ArtistBrowseSections
{
    public static string Label(this ArtistBrowseSection section) => section switch
    {
        ArtistBrowseSection.Albums => "Albumy",
        ArtistBrowseSection.Tracks => "Utwory",
        ArtistBrowseSection.SimilarArtists => "Podobni wykonawcy",
        _ => throw new ArgumentOutOfRangeException(nameof(section))
    };
}
