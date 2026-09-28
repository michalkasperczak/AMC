using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AccessibleMediaController.Core.Commands;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Core.Sonos;

// SESJA Sonos: pamiec wybranego domu/grupy (BEZ tokenow), lista grup, formatowanie
// odtwarzacza z PRAWDZIWEGO odczytu, bramka dostepnosci polecen i werdykt po
// poleceniu. Wylacznie dane syntetyczne: zero sieci, konta, DPAPI, GUI i DPAPI.
internal static class SonosSessionPresentationTests
{
    private const string House = "Sonos_household.1234567890";
    private const string GroupA = "RINCON_00012345678001400:1";
    private const string GroupB = "RINCON_00099999999001400:7";

    internal static void Run()
    {
        SettingsRoundtripKeepsSelectionWithoutTokens();
        NormalizationRejectsUnusableIdentifiers();
        SnapshotIsDetached();
        SessionListHasSonosOnFreeSlotOnly();
        GroupRowsAndEmptyStates();
        PlayerTextFromRealRead();
        PositionNeverInvented();
        CommandGatingFromCapabilities();
        VerdictAfterExplicitRead();
        ActiveGroupFollowsIdentifier();
        Console.WriteLine("Sesja Sonos: pamiec wyboru, lista grup, odtwarzacz, bramka i werdykt - OK.");
    }

    // 1. PELNY roundtrip przez PRAWDZIWY ConfigurationStore. Zapis idzie "tylko
    // ustawieniami" (CreateSettingsOnlyState), wiec pominiecie Sonosa w powloce
    // po cichu gubiloby wybor domu i grupy.
    private static void SettingsRoundtripKeepsSelectionWithoutTokens()
    {
        var folder = NewFolder();
        try
        {
            var statePath = Path.Combine(folder, "state.json");
            var store = NewStore(statePath);
            var state = ConfigurationStore.CreateDefaultState();
            state.Sonos.SelectedHouseholdId = House;
            state.Sonos.SelectedGroupId = GroupA;
            store.Save(state);

            var text = File.ReadAllText(statePath);
            True(text.Contains(House, StringComparison.Ordinal), "Zapis ustawien powinien pamietac wybrany dom Sonos.");
            True(!text.Contains("accessToken", StringComparison.OrdinalIgnoreCase)
                && !text.Contains("refreshToken", StringComparison.OrdinalIgnoreCase),
                "Ustawienia sesji Sonos nie moga zawierac zadnych tokenow.");

            var reloaded = NewStore(statePath).LoadOrCreate();
            Equal(House, reloaded.Sonos.SelectedHouseholdId);
            Equal(GroupA, reloaded.Sonos.SelectedGroupId);
        }
        finally
        {
            TryDelete(folder);
        }
    }

    // 2. Normalizacja: identyfikatory NIE do uzycia w sciezce API nie moga
    // zostac w stanie, a biale znaki sa obcinane.
    private static void NormalizationRejectsUnusableIdentifiers()
    {
        var folder = NewFolder();
        try
        {
            var statePath = Path.Combine(folder, "state.json");
            var state = ConfigurationStore.CreateDefaultState();
            state.Sonos.SelectedHouseholdId = "  " + House + "  ";
            state.Sonos.SelectedGroupId = "RINCON/../../admin";
            NewStore(statePath).Save(state);

            var reloaded = NewStore(statePath).LoadOrCreate();
            Equal(House, reloaded.Sonos.SelectedHouseholdId);
            True(reloaded.Sonos.SelectedGroupId is null,
                "Grupa o niedopuszczalnym identyfikatorze nie moze zostac zapamietana.");
        }
        finally
        {
            TryDelete(folder);
        }
    }

    // 3. Migawka stanu musi byc ODLACZONA takze dla nowej sekcji.
    private static void SnapshotIsDetached()
    {
        var folder = NewFolder();
        try
        {
            var store = NewStore(Path.Combine(folder, "state.json"));
            var state = ConfigurationStore.CreateDefaultState();
            state.Sonos.SelectedGroupId = GroupA;
            var snapshot = store.CloneState(state);
            state.Sonos.SelectedGroupId = GroupB;
            Equal(GroupA, snapshot.Sonos.SelectedGroupId);
        }
        finally
        {
            TryDelete(folder);
        }
    }

