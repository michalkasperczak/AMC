using System.Runtime.InteropServices;
using System.Text.Json;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Podcasts;
using AccessibleMediaController.LiteHost;
using AccessibleMediaController.LiteHost.Protocol;
using AccessibleMediaController.Windows.Services;
using Microsoft.Data.Sqlite;
using NAudio.Wave;

namespace AccessibleMediaController.LiteHost.HandlerTests;

internal static class Program
{
    private static int Main()
    {
        var root = Path.Combine(Path.GetTempPath(), "amc-lite-handler-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            Assert.Equal(4, DispatchRequested(root, "{}"),
                "brak cadenceSeconds musi przejść przez rzeczywisty dispatcher do legacy pełnego limitu");
            Assert.Equal(1, DispatchRequested(root, "{\"cadenceSeconds\":60}"),
                "jawny takt ma użyć adaptacyjnego limitu, nie legacy pełnego limitu");
            Assert.Equal(1, DispatchRequested(root, "{\"cadenceSeconds\":0}"),
                "jawne zero musi być nadal walidowane jako takt co najmniej jednej sekundy, nie jak brak pola");
            ProtectedFallbackResumesThroughProductionTransport(root);
            Console.WriteLine("LiteHost automatic refresh dispatcher and protected output transport: OK");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static void ProtectedFallbackResumesThroughProductionTransport(string root)
    {
        var wavePath = Path.Combine(root, "same-item.wav");
        WriteWave(wavePath);
        var outputs = new List<FakeWavePlayer>();
        using var factory = AudioOutputDeviceCatalog.OverrideOutputFactoryForTesting((_, _) =>
        {
            var replacement = new FakeWavePlayer();
            outputs.Add(replacement);
            return new AudioOutputDeviceLease(
                replacement,
                device: null,
                usesDefaultDevice: outputs.Count > 1,
                endpointId: outputs.Count > 1 ? "default-speakers" : "denon");
        });
        using var handlers = new LiteEngineHandlers(0);
        var operations = handlers.Build();
        var sink = new LiteEventSink(_ => { });

        Dispatch(operations, "audio.configure", "{\"pauseOnOutputFallback\":true}", sink);
        Dispatch(operations, "files.play", JsonSerializer.Serialize(new
        {
            path = wavePath,
            id = "same-item",
            title = "Ta sama pozycja",
            positionSeconds = 0,
            volume = 37,
            rate = 1d
        }), sink);
        Assert.True(SpinWait.SpinUntil(() => outputs.Count == 1 && outputs[0].PlayCalls == 1,
            TimeSpan.FromSeconds(5)),
            "host nie uruchomił początkowego toru pliku przez produkcyjny dispatcher");

        outputs[0].RaiseStopped(new COMException("Urządzenie odłączone", unchecked((int)0x88890004)));
        Assert.True(SpinWait.SpinUntil(() => outputs.Count == 2, TimeSpan.FromSeconds(5)),
            "utrata wyjścia nie zbudowała fallbacku przez produkcyjny host");
        Assert.Equal(0, outputs[1].PlayCalls,
            "ochronny fallback uruchomił dźwięk przed jawnym wznowieniem transportu");

        var resumed = Dispatch(operations, "transport.pauseResume", "{\"sessionId\":\"files\"}", sink);
        using var response = JsonDocument.Parse(JsonSerializer.Serialize(resumed));
        Assert.True(!response.RootElement.GetProperty("paused").GetBoolean(),
            "transport.pauseResume nie zwrócił wznowionego stanu po ochronnym fallbacku");
        Assert.True(SpinWait.SpinUntil(() => outputs[1].PlayCalls == 1, TimeSpan.FromSeconds(5)),
            "transport.pauseResume nie wznowił tej samej pozycji na fallbacku");
        Assert.Equal(2, outputs.Count,
            "wznowienie po fallbacku otworzyło nowy dekoder zamiast użyć podmienionego wyjścia");
        Dispatch(operations, "transport.stop", "{\"sessionId\":\"files\"}", sink);
    }

    private static object? Dispatch(
        IReadOnlyDictionary<string, Func<LiteRequest, LiteEventSink, object?>> operations,
        string operation,
        string argsJson,
        LiteEventSink sink)
    {
        using var args = JsonDocument.Parse(argsJson);
        return operations[operation](new LiteRequest
        {
            Id = operation,
            Op = operation,
            Args = args.RootElement.Clone()
        }, sink);
    }

    private static void WriteWave(string path)
    {
        const int sampleRate = 8_000;
        var samples = Enumerable.Range(0, sampleRate)
            .Select(index => (short)(index % 40 < 20 ? 4_000 : -4_000)).ToArray();
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);
        writer.Write("RIFF"u8.ToArray());
        writer.Write(36 + samples.Length * sizeof(short));
        writer.Write("WAVEfmt "u8.ToArray());
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(sampleRate);
        writer.Write(sampleRate * sizeof(short));
        writer.Write((short)sizeof(short));
        writer.Write((short)16);
        writer.Write("data"u8.ToArray());
        writer.Write(samples.Length * sizeof(short));
        foreach (var sample in samples) writer.Write(sample);
    }

