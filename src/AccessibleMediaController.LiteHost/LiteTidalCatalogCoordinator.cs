using System.Text.Json;
using System.Text.Json.Serialization;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.LiteHost.Protocol;
using AccessibleMediaController.Windows.Services;

namespace AccessibleMediaController.LiteHost;

/// <summary>
/// Czytelnik katalogu TIDAL dla wxPython. Korzysta z tego samego klienta API,
/// tego samego odswiezania tokenu i tego samego Menedzera poswiadczen Windows
/// co glowne AMC. Token nigdy nie trafia do odpowiedzi protokolu. Jawnie
/// wywolane logowanie zapisuje go tylko w Menedzerze poswiadczen Windows.
/// </summary>
internal sealed class LiteTidalCatalogCoordinator(string statePath, string profileId = "test") : IDisposable
{
    internal static string CredentialTarget(string profileId) => profileId switch
    {
        "public" => "AccessibleMediaController.Public/TIDAL",
        "test" => "AccessibleMediaController/TIDAL",
        _ => throw new LiteRequestException("Nieznany profil wydania AMC.")
    };
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly object _gate = new();
    private readonly object _collectionGate = new();
    private TidalIntegrationService? _integration;
    private TidalSettings? _settings;
    private IReadOnlyList<MediaItem>? _collectionSnapshot;
    private bool _collectionSyncAttempted;

    public object AccountStatus()
    {
        var integration = RequireIntegration();
        var settings = _settings!;
        return LiteTidalCatalogContract.CreateAccountStatus(
            integration.IsConfigured,
            integration.HasStoredLogin,
            settings.AccountDisplayName,
            settings.RedirectUri);
    }

    public object Configure(JsonElement args)
    {
        var clientId = LiteTidalCatalogContract.ReadClientId(args);
        lock (_gate)
        {
            var settings = _settings ?? ReadSettings();
            if (!string.Equals(settings.ClientId, clientId, StringComparison.Ordinal))
            {
                settings.ClientId = clientId;
                try
                {
                    LiteAccountSettingsStore.UpdateSection(statePath, "tidal", settings);
                }
                catch
                {
                    throw new LiteRequestException("Nie udało się zapisać ustawień konta TIDAL.");
                }
                _integration?.Dispose();
                _integration = null;
                _settings = settings;
            }
        }
        return new { message = "Zapisano identyfikator aplikacji TIDAL. Możesz rozpocząć logowanie." };
    }

    public object ProbePlayback(JsonElement args)
    {
        var trackId = LiteTidalPlaybackProbe.ReadTrackId(args);
        try
        {
            var credentials = RequireIntegration()
                .GetPlaybackCredentialsAsync(CancellationToken.None)
                .GetAwaiter().GetResult();
            using var client = new HttpClient(new HttpClientHandler
            {
                AllowAutoRedirect = false
            }) { Timeout = TimeSpan.FromSeconds(15) };
            return LiteTidalPlaybackProbe.RunAsync(
                    client, trackId, credentials.AccessToken,
                    _settings!.CountryCode, CancellationToken.None)
                .GetAwaiter().GetResult();
        }
        catch (Exception)
        {
            // Credential and HTTP exceptions may include private details.
            // The protocol exposes only a fixed result code.
            return new LiteTidalPlaybackProbeResult(
                "unavailable", "credentials_unavailable", 0, false);
        }
    }

    public object Login()
    {
        var integration = RequireIntegration();
        var settings = _settings!;
        if (!integration.IsConfigured)
            throw new LiteRequestException(
                "Konto TIDAL nie jest skonfigurowane. Uzupełnij identyfikator aplikacji w ustawieniach AMC.");
        try
        {
            integration.LoginAsync(CancellationToken.None)
                .GetAwaiter()
                .GetResult();
            lock (_collectionGate)
            {
                _collectionSyncAttempted = false;
                _collectionSnapshot = null;
            }
            LiteAccountSettingsStore.UpdateSection(statePath, "tidal", settings);
            return LiteTidalCatalogContract.CreateLoginResult(settings.AccountDisplayName);
        }
        catch (OperationCanceledException)
        {
            throw new LiteRequestException("Logowanie TIDAL zostało anulowane.");
        }
        catch (HttpRequestException)
        {
            throw new LiteRequestException(
                "Nie udało się połączyć z TIDAL. Sprawdź połączenie z internetem i spróbuj ponownie.");
        }
        catch (InvalidOperationException exception)
        {
            throw new LiteRequestException(exception.Message);
        }
        catch
        {
            throw new LiteRequestException("Nie udało się zalogować konta TIDAL.");
        }
    }

