using AccessibleMediaController.Core.Sessions;

namespace AccessibleMediaController.Core.Spotify;

/// <summary>Catalogue metadata may be shared, mutable playback/queue rows must not be.</summary>
public static class SpotifySessionItemCopies
{
    public static MediaItem ForSession(MediaItem source, string sessionId)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!SpotifyPlaybackSettingsResolver.IsSpotifySession(sessionId))
            throw new ArgumentException("Nieznana sesja Spotify.", nameof(sessionId));
        var prefix = sessionId + ":";
        return new MediaItem
        {
            Id = source.Id.StartsWith(prefix, StringComparison.Ordinal) ? source.Id : prefix + source.Id,
            Title = source.Title,
            HasCustomTitle = source.HasCustomTitle,
            Artist = source.Artist,
            Kind = source.Kind,
            Duration = source.Duration,
            BitrateKbps = source.BitrateKbps,
            IsBitrateEstimated = source.IsBitrateEstimated,
            SampleRateHz = source.SampleRateHz,
            Source = source.Source,
            PublicUri = source.PublicUri,
            HomepageUri = source.HomepageUri,
            Country = source.Country,
            Language = source.Language,
            Tags = source.Tags,
            Codec = source.Codec,
            ExternalId = source.ExternalId,
            RelatedAlbumExternalId = source.RelatedAlbumExternalId,
            RelatedAlbumTitle = source.RelatedAlbumTitle,
            RelatedArtistExternalId = source.RelatedArtistExternalId,
            RelatedArtistName = source.RelatedArtistName,
            ContainerEntryId = source.ContainerEntryId,
            CollectionAddedUtcTicks = source.CollectionAddedUtcTicks,
            IsFavorite = source.IsFavorite,
            IsInLibrary = source.IsInLibrary,
            IsAvailable = source.IsAvailable,
            IsInQueue = false,
            IsPlayNext = false
        };
    }
}
