using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Sessions;

namespace AccessibleMediaController.Core.Spotify;

/// <summary>
/// Scala DWIE osobne sesje Spotify ("spotify" dla Web Playback SDK i
/// "spotifyLibrespot" dla wbudowanego hosta) w JEDNA kanoniczna sesje "spotify",
/// w ktorej silnik odtwarzania jest ustawieniem, a nie osobna sesja.
///
/// TRZY WLASNOSCI, ktore migracja MUSI miec, bo kazda z nich osobno potrafi
/// zabrac uzytkownikowi dane:
///
/// 1. NIEUSUWAJACA. Zrodlowe dane starej sesji ida w calosci do
///    <see cref="AppSettings.SpotifyLegacySession"/>. Nawet to, co scalenie
///    odrzucilo jako konflikt, daje sie potem odczytac. Poswiadczen i cache
///    biblioteki ta klasa NIE DOTYKA - siedza poza stanem (DPAPI / Credential
///    Manager) albo w sekcji konta, ktorej nie czytamy.
/// 2. WERSJONOWANA i IDEMPOTENTNA.
///    <see cref="AppSettings.SpotifySessionUnificationVersion"/> rowne
///    <see cref="Version"/> zatrzymuje kolejne wywolanie natychmiast. Bez tego
///    drugie uruchomienie zdublowalo by kolejke i nadpisalo prefsy archiwum
///    danymi, ktore samo wczesniej przeniosla.
/// 3. TOZSAMOSCIOWA, nie pozycyjna. Elementy scalamy po STABILNYM kluczu
///    uslugi (adres "spotify:track:...", nie losowe MediaItem.Id, ktore zmienia
///    sie przy kazdym pobraniu biblioteki). Identyfikatory kopii Librespot
///    ("spotifyLibrespot:&lt;id&gt;") przepisujemy na kanoniczny prefiks.
///
/// DETERMINISTYCZNA REGULA KONFLIKTU (ta sama dla pozycji, trybow pamieci
/// pozycji, wyciszenia, wyjscia audio, odstepstw dzwieku, widoku i sortowan):
///
///   a) Jesli obie sesje maja wpis dla tego samego stabilnego klucza, wygrywa
///      wpis sesji, ktora byla OSTATNIO AKTYWNA (Settings.LastSessionId).
///   b) Gdy zadna z dwoch nie byla ostatnia aktywna, wygrywa LIBRESPOT - to on
///      zostaje domyslnym silnikiem, wiec jego pozycje sa tymi, ktore
///      uzytkownik naprawde slyszal.
///   c) Wpisy bezkonfliktowe (obecne tylko w jednej sesji) przechodza ZAWSZE,
///      niezaleznie od tego, ktora sesja wygrala rozstrzyganie.
///   d) Przegrana wartosc nie ginie: ladauje w
///      <see cref="SpotifyLegacySessionArchive.ConflictNotes"/> razem z pelna
///      kopia zrodla w archiwum.
///
/// Reguly (a)-(d) nie zaleza od kolejnosci iteracji slownikow ani od zegara,
/// wiec dwa uruchomienia na tym samym pliku daja ten sam wynik.
///
/// Numery slotow innych sesji zostaja NIETKNIETE. Slot zwolniony przez
/// Librespot jest po prostu usuwany; <see cref="SessionSlotOrder.Normalize"/>
/// przy nastepnym odczycie domknie numeracje, nie przestawiajac sesji 1-6.
/// </summary>
public static class SpotifySessionMigration
{
    public const int Version = 1;
    public const string CanonicalSessionId = SpotifyPlaybackSettingsResolver.SessionId;
    public const string LegacySessionId = SpotifyPlaybackSettingsResolver.LibrespotSessionId;

    private const string CanonicalPrefix = CanonicalSessionId + ":";
    private const string LegacyPrefix = LegacySessionId + ":";

    public sealed record Result(
        bool Applied,
        int MergedQueueItems,
        int MergedPositions,
        int Conflicts,
        IReadOnlyList<string> ConflictNotes)
    {
        public static readonly Result AlreadyDone =
            new(false, 0, 0, 0, Array.Empty<string>());
    }

