using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using AccessibleMediaController.Core.Commands;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Core.Sonos;
using AccessibleMediaController.Windows;
using AccessibleMediaController.Windows.Services;

/// <summary>
/// REAKCJA istniejacej sesji Sonos na zmiane albo odlaczenie konta, mierzona na
/// PRAWDZIWYM MainWindow. Konto zmienia sie WYLACZNIE przez PUBLICZNE operacje
/// prawdziwego <see cref="SonosAccountCoordinator"/> nad syntetycznym magazynem i
/// syntetyczna bramka - zaden numer nie jest ustawiany recznie i zaden callback
/// nie jest wolany wprost.
///
/// Granica bezpieczenstwa pomiaru: wlasciciel konta z prawdziwego okna dostaje
/// syntetyczne StoreFactory/GatewayFactory, wiec nie ma DPAPI, nie ma HTTP i nic
/// nie otwiera przegladarki. Zaden POST do Sonosa nie ma prawa wyjsc.
/// </summary>
internal static class SonosSessionAccountUiTests
{
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    internal static void Run()
    {
        var checks = 0;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                checks += MeasureRealDisconnectDropsSessionState();
                checks += MeasureOrdinaryRefreshKeepsSelection();
                checks += MeasureLateReadAfterAccountSwapIsDiscarded();
                checks += MeasureFailedNewLoginKeepsWorkingAccount();
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
            "OK: sesja Sonos reaguje na rzeczywista zmiane konta - odlaczenie porzuca dane i wybor, "
            + $"zwykle odnowienie ich nie rusza ({checks} sprawdzeń, bez pokazywania GUI)");
    }

    // ===== ODLACZENIE prawdziwego, obserwowanego konta porzuca stan sesji =====

    private static int MeasureRealDisconnectDropsSessionState()
    {
        using var harness = Harness.Create();
        var window = harness.Window;

        // PRAWDZIWA droga uzytkownika do konta: wlasciciel okna, jego koordynator,
        // odtworzenie zapisu z SYNTETYCZNEGO magazynu.
        var coordinator = harness.AccountOwner.EnsureCoordinator();
        if (!coordinator.Snapshot.HasCredentials)
        {
            throw new Exception("Przygotowanie: odtworzone konto powinno mieć zestaw poświadczeń.");
        }
        var podlaczenie = coordinator.Snapshot.AccountBindingGeneration;
        if (podlaczenie != 0)
        {
            throw new Exception($"Przygotowanie: RestoreOnce nie ma prawa podnosić znacznika, jest {podlaczenie}.");
        }

        // Wejscie do sesji i wybor grupy PRZEZ prawdziwe polecenie.
        harness.ExecuteCommand(CommandIds.SessionSlot(8));
        harness.PumpUntil(() => window.SonosGroupRows.Count == 2, "wejście do sesji Sonos nie odczytało grup");
        harness.Pump(window.ActivateSonosGroupAsync("GRUPA-SALON"));
        if (window.SonosSelectedGroupId != "GRUPA-SALON" || window.SonosActiveGroup is null)
        {
            throw new Exception("Przygotowanie: wybór grupy Sonos nie został zapamiętany.");
        }
        if (SonosSession(window).Items.Count != 2)
        {
            throw new Exception("Przygotowanie: sesja Sonos nie ma odczytanych wierszy grup.");
        }
        var readsBefore = harness.Backend.PlaybackReads;
        if (readsBefore == 0)
        {
            throw new Exception("Przygotowanie: wybór grupy nie odczytał jej stanu.");
        }
        var householdReadsBefore = harness.Backend.HouseholdReads;

        // RZECZYWISTE odlaczenie konta PUBLICZNA operacja koordynatora.
        var disconnect = coordinator.Disconnect();
        if (!disconnect.Disconnected)
        {
            throw new Exception("Przygotowanie: syntetyczny magazyn nie potwierdził wylogowania.");
        }
        if (disconnect.Snapshot.AccountBindingGeneration <= podlaczenie)
        {
            throw new Exception("Przygotowanie: realne odłączenie nie zmieniło znacznika podłączenia.");
        }

        // GRANICE PRODUKCYJNE, ktore naprawde biegna po zmianie konta w trakcie
        // pracy sesji: tyknieciE licznika odtwarzacza i kolejne polecenie
        // uzytkownika. Nie wolamy zadnego callbacka testowego.
        harness.Pump(window.PollSonosGroupIfDueAsync(DateTime.UtcNow.AddHours(1)));
        harness.Pump(window.ExecuteSonosCommandForTests(CommandIds.PlayPause));
        harness.PumpQuietly(TimeSpan.FromMilliseconds(200));

        if (window.SonosSelectedGroupId is not null)
        {
            throw new Exception(
                "Po ODLACZENIU obserwowanego konta wybór grupy przeżył: " + window.SonosSelectedGroupId + ".");
        }
        if (window.SonosActiveGroup is not null)
        {
            throw new Exception("Po odłączeniu konta aktywna grupa Sonos nadal istnieje.");
        }
        if (SonosPlayback(window) is not null || SonosMetadata(window) is not null || SonosVolume(window) is not null)
        {
            throw new Exception("Po odłączeniu konta stare dane odtwarzacza Sonos zostały bieżące.");
        }
        if (window.SonosGroupRows.Count != 0)
        {
            throw new Exception("Po odłączeniu konta lista grup starego konta została na widoku.");
        }
        if (harness.Backend.HouseholdReads != householdReadsBefore)
        {
            throw new Exception("Zmiana konta sama poszła po domy zamiast poczekać na jawne wejście.");
        }
        if (harness.Backend.PlaybackReads != readsBefore)
        {
            throw new Exception("Po odłączeniu konta licznik odczytał stan STAREJ grupy.");
        }
        if (harness.Backend.Commands.Count != 0)
        {
            throw new Exception(
                "Po zmianie konta poszło polecenie do Sonosa ze starym celem: "
                + string.Join(", ", harness.Backend.CommandGroupIds) + ".");
        }
        if (window.SonosAccountChangeDropsForTests == 0)
        {
            throw new Exception("Sesja w ogóle nie zauważyła rzeczywistej zmiany konta.");
        }
        return 9;
    }

    // ===== ZWYKLA rotacja poswiadczen ZACHOWUJE wybor, liste i kontekst =====

    private static int MeasureOrdinaryRefreshKeepsSelection()
    {
        using var harness = Harness.Create();
        var window = harness.Window;
        var coordinator = harness.AccountOwner.EnsureCoordinator();

        harness.ExecuteCommand(CommandIds.SessionSlot(8));
        harness.PumpUntil(() => window.SonosGroupRows.Count == 2, "wejście do sesji Sonos nie odczytało grup");
        harness.Pump(window.ActivateSonosGroupAsync("GRUPA-KUCHNIA"));
        if (window.SonosSelectedGroupId != "GRUPA-KUCHNIA")
        {
            throw new Exception("Przygotowanie: wybór grupy Sonos nie został zapamiętany.");
        }
        var przed = coordinator.Snapshot;
        var householdReadsBefore = harness.Backend.HouseholdReads;
        var dropsBefore = window.SonosAccountChangeDropsForTests;

        // RZECZYWISTA, UDANA rotacja zestawu PUBLICZNA operacja koordynatora.
        harness.Gateway.PlanOdnowienia = SonosRefreshOutcome.Ok(
            new SonosTokens("ACCESS-NOWY", "Bearer", 3600, "RT-NOWY", "playback-control-all"));
        var refresh = coordinator.RefreshAsync(CancellationToken.None).GetAwaiter().GetResult();
        if (!refresh.Renewed)
        {
            throw new Exception("Przygotowanie: syntetyczna rotacja poświadczeń się nie udała.");
        }
        if (refresh.Snapshot.CredentialGeneration == przed.CredentialGeneration)
        {
            throw new Exception("Przygotowanie: rotacja nie podniosła generacji zestawu poświadczeń.");
        }
        if (refresh.Snapshot.AccountBindingGeneration != przed.AccountBindingGeneration)
        {
            throw new Exception("Przygotowanie: zwykła rotacja zmieniła znacznik podłączenia konta.");
        }

        // GRANICE produkcyjne po rotacji: odczyt, tyknięcie licznika i polecenie.
        harness.Pump(window.ReadSonosGroupStateAsync());
        harness.Pump(window.PollSonosGroupIfDueAsync(DateTime.UtcNow.AddHours(1)));
        harness.Pump(window.ExecuteSonosCommandForTests(CommandIds.PlayPause));
        harness.PumpQuietly(TimeSpan.FromMilliseconds(200));

        if (window.SonosSelectedGroupId != "GRUPA-KUCHNIA")
        {
            throw new Exception(
                "Zwykła rotacja poświadczeń zgubiła wybór grupy: " + (window.SonosSelectedGroupId ?? "brak") + ".");
        }
        if (window.SonosGroupRows.Count != 2 || window.SonosActiveGroup?.Id != "GRUPA-KUCHNIA")
        {
            throw new Exception("Zwykła rotacja poświadczeń zgubiła listę grup albo aktywną grupę.");
        }
        if (window.SonosAccountChangeDropsForTests != dropsBefore)
        {
            throw new Exception("Zwykła rotacja poświadczeń została uznana za zmianę konta.");
        }
        if (harness.Backend.HouseholdReads != householdReadsBefore)
        {
            throw new Exception("Zwykła rotacja poświadczeń wymusiła ponowny odczyt domów.");
        }
        if (harness.Backend.Commands.Count != 1
            || harness.Backend.CommandGroupIds is not ["GRUPA-KUCHNIA"])
        {
            throw new Exception(
                "Po rotacji polecenie użytkownika nie poszło do WYBRANEJ grupy: "
                + string.Join(", ", harness.Backend.CommandGroupIds) + ".");
        }
        if (harness.Announcements.Any(a => a.Contains("konto", StringComparison.OrdinalIgnoreCase)))
        {
            throw new Exception("Rotacja poświadczeń powtórzyła komunikat o koncie do czytnika ekranu.");
        }
        return 7;
    }

    // ===== SPOZNIONY odczyt STAREGO konta nie odtwarza jego stanu =====

    private static int MeasureLateReadAfterAccountSwapIsDiscarded()
    {
        using var harness = Harness.Create();
        var window = harness.Window;
        var coordinator = harness.AccountOwner.EnsureCoordinator();

        harness.ExecuteCommand(CommandIds.SessionSlot(8));
        harness.PumpUntil(() => window.SonosGroupRows.Count == 2, "wejście do sesji Sonos nie odczytało grup");
        harness.Pump(window.ActivateSonosGroupAsync("GRUPA-SALON"));
        if (window.SonosSelectedGroupId != "GRUPA-SALON")
        {
            throw new Exception("Przygotowanie: wybór grupy Sonos nie został zapamiętany.");
        }

        // BARIERA: odczyt STAREGO konta wisi w pół drogi.
        var brama = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        harness.Backend.ReadGate = brama.Task;
        var spozniony = window.ReadSonosGroupStateAsync();
        harness.PumpQuietly(TimeSpan.FromMilliseconds(150));
        if (spozniony.IsCompleted)
        {
            throw new Exception("Przygotowanie: odczyt nie zatrzymał się na barierze.");
        }

        // RZECZYWISTA instalacja NOWEGO konta PELNA publiczna droga logowania.
        var przed = coordinator.Snapshot.AccountBindingGeneration;
        InstallNewAccount(harness, coordinator, "ACCESS-NOWE-KONTO", "RT-NOWE-KONTO");
        if (coordinator.Snapshot.AccountBindingGeneration <= przed)
        {
            throw new Exception("Przygotowanie: nowe konto nie podniosło znacznika podłączenia.");
        }

        // GRANICA produkcyjna zauwaza zmiane, a POTEM zwalniamy stary GET.
        harness.Pump(window.PollSonosGroupIfDueAsync(DateTime.UtcNow.AddHours(1)));
        var poGranicy = window.SonosSelectedGroupId;
        brama.TrySetResult(true);
        harness.Backend.ReadGate = null;
        harness.Pump(spozniony);
        harness.PumpQuietly(TimeSpan.FromMilliseconds(250));

        if (poGranicy is not null)
        {
            throw new Exception("Granica konta nie porzuciła wyboru starego konta: " + poGranicy + ".");
        }
        if (window.SonosSelectedGroupId is not null || window.SonosActiveGroup is not null)
        {
            throw new Exception(
                "SPÓŹNIONY odczyt starego konta odtworzył wybór grupy: "
                + (window.SonosSelectedGroupId ?? "aktywna grupa") + ".");
        }
        if (window.SonosGroupRows.Count != 0)
        {
            throw new Exception("Spóźniony odczyt starego konta odtworzył listę grup.");
        }
        if (SonosPlayback(window) is not null || SonosMetadata(window) is not null)
        {
            throw new Exception("Spóźniony odczyt starego konta odtworzył dane odtwarzacza.");
        }
        if (harness.Backend.Commands.Count != 0)
        {
            throw new Exception("Wymiana konta wysłała polecenie do Sonosa.");
        }
        return 6;
    }

    // ===== START i ODMOWA nowego logowania nie ruszaja dzialajacego konta =====

    private static int MeasureFailedNewLoginKeepsWorkingAccount()
    {
        using var harness = Harness.Create();
        var window = harness.Window;
        var coordinator = harness.AccountOwner.EnsureCoordinator();

        harness.ExecuteCommand(CommandIds.SessionSlot(8));
        harness.PumpUntil(() => window.SonosGroupRows.Count == 2, "wejście do sesji Sonos nie odczytało grup");
        harness.Pump(window.ActivateSonosGroupAsync("GRUPA-KUCHNIA"));
        if (window.SonosSelectedGroupId != "GRUPA-KUCHNIA")
        {
            throw new Exception("Przygotowanie: wybór grupy Sonos nie został zapamiętany.");
        }
        var znacznik = coordinator.Snapshot.AccountBindingGeneration;
        var dropsBefore = window.SonosAccountChangeDropsForTests;

        // START nowego logowania: konto dalej dziala, bo nic go nie zastapilo.
        harness.Gateway.PlanStartu = SonosLoginStartOutcome.Ok(SyntetycznaSesjaLogowania("start-odmowa"));
        var start = coordinator.BeginLoginAsync(CancellationToken.None).GetAwaiter().GetResult();
        if (!start.Started)
        {
            throw new Exception("Przygotowanie: syntetyczne logowanie się nie rozpoczęło.");
        }

        harness.Pump(window.ReadSonosGroupStateAsync());
        harness.Pump(window.PollSonosGroupIfDueAsync(DateTime.UtcNow.AddHours(1)));
        if (window.SonosSelectedGroupId != "GRUPA-KUCHNIA" || window.SonosActiveGroup is null)
        {
            throw new Exception("Sam START nowego logowania skasował wybór działającego konta.");
        }
        if (coordinator.Snapshot.AccountBindingGeneration != znacznik)
        {
            throw new Exception("Sam start logowania zmienił znacznik podłączenia konta.");
        }

        // ODMOWA w przegladarce: nadal zadne konto nie zostalo zastapione.
        harness.Gateway.PlanWyniku = SonosLoginResultOutcome.Failure(SonosLoginStatus.Denied);
        var denied = coordinator.CheckLoginAsync(CancellationToken.None).GetAwaiter().GetResult();
        if (denied.Connected)
        {
            throw new Exception("Przygotowanie: odmowa nie ma prawa dać konta.");
        }
        if (!denied.Snapshot.HasCredentials)
        {
            throw new Exception("Przygotowanie: odmowa skasowała działający zestaw poświadczeń.");
        }

        harness.Pump(window.ReadSonosGroupStateAsync());
        harness.Pump(window.PollSonosGroupIfDueAsync(DateTime.UtcNow.AddHours(1)));
        harness.Pump(window.ExecuteSonosCommandForTests(CommandIds.PlayPause));
        harness.PumpQuietly(TimeSpan.FromMilliseconds(200));

        if (window.SonosSelectedGroupId != "GRUPA-KUCHNIA" || window.SonosActiveGroup?.Id != "GRUPA-KUCHNIA")
        {
            throw new Exception(
                "ODMOWA nowego logowania skasowała wybór działającego konta: "
                + (window.SonosSelectedGroupId ?? "brak") + ".");
        }
        if (window.SonosGroupRows.Count != 2)
        {
            throw new Exception("Odmowa nowego logowania skasowała listę grup działającego konta.");
        }
        if (window.SonosAccountChangeDropsForTests != dropsBefore)
        {
            throw new Exception("Nieudane logowanie uznano za rzeczywistą zmianę konta.");
        }
        if (harness.Backend.CommandGroupIds is not ["GRUPA-KUCHNIA"])
        {
            throw new Exception(
                "Po odmowie polecenie nie poszło do WYBRANEJ grupy działającego konta: "
                + string.Join(", ", harness.Backend.CommandGroupIds) + ".");
        }
        return 8;
    }

    /// <summary>
    /// Instaluje INNE konto PELNA publiczna droga koordynatora: BeginLoginAsync
    /// plus CheckLoginAsync na syntetycznej bramce. Zadnego recznego numeru,
    /// zadnego zerowania pol i zadnego wolania callbacka.
    /// </summary>
    private static void InstallNewAccount(
        Harness harness, SonosAccountCoordinator coordinator, string accessToken, string refreshToken)
    {
        harness.Gateway.PlanStartu = SonosLoginStartOutcome.Ok(SyntetycznaSesjaLogowania(accessToken));
        var start = coordinator.BeginLoginAsync(CancellationToken.None).GetAwaiter().GetResult();
        if (!start.Started)
        {
            throw new Exception("Przygotowanie: syntetyczne logowanie się nie rozpoczęło.");
        }

        harness.Gateway.PlanWyniku = SonosLoginResultOutcome.Ok(
            new SonosTokens(accessToken, "Bearer", 3600, refreshToken, "playback-control-all"));
        var check = coordinator.CheckLoginAsync(CancellationToken.None).GetAwaiter().GetResult();
        if (!check.Connected)
        {
            throw new Exception("Przygotowanie: syntetyczne logowanie nie dało konta.");
        }
    }

    /// <summary>
    /// PRAWDZIWA sesja logowania z produkcyjnego <see cref="SonosLoginClient"/>
    /// na syntetycznym handlerze: nic nie wychodzi do sieci i nic nie otwiera
    /// przegladarki.
    /// </summary>
    private static SonosLoginSession SyntetycznaSesjaLogowania(string ziarno)
    {
        if (!SonosLoginBrokerConfiguration.TryCreate("https://localhost:59993", out var konfiguracja)
            || konfiguracja is null)
        {
            throw new Exception("Syntetyczny origin brokera nie przeszedł walidacji.");
        }

        var sessionId = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(ziarno)))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        var cialo = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["session_id"] = sessionId,
            ["authorize_url"] = "https://api.sonos.com/login/v3/oauth?client_id=syntetyczny&response_type=code",
            ["expires_in"] = 600
        });

        using var handler = new JednorazowyHandler(cialo);
        using var klient = new SonosLoginClient(
            konfiguracja, handler, clock: () => new DateTimeOffset(2026, 9, 28, 12, 0, 0, TimeSpan.Zero));
        return klient.StartAsync(CancellationToken.None).GetAwaiter().GetResult().Session
            ?? throw new Exception("Syntetyczny start nie wydał sesji logowania.");
    }

    /// <summary>Oddaje JEDNA przygotowana odpowiedz: zero prawdziwego HTTP.</summary>
    private sealed class JednorazowyHandler(string cialo) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(cialo, Encoding.UTF8, "application/json")
            });
    }

    // ==================== pomocnicze ====================

    private static DemoMediaSession SonosSession(MainWindow window) =>
        window.SessionsForTests.FindSession("sonos")
        ?? throw new Exception("Sesja Sonos nie istnieje w prawdziwym SessionManagerze.");

    private static object? SonosPlayback(MainWindow window) => Field(window, "_sonosPlayback");

    private static object? SonosMetadata(MainWindow window) => Field(window, "_sonosMetadata");

    private static object? SonosVolume(MainWindow window) => Field(window, "_sonosVolume");

    private static object? Field(MainWindow window, string name) =>
        (window.GetType().GetField(name, Instance)
            ?? throw new Exception("Nie ma pola " + name + " w prawdziwym MainWindow."))
        .GetValue(window);

    // ==================== harness ====================

    /// <summary>
    /// WLASNE okno, WLASNA konfiguracja w katalogu tymczasowym, syntetyczna
    /// granica API grup ORAZ syntetyczny magazyn i bramka wlasciciela konta.
    /// Zadnego Show: ten pomiar nie potrzebuje pokazanego okna.
    /// </summary>
    private sealed class Harness : IDisposable
    {
        private static readonly TimeSpan Limit = TimeSpan.FromSeconds(15);

        private readonly string _directory;
        private readonly Dispatcher _dispatcher;

        private Harness(
            string directory,
            MainWindow window,
            FakeBackend backend,
            SonosAccountOwner owner,
            SyntetycznyMagazyn store,
            NieuzywanaBramka gateway,
            List<string> announcements)
        {
            _directory = directory;
            _dispatcher = Dispatcher.CurrentDispatcher;
            Window = window;
            Backend = backend;
            AccountOwner = owner;
            Store = store;
            Gateway = gateway;
            Announcements = announcements;
        }

        internal MainWindow Window { get; }

        internal FakeBackend Backend { get; }

        /// <summary>TEN SAM wlasciciel konta, ktorego uzywa prawdziwe okno.</summary>
        internal SonosAccountOwner AccountOwner { get; }

        internal SyntetycznyMagazyn Store { get; }

        internal NieuzywanaBramka Gateway { get; }

        internal List<string> Announcements { get; }

        internal ListBox MediaList => (ListBox)Window.FindName("MediaList")!;

        internal static Harness Create()
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));

            var directory = Path.Combine(Path.GetTempPath(), "amc-sonos-account-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var store = new ConfigurationStore(Path.Combine(directory, "settings.json"));
            var state = store.LoadOrCreate();
            state.Podcasts.Subscriptions.Clear();
            state.Podcasts.Episodes.Clear();
            state.WiiM.Devices.Clear();
            state.Radio.RecordingSchedules.Clear();
            state.Settings.Updates.CheckAutomatically = false;
            state.Settings.Updates.InstallOnExit = false;

            var announcements = new List<string>();
            var accountStore = new SyntetycznyMagazyn();
            var gateway = new NieuzywanaBramka();
            var window = new MainWindow(state, store)
            {
                SuppressDesktopIntegrationForTests = true,
                AnnouncementSinkForTests = announcements.Add
            };
            window.DenyApplicationUpdateStartForTests();

            // TEN SAM wlasciciel konta co produkcyjny, ale z syntetycznym
            // magazynem i bramka: zero DPAPI, zero HTTP, zero przegladarki.
            var owner = (SonosAccountOwner)(window.GetType().GetField("_sonosAccount", Instance)
                ?? throw new Exception("Nie ma pola _sonosAccount w prawdziwym MainWindow."))
                .GetValue(window)!;
            if (owner.HasCoordinator)
            {
                throw new Exception("Konstrukcja okna zainicjowała konto Sonos przed jawnym wejściem.");
            }
            owner.StoreFactory = broker =>
            {
                accountStore.BrokerOrigin = broker.Origin.AbsoluteUri;
                return accountStore;
            };
            owner.GatewayFactory = _ => gateway;

            // Zaplecze GRUP jest syntetyczne, ale znacznik podlaczenia czyta
            // PRODUKCYJNY cienki adapter nad TYM SAMYM wlascicielem.
            var backend = new FakeBackend(new SonosAccountOwnerGroupBackend(owner));
            window.SonosBackendOverride = backend;
            return new Harness(directory, window, backend, owner, accountStore, gateway, announcements);
        }

        internal void ExecuteCommand(string commandId)
        {
            var method = Window.GetType().GetMethod(
                "ExecuteCommand", Instance, binder: null, types: [typeof(string)], modifiers: null)
                ?? throw new Exception("Nie ma prawdziwej metody ExecuteCommand(string).");
            try
            {
                method.Invoke(Window, [commandId]);
            }
            catch (TargetInvocationException exception) when (exception.InnerException is not null)
            {
                throw exception.InnerException;
            }
        }

        internal void PumpUntil(Func<bool> condition, string what)
        {
            var deadline = DateTime.UtcNow + Limit;
            while (!condition())
            {
                if (DateTime.UtcNow > deadline) throw new Exception("Limit czasu: " + what + ".");
                DoEvents();
            }
        }

        internal void PumpQuietly(TimeSpan duration)
        {
            var deadline = DateTime.UtcNow + duration;
            while (DateTime.UtcNow < deadline) DoEvents();
        }

        internal void Pump(Task task)
        {
            var deadline = DateTime.UtcNow + Limit;
            while (!task.IsCompleted)
            {
                if (DateTime.UtcNow > deadline)
                {
                    throw new Exception("Limit czasu: zadanie sesji Sonos się nie zakończyło.");
                }
                DoEvents();
            }

            task.GetAwaiter().GetResult();
        }

        internal void Pump<T>(Task<T> task) => Pump((Task)task);

        private void DoEvents()
        {
            var frame = new DispatcherFrame();
            _dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => frame.Continue = false));
            Dispatcher.PushFrame(frame);
            Thread.Sleep(1);
        }

        public void Dispose()
        {
            Window.CancelSonosPendingWork();

            var closed = false;
            void OnClosed(object? sender, EventArgs e) => closed = true;
            Window.Closed += OnClosed;
            try
            {
                Window.Close();
            }
            catch (InvalidOperationException)
            {
                closed = true;
            }

            var deadline = DateTime.UtcNow + Limit;
            while (!closed)
            {
                if (DateTime.UtcNow > deadline)
                {
                    Window.Closed -= OnClosed;
                    throw new Exception("Limit czasu: własne okno pomiaru się nie zamknęło.");
                }
                DoEvents();
            }
            Window.Closed -= OnClosed;

            PumpQuietly(TimeSpan.FromMilliseconds(200));
            try
            {
                Directory.Delete(_directory, true);
            }
            catch (IOException)
            {
            }
        }
    }

    /// <summary>
    /// SYNTETYCZNA granica API grup: zero HTTP, zero tokenu. Znacznik podlaczenia
    /// pochodzi z PRODUKCYJNEGO adaptera nad tym samym wlascicielem konta.
    /// </summary>
    private sealed class FakeBackend : ISonosGroupSessionBackend, ISonosAccountBoundBackend
    {
        private readonly SonosAccountOwnerGroupBackend _adapter;

        internal FakeBackend(SonosAccountOwnerGroupBackend adapter) => _adapter = adapter;

        /// <summary>Cienki adapter nad TYM SAMYM wlascicielem - zrodlo znacznika.</summary>
        internal SonosAccountOwnerGroupBackend Adapter => _adapter;

        internal List<SonosGroupCommand> Commands { get; } = [];

        internal List<string?> CommandGroupIds { get; } = [];

        internal int PlaybackReads { get; private set; }

        internal int HouseholdReads { get; private set; }

        /// <summary>
        /// Migawka konta czytana z PRODUKCYJNEGO adaptera nad tym samym
        /// wlascicielem. Wlasnosc jest TU, zeby pomiar dzialal takze przed
        /// rozszerzeniem interfejsu zaplecza - zachowanie mierzymy na produkcji.
        /// </summary>
        public SonosAccountSnapshot? AccountSnapshot => _adapter.AccountSnapshot;

        /// <summary>Wstrzymanie ODCZYTU: pozwala zmierzyć spóźniony wynik.</summary>
        internal Task? ReadGate { get; set; }

        private readonly SonosPlaybackActions _actions = new(
            canPlay: true, canSkip: true, canSkipBack: true, canSkipToPrevious: true,
            canSeek: true, canPause: true, canStop: null, canRepeat: null, canRepeatOne: null,
            canCrossfade: null, canShuffle: null);

        public async Task<SonosGroupReadResult<SonosGroupPlaybackStatus>> ReadGroupPlaybackAsync(
            string? groupId, CancellationToken cancellationToken)
        {
            PlaybackReads++;
            if (ReadGate is { } gate) await gate.ConfigureAwait(true);
            var status = new SonosGroupPlaybackStatus(
                SonosPlaybackState.Playing, null, null, "UTWOR-1", 12_000, null, null, null, _actions);
            return SonosGroupReadResult<SonosGroupPlaybackStatus>.Success(status);
        }

        public Task<SonosGroupReadResult<SonosGroupMetadata>> ReadGroupMetadataAsync(
            string? groupId, CancellationToken cancellationToken)
        {
            var track = new SonosTrackMetadata(
                "track", "Preludium", "Chopin", "Nokturny", null,
                new SonosMetadataService("Sonos Radio", "9"), 180_000);
            var metadata = new SonosGroupMetadata(
                null, new SonosQueueItem("UTWOR-1", track, null), null, null, null);
            return Task.FromResult(SonosGroupReadResult<SonosGroupMetadata>.Success(metadata));
        }

        public Task<SonosGroupReadResult<SonosGroupVolume>> ReadGroupVolumeAsync(
            string? groupId, CancellationToken cancellationToken) =>
            Task.FromResult(
                SonosGroupReadResult<SonosGroupVolume>.Success(new SonosGroupVolume(30, false, false)));

        public Task<SonosGroupCommandResult> SendGroupCommandAsync(
            string? groupId, SonosGroupCommand command, CancellationToken cancellationToken)
        {
            Commands.Add(command);
            CommandGroupIds.Add(groupId);
            return Task.FromResult(SonosGroupCommandResult.CreateAcceptedForMeasurement(command));
        }

        public Task<SonosGroupCommandResult> SeekRelativeAsync(
            string? groupId, int deltaMillis, string? itemId, CancellationToken cancellationToken)
        {
            Commands.Add(SonosGroupCommand.SeekRelative);
            CommandGroupIds.Add(groupId);
            return Task.FromResult(
                SonosGroupCommandResult.CreateAcceptedForMeasurement(SonosGroupCommand.SeekRelative));
        }

        public Task<SonosGroupCommandResult> SetGroupVolumeAsync(
            string? groupId, int volume, CancellationToken cancellationToken)
        {
            Commands.Add(SonosGroupCommand.SetVolume);
            CommandGroupIds.Add(groupId);
            return Task.FromResult(
                SonosGroupCommandResult.CreateAcceptedForMeasurement(SonosGroupCommand.SetVolume));
        }

        public Task<SonosGroupCommandResult> SetGroupMuteAsync(
            string? groupId, bool muted, CancellationToken cancellationToken)
        {
            Commands.Add(SonosGroupCommand.SetMute);
            CommandGroupIds.Add(groupId);
            return Task.FromResult(
                SonosGroupCommandResult.CreateAcceptedForMeasurement(SonosGroupCommand.SetMute));
        }

        public Task<SonosHouseholdsReadResult> ReadHouseholdsAsync(CancellationToken cancellationToken)
        {
            HouseholdReads++;
            return Task.FromResult(SonosHouseholdsReadResult.Success([new SonosHousehold("DOM-1", "Dom", null)]));
        }

        public Task<SonosGroupsReadResult> ReadGroupsAsync(
            string householdId, CancellationToken cancellationToken) =>
            Task.FromResult(SonosGroupsReadResult.Success(new SonosHouseholdTopology(
                [
                    new SonosGroup("GRUPA-SALON", "Salon", "P1", ["P1"], SonosPlaybackState.Playing),
                    new SonosGroup("GRUPA-KUCHNIA", "Kuchnia", "P2", ["P2"], SonosPlaybackState.Idle)
                ],
                [
                    new SonosPlayer("P1", "Salon", null, null, null),
                    new SonosPlayer("P2", "Kuchnia", null, null, null)
                ],
                false)));
    }

    /// <summary>
    /// Magazyn w PAMIECI: oddaje jeden zapisany zestaw, liczy operacje. Nic nie
    /// dotyka DPAPI ani dysku uzytkownika.
    /// </summary>
    private sealed class SyntetycznyMagazyn : ISonosCredentialStore
    {
        internal string BrokerOrigin = "https://broker-testowy.invalid/";
        internal int Reads;
        internal int Writes;
        internal int Deletes;
        internal bool UsuniecieUdane = true;

        public SonosCredentialReadOutcome Read()
        {
            Reads++;
            var tokens = new SonosTokens("ACCESS-STARY", "Bearer", 3600, "RT-STARY", "playback-control-all");
            return SonosCredentialReadOutcome.Ok(
                new SonosStoredCredentials(BrokerOrigin, tokens, DateTimeOffset.UtcNow));
        }

        public SonosCredentialWriteOutcome Write(SonosStoredCredentials credentials)
        {
            Writes++;
            return SonosCredentialWriteOutcome.Ok();
        }

        public bool Delete()
        {
            Deletes++;
            return UsuniecieUdane;
        }
    }

    /// <summary>Bramka, ktora ma NIE zostac zawolana: zaden pomiar nie loguje sie na zywo.</summary>
    private sealed class NieuzywanaBramka : ISonosLoginGateway
    {
        internal int Calls;

        /// <summary>ZAPLANOWANY start. Bez planu start jest bledem pomiaru.</summary>
        internal SonosLoginStartOutcome? PlanStartu { get; set; }

        /// <summary>ZAPLANOWANY wynik. Bez planu odbior wyniku jest bledem pomiaru.</summary>
        internal SonosLoginResultOutcome? PlanWyniku { get; set; }

        public Task<SonosLoginStartOutcome> StartAsync(CancellationToken cancellationToken)
        {
            Calls++;
            if (PlanStartu is not { } plan)
            {
                throw new Exception("Pomiar sesji nie ma prawa rozpoczynać logowania Sonos bez planu.");
            }

            return Task.FromResult(plan);
        }

        public Task<SonosLoginResultOutcome> FetchResultAsync(
            SonosLoginSession session, CancellationToken cancellationToken)
        {
            Calls++;
            if (PlanWyniku is { } plan) return Task.FromResult(plan);
            throw new Exception("Pomiar sesji nie ma prawa odbierać wyniku logowania Sonos bez planu.");
        }

        /// <summary>ZAPLANOWANE odnowienie. Bez planu odnowienie jest bledem pomiaru.</summary>
        internal SonosRefreshOutcome? PlanOdnowienia { get; set; }

        internal int Odnowienia { get; private set; }

        public Task<SonosRefreshOutcome> RefreshAsync(string? refreshToken, CancellationToken cancellationToken)
        {
            Calls++;
            if (PlanOdnowienia is not { } plan)
            {
                throw new Exception("Pomiar sesji nie ma prawa odnawiać dostępu Sonos bez planu.");
            }

            Odnowienia++;
            return Task.FromResult(plan);
        }
    }
}
