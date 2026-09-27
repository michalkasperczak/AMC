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
    /// FAKTYCZNE zakonczenie wlasciciela. Wolane tylko przy prawdziwym zamykaniu
    /// AMC - nie przy zamknieciu okna konta i nie przy anulowanym zamknieciu.
    /// </summary>
    public void Dispose()
    {
        SonosAccountCoordinator? coordinator;
        SonosLoginClient? client;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            coordinator = _coordinator;
            client = _client;
            _coordinator = null;
            _client = null;
        }

        coordinator?.Dispose();
        client?.Dispose();
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
            confirmDisconnect: () => Confirm(created));
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
