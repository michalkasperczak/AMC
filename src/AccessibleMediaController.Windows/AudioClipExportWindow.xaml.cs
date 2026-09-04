using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using AccessibleMediaController.Windows.Services;
using Microsoft.Win32;

namespace AccessibleMediaController.Windows;

public partial class AudioClipExportWindow : AccessibleMediaController.Windows.Controls.AccessibleWindow
{
    private readonly string _sourcePath;
    private readonly string _sourceTitle;
    private readonly TimeSpan _start;
    private readonly TimeSpan _end;
    private readonly string _contentKind;
    private CancellationTokenSource? _exportCancellation;
    private bool _exporting;
    private bool _allowClose;

    public AudioClipExportWindow(
        string sourcePath,
        string sourceTitle,
        TimeSpan start,
        TimeSpan end,
        string contentKind = "fragment")
    {
        InitializeComponent();
        _sourcePath = sourcePath;
        _sourceTitle = sourceTitle;
        _start = start;
        _end = end;
        _contentKind = string.Equals(contentKind, "rozdział", StringComparison.OrdinalIgnoreCase)
            ? "rozdział"
            : "fragment";
        Title = _contentKind == "rozdział" ? "Zapisz rozdział audio" : "Zapisz fragment audio";
        SelectionText.Text =
            $"{sourceTitle}. Od {FormatTime(start)} do {FormatTime(end)}. "
            + $"Długość {_contentKind}u: {FormatTime(end - start)}.";
        System.Windows.Automation.AutomationProperties.SetName(SelectionText, SelectionText.Text);
        System.Windows.Automation.AutomationProperties.SetName(
            FormatCombo,
            _contentKind == "rozdział" ? "Sposób zapisu rozdziału" : "Sposób zapisu fragmentu");
        System.Windows.Automation.AutomationProperties.SetName(
            ExportProgress,
            _contentKind == "rozdział" ? "Postęp zapisywania rozdziału" : "Postęp zapisywania fragmentu");

        if (AudioClipExporter.IsFfmpegAvailable)
        {
            OriginalFormatItem.IsSelected = true;
            ComponentNotice.Text =
                "Zapis bez konwersji i FLAC korzystają ze składnika FFmpeg. WAV działa niezależnie.";
        }
        else
        {
            OriginalFormatItem.Visibility = Visibility.Collapsed;
            FlacFormatItem.Visibility = Visibility.Collapsed;
            WavFormatItem.IsSelected = true;
            ComponentNotice.Text =
                "Składnik FFmpeg nie jest dostępny, dlatego obecnie można zapisać dokładny fragment WAV.";
        }
    }

    public string? ResultPath { get; private set; }

    private void Window_Loaded(object sender, RoutedEventArgs e) => FormatCombo.Focus();

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (_exporting) return;
        var format = SelectedFormat();
        var extension = AudioClipExporter.SuggestedExtension(_sourcePath, format);
        var dialog = new SaveFileDialog
        {
            Title = _contentKind == "rozdział"
                ? "Zapisz rozdział jako nowy plik"
                : "Zapisz zaznaczony fragment jako nowy plik",
            AddExtension = true,
            DefaultExt = extension,
            FileName = SanitizeFileName(_sourceTitle) + $" - {_contentKind}" + extension,
            Filter = format switch
            {
                AudioClipExportFormat.Flac => "Plik FLAC (*.flac)|*.flac",
                AudioClipExportFormat.Wav => "Plik WAV (*.wav)|*.wav",
                _ => $"Oryginalny format (*{extension})|*{extension}"
            },
            OverwritePrompt = true
        };
        if (dialog.ShowDialog(this) != true) return;

        _exporting = true;
        SaveButton.IsEnabled = false;
        FormatCombo.IsEnabled = false;
        ExportProgress.Visibility = Visibility.Visible;
        ExportProgress.Value = 0;
        ExportStatus.Text = $"Zapisywanie {_contentKind}u…";
        CancelButton.Content = "_Przerwij";
        _exportCancellation = new CancellationTokenSource();
        var progress = new Progress<double>(value =>
        {
            ExportProgress.Value = Math.Clamp(value * 100d, 0d, 100d);
            ExportStatus.Text = $"Zapisano {Math.Round(ExportProgress.Value):0}%.";
        });

        try
        {
            await AudioClipExporter.ExportAsync(
                new AudioClipExportRequest(_sourcePath, dialog.FileName, _start, _end, format),
                progress,
                _exportCancellation.Token);
            ResultPath = dialog.FileName;
            ExportStatus.Text = _contentKind == "rozdział" ? "Rozdział zapisany." : "Fragment zapisany.";
            _allowClose = true;
            DialogResult = true;
        }
        catch (OperationCanceledException)
        {
            ExportStatus.Text = "Zapisywanie przerwane. Plik częściowy został usunięty.";
            ResetAfterExport();
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or InvalidDataException
            or InvalidOperationException
            or NotSupportedException
            or ArgumentException)
        {
            ExportStatus.Text = exception.Message;
            System.Windows.MessageBox.Show(
                this,
                exception.Message,
                _contentKind == "rozdział" ? "Nie udało się zapisać rozdziału" : "Nie udało się zapisać fragmentu",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            ResetAfterExport();
        }
        finally
        {
            _exportCancellation?.Dispose();
            _exportCancellation = null;
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        if (_exporting)
        {
            ExportStatus.Text = "Przerywanie zapisywania…";
            _exportCancellation?.Cancel();
            return;
        }
        _allowClose = true;
        DialogResult = false;
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_allowClose || !_exporting) return;
        e.Cancel = true;
        ExportStatus.Text = "Przerywanie zapisywania…";
        _exportCancellation?.Cancel();
    }

    private void ResetAfterExport()
    {
        _exporting = false;
        SaveButton.IsEnabled = true;
        FormatCombo.IsEnabled = true;
        CancelButton.Content = "_Anuluj";
        FormatCombo.Focus();
    }

    private AudioClipExportFormat SelectedFormat()
    {
        var tag = (FormatCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        return Enum.TryParse<AudioClipExportFormat>(tag, out var format)
            ? format
            : AudioClipExportFormat.Wav;
    }

    private static string FormatTime(TimeSpan value) =>
        value.TotalHours >= 1
            ? value.ToString(@"h\:mm\:ss\.fff")
            : value.ToString(@"m\:ss\.fff");

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(value.Select(character => invalid.Contains(character) ? '_' : character).ToArray())
            .Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? "fragment audio" : cleaned;
    }
}
