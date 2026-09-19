using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using AccessibleMediaController.Core.Updates;
using AccessibleMediaController.Windows.Services;

namespace AccessibleMediaController.Windows;

/// <summary>
/// Dostepne okno aktualizacji calego AMC.
///
/// Okno samo NICZEGO nie instaluje i nie dotyka ustawien ani profilu: pyta
/// przekazany delegat o stan wydania, a po jawnej zgodzie uzytkownika i tylko
/// dla paczki gotowej ORAZ z potwierdzona suma kontrolna ustawia
/// <see cref="InstallRequested"/> i zamyka sie. Instalacje wykonuje kod
/// wywolujacy - tak samo jak zamkniecie AMC, ktorego to okno nie robi.
///
/// Dostepnosc: wynik i wersje sa polami tylko do odczytu, wiec kursor czytnika
/// wchodzi w tresc (TextBlock jako jedyna tresc wyniku byl by nie do
/// przeczytania wierszami). Postep ma wlasne pole i jest zapisywany co 5 %,
/// wiec czytnik nie zasypuje uzytkownika mowa; komunikat mowiony
/// (<c>OperationStatusText</c>) dostaje tylko zdarzenia poczatku i konca.
/// </summary>
internal sealed partial class ApplicationUpdateWindow
{
    private readonly string _installedVersion;
    private readonly Func<bool, IProgress<double>?, CancellationToken, Task<ApplicationUpdateStatus>> _check;

    private CancellationTokenSource? _operationCancellation;
    private bool _closed;
    private int _lastReportedPercent = -1;
    private ApplicationUpdateStatus? _lastStatus;

    /// <summary>Uzytkownik jawnie zazadal instalacji gotowej, zweryfikowanej paczki.</summary>
    internal bool InstallRequested { get; private set; }

    /// <summary>Trwajaca operacja - do deterministycznego pomiaru w testach.</summary>
    internal Task PendingOperation { get; private set; } = Task.CompletedTask;

    internal ApplicationUpdateWindow(
        string installedVersion,
        Func<bool, IProgress<double>?, CancellationToken, Task<ApplicationUpdateStatus>> check)
    {
        _installedVersion = installedVersion ?? string.Empty;
        _check = check ?? throw new ArgumentNullException(nameof(check));
        InitializeComponent();
        System.Windows.Input.FocusManager.SetFocusedElement(this, UpdateText);

        InstalledVersionBox.Text = _installedVersion;
        AvailableVersionBox.Text = "nie sprawdzono";
        ProgressBox.Text = "nie rozpoczęto";
        UpdateText.Text = "Naciśnij „Sprawdź ponownie”, aby sprawdzić dostępność nowej wersji AMC.";

        // Pierwsza faza to WYLACZNIE sprawdzenie dostepnosci - nigdy pobieranie.
        ContentRendered += FocusInitialResult;
        Loaded += OnLoadedStartCheck;
        Closed += OnClosed;
    }

    private void FocusInitialResult(object? sender, EventArgs e)
    {
        ContentRendered -= FocusInitialResult;
        if (_closed) return;
        UpdateText.Focus();
        System.Windows.Input.Keyboard.Focus(UpdateText);
    }

    private void OnLoadedStartCheck(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoadedStartCheck;
        if (PendingOperation.IsCompleted && _lastStatus is null) _ = StartCheckAsync();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _closed = true;
        // Zamkniete okno nie moze zostac zmienione przez spozniona kontynuacje.
        try { _operationCancellation?.Cancel(); } catch (ObjectDisposedException) { }
    }

    /// <summary>Sprawdzenie dostepnosci bez pobierania. Zwraca zadanie do odczekania.</summary>
    internal Task StartCheckAsync() => RunOperationAsync(download: false);

    private void Check_Click(object sender, RoutedEventArgs e) => _ = RunOperationAsync(download: false);

    private void Download_Click(object sender, RoutedEventArgs e) => _ = RunOperationAsync(download: true);

