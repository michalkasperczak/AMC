using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using AccessibleMediaController.Core.Updates;
using AccessibleMediaController.Windows.Services;

namespace AccessibleMediaController.Windows;

/// <summary>
/// Co sie stalo ze zgloszeniem. Rozdzielone, bo kopia na dysku zapisuje sie
/// ZAWSZE - takze przy udanej wysylce - wiec sama jej obecnosc nic nie mowi
/// o tym, czy zgloszenie doszlo gdziekolwiek dalej.
/// </summary>
public enum ProblemReportOutcome
{
    /// <summary>Okno zamkniete Escape lub Anuluj - nic nie powstalo.</summary>
    Abandoned,

    /// <summary>Zapisane tylko na dysku, na wlasne zyczenie uzytkownika.</summary>
    SavedToDiskOnly,

    /// <summary>Formularz otwarty w przegladarce; wysylka jeszcze przed uzytkownikiem.</summary>
    OpenedInBrowser,

    /// <summary>Nie udalo sie ani zapisac, ani wyslac.</summary>
    NothingSaved
}

/// <summary>
/// Okno zgloszenia bledu lub uwagi.
///
/// Kopia zgloszenia zapisuje sie na dysku ZAWSZE, przed jakakolwiek proba
/// wyslania. Gdy przegladarka sie nie otworzy albo GitHub odmowi, tekst nie
/// przepada - a wlasnie przy zglaszaniu awarii jest najwieksza szansa, ze cos
/// jeszcze nie zadziala.
/// </summary>
public partial class ProblemReportWindow : Window
{
    private readonly string? _exceptionTrace;
    private readonly string? _activeSession;
    private readonly string? _audioOutput;

