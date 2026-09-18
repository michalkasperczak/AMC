using System.Reflection;
using System.Runtime.CompilerServices;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Playback;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Core.Spotify;
using AccessibleMediaController.Windows;

internal static class SpotifyNativeCollectionTests
{
    internal static void Run()
    {
        var state = new PersistedState();
        var webOutput = new ProbeOutput();
        var nativeOutput = new ProbeOutput();
        var manager = new SessionManager(state.Settings, spotifyOutput: webOutput);
        var web = manager.FindSession("spotify")!;
        var native = manager.RegisterSpotifyLibrespotSession(nativeOutput);
        var source = new MediaItem { Id="spotify:track:A", ExternalId="A", Source="spotify:track:A",
            Title="A", Kind=MediaItemKind.Track };
        var webItem = SpotifySessionItemCopies.ForSession(source,"spotify");
        var nativeItem = SpotifySessionItemCopies.ForSession(source,"spotifyLibrespot");
        web.ReplaceItems([webItem]);
        native.ReplaceItems([nativeItem]);
        web.Play(webItem);
        native.Play(nativeItem);
        nativeItem.IsInQueue = true;
        var webStops = webOutput.Stops;
        var nativeStops = nativeOutput.Stops;
        var window = (MainWindow)RuntimeHelpers.GetUninitializedObject(typeof(MainWindow));
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        void Set(string name, object value) => typeof(MainWindow).GetField(name, flags)!.SetValue(window,value);
        Set("_state",state);
        Set("_sessions",manager);
        Set("_spotifyItems",new List<MediaItem> { source });
        var views = typeof(MainWindow).GetField("_spotifyContainerViews",flags)!;
        views.SetValue(window,Activator.CreateInstance(views.FieldType));
        var sync = typeof(MainWindow).GetMethod("SynchronizeSpotifyCachedMembership",flags)!;
        sync.Invoke(window,[new[]{source},true]);
        if (!webItem.IsFavorite || !nativeItem.IsFavorite)
            throw new Exception("Potwierdzone polubienie na wspólnym koncie nie dociera do obu sesji Spotify.");
        if (webItem.IsInQueue || !nativeItem.IsInQueue || webStops!=webOutput.Stops || nativeStops!=nativeOutput.Stops)
            throw new Exception("Synchronizacja konta zmieniła kolejkę lub przerwała odtwarzanie jednej z sesji.");
        var b = new MediaItem { Id="spotify:track:B", ExternalId="B", Source="spotify:track:B",
            Title="B", Kind=MediaItemKind.Track };
        sync.Invoke(window,[new[]{b},true]);
        sync.Invoke(window,[new[]{b},true]);
        var webB = web.Items.Single(x=>x.ExternalId=="B");
        var nativeB = native.Items.Single(x=>x.ExternalId=="B");
        if (ReferenceEquals(webB,nativeB) || nativeB.Id==webB.Id)
            throw new Exception("Dodany utwór współdzieli obiekt lub identyfikator obu sesji.");
        sync.Invoke(window,[new[]{source},false]);
        if (webItem.IsFavorite || nativeItem.IsFavorite || !nativeItem.IsInQueue)
            throw new Exception("Usunięcie polubienia nie dociera do obu sesji albo usuwa niezależną kolejkę.");
        manager.SelectSession("spotifyLibrespot");
        var visible = (bool)typeof(MainWindow).GetMethod("CommandVisibleInPalette",flags)!.Invoke(window,
            [AccessibleMediaController.Core.Commands.CommandIds.ManageSpotifyConnection])!;
        if (!visible) throw new Exception("Konto Spotify znika z palety drugiej sesji.");
        var collections = (bool)typeof(MainWindow).GetMethod("UsesTidalStyleCollections",BindingFlags.Static|BindingFlags.NonPublic)!
            .Invoke(null,["spotifyLibrespot"])!;
        if (!collections) throw new Exception("Druga sesja nie rozróżnia Ulubionych i Biblioteki Spotify.");
        var cache = typeof(MainWindow).GetMethod("StoreSpotifyContainerForSession", flags);
        if (cache is null) throw new Exception("Brak oddzielnego zapisu zawartości albumu dla obu sesji.");
        var container = new MediaItem { Id="spotify:album:ALB", ExternalId="ALB", Title="Album", Kind=MediaItemKind.Album };
        var c = new MediaItem { Id="spotify:track:C", ExternalId="C", Source="spotify:track:C", Title="C", Kind=MediaItemKind.Track };
        var webView = (string)cache.Invoke(window,["spotify",container,new[]{c}])!;
        var nativeView = (string)cache.Invoke(window,["spotifyLibrespot",container,new[]{c}])!;
        if (webView == nativeView) throw new Exception("Obie sesje nadpisują tę samą zawartość albumu.");
        var webC = web.Items.Single(x=>x.ExternalId=="C");
        var nativeC = native.Items.Single(x=>x.ExternalId=="C");
        if (ReferenceEquals(webC,nativeC)) throw new Exception("Album przenosi wspólny obiekt do dwóch kolejek.");
        nativeC.IsInQueue = true;
        cache.Invoke(window,["spotifyLibrespot",container,new[]{c}]);
        if (!nativeC.IsInQueue || webC.IsInQueue)
            throw new Exception("Ponowne otwarcie albumu miesza lub zeruje kolejkę.");
        VerifyNativeDeviceDialog();
        Console.WriteLine("OK: wspólna kolekcja Spotify, niezależne modele i kolejki SDK/Librespot");
    }

