using System;
using System.Windows;
using AccessibleMediaController.Core.Sonos;

namespace AccessibleMediaController.Windows.Services;

/// <summary>
/// JEDEN aplikacyjny WLASCICIEL logowania Sonos: <see cref="SonosLoginClient"/>,
/// <see cref="SonosAccountCoordinator"/> i <see cref="SonosDpapiCredentialStore"/>.
///
/// Granice, swiadome i sprawdzane testami:
///   * LENIWE utworzenie. Ogolny start AMC, RebuildCore i cudze testy NIE czytaja
///     konta i NIE wysylaja zadnego zapytania HTTP - zasoby powstaja dopiero przy
///     JAWNYM otwarciu konta Sonos,
///   * <see cref="SonosAccountCoordinator.RestoreOnce"/> woluje sie DOKLADNIE raz,
///     PRZED pierwszym pokazaniem okna. Kolejne otwarcie dostaje TEN SAM
///     koordynator, bez powtornego odczytu magazynu,
///   * zamkniecie okna NIE konczy zycia wlasciciela. Zwalnia je tylko jawne
///     <see cref="Dispose"/> przy faktycznym zamknieciu AMC; ANULOWANE zamykanie
///     (np. ochrona nagrywania) do Dispose nie dochodzi,
///   * broker jest STALA konfiguracja integracji: zero pol w UI, zero
///     client id/secret, zero adresu z ustawien uzytkownika,
///   * zaden token, scope ani sciezka nie trafia do AppSettings, state.json
///     ani do logu - magazyn DPAPI ma wlasny plik.
/// </summary>
internal sealed class SonosAccountOwner : IDisposable
{
    /// <summary>
    /// DOMYSLNY zaufany broker AMC. ROOT origin - z niego
    /// <see cref="SonosLoginBrokerConfiguration"/> wyprowadza login/start,
    /// login/result i login/refresh. NIE wpisujemy tu sciezki zwrotnej
    /// (/sonos/callback): ona nalezy do brokera i Sonos, nie do klienta.
    /// </summary>
    internal const string DefaultBrokerOrigin = "https://hermes.tail6caad7.ts.net/";

    /// <summary>
    /// JAWNY identyfikator klienta tej integracji Sonos. To ta sama publiczna
    /// wartosc, ktora idzie w adresie autoryzacji i w naglowku X-Sonos-Api-Key -
    /// NIE jest sekretem i nie jest tokenem. Sekret zostaje w brokerze i do
    /// Control API nie jest potrzebny.
    ///
    /// Jest STALA KONFIGURACJI programu, a nie polem w interfejsie: tester ani
    /// uzytkownik nie ma wpisywac zadnych kluczy, zeby zobaczyc swoje glosniki.
    /// </summary>
    internal const string IntegrationApiKey = "b051a8f0-499c-4deb-9f66-843a752c36e4";

    private SonosControlApiClient? _controlApi;
    private SonosControlApiDeviceApi? _deviceApi;
    private SonosControlApiGroupApi? _groupApi;

    private readonly object _gate = new();

    private SonosLoginClient? _client;
    private SonosAccountCoordinator? _coordinator;
    private bool _disposed;

    /// <summary>
    /// TESTOWY punkt podstawienia magazynu. Produkcyjnie null, czyli istniejacy
    /// <see cref="SonosDpapiCredentialStore"/> na jego wlasnej domyslnej sciezce.
    /// </summary>
    internal Func<SonosLoginBrokerConfiguration, ISonosCredentialStore>? StoreFactory { get; set; }

    /// <summary>
    /// TESTOWY punkt podstawienia transportu. Produkcyjnie null, czyli prawdziwy
    /// <see cref="SonosLoginClient"/> na zaufanym brokerze. Testy podaja tu
    /// atrape, wiec zaden pomiar nie puka do publicznego serwera.
    /// </summary>
    internal Func<SonosLoginBrokerConfiguration, ISonosLoginGateway>? GatewayFactory { get; set; }

    /// <summary>Ile RAZY powstal koordynator. Drugie otwarcie nie ma prawa tego podniesc.</summary>
    internal int CoordinatorCreations { get; private set; }

    /// <summary>Ile razy zawolano odtworzenie z magazynu. Kwit dla testu "RestoreRead1 przy 2 otwarciach".</summary>
    internal int RestoreCalls { get; private set; }

