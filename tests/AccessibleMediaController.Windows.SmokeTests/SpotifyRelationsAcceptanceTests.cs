using System.Reflection;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Windows;

internal static class SpotifyRelationsAcceptanceTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    internal static void Run()
    {
        var cases = new (string, Action<MainWindow>)[] {
            ("Prawa strzałka z albumu otwiera od razu wykonawcę", AlbumToArtist),
            ("Wykonawca ma tylko Albumy i Utwory", ArtistMenu),
            ("Album bez ID wykonawcy nie otwiera martwej pozycji", MissingArtist),
            ("Utwór zachowuje wybór albumu i wykonawcy", TrackMenu),
            ("Enter albumu nadal wczytuje jego utwory", AlbumEnter),
            ("Wybór Albumy otwiera albumy, nie kategorie", w=>ArtistChoice(w,0,"/artists/artist1/albums")),
            ("Wybór Utwory szuka utworów wykonawcy", w=>ArtistChoice(w,1,"/search")),
            ("Playlista nadal otwiera zawartość", w=>OtherContainer(w,"spotify:playlist:list1","/playlists/list1")),
            ("Podcast nadal otwiera odcinki", w=>OtherContainer(w,"spotify:show:show1","/shows/show1")),
            ("Odcinek nadal przechodzi do podcastu", w=>OtherContainer(w,"spotify:episode:episode1","/shows/show1")),
        };
        var failed = 0;
        foreach (var (name, action) in cases)
        {
            Exception? error = null;
            var thread = new Thread(() => {
                var root = Path.Combine(Path.GetTempPath(), "amc-relations-" + Guid.NewGuid().ToString("N"));
                MainWindow? window = null;
                try
                {
                    SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
                    window=CreateFixture(root);
                    window.Show();Pump();Call(window,"NavigateTo","Biblioteka");Pump();
                    action(window);
                }
                catch(Exception e){error=e;}
                finally
                {
                    if(Menu() is {} menu) menu.IsOpen=false;
                    window?.Close();Dispatcher.CurrentDispatcher.InvokeShutdown();
                    try{Directory.Delete(root,true);}catch(IOException){}catch(UnauthorizedAccessException){}
                }
            }){IsBackground=true};
            thread.SetApartmentState(ApartmentState.STA);thread.Start();
            if(!thread.Join(TimeSpan.FromSeconds(30)))error=new Exception("Timeout interfejsu powiązań");
            if(error is null)Console.WriteLine("OK: "+name);
            else{failed++;Console.Error.WriteLine(name+": "+error);}
        }
        Console.WriteLine($"POWIAZANIA: {cases.Length-failed} OK / {failed} BLAD / razem {cases.Length}");
        if(failed>0)throw new Exception("Nie przeszły wszystkie przypadki powiązań Spotify");
    }
    internal static MainWindow CreateFixture(string root, bool speak = false)
    {
                    var store = new ConfigurationStore(Path.Combine(root, "state.json"));
                    var state = store.LoadOrCreate();
                    state.Settings.Updates.CheckAutomatically = state.Settings.Updates.InstallOnExit = false;
                    state.Settings.LastSessionId = "spotify";
                    state.Settings.Messages.Enabled = speak;
                    state.Tidal.ClientId = state.Spotify.ClientId = "";
                    state.Tidal.CachedCollectionItems.Clear(); state.Spotify.CachedCollectionItems.Clear();
                    state.LocalMedia.Items.Clear(); state.LocalMedia.FolderSources.Clear();
                    state.Podcasts.Episodes.Clear(); state.Podcasts.Subscriptions.Clear();
                    state.Podcasts.DownloadsFolder = Path.Combine(root,"downloads");
                    state.Radio.Stations.Clear(); state.Radio.RecordingSchedules.Clear(); state.Radio.WakeScheduledRecordings = false;
                    state.Radio.RecordingsFolder=Path.Combine(root,"recordings");state.WiiM.Devices.Clear();
                    foreach(var item in new[]{
                        new MediaItem {Id="spotify:album:album1",ExternalId="album1",Kind=MediaItemKind.Album,Title="Album próby",Source="spotify:album:album1",IsInLibrary=true,RelatedArtistExternalId="artist1",RelatedArtistName="Wykonawca próby"},
                        new MediaItem {Id="spotify:artist:artist1",ExternalId="artist1",Kind=MediaItemKind.Artist,Title="Wykonawca próby",Source="spotify:artist:artist1",IsInLibrary=true},
                        new MediaItem {Id="spotify:album:orphan",ExternalId="orphan",Kind=MediaItemKind.Album,Title="Album bez wykonawcy",Source="spotify:album:orphan",IsInLibrary=true},
                        new MediaItem {Id="spotify:track:track1",ExternalId="track1",Kind=MediaItemKind.Track,Title="Utwór próby",Source="spotify:track:track1",RelatedAlbumExternalId="album1",RelatedAlbumTitle="Album próby",RelatedArtistExternalId="artist1",RelatedArtistName="Wykonawca próby",IsInLibrary=true},
                        new MediaItem {Id="spotify:playlist:list1",ExternalId="list1",Kind=MediaItemKind.Playlist,Title="Playlista próby",Source="spotify:playlist:list1",IsInLibrary=true},
                        new MediaItem {Id="spotify:show:show1",ExternalId="show1",Kind=MediaItemKind.Podcast,Title="Podcast próby",Source="spotify:show:show1",IsInLibrary=true},
                        new MediaItem {Id="spotify:episode:episode1",ExternalId="episode1",Kind=MediaItemKind.Episode,Title="Odcinek próby",Source="spotify:episode:episode1",RelatedAlbumExternalId="show1",RelatedAlbumTitle="Podcast próby",IsInLibrary=true}
                    }) state.Spotify.CachedCollectionItems.Add(TidalCachedCollectionItemSettings.FromMediaItem(item));
                    store.Save(state);
                    var window = new MainWindow(store.LoadOrCreate(),store) {SuppressDesktopIntegrationForTests=true};
                    typeof(MainWindow).GetField("_applicationUpdateStartOverride",Private)!.SetValue(window,(Func<bool,bool>)(_=>throw new Exception("No installer in tests")));
                    window.ContentRendered-=(EventHandler)Delegate.CreateDelegate(typeof(EventHandler),window,typeof(MainWindow).GetMethod("Window_ContentRendered",Private)!);
                    return window;
    }
    internal static void RunInteractive(string root)
    {
        Exception? error=null;
        var thread=new Thread(()=>
        {
            try
            {
                Directory.CreateDirectory(root);
                AccessibleMediaController.Windows.Services.DiagnosticLog.Initialize(Path.Combine(root,"logs"));
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
                var window=CreateFixture(root,true);
                _=Connect(window);
                window.ContentRendered+=(_,_)=>{Call(window,"NavigateTo","Biblioteka");Call(window,"SelectMediaItem","spotify:album:album1");};
                new System.Windows.Application().Run(window);
            }
            catch(Exception e){error=e;}
            finally{Dispatcher.CurrentDispatcher.InvokeShutdown();}
        });
        thread.SetApartmentState(ApartmentState.STA);thread.Start();thread.Join();
        if(error is not null)throw error;
    }
    private static void AlbumToArtist(MainWindow window)
    {
        Call(window,"SelectMediaItem","spotify:album:album1");Pump();
        Send(window,Key.Right);
        Check(Menu() is null,"Album nadal otwiera zbędne menu zamiast przejść do wykonawcy");
        Check((string)Field(window,"_currentView")! == "Spotify:artist1","Nie otwarto właściwego wykonawcy");
        Check(window.MediaList.Items.Count==2,"Brak dwóch kategorii wykonawcy");
        Check(window.MediaList.IsKeyboardFocusWithin,"Fokus nie trafił do listy wykonawcy");
        Send(window,Key.Escape);
        Check((string)Field(window,"_currentView")! == "Biblioteka","Escape nie wrócił do Biblioteki");
        var selected=window.MediaList.SelectedItem!;
        Check(((MediaItem)selected.GetType().GetProperty("Item")!.GetValue(selected)!).Id=="spotify:album:album1","Powrót zgubił zaznaczony album");
    }
    private static void ArtistMenu(MainWindow window)
    {
        Call(window,"SelectMediaItem","spotify:artist:artist1");Pump();Send(window,Key.Right);
        var menu=Menu();Check(menu is not null,"Brak wyboru dla wykonawcy");
        var labels=menu!.Items.OfType<MenuItem>().Select(x=>x.Header?.ToString()).ToArray();
        Check(labels.SequenceEqual(new[]{"Albumy","Utwory"}),"Nadmiarowe lub długie pozycje: "+string.Join(" | ",labels));
        menu.IsOpen=false;
        var until=System.Diagnostics.Stopwatch.StartNew();
        while(!window.MediaList.IsKeyboardFocusWithin && until.Elapsed<TimeSpan.FromSeconds(2)){Pump();Thread.Sleep(10);}
        Check(window.MediaList.IsKeyboardFocusWithin,"Anulowanie menu nie oddało fokusu liście: "+Keyboard.FocusedElement?.GetType().Name);
    }
    private static void MissingArtist(MainWindow window)
    {
        Call(window,"SelectMediaItem","spotify:album:orphan");Pump();var view=Field(window,"_currentView");
        Send(window,Key.Right);
        Check(Menu() is null && Equals(view,Field(window,"_currentView")),"Brak wykonawcy otworzył martwe menu lub inny widok");
        Check(window.StatusText.Text.Contains("nie ma dostępnego"),"Brak informacji o nieobecnych powiązaniach");
    }
    private static void TrackMenu(MainWindow window)
    {
        using var handler=Connect(window);
        Call(window,"SelectMediaItem","spotify:album:album1");Pump();Send(window,Key.Enter);
        WaitUntil(()=>window.ActionItem?.ExternalId=="new-track");Send(window,Key.Right);
        Check(Menu()?.Items.OfType<MenuItem>().Select(x=>x.Header.ToString()).SequenceEqual(new[]{"Przejdź do albumu","Przejdź do wykonawcy"})==true,"Zmieniono powiązania utworu");
    }
    private static BrowseHandler Connect(MainWindow window)
    {
        var handler=new BrowseHandler();window.SpotifyHttpClientForTests=new HttpClient(handler);
        window.SpotifyAccessTokenForTests="synthetic-test-value";return handler;
    }
    private static void AlbumEnter(MainWindow window)
    {
        using var handler=Connect(window);Call(window,"SelectMediaItem","spotify:album:album1");Pump();Send(window,Key.Enter);
        WaitUntil(()=>handler.Urls.Any(x=>x.Contains("/albums/album1/tracks")));
        Check(Menu() is null,"Enter albumu pokazał wybór zamiast zawartości");
        Check((string)Field(window,"_currentView")! == "Spotify:album1","Enter nie otworzył zawartości albumu");
        Check(window.MediaList.Items.Count>0,"Album utracił wiersze utworów");
        var state=(PersistedState)Field(window,"_state")!;
        Check(!state.Spotify.CachedCollectionItems.Any(x=>x.ExternalId=="new-track" && x.IsInLibrary),"Otwarcie dodało nowy utwór do Biblioteki");
    }
    private static void ArtistChoice(MainWindow window,int index,string endpoint)
    {
        using var handler=Connect(window);Call(window,"SelectMediaItem","spotify:artist:artist1");Pump();Send(window,Key.Right);
        var menu=Menu()!;var item=(MenuItem)menu.Items[index];menu.IsOpen=false;
        item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));Pump();
        WaitUntil(()=>handler.Urls.Any(x=>x.Contains(endpoint)));
        Check(handler.Urls.All(x=>!x.Contains("top-tracks")),"Wybrano niedostępny endpoint");
        WaitUntil(()=>window.ActionItem?.Kind == (index==0?MediaItemKind.Album:MediaItemKind.Track));
        Check(window.MediaList.IsKeyboardFocusWithin,"Po wykonaniu wyboru brak fokusu na wynikach");
    }
    private static void OtherContainer(MainWindow window,string id,string endpoint)
    {
        using var handler=Connect(window);
        if(id.StartsWith("spotify:show:",StringComparison.Ordinal) || id.StartsWith("spotify:episode:",StringComparison.Ordinal))
        {
            var sessions=(SessionManager)Field(window,"_sessions")!;
            var target=sessions.Current.Items.Single(x=>x.Id==id);
            var view=(string)Call(window,"StoreSpotifyPodcastsForSession","spotify",new[]{target})!;
            window.ShowCurrentSession(view);
        }
        Call(window,"SelectMediaItem",id);Pump();Check(window.ActionItem?.Id==id,"Fixture not selected: "+id+" actual="+window.ActionItem?.Id+" view="+Field(window,"_currentView"));Send(window,Key.Right);
        WaitUntil(()=>handler.Urls.Any(x=>x.Contains(endpoint)));
        Check(Menu() is null,"Pojedyncze przejście wymaga zbędnego wyboru");
    }
    private static void WaitUntil(Func<bool> condition)
    {
        var clock=System.Diagnostics.Stopwatch.StartNew();
        while(!condition() && clock.Elapsed<TimeSpan.FromSeconds(3)){Pump();Thread.Sleep(10);}
        Pump();Check(condition(),"Nie wykonano właściwej nawigacji");
    }
    private sealed class BrowseHandler : HttpMessageHandler
    {
        internal List<string> Urls {get;}=[];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)
        {
            var url=request.RequestUri!.AbsolutePath;Urls.Add(url);
            const string track="""{"id":"new-track","name":"Nowy utwór próby","type":"track","duration_ms":120000,"is_playable":true,"artists":[{"id":"artist1","name":"Wykonawca próby"}]}""";
            var json=url.EndsWith("/artists/artist1/albums",StringComparison.Ordinal)?"""{"items":[{"id":"album2","name":"Album wybranego wykonawcy","type":"album","album_type":"album","album_group":"album","artists":[{"id":"artist1","name":"Wykonawca próby"}]}],"next":null}""":
                url.EndsWith("/tracks",StringComparison.Ordinal)?"{\"items\":["+track+"],\"next\":null}":
                url.EndsWith("/search",StringComparison.Ordinal)?"{\"tracks\":{\"items\":["+track+"],\"next\":null}}":
                url.EndsWith("/albums/album1",StringComparison.Ordinal)?"""{"id":"album1","name":"Album próby","album_type":"album","artists":[{"id":"artist1","name":"Wykonawca próby"}]}""":
                "{\"items\":[],\"next\":null}";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(json,Encoding.UTF8,"application/json")});
        }
    }
    private static ContextMenu? Menu()=>Keyboard.FocusedElement is MenuItem item
        ? ItemsControl.ItemsControlFromItemContainer(item) as ContextMenu : null;
    private static void Send(MainWindow window,Key key)
    {
        var old=new byte[256];Check(GetKeyboardState(old),"Brak stanu klawiatury");
        try
        {
            Check(SetKeyboardState(new byte[256]),"Brak klawiatury próbnej");
            var e=new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(window),Environment.TickCount,key)
                {RoutedEvent=Keyboard.PreviewKeyDownEvent,Source=window.MediaList};
            Call(window,"Window_PreviewKeyDown",window,e);
            if(!e.Handled)Call(window,"MediaList_PreviewKeyDown",window.MediaList,e);
        }
        finally{SetKeyboardState(old);}
        Pump();
    }
    [DllImport("user32.dll")]private static extern bool GetKeyboardState(byte[] keys);
    [DllImport("user32.dll")]private static extern bool SetKeyboardState(byte[] keys);
    private static void Pump()=>Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.ContextIdle);
    private static object? Field(MainWindow window,string name)=>typeof(MainWindow).GetField(name,Private)!.GetValue(window);
    private static object? Call(MainWindow window,string name,params object?[] args)=>typeof(MainWindow).GetMethod(name,Private)!.Invoke(window,args);
    private static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
}
