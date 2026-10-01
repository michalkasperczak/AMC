using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Sonos;

/// <summary>
/// PRESETY SONOSA - warstwa MODELU i ZASAD, mierzona na PRAWDZIWYM
/// <see cref="ConfigurationStore"/> (zapis na dysk i ponowny odczyt) oraz na
/// PRODUKCYJNEJ metodzie klonowania stanu.
///
/// Czego tu NIE MA: HTTP, konta, Sonosa, okien. Te rzeczy mierzy pion w
/// zestawie Windows. Tutaj pytamy WYLACZNIE o to, czego nie da sie zobaczyc
/// przez UI: czy nowe pola PRZEZYWAJA Save/Load/Clone, czy identyfikator
/// OPAQUE nie jest po cichu zmieniany, czy staly cel wymaga DOKLADNEGO skladu i
/// czy powtorzenie bez tozsamosci NIE udaje, ze wie.
/// </summary>
internal static class SonosPresetModelTests
{
    private readonly record struct Verdict(string Case, string Hypothesis, bool Passed, string Detail);

    internal static void Run()
    {
        List<Verdict> verdicts =
        [
            Measure("M1", "preset ulubionego PRZEZYWA Save/Load z domem i LITERALNYM identyfikatorem",
                MeasureFavoriteSurvivesRoundTrip),
            Measure("M2", "identyfikator OPAQUE NIE jest trymowany, a identyfikatory innych rodzajow - TAK",
                MeasureOpaqueIdNotTrimmed),
            Measure("M3", "staly zestaw glosnikow PRZEZYWA Save/Load, a pusty zapisuje sie jako BRAK",
                MeasureFixedSetSurvivesRoundTrip),
            Measure("M4", "Clone przenosi dom i staly zestaw jako KOPIE, nie wspolna liste",
                MeasureCloneCarriesNewFields),
            Measure("M5", "starszy zapis bez nowych pol czyta sie jako 'biezacy cel', nie jako pusty zestaw",
                MeasureLegacyEntryDefaultsToCurrentTarget),
            Measure("M6", "staly cel wymaga DOKLADNEGO skladu: szerszy, wezszy i inny dom sa ODRZUCONE",
                MeasureFixedTargetNeedsExactSet),
            Measure("M7", "dwie grupy o tym samym skladzie to NIEJEDNOZNACZNOSC, nie wybor pierwszej",
                MeasureAmbiguousFixedTargetRefused),
            Measure("M8", "powtorzenie BEZ klucza tozsamosci idzie w zwykly load, nie w ciche 'to samo'",
                MeasureRepeatWithoutIdentityLoads),
            Measure("M9", "ten material GRA -> tylko nazwa; SPAUZOWANY lub radio IDLE z kontenerem -> wznowienie",
                MeasureRepeatDecisions),
            Measure("M10", "klucz wlasnej stacji zmienia sie po zmianie adresu i trzyma limit 128",
                MeasureOwnStreamItemIdFollowsUrl),
            Measure("M11", "usuniety preset NIE zmartwychwstaje po Save/Load",
                MeasureRemovedPresetStaysRemoved),
            Measure("M12", "GRUPA glosnikow NIE jest materialem presetu",
                MeasureGroupIsNotMaterial),
        ];

        foreach (var verdict in verdicts)
        {
            Console.WriteLine(
                (verdict.Passed ? "POTWIERDZONY " : "ODRZUCONY ")
                + verdict.Case + ": " + verdict.Hypothesis + " -> " + verdict.Detail);
        }

        var rejected = verdicts.Where(verdict => !verdict.Passed).ToArray();
        Console.WriteLine($"OK: presety Sonosa - model i zasady ({verdicts.Count} sprawdzeń)");
        if (rejected.Length != 0)
        {
            throw new Exception(
                "Presety Sonosa: ODRZUCONO " + rejected.Length + " z " + verdicts.Count + ": "
                + string.Join(" || ", rejected.Select(verdict => verdict.Case + " " + verdict.Detail)));
        }
    }

    private static Verdict Measure(string name, string hypothesis, Func<string> body)
    {
        try { return new Verdict(name, hypothesis, true, body()); }
        catch (Exception exception) { return new Verdict(name, hypothesis, false, exception.Message); }
    }

