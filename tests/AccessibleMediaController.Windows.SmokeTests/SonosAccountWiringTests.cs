using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using AccessibleMediaController.Core.Commands;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Sonos;
using AccessibleMediaController.Windows;
using AccessibleMediaController.Windows.Services;

/// <summary>
/// PODLACZENIE okna konta Sonos do AMC, mierzone BEZ pokazywania GUI.
///
/// Co dokladnie jest mierzone PRAWDZIWYM kodem produkcyjnym:
///   * <see cref="SonosAccountOwner"/> jest LENIWY: samo utworzenie nie czyta
///     magazynu i nie wola bramki,
///   * DWA otwarcia = JEDEN koordynator i DOKLADNIE JEDEN odczyt magazynu
///     (RestoreRead 1 przy 2 otwarciach),
///   * zamkniecie okna NIE wola Dispose ani Delete; zestaw poswiadczen i stan
///     WriteFailure przezywaja miedzy otwarciami,
///   * FAKTYCZNE zakonczenie wlasciciela zwalnia koordynator,
///   * <see cref="SonosAccountPresenter"/> nie tworzy DRUGIEGO okna na tym samym
///     wlascicielu i guard nie robi drugiego Restore,
///   * potwierdzenie wylogowania jest zbudowane przez ISTNIEJACE
///     <see cref="AccessibleDialog"/> z ownerem OKNA KONTA, odpowiedz domyslna Nie;
///     brak potwierdzenia NIE usuwa konta,
///   * zaden token nie trafia do stanu aplikacji, ale wybor domu i grupy Sonos
///     PRZEZYWA zapis i odczyt ustawien (prawdziwy ConfigurationStore, katalog
///     tymczasowy testu),
///   * menu Plik ma pozycje konta Sonos bez skrotu i bez zmiany Ctrl+F5.
///
/// SCISLA BRAMKA PULPITU: zero Show, ShowDialog, Activate i EnsureHandle. Okno
/// konta jest KONSTRUOWANE na watku STA, pokazanie zastapione testowym
/// odpowiednikiem, a potwierdzenie budowane przez
/// <see cref="AccessibleDialog.CreateForMeasurement"/>, wiec nic nie zabiera
/// fokusu i nic nie mowi do czytnika ekranu.
///
/// Czego ten pomiar NIE dowodzi: nie tworzy MainWindow, wiec nie jest pomiarem
/// zywego modala ani pierwszego fokusu w POKAZANYM oknie. To osobna czynnosc
/// wlasciciela pulpitu.
/// </summary>
internal static class SonosAccountWiringTests
{
    private const BindingFlags Internal = BindingFlags.Instance | BindingFlags.NonPublic;

