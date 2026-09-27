using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using AccessibleMediaController.Core.Sonos;
using AccessibleMediaController.Windows;

namespace SonosAccountWindowHarness;

/// <summary>
/// JAWNIE NAZWANE dane probne. Nazwy mowia wprost, ze to atrapa: zaden token ani
/// adres tutaj nie jest prawdziwym sekretem i nic nie idzie do sieci.
/// </summary>
internal static class Fakes
{
    /// <summary>Adres brokera ma tylko przejsc walidacje konfiguracji - nikt go nie odwiedza.</summary>
    internal const string BrokerOrigin = "https://broker-probny.invalid/";

    /// <summary>
    /// Adres autoryzacji zgodny z ISTNIEJACA polityka Sonos. Nie wymyslamy jej tu
    /// na nowo - <see cref="SonosAuthorizeUrlPolicy"/> zada dokladnie tego hosta i
    /// tej sciezki, wiec adres probny musi je miec, zeby pomiar dotyczyl
    /// prawdziwej regulý.
    /// </summary>
    internal static readonly Uri TrustedAuthorizeUri =
        new("https://api.sonos.com/login/v3/oauth?client_id=PROBNY&state=PROBNY");

    internal static SonosLoginSession Session() =>
        Reflected.NewSession("sesja-probna", "weryfikator-probny", TrustedAuthorizeUri,
            DateTimeOffset.UtcNow.AddMinutes(10));

    internal static SonosTokens Tokens(string? refreshToken = "refresh-probny") =>
        new("dostep-probny", SonosTokens.BearerTokenType, 3600, refreshToken, "playback-control-all");

    internal static SonosStoredCredentials Stored() =>
        new(NormalizedBrokerOrigin(), Tokens(), DateTimeOffset.UtcNow.AddMinutes(-5));

    /// <summary>
    /// Koordynator zapisuje rekord pod ZNORMALIZOWANYM origin, wiec atrapa
    /// magazynu musi oddawac dokladnie taka postac - inaczej odtworzenie
    /// wygladaloby na niezgodnosc brokera.
    /// </summary>
    internal static string NormalizedBrokerOrigin() =>
        SonosLoginBrokerConfiguration.TryCreate(BrokerOrigin, out var configuration) && configuration is not null
            ? configuration.Origin.AbsoluteUri
            : BrokerOrigin;
}

/// <summary>
/// SonosLoginSession ma konstruktor wewnetrzny Core, a harness jest osobna
/// assembly na LINKOWANYCH zrodlach - typ jest wiec ten sam, ale dostep do
/// konstruktora zalezy od tego, jak zlinkowano. Zamiast dodawac
/// InternalsVisibleTo do PRODUKTU dla potrzeb pomiaru, budujemy sesje refleksja.
/// </summary>
internal static class Reflected
{
    internal static SonosLoginSession NewSession(
        string sessionId,
        string codeVerifier,
        Uri authorizeUri,
        DateTimeOffset expiresAtUtc)
    {
        var constructor = typeof(SonosLoginSession).GetConstructor(
            System.Reflection.BindingFlags.Instance
            | System.Reflection.BindingFlags.NonPublic
            | System.Reflection.BindingFlags.Public,
            binder: null,
            new[] { typeof(string), typeof(string), typeof(Uri), typeof(DateTimeOffset) },
            modifiers: null)
            ?? throw new InvalidOperationException("Nie znaleziono konstruktora sesji logowania Sonos.");

        return (SonosLoginSession)constructor.Invoke(
            new object[] { sessionId, codeVerifier, authorizeUri, expiresAtUtc });
    }
}

/// <summary>
/// SYNTETYCZNA bramka logowania: zero HTTP, zero gniazd. Liczy wywolania, zeby
/// pomiar mogl twierdzic "dokladnie jedno zapytanie" na podstawie licznika, a nie
/// wiary.
/// </summary>
internal sealed class FakeGateway : ISonosLoginGateway
{
    internal SonosLoginStartOutcome? StartResult { get; set; }

