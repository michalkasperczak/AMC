using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Threading;
using AccessibleMediaController.Core.Commands;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Windows;

/// <summary>
/// DECYZJA Michala 25.09.2026: po UDANEJ edycji istniejacego nagrania i
/// sprawdzeniu pliku wynikowego nowa kopia ma byc USUWANA, a przy bledzie albo
/// niepewnosci ZACHOWANA. Kto chce kopie mimo to, wlacza globalne
/// „Zachowuj kopie po edycji”. Domyslnie opcja jest WYLACZONA.
///
/// Pomiar dotyczy USTAWIENIA i jego interfejsu, nie samego cieca: prawdziwy
/// <see cref="AppSettings"/>, prawdziwy <see cref="ConfigurationStore"/> w
/// osobnym katalogu tymczasowym i prawdziwe okno <see cref="SettingsWindow"/>.
/// Nie ma tu kont, sieci ani dzwieku.
///
/// Podzial trybow jest celowy:
/// * <see cref="RunModel"/> — czysty model i katalog polecen, BEZ okien. Da sie
///   uruchomic wszedzie, gdzie projekt sie kompiluje.
/// * <see cref="RunControls"/> — prawdziwe okno Ustawien BEZ ShowDialog: okno
///   jest konstruowane, a kontrolka odczytana z drzewa logicznego.
/// * <see cref="Run"/> — komplet, razem z przypadkami przechodzacymi przez
///   rzeczywisty przycisk Zapisz i Anuluj (ShowDialog).
/// </summary>
internal static class AudioEditBackupSettingsTests
{
    /// <summary>Etykieta zatwierdzona przez Michala. Zmiana slowa to zmiana umowy.</summary>
    private const string Etykieta = "Zachowuj kopie po edycji";

    /// <summary>Plik stanu kazdego magazynu, zeby nie zgadywac pol prywatnych.</summary>
    private static readonly Dictionary<ConfigurationStore, string> _storePaths = new();

    internal static void Run() => RunCore(0);
    internal static void RunModel() => RunCore(1);
    internal static void RunControls() => RunCore(2);

