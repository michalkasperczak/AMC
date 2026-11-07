using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using AccessibleMediaController.Core.Spotify;

internal static class SpotifyPlaylistWriteTests
{
    public static void Run()
    {
        TestScopes();
        TestTargetsAndAccessibleLabels();
        TestCreatePrivateAndPublic();
        TestIdempotenceBatchesAndSnapshots();
        TestLaterBatchFailureKeepsEarlierSnapshot();
        TestPartialResult();
        TestRemoveUniqueItemAndRejectDuplicate();
    }

    private static void TestScopes()
    {
        Check(SpotifyScopes.AllowsPlaylistWrite(SpotifyScopes.Requested, false),
            "pełny zakres pozwala zmienić prywatną playlistę");
        Check(SpotifyScopes.AllowsPlaylistWrite(SpotifyScopes.Requested, true),
            "pełny zakres pozwala zmienić publiczną playlistę");
        Check(SpotifyScopes.MissingPlaylistScopes("playlist-modify-private").SequenceEqual(
                ["playlist-modify-public"]),
            "brakujący zakres publiczny jest jawny");
    }

    private static void TestTargetsAndAccessibleLabels()
    {
        var stub = new PlaylistHttpStub();
        using var client = new SpotifyPlaylistWriteClient(new HttpClient(stub));
        var targets = client.GetEditablePlaylistsAsync("token", "user1", CancellationToken.None)
            .GetAwaiter().GetResult();
        Check(targets.Count == 2, "właściciel i playlista współdzielona są edytowalne");
        Check(targets.All(target => !target.AccessibleLabel.Contains(target.Id, StringComparison.Ordinal)),
            "etykieta dostępna nie ujawnia identyfikatora playlisty");
        Check(targets.All(target => target.ToString() == target.AccessibleLabel),
            "ToString zwraca tylko celową etykietę użytkową");
    }

    private static void TestCreatePrivateAndPublic()
    {
        var stub = new PlaylistHttpStub();
        using var client = new SpotifyPlaylistWriteClient(new HttpClient(stub));
        var privateList = client.CreatePlaylistAsync(
            "token", SpotifyScopes.Requested, "user1", "Prywatna", false, CancellationToken.None)
            .GetAwaiter().GetResult();
        var publicList = client.CreatePlaylistAsync(
            "token", SpotifyScopes.Requested, "user1", "Publiczna", true, CancellationToken.None)
            .GetAwaiter().GetResult();
        Check(!privateList.IsPublic && publicList.IsPublic,
            "widoczność utworzonej playlisty jest zachowana");
        Check(stub.CreatedPublic.SequenceEqual([false, true]),
            "API dostało jawne wartości prywatna i publiczna");

        var noScope = Throws(() => client.CreatePlaylistAsync(
            "token", "playlist-modify-private", "user1", "Brak zgody", true, CancellationToken.None)
            .GetAwaiter().GetResult());
        Check(noScope is SpotifyWriteBlockedException { Reason: SpotifyWriteBlockReason.MissingScope },
            "brak zakresu blokuje zapis przed żądaniem");
    }

    private static void TestIdempotenceBatchesAndSnapshots()
    {
        var stub = new PlaylistHttpStub();
        stub.Items["own"] = ["spotify:track:t000"];
        using var client = new SpotifyPlaylistWriteClient(new HttpClient(stub));
        var target = new SpotifyPlaylistTarget(
            "own", "Moja", "user1", "Użytkownik", false, false, true, "before");
        var uris = Enumerable.Range(0, 206).Select(i => $"spotify:track:t{i:D3}").ToArray();
        var result = client.AddItemsAsync(
            "token", SpotifyScopes.Requested, [target], uris, CancellationToken.None)
            .GetAwaiter().GetResult();
        var outcome = result.Playlists.Single();
        Check(outcome.AlreadyPresentCount == 1 && outcome.AddedCount == 205,
            "obecna pozycja nie została dodana drugi raz");
        Check(stub.AddBatchSizes.SequenceEqual([100, 100, 5]),
            "zapis jest podzielony na partie najwyżej 100 pozycji");
        Check(outcome.SnapshotId == "snapshot-3",
            "wynik zachowuje ostatni potwierdzony snapshot_id");
        Check(stub.Requests.Any(request => request.Contains("/playlists/own/items", StringComparison.Ordinal)),
            "użyto aktualnego endpointu /items");
        Check(stub.Requests.All(request => !request.Contains("/tracks", StringComparison.Ordinal)),
            "nie użyto wycofanego endpointu /tracks");
    }

