using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Windows;

internal static class TimeshiftRateHelpTests
{
    internal static void Run()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            var root = Path.Combine(Path.GetTempPath(), "amc-timeshift-help-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            SettingsWindow? window = null;
            try
            {
                var state = new PersistedState();
                window = new SettingsWindow(state, new ConfigurationStore(Path.Combine(root, "state.json")));
                var help = Walk(window).OfType<TextBlock>().Select(block => block.Text)
                    .Single(text => text.StartsWith("Podczas słuchania z buforu", StringComparison.Ordinal));
                if (!help.Contains("Shift+przecinek", StringComparison.Ordinal)
                    || !help.Contains("Shift+kropka", StringComparison.Ordinal)
                    || !help.Contains("Ctrl+kropka", StringComparison.Ordinal)
                    || help.Contains("Ctrl+Shift+strzałka", StringComparison.Ordinal))
                    throw new Exception("Pomoc TimeShift podaje inne skróty niż obsługa klawiatury: " + help);
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
        if (!thread.Join(TimeSpan.FromSeconds(30))) throw new Exception("Pomiar treści pomocy TimeShift przekroczył czas.");
        if (failure is not null) throw new Exception("Nieprawidłowa pomoc tempa TimeShift", failure);
        Console.WriteLine("OK: okno ustawień TimeShift podaje właściwe skróty tempa");
    }

    private static IEnumerable<DependencyObject> Walk(DependencyObject root)
    {
        yield return root;
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
            foreach (var descendant in Walk(child)) yield return descendant;
    }
}