    public object Disconnect()
    {
        lock (_gate)
        {
            var integration = RequireIntegration();
            try
            {
                integration.Disconnect();
                LiteAccountSettingsStore.UpdateSection(statePath, "tidal", _settings!);
            }
            catch
            {
                throw new LiteRequestException("Nie udało się odłączyć konta katalogu TIDAL.");
            }
            lock (_collectionGate)
            {
                _collectionSyncAttempted = false;
                _collectionSnapshot = null;
            }
        }
        return LiteTidalCatalogContract.CreateDisconnectResult();
    }

    public object Search(JsonElement args)
    {
        var query = LiteTidalCatalogContract.ReadSearchQuery(args);
        var items = RequireIntegration().SearchAsync(query, CancellationToken.None)
            .GetAwaiter()
            .GetResult();
        return LiteTidalCatalogContract.CreateSearchResult(items);
    }

    public object GetContainerItems(JsonElement args)
    {
        var request = LiteTidalCatalogContract.ReadRequest(args);
        var integration = RequireIntegration();
        var items = integration.GetContainerItemsAsync(
                request.Container,
                CancellationToken.None,
                request.ArtistSection)
            .GetAwaiter()
            .GetResult();
        return LiteTidalCatalogContract.CreateResult(
            request.Container,
            request.ArtistSection,
            items);
    }

    public object ChangeMembership(JsonElement args)
    {
        lock (_collectionGate)
        {
            var request = LiteTidalCatalogContract.ReadMembershipRequest(args);
            var result = RequireIntegration().ChangeCollectionMembershipAsync(
                    request.Items,
                    request.RequestedAddition,
                    CancellationToken.None)
                .GetAwaiter()
                .GetResult();
            _collectionSnapshot = result.Items;
            var label = result.ChangedCount == 1
                ? request.Items[0].Title
                : $"{result.ChangedCount} elementów";
            var destination = request.Mode == "favorite" ? "Ulubionych" : "Biblioteki";
            var message = result.Added
                ? $"Dodano do {destination} TIDAL: {label}"
                : $"Usunięto z {destination} TIDAL: {label}";
            return new LiteTidalMembershipResult(
                result.Added,
                result.ChangedCount,
                request.Items.Select(item => item.Id).ToArray(),
                message);
        }
    }

    public object GetCollectionView(JsonElement args)
    {
        var view = LiteTidalCatalogContract.ReadCollectionView(args);
        lock (_collectionGate)
        {
            var integration = RequireIntegration();
            var warning = string.Empty;
            if (!_collectionSyncAttempted)
            {
                _collectionSyncAttempted = true;
                try
                {
                    var synchronized = integration.SynchronizeAsync(CancellationToken.None)
                        .GetAwaiter()
                        .GetResult();
                    _collectionSnapshot = synchronized.Items;
                    warning = string.Join("; ", synchronized.Warnings);
                }
                catch (Exception exception)
                {
                    warning = "Nie udało się odświeżyć kolekcji TIDAL; pokazano ostatni zapis. "
                        + exception.Message;
                }
            }
            return LiteTidalCatalogContract.CreateCollectionViewResult(
                view,
                _collectionSnapshot ?? [],
                warning);
        }
    }

    public object CreatePlaylist(JsonElement args)
    {
        var name = LiteTidalCatalogContract.ReadPlaylistName(args);
        lock (_collectionGate)
        {
            var playlist = RequireIntegration().CreatePlaylistAsync(name, CancellationToken.None)
                .GetAwaiter().GetResult();
            _collectionSnapshot = (_collectionSnapshot ?? [])
                .Append(playlist)
                .DistinctBy(item => item.ExternalId ?? item.Id, StringComparer.Ordinal)
                .ToArray();
            return new
            {
                playlist = LiteTidalCatalogItem.FromMediaItem(playlist),
                message = $"Utworzono playlistę TIDAL: {playlist.Title}"
            };
        }
    }

