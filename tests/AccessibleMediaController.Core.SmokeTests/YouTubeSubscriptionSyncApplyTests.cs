using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Podcasts;
using AccessibleMediaController.Core.YouTube;

internal static class YouTubeSubscriptionSyncApplyTests
{
    internal static void Run()
    {
        AddsShellsForBoundedRefreshAndIsIdempotent();
        NewAccountChannelsStayOutsideCustomizedInboxSelection();
        KeepsSuppressedUntilExplicitRestore();
        RemoteAbsenceDoesNotRemoveLocalSource();
        ConflictDoesNotPartiallyMutateDatabase();
        KeepsFailedAutomaticRefreshFairAndPersistent();
        DeletesOnlyExpiredOrExplicitAccountData();
        RefreshesSeveralSourcesWithOneDurableSave();
        Console.WriteLine("OK: atomowy zapis subskrypcji YouTube i seria podcastów (8 scenariuszy)");
    }

    private static void NewAccountChannelsStayOutsideCustomizedInboxSelection()
    {
        var settings = new PodcastSettings
        {
            InboxSelectionCustomized = true,
            YouTubeRefreshIntervalMinutes = 60
        };
        WithDatabase(settings, (database, store) =>
        {
            var result = store.ApplyYouTubeSubscriptions(
                [Remote("UC-NEW-ACCOUNT", "Nowy kanał")]);
            Assert(result.Added == 1,
                "Synchronizacja nie dodała nowego kanału konta.");
            var reopened = new PodcastSettings();
            database.LoadInto(reopened);
            Assert(reopened.Subscriptions.Single().RefreshIntervalMinutes == 0,
                "Nowy kanał konta ominął ręczny wybór źródeł skrzynki.");
        });
    }

    private static void RefreshesSeveralSourcesWithOneDurableSave()
    {
        var settings = new PodcastSettings
        {
            Subscriptions = [
                new PodcastSubscriptionSettings {
                    Id = "batch-a", Title = "Pierwszy", FeedUrl = "https://example.invalid/a.xml",
                    SourceKind = PodcastSourceKind.Rss, IsInLibrary = true },
                new PodcastSubscriptionSettings {
                    Id = "batch-b", Title = "Drugi", FeedUrl = "https://example.invalid/b.xml",
                    SourceKind = PodcastSourceKind.Rss, IsInLibrary = true }
            ]
        };
        WithDatabase(settings, (database, store) =>
        {
            static PodcastRefreshRequest Request(string id, string suffix) => new(
                id,
                new PodcastFeedDocument(
                    id, id, "Autor", "Opis", new Uri($"https://example.invalid/{suffix}.xml"),
                    null,
                    [new PodcastFeedEpisode(
                        $"{id}:episode", $"{id}:source", $"Odcinek {suffix}",
                        "Autor", "Opis", DateTimeOffset.UtcNow,
                        TimeSpan.FromMinutes(5),
                        new Uri($"https://example.invalid/{suffix}.mp3"),
                        null, "audio/mpeg", null)]));

            var requests = new[] { Request("batch-a", "a"), Request("batch-b", "b") };
            var first = store.ApplyRefreshBatch(requests, DateTime.UtcNow);
            Assert(first.Count == 2 && first.All(result => result.AddedEpisodes == 1),
                "Seria nie dodała po jednym odcinku do obu źródeł.");
            Assert(first.All(result => result.InboxCount == 2),
                "Wyniki serii nie zawierają końcowej liczby nowych odcinków.");
            var reopened = new PodcastSettings();
            database.LoadInto(reopened);
            Assert(reopened.Episodes.Select(item => item.Id).ToHashSet(StringComparer.Ordinal)
                       .SetEquals(["batch-a:episode", "batch-b:episode"]),
                "Ponowny odczyt nie zachował obu źródeł serii.");
            var again = store.ApplyRefreshBatch(requests, DateTime.UtcNow);
            Assert(again.All(result => result.AddedEpisodes == 0)
                   && store.GetInboxCount() == 2,
                "Ponowienie serii zdublowało odcinki lub zmieniło skrzynkę.");
        });
    }

