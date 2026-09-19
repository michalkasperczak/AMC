using System.Reflection;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Playback;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Core.Spotify;

/// <summary>
/// Trzy luki integracji, mierzone runtime na PRAWDZIWYM SessionManagerze:
/// ruch numeru sesji przy DZIURACH nie przenumerowuje innych sesji, a
/// ActiveSpotifyEngine opisuje FAKTYCZNE wyjscie takze po awaryjnym zejsciu
/// na drugi silnik i po odbudowie z zachowana sesja.
/// </summary>
internal static class SessionSlotGapsAndEngineTests
{
    internal static void Run()
    {
        RuchPrzyDziurachNiePrzenumerowuje();
        SilnikOdpowiadaFaktycznemuWyjsciu();
        Console.WriteLine("OK: numery sesji z dziurami i silnik Spotify wg rzeczywistego wyjscia");
    }

    private static void RuchPrzyDziurachNiePrzenumerowuje()
    {
        // Numer 3 i 6 celowo puste - tak wyglada zapis po scaleniu sesji Spotify.
        var settings = new AppSettings
        {
            LastSessionId = "local",
            SessionSlots = new Dictionary<int, string>
            {
                [1] = "local", [2] = "wiim", [4] = "tidal", [5] = "radio",
                [7] = "podcasts", [8] = "spotify", [9] = "appleMusic"
            }
        };
        var manager = new SessionManager(settings);
        var przed = new Dictionary<int, string>(manager.SessionSlots);
        if (przed.ContainsKey(3) || przed.ContainsKey(6))
            throw new Exception("Normalizacja zageszczila numery i zlikwidowala dziury.");

        // Ruch w gore z numeru 4: sasiadem jest ISTNIEJACY numer 2, nie puste 3.
        var wGore = manager.MoveSessionSlot(4, -1);
        if (!wGore.Moved)
            throw new Exception("Ruch w gore przez dziure zglosil, ze sesja jest juz pierwsza.");
        if (wGore.Slot != 2)
            throw new Exception($"Ruch w gore wyladowal w numerze {wGore.Slot}, a wolny sasiad to 2.");
        if (manager.FindSlot("tidal") != 2 || manager.FindSlot("wiim") != 4)
            throw new Exception("Wymiana z sasiadem nie objela obu sesji.");
        foreach (var id in new[] { "local", "radio", "podcasts", "spotify", "appleMusic" })
        {
            if (manager.FindSlot(id) != przed.First(pair => pair.Value == id).Key)
                throw new Exception($"Ruch sesji przenumerowal nietknieta sesje {id}.");
        }
        if (manager.SessionSlots.Keys.Order().SequenceEqual(Enumerable.Range(1, manager.SessionSlots.Count)))
            throw new Exception("Ruch sesji zagescil numery i skasowal dziury.");

        // Ruch w dol z ostatniego numeru nadal odmawia.
        if (manager.MoveSessionSlot(9, 1).Moved)
            throw new Exception("Ruch w dol z ostatniego numeru sie udal.");
        // Ruch w dol z numeru 5 idzie do ISTNIEJACEGO 7, nie do pustego 6.
        var wDol = manager.MoveSessionSlot(5, 1);
        if (!wDol.Moved || wDol.Slot != 7)
            throw new Exception($"Ruch w dol przez dziure dal numer {wDol.Slot} zamiast 7.");
        // Zapis ustawien odzwierciedla slownik razem z dziurami.
        if (settings.SessionSlots.ContainsKey(3) || settings.SessionSlots.ContainsKey(6))
            throw new Exception("Zapis po ruchu wypelnil dziury numerow.");
        if (settings.SessionSlots.Count != manager.SessionSlots.Count)
            throw new Exception("Zapis numerow rozjechal sie ze slownikiem sesji.");
    }

