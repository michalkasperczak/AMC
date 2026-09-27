using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
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
/// POMIAR kontrolek okna konta Sonos BEZ pokazywania interfejsu.
///
/// SCISLA BRAMKA PULPITU: tryb domyslny NIE wola Show, ShowDialog, Activate ani
/// EnsureHandle, nie tworzy App ani MainWindow, nie czyta AppSettings i nie
/// otwiera przegladarki. Ogloszenia ida do TESTOWEGO odbiornika, wiec zaden
/// prawdziwy komunikat nie trafia do czytnika ekranu. Dzieki temu da sie
/// zmierzyc wlasciwosci kontrolek i obsluge zdarzen bez ruszania fokusu.
///
/// Czego ten pomiar NIE dowodzi: nie jest pomiarem zywego NVDA ani pierwszego
/// fokusu w pokazanym oknie. To osobna czynnosc rodzica po swiezej bramce.
///
/// Tryb --show-fixture (PRZYGOTOWANY, nieuruchamiany tutaj) pokazuje rzeczywiste
/// okno na jawnie nazwanych danych probnych - opis nizej przy FixtureMode.
/// </summary>
internal static class Program
{
    private static int checks;
    private static int failures;
    private static readonly List<string> Report = new();

    [STAThread]
    internal static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        SynchronizationContext.SetSynchronizationContext(
            new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));

        if (args.Contains("--show-fixture", StringComparer.Ordinal))
        {
            return FixtureMode.Run(args);
        }

        try
        {
            MeasureInstructionAndTabOrder();
            MeasureButtonNamesWithoutShortcuts();
            MeasureNegativeControlForTabOrder();
            MeasureButtonMatrixFromSnapshot();
            MeasureLoginPendingKeepsButtonsUsable();
            MeasureProofMismatchIsNotJustWaiting();
            MeasureLateCallbackAfterClose();
            MeasureBrowserNotOpenedForInvalidOutcome();
            MeasureWriteFailureThenRetryPersistWithoutHttp();
            MeasureDisconnectDeleteSuccessAndFailure();
            MeasureCloseDoesNotDisposeOrDeleteAccount();
        }
        catch (Exception exception)
        {
            failures++;
            Report.Add("WYJATEK POMIARU: " + exception.GetType().Name + ": " + exception.Message);
        }

        foreach (var line in Report)
        {
            Console.WriteLine(line);
        }

        Console.WriteLine("SPRAWDZEN=" + checks.ToString(CultureInfo.InvariantCulture)
            + " NIEZALICZONYCH=" + failures.ToString(CultureInfo.InvariantCulture));
        return failures == 0 ? 0 : 1;
    }

    // ============================ asercje ============================

    private static void Check(bool condition, string what)
    {
        checks++;
        if (!condition)
        {
            failures++;
            Report.Add("NIEZALICZONE: " + what);
        }
        else
        {
            Report.Add("ok: " + what);
        }
    }

    private static void CheckEqual(object? expected, object? actual, string what)
    {
        checks++;
        var equal = Equals(expected, actual);
        if (!equal)
        {
            failures++;
            Report.Add($"NIEZALICZONE: {what} (oczekiwano '{expected}', zmierzono '{actual}')");
        }
        else
        {
            Report.Add($"ok: {what} = {actual}");
        }
    }

    // ============================ pomiary ============================

    /// <summary>
    /// Tresc musi byc POLEM tylko do odczytu (da sie ja przejrzec strzalkami),
    /// fokusowalnym, z fokusem startowym i NIZSZYM numerem tabulacji niz kazdy
    /// przycisk. To kolejnosc z BUDOWY okna, nie z opoznienia.
    /// </summary>
    private static void MeasureInstructionAndTabOrder()
    {
        using var scenario = Scenario.Fresh();
        var window = scenario.Window;

        var instruction = (TextBox)window.FindName("InstructionBox");
        Check(instruction.IsReadOnly, "pole instrukcji jest tylko do odczytu");
        Check(instruction.Focusable, "pole instrukcji jest fokusowalne");
        Check(KeyboardNavigation.GetIsTabStop(instruction), "pole instrukcji jest przystankiem tabulacji");

        var focused = FocusManager.GetFocusedElement(window);
        Check(ReferenceEquals(focused, instruction), "fokus startowy stoi na polu instrukcji");

        foreach (var button in Buttons(window))
        {
            Check(
                instruction.TabIndex < button.TabIndex,
                $"pole instrukcji ma nizszy numer tabulacji niz przycisk {button.Name}");
        }

        var order = Buttons(window).Select(b => b.TabIndex).ToArray();
        Check(
            order.SequenceEqual(order.OrderBy(v => v)),
            "numery tabulacji przyciskow rosna w kolejnosci logicznej, nie w kolejnosci dodania");

        var text = instruction.Text;
        Check(
            text.Contains("wyłącznie w oficjalnej", StringComparison.Ordinal)
            && text.Contains("przeglądarce", StringComparison.Ordinal),
            "instrukcja mowi, ze haslo wpisuje sie wylacznie w oficjalnej przegladarce");
        Check(
            !text.Contains("Client", StringComparison.OrdinalIgnoreCase)
            && !text.Contains("sekret", StringComparison.OrdinalIgnoreCase)
            && !text.Contains("redirect", StringComparison.OrdinalIgnoreCase),
            "instrukcja nie zawiera pojec deweloperskich");

        // ZERO pol deweloperskich: jedynym polem tekstowym okna jest instrukcja.
        var textBoxes = Descendants<TextBox>(window).ToArray();
        CheckEqual(1, textBoxes.Length, "okno ma dokladnie jedno pole tekstowe (instrukcje), zero pol do wpisywania");
    }

    /// <summary>
    /// Nazwa dla czytnika NIE moze nosic skrotu ani podkreslnika: czytnik
    /// odczytuje klawisz dostepu sam, wiec doklejony skrot brzmi jak podwojenie.
    /// </summary>
    private static void MeasureButtonNamesWithoutShortcuts()
    {
        using var scenario = Scenario.Fresh();
        foreach (var button in Buttons(scenario.Window))
        {
            var name = AutomationProperties.GetName(button);
            Check(!string.IsNullOrWhiteSpace(name), $"przycisk {button.Name} ma nazwe dla czytnika");
            Check(!name.Contains('_'), $"nazwa przycisku {button.Name} nie zawiera podkreslnika klawisza dostepu");
            Check(
                !name.Contains("Alt+", StringComparison.OrdinalIgnoreCase)
                && !name.Contains("Ctrl+", StringComparison.OrdinalIgnoreCase),
                $"nazwa przycisku {button.Name} nie doklej a skrotu klawiszowego");
        }
    }

    /// <summary>
    /// KONTROLA UJEMNA: ta sama regula pomiaru zastosowana do ukladu zbudowanego
    /// STARYM, wadliwym sposobem (tresc jako napis po przyciskach, numer
    /// tabulacji z kolejnosci dodawania wspak) musi dac wynik ODWROTNY. Bez tego
    /// nie wiadomo, czy pomiar w ogole odroznia dobry uklad od zlego.
    /// </summary>
    private static void MeasureNegativeControlForTabOrder()
    {
        var panel = new StackPanel();
        var bad = new TextBlock { Text = "tresc jako napis" };
        var first = new Button { Content = "pierwszy", TabIndex = 0 };
        var second = new Button { Content = "drugi", TabIndex = 1 };
        panel.Children.Add(first);
        panel.Children.Add(second);
        panel.Children.Add(bad);

        Check(
            bad.GetType() != typeof(TextBox),
            "kontrola ujemna: napis NIE jest polem do przejrzenia strzalkami");
        Check(
            !bad.Focusable,
            "kontrola ujemna: napis nie jest fokusowalny, wiec nie moze nosic fokusu startowego");
        Check(
            first.TabIndex < second.TabIndex && panel.Children.IndexOf(bad) > panel.Children.IndexOf(first),
            "kontrola ujemna: stary uklad stawia tresc PO przyciskach, co pomiar odrzuca");
    }

    /// <summary>
    /// Kazdy stan migawki wystawia inny zestaw przyciskow. Martwych przyciskow
    /// nie oferujemy - to koszt przy tabulacji.
    /// </summary>
    private static void MeasureButtonMatrixFromSnapshot()
    {
        // 1. brak konta
        using (var scenario = Scenario.Fresh())
        {
            var w = scenario.Window;
            CheckEqual(Visibility.Visible, Button(w, "LoginButton").Visibility, "brak konta: Zaloguj widoczny");
            CheckEqual(Visibility.Collapsed, Button(w, "CheckLoginButton").Visibility, "brak konta: Sprawdz ukryty");
            CheckEqual(Visibility.Collapsed, Button(w, "CancelLoginButton").Visibility, "brak konta: Anuluj ukryty");
            CheckEqual(Visibility.Collapsed, Button(w, "RefreshButton").Visibility, "brak konta: Odnow ukryty");
            CheckEqual(Visibility.Collapsed, Button(w, "RetryPersistButton").Visibility, "brak konta: Ponow zapis ukryty");
            CheckEqual(Visibility.Collapsed, Button(w, "DisconnectButton").Visibility, "brak konta: Wyloguj ukryty");
            CheckEqual(true, Button(w, "CloseButton").IsEnabled, "brak konta: Zamknij czynny");
        }

        // 2. konto odtworzone z magazynu (RestoreOnce PRZED otwarciem okna)
        using (var scenario = Scenario.Restored())
        {
            var w = scenario.Window;
            CheckEqual(Visibility.Visible, Button(w, "RefreshButton").Visibility, "konto polaczone: Odnow widoczny");
            CheckEqual(Visibility.Visible, Button(w, "DisconnectButton").Visibility, "konto polaczone: Wyloguj widoczny");
            CheckEqual(
                Visibility.Collapsed,
                Button(w, "RetryPersistButton").Visibility,
                "konto zapisane: Ponow zapis ukryty");
            Check(
                scenario.Window.CurrentSnapshot.State == SonosAccountState.Connected,
                "okno pokazuje PRAWDZIWA migawke po RestoreOnce, nie sam status magazynu");
        }
    }

    /// <summary>
    /// Pending zostawia probe czynna: Sprawdz i Anuluj maja dzialac dalej, a
    /// komunikat kieruje do przegladarki.
    /// </summary>
    private static void MeasureLoginPendingKeepsButtonsUsable()
    {
        using var scenario = Scenario.Fresh();
        var w = scenario.Window;

        scenario.Gateway.StartResult = Outcomes.StartOk(Fakes.Session());
        Pump(w.InvokeLoginAsync());

        CheckEqual(1, scenario.Gateway.StartCalls, "Zaloguj wola Start dokladnie raz");
        CheckEqual(1, scenario.Browser.OpenedUris.Count, "Zaloguj otwiera dokladnie jeden adres");
        CheckEqual(
            Fakes.TrustedAuthorizeUri.AbsoluteUri,
            scenario.Browser.OpenedUris[0].AbsoluteUri,
            "otwarty adres pochodzi z wyniku Start i przechodzi polityke Sonos");
        CheckEqual(Visibility.Visible, Button(w, "CheckLoginButton").Visibility, "po starcie: Sprawdz widoczny");
        CheckEqual(true, Button(w, "CancelLoginButton").IsEnabled, "po starcie: Anuluj czynny");

        scenario.Gateway.FetchResult = Outcomes.FetchFailure(SonosLoginStatus.Pending);
        Pump(w.InvokeCheckLoginAsync());
        CheckEqual(1, scenario.Gateway.FetchCalls, "Sprawdz wola Fetch dokladnie raz - zadnego pollingu");
        Check(
            w.LastAnnouncement.Contains("przeglądarce", StringComparison.Ordinal),
            "Pending kieruje do dokonczenia w przegladarce");
        CheckEqual(true, Button(w, "CheckLoginButton").IsEnabled, "Pending zostawia Sprawdz czynny");
        CheckEqual(
            w.LastAnnouncement,
            ((AccessibleMediaController.Windows.Controls.AccessibleStatusTextBlock)w.FindName("OperationStatusText")).Text,
            "tresc ostatniego komunikatu zostaje w dostepnym polu do ponownego odczytu");
        CheckEqual(2, scenario.Sink.Messages.Count, "kazde zdarzenie oglasza sie RAZ, bez duplikatu");
    }

    /// <summary>
    /// StillWaiting NIE znaczy zawsze "czekaj": przy ProofMismatch i niezgodnej
    /// odpowiedzi uzytkownik ma uslyszec przyczyne i moc zaczac od nowa.
    /// </summary>
    private static void MeasureProofMismatchIsNotJustWaiting()
    {
        using var scenario = Scenario.Fresh();
        var w = scenario.Window;
        scenario.Gateway.StartResult = Outcomes.StartOk(Fakes.Session());
        Pump(w.InvokeLoginAsync());

        scenario.Gateway.FetchResult = Outcomes.FetchFailure(SonosLoginStatus.ProofMismatch);
        Pump(w.InvokeCheckLoginAsync());

        var expected = SonosLoginMessages.Describe(SonosLoginStatus.ProofMismatch);
        Check(
            w.LastAnnouncement.StartsWith(expected, StringComparison.Ordinal),
            "ProofMismatch oglasza wlasny komunikat statusu, nie samo oczekiwanie");
        Check(
            w.LastAnnouncement.Contains("na nowo", StringComparison.Ordinal),
            "ProofMismatch podaje mozliwosc rozpoczecia nowej proby");
        Check(
            !w.LastAnnouncement.Contains("Dokończ", StringComparison.Ordinal),
            "ProofMismatch NIE udaje zwyklego czekania na przegladarke");
    }

    /// <summary>
    /// Spozniona kontynuacja po zamknieciu okna nie rusza interfejsu, nie
    /// oglasza i nie wskrzesza okna.
    /// </summary>
    private static void MeasureLateCallbackAfterClose()
    {
        using var scenario = Scenario.Fresh();
        var w = scenario.Window;

        var barrier = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        scenario.Gateway.StartBarrier = barrier.Task;
        scenario.Gateway.StartResult = Outcomes.StartOk(Fakes.Session());

        var pending = w.InvokeLoginAsync();
        Check(!pending.IsCompleted, "operacja czeka na bramce, a interfejs nie jest zablokowany przez Wait");

        var announcementsBefore = scenario.Sink.Messages.Count;
        w.ShutdownOwnWork();
        barrier.SetResult(true);
        Pump(pending);

        CheckEqual(
            announcementsBefore,
            scenario.Sink.Messages.Count,
            "spozniona kontynuacja po zamknieciu NIE oglasza niczego");
        CheckEqual(0, scenario.Browser.OpenedUris.Count, "spozniona kontynuacja po zamknieciu nie otwiera przegladarki");
        Check(w.IsClosedForWork, "okno pozostaje zamkniete, nie wskrzesza sie");
    }

    /// <summary>Nieudany Start ani niezaufany wynik nie moga otworzyc przegladarki.</summary>
    private static void MeasureBrowserNotOpenedForInvalidOutcome()
    {
        using (var scenario = Scenario.Fresh())
        {
            var w = scenario.Window;
            scenario.Gateway.StartResult = Outcomes.StartFailure(SonosLoginStatus.BrokerUnreachable);
            Pump(w.InvokeLoginAsync());
            CheckEqual(0, scenario.Browser.OpenedUris.Count, "nieudany Start nie otwiera przegladarki");
            CheckEqual(
                SonosLoginMessages.Describe(SonosLoginStatus.BrokerUnreachable),
                w.LastAnnouncement,
                "nieudany Start oglasza swoj status");
        }

        using (var scenario = Scenario.Fresh())
        {
            var w = scenario.Window;
            scenario.Gateway.StartThrows = new InvalidOperationException("awaria transportu (tresc testowa)");
            Pump(w.InvokeLoginAsync());
            CheckEqual(0, scenario.Browser.OpenedUris.Count, "wyjatek Start nie otwiera przegladarki");
            Check(
                !w.LastAnnouncement.Contains("tresc testowa", StringComparison.Ordinal)
                && !w.LastAnnouncement.Contains("http", StringComparison.OrdinalIgnoreCase),
                "wyjatek Start nie wypuszcza tresci wyjatku, adresu ani tokenu");
            Check(!w.IsBusy, "po wyjatku okno przestaje byc zajete i przyjmuje kolejne polecenia");
        }

        // Domyslny otwieracz przyjmuje WYLACZNIE zaufany adres autoryzacji Sonos.
        Check(
            SonosAuthorizeUrlPolicy.IsTrustedAuthorizeUrl(Fakes.TrustedAuthorizeUri.AbsoluteUri),
            "adres uzyty w pomiarze przechodzi polityke zaufanego adresu Sonos");
        Check(
            !SonosAuthorizeUrlPolicy.IsTrustedAuthorizeUrl("https://przyklad.example/login"),
            "obcy adres logowania NIE przechodzi polityki, wiec domyslny otwieracz go odrzuci");
    }

    /// <summary>
    /// Nieudany zapis pokazujemy jako "dziala, ale nie zapisane" i oferujemy
    /// ponowienie SAMEGO zapisu - bez jednego zapytania HTTP.
    /// </summary>
    private static void MeasureWriteFailureThenRetryPersistWithoutHttp()
    {
        using var scenario = Scenario.Fresh();
        var w = scenario.Window;

        scenario.Gateway.StartResult = Outcomes.StartOk(Fakes.Session());
        Pump(w.InvokeLoginAsync());

        scenario.Store.WriteResult = SonosCredentialWriteOutcome.Failure(SonosCredentialWriteStatus.WriteFailure);
        scenario.Gateway.FetchResult = Outcomes.FetchOk(Fakes.Tokens());
        Pump(w.InvokeCheckLoginAsync());

        Check(
            w.LastAnnouncement.Contains("nie zostało zapisane", StringComparison.Ordinal),
            "nieudany zapis oglasza sie jako dzialajace, ale niezapisane konto");
        Check(
            !w.LastAnnouncement.Contains("Wylogowano", StringComparison.Ordinal),
            "nieudany zapis NIE jest pokazywany jako wylogowanie");
        CheckEqual(
            Visibility.Visible,
            Button(w, "RetryPersistButton").Visibility,
            "nieudany zapis oferuje ponowienie samego zapisu");

        var httpBefore = scenario.Gateway.TotalCalls;
        scenario.Store.WriteResult = SonosCredentialWriteOutcome.Ok();
        w.InvokeRetryPersist();

        CheckEqual(httpBefore, scenario.Gateway.TotalCalls, "ponowienie zapisu nie wysyla ZADNEGO zapytania");
        CheckEqual(2, scenario.Store.WriteCalls, "ponowienie zapisu wola magazyn dokladnie raz wiecej");
        Check(
            w.CurrentSnapshot.IsPersisted,
            "po udanym ponowieniu konto jest zapisane");
        CheckEqual(
            Visibility.Collapsed,
            Button(w, "RetryPersistButton").Visibility,
            "po udanym zapisie przycisk ponowienia znika");
    }

    /// <summary>
    /// Jawne wylogowanie idzie do Store.Delete. Nieudane usuniecie MUSI byc
    /// pokazane jako niedokonczone, nigdy jako wylogowanie.
    /// </summary>
    private static void MeasureDisconnectDeleteSuccessAndFailure()
    {
        using (var scenario = Scenario.Restored())
        {
            var w = scenario.Window;
            scenario.Confirm.Answer = false;
            w.InvokeDisconnect();
            CheckEqual(0, scenario.Store.DeleteCalls, "odmowa potwierdzenia nie usuwa niczego");
            Check(w.CurrentSnapshot.HasCredentials, "odmowa potwierdzenia zachowuje konto");

            scenario.Confirm.Answer = true;
            w.InvokeDisconnect();
            CheckEqual(1, scenario.Store.DeleteCalls, "potwierdzone wylogowanie wola Delete dokladnie raz");
            Check(
                w.LastAnnouncement.Contains("Wylogowano", StringComparison.Ordinal),
                "udane wylogowanie oglasza sie jako wykonane");
            CheckEqual(
                Visibility.Collapsed,
                Button(w, "DisconnectButton").Visibility,
                "po wylogowaniu przycisk wylogowania znika");
        }

        using (var scenario = Scenario.Restored())
        {
            var w = scenario.Window;
            scenario.Confirm.Answer = true;
            scenario.Store.DeleteResult = false;
            w.InvokeDisconnect();

            Check(
                w.LastAnnouncement.Contains("nie zostało dokończone", StringComparison.Ordinal),
                "nieudane usuniecie oglasza sie jako NIEDOKONCZONE wylogowanie");
            Check(
                !w.LastAnnouncement.StartsWith("Wylogowano", StringComparison.Ordinal),
                "nieudane usuniecie NIE jest pokazywane jako wylogowanie");
            Check(
                w.CurrentSnapshot.PersistedRecordMayRemain,
                "nieudane usuniecie zostawia jawny stan: zapis moze pozostac na dysku");
        }
    }

    /// <summary>
    /// WLASCICIELEM koordynatora jest aplikacja. Zamkniecie okna anuluje wlasna
    /// probe, ale nie wola Dispose, nie wola Disconnect i nie kasuje konta.
    /// </summary>
    private static void MeasureCloseDoesNotDisposeOrDeleteAccount()
    {
        using var scenario = Scenario.Restored();
        var w = scenario.Window;
        var generationBefore = w.CurrentSnapshot.CredentialGeneration;

        scenario.Gateway.StartResult = Outcomes.StartOk(Fakes.Session());
        Pump(w.InvokeLoginAsync());
        w.ShutdownOwnWork();

        CheckEqual(0, scenario.Store.DeleteCalls, "zamkniecie okna nie kasuje zapisanego konta");

        var after = scenario.Coordinator.Snapshot;
        Check(after.HasCredentials, "koordynator po zamknieciu okna nadal ma poswiadczenia w pamieci");
        CheckEqual(
            generationBefore,
            after.CredentialGeneration,
            "zamkniecie okna nie rusza generacji zestawu poswiadczen");
        Check(!after.IsAwaitingBrowser, "zamkniecie anuluje WLASNA probe logowania okna");

        // Koordynator nie jest zwolniony: kolejna operacja wlasciciela dziala.
        var retry = scenario.Coordinator.RetryPersist();
        Check(retry is not null, "koordynator dziala po zamknieciu okna - okno go NIE zwolnilo");
    }

    // ============================ narzedzia ============================

    internal static Button Button(DependencyObject root, string name) =>
        (Button)((FrameworkElement)root).FindName(name);

    internal static IReadOnlyList<Button> Buttons(SonosAccountWindow window) =>
        new[]
        {
            Button(window, "LoginButton"),
            Button(window, "CheckLoginButton"),
            Button(window, "CancelLoginButton"),
            Button(window, "RefreshButton"),
            Button(window, "RetryPersistButton"),
            Button(window, "DisconnectButton"),
            Button(window, "CloseButton")
        };

    private static IEnumerable<T> Descendants<T>(DependencyObject root)
        where T : DependencyObject
    {
        foreach (var child in System.Windows.LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
        {
            if (child is T typed)
            {
                yield return typed;
            }

            foreach (var nested in Descendants<T>(child))
            {
                yield return nested;
            }
        }
    }

    /// <summary>
    /// Pompuje petle Dispatchera Z LIMITEM zamiast blokowac watek przez Wait lub
    /// Result. Blokada zabilaby kontynuacje planowane na tym samym watku.
    /// </summary>
    internal static void Pump(Task task, int timeoutMilliseconds = 5000)
    {
        if (task.IsCompleted)
        {
            Rethrow(task);
            return;
        }

        var frame = new DispatcherFrame();
        var watch = Stopwatch.StartNew();
        var timer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher.CurrentDispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(10)
        };
        timer.Tick += (_, _) =>
        {
            if (task.IsCompleted || watch.ElapsedMilliseconds > timeoutMilliseconds)
            {
                frame.Continue = false;
            }
        };
        timer.Start();
        Dispatcher.PushFrame(frame);
        timer.Stop();

        if (!task.IsCompleted)
        {
            throw new TimeoutException("Operacja okna nie zakonczyla sie w limicie pomiaru.");
        }

        Rethrow(task);
    }

    private static void Rethrow(Task task)
    {
        if (task.IsFaulted && task.Exception is not null)
        {
            throw task.Exception.GetBaseException();
        }
    }
}