    private static void TestPartialResult()
    {
        var stub = new PlaylistHttpStub { ForbiddenPlaylistId = "bad" };
        using var client = new SpotifyPlaylistWriteClient(new HttpClient(stub));
        var good = new SpotifyPlaylistTarget(
            "own", "Dobra", "user1", "Użytkownik", false, false, true, "");
        var bad = new SpotifyPlaylistTarget(
            "bad", "Odmowa", "user1", "Użytkownik", false, false, true, "");
        var result = client.AddItemsAsync(
            "token",
            SpotifyScopes.Requested,
            [good, bad],
            ["spotify:episode:e1"],
            CancellationToken.None).GetAwaiter().GetResult();
        Check(result.IsPartial && result.AddedCount == 1 && result.FailedCount == 1,
            "sukces jednej playlisty nie ginie przy odmowie drugiej");
        Check(result.Playlists.Single(item => item.Target.Id == "bad").Message.Contains(
                "odmówił", StringComparison.OrdinalIgnoreCase),
            "wynik częściowy ma użytkową przyczynę odmowy");
    }

    private static void TestLaterBatchFailureKeepsEarlierSnapshot()
    {
        var stub = new PlaylistHttpStub { FailAddAfterBatches = 1 };
        using var client = new SpotifyPlaylistWriteClient(new HttpClient(stub));
        var target = new SpotifyPlaylistTarget(
            "own", "Partie", "user1", "Użytkownik", false, false, true, "before");
        var uris = Enumerable.Range(0, 150).Select(i => $"spotify:track:b{i:D3}").ToArray();
        var outcome = client.AddItemsAsync(
            "token", SpotifyScopes.Requested, [target], uris, CancellationToken.None)
            .GetAwaiter().GetResult().Playlists.Single();
        Check(outcome.AddedCount == 100 && outcome.FailedCount == 50,
            "awaria drugiej partii zachowuje wynik pierwszej");
        Check(outcome.SnapshotId == "snapshot-1"
            && outcome.Message.Contains("Częściowy wynik", StringComparison.Ordinal),
            "częściowy wynik zachowuje ostatni snapshot i jasny komunikat");
    }

    private static void TestRemoveUniqueItemAndRejectDuplicate()
    {
        var stub = new PlaylistHttpStub();
        stub.Items["own"] = ["spotify:track:one", "spotify:episode:two",
            "spotify:track:duplicate", "spotify:track:duplicate"];
        using var client = new SpotifyPlaylistWriteClient(new HttpClient(stub));
        var target = new SpotifyPlaylistTarget(
            "own", "Moja", "user1", "Użytkownik", false, false, true, "before");
        var duplicateError = Throws(() => client.RemoveItemsAsync(
            "token", SpotifyScopes.Requested, target,
            ["spotify:track:duplicate"], CancellationToken.None)
            .GetAwaiter().GetResult());
        Check(duplicateError is SpotifyWriteBlockedException
            && stub.Items["own"].Count == 4,
            "duplikat nie uruchamia DELETE i nie znika z playlisty");
        var removed = client.RemoveItemsAsync(
            "token", SpotifyScopes.Requested, target,
            ["spotify:track:one", "spotify:episode:two"], CancellationToken.None)
            .GetAwaiter().GetResult();
        Check(removed == 2 && stub.Items["own"].SequenceEqual(
                ["spotify:track:duplicate", "spotify:track:duplicate"]),
            "Delete usuwa tylko wybrane unikatowe pozycje");
        Check(stub.Requests.Any(request => request.StartsWith("DELETE ", StringComparison.Ordinal)
                && request.Contains("/playlists/own/items", StringComparison.Ordinal)),
            "usuwanie używa aktualnego endpointu /items");
    }

    private static Exception? Throws(Action action)
    {
        try { action(); return null; }
        catch (Exception exception) { return exception; }
    }

    private static void Check(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException($"Playlisty Spotify: {description}");
    }