    /// <summary>
    /// Wykonuje scalenie na podanym stanie. Wolac PRZED zbudowaniem okna
    /// glownego: po utworzeniu sesji przez <see cref="SessionManager"/> zmiana
    /// identyfikatorow nie dotarlaby do zywych obiektow sesji.
    /// </summary>
    public static Result Apply(PersistedState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        state.Settings ??= new AppSettings();
        var settings = state.Settings;

        // Silnik normalizujemy ZAWSZE, takze przy juz wykonanej migracji.
        // Nieznana wartosc moze trafic do pliku pozniej - recznie albo z
        // nowszego wydania - i nie wolno jej wpuscic do odtwarzacza.
        settings.SpotifyEngine = SpotifyPlaybackEngineRules.Normalize(settings.SpotifyEngine);

        if (settings.SpotifySessionUnificationVersion >= Version) return Result.AlreadyDone;

        var legacySlot = settings.SessionSlots?
            .FirstOrDefault(pair => string.Equals(pair.Value, LegacySessionId, StringComparison.OrdinalIgnoreCase))
            .Key ?? 0;
        var legacyWasLast = string.Equals(settings.LastSessionId, LegacySessionId, StringComparison.OrdinalIgnoreCase);
        var canonicalWasLast = string.Equals(settings.LastSessionId, CanonicalSessionId, StringComparison.OrdinalIgnoreCase);
        // Regula (a) i (b): bez ostatniej aktywnej sesji Spotify wygrywa Librespot.
        var legacyWins = legacyWasLast || !canonicalWasLast;

        var archive = new SpotifyLegacySessionArchive
        {
            SessionId = LegacySessionId,
            Slot = legacySlot,
            ArchivedUtcTicks = DateTime.UtcNow.Ticks,
            WasLastSession = legacyWasLast,
            DeviceName = settings.SpotifyLibrespotDeviceName
        };
        var notes = new List<string>();

        var mergedPositions = ArchiveAndMergePlayback(settings, archive, notes, legacyWins);
        ArchiveAndMergeSessionScalars(settings, archive, notes, legacyWins);
        var mergedQueue = ArchiveAndMergeRemoteQueue(state, archive, notes, legacyWins);
        ArchiveAndMergeNavigation(state, archive, notes, legacyWins);
        ArchiveAndMergeCollectionOrders(state, archive, notes);
        ArchiveAndMergeHistories(state, archive);
        ArchiveAndMergeSessionScopedLists(state, archive);
        ReleaseLegacySlot(settings, archive);

        archive.ConflictNotes = notes;
        settings.SpotifyLegacySession = archive;
        settings.SpotifySessionUnificationVersion = Version;
        return new Result(true, mergedQueue, mergedPositions, notes.Count, notes);
    }

    /// <summary>
    /// Przepisuje identyfikator elementu skopiowanego do sesji Librespot na
    /// kanoniczny prefiks Spotify. Identyfikatory bez prefiksu zostaja bez zmian:
    /// element wspolny obu sesjom ma ten sam klucz uslugi w Source/ExternalId.
    /// </summary>
    public static string CanonicalItemId(string? itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return string.Empty;
        return itemId.StartsWith(LegacyPrefix, StringComparison.Ordinal)
            ? CanonicalPrefix + itemId[LegacyPrefix.Length..]
            : itemId;
    }

    /// <summary>
    /// Stabilna tozsamosc wiersza kolejki: adres uslugi albo rodzaj+identyfikator
    /// zewnetrzny. Dopiero bez obu spada na (znormalizowany) identyfikator lokalny.
    /// Ta sama pozycja dodana do kolejki w obu sesjach ma tu JEDEN klucz - stad
    /// scalenie nie robi z niej dwoch wierszy.
    /// </summary>
    public static string QueueIdentity(RemoteQueueItemSettings item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var key = SpotifyPlaybackSettingsResolver.StorageKey(item.ToMediaItem());
        if (key.Length > 0 && key.StartsWith("spotify:", StringComparison.Ordinal)) return key;
        return CanonicalItemId(item.Id);
    }

