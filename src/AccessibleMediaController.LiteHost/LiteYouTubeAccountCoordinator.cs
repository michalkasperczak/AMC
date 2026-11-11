using System.Text.Json;
using AccessibleMediaController.Core.YouTube;
using AccessibleMediaController.Windows.Services;
using AccessibleMediaController.LiteHost.Protocol;

namespace AccessibleMediaController.LiteHost;

internal interface ILiteYouTubeAccountBackend : IDisposable
{
    YouTubeAccountStatus GetStatus();
    Task<YouTubeAccountOperationResult> LoginAsync(CancellationToken cancellationToken);
    Task<YouTubeAccountOperationResult> LogoutAsync(CancellationToken cancellationToken);
    Task<YouTubeSubscriptionSyncPlan> PreviewAsync(
        bool restoreSuppressed,
        CancellationToken cancellationToken);
    Task<YouTubeSubscriptionSyncApplyResult> ApplyAsync(
        bool restoreSuppressed,
        CancellationToken cancellationToken);
    Task<YouTubePlaylistSyncPlan> PreviewPlaylistsAsync(
        bool restoreSuppressed,
        CancellationToken cancellationToken);
    Task<YouTubePlaylistSyncApplyResult> ApplyPlaylistsAsync(
        bool restoreSuppressed,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<YouTubeBroadcast>> ReadBroadcastsAsync(
        string subscriptionId,
        bool forceRefresh,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<YouTubeBroadcast>> ReadChannelBroadcastsAsync(
        string channelId, CancellationToken cancellationToken);
}

/// <summary>
/// Wąska powierzchnia protokołu konta YouTube. Żaden wynik nie zawiera tokenu,
/// kodu, identyfikatora klienta, identyfikatora konta ani technicznego ID kanału.
/// </summary>
internal sealed class LiteYouTubeAccountCoordinator(
    ILiteYouTubeAccountBackend backend) : IDisposable
{
    private static readonly HashSet<string> SafeBackendMessages = new(StringComparer.Ordinal)
    {
        "Najpierw zaloguj się do YouTube.",
        "Najpierw sprawdź aktualne subskrypcje YouTube i zatwierdź ten sam zakres zmian.",
        "Najpierw sprawdź aktualne playlisty YouTube i zatwierdź ten sam zakres zmian.",
        "Logowanie YouTube straciło ważność. Zaloguj się ponownie.",
        "Kod lub zapisane logowanie YouTube straciło ważność. Zaloguj się ponownie.",
        "To konto Google nie ma jeszcze kanału YouTube.",
        "Wyczerpano dzienny limit YouTube. Spróbuj później.",
        "YouTube chwilowo ograniczył liczbę żądań. Spróbuj później.",
        "YouTube odmówił dostępu do subskrypcji tego konta.",
        "Nie udało się pobrać subskrypcji YouTube.",
        "Lista subskrypcji YouTube jest zbyt duża, aby bezpiecznie pobrać ją w jednym przebiegu.",
        "YouTube zwrócił powtarzającą się stronę subskrypcji.",
        "YouTube zwrócił nieprawidłowy znacznik strony subskrypcji.",
        "YouTube zwrócił nieczytelną listę subskrypcji.",
        "YouTube odmówił dostępu do playlist tego konta.",
        "Nie udało się pobrać playlist YouTube.",
        "Lista playlist YouTube jest zbyt duża, aby bezpiecznie pobrać ją w jednym przebiegu.",
        "YouTube zwrócił powtarzającą się stronę playlist.",
        "YouTube zwrócił powtarzającą się stronę materiałów playlisty.",
        "YouTube zwrócił nieprawidłowy znacznik strony playlist.",
        "YouTube zwrócił nieprawidłowy znacznik strony materiałów playlisty.",
        "YouTube zwrócił nieczytelną listę playlist.",
        "YouTube zwrócił nieczytelną zawartość playlisty.",
        "Tego kanału YouTube nie ma już w Bibliotece.",
        "To źródło nie jest kanałem YouTube.",
        "Ten kanał YouTube nie ma stabilnego identyfikatora potrzebnego do odczytu transmisji.",
        "Wyczerpano dzienny limit wyszukiwania transmisji YouTube. Spróbuj później.",
        "YouTube odmówił dostępu do transmisji tego kanału.",
        "Nie udało się pobrać transmisji YouTube.",
        "YouTube zwrócił nieczytelną listę transmisji.",
        "YouTube zwrócił nieczytelne szczegóły transmisji.",
        "Google zwrócił niepełne dane logowania YouTube.",
        "Google zwrócił nieczytelną odpowiedź logowania YouTube.",
        "Ta wersja AMC nie ma prawidłowo skonfigurowanego logowania YouTube.",
        "Klient OAuth YouTube ma niewłaściwy typ. Utwórz w Google klienta typu Aplikacja komputerowa.",
        "Google odrzucił dane logowania YouTube. Sprawdź konfigurację klienta typu Aplikacja komputerowa.",
        "Adres powrotu logowania YouTube nie pasuje do klienta OAuth. Użyj klienta typu Aplikacja komputerowa.",
        "Logowanie YouTube zostało anulowane na stronie Google.",
        "Google chwilowo ograniczył liczbę prób logowania YouTube. Spróbuj później.",
        "Google chwilowo ograniczył cofanie dostępu YouTube. Spróbuj później.",
        "Nie udało się cofnąć dostępu YouTube w Google. Spróbuj ponownie.",
        "Nie można bezpiecznie sprawdzić i usunąć nieaktualnych danych konta YouTube.",
        "Google odrzucił logowanie YouTube. Spróbuj zalogować się ponownie.",
        "Nie udało się połączyć z usługą logowania YouTube.",
        "Biblioteka zawiera sprzeczny rekord kanału YouTube. Nie zapisano synchronizacji.",
        "Biblioteka zmieniła się przed przywróceniem kanału YouTube. Nie zapisano synchronizacji.",
        "Biblioteka zawiera sprzeczny rekord playlisty YouTube. Nie zapisano synchronizacji.",
        "Biblioteka zmieniła się przed zapisem playlisty YouTube. Nie zapisano synchronizacji."
    };

    private readonly ILiteYouTubeAccountBackend _backend = backend
        ?? throw new ArgumentNullException(nameof(backend));
    private readonly object _loginGate = new();
    private CancellationTokenSource? _loginCancellation;

    internal IReadOnlyDictionary<string, Func<LiteRequest, LiteEventSink, object?>> Handlers() =>
        new Dictionary<string, Func<LiteRequest, LiteEventSink, object?>>(StringComparer.Ordinal)
        {
            [LiteYouTubeAccountContract.CapabilitiesOperation] = (_, _) =>
                new LiteYouTubeAccountCapabilities(
                    true,
                    true,
                    true,
                    "Konto YouTube jest dostępne tylko do odczytu."),
            [LiteYouTubeAccountContract.StatusOperation] = (_, _) => Status(),
            [LiteYouTubeAccountContract.LoginOperation] = (_, _) => Login(),
            [LiteYouTubeAccountContract.CancelLoginOperation] = (_, _) => CancelLogin(),
            [LiteYouTubeAccountContract.LogoutOperation] = (_, _) => Logout(),
            [LiteYouTubeAccountContract.SyncPreviewOperation] = (request, _) =>
                Preview(request.Args),
            [LiteYouTubeAccountContract.SyncApplyOperation] = (request, _) =>
                Apply(request.Args),
            [LiteYouTubeAccountContract.PlaylistPreviewOperation] = (request, _) =>
                PreviewPlaylists(request.Args),
            [LiteYouTubeAccountContract.PlaylistApplyOperation] = (request, _) =>
                ApplyPlaylists(request.Args),
            [LiteYouTubeAccountContract.BroadcastsOperation] = (request, _) =>
                Broadcasts(request.Args),
            [LiteYouTubeAccountContract.RadioBroadcastsOperation] = (request, _) =>
                RadioBroadcasts(request.Args)
        };

    private object Status()
    {
        try
        {
            var status = _backend.GetStatus();
            return new LiteYouTubeAccountState(status.Connected, status.Message);
        }
        catch
        {
            return new LiteYouTubeAccountState(
                false,
                "Nie można teraz sprawdzić logowania YouTube.");
        }
    }

    private object Login()
    {
        CancellationTokenSource cancellation;
        lock (_loginGate)
        {
            if (_loginCancellation is not null)
                return new LiteYouTubeAccountActionResult(
                    false,
                    "Logowanie YouTube już trwa.");
            cancellation = new CancellationTokenSource();
            _loginCancellation = cancellation;
        }
        try
        {
            var result = _backend.LoginAsync(cancellation.Token)
                .GetAwaiter().GetResult();
            return new LiteYouTubeAccountActionResult(result.Succeeded, result.Message);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            return new LiteYouTubeAccountActionResult(
                false,
                "Anulowano oczekiwanie na logowanie YouTube.");
        }
        catch (Exception exception)
        {
            throw SafeFailure(exception, "Nie udało się zalogować do YouTube.");
        }
        finally
        {
            lock (_loginGate)
            {
                if (ReferenceEquals(_loginCancellation, cancellation))
                    _loginCancellation = null;
            }
            cancellation.Dispose();
        }
    }

    private object CancelLogin()
    {
        CancellationTokenSource? cancellation;
        lock (_loginGate)
            cancellation = _loginCancellation;
        if (cancellation is null)
            return new LiteYouTubeAccountActionResult(
                false,
                "Logowanie YouTube nie jest teraz aktywne.");
        cancellation.Cancel();
        return new LiteYouTubeAccountActionResult(
            true,
            "Anulowano oczekiwanie na logowanie YouTube.");
    }

    private object Logout()
    {
        try
        {
            var result = _backend.LogoutAsync(CancellationToken.None)
                .GetAwaiter().GetResult();
            return new LiteYouTubeAccountActionResult(result.Succeeded, result.Message);
        }
        catch (Exception exception)
        {
            throw SafeFailure(exception, "Nie udało się wylogować z YouTube.");
        }
    }

    private object Preview(JsonElement args)
    {
        var restore = LiteYouTubeAccountContract.ReadRestoreSuppressed(args);
        try
        {
            var plan = _backend.PreviewAsync(restore, CancellationToken.None)
                .GetAwaiter().GetResult();
            return LiteYouTubeAccountContract.CreatePreview(plan);
        }
        catch (Exception exception)
        {
            throw SafeFailure(exception, "Nie udało się sprawdzić subskrypcji YouTube.");
        }
    }

    private object Apply(JsonElement args)
    {
        var restore = LiteYouTubeAccountContract.ReadRestoreSuppressed(args);
        try
        {
            var result = _backend.ApplyAsync(restore, CancellationToken.None)
                .GetAwaiter().GetResult();
            return LiteYouTubeAccountContract.CreateApply(result);
        }
        catch (Exception exception)
        {
            throw SafeFailure(exception, "Nie udało się zapisać subskrypcji YouTube.");
        }
    }

    private object PreviewPlaylists(JsonElement args)
    {
        var restore = LiteYouTubeAccountContract.ReadRestoreSuppressed(args);
        try
        {
            var plan = _backend.PreviewPlaylistsAsync(restore, CancellationToken.None)
                .GetAwaiter().GetResult();
            return LiteYouTubeAccountContract.CreatePlaylistPreview(plan);
        }
        catch (Exception exception)
        {
            throw SafeFailure(exception, "Nie udało się sprawdzić playlist YouTube.");
        }
    }

    private object ApplyPlaylists(JsonElement args)
    {
        var restore = LiteYouTubeAccountContract.ReadRestoreSuppressed(args);
        try
        {
            var result = _backend.ApplyPlaylistsAsync(restore, CancellationToken.None)
                .GetAwaiter().GetResult();
            return LiteYouTubeAccountContract.CreatePlaylistApply(result);
        }
        catch (Exception exception)
        {
            throw SafeFailure(exception, "Nie udało się zapisać playlist YouTube.");
        }
    }

    private object Broadcasts(JsonElement args)
    {
        var request = LiteYouTubeAccountContract.ReadBroadcastRequest(args);
        try
        {
            var rows = _backend.ReadBroadcastsAsync(
                    request.SubscriptionId,
                    request.ForceRefresh,
                    CancellationToken.None)
                .GetAwaiter().GetResult();
            return LiteYouTubeAccountContract.CreateBroadcasts(rows);
        }
        catch (KeyNotFoundException)
        {
            throw new LiteRequestException("Tego kanału YouTube nie ma już w Bibliotece.");
        }
        catch (Exception exception)
        {
            throw SafeFailure(exception, "Nie udało się pobrać transmisji YouTube.");
        }
    }

    private object RadioBroadcasts(JsonElement args)
    {
        var sourceUrl = LiteArgs.RequireText(args, "url");
        if (!YouTubeSourceResolver.IsYouTubeUrl(sourceUrl))
            throw new LiteRequestException("Ta stacja nie ma adresu YouTube.");
        try
        {
            string? subscriptionId = null;
            if (args.TryGetProperty("subscriptionId", out var subscription)
                && subscription.ValueKind == JsonValueKind.String)
                subscriptionId = subscription.GetString()?.Trim();
            IReadOnlyList<YouTubeBroadcast> rows;
            if (!string.IsNullOrEmpty(subscriptionId))
            {
                rows = _backend.ReadBroadcastsAsync(
                    subscriptionId, true, CancellationToken.None).GetAwaiter().GetResult();
            }
            else
            {
                var uri = new Uri(sourceUrl);
                var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                var liveUrl = segments.Length >= 2
                              && segments[0].Equals("channel", StringComparison.OrdinalIgnoreCase)
                    ? YouTubeSourceResolver.TryBuildChannelLiveUrl(
                        Uri.UnescapeDataString(segments[1]))
                    : null;
                liveUrl ??= YouTubeSourceResolver.TryResolveChannelLiveUrlAsync(
                    YouTubeSourceResolver.FindExecutable()
                    ?? throw new LiteRequestException(
                        "Sprawdź składnik yt-dlp w menu Pomoc."),
                    sourceUrl, CancellationToken.None).GetAwaiter().GetResult();
                if (liveUrl is null)
                    throw new LiteRequestException(
                        "Nie udało się ustalić kanału YouTube tej stacji.");
                var channelId = new Uri(liveUrl).AbsolutePath.Split(
                    '/', StringSplitOptions.RemoveEmptyEntries)[1];
                rows = _backend.ReadChannelBroadcastsAsync(
                    channelId, CancellationToken.None).GetAwaiter().GetResult();
            }
            return LiteYouTubeAccountContract.CreateBroadcasts(rows);
        }
        catch (LiteRequestException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw SafeFailure(exception, "Nie udało się znaleźć transmisji kanału YouTube.");
        }
    }

    private static LiteRequestException SafeFailure(Exception exception, string fallback)
    {
        if (exception is InvalidOperationException or InvalidDataException
            && SafeBackendMessages.Contains(exception.Message))
            return new LiteRequestException(exception.Message);
        if (exception is TimeoutException or OperationCanceledException)
            return new LiteRequestException("YouTube nie odpowiedział w bezpiecznym czasie.");
        if (exception is HttpRequestException)
            return new LiteRequestException("Nie udało się połączyć z YouTube.");
        return new LiteRequestException(fallback);
    }

    public void Dispose()
    {
        lock (_loginGate)
            _loginCancellation?.Cancel();
        _backend.Dispose();
    }
}