    // 4. Sesja Sonos jest w liscie sesji i NIE zajmuje numeru innej sesji.
    private static void SessionListHasSonosOnFreeSlotOnly()
    {
        var settings = new AppSettings();
        var manager = new SessionManager(settings);
        var sonos = manager.FindSession("sonos");
        True(sonos is not null, "Lista sesji powinna zawierac sesje Sonos.");
        Equal("Sonos", sonos!.DisplayName);
        True(sonos.Items.Count == 0, "Sesja Sonos nie ma miec elementow demonstracyjnych.");

        var slots = SessionSlotOrder.Normalize(new Dictionary<int, string>
        {
            [1] = "local",
            [2] = "wiim",
            [3] = "tidal",
            [4] = "appleMusic",
            [5] = "radio",
            [6] = "podcasts",
            [7] = "spotify"
        });
        Equal("wiim", slots[2]);
        Equal("spotify", slots[7]);
        True(slots.ContainsValue("sonos"), "Sonos powinien dostac wlasny numer sesji.");
        var sonosSlot = slots.First(pair => pair.Value == "sonos").Key;
        True(sonosSlot >= 8, "Sonos nie moze zajac numeru uzywanego przez inna sesje.");
    }

    // 5. Lista GRUP (nie odtwarzalne demo) i czytelne stany puste z droga do konta.
    private static void GroupRowsAndEmptyStates()
    {
        var topology = new SonosHouseholdTopology(
            [
                new SonosGroup(GroupA, "Salon + Kuchnia", "P1", ["P1", "P2"], SonosPlaybackState.Playing),
                new SonosGroup(GroupB, "Sypialnia", "P3", ["P3"], SonosPlaybackState.Idle)
            ],
            [],
            partial: false);

        var rows = SonosSessionListPresentation.DescribeGroups(topology);
        Equal(2, rows.Count);
        Equal(GroupA, rows[0].GroupId);
        True(rows[0].Text.Contains("Salon + Kuchnia", StringComparison.Ordinal)
            && rows[0].Text.Contains("2", StringComparison.Ordinal),
            "Wiersz grupy podaje nazwe i liczbe glosnikow: " + rows[0].Text);
        True(rows[0].Text.Contains("odtwarza", StringComparison.OrdinalIgnoreCase),
            "Wiersz grupy podaje stan odtwarzania: " + rows[0].Text);
        True(!rows[1].Text.Contains("RINCON", StringComparison.Ordinal),
            "Wiersz listy nie pokazuje surowego identyfikatora.");

        var noAccount = SonosSessionListPresentation.DescribeEmptyState(SonosSessionEmptyReason.NoAccount);
        True(noAccount.Contains("konto", StringComparison.OrdinalIgnoreCase)
            && noAccount.Contains("Sonos", StringComparison.Ordinal),
            "Brak konta ma wskazac droge do konta: " + noAccount);
        var noGroups = SonosSessionListPresentation.DescribeEmptyState(SonosSessionEmptyReason.NoGroups);
        True(!string.Equals(noAccount, noGroups, StringComparison.Ordinal),
            "Brak grup to inny stan niz brak konta.");
    }

