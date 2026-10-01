using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using AccessibleMediaController.Core.Commands;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Windows;

internal static partial class SonosFavoritePlayRealOwnerTests
{
    internal static void RunOwnStreams()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                MeasureOwnStream("accepted");
                MeasureOwnStream("invalid");
                MeasureOwnStream("no-group");
                MeasureOwnStream("changed-account");
            }
            catch (Exception e) { failure = e; }
            finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(150))) throw new Exception("Limit pomiaru własnych stacji Sonosa");
        if (failure is not null) throw failure;
        Console.WriteLine("OK: własne stacje Sonosa - rzeczywisty właściciel/HTTP, adres, brak celu i zmiana konta (4 przypadki)");
    }

    private static void MeasureOwnStream(string mode)
    {
        Console.Error.WriteLine("WŁASNE STACJE: " + mode);
        using var h = RealHarness.Create();
        h.Handler.RouteOverride = (request, body) =>
        {
            if (request.Method == HttpMethod.Post && request.RequestUri!.AbsolutePath.EndsWith("/playbackSession", StringComparison.Ordinal))
                return Json("{\"sessionId\":\"SESSION-TEST\"}");
            if (request.Method == HttpMethod.Post && request.RequestUri!.AbsolutePath.EndsWith("/loadStreamUrl", StringComparison.Ordinal))
                return Json("{}");
            return null;
        };
        h.Enter();
        Console.Error.WriteLine("  MainWindow gotowe");
        const string url = "https://radio.example.invalid/live?Key=AbC%2Fz&b=2";
        h.Window.StateForTests.Sonos.OwnStreams.Add(new SonosOwnStreamSettings
            { Id = "station-one", Name = "Stacja próbna", StreamUrl = mode == "invalid" ? "file:///secret" : url });
        if (mode == "no-group") h.Window.StateForTests.Sonos.SelectedGroupId = null;
        var held = mode == "changed-account" ? h.Handler.HoldNextPost() : null;
        var phase = 0;
        var released = false;
        var finished = false;
        Exception? inside = null;
        var deadline = DateTime.UtcNow.AddSeconds(25);
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(20) };
        timer.Tick += (_, _) =>
        {
            try
            {
                if (DateTime.UtcNow > deadline) throw new Exception("Brak postępu w fazie " + phase);
                if (phase == 0 && h.Window.OpenSonosLibraryWindowForTests is { IsVisible: true } library)
                {
                    phase = 1;
                    Console.Error.WriteLine("  Biblioteka gotowa");
                    library.SelectRowForTests(2);
                    // Oddaj tick: otwierana kategoria ma zagnieżdżony ShowDialog.
                    // Ten sam DispatcherTimer nie wywoła następnego ticku, dopóki bieżący trwa.
                    library.Dispatcher.BeginInvoke(new Action(library.OpenSelectedForTests));
                }
                else if (phase == 1 && h.Window.OpenSonosOwnStreamsWindowForTests is { IsVisible: true } dialog)
                {
                    phase = 2;
                    Console.Error.WriteLine("  Moje stacje gotowe, Enter");
                    if (h.Handler.Posts.Count != 0) throw new Exception("Samo otwarcie wysłało POST");
                    var list = (ListBox)dialog.FindName("StationsList");
                    list.SelectedIndex = 0;
                    LibraryFixture.RaiseKey(dialog, list, System.Windows.Input.Key.Enter);
                }
                else if (phase == 2 && h.Window.OpenSonosOwnStreamsWindowForTests is { } current)
                {
                    if (held is { Arrived: true } && !released)
                    {
                        released = true;
                        h.SwapAccount("OWN-RADIO-B");
                        held.Release();
                    }
                    if (current.LastPlayTaskForTests is { IsCompleted: false }) return;
                    current.LastPlayTaskForTests?.GetAwaiter().GetResult();
                    if (mode == "accepted" && !current.StatusForTests.Contains("Sonos przyjął stację", StringComparison.Ordinal))
                        throw new Exception("Brak uczciwego wyniku: " + current.StatusForTests);
                    if (mode != "accepted" && string.IsNullOrWhiteSpace(current.StatusForTests))
                        throw new Exception("Odmowa nie zostawiła komunikatu");
                    phase = 3;
                    finished = true;
                    current.Close();
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
        try { h.ExecuteCommand(CommandIds.ViewLibrary); }
        finally { timer.Stop(); held?.Release(); }
        if (inside is not null) throw inside;
        if (!finished) throw new Exception("Nie otwarto własnych stacji rzeczywistą drogą Biblioteki");
        var posts = h.Handler.Posts;
        var expected = mode == "accepted" ? 2 : mode == "changed-account" ? 1 : 0;
        if (posts.Count != expected) throw new Exception($"{mode}: {posts.Count} POST zamiast {expected}");
        if (h.Handler.Requests.Any(w => w.Uri.Host == "radio.example.invalid"))
            throw new Exception("AMC pobrało adres radia zamiast tylko przekazać go Sonosowi");
        if (mode == "accepted")
        {
            if (!posts[0].Uri.AbsolutePath.EndsWith("/groups/" + GroupId + "/playbackSession", StringComparison.Ordinal))
                throw new Exception("Utworzono sesję na niewłaściwej grupie");
            if (!posts[1].Uri.AbsolutePath.EndsWith("/playbackSessions/SESSION-TEST/playbackSession/loadStreamUrl", StringComparison.Ordinal))
                throw new Exception("Adres trafił do niewłaściwej sesji");
            using var document = JsonDocument.Parse(posts[1].Body);
            if (document.RootElement.GetProperty("streamUrl").GetString() != url
                || !document.RootElement.GetProperty("playOnCompletion").GetBoolean())
                throw new Exception("Nie zachowano adresu/autostartu");
            using var create = JsonDocument.Parse(posts[0].Body);
            if (create.RootElement.EnumerateObject().Count() != 2)
                throw new Exception("Wysłano dodatkowe pola sesji");
        }
    }
}
