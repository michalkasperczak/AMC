using System.Text.Json.Nodes;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Core.Spotify;

internal static class SpotifyMigrationReviewTests
{
    internal static void Run()
    {
        VerifyMalformedEngineKeepsOtherData();
        VerifyIdsFromRealSessionCopies();
        VerifyPresetArchiveIsIndependent();
        VerifyReferencesFollowMergedIdentity();
    }

    private static void VerifyMalformedEngineKeepsOtherData()
    {
        var root = Path.Combine(Path.GetTempPath(), "amc-migration-review-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "state.json");
            var store = new ConfigurationStore(path);
            var original = new PersistedState();
            original.Settings.LastSessionId = "radio";
            original.Spotify.ClientId = "synthetic-existing-client";
            original.Bookmarks.Entries.Add(new BookmarkEntry
            {
                Id = "probe-bookmark", SessionId = "radio", ItemId = "probe-station",
                Name = "Zakladka kontrolna", PositionTicks = 12300000, CreatedUtcTicks = 1
            });
            store.Save(original);
            var json = File.ReadAllText(path);
            foreach (var invalid in new[] { "{}", "[]", "{\"nested\":[{\"x\":1}]}", "[1,{\"x\":[2]}]", "true", "false", "null", "99", "\"unknown\"" })
            {
                var document = JsonNode.Parse(json)!.AsObject();
                document["settings"]!["spotifyEngine"] = JsonNode.Parse(invalid);
                File.WriteAllText(path, document.ToJsonString());
                var loaded = store.LoadOrCreate();
                Check(loaded.Settings.SpotifyEngine == SpotifyPlaybackEngine.Librespot, "Zla wartosc silnika nie daje Librespot: " + invalid);
                Check(loaded.Settings.LastSessionId == "radio" && loaded.Spotify.ClientId == original.Spotify.ClientId,
                    "Niepoprawny silnik skasowal pozostale ustawienia: " + invalid);
                Check(loaded.Bookmarks.Entries.Single().PositionTicks == 12300000,
                    "Niepoprawny silnik skasowal zakladke: " + invalid);
            }
            Console.WriteLine("OK: niepoprawny typ odtwarzacza nie przerywa odczytu danych");
        }
        finally { Directory.Delete(root, true); }
    }