    internal bool HasCoordinator
    {
        get
        {
            lock (_gate)
            {
                return _coordinator is not null;
            }
        }
    }

    /// <summary>
    /// BEZPIECZNA migawka konta albo <c>null</c>, gdy konta jeszcze NIE
    /// zainicjowano. Czyta TYLKO gotowy koordynator: sam odczyt nie budzi konta,
    /// nie rusza magazynu i nie idzie do sieci. <c>null</c> znaczy NIEZNANE, a
    /// NIE odlaczone - inaczej zaplecze bez konta udawaloby utrate konta.
    /// </summary>
    internal SonosAccountSnapshot? AccountSnapshot
    {
        get
        {
            lock (_gate)
            {
                return _coordinator?.Snapshot;
            }
        }
    }

    /// <summary>
    /// Zwraca WSPOLNY koordynator, tworzac go przy pierwszym wywolaniu i
    /// odtwarzajac konto z magazynu JEDEN raz - zawsze przed oddaniem go oknu.
    /// </summary>
    internal SonosAccountCoordinator EnsureCoordinator()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_coordinator is not null)
            {
                return _coordinator;
            }

            if (!SonosLoginBrokerConfiguration.TryCreate(DefaultBrokerOrigin, out var broker) || broker is null)
            {
                // Stala konfiguracja integracji nie moze byc niepoprawna. Gdyby
                // byla, lepiej nie udawac konta niz milczeco zmienic adres.
                throw new InvalidOperationException("Wbudowany adres serwera logowania Sonos jest nieprawidłowy.");
            }

            ISonosLoginGateway gateway;
            if (GatewayFactory is { } gatewayFactory)
            {
                gateway = gatewayFactory(broker);
            }
            else
            {
                _client = new SonosLoginClient(broker);
                gateway = new SonosLoginClientGateway(_client);
            }

            var store = StoreFactory is { } storeFactory
                ? storeFactory(broker)
                : new SonosDpapiCredentialStore(broker, SonosDpapiCredentialStore.DefaultFilePath());

            var coordinator = new SonosAccountCoordinator(gateway, store, broker.Origin.AbsoluteUri);
            CoordinatorCreations++;

            // PRZED pierwszym pokazaniem okna: okno czyta gotowa migawke i samo
            // nie dotyka magazynu.
            coordinator.RestoreOnce();
            RestoreCalls++;

            _coordinator = coordinator;
            return coordinator;
        }
    }

    /// <summary>
    /// WSPOLNY klient Control API tej integracji, tworzony LENIWIE przy
    /// pierwszym odczycie urzadzen i zyjacy tak dlugo jak wlasciciel - nie jak
    /// okno. Klucz integracji jest wbudowany, wiec zadne okno go nie dostaje i
    /// nikt go nie wpisuje.
    ///
    /// Klient jest TYLKO DO ODCZYTU: umie wylacznie GET domow i grup.
    /// </summary>
    internal SonosControlApiClient EnsureControlApiClient()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_controlApi is not null)
            {
                return _controlApi;
            }

            var configuration = ControlApiConfigurationFactory is { } factory
                ? factory()
                : SonosControlApiConfiguration.CreateDefault(IntegrationApiKey);

            _controlApi = new SonosControlApiClient(configuration);
            _deviceApi = new SonosControlApiDeviceApi(_controlApi);
            _groupApi = new SonosControlApiGroupApi(_controlApi);
            ControlApiCreations++;
            return _controlApi;
        }
    }

    /// <summary>
    /// TESTOWY punkt podstawienia konfiguracji Control API - produkcyjnie null,
    /// czyli stale wbudowane ustawienia. Testy kieruja go na atrape transportu,
    /// zeby nie bylo ani sieci, ani prawdziwego konta.
    /// </summary>
    internal Func<SonosControlApiConfiguration>? ControlApiConfigurationFactory { get; set; }

    /// <summary>Ile klientow Control API powstalo. Leniwosc znaczy: najwyzej jeden.</summary>
    internal int ControlApiCreations { get; private set; }

    /// <summary>
    /// ODCZYT domow przez wspolny koordynator i wspolny klient. Oddawane oknu
    /// jako zwykly callback, wiec okno nie widzi ani koordynatora, ani tokenow.
    /// </summary>
    internal Task<SonosHouseholdsReadResult> ReadHouseholdsAsync(CancellationToken cancellationToken) =>
        EnsureCoordinator().ReadHouseholdsAsync(EnsureDeviceApi(), cancellationToken);

    /// <summary>ODCZYT grup i glosnikow wybranego domu. Tez tylko GET.</summary>
    internal Task<SonosGroupsReadResult> ReadGroupsAsync(string householdId, CancellationToken cancellationToken) =>
        EnsureCoordinator().ReadGroupsAsync(EnsureDeviceApi(), householdId, cancellationToken);

    /// <summary>
    /// F2: ODCZYT ULUBIONYCH wybranego domu. CIENKIE przekazanie do tego SAMEGO
    /// koordynatora i tego SAMEGO klienta Control API, co domy i grupy - zaden
    /// drugi wlasciciel, zaden drugi HttpClient, zadna wlasna polityka biletu.
    ///
    /// LENIWOSC: pierwsze dotkniecie konta i klienta dzieje sie DOPIERO tutaj,
    /// czyli przy jawnym poleceniu uzytkownika. Start programu i obce sesje nie
    /// wolaja tej metody, wiec konta nie budza.
    ///
    /// Tylko GET: klient ulubionych nie ma zadnej operacji zapisu.
    /// </summary>
    internal Task<SonosFavoritesReadResult> ReadFavoritesAsync(
        string? householdId, CancellationToken cancellationToken) =>
        EnsureCoordinator().ReadFavoritesAsync(EnsureFavoritesApi(), householdId, cancellationToken);

    /// <summary>
    /// WASKI interfejs ULUBIONYCH nad tym SAMYM klientem Control API. Klient
    /// sam implementuje <see cref="ISonosFavoritesApi"/> (F1a), wiec nie ma tu
    /// ani nowego adaptera, ani nowego transportu.
    /// </summary>
    internal ISonosFavoritesApi EnsureFavoritesApi() => EnsureControlApiClient();

    /// <summary>
    /// WASKI interfejs GRUP nad tym SAMYM klientem i tym SAMYM koordynatorem.
    /// Leniwy jak reszta: powstaje razem z klientem Control API.
    /// </summary>
    internal ISonosGroupApi EnsureGroupApi()
    {
        EnsureControlApiClient();
        lock (_gate)
        {
            return _groupApi ?? throw new InvalidOperationException("Klient Control API Sonos nie został utworzony.");
        }
    }

    /// <summary>ODCZYT stanu odtwarzania GRUPY. Poswiadczenia zostaja tutaj.</summary>
    internal Task<SonosGroupReadResult<SonosGroupPlaybackStatus>> ReadGroupPlaybackAsync(
        string? groupId,
        CancellationToken cancellationToken) =>
        EnsureCoordinator().ReadGroupPlaybackAsync(EnsureGroupApi(), groupId, cancellationToken);

    /// <summary>ODCZYT metadanych GRUPY (tytul, wykonawca, zrodlo, radio).</summary>
    internal Task<SonosGroupReadResult<SonosGroupMetadata>> ReadGroupMetadataAsync(
        string? groupId,
        CancellationToken cancellationToken) =>
        EnsureCoordinator().ReadGroupMetadataAsync(EnsureGroupApi(), groupId, cancellationToken);

    /// <summary>ODCZYT glosnosci i wyciszenia GRUPY.</summary>
    internal Task<SonosGroupReadResult<SonosGroupVolume>> ReadGroupVolumeAsync(
        string? groupId,
        CancellationToken cancellationToken) =>
        EnsureCoordinator().ReadGroupVolumeAsync(EnsureGroupApi(), groupId, cancellationToken);

    /// <summary>POLECENIE grupy bez parametrow. Guardy transportu bez zmian.</summary>
    internal Task<SonosGroupCommandResult> SendGroupCommandAsync(
        string? groupId,
        SonosGroupCommand command,
        CancellationToken cancellationToken) =>
        EnsureCoordinator().SendGroupCommandAsync(EnsureGroupApi(), groupId, command, cancellationToken);

    internal Task<SonosGroupCommandResult> SeekRelativeAsync(
        string? groupId,
        int deltaMillis,
        string? itemId,
        CancellationToken cancellationToken) =>
        EnsureCoordinator().SeekRelativeAsync(EnsureGroupApi(), groupId, deltaMillis, itemId, cancellationToken);

    internal Task<SonosGroupCommandResult> SetGroupVolumeAsync(
        string? groupId,
        int volume,
        CancellationToken cancellationToken) =>
        EnsureCoordinator().SetGroupVolumeAsync(EnsureGroupApi(), groupId, volume, cancellationToken);

    internal Task<SonosGroupCommandResult> SetGroupMuteAsync(
        string? groupId,
        bool muted,
        CancellationToken cancellationToken) =>
        EnsureCoordinator().SetGroupMuteAsync(EnsureGroupApi(), groupId, muted, cancellationToken);

    /// <summary>
    /// WASKI interfejs odczytu nad odebranym klientem. Osobna metoda, zeby
    /// koordynator nie zalezal od typu klienta HTTP.
    /// </summary>
    internal ISonosDeviceApi EnsureDeviceApi()
    {
        EnsureControlApiClient();
        lock (_gate)
        {
            return _deviceApi ?? throw new InvalidOperationException("Klient Control API Sonos nie został utworzony.");
        }
    }

    /// <summary>
    /// FAKTYCZNE zakonczenie wlasciciela. Wolane tylko przy prawdziwym zamykaniu
    /// AMC - nie przy zamknieciu okna konta i nie przy anulowanym zamknieciu.
    /// </summary>
    public void Dispose()
    {
        SonosAccountCoordinator? coordinator;
        SonosLoginClient? client;
        SonosControlApiClient? controlApi;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            coordinator = _coordinator;
            client = _client;
            controlApi = _controlApi;
            _coordinator = null;
            _client = null;
            _controlApi = null;
            _deviceApi = null;
        }

        coordinator?.Dispose();
        client?.Dispose();
        controlApi?.Dispose();
    }
}