    // ==================== PRAWDZIWY MAGAZYN ====================

    /// <summary>
    /// Zapis i ODCZYT przez PRAWDZIWY ConfigurationStore na pliku tymczasowym.
    /// Nie podmieniamy serializacji - pytanie brzmi wlasnie o to, czy pola
    /// przezyja produkcyjna droge, w tym normalizacje przy odczycie.
    /// </summary>
    private static PersistedState RoundTrip(Action<PersistedState> arrange)
    {
        var directory = Path.Combine(Path.GetTempPath(), "amc-sonos-preset-model-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "settings.json");
            var store = new ConfigurationStore(path);
            var state = store.LoadOrCreate();
            arrange(state);
            store.Save(state);
            return new ConfigurationStore(path).LoadOrCreate();
        }
        finally
        {
            try { Directory.Delete(directory, recursive: true); } catch (IOException) { }
        }
    }

    private static List<SessionPresetEntry> EntriesFor(PersistedState state, string session) =>
        state.SessionPresets.EntriesBySession.TryGetValue(session, out var entries)
            ? entries
            : throw new Exception("Po odczycie nie ma wpisów presetów dla sesji " + session + ".");

    private static void Put(PersistedState state, string session, params SessionPresetEntry[] entries) =>
        state.SessionPresets.EntriesBySession[session] = [.. entries];

    // ==================== PRZYPADKI ====================

    private static string MeasureFavoriteSurvivesRoundTrip()
    {
        // Identyfikator Z SPACJAMI W SRODKU i znakami, ktorych nie wolno ruszac.
        const string opaque = "FV:2/ a+b/c==";
        var loaded = RoundTrip(state => Put(state, "sonos", new SessionPresetEntry
        {
            Slot = 3,
            TargetId = opaque,
            TargetKind = SonosPresetKinds.Favorite,
            TargetTitle = "Radio Nowy Świat",
            SonosHouseholdId = "Sonos_abc123"
        }));

        var entry = EntriesFor(loaded, "sonos").Single(candidate => candidate.Slot == 3);
        if (entry.TargetId != opaque)
        {
            throw new Exception($"Identyfikator zmieniony w drodze: '{opaque}' -> '{entry.TargetId}'.");
        }

        if (entry.SonosHouseholdId != "Sonos_abc123")
        {
            throw new Exception("Dom nie przeżył zapisu: " + (entry.SonosHouseholdId ?? "null") + ".");
        }

        if (entry.TargetTitle != "Radio Nowy Świat")
        {
            throw new Exception("Etykieta nie przeżyła zapisu: " + entry.TargetTitle + ".");
        }

        return $"slot 3, identyfikator literalnie '{entry.TargetId}', dom {entry.SonosHouseholdId}";
    }

    private static string MeasureOpaqueIdNotTrimmed()
    {
        const string padded = "  FV:2/trailing  ";
        var loaded = RoundTrip(state => Put(state, "sonos",
            new SessionPresetEntry
            {
                Slot = 1,
                TargetId = padded,
                TargetKind = SonosPresetKinds.Favorite,
                TargetTitle = "Ulubiony"
            },
            new SessionPresetEntry
            {
                Slot = 2,
                TargetId = padded,
                TargetKind = SonosPresetKinds.Playlist,
                TargetTitle = "Playlista"
            },
            new SessionPresetEntry
            {
                Slot = 4,
                TargetId = "  amc-station-7  ",
                TargetKind = SonosPresetKinds.OwnStream,
                TargetTitle = "Moja stacja"
            }));

        var entries = EntriesFor(loaded, "sonos");
        foreach (var slot in new[] { 1, 2 })
        {
            var entry = entries.Single(candidate => candidate.Slot == slot);
            if (entry.TargetId != padded)
            {
                throw new Exception($"Slot {slot}: identyfikator OPAQUE został znormalizowany na '{entry.TargetId}'.");
            }
        }

        var own = entries.Single(candidate => candidate.Slot == 4);
        if (own.TargetId != "amc-station-7")
        {
            throw new Exception("Własna stacja: identyfikator lokalny powinien być trymowany, jest '"
                + own.TargetId + "'.");
        }

        return "ulubiony i playlista literalnie, własna stacja trymowana - wyjątek jest WĄSKI";
    }