    internal SonosLoginResultOutcome? FetchResult { get; set; }

    internal SonosRefreshOutcome? RefreshResult { get; set; }

    internal Exception? StartThrows { get; set; }

    /// <summary>Bramka CZASOWA: pozwala zamknac okno w trakcie trwajacej operacji.</summary>
    internal Task? StartBarrier { get; set; }

    /// <summary>
    /// Bramka CZASOWA sprawdzenia: pozwala przeniesc fokus gdzie indziej, ZANIM
    /// wynik wroci i schowa przycisk. Bez niej nie da sie zmierzyc ochrony fokusu
    /// uzytkownika przy spoznionym zakonczeniu.
    /// </summary>
    internal Task? FetchBarrier { get; set; }

    internal int StartCalls { get; private set; }

    internal int FetchCalls { get; private set; }

    internal int RefreshCalls { get; private set; }

    internal int TotalCalls => StartCalls + FetchCalls + RefreshCalls;

    public async Task<SonosLoginStartOutcome> StartAsync(CancellationToken cancellationToken)
    {
        StartCalls++;
        if (StartBarrier is not null)
        {
            await StartBarrier.ConfigureAwait(false);
        }

        if (StartThrows is not null)
        {
            throw StartThrows;
        }

        return StartResult ?? Outcomes.StartFailure(SonosLoginStatus.BrokerUnreachable);
    }

    public async Task<SonosLoginResultOutcome> FetchResultAsync(
        SonosLoginSession session,
        CancellationToken cancellationToken)
    {
        FetchCalls++;
        if (FetchBarrier is not null)
        {
            await FetchBarrier.ConfigureAwait(false);
        }

        return FetchResult ?? Outcomes.FetchFailure(SonosLoginStatus.Pending);
    }

    public Task<SonosRefreshOutcome> RefreshAsync(string? refreshToken, CancellationToken cancellationToken)
    {
        RefreshCalls++;
        return Task.FromResult(RefreshResult ?? Outcomes.RefreshFailure(SonosRefreshStatus.BrokerUnreachable));
    }
}

/// <summary>
/// Fabryki wynikow Core sa wewnetrzne, wiec atrapy tworza je refleksja - bez
/// rozszerzania widocznosci produktu tylko dla pomiaru.
/// </summary>
internal static class Outcomes
{
    internal static SonosLoginStartOutcome StartOk(SonosLoginSession session) =>
        (SonosLoginStartOutcome)Invoke(typeof(SonosLoginStartOutcome), "Ok", session);

    internal static SonosLoginStartOutcome StartFailure(SonosLoginStatus status) =>
        (SonosLoginStartOutcome)Invoke(typeof(SonosLoginStartOutcome), "Failure", status);

    internal static SonosLoginResultOutcome FetchOk(SonosTokens tokens) =>
        (SonosLoginResultOutcome)Invoke(typeof(SonosLoginResultOutcome), "Ok", tokens);

    internal static SonosLoginResultOutcome FetchFailure(SonosLoginStatus status) =>
        (SonosLoginResultOutcome)Invoke(typeof(SonosLoginResultOutcome), "Failure", status);

    internal static SonosRefreshOutcome RefreshOk(SonosTokens tokens) =>
        (SonosRefreshOutcome)Invoke(typeof(SonosRefreshOutcome), "Ok", tokens);

    internal static SonosRefreshOutcome RefreshFailure(SonosRefreshStatus status) =>
        (SonosRefreshOutcome)Invoke(typeof(SonosRefreshOutcome), "Failure", status);

    private static object Invoke(Type type, string name, object argument)
    {
        var method = type.GetMethod(
            name,
            System.Reflection.BindingFlags.Static
            | System.Reflection.BindingFlags.NonPublic
            | System.Reflection.BindingFlags.Public,
            binder: null,
            new[] { argument.GetType() },
            modifiers: null)
            ?? throw new InvalidOperationException($"Nie znaleziono {type.Name}.{name}.");