    public object AddPlaylistItems(JsonElement args)
    {
        var request = LiteTidalCatalogContract.ReadPlaylistAddRequest(args);
        lock (_collectionGate)
        {
            var integration = RequireIntegration();
            var playlists = request.PlaylistExternalIds.Select(id =>
                (_collectionSnapshot ?? []).FirstOrDefault(item =>
                    item.Kind == MediaItemKind.Playlist
                    && string.Equals(item.ExternalId, id, StringComparison.Ordinal))
                ?? throw new LiteRequestException("Wybrana playlista TIDAL nie jest dostępna na koncie.")
            ).ToArray();
            var tracks = new List<MediaItem>();
            foreach (var item in request.Items)
            {
                if (item.Kind == MediaItemKind.Album)
                {
                    var albumTracks = integration.GetContainerItemsAsync(
                        item, CancellationToken.None).GetAwaiter().GetResult();
                    tracks.AddRange(albumTracks.Where(track =>
                        track.IsAvailable
                        && track.Kind is MediaItemKind.Track or MediaItemKind.Video));
                }
                else
                    tracks.Add(item);
            }
            tracks = tracks.DistinctBy(item => item.ExternalId ?? item.Id,
                StringComparer.Ordinal).ToList();
            if (tracks.Count == 0)
                throw new LiteRequestException("Album TIDAL nie zawiera dostępnych utworów.");
            var completed = new List<string>();
            var completedIds = new List<string>();
            var failed = new List<string>();
            foreach (var playlist in playlists)
            {
                try
                {
                    integration.AddPlaylistItemsAsync(playlist, tracks, CancellationToken.None)
                        .GetAwaiter().GetResult();
                    completed.Add(playlist.Title);
                    completedIds.Add(playlist.ExternalId!);
                }
                catch (Exception exception)
                {
                    var reason = exception is InvalidOperationException
                        ? exception.Message
                        : "błąd usługi TIDAL";
                    failed.Add($"{playlist.Title}: {reason}");
                }
            }
            var message = completed.Count > 0
                ? $"Dodano {tracks.Count} utworów do {completed.Count} playlist TIDAL."
                : "Nie dodano utworów do playlist TIDAL.";
            if (failed.Count > 0)
                message += $" Niepowodzenie: {string.Join("; ", failed)}";
            return new
            {
                added = completed.Count > 0 ? tracks.Count : 0,
                playlists = completed.Count,
                succeededPlaylistExternalIds = completedIds,
                message
            };
        }
    }

    private TidalIntegrationService RequireIntegration()
    {
        lock (_gate)
        {
            if (_integration is not null) return _integration;
            var settings = ReadSettings();
            _settings = settings;
            var integration = new TidalIntegrationService(
                settings, credentialTarget: CredentialTarget(profileId));
            var cached = settings.CachedCollectionItems
                .Select(item => item.ToMediaItem())
                .ToArray();
            integration.RestoreCachedCollection(cached);
            _collectionSnapshot = cached;
            _integration = integration;
            return integration;
        }
    }

    private TidalSettings ReadSettings()
    {
        try
        {
            using var stream = new FileStream(
                statePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using var document = JsonDocument.Parse(stream);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("tidal", out var tidal)
                || tidal.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException(
                    "Profil AMC nie zawiera ustawień TIDAL.");
            }
            return JsonSerializer.Deserialize<TidalSettings>(tidal.GetRawText(), JsonOptions)
                ?? throw new InvalidOperationException(
                    "Nie można odczytać ustawień TIDAL z profilu AMC.");
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (FileNotFoundException exception)
        {
            throw new InvalidOperationException(
                "Nie znaleziono profilu AMC potrzebnego do katalogu TIDAL.",
                exception);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                "Profil AMC z ustawieniami TIDAL jest uszkodzony.",
                exception);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException(
                "Nie można teraz odczytać ustawień TIDAL z profilu AMC.",
                exception);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _integration?.Dispose();
            _integration = null;
        }
    }
}
