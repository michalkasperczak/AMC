using System.ComponentModel;
using System.IO;
using System.Windows;
using AccessibleMediaController.Windows.Services;
using Microsoft.Win32;

namespace AccessibleMediaController.Windows;

public partial class AudioClipAppendWindow : Controls.AccessibleWindow
{
    private readonly string _sourcePath;
    private readonly TimeSpan _start;
    private readonly TimeSpan _end;
    /// <summary>
    /// Globalne „Zachowuj kopie po edycji”, przechwycone w chwili otwarcia okna,
    /// zgodnie z tym, co uzytkownik widzial przed rozpoczeciem dopisywania.
    /// </summary>
    private readonly bool _keepBackup;
    private readonly string _backupPolicyNotice;
    private CancellationTokenSource? _cancellation;
    private bool _working;
    private bool _allowClose;

    public AudioClipAppendWindow(
        string sourcePath,
        string sourceTitle,
        TimeSpan start,
        TimeSpan end,
        bool keepBackup = false)
    {
        InitializeComponent();
        _sourcePath = sourcePath; _start = start; _end = end;
        _keepBackup = keepBackup;
        _backupPolicyNotice = (keepBackup
            ? "Kopia poprzedniej wersji pozostanie obok pliku po edycji. "
            : "Kopia poprzedniej wersji zostanie usunięta po sprawdzeniu zapisanego pliku. ")
            + "Przy błędzie lub niepewnym wyniku kopia zostanie zachowana.";
        System.Windows.Automation.AutomationProperties.SetHelpText(TargetPathBox,
            "Fragment zostanie dopisany na końcu. Źródło nie zmieni się. " + _backupPolicyNotice);
        BackupRulesText.Text = "Dopisanie zachowuje dotychczasowy dźwięk na początku pliku. WAV i FLAC są bezstratne. "
            + "W formatach MP3, M4A, AAC, Ogg i Opus całość zostanie ponownie skompresowana; "
            + "przed rozpoczęciem pojawi się pytanie o zgodę. " + _backupPolicyNotice;
        SelectionText.Text = $"{sourceTitle}. Fragment od {start:c} do {end:c}.";
        System.Windows.Automation.AutomationProperties.SetName(SelectionText, SelectionText.Text);
    }

