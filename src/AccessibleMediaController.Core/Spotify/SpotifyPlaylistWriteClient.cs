using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace AccessibleMediaController.Core.Spotify;

/// <summary>
/// Jedna natywna playlista konta Spotify. Identyfikator i migawka pozostają
/// danymi modelu; <see cref="ToString"/> zwraca wyłącznie etykietę użytkową,
/// żeby kontrolka związana z danymi nie odczytała przez NVDA rekordu technicznego.
/// </summary>
public sealed record SpotifyPlaylistTarget(
    string Id,
    string Name,
    string OwnerId,
    string OwnerName,
    bool IsPublic,
    bool Collaborative,
    bool CanEdit,
    string SnapshotId)
{
    public string AccessibleLabel
    {
        get
        {
            var visibility = IsPublic ? "publiczna" : "prywatna";
            var shared = Collaborative ? ", współdzielona" : string.Empty;
            var owner = string.IsNullOrWhiteSpace(OwnerName)
                ? string.Empty
                : $", właściciel {OwnerName}";
            return $"{Name}, {visibility}{shared}{owner}";
        }
    }

    public override string ToString() => AccessibleLabel;
}

public sealed record SpotifyPlaylistTargetOutcome(
    SpotifyPlaylistTarget Target,
    int AddedCount,
    int AlreadyPresentCount,
    int FailedCount,
    string SnapshotId,
    string Message)
{
    public bool Succeeded => FailedCount == 0;
}

public sealed record SpotifyPlaylistWriteResult(
    int RequestedItemCount,
    IReadOnlyList<SpotifyPlaylistTargetOutcome> Playlists)
{
    public int AddedCount => Playlists.Sum(result => result.AddedCount);
    public int FailedCount => Playlists.Sum(result => result.FailedCount);
    public bool IsPartial => FailedCount > 0
        && Playlists.Any(result => result.Succeeded || result.AddedCount > 0);
}

/// <summary>
/// Odczyt, tworzenie i uzupełnianie natywnych playlist Spotify. Klient nie
/// rozpoczyna logowania i nie otwiera przeglądarki. Mutacje są dzielone na
/// partie do 100 URI, wejście jest odduplikowane, a przed zapisem odczytywana
/// jest zawartość playlisty, dzięki czemu ponowienie operacji nie dodaje
/// drugiego wystąpienia tej samej pozycji.
/// </summary>
public sealed class SpotifyPlaylistWriteClient(HttpClient? httpClient = null) : IDisposable
{
    private const string ApiRoot = "https://api.spotify.com/v1";
    public const int MaxItemsPerRequest = 100;
    private const int MaxPages = 400;

    private readonly HttpClient http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    private readonly bool ownsHttpClient = httpClient is null;