    private static int ArchiveAndMergePlayback(
        AppSettings settings,
        SpotifyLegacySessionArchive archive,
        List<string> notes,
        bool legacyWins)
    {
        var legacy = settings.SpotifyLibrespotPlayback ?? new SpotifyPlaybackSettings();
        var canonical = settings.SpotifyPlayback ??= new SpotifyPlaybackSettings();
        archive.Playback = ClonePlayback(legacy);

        var merged = 0;
        foreach (var pair in legacy.ItemsByKey.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            if (!canonical.ItemsByKey.TryGetValue(pair.Key, out var existing))
            {
                // Regula (c): bezkonfliktowe wnosimy zawsze.
                canonical.ItemsByKey[pair.Key] = CloneItem(pair.Value);
                merged++;
                continue;
            }

            if (Equals(existing.PositionTicks, pair.Value.PositionTicks)
                && existing.ResumePositionMode == pair.Value.ResumePositionMode)
            {
                continue;
            }

            if (legacyWins)
            {
                notes.Add($"Pozycja i tryb pamieci dla {pair.Key}: zachowano wartosc Librespot "
                          + $"({pair.Value.PositionTicks} tickow, {pair.Value.ResumePositionMode}); "
                          + $"wartosc SDK ({existing.PositionTicks} tickow, {existing.ResumePositionMode}) "
                          + "jest w archiwum.");
                canonical.ItemsByKey[pair.Key] = CloneItem(pair.Value);
                merged++;
            }
            else
            {
                notes.Add($"Pozycja i tryb pamieci dla {pair.Key}: zachowano wartosc SDK "
                          + $"({existing.PositionTicks} tickow, {existing.ResumePositionMode}); "
                          + $"wartosc Librespot ({pair.Value.PositionTicks} tickow, {pair.Value.ResumePositionMode}) "
                          + "jest w archiwum.");
            }
        }

        foreach (var pair in legacy.ContainersByKey.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            if (!canonical.ContainersByKey.TryGetValue(pair.Key, out var existing))
            {
                canonical.ContainersByKey[pair.Key] = pair.Value;
                continue;
            }
            if (existing == pair.Value) continue;
            if (legacyWins)
            {
                notes.Add($"Tryb pamieci pojemnika {pair.Key}: zachowano {pair.Value} z Librespot, "
                          + $"{existing} z SDK jest w archiwum.");
                canonical.ContainersByKey[pair.Key] = pair.Value;
            }
            else
            {
                notes.Add($"Tryb pamieci pojemnika {pair.Key}: zachowano {existing} z SDK, "
                          + $"{pair.Value} z Librespot jest w archiwum.");
            }
        }

        // Profil starej sesji opustoszal, bo jego tresc jest w archiwum i w
        // sesji kanonicznej. Wlasnosci NIE usuwamy: stare wydanie czytajace ten
        // plik dostalo by wyjatek zamiast pustej sekcji.
        settings.SpotifyLibrespotPlayback = new SpotifyPlaybackSettings();
        return merged;
    }

