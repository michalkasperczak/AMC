using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Core.Devices.WiiM;
using AccessibleMediaController.Windows;

internal static partial class SonosFavoritePlayRealOwnerTests
{
    internal static void RunClipboardLocations()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            string? file = null;
            var prior = Clipboard.GetText();
            try
            {
                using var h = RealHarness.Create();
                object? Call(object target, string name, params object[] args) =>
                    target.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(target, args);
                void Text(string expected)
                {
                    if (Clipboard.GetText() != expected) throw new Exception("Nieprawidłowy tekst schowka: " + Clipboard.GetText());
                }
                var a = new MediaItem { Id="a", Title="Nazwa nie jest adresem", Source="https://radio.invalid/a?k=1&b=2", Kind=MediaItemKind.Station };
                var b = new MediaItem { Id="b", Title="Druga nazwa", Source="https://radio.invalid/b", Kind=MediaItemKind.Station };
                Call(h.Window, "CopyItemLocations", new MediaItem[] { a, b }, "radio");
                Text(a.Source + Environment.NewLine + b.Source);
                Call(h.Window, "CopySearchResultLocations", (object)new SearchWindow.SearchResult[] { new("radio", a), new("radio", b) });
                Text(a.Source + Environment.NewLine + b.Source);
                var video = new MediaItem { Id="youtube:abcdefghijk", ExternalId="abcdefghijk", Title="Tytuł filmu", Source="https://www.youtube.com/watch?v=abcdefghijk", Kind=MediaItemKind.Track };
                Call(h.Window, "CopyItemLocations", new MediaItem[] { video }, "youtube");
                Text(video.Source!);
                var episode = new MediaItem { Id="episode-a", Title="Odcinek", Source="https://podcast.invalid/a.mp3", Kind=MediaItemKind.Episode };
                Call(h.Window, "CopyItemLocations", new MediaItem[] { episode }, "podcasts");
                Text(episode.Source!);
                file = System.IO.Path.GetTempFileName();
                var local = new MediaItem { Id="local", Title="Plik", Source=file, Kind=MediaItemKind.Track };
                Call(h.Window, "CopyItemLocations", new MediaItem[] { local }, "local");
                Text(file);
                if (!Clipboard.GetFileDropList().Cast<string>().SequenceEqual(new[] {file})) throw new Exception("Zaginął FileDrop");
                Call(h.Window, "CopySearchResultLocations", (object)new SearchWindow.SearchResult[] { new("local", local), new("radio", a) });
                Text(file + Environment.NewLine + a.Source);
                if (!Clipboard.ContainsFileDropList()) throw new Exception("Zaginął mieszany FileDrop");
                var presets = new RadioPresetsWindow(new RadioPresetChoice[] {
                    new(1,"1","1","a","Nazwa A",a.Source), new(2,"2","2","b","Nazwa B",b.Source)}, "a");
                ((ListBox)presets.FindName("PresetList")).SelectAll();
                Call(presets, "CopySelectedNamesAndLinks");
                Text(a.Source + Environment.NewLine + b.Source);
                presets.Close();
                var wiim = new WiiMDevicePresetsWindow("Test", new WiiMPresetInformation[] {
                    new(1,"Nazwa A","radio",a.Source), new(2,"Bez URI","radio",null)});
                var list=(ListBox)wiim.FindName("PresetList");
                list.SelectedIndex=1;Clipboard.SetText("nie zmieniaj");
                Call(wiim,"CopySelected",true);Text("nie zmieniaj");
                list.SelectAll();Call(wiim,"CopySelected",true);Text(a.Source!);
                Call(wiim,"CopySelected",false);Text("Nazwa A"+Environment.NewLine+"Bez URI");
                wiim.Close();
                Console.WriteLine("OK: rzeczywisty schowek — lista, wyszukiwanie, YouTube, podcast, FileDrop, presety AMC/WiiM i brak URI");
            }
            catch(Exception e){failure=e;}
            finally
            {
                if(file is not null) System.IO.File.Delete(file);
                if(prior.Length==0) Clipboard.Clear();else Clipboard.SetText(prior);
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });
        thread.SetApartmentState(ApartmentState.STA);thread.IsBackground=true;thread.Start();
        if(!thread.Join(TimeSpan.FromSeconds(45))) throw new Exception("Limit próby schowka");
        if(failure is not null) throw failure;
    }
}
