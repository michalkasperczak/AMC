using System.Net;
using System.Net.Http;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Threading;
using AccessibleMediaController.Core.Commands;
using AccessibleMediaController.Windows;

/// <summary>
/// WYBOR GLOSNIKOW (Ctrl+F5 -> "Wybierz glosniki") na RZECZYWISTYM Main/Owner/
/// kliencie nad syntetycznym transportem. Mierzymy DROGE, nie atrape:
/// prawdziwe okno celu, prawdziwe okno glosnikow, prawdziwy POST i prawdziwy
/// GET weryfikacyjny.
///
/// SZESC przypadkow, nie macierz: Wszystkie(setGroupMembers), pojedynczy
/// glosnik bez starego koordynatora (setGroupMembers), zaznaczenie bez zmian(0 POST), anulowanie
/// konfliktu(0 POST), spozniona zmiana konta i GET, ktory NIE potwierdza skladu.
/// </summary>
internal static partial class SonosFavoritePlayRealOwnerTests
{
    private const string SpeakerGroupsThree =
        "{\"groups\":[{\"id\":\"GRUPA-SALON:1\",\"name\":\"Salon\",\"coordinatorId\":\"P1\","
        + "\"playerIds\":[\"P1\"],\"playbackState\":\"PLAYBACK_STATE_PLAYING\"},"
        + "{\"id\":\"GRUPA-KUCHNIA:2\",\"name\":\"Kuchnia\",\"coordinatorId\":\"P2\","
        + "\"playerIds\":[\"P2\"],\"playbackState\":\"PLAYBACK_STATE_PLAYING\"},"
        + "{\"id\":\"GRUPA-BIURO:3\",\"name\":\"Biuro\",\"coordinatorId\":\"P3\","
        + "\"playerIds\":[\"P3\"],\"playbackState\":\"PLAYBACK_STATE_IDLE\"}],"
        + "\"players\":[{\"id\":\"P1\",\"name\":\"Salon\"},{\"id\":\"P2\",\"name\":\"Kuchnia\"},"
        + "{\"id\":\"P3\",\"name\":\"Biuro\"}],\"partial\":false}";

    /// <summary>Po POST: Salon+Kuchnia+Biuro w JEDNEJ grupie Salonu.</summary>
    private const string SpeakerGroupsMerged =
        "{\"groups\":[{\"id\":\"GRUPA-SALON:1\",\"name\":\"Salon\",\"coordinatorId\":\"P1\","
        + "\"playerIds\":[\"P1\",\"P2\",\"P3\"],\"playbackState\":\"PLAYBACK_STATE_PLAYING\"}],"
        + "\"players\":[{\"id\":\"P1\",\"name\":\"Salon\"},{\"id\":\"P2\",\"name\":\"Kuchnia\"},"
        + "{\"id\":\"P3\",\"name\":\"Biuro\"}],\"partial\":false}";

    /// <summary>Po zmianie składu: następca ma nowy ID i tylko Biuro; pozostałe grupy są osobno.</summary>
    private const string SpeakerGroupsCreated =
        "{\"groups\":[{\"id\":\"GRUPA-SALON:1\",\"name\":\"Salon\",\"coordinatorId\":\"P1\","
        + "\"playerIds\":[\"P1\"],\"playbackState\":\"PLAYBACK_STATE_IDLE\"},"
        + "{\"id\":\"GRUPA-KUCHNIA:2\",\"name\":\"Kuchnia\",\"coordinatorId\":\"P2\","
        + "\"playerIds\":[\"P2\"],\"playbackState\":\"PLAYBACK_STATE_PLAYING\"},"
        + "{\"id\":\"GRUPA-NOWA:9\",\"name\":\"Biuro\",\"coordinatorId\":\"P3\","
        + "\"playerIds\":[\"P3\"],\"playbackState\":\"PLAYBACK_STATE_PLAYING\"}],"
        + "\"players\":[{\"id\":\"P1\",\"name\":\"Salon\"},{\"id\":\"P2\",\"name\":\"Kuchnia\"},"
        + "{\"id\":\"P3\",\"name\":\"Biuro\"}],\"partial\":false}";

