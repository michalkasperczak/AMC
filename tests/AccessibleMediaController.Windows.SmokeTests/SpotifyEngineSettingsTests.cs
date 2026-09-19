using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Threading;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Windows;

internal static class SpotifyEngineSettingsTests
{
    internal static void Run()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            var root = Path.Combine(Path.GetTempPath(), "amc-spotify-engine-ui-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            SettingsWindow? window = null;
            try
            {
                var store = new ConfigurationStore(Path.Combine(root, "state.json"));
                var state = new PersistedState();
                state.Podcasts.DownloadsFolder = Path.Combine(root, "podcasts");
                state.Radio.RecordingsFolder = Path.Combine(root, "recordings");
                window = new SettingsWindow(state, store);
                var combo = window.FindName("SpotifyEngineCombo") as ComboBox;
                Check(combo is not null, "W ustawieniach brak wyboru silnika Spotify.");
                Check(AutomationProperties.GetName(combo!) == "Silnik odtwarzania Spotify", "Brak czytelnej nazwy pola silnika.");
                Check(AutomationProperties.GetHelpText(combo!).Contains("ponownym uruchomieniu AMC", StringComparison.Ordinal), "Brak informacji, kiedy zmiana zacznie działać.");
                var options = combo!.Items.OfType<ComboBoxItem>().ToArray();
                Check(options.Length == 2, "Nie ma dokładnie dwóch silników do wyboru.");
                Check(((ComboBoxItem)combo.SelectedItem).Tag?.ToString() == "Librespot", "Domyślny silnik nie jest Librespot.");
                var property = typeof(AppSettings).GetProperty("SpotifyEngine")
                    ?? throw new Exception("Model nie ma trwałego wyboru silnika Spotify.");
                combo.SelectedItem = options.Single(item => item.Tag?.ToString() == "Sdk");
                Check(property.GetValue(state.Settings)?.ToString() == "Librespot", "Edycja okna zmieniła oryginalny stan przed zapisem.");
                window.Close();
                window = new SettingsWindow(state, store);
                combo = (ComboBox)window.FindName("SpotifyEngineCombo");
                Check(((ComboBoxItem)combo.SelectedItem).Tag?.ToString() == "Librespot", "Anulowanie nie zachowało poprzedniego silnika.");
                combo.SelectedItem = combo.Items.OfType<ComboBoxItem>().Single(item => item.Tag?.ToString() == "Sdk");
                // Arrange a gap left by a removed session. This tests the UI's
                // save boundary separately from startup's slot normalization.
                var rows = ((ListBox)window.FindName("SessionOrderList")).Items.Cast<object>().ToArray();
                foreach (var row in rows.Skip(1))
                {
                    var slot = row.GetType().GetProperty("Slot")!;
                    slot.SetValue(row, (int)slot.GetValue(row)! + 1);
                }
                var displayedSlots = rows.ToDictionary(
                    row => (int)row.GetType().GetProperty("Slot")!.GetValue(row)!,
                    row => (string)row.GetType().GetProperty("SessionId")!.GetValue(row)!);
                var move = typeof(SettingsWindow).GetMethod("MoveSession", BindingFlags.Instance | BindingFlags.NonPublic)!;
                ((ListBox)window.FindName("SessionOrderList")).SelectedIndex = 0;
                move.Invoke(window, new object[] { 1 });
                move.Invoke(window, new object[] { -1 });
                Check(rows.Select(row => (int)row.GetType().GetProperty("Slot")!.GetValue(row)!).Order().SequenceEqual(displayedSlots.Keys.Order()),
                    "Przeniesienie sesji i cofnięcie ruchu skasowało przerwę w numerach skrótów.");
                var save = Walk(window).OfType<Button>().Single(button => button.Content?.ToString() == "_Zapisz");
                var shown = window;
                shown.ContentRendered += (_, _) => shown.Dispatcher.BeginInvoke(new Action(() =>
                {
                    try { save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); }
                    catch (Exception exception) { failure = exception; shown.Close(); }
                }));
                Check(window.ShowDialog() == true, "Rzeczywisty przycisk Zapisz nie zatwierdził ustawień.");
                if (failure is not null) throw failure;
                var result = window.ResultState ?? throw new Exception("Brak wyniku ustawień.");
                Check(property.GetValue(result.Settings)?.ToString() == "Sdk", "Okno nie przekazało wyboru SDK.");
                Check(result.Settings.SessionSlots.OrderBy(pair => pair.Key).SequenceEqual(displayedSlots.OrderBy(pair => pair.Key)),
                    "Zapis wyboru silnika przenumerował niezmieniane skróty innych sesji.");
                store.Save(result);
                var reloaded = store.LoadOrCreate();
                Check(property.GetValue(reloaded.Settings)?.ToString() == "Sdk", "Zapis i odczyt zgubił wybór SDK.");
                window = new SettingsWindow(reloaded, store);
                combo = (ComboBox)window.FindName("SpotifyEngineCombo");
                Check(((ComboBoxItem)combo.SelectedItem).Tag?.ToString() == "Sdk", "Ponownie otwarte ustawienia nie pokazują zapisanego SDK.");
                var order = (ListBox)window.FindName("SessionOrderList");
                Check(!order.Items.Cast<object>().Any(item => item.GetType().GetProperty("SessionId")?.GetValue(item)?.ToString() == "spotifyLibrespot"), "Druga sesja wróciła na listę kolejności.");
            }
            catch (Exception exception) { failure = exception; }
            finally
            {
                window?.Close();
                Dispatcher.CurrentDispatcher.InvokeShutdown();
                try { Directory.Delete(root, true); } catch (IOException) { }
            }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(45))) throw new Exception("Pomiar wyboru silnika Spotify przekroczył czas.");
        if (failure is not null) throw new Exception("Ustawienia odtwarzacza Spotify: " + failure.Message, failure);
        Console.WriteLine("OK: dostępny wybór odtwarzacza Spotify, anulowanie, zapis i odczyt po restarcie");
    }

    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static IEnumerable<DependencyObject> Walk(DependencyObject root)
    {
        yield return root;
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
            foreach (var descendant in Walk(child)) yield return descendant;
    }
}