    private sealed class PlaylistHttpStub : HttpMessageHandler
    {
        public Dictionary<string, List<string>> Items { get; } = new(StringComparer.Ordinal);
        public List<int> AddBatchSizes { get; } = [];
        public List<bool> CreatedPublic { get; } = [];
        public List<string> Requests { get; } = [];
        public string? ForbiddenPlaylistId { get; init; }
        public int? FailAddAfterBatches { get; init; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var url = request.RequestUri?.AbsoluteUri ?? string.Empty;
            Requests.Add($"{request.Method} {url}");
            if (request.Method == HttpMethod.Get && url.Contains("/me/playlists", StringComparison.Ordinal))
            {
                return Json("""
                    {"items":[
                      {"id":"technical-private-741","name":"Moja","public":false,"collaborative":false,"snapshot_id":"s1","owner":{"id":"user1","display_name":"Użytkownik"}},
                      {"id":"technical-shared-852","name":"Wspólna","public":true,"collaborative":true,"snapshot_id":"s2","owner":{"id":"other","display_name":"Inna osoba"}},
                      {"id":"technical-foreign-963","name":"Cudza","public":true,"collaborative":false,"snapshot_id":"s3","owner":{"id":"other","display_name":"Inna osoba"}}
                    ],"next":null}
                    """);
            }
            if (request.Method == HttpMethod.Post && url.EndsWith("/me/playlists", StringComparison.Ordinal))
            {
                var body = await request.Content!.ReadAsStringAsync(cancellationToken);
                using var document = JsonDocument.Parse(body);
                var isPublic = document.RootElement.GetProperty("public").GetBoolean();
                CreatedPublic.Add(isPublic);
                var name = document.RootElement.GetProperty("name").GetString();
                return Json(JsonSerializer.Serialize(new
                {
                    id = $"created-{CreatedPublic.Count}",
                    name,
                    @public = isPublic,
                    collaborative = false,
                    snapshot_id = "created",
                    owner = new { id = "user1", display_name = "Użytkownik" }
                }), HttpStatusCode.Created);
            }
            var marker = "/playlists/";
            var start = url.IndexOf(marker, StringComparison.Ordinal);
            if (start >= 0)
            {
                var rest = url[(start + marker.Length)..];
                var playlistId = rest.Split('/')[0];
                if (playlistId == ForbiddenPlaylistId)
                    return new HttpResponseMessage(HttpStatusCode.Forbidden);
                if (request.Method == HttpMethod.Get)
                {
                    var entries = Items.GetValueOrDefault(playlistId, [])
                        .Select((uri, index) => index % 2 == 0
                            ? new Dictionary<string, object?>
                            {
                                ["item"] = new { uri },
                                ["track"] = null
                            }
                            : new Dictionary<string, object?>
                            {
                                ["item"] = null,
                                ["track"] = new { uri }
                            })
                        .ToArray();
                    return Json(JsonSerializer.Serialize(new { items = entries, next = (string?)null }));
                }
                if (request.Method == HttpMethod.Post)
                {
                    if (FailAddAfterBatches is { } allowed
                        && AddBatchSizes.Count >= allowed)
                        return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
                    var body = await request.Content!.ReadAsStringAsync(cancellationToken);
                    using var document = JsonDocument.Parse(body);
                    var uris = document.RootElement.GetProperty("uris").EnumerateArray()
                        .Select(value => value.GetString()!).ToArray();
                    AddBatchSizes.Add(uris.Length);
                    if (!Items.TryGetValue(playlistId, out var stored)) Items[playlistId] = stored = [];
                    stored.AddRange(uris);
                    return Json(JsonSerializer.Serialize(new { snapshot_id = $"snapshot-{AddBatchSizes.Count}" }),
                        HttpStatusCode.Created);
                }
                if (request.Method == HttpMethod.Delete)
                {
                    var body = await request.Content!.ReadAsStringAsync(cancellationToken);
                    using var document = JsonDocument.Parse(body);
                    var uris = document.RootElement.GetProperty("items").EnumerateArray()
                        .Select(value => value.GetProperty("uri").GetString()!).ToHashSet();
                    Items[playlistId].RemoveAll(uri => uris.Contains(uri));
                    return Json("""{"snapshot_id":"after-remove"}""");
                }
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        private static HttpResponseMessage Json(
            string body,
            HttpStatusCode status = HttpStatusCode.OK) => new(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
    }
}
