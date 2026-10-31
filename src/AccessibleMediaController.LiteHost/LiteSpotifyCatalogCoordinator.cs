using System.Text.Json;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Podcasts;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Core.Spotify;
using AccessibleMediaController.LiteHost.Protocol;

namespace AccessibleMediaController.LiteHost;

/// <summary>
/// Spotify catalogue surface exposed by LiteHost. Authentication starts only
/// through the explicit login operation; ordinary reads and writes reuse the
/// account already stored in Windows Credential Manager.
/// </summary>
internal sealed class LiteSpotifyCatalogCoordinator(
    ILiteSpotifyReadOnlyBackend backend) : IDisposable
{
    private readonly object _collectionGate = new();
    private IReadOnlyList<MediaItem> _collectionSnapshot = backend.CachedCollection;
    private bool _hasCollectionCache = backend.CachedCollection.Count > 0;
    private bool _collectionRefreshAttempted;
    private bool _albumRefreshAttempted;
    private bool _podcastRefreshAttempted;
    private bool _playlistRefreshAttempted;

    public object Capabilities() => LiteSpotifyCatalogContract.CreateCapabilities(
        backend is ILiteSpotifyWriteBackend,
        backend is ILiteSpotifyAccountBackend);

    public object AccountStatus()
    {
        try
        {
            return LiteSpotifyCatalogContract.CreateAccountStatus(backend.ReadAccountStatus());
        }
        catch
        {
            // Credential Manager and account errors must never escape through
            // exception.Message. At this boundary even the exception type is
            // deliberately discarded.
            return LiteSpotifyCatalogContract.CreateAccountStatus(
                new LiteSpotifyAccountProbe(
                    CatalogConfigured: true,
                    CatalogConnected: null,
                    AccountDisplayName: string.Empty,
                    AccountProduct: string.Empty,
                    PlaybackConnected: null));
        }
    }

    public object Login()
    {
        if (backend is not ILiteSpotifyAccountBackend accountBackend)
            throw new LiteRequestException("Logowanie Spotify nie jest dostępne w tym profilu AMC.");
        try
        {
            var result = accountBackend.LoginAsync(CancellationToken.None)
                .GetAwaiter()
                .GetResult();
            lock (_collectionGate)
            {
                // OAuth może połączyć inne konto albo rozszerzyć zakresy
                // bieżącego. Żadna migawka sprzed logowania nie jest wtedy
                // wiarygodna. Następny widok wykona dokładnie jeden świeży
                // odczyt właściwej kategorii.
                _collectionSnapshot = [];
                _hasCollectionCache = false;
                _collectionRefreshAttempted = false;
                _albumRefreshAttempted = false;
                _podcastRefreshAttempted = false;
                _playlistRefreshAttempted = false;
            }
            return LiteSpotifyCatalogContract.CreateLoginResult(
                result.AccountDisplayName,
                result.AccountProduct);
        }
        catch (Exception exception)
        {
            throw SafeRequestException(exception, LiteSpotifyCatalogContract.LoginOperation);
        }
    }

    public object Configure(JsonElement args)
    {
        if (backend is not ILiteSpotifyAccountBackend accountBackend)
            throw new LiteRequestException("Ustawienia Spotify nie są dostępne w tym profilu AMC.");
        var clientId = LiteSpotifyCatalogContract.ReadClientId(args);
        try
        {
            accountBackend.ConfigureClientId(clientId);
            return new { message = "Zapisano identyfikator aplikacji Spotify. Możesz rozpocząć logowanie katalogu." };
        }
        catch
        {
            throw new LiteRequestException("Nie udało się zapisać ustawień konta Spotify.");
        }
    }

    public object StartPairing()
    {
        if (backend is not ILiteSpotifyAccountBackend accountBackend)
            throw new LiteRequestException("Parowanie Spotify nie jest dostępne w tym profilu AMC.");
        try
        {
            var result = accountBackend.StartPairingAsync(CancellationToken.None)
                .GetAwaiter().GetResult();
            return new LiteSpotifyPairingStartResult(
                result.UserCode,
                result.VerificationUri,
                "Wpisz kod na stronie Spotify, a potem wróć do AMC Test.");
        }
        catch (LiteRequestException)
        {
            throw;
        }
        catch
        {
            throw new LiteRequestException("Nie udało się rozpocząć parowania odtwarzacza Spotify.");
        }
    }

    public object PairingStatus()
    {
        if (backend is not ILiteSpotifyAccountBackend accountBackend)
            throw new LiteRequestException("Parowanie Spotify nie jest dostępne w tym profilu AMC.");
        return accountBackend.PairingStatus();
    }

    public object CancelPairing()
    {
        if (backend is not ILiteSpotifyAccountBackend accountBackend)
            throw new LiteRequestException("Parowanie Spotify nie jest dostępne w tym profilu AMC.");
        accountBackend.CancelPairing();
        return new LiteSpotifyPairingStatusResult("cancelled", "Anulowano parowanie odtwarzacza Spotify.");
    }

    public object CollectionRoot() => LiteSpotifyCatalogContract.CreateCollectionRoot();

    public object CollectionView(JsonElement args)
    {
        var view = LiteSpotifyCatalogContract.ReadCollectionView(args);
        var loadedCount = LiteSpotifyCatalogContract.ReadCollectionLoadedCount(args);
        var sortMode = LiteSpotifyCatalogContract.ReadCollectionSortMode(args);
        lock (_collectionGate)
        {
            var warning = string.Empty;
            LiteSpotifyAccountProbe account;
            try
            {
                account = backend.ReadAccountStatus();
            }
            catch
            {
                account = new LiteSpotifyAccountProbe(
                    true, null, string.Empty, string.Empty, null);
            }

            // The legacy profile already contains a durable collection cache.
            // Showing one category must not first synchronize every saved
            // track, album, artist, playlist and podcast: a real profile with
            // thousands of tracks needs over a minute for that operation and
            // consumes all concurrent host slots when Enter is repeated.
            // Use the populated snapshot immediately.  A network refresh is
            // retained only as the fallback for a profile with no cache.
            if (!_collectionRefreshAttempted
                && !_hasCollectionCache
                && account.CatalogConnected == true
                && view is not ("podcasts" or "episodes")
                && !(view == "playlists" && backend is ILiteSpotifyWriteBackend))
            {
                _collectionRefreshAttempted = true;
                try
                {
                    var refreshed = backend.RefreshCollectionAsync(CancellationToken.None)
                        .GetAwaiter()
                        .GetResult();
                    _collectionSnapshot = refreshed.Items;
                    _hasCollectionCache = true;
                    if (refreshed.HadWarnings)
                    {
                        warning = "Nie wszystkie części kolekcji Spotify udało się odświeżyć.";
                    }
                }
                catch (Exception exception)
                {
                    warning = SafeFailure(exception, LiteSpotifyCatalogContract.CollectionViewOperation)
                        + " Pokazano ostatni zapis.";
                }
            }
            else if (!account.CatalogConfigured)
            {
                warning = "Katalog Spotify nie jest skonfigurowany. Pokazano ostatni zapis.";
            }
            else if (account.CatalogConnected == false)
            {
                warning = "Katalog Spotify nie jest połączony. Pokazano ostatni zapis.";
            }
            else if (account.CatalogConnected is null)
            {
                warning = "Nie można sprawdzić połączenia katalogu Spotify. Pokazano ostatni zapis.";
            }

            // Podcasty i zapisane odcinki mają dwa małe, stronicowane
            // endpointy. Odświeżamy je razem przy pierwszym wejściu w jeden
            // z tych widoków, także wtedy, gdy cache innych kategorii nie jest
            // pusty. Wcześniej taki cache blokował odczyt podcastów do końca
            // procesu i wyglądał jak ograniczenie Librespota.
            if (view is "podcasts" or "episodes"
                && !_podcastRefreshAttempted
                && account.CatalogConnected == true)
            {
                _podcastRefreshAttempted = true;
                try
                {
                    var refreshed = backend.RefreshPodcastsAsync(CancellationToken.None)
                        .GetAwaiter()
                        .GetResult();
                    _collectionSnapshot = MergeRefreshedPodcasts(
                        _collectionSnapshot,
                        refreshed.Items,
                        keepMissing: refreshed.HadWarnings);
                    if (refreshed.HadWarnings)
                    {
                        warning = JoinWarnings(
                            warning,
                            "Nie wszystkie podcasty i zapisane odcinki Spotify udało się odświeżyć. "
                                + "Pokazano dostępne dane i ostatni zapis brakującej części.");
                    }
                }
                catch (Exception exception)
                {
                    warning = JoinWarnings(
                        warning,
                        SafeFailure(
                            exception,
                            LiteSpotifyCatalogContract.CollectionViewOperation)
                        + " Pokazano ostatni zapis podcastów i odcinków.");
                }
            }

            // Lista playlist ma własny, mały endpoint /me/playlists. Dzięki
            // temu Ctrl+P widzi także playlistę utworzoną wcześniej (na
            // przykład w poprzednim uruchomieniu), bez pełnej synchronizacji
            // tysięcy elementów kolekcji. Jedno uruchomienie odczytuje ją raz.
            if (view == "playlists"
                && !_playlistRefreshAttempted
                && account.CatalogConnected == true
                && backend is ILiteSpotifyWriteBackend playlistReader)
            {
                _playlistRefreshAttempted = true;
                try
                {
                    var refreshed = playlistReader
                        .GetPlaylistTargetsAsync(CancellationToken.None)
                        .GetAwaiter()
                        .GetResult();
                    _collectionSnapshot = MergeEditablePlaylists(
                        _collectionSnapshot,
                        refreshed);
                }
                catch (Exception exception)
                {
                    warning = JoinWarnings(
                        warning,
                        SafeFailure(
                            exception,
                            LiteSpotifyCatalogContract.PlaylistTargetsOperation)
                        + " Pokazano ostatni zapis playlist.");
                }
            }

            // Stary cache AMC nie przechowywał czasu albumu. Gdy użytkownik
            // rzeczywiście otwiera kategorię Albumy, pobieramy tylko strony
            // /me/albums. To kilka małych stron zamiast pełnej synchronizacji
            // tysięcy utworów i nie wykonuje się podczas chodzenia strzałkami.
            if (view == "albums"
                && !_albumRefreshAttempted
                && account.CatalogConnected == true
                && _collectionSnapshot.Any(item =>
                    item.Kind == MediaItemKind.Album
                    && item.IsInLibrary
                    && item.Duration <= TimeSpan.Zero))
            {
                _albumRefreshAttempted = true;
                try
                {
                    var refreshed = backend.RefreshAlbumsAsync(CancellationToken.None)
                        .GetAwaiter()
                        .GetResult();
                    _collectionSnapshot = MergeRefreshedAlbums(
                        _collectionSnapshot,
                        refreshed.Items,
                        keepMissing: refreshed.HadWarnings);
                    if (refreshed.HadWarnings)
                    {
                        warning = JoinWarnings(
                            warning,
                            "Nie wszystkie czasy albumów Spotify udało się odświeżyć.");
                    }
                }
                catch (Exception exception)
                {
                    warning = JoinWarnings(
                        warning,
                        SafeFailure(
                            exception,
                            LiteSpotifyCatalogContract.CollectionViewOperation)
                        + " Pokazano ostatni zapis albumów.");
                }
            }

            return LiteSpotifyCatalogContract.CreateCollectionViewResult(
                view,
                _collectionSnapshot,
                warning,
                loadedCount,
                sortMode);
        }
    }

    private static IReadOnlyList<MediaItem> MergeRefreshedAlbums(
        IReadOnlyList<MediaItem> snapshot,
        IReadOnlyList<MediaItem> refreshedAlbums,
        bool keepMissing)
    {
        if (!keepMissing)
        {
            return snapshot
                .Where(item => item.Kind != MediaItemKind.Album)
                .Concat(refreshedAlbums)
                .ToArray();
        }

        var byId = refreshedAlbums
            .Where(item => item.Kind == MediaItemKind.Album)
            .GroupBy(
                item => NormalizeExternalId(item.ExternalId),
                StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.First(),
                StringComparer.Ordinal);
        var merged = new List<MediaItem>(snapshot.Count + refreshedAlbums.Count);
        var used = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in snapshot)
        {
            var id = NormalizeExternalId(item.ExternalId);
            if (item.Kind == MediaItemKind.Album
                && byId.TryGetValue(id, out var replacement))
            {
                merged.Add(replacement);
                used.Add(id);
            }
            else
            {
                merged.Add(item);
            }
        }
        merged.AddRange(refreshedAlbums.Where(item =>
            item.Kind == MediaItemKind.Album
            && !used.Contains(NormalizeExternalId(item.ExternalId))));
        return merged;
    }

    private static IReadOnlyList<MediaItem> MergeRefreshedPodcasts(
        IReadOnlyList<MediaItem> snapshot,
        IReadOnlyList<MediaItem> refreshedPodcasts,
        bool keepMissing)
    {
        static bool IsPodcastItem(MediaItem item) =>
            item.Kind is MediaItemKind.Podcast or MediaItemKind.Episode;

        if (!keepMissing)
        {
            return snapshot
                .Where(item => !IsPodcastItem(item))
                .Concat(refreshedPodcasts.Where(IsPodcastItem))
                .ToArray();
        }

        var byIdentity = refreshedPodcasts
            .Where(IsPodcastItem)
            .GroupBy(
                item => $"{item.Kind}:{NormalizeExternalId(item.ExternalId)}",
                StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.First(),
                StringComparer.Ordinal);
        var merged = new List<MediaItem>(snapshot.Count + refreshedPodcasts.Count);
        var used = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in snapshot)
        {
            var identity = $"{item.Kind}:{NormalizeExternalId(item.ExternalId)}";
            if (IsPodcastItem(item)
                && byIdentity.TryGetValue(identity, out var replacement))
            {
                merged.Add(replacement);
                used.Add(identity);
            }
            else
            {
                merged.Add(item);
            }
        }
        merged.AddRange(refreshedPodcasts.Where(item =>
            IsPodcastItem(item)
            && !used.Contains(
                $"{item.Kind}:{NormalizeExternalId(item.ExternalId)}")));
        return merged;
    }

    private static IReadOnlyList<MediaItem> MergeEditablePlaylists(
        IReadOnlyList<MediaItem> snapshot,
        IReadOnlyList<SpotifyPlaylistTarget> refreshedPlaylists)
    {
        var refreshed = refreshedPlaylists
            .Where(target => !string.IsNullOrWhiteSpace(target.Id)
                && !string.IsNullOrWhiteSpace(target.Name))
            .GroupBy(target => target.Id.Trim(), StringComparer.Ordinal)
            .Select(group => group.First())
            .Select(target => new MediaItem
            {
                Id = $"spotify:playlist:{target.Id.Trim()}",
                ExternalId = target.Id.Trim(),
                Title = target.Name.Trim(),
                Kind = MediaItemKind.Playlist,
                IsInLibrary = true,
                IsAvailable = true
            })
            .ToArray();
        var refreshedIds = refreshed
            .Select(item => NormalizeExternalId(item.ExternalId))
            .Where(id => id.Length > 0)
            .ToHashSet(StringComparer.Ordinal);
        return refreshed
            .Concat(snapshot.Where(item =>
                item.Kind != MediaItemKind.Playlist
                || !refreshedIds.Contains(NormalizeExternalId(item.ExternalId))))
            .ToArray();
    }

    private static string JoinWarnings(string first, string second) =>
        string.Join(" ", new[] { first, second }.Where(value =>
            !string.IsNullOrWhiteSpace(value)));

    public object Search(JsonElement args)
    {
        var query = LiteSpotifyCatalogContract.ReadSearchQuery(args);
        EnsureCatalogReady();
        try
        {
            var items = backend.SearchAsync(query, CancellationToken.None)
                .GetAwaiter()
                .GetResult();
            return LiteSpotifyCatalogContract.CreateSearchResult(items);
        }
        catch (Exception exception)
        {
            throw SafeRequestException(exception, LiteSpotifyCatalogContract.SearchOperation);
        }
    }

    public object Description(JsonElement args)
    {
        var request = LiteSpotifyCatalogContract.ReadDescriptionRequest(args);
        EnsureCatalogReady();
        try
        {
            var description = backend.GetDescriptionAsync(
                    request,
                    CancellationToken.None)
                .GetAwaiter()
                .GetResult();
            return LiteSpotifyCatalogContract.CreateDescriptionResult(description);
        }
        catch (Exception exception)
        {
            throw SafeRequestException(
                exception,
                LiteSpotifyCatalogContract.DescriptionOperation);
        }
    }

    /// <summary>
    /// Spotify nie publikuje osobnego kontraktu rozdziałów podcastu. Jeżeli
    /// wydawca umieścił spis z czasami w opisie odcinka, wykorzystujemy ten
    /// sam ostrożny parser, co dla kanałów RSS.
    /// </summary>
    internal IReadOnlyList<ProviderChapterPoint> ReadEpisodeChapterPoints(
        MediaItem item,
        TimeSpan duration)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.Kind != MediaItemKind.Episode
            || string.IsNullOrWhiteSpace(item.ExternalId))
        {
            return [];
        }
        EnsureCatalogReady();
        try
        {
            var request = new LiteSpotifyDescriptionRequest(
                MediaItemKind.Episode,
                item.ExternalId.Trim());
            var description = backend.GetDescriptionAsync(
                    request,
                    CancellationToken.None)
                .GetAwaiter()
                .GetResult();
            return PodcastDescriptionChapterParser.Parse(
                LiteSpotifyCatalogContract.CleanDescription(description),
                duration);
        }
        catch (Exception exception)
        {
            throw SafeRequestException(
                exception,
                LiteSpotifyCatalogContract.DescriptionOperation);
        }
    }

    public object ContainerItems(JsonElement args)
    {
        var request = LiteSpotifyCatalogContract.ReadContainerRequest(args);
        EnsureCatalogReady();
        try
        {
            var result = backend.GetContainerItemsAsync(request, CancellationToken.None)
                .GetAwaiter()
                .GetResult();
            return LiteSpotifyCatalogContract.CreateContainerResult(
                request,
                result.Items,
                result.Available,
                result.HasMore);
        }
        catch (Exception exception)
        {
            throw SafeRequestException(
                exception,
                LiteSpotifyCatalogContract.ContainerItemsOperation);
        }
    }

    public object ChangeMembership(JsonElement args)
    {
        var request = LiteSpotifyCatalogContract.ReadMembershipRequest(args);
        EnsureCatalogReady();
        if (backend is not ILiteSpotifyWriteBackend writer)
        {
            throw new LiteRequestException(
                "Ten silnik Spotify nie udostępnia zmiany kolekcji.");
        }
        lock (_collectionGate)
        {
            try
            {
                var result = writer.ChangeMembershipAsync(
                        request.Items,
                        request.RequestedAddition,
                        CancellationToken.None)
                    .GetAwaiter()
                    .GetResult();
                foreach (var confirmed in result.ConfirmedItems)
                {
                    SpotifyCollectionSemantics.ApplyMembership(
                        confirmed, result.Added);
                    foreach (var cached in _collectionSnapshot.Where(item =>
                        item.Kind == confirmed.Kind
                        && string.Equals(
                            NormalizeExternalId(item.ExternalId),
                            NormalizeExternalId(confirmed.ExternalId),
                            StringComparison.Ordinal)))
                    {
                        SpotifyCollectionSemantics.ApplyMembership(
                            cached, result.Added);
                    }
                }
                var changed = result.ConfirmedItems.Count;
                var subject = changed == 1
                    ? result.ConfirmedItems[0].Title
                    : $"{changed} elementów";
                var destination = request.Mode == "favorite"
                    ? "Ulubionych"
                    : "Biblioteki";
                var message = result.Added
                    ? $"Dodano do {destination} Spotify: {subject}"
                    : $"Usunięto z {destination} Spotify: {subject}";
                if (result.FailedCount > 0)
                    message += $". Bez zmiany: {result.FailedCount} elementów";
                return new LiteSpotifyMembershipResult(
                    result.Added,
                    changed,
                    result.ConfirmedItems.Select(item => item.Id).ToArray(),
                    message);
            }
            catch (Exception exception)
            {
                throw SafeRequestException(
                    exception,
                    LiteSpotifyCatalogContract.MembershipOperation);
            }
        }
    }

    public object PlaylistTargets()
    {
        EnsureCatalogReady();
        if (backend is not ILiteSpotifyWriteBackend writer)
            throw new LiteRequestException("Ten silnik Spotify nie udostępnia zmiany playlist.");
        try
        {
            var targets = writer.GetPlaylistTargetsAsync(CancellationToken.None)
                .GetAwaiter().GetResult();
            lock (_collectionGate)
            {
                _collectionSnapshot = MergeEditablePlaylists(
                    _collectionSnapshot,
                    targets);
                _playlistRefreshAttempted = true;
            }
            return new LiteSpotifyPlaylistTargetsResult(
                targets.Select(LiteSpotifyCatalogContract.ToPlaylistTarget).ToArray(),
                targets.Count == 0
                    ? "Nie ma playlist Spotify dostępnych do edycji. Możesz utworzyć nową."
                    : $"Playlisty Spotify dostępne do edycji: {targets.Count}.");
        }
        catch (Exception exception)
        {
            throw SafeRequestException(exception, LiteSpotifyCatalogContract.PlaylistTargetsOperation);
        }
    }

    public object CreatePlaylist(JsonElement args)
    {
        var request = LiteSpotifyCatalogContract.ReadPlaylistCreateRequest(args);
        EnsureCatalogReady();
        if (backend is not ILiteSpotifyWriteBackend writer)
            throw new LiteRequestException("Ten silnik Spotify nie udostępnia tworzenia playlist.");
        try
        {
            var created = writer.CreatePlaylistAsync(
                    request.Name,
                    request.IsPublic,
                    CancellationToken.None)
                .GetAwaiter().GetResult();
            // Lista celów Ctrl+Shift+P ma własną migawkę interfejsu, natomiast
            // Ctrl+P czyta _collectionSnapshot. Po potwierdzonym utworzeniu
            // dopisujemy tę samą playlistę również do kolekcji bieżącego hosta.
            lock (_collectionGate)
            {
                _collectionSnapshot = MergeEditablePlaylists(
                    _collectionSnapshot,
                    [created]);
            }
            return new LiteSpotifyPlaylistCreateResult(
                LiteSpotifyCatalogContract.ToPlaylistTarget(created),
                $"Utworzono playlistę Spotify: {created.Name}, "
                    + (created.IsPublic ? "publiczna." : "prywatna."));
        }
        catch (Exception exception)
        {
            throw SafeRequestException(exception, LiteSpotifyCatalogContract.PlaylistCreateOperation);
        }
    }

    public object AddPlaylistItems(JsonElement args)
    {
        var request = LiteSpotifyCatalogContract.ReadPlaylistAddRequest(args);
        EnsureCatalogReady();
        if (backend is not ILiteSpotifyWriteBackend writer)
            throw new LiteRequestException("Ten silnik Spotify nie udostępnia zmiany playlist.");
        try
        {
            var result = writer.AddPlaylistItemsAsync(
                    request.PlaylistIds,
                    request.Items,
                    CancellationToken.None)
                .GetAwaiter().GetResult();
            var outcomes = result.Playlists.Select(item => new LiteSpotifyPlaylistOutcome(
                item.Target.Id,
                item.Target.AccessibleLabel,
                item.AddedCount,
                item.AlreadyPresentCount,
                item.FailedCount,
                item.SnapshotId,
                item.Message)).ToArray();
            var failureMessages = result.Playlists
                .Where(item => item.FailedCount > 0)
                .Select(item => item.Message)
                .Where(message => !string.IsNullOrWhiteSpace(message))
                .Distinct(StringComparer.CurrentCulture)
                .ToArray();
            var failureDetail = failureMessages.FirstOrDefault() ?? string.Empty;
            var message = result.IsPartial
                ? $"Częściowy wynik: dodano {result.AddedCount}, bez potwierdzenia {result.FailedCount}."
                    + (failureDetail.Length > 0 ? $" {failureDetail}" : string.Empty)
                : result.FailedCount > 0
                    ? (failureDetail.Length > 0
                        ? failureDetail
                        : $"Nie dodano pozycji. Bez potwierdzenia: {result.FailedCount}.")
                    : result.AddedCount > 0
                        ? $"Dodano {result.AddedCount} pozycji do wybranych playlist Spotify."
                        : "Wybrane playlisty już zawierają te pozycje.";
            return new LiteSpotifyPlaylistAddResult(
                result.RequestedItemCount,
                result.AddedCount,
                result.FailedCount,
                result.IsPartial,
                outcomes,
                message);
        }
        catch (Exception exception)
        {
            throw SafeRequestException(exception, LiteSpotifyCatalogContract.PlaylistAddItemsOperation);
        }
    }

    private void EnsureCatalogReady()
    {
        LiteSpotifyAccountProbe account;
        try
        {
            account = backend.ReadAccountStatus();
        }
        catch
        {
            throw new LiteRequestException(
                LiteSpotifyCatalogContract.FailureMessage(
                    LiteSpotifyFailureReason.Unknown,
                    LiteSpotifyCatalogContract.AccountStatusOperation));
        }
        if (!account.CatalogConfigured)
        {
            throw new LiteRequestException(
                LiteSpotifyCatalogContract.FailureMessage(
                    LiteSpotifyFailureReason.NotConfigured,
                    LiteSpotifyCatalogContract.AccountStatusOperation));
        }
        if (account.CatalogConnected != true)
        {
            var reason = account.CatalogConnected == false
                ? LiteSpotifyFailureReason.NotConnected
                : LiteSpotifyFailureReason.Unknown;
            throw new LiteRequestException(
                LiteSpotifyCatalogContract.FailureMessage(
                    reason,
                    LiteSpotifyCatalogContract.AccountStatusOperation));
        }
    }

    private static LiteRequestException SafeRequestException(
        Exception exception,
        string operation) => new(SafeFailure(exception, operation));

    private static string SafeFailure(Exception exception, string operation)
    {
        var actual = exception is AggregateException aggregate
            ? aggregate.Flatten().InnerExceptions.FirstOrDefault() ?? exception
            : exception;
        var reason = actual switch
        {
            LiteSpotifyBackendException backendException => backendException.Reason,
            SpotifyWriteBlockedException blocked => blocked.Reason switch
            {
                SpotifyWriteBlockReason.MissingScope =>
                    LiteSpotifyFailureReason.AccessDenied,
                SpotifyWriteBlockReason.Unauthorized =>
                    LiteSpotifyFailureReason.NotConnected,
                SpotifyWriteBlockReason.Denied =>
                    LiteSpotifyFailureReason.AccessDenied,
                _ => LiteSpotifyFailureReason.Unknown
            },
            OperationCanceledException => LiteSpotifyFailureReason.Cancelled,
            HttpRequestException => LiteSpotifyFailureReason.Network,
            TimeoutException => LiteSpotifyFailureReason.Timeout,
            _ => LiteSpotifyFailureReason.Unknown
        };
        if ((operation == LiteSpotifyCatalogContract.MembershipOperation
                || operation == LiteSpotifyCatalogContract.PlaylistTargetsOperation
                || operation == LiteSpotifyCatalogContract.PlaylistCreateOperation
                || operation == LiteSpotifyCatalogContract.PlaylistAddItemsOperation)
            && actual is SpotifyWriteBlockedException blockedWrite)
        {
            return blockedWrite.Reason switch
            {
                SpotifyWriteBlockReason.MissingScope =>
                    "Brakuje zgody Spotify na zmiany w kolekcji. "
                        + "Zaloguj konto Spotify ponownie w ustawieniach AMC.",
                SpotifyWriteBlockReason.Unauthorized =>
                    "Logowanie katalogu Spotify wygasło. Zaloguj konto ponownie.",
                SpotifyWriteBlockReason.UnsupportedKind =>
                    "Spotify nie pozwala zapisać tego rodzaju elementu.",
                _ => "Spotify odmówił zmiany kolekcji."
            };
        }
        return LiteSpotifyCatalogContract.FailureMessage(reason, operation);
    }

    private static string NormalizeExternalId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var candidate = value.Trim();
        var parts = candidate.Split(':', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 3
            && string.Equals(parts[0], "spotify", StringComparison.OrdinalIgnoreCase)
                ? parts[2]
                : candidate;
    }

    public void Dispose() => backend.Dispose();
}