    private static string MeasureFixedSetSurvivesRoundTrip()
    {
        var loaded = RoundTrip(state => Put(state, "sonos",
            new SessionPresetEntry
            {
                Slot = 1,
                TargetId = "FV:2/kuchnia",
                TargetKind = SonosPresetKinds.Favorite,
                TargetTitle = "Z zestawem",
                SonosHouseholdId = "Sonos_dom1",
                // Kolejnosc ODWROTNA i jeden duplikat - kanonizacja ma to uporzadkowac.
                SonosFixedPlayerIds = ["RINCON_B", "RINCON_A", "RINCON_B"]
            },
            new SessionPresetEntry
            {
                Slot = 2,
                TargetId = "FV:2/bez",
                TargetKind = SonosPresetKinds.Favorite,
                TargetTitle = "Bez zestawu",
                SonosFixedPlayerIds = []
            }));

        var entries = EntriesFor(loaded, "sonos");
        var fixedEntry = entries.Single(candidate => candidate.Slot == 1);
        if (fixedEntry.SonosFixedPlayerIds is not { Count: 2 } ids)
        {
            throw new Exception("Stały zestaw nie przeżył zapisu: "
                + (fixedEntry.SonosFixedPlayerIds?.Count.ToString() ?? "null") + " pozycji.");
        }

        if (ids[0] != "RINCON_A" || ids[1] != "RINCON_B")
        {
            throw new Exception("Skład nie jest kanoniczny: " + string.Join(",", ids) + ".");
        }

        var empty = entries.Single(candidate => candidate.Slot == 2);
        if (empty.SonosFixedPlayerIds is not null)
        {
            throw new Exception("Pusty zestaw zapisał się jako lista, a znaczy 'bieżący cel'.");
        }

        return "zestaw [RINCON_A, RINCON_B] kanoniczny i bez duplikatu; pusty = brak (bieżący cel)";
    }

    private static string MeasureCloneCarriesNewFields()
    {
        var directory = Path.Combine(Path.GetTempPath(), "amc-sonos-preset-clone-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var store = new ConfigurationStore(Path.Combine(directory, "settings.json"));
            var source = store.LoadOrCreate();
            Put(source, "sonos", new SessionPresetEntry
            {
                Slot = 5,
                TargetId = "FV:2/klon",
                TargetKind = SonosPresetKinds.Favorite,
                TargetTitle = "Do klonowania",
                SonosHouseholdId = "Sonos_dom9",
                SonosFixedPlayerIds = ["RINCON_X", "RINCON_Y"]
            });

            var clone = store.CloneState(source);
            var cloned = clone.SessionPresets.EntriesBySession["sonos"]
                .Single(entry => entry.Slot == 5);

            if (cloned.SonosHouseholdId != "Sonos_dom9")
            {
                throw new Exception("Clone zgubił dom: " + (cloned.SonosHouseholdId ?? "null") + ".");
            }

            if (cloned.SonosFixedPlayerIds is not { Count: 2 })
            {
                throw new Exception("Clone zgubił stały zestaw.");
            }

            // Kopia, nie ta sama lista: mutacja klona nie ma ruszac zrodla.
            cloned.SonosFixedPlayerIds.Add("RINCON_Z");
            var original = source.SessionPresets.EntriesBySession["sonos"]
                .Single(entry => entry.Slot == 5);
            if (original.SonosFixedPlayerIds!.Count != 2)
            {
                throw new Exception("Clone oddał WSPÓLNĄ listę - zmiana klona ruszyła źródło.");
            }

            return "dom i zestaw przeniesione; lista jest KOPIĄ (źródło nadal 2 pozycje)";
        }
        finally
        {
            try { Directory.Delete(directory, recursive: true); } catch (IOException) { }
        }
    }

