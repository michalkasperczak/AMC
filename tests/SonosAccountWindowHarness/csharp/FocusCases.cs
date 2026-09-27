using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using AccessibleMediaController.Core.Sonos;
using AccessibleMediaController.Windows;

namespace SonosAccountWindowHarness;

/// <summary>
/// TRYB --focus-cases: POKAZANY, izolowany pomiar PRAWDZIWEGO fokusu klawiatury w
/// oknie konta Sonos, gdy przycisk niosacy fokus przestaje byc dostepny.
///
/// Czym ten pomiar rozni sie od trybu domyslnego (94 sprawdzenia bez GUI):
///   * okno jest RZECZYWISCIE pokazane jako dialog z WLASNYM, malym wlascicielem
///     (ShowDialog), bo dopiero wtedy istnieje fokus klawiatury systemu;
///   * mierzy <see cref="Keyboard.FocusedElement"/>, a nie tylko
///     FocusManager.GetFocusedElement - to pierwsze widzi czytnik ekranu;
///   * dziala PRAWDZIWYMI klawiszami (Tab, Enter, Escape) wyslanymi do WLASNEGO
///     okna pierwszego planu, a nie testowym Invoke z fokusem w polu instrukcji.
///     Gdy okno pierwszego planu nie jest nasze, pomiar NIE wysyla klawiszy:
///     schodzi na Button.OnClick albo Focus() i ZAPISUJE to w kwicie.
///
/// Czego ten pomiar NIE dowodzi: nie jest pomiarem mowy zywego NVDA. Mierzy
/// PRZYCZYNE zglaszana przez czytnik (fokus ladujacy na samym oknie dialogu),
/// a nie tresc wypowiedzi. Odbior zywym NVDA jest osobna czynnoscia rodzica.
///
/// Zero produkcyjnej App, MainWindow, IPC, sieci, audio, przegladarki, ustawien,
/// DPAPI i aktualizacji. Bramka, magazyn, przegladarka i potwierdzenie sa
/// atrapami z <see cref="Fakes"/>. Sterujemy WYLACZNIE wlasnymi dwoma oknami.
/// </summary>
internal static class FocusCases
{
    private const string OwnerTitle = "AMC PROBA WLASCICIEL - fokus konta Sonos (dane probne)";
    private const string DialogTitle = "AMC PROBA A11Y - fokus konta Sonos (dane probne)";

    private const byte VkTab = 0x09;
    private const byte VkReturn = 0x0D;
    private const byte VkEscape = 0x1B;
    private const uint KeyUp = 0x0002;

    private static readonly List<Row> Rows = new();
    private static int failures;
    private static Window? liveOwner;
    private static SonosAccountWindow? liveDialog;