    private static void RunCore(int mode)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            var root = Path.Combine(
                Path.GetTempPath(),
                "amc-edit-backups-settings-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());

                Console.WriteLine("ETAP: TestDomyslnieWylaczoneRowniezPrzyStarymZapisie");
                TestDomyslnieWylaczoneRowniezPrzyStarymZapisie(root);
                Console.WriteLine("ETAP: TestZapisIOdczytObuWartosci");
                TestZapisIOdczytObuWartosci(root);
                Console.WriteLine("ETAP: TestCloneStatePrzenosiWybor");
                TestCloneStatePrzenosiWybor(root);
                Console.WriteLine("ETAP: TestUstawienieJestWyszukiwalne");
                TestUstawienieJestWyszukiwalne();
                Console.WriteLine("ETAP: TestKomunikatNieObiecujeKopiiKtorejNieMa");
                TestKomunikatNieObiecujeKopiiKtorejNieMa();
                Console.WriteLine("ETAP: TestDopisywanieRzeczywiscieNiesieUstawienie");
                TestDopisywanieRzeczywiscieNiesieUstawienie();
                if (mode == 1) return;

                Console.WriteLine("ETAP: TestAppendExplainsSelectedBackupPolicy");
                TestAppendExplainsSelectedBackupPolicy(root);
                Console.WriteLine("ETAP: TestKontrolkaOdtwarzaWartosc");
                TestKontrolkaOdtwarzaWartosc(root);
                Console.WriteLine("ETAP: TestKontrolkaJestDostepnaIOpisujeZakres");
                TestKontrolkaJestDostepnaIOpisujeZakres(root);
                Console.WriteLine("ETAP: TestKolejnoscTabulacji");
                TestKolejnoscTabulacji(root);
                if (mode == 2) return;

                Console.WriteLine("ETAP: TestZapisPrzenosiWybor");
                TestZapisPrzenosiWybor(root);
                Console.WriteLine("ETAP: TestAnulowanieNieZmieniaOryginalu");
                TestAnulowanieNieZmieniaOryginalu(root);
            }
            catch (Exception exception) { failure = exception; }
            finally
            {
                Dispatcher.CurrentDispatcher.InvokeShutdown();
                try { Directory.Delete(root, true); } catch (IOException) { }
            }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(90)))
            throw new Exception("Pomiar ustawienia kopii po edycji przekroczył czas.");
        if (failure is not null)
            throw new Exception("Kopie po edycji: " + failure.Message, failure);
        Console.WriteLine(mode switch
        {
            1 => "OK: model ustawienia „Zachowuj kopie po edycji” bez okien",
            2 => "OK: kontrolka ustawienia „Zachowuj kopie po edycji” w oknie Ustawień",
            _ => "OK: ustawienie „Zachowuj kopie po edycji”: domyślna wartość, zapis, anulowanie i dostępność"
        });
    }

    // ---------------------------------------------------------------- model

    /// <summary>
    /// Nowe pole nie moze zmieniac zachowania istniejacych instalacji: swiezy
    /// <see cref="AppSettings"/> ORAZ stary zapis bez tej wlasnosci musza dac
    /// WYLACZONE zachowywanie kopii. Pomiar idzie przez plik na dysku i
    /// produkcyjny <see cref="ConfigurationStore.LoadOrCreate"/>, bo tylko on
    /// pokazuje, co zobaczy prawdziwy uzytkownik po aktualizacji.
    /// </summary>
    private static void TestDomyslnieWylaczoneRowniezPrzyStarymZapisie(string root)
    {
        Check(!new AppSettings().KeepAudioEditBackups,
            "Świeże ustawienia zachowują kopie po edycji, choć domyślnie nie powinny.");

        var (state, store) = Prepare(root, "domyslnie");
        store.Save(state);
        var path = StorePath(store);
        var json = JsonNode.Parse(File.ReadAllText(path))!.AsObject();

        // Zdejmujemy wlasnosc tak, jak wygladal zapis PRZED ta zmiana.
        var settings = FindSettingsObject(json)
            ?? throw new Exception("Zapis stanu nie zawiera obiektu ustawień ogólnych.");
        var usuniete = settings.Remove(NameOfSetting());
        Check(usuniete, "Zapis stanu nie zawiera własności kopii po edycji — nie ma czego migrować.");
        File.WriteAllText(path, json.ToJsonString());

        var wczytane = store.LoadOrCreate();
        Check(!wczytane.Settings.KeepAudioEditBackups,
            "Stary zapis bez tej własności włączył zachowywanie kopii po edycji.");
    }

    /// <summary>Obie wartosci musza przezyc zapis na dysk i ponowny odczyt.</summary>
    private static void TestZapisIOdczytObuWartosci(string root)
    {
        foreach (var wybor in new[] { true, false })
        {
            var (state, store) = Prepare(root, "zapis-" + wybor);
            state.Settings.KeepAudioEditBackups = wybor;
            store.Save(state);
            var wczytane = store.LoadOrCreate();
            Check(wczytane.Settings.KeepAudioEditBackups == wybor,
                $"Zapis {wybor} nie wrócił z dysku: odczytano {wczytane.Settings.KeepAudioEditBackups}.");

            // Drugi obieg: ponowny zapis odczytanego stanu nie moze gubic wyboru.
            store.Save(wczytane);
            Check(store.LoadOrCreate().Settings.KeepAudioEditBackups == wybor,
                $"Powtórny zapis zgubił wybór {wybor}.");
        }
    }

    /// <summary>
    /// Okno Ustawien pracuje na migawce <see cref="ConfigurationStore.CloneState"/>.
    /// Migawka kopiuje pola planem budowanym z typu, wiec pominiecie nowej
    /// wlasnosci objawiloby sie wlasnie tutaj: uzytkownik widzialby wylaczone
    /// pole, mimo ze ustawienie jest wlaczone.
    /// </summary>
    private static void TestCloneStatePrzenosiWybor(string root)
    {
        var (state, store) = Prepare(root, "clone");
        state.Settings.KeepAudioEditBackups = true;
        var kopia = store.CloneState(state);
        Check(kopia.Settings.KeepAudioEditBackups,
            "Migawka stanu zgubiła włączone zachowywanie kopii po edycji.");

        kopia.Settings.KeepAudioEditBackups = false;
        Check(state.Settings.KeepAudioEditBackups,
            "Migawka nie jest odłączona: zmiana w kopii ruszyła oryginał.");
    }

    /// <summary>
    /// Ustawienie ma byc do znalezienia w palecie polecen i ma prowadzic do
    /// wlasnego pola, a nie do pierwszej zakladki Ustawien.
    /// </summary>
    private static void TestUstawienieJestWyszukiwalne()
    {
        Check(CommandCatalog.GetAllCommandIds().Contains(CommandIds.SettingsKeepAudioEditBackups),
            "Polecenia ustawienia kopii po edycji nie ma w katalogu.");
        var nazwa = CommandCatalog.GetDisplayName(CommandIds.SettingsKeepAudioEditBackups);
        Check(nazwa.Contains(Etykieta, StringComparison.OrdinalIgnoreCase),
            $"Nazwa polecenia „{nazwa}” nie zawiera etykiety „{Etykieta}”.");
        Check(!string.Equals(nazwa, CommandIds.SettingsKeepAudioEditBackups, StringComparison.Ordinal),
            "Polecenie nie ma własnej nazwy — paleta pokazałaby identyfikator.");

        var router = typeof(CommandRouter)
            .GetMethod("TryGetSettingsTarget", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
            ?? throw new Exception("CommandRouter nie ma prywatnej reguły celu ustawień.");
        object?[] argumenty = [CommandIds.SettingsKeepAudioEditBackups, null];
        Check((bool)router.Invoke(null, argumenty)!,
            "Polecenie ustawienia kopii po edycji nie prowadzi do żadnego pola Ustawień.");
        Check((SettingsTarget)argumenty[1]! == SettingsTarget.KeepAudioEditBackups,
            $"Polecenie prowadzi do {argumenty[1]} zamiast do własnego pola.");
    }

    /// <summary>
    /// Komunikat po dopisaniu nie moze obiecywac kopii, ktorej nie ma. Pusta
    /// sciezka kopii w wyniku backendu oznacza kopie USUNIETA. Niepusta sciezka
    /// przy WYLACZONEJ opcji to sytuacja „nie udalo sie usunac” — i trzeba to
    /// powiedziec wprost, a nie milczec ani udawac, ze kopia byla chciana.
    /// </summary>
    private static void TestKomunikatNieObiecujeKopiiKtorejNieMa()
    {
        var opis = typeof(AudioClipAppendWindow).GetMethod(
            "DescribeBackupOutcome",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
            ?? throw new Exception("Okno dopisywania nie ma osobnej reguły opisu losu kopii.");
        string Opisz(bool keep, string? backup) => (string)opis.Invoke(null, [keep, backup])!;

        foreach (var pusta in new[] { "", "   ", null })
        {
            var tekst = Opisz(false, pusta);
            Check(!ObiecujeKopie(tekst),
                $"Bez kopii komunikat nadal obiecuje jej zachowanie: „{tekst}”.");
            Check(tekst.Contains("usunięt", StringComparison.OrdinalIgnoreCase),
                $"Komunikat nie mówi, że kopia została usunięta: „{tekst}”.");
        }

        var zachowana = Opisz(true, @"C:\nagrania\audycja.mp3.amc-backup");
        Check(ObiecujeKopie(zachowana),
            $"Przy włączonej opcji i realnej kopii komunikat jej nie potwierdza: „{zachowana}”.");

        // Opcja wylaczona, ale kopia zostala: uczciwie, ze plik jest na dysku.
        var mimoWszystko = Opisz(false, @"C:\nagrania\audycja.mp3.amc-backup");
        Check(mimoWszystko.Contains("zachowano", StringComparison.OrdinalIgnoreCase)
              && mimoWszystko.Contains("nie usunięto", StringComparison.OrdinalIgnoreCase),
            $"Komunikat nie tłumaczy, dlaczego kopia mimo wyłączonej opcji została: „{mimoWszystko}”.");
        Check(!string.Equals(mimoWszystko, zachowana, StringComparison.Ordinal),
            "Komunikat nie rozróżnia kopii chcianej od kopii, której nie udało się usunąć.");
    }

    private static bool ObiecujeKopie(string tekst) =>
        tekst.Contains("Zachowano kopię", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Okno dopisywania musi PRZEKAZAC ustawienie do backendu, a nie tylko o nim
    /// opowiadac. Pomiar idzie po zrodle, bo sam przebieg dopisywania wymaga
    /// plikow audio i nalezy do testow fragmentow audio.
    /// </summary>
    private static void TestDopisywanieRzeczywiscieNiesieUstawienie()
    {
        var konstruktor = typeof(AudioClipAppendWindow).GetConstructors().Single();
        var parametry = konstruktor.GetParameters();
        var ostatni = parametry[^1];
        Check(ostatni.ParameterType == typeof(bool) && ostatni.IsOptional,
            "Okno dopisywania nie przyjmuje opcjonalnego wyboru zachowania kopii.");
        Check(ostatni.DefaultValue is false,
            "Domyślne wywołanie okna dopisywania zachowywałoby kopię bez zgody użytkownika.");

        var zrodlo = string.Join("\n", File.ReadAllLines(FindSource("AudioClipAppendWindow.xaml.cs"))
            .Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal)));
        Check(zrodlo.Contains("keepBackup: _keepBackup", StringComparison.Ordinal),
            "Okno dopisywania nie przekazuje wyboru do AppendAsync nazwanym argumentem keepBackup.");
        Check(!zrodlo.Contains("Zachowano kopię poprzedniej wersji.\";", StringComparison.Ordinal),
            "Okno dopisywania nadal obiecuje kopię bez sprawdzenia wyniku backendu.");
    }

    // -------------------------------------------------------------- kontrolka

    /// <summary>
    /// Kontrolka musi POKAZYWAC zapisany stan, a nie zawsze to samo. Okno jest
    /// tu konstruowane, ale NIE pokazywane: wystarcza, bo wczytanie kontrolek
    /// dzieje sie w konstruktorze.
    /// </summary>
    private static void TestAppendExplainsSelectedBackupPolicy(string root)
    {
        foreach(var keep in new[]{false,true})
        {
            var window=new AudioClipAppendWindow(Path.Combine(root,"source.wav"),"Źródło próby",TimeSpan.Zero,TimeSpan.FromSeconds(1),keepBackup:keep);
            try
            {
                var help=AutomationProperties.GetHelpText((TextBox)window.FindName("TargetPathBox"));
                var rules=Walk(window).OfType<TextBox>().Single(x=>AutomationProperties.GetName(x)=="Zasady dopisywania i kopii zapasowej").Text;
                foreach(var text in new[]{help,rules})
                {
                    Check(text.Contains(keep?"pozostanie":"usunięta po sprawdzeniu",StringComparison.OrdinalIgnoreCase),
                        "Opis przed dopisaniem obiecuje niewłaściwy los kopii (keep="+keep+"): "+text);
                    Check(keep || !text.Contains("Kopia poprzedniego pliku pozostanie obok niego",StringComparison.OrdinalIgnoreCase),
                        "Pozostała bezwarunkowa obietnica kopii przy wyłączonej opcji");
                }
            }
            finally{window.Close();}
        }
    }

    private static void TestKontrolkaOdtwarzaWartosc(string root)
    {
        foreach (var wybor in new[] { true, false })
        {
            var (state, store) = Prepare(root, "kontrolka-" + wybor);
            state.Settings.KeepAudioEditBackups = wybor;
            var window = new SettingsWindow(state, store);
            try
            {
                var box = Checkbox(window);
                Check(box.IsChecked == wybor,
                    $"Kontrolka pokazuje {box.IsChecked} zamiast zapisanego {wybor}.");
            }
            finally { window.Close(); }
        }
    }

    /// <summary>
    /// Czytnik ekranu musi uslyszec dokladna etykiete oraz zakres dzialania:
    /// czego ustawienie dotyczy, czego NIE dotyczy i ze nie sprzata starych kopii.
    /// </summary>
    private static void TestKontrolkaJestDostepnaIOpisujeZakres(string root)
    {
        var (state, store) = Prepare(root, "dostepnosc");
        var window = new SettingsWindow(state, store);
        try
        {
            var box = Checkbox(window);
            Check(string.Equals(box.Content?.ToString(), Etykieta, StringComparison.Ordinal),
                $"Etykieta kontrolki to „{box.Content}”, a nie dokładnie „{Etykieta}”.");
            Check(string.Equals(AutomationProperties.GetName(box), Etykieta, StringComparison.Ordinal),
                $"Nazwa dla czytnika to „{AutomationProperties.GetName(box)}”, a nie „{Etykieta}”.");

            var pomoc = AutomationProperties.GetHelpText(box);
            Check(!string.IsNullOrWhiteSpace(pomoc), "Kontrolka nie ma opisu pomocniczego.");
            foreach (var (fragment, czego) in new[]
            {
                ("dopisania", "dopisywania fragmentu do istniejącego pliku"),
                ("usunięcia", "usuwania fragmentu z oryginału"),
                ("nowego pliku", "braku wpływu na zapis do nowego pliku"),
                ("wcześniej", "braku sprzątania kopii utworzonych wcześniej"),
                ("starcie", "braku sprzątania przy starcie programu"),
                ("błędzie", "zachowania kopii przy błędzie")
            })
            {
                Check(pomoc.Contains(fragment, StringComparison.OrdinalIgnoreCase),
                    $"Opis pomocniczy nie mówi o {czego} (brak „{fragment}”).");
            }

            Check(box.IsTabStop, "Kontrolki nie da się osiągnąć tabulatorem.");
            Check(!box.IsChecked!.Value, "Okno pokazuje włączone zachowywanie kopii dla świeżych ustawień.");
        }
        finally { window.Close(); }
    }

    /// <summary>
    /// Kolejnosc tabulacji ma byc sensowna: nowe pole stoi w zakladce Ogólne
    /// wsrod pozostalych opcji globalnych, PO pamieci pozycji i PRZED dlugoscia
    /// przeskoku — a nie na koncu okna ani w obcej zakladce.
    /// </summary>
    private static void TestKolejnoscTabulacji(string root)
    {
        var (state, store) = Prepare(root, "kolejnosc");
        var window = new SettingsWindow(state, store);
        try
        {
            var zakladka = (TabItem)window.FindName("GeneralTab")
                ?? throw new Exception("Okno Ustawień nie ma zakładki Ogólne.");
            // Editable combo boxes can delegate tab focus to their template's
            // TextBox. Compare logical placement, not the container's IsTabStop.
            // The checkbox's own IsTabStop is asserted separately; NVDA checks
            // the real keyboard transition during the live acceptance run.
            var kolejnosc = Walk(zakladka).OfType<Control>().ToList();
            var nowe = kolejnosc.IndexOf(Checkbox(window));
            Check(nowe >= 0, "Nowe pole nie leży w zakładce Ogólne.");

            var pamiec = kolejnosc.IndexOf((Control)window.FindName("RememberLocalPlaybackPositionsCheck"));
            var przeskok = kolejnosc.IndexOf((Control)window.FindName("CustomSeekSecondsCombo"));
            Check(pamiec >= 0 && przeskok >= 0, "Nie znaleziono sąsiadujących pól zakładki Ogólne.");
            Check(pamiec < nowe && nowe < przeskok,
                $"Pole stoi w kolejności {nowe}, poza grupą opcji globalnych ({pamiec}…{przeskok}).");
        }
        finally { window.Close(); }
    }

    // ------------------------------------------------- prawdziwy Zapisz/Anuluj

    /// <summary>Prawdziwy przycisk Zapisz musi przeniesc wybor do stanu.</summary>
    private static void TestZapisPrzenosiWybor(string root)
    {
        var (state, store) = Prepare(root, "zapisz");
        var result = WithSettings(state, store, save: true, body: window =>
        {
            var box = Checkbox(window);
            Check(box.IsChecked == false, "Test zaczyna od stanu, którego nie zamierzał zmienić.");
            box.IsChecked = true;
        });

        var zapisane = result ?? throw new Exception("Zapisz nie zwrócił stanu ustawień.");
        Check(zapisane.Settings.KeepAudioEditBackups,
            "Przycisk Zapisz nie przeniósł włączonego zachowywania kopii.");

        // Trwalosc i powrot do okna: wybor widoczny po ponownym otwarciu.
        store.Save(zapisane);
        var wczytane = store.LoadOrCreate();
        Check(wczytane.Settings.KeepAudioEditBackups, "Zapis na dysk zgubił wybór.");
        WithSettings(wczytane, store, save: false, body: window =>
            Check(Checkbox(window).IsChecked == true,
                "Po ponownym otwarciu okno nie pokazuje zapisanego wyboru."));

        // Droga powrotna: wylaczenie musi dzialac tak samo.
        var wylaczone = WithSettings(wczytane, store, save: true, body: window =>
            Checkbox(window).IsChecked = false);
        Check(wylaczone is not null && !wylaczone.Settings.KeepAudioEditBackups,
            "Przycisk Zapisz nie przeniósł wyłączenia zachowywania kopii.");
    }

    /// <summary>Anuluj nie moze ruszyc oryginalnego stanu ani pliku na dysku.</summary>
    private static void TestAnulowanieNieZmieniaOryginalu(string root)
    {
        var (state, store) = Prepare(root, "anuluj");
        store.Save(state);

        var result = WithSettings(state, store, save: false, body: window =>
            Checkbox(window).IsChecked = true);

        Check(result is null, "Anuluj zwrócił stan do zapisania.");
        Check(!state.Settings.KeepAudioEditBackups,
            "Anulowanie mimo wszystko włączyło zachowywanie kopii w stanie okna wywołującego.");
        Check(!store.LoadOrCreate().Settings.KeepAudioEditBackups,
            "Anulowanie zapisało zmianę na dysk.");
    }

    // ------------------------------------------------------------- narzedzia

    private static CheckBox Checkbox(SettingsWindow window) =>
        (CheckBox)window.FindName("KeepAudioEditBackupsCheck")
        ?? throw new Exception("Okno Ustawień nie ma pola „" + Etykieta + "”.");

    /// <summary>Świeży, izolowany stan: brak kont, sieci i cudzych danych.</summary>
    private static (PersistedState State, ConfigurationStore Store) Prepare(string root, string name)
    {
        var folder = Path.Combine(root, name);
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "state.json");
        var store = new ConfigurationStore(path);
        _storePaths[store] = path;
        var state = new PersistedState();
        state.Podcasts.DownloadsFolder = Path.Combine(folder, "podcasts");
        state.Radio.RecordingsFolder = Path.Combine(folder, "recordings");
        return (state, store);
    }

    /// <summary>
    /// Otwiera RZECZYWISTE okno Ustawień, wykonuje próbę i kończy prawdziwym
    /// przyciskiem Zapisz albo Anuluj. Zwraca stan przekazany do zapisania
    /// (null po Anuluj).
    /// </summary>
    private static PersistedState? WithSettings(
        PersistedState state,
        ConfigurationStore store,
        bool save,
        Action<SettingsWindow> body)
    {
        var window = new SettingsWindow(state, store);
        Exception? failure = null;
        window.ContentRendered += (_, _) => window.Dispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                body(window);
                var label = save ? "_Zapisz" : "_Anuluj";
                var button = Walk(window).OfType<Button>()
                    .Single(candidate => candidate.Content?.ToString() == label);
                ClickButton(button);
            }
            catch (Exception exception)
            {
                failure = exception;
                try { window.DialogResult = false; } catch (InvalidOperationException) { window.Close(); }
            }
        }), DispatcherPriority.ApplicationIdle);

        var accepted = window.ShowDialog();
        if (failure is not null) throw failure;
        if (save && accepted != true) throw new Exception("Przycisk Zapisz nie zatwierdził ustawień.");
        if (!save && accepted == true) throw new Exception("Przycisk Anuluj zatwierdził ustawienia.");
        return window.ResultState;
    }

    // RaiseEvent pomija Button.OnClick i wbudowane IsCancel. Wywołujemy prawdziwe kliknięcie WPF.
    private static void ClickButton(Button button) => typeof(Button)
        .GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
        .Invoke(button, null);

    private static string StorePath(ConfigurationStore store) =>
        _storePaths.TryGetValue(store, out var path)
            ? path
            : throw new Exception("Nie udało się ustalić pliku stanu magazynu konfiguracji.");

    /// <summary>
    /// Klucz własności w zapisie odczytujemy z RZECZYWISTEJ polityki nazw tego
    /// magazynu, bo wpisany na sztywno „keepAudioEditBackups” cicho przestałby
    /// cokolwiek mierzyć po zmianie zasad nazewnictwa JSON.
    /// </summary>
    private static string NameOfSetting() =>
        JsonNamingPolicy.CamelCase.ConvertName(nameof(AppSettings.KeepAudioEditBackups));

    /// <summary>Obiekt ustawień ogólnych w zapisie, niezależnie od nazwy klucza.</summary>
    private static JsonObject? FindSettingsObject(JsonObject json)
    {
        var key = NameOfSetting();
        foreach (var (_, value) in json)
        {
            if (value is not JsonObject nested) continue;
            if (nested.ContainsKey(key)) return nested;
            var found = FindSettingsObject(nested);
            if (found is not null) return found;
        }
        return null;
    }

    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }

    private static string FindSource(string name)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var src = Path.Combine(directory.FullName, "src");
            if (Directory.Exists(src))
            {
                var found = Directory.GetFiles(src, name, SearchOption.AllDirectories);
                if (found.Length > 0) return found[0];
            }
            directory = directory.Parent;
        }
        throw new Exception($"Nie znaleziono pliku źródłowego {name}.");
    }

    private static IEnumerable<DependencyObject> Walk(DependencyObject root)
    {
        yield return root;
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
            foreach (var descendant in Walk(child)) yield return descendant;
    }
}
