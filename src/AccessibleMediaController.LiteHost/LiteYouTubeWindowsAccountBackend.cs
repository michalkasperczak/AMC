using System.Diagnostics;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.YouTube;
using AccessibleMediaController.Windows.Services;

namespace AccessibleMediaController.LiteHost;

/// <summary>
/// Produkcyjny właściciel tokenu i świeżego podglądu. Zapis jest możliwy tylko
/// po podglądzie nie starszym niż dziesięć minut i dla tego samego wyboru
/// jawnego przywracania usuniętych kanałów lub playlist.
/// </summary>
internal sealed class LiteYouTubeWindowsAccountBackend : ILiteYouTubeAccountBackend
{
    private static readonly TimeSpan PreviewLifetime = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan BroadcastCacheLifetime = TimeSpan.FromMinutes(5);
    private readonly YouTubeAccountCoordinator _account;
    private readonly YouTubeSubscriptionClient _subscriptions = new();
    private readonly YouTubePlaylistClient _playlists = new();
    private readonly YouTubeBroadcastClient _broadcasts = new();
    private readonly PodcastLibraryMutationStore _podcasts;
    private readonly bool _publicDataPolicy;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private PreviewSnapshot? _preview;
    private PlaylistPreviewSnapshot? _playlistPreview;
    private readonly Dictionary<string, BroadcastSnapshot> _broadcastCache =
        new(StringComparer.Ordinal);
    private YouTubeAccountDataDeletionResult _expiredDataDeletion = new(0, 0);
    private bool _accountDataMaintenanceFailed;
    private bool _disposed;

    internal LiteYouTubeWindowsAccountBackend(
        string clientId,
        string clientSecret,
        string releaseProfile,
        string podcastsDatabasePath)
    {
        _podcasts = new PodcastLibraryMutationStore(
            Path.GetFullPath(podcastsDatabasePath));
        _account = new YouTubeAccountCoordinator(
            clientId,
            clientSecret,
            YouTubeCredentialStore.ForReleaseProfile(releaseProfile),
            new YouTubeOAuthTokenClient(),
            OpenAuthorizationPageAsync);
        _publicDataPolicy = false;
    }

    internal LiteYouTubeWindowsAccountBackend(
        string clientId,
        Uri tokenBrokerEndpoint,
        string releaseProfile,
        string podcastsDatabasePath)
    {
        _podcasts = new PodcastLibraryMutationStore(
            Path.GetFullPath(podcastsDatabasePath));
        _account = new YouTubeAccountCoordinator(
            clientId,
            tokenBrokerEndpoint,
            YouTubeCredentialStore.ForReleaseProfile(releaseProfile),
            new YouTubeOAuthTokenClient(),
            OpenAuthorizationPageAsync);
        _publicDataPolicy = string.Equals(
            releaseProfile,
            "public",
            StringComparison.OrdinalIgnoreCase);
        if (_publicDataPolicy)
        {
            try
            {
                _expiredDataDeletion = _podcasts.DeleteExpiredYouTubeAccountData(
                    DateTime.UtcNow,
                    TimeSpan.FromDays(30));
            }
            catch (Exception exception) when (exception is IOException
                or UnauthorizedAccessException
                or InvalidDataException
                or Microsoft.Data.Sqlite.SqliteException)
            {
                _accountDataMaintenanceFailed = true;
            }
        }
    }

    public YouTubeAccountStatus GetStatus()
    {
        if (_accountDataMaintenanceFailed)
            return new YouTubeAccountStatus(
                false,
                "Nie można bezpiecznie sprawdzić i usunąć nieaktualnych danych konta YouTube.",
                null);
        var status = _account.GetStatus();
        if (!_expiredDataDeletion.Changed) return status;
        return status with
        {
            Message = status.Message
                + $" Usunięto nieaktualne dane konta YouTube: źródła {_expiredDataDeletion.RemovedSources}, materiały {_expiredDataDeletion.RemovedEpisodes}."
        };
    }

