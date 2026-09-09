using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Core.Tidal;

namespace AccessibleMediaController.Windows.Services;

internal sealed record TidalSynchronizationResult(
    IReadOnlyList<MediaItem> Items,
    string AccountDisplayName,
    IReadOnlyList<string> Warnings,
    bool IsComplete);

internal sealed record TidalMembershipChangeResult(
    IReadOnlyList<MediaItem> Items,
    bool Added,
    int ChangedCount,
    IReadOnlyList<string> Warnings);

internal sealed record TidalPlaybackCredentials(
    string ClientId,
    string AccessToken,
    DateTimeOffset ExpiresAtUtc,
    IReadOnlyList<string> Scopes,
    string UserId);

internal sealed class TidalIntegrationService(TidalSettings settings) : IDisposable
{
    private readonly TidalOAuthClient oauth = new();
    private readonly TidalApiClient api = new();
    private readonly SemaphoreSlim operationGate = new(1, 1);
    private readonly Dictionary<MediaItemKind, IReadOnlyList<MediaItem>> synchronizedCollections = [];
    private HashSet<string> collectionExternalIds = new(StringComparer.Ordinal);
    private string? accountUserId;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(settings.ClientId);

    public bool HasStoredLogin
    {
        get
        {
            try { return TidalCredentialStore.TryRead(out _); }
            catch (Exception exception)
            {
                DiagnosticLog.Warning("tidal-auth", $"Nie można odczytać bezpiecznie zapisanego logowania: {exception.GetType().Name}.");
                return false;
            }
        }
    }

    public void RestoreCachedCollection(IReadOnlyList<MediaItem> items)
    {
        synchronizedCollections.Clear();
        foreach (var kindGroup in items
                     .Where(item => TidalCollectionSemantics.IsCollectionKind(item.Kind))
                     .GroupBy(item => item.Kind))
        {
            synchronizedCollections[kindGroup.Key] = kindGroup
                .DistinctBy(item => item.ExternalId ?? item.Id, StringComparer.Ordinal)
                .ToArray();
        }
        collectionExternalIds = items
            .Select(item => item.ExternalId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .ToHashSet(StringComparer.Ordinal);
    }

    public async Task LoginAsync(CancellationToken cancellationToken)
    {
        await operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var tokens = await oauth.AuthorizeAsync(settings, cancellationToken).ConfigureAwait(false);
            TidalCredentialStore.Write(tokens);
            DiagnosticLog.Info("tidal-auth", "Zalogowano konto TIDAL; token zapisano w Menedżerze poświadczeń Windows.");
        }
        finally
        {
            operationGate.Release();
        }
    }

    public async Task<TidalSynchronizationResult> SynchronizeAsync(CancellationToken cancellationToken)
    {
        await operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var tokens = await EnsureValidTokensAsync(cancellationToken).ConfigureAwait(false);
            return await SynchronizeCoreAsync(tokens, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            operationGate.Release();
        }
    }

    public async Task<TidalPlaybackCredentials> GetPlaybackCredentialsAsync(
        CancellationToken cancellationToken)
    {
        await operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var tokens = await EnsureValidTokensAsync(cancellationToken).ConfigureAwait(false);
            var userId = accountUserId;
            if (string.IsNullOrWhiteSpace(userId))
            {
                var account = await api.GetAccountIdentityAsync(tokens.AccessToken, cancellationToken)
                    .ConfigureAwait(false);
                userId = account.UserId;
                accountUserId = userId;
            }
            return new TidalPlaybackCredentials(
                tokens.ClientId,
                tokens.AccessToken,
                tokens.ExpiresAtUtc,
                string.IsNullOrWhiteSpace(tokens.Scope)
                    ? Array.Empty<string>()
                    : tokens.Scope.Split(
                        ' ',
                        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                userId);
        }
        finally
        {
            operationGate.Release();
        }
    }

    public async Task<IReadOnlyList<MediaItem>> SearchAsync(
        string query,
        CancellationToken cancellationToken)
    {
        await operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var tokens = await EnsureValidTokensAsync(cancellationToken).ConfigureAwait(false);
            return await api.SearchAsync(
                tokens.AccessToken,
                settings.CountryCode,
                query,
                collectionExternalIds,
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            operationGate.Release();
        }
    }

    public async Task<IReadOnlyList<MediaItem>> GetContainerItemsAsync(
        MediaItem container,
        CancellationToken cancellationToken)
    {
        await operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var tokens = await EnsureValidTokensAsync(cancellationToken).ConfigureAwait(false);
            return await api.GetContainerItemsAsync(
                tokens.AccessToken,
                settings.CountryCode,
                container,
                collectionExternalIds,
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            operationGate.Release();
        }
    }