        return method.Invoke(null, new[] { argument })
            ?? throw new InvalidOperationException($"{type.Name}.{name} nie zwrocilo wyniku.");
    }
}

/// <summary>
/// SYNTETYCZNY magazyn: tylko pamiec, zero DPAPI i zero plikow. Prawdziwy
/// magazyn produkcyjny NIE jest tu dotykany.
/// </summary>
internal sealed class FakeStore : ISonosCredentialStore
{
    private SonosStoredCredentials? held;

    internal SonosCredentialReadOutcome? ReadResult { get; set; }

    internal SonosCredentialWriteOutcome? WriteResult { get; set; }

    internal bool DeleteResult { get; set; } = true;

    internal int ReadCalls { get; private set; }

    internal int WriteCalls { get; private set; }

    internal int DeleteCalls { get; private set; }

    internal void Preload(SonosStoredCredentials credentials) => held = credentials;

    public SonosCredentialReadOutcome Read()
    {
        ReadCalls++;
        if (ReadResult is not null)
        {
            return ReadResult;
        }

        return held is null
            ? Read(SonosCredentialReadStatus.Missing)
            : ReadOk(held);
    }

    public SonosCredentialWriteOutcome Write(SonosStoredCredentials credentials)
    {
        WriteCalls++;
        var outcome = WriteResult ?? SonosCredentialWriteOutcome.Ok();
        if (outcome.Status == SonosCredentialWriteStatus.Success)
        {
            held = credentials;
        }

        return outcome;
    }

    public bool Delete()
    {
        DeleteCalls++;
        if (DeleteResult)
        {
            held = null;
        }

        return DeleteResult;
    }

    private static SonosCredentialReadOutcome ReadOk(SonosStoredCredentials credentials) =>
        SonosCredentialReadOutcome.Ok(credentials);

    private static SonosCredentialReadOutcome Read(SonosCredentialReadStatus status) =>
        SonosCredentialReadOutcome.Failure(status);
}

/// <summary>Testowy otwieracz: NICZEGO nie otwiera, tylko zapisuje, co dostal.</summary>
internal sealed class FakeBrowser
{
    internal List<Uri> OpenedUris { get; } = new();

    internal bool Result { get; set; } = true;

    internal bool Open(Uri uri)
    {
        if (Result)
        {
            OpenedUris.Add(uri);
        }

        return Result;
    }
}

/// <summary>Wstrzykniete potwierdzenie wylogowania - bez zadnego nowego dialogu.</summary>
internal sealed class FakeConfirm
{
    internal bool Answer { get; set; }

    internal int Calls { get; private set; }

    internal bool Ask()
    {
        Calls++;
        return Answer;
    }
}

/// <summary>Testowy odbiornik ogloszen: liczy je, zamiast mowic do czytnika.</summary>
internal sealed class FakeSink
{
    internal List<string> Messages { get; } = new();

    internal void Accept(string message) => Messages.Add(message);
}

/// <summary>
/// Jedno ustawienie pomiaru. Okno jest KONSTRUOWANE, ale nigdy nie pokazywane:
/// brak Show, ShowDialog, Activate i EnsureHandle.
/// </summary>
internal sealed class Scenario : IDisposable
{
    private Scenario(SonosAccountCoordinator coordinator, FakeGateway gateway, FakeStore store)
    {
        Coordinator = coordinator;
        Gateway = gateway;
        Store = store;
        Browser = new FakeBrowser();
        Confirm = new FakeConfirm();
        Sink = new FakeSink();
        Window = new SonosAccountWindow(coordinator, Browser.Open, Confirm.Ask, Sink.Accept);
    }

    internal SonosAccountCoordinator Coordinator { get; }

    internal FakeGateway Gateway { get; }

    internal FakeStore Store { get; }

    internal FakeBrowser Browser { get; }