    private static void VerifyIdsFromRealSessionCopies()
    {
        foreach (var canonicalPrefix in new[] { string.Empty, "spotify:" })
        {
            var first = new MediaItem { Id = canonicalPrefix + "82df077422294986a2e4d847d48b179e", ExternalId = "probe-a", Source = "spotify:track:probe-a", Title = "Proba A", IsInQueue = true };
            var second = new MediaItem { Id = canonicalPrefix + "cb9a3b841a0c42be9a0c08e915b98707", ExternalId = "probe-b", Source = "spotify:track:probe-b", Title = "Proba B", IsInQueue = true };
            var legacyFirst = SpotifySessionItemCopies.ForSession(first, "spotifyLibrespot");
            var legacySecond = SpotifySessionItemCopies.ForSession(second, "spotifyLibrespot");
            legacyFirst.IsInQueue = legacySecond.IsInQueue = true;
            Check(SpotifySessionMigration.CanonicalItemId(legacyFirst.Id) == first.Id,
                "Migracja nie odwraca rzeczywistej kopii elementu: " + legacyFirst.Id);
            var state = new PersistedState();
            state.Settings.LastSessionId = "spotifyLibrespot";
            state.Spotify.CachedCollectionItems.AddRange(new[] { first, second }.Select(TidalCachedCollectionItemSettings.FromMediaItem));
            state.RemoteQueues.ItemsBySession["spotify"] = [RemoteQueueItemSettings.FromMediaItem("spotify", first)];
            state.RemoteQueues.ItemsBySession["spotifyLibrespot"] = new[] { legacyFirst, legacySecond }
                .Select(item => RemoteQueueItemSettings.FromMediaItem("spotifyLibrespot", item)).ToList();
            state.CollectionOrders.QueueItemIdsBySession["spotifyLibrespot"] = [legacySecond.Id, legacyFirst.Id];
            state.CollectionOrders.QueueRegularItemIdsBySession["spotifyLibrespot"] = [legacyFirst.Id, legacySecond.Id];
            state.CollectionOrders.LibraryItemIdsBySession["spotifyLibrespot"] = [legacySecond.Id];
            state.Bookmarks.Entries.Add(new BookmarkEntry { SessionId = "spotifyLibrespot", ItemId = legacySecond.Id, Name = "Proba" });
            state.Playlists.Entries.Add(new PlaylistEntry { SessionId = "spotifyLibrespot", ItemIds = [legacySecond.Id] });
            state.SessionNavigation.Sessions["spotifyLibrespot"] = new SessionNavigationState
            {
                SelectedItemIds = { ["Biblioteka"] = legacySecond.Id },
                PlaybackContextItemIds = [legacySecond.Id]
            };
            SpotifySessionMigration.Apply(state);
            Check(state.RemoteQueues.ItemsBySession["spotify"].Select(item => item.Id).ToHashSet().SetEquals(new[] { first.Id, second.Id }),
                "Scalona kolejka nie uzywa identyfikatorow rzeczywistego katalogu.");
            Check(state.Bookmarks.Entries.Single().ItemId == second.Id && state.Playlists.Entries.Single().ItemIds.Single() == second.Id,
                "Zakladka albo playlista wskazuje nieistniejacy element.");
            Check(state.SessionNavigation.Sessions["spotify"].SelectedItemIds["Biblioteka"] == second.Id,
                "Zapamietane zaznaczenie wskazuje nieistniejacy element.");
            var liveItems = state.Spotify.CachedCollectionItems.Select(item => item.ToMediaItem()).ToArray();
            TransientQueuePersistence.Restore("spotify", liveItems,
                state.CollectionOrders.QueueItemIdsBySession["spotify"],
                state.CollectionOrders.QueueRegularItemIdsBySession["spotify"], []);
            Check(liveItems.All(item => item.IsInQueue), "Rzeczywiste przywracanie kolejki nie odnajduje elementow po migracji.");
            var noRemoteIdentity = new MediaItem { Id = "raw-without-remote-id", Title = "Proba bez zewnetrznego ID" };
            var noRemoteCopy = SpotifySessionItemCopies.ForSession(noRemoteIdentity, "spotifyLibrespot");
            Check(SpotifySessionMigration.QueueIdentity(RemoteQueueItemSettings.FromMediaItem("spotify", noRemoteIdentity))
                == SpotifySessionMigration.QueueIdentity(RemoteQueueItemSettings.FromMediaItem("spotifyLibrespot", noRemoteCopy)),
                "Brak zewnetrznego ID zmienia tozsamosc kopii kolejki.");
        }
        Console.WriteLine("OK: kopie, kolejki, zaznaczenie, zakladki i playlisty wskazuja rzeczywiste ID katalogu");
    }

    private static void VerifyPresetArchiveIsIndependent()
    {
        var state = new PersistedState();
        const string legacyId = "spotifyLibrespot:preset-item";
        state.SessionPresets.EntriesBySession["spotifyLibrespot"] =
        [new SessionPresetEntry { Slot = 2, TargetId = legacyId, TargetKind = "Track", TargetTitle = "Przed migracja", TargetLocation = "spotify:track:probe-preset" }];
        SpotifySessionMigration.Apply(state);
        var archive = state.Settings.SpotifyLegacySession!.Presets.Single();
        var live = state.SessionPresets.EntriesBySession["spotify"].Single();
        Check(archive.TargetId == legacyId, "Archiwum presetu nie zachowuje oryginalnego identyfikatora.");
        Check(!ReferenceEquals(archive, live), "Archiwum i biezacy preset wspoldziela obiekt.");
        live.TargetTitle = "Zmieniony po migracji";
        Check(archive.TargetTitle == "Przed migracja" && archive.Slot == 2
            && archive.TargetKind == "Track" && archive.TargetLocation == "spotify:track:probe-preset",
            "Zmiana presetu uszkodzila archiwum albo kopia zgubila metadane.");
        Console.WriteLine("OK: archiwum presetu pozostaje oryginalem niezaleznym od biezacej sesji");
    }