    private static void ArchiveAndMergeSessionScalars(
        AppSettings settings,
        SpotifyLegacySessionArchive archive,
        List<string> notes,
        bool legacyWins)
    {
        settings.ResumePositionModeBySession ??= new Dictionary<string, ResumePositionMode>(StringComparer.OrdinalIgnoreCase);
        if (settings.ResumePositionModeBySession.TryGetValue(LegacySessionId, out var legacyMode))
        {
            archive.SessionResumePositionMode = legacyMode;
            settings.ResumePositionModeBySession.Remove(LegacySessionId);
            var hasCanonical = settings.ResumePositionModeBySession.TryGetValue(CanonicalSessionId, out var canonicalMode);
            if (!hasCanonical)
            {
                settings.ResumePositionModeBySession[CanonicalSessionId] = legacyMode;
            }
            else if (canonicalMode != legacyMode)
            {
                if (legacyWins)
                {
                    notes.Add($"Pamiec pozycji calej sesji: zachowano {legacyMode} z Librespot, {canonicalMode} z SDK jest w archiwum.");
                    settings.ResumePositionModeBySession[CanonicalSessionId] = legacyMode;
                }
                else
                {
                    notes.Add($"Pamiec pozycji calej sesji: zachowano {canonicalMode} z SDK, {legacyMode} z Librespot jest w archiwum.");
                }
            }
        }

        var audio = settings.Audio ??= new PlaybackAudioSettings();
        audio.SessionMutedById ??= new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        audio.OutputDeviceIdsBySession ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        audio.OverridesBySession ??= new Dictionary<string, SessionPlaybackAudioOverrides>(StringComparer.OrdinalIgnoreCase);

        if (audio.SessionMutedById.TryGetValue(LegacySessionId, out var legacyMuted))
        {
            archive.Muted = legacyMuted;
            audio.SessionMutedById.Remove(LegacySessionId);
            var hasCanonical = audio.SessionMutedById.TryGetValue(CanonicalSessionId, out var canonicalMuted);
            if (!hasCanonical) audio.SessionMutedById[CanonicalSessionId] = legacyMuted;
            else if (canonicalMuted != legacyMuted)
            {
                notes.Add(legacyWins
                    ? "Wyciszenie sesji: zachowano stan Librespot, stan SDK jest w archiwum."
                    : "Wyciszenie sesji: zachowano stan SDK, stan Librespot jest w archiwum.");
                if (legacyWins) audio.SessionMutedById[CanonicalSessionId] = legacyMuted;
            }
        }

        if (audio.OutputDeviceIdsBySession.TryGetValue(LegacySessionId, out var legacyDevice))
        {
            archive.OutputDeviceId = legacyDevice;
            audio.OutputDeviceIdsBySession.Remove(LegacySessionId);
            var hasCanonical = audio.OutputDeviceIdsBySession.TryGetValue(CanonicalSessionId, out var canonicalDevice);
            if (!hasCanonical) audio.OutputDeviceIdsBySession[CanonicalSessionId] = legacyDevice;
            else if (!string.Equals(canonicalDevice, legacyDevice, StringComparison.Ordinal))
            {
                notes.Add(legacyWins
                    ? "Wyjscie audio sesji: zachowano urzadzenie Librespot, urzadzenie SDK jest w archiwum."
                    : "Wyjscie audio sesji: zachowano urzadzenie SDK, urzadzenie Librespot jest w archiwum.");
                if (legacyWins) audio.OutputDeviceIdsBySession[CanonicalSessionId] = legacyDevice;
            }
        }

        if (audio.OverridesBySession.TryGetValue(LegacySessionId, out var legacyOverrides))
        {
            archive.AudioOverrides = legacyOverrides;
            audio.OverridesBySession.Remove(LegacySessionId);
            if (!audio.OverridesBySession.ContainsKey(CanonicalSessionId))
            {
                audio.OverridesBySession[CanonicalSessionId] = legacyOverrides;
            }
            else
            {
                notes.Add("Odstepstwa dzwieku sesji: zachowano wpis sesji kanonicznej, wpis Librespot jest w archiwum.");
            }
        }

        // Ostatnia sesja nie moze wskazywac identyfikatora, ktorego po scaleniu
        // nie ma. Program zaczalby wtedy od pierwszej sesji na liscie, co dla
        // uzytkownika czytnika wyglada jak zgubione ustawienie.
        if (string.Equals(settings.LastSessionId, LegacySessionId, StringComparison.OrdinalIgnoreCase))
        {
            settings.LastSessionId = CanonicalSessionId;
        }
    }