    private static void SilnikOdpowiadaFaktycznemuWyjsciu()
    {
        var sdk = new ProbeOutput();
        var librespot = new ProbeOutput();
        var konstruktor = typeof(SessionManager).GetConstructors().First(c =>
            c.GetParameters().Any(p => p.Name == "spotifyLibrespotOutput"));
        SessionManager Zbuduj(
            SpotifyPlaybackEngine engine,
            ProbeOutput? sdkOutput,
            ProbeOutput? librespotOutput,
            DemoMediaSession? istniejaca,
            out AppSettings settings)
        {
            var local = new AppSettings { LastSessionId = "spotify", SpotifyEngine = engine };
            settings = local;
            var argumenty = konstruktor.GetParameters().Select(p => p.Name switch
            {
                "settings" => (object?)local,
                "spotifyOutput" => sdkOutput,
                "spotifyLibrespotOutput" => librespotOutput,
                "existingSpotifySession" => istniejaca,
                _ => null
            }).ToArray();
            return (SessionManager)konstruktor.Invoke(argumenty);
        }

        // 1. Oba wyjscia dostepne: silnik = zapis.
        var pelny = Zbuduj(SpotifyPlaybackEngine.Librespot, sdk, librespot, null, out _);
        if (pelny.ActiveSpotifyEngine != SpotifyPlaybackEngine.Librespot)
            throw new Exception("Przy obu wyjsciach silnik nie odpowiada zapisowi.");

        // 2. FALLBACK: wybrany Librespot, ale jego wyjscia nie ma -> gra SDK.
        var fallbackNaSdk = Zbuduj(SpotifyPlaybackEngine.Librespot, sdk, null, null, out _);
        if (fallbackNaSdk.ActiveSpotifyEngine != SpotifyPlaybackEngine.Sdk)
            throw new Exception("Po awaryjnym zejsciu na SDK silnik nadal raportuje Librespot.");
        var sesjaSdk = fallbackNaSdk.FindSession("spotify")!;
        if (!ReferenceEquals(sesjaSdk.Output, sdk))
            throw new Exception("Sesja Spotify nie dostala wyjscia SDK przy fallbacku.");

        // 3. FALLBACK odwrotny: wybrany SDK bez wyjscia -> gra Librespot.
        var fallbackNaLibrespot = Zbuduj(SpotifyPlaybackEngine.Sdk, null, librespot, null, out _);
        if (fallbackNaLibrespot.ActiveSpotifyEngine != SpotifyPlaybackEngine.Librespot)
            throw new Exception("Po awaryjnym zejsciu na Librespot silnik nadal raportuje SDK.");

        // 4. ODBUDOWA z zachowana sesja: sesja gra Librespotem, zapis zmieniony
        // na SDK - raport MUSI mowic o grajacym torze, nie o nowym zapisie.
        var graj = Zbuduj(SpotifyPlaybackEngine.Librespot, sdk, librespot, null, out _);
        var grajaca = graj.FindSession("spotify")!;
        var utwor = new MediaItem
        {
            Id = "spotify:track:GRA", Source = "spotify:track:GRA", Duration = TimeSpan.FromMinutes(3)
        };
        grajaca.ReplaceItems([utwor]);
        grajaca.Play(utwor);
        var odbudowany = Zbuduj(SpotifyPlaybackEngine.Sdk, sdk, librespot, grajaca, out _);
        if (!ReferenceEquals(odbudowany.FindSession("spotify"), grajaca))
            throw new Exception("Odbudowa nie zachowala grajacej sesji Spotify.");
        if (!odbudowany.FindSession("spotify")!.IsPlaying)
            throw new Exception("Odbudowa zatrzymala grajaca sesje Spotify.");
        if (odbudowany.ActiveSpotifyEngine != SpotifyPlaybackEngine.Librespot)
            throw new Exception(
                "Po odbudowie z zachowana sesja silnik raportuje nowy zapis, a gra stary tor.");

        // 5. Odbudowa bez zmiany zapisu tez trzyma tor zachowanej sesji.
        var odbudowanyBezZmiany = Zbuduj(SpotifyPlaybackEngine.Librespot, sdk, librespot, grajaca, out _);
        if (odbudowanyBezZmiany.ActiveSpotifyEngine != SpotifyPlaybackEngine.Librespot)
            throw new Exception("Odbudowa bez zmiany zapisu zgubila silnik zachowanej sesji.");
    }

    private sealed class ProbeOutput : IMediaOutput
    {
        public string? LoadedItemId { get; private set; }
        public TimeSpan Position { get; private set; }
        public bool SupportsPlaybackRate => false;
        public void Play(MediaItem item, TimeSpan position, int volume, double playbackRate)
        {
            LoadedItemId = item.Id;
            Position = position;
        }
        public void Pause() { }
        public void Stop() { }
        public void Seek(TimeSpan position) => Position = position;
        public void SetVolume(int volume) { }
        public void SetPlaybackRate(double rate) { }
    }
}