    internal static void RunSpeakerSelection()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var failures = new List<string>();
                foreach (var mode in new[] { "all", "single", "unchanged", "conflict-cancel",
                    "changed-account", "unconfirmed", "partial", "verification-failed", "changed-during-question" })
                {
                    try { MeasureSpeakerSelection(mode); }
                    catch (Exception e) { failures.Add(mode + ": " + e.Message); Console.Error.WriteLine("FAIL: " + failures[^1]); }
                }
                if (failures.Count > 0) throw new Exception(string.Join(" | ", failures));
            }
            catch (Exception e) { failure = e; }
            finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(240)))
        {
            throw new Exception("Limit pomiaru wyboru głośników Sonosa");
        }

        if (failure is not null) throw failure;
        Console.WriteLine("OK: wybór głośników Sonosa - rzeczywisty właściciel/HTTP, "
            + "pełny skład/setGroupMembers, pojedynczy głośnik, 0 POST bez zmian, "
            + "konflikt, zmiana konta/topologii i weryfikacja wyniku (9 przypadków)");
    }

    private static void MeasureSpeakerSelection(string mode)
    {
        Console.Error.WriteLine("WYBÓR GŁOŚNIKÓW: " + mode);
        using var h = RealHarness.Create();

        var postCount = 0;
        var changedDuringQuestion = false;
        var announcementsAfterSwap = -1;
        string? postPath = null;
        string? postBody = null;
        h.Handler.RouteOverride = (request, body) =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Get && path.EndsWith("/groups", StringComparison.Ordinal))
            {
                // GET PO POST oddaje RZECZYWISTY sklad; przed POST - stan wyjsciowy.
                if (postCount == 0)
                {
                    var before = changedDuringQuestion ? SpeakerGroupsMerged : SpeakerGroupsThree;
                    if (mode == "single") before = before.Replace(
                        "\"playerIds\":[\"P1\"],\"playbackState\":\"PLAYBACK_STATE_PLAYING\"",
                        "\"playerIds\":[\"P1\"],\"playbackState\":\"PLAYBACK_STATE_PAUSED\"", StringComparison.Ordinal);
                    return Json(before);
                }
                if (mode == "verification-failed") return Json("{}", HttpStatusCode.ServiceUnavailable);
                if (mode == "partial") return Json(SpeakerGroupsMerged.Replace("\"partial\":false", "\"partial\":true", StringComparison.Ordinal));
                if (mode == "unconfirmed") return Json(SpeakerGroupsThree); // skladu NIE potwierdza
                return Json(mode == "single" ? SpeakerGroupsCreated.Replace(
                    "\"playerIds\":[\"P3\"],\"playbackState\":\"PLAYBACK_STATE_PLAYING\"",
                    "\"playerIds\":[\"P3\"],\"playbackState\":\"PLAYBACK_STATE_PAUSED\"", StringComparison.Ordinal) : SpeakerGroupsMerged);
            }

            // PRAWDZIWE sciezki klienta: .../groups/{id}/groups/setGroupMembers
            // oraz .../households/{id}/groups/createGroup.
            if (request.Method == HttpMethod.Post
                && (path.EndsWith("/createGroup", StringComparison.Ordinal)
                    || path.EndsWith("/setGroupMembers", StringComparison.Ordinal)))
            {
                postCount++;
                postPath = path;
                postBody = body;
                if (mode == "single")
                {
                    return Json("{\"group\":{\"id\":\"GRUPA-NOWA:9\",\"name\":\"Biuro\","
                        + "\"coordinatorId\":\"P3\",\"playerIds\":[\"P3\"]}}");
                }

                return Json("{\"group\":{\"id\":\"GRUPA-SALON:1\",\"name\":\"Salon\","
                    + "\"coordinatorId\":\"P1\",\"playerIds\":[\"P1\",\"P2\",\"P3\"]}}");
            }

            return null;
        };

        h.Enter();
        h.Window.StateForTests.Sonos.SelectedGroupId = "GRUPA-SALON:1";

        // Konflikt: Kuchnia GRA. Pytanie podstawiamy, zeby zmierzyc DECYZJE.
        var conflictAsked = 0;
        h.Window.ConfirmSonosSpeakerConflictOverrideForTests = question =>
        {
            conflictAsked++;
            Console.Error.WriteLine("  pytanie o konflikt: " + question);
            if (mode == "changed-during-question") changedDuringQuestion = true;
            return mode != "conflict-cancel";
        };

        var held = mode == "changed-account" ? h.Handler.HoldNextPost() : null;
        var phase = 0;
        var released = false;
        var finished = false;
        Exception? inside = null;
        var deadline = DateTime.UtcNow.AddSeconds(30);
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(20) };
        timer.Tick += (_, _) =>
        {
            try
            {
                if (DateTime.UtcNow > deadline) throw new Exception("Brak postępu w fazie " + phase);

                if (phase == 0 && h.Window.OpenSonosTargetWindowForTests is { IsVisible: true } target)
                {
                    phase = 1;
                    Console.Error.WriteLine("  okno celu gotowe, Wybierz głośniki");
                    if (h.Handler.Posts.Count != 0) throw new Exception("Samo otwarcie wysłało POST");
                    // ZWIEKSZAMY etap PRZED akcja i otwieramy modal przez BeginInvoke:
                    // zagniezdzony ShowDialog w TYM ticku nigdy by sie nie skonczyl.
                    target.Dispatcher.BeginInvoke(new Action(target.RequestSpeakersForTests));
                }
                else if (phase == 1 && h.Window.OpenSonosSpeakersWindowForTests is { IsVisible: true } speakers)
                {
                    phase = 2;
                    Console.Error.WriteLine("  okno głośników gotowe: " + speakers.SelectionSummaryForTests);
                    if (h.Handler.Posts.Count != 0) throw new Exception("Otwarcie głośników wysłało POST");

                    // POLE WYBORU jest PRAWDZIWE: rola i stan z automatyki UI.
                    MeasureCheckBoxAccessibility(speakers);

                    if (speakers.RowCountForTests != 3)
                    {
                        throw new Exception("Lista ma " + speakers.RowCountForTests + " pozycji zamiast 3");
                    }

                    // CZLONEK aktywnej grupy JUZ zaznaczony, pozostali nie.
                    var initial = speakers.RowCheckedForTests;
                    if (!initial[0] || initial[1] || initial[2])
                    {
                        throw new Exception("Złe zaznaczenie początkowe: " + string.Join(",", initial));
                    }

                    if (mode == "single")
                    {
                        // Stary koordynator wypada: pełny nowy zestaw zawiera tylko Biuro.
                        speakers.SetRowForTests(0, false);
                        speakers.SetRowForTests(2, true);
                    }
                    else if (mode != "unchanged")
                    {
                        // "Zaznacz wszystkie" zmienia TYLKO zaznaczenie.
                        speakers.SelectAllForTests();
                        if (h.Handler.Posts.Count != 0)
                        {
                            throw new Exception("Zaznacz wszystkie wysłało POST");
                        }
                    }

                    speakers.Dispatcher.BeginInvoke(new Action(speakers.ApplyForTests));
                }
                else if (phase == 2)
                {
                    if (mode == "unchanged" && h.Window.OpenSonosSpeakersWindowForTests is { } still)
                    {
                        // NIEZMIENIONE zaznaczenie: okno ZOSTAJE i mowi dlaczego.
                        if (!still.StatusForTests.Contains("nic nie wysłano", StringComparison.Ordinal))
                        {
                            throw new Exception("Brak uczciwego komunikatu: " + still.StatusForTests);
                        }

                        phase = 3;
                        finished = true;
                        still.Close();
                        return;
                    }

                    if (h.Window.OpenSonosSpeakersWindowForTests is not null) return;

                    if (held is { Arrived: true } && !released)
                    {
                        released = true;
                        h.SwapAccount("SPEAKERS-B");
                        announcementsAfterSwap = h.Announcements.Count;
                        held.Release();
                    }

                    if (h.Window.LastSonosSpeakersTaskForTests is { IsCompleted: false }) return;
                    h.Window.LastSonosSpeakersTaskForTests?.GetAwaiter().GetResult();
                    phase = 3;
                    finished = true;
                }
            }
            catch (Exception e)
            {
                inside = e;
                timer.Stop();
                held?.Release();
                foreach (Window dialog in h.Window.OwnedWindows.Cast<Window>().ToArray()) dialog.Close();
            }
        };

        timer.Start();
        try
        {
            // Ctrl+F5 zwraca sie po ZAMKNIECIU okna celu, a dalsza droga glosnikow
            // biegnie dalej. Pompujemy PRAWDZIWA petla komunikatow, dopoki krok
            // maszyny nie zglosi konca - inaczej timer ginie w polowie drogi.
            h.ExecuteCommand(CommandIds.ChooseSonosTarget);

            var frame = new DispatcherFrame();
            var guard = new DispatcherTimer(DispatcherPriority.Normal)
            {
                Interval = TimeSpan.FromMilliseconds(50)
            };
            var pumpDeadline = DateTime.UtcNow.AddSeconds(30);
            guard.Tick += (_, _) =>
            {
                if (!finished && inside is null && DateTime.UtcNow < pumpDeadline) return;
                guard.Stop();
                frame.Continue = false;
            };
            guard.Start();
            Dispatcher.PushFrame(frame);
            guard.Stop();
        }
        finally { timer.Stop(); held?.Release(); }

        if (inside is not null) throw inside;
        if (!finished) throw new Exception("Nie przeszliśmy rzeczywistą drogą wyboru głośników");

        // POST: DOKLADNIE jeden tam, gdzie zmiana byla zamierzona; zero indziej.
        var expectedPosts = mode is "unchanged" or "conflict-cancel" or "changed-during-question" ? 0 : 1;
        if (postCount != expectedPosts)
        {
            throw new Exception($"{mode}: {postCount} POST zamiast {expectedPosts}");
        }

        // PYTANIE o konflikt TYLKO gdy dolaczany glosnik GRA. W "single" dochodzi
        // samo Biuro, ktore jest Idle - bezkonfliktowy Idle NIE pyta.
        var expectedAsk = mode is "unchanged" or "single" ? 0 : 1;
        if (conflictAsked != expectedAsk)
        {
            throw new Exception($"{mode}: {conflictAsked} pytań o konflikt zamiast {expectedAsk}");
        }

        // ZADNEGO Play/Stop i zadnego ladowania materialu na tej drodze.
        if (h.Handler.Requests.Any(w => w.Uri.AbsolutePath.Contains("/playback/play", StringComparison.Ordinal)
            || w.Uri.AbsolutePath.Contains("/playback/pause", StringComparison.Ordinal)
            || w.Uri.AbsolutePath.Contains("loadStreamUrl", StringComparison.Ordinal)
            || w.Uri.AbsolutePath.Contains("loadFavorite", StringComparison.Ordinal)))
        {
            throw new Exception("Wybór głośników ruszył materiał albo odtwarzanie");
        }

        if (expectedPosts == 1)
        {
            if (postPath is null || !postPath.EndsWith("/setGroupMembers", StringComparison.Ordinal))
                throw new Exception("Wybór miejsc musi zmieniać pełny skład, nie kopiować źródło: " + postPath);
            if (postBody is null || postBody.Contains("musicContextGroupId", StringComparison.Ordinal))
                throw new Exception("Nieoczekiwane kopiowanie kontekstu muzyki");
        }

        if (mode == "all")
        {
            // SKLAD DOWIEDZIONY swiezym GET: PELNE nazwy w komunikacie.
            var spoken = string.Join(" | ", h.Announcements);
            if (spoken.Contains("Muzyka nie została przerwana", StringComparison.Ordinal))
                throw new Exception("Sam skład grupy nie dowodzi nieprzerwanego dźwięku");
            foreach (var name in new[] { "Salon", "Kuchnia", "Biuro" })
            {
                if (!spoken.Contains(name, StringComparison.Ordinal))
                {
                    throw new Exception("Komunikat bez pełnej nazwy " + name + ": " + spoken);
                }
            }

            if (!string.Equals(h.Window.StateForTests.Sonos.SelectedGroupId, "GRUPA-SALON:1",
                StringComparison.Ordinal))
            {
                throw new Exception("Cel po zmianie składu: "
                    + h.Window.StateForTests.Sonos.SelectedGroupId);
            }
        }

        if (mode == "single")
        {
            // CEL przeszedl na NASTEPCZA grupe Z ODPOWIEDZI, nie z nazwy.
            if (h.Window.SonosActiveGroup?.PlaybackState != AccessibleMediaController.Core.Sonos.SonosPlaybackState.Paused)
                throw new Exception("Nie zachowano odczytanego stanu pauzy nowej grupy");
            if (!string.Equals(h.Window.StateForTests.Sonos.SelectedGroupId, "GRUPA-NOWA:9",
                StringComparison.Ordinal))
            {
                throw new Exception("Cel nie przeszedł na nową grupę: "
                    + h.Window.StateForTests.Sonos.SelectedGroupId);
            }
        }

        if (mode is "unconfirmed" or "partial" or "verification-failed")
        {
            // GET NIE pokazal zadanego skladu: ZADNEGO "Gotowe".
            var spoken = string.Join(" | ", h.Announcements);
            if (spoken.Contains("więc nic nie wysłano", StringComparison.Ordinal)
                || spoken.Contains("Wybrano:", StringComparison.Ordinal))
                throw new Exception("Nieprawdziwy wynik po wysłanym POST: " + spoken);
            if (!spoken.Contains("wynik niepotwierdzony", StringComparison.Ordinal))
            {
                throw new Exception("Niepotwierdzony skład ogłoszony jako sukces: " + spoken);
            }
        }

        if (mode == "changed-account")
        {
            // SPOZNIONA zmiana konta: wyniku starego kontekstu nie publikujemy.
            if (announcementsAfterSwap < 0 || h.Announcements.Count != announcementsAfterSwap)
                throw new Exception("Spóźniony komunikat starego konta: "
                    + string.Join(" | ", h.Announcements.Skip(Math.Max(announcementsAfterSwap, 0))));
            if (string.Equals(h.Window.StateForTests.Sonos.SelectedGroupId, "GRUPA-NOWA:9",
                StringComparison.Ordinal))
            {
                throw new Exception("Wynik starego konta trafił do nowego kontekstu");
            }
        }

        Console.Error.WriteLine("  OK " + mode + ": POST=" + postCount + ", pytań=" + conflictAsked);
    }

    /// <summary>
    /// DOWOD, ze wiersz to PRAWDZIWE pole wyboru: automatyka UI podaje role
    /// CheckBox i stan przelaczenia NA TEJ kontrolce, a Spacja ten stan zmienia.
    /// Element listy bez stanu by tego nie przeszedl.
    /// </summary>
    private static void MeasureSpeakerCheckBox(SonosSpeakerSelectionWindow speakers)
    {
        var box = speakers.CheckBoxForTests(1)
            ?? throw new Exception("Wiersz nie ma prawdziwego pola wyboru");

        var peer = UIElementAutomationPeer.CreatePeerForElement(box)
            ?? throw new Exception("Pole wyboru nie ma elementu automatyki");

        if (peer.GetAutomationControlType() != AutomationControlType.CheckBox)
        {
            throw new Exception("Rola w automatyce: " + peer.GetAutomationControlType());
        }

        var name = peer.GetName();
        if (!string.Equals(name, "Kuchnia", StringComparison.Ordinal))
        {
            throw new Exception("Nazwa pola wyboru w automatyce: '" + name + "'");
        }

        if (peer.GetPattern(PatternInterface.Toggle) is not IToggleProvider toggle)
        {
            throw new Exception("Pole wyboru nie udostępnia stanu przełączenia");
        }

        if (toggle.ToggleState != ToggleState.Off)
        {
            throw new Exception("Stan przed spacją: " + toggle.ToggleState);
        }

        // SPACJA przez prawdziwe zdarzenie klawiatury na TYM polu wyboru.
        RaiseSpace(speakers, box);
        if (toggle.ToggleState != ToggleState.On)
        {
            throw new Exception("Spacja nie zmieniła stanu pola wyboru: " + toggle.ToggleState);
        }

        // Wracamy do stanu wyjsciowego - pomiar nie moze zmieniac zamiaru testu.
        RaiseSpace(speakers, box);
        if (toggle.ToggleState != ToggleState.Off)
        {
            throw new Exception("Druga spacja nie odznaczyła pola: " + toggle.ToggleState);
        }
    }

    /// <summary>
    /// SPACJA jako PRAWDZIWE zdarzenie wejscia na podanej kontrolce - to samo
    /// zdarzenie, ktore wysyla klawiatura uzytkownika.
    /// </summary>
    private static void RaiseSpace(Window window, System.Windows.Controls.CheckBox box)
    {
        box.Focus();
        var source = PresentationSource.FromVisual(window)
            ?? throw new Exception("Okno nie ma źródła prezentacji");
        var args = new System.Windows.Input.KeyEventArgs(
            System.Windows.Input.Keyboard.PrimaryDevice, source, 0, System.Windows.Input.Key.Space)
        {
            RoutedEvent = System.Windows.Input.Keyboard.KeyDownEvent
        };
        box.RaiseEvent(args);
        // CheckBox przelacza sie na KeyUp dla spacji.
        var up = new System.Windows.Input.KeyEventArgs(
            System.Windows.Input.Keyboard.PrimaryDevice, source, 0, System.Windows.Input.Key.Space)
        {
            RoutedEvent = System.Windows.Input.Keyboard.KeyUpEvent
        };
        box.RaiseEvent(up);
    }

    private static void MeasureCheckBoxAccessibility(SonosSpeakerSelectionWindow speakers) =>
        MeasureSpeakerCheckBox(speakers);
}