    // 6. Tytul, wykonawca, zrodlo, stan, glosnosc i wyciszenie z PRAWDZIWEGO odczytu.
    private static void PlayerTextFromRealRead()
    {
        var track = new SonosTrackMetadata(
            "track", "Brzeg ciszy", "Anna Kowalska", "Album", null,
            new SonosMetadataService("TIDAL", "tidal"), 214000);
        var metadata = new SonosGroupMetadata(
            new SonosMetadataContainer("Moja playlista", "playlist", null),
            new SonosQueueItem("it1", track, null),
            null,
            null,
            null);
        var status = new SonosGroupPlaybackStatus(
            SonosPlaybackState.Playing, null, null, "it1", 61000, null, null, null,
            new SonosPlaybackActions(true, true, null, true, true, true, null, null, null, null, null));
        var volume = new SonosGroupVolume(34, muted: false, fixedVolume: false);

        var view = SonosPlayerPresentation.Describe(status, metadata, volume);
        True(view.Title.Contains("Brzeg ciszy", StringComparison.Ordinal), view.Title);
        True(view.Title.Contains("Anna Kowalska", StringComparison.Ordinal), view.Title);
        True(view.Source.Contains("TIDAL", StringComparison.Ordinal), "Zrodlo: " + view.Source);
        True(view.StateText.Contains("Odtwarzanie", StringComparison.OrdinalIgnoreCase), view.StateText);
        True(view.VolumeText.Contains("34", StringComparison.Ordinal), view.VolumeText);
        True(!view.VolumeText.Contains("wyciszona", StringComparison.OrdinalIgnoreCase), view.VolumeText);

        // RADIO bez currentItem: nadal poprawny odczyt, nazwa stacji z kontenera.
        var radio = SonosPlayerPresentation.Describe(
            new SonosGroupPlaybackStatus(SonosPlaybackState.Playing, null, null, null, null, null, null, null, null),
            new SonosGroupMetadata(
                new SonosMetadataContainer("Radio 357", "station", new SonosMetadataService("Radio", "radio")),
                null, null, null, "Teraz w programie"),
            new SonosGroupVolume(20, muted: null, fixedVolume: null));
        True(radio.Title.Contains("Radio 357", StringComparison.Ordinal), "Radio: " + radio.Title);
        True(radio.VolumeText.Contains("nie wiadomo", StringComparison.OrdinalIgnoreCase)
            || radio.VolumeText.Contains("nieznane", StringComparison.OrdinalIgnoreCase),
            "Nieznane wyciszenie nie moze byc zgadniete na false: " + radio.VolumeText);

        // NIEUDANY odczyt nie niesie poprzednich danych jako sukcesu.
        var unknown = SonosPlayerPresentation.Describe(null, null, null);
        True(unknown.Title.Contains("Brak informacji", StringComparison.OrdinalIgnoreCase), unknown.Title);
        True(unknown.PositionText.Contains("Brak informacji", StringComparison.OrdinalIgnoreCase), unknown.PositionText);
    }

    // 7. Czas: zadnego zegara demo. Brak pozycji = brak informacji; ekstrapolacja
    // tylko dla znanej pozycji, stanu Playing i SWIEZEGO odczytu.
    private static void PositionNeverInvented()
    {
        var read = new DateTime(2026, 9, 28, 12, 0, 0, DateTimeKind.Utc);
        var playing = new SonosGroupPlaybackStatus(
            SonosPlaybackState.Playing, null, null, "it1", 60000, null, null, null, null);

        var fresh = SonosPlayerPosition.Resolve(playing, 214000, read, read.AddSeconds(4));
        Equal(64, (int)fresh.Position!.Value.TotalSeconds);
        True(fresh.Extrapolated, "Swiezy odczyt w stanie Playing moze doliczyc czas.");

        var stale = SonosPlayerPosition.Resolve(playing, 214000, read, read.AddMinutes(5));
        Equal(60, (int)stale.Position!.Value.TotalSeconds);
        True(!stale.Extrapolated && stale.Stale,
            "Po wygasnieciu swiezosci pokazujemy OSTATNIA odczytana pozycje, nie wyliczona.");

        var paused = SonosPlayerPosition.Resolve(
            new SonosGroupPlaybackStatus(SonosPlaybackState.Paused, null, null, "it1", 60000, null, null, null, null),
            214000, read, read.AddSeconds(30));
        Equal(60, (int)paused.Position!.Value.TotalSeconds);
        True(!paused.Extrapolated, "Pauza nie przesuwa czasu.");

        var nothing = SonosPlayerPosition.Resolve(
            new SonosGroupPlaybackStatus(SonosPlaybackState.Playing, null, null, "it1", null, null, null, null, null),
            null, read, read.AddSeconds(4));
        True(nothing.Position is null && !nothing.Extrapolated, "Brak pozycji zostaje brakiem informacji.");
        True(SonosPlayerPosition.Resolve(null, null, read, read).Position is null,
            "Nieudany odczyt nie ma pozycji.");
    }

