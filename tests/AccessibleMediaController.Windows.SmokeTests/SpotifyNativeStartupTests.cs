using System.Reflection;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Windows;
using AccessibleMediaController.Windows.Services;

internal static class SpotifyNativeStartupTests
{
    internal static void Run()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            var root = Path.Combine(Path.GetTempPath(), "amc-native-startup-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            MainWindow? window = null;
            try
            {
                var state = new PersistedState();
                state.Settings.Updates.CheckAutomatically = false;
                state.Settings.LastSessionId = "spotifyLibrespot";
                var slots = new Dictionary<int,string>(state.Settings.SessionSlots);
                var track = new MediaItem { Id="spotify:track:TEST", ExternalId="TEST", Source="spotify:track:TEST",
                    Title="Utwór testowy", Kind=MediaItemKind.Track, IsFavorite=true };
                state.Spotify.CachedCollectionItems.Add(TidalCachedCollectionItemSettings.FromMediaItem(track));
                var store = new ConfigurationStore(Path.Combine(root,"state.json"));
                window = new MainWindow(state,store);
                const BindingFlags flags = BindingFlags.Instance|BindingFlags.NonPublic;
                SessionManager Sessions() => (SessionManager)typeof(MainWindow).GetField("_sessions",flags)!.GetValue(window)!;
                var manager = Sessions();
                var native = manager.FindSession("spotifyLibrespot")
                    ?? throw new Exception("Główne okno nie tworzy drugiej sesji Spotify.");
                var web = manager.FindSession("spotify")!;
                if (manager.Current != native) throw new Exception("Start nie przywrócił ostatniej sesji Librespot.");
                foreach(var slot in slots)
                    if(manager.SessionSlots[slot.Key] != slot.Value) throw new Exception("Start zmienił dotychczasowy skrót sesji.");
                if(native.Items.Count != 1 || native.Items[0].ExternalId != "TEST" || !native.Items[0].IsFavorite)
                    throw new Exception("Druga sesja nie wczytała zapamiętanej kolekcji Spotify.");
                if(ReferenceEquals(native.Items[0],web.Items[0])) throw new Exception("Kolekcja współdzieli obiekty między sesjami.");
                var output = (SpotifyLibrespotMediaOutput)typeof(MainWindow).GetField("_spotifyLibrespotOutput",flags)!.GetValue(window)!;
                if(output.IsHostRunning) throw new Exception("Samo otwarcie okna uruchomiło hosta Librespot.");
                if(!(bool)typeof(MainWindow).GetMethod("CurrentSessionSupportsAudioOutputSelection",flags)!.Invoke(window,null)!)
                    throw new Exception("Druga sesja nie udostępnia wyboru wyjścia.");
                manager.SelectSession("spotify");
                if((bool)typeof(MainWindow).GetMethod("CurrentSessionSupportsAudioOutputSelection",flags)!.Invoke(window,null)!)
                    throw new Exception("Dodanie Librespot zmieniło możliwości sesji SDK.");
                manager.SelectSession("spotifyLibrespot");
                native.Items[0].IsInQueue=true;
                typeof(MainWindow).GetMethod("RebuildCore",flags)!.Invoke(window,null);
                if(!ReferenceEquals(Sessions().FindSession("spotifyLibrespot"),native)
                    || !ReferenceEquals(Sessions().FindSession("spotify"),web) || !native.Items[0].IsInQueue)
                    throw new Exception("Zmiana ustawień przebudowała grające sesje lub zgubiła kolejkę.");
            }
            catch(Exception exception) { failure=exception; }
            finally { window?.Close(); }
        }) { IsBackground=true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if(!thread.Join(TimeSpan.FromSeconds(30))) throw new Exception("Start okna nie zakończył testu w 30 s.");
        if(failure is not null) throw new Exception("Integracja dwóch sesji w głównym oknie.",failure);
        Console.WriteLine("OK: dwie sesje w rzeczywistym MainWindow, lazy host, osobne modele i zachowanie po zmianie ustawień");
    }
}