    private static string MeasureLegacyEntryDefaultsToCurrentTarget()
    {
        // Starszy format: BRAK obu nowych pol. Nie wolno z tego zrobic ani
        // "pustego zestawu glosnikow", ani domu "kazdy".
        var loaded = RoundTrip(state => Put(state, "sonos", new SessionPresetEntry
        {
            Slot = 7,
            TargetId = "FV:2/stary",
            TargetKind = SonosPresetKinds.Favorite,
            TargetTitle = "Stary zapis"
        }));

        var entry = EntriesFor(loaded, "sonos").Single(candidate => candidate.Slot == 7);
        if (entry.SonosFixedPlayerIds is not null)
        {
            throw new Exception("Starszy wpis dostał stały zestaw, którego nikt nie zapisywał.");
        }

        if (entry.SonosHouseholdId is not null)
        {
            throw new Exception("Starszy wpis dostał dom, którego nikt nie zapisywał.");
        }

        var resolution = SonosPresetFixedTarget.Resolve(
            entry.SonosHouseholdId, entry.SonosFixedPlayerIds, "Sonos_dowolny", topology: null, out var group);
        if (resolution != SonosFixedTargetResolution.NotFixed || group is not null)
        {
            throw new Exception("Starszy wpis nie rozwiązał się jako 'bieżący cel', a jako " + resolution + ".");
        }

        return "brak obu pól -> NotFixed, czyli bieżąco wybrana grupa; żadnej grupy nie wskazano";
    }

    private static string MeasureFixedTargetNeedsExactSet()
    {
        var topology = Topology(
            Group("GRUPA:szersza", "Kuchnia + Salon + Sypialnia", "RINCON_A", "RINCON_B", "RINCON_C"),
            Group("GRUPA:wezsza", "Kuchnia", "RINCON_A"),
            Group("GRUPA:dokladna", "Kuchnia + Salon", "RINCON_A", "RINCON_B"));

        string[] wanted = ["RINCON_B", "RINCON_A"];

        var exact = SonosPresetFixedTarget.Resolve(
            "Sonos_dom1", wanted, "Sonos_dom1", topology, out var group);
        if (exact != SonosFixedTargetResolution.Resolved || group?.Id != "GRUPA:dokladna")
        {
            throw new Exception("Dokładny skład nie trafił w swoją grupę: " + exact + ", "
                + (group?.Id ?? "brak") + ".");
        }

        // Skladu, ktorego NIE MA, nie wolno dopasowac do nadzbioru ani podzbioru.
        var missing = SonosPresetFixedTarget.Resolve(
            "Sonos_dom1", new[] { "RINCON_A", "RINCON_D" }, "Sonos_dom1", topology, out var none);
        if (missing != SonosFixedTargetResolution.NoGroupWithExactSet || none is not null)
        {
            throw new Exception("Nieistniejący skład dał " + missing + " i grupę " + (none?.Id ?? "brak") + ".");
        }

        var otherHome = SonosPresetFixedTarget.Resolve(
            "Sonos_dom1", wanted, "Sonos_dom2", topology, out var foreign);
        if (otherHome != SonosFixedTargetResolution.DifferentHousehold || foreign is not null)
        {
            throw new Exception("Inny dom dał " + otherHome + ".");
        }

        var unknown = SonosPresetFixedTarget.Resolve(
            "Sonos_dom1", wanted, "Sonos_dom1", topology: null, out _);
        if (unknown != SonosFixedTargetResolution.TopologyUnavailable)
        {
            throw new Exception("Nieznana topologia dała " + unknown + " zamiast uczciwego 'nie wiem'.");
        }

        return "dokładny skład trafia; nadzbiór/podzbiór/inny dom/brak topologii - ODMOWA bez grupy";
    }

    private static string MeasureAmbiguousFixedTargetRefused()
    {
        var topology = Topology(
            Group("GRUPA:pierwsza", "Kuchnia + Salon", "RINCON_A", "RINCON_B"),
            Group("GRUPA:druga", "Salon + Kuchnia", "RINCON_B", "RINCON_A"));

        var resolution = SonosPresetFixedTarget.Resolve(
            "Sonos_dom1", new[] { "RINCON_A", "RINCON_B" }, "Sonos_dom1", topology, out var group);
        if (resolution != SonosFixedTargetResolution.Ambiguous || group is not null)
        {
            throw new Exception("Dwie grupy o tym samym składzie dały " + resolution
                + " i grupę " + (group?.Id ?? "brak") + ".");
        }

        return "dwie grupy o identycznym składzie -> Ambiguous, ŻADNEJ nie wybrano";
    }

