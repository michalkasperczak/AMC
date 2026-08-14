namespace AccessibleMediaController.Core.Sessions;

public enum MediaItemKind
{
    Track,
    Album,
    Playlist,
    Artist,
    Station,
    Device
}

public sealed class MediaItem
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Title { get; init; } = string.Empty;
    public string Artist { get; init; } = string.Empty;
    public MediaItemKind Kind { get; init; } = MediaItemKind.Track;
    public TimeSpan Duration { get; init; }
    public bool IsFavorite { get; set; }
    public bool IsInLibrary { get; set; }
    public bool IsInQueue { get; set; }
    public bool IsPlayNext { get; set; }

    // Title currently stores the semantic primary name for every resource kind:
    // a track title, album title, playlist name, artist name, station name or
    // device name. Keep this separate from the configurable accessible label.
    public string PrimaryText => Title;

    public string AccessibleLabel => string.IsNullOrWhiteSpace(Artist)
        ? $"{Title}, {KindLabel}"
        : $"{Title}, {Artist}, {KindLabel}";

    public string KindLabel => Kind switch
    {
        MediaItemKind.Track => "utwór",
        MediaItemKind.Album => "album",
        MediaItemKind.Playlist => "playlista",
        MediaItemKind.Artist => "wykonawca",
        MediaItemKind.Station => "stacja",
        MediaItemKind.Device => "urządzenie",
        _ => "element"
    };

    public override string ToString() => AccessibleLabel;
}