    internal static void Run()
    {
        var checks = 0;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                checks += MeasureLazyOwner();
                checks += MeasureTwoOpeningsShareCoordinator();
                checks += MeasureGuardAgainstSecondWindow();
                checks += MeasureDisconnectConfirmation();
                checks += MeasureMenuAndShortcuts();
                checks += MeasureSettingsKeepSelectionWithoutCredentials();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
            finally
            {
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        thread.Join();
        if (failure is not null) throw failure;

        Console.WriteLine(
            "OK: konto Sonos podłączone - jeden właściciel, RestoreOnce przed pierwszym oknem, "
            + "zamknięcie bez Dispose i Delete, guard drugiego okna, dostępne potwierdzenie wylogowania "
            + $"({checks} sprawdzeń, bez pokazywania GUI)");
    }

    // ================= 1. leniwy wlasciciel =================

    private static int MeasureLazyOwner()
    {
        var store = new LiczacyMagazyn();
        var gateway = new NieuzywanaBramka();
        using var owner = new SonosAccountOwner { StoreFactory = _ => store, GatewayFactory = _ => gateway };

        if (owner.HasCoordinator || owner.CoordinatorCreations != 0 || owner.RestoreCalls != 0)
        {
            throw new Exception("Samo utworzenie właściciela zbudowało koordynator.");
        }
        if (store.Reads != 0 || store.Writes != 0 || store.Deletes != 0)
        {
            throw new Exception("Samo utworzenie właściciela dotknęło magazynu poświadczeń.");
        }
        if (gateway.Calls != 0)
        {
            throw new Exception("Samo utworzenie właściciela wysłało żądanie do brokera.");
        }

        // Domyslny broker: ROOT origin, z ktorego wyprowadzaja sie sciezki.
        if (!SonosLoginBrokerConfiguration.TryCreate(SonosAccountOwner.DefaultBrokerOrigin, out var broker)
            || broker is null)
        {
            throw new Exception("Domyślny adres brokera Sonos nie przechodzi własnej konfiguracji.");
        }
        if (broker.Origin.AbsoluteUri != "https://hermes.tail6caad7.ts.net/"
            || broker.StartUri.AbsoluteUri != "https://hermes.tail6caad7.ts.net/login/start"
            || broker.ResultUri.AbsoluteUri != "https://hermes.tail6caad7.ts.net/login/result"
            || broker.RefreshUri.AbsoluteUri != "https://hermes.tail6caad7.ts.net/login/refresh")
        {
            throw new Exception("Domyślny broker nie wyprowadza oczekiwanych ścieżek z roota.");
        }
        return 3;
    }

    // ================= 2. dwa otwarcia, jeden koordynator =================

    private static int MeasureTwoOpeningsShareCoordinator()
    {
        var store = new LiczacyMagazyn { WriteResult = SonosCredentialWriteStatus.WriteFailure };
        var owner = new SonosAccountOwner { StoreFactory = _ => store, GatewayFactory = _ => new NieuzywanaBramka() };
        var presenter = new SonosAccountPresenter(owner);

        SonosAccountWindow? first = null;
        presenter.PresentOverride = window => first = window;
        if (!presenter.Show(null)) throw new Exception("Pierwsze otwarcie nie zbudowało okna konta.");
        var coordinator = owner.EnsureCoordinator();

        if (owner.RestoreCalls != 1 || store.Reads != 1)
        {
            throw new Exception($"Odczyt magazynu wykonał się {store.Reads} razy zamiast raz.");
        }
        if (first is null) throw new Exception("Okno konta nie dotarło do testowego pokazania.");

        // Zamkniecie okna: bez Dispose koordynatora i bez Delete zapisu.
        first.Close();
        if (store.Deletes != 0) throw new Exception("Zamknięcie okna usunęło zapisane logowanie.");
        if (!ReferenceEquals(owner.EnsureCoordinator(), coordinator))
        {
            throw new Exception("Po zamknięciu okna właściciel oddał INNY koordynator.");
        }

        // Koordynator ZYJE: udalo sie wolac operacje bez ObjectDisposedException.
        var snapshotAfterClose = coordinator.Snapshot;
        if (snapshotAfterClose is null) throw new Exception("Koordynator nie żyje po zamknięciu okna.");

        // Drugie otwarcie: TEN SAM koordynator, bez powtornego Restore.
        SonosAccountWindow? second = null;
        presenter.PresentOverride = window => second = window;
        if (!presenter.Show(null)) throw new Exception("Drugie otwarcie nie zbudowało okna.");
        if (owner.CoordinatorCreations != 1 || owner.RestoreCalls != 1 || store.Reads != 1)
        {
            throw new Exception(
                $"Drugie otwarcie odtworzyło konto ponownie (koordynatory {owner.CoordinatorCreations}, "
                + $"restore {owner.RestoreCalls}, odczyty {store.Reads}).");
        }
        if (second is null || ReferenceEquals(second, first))
        {
            throw new Exception("Drugie otwarcie nie dało nowego okna na tym samym koordynatorze.");
        }
        var secondCoordinator = (SonosAccountCoordinator)typeof(SonosAccountWindow)
            .GetField("coordinator", Internal)!.GetValue(second)!;
        if (!ReferenceEquals(secondCoordinator, coordinator))
        {
            throw new Exception("Drugie okno dostało inny koordynator.");
        }
        second.Close();

        // FAKTYCZNE zakonczenie wlasciciela zwalnia koordynator.
        owner.Dispose();
        var released = false;
        try { _ = coordinator.RestoreOnce(); }
        catch (ObjectDisposedException) { released = true; }
        if (!released) throw new Exception("Zakończenie właściciela nie zwolniło koordynatora.");
        return 6;
    }

    // ================= 3. guard drugiego okna =================

    private static int MeasureGuardAgainstSecondWindow()
    {
        var store = new LiczacyMagazyn();
        using var owner = new SonosAccountOwner { StoreFactory = _ => store, GatewayFactory = _ => new NieuzywanaBramka() };
        var presenter = new SonosAccountPresenter(owner);

        var reentered = false;
        presenter.PresentOverride = _ =>
        {
            // Drugie polecenie PODCZAS otwartego okna - dokladnie ten przypadek,
            // ktory nie moze zbudowac drugiego okna ani powtorzyc Restore.
            reentered = presenter.Show(null);
        };
        presenter.Show(null);

        if (reentered) throw new Exception("Drugie polecenie zbudowało drugie okno na tym samym właścicielu.");
        if (presenter.WindowsCreated != 1) throw new Exception("Guard zbudował dodatkowe okno konta.");
        if (owner.RestoreCalls != 1 || store.Reads != 1) throw new Exception("Guard wykonał dodatkowy odczyt magazynu.");
        return 2;
    }

    // ================= 4. potwierdzenie wylogowania =================

    private static int MeasureDisconnectConfirmation()
    {
        // ZBUDOWANE okno potwierdzenia, bez Show: mierzymy jego domyslna
        // odpowiedz i tresc. Zywy odbior modala robi wlasciciel pulpitu.
        var dialog = AccessibleDialog.CreateForMeasurement(
            SonosAccountPresenter.DisconnectQuestion,
            SonosAccountPresenter.DisconnectCaption,
            MessageBoxButton.YesNo,
            MessageBoxResult.No);
        if (AccessibleDialog.PeekResult(dialog) != MessageBoxResult.No)
        {
            throw new Exception("Domyślna odpowiedź potwierdzenia wylogowania nie jest Nie.");
        }
        if (!SonosAccountPresenter.DisconnectQuestion.Contains("Wylogować", StringComparison.Ordinal)
            || SonosAccountPresenter.DisconnectQuestion.Contains("Ctrl", StringComparison.OrdinalIgnoreCase))
        {
            throw new Exception("Treść pytania o wylogowanie nie jest tekstem użytkowym bez skrótu.");
        }

        // Owner potwierdzenia to OKNO KONTA, nie nieaktywne okno glowne.
        var store = new LiczacyMagazyn();
        using var owner = new SonosAccountOwner { StoreFactory = _ => store, GatewayFactory = _ => new NieuzywanaBramka() };
        var presenter = new SonosAccountPresenter(owner);
        Window? confirmOwner = null;
        var confirmCalls = 0;
        presenter.ConfirmOverride = ownerWindow =>
        {
            confirmCalls++;
            confirmOwner = ownerWindow;
            // Escape, Alt+F4 i Nie daja dokladnie to.
            return MessageBoxResult.No;
        };
        SonosAccountWindow? accountWindow = null;
        presenter.PresentOverride = window =>
        {
            accountWindow = window;
            var confirm = (Func<bool>)typeof(SonosAccountWindow)
                .GetField("confirmDisconnect", Internal)!.GetValue(window)!;
            if (confirm()) throw new Exception("Odpowiedź Nie została zrozumiana jako zgoda na wylogowanie.");
        };
        presenter.Show(null);

        if (confirmCalls != 1) throw new Exception("Potwierdzenie nie zostało zapytane dokładnie raz.");
        if (!ReferenceEquals(confirmOwner, accountWindow))
        {
            throw new Exception("Potwierdzenie ma innego właściciela niż okno konta Sonos.");
        }
        if (store.Deletes != 0) throw new Exception("Brak potwierdzenia mimo to usunął zapisane logowanie.");
        return 4;
    }

    // ================= 5. menu, skroty i brak zapisu w stanie =================

    private static int MeasureMenuAndShortcuts()
    {
        var xaml = File.ReadAllText(LocateRepositoryFile("src/AccessibleMediaController.Windows/MainWindow.xaml"));
        var menuIndex = xaml.IndexOf("x:Name=\"ManageSonosConnectionMenuItem\"", StringComparison.Ordinal);
        if (menuIndex < 0) throw new Exception("Menu Plik nie ma pozycji konta Sonos.");
        // Tylko TEN element, do jego wlasnego zamkniecia - inaczej pomiar
        // czytalby nastepna pozycje menu i jej skrot.
        var menuEnd = xaml.IndexOf("/>", menuIndex, StringComparison.Ordinal);
        if (menuEnd < 0) throw new Exception("Pozycja konta Sonos nie jest zamknietym elementem.");
        var menuBlock = xaml.Substring(menuIndex, menuEnd - menuIndex);
        if (!menuBlock.Contains("Click=\"ManageSonosConnection_Click\"", StringComparison.Ordinal))
        {
            throw new Exception("Pozycja konta Sonos nie ma podłączonej obsługi.");
        }
        if (menuBlock.Contains("InputGestureText", StringComparison.Ordinal))
        {
            throw new Exception("Pozycja konta Sonos ogłasza skrót, którego nie ma.");
        }
        if (menuBlock.Contains("Header=\"Konto _Sonos", StringComparison.Ordinal))
        {
            throw new Exception("Pozycja konta Sonos zajmuje literę dostępu, choć miała jej nie brać.");
        }

        // PRAWDZIWY caller: MainWindow realizuje kontraktowa metode routera.
        var method = typeof(MainWindow).GetMethod(nameof(IApplicationActions.ShowSonosAccountManager))
            ?? throw new Exception("MainWindow nie realizuje otwarcia konta Sonos z routera.");
        if (!typeof(IApplicationActions).IsAssignableFrom(typeof(MainWindow)))
        {
            throw new Exception("MainWindow nie jest już adresatem poleceń routera.");
        }
        var handler = typeof(MainWindow).GetMethod("ManageSonosConnection_Click", Internal)
            ?? throw new Exception("Obsługa pozycji menu konta Sonos nie istnieje.");
        _ = method;
        _ = handler;

        // Ctrl+F5 W SESJI SONOS otwiera WYBOR CELU STEROWANIA
        // (CommandIds.ChooseSonosTarget). Ten test wymagal tu jeszcze
        // ManageSonosConnection - kontraktu sprzed 4.1.6/4.1.7, gdy Ctrl+L
        // pokazywalo glosniki. Odkad Ctrl+L pokazuje MATERIAL, wybor celu ma
        // wlasne miejsce pod Ctrl+F5, a KONTO SONOS NIE GINIE: zostaje w menu
        // Plik i w palecie (sprawdzane nizej). Asercja jest dostosowana do
        // OBOWIAZUJACEGO kontraktu, nie usunieta.
        var source = File.ReadAllText(LocateRepositoryFile("src/AccessibleMediaController.Windows/MainWindow.xaml.cs"));
        // Kotwica na PELNYM warunku Ctrl+F5, nie na samym "&& key == Key.F5":
        // ten fragment wystepuje takze w sasiedniej obsludze F5 bez modyfikatora,
        // a okno 1200 znakow od zlej kotwicy nie siegalo galezi Sonosa.
        const string ctrlF5Condition = "Keyboard.Modifiers == ModifierKeys.Control && key == Key.F5";
        var ctrlF5 = source.IndexOf(ctrlF5Condition, StringComparison.Ordinal);
        if (ctrlF5 < 0) throw new Exception("Nie udało się odnaleźć obsługi Ctrl+F5.");
        // Blok konczy sie na zamykajacym "return true;" tej galezi - mierzymy
        // CALY rozdzial sesji dla Ctrl+F5, a nie staly wycinek znakow.
        var ctrlF5End = source.IndexOf("return true;", ctrlF5, StringComparison.Ordinal);
        if (ctrlF5End < 0) throw new Exception("Obsługa Ctrl+F5 nie ma zakończenia gałęzi.");
        var ctrlF5Block = source.Substring(ctrlF5, ctrlF5End - ctrlF5);
        if (!ctrlF5Block.Contains("ChooseSonosTarget", StringComparison.Ordinal))
        {
            throw new Exception("Ctrl+F5 w sesji Sonos nie kieruje do wyboru celu sterowania.");
        }
        if (ctrlF5Block.Contains("SonosSettingsWindow", StringComparison.Ordinal)
            || ctrlF5Block.Contains("SonosAccountSettings", StringComparison.Ordinal))
        {
            throw new Exception("Ctrl+F5 otwiera NOWY panel Sonosa zamiast istniejącego okna wyboru.");
        }

        // KONTO SONOS NADAL OSIAGALNE istniejaca droga: menu Plik / paleta.
        // Bez tego powyzsza zmiana mogloby przykryc zniknięcie dostepu do konta.
        if (!source.Contains("CommandIds.ManageSonosConnection", StringComparison.Ordinal))
        {
            throw new Exception("Konto Sonos zniknęło z poleceń okna głównego - "
                + "Ctrl+F5 wolno przenieść na wybór celu TYLKO przy zachowanym menu/palecie.");
        }
        return 4;
    }

    // ================= 6. ustawienia: WYBOR tak, POSWIADCZENIA nie =================

    // Zastepuje dawny zakaz samego slowa "Sonos" w AppSettings.cs. Ten zakaz byl
    // przestarzaly i mierzyl tekst zrodla, nie zachowanie: wybor domu i grupy MUSI
    // trafic do ustawien, inaczej AMC zapominalby go po restarcie. Zakaz
    // poswiadczen zostaje, ale sprawdzalny na tym, co naprawde jest zapisywane:
    // KSZTALCIE SonosSessionSettings i PRAWDZIWEJ serializacji przez
    // ConfigurationStore do wlasnego katalogu tymczasowego.
    //
    // Celowo mierzony jest tylko wezel Sonos: AppSettings przechowuje takze inne
    // konta (TIDAL, Spotify), wiec globalny zakaz slowa "token" w pliku stanu
    // banowalby cudze, poprawne pola.
    private static int MeasureSettingsKeepSelectionWithoutCredentials()
    {
        const string household = "Sonos_household.9000000001";
        const string group = "RINCON_00012345678001400:9";
        // WYBOR (tekstowe identyfikatory) plus WLASNE STRUMIENIE uzytkownika.
        // OwnStreams JEST w produkcie od 4.1.7 (wlasne adresy strumieni w sesji
        // Sonos), a ten test dopuszczal jeszcze tylko dwa pola - stad zastany
        // RED. Lista jest WYBOREM MATERIALU uzytkownika, nie poswiadczeniem, ale
        // nie jest tekstem, wiec ma osobna kontrole kształtu nizej. ZAKAZ
        // poswiadczen zostaje nienaruszony.
        string[] allowedTextProperties = ["SelectedGroupId", "SelectedHouseholdId"];
        string[] allowedProperties = [.. allowedTextProperties, "OwnStreams"];
        string[] allowedJsonKeys = ["selectedGroupId", "selectedHouseholdId", "ownStreams"];

        // 6a. KSZTALT typu: pola wyboru (tekstowe) i lista wlasnych strumieni.
        // Dopisanie tokenu, scope, URI brokera czy sciezki magazynu wpada tu
        // jako ASERCJA, nie jako blad kompilacji testu.
        var properties = typeof(SonosSessionSettings)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .ToArray();
        var unexpected = properties
            .Select(property => property.Name)
            .Where(name => !allowedProperties.Contains(name, StringComparer.Ordinal))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        if (unexpected.Length > 0)
        {
            throw new Exception(
                "Ustawienia sesji Sonos zyskały pole poza wyborem domu i grupy (poświadczenia należą "
                + "do osobnego magazynu właściciela konta): " + string.Join(", ", unexpected));
        }
        foreach (var expected in allowedTextProperties)
        {
            var property = properties.FirstOrDefault(candidate =>
                string.Equals(candidate.Name, expected, StringComparison.Ordinal))
                ?? throw new Exception("Ustawienia sesji Sonos przestały pamiętać " + expected + ".");
            if (property.PropertyType != typeof(string))
            {
                throw new Exception(
                    $"Pole {expected} przestało być identyfikatorem tekstowym (jest {property.PropertyType.Name}).");
            }
        }

        // Wlasne strumienie: NAZWA i ADRES, nic wiecej. Gdyby ktos dopisal tu
        // token albo naglowek autoryzacji, poswiadczenie weszloby do ustawien
        // boczna droga - i ta asercja to wylapie.
        var ownStreams = properties.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, "OwnStreams", StringComparison.Ordinal))
            ?? throw new Exception("Ustawienia sesji Sonos przestały pamiętać OwnStreams.");
        if (ownStreams.PropertyType != typeof(List<SonosOwnStreamSettings>))
        {
            throw new Exception(
                $"OwnStreams przestało być listą własnych strumieni (jest {ownStreams.PropertyType.Name}).");
        }
        string[] allowedStreamProperties = ["Id", "Name", "StreamUrl"];
        var unexpectedStream = typeof(SonosOwnStreamSettings)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(property => property.Name)
            .Where(name => !allowedStreamProperties.Contains(name, StringComparer.Ordinal))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        if (unexpectedStream.Length > 0)
        {
            throw new Exception(
                "Własny strumień Sonos zyskał pole poza nazwą i adresem (poświadczenia należą do "
                + "osobnego magazynu): " + string.Join(", ", unexpectedStream));
        }

