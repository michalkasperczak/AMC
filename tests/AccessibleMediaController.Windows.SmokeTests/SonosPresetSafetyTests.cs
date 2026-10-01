using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Reflection;
using System.Windows.Threading;
using AccessibleMediaController.Windows;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Sonos;

internal static partial class SonosFavoritePlayRealOwnerTests
{
    private static string MeasureOwnPreset(string mode)
    {
        using var h = RealHarness.Create();
        h.Enter();
        var station = new SonosOwnStreamSettings { Id = "station-test", Name = "Radio testowe", StreamUrl = "https://radio.invalid/old" };
        h.Window.StateForTests.Sonos.OwnStreams.Add(station);
        var expected = SonosOwnStreamIdentity.TryComputeItemId(station.Id, station.StreamUrl);
        if (mode == "edited") station.StreamUrl = "https://radio.invalid/new";
        var playbackReads = 0;
        h.Handler.RouteOverride = (request, _) =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Get && path.EndsWith("/playback", StringComparison.Ordinal))
            {
                playbackReads++;
                if (mode == "read-failed") return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
                var id = mode == "changed" && playbackReads > 1 ? "external-source" : expected;
                var state = mode == "idle" ? "PLAYBACK_STATE_IDLE"
                    : mode == "changed" ? "PLAYBACK_STATE_PAUSED" : "PLAYBACK_STATE_PLAYING";
                return Json(JsonSerializer.Serialize(new { playbackState = state, itemId = id }));
            }
            if (request.Method == HttpMethod.Get && path.EndsWith("/playbackMetadata", StringComparison.Ordinal))
                return Json("{\"container\":{\"name\":\"Radio testowe\",\"type\":\"station\"}}");
            if (request.Method == HttpMethod.Post && path.EndsWith("/playbackSession", StringComparison.Ordinal))
                return Json("{\"sessionId\":\"SESSION-PRESET\"}");
            if (request.Method == HttpMethod.Post && (path.EndsWith("/loadStreamUrl", StringComparison.Ordinal)
                || path.EndsWith("/play", StringComparison.Ordinal))) return Json("{}");
            return null;
        };
        var before = h.Handler.Posts.Count;
        FirePreset(h, new SessionPresetEntry { Slot = 4, TargetId = station.Id, TargetKind = SonosPresetKinds.OwnStream, TargetTitle = station.Name });
        var posts = h.Handler.Posts.Skip(before).ToArray();
        switch (mode)
        {
            case "playing":
                if (posts.Length != 0 || LastAnnouncement(h) != station.Name) throw new Exception("Już gra: wymagane zero POST i sama nazwa.");
                break;
            case "idle":
                if (posts.Length != 1 || !posts[0].Uri.AbsolutePath.EndsWith("/play", StringComparison.Ordinal))
                    throw new Exception("Spauzowane radio IDLE: wymagane tylko Play, bez create/load; POST=" + posts.Length);
                break;
            case "read-failed":
                if (posts.Length != 0) throw new Exception("Błąd odczytu uruchomił create/load: POST=" + posts.Length);
                break;
            case "changed":
                if (playbackReads < 2 || posts.Any(x => x.Uri.AbsolutePath.EndsWith("/play", StringComparison.Ordinal)))
                    throw new Exception("Brak recheck itemId po metadata albo wznowiono obce źródło.");
                break;
            case "edited":
                if (posts.Length != 2) throw new Exception("Edycja URL wymaga create/load nowego adresu.");
                using (var payload = JsonDocument.Parse(posts[1].Body))
                {
                    if (payload.RootElement.GetProperty("itemId").GetString() != SonosOwnStreamIdentity.TryComputeItemId(station.Id, station.StreamUrl))
                        throw new Exception("Load nie wysłał klucza nowego adresu.");
                    if (!posts[1].Body.Contains("https://radio.invalid/new", StringComparison.Ordinal)) throw new Exception("Wysłano stary adres.");
                }
                break;
        }
        return mode + ": POST=" + posts.Length + ", playbackGET=" + playbackReads + ", mowa=" + LastAnnouncement(h);
    }

    private static string MeasureOwnEnterThenPreset()
    {
        using var h = RealHarness.Create();
        h.Enter();
        var station = new SonosOwnStreamSettings { Id = "station-enter", Name = "Radio Enter", StreamUrl = "https://radio.invalid/enter" };
        h.Window.StateForTests.Sonos.OwnStreams.Add(station);
        string? loadedId = null;
        h.Handler.RouteOverride = (request, body) =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Post && path.EndsWith("/playbackSession", StringComparison.Ordinal)) return Json("{\"sessionId\":\"SESSION-ENTER\"}");
            if (request.Method == HttpMethod.Post && path.EndsWith("/loadStreamUrl", StringComparison.Ordinal))
            {
                using var data = JsonDocument.Parse(body);
                loadedId = data.RootElement.GetProperty("itemId").GetString();
                return Json("{}");
            }
            if (request.Method == HttpMethod.Get && path.EndsWith("/playback", StringComparison.Ordinal))
                return Json(JsonSerializer.Serialize(new { playbackState = "PLAYBACK_STATE_PLAYING", itemId = loadedId }));
            if (request.Method == HttpMethod.Get && path.EndsWith("/playbackMetadata", StringComparison.Ordinal))
                return Json("{\"container\":{\"name\":\"Radio Enter\",\"type\":\"station\"}}");
            return null;
        };
        var beforeIntent = h.Window.SonosPlaybackIntentForTests;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(25) };
        Exception? failure = null;
        var started = false;
        var ended = false;
        timer.Tick += (_, _) =>
        {
            var window = h.Window.OpenSonosOwnStreamsWindowForTests;
            if (window is null || !window.IsVisible) return;
            try
            {
                if (!started)
                {
                    started = true;
                    typeof(SonosOwnStreamsWindow).GetMethod("StartPlay", Instance)!.Invoke(window, null);
                }
                if (window.LastPlayTaskForTests?.IsCompleted == true)
                {
                    window.LastPlayTaskForTests.GetAwaiter().GetResult();
                    timer.Stop(); window.Close(); ended = true;
                }
            }
            catch (Exception exception) { failure = exception; timer.Stop(); window.Close(); ended = true; }
        };
        timer.Start();
        try { typeof(MainWindow).GetMethod("ShowSonosOwnStreams", Instance)!.Invoke(h.Window, null); }
        finally { timer.Stop(); }
        if (failure is not null) throw failure;
        if (!ended || loadedId is null) throw new Exception("Zwykłe uruchomienie stacji nie zakończyło się.");
        var beforeRepeat = h.Handler.Posts.Count;
        FirePreset(h, new SessionPresetEntry { Slot = 4, TargetId = station.Id, TargetKind = SonosPresetKinds.OwnStream, TargetTitle = station.Name });
        if (h.Handler.Posts.Count != beforeRepeat || loadedId != SonosOwnStreamIdentity.TryComputeItemId(station.Id, station.StreamUrl))
            throw new Exception("Enter i preset mają różne klucze: preset ponownie załadował stację.");
        if (h.Window.SonosPlaybackIntentForTests < beforeIntent + 2)
            throw new Exception("Zwykły Enter nie unieważnia starszego zamiaru presetu.");
        return "Rzeczywisty modal stacji: create/load; późniejszy preset zero POST, wspólny klucz i zamiar.";
    }

    private static string MeasurePartialFixedPreset()
    {
        using var h = RealHarness.Create();
        h.Enter();
        h.Handler.RouteOverride = (request, _) => request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath.EndsWith("/groups", StringComparison.Ordinal)
            ? Json(TwoGroupsBody.Replace("\"partial\":false", "\"partial\":true", StringComparison.Ordinal)) : null;
        var before = h.Handler.Posts.Count;
        FirePreset(h, FavoritePreset(fixedPlayers: ["P8", "P9"]));
        if (h.Handler.Posts.Count != before) throw new Exception("Niepełna topologia pozwoliła na POST stałego celu.");
        return "Niepełny odczyt grup: zero POST.";
    }

    /// <summary>
    /// RYZYKO 3b, PRÓBA ROZRÓŻNIAJĄCA: ten sam identyfikator w innym rodzaju i w
    /// innym domu to INNY materiał. Gdyby zajętość liczyła samo targetId, oba
    /// warianty uchodziłyby za już przypisane i OMIJAŁY zgodę na nadpisanie.
    /// </summary>
    internal static string MeasureMaterialKeySeparatesKindAndHousehold()
    {
        const string sameId = "WSPOLNY-IDENTYFIKATOR";
        var favDom1 = InvokeMaterialKey("sonos-favorite", "DOM-1", sameId);
        var listDom1 = InvokeMaterialKey("sonos-playlist", "DOM-1", sameId);
        var favDom2 = InvokeMaterialKey("sonos-favorite", "DOM-2", sameId);
        var favDom1Again = InvokeMaterialKey("sonos-favorite", "DOM-1", sameId);

        if (favDom1 == listDom1)
            throw new Exception("Ulubiony i playlista z tym samym ID dały ten sam klucz materiału.");
        if (favDom1 == favDom2)
            throw new Exception("Ten sam ID w dwóch domach dał ten sam klucz materiału.");
        if (favDom1 != favDom1Again)
            throw new Exception("Klucz materiału nie jest stabilny dla tych samych danych.");

        // WŁASNA STACJA ma identyfikator lokalny, więc dom NIE może jej dzielić -
        // inaczej ta sama stacja po przelogowaniu zajęłaby drugie miejsce.
        var ownA = InvokeMaterialKey("sonos-own-stream", "DOM-1", sameId);
        var ownB = InvokeMaterialKey("sonos-own-stream", "DOM-2", sameId);
        if (ownA != ownB)
            throw new Exception("Dom rozróżnił własną stację, choć jej ID jest lokalny.");

        return "Klucz materiału: rodzaj i dom rozróżniają, własna stacja niezależna od domu.";
    }

    private static string InvokeMaterialKey(string kind, string household, string targetId)
    {
        var method = typeof(MainWindow).GetMethod(
            "SonosPresetMaterialKey",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new Exception("Brak SonosPresetMaterialKey.");
        return (string)method.Invoke(null, [kind, household, targetId])!;
    }
}