/// <summary>
/// PRODUKCYJNY caller okna konta Sonos: bierze wspolny koordynator od
/// <see cref="SonosAccountOwner"/>, buduje <see cref="SonosAccountWindow"/> z
/// wlascicielem okna i DOSTEPNYM potwierdzeniem wylogowania, a przy drugim
/// wywolaniu nie tworzy drugiego okna na tym samym wlascicielu.
///
/// Wydzielone z MainWindow, zeby dalo sie zmierzyc CALA droge
/// (wlasciciel -&gt; okno -&gt; potwierdzenie) bez pokazywania GUI i bez
/// budowania glownego okna.
/// </summary>
internal sealed class SonosAccountPresenter
{
    /// <summary>Tresc pytania o wylogowanie. Nieodwracalna operacja, wiec pytanie jest jawne.</summary>
    internal const string DisconnectQuestion =
        "Wylogować konto Sonos? Zapisane logowanie zostanie usunięte z tego komputera. "
        + "Aby wrócić do Sonos, zalogujesz się ponownie w przeglądarce.";

    internal const string DisconnectCaption = "Wylogowanie Sonos";

    private readonly SonosAccountOwner _owner;

    internal SonosAccountPresenter(SonosAccountOwner owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        _owner = owner;
    }

    /// <summary>Otwarte okno konta albo null. Jedno okno na jednego wlasciciela.</summary>
    internal SonosAccountWindow? OpenWindow { get; private set; }