    private static int ArchiveAndMergeRemoteQueue(
        PersistedState state,
        SpotifyLegacySessionArchive archive,
        List<string> notes,
        bool legacyWins)
    {
        var queues = (state.RemoteQueues ??= new RemoteQueueCacheSettings());
        queues.ItemsBySession ??= new Dictionary<string, List<RemoteQueueItemSettings>>(StringComparer.OrdinalIgnoreCase);
        if (!queues.ItemsBySession.TryGetValue(LegacySessionId, out var legacyQueue) || legacyQueue is null)
        {
            return 0;
        }

        archive.RemoteQueue = legacyQueue.Select(CloneQueueItem).ToList();
        queues.ItemsBySession.Remove(LegacySessionId);

        var canonicalQueue = queues.ItemsBySession.TryGetValue(CanonicalSessionId, out var existing) && existing is not null
            ? existing
            : queues.ItemsBySession[CanonicalSessionId] = [];

        var byIdentity = new Dictionary<string, RemoteQueueItemSettings>(StringComparer.Ordinal);
        foreach (var item in canonicalQueue)
        {
            byIdentity.TryAdd(QueueIdentity(item), item);
        }

        var merged = 0;
        foreach (var legacyItem in legacyQueue)
        {
            var identity = QueueIdentity(legacyItem);
            if (!byIdentity.TryGetValue(identity, out var canonicalItem))
            {
                // Kolejnosc: najpierw dotychczasowa kolejka sesji kanonicznej,
                // potem doklejone wiersze Librespot w ich wlasnej kolejnosci.
                // Deterministyczna i niezalezna od iteracji slownika.
                var copy = CloneQueueItem(legacyItem);
                copy.Id = CanonicalItemId(copy.Id);
                canonicalQueue.Add(copy);
                byIdentity[identity] = copy;
                merged++;
                continue;
            }

            var sameFlags = canonicalItem.IsInQueue == legacyItem.IsInQueue
                            && canonicalItem.IsPlayNext == legacyItem.IsPlayNext;
            if (sameFlags) continue;

            if (legacyWins)
            {
                notes.Add($"Flagi kolejki dla {identity}: zachowano ustawienie Librespot "
                          + $"(kolejka={legacyItem.IsInQueue}, nastepne={legacyItem.IsPlayNext}); "
                          + "ustawienie SDK jest w archiwum.");
                canonicalItem.IsInQueue = legacyItem.IsInQueue;
                canonicalItem.IsPlayNext = legacyItem.IsPlayNext;
            }
            else
            {
                notes.Add($"Flagi kolejki dla {identity}: zachowano ustawienie SDK "
                          + $"(kolejka={canonicalItem.IsInQueue}, nastepne={canonicalItem.IsPlayNext}); "
                          + "ustawienie Librespot jest w archiwum.");
            }
        }

        return merged;
    }

    private static void ArchiveAndMergeNavigation(
        PersistedState state,
        SpotifyLegacySessionArchive archive,
        List<string> notes,
        bool legacyWins)
    {
        var navigation = (state.SessionNavigation ??= new SessionNavigationSettings());
        navigation.Sessions ??= new Dictionary<string, SessionNavigationState>(StringComparer.OrdinalIgnoreCase);
        if (!navigation.Sessions.TryGetValue(LegacySessionId, out var legacy) || legacy is null) return;

        archive.Navigation = legacy;
        navigation.Sessions.Remove(LegacySessionId);

        if (!navigation.Sessions.TryGetValue(CanonicalSessionId, out var canonical) || canonical is null)
        {
            navigation.Sessions[CanonicalSessionId] = RemapNavigation(legacy);
            return;
        }

        // Widok i sortowania: bezkonfliktowe wnosimy, konfliktowe rozstrzyga
        // ta sama regula pierwszenstwa co pozycje.
        foreach (var pair in legacy.CollectionSortModes.OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (!canonical.CollectionSortModes.TryGetValue(pair.Key, out var existingSort))
            {
                canonical.CollectionSortModes[pair.Key] = pair.Value;
                continue;
            }
            if (existingSort == pair.Value) continue;
            if (legacyWins)
            {
                notes.Add($"Sortowanie listy {pair.Key}: zachowano {pair.Value} z Librespot, {existingSort} z SDK jest w archiwum.");
                canonical.CollectionSortModes[pair.Key] = pair.Value;
            }
            else
            {
                notes.Add($"Sortowanie listy {pair.Key}: zachowano {existingSort} z SDK, {pair.Value} z Librespot jest w archiwum.");
            }
        }

        foreach (var pair in legacy.Filters.OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (!canonical.Filters.ContainsKey(pair.Key)) canonical.Filters[pair.Key] = pair.Value;
        }

        foreach (var pair in legacy.SelectedItemIds.OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (!canonical.SelectedItemIds.ContainsKey(pair.Key))
            {
                canonical.SelectedItemIds[pair.Key] = CanonicalItemIdOrNull(pair.Value);
            }
        }

        if (canonical.PlaybackContextItemIds.Count == 0 && legacy.PlaybackContextItemIds.Count > 0)
        {
            canonical.PlaybackContextItemIds = legacy.PlaybackContextItemIds
                .Select(CanonicalItemId)
                .Where(id => id.Length > 0)
                .ToList();
            canonical.PlaybackContextView = legacy.PlaybackContextView;
        }
    }

