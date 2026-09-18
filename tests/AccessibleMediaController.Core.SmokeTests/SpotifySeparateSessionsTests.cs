using System.Reflection;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Playback;
using AccessibleMediaController.Core.Sessions;

internal static class SpotifySeparateSessionsTests
{
    internal static void Run()
    {
        var settings = new AppSettings();
        var oldSlots = new Dictionary<int, string>
        {
            [1]="radio", [2]="local", [3]="wiim", [4]="tidal", [5]="appleMusic", [6]="podcasts", [7]="spotify"
        };
        settings.SessionSlots = new(oldSlots);
        var webOutput = new ProbeOutput();
        var nativeOutput = new ProbeOutput();
        var manager = new SessionManager(settings, spotifyOutput: webOutput);
        var web = manager.FindSession("spotify")!;
        var webTrack = new MediaItem { Id="web-track", Source="spotify:track:track", Duration=TimeSpan.FromMinutes(4) };
        web.ReplaceItems([webTrack]);
        web.Play(webTrack);
        web.SetPosition(TimeSpan.FromSeconds(71));
        var oldStops = webOutput.Stops;

        var register = typeof(SessionManager).GetMethod("RegisterSpotifyLibrespotSession", BindingFlags.Public|BindingFlags.Instance);
        if(register is null) throw new Exception("Brak rejestracji drugiej niezależnej sesji Spotify Librespot.");
        var native = (DemoMediaSession)register.Invoke(manager, [nativeOutput, null])!;
        if (native.Id != "spotifyLibrespot" || native.DisplayName != "Spotify — Librespot")
            throw new Exception("Nowa sesja nie jest odróżnialna od dotychczasowego Spotify.");
        foreach(var pair in oldSlots)
            if(manager.SessionSlots[pair.Key]!=pair.Value) throw new Exception("Dodanie Librespot przestawiło dotychczasowy skrót sesji.");
        if (manager.SessionSlots[8] != native.Id || manager.Sessions[^1] != native)
            throw new Exception("Librespot nie został dopisany na końcu.");
        var nativeTrack = new MediaItem { Id="native-track", Source=webTrack.Source, Duration=webTrack.Duration };
        native.ReplaceItems([nativeTrack]);
        native.Play(nativeTrack);
        native.SetPosition(TimeSpan.FromSeconds(23));
        nativeTrack.IsInQueue = true;
        if(webTrack.IsInQueue || web.Position!=TimeSpan.FromSeconds(71) || webOutput.Plays!=1 || webOutput.Stops!=oldStops)
            throw new Exception("Druga sesja zmieniła pozycję, kolejkę lub wyjście pierwszej.");
        if(nativeOutput.Plays!=1 || native.Position!=TimeSpan.FromSeconds(23))
            throw new Exception("Librespot nie dostał własnego wyjścia i pozycji.");
        var again = (DemoMediaSession)register.Invoke(manager, [nativeOutput, native])!;
        if(!ReferenceEquals(again,native) || nativeOutput.Plays!=1 || native.Position!=TimeSpan.FromSeconds(23))
            throw new Exception("Ponowna rejestracja restartuje odtwarzanie Librespot.");
        VerifyIndependentSettings();
        VerifyIndependentCopies();
        if(SessionSlotOrder.GetDisplayName(native.Id)!=native.DisplayName)
            throw new Exception("Ustawienia sesji odczytują techniczny identyfikator Librespot.");
    }