    private static int DispatchRequested(string root, string argsJson)
    {
        var database = Path.Combine(root, Guid.NewGuid().ToString("N"), "podcasts.db");
        using var store = LitePodcastProgressStore.Open(database, () => false);
        store.ConfigureGlobalSettings(null, 60, 60, automaticRefreshBatchSize: 4);
        for (var number = 1; number <= 5; number++)
        {
            var id = $"source-{number}";
            store.AddSource(new PodcastFeedDocument(
                id, $"Źródło {number}", "Autor", string.Empty,
                new Uri($"https://example.invalid/{id}.xml"), null, []),
                null, PodcastSourceKind.Rss);
            SetDue(database, id);
        }

        using var coordinator = new LitePodcastRefreshCoordinator(
            store,
            null,
            (target, _) => Task.FromResult(new PodcastFeedDocument(
                target.SubscriptionId, target.Title, "Autor", string.Empty,
                new Uri(target.FeedUrl), null, [])));
        using var handlers = new LiteEngineHandlers(0, coordinator);
        using var args = JsonDocument.Parse(argsJson);
        var request = new LiteRequest
        {
            Id = "automatic-refresh",
            Op = LitePodcastRefreshCoordinator.AutomaticOperation,
            Args = args.RootElement.Clone()
        };
        var result = handlers.Build()[LitePodcastRefreshCoordinator.AutomaticOperation](
            request, new LiteEventSink(_ => { }));
        using var response = JsonDocument.Parse(JsonSerializer.Serialize(result));
        return response.RootElement.GetProperty("requested").GetInt32();
    }

    private static void SetDue(string database, string subscriptionId)
    {
        using var connection = new SqliteConnection($"Data Source={database};Pooling=False");
        connection.Open();
        using var read = connection.CreateCommand();
        read.CommandText = "SELECT payload_json FROM podcast_subscriptions WHERE id = $id;";
        read.Parameters.AddWithValue("$id", subscriptionId);
        var subscription = JsonSerializer.Deserialize<PodcastSubscriptionSettings>(
            Convert.ToString(read.ExecuteScalar())!)!;
        subscription.LastRefreshUtcTicks = 0;
        using var update = connection.CreateCommand();
        update.CommandText = """
            UPDATE podcast_subscriptions
            SET last_refresh_utc_ticks = 0, payload_json = $payload
            WHERE id = $id;
            """;
        update.Parameters.AddWithValue("$id", subscriptionId);
        update.Parameters.AddWithValue("$payload", JsonSerializer.Serialize(subscription));
        update.ExecuteNonQuery();
    }

    private sealed class FakeWavePlayer : IWavePlayer
    {
        public event EventHandler<StoppedEventArgs>? PlaybackStopped;
        public PlaybackState PlaybackState { get; private set; } = PlaybackState.Stopped;
        public float Volume { get; set; }
        public WaveFormat OutputWaveFormat { get; private set; } = WaveFormat.CreateIeeeFloatWaveFormat(8_000, 1);
        public int PlayCalls { get; private set; }

        public void Init(IWaveProvider waveProvider)
        {
            ArgumentNullException.ThrowIfNull(waveProvider);
            OutputWaveFormat = waveProvider.WaveFormat;
        }
        public void Play() { ++PlayCalls; PlaybackState = PlaybackState.Playing; }
        public void Pause() => PlaybackState = PlaybackState.Paused;
        public void Stop() => PlaybackState = PlaybackState.Stopped;
        public void Dispose() => PlaybackState = PlaybackState.Stopped;
        public void RaiseStopped(Exception exception)
        {
            PlaybackState = PlaybackState.Stopped;
            PlaybackStopped?.Invoke(this, new StoppedEventArgs(exception));
        }
    }

    private static class Assert
    {
        public static void True(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        public static void Equal(int expected, int actual, string message)
        {
            if (expected != actual)
                throw new InvalidOperationException($"{message}; oczekiwano {expected}, otrzymano {actual}.");
        }
    }
}