    private static void ArchiveAndMergeCollectionOrders(
        PersistedState state,
        SpotifyLegacySessionArchive archive,
        List<string> notes)
    {
        var orders = state.CollectionOrders ??= new CollectionOrderSettings();
        var maps = new (string Name, Dictionary<string, List<string>> Map)[]
        {
            ("favoriteAdded", orders.FavoriteAddedItemIdsBySession ??= new(StringComparer.OrdinalIgnoreCase)),
            ("favorite", orders.FavoriteItemIdsBySession ??= new(StringComparer.OrdinalIgnoreCase)),
            ("libraryAdded", orders.LibraryAddedItemIdsBySession ??= new(StringComparer.OrdinalIgnoreCase)),
            ("library", orders.LibraryItemIdsBySession ??= new(StringComparer.OrdinalIgnoreCase)),
            ("queue", orders.QueueItemIdsBySession ??= new(StringComparer.OrdinalIgnoreCase)),
            ("queueRegular", orders.QueueRegularItemIdsBySession ??= new(StringComparer.OrdinalIgnoreCase)),
            ("queuePlayNext", orders.QueuePlayNextItemIdsBySession ??= new(StringComparer.OrdinalIgnoreCase))
        };

        foreach (var (name, map) in maps)
        {
            if (!map.TryGetValue(LegacySessionId, out var legacyOrder) || legacyOrder is null) continue;
            archive.CollectionOrders[name] = [.. legacyOrder];
            map.Remove(LegacySessionId);

            var canonicalOrder = map.TryGetValue(CanonicalSessionId, out var existing) && existing is not null
                ? existing
                : map[CanonicalSessionId] = [];
            var known = canonicalOrder.ToHashSet(StringComparer.Ordinal);
            var appended = 0;
            foreach (var id in legacyOrder)
            {
                var canonicalId = CanonicalItemId(id);
                if (canonicalId.Length == 0 || !known.Add(canonicalId)) continue;
                // Kolejnosc uzytkownika w sesji kanonicznej zostaje; wiersze
                // znane tylko Librespotowi ida na koniec, nie przeplataja sie.
                canonicalOrder.Add(canonicalId);
                appended++;
            }
            if (appended > 0)
            {
                notes.Add($"Lista {name}: dopisano {appended} pozycji z Librespot na koncu kolejnosci sesji Spotify.");
            }
        }
    }

    private static void ArchiveAndMergeHistories(PersistedState state, SpotifyLegacySessionArchive archive)
    {
        var playback = state.PlaybackHistory ??= new PlaybackHistorySettings();
        playback.ItemIdsBySession ??= new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        if (playback.ItemIdsBySession.TryGetValue(LegacySessionId, out var legacyHistory) && legacyHistory is not null)
        {
            archive.PlaybackHistoryItemIds = [.. legacyHistory];
            playback.ItemIdsBySession.Remove(LegacySessionId);
            var canonicalHistory = playback.ItemIdsBySession.TryGetValue(CanonicalSessionId, out var existing) && existing is not null
                ? existing
                : playback.ItemIdsBySession[CanonicalSessionId] = [];
            var known = canonicalHistory.ToHashSet(StringComparer.Ordinal);
            foreach (var id in legacyHistory)
            {
                var canonicalId = CanonicalItemId(id);
                if (canonicalId.Length > 0 && known.Add(canonicalId)) canonicalHistory.Add(canonicalId);
            }
        }

        var search = state.SearchHistory ??= new SearchHistorySettings();
        search.Entries ??= new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        if (search.Entries.TryGetValue(LegacySessionId, out var legacySearch) && legacySearch is not null)
        {
            archive.SearchHistory = [.. legacySearch];
            search.Entries.Remove(LegacySessionId);
            var canonicalSearch = search.Entries.TryGetValue(CanonicalSessionId, out var existing) && existing is not null
                ? existing
                : search.Entries[CanonicalSessionId] = [];
            foreach (var query in legacySearch)
            {
                if (!canonicalSearch.Contains(query, StringComparer.OrdinalIgnoreCase)) canonicalSearch.Add(query);
            }
        }
    }