    /// <summary>Ile okien konta powstalo. Guard nie ma prawa tego podniesc drugi raz.</summary>
    internal int WindowsCreated { get; private set; }

    /// <summary>
    /// TESTOWY punkt podstawienia POKAZANIA okna. Produkcyjnie null, czyli
    /// prawdziwe modalne ShowDialog. Testy podaja tu odpowiednik, ktory okna NIE
    /// pokazuje, wiec cala droga da sie zmierzyc bez ruszania pulpitu.
    /// </summary>
    internal Action<SonosAccountWindow>? PresentOverride { get; set; }

    /// <summary>
    /// TESTOWY punkt podstawienia POKAZANIA potwierdzenia. Produkcyjnie null,
    /// czyli istniejace <see cref="AccessibleDialog"/> z wlascicielem okna konta.
    /// </summary>
    internal Func<Window?, MessageBoxResult>? ConfirmOverride { get; set; }

    /// <summary>
    /// Otwiera konto Sonos. Zwraca true, gdy powstalo NOWE okno; false, gdy
    /// zadzialal guard i okno jest juz otwarte (bez drugiego Restore i Start).
    /// </summary>
    internal bool Show(Window? mainWindow)
    {
        if (OpenWindow is { } existing)
        {
            // GUARD: nie budujemy drugiego okna na tym samym wlascicielu.
            // Nie ma tu ani Restore, ani BeginLogin - tylko powrot do okna,
            // ktore uzytkownik juz ma.
            if (PresentOverride is null)
            {
                existing.Activate();
            }

            return false;
        }

        var coordinator = _owner.EnsureCoordinator();

        SonosAccountWindow? created = null;
        var window = new SonosAccountWindow(
            coordinator,
            confirmDisconnect: () => Confirm(created),
            showDevices: () => ShowDevices(created));
        created = window;
        if (mainWindow is not null)
        {
            window.Owner = mainWindow;
        }

        OpenWindow = window;
        WindowsCreated++;
        try
        {
            if (PresentOverride is { } present)
            {
                present(window);
            }
            else
            {
                window.ShowDialog();
            }
        }
        finally
        {
            OpenWindow = null;
        }

        return true;
    }