    public ProblemReportWindow(
        string? exceptionTrace = null,
        string? activeSession = null,
        string? audioOutput = null,
        string? prefilledSubject = null)
    {
        InitializeComponent();
        _exceptionTrace = exceptionTrace;
        _activeSession = activeSession;
        _audioOutput = audioOutput;

        if (!string.IsNullOrWhiteSpace(prefilledSubject)) SubjectBox.Text = prefilledSubject;

        if (!string.IsNullOrWhiteSpace(exceptionTrace))
        {
            PrivacyNote.Text = "Do zgłoszenia dołączony jest opis błędu, który zgłosił program. "
                               + "Dziennik może zawierać nazwy plików i adresy, które odtwarzałeś. "
                               + "Zgłoszenia na GitHubie są publiczne.";
        }

        Loaded += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(SubjectBox.Text)) SubjectBox.Focus();
            else BodyBox.Focus();
        };
    }

    /// <summary>Sciezka zapisanej kopii - do odczytania przez okno wywolujace.</summary>
    public string? SavedCopyPath { get; private set; }

    /// <summary>
    /// Co sie NAPRAWDE stalo ze zgloszeniem. Sama sciezka kopii nie wystarcza:
    /// kopia zapisuje sie ZAWSZE, takze przy wysylce, wiec okno wywolujace
    /// mowilo "zapisane na dysku" rowniez wtedy, gdy zgloszenie poszlo dalej.
    /// </summary>
    public ProblemReportOutcome Outcome { get; private set; } = ProblemReportOutcome.Abandoned;

    private ProblemReportKind SelectedKind => KindBox.SelectedIndex switch
    {
        1 => ProblemReportKind.FeatureRequest,
        2 => ProblemReportKind.Question,
        _ => ProblemReportKind.NotWorking
    };

    private void Send_Click(object sender, RoutedEventArgs e) => Submit(openBrowser: true);

    private void SaveOnly_Click(object sender, RoutedEventArgs e) => Submit(openBrowser: false);

    /// <summary>
    /// Escape i Anuluj nie moga po cichu wyrzucic napisanego tekstu. Gdy w
    /// oknie cokolwiek jest, pytamy - i domyslna odpowiedzia jest "nie
    /// zamykaj", zeby przypadkowe Escape nic nie kosztowalo.
    /// </summary>
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        base.OnClosing(e);
        if (e.Cancel) return;
        if (DialogResult == true) return;
        if (string.IsNullOrWhiteSpace(SubjectBox.Text) && string.IsNullOrWhiteSpace(BodyBox.Text)) return;

        var answer = AccessibleMediaController.Windows.Services.AccessibleDialog.Show(
            "Zgłoszenie nie zostało ani wysłane, ani zapisane. Zamknąć okno i porzucić napisany tekst?",
            "Zgłoś błąd lub uwagę",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);
        if (answer == MessageBoxResult.Yes) return;

        e.Cancel = true;
        if (string.IsNullOrWhiteSpace(BodyBox.Text)) SubjectBox.Focus();
        else BodyBox.Focus();
    }

    private void Submit(bool openBrowser)
    {
        if (string.IsNullOrWhiteSpace(SubjectBox.Text))
        {
            AccessibleMediaController.Windows.Services.AccessibleDialog.Show(
                "Temat zgłoszenia nie może być pusty.",
                "Zgłoś błąd lub uwagę",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            SubjectBox.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(BodyBox.Text))
        {
            AccessibleMediaController.Windows.Services.AccessibleDialog.Show(
                "Opis zgłoszenia nie może być pusty. Napisz, co robiłeś i co się stało.",
                "Zgłoś błąd lub uwagę",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            BodyBox.Focus();
            return;
        }

        var input = new ProblemReportInput(
            SubjectBox.Text,
            BodyBox.Text,
            SelectedKind,
            EmailBox.Text,
            _exceptionTrace,
            IncludeLogBox.IsChecked == true ? ReadLogTail() : null);

        var environment = new ProblemReportEnvironment(
            ApplicationUpdateManager.InstalledVersion,
            Environment.OSVersion.VersionString,
            Environment.Version.ToString(),
            CultureInfo.CurrentUICulture.Name,
            _activeSession,
            _audioOutput);

        var title = ProblemReportComposer.ComposeTitle(input);
        var body = ProblemReportComposer.ComposeBody(input, environment);

        var saved = TrySaveCopy(body);
        SavedCopyPath = saved;

        if (!openBrowser)
        {
            Outcome = saved is null
                ? ProblemReportOutcome.NothingSaved
                : ProblemReportOutcome.SavedToDiskOnly;
            AccessibleMediaController.Windows.Services.AccessibleDialog.Show(
                saved is null
                    ? "Nie udało się zapisać zgłoszenia na dysku. Skopiuj treść z okna i wklej ją ręcznie."
                    : $"Zgłoszenie zapisane w pliku:\n{saved}",
                "Zgłoś błąd lub uwagę",
                MessageBoxButton.OK,
                saved is null ? MessageBoxImage.Warning : MessageBoxImage.Information);
            if (saved is not null) DialogResult = true;
            return;
        }

        try
        {
            var uri = ProblemReportComposer.ComposeIssueUri(
                ApplicationUpdateManager.RepositoryUri.AbsoluteUri, title, body, saved);
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
            Outcome = ProblemReportOutcome.OpenedInBrowser;
            DiagnosticLog.Info("zgloszenie", $"Otwarto formularz zgłoszenia; kopia: {saved ?? "brak"}.");
            AccessibleMediaController.Windows.Services.AccessibleDialog.Show(
                saved is null
                    ? "Formularz zgłoszenia otworzył się w przeglądarce. Treść jest już wpisana — zostaje kliknąć przycisk wysyłania na stronie. Dopóki tego nie zrobisz, zgłoszenie NIE jest wysłane."
                    : "Formularz zgłoszenia otworzył się w przeglądarce. Treść jest już wpisana — zostaje kliknąć przycisk wysyłania na stronie. Dopóki tego nie zrobisz, zgłoszenie NIE jest wysłane.\n\n"
                      + $"Kopia zgłoszenia jest w pliku:\n{saved}",
                "Zgłoś błąd lub uwagę",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            DialogResult = true;
        }
        catch (Exception exception)
        {
            Outcome = saved is null
                ? ProblemReportOutcome.NothingSaved
                : ProblemReportOutcome.SavedToDiskOnly;
            DiagnosticLog.Error("zgloszenie", "Nie udało się otworzyć formularza zgłoszenia.", exception);
            AccessibleMediaController.Windows.Services.AccessibleDialog.Show(
                saved is null
                    ? $"Nie udało się otworzyć przeglądarki ({exception.Message}) i nie udało się zapisać kopii. "
                      + "Skopiuj treść z pola opisu, żeby jej nie stracić."
                    : $"Nie udało się otworzyć przeglądarki ({exception.Message}).\n\n"
                      + $"Zgłoszenie jest zapisane w pliku:\n{saved}\n\n"
                      + $"Możesz je wkleić na stronie {ApplicationUpdateManager.RepositoryUri}/issues/new",
                "Zgłoś błąd lub uwagę",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private static string? TrySaveCopy(string body)
    {
        try
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AccessibleMediaController",
                "zgloszenia");
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, ProblemReportComposer.ComposeFileName(DateTimeOffset.Now));
            File.WriteAllText(path, body);
            return path;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or NotSupportedException)
        {
            DiagnosticLog.Warning("zgloszenie", $"Nie udało się zapisać kopii zgłoszenia: {exception.Message}");
            return null;
        }
    }

    private static IReadOnlyList<string>? ReadLogTail()
    {
        try
        {
            var path = DiagnosticLog.CurrentLogPath;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;

            // Dziennik czytamy z wspoldzieleniem zapisu - program wlasnie do
            // niego pisze, a wylaczne otwarcie rzuciloby wyjatkiem.
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);
            var buffer = new Queue<string>(ProblemReportComposer.LogTailLines);
            while (reader.ReadLine() is { } line)
            {
                if (buffer.Count == ProblemReportComposer.LogTailLines) buffer.Dequeue();
                buffer.Enqueue(line);
            }
            return buffer.Count == 0 ? null : buffer.ToList();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            DiagnosticLog.Warning("zgloszenie", $"Nie udało się odczytać dziennika: {exception.Message}");
            return null;
        }
    }
}