    internal FakeConfirm Confirm { get; }

    internal FakeSink Sink { get; }

    internal SonosAccountWindow Window { get; }

    /// <summary>Brak zapisanego konta. RestoreOnce wolane PRZED konstrukcja okna.</summary>
    internal static Scenario Fresh() => Build(preloaded: false);

    /// <summary>Konto odtworzone z magazynu PRZED otwarciem okna - jak u przyszlego wolajacego.</summary>
    internal static Scenario Restored() => Build(preloaded: true);

    private static Scenario Build(bool preloaded)
    {
        var gateway = new FakeGateway();
        var store = new FakeStore();
        if (preloaded)
        {
            store.Preload(Fakes.Stored());
        }

        var coordinator = new SonosAccountCoordinator(gateway, store, Fakes.BrokerOrigin);
        coordinator.RestoreOnce();
        return new Scenario(coordinator, gateway, store);
    }

    public void Dispose()
    {
        // Okno konczy WLASNA prace; wlascicielem koordynatora jest to ustawienie,
        // wiec ono - a nie okno - go zwalnia.
        Window.ShutdownOwnWork();
        Coordinator.Dispose();
    }
}

/// <summary>
/// TRYB --show-fixture: PRZYGOTOWANY, tu NIE uruchamiany. Pokazuje RZECZYWISTE
/// okno na jawnie nazwanych danych probnych, zeby rodzic mogl je pozniej zmierzyc
/// zywym NVDA po swiezej bramce nagrywania.
///
/// Czego ten tryb NIE robi: nie startuje App produkcji ani MainWindow, nie czyta
/// AppSettings, nie dotyka prawdziwego magazynu (DPAPI), nie otwiera przegladarki,
/// nie robi IPC, nie sprawdza aktualizacji, nie rusza audio i nie wchodzi do sieci.
/// Otwieracz przegladarki jest ZASTAPIONY zapisem do kwitu.
///
/// Kazde odtworzenie dostaje OSOBNY katalog wynikow; nic nie kasuje poprzednich.
/// Watchdog zamyka WYLACZNIE wlasne okno po ograniczonym czasie.
/// </summary>
internal static class FixtureMode
{
    private const string FixtureTitle = "AMC PROBA A11Y - Konto Sonos (dane probne)";

    internal static int Run(string[] args)
    {
        var resultsRoot = ArgumentValue(args, "--results-root")
            ?? "/home/michal/projekty/amc_pomoc/sonos-account-window1";
        var seconds = int.TryParse(ArgumentValue(args, "--seconds"), out var parsed) ? parsed : 240;
        var stamp = DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var runDirectory = Path.Combine(resultsRoot, "okno-" + stamp + "-pid" + Environment.ProcessId);
        Directory.CreateDirectory(runDirectory);

        var gateway = new FakeGateway { StartResult = Outcomes.StartOk(Fakes.Session()) };
        var store = new FakeStore();
        if (args.Contains("--with-account"))
        {
            store.Preload(Fakes.Stored());
        }

        var coordinator = new SonosAccountCoordinator(gateway, store, Fakes.BrokerOrigin);
        coordinator.RestoreOnce();

        var openedUris = new List<string>();
        var announcements = new List<string>();
        var application = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };

        // Otwieracz TESTOWY: zapisuje adres do kwitu, nie uruchamia przegladarki.
        //
        // APARATURA: w trybie WIDOCZNYM sink jest NULL, wiec komunikaty ida
        // PRAWDZIWYM announcerem produkcji (UIA notification). Testowy sink
        // polykal je i przez to podglad mowy nie mogl niczego pokazac. Licznik i
        // tresc bierzemy z OBSERWACJI zmian Text, nie ze sterowania mowa.
        // Tryb domyslny (bez GUI) nadal uzywa atrapy odbiornika.
        var window = new SonosAccountWindow(
            coordinator,
            uri =>
            {
                openedUris.Add(uri.AbsoluteUri);
                return true;
            },
            () => true,
            announcementSink: null);