    private static void VerifyIndependentCopies()
    {
        var type = typeof(SessionManager).Assembly.GetType("AccessibleMediaController.Core.Spotify.SpotifySessionItemCopies");
        if(type is null) throw new Exception("Brak niezależnych kopii elementów między sesjami Spotify.");
        var source = new MediaItem { Id="shared", ExternalId="service-id", Source="spotify:track:service-id", Title="Tytuł", Kind=MediaItemKind.Track,
            IsInQueue=true, IsPlayNext=true, IsFavorite=true, IsInLibrary=true, IsAvailable=false, RelatedAlbumExternalId="album", RelatedArtistExternalId="artist" };
        var clone = (MediaItem)type.GetMethod("ForSession")!.Invoke(null,[source,"spotifyLibrespot"])!;
        if(ReferenceEquals(source,clone) || clone.Id==source.Id || clone.IsInQueue || clone.IsPlayNext)
            throw new Exception("Dwie sesje współdzielą element lub flagi kolejki.");
        foreach(var property in typeof(MediaItem).GetProperties().Where(p=>p.SetMethod is not null && p.Name is not ("Id" or "IsInQueue" or "IsPlayNext")))
            if(!Equals(property.GetValue(source),property.GetValue(clone)))
                throw new Exception("Kopia Librespot zgubiła pole: "+property.Name);
        clone.Title="Inny tytuł";
        if(source.Title!="Tytuł") throw new Exception("Zmiana w Librespot przeszła do modelu SDK.");
    }

    private static void VerifyIndependentSettings()
    {
        var resolver = typeof(AccessibleMediaController.Core.Spotify.SpotifyPlaybackSettingsResolver);
        var setMode = resolver.GetMethod("SetItemMode")!;
        if (setMode.GetParameters().Length != 4)
            throw new Exception("Pamięć pozycji nie rozróżnia dwóch sesji Spotify.");
        var store = resolver.GetMethod("StorePosition")!;
        var read = resolver.GetMethod("ResolvePosition")!;
        var settings = new AppSettings();
        var track = new MediaItem { Id="same-item", Source="spotify:track:same", Duration=TimeSpan.FromMinutes(4) };
        setMode.Invoke(null, [settings, track, ResumePositionMode.Remember, "spotify"]);
        setMode.Invoke(null, [settings, track, ResumePositionMode.Remember, "spotifyLibrespot"]);
        store.Invoke(null, [settings, track, TimeSpan.FromSeconds(71), "spotify"]);
        store.Invoke(null, [settings, track, TimeSpan.FromSeconds(23), "spotifyLibrespot"]);
        var device = typeof(AppSettings).GetProperty("SpotifyLibrespotDeviceName");
        if (device is null) throw new Exception("Brak osobnego zapisu urządzenia Librespot.");
        device.SetValue(settings, "Wyjście USB testowe");
        var folder = Path.Combine(Path.GetTempPath(), "amc-librespot-settings-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var config = new ConfigurationStore(Path.Combine(folder,"state.json"));
            config.Save(new PersistedState { Settings=settings });
            var restored = config.LoadOrCreate().Settings;
            if ((TimeSpan)read.Invoke(null,[restored,track,"spotify"])! != TimeSpan.FromSeconds(71)
                || (TimeSpan)read.Invoke(null,[restored,track,"spotifyLibrespot"])! != TimeSpan.FromSeconds(23)
                || (string?)device.GetValue(restored) != "Wyjście USB testowe")
                throw new Exception("Zapis ustawień miesza pozycje lub urządzenie dwóch sesji Spotify.");
            setMode.Invoke(null,[restored,track,ResumePositionMode.StartFromBeginning,"spotifyLibrespot"]);
            if ((TimeSpan)read.Invoke(null,[restored,track,"spotify"])! != TimeSpan.FromSeconds(71)
                || (TimeSpan)read.Invoke(null,[restored,track,"spotifyLibrespot"])! != TimeSpan.Zero)
                throw new Exception("Opcja od początku w Librespot zmieniła pozycję standardowego Spotify.");
        }
        finally { Directory.Delete(folder, true); }
    }

    private sealed class ProbeOutput : IMediaOutput
    {
        public string? LoadedItemId {get;private set;}
        public TimeSpan Position {get;private set;}
        public bool SupportsPlaybackRate=>false;
        public int Plays,Stops;
        public void Play(MediaItem item,TimeSpan position,int volume,double playbackRate){Plays++;LoadedItemId=item.Id;Position=position;}
        public void Pause(){}
        public void Stop(){Stops++;}
        public void Seek(TimeSpan position){Position=position;}
        public void SetVolume(int volume){}
        public void SetPlaybackRate(double playbackRate){}
    }
}
