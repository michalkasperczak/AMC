using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Windows;

internal static class RadioScheduleCopyTests
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    internal static void Run()
    {
        var cases=new (string,Action<RadioSchedulesWindow,RadioRecordingScheduleSettings>)[]{
            ("Ctrl+D kopiuje plan bez uzbrojenia nagrania i bez zmiany oryginału", CopyDisabled),
            ("Kolejne kopie mają wolne numery, także przy kopiowaniu kopii", CopyNames),
            ("Przycisk Powiel i wiersz kopii podają osobną nazwę", CopyButtonAndLabel),
            ("Powielenie przez MainWindow zachowuje nazwę po zapisie i restarcie", MainWindowRoundtrip),
            ("Enter edytuje nazwę, dni i czas kopii niezależnie od oryginału", (w,s)=>MainWindowRoundtripCore(w,s,true)),
            ("Usuwanie pyta o nazwę kopii, a anulowanie zachowuje oba plany", DeleteNamesCopy),
            ("Brak zaznaczenia nie tworzy planu, Spacja włącza tylko kopię", SelectionAndEnable),
            ("Celowo usunięta kopia nie wraca po ponownym starcie", (w,s)=>MainWindowRoundtripCore(w,s,false,true)),
        };
        var failed=0;
        foreach(var (name,body) in cases)
        {
            Exception? error=null;
            var thread=new Thread(()=>
            {
                RadioSchedulesWindow? window=null;
                try
                {
                    SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
                    var original=Fixture();
                    window=new RadioSchedulesWindow(
                        new[]{new MediaItem{Id="station-test",Title="Stacja próby",Kind=MediaItemKind.Station,Source="https://example.invalid/radio"}},
                        new[]{original},new[]{original.Id},original.StationId,true,RadioRecordingFormat.Mp3,192)
                        {ShowInTaskbar=true};
                    window.Show();Pump();body(window,original);
                }
                catch(Exception e){error=e;}
                finally{window?.Close();Dispatcher.CurrentDispatcher.InvokeShutdown();}
            }){IsBackground=true};
            thread.SetApartmentState(ApartmentState.STA);thread.Start();
            if(!thread.Join(TimeSpan.FromSeconds(30)))error=new Exception("Timeout harmonogramu");
            if(error is null)Console.WriteLine("OK: "+name);
            else{failed++;Console.Error.WriteLine(name+": "+error);}
        }
        Console.WriteLine($"POWIELANIE: {cases.Length-failed} OK / {failed} BLAD / razem {cases.Length}");
        if(failed>0)throw new Exception("Nie przeszły wszystkie testy powielania harmonogramu");
    }
    internal static void RunInteractive(string root)
    {
        Exception? error=null;
        var thread=new Thread(()=>
        {
            RadioSchedulesWindow? window=null;
            try
            {
                Directory.CreateDirectory(root);
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
                AccessibleMediaController.Windows.Services.DiagnosticLog.Initialize(Path.Combine(root,"logs"));
                var statePath=Path.Combine(root,"state.json");var existed=File.Exists(statePath);
                var store=new ConfigurationStore(statePath);var state=store.LoadOrCreate();
                state.Radio.WakeScheduledRecordings=false;
                if(!existed){var original=Fixture();original.WakeComputer=false;state.Radio.RecordingSchedules=new(){original};store.Save(state);}
                window=new RadioSchedulesWindow(
                    new[]{new MediaItem{Id="station-test",Title="Stacja próby",Kind=MediaItemKind.Station,Source="https://example.invalid/radio"}},
                    state.Radio.RecordingSchedules,Array.Empty<string>(),"station-test",false,RadioRecordingFormat.Mp3,192)
                    {ShowInTaskbar=true};
                window.CommittedChanges+=(_,_)=>{state.Radio.RecordingSchedules=window.ResultSchedules.ToList();store.Save(state);};
                window.ShowDialog();
            }
            catch(Exception e){error=e;}
            finally{window?.Close();Dispatcher.CurrentDispatcher.InvokeShutdown();}
        });
        thread.SetApartmentState(ApartmentState.STA);thread.Start();thread.Join();if(error is not null)throw error;
    }
    private static RadioRecordingScheduleSettings Fixture()=>new(){
        Id="schedule-original",StationId="station-test",StationName="Stacja próby",StreamUrl="https://example.invalid/radio",
        NextStartUtcTicks=DateTime.UtcNow.AddDays(2).Ticks,TimeZoneId=TimeZoneInfo.Local.Id,
        DurationMinutes=97,SegmentMinutes=13,Recurrence=RadioScheduleRecurrence.SelectedDays,
        ActiveDays=new(){DayOfWeek.Tuesday,DayOfWeek.Friday},OutputFolder=Path.GetTempPath(),
        FileNameTemplate="{stacja}-{data}-{czas}",RecordingFormat=RadioRecordingFormat.Mp3,RecordingBitrateKbps=192,
        WakeComputer=true,Enabled=true,SuppressedOccurrenceStartUtcTicks=1,LastFailureUtcTicks=DateTime.UtcNow.AddHours(-1).Ticks,
        LastFailureMessage="Błąd próby",LastFailureAcknowledged=false
    };
    private static void CopyDisabled(RadioSchedulesWindow window,RadioRecordingScheduleSettings original)
    {
        var before=JsonSerializer.Serialize(original);var commits=0;window.CommittedChanges+=(_,_)=>commits++;
        Send(window,Key.D,ModifierKeys.Control);
        Check(window.ResultSchedules.Count==2,"Ctrl+D nie utworzyło osobnej kopii planu");
        Check(commits==1 && window.HasCommittedChanges,"Kopia nie przeszła zwykłej drogi zapisu");
        var copy=window.ResultSchedules.Single(x=>x.Id!=original.Id);
        Check(copy.Id.Length>0 && !copy.Enabled,"Kopia nie ma nowego ID albo została od razu włączona");
        Check(copy.LastFailureUtcTicks is null && copy.LastFailureMessage.Length==0 && copy.LastFailureAcknowledged && copy.SuppressedOccurrenceStartUtcTicks is null,"Skopiowano stan błędu lub zatrzymanego wystąpienia");
        Check(JsonSerializer.Serialize(window.ResultSchedules.Single(x=>x.Id==original.Id))==before,"Zmieniono oryginalny plan");
        Check(copy.StationId==original.StationId && copy.StationName==original.StationName && copy.StreamUrl==original.StreamUrl,"Zmieniono stację zamiast nazwy planu");
        Check(copy.ActiveDays.SequenceEqual(original.ActiveDays) && !ReferenceEquals(copy.ActiveDays,original.ActiveDays),"Dni nagrywania nie są niezależną kopią");
        var name=typeof(RadioRecordingScheduleSettings).GetProperty("Name");
        Check(name?.GetValue(copy)?.ToString()=="Stacja próby (2)","Kopia nie ma osobnej numerowanej nazwy planu");
        Check(window.OwnedWindows.Count==0,"Powielenie wymusiło otwarcie edytora");
        var row=window.SchedulesList.SelectedItem!;
        Check(((RadioRecordingScheduleSettings)row.GetType().GetProperty("Schedule")!.GetValue(row)!).Id==copy.Id,"Kopia nie jest zaznaczona");
        Check(window.SchedulesList.IsKeyboardFocusWithin,"Fokus nie pozostał na kopii");
    }
    private static void CopyButtonAndLabel(RadioSchedulesWindow window,RadioRecordingScheduleSettings original)
    {
        var button=window.FindName("DuplicateButton") as Button;
        Check(button is not null,"Nie ma dostępnego przycisku Powiel");
        var peer=new System.Windows.Automation.Peers.ButtonAutomationPeer(button!);
        Check(peer.GetName()=="Powiel" && peer.GetAcceleratorKey()=="Ctrl+D","Przycisk nie podaje nazwy lub skrótu");
        typeof(Button).GetMethod("OnClick",Private)!.Invoke(button,null);Pump();
        var selected=window.SchedulesList.SelectedItem!;
        Check(selected.ToString()!.StartsWith("Stacja próby (2), wyłączone",StringComparison.Ordinal),"Czytnik nie rozróżni nazwy kopii: "+selected);
        Check(selected.GetType().GetProperty("NavigationText")!.GetValue(selected)?.ToString()=="Stacja próby (2)","Nawigacja po nazwie pomija numer kopii");
        var copy=window.ResultSchedules.Single(x=>x.Id!=original.Id);
        var excluded=new HashSet<string>{"Id","Name","Enabled","SuppressedOccurrenceStartUtcTicks","LastFailureUtcTicks","LastFailureMessage","LastFailureAcknowledged"};
        foreach(var property in typeof(RadioRecordingScheduleSettings).GetProperties().Where(x=>!excluded.Contains(x.Name)))
            Check(JsonSerializer.Serialize(property.GetValue(copy))==JsonSerializer.Serialize(property.GetValue(original)),"Nie skopiowano ustawienia "+property.Name);
        var liveCopy=(RadioRecordingScheduleSettings)selected.GetType().GetProperty("Schedule")!.GetValue(selected)!;
        liveCopy.ActiveDays.Clear();
        var liveOriginal=(RadioRecordingScheduleSettings)window.SchedulesList.Items[0].GetType().GetProperty("Schedule")!.GetValue(window.SchedulesList.Items[0])!;
        Check(liveOriginal.ActiveDays.SequenceEqual(original.ActiveDays),"Zmiana dni kopii zmienia oryginał");
    }
    private static void MainWindowRoundtrip(RadioSchedulesWindow unused,RadioRecordingScheduleSettings original) => MainWindowRoundtripCore(unused,original,false);

    private static void MainWindowRoundtripCore(RadioSchedulesWindow unused,RadioRecordingScheduleSettings original,bool edit,bool remove=false)
    {
        unused.Hide();
        var root=Path.Combine(Path.GetTempPath(),"amc-schedule-roundtrip-"+Guid.NewGuid().ToString("N"));
        MainWindow? main=null;
        try
        {
            var store=new ConfigurationStore(Path.Combine(root,"state.json"));var state=store.LoadOrCreate();
            state.Settings.LastSessionId="radio";state.Settings.Updates.CheckAutomatically=state.Settings.Updates.InstallOnExit=false;
            state.Tidal.ClientId=state.Spotify.ClientId="";state.Tidal.CachedCollectionItems.Clear();state.Spotify.CachedCollectionItems.Clear();
            state.LocalMedia.Items.Clear();state.LocalMedia.FolderSources.Clear();state.WiiM.Devices.Clear();
            state.Podcasts.Episodes.Clear();state.Podcasts.Subscriptions.Clear();state.Podcasts.DownloadsFolder=Path.Combine(root,"downloads");
            state.Radio.Stations.Clear();state.Radio.WakeScheduledRecordings=false;state.Radio.RecordingsFolder=Path.Combine(root,"recordings");
            original.WakeComputer=false;state.Radio.RecordingSchedules=new(){original};store.Save(state);
            // Literal old format: it did not contain the new optional plan name.
            var oldFile=Path.Combine(root,"state.json");
            var oldJson=System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(oldFile))!;
            oldJson["radio"]!["recordingSchedules"]![0]!.AsObject().Remove("name");
            File.WriteAllText(oldFile,oldJson.ToJsonString());
            main=new MainWindow(store.LoadOrCreate(),store){SuppressDesktopIntegrationForTests=true};
            typeof(MainWindow).GetField("_applicationUpdateStartOverride",Private)!.SetValue(main,(Func<bool,bool>)(_=>throw new Exception("No installer in tests")));
            main.ContentRendered-=(EventHandler)Delegate.CreateDelegate(typeof(EventHandler),main,typeof(MainWindow).GetMethod("Window_ContentRendered",Private)!);
            main.Show();Pump();
            Exception? actionError=null;var done=false;
            var timer=new DispatcherTimer(DispatcherPriority.Background){Interval=TimeSpan.FromMilliseconds(20)};
            timer.Tick+=(_,_)=>
            {
                var dialog=main.OwnedWindows.OfType<RadioSchedulesWindow>().FirstOrDefault();
                if(dialog is null || done)return;
                done=true;timer.Stop();
                try{Send(dialog,Key.D,ModifierKeys.Control);if(edit)EditThroughEnter(dialog);if(remove)DeleteConfirmed(dialog);}
                catch(Exception e){actionError=e;}
                finally{dialog.Close();}
            };
            timer.Start();
            try{typeof(MainWindow).GetMethod("ShowRadioSchedules",Private)!.Invoke(main,null);}
            finally{timer.Stop();}
            Check(done,"Nie otwarto prawdziwego menedżera harmonogramów");if(actionError is not null)throw actionError;
            main.Close();
            var clock=System.Diagnostics.Stopwatch.StartNew();
            while(main.IsVisible && clock.Elapsed<TimeSpan.FromSeconds(8)){Pump();Thread.Sleep(10);}
            Check(!main.IsVisible,"Okno nie zakończyło zapisu");
            var restored=store.LoadOrCreate();
            Check(restored.Radio.RecordingSchedules.Count==(remove?1:2),"Zapis nie zachował właściwej liczby planów");
            if(remove)
            {
                Check(restored.Radio.RecordingSchedules.Single().Id==original.Id,"Zamiast kopii usunięto oryginał");
                var reopenedAfterDelete=new RadioSchedulesWindow(Array.Empty<MediaItem>(),restored.Radio.RecordingSchedules,Array.Empty<string>(),null,false,RadioRecordingFormat.Mp3,192);
                try{reopenedAfterDelete.Show();Pump();Check(reopenedAfterDelete.SchedulesList.Items.Count==1 && !reopenedAfterDelete.SchedulesList.Items[0].ToString()!.Contains("(2)"),"Kopia wróciła do odtworzonego okna");}
                finally{reopenedAfterDelete.Close();}
                return;
            }
            var copy=restored.Radio.RecordingSchedules.Single(x=>x.Id!=original.Id);
            var expectedName=edit?"Własna audycja":"Stacja próby (2)";
            Check(copy.Name==expectedName,"MainWindow zgubił nazwę kopii w zapisie");
            if(edit)
            {
                Check(copy.ActiveDays.SequenceEqual(new[]{DayOfWeek.Wednesday}) && copy.DurationMinutes==82,"Zmiany dni/czasu nie przetrwały zapisu");
                Check(!copy.Enabled,"Edytor sam uzbroił wyłączoną kopię");
                var start=TimeZoneInfo.ConvertTimeFromUtc(new DateTime(copy.NextStartUtcTicks,DateTimeKind.Utc),RadioScheduleCalculator.ResolveTimeZone(copy.TimeZoneId));
                Check(start.Hour==16 && start.Minute==47,"Godzina początku kopii nie przetrwała zapisu");
                var kept=restored.Radio.RecordingSchedules.Single(x=>x.Id==original.Id);
                Check(kept.ActiveDays.SequenceEqual(original.ActiveDays) && kept.DurationMinutes==original.DurationMinutes && kept.NextStartUtcTicks==original.NextStartUtcTicks && kept.Name==original.Name,"Edycja kopii zmieniła oryginał");
            }
            var reopened=new RadioSchedulesWindow(Array.Empty<MediaItem>(),restored.Radio.RecordingSchedules,Array.Empty<string>(),null,false,RadioRecordingFormat.Mp3,192);
            try{reopened.Show();Pump();Check(reopened.SchedulesList.Items.Cast<object>().Any(x=>x.ToString()!.StartsWith(expectedName+", wyłączone",StringComparison.Ordinal)),"Po ponownym otwarciu brak nazwanej kopii");}
            finally{reopened.Close();}
        }
        finally{main?.Close();try{Directory.Delete(root,true);}catch(IOException){}catch(UnauthorizedAccessException){}}
    }
    private static void EditThroughEnter(RadioSchedulesWindow manager)
    {
        Exception? error=null;var seen=false;
        var timer=new DispatcherTimer(DispatcherPriority.Background){Interval=TimeSpan.FromMilliseconds(20)};
        timer.Tick+=(_,_)=>
        {
            var editor=manager.OwnedWindows.OfType<RadioScheduleEditorWindow>().FirstOrDefault();
            if(editor is null || seen)return;seen=true;timer.Stop();
            try
            {
                var name=editor.FindName("ScheduleNameBox") as TextBox;
                Check(name is not null,"Edytor nie ma pola osobnej nazwy planu");
                Check(name!.Text=="Stacja próby (2)" && System.Windows.Automation.AutomationProperties.GetName(name)=="Nazwa planu","Edytor nie odtworzył nazwy kopii");
                name.Text="Własna audycja";
                var days=(System.Collections.IEnumerable)typeof(RadioScheduleEditorWindow).GetField("_dayChoices",Private)!.GetValue(editor)!;
                foreach(var day in days)
                    day.GetType().GetProperty("IsChecked")!.SetValue(day,(DayOfWeek)day.GetType().GetProperty("Value")!.GetValue(day)! == DayOfWeek.Wednesday);
                var picker=(System.Windows.Forms.NumericUpDown)typeof(RadioScheduleEditorWindow).GetField("_durationMinutesPicker",Private)!.GetValue(editor)!;
                picker.Value=22; // The existing hours component remains one hour.
                var start=(System.Windows.Forms.DateTimePicker)typeof(RadioScheduleEditorWindow).GetField("_timePicker",Private)!.GetValue(editor)!;
                start.Value=DateTime.Today.AddHours(16).AddMinutes(47);
                typeof(RadioScheduleEditorWindow).GetMethod("Save_Click",Private)!.Invoke(editor,new object[]{editor,new RoutedEventArgs()});
                Check(editor.ResultSchedule is not null,"Edytor odmówił zapisu danych próby");
            }
            catch(Exception e){error=e;editor.Close();}
        };
        timer.Start();try{Send(manager,Key.Enter,ModifierKeys.None);}finally{timer.Stop();}
        Check(seen,"Enter nie otworzył edytora kopii");if(error is not null)throw error;
    }
    private static void DeleteConfirmed(RadioSchedulesWindow window)
    {
        var timer=new DispatcherTimer(DispatcherPriority.Background){Interval=TimeSpan.FromMilliseconds(20)};
        Exception? error=null;var seen=false;
        timer.Tick+=(_,_)=>
        {
            var dialog=window.OwnedWindows.OfType<Window>().FirstOrDefault();if(dialog is null)return;
            timer.Stop();seen=true;
            try
            {
                var yes=Elements<Button>(dialog).Single(x=>x.Content?.ToString()?.Replace("_","")=="Tak");
                typeof(Button).GetMethod("OnClick",Private)!.Invoke(yes,null);
            }
            catch(Exception e){error=e;dialog.Close();}
        };
        timer.Start();try{Send(window,Key.Delete,ModifierKeys.None);}finally{timer.Stop();}
        Check(seen,"Usuwanie nie zapytało o potwierdzenie");if(error is not null)throw error;
    }
    private static void DeleteNamesCopy(RadioSchedulesWindow window,RadioRecordingScheduleSettings original)
    {
        Send(window,Key.D,ModifierKeys.Control);
        string? question=null;
        var timer=new DispatcherTimer(DispatcherPriority.Background){Interval=TimeSpan.FromMilliseconds(20)};
        timer.Tick+=(_,_)=>
        {
            var dialog=window.OwnedWindows.OfType<Window>().FirstOrDefault();if(dialog is null)return;
            timer.Stop();question=Elements<TextBox>(dialog).FirstOrDefault(x=>x.IsReadOnly)?.Text;dialog.Close();
        };
        timer.Start();try{Send(window,Key.Delete,ModifierKeys.None);}finally{timer.Stop();}
        Check(question?.Contains("Stacja próby (2)")==true,"Potwierdzenie usuwania nie wymienia kopii: "+question);
        Check(window.ResultSchedules.Count==2,"Anulowanie usunęło plan");
    }
    private static IEnumerable<T> Elements<T>(DependencyObject root) where T:DependencyObject
    {
        if(root is T element)yield return element;
        foreach(var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
            foreach(var found in Elements<T>(child))yield return found;
    }
    private static void SelectionAndEnable(RadioSchedulesWindow window,RadioRecordingScheduleSettings original)
    {
        window.SchedulesList.SelectedIndex=-1;Pump();Send(window,Key.D,ModifierKeys.Control);
        Check(window.ResultSchedules.Count==1 && !window.HasCommittedChanges,"Brak zaznaczenia utworzył przypadkowy plan");
        window.SchedulesList.SelectedIndex=0;Pump();Send(window,Key.D,ModifierKeys.Control);
        Send(window,Key.Space,ModifierKeys.None);
        Check(window.ResultSchedules.Single(x=>x.Id!=original.Id).Enabled,"Spacja nie włączyła kopii");
        Check(window.ResultSchedules.Single(x=>x.Id==original.Id).Enabled==original.Enabled,"Spacja na kopii zmieniła oryginał");
    }
    private static void CopyNames(RadioSchedulesWindow window,RadioRecordingScheduleSettings original)
    {
        Send(window,Key.D,ModifierKeys.Control);Send(window,Key.D,ModifierKeys.Control);
        window.SchedulesList.SelectedIndex=0;Pump();Send(window,Key.D,ModifierKeys.Control);
        var names=window.ResultSchedules.Where(x=>x.Id!=original.Id).Select(x=>x.Name).OrderBy(x=>x).ToArray();
        Check(names.SequenceEqual(new[]{"Stacja próby (2)","Stacja próby (3)","Stacja próby (4)"}),"Nazwy kopii są sprzeczne lub powtarzają się: "+string.Join(" | ",names));
        Check(window.ResultSchedules.Select(x=>x.Id).Distinct().Count()==4,"Powtórzono ID planu");
    }
    private static void Send(RadioSchedulesWindow window,Key key,ModifierKeys modifiers)
    {
        var old=new byte[256];Check(GetKeyboardState(old),"Brak klawiatury");var keys=new byte[256];
        if(modifiers.HasFlag(ModifierKeys.Control))keys[0x11]=keys[0xA2]=0x80;
        try
        {
            Check(SetKeyboardState(keys),"Nie ustawiono klawiatury testu");
            var e=new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(window),Environment.TickCount,key){RoutedEvent=Keyboard.PreviewKeyDownEvent,Source=window.SchedulesList};
            typeof(RadioSchedulesWindow).GetMethod("SchedulesList_PreviewKeyDown",Private)!.Invoke(window,new object[]{window.SchedulesList,e});
        }
        finally{SetKeyboardState(old);}
        Pump();
    }
    [DllImport("user32.dll")]private static extern bool GetKeyboardState(byte[] keys);
    [DllImport("user32.dll")]private static extern bool SetKeyboardState(byte[] keys);
    private static void Pump()=>Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.ContextIdle);
    private static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
}
