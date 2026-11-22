using System.Text.Json;
using AccessibleMediaController.Core.YouTube;
using AccessibleMediaController.LiteHost.Protocol;

namespace AccessibleMediaController.LiteHost.ProtocolTests;

internal static class YouTubePublicBroadcastCoordinatorTests
{
    internal static void Run()
    {
        AvailableWithoutGoogleAccountAndReadsPublicChannel();
        EmptyPublicResponseIsPreserved();
        RadioBroadcastReturnsDurableChannelAddress();
        PublicFailureIsRedacted();
        Console.WriteLine("OK: publiczne transmisje YouTube bez konta (4 scenariusze)");
    }

    private static void AvailableWithoutGoogleAccountAndReadsPublicChannel()
    {
        using var backend = new FakePublicBackend
        {
            Broadcasts = [Live("Publiczna transmisja")]
        };
        using var coordinator = new LiteYouTubePublicBroadcastCoordinator(backend);
        var capabilities = Invoke(coordinator, LiteYouTubeAccountContract.BroadcastCapabilitiesOperation);
        var broadcasts = Invoke(coordinator, LiteYouTubeAccountContract.BroadcastsOperation,
            """{"subscriptionId":"youtube-channel:publiczny","refresh":false}""");
        var json = LiteJson.Serialize(new[] { capabilities, broadcasts });

        Check(json.Contains("\"available\":true", StringComparison.Ordinal)
              && json.Contains("bez konta Google", StringComparison.Ordinal)
              && backend.ReadCalls == 1
              && json.Contains("Publiczna transmisja", StringComparison.Ordinal),
            "Publiczna ścieżka transmisji wymagała konta albo nie zwróciła wyniku.");
    }

    private static void EmptyPublicResponseIsPreserved()
    {
        using var coordinator = new LiteYouTubePublicBroadcastCoordinator(new FakePublicBackend());
        var result = Invoke(coordinator, LiteYouTubeAccountContract.BroadcastsOperation,
            """{"subscriptionId":"youtube-channel:pusty","refresh":true}""");
        var json = LiteJson.Serialize(result);
        Check(json.Contains("\"items\":[]", StringComparison.Ordinal)
              && json.Contains("Brak trwających", StringComparison.Ordinal),
            "Pusta lista transmisji nie zachowała kontraktu.");
    }

    private static void PublicFailureIsRedacted()
    {
        using var backend = new FakePublicBackend
        {
            FailedIds = new HashSet<string> { "youtube-channel:awaria" }
        };
        using var coordinator = new LiteYouTubePublicBroadcastCoordinator(backend);
        try
        {
            _ = Invoke(coordinator, LiteYouTubeAccountContract.BroadcastsOperation,
                """{"subscriptionId":"youtube-channel:awaria","refresh":true}""");
            throw new InvalidOperationException("Nie zgłoszono awarii publicznego kanału.");
        }
        catch (LiteRequestException exception)
        {
            Check(!exception.Message.Contains("prywatna", StringComparison.Ordinal)
                  && exception.Message == "Nie udało się pobrać transmisji YouTube.",
                "Surowy błąd publicznego backendu wyszedł przez protokół.");
        }
    }

    private static void RadioBroadcastReturnsDurableChannelAddress()
    {
        using var backend = new FakePublicBackend
        {
            Broadcasts = [Live("Trwająca transmisja")],
            ChannelLiveUrl = "https://www.youtube.com/channel/UCLQDRrFkBrwK6UbcUypMi5w/live"
        };
        using var coordinator = new LiteYouTubePublicBroadcastCoordinator(backend);
        var result = Invoke(coordinator, LiteYouTubeAccountContract.RadioBroadcastsOperation,
            """{"url":"https://www.youtube.com/watch?v=mD6gpBK7r6E"}""");
        var json = LiteJson.Serialize(result);
        Check(json.Contains("\"channelLiveUrl\":\"https://www.youtube.com/channel/UCLQDRrFkBrwK6UbcUypMi5w/live\"", StringComparison.Ordinal)
              && json.Contains("Trwająca transmisja", StringComparison.Ordinal),
            "Odświeżenie stacji nie zachowało stałego adresu kanału.");
    }

    private static object Invoke(LiteYouTubePublicBroadcastCoordinator coordinator, string operation,
        string? args = null)
    {
        using var document = args is null ? null : JsonDocument.Parse(args);
        return coordinator.Handlers()[operation](
            new LiteRequest { Id = "test", Op = operation, Args = document?.RootElement ?? default },
            new LiteEventSink(_ => { }))!;
    }

    private static YouTubeBroadcast Live(string title) => new(
        "wewnetrzne-id", title, "Kanał", new Uri("https://www.youtube.com/watch?v=publiczny"),
        YouTubeBroadcastState.Live, null, DateTimeOffset.Parse("2026-11-17T12:00:00Z"));

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class FakePublicBackend : ILiteYouTubePublicBroadcastBackend
    {
        internal IReadOnlyList<YouTubeBroadcast> Broadcasts { get; init; } = [];
        internal IReadOnlySet<string> FailedIds { get; init; } = new HashSet<string>();
        internal string? ChannelLiveUrl { get; init; }
        internal int ReadCalls { get; private set; }
        public bool IsAvailable => true;
        public string AvailabilityMessage => "Publiczne transmisje YouTube są dostępne bez konta Google.";
        public Task<IReadOnlyList<YouTubeBroadcast>> ReadBroadcastsAsync(string subscriptionId,
            bool forceRefresh, CancellationToken cancellationToken)
        {
            ReadCalls++;
            if (FailedIds.Contains(subscriptionId))
                throw new InvalidOperationException("awaria prywatna");
            return Task.FromResult(Broadcasts);
        }
        public async Task<LiteYouTubeRadioBroadcastResult> ReadRadioBroadcastsAsync(string sourceUrl,
            string? subscriptionId, CancellationToken cancellationToken) =>
            new(await ReadBroadcastsAsync(subscriptionId ?? sourceUrl, true, cancellationToken),
                ChannelLiveUrl);
        public void Dispose() { }
    }
}