    private static void VerifyNativeDeviceDialog()
    {
        var choiceType = typeof(AccessibleMediaController.Windows.Services.AudioOutputDeviceChoice);
        var constructor = typeof(AudioOutputDeviceWindow).GetConstructor(BindingFlags.Instance|BindingFlags.NonPublic,
            null, [typeof(string), typeof(string), typeof(IReadOnlyList<>).MakeGenericType(choiceType)], null);
        if (constructor is null) throw new Exception("Okno audio nie przyjmuje urządzeń z rzeczywistej listy Librespot.");
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                IReadOnlyList<AccessibleMediaController.Windows.Services.AudioOutputDeviceChoice> choices =
                [new(null,"Domyślne"), new("Nazwa hosta testowa","Nazwa hosta testowa"), new("Nieobecne","Nieobecne — niedostępne",false)];
                var dialog = (AudioOutputDeviceWindow)constructor.Invoke(["Spotify — Librespot","Nazwa hosta testowa",choices]);
                try
                {
                    if (dialog.SelectedDeviceId != "Nazwa hosta testowa" || dialog.SelectedDeviceLabel != "Nazwa hosta testowa")
                        throw new Exception("Okno zastąpiło nazwę urządzenia Librespot identyfikatorem innego katalogu.");
                }
                finally { dialog.Close(); }
                var missing = (AudioOutputDeviceWindow)constructor.Invoke(["Spotify — Librespot","Nieobecne",choices]);
                try
                {
                    if (missing.SelectedDeviceIsAvailable || missing.SelectedDeviceId != "Nieobecne")
                        throw new Exception("Okno ukryło niedostępność zapamiętanego urządzenia.");
                }
                finally { missing.Close(); }
            }
            catch (Exception exception) { failure = exception; }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(15))) throw new Exception("Test okna audio nie zakończył się w czasie.");
        if (failure is not null) throw new Exception("Błąd okna wyboru audio Librespot.",failure);
    }

    private sealed class ProbeOutput : IMediaOutput
    {
        public int Stops;
        public string? LoadedItemId {get;private set;}
        public TimeSpan Position {get;private set;}
        public bool SupportsPlaybackRate=>false;
        public void Play(MediaItem item,TimeSpan position,int volume,double playbackRate){LoadedItemId=item.Id;Position=position;}
        public void Pause(){}
        public void Stop(){Stops++;}
        public void Seek(TimeSpan position){Position=position;}
        public void SetVolume(int volume){}
        public void SetPlaybackRate(double playbackRate){}
    }
}