        var status = (System.Windows.Controls.TextBlock)window.FindName("OperationStatusText");
        System.ComponentModel.DependencyPropertyDescriptor
            .FromProperty(System.Windows.Controls.TextBlock.TextProperty, typeof(System.Windows.Controls.TextBlock))
            .AddValueChanged(status, (_, _) => announcements.Add(status.Text));

        window.Title = FixtureTitle;

        // Bez wlasciciela okno MUSI byc widoczne na pasku zadan, inaczej trudno je
        // znalezc czytnikiem i przelaczaniem okien.
        window.ShowInTaskbar = true;

        window.SourceInitialized += (_, _) =>
        {
            var handle = new WindowInteropHelper(window).Handle;
            WriteReceipt(
                Path.Combine(runDirectory, "gotowe.txt"),
                new[]
                {
                    "tytul=" + FixtureTitle,
                    "pid=" + Environment.ProcessId.ToString(CultureInfo.InvariantCulture),
                    "hwnd=0x" + handle.ToString("X"),
                    "showInTaskbar=" + window.ShowInTaskbar.ToString(),
                    "konto=" + (store.ReadCalls > 0 ? coordinator.Snapshot.State.ToString() : "brak"),
                    "gotowe=" + DateTimeOffset.Now.ToString("O", CultureInfo.InvariantCulture)
                });
        };

        var watchdog = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(Math.Clamp(seconds, 15, 1800))
        };
        watchdog.Tick += (_, _) =>
        {
            watchdog.Stop();
            // Zamyka WYLACZNIE swoje okno. Zadnych obcych okien, zadnego kill.
            window.Close();
        };

        window.Closed += (_, _) =>
        {
            watchdog.Stop();
            WriteReceipt(
                Path.Combine(runDirectory, "po-operacjach.txt"),
                new[]
                {
                    "start=" + gateway.StartCalls.ToString(CultureInfo.InvariantCulture),
                    "fetch=" + gateway.FetchCalls.ToString(CultureInfo.InvariantCulture),
                    "refresh=" + gateway.RefreshCalls.ToString(CultureInfo.InvariantCulture),
                    "zapis=" + store.WriteCalls.ToString(CultureInfo.InvariantCulture),
                    "usuniecie=" + store.DeleteCalls.ToString(CultureInfo.InvariantCulture),
                    "otwarteAdresy=" + openedUris.Count.ToString(CultureInfo.InvariantCulture),
                    "ogloszen=" + announcements.Count.ToString(CultureInfo.InvariantCulture),
                    "stanKoncowy=" + coordinator.Snapshot.State,
                    "koniec=" + DateTimeOffset.Now.ToString("O", CultureInfo.InvariantCulture)
                });
            WriteReceipt(Path.Combine(runDirectory, "komunikaty.txt"), announcements);
            WriteReceipt(Path.Combine(runDirectory, "otwarte-adresy.txt"), openedUris);
        };

        watchdog.Start();
        application.MainWindow = window;
        window.Show();
        var exit = application.Run();

        // WLASCICIELEM koordynatora jest ten tryb, nie okno.
        coordinator.Dispose();
        Console.WriteLine("Kwity: " + runDirectory);
        return exit;
    }

    private static string? ArgumentValue(string[] args, string name)
    {
        for (var index = 0; index < args.Length - 1; index++)
        {
            if (string.Equals(args[index], name, StringComparison.Ordinal))
            {
                return args[index + 1];
            }
        }

        return null;
    }

    /// <summary>Kwity sa NIEMUTOWALNE: nowy plik w nowym katalogu, bez nadpisywania.</summary>
    private static void WriteReceipt(string path, IEnumerable<string> lines)
    {
        if (File.Exists(path))
        {
            return;
        }

        File.WriteAllLines(path, lines);
    }

    private static bool Contains(this string[] args, string value) =>
        Array.IndexOf(args, value) >= 0;
}