    public string? ResultPath { get; private set; }
    internal AudioClipAppendResult? Result { get; private set; }
    private void Window_Loaded(object sender, RoutedEventArgs e) => TargetPathBox.Focus();

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        if (_working) return;
        var dialog = new OpenFileDialog
        {
            Title = "Wybierz istniejący plik, na końcu którego dopisać fragment",
            CheckFileExists = true, CheckPathExists = true, Multiselect = false,
            Filter = "Obsługiwane audio (*.wav;*.flac;*.mp3;*.m4a;*.aac;*.ogg;*.oga;*.opus)|*.wav;*.flac;*.mp3;*.m4a;*.aac;*.ogg;*.oga;*.opus"
        };
        if (dialog.ShowDialog(this) == true) TargetPathBox.Text = dialog.FileName;
        TargetPathBox.Focus();
    }

    private async void Append_Click(object sender, RoutedEventArgs e)
    {
        if (_working) return;
        try
        {
            var target = Path.GetFullPath(TargetPathBox.Text.Trim());
            if (!File.Exists(target)) throw new FileNotFoundException("Wybierz istniejący plik docelowy.");
            if (!AudioClipAppender.SupportsTarget(target))
                throw new NotSupportedException("Ten format wymaga składnika FFmpeg lub nie jest obsługiwany. Wybierz WAV, FLAC, MP3, M4A, AAC, Ogg albo Opus.");
            if (string.Equals(Path.GetFullPath(_sourcePath), target, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Plik docelowy musi być inny niż źródło fragmentu.");
            var extension = Path.GetExtension(target).ToLowerInvariant();
            if (extension is not (".wav" or ".flac"))
            {
                // Use the same selected policy as the target field and rules.
                var backupSentence = _backupPolicyNotice + " ";
                var answer = AccessibleDialog.Show(this,
                    "Dopisanie do tego formatu wymaga ponownej kompresji całej zawartości pliku i może obniżyć jej jakość. "
                    + backupSentence
                    + "Czy dopisać fragment?",
                    "Ponowna kompresja pliku docelowego", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
                if (answer != MessageBoxResult.Yes)
                {
                    AppendStatus.Text = "Anulowano dopisywanie. Pliki nie zostały zmienione.";
                    TargetPathBox.Focus(); return;
                }
            }

            _working = true;
            TargetPathBox.IsEnabled = BrowseButton.IsEnabled = AppendButton.IsEnabled = false;
            AppendProgress.Value = 0; AppendProgress.Visibility = Visibility.Visible;
            AppendStatus.Text = "Przygotowywanie fragmentu…";
            CancelButton.Content = "_Przerwij";
            _cancellation = new CancellationTokenSource();
            var progress = new Progress<double>(value => {
                if (!_working) return;
                AppendProgress.Value = Math.Clamp(value * 100d, 0d, 100d);
                AppendStatus.Text = $"Przygotowano {Math.Round(AppendProgress.Value):0}%.";
            });
            var request = new AudioClipAppendRequest(_sourcePath, target, _start, _end);
            var token = _cancellation.Token;
            Result = await Task.Run(
                () => AudioClipAppender.AppendAsync(request, progress, token, keepBackup: _keepBackup),
                token);
            ResultPath = target;
            AppendStatus.Text = "Fragment dopisany. " + DescribeBackupOutcome(_keepBackup, Result.BackupPath);
            _allowClose = true; DialogResult = true;
        }
        catch (OperationCanceledException)
        {
            AppendStatus.Text = "Dopisywanie przerwane. Pliki źródłowy i docelowy nie zostały zmienione.";
            DiagnosticLog.Info("audio-clip", "Anulowano dopisywanie fragmentu.");
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or FormatException or UnauthorizedAccessException
            or InvalidOperationException or NotSupportedException or ArgumentException)
        {
            DiagnosticLog.Warning("audio-clip", $"Dopisanie nie powiodło się: {exception.GetType().Name}: {exception.Message}");
            AppendStatus.Text = exception.Message;
            AccessibleDialog.Show(this, exception.Message, "Nie udało się dopisać fragmentu", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _working = false; _cancellation?.Dispose(); _cancellation = null;
            TargetPathBox.IsEnabled = BrowseButton.IsEnabled = AppendButton.IsEnabled = true;
            CancelButton.Content = "_Anuluj";
            if (!_allowClose) TargetPathBox.Focus();
        }
    }

    /// <summary>
    /// Uczciwy opis losu kopii, oparty na RZECZYWISTYM wyniku backendu, a nie na
    /// samym zyczeniu. Pusta <paramref name="backupPath"/> oznacza kopie usunieta,
    /// wiec komunikat nie moze jej obiecywac. Niepusta sciezka przy WYLACZONEJ
    /// opcji znaczy, ze kopii nie usunieto (blad albo niepewnosc) — i to trzeba
    /// powiedziec wprost, zeby uzytkownik wiedzial, ze plik zajmuje miejsce.
    /// </summary>
    internal static string DescribeBackupOutcome(bool keepBackup, string? backupPath)
    {
        if (string.IsNullOrWhiteSpace(backupPath))
            return "Kopia poprzedniej wersji została usunięta po sprawdzeniu pliku.";
        return keepBackup
            ? "Zachowano kopię poprzedniej wersji."
            : "Plik został wyedytowany, ale kopię poprzedniej wersji zachowano, bo jej nie usunięto.";
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        if (_working) { RequestCancellation(); return; }
        _allowClose = true; DialogResult = false;
    }
    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_allowClose || !_working) return;
        e.Cancel = true; RequestCancellation();
    }
    private void RequestCancellation()
    {
        AppendStatus.Text = "Przerywanie dopisywania…";
        _cancellation?.Cancel();
    }
}
