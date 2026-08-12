using System.IO;
using System.Windows;
using AccessibleMediaController.Core.Configuration;

namespace AccessibleMediaController.Windows;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var configurationDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AccessibleMediaController");
        var store = new ConfigurationStore(Path.Combine(configurationDirectory, "state.json"));

        PersistedState state;
        try
        {
            state = store.LoadOrCreate();
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                $"Nie udało się odczytać konfiguracji. Program uruchomi ustawienia domyślne.\n\n{exception.Message}",
                "Dostępny kontroler multimedialny",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            state = ConfigurationStore.CreateDefaultState();
        }

        var mainWindow = new MainWindow(state, store);
        MainWindow = mainWindow;
        mainWindow.Show();
    }
}
