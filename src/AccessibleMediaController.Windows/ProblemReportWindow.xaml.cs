using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using AccessibleMediaController.Core.Updates;
using AccessibleMediaController.Windows.Services;

namespace AccessibleMediaController.Windows;

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

    private ProblemReportKind SelectedKind => KindBox.SelectedIndex switch
    {
        1 => ProblemReportKind.FeatureRequest,
        2 => ProblemReportKind.Question,
        _ => ProblemReportKind.NotWorking
    };

    private void Send_Click(object sender, RoutedEventArgs e) => Submit(openBrowser: true);

    private void SaveOnly_Click(object sender, RoutedEventArgs e) => Submit(openBrowser: false);

    private void Submit(bool openBrowser)
    {
        if (string.IsNullOrWhiteSpace(SubjectBox.Text))
        {
            MessageBox.Show(
                "Temat zgłoszenia nie może być pusty.",
                "Zgłoś błąd lub uwagę",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            SubjectBox.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(BodyBox.Text))
        {
            MessageBox.Show(
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
            MessageBox.Show(
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
            DiagnosticLog.Info("zgloszenie", $"Otwarto formularz zgłoszenia; kopia: {saved ?? "brak"}.");
            MessageBox.Show(
                saved is null
                    ? "Formularz zgłoszenia otworzył się w przeglądarce. Sprawdź treść i wyślij ją przyciskiem na stronie."
                    : "Formularz zgłoszenia otworzył się w przeglądarce. Sprawdź treść i wyślij ją przyciskiem na stronie.\n\n"
                      + $"Kopia zgłoszenia jest w pliku:\n{saved}",
                "Zgłoś błąd lub uwagę",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            DialogResult = true;
        }
        catch (Exception exception)
        {
            DiagnosticLog.Error("zgloszenie", "Nie udało się otworzyć formularza zgłoszenia.", exception);
            MessageBox.Show(
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
