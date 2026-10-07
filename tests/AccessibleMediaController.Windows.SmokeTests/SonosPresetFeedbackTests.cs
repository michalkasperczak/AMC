using System.Diagnostics;
using System.Windows.Threading;

internal static partial class SonosFavoritePlayRealOwnerTests
{
    internal static void ShowFinalForNvda(int seconds)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                using var h = RealHarness.Create();
                h.Enter();
                h.Window.AnnouncementSinkForTests = null;
                var station = new AccessibleMediaController.Core.Configuration.SonosOwnStreamSettings
                { Id = "station-live", Name = "Radio kontrolne", StreamUrl = "https://radio.example.invalid/live?a=1&b=2" };
                h.Window.StateForTests.Sonos.OwnStreams.Add(station);
                // DRUGI WIERSZ w Moich stacjach: bez niego pokaz nie odroznia
                // POWROTU NA SWOJE MIEJSCE od zwyklego otwarcia listy od zera.
                h.Window.StateForTests.Sonos.OwnStreams.Add(
                    new AccessibleMediaController.Core.Configuration.SonosOwnStreamSettings
                    {
                        Id = "station-live-2",
                        Name = "Druga stacja kontrolna",
                        StreamUrl = "https://radio.example.invalid/live2"
                    });
                PutPreset(h, FavoritePreset());
                PutPreset(h, new AccessibleMediaController.Core.Configuration.SessionPresetEntry
                { Slot = 2, TargetId = station.Id, TargetKind = AccessibleMediaController.Core.Sonos.SonosPresetKinds.OwnStream, TargetTitle = station.Name });
                h.Handler.RouteOverride = (request, body) =>
                {
                    var path = request.RequestUri!.AbsolutePath;
                    if (request.Method == System.Net.Http.HttpMethod.Post && path.EndsWith("/playbackSession", StringComparison.Ordinal)) return Json("{\"sessionId\":\"SESSION-LIVE\"}");
                    if (request.Method == System.Net.Http.HttpMethod.Post && path.EndsWith("/loadStreamUrl", StringComparison.Ordinal)) return Json("{}");
                    // ULUBIONE I PLAYLISTY po DWA wiersze: pokaz ma objac WSZYSTKIE
                    // TRZY podlisty, nie tylko Moje stacje.
                    // URUCHOMIENIE PLAYLISTY: POST /groups/{id}/playlists. Atrapa
                    // musi je obsluzyc OSOBNO od GET /playlists, inaczej pokaz
                    // mowi blad zamiast krotkiej nazwy. To luka APARATURY pokazu,
                    // nie kodu produkcyjnego.
                    if (request.Method == System.Net.Http.HttpMethod.Post && path.EndsWith("/playlists", StringComparison.Ordinal)) return Json("{}");

                    if (request.Method == System.Net.Http.HttpMethod.Get && path.EndsWith("/playlists", StringComparison.Ordinal))
                    {
                        return Json("{\"version\":\"PL1\",\"playlists\":["
                            + "{\"id\":\"LISTA-PIERWSZA\",\"name\":\"Pierwsza playlista kontrolna\",\"type\":\"playlist\",\"trackCount\":3},"
                            + "{\"id\":\"LISTA-DRUGA\",\"name\":\"Druga playlista kontrolna\",\"type\":\"playlist\",\"trackCount\":5}]}");
                    }

                    if (request.Method == System.Net.Http.HttpMethod.Get && path.EndsWith("/favorites", StringComparison.Ordinal))
                    {
                        return Json("{\"version\":\"FAV1\",\"items\":["
                            + "{\"id\":\"FAV-PIERWSZY\",\"name\":\"Pierwsze ulubione kontrolne\",\"description\":\"stacja\"},"
                            + "{\"id\":\"FAV-DRUGI\",\"name\":\"Drugie ulubione kontrolne\",\"description\":\"stacja\"}]}");
                    }

                    return null;
                };
                var radio = h.Window.SessionsForTests.FindSession("radio")!;
                radio.ReplaceItems(new[] {
                    new AccessibleMediaController.Core.Sessions.MediaItem { Id="test-a", Title="Stacja kontrolna A", Source="https://radio.example.invalid/a?k=1&n=2", Kind=AccessibleMediaController.Core.Sessions.MediaItemKind.Station, IsInLibrary=true },
                    new AccessibleMediaController.Core.Sessions.MediaItem { Id="test-b", Title="Stacja kontrolna B", Source="https://radio.example.invalid/b", Kind=AccessibleMediaController.Core.Sessions.MediaItemKind.Station, IsInLibrary=true }
                });
                Console.WriteLine("GUI_READY PID=" + Environment.ProcessId + " RADIO_SLOT=" + h.Window.SessionsForTests.FindSlot("radio") + " SONOS_SLOT=" + h.Window.SessionsForTests.FindSlot("sonos"));
                h.PumpUntil(() => !h.Window.IsVisible, TimeSpan.FromSeconds(seconds), "Limit pokazu");
                foreach (System.Windows.Window dialog in h.Window.OwnedWindows.Cast<System.Windows.Window>().ToArray()) dialog.Close();
                Console.WriteLine("GUI_END POST=" + h.Handler.Posts.Count);
            }
            catch (Exception e) { failure = e; }
            finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        });
        thread.SetApartmentState(ApartmentState.STA);thread.IsBackground=true;thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(seconds + 45))) throw new Exception("Limit pokazu");
        if (failure is not null) throw failure;
    }

    private static string MeasurePresetFeedbackBeforeNetwork()
    {
        using var h = RealHarness.Create();
        h.Enter();
        var preset = FavoritePreset();
        var filter = (System.Windows.Controls.TextBox)h.Window.FindName("FilterBox");
        filter.Focus();
        System.Windows.Input.Keyboard.Focus(filter);
        var held = h.Handler.HoldNextGet("/favorites");
        var before = h.Announcements.Count;
        var clock = Stopwatch.StartNew();
        var task = h.Window.ActivateSonosPresetForTests(preset, "1");
        try
        {
            h.PumpUntil(() => held.Arrived, TimeSpan.FromSeconds(5), "GET nie dotarł");
            if (task.IsCompleted) throw new Exception("Próba nie zatrzymała odpowiedzi sieci");
            if (LastAnnouncement(h) != preset.TargetTitle)
                throw new Exception("Cisza do końca sieci: " + LastAnnouncement(h));
            var feedbackMs = clock.Elapsed.TotalMilliseconds;
            var dispatcherAlive = false;
            h.Window.Dispatcher.BeginInvoke(DispatcherPriority.Input,
                new Action(() => dispatcherAlive = true));
            h.PumpUntil(() => dispatcherAlive, TimeSpan.FromSeconds(2), "UI czeka na sieć");
            held.Release();
            h.Pump(task);
            if (!ReferenceEquals(System.Windows.Input.Keyboard.FocusedElement, filter))
                throw new Exception("Preset zabrał fokus z pola filtra");
            var selected = h.Announcements.Skip(before).Count(x => x == preset.TargetTitle);
            if (selected != 1) throw new Exception("Nazwa presetu padła " + selected + " razy zamiast raz");
            if (h.Announcements.Skip(before).Any(x => x.Contains("Przyjęto polecenie", StringComparison.Ordinal)))
                throw new Exception("Techniczne potwierdzenie po nazwie");
            return $"Nazwa po {feedbackMs:F1} ms przed zwolnieniem GET; UI działa; nazwa raz";
        }
        finally
        {
            held.Release();
            h.Pump(task);
        }
    }
}
