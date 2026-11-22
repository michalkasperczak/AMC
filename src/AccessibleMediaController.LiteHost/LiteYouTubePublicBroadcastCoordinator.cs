using System.Text.Json;
using AccessibleMediaController.Core.YouTube;
using AccessibleMediaController.LiteHost.Protocol;

namespace AccessibleMediaController.LiteHost;

/// <summary>
/// Publiczna, bezstanowa powierzchnia transmisji YouTube. Nie tworzy OAuth,
/// nie czyta poświadczeń i może istnieć, gdy konto YouTube nie jest skonfigurowane.
/// </summary>
internal interface ILiteYouTubePublicBroadcastBackend : IDisposable
{
    bool IsAvailable { get; }
    string AvailabilityMessage { get; }
    Task<IReadOnlyList<YouTubeBroadcast>> ReadBroadcastsAsync(
        string subscriptionId, bool forceRefresh, CancellationToken cancellationToken);
    Task<LiteYouTubeRadioBroadcastResult> ReadRadioBroadcastsAsync(
        string sourceUrl, string? subscriptionId, CancellationToken cancellationToken);
}

internal sealed record LiteYouTubeRadioBroadcastResult(
    IReadOnlyList<YouTubeBroadcast> Rows, string? ChannelLiveUrl);

internal sealed class LiteYouTubePublicBroadcastCoordinator(
    ILiteYouTubePublicBroadcastBackend backend) : IDisposable
{
    private readonly ILiteYouTubePublicBroadcastBackend _backend = backend
        ?? throw new ArgumentNullException(nameof(backend));

    internal IReadOnlyDictionary<string, Func<LiteRequest, LiteEventSink, object?>> Handlers() =>
        new Dictionary<string, Func<LiteRequest, LiteEventSink, object?>>(StringComparer.Ordinal)
        {
            [LiteYouTubeAccountContract.BroadcastCapabilitiesOperation] = (_, _) =>
                new LiteYouTubeBroadcastCapabilities(_backend.IsAvailable, _backend.AvailabilityMessage),
            [LiteYouTubeAccountContract.BroadcastsOperation] = (request, _) =>
                Broadcasts(request.Args),
            [LiteYouTubeAccountContract.RadioBroadcastsOperation] = (request, _) =>
                RadioBroadcasts(request.Args)
        };

    private object Broadcasts(JsonElement args)
    {
        var request = LiteYouTubeAccountContract.ReadBroadcastRequest(args);
        if (!_backend.IsAvailable)
            throw new LiteRequestException(_backend.AvailabilityMessage);
        try
        {
            var rows = _backend.ReadBroadcastsAsync(
                request.SubscriptionId, request.ForceRefresh, CancellationToken.None)
                .GetAwaiter().GetResult();
            return LiteYouTubeAccountContract.CreateBroadcasts(rows);
        }
        catch (KeyNotFoundException)
        {
            throw new LiteRequestException("Tego kanału YouTube nie ma już w Bibliotece.");
        }
        catch (LiteRequestException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw SafeFailure(exception, "Nie udało się pobrać transmisji YouTube.");
        }
    }

    private object RadioBroadcasts(JsonElement args)
    {
        var sourceUrl = LiteArgs.RequireText(args, "url");
        if (!_backend.IsAvailable)
            throw new LiteRequestException(_backend.AvailabilityMessage);
        string? subscriptionId = null;
        if (args.TryGetProperty("subscriptionId", out var subscription)
            && subscription.ValueKind == JsonValueKind.String)
            subscriptionId = subscription.GetString()?.Trim();
        try
        {
            var result = _backend.ReadRadioBroadcastsAsync(
                sourceUrl, subscriptionId, CancellationToken.None).GetAwaiter().GetResult();
            return LiteYouTubeAccountContract.CreateBroadcasts(
                result.Rows, result.ChannelLiveUrl);
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
        return exception switch
        {
            TimeoutException or OperationCanceledException =>
                new LiteRequestException("YouTube nie odpowiedział w bezpiecznym czasie."),
            HttpRequestException => new LiteRequestException("Nie udało się połączyć z YouTube."),
            _ => new LiteRequestException(fallback)
        };
    }

    public void Dispose() => _backend.Dispose();
}
