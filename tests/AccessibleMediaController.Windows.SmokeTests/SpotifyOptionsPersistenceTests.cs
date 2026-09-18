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
    }
}
