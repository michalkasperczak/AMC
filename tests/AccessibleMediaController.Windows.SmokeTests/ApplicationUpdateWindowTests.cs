using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Threading;
using AccessibleMediaController.Core.Updates;
using AccessibleMediaController.Windows;
using AccessibleMediaController.Windows.Services;

/// <summary>
/// Dostepne okno aktualizacji AMC mierzone na PRAWDZIWYCH kontrolkach.
///
/// Delegat sprawdzania jest atrapa: zadnego HTTP, zadnego GitHuba, zadnego
/// pliku w profilu uzytkownika. Okno nie uruchamia instalatora - jedynie
/// zglasza InstallRequested, a instalacje wykonuje kod nadrzedny.
/// </summary>
internal static class ApplicationUpdateWindowTests
{
    private const string InstalledVersion = "0.1.0-alpha.394";

    internal static void Run() => OnSta(RunSta);

    internal static void ShowForNvda()
    {
        var thread = new Thread(() =>
        {
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
            var window = new ApplicationUpdateWindow(InstalledVersion, async (download, progress, token) =>
            {
                if (!download)
                {
                    await Task.Delay(500, token);
                    return new ApplicationUpdateStatus(ApplicationUpdateDecision.UpdateAvailable,
                        "Próba interfejsu. Dostępna jest przykładowa wersja 395. Żaden plik nie zostanie pobrany ani zainstalowany.",
                        "0.1.0-alpha.395");
                }
                for (var step = 0; step <= 10; step++)
                {
                    await Task.Delay(350, token);
                    progress?.Report(step / 10d);
                }
                return new ApplicationUpdateStatus(ApplicationUpdateDecision.UpdateAvailable,
                    "Próbna paczka jest gotowa. To tylko symulacja interfejsu.", "0.1.0-alpha.395", true, true);
            });
            window.Title += " — próba bez pobierania i instalacji";
            window.ShowInTaskbar = true;
            window.ShowDialog();
            Console.WriteLine("UI_FIXTURE_CLOSED|InstallRequested=" + window.InstallRequested);
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
    }

    private static void OnSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
            try { action(); } catch (Exception exception) { failure = exception; }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(60))) throw new Exception("Próba okna aktualizacji przekroczyła limit czasu.");
        if (failure is not null) throw new Exception("Okno aktualizacji AMC", failure);
    }

    private static void RunSta()
    {
        PoczatkoweSprawdzenieNiePobiera();
        BrakAktualizacjiPozwalaPowtorzyc();
        DostepnaAktualizacjaWymagaSwiadomegoPobrania();
        GotowaPaczkaBezSumyNieObiecujeWeryfikacji();
        BladPozwalaPowtorzycBezInstalacji();
        BladPoPobraniuNieZaprzeczaPobraniu();
        AnulowanieWyprzedzaSpoznionySukces();
        NatychmiastowePowtorzeniePoAnulowaniuDziala();
        ZamkniecieWyprzedzaSpoznionySukces();
        PodwojneKlikniecieToJednaOperacja();
        EscapeNieInstalujeGotowejPaczki();
        Console.WriteLine("OK: okno aktualizacji AMC - pola z kursorem, świadoma zgoda, anulowanie, "
            + "postęp bez zasypywania mową; bez sieci i bez uruchamiania instalatora");
    }

    // 1. Samo otwarcie i pierwsza faza sprawdzaja WYLACZNIE dostepnosc wydania.
    private static void PoczatkoweSprawdzenieNiePobiera()
    {
        var downloads = new List<bool>();
        var window = Create((download, _, _) =>
        {
            downloads.Add(download);
            return Task.FromResult(new ApplicationUpdateStatus(
                ApplicationUpdateDecision.UpToDate, "AMC jest aktualne."));
        });
        try
        {
            if (downloads.Count != 0) throw new Exception("Samo otwarcie okna już sprawdza albo pobiera aktualizację.");

            foreach (var name in new[] { "UpdateText", "InstalledVersionBox", "AvailableVersionBox", "ProgressBox" })
            {
                var box = window.FindName(name) as TextBox
                    ?? throw new Exception($"Brak pola odczytu z kursorem: {name}.");
                if (!box.IsReadOnly) throw new Exception($"Pole {name} jest edytowalne, a ma być tylko do odczytu.");
                var label = AutomationProperties.GetName(box);
                if (string.IsNullOrWhiteSpace(label) || label.Contains('{'))
                    throw new Exception($"Pole {name} nie ma czytelnej nazwy dla czytnika: „{label}”.");
            }
            if (!ReferenceEquals(System.Windows.Input.FocusManager.GetFocusedElement(window), window.FindName("UpdateText")))
                throw new Exception("Początkowy fokus nie jest przypisany do treści aktualizacji.");
            if (((TextBox)window.FindName("InstalledVersionBox")).Text != InstalledVersion)
                throw new Exception("Okno nie podaje zainstalowanej wersji AMC.");

            foreach (var (name, expected) in new[]
            {
                ("CheckButton", "Sprawdź ponownie"),
                ("DownloadButton", "Pobierz i zainstaluj"),
                ("InstallButton", "Zainstaluj teraz"),
                ("CancelButton", "Anuluj"),
                ("CloseButton", "Zamknij")
            })
            {
                var button = window.FindName(name) as Button
                    ?? throw new Exception($"Brak przycisku {name}.");
                var content = (button.Content as string ?? string.Empty).Replace("_", string.Empty);
                var automation = AutomationProperties.GetName(button);
                if (!content.Contains(expected, StringComparison.Ordinal))
                    throw new Exception($"Przycisk {name} nie ma etykiety użytkowej „{expected}”, ma „{content}”.");
                if (string.IsNullOrWhiteSpace(automation) || automation.Contains('_') || automation.Contains('{'))
                    throw new Exception($"Przycisk {name} nie ma czytelnej nazwy dla czytnika: „{automation}”.");
                if (!button.IsTabStop) throw new Exception($"Przycisk {name} jest pominięty przy Tab.");
            }
            if (!((Button)window.FindName("CloseButton")).IsCancel)
                throw new Exception("Escape nie zamyka okna aktualizacji.");
            if (((Button)window.FindName("DownloadButton")).IsEnabled
                || ((Button)window.FindName("InstallButton")).IsEnabled
                || ((Button)window.FindName("CancelButton")).IsEnabled)
                throw new Exception("Pobieranie, instalacja albo anulowanie są dostępne bez wyniku sprawdzenia.");

            SprawdzKolejnoscTresciPrzedPrzyciskami(window);

            var pending = Start(window);
            PumpUntil(() => pending.IsCompleted);
            pending.GetAwaiter().GetResult();
            if (downloads.Count != 1 || downloads[0])
                throw new Exception($"Pierwsza faza nie jest samym sprawdzeniem: {string.Join(',', downloads)}.");
            if (InstallRequested(window)) throw new Exception("Samo sprawdzenie zgłosiło instalację.");
            if (((TextBox)window.FindName("ProgressBox")).Text.Contains("trwa sprawdzanie", StringComparison.Ordinal))
                throw new Exception("Zakończona operacja nadal pokazuje, że trwa sprawdzanie.");
        }
        finally { window.Close(); }
    }

    // Tresc wyniku stoi PRZED przyciskami i nie jest samym TextBlock.
    private static void SprawdzKolejnoscTresciPrzedPrzyciskami(Window window)
    {
        var order = Flatten(window).ToList();
        var firstButton = order.FindIndex(element => element is Button);
        if (firstButton < 0) throw new Exception("Okno nie ma żadnego przycisku.");
        foreach (var name in new[] { "UpdateText", "InstalledVersionBox", "AvailableVersionBox", "ProgressBox" })
        {
            var index = order.FindIndex(element => element.Name == name);
            if (index < 0 || index > firstButton)
                throw new Exception($"Treść {name} czyta się po przyciskach, a ma stać przed nimi.");
        }
        if (window.FindName("UpdateText") is not TextBox)
            throw new Exception("Wynik sprawdzenia nie jest polem z kursorem.");
    }

    // 2. Brak aktualizacji: czytelny tekst, mozliwosc powtorzenia, zero instalacji.
    private static void BrakAktualizacjiPozwalaPowtorzyc()
    {
        var checks = 0;
        var window = Create((download, _, _) =>
        {
            checks++;
            if (download) throw new Exception("Brak aktualizacji nie może prowadzić do pobierania.");
            return Task.FromResult(new ApplicationUpdateStatus(
                ApplicationUpdateDecision.UpToDate, "Masz najnowszą wersję AMC."));
        });
        try
        {
            PumpUntil(Start(window));
            var text = ((TextBox)window.FindName("UpdateText")).Text;
            if (!text.Contains("Masz najnowszą wersję AMC.", StringComparison.Ordinal))
                throw new Exception($"Okno nie pokazało komunikatu usługi: „{text}”.");
            if (!((Button)window.FindName("CheckButton")).IsEnabled)
                throw new Exception("Po braku aktualizacji nie da się powtórzyć sprawdzenia.");
            if (((Button)window.FindName("DownloadButton")).IsEnabled
                || ((Button)window.FindName("InstallButton")).IsEnabled)
                throw new Exception("Brak aktualizacji zostawił dostępne pobieranie albo instalację.");
            Click(window, "CheckButton");
            PumpUntil(Pending(window));
            if (checks != 2) throw new Exception($"Powtórzenie sprawdzenia nie wywołało usługi ponownie: {checks}.");
            if (InstallRequested(window)) throw new Exception("Brak aktualizacji zgłosił instalację.");
        }
        finally { window.Close(); }
    }

    // 3. Dostepna aktualizacja -> JAWNA zgoda -> pobrana i zweryfikowana paczka.
    private static void DostepnaAktualizacjaWymagaSwiadomegoPobrania()
    {
        var calls = new List<bool>();
        var window = Create((download, progress, _) =>
        {
            calls.Add(download);
            if (!download)
            {
                return Task.FromResult(new ApplicationUpdateStatus(
                    ApplicationUpdateDecision.UpdateAvailable,
                    "Dostępne jest AMC 0.1.0-alpha.395.",
                    "0.1.0-alpha.395"));
            }
            for (var percent = 1; percent <= 100; percent++) progress?.Report(percent / 100d);
            return Task.FromResult(new ApplicationUpdateStatus(
                ApplicationUpdateDecision.UpdateAvailable,
                "AMC 0.1.0-alpha.395 jest pobrane i gotowe. Suma kontrolna zgodna.",
                "0.1.0-alpha.395",
                ChecksumVerified: true,
                ReadyToInstall: true));
        });
        var closed = false;
        window.Closed += (_, _) => closed = true;
        var progressChanges = 0;
        ((TextBox)window.FindName("ProgressBox")).TextChanged += (_, _) => progressChanges++;
        try
        {
            PumpUntil(Start(window));
            if (((TextBox)window.FindName("AvailableVersionBox")).Text != "0.1.0-alpha.395")
                throw new Exception("Okno nie podaje dostępnej wersji AMC.");
            var download = (Button)window.FindName("DownloadButton");
            if (!download.IsEnabled) throw new Exception("Nowa wersja nie daje przycisku pobrania i instalacji.");
            if (InstallRequested(window))
                throw new Exception("Samo wykrycie nowej wersji zgłosiło instalację bez zgody użytkownika.");

            Click(window, "DownloadButton");
            PumpUntil(Pending(window));
            if (calls.Count != 2 || !calls[1]) throw new Exception($"Jawna zgoda nie uruchomiła pobierania: {string.Join(',', calls)}.");
            if (!InstallRequested(window))
                throw new Exception("Gotowa i zweryfikowana paczka po jawnej zgodzie nie zgłosiła instalacji.");
            PumpUntil(() => closed);
            if (!closed) throw new Exception("Okno nie zamknęło się po zgłoszeniu instalacji.");

            var shown = ((TextBox)window.FindName("ProgressBox")).Text;
            if (!shown.Contains("100", StringComparison.Ordinal))
                throw new Exception($"Postęp nie doszedł do końca w czytelnym polu: „{shown}”.");
            if (progressChanges > 25)
                throw new Exception($"Postęp zasypuje czytnik zmianami treści: {progressChanges} na 100 zgłoszeń.");
            var status = (window.FindName("OperationStatusText") as TextBlock)?.Text ?? string.Empty;
            if (status.Contains('%'))
                throw new Exception("Procenty postępu idą przez komunikat mówiony - czytnik będzie mówił co chwilę.");
        }
        finally { window.Close(); }
    }

    // 4. Gotowa paczka BEZ potwierdzonej sumy: zadnej obietnicy weryfikacji ani instalacji.
    private static void GotowaPaczkaBezSumyNieObiecujeWeryfikacji()
    {
        var window = Create((download, _, _) => Task.FromResult(download
            ? new ApplicationUpdateStatus(
                ApplicationUpdateDecision.UpdateAvailable,
                "AMC 0.1.0-alpha.395 zostało pobrane.",
                "0.1.0-alpha.395",
                ChecksumVerified: false,
                ReadyToInstall: true)
            : new ApplicationUpdateStatus(
                ApplicationUpdateDecision.UpdateAvailable,
                "Dostępne jest AMC 0.1.0-alpha.395.",
                "0.1.0-alpha.395")));
        try
        {
            PumpUntil(Start(window));
            Click(window, "DownloadButton");
            PumpUntil(Pending(window));
            var text = ((TextBox)window.FindName("UpdateText")).Text;
            if (!text.Contains("nie może potwierdzić", StringComparison.Ordinal))
                throw new Exception($"Okno nie mówi wprost o braku potwierdzenia sumy: „{text}”.");
            if (text.Contains("zweryfikowan", StringComparison.OrdinalIgnoreCase)
                || text.Contains("Suma kontrolna zgodna", StringComparison.Ordinal))
                throw new Exception($"Okno obiecuje weryfikację, której nie było: „{text}”.");
            if (InstallRequested(window))
                throw new Exception("Niepotwierdzona paczka zgłosiła instalację.");
            if (((Button)window.FindName("InstallButton")).IsEnabled)
                throw new Exception("Niepotwierdzona paczka zostawiła dostępną instalację.");
            if (!((Button)window.FindName("CheckButton")).IsEnabled)
                throw new Exception("Po braku potwierdzenia nie da się powtórzyć sprawdzenia.");
        }
        finally { window.Close(); }
    }

    // 5. Blad usługi: czytelny tekst, powtorzenie, zero instalacji.
    private static void BladPozwalaPowtorzycBezInstalacji()
    {
        var window = Create((_, _, _) => Task.FromResult(new ApplicationUpdateStatus(
            ApplicationUpdateDecision.NotUnderstood,
            "Nie udało się sprawdzić aktualizacji AMC: brak połączenia.")));
        try
        {
            PumpUntil(Start(window));
            var text = ((TextBox)window.FindName("UpdateText")).Text;
            if (!text.Contains("brak połączenia", StringComparison.Ordinal))
                throw new Exception($"Okno nie pokazało treści błędu: „{text}”.");
            var announced = ((TextBlock)window.FindName("OperationStatusText")).Text;
            if (!announced.Contains("brak połączenia", StringComparison.Ordinal))
                throw new Exception("Komunikat dla czytnika pomija wynik operacji i mówi tylko o zakończeniu.");
            if (!((Button)window.FindName("CheckButton")).IsEnabled)
                throw new Exception("Po błędzie nie da się powtórzyć sprawdzenia.");
            if (InstallRequested(window) || ((Button)window.FindName("InstallButton")).IsEnabled)
                throw new Exception("Błąd sprawdzenia zostawił instalację.");
        }
        finally { window.Close(); }
    }

    private static void BladPoPobraniuNieZaprzeczaPobraniu()
    {
        var window = Create((download, _, _) => Task.FromResult(download
            ? new ApplicationUpdateStatus(ApplicationUpdateDecision.NotUnderstood,
                "Pobrany plik miał złą sumę i został usunięty.")
            : new ApplicationUpdateStatus(ApplicationUpdateDecision.UpdateAvailable,
                "Dostępna nowsza wersja.", "0.1.0-alpha.395")));
        try
        {
            PumpUntil(Start(window));
            Click(window, "DownloadButton");
            PumpUntil(Pending(window));
            var text = ((TextBox)window.FindName("UpdateText")).Text;
            if (text.Contains("Nic nie zostało pobrane", StringComparison.Ordinal))
                throw new Exception("Po błędzie sprawdzania pobranego pliku okno fałszywie twierdzi, że nic nie pobrało.");
            if (InstallRequested(window)) throw new Exception("Błąd sumy uruchomił instalację.");
        }
        finally { window.Close(); }
    }

    // 6. Anulowanie wyprzedza SPOZNIONY sukces: zadnej instalacji po anulowaniu.
    private static void AnulowanieWyprzedzaSpoznionySukces()
    {
        var late = new TaskCompletionSource<ApplicationUpdateStatus>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelled = false;
        var window = Create((download, _, token) =>
        {
            if (!download)
            {
                return Task.FromResult(new ApplicationUpdateStatus(
                    ApplicationUpdateDecision.UpdateAvailable,
                    "Dostępne jest AMC 0.1.0-alpha.395.",
                    "0.1.0-alpha.395"));
            }
            token.Register(() => cancelled = true);
            return late.Task;
        });
        try
        {
            PumpUntil(Start(window));
            Click(window, "DownloadButton");
            if (!((Button)window.FindName("CancelButton")).IsEnabled)
                throw new Exception("Trwające pobieranie nie da się anulować.");
            Click(window, "CancelButton");
            if (!cancelled) throw new Exception("Anulowanie nie dotarło do usługi aktualizacji.");
            // Spozniony sukces usługi, ktora nie uszanowala anulowania.
            late.SetResult(new ApplicationUpdateStatus(
                ApplicationUpdateDecision.UpdateAvailable,
                "AMC 0.1.0-alpha.395 jest pobrane i gotowe.",
                "0.1.0-alpha.395",
                ChecksumVerified: true,
                ReadyToInstall: true));
            PumpUntil(Pending(window));
            if (InstallRequested(window))
                throw new Exception("Spóźniony sukces po anulowaniu zgłosił instalację.");
            if (!((Button)window.FindName("CheckButton")).IsEnabled)
                throw new Exception("Po anulowaniu nie da się powtórzyć sprawdzenia.");
        }
        finally { window.Close(); }
    }

    private static void NatychmiastowePowtorzeniePoAnulowaniuDziala()
    {
        // Zdarzenie wlaczenia przycisku kolejkuje wejscie z najwyzszym priorytetem.
        // Nie wywolujemy obslugi klawisza rekurencyjnie z IsEnabledChanged:
        // takie zagniezdzenie nie jest normalnym zdarzeniem wejscia WPF.
        for (var iteration = 0; iteration < 20; iteration++)
        {
            var checks = 0;
            var window = Create(async (download, _, token) =>
            {
                if (download) await Task.Delay(Timeout.Infinite, token);
                checks++;
                return new ApplicationUpdateStatus(ApplicationUpdateDecision.UpdateAvailable,
                    "Dostępna próbna aktualizacja.", "0.1.0-alpha.395");
            });
            try
            {
                PumpUntil(Start(window));
                Click(window, "DownloadButton");
                var check = (Button)window.FindName("CheckButton");
                var queued = false;
                var retried = false;
                var priorCompleted = false;
                check.IsEnabledChanged += (_, _) =>
                {
                    if (!check.IsEnabled || queued) return;
                    queued = true;
                    window.Dispatcher.BeginInvoke(DispatcherPriority.Send, new Action(() =>
                    {
                        priorCompleted = Pending(window).IsCompleted;
                        Click(window, "CheckButton");
                        retried = true;
                    }));
                };
                Click(window, "CancelButton");
                PumpUntil(() => retried);
                PumpUntil(Pending(window));
                if (!priorCompleted || checks != 2)
                    throw new Exception("Wznowiony przycisk nie wykonał natychmiastowego ponownego sprawdzenia.");
                if (InstallRequested(window)) throw new Exception("Powtórzenie po anulowaniu zgłosiło instalację.");
            }
            finally { window.Close(); }
        }
        Console.WriteLine("OK: 20 natychmiastowych powtórzeń po anulowaniu przez kolejkę wejścia WPF");
    }

    private static void ZamkniecieWyprzedzaSpoznionySukces()
    {
        var late = new TaskCompletionSource<ApplicationUpdateStatus>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelled = false;
        var window = Create((download, _, token) =>
        {
            if (!download) return Task.FromResult(new ApplicationUpdateStatus(
                ApplicationUpdateDecision.UpdateAvailable, "Dostępna próbna aktualizacja.", "0.1.0-alpha.395"));
            token.Register(() => cancelled = true);
            return late.Task;
        });
        PumpUntil(Start(window));
        Click(window, "DownloadButton");
        window.Close();
        late.SetResult(new ApplicationUpdateStatus(ApplicationUpdateDecision.UpdateAvailable,
            "Paczka przygotowana pomimo zamknięcia okna.", "0.1.0-alpha.395", true, true));
        PumpUntil(Pending(window));
        if (!cancelled || InstallRequested(window))
            throw new Exception("Zamknięcie okna nie powstrzymało spóźnionej zgody na instalację.");
        Console.WriteLine("OK: zamknięcie okna blokuje instalację także po spóźnionym przygotowaniu paczki");
    }

    // 7. Jedno klikniecie = jedna operacja; podwojne klikniecia ignorowane.
    private static void PodwojneKlikniecieToJednaOperacja()
    {
        var gate = new TaskCompletionSource<ApplicationUpdateStatus>(TaskCreationOptions.RunContinuationsAsynchronously);
        var downloads = 0;
        var window = Create((download, _, _) =>
        {
            if (!download)
            {
                return Task.FromResult(new ApplicationUpdateStatus(
                    ApplicationUpdateDecision.UpdateAvailable,
                    "Dostępne jest AMC 0.1.0-alpha.395.",
                    "0.1.0-alpha.395"));
            }
            downloads++;
            return gate.Task;
        });
        try
        {
            PumpUntil(Start(window));
            Click(window, "DownloadButton");
            Click(window, "DownloadButton");
            Click(window, "CheckButton");
            if (downloads != 1) throw new Exception($"Podwójne kliknięcie uruchomiło {downloads} pobierania.");
            gate.SetResult(new ApplicationUpdateStatus(
                ApplicationUpdateDecision.UpdateAvailable,
                "AMC 0.1.0-alpha.395 jest pobrane i gotowe. Suma kontrolna zgodna.",
                "0.1.0-alpha.395",
                ChecksumVerified: true,
                ReadyToInstall: true));
            PumpUntil(Pending(window));
            if (downloads != 1) throw new Exception($"Po zakończeniu doliczono kolejne pobieranie: {downloads}.");
            if (!InstallRequested(window)) throw new Exception("Jedno pobranie gotowej paczki nie zgłosiło instalacji.");
        }
        finally { window.Close(); }
    }

    // 8. Gotowa paczka z poprzedniego przebiegu: instalacja tylko na jawne zadanie,
    //    Escape (zamkniecie) NIE instaluje.
    private static void EscapeNieInstalujeGotowejPaczki()
    {
        var window = Create((_, _, _) => Task.FromResult(new ApplicationUpdateStatus(
            ApplicationUpdateDecision.UpdateAvailable,
            "AMC 0.1.0-alpha.395 jest już pobrane i gotowe. Suma kontrolna zgodna.",
            "0.1.0-alpha.395",
            ChecksumVerified: true,
            ReadyToInstall: true)));
        PumpUntil(Start(window));
        if (!((Button)window.FindName("InstallButton")).IsEnabled)
            throw new Exception("Gotowa paczka nie daje przycisku „Zainstaluj teraz”.");
        if (InstallRequested(window))
            throw new Exception("Samo sprawdzenie gotowej paczki zgłosiło instalację bez zgody.");
        var description = ((TextBox)window.FindName("UpdateText")).Text;
        if (!description.Contains("Zainstaluj teraz", StringComparison.Ordinal)
            || description.Contains("Instalację wykona AMC po zamknięciu tego okna", StringComparison.Ordinal))
            throw new Exception("Opis gotowej paczki myli zamknięcie okna ze zgodą na instalację.");
        window.Close();
        if (InstallRequested(window)) throw new Exception("Zamknięcie okna zgłosiło instalację.");

        // Jawne zadanie instalacji tej samej paczki dziala i zamyka okno.
        var second = Create((_, _, _) => Task.FromResult(new ApplicationUpdateStatus(
            ApplicationUpdateDecision.UpdateAvailable,
            "AMC 0.1.0-alpha.395 jest już pobrane i gotowe. Suma kontrolna zgodna.",
            "0.1.0-alpha.395",
            ChecksumVerified: true,
            ReadyToInstall: true)));
        var closed = false;
        second.Closed += (_, _) => closed = true;
        PumpUntil(Start(second));
        Click(second, "InstallButton");
        if (!InstallRequested(second)) throw new Exception("Jawna instalacja gotowej paczki nie została zgłoszona.");
        PumpUntil(() => closed);
        if (!closed) throw new Exception("Po jawnej instalacji okno nie zamknęło się.");
        second.Close();
    }

    private static Window Create(Func<bool, IProgress<double>?, CancellationToken, Task<ApplicationUpdateStatus>> check)
    {
        var type = WindowType();
        var constructor = type.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
            .FirstOrDefault(info => info.GetParameters().Length == 2)
            ?? throw new Exception("Okno aktualizacji nie ma konstruktora (wersja, delegat sprawdzania).");
        return (Window)constructor.Invoke([InstalledVersion, check]);
    }

    private static Type WindowType() =>
        typeof(MainWindow).Assembly.GetType("AccessibleMediaController.Windows.ApplicationUpdateWindow")
        ?? throw new Exception("Brakuje osobnego dostępnego okna aktualizacji AMC.");

    private static Task Start(Window window) =>
        (Task)(window.GetType().GetMethod("StartCheckAsync", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new Exception("Okno nie udostępnia StartCheckAsync do deterministycznego pomiaru."))
        .Invoke(window, null)!;

    private static Task Pending(Window window) =>
        (Task)(window.GetType().GetProperty("PendingOperation", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new Exception("Okno nie udostępnia PendingOperation."))
        .GetValue(window)!;

    private static bool InstallRequested(Window window) =>
        (bool)(window.GetType().GetProperty("InstallRequested", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new Exception("Okno nie udostępnia InstallRequested."))
        .GetValue(window)!;

    private static void Click(Window window, string name) =>
        ((Button)window.FindName(name)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    private static IEnumerable<FrameworkElement> Flatten(DependencyObject root)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
        {
            if (child is FrameworkElement element) yield return element;
            foreach (var nested in Flatten(child)) yield return nested;
        }
    }

    private static void PumpUntil(Task operation) => PumpUntil(() => operation.IsCompleted);

    private static void PumpUntil(Func<bool> complete)
    {
        var frame = new DispatcherFrame();
        var started = DateTime.UtcNow;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(5) };
        timer.Tick += (_, _) => { if (complete() || DateTime.UtcNow - started > TimeSpan.FromSeconds(5)) frame.Continue = false; };
        timer.Start();
        Dispatcher.PushFrame(frame);
        timer.Stop();
        if (!complete()) throw new Exception("Okno aktualizacji nie zakończyło operacji w limicie czasu.");
    }
}