    // 8. Bramka: availablePlaybackActions i volume.fixed decyduja o dostepnosci.
    // Niedostepne polecenie konczy sie ODMOWA bez zadnego POST.
    private static void CommandGatingFromCapabilities()
    {
        var actions = new SonosPlaybackActions(
            canPlay: true, canSkip: false, canSkipBack: true, canSkipToPrevious: false,
            canSeek: null, canPause: false, canStop: null, canRepeat: null, canRepeatOne: null,
            canCrossfade: null, canShuffle: null);
        var fixedVolume = new SonosGroupVolume(30, muted: false, fixedVolume: true);

        var play = SonosCommandGating.Evaluate(CommandIds.PlayPause, SonosPlaybackState.Paused, actions, fixedVolume);
        True(play.Allowed, "Play przy canPlay=true ma byc dostepne.");

        var pause = SonosCommandGating.Evaluate(CommandIds.PlayPause, SonosPlaybackState.Playing, actions, fixedVolume);
        True(!pause.Allowed && pause.Refusal!.Length > 0, "Pauza przy canPause=false to odmowa bez POST.");

        var next = SonosCommandGating.Evaluate(CommandIds.Next, SonosPlaybackState.Playing, actions, fixedVolume);
        True(!next.Allowed, "canSkip=false blokuje nastepny utwor.");

        var previous = SonosCommandGating.Evaluate(CommandIds.Previous, SonosPlaybackState.Playing, actions, fixedVolume);
        True(!previous.Allowed,
            "canSkipToPrevious=false ma pierwszenstwo nad przeterminowanym canSkipBack.");

        var seek = SonosCommandGating.Evaluate(CommandIds.SeekForward10, SonosPlaybackState.Playing, actions, fixedVolume);
        True(!seek.Allowed, "Nieznane canSeek nie jest zgoda.");

        var volumeUp = SonosCommandGating.Evaluate(CommandIds.VolumeUp5, SonosPlaybackState.Playing, actions, fixedVolume);
        True(!volumeUp.Allowed && volumeUp.Refusal!.Contains("stały poziom", StringComparison.OrdinalIgnoreCase),
            "volume.fixed blokuje poziom glosnosci: " + volumeUp.Refusal);

        var mute = SonosCommandGating.Evaluate(
            CommandIds.ToggleMuteCurrentSession, SonosPlaybackState.Playing, actions, fixedVolume);
        True(mute.Allowed, "Wyciszenie jest dopuszczalne takze przy stalej glosnosci.");

        var unknownMute = SonosCommandGating.Evaluate(
            CommandIds.ToggleMuteCurrentSession,
            SonosPlaybackState.Playing,
            actions,
            new SonosGroupVolume(30, muted: null, fixedVolume: false));
        True(!unknownMute.Allowed,
            "Nieznane wyciszenie -> odmowa albo odczyt, nigdy zgadniety bool.");

        var withoutRead = SonosCommandGating.Evaluate(CommandIds.PlayPause, SonosPlaybackState.Unknown, null, null);
        True(!withoutRead.Allowed, "Bez odczytu stanu nie udajemy dostepnosci.");
    }