    private static void AddsShellsForBoundedRefreshAndIsIdempotent()
    {
        WithDatabase(new PodcastSettings(), (database, store) =>
        {
            var preview = store.PreviewYouTubeSubscriptions(
                [Remote("UC-JEDEN", "Kanał jeden"), Remote("UC-DWA", "Kanał dwa")]);
            Assert(preview.Actions.Count == 2 && store.GetRefreshTargets().Count == 0,
                "Sam podgląd zmienił Bibliotekę przed potwierdzeniem.");
            var first = store.ApplyYouTubeSubscriptions(
                [Remote("UC-JEDEN", "Kanał jeden"), Remote("UC-DWA", "Kanał dwa")]);
            Assert(first.Added == 2 && first.Changed == 2,
                "Pierwsza synchronizacja nie dodała dwóch źródeł.");

            var due = store.GetDueRefreshTargets(DateTime.UtcNow);
            Assert(due.Count == 2
                   && due.All(item => item.SourceKind == PodcastSourceKind.YouTubeChannel),
                "Nowe źródła nie oczekują na istniejące porcjowane odświeżenie.");
            Assert(due.All(item => item.FeedUrl.EndsWith("/videos", StringComparison.Ordinal)),
                "Zapisano niestabilny adres kanału YouTube.");
            var tagged = new PodcastSettings();
            database.LoadInto(tagged);
            Assert(tagged.Subscriptions.All(item =>
                       item.IsYouTubeAccountData
                       && item.YouTubeAccountDataRefreshedUtcTicks > 0),
                "Kanały konta nie dostały jawnego pochodzenia i daty odświeżenia.");

            var second = store.ApplyYouTubeSubscriptions(
                [Remote("UC-JEDEN", "Kanał jeden"), Remote("UC-DWA", "Kanał dwa")]);
            Assert(second.Added == 0 && second.AlreadyPresent == 2,
                "Ponowienie synchronizacji utworzyłoby duplikaty.");
            Assert(store.GetRefreshTargets().Count == 2,
                "Ponowienie zmieniło liczbę źródeł.");
        });
    }

    private static void KeepsSuppressedUntilExplicitRestore()
    {
        var settings = new PodcastSettings
        {
            Subscriptions =
            [
                new PodcastSubscriptionSettings
                {
                    Id = "stary-kanal",
                    Title = "Stara nazwa",
                    FeedUrl = "https://www.youtube.com/channel/UC-USUNIETY/videos",
                    SourceKind = PodcastSourceKind.YouTubeChannel,
                    IsInLibrary = false
                }
            ]
        };
        WithDatabase(settings, (database, store) =>
        {
            var ordinary = store.ApplyYouTubeSubscriptions(
                [Remote("UC-USUNIETY", "Nazwa z YouTube")]);
            Assert(ordinary.Changed == 0 && ordinary.Suppressed == 1,
                "Zwykła synchronizacja przywróciła świadomie usunięty kanał.");
            Assert(store.GetRefreshTargets().Count == 0,
                "Usunięty kanał wrócił do aktywnych źródeł.");

            var reopened = new PodcastLibraryMutationStore(database.Path);
            var afterRestart = reopened.ApplyYouTubeSubscriptions(
                [Remote("UC-USUNIETY", "Nazwa z YouTube")]);
            Assert(afterRestart.Changed == 0 && afterRestart.Suppressed == 1
                   && reopened.GetRefreshTargets().Count == 0,
                "Ponowne uruchomienie albo synchronizacja przywróciły usunięty kanał.");

            var restored = reopened.ApplyYouTubeSubscriptions(
                [Remote("UC-USUNIETY", "Nazwa z YouTube")],
                restoreSuppressed: true);
            Assert(restored.Restored == 1 && restored.Added == 0,
                "Jawne przywrócenie nie użyło istniejącego rekordu.");
            var target = reopened.GetRefreshTargets().Single();
            Assert(target.SubscriptionId == "stary-kanal" && target.Title == "Stara nazwa",
                "Przywrócenie zgubiło tożsamość albo nadpisało ręczne dane nazwą z konta.");
        });
    }