    private static string MeasureRepeatWithoutIdentityLoads()
    {
        // Brak klucza po NASZEJ stronie (ulubiony/playlista) - to wlasnie LUKA.
        var noExpected = SonosPresetRepeat.Decide(
            expectedItemId: null, currentItemId: "cokolwiek", SonosPlaybackState.Playing, hasContainer: true);
        if (noExpected != SonosPresetRepeatDecision.Load)
        {
            throw new Exception("Bez naszego klucza decyzja była " + noExpected + ".");
        }

        // Brak klucza po stronie urzadzenia - tez nie ma tozsamosci.
        var noCurrent = SonosPresetRepeat.Decide(
            "amc-own-abc", currentItemId: null, SonosPlaybackState.Playing, hasContainer: true);
        if (noCurrent != SonosPresetRepeatDecision.Load)
        {
            throw new Exception("Bez klucza z odczytu decyzja była " + noCurrent + ".");
        }

        // Klucze ROZNE - inny material, zwykly load.
        var different = SonosPresetRepeat.Decide(
            "amc-own-abc", "amc-own-xyz", SonosPlaybackState.Playing, hasContainer: true);
        if (different != SonosPresetRepeatDecision.Load)
        {
            throw new Exception("Różne klucze dały " + different + ".");
        }

        return "brak klucza po którejkolwiek stronie i klucze różne -> Load; zero udawanej wiedzy";
    }

    private static string MeasureRepeatDecisions()
    {
        const string key = "amc-own-abc";

        var playing = SonosPresetRepeat.Decide(key, key, SonosPlaybackState.Playing, hasContainer: true);
        if (playing != SonosPresetRepeatDecision.AnnounceOnly)
        {
            throw new Exception("Grający ten sam materiał dał " + playing + " zamiast samej nazwy.");
        }

        var buffering = SonosPresetRepeat.Decide(key, key, SonosPlaybackState.Buffering, hasContainer: true);
        if (buffering != SonosPresetRepeatDecision.AnnounceOnly)
        {
            throw new Exception("Materiał w drodze dał " + buffering + " - przeładowanie bez powodu.");
        }

        var paused = SonosPresetRepeat.Decide(key, key, SonosPlaybackState.Paused, hasContainer: true);
        if (paused != SonosPresetRepeatDecision.Resume)
        {
            throw new Exception("Spauzowany ten sam materiał dał " + paused + " zamiast wznowienia.");
        }

        var idle = SonosPresetRepeat.Decide(key, key, SonosPlaybackState.Idle, hasContainer: true);
        if (idle != SonosPresetRepeatDecision.Resume)
            throw new Exception("Radio IDLE ze zgodnym kluczem i kontenerem nie zostało wznowione.");
        if (SonosPresetRepeat.Decide(key, key, SonosPlaybackState.Idle, hasContainer: false) != SonosPresetRepeatDecision.Load)
            throw new Exception("Bez kontenera nie ma czego wznawiać.");
        return "PLAYING/BUFFERING -> nazwa; PAUSED/IDLE ze zgodnym kontenerem -> wznowienie";
    }

    private static string MeasureOwnStreamItemIdFollowsUrl()
    {
        var first = SonosOwnStreamIdentity.TryComputeItemId("station-1", "https://example.test/a.mp3");
        var sameAgain = SonosOwnStreamIdentity.TryComputeItemId("station-1", "https://example.test/a.mp3");
        var afterEdit = SonosOwnStreamIdentity.TryComputeItemId("station-1", "https://example.test/b.mp3");
        var otherStation = SonosOwnStreamIdentity.TryComputeItemId("station-2", "https://example.test/a.mp3");

        if (first is null || sameAgain is null || afterEdit is null || otherStation is null)
        {
            throw new Exception("Klucz nie policzył się dla kompletnego wpisu.");
        }

        if (first != sameAgain)
        {
            throw new Exception("Ten sam wpis dał DWA różne klucze - powtórzenie nigdy by nie trafiło.");
        }

        if (first == afterEdit)
        {
            throw new Exception("Po zmianie adresu klucz się NIE zmienił - preset grałby stary materiał.");
        }

        if (first == otherStation)
        {
            throw new Exception("Dwie różne stacje mają ten sam klucz.");
        }

        // Limit API: itemId <= 128 znakow, takze dla bardzo dlugiego adresu.
        var longUrl = "https://example.test/" + new string('x', 4000);
        var fromLongUrl = SonosOwnStreamIdentity.TryComputeItemId("station-1", longUrl);
        if (fromLongUrl is null || fromLongUrl.Length > 128)
        {
            throw new Exception("Klucz z długiego adresu ma " + (fromLongUrl?.Length.ToString() ?? "null")
                + " znaków, a limit to 128.");
        }

        if (SonosOwnStreamIdentity.TryComputeItemId("station-1", "   ") is not null
            || SonosOwnStreamIdentity.TryComputeItemId(null, "https://example.test/a.mp3") is not null)
        {
            throw new Exception("Niekompletny wpis dostał klucz, zamiast uczciwego null.");
        }

        return $"klucz stabilny, zmienia się z adresem i stacją, {fromLongUrl.Length} znaków dla adresu 4 kB";
    }