    // 9. Po poleceniu JAWNY odczyt. Accepted != wykonane, a "stan byl juz
    // docelowy" to odczyt, nie dowod skutku NASZEGO polecenia.
    private static void VerdictAfterExplicitRead()
    {
        var confirmed = SonosCommandVerdict.Describe(
            SonosVerdictCommand.Pause, accepted: true, stateReadSucceeded: true,
            before: SonosPlaybackState.Playing, after: SonosPlaybackState.Paused,
            beforeItemId: "it1", afterItemId: "it1");
        True(confirmed.Confirmed && confirmed.Text.Contains("Pauza", StringComparison.OrdinalIgnoreCase),
            "Zmiana stanu na docelowy po naszym poleceniu jest potwierdzeniem: " + confirmed.Text);

        var alreadyThere = SonosCommandVerdict.Describe(
            SonosVerdictCommand.Pause, accepted: true, stateReadSucceeded: true,
            before: SonosPlaybackState.Paused, after: SonosPlaybackState.Paused,
            beforeItemId: "it1", afterItemId: "it1");
        True(!alreadyThere.Confirmed,
            "Stan juz docelowy przed poleceniem nie dowodzi skutku naszego POST.");
        True(alreadyThere.Text.Contains("odczytan", StringComparison.OrdinalIgnoreCase),
            "Komunikat ma mowic o ODCZYTANYM stanie: " + alreadyThere.Text);

        var readFailed = SonosCommandVerdict.Describe(
            SonosVerdictCommand.Pause, accepted: true, stateReadSucceeded: false,
            before: SonosPlaybackState.Playing, after: SonosPlaybackState.Unknown,
            beforeItemId: "it1", afterItemId: null);
        True(!readFailed.Confirmed
            && readFailed.Text.Contains("niepotwierdzon", StringComparison.OrdinalIgnoreCase),
            "Nieudany odczyt = niepotwierdzone: " + readFailed.Text);

        var skipNoMaterial = SonosCommandVerdict.Describe(
            SonosVerdictCommand.Next, accepted: true, stateReadSucceeded: true,
            before: SonosPlaybackState.Playing, after: SonosPlaybackState.Playing,
            beforeItemId: null, afterItemId: null);
        True(!skipNoMaterial.Confirmed,
            "Sam udany GET nie dowodzi zmiany utworu bez porownywalnego materialu.");

        var skipChanged = SonosCommandVerdict.Describe(
            SonosVerdictCommand.Next, accepted: true, stateReadSucceeded: true,
            before: SonosPlaybackState.Playing, after: SonosPlaybackState.Playing,
            beforeItemId: "it1", afterItemId: "it2");
        True(skipChanged.Confirmed, "Zmieniony identyfikator utworu potwierdza przejscie.");

        var notAccepted = SonosCommandVerdict.Describe(
            SonosVerdictCommand.Pause, accepted: false, stateReadSucceeded: true,
            before: SonosPlaybackState.Playing, after: SonosPlaybackState.Playing,
            beforeItemId: "it1", afterItemId: "it1");
        True(!notAccepted.Confirmed, "Nieprzyjete polecenie nie jest potwierdzone.");
    }

    // 10. Aktywna grupa trzymana po IDENTYFIKATORZE. Zniknieta grupa nie jest po
    // cichu zamieniana na inna do STEROWANIA.
    private static void ActiveGroupFollowsIdentifier()
    {
        var first = new SonosHouseholdTopology(
            [
                new SonosGroup(GroupA, "Salon", "P1", ["P1"], SonosPlaybackState.Playing),
                new SonosGroup(GroupB, "Sypialnia", "P3", ["P3"], SonosPlaybackState.Idle)
            ], [], false);
        var reordered = new SonosHouseholdTopology(
            [
                new SonosGroup(GroupB, "Sypialnia", "P3", ["P3"], SonosPlaybackState.Idle),
                new SonosGroup(GroupA, "Salon zmieniony", "P1", ["P1"], SonosPlaybackState.Paused)
            ], [], false);

        Equal(GroupA, SonosActiveGroupPolicy.Resolve(GroupA, first)?.Id);
        Equal(GroupA, SonosActiveGroupPolicy.Resolve(GroupA, reordered)?.Id);
        True(SonosActiveGroupPolicy.Resolve("RINCON_nieistniejaca:0", first) is null,
            "Znikniona grupa nie moze po cichu wskazac innej grupy do sterowania.");
        True(SonosActiveGroupPolicy.Resolve(null, first) is null,
            "Brak wyboru nie wybiera pierwszej grupy do STEROWANIA.");
    }

    private static ConfigurationStore NewStore(string statePath) =>
        new(statePath,
            Path.ChangeExtension(statePath, ".library.db"),
            Path.ChangeExtension(statePath, ".podcasts.db"));

    private static string NewFolder()
    {
        var folder = Path.Combine(Path.GetTempPath(), "amc-sonos-session-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        return folder;
    }

    private static void TryDelete(string folder)
    {
        try { Directory.Delete(folder, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static void True(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"Oczekiwano {expected}, otrzymano {actual}.");
    }
}
