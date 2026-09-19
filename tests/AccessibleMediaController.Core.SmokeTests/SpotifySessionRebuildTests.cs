using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Playback;
using AccessibleMediaController.Core.Sessions;

internal static class SpotifySessionRebuildTests
{
    internal static void Run()
    {
        var output = new CountingOutput();
        var firstSettings = new AppSettings { LastSessionId = "spotify" };
        var initial = new SessionManager(firstSettings, spotifyOutput: output);
        var spotify = initial.FindSession("spotify")!;
        var track = new MediaItem { Id="track", Source="spotify:track:track", Duration=TimeSpan.FromMinutes(4) };
        spotify.ReplaceItems([track]);
        spotify.Play(track);
        spotify.SetPosition(TimeSpan.FromSeconds(73));
        var stopsBeforeRebuild = output.Stops;
        var newSettings = new AppSettings { LastSessionId = "spotify", RememberLocalPlaybackPositions=false };
        // Konstruktor przyjmuje teraz takze wyjscie Librespot, wiec szukamy go po
        // parametrze zachowywanej sesji, nie po liczbie argumentow.
        var constructor = typeof(SessionManager).GetConstructors().FirstOrDefault(c =>
            c.GetParameters().Any(p => p.Name == "existingSpotifySession"));
        if(constructor is null) throw new Exception("Odbudowa sesji nie potrafi zachować grającej sesji Spotify.");
        object?[] Argumenty(DemoMediaSession zachowywana) => constructor.GetParameters()
            .Select(p => p.Name switch
            {
                "settings" => newSettings,
                "spotifyOutput" => output,
                "existingSpotifySession" => (object?)zachowywana,
                _ => null
            })
            .ToArray();
        var rebuilt = (SessionManager)constructor.Invoke(Argumenty(spotify));
        if(!ReferenceEquals(rebuilt.FindSession("spotify"),spotify) || !rebuilt.Current.IsPlaying
            || rebuilt.Current.CurrentItem.Id!="track" || rebuilt.Current.Position!=TimeSpan.FromSeconds(73))
            throw new Exception("Odbudowa gubi utwór, czas albo stan Spotify.");
        if(output.Plays!=1 || output.Stops!=stopsBeforeRebuild)
            throw new Exception("Zmiana ustawień restartuje działający odtwarzacz Spotify.");
        spotify.RememberCurrentPosition();
        if(spotify.RememberedPositions.ContainsKey(track.Id))
            throw new Exception("Zachowana sesja czyta stare ustawienie pamięci pozycji.");
        spotify.TogglePlayback();
        var paused=(SessionManager)constructor.Invoke(Argumenty(spotify));
        if(!paused.Current.IsPaused || paused.Current.IsPlaying || output.Plays!=1)
            throw new Exception("Odbudowa po pauzie sama uruchamia muzykę.");
    }

    private sealed class CountingOutput : IMediaOutput
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
        public void SetPlaybackRate(double rate){}
    }
}