    private static string MeasureRemovedPresetStaysRemoved()
    {
        var directory = Path.Combine(Path.GetTempPath(), "amc-sonos-preset-remove-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "settings.json");
            var store = new ConfigurationStore(path);
            var state = store.LoadOrCreate();
            Put(state, "sonos",
                new SessionPresetEntry
                {
                    Slot = 1,
                    TargetId = "FV:2/zostaje",
                    TargetKind = SonosPresetKinds.Favorite,
                    TargetTitle = "Zostaje"
                },
                new SessionPresetEntry
                {
                    Slot = 2,
                    TargetId = "FV:2/do-usuniecia",
                    TargetKind = SonosPresetKinds.Favorite,
                    TargetTitle = "Do usunięcia"
                });
            store.Save(state);

            // USUNIECIE przypisania produkcyjna droga: wpis wypada z listy sesji.
            var reopened = new ConfigurationStore(path);
            var loaded = reopened.LoadOrCreate();
            var entries = loaded.SessionPresets.EntriesBySession["sonos"];
            entries.RemoveAll(entry => entry.Slot == 2);
            reopened.Save(loaded);

            var after = new ConfigurationStore(path).LoadOrCreate();
            var remaining = after.SessionPresets.EntriesBySession["sonos"];
            if (remaining.Any(entry => entry.Slot == 2))
            {
                throw new Exception("Usunięty preset wrócił po ponownym odczycie.");
            }

            if (remaining.SingleOrDefault(entry => entry.Slot == 1) is null)
            {
                throw new Exception("Usunięcie slotu 2 zabrało też slot 1.");
            }

            return "slot 2 nie wrócił, slot 1 nietknięty";
        }
        finally
        {
            try { Directory.Delete(directory, recursive: true); } catch (IOException) { }
        }
    }

    private static string MeasureGroupIsNotMaterial()
    {
        if (SonosPresetKinds.IsSonosMaterial("sonos-group")
            || SonosPresetKinds.IsSonosMaterial("group")
            || SonosPresetKinds.IsSonosMaterial("current"))
        {
            throw new Exception("Grupa głośników przeszła jako materiał presetu.");
        }

        foreach (var kind in new[]
                 { SonosPresetKinds.Favorite, SonosPresetKinds.Playlist, SonosPresetKinds.OwnStream })
        {
            if (!SonosPresetKinds.IsSonosMaterial(kind))
            {
                throw new Exception("Rodzaj " + kind + " nie jest uznany za materiał.");
            }
        }

        // Dom wiaze TYLKO katalogi serwisu; wlasna stacja jest nasza.
        if (!SonosPresetKinds.IsHouseholdBound(SonosPresetKinds.Favorite)
            || !SonosPresetKinds.IsHouseholdBound(SonosPresetKinds.Playlist)
            || SonosPresetKinds.IsHouseholdBound(SonosPresetKinds.OwnStream))
        {
            throw new Exception("Wiązanie z domem nie zgadza się z pochodzeniem identyfikatora.");
        }

        return "trzy rodzaje materiału uznane, grupa ODRZUCONA; dom wiąże tylko ulubione i playlisty";
    }

    // ==================== POMOCNICZE ====================

    private static SonosGroup Group(string id, string name, params string[] playerIds) =>
        new(id, name, playerIds[0], playerIds, SonosPlaybackState.Unknown);

    private static SonosHouseholdTopology Topology(params SonosGroup[] groups) =>
        new(groups, [], partial: false);
}