internal interface ILiteSpotifyReadOnlyBackend : IDisposable
{
    IReadOnlyList<MediaItem> CachedCollection { get; }

    LiteSpotifyAccountProbe ReadAccountStatus();

    Task<LiteSpotifyCollectionRefresh> RefreshCollectionAsync(
        CancellationToken cancellationToken);

    Task<LiteSpotifyCollectionRefresh> RefreshAlbumsAsync(
        CancellationToken cancellationToken);

    Task<LiteSpotifyCollectionRefresh> RefreshPodcastsAsync(
        CancellationToken cancellationToken);

    Task<IReadOnlyList<MediaItem>> SearchAsync(
        string query,
        CancellationToken cancellationToken);

    Task<string?> GetDescriptionAsync(
        LiteSpotifyDescriptionRequest request,
        CancellationToken cancellationToken);

    Task<LiteSpotifyContainerBackendResult> GetContainerItemsAsync(
        LiteSpotifyContainerRequest request,
        CancellationToken cancellationToken);
}

internal interface ILiteSpotifyWriteBackend : ILiteSpotifyReadOnlyBackend
{
    Task<LiteSpotifyMembershipChange> ChangeMembershipAsync(
        IReadOnlyList<MediaItem> items,
        bool? requestedAddition,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<SpotifyPlaylistTarget>> GetPlaylistTargetsAsync(
        CancellationToken cancellationToken);

    Task<SpotifyPlaylistTarget> CreatePlaylistAsync(
        string name,
        bool isPublic,
        CancellationToken cancellationToken);

    Task<SpotifyPlaylistWriteResult> AddPlaylistItemsAsync(
        IReadOnlyList<string> playlistIds,
        IReadOnlyList<MediaItem> items,
        CancellationToken cancellationToken);
}

internal interface ILiteSpotifyAccountBackend : ILiteSpotifyReadOnlyBackend
{
    void ConfigureClientId(string clientId);

    Task<LiteSpotifyPairingRequest> StartPairingAsync(CancellationToken cancellationToken);

    LiteSpotifyPairingStatusResult PairingStatus();

    void CancelPairing();

    Task<LiteSpotifyLoginOutcome> LoginAsync(CancellationToken cancellationToken);
}

internal sealed record LiteSpotifyPairingRequest(string UserCode, string VerificationUri);

internal sealed record LiteSpotifyLoginOutcome(
    string AccountDisplayName,
    string AccountProduct);

internal sealed record LiteSpotifyCollectionRefresh(
    IReadOnlyList<MediaItem> Items,
    bool HadWarnings);

internal sealed record LiteSpotifyContainerBackendResult(
    IReadOnlyList<MediaItem> Items,
    bool Available,
    bool HasMore = false);

internal sealed record LiteSpotifyMembershipChange(
    bool Added,
    IReadOnlyList<MediaItem> ConfirmedItems,
    int FailedCount);

internal sealed class LiteSpotifyBackendException(
    LiteSpotifyFailureReason reason,
    Exception? innerException = null)
    : Exception("Spotify backend failure.", innerException)
{
    public LiteSpotifyFailureReason Reason { get; } = reason;
}