    internal static int Run(string[] args)
    {
        var resultsRoot = ArgumentValue(args, "--results-root")
            ?? "/home/michal/projekty/amc_pomoc/sonos-account-window-focus-fix1";
        var seconds = int.TryParse(ArgumentValue(args, "--seconds"), out var parsed) ? parsed : 180;
        Directory.CreateDirectory(resultsRoot);

        // WATCHDOG OGRANICZONY: zamyka WYLACZNIE wlasne okna, zadnych obcych i
        // zadnego kill. Nie ma zadnego zwiazku z ustawianiem fokusu.
        var watchdog = new DispatcherTimer(DispatcherPriority.Send, Dispatcher.CurrentDispatcher)
        {
            Interval = TimeSpan.FromSeconds(Math.Clamp(seconds, 30, 600))
        };
        watchdog.Tick += (_, _) =>
        {
            watchdog.Stop();
            Check("watchdog/limit-pomiaru", false, "przekroczony limit czasu pomiaru");
            CloseOwnWindows();
        };
        watchdog.Start();

        try
        {
            if (args.Contains("--busy-only", StringComparer.Ordinal))
            {
                MeasureForeignFocusDuringLateCompletion();
            }
            else
            {
                MeasureCheckSuccess();
                MeasureCancelLogin();
                MeasureRetryPersist();
                MeasureDisconnect();
                MeasureForeignFocusDuringLateCompletion();
                MeasureInactiveWindowIsNotForced();
                MeasureNormalTabAndEscape();
            }
        }
        catch (Exception exception)
        {
            Check("pomiar/wyjatek", false, exception.GetType().Name + ": " + exception.Message);
        }
        finally
        {
            watchdog.Stop();
            CloseOwnWindows();
        }

        var gating = Rows.Count(row => !row.Informational);
        var payload = new
        {
            checks = gating,
            informational = Rows.Count - gating,
            failed = failures,
            pid = Environment.ProcessId,
            finished = DateTimeOffset.Now.ToString("O", CultureInfo.InvariantCulture),
            rows = Rows
        };
        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        var path = Path.Combine(
            resultsRoot,
            "focus-cases-" + DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)
            + "-pid" + Environment.ProcessId.ToString(CultureInfo.InvariantCulture) + ".json");
        File.WriteAllText(path, json);
        Console.WriteLine(json);
        Console.WriteLine("KWIT=" + path);
        Console.WriteLine("SPRAWDZEN=" + gating.ToString(CultureInfo.InvariantCulture)
            + " NIEZALICZONYCH=" + failures.ToString(CultureInfo.InvariantCulture)
            + " OBSERWACJI=" + (Rows.Count - gating).ToString(CultureInfo.InvariantCulture));
        return failures == 0 ? 0 : 1;
    }

    // ===================== przypadki =====================

    /// <summary>
    /// Sprawdz logowanie konczy sie SUKCESEM i przycisk Sprawdz znika, gdy stoi na
    /// nim fokus. To dokladnie sciezka zmierzona przez rodzica zywym NVDA.
    /// </summary>
    private static void MeasureCheckSuccess() => RunCase("sprawdz-sukces", modal: true, withAccount: false, body: ctx =>
    {
        ctx.Gateway.FetchResult = Outcomes.FetchOk(Fakes.Tokens());
        PressButton(ctx, "LoginButton", "sprawdz-sukces/start");
        Check(
            "sprawdz-sukces/po-starcie-sprawdz-widoczny",
            Program.Button(ctx.Window, "CheckLoginButton").Visibility == Visibility.Visible);

        PressButton(ctx, "CheckLoginButton", "sprawdz-sukces");
        MeasureFocusAfterVanish("sprawdz-sukces", ctx, "CheckLoginButton");
    });

    /// <summary>Anuluj logowanie: przycisk znika razem ze Sprawdz.</summary>
    private static void MeasureCancelLogin() => RunCase("anuluj", modal: true, withAccount: false, body: ctx =>
    {
        PressButton(ctx, "LoginButton", "anuluj/start");
        PressButton(ctx, "CancelLoginButton", "anuluj");
        MeasureFocusAfterVanish("anuluj", ctx, "CancelLoginButton");
    });

    /// <summary>Ponow zapis logowania: po UDANYM zapisie przycisk znika.</summary>
    private static void MeasureRetryPersist() => RunCase("ponow-zapis", modal: true, withAccount: false, body: ctx =>
    {
        ctx.Gateway.FetchResult = Outcomes.FetchOk(Fakes.Tokens());
        ctx.Store.WriteResult = SonosCredentialWriteOutcome.Failure(SonosCredentialWriteStatus.WriteFailure);
        PressButton(ctx, "LoginButton", "ponow-zapis/start");
        PressButton(ctx, "CheckLoginButton", "ponow-zapis/sprawdz");
        Check(
            "ponow-zapis/przycisk-ponowienia-widoczny",
            Program.Button(ctx.Window, "RetryPersistButton").Visibility == Visibility.Visible);

        ctx.Store.WriteResult = SonosCredentialWriteOutcome.Ok();
        PressButton(ctx, "RetryPersistButton", "ponow-zapis");
        MeasureFocusAfterVanish("ponow-zapis", ctx, "RetryPersistButton");
    });

    /// <summary>Wyloguj z Sonos przy SYNTETYCZNYM potwierdzeniu true: przycisk znika.</summary>
    private static void MeasureDisconnect() => RunCase("wyloguj", modal: true, withAccount: true, body: ctx =>
    {
        ctx.Confirm = true;
        PressButton(ctx, "DisconnectButton", "wyloguj");
        MeasureFocusAfterVanish("wyloguj", ctx, "DisconnectButton");
    });

    /// <summary>
    /// OCHRONA OBCEGO FOKUSU: uzytkownik przeszedl w trakcie oczekiwania na inna
    /// kontrolke. Spoznione zakonczenie NIE moze mu zabrac fokusu.
    /// </summary>
    private static void MeasureForeignFocusDuringLateCompletion() =>
        RunCase("obcy-fokus-late", modal: true, withAccount: false, body: ctx =>
        {
            var barrier = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            ctx.Gateway.FetchResult = Outcomes.FetchOk(Fakes.Tokens());
            ctx.Gateway.FetchBarrier = barrier.Task;

            PressButton(ctx, "LoginButton", "obcy-fokus-late/start");
            var check = Program.Button(ctx.Window, "CheckLoginButton");
            GiveFocus(ctx, check, "obcy-fokus-late/fokus-na-sprawdz");
            SendOrClick(ctx, check, "obcy-fokus-late/klik");
            Check("busy/operacja-rzeczywiscie-czeka", ctx.Window.IsBusy);
            var during = Keyboard.FocusedElement as FrameworkElement;
            Check("busy/fokus-na-uzywalnej-kontrolce",
                during is not null && IsInsideWindow(during, ctx.Window) && CanHoldFocus(during),
                Describe(during));
            Check("busy/anuluj-nadal-czynny", Program.Button(ctx.Window, "CancelLoginButton").IsEnabled);

            // UZYTKOWNIK przechodzi gdzie indziej, jeszcze przed zakonczeniem.
            // Celowo na Zamknij: jest ZAWSZE widoczny i jest kontrolka INNA niz
            // jakikolwiek sensowny punkt zapasowy przy znikajacym Sprawdz, wiec
            // pomiar wykryje bezwarunkowe przeniesienie fokusu.
            var elsewhere = Program.Button(ctx.Window, "CloseButton");
            elsewhere.Focus();
            PumpUntil(() => ReferenceEquals(Keyboard.FocusedElement, elsewhere), 2000);
            Check(
                "obcy-fokus-late/uzytkownik-stoi-na-innej-kontrolce",
                ReferenceEquals(Keyboard.FocusedElement, elsewhere),
                Describe(Keyboard.FocusedElement));

            var announcements = ctx.Window.AnnouncementCount;
            barrier.SetResult(true);
            PumpUntil(() => ctx.Window.AnnouncementCount > announcements, 5000);
            PumpUntil(() => false, 200);

            Check(
                "obcy-fokus-late/przycisk-zniknal",
                check.Visibility != Visibility.Visible,
                check.Visibility.ToString());
            Check(
                "obcy-fokus-late/fokus-uzytkownika-nie-zabrany",
                ReferenceEquals(Keyboard.FocusedElement, elsewhere),
                Describe(Keyboard.FocusedElement));
        });

    /// <summary>
    /// OKNO NIEAKTYWNE: fokus trzyma INNE okno (tu wlasne okno wlasciciela, nigdy
    /// obce). Zakonczenie operacji NIE moze go odebrac ani aktywowac dialogu.
    /// </summary>
    private static void MeasureInactiveWindowIsNotForced() =>
        RunCase("okno-nieaktywne", modal: false, withAccount: false, body: ctx =>
        {
            var barrier = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            ctx.Gateway.FetchResult = Outcomes.FetchOk(Fakes.Tokens());
            ctx.Gateway.FetchBarrier = barrier.Task;

            PressButton(ctx, "LoginButton", "okno-nieaktywne/start");
            var check = Program.Button(ctx.Window, "CheckLoginButton");
            GiveFocus(ctx, check, "okno-nieaktywne/fokus-na-sprawdz");
            SendOrClick(ctx, check, "okno-nieaktywne/klik");

            // Uzytkownik przechodzi do INNEGO okna - tu do WLASNEGO wlasciciela.
            // Gdy system nie odda nam aktywacji (np. obce okno trzyma pierwszy
            // plan), warunek wstepny NIE jest spelniony i przypadek zapisujemy
            // jako NIEZMIERZONY, zamiast udawac wynik.
            ctx.Owner.Activate();
            PumpUntil(() => ctx.Owner.IsActive && !ctx.Window.IsActive, 3000);
            var preconditionMet = !ctx.Window.IsActive;
            Note(
                "okno-nieaktywne/warunek-wstepny-dialog-nieaktywny",
                preconditionMet,
                new
                {
                    dialog = ctx.Window.IsActive,
                    owner = ctx.Owner.IsActive,
                    pierwszyPlan = DescribeWindow(GetForegroundWindow()),
                });

            var dialogHandle = new WindowInteropHelper(ctx.Window).Handle;
            var announcements = ctx.Window.AnnouncementCount;
            barrier.SetResult(true);
            PumpUntil(() => ctx.Window.AnnouncementCount > announcements, 5000);
            PumpUntil(() => false, 300);

            Check(
                "okno-nieaktywne/przycisk-zniknal",
                check.Visibility != Visibility.Visible,
                check.Visibility.ToString());

            if (preconditionMet)
            {
                Check(
                    "okno-nieaktywne/dialog-nie-aktywowal-sie",
                    !ctx.Window.IsActive,
                    new { dialog = ctx.Window.IsActive });
                Check(
                    "okno-nieaktywne/dialog-nie-wszedl-na-pierwszy-plan",
                    GetForegroundWindow() != dialogHandle,
                    DescribeWindow(GetForegroundWindow()));
                Check(
                    "okno-nieaktywne/fokus-klawiatury-nie-wrocil-do-dialogu",
                    !IsInsideWindow(Keyboard.FocusedElement, ctx.Window),
                    Describe(Keyboard.FocusedElement));
            }
            else
            {
                Note(
                    "okno-nieaktywne/przypadek-niezmierzony-brak-warunku-wstepnego",
                    false,
                    "system nie oddal aktywacji wlasnemu oknu wlasciciela");
            }

            // Punkt POWROTU okna zostaje sensowny, choc fokusu nie zabieramy.
            // To sprawdzenie jest niezalezne od aktywacji: dotyczy fokusu
            // LOGICZNEGO okna, ktory decyduje, gdzie wroci uzytkownik.
            var logical = FocusManager.GetFocusedElement(ctx.Window) as FrameworkElement;
            Check(
                "okno-nieaktywne/punkt-powrotu-jest-uzywalna-kontrolka",
                logical is not null && !ReferenceEquals(logical, ctx.Window) && CanHoldFocus(logical),
                Describe(logical));
        });

    /// <summary>
    /// KONTROLA DODATNIA aparatury i zachowania: zwykly Tab i Escape dzialaja.
    /// Najpierw PRAWDZIWE klawisze; gdy system odmowil nam pierwszego planu,
    /// pomiar schodzi na DOKLADNIE te drogi WPF, ktore Tab i Escape uruchamiaja
    /// (MoveFocus traversalem Next oraz przycisk IsCancel), i zapisuje sposob w
    /// kwicie. Bez tej kontroli nie wiadomo, czy poprawka nie zepsula nawigacji.
    /// </summary>
    private static void MeasureNormalTabAndEscape() =>
        RunCase("tab-escape", modal: true, withAccount: false, body: ctx =>
        {
            var instruction = (TextBox)ctx.Window.FindName("InstructionBox");
            Check(
                "tab-escape/fokus-startowy-na-polu-instrukcji",
                ReferenceEquals(Keyboard.FocusedElement, instruction),
                Describe(Keyboard.FocusedElement));

            var login = Program.Button(ctx.Window, "LoginButton");
            var howFirst = TabForward(ctx, () => ReferenceEquals(Keyboard.FocusedElement, login));
            Check(
                "tab-escape/tab-z-pola-instrukcji-idzie-na-pierwszy-przycisk",
                ReferenceEquals(Keyboard.FocusedElement, login),
                new { sposob = howFirst, fokus = Describe(Keyboard.FocusedElement) });

            var close = Program.Button(ctx.Window, "CloseButton");
            var howSecond = TabForward(ctx, () => ReferenceEquals(Keyboard.FocusedElement, close));
            Check(
                "tab-escape/kolejny-tab-idzie-na-zamknij",
                ReferenceEquals(Keyboard.FocusedElement, close),
                new { sposob = howSecond, fokus = Describe(Keyboard.FocusedElement) });

            var howEscape = "prawdziwy Escape";
            if (!SendKey(ctx, VkEscape, () => !ctx.Window.IsVisible))
            {
                howEscape = "przycisk IsCancel (ta sama droga co Escape)";
                typeof(Button)
                    .GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                    .Invoke(close, null);
                PumpUntil(() => !ctx.Window.IsVisible, 3000);
            }

            Check(
                "tab-escape/escape-zamyka-okno",
                !ctx.Window.IsVisible,
                new { sposob = howEscape, widoczne = ctx.Window.IsVisible });
        });

    /// <summary>
    /// Jeden krok tabulacji W PRZOD: prawdziwy klawisz, a gdy nie mamy pierwszego
    /// planu - ta sama droga WPF, ktora Tab uruchamia (MoveFocus/Next).
    /// </summary>
    private static string TabForward(Ctx ctx, Func<bool> until)
    {
        if (SendKey(ctx, VkTab, until))
        {
            return "prawdziwy Tab";
        }

        if (Keyboard.FocusedElement is FrameworkElement current)
        {
            current.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            PumpUntil(until, 2000);
        }

        return "MoveFocus Next (ta sama droga co Tab)";
    }

    // ===================== wspolny pomiar fokusu =====================

    /// <summary>
    /// SEDNO POMIARU: po zniknieciu przycisku, na ktorym stal fokus, fokus
    /// klawiatury musi zostac na UZYWALNEJ kontrolce TEGO okna. Fokus na samym
    /// oknie dialogu jest tym, co czytnik zglasza jako role 4 i co kaze mu czytac
    /// caly dialog od nowa.
    ///
    /// Pomiar NIE narzuca, KTORA to ma byc kontrolka.
    /// </summary>
    private static void MeasureFocusAfterVanish(string id, Ctx ctx, string buttonName)
    {
        var window = ctx.Window;
        var vanished = Program.Button(window, buttonName);
        Check(
            id + "/przycisk-zniknal",
            vanished.Visibility != Visibility.Visible,
            vanished.Visibility.ToString());

        var focused = Keyboard.FocusedElement;
        Check(id + "/fokus-nie-jest-pusty", focused is not null, Describe(focused));
        Check(
            id + "/fokus-nie-stoi-na-samym-oknie-dialogu",
            !ReferenceEquals(focused, window),
            Describe(focused));
        Check(
            id + "/fokus-stoi-na-kontrolce-tego-okna",
            IsInsideWindow(focused, window),
            Describe(focused));

        var element = focused as FrameworkElement;
        var usable = element is not null
            && IsInsideWindow(focused, window)
            && CanHoldFocus(element);
        Check(id + "/fokus-na-kontrolce-uzywalnej", usable, Describe(focused));
        Check(
            id + "/fokus-nie-zostal-na-znikajacym-przycisku",
            !ReferenceEquals(focused, vanished),
            Describe(focused));
        Check(
            id + "/kontrolka-fokusu-ma-nazwe-dla-czytnika",
            usable && !string.IsNullOrWhiteSpace(AccessibleName(element!)),
            usable ? AccessibleName(element!) : Describe(focused));

        // Komunikat nadal daje sie odczytac ponownie z dostepnego pola.
        var status = (TextBlock)window.FindName("OperationStatusText");
        Check(
            id + "/komunikat-zostaje-do-ponownego-odczytu",
            !string.IsNullOrWhiteSpace(status.Text) && status.Text == window.LastAnnouncement,
            status.Text);
        Check(id + "/jeden-komunikat-na-zdarzenie", ctx.Window.AnnouncementCount == ctx.ExpectedAnnouncements,
            new { licznikOkna = ctx.Window.AnnouncementCount, oczekiwano = ctx.ExpectedAnnouncements });
    }

    // ===================== sterowanie wlasnym oknem =====================

    private sealed class Ctx
    {
        internal FakeGateway Gateway = null!;
        internal FakeStore Store = null!;
        internal SonosAccountWindow Window = null!;
        internal Window Owner = null!;
        internal bool Confirm;
        internal int ExpectedAnnouncements;
        internal List<string> Opened { get; } = new();
        internal List<string> ObservedStatusText { get; } = new();
    }

    private static void RunCase(string id, bool modal, bool withAccount, Action<Ctx> body)
    {
        var gateway = new FakeGateway { StartResult = Outcomes.StartOk(Fakes.Session()) };
        var store = new FakeStore();
        if (withAccount)
        {
            store.Preload(Fakes.Stored());
        }

        // WLASCICIELEM koordynatora jest ten pomiar, nie okno.
        using var coordinator = new SonosAccountCoordinator(gateway, store, Fakes.BrokerOrigin);
        coordinator.RestoreOnce();

        var ctx = new Ctx { Gateway = gateway, Store = store, Confirm = true };
        var owner = new Window
        {
            Title = OwnerTitle,
            Width = 440,
            Height = 160,
            ShowInTaskbar = true,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Content = new Button { Content = "Okno wlasciciela proby (dane probne)" }
        };
        ctx.Owner = owner;
        liveOwner = owner;
        owner.Show();

        // PRAWDZIWY announcer produkcji (sink = null). Licznik i tresc bierzemy z
        // OBSERWACJI, nie ze sterowania mowa - inaczej atrapa polykalaby
        // produkcyjne powiadomienia UIA.
        var window = new SonosAccountWindow(
            coordinator,
            uri =>
            {
                ctx.Opened.Add(uri.AbsoluteUri);
                return true;
            },
            () => ctx.Confirm,
            announcementSink: null)
        {
            Owner = owner,
            Title = DialogTitle + " - " + id
        };
        ctx.Window = window;
        liveDialog = window;

        var status = (TextBlock)window.FindName("OperationStatusText");
        System.ComponentModel.DependencyPropertyDescriptor
            .FromProperty(TextBlock.TextProperty, typeof(TextBlock))
            .AddValueChanged(status, (_, _) => ctx.ObservedStatusText.Add(status.Text));

        window.ContentRendered += (_, _) =>
        {
            try
            {
                // Wlasne okno probuje raz przejac pierwszy plan, zeby klawisze
                // sprzetowe do niego trafialy. Odmowa systemu jest zapisywana,
                // a pomiar schodzi wtedy na Button.OnClick.
                TryTakeForegroundForOwnWindow(ctx, id);
                body(ctx);
            }
            catch (Exception exception)
            {
                Check(id + "/wyjatek-przypadku", false, exception.GetType().Name + ": " + exception.Message);
            }
            finally
            {
                if (window.IsVisible)
                {
                    window.Close();
                }
            }
        };

        if (modal)
        {
            window.ShowDialog();
        }
        else
        {
            window.Show();
            PumpUntil(() => !window.IsVisible, 60000);
        }

        Check(
            id + "/obserwowane-teksty-zgadzaja-sie-z-licznikiem-okna",
            ctx.ObservedStatusText.Count == window.AnnouncementCount,
            new { obserwacja = ctx.ObservedStatusText.Count, licznikOkna = window.AnnouncementCount });

        liveDialog = null;
        owner.Close();
        liveOwner = null;
    }

    /// <summary>
    /// Ustawia fokus na przycisku PRAWDZIWYM Tabem (a gdy pierwszy plan nie jest
    /// nasz - jawnym Focus, co trafia do kwitu), potem POTWIERDZA precondycje.
    /// </summary>
    private static void GiveFocus(Ctx ctx, Button target, string id)
    {
        var how = "tab";
        for (var index = 0; index < 12 && !ReferenceEquals(Keyboard.FocusedElement, target); index++)
        {
            if (!SendKey(ctx, VkTab, () => ReferenceEquals(Keyboard.FocusedElement, target)))
            {
                how = "Focus()";
                break;
            }
        }

        if (!ReferenceEquals(Keyboard.FocusedElement, target))
        {
            how = how == "tab" ? "tab+Focus()" : "Focus()";
            target.Focus();
            PumpUntil(() => ReferenceEquals(Keyboard.FocusedElement, target), 2000);
        }

        Check(
            id + "/precondycja-fokus-stoi-na-przycisku",
            ReferenceEquals(Keyboard.FocusedElement, target),
            new { sposob = how, fokus = Describe(Keyboard.FocusedElement) });
    }

    /// <summary>
    /// Nacisniecie przycisku PRAWDZIWYM Enterem z fokusem NA NIM. Gdy okno
    /// pierwszego planu nie jest nasze, schodzi na Button.OnClick - ta sama
    /// koncowa droga, ktora wola prawdziwy klik i Enter - i zapisuje to w kwicie.
    /// </summary>
    private static void PressButton(Ctx ctx, string name, string id)
    {
        var button = Program.Button(ctx.Window, name);
        GiveFocus(ctx, button, id);
        SendOrClick(ctx, button, id);
        PumpUntil(() => ctx.Window.AnnouncementCount > ctx.ExpectedAnnouncements, 5000);
        ctx.ExpectedAnnouncements = ctx.Window.AnnouncementCount;
    }

    private static void SendOrClick(Ctx ctx, Button button, string id)
    {
        var before = ctx.Window.AnnouncementCount;
        var sent = SendKey(ctx, VkReturn, () => ctx.Window.AnnouncementCount > before || !button.IsEnabled);
        if (!sent)
        {
            // Ta sama koncowa droga co prawdziwy klik/Enter - nie osobna sciezka testowa.
            typeof(Button)
                .GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .Invoke(button, null);
        }

        Note(id + "/sposob-nacisniecia", sent, sent ? "prawdziwy Enter" : "Button.OnClick");
    }

    /// <summary>
    /// Wysyla klawisz TYLKO wtedy, gdy oknem pierwszego planu jest WLASNE okno
    /// pomiaru. Inaczej nic nie wysyla i zwraca false - zaden klawisz nie moze
    /// trafic do obcego okna ani do uzytkownika.
    /// </summary>
    private static bool SendKey(Ctx ctx, byte key, Func<bool> until)
    {
        var handle = new WindowInteropHelper(ctx.Window).Handle;
        if (handle == IntPtr.Zero || GetForegroundWindow() != handle || !ctx.Window.IsActive)
        {
            return false;
        }

        keybd_event(key, 0, 0, IntPtr.Zero);
        keybd_event(key, 0, KeyUp, IntPtr.Zero);
        PumpUntil(until, 3000);
        return true;
    }

    /// <summary>
    /// Jednorazowa proba postawienia WLASNEGO okna na pierwszym planie, zeby
    /// klawisze sprzetowe trafialy do niego. Dotyczy WYLACZNIE okna tego pomiaru;
    /// zadnego obcego okna nie ruszamy. Wynik (takze odmowa systemu) idzie do
    /// kwitu - pomiar nie udaje, ze klawisze dotarly, gdy nie dotarly.
    /// </summary>
    private static void TryTakeForegroundForOwnWindow(Ctx ctx, string id)
    {
        var handle = new WindowInteropHelper(ctx.Window).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        ctx.Window.Activate();
        SetForegroundWindow(handle);
        PumpUntil(() => GetForegroundWindow() == handle && ctx.Window.IsActive, 1500);

        var foreground = GetForegroundWindow();
        var mine = foreground == handle;
        Note(
            id + "/pierwszy-plan-wlasnego-okna",
            mine,
            new
            {
                naszeOkno = mine,
                aktywne = ctx.Window.IsActive,
                pierwszyPlan = DescribeWindow(foreground),
                klawiszeSprzetowe = mine ? "mozliwe" : "niemozliwe - schodzimy na Button.OnClick"
            });
    }

    private static string DescribeWindow(IntPtr handle)
    {
        if (handle == IntPtr.Zero)
        {
            return "brak";
        }

        GetWindowThreadProcessId(handle, out var pid);
        var title = new System.Text.StringBuilder(256);
        GetWindowText(handle, title, title.Capacity);
        var own = pid == Environment.ProcessId ? "WLASNY" : "obcy";
        return own + " pid=" + pid.ToString(CultureInfo.InvariantCulture) + " tytul=\"" + title + "\"";
    }

    private static void CloseOwnWindows()
    {
        try
        {
            liveDialog?.Close();
        }
        catch (Exception)
        {
            // Zamykanie wlasnego okna nie moze wywrocic kwitu.
        }

        try
        {
            liveOwner?.Close();
        }
        catch (Exception)
        {
            // jak wyzej
        }

        liveDialog = null;
        liveOwner = null;
    }

    // ===================== narzedzia =====================

    private static bool IsInsideWindow(object? focused, Window window) =>
        focused is FrameworkElement element
        && !ReferenceEquals(element, window)
        && ReferenceEquals(Window.GetWindow(element), window);

    private static bool CanHoldFocus(FrameworkElement element) =>
        element.Focusable && element.IsVisible && element.IsEnabled;

    private static string AccessibleName(FrameworkElement element)
    {
        var name = AutomationProperties.GetName(element);
        return string.IsNullOrWhiteSpace(name)
            ? System.Windows.Automation.Peers.UIElementAutomationPeer.CreatePeerForElement(element)?.GetName() ?? string.Empty
            : name;
    }

    private static string Describe(object? focused) => focused switch
    {
        null => "brak (null)",
        SonosAccountWindow => "SAMO OKNO DIALOGU (SonosAccountWindow)",
        FrameworkElement element => element.GetType().Name
            + (string.IsNullOrEmpty(element.Name) ? string.Empty : " x:Name=" + element.Name),
        _ => focused.GetType().Name
    };

    /// <summary>
    /// Pompuje petle komunikatow Z LIMITEM, zamiast blokowac watek. Sluzy tylko do
    /// DOCZEKANIA zdarzenia w pomiarze - produkt nie ma tu zadnego timera fokusu.
    /// </summary>
    private static void PumpUntil(Func<bool> condition, int timeoutMilliseconds)
    {
        if (condition())
        {
            return;
        }

        var frame = new DispatcherFrame();
        var watch = Stopwatch.StartNew();
        var timer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher.CurrentDispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(5)
        };
        timer.Tick += (_, _) =>
        {
            if (condition() || watch.ElapsedMilliseconds > timeoutMilliseconds)
            {
                frame.Continue = false;
            }
        };
        timer.Start();
        Dispatcher.PushFrame(frame);
        timer.Stop();
    }

    /// <summary>
    /// Wiersz kwitu. Informacyjne wiersze (Informational) NIE wchodza do
    /// wyniku - opisuja warunki wstepne i sposob wykonania kroku.
    /// </summary>
    private sealed record Row(string id, bool pass, object? detail, bool Informational = false);

    /// <summary>
    /// Zapisuje OBSERWACJE do kwitu bez wplywu na wynik. Sluzy do warunkow
    /// wstepnych zaleznych od systemu i do sposobu wykonania kroku.
    /// </summary>
    private static void Note(string id, bool value, object? detail) =>
        Rows.Add(new Row(id, value, detail, Informational: true));

    private static void Check(string id, bool pass, object? detail = null)
    {
        Rows.Add(new Row(id, pass, detail));
        if (!pass)
        {
            failures++;
        }
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

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr window);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr window, System.Text.StringBuilder text, int maxCount);

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte virtualKey, byte scanCode, uint flags, IntPtr extraInfo);
}
