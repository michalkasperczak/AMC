using System.Net.Http;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Sonos;
using AccessibleMediaController.Windows;

/// <summary>
/// PRESETY SONOSA - JEDEN PION PRZEZ CALA DROGE, na PRAWDZIWYM
/// <see cref="MainWindow"/>, PRODUKCYJNYM wlascicielu konta i PRAWDZIWYM
/// <see cref="ConfigurationStore"/> na dysku, az do SYNTETYCZNEGO
/// <see cref="HttpMessageHandler"/>.
///
/// Partial TEGO SAMEGO typu co F3c - zeby uzyc ISTNIEJACEJ aparatury
/// <c>RealHarness</c> (konto w pamieci, bramka, transport, pompowanie
/// prawdziwym <c>DispatcherFrame</c>) BEZ kopiowania jej drugi raz.
///
/// Mierzymy ZADANIA HTTP: metode, adres i CIALO. HTTP 200 nie jest tu
/// traktowane jako dowod, ze cokolwiek gra - sprawdzamy TRESC zlecenia i
/// LICZBE zlecen, w tym tam, gdzie poprawna odpowiedz to ZERO POST.
/// </summary>
internal static partial class SonosFavoritePlayRealOwnerTests
{
    /// <summary>WEJSCIE pionu presetow - wlasny watek STA, jak reszta zestawu.</summary>
    internal static void RunPresets()
    {
        List<Verdict> verdicts = [];
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { verdicts = RunAllPresets(); }
            catch (Exception exception) { failure = exception; }
            finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(240)))
        {
            throw new Exception("Limit czasu pomiaru pionu presetów Sonos.");
        }

        if (failure is not null) throw failure;

        foreach (var verdict in verdicts)
        {
            Console.WriteLine(
                (verdict.Passed ? "REAL-POTWIERDZONY " : "REAL-ODRZUCONY ")
                + verdict.Case + ": " + verdict.Hypothesis + " -> " + verdict.Detail);
        }

        var rejected = verdicts.Where(verdict => !verdict.Passed).ToArray();
        Console.WriteLine($"OK: presety Sonos - pion przez realną drogę ({verdicts.Count} sprawdzeń)");
        if (rejected.Length != 0)
        {
            throw new Exception(
                "Presety Sonos pion: ODRZUCONO " + rejected.Length + " z " + verdicts.Count + ": "
                + string.Join(" || ", rejected.Select(verdict => verdict.Case + " " + verdict.Detail)));
        }
    }

    private static List<Verdict> RunAllPresets() =>
    [
        Measure("P1", "PRZYPISANIE z PRAWDZIWEGO modalu ulubionych zapisuje ZAZNACZONY opaque ID "
            + "przez PRAWDZIWY store, przezywa ponowny odczyt i NIE wysyla ZADNEGO POST",
            MeasureAssignFromRealModalPersistsAndSendsNothing),
        Measure("P2", "URUCHOMIENIE presetu idzie na AKTUALNIE wybrana grupe: JEDEN POST "
            + "loadFavorite z LITERALNYM zapisanym ID, INSERT/true i BEZ playModes",
            MeasurePresetFiresOnCurrentTarget),
        Measure("P3", "STALY CEL, ktory ISTNIEJE: POST idzie na grupe o DOKLADNIE zapisanym skladzie, "
            + "a nie na biezaco wybrana",
            MeasureFixedTargetResolvedGoesToItsGroup),
        Measure("P4", "STALY CEL, ktorego NIE MA: ZERO POST, uczciwa odmowa, "
            + "ZADNEGO przegrupowania i ZADNEGO grania obok",
            MeasureFixedTargetGoneRefusesWithoutPost),
        Measure("P5", "preset z INNEGO DOMU: ZERO POST - stary opaque ID nie idzie w cudzy katalog",
            MeasureForeignHouseholdPresetSendsNothing),
        Measure("P6", "USUNIETA wlasna stacja: ZERO POST i instrukcja; "
            + "preset nie wskrzesza stacji ani nie mrozi starego adresu",
            MeasureRemovedOwnStreamRefusesWithoutPost),
        Measure("P7", "BRAK wybranego celu: ZERO POST i odesłanie do wyboru głośników",
            MeasureNoTargetSendsNothing),
        Measure("P8", "SPOZNIONY preset NIE przykrywa nowszego zamiaru: po zmianie konta "
            + "stara proba konczy sie bez POST starym biletem",
            MeasureLatePresetDoesNotOverrideNewerIntent),
        Measure("P9", "USTAWIANIE presetu nie odtwarza niczego, a USUNIECIE przypisania "
            + "nie zmartwychwstaje po ponownym odczycie",
            MeasureAssignNeverPlaysAndRemovalSticks),
        Measure("P10", "Własna stacja już gra: sama nazwa i zero POST", () => MeasureOwnPreset("playing")),
        Measure("P11", "Radio IDLE z tym samym kluczem: wznowienie bez load", () => MeasureOwnPreset("idle")),
        Measure("P12", "Błąd GET nie uprawnia do przejęcia grupy", () => MeasureOwnPreset("read-failed")),
        Measure("P13", "Zmiana itemId podczas odczytu metadata nie wznawia obcego źródła", () => MeasureOwnPreset("changed")),
        Measure("P14", "Nowy URL pod tym samym ID wysyła nowy adres i klucz", () => MeasureOwnPreset("edited")),
        Measure("P15", "Niepełna topologia nie potwierdza stałego celu", MeasurePartialFixedPreset),
        Measure("P16", "Enter własnej stacji i preset używają jednej tożsamości i zamiaru", MeasureOwnEnterThenPreset),
        // RYZYKO 3b: zajętość miejsca nie może zlewać różnych rodzajów ani domów.
        Measure("P17", "Klucz materiału rozróżnia rodzaj i dom, własna stacja niezależna", MeasureMaterialKeySeparatesKindAndHousehold),
        Measure("P18", "Nazwa presetu przed odpowiedzią sieci, działający Dispatcher, bez powtórzonej mowy", MeasurePresetFeedbackBeforeNetwork),
    ];

    // ==================== APARATURA PRESETOW ====================

    private const string PlaylistsBody =
        "{\"version\":\"W1\",\"playlists\":[{\"id\":\"PL-PIERWSZA\",\"name\":\"Poranek\",\"trackCount\":3}]}";

    /// <summary>
    /// TOPOLOGIA z DWOMA grupami o ROZNYM skladzie - zeby staly cel mial co
    /// wskazac I zeby bylo widac, ze POST nie poszedl na biezaco wybrana grupe.
    /// </summary>
    private const string FixedGroupId = "GRUPA-SYPIALNIA:9";

    private static string TwoGroupsBody =>
        "{\"groups\":["
        + "{\"id\":\"" + GroupId + "\",\"name\":\"Salon\",\"coordinatorId\":\"P1\","
        + "\"playerIds\":[\"P1\"],\"playbackState\":\"PLAYBACK_STATE_IDLE\"},"
        + "{\"id\":\"" + FixedGroupId + "\",\"name\":\"Sypialnia\",\"coordinatorId\":\"P9\","
        + "\"playerIds\":[\"P9\",\"P8\"],\"playbackState\":\"PLAYBACK_STATE_IDLE\"}],"
        + "\"players\":[{\"id\":\"P1\",\"name\":\"Salon\"},{\"id\":\"P9\",\"name\":\"Sypialnia\"},"
        + "{\"id\":\"P8\",\"name\":\"Gabinet\"}],\"partial\":false}";

    /// <summary>
    /// Trasy POTRZEBNE presetom, ktorych bazowa odpowiedz F3c nie zna: playlisty
    /// i POST playlist. Nieznana trasa nadal jest TWARDYM bledem pomiaru.
    /// </summary>
    private static HttpResponseMessage? PresetRoutes(HttpRequestMessage request, string body)
    {
        _ = body;
        var path = request.RequestUri!.AbsolutePath;
        if (request.Method == HttpMethod.Get && path.EndsWith("/playlists", StringComparison.Ordinal))
        {
            return Json(PlaylistsBody);
        }

        if (request.Method == HttpMethod.Post && path.EndsWith("/playlists", StringComparison.Ordinal))
        {
            return Json("{}");
        }

        return null;
    }

    /// <summary>Zapis presetu WPROST do modelu sesji - stan wejsciowy, nie mierzona droga.</summary>
    private static void PutPreset(RealHarness harness, SessionPresetEntry entry)
    {
        var presets = harness.Window.StateForTests.SessionPresets.EntriesBySession;
        if (!presets.TryGetValue("sonos", out var entries))
        {
            entries = [];
            presets["sonos"] = entries;
        }

        entries.RemoveAll(existing => existing.Slot == entry.Slot);
        entries.Add(entry);
    }

    private static SessionPresetEntry FavoritePreset(
        int slot = 1,
        string id = "ULU-DRUGI",
        string title = "Nokturny",
        string? household = HouseholdId,
        List<string>? fixedPlayers = null) => new()
        {
            Slot = slot,
            TargetId = id,
            TargetKind = SonosPresetKinds.Favorite,
            TargetTitle = title,
            SonosHouseholdId = household,
            SonosFixedPlayerIds = fixedPlayers
        };

    /// <summary>URUCHOMIENIE presetu PRODUKCYJNA metoda okna, z pompowaniem do konca.</summary>
    private static void FirePreset(RealHarness harness, SessionPresetEntry preset, string slotLabel = "1")
    {
        var task = harness.Window.ActivateSonosPresetForTests(preset, slotLabel);
        harness.Pump(task);
        harness.PumpQuietly(TimeSpan.FromMilliseconds(120));
    }

    private static string LastAnnouncement(RealHarness harness) =>
        harness.Announcements.Count == 0 ? "(nic nie powiedziano)" : harness.Announcements[^1];

    // ==================== P1 ====================

    /// <summary>
    /// PRZYPISANIE z PRAWDZIWEGO modalu: skrot Ctrl+Alt+Shift+P w oknie ulubionych
    /// (glowne okno jest wylaczone jako Owner, wiec droga musi byc WLASNA okna),
    /// potem PRAWDZIWY zapis na dysk i PONOWNY odczyt nowym store.
    /// </summary>
    private static string MeasureAssignFromRealModalPersistsAndSendsNothing()
    {
        using var harness = RealHarness.Create();
        harness.Enter();
        var postsBefore = harness.Handler.Posts.Count;

        string[] labels = [];
        SonosFavorite? picked = null;
        harness.RunFavoritesModal(window =>
        {
            labels = window.RowLabelsForTests.ToArray();
            // DRUGA z dwoch JEDNAKOWYCH etykiet: przypisanie musi wziac
            // identyfikator Z WIERSZA, nie pierwszy o tej nazwie.
            window.SelectForTests(1);
            picked = window.SelectedFavoriteForTests;
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(25) };
            Exception? dialogFailure = null;
            timer.Tick += (_, _) =>
            {
                var assignment = window.OwnedWindows.OfType<RadioPresetAssignmentWindow>().SingleOrDefault();
                if (assignment is null || !assignment.IsVisible) return;
                timer.Stop();
                try
                {
                    typeof(RadioPresetAssignmentWindow).GetMethod("SelectSlot", Instance)!.Invoke(assignment, [2, false]);
                    typeof(RadioPresetAssignmentWindow).GetMethod("Confirm", Instance)!.Invoke(assignment, null);
                }
                catch (Exception exception) { dialogFailure = exception; assignment.Close(); }
            };
            timer.Start();
            try { window.RequestPresetAssignmentForTests(); }
            finally { timer.Stop(); }
            if (dialogFailure is not null) throw dialogFailure;
        });

        if (labels.Length != 2 || labels[0] != labels[1])
        {
            throw new Exception("Lista nie miała dwóch JEDNAKOWYCH etykiet: " + string.Join(" | ", labels) + ".");
        }

        if (picked?.Id != "ULU-DRUGI")
        {
            throw new Exception("Modal oddał nie ten ulubiony: " + (picked?.Id ?? "brak") + ".");
        }

        // Flush only the save queued by the actual dialog; do not fabricate the entry.
        object?[] flushArgs = [TimeSpan.FromSeconds(10), null];
        if (typeof(MainWindow).GetMethod("FlushStateSave", Instance)!.Invoke(harness.Window, flushArgs) is not true)
            throw new Exception("Zapis z okna nie zakończył się: " + flushArgs[1]);

        var posts = harness.Handler.Posts.Count - postsBefore;
        if (posts != 0)
        {
            throw new Exception($"Samo PRZYPISANIE wysłało {posts} POST - preset nie ma nic odtwarzać.");
        }

        var reloaded = harness.ReloadStateForTests();
        var entry = reloaded.SessionPresets.EntriesBySession["sonos"].Single(candidate => candidate.Slot == 2);
        if (entry.TargetId != "ULU-DRUGI")
        {
            throw new Exception("Po ponownym odczycie preset ma identyfikator '" + entry.TargetId + "'.");
        }

        if (!SonosPresetKinds.IsFavorite(entry.TargetKind))
        {
            throw new Exception("Rodzaj materiału zgubiony: '" + entry.TargetKind + "'.");
        }

        return $"ZAZNACZONY '{entry.TargetId}' ({entry.TargetKind}) przeżył zapis i odczyt; 0 POST";
    }

    // ==================== P2 ====================

    private static string MeasurePresetFiresOnCurrentTarget()
    {
        using var harness = RealHarness.Create();
        harness.Enter();
        var postsBefore = harness.Handler.Posts.Count;

        FirePreset(harness, FavoritePreset());

        var posts = harness.Handler.Posts.Skip(postsBefore).ToArray();
        if (posts.Length != 1)
        {
            throw new Exception($"Preset wysłał {posts.Length} POST zamiast dokładnie jednego.");
        }

        var post = posts[0];
        if (!post.Uri.AbsolutePath.Contains(GroupId, StringComparison.Ordinal))
        {
            throw new Exception("POST nie poszedł na AKTUALNIE wybraną grupę: " + post.Uri.AbsolutePath + ".");
        }

        if (!post.Uri.AbsolutePath.EndsWith("/favorites", StringComparison.Ordinal))
        {
            throw new Exception("POST nie trafił w loadFavorite: " + post.Uri.AbsolutePath + ".");
        }

        if (!post.Body.Contains("\"ULU-DRUGI\"", StringComparison.Ordinal))
        {
            throw new Exception("Ciało nie niesie LITERALNIE zapisanego identyfikatora: " + post.Body);
        }

        if (!post.Body.Contains("INSERT", StringComparison.Ordinal)
            || !post.Body.Contains("\"playOnCompletion\":true", StringComparison.Ordinal))
        {
            throw new Exception("Ciało nie ma INSERT + playOnCompletion=true: " + post.Body);
        }

        if (post.Body.Contains("playModes", StringComparison.Ordinal))
        {
            throw new Exception("Ciało niesie playModes, których preset nie ustawia: " + post.Body);
        }

        var spoken = LastAnnouncement(harness);
        if (spoken.Contains("Preset 1", StringComparison.OrdinalIgnoreCase)
            || spoken.Contains("preset 1", StringComparison.Ordinal))
        {
            throw new Exception("Na sukces powiedziano numer slotu, nie nazwę: " + spoken);
        }

        if (!spoken.Contains("Nokturny", StringComparison.Ordinal))
        {
            throw new Exception("Na sukces nie powiedziano nazwy materiału: " + spoken);
        }

        return $"1 POST na {GroupId}, ciało z 'ULU-DRUGI' INSERT/true bez playModes; mowa: {spoken}";
    }

    // ==================== P3 ====================

    private static string MeasureFixedTargetResolvedGoesToItsGroup()
    {
        using var harness = RealHarness.Create();
        // DWIE grupy: biezaca (Salon, P1) i zapisany zestaw (Sypialnia, P9+P8).
        harness.Handler.RouteOverride = (request, body) =>
            request.Method == HttpMethod.Get
            && request.RequestUri!.AbsolutePath.EndsWith("/groups", StringComparison.Ordinal)
                ? Json(TwoGroupsBody)
                : PresetRoutes(request, body);
        harness.Enter();
        var postsBefore = harness.Handler.Posts.Count;

        FirePreset(harness, FavoritePreset(fixedPlayers: ["P8", "P9"]));

        var posts = harness.Handler.Posts.Skip(postsBefore).ToArray();
        if (posts.Length != 1)
        {
            throw new Exception($"Stały cel wysłał {posts.Length} POST zamiast jednego.");
        }

        var path = posts[0].Uri.AbsolutePath;
        if (!path.Contains(FixedGroupId, StringComparison.Ordinal))
        {
            throw new Exception("POST nie poszedł na grupę o zapisanym składzie: " + path + ".");
        }

        if (path.Contains(GroupId, StringComparison.Ordinal))
        {
            throw new Exception("POST poszedł na BIEŻĄCO wybraną grupę, a preset miał stały cel: " + path + ".");
        }

        if (harness.Window.SonosActiveGroup?.Id != GroupId)
        {
            throw new Exception("Uruchomienie na stałym celu PRZEŁĄCZYŁO bieżący wybór na "
                + (harness.Window.SonosActiveGroup?.Id ?? "brak") + ".");
        }

        return $"POST na {FixedGroupId} (zapisany skład P8+P9), bieżący wybór nadal {GroupId}";
    }

    // ==================== P4 ====================

    private static string MeasureFixedTargetGoneRefusesWithoutPost()
    {
        using var harness = RealHarness.Create();
        harness.Handler.RouteOverride = PresetRoutes;
        harness.Enter();
        var postsBefore = harness.Handler.Posts.Count;

        // Zapisany zestaw, ktorego w topologii NIE MA (P7 nie istnieje).
        FirePreset(harness, FavoritePreset(fixedPlayers: ["P7"]));

        var posts = harness.Handler.Posts.Count - postsBefore;
        if (posts != 0)
        {
            throw new Exception($"Brak zapisanego zestawu wysłał {posts} POST - zagrało gdzieś indziej.");
        }

        var spoken = LastAnnouncement(harness);
        if (!spoken.Contains("nie uruchomiłem", StringComparison.OrdinalIgnoreCase))
        {
            throw new Exception("Odmowa nie była jawna: " + spoken);
        }

        if (!spoken.Contains("przegrupowałem", StringComparison.OrdinalIgnoreCase))
        {
            throw new Exception("Odmowa nie mówi, że NIC nie przegrupowano: " + spoken);
        }

        return "0 POST, odmowa jawna i bez przegrupowania: " + spoken;
    }

    // ==================== P5 ====================

    private static string MeasureForeignHouseholdPresetSendsNothing()
    {
        using var harness = RealHarness.Create();
        harness.Handler.RouteOverride = PresetRoutes;
        harness.Enter();
        var postsBefore = harness.Handler.Posts.Count;

        // Ten sam opaque ID, ale zapisany dla DRUGIEGO domu.
        FirePreset(harness, FavoritePreset(household: OtherHouseholdId));

        var posts = harness.Handler.Posts.Count - postsBefore;
        if (posts != 0)
        {
            throw new Exception($"Preset z innego domu wysłał {posts} POST - stary ID poszedł w cudzy katalog.");
        }

        var spoken = LastAnnouncement(harness);
        if (!spoken.Contains("dom", StringComparison.OrdinalIgnoreCase))
        {
            throw new Exception("Nie powiedziano, że zapis jest z innego domu: " + spoken);
        }

        return "0 POST dla materiału z obcego domu: " + spoken;
    }

    // ==================== P6 ====================

    private static string MeasureRemovedOwnStreamRefusesWithoutPost()
    {
        using var harness = RealHarness.Create();
        harness.Handler.RouteOverride = PresetRoutes;
        harness.Enter();
        var postsBefore = harness.Handler.Posts.Count;

        // Preset na stacje, ktorej w stanie NIE MA (usunieta w Moje stacje).
        FirePreset(harness, new SessionPresetEntry
        {
            Slot = 3,
            TargetId = "stacja-ktorej-nie-ma",
            TargetKind = SonosPresetKinds.OwnStream,
            TargetTitle = "Usunięta stacja"
        }, slotLabel: "3");

        var posts = harness.Handler.Posts.Count - postsBefore;
        if (posts != 0)
        {
            throw new Exception($"Usunięta stacja wysłała {posts} POST.");
        }

        var spoken = LastAnnouncement(harness);
        if (!spoken.Contains("nie uruchomiłem", StringComparison.OrdinalIgnoreCase))
        {
            throw new Exception("Brak jawnej odmowy dla usuniętej stacji: " + spoken);
        }

        return "0 POST, instrukcja zamiast grania: " + spoken;
    }

    // ==================== P7 ====================

    private static string MeasureNoTargetSendsNothing()
    {
        using var harness = RealHarness.Create();
        harness.Handler.RouteOverride = PresetRoutes;
        harness.Enter();
        harness.ClearSonosTargetForTests();
        var postsBefore = harness.Handler.Posts.Count;

        FirePreset(harness, FavoritePreset());

        var posts = harness.Handler.Posts.Count - postsBefore;
        if (posts != 0)
        {
            throw new Exception($"Brak wybranego celu wysłał {posts} POST.");
        }

        var spoken = LastAnnouncement(harness);
        if (!spoken.Contains("głośnik", StringComparison.OrdinalIgnoreCase)
            && !spoken.Contains("grupę", StringComparison.OrdinalIgnoreCase))
        {
            throw new Exception("Nie odesłano do wyboru głośników: " + spoken);
        }

        return "0 POST i odesłanie do wyboru celu: " + spoken;
    }

    // ==================== P8 ====================

    /// <summary>
    /// SPOZNIONA proba: POST wstrzymany, w locie ZMIANA KONTA publiczna droga
    /// koordynatora. Stara proba NIE MA prawa domknac sie sukcesem ani wyslac
    /// czegokolwiek starym biletem po zmianie.
    /// </summary>
    private static string MeasureLatePresetDoesNotOverrideNewerIntent()
    {
        using var harness = RealHarness.Create();
        harness.Handler.RouteOverride = PresetRoutes;
        harness.Enter();

        var oldAccess = harness.Store.Access;
        var held = harness.Handler.HoldNextPost();
        var task = harness.Window.ActivateSonosPresetForTests(FavoritePreset(), "1");

        harness.PumpUntil(() => held.Arrived, TimeSpan.FromSeconds(5),
            "POST presetu nie dotarł do transportu");

        var spokenBeforeSwap = harness.Announcements.Count;
        // NOWSZY ZAMIAR: zmiana konta PRODUKCYJNA droga.
        harness.SwapAccount("KONTO-PRESET-B");
        held.Release();
        harness.PumpUntil(() => task.IsCompleted, TimeSpan.FromSeconds(15),
            "spóźniona próba presetu się nie rozliczyła");
        task.GetAwaiter().GetResult();
        harness.PumpQuietly(TimeSpan.FromMilliseconds(200));

        if (held.Authorization != oldAccess)
        {
            throw new Exception("Wstrzymany POST nie niósł biletu konta z czasu kliknięcia.");
        }

        // Po zmianie konta NIE WOLNO wyslac niczego starym biletem.
        var lateOldTicket = harness.Handler.Posts
            .Count(wire => wire.Authorization == oldAccess);
        if (lateOldTicket != 1)
        {
            throw new Exception($"Starym biletem poszło {lateOldTicket} POST zamiast dokładnie jednego "
                + "(tego z czasu kliknięcia).");
        }

        var spoken = string.Join(" | ", harness.Announcements.Skip(spokenBeforeSwap));
        if (harness.Announcements.Skip(spokenBeforeSwap).Any(message => message.Contains("Nokturny", StringComparison.Ordinal)
            && !message.Contains("nie", StringComparison.OrdinalIgnoreCase)))
        {
            throw new Exception("Spóźniona próba ogłosiła sukces po zmianie konta: " + spoken);
        }

        return "wstrzymany POST z biletem z czasu kliknięcia, po zmianie konta ZERO nowych starym "
            + "biletem; mowa: " + spoken;
    }

    // ==================== P9 ====================

    private static string MeasureAssignNeverPlaysAndRemovalSticks()
    {
        using var harness = RealHarness.Create();
        harness.Handler.RouteOverride = PresetRoutes;
        harness.Enter();
        var postsBefore = harness.Handler.Posts.Count;

        PutPreset(harness, FavoritePreset(slot: 1));
        PutPreset(harness, FavoritePreset(slot: 2, id: "ULU-PIERWSZY", title: "Zostaje"));
        harness.SaveStateForTests();

        if (harness.Handler.Posts.Count != postsBefore)
        {
            throw new Exception("Ustawianie presetu odtworzyło coś - poszedł POST.");
        }

        // USUNIECIE przypisania.
        var entries = harness.Window.StateForTests.SessionPresets.EntriesBySession["sonos"];
        entries.RemoveAll(entry => entry.Slot == 1);
        harness.SaveStateForTests();

        var reloaded = harness.ReloadStateForTests();
        var remaining = reloaded.SessionPresets.EntriesBySession["sonos"];
        if (remaining.Any(entry => entry.Slot == 1))
        {
            throw new Exception("Usunięte przypisanie wróciło po ponownym odczycie.");
        }

        if (remaining.SingleOrDefault(entry => entry.Slot == 2) is null)
        {
            throw new Exception("Usunięcie slotu 1 zabrało też slot 2.");
        }

        if (harness.Handler.Posts.Count != postsBefore)
        {
            throw new Exception("Zapis/usunięcie presetu wysłało POST.");
        }

        return "ustawienie i usunięcie: 0 POST; slot 1 nie wrócił, slot 2 nietknięty";
    }
}