    public async Task<IReadOnlyList<SpotifyPlaylistTarget>> GetEditablePlaylistsAsync(
        string accessToken,
        string currentUserId,
        CancellationToken cancellationToken)
    {
        var result = new List<SpotifyPlaylistTarget>();
        string? next = $"{ApiRoot}/me/playlists?limit=50";
        var pages = 0;
        while (!string.IsNullOrWhiteSpace(next) && pages++ < MaxPages)
        {
            using var document = await GetJsonAsync(accessToken, next, cancellationToken)
                .ConfigureAwait(false);
            var root = document.RootElement;
            if (root.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in items.EnumerateArray())
                {
                    if (ReadTarget(item, currentUserId) is { CanEdit: true } target)
                        result.Add(target);
                }
            }
            next = Text(root, "next");
        }
        return result
            .GroupBy(target => target.Id, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(target => target.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    public async Task<SpotifyPlaylistTarget> CreatePlaylistAsync(
        string accessToken,
        string? grantedScope,
        string currentUserId,
        string name,
        bool isPublic,
        CancellationToken cancellationToken)
    {
        var cleanName = name?.Trim() ?? string.Empty;
        if (cleanName.Length is 0 or > 100)
            throw new SpotifyWriteBlockedException(
                "Nazwa playlisty Spotify musi mieć od 1 do 100 znaków.",
                SpotifyWriteBlockReason.UnsupportedKind);
        EnsureScope(grantedScope, isPublic);
        using var body = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            name = cleanName,
            @public = isPublic,
            description = "Utworzono w AMC"
        }));
        using var response = await SendAsync(
                HttpMethod.Post,
                $"{ApiRoot}/me/playlists",
                accessToken,
                body.RootElement.GetRawText(),
                cancellationToken)
            .ConfigureAwait(false);
        EnsureSuccess(response, "utworzenia playlisty");
        using var document = await ReadJsonAsync(response, cancellationToken).ConfigureAwait(false);
        return ReadTarget(document.RootElement, currentUserId)
            ?? throw new SpotifyWriteBlockedException(
                "Spotify utworzył playlistę, ale nie zwrócił jej danych. Odśwież listę playlist.",
                SpotifyWriteBlockReason.Denied);
    }

    public async Task<SpotifyPlaylistWriteResult> AddItemsAsync(
        string accessToken,
        string? grantedScope,
        IReadOnlyList<SpotifyPlaylistTarget> targets,
        IReadOnlyList<string> itemUris,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentNullException.ThrowIfNull(itemUris);
        var uniqueUris = itemUris
            .Select(uri => uri?.Trim() ?? string.Empty)
            .Where(IsSupportedItemUri)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (uniqueUris.Length == 0)
            throw new SpotifyWriteBlockedException(
                "Wybierz utwór albo odcinek Spotify, który można dodać do playlisty.",
                SpotifyWriteBlockReason.UnsupportedKind);
        var uniqueTargets = targets
            .Where(target => target.CanEdit && !string.IsNullOrWhiteSpace(target.Id))
            .GroupBy(target => target.Id, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
        if (uniqueTargets.Length == 0)
            throw new SpotifyWriteBlockedException(
                "Nie wybrano playlisty Spotify, którą można edytować.",
                SpotifyWriteBlockReason.UnsupportedKind);

        var outcomes = new List<SpotifyPlaylistTargetOutcome>();
        foreach (var target in uniqueTargets)
        {
            try
            {
                EnsureScope(grantedScope, target.IsPublic);
                var existing = await ReadExistingUrisAsync(
                        accessToken, target.Id, cancellationToken)
                    .ConfigureAwait(false);
                var toAdd = uniqueUris.Except(existing, StringComparer.Ordinal).ToArray();
                var snapshot = target.SnapshotId;
                var added = 0;
                var failed = 0;
                string? failure = null;
                var batches = toAdd.Chunk(MaxItemsPerRequest).ToArray();
                for (var batchIndex = 0; batchIndex < batches.Length; batchIndex++)
                {
                    var batch = batches[batchIndex];
                    try
                    {
                        using var response = await SendAsync(
                                HttpMethod.Post,
                                $"{ApiRoot}/playlists/{Uri.EscapeDataString(target.Id)}/items",
                                accessToken,
                                JsonSerializer.Serialize(new { uris = batch }),
                                cancellationToken)
                            .ConfigureAwait(false);
                        EnsureSuccess(response, $"dodania pozycji do playlisty {target.Name}");
                        using var document = await ReadJsonAsync(response, cancellationToken)
                            .ConfigureAwait(false);
                        snapshot = Text(document.RootElement, "snapshot_id") ?? snapshot;
                        added += batch.Length;
                    }
                    catch (Exception exception) when (exception is not OperationCanceledException)
                    {
                        failed = batches.Skip(batchIndex).Sum(remaining => remaining.Length);
                        failure = SafeFailure(exception, target.Name);
                        break;
                    }
                }
                var already = uniqueUris.Length - toAdd.Length;
                var message = failure is not null
                    ? (added > 0
                        ? $"Częściowy wynik dla playlisty {target.Name}: dodano {added}, bez potwierdzenia {failed}. {failure}"
                        : failure)
                    : added == 0
                        ? $"Playlista {target.Name} już zawiera wszystkie wybrane pozycje."
                        : $"Dodano {added} pozycji do playlisty {target.Name}."
                            + (already > 0 ? $" Już obecne: {already}." : string.Empty);
                outcomes.Add(new SpotifyPlaylistTargetOutcome(
                    target, added, already, failed, snapshot, message));
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                outcomes.Add(new SpotifyPlaylistTargetOutcome(
                    target,
                    0,
                    0,
                    uniqueUris.Length,
                    target.SnapshotId,
                    SafeFailure(exception, target.Name)));
            }
        }
        return new SpotifyPlaylistWriteResult(uniqueUris.Length, outcomes);
    }

    public async Task<int> RemoveItemsAsync(
        string accessToken,
        string? grantedScope,
        SpotifyPlaylistTarget target,
        IReadOnlyList<string> itemUris,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(itemUris);
        if (!target.CanEdit)
            throw new SpotifyWriteBlockedException(
                "Tej playlisty Spotify nie można edytować.",
                SpotifyWriteBlockReason.Denied);
        EnsureScope(grantedScope, target.IsPublic);
        var uris = itemUris.Distinct(StringComparer.Ordinal).ToArray();
        if (uris.Length is 0 or > MaxItemsPerRequest
            || uris.Any(uri => !IsSupportedItemUri(uri)))
            throw new SpotifyWriteBlockedException(
                "Wybierz od 1 do 100 utworów lub odcinków z playlisty Spotify.",
                SpotifyWriteBlockReason.UnsupportedKind);
        // Nowy endpoint usuwa po URI, bez pozycji. Nie wolno usuwać wybranego
        // wystąpienia, jeśli ta sama pozycja jest na playliście wielokrotnie.
        var counts = await ReadUriCountsAsync(accessToken, target.Id, cancellationToken)
            .ConfigureAwait(false);
        if (uris.Any(uri => !counts.TryGetValue(uri, out var count) || count != 1))
            throw new SpotifyWriteBlockedException(
                "Pozycja nie jest już na playliście albo występuje na niej więcej niż raz. Odśwież playlistę; duplikatów nie można bezpiecznie usunąć pojedynczo.",
                SpotifyWriteBlockReason.Denied);
        var body = new Dictionary<string, object>
        {
            ["items"] = uris.Select(uri => new { uri }).ToArray()
        };
        if (!string.IsNullOrWhiteSpace(target.SnapshotId))
            body["snapshot_id"] = target.SnapshotId;
        using var response = await SendAsync(
                HttpMethod.Delete,
                $"{ApiRoot}/playlists/{Uri.EscapeDataString(target.Id)}/items",
                accessToken,
                JsonSerializer.Serialize(body),
                cancellationToken)
            .ConfigureAwait(false);
        EnsureSuccess(response, $"usunięcia pozycji z playlisty {target.Name}");
        return uris.Length;
    }

    private async Task<Dictionary<string, int>> ReadUriCountsAsync(
        string accessToken, string playlistId, CancellationToken cancellationToken)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        string? next = $"{ApiRoot}/playlists/{Uri.EscapeDataString(playlistId)}/items?limit=100";
        var pages = 0;
        while (!string.IsNullOrWhiteSpace(next))
        {
            if (++pages > MaxPages)
                throw new SpotifyWriteBlockedException(
                    "Playlista jest zbyt długa, by bezpiecznie potwierdzić usunięcie.",
                    SpotifyWriteBlockReason.Denied);
            using var document = await GetJsonAsync(accessToken, next, cancellationToken)
                .ConfigureAwait(false);
            var root = document.RootElement;
            if (root.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
                foreach (var entry in items.EnumerateArray())
                    if (ReadPlaylistItemUri(entry) is { } uri)
                        counts[uri] = counts.GetValueOrDefault(uri) + 1;
            next = Text(root, "next");
        }
        return counts;
    }

    private async Task<HashSet<string>> ReadExistingUrisAsync(
        string accessToken,
        string playlistId,
        CancellationToken cancellationToken)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        string? next = $"{ApiRoot}/playlists/{Uri.EscapeDataString(playlistId)}/items?limit=100";
        var pages = 0;
        while (!string.IsNullOrWhiteSpace(next) && pages++ < MaxPages)
        {
            using var document = await GetJsonAsync(accessToken, next, cancellationToken)
                .ConfigureAwait(false);
            var root = document.RootElement;
            if (root.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
            {
                foreach (var entry in items.EnumerateArray())
                {
                    if (ReadPlaylistItemUri(entry) is { } uri) result.Add(uri);
                }
            }
            next = Text(root, "next");
        }
        return result;
    }

    private async Task<JsonDocument> GetJsonAsync(
        string accessToken,
        string url,
        CancellationToken cancellationToken)
    {
        using var response = await SendAsync(
                HttpMethod.Get, url, accessToken, null, cancellationToken)
            .ConfigureAwait(false);
        EnsureSuccess(response, "odczytu playlist");
        return await ReadJsonAsync(response, cancellationToken).ConfigureAwait(false);
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string url,
        string accessToken,
        string? json,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
            throw new SpotifyWriteBlockedException(
                "Nie ma zapisanego logowania Spotify. Otwórz Ctrl+F5 i zaloguj się.",
                SpotifyWriteBlockReason.Unauthorized);
        for (var attempt = 0; attempt < 4; attempt++)
        {
            using var request = new HttpRequestMessage(method, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            if (json is not null)
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode != HttpStatusCode.TooManyRequests) return response;
            var wait = response.Headers.RetryAfter?.Delta
                ?? (response.Headers.RetryAfter?.Date is { } date
                    ? date - DateTimeOffset.UtcNow
                    : TimeSpan.FromSeconds(2));
            response.Dispose();
            if (wait < TimeSpan.Zero) wait = TimeSpan.FromSeconds(2);
            if (wait > TimeSpan.FromMinutes(2))
                throw new SpotifyWriteBlockedException(
                    $"Spotify wstrzymał zapytania na {(int)wait.TotalSeconds} sekund. Spróbuj później.",
                    SpotifyWriteBlockReason.Denied);
            await Task.Delay(wait, cancellationToken).ConfigureAwait(false);
        }
        throw new SpotifyWriteBlockedException(
            "Spotify wielokrotnie ograniczył tempo zapytań. Spróbuj ponownie za chwilę.",
            SpotifyWriteBlockReason.Denied);
    }

    private static async Task<JsonDocument> ReadJsonAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using (stream.ConfigureAwait(false))
        {
            return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static void EnsureSuccess(HttpResponseMessage response, string operation)
    {
        if (response.IsSuccessStatusCode) return;
        throw response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => new SpotifyWriteBlockedException(
                "Logowanie Spotify wygasło. Otwórz Ctrl+F5 i zaloguj się ponownie.",
                SpotifyWriteBlockReason.Unauthorized),
            HttpStatusCode.Forbidden => new SpotifyWriteBlockedException(
                $"Spotify odmówił {operation}. Otwórz Ctrl+F5 i zaloguj się ponownie, aby przyznać uprawnienia do playlist.",
                SpotifyWriteBlockReason.Denied),
            _ => new SpotifyWriteBlockedException(
                $"Spotify nie wykonał {operation}: błąd HTTP {(int)response.StatusCode}.",
                SpotifyWriteBlockReason.Denied)
        };
    }

    private static void EnsureScope(string? grantedScope, bool isPublic)
    {
        if (SpotifyScopes.AllowsPlaylistWrite(grantedScope, isPublic)) return;
        throw new SpotifyWriteBlockedException(
            "Brakuje zgody Spotify na zmianę playlist. Otwórz Ctrl+F5 i zaloguj konto ponownie.",
            SpotifyWriteBlockReason.MissingScope);
    }

    private static SpotifyPlaylistTarget? ReadTarget(JsonElement item, string currentUserId)
    {
        if (item.ValueKind != JsonValueKind.Object) return null;
        var id = Text(item, "id")?.Trim();
        var name = Text(item, "name")?.Trim();
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name)) return null;
        var ownerId = string.Empty;
        var ownerName = string.Empty;
        if (item.TryGetProperty("owner", out var owner) && owner.ValueKind == JsonValueKind.Object)
        {
            ownerId = Text(owner, "id")?.Trim() ?? string.Empty;
            ownerName = Text(owner, "display_name")?.Trim() ?? ownerId;
        }
        var isPublic = item.TryGetProperty("public", out var publicValue)
            && publicValue.ValueKind == JsonValueKind.True;
        var collaborative = item.TryGetProperty("collaborative", out var collaborativeValue)
            && collaborativeValue.ValueKind == JsonValueKind.True;
        var canEdit = collaborative
            || (!string.IsNullOrWhiteSpace(currentUserId)
                && string.Equals(ownerId, currentUserId, StringComparison.Ordinal));
        return new SpotifyPlaylistTarget(
            id,
            name,
            ownerId,
            ownerName,
            isPublic,
            collaborative,
            canEdit,
            Text(item, "snapshot_id")?.Trim() ?? string.Empty);
    }

    private static string? ReadPlaylistItemUri(JsonElement entry)
    {
        if (entry.ValueKind != JsonValueKind.Object) return null;
        JsonElement item;
        if (entry.TryGetProperty("item", out var current) && current.ValueKind == JsonValueKind.Object)
            item = current;
        else if (entry.TryGetProperty("track", out var legacy) && legacy.ValueKind == JsonValueKind.Object)
            item = legacy;
        else
            return null;
        var uri = Text(item, "uri")?.Trim();
        if (IsSupportedItemUri(uri)) return uri;
        var type = Text(item, "type")?.Trim();
        var id = Text(item, "id")?.Trim();
        return (type is "track" or "episode") && !string.IsNullOrWhiteSpace(id)
            ? $"spotify:{type}:{id}"
            : null;
    }

    private static bool IsSupportedItemUri(string? uri) =>
        uri?.StartsWith("spotify:track:", StringComparison.Ordinal) == true
        || uri?.StartsWith("spotify:episode:", StringComparison.Ordinal) == true;

    private static string? Text(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string SafeFailure(Exception exception, string playlistName) => exception switch
    {
        SpotifyWriteBlockedException blocked => blocked.Message,
        HttpRequestException => $"Nie udało się połączyć ze Spotify dla playlisty {playlistName}.",
        JsonException => $"Spotify zwrócił nieprawidłowe dane playlisty {playlistName}.",
        _ => $"Nie udało się uzupełnić playlisty {playlistName}."
    };

    public void Dispose()
    {
        if (ownsHttpClient) http.Dispose();
    }
}
