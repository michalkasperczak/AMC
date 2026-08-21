using System.Windows;
using System.Windows.Input;
using AccessibleMediaController.Core.LocalMedia;
using Microsoft.Win32;

namespace AccessibleMediaController.Windows;

public sealed record LocalSourceActionResult(string Message, string? SelectedSourceId = null);

public partial class LocalSourcesWindow : Window
{
    private readonly Func<IReadOnlyList<LocalFolderSourceStatus>> _loadStatuses;
    private readonly Func<string, Task<LocalSourceActionResult>> _addSource;
    private readonly Func<IReadOnlyCollection<string>, Task<LocalSourceActionResult>> _refreshSources;
    private readonly Func<string, LocalSourceActionResult> _detachSource;
    private readonly Action<string> _exportBackup;

    public LocalSourcesWindow(
        Func<IReadOnlyList<LocalFolderSourceStatus>> loadStatuses,
        Func<string, Task<LocalSourceActionResult>> addSource,
        Func<IReadOnlyCollection<string>, Task<LocalSourceActionResult>> refreshSources,
        Func<string, LocalSourceActionResult> detachSource,
        Action<string> exportBackup)
    {
        InitializeComponent();
        _loadStatuses = loadStatuses;
        _addSource = addSource;
        _refreshSources = refreshSources;
        _detachSource = detachSource;
        _exportBackup = exportBackup;
        ReloadStatuses();
        Loaded += (_, _) => SourcesList.Focus();
    }

    private LocalFolderSourceStatus? SelectedStatus => SourcesList.SelectedItem as LocalFolderSourceStatus;

    private void ReloadStatuses(string? preferredSourceId = null)
    {
        preferredSourceId ??= SelectedStatus?.Id;
        var statuses = _loadStatuses();
        SourcesList.ItemsSource = statuses;
        SourcesList.SelectedItem = statuses.FirstOrDefault(status =>
            string.Equals(status.Id, preferredSourceId, StringComparison.Ordinal));
        if (SourcesList.SelectedItem is null && statuses.Count > 0) SourcesList.SelectedIndex = 0;
        UpdateSelectionState();
    }

    private async void AddSource_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Dodaj źródło Biblioteki lokalnej",
            Multiselect = false
        };
        if (dialog.ShowDialog(this) != true) return;
        await RunAsync(() => _addSource(dialog.FolderName));
    }

    private async void RefreshSelected_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedStatus is not { } status) return;
        await RunAsync(() => _refreshSources([status.Id]));
    }

    private async void RefreshAll_Click(object sender, RoutedEventArgs e) =>
        await RunAsync(() => _refreshSources([]));

    private void DetachSource_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedStatus is not { } status) return;
        var answer = MessageBox.Show(
            this,
            $"Odłączyć źródło „{status.DisplayName}” od automatycznej synchronizacji?\n\n"
            + "Pliki na dysku nie zostaną usunięte. AMC zachowa wpisy Biblioteki, Ulubione, kolejkę, historię, zakładki i pozycje odtwarzania.",
            "Bezpieczne odłączenie źródła",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No);
        if (answer != MessageBoxResult.Yes) return;

        try
        {
            var result = _detachSource(status.Id);
            OperationStatusText.Text = result.Message;
            ReloadStatuses(result.SelectedSourceId);
            SourcesList.Focus();
        }
        catch (Exception exception)
        {
            OperationStatusText.Text = $"Nie można odłączyć źródła: {exception.Message}";
        }
    }

    private void ExportBackup_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Eksportuj pełną kopię AMC",
            Filter = "Pełna kopia programu|*.amcbackup.json",
            DefaultExt = ".amcbackup.json",
            FileName = $"AMC-pelna-kopia-{DateTime.Now:yyyy-MM-dd}.amcbackup.json",
            AddExtension = true
        };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            _exportBackup(dialog.FileName);
            OperationStatusText.Text = "Wyeksportowano pełną kopię AMC: katalog Biblioteki, źródła, zakładki, historię, pozycje, kolejki i ustawienia.";
        }
        catch (Exception exception)
        {
            OperationStatusText.Text = $"Nie można wyeksportować kopii: {exception.Message}";
        }
    }

    private async Task RunAsync(Func<Task<LocalSourceActionResult>> operation)
    {
        SetBusy(true);
        try
        {
            var result = await operation();
            OperationStatusText.Text = result.Message;
            ReloadStatuses(result.SelectedSourceId);
            SourcesList.Focus();
        }
        catch (Exception exception)
        {
            OperationStatusText.Text = $"Operacja nie powiodła się: {exception.Message}";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        AddSourceButton.IsEnabled = !busy;
        RefreshAllButton.IsEnabled = !busy;
        ExportBackupButton.IsEnabled = !busy;
        RefreshSelectedButton.IsEnabled = !busy && SelectedStatus is not null;
        DetachSourceButton.IsEnabled = !busy && SelectedStatus is not null;
        if (busy) OperationStatusText.Text = "Trwa operacja…";
    }

    private void SourcesList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) =>
        UpdateSelectionState();

    private void UpdateSelectionState()
    {
        var status = SelectedStatus;
        RefreshSelectedButton.IsEnabled = status is not null;
        DetachSourceButton.IsEnabled = status is not null;
        SourceDetailsText.Text = status is null
            ? "Brak zarejestrowanych źródeł. Dodaj folder, aby objąć go automatyczną synchronizacją."
            : $"{(status.IsReachable ? "Źródło dostępne" : "Źródło chwilowo niedostępne; rekordy pozostają w AMC")}. "
              + $"Aktywne pliki: {status.ActiveItemCount}. Niedostępne: {status.UnavailableItemCount}. Wykluczone: {status.ExcludedItemCount}. "
              + (string.IsNullOrWhiteSpace(status.OverlapWarning) ? string.Empty : $"Uwaga: {status.OverlapWarning}. ")
              + $"Ścieżka: {status.Path}";
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        Close();
        e.Handled = true;
    }
}