    private void Install_Click(object sender, RoutedEventArgs e)
    {
        // Gotowa i zweryfikowana paczka z poprzedniego sprawdzenia - jawne zadanie.
        if (!IsOperationRunning() && IsInstallable(_lastStatus)) RequestInstallAndClose();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        if (!IsOperationRunning()) return;
        try { _operationCancellation?.Cancel(); } catch (ObjectDisposedException) { }
        OperationStatusText.Announce("Przerywanie operacji aktualizacji.");
    }

    private void Close_Click(object sender, RoutedEventArgs e) => CloseSafely();

    private bool IsOperationRunning() => !PendingOperation.IsCompleted;

    private Task RunOperationAsync(bool download)
    {
        // Pojedynczy aktywny task: podwojne klikniecia i klikniecie innego
        // przycisku w trakcie operacji sa po prostu ignorowane.
        if (IsOperationRunning() || _closed) return PendingOperation;
        if (download && !IsDownloadable(_lastStatus)) return PendingOperation;

        var cancellation = new CancellationTokenSource();
        _operationCancellation = cancellation;
        var task = ExecuteAsync(download, cancellation);
        PendingOperation = task;
        return task;
    }

    private async Task ExecuteAsync(bool download, CancellationTokenSource cancellation)
    {
        SetBusy(true, download);
        _lastReportedPercent = -1;
        ProgressBox.Text = download ? "0 %" : "trwa sprawdzanie";
        OperationStatusText.Announce(download
            ? "Pobieranie aktualizacji AMC rozpoczęte."
            : "Sprawdzanie dostępności aktualizacji AMC.");

        ApplicationUpdateStatus? status = null;
        Exception? failure = null;
        var progress = download ? new Progress<double>(ReportProgress) : null;
        try
        {
            status = await _check(download, progress, cancellation.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // Anulowanie obsluzone nizej jako zadanie uzytkownika.
        }
        catch (Exception exception)
        {
            failure = exception;
        }

        var cancelled = cancellation.IsCancellationRequested;
        cancellation.Dispose();
        if (ReferenceEquals(_operationCancellation, cancellation)) _operationCancellation = null;

        // Okno zamkniete albo operacja anulowana: zaden SPOZNIONY sukces nie
        // moze zmienic zamknietego okna ani zglosic instalacji.
        if (_closed) return;
        if (cancelled)
        {
            _lastStatus = null;
            UpdateText.Text = "Operacja aktualizacji została anulowana. Nic nie zostało zainstalowane. "
                + "Możesz sprawdzić ponownie, kiedy zechcesz.";
            AvailableVersionBox.Text = "nie sprawdzono";
            ProgressBox.Text = "anulowano";
            SetBusy(false, download);
            OperationStatusText.Announce("Operacja aktualizacji anulowana.");
            return;
        }

        if (failure is not null || status is null)
        {
            _lastStatus = null;
            UpdateText.Text = "Nie udało się sprawdzić aktualizacji AMC: "
                + (failure?.Message ?? "usługa nie zwróciła wyniku.")
                + Environment.NewLine
                + "Aktualizacja nie została zainstalowana. Możesz spróbować ponownie.";
            AvailableVersionBox.Text = "nie ustalono";
            ProgressBox.Text = "przerwano błędem";
            SetBusy(false, download);
            OperationStatusText.Announce("Sprawdzanie aktualizacji zakończone błędem.");
            return;
        }

        _lastStatus = status;
        AvailableVersionBox.Text = string.IsNullOrWhiteSpace(status.AvailableVersion)
            ? "brak nowszej wersji"
            : status.AvailableVersion;
        UpdateText.Text = DescribeStatus(status, download);
        ProgressBox.Text = download
            ? status.ReadyToInstall ? "100 % — pobrano" : "zakończono bez gotowej aktualizacji"
            : "sprawdzanie zakończone";
        SetBusy(false, download);

        if (download && IsInstallable(status))
        {
            OperationStatusText.Announce("Aktualizacja pobrana i zweryfikowana. Przekazuję do instalacji.");
            RequestInstallAndClose();
            return;
        }

        OperationStatusText.Announce(status.Message);
    }

    private void ReportProgress(double fraction)
    {
        if (_closed) return;
        var percent = (int)Math.Round(Math.Clamp(fraction, 0d, 1d) * 100d);
        // Co 5 % (i zawsze 100 %), zeby czytnik nie mowil przy kazdym procencie.
        if (percent != 100 && _lastReportedPercent >= 0 && percent - _lastReportedPercent < 5) return;
        _lastReportedPercent = percent;
        ProgressBox.Text = string.Format(CultureInfo.CurrentCulture, "{0} %", percent);
    }

    private static bool IsInstallable(ApplicationUpdateStatus? status) =>
        status is { ReadyToInstall: true, ChecksumVerified: true };

    /// <summary>
    /// Wolno proponowac pobranie. Uwaga: przy samym sprawdzeniu
    /// ChecksumVerified = false znaczy tylko „jeszcze nie pobrano i nie
    /// policzono sumy”, a nie „wydanie nie ma opublikowanej sumy” - brak sumy
    /// usluga zglasza osobna decyzja z wlasnym komunikatem.
    /// </summary>
    private static bool IsDownloadable(ApplicationUpdateStatus? status) =>
        status is { Decision: ApplicationUpdateDecision.UpdateAvailable };

    private string DescribeStatus(ApplicationUpdateStatus status, bool afterDownload)
    {
        var message = string.IsNullOrWhiteSpace(status.Message) ? "Usługa nie podała opisu." : status.Message.Trim();
        var lines = new List<string> { message };

        if (status.Decision != ApplicationUpdateDecision.UpdateAvailable)
        {
            lines.Add("Aktualizacja nie została zainstalowana. Możesz sprawdzić ponownie.");
            return string.Join(Environment.NewLine, lines);
        }

        if (IsInstallable(status))
        {
            lines.Add(FormattableString.Invariant($"Paczka {status.AvailableVersion} jest pobrana, a jej suma kontrolna zgadza się z opublikowaną."));
            lines.Add("Wybierz „Zainstaluj teraz”, aby zamknąć AMC i uruchomić instalację. Samo zamknięcie tego okna nie rozpocznie instalacji.");
        }
        else if (status.ReadyToInstall)
        {
            // Paczka jest, ale sumy NIE potwierdzono - nie wolno obiecywac weryfikacji.
            lines.Add("AMC nie może potwierdzić sumy kontrolnej pobranej paczki, dlatego nie zostanie ona zainstalowana.");
            lines.Add("Pobierz ponownie albo zaktualizuj AMC ręcznie z oficjalnego wydania.");
        }
        else if (afterDownload)
        {
            lines.Add("Pobieranie nie przygotowało gotowej paczki. Nic nie zostało zainstalowane. Możesz spróbować ponownie.");
        }
        else
        {
            lines.Add(FormattableString.Invariant($"Masz wersję {_installedVersion}. Wybierz „Pobierz i zainstaluj”, aby świadomie pobrać nową wersję."));
            lines.Add("Suma kontrolna zostanie sprawdzona po pobraniu; dopóki się nie zgodzi, nic nie zostanie zainstalowane.");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private void SetBusy(bool busy, bool download)
    {
        CheckButton.IsEnabled = !busy;
        CancelButton.IsEnabled = busy;
        DownloadButton.IsEnabled = !busy && IsDownloadable(_lastStatus) && !IsInstallable(_lastStatus);
        InstallButton.IsEnabled = !busy && IsInstallable(_lastStatus);
        if (busy && download) DownloadButton.IsEnabled = false;
    }

    private void RequestInstallAndClose()
    {
        InstallRequested = true;
        CloseSafely();
    }

    private void CloseSafely()
    {
        if (_closed) return;
        try
        {
            Close();
        }
        catch (InvalidOperationException)
        {
            // Okno nigdy nie bylo pokazane (Show/ShowDialog) - zamkniecie ma
            // byc mimo to skuteczne i nie moze wysadzic wywolujacego.
            _closed = true;
        }
    }
}
