using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Threading;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Windows;

namespace AccessibleMediaController.Windows.SmokeTests;

/// <summary>
/// Nowe algorytmy tempa (Speedy dla mowy, Signalsmith dla muzyki) byly w
/// modelu i w torze odtwarzania, ale uzytkownik NIE MIAL ich jak wybrac -
/// zadne okno nie wystawialo pola. Ten test mierzy RZECZYWISTE kontrolki:
/// liste w glownych Ustawieniach (zakres globalny) oraz liste w oknie opcji
/// pliku i folderu (zakres lokalny, z dziedziczeniem).
///
/// Bez kont, sieci i dzwieku - tylko stan konfiguracji w katalogu tymczasowym.
/// </summary>
internal static class TempoAlgorithmChoiceUiTests
{
    internal static void Run()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            var root = Path.Combine(
                Path.GetTempPath(),
                "amc-tempo-algorithm-ui-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                SynchronizationContext.SetSynchronizationContext(
                    new DispatcherSynchronizationContext());
                Console.WriteLine("ETAP: TestUstawieniaOgolneCzytajaIZapisujaAlgorytm");
                TestUstawieniaOgolneCzytajaIZapisujaAlgorytm(root);
                Console.WriteLine("ETAP: TestOknoPlikuDajeDziedziczenieIWlasnyWybor");
                TestOknoPlikuDajeDziedziczenieIWlasnyWybor();
                Console.WriteLine("ETAP: TestObcySilnikNiePokazujeWyboru");
                TestObcySilnikNiePokazujeWyboru();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
            finally
            {
                try { Directory.Delete(root, true); } catch { /* katalog tymczasowy */ }
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) throw failure;
    }

    private static void TestUstawieniaOgolneCzytajaIZapisujaAlgorytm(string root)
    {
        var directory = Path.Combine(root, "ogolne");
        Directory.CreateDirectory(directory);
        var state = new PersistedState();
        // Stary profil bez tego pola ma zostac przy dotychczasowym SoundTouch.
        Assert(state.Settings.Audio.TempoAlgorithm == PlaybackTempoAlgorithm.SoundTouch,
            "Domyslnym algorytmem tempa nie jest dotychczasowy SoundTouch.");
        var store = new ConfigurationStore(
            Path.Combine(directory, "state.json"),
            Path.Combine(directory, "library.db"));

        // Prawdziwa kontrolka, prawdziwy przycisk Zapisz - nie wlasna atrapa.
        var zapisane = WithSettings(state, store, save: true, body: window =>
        {
            var combo = (ComboBox)window.FindName("TempoAlgorithmCombo");
            Assert(combo is not null, "Ustawienia nie maja listy wyboru algorytmu tempa.");
            Assert(combo!.SelectedItem is ComboBoxItem { Tag: "SoundTouch" },
                "Lista algorytmu tempa nie wczytuje zapisanej wartosci.");

            var nazwy = combo.Items.Cast<ComboBoxItem>()
                .Select(AutomationProperties.GetName)
                .ToArray();
            Assert(nazwy.Length == 3, "Lista algorytmu tempa nie ma trzech pozycji.");
            Assert(nazwy.Contains("Mowa — Speedy", StringComparer.Ordinal)
                && nazwy.Contains("Muzyka — Signalsmith", StringComparer.Ordinal)
                && nazwy.Contains("Dotychczasowy — SoundTouch", StringComparer.Ordinal),
                "Pozycje listy nie maja uzytkowych nazw dla NVDA: " + string.Join(" | ", nazwy));
            Assert(nazwy.All(nazwa => !string.IsNullOrWhiteSpace(nazwa)),
                "Pozycja listy algorytmu tempa czytalaby sie jako obiekt albo enum.");

            var pomoc = AutomationProperties.GetHelpText(combo) ?? string.Empty;
            // UI NIE MOZE obiecywac natychmiastowego zastosowania: natywny tor
            // podmienia silnik dopiero przy otwarciu kolejnego strumienia.
            Assert(pomoc.Contains("po ponownym otwarciu materiału", StringComparison.Ordinal),
                "Pomoc listy nie mowi, ze algorytm obowiazuje po ponownym otwarciu materialu.");

            // Rzeczywisty wybor uzytkownika: Mowa - Speedy.
            combo.SelectedItem = combo.Items.Cast<ComboBoxItem>()
                .First(item => (string?)item.Tag == "Speech");
        }) ?? throw new InvalidOperationException("Zapisz nie zwrocil stanu ustawien.");

        Assert(zapisane.Settings.Audio.TempoAlgorithm == PlaybackTempoAlgorithm.Speech,
            "Wybor Mowa — Speedy nie trafil do ustawien: "
            + zapisane.Settings.Audio.TempoAlgorithm);

        // Trwalosc i powrot do okna: wybor widoczny po ponownym otwarciu.
        store.Save(zapisane);
        var wczytane = store.LoadOrCreate();
        Assert(wczytane.Settings.Audio.TempoAlgorithm == PlaybackTempoAlgorithm.Speech,
            "Zapis na dysk zgubil wybrany algorytm tempa.");
        WithSettings(wczytane, store, save: false, body: window =>
            Assert(((ComboBox)window.FindName("TempoAlgorithmCombo")).SelectedItem
                    is ComboBoxItem { Tag: "Speech" },
                "Po ponownym otwarciu okno nie pokazuje zapisanego algorytmu tempa."));

        // Droga powrotna: Muzyka - Signalsmith musi dzialac tak samo.
        var muzyka = WithSettings(wczytane, store, save: true, body: window =>
        {
            var combo = (ComboBox)window.FindName("TempoAlgorithmCombo");
            combo.SelectedItem = combo.Items.Cast<ComboBoxItem>()
                .First(item => (string?)item.Tag == "Music");
        });
        Assert(muzyka is not null
            && muzyka.Settings.Audio.TempoAlgorithm == PlaybackTempoAlgorithm.Music,
            "Wybor Muzyka — Signalsmith nie trafil do ustawien.");
    }

    private static PersistedState? WithSettings(
        PersistedState state,
        ConfigurationStore store,
        bool save,
        Action<SettingsWindow> body)
    {
        var window = new SettingsWindow(state, store, SettingsTarget.TempoAlgorithm);
        Exception? failure = null;
        window.ContentRendered += (_, _) => window.Dispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                body(window);
                var label = save ? "_Zapisz" : "_Anuluj";
                var button = Walk(window).OfType<Button>()
                    .Single(candidate => candidate.Content?.ToString() == label);
                typeof(Button)
                    .GetMethod(
                        "OnClick",
                        System.Reflection.BindingFlags.Instance
                            | System.Reflection.BindingFlags.NonPublic)!
                    .Invoke(button, null);
            }
            catch (Exception exception)
            {
                failure = exception;
                try { window.DialogResult = false; }
                catch (InvalidOperationException) { window.Close(); }
            }
        }), DispatcherPriority.ApplicationIdle);

        var accepted = window.ShowDialog();
        if (failure is not null) throw failure;
        if (save && accepted != true)
            throw new InvalidOperationException("Przycisk Zapisz nie zatwierdzil ustawien.");
        if (!save && accepted == true)
            throw new InvalidOperationException("Przycisk Anuluj zatwierdzil ustawienia.");
        return window.ResultState;
    }

    private static IEnumerable<System.Windows.DependencyObject> Walk(
        System.Windows.DependencyObject root)
    {
        yield return root;
        foreach (var child in System.Windows.LogicalTreeHelper.GetChildren(root)
            .OfType<System.Windows.DependencyObject>())
        {
            foreach (var descendant in Walk(child)) yield return descendant;
        }
    }

    private static void TestOknoPlikuDajeDziedziczenieIWlasnyWybor()
    {
        ItemPlaybackOptionsWindow? plik = null;
        ItemPlaybackOptionsWindow? folder = null;
        try
        {
            plik = new ItemPlaybackOptionsWindow(
                "Nagranie.mp3", ResumePositionMode.Inherit, null, null, null, null);
            var combo = (ComboBox)plik.FindName("TempoAlgorithmBox");
            Assert(combo is not null, "Okno opcji pliku nie ma listy algorytmu tempa.");
            Assert(plik.SelectedTempoAlgorithmOverride is null,
                "Brak wlasnego wyboru powinien oznaczac dziedziczenie, nie konkretny algorytm.");
            Assert(combo!.Visibility == System.Windows.Visibility.Visible,
                "Lista algorytmu tempa jest ukryta dla pliku lokalnego.");
            var etykiety = combo.Items.Cast<object>().Select(item => item.ToString() ?? string.Empty).ToArray();
            Assert(etykiety.Length == 4,
                "Lista pliku nie ma dziedziczenia i trzech algorytmow: " + string.Join(" | ", etykiety));
            Assert(etykiety[0].Contains("folderu", StringComparison.Ordinal),
                "Pierwsza pozycja nie nazywa dziedziczenia zakresem: " + etykiety[0]);

            combo.SelectedIndex = 2;
            Assert(plik.SelectedTempoAlgorithmOverride == PlaybackTempoAlgorithm.Music,
                "Wybor w oknie pliku nie jest odczytywany: " + plik.SelectedTempoAlgorithmOverride);

            // Zapisany wybor folderu ma wrocic na liste przy ponownym otwarciu.
            folder = new ItemPlaybackOptionsWindow(
                "Folder: Audiobooki", ResumePositionMode.Inherit, null, null, null, null,
                target: ItemPlaybackOptionsTarget.LocalFolder,
                tempoAlgorithmOverride: PlaybackTempoAlgorithm.Speech);
            Assert(folder.SelectedTempoAlgorithmOverride == PlaybackTempoAlgorithm.Speech,
                "Okno folderu nie wczytuje zapisanego algorytmu tempa.");
            var nazwaFolderu = AutomationProperties.GetName(
                (ComboBox)folder.FindName("TempoAlgorithmBox"));
            Assert(nazwaFolderu == "Sposób przeliczania tempa plików w folderze",
                "Lista folderu nie ma wlasnej nazwy dla NVDA: " + nazwaFolderu);
        }
        finally
        {
            plik?.Close();
            folder?.Close();
        }
    }

    private static void TestObcySilnikNiePokazujeWyboru()
    {
        // Spotify i radio na zywo nie licza tempa naszym lancuchem - pokazanie
        // tam listy obiecywaloby dzialanie, ktorego nie ma.
        ItemPlaybackOptionsWindow? spotify = null;
        ItemPlaybackOptionsWindow? radio = null;
        try
        {
            spotify = new ItemPlaybackOptionsWindow(
                "Utwór Spotify", ResumePositionMode.Inherit, null, null, null, null,
                target: ItemPlaybackOptionsTarget.SpotifyItem,
                showAudioProcessingOptions: false,
                showPlaybackRateOption: false);
            Assert(((ComboBox)spotify.FindName("TempoAlgorithmBox")).Visibility
                    != System.Windows.Visibility.Visible,
                "Okno Spotify pokazuje wybor algorytmu tempa, ktorego ta usluga nie wykonuje.");

            radio = new ItemPlaybackOptionsWindow(
                "Stacja", ResumePositionMode.Inherit, null, null, null, null,
                target: ItemPlaybackOptionsTarget.RadioStation);
            Assert(((ComboBox)radio.FindName("TempoAlgorithmBox")).Visibility
                    != System.Windows.Visibility.Visible,
                "Okno stacji radiowej pokazuje wybor algorytmu tempa.");
        }
        finally
        {
            spotify?.Close();
            radio?.Close();
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
