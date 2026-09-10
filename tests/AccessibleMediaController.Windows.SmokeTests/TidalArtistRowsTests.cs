using System.Collections;
using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using AccessibleMediaController.Core.Presentation;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Windows;

internal static class TidalArtistRowsTests
{
    // Runs on the existing TIDAL STA test thread, without constructing the real
    // MainWindow (which would load the user's accounts, audio and schedules).
    internal static void Run()
    {
        var artist = new MediaItem { ExternalId = "artists:test", Title = "Wykonawca", Kind = MediaItemKind.Artist };
        var factory = typeof(MainWindow).GetMethod("CreateTidalArtistSectionRows", BindingFlags.NonPublic | BindingFlags.Static)!;
        object[] Rows() => ((IEnumerable)factory.Invoke(null, [artist])!).Cast<object>().ToArray();
        var expected = new[] { "Albumy", "Utwory", "Podobni wykonawcy" };
        var initial = Rows();
        Check(initial.Select(row => row.ToString()).SequenceEqual(expected), "Kategorie mają techniczne etykiety");
        string Id(object row) => ((MediaItem)row.GetType().GetProperty("Item")!.GetValue(row)!).Id;
        Check(initial.Select(Id).Distinct().Count() == 3 && initial.Select(Id).SequenceEqual(Rows().Select(Id)),
            "Identyfikatory nawigacji kategorii nie są stabilne");
        foreach (var row in initial)
        {
            var item = (MediaItem)row.GetType().GetProperty("Item")!.GetValue(row)!;
            Check(item.ExternalId is null && !item.IsFavorite && !item.IsInLibrary,
                "Kategoria udaje element zdalnej kolekcji");
            Check((string)row.GetType().GetProperty("NavigationText")!.GetValue(row)! == row.ToString(),
                "Nawigacja literami nie odpowiada nazwie kategorii");
            Check(row.GetType().GetProperty("ArtistSection")!.GetValue(row) is ArtistBrowseSection,
                "Brak oddzielnej roli nawigacyjnej");
        }
        for (var iteration = 0; iteration < 2; iteration++)
        {
            var style = new Style(typeof(ListBoxItem));
            style.Setters.Add(new Setter(AutomationProperties.NameProperty, new Binding("Label")));
            var list = new ListBox
            {
                ItemsSource = Rows(), DisplayMemberPath = "Label", ItemContainerStyle = style,
                IsTextSearchEnabled = false, SelectedIndex = 0
            };
            TextSearch.SetTextPath(list, "NavigationText");
            var window = new Window { Content = list, Width = 350, Height = 200, ShowInTaskbar = false };
            try
            {
                window.Show(); window.Activate(); Drain(window.Dispatcher);
                for (var i = 0; i < 3; i++)
                {
                    list.SelectedIndex = i; list.UpdateLayout();
                    var container = (ListBoxItem)list.ItemContainerGenerator.ContainerFromIndex(i);
                    container.Focus(); Keyboard.Focus(container);
                    Check(UIElementAutomationPeer.CreatePeerForElement(container)!.GetName() == expected[i],
                        "UI Automation nie otrzymuje samej nazwy kategorii");
                    Check(container.IsKeyboardFocused, "Kategoria nie otrzymała fokusu");
                }
                // Same list is reused for album contents, then Escape restores
                // the category row by stable ID, including after a refresh.
                var remembered = Id(list.SelectedItem);
                list.ItemsSource = new[] { "Album testowy" };
                list.ItemsSource = Rows();
                list.SelectedItem = list.Items.Cast<object>().Single(row => Id(row) == remembered);
                list.UpdateLayout();
                var restored = (ListBoxItem)list.ItemContainerGenerator.ContainerFromItem(list.SelectedItem);
                restored.Focus(); Keyboard.Focus(restored);
                Check(restored.IsKeyboardFocused && list.SelectedIndex == 2, "Powrót do wybranej kategorii");
                AutomationProperties.SetName(restored, "Wstecz, Podobni wykonawcy");
                restored.ClearValue(AutomationProperties.NameProperty);
                Check(UIElementAutomationPeer.CreatePeerForElement(restored)!.GetName() == expected[2],
                    "Jednorazowy komunikat powrotu usunął podstawową etykietę");
            }
            finally { window.Close(); }
        }
    }

    private static void Drain(Dispatcher dispatcher)
    {
        var frame = new DispatcherFrame();
        dispatcher.BeginInvoke(() => frame.Continue = false, DispatcherPriority.ApplicationIdle);
        Dispatcher.PushFrame(frame);
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