    /// <summary>Otwarte okno urzadzen albo null. Jedno na jednego wlasciciela.</summary>
    internal SonosDevicesWindow? OpenDevicesWindow { get; private set; }

    /// <summary>Ile okien urzadzen powstalo. Guard nie ma prawa tego podniesc drugi raz.</summary>
    internal int DevicesWindowsCreated { get; private set; }

    /// <summary>
    /// TESTOWY punkt podstawienia POKAZANIA okna urzadzen. Produkcyjnie null,
    /// czyli prawdziwe modalne ShowDialog na oknie konta.
    /// </summary>
    internal Action<SonosDevicesWindow>? PresentDevicesOverride { get; set; }

    /// <summary>
    /// LISTA URZADZEN: okno dostaje WYLACZNIE dwa waskie callbacki odczytu od
    /// wlasciciela. Nie widzi koordynatora, klienta ani tokenow, a klucz
    /// integracji zostaje w warstwie uslug.
    ///
    /// Zwraca true, gdy powstalo NOWE okno; false gdy zadzialal guard.
    /// </summary>
    internal bool ShowDevices(Window? accountWindow)
    {
        if (OpenDevicesWindow is { } existing)
        {
            if (PresentDevicesOverride is null)
            {
                existing.Activate();
            }

            return false;
        }

        var window = new SonosDevicesWindow(
            readHouseholds: _owner.ReadHouseholdsAsync,
            readGroups: _owner.ReadGroupsAsync);
        // WLASCICIEL OKNA: WPF przyjmuje Owner tylko dla okna, ktore JUZ zostalo
        // pokazane. Brak wlasciciela nie moze wywrocic drogi do listy, wiec
        // nieudane powiazanie jest pomijane - lista dziala dalej jako osobne okno.
        if (accountWindow is not null && accountWindow.IsVisible)
        {
            try
            {
                window.Owner = accountWindow;
            }
            catch (InvalidOperationException)
            {
            }
        }

        OpenDevicesWindow = window;
        DevicesWindowsCreated++;
        try
        {
            if (PresentDevicesOverride is { } present)
            {
                present(window);
            }
            else
            {
                // Pierwsze wczytanie startuje PRZED pokazaniem, ale jest
                // asynchroniczne: okno pojawia sie z instrukcja i komunikatem
                // "wczytywanie", a nie zamrozone na czas HTTP.
                _ = window.LoadAsync();
                window.ShowDialog();
            }
        }
        finally
        {
            OpenDevicesWindow = null;
        }

        return true;
    }

    /// <summary>
    /// Potwierdzenie wylogowania z wlascicielem WLASCIWEGO okna konta - nie
    /// nieaktywnego glownego okna pod modalem. Domyslna odpowiedz to Nie, wiec
    /// Escape i Alt+F4 nie usuwaja konta.
    /// </summary>
    private bool Confirm(Window? accountWindow)
    {
        var answer = ConfirmOverride is { } confirm
            ? confirm(accountWindow)
            : AccessibleDialog.Show(
                accountWindow,
                DisconnectQuestion,
                DisconnectCaption,
                MessageBoxButton.YesNo,
                MessageBoxImage.Question,
                MessageBoxResult.No);
        return answer == MessageBoxResult.Yes;
    }
}