    private static void ArchiveAndMergeSessionScopedLists(PersistedState state, SpotifyLegacySessionArchive archive)
    {
        var presets = state.SessionPresets ??= new SessionPresetSettings();
        presets.EntriesBySession ??= new Dictionary<string, List<SessionPresetEntry>>(StringComparer.OrdinalIgnoreCase);
        if (presets.EntriesBySession.TryGetValue(LegacySessionId, out var legacyPresets) && legacyPresets is not null)
        {
            archive.Presets = [.. legacyPresets];
            presets.EntriesBySession.Remove(LegacySessionId);
            var canonicalPresets = presets.EntriesBySession.TryGetValue(CanonicalSessionId, out var existing) && existing is not null
                ? existing
                : presets.EntriesBySession[CanonicalSessionId] = [];
            // Preset zajmuje numer klawisza. Zajety numer NIE jest nadpisywany -
            // nadpisanie zmienilo by znaczenie skrotu, ktory uzytkownik zna.
            var takenSlots = canonicalPresets.Select(entry => entry.Slot).ToHashSet();
            foreach (var preset in legacyPresets)
            {
                if (!takenSlots.Add(preset.Slot)) continue;
                preset.TargetId = CanonicalItemId(preset.TargetId);
                canonicalPresets.Add(preset);
            }
        }

        var playlists = state.Playlists ??= new PlaylistSettings();
        playlists.Entries ??= [];
        var legacyPlaylists = playlists.Entries
            .Where(entry => string.Equals(entry.SessionId, LegacySessionId, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (legacyPlaylists.Count > 0)
        {
            archive.Playlists = legacyPlaylists.Select(ClonePlaylist).ToList();
            foreach (var entry in legacyPlaylists)
            {
                entry.SessionId = CanonicalSessionId;
                entry.ItemIds = entry.ItemIds.Select(CanonicalItemId).Where(id => id.Length > 0).ToList();
            }
        }

        var bookmarks = state.Bookmarks ??= new BookmarkSettings();
        bookmarks.Entries ??= [];
        var legacyBookmarks = bookmarks.Entries
            .Where(entry => string.Equals(entry.SessionId, LegacySessionId, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (legacyBookmarks.Count > 0)
        {
            archive.Bookmarks = legacyBookmarks.Select(CloneBookmark).ToList();
            foreach (var entry in legacyBookmarks)
            {
                entry.SessionId = CanonicalSessionId;
                entry.SessionName = SessionSlotOrder.GetDisplayName(CanonicalSessionId);
                entry.ItemId = CanonicalItemId(entry.ItemId);
            }
        }

        var volumes = state.PlaybackVolumes ??= new PlaybackVolumeMemorySettings();
        volumes.Entries ??= [];
        var legacyVolumes = volumes.Entries
            .Where(entry => string.Equals(entry.SessionId, LegacySessionId, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (legacyVolumes.Count > 0)
        {
            archive.Volumes = legacyVolumes.Select(entry => new PlaybackVolumeMemoryEntry
            {
                SessionId = entry.SessionId,
                ContextId = entry.ContextId,
                OutputDeviceId = entry.OutputDeviceId,
                Volume = entry.Volume
            }).ToList();
            foreach (var entry in legacyVolumes)
            {
                var canonicalContext = CanonicalItemId(entry.ContextId);
                var alreadyThere = volumes.Entries.Any(other =>
                    string.Equals(other.SessionId, CanonicalSessionId, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(other.ContextId, canonicalContext, StringComparison.Ordinal)
                    && string.Equals(other.OutputDeviceId, entry.OutputDeviceId, StringComparison.Ordinal));
                if (alreadyThere)
                {
                    volumes.Entries.Remove(entry);
                    continue;
                }
                entry.SessionId = CanonicalSessionId;
                entry.ContextId = canonicalContext;
            }
        }
    }

    private static void ReleaseLegacySlot(AppSettings settings, SpotifyLegacySessionArchive archive)
    {
        if (settings.SessionSlots is null || archive.Slot == 0) return;
        // Tylko wpis Librespot. Numery pozostalych sesji zostaja dokladnie tam,
        // gdzie byly - skroty Alt+cyfra, ktore uzytkownik zna na pamiec.
        if (settings.SessionSlots.TryGetValue(archive.Slot, out var occupant)
            && string.Equals(occupant, LegacySessionId, StringComparison.OrdinalIgnoreCase))
        {
            settings.SessionSlots.Remove(archive.Slot);
        }
    }

    /// <summary>
    /// Kopia stanu nawigacji starej sesji z identyfikatorami przepisanymi na
    /// kanoniczne. Uzywana, gdy sesja kanoniczna nie ma wlasnego stanu - wtedy
    /// nie ma czego rozstrzygac i przejmujemy widok Librespot w calosci.
    /// </summary>
    private static SessionNavigationState RemapNavigation(SessionNavigationState source) => new()
    {
        CurrentView = source.CurrentView,
        LastLibraryView = source.LastLibraryView,
        SelectedItemIds = source.SelectedItemIds.ToDictionary(
            pair => pair.Key,
            pair => CanonicalItemIdOrNull(pair.Value),
            StringComparer.OrdinalIgnoreCase),
        Filters = new Dictionary<string, string>(source.Filters, StringComparer.OrdinalIgnoreCase),
        CollectionSortModes = new Dictionary<string, CollectionSortMode>(source.CollectionSortModes, StringComparer.OrdinalIgnoreCase),
        PlaybackContextView = source.PlaybackContextView,
        PlaybackContextItemIds = source.PlaybackContextItemIds
            .Select(CanonicalItemId)
            .Where(id => id.Length > 0)
            .ToList(),
        PlayerActive = source.PlayerActive
    };

    private static string? CanonicalItemIdOrNull(string? itemId) =>
        string.IsNullOrEmpty(itemId) ? itemId : CanonicalItemId(itemId);

    private static SpotifyPlaybackSettings ClonePlayback(SpotifyPlaybackSettings source) => new()
    {
        ItemsByKey = source.ItemsByKey.ToDictionary(pair => pair.Key, pair => CloneItem(pair.Value), StringComparer.Ordinal),
        ContainersByKey = new Dictionary<string, ResumePositionMode>(source.ContainersByKey, StringComparer.Ordinal)
    };

    private static SpotifyItemPlaybackSettings CloneItem(SpotifyItemPlaybackSettings source) => new()
    {
        ResumePositionMode = source.ResumePositionMode,
        PositionTicks = source.PositionTicks
    };

    private static PlaylistEntry ClonePlaylist(PlaylistEntry source) => new()
    {
        Id = source.Id,
        SessionId = source.SessionId,
        Name = source.Name,
        CreatedUtcTicks = source.CreatedUtcTicks,
        ItemIds = [.. source.ItemIds]
    };

    private static BookmarkEntry CloneBookmark(BookmarkEntry source) => new()
    {
        Id = source.Id,
        SessionId = source.SessionId,
        SessionName = source.SessionName,
        ItemId = source.ItemId,
        ItemTitle = source.ItemTitle,
        Name = source.Name,
        PositionTicks = source.PositionTicks,
        CreatedUtcTicks = source.CreatedUtcTicks,
        Purpose = source.Purpose,
        ChapterOrigin = source.ChapterOrigin,
        ChapterSourceId = source.ChapterSourceId
    };

    private static RemoteQueueItemSettings CloneQueueItem(RemoteQueueItemSettings source) => new()
    {
        Id = source.Id,
        ExternalId = source.ExternalId,
        Title = source.Title,
        Artist = source.Artist,
        Kind = source.Kind,
        DurationTicks = source.DurationTicks,
        BitrateKbps = source.BitrateKbps,
        SampleRateHz = source.SampleRateHz,
        PublicUri = source.PublicUri,
        HomepageUri = source.HomepageUri,
        Country = source.Country,
        Language = source.Language,
        Tags = source.Tags,
        Codec = source.Codec,
        RelatedAlbumExternalId = source.RelatedAlbumExternalId,
        RelatedAlbumTitle = source.RelatedAlbumTitle,
        RelatedArtistExternalId = source.RelatedArtistExternalId,
        RelatedArtistName = source.RelatedArtistName,
        IsInQueue = source.IsInQueue,
        IsPlayNext = source.IsPlayNext
    };
}