    private static void VerifyReferencesFollowMergedIdentity()
    {
        // The same service item can have different runtime IDs in a search,
        // a saved library and the other engine's queue.
        foreach (var mode in new[] { "existing-queue", "catalog-only", "legacy-only" })
        {
            var canonical = new MediaItem { Id = "catalog-id", ExternalId = "same-service-id", Title = "Proba", Source = "spotify:track:same-service-id" };
            var legacy = new[] { "search-id-one", "search-id-two" }.Select(id =>
            {
                var item = SpotifySessionItemCopies.ForSession(new MediaItem
                { Id = id, ExternalId = canonical.ExternalId, Title = "Proba", Source = canonical.Source }, "spotifyLibrespot");
                item.IsInQueue = true;
                return item;
            }).ToArray();
            var state = new PersistedState();
            if (mode != "legacy-only") state.Spotify.CachedCollectionItems.Add(TidalCachedCollectionItemSettings.FromMediaItem(canonical));
            if (mode == "existing-queue") state.RemoteQueues.ItemsBySession["spotify"] = [RemoteQueueItemSettings.FromMediaItem("spotify", canonical)];
            state.RemoteQueues.ItemsBySession["spotifyLibrespot"] = legacy.Select(item => RemoteQueueItemSettings.FromMediaItem("spotifyLibrespot", item)).ToList();
            state.Bookmarks.Entries.Add(new BookmarkEntry { SessionId = "spotifyLibrespot", ItemId = legacy[1].Id, Name = "Proba" });
            state.Playlists.Entries.Add(new PlaylistEntry { SessionId = "spotifyLibrespot", ItemIds = [legacy[1].Id] });
            state.SessionPresets.EntriesBySession["spotifyLibrespot"] = [new SessionPresetEntry { Slot = 2, TargetId = legacy[1].Id, TargetKind = "Track" }];
            state.SessionNavigation.Sessions["spotifyLibrespot"] = new SessionNavigationState { SelectedItemIds = { ["Kolejka"] = legacy[1].Id } };
            state.PlaybackHistory.ItemIdsBySession["spotifyLibrespot"] = [legacy[1].Id];
            state.PlaybackVolumes.Entries.Add(new PlaybackVolumeMemoryEntry { SessionId = "spotifyLibrespot", ContextId = legacy[1].Id, Volume = 30 });
            state.CollectionOrders.QueueItemIdsBySession["spotifyLibrespot"] = legacy.Select(item => item.Id).ToList();
            state.CollectionOrders.QueueRegularItemIdsBySession["spotifyLibrespot"] = legacy.Select(item => item.Id).ToList();
            SpotifySessionMigration.Apply(state);
            var expected = mode == "legacy-only" ? "search-id-one" : canonical.Id;
            var queued = state.RemoteQueues.ItemsBySession["spotify"].Single();
            Check(queued.Id == expected, "Scalanie nie wybralo istniejacego reprezentanta katalogu: " + mode);
            Check(state.Bookmarks.Entries.Single().ItemId == expected, "Zakladka wskazuje odrzucony duplikat po scaleniu kolejki: " + mode);
            Check(state.Playlists.Entries.Single().ItemIds.Single() == expected
                && state.SessionPresets.EntriesBySession["spotify"].Single().TargetId == expected
                && state.SessionNavigation.Sessions["spotify"].SelectedItemIds["Kolejka"] == expected
                && state.PlaybackHistory.ItemIdsBySession["spotify"].Single() == expected
                && state.PlaybackVolumes.Entries.Single().ContextId == expected,
                "Referencje nie podazaja za tozsamoscia scalonej pozycji: " + mode);
            var restored = new[] { queued.ToMediaItem() };
            TransientQueuePersistence.Restore("spotify", restored,
                state.CollectionOrders.QueueItemIdsBySession["spotify"], state.CollectionOrders.QueueRegularItemIdsBySession["spotify"], []);
            Check(restored.Single().IsInQueue, "Przywrocenie nie odnajduje scalonego reprezentanta: " + mode);
        }
        Console.WriteLine("OK: referencje ida za reprezentantem kolejki o tej samej tozsamosci Spotify");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