    public async Task<YouTubeAccountOperationResult> LoginAsync(
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureAccountDataMaintenance();
            var result = await _account.LoginAsync(cancellationToken).ConfigureAwait(false);
            if (result.Succeeded)
            {
                _preview = null;
                _playlistPreview = null;
                _broadcastCache.Clear();
            }
            return result;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<YouTubeAccountOperationResult> LogoutAsync(
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureAccountDataMaintenance();
            YouTubeAccountDataDeletionResult removed = new(0, 0);
            var result = _publicDataPolicy
                ? await _account.RevokeAndLogoutAsync(
                    token =>
                    {
                        token.ThrowIfCancellationRequested();
                        removed = _podcasts.DeleteAllYouTubeAccountData(DateTime.UtcNow);
                        return Task.CompletedTask;
                    },
                    cancellationToken).ConfigureAwait(false)
                : await _account.LogoutAsync(cancellationToken).ConfigureAwait(false);
            _preview = null;
            _playlistPreview = null;
            _broadcastCache.Clear();
            return _publicDataPolicy && result.Succeeded
                ? result with
                {
                    Message = result.Message
                        + $" Usunięte źródła: {removed.RemovedSources}; materiały: {removed.RemovedEpisodes}."
                }
                : result;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<YouTubeSubscriptionSyncPlan> PreviewAsync(
        bool restoreSuppressed,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureAccountDataMaintenance();
            var token = await _account.GetAccessTokenAsync(cancellationToken)
                .ConfigureAwait(false);
            var remote = await _subscriptions.ReadAllAsync(token, cancellationToken)
                .ConfigureAwait(false);
            var plan = _podcasts.PreviewYouTubeSubscriptions(remote, restoreSuppressed);
            _preview = new PreviewSnapshot(
                remote.ToArray(),
                restoreSuppressed,
                DateTimeOffset.UtcNow);
            return plan;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<YouTubeSubscriptionSyncApplyResult> ApplyAsync(
        bool restoreSuppressed,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureAccountDataMaintenance();
            if (_preview is not { } preview
                || preview.RestoreSuppressed != restoreSuppressed
                || DateTimeOffset.UtcNow - preview.CreatedAtUtc > PreviewLifetime)
            {
                throw new InvalidOperationException(
                    "Najpierw sprawdź aktualne subskrypcje YouTube i zatwierdź ten sam zakres zmian.");
            }
            var result = _podcasts.ApplyYouTubeSubscriptions(
                preview.Subscriptions,
                restoreSuppressed);
            _preview = null;
            return result;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<YouTubePlaylistSyncPlan> PreviewPlaylistsAsync(
        bool restoreSuppressed,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureAccountDataMaintenance();
            var token = await _account.GetAccessTokenAsync(cancellationToken)
                .ConfigureAwait(false);
            var remote = await _playlists.ReadAllAsync(token, cancellationToken)
                .ConfigureAwait(false);
            var plan = _podcasts.PreviewYouTubePlaylists(remote, restoreSuppressed);
            _playlistPreview = new PlaylistPreviewSnapshot(
                remote.ToArray(),
                restoreSuppressed,
                DateTimeOffset.UtcNow);
            return plan;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<YouTubePlaylistSyncApplyResult> ApplyPlaylistsAsync(
        bool restoreSuppressed,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureAccountDataMaintenance();
            if (_playlistPreview is not { } preview
                || preview.RestoreSuppressed != restoreSuppressed
                || DateTimeOffset.UtcNow - preview.CreatedAtUtc > PreviewLifetime)
            {
                throw new InvalidOperationException(
                    "Najpierw sprawdź aktualne playlisty YouTube i zatwierdź ten sam zakres zmian.");
            }
            var result = _podcasts.ApplyYouTubePlaylists(
                preview.Playlists,
                restoreSuppressed);
            _playlistPreview = null;
            return result;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<YouTubeBroadcast>> ReadBroadcastsAsync(
        string subscriptionId,
        bool forceRefresh,
        CancellationToken cancellationToken)
    {
        subscriptionId = subscriptionId?.Trim() ?? string.Empty;
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureAccountDataMaintenance();
            if (!forceRefresh
                && _broadcastCache.TryGetValue(subscriptionId, out var cached)
                && DateTimeOffset.UtcNow - cached.CreatedAtUtc <= BroadcastCacheLifetime)
                return cached.Rows;

            var target = _podcasts.GetRefreshTargets(subscriptionId).Single();
            if (target.SourceKind != PodcastSourceKind.YouTubeChannel)
                throw new InvalidOperationException("To źródło nie jest kanałem YouTube.");
            if (!TryReadChannelId(target.FeedUrl, target.SubscriptionId, out var channelId))
                throw new InvalidDataException(
                    "Ten kanał YouTube nie ma stabilnego identyfikatora potrzebnego do odczytu transmisji.");

            var token = await _account.GetAccessTokenAsync(cancellationToken)
                .ConfigureAwait(false);
            var rows = await _broadcasts.ReadChannelAsync(token, channelId, cancellationToken)
                .ConfigureAwait(false);
            var snapshot = new BroadcastSnapshot(rows.ToArray(), DateTimeOffset.UtcNow);
            _broadcastCache[subscriptionId] = snapshot;
            return snapshot.Rows;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<YouTubeBroadcast>> ReadChannelBroadcastsAsync(
        string channelId, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var token = await _account.GetAccessTokenAsync(cancellationToken)
                .ConfigureAwait(false);
            return await _broadcasts.ReadChannelAsync(token, channelId, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private static bool TryReadChannelId(
        string source,
        string subscriptionId,
        out string channelId)
    {
        channelId = string.Empty;
        string? candidate = null;
        if (Uri.TryCreate(source, UriKind.Absolute, out var uri)
            && (uri.Host.Equals("youtube.com", StringComparison.OrdinalIgnoreCase)
                || uri.Host.EndsWith(".youtube.com", StringComparison.OrdinalIgnoreCase)))
        {
            var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length >= 2
                && segments[0].Equals("channel", StringComparison.OrdinalIgnoreCase))
                candidate = Uri.UnescapeDataString(segments[1]).Trim();
        }
        const string subscriptionPrefix = "youtube-channel:";
        if (candidate is null
            && subscriptionId.StartsWith(subscriptionPrefix, StringComparison.Ordinal))
        {
            var storedIdentity = subscriptionId[subscriptionPrefix.Length..].Trim();
            // Prawdziwe identyfikatory kanałów YouTube zaczynają się od UC.
            // Odrzuca to lokalny skrót-hash używany tylko awaryjnie przez
            // publiczny parser kolekcji, który nie jest identyfikatorem API.
            if (storedIdentity.StartsWith("UC", StringComparison.Ordinal))
                candidate = storedIdentity;
        }
        if (candidate is null
            || candidate.Length is < 2 or > 256
            || candidate.Any(character => !char.IsAsciiLetterOrDigit(character)
                                          && character is not '-' and not '_'))
            return false;
        channelId = candidate;
        return true;
    }

    private void EnsureAccountDataMaintenance()
    {
        if (_accountDataMaintenanceFailed)
            throw new InvalidOperationException(
                "Nie można bezpiecznie sprawdzić i usunąć nieaktualnych danych konta YouTube.");
    }

    private static Task<bool> OpenAuthorizationPageAsync(
        Uri address,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            return Task.FromResult(Process.Start(new ProcessStartInfo
            {
                FileName = address.AbsoluteUri,
                UseShellExecute = true
            }) is not null);
        }
        catch (Exception exception) when (exception is InvalidOperationException
            or System.ComponentModel.Win32Exception)
        {
            return Task.FromResult(false);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _preview = null;
        _playlistPreview = null;
        _broadcastCache.Clear();
        _gate.Dispose();
        _broadcasts.Dispose();
        _playlists.Dispose();
        _subscriptions.Dispose();
        _account.Dispose();
    }

    private sealed record PreviewSnapshot(
        IReadOnlyList<YouTubeSubscription> Subscriptions,
        bool RestoreSuppressed,
        DateTimeOffset CreatedAtUtc);

    private sealed record PlaylistPreviewSnapshot(
        IReadOnlyList<YouTubeAccountPlaylist> Playlists,
        bool RestoreSuppressed,
        DateTimeOffset CreatedAtUtc);

    private sealed record BroadcastSnapshot(
        IReadOnlyList<YouTubeBroadcast> Rows,
        DateTimeOffset CreatedAtUtc);
}