    private static void RemoteAbsenceDoesNotRemoveLocalSource()
    {
        var settings = new PodcastSettings
        {
            Subscriptions =
            [
                new PodcastSubscriptionSettings
                {
                    Id = "lokalny-kanal",
                    Title = "Lokalny kanał",
                    FeedUrl = "https://www.youtube.com/channel/UC-LOKALNY/videos",
                    SourceKind = PodcastSourceKind.YouTubeChannel,
                    IsInLibrary = true
                }
            ]
        };
        WithDatabase(settings, (_, store) =>
        {
            var result = store.ApplyYouTubeSubscriptions([]);
            Assert(result.Changed == 0 && store.GetRefreshTargets().Count == 1,
                "Brak subskrypcji w odpowiedzi Google usunął lokalne źródło.");
        });
    }

    private static void ConflictDoesNotPartiallyMutateDatabase()
    {
        var settings = new PodcastSettings
        {
            Subscriptions =
            [
                new PodcastSubscriptionSettings
                {
                    Id = "youtube-account-channel:UC-KOLIZJA",
                    Title = "Inny typ",
                    FeedUrl = "https://example.test/feed.xml",
                    SourceKind = PodcastSourceKind.Rss,
                    IsInLibrary = true
                }
            ]
        };
        WithDatabase(settings, (_, store) =>
        {
            try
            {
                store.ApplyYouTubeSubscriptions(
                    [Remote("UC-NOWY", "Nowy"), Remote("UC-KOLIZJA", "Kolizja")]);
                throw new InvalidOperationException("Sprzeczny profil został zaakceptowany.");
            }
            catch (InvalidDataException)
            {
            }
            var targets = store.GetRefreshTargets();
            Assert(targets.Count == 1 && targets[0].SourceKind == PodcastSourceKind.Rss,
                "Błąd w połowie planu pozostawił częściowy zapis.");
        });
    }

    private static void KeepsFailedAutomaticRefreshFairAndPersistent()
    {
        var start = new DateTime(2026, 10, 23, 18, 0, 0, DateTimeKind.Utc);
        var settings = new PodcastSettings
        {
            AutomaticRefreshBatchSize = 5,
            YouTubeRefreshIntervalMinutes = 60,
            Subscriptions = Enumerable.Range(1, 6)
                .Select(number => new PodcastSubscriptionSettings
                {
                    Id = $"kanał-{number}",
                    Title = $"Kanał {number}",
                    FeedUrl = $"https://www.youtube.com/channel/UC-KANAL-{number}/videos",
                    SourceKind = PodcastSourceKind.YouTubeChannel,
                    IsInLibrary = true,
                    RefreshIntervalMinutes = 60
                })
                .ToList()
        };
        WithDatabase(settings, (database, store) =>
        {
            var firstBatch = store.GetDueRefreshTargets(start);
            Assert(firstBatch.Count == 5,
                "Pierwszy przebieg nie zachował ustawionego rozmiaru porcji.");
            foreach (var target in firstBatch)
                store.MarkRefreshAttempt(target.SubscriptionId, start);

            var reopened = new PodcastLibraryMutationStore(database.Path);
            var secondBatch = reopened.GetDueRefreshTargets(start.AddMinutes(1));
            Assert(secondBatch.Count == 1 && secondBatch[0].SubscriptionId == "kanał-6",
                "Nieudana porcja zablokowała następny kanał albo nie przetrwała ponownego otwarcia bazy.");
            reopened.MarkRefreshAttempt(secondBatch[0].SubscriptionId, start.AddMinutes(1));

            Assert(reopened.GetDueRefreshTargets(start.AddMinutes(2)).Count == 0,
                "Automat ponowił nieudane źródła bez zachowania odstępu.");
            Assert(reopened.GetRefreshTargets().Count == 6,
                "Odstęp automatu zablokował ręczne odświeżanie F5.");

            var retry = reopened.GetDueRefreshTargets(start.AddMinutes(61));
            Assert(retry.Count == 5
                   && retry.All(target => target.SubscriptionId != "kanał-6"),
                "Po upływie odstępu automat nie wrócił do najstarszej nieudanej porcji.");
        });
    }

