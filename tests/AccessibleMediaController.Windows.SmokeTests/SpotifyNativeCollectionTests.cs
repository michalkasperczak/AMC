using System.Reflection;
using System.Runtime.CompilerServices;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Playback;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Core.Spotify;
using AccessibleMediaController.Windows;

/// <summary>
/// Kolekcja Spotify po scaleniu: JEDNA sesja, jeden model, jedna kolejka. Dawna
/// wersja tego testu sprawdzala, ze dwie sesje maja niezalezne modele - teraz
/// sprawdzamy, ze model jest jeden i ze oba silniki korzystaja z tych samych
/// danych, bez dublowania wierszy.
/// </summary>
internal static class SpotifyNativeCollectionTests
{
    internal static void Run()
    {
        var state = new PersistedState();
        var sdkOutput = new ProbeOutput();
        var librespotOutput = new ProbeOutput();
        state.Settings.SpotifyEngine = SpotifyPlaybackEngine.Librespot;
        var manager = new SessionManager(
            state.Settings, spotifyOutput: sdkOutput, spotifyLibrespotOutput: librespotOutput);
        var spotify = manager.FindSession("spotify")!;
        var source = new MediaItem { Id="spotify:track:A", ExternalId="A", Source="spotify:track:A",
            Title="A", Kind=MediaItemKind.Track };
        var item = SpotifySessionItemCopies.ForSession(source,"spotify");
        spotify.ReplaceItems([item]);
        spotify.Play(item);
        item.IsInQueue = true;
        var librespotStops = librespotOutput.Stops;
        var window = (MainWindow)RuntimeHelpers.GetUninitializedObject(typeof(MainWindow));
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        void Set(string name, object value) => typeof(MainWindow).GetField(name, flags)!.SetValue(window,value);
        Set("_state",state);
        Set("_sessions",manager);
        Set("_activeSpotifyEngine",SpotifyPlaybackEngine.Librespot);
        Set("_spotifyItems",new List<MediaItem> { source });
        var views = typeof(MainWindow).GetField("_spotifyContainerViews",flags)!;
        views.SetValue(window,Activator.CreateInstance(views.FieldType));
        var sync = typeof(MainWindow).GetMethod("SynchronizeSpotifyCachedMembership",flags)!;
        sync.Invoke(window,[new[]{source},true]);
        if (!item.IsFavorite)
            throw new Exception("Potwierdzone polubienie na koncie nie dociera do sesji Spotify.");
        if (!item.IsInQueue || librespotOutput.Stops != librespotStops)
            throw new Exception("Synchronizacja konta zmienila kolejke lub przerwala odtwarzanie.");
        var b = new MediaItem { Id="spotify:track:B", ExternalId="B", Source="spotify:track:B",
            Title="B", Kind=MediaItemKind.Track };
        sync.Invoke(window,[new[]{b},true]);
        sync.Invoke(window,[new[]{b},true]);
        if (spotify.Items.Count(x=>x.ExternalId=="B") != 1)
            throw new Exception("Dodany utwor pojawil sie w jedynej sesji Spotify dwa razy.");
        sync.Invoke(window,[new[]{source},false]);
        if (item.IsFavorite || !item.IsInQueue)
            throw new Exception("Usuniecie polubienia nie dotarlo do sesji albo skasowalo kolejke.");
        manager.SelectSession("spotify");
        var visible = (bool)typeof(MainWindow).GetMethod("CommandVisibleInPalette",flags)!.Invoke(window,
            [AccessibleMediaController.Core.Commands.CommandIds.ManageSpotifyConnection])!;
        if (!visible) throw new Exception("Konto Spotify znika z palety jedynej sesji.");
        var collections = (bool)typeof(MainWindow).GetMethod("UsesTidalStyleCollections",BindingFlags.Static|BindingFlags.NonPublic)!
            .Invoke(null,["spotify"])!;
        if (!collections) throw new Exception("Sesja Spotify nie rozroznia Ulubionych i Biblioteki.");
        var cache = typeof(MainWindow).GetMethod("StoreSpotifyContainerForSession", flags);
        if (cache is null) throw new Exception("Brak zapisu zawartosci albumu dla sesji Spotify.");
        var container = new MediaItem { Id="spotify:album:ALB", ExternalId="ALB", Title="Album", Kind=MediaItemKind.Album };
        var c = new MediaItem { Id="spotify:track:C", ExternalId="C", Source="spotify:track:C", Title="C", Kind=MediaItemKind.Track };
        var viewName = (string)cache.Invoke(window,["spotify",container,new[]{c},null])!;
        // Ten sam album otwarty ponownie w tej samej sesji musi trafic w TEN SAM
        // widok, inaczej wracaja dwie rozne listy tego samego albumu.
        if ((string)cache.Invoke(window,["spotify",container,new[]{c},null])! != viewName)
            throw new Exception("Ponowne otwarcie albumu tworzy nowy widok zamiast odswiezyc istniejacy.");
        var trackC = spotify.Items.Single(x=>x.ExternalId=="C");
        trackC.IsInQueue = true;
        cache.Invoke(window,["spotify",container,new[]{c},null]);
        if (!spotify.Items.Single(x=>x.ExternalId=="C").IsInQueue)
            throw new Exception("Ponowne otwarcie albumu zeruje kolejke.");
        VerifyNativeDeviceDialog();
        Console.WriteLine("OK: jedna kolekcja Spotify, jeden model i kolejka dla obu silnikow");
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