        var folder = Path.Combine(Path.GetTempPath(), "amc-sonos-guard-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var statePath = Path.Combine(folder, "state.json");
            var state = ConfigurationStore.CreateDefaultState();
            state.Sonos.SelectedHouseholdId = household;
            state.Sonos.SelectedGroupId = group;
            NewSettingsStore(statePath).Save(state);

            var json = File.ReadAllText(statePath);
            using var document = JsonDocument.Parse(json);

            // 6b. Wybór NAPRAWDE idzie na dysk (zapis ustawien ma wlasna powloke,
            // wiec pominiecie Sonosa po cichu gubiloby dom i grupe).
            if (!document.RootElement.TryGetProperty("sonos", out var sonos))
            {
                throw new Exception("Zapis ustawień pominął węzeł sonos - wybór domu i grupy nie przeżyłby restartu.");
            }
            var sonosText = sonos.GetRawText();
            if (!sonosText.Contains(household, StringComparison.Ordinal)
                || !sonosText.Contains(group, StringComparison.Ordinal))
            {
                throw new Exception("Zapisane ustawienia nie pamiętają wybranego domu i grupy Sonos.");
            }

            // 6c. W wezle Sonos sa DOKLADNIE klucze wyboru i wlasnych
            // strumieni - nic wiecej.
            var extraKeys = sonos.EnumerateObject()
                .Select(property => property.Name)
                .Where(name => !allowedJsonKeys.Contains(name, StringComparer.OrdinalIgnoreCase))
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
            if (extraKeys.Length > 0)
            {
                throw new Exception(
                    "Serializowane ustawienia Sonos mają klucz poza wyborem domu i grupy: "
                    + string.Join(", ", extraKeys));
            }

            // 6d. Zaden inny wezel stanu nie zaczyna przechowywac danych konta Sonos
            // (np. sonosTokens, sonosCredentials obok wezla sonos).
            var strayRoots = document.RootElement.EnumerateObject()
                .Select(property => property.Name)
                .Where(name => name.Contains("sonos", StringComparison.OrdinalIgnoreCase)
                    && !name.Equals("sonos", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (strayRoots.Length > 0)
            {
                throw new Exception(
                    "Stan aplikacji zyskał osobny węzeł danych konta Sonos: " + string.Join(", ", strayRoots));
            }

            // 6e. PRAWDZIWY odczyt przywraca wybor - zakaz poswiadczen nie moze
            // byc osiagniety przez wyrzucenie wyboru.
            var reloaded = NewSettingsStore(statePath).LoadOrCreate();
            if (!string.Equals(reloaded.Sonos.SelectedHouseholdId, household, StringComparison.Ordinal)
                || !string.Equals(reloaded.Sonos.SelectedGroupId, group, StringComparison.Ordinal))
            {
                throw new Exception(
                    "Po ponownym odczycie ustawień wybór domu i grupy Sonos nie wrócił: "
                    + $"dom={reloaded.Sonos.SelectedHouseholdId ?? "(brak)"}, "
                    + $"grupa={reloaded.Sonos.SelectedGroupId ?? "(brak)"}.");
            }
            return 5;
        }
        finally
        {
            try { Directory.Delete(folder, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    private static ConfigurationStore NewSettingsStore(string statePath) =>
        new(statePath,
            Path.ChangeExtension(statePath, ".library.db"),
            Path.ChangeExtension(statePath, ".podcasts.db"));

    private static string LocateRepositoryFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }

        throw new Exception("Nie odnaleziono pliku źródłowego: " + relativePath);
    }

    // ================= atrapy =================

    /// <summary>Magazyn LICZACY operacje. Nic nie szyfruje i nie dotyka dysku.</summary>
    private sealed class LiczacyMagazyn : ISonosCredentialStore
    {
        internal int Reads;
        internal int Writes;
        internal int Deletes;
        internal SonosCredentialWriteStatus WriteResult = SonosCredentialWriteStatus.Success;

        public SonosCredentialReadOutcome Read()
        {
            Reads++;
            return SonosCredentialReadOutcome.Failure(SonosCredentialReadStatus.Missing);
        }

        public SonosCredentialWriteOutcome Write(SonosStoredCredentials credentials)
        {
            Writes++;
            return WriteResult == SonosCredentialWriteStatus.Success
                ? SonosCredentialWriteOutcome.Ok()
                : SonosCredentialWriteOutcome.Failure(WriteResult);
        }

        public bool Delete()
        {
            Deletes++;
            return true;
        }
    }

    /// <summary>
    /// Bramka, ktora ma NIE zostac zawolana. Kazde uzycie to blad pomiaru, nie
    /// zapytanie do publicznego serwera.
    /// </summary>
    private sealed class NieuzywanaBramka : ISonosLoginGateway
    {
        internal int Calls;

        public Task<SonosLoginStartOutcome> StartAsync(CancellationToken cancellationToken)
        {
            Calls++;
            throw new Exception("Pomiar podłączenia nie ma prawa rozpoczynać logowania.");
        }

        public Task<SonosLoginResultOutcome> FetchResultAsync(
            SonosLoginSession session,
            CancellationToken cancellationToken)
        {
            Calls++;
            throw new Exception("Pomiar podłączenia nie ma prawa odbierać wyniku logowania.");
        }

        public Task<SonosRefreshOutcome> RefreshAsync(string? refreshToken, CancellationToken cancellationToken)
        {
            Calls++;
            throw new Exception("Pomiar podłączenia nie ma prawa odnawiać dostępu.");
        }
    }
}
