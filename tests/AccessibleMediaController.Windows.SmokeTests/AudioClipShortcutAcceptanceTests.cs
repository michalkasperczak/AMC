using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Windows;

internal static class AudioClipShortcutAcceptanceTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    internal static void Run()
    {
        var cases = new (string, Action<MainWindow, DemoMediaSession, string>)[] {
            ("Ctrl+S otwiera istniejący eksport po I/O", ExportShortcut),
            ("Ctrl+S podcastu eksportuje tylko w kontekście fragmentu", PodcastShortcut),
            ("Ctrl+D otwiera wybór istniejącego celu i bezpiecznie anuluje", AppendShortcut),
            ("Ctrl+D zapisuje prawdziwy plik przez przycisk Dopisz", AppendThroughWindow),
            ("Menu, pomoc i paleta podają nowe skróty bez X", ShortcutDescriptions),
            ("Uszkodzony WAV daje dostępny błąd bez wyjątku dyspozytora", (w,s,p)=>CorruptTargetIsReported(w,s,p,".wav")),
            ("Błąd FFmpeg daje dostępny komunikat bez wyjątku dyspozytora", (w,s,p)=>CorruptTargetIsReported(w,s,p,".flac")),
        };
        var failed = 0;
        foreach (var (name, action) in cases)
        {
            Exception? error = null;
            var thread = new Thread(() => {
                var root = Path.Combine(Path.GetTempPath(), "amc-clip-acceptance-" + Guid.NewGuid().ToString("N"));
                MainWindow? window = null;
                try
                {
                    SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
                    Directory.CreateDirectory(root);
                    var path = Path.Combine(root, "źródło próby.wav");
                    var format = new NAudio.Wave.WaveFormat(8000, 16, 1);
                    using (var writer = new NAudio.Wave.WaveFileWriter(path, format)) writer.Write(new byte[format.AverageBytesPerSecond * 10]);
                    var store = new ConfigurationStore(Path.Combine(root, "state.json"));
                    var state = store.LoadOrCreate();
                    state.Settings.Updates.CheckAutomatically = state.Settings.Updates.InstallOnExit = false;
                    state.Settings.LastSessionId = "local";
                    state.Tidal.ClientId = state.Spotify.ClientId = "";
                    state.Tidal.CachedCollectionItems.Clear(); state.Spotify.CachedCollectionItems.Clear();
                    state.LocalMedia.Items.Clear(); state.LocalMedia.FolderSources.Clear();
                    state.Podcasts.Episodes.Clear(); state.Podcasts.Subscriptions.Clear();
                    state.Podcasts.DownloadsFolder = Path.Combine(root, "downloads");
                    state.Radio.Stations.Clear(); state.Radio.RecordingSchedules.Clear(); state.Radio.WakeScheduledRecordings = false;
                    state.Radio.RecordingsFolder = Path.Combine(root, "recordings"); state.WiiM.Devices.Clear();
                    state.LocalMedia.Items.Add(new LocalMediaItemSettings { Id="clip-source", Title="Źródło próby", Path=path,
                        IsInLibrary=true, DurationTicks=TimeSpan.FromSeconds(10).Ticks });
                    store.Save(state);
                    window = new MainWindow(store.LoadOrCreate(), store) { SuppressDesktopIntegrationForTests=true };
                    typeof(MainWindow).GetField("_applicationUpdateStartOverride",Private)!.SetValue(window,(Func<bool,bool>)(_=>throw new Exception("No installer in tests")));
                    window.ContentRendered -= (EventHandler)Delegate.CreateDelegate(typeof(EventHandler),window,typeof(MainWindow).GetMethod("Window_ContentRendered",Private)!);
                    var sessions=(SessionManager)typeof(MainWindow).GetField("_sessions",Private)!.GetValue(window)!;
                    var local=sessions.SelectSession("local")!;
                    typeof(DemoMediaSession).GetField("_output",Private)!.SetValue(local,new ControlledOutput());
                    local.Play(local.Items.Single(item=>item.Id=="clip-source"));
                    window.Show(); Pump(); Call(window,"ShowPlayerView"); Pump();
                    action(window,local,path);
                }
                catch(Exception e) { error=e; }
                finally { window?.Close(); Dispatcher.CurrentDispatcher.InvokeShutdown(); try { Directory.Delete(root,true); } catch(IOException){} catch(UnauthorizedAccessException){} }
            }) { IsBackground=true };
            thread.SetApartmentState(ApartmentState.STA);thread.Start();
            if(!thread.Join(TimeSpan.FromSeconds(45))) error=new Exception("Timeout");
            if(error is null) Console.WriteLine("OK: "+name);
            else { failed++;Console.Error.WriteLine(name+": "+error); }
        }
        Console.WriteLine($"KLIPY: {cases.Length-failed} OK / {failed} BLAD / razem {cases.Length}");
        if(failed!=0) throw new Exception("Skróty fragmentów: nie przeszły wszystkie przypadki");
    }

    private static void CorruptTargetIsReported(MainWindow window, DemoMediaSession session, string source, string extension)
    {
        var target=Path.Combine(Path.GetDirectoryName(source)!,"uszkodzony cel"+extension);File.WriteAllText(target,"To nie jest RIFF ani dźwięk.");
        var before=File.ReadAllBytes(target);var original=File.ReadAllBytes(source);
        session.SetPosition(TimeSpan.FromSeconds(1));Send(window,Key.I,ModifierKeys.None);
        session.SetPosition(TimeSpan.FromSeconds(3));Send(window,Key.O,ModifierKeys.None);
        AudioClipAppendWindow? seen=null;var started=false;var errorDialog=false;Exception? unhandled=null;
        DispatcherUnhandledExceptionEventHandler trap=(_,e)=>{unhandled=e.Exception;e.Handled=true;seen?.Close();};
        Dispatcher.CurrentDispatcher.UnhandledException+=trap;
        var timer=new DispatcherTimer(DispatcherPriority.Background){Interval=TimeSpan.FromMilliseconds(30)};
        var clock=System.Diagnostics.Stopwatch.StartNew();
        timer.Tick+=(_,_)=>{
            seen ??= window.OwnedWindows.OfType<AudioClipAppendWindow>().FirstOrDefault();
            if(seen is null)return;
            if(!started){started=true;seen.TargetPathBox.Text=target;typeof(System.Windows.Controls.Button).GetMethod("OnClick",Private)!.Invoke(seen.AppendButton,null);return;}
            var message=seen.OwnedWindows.OfType<Window>().FirstOrDefault(w=>w.Title=="Nie udało się dopisać fragmentu");
            if(message is not null){errorDialog=true;message.Close();return;}
            if((errorDialog && seen.AppendButton.IsEnabled) || clock.Elapsed>TimeSpan.FromSeconds(20))seen.Close();
        };
        timer.Start();
        try {Send(window,Key.D,ModifierKeys.Control);}
        finally {timer.Stop();Dispatcher.CurrentDispatcher.UnhandledException-=trap;}
        Check(started,"Próba nie otworzyła okna");
        Check(unhandled is null,"Błąd pliku wydostał się z async void do dyspozytora: "+unhandled?.GetType().Name);
        Check(errorDialog && !string.IsNullOrWhiteSpace(seen?.AppendStatus.Text),"Brak dostępnego komunikatu o błędzie pliku");
        Check(before.SequenceEqual(File.ReadAllBytes(target)) && original.SequenceEqual(File.ReadAllBytes(source)),"Odmowa zmieniła pliki");
    }

    private static void ShortcutDescriptions(MainWindow window, DemoMediaSession session, string source)
    {
        var entries=AccessibleMediaController.Core.Presentation.CommandPaletteSearch.CreateEntries(
            AccessibleMediaController.Core.Input.KeyboardProfile.CreateDefault(),new AppSettings(),includeCommandPalette:true);
        var export=entries.Single(x=>x.CommandId==AccessibleMediaController.Core.Commands.CommandIds.ExportClip);
        var append=entries.Single(x=>x.CommandId==AccessibleMediaController.Core.Commands.CommandIds.AppendClip);
        Check(export.LocalShortcut?.StartsWith("Ctrl+S",StringComparison.Ordinal)==true,"Paleta nadal opisuje stary skrót eksportu: "+export.LocalShortcut);
        Check(append.LocalShortcut?.StartsWith("Ctrl+D",StringComparison.Ordinal)==true,"Paleta nie opisuje dołączania");
        var help=AccessibleMediaController.Core.Presentation.ShortcutHelpCatalog.CreateForContext(
            AccessibleMediaController.Core.Input.KeyboardProfile.CreateDefault(),new AppSettings(),"local",true).SelectMany(x=>x.Entries).ToArray();
        Check(help.Single(x=>x.CommandId==export.CommandId).Shortcut=="Ctrl+S","Pomoc ma inny skrót eksportu");
        Check(help.Single(x=>x.CommandId==append.CommandId).Shortcut=="Ctrl+D","Pomoc ma inny skrót dołączania");
        Call(window,"UpdateFileMenuForCurrentSession");
        foreach(var name in new[]{"PlaybackExportClipMenuItem","PlayerExportClipMenuItem","PlaybackAppendClipMenuItem","PlayerAppendClipMenuItem"})
        {
            var menu=window.FindName(name) as System.Windows.Controls.MenuItem;
            Check(menu is not null,"Brak pozycji "+name);
            var shortcut=name.Contains("Append",StringComparison.Ordinal)?"Ctrl+D":"Ctrl+S";
            Check(menu!.InputGestureText==shortcut && System.Windows.Automation.AutomationProperties.GetAcceleratorKey(menu)==shortcut,"Menu opisuje niewłaściwy skrót: "+name);
        }
        Check(HelpCommand(window,Key.X,ModifierKeys.None)!=export.CommandId,"Pozostawiono stary alias X");
    }

    private static void AppendThroughWindow(MainWindow window, DemoMediaSession session, string source)
    {
        var target=Path.Combine(Path.GetDirectoryName(source)!,"cel okna.wav");
        var format=new NAudio.Wave.WaveFormat(8000,16,1);
        using(var writer=new NAudio.Wave.WaveFileWriter(target,format))writer.Write(new byte[format.AverageBytesPerSecond*2]);
        var before=File.ReadAllBytes(target);var sourceBefore=File.ReadAllBytes(source);
        session.SetPosition(TimeSpan.FromSeconds(2));Send(window,Key.I,ModifierKeys.None);
        session.SetPosition(TimeSpan.FromSeconds(5));Send(window,Key.O,ModifierKeys.None);
        var output=(ControlledOutput)typeof(DemoMediaSession).GetField("_output",Private)!.GetValue(session)!;
        var calls=output.Calls;var opened=false;AudioClipAppendWindow? seen=null;
        var timer=new DispatcherTimer(DispatcherPriority.Background){Interval=TimeSpan.FromMilliseconds(30)};
        var clock=System.Diagnostics.Stopwatch.StartNew();
        timer.Tick+=(_,_)=>{
            seen ??= window.OwnedWindows.OfType<AudioClipAppendWindow>().FirstOrDefault();
            if(seen is null)return;
            if(clock.Elapsed>TimeSpan.FromSeconds(20)) {seen.Close();return;}
            if(opened)return;
            opened=true;seen.TargetPathBox.Text=target;
            typeof(System.Windows.Controls.Button).GetMethod("OnClick",Private)!.Invoke(seen.AppendButton,null);
        };
        timer.Start();try {Send(window,Key.D,ModifierKeys.Control);}finally{timer.Stop();}
        Check(opened && seen?.ResultPath==target && seen.Result is not null,"Okno nie zakończyło rzeczywistego dopisania: "+seen?.AppendStatus.Text);
        using var reader=new NAudio.Wave.WaveFileReader(target);
        Check(Math.Abs(reader.TotalTime.TotalSeconds-5)<0.04,"Okno nie dopisało trzech sekund do dwóch sekund celu");
        Check(sourceBefore.SequenceEqual(File.ReadAllBytes(source)),"Okno zmieniło źródło");
        Check(before.SequenceEqual(File.ReadAllBytes(seen!.Result!.BackupPath)),"Okno nie zachowało wiernej kopii celu");
        Check(session.IsPlaying && session.Position==TimeSpan.FromSeconds(5) && output.Calls==calls,"Dopisanie zmieniło słuchanie źródła");
        Check(window.PlayerPanel.IsKeyboardFocusWithin,"Dopisanie zgubiło fokus odtwarzacza");
    }

    private static void AppendShortcut(MainWindow window, DemoMediaSession session, string source)
    {
        session.SetPosition(TimeSpan.FromSeconds(2));Send(window,Key.I,ModifierKeys.None);
        session.SetPosition(TimeSpan.FromSeconds(5));Send(window,Key.O,ModifierKeys.None);
        var original=System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(source));
        var opened=false;var accessible=false;
        var timer=new DispatcherTimer(DispatcherPriority.Background){Interval=TimeSpan.FromMilliseconds(20)};
        timer.Tick+=(_,_)=>{
            var dialog=window.OwnedWindows.OfType<Window>().FirstOrDefault(w=>w.GetType().Name=="AudioClipAppendWindow");
            if(dialog is null)return;
            opened=true;timer.Stop();
            accessible=dialog.FindName("TargetPathBox") is System.Windows.Controls.TextBox box
                && !string.IsNullOrWhiteSpace(System.Windows.Automation.AutomationProperties.GetName(box));
            dialog.DialogResult=false;
        };
        timer.Start();try {Send(window,Key.D,ModifierKeys.Control);Pump();}finally{timer.Stop();}
        Check(opened,"Ctrl+D nie otworzyło okna dopisania do istniejącego pliku");
        Check(accessible,"Brak dostępnego pola ścieżki celu");
        Check(original.SequenceEqual(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(source))),"Anulowanie dołączania zmieniło oryginał");
        Check(window.PlayerPanel.IsKeyboardFocusWithin,"Anulowanie dołączania zgubiło fokus odtwarzacza");
    }

    private static string? HelpCommand(MainWindow window, Key key, ModifierKeys modifiers)
    {
        object?[] args=[key,modifiers,string.Empty];
        var found=(bool)typeof(MainWindow).GetMethod("TryResolveKeyboardHelpCommand",Private)!.Invoke(window,args)!;
        return found ? (string?)args[2] : null;
    }
    private static void PodcastShortcut(MainWindow window, DemoMediaSession unused, string source)
    {
        var sessions=(SessionManager)typeof(MainWindow).GetField("_sessions",Private)!.GetValue(window)!;
        var podcast=sessions.SelectSession("podcasts")!;
        typeof(DemoMediaSession).GetField("_output",Private)!.SetValue(podcast,new ControlledOutput());
        var item=new MediaItem { Id="clip-podcast",Title="Odcinek próby",Source=source,
            Kind=MediaItemKind.Episode,Duration=TimeSpan.FromSeconds(10) };
        podcast.ReplaceItems([item]);podcast.Play(item);Call(window,"ShowPlayerView");Pump();
        Check(HelpCommand(window,Key.S,ModifierKeys.Control)==AccessibleMediaController.Core.Commands.CommandIds.SavePodcastAs,
            "Podcast bez fragmentu stracił Zapisz odcinek jako");
        podcast.SetPosition(TimeSpan.FromSeconds(2));Send(window,Key.I,ModifierKeys.None);
        podcast.SetPosition(TimeSpan.FromSeconds(5));Send(window,Key.O,ModifierKeys.None);
        Check(HelpCommand(window,Key.S,ModifierKeys.Control)==AccessibleMediaController.Core.Commands.CommandIds.ExportClip,
            "Pomoc Ctrl+S po I/O nadal wskazuje cały odcinek zamiast fragmentu");
        CheckExportDialog(window);
        Check((bool)Call(window,"CommandVisibleInPalette",AccessibleMediaController.Core.Commands.CommandIds.ExportClip)!,"Paleta ukrywa eksport fragmentu pobranego podcastu");
        Check((bool)Call(window,"CommandVisibleInPalette",AccessibleMediaController.Core.Commands.CommandIds.AppendClip)!,"Paleta ukrywa dołączenie fragmentu pobranego podcastu");
        Call(window,"HidePlayerForBrowserNavigation"); window.MediaList.Focus();Pump();
        Check(HelpCommand(window,Key.S,ModifierKeys.Control)==AccessibleMediaController.Core.Commands.CommandIds.SavePodcastAs,
            "Po wyjściu z edycji fragmentu Ctrl+S nie wróciło do zapisu odcinka");
    }

    private static void ExportShortcut(MainWindow window, DemoMediaSession session, string source)
    {
        var before=System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(source));
        session.SetPosition(TimeSpan.FromSeconds(2)); Send(window,Key.I,ModifierKeys.None);
        session.SetPosition(TimeSpan.FromSeconds(5)); Send(window,Key.O,ModifierKeys.None);
        var state=(PersistedState)typeof(MainWindow).GetField("_state",Private)!.GetValue(window)!;
        var saved=state.LocalMedia.Items.Single(item=>item.Id=="clip-source");
        Check(saved.ClipStartTicks==TimeSpan.FromSeconds(2).Ticks && saved.ClipEndTicks==TimeSpan.FromSeconds(5).Ticks,"I/O nie zapisało prawdziwego zaznaczenia");
        CheckExportDialog(window);
        Check(before.SequenceEqual(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(source))),"Anulowanie zmieniło oryginał");
        Check(window.PlayerPanel.IsKeyboardFocusWithin,"Anulowanie nie oddało fokusu odtwarzaczowi");
    }

    private static void CheckExportDialog(MainWindow window)
    {
        var opened=false;
        var timer=new DispatcherTimer(DispatcherPriority.Background) { Interval=TimeSpan.FromMilliseconds(20) };
        timer.Tick+=(_,_)=>{
            var dialog=window.OwnedWindows.OfType<AudioClipExportWindow>().FirstOrDefault();
            if(dialog is null) return;
            opened=true; timer.Stop(); dialog.DialogResult=false;
        };
        timer.Start();
        try { Send(window,Key.S,ModifierKeys.Control); Pump(); }
        finally { timer.Stop(); }
        Check(opened,"Ctrl+S nie otworzyło istniejącego AudioClipExportWindow");
    }

    private static void Send(MainWindow window, Key key, ModifierKeys modifiers)
    {
        var old=new byte[256];Check(GetKeyboardState(old),"Keyboard state unavailable");
        var keys=new byte[256];
        if(modifiers.HasFlag(ModifierKeys.Control)) keys[0x11]=keys[0xA2]=0x80;
        if(modifiers.HasFlag(ModifierKeys.Shift)) keys[0x10]=keys[0xA0]=0x80;
        try {
            Check(SetKeyboardState(keys),"Cannot set test thread keyboard state");
            var e=new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(window),Environment.TickCount,key)
                { RoutedEvent=Keyboard.PreviewKeyDownEvent,Source=window.PlayerPlayPauseButton };
            Call(window,"Window_PreviewKeyDown",window,e);
        } finally {SetKeyboardState(old);}
        Pump();
    }
    private sealed class ControlledOutput : AccessibleMediaController.Core.Playback.IMediaOutput
    {
        public string? LoadedItemId {get;private set;}
        public TimeSpan Position {get;private set;}
        public bool SupportsPlaybackRate=>false;
        public int Calls {get;private set;}
        public void Play(MediaItem item,TimeSpan position,int volume,double rate) {LoadedItemId=item.Id;Position=position;Calls++;}
        public void Pause() {Calls++;}
        public void Stop() {LoadedItemId=null;Calls++;}
        public void Seek(TimeSpan position) {Position=position;Calls++;}
        public void SetVolume(int volume) {Calls++;}
        public void SetPlaybackRate(double rate) {Calls++;}
    }
    [DllImport("user32.dll")] private static extern bool GetKeyboardState(byte[] keys);
    [DllImport("user32.dll")] private static extern bool SetKeyboardState(byte[] keys);
    private static void Pump()=>Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.ContextIdle);
    private static object? Call(MainWindow window,string name,params object?[] args)=>typeof(MainWindow).GetMethod(name,Private)!.Invoke(window,args);
    private static void Check(bool ok,string message) {if(!ok) throw new Exception(message);}
}