    public async Task<IReadOnlyList<MediaItem>> ReorderPlaylistItemsAsync(
        MediaItem playlist,
        IReadOnlyList<MediaItem> items,
        string positionBeforeEntryId,
        CancellationToken cancellationToken)
    {
        await operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var tokens = await EnsureValidTokensAsync(cancellationToken).ConfigureAwait(false);
            if (!HasScope(tokens.Scope, "playlists.write"))
            {
                throw new InvalidOperationException(
                    "Zmiana kolejności playlisty TIDAL wymaga jednorazowego ponownego zalogowania po aktualizacji AMC. Otwórz Ctrl+F5 i wybierz Zaloguj w przeglądarce.");
            }
            try
            {
                await api.ReorderPlaylistItemsAsync(
                    tokens.AccessToken,
                    playlist,
                    items,
                    positionBeforeEntryId,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (TidalApiException exception) when (exception.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                throw new InvalidOperationException(
                    "TIDAL nie pozwala zmienić kolejności tej playlisty. Można edytować tylko playlisty, do których konto ma prawo zapisu.",
                    exception);
            }
            return await api.GetContainerItemsAsync(
                tokens.AccessToken,
                settings.CountryCode,
                playlist,
                collectionExternalIds,
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            operationGate.Release();
        }
    }

    public async Task<IReadOnlyList<MediaItem>> AddPlaylistItemsAsync(
        MediaItem playlist,
        IReadOnlyList<MediaItem> items,
        CancellationToken cancellationToken)
    {
        await operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var tokens = await EnsureValidTokensAsync(cancellationToken).ConfigureAwait(false);
            EnsurePlaylistWriteScope(tokens.Scope);
            try
            {
                await api.AddPlaylistItemsAsync(
                    tokens.AccessToken,
                    playlist,
                    items,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (TidalApiException exception) when (exception.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                throw PlaylistWriteDenied(exception);
            }
            return await api.GetContainerItemsAsync(
                tokens.AccessToken,
                settings.CountryCode,
                playlist,
                collectionExternalIds,
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            operationGate.Release();
        }
    }

    public async Task<MediaItem> CreatePlaylistAsync(
        string name,
        CancellationToken cancellationToken)
    {
        await operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var tokens = await EnsureValidTokensAsync(cancellationToken).ConfigureAwait(false);
            EnsurePlaylistWriteScope(tokens.Scope);
            MediaItem playlist;
            try
            {
                playlist = await api.CreatePlaylistAsync(
                    tokens.AccessToken,
                    name,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (TidalApiException exception) when (exception.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                throw PlaylistWriteDenied(exception);
            }

            var existing = synchronizedCollections.GetValueOrDefault(MediaItemKind.Playlist)
                ?? Array.Empty<MediaItem>();
            synchronizedCollections[MediaItemKind.Playlist] = existing
                .Append(playlist)
                .DistinctBy(item => item.ExternalId ?? item.Id, StringComparer.Ordinal)
                .ToArray();
            if (!string.IsNullOrWhiteSpace(playlist.ExternalId))
                collectionExternalIds.Add(playlist.ExternalId);
            return playlist;
        }
        finally
        {
            operationGate.Release();
        }
    }

    public async Task<IReadOnlyList<MediaItem>> RemovePlaylistItemsAsync(
        MediaItem playlist,
        IReadOnlyList<MediaItem> items,
        CancellationToken cancellationToken)
    {
        await operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var tokens = await EnsureValidTokensAsync(cancellationToken).ConfigureAwait(false);
            EnsurePlaylistWriteScope(tokens.Scope);
            try
            {
                await api.RemovePlaylistItemsAsync(
                    tokens.AccessToken,
                    playlist,
                    items,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (TidalApiException exception) when (exception.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                throw PlaylistWriteDenied(exception);
            }
            return await api.GetContainerItemsAsync(
                tokens.AccessToken,
                settings.CountryCode,
                playlist,
                collectionExternalIds,
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            operationGate.Release();
        }
    }

    public async Task<TidalMembershipChangeResult> ChangeCollectionMembershipAsync(
        IReadOnlyList<MediaItem> items,
        bool add,
        CancellationToken cancellationToken)
    {
        await operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var tokens = await EnsureValidTokensAsync(cancellationToken).ConfigureAwait(false);
            if (!HasScope(tokens.Scope, "collection.write"))
            {
                throw new InvalidOperationException(
                    "Zapis do kolekcji TIDAL wymaga ponownego zalogowania po aktualizacji AMC. Otwórz Ctrl+F5 i wybierz Zaloguj w przeglądarce.");
            }
            var uniqueItems = items
                .Where(item => item.ExternalId is { Length: > 0 })
                .DistinctBy(item => item.Id, StringComparer.Ordinal)
                .ToArray();
            if (uniqueItems.Length == 0)
                throw new InvalidOperationException("Brak elementów TIDAL możliwych do zapisania.");
            await api.ChangeCollectionMembershipAsync(
                tokens.AccessToken,
                uniqueItems,
                add,
                cancellationToken).ConfigureAwait(false);
            var mergedItems = ApplyConfirmedMembershipChange(uniqueItems, add);
            DiagnosticLog.Info(
                "tidal-collection",
                $"TIDAL potwierdził {(add ? "dodanie" : "usunięcie")} {uniqueItems.Length} elementów; widok zaktualizowano bez pełnej ponownej synchronizacji.");
            return new TidalMembershipChangeResult(
                mergedItems,
                add,
                uniqueItems.Length,
                []);
        }
        finally
        {
            operationGate.Release();
        }
    }

    private IReadOnlyList<MediaItem> ApplyConfirmedMembershipChange(
        IReadOnlyList<MediaItem> items,
        bool add)
    {
        foreach (var kindGroup in items.GroupBy(item => item.Kind))
        {
            // Search results are transient objects and are not necessarily
            // present in the synchronized session cache. Keep the exact
            // objects on which the user acted authoritative as soon as TIDAL
            // confirms the mutation, so a repeated shortcut toggles the
            // state instead of repeating the previous request.
            foreach (var item in kindGroup)
            {
                TidalCollectionSemantics.ApplyMembership(item, add);
            }
            var changedIds = kindGroup
                .Select(item => item.ExternalId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id!)
                .ToHashSet(StringComparer.Ordinal);
            var existing = synchronizedCollections.GetValueOrDefault(kindGroup.Key, []);
            if (!add)
            {
                synchronizedCollections[kindGroup.Key] = existing
                    .Where(item => item.ExternalId is null || !changedIds.Contains(item.ExternalId))
                    .ToArray();
                continue;
            }

            var merged = existing.ToList();
            var existingIds = merged
                .Select(item => item.ExternalId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id!)
                .ToHashSet(StringComparer.Ordinal);
            foreach (var item in kindGroup)
            {
                if (item.ExternalId is not { Length: > 0 } externalId || !existingIds.Add(externalId)) continue;
                merged.Add(CloneCollectionItem(item));
            }
            synchronizedCollections[kindGroup.Key] = merged;
        }
        return MergeSynchronizedCollections();
    }

    private IReadOnlyList<MediaItem> MergeSynchronizedCollections()
    {
        var mergedItems = synchronizedCollections.Values
            .SelectMany(items => items)
            .DistinctBy(item => item.ExternalId ?? item.Id, StringComparer.Ordinal)
            .OrderBy(item => item.CollectionAddedUtcTicks ?? long.MinValue)
            .ToArray();
        collectionExternalIds = mergedItems
            .Select(item => item.ExternalId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .ToHashSet(StringComparer.Ordinal);
        return mergedItems;
    }

    private static MediaItem CloneCollectionItem(MediaItem item) => new()
    {
        Id = item.ExternalId is { Length: > 0 } externalId ? $"tidal:{externalId}" : item.Id,
        ExternalId = item.ExternalId,
        RelatedAlbumExternalId = item.RelatedAlbumExternalId,
        RelatedAlbumTitle = item.RelatedAlbumTitle,
        RelatedArtistExternalId = item.RelatedArtistExternalId,
        RelatedArtistName = item.RelatedArtistName,
        Title = item.Title,
        Artist = item.Artist,
        Kind = item.Kind,
        Duration = item.Duration,
        Source = item.Source,
        PublicUri = item.PublicUri,
        CollectionAddedUtcTicks = item.CollectionAddedUtcTicks ?? DateTime.UtcNow.Ticks,
        IsFavorite = TidalCollectionSemantics.UsesFavorites(item.Kind),
        IsInLibrary = TidalCollectionSemantics.UsesLibrary(item.Kind),
        IsAvailable = item.IsAvailable,
        IsInQueue = item.IsInQueue,
        IsPlayNext = item.IsPlayNext
    };

    public void Disconnect()
    {
        TidalCredentialStore.Delete();
        settings.AccountDisplayName = string.Empty;
        settings.LastSuccessfulSyncUtcTicks = 0;
        settings.CachedCollectionItems.Clear();
        synchronizedCollections.Clear();
        collectionExternalIds.Clear();
        accountUserId = null;
        DiagnosticLog.Info("tidal-auth", "Usunięto logowanie TIDAL z Menedżera poświadczeń Windows.");
    }

    private static void EnsurePlaylistWriteScope(string scope)
    {
        if (!HasScope(scope, "playlists.write"))
        {
            throw new InvalidOperationException(
                "Edycja playlist TIDAL wymaga jednorazowego ponownego zalogowania po aktualizacji AMC. Otwórz Ctrl+F5 i wybierz Zaloguj w przeglądarce.");
        }
    }

    private static InvalidOperationException PlaylistWriteDenied(Exception innerException) =>
        new(
            "TIDAL nie pozwala edytować tej playlisty. Można zmieniać tylko playlisty, do których konto ma prawo zapisu.",
            innerException);

    private async Task<TidalTokenSet> EnsureValidTokensAsync(CancellationToken cancellationToken)
    {
        if (!IsConfigured)
            throw new InvalidOperationException("Najpierw wpisz identyfikator aplikacji TIDAL.");
        if (!TidalCredentialStore.TryRead(out var tokens) || tokens is null)
            throw new InvalidOperationException("Najpierw zaloguj się do TIDAL.");
        if (!string.Equals(tokens.ClientId, settings.ClientId, StringComparison.Ordinal))
            throw new InvalidOperationException("Identyfikator aplikacji TIDAL został zmieniony. Zaloguj się ponownie.");
        if (tokens.ExpiresAtUtc > DateTimeOffset.UtcNow.AddMinutes(1)) return tokens;
        var refreshed = await oauth.RefreshAsync(settings, tokens, cancellationToken).ConfigureAwait(false);
        TidalCredentialStore.Write(refreshed);
        DiagnosticLog.Info("tidal-auth", "Odświeżono logowanie TIDAL.");
        return refreshed;
    }

    private async Task<TidalSynchronizationResult> SynchronizeCoreAsync(
        TidalTokenSet tokens,
        CancellationToken cancellationToken)
    {
        TidalAccountIdentity? accountIdentity = null;
        try
        {
            accountIdentity = await api.GetAccountIdentityAsync(tokens.AccessToken, cancellationToken).ConfigureAwait(false);
            accountUserId = accountIdentity.UserId;
        }
        catch (TidalApiException exception) when (!exception.IsAuthorizationFailure)
        {
            DiagnosticLog.Warning("tidal-sync", $"Nie odczytano nazwy konta: HTTP {(int)exception.StatusCode}.");
        }
        var synchronized = await api.SynchronizeCollectionAsync(
            tokens.AccessToken,
            cancellationToken).ConfigureAwait(false);
        if (synchronized.UpdatedKinds.Count == 0)
        {
            var details = synchronized.Warnings.Count == 0
                ? string.Empty
                : $" {string.Join("; ", synchronized.Warnings)}.";
            throw new InvalidOperationException(
                $"Nie udało się odświeżyć żadnej kolekcji TIDAL. Poprzednie dane pozostają bez zmian.{details}");
        }
        foreach (var kind in synchronized.UpdatedKinds)
        {
            synchronizedCollections[kind] = synchronized.Items
                .Where(item => item.Kind == kind)
                .ToArray();
        }
        var mergedItems = MergeSynchronizedCollections();
        var accountName = accountIdentity?.DisplayName;
        var displayName = string.IsNullOrWhiteSpace(accountName)
            ? settings.AccountDisplayName
            : accountName.Trim();
        settings.AccountDisplayName = displayName;
        settings.LastSuccessfulSyncUtcTicks = DateTime.UtcNow.Ticks;
        DiagnosticLog.Info(
            "tidal-sync",
            $"Zsynchronizowano {mergedItems.Count} elementów kolekcji; ostrzeżenia: {synchronized.Warnings.Count}.");
        return new TidalSynchronizationResult(
            mergedItems,
            displayName,
            synchronized.Warnings,
            synchronized.UpdatedKinds.Count == 5);
    }

    private static bool HasScope(string scopes, string requiredScope) =>
        scopes.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Contains(requiredScope, StringComparer.Ordinal);

    public void Dispose()
    {
        // A cancelled request may still execute its finally block and release
        // this gate while the main window is closing. Keep the tiny managed
        // semaphore alive until process exit instead of introducing a race.
        oauth.Dispose();
        api.Dispose();
    }
}
