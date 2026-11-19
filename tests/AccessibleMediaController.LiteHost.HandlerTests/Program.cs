using System.Text.Json;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Podcasts;
using AccessibleMediaController.LiteHost;
using AccessibleMediaController.LiteHost.Protocol;
using Microsoft.Data.Sqlite;

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
            Console.WriteLine("LiteHost automatic refresh dispatcher: OK");
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

    private static class Assert
    {
        public static void Equal(int expected, int actual, string message)
        {
            if (expected != actual)
                throw new InvalidOperationException($"{message}; oczekiwano {expected}, otrzymano {actual}.");
        }
    }
}
