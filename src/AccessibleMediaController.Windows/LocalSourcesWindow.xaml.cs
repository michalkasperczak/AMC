using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Threading;
using AccessibleMediaController.Core.Configuration;
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
    private readonly Func<string, ResumePositionMode, LocalSourceActionResult> _setResumePositionMode;
    private readonly Action<string> _exportBackup;

    public LocalSourcesWindow(
        Func<IReadOnlyList<LocalFolderSourceStatus>> loadStatuses,
        Func<string, Task<LocalSourceActionResult>> addSource,
        Func<IReadOnlyCollection<string>, Task<LocalSourceActionResult>> refreshSources,
        Func<string, LocalSourceActionResult> detachSource,
        Func<string, ResumePositionMode, LocalSourceActionResult> setResumePositionMode,
        Action<string> exportBackup)
    {
        InitializeComponent();
        _loadStatuses = loadStatuses;
        _addSource = addSource;
        _refreshSources = refreshSources;
        _detachSource = detachSource;
        _setResumePositionMode = setResumePositionMode;
        _exportBackup = exportBackup;
        ResumePositionModeCombo.ItemsSource = new ResumeChoice[]
        {
            new(ResumePositionMode.Remember, "Pamiętaj pozycję odtwarzania"),
            new(ResumePositionMode.StartFromBeginning, "Zawsze od początku"),
            new(ResumePositionMode.Inherit, "Zgodnie z ustawieniem globalnym")
        };
        ReloadStatuses();
        Loaded += (_, _) => Dispatcher.BeginInvoke(FocusSelectedFolder, DispatcherPriority.ContextIdle);
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
            Title = "Dodaj folder do Biblioteki",
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
            $"Odłączyć folder „{status.DisplayName}” od automatycznej synchronizacji?\n\n"
            + "Pliki na dysku nie zostaną usunięte. AMC zachowa wpisy Biblioteki, Ulubione, kolejkę, historię, zakładki i pozycje odtwarzania.",
            "Bezpieczne odłączenie folderu",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No);
        if (answer != MessageBoxResult.Yes) return;

        try
        {
            var result = _detachSource(status.Id);
            OperationStatusText.Text = result.Message;
            ReloadStatuses(result.SelectedSourceId);
            FocusSelectedFolder();
        }
        catch (Exception exception)
        {
            OperationStatusText.Text = $"Nie można odłączyć folderu: {exception.Message}";
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
            OperationStatusText.Text = "Wyeksportowano pełną kopię AMC: katalog Biblioteki, foldery, zakładki, historię, pozycje, kolejki i ustawienia.";
        }
        catch (Exception exception)
        {
            OperationStatusText.Text = $"Nie można wyeksportować kopii: {exception.Message}";
        }
    }

    private void SaveResumePositionMode_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedStatus is not { } status
            || ResumePositionModeCombo.SelectedItem is not ResumeChoice selected)
        {
            return;
        }

        try
        {
            var result = _setResumePositionMode(status.Id, selected.Value);
            OperationStatusText.Text = result.Message;
            ReloadStatuses(result.SelectedSourceId ?? status.Id);
            ResumePositionModeCombo.Focus();
            Keyboard.Focus(ResumePositionModeCombo);
        }
        catch (Exception exception)
        {
            OperationStatusText.Text = $"Nie można zapisać ustawienia pozycji: {exception.Message}";
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
            FocusSelectedFolder();
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
        ResumePositionModeCombo.IsEnabled = !busy && SelectedStatus is not null;
        SaveResumePositionModeButton.IsEnabled = !busy && SelectedStatus is not null;
        if (busy) OperationStatusText.Text = "Trwa operacja…";
    }

    private void SourcesList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) =>
        UpdateSelectionState();

    private void UpdateSelectionState()
    {
        var status = SelectedStatus;
        RefreshSelectedButton.IsEnabled = status is not null;
        DetachSourceButton.IsEnabled = status is not null;
        ResumePositionModeCombo.IsEnabled = status is not null;
        SaveResumePositionModeButton.IsEnabled = status is not null;
        ResumePositionModeCombo.SelectedItem = ResumePositionModeCombo.Items
            .OfType<ResumeChoice>()
            .FirstOrDefault(item => item.Value == status?.ResumePositionMode)
            ?? ResumePositionModeCombo.Items.OfType<ResumeChoice>().FirstOrDefault();
        SourceDetailsText.Text = status is null
            ? "Brak Folderów Biblioteki. Dodaj folder, aby objąć go automatyczną synchronizacją."
            : $"{(status.IsReachable ? "Folder dostępny" : "Folder chwilowo niedostępny; rekordy pozostają w AMC")}. "
              + $"Aktywne pliki: {status.ActiveItemCount}. Niedostępne: {status.UnavailableItemCount}. Wykluczone: {status.ExcludedItemCount}. "
              + $"Pamiętanie pozycji: {status.ResumePositionLabel}. "
              + (string.IsNullOrWhiteSpace(status.OverlapWarning) ? string.Empty : $"Uwaga: {status.OverlapWarning}. ")
              + $"Ścieżka: {status.Path}";
    }

    private void FocusSelectedFolder()
    {
        if (SourcesList.Items.Count > 0 && SourcesList.SelectedIndex < 0)
        {
            SourcesList.SelectedIndex = 0;
        }
        if (SourcesList.SelectedItem is not null)
        {
            SourcesList.ScrollIntoView(SourcesList.SelectedItem);
        }
        SourcesList.UpdateLayout();
        if (SourcesList.ItemContainerGenerator.ContainerFromItem(SourcesList.SelectedItem) is ListBoxItem item)
        {
            item.Focus();
            Keyboard.Focus(item);
            return;
        }
        SourcesList.Focus();
        Keyboard.Focus(SourcesList);
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        Close();
        e.Handled = true;
    }

    private sealed record ResumeChoice(ResumePositionMode Value, string Label)
    {
        public override string ToString() => Label;
    }
}
