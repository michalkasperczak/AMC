using System.Net;
using System.Text.Json;
using AccessibleMediaController.Core.Presentation;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Core.Tidal;

internal static class TidalArtistBrowseTests
{
    internal static void Run() => Task.Run(RunAsync).GetAwaiter().GetResult();

    private static async Task RunAsync()
    {
        using var handler = new ArtistHandler();
        using var http = new HttpClient(handler);
        using var api = new TidalApiClient(http);
        var artist = new MediaItem { Id = "tidal:artists:one", ExternalId = "artists:one", Title = "Wykonawca", Kind = MediaItemKind.Artist };
        Check(Enum.GetValues<ArtistBrowseSection>().Select(section => section.Label())
            .SequenceEqual(["Albumy", "Utwory", "Podobni wykonawcy"]), "Etykiety kategorii");

        var albums = await api.GetContainerItemsAsync("test", "PL", artist, null,
            CancellationToken.None, ArtistBrowseSection.Albums);
        Check(albums.Select(item => item.ExternalId).SequenceEqual(["albums:b", "albums:a"]),
            "Kolejność, paginacja lub odrzucenie obcych obiektów included");
        Check(handler.Paths.Count == 2 && handler.Paths.All(path => path.Contains("/relationships/albums")),
            "Albumy pobierają również inne kategorie");
        Check(handler.Paths[0].Contains("albums.artists") && handler.Paths[0].Contains("countryCode=PL"),
            "Parametry zapytania albumów");
        handler.Paths.Clear();

        var tracks = await api.GetContainerItemsAsync("test", "PL", artist,
            new HashSet<string>(["tracks:t"]), CancellationToken.None, ArtistBrowseSection.Tracks);
        Check(tracks.Count == 1 && tracks[0].Kind == MediaItemKind.Track && tracks[0].IsFavorite,
            "Utwory są pomieszane z albumami lub nie zachowują Ulubionych");
        Check(tracks[0].Title == "Tytuł utworu" && tracks[0].Artist == "Wykonawca"
            && tracks[0].RelatedAlbumExternalId == "albums:b" && tracks[0].RelatedArtistExternalId == "artists:one",
            "Powiązania utworu");
        Check(handler.Paths.Count == 1 && handler.Paths[0].Contains("/relationships/tracks")
            && handler.Paths[0].Contains("tracks.artists,tracks.albums"), "Żądanie utworów");
        handler.Paths.Clear();

        var similar = await api.GetContainerItemsAsync("test", "PL", artist, null,
            CancellationToken.None, ArtistBrowseSection.SimilarArtists);
        Check(similar.Count == 1 && similar[0].ExternalId == "artists:two"
            && similar[0].Kind == MediaItemKind.Artist, "Podobni wykonawcy zawierają niepowiązane zasoby");
        Check(handler.Paths.Count == 1 && handler.Paths[0].Contains("/relationships/similarArtists"),
            "Żądanie podobnych wykonawców");

        handler.Empty = true;
        var empty = await api.GetContainerItemsAsync("test", "PL", artist, null,
            CancellationToken.None, ArtistBrowseSection.SimilarArtists);
        Check(empty.Count == 0, "Pusta relacja zamieniła obce included na wyniki");
        handler.Empty = false;
        handler.Fail = true;
        try
        {
            await api.GetContainerItemsAsync("test", "PL", artist, null, CancellationToken.None, ArtistBrowseSection.Tracks);
            throw new Exception("Błąd HTTP zamienił się w pustą listę");
        }
        catch (TidalApiException exception) when (exception.StatusCode == HttpStatusCode.Forbidden) { }

        var count = handler.Paths.Count;
        try
        {
            await api.GetContainerItemsAsync("test", "PL", new MediaItem { ExternalId = "albums:b" }, null,
                CancellationToken.None, ArtistBrowseSection.Tracks);
            throw new Exception("Otwarto kategorię wykonawcy na albumie");
        }
        catch (InvalidOperationException) { }
        Check(count == handler.Paths.Count, "Nieprawidłowy kontener wysłał żądanie");

        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        try
        {
            await api.GetContainerItemsAsync("test", "PL", artist, null, cancelled.Token, ArtistBrowseSection.Tracks);
            throw new Exception("Anulowanie nie działa");
        }
        catch (OperationCanceledException) { }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private sealed class ArtistHandler : HttpMessageHandler
    {
        internal List<string> Paths { get; } = [];
        internal bool Empty { get; set; }
        internal bool Fail { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var uri = request.RequestUri!;
            Paths.Add(Uri.UnescapeDataString(uri.PathAndQuery));
            if (Fail) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden)
                { Content = new StringContent("{\"errors\":[]}") });
            var relationship = uri.AbsolutePath.Split('/')[^1];
            var secondPage = uri.Query.Contains("cursor", StringComparison.OrdinalIgnoreCase);
            var (type, id) = relationship switch
            {
                "albums" => ("albums", secondPage ? "a" : "b"),
                "tracks" => ("tracks", "t"),
                "similarArtists" => ("artists", "two"),
                _ => throw new Exception("Nieoczekiwane żądanie")
            };
            object[] data = Empty ? [] : [new { type, id }];
            var body = JsonSerializer.Serialize(new
            {
                data,
                included = new object[]
                {
                    new { type = "artists", id = "one", attributes = new { name = "Wykonawca" } },
                    new { type = "artists", id = "two", attributes = new { name = "Drugi wykonawca" } },
                    new { type = "albums", id = "a", attributes = new { title = "Album A" } },
                    new { type = "albums", id = "b", attributes = new { title = "Album B" } },
                    new { type = "albums", id = "unrelated", attributes = new { title = "Obcy album" } },
                    new { type = "tracks", id = "t", attributes = new { title = "Tytuł utworu", duration = "PT3M" },
                        relationships = new {
                            artists = new { data = new[] { new { type = "artists", id = "one" } } },
                            albums = new { data = new[] { new { type = "albums", id = "b" } } }
                        } }
                },
                links = new { next = relationship == "albums" && !secondPage
                    ? "/v2/artists/one/relationships/albums?page%5Bcursor%5D=second&include=albums.artists&countryCode=PL"
                    : null }
            });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
        }
    }
}