    private static void DeletesOnlyExpiredOrExplicitAccountData()
    {
        var now = new DateTime(2026, 10, 27, 12, 0, 0, DateTimeKind.Utc);
        var oldAccount = new PodcastSubscriptionSettings
        {
            Id = "youtube-account-channel:UC-STARY",
            Title = "Stare dane konta",
            FeedUrl = "https://www.youtube.com/channel/UC-STARY/videos",
            SourceKind = PodcastSourceKind.YouTubeChannel,
            IsInLibrary = true,
            IsYouTubeAccountData = true,
            YouTubeAccountDataRefreshedUtcTicks = now.AddDays(-31).Ticks
        };
        var freshAccount = new PodcastSubscriptionSettings
        {
            Id = "youtube-account-channel:UC-SWIEZY",
            Title = "Świeże dane konta",
            FeedUrl = "https://www.youtube.com/channel/UC-SWIEZY/videos",
            SourceKind = PodcastSourceKind.YouTubeChannel,
            IsInLibrary = true,
            IsYouTubeAccountData = true,
            YouTubeAccountDataRefreshedUtcTicks = now.AddDays(-29).Ticks
        };
        var manual = new PodcastSubscriptionSettings
        {
            Id = "youtube-channel:UC-RECZNY",
            Title = "Kanał dodany ręcznie",
            FeedUrl = "https://www.youtube.com/channel/UC-RECZNY/videos",
            SourceKind = PodcastSourceKind.YouTubeChannel,
            IsInLibrary = true
        };
        var settings = new PodcastSettings
        {
            CurrentItemId = oldAccount.Id + ":FILM",
            Subscriptions = [oldAccount, freshAccount, manual],
            Episodes =
            [
                new PodcastEpisodeSettings
                {
                    Id = oldAccount.Id + ":FILM",
                    SubscriptionId = oldAccount.Id,
                    Title = "Stary film"
                },
                new PodcastEpisodeSettings
                {
                    Id = freshAccount.Id + ":FILM",
                    SubscriptionId = freshAccount.Id,
                    Title = "Świeży film"
                }
            ]
        };

        WithDatabase(settings, (database, store) =>
        {
            var expired = store.DeleteExpiredYouTubeAccountData(now, TimeSpan.FromDays(30));
            var afterExpiry = new PodcastSettings();
            database.LoadInto(afterExpiry);
            Assert(expired.RemovedSources == 1 && expired.RemovedEpisodes == 1,
                "Czyszczenie po 30 dniach usunęło nieprawidłowy zakres.");
            Assert(afterExpiry.Subscriptions.Select(item => item.Id).ToHashSet(StringComparer.Ordinal)
                       .SetEquals([freshAccount.Id, manual.Id])
                   && afterExpiry.CurrentItemId is null,
                "Czyszczenie naruszyło świeże albo ręczne źródło lub zostawiło martwe odtwarzanie.");

            var all = store.DeleteAllYouTubeAccountData(now);
            var afterRevoke = new PodcastSettings();
            database.LoadInto(afterRevoke);
            Assert(all.RemovedSources == 1 && all.RemovedEpisodes == 1,
                "Cofnięcie zgody nie usunęło pozostałych danych konta.");
            Assert(afterRevoke.Subscriptions.Count == 1
                   && afterRevoke.Subscriptions[0].Id == manual.Id,
                "Cofnięcie zgody usunęło publiczny kanał dodany ręcznie.");
        });
    }

    private static void WithDatabase(
        PodcastSettings initial,
        Action<PodcastLibraryDatabase, PodcastLibraryMutationStore> action)
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "amc-youtube-sync-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "podcasts.db");
            var database = new PodcastLibraryDatabase(path);
            database.Initialize(initial);
            action(database, new PodcastLibraryMutationStore(path));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static YouTubeSubscription Remote(string id, string title) =>
        new("subscription-" + id, id, title, null);

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
