using System.Reflection;
using System.Runtime.CompilerServices;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Core.Spotify;
using AccessibleMediaController.Windows;

internal static class SpotifyOptionsPersistenceTests
{
    internal static void Run()
    {
        var state = new PersistedState();
        var manager = new SessionManager(state.Settings);
        var session = manager.FindSession("spotify")!;
        var first = new MediaItem { Id="first", ExternalId="first", Source="spotify:track:first" };
        var second = new MediaItem { Id="second", ExternalId="second", Source="spotify:track:second" };
        session.ReplaceItems([first,second]);
        session.SelectItem(first);
        session.SetPosition(TimeSpan.FromSeconds(71));
        session.SelectItem(second);
        session.SetPosition(TimeSpan.FromSeconds(23));
        // No Window is constructed or shown: exercise the exact capture seam.
        var window = (MainWindow)RuntimeHelpers.GetUninitializedObject(typeof(MainWindow));
        const BindingFlags flags = BindingFlags.Instance|BindingFlags.NonPublic;
        typeof(MainWindow).GetField("_state", flags)!.SetValue(window,state);
        typeof(MainWindow).GetField("_sessions", flags)!.SetValue(window,manager);
        typeof(MainWindow).GetMethod("CaptureSpotifyPlaybackPosition",flags)!.Invoke(window,null);
        if(SpotifyPlaybackSettingsResolver.ResolvePosition(state.Settings,first)!=TimeSpan.FromSeconds(71))
            throw new Exception("Zmiana utworu przed cyklicznym zapisem gubi pozycję poprzedniego utworu Spotify.");
        if(SpotifyPlaybackSettingsResolver.ResolvePosition(state.Settings,second)!=TimeSpan.FromSeconds(23))
            throw new Exception("Brak bieżącej pozycji Spotify.");
        // Reopening another copy of a previously visited track should retain its position.
        var restoredManager = new SessionManager(state.Settings);
        var restored = restoredManager.FindSession("spotify")!;
        restored.ReplaceItems([new MediaItem {Id="new-first",ExternalId="first",Source="spotify:track:first"}]);
        typeof(MainWindow).GetField("_sessions",flags)!.SetValue(window,restoredManager);
        typeof(MainWindow).GetMethod("RestoreSpotifyRememberedPositions",flags)!.Invoke(window,null);
        restored.SelectItem(restored.Items[0]);
        if(restored.Position!=TimeSpan.FromSeconds(71))
            throw new Exception("Odtworzenie biblioteki z nowym ID gubi pozycję Spotify.");

        session.Play(first);
        typeof(MainWindow).GetField("_sessions", flags)!.SetValue(window, manager);
        typeof(MainWindow).GetField("_spotifyItems", flags)!.SetValue(window, new List<MediaItem> { first, second });
        var views = typeof(MainWindow).GetField("_spotifyContainerViews", flags)!;
        views.SetValue(window, Activator.CreateInstance(views.FieldType));
        typeof(MainWindow).GetMethod("SynchronizeSpotifyCachedMembership", flags)!.Invoke(window, [new[] { first }, false]);
        if (!session.HasCurrentItem || session.CurrentItem.Id != first.Id || !session.IsPlaying)
            throw new Exception("Usunięcie z Ulubionych przerwało bieżące odtwarzanie Spotify.");
        var searchCopy = new MediaItem { Id="search-first", ExternalId=first.ExternalId, Kind=first.Kind, Source=first.Source };
        typeof(MainWindow).GetMethod("SynchronizeSpotifyCachedMembership", flags)!.Invoke(window, [new[] { searchCopy }, true]);
        if (session.Items.Count(x => x.ExternalId == first.ExternalId && x.Kind == first.Kind) != 1)
            throw new Exception("Dodanie z wyszukiwania tworzy drugi wiersz tego samego utworu Spotify.");

        var native = manager.RegisterSpotifyLibrespotSession(new SilentFixtureOutput());
        var nativeItem = SpotifySessionItemCopies.ForSession(first, "spotifyLibrespot");
        native.ReplaceItems([nativeItem]);
        native.SelectItem(nativeItem);
        native.SetPosition(TimeSpan.FromSeconds(23));
        typeof(MainWindow).GetMethod("CaptureSpotifyPlaybackPosition",flags)!.Invoke(window,null);
        if(SpotifyPlaybackSettingsResolver.ResolvePosition(state.Settings,nativeItem,"spotifyLibrespot")!=TimeSpan.FromSeconds(23))
            throw new Exception("Okno główne nie przechwytuje pozycji drugiej sesji Spotify.");
    }

    private sealed class SilentFixtureOutput : AccessibleMediaController.Core.Playback.IMediaOutput
    {
        public string? LoadedItemId => null;
        public TimeSpan Position => TimeSpan.Zero;
        public bool SupportsPlaybackRate => false;
        public void Play(MediaItem item, TimeSpan position, int volume, double playbackRate) { }
        public void Pause() { }
        public void Stop() { }
        public void Seek(TimeSpan position) { }
        public void SetVolume(int volume) { }
        public void SetPlaybackRate(double playbackRate) { }
    }
}
