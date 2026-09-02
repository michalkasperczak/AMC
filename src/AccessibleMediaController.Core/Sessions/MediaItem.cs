namespace AccessibleMediaController.Core.Sessions;

public enum MediaItemKind
{
    Track,
    Album,
    Playlist,
    Artist,
    Station,
    Device,
    Folder,
    Podcast,
    Episode
}

public sealed class MediaItem
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = string.Empty;
    public bool HasCustomTitle { get; set; }
    public string Artist { get; init; } = string.Empty;
    public MediaItemKind Kind { get; init; } = MediaItemKind.Track;
    public TimeSpan Duration { get; set; }
    public int? BitrateKbps { get; set; }
    public bool IsBitrateEstimated { get; set; }
    public int? SampleRateHz { get; set; }
    public string? Source { get; set; }
    public string? PublicUri { get; set; }
    public string? HomepageUri { get; set; }
    public string? Country { get; set; }
    public string? Language { get; set; }
    public string? Tags { get; set; }
    public string? Codec { get; set; }
    public string? ExternalId { get; set; }
    public bool IsFavorite { get; set; }
    public bool IsInLibrary { get; set; }
    public bool IsAvailable { get; set; } = true;
    public bool IsInQueue { get; set; }
    public bool IsPlayNext { get; set; }

    // Title currently stores the semantic primary name for every resource kind:
    // a track title, album title, playlist name, artist name, station name or
    // device name. Keep this separate from the configurable accessible label.
    // PublicUri is the canonical shareable service link. Source may instead be
    // a local path, a temporary stream URL or another private playback handle.
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
        MediaItemKind.Folder => "folder",
        MediaItemKind.Podcast => "podcast",
        MediaItemKind.Episode => "odcinek",
        _ => "element"
    };

    public override string ToString() => AccessibleLabel;
}
