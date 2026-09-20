using System.Reflection;
using System.Windows.Controls;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Windows;

/// <summary>
/// ZGLOSZENIE Michala: Ctrl+Shift+cyfra na presecie FOLDERU w Bibliotece mowi
/// "Preset numer 5, folder Nazwa". Numer presetu jest zbedny - uzytkownik sam
/// go wlasnie nacisnal, a przy radiu i WiiM program numeru nie powtarza.
/// Preset folderu ma zapowiadac to samo, co zwykle wejscie w folder:
/// "Foldery, Nazwa".
///
/// Pomiar idzie przez PRAWDZIWE okno glowne i produkcyjna metode
/// ActivatePreset - bez podstawiania formatera. Sprawdzamy trzy rzeczy naraz:
/// tekst kontekstu fokusu (to, co NVDA czyta przed pozycja listy), rzeczywista
/// nawigacje do folderu presetu oraz nienaruszone definicje presetow.
/// </summary>
internal static class LocalFolderPresetAnnouncementTests
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;

    /// <summary>Zapowiedz zgodna ze zwyklym wejsciem w folder (OpenFolderPath).</summary>
    private const string OczekiwanyPrefiksKoncerty = "Foldery, Koncerty";
    private const string OczekiwanyPrefiksAudiobooki = "Foldery, Audiobooki";
    private const string OczekiwanyPrefiksPusty = "Foldery, Pusty";

    internal static void Run()
    {
        PresetFolderuNieMowiNumeruPresetu();
        PowtorzonaAktywacjaMowiToSamo();
        PustyFolderPresetuMowiNazweFolderuIPustaListe();
        Console.WriteLine(
            "OK: preset folderu lokalnego zapowiada sam folder (sloty 3 i 5, powtorzenie, folder pusty)");
    }

    /// <summary>
    /// Sloty 3 i 5: po skrocie program ma powiedziec sam folder, przejsc do niego
    /// i nie zmienic zapisanych presetow.
    /// </summary>
    private static void PresetFolderuNieMowiNumeruPresetu()
    {
        WOknie((window, state, katalogi) =>
        {
            foreach (var (slot, folder, oczekiwany) in new[]
                     {
                         (3, katalogi.Koncerty, OczekiwanyPrefiksKoncerty),
                         (5, katalogi.Audiobooki, OczekiwanyPrefiksAudiobooki)
                     })
            {
                AktywujPreset(window, slot);

                var prefiks = KontekstFokusu(window);
                Sprawdz(
                    prefiks is not null,
                    $"Preset {slot} nie ustawil zadnego kontekstu fokusu - NVDA nie powie, gdzie jestesmy.");
                Sprawdz(
                    !prefiks!.Contains("Preset", StringComparison.OrdinalIgnoreCase),
                    $"Preset {slot} nadal mowi numer presetu: \"{prefiks}\". "
                    + "Przy radiu i WiiM numeru nie powtarzamy, folder ma byc tak samo cichy.");
                Sprawdz(
                    string.Equals(prefiks, oczekiwany, StringComparison.Ordinal),
                    $"Preset {slot} zapowiada \"{prefiks}\", a ma zapowiadac \"{oczekiwany}\" "
                    + "- dokladnie tak, jak zwykle wejscie w ten folder.");

                Sprawdz(
                    string.Equals(state.LocalMedia.CurrentFolderPath, folder, StringComparison.OrdinalIgnoreCase),
                    $"Preset {slot} nie wszedl do swojego folderu "
                    + $"(jest \"{state.LocalMedia.CurrentFolderPath}\", ma byc \"{folder}\").");
                Sprawdz(
                    string.Equals(state.LocalMedia.LibraryView, "Foldery", StringComparison.Ordinal),
                    $"Preset {slot} nie przelaczyl Biblioteki na widok Folderow.");

                var etykieta = EtykietaPierwszegoWiersza(window);
                Sprawdz(
                    etykieta is not null,
                    $"Widok folderu presetu {slot} jest pusty - nie ma czego odczytac.");
                var odczyt = EfektywnaEtykietaFokusu(window, etykieta!);
                Sprawdz(
                    odczyt.StartsWith(oczekiwany + ", ", StringComparison.Ordinal),
                    $"Czytnik uslyszy \"{odczyt}\", a ma uslyszec \"{oczekiwany}, <pozycja>\".");
            }

            SprawdzNienaruszonePresety(state);
        });
    }

    /// <summary>
    /// Ten sam preset dwa razy pod rzad: druga zapowiedz musi byc identyczna.
    /// </summary>
    private static void PowtorzonaAktywacjaMowiToSamo()
    {
        WOknie((window, state, katalogi) =>
        {
            AktywujPreset(window, 5);
            var pierwsza = KontekstFokusu(window);
            AktywujPreset(window, 5);
            var druga = KontekstFokusu(window);

            Sprawdz(
                string.Equals(pierwsza, OczekiwanyPrefiksAudiobooki, StringComparison.Ordinal),
                $"Pierwsza aktywacja presetu 5 mowi \"{pierwsza}\" zamiast \"{OczekiwanyPrefiksAudiobooki}\".");
            Sprawdz(
                string.Equals(druga, pierwsza, StringComparison.Ordinal),
                $"Powtorzona aktywacja presetu 5 mowi co innego: \"{druga}\" po \"{pierwsza}\".");
            Sprawdz(
                string.Equals(state.LocalMedia.CurrentFolderPath, katalogi.Audiobooki, StringComparison.OrdinalIgnoreCase),
                "Powtorzona aktywacja presetu 5 wyprowadzila z folderu presetu.");

            SprawdzNienaruszonePresety(state);
        });
    }

    /// <summary>
    /// Folder presetu bez plikow: nazwa folderu nadal musi wybrzmiec, razem z
    /// informacja o pustej liscie. Numeru presetu nadal nie mowimy.
    /// </summary>
    private static void PustyFolderPresetuMowiNazweFolderuIPustaListe()
    {
        WOknie((window, state, katalogi) =>
        {
            AktywujPreset(window, 7);

            Sprawdz(
                string.Equals(state.LocalMedia.CurrentFolderPath, katalogi.Pusty, StringComparison.OrdinalIgnoreCase),
                "Preset pustego folderu nie wszedl do tego folderu.");
            Sprawdz(
                window.MediaList.Items.Count == 0,
                "Test pustego folderu mierzy niewlasciwy stan: lista nie jest pusta.");

            var nazwa = System.Windows.Automation.AutomationProperties.GetName(window.MediaList);
            Sprawdz(
                !string.IsNullOrEmpty(nazwa),
                "Pusty folder presetu nie dostal zadnej etykiety dostepnosci.");
            Sprawdz(
                !nazwa.Contains("Preset", StringComparison.OrdinalIgnoreCase),
                $"Pusty folder presetu nadal mowi numer presetu: \"{nazwa}\".");
            Sprawdz(
                string.Equals(nazwa, OczekiwanyPrefiksPusty + ", lista pusta", StringComparison.Ordinal),
                $"Pusty folder presetu mowi \"{nazwa}\", a ma mowic "
                + $"\"{OczekiwanyPrefiksPusty}, lista pusta\".");

            SprawdzNienaruszonePresety(state);
        });
    }

    // --- pomiar na prawdziwym oknie ------------------------------------------

    /// <summary>Produkcyjna sciezka skrotu Ctrl+Shift+cyfra.</summary>
    private static void AktywujPreset(MainWindow window, int slot) =>
        typeof(MainWindow)
            .GetMethod("ActivatePreset", Flags, null, [typeof(int), typeof(bool), typeof(bool)], null)!
            .Invoke(window, [slot, false, false]);

    /// <summary>Tekst, ktory produkcja dokleja przed pozycja listy dla czytnika.</summary>
    private static string? KontekstFokusu(MainWindow window) =>
        (string?)typeof(MainWindow).GetField("_focusContextPrefix", Flags)!.GetValue(window);

    private static string? EtykietaPierwszegoWiersza(MainWindow window)
    {
        if (window.MediaList.Items.Count == 0) return null;
        var row = window.MediaList.Items[0]!;
        return (string?)row.GetType().GetProperty("Label")!.GetValue(row);
    }

    /// <summary>
    /// To, co czytnik naprawde uslyszy na wierszu. Gdy okno nie jest pokazane,
    /// kontener wiersza moze jeszcze nie istniec - wtedy uzywamy tego samego
    /// PRODUKCYJNEGO formatera, ktorego uzywa ApplyFocusContext.
    /// </summary>
    private static string EfektywnaEtykietaFokusu(MainWindow window, string etykietaWiersza)
    {
        window.MediaList.UpdateLayout();
        if (window.MediaList.ItemContainerGenerator.ContainerFromIndex(0) is ListBoxItem container)
        {
            typeof(MainWindow).GetMethod("ApplyFocusContext", Flags)!.Invoke(window, [container]);
            var nazwa = System.Windows.Automation.AutomationProperties.GetName(container);
            if (!string.IsNullOrEmpty(nazwa)) return nazwa;
        }

        var prefiks = KontekstFokusu(window);
        var sufiks = (string?)typeof(MainWindow).GetField("_focusContextSuffix", Flags)!.GetValue(window);
        return MainWindowNavigationPolicy.FormatFocusedListEntry(etykietaWiersza, prefiks, sufiks);
    }

    private static void SprawdzNienaruszonePresety(PersistedState state)
    {
        var entries = state.SessionPresets.EntriesBySession["local"];
        Sprawdz(entries.Count == 3, $"Aktywacja presetu zmienila liczbe presetow na {entries.Count}.");
        foreach (var (slot, tytul) in new[] { (3, "Koncerty"), (5, "Audiobooki"), (7, "Pusty") })
        {
            var entry = entries.SingleOrDefault(candidate => candidate.Slot == slot);
            Sprawdz(entry is not null, $"Aktywacja presetu skasowala definicje slotu {slot}.");
            Sprawdz(
                string.Equals(entry!.TargetKind, "folder", StringComparison.Ordinal)
                && string.Equals(entry.TargetTitle, tytul, StringComparison.Ordinal),
                $"Aktywacja presetu zmienila definicje slotu {slot} "
                + $"(rodzaj \"{entry.TargetKind}\", tytul \"{entry.TargetTitle}\").");
        }
    }

    private sealed record Katalogi(string Korzen, string Koncerty, string Audiobooki, string Pusty);

    /// <summary>
    /// Prawdziwe MainWindow na wlasnym watku STA, wlasny katalog tymczasowy,
    /// wlasny magazyn konfiguracji. Bez pokazywania okna, bez kont, bez sieci,
    /// bez dzwieku i bez globalnych skrotow (te rejestruje dopiero
    /// OnSourceInitialized pokazanego okna).
    /// </summary>
    private static void WOknie(Action<MainWindow, PersistedState, Katalogi> sprawdz)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            var root = Path.Combine(Path.GetTempPath(), "amc-preset-folder-" + Guid.NewGuid().ToString("N"));
            MainWindow? window = null;
            try
            {
                var biblioteka = Path.Combine(root, "Biblioteka");
                var katalogi = new Katalogi(
                    biblioteka,
                    Path.Combine(biblioteka, "Koncerty"),
                    Path.Combine(biblioteka, "Audiobooki"),
                    Path.Combine(biblioteka, "Pusty"));
                Directory.CreateDirectory(katalogi.Koncerty);
                Directory.CreateDirectory(katalogi.Audiobooki);
                Directory.CreateDirectory(katalogi.Pusty);
                var plikKoncert = Path.Combine(katalogi.Koncerty, "koncert.mp3");
                var plikAudiobook = Path.Combine(katalogi.Audiobooki, "rozdzial.mp3");
                File.WriteAllBytes(plikKoncert, []);
                File.WriteAllBytes(plikAudiobook, []);

                var state = new PersistedState();
                state.Settings.Updates.CheckAutomatically = false;
                state.Settings.LastSessionId = "local";
                state.LocalMedia.LibraryView = "Foldery";
                state.LocalMedia.FolderSources.Add(new LocalFolderSourceSettings
                {
                    Path = katalogi.Korzen,
                    DisplayName = "Biblioteka testowa"
                });
                state.LocalMedia.Items.Add(Utwor(plikKoncert, "Koncert"));
                state.LocalMedia.Items.Add(Utwor(plikAudiobook, "Rozdzial"));
                state.SessionPresets.EntriesBySession["local"] =
                [
                    Preset(3, katalogi.Koncerty, "Koncerty"),
                    Preset(5, katalogi.Audiobooki, "Audiobooki"),
                    Preset(7, katalogi.Pusty, "Pusty")
                ];

                Directory.CreateDirectory(root);
                var store = new ConfigurationStore(Path.Combine(root, "state.json"));
                var options = (System.Text.Json.JsonSerializerOptions)typeof(ConfigurationStore)
                    .GetField("JsonOptions", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
                File.WriteAllText(
                    Path.Combine(root, "state.json"),
                    System.Text.Json.JsonSerializer.Serialize(state, options));
                state = store.LoadOrCreate();

                window = new MainWindow(state, store);
                var sesje = (SessionManager)typeof(MainWindow).GetField("_sessions", Flags)!.GetValue(window)!;
                sesje.SelectSession("local");
                sprawdz(window, state, katalogi);
            }
            catch (Exception exception)
            {
                failure = exception;
            }
            finally
            {
                window?.Close();
                try { Directory.Delete(root, true); } catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(60)))
            throw new Exception("Okno presetow folderu nie zakonczylo testu w 60 s.");
        if (failure is not null)
            throw new Exception("Zapowiedz presetu folderu lokalnego.", failure);
    }

    private static LocalMediaItemSettings Utwor(string path, string title) => new()
    {
        Id = "local:" + path.ToUpperInvariant(),
        Title = title,
        Path = path,
        IsInLibrary = true,
        IsAvailable = true
    };

    private static SessionPresetEntry Preset(int slot, string folder, string tytul) => new()
    {
        Slot = slot,
        TargetId = $"folder:{folder.ToUpperInvariant()}",
        TargetKind = "folder",
        TargetTitle = tytul,
        TargetLocation = folder
    };

    private static void Sprawdz(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
